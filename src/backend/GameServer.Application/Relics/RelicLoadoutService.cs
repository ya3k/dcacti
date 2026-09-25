using GameServer.Domain.Relics;

namespace GameServer.Application.Relics;

/// <summary>
/// Validates a battle's Relic loadout and produces the battle-scoped snapshot
/// (<c>RELIC_RULES.md</c> §2.1–§2.5, <c>API_CONTRACTS.md</c> §3).
///
/// <code>
/// Player-owned Relic instances
///         ↓
///   relicLoadout (request order)
///         ↓
///   1. count        3–5            RELIC_RULES.md §2.1 item 1
///   2. ownership    each instance owned by the Player   §2.1 item 2
///   3. distinctness no repeated RelicInstanceId         §2.4 items 1–2
///         ↓
///   preserve request order          §2.3
///         ↓
///   EquippedRelicIdentity[]         §2.2, §2.5
/// </code>
///
/// <b>Server-authoritative.</b> Ownership is established from persistence via
/// <see cref="IRelicRepository"/>, never from a client-supplied ownership
/// claim (<c>GAME_RULES.md</c> §18, ADR-001). A caller supplies only the
/// requesting <c>PlayerId</c> and the selected instance identities; it never
/// supplies the resulting snapshot.
///
/// <b>What this service deliberately does not do.</b> It computes no Relic
/// effect, evaluates no trigger, resolves no stacking, touches no combat
/// value, mutates no persistent ownership row, and creates no equipment
/// record (<c>RELIC_RULES.md</c> §4–§5 are out of scope; ADR-012 item 7
/// forbids a persistent equip table). It returns a value; the caller copies
/// it into <c>PetState.EquippedRelics[]</c> at battle start
/// (<c>RELIC_RULES.md</c> §2.5).
///
/// <b>It is a read-only pass.</b> Nothing here writes to the database — no
/// ownership change, no equip row — so the snapshot cannot alter the Player's
/// collection (ADR-012 item 8).
/// </summary>
public sealed class RelicLoadoutService
{
    /// <summary>
    /// The lowest number of Relics a loadout may contain
    /// (<c>RELIC_RULES.md</c> §2.1 item 1, <c>GAME_RULES.md</c> §13.3,
    /// <c>GAME_STATE.md</c> §2.3: "3–5").
    /// </summary>
    public const int MinLoadoutSize = 3;

    /// <summary>
    /// The highest number of Relics a loadout may contain
    /// (<c>RELIC_RULES.md</c> §2.1 item 1, <c>GAME_RULES.md</c> §13.3,
    /// <c>GAME_STATE.md</c> §2.3: "3–5").
    /// </summary>
    public const int MaxLoadoutSize = 5;

    private readonly IRelicRepository _relics;

    /// <summary>
    /// Creates the loadout validator over the Relic persistence boundary.
    /// </summary>
    /// <param name="relics">The Relic persistence boundary (ownership source).</param>
    public RelicLoadoutService(IRelicRepository relics)
    {
        _relics = relics;
    }

    /// <summary>
    /// Validates <paramref name="relicInstanceIds"/> as the loadout for
    /// <paramref name="playerId"/> and returns the ordered snapshot.
    ///
    /// The returned snapshot preserves the submitted order exactly: element
    /// <c>i</c> is equip slot <c>i + 1</c> (<c>RELIC_RULES.md</c> §2.3). The
    /// selection is never sorted, and the persistence read's own order is
    /// never used — the result is reconstructed from the request sequence.
    ///
    /// Two distinct instances that reference the same
    /// <see cref="Relic.RelicDefinitionId"/> are <b>accepted</b>: the rule
    /// constrains instance identity, never definition identity
    /// (<c>RELIC_RULES.md</c> §2.4 item 3).
    /// </summary>
    /// <param name="playerId">
    /// The requesting Player — the owner every selected instance must belong
    /// to and the subject the snapshot is scoped to.
    /// </param>
    /// <param name="relicInstanceIds">
    /// The selected owned Relic instance identities, in the order the client
    /// submitted them. That order <b>is</b> the equip-slot order.
    /// </param>
    /// <param name="cancellationToken">Cancels the ownership read.</param>
    /// <returns>
    /// A valid result carrying the ordered snapshot, or an invalid result
    /// naming the first documented rule the selection violated.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="playerId"/> is null, empty, or whitespace. The
    /// requesting identity is required: without it ownership cannot be
    /// established, and defaulting it would be a client-authoritative claim
    /// (<c>GAME_RULES.md</c> §18).
    /// </exception>
    public async Task<RelicLoadoutValidation> ValidateAsync(
        string playerId,
        IReadOnlyList<string> relicInstanceIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);
        ArgumentNullException.ThrowIfNull(relicInstanceIds);

        // RELIC_RULES.md §2.1 item 1 / §2.4 item 4: the documented 3–5 bound
        // counts the ELEMENTS of relicLoadout. A duplicate is rejected below
        // rather than collapsed, so an out-of-range count is not rescued by
        // de-duplication.
        if (relicInstanceIds.Count < MinLoadoutSize ||
            relicInstanceIds.Count > MaxLoadoutSize)
        {
            return RelicLoadoutValidation.Invalid(
                RelicLoadoutRejectionReason.CountOutOfRange);
        }

        // RELIC_RULES.md §2.4 items 1–2: the selected RelicInstanceId values
        // must be pairwise distinct — one instance occupies at most one slot.
        // Checked before ownership so a duplicate is reported as such rather
        // than as a spurious ownership failure, and so the ownership read
        // below cannot be handed a repeated id.
        if (HasDuplicate(relicInstanceIds))
        {
            return RelicLoadoutValidation.Invalid(
                RelicLoadoutRejectionReason.DuplicateInstance);
        }

        // RELIC_RULES.md §2.1 item 2: every selected instance must be owned by
        // the requesting Player. Ownership is read from persistence, filtered
        // by PlayerId at the boundary, so an instance belonging to another
        // Player is simply absent from the result (GAME_RULES.md §18,
        // ADR-001). No per-instance definition lookup is required: acceptance
        // constrains instance identity, not definition identity (§2.4 item 3).
        var ownedInstances = await _relics.ListOwnedInstancesAsync(
            playerId,
            relicInstanceIds,
            cancellationToken);

        if (ownedInstances.Count != relicInstanceIds.Count)
        {
            return RelicLoadoutValidation.Invalid(
                RelicLoadoutRejectionReason.NotOwned);
        }

        // RELIC_RULES.md §2.3: slot index = submitted array position + 1, so
        // the snapshot is the request sequence itself. The owned-instance
        // query's row order is deliberately NOT used: the result is rebuilt
        // from relicInstanceIds in its original order, which is what keeps the
        // equip order authoritative and makes any repository ordering
        // irrelevant (§2.3 item 2 forbids sorting by RelicInstanceId,
        // RelicDefinitionId, AcquiredAt, or database order).
        var snapshot = new EquippedRelicIdentity[relicInstanceIds.Count];

        for (var slotIndex = 0; slotIndex < relicInstanceIds.Count; slotIndex++)
        {
            snapshot[slotIndex] = new EquippedRelicIdentity(relicInstanceIds[slotIndex]);
        }

        return RelicLoadoutValidation.Valid(snapshot);
    }

    /// <summary>
    /// Whether any identity appears more than once in the selection
    /// (<c>RELIC_RULES.md</c> §2.4 items 1–2).
    ///
    /// The comparison is ordinal, matching how instance identities are stored
    /// and read (<c>DATABASE.md</c> §1 <c>RelicInstanceId</c> is an opaque
    /// identifier, not a culture-sensitive string).
    /// </summary>
    private static bool HasDuplicate(IReadOnlyList<string> relicInstanceIds)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var relicInstanceId in relicInstanceIds)
        {
            if (!seen.Add(relicInstanceId))
            {
                return true;
            }
        }

        return false;
    }
}
