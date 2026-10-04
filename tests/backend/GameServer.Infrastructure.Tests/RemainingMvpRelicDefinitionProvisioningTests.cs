using System.Text;
using GameServer.Domain.Relics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// The TASK-184 provisioning migration for the six remaining canonical MVP
/// <c>RelicDefinition</c> rows — <c>DATABASE.md</c> §1/§5 item 4,
/// <c>RELIC_RULES.md</c> §3/§6/§8.5.
///
/// <b>Why these assertions live against the migration source.</b> The decided
/// mechanism is <b>migration-level <c>InsertData</c></b> — not model seed data
/// (<c>HasData</c>), not a startup loader, not a JSON pipeline. The InMemory
/// provider cannot apply a migration, and <c>GetSeedData()</c> is deliberately
/// empty, so the migration's own source is the authoritative record of what is
/// provisioned. This mirrors the convention
/// <see cref="PetCardRelicDefinitionProvisioningTests"/> established for TASK-085,
/// <see cref="ThanhXaAndSonHungSignatureSkillProvisioningTests"/> for TASK-168,
/// and <see cref="BossPersistenceTests"/> for TASK-053; the *applied* rows are
/// covered separately by
/// <see cref="PetCardRelicDefinitionPostgresProvisioningTests"/>.
///
/// What is verified is the documented contract only (TASK-184 AC-01…AC-06):
/// exactly six inserted rows, the canonical IDs, every value transcribed from
/// <c>RELIC_RULES.md</c> §8.5, a mirror <c>DeleteData</c> per insert, the absence
/// of any schema operation or second seed mechanism, and the untouched four-row
/// pre-existing set.
/// </summary>
public class RemainingMvpRelicDefinitionProvisioningTests
{
    private const string MigrationSuffix = "ProvisionRemainingMvpRelicDefinitions";

    // -----------------------------------------------------------------------
    // Canonical IDs — DATABASE.md §1 value forms
    // (`relic-` + ASCII kebab-case of the documented display name,
    // TASK-082 decision B / R2-9).
    //
    // Declared here as the literal documented values, so a drifted migration
    // fails against the contract itself rather than against its own contents.
    // -----------------------------------------------------------------------

    /// <summary>
    /// The six Relics TASK-184 provisions (TASK-184 AC-02) — the rows
    /// <c>RELIC_RULES.md</c> §6 note 4 recorded as content-defined and awaiting a
    /// downstream provisioning migration.
    /// </summary>
    private static readonly string[] ProvisionedRelicDefinitionIds =
    [
        "relic-burning-curse",
        "relic-combo-fang",
        "relic-arcane-battery",
        "relic-execution-mark",
        "relic-cascade-core",
        "relic-battle-instinct",
    ];

    /// <summary>
    /// The four Relics provisioned before TASK-184 by
    /// <c>20260929152651_ProvisionPetCardRelicContentDefinitions</c> and
    /// re-encoded by <c>20261003074309_StructureRelicDefinitionStructuredColumns</c>.
    /// This migration must neither re-insert, update, nor delete them
    /// (<c>AGENTS.md</c> §16; TASK-184 AC-04).
    /// </summary>
    private static readonly string[] PreExistingRelicDefinitionIds =
    [
        "relic-berserker-core",
        "relic-mana-crystal",
        "relic-assassin-eye",
        "relic-emergency-core",
    ];

    /// <summary>
    /// <c>RELIC_RULES.md</c> §3's closed Trigger list. §8.5 item 3 (TASK-131 D8)
    /// leaves §3 unchanged, so every Trigger any provisioned row declares must be
    /// a member of it.
    /// </summary>
    private static readonly string[] ClosedTriggerList =
    [
        "OnBattleStart", "OnMatch", "OnMatchCount", "OnCombo", "OnCascade",
        "OnPowerGain", "OnDamageDealt", "OnDamageTaken", "OnHpBelow",
        "OnCardCast", "OnTurnStart", "OnTurnEnd",
    ];

    // -----------------------------------------------------------------------
    // DATABASE.md §5 item 4 — no second provisioning mechanism
    // -----------------------------------------------------------------------

    [Fact]
    public void MigrationSource_ShouldContainNoHasDataOrRuntimeSeeder()
    {
        // DATABASE.md §5 item 4 forbids `HasData`, a seed runner, a startup
        // loader, a JSON content pipeline, and an external content service. The
        // migration is the only provisioning mechanism, and it must stay a
        // migration.
        var operations = ReadMigrationOperations();

        foreach (var forbidden in new[]
                 {
                     "HasData", "UseSeeding", "UseAsyncSeeding", "IHostedService",
                     "BackgroundService", "Seeder", "SeedAsync", "JsonSerializer",
                     ".json",
                 })
        {
            Assert.DoesNotContain(forbidden, operations, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Model_ShouldStillProvisionNothing()
    {
        // DATABASE.md §5 item 4: the mechanism is a migration INSERT, so the
        // MODEL must carry no seed of its own — the six new rows are not
        // HasData content.
        using var context = TestGameDbContextFactory.Create(nameof(Model_ShouldStillProvisionNothing));

        var model = context.GetService<IDesignTimeModel>().Model;

        Assert.Empty(model.FindEntityType(typeof(RelicDefinition))!.GetSeedData());
    }

    [Fact]
    public void MigrationSource_ShouldContainNoSchemaOperation()
    {
        // DATABASE.md §5 item 1: this migration is data only. The
        // RelicDefinition table, its primary key, and every column already exist
        // (20260925134303_AddRelicPersistence; the jsonb columns from
        // 20261003074309_StructureRelicDefinitionStructuredColumns), so any
        // schema operation here would be a contract violation rather than an
        // unavoidable generation artifact.
        var operations = ReadMigrationOperations();

        foreach (var schemaOperation in new[]
                 {
                     "CreateTable", "DropTable", "AddColumn", "DropColumn",
                     "AlterColumn", "CreateIndex", "DropIndex", "AddForeignKey",
                     "DropForeignKey", "AddPrimaryKey", "DropPrimaryKey",
                     "CreateSequence", "DropSequence", "RenameColumn",
                     "RenameTable", "AddCheckConstraint", "DropCheckConstraint",
                     "migrationBuilder.Sql(",
                 })
        {
            Assert.DoesNotContain(schemaOperation, operations);
        }
    }

    [Fact]
    public void ModelSnapshot_ShouldBeUnchangedByTheDataMigration()
    {
        // A data-only migration must not change the model. The snapshot is
        // therefore still the one the last schema migration generated and
        // contains no hint of the newly provisioned rows.
        var snapshot = ReadMigrationSourceFile("GameDbContextModelSnapshot");

        foreach (var forbidden in new[]
                 {
                     ".HasData(", "GetSeedData",
                     "relic-burning-curse", "relic-combo-fang",
                     "relic-arcane-battery", "relic-execution-mark",
                     "relic-cascade-core", "relic-battle-instinct",
                 })
        {
            Assert.DoesNotContain(forbidden, snapshot);
        }
    }

    // -----------------------------------------------------------------------
    // Row set — TASK-184 AC-01/AC-02/AC-05
    // -----------------------------------------------------------------------

    [Fact]
    public void MigrationSource_ShouldInsertExactlySixRelicRows()
    {
        // TASK-184: six RelicDefinition rows, and nothing else. Relics have no
        // FK, so this migration touches no other table.
        var operations = ReadMigrationOperations();

        Assert.Equal(6, CountOccurrences(operations, "migrationBuilder.InsertData("));
        Assert.Equal(6, CountOccurrences(operations, "migrationBuilder.DeleteData("));
        Assert.Equal(12, CountOccurrences(operations, "migrationBuilder."));

        // Every statement targets RelicDefinition and no other table. The
        // migration's two methods name it once per operation — six inserts in Up
        // and six deletes in Down — so twelve in total.
        Assert.Equal(
            12,
            CountOccurrences(operations, "table: \"RelicDefinition\","));

        foreach (var otherTable in new[]
                 {
                     "table: \"CardDefinition\"", "table: \"PetDefinition\"",
                     "table: \"BossDefinition\"", "table: \"Player\"",
                     "table: \"Relic\"",
                 })
        {
            Assert.DoesNotContain(otherTable, operations);
        }
    }

    [Fact]
    public void MigrationSource_ShouldInsertEveryCanonicalIdExactlyOnce()
    {
        // TASK-184 AC-05: each of the six ids appears once in the Up insert and
        // once in the Down delete key — never twice in either, so no row is
        // provisioned twice and none is left behind.
        var operations = ReadMigrationOperations();

        foreach (var canonicalId in ProvisionedRelicDefinitionIds)
        {
            // Each id appears once as an inserted value and once as a Down key,
            // so exactly twice — never three times, which would mean a duplicate
            // insert or a stray reference. Both forms are matched with a
            // delimiting quote so an id that is a prefix of another cannot
            // inflate the count.
            Assert.Equal(2, CountOccurrences(operations, $"\"{canonicalId}\""));

            // And exactly one of those two is the inserted value, the other the
            // delete key.
            Assert.Equal(
                1,
                CountOccurrences(operations, $"{canonicalId}\", \""));
            Assert.Equal(
                1,
                CountOccurrences(operations, $"keyValue: \"{canonicalId}\")"));
        }

        Assert.Equal(6, ProvisionedRelicDefinitionIds.Length);
    }

    [Fact]
    public void MigrationSource_ShouldInsertNoRowOutsideItsOwnDocumentedSet()
    {
        // TASK-184 AC-02/AC-04: exactly the six canonical ids appear. Any seventh
        // `relic-` literal would be invented content, and the four pre-existing
        // ids would mean this migration touched rows it does not own.
        var operations = ReadMigrationOperations();

        // Six ids × two occurrences each (Up value + Down key) = 12. A seventh
        // `relic-` literal would mean this migration provisions or references a
        // Relic outside its own documented set.
        Assert.Equal(12, CountOccurrences(operations, "\"relic-"));

        // No row outside the six is named: each pre-existing id is absent, and
        // the four are the only ids this migration must leave alone.
        foreach (var preExistingId in PreExistingRelicDefinitionIds)
        {
            Assert.DoesNotContain(preExistingId, operations, StringComparison.Ordinal);
        }

        foreach (var invented in new[]
                 {
                     "placeholder", "TODO", "FIXME",
                 })
        {
            Assert.DoesNotContain(invented, operations, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void MigrationSource_ShouldDeleteOnlyTheProvisionedRows()
    {
        // Down removes exactly the rows this migration introduced — no broad
        // table delete, no unrelated row, and every delete keyed by a canonical
        // primary key.
        var operations = ReadMigrationOperations();

        var upEnd = operations.IndexOf("protected override void Down", StringComparison.Ordinal);
        Assert.True(upEnd > 0, "The migration source declares no Down method.");

        var down = operations[upEnd..];

        Assert.Equal(6, CountOccurrences(down, "migrationBuilder.DeleteData("));
        Assert.DoesNotContain("migrationBuilder.Sql(", down);

        // Each delete names its key column and value explicitly.
        Assert.Equal(6, CountOccurrences(down, "keyColumn: \"RelicDefinitionId\""));
        Assert.Equal(6, CountOccurrences(down, "keyValue: \"relic-"));

        // The four pre-existing rows are never deleted.
        foreach (var preExistingId in PreExistingRelicDefinitionIds)
        {
            Assert.DoesNotContain(preExistingId, down, StringComparison.Ordinal);
        }
    }

    // -----------------------------------------------------------------------
    // RelicDefinition values — TASK-184 AC-03
    //
    // RELIC_RULES.md §8.5 owns each Trigger, Condition, and EffectDefinition
    // below verbatim; §3 is the closed Trigger list; §8.1–§8.4 own the shapes.
    // -----------------------------------------------------------------------

    [Fact]
    public void MigrationSource_ShouldWriteTheDocumentedTriggerPerRow()
    {
        // RELIC_RULES.md §8.5's Trigger column, transcribed. §8.5 item 3 keeps
        // §3's closed list unchanged, so each value is a §3 member and is written
        // as its member NAME — never an ordinal.
        var operations = ReadMigrationOperations();

        var expected = new (string Id, string Name, string Trigger)[]
        {
            ("relic-burning-curse", "Burning Curse", "OnBattleStart"),
            ("relic-combo-fang", "Combo Fang", "OnCombo"),
            ("relic-arcane-battery", "Arcane Battery", "OnPowerGain"),
            ("relic-execution-mark", "Execution Mark", "OnHpBelow"),
            ("relic-cascade-core", "Cascade Core", "OnCascade"),
            ("relic-battle-instinct", "Battle Instinct", "OnDamageTaken"),
        };

        foreach (var (id, name, trigger) in expected)
        {
            Assert.Contains(trigger, ClosedTriggerList);
            Assert.Contains($"\"{id}\", \"{name}\", \"{trigger}\"", operations);
        }
    }

    [Fact]
    public void MigrationSource_ShouldWriteTheDocumentedConditionPerRow()
    {
        // RELIC_RULES.md §8.5's Condition column, transcribed as the structured
        // object §8.1 defines (`conditionType` + `threshold`). §8.1 item 1 makes
        // the threshold an integer carried as data — "Combo ≥ 5" as a string is
        // explicitly not a valid Condition.
        var operations = ReadMigrationOperations();

        // The two conditional rows.
        Assert.Contains(
            """{"conditionType":"ComboAtLeast","threshold":5}""",
            operations);

        Assert.Contains(
            """{"conditionType":"HpPercentageBelow","threshold":30}""",
            operations);

        // The four rows §8.5 records `null` for carry no condition object at all
        // — §8.1 item 4's absent case, written as SQL NULL rather than a sentinel.
        Assert.Equal(4, CountOccurrences(operations, "\"OnBattleStart\", null,")
                        + CountOccurrences(operations, "\"OnPowerGain\", null,")
                        + CountOccurrences(operations, "\"OnCascade\", null,")
                        + CountOccurrences(operations, "\"OnDamageTaken\", null,"));

        // No superseded prose condition survives: none of the old spellings
        // appears anywhere in the executable operations.
        foreach (var prose in new[]
                 {
                     "every 3 Matches", "every 4 Matches", "Combo ≥ 3",
                     "Combo ≥ 5", "HP < 30%",
                 })
        {
            Assert.DoesNotContain(prose, operations, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MigrationSource_ShouldWriteTheDocumentedEffectPerRow()
    {
        // RELIC_RULES.md §8.5's EffectDefinition[] column, transcribed as the
        // structured array §8.2 defines. Each element carries the
        // effectType/valueType/value triple plus §8.3's target and lifetime, and
        // every member is drawn from the closed vocabularies §8.2/§8.3 fix.
        var operations = ReadMigrationOperations();

        var expected = new (string Id, string Effect)[]
        {
            ("relic-burning-curse",
             """[{"effectType":"BurnDamage","valueType":"Percentage","value":30,"target":"Pet","lifetime":"Battle"}]"""),
            ("relic-combo-fang",
             """[{"effectType":"Crit","valueType":"PercentagePoints","value":20,"target":"Pet","lifetime":"NextAttack"}]"""),
            ("relic-arcane-battery",
             """[{"effectType":"Power","valueType":"Flat","value":5,"target":"Pet","lifetime":"Immediate"}]"""),
            ("relic-execution-mark",
             """[{"effectType":"Crit","valueType":"PercentagePoints","value":15,"target":"Pet","lifetime":"NextAttack"}]"""),
            ("relic-cascade-core",
             """[{"effectType":"Power","valueType":"Flat","value":5,"target":"Pet","lifetime":"Immediate"}]"""),
            ("relic-battle-instinct",
             """[{"effectType":"ATK","valueType":"Percentage","value":10,"target":"Pet","lifetime":"NextAttack"}]"""),
        };

        foreach (var (_, effect) in expected)
        {
            Assert.Contains(effect, operations);
        }

        // Every written effect is a ONE-element array (each of the six declares
        // exactly one effect) and no row stores a second element.
        Assert.Equal(6, CountOccurrences(operations, "\"[{\""));
        Assert.Equal(0, CountOccurrences(operations, "},{"));

        // §8.3 item 5: no member beyond target and lifetime, so the Card-only
        // `duration` and `scope` members appear in no Relic payload.
        Assert.DoesNotContain("\"duration\"", operations);
        Assert.DoesNotContain("\"scope\"", operations);

        // §8.2 item 2/3: every magnitude is authored, so the unauthored marker
        // never appears.
        Assert.DoesNotContain("Undetermined", operations);
    }

    [Fact]
    public void MigrationSource_ShouldEncodeOnlyDocumentedEffectVocabularies()
    {
        // RELIC_RULES.md §8.2 item 1 closes effectType at
        // ATK | Power | Crit | CardCost | BurnDamage; §8.2 item 2 closes valueType
        // at Flat | Percentage | PercentagePoints | Undetermined; §8.3 item 1
        // defines Pet as the only target; §8.3 item 2's lifetimes are
        // Immediate | Battle | NextAttack. A value outside those sets would be an
        // invented member.
        var operations = ReadMigrationOperations();

        foreach (var effectType in new[]
                 {
                     "\"effectType\":\"BurnDamage\"", "\"effectType\":\"Crit\"",
                     "\"effectType\":\"Power\"", "\"effectType\":\"ATK\"",
                 })
        {
            Assert.Contains(effectType, operations);
        }

        foreach (var valueType in new[]
                 {
                     "\"valueType\":\"Percentage\"", "\"valueType\":\"PercentagePoints\"",
                     "\"valueType\":\"Flat\"",
                 })
        {
            Assert.Contains(valueType, operations);
        }

        // Every effect targets Pet, and only the three documented lifetimes are
        // used. Each written effect is counted so a stray fourth value fails here.
        Assert.Equal(6, CountOccurrences(operations, "\"target\":\"Pet\""));
        Assert.Equal(3, CountOccurrences(operations, "\"lifetime\":\"NextAttack\""));
        Assert.Equal(2, CountOccurrences(operations, "\"lifetime\":\"Immediate\""));
        Assert.Equal(1, CountOccurrences(operations, "\"lifetime\":\"Battle\""));

        // No Card-specific vocabulary leaks into a Relic payload.
        foreach (var cardOnly in new[]
                 {
                     "PercentMaxHp", "\"Heal\"", "\"Shield\"", "\"Damage\"",
                     "\"Burn\"", "\"PowerCost\"", "\"LoadoutCopyLimit\"",
                 })
        {
            Assert.DoesNotContain(cardOnly, operations);
        }
    }

    [Fact]
    public void MigrationSource_ShouldMatchTheSharedCanonicalContentTable()
    {
        // The migration and RelicProvisionedContent are two transcriptions of the
        // same RELIC_RULES.md §8.5 table. This cross-check fails if either drifts
        // from the other, so the test suite cannot hold two disagreeing versions
        // of the canonical contract.
        var operations = ReadMigrationOperations();

        foreach (var row in RelicProvisionedContent.Rows.Where(
                     row => ProvisionedRelicDefinitionIds.Contains(row.Id)))
        {
            Assert.Contains($"\"{row.Id}\", \"{row.Name}\", \"{row.Trigger}\"", operations);

            if (row.Condition is not null)
            {
                Assert.Contains(row.Condition.Value.ToPersistedPayload(), operations);
            }

            Assert.Contains(row.Effects.ToPersistedPayload(), operations);
        }

        // And §8.5's canonical set is exactly the ten: four pre-existing, six
        // newly provisioned. Nothing is missing and nothing is extra.
        Assert.Equal(
            10,
            RelicProvisionedContent.Rows.Length);

        foreach (var preExistingId in PreExistingRelicDefinitionIds)
        {
            Assert.Contains(
                RelicProvisionedContent.Rows,
                row => row.Id == preExistingId);
        }
    }

    // -----------------------------------------------------------------------
    // Fixtures
    // -----------------------------------------------------------------------

    /// <summary>
    /// A migration's source with line and XML-documentation comments removed, so
    /// an assertion targets the executable operations only. The migration
    /// documents the contract (including the mechanism names it must not use and
    /// the values it transcribes) in its comments, and a whole-file substring
    /// scan would otherwise flag that documentation.
    /// </summary>
    private static string ReadMigrationOperations()
    {
        var source = ReadMigrationSourceFile(MigrationSuffix);
        var builder = new StringBuilder();

        foreach (var rawLine in source.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            if (line.TrimStart().StartsWith("///", StringComparison.Ordinal) ||
                line.TrimStart().StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            builder.AppendLine(line);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Reads a file from the Infrastructure project's Migrations folder so its
    /// generated source can be asserted without a relational provider.
    ///
    /// Migrations are named <c>&lt;timestamp&gt;_&lt;Name&gt;.cs</c>; the model
    /// snapshot is a fixed file name with no timestamp.
    /// </summary>
    private static string ReadMigrationSourceFile(string fileNameSuffix)
    {
        var migrations = MigrationsDirectory();

        var file = Directory
            .EnumerateFiles(migrations, $"*_{fileNameSuffix}.cs")
            .SingleOrDefault(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal))
            ?? Directory
                .EnumerateFiles(migrations, $"*_{fileNameSuffix}.cs")
                .SingleOrDefault()
            ?? Directory
                .EnumerateFiles(migrations, $"{fileNameSuffix}.cs")
                .Single();

        return File.ReadAllText(file);
    }

    private static string MigrationsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null &&
               !Directory.Exists(Path.Combine(directory.FullName, "src", "backend")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        var migrations = Path.Combine(
            directory!.FullName,
            "src",
            "backend",
            "GameServer.Infrastructure",
            "Postgres",
            "Migrations");

        Assert.True(Directory.Exists(migrations), $"Migrations folder not found at {migrations}.");

        return migrations;
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
