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
            // COMBAT_RULES.md §7.5 item 3 / DATABASE.md §3: a newly created
            // Player has XP = 0 and Level = 1. The XP initial value is written
            // explicitly alongside the Level so the two documented initial
            // values are set by the same creation path that owns the row; both
            // are the domain constants that define them.
            XP = Player.InitialXp,
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
        // A pure read of the persistent account row. It never creates a Player
        // and never rewrites a value. Its caller — the battle-end reward path
        // (COMBAT_RULES.md §7.2) — loads the tracked row, applies the
        // documented grant, and stores the result through
        // SaveProgressionAsync below, so the read and the write describe one
        // tracked instance.
        return await _dbContext.Players
            .FirstOrDefaultAsync(player => player.PlayerId == playerId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> SaveProgressionAsync(
        Player player,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(player);

        // DATABASE.md §1: the row is identified by its primary key. The
        // instance handed in is normally already tracked (it came from
        // GetByIdAsync in the same scope), so the changes are flushed by the
        // SaveChanges below. When it is not tracked — a caller that built its
        // own instance — the stored row is loaded and only the two documented
        // progression columns are copied, so an update can never rewrite the
        // identity, the Discord link, or the creation timestamp.
        var entry = _dbContext.Entry(player);

        if (entry.State == EntityState.Detached)
        {
            var stored = await _dbContext.Players
                .FirstOrDefaultAsync(
                    candidate => candidate.PlayerId == player.PlayerId,
                    cancellationToken)
                .ConfigureAwait(false);

            // No Player exists for that identifier. Nothing is created and
            // nothing is written: a reward must not bring a Player into
            // existence (the creation path owns that, DATABASE.md §1).
            if (stored is null)
            {
                return false;
            }

            stored.XP = player.XP;
            stored.Level = player.Level;
        }
        else if (entry.State == EntityState.Unchanged && _dbContext.Players.Local.Contains(player))
        {
            // A tracked, unmodified instance: nothing changed, so there is
            // nothing to write. Reported as a completed save rather than as a
            // missing row.
            return true;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
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
