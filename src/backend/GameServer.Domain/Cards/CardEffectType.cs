namespace GameServer.Domain.Cards;

/// <summary>
/// The effect identity a <see cref="CardDefinition"/> carries in a
/// <see cref="CardEffectDefinition"/> element — the <c>effectType</c> member of
/// <c>DATABASE.md</c> §1's stored contract.
///
/// <code>
/// Heal       restore HP
/// Shield     grant an absorption pool
/// Power      grant Power (Power Charge)
/// Damage     deal damage — the base value entering the Damage Pipeline
/// Burn       apply damage over time
/// Crit       increase Crit chance
/// </code>
///
/// <b>The six members are exactly the closed set <c>DATABASE.md</c> §1 item 1
/// defines</b> (TASK-111 decision D-2, which the Product Owner supplied
/// verbatim): <c>Heal | Shield | Power | Damage | Burn | Crit</c>. <c>Heal</c>,
/// <c>Shield</c>, and <c>Power</c> are the three effects <c>CARD_RULES.md</c>
/// §2 documents; <c>Damage</c>, <c>Burn</c>, and <c>Crit</c> are the three the
/// same owner named for the §4.1 Pet Skill Cards, while confirming the first
/// three remain. A seventh identity is a gameplay rule no document authors, and
/// adding one would invent content (<c>AGENTS.md</c> §7). The identities are
/// named after the effect, not after a Card: the identity says <i>which domain
/// effect</i> applies, and the Card names <c>CARD_RULES.md</c> uses ("Heal",
/// "Shield", "Power Charge") are display text that no rule derives behavior
/// from (D-1 rejects name inference).
///
/// <b><see cref="Damage"/> is distinct from <see cref="Power"/>.</b> TASK-111
/// decision D-4 records it: <see cref="Power"/> continues to denote Power Charge
/// (<c>CARD_RULES.md</c> §2), while <see cref="Damage"/> carries a
/// damage-dealing effect's base value — the input to <c>COMBAT_RULES.md</c> §3
/// step 1, whose magnitudes <c>CARD_RULES.md</c> §4.1 owns. The two are never
/// collapsed: a damage effect stored as <see cref="Power"/> would select the
/// wrong domain effect.
///
/// <b>It is the runtime discriminator.</b> D-1 requires effect identity to come
/// from structured effect data, so the runtime reads this member rather than
/// parsing prose, inferring from the Card's name, mapping from
/// <see cref="CardDefinition.CardDefinitionId"/>, or hardcoding card-specific
/// logic. It is a typed member rather than a free string so an unrecognized
/// stored token cannot silently become a no-op: an identity outside this set is
/// rejected when the definition is read (<see cref="CardEffectDefinition"/>).
///
/// <b>It says which effect, never how much.</b> The magnitude, and the rule for
/// interpreting it, are carried separately by
/// <see cref="CardEffectDefinition.ValueType"/> and
/// <see cref="CardEffectDefinition.Value"/>; an effect that needs a parameter
/// <c>value</c> alone cannot carry stores it as an extra member on the same
/// element (<see cref="CardEffectDefinition.Duration"/> for
/// <see cref="Burn"/>, <see cref="CardEffectDefinition.Scope"/> for
/// <see cref="Crit"/>). An effect whose magnitude the documents leave unauthored
/// still names its identity and records the absence through
/// <see cref="CardEffectValueType.Undetermined"/>.
///
/// <b>This type stores an identity; it executes nothing.</b> The domain write
/// sites that own each effect's application — Heal's clamp
/// (<c>COMBAT_RULES.md</c> §4 item 1), Shield's contract (§4), Power's range
/// (<c>GAME_RULES.md</c> §12), and the Damage Pipeline with its Burn and Crit
/// rules (§3/§3.3/§5) — are reached only by a resolver, which is a separate,
/// unimplemented concern (<c>CARD_RULES.md</c> §3). No member of this enum
/// applies, targets, or resolves anything.
///
/// <b>Member order carries no documented meaning.</b> The numeric values are
/// storage identifiers and no rule derives from them; the persisted contract
/// stores the member <b>names</b>, never these ordinals
/// (<c>DATABASE.md</c> §1 item 2). The order below is a stable reading aid.
/// </summary>
public enum CardEffectType
{
    /// <summary>
    /// Restore HP (<c>CARD_RULES.md</c> §2, Heal). The write site that applies
    /// it and owns the Max HP clamp is the existing
    /// <c>ResourceGenerator.ApplyHeal</c> (<c>COMBAT_RULES.md</c> §4 item 1);
    /// nothing in this task calls it.
    /// </summary>
    Heal = 0,

    /// <summary>
    /// Grant an absorption pool (<c>CARD_RULES.md</c> §2, Shield). The write
    /// site that applies it and owns one-instance-per-entity, refresh-replaces,
    /// no additive stacking, and the trigger-based expiry is the existing
    /// <c>StatusEffectLifecycle.ApplyShield</c> (<c>COMBAT_RULES.md</c> §4,
    /// TASK-105); nothing in this task calls it, and no Shield semantic is
    /// defined or altered here.
    /// </summary>
    Shield = 1,

    /// <summary>
    /// Grant Power (<c>CARD_RULES.md</c> §2, Power Charge). The write site that
    /// applies it and owns the range clamp is the existing
    /// <c>ResourceGenerator.ApplyPower</c> (<c>GAME_RULES.md</c> §12); nothing
    /// in this task calls it.
    ///
    /// <b>It is not the Card's Cost, and it is not damage.</b> Power Charge's
    /// Cost is <c>0</c> by design while its effect is a positive Power gain
    /// (<c>CARD_RULES.md</c> §2 item 3); the Cost is
    /// <see cref="CardDefinition.PowerCost"/>. A damage-dealing effect is
    /// <see cref="Damage"/>, a distinct member (TASK-111 D-4), not this one.
    /// </summary>
    Power = 2,

    /// <summary>
    /// Deal damage (<c>CARD_RULES.md</c> §4.1, Inferno and Iron Fang). The
    /// magnitude is the effect's base value as the owning document states it —
    /// "flat base value, entering the Damage Pipeline as the Card/Skill base
    /// value" (<c>COMBAT_RULES.md</c> §3 step 1).
    ///
    /// <b>It is a member distinct from <see cref="Power"/></b> (TASK-111 D-4):
    /// this identity denotes damage dealing, while <see cref="Power"/> continues
    /// to denote Power Charge. It is read from content — never from the Card's
    /// name or <see cref="CardDefinition.CardDefinitionId"/> (D-1).
    ///
    /// <b>Storing this identity executes nothing.</b> The Damage Pipeline's
    /// formula, Element Modifier, DEF, and Crit steps remain owned by
    /// <c>COMBAT_RULES.md</c> §3/§3.3 and are untouched; no damage is computed
    /// anywhere in this task, and no Crit roll exists in MVP.
    /// </summary>
    Damage = 3,

    /// <summary>
    /// Apply damage over time (<c>CARD_RULES.md</c> §4.1, Inferno's Burn: "50
    /// damage per tick for 2 Turns").
    ///
    /// <b>Its element carries two magnitudes.</b> <c>value</c> is the
    /// <b>damage per tick</b> and the extra <c>duration</c> member is the number
    /// of Turns (<c>DATABASE.md</c> §1 item 1, TASK-111 D-3). The stored unit is
    /// the authoritative Turn / End-Turn-tick unit owned by
    /// <c>GAME_RULES.md</c> §17 step 19a and <c>COMBAT_RULES.md</c> §5.1–§5.2.
    ///
    /// <b>Storing this identity executes nothing.</b> No Burn instance is
    /// created, no tick is scheduled, and no damage is applied:
    /// <c>StatusEffectLifecycle</c> applies and expires status effects but does
    /// not tick damage-over-time, and this task does not change that.
    /// </summary>
    Burn = 4,

    /// <summary>
    /// Increase Crit chance (<c>CARD_RULES.md</c> §4.1, Iron Fang: "increase Crit
    /// chance by 10 percentage points for the next attack only").
    ///
    /// <b>Its element carries the scope.</b> <c>value</c> is the increase in
    /// percentage points with <see cref="CardEffectValueType.PercentagePoints"/>
    /// as its interpretation, and the extra <c>scope</c> member is
    /// <c>NextAttack</c> (<c>DATABASE.md</c> §1 item 1, TASK-111 D-3). The
    /// next-attack scope is a rule <c>CARD_RULES.md</c> §4.1 already authors; the
    /// stored member authors no new one.
    ///
    /// <b>Storing this identity executes nothing.</b> No Crit roll is performed:
    /// <c>COMBAT_RULES.md</c> §3.3's roll and its RNG infrastructure do not exist
    /// in MVP, and implementing one would introduce a randomization mechanism no
    /// document authorizes (<c>AGENTS.md</c> §11).
    /// </summary>
    Crit = 5,
}
