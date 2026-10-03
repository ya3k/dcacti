namespace GameServer.Domain.Relics;

/// <summary>
/// How long an applied Relic effect modification persists — the
/// <c>lifetime</c> member <c>RELIC_RULES.md</c> §8.3 defines.
///
/// <code>
/// Immediate    applied once, at the moment it triggers; leaves nothing behind
/// Battle       a standing modification for the remainder of the battle
/// NextAttack   consumed by the next qualifying owner attack
/// </code>
///
/// <b>These three are the whole vocabulary</b> (<c>ADR-018</c> item 3: "lifetime
/// values are <c>Immediate</c>, <c>Battle</c>, and <c>NextAttack</c>"), and the
/// allowed combination is fixed <b>per effect type</b> by §8.3's table. A value
/// outside the allowed combination for its <c>effectType</c> "is not defined"
/// (§8.3 item 2) and is rejected rather than inferred.
///
/// <b><see cref="Immediate"/> is not a duration.</b> §8.3 item 3: "It denotes an
/// effect applied once, at the moment it triggers, which leaves no standing
/// modification behind — Mana Crystal's Power grant is applied to
/// <c>PetState.Power</c> and the effect itself then ends."
///
/// <b><see cref="NextAttack"/> reuses an established boundary rather than
/// introducing a second one.</b> §8.3 item 4: "denotes a modification consumed
/// by the next qualifying owner attack, which is the lifetime <c>scope:
/// "NextAttack"</c> already carries for the Card <c>Crit</c> effect
/// (<c>DATABASE.md</c> §1; <c>COMBAT_RULES.md</c> §2 item 7; <c>ADR-017</c>).
/// This contract reuses that established boundary and introduces no second
/// consumption rule."
///
/// <b>Lifetime is independent of trigger re-evaluation.</b> §8.4 item 3: a Relic
/// whose <c>Immediate</c> or <c>NextAttack</c> effect has ended "is not
/// disabled" — its Trigger is still re-evaluated on later events. This member
/// therefore stores a lifetime only: it adds no cooldown, no charge, and no
/// per-Relic reset state (§8.4 item 4).
///
/// <b>It stores a lifetime; it applies nothing and consumes nothing.</b> No
/// modification is created, tracked, expired, or consumed by this type
/// (<c>RELIC_RULES.md</c> §8.7).
///
/// <b>Member order carries no documented meaning</b> — the persisted contract
/// stores the member name, never this ordinal.
/// </summary>
public enum RelicEffectLifetime
{
    /// <summary>
    /// Applied once, at the moment it triggers, leaving no standing modification
    /// behind (<c>RELIC_RULES.md</c> §8.3 item 3). §8.4 item 5 fixes it as Mana
    /// Crystal's lifetime.
    /// </summary>
    Immediate = 0,

    /// <summary>
    /// A standing modification for the remainder of the battle (<c>RELIC_RULES.md</c>
    /// §8.3 item 4). §8.4 item 5 fixes it as Berserker Core's and Emergency
    /// Core's lifetime.
    ///
    /// It is not Turn-based and it introduces no Turn countdown: the lifetime
    /// states how long the applied modification persists, not a per-Turn tick.
    /// </summary>
    Battle = 1,

    /// <summary>
    /// Consumed by the next qualifying owner attack (<c>RELIC_RULES.md</c> §8.3
    /// item 4) — the boundary the Card <c>Crit</c> effect's <c>NextAttack</c>
    /// scope already carries (<c>ADR-017</c>). §8.4 item 5 fixes it as Assassin
    /// Eye's lifetime.
    ///
    /// It <b>reuses</b> that established boundary and defines no second
    /// consumption rule (which entity qualifies, and when the modifier is
    /// consumed, are <c>COMBAT_RULES.md</c> §3.3 items 7–10's and
    /// <c>GAME_STATE.md</c> §2.3.4's).
    /// </summary>
    NextAttack = 2,
}
