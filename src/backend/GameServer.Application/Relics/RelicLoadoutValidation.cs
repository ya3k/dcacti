using GameServer.Domain.Relics;

namespace GameServer.Application.Relics;

/// <summary>
/// The outcome of validating a submitted <c>relicLoadout</c>
/// (<c>RELIC_RULES.md</c> §2.1, §2.4, §2.5).
///
/// On success it carries the <b>battle-scoped snapshot</b> — the selected
/// owned Relic instance identities <b>in equip-slot order</b>, so
/// <c>EquippedRelics[i]</c> is slot <c>i + 1</c>
/// (<c>RELIC_RULES.md</c> §2.3, §2.5). On failure it carries the rejection
/// reason and no snapshot, so a caller cannot accidentally equip a rejected
/// selection.
///
/// <b>The snapshot is identity-only and already ordered.</b> It holds
/// <see cref="EquippedRelicIdentity"/> values, never resolved
/// <see cref="RelicDefinition"/> data (<c>RELIC_RULES.md</c> §2.2;
/// <c>GAME_STATE.md</c> §0 item 5), and the order is the submitted request
/// order — never sorted by <c>RelicInstanceId</c>, <c>RelicDefinitionId</c>,
/// <c>AcquiredAt</c>, or database order (<c>RELIC_RULES.md</c> §2.3 item 2).
///
/// <b>It is a value, not a stored record.</b> Producing it writes no
/// ownership row and no equip row; it is the value the battle-creation path
/// copies into <c>PetState.EquippedRelics[]</c>
/// (<c>RELIC_RULES.md</c> §2.5, ADR-012 item 8).
/// </summary>
public readonly record struct RelicLoadoutValidation
{
    private RelicLoadoutValidation(
        bool isValid,
        RelicLoadoutRejectionReason reason,
        EquippedRelicIdentity[] equippedRelics)
    {
        IsValid = isValid;
        Reason = reason;
        EquippedRelics = equippedRelics;
    }

    /// <summary>
    /// Whether the selection satisfied every documented loadout rule.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Why the selection was rejected, or
    /// <see cref="RelicLoadoutRejectionReason.None"/> when it was accepted.
    /// Every non-<c>None</c> value maps to the one documented
    /// <c>INVALID_LOADOUT</c> code (<c>API_CONTRACTS.md</c> §3).
    /// </summary>
    public RelicLoadoutRejectionReason Reason { get; }

    /// <summary>
    /// The battle-scoped snapshot in equip-slot order, or an empty array when
    /// <see cref="IsValid"/> is <c>false</c>.
    /// </summary>
    public EquippedRelicIdentity[] EquippedRelics { get; }

    /// <summary>
    /// A successful validation carrying the ordered snapshot
    /// (<c>RELIC_RULES.md</c> §2.5).
    /// </summary>
    /// <param name="equippedRelics">
    /// The selected instance identities, already in submitted order.
    /// </param>
    public static RelicLoadoutValidation Valid(EquippedRelicIdentity[] equippedRelics) =>
        new(true, RelicLoadoutRejectionReason.None, equippedRelics);

    /// <summary>
    /// A rejected validation carrying no snapshot
    /// (<c>RELIC_RULES.md</c> §2.5: a rejection equips nothing).
    /// </summary>
    /// <param name="reason">The documented rule the selection violated.</param>
    public static RelicLoadoutValidation Invalid(RelicLoadoutRejectionReason reason) =>
        new(false, reason, []);
}
