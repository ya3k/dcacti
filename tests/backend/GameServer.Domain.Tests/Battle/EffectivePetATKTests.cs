using GameServer.Domain.Battle;
using Xunit;

namespace GameServer.Domain.Tests.Battle;

/// <summary>
/// The unified Pet ATK composition — <c>EffectivePetATK</c>
/// (<c>COMBAT_RULES.md</c> §5.6.1, §5.6.6).
///
/// <code>
/// Rule (COMBAT_RULES.md §5.6.6 items 1–8, TASK-137/138 D1–D7)
///  ↓
/// Scenario (Given a base ATK and the active carriers,
///           When the composition runs,
///           Then one signed sum is applied and truncated once)
///  ↓
/// Test
/// </code>
///
/// <b>These are the contract's own worked examples.</b> §5.6.6 item 6 fixes the
/// resolved integers — <c>50</c> at <c>+5</c> → <c>52</c>, at <c>-30</c> →
/// <c>35</c>, and at <c>(+5) + (-30)</c> → <c>37</c> — and §5.6.6 item 6's fourth
/// example fixes the divergence from the prohibited sequential model
/// (<c>30</c>, not <c>31</c>). A test asserting them is asserting the documented
/// arithmetic, not the implementation's current output.
/// </summary>
public class EffectivePetATKTests
{
    // =======================================================================
    // The composition — COMBAT_RULES.md §5.6.6 item 5
    // =======================================================================

    [Theory]
    [InlineData(0, 0)]
    [InlineData(50, 50)]
    [InlineData(1000, 1000)]
    public void Compose_ShouldBeTheBaseStat_WhenNoModifierIsApplicable(int baseAtk, int expected)
    {
        // §5.6.6 item 2: TotalATKModifierPercentage = 0 means unchanged, and
        // §5.6.1 item 4 states the composed value is then the base stat itself.
        Assert.Equal(expected, EffectivePetATK.Compose(baseAtk, [], []));
    }

    [Fact]
    public void Compose_ShouldApplyTheRelicCarrierAlone()
    {
        // §5.6.6 item 6 "Example 3: Relic-Only" — and §5.6.1's own worked example:
        // truncate(50 × 105 / 100) = truncate(52.5) = 52.
        var effective = EffectivePetATK.Compose(50, [new ATKModifier("berserker-core", 5)], []);

        Assert.Equal(52, effective);
    }

    [Fact]
    public void Compose_ShouldApplyTheBuffDebuffCarrierAlone()
    {
        // §5.6.6 item 6 "Example 2: BuffDebuff-Only" (TASK-138 D1) — the case that
        // made §5.6.6, not §5.4.1, the owner of the arithmetic:
        // truncate(50 × (100 − 30) / 100) = 35.
        var root = BuffDebuff("Root", "ATK", 30, duration: 2);

        Assert.Equal(35, EffectivePetATK.Compose(50, [], [root]));
    }

    [Fact]
    public void Compose_ShouldComposeBothCarriersAsOneSignedSum()
    {
        // §5.6.6 item 6 "Example 1: Relic + BuffDebuff Coexistence" (TASK-137 D1):
        // (+5) + (-30) = -25, applied once: truncate(50 × 75 / 100) = 37.
        var effective = EffectivePetATK.Compose(
            50,
            [new ATKModifier("berserker-core", 5)],
            [BuffDebuff("Root", "ATK", 30, duration: 2)]);

        Assert.Equal(37, effective);
    }

    [Fact]
    public void Compose_ShouldBeOrderIndependent()
    {
        // §5.6.6 item 3: composition is order-independent because every applicable
        // source is combined into TotalATKModifierPercentage BEFORE it is applied to
        // the base stat.
        ATKModifier[] relics = [new("source-a", 5), new("source-b", -10)];
        StatusEffect[] effects = [BuffDebuff("Root", "ATK", 30, duration: 2)];

        ATKModifier[] relicsReversed = [relics[1], relics[0]];

        Assert.Equal(
            EffectivePetATK.Compose(50, relics, effects),
            EffectivePetATK.Compose(50, relicsReversed, effects));
    }

    [Fact]
    public void Compose_ShouldTruncateOnce_NotPerModifier()
    {
        // §5.6.6 item 4 / item 6 "Example 4: Multi-Modifier Divergence Illustration":
        // two simultaneous BuffDebuff instances (-30% and -10%) at base 50 give
        // TotalATKModifierPercentage = -40 and truncate(50 × 60 / 100) = 30.
        //
        // The prohibited sequential model — per-instance application with intermediate
        // truncation — gives truncate(35 × 90 / 100) = 31, which is exactly the
        // divergence §5.6.6 item 4 forbids.
        var effective = EffectivePetATK.Compose(
            50,
            [],
            [BuffDebuff("Debuff A", "ATK", 30, duration: 2), BuffDebuff("Debuff B", "ATK", 10, duration: 2)]);

        Assert.Equal(30, effective);
    }

    [Theory]
    // §5.4.2's single-modifier truncation table, which the unified formula reproduces
    // because one modifier is the case where the two models coincide.
    [InlineData(50, 35)]
    [InlineData(51, 35)]
    [InlineData(99, 69)]
    [InlineData(100, 70)]
    [InlineData(101, 70)]
    public void Compose_ShouldTruncateTowardZero_ForASingleReduction(int baseAtk, int expected)
    {
        Assert.Equal(expected, EffectivePetATK.Compose(baseAtk, [], [BuffDebuff("Root", "ATK", 30, duration: 1)]));
    }

    [Fact]
    public void Compose_ShouldAuthorNoCap()
    {
        // §5.6.1 item 5 / §5.6.6 item 2: ATK has no documented valid range, so the
        // composed value is deliberately unclamped. Authoring a range is a separate
        // balance decision (GAME_RULES.md §20) and none is made here.
        var effective = EffectivePetATK.Compose(50, [new ATKModifier("huge", 300)], []);

        Assert.Equal(200, effective);
    }

    // =======================================================================
    // Selection and the base stat — §5.4.5, §5.6.6 items 2 and 8
    // =======================================================================

    [Fact]
    public void Compose_ShouldSelectBuffDebuffInstancesByTypeAndTargetStat_NotByIdentity()
    {
        // §5.4.5 / §5.6.6 item 2: Root is the MVP INSTANCE of the rule, not the
        // rule's name, so a differently-named instance naming "ATK" applies while a
        // DoT, a Shield, a State, or a BuffDebuff naming another stat does not.
        StatusEffect[] effects =
        [
            BuffDebuff("Some Future Weaken", "ATK", 30, duration: 2),
            BuffDebuff("Guard", "DEF", 50, duration: 2),
            Dot("Burn", 40, duration: 2),
            Shield(100),
            Stun(1),
        ];

        Assert.Equal(70, EffectivePetATK.Compose(100, [], effects));
    }

    [Fact]
    public void Compose_ShouldNotWriteTheBaseStatOrTheCollections()
    {
        // §5.6.4 / §5.6.6 item 8: PetState.ATK remains permanent and is never
        // overwritten, restored, or reset, and the composed value is derived for one
        // calculation only. A pure function over collections it cannot mutate is the
        // strongest form of that statement.
        var relics = new[] { new ATKModifier("berserker-core", 5) };
        var effects = new[] { BuffDebuff("Root", "ATK", 30, duration: 2) };

        var first = EffectivePetATK.Compose(50, relics, effects);
        var second = EffectivePetATK.Compose(50, relics, effects);

        Assert.Equal(37, first);
        Assert.Equal(first, second);
        Assert.Equal(5, relics[0].ATKModifierPercentage);
        Assert.Equal(30d, effects[0].Magnitude);
    }

    [Fact]
    public void Compose_ShouldSelectOnlyTheDocumentedStat()
    {
        // §5.4.5: no other BuffDebuff stat has a recorded consumption rule, so none
        // contributes and none is inferred.
        Assert.Equal(
            50,
            EffectivePetATK.Compose(50, [], [BuffDebuff("Future Crit Buff", "Crit", 30, duration: 2)]));
    }

    // =======================================================================
    // Harness
    // =======================================================================

    private static StatusEffect BuffDebuff(string id, string stat, double magnitude, int duration) =>
        StatusEffect.TurnBased(id, StatusEffectType.BuffDebuff, StatusEffectSource.Boss, magnitude, duration, stat);

    private static StatusEffect Dot(string id, double magnitude, int duration) =>
        StatusEffect.TurnBased(id, StatusEffectType.DoT, StatusEffectSource.Boss, magnitude, duration);

    private static StatusEffect Shield(double magnitude) =>
        StatusEffect.TriggerBased(
            "Shield",
            StatusEffectType.Shield,
            StatusEffectSource.Player,
            magnitude,
            StatusEffect.ShieldDepletedCondition);

    private static StatusEffect Stun(int duration) =>
        StatusEffect.TurnBased("Stun", StatusEffectType.State, StatusEffectSource.Boss, 0, duration);
}
