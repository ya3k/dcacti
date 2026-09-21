namespace GameServer.Domain.Elements;

/// <summary>
/// The five MVP Elements (<c>ELEMENT_RULES.md</c> §1, <c>GAME_RULES.md</c> §8,
/// <c>MVP_SCOPE.md</c> §1).
///
/// <code>
/// Mộc  (Wood)
/// Hỏa  (Fire)
/// Thổ  (Earth)
/// Kim  (Metal)
/// Thủy (Water)
/// </code>
///
/// These five are the complete MVP set and the set is closed: §1 defines exactly
/// these, and §7 defers dual/multi-element entities to a future expansion that
/// must not be implemented without the Rule Change Policy
/// (<c>GAME_RULES.md</c> §20, <c>AGENTS.md</c> §7).
///
/// <b>There is deliberately no <c>None</c> member.</b> Elementless is the
/// <i>absence</i> of an Element, not a sixth Element (§3; <c>COMBAT_RULES.md</c>
/// §1 lists <c>Element</c> among an entity's properties and §5.1 shows an
/// elementless Shield effect — nothing defines it as a member of §1's set). It
/// is represented by a <c>null</c> <see cref="Element"/> reference — see
/// <see cref="ElementMatchups"/>, which accepts <c>null</c> on either side.
/// Adding a member here would make "elementless" a value an entity can *have*
/// rather than *lack*, and would put it inside §1's closed set.
///
/// <b>Member order follows the Tương Khắc cycle</b> (<c>ELEMENT_RULES.md</c>
/// §2, <c>GAME_RULES.md</c> §8): <c>Mộc → Thổ → Thủy → Hỏa → Kim → Mộc</c>, so
/// each member's successor in this list is the Element it counters. The order is
/// a reading aid only — no rule depends on the numeric values, which have no
/// documented meaning and are simply stable identifiers.
///
/// <b>No wire representation is defined.</b> <c>SIGNALR_PROTOCOL.md</c> and
/// <c>API_CONTRACTS.md</c> declare no Element serialization contract, and this
/// task does not introduce one, so no contract-name mapping is declared here.
/// </summary>
public enum Element
{
    /// <summary>Mộc (Wood) — counters Thổ (<c>ELEMENT_RULES.md</c> §1, §2).</summary>
    Moc = 0,

    /// <summary>Thổ (Earth) — counters Thủy (<c>ELEMENT_RULES.md</c> §1, §2).</summary>
    Tho = 1,

    /// <summary>Thủy (Water) — counters Hỏa (<c>ELEMENT_RULES.md</c> §1, §2).</summary>
    Thuy = 2,

    /// <summary>Hỏa (Fire) — counters Kim (<c>ELEMENT_RULES.md</c> §1, §2).</summary>
    Hoa = 3,

    /// <summary>Kim (Metal) — counters Mộc (<c>ELEMENT_RULES.md</c> §1, §2).</summary>
    Kim = 4,
}
