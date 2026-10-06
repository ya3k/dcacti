using GameServer.Application.Battle;
using GameServer.Application.Cards;
using GameServer.Application.Tests.Balance;
using GameServer.Domain.Battle;
using GameServer.Domain.Cards;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Relics;

namespace GameServer.Application.Tests.Balance;

/// <summary>
/// The deterministic balance simulation harness (<c>TASK-193</c>).
///
/// <b>It executes the real authoritative gameplay code and reimplements none of
/// it.</b> A run drives the production <see cref="BattleStateService"/> — the
/// same pipeline the SignalR hub calls — which in turn runs the real
/// <c>SwapExecutor</c>, <c>CardCastExecutor</c>, damage pipeline, resource
/// generation, Relic stage, Boss Response, and Passive tracking. The harness
/// supplies configuration and a policy, then <i>reads</i> what those stages
/// produced.
///
/// <code>
/// BalanceSimulator
///       ↓
/// BattleStateService.CreateBattleAsync / ExecuteSwapAsync / ExecuteCardCastAsync
///       ↓
/// SwapExecutor · CardCastExecutor · DamagePipeline · ResourceGenerator
///   · RelicResolver · PassiveTracker · Boss Response
///       ↓
/// committed BattleState + ordered BattleEvent list
///       ↓
/// metrics (summed and counted, never recomputed)
/// </code>
///
/// <b>If this type ever computes a damage number, a match result, a resource
/// amount, or a Turn outcome, its measurements are void.</b> Every metric is
/// derived from the resolutions' own outputs: the committed state and the events
/// the authoritative stages emitted.
///
/// <b>Determinism.</b> A run's inputs (<see cref="BalanceSimulationConfiguration"/>)
/// fully determine its result. The seed is supplied explicitly to
/// <c>CreateBattleAsync</c>, the store is an in-process double, the policy is a
/// pure function, and nothing reads a clock, a network, a browser, or an external
/// service (<c>TASK-193</c> §4.2). A second run over identical inputs reproduces
/// the first exactly.
/// </summary>
internal sealed class BalanceSimulator
{
    private readonly IBattleStateRepository _repository;
    private readonly BalanceSimulationConfiguration _configuration;
    private readonly CardDefinitionLookup _cardDefinitions;
    private readonly BalancePlayerPolicy _policy;

    /// <summary>
    /// The production pipeline this run drives. It is created once and reused for
    /// every action, because <see cref="BattleStateService"/> holds per-battle
    /// configuration (the resolved Boss and the equipped Relic definitions)
    /// between the battle's creation and its later resolutions. A fresh service
    /// per action would lose that configuration and the resolutions would fail.
    /// </summary>
    private readonly BattleStateService _service;

    /// <summary>
    /// Creates a simulator for one configuration.
    /// </summary>
    /// <param name="configuration">The complete input set (<c>§4.1</c>).</param>
    /// <param name="repository">
    /// The battle-state store. An in-process double keeps the run isolated and
    /// free of any external service (<c>§4.2</c>, <c>H-10</c>) while still
    /// exercising the service's real compare-and-set path.
    /// </param>
    /// <exception cref="ArgumentNullException">Any argument is <c>null</c>.</exception>
    public BalanceSimulator(
        BalanceSimulationConfiguration configuration,
        IBattleStateRepository repository)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(repository);

        _configuration = configuration;
        _repository = repository;
        _cardDefinitions = new CardDefinitionLookup(configuration.CardDefinitions);
        _policy = configuration.Policy;
        _service = new BattleStateService(
            repository,
            new BalanceFixedSeedSource(configuration.Seed),
            battleResults: null,
            cardDefinitions: _cardDefinitions);
    }

    /// <summary>
    /// Runs the simulation to a terminal outcome or the configured safety limit
    /// (<c>TASK-193</c> §5.1) and returns the result record (<c>H-09</c>).
    /// </summary>
    public async Task<BalanceSimulationResult> RunAsync()
    {
        // ---------------------------------------------------------------
        // Initialize — validate the configuration before any state exists, so an
        // invalid setup is reported as INVALID_SIMULATION rather than surfacing
        // as a mid-run fault (TASK-193 §5.3).
        // ---------------------------------------------------------------
        if (Validate() is { } invalid)
        {
            return Invalid(invalid);
        }

        var mode = _configuration.Mode;

        BattleState state;

        try
        {
            // -----------------------------------------------------------
            // Configure deterministic state — the seed is supplied explicitly,
            // which is the reproducibility ADR-009 / GAME_STATE.md §2.6.2 item 4
            // require. Battle creation runs the real production path, including
            // the OnBattleStart Relic firing point.
            // -----------------------------------------------------------
            state = await CreateBattleAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Invalid($"battle creation failed: {ex.Message}");
        }

        var accumulator = new MetricAccumulator(_configuration);

        // The Swap candidates this Turn has already had rejected. A rejected
        // Swap changes no state (MATCH3_RULES.md §2.1.5), so without this the
        // policy would re-propose the same pair forever. It is cleared whenever a
        // Swap actually commits, because a new committed Turn is a new board.
        var attemptedSwaps = new HashSet<int>();

        // ---------------------------------------------------------------
        // Simulate Turn → collect metrics → continue, until terminal or limit.
        // ---------------------------------------------------------------
        while (true)
        {
            // The safety limit is checked BEFORE proposing an action, so a
            // non-terminal run stops with exactly `MaxTurns` resolved Turns and
            // reports STALEMATE (H-05, H-08).
            if (state.Turn >= _configuration.MaxTurns)
            {
                return Build(
                    state,
                    accumulator,
                    mode,
                    BalanceSimulationOutcome.Stalemate,
                    $"reached the configured maximum of {_configuration.MaxTurns} Turns without a terminal result");
            }

            if (attemptedSwaps.Count == 0)
            {
                var turnNumber = state.Turn + 1;

                // Open this Turn for accumulation. All of its resolutions — the
                // auxiliary Card cast and the committed Swap — contribute to the same
                // Turn's metrics, which EndTurn flushes exactly once.
                accumulator.BeginTurn(turnNumber);
            }

            // --- Card cast (an auxiliary action within the Turn) ------------
            // CARD_RULES.md §3 item 6 permits at most one successful cast per
            // committed Turn; the production path enforces it, and a rejection is
            // recorded as gameplay rather than retried.
            if (_policy.ProposeCast(state, Context, attemptedSwaps) is { } cardId)
            {
                var cast = await Service.ExecuteCardCastAsync(
                    _configuration.BattleId,
                    cardId).ConfigureAwait(false);

                if (cast is null)
                {
                    return Invalid("the battle record disappeared during a Card cast.");
                }

                if (cast.Value.IsRejected)
                {
                    accumulator.RecordRejectedCast(cast.Value.Reason);
                }
                else
                {
                    state = cast.Value.State;
                    accumulator.RecordResolution(cast.Value.Events);

                    // A cast that ended the battle reconciles terminal state and damage
                    // without emitting an uncommitted phantom Turn T+1 record into the
                    // per-Turn series (TASK-195 §4.2, §5.1.2).
                    if (Terminal(cast.Value.Events) is { } castOutcome)
                    {
                        accumulator.ReconcileTerminalCardCast(state);
                        return Build(state, accumulator, mode, castOutcome, $"Card cast resolved the battle ({castOutcome}).");
                    }
                }
            }

            // --- Committed Swap -------------------------------------------
            // The policy proposes; the production validator decides. A rejected
            // proposal begins no Turn (MATCH3_RULES.md §2.1.5 item 2), so the
            // loop records it and re-proposes against the unchanged state — with
            // the attempted candidate excluded, so the policy advances instead of
            // repeating itself.
            var proposal = _policy.ProposeSwap(state, Context, attemptedSwaps);

            if (proposal is not { } swap)
            {
                return Build(
                    state,
                    accumulator,
                    mode,
                    BalanceSimulationOutcome.Stalemate,
                    "the policy exhausted every legal Swap candidate without a terminal result");
            }

            var swapResult = await Service.ExecuteSwapAsync(
                _configuration.BattleId,
                swap).ConfigureAwait(false);

            if (swapResult is null)
            {
                return Invalid("the battle record disappeared during a Swap.");
            }

            if (swapResult.Value.IsRejected)
            {
                accumulator.RecordRejectedSwap(swapResult.Value.Reason);
                attemptedSwaps.Add(BalancePlayerPolicy.PairKey(swap.From, swap.To));

                continue;
            }

            accumulator.ResetRejectedSwapStreak();
            attemptedSwaps.Clear();
            state = swapResult.Value.State;
            accumulator.RecordResolution(swapResult.Value.Events);
            accumulator.EndTurn(state);

            if (Terminal(swapResult.Value.Events) is { } outcome)
            {
                return Build(state, accumulator, mode, outcome, $"the committed Swap resolved the battle ({outcome}).");
            }
        }
    }

    /// <summary>The production battle pipeline this run drives.</summary>
    private BattleStateService Service => _service;

    private BalancePolicyContext Context => new(
        _configuration.Pet.EquippedCards?.Select(card => card.Value).ToArray() ?? [],
        cardId => _cardDefinitions.CostOf(cardId));

    /// <summary>
    /// Creates the battle through the production path with the configuration's
    /// explicit seed (<c>GAME_STATE.md</c> §2.6.1 item 3).
    /// </summary>
    private Task<BattleState> CreateBattleAsync() => Service.CreateBattleAsync(
        _configuration.BattleId,
        _configuration.PlayerId,
        _configuration.Pet,
        _configuration.Boss,
        new BattleSeed(_configuration.Seed),
        cancellationToken: default,
        equippedRelicDefinitions: _configuration.RelicDefinitions);

    /// <summary>
    /// The terminal outcome the production pipeline itself reported, or
    /// <c>null</c> when the resolution ended non-terminally. It reads the
    /// pipeline's own <c>BattleWon</c>/<c>BattleLost</c> outcomes rather than
    /// comparing HP, so the harness cannot disagree with the rules about who won
    /// (<c>GAME_EVENTS.md</c> §2, <c>GAME_RULES.md</c> §1.4).
    /// </summary>
    private static BalanceSimulationOutcome? Terminal(IReadOnlyList<BattleEvent> events)
    {
        foreach (var battleEvent in events)
        {
            if (battleEvent.Type == BattleEventType.BattleWon)
            {
                return BalanceSimulationOutcome.Victory;
            }

            if (battleEvent.Type == BattleEventType.BattleLost)
            {
                return BalanceSimulationOutcome.Defeat;
            }
        }

        return null;
    }

    /// <summary>
    /// Validates the configuration before any state exists. It returns the
    /// documented reason for an <see cref="BalanceSimulationOutcome.InvalidSimulation"/>
    /// run, or <c>null</c> when the configuration is runnable.
    /// </summary>
    private string? Validate()
    {
        if (_configuration.MaxTurns < 1)
        {
            return $"MaxTurns must be at least 1; received {_configuration.MaxTurns}.";
        }

        if (_configuration.Boss.MaxHP <= 0)
        {
            return "the Boss definition has no MaxHP.";
        }

        if (_configuration.Pet.EquippedCards is not { Length: > 0 })
        {
            return "the Pet configuration carries no equipped Cards; CARD_RULES.md §1 defines no zero-Card battle.";
        }

        if (_configuration.CardDefinitions.Count == 0)
        {
            return "no Card definitions were supplied, so no cast could ever resolve.";
        }

        return null;
    }

    /// <summary>
    /// Builds the result record, including the full metric set (<c>H-09</c>).
    /// </summary>
    private BalanceSimulationResult Build(
        BattleState state,
        MetricAccumulator accumulator,
        BalanceSimulationMode mode,
        BalanceSimulationOutcome outcome,
        string detail,
        BalanceSimulationOutcome? forceOutcome = null)
    {
        var effective = forceOutcome ?? outcome;

        return new BalanceSimulationResult
        {
            Seed = _configuration.Seed,
            Mode = mode,
            PolicyName = _policy.Name,
            Outcome = effective,
            OutcomeDetail = detail,
            MaxTurns = _configuration.MaxTurns,
            PlayerId = _configuration.PlayerId,
            PetId = state.PetState.PetId.Value,
            BossId = state.BossState.BossId.Value,
            Boss = _configuration.Boss,
            EquippedCardIds = state.PetState.EquippedCards?.Select(card => card.Value).ToArray() ?? [],
            EquippedRelicIds = state.PetState.EquippedRelics?.Select(relic => relic.Value).ToArray() ?? [],
            Metrics = accumulator.Build(state, effective),
            FinalState = effective == BalanceSimulationOutcome.InvalidSimulation ? null : state,
        };
    }

    /// <summary>
    /// The result for a configuration the simulation cannot legally run.
    /// </summary>
    private BalanceSimulationResult Invalid(string reason) => new()
    {
        Seed = _configuration.Seed,
        Mode = _configuration.Mode,
        PolicyName = _policy.Name,
        Outcome = BalanceSimulationOutcome.InvalidSimulation,
        OutcomeDetail = reason,
        MaxTurns = _configuration.MaxTurns,
        PlayerId = _configuration.PlayerId,
        PetId = _configuration.Pet.PetId.Value,
        BossId = _configuration.Boss.BossId.Value,
        Boss = _configuration.Boss,
        EquippedCardIds = _configuration.Pet.EquippedCards?.Select(card => card.Value).ToArray() ?? [],
        EquippedRelicIds = _configuration.Pet.EquippedRelics?.Select(relic => relic.Value).ToArray() ?? [],
        Metrics = MetricAccumulator.Empty(),
        FinalState = null,
    };
}

/// <summary>
/// An <see cref="ICardDefinitionLookup"/> over an in-memory definition set —
/// the same identity lookup a production lookup performs for equipped ids.
/// </summary>
internal sealed class CardDefinitionLookup : ICardDefinitionLookup
{
    private readonly Dictionary<string, CardDefinition> _definitions;

    public CardDefinitionLookup(IReadOnlyList<CardDefinition> definitions)
    {
        _definitions = new Dictionary<string, CardDefinition>(StringComparer.Ordinal);

        foreach (var definition in definitions)
        {
            _definitions[definition.CardDefinitionId] = definition;
        }
    }

    /// <inheritdoc />
    public Task<CardDefinition?> GetDefinitionAsync(
        string cardDefinitionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_definitions.TryGetValue(cardDefinitionId, out var definition) ? definition : null);

    /// <summary>
    /// A Card definition's authored <c>PowerCost</c>, or <c>0</c> for an unknown
    /// id — a policy consults this to decide only whether to <i>propose</i> a
    /// cast; the production validator is what decides.
    /// </summary>
    public int CostOf(string cardDefinitionId) =>
        _definitions.TryGetValue(cardDefinitionId, out var definition) ? definition.PowerCost : 0;
}

/// <summary>
/// A deterministic <see cref="IRngSeedSource"/> that returns the simulation's
/// configured seed (<c>GAME_STATE.md</c> §2.6.1).
///
/// It substitutes the entropy <i>source</i>, never the PRNG: the production
/// PRNG consumes the same seed and generates the same board stream
/// (<c>ADR-009</c>). No second random-number implementation exists here.
/// </summary>
internal sealed class BalanceFixedSeedSource : IRngSeedSource
{
    private readonly ulong _seed;

    public BalanceFixedSeedSource(ulong seed) => _seed = seed;

    /// <inheritdoc />
    public ulong CreateSeed() => _seed;
}
