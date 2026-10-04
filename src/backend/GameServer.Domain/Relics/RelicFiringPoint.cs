namespace GameServer.Domain.Relics;

/// <summary>
/// The <b>firing point</b> a Relic resolution pass runs for — which documented
/// event of <c>RELIC_RULES.md</c> §3 is being processed, and therefore which
/// Relics are eligible for that pass.
///
/// <code>
/// BoardResolution    the committed Swap's resolution state — GAME_RULES.md §17
///                    step 11 ("Trigger Relics"). Its eligible Triggers are
///                    OnMatchCount, OnCombo, and OnHpBelow.
/// BattleStart        battle start — §3's OnBattleStart ("fires once, at battle
///                    start")
/// CascadeIteration   one Match-3 cascade iteration — §3.2 ("Each actual cascade
///                    iteration is an independent OnCascade event"), whose
///                    iteration identity MATCH3_RULES.md §4.2 owns
/// PowerGain          one qualifying Power gain — §3.1 ("qualifying
///                    non-Relic-generated Power gains")
/// DamageTaken        one resolved damage instance the active Pet took — §3's
///                    OnDamageTaken ("fires when the active Pet takes damage")
/// </code>
///
/// <b>This is a firing point, not a new Trigger.</b> Every value above names an
/// event one of §3's <b>existing</b> twelve Trigger values is documented to fire
/// at; §3's closed list is unchanged, no Trigger is added, removed, or
/// reinterpreted, and a Relic still declares exactly one primary Trigger from it
/// (§8.5 item 3). The type exists because §3 gives different Triggers different
/// firing points, so a resolution pass must state which point it is (unchanged
/// from what §3's own per-Trigger wording already says).
///
/// <b><see cref="BoardResolution"/> is the only multi-Trigger point, and that is
/// §3's own grouping.</b> <c>GAME_RULES.md</c> §17 step 11 is one step, and
/// <c>RELIC_RULES.md</c> §8.1 item 8 fixes the point at which
/// <c>MatchCountAtLeast</c>, <c>ComboAtLeast</c>, and <c>HpPercentageBelow</c>
/// are all read: after step 10 and before steps 12–14. Those three Triggers
/// therefore share one firing point, and each other Trigger has its own.
///
/// <b><see cref="CascadeIteration"/> is fired once per iteration, never
/// aggregated.</b> §3.2 item 3: "Cascades from one Swap are <b>NOT</b> collapsed
/// into one aggregated <c>OnCascade</c>" — each iteration is its own event and
/// therefore its own resolution pass.
///
/// <b>Member order carries no documented meaning</b>; no rule derives from these
/// ordinals and nothing persists them.
/// </summary>
public enum RelicFiringPoint
{
    /// <summary>
    /// The committed Swap's resolution state — <c>GAME_RULES.md</c> §17 step 11,
    /// whose eligible Triggers are <c>OnMatchCount</c>, <c>OnCombo</c>, and
    /// <c>OnHpBelow</c> (<c>RELIC_RULES.md</c> §3, §8.1 item 8).
    /// </summary>
    BoardResolution = 0,

    /// <summary>
    /// Battle start — <c>RELIC_RULES.md</c> §3's <c>OnBattleStart</c>, "fires
    /// once, at battle start". Burning Curse's declared Trigger (§6 note 1, §8.5
    /// item 5).
    /// </summary>
    BattleStart = 1,

    /// <summary>
    /// One Match-3 cascade iteration — <c>RELIC_RULES.md</c> §3.2, whose
    /// iteration identity is <c>MATCH3_RULES.md</c> §4.2's (depth 1 is not a
    /// Cascade; <c>d ≥ 2</c> is). Cascade Core's declared Trigger (§8.5 item 9).
    /// </summary>
    CascadeIteration = 2,

    /// <summary>
    /// One qualifying Power gain — <c>RELIC_RULES.md</c> §3.1, "qualifying
    /// non-Relic-generated Power gains". Arcane Battery's declared Trigger (§8.5
    /// item 7).
    /// </summary>
    PowerGain = 3,

    /// <summary>
    /// One resolved damage instance the active Pet took —
    /// <c>RELIC_RULES.md</c> §3's <c>OnDamageTaken</c>. Battle Instinct's declared
    /// Trigger (§8.5 item 10).
    /// </summary>
    DamageTaken = 4,
}
