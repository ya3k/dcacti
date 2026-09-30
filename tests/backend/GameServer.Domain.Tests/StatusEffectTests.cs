using GameServer.Domain.Battle;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// The <c>StatusEffect</c> instance contract — <c>GAME_STATE.md</c> §2.3.1.
///
/// Every assertion below traces to a numbered item of that section. The tests
/// verify the <b>shape and its structural rules</b>, not gameplay behavior: the
/// effect rules themselves live in <c>COMBAT_RULES.md</c> §5 and
/// <c>BOSS_RULES.md</c> §6.3.1 and are deliberately not represented here
/// (§2.3.1 item 1).
/// </summary>
public class StatusEffectTests
{
    // =====================================================================
    // §2.3.1 item 3 / item 7: the two duration models are mutually exclusive
    // =====================================================================

    [Fact]
    public void TurnBased_ShouldCarryRemainingTurnsAndNoExpiryCondition()
    {
        // GAME_STATE.md §2.3.1 item 3: a Turn-based instance (DoT, BuffDebuff,
        // State) carries RemainingTurns and no ExpiryCondition.
        var effect = StatusEffect.TurnBased(
            "Root",
            StatusEffectType.BuffDebuff,
            StatusEffectSource.Boss,
            magnitude: -30,
            duration: 2,
            targetStat: "ATK");

        Assert.Equal("Root", effect.Id);
        Assert.Equal(StatusEffectType.BuffDebuff, effect.Type);
        Assert.Equal(StatusEffectSource.Boss, effect.Source);
        Assert.Equal(-30, effect.Magnitude);
        Assert.Equal("ATK", effect.TargetStat);
        Assert.Equal(2, effect.RemainingTurns);
        Assert.Null(effect.ExpiryCondition);
        Assert.True(effect.UsesTurnCountdown);
    }

    [Fact]
    public void TriggerBased_ShouldCarryExpiryConditionAndNoRemainingTurns()
    {
        // GAME_STATE.md §2.3.1 item 3: Shield is the trigger-based type ("until
        // Shield is depleted"); it carries ExpiryCondition and no RemainingTurns.
        var effect = StatusEffect.TriggerBased(
            "Shield",
            StatusEffectType.Shield,
            StatusEffectSource.Player,
            magnitude: 200,
            expiryCondition: StatusEffect.ShieldDepletedCondition);

        Assert.Equal("Shield", effect.Id);
        Assert.Equal(StatusEffectType.Shield, effect.Type);
        Assert.Equal(200, effect.Magnitude);
        Assert.Null(effect.RemainingTurns);
        Assert.Equal(StatusEffect.ShieldDepletedCondition, effect.ExpiryCondition);
        Assert.False(effect.UsesTurnCountdown);
    }

    [Fact]
    public void Instance_ShouldNeverCarryBothDurationModels()
    {
        // GAME_STATE.md §2.3.1 item 3: an instance uses "either the Turn countdown
        // ... or a trigger-based expiry ... — never both and never neither". The
        // two factories are the only construction paths, so every instance
        // satisfies "exactly one" by construction. This asserts the invariant
        // across both models rather than trusting it.
        var turnBased = StatusEffect.TurnBased(
            "Burn", StatusEffectType.DoT, StatusEffectSource.Boss, 50, 2);

        var triggerBased = StatusEffect.TriggerBased(
            "Shield", StatusEffectType.Shield, StatusEffectSource.Player, 100,
            StatusEffect.ShieldDepletedCondition);

        foreach (var effect in new[] { turnBased, triggerBased })
        {
            var hasTurns = effect.RemainingTurns is not null;
            var hasCondition = effect.ExpiryCondition is not null;

            Assert.True(
                hasTurns ^ hasCondition,
                $"'{effect.Id}' must use exactly one duration model (GAME_STATE.md §2.3.1 item 3).");
        }
    }

    // =====================================================================
    // §2.3.1 item 7: TargetStat is present iff Type = BuffDebuff
    // =====================================================================

    [Theory]
    [InlineData(StatusEffectType.DoT)]
    [InlineData(StatusEffectType.State)]
    public void TurnBased_ShouldRejectTargetStatForNonBuffDebuffTypes(StatusEffectType type)
    {
        // GAME_STATE.md §2.3.1 item 7: TargetStat is "present iff Type =
        // BuffDebuff ... absent otherwise", so supplying one for another type is a
        // contract violation rather than a value to ignore.
        Assert.Throws<ArgumentException>(() => StatusEffect.TurnBased(
            "Burn", type, StatusEffectSource.Boss, 50, 2, targetStat: "ATK"));
    }

    [Fact]
    public void TurnBased_ShouldRequireTargetStatForBuffDebuff()
    {
        // GAME_STATE.md §2.3.1 item 7: a BuffDebuff's TargetStat is present, so an
        // instance that modifies a stat must say which one. The framework's
        // guard distinguishes an absent value (ArgumentNullException) from a blank
        // one (ArgumentException); both derive from ArgumentException and both are
        // a rejection, which is what this asserts.
        Assert.ThrowsAny<ArgumentException>(() => StatusEffect.TurnBased(
            "Root", StatusEffectType.BuffDebuff, StatusEffectSource.Boss, -30, 2));

        Assert.Throws<ArgumentException>(() => StatusEffect.TurnBased(
            "Root", StatusEffectType.BuffDebuff, StatusEffectSource.Boss, -30, 2,
            targetStat: "   "));
    }

    // =====================================================================
    // §2.3.1 item 3: the type/model pairing is fixed
    // =====================================================================

    [Fact]
    public void TurnBased_ShouldRejectShield()
    {
        // GAME_STATE.md §2.3.1 item 3: Shield uses the trigger-based model, so
        // building one on the Turn countdown would give a single effect two
        // duration models.
        Assert.Throws<ArgumentException>(() => StatusEffect.TurnBased(
            "Shield", StatusEffectType.Shield, StatusEffectSource.Player, 100, 2));
    }

    [Theory]
    [InlineData(StatusEffectType.DoT)]
    [InlineData(StatusEffectType.BuffDebuff)]
    [InlineData(StatusEffectType.State)]
    public void TriggerBased_ShouldRejectNonShieldTypes(StatusEffectType type)
    {
        // GAME_STATE.md §2.3.1 item 3: DoT, BuffDebuff, and State use the Turn
        // countdown; the trigger-based model is Shield's.
        Assert.Throws<ArgumentException>(() => StatusEffect.TriggerBased(
            "X", type, StatusEffectSource.Boss, 1, StatusEffect.ShieldDepletedCondition));
    }

    // =====================================================================
    // §2.3.1 item 1 / item 6: the identity is required and is the uniqueness key
    // =====================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Instance_ShouldRejectAnAbsentIdentity(string? id)
    {
        // GAME_STATE.md §2.3.1 item 1: Id names which Status Effect the instance
        // is. Item 6 de-duplicates on it, so an instance that does not name an
        // effect cannot participate in the one-per-identity rule. An absent value
        // and a blank one are both rejected (ArgumentNullException derives from
        // ArgumentException, so ThrowsAny covers the framework's split).
        Assert.ThrowsAny<ArgumentException>(() => StatusEffect.TurnBased(
            id!, StatusEffectType.DoT, StatusEffectSource.Boss, 50, 2));

        Assert.ThrowsAny<ArgumentException>(() => StatusEffect.TriggerBased(
            id!, StatusEffectType.Shield, StatusEffectSource.Player, 100,
            StatusEffect.ShieldDepletedCondition));
    }

    [Fact]
    public void TriggerBased_ShouldRejectAnAbsentExpiryCondition()
    {
        // GAME_STATE.md §2.3.1 item 3: a trigger-based instance's expiry IS its
        // ExpiryCondition, so an absent label would leave it with "neither" model.
        Assert.Throws<ArgumentException>(() => StatusEffect.TriggerBased(
            "Shield", StatusEffectType.Shield, StatusEffectSource.Player, 100, "  "));
    }

    // =====================================================================
    // §2.3.1 item 8 / COMBAT_RULES.md §5.3 DR5: 0 is never a committed counter
    // =====================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TurnBased_ShouldRejectANonPositiveDuration(int duration)
    {
        // GAME_STATE.md §2.3.1 item 8: "An instance at RemainingTurns = 0 is
        // removed at the step 19a resolution that produced the 0 ... so it is
        // never observable in a committed state." Constructing one already at 0
        // would create exactly the committed-zero state item 8 forbids. DR5 makes
        // expiry the step-19a transition to 0, not a construction value.
        Assert.Throws<ArgumentException>(() => StatusEffect.TurnBased(
            "Burn", StatusEffectType.DoT, StatusEffectSource.Boss, 50, duration));
    }

    // =====================================================================
    // §2.3.1 item 2: Magnitude is typed but not interpreted
    // =====================================================================

    [Fact]
    public void Instance_ShouldStoreMagnitudeWithoutInterpretingIt()
    {
        // GAME_STATE.md §2.3.1 item 2: "What the number means (flat damage, a
        // percentage reduction, an absorption pool) is owned by the effect's rule
        // document." This state layer therefore imposes no unit, sign, or range:
        // Burn's magnitude is flat positive damage and Root's is a negative
        // percentage, and both are stored verbatim.
        var burn = StatusEffect.TurnBased(
            "Burn", StatusEffectType.DoT, StatusEffectSource.Boss, 50, 2);

        var root = StatusEffect.TurnBased(
            "Root", StatusEffectType.BuffDebuff, StatusEffectSource.Boss, -30, 2, "ATK");

        Assert.Equal(50, burn.Magnitude);
        Assert.Equal(-30, root.Magnitude);
    }

    [Fact]
    public void Instance_ShouldBeAValueWithStructuralEquality()
    {
        // GAME_STATE.md §2.3.2 item 5: a round trip must return "the same elements,
        // the same member values, the same optional-member presence/absence". Value
        // equality is what makes that comparison meaningful, so two instances built
        // from the same values are equal and one differing in any member is not.
        var first = StatusEffect.TurnBased(
            "Burn", StatusEffectType.DoT, StatusEffectSource.Boss, 50, 2);

        var same = StatusEffect.TurnBased(
            "Burn", StatusEffectType.DoT, StatusEffectSource.Boss, 50, 2);

        var differentMagnitude = StatusEffect.TurnBased(
            "Burn", StatusEffectType.DoT, StatusEffectSource.Boss, 51, 2);

        var differentDuration = StatusEffect.TurnBased(
            "Burn", StatusEffectType.DoT, StatusEffectSource.Boss, 50, 3);

        Assert.Equal(first, same);
        Assert.NotEqual(first, differentMagnitude);
        Assert.NotEqual(first, differentDuration);
    }
}
