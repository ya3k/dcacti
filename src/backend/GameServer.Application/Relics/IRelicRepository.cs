using GameServer.Domain.Relics;

namespace GameServer.Application.Relics;

/// <summary>
/// The Relic persistence boundary (<c>DATABASE.md</c> §1–§2).
///
/// It exposes the operations TASK-027 defines: add a definition row, add an
/// owned instance, and resolve a set of requested instance identities to the
/// rows the requesting Player actually owns.
///
/// <b>It is an Application boundary, not a persistence implementation.</b>
/// The interface lives here so the Application layer can validate a loadout
/// without depending on EF Core; the implementation is an Infrastructure
/// concern (<c>ARCHITECTURE.md</c> §2.1, §3 "PersistenceRepository (Postgres)
/// — Infrastructure"). Domain types cross this boundary; persistence types do
/// not (<c>ARCHITECTURE.md</c> §2 item 3).
///
/// <b>It exposes no equip write.</b> There is no "set loadout" operation,
/// because equipment is battle-scoped and is never persisted
/// (<c>DATABASE.md</c> §2: "There is likewise no persistent Relic-equip
/// table"; ADR-012 item 7). A battle-scoped snapshot is written only into
/// <c>PetState.EquippedRelics[]</c> (<c>RELIC_RULES.md</c> §2.5), never back
/// through this boundary.
/// </summary>
public interface IRelicRepository
{
    /// <summary>
    /// Persists a new owned Relic instance row (<c>DATABASE.md</c> §1).
    /// </summary>
    /// <param name="relic">The owned instance to persist.</param>
    /// <param name="cancellationToken">Cancels the persistence work.</param>
    Task AddAsync(Relic relic, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new static content definition row (<c>DATABASE.md</c> §1).
    /// </summary>
    /// <param name="definition">The definition to persist.</param>
    /// <param name="cancellationToken">Cancels the persistence work.</param>
    Task AddDefinitionAsync(
        RelicDefinition definition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every Relic instance owned by <paramref name="playerId"/>
    /// (<c>DATABASE.md</c> §2: Player 1 ── N Relic), served by the documented
    /// <c>Relic(PlayerId)</c> index (§4).
    /// </summary>
    /// <param name="playerId">The owning Player's identifier.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<Relic>> ListByPlayerIdAsync(
        string playerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the owned instances matching <paramref name="relicInstanceIds"/>
    /// <b>for the requesting Player only</b> — the ownership read the loadout
    /// validator performs (<c>RELIC_RULES.md</c> §2.1 item 2).
    ///
    /// The Player filter is part of the operation, not a convenience: an
    /// instance owned by another Player must never be returned, because the
    /// server must establish ownership from persistence rather than from a
    /// client-supplied claim (<c>GAME_RULES.md</c> §18, ADR-001).
    ///
    /// <b>The result is not an ordered contract.</b> Callers must not depend
    /// on the order of the returned rows: equip slot order is the submitted
    /// request order (<c>RELIC_RULES.md</c> §2.3), and sorting by
    /// <c>RelicInstanceId</c>, <c>RelicDefinitionId</c>, <c>AcquiredAt</c>, or
    /// database order is explicitly prohibited (§2.3 item 2).
    /// </summary>
    /// <param name="playerId">The owning Player's identifier.</param>
    /// <param name="relicInstanceIds">The requested instance identities.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<Relic>> ListOwnedInstancesAsync(
        string playerId,
        IReadOnlyCollection<string> relicInstanceIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the definition row for
    /// <paramref name="relicDefinitionId"/>, or <c>null</c> when no such
    /// definition exists.
    /// </summary>
    /// <param name="relicDefinitionId">The definition's identifier.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<RelicDefinition?> GetDefinitionAsync(
        string relicDefinitionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the definition rows matching
    /// <paramref name="relicDefinitionIds"/> — the bulk content read the
    /// collection projection uses (<c>API_CONTRACTS.md</c> §5.4: <c>name</c> is
    /// <c>RelicDefinition.Name</c>).
    ///
    /// <b>It exists so a collection read is not an N+1 read.</b> A list
    /// response needs one definition per owned instance, so resolving them one
    /// <see cref="GetDefinitionAsync"/> call at a time would issue one query per
    /// Relic. This read resolves the whole set in one query against the existing
    /// <c>RelicDefinition</c> table (<c>DATABASE.md</c> §1); it introduces no new
    /// index, no cache, and no read model.
    ///
    /// <b>It is a content read, not an ownership filter.</b> A Relic's
    /// definition is shared static content, not Player-owned data
    /// (<c>DATABASE.md</c> §2: Relic N ── 1 RelicDefinition), so the ownership
    /// scoping of the collection happens on the instance read
    /// (<see cref="ListByPlayerIdAsync"/>), not here.
    ///
    /// <b>A definition that does not exist is simply absent from the result.</b>
    /// No placeholder row is fabricated (<c>AGENTS.md</c> §7).
    ///
    /// <b>The result is not an ordered contract.</b>
    /// </summary>
    /// <param name="relicDefinitionIds">The requested definition identities.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<RelicDefinition>> ListDefinitionsAsync(
        IReadOnlyCollection<string> relicDefinitionIds,
        CancellationToken cancellationToken = default);
}