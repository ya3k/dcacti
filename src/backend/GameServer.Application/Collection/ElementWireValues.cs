using GameServer.Domain.Elements;

namespace GameServer.Application.Collection;

/// <summary>
/// The canonical REST wire value of a <see cref="Element"/>
/// (<c>API_CONTRACTS.md</c> §5.1, §5.2 — the value set fixed by TASK-072).
///
/// <code>
/// Moc  → "Wood"
/// Tho  → "Earth"
/// Thuy → "Water"
/// Hoa  → "Fire"
/// Kim  → "Metal"
/// </code>
///
/// <b>Why an explicit mapping and not <c>Element.ToString()</c>.</b>
/// <c>Element</c>'s members are named after the Vietnamese Element names
/// (<c>ELEMENT_RULES.md</c> §1: Mộc, Hỏa, Thổ, Kim, Thủy) because that is the
/// vocabulary the game-design document owns, so the enum's own member names
/// (<c>Moc</c>, <c>Tho</c>, <c>Thuy</c>, <c>Hoa</c>, <c>Kim</c>) are <b>not</b>
/// the wire values. §5.1 binds <c>element</c> to exactly
/// <c>"Fire" | "Water" | "Earth" | "Wood" | "Metal"</c> and records the
/// Vietnamese forms as display values only — "presentation text, never API wire
/// values". Serializing the enum member name would therefore emit a value the
/// contract does not contain.
///
/// <b>This is a wire projection, not a rules change.</b> It redefines no
/// Element, adds no member to the five-element set, and changes no modifier,
/// matchup, or cycle: it spells for the wire what <c>ELEMENT_RULES.md</c> §1
/// already states in English beside each Vietnamese name. The Element rules
/// document is untouched.
///
/// <b>It is total over the documented set.</b> The five members are the
/// complete and closed MVP Element set (<c>ELEMENT_RULES.md</c> §1), so a value
/// outside them is a defect rather than a sixth Element — it is refused instead
/// of being passed through as its own enum name.
///
/// <b>The battle-start REST summary uses this; the Redis record deliberately does
/// not.</b> <c>POST /api/battle/start</c>'s <c>initialState.petState.element</c> /
/// <c>initialState.bossState.element</c> are REST body members, so §3 binds them
/// to the set above and they are projected through this type.
/// <c>BattleStateSerializer</c>, by contrast, spells an Element with
/// <c>Element.ToString()</c> for the runtime/Redis record — a different contract
/// (<c>GAME_STATE.md</c> §2, <c>REDIS_STATE.md</c> §2) whose Element encoding is
/// unbound and intentionally independent of the REST wire set. The two
/// representations are not converged, and are not required to be.
/// </summary>
public static class ElementWireValues
{
    /// <summary>
    /// The documented wire value of <paramref name="element"/>
    /// (<c>API_CONTRACTS.md</c> §5.1).
    /// </summary>
    /// <param name="element">
    /// The Pet definition's Element (<c>ELEMENT_RULES.md</c> §1) — exactly one
    /// of the five MVP Elements.
    /// </param>
    /// <returns>One of <c>"Fire"</c>, <c>"Water"</c>, <c>"Earth"</c>, <c>"Wood"</c>, <c>"Metal"</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is not one of the five documented Elements. It is refused
    /// rather than emitted, because a name outside the set is not a value §5.1
    /// defines.
    /// </exception>
    public static string ToWireValue(Element element) => element switch
    {
        Element.Moc => "Wood",
        Element.Tho => "Earth",
        Element.Thuy => "Water",
        Element.Hoa => "Fire",
        Element.Kim => "Metal",
        _ => throw new ArgumentOutOfRangeException(
            nameof(element),
            element,
            "Not one of the five documented Elements (ELEMENT_RULES.md §1)."),
    };
}
