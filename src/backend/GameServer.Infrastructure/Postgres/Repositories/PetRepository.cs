using GameServer.Application.Pets;
using GameServer.Domain.Pets;
using GameServer.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Infrastructure.Postgres.Repositories;

/// <summary>
/// The EF Core implementation of the Pet persistence boundary
/// (<c>DATABASE.md</c> §1–§2) — <c>ARCHITECTURE.md</c> §3's
/// "PersistenceRepository (Postgres)", Infrastructure layer.
///
/// Reads and writes the owned-instance and definition rows only; derivation
/// of <see cref="Pet.Level"/> is a Domain concern
/// (<see cref="PetLevelDerivation"/>) invoked by the Application recompute
/// hook, never here.
/// </summary>
public sealed class PetRepository : IPetRepository
{
    private readonly GameDbContext _dbContext;

    public PetRepository(GameDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task AddAsync(Pet pet, CancellationToken cancellationToken = default)
    {
        _dbContext.Pets.Add(pet);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Pet>> ListByPlayerIdAsync(
        string playerId,
        CancellationToken cancellationToken = default)
    {
        // DATABASE.md §4: the Pet(PlayerId) index serves this lookup —
        // "list a player's Pets".
        return await _dbContext.Pets
            .Where(pet => pet.PlayerId == playerId)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Pet>> ListByDefinitionIdAsync(
        string petDefinitionId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Pets
            .Where(pet => pet.PetDefinitionId == petDefinitionId)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PetDefinition?> GetDefinitionAsync(
        string petDefinitionId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PetDefinitions
            .FirstOrDefaultAsync(
                definition => definition.PetDefinitionId == petDefinitionId,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Pet?> GetByIdAsync(
        string petInstanceId,
        CancellationToken cancellationToken = default)
    {
        // DATABASE.md §1: the primary-key lookup on PetInstanceId. The battle
        // start path resolves the submitted `petId` here and compares the
        // returned instance's PlayerId itself, so this read is deliberately
        // NOT filtered by PlayerId (API_CONTRACTS.md §3).
        return await _dbContext.Pets
            .FirstOrDefaultAsync(
                pet => pet.PetInstanceId == petInstanceId,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
