using GameServer.Domain.Cards;

namespace GameServer.Application.Cards;

/// <summary>
/// The outcome of validating a submitted <c>cardLoadout</c>
/// (<c>CARD_RULES.md</c> §1; <c>API_CONTRACTS.md</c> §3).
///
/// On success it carries the <b>battle-scoped snapshot</b> — exactly 4
/// <see cref="EquippedCardIdentity"/> values: the 3 submitted Basic Cards plus
/// the active Pet's derived Signature Skill Card. On failure it carries the
/// rejection reason and <b>no snapshot</b>, so a caller cannot accidentally
/// equip a rejected selection or persist a partially valid one
/// (<c>API_CONTRACTS.md</c> §3: "A rejected request equips nothing and writes
/// no battle state").
///
/// <b>The snapshot is definition-identity-only.</b> It holds
/// <see cref="EquippedCardIdentity"/> values, never resolved
/// <see cref="CardDefinition"/> data (<c>GAME_STATE.md</c> §2.3, §0 item 5).
/// Repeated identities are the documented same-definition repetition permitted
/// by that definition's <c>LoadoutCopyLimit</c> (<c>CARD_RULES.md</c> §1
/// item 1) — they are <b>not</b> separate instances, so nothing here is ever
/// de-duplicated.
///
/// <b>It is a value, not a stored record.</b> Producing it writes no unlock
/// row and no equip row; it is the value the battle-creation path copies into
/// <c>PetState.EquippedCards[]</c> (<c>DATABASE.md</c> §2, ADR-012 item 10).
/// </summary>
public readonly record struct CardLoadoutValidation
{
    private CardLoadoutValidation(
        bool isValid,
        CardLoadoutRejectionReason reason,
        EquippedCardIdentity[] equippedCards)
    {
        IsValid = isValid;
        Reason = reason;
        EquippedCards = equippedCards;
    }

    /// <summary>
    /// Whether the selection satisfied every documented loadout rule.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Why the selection was rejected, or
    /// <see cref="CardLoadoutRejectionReason.None"/> when it was accepted.
    /// Every non-<c>None</c> value maps to the one documented
    /// <c>INVALID_LOADOUT</c> code (<c>API_CONTRACTS.md</c> §3).
    /// </summary>
    public CardLoadoutRejectionReason Reason { get; }

    /// <summary>
    /// The battle-scoped snapshot — 3 submitted Basics plus the derived
    /// Signature Skill — or an empty array when <see cref="IsValid"/> is
    /// <c>false</c>.
    /// </summary>
    public EquippedCardIdentity[] EquippedCards { get; }

    /// <summary>
    /// A successful validation carrying the snapshot (<c>CARD_RULES.md</c> §1).
    /// </summary>
    /// <param name="equippedCards">
    /// The 4 equipped Card definition identities, in submitted order followed
    /// by the derived Signature Skill. Order carries no gameplay significance
    /// (<c>GAME_STATE.md</c> §2.3); it is the reading order of the snapshot.
    /// </param>
    public static CardLoadoutValidation Valid(EquippedCardIdentity[] equippedCards) =>
        new(true, CardLoadoutRejectionReason.None, equippedCards);

    /// <summary>
    /// A rejected validation carrying no snapshot
    /// (<c>API_CONTRACTS.md</c> §3: a rejection equips nothing).
    /// </summary>
    /// <param name="reason">The documented rule the selection violated.</param>
    public static CardLoadoutValidation Invalid(CardLoadoutRejectionReason reason) =>
        new(false, reason, []);
}
