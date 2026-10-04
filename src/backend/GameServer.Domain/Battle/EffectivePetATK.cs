namespace GameServer.Domain.Battle;

/// <summary>
/// <c>EffectivePetATK</c> — the unified composition of every applicable Pet ATK
/// modifier into the value the Player → Boss Damage Pipeline's Step 1
/// <c>Attack</c> input consumes (<c>COMBAT_RULES.md</c> §5.6.1, §5.6.6).
///
/// <code>
/// PetState.ATK                                        permanent base stat
///   + Σ PetState.ATKModifiers[].ATKModifierPercentage  Relic modifiers, both
///                                                      lifetimes while active
///   + Σ signed BuffDebuff ATK contributions            Turn-based StatusEffects
///   = TotalATKModifierPercentage                       one signed percentage
///         ↓
/// truncate( PetState.ATK × (100 + Total) / 100 )       truncated once
///         ↓
/// EffectivePetATK                                      derived, never stored
/// </code>
///
/// <b>Every element of <c>ATKModifiers[]</c> that is active participates,
/// whatever lifetime it declares.</b> The collection carries <c>Battle</c> and
/// <c>NextAttack</c> elements (<c>GAME_STATE.md</c> §2.3.7 item 11), and a
/// <c>NextAttack</c> element is composed while it is awaiting its attack —
/// <c>COMBAT_RULES.md</c> §3.3 item 8 states that a Burn/DoT tick and the Boss's
/// own attack "do participate in Effective Crit composition while a modifier is
/// active" and that "eligibility and consumption are different questions", and
/// <c>GAME_STATE.md</c> §5.1.4 item 4 applies the same split here: the element is
/// consumed by the owner's next qualifying attack, not before it.
///
/// <b>This is the canonical composition model, and it is the only one.</b>
/// <c>COMBAT_RULES.md</c> §5.6.6 is the canonical owner and governs all three
/// cases — Relic-only, BuffDebuff-only (TASK-138 <b>D1</b>), and Relic +
/// BuffDebuff coexistence (TASK-137 <b>D1</b>). §5.4.1's historical
/// absolute-value composition, which reduced the stat once per active instance,
/// is superseded/narrowed for ATK composition by TASK-138 <b>D2</b>/<b>D4</b> and
/// "no longer defines a separate calculation path". There is therefore exactly
/// one composition in code, and no second stored or derived ATK representation:
/// <c>EffectivePetATK</c> is not a <c>PetState</c> member, not a
/// <c>BattleState</c> member, and not a Redis field (§5.6.6 item 5, §5.6.4).
///
/// <b>Composition is a signed sum, applied once.</b> §5.6.6 item 2 sums every
/// applicable modifier "with its documented sign", §5.6.6 item 3 makes the result
/// order-independent, and §5.6.6 item 4 truncates toward zero exactly once. The
/// arithmetic below is integer arithmetic — no <c>double</c> multiply-then-cast —
/// so the result does not depend on floating-point representation
/// (<c>TDD.md</c> §6, <c>AGENTS.md</c> §11).
///
/// <b>No cap is authored, and none is invented.</b> §5.6.1 item 5 / §5.6.6
/// item 2: <c>ATK</c> has no documented valid range (unlike <c>Power</c>'s 0–100
/// and <c>Crit</c>'s 0–100 percentage points), so the composed value is
/// deliberately unclamped. Authoring an ATK range is a separate balance decision
/// (<c>GAME_RULES.md</c> §20).
///
/// <b>The base stat is never written.</b> §5.6.4 and §5.6.6 item 8 keep
/// <c>PetState.ATK</c> permanent: this is a pure function returning an
/// <c>int</c>, so no "restore" step exists, no configured default is used as a
/// reset, and the derived value is discarded when the pipeline call ends.
///
/// <b>It is framework-independent and side-effect free.</b> No RNG, no clock, no
/// I/O, and no state mutation (<c>ARCHITECTURE.md</c> §2.1).
/// </summary>
public static class EffectivePetATK
{
    /// <summary>
    /// The one documented <c>TargetStat</c> value this composition consumes a
    /// <c>BuffDebuff</c> instance for (<c>COMBAT_RULES.md</c> §5.4.1:
    /// <c>TargetStat = "ATK"</c>).
    ///
    /// §5.4.5 states that a <c>BuffDebuff</c> naming any other stat "would require
    /// its own recorded decision before it could be implemented", so no second
    /// value is added here and no other stat's contribution is inferred.
    /// </summary>
    public const string AttackStat = "ATK";

    /// <summary>
    /// Whether an instance is one of the Pet ATK modifiers this composition
    /// consumes — a Turn-based <see cref="StatusEffectType.BuffDebuff"/> whose
    /// <see cref="StatusEffect.TargetStat"/> is <c>"ATK"</c>
    /// (<c>COMBAT_RULES.md</c> §5.4.1, §5.4.5, §5.6.6 item 2).
    ///
    /// <b>Identity is deliberately not consulted.</b> §5.4.5 makes <c>Root</c> the
    /// MVP <i>instance</i> of the rule rather than the rule's name, so dispatching
    /// on <see cref="StatusEffect.Id"/> would encode a second, undocumented
    /// definition of which effects modify ATK. The selection is by
    /// <c>Type</c> and <c>TargetStat</c>, and the Boss-side consumer
    /// (<see cref="StatusEffectLifecycle.EffectiveBossAttack"/>) shares this one
    /// predicate — §5.5.3 applies §5.4.5's discipline to the Boss side.
    ///
    /// A trigger-based instance carries no <c>TargetStat</c> at all
    /// (<c>GAME_STATE.md</c> §2.3.1 item 7), so it can never be selected.
    /// </summary>
    /// <param name="effect">The instance to test.</param>
    public static bool IsAtkStatModifier(StatusEffect effect) =>
        effect.Type == StatusEffectType.BuffDebuff
        && string.Equals(effect.TargetStat, AttackStat, StringComparison.Ordinal);

    /// <summary>
    /// The signed percentage-point contribution one applicable
    /// <c>BuffDebuff</c> ATK instance makes to
    /// <c>TotalATKModifierPercentage</c> (<c>COMBAT_RULES.md</c> §5.6.6 item 2,
    /// TASK-138 <b>D3</b>).
    ///
    /// <code>
    /// ATK reduction (Root -30%)  contributes  -|Magnitude|  = -30
    /// ATK increase (if authored) contributes  +|Magnitude|
    /// </code>
    ///
    /// <b>The stored magnitude does not encode the sign.</b> §5.6.6 item 2:
    /// "The stored <c>StatusEffect.Magnitude</c> remains the existing applied
    /// magnitude (e.g. <c>30</c> for Root) and does not itself encode the sign"
    /// (<c>GAME_STATE.md</c> §2.3.1 item 2). D3 maps a <b>reduction</b> to
    /// <c>-|Magnitude|</c> and an <b>increase</b> to <c>+|Magnitude|</c>.
    ///
    /// <b>Only the reduction is determinable, and only it is implemented.</b> D3's
    /// increase case is conditional — "if introduced by an authoritative gameplay
    /// rule" — and no such rule exists: <c>COMBAT_RULES.md</c> §5.1's closed MVP
    /// type list contains no ATK <i>buff</i>, and the one authored ATK
    /// <c>BuffDebuff</c> (<c>BOSS_RULES.md</c> §6.3.1 item 3's Root) is a
    /// reduction. The instance carries no buff/debuff discriminator and no
    /// document defines one, so branching on an inferred direction would be the
    /// invented rule <c>AGENTS.md</c> §7 forbids. The reduction mapping is
    /// therefore the whole of this contribution, and a future authored increase
    /// is a new recorded decision rather than something inferred here.
    /// </summary>
    /// <param name="effect">An applicable instance (<see cref="IsAtkStatModifier"/>).</param>
    /// <returns>The signed percentage-point contribution.</returns>
    public static int BuffDebuffAtkContribution(StatusEffect effect)
    {
        // The document's own range for this contribution is the 0–100 percentage
        // §5.4.2 states; a magnitude outside it would make the combined numerator
        // negative from the wrong side, so it is reported rather than silently
        // reinterpreted (AGENTS.md §7 is a stop condition, not a value to guess).
        var magnitude = (int)Math.Abs(effect.Magnitude);

        if (magnitude is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(effect),
                magnitude,
                "A TargetStat = \"ATK\" BuffDebuff magnitude must be a percentage in 0-100 "
                + $"(COMBAT_RULES.md §5.4.2); instance '{effect.Id}' carries {magnitude}.");
        }

        return -magnitude;
    }

    /// <summary>
    /// Composes <c>EffectivePetATK</c> from the permanent base ATK and every
    /// applicable Pet ATK modifier (<c>COMBAT_RULES.md</c> §5.6.1, §5.6.6).
    /// </summary>
    /// <param name="baseAtk">
    /// The permanent base stat — <c>PetState.ATK</c> (<c>COMBAT_RULES.md</c> §1.1,
    /// <c>GAME_STATE.md</c> §2.3). It is read and never written: §5.6.4 forbids
    /// overwriting it and forbids resetting it to a configuration default.
    /// </param>
    /// <param name="atkModifiers">
    /// The Pet's applied Relic ATK modifiers (<c>GAME_STATE.md</c> §2.3.7), always a
    /// collection and never <c>null</c> (§2.3.7 item 6). Each entry is already a
    /// <b>signed</b> percentage-point contribution (§2.3.7 item 5), so each is summed
    /// with its own sign (<c>COMBAT_RULES.md</c> §5.6.6 item 2: "Berserker Core
    /// <c>+5%</c> contributes <c>+5</c>"). Both of the collection's lifetimes
    /// participate while their element is active — §2.3.7 item 11 carries
    /// <c>Battle</c> and <c>NextAttack</c> elements in this one collection, and a
    /// <c>NextAttack</c> element is consumed by the qualifying attack this composition
    /// feeds, not before it (§5.1.4 item 4).
    /// </param>
    /// <param name="statusEffects">
    /// The Pet's active Status Effects (<c>GAME_STATE.md</c> §2.3.1), always a
    /// collection and never <c>null</c> (§2.3.2 item 1). Only the instances
    /// <see cref="IsAtkStatModifier"/> selects contribute, and each contributes
    /// <see cref="BuffDebuffAtkContribution"/>.
    /// </param>
    /// <returns>
    /// The Step-1 <c>Attack</c> input. It equals <paramref name="baseAtk"/> when no
    /// applicable modifier is present, and is otherwise the once-truncated result of
    /// the combined signed percentage (§5.6.6 items 4–5). <c>COMBAT_RULES.md</c>
    /// §5.6.6 item 6's worked examples fall out of it exactly: <c>50</c> at
    /// <c>+5</c> → <c>52</c>, at <c>-30</c> → <c>35</c>, and at
    /// <c>(+5) + (-30)</c> → <c>37</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="atkModifiers"/> or <paramref name="statusEffects"/> is
    /// <c>null</c> — neither collection has an absent representation
    /// (<c>GAME_STATE.md</c> §2.3.7 item 6, §2.3.2 item 1).
    /// </exception>
    public static int Compose(
        int baseAtk,
        IReadOnlyList<ATKModifier> atkModifiers,
        IReadOnlyList<StatusEffect> statusEffects)
    {
        ArgumentNullException.ThrowIfNull(atkModifiers);
        ArgumentNullException.ThrowIfNull(statusEffects);

        // COMBAT_RULES.md §5.6.6 item 2: "the signed sum of every applicable Pet
        // ATK modifier". The two carriers are summed into ONE percentage before
        // anything is applied to the base stat — that is what makes the result
        // order-independent (§5.6.6 item 3) and gives the single truncation point
        // its meaning (§5.6.6 item 4). Widened to long so a large combination
        // cannot overflow before it is applied.
        var totalPercentage = 0L;

        // Every element participates while it is active, whatever lifetime it
        // declares: a `NextAttack` element is awaiting its attack, not excluded from
        // the composition (GAME_STATE.md §2.3.7 item 11, §5.1.4 item 4), and the
        // qualifying attack that consumes it is the same attack this composition
        // feeds (COMBAT_RULES.md §3.3 items 7–8).
        for (var index = 0; index < atkModifiers.Count; index++)
        {
            totalPercentage += atkModifiers[index].ATKModifierPercentage;
        }

        for (var index = 0; index < statusEffects.Count; index++)
        {
            var effect = statusEffects[index];

            if (IsAtkStatModifier(effect))
            {
                totalPercentage += BuffDebuffAtkContribution(effect);
            }
        }

        // §5.6.1 item 4 / §5.6.6 item 4: apply the combined signed percentage to the
        // base stat and truncate toward zero EXACTLY ONCE. Integer division on a
        // widened operand truncates toward zero for both signs, so a net-negative
        // total behaves as §5.6.1 item 4 states and no intermediate per-modifier
        // truncation can occur. §5.6.1 item 5 adds that no clamp is applied.
        return (int)(baseAtk * (100 + totalPercentage) / 100);
    }
}
