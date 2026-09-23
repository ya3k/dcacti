namespace GameServer.Domain.Combat;

/// <summary>
/// Which entity a damage instance is applied to — the <c>source</c> and
/// <c>target</c> members <c>GAME_EVENTS.md</c> §2's <c>DamageDealt</c> and
/// <c>DamageTaken</c> carry.
///
/// <code>
/// DamageDealt:  source, target, Final Damage amount   (GAME_EVENTS.md §2)
/// DamageTaken:  source, target, Final Damage amount   (GAME_EVENTS.md §2)
/// </code>
///
/// <b>Why this is a small enum and not an identity string.</b> §2 fixes the
/// members as "source, target, Final Damage amount" and does not define how a
/// party is identified. What the pipeline must state is the <i>role</i> of each
/// side — who dealt the damage and who took it — and in MVP that role is
/// unambiguous: <c>COMBAT_RULES.md</c> §3's pipeline serves exactly two
/// directions, the player's damage instance against the Boss
/// (<c>GAME_RULES.md</c> §17 steps 15–17) and the Boss's against the player
/// (§17 step 18b–18c, §3.4), and each names its two sides from this set. A
/// per-entity identity (<c>BossId</c>, a future Pet identity) is a
/// content/loadout concern that §2 does not ask this payload for, and inventing
/// one here would be a representation the documentation does not define
/// (<c>GAME_STATE.md</c> §0 item 5, <c>AGENTS.md</c> §7).
///
/// <b>Both directions are represented.</b> Player→Boss damage carries
/// <c>source = Player</c>, <c>target = Boss</c>; Boss→Player damage carries
/// <c>source = Boss</c>, <c>target = Player</c> — which is exactly the
/// widening §3.2.14 item 3 records for the Boss damage instance, and why
/// <see cref="DamagePipeline"/> takes both sides from its caller rather than
/// reading a direction from a constant.
/// </summary>
public enum DamageParty
{
    /// <summary>The player's side — <c>PlayerState</c> and the active Pet.</summary>
    Player = 0,

    /// <summary>The Boss — <c>BossState</c> (<c>GAME_STATE.md</c> §2.4).</summary>
    Boss = 1,
}

/// <summary>
/// The <c>DamageCalculated</c> report — the full breakdown of one damage
/// instance (<c>GAME_EVENTS.md</c> §2).
///
/// <code>
/// DamageCalculated: Base, Combo Modifier, Element Modifier, Other Modifiers,
///                   Defense, Final Damage (full breakdown, for client feedback)
/// </code>
///
/// <b>All six documented members are present, in the pipeline's order.</b>
/// <c>COMBAT_RULES.md</c> §3 fixes the order — Base, × Combo, × Element,
/// × Other, − Defense, → Final Damage — and §3.1 makes it mandatory ("Steps 1–6
/// must execute in this order for every damage instance"). The members below are
/// named exactly as §2 names them and appear in that order, so the breakdown
/// reads as the pipeline does.
///
/// <b>The modifiers are reported as the factors the pipeline applied, not as
/// deltas.</b> §2 lists "Combo Modifier", "Element Modifier", and "Other
/// Modifiers" beside "Base" and "Final Damage" as the values that produce the
/// final number, and the pipeline applies each as a multiplication. Reporting
/// the factor is therefore what lets a client show the pipeline the way
/// <c>GDD</c>'s Design Philosophy asks — a number and the multipliers that
/// produced it — and it is the form the configuration types already carry
/// (<see cref="ComboModifiers"/>, <see cref="Elements.ElementModifiers"/>).
///
/// <b>The values are reported at the precision the pipeline computes them
/// in.</b> The multipliers are <c>double</c> because that is the form
/// <see cref="Elements.ElementModifiers"/> stores the Element factors in
/// (<c>ELEMENT_RULES.md</c> §2.2 owns those as <c>1.50</c> / <c>1.00</c> /
/// <c>0.75</c>), and <see cref="Defense"/> is a <c>double</c> because the
/// mitigation formula divides by <c>(K + DEF)</c> and its result is genuinely
/// fractional (<c>96 × 100/140 ≈ 68.57</c> — <c>COMBAT_RULES.md</c> §3.2). The
/// <b>FinalDamage</b> is an <c>int</c>: §3 step 6 turns the pipeline into
/// damage, and §3.2's worked value is truncated toward zero to <c>68</c>. The
/// report therefore never presents a fraction as if it were the damage dealt.
///
/// <b>This is a Domain value, not a wire type.</b> It introduces no protocol
/// message and defines no serialization (<c>GAME_EVENTS.md</c> §3 item 1,
/// <c>SIGNALR_PROTOCOL.md</c> §8 item 1), exactly as the Match-3 event payloads
/// and the Passive reports do not. Delivering it is a protocol change owned by
/// its own task.
/// </summary>
/// <param name="Base">
/// Step 1 — Base Damage: <c>PlayerState.ATK + ResourceGeneration.BaseDamagePool</c>
/// (<c>COMBAT_RULES.md</c> §3 step 1: "from ATK stat, Skill/Card base value, and
/// any ATK-Gem-generated damage pool for this action").
/// </param>
/// <param name="ComboModifier">
/// Step 2 — the factor <c>GAME_RULES.md</c> §5's table selects for the Swap's
/// Combo (<c>COMBAT_RULES.md</c> §3 step 2: "× Combo Modifier (GAME_RULES.md §5
/// table)").
/// </param>
/// <param name="ElementModifier">
/// Step 3 — the factor <c>ELEMENT_RULES.md</c> §2.2 assigns to the resolved
/// matchup (<c>COMBAT_RULES.md</c> §3 step 3: "× Element Modifier
/// (ELEMENT_RULES.md §2.2)").
/// </param>
/// <param name="OtherModifiers">
/// Step 4 — the combined Relic / Passive / Buff / Debuff / Crit factor.
///
/// <b>It is a pass-through of <c>1.00</c> in MVP, and the member is required
/// even so.</b> <c>GAME_EVENTS.md</c> §2 lists it in the <c>DamageCalculated</c>
/// payload, and <c>COMBAT_RULES.md</c> §3.1 makes step 4 a stage the pipeline
/// always has ("step 4 as a whole always resolves before step 5"). No Relic,
/// Passive damage bonus, Buff/Debuff, or Crit roll exists in MVP — §3.3's Crit
/// roll, <c>RELIC_RULES.md</c>, and <c>PASSIVE_RULES.md</c> are separate
/// unimplemented systems — so the stage multiplies by the identity factor and
/// reports that identity. Omitting the member would understate the breakdown the
/// contract asks for, and would make a later step-4 implementation a payload
/// change rather than a value change.
/// </param>
/// <param name="Defense">
/// Step 5 — the damage after the target's Defense Mitigation, before §3 step 6
/// turns it into damage: <c>Pre-Defense Damage × ( K / (K + DEF) )</c>
/// (<c>COMBAT_RULES.md</c> §3.2). It is fractional wherever the division is
/// (<c>96 × 100/140 ≈ 68.57</c>), which is why it is reported as a
/// <c>double</c> and not as <see cref="FinalDamage"/>.
/// </param>
/// <param name="FinalDamage">
/// Step 6 — the Final Damage: <see cref="Defense"/> truncated toward zero, never
/// negative (<c>COMBAT_RULES.md</c> §3 step 6: "minimum 0"). It is the value
/// applied to Boss HP and the amount <c>DamageDealt</c>/<c>DamageTaken</c>
/// report.
/// </param>
public readonly record struct DamageCalculation(
    int Base,
    double ComboModifier,
    double ElementModifier,
    double OtherModifiers,
    double Defense,
    int FinalDamage);

/// <summary>
/// One <c>DamageDealt</c> report — the damage one party dealt to another
/// (<c>GAME_EVENTS.md</c> §2).
///
/// <code>
/// DamageDealt: source, target, Final Damage amount   (GAME_EVENTS.md §2)
/// </code>
///
/// <b>Domain value, not a wire type</b> (<c>GAME_EVENTS.md</c> §3 item 1), for
/// the same reason as <see cref="DamageCalculated"/>.
/// </summary>
/// <param name="Source">
/// The party that dealt the damage. In this stage's damage instance that is the
/// player's side (<c>GAME_RULES.md</c> §17 steps 15–17).
/// </param>
/// <param name="Target">The party that received it — the Boss.</param>
/// <param name="Amount">
/// The Final Damage applied (<c>COMBAT_RULES.md</c> §3 step 6), the same value
/// <see cref="DamageCalculation.FinalDamage"/> reports.
/// </param>
public readonly record struct DamageDealtEvent(
    DamageParty Source,
    DamageParty Target,
    int Amount)
{
    /// <summary>"DamageDealt (Player → Boss, 68)" — for test diagnostics only.</summary>
    public override string ToString() => $"DamageDealt ({Source} -> {Target}, {Amount})";
}

/// <summary>
/// One <c>DamageTaken</c> report — the damage one party took from another
/// (<c>GAME_EVENTS.md</c> §2).
///
/// <code>
/// DamageTaken: source, target, Final Damage amount   (GAME_EVENTS.md §2)
/// </code>
///
/// <b>It is the same damage instance as <see cref="DamageDealtEvent"/>, reported
/// from the receiver's side.</b> <c>GAME_EVENTS.md</c> §2 gives both events
/// identical members, and §1's ordering list places <c>DamageCalculated</c>
/// then <c>DamageDealt</c> then <c>DamageTaken</c> — so the two are two
/// statements about one instance, not two applications of damage. Boss HP is
/// reduced once, by <see cref="DamagePipeline"/>; these reports describe it.
///
/// <b>Domain value, not a wire type</b> (<c>GAME_EVENTS.md</c> §3 item 1).
/// </summary>
/// <param name="Source">The party that dealt the damage — the player's side.</param>
/// <param name="Target">The party that received it — the Boss.</param>
/// <param name="Amount">
/// The Final Damage taken (<c>COMBAT_RULES.md</c> §3 step 6), the same value
/// <see cref="DamageCalculation.FinalDamage"/> reports.
/// </param>
public readonly record struct DamageTakenEvent(
    DamageParty Source,
    DamageParty Target,
    int Amount)
{
    /// <summary>"DamageTaken (Player → Boss, 68)" — for test diagnostics only.</summary>
    public override string ToString() => $"DamageTaken ({Source} -> {Target}, {Amount})";
}

/// <summary>
/// The result of one damage instance through the Damage Pipeline — the defending
/// target's post-damage HP together with the three documented reports
/// (<c>GAME_RULES.md</c> §17 steps 15–17, <c>COMBAT_RULES.md</c> §3, §3.4,
/// <c>GAME_EVENTS.md</c> §2).
///
/// <code>
/// DamageResult
/// ├── Calculation    the full DamageCalculated breakdown
/// ├── TargetHp       the target's HP after Final Damage was applied
/// ├── DamageDealt    source, target, amount
/// └── DamageTaken    source, target, amount
/// </code>
///
/// <b>The HP and the events describe one instance.</b> <see cref="TargetHp"/>
/// carries the applied damage and the reports carry the same number, so a
/// consumer cannot observe an HP that moved by an amount no event explains
/// (<c>GAME_EVENTS.md</c> §3 item 6: an event is never a substitute for the state
/// write-back, and the write-back is never replaced by an event).
/// <c>GAME_STATE.md</c> §5.1's single write-back is preserved: the caller writes
/// <see cref="TargetHp"/> onto the state record it owns — the Boss's
/// <c>BossState</c> or the player's <c>PlayerState</c> — in the same write-back
/// the rest of the Swap's resolution uses.
///
/// <b>It is an HP, not a state record, because the pipeline serves both
/// directions.</b> <c>COMBAT_RULES.md</c> §3.4 puts Boss→Player damage through the
/// same steps 1–6, and its target is a <c>PlayerState</c> rather than a
/// <c>BossState</c>. Returning the one value both directions produce keeps this a
/// single result type and a single pipeline; the direction-specific part — which
/// record the value is written back onto — stays with the caller that owns that
/// record.
///
/// <b>The reports are always present, including when Final Damage is 0.</b>
/// <c>GAME_EVENTS.md</c> §1 places the three events unconditionally on the
/// resolution, and a damage instance of 0 (<c>BaseDamagePool = 0</c> with
/// <c>ATK = 0</c>) is still an instance the pipeline evaluated and the client
/// must be able to render. Emitting nothing would make "no damage instance
/// occurred" indistinguishable from "an instance of 0 occurred".
/// </summary>
/// <param name="Calculation">
/// The <c>DamageCalculated</c> payload — Base, Combo Modifier, Element Modifier,
/// Other Modifiers, Defense, Final Damage (<c>GAME_EVENTS.md</c> §2).
/// </param>
/// <param name="TargetHp">
/// The defending target's HP after the Final Damage was applied, clamped so HP is
/// never negative (<c>COMBAT_RULES.md</c> §3 step 6, <c>GAME_RULES.md</c> §1.4).
/// The caller writes it onto the target record it owns; this stage transitions no
/// Boss State, fires no Boss mechanic, and touches no other field.
/// </param>
/// <param name="DamageDealt">
/// The <c>DamageDealt</c> report — source, target, Final Damage amount.
/// </param>
/// <param name="DamageTaken">
/// The <c>DamageTaken</c> report — source, target, Final Damage amount.
/// </param>
public readonly record struct DamageResult(
    DamageCalculation Calculation,
    int TargetHp,
    DamageDealtEvent DamageDealt,
    DamageTakenEvent DamageTaken)
{
    /// <summary>
    /// The three documented events of one damage instance, in the order
    /// <c>GAME_EVENTS.md</c> §1 places them: <c>DamageCalculated</c>,
    /// <c>DamageDealt</c>, <c>DamageTaken</c>.
    ///
    /// It exists so the stage that assembles the resolution's event list appends
    /// this instance's reports in the documented order without re-deciding it.
    /// The values are the ones this result already carries — nothing is rebuilt,
    /// reordered, or recomputed.
    /// </summary>
    public DamageEvents Events => new(Calculation, DamageDealt, DamageTaken);
}

/// <summary>
/// One damage instance's three reports, in the documented order
/// (<c>GAME_EVENTS.md</c> §1: <c>DamageCalculated</c>, <c>DamageDealt</c>,
/// <c>DamageTaken</c>).
///
/// <code>
/// DamageEvents
/// ├── Calculated    the full breakdown   (GAME_EVENTS.md §2)
/// ├── Dealt         source, target, amount
/// └── Taken         source, target, amount
/// </code>
///
/// <b>No instance identity is carried.</b> MVP resolves exactly one damage
/// instance per committed Swap (<c>COMBAT_RULES.md</c> §3 has one instance per
/// action, and this stage implements the player's), so there is no second
/// instance for a consumer to tell this one apart from, and no id, index, or
/// sequence number is invented to separate them.
/// </summary>
/// <param name="Calculated">The <c>DamageCalculated</c> breakdown.</param>
/// <param name="Dealt">The <c>DamageDealt</c> report.</param>
/// <param name="Taken">The <c>DamageTaken</c> report.</param>
public readonly record struct DamageEvents(
    DamageCalculation Calculated,
    DamageDealtEvent Dealt,
    DamageTakenEvent Taken);
