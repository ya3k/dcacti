using GameServer.Application.Collection;
using GameServer.Domain.Elements;

namespace GameServer.Application.Tests;

/// <summary>
/// The canonical Element wire projection — <c>API_CONTRACTS.md</c> §5.1,
/// §5.2 (TASK-072's value set).
///
/// <code>
/// Moc  → "Wood"
/// Tho  → "Earth"
/// Thuy → "Water"
/// Hoa  → "Fire"
/// Kim  → "Metal"
/// </code>
///
/// <b>What these tests establish.</b> §5.1 binds the collection <c>element</c>
/// member to exactly <c>"Fire" | "Water" | "Earth" | "Wood" | "Metal"</c> and
/// records the Vietnamese Element names (<c>ELEMENT_RULES.md</c> §1: Mộc, Hỏa,
/// Thổ, Kim, Thủy) as display values only — "presentation text, never API wire
/// values". The Domain enum's member names are the Vietnamese-derived ones
/// (<c>Moc</c>, <c>Tho</c>, <c>Thuy</c>, <c>Hoa</c>, <c>Kim</c>), so
/// <c>Element.ToString()</c> would emit a value the contract does not contain.
/// The mapping is therefore asserted pair by pair, including the two trap cases
/// (<c>Moc→Wood</c> and <c>Kim→Metal</c>).
/// </summary>
public class ElementWireValuesTests
{
    /// <summary>
    /// The five documented pairs, spelled from <c>API_CONTRACTS.md</c> §5.1 /
    /// <c>ELEMENT_RULES.md</c> §1 rather than from the implementation.
    /// </summary>
    public static TheoryData<Element, string> DocumentedPairs => new()
    {
        { Element.Moc, "Wood" },
        { Element.Tho, "Earth" },
        { Element.Thuy, "Water" },
        { Element.Hoa, "Fire" },
        { Element.Kim, "Metal" },
    };

    [Theory]
    [MemberData(nameof(DocumentedPairs))]
    public void ToWireValue_ShouldReturnTheDocumentedWireValue(Element element, string expected)
    {
        // §5.1's bound value set, element by element.
        Assert.Equal(expected, ElementWireValues.ToWireValue(element));
    }

    [Theory]
    [MemberData(nameof(DocumentedPairs))]
    public void ToWireValue_ShouldNeverReturnTheDomainEnumMemberName(Element element, string expected)
    {
        // The trap this mapping exists for: the Domain enum's member names are
        // the Vietnamese forms, and §5.1 binds the wire member to the English
        // form for the whole REST surface — §3's battle-start initialState
        // included.
        Assert.NotEqual(element.ToString(), expected);
    }

    [Theory]
    [MemberData(nameof(DocumentedPairs))]
    public void ToWireValue_ShouldBeOneOfTheFiveDocumentedValues(Element element, string _)
    {
        // The set is closed (ELEMENT_RULES.md §1): the response can only ever
        // carry one of these five strings.
        Assert.Contains(
            ElementWireValues.ToWireValue(element),
            new[] { "Fire", "Water", "Earth", "Wood", "Metal" });
    }

    [Fact]
    public void ToWireValue_ShouldCoverEveryDocumentedElement()
    {
        // ELEMENT_RULES.md §1's set is complete and closed — five members, no
        // sixth and no None (the Domain enum states the same). A member the
        // mapping did not cover would throw, so completeness is asserted by
        // mapping every declared value.
        var elements = Enum.GetValues<Element>();

        Assert.Equal(5, elements.Length);

        foreach (var element in elements)
        {
            Assert.False(string.IsNullOrWhiteSpace(ElementWireValues.ToWireValue(element)));
        }
    }

    [Fact]
    public void ToWireValue_ShouldRefuseAValueOutsideTheDocumentedSet()
    {
        // A name outside the closed five-member set is not a value §5.1 defines,
        // so it is refused rather than passed through as its own enum name — no
        // sixth Element is invented (AGENTS.md §7).
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ElementWireValues.ToWireValue((Element)999));
    }
}
