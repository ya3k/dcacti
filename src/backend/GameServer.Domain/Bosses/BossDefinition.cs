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
/// <b>No registry, service, or persistence.</b> The MVP definitions are the
/// static values of <see cref="BossDefinitions"/> — plain in-code
/// configuration, per <c>ARCHITECTURE.md</c> §5 item 1's "each is a concrete
/// Domain type with data-driven configuration (numbers only)" and
/// <c>ARCHITECTURE.md</c> §1's <c>Bosses/</c> module. No
/// <c>BossRegistry</c>, <c>BossService</c>, <c>BossRepository</c>,
/// <c>BossDatabase</c>, or <c>BossFactory</c> is introduced: nothing in the
/// documented contract requires one, and <c>AGENTS.md</c> §9 forbids the
/// abstraction.
/// </summary>
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
public readonly record struct BossDefinition(
    BossId BossId,
    Element Element,
    int MaxHP,
    int ATK,
    int DEF,
    PassiveId PassiveId,
    int PassiveThreshold,
    string SkillId,
    int SkillBaseDamage,
    int SkillChargeRequirement,
    int SkillCooldownTurns,
    double EnrageThreshold,
    PassiveResetBehavior? PassiveResetBehavior = null)
{
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
