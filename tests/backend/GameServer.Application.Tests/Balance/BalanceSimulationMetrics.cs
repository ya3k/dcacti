namespace GameServer.Application.Tests.Balance;

/// <summary>
/// The metrics one balance simulation run reports (<c>TASK-193</c> §3.1,
/// M-01…M-15).
///
/// <b>Every value here is read from the production pipeline's own outputs</b> —
/// the committed <c>BattleState</c> and the ordered <c>BattleEvent</c> list the
/// resolution emitted. The harness computes no combat value of its own: it sums
/// and counts what the authoritative stages already produced, so a metric can
/// only ever restate a rule, never replace one.
///
/// <b>Zero is a valid value.</b> A metric that legitimately did not occur (no
/// Boss regeneration against a Boss without that Passive, no Relic triggers with
/// no Relics equipped) reports <c>0</c>. Missing data is never substituted with
/// an estimate.
/// </summary>
internal sealed record BalanceSimulationMetrics
{
    // ------------------------------------------------------------------
    // M-01 / M-02 — Turns and duration
    // ------------------------------------------------------------------

    /// <summary>
    /// M-01 — the number of Turns (committed Swaps) this run resolved. It is the
    /// terminal state's own <c>Turn</c> counter (<c>GAME_STATE.md</c> §2.0.2,
    /// <c>MATCH3_RULES.md</c> §8.1), read rather than counted by the harness.
    /// </summary>
    public required int Turns { get; init; }

    /// <summary>
    /// M-02 — duration in Turns. It is the same quantity as
    /// <see cref="Turns"/> and is reported separately because
    /// <c>TASK-193</c> §4.2 <b>excludes wall-clock duration</b>: a
    /// millisecond measurement would depend on machine speed and destroy
    /// determinism, so the documented duration metric is Turns.
    ///
    /// Per Q-1 = QUALITATIVE there is <b>no approved target</b> for this value,
    /// and the harness applies no pass/fail threshold to it.
    /// </summary>
    public required int DurationTurns { get; init; }

    /// <summary>
    /// The Swaps the player policy proposed that the production validator
    /// <b>rejected</b> (<c>MATCH3_RULES.md</c> §2.1.5). Recorded for
    /// reproducibility: a run whose policy wasted Turns on illegal swaps is a
    /// different measurement from one that did not.
    /// </summary>
    public required int RejectedSwaps { get; init; }

    /// <summary>
    /// The Card casts the player policy proposed that the production cast path
    /// rejected (<c>CARD_RULES.md</c> §3). Includes the B-02 per-Turn allowance
    /// rejection, which is one of the reasons a policy's request can be refused.
    /// </summary>
    public required int RejectedCasts { get; init; }

    // ------------------------------------------------------------------
    // M-03 — Player damage
    // ------------------------------------------------------------------

    /// <summary>
    /// M-03 — cumulative damage the player's side dealt to the Boss, summed from
    /// the resolution's own <c>DamageDealt</c> reports where
    /// <c>Source = Player</c> (<c>GAME_EVENTS.md</c> §2, <c>COMBAT_RULES.md</c>
    /// §3 step 6's Final Damage). It is not recomputed from HP deltas.
    /// </summary>
    public required int PlayerDamageTotal { get; init; }

    /// <summary>
    /// M-03 — player damage per Turn, in Turn order. Index <c>i</c> is the damage
    /// dealt during the <c>(i + 1)</c>-th resolved Turn.
    /// </summary>
    public required IReadOnlyList<int> PlayerDamagePerTurn { get; init; }

    // ------------------------------------------------------------------
    // M-04 — Boss damage
    // ------------------------------------------------------------------

    /// <summary>
    /// M-04 — cumulative damage the Boss dealt to the active Pet, summed from the
    /// same reports where <c>Source = Boss</c>.
    /// </summary>
    public required int BossDamageTotal { get; init; }

    /// <summary>
    /// M-04 — Boss damage per Turn, in Turn order.
    /// </summary>
    public required IReadOnlyList<int> BossDamagePerTurn { get; init; }

    // ------------------------------------------------------------------
    // M-05 / M-06 — HP progression
    // ------------------------------------------------------------------

    /// <summary>
    /// M-05 — the Boss's HP after each resolved Turn, in Turn order.
    /// </summary>
    public required IReadOnlyList<int> BossHpPerTurn { get; init; }

    /// <summary>
    /// M-05 — the lowest Boss HP observed across the run. This is the value that
    /// shows how close a non-winning run came, which B-03/B-07 need.
    /// </summary>
    public required int BossHpMin { get; init; }

    /// <summary>
    /// M-06 — the active Pet's HP after each resolved Turn, in Turn order.
    /// </summary>
    public required IReadOnlyList<int> PlayerHpPerTurn { get; init; }

    /// <summary>
    /// M-06 — the lowest active-Pet HP observed across the run.
    /// </summary>
    public required int PlayerHpMin { get; init; }

    // ------------------------------------------------------------------
    // M-07 — Power
    // ------------------------------------------------------------------

    /// <summary>
    /// M-07 — cumulative Power the active Pet gained, summed from the positive
    /// deltas of the resolution's own <c>PowerChanged</c> reports
    /// (<c>SIGNALR_PROTOCOL.md</c> §3.2.24).
    /// </summary>
    public required int PowerGeneratedTotal { get; init; }

    /// <summary>
    /// M-07 — cumulative Power the active Pet spent/lost across all sinks, as the
    /// absolute value of the negative deltas of <c>PowerChanged</c> reports.
    /// This includes both Card cast expenditures and external resource drains
    /// (such as Thủy Ma's Drain Power skill effect; <c>TASK-195</c> §4.6).
    /// </summary>
    public required int PowerSpentTotal { get; init; }

    /// <summary>
    /// M-07 — the active Pet's Power at the terminal state.
    /// </summary>
    public required int PowerFinal { get; init; }

    // ------------------------------------------------------------------
    // M-08 — Cards cast
    // ------------------------------------------------------------------

    /// <summary>
    /// M-08 — successful casts per Card definition identity, counted from the
    /// resolution's own <c>CardCast</c> reports (<c>CARD_RULES.md</c> §6).
    /// </summary>
    public required IReadOnlyDictionary<string, int> CastsByCard { get; init; }

    /// <summary>
    /// M-08 — total successful casts. It is the sum of
    /// <see cref="CastsByCard"/> and is stated separately because the per-Card
    /// breakdown is what B-08/B-09 read.
    /// </summary>
    public required int CastsTotal { get; init; }

    /// <summary>
    /// M-08 — the Turn number during which each cast resolved, in cast order
    /// (1-based: a cast during the first resolved Turn records <c>1</c>).
    /// </summary>
    public required IReadOnlyList<int> CastTurns { get; init; }

    // ------------------------------------------------------------------
    // M-09 — Relic triggers
    // ------------------------------------------------------------------

    /// <summary>
    /// M-09 — <c>RelicTriggered</c> counts per Relic instance identity
    /// (<c>RELIC_RULES.md</c> §7, <c>SIGNALR_PROTOCOL.md</c> §3.2.23). Empty when
    /// the battle carries no Relic content.
    /// </summary>
    public required IReadOnlyDictionary<string, int> RelicTriggersByRelic { get; init; }

    /// <summary>
    /// M-09 — total Relic triggers.
    /// </summary>
    public required int RelicTriggersTotal { get; init; }

    // ------------------------------------------------------------------
    // M-10 — Combo
    // ------------------------------------------------------------------

    /// <summary>
    /// M-10 — the Combo value each resolved Turn ended at, in Turn order
    /// (<c>MATCH3_RULES.md</c> §6). One entry per committed Swap, because a
    /// committed Swap always produces at least one Match and therefore a Combo of
    /// at least 1 (§6.5 item 3).
    /// </summary>
    public required IReadOnlyList<int> ComboPerTurn { get; init; }

    /// <summary>
    /// M-10 — the highest Combo observed. B-11 reads this against the documented
    /// Combo table's thresholds (<c>GAME_RULES.md</c> §5).
    /// </summary>
    public required int ComboMax { get; init; }

    /// <summary>
    /// M-10 — how many times each Combo value occurred, so the distribution's
    /// shape is reportable without re-walking the per-Turn list.
    /// </summary>
    public required IReadOnlyDictionary<int, int> ComboDistribution { get; init; }

    // ------------------------------------------------------------------
    // M-11 — Element modifiers
    // ------------------------------------------------------------------

    /// <summary>
    /// M-11 — damage instances that resolved at the advantage modifier, read from
    /// the pipeline's own <c>DamageCalculation.ElementModifier</c>
    /// (<c>COMBAT_RULES.md</c> §3 step 3, <c>ELEMENT_RULES.md</c> §2.2). The
    /// classification compares the observed factor against the documented
    /// <c>ElementModifiers.Default</c> table — the harness invents no threshold of
    /// its own.
    /// </summary>
    public required int ElementAdvantageCount { get; init; }

    /// <summary>M-11 — damage instances that resolved at the neutral modifier.</summary>
    public required int ElementNeutralCount { get; init; }

    /// <summary>M-11 — damage instances that resolved at the disadvantage modifier.</summary>
    public required int ElementDisadvantageCount { get; init; }

    // ------------------------------------------------------------------
    // M-12 — Boss regeneration
    // ------------------------------------------------------------------

    /// <summary>
    /// M-12 — total HP the Boss restored through regeneration across the run. It
    /// is measured as the sum of positive Boss-HP movements between consecutive
    /// Turns, which is how <c>BOSS_RULES.md</c> §6.2.3's regeneration manifests
    /// (the production stage reports no dedicated event). Zero for a Boss without a
    /// regeneration Passive.
    ///
    /// <b>Authoritative lower bound:</b> Because regeneration resolves inside the same
    /// Turn as player damage and no <c>BossHealed</c> event exists in the production
    /// pipeline, any regeneration smaller than that Turn's damage is masked. The
    /// harness preserves authoritative lower-bound semantics and does not invent
    /// synthetic calculations or un-emitted events (<c>TASK-195</c> §4.3).
    ///
    /// <b>This measures; it does not judge.</b> B-07's question — whether that
    /// regen is too strong — is Q-8's, and remains open.
    /// </summary>
    public required int BossRegenerationTotal { get; init; }

    /// <summary>
    /// M-12 — the number of Turns on which the Boss regained HP. Reported
    /// alongside the total because a single large heal and many small ones differ
    /// in gameplay even at an equal sum.
    /// </summary>
    public required int BossRegenerationTurns { get; init; }

    // ------------------------------------------------------------------
    // M-13 — Boss threshold events and cadence
    // ------------------------------------------------------------------

    /// <summary>
    /// M-13 — how many Boss Skill casts fired, counted from <c>BossSkillCast</c>
    /// (<c>BOSS_RULES.md</c> §4, §6.3). This is the documented Skill cadence
    /// measurement.
    /// </summary>
    public required int BossSkillCastCount { get; init; }

    /// <summary>
    /// M-13 — how many Boss Passive triggers fired, counted from
    /// <c>PassiveTriggered</c> reports whose source is the Boss
    /// (<c>PASSIVE_RULES.md</c> §7).
    /// </summary>
    public required int BossPassiveTriggerCount { get; init; }

    /// <summary>
    /// M-13 — the Turn number on which the Boss first became Enraged, or
    /// <c>null</c> if it never did.
    ///
    /// Enrage emits no event (<c>BOSS_RULES.md</c> §5 item 4), so this is derived
    /// from the Boss HP progression the harness already records, against the
    /// Boss's own declared <c>EnrageThreshold</c> — the definition's value, not
    /// one the harness chooses.
    /// </summary>
    public required int? BossEnrageTurn { get; init; }

    // ------------------------------------------------------------------
    // M-15 — Resource trace
    // ------------------------------------------------------------------

    /// <summary>
    /// M-15 — the checkpoint trace: one entry per resolved Turn, carrying the
    /// HP/Power/status state that <c>B-07</c> and <c>B-09</c> need in order to see
    /// how a fight developed rather than only how it ended.
    /// </summary>
    public required IReadOnlyList<BalanceResourceCheckpoint> ResourceTrace { get; init; }
}

/// <summary>
/// One checkpoint of the M-15 resource trace — the state at the end of one
/// resolved Turn.
///
/// Every member is read from the committed authoritative state; none is
/// computed. Status counts are included because Shield (<c>COMBAT_RULES.md</c>
/// §4) and Burn (§5.1) are the two effects B-07/B-09 reason about.
/// </summary>
/// <param name="Turn">The Turn this checkpoint ends (1-based).</param>
/// <param name="PlayerHp">The active Pet's HP at the end of that Turn.</param>
/// <param name="PlayerMaxHp">The active Pet's MaxHP — constant, carried for ratio reading.</param>
/// <param name="PlayerPower">The active Pet's Power at the end of that Turn.</param>
/// <param name="BossHp">The Boss's HP at the end of that Turn.</param>
/// <param name="PlayerStatusCount">How many Status Effects were active on the active Pet.</param>
/// <param name="BossStatusCount">How many Status Effects were active on the Boss.</param>
internal readonly record struct BalanceResourceCheckpoint(
    int Turn,
    int PlayerHp,
    int PlayerMaxHp,
    int PlayerPower,
    int BossHp,
    int PlayerStatusCount,
    int BossStatusCount);
