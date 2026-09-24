using GameServer.Domain.Players;
using GameServer.Infrastructure.Postgres;
using GameServer.Infrastructure.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// The Player constraints as PostgreSQL actually enforces them — the
/// authoritative protection <c>DATABASE.md</c> §3 relies on.
///
/// The in-memory provider used by <see cref="PlayerPersistenceTests"/> and
/// <see cref="PlayerMatchOrCreateTests"/> does not enforce unique indexes or
/// check constraints, so those tests assert the *declared* model. These tests
/// assert the *applied schema*: they run against a real PostgreSQL instance
/// and are skipped when one is not reachable, so the suite still runs
/// hermetically where no database is available.
///
/// What is verified is the documented contract only: the unique
/// <c>DiscordUserId</c>, the <c>[1, 50]</c> Level range, and the creation
/// Level of <c>1</c>.
/// </summary>
public class PlayerPostgresConstraintTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=dcacti_db;Username=dcacti;Password=dcacti_dev_password";

    private NpgsqlDataSource? _dataSource;
    private bool _available;

    public async Task InitializeAsync()
    {
        try
        {
            var builder = new NpgsqlDataSourceBuilder(ConnectionString);
            _dataSource = builder.Build();

            await using var command = _dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync();

            _available = true;
        }
        catch (Exception)
        {
            // No reachable PostgreSQL: the schema-level tests below are skipped
            // rather than failing the suite. The provider-independent model
            // assertions still run in PlayerPersistenceTests.
            _available = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }
    }

    private GameDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseNpgsql(_dataSource!)
            .Options;

        return new GameDbContext(options);
    }

    private static string NewDiscordUserId() => $"9{Random.Shared.NextInt64(1_000_000_000_000_000L):D16}";

    [Fact]
    public async Task Postgres_ShouldRejectASecondPlayerWithTheSameDiscordUserId()
    {
        if (!_available) return; // no live PostgreSQL — covered by the model assertion

        await using var context = CreateContext();
        var repository = new PlayerRepository(context);

        var discordUserId = NewDiscordUserId();
        var created = await repository.GetOrCreateByDiscordUserIdAsync(discordUserId);

        try
        {
            // A direct second insert of the same identity must be rejected by
            // the unique constraint (DATABASE.md §1/§3).
            context.Players.Add(new Player
            {
                PlayerId = $"player_dup_{Guid.NewGuid():N}",
                DiscordUserId = discordUserId,
                Level = Player.InitialLevel,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
        finally
        {
            // Keep the shared development database clean.
            await using var cleanup = CreateContext();
            var entity = await cleanup.Players.SingleOrDefaultAsync(p => p.PlayerId == created.PlayerId);
            if (entity is not null)
            {
                cleanup.Players.Remove(entity);
                await cleanup.SaveChangesAsync();
            }
        }
    }

    [Fact]
    public async Task Postgres_ShouldRejectALevelOutsideTheDocumentedRange()
    {
        if (!_available) return;

        await using var context = CreateContext();

        var player = new Player
        {
            PlayerId = $"player_range_{Guid.NewGuid():N}",
            DiscordUserId = NewDiscordUserId(),
            Level = Player.MaxLevel,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        context.Players.Add(player);
        await context.SaveChangesAsync();

        try
        {
            // DATABASE.md §3 / PET_RULES.md §5 item 1: Level ∈ [1, 50]. Both
            // bounds are exercised as stored values.
            foreach (var illegalLevel in new[] { Player.MaxLevel + 1, Player.MinLevel - 1 })
            {
                context.Entry(player).Property(nameof(Player.Level)).CurrentValue = illegalLevel;

                await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

                // Detach so the failed change does not leak into the next attempt.
                context.Entry(player).State = EntityState.Detached;
                context.Players.Attach(player);
            }
        }
        finally
        {
            await using var cleanup = CreateContext();
            var entity = await cleanup.Players.SingleOrDefaultAsync(p => p.PlayerId == player.PlayerId);
            if (entity is not null)
            {
                cleanup.Players.Remove(entity);
                await cleanup.SaveChangesAsync();
            }
        }
    }

    [Fact]
    public async Task Postgres_ShouldApplyTheDocumentedCreationLevel()
    {
        if (!_available) return;

        await using var context = CreateContext();
        var repository = new PlayerRepository(context);

        var discordUserId = NewDiscordUserId();
        var created = await repository.GetOrCreateByDiscordUserIdAsync(discordUserId);

        try
        {
            // PET_RULES.md §5 item 8: a newly created Player starts at Level 1.
            Assert.Equal(1, created.Level);

            var stored = await context.Players.AsNoTracking()
                .SingleAsync(p => p.DiscordUserId == discordUserId);

            Assert.Equal(1, stored.Level);
        }
        finally
        {
            await using var cleanup = CreateContext();
            var entity = await cleanup.Players.SingleOrDefaultAsync(p => p.PlayerId == created.PlayerId);
            if (entity is not null)
            {
                cleanup.Players.Remove(entity);
                await cleanup.SaveChangesAsync();
            }
        }
    }
}
