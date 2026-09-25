using GameServer.Domain.Cards;

namespace GameServer.Application.Cards;

/// <summary>
/// The Card persistence boundary (<c>DATABASE.md</c> §1–§2).
///
/// It exposes the operations TASK-028 defines: add a definition row, add a
/// Player's unlock row, and resolve requested definition identities to the
/// definitions the requesting Player has actually unlocked.
///
/// <b>It is an Application boundary, not a persistence implementation.</b> The
/// interface lives here so the Application layer can validate a loadout without
/// depending on EF Core; the implementation is an Infrastructure concern
/// (<c>ARCHITECTURE.md</c> §2.1, §3 "PersistenceRepository (Postgres) —
/// Infrastructure"). Domain types cross this boundary; persistence types do not
/// (<c>ARCHITECTURE.md</c> §2 item 3).
///
/// <b>It exposes no equip write.</b> There is no "set loadout" operation,
/// because Card equipment is battle-scoped and is never persisted
/// (<c>DATABASE.md</c> §2: "Battle equip of Cards is not persisted here";
/// ADR-012 item 10). A battle-scoped snapshot is written only into
/// <c>PetState.EquippedCards[]</c> (<c>CARD_RULES.md</c> §1), never back
/// through this boundary.
///
/// <b>It is not an inventory boundary.</b> Cards are unlock flags, not owned
/// copies (ADR-012 item 9), so there is deliberately no quantity read, no
/// "decrement", and no per-copy lookup: one unlock row per (Player,
/// Definition) is the whole ownership model (<c>CARD_RULES.md</c> §1 item 3).
/// </summary>
public interface ICardRepository
{
    /// <summary>
    /// Persists a new static content definition row (<c>DATABASE.md</c> §1).
    /// </summary>
    /// <param name="definition">The definition to persist.</param>
    /// <param name="cancellationToken">Cancels the persistence work.</param>
    Task AddDefinitionAsync(
        CardDefinition definition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a Player's unlock of a definition (<c>DATABASE.md</c> §1–§2,
    /// ADR-012 item 9).
    /// </summary>
    /// <param name="unlockedCard">The unlock row to persist.</param>
    /// <param name="cancellationToken">Cancels the persistence work.</param>
    Task AddUnlockAsync(
        PlayerUnlockedCard unlockedCard,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every Card definition the Player has unlocked
    /// (<c>DATABASE.md</c> §2: Player 1 ── N PlayerUnlockedCard), served by
    /// the documented <c>PlayerUnlockedCard(PlayerId)</c> index (§4).
    /// </summary>
    /// <param name="playerId">The owning Player's identifier.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<CardDefinition>> ListUnlockedAsync(
        string playerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the definitions matching
    /// <paramref name="cardDefinitionIds"/> that the requesting Player has
    /// unlocked — the ownership read the loadout validator performs
    /// (<c>API_CONTRACTS.md</c> §3 step 2).
    ///
    /// The Player filter is part of the operation, not a convenience: a
    /// definition the Player has not unlocked must never be returned, because
    /// the server must establish ownership from persistence rather than from a
    /// client-supplied claim (<c>GAME_RULES.md</c> §18, ADR-001).
    ///
    /// <b>Repeats are meaningful and must be preserved by the caller.</b>
    /// A submitted loadout may legitimately name the same definition more than
    /// once (<c>CARD_RULES.md</c> §1 item 1), so this read returns each
    /// matching definition <b>once</b> — it is a definition lookup, not a
    /// positional match. The caller resolves each submitted entry against the
    /// returned set; it must never treat this list's length as the loadout
    /// length.
    ///
    /// <b>The result is not an ordered contract.</b> No rule reads Card array
    /// positions (<c>GAME_STATE.md</c> §2.3), and callers must not depend on
    /// the order of the returned rows.
    /// </summary>
    /// <param name="playerId">The owning Player's identifier.</param>
    /// <param name="cardDefinitionIds">The requested definition identities.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<CardDefinition>> ListUnlockedDefinitionsAsync(
        string playerId,
        IReadOnlyCollection<string> cardDefinitionIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the definition row for
    /// <paramref name="cardDefinitionId"/>, or <c>null</c> when no such
    /// definition exists.
    ///
    /// It is an unrestricted content read — it is <b>not</b> an ownership
    /// check. Resolving the active Pet's derived Signature Skill Card uses it,
    /// because that Card is derived from the Pet's definition and is not
    /// subject to the Player's unlock of the submitted Basic Cards
    /// (<c>CARD_RULES.md</c> §1 item 4).
    /// </summary>
    /// <param name="cardDefinitionId">The definition's identifier.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<CardDefinition?> GetDefinitionAsync(
        string cardDefinitionId,
        CancellationToken cancellationToken = default);
}
