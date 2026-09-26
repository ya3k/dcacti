using GameServer.Domain.Relics;
using GameServer.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// Relic and RelicDefinition persistence — <c>DATABASE.md</c> §1, §2, §4
/// (TASK-027).
///
/// What is verified is the documented contract: the two field sets, the
/// ownership and definition relationships, the single
/// <c>Relic(PlayerId)</c> index, and — critically — the <b>absence</b> of any
/// persistent equip table or equip column (<c>DATABASE.md</c> §2,
/// ADR-012 item 7).
/// </summary>
public class RelicPersistenceTests
{
    private static GameDbContext CreateContext(string storeName) =>
        TestGameDbContextFactory.Create(storeName);

    private static IModel CreateDesignTimeModel(string storeName)
    {
        using var context = CreateContext(storeName);
        return context.GetService<IDesignTimeModel>().Model;
    }

    private static RelicDefinition NewDefinition(string id) => new()
    {
        RelicDefinitionId = id,
        Name = "Berserker Core",
        Trigger = "OnMatchCount",
        Condition = "every 3 Matches",
        EffectDefinition = "atk_plus_5_percent",
    };

    private static Relic NewRelic(string instanceId, string playerId, string definitionId) => new()
    {
        RelicInstanceId = instanceId,
        PlayerId = playerId,
        RelicDefinitionId = definitionId,
        AcquiredAt = DateTimeOffset.UtcNow,
    };

    // -----------------------------------------------------------------------
    // DATABASE.md §1 — the field sets
    // -----------------------------------------------------------------------

    [Fact]
    public void Relic_ShouldCarryExactlyTheDocumentedFields()
    {
        // DATABASE.md §1 defines Relic as RelicInstanceId (PK), PlayerId (FK),
        // RelicDefinitionId (FK), and AcquiredAt. The field list is closed:
        // there is no equip/slot column and no per-instance Relic state.
        var fields = typeof(Relic)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "AcquiredAt", "PlayerId", "RelicDefinitionId", "RelicInstanceId" },
            fields);
    }

    [Fact]
    public void Relic_ShouldExposeNoEquipOrLoadoutColumn()
    {
        // DATABASE.md §2: "There is likewise no persistent Relic-equip table:
        // Player owns Relic instances; which 3–5 are equipped for a given
        // battle is request-time loadout only" (RELIC_RULES.md §2 item 1,
        // ADR-012 item 7). The instance row therefore carries no equip state.
        var fields = typeof(Relic).GetProperties().Select(p => p.Name).ToArray();

        foreach (var forbidden in new[]
                 {
                     "Equipped", "IsEquipped", "EquipSlot", "SlotIndex", "Slot",
                     "Loadout", "BattleId", "PetInstanceId", "PetId",
                 })
        {
            Assert.DoesNotContain(forbidden, fields);
        }
    }

    [Fact]
    public void Relic_ShouldExposeNoRelicEffectOrTriggerState()
    {
        // RELIC_RULES.md §4–§5 (trigger order, anti-chain) are out of
        // TASK-027's scope. The instance carries no trigger/effect/stack
        // state: Trigger, Condition, and Effect are the DEFINITION's
        // (DATABASE.md §1), read through the FK, and no MVP Relic in
        // RELIC_RULES.md §6 has per-instance state.
        var fields = typeof(Relic).GetProperties().Select(p => p.Name).ToArray();

        foreach (var forbidden in new[]
                 {
                     "Trigger", "Condition", "Effect", "EffectDefinition",
                     "Stacks", "Cooldown", "Charges", "TriggerState",
                 })
        {
            Assert.DoesNotContain(forbidden, fields);
        }
    }

    [Fact]
    public void RelicDefinition_ShouldCarryExactlyTheDocumentedFields()
    {
        // DATABASE.md §1: RelicDefinitionId (PK), Name, Trigger, Condition,
        // EffectDefinition (RELIC_RULES.md §1's Relic structure).
        var fields = typeof(RelicDefinition)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "Condition", "EffectDefinition", "Name", "RelicDefinitionId", "Trigger" },
            fields);
    }

    // -----------------------------------------------------------------------
    // DATABASE.md §1/§2 — mapping and relationships
    // -----------------------------------------------------------------------

    [Fact]
    public void Model_ShouldMapRelicToTheDocumentedTable()
    {
        var model = CreateDesignTimeModel(nameof(Model_ShouldMapRelicToTheDocumentedTable));

        var entity = model.FindEntityType(typeof(Relic));

        Assert.NotNull(entity);
        Assert.Equal("Relic", entity!.GetTableName());
        Assert.Equal("RelicInstanceId", entity.FindPrimaryKey()!.Properties.Single().Name);
    }

    [Fact]
    public void Model_ShouldMapRelicDefinitionToTheDocumentedTable()
    {
        var model = CreateDesignTimeModel(nameof(Model_ShouldMapRelicDefinitionToTheDocumentedTable));

        var entity = model.FindEntityType(typeof(RelicDefinition));

        Assert.NotNull(entity);
        Assert.Equal("RelicDefinition", entity!.GetTableName());
        Assert.Equal(
            "RelicDefinitionId",
            entity.FindPrimaryKey()!.Properties.Single().Name);
    }

    [Fact]
    public void Model_ShouldCreateForeignKeysForPlayerAndDefinition()
    {
        // DATABASE.md §2: Player 1 ── N Relic; Relic N ── 1 RelicDefinition.
        var model = CreateDesignTimeModel(nameof(Model_ShouldCreateForeignKeysForPlayerAndDefinition));

        var entity = model.FindEntityType(typeof(Relic))!;
        var foreignKeys = entity.GetForeignKeys().ToArray();

        Assert.Equal(2, foreignKeys.Length);
        Assert.Contains(foreignKeys, fk =>
            fk.PrincipalEntityType?.ClrType == typeof(Domain.Players.Player));
        Assert.Contains(foreignKeys, fk =>
            fk.PrincipalEntityType?.ClrType == typeof(RelicDefinition));
    }

    [Fact]
    public void Model_ShouldDeclareExactlyTheOneDocumentedRelicIndex()
    {
        // DATABASE.md §4: Relic(PlayerId) — "list a player's Relics". It is the
        // only documented Relic index; no further indexes are specified, and
        // the RelicDefinitionId FK must NOT gain an implicit index
        // (ForeignKeyIndexConvention is removed by GameDbContext — the
        // TASK-024 behavior, not reintroduced).
        var model = CreateDesignTimeModel(nameof(Model_ShouldDeclareExactlyTheOneDocumentedRelicIndex));

        var entity = model.FindEntityType(typeof(Relic))!;
        var indexes = entity.GetIndexes().ToArray();

        var playerIdIndex = indexes
            .Single(i => i.Properties.Any(p => p.Name == nameof(Relic.PlayerId)));

        Assert.False(playerIdIndex.IsUnique, "Relic(PlayerId) is a non-unique collection index.");
        Assert.Equal(1, indexes.Length);
    }

    [Fact]
    public void Model_ShouldNotImposeUniquenessOnPlayerAndDefinition()
    {
        // RELIC_RULES.md §2.4 item 3: two DISTINCT owned instances that
        // reference the same RelicDefinitionId MAY be equipped simultaneously.
        // A uniqueness constraint on PlayerId + RelicDefinitionId would
        // contradict that rule, so none may exist.
        var model = CreateDesignTimeModel(nameof(Model_ShouldNotImposeUniquenessOnPlayerAndDefinition));

        var entity = model.FindEntityType(typeof(Relic))!;

        var compositeUnique = entity.GetIndexes().Any(index =>
            index.IsUnique &&
            index.Properties.Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .SequenceEqual(
                    new[] { nameof(Relic.PlayerId), nameof(Relic.RelicDefinitionId) }
                        .OrderBy(n => n, StringComparer.Ordinal)));

        Assert.False(compositeUnique);

        // And the definition FK is not unique on its own either.
        Assert.DoesNotContain(
            entity.GetIndexes(),
            index => index.IsUnique &&
                     index.Properties.Any(p => p.Name == nameof(Relic.RelicDefinitionId)));
    }

    // -----------------------------------------------------------------------
    // DATABASE.md §1/§2 — no persistent equip table
    // -----------------------------------------------------------------------

    [Fact]
    public void Model_ShouldIntroduceNoPersistentEquipTable()
    {
        // DATABASE.md §2 / ADR-012 item 7: no persistent Relic-equip table.
        // RELIC_RULES.md §2 item 1: equipment "is selected at battle start and
        // exists only for that battle".
        //
        // CardDefinition and PlayerUnlockedCard are the Card ownership/content
        // tables added by TASK-028 (DATABASE.md §1–§2) — static Card content and
        // Player unlock flags. BossDefinition is the static Boss content table
        // added by TASK-044 (DATABASE.md §1). No equip/loadout table exists for
        // any system: Card equipment is battle-scoped exactly as Relic
        // equipment is (DATABASE.md §2, ADR-012 item 10).
        var model = CreateDesignTimeModel(nameof(Model_ShouldIntroduceNoPersistentEquipTable));

        var tables = model.GetEntityTypes()
            .Select(e => e.GetTableName())
            .Where(name => name is not null)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "BossDefinition", "CardDefinition", "Pet", "PetDefinition",
                "Player", "PlayerUnlockedCard", "Relic", "RelicDefinition",
            },
            tables.OrderBy(n => n, StringComparer.Ordinal).ToArray());
    }

    // -----------------------------------------------------------------------
    // Persistence round-trip
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RelicDefinition_ShouldPersist()
    {
        var storeName = nameof(RelicDefinition_ShouldPersist);

        await using (var context = CreateContext(storeName))
        {
            context.RelicDefinitions.Add(NewDefinition("relic_def_1"));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var stored = await context.RelicDefinitions.SingleAsync();

            Assert.Equal("relic_def_1", stored.RelicDefinitionId);
            Assert.Equal("Berserker Core", stored.Name);
            Assert.Equal("OnMatchCount", stored.Trigger);
            Assert.Equal("every 3 Matches", stored.Condition);
            Assert.Equal("atk_plus_5_percent", stored.EffectDefinition);
        }
    }

    [Fact]
    public async Task RelicDefinition_ShouldAllowAnAbsentCondition()
    {
        // RELIC_RULES.md §1: the Condition is optional ("plus an optional
        // Condition"). "Burning Curse" in §6 declares none.
        var storeName = nameof(RelicDefinition_ShouldAllowAnAbsentCondition);

        await using (var context = CreateContext(storeName))
        {
            context.RelicDefinitions.Add(new RelicDefinition
            {
                RelicDefinitionId = "relic_def_burning",
                Name = "Burning Curse",
                Trigger = "OnDamageDealt",
                Condition = null,
                EffectDefinition = "burn_damage_plus_30_percent",
            });
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var stored = await context.RelicDefinitions.SingleAsync();

            Assert.Null(stored.Condition);
        }
    }

    [Fact]
    public async Task Relic_ShouldPersistWithItsOwnershipAndDefinitionRelationships()
    {
        // DATABASE.md §1/§2: Relic references both Player (ownership) and
        // RelicDefinition (content).
        var storeName = nameof(Relic_ShouldPersistWithItsOwnershipAndDefinitionRelationships);

        await using (var context = CreateContext(storeName))
        {
            context.Players.Add(new Domain.Players.Player
            {
                PlayerId = "player_1",
                DiscordUserId = "discord_1",
                Level = 1,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            context.RelicDefinitions.Add(NewDefinition("relic_def_1"));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            context.Relics.Add(NewRelic("relic_1", "player_1", "relic_def_1"));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var stored = await context.Relics.SingleAsync();

            Assert.Equal("relic_1", stored.RelicInstanceId);
            Assert.Equal("player_1", stored.PlayerId);
            Assert.Equal("relic_def_1", stored.RelicDefinitionId);
        }
    }

    [Fact]
    public async Task Relic_ShouldAllowTwoInstancesOfOneDefinition()
    {
        // RELIC_RULES.md §2.4 item 3: multiple distinct owned instances of the
        // SAME RelicDefinition may be equipped simultaneously. The storage
        // model must therefore permit several Relic rows sharing one
        // RelicDefinitionId for one Player.
        var storeName = nameof(Relic_ShouldAllowTwoInstancesOfOneDefinition);

        await using (var context = CreateContext(storeName))
        {
            context.Players.Add(new Domain.Players.Player
            {
                PlayerId = "player_1",
                DiscordUserId = "discord_1",
                Level = 1,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            context.RelicDefinitions.Add(NewDefinition("relic_def_fire"));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            context.Relics.Add(NewRelic("relic_a", "player_1", "relic_def_fire"));
            context.Relics.Add(NewRelic("relic_b", "player_1", "relic_def_fire"));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var stored = await context.Relics.ToListAsync();

            Assert.Equal(2, stored.Count);
            Assert.All(stored, r => Assert.Equal("relic_def_fire", r.RelicDefinitionId));
            Assert.Equal(
                new[] { "relic_a", "relic_b" },
                stored.Select(r => r.RelicInstanceId).OrderBy(n => n, StringComparer.Ordinal));
        }
    }
}
