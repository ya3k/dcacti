using GameServer.Domain.Battle;
using GameServer.Domain.Elements;

namespace GameServer.Domain.Bosses;

/// <summary>
/// A Boss's definition — the configuration <see cref="BossState"/> is built from
/// (<c>BOSS_RULES.md</c> §1, §6; <c>GAME_STATE.md</c> §2.4).
///
/// <code>
/// Boss
/// ├── BossId       identity (BOSS_RULES.md §6)
/// ├── Element      exactly one of the Five Elements (§1, §6.1)
/// └── HP / MaxHP / ATK / DEF              (§6.1)
/// </code>
///
/// <b>The definition and the state are different things.</b> A definition is
/// static content — which Boss exists, its Element, and its base stats. The
/// <see cref="BossState"/> a battle carries is that definition applied at battle
/// creation (<see cref="BossState.Initial"/>), plus the values a resolution
/// changes (current HP, State). The identity wrapper <see cref="BossId"/> holds
/// only the name, and this type holds only the numbers, so neither duplicates
/// the other (<c>GAME_STATE.md</c> §0 item 5).
///
/// <b>The stats are configuration, not invariants.</b> <c>BOSS_RULES.md</c>
/// §6.1 states the MVP values are "MVP base configuration — not universal
/// balance invariants. The project owner approved these values. They are
/// configuration defaults used at battle creation; they do not represent
/// formulas or scaling rules." They are therefore data on this record and are
/// deliberately not expressed as formulas over the player's stats.
///
/// <b>Only what constructing a <see cref="BossState"/> requires.</b> A Boss's
/// <c>Passive</c> and <c>Skill</c> are also part of its §1 structure, but they
/// are the Boss Passive and Boss Skill systems' content
/// (<c>BOSS_RULES.md</c> §3–§4) and are <b>not yet implemented</b>, not <b>not
/// required</b> (<c>GAME_STATE.md</c> §0 item 4). They are not stubbed here, and
/// no <c>PassiveId</c>/<c>SkillId</c> placeholder is invented: a field no rule
/// yet reads would be a representation of its own (§0 item 5).
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
/// The Boss's identity (<c>BOSS_RULES.md</c> §6, <c>GAME_STATE.md</c> §2.4) —
/// the value <c>POST /api/battle/start</c> resolves against
/// (<c>API_CONTRACTS.md</c> §3: "bossId must be a valid MVP Boss —
/// BOSS_RULES.md §6").
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
/// The Boss's attack power (<c>BOSS_RULES.md</c> §6.1).
/// </param>
/// <param name="DEF">
/// The Boss's defense (<c>BOSS_RULES.md</c> §6.1) — the value
/// <c>COMBAT_RULES.md</c> §3.2's mitigation formula consumes.
/// </param>
public readonly record struct BossDefinition(
    BossId BossId,
    Element Element,
    int MaxHP,
    int ATK,
    int DEF)
{
    /// <summary>
    /// The <see cref="BossState"/> this definition produces at battle creation —
    /// the documented Initial State with the Boss at full health
    /// (<c>GAME_STATE.md</c> §2.4, <c>BOSS_RULES.md</c> §6.1).
    ///
    /// It is a delegation to <see cref="BossState.Initial"/>, which owns the rule
    /// that <c>HP = MaxHP</c> and <c>State = Idle</c>: the definition supplies
    /// its own values and this method decides none of them.
    /// </summary>
    public BossState ToInitialState() =>
        BossState.Initial(BossId, Element, MaxHP, ATK, DEF);
}
