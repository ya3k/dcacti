using GameServer.Application.Players;
using GameServer.Domain.Players;
using GameServer.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GameServer.Infrastructure.Postgres.Repositories;

/// <summary>
/// The EF Core implementation of the Player ownership boundary
/// (<c>DATABASE.md</c> §1) — <c>ARCHITECTURE.md</c> §3's
/// "PersistenceRepository (Postgres)", Infrastructure layer.
///
/// The matching key is the unique <c>DiscordUserId</c> column
/// (<c>DATABASE.md</c> §1, §3). A new Player is created at
/// <see cref="Player.InitialLevel"/> (<c>PET_RULES.md</c> §5 item 8) with a
/// fresh <see cref="Player.CreatedAt"/>.
/// </summary>
public sealed class PlayerRepository : IPlayerRepository
{
    private readonly GameDbContext _dbContext;

    public PlayerRepository(GameDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<Player> GetOrCreateByDiscordUserIdAsync(
        string discordUserId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Players
            .FirstOrDefaultAsync(player => player.DiscordUserId == discordUserId, cancellationToken);

        if (existing is not null)
        {
            // An existing Player is returned as it stands. Its Level is not
            // reset and its CreatedAt is not rewritten: matching a Player is
            // not an update of it (DATABASE.md §3 — a repeated authentication
            // reuses the row and creates no duplicate).
            return existing;
        }

        var created = new Player
        {
            // The Player's identifier is minted here, at creation, and is the
            // value the auth boundary returns as `playerId`
            // (API_CONTRACTS.md §2.5). It is opaque to the client.
            PlayerId = $"player_{Guid.NewGuid():N}",
            DiscordUserId = discordUserId,
            Level = Player.InitialLevel,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Players.Add(created);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return created;
        }
        catch (DbUpdateException ex) when (IsDiscordUserIdUniqueViolation(ex))
        {
            // Two concurrent first-login requests for the same Discord
            // identity can both miss the find above and both attempt the
            // insert. The unique constraint on DiscordUserId is the
            // authoritative protection (DATABASE.md §1, §3), so the loser of
            // that race is rejected by the database rather than allowed to
            // create a second ownership record. The rejected insert is
            // detached and the winner's row is read back.
            //
            // No locking or coordination is introduced for this: the database
            // constraint is the documented mechanism, and it is sufficient.
            _dbContext.Entry(created).State = EntityState.Detached;

            return await _dbContext.Players
                .FirstAsync(player => player.DiscordUserId == discordUserId, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<Player?> GetByIdAsync(
        string playerId,
        CancellationToken cancellationToken = default)
    {
        // A pure read of the persistent account row — used by the Pet Level
        // recompute path (ADR-012 Consequences) to obtain Player.Level.
        // It never creates a Player and never rewrites Level.
        return await _dbContext.Players
            .FirstOrDefaultAsync(player => player.PlayerId == playerId, cancellationToken);
    }

    /// <summary>
    /// Whether a save failure is the <c>DiscordUserId</c> unique-constraint
    /// violation rather than an unrelated database error.
    ///
    /// Only the documented uniqueness constraint is handled: an error of any
    /// other kind propagates, so a real persistence failure is never masked
    /// as a successful match.
    /// </summary>
    private static bool IsDiscordUserIdUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
        && postgres.ConstraintName is not null
        && postgres.ConstraintName.Contains("DiscordUserId", StringComparison.Ordinal);
}
