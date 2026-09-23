using GameServer.Domain.Bosses;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;

namespace GameServer.Domain.Battle;

/// <summary>
/// The Boss's battle state (<c>GAME_STATE.md</c> §2.4).
///
/// <code>
/// BattleState
/// └── BossState
///     ├── BossId       identity of the Boss being fought           (§2.4)
///     ├── Element      the Boss's one Element (BOSS_RULES.md §1)   (§2.4)
///     ├── HP           current health    (COMBAT_RULES.md §1.2)     (§2.4)
///     ├── MaxHP        maximum health    (COMBAT_RULES.md §1.2)     (§2.4)
///     ├── ATK          attack power      (COMBAT_RULES.md §1.2)     (§2.4)
///     ├── DEF          defense           (COMBAT_RULES.md §1.2)     (§2.4)
///     ├── State        Idle / Charging / Enraged / Stunned          (§2.4)
///     ├── PassiveId    which Boss Passive (BOSS_RULES.md §3)        (§2.4)
///     ├── PassiveProgress  charging position (§2.4, §2.5)           (§2.4)
///     ├── SkillCharge  matches charged toward the Skill (§2.4.3)    (§2.4)
///     └── SkillCooldown turns remaining before the Skill can fire    (§2.4)
/// </code>
///
/// <b>This is the documented owner, not a new decision.</b> <c>GAME_STATE.md</c>
/// §2.4 places every field above here, and §2 nests <c>BossState</c> inside
/// <c>BattleState</c>. All of them are therefore ordinary <b>Active Battle
/// State</b>: authoritative, server-produced, and written in the same single
/// post-resolution write-back as <c>Turn</c>, <c>Sequence</c>,
/// <c>BoardState</c>, <c>RngState</c>, <c>PlayerState</c>, and <c>PetState</c>
/// (§5.1). There is no second representation of any value — not on
/// <c>BattleState</c>, and not on the transient <c>ResolutionContext</c>
/// (§0 item 5).
///
/// <b>The stats are configuration, not invariants.</b> <c>BOSS_RULES.md</c>
/// §6.1 states the MVP values are "MVP base configuration — not universal
/// balance invariants", approved by the project owner, and used as
/// configuration defaults at battle creation. This type therefore holds the
/// current values as data and defines no formula over them: it does not derive
/// HP from the player's HP, or ATK/DEF from the player's ATK/DEF, and it does
/// not give different Bosses different stats. The values come from the Boss's
/// definition (<see cref="BossDefinition"/>), exactly as
/// <c>PlayerState</c>'s combat stats come from <c>COMBAT_RULES.md</c> §1.1's
/// configuration.
///
/// <b>Only the fields this stage requires exist.</b> §2.4 also lists
/// <c>StatusEffects[]</c>. That belongs to the Status Effects system
/// (<c>COMBAT_RULES.md</c> §5) and is <b>not yet implemented</b>, not <b>not
/// required</b> (§0 item 4, §2.0.5.3): it is added by its own owning task,
/// exactly as this stage adds the eleven above. It is not stubbed, defaulted, or
/// represented by a placeholder, because a placeholder for a field no rule yet
/// reads would be a representation of its own (§0 item 5).
///
/// <b>The Boss's Passive and Skill charging state are carried, not resolved.</b>
/// §2.4.1 lists <c>PassiveId</c>, <c>PassiveProgress</c>, <c>SkillCharge</c>,
/// and <c>SkillCooldown</c> as the fields implemented by the Boss Response
/// stage, and §2.4.2/§2.4.3 give their meaning. This type holds them as state:
/// it does not charge the Passive, evaluate its Threshold, increment
/// <c>SkillCharge</c>, decrement <c>SkillCooldown</c>, or decide whether the
/// Skill fires. Those are the resolution's steps, owned by
/// <c>BattleStateService.ExecuteSwap</c> (<c>GAME_RULES.md</c> §17 step 18a–18c).
///
/// <b>No Boss mechanic is implemented by this type.</b> <c>BOSS_RULES.md</c>
/// §3's Passive, §4's Skill, §5's State transitions, the Damage Pipeline
/// (<c>COMBAT_RULES.md</c> §3), Boss Response (<c>GAME_RULES.md</c> §17 step
/// 18), and Victory/Defeat (step 19) are explicitly out of scope and remain
/// owned by their own stages. In particular <c>State</c> is carried as a value
/// and never transitioned here: §5 item 3 defers a full multi-phase State
/// machine to Future Expansion.
///
/// <b>No <c>Status</c> field and no lifecycle value.</b> A battle has no
/// lifecycle state machine (<c>GAME_STATE.md</c> §2.0.3), and this stage adds
/// none. <c>State</c> is the Boss's own documented enum (§2.4,
/// <c>BOSS_RULES.md</c> §1) and is not a battle lifecycle value.
///
/// This type is deliberately minimal and framework-independent
/// (<c>ARCHITECTURE.md</c> §2.1): it references no ASP.NET Core, SignalR, EF
/// Core, Redis, HTTP, Phaser, or Discord concern.
/// </summary>
/// <param name="BossId">
/// The identity of the Boss being fought (<c>GAME_STATE.md</c> §2.4,
/// <c>BOSS_RULES.md</c> §6). A battle has exactly one Boss
/// (<c>GAME_RULES.md</c> §1.1), so this names one Boss — it does not select
/// among several.
///
/// It is <b>set at battle creation and never changes</b>: §2's initialization
/// path resolves <c>bossId</c> to a Boss definition once, and nothing in a
/// resolution rewrites it. It is the value <c>GAME_EVENTS.md</c> §2's
/// <c>BattleStarted</c> reports, read and reported rather than re-derived.
///
/// It is <b>not</b> an absent-when-unset convention: a battle always has its one
/// Boss (<c>GAME_EVENTS.md</c> §2 <c>BattleStarted</c> "requires a created
/// battle with a Pet and a Boss"), so it is present from battle creation with no
/// null or "no Boss yet" form. <see cref="BossState"/> is therefore not
/// nullable on <see cref="BattleState"/>.
/// </param>
/// <param name="Element">
/// The Boss's Element (<c>GAME_STATE.md</c> §2.4, <c>BOSS_RULES.md</c> §1) — the
/// defending Element the Element Modifier is resolved against
/// (<c>ELEMENT_RULES.md</c> §2.1, §5; <c>COMBAT_RULES.md</c> §3 step 3).
///
/// Every Boss has <b>exactly one</b> Element and MVP supports no dual/multi
/// element entity (<c>ELEMENT_RULES.md</c> §1.2, §7), so this is a single value
/// and not a collection. It is a non-nullable <see cref="Element"/>: §1.1 makes
/// Element an ownership every Boss has, so there is no elementless-Boss case
/// for a nullable member to spell. (An elementless <i>damage source</i> is the
/// absence <c>ElementMatchups.Resolve</c> accepts as <c>null</c> — that is the
/// attacker's side, not the Boss's.)
///
/// It is set from the Boss's definition at battle creation and never changes.
/// This type performs no matchup resolution: <see cref="ElementMatchups"/> owns
/// that, and the Damage Pipeline that consumes its result is unimplemented.
/// </param>
/// <param name="HP">
/// The Boss's current health (<c>COMBAT_RULES.md</c> §1.2, <c>GAME_STATE.md</c>
/// §2.4). It starts equal to <see cref="MaxHP"/> — a battle begins with the Boss
/// at full health — and is the value the Damage Pipeline reduces
/// (<c>COMBAT_RULES.md</c> §3) and the win/loss check reads
/// (<c>GAME_RULES.md</c> §1.4: "a battle ends when either the Boss or Player
/// reaches 0 HP").
///
/// <b>Nothing in this type computes, clamps, or compares it.</b> Damage
/// application and Victory/Defeat are still unimplemented and remain owned by
/// their own stages: no damage is applied here, and no <c>BattleWon</c>/
/// <c>BattleLost</c> outcome is derived from it.
/// </param>
/// <param name="MaxHP">
/// The Boss's maximum health (<c>COMBAT_RULES.md</c> §1.2,
/// <c>GAME_STATE.md</c> §2.4). Its MVP value is the Boss definition's
/// (<c>BOSS_RULES.md</c> §6.1), which is configuration and not an invariant
/// (§6.1). Boss healing and regeneration are unimplemented
/// (<c>BOSS_RULES.md</c> §6: Mộc Yêu's Passive regenerates HP) and no ceiling is
/// enforced here.
/// </param>
/// <param name="ATK">
/// The Boss's attack power (<c>COMBAT_RULES.md</c> §1.2, <c>GAME_STATE.md</c>
/// §2.4) — the stat Boss Skill damage is read from (<c>BOSS_RULES.md</c> §4
/// item 3: "Boss Skill damage (if any) goes through the full Damage Pipeline").
/// Its MVP value is the Boss definition's (<c>BOSS_RULES.md</c> §6.1).
///
/// No Boss attack, Skill, or damage instance is implemented here
/// (<c>BOSS_RULES.md</c> §4 is out of scope).
/// </param>
/// <param name="DEF">
/// The Boss's defense (<c>COMBAT_RULES.md</c> §1.2, <c>GAME_STATE.md</c> §2.4)
/// — the value the mitigation formula consumes as the defending target's DEF
/// (<c>COMBAT_RULES.md</c> §3.2: <c>Mitigated Damage = Pre-Defense Damage ×
/// ( K / (K + DEF) )</c>). Its MVP value is the Boss definition's
/// (<c>BOSS_RULES.md</c> §6.1).
///
/// No mitigation is computed here, and <c>K</c> is not defined by this type:
/// §3.2 owns the formula and the constant.
/// </param>
/// <param name="State">
/// The Boss's internal State (<c>GAME_STATE.md</c> §2.4, <c>BOSS_RULES.md</c>
/// §1) — one of <see cref="BossStateKind"/>'s four members.
///
/// It begins at <see cref="BossStateKind.Idle"/>, which is the documented
/// <b>Initial State</b> of every MVP Boss (<c>BOSS_RULES.md</c> §6.1).
///
/// §5 item 3 defers a full multi-phase State machine to Future Expansion, so the
/// only transition implemented is §5 item 4's Enrage — a permanent
/// <c>Idle → Enraged</c> transition applied by the resolution when
/// <c>BossHP &lt; MaxHP × EnrageThreshold</c>. That transition is not made by
/// this type: it is applied by <c>BattleStateService.ExecuteSwap</c> in the
/// documented order, after Player→Boss damage and before the Boss HP terminal
/// check. No Stun rule is implemented (<c>BOSS_RULES.md</c> §5 item 5).
/// </param>
/// <param name="PassiveId">
/// The identity of the Boss's one Passive (<c>GAME_STATE.md</c> §2.4, §2.4.2;
/// <c>BOSS_RULES.md</c> §3) — the value the shared
/// <c>PassiveCharged</c>/<c>PassiveTriggered</c> events report as
/// <c>passiveId</c> (<c>GAME_EVENTS.md</c> §2, <c>SIGNALR_PROTOCOL.md</c>
/// §3.2.16–§3.2.17).
///
/// It is set at battle creation from the Boss's definition
/// (<see cref="BossDefinition.PassiveId"/>) and never changes, exactly as
/// <see cref="PetState.PassiveId"/> is set from the Pet's Passive definition
/// (§2.4.2, §2.3 item 2). The canonical MVP values are <c>BOSS_RULES.md</c>
/// §6.4's (<c>"boss-hoa-long-rage"</c>, <c>"boss-thuy-ma-heal"</c>,
/// <c>"boss-moc-yeu-regen"</c>).
/// </param>
/// <param name="PassiveProgress">
/// The Boss Passive's charging position — the progress reached and the
/// Threshold it is measured against (<c>GAME_STATE.md</c> §2.4, §2.4.2, §2.5).
///
/// It starts at <see cref="PassiveProgress.AtStart"/> with the Boss definition's
/// <see cref="BossDefinition.PassiveThreshold"/> (§2.4.2) and is written after
/// each committed Swap's Boss Passive step in the same single post-resolution
/// write-back as the rest of the state (§5.1).
///
/// <b>A Threshold of <c>0</c> is the Always-Active marker, not a zero-Match
/// threshold.</b> <c>BOSS_RULES.md</c> §6.2 gives Thủy Ma the trigger "Passive
/// (always active)" — an alternate trigger (<c>PASSIVE_RULES.md</c> §3), not a
/// match count. <c>PassiveTracker.Charge</c> rejects a Threshold below <c>1</c>,
/// and the resolution skips charging entirely for such a Boss: no
/// <c>PassiveCharged</c>/<c>PassiveTriggered</c> is emitted from match progress.
/// This type holds the marker and applies no meaning to it.
/// </param>
/// <param name="SkillCharge">
/// The matches charged toward the Boss Skill's charge requirement
/// (<c>GAME_STATE.md</c> §2.4, §2.4.3; <c>BOSS_RULES.md</c> §6.3).
///
/// It starts at <c>0</c> — no Match has been counted at battle creation — and is
/// incremented by the committed Swap's Player Match total. It is
/// <b>independent</b> of <see cref="PassiveProgress"/>: neither resets the other.
/// The Skill fires when this reaches <see cref="BossDefinition.SkillChargeRequirement"/>
/// <b>and</b> <see cref="SkillCooldown"/> is <c>0</c>, at which point it resets
/// to <c>0</c>. The firing decision is the resolution's, not this type's.
/// </param>
/// <param name="SkillCooldown">
/// The turns remaining before the Boss Skill can fire again
/// (<c>GAME_STATE.md</c> §2.4, §2.4.3; <c>BOSS_RULES.md</c> §6.3).
///
/// It starts at <c>0</c> — no Skill has been cast, so nothing is on cooldown —
/// and decrements by 1 per Turn increment, blocked while <c>&gt; 0</c>. When the
/// Skill fires it is set to <see cref="BossDefinition.SkillCooldownTurns"/>.
/// The decrement and the reset are the resolution's, not this type's.
/// </param>
public readonly record struct BossState(
    BossId BossId,
    Element Element,
    int HP,
    int MaxHP,
    int ATK,
    int DEF,
    BossStateKind State,
    PassiveId PassiveId,
    PassiveProgress PassiveProgress,
    int SkillCharge,
    int SkillCooldown)
{
    /// <summary>
    /// The documented Initial State of every MVP Boss (<c>BOSS_RULES.md</c>
    /// §6.1: "Initial State — Idle" for Hỏa Long, Thủy Ma, and Mộc Yêu;
    /// <c>GAME_STATE.md</c> §2.4).
    ///
    /// <c>Idle</c> is a real value here, not an "absent" convention and not an
    /// omitted field (<c>GAME_STATE.md</c> §2.1.7 item 5): it is the State a
    /// battle begins in, before any Passive or Skill condition has changed it.
    /// </summary>
    public const BossStateKind InitialState = BossStateKind.Idle;

    /// <summary>
    /// Whether the Boss is in its documented Initial State — the value
    /// <see cref="Initial"/> produces, before any Boss mechanic has changed it.
    /// </summary>
    public bool IsIdle => State == InitialState;

    /// <summary>
    /// The documented <c>BossState</c> of a newly created battle: the Boss's
    /// identity, its Element, and its stats at full health, in the Initial State,
    /// with its Passive's identity and the start of that Passive's progress, and
    /// no Skill charge or cooldown yet (<c>GAME_STATE.md</c> §2.4, §2.4.1–§2.4.3;
    /// <c>BOSS_RULES.md</c> §6.1).
    ///
    /// <code>
    /// HP            = MaxHP                          (§2.4 / BOSS §6.1)
    /// State         = Idle                           (§2.4 / BOSS §6.1)
    /// PassiveId     = passiveId                      (§2.4.2)
    /// PassiveProgress = AtStart(passiveThreshold)    (§2.4.2)
    /// SkillCharge   = 0                              (§2.4.3)
    /// SkillCooldown = 0                              (§2.4.3)
    /// </code>
    ///
    /// <b>The Element and the stats are supplied, not invented.</b> They are the
    /// Boss definition's own values (<c>BOSS_RULES.md</c> §6.1, and
    /// <see cref="BossDefinition"/> for the MVP definitions), exactly as
    /// <c>PetState.AtBattleCreation</c> takes the Passive's identity and
    /// Threshold from the Passive's definition rather than choosing them. This
    /// factory therefore defines no stat formula and no per-Boss variation: it
    /// applies the documented initial values above and carries the rest
    /// across unchanged.
    ///
    /// It is the same shape as <see cref="PlayerState.Initial"/> —
    /// <c>HP == MaxHP</c> at the start of a battle — and, like it, is a static
    /// factory rather than a defaulted parameter, so a caller must supply the
    /// Boss's real configuration rather than letting one be defaulted with an
    /// invented <c>MaxHP</c> or Element.
    ///
    /// Battle creation is not a resolution and starts no Turn
    /// (<c>MATCH3_RULES.md</c> §8.1 item 4), so no Boss mechanic has run: the
    /// returned State is <see cref="InitialState"/>, the Skill is neither charged
    /// nor cooling down, and no event is emitted.
    /// </summary>
    /// <param name="bossId">
    /// The Boss's identity (<c>GAME_STATE.md</c> §2.4) — the value the battle was
    /// created against (<c>API_CONTRACTS.md</c> §3) and the value
    /// <c>BattleStarted</c> reports (<c>GAME_EVENTS.md</c> §2).
    /// </param>
    /// <param name="element">
    /// The Boss's one Element (<c>BOSS_RULES.md</c> §1, §6.1). It comes from the
    /// Boss definition and is never chosen here.
    /// </param>
    /// <param name="maxHp">
    /// The Boss's <c>MaxHP</c> from its definition (<c>BOSS_RULES.md</c> §6.1).
    /// This is the value <c>HP</c> starts at.
    /// </param>
    /// <param name="atk">
    /// The Boss's <c>ATK</c> from its definition (<c>BOSS_RULES.md</c> §6.1).
    /// </param>
    /// <param name="def">
    /// The Boss's <c>DEF</c> from its definition (<c>BOSS_RULES.md</c> §6.1).
    /// </param>
    /// <param name="passiveId">
    /// The identity of the Boss's one Passive, from its definition
    /// (<c>GAME_STATE.md</c> §2.4.2, <c>BOSS_RULES.md</c> §3, §6.4).
    /// </param>
    /// <param name="passiveThreshold">
    /// The Boss Passive's Threshold — "every N Player Matches" for the
    /// match-charged Bosses, and <c>0</c> as the Always-Active marker for Thủy Ma
    /// (<c>BOSS_RULES.md</c> §6.2). Progress begins at <c>0</c> against it.
    /// </param>
    public static BossState Initial(
        BossId bossId,
        Element element,
        int maxHp,
        int atk,
        int def,
        PassiveId passiveId,
        int passiveThreshold) =>
        // §2.4 / BOSS_RULES.md §6.1: a battle begins with the Boss at full health
        // and in the Idle State. §2.4.2–§2.4.3: the Passive's identity is set at
        // creation with its progress at the start of its first charge, and the
        // Skill is neither charged nor on cooldown. All are the documented initial
        // values, not chosen here, and nothing is derived from the player's stats.
        new(
            bossId,
            element,
            maxHp,
            maxHp,
            atk,
            def,
            InitialState,
            passiveId,
            PassiveProgress.AtStart(passiveThreshold),
            SkillCharge: 0,
            SkillCooldown: 0);
}
