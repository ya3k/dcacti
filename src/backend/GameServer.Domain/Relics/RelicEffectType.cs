namespace GameServer.Domain.Relics;

/// <summary>
/// The effect identity a <see cref="RelicDefinition"/> carries in a
/// <see cref="RelicEffectDefinition"/> element — the <c>effectType</c> member of
/// the Relic representation <c>RELIC_RULES.md</c> §8.2 defines.
///
/// <code>
/// ATK          a stat modifier
/// Power        a Power grant (GAME_RULES.md §12)
/// Crit         a Crit-chance increase
/// CardCost     a Card-cost modifier
/// BurnDamage   a percentage modifier to Burn damage-over-time ticks
///              (COMBAT_RULES.md §5)
/// </code>
///
/// <b>The five members are exactly the closed set §8.2 item 1 defines</b>
/// (<c>ATK</c> | <c>Power</c> | <c>Crit</c> | <c>CardCost</c> |
/// <c>BurnDamage</c>), "carrying the effect identities §6's rows declare". A
/// sixth identity is a gameplay rule no document authors, and adding one would
/// invent content (<c>AGENTS.md</c> §7). <c>BurnDamage</c> is the only member
/// TASK-176 added; it is the identity Burning Curse's §6 row declares
/// (<c>RELIC_RULES.md</c> §8.5 item 5).
///
/// <b>It is a Relic-specific set, and it is NOT the Card set.</b> Reusing
/// <c>CardEffectType</c> would be wrong rather than merely convenient: the Card
/// set is <c>Heal | Shield | Power | Damage | Burn | Crit</c>
/// (<c>DATABASE.md</c> §1's Card contract), and the two sets overlap on
/// <c>Power</c> and <c>Crit</c> while differing on the others — <c>ATK</c>,
/// <c>CardCost</c>, and <c>BurnDamage</c> are Relic effects, <c>Heal</c>,
/// <c>Shield</c>, <c>Damage</c>, and <c>Burn</c> are Card effects (the Card
/// <c>Burn</c> is a Burn-applying effect; the Relic <c>BurnDamage</c> is a
/// modifier on an existing Burn tick, which §8.2 item 1 defines and
/// <c>COMBAT_RULES.md</c> §5.2 item 4 scopes). §8.2 item 1 says the Relic
/// representation follows the Card contract's <i>shape</i>; it does not say the
/// vocabulary is shared, and a shared enum would make each side accept
/// identities its own owner document does not define.
///
/// <b>It is the runtime discriminator.</b> §8.2 item 1 requires that "the
/// runtime must never derive a Relic's effect from parsed prose, from the
/// Relic's <c>Name</c>, from <c>RelicDefinitionId</c> mapping, or from
/// hardcoded per-Relic logic". It is a typed member rather than a free string so
/// an unrecognized stored token cannot silently become a no-op: an identity
/// outside this set is rejected when the definition is read
/// (<see cref="RelicEffectDefinition"/>).
///
/// <b>It says which effect, never how much.</b> The magnitude and its
/// interpretation are carried by <see cref="RelicEffectValueType"/> and
/// <see cref="RelicEffectDefinition.Value"/>; the effect's target and lifetime
/// are separate members (§8.3).
///
/// <b>This type stores an identity; it executes nothing.</b> Every domain write
/// site that would apply one of these effects — the Damage Pipeline's
/// composition (<c>COMBAT_RULES.md</c> §2/§3), Power's range
/// (<c>GAME_RULES.md</c> §12), the <c>NextAttack</c> Crit boundary
/// (<c>ADR-017</c>), and Card-cost composition (<c>CARD_RULES.md</c> §3.6) — is
/// reached only by the Relic resolution stage, which is <b>not implemented</b>
/// (<c>RELIC_RULES.md</c> §8.7) and is not introduced here. No member of this
/// enum applies, targets, or resolves anything.
///
/// <b>Member order carries no documented meaning.</b> The numeric values are
/// storage identifiers and no rule derives from them; the persisted contract
/// stores the member <b>names</b>, never these ordinals. The order below is a
/// stable reading aid and follows §8.3's table order.
/// </summary>
public enum RelicEffectType
{
    /// <summary>
    /// A stat modifier — §8.2 item 1: "<c>ATK</c> is a stat modifier". Its
    /// allowed combinations are <c>target: Pet</c> with
    /// <c>valueType: Percentage</c> and the <b>two</b> lifetimes §8.3's table
    /// lists for it — <c>Battle</c> (Berserker Core, §8.5 item 4) and
    /// <c>NextAttack</c> (Battle Instinct, §8.5 item 10; TASK-178 Product Owner
    /// decision <b>Q-1 = A</b>).
    ///
    /// <b>Both lifetimes ride one carrier.</b> §8.5 item 10 records that an
    /// applied <c>ATK</c> modification is carried by
    /// <c>PetState.ATKModifiers[]</c> (<c>GAME_STATE.md</c> §2.3.7/§5.1.4), whose
    /// element carries its declared <c>Lifetime</c>; no
    /// <c>NextAttackATKModifiers[]</c> and no generic
    /// <c>NextAttackModifiers[]</c> is introduced (<c>AGENTS.md</c> §9/§16).
    /// </summary>
    ATK = 0,

    /// <summary>
    /// A Power grant — §8.2 item 1: "<c>Power</c> is a Power grant
    /// (<c>GAME_RULES.md</c> §12)". Its allowed combination is <c>target: Pet</c>,
    /// <c>lifetime: Immediate</c>, <c>valueType: Flat</c> (§8.3); §8.3 item 3
    /// states that <c>Immediate</c> denotes an effect applied once, at the moment
    /// it triggers, leaving no standing modification behind.
    ///
    /// <b>Storing this identity executes nothing:</b> no Power is granted, no
    /// power-change event is emitted, and <c>PetState.Power</c> is untouched.
    /// </summary>
    Power = 1,

    /// <summary>
    /// A Crit-chance increase — §8.2 item 1: "<c>Crit</c> is a Crit-chance
    /// increase participating in <c>COMBAT_RULES.md</c> §2 item 7's
    /// <c>EffectiveCrit</c> composition". Its allowed combination is
    /// <c>target: Pet</c>, <c>lifetime: NextAttack</c>,
    /// <c>valueType: PercentagePoints</c> (§8.3).
    ///
    /// <b>Storing this identity executes nothing:</b> no Crit modifier is
    /// created, no roll is performed, and no <c>NextAttackCritModifiers[]</c>
    /// entry is written. <c>COMBAT_RULES.md</c> §2 item 7's composition, its cap,
    /// and its consumption boundary (<c>ADR-017</c>) are unchanged and are not
    /// restated here.
    /// </summary>
    Crit = 2,

    /// <summary>
    /// A Card-cost modifier — §8.2 item 1: "<c>CardCost</c> is a Card-cost
    /// modifier". Its allowed combination is <c>target: Pet</c>,
    /// <c>lifetime: Battle</c>, <c>valueType: Percentage</c> (§8.3).
    ///
    /// <b>Storing this identity executes nothing:</b> no cost modifier is
    /// applied, no condition is re-evaluated, and no Card cost is calculated.
    /// §8.5 item 2 records where an <i>applied</i> modifier lives
    /// (<c>GAME_STATE.md</c> §2.3.5), how it mutates (§5.1.3), and who owns the
    /// cost composition (<c>CARD_RULES.md</c> §3.6) — none of which is a content
    /// member and none of which is touched by this task.
    /// </summary>
    CardCost = 3,

    /// <summary>
    /// A percentage modifier to Burn damage-over-time ticks — §8.2 item 1:
    /// "<c>BurnDamage</c> is a percentage modifier to Burn damage-over-time ticks
    /// (<c>COMBAT_RULES.md</c> §5)". Its allowed combination is
    /// <c>target: Pet</c>, <c>lifetime: Battle</c>, <c>valueType: Percentage</c>
    /// (§8.3). Burning Curse is its one provisioned identity (§8.5 item 5).
    ///
    /// <b>It modifies Pet-owned Burn only.</b> §6 note 1 and
    /// <c>COMBAT_RULES.md</c> §5.2 item 4 scope the modifier by the Burn
    /// instance's <b>source/ownership</b>: Burn the Pet applied is modified, Burn
    /// the Boss applied is not, and the distinction is never the entity receiving
    /// the tick's damage (TASK-178 Product Owner decision <b>Q-4 = C</b>).
    /// <c>target: Pet</c> names the Pet as the owner/source context of the
    /// modifier, not a damage recipient.
    ///
    /// <b>It changes damage and nothing else.</b> §5.2 item 4: it must not emit,
    /// create, re-enter, or refresh a Burn event or Burn instance, and it must not
    /// extend or consume a Burn instance's duration. It is one percentage applied
    /// to the tick's damage, not a second Burn system.
    /// </summary>
    BurnDamage = 4,
}
