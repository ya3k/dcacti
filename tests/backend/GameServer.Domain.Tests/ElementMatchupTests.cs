using System.Reflection;
using GameServer.Domain.Elements;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Tương Khắc matchup resolution tests (<c>ELEMENT_RULES.md</c> §1, §2, §2.1, §3).
///
/// Every expected result below is derived from the documented cycle, not from the
/// implementation:
///
/// <code>
/// Mộc  →  Thổ      (ELEMENT_RULES.md §2)
/// Thổ  →  Thủy
/// Thủy →  Hỏa
/// Hỏa  →  Kim
/// Kim  →  Mộc
/// </code>
///
/// "the element on the left counters the element on the right" (§2), and §2.1
/// fixes the three outcomes: <c>A → D</c> is Advantage, <c>D → A</c> is
/// Disadvantage, and everything else — <c>A == D</c> or an absent element — is
/// Neutral.
///
/// The full 5×5 matrix is asserted cell by cell in
/// <see cref="Resolve_ShouldMatchTheTươngKhắcMatrixForEveryElementPair"/> from an
/// independently written expectation table, so a single wrong cell fails with the
/// exact pair named. The remaining cases are §2.1's same-element rule, §3's two
/// elementless rules, the symmetry/inverse structure of the cycle, and the
/// deliberate absence of a sixth Element.
/// </summary>
public class ElementMatchupTests
{
    /// <summary>
    /// The five MVP Elements in <c>ELEMENT_RULES.md</c> §1 order
    /// (Mộc, Hỏa, Thổ, Kim, Thủy).
    /// </summary>
    private static readonly Element[] AllElements =
    [
        Element.Moc,
        Element.Hoa,
        Element.Tho,
        Element.Kim,
        Element.Thuy,
    ];

    /// <summary>
    /// The expects table for the 5×5 matrix, written from
    /// <c>ELEMENT_RULES.md</c> §2's cycle rather than from the code under test.
    ///
    /// Indexed <c>[attacker, defender]</c> over <see cref="AllElements"/>
    /// (Mộc, Hỏa, Thổ, Kim, Thủy) and read straight off the five documented
    /// relationships:
    ///
    /// <code>
    /// Mộc counters Thổ        → (Mộc, Thổ)   Advantage
    /// Thổ counters Thủy       → (Thổ, Thủy)  Advantage
    /// Thủy counters Hỏa       → (Thủy, Hỏa)  Advantage
    /// Hỏa counters Kim        → (Hỏa, Kim)   Advantage
    /// Kim counters Mộc        → (Kim, Mộc)   Advantage
    ///
    /// Each documented relationship read backwards is the Disadvantage cell:
    /// (Thổ, Mộc), (Thủy, Thổ), (Hỏa, Thủy), (Kim, Hỏa), (Mộc, Kim) → Disadvantage
    ///
    /// Everything else is Neutral (§2.1: A == D, and no other relationship exists).
    /// </code>
    /// </summary>
    private static ElementMatchup[,] DocumentedMatrix()
    {
        var matrix = new ElementMatchup[5, 5];

        // Neutral for every pair first (§2.1's fallback), then the ten
        // Advantage/Disadvantage cells the cycle defines.
        for (var attacker = 0; attacker < 5; attacker++)
        {
            for (var defender = 0; defender < 5; defender++)
            {
                matrix[attacker, defender] = ElementMatchup.Neutral;
            }
        }

        // The five documented relationships, as (attacker index, defender index)
        // pairs over Mộc, Hỏa, Thổ, Kim, Thủy.
        // Mộc(0) → Thổ(2), Hỏa(1) → Kim(3), Thổ(2) → Thủy(4),
        // Kim(3) → Mộc(0), Thủy(4) → Hỏa(1).
        (int Attacker, int Defender)[] counters =
        [
            (0, 2),
            (2, 4),
            (4, 1),
            (1, 3),
            (3, 0),
        ];

        foreach (var (attacker, defender) in counters)
        {
            matrix[attacker, defender] = ElementMatchup.Advantage;
            matrix[defender, attacker] = ElementMatchup.Disadvantage;
        }

        return matrix;
    }

    // --------------------------------------------------- the 25 combinations ---

    [Fact]
    public void Resolve_ShouldMatchTheTươngKhắcMatrixForEveryElementPair()
    {
        // Given the five MVP Elements (ELEMENT_RULES.md §1)
        // And the expectation table read from §2's cycle
        var expected = DocumentedMatrix();

        var covered = 0;

        // When every one of the 25 attacker × defender combinations is resolved
        for (var a = 0; a < AllElements.Length; a++)
        {
            for (var d = 0; d < AllElements.Length; d++)
            {
                var attacker = AllElements[a];
                var defender = AllElements[d];

                var actual = ElementMatchups.Resolve(attacker, defender);

                // Then it is exactly the documented outcome (§2.1)
                Assert.Equal(expected[a, d], actual);
                covered++;
            }
        }

        // And all 25 combinations were exercised
        Assert.Equal(25, covered);
    }

    [Fact]
    public void Resolve_ShouldProduceEachOutcomeTheDocumentedNumberOfTimes()
    {
        // ELEMENT_RULES.md §2 defines exactly five countering relationships over
        // five Elements, so the matrix contains exactly five Advantages and their
        // five mirror Disadvantage cells; the remaining fifteen pairs share no
        // documented relationship and are Neutral (§2.1).
        var counts = new Dictionary<ElementMatchup, int>();

        foreach (var attacker in AllElements)
        {
            foreach (var defender in AllElements)
            {
                var matchup = ElementMatchups.Resolve(attacker, defender);
                counts[matchup] = counts.GetValueOrDefault(matchup) + 1;
            }
        }

        Assert.Equal(5, counts[ElementMatchup.Advantage]);
        Assert.Equal(5, counts[ElementMatchup.Disadvantage]);
        Assert.Equal(15, counts[ElementMatchup.Neutral]);
    }

    [Theory]
    // The five documented relationships (ELEMENT_RULES.md §2): left counters right.
    [InlineData(Element.Moc, Element.Tho)]
    [InlineData(Element.Tho, Element.Thuy)]
    [InlineData(Element.Thuy, Element.Hoa)]
    [InlineData(Element.Hoa, Element.Kim)]
    [InlineData(Element.Kim, Element.Moc)]
    public void Resolve_ShouldBeAdvantage_WhenAttackerCountersDefender(
        Element attacker,
        Element defender)
    {
        // Given an attack with A against a defender with D
        // When A → D in the Tương Khắc cycle (ELEMENT_RULES.md §2)
        var matchup = ElementMatchups.Resolve(attacker, defender);

        // Then the matchup is Advantage (§2.1)
        Assert.Equal(ElementMatchup.Advantage, matchup);
    }

    [Theory]
    // The same five relationships read in reverse: right counters left.
    [InlineData(Element.Tho, Element.Moc)]
    [InlineData(Element.Thuy, Element.Tho)]
    [InlineData(Element.Hoa, Element.Thuy)]
    [InlineData(Element.Kim, Element.Hoa)]
    [InlineData(Element.Moc, Element.Kim)]
    public void Resolve_ShouldBeDisadvantage_WhenDefenderCountersAttacker(
        Element attacker,
        Element defender)
    {
        // Given an attack with A against a defender with D
        // When D → A in the Tương Khắc cycle (ELEMENT_RULES.md §2)
        var matchup = ElementMatchups.Resolve(attacker, defender);

        // Then the matchup is Disadvantage (§2.1)
        Assert.Equal(ElementMatchup.Disadvantage, matchup);
    }

    // ------------------------------------------------- same-element Neutral ---

    [Theory]
    [InlineData(Element.Moc)]
    [InlineData(Element.Hoa)]
    [InlineData(Element.Tho)]
    [InlineData(Element.Kim)]
    [InlineData(Element.Thuy)]
    public void Resolve_ShouldBeNeutral_WhenElementsAreEqual(Element element)
    {
        // Given an attack whose Element equals the defender's Element
        // When the matchup is resolved
        var matchup = ElementMatchups.Resolve(element, element);

        // Then it is Neutral and never Advantage (§2.1's explicit A == D case):
        // §2 defines no self-relationship, so an Element does not counter itself.
        Assert.Equal(ElementMatchup.Neutral, matchup);
        Assert.NotEqual(ElementMatchup.Advantage, matchup);
        Assert.NotEqual(ElementMatchup.Disadvantage, matchup);
    }

    [Fact]
    public void Resolve_ShouldBeNeutral_ForEverySameElementPair()
    {
        // The complete same-element set, so no Element is missed by the Theory
        // above (ELEMENT_RULES.md §1: exactly these five).
        foreach (var element in AllElements)
        {
            Assert.Equal(ElementMatchup.Neutral, ElementMatchups.Resolve(element, element));
        }
    }

    // ----------------------------------------------------- elementless (§3) ---

    [Fact]
    public void Resolve_ShouldBeNeutral_WhenAttackerIsElementless()
    {
        // Given a damage source with no Element — "a Basic Card with no elemental
        // tag, or a pure 'true damage' effect" (ELEMENT_RULES.md §3)
        Element? attacker = null;

        foreach (var defender in AllElements)
        {
            // When an elementless attack is resolved against each defending Element
            var matchup = ElementMatchups.Resolve(attacker, defender);

            // Then the matchup is Neutral, 1.00× (§3 item 1) — never Disadvantage,
            // because an absent Element cannot be countered.
            Assert.Equal(ElementMatchup.Neutral, matchup);
            Assert.Equal(1.00, ElementModifiers.Default.For(matchup));
        }
    }

    [Fact]
    public void Resolve_ShouldBeNeutral_WhenDefenderIsElementless()
    {
        // Given a target with no Element — "an attack against an elementless target
        // (if any such target exists)" (ELEMENT_RULES.md §3 item 2)
        Element? defender = null;

        foreach (var attacker in AllElements)
        {
            // When each Element's attack is resolved against it
            var matchup = ElementMatchups.Resolve(attacker, defender);

            // Then the matchup is Neutral, 1.00× (§3 item 2) — never Advantage,
            // because there is no defending Element to counter.
            Assert.Equal(ElementMatchup.Neutral, matchup);
            Assert.Equal(1.00, ElementModifiers.Default.For(matchup));
        }
    }

    [Fact]
    public void Resolve_ShouldBeNeutral_WhenBothSidesAreElementless()
    {
        // Given neither side carries an Element
        // When the matchup is resolved
        var matchup = ElementMatchups.Resolve(null, null);

        // Then it is Neutral: no elemental relationship exists between two absent
        // Elements, which is the same reading as §3 items 1–2 and introduces no
        // relationship §2 does not define. It is never Advantage or Disadvantage,
        // because no countering relationship holds in either direction.
        Assert.Equal(ElementMatchup.Neutral, matchup);
    }

    [Fact]
    public void Resolve_ShouldReturnNoElementlessOutcomeOfItsOwn()
    {
        // §2.1: "A matchup is exactly one of Advantage / Neutral / Disadvantage."
        // Elementless is not a fourth outcome — it resolves to Neutral (§3), which
        // is why no Elementless/Absent member exists on the outcome type.
        Assert.Equal(
            [ElementMatchup.Advantage, ElementMatchup.Neutral, ElementMatchup.Disadvantage],
            Enum.GetValues<ElementMatchup>());
    }

    // ------------------------------------------------------- cycle structure ---

    [Fact]
    public void Counters_ShouldDescribeAClosedFiveElementCycle()
    {
        // ELEMENT_RULES.md §2 (and GAME_RULES.md §8): Mộc → Thổ → Thủy → Hỏa → Kim
        // → Mộc. Each Element counters exactly one other, so following the cycle
        // from any Element returns to it in exactly five steps, having visited
        // every Element once. This is the documented cycle's own invariant and is
        // independent of the code's structure.
        var expectedCycle = new[]
        {
            Element.Moc,
            Element.Tho,
            Element.Thuy,
            Element.Hoa,
            Element.Kim,
        };

        for (var start = 0; start < expectedCycle.Length; start++)
        {
            var visited = new List<Element>();
            var current = expectedCycle[start];

            for (var step = 0; step < 5; step++)
            {
                visited.Add(current);

                var next = AllElements.Single(e => ElementMatchups.Counters(current, e));
                current = next;
            }

            // Five steps return to the start, visiting all five Elements once.
            Assert.Equal(expectedCycle[start], current);
            Assert.Equal(expectedCycle.Length, visited.Distinct().Count());
        }
    }

    [Fact]
    public void Counters_ShouldBeExactlyOneTargetPerElement()
    {
        // §2 lists exactly one relationship per Element, so no Element counters two
        // others and none counters none.
        foreach (var attacker in AllElements)
        {
            var countered = AllElements.Where(d => ElementMatchups.Counters(attacker, d)).ToArray();

            Assert.Single(countered);
        }
    }

    [Fact]
    public void Counters_ShouldNeverBeReflexive()
    {
        // §2 lists no self-relationship: this is what makes the same-element case
        // Neutral rather than Advantage (§2.1).
        foreach (var element in AllElements)
        {
            Assert.False(ElementMatchups.Counters(element, element));
        }
    }

    [Fact]
    public void Resolve_ShouldBeTheInverseOfItselfForCounteringPairs()
    {
        // For a documented countering pair the two directions are Advantage and
        // Disadvantage (ELEMENT_RULES.md §2, §2.1) — the cycle is directional, not
        // symmetric.
        foreach (var attacker in AllElements)
        {
            foreach (var defender in AllElements)
            {
                var forward = ElementMatchups.Resolve(attacker, defender);
                var reverse = ElementMatchups.Resolve(defender, attacker);

                if (forward == ElementMatchup.Advantage)
                {
                    Assert.Equal(ElementMatchup.Disadvantage, reverse);
                }
                else if (forward == ElementMatchup.Disadvantage)
                {
                    Assert.Equal(ElementMatchup.Advantage, reverse);
                }
                else
                {
                    // A Neutral pair is Neutral in both directions: either the two
                    // Elements are equal, or no documented relationship joins them.
                    Assert.Equal(ElementMatchup.Neutral, reverse);
                }
            }
        }
    }

    // --------------------------------------------------- the Element enum ---

    [Fact]
    public void Element_ShouldDeclareExactlyTheFiveDocumentedMembers()
    {
        // ELEMENT_RULES.md §1: Mộc, Hỏa, Thổ, Kim, Thủy — and nothing else.
        // §1.2: "MVP does not support dual/multi element entities", and §7 defers
        // dual-element to a non-MVP expansion.
        var names = Enum.GetNames<Element>();

        Assert.Equal(5, names.Length);
        Assert.Equal(
            ["Hoa", "Kim", "Moc", "Tho", "Thuy"],
            names.OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void Element_ShouldNotDeclareANoneOrElementlessMember()
    {
        // TASK-008 requirement 5: elementless is an absent/null Element input, not
        // a sixth enum member. §1's set is closed and §3 treats elementless as the
        // absence of an Element, so a `None` member would put a non-Element inside
        // §1's set and turn "elementless" into a value an entity can have.
        foreach (var name in Enum.GetNames<Element>())
        {
            Assert.DoesNotContain("none", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("elementless", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("null", name, StringComparison.OrdinalIgnoreCase);
        }

        // And the elementless input is accepted as null on both sides (§3).
        Assert.Equal(ElementMatchup.Neutral, ElementMatchups.Resolve(null, Element.Moc));
        Assert.Equal(ElementMatchup.Neutral, ElementMatchups.Resolve(Element.Moc, null));
        Assert.Equal(ElementMatchup.Neutral, ElementMatchups.Resolve(null, null));
    }

    [Fact]
    public void Element_ShouldNotDeclareAMemberOutsideTheDocumentedSet()
    {
        // The enum's members must be exactly §1's five — no extra Element invented
        // (AGENTS.md §7), and none of the four Gem types leaking in as an Element
        // (MATCH3_RULES.md §1.1: "a Gem type is not an Element").
        var declared = Enum.GetValues<Element>().Select(e => e.ToString()).ToHashSet();

        Assert.Equal(
            new HashSet<string> { "Moc", "Hoa", "Tho", "Kim", "Thuy" },
            declared);
    }

    [Fact]
    public void Resolve_ShouldRejectAnUndocumentedElementValue()
    {
        // ELEMENT_RULES.md §1 defines exactly five Elements. A value outside them is
        // not an Element and has no Tương Khắc relationship, so it is not silently
        // treated as Neutral.
        var undefined = (Element)99;

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ElementMatchups.Resolve(undefined, Element.Moc));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ElementMatchups.Resolve(Element.Moc, undefined));
    }

    // ------------------------------------------------ domain purity (scope) ---

    [Fact]
    public void ElementsModule_ShouldReferenceNoOtherLayerOrDomainEntity()
    {
        // ARCHITECTURE.md §2.1 item 1: Domain has zero dependencies on ASP.NET
        // Core, Redis, PostgreSQL, or SignalR — and TASK-008's stop condition
        // forbids referencing Pet, Boss, Skill, or Effect, whose Element ownership
        // is a separate task.
        var assembly = typeof(Element).Assembly;
        var offenders = new List<string>();

        foreach (var type in assembly.GetTypes().Where(t => t.Namespace == "GameServer.Domain.Elements"))
        {
            foreach (var referenced in ReferencedTypes(type))
            {
                var ns = referenced.Namespace ?? string.Empty;
                var name = referenced.Name;

                var isForeignNamespace =
                    ns.StartsWith("GameServer.Application", StringComparison.Ordinal)
                    || ns.StartsWith("GameServer.Infrastructure", StringComparison.Ordinal)
                    || ns.StartsWith("GameServer.Api", StringComparison.Ordinal)
                    || ns.StartsWith("Microsoft.", StringComparison.Ordinal)
                    || ns.StartsWith("System.Net", StringComparison.Ordinal)
                    || ns.StartsWith("StackExchange", StringComparison.Ordinal)
                    || ns.StartsWith("Npgsql", StringComparison.Ordinal);

                var isEntityBoundary = name is "Pet" or "Boss" or "Skill" or "Effect";

                if (isForeignNamespace || isEntityBoundary)
                {
                    offenders.Add($"{type.FullName} → {referenced.FullName}");
                }
            }
        }

        Assert.Empty(offenders);
    }

    /// <summary>
    /// Every type a type's own public/internal surface mentions — declared members
    /// and the types of their parameters, return values, and fields. Test-only
    /// reflection, mirroring <c>ArchitectureTests</c>' assembly-level checks.
    /// </summary>
    private static IEnumerable<Type> ReferencedTypes(Type type)
    {
        const BindingFlags Flags =
            BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var field in type.GetFields(Flags))
        {
            yield return field.FieldType;
        }

        foreach (var property in type.GetProperties(Flags))
        {
            yield return property.PropertyType;
        }

        foreach (var method in type.GetMethods(Flags))
        {
            yield return method.ReturnType;

            foreach (var parameter in method.GetParameters())
            {
                yield return parameter.ParameterType;
            }
        }
    }
}
