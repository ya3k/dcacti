namespace GameServer.Domain.Battle;

/// <summary>
/// The Boss's internal State enum (<c>GAME_STATE.md</c> §2.4,
/// <c>BOSS_RULES.md</c> §1, <c>COMBAT_RULES.md</c> §1.2).
///
/// <code>
/// Idle
/// Charging
/// Enraged
/// Stunned
/// </code>
///
/// <b>The vocabulary is <c>BOSS_RULES.md</c> §1's, exactly.</b> §1 defines the
/// Boss's State as "(internal enum, e.g. Idle / Charging / Enraged / Stunned)",
/// and <c>COMBAT_RULES.md</c> §1.2 states the same four names when it lists
/// <c>State</c> among the Boss's properties. There is no fifth member and no
/// <c>ChargingSkill</c>: the former <c>COMBAT_RULES.md</c> §1.2 spelling was a
/// documentation bug, corrected to <c>Charging</c> by TASK-020A, and
/// <c>BOSS_RULES.md</c> §1 is the authoritative source for the vocabulary.
///
/// <b>This enum is state representation only.</b> <c>BOSS_RULES.md</c> §5 item 1
/// defines State as what gates which Passive/Skill logic is currently active,
/// and §5 item 3 states that MVP Bosses "may use State minimally (e.g. a simple
/// Idle/Casting toggle); a full multi-phase State machine is a Boss Phases
/// feature explicitly deferred to Future Expansion (GDD §20), not required for
/// MVP". No transition, no trigger, no Enrage rule, and no Stun rule is defined
/// here or anywhere in this task: each is owned by the Boss mechanics that read
/// this value (<c>BOSS_RULES.md</c> §3–§5) and none of them is implemented yet.
///
/// <b>Server-authoritative.</b> <c>BOSS_RULES.md</c> §5 item 2 and §8 make State
/// entirely server-authoritative (<c>GAME_RULES.md</c> §15.5, §18; ADR-001): the
/// client never computes it, and it is exposed only through emitted events and
/// rendered state.
///
/// <b>No wire representation is defined here.</b> <c>SIGNALR_PROTOCOL.md</c>
/// declares no <c>bossState</c> payload member at this stage, so no
/// contract-name mapping and no <c>JsonPropertyName</c> contract is declared by
/// this type.
/// </summary>
public enum BossStateKind
{
    /// <summary>
    /// The Boss's documented initial State (<c>BOSS_RULES.md</c> §6.1,
    /// <c>GAME_STATE.md</c> §2.4) — the State every MVP Boss begins a battle in,
    /// before any Passive or Skill condition has changed it.
    /// </summary>
    Idle = 0,

    /// <summary>
    /// The Boss is charging its Skill (<c>BOSS_RULES.md</c> §4). Named
    /// <c>Charging</c> per <c>BOSS_RULES.md</c> §1 — not <c>ChargingSkill</c>.
    ///
    /// What sets this State, what it gates, and when it is left are owned by the
    /// Boss Skill mechanics (<c>BOSS_RULES.md</c> §4) and are <b>not yet
    /// implemented</b>, not <b>not required</b>.
    /// </summary>
    Charging = 1,

    /// <summary>
    /// The Boss is Enraged (<c>BOSS_RULES.md</c> §1, §5 item 1) — the State §5
    /// item 1 gives as its example of a Passive-triggered condition changing
    /// Skill behavior.
    ///
    /// The Enrage mechanic itself — its trigger, its threshold, and its effect —
    /// is owned by the Boss Passive system (<c>BOSS_RULES.md</c> §3) and is
    /// <b>not yet implemented</b>, not <b>not required</b>. No Enrage rule is
    /// defined by this type.
    /// </summary>
    Enraged = 2,

    /// <summary>
    /// The Boss is Stunned (<c>BOSS_RULES.md</c> §1, <c>COMBAT_RULES.md</c>
    /// §1.2).
    ///
    /// The Stun mechanic — what applies it, for how long, and what it prevents —
    /// belongs to the Boss/Status Effects systems and is <b>not yet
    /// implemented</b>, not <b>not required</b>. No Stun rule is defined by this
    /// type.
    /// </summary>
    Stunned = 3,
}
