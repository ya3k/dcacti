using GameServer.Application.Relics;
using GameServer.Domain.Relics;
using GameServer.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Infrastructure.Postgres.Repositories;

/// <summary>
/// The EF Core implementation of the Relic persistence boundary
/// (<c>DATABASE.md</c> §1–§2, §4) — <c>ARCHITECTURE.md</c> §3's
/// "PersistenceRepository (Postgres)", Infrastructure layer.
///
/// Reads and writes ownership rows and static content only. It exposes no
/// equip write: equipment is battle-scoped and is never persisted
/// (<c>DATABASE.md</c> §2, ADR-012 item 7; <c>RELIC_RULES.md</c> §2.5).
/// </summary>
public sealed class RelicRepository : IRelicRepository
{
    private readonly GameDbContext _dbContext;

    public RelicRepository(GameDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task AddAsync(Relic relic, CancellationToken cancellationToken = default)
    {
        _dbContext.Relics.Add(relic);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddDefinitionAsync(
        RelicDefinition definition,
        CancellationToken cancellationToken = default)
    {
        _dbContext.RelicDefinitions.Add(definition);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Relic>> ListByPlayerIdAsync(
        string playerId,
        CancellationToken cancellationToken = default)
    {
        // DATABASE.md §4: the Relic(PlayerId) index serves this lookup —
        // "list a player's Relics".
        return await _dbContext.Relics
            .Where(relic => relic.PlayerId == playerId)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Relic>> ListOwnedInstancesAsync(
        string playerId,
        IReadOnlyCollection<string> relicInstanceIds,
        CancellationToken cancellationToken = default)
    {
        // RELIC_RULES.md §2.1 item 2: ownership is established from
        // persistence and is filtered by the requesting Player at the query
        // itself. Filtering in the predicate — rather than fetching by id and
        // checking the owner afterwards — means an instance belonging to
        // another Player is never materialized for this Player at all
        // (GAME_RULES.md §18, ADR-001).
        //
        // No ordering is applied or promised: equip slot order is the
        // submitted request order and is reconstructed by the caller from that
        // sequence (RELIC_RULES.md §2.3, §2.3 item 2). The caller must not
        // depend on this result's order.
        return await _dbContext.Relics
            .Where(relic => relic.PlayerId == playerId)
            .Where(relic => relicInstanceIds.Contains(relic.RelicInstanceId))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RelicDefinition?> GetDefinitionAsync(
        string relicDefinitionId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.RelicDefinitions
            .FirstOrDefaultAsync(
                definition => definition.RelicDefinitionId == relicDefinitionId,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RelicDefinition>> ListDefinitionsAsync(
        IReadOnlyCollection<string> relicDefinitionIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(relicDefinitionIds);

        // One query for the whole set, so a collection projection with N owned
        // instances issues two reads rather than N+1. This is a content read on
        // the existing RelicDefinition table (DATABASE.md §1): no new index, no
        // cache, and no read model is introduced.
        //
        // A requested id with no row is simply absent from the result — no
        // placeholder definition is fabricated here (AGENTS.md §7).
        return await _dbContext.RelicDefinitions
            .Where(definition => relicDefinitionIds.Contains(definition.RelicDefinitionId))
            .ToListAsync(cancellationToken);
    }
}
