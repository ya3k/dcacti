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
        Func<CancellationToken, Task<PlayerStarterGrant>> composeStarterGrant,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(composeStarterGrant);

        var existing = await _dbContext.Players
            .FirstOrDefaultAsync(player => player.DiscordUserId == discordUserId, cancellationToken);

        if (existing is not null)
        {
            // An existing Player is returned as it stands. Its Level is not
            // reset and its CreatedAt is not rewritten: matching a Player is
            // not an update of it (DATABASE.md §3 — a repeated authentication
            // reuses the row and creates no duplicate).
            //
            // No starter step is reached here at all, and the composition
            // callback is deliberately not invoked: the starter ownership set is
            // bound to creation (DATABASE.md §2 item 3), so an existing Player —
            // including one created before this bootstrap existed — receives
            // none by authenticating. Nothing is probed to decide that; this
            // branch is the whole rule.
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

        // DATABASE.md §2 item 1/§2 item 4: the starter ownership set is staged
        // beside the Player row and committed through the single SaveChangesAsync
        // below. All four entity types share this one scoped GameDbContext, so
        // that one save is one database transaction: the Player and its seven
        // ownership rows persist together or not at all.
        //
        // The composition runs here — on the creation branch, and only here —
        // and is bound to the identifier just minted. A composition failure
        // (a provisioned definition row is missing) throws before anything is
        // added to the change tracker, so no partial starter state and no Player
        // row can result from it (DATABASE.md §2 item 4).
        //
        // This is the combined commit-scope surface DATABASE.md §2 item 4
        // records. It is not a new abstraction: the existing boundary simply
        // stops committing each set independently, so nothing here can leave a
        // Player with partial ownership.
        var starterGrant = await composeStarterGrant(cancellationToken).ConfigureAwait(false);
        var stagedStarter = StageStarterOwnership(created.PlayerId, starterGrant);

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
            // The WHOLE staged batch is discarded, not only the Player entity
            // (DATABASE.md §2 item 4). The seven ownership entities carry a
            // PlayerId foreign key to a Player row that was never written; had
            // they stayed Added in the change tracker, a later SaveChangesAsync
            // in the same scope would have attempted inserts whose FK target
            // does not exist — and the loser would have retained a starter set
            // belonging to no Player.
            //
            // No locking or coordination is introduced for this: the database
            // constraint is the documented mechanism, and it is sufficient.
            DiscardStagedBatch(created, stagedStarter);

            return await _dbContext.Players
                .FirstAsync(player => player.DiscordUserId == discordUserId, cancellationToken);
        }
    }

    /// <summary>
    /// Stages the starter ownership set for <paramref name="playerId"/> in the
    /// change tracker without committing it — the "stage all entities" half of
    /// the single-commit bootstrap (<c>DATABASE.md</c> §2 item 4).
    ///
    /// The rows are re-parented to <paramref name="playerId"/> here because the
    /// identifier is minted by this creation path: the Application layer builds
    /// the composition, but only this boundary knows the row's key. Re-parenting
    /// is a plain assignment on the Domain instances' <c>required init</c>
    /// members only if they are constructed here — they are not, so the
    /// ownership rows are rebuilt against the minted identifier, which keeps
    /// every FK pointing at the Player actually being inserted.
    /// </summary>
    private List<object> StageStarterOwnership(string playerId, PlayerStarterGrant starterGrant)
    {
        // The one starter Pet (DATABASE.md §2 item 1). Every documented creation
        // value was set by the composition; only the owner is bound here.
        var pet = new Pet
        {
            PetInstanceId = starterGrant.StarterPet.PetInstanceId,
            PlayerId = playerId,
            PetDefinitionId = starterGrant.StarterPet.PetDefinitionId,
            Tier = starterGrant.StarterPet.Tier,
            Star = starterGrant.StarterPet.Star,
            XP = starterGrant.StarterPet.XP,
            Level = starterGrant.StarterPet.Level,
            AcquiredAt = starterGrant.StarterPet.AcquiredAt,
        };

        _dbContext.Pets.Add(pet);

        var staged = new List<object> { pet };

        // The three Card unlock rows (DATABASE.md §2 item 1). Ownership is an
        // unlock flag with exactly two columns (ADR-012 item 9), so there is no
        // value here beyond the owner and the definition.
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

        // The three owned Relic instances (DATABASE.md §2 item 1). Their
        // instance identities were minted by the composition and stay distinct
        // from the definition ids (RELIC_RULES.md §2.2).
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

    /// <summary>
    /// Detaches every entity the losing race attempt staged, so no ownership
    /// row outlives the rejected Player insert (<c>DATABASE.md</c> §2 item 4).
    ///
    /// Detaching the Player alone is <b>not</b> sufficient: the seven ownership
    /// entities would remain <c>Added</c>, each carrying a foreign key to a
    /// Player row that does not exist, and any later <c>SaveChangesAsync</c> in
    /// the same scope would try to insert them.
    /// </summary>
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
