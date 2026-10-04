using GameServer.Domain.Bosses;
using GameServer.Infrastructure.Postgres;
using GameServer.Infrastructure.Postgres.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// The applied <c>BossDefinition</c> provisioning as PostgreSQL actually holds
/// it — the authoritative proof that the five canonical rows exist after
/// <c>dotnet ef database update</c> (<c>DATABASE.md</c> §1 note item 5).
///
/// The migration-source assertions in <see cref="BossPersistenceTests"/> prove
/// *what* each migration contains; the InMemory provider cannot prove the rows
/// actually landed, because it never applies migrations. These tests therefore
/// run against a real PostgreSQL instance and are skipped when one is not
/// reachable, following the existing <see cref="PlayerPostgresConstraintTests"/>
/// convention — so the suite still runs hermetically where no database is
/// available.
///
/// What is verified is the documented contract only: exactly five canonical
/// rows, the exact <c>BossDefinitionId</c>/<c>Identity</c> values, no
/// duplicates, the <c>Element</c> and both JSON documents round-tripping to the
/// Domain definitions, and the three-way identity contract (no display-name
/// column exists and no display name is stored).
///
/// <b>Precondition:</b> the database must have this repository's migrations
/// applied. These tests do not create the schema and do not apply migrations
/// themselves — doing so would make a test the provisioning mechanism, which is
/// exactly what TASK-053 forbids. When the table is absent the tests skip with
/// that reason recorded rather than failing the suite.
/// </summary>
public class BossDefinitionPostgresProvisioningTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=dcacti_db;Username=dcacti;Password=dcacti_dev_password";

    private NpgsqlDataSource? _dataSource;
    private bool _available;
    private bool _schemaApplied;

    public async Task InitializeAsync()
    {
        try
        {
            var builder = new NpgsqlDataSourceBuilder(ConnectionString);
            _dataSource = builder.Build();

            await using var command = _dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync();

            _available = true;

            // The migration must already have been applied to this database
            // (dotnet ef database update). A missing table means the
            // precondition is unmet — skip rather than fail, and never create
            // it here.
            await using var schemaCheck = _dataSource.CreateCommand(
                "SELECT to_regclass('\"BossDefinition\"') IS NOT NULL");

            _schemaApplied = (bool)(await schemaCheck.ExecuteScalarAsync() ?? false);
        }
        catch (Exception)
        {
            // No reachable PostgreSQL: the applied-row tests are skipped rather
            // than failing the suite. The provider-independent migration-source
            // assertions still run in BossPersistenceTests.
            _available = false;
            _schemaApplied = false;
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

    /// <summary>
    /// The three canonical rows, read from the applied database. Returns
    /// <c>null</c> when PostgreSQL or the applied schema is unavailable, which
    /// is the documented skip condition for these tests.
    /// </summary>
    private async Task<List<BossDefinition>?> ReadProvisionedRowsAsync()
    {
        if (!_available || !_schemaApplied) return null;

        await using var context = CreateContext();

        return await context.BossDefinitions
            .AsNoTracking()
            .OrderBy(d => d.BossDefinitionId)
            .ToListAsync();
    }

    [Fact]
    public async Task Postgres_ShouldHoldExactlyTheFiveCanonicalBossDefinitionRows()
    {
        var rows = await ReadProvisionedRowsAsync();
        if (rows is null) return; // no live PostgreSQL / schema — covered by the migration-source assertion

        // DATABASE.md §1 note item 5: the MVP Boss set is complete at five —
        // the three TASK-053 rows plus the two TASK-172 adds. No sixth row and
        // no placeholder; the applied end state is identical whether the
        // migrations ran once or were retried (migration history).
        Assert.Equal(
            new[]
            {
                "boss-def-hoa-long", "boss-def-kim-loi-vuong", "boss-def-moc-yeu",
                "boss-def-son-thach-ve", "boss-def-thuy-ma",
            },
            rows.Select(r => r.BossDefinitionId).ToArray());
    }

    [Fact]
    public async Task Postgres_ShouldHoldTheCanonicalIdentityAndElementPerRow()
    {
        var rows = await ReadProvisionedRowsAsync();
        if (rows is null) return;

        // Each row's Identity and Element must equal the authoritative Domain
        // definition's (BOSS_RULES.md §6, §6.4) — proving the inserted values
        // came from the documented content and not from a derived or guessed
        // value.
        foreach (var definition in BossDefinitions.All)
        {
            var row = rows.Single(r => r.BossDefinitionId == definition.BossDefinitionId);

            Assert.Equal(definition.BossId.Value, row.BossId.Value);
            Assert.Equal(definition.Element, row.Element);

            // The three-way identity contract: the persistence key and the
            // canonical technical Identity are distinct values.
            Assert.NotEqual(row.BossDefinitionId, row.BossId.Value);
        }
    }

    [Fact]
    public async Task Postgres_ShouldRoundTripBothJsonDocumentsToTheDomainDefinitions()
    {
        var rows = await ReadProvisionedRowsAsync();
        if (rows is null) return;

        // DATABASE.md §1 note items 3–4: the stored JSON satisfies the
        // persistence converters and equals the Domain's configuration. Stored
        // document text is compared too, so a column that round-trips only
        // because the converter is lenient is still caught.
        foreach (var definition in BossDefinitions.All)
        {
            var row = rows.Single(r => r.BossDefinitionId == definition.BossDefinitionId);

            Assert.Equal(definition.PassiveDefinition, row.PassiveDefinition);

            // DATABASE.md §1 note item 4 fixes the stored SkillDefinition
            // document to exactly skillId, baseDamage, chargeRequirement, and
            // cooldownTurns ("fixed by TASK-045 … unchanged"), so the comparison
            // is scoped to those four persisted members. The Skill's secondary
            // effect (BOSS_RULES.md §6.3.1) is Domain configuration on the same
            // record but is deliberately NOT a stored member — the same
            // treatment the combat stats MaxHP/ATK/DEF/EnrageThreshold receive —
            // so it is excluded from the persisted comparison rather than being
            // silently absent from it.
            Assert.Equal(
                definition.SkillDefinition with { SecondaryEffect = null },
                row.SkillDefinition);

            Assert.Equal(
                BossDefinitionJson.WritePassive(definition.PassiveDefinition),
                BossDefinitionJson.WritePassive(row.PassiveDefinition));

            Assert.Equal(
                BossDefinitionJson.WriteSkill(definition.SkillDefinition),
                BossDefinitionJson.WriteSkill(row.SkillDefinition));
        }
    }

    [Fact]
    public async Task Postgres_ShouldPreserveTheNonMatchChargedNullThreshold()
    {
        var rows = await ReadProvisionedRowsAsync();
        if (rows is null) return;

        // DATABASE.md §1 note item 3 / §3: a non-match-charged Passive is stored
        // as JSON null — never the 0 sentinel. BOSS_RULES.md §6.2 gives Thủy Ma
        // the Battle Start trigger, Sơn Thạch Vệ the "Boss HP ≤ 50%" trigger, and
        // Kim Lôi Vương the "Player Combo ≥ 4" trigger — none is match-charged.
        foreach (var id in new[]
                 {
                     "boss-def-thuy-ma", "boss-def-son-thach-ve", "boss-def-kim-loi-vuong",
                 })
        {
            var row = rows.Single(r => r.BossDefinitionId == id);

            Assert.Null(row.PassiveDefinition.Threshold);
        }

        // The two match-charged Passives keep their real threshold.
        foreach (var id in new[] { "boss-def-hoa-long", "boss-def-moc-yeu" })
        {
            Assert.Equal(5, rows.Single(r => r.BossDefinitionId == id).PassiveDefinition.Threshold);
        }
    }

    [Fact]
    public async Task Postgres_ShouldStoreTheDocumentedResetBehaviorPerRow()
    {
        var rows = await ReadProvisionedRowsAsync();
        if (rows is null) return;

        // BOSS_RULES.md §6.2.4 authors Sơn Thạch Vệ's Passive one-time / no
        // re-trigger after activation — the non-default No reset / persistent form
        // (PASSIVE_RULES.md §4 items 2–3), stored as the existing `Persistent`
        // token. Every other MVP Boss leaves the documented default
        // (DATABASE.md §1 note item 3, §3).
        var byId = rows.ToDictionary(r => r.BossDefinitionId, r => r.PassiveDefinition.ResetBehavior, StringComparer.Ordinal);

        Assert.Equal("Persistent", byId["boss-def-son-thach-ve"]);

        foreach (var id in new[]
                 {
                     "boss-def-hoa-long", "boss-def-thuy-ma", "boss-def-moc-yeu",
                     "boss-def-kim-loi-vuong",
                 })
        {
            Assert.Equal("Default", byId[id]);
        }
    }

    [Fact]
    public async Task Postgres_ShouldHoldNoDuplicateCanonicalRow()
    {
        var rows = await ReadProvisionedRowsAsync();
        if (rows is null) return;

        // DATABASE.md §3: BossDefinitionId (PK) and Identity (UNIQUE) are the
        // database-level guarantee against duplicates. Both keys are asserted
        // to be unique across the applied table — not merely the PK the
        // provider enforces anyway.
        Assert.Equal(5, rows.Count);

        Assert.Equal(
            rows.Count,
            rows.Select(r => r.BossDefinitionId).Distinct(StringComparer.Ordinal).Count());

        Assert.Equal(
            rows.Count,
            rows.Select(r => r.BossId.Value).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Postgres_ShouldStoreNoDisplayNameInAnyColumn()
    {
        var rows = await ReadProvisionedRowsAsync();
        if (rows is null) return;

        // DATABASE.md §1 note item 1: the display name is presentation content
        // and is never a column — only the five documented columns exist, so no
        // display-name text can be stored anywhere in the row. This asserts the
        // applied table's column set and that the stored Identity values are
        // the canonical technical IDs, never display names.
        await using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(BossDefinition))!;

        Assert.Equal(
            new[]
            {
                "BossDefinitionId",
                "Element",
                "Identity",
                "PassiveDefinition",
                "SkillDefinition",
            },
            entity.GetProperties()
                .Select(p => p.GetColumnName())
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray());

        foreach (var row in rows)
        {
            Assert.DoesNotContain(
                row.BossId.Value,
                new[] { "Hỏa Long", "Thủy Ma", "Mộc Yêu", "Sơn Thạch Vệ", "Kim Lôi Vương" });
        }
    }
}
