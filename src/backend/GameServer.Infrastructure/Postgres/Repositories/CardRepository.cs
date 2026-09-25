using GameServer.Application.Cards;
using GameServer.Domain.Cards;
using GameServer.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Infrastructure.Postgres.Repositories;

/// <summary>
/// The EF Core implementation of the Card persistence boundary
/// (<c>DATABASE.md</c> §1–§2, §4) — <c>ARCHITECTURE.md</c> §3's
/// "PersistenceRepository (Postgres)", Infrastructure layer.
///
/// Reads and writes unlock rows and static content only. It exposes no equip
/// write: Card equipment is battle-scoped and is never persisted
/// (<c>DATABASE.md</c> §2, ADR-012 item 10). It is likewise not an inventory
/// boundary — no quantity is read, written, or decremented
/// (ADR-012 item 9).
/// </summary>
public sealed class CardRepository : ICardRepository
{
    private readonly GameDbContext _dbContext;

    public CardRepository(GameDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task AddDefinitionAsync(
        CardDefinition definition,
        CancellationToken cancellationToken = default)
    {
        _dbContext.CardDefinitions.Add(definition);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddUnlockAsync(
        PlayerUnlockedCard unlockedCard,
        CancellationToken cancellationToken = default)
    {
        _dbContext.PlayerUnlockedCards.Add(unlockedCard);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CardDefinition>> ListUnlockedAsync(
        string playerId,
        CancellationToken cancellationToken = default)
    {
        // DATABASE.md §4: the PlayerUnlockedCard(PlayerId) index serves this
        // lookup — "list a player's unlocked Cards". The join reads the
        // definitions the Player's unlock rows reference (DATABASE.md §2:
        // PlayerUnlockedCard N ── 1 CardDefinition).
        return await _dbContext.PlayerUnlockedCards
            .Where(unlockedCard => unlockedCard.PlayerId == playerId)
            .Join(
                _dbContext.CardDefinitions,
                unlockedCard => unlockedCard.CardDefinitionId,
                definition => definition.CardDefinitionId,
                (_, definition) => definition)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CardDefinition>> ListUnlockedDefinitionsAsync(
        string playerId,
        IReadOnlyCollection<string> cardDefinitionIds,
        CancellationToken cancellationToken = default)
    {
        // API_CONTRACTS.md §3 step 2: ownership is established from persistence
        // and is filtered by the requesting Player at the query itself.
        // Filtering in the predicate — rather than resolving definitions by id
        // and checking the owner afterwards — means a Card this Player has not
        // unlocked is never materialized for this Player at all
        // (GAME_RULES.md §18, ADR-001).
        //
        // This is a DEFINITION lookup, deliberately not positional: several
        // submitted entries may name one definition (CARD_RULES.md §1 item 1),
        // so each matching definition is returned once and the validator
        // resolves repeats itself by counting occurrences.
        //
        // No ordering is applied or promised: no rule reads Card array
        // positions (GAME_STATE.md §2.3). The caller must not depend on this
        // result's order.
        return await _dbContext.PlayerUnlockedCards
            .Where(unlockedCard => unlockedCard.PlayerId == playerId)
            .Where(unlockedCard => cardDefinitionIds.Contains(unlockedCard.CardDefinitionId))
            .Join(
                _dbContext.CardDefinitions,
                unlockedCard => unlockedCard.CardDefinitionId,
                definition => definition.CardDefinitionId,
                (_, definition) => definition)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CardDefinition?> GetDefinitionAsync(
        string cardDefinitionId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.CardDefinitions
            .FirstOrDefaultAsync(
                definition => definition.CardDefinitionId == cardDefinitionId,
                cancellationToken);
    }
}
