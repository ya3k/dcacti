using GameServer.Domain.Battle;
using GameServer.Domain.Cards;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;

namespace GameServer.Application.Tests.Balance;

/// <summary>
/// Accumulates the <c>TASK-193</c> §3.1 metric set from the production
/// pipeline's own outputs.
///
/// <b>It sums and counts; it computes nothing.</b> Damage totals are summed from
/// the resolutions' <c>DamageDealt</c> reports, Power from their
/// <c>PowerChanged</c> deltas, casts from their <c>CardCast</c> reports, Relic
/// triggers from their <c>RelicTriggered</c> reports. HP and Combo are copied
/// from the committed <c>BattleState</c>. No value is re-derived from another, so
/// the metrics cannot disagree with the rules they describe.
/// </summary>
internal sealed class MetricAccumulator
{
    private readonly BalanceSimulationConfiguration _configuration;

    private readonly List<int> _playerDamagePerTurn = [];
    private readonly List<int> _bossDamagePerTurn = [];
    private readonly List<int> _bossHpPerTurn = [];
    private readonly List<int> _playerHpPerTurn = [];
    private readonly List<int> _comboPerTurn = [];
    private readonly List<int> _castTurns = [];
    private readonly List<BalanceResourceCheckpoint> _trace = [];
    private readonly Dictionary<string, int> _castsByCard = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _relicTriggersByRelic = new(StringComparer.Ordinal);
    private readonly Dictionary<int, int> _comboDistribution = [];

    private int _playerDamageTotal;
    private int _bossDamageTotal;
    private int _powerGenerated;
    private int _powerSpent;
    private int _castsTotal;
    private int _relicTriggersTotal;
    private int _elementAdvantage;
    private int _elementNeutral;
    private int _elementDisadvantage;
    private int _bossSkillCasts;
    private int _bossPassiveTriggers;
    private int _bossRegenerationTotal;
    private int _bossRegenerationTurns;
    private int? _previousBossHp;
    private int? _bossEnrageTurn;

    /// <summary>The Turn currently being accumulated (1-based).</summary>
    private int _currentTurn;

    /// <summary>
    /// Damage dealt during the current Turn, across every resolution in it. It is
    /// flushed by <see cref="EndTurn"/> so a Turn with both a Card cast and a
    /// committed Swap reports each Turn once.
    /// </summary>
    private int _pendingPlayerDamage;
    private int _pendingBossDamage;

    public MetricAccumulator(BalanceSimulationConfiguration configuration) =>
        _configuration = configuration;

    /// <summary>Swaps the production validator rejected (<c>MATCH3_RULES.md</c> §2.1.5).</summary>
    public int RejectedSwaps { get; private set; }

    /// <summary>Card casts the production cast path rejected (<c>CARD_RULES.md</c> §3).</summary>
    public int RejectedCasts { get; private set; }

    /// <summary>The cast rejection reasons observed, for reproducibility of a run's record.</summary>
    public List<CardCastRejectionReason> CastRejectionReasons { get; } = [];

    private int _consecutiveRejectedSwaps;

    /// <summary>Reads the consecutive-rejection streak the driver bounds.</summary>
    public int ConsecutiveRejectedSwaps => _consecutiveRejectedSwaps;

    /// <summary>Records one rejected Swap proposal and extends the streak.</summary>
    public void RecordRejectedSwap(SwapRejectionReason reason)
    {
        _ = reason;
        RejectedSwaps++;
        _consecutiveRejectedSwaps++;
    }

    /// <summary>Clears the consecutive-rejection streak after a committed Swap.</summary>
    public void ResetRejectedSwapStreak() => _consecutiveRejectedSwaps = 0;

    /// <summary>Records one rejected Card cast proposal.</summary>
    public void RecordRejectedCast(CardCastRejectionReason reason)
    {
        RejectedCasts++;
        CastRejectionReasons.Add(reason);
    }

    /// <summary>
    /// Collects the events of <b>one resolution</b> within the current Turn.
    ///
    /// A committed Match-3 Turn can contain more than one resolution: an
    /// auxiliary Card cast (<c>CARD_RULES.md</c> §3 item 6 permits one), and the
    /// committed Swap that advances the Turn. Both belong to the <i>same</i> Turn,
    /// so per-Turn series must receive exactly one entry for the Turn and the
    /// damage totals must count each instance once. This method accumulates;
    /// <see cref="EndTurn"/> flushes.
    /// </summary>
    /// <param name="events">The resolution's own ordered event list.</param>
    public void RecordResolution(IReadOnlyList<BattleEvent> events)
    {
        foreach (var battleEvent in events)
        {
            switch (battleEvent.Type)
            {
                case BattleEventType.DamageDealt:
                {
                    // GAME_EVENTS.md §2: DamageDealt and DamageTaken are two
                    // reports of ONE damage instance — "the two are two statements
                    // about one instance, not two applications of damage"
                    // (DamageEvents.cs). Only DamageDealt is summed, so each
                    // instance contributes its Final Damage exactly once.
                    var dealt = battleEvent.DamageDealt;

                    if (dealt.Source == DamageParty.Player)
                    {
                        _pendingPlayerDamage += dealt.Amount;
                    }
                    else if (dealt.Source == DamageParty.Boss)
                    {
                        _pendingBossDamage += dealt.Amount;
                    }

                    break;
                }

                case BattleEventType.DamageCalculated:
                {
                    // M-11: the Element Modifier the pipeline itself composed
                    // (COMBAT_RULES.md §3 step 3). Classification compares it with
                    // the documented Default table — the harness chooses no
                    // threshold of its own.
                    ClassifyElement(battleEvent.DamageCalculated.ElementModifier);
                    break;
                }

                case BattleEventType.PowerChanged:
                {
                    // M-07: the signed delta and resulting value the authoritative
                    // mutation reported (SIGNALR_PROTOCOL.md §3.2.24). Positive joins
                    // generation, negative joins spend/drain (TASK-195 §4.6).
                    var changed = battleEvent.PowerChanged;

                    if (changed.Delta > 0)
                    {
                        _powerGenerated += changed.Delta;
                    }
                    else if (changed.Delta < 0)
                    {
                        _powerSpent += -changed.Delta;
                    }

                    break;
                }

                case BattleEventType.CardCast:
                {
                    // M-08: one report per successful cast (CARD_RULES.md §6).
                    var cast = battleEvent.CardCast;
                    _castsTotal++;
                    _castTurns.Add(_currentTurn);
                    _castsByCard[cast.CardId] = _castsByCard.GetValueOrDefault(cast.CardId) + 1;
                    break;
                }

                case BattleEventType.RelicTriggered:
                {
                    // M-09: one report per Relic whose effect actually applied
                    // (RELIC_RULES.md §7).
                    var relicId = battleEvent.RelicTriggered.RelicId;
                    _relicTriggersTotal++;
                    _relicTriggersByRelic[relicId] = _relicTriggersByRelic.GetValueOrDefault(relicId) + 1;
                    break;
                }

                case BattleEventType.BossSkillCast:
                    _bossSkillCasts++;
                    break;

                case BattleEventType.PassiveTriggered:
                {
                    // M-13: the Boss's own Passive firings. The tracker reports
                    // source="boss" for these (PASSIVE_RULES.md §7). Pet passive
                    // firings are excluded (finding D-194-6, TASK-195 §4.8).
                    if (battleEvent.PassiveTriggered.Source == PassiveEventSource.Boss)
                    {
                        _bossPassiveTriggers++;
                    }

                    break;
                }
            }
        }
    }

    /// <summary>
    /// Opens the current Turn for accumulation. Called once per committed Turn,
    /// before any of that Turn's resolutions. Preserves pending damage if called
    /// for the already-open Turn (<c>TASK-195</c> §5.1.3).
    /// </summary>
    /// <param name="turnNumber">The 1-based Turn number.</param>
    public void BeginTurn(int turnNumber)
    {
        if (_currentTurn == turnNumber)
        {
            return;
        }

        _currentTurn = turnNumber;
        _pendingPlayerDamage = 0;
        _pendingBossDamage = 0;
    }

    /// <summary>
    /// Flushes the current Turn into the per-Turn series and the totals,
    /// reading the committed state for the values the state itself owns.
    /// Called exactly once per committed Turn, after its last resolution.
    /// </summary>
    /// <param name="state">The committed authoritative state at the Turn's end.</param>
    public void EndTurn(BattleState state)
    {
        _playerDamagePerTurn.Add(_pendingPlayerDamage);
        _bossDamagePerTurn.Add(_pendingBossDamage);
        _playerDamageTotal += _pendingPlayerDamage;
        _bossDamageTotal += _pendingBossDamage;
        _pendingPlayerDamage = 0;
        _pendingBossDamage = 0;

        _bossHpPerTurn.Add(state.BossState.HP);
        _playerHpPerTurn.Add(state.PetState.HP);
        _comboPerTurn.Add(state.Combo);
        _comboDistribution[state.Combo] = _comboDistribution.GetValueOrDefault(state.Combo) + 1;

        // M-12: Boss regeneration. BOSS_RULES.md §6.2.3 applies it at step 18a
        // and reports no dedicated event, so it is measured as a positive Boss-HP
        // movement between consecutive Turns — the same progression M-05 records.
        // This is an empirical lower bound (TASK-195 §4.3).
        if (_previousBossHp is { } previous && state.BossState.HP > previous)
        {
            _bossRegenerationTotal += state.BossState.HP - previous;
            _bossRegenerationTurns++;
        }

        _previousBossHp = state.BossState.HP;

        // M-13: Enrage. BOSS_RULES.md §5 item 4 emits no event for it, so the
        // first Turn whose Boss HP is below the definition's own declared
        // threshold is the enrage Turn. The threshold is the Boss's value, not
        // one the harness invents.
        _bossEnrageTurn ??= state.BossState.HP < _configuration.Boss.MaxHP * _configuration.Boss.EnrageThreshold
            ? _currentTurn
            : null;

        // M-15: the checkpoint trace — one entry per committed Turn.
        _trace.Add(new BalanceResourceCheckpoint(
            Turn: _currentTurn,
            PlayerHp: state.PetState.HP,
            PlayerMaxHp: state.PetState.MaxHP,
            PlayerPower: state.PetState.Power,
            BossHp: state.BossState.HP,
            PlayerStatusCount: state.PetState.ActiveStatusEffects.Length,
            BossStatusCount: state.BossState.ActiveStatusEffects.Length));
    }

    /// <summary>
    /// Reconciles terminal battle resolution from a Card cast (<c>TASK-195</c> §5.1.3, §5.2).
    ///
    /// Under authoritative rules (<c>CARD_RULES.md</c> §3 item 5, <c>DATABASE.md</c> §1 item 1),
    /// a Card cast does not begin or advance a Turn; <c>BattleState.Turn</c> remains equal to
    /// the count of committed Swaps (T). This method attributes the terminal card cast damage and
    /// terminal combat state to the final resolving combat state (the T-th turn record) without
    /// emitting an uncommitted phantom Turn T+1 record into the per-Turn series.
    /// </summary>
    /// <param name="state">The terminal authoritative state.</param>
    public void ReconcileTerminalCardCast(BattleState state)
    {
        _playerDamageTotal += _pendingPlayerDamage;
        _bossDamageTotal += _pendingBossDamage;

        if (_playerDamagePerTurn.Count > 0)
        {
            _playerDamagePerTurn[^1] += _pendingPlayerDamage;
            _bossDamagePerTurn[^1] += _pendingBossDamage;
            _bossHpPerTurn[^1] = state.BossState.HP;
            _playerHpPerTurn[^1] = state.PetState.HP;

            var lastTrace = _trace[^1];
            _trace[^1] = new BalanceResourceCheckpoint(
                Turn: lastTrace.Turn,
                PlayerHp: state.PetState.HP,
                PlayerMaxHp: state.PetState.MaxHP,
                PlayerPower: state.PetState.Power,
                BossHp: state.BossState.HP,
                PlayerStatusCount: state.PetState.ActiveStatusEffects.Length,
                BossStatusCount: state.BossState.ActiveStatusEffects.Length);
        }

        if (_castTurns.Count > 0 && _castTurns[^1] > state.Turn)
        {
            _castTurns[^1] = state.Turn;
        }

        _pendingPlayerDamage = 0;
        _pendingBossDamage = 0;
    }

    /// <summary>
    /// Classifies one damage instance's Element Modifier against the documented
    /// <c>ElementModifiers.Default</c> table (<c>ELEMENT_RULES.md</c> §2.2). A
    /// factor matching none of the three documented values is counted as neutral,
    /// because the table is closed and an unmatched factor is not a documented
    /// disadvantage.
    /// </summary>
    private void ClassifyElement(double elementModifier)
    {
        if (elementModifier == ElementModifiers.Default.Advantage)
        {
            _elementAdvantage++;
        }
        else if (elementModifier == ElementModifiers.Default.Disadvantage)
        {
            _elementDisadvantage++;
        }
        else
        {
            _elementNeutral++;
        }
    }

    /// <summary>
    /// Produces the immutable metric record for the run (<c>H-06</c>).
    /// </summary>
    public BalanceSimulationMetrics Build(BattleState state, BalanceSimulationOutcome outcome)
    {
        _ = outcome;

        return new BalanceSimulationMetrics
        {
            Turns = state.Turn,
            DurationTurns = state.Turn,
            RejectedSwaps = RejectedSwaps,
            RejectedCasts = RejectedCasts,

            PlayerDamageTotal = _playerDamageTotal,
            PlayerDamagePerTurn = _playerDamagePerTurn.ToArray(),

            BossDamageTotal = _bossDamageTotal,
            BossDamagePerTurn = _bossDamagePerTurn.ToArray(),

            BossHpPerTurn = _bossHpPerTurn.ToArray(),
            BossHpMin = _bossHpPerTurn.Count == 0 ? state.BossState.HP : _bossHpPerTurn.Min(),
            PlayerHpPerTurn = _playerHpPerTurn.ToArray(),
            PlayerHpMin = _playerHpPerTurn.Count == 0 ? state.PetState.HP : _playerHpPerTurn.Min(),

            PowerGeneratedTotal = _powerGenerated,
            PowerSpentTotal = _powerSpent,
            PowerFinal = state.PetState.Power,

            CastsByCard = new Dictionary<string, int>(_castsByCard, StringComparer.Ordinal),
            CastsTotal = _castsTotal,
            CastTurns = _castTurns.ToArray(),

            RelicTriggersByRelic = new Dictionary<string, int>(_relicTriggersByRelic, StringComparer.Ordinal),
            RelicTriggersTotal = _relicTriggersTotal,

            ComboPerTurn = _comboPerTurn.ToArray(),
            ComboMax = _comboPerTurn.Count == 0 ? BattleState.InitialCombo : _comboPerTurn.Max(),
            ComboDistribution = new Dictionary<int, int>(_comboDistribution),

            ElementAdvantageCount = _elementAdvantage,
            ElementNeutralCount = _elementNeutral,
            ElementDisadvantageCount = _elementDisadvantage,

            BossRegenerationTotal = _bossRegenerationTotal,
            BossRegenerationTurns = _bossRegenerationTurns,
            BossSkillCastCount = _bossSkillCasts,
            BossPassiveTriggerCount = _bossPassiveTriggers,
            BossEnrageTurn = _bossEnrageTurn,

            ResourceTrace = _trace.ToArray(),
        };
    }

    /// <summary>
    /// The empty metric set an invalid configuration reports — every value zero,
    /// with no invented estimate standing in for data that does not exist.
    /// </summary>
    public static BalanceSimulationMetrics Empty() => new()
    {
        Turns = 0,
        DurationTurns = 0,
        RejectedSwaps = 0,
        RejectedCasts = 0,
        PlayerDamageTotal = 0,
        PlayerDamagePerTurn = [],
        BossDamageTotal = 0,
        BossDamagePerTurn = [],
        BossHpPerTurn = [],
        BossHpMin = 0,
        PlayerHpPerTurn = [],
        PlayerHpMin = 0,
        PowerGeneratedTotal = 0,
        PowerSpentTotal = 0,
        PowerFinal = 0,
        CastsByCard = new Dictionary<string, int>(StringComparer.Ordinal),
        CastsTotal = 0,
        CastTurns = [],
        RelicTriggersByRelic = new Dictionary<string, int>(StringComparer.Ordinal),
        RelicTriggersTotal = 0,
        ComboPerTurn = [],
        ComboMax = 0,
        ComboDistribution = new Dictionary<int, int>(),
        ElementAdvantageCount = 0,
        ElementNeutralCount = 0,
        ElementDisadvantageCount = 0,
        BossRegenerationTotal = 0,
        BossRegenerationTurns = 0,
        BossSkillCastCount = 0,
        BossPassiveTriggerCount = 0,
        BossEnrageTurn = null,
        ResourceTrace = [],
    };
}
