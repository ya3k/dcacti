using GameServer.Application.Players;
using GameServer.Domain.Cards;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Domain.Relics;
using GameServer.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GameServer.Infrastructure.Postgres.Repositories;

/// <summary>
/// The EF Core implementation of the Player ownership boundary
/// (<c>DATABASE.md</c> §1, <c>ADR-020</c>).
/// </summary>
public sealed class PlayerRepository : IPlayerRepository
{
    private readonly GameDbContext _dbContext;

    public PlayerRepository(GameDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<Player> GetOrCreateForAccountAsync(
        Guid accountId,
        Func<CancellationToken, Task<PlayerStarterGrant>> composeStarterGrant,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(composeStarterGrant);

        var existing = await _dbContext.Players
            .FirstOrDefaultAsync(player => player.AccountId == accountId, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        Domain.Accounts.Account? stagedAccount = null;
        var accountExists = await _dbContext.Accounts
            .AnyAsync(a => a.AccountId == accountId, cancellationToken);

        if (!accountExists)
        {
            stagedAccount = new Domain.Accounts.Account
            {
                AccountId = accountId,
                Username = $"user_{accountId:N}"[..24],
                PasswordHash = "placeholder_hash",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            _dbContext.Accounts.Add(stagedAccount);
        }

        var created = new Player
        {
            PlayerId = $"player_{Guid.NewGuid():N}",
            AccountId = accountId,
            XP = Player.InitialXp,
            Level = Player.InitialLevel,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var starterGrant = await composeStarterGrant(cancellationToken).ConfigureAwait(false);
        var stagedStarter = StageStarterOwnership(created.PlayerId, starterGrant);

        _dbContext.Players.Add(created);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return created;
        }
        catch (DbUpdateException ex) when (IsAccountIdUniqueViolation(ex))
        {
            DiscardStagedBatch(created, stagedStarter);
            if (stagedAccount is not null)
            {
                _dbContext.Entry(stagedAccount).State = EntityState.Detached;
            }

            return await _dbContext.Players
                .FirstAsync(player => player.AccountId == accountId, cancellationToken);
        }
        catch (Exception)
        {
            DiscardStagedBatch(created, stagedStarter);
            if (stagedAccount is not null)
            {
                _dbContext.Entry(stagedAccount).State = EntityState.Detached;
            }
            throw;
        }
    }

    /// <summary>
    /// Test-compatibility bridge for string identifiers (ADR-020).
    /// </summary>
    [Obsolete("Use GetOrCreateForAccountAsync")]
    public Task<Player> GetOrCreateByDiscordUserIdAsync(
        string discordUserId,
        Func<CancellationToken, Task<PlayerStarterGrant>> composeStarterGrant,
        CancellationToken cancellationToken = default)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(discordUserId));
        var accountId = new Guid(bytes);
        return GetOrCreateForAccountAsync(accountId, composeStarterGrant, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Player?> GetByAccountIdAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Players
            .FirstOrDefaultAsync(player => player.AccountId == accountId, cancellationToken);
    }

    /// <summary>
    /// Stages the starter ownership set for <paramref name="playerId"/> in the
    /// change tracker without committing it.
    ///
    /// It stages exactly what the composition carried — one row per granted Pet
    /// instance, per unlocked Card, and per owned Relic instance
    /// (<c>DATABASE.md</c> §2 item 1) — and invents none of its own: the counts
    /// are the composition's, never a literal here.
    /// </summary>
    private List<object> StageStarterOwnership(string playerId, PlayerStarterGrant starterGrant)
    {
        var staged = new List<object>();

        foreach (var ownedPet in starterGrant.StarterPets)
        {
            var pet = new Pet
            {
                PetInstanceId = ownedPet.PetInstanceId,
                PlayerId = playerId,
                PetDefinitionId = ownedPet.PetDefinitionId,
                Tier = ownedPet.Tier,
                Star = ownedPet.Star,
                XP = ownedPet.XP,
                Level = ownedPet.Level,
                AcquiredAt = ownedPet.AcquiredAt,
            };

            _dbContext.Pets.Add(pet);
            staged.Add(pet);
        }

        foreach (var unlockedCard in starterGrant.StarterCards)
        {
            var card = new PlayerUnlockedCard
            {
                PlayerId = playerId,
                CardDefinitionId = unlockedCard.CardDefinitionId,
            };

            _dbContext.PlayerUnlockedCards.Add(card);
            staged.Add(card);
        }

        foreach (var ownedRelic in starterGrant.StarterRelics)
        {
            var relic = new Relic
            {
                RelicInstanceId = ownedRelic.RelicInstanceId,
                PlayerId = playerId,
                RelicDefinitionId = ownedRelic.RelicDefinitionId,
                AcquiredAt = ownedRelic.AcquiredAt,
            };

            _dbContext.Relics.Add(relic);
            staged.Add(relic);
        }

        return staged;
    }

    private void DiscardStagedBatch(Player created, List<object> stagedStarterOwnership)
    {
        _dbContext.Entry(created).State = EntityState.Detached;

        foreach (var staged in stagedStarterOwnership)
        {
            _dbContext.Entry(staged).State = EntityState.Detached;
        }
    }

    /// <inheritdoc />
    public async Task<Player?> GetByIdAsync(
        string playerId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Players
            .FirstOrDefaultAsync(player => player.PlayerId == playerId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> SaveProgressionAsync(
        Player player,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(player);

        var entry = _dbContext.Entry(player);

        if (entry.State == EntityState.Detached)
        {
            var stored = await _dbContext.Players
                .FirstOrDefaultAsync(
                    candidate => candidate.PlayerId == player.PlayerId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (stored is null)
            {
                return false;
            }

            stored.XP = player.XP;
            stored.Level = player.Level;
        }
        else if (entry.State == EntityState.Unchanged && _dbContext.Players.Local.Contains(player))
        {
            return true;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static bool IsAccountIdUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
        && postgres.ConstraintName is not null
        && (postgres.ConstraintName.Contains("AccountId", StringComparison.OrdinalIgnoreCase)
            || postgres.ConstraintName.Equals("PK_Account", StringComparison.OrdinalIgnoreCase));
}
