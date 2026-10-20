using GameServer.Application.Battle;
using GameServer.Application.Cards;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Combat;
using GameServer.Domain.Match3;

namespace GameServer.Application.Tests;

/// <summary>
/// A pinned board plus the one committed Player Swap that acts on it, together
/// with the resolved cascade that Swap produces — the fixture an atomic-round
/// test pins before it calls <see cref="BattleStateService.ExecuteSwapAsync"/>.
/// </summary>
/// <param name="Board">The board the Player acts on (<c>BattleState.BoardState</c>).</param>
/// <param name="Swap">
/// The one adjacent pair that is legal on <paramref name="Board"/>, produces a Match,
/// and clears Gems only of <paramref name="MatchType"/>.
/// </param>
/// <param name="MatchType">The Gem type the Swap's resolved cascade clears.</param>
/// <param name="Resolution">The cascade the Swap produces (derived, not re-detected).</param>
/// <param name="Resources">The resources that cascade generates (<c>GAME_RULES.md</c> §17 step 12).</param>
internal sealed record PinnedPlayerRound(
    BoardState Board,
    SwapRequest Swap,
    GemType MatchType,
    CascadeResolver.CascadeResult Resolution,
    ResourceGeneration Resources);

/// <summary>
/// Test fixtures and event-list readings for the atomic-round contract
/// (<c>ADR-026</c>).
///
/// <b>Why a pinned board is needed.</b> Under <c>ADR-026</c> D1 attack damage is
/// gated on a confirmed ATK-Gem match, so a test that wants the Player's attack to
/// qualify must control <em>which Gem type the committed Swap clears</em> — a
/// board drawn from the battle's own RNG cannot offer that. The fixtures below
/// build such a board by construction and verify their own preconditions against
/// the real pipeline, so a fixture that stopped producing the promised Match
/// fails loudly instead of silently weakening a test.
///
/// <b>No gameplay rule lives here.</b> Every value is produced by the Domain
/// stages the production pipeline uses (<see cref="MatchDetector"/>,
/// <see cref="CascadeResolver"/>, <see cref="ResourceGenerator"/>) — this type
/// decides no rule and reimplements no step.
/// </summary>
internal static class RoundTestSupport
{
    /// <summary>
    /// A board with no pre-existing Match on which exactly one adjacent Swap
    /// produces a Match of <paramref name="matchType"/>, and whose resolved cascade
    /// clears Gems of <paramref name="matchType"/> only.
    ///
    /// <b>It is the fixture the ATK gate is read against.</b> For
    /// <see cref="GemType.Atk"/> the cascade clears at least one ATK Gem, so the
    /// acting side produces a qualifying attack; for every other type it clears
    /// none, so no attack may occur (<c>ADR-026</c> D1, Scope A). The search is
    /// deterministic — the first accepted placement in ascending cell order — so
    /// the same <paramref name="rngState"/> always yields the same fixture.
    /// </summary>
    /// <param name="matchType">The Gem type the fixture's Swap must clear.</param>
    /// <param name="rngState">
    /// The battle's authoritative PRNG state (<c>GAME_STATE.md</c> §2.6.2), which
    /// the fixture resolves its cascade against exactly as the production path does.
    /// </param>
    /// <param name="excluded">
    /// A pair the fixture must not return — normally the battle's
    /// <c>LastCommittedSwapPair</c>, because a Swap already recorded as committed is
    /// rejected as already applied (<c>MATCH3_RULES.md</c> §2.1.4 item 2). A
    /// multi-round scenario passes it so every round's Swap is a new logical action.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// No placement satisfies the fixture's preconditions. It is a fixture defect,
    /// never a production failure.
    /// </exception>
    public static PinnedPlayerRound PlayerMatch(
        GemType matchType,
        RngState rngState,
        CommittedSwapPair? excluded = null) =>
        TryPlayerMatch(matchType, rngState, excluded)
        ?? throw new InvalidOperationException(
            $"ADR-026 fixture for '{matchType}' could not be constructed. This is a test-fixture "
            + "defect: the board builder no longer produces a single-type Match opportunity.");

    /// <summary>
    /// The fixture for a played round, together with the RNG stream the battle must be
    /// holding for it to hold: the first candidate stream, in a fixed deterministic
    /// order, that satisfies <see cref="PlayerMatch"/>'s preconditions.
    ///
    /// <b>Why a stream is chosen rather than assumed.</b> The strict fixture only
    /// accepts a stream whose cascades clear one Gem type, so a scenario that plays
    /// several rounds from one evolving stream may reach a round whose stream admits
    /// no such arrangement. The scenario then re-pins the stream — the
    /// <c>RngState</c> is <b>fixture state</b> (<c>GAME_STATE.md</c> §2.6.2), never an
    /// asserted value, and no gameplay rule is derived from it (<c>ADR-009</c>): the
    /// scenario decides which stream to play and then asserts the documented
    /// contracts on that round.
    /// </summary>
    /// <param name="matchType">The Gem type the fixture's Swap must clear.</param>
    /// <param name="starting">
    /// The stream the scenario would otherwise continue — the search starts there, so a
    /// scenario whose own stream already satisfies the fixture keeps it.
    /// </param>
    /// <param name="excluded">A pair the fixture must not return (<c>MATCH3_RULES.md</c> §2.1.4 item 2).</param>
    /// <exception cref="InvalidOperationException">
    /// No candidate stream satisfies the fixture. It is a fixture defect, never a
    /// production failure.
    /// </exception>
    public static (RngState RngState, PinnedPlayerRound Pinned) PinnedForStream(
        GemType matchType,
        RngState starting,
        CommittedSwapPair? excluded = null)
    {
        for (ulong offset = 0; offset < 256; offset++)
        {
            var candidate = new RngState(starting.State + offset, starting.Increment);

            if (TryPlayerMatch(matchType, candidate, excluded) is { } pinned)
            {
                return (candidate, pinned);
            }
        }

        throw new InvalidOperationException(
            $"ADR-026 fixture for '{matchType}' could not be constructed against any of the 256 "
            + "candidate streams. This is a test-fixture defect, never a production failure.");
    }

    /// <summary>
    /// A board + stream whose committed Player Swap resolves in <b>at least two
    /// detection passes</b> — a Swap whose first pass's Gravity and Spawn produce a
    /// Cascade (<c>MATCH3_RULES.md</c> §4.2) — so a scenario can assert how a
    /// multi-pass cycle is aggregated.
    /// </summary>
    /// <param name="matchType">The Gem type the fixture's Swap must clear.</param>
    /// <param name="starting">
    /// The stream the search starts from. The Board's own Matches are fixed; whether
    /// the Spawn after the first pass completes a second Match depends on the
    /// stream, so the search re-pins the stream rather than assuming one
    /// (<c>GAME_STATE.md</c> §2.6.2 — the stream is fixture state, never an asserted
    /// value).
    /// </param>
    /// <param name="excluded">A pair the fixture must not return.</param>
    public static (RngState RngState, PinnedPlayerRound Pinned) PlayerCascadeMatch(
        GemType matchType,
        RngState starting,
        CommittedSwapPair? excluded = null)
    {
        for (ulong offset = 0; offset < 256; offset++)
        {
            var candidate = new RngState(starting.State + offset, starting.Increment);

            if (TryPlayerMatch(
                    matchType,
                    candidate,
                    excluded,
                    (_, resolution) => resolution.Passes.Count >= 2) is { } pinned)
            {
                return (candidate, pinned);
            }
        }

        throw new InvalidOperationException(
            $"The multi-pass cascade fixture for '{matchType}' could not be constructed against any "
            + "of the 256 candidate streams. This is a test-fixture defect, never a production "
            + "failure.");
    }

    /// <summary>
    /// A board + stream whose committed Player Swap clears the requested Gem type
    /// <b>and at least one other type</b>, so a scenario can assert that one cycle's
    /// summary reports several Gem types at once.
    /// </summary>
    /// <param name="matchType">
    /// A Gem type the fixture's Swap must clear. The cascade clears at least one
    /// other type as well, which is what makes the summary multi-entry.
    /// </param>
    /// <param name="starting">The stream the search starts from.</param>
    /// <param name="excluded">A pair the fixture must not return.</param>
    public static (RngState RngState, PinnedPlayerRound Pinned) PlayerMultiTypeMatch(
        GemType matchType,
        RngState starting,
        CommittedSwapPair? excluded = null)
    {
        for (ulong offset = 0; offset < 256; offset++)
        {
            var candidate = new RngState(starting.State + offset, starting.Increment);

            if (TryPlayerMatch(
                    matchType,
                    candidate,
                    excluded,
                    (generation, _) =>
                        generation.ClearedGemCounts.Total
                        > generation.ClearedGemCounts.For(matchType)) is { } pinned)
            {
                return (candidate, pinned);
            }
        }

        throw new InvalidOperationException(
            $"The multi-type fixture for '{matchType}' could not be constructed against any of the "
            + "256 candidate streams. This is a test-fixture defect, never a production failure.");
    }

    /// <summary>
    /// The fixture search shared by every public fixture above.
    /// </summary>
    /// <param name="matchType">The Gem type the fixture's Swap must clear.</param>
    /// <param name="rngState">The stream the cascade is resolved against.</param>
    /// <param name="excluded">A pair the fixture must not return.</param>
    /// <param name="accept">
    /// An additional acceptance predicate for a fixture that needs more than the
    /// default preconditions. When it is supplied, the default "clears only
    /// <paramref name="matchType"/>" precondition is replaced by it — the caller
    /// states what it needs instead of inheriting a constraint it does not want.
    /// </param>
    private static PinnedPlayerRound? TryPlayerMatch(
        GemType matchType,
        RngState rngState,
        CommittedSwapPair? excluded,
        Func<ResourceGeneration, CascadeResolver.CascadeResult, bool>? accept = null)
    {
        for (var row = 0; row + 1 < BoardState.Rows; row++)
        {
            for (var column = 0; column + 2 < BoardState.Columns; column++)
            {
                var board = WithSingleMatchOpportunity(matchType, row, column);

                if (MatchDetector.Detect(board).Count != 0)
                {
                    continue;
                }

                var from = BoardState.ToIndex(row, column + 2);
                var to = BoardState.ToIndex(row + 1, column + 2);

                if (excluded is { } alreadyCommitted
                    && alreadyCommitted.MatchesPair(from, to))
                {
                    continue;
                }

                var swap = new SwapRequest(from, to);

                var resolution = CascadeResolver.Resolve(
                    board.WithSwapped(from, to),
                    new Pcg32(rngState.State, rngState.Increment));

                if (resolution.Passes.Count == 0)
                {
                    continue;
                }

                var resources = ResourceGenerator.Generate(resolution);
                var cleared = resources.ClearedGemCounts;

                // The gate the fixture exists to control: the cascade clears the
                // requested type and, when that type is not ATK, clears no ATK Gem at
                // all — so the acting side cannot be a qualifying attacker.
                if (cleared.For(matchType) == 0)
                {
                    continue;
                }

                if (accept is null)
                {
                    if (matchType != GemType.Atk && cleared.Atk != 0)
                    {
                        continue;
                    }

                    // Only the requested type is cleared, so an assertion about "the
                    // Swap's Gems" is an assertion about one Gem type.
                    if (cleared.Total != cleared.For(matchType))
                    {
                        continue;
                    }
                }
                else if (!accept(resources, resolution))
                {
                    continue;
                }

                return new PinnedPlayerRound(board, swap, matchType, resolution, resources);
            }
        }

        return null;
    }

    /// <summary>
    /// The board the fixture's Swap acts on: a three-way Gem cycle that contains no
    /// Match, with two <paramref name="matchType"/> Gems placed on
    /// <paramref name="row"/> and one immediately below the third column, so
    /// swapping the third cell down completes a three-in-a-row
    /// (<c>MATCH3_RULES.md</c> §2 item 3).
    /// </summary>
    private static BoardState WithSingleMatchOpportunity(GemType matchType, int row, int column)
    {
        // MATCH3_RULES.md §1.1's four Gem types in a repeating cycle: no three
        // consecutive cells of one type occur horizontally or vertically, so the
        // board starts stable.
        GemType[] cycle = [GemType.Atk, GemType.Def, GemType.Power];
        GemType[] filler = [GemType.Def, GemType.Power, GemType.Hp];

        var cells = new GemType[BoardState.CellCount];

        for (var r = 0; r < BoardState.Rows; r++)
        {
            for (var c = 0; c < BoardState.Columns; c++)
            {
                cells[BoardState.ToIndex(r, c)] = cycle[(r + c) % cycle.Length];
            }
        }

        // The first two cells of the would-be Match, deliberately not yet a Match
        // (the third column is not of this type), and the completing Gem one row
        // below it.
        cells[BoardState.ToIndex(row, column)] = matchType;
        cells[BoardState.ToIndex(row, column + 1)] = matchType;
        cells[BoardState.ToIndex(row + 1, column + 2)] = matchType;

        // Keep the surrounding cells off the match type so the placement itself
        // introduces no Match and the arriving Gem completes exactly one.
        foreach (var (r, c) in new[]
        {
            (row, column + 2),
            (row, column - 1),
            (row + 1, column + 1),
            (row + 1, column + 3),
        })
        {
            if (r < 0 || r >= BoardState.Rows || c < 0 || c >= BoardState.Columns)
            {
                continue;
            }

            var index = BoardState.ToIndex(r, c);

            if (cells[index] == matchType)
            {
                cells[index] = filler[(r + c) % filler.Length];
            }
        }

        return BoardState.FromCells(cells);
    }

    /// <summary>
    /// The Match-3 board events of the <b>Player action phase</b> — the first
    /// contiguous run of board events in the committed round's own event list
    /// (<c>GAME_EVENTS.md</c> §1.1).
    /// </summary>
    /// <remarks>
    /// Read from the delivered list, not re-derived: the round's list is
    /// <c>[SwapStarted(player), SwapResolved(player), &lt;player board run&gt;, …,
    /// SwapStarted(boss), SwapResolved(boss), &lt;boss board run&gt;, …]</c>
    /// (<c>GAME_EVENTS.md</c> §1, <c>SIGNALR_PROTOCOL.md</c> §3.2.26–§3.2.27), so
    /// the action's boundary pair is <b>not</b> a board event
    /// (<see cref="IsBoardEvent"/>) and the player's cycle is the first run, not
    /// the list's prefix. Re-deriving it from <c>BattleEventBuilder</c> would
    /// answer a different list than the one the round delivered.
    /// </remarks>
    public static IReadOnlyList<BattleEvent> PlayerBoardEvents(SwapExecutionResult result)
    {
        var runs = BoardEventRuns(result);

        return runs.Count > 0 ? runs[0] : [];
    }

    /// <summary>
    /// The Match-3 board events of the <b>Boss response phase</b> — the second
    /// contiguous run of board events in the committed round's own event list
    /// (<c>GAME_EVENTS.md</c> §1, §1.1; <c>ADR-026</c> §2).
    /// </summary>
    /// <param name="result">The committed round's result.</param>
    /// <remarks>
    /// The Player's board cycle is the list's first run, and the only other board
    /// events in a committed round are the Boss's, so the second run is the Boss's
    /// cycle. Nothing is re-detected: the events are the resolution's own output,
    /// read by position.
    /// </remarks>
    public static IReadOnlyList<BattleEvent> BossBoardEvents(SwapExecutionResult result)
    {
        var runs = BoardEventRuns(result);

        return runs.Count > 1 ? runs[1] : [];
    }

    /// <summary>
    /// The round's contiguous runs of board events, in delivered order — the
    /// Player action's cycle first, the Boss response phase's second
    /// (<c>GAME_EVENTS.md</c> §1.1).
    /// </summary>
    /// <remarks>
    /// A run is maximal: it opens at the first board event that follows a
    /// non-board event (or the list's start) and closes at the first board event
    /// that is followed by a non-board one. The events between two runs — the
    /// action's boundary pair, the Passive/Relic/Power stage reports, the damage
    /// reports, and the terminal outcome — are not board events and are skipped,
    /// never reclassified: the classification stays the four-name set
    /// <see cref="IsBoardEvent"/> has always used, and admitting
    /// <c>SwapStarted</c>/<c>SwapResolved</c> does not add a fifth name to it.
    /// </remarks>
    private static IReadOnlyList<IReadOnlyList<BattleEvent>> BoardEventRuns(SwapExecutionResult result)
    {
        var events = result.Events;
        var runs = new List<IReadOnlyList<BattleEvent>>();
        var index = 0;

        while (index < events.Count)
        {
            if (!IsBoardEvent(events[index]))
            {
                index++;
                continue;
            }

            var start = index;

            while (index < events.Count && IsBoardEvent(events[index]))
            {
                index++;
            }

            var run = new List<BattleEvent>(index - start);

            for (var position = start; position < index; position++)
            {
                run.Add(events[position]);
            }

            runs.Add(run);
        }

        return runs;
    }

    /// <summary>
    /// Whether the Boss's board action cleared at least one ATK Gem — the acting
    /// side's qualifying-attack gate (<c>ADR-026</c> D1).
    /// </summary>
    public static bool BossClearedAtkGem(SwapExecutionResult result) =>
        BossBoardEvents(result).Any(
            e => e.Type == BattleEventType.GemMatched && e.Gem.GemType == GemType.Atk);

    /// <summary>
    /// The number of POWER match groups the Boss's board action produced — one per
    /// detected POWER Match. It is a diagnostic reading of the Boss's own cycle; the
    /// retired legacy charge counter it used to feed
    /// (<see cref="ResourceGenerator.CountPowerMatchGroups"/>, removed with ADR-029
    /// Slice 5 / TASK-313) no longer exists.
    /// </summary>
    public static int BossPowerMatchGroups(SwapExecutionResult result) =>
        BossBoardEvents(result).Count(
            e => e.Type == BattleEventType.MatchCreated && e.Match.Shape.GemType == GemType.Power);

    /// <summary>
    /// The Power the Boss's own board action generated — the
    /// <c>ResourceGeneration.Power</c> the Boss branch of
    /// <see cref="ResourceGenerator.ApplyActorMatchEffects"/> writes into
    /// <c>BossState.Power</c>: <see cref="ResourceGenerator.PowerGemBasePower"/> per
    /// unique cleared POWER cell of the Boss's own cycle (<c>COMBAT_RULES.md</c> §2,
    /// <c>ADR-029</c> §2.2).
    /// </summary>
    public static int BossPowerGeneration(SwapExecutionResult result) =>
        BossBoardEvents(result).Count(
            e => e.Type == BattleEventType.GemMatched && e.Gem.GemType == GemType.Power)
        * ResourceGenerator.PowerGemBasePower;

    /// <summary>
    /// Whether a Battle Event is part of a Match-3 board cycle
    /// (<c>GAME_EVENTS.md</c> §1.1).
    /// </summary>
    /// <remarks>
    /// <c>CascadeStepResolved</c> is a board-cycle event like the other four: it is
    /// emitted as its own pass's <b>last</b> item
    /// (<c>GAME_EVENTS.md</c> §1.1 item 8, <c>SIGNALR_PROTOCOL.md</c> §3.2.28), so a
    /// pass's run stays contiguous and the round keeps its two runs — the Player
    /// action's cycle and the Boss response phase's. Leaving it out would split every
    /// pass at its own closing report and make <see cref="BossBoardEvents"/> answer
    /// the Player cycle's tail.
    /// </remarks>
    private static bool IsBoardEvent(BattleEvent battleEvent) =>
        battleEvent.Type
            is BattleEventType.MatchCreated
            or BattleEventType.CascadeCreated
            or BattleEventType.GemMatched
            or BattleEventType.ComboChanged
            or BattleEventType.CascadeStepResolved;

    /// <summary>
    /// Pins <paramref name="board"/> as the battle's authoritative board and
    /// <paramref name="bossState"/>/<paramref name="petState"/> as its combat states.
    ///
    /// The write goes through the store's own <c>Sequence</c> compare-and-set, which
    /// is the idiom the application suites already use for arranging a scenario, so
    /// a test's write accounting is unchanged by the pinning: the pinned state
    /// carries the same <c>Sequence</c> the record already had.
    /// </summary>
    public static async Task PinAsync(
        InMemoryBattleStateRepository repository,
        BattleState state,
        BoardState board,
        BossState? bossState = null,
        PetState? petState = null,
        RngState? rngState = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(state);

        var pinned = state with
        {
            BoardState = board,
            BossState = bossState ?? state.BossState,
            PetState = petState ?? state.PetState,

            // GAME_STATE.md §2.6.2: the retained stream is fixture state. A
            // scenario that pins a crafted board also pins the stream its cascade
            // resolves against, exactly as the production resolution would resume
            // it, so the fixture's derived resolution is the one the round produces.
            RngState = rngState ?? state.RngState,
        };

        if (!await repository.TryUpdateAsync(pinned, state.Sequence).ConfigureAwait(false))
        {
            throw new InvalidOperationException(
                "The scenario's board could not be pinned: the stored Sequence moved on.");
        }
    }
}

/// <summary>
/// The provisioned <c>BossCard</c> <c>CardDefinition</c> content of TASK-309 §1 —
/// the five rows the migration chain leaves in PostgreSQL
/// (<c>DATABASE.md</c> §1/§3, <c>CARD_RULES.md</c> §1.1).
///
/// <code>
/// flame-burst     PowerCost 40   AtkBuff     +50%, duration 3   (Hỏa Long)
/// root            PowerCost 35   AtkDebuff   -30%, duration 3   (Mộc Yêu)
/// drain-power     PowerCost 25   PowerDrain  -20 flat          (Thủy Ma)
/// thunder-strike  PowerCost 40   NextAtkBuff +50%, NextAttack   (Kim Lôi Vương)
/// earthquake      PowerCost 30   Shield      150 flat          (Sơn Thạch Vệ, TASK-308)
/// </code>
///
/// <b>Why this exists, and what it is not.</b> The Application test project has no
/// database, so an end-to-end runtime test must supply the same content the
/// provisioned rows carry through the documented <see cref="ICardDefinitionLookup"/>
/// seam. Every value below is transcribed from TASK-309 §1 (and the migrations'
/// written contract), never computed here and never a second source of truth: the
/// owning values remain <c>BOSS_RULES.md</c> §6.3/§6.3.1, <c>DATABASE.md</c> §1, and
/// the TASK-309 Operator Decision Record. The infrastructure suite verifies the
/// database rows themselves.
/// </summary>
internal static class ProvisionedBossCardContent
{
    internal const string FlameBurstCardId = "flame-burst";
    internal const string RootCardId = "root";
    internal const string DrainPowerCardId = "drain-power";
    internal const string ThunderStrikeCardId = "thunder-strike";
    internal const string EarthquakeCardId = "earthquake";

    internal const int FlameBurstPowerCost = 40;
    internal const int RootPowerCost = 35;
    internal const int DrainPowerCost = 25;
    internal const int ThunderStrikePowerCost = 40;
    internal const int EarthquakePowerCost = 30;

    internal const int FlameBurstAtkPercent = 50;
    internal const int RootAtkPercent = 30;
    internal const int DrainPowerAmount = 20;
    internal const int ThunderStrikeAtkPercent = 50;
    internal const int TimedAtkModifierDuration = 3;

    internal static CardDefinition FlameBurst() => BossCard(
        FlameBurstCardId,
        "Flame Burst",
        FlameBurstPowerCost,
        CardEffectDefinitions.Create(
            CardEffectDefinition.AtkBuff(FlameBurstAtkPercent, TimedAtkModifierDuration)));

    internal static CardDefinition Root() => BossCard(
        RootCardId,
        "Root",
        RootPowerCost,
        CardEffectDefinitions.Create(
            CardEffectDefinition.AtkDebuff(RootAtkPercent, TimedAtkModifierDuration)));

    internal static CardDefinition DrainPower() => BossCard(
        DrainPowerCardId,
        "Drain Power",
        DrainPowerCost,
        CardEffectDefinitions.Create(
            CardEffectDefinition.PowerDrain(DrainPowerAmount)));

    internal static CardDefinition ThunderStrike() => BossCard(
        ThunderStrikeCardId,
        "Thunder Strike",
        ThunderStrikePowerCost,
        CardEffectDefinitions.Create(
            CardEffectDefinition.NextAtkBuff(ThunderStrikeAtkPercent)));

    internal static CardDefinition Earthquake() => BossCard(
        EarthquakeCardId,
        "Earthquake",
        EarthquakePowerCost,
        CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Shield, CardEffectValueType.Flat, 150)));

    /// <summary>The five provisioned Boss Cards, one per MVP Boss.</summary>
    internal static IReadOnlyList<CardDefinition> All { get; } =
    [
        FlameBurst(),
        Root(),
        DrainPower(),
        ThunderStrike(),
        Earthquake(),
    ];

    private static CardDefinition BossCard(
        string cardDefinitionId,
        string name,
        int powerCost,
        CardEffectDefinitions effectDefinition) =>
        new()
        {
            CardDefinitionId = cardDefinitionId,
            Name = name,
            Category = CardCategory.BossCard,
            PowerCost = powerCost,
            EffectDefinition = effectDefinition,

            // Persisted because the column is required and non-nullable and is
            // never read for a BossCard row: a Boss Card has no Player loadout
            // (CARD_RULES.md §1.1 items 4–5; DATABASE.md §3).
            LoadoutCopyLimit = 1,
        };
}

/// <summary>
/// An in-memory <see cref="ICardDefinitionLookup"/> over the provisioned Boss Card
/// content, recording what was requested so a test can assert the creation-time
/// form resolution ran exactly once and was keyed by the ability
/// <c>SkillId</c>.
/// </summary>
internal sealed class TestCardDefinitionLookup : ICardDefinitionLookup
{
    private readonly Dictionary<string, CardDefinition> _definitions = new(StringComparer.Ordinal);

    /// <summary>The ids requested, in order — the creation-time content read's trace.</summary>
    internal List<string> Reads { get; } = [];

    internal TestCardDefinitionLookup With(CardDefinition definition)
    {
        _definitions[definition.CardDefinitionId] = definition;

        return this;
    }

    /// <summary>Every provisioned Boss Card (the post-migration database content).</summary>
    internal TestCardDefinitionLookup WithProvisionedBossCards()
    {
        foreach (var definition in ProvisionedBossCardContent.All)
        {
            With(definition);
        }

        return this;
    }

    public Task<CardDefinition?> GetDefinitionAsync(
        string cardDefinitionId,
        CancellationToken cancellationToken = default)
    {
        Reads.Add(cardDefinitionId);

        _definitions.TryGetValue(cardDefinitionId, out var definition);

        return Task.FromResult(definition);
    }
}

/// <summary>
/// A <see cref="BattleStateService"/> over the in-memory active-state store, with the
/// provisioned Boss Card content behind its creation-time form resolution — the real
/// production pipeline over the suite's documented test doubles.
/// </summary>
/// <param name="Service">The service whose rounds a scenario resolves.</param>
/// <param name="Store">The store, for pinning a fixture and counting the write-backs.</param>
/// <param name="CardDefinitions">The content read, so a test can assert what was read.</param>
internal sealed record BossCardTestHarness(
    BattleStateService Service,
    InMemoryBattleStateRepository Store,
    TestCardDefinitionLookup CardDefinitions)
{
    /// <summary>
    /// The post-migration composition: all five MVP Bosses resolve the
    /// <c>BossCard</c> form when their card row exists.
    /// </summary>
    internal static BossCardTestHarness CreateProvisioned()
    {
        var store = new InMemoryBattleStateRepository();
        var cards = new TestCardDefinitionLookup().WithProvisionedBossCards();

        return new BossCardTestHarness(
            new BattleStateService(store, new FixedRngSeedSource(), cardDefinitions: cards),
            store,
            cards);
    }

    /// <summary>
    /// The transitional composition: no provisioned <c>BossCard</c> row is readable,
    /// so every Boss keeps the legacy <c>BossSkill</c> form
    /// (<c>BOSS_RULES.md</c> §4.4 item 6). It is the explicit "unmigrated" fixture
    /// the legacy coverage uses, not a weakened migrated-form scenario.
    /// </summary>
    internal static BossCardTestHarness CreateWithoutProvisionedCards()
    {
        var store = new InMemoryBattleStateRepository();
        var cards = new TestCardDefinitionLookup();

        return new BossCardTestHarness(
            new BattleStateService(store, new FixedRngSeedSource(), cardDefinitions: cards),
            store,
            cards);
    }

    /// <summary>
    /// Commits one round in which the Boss Card cast is selected. The Player's action
    /// is a non-ATK Match (<see cref="RoundTestSupport.PlayerMatch"/>), so the Player
    /// deals no damage and the round's combat modifiers are the cast's alone — the
    /// standard cast-phase fixture of this suite.
    /// </summary>
    /// <param name="battleId">The battle whose record is pinned and resolved.</param>
    /// <param name="from">The state the round starts from (its board/stream are replaced).</param>
    /// <param name="bossState">The Boss state the scenario pins (Power, cooldown, ...).</param>
    /// <param name="petState">The Pet state the scenario pins.</param>
    internal async Task<SwapExecutionResult> CastRoundAsync(
        string battleId,
        BattleState from,
        BossState bossState,
        PetState petState)
    {
        var pinned = RoundTestSupport.PlayerMatch(GemType.Def, from.RngState, from.LastCommittedSwapPair);

        return await ExecutePinnedRoundAsync(battleId, pinned, from.RngState, bossState, petState);
    }

    /// <summary>
    /// Pins a <see cref="PinnedPlayerRound"/> fixture into the store and commits the one
    /// round that acts on it.
    /// </summary>
    internal async Task<SwapExecutionResult> ExecutePinnedRoundAsync(
        string battleId,
        PinnedPlayerRound pinned,
        RngState rngState,
        BossState bossState,
        PetState petState)
    {
        var state = (await Service.GetBattleAsync(battleId))!;

        await RoundTestSupport.PinAsync(Store, state, pinned.Board, bossState, petState, rngState);

        return await ExecutePinnedSwapAsync(battleId, pinned.Swap);
    }

    /// <summary>
    /// Pins a <see cref="BossRoundFixture"/> (a generated board whose Boss action is
    /// known) and commits the one round that acts on it.
    /// </summary>
    internal async Task<SwapExecutionResult> ExecuteFixtureRoundAsync(
        string battleId,
        BossRoundFixture fixture,
        BossState bossState,
        PetState petState)
    {
        var state = (await Service.GetBattleAsync(battleId))!;

        await RoundTestSupport.PinAsync(Store, state, fixture.Board, bossState, petState, fixture.RngState);

        return await ExecutePinnedSwapAsync(battleId, fixture.Swap);
    }

    /// <summary>
    /// Commits the pinned Swap against the stored record, asserting the action was
    /// accepted so a scenario's assertions are about gameplay rather than about a
    /// rejected request.
    /// </summary>
    private async Task<SwapExecutionResult> ExecutePinnedSwapAsync(string battleId, SwapRequest swap)
    {
        var pinnedState = (await Service.GetBattleAsync(battleId))!;

        var round = await Service.ExecuteSwapAsync(
            battleId,
            swap with { ExpectedSequence = pinnedState.Sequence });

        Assert.NotNull(round);
        Assert.True(round.Value.IsAccepted, $"reason={round.Value.Reason}");

        return round.Value;
    }
}

/// <summary>
/// A deterministic, generated round fixture whose Boss response phase is known before
/// the round runs: the board the Player acts on, the one committed Player Swap, the
/// cascade it produces, and the Boss's own deterministic post-player selection with
/// the cascade that selection resolves (<c>MATCH3_RULES.md</c> §1.2.1/§2.1.6,
/// <c>ADR-026</c> §8, <c>BOSS_RULES.md</c> §4.3).
///
/// <b>Every value is production output.</b> The board is
/// <see cref="BoardGenerator.Generate"/>'s, the Player cascade is
/// <see cref="CascadeResolver.Resolve"/>'s, the Boss selection is
/// <see cref="BossAiEngine.SelectMove"/>'s, and the resources are
/// <see cref="ResourceGenerator.Generate"/>'s. The fixture chooses <i>which</i>
/// deterministic stream to play (it is fixture state, <c>GAME_STATE.md</c> §2.6.2)
/// and then reports what the rules produced — it decides no gameplay value and
/// reimplements no step.
/// </summary>
internal sealed record BossRoundFixture(
    RngState RngState,
    BoardState Board,
    SwapRequest Swap,
    CascadeResolver.CascadeResult PlayerResolution,
    ResourceGeneration PlayerResources,
    BossMoveSelectionResult Selection,
    CascadeResolver.CascadeResult BossResolution,
    ResourceGeneration BossResources)
{
    /// <summary>Whether the Player's committed Swap clears at least one ATK Gem.</summary>
    internal bool PlayerClearsAtkGem => PlayerResources.ClearedGemCounts.Atk > 0;

    /// <summary>
    /// Whether the Boss's own deterministic board action is a qualifying attack —
    /// a confirmed ATK-Gem match in its resolution (<c>ADR-026</c> D1).
    /// </summary>
    internal bool BossAttackQualifies =>
        AttackQualification.FromResolution(BossResolution).HasConfirmedAtkMatch;
}

/// <summary>
/// The deterministic search for a <see cref="BossRoundFixture"/> that satisfies a
/// scenario's predicate.
///
/// <b>It searches fixture state, not gameplay.</b> Each candidate is one generated
/// board and one adjacent swap that produces a Match; the search calls the production
/// stages in the production order and accepts the first candidate whose <i>derived</i>
/// values satisfy the predicate. A scenario therefore starts from a board the game
/// genuinely produces, on which the documented rules genuinely make the Boss act as
/// the scenario needs — exactly the discipline
/// <see cref="RoundTestSupport.PlayerMatch"/> applies to Gem-type fixtures.
///
/// The search is bounded and deterministic: the same predicate always yields the same
/// fixture, so a test that asserts on it is reproducible. A predicate with no fixture
/// inside the budget is a fixture defect, never a production failure.
/// </summary>
internal static class BossRoundFixtures
{
    /// <summary>
    /// Finds the first deterministic fixture satisfying <paramref name="accept"/>.
    /// </summary>
    /// <param name="accept">
    /// The scenario's predicate over the derived fixture. It may inspect the Boss
    /// selection (its score, whether its action is a qualifying ATK match) and the
    /// Player cascade's cleared types, and must not derive any new gameplay value.
    /// </param>
    /// <param name="boardBudget">
    /// How many generated boards to try before reporting a fixture defect.
    /// </param>
    /// <exception cref="InvalidOperationException">No fixture satisfied the predicate.</exception>
    internal static BossRoundFixture Find(Func<BossRoundFixture, bool> accept, int boardBudget = 6_000)
    {
        ArgumentNullException.ThrowIfNull(accept);

        // The stream is fixture state (GAME_STATE.md §2.6.2): each candidate board is
        // generated from its own deterministic pair, in a fixed order.
        for (var boardIndex = 1; boardIndex <= boardBudget; boardIndex++)
        {
            var rngState = new RngState(
                unchecked(((ulong)boardIndex * 0x9E3779B97F4A7C15UL) | 1UL),
                0x0309UL);

            BoardState board;

            try
            {
                // MATCH3_RULES.md §1.5: a successful generation carries the accepted
                // board; a failed one throws instead (never a null board), so the
                // value is read through the documented postcondition.
                board = BoardGenerator.Generate(new Pcg32(rngState.State, rngState.Increment)).Board!;
            }
            catch (BoardGenerationFailedException)
            {
                // MATCH3_RULES.md §1.5 item 3: the documented 64-attempt bound was
                // exhausted for this stream. It is a rejected candidate, not a
                // scenario failure; the search continues on the next stream.
                continue;
            }

            foreach (var (from, to) in AdjacentPairs())
            {
                if (MatchDetector.Detect(board.WithSwapped(from, to)).Count == 0)
                {
                    continue;
                }

                // The Player action's own cascade, resolved exactly as the production
                // Swap path resolves it (MATCH3_RULES.md §2.1.6, §4).
                var playerResolution = CascadeResolver.Resolve(
                    board.WithSwapped(from, to),
                    new Pcg32(rngState.State, rngState.Increment));
                var playerResources = ResourceGenerator.Generate(playerResolution);

                // ADR-026 §8 / BOSS_RULES.md §4.3: the Boss's one selected action on
                // the board the Player's action left.
                var selection = BossAiEngine.SelectMove(
                    playerResolution.Board,
                    new Pcg32(playerResolution.RngState.State, playerResolution.RngState.Increment));

                if (selection.LegalCandidatesCount == 0)
                {
                    continue;
                }

                // The cascade that selected board action resolves — the production
                // order's own RNG choice: a pre-selection recovery reshuffle carries
                // its advanced stream into the cascade, otherwise the retained one is
                // used unchanged (BattleStateService step 9).
                var bossCascadeRng = selection.Reshuffled
                    ? selection.RngState
                    : playerResolution.RngState;

                var bossResolution = CascadeResolver.Resolve(
                    selection.Board.WithSwapped(
                        selection.SelectedMove.From,
                        selection.SelectedMove.To),
                    new Pcg32(bossCascadeRng.State, bossCascadeRng.Increment));

                var fixture = new BossRoundFixture(
                    rngState,
                    board,
                    new SwapRequest(from, to),
                    playerResolution,
                    playerResources,
                    selection,
                    bossResolution,
                    ResourceGenerator.Generate(bossResolution));

                if (accept(fixture))
                {
                    return fixture;
                }
            }
        }

        throw new InvalidOperationException(
            "No deterministic Boss-round fixture satisfied the scenario's predicate within "
            + $"{boardBudget} generated boards. This is a fixture defect, never a production "
            + "failure: extend the budget or relax the predicate, but do not weaken the rule "
            + "the scenario asserts.");
    }

    /// <summary>The 112 orthogonal adjacent pairs, in ascending cell order.</summary>
    private static IEnumerable<(int From, int To)> AdjacentPairs()
    {
        for (var index = 0; index < BoardState.CellCount; index++)
        {
            var right = BoardState.ToColumn(index) + 1 < BoardState.Columns ? index + 1 : -1;
            var down = index + BoardState.Width < BoardState.CellCount ? index + BoardState.Width : -1;

            if (right >= 0)
            {
                yield return (index, right);
            }

            if (down >= 0)
            {
                yield return (index, down);
            }
        }
    }
}
