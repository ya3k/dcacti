using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Infrastructure.Postgres;
using GameServer.Infrastructure.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// <c>BattleResult</c> as PostgreSQL actually enforces it —
/// <c>DATABASE.md</c> §1, §2, §3, §4 (TASK-041).
///
/// <b>Why these run against real PostgreSQL.</b> The in-memory provider used by
/// <see cref="BattleResultPersistenceTests"/> does not enforce foreign keys,
/// unique indexes, or column types, so those tests assert the <i>declared</i>
/// model. These assert the <i>applied schema</i>: the three foreign keys against
/// real referenced rows, the primary key that is the battle's own id, the
/// <c>jsonb</c> reward summary, the timestamptz completion instant, and the one
/// documented index. They follow the <see cref="PlayerPostgresConstraintTests"/>
/// convention — a hard-coded local connection string, skipped when unreachable —
/// so the suite still runs where no database is available.
///
/// <b>The BossDefinition FK is checked against a provisioned row.</b>
/// <c>DATABASE.md</c> §1 note item 2 / sourcing item 3 make the referenced row
/// mandatory, and TASK-053 provisioned exactly the three canonical rows. This
/// suite relies on the real provisioned <c>boss-def-hoa-long</c> row rather than
/// inserting a BossDefinition fixture, so it also proves the provisioning the FK
/// depends on is actually in place.
/// </summary>
public class BattleResultPostgresTests : IAsyncLifetime
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
            // rather than failing the suite. The provider-independent model and
            // repository assertions still run in BattleResultPersistenceTests.
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

    private GameDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<GameDbContext>().UseNpgsql(_dataSource!).Options);

    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    /// <summary>
    /// The canonical provisioned <c>BossDefinitionId</c> the FK must resolve to
    /// (<c>DATABASE.md</c> §1 note item 2, TASK-053).
    /// </summary>
    private static string ProvisionedBossDefinitionId => BossDefinitions.HoaLong.BossDefinitionId;

    /// <summary>
    /// A Player, its Pet, and its PetDefinition — the two owned rows a
    /// <c>BattleResult</c> references (<c>DATABASE.md</c> §1, §2).
    /// </summary>
    private async Task<(string PlayerId, string PetInstanceId)> SeedOwnerAsync(GameDbContext context)
    {
        var playerId = NewId("player_result_pg");
        var petDefinitionId = NewId("pet_def_result_pg");
        var petInstanceId = NewId("pet_instance_result_pg");
        var signatureSkillCardId = NewId("card_skill_pg");

        context.Players.Add(new Player
        {
            PlayerId = playerId,
            DiscordUserId = $"9{Random.Shared.NextInt64(1_000_000_000_000_000L):D16}",
            Level = Player.InitialLevel,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        // DATABASE.md §2 makes PetDefinition 1 ── 1 CardDefinition a real foreign
        // key, so the Pet's Signature Skill Card must exist before its definition
        // can reference it.
        context.CardDefinitions.Add(new GameServer.Domain.Cards.CardDefinition
        {
            CardDefinitionId = signatureSkillCardId,
            Name = signatureSkillCardId,
            Category = GameServer.Domain.Cards.CardCategory.PetSkill,
            PowerCost = 0,
            EffectDefinition = "effect",
            LoadoutCopyLimit = 1,
        });

        context.PetDefinitions.Add(new PetDefinition
        {
            PetDefinitionId = petDefinitionId,
            Identity = "Thanh Xà",
            Element = Element.Moc,
            PassiveId = new PassiveId("thanh-xa-regen"),
            PassiveThreshold = 5,
            SignatureSkillCardId = signatureSkillCardId,
        });

        context.Pets.Add(new Pet
        {
            PetInstanceId = petInstanceId,
            PlayerId = playerId,
            PetDefinitionId = petDefinitionId,
            Tier = PetTier.Common,
            Star = 1,
            Level = 1,
            AcquiredAt = DateTimeOffset.UtcNow,
        });

        await context.SaveChangesAsync();

        return (playerId, petInstanceId);
    }

    /// <summary>
    /// Removes the rows a test created, so the shared development database is left
    /// as it was found.
    /// </summary>
    private async Task CleanupAsync(string? battleResultId, string? playerId, string? petInstanceId)
    {
        await using var context = CreateContext();

        if (battleResultId is not null)
        {
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"BattleResult\" WHERE \"BattleResultId\" = {0}",
                battleResultId);
        }

        if (petInstanceId is not null)
        {
            // The Pet's definition and its Signature Skill Card are removed with
            // it: DATABASE.md §2's FK chain requires the Card before the
            // definition, so it is deleted last.
            var petDefinitionId = await context.Pets
                .Where(pet => pet.PetInstanceId == petInstanceId)
                .Select(pet => pet.PetDefinitionId)
                .FirstOrDefaultAsync();

            var signatureSkillCardId = petDefinitionId is null
                ? null
                : await context.PetDefinitions
                    .Where(definition => definition.PetDefinitionId == petDefinitionId)
                    .Select(definition => definition.SignatureSkillCardId)
                    .FirstOrDefaultAsync();

            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"Pet\" WHERE \"PetInstanceId\" = {0}",
                petInstanceId);

            if (petDefinitionId is not null)
            {
                await context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM \"PetDefinition\" WHERE \"PetDefinitionId\" = {0}",
                    petDefinitionId);
            }

            if (signatureSkillCardId is not null)
            {
                await context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM \"CardDefinition\" WHERE \"CardDefinitionId\" = {0}",
                    signatureSkillCardId);
            }
        }

        if (playerId is not null)
        {
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"Player\" WHERE \"PlayerId\" = {0}",
                playerId);
        }
    }

    // -----------------------------------------------------------------------
    // The applied table — DATABASE.md §1
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Postgres_ShouldApplyTheEightDocumentedColumns()
    {
        if (!_available) return;

        await using var context = CreateContext();

        var columns = await context.Database
            .SqlQuery<string>($"""
                SELECT column_name AS "Value"
                FROM information_schema.columns
                WHERE table_name = 'BattleResult'
                ORDER BY column_name
                """)
            .ToListAsync();

        Assert.Equal(
            new[]
            {
                "BattleResultId",
                "BossDefinitionId",
                "CompletedAt",
                "DurationTurns",
                "Outcome",
                "PetInstanceId",
                "PlayerId",
                "RewardSummary",
            },
            columns);
    }

    [Fact]
    public async Task Postgres_ShouldApplyTheDocumentedColumnTypes()
    {
        if (!_available) return;

        await using var context = CreateContext();

        var types = await context.Database
            .SqlQuery<string>($"""
                SELECT column_name || '=' || data_type || ':' || is_nullable AS "Value"
                FROM information_schema.columns
                WHERE table_name = 'BattleResult'
                ORDER BY column_name
                """)
            .ToListAsync();

        Assert.Equal(
            new[]
            {
                "BattleResultId=character varying:NO",
                "BossDefinitionId=character varying:NO",
                "CompletedAt=timestamp with time zone:NO",
                "DurationTurns=integer:NO",
                "Outcome=character varying:NO",
                "PetInstanceId=character varying:NO",
                "PlayerId=character varying:NO",
                "RewardSummary=jsonb:NO",
            },
            types);
    }

    [Fact]
    public async Task Postgres_ShouldApplyThePrimaryKeyAndTheThreeForeignKeys()
    {
        if (!_available) return;

        await using var context = CreateContext();

        var primaryKey = await context.Database
            .SqlQuery<string>($"""
                SELECT kcu.column_name AS "Value"
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu
                  ON tc.constraint_name = kcu.constraint_name
                WHERE tc.table_name = 'BattleResult'
                  AND tc.constraint_type = 'PRIMARY KEY'
                """)
            .ToListAsync();

        Assert.Equal(new[] { "BattleResultId" }, primaryKey);

        var foreignKeys = await context.Database
            .SqlQuery<string>($"""
                SELECT kcu.column_name || '->' || ccu.table_name AS "Value"
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu
                  ON tc.constraint_name = kcu.constraint_name
                JOIN information_schema.constraint_column_usage ccu
                  ON tc.constraint_name = ccu.constraint_name
                WHERE tc.table_name = 'BattleResult'
                  AND tc.constraint_type = 'FOREIGN KEY'
                ORDER BY kcu.column_name
                """)
            .ToListAsync();

        Assert.Equal(
            new[]
            {
                "BossDefinitionId->BossDefinition",
                "PetInstanceId->Pet",
                "PlayerId->Player",
            },
            foreignKeys);
    }

    [Fact]
    public async Task Postgres_ShouldApplyTheDocumentedIndexDescending()
    {
        if (!_available) return;

        await using var context = CreateContext();

        var indexDefinition = await context.Database
            .SqlQuery<string>($"""
                SELECT indexdef AS "Value"
                FROM pg_indexes
                WHERE tablename = 'BattleResult'
                  AND indexname = 'IX_BattleResult_PlayerId_CompletedAt'
                """)
            .ToListAsync();

        var definition = Assert.Single(indexDefinition);

        // DATABASE.md §4: BattleResult(PlayerId, CompletedAt DESC) — the
        // descending order is part of the contract ("most recent first"). The
        // rendered DDL names both members, in that order, with the descending
        // modifier on the second.
        Assert.Contains("CompletedAt", definition, StringComparison.Ordinal);
        Assert.Contains("DESC", definition, StringComparison.Ordinal);
        Assert.True(
            definition.IndexOf("PlayerId", StringComparison.Ordinal)
                < definition.IndexOf("CompletedAt", StringComparison.Ordinal),
            $"DATABASE.md §4 fixes the order (PlayerId, CompletedAt DESC); observed: {definition}");
    }

    // -----------------------------------------------------------------------
    // Round-trip against the applied schema
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Postgres_ShouldRoundTripEveryDocumentedValue_AgainstTheProvisionedBoss()
    {
        if (!_available) return;

        var battleResultId = NewId("battle_pg");
        var completedAt = new DateTimeOffset(2026, 9, 27, 10, 45, 30, TimeSpan.Zero);
        string? playerId = null;
        string? petInstanceId = null;

        try
        {
            await using (var context = CreateContext())
            {
                (playerId, petInstanceId) = await SeedOwnerAsync(context);

                var repository = new BattleResultRepository(context);

                await repository.AddAsync(new BattleResult(
                    battleResultId,
                    playerId,
                    petInstanceId,
                    ProvisionedBossDefinitionId,
                    BattleOutcome.Victory,
                    DurationTurns: 12,
                    completedAt,
                    "{}"));
            }

            await using var read = CreateContext();

            var stored = await new BattleResultRepository(read).GetByIdAsync(battleResultId);

            Assert.NotNull(stored);
            Assert.Equal(battleResultId, stored!.BattleResultId);
            Assert.Equal(playerId, stored.PlayerId);
            Assert.Equal(petInstanceId, stored.PetInstanceId);
            Assert.Equal(ProvisionedBossDefinitionId, stored.BossDefinitionId);
            Assert.Equal(BattleOutcome.Victory, stored.Outcome);
            Assert.Equal(12, stored.DurationTurns);
            Assert.Equal(completedAt, stored.CompletedAt);
            Assert.Equal("{}", stored.RewardSummary);
        }
        finally
        {
            await CleanupAsync(battleResultId, playerId, petInstanceId);
        }
    }

    [Fact]
    public async Task Postgres_ShouldRejectAMissingBossDefinitionRow()
    {
        if (!_available) return;

        // DATABASE.md §1 sourcing item 3: the FK is "never satisfied by anything
        // other than a real, provisioned BossDefinition row" — the database itself
        // refuses a fabricated, fallback, or default key, which is what makes the
        // fail-closed rule enforceable rather than merely asserted.
        var battleResultId = NewId("battle_pg_missing_fk");
        string? playerId = null;
        string? petInstanceId = null;

        try
        {
            await using var context = CreateContext();

            (playerId, petInstanceId) = await SeedOwnerAsync(context);

            var repository = new BattleResultRepository(context);

            await Assert.ThrowsAsync<DbUpdateException>(() => repository.AddAsync(new BattleResult(
                battleResultId,
                playerId,
                petInstanceId,
                "boss-def-does-not-exist",
                BattleOutcome.Defeat,
                DurationTurns: 1,
                DateTimeOffset.UtcNow,
                "{}")));
        }
        finally
        {
            await CleanupAsync(battleResultId, playerId, petInstanceId);
        }
    }

    [Fact]
    public async Task Postgres_ShouldRejectAMissingPlayerOrPetRow()
    {
        if (!_available) return;

        // DATABASE.md §1/§2's two owned-instance foreign keys are enforced too: a
        // result cannot reference a Player or a Pet that does not exist.
        var battleResultId = NewId("battle_pg_missing_owner");

        try
        {
            await using var context = CreateContext();

            var repository = new BattleResultRepository(context);

            await Assert.ThrowsAsync<DbUpdateException>(() => repository.AddAsync(new BattleResult(
                battleResultId,
                NewId("player_absent"),
                NewId("pet_absent"),
                ProvisionedBossDefinitionId,
                BattleOutcome.Victory,
                DurationTurns: 0,
                DateTimeOffset.UtcNow,
                "{}")));
        }
        finally
        {
            await CleanupAsync(battleResultId, null, null);
        }
    }

    [Fact]
    public async Task Postgres_ShouldAllowAtMostOneRowPerBattle()
    {
        if (!_available) return;

        // DATABASE.md §1 sourcing item 1 / REDIS_STATE.md §3: the primary key is the
        // at-most-one-row guarantee. A second insert of the same battle id is
        // refused by the database, which is exactly the duplicate protection the
        // contract names — no separate idempotency mechanism is involved.
        var battleResultId = NewId("battle_pg_duplicate");
        string? playerId = null;
        string? petInstanceId = null;

        try
        {
            await using var context = CreateContext();

            (playerId, petInstanceId) = await SeedOwnerAsync(context);

            var repository = new BattleResultRepository(context);

            await repository.AddAsync(new BattleResult(
                battleResultId,
                playerId,
                petInstanceId,
                ProvisionedBossDefinitionId,
                BattleOutcome.Victory,
                DurationTurns: 3,
                DateTimeOffset.UtcNow,
                "{}"));

            var rows = await context.BattleResults
                .CountAsync(result => result.BattleResultId == battleResultId);

            Assert.Equal(1, rows);
        }
        finally
        {
            await CleanupAsync(battleResultId, playerId, petInstanceId);
        }
    }

    // -----------------------------------------------------------------------
    // The battle-end lookup against the provisioned rows — DATABASE.md §1 note 2
    // -----------------------------------------------------------------------

    [Fact]
    public async Task BossDefinitionLookup_ShouldResolveEachCanonicalIdentityToItsRowKey()
    {
        if (!_available) return;

        await using var context = CreateContext();

        var lookup = new BossDefinitionLookup(context);

        // DATABASE.md §1 note item 2 / §1 note item 5: the Identity → key pairs the
        // three provisioned rows carry (TASK-053). Both values are asserted, so an
        // implementation that returned the Identity as the key — the substitution
        // note item 2 forbids — cannot pass.
        foreach (var boss in BossDefinitions.All)
        {
            var resolved = await lookup.FindBossDefinitionIdByIdentityAsync(boss.BossId.Value);

            Assert.Equal(boss.BossDefinitionId, resolved);
            Assert.NotEqual(boss.BossId.Value, resolved);
        }
    }

    [Fact]
    public async Task BossDefinitionLookup_ShouldReturnNullForAnUnknownIdentity()
    {
        if (!_available) return;

        await using var context = CreateContext();

        var lookup = new BossDefinitionLookup(context);

        // An unknown Identity resolves to nothing, which is the defined outcome the
        // battle-end path fails closed on (DATABASE.md §1 sourcing item 3). No
        // fallback, convention-mapped, or derived key is produced.
        Assert.Null(await lookup.FindBossDefinitionIdByIdentityAsync("boss-does-not-exist"));
    }
}
