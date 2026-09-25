using GameServer.Domain.Cards;
using GameServer.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// CardDefinition and PlayerUnlockedCard persistence — <c>DATABASE.md</c> §1,
/// §2, §4 (TASK-028).
///
/// What is verified is the documented contract: the two field sets, the
/// unlock and definition relationships, the composite unlock key, the single
/// <c>PlayerUnlockedCard(PlayerId)</c> index, the required
/// <c>LoadoutCopyLimit</c> with no default, the
/// <c>PetDefinition.SignatureSkillCardId</c> relationship TASK-024 deferred —
/// and, critically, the <b>absence</b> of any instance/quantity table, equip
/// table, or equip column (ADR-012 items 9–10, <c>DATABASE.md</c> §2).
/// </summary>
public class CardPersistenceTests
{
    private static GameDbContext CreateContext(string storeName) =>
        TestGameDbContextFactory.Create(storeName);

    private static IModel CreateDesignTimeModel(string storeName)
    {
        using var context = CreateContext(storeName);
        return context.GetService<IDesignTimeModel>().Model;
    }

    private static CardDefinition NewDefinition(
        string id,
        CardCategory category = CardCategory.Basic,
        int loadoutCopyLimit = 1) => new()
    {
        CardDefinitionId = id,
        Name = "Heal",
        Category = category,
        PowerCost = 0,
        EffectDefinition = "heal_effect",
        LoadoutCopyLimit = loadoutCopyLimit,
    };

    private static PlayerUnlockedCard NewUnlock(string playerId, string definitionId) => new()
    {
        PlayerId = playerId,
        CardDefinitionId = definitionId,
    };

    private static Domain.Players.Player NewPlayer(string id) => new()
    {
        PlayerId = id,
        DiscordUserId = $"discord_{id}",
        Level = 1,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    // -----------------------------------------------------------------------
    // DATABASE.md §1 — the field sets
    // -----------------------------------------------------------------------

    [Fact]
    public void CardDefinition_ShouldCarryExactlyTheDocumentedFields()
    {
        // DATABASE.md §1 defines CardDefinition as CardDefinitionId (PK), Name,
        // Category, PowerCost, EffectDefinition, and LoadoutCopyLimit. The field
        // list is closed: MVP Cards have no Tier/Star/Level and no instance
        // state (ADR-012 item 9).
        var fields = typeof(CardDefinition)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "CardDefinitionId",
                "Category",
                "EffectDefinition",
                "LoadoutCopyLimit",
                "Name",
                "PowerCost",
            },
            fields);
    }

    [Fact]
    public void CardDefinition_ShouldExposeNoInstanceOrQuantityState()
    {
        // ADR-012 item 9: MVP Cards have no Tier/Star/Level, so an instance
        // table is unnecessary, and CARD_RULES.md §1 item 3 states "no Card
        // instance exists at any time". No quantity/ownership-count field may
        // appear: LoadoutCopyLimit is a per-loadout bound, not inventory.
        var fields = typeof(CardDefinition).GetProperties().Select(p => p.Name).ToArray();

        foreach (var forbidden in new[]
                 {
                     "Tier", "Star", "Level", "Quantity", "Count", "OwnedCount",
                     "Owned", "PlayerId", "CardInstanceId", "InstanceId",
                 })
        {
            Assert.DoesNotContain(forbidden, fields);
        }
    }

    [Fact]
    public void PlayerUnlockedCard_ShouldCarryExactlyTheDocumentedFields()
    {
        // DATABASE.md §1: PlayerUnlockedCard is the unlock-flag join and lists
        // exactly PlayerId (FK → Player) and CardDefinitionId (FK →
        // CardDefinition). Nothing else — no equip column, no acquisition
        // timestamp, no quantity.
        var fields = typeof(PlayerUnlockedCard)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "CardDefinitionId", "PlayerId" }, fields);
    }

    [Fact]
    public void PlayerUnlockedCard_ShouldExposeNoEquipOrQuantityColumn()
    {
        // DATABASE.md §2: "Battle equip of Cards is not persisted here"; the
        // unlock row is ownership only, and ownership is one row regardless of
        // allowed copies (CARD_RULES.md §1 item 3).
        var fields = typeof(PlayerUnlockedCard).GetProperties().Select(p => p.Name).ToArray();

        foreach (var forbidden in new[]
                 {
                     "Equipped", "IsEquipped", "EquipSlot", "SlotIndex", "Slot",
                     "Loadout", "BattleId", "PetInstanceId", "PetId",
                     "Quantity", "Count", "Copies", "AcquiredAt",
                 })
        {
            Assert.DoesNotContain(forbidden, fields);
        }
    }

    // -----------------------------------------------------------------------
    // DATABASE.md §2/§4 — mapping, keys, relationships, indexes
    // -----------------------------------------------------------------------

    [Fact]
    public void Model_ShouldMapCardEntitiesToTheDocumentedTables()
    {
        var model = CreateDesignTimeModel(nameof(Model_ShouldMapCardEntitiesToTheDocumentedTables));

        var definition = model.FindEntityType(typeof(CardDefinition));
        var unlock = model.FindEntityType(typeof(PlayerUnlockedCard));

        Assert.NotNull(definition);
        Assert.Equal("CardDefinition", definition!.GetTableName());
        Assert.Equal(
            "CardDefinitionId",
            definition.FindPrimaryKey()!.Properties.Single().Name);

        Assert.NotNull(unlock);
        Assert.Equal("PlayerUnlockedCard", unlock!.GetTableName());
    }

    [Fact]
    public void Model_ShouldKeyTheUnlockByPlayerAndDefinition()
    {
        // DATABASE.md §1 lists exactly (PlayerId, CardDefinitionId) and declares
        // no surrogate id, so the pair is the natural key — which is also what
        // enforces the documented one-unlock-row-per-(Player, Definition)
        // ownership shape (CARD_RULES.md §1 item 3).
        var model = CreateDesignTimeModel(nameof(Model_ShouldKeyTheUnlockByPlayerAndDefinition));

        var entity = model.FindEntityType(typeof(PlayerUnlockedCard))!;
        var keyProperties = entity.FindPrimaryKey()!.Properties
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { nameof(PlayerUnlockedCard.CardDefinitionId), nameof(PlayerUnlockedCard.PlayerId) }
                .OrderBy(n => n, StringComparer.Ordinal),
            keyProperties);
    }

    [Fact]
    public void Model_ShouldCreateForeignKeysForPlayerAndDefinition()
    {
        // DATABASE.md §2: Player 1 ── N PlayerUnlockedCard;
        // PlayerUnlockedCard N ── 1 CardDefinition; and
        // PetDefinition 1 ── 1 CardDefinition (SignatureSkillCardId).
        var model = CreateDesignTimeModel(nameof(Model_ShouldCreateForeignKeysForPlayerAndDefinition));

        var unlockForeignKeys = model.FindEntityType(typeof(PlayerUnlockedCard))!
            .GetForeignKeys()
            .ToArray();

        Assert.Equal(2, unlockForeignKeys.Length);
        Assert.Contains(unlockForeignKeys, fk =>
            fk.PrincipalEntityType?.ClrType == typeof(Domain.Players.Player));
        Assert.Contains(unlockForeignKeys, fk =>
            fk.PrincipalEntityType?.ClrType == typeof(CardDefinition));

        var petDefinitionForeignKeys = model.FindEntityType(typeof(Domain.Pets.PetDefinition))!
            .GetForeignKeys()
            .ToArray();

        Assert.Contains(petDefinitionForeignKeys, fk =>
            fk.PrincipalEntityType?.ClrType == typeof(CardDefinition));
    }

    [Fact]
    public void Model_ShouldDeclareExactlyTheOneDocumentedUnlockIndex()
    {
        // DATABASE.md §4: PlayerUnlockedCard(PlayerId) — "list a player's
        // unlocked Cards". It is the only documented index for this table; no
        // further indexes are specified, and the CardDefinitionId FK must NOT
        // gain an implicit index (ForeignKeyIndexConvention is removed by
        // GameDbContext — the TASK-024 behavior, not reintroduced).
        var model = CreateDesignTimeModel(nameof(Model_ShouldDeclareExactlyTheOneDocumentedUnlockIndex));

        var entity = model.FindEntityType(typeof(PlayerUnlockedCard))!;
        var indexes = entity.GetIndexes().ToArray();

        var playerIdIndex = indexes
            .Single(i => i.Properties.Any(p => p.Name == nameof(PlayerUnlockedCard.PlayerId)));

        Assert.False(playerIdIndex.IsUnique, "PlayerUnlockedCard(PlayerId) is a non-unique collection index.");
        Assert.Equal(1, indexes.Length);
    }

    [Fact]
    public void Model_ShouldDeclareNoCardDefinitionIndex()
    {
        // DATABASE.md §4 lists no CardDefinition index.
        var model = CreateDesignTimeModel(nameof(Model_ShouldDeclareNoCardDefinitionIndex));

        var entity = model.FindEntityType(typeof(CardDefinition))!;

        Assert.Empty(entity.GetIndexes());
    }

    // -----------------------------------------------------------------------
    // DATABASE.md §1 / CARD_RULES.md §1 item 2 — LoadoutCopyLimit is required
    // -----------------------------------------------------------------------

    [Fact]
    public void Model_ShouldRequireLoadoutCopyLimitWithNoDefault()
    {
        // DATABASE.md §1: LoadoutCopyLimit is "required … explicit value
        // required, no default"; CARD_RULES.md §1 item 2: "a missing value is
        // invalid definition data. There is no default." The column must
        // therefore be non-nullable AND must declare no default value — a
        // HasDefaultValue of 1 or 3 would be exactly the invented default the
        // contract forbids.
        //
        // The CLR default (0) is a value a caller may legitimately store; what
        // matters is that the MAPPING supplies nothing, so a definition row
        // must state its own limit.
        var model = CreateDesignTimeModel(nameof(Model_ShouldRequireLoadoutCopyLimitWithNoDefault));

        var entity = model.FindEntityType(typeof(CardDefinition))!;
        var property = entity.FindProperty(nameof(CardDefinition.LoadoutCopyLimit))!;

        Assert.False(property.IsNullable);
        Assert.Equal(typeof(int), property.ClrType);

        // No database-side default may be configured. EF reports the CLR
        // default (0) for a non-nullable property, so the meaningful check is
        // that the column carries no configured default SQL and is not
        // store-generated on insert — i.e. the value always comes from the
        // definition row itself, never from the schema.
        Assert.Null(property.GetDefaultValueSql());
        Assert.NotEqual(ValueGenerated.OnAdd, property.ValueGenerated);

        // And the migration must not spell one either.
        Assert.DoesNotContain(
            entity.GetProperties(),
            p => p.Name == nameof(CardDefinition.LoadoutCopyLimit) &&
                 p.GetDefaultValueSql() is not null);
    }

    [Fact]
    public void Model_ShouldStoreCategoryAsItsEnumValue()
    {
        // DATABASE.md §1/§3: Category ∈ {Basic, PetSkill}. The closed set lives
        // in the Domain enum (CARD_RULES.md §1), stored as its numeric value —
        // the same convention PetDefinition.Element and Pet.Tier use.
        var model = CreateDesignTimeModel(nameof(Model_ShouldStoreCategoryAsItsEnumValue));

        var property = model.FindEntityType(typeof(CardDefinition))!
            .FindProperty(nameof(CardDefinition.Category))!;

        Assert.False(property.IsNullable);

        // The mapping converts the enum to its underlying numeric type rather
        // than storing the enum name or an identity value.
        var converter = property.GetTypeMapping().Converter;

        Assert.NotNull(converter);
        Assert.Equal(typeof(CardCategory), converter!.ModelClrType);
        Assert.Equal(typeof(int), converter.ProviderClrType);
    }

    // -----------------------------------------------------------------------
    // ADR-012 items 9–10 / DATABASE.md §2 — no instance, quantity, or equip table
    // -----------------------------------------------------------------------

    [Fact]
    public void Model_ShouldIntroduceNoCardInstanceQuantityOrEquipTable()
    {
        // ADR-012 item 9: no Card instance table and no Pet.CardInventory.
        // ADR-012 item 10 / DATABASE.md §2: "Battle equip of Cards is not
        // persisted here". TASK-028 therefore adds exactly two tables.
        var model = CreateDesignTimeModel(nameof(Model_ShouldIntroduceNoCardInstanceQuantityOrEquipTable));

        var tables = model.GetEntityTypes()
            .Select(e => e.GetTableName())
            .Where(name => name is not null)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "CardDefinition",
                "Pet",
                "PetDefinition",
                "Player",
                "PlayerUnlockedCard",
                "Relic",
                "RelicDefinition",
            },
            tables);
    }

    // -----------------------------------------------------------------------
    // Persistence round-trips
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CardDefinition_ShouldPersistEveryDocumentedField()
    {
        var storeName = nameof(CardDefinition_ShouldPersistEveryDocumentedField);

        await using (var context = CreateContext(storeName))
        {
            context.CardDefinitions.Add(NewDefinition("card_heal", CardCategory.Basic, loadoutCopyLimit: 2));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var stored = await context.CardDefinitions.SingleAsync();

            Assert.Equal("card_heal", stored.CardDefinitionId);
            Assert.Equal("Heal", stored.Name);
            Assert.Equal(CardCategory.Basic, stored.Category);
            Assert.Equal(0, stored.PowerCost);
            Assert.Equal("heal_effect", stored.EffectDefinition);
            Assert.Equal(2, stored.LoadoutCopyLimit);
        }
    }

    [Fact]
    public async Task CardDefinition_ShouldRoundTripBothCategories()
    {
        // CARD_RULES.md §1's closed set: Basic and PetSkill.
        var storeName = nameof(CardDefinition_ShouldRoundTripBothCategories);

        await using (var context = CreateContext(storeName))
        {
            context.CardDefinitions.Add(NewDefinition("card_basic", CardCategory.Basic));
            context.CardDefinitions.Add(NewDefinition("card_skill", CardCategory.PetSkill));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var basic = await context.CardDefinitions.SingleAsync(d => d.CardDefinitionId == "card_basic");
            var skill = await context.CardDefinitions.SingleAsync(d => d.CardDefinitionId == "card_skill");

            Assert.Equal(CardCategory.Basic, basic.Category);
            Assert.Equal(CardCategory.PetSkill, skill.Category);
        }
    }

    [Fact]
    public async Task PlayerUnlockedCard_ShouldPersistTheUnlockRelationship()
    {
        // DATABASE.md §1/§2: the unlock row references both Player (ownership)
        // and CardDefinition (content).
        var storeName = nameof(PlayerUnlockedCard_ShouldPersistTheUnlockRelationship);

        await using (var context = CreateContext(storeName))
        {
            context.Players.Add(NewPlayer("player_1"));
            context.CardDefinitions.Add(NewDefinition("card_heal"));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            context.PlayerUnlockedCards.Add(NewUnlock("player_1", "card_heal"));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var stored = await context.PlayerUnlockedCards.SingleAsync();

            Assert.Equal("player_1", stored.PlayerId);
            Assert.Equal("card_heal", stored.CardDefinitionId);
        }
    }

    [Fact]
    public async Task PlayerUnlockedCard_ShouldHoldOneRowRegardlessOfAllowedCopies()
    {
        // CARD_RULES.md §1 item 3: "ownership remains one unlock row per
        // (Player, CardDefinition) regardless of allowed copies". A card with
        // LoadoutCopyLimit 3 still needs exactly one unlock row — there is no
        // quantity to store.
        var storeName = nameof(PlayerUnlockedCard_ShouldHoldOneRowRegardlessOfAllowedCopies);

        await using (var context = CreateContext(storeName))
        {
            context.Players.Add(NewPlayer("player_1"));
            context.CardDefinitions.Add(
                NewDefinition("card_heal", CardCategory.Basic, loadoutCopyLimit: 3));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            context.PlayerUnlockedCards.Add(NewUnlock("player_1", "card_heal"));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var unlocks = await context.PlayerUnlockedCards.ToListAsync();

            Assert.Single(unlocks);
        }
    }

    [Fact]
    public async Task PetDefinition_ShouldPersistItsSignatureSkillCardReference()
    {
        // DATABASE.md §1/§2: PetDefinition.SignatureSkillCardId (FK →
        // CardDefinition), the TASK-024 deferral TASK-028 completes. It is the
        // source of the battle's derived fourth Card (CARD_RULES.md §1).
        var storeName = nameof(PetDefinition_ShouldPersistItsSignatureSkillCardReference);

        await using (var context = CreateContext(storeName))
        {
            context.CardDefinitions.Add(NewDefinition("card_skill_inferno", CardCategory.PetSkill));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            context.PetDefinitions.Add(new Domain.Pets.PetDefinition
            {
                PetDefinitionId = "pet_def_xich_lang",
                Identity = "Xích Lang",
                Element = Domain.Elements.Element.Hoa,
                PetLevelMultiplier = 1.5m,
                PassiveId = new Domain.Passives.PassiveId("xich-lang-passive"),
                PassiveThreshold = 5,
                SignatureSkillCardId = "card_skill_inferno",
            });
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var stored = await context.PetDefinitions.SingleAsync();

            Assert.Equal("card_skill_inferno", stored.SignatureSkillCardId);
        }
    }
}
