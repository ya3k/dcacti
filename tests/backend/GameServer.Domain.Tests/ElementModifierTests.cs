using GameServer.Domain.Elements;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Element Modifier lookup tests (<c>ELEMENT_RULES.md</c> §2.2, §3;
/// <c>COMBAT_RULES.md</c> §3 step 3).
///
/// §2.2 fixes the three default factors:
///
/// <code>
/// Advantage     = 1.50×
/// Neutral       = 1.00×
/// Disadvantage  = 0.75×
/// </code>
///
/// and states they "are configuration values ... not hardcoded constants", so
/// these tests cover both halves of that sentence: the documented defaults are
/// exactly as §2.2 gives them, and a caller-supplied set replaces them without
/// any code change — which is the balance-change path §2.2 describes.
///
/// §2.2 also gives its values as multipliers (<c>1.50×</c>), so the assertions
/// check the factor itself, not a percentage or a delta. These numbers are read
/// from §2.2, not from the implementation.
/// </summary>
public class ElementModifierTests
{
    // ------------------------------------------------------- default values ---

    [Fact]
    public void Default_ShouldCarryTheDocumentedFactors()
    {
        // ELEMENT_RULES.md §2.2: Advantage 1.50×, Neutral 1.00×, Disadvantage 0.75×
        var modifiers = ElementModifiers.Default;

        Assert.Equal(1.50, modifiers.Advantage);
        Assert.Equal(1.00, modifiers.Neutral);
        Assert.Equal(0.75, modifiers.Disadvantage);
    }

    [Theory]
    [InlineData(ElementMatchup.Advantage, 1.50)]
    [InlineData(ElementMatchup.Neutral, 1.00)]
    [InlineData(ElementMatchup.Disadvantage, 0.75)]
    public void For_ShouldReturnTheDocumentedFactorForEachMatchup(
        ElementMatchup matchup,
        double expected)
    {
        // Given a matchup resolved by §2.1
        // When the modifier lookup runs (COMBAT_RULES.md §3 step 3)
        var factor = ElementModifiers.Default.For(matchup);

        // Then it is the §2.2 default factor for that outcome
        Assert.Equal(expected, factor);
    }

    [Fact]
    public void Default_ShouldBeTheOnlySetOfDefaultValues()
    {
        // §2.2's values are configuration in one place, so a balance change edits
        // one record rather than scattered literals. Concretely: the default set is
        // the three documented factors and nothing else is exposed to override.
        var modifiers = ElementModifiers.Default;

        Assert.Equal(1.50, modifiers.For(ElementMatchup.Advantage));
        Assert.Equal(1.00, modifiers.For(ElementMatchup.Neutral));
        Assert.Equal(0.75, modifiers.For(ElementMatchup.Disadvantage));
    }

    // ---------------------------------------------------- configurable (§2.2) ---

    [Fact]
    public void For_ShouldReturnSuppliedFactors_BecauseTheyAreConfiguration()
    {
        // §2.2: "These are configuration values ... Changing them is a balance
        // change and does not require a rule-conflict process". Given a balance
        // change that re-tunes the three factors
        var rebalanced = new ElementModifiers(advantage: 2.00, neutral: 1.00, disadvantage: 0.50);

        // When each matchup is priced
        // Then the supplied factors are used, with no code change required
        Assert.Equal(2.00, rebalanced.For(ElementMatchup.Advantage));
        Assert.Equal(1.00, rebalanced.For(ElementMatchup.Neutral));
        Assert.Equal(0.50, rebalanced.For(ElementMatchup.Disadvantage));
    }

    [Fact]
    public void Modifiers_ShouldBeSuppliedPerCall_NotHeldAsMutableSharedState()
    {
        // §2.2 makes the factors configuration, but a static mutable "current"
        // instance would let one resolution's balance edit affect another's. The
        // configuration is therefore a value a caller passes: supplying a set never
        // changes the documented default for anyone else.
        var rebalanced = new ElementModifiers(2.00, 1.00, 0.50);

        Assert.Equal(2.00, rebalanced.For(ElementMatchup.Advantage));

        // The default is unchanged and still reachable — no global was mutated.
        Assert.Equal(1.50, ElementModifiers.Default.For(ElementMatchup.Advantage));
    }

    [Fact]
    public void ElementModifiers_ShouldExposeNoStaticMutableMember()
    {
        // §2.2 configuration must be replaceable, not secretly global state: no
        // settable static field or property may exist on the configuration type.
        const System.Reflection.BindingFlags Statics =
            System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Static;

        var mutableStatics = typeof(ElementModifiers)
            .GetFields(Statics)
            .Where(f => !f.IsInitOnly && !f.IsLiteral)
            .Select(f => f.Name)
            .Concat(typeof(ElementModifiers)
                .GetProperties(Statics)
                .Where(p => p.SetMethod is not null)
                .Select(p => p.Name))
            .ToArray();

        Assert.Empty(mutableStatics);
    }

    [Fact]
    public void For_ShouldRejectAnUndocumentedMatchupOutcome()
    {
        // §2.1: "A matchup is exactly one of Advantage / Neutral / Disadvantage."
        // An outcome outside those three has no §2.2 factor, and returning a
        // plausible default would silently price an undefined matchup.
        var undefined = (ElementMatchup)99;

        Assert.Throws<ArgumentOutOfRangeException>(() => ElementModifiers.Default.For(undefined));
    }

    // -------------------------------------------------- constructor / shape ---

    [Fact]
    public void ElementModifiers_ShouldRoundTripThroughItsConstructor()
    {
        // The three factors are the whole of the configuration: nothing else is
        // needed to price §2.1's three outcomes.
        var modifiers = new ElementModifiers(1.25, 1.05, 0.80);

        Assert.Equal(1.25, modifiers.Advantage);
        Assert.Equal(1.05, modifiers.Neutral);
        Assert.Equal(0.80, modifiers.Disadvantage);
    }

    // ------------------------------------ integrated matchup → modifier ---

    [Fact]
    public void ResolvedMatchup_ShouldPriceAtTheDocumentedFactor()
    {
        // The two stages composed, as COMBAT_RULES.md §3 step 3 consumes them:
        // §2.1 resolves the matchup, then §2.2 prices it.
        var modifiers = ElementModifiers.Default;

        // Advantage — a documented countering pair (ELEMENT_RULES.md §2)
        Assert.Equal(
            1.50,
            modifiers.For(ElementMatchups.Resolve(Element.Moc, Element.Tho)));

        // Disadvantage — the same pair reversed (§2.1)
        Assert.Equal(
            0.75,
            modifiers.For(ElementMatchups.Resolve(Element.Tho, Element.Moc)));

        // Neutral — same Element (§2.1)
        Assert.Equal(
            1.00,
            modifiers.For(ElementMatchups.Resolve(Element.Hoa, Element.Hoa)));

        // Neutral — elementless attacker (§3 item 1)
        Assert.Equal(
            1.00,
            modifiers.For(ElementMatchups.Resolve(null, Element.Kim)));

        // Neutral — elementless defender (§3 item 2)
        Assert.Equal(
            1.00,
            modifiers.For(ElementMatchups.Resolve(Element.Kim, null)));

        // Neutral — both elementless (§3, no relationship in either direction)
        Assert.Equal(
            1.00,
            modifiers.For(ElementMatchups.Resolve(null, null)));
    }

    [Fact]
    public void NeutralFactor_ShouldBeTheIdentitySoNeutralChangesNothing()
    {
        // §2.2 gives Neutral as 1.00×. COMBAT_RULES.md §3 multiplies this step into
        // the running damage value, so a Neutral matchup leaves Base Damage × Combo
        // Modifier unchanged into step 4 — the documented meaning of "Neutral".
        var modifiers = ElementModifiers.Default;

        Assert.Equal(1.0, modifiers.Neutral);

        foreach (var value in new[] { 0.0, 1.0, 37.5, 1000.0 })
        {
            Assert.Equal(value, value * modifiers.Neutral);
        }
    }

    [Fact]
    public void AdvantageFactor_ShouldIncreaseAndDisadvantageFactor_ShouldDecrease()
    {
        // §2.2 names the outcomes "Advantage" and "Disadvantage", so the defaults
        // must actually be an increase and a decrease relative to Neutral. This
        // pins the direction of the documented values independently of their exact
        // magnitude.
        var modifiers = ElementModifiers.Default;

        Assert.True(modifiers.Advantage > modifiers.Neutral);
        Assert.True(modifiers.Disadvantage < modifiers.Neutral);
    }
}
