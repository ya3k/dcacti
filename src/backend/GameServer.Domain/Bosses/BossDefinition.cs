using GameServer.Domain.Battle;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;

namespace GameServer.Domain.Bosses;

/// <summary>
/// A Boss's definition — the configuration <see cref="BossState"/> is built from
/// (<c>BOSS_RULES.md</c> §1, §6; <c>GAME_STATE.md</c> §2.4).
///
/// <code>
/// Boss
/// ├── BossDefinitionId  persistence key (DATABASE.md §1, TASK-049)
/// ├── BossId       identity (BOSS_RULES.md §6.4)
/// ├── Element      exactly one of the Five Elements (§1, §6.1)
/// ├── HP / MaxHP / ATK / DEF              (§6.1)
/// ├── PassiveId / PassiveThreshold / PassiveResetBehavior   (§3, §6.2, §6.4)
/// ├── SkillId / SkillBaseDamage / SkillChargeRequirement /
/// │   SkillCooldownTurns                                    (§4, §6.3, §6.4)
/// └── EnrageThreshold                     (§5 item 4, §6.1)
/// </code>
///
/// <b>The definition and the state are different things.</b> A definition is
/// static content — which Boss exists, its Element, its base stats, and its
/// Passive/Skill configuration. The <see cref="BossState"/> a battle carries is
/// that definition applied at battle creation (<see cref="BossState.Initial"/>),
/// plus the values a resolution changes (current HP, State, Passive progress,
/// Skill charge and cooldown). The identity wrapper <see cref="BossId"/> holds
/// only the name, and this type holds only the numbers and identities, so neither
/// duplicates the other (<c>GAME_STATE.md</c> §0 item 5).
///
/// <b>The stats are configuration, not invariants.</b> <c>BOSS_RULES.md</c>
/// §6.1 states the MVP values are "MVP base configuration — not universal
/// balance invariants. The project owner approved these values. They are
/// configuration defaults used at battle creation; they do not represent
/// formulas or scaling rules." They are therefore data on this record and are
/// deliberately not expressed as formulas over the player's stats. §6.3 states
/// the same for the Skill's charge requirement, cooldown, and base damage.
///
/// <b>No registry, service, or repository abstraction.</b> The MVP definitions
/// are the static values of <see cref="BossDefinitions"/> — plain in-code
/// configuration, per <c>ARCHITECTURE.md</c> §5 item 1's "each is a concrete
/// Domain type with data-driven configuration (numbers only)" and
/// <c>ARCHITECTURE.md</c> §1's <c>Bosses/</c> module. No
/// <c>BossRegistry</c>, <c>BossService</c>, <c>BossRepository</c>,
/// <c>BossDatabase</c>, or <c>BossFactory</c> is introduced: nothing in the
/// documented contract requires one, and <c>AGENTS.md</c> §9 forbids the
/// abstraction.
///
/// <b>The persisted row does not carry the combat stats.</b>
/// <c>DATABASE.md</c> §1's <c>BossDefinition</c> table stores the row's
/// persistence key, its canonical technical Identity, its Element, and its
/// Passive/Skill configuration. The combat-definition values
/// <see cref="MaxHP"/>, <see cref="ATK"/>, <see cref="DEF"/>, and
/// <see cref="EnrageThreshold"/> are deliberately <b>not</b> columns: §1's
/// persistence contract states they "remain sourced from the authoritative
/// Domain <c>BossDefinition</c> content at battle creation". Both live on this
/// one type; only the documented subset is persisted, and no field is removed
/// from the Domain because the table does not store it.
/// <b>It is a reference type because it is persisted.</b> EF Core maps entity
/// types as reference types, and this definition is the entity
/// <c>DATABASE.md</c> §1's <c>BossDefinition</c> table stores — the same shape
/// <see cref="Pets.PetDefinition"/>, <see cref="Cards.CardDefinition"/>, and
/// <see cref="Relics.RelicDefinition"/> already have. A <c>record class</c>
/// keeps the value-style semantics the call sites rely on (structural equality,
/// and <c>with</c> expressions for scenario overrides) without a second,
/// parallel persistence model. The type itself remains free of any persistence
/// framework reference.
/// </summary>
/// <param name="BossDefinitionId">
/// The row's persistence primary key (<c>DATABASE.md</c> §1,
/// <c>BOSS_RULES.md</c> §6.4) — an independent, stable, caller/content-supplied
/// string. It is <b>neither</b> the canonical technical Identity
/// (<paramref name="BossId"/>) <b>nor</b> the display name: the three are
/// never collapsed (<c>DATABASE.md</c> §1 contract note item 1), and this value
/// is never derived from either at runtime. Value form is
/// <c>boss-def-&lt;ascii-kebab-case-name&gt;</c> (<c>DATABASE.md</c> §1 note
/// item 2), e.g. <c>"boss-def-hoa-long"</c>. It is content-supplied and never
/// database-generated.
/// </param>
/// <param name="BossId">
/// The Boss's identity (<c>BOSS_RULES.md</c> §6.4, <c>GAME_STATE.md</c> §2.4) —
/// the value <c>POST /api/battle/start</c> resolves against
/// (<c>API_CONTRACTS.md</c> §3: "bossId must be a valid MVP Boss —
/// BOSS_RULES.md §6"), and the value Boss events report as <c>sourceId</c>
/// (<c>SIGNALR_PROTOCOL.md</c> §3.2.16–§3.2.18).
/// </param>
/// <param name="Element">
/// The Boss's one Element (<c>BOSS_RULES.md</c> §1, §6.1;
/// <c>ELEMENT_RULES.md</c> §1.2). MVP Boss assignments are owned by
/// <c>BOSS_RULES.md</c> §6 and are not restated or reassigned here.
/// </param>
/// <param name="MaxHP">
/// The Boss's maximum health (<c>BOSS_RULES.md</c> §6.1). It is also the
/// <c>HP</c> a battle begins with (<see cref="BossState.Initial"/>), because a
/// battle starts with the Boss at full health.
/// </param>
/// <param name="ATK">
/// The Boss's attack power (<c>BOSS_RULES.md</c> §6.1) — the persistent half of a
/// Boss attack's Base Damage (<c>COMBAT_RULES.md</c> §3.4).
/// </param>
/// <param name="DEF">
/// The Boss's defense (<c>BOSS_RULES.md</c> §6.1) — the value
/// <c>COMBAT_RULES.md</c> §3.2's mitigation formula consumes when the Boss is the
/// defending target.
/// </param>
/// <param name="PassiveId">
/// The identity of the Boss's one Passive (<c>BOSS_RULES.md</c> §3, §6.4;
/// <c>GAME_STATE.md</c> §2.4.2) — the value <c>PassiveCharged</c>/
/// <c>PassiveTriggered</c> report as <c>passiveId</c> when
/// <c>source = "boss"</c>. It is an identity, not a definition, and carries no
/// threshold or effect.
/// </param>
/// <param name="PassiveThreshold">
/// The Boss Passive's Threshold — "Every 5 Player Matches" for Hỏa Long and Mộc
/// Yêu (<c>BOSS_RULES.md</c> §6.2).
///
/// <b><c>0</c> is the Always-Active marker, and it is not a threshold.</b> §6.2
/// gives Thủy Ma the trigger "Passive (always active)" — an alternate trigger
/// (<c>PASSIVE_RULES.md</c> §3), not a Match count — so it has no
/// match-counting Threshold. <c>PassiveTracker</c> rejects a Threshold below
/// <c>1</c> (<c>PASSIVE_RULES.md</c> §1 defines one as a Match count), so the
/// resolution <b>never calls</b> <c>PassiveTracker.Charge</c> for such a Boss and
/// emits no match-driven <c>PassiveCharged</c>/<c>PassiveTriggered</c> for it.
/// The field is still declared, so the Boss's configuration states its trigger
/// kind explicitly rather than by omission.
/// </param>
/// <param name="PassiveResetBehavior">
/// The Boss Passive's declared non-default Reset Behavior, or <c>null</c> for the
/// default (<c>PASSIVE_RULES.md</c> §4 items 1–3) — the same contract
/// <see cref="PetState.PassiveResetOverride"/> carries for a Pet. §4 item 1's
/// default resets progress to <c>0</c> after the Passive triggers, and §6.2
/// declares no non-default behavior for any MVP Boss, so every MVP definition
/// leaves this absent. A caller supplying one does so explicitly; it is never
/// assumed.
/// </param>
/// <param name="SkillId">
/// The identity of the Boss's Skill (<c>BOSS_RULES.md</c> §4, §6.4) — the value
/// <c>BossSkillCast</c> reports as <c>skillId</c>
/// (<c>SIGNALR_PROTOCOL.md</c> §3.2.18). Canonical MVP values are §6.4's
/// (<c>"flame-burst"</c>, <c>"drain-power"</c>, <c>"root"</c>).
/// </param>
/// <param name="SkillBaseDamage">
/// The Skill/Card base value term of the Boss Skill's Base Damage
/// (<c>COMBAT_RULES.md</c> §3 step 1, §3.4: the Skill's Step 1 is "defined per
/// Skill"). The Boss Skill's Base Damage is
/// <c>BossState.ATK + SkillBaseDamage</c> — a separate additive component, not a
/// replacement for ATK and not part of the ATK-Gem pool. MVP values are
/// <c>BOSS_RULES.md</c> §6.3's.
/// </param>
/// <param name="SkillChargeRequirement">
/// The number of Player Matches required before the Skill is eligible to fire
/// (<c>BOSS_RULES.md</c> §6.3). The Skill fires when
/// <c>SkillCharge ≥ SkillChargeRequirement</c> <b>and</b> <c>SkillCooldown == 0</c>
/// (<c>GAME_STATE.md</c> §2.4.3). It is independent of
/// <paramref name="PassiveThreshold"/>.
/// </param>
/// <param name="SkillCooldownTurns">
/// The turns the Skill is blocked for after it fires (<c>BOSS_RULES.md</c> §6.3).
/// <c>SkillCooldown</c> is set to this value when the Skill fires and decrements
/// by 1 per Turn increment.
/// </param>
/// <param name="EnrageThreshold">
/// The fraction of <paramref name="MaxHP"/> below which the Boss becomes Enraged
/// (<c>BOSS_RULES.md</c> §5 item 4, §6.1: "1500 (30%)"). The comparison is
/// strict — <c>BossHP &lt; MaxHP × EnrageThreshold</c> — and the transition is
/// permanent.
/// </param>
public sealed record class BossDefinition(
    string BossDefinitionId,
    BossId BossId,
    Element Element,
    BossPassiveDefinition PassiveDefinition,
    BossSkillDefinition SkillDefinition)
{
    /// <summary>
    /// The Boss's maximum health (<c>BOSS_RULES.md</c> §6.1). It is also the
    /// <c>HP</c> a battle begins with (<see cref="BossState.Initial"/>).
    ///
    /// <b>It is Domain configuration, not a persisted column.</b>
    /// <c>DATABASE.md</c> §1 note item 1 states the combat-definition values
    /// <c>MaxHP</c>, <c>ATK</c>, <c>DEF</c>, and <c>EnrageThreshold</c> are
    /// <b>not</b> stored in the <c>BossDefinition</c> table — they "remain
    /// sourced from the authoritative Domain <c>BossDefinition</c> content at
    /// battle creation". It is therefore declared outside the constructor (the
    /// persisted surface) and excluded from the persistence model.
    /// </summary>
    public int MaxHP { get; init; } = BossRules.BaseMaxHP;

    /// <summary>
    /// The Boss's attack power (<c>BOSS_RULES.md</c> §6.1) — the persistent half
    /// of a Boss attack's Base Damage (<c>COMBAT_RULES.md</c> §3.4). Domain
    /// configuration, not a persisted column (<c>DATABASE.md</c> §1 note item 1).
    /// </summary>
    public int ATK { get; init; } = BossRules.BaseATK;

    /// <summary>
    /// The Boss's defense (<c>BOSS_RULES.md</c> §6.1) — the value
    /// <c>COMBAT_RULES.md</c> §3.2's mitigation formula consumes when the Boss is
    /// the defending target. Domain configuration, not a persisted column
    /// (<c>DATABASE.md</c> §1 note item 1).
    /// </summary>
    public int DEF { get; init; } = BossRules.BaseDEF;

    /// <summary>
    /// The fraction of <see cref="MaxHP"/> below which the Boss becomes Enraged
    /// (<c>BOSS_RULES.md</c> §5 item 4, §6.1: "1500 (30%)"). The comparison is
    /// strict — <c>BossHP &lt; MaxHP × EnrageThreshold</c> — and the transition
    /// is permanent. Domain configuration, not a persisted column
    /// (<c>DATABASE.md</c> §1 note item 1).
    /// </summary>
    public double EnrageThreshold { get; init; } = BossRules.BaseEnrageThreshold;

    /// <summary>
    /// The Boss's one Passive identity (<c>BOSS_RULES.md</c> §3, §6.4;
    /// <c>GAME_STATE.md</c> §2.4.2) — the value <c>PassiveCharged</c>/
    /// <c>PassiveTriggered</c> report as <c>passiveId</c> when
    /// <c>source = "boss"</c>. A projection of
    /// <see cref="BossPassiveDefinition.PassiveId"/>.
    /// </summary>
    public PassiveId PassiveId => PassiveDefinition.PassiveId;

    /// <summary>
    /// The Boss Passive's match-charging Threshold (<c>BOSS_RULES.md</c> §6.2).
    ///
    /// <b><c>0</c> is the Always-Active marker, and it is not a threshold.</b>
    /// §6.2 gives Thủy Ma the alternate "Passive (always active)" trigger
    /// (<c>PASSIVE_RULES.md</c> §3), so it has no match-counting Threshold, and
    /// <c>PassiveTracker</c> rejects a Threshold below <c>1</c>
    /// (<c>PASSIVE_RULES.md</c> §1). Storage records the documented <c>null</c>
    /// for that case, never <c>0</c> (<c>DATABASE.md</c> §1 note item 3). A
    /// projection of <see cref="BossPassiveDefinition.Threshold"/>.
    /// </summary>
    public int PassiveThreshold => PassiveDefinition.Threshold ?? 0;

    /// <summary>
    /// The Boss Passive's declared non-default Reset Behavior, or <c>null</c>
    /// for the default (<c>PASSIVE_RULES.md</c> §4 items 1–3). A projection of
    /// <see cref="BossPassiveDefinition.ResetBehavior"/>; the storage token
    /// <c>Persistent</c> is the Domain's <c>NoReset</c> (<c>DATABASE.md</c> §1
    /// note item 3), and the token <c>Default</c> is the Domain's absent value.
    /// </summary>
    public PassiveResetBehavior? PassiveResetBehavior => PassiveDefinition.ResetBehavior switch
    {
        "Partial" => Passives.PassiveResetBehavior.Partial,
        "Persistent" => Passives.PassiveResetBehavior.NoReset,
        _ => null,
    };

    /// <summary>
    /// The identity of the Boss's Skill (<c>BOSS_RULES.md</c> §4, §6.4) — the
    /// value <c>BossSkillCast</c> reports as <c>skillId</c>. A projection of
    /// <see cref="BossSkillDefinition.SkillId"/>.
    /// </summary>
    public string SkillId => SkillDefinition.SkillId;

    /// <summary>
    /// The Skill/Card base value term of the Boss Skill's Base Damage
    /// (<c>COMBAT_RULES.md</c> §3 step 1, §3.4: the Skill's Step 1 is "defined
    /// per Skill"). A projection of <see cref="BossSkillDefinition.BaseDamage"/>.
    /// </summary>
    public int SkillBaseDamage => SkillDefinition.BaseDamage;

    /// <summary>
    /// The number of Player Matches required before the Skill is eligible to
    /// fire (<c>BOSS_RULES.md</c> §6.3). A projection of
    /// <see cref="BossSkillDefinition.ChargeRequirement"/>.
    /// </summary>
    public int SkillChargeRequirement => SkillDefinition.ChargeRequirement;

    /// <summary>
    /// The turns the Skill is blocked for after it fires
    /// (<c>BOSS_RULES.md</c> §6.3). A projection of
    /// <see cref="BossSkillDefinition.CooldownTurns"/>.
    /// </summary>
    public int SkillCooldownTurns => SkillDefinition.CooldownTurns;

    /// <summary>
    /// The <see cref="BossState"/> this definition produces at battle creation —
    /// the documented Initial State with the Boss at full health, its Passive's
    /// identity and the start of that Passive's progress, and no Skill charge or
    /// cooldown (<c>GAME_STATE.md</c> §2.4, §2.4.1–§2.4.3;
    /// <c>BOSS_RULES.md</c> §6.1).
    ///
    /// It is a delegation to <see cref="BossState.Initial"/>, which owns the rules
    /// that <c>HP = MaxHP</c>, <c>State = Idle</c>, progress starts at <c>0</c>,
    /// and the Skill begins uncharged and off cooldown: the definition supplies
    /// its own values and this method decides none of them.
    /// </summary>
    public BossState ToInitialState() =>
        BossState.Initial(
            BossId,
            Element,
            MaxHP,
            ATK,
            DEF,
            PassiveId,
            PassiveThreshold);
}

/// <summary>
/// The persisted <c>PassiveDefinition</c> object of a Boss definition
/// (<c>DATABASE.md</c> §1 note item 3).
///
/// <code>
/// { "passiveId": "…", "threshold": 5, "resetBehavior": "Default" }
/// </code>
///
/// <b>The member names are the storage contract.</b> §1 states "the
/// JSON/storage names are the contract; internal representation maps to them,
/// not vice versa", so these names are §1's, not the Domain's field names.
///
/// <b><see cref="Threshold"/> is <c>int?</c> and <c>null</c> means
/// always-active.</b> §1 note item 3: "<c>null</c> means the Passive has <b>no
/// threshold and is always active</b>; <c>0</c> is never used as a 'no
/// threshold' sentinel".
///
/// <b><see cref="ResetBehavior"/> is the storage token, not the enum.</b> §1
/// note item 3 fixes exactly <c>Default</c> | <c>Partial</c> |
/// <c>Persistent</c> and documents the internal <c>NoReset</c> ↔
/// <c>Persistent</c> mapping.
/// </summary>
/// <param name="PassiveId">The Boss's canonical PassiveId (<c>BOSS_RULES.md</c> §6.4).</param>
/// <param name="Threshold">
/// The Passive's match-charging Threshold, or <c>null</c> when it has none and
/// is always active (<c>PASSIVE_RULES.md</c> §1, <c>BOSS_RULES.md</c> §6.2).
/// </param>
/// <param name="ResetBehavior">
/// The Reset Behavior storage token — <c>Default</c>, <c>Partial</c>, or
/// <c>Persistent</c> (<c>PASSIVE_RULES.md</c> §4).
/// </param>
public readonly record struct BossPassiveDefinition(
    PassiveId PassiveId,
    int? Threshold,
    string ResetBehavior);

/// <summary>
/// The persisted <c>SkillDefinition</c> object of a Boss definition
/// (<c>DATABASE.md</c> §1 note item 4).
///
/// <code>
/// { "skillId": "…", "baseDamage": 0, "chargeRequirement": 0, "cooldownTurns": 0 }
/// </code>
///
/// Exactly four members, all required (§1 note item 4). No <c>damageType</c>,
/// <c>element</c>, <c>name</c>, <c>description</c>, <c>target</c>, or
/// <c>effects</c> member exists. The battle-state member names
/// (<c>GAME_STATE.md</c> §2.4) are a separate contract and are not used here.
/// </summary>
/// <param name="SkillId">The Boss's canonical SkillId (<c>BOSS_RULES.md</c> §6.4).</param>
/// <param name="BaseDamage">Skill Base Dmg (<c>BOSS_RULES.md</c> §6.3).</param>
/// <param name="ChargeRequirement">Charge Requirement (<c>BOSS_RULES.md</c> §6.3).</param>
/// <param name="CooldownTurns">Cooldown (CD) in turns (<c>BOSS_RULES.md</c> §6.3).</param>
public readonly record struct BossSkillDefinition(
    string SkillId,
    int BaseDamage,
    int ChargeRequirement,
    int CooldownTurns);

/// <summary>
/// The <c>BOSS_RULES.md</c> §6.1 MVP Boss base-stat values, shared by the three
/// content-defined Bosses. They are the documented defaults a
/// <see cref="BossDefinition"/> carries when its scenario does not restate them
/// (§6.1: "MVP base configuration — not universal balance invariants … used at
/// battle creation").
///
/// They exist because the combat stats are <b>not</b> persisted
/// (<c>DATABASE.md</c> §1 note item 1): they live on the Domain definition, and
/// a definition read back from persistence takes these documented values rather
/// than a column's.
/// </summary>
public static class BossRules
{
    /// <summary><c>BOSS_RULES.md</c> §6.1: 5000 for each MVP Boss.</summary>
    public const int BaseMaxHP = 5000;

    /// <summary><c>BOSS_RULES.md</c> §6.1: 100 for each MVP Boss.</summary>
    public const int BaseATK = 100;

    /// <summary><c>BOSS_RULES.md</c> §6.1: 50 for each MVP Boss.</summary>
    public const int BaseDEF = 50;

    /// <summary><c>BOSS_RULES.md</c> §6.1: "1500 (30%)" of MaxHP.</summary>
    public const double BaseEnrageThreshold = 0.30;
}
