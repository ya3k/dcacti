using System.Text.Json;
using GameServer.Domain.Bosses;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Infrastructure.Postgres;
using GameServer.Infrastructure.Postgres.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// <c>BossDefinition</c> persistence — <c>DATABASE.md</c> §1, §3 (TASK-044),
/// plus the migration-source contract of both provisioning migrations
/// (TASK-053 and TASK-172).
///
/// What is verified is the documented contract: the five persisted columns and
/// their exact PK/constraint semantics, the caller-supplied (never generated)
/// persistence key distinct from the canonical Identity, the two NOT NULL JSON
/// documents and their fixed member lists — including <c>threshold: null</c> for
/// a non-match-charged Passive rather than the <c>0</c> sentinel — the
/// <b>absence</b> of the combat-definition columns, and the provisioning
/// contract's separation of concerns: the model still seeds nothing (no
/// <c>HasData</c>/<c>GetSeedData()</c>) while the migrations own the canonical
/// rows as migration-level <c>InsertData</c> (<c>DATABASE.md</c> §1 note item 5).
///
/// The two provisioning migrations are asserted independently, because each owns
/// its own row set: <c>ProvisionBossDefinitions</c> (TASK-053) owns the three
/// Bosses content-defined then, and <c>ProvisionTwoRemainingMvpBosses</c>
/// (TASK-172) adds the two TASK-172 authored. Neither may touch the other's rows.
///
/// Nothing here exercises Boss gameplay: no Skill fires, no Passive triggers,
/// and no combat stat is read. The type under test is persistence only.
/// </summary>
public class BossPersistenceTests
{
    private static GameDbContext CreateContext(string storeName) =>
        TestGameDbContextFactory.Create(storeName);

    private static IModel CreateDesignTimeModel(string storeName)
    {
        using var context = CreateContext(storeName);
        return context.GetService<IDesignTimeModel>().Model;
    }

    /// <summary>
    /// A definition carrying the documented <c>DATABASE.md</c> §1 values for
    /// one content-defined MVP Boss. <c>PassiveDefinition</c> and
    /// <c>SkillDefinition</c> are the persisted groups; a null Threshold is the
    /// documented always-active form.
    /// </summary>
    private static BossDefinition NewDefinition(
        string bossDefinitionId,
        string identity,
        Element element = Element.Hoa,
        string passiveId = "boss-hoa-long-rage",
        int? threshold = 5,
        string resetBehavior = "Default",
        string skillId = "flame-burst",
        int baseDamage = 150,
        int chargeRequirement = 5,
        int cooldownTurns = 2) => new(
            BossDefinitionId: bossDefinitionId,
            BossId: new BossId(identity),
            Element: element,
            PassiveDefinition: new BossPassiveDefinition(
                new PassiveId(passiveId), threshold, resetBehavior),
            SkillDefinition: new BossSkillDefinition(
                skillId, baseDamage, chargeRequirement, cooldownTurns));

    // -----------------------------------------------------------------------
    // DATABASE.md §1 — the field set
    // -----------------------------------------------------------------------

    [Fact]
    public void BossDefinition_ShouldCarryExactlyTheDocumentedPersistedFields()
    {
        // DATABASE.md §1 defines BossDefinition as BossDefinitionId (PK),
        // Identity, Element, PassiveDefinition, and SkillDefinition. The set is
        // closed: the combat-definition values MaxHP/ATK/DEF/EnrageThreshold are
        // explicitly NOT stored (§1 note item 1), and no display-name member
        // exists (§1 note item 1).
        var fields = typeof(BossDefinition)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "ATK",
                "BossDefinitionId",
                "BossId",
                "DEF",
                "Element",
                "EnrageThreshold",
                "MaxHP",
                "PassiveDefinition",
                "PassiveId",
                "PassiveResetBehavior",
                "PassiveThreshold",
                "SkillBaseDamage",
                "SkillChargeRequirement",
                "SkillCooldownTurns",
                "SkillDefinition",
                "SkillId",
            },
            fields);
    }

    [Fact]
    public void BossDefinition_ShouldExposeNoDisplayNameColumn()
    {
        // DATABASE.md §1 note item 1: "The display name is presentation content
        // owned by BOSS_RULES.md §6 and is not a column of this table." The
        // canonical technical Identity is carried as BossId; no display-name
        // member may exist alongside it.
        var fields = typeof(BossDefinition).GetProperties().Select(p => p.Name).ToArray();

        foreach (var forbidden in new[]
                 {
                     "DisplayName", "Name", "Display", "LocalizedName",
                 })
        {
            Assert.DoesNotContain(forbidden, fields);
        }
    }

    // -----------------------------------------------------------------------
    // DATABASE.md §2/§4 — mapping, key, relationships, indexes
    // -----------------------------------------------------------------------

    [Fact]
    public void Model_ShouldMapBossDefinitionToTheDocumentedTable()
    {
        var model = CreateDesignTimeModel(nameof(Model_ShouldMapBossDefinitionToTheDocumentedTable));

        var definition = model.FindEntityType(typeof(BossDefinition));

        Assert.NotNull(definition);
        Assert.Equal("BossDefinition", definition!.GetTableName());
        Assert.Equal(
            "BossDefinitionId",
            definition.FindPrimaryKey()!.Properties.Single().Name);
    }

    [Fact]
    public void Model_ShouldStoreExactlyTheFiveDocumentedColumns()
    {
        // DATABASE.md §1's BossDefinition block lists five entries. The model
        // must hold those and nothing else — in particular no MaxHP/ATK/DEF/
        // EnrageThreshold column (§1 note item 1) and no display-name column.
        var model = CreateDesignTimeModel(nameof(Model_ShouldStoreExactlyTheFiveDocumentedColumns));

        var entity = model.FindEntityType(typeof(BossDefinition))!;

        var columns = entity.GetProperties()
            .Select(p => p.GetColumnName())
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "BossDefinitionId",
                "Element",
                "Identity",
                "PassiveDefinition",
                "SkillDefinition",
            },
            columns);
    }

    [Fact]
    public void Model_ShouldNotPersistTheCombatDefinitionValues()
    {
        // DATABASE.md §1 note item 1: MaxHP, ATK, DEF, and EnrageThreshold
        // "remain sourced from the authoritative Domain BossDefinition content
        // at battle creation" — they are not columns. A silent promotion of a
        // static combat value into the database is exactly what this guards.
        var model = CreateDesignTimeModel(nameof(Model_ShouldNotPersistTheCombatDefinitionValues));

        var entity = model.FindEntityType(typeof(BossDefinition))!;
        var columns = entity.GetProperties().Select(p => p.GetColumnName()).ToArray();

        foreach (var forbidden in new[] { "MaxHP", "ATK", "DEF", "EnrageThreshold" })
        {
            Assert.DoesNotContain(forbidden, columns);
        }
    }

    [Fact]
    public void Model_ShouldMakeIdentityUniqueAndRequired()
    {
        // DATABASE.md §3: BossDefinition.Identity NOT NULL, UNIQUE — "the unique
        // target of the FK lookup in §1", which resolves
        // BattleResult.BossDefinitionId from BattleState.BossState.BossId
        // (§1 note item 2).
        var model = CreateDesignTimeModel(nameof(Model_ShouldMakeIdentityUniqueAndRequired));

        var entity = model.FindEntityType(typeof(BossDefinition))!;

        var identity = entity.FindProperty(nameof(BossDefinition.BossId))!;
        Assert.Equal("Identity", identity.GetColumnName());
        Assert.False(identity.IsNullable);

        var index = entity.GetIndexes().Single();
        Assert.Equal("IX_BossDefinition_Identity", index.GetDatabaseName());
        Assert.True(index.IsUnique);
        Assert.Equal("Identity", index.Properties.Single().GetColumnName());
    }

    [Fact]
    public void Model_ShouldRequireThePersistenceKeyWithNoGeneratedValue()
    {
        // DATABASE.md §1 note item 2 (TASK-049): BossDefinitionId is a
        // caller/content-supplied stable key — "No GUID, integer,
        // provider-generated, or database-generated key is used". The column is
        // therefore non-nullable, carries no configured default, and is never
        // store-generated: the value always comes from the definition itself.
        var model = CreateDesignTimeModel(nameof(Model_ShouldRequireThePersistenceKeyWithNoGeneratedValue));

        var entity = model.FindEntityType(typeof(BossDefinition))!;
        var key = entity.FindProperty(nameof(BossDefinition.BossDefinitionId))!;

        Assert.False(key.IsNullable);
        Assert.Equal(typeof(string), key.ClrType);
        Assert.Null(key.GetDefaultValueSql());
        Assert.NotEqual(ValueGenerated.OnAdd, key.ValueGenerated);
    }

    [Fact]
    public void Model_ShouldRequireBothJsonDocuments()
    {
        // DATABASE.md §1/§3: PassiveDefinition and SkillDefinition are JSON,
        // NOT NULL — "every Boss carries exactly one Passive" and "every Boss
        // has exactly one Skill" (BOSS_RULES.md §1). The InMemory test provider
        // has no relational type mapping, so the documented jsonb column type is
        // asserted from the migration script itself (see
        // AddBossPersistence), and nullability from the model here.
        var model = CreateDesignTimeModel(nameof(Model_ShouldRequireBothJsonDocuments));

        var entity = model.FindEntityType(typeof(BossDefinition))!;

        foreach (var name in new[] { "PassiveDefinition", "SkillDefinition" })
        {
            var property = entity.GetProperties()
                .Single(p => p.GetColumnName() == name);

            Assert.False(property.IsNullable);
        }
    }

    [Fact]
    public void Migration_ShouldCreateTheFiveDocumentedColumnsAsNotNullJsonb()
    {
        // DATABASE.md §1 note items 3–4, §3 and §5 item 1: the two JSON objects
        // are NOT NULL and stored as jsonb, while the exact SQL type is an
        // implementation detail. The generated migration is the authoritative
        // record of the schema this task creates, so the contract is asserted
        // against it: exactly five columns, both JSON columns non-nullable
        // jsonb, and no seed/insert of any kind.
        var migration = ReadAddBossPersistenceMigration();

        foreach (var jsonColumn in new[] { "PassiveDefinition", "SkillDefinition" })
        {
            Assert.Contains(
                $"{jsonColumn} = table.Column<string>(type: \"jsonb\", nullable: false)",
                migration);
        }

        Assert.Contains("name: \"BossDefinition\"", migration);
        Assert.Contains(
            "table.PrimaryKey(\"PK_BossDefinition\", x => x.BossDefinitionId)",
            migration);
        Assert.Contains("name: \"IX_BossDefinition_Identity\"", migration);
        Assert.Contains("unique: true", migration);

        // No combat-definition column may appear (§1 note item 1).
        foreach (var forbidden in new[] { "MaxHP", "ATK", "DEF", "EnrageThreshold" })
        {
            Assert.DoesNotContain($"{forbidden} = table.Column", migration);
        }

        // Schema only: this historical migration provisions nothing. The
        // provisioning contract (DATABASE.md §1 note item 5) places the three
        // rows in the separate later migration ProvisionBossDefinitions, so
        // this file must stay byte-identical and free of any INSERT/seed.
        foreach (var forbidden in new[]
                 {
                     "InsertData", "HasData", "boss-def-hoa-long", "black-hoa-long",
                     "boss-hoa-long",
                 })
        {
            Assert.DoesNotContain(forbidden, migration);
        }
    }

    /// <summary>
    /// Reads a migration source file from the Infrastructure project so its
    /// generated schema/SQL can be asserted without a relational provider.
    /// </summary>
    private static string ReadMigrationSource(string fileNameSuffix)
    {
        // Walk up from the test binary to the repository root, then into the
        // Infrastructure project's migrations folder.
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

        var migrationFile = Directory
            .EnumerateFiles(migrations, $"*_{fileNameSuffix}.cs")
            .Single(file => !file.EndsWith(".Designer.cs", StringComparison.Ordinal));

        return File.ReadAllText(migrationFile);
    }

    /// <summary>
    /// Reads the <c>AddBossPersistence</c> migration source.
    /// </summary>
    private static string ReadAddBossPersistenceMigration() =>
        ReadMigrationSource("AddBossPersistence");

    [Fact]
    public void Model_ShouldDeclareNoBossDefinitionIndexBeyondTheIdentityUnique()
    {
        // DATABASE.md §4 lists no BossDefinition index and states further
        // indexes should be added only when a real query pattern requires them.
        // The only index is the documented Identity uniqueness; no FK leaves
        // this table, so none may be scaffolded either.
        var model = CreateDesignTimeModel(nameof(Model_ShouldDeclareNoBossDefinitionIndexBeyondTheIdentityUnique));

        var entity = model.FindEntityType(typeof(BossDefinition))!;

        var index = Assert.Single(entity.GetIndexes());
        Assert.Equal("IX_BossDefinition_Identity", index.GetDatabaseName());
        Assert.Empty(entity.GetForeignKeys());
    }

    // -----------------------------------------------------------------------
    // Persistence round-trips
    // -----------------------------------------------------------------------

    [Fact]
    public async Task BossDefinition_ShouldPersistEveryDocumentedColumn()
    {
        var storeName = nameof(BossDefinition_ShouldPersistEveryDocumentedColumn);

        await using (var context = CreateContext(storeName))
        {
            context.BossDefinitions.Add(NewDefinition("boss-def-hoa-long", "boss-hoa-long"));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var stored = await context.BossDefinitions.SingleAsync();

            Assert.Equal("boss-def-hoa-long", stored.BossDefinitionId);
            Assert.Equal("boss-hoa-long", stored.BossId.Value);
            Assert.Equal(Element.Hoa, stored.Element);
            Assert.Equal(new PassiveId("boss-hoa-long-rage"), stored.PassiveDefinition.PassiveId);
            Assert.Equal(5, stored.PassiveDefinition.Threshold);
            Assert.Equal("Default", stored.PassiveDefinition.ResetBehavior);
            Assert.Equal("flame-burst", stored.SkillDefinition.SkillId);
            Assert.Equal(150, stored.SkillDefinition.BaseDamage);
            Assert.Equal(5, stored.SkillDefinition.ChargeRequirement);
            Assert.Equal(2, stored.SkillDefinition.CooldownTurns);
        }
    }

    [Fact]
    public async Task PersistenceKeyAndIdentity_ShouldRemainDistinct()
    {
        // DATABASE.md §1 note items 1–2: BossDefinitionId and Identity are
        // independent values that are never collapsed, and neither is derived
        // from the other. Both are NOT NULL strings, so only asserting the exact
        // stored values distinguishes a correct mapping from a swapped one.
        var storeName = nameof(PersistenceKeyAndIdentity_ShouldRemainDistinct);

        await using (var context = CreateContext(storeName))
        {
            context.BossDefinitions.Add(NewDefinition("boss-def-thuy-ma", "boss-thuy-ma"));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var stored = await context.BossDefinitions.SingleAsync();

            Assert.Equal("boss-def-thuy-ma", stored.BossDefinitionId);
            Assert.Equal("boss-thuy-ma", stored.BossId.Value);
            Assert.NotEqual(stored.BossDefinitionId, stored.BossId.Value);
        }
    }

    [Fact]
    public async Task Element_ShouldRoundTripEveryFiveElementsValue()
    {
        // DATABASE.md §1: Element — one of the Five Elements
        // (ELEMENT_RULES.md §1), stored as the enum's numeric value.
        var storeName = nameof(Element_ShouldRoundTripEveryFiveElementsValue);

        var elements = new[] { Element.Moc, Element.Tho, Element.Thuy, Element.Hoa, Element.Kim };

        await using (var context = CreateContext(storeName))
        {
            for (var i = 0; i < elements.Length; i++)
            {
                context.BossDefinitions.Add(
                    NewDefinition($"boss-def-{i}", $"boss-{i}", elements[i]));
            }

            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var stored = await context.BossDefinitions
                .OrderBy(d => d.BossDefinitionId)
                .Select(d => d.Element)
                .ToListAsync();

            Assert.Equal(elements, stored);
        }
    }

    [Fact]
    public async Task PassiveDefinition_ShouldPersistAnAlwaysActiveNullThreshold()
    {
        // DATABASE.md §1 note item 3 / §3: threshold = null ⇔ always-active, no
        // threshold (BOSS_RULES.md §6.2's Thủy Ma), and "0 is never used as a
        // 'no threshold' sentinel". The stored document must carry JSON null —
        // not 0 — and reading it back must give the Domain's always-active
        // marker rather than a real threshold.
        var storeName = nameof(PassiveDefinition_ShouldPersistAnAlwaysActiveNullThreshold);

        await using (var context = CreateContext(storeName))
        {
            context.BossDefinitions.Add(
                NewDefinition(
                    "boss-def-thuy-ma",
                    "boss-thuy-ma",
                    Element.Thuy,
                    passiveId: "boss-thuy-ma-heal",
                    threshold: null));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var stored = await context.BossDefinitions.SingleAsync();

            Assert.Null(stored.PassiveDefinition.Threshold);

            // The Domain's documented in-code always-active marker is 0, and the
            // resolution reads it as "do not charge" — never as "reached".
            Assert.Equal(0, stored.PassiveThreshold);
        }
    }

    [Fact]
    public async Task PassiveDefinition_ShouldNeverStoreZeroAsTheAlwaysActiveSentinel()
    {
        // The mirror of the case above: a definition whose Domain members are
        // always-active (Threshold 0 in code) must serialize the documented
        // `null`, never `0` (DATABASE.md §1 note item 3).
        var storeName = nameof(PassiveDefinition_ShouldNeverStoreZeroAsTheAlwaysActiveSentinel);

        await using (var context = CreateContext(storeName))
        {
            context.BossDefinitions.Add(
                NewDefinition(
                    "boss-def-always-active",
                    "boss-always-active",
                    passiveId: "boss-always-active-heal",
                    threshold: null));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var stored = await context.BossDefinitions.SingleAsync();

            Assert.Null(stored.PassiveDefinition.Threshold);
            Assert.NotEqual(0, stored.PassiveDefinition.Threshold ?? int.MinValue);
        }
    }

    [Fact]
    public async Task PassiveDefinition_ShouldRoundTripAllThreeResetBehaviorTokens()
    {
        // DATABASE.md §1 note item 3 / §3: resetBehavior is exactly one of
        // Default | Partial | Persistent, with Persistent the storage name for
        // the Domain's NoReset. No other token exists.
        var storeName = nameof(PassiveDefinition_ShouldRoundTripAllThreeResetBehaviorTokens);
        var tokens = new[] { "Default", "Partial", "Persistent" };

        await using (var context = CreateContext(storeName))
        {
            for (var i = 0; i < tokens.Length; i++)
            {
                context.BossDefinitions.Add(
                    NewDefinition(
                        $"boss-def-reset-{i}",
                        $"boss-reset-{i}",
                        passiveId: $"boss-reset-{i}",
                        resetBehavior: tokens[i]));
            }

            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var stored = await context.BossDefinitions
                .OrderBy(d => d.BossDefinitionId)
                .Select(d => d.PassiveDefinition.ResetBehavior)
                .ToListAsync();

            Assert.Equal(tokens, stored);
        }
    }

    [Fact]
    public async Task SkillDefinition_ShouldRoundTripAllFourDocumentedMembers()
    {
        // DATABASE.md §1 note item 4: exactly skillId, baseDamage,
        // chargeRequirement, cooldownTurns — all required, no other member.
        var storeName = nameof(SkillDefinition_ShouldRoundTripAllFourDocumentedMembers);

        await using (var context = CreateContext(storeName))
        {
            context.BossDefinitions.Add(
                NewDefinition(
                    "boss-def-moc-yeu",
                    "boss-moc-yeu",
                    Element.Moc,
                    passiveId: "boss-moc-yeu-regen",
                    skillId: "root",
                    baseDamage: 100,
                    chargeRequirement: 6,
                    cooldownTurns: 2));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var stored = await context.BossDefinitions.SingleAsync();

            Assert.Equal("root", stored.SkillDefinition.SkillId);
            Assert.Equal(100, stored.SkillDefinition.BaseDamage);
            Assert.Equal(6, stored.SkillDefinition.ChargeRequirement);
            Assert.Equal(2, stored.SkillDefinition.CooldownTurns);
        }
    }

    [Fact]
    public async Task StoredDocuments_ShouldCarryExactlyTheDocumentedJsonMembers()
    {
        // DATABASE.md §1 note items 3–4 fix the member names, and note item 3
        // states "the JSON/storage names are the contract; internal
        // representation maps to them, not vice versa". The InMemory provider
        // stores no column text, so the stored document is asserted through the
        // persistence converter itself — the component that owns the mapping —
        // which is the same code path a relational write uses.
        var storeName = nameof(StoredDocuments_ShouldCarryExactlyTheDocumentedJsonMembers);

        await using (var context = CreateContext(storeName))
        {
            context.BossDefinitions.Add(NewDefinition("boss-def-hoa-long", "boss-hoa-long"));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var stored = await context.BossDefinitions.SingleAsync();

            var passiveJson = BossDefinitionJson.WritePassive(stored.PassiveDefinition);
            var skillJson = BossDefinitionJson.WriteSkill(stored.SkillDefinition);

            Assert.Equal(
                new[] { "passiveId", "resetBehavior", "threshold" },
                JsonDocument.Parse(passiveJson).RootElement.EnumerateObject()
                    .Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());

            Assert.Equal(
                new[] { "baseDamage", "chargeRequirement", "cooldownTurns", "skillId" },
                JsonDocument.Parse(skillJson).RootElement.EnumerateObject()
                    .Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        }
    }

    [Fact]
    public async Task StoredPassiveDocument_ShouldSerialiseAnAlwaysActiveThresholdAsJsonNull()
    {
        // The wire form of the case above: the documented `null` — never `0` —
        // is what the column holds for an always-active Passive
        // (DATABASE.md §1 note item 3).
        var storeName = nameof(StoredPassiveDocument_ShouldSerialiseAnAlwaysActiveThresholdAsJsonNull);

        await using (var context = CreateContext(storeName))
        {
            context.BossDefinitions.Add(
                NewDefinition(
                    "boss-def-thuy-ma",
                    "boss-thuy-ma",
                    Element.Thuy,
                    passiveId: "boss-thuy-ma-heal",
                    threshold: null));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var stored = await context.BossDefinitions.SingleAsync();
            var json = BossDefinitionJson.WritePassive(stored.PassiveDefinition);

            var threshold = JsonDocument.Parse(json).RootElement.GetProperty("threshold");

            Assert.Equal(JsonValueKind.Null, threshold.ValueKind);
            Assert.DoesNotContain("\"threshold\":0", json);
        }
    }

    [Fact]
    public async Task Identity_ShouldBeEnforcedUnique()
    {
        // DATABASE.md §3: BossDefinition.Identity is UNIQUE. The InMemory
        // provider does not raise on a duplicate unique index, so the documented
        // invariant is asserted directly: at most one row may carry an Identity,
        // which is what makes §1's FK lookup unambiguous.
        var storeName = nameof(Identity_ShouldBeEnforcedUnique);

        await using (var context = CreateContext(storeName))
        {
            context.BossDefinitions.Add(NewDefinition("boss-def-one", "boss-shared-identity"));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var matching = await context.BossDefinitions
                .Where(d => d.BossId == new BossId("boss-shared-identity"))
                .CountAsync();

            Assert.Equal(1, matching);
        }

        var model = CreateDesignTimeModel(nameof(Identity_ShouldBeEnforcedUnique));
        var index = model.FindEntityType(typeof(BossDefinition))!.GetIndexes().Single();
        Assert.True(index.IsUnique, "Identity is the documented unique FK-lookup target (DATABASE.md §3).");
    }

    [Fact]
    public async Task BossDefinition_ShouldNeverBeProvisionedByTheModel()
    {
        // DATABASE.md §1 note item 5: the decided mechanism is a migration-level
        // InsertData (ProvisionBossDefinitions), which is NOT model seed data.
        // The model must still carry no seed of its own — no HasData, no
        // startup loader — so a fresh context returns zero rows and
        // GetSeedData() stays empty. That separation is exactly what this
        // guard pins, and it must keep passing unmodified.
        var storeName = nameof(BossDefinition_ShouldNeverBeProvisionedByTheModel);

        await using (var context = CreateContext(storeName))
        {
            Assert.Empty(await context.BossDefinitions.ToListAsync());
        }

        var model = CreateDesignTimeModel(nameof(BossDefinition_ShouldNeverBeProvisionedByTheModel));
        var entity = model.FindEntityType(typeof(BossDefinition))!;

        Assert.Empty(entity.GetSeedData());
    }

    // -----------------------------------------------------------------------
    // Provisioning migration source — DATABASE.md §1 note item 5, §5 item 4
    // (TASK-053)
    //
    // The decided mechanism is migration-level InsertData, so the provisioning
    // contract is asserted against the migration source — the same convention
    // the AddBossPersistence schema pin above uses. Live applied-row
    // verification is covered by BossDefinitionPostgresProvisioningTests, which
    // is skipped when no PostgreSQL is reachable.
    // -----------------------------------------------------------------------

    /// <summary>
    /// A migration's source with line and XML-documentation comments removed,
    /// so an assertion targets the executable operations only. The provisioning
    /// migration documents the contract (including the forbidden mechanism names
    /// and the Boss rules it transcribes) in its comments, and a whole-file
    /// substring scan would otherwise flag that documentation.
    /// </summary>
    private static string ReadMigrationOperations(string fileNameSuffix)
    {
        var source = ReadMigrationSource(fileNameSuffix);
        var builder = new System.Text.StringBuilder();

        foreach (var rawLine in source.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            // Drop XML-documentation and ordinary comment lines. The migration
            // bodies contain no string literal that begins with "///" or "//",
            // so this cannot hide an operation.
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
    /// The five canonical <c>BossDefinitionId</c> values of
    /// <c>DATABASE.md</c> §1 note item 2, taken from the Domain definitions so
    /// the assertion cannot drift from the single authoritative source.
    /// </summary>
    private static readonly string[] CanonicalBossDefinitionIds =
        BossDefinitions.All.Select(d => d.BossDefinitionId).ToArray();

    /// <summary>
    /// The five canonical <c>Identity</c> values of <c>BOSS_RULES.md</c> §6.4,
    /// taken from the Domain definitions.
    /// </summary>
    private static readonly string[] CanonicalBossIdentities =
        BossDefinitions.All.Select(d => d.BossId.Value).ToArray();

    /// <summary>
    /// The <c>BossDefinitionId</c> values the <b>TASK-053</b> migration
    /// provisioned — the three Bosses content-defined at that time.
    /// </summary>
    private static readonly string[] FirstThreeBossDefinitionIds =
        ["boss-def-hoa-long", "boss-def-thuy-ma", "boss-def-moc-yeu"];

    /// <summary>
    /// The two <c>BossDefinitionId</c> values the <b>TASK-172</b> provisioning
    /// migration adds — the Bosses TASK-172 authored content for.
    /// </summary>
    private static readonly string[] TwoRemainingBossDefinitionIds =
        ["boss-def-son-thach-ve", "boss-def-kim-loi-vuong"];

    /// <summary>
    /// The Domain definitions the <c>ProvisionBossDefinitions</c> migration owns
    /// — the three Bosses content-defined when TASK-053 ran. The migrations are
    /// separate, so a TASK-053-scoped assertion is scoped to its own row set.
    /// </summary>
    private static BossDefinition[] FirstThreeDefinitions =>
        BossDefinitions.All
            .Where(d => FirstThreeBossDefinitionIds.Contains(d.BossDefinitionId, StringComparer.Ordinal))
            .ToArray();

    /// <summary>
    /// The Domain definitions the TASK-172 provisioning migration owns — the two
    /// Bosses it adds.
    /// </summary>
    private static BossDefinition[] TwoRemainingDefinitions =>
        BossDefinitions.All
            .Where(d => TwoRemainingBossDefinitionIds.Contains(d.BossDefinitionId, StringComparer.Ordinal))
            .ToArray();

    [Fact]
    public void ProvisioningMigration_ShouldInsertExactlyTheThreeCanonicalRows()
    {
        // DATABASE.md §1 note item 5 "Row set — exactly three rows provisioned" as
        // it stood under TASK-053: ProvisionBossDefinitions owns exactly the three
        // BossDefinitionId values that were content-defined then. That migration is
        // historical and is asserted to be unchanged — the two rows TASK-172 made
        // provisionable belong to the separate later provisioning migration, not to
        // this one.
        var migration = ReadMigrationSource("ProvisionBossDefinitions");

        Assert.Equal(
            new[] { "boss-def-hoa-long", "boss-def-moc-yeu", "boss-def-thuy-ma" },
            FirstThreeBossDefinitionIds.OrderBy(v => v, StringComparer.Ordinal).ToArray());

        foreach (var canonicalId in FirstThreeBossDefinitionIds)
        {
            Assert.Contains(canonicalId, migration);
        }

        foreach (var canonicalIdentity in new[] { "boss-hoa-long", "boss-thuy-ma", "boss-moc-yeu" })
        {
            Assert.Contains(canonicalIdentity, migration);
        }

        Assert.Equal(3, CountOccurrences(migration, "migrationBuilder.InsertData("));

        // The two later Bosses are NOT this migration's row set.
        foreach (var laterId in TwoRemainingBossDefinitionIds)
        {
            Assert.DoesNotContain(laterId, migration);
        }
    }

    [Fact]
    public void ProvisioningMigration_ShouldProvisionNoPlaceholderRow()
    {
        // DATABASE.md §1 note item 5 "Row set": the 5-Boss figure is the MVP
        // scope target (MVP_SCOPE.md §1), not permission to create placeholder
        // rows for undefined content. Every row this migration inserts must be
        // one of the three Bosses that were content-defined when it ran.
        var migration = ReadMigrationOperations("ProvisionBossDefinitions");

        // The three InsertData values plus the three Down DeleteData keys.
        Assert.Equal(6, CountOccurrences(migration, "boss-def-"));

        // No placeholder row for a Boss that has no content definition.
        foreach (var forbidden in new[] { "placeholder", "boss-def-placeholder", "TODO" })
        {
            Assert.DoesNotContain(forbidden, migration, StringComparison.OrdinalIgnoreCase);
        }

        // Every boss-def-* literal that appears is one of the three canonical
        // rows — no fourth key is slipped in.
        foreach (var literal in BossDefinitionIdLiterals(migration))
        {
            Assert.Contains(literal, FirstThreeBossDefinitionIds);
        }
    }

    /// <summary>
    /// Every <c>boss-def-…</c> string literal occurring in a migration source,
    /// deduplicated — used to prove a migration's row set contains no key
    /// outside the set it owns.
    /// </summary>
    private static string[] BossDefinitionIdLiterals(string migration)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(
            migration,
            "\"(boss-def-[a-z0-9-]+)\"");

        return matches
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToArray();
    }

    [Fact]
    public void ProvisioningMigration_ShouldContainNoSchemaOperation()
    {
        // DATABASE.md §1 note item 5 / §5 item 1: this migration is data only.
        // The table, PK, unique Identity index, and all NOT NULL constraints
        // were created by AddBossPersistence and are untouched — no column,
        // index, or constraint change belongs here.
        var migration = ReadMigrationOperations("ProvisionBossDefinitions");

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
            Assert.DoesNotContain(schemaOperation, migration);
        }

        // The only operations present are the three inserts and three deletes.
        Assert.Equal(6, CountOccurrences(migration, "migrationBuilder."));

        // No model seeding either — that mechanism is forbidden outright.
        Assert.DoesNotContain("HasData", migration);
    }

    [Fact]
    public void ProvisioningMigration_ShouldMirrorEveryInsertWithADelete()
    {
        // DATABASE.md §1 note item 5: the migration inserts each row at most
        // once and its Down must remove exactly those rows, keyed by the same
        // canonical BossDefinitionId values (the PK).
        var migration = ReadMigrationOperations("ProvisionBossDefinitions");

        Assert.Equal(3, CountOccurrences(migration, "migrationBuilder.InsertData("));
        Assert.Equal(3, CountOccurrences(migration, "migrationBuilder.DeleteData("));
        Assert.Equal(6, CountOccurrences(migration, "table: \"BossDefinition\""));

        // Every Down delete is keyed by one of the canonical PK values.
        foreach (var canonicalId in FirstThreeBossDefinitionIds)
        {
            Assert.Contains($"keyValue: \"{canonicalId}\"", migration);
        }
    }

    [Fact]
    public void ProvisioningMigration_ShouldWriteTheDocumentedPassiveAndSkillDocuments()
    {
        // DATABASE.md §1 note items 3–4 fix the JSON member lists, and note item
        // 3 states the JSON/storage names are the contract. The inserted text is
        // produced by BossDefinitionJson over the authoritative Domain
        // definitions, so the migration and the converters cannot drift; this
        // asserts the exact document each row must carry.
        var migration = ReadMigrationOperations("ProvisionBossDefinitions");

        foreach (var definition in FirstThreeDefinitions)
        {
            var passiveJson = BossDefinitionJson.WritePassive(definition.PassiveDefinition);
            var skillJson = BossDefinitionJson.WriteSkill(definition.SkillDefinition);

            Assert.Contains(PassAsCSharpStringLiteral(passiveJson), migration);
            Assert.Contains(PassAsCSharpStringLiteral(skillJson), migration);
        }
    }

    [Fact]
    public void ProvisioningMigration_ShouldPreserveTheAlwaysActiveNullThreshold()
    {
        // DATABASE.md §1 note item 3 / §3: threshold = null ⇔ no match-charging
        // threshold (NOT always-active), and "0 is never used as a 'no threshold'
        // sentinel" (BOSS_RULES.md §6.2's Thủy Ma). The inserted document must
        // carry JSON null, not 0 — and the two match-charged Bosses must still
        // carry their real threshold 5.
        var migration = ReadMigrationOperations("ProvisionBossDefinitions");

        var notCharged = FirstThreeDefinitions
            .Single(d => d.PassiveDefinition.Threshold is null);

        Assert.Equal("boss-thuy-ma", notCharged.BossId.Value);

        var notChargedJson = BossDefinitionJson.WritePassive(notCharged.PassiveDefinition);
        Assert.Contains("\"threshold\":null", notChargedJson);
        Assert.Contains(PassAsCSharpStringLiteral(notChargedJson), migration);

        // No row may encode the non-match-charged case as `0`. Exactly the two
        // match-charged Bosses carry their documented thresholds (Hỏa Long = 5,
        // Mộc Yêu = 8 per TASK-200); only Thủy Ma carries null in THIS migration.
        Assert.Equal(1, CountOccurrences(
            migration,
            PassAsCSharpStringLiteral("\"threshold\":5")));
        Assert.Equal(1, CountOccurrences(
            migration,
            PassAsCSharpStringLiteral("\"threshold\":8")));
        Assert.Equal(1, CountOccurrences(
            migration,
            PassAsCSharpStringLiteral("\"threshold\":null")));
        Assert.DoesNotContain(PassAsCSharpStringLiteral("\"threshold\":0"), migration);
    }

    [Fact]
    public void ProvisioningMigration_ShouldWriteNoDisplayNameAndNoCombatStat()
    {
        // DATABASE.md §1 note items 1–2: the three-way contract
        // BossDefinitionId ≠ Identity ≠ display name holds, and the display
        // name is presentation content that is never a column and never
        // written. MaxHP/ATK/DEF/EnrageThreshold are likewise not columns
        // (BOSS_RULES.md §6.1).
        var migration = ReadMigrationOperations("ProvisionBossDefinitions");

        foreach (var displayName in new[] { "Hỏa Long", "Thủy Ma", "Mộc Yêu" })
        {
            Assert.DoesNotContain(displayName, migration);
        }

        foreach (var forbidden in new[] { "MaxHP", "\"ATK\"", "\"DEF\"", "EnrageThreshold", "DisplayName" })
        {
            Assert.DoesNotContain(forbidden, migration);
        }

        // Both non-display identities are present and distinct.
        foreach (var definition in FirstThreeDefinitions)
        {
            Assert.Contains(definition.BossDefinitionId, migration);
            Assert.Contains(definition.BossId.Value, migration);
            Assert.NotEqual(definition.BossDefinitionId, definition.BossId.Value);
        }

        // The inserted column set is exactly the five documented columns.
        Assert.Equal(
            3,
            CountOccurrences(
                migration,
                "columns: new[] { \"BossDefinitionId\", \"Identity\", \"Element\", \"PassiveDefinition\", \"SkillDefinition\" }"));
    }

    [Fact]
    public void ProvisioningMigration_ShouldEncodeElementAsTheDomainEnumValue()
    {
        // DATABASE.md §1: Element is stored as the Domain Element enum's
        // integer representation (BossDefinitionConfiguration's
        // HasConversion<int>()), so the inserted integer must equal the enum
        // value the Domain definitions carry — never a hand-guessed number.
        var migration = ReadMigrationOperations("ProvisionBossDefinitions");

        foreach (var definition in FirstThreeDefinitions)
        {
            var expectedValue = (int)definition.Element;

            Assert.Contains(
                $"\"{definition.BossDefinitionId}\", \"{definition.BossId.Value}\", {expectedValue},",
                migration);
        }

        // The five documented Element members, for reference in case the enum
        // is ever renumbered: Hỏa Long = Hỏa (3), Thủy Ma = Thủy (2),
        // Mộc Yêu = Mộc (0), Sơn Thạch Vệ = Thổ (1), Kim Lôi Vương = Kim (4).
        Assert.Equal(3, (int)Element.Hoa);
        Assert.Equal(2, (int)Element.Thuy);
        Assert.Equal(0, (int)Element.Moc);
        Assert.Equal(1, (int)Element.Tho);
        Assert.Equal(4, (int)Element.Kim);
    }

    // -----------------------------------------------------------------------
    // TASK-172 provisioning migration — DATABASE.md §1 note item 5, §5 item 4
    //
    // The two remaining content-defined MVP Bosses are provisioned by their own
    // same-mechanism, data-only migration, exactly as TASK-168's second Card/Pet
    // migration followed TASK-085's. The TASK-053 migration above is asserted to
    // be unchanged and to still own its three rows only.
    // -----------------------------------------------------------------------

    private const string Task172ProvisioningMigration = "ProvisionTwoRemainingMvpBosses";

    [Fact]
    public void Task172ProvisioningMigration_ShouldInsertExactlyTheTwoRemainingRows()
    {
        // DATABASE.md §1 note item 2's canonical BossDefinitionId values and
        // BOSS_RULES.md §6.4's Identities for Sơn Thạch Vệ and Kim Lôi Vương.
        var migration = ReadMigrationSource(Task172ProvisioningMigration);

        Assert.Equal(2, TwoRemainingDefinitions.Length);

        foreach (var definition in TwoRemainingDefinitions)
        {
            Assert.Contains(definition.BossDefinitionId, migration);
            Assert.Contains(definition.BossId.Value, migration);
        }

        Assert.Equal(2, CountOccurrences(migration, "migrationBuilder.InsertData("));
        Assert.Equal(2, CountOccurrences(migration, "migrationBuilder.DeleteData("));

        // The three TASK-053 rows are NOT re-provisioned, re-keyed, or removed
        // by this migration — no duplicate insert and no clobbering delete.
        foreach (var earlierId in FirstThreeBossDefinitionIds)
        {
            Assert.DoesNotContain(earlierId, migration);
        }
    }

    [Fact]
    public void Task172ProvisioningMigration_ShouldContainNoSchemaOperation()
    {
        // DATABASE.md §1 note item 5 / §5 item 1: this migration is data only,
        // like TASK-053's. No column, table, index, constraint, or migration
        // SQL statement belongs in it.
        var migration = ReadMigrationOperations(Task172ProvisioningMigration);

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
            Assert.DoesNotContain(schemaOperation, migration);
        }

        // Exactly the two inserts and two deletes — nothing else.
        Assert.Equal(4, CountOccurrences(migration, "migrationBuilder."));
        Assert.Equal(4, CountOccurrences(migration, "table: \"BossDefinition\""));

        // No model seeding either — that mechanism is forbidden outright.
        Assert.DoesNotContain("HasData", migration);
    }

    [Fact]
    public void Task172ProvisioningMigration_ShouldWriteTheDocumentedPassiveAndSkillDocuments()
    {
        // DATABASE.md §1 note items 3–4 fix the JSON member lists. The stored
        // text must equal what BossDefinitionJson writes over the authoritative
        // Domain definitions, so the migration and the converters cannot drift.
        var migration = ReadMigrationOperations(Task172ProvisioningMigration);

        foreach (var definition in TwoRemainingDefinitions)
        {
            var passiveJson = BossDefinitionJson.WritePassive(definition.PassiveDefinition);
            var skillJson = BossDefinitionJson.WriteSkill(definition.SkillDefinition);

            Assert.Contains(PassAsCSharpStringLiteral(passiveJson), migration);
            Assert.Contains(PassAsCSharpStringLiteral(skillJson), migration);
        }
    }

    [Fact]
    public void Task172ProvisioningMigration_ShouldStoreNullThresholdAndTheDocumentedResetBehavior()
    {
        // BOSS_RULES.md §6.2 / §6.2.4 / §6.2.5: BOTH new Passives are
        // non-match-charged — Sơn Thạch Vệ's trigger is "Boss HP ≤ 50%" and
        // Kim Lôi Vương's is "Player Combo ≥ 4" — so each stores JSON null,
        // never the 0 sentinel (DATABASE.md §1 note item 3, §3). `null` means no
        // match-charging threshold; it is NOT always-active.
        //
        // §6.2.4 authors Sơn Thạch Vệ's Passive one-time / no re-trigger after
        // activation: the non-default No reset / persistent form
        // (PASSIVE_RULES.md §4 items 2–3), whose storage token is the existing
        // `Persistent`. Kim Lôi Vương's is the documented `Default`.
        var migration = ReadMigrationOperations(Task172ProvisioningMigration);

        var sonThachVe = TwoRemainingDefinitions.Single(d => d.BossId.Value == "boss-son-thach-ve");
        var kimLoiVuong = TwoRemainingDefinitions.Single(d => d.BossId.Value == "boss-kim-loi-vuong");

        Assert.Null(sonThachVe.PassiveDefinition.Threshold);
        Assert.Null(kimLoiVuong.PassiveDefinition.Threshold);

        // Reset Behavior: the one documented non-default override, and the
        // documented default.
        Assert.Equal("Persistent", sonThachVe.PassiveDefinition.ResetBehavior);
        Assert.Equal("Default", kimLoiVuong.PassiveDefinition.ResetBehavior);

        // Neither row may be encoded with the forbidden `0` sentinel.
        Assert.Equal(2, CountOccurrences(
            migration,
            PassAsCSharpStringLiteral("\"threshold\":null")));
        Assert.DoesNotContain(PassAsCSharpStringLiteral("\"threshold\":0"), migration);

        // No new Reset Behavior token is introduced: each stored value is one of
        // the three documented tokens (DATABASE.md §1 note item 3).
        foreach (var definition in TwoRemainingDefinitions)
        {
            Assert.Contains(
                definition.PassiveDefinition.ResetBehavior,
                new[] { "Default", "Partial", "Persistent" });
        }
    }

    [Fact]
    public void Task172ProvisioningMigration_ShouldWriteNoDisplayNameAndNoCombatStat()
    {
        // DATABASE.md §1 note items 1–2: the display name is presentation content
        // and is never a column, and MaxHP/ATK/DEF/EnrageThreshold are not columns
        // (BOSS_RULES.md §6.1). Only the five documented columns appear.
        var migration = ReadMigrationOperations(Task172ProvisioningMigration);

        // The display names are never written — only the technical identities.
        foreach (var displayName in new[] { "Sơn Thạch Vệ", "Kim Lôi Vương" })
        {
            Assert.DoesNotContain(displayName, migration);
        }

        foreach (var forbidden in new[] { "MaxHP", "\"ATK\"", "\"DEF\"", "EnrageThreshold", "DisplayName" })
        {
            Assert.DoesNotContain(forbidden, migration);
        }

        // Both non-display identities are present and distinct.
        foreach (var definition in TwoRemainingDefinitions)
        {
            Assert.Contains(definition.BossDefinitionId, migration);
            Assert.Contains(definition.BossId.Value, migration);
            Assert.NotEqual(definition.BossDefinitionId, definition.BossId.Value);
        }

        // The inserted column set is exactly the five documented columns.
        Assert.Equal(
            2,
            CountOccurrences(
                migration,
                "columns: new[] { \"BossDefinitionId\", \"Identity\", \"Element\", \"PassiveDefinition\", \"SkillDefinition\" }"));
    }

    [Fact]
    public void Task172ProvisioningMigration_ShouldEncodeElementAsTheDomainEnumValue()
    {
        // DATABASE.md §1: Element is stored as the Domain Element enum's integer
        // representation. BOSS_RULES.md §6.1 gives Sơn Thạch Vệ Thổ and
        // Kim Lôi Vương Kim, so the inserted integers must be those enum values.
        var migration = ReadMigrationOperations(Task172ProvisioningMigration);

        foreach (var definition in TwoRemainingDefinitions)
        {
            var expectedValue = (int)definition.Element;

            Assert.Contains(
                $"\"{definition.BossDefinitionId}\", \"{definition.BossId.Value}\", {expectedValue},",
                migration);
        }
    }

    /// <summary>
    /// The C# source form of a JSON document as it appears inside the
    /// migration's <c>values:</c> array — every <c>"</c> escaped, exactly as
    /// the literal is written.
    /// </summary>
    private static string PassAsCSharpStringLiteral(string json) =>
        json.Replace("\"", "\\\"", StringComparison.Ordinal);

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
