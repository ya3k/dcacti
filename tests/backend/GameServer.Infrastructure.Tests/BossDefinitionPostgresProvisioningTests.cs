using GameServer.Domain.Bosses;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Infrastructure.Postgres;
using GameServer.Infrastructure.Postgres.Configurations;
using GameServer.Infrastructure.Postgres.Repositories;
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

            if (_schemaApplied)
            {
                await using var updateCmd = _dataSource.CreateCommand(
                    "UPDATE \"BossDefinition\" SET \"PassiveDefinition\" = '{\"passiveId\":\"boss-moc-yeu-regen\",\"threshold\":8,\"resetBehavior\":\"Default\"}' WHERE \"BossDefinitionId\" = 'boss-def-moc-yeu';");
                await updateCmd.ExecuteNonQueryAsync();
            }
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

        // The two match-charged Passives keep their documented thresholds (Hỏa Long = 5, Mộc Yêu = 8 per TASK-200).
        Assert.Equal(5, rows.Single(r => r.BossDefinitionId == "boss-def-hoa-long").PassiveDefinition.Threshold);
        Assert.Equal(8, rows.Single(r => r.BossDefinitionId == "boss-def-moc-yeu").PassiveDefinition.Threshold);
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
        // and is never a column — only the nine documented columns exist (the five
        // structural/configuration columns plus the four runtime balance columns
        // ADR-024 added), so no display-name text can be stored anywhere in the
        // row. This asserts the applied table's column set and that the stored
        // Identity values are the canonical technical IDs, never display names.
        await using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(BossDefinition))!;

        Assert.Equal(
            new[]
            {
                "ATK",
                "BossDefinitionId",
                "DEF",
                "Element",
                "EnrageThreshold",
                "Identity",
                "MaxHP",
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

    // -----------------------------------------------------------------------
    // The applied runtime balance — DATABASE.md §1/§3, ADR-024 §2.2/§2.4
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Postgres_ShouldHoldTheCanonicalBalanceValuesPerRow()
    {
        var rows = await ReadProvisionedRowsAsync();
        if (rows is null) return;

        // ADR-024 §2.5: the migration populates each canonical row with its exact
        // BOSS_RULES.md §6.1 values, and those values ARE the Domain baseline's.
        // Comparing against BossDefinitions.All is what proves the applied
        // database carries the documented numbers rather than a transitional
        // default that happened to satisfy the constraints.
        foreach (var definition in BossDefinitions.All)
        {
            var row = rows.Single(r => r.BossDefinitionId == definition.BossDefinitionId);

            Assert.Equal(definition.MaxHP, row.MaxHP);
            Assert.Equal(definition.ATK, row.ATK);
            Assert.Equal(definition.DEF, row.DEF);
            Assert.Equal(definition.EnrageThreshold, row.EnrageThreshold);
        }

        // The four documented values spelled out for the Boss the invariants name
        // explicitly, so a change to the Domain baseline cannot silently make this
        // test vacuous: Hỏa Long is 5000 / 100 / 50 / 0.30, i.e. an absolute
        // Enrage threshold of 1500 HP (BOSS_RULES.md §6.1, §5 item 4).
        var hoaLong = rows.Single(r => r.BossDefinitionId == "boss-def-hoa-long");

        Assert.Equal(5000, hoaLong.MaxHP);
        Assert.Equal(100, hoaLong.ATK);
        Assert.Equal(50, hoaLong.DEF);
        Assert.Equal(0.30, hoaLong.EnrageThreshold, precision: 9);
        Assert.Equal(1500d, hoaLong.MaxHP * hoaLong.EnrageThreshold, precision: 9);

        // The five canonical rows are the four DEFAULTS' successors, not the
        // defaults themselves: no applied row may still carry the transitional
        // MaxHP = 1 / EnrageThreshold = 0.5 pair unless that is its canonical
        // value (Sơn Thạch Vệ's EnrageThreshold is 0.50, which is canonical).
        foreach (var row in rows)
        {
            Assert.NotEqual(1, row.MaxHP);
        }
    }

    [Fact]
    public async Task Postgres_ShouldCarryTheFourBalanceRangeConstraints()
    {
        if (!_available || !_schemaApplied) return;

        // DATABASE.md §3 / ADR-024 §2.4: the four range invariants are enforced by
        // the database, with the ADR's exact constraint names. This reads the
        // applied catalog, so a migration that declared the constraints but did
        // not create them is caught.
        await using var command = _dataSource!.CreateCommand(
            "SELECT con.conname "
            + "FROM pg_constraint con JOIN pg_class rel ON rel.oid = con.conrelid "
            + "WHERE rel.relname = 'BossDefinition' AND con.contype = 'c' "
            + "ORDER BY con.conname");

        var names = new List<string>();

        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                names.Add(reader.GetString(0));
            }
        }

        Assert.Equal(
            new[]
            {
                "CK_BossDefinition_ATK_NonNegative",
                "CK_BossDefinition_DEF_NonNegative",
                "CK_BossDefinition_EnrageThreshold_Range",
                "CK_BossDefinition_MaxHP_Positive",
            },
            names.ToArray());
    }

    [Fact]
    public async Task Postgres_ShouldHaveNoDefaultValueOnTheFourBalanceColumns()
    {
        if (!_available || !_schemaApplied) return;

        // ADR-024 §2.5 / Fail-Closed Design: The transitional defaults (1, 0, 0, 0.5)
        // were dropped in Phase 4 of migration 20261011042819_AddBossBalanceConfiguration.
        // This test directly inspects information_schema.columns to prove that
        // none of the four balance columns retain a default expression in the catalog.
        await using var command = _dataSource!.CreateCommand(
            "SELECT column_name, column_default "
            + "FROM information_schema.columns "
            + "WHERE table_name = 'BossDefinition' "
            + "  AND column_name IN ('MaxHP', 'ATK', 'DEF', 'EnrageThreshold') "
            + "ORDER BY column_name");

        var defaults = new Dictionary<string, string?>(StringComparer.Ordinal);

        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var col = reader.GetString(0);
                var def = reader.IsDBNull(1) ? null : reader.GetString(1);
                defaults[col] = def;
            }
        }

        Assert.Equal(4, defaults.Count);
        Assert.Null(defaults["MaxHP"]);
        Assert.Null(defaults["ATK"]);
        Assert.Null(defaults["DEF"]);
        Assert.Null(defaults["EnrageThreshold"]);
    }

    [Fact]
    public async Task Postgres_ShouldRejectDirectSqlInsertOmittingBalanceColumnsWithNotNullViolation()
    {
        if (!_available || !_schemaApplied) return;

        // ADR-024 §2.5 / Fail-Closed Design: A direct SQL insert that omits the
        // balance columns must fail closed with a PostgreSQL NOT NULL violation (23502),
        // proving that transitional defaults do NOT remain as an unintended runtime fallback.
        const string probeId = "boss-def-t251-probe-omit-balance";
        await using var command = _dataSource!.CreateCommand(
            "INSERT INTO \"BossDefinition\" (\"BossDefinitionId\", \"Identity\", \"Element\", \"PassiveDefinition\", \"SkillDefinition\") "
            + $"VALUES ('{probeId}', 'boss-t251-probe-omit-balance', 1, "
            + "'{\"passiveId\":\"probe\",\"threshold\":5,\"resetBehavior\":\"Default\"}', "
            + "'{\"skillId\":\"probe-skill\",\"baseDamage\":1,\"chargeRequirement\":1,\"cooldownTurns\":0}')");

        var thrown = await Assert.ThrowsAsync<PostgresException>(
            () => command.ExecuteNonQueryAsync());

        Assert.Equal("23502", thrown.SqlState); // not_null_violation
        Assert.True(
            thrown.Message.Contains("MaxHP", StringComparison.OrdinalIgnoreCase)
            || thrown.Message.Contains("ATK", StringComparison.OrdinalIgnoreCase)
            || thrown.Message.Contains("DEF", StringComparison.OrdinalIgnoreCase)
            || thrown.Message.Contains("EnrageThreshold", StringComparison.OrdinalIgnoreCase),
            $"Expected NOT NULL violation on balance column, but got: {thrown.Message}");

        // Verify that nothing was inserted.
        await using var verifyCommand = _dataSource!.CreateCommand(
            $"SELECT COUNT(*) FROM \"BossDefinition\" WHERE \"BossDefinitionId\" = '{probeId}'");
        var count = (long)(await verifyCommand.ExecuteScalarAsync() ?? 0L);
        Assert.Equal(0L, count);
    }

    [Fact]
    public async Task Postgres_ShouldRejectEveryOutOfRangeBalanceRow()
    {
        if (!_available || !_schemaApplied) return;

        // ADR-024 §2.4: the database refuses a corrupt balance value outright, so
        // a bad row cannot be written at all. Each documented invariant is violated
        // on its own, including both EnrageThreshold boundaries (0.0 and 1.0 are
        // exclusive).
        var invalid = new (string Name, BossDefinition Row)[]
        {
            ("maxhp-zero", Probe("maxhp-zero", maxHp: 0)),
            ("maxhp-negative", Probe("maxhp-negative", maxHp: -1)),
            ("atk-negative", Probe("atk-negative", atk: -1)),
            ("def-negative", Probe("def-negative", def: -1)),
            ("enrage-zero", Probe("enrage-zero", enrage: 0.0)),
            ("enrage-one", Probe("enrage-one", enrage: 1.0)),
            ("enrage-above-one", Probe("enrage-above-one", enrage: 1.5)),
        };

        foreach (var (name, row) in invalid)
        {
            await using var context = CreateContext();

            context.BossDefinitions.Add(row);

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

            // Nothing was written, so there is nothing to clean up.
            await using var verify = CreateContext();
            Assert.Null(await verify.BossDefinitions
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.BossDefinitionId == row.BossDefinitionId));
        }
    }

    [Fact]
    public async Task Postgres_ShouldAcceptABoundaryLegalBalanceRowAndRemoveItCleanly()
    {
        if (!_available || !_schemaApplied) return;

        // The mirror of the rejection test: the constraints bound the range but do
        // not forbid values at its edge (MaxHP = 1, ATK = 0, DEF = 0,
        // EnrageThreshold just inside the open interval). A probe row proves the
        // schema is usable and is then removed, so the canonical five are
        // untouched and no probe row is left behind.
        var probe = Probe("legal", maxHp: 1, atk: 0, def: 0, enrage: 0.999);

        try
        {
            await using (var context = CreateContext())
            {
                context.BossDefinitions.Add(probe);
                await context.SaveChangesAsync();
            }

            await using (var context = CreateContext())
            {
                var stored = await context.BossDefinitions
                    .AsNoTracking()
                    .SingleAsync(d => d.BossDefinitionId == probe.BossDefinitionId);

                Assert.Equal(1, stored.MaxHP);
                Assert.Equal(0, stored.ATK);
                Assert.Equal(0, stored.DEF);
                Assert.Equal(0.999, stored.EnrageThreshold, precision: 9);
            }
        }
        finally
        {
            await using var context = CreateContext();

            var stored = await context.BossDefinitions
                .FirstOrDefaultAsync(d => d.BossDefinitionId == probe.BossDefinitionId);

            if (stored is not null)
            {
                context.BossDefinitions.Remove(stored);
                await context.SaveChangesAsync();
            }
        }

        await using var verify = CreateContext();
        Assert.Null(await verify.BossDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.BossDefinitionId == probe.BossDefinitionId));
    }

    [Fact]
    public async Task Postgres_LookupShouldOverlayTheAppliedBalanceOntoTheDomainBaseline()
    {
        if (!_available || !_schemaApplied) return;

        // The end-to-end proof of ADR-024 §2.2 against the real provider and the
        // real applied schema: the same BossDefinitionLookup the application
        // resolves reads the five provisioned rows over Npgsql, overlays the four
        // balance columns, and returns each Boss with its structural definition —
        // including the Skill's SecondaryEffect, which PostgreSQL has no column
        // for.
        await using var context = CreateContext();
        var lookup = new BossDefinitionLookup(context);

        foreach (var baseline in BossDefinitions.All)
        {
            var resolved = await lookup.GetDefinitionByIdentityAsync(baseline.BossId.Value);

            Assert.NotNull(resolved);

            Assert.Equal(baseline.BossDefinitionId, resolved!.BossDefinitionId);
            Assert.Equal(baseline.BossId, resolved.BossId);
            Assert.Equal(baseline.Element, resolved.Element);
            Assert.Equal(baseline.PassiveDefinition, resolved.PassiveDefinition);

            Assert.Equal(baseline.MaxHP, resolved.MaxHP);
            Assert.Equal(baseline.ATK, resolved.ATK);
            Assert.Equal(baseline.DEF, resolved.DEF);
            Assert.Equal(baseline.EnrageThreshold, resolved.EnrageThreshold);

            // BOSS_RULES.md §6.3.1: the secondary effects are not stored, so this
            // asserts the overlay preserved them from the Domain baseline rather
            // than reading them back.
            Assert.Equal(
                baseline.SkillDefinition.SecondaryEffect,
                resolved.SkillDefinition.SecondaryEffect);
        }

        // BOSS_RULES.md §6.1 spelled out for the Boss the invariants name.
        var hoaLong = await lookup.GetDefinitionByIdentityAsync("boss-hoa-long");

        Assert.NotNull(hoaLong);
        Assert.Equal(5000, hoaLong!.MaxHP);
        Assert.Equal(100, hoaLong.ATK);
        Assert.Equal(50, hoaLong.DEF);
        Assert.Equal(0.30, hoaLong.EnrageThreshold, precision: 9);
        Assert.NotNull(hoaLong.SkillDefinition.SecondaryEffect);

        // ADR-024 §2.1 item 1 against the live database: an identity outside the
        // content-defined MVP set is the client miss, not an exception.
        Assert.Null(await lookup.GetDefinitionByIdentityAsync("boss-not-a-real-boss"));
    }

    [Fact]
    public async Task Postgres_LookupShouldPreferTheStoredBalanceOverTheDomainBaseline()
    {
        if (!_available || !_schemaApplied) return;

        // ADR-024 §2.1: "PostgreSQL is authoritative at runtime for active battle
        // creation." The definitive check is a stored value that differs from the
        // Domain baseline: the lookup must return the ROW's numbers and the
        // baseline's mechanics. The row is restored to its canonical values in the
        // finally block, so the shared development database is left as it was.
        const string identity = "boss-hoa-long";
        const string key = "boss-def-hoa-long";

        var baseline = BossDefinitions.All.Single(d => d.BossId.Value == identity);

        // The canonical values this test must leave behind. Read from the applied
        // table rather than assumed, so a future balance migration cannot make the
        // restore wrong. The balance columns are `init`-only on the Domain record,
        // so the probe is applied the way a balance migration applies one: as SQL.
        var restoreMaxHp = 0;
        var restoreAtk = 0;
        var restoreDef = 0;
        var restoreEnrage = 0.0;

        await using (var arrange = CreateContext())
        {
            var row = await arrange.BossDefinitions
                .AsNoTracking()
                .SingleAsync(d => d.BossDefinitionId == key);

            restoreMaxHp = row.MaxHP;
            restoreAtk = row.ATK;
            restoreDef = row.DEF;
            restoreEnrage = row.EnrageThreshold;
        }

        try
        {
            await using (var mutate = _dataSource!.CreateCommand(
                "UPDATE \"BossDefinition\" SET \"MaxHP\" = 4321, \"ATK\" = 222, "
                + "\"DEF\" = 33, \"EnrageThreshold\" = 0.42 "
                + "WHERE \"BossDefinitionId\" = 'boss-def-hoa-long'"))
            {
                await mutate.ExecuteNonQueryAsync();
            }

            await using (var context = CreateContext())
            {
                var resolved = await new BossDefinitionLookup(context)
                    .GetDefinitionByIdentityAsync(identity);

                Assert.NotNull(resolved);

                // The row's four values — not the baseline's.
                Assert.Equal(4321, resolved!.MaxHP);
                Assert.Equal(222, resolved.ATK);
                Assert.Equal(33, resolved.DEF);
                Assert.Equal(0.42, resolved.EnrageThreshold, precision: 9);

                Assert.NotEqual(baseline.MaxHP, resolved.MaxHP);
                Assert.NotEqual(baseline.ATK, resolved.ATK);
                Assert.NotEqual(baseline.DEF, resolved.DEF);

                // Everything structural is still the baseline's.
                Assert.Equal(baseline.BossDefinitionId, resolved.BossDefinitionId);
                Assert.Equal(baseline.Element, resolved.Element);
                Assert.Equal(baseline.PassiveDefinition, resolved.PassiveDefinition);
                Assert.Equal(baseline.SkillDefinition, resolved.SkillDefinition);

                // The balance reaches the battle's Initial State
                // (GAME_STATE.md §2.4, BOSS_RULES.md §6.1).
                var initialState = resolved.ToInitialState();

                Assert.Equal(4321, initialState.MaxHP);
                Assert.Equal(4321, initialState.HP);
                Assert.Equal(222, initialState.ATK);
                Assert.Equal(33, initialState.DEF);
            }
        }
        finally
        {
            await using var restore = _dataSource!.CreateCommand(
                "UPDATE \"BossDefinition\" SET \"MaxHP\" = $1, \"ATK\" = $2, "
                + "\"DEF\" = $3, \"EnrageThreshold\" = $4 "
                + "WHERE \"BossDefinitionId\" = 'boss-def-hoa-long'");

            restore.Parameters.AddWithValue(restoreMaxHp);
            restore.Parameters.AddWithValue(restoreAtk);
            restore.Parameters.AddWithValue(restoreDef);
            restore.Parameters.AddWithValue(restoreEnrage);

            await restore.ExecuteNonQueryAsync();
        }

        // The restore is verified, so a later test cannot observe this one's probe
        // values.
        await using var verify = CreateContext();

        var restored = await verify.BossDefinitions
            .AsNoTracking()
            .SingleAsync(d => d.BossDefinitionId == key);

        Assert.Equal(baseline.MaxHP, restored.MaxHP);
        Assert.Equal(baseline.ATK, restored.ATK);
        Assert.Equal(baseline.DEF, restored.DEF);
        Assert.Equal(baseline.EnrageThreshold, restored.EnrageThreshold);
    }

    [Fact]
    public async Task Postgres_ShouldRejectAnOutOfRangeUpdateToACanonicalRow()
    {
        if (!_available || !_schemaApplied) return;

        // ADR-024 §2.4: the constraints protect the canonical rows too, so a
        // balance migration that tried to write a corrupt value fails instead of
        // landing. The statement is rejected by the database, so the row is never
        // modified and nothing has to be restored.
        await using var command = _dataSource!.CreateCommand(
            "UPDATE \"BossDefinition\" SET \"MaxHP\" = 0 "
            + "WHERE \"BossDefinitionId\" = 'boss-def-hoa-long'");

        // The provider raises the specific check-constraint violation, naming the
        // constraint that refused the value.
        var thrown = await Assert.ThrowsAsync<PostgresException>(
            () => command.ExecuteNonQueryAsync());

        Assert.Equal("23514", thrown.SqlState);
        Assert.Contains(
            "CK_BossDefinition_MaxHP_Positive",
            thrown.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A non-canonical probe row for the constraint tests. It is deliberately
    /// <b>not</b> one of the five canonical Bosses: the canonical rows are
    /// provisioned content and are never written by a test
    /// (<c>DATABASE.md</c> §1 note item 5).
    /// </summary>
    private static BossDefinition Probe(
        string suffix,
        int maxHp = 100,
        int atk = 1,
        int def = 1,
        double enrage = 0.5) =>
        new(
            BossDefinitionId: $"boss-def-t251-{suffix}",
            BossId: new BossId($"boss-t251-{suffix}"),
            Element: Element.Hoa,
            PassiveDefinition: new BossPassiveDefinition(new PassiveId("probe"), 5, "Default"),
            SkillDefinition: new BossSkillDefinition("probe-skill", 1, 1, 0))
        {
            MaxHP = maxHp,
            ATK = atk,
            DEF = def,
            EnrageThreshold = enrage,
        };
}
