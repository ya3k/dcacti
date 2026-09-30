namespace GameServer.Domain.Battle;

/// <summary>
/// Which duration model a Status Effect instance uses
/// (<c>GAME_STATE.md</c> §2.3.1 <c>Type</c>, item 3).
///
/// <code>
/// DoT         damage-over-time (Burn)                     → Turn countdown
/// BuffDebuff  temporary stat modification (Root)          → Turn countdown
/// Shield      absorption pool                             → trigger-based expiry
/// State       entity state such as Stun                   → Turn countdown
/// </code>
///
/// <b>This is the documented <c>Type</c> vocabulary of §2.3.1</b> — the four
/// members §2.3.1's schema lists and nothing more. The MVP set is
/// <c>COMBAT_RULES.md</c> §5.1's (Burn, Shield, Buff/Debuff) plus Stun, which
/// §2.3.1 item 3 and §2.4.5 define as existing for future content. Introducing
/// another type is a gameplay decision owned by <c>COMBAT_RULES.md</c>, not by
/// this state (§2.3.3 item 1).
///
/// <b>It selects the duration model, and the model is exclusive.</b>
/// <c>GAME_STATE.md</c> §2.3.1 item 3 fixes exactly one model per instance:
/// either the Turn countdown (<see cref="StatusEffect.RemainingTurns"/>) or a
/// trigger-based expiry (<see cref="StatusEffect.ExpiryCondition"/>) — never
/// both and never neither. That rule is expressed by the instance's construction
/// rather than by this enum, because the model is a property of the individual
/// effect: §2.3.1 item 3 records that <c>Shield</c> uses the trigger model while
/// the other three use the countdown.
/// </summary>
public enum StatusEffectType
{
    /// <summary>
    /// A damage-over-time effect (<c>COMBAT_RULES.md</c> §5.1: Burn), which ticks
    /// once per resolved Turn at <c>GAME_RULES.md</c> §17 step 19a.
    ///
    /// It uses the Turn countdown (<c>GAME_STATE.md</c> §2.3.1 item 3), and the
    /// tick itself runs through the Damage Pipeline with <c>Combo = 1</c>
    /// (<c>COMBAT_RULES.md</c> §5.2 item 3). That tick is a damage concern and is
    /// not implemented by this state model: what is represented here is the
    /// instance and its duration.
    /// </summary>
    DoT = 0,

    /// <summary>
    /// A temporary stat modification — "ATK/DEF/Crit/etc., with duration measured
    /// in Turns unless stated otherwise" (<c>COMBAT_RULES.md</c> §5.1).
    ///
    /// It uses the Turn countdown and is the one type that carries
    /// <see cref="StatusEffect.TargetStat"/> (<c>GAME_STATE.md</c> §2.3.1 item 7:
    /// present for <c>BuffDebuff</c>, absent otherwise). Root is the documented
    /// MVP instance of this type (<c>BOSS_RULES.md</c> §6.3.1 item 3).
    /// </summary>
    BuffDebuff = 1,

    /// <summary>
    /// An absorption pool (<c>COMBAT_RULES.md</c> §5.1, §4).
    ///
    /// It is the documented trigger-based model: <c>GAME_STATE.md</c> §2.3.1
    /// item 3 gives it <c>ExpiryCondition</c> ("until Shield is depleted") instead
    /// of <c>RemainingTurns</c>, and §5.1.1 item 7 requires the step 19a pass to
    /// leave it alone. The depletion behavior itself is owned by
    /// <c>COMBAT_RULES.md</c> §4 and is not implemented here.
    /// </summary>
    Shield = 2,

    /// <summary>
    /// An entity state such as Stun (<c>GAME_STATE.md</c> §2.4.5).
    ///
    /// It uses the Turn countdown: §2.3.1 item 3 records that <c>State</c>-typed
    /// instances do so because §2.4.5 defines Stun's duration as "measured in
    /// Turns". §5.1.1 item 8 requires the Boss's <c>State</c> to revert to
    /// <c>Idle</c> when a Stun instance expires, so <c>State</c> never disagrees
    /// with this instance's presence.
    /// </summary>
    State = 3,
}

/// <summary>
/// Which side applied a Status Effect instance (<c>GAME_STATE.md</c> §2.3.1
/// <c>Source</c>, item 7).
///
/// <code>
/// Player  applied by the player's side (e.g. a Card's Shield)
/// Boss    applied by the Boss (e.g. Flame Burst's Burn, Root)
/// </code>
///
/// <b>This is the documented source convention</b> — <c>GAME_STATE.md</c> §2.3.1
/// gives the values as <c>"player" | "boss"</c> and cites
/// <c>GAME_EVENTS.md</c> §2's source convention, which the Battle Events already
/// use (<c>PassiveEventSource</c>). It is required on every instance:
/// <c>COMBAT_RULES.md</c> §5.2 item 1 requires every Status Effect to have a
/// source, and §2.3.1 item 7 states an instance "is never created without" it.
///
/// <b>It is a label, not a lookup.</b> It records which side applied the effect
/// so a later reader can attribute it; it grants no ownership of the effect and
/// no rule reads it to decide behavior.
/// </summary>
public enum StatusEffectSource
{
    /// <summary>The player's side applied the effect (<c>GAME_STATE.md</c> §2.3.1).</summary>
    Player = 0,

    /// <summary>The Boss applied the effect (<c>GAME_STATE.md</c> §2.3.1).</summary>
    Boss = 1,
}
