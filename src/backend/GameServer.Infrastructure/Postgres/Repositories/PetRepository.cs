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
/// Reads and writes the owned-instance and definition rows only. It
/// derives no progression value: the retired <c>Player.Level ×
/// PetLevelMultiplier</c> recompute pass was removed
/// (<c>PET_RULES.md</c> §5.6 item 1, ADR-016 item 13), so this boundary
/// neither recomputes <see cref="Pet.Level"/> nor exposes a bulk-save step
/// for one. Pet Level is re-derived from that Pet instance's own XP by
/// <see cref="Pet.GrantBattleXp"/>, and this boundary only stores the values
/// it is given (<c>PET_RULES.md</c> §5.4).
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
    public async Task<bool> SaveProgressionAsync(
        Pet pet,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pet);

        // DATABASE.md §1: the primary-key lookup on PetInstanceId — the same
        // identity GetByIdAsync resolves, so a read and a write through this
        // boundary always describe the same row.
        //
        // Nothing is derived here: the caller applies the documented grant and
        // its hard cap through Pet.GrantBattleXp (PET_RULES.md §5.3-§5.5), and
        // this boundary stores the two progression values it is handed.
        var stored = await _dbContext.Pets
            .FirstOrDefaultAsync(
                instance => instance.PetInstanceId == pet.PetInstanceId,
                cancellationToken);

        // An absent row is reported as absence and nothing is created: the
        // reward updates the Pet that fought, it does not bring a Pet instance
        // into existence (DATABASE.md §1 - Pet creation is not a reward step).
        if (stored is null)
        {
            return false;
        }

        stored.XP = pet.XP;
        stored.Level = pet.Level;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
