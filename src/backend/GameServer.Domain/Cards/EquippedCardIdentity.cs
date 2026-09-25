namespace GameServer.Domain.Cards;

/// <summary>
/// The identity of one battle-equipped Card — a <c>CardDefinitionId</c>, the
/// value <c>PetState.EquippedCards[]</c> carries (<c>GAME_STATE.md</c> §2.3;
/// <c>API_CONTRACTS.md</c> §3).
///
/// <b>It is a definition identity, not an instance identity.</b> §2.3 states
/// each element is one <c>CardDefinitionId</c> and records it as the
/// <b>contrast</b> of <c>EquippedRelics[]</c>, whose element is an owned
/// instance identity: "There are no Card instances (ADR-012 item 9): if the
/// same <c>CardDefinitionId</c> appears more than once, the repeated elements
/// are that same definition repeated … and never separate owned or persistent
/// entities". A repeated value is therefore <b>not</b> a copy of anything — it
/// is the same definition selected again, permitted up to its
/// <see cref="CardDefinition.LoadoutCopyLimit"/>.
///
/// <b>There is no slot, copy index, or quantity.</b> §2.3 states element order
/// carries no gameplay significance — "no rule reads card array positions,
/// unlike <c>EquippedRelics[]</c> … whose order is the equip slot order". No
/// <c>CardSlot</c>, <c>CardSlotIndex</c>, <c>BasicCardSlot</c>, <c>copyIndex</c>,
/// or quantity is defined by any document, so none is introduced here and this
/// type carries only the identity.
///
/// <b>Why a value type and not a bare `string`.</b> The value travels into
/// authoritative battle state alongside <c>EquippedRelicIdentity</c>, which it
/// otherwise resembles closely, and the two must not be interchangeable: this
/// one names static content and that one names an owned copy. Naming the type
/// makes passing a Relic instance id where a Card definition id belongs a
/// compile error, following the existing <c>PassiveId</c>, <c>BossId</c>, and
/// <c>EquippedRelicIdentity</c> pattern.
///
/// No document defines an id format, scheme, or validation rule — the contract
/// defines the field's meaning, not its spelling — so this type imposes none
/// and holds the identifier verbatim.
/// </summary>
/// <param name="Value">
/// The definition's identifier, exactly as
/// <see cref="CardDefinition.CardDefinitionId"/> holds it
/// (<c>DATABASE.md</c> §1) and exactly as the submitted loadout named it. It is
/// reported and never re-derived, re-numbered, or invented by a reader.
/// </param>
public readonly record struct EquippedCardIdentity(string Value)
{
    /// <summary>
    /// The documented identity, for diagnostics and for a caller that has one.
    /// </summary>
    public override string ToString() => Value;
}
