using GameServer.Domain.Battle;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// The <c>BuffDebuff</c> ATK consumption rule — <c>COMBAT_RULES.md</c> §5.4
/// (TASK-119's resolved contract), whose MVP instance is Root
/// (<c>BOSS_RULES.md</c> §6.3.1 item 3).
///
/// <code>
/// Rule (COMBAT_RULES.md §5.4.1-§5.4.5)
///  ↓
/// Scenario (Given ATK and active instances, When the Pet's own attack resolves,
///           Then the Step-1 Attack input is the documented value)
///  ↓
/// Test
/// </code>
///
/// <b>Every expected value is read from §5.4, never from the implementation.</b>
/// §5.4.2's worked values (50 → 35, 51 → 35, 99 → 69, 100 → 70, 101 → 70) and
/// §5.4.1's worked example (100 with a pool of 40 at −30% → 70, Step 1 = 110, not
/// 98) are asserted verbatim. Where §5.4 fixes a negative — that the pool is not
/// reduced, that the stat is not overwritten — the negative is asserted
/// explicitly, so the test fails if the implementation drifts to the rejected
/// reading rather than merely to a different number.
///
/// <b>Scope.</b> This file covers the Domain-level consumer
/// (<see cref="StatusEffectLifecycle.EffectiveAttack"/>): selection, the
/// percentage, rounding, non-<c>"ATK"</c> instances, the expired case, and base
/// preservation. The Turn ordering §5.4.3 fixes (step 15 → step 18b → step 19a)
/// is asserted end-to-end in the Application suite, where a real Turn can be
/// resolved.
/// </summary>
public class EffectiveAttackTests
{
    /// <summary>
    /// Root as §6.3.1 item 3 fixes it, built through the documented factory
    /// (<c>GAME_STATE.md</c> §2.3.1 item 7 enforces the <c>TargetStat</c>-iff-
    /// <c>BuffDebuff</c> pairing at construction).
    /// </summary>
    private static StatusEffect Root(int duration) =>
        StatusEffect.TurnBased(
            "Root", StatusEffectType.BuffDebuff, StatusEffectSource.Boss, 30, duration, "ATK");

    /// <summary>
    /// A <c>BuffDebuff</c> naming a stat other than <c>"ATK"</c> — the case
    /// §5.4.5 states modifies no ATK.
    /// </summary>
    private static StatusEffect Debuff(string id, string targetStat, double magnitude, int duration) =>
        StatusEffect.TurnBased(
            id, StatusEffectType.BuffDebuff, StatusEffectSource.Boss, magnitude, duration, targetStat);

    // =======================================================================
    // No modifier active — the unreduced value
    // =======================================================================

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(101)]
    public void EffectiveAttack_ShouldBeTheStoredStat_WhenNoAtkInstanceIsActive(int attack)
    {
        // §5.4.1 consumes the modifier only while an instance is active; with no
        // TargetStat = "ATK" BuffDebuff present the Step-1 Attack input is the
        // stored stat itself (GAME_STATE.md §2.3.2 item 1: "no active effect" is
        // an empty array, never null).
        Assert.Equal(attack, StatusEffectLifecycle.EffectiveAttack(attack, [], []));
    }

    // =======================================================================
    // Root — the 30% reduction, COMBAT_RULES.md §5.4.1 item 3
    // =======================================================================

    [Fact]
    public void EffectiveAttack_ShouldReduceAtkByTheInstancesMagnitude()
    {
        // §5.4.1 item 3 for Root's own magnitude: truncate(100 × 70 / 100) = 70.
        Assert.Equal(70, StatusEffectLifecycle.EffectiveAttack(100, [], [Root(duration: 2)]));
    }

    [Theory]
    [InlineData(50, 35)]
    [InlineData(51, 35)]
    [InlineData(99, 69)]
    [InlineData(100, 70)]
    [InlineData(101, 70)]
    public void EffectiveAttack_ShouldTruncateTowardZero(int attack, int expected)
    {
        // §5.4.2's worked values, asserted verbatim. 51 → 35 and 101 → 70 are the
        // cases that distinguish truncation from rounding-to-nearest (which would
        // give 36 and 71); 99 → 69 distinguishes it from a ceiling.
        Assert.Equal(expected, StatusEffectLifecycle.EffectiveAttack(attack, [], [Root(duration: 1)]));
    }

    [Fact]
    public void EffectiveAttack_ShouldNotDependOnFloatingPointRepresentation()
    {
        // §5.4.2: "The calculation must not depend on floating-point
        // representation: the same input ATK and magnitude yield the same
        // EffectiveATK integer on every platform and in every evaluation order."
        // A value whose exact product is a repeating binary fraction is the case
        // a double multiply-then-cast could round differently; the documented
        // truncation is asserted exactly.
        // 77 × 70 / 100 = 53.9 → 53.
        Assert.Equal(53, StatusEffectLifecycle.EffectiveAttack(77, [], [Root(duration: 1)]));
    }

    // =======================================================================
    // What the percentage applies to — COMBAT_RULES.md §5.4.1 item 2
    // =======================================================================

    [Fact]
    public void EffectiveAttack_ShouldReduceAtkOnlyAndLeaveTheDamagePoolUnmodified()
    {
        // §5.4.1's worked example: PetState.ATK 100, ATK-Gem-generated pool 40,
        // Magnitude 30. EffectiveATK = truncate(100 × 70 / 100) = 70, so Step 1
        // Base Damage = 70 + 40 = 110.
        //
        // The pool is a separate Step-1 contribution (§3 step 1) passed straight
        // through by the caller, so this asserts the ATK term alone and then the
        // sum the pipeline forms — the reduction happens BEFORE the two are added.
        const int attack = 100;
        const int pool = 40;

        var effective = StatusEffectLifecycle.EffectiveAttack(attack, [], [Root(duration: 2)]);

        Assert.Equal(70, effective);
        Assert.Equal(110, effective + pool);

        // §5.4.1: "It is NOT (100 + 40) × 70% = 98." Asserted as a negative so the
        // rejected reading fails loudly rather than merely producing a difference.
        // The pool is never an input to the reduction — it is added afterwards by
        // the caller — so the reduction is applied to the ATK term before the sum,
        // which is exactly what the two assertions above state.
        Assert.NotEqual(98, effective + pool);
    }

    // =======================================================================
    // Base stat preservation — COMBAT_RULES.md §5.4.4
    // =======================================================================

    [Fact]
    public void EffectiveAttack_ShouldNeverOverwriteTheStoredStat()
    {
        // §5.4.4: "PetState.ATK is never overwritten by the modifier, and there is
        // no 'restore' step: the stored stat is unchanged throughout." The
        // consumer is a pure function of (stat, instances) — it returns a value
        // and mutates neither its argument nor the instance collection.
        var effects = new[] { Root(duration: 2) };
        var before = effects[0];

        var first = StatusEffectLifecycle.EffectiveAttack(100, [], effects);
        var second = StatusEffectLifecycle.EffectiveAttack(100, [], effects);

        Assert.Equal(70, first);
        Assert.Equal(70, second);

        // The collection is unchanged, so no expiry/reset mechanism and no
        // EffectiveATK member could have been written into it.
        Assert.Single(effects);
        Assert.Equal(before, effects[0]);
        Assert.Equal(2, effects[0].RemainingTurns);
        Assert.Equal(30, effects[0].Magnitude);

        // And the identical input still yields the identical output — the stored
        // stat was never consumed, decremented, or restored.
        Assert.Equal(first, StatusEffectLifecycle.EffectiveAttack(100, [], effects));
    }

    // =======================================================================
    // Selection — COMBAT_RULES.md §5.4.5
    // =======================================================================

    [Fact]
    public void EffectiveAttack_ShouldIgnoreABuffDebuffNamingAnotherStat()
    {
        // §5.4.5: "Does NOT apply to a BuffDebuff naming any stat other than
        // 'ATK'; the consumer reads TargetStat explicitly and a non-'ATK' value
        // modifies no ATK." TASK-118 §14 requires this guard explicitly.
        StatusEffect[] effects =
        [
            Debuff("Weaken", "DEF", 30, duration: 2),
            Debuff("Sluggish", "Crit", 50, duration: 2),
        ];

        Assert.Equal(100, StatusEffectLifecycle.EffectiveAttack(100, [], effects));
    }

    [Fact]
    public void EffectiveAttack_ShouldIgnoreDoTShieldAndStateInstances()
    {
        // §5.4.5: "Does NOT apply to a DoT tick or a Shield: those Types have
        // their own rules (§5.2 item 3, §4) and are not ATK modifiers." A Stun
        // (Type = State, GAME_STATE.md §2.4.5) is likewise not one. None of the
        // three carries a TargetStat at all (§2.3.1 item 7 pairs it with
        // BuffDebuff exclusively), so none can be selected.
        StatusEffect[] effects =
        [
            StatusEffect.TurnBased("Burn", StatusEffectType.DoT, StatusEffectSource.Boss, 50, 2),
            StatusEffect.TurnBased("Stun", StatusEffectType.State, StatusEffectSource.Boss, 0, 2),
            StatusEffect.TriggerBased(
                "Shield", StatusEffectType.Shield, StatusEffectSource.Player, 200,
                StatusEffect.ShieldDepletedCondition),
        ];

        Assert.Equal(100, StatusEffectLifecycle.EffectiveAttack(100, [], effects));
    }

    [Fact]
    public void EffectiveAttack_ShouldSelectOnTargetStatAndNotOnIdentity()
    {
        // §5.4.5 makes Root the MVP INSTANCE of the rule, not the rule's identity.
        // Dispatching on Id would encode a second, undocumented definition of which
        // effects modify ATK — so a differently-named instance naming "ATK" must
        // apply, and Root's own identity must not be what selects it.
        StatusEffect[] renamed =
        [
            Debuff("Armor Break", "ATK", 30, duration: 2),
        ];

        Assert.Equal(70, StatusEffectLifecycle.EffectiveAttack(100, [], renamed));
    }

    [Fact]
    public void EffectiveAttack_ShouldIgnoreAnExpiredRootBecauseItIsNoLongerPresent()
    {
        // §5.4.3: the modifier is active "according to the committed
        // GAME_STATE.md §2.3.1 instance state at attack resolution". §5.3 DR5 and
        // §2.3.1 item 8 remove an instance whose count reaches 0 in the same
        // step-19a resolution, so an expired Root is ABSENT rather than stored at
        // 0 — and absence is what makes it ignored. This asserts the documented
        // state transition rather than a special case in the consumer.
        StatusEffect[] active = [Root(duration: 1)];

        Assert.Equal(70, StatusEffectLifecycle.EffectiveAttack(100, [], active));

        // Turn N+1's step 19a: 1 → 0 → the instance is removed.
        var afterExpiry = StatusEffectLifecycle.ConsumeAtStep19a(active);

        Assert.Empty(afterExpiry);
        Assert.Equal(100, StatusEffectLifecycle.EffectiveAttack(100, [], afterExpiry));
    }

    // =======================================================================
    // Duration — COMBAT_RULES.md §5.4.3, §5.3 DR2/DR5
    // =======================================================================

    [Fact]
    public void EffectiveAttack_ShouldRemainReducedWhileTheInstanceIsActive()
    {
        // §5.4.3's documented timeline, asserted as state transitions:
        //   applied at step 18b of Turn N with RemainingTurns = 2
        //   Turn N   step 19a: 2 → 1  (still active)
        //   Turn N+1 step 19a: 1 → 0  → expires
        var applied = StatusEffectLifecycle.Apply([], Root(duration: 2));

        Assert.Equal(2, applied[0].RemainingTurns);

        var afterTurnN = StatusEffectLifecycle.ConsumeAtStep19a(applied);

        Assert.Single(afterTurnN);
        Assert.Equal(1, afterTurnN[0].RemainingTurns);
        Assert.Equal(70, StatusEffectLifecycle.EffectiveAttack(100, [], afterTurnN));

        var afterTurnNPlus1 = StatusEffectLifecycle.ConsumeAtStep19a(afterTurnN);

        // Turn N+2: inactive — Pet ATK is its normal derived value again.
        Assert.Empty(afterTurnNPlus1);
        Assert.Equal(100, StatusEffectLifecycle.EffectiveAttack(100, [], afterTurnNPlus1));
    }

    [Fact]
    public void EffectiveAttack_ShouldUseTheRefreshedMagnitudeAfterReapplication()
    {
        // COMBAT_RULES.md §5.2 item 2 / §5.3 DR3: re-application refreshes the one
        // instance (duration and magnitude re-set) rather than appending a second
        // or summing. §5.4.3 then reads the refreshed instance's own magnitude.
        var applied = StatusEffectLifecycle.Apply([], Root(duration: 2));
        var refreshed = StatusEffectLifecycle.Apply(applied, Root(duration: 2));

        Assert.Single(refreshed);
        Assert.Equal(2, refreshed[0].RemainingTurns);
        Assert.Equal(70, StatusEffectLifecycle.EffectiveAttack(100, [], refreshed));
    }

    // =======================================================================
    // Several simultaneous ATK modifiers — COMBAT_RULES.md §5.6.6
    // =======================================================================

    [Fact]
    public void EffectiveAttack_ShouldSumTheActiveInstancesAndBeOrderIndependent()
    {
        // COMBAT_RULES.md §5.6.6 item 2 makes every applicable instance a signed
        // percentage-point contribution to ONE TotalATKModifierPercentage, item 3
        // makes the composition order-independent, and item 4 applies the combined
        // percentage with a single truncation. Sequential per-instance application
        // — the model §5.4.1 used before the TASK-138 decision — is explicitly
        // prohibited (TASK-138 D3/D5), so this asserts the canonical reading: no
        // MVP content produces two simultaneous TargetStat = "ATK" instances
        // (GAME_STATE.md §2.3.1 item 6 bounds the collection to one instance per
        // Id), and the documented property is the signed sum.
        StatusEffect[] forward =
        [
            Debuff("Debuff A", "ATK", 30, duration: 2),
            Debuff("Debuff B", "ATK", 50, duration: 2),
        ];

        StatusEffect[] reversed = [forward[1], forward[0]];

        // TotalATKModifierPercentage = (-30) + (-50) = -80, applied once:
        // truncate(100 × (100 − 80) / 100) = 20.
        //
        // The prohibited sequential model would give 100 → 70 → 35, which is the
        // divergence §5.6.6 item 3 and item 4 forbid.
        Assert.Equal(20, StatusEffectLifecycle.EffectiveAttack(100, [], forward));
        Assert.Equal(20, StatusEffectLifecycle.EffectiveAttack(100, [], reversed));
    }
}
