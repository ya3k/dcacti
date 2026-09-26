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
/// <c>BossDefinition</c> persistence — <c>DATABASE.md</c> §1, §3 (TASK-044).
///
/// What is verified is the documented contract: the five persisted columns and
/// their exact PK/constraint semantics, the caller-supplied (never generated)
/// persistence key distinct from the canonical Identity, the two NOT NULL JSON
/// documents and their fixed member lists — including <c>threshold: null</c> for
/// an always-active Passive rather than the <c>0</c> sentinel — and the
/// <b>absence</b> of the combat-definition columns and of any provisioning.
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

        // Schema only: no provisioning of any kind (§1 note item 5, §5 item 4).
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
    /// Reads the <c>AddBossPersistence</c> migration source from the
    /// Infrastructure project so its generated schema can be asserted without a
    /// relational provider.
    /// </summary>
    private static string ReadAddBossPersistenceMigration()
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
            .EnumerateFiles(migrations, "*_AddBossPersistence.cs")
            .Single(file => !file.EndsWith(".Designer.cs", StringComparison.Ordinal));

        return File.ReadAllText(migrationFile);
    }

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
        // DATABASE.md §1 note item 5 / §5 item 4: no provisioning mechanism is
        // documented and none may be invented — no HasData, seed, startup
        // loader, or migration-inserted production row.
        var storeName = nameof(BossDefinition_ShouldNeverBeProvisionedByTheModel);

        await using (var context = CreateContext(storeName))
        {
            Assert.Empty(await context.BossDefinitions.ToListAsync());
        }

        var model = CreateDesignTimeModel(nameof(BossDefinition_ShouldNeverBeProvisionedByTheModel));
        var entity = model.FindEntityType(typeof(BossDefinition))!;

        Assert.Empty(entity.GetSeedData());
    }
}
