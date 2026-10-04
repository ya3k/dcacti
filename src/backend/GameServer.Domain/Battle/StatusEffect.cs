namespace GameServer.Domain.Battle;

/// <summary>
/// One active Status Effect instance (<c>GAME_STATE.md</c> §2.3.1).
///
/// <code>
/// StatusEffect
/// ├── Id               identity ("Burn" | "Root" | "Shield" | "Stun")   item 1
/// ├── Type             "DoT" | "BuffDebuff" | "Shield" | "State"        item 3
/// ├── Source           "player" | "boss"                                item 7
/// ├── Magnitude        the applied value; NOT interpreted here          item 2
/// ├── TargetStat?      the modified stat, iff Type = "BuffDebuff"       item 7
/// └── RemainingTurns   the per-instance countdown, xor ExpiryCondition  items 3, 4
///     ExpiryCondition  the trigger-based expiry, xor RemainingTurns     items 3, 5
/// </code>
///
/// <b>This is the documented element shape, not a new decision.</b>
/// <c>GAME_STATE.md</c> §2.3.1 fixes every member above, and §2.3.1's preamble
/// states the shape serves <c>PetState.StatusEffects[]</c> and
/// <c>BossState.StatusEffects[]</c> identically: "identical element shape and
/// identical lifecycle". There is therefore <b>no boss-specific variant</b> of
/// this type (§2.4.1, §0 item 5).
///
/// <b>It is an instance whose rules live elsewhere.</b> §2.3.1 item 1 makes
/// <see cref="Id"/> an identity, not a definition: Burn's element and tick
/// behavior, Root's ATK percentage, and Shield's absorption behavior are owned by
/// <c>COMBAT_RULES.md</c> §5 and <c>BOSS_RULES.md</c> §6.3.1 and are deliberately
/// <b>not</b> copied into the instance (§0 item 5). <see cref="Magnitude"/>
/// carries the applied value only, and per §2.3.1 item 2 what that number
/// <i>means</i> — flat damage, a percentage reduction, an absorption pool — is
/// owned by the effect's rule document. Nothing in this type interprets it.
///
/// <b>The two duration models are structurally exclusive.</b> §2.3.1 item 3
/// requires an instance to use <b>either</b> the Turn countdown <b>or</b> a
/// trigger-based expiry — "never both and never neither". That is
/// <c>COMBAT_RULES.md</c> §5.2 item 1's "a duration (in Turns) or a trigger-based
/// expiry" expressed as state, and it is enforced here by construction: the two
/// factories below are the only ways to build an instance, one per model, so an
/// instance carrying both members or neither cannot be represented at all
/// (§0 item 5 — no second representation of the same fact).
///
/// <b>The Turn countdown's meaning is owned by §5.1.1.</b> §2.3.1 item 4 makes
/// <see cref="RemainingTurns"/> the authoritative per-instance counter and states
/// that its meaning and mutation are owned by §5.1.1, with this section defining
/// only its type and presence. What one Turn of duration means in play is owned
/// by <c>COMBAT_RULES.md</c> §5.3 (DR1–DR6). This type defines neither and must
/// not be read as an independent source for either.
///
/// <b>Zero is a real value that is never observable here.</b> §2.3.1 item 8: an
/// instance at <c>RemainingTurns = 0</c> is removed by the step 19a resolution
/// that produced the <c>0</c> (§5.1.1 item 5), so absence of the element means
/// "not active" — never "active with 0 Turns". The countdown lifecycle is
/// <see cref="StatusEffectLifecycle"/>'s, which upholds that.
///
/// <b>Absence, not null.</b> §2.3.1 item 7: an inapplicable member is
/// <b>absent</b> — never <c>null</c> and never a sentinel string — following the
/// absent-member convention of §2.1.7 item 3. That is why the optional members
/// are nullable here and are written as absent rather than as a placeholder
/// value, and why §2.3.2 item 5 makes a round trip that materializes one as
/// <c>null</c> a defect.
///
/// This type is deliberately minimal and framework-independent
/// (<c>ARCHITECTURE.md</c> §2.1): it references no ASP.NET Core, SignalR, EF
/// Core, Redis, HTTP, Phaser, or Discord concern. It holds no gameplay state
/// beyond the instance itself and exposes no behavior — the lifecycle that
/// applies, refreshes, consumes, and expires instances is
/// <see cref="StatusEffectLifecycle"/>'s.
/// </summary>
/// <param name="Id">
/// The Status Effect identity (<c>GAME_STATE.md</c> §2.3.1 items 1 and 6), e.g.
/// <c>"Burn"</c>, <c>"Root"</c>, <c>"Shield"</c>, <c>"Stun"</c>.
///
/// It is <b>an identity, not a definition</b> (item 1): the effect's rules stay
/// with <c>COMBAT_RULES.md</c> §5 and <c>BOSS_RULES.md</c> §6.3.1 and are not
/// copied here. It is also the collection's uniqueness key — §2.3.1 item 6 makes
/// it impossible for an entity to hold two instances with the same
/// <see cref="Id"/> — so it is required, and it is never empty or whitespace
/// (an instance must name which effect it is).
/// </param>
/// <param name="Type">
/// Which duration model and category the instance belongs to
/// (<c>GAME_STATE.md</c> §2.3.1 item 3).
///
/// It is required: §2.3.1's schema marks it so, precisely because it selects the
/// duration model, and an instance with no model would be neither decrementable
/// nor trigger-expirable.
/// </param>
/// <param name="Source">
/// Which side applied the instance (<c>GAME_STATE.md</c> §2.3.1 item 7).
///
/// It is required: <c>COMBAT_RULES.md</c> §5.2 item 1 requires every Status
/// Effect to have a source, and §2.3.1 item 7 states an instance is never
/// created without one.
/// </param>
/// <param name="Magnitude">
/// The effect's applied magnitude (<c>GAME_STATE.md</c> §2.3.1 item 2).
///
/// It is required and always present. §2.3.1 item 2 states it is <b>typed but
/// not interpreted</b> here: what the number means is owned by the effect's rule
/// document, and this type fixes only that the value is stored on the instance so
/// a refresh can re-apply it (<c>COMBAT_RULES.md</c> §5.2 item 2). No unit, sign,
/// or range is imposed, because none is documented at this layer — Root's
/// magnitude is a negative percentage and Burn's is flat damage per tick.
/// </param>
/// <param name="TargetStat">
/// The modified stat for a <see cref="StatusEffectType.BuffDebuff"/> instance,
/// e.g. <c>"ATK"</c> (<c>GAME_STATE.md</c> §2.3.1 item 7).
///
/// It is <b>present iff <see cref="Type"/> is <see cref="StatusEffectType.BuffDebuff"/></b>
/// and absent otherwise — absent, never <c>null</c> and never a sentinel (item 7,
/// §2.1.7 item 3). The <see cref="BuffDebuff"/> factory enforces that pairing, so
/// a <c>null</c> here means the member does not apply and never "unset".
/// </param>
/// <param name="RemainingTurns">
/// The per-instance duration counter for a Turn-based instance
/// (<c>GAME_STATE.md</c> §2.3.1 items 3 and 4; <c>COMBAT_RULES.md</c> §5.3 DR1).
///
/// It is present for <see cref="StatusEffectType.DoT"/>,
/// <see cref="StatusEffectType.BuffDebuff"/>, and
/// <see cref="StatusEffectType.State"/>; it is absent for
/// <see cref="StatusEffectType.Shield"/>, which uses
/// <paramref name="ExpiryCondition"/> instead.
///
/// <b>It is an <c>int</c> and never nullable in meaning</b> (item 4: "never
/// nullable, never fractional"), initialized to the applied or refreshed duration
/// value. §2.3.1 item 4 states its meaning and mutation are owned by §5.1.1 —
/// this type defines only its type and presence, and the countdown itself is
/// <see cref="StatusEffectLifecycle"/>'s.
/// </param>
/// <param name="ExpiryCondition">
/// The trigger-based expiry label for an instance that does not use the Turn
/// countdown, e.g. <c>"ShieldDepleted"</c> (<c>GAME_STATE.md</c> §2.3.1 items 3
/// and 5).
///
/// It is present iff <paramref name="RemainingTurns"/> is absent — the two are
/// mutually exclusive by item 3, so exactly one is present. §2.3.1 item 5 makes
/// it <b>a condition label, not a rule</b>: it names which documented trigger
/// ends the instance, and the condition's behavior is owned by
/// <c>COMBAT_RULES.md</c> §4 and §5.1 and is not restated here. §5.1.1 item 7
/// requires the step 19a countdown to leave such an instance alone.
/// </param>
public readonly record struct StatusEffect(
    string Id,
    StatusEffectType Type,
    StatusEffectSource Source,
    double Magnitude,
    string? TargetStat,
    int? RemainingTurns,
    string? ExpiryCondition)
{
    /// <summary>
    /// The <c>ExpiryCondition</c> label a Shield instance carries
    /// (<c>GAME_STATE.md</c> §2.3.1 item 3: <c>Shield</c> is "until Shield is
    /// depleted"; <c>COMBAT_RULES.md</c> §5.1, §4).
    ///
    /// §2.3.1 item 5 makes this a <b>label naming the trigger</b>, not the
    /// trigger's implementation: the depletion behavior is owned by
    /// <c>COMBAT_RULES.md</c> §4 and is not implemented here. It exists so the
    /// one documented trigger-based effect has its documented condition, rather
    /// than a spelling invented at a call site.
    /// </summary>
    public const string ShieldDepletedCondition = "ShieldDepleted";

    /// <summary>
    /// Whether this instance uses the Turn countdown rather than a trigger-based
    /// expiry (<c>GAME_STATE.md</c> §2.3.1 item 3).
    ///
    /// This is the reading <c>GAME_STATE.md</c> §5.1.1 item 2 and item 7 depend
    /// on: only Turn-countdown instances are decremented at step 19a, and a
    /// trigger-based instance "is not touched by the step 19a countdown".
    /// </summary>
    public bool UsesTurnCountdown => RemainingTurns is not null;

    /// <summary>
    /// Builds a Turn-based instance — one that carries
    /// <see cref="RemainingTurns"/> and no <see cref="ExpiryCondition"/>
    /// (<c>GAME_STATE.md</c> §2.3.1 item 3).
    ///
    /// This is the model <see cref="StatusEffectType.DoT"/>,
    /// <see cref="StatusEffectType.BuffDebuff"/>, and
    /// <see cref="StatusEffectType.State"/> use, and it is the only way to
    /// construct one — so the mutual exclusivity item 3 requires cannot be
    /// violated by a caller, and neither can the
    /// <see cref="TargetStat"/>-iff-<c>BuffDebuff</c> pairing of item 7.
    ///
    /// <b>It is not the apply operation.</b> §5.1.1 item 1 makes application
    /// `RemainingTurns = duration`, and that mutation — including the
    /// refresh-in-place rule that prevents a second element for the same
    /// <see cref="Id"/> — is <see cref="StatusEffectLifecycle.Apply"/>'s. This
    /// factory builds <i>one</i> instance; it does not decide whether one already
    /// exists.
    /// </summary>
    /// <param name="id">
    /// The effect identity (§2.3.1 item 1). It must name an effect: a null, empty,
    /// or whitespace <paramref name="id"/> is rejected rather than admitted,
    /// because an instance that does not say which effect it is cannot be
    /// de-duplicated under item 6's one-instance-per-identity rule.
    /// </param>
    /// <param name="type">
    /// The effect category (§2.3.1 item 3). It must not be
    /// <see cref="StatusEffectType.Shield"/>, which §2.3.1 item 3 makes
    /// trigger-based — that model is <see cref="TriggerBased"/>'s.
    /// </param>
    /// <param name="source">Which side applied it (§2.3.1 item 7).</param>
    /// <param name="magnitude">The applied magnitude (§2.3.1 item 2), uninterpreted.</param>
    /// <param name="duration">
    /// The duration in Turns — the value §5.1.1 item 1 sets
    /// <c>RemainingTurns</c> to on apply and on refresh
    /// (<c>COMBAT_RULES.md</c> §5.3 DR1, DR3).
    ///
    /// <b>It must be a positive Turn count.</b> A duration of <c>0</c> or less
    /// would produce an instance that is already expired at construction, which
    /// §2.3.1 item 8 states is never observable: an instance at <c>0</c> "is
    /// removed at the step 19a resolution that produced the 0 … so it is never
    /// observable in a committed state". Admitting one here would create exactly
    /// the committed-zero state that item forbids.
    /// </param>
    /// <param name="targetStat">
    /// The modified stat, required for <see cref="StatusEffectType.BuffDebuff"/>
    /// and absent for every other type (§2.3.1 item 7).
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> is null, empty, or whitespace;
    /// <paramref name="targetStat"/> is absent for a <c>BuffDebuff</c> or present
    /// for a type that does not modify a stat;
    /// <paramref name="duration"/> is not a positive Turn count; or
    /// <paramref name="type"/> is <see cref="StatusEffectType.Shield"/>, whose
    /// documented model is trigger-based (§2.3.1 item 3).
    /// </exception>
    /// <summary>
    /// Builds a Turn-based instance that does not modify a stat — one that carries
    /// <see cref="RemainingTurns"/> and no <see cref="TargetStat"/> (absent).
    /// Used for <see cref="StatusEffectType.DoT"/>, <see cref="StatusEffectType.State"/>,
    /// and the documented non-stat <see cref="StatusEffectType.BuffDebuff"/> exception
    /// ("boss-thuy-ma-heal", <c>GAME_STATE.md</c> §2.3.1 item 7, <c>BOSS_RULES.md</c> §6.2.2).
    /// </summary>
    /// <param name="id">The effect identity.</param>
    /// <param name="type">The effect category.</param>
    /// <param name="source">Which side applied it.</param>
    /// <param name="magnitude">The applied magnitude, uninterpreted.</param>
    /// <param name="duration">The duration in Turns (must be at least 1).</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> is null, empty, or whitespace;
    /// <paramref name="type"/> is <see cref="StatusEffectType.Shield"/>;
    /// <paramref name="duration"/> is less than 1; or
    /// <paramref name="type"/> is <see cref="StatusEffectType.BuffDebuff"/> and
    /// <paramref name="id"/> is not the documented non-stat exception.
    /// </exception>
    public static StatusEffect TurnBased(
        string id,
        StatusEffectType type,
        StatusEffectSource source,
        double magnitude,
        int duration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (type == StatusEffectType.Shield)
        {
            throw new ArgumentException(
                "A Shield instance uses the trigger-based expiry model (GAME_STATE.md "
                + "§2.3.1 item 3); build it with StatusEffect.TriggerBased.",
                nameof(type));
        }

        // §2.3.1 item 7 (GAME_STATE.md v2.18 / TASK-156 / TASK-157):
        // a BuffDebuff carries TargetStat iff its Magnitude is consumed as a stat modifier;
        // a BuffDebuff consumed by a non-stat rule selects by Id and omits TargetStat.
        // The documented non-stat BuffDebuff exception is "boss-thuy-ma-heal" (BOSS_RULES.md §6.2.2).
        // Any other BuffDebuff modifies a stat and MUST specify a TargetStat.
        if (type == StatusEffectType.BuffDebuff &&
            !string.Equals(id, "boss-thuy-ma-heal", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"BuffDebuff '{id}' requires a TargetStat (GAME_STATE.md §2.3.1 item 7).",
                nameof(id));
        }

        if (duration < 1)
        {
            throw new ArgumentException(
                "A Turn-based Status Effect duration must be at least 1 Turn "
                + "(GAME_STATE.md §2.3.1 item 8: RemainingTurns = 0 is never an "
                + "observable committed value).",
                nameof(duration));
        }

        return new StatusEffect(
            id,
            type,
            source,
            magnitude,
            TargetStat: null,
            RemainingTurns: duration,
            ExpiryCondition: null);
    }

    /// <summary>
    /// Builds a Turn-based <see cref="StatusEffectType.BuffDebuff"/> instance that modifies
    /// a stat — carrying <see cref="RemainingTurns"/> and a required non-null <see cref="TargetStat"/>
    /// (<c>GAME_STATE.md</c> §2.3.1 item 7).
    /// </summary>
    /// <param name="id">The effect identity.</param>
    /// <param name="type">The effect category (must be <see cref="StatusEffectType.BuffDebuff"/>).</param>
    /// <param name="source">Which side applied it.</param>
    /// <param name="magnitude">The applied magnitude, uninterpreted.</param>
    /// <param name="duration">The duration in Turns (must be at least 1).</param>
    /// <param name="targetStat">The modified stat (e.g. "ATK").</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> or <paramref name="targetStat"/> is null, empty, or whitespace;
    /// <paramref name="type"/> is not <see cref="StatusEffectType.BuffDebuff"/>;
    /// <paramref name="duration"/> is less than 1; or
    /// <paramref name="id"/> is the documented non-stat exception "boss-thuy-ma-heal"
    /// which must omit TargetStat.
    /// </exception>
    public static StatusEffect TurnBased(
        string id,
        StatusEffectType type,
        StatusEffectSource source,
        double magnitude,
        int duration,
        string targetStat)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetStat);

        if (type == StatusEffectType.Shield)
        {
            throw new ArgumentException(
                "A Shield instance uses the trigger-based expiry model (GAME_STATE.md "
                + "§2.3.1 item 3); build it with StatusEffect.TriggerBased.",
                nameof(type));
        }

        if (type != StatusEffectType.BuffDebuff)
        {
            throw new ArgumentException(
                $"TargetStat applies only to a BuffDebuff instance (GAME_STATE.md §2.3.1 "
                + $"item 7); type '{type}' does not modify a stat.",
                nameof(targetStat));
        }

        // §2.3.1 item 7 (GAME_STATE.md v2.18 / TASK-156 / TASK-157):
        // a BuffDebuff consumed by a non-stat rule selects by Id and omits TargetStat.
        if (string.Equals(id, "boss-thuy-ma-heal", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Non-stat BuffDebuff '{id}' must omit TargetStat (GAME_STATE.md §2.3.1 item 7).",
                nameof(targetStat));
        }

        if (duration < 1)
        {
            throw new ArgumentException(
                "A Turn-based Status Effect duration must be at least 1 Turn "
                + "(GAME_STATE.md §2.3.1 item 8: RemainingTurns = 0 is never an "
                + "observable committed value).",
                nameof(duration));
        }

        return new StatusEffect(
            id,
            type,
            source,
            magnitude,
            TargetStat: targetStat,
            RemainingTurns: duration,
            ExpiryCondition: null);
    }

    /// <summary>
    /// Builds a trigger-based instance — one that carries
    /// <see cref="ExpiryCondition"/> and no <see cref="RemainingTurns"/>
    /// (<c>GAME_STATE.md</c> §2.3.1 item 3).
    ///
    /// This is the model §2.3.1 item 3 gives <see cref="StatusEffectType.Shield"/>
    /// ("until Shield is depleted"), and §5.1.1 item 7 requires the step 19a
    /// countdown to leave such an instance untouched — it "is removed by its own
    /// documented trigger", and the pass "must not invent a duration for it".
    ///
    /// The trigger's <i>behavior</i> is owned by <c>COMBAT_RULES.md</c> §4 and
    /// §5.1 (§2.3.1 item 5: the condition is "a condition label, not a rule") and
    /// is not implemented here: this factory records which trigger ends the
    /// instance, and nothing evaluates it.
    /// </summary>
    /// <param name="id">
    /// The effect identity (§2.3.1 item 1); required, on the same basis as
    /// <see cref="TurnBased"/>.
    /// </param>
    /// <param name="type">
    /// The effect category (§2.3.1 item 3). It must be
    /// <see cref="StatusEffectType.Shield"/>: item 3 assigns the trigger-based
    /// model to Shield alone and gives the other three the Turn countdown.
    /// </param>
    /// <param name="source">Which side applied it (§2.3.1 item 7).</param>
    /// <param name="magnitude">
    /// The applied magnitude (§2.3.1 item 2) — for a Shield, the absorption pool
    /// the owning rule defines. Uninterpreted here, on the same basis as
    /// <see cref="TurnBased"/>.
    /// </param>
    /// <param name="expiryCondition">
    /// The name of the trigger that ends the instance (§2.3.1 item 5), e.g.
    /// <see cref="ShieldDepletedCondition"/>. It must name a trigger: an absent
    /// label would leave the instance with no expiry at all, which item 3 forbids
    /// ("never both and never neither").
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> or <paramref name="expiryCondition"/> is null, empty,
    /// or whitespace; or <paramref name="type"/> is not
    /// <see cref="StatusEffectType.Shield"/>.
    /// </exception>
    public static StatusEffect TriggerBased(
        string id,
        StatusEffectType type,
        StatusEffectSource source,
        double magnitude,
        string expiryCondition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(expiryCondition);

        // §2.3.1 item 3: the trigger-based model belongs to Shield; the other
        // three types are Turn-based by the same item.
        if (type != StatusEffectType.Shield)
        {
            throw new ArgumentException(
                $"Type '{type}' uses the Turn countdown (GAME_STATE.md §2.3.1 item 3); "
                + "the trigger-based model is Shield's.",
                nameof(type));
        }

        return new StatusEffect(
            id,
            type,
            source,
            magnitude,
            TargetStat: null,
            RemainingTurns: null,
            ExpiryCondition: expiryCondition);
    }
}
