using GameServer.Domain.Pets;

namespace GameServer.Application.Pets;

/// <summary>
/// The Pet persistence boundary (<c>DATABASE.md</c> §1–§2).
///
/// It exposes the operations TASK-024 defines: add an owned instance, load
/// instances for recompute, and read definition configuration.
///
/// <b>It is an Application boundary, not a persistence implementation.</b>
/// The interface lives here so the Application layer can orchestrate Pet
/// Level recompute without depending on EF Core; the implementation is an
/// Infrastructure concern (<c>ARCHITECTURE.md</c> §2.1, §3
/// "PersistenceRepository (Postgres) — Infrastructure"). Domain types cross
/// this boundary; persistence types do not (<c>ARCHITECTURE.md</c> §2 item
/// 3).
/// </summary>
public interface IPetRepository
{
    /// <summary>
    /// Persists a new owned Pet instance (<c>DATABASE.md</c> §1).
    ///
    /// The caller is responsible for supplying a <see cref="Pet.Level"/>
    /// already produced by <see cref="PetLevelDerivation.Derive"/> — this
    /// boundary stores the denormalized snapshot; it does not invent one.
    /// </summary>
    /// <param name="pet">The owned instance to persist.</param>
    /// <param name="cancellationToken">Cancels the persistence work.</param>
    Task AddAsync(Pet pet, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every Pet instance owned by <paramref name="playerId"/>
    /// (<c>DATABASE.md</c> §2: Player 1 ── N Pet) — the set the Player
    /// Level recompute path walks.
    /// </summary>
    /// <param name="playerId">The owning Player's identifier.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<Pet>> ListByPlayerIdAsync(
        string playerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every Pet instance that references
    /// <paramref name="petDefinitionId"/> (<c>DATABASE.md</c> §2: Pet N ── 1
    /// PetDefinition) — the set the multiplier recompute path walks.
    /// </summary>
    /// <param name="petDefinitionId">The definition's identifier.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<Pet>> ListByDefinitionIdAsync(
        string petDefinitionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the definition row for <paramref name="petDefinitionId"/>,
    /// or <c>null</c> when no such definition exists.
    ///
    /// The multiplier recompute path reads
    /// <see cref="PetDefinition.PetLevelMultiplier"/> through this lookup.
    /// </summary>
    /// <param name="petDefinitionId">The definition's identifier.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<PetDefinition?> GetDefinitionAsync(
        string petDefinitionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the owned Pet instance identified by
    /// <paramref name="petInstanceId"/>, or <c>null</c> when no such
    /// instance exists.
    ///
    /// The battle-start path resolves the selected <c>petId</c> through this
    /// lookup and checks the returned instance's
    /// <see cref="Pet.PlayerId"/> against the requesting Player
    /// (<c>API_CONTRACTS.md</c> §3: "petId must be owned by the player —
    /// <c>PET_RULES.md</c> §2"). Ownership is established from persistence,
    /// never from a client-supplied claim (<c>GAME_RULES.md</c> §18,
    /// ADR-001).
    ///
    /// It is deliberately an instance lookup and not a Player-scoped one: the
    /// caller must be able to distinguish "no such Pet exists" from "the Pet
    /// exists but belongs to another Player", which a Player-filtered query
    /// would collapse into one absent result. Both are rejected, and the
    /// caller applies that single rejection — this boundary reports only what
    /// the store holds.
    /// </summary>
    /// <param name="petInstanceId">
    /// The owned instance's identifier (<c>DATABASE.md</c> §1:
    /// <c>PetInstanceId</c> (PK)) — the value the battle-start request submits
    /// as <c>petId</c>.
    /// </param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<Pet?> GetByIdAsync(
        string petInstanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists pending entity changes — the save step of a recompute pass
    /// after <see cref="Pet.Level"/> has been rewritten with the derived
    /// value.
    /// </summary>
    /// <param name="cancellationToken">Cancels the save.</param>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
