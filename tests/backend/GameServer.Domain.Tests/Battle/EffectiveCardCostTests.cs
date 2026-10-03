using GameServer.Domain.Battle;
using GameServer.Domain.Cards;
using Xunit;

namespace GameServer.Domain.Tests.Battle;

/// <summary>
/// The effective Card cost — <c>EffectiveCardCost</c>
/// (<c>CARD_RULES.md</c> §3.6).
///
/// <code>
/// Rule (CARD_RULES.md §3.6 items 1–9, TASK-134 D4/D5 + the truncation decision)
///  ↓
/// Scenario (Given an authored cost and the active modifiers,
///           When the cost is composed,
///           Then the documented integer is produced)
///  ↓
/// Test
/// </code>
///
/// <b>These are §3.6 item 4's own worked values.</b> The subsection fixes
/// <c>15</c> at 50% → <c>7</c>, <c>25</c> at 50% → <c>12</c>, <c>15</c> at 100% →
/// <c>0</c>, and <c>20</c> at 50% → <c>10</c>, and item 5 fixes that a cost of
/// <c>0</c> is a real cost with no minimum. A test asserting them is asserting
/// the documented arithmetic, not the implementation's current output.
/// </summary>
public class EffectiveCardCostTests
{
    [Theory]
    [InlineData(15, 7)]
    [InlineData(25, 12)]
    [InlineData(20, 10)]
    [InlineData(1, 0)]
    public void Compose_ShouldTruncateTowardZero(int powerCost, int expected)
    {
        // §3.6 item 4: EffectiveCardCost is an integer and a fractional
        // RawEffectiveCardCost is truncated TOWARD ZERO — not floor, ceil,
        // round-half-up, or banker's rounding.
        Assert.Equal(expected, EffectiveCardCost.Compose(powerCost, [Reduction("emergency-core", 50)]));
    }

    [Fact]
    public void Compose_ShouldBeTheAuthoredCost_WhenNoModifierIsActive()
    {
        // §3.6 item 7's "no modifier active" case: the composed cost is
        // CardDefinition.PowerCost itself, which item 1 keeps the authored/base value.
        Assert.Equal(30, EffectiveCardCost.Compose(30, []));
    }

    [Fact]
    public void Compose_ShouldCapTheTotalReductionAtOneHundredPercent()
    {
        // §3.6 item 3: multiple simultaneously-active modifiers compose by ADDING
        // their reductions, and the total is capped at 100 — which is what makes
        // RawEffectiveCardCost never negative and EffectiveCardCost never below 0.
        var effective = EffectiveCardCost.Compose(
            15,
            [Reduction("source-a", 50), Reduction("source-b", 50)]);

        Assert.Equal(0, effective);
    }

    [Fact]
    public void Compose_ShouldAddReductionsRatherThanApplyingThemSequentially()
    {
        // §3.6 item 3: "two independent 50% sources → 50 + 50, capped to 100".
        // Sequential application would give truncate(15 × 50 / 100) = 7 and then
        // truncate(7 × 50 / 100) = 3, which the additive rule does not define.
        var additive = EffectiveCardCost.Compose(20, [Reduction("source-a", 25), Reduction("source-b", 25)]);

        Assert.Equal(10, additive);
    }

    [Fact]
    public void Compose_ShouldHaveNoMinimumCost()
    {
        // §3.6 item 5: "There is no minimum-cost rule, and cost 0 is a real cost. No
        // 'minimum cost of 1' floor exists: a 100% total reduction yields
        // EffectiveCardCost = 0, and a cost of 0 is validated and deducted as 0."
        Assert.Equal(0, EffectiveCardCost.Compose(15, [Reduction("emergency-core", 100)]));
        Assert.Equal(0, EffectiveCardCost.Compose(0, []));
    }

    [Fact]
    public void Compose_ShouldReadEveryActiveSourceAndRequireTheCollection()
    {
        // §3.6 item 8: which modifiers participate is decided by the committed
        // PetState.CardCostModifiers[] contents at the moment the cast resolves, so
        // every element contributes. The collection has no absent representation
        // (GAME_STATE.md §2.3.5 item 6), so null is not a state it can be in.
        var effective = EffectiveCardCost.Compose(
            100,
            [Reduction("source-a", 10), Reduction("source-b", 5), Reduction("source-c", 25)]);

        Assert.Equal(60, effective);
        Assert.Throws<ArgumentNullException>(() => EffectiveCardCost.Compose(10, null!));
    }

    [Fact]
    public void Compose_ShouldNotMutateTheAuthoredCostOrTheCollection()
    {
        // §3.6 item 1: CardDefinition.PowerCost is never mutated — it is the INPUT to
        // the calculation. §3.6 item 2: the composed cost is derived and not stored.
        var modifiers = new[] { new CardCostModifier("emergency-core", 50) };
        var authored = 15;

        var first = EffectiveCardCost.Compose(authored, modifiers);
        var second = EffectiveCardCost.Compose(authored, modifiers);

        Assert.Equal(7, first);
        Assert.Equal(first, second);
        Assert.Equal(15, authored);
        Assert.Equal(50, modifiers[0].CostReductionPercentage);
    }

    private static CardCostModifier Reduction(string sourceIdentity, int percentage) =>
        new(sourceIdentity, percentage);
}
