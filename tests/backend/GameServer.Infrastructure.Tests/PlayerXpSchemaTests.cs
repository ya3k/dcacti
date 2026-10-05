using GameServer.Domain.Players;
using GameServer.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// The Player XP schema as the applied migration actually produced it —
/// <c>DATABASE.md</c> §1/§3, <c>COMBAT_RULES.md</c> §7.
///
/// <b>Why this suite exists.</b> The model-level tests in
/// <see cref="PlayerPersistenceTests"/> assert the <i>declared</i> model, which is
/// what the migration is generated from. These assert the <i>applied schema</i> in
/// a real PostgreSQL database, so the Migrations/ directory is proven to have
/// actually been applied rather than merely written — the documented
/// <c>dotnet ef database update</c> step.
///
/// Following the <see cref="PlayerPostgresConstraintTests"/> convention, it is
/// skipped when no database is reachable.
/// </summary>
public class PlayerXpSchemaTests : IAsyncLifetime
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

    [Fact]
    public async Task AppliedSchema_ShouldHaveTheXpColumn_NonNullable_DefaultingToZero()
    {
        if (!_available) return;

        // DATABASE.md §1/§3: Player.XP is int, NOT NULL, default 0. Asserted
        // against the database's own catalogue, so a migration that was written but
        // never applied fails here.
        await using var command = _dataSource!.CreateCommand(
            """
            SELECT data_type, is_nullable, column_default
            FROM information_schema.columns
            WHERE table_name = 'Player' AND column_name = 'XP'
            """);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync(), "The applied schema has no Player.XP column.");

        Assert.Equal("integer", reader.GetString(0));
        Assert.Equal("NO", reader.GetString(1));
        Assert.Contains("0", reader.GetString(2));
    }

    [Fact]
    public async Task AppliedSchema_ShouldCarryTheDocumentedXpAndLevelConstraints()
    {
        if (!_available) return;

        // DATABASE.md §3: Player.XP >= 0 with NO upper bound, and
        // Player.Level ∈ [1, 50]. Both check constraints are asserted by name and
        // definition, so neither can be dropped or widened silently.
        await using var command = _dataSource!.CreateCommand(
            """
            SELECT conname, pg_get_constraintdef(oid)
            FROM pg_constraint
            WHERE conrelid = '"Player"'::regclass AND contype = 'c'
            """);

        var constraints = new Dictionary<string, string>(StringComparer.Ordinal);

        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                constraints[reader.GetString(0)] = reader.GetString(1);
            }
        }

        Assert.True(constraints.ContainsKey("CK_Player_XP_NonNegative"));
        Assert.True(constraints.ContainsKey("CK_Player_Level_Range"));

        // Only a lower bound on XP: COMBAT_RULES.md §7.5 item 1 keeps XP uncapped,
        // so no ceiling (and not the Pet track's 4900) may appear.
        Assert.Contains("\"XP\" >= 0", constraints["CK_Player_XP_NonNegative"]);
        Assert.DoesNotContain("4900", constraints["CK_Player_XP_NonNegative"]);

        Assert.Contains("\"Level\" >= 1", constraints["CK_Player_Level_Range"]);
        Assert.Contains("\"Level\" <= 50", constraints["CK_Player_Level_Range"]);
    }

    [Fact]
    public async Task AppliedSchema_ShouldHaveReceivedTheAddPlayerXpMigration()
    {
        if (!_available) return;

        // The documented apply step is `dotnet ef database update`, which records
        // the applied migration in __EFMigrationsHistory. Its presence is what
        // proves the shipped migration — not a hand-built schema — produced the
        // column above.
        await using var command = _dataSource!.CreateCommand(
            """
            SELECT COUNT(*) FROM "__EFMigrationsHistory"
            WHERE "MigrationId" LIKE '%_AddPlayerXp'
            """);

        var applied = (long)(await command.ExecuteScalarAsync())!;

        Assert.Equal(1, applied);
    }

    [Fact]
    public async Task AppliedSchema_ShouldCarryPlayerXpOnNoTableOtherThanPlayerAndPet()
    {
        if (!_available) return;

        // TASK-065 §17/§24: Player XP is persistent Player progression, and this
        // task adds no XP column anywhere else — not to any battle-state or result
        // table, and not to Pet.
        //
        // TASK-067 later gave Pet its own XP column (DATABASE.md §1/§3,
        // PET_RULES.md §5.1): a SEPARATE per-instance progression pool with its own
        // hard cap, deliberately independent of this one (ADR-016 item 12). The
        // assertion is therefore updated rather than deleted — the documented set of
        // XP-carrying tables is now exactly {Player, Pet}, so any OTHER table
        // gaining an XP column still fails here.
        await using var command = _dataSource!.CreateCommand(
            """
            SELECT table_name FROM information_schema.columns
            WHERE column_name = 'XP' AND table_name NOT IN ('Player', 'Pet')
            ORDER BY table_name
            """);

        var tables = new List<string>();

        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        Assert.Empty(tables);
    }

    [Fact]
    public async Task AppliedSchema_ShouldKeepThePetLevelMultiplierColumnRemoved()
    {
        if (!_available) return;

        // TASK-064 retired PetLevelMultiplier and this task must not resurrect it
        // (ADR-016 item 13). Asserted against the applied schema rather than the
        // source alone, so a stray migration could not quietly restore it.
        await using var command = _dataSource!.CreateCommand(
            """
            SELECT COUNT(*) FROM information_schema.columns
            WHERE table_name = 'PetDefinition' AND column_name = 'PetLevelMultiplier'
            """);

        var present = (long)(await command.ExecuteScalarAsync())!;

        Assert.Equal(0, present);
    }

    [Fact]
    public async Task AppliedSchema_ShouldStoreAndReloadAnUncappedXpValue()
    {
        if (!_available) return;

        // The end-to-end round trip through the applied schema: COMBAT_RULES.md
        // §7.5 item 1 keeps XP uncapped, so a value past the Level-50 boundary is
        // stored verbatim while the Level constraint still holds.
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseNpgsql(_dataSource!)
            .Options;

        var playerId = $"player_xp_schema_{Guid.NewGuid():N}";

        var accountId = Guid.NewGuid();

        await using (var context = new GameDbContext(options))
        {
            context.Accounts.Add(new Domain.Accounts.Account
            {
                AccountId = accountId,
                Username = $"u_{accountId:N}"[..20],
                PasswordHash = "hash",
                CreatedAt = DateTimeOffset.UtcNow,
            });

            context.Players.Add(new Player
            {
                PlayerId = playerId,
                AccountId = accountId,
                XP = 12_345,
                Level = Player.LevelForXp(12_345),
                CreatedAt = DateTimeOffset.UtcNow,
            });

            await context.SaveChangesAsync();
        }

        try
        {
            await using var reload = new GameDbContext(options);

            var stored = await reload.Players.AsNoTracking()
                .SingleAsync(player => player.PlayerId == playerId);

            Assert.Equal(12_345, stored.XP);
            Assert.Equal(Player.MaxLevel, stored.Level);
        }
        finally
        {
            await using var cleanup = new GameDbContext(options);

            var entity = await cleanup.Players
                .SingleOrDefaultAsync(player => player.PlayerId == playerId);

            if (entity is not null)
            {
                cleanup.Players.Remove(entity);
            }

            var acc = await cleanup.Accounts
                .SingleOrDefaultAsync(a => a.AccountId == accountId);

            if (acc is not null)
            {
                cleanup.Accounts.Remove(acc);
            }

            await cleanup.SaveChangesAsync();
        }
    }
}
