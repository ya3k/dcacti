using GameServer.Domain.Relics;
using GameServer.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// The structured Relic <c>Condition</c> and <c>EffectDefinition</c> storage
/// contract at the persistence boundary — <c>DATABASE.md</c> §1's
/// <c>RelicDefinition</c> block and its "Relic <c>EffectDefinition</c> and
/// <c>Condition</c> contract" note, plus <c>RELIC_RULES.md</c> §8.1–§8.5, as
/// recorded by <c>ADR-018</c> items 1–4/9 and implemented by TASK-132.
///
/// What is verified is the documented storage contract only: the EF mapping
/// carries the structured values losslessly through the configured provider, the
/// two columns are the JSON type <c>DATABASE.md</c> §1 records (and no longer
/// carry the prose length limit), the four provisioned rows are encoded with the
/// values transcribed from <c>RELIC_RULES.md</c> §8.5, and the migration is
/// schema-correct, reversible, and scoped to exactly those four keys.
///
/// <b>No test here resolves a Relic.</b> No trigger is evaluated, no condition is
/// compared, no effect is applied, no <c>RelicTriggered</c> event is emitted, and
/// no battle-state member is touched: <c>RELIC_RULES.md</c> §8.7 records every
/// one of those as NOT IMPLEMENTED.
/// </summary>
public class RelicStructuredStorageTests
{
    private static GameDbContext CreateContext(string storeName) =>
        TestGameDbContextFactory.Create(storeName);

    private static RelicDefinition NewDefinition(
        string id,
        RelicCondition? condition,
        RelicEffectDefinitions effects) => new()
    {
        RelicDefinitionId = id,
        Name = id,
        Trigger = "OnMatchCount",
        Condition = condition,
        EffectDefinition = effects,
    };

    // -----------------------------------------------------------------------
    // The applied column shape — DATABASE.md §1
    // -----------------------------------------------------------------------

    [Fact]
    public void Model_ShouldMapBothRelicMembersThroughTheirStructuredConverters()
    {
        // DATABASE.md §1: both members are now STRUCTURED, so each must convert
        // rather than persist directly — and the converters are what make the
        // stored document the contract's shape rather than the Domain's field
        // names.
        using var context = CreateContext(nameof(Model_ShouldMapBothRelicMembersThroughTheirStructuredConverters));

        var entity = context.Model.FindEntityType(typeof(RelicDefinition))!;

        var condition = entity.FindProperty(nameof(RelicDefinition.Condition))!;
        var effect = entity.FindProperty(nameof(RelicDefinition.EffectDefinition))!;

        Assert.Equal(typeof(RelicCondition), condition.GetTypeMapping().Converter!.ModelClrType);
        Assert.Equal(typeof(string), condition.GetTypeMapping().Converter!.ProviderClrType);

        Assert.Equal(typeof(RelicEffectDefinitions), effect.GetTypeMapping().Converter!.ModelClrType);
        Assert.Equal(typeof(string), effect.GetTypeMapping().Converter!.ProviderClrType);
    }

    [Fact]
    public void Model_ShouldDropTheProseLengthLimitOnBothRelicMembers()
    {
        // RELIC_RULES.md §8.6 / TASK-131 D9: the former
        // `character varying(128)` columns are INSUFFICIENT for the structured
        // representation, so `HasMaxLength(128)` must no longer constrain either
        // member. Nullability follows the documented optionality: Condition is
        // optional (§8.1 item 4) and EffectDefinition is required.
        using var context = CreateContext(nameof(Model_ShouldDropTheProseLengthLimitOnBothRelicMembers));

        var entity = context.Model.FindEntityType(typeof(RelicDefinition))!;

        var condition = entity.FindProperty(nameof(RelicDefinition.Condition))!;
        var effect = entity.FindProperty(nameof(RelicDefinition.EffectDefinition))!;

        Assert.Null(condition.GetMaxLength());
        Assert.True(condition.IsNullable);

        Assert.Null(effect.GetMaxLength());
        Assert.False(effect.IsNullable);
    }

    [Fact]
    public void Model_ShouldAddNoRelicColumnAndLeaveTriggerAndIdentityAlone()
    {
        // DATABASE.md §1: only the two representations moved. The column list is
        // unchanged, and Trigger stays the §3 prose identity
        // (RELIC_RULES.md §8.5 item 3 / TASK-131 D8 leaves §3 unchanged) — so no
        // enum conversion is introduced for it.
        using var context = CreateContext(nameof(Model_ShouldAddNoRelicColumnAndLeaveTriggerAndIdentityAlone));

        var entity = context.Model.FindEntityType(typeof(RelicDefinition))!;

        var columns = entity.GetProperties()
            .Select(property => property.GetColumnName())
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "Condition", "EffectDefinition", "Name", "RelicDefinitionId", "Trigger",
            },
            columns);

        var trigger = entity.FindProperty(nameof(RelicDefinition.Trigger))!;

        Assert.Equal(typeof(string), trigger.ClrType);
        Assert.Null(trigger.GetValueConverter());
    }

    // -----------------------------------------------------------------------
    // Round trips through the configured provider — DATABASE.md §1
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RelicDefinition_ShouldRoundTripBothStructuredMembersLosslessly()
    {
        // The documented round chain "Domain → EF → storage → EF → Domain": the
        // structured condition and effect array come back member-for-member, and
        // the re-serialized form is byte-identical to what was written.
        var storeName = nameof(RelicDefinition_ShouldRoundTripBothStructuredMembersLosslessly);

        var condition = RelicCondition.Create(RelicConditionType.HpPercentageBelow, 30);
        var effects = RelicEffectDefinitions.Create(RelicEffectDefinition.Create(
            RelicEffectType.CardCost,
            RelicEffectValueType.Percentage,
            50,
            RelicEffectTarget.Pet,
            RelicEffectLifetime.Battle));

        var conditionPayload = condition.ToPersistedPayload();
        var effectsPayload = effects.ToPersistedPayload();

        await using (var context = CreateContext(storeName))
        {
            context.RelicDefinitions.Add(
                NewDefinition("relic-roundtrip", condition, effects));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var stored = await context.RelicDefinitions.SingleAsync();

            Assert.Equal(condition, stored.Condition);
            Assert.Equal(RelicConditionType.HpPercentageBelow, stored.Condition!.Value.ConditionType);
            Assert.Equal(30, stored.Condition!.Value.Threshold);

            Assert.Equal(effects, stored.EffectDefinition);
            Assert.Equal(1, stored.EffectDefinition.Count);
            Assert.Equal(RelicEffectType.CardCost, stored.EffectDefinition[0].EffectType);
            Assert.Equal(RelicEffectValueType.Percentage, stored.EffectDefinition[0].ValueType);
            Assert.Equal(50, stored.EffectDefinition[0].Value);
            Assert.Equal(RelicEffectTarget.Pet, stored.EffectDefinition[0].Target);
            Assert.Equal(RelicEffectLifetime.Battle, stored.EffectDefinition[0].Lifetime);

            Assert.Equal(conditionPayload, stored.Condition!.Value.ToPersistedPayload());
            Assert.Equal(effectsPayload, stored.EffectDefinition.ToPersistedPayload());
        }
    }

    [Fact]
    public async Task RelicDefinition_ShouldRoundTripAllFourProvisionedRows()
    {
        // The four content-defined rows as RELIC_RULES.md §8.5 authors them,
        // asserted per row through the configured provider. This is the
        // Domain-level twin of the applied-migration assertions below: the same
        // four values, read back through EF.
        var storeName = nameof(RelicDefinition_ShouldRoundTripAllFourProvisionedRows);

        var rows = RelicProvisionedContent.Rows;

        await using (var context = CreateContext(storeName))
        {
            foreach (var row in rows)
            {
                var definition = NewDefinition(row.Id, row.Condition, row.Effects);

                context.RelicDefinitions.Add(new RelicDefinition
                {
                    RelicDefinitionId = definition.RelicDefinitionId,
                    Name = row.Name,
                    Trigger = row.Trigger,
                    Condition = definition.Condition,
                    EffectDefinition = definition.EffectDefinition,
                });
            }

            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var stored = await context.RelicDefinitions
                .Where(definition => RelicProvisionedContent.Ids.Contains(
                    definition.RelicDefinitionId))
                .ToListAsync();

            Assert.Equal(rows.Length, stored.Count);

            foreach (var row in rows)
            {
                var definition = Assert.Single(
                    stored, candidate => candidate.RelicDefinitionId == row.Id);

                Assert.Equal(row.Name, definition.Name);
                Assert.Equal(row.Trigger, definition.Trigger);
                Assert.Equal(row.Condition, definition.Condition);
                Assert.Equal(row.Effects, definition.EffectDefinition);
            }
        }
    }

    [Fact]
    public async Task StructuredCondition_ShouldRemainOptionalAndNullRoundTrip()
    {
        // RELIC_RULES.md §8.1 item 4: Condition stays optional, so a Relic whose
        // Trigger alone is its complete condition carries none — and null must
        // survive the round trip rather than becoming a sentinel condition.
        var storeName = nameof(StructuredCondition_ShouldRemainOptionalAndNullRoundTrip);

        await using (var context = CreateContext(storeName))
        {
            context.RelicDefinitions.Add(NewDefinition(
                "relic-no-condition",
                condition: null,
                RelicProvisionedContent.Single("relic-berserker-core").Effects));

            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var stored = await context.RelicDefinitions.SingleAsync();

            Assert.Null(stored.Condition);
            Assert.NotNull(stored.EffectDefinition);
        }
    }

    [Fact]
    public async Task RelicDefinition_ShouldPreserveMultiElementOrderAcrossTheRoundTrip()
    {
        // RELIC_RULES.md §8.2 item 4: array order is a storage sequence only. The
        // reader/writer preserve it exactly and normalize nothing, so two arrays
        // differing only in order remain distinct stored values.
        var storeName = nameof(RelicDefinition_ShouldPreserveMultiElementOrderAcrossTheRoundTrip);

        var atk = RelicEffectDefinition.Create(
            RelicEffectType.ATK, RelicEffectValueType.Percentage, 5,
            RelicEffectTarget.Pet, RelicEffectLifetime.Battle);

        var power = RelicEffectDefinition.Create(
            RelicEffectType.Power, RelicEffectValueType.Flat, 10,
            RelicEffectTarget.Pet, RelicEffectLifetime.Immediate);

        await using (var context = CreateContext(storeName))
        {
            context.RelicDefinitions.Add(NewDefinition(
                "relic-forward",
                null,
                RelicEffectDefinitions.Create(atk, power)));

            context.RelicDefinitions.Add(NewDefinition(
                "relic-reverse",
                null,
                RelicEffectDefinitions.Create(power, atk)));

            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var forward = await context.RelicDefinitions
                .SingleAsync(definition => definition.RelicDefinitionId == "relic-forward");
            var reverse = await context.RelicDefinitions
                .SingleAsync(definition => definition.RelicDefinitionId == "relic-reverse");

            Assert.Equal(2, forward.EffectDefinition.Count);
            Assert.Equal(RelicEffectType.ATK, forward.EffectDefinition[0].EffectType);
            Assert.Equal(RelicEffectType.Power, forward.EffectDefinition[1].EffectType);

            Assert.Equal(RelicEffectType.Power, reverse.EffectDefinition[0].EffectType);
            Assert.Equal(RelicEffectType.ATK, reverse.EffectDefinition[1].EffectType);

            // Neither was sorted, so the two rows are not equal.
            Assert.NotEqual(forward.EffectDefinition, reverse.EffectDefinition);
        }
    }

    // -----------------------------------------------------------------------
    // The applied PostgreSQL rows — RELIC_RULES.md §8.5
    // -----------------------------------------------------------------------

    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=dcacti_db;Username=dcacti;Password=dcacti_dev_password";

    private static async Task<Npgsql.NpgsqlDataSource?> TryConnectAsync()
    {
        try
        {
            var dataSource = new Npgsql.NpgsqlDataSourceBuilder(ConnectionString).Build();

            await using var command = dataSource.CreateCommand(
                """
                SELECT to_regclass('"RelicDefinition"') IS NOT NULL
                   AND EXISTS (
                     SELECT 1 FROM information_schema.columns
                     WHERE table_name = 'RelicDefinition'
                       AND column_name = 'EffectDefinition'
                       AND data_type = 'jsonb')
                   AND EXISTS (
                     SELECT 1 FROM information_schema.columns
                     WHERE table_name = 'RelicDefinition'
                       AND column_name = 'Condition'
                       AND data_type = 'jsonb')
                """);

            var applied = (bool)(await command.ExecuteScalarAsync() ?? false);

            if (!applied)
            {
                await dataSource.DisposeAsync();
                return null;
            }

            return dataSource;
        }
        catch (Exception)
        {
            // No reachable PostgreSQL (or the migration is not applied): the
            // applied-row assertions are skipped rather than failing the suite,
            // following the existing PetCardRelicDefinitionPostgresProvisioningTests
            // convention. The provider-independent assertions above still run.
            return null;
        }
    }

    [Fact]
    public async Task AppliedRows_ShouldReadEveryProvisionedRowThroughTheStructuredContract()
    {
        // The documented chain "Database row → EF → Domain → serialization →
        // same contract shape", asserted on real PostgreSQL. The read is scoped
        // to the four exact keys rather than to a `relic-` prefix, because the
        // shared development database is also written by the API suites' smoke
        // fixtures — those rows are not this contract's content.
        await using var dataSource = await TryConnectAsync();
        if (dataSource is null) return;

        await using var command = dataSource.CreateCommand(
            """
            SELECT "RelicDefinitionId", "Trigger",
                   "Condition"::text, "EffectDefinition"::text
            FROM "RelicDefinition"
            WHERE "RelicDefinitionId" IN (
              'relic-berserker-core', 'relic-mana-crystal',
              'relic-assassin-eye', 'relic-emergency-core')
            ORDER BY "RelicDefinitionId"
            """);

        var stored = new Dictionary<string, (string Trigger, string Condition, string Effect)>(
            StringComparer.Ordinal);

        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                stored[reader.GetString(0)] = (
                    reader.GetString(1), reader.GetString(2), reader.GetString(3));
            }
        }

        Assert.Equal(4, stored.Count);

        foreach (var row in RelicProvisionedContent.Rows)
        {
            Assert.True(stored.ContainsKey(row.Id), $"The provisioned row '{row.Id}' must exist.");

            var (trigger, conditionPayload, effectPayload) = stored[row.Id];

            // Trigger is unchanged by §8 (TASK-131 D8).
            Assert.Equal(row.Trigger, trigger);

            // The reader accepts the applied value...
            var condition = RelicCondition.FromPersistedPayload(conditionPayload);
            var effects = RelicEffectDefinitions.FromPersistedPayload(effectPayload);

            // ...yields exactly the documented §8.5 values...
            Assert.Equal(row.Condition, condition);
            Assert.Equal(row.Effects, effects);

            // ...and serializes back to a shape the reader accepts again, with
            // the same elements in the same order, so no position-based rule was
            // applied.
            Assert.Equal(condition, RelicCondition.FromPersistedPayload(
                condition.ToPersistedPayload()));
            Assert.Equal(effects, RelicEffectDefinitions.FromPersistedPayload(
                effects.ToPersistedPayload()));

            for (var index = 0; index < row.Effects.Count; index++)
            {
                Assert.Equal(row.Effects[index].EffectType, effects[index].EffectType);
                Assert.Equal(row.Effects[index].ValueType, effects[index].ValueType);
                Assert.Equal(row.Effects[index].Value, effects[index].Value);
                Assert.Equal(row.Effects[index].Target, effects[index].Target);
                Assert.Equal(row.Effects[index].Lifetime, effects[index].Lifetime);
            }
        }
    }

    [Fact]
    public async Task AppliedRows_ShouldStoreBothMembersAsJsonAndHoldNoProse()
    {
        // The migrated rows contain STRUCTURED data rather than the superseded
        // prose: the effect column is an ARRAY, and the (required) condition
        // column is an OBJECT. The condition check is shape-based rather than a
        // list of the old strings, so it cannot pass merely because one spelling
        // was missed.
        await using var dataSource = await TryConnectAsync();
        if (dataSource is null) return;

        await using var command = dataSource.CreateCommand(
            """
            SELECT
              COUNT(*) FILTER (WHERE jsonb_typeof("EffectDefinition") = 'array'),
              COUNT(*) FILTER (WHERE jsonb_typeof("Condition") = 'object'),
              COUNT(*) FILTER (WHERE jsonb_typeof("Condition") <> 'object')
            FROM "RelicDefinition"
            WHERE "RelicDefinitionId" IN (
              'relic-berserker-core', 'relic-mana-crystal',
              'relic-assassin-eye', 'relic-emergency-core')
            """);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());

        Assert.Equal(4L, reader.GetInt64(0));
        Assert.Equal(4L, reader.GetInt64(1));
        Assert.Equal(0L, reader.GetInt64(2));

        // The superseded R2-7 prose appears in no row at all. Each old value is
        // matched as the JSON *string* it was stored as, which is exactly the
        // shape the migration replaced.
        await using var proseCommand = dataSource.CreateCommand(
            """
            SELECT COUNT(*)
            FROM "RelicDefinition"
            WHERE "RelicDefinitionId" IN (
              'relic-berserker-core', 'relic-mana-crystal',
              'relic-assassin-eye', 'relic-emergency-core')
              AND ("Condition"::text LIKE '%"every 3 Matches"%'
                   OR "Condition"::text LIKE '%"every 4 Matches"%'
                   OR "Condition"::text LIKE '%"Combo ≥ 3"%'
                   OR "Condition"::text LIKE '%"HP < 30%"%'
                   OR "EffectDefinition"::text LIKE '%"+5% ATK"%'
                   OR "EffectDefinition"::text LIKE '%"+10 Power"%'
                   OR "EffectDefinition"::text LIKE '%"Increased Crit chance"%'
                   OR "EffectDefinition"::text LIKE '%"Heal Card cost −50%"%')
            """);

        Assert.Equal(0L, (long)(await proseCommand.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task AppliedRows_ShouldHoldNoUndeterminedMagnitudeAndNoCardOnlyMember()
    {
        // §8.5 authors every magnitude of all four rows, so none may hold the
        // unauthored marker; and §8.3 item 5 defines no member beyond target and
        // lifetime, so no Card-only `duration`/`scope` member may appear.
        await using var dataSource = await TryConnectAsync();
        if (dataSource is null) return;

        await using var command = dataSource.CreateCommand(
            """
            SELECT COUNT(*)
            FROM "RelicDefinition"
            WHERE "RelicDefinitionId" IN (
              'relic-berserker-core', 'relic-mana-crystal',
              'relic-assassin-eye', 'relic-emergency-core')
              AND ("EffectDefinition"::text LIKE '%Undetermined%'
                   OR "EffectDefinition"::text LIKE '%duration%'
                   OR "EffectDefinition"::text LIKE '%scope%')
            """);

        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task AppliedRows_ShouldHoldTheDocumentedThresholdPerRow()
    {
        // RELIC_RULES.md §8.1 item 1: the threshold is part of the value. Each of
        // §8.5's four thresholds is asserted from the applied database, so a
        // prose value could not satisfy it.
        await using var dataSource = await TryConnectAsync();
        if (dataSource is null) return;

        await using var command = dataSource.CreateCommand(
            """
            SELECT "RelicDefinitionId",
                   "Condition" ->> 'conditionType',
                   ("Condition" ->> 'threshold')::int
            FROM "RelicDefinition"
            WHERE "RelicDefinitionId" IN (
              'relic-berserker-core', 'relic-mana-crystal',
              'relic-assassin-eye', 'relic-emergency-core')
            ORDER BY "RelicDefinitionId"
            """);

        var stored = new Dictionary<string, (string Form, int Threshold)>(StringComparer.Ordinal);

        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                stored[reader.GetString(0)] = (reader.GetString(1), reader.GetInt32(2));
            }
        }

        Assert.Equal(4, stored.Count);

        foreach (var row in RelicProvisionedContent.Rows)
        {
            var (form, threshold) = stored[row.Id];

            Assert.Equal(row.Condition.ConditionType.ToString(), form);
            Assert.Equal(row.Condition.Threshold, threshold);
        }
    }

    [Fact]
    public async Task AppliedRows_ShouldHoldNoBurningCurseRow()
    {
        // RELIC_RULES.md §6 note 3 / §8.5 item 4: Burning Curse stays deferred,
        // because §3 requires a primary Trigger from its closed list while §6
        // note 1 describes that Relic as a static modifier with none. No
        // placeholder row, Trigger, or value may be inserted by TASK-132.
        await using var dataSource = await TryConnectAsync();
        if (dataSource is null) return;

        await using var command = dataSource.CreateCommand(
            """
            SELECT COUNT(*) FROM "RelicDefinition"
            WHERE "RelicDefinitionId" = 'relic-burning-curse'
            """);

        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
    }

    // -----------------------------------------------------------------------
    // The migration source — deterministic, reversible, and scoped
    // -----------------------------------------------------------------------

    private const string MigrationName = "StructureRelicDefinitionStructuredColumns";

    private static string ReadMigrationSource() => ReadMigrationSource(MigrationName);

    private static string ReadMigrationSource(string migrationName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        var probed = new List<string>();

        while (directory is not null &&
               !Directory.Exists(Path.Combine(
                   directory.FullName, "src", "backend", "GameServer.Infrastructure")))
        {
            probed.Add(directory.FullName);
            directory = directory.Parent;
        }

        Assert.True(
            directory is not null,
            "The repository root could not be located from '" + AppContext.BaseDirectory
            + "'. Probed: " + string.Join(" -> ", probed));

        var migrations = Path.Combine(
            directory!.FullName,
            "src", "backend", "GameServer.Infrastructure", "Postgres", "Migrations");

        Assert.True(
            Directory.Exists(migrations),
            $"The migrations directory does not exist: '{migrations}'.");

        // The match is exact on the base name, excluding the EF Designer
        // companion: a wildcard `*_<name>.cs` search also matches
        // `<stamp>_<name>.Designer.cs` on Windows, which would make the
        // "exactly one" assertions below depend on filesystem glob semantics.
        var candidates = Directory
            .GetFiles(migrations, "*.cs")
            .Where(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal))
            .Where(path => Path.GetFileNameWithoutExtension(path).EndsWith(
                $"_{migrationName}",
                StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            candidates.Length == 1,
            $"Expected exactly one migration source for '{migrationName}' under "
            + $"'{migrations}' but found {candidates.Length}: "
            + string.Join(", ", candidates.Select(Path.GetFileName)));

        return File.ReadAllText(candidates[0]);
    }

    /// <summary>
    /// The migration's executable content only — the <c>Up</c>/<c>Down</c> method
    /// bodies with their documentation comments stripped.
    ///
    /// The distinction matters: the class and method documentation legitimately
    /// <i>discusses</i> Burning Curse, the Card members, and the superseded prose
    /// while explaining that they are not written, while the migration operations
    /// must not touch them. A source-wide substring check would fail on prose
    /// that is merely explanatory.
    /// </summary>
    private static string ReadMigrationOperations()
    {
        var source = ReadMigrationSource();
        var lines = source.Split('\n');
        var body = new List<string>();
        var insideBlockComment = false;
        var insideMethod = false;

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();

            if (insideBlockComment)
            {
                if (trimmed.Contains("*/", StringComparison.Ordinal))
                {
                    insideBlockComment = false;
                }

                continue;
            }

            if (trimmed.StartsWith("/*", StringComparison.Ordinal))
            {
                insideBlockComment = !trimmed.Contains("*/", StringComparison.Ordinal);
                continue;
            }

            if (trimmed.StartsWith("///", StringComparison.Ordinal) ||
                trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmed.Contains("protected override void Up", StringComparison.Ordinal) ||
                trimmed.Contains("protected override void Down", StringComparison.Ordinal))
            {
                insideMethod = true;
                body.Add(line);
                continue;
            }

            if (!insideMethod)
            {
                continue;
            }

            body.Add(line);
        }

        return string.Join('\n', body);
    }

    /// <summary>Only the <c>Up</c> method's executable content.</summary>
    private static string ReadUpOperations()
    {
        var operations = ReadMigrationOperations();
        var downStart = operations.IndexOf("protected override void Down", StringComparison.Ordinal);

        Assert.True(downStart > 0, "The migration must define Down.");

        return operations[..downStart];
    }

    [Fact]
    public void MigrationSource_ShouldBeTheOnlyOneTouchingTheRelicColumns()
    {
        // TASK-132 acceptance: exactly ONE migration is added for this task, and
        // the applied history — including TASK-131's no-schema-change state and
        // the Card migrations — is unmodified.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null &&
               !Directory.Exists(Path.Combine(directory.FullName, "src", "backend")))
        {
            directory = directory.Parent;
        }

        var migrations = Path.Combine(
            directory!.FullName,
            "src", "backend", "GameServer.Infrastructure", "Postgres", "Migrations");

        Assert.Single(Directory
            .GetFiles(migrations, "*.cs")
            .Where(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal))
            .Where(path => Path.GetFileNameWithoutExtension(path).EndsWith(
                "_StructureRelicDefinitionStructuredColumns", StringComparison.Ordinal)));

        // The Card migrations still exist exactly once each — they were not
        // replaced, renamed, or re-added.
        Assert.Single(Directory
            .GetFiles(migrations, "*.cs")
            .Where(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal))
            .Where(path => Path.GetFileNameWithoutExtension(path).EndsWith(
                "_StructureCardDefinitionEffectDefinition", StringComparison.Ordinal)));

        Assert.Single(Directory
            .GetFiles(migrations, "*.cs")
            .Where(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal))
            .Where(path => Path.GetFileNameWithoutExtension(path).EndsWith(
                "_EncodeCardEffectDefinitionArrayRows", StringComparison.Ordinal)));
    }

    [Fact]
    public void MigrationSource_ShouldChangeExactlyTheTwoRelicColumns()
    {
        // DATABASE.md §1: two columns move to `jsonb` and nothing else changes —
        // no new column, no table, no constraint, no index, no foreign key. In
        // particular the `Relic` ownership table, `Trigger`, and every other
        // table are untouched.
        var operations = ReadMigrationOperations();

        Assert.Contains("ALTER TABLE \"RelicDefinition\"", operations, StringComparison.Ordinal);
        Assert.Contains("TYPE jsonb", operations, StringComparison.Ordinal);

        foreach (var forbidden in new[]
                 {
                     "CreateTable", "DropTable", "CreateIndex", "DropIndex",
                     "AddColumn", "DropColumn", "AddForeignKey", "DropForeignKey",
                     "AddPrimaryKey", "DROP COLUMN", "RENAME",
                 })
        {
            Assert.DoesNotContain(forbidden, operations, StringComparison.Ordinal);
        }

        // The only modified table is RelicDefinition: no other table's name
        // appears as an ALTER/UPDATE target.
        foreach (var otherTable in new[]
                 {
                     "\"Relic\"", "\"CardDefinition\"", "\"PetDefinition\"", "\"Player\"",
                     "\"Pet\"", "\"BattleResult\"", "\"BossDefinition\"",
                     "\"PlayerUnlockedCard\"",
                 })
        {
            Assert.DoesNotContain(otherTable, operations, StringComparison.Ordinal);
        }

        // Both members are altered, and no other RelicDefinition column is.
        Assert.Contains("ALTER COLUMN \"Condition\"", operations, StringComparison.Ordinal);
        Assert.Contains("ALTER COLUMN \"EffectDefinition\"", operations, StringComparison.Ordinal);

        foreach (var untouchedColumn in new[] { "\"Trigger\"", "\"Name\"", "\"RelicDefinitionId\"" })
        {
            Assert.DoesNotContain(
                $"ALTER COLUMN {untouchedColumn}", operations, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MigrationSource_ShouldTranscribeEveryProvisionedValueFromRelicRules()
    {
        // Each of §8.5's four rows is written with its documented values,
        // asserted member by member so a transcription error fails here.
        var up = ReadUpOperations();

        // The four exact keys — never a prefix, never a LIKE pattern.
        foreach (var id in RelicProvisionedContent.Ids)
        {
            Assert.Contains($"\"RelicDefinitionId\" = '{id}'", up, StringComparison.Ordinal);
        }

        // §8.1's three condition forms with §8.5's thresholds.
        Assert.Contains("'conditionType', 'MatchCountAtLeast'", up, StringComparison.Ordinal);
        Assert.Contains("'conditionType', 'ComboAtLeast'", up, StringComparison.Ordinal);
        Assert.Contains("'conditionType', 'HpPercentageBelow'", up, StringComparison.Ordinal);
        Assert.Contains("'threshold', 3", up, StringComparison.Ordinal);
        Assert.Contains("'threshold', 4", up, StringComparison.Ordinal);
        Assert.Contains("'threshold', 30", up, StringComparison.Ordinal);

        // §8.2's four effect identities and the value types §8.3 pairs them with.
        Assert.Contains("'effectType', 'ATK'", up, StringComparison.Ordinal);
        Assert.Contains("'effectType', 'Power'", up, StringComparison.Ordinal);
        Assert.Contains("'effectType', 'Crit'", up, StringComparison.Ordinal);
        Assert.Contains("'effectType', 'CardCost'", up, StringComparison.Ordinal);
        Assert.Contains("'valueType', 'Percentage'", up, StringComparison.Ordinal);
        Assert.Contains("'valueType', 'Flat'", up, StringComparison.Ordinal);
        Assert.Contains("'valueType', 'PercentagePoints'", up, StringComparison.Ordinal);

        // §8.5's four magnitudes.
        Assert.Contains("'value', 5", up, StringComparison.Ordinal);
        Assert.Contains("'value', 10", up, StringComparison.Ordinal);
        Assert.Contains("'value', 50", up, StringComparison.Ordinal);

        // §8.3's target vocabulary and §8.4 item 5's lifetimes.
        Assert.Contains("'target', 'Pet'", up, StringComparison.Ordinal);
        Assert.Contains("'lifetime', 'Battle'", up, StringComparison.Ordinal);
        Assert.Contains("'lifetime', 'Immediate'", up, StringComparison.Ordinal);
        Assert.Contains("'lifetime', 'NextAttack'", up, StringComparison.Ordinal);

        // Every element carries exactly the five documented members: each of the
        // four rows constructs one array and one object. The object constructor
        // appears twice per row because each row is written by one statement that
        // names the array and nests the object — 4 arrays, and 8 occurrences of
        // the object constructor (one per row, plus the one nested in each row's
        // array).
        Assert.Equal(4, CountOccurrences(up, "jsonb_build_array("));
        Assert.Equal(8, CountOccurrences(up, "jsonb_build_object("));
    }

    [Fact]
    public void MigrationSource_ShouldWriteNoUndeterminedAndNoCardOnlyMember()
    {
        // §8.5 authors every magnitude, so `Up` writes no unauthored marker; and
        // §8.3 item 5 defines no member beyond `target`/`lifetime`, so no
        // Card-only `duration`/`scope` member is written. Both survive only in
        // the documentation comments, not in the executable statements.
        var up = ReadUpOperations();

        Assert.DoesNotContain("Undetermined", up, StringComparison.Ordinal);
        Assert.DoesNotContain("'duration'", up, StringComparison.Ordinal);
        Assert.DoesNotContain("'scope'", up, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrationSource_ShouldInsertNoBurningCurseRowAndDeleteNothing()
    {
        // RELIC_RULES.md §6 note 3 / §8.5 item 4: Burning Curse stays deferred,
        // and this migration is a content rewrite rather than a provisioning
        // change — so it inserts no row and deletes none.
        var operations = ReadMigrationOperations();

        Assert.DoesNotContain("relic-burning-curse", operations, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT", operations, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE", operations, StringComparison.Ordinal);
        Assert.DoesNotContain("SET \"Condition\" = NULL", operations, StringComparison.Ordinal);
        Assert.DoesNotContain("SET \"EffectDefinition\" = NULL", operations, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrationSource_ShouldScopeItsContentStatementsToTheFourExactKeys()
    {
        // The row scope is exact: the content statements match the four keys and
        // nothing else — no pattern matching, no prefix, no discovery by name or
        // by current value — so no fifth row can be swept in.
        var up = ReadUpOperations();

        var keys = System.Text.RegularExpressions.Regex
            .Matches(up, @"RelicDefinitionId"" = '([^']+)'")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(RelicProvisionedContent.Ids.OrderBy(id => id, StringComparer.Ordinal), keys);

        Assert.DoesNotContain("LIKE", up, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrationSource_ShouldPreserveRowsOutsideTheFourContentKeys()
    {
        // Both tables are shared with the API suites' smoke fixtures, which carry
        // arbitrary non-JSON text in these columns. They are not this
        // migration's content, so the text is preserved verbatim rather than
        // given an invented structured value (AGENTS.md §7).
        var up = ReadUpOperations();

        Assert.Contains("to_jsonb(", up, StringComparison.Ordinal);
        Assert.Contains("IS NULL THEN NULL", up, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrationSource_ShouldCastExplicitly()
    {
        // PostgreSQL has no assignment cast from `character varying` to `jsonb`,
        // so EF's generated ALTER alone is rejected with SQLSTATE 42804 (`USING`
        // states the conversion). The reverse direction needs it too: converting
        // `jsonb` back to a bounded `character varying` is not an assignment cast
        // either, and `#>> '{}'` renders a value as its unquoted text.
        var operations = ReadMigrationOperations();

        Assert.Equal(2, CountOccurrences(operations, "USING ("));
        Assert.Equal(2, CountOccurrences(operations, "#>> '{}'"));
    }

    [Fact]
    public void MigrationSource_ShouldReverseDeterministically()
    {
        // TASK-132 acceptance: the migration's `Down` restores the prior
        // `character varying(128)` shape AND the four rows' documented prose, so
        // Up → Down returns the schema and the content to the pre-migration
        // state. The prose restored is RELIC_RULES.md §6's rule text — including
        // its "≥" and "−" characters — because that is what the provisioning
        // migration stored.
        // The counting assertions are made against the EXECUTABLE statements, so
        // they cannot be satisfied (or broken) by the documentation comments that
        // discuss the same constructs. The '≥' / '−' character check below uses
        // the whole source, because a literal in a statement and its description
        // are the same text.
        var operations = ReadMigrationOperations();
        var downStart = operations.IndexOf("protected override void Down", StringComparison.Ordinal);

        Assert.True(downStart > 0, "Down must exist.");

        var down = operations[downStart..];
        var source = ReadMigrationSource();

        Assert.Contains("character varying(128)", down, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(down, "TYPE character varying(128)"));

        // Two `#>> '{}'` casts — one per column — render the stored JSON back to
        // unquoted text before the prose is restored.
        Assert.Equal(2, CountOccurrences(down, "#>> '{}'"));
        Assert.Equal(2, CountOccurrences(down, "ALTER COLUMN"));

        // Each of the four rows returns to its documented prose, matched on the
        // structured payload this migration wrote as well as on its key.
        Assert.Equal(4, CountOccurrences(down, "AND \"EffectDefinition\"::jsonb = jsonb_build_array("));
        Assert.Equal(4, CountOccurrences(down, "SET \"Condition\" = '"));

        Assert.Contains("'every 3 Matches'", source, StringComparison.Ordinal);
        Assert.Contains("'+5% ATK'", source, StringComparison.Ordinal);
        Assert.Contains("'every 4 Matches'", source, StringComparison.Ordinal);
        Assert.Contains("'+10 Power'", source, StringComparison.Ordinal);
        Assert.Contains("'Combo ≥ 3'", source, StringComparison.Ordinal);
        Assert.Contains("'Increased Crit chance'", source, StringComparison.Ordinal);
        Assert.Contains("'HP < 30%'", source, StringComparison.Ordinal);
        Assert.Contains("'Heal Card cost −50%'", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrationSource_ShouldUseNoNonDeterministicConstruct()
    {
        // Determinism (TDD.md §6): fixed keys and fixed values only — no clock,
        // no RNG, no environment-derived value, no ordering dependence.
        var source = ReadMigrationSource();

        foreach (var forbidden in new[]
                 {
                     "NOW()", "now()", "CURRENT_TIMESTAMP", "random()", "RANDOM()",
                     "gen_random_uuid", "uuid_generate", "NEWID", "ORDER BY",
                 })
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MigrationSource_ShouldAuthorNoRuntimeBehaviour()
    {
        // RELIC_RULES.md §8.7: TASK-132 implements no trigger evaluation, no
        // condition evaluation, and no effect application. The migration is
        // DDL/DML over content definition columns and contains no executable
        // gameplay, no event emission, and no battle-state carrier.
        //
        // The assertions are made against the EXECUTABLE statements rather than
        // the whole source: the class documentation legitimately names
        // `RelicTriggered` and the unimplemented stages while recording that it
        // performs none of them.
        var operations = ReadMigrationOperations();

        foreach (var forbidden in new[]
                 {
                     "RelicTriggered", "RelicEngine", "RelicManager", "EffectEngine",
                     "ConditionEngine", "BattleState", "PetState", "StatusEffect",
                     "CardCostModifiers", "NextAttackCritModifiers", "EffectiveATK",
                     "EffectiveCrit", "EffectiveCardCost", "SignalR", "Redis",
                 })
        {
            Assert.DoesNotContain(forbidden, operations, StringComparison.Ordinal);
        }
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = haystack.IndexOf(needle, StringComparison.Ordinal);

        while (index >= 0)
        {
            count++;
            index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
        }

        return count;
    }
}
