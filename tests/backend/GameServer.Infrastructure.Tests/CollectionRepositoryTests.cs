using GameServer.Application.Cards;
using GameServer.Application.Pets;
using GameServer.Application.Relics;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Domain.Relics;
using GameServer.Infrastructure.Postgres;
using GameServer.Infrastructure.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// The repository operations the collection read adds — <c>DATABASE.md</c> §4.
///
/// <code>
/// GET /api/pets     → IPetRepository.ListByPlayerIdAsync    + ListDefinitionsAsync
/// GET /api/cards    → ICardRepository.ListUnlockedAsync       (pre-existing)
/// GET /api/relics   → IRelicRepository.ListByPlayerIdAsync  + ListDefinitionsAsync
/// </code>
///
/// <b>What these tests establish.</b> That the two new operations are served by
/// the documented <c>Pet(PlayerId)</c> / <c>Relic(PlayerId)</c> indexes over the
/// existing tables, that the Player filter is applied in the query itself, and
/// that the bulk content reads resolve exactly the requested definitions. No
/// migration, no new index, and no new table is involved: these are queries on
/// the model TASK-024/TASK-027 already established.
///
/// <b>They are the real EF Core repositories</b> — the production
/// implementations over the same <c>GameDbContext</c> model the API host and
/// <c>dotnet ef</c> use — not doubles, so a predicate EF cannot translate would
/// fail here rather than in production.
/// </summary>
public class CollectionRepositoryTests
{
    private const string Owner = "player_owner";
    private const string OtherPlayer = "player_other";

    private static GameDbContext CreateContext(string storeName) =>
        TestGameDbContextFactory.Create(storeName);

    // -----------------------------------------------------------------------
    // IPetRepository.ListByPlayerIdAsync — DATABASE.md §4 Pet(PlayerId)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Pets_ListByPlayerId_ShouldReturnOnlyThatPlayersInstances()
    {
        var storeName = nameof(Pets_ListByPlayerId_ShouldReturnOnlyThatPlayersInstances);

        await SeedTwoPlayersAsync(storeName);

        await using var context = CreateContext(storeName);

        var pets = await new PetRepository(context).ListByPlayerIdAsync(Owner);

        // DATABASE.md §2: Player 1 ── N Pet, filtered by the requesting Player at
        // the query itself (GAME_RULES.md §18, ADR-001).
        Assert.Equal("pet_mine", Assert.Single(pets).PetInstanceId);

        // The other Player's instance really exists in the store, so this proves
        // the filter rather than an empty table.
        var theirs = await new PetRepository(context).ListByPlayerIdAsync(OtherPlayer);

        Assert.Equal("pet_theirs", Assert.Single(theirs).PetInstanceId);
    }

    [Fact]
    public async Task Pets_ListByPlayerId_ShouldReturnEveryOwnedInstance()
    {
        var storeName = nameof(Pets_ListByPlayerId_ShouldReturnEveryOwnedInstance);

        await using (var seed = CreateContext(storeName))
        {
            seed.Players.Add(NewPlayer(Owner));
            seed.PetDefinitions.Add(NewPetDefinition("def_1"));
            await seed.SaveChangesAsync();
        }

        await using (var seed = CreateContext(storeName))
        {
            for (var index = 0; index < 5; index++)
            {
                seed.Pets.Add(NewPet($"pet_{index}", Owner, "def_1"));
            }

            seed.Pets.Add(NewPet("pet_other", OtherPlayer, "def_1"));
            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext(storeName);

        var pets = await new PetRepository(context).ListByPlayerIdAsync(Owner);

        // API_CONTRACTS.md §5.5: "the array is the full collection" — no page, no
        // limit, no truncation.
        Assert.Equal(5, pets.Count);
        Assert.DoesNotContain(pets, pet => pet.PetInstanceId == "pet_other");
    }

    [Fact]
    public async Task Pets_ListByPlayerId_ShouldReturnNothingForAPlayerOwningNothing()
    {
        // API_CONTRACTS.md §5.5: an empty collection is represented by an empty
        // result, not by an error.
        var storeName = nameof(Pets_ListByPlayerId_ShouldReturnNothingForAPlayerOwningNothing);

        await SeedTwoPlayersAsync(storeName);

        await using var context = CreateContext(storeName);

        Assert.Empty(await new PetRepository(context).ListByPlayerIdAsync("player_nobody"));
    }

    [Fact]
    public async Task Pets_ListByPlayerId_ShouldReturnTheInstanceRowUnchanged()
    {
        // The boundary reports what the store holds: the read projects nothing
        // and derives no Level (PET_RULES.md §5.4 — the stored value is the
        // documented one).
        var storeName = nameof(Pets_ListByPlayerId_ShouldReturnTheInstanceRowUnchanged);

        await SeedTwoPlayersAsync(storeName);

        await using var context = CreateContext(storeName);

        var pet = Assert.Single(await new PetRepository(context).ListByPlayerIdAsync(Owner));

        Assert.Equal("pet_mine", pet.PetInstanceId);
        Assert.Equal(Owner, pet.PlayerId);
        Assert.Equal("def_mine", pet.PetDefinitionId);
        Assert.Equal(PetTier.Rare, pet.Tier);
        Assert.Equal(3, pet.Star);
        Assert.Equal(12, pet.Level);
    }

    // -----------------------------------------------------------------------
    // IPetRepository.ListDefinitionsAsync — the bulk content read
    // -----------------------------------------------------------------------

    [Fact]
    public async Task PetDefinitions_List_ShouldReturnExactlyTheRequestedDefinitions()
    {
        // API_CONTRACTS.md §5.1: `identity` and `element` come from the
        // definition, so the collection projection resolves the owned instances'
        // definitions in one read.
        var storeName = nameof(PetDefinitions_List_ShouldReturnExactlyTheRequestedDefinitions);

        await using (var seed = CreateContext(storeName))
        {
            seed.PetDefinitions.Add(NewPetDefinition("def_a", "Thanh Xà", Element.Moc));
            seed.PetDefinitions.Add(NewPetDefinition("def_b", "Xích Lang", Element.Hoa));
            seed.PetDefinitions.Add(NewPetDefinition("def_c", "Sơn Hùng", Element.Tho));
            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext(storeName);

        var definitions = await new PetRepository(context)
            .ListDefinitionsAsync(new[] { "def_a", "def_c" });

        Assert.Equal(
            new[] { "def_a", "def_c" },
            definitions
                .Select(definition => definition.PetDefinitionId)
                .OrderBy(id => id, StringComparer.Ordinal));

        // The unrequested definition really exists, so this proves the set filter
        // rather than an empty table.
        Assert.DoesNotContain(definitions, definition => definition.PetDefinitionId == "def_b");
    }

    [Fact]
    public async Task PetDefinitions_List_ShouldOmitAnIdentityWithNoRow()
    {
        // A requested identity that resolves to nothing is simply absent — no
        // placeholder definition is fabricated (AGENTS.md §7).
        var storeName = nameof(PetDefinitions_List_ShouldOmitAnIdentityWithNoRow);

        await using (var seed = CreateContext(storeName))
        {
            seed.PetDefinitions.Add(NewPetDefinition("def_a"));
            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext(storeName);

        var definitions = await new PetRepository(context)
            .ListDefinitionsAsync(new[] { "def_a", "def_missing" });

        Assert.Equal("def_a", Assert.Single(definitions).PetDefinitionId);
    }

    [Fact]
    public async Task PetDefinitions_List_ShouldCarryTheProjectedContent()
    {
        var storeName = nameof(PetDefinitions_List_ShouldCarryTheProjectedContent);

        await using (var seed = CreateContext(storeName))
        {
            seed.PetDefinitions.Add(NewPetDefinition("def_xich_lang", "Xích Lang", Element.Hoa));
            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext(storeName);

        var definition = Assert.Single(
            await new PetRepository(context).ListDefinitionsAsync(new[] { "def_xich_lang" }));

        Assert.Equal("Xích Lang", definition.Identity);
        Assert.Equal(Element.Hoa, definition.Element);
    }

    // -----------------------------------------------------------------------
    // IRelicRepository.ListByPlayerIdAsync — DATABASE.md §4 Relic(PlayerId)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Relics_ListByPlayerId_ShouldReturnOnlyThatPlayersInstances()
    {
        var storeName = nameof(Relics_ListByPlayerId_ShouldReturnOnlyThatPlayersInstances);

        await using (var seed = CreateContext(storeName))
        {
            seed.Players.Add(NewPlayer(Owner));
            seed.Players.Add(NewPlayer(OtherPlayer));
            seed.RelicDefinitions.Add(NewRelicDefinition("relic_def_1"));
            await seed.SaveChangesAsync();
        }

        await using (var seed = CreateContext(storeName))
        {
            seed.Relics.Add(NewRelic("relic_mine", Owner, "relic_def_1"));
            seed.Relics.Add(NewRelic("relic_theirs", OtherPlayer, "relic_def_1"));
            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext(storeName);

        // DATABASE.md §2: Player 1 ── N Relic, filtered by the requesting Player
        // at the query itself.
        var relics = await new RelicRepository(context).ListByPlayerIdAsync(Owner);

        Assert.Equal("relic_mine", Assert.Single(relics).RelicInstanceId);
    }

    [Fact]
    public async Task Relics_ListByPlayerId_ShouldReturnBothInstancesOfOneDefinition()
    {
        // RELIC_RULES.md §2.4 item 3: two distinct owned instances may reference
        // one definition, so both are listed with their own instance identities.
        var storeName = nameof(Relics_ListByPlayerId_ShouldReturnBothInstancesOfOneDefinition);

        await using (var seed = CreateContext(storeName))
        {
            seed.Players.Add(NewPlayer(Owner));
            seed.RelicDefinitions.Add(NewRelicDefinition("relic_def_1"));
            await seed.SaveChangesAsync();
        }

        await using (var seed = CreateContext(storeName))
        {
            seed.Relics.Add(NewRelic("relic_a", Owner, "relic_def_1"));
            seed.Relics.Add(NewRelic("relic_b", Owner, "relic_def_1"));
            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext(storeName);

        var relics = await new RelicRepository(context).ListByPlayerIdAsync(Owner);

        Assert.Equal(
            new[] { "relic_a", "relic_b" },
            relics
                .Select(relic => relic.RelicInstanceId)
                .OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Relics_ListByPlayerId_ShouldReturnNothingForAPlayerOwningNothing()
    {
        // API_CONTRACTS.md §5.5: empty → an empty result.
        var storeName = nameof(Relics_ListByPlayerId_ShouldReturnNothingForAPlayerOwningNothing);

        await using (var seed = CreateContext(storeName))
        {
            seed.Players.Add(NewPlayer(Owner));
            seed.RelicDefinitions.Add(NewRelicDefinition("relic_def_1"));
            await seed.SaveChangesAsync();
        }

        await using (var seed = CreateContext(storeName))
        {
            seed.Relics.Add(NewRelic("relic_mine", Owner, "relic_def_1"));
            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext(storeName);

        Assert.Empty(await new RelicRepository(context).ListByPlayerIdAsync("player_nobody"));
    }

    // -----------------------------------------------------------------------
    // IRelicRepository.ListDefinitionsAsync — the bulk content read
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RelicDefinitions_List_ShouldReturnExactlyTheRequestedDefinitions()
    {
        // API_CONTRACTS.md §5.4: `name` comes from the definition, so the
        // collection projection resolves the owned instances' definitions in one
        // read.
        var storeName = nameof(RelicDefinitions_List_ShouldReturnExactlyTheRequestedDefinitions);

        await using (var seed = CreateContext(storeName))
        {
            seed.RelicDefinitions.Add(NewRelicDefinition("relic_def_berserker", "Berserker Core"));
            seed.RelicDefinitions.Add(NewRelicDefinition("relic_def_mana", "Mana Crystal"));
            seed.RelicDefinitions.Add(NewRelicDefinition("relic_def_third", "Third"));
            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext(storeName);

        var definitions = await new RelicRepository(context)
            .ListDefinitionsAsync(new[] { "relic_def_berserker", "relic_def_third" });

        Assert.Equal(
            new[] { "relic_def_berserker", "relic_def_third" },
            definitions
                .Select(definition => definition.RelicDefinitionId)
                .OrderBy(id => id, StringComparer.Ordinal));

        Assert.DoesNotContain(
            definitions,
            definition => definition.RelicDefinitionId == "relic_def_mana");
    }

    [Fact]
    public async Task RelicDefinitions_List_ShouldCarryTheProjectedName()
    {
        var storeName = nameof(RelicDefinitions_List_ShouldCarryTheProjectedName);

        await using (var seed = CreateContext(storeName))
        {
            seed.RelicDefinitions.Add(NewRelicDefinition("relic_def_1", "Berserker Core"));
            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext(storeName);

        var definition = Assert.Single(
            await new RelicRepository(context).ListDefinitionsAsync(new[] { "relic_def_1" }));

        Assert.Equal("Berserker Core", definition.Name);
    }

    [Fact]
    public async Task RelicDefinitions_List_ShouldOmitAnIdentityWithNoRow()
    {
        // A requested identity that resolves to nothing is simply absent — no
        // placeholder definition is fabricated (AGENTS.md §7).
        var storeName = nameof(RelicDefinitions_List_ShouldOmitAnIdentityWithNoRow);

        await using (var seed = CreateContext(storeName))
        {
            seed.RelicDefinitions.Add(NewRelicDefinition("relic_def_1"));
            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext(storeName);

        var definitions = await new RelicRepository(context)
            .ListDefinitionsAsync(new[] { "relic_def_1", "relic_def_missing" });

        Assert.Equal("relic_def_1", Assert.Single(definitions).RelicDefinitionId);
    }

    // -----------------------------------------------------------------------
    // ICardRepository.ListUnlockedAsync — the pre-existing unlock read
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Cards_ListUnlocked_ShouldJoinOnlyTheCallersUnlockRows()
    {
        // API_CONTRACTS.md §5.3: membership is the Player's PlayerUnlockedCard
        // rows joined to their definitions — "presence in this array IS the
        // unlocked state" (ADR-012 item 9: no Card instance exists).
        var storeName = nameof(Cards_ListUnlocked_ShouldJoinOnlyTheCallersUnlockRows);

        await using (var seed = CreateContext(storeName))
        {
            seed.Players.Add(NewPlayer(Owner));
            seed.Players.Add(NewPlayer(OtherPlayer));
            seed.CardDefinitions.Add(NewCardDefinition("card_heal"));
            seed.CardDefinitions.Add(NewCardDefinition("card_shield"));
            seed.CardDefinitions.Add(NewCardDefinition("card_unowned"));
            await seed.SaveChangesAsync();
        }

        await using (var seed = CreateContext(storeName))
        {
            seed.PlayerUnlockedCards.Add(NewUnlock(Owner, "card_heal"));
            seed.PlayerUnlockedCards.Add(NewUnlock(OtherPlayer, "card_shield"));
            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext(storeName);

        var unlocked = await new CardRepository(context).ListUnlockedAsync(Owner);

        Assert.Equal("card_heal", Assert.Single(unlocked).CardDefinitionId);
    }

    // -----------------------------------------------------------------------
    // DATABASE.md §4 — no index or table was added for these reads
    // -----------------------------------------------------------------------

    [Fact]
    public void Model_ShouldStillDeclareExactlyTheThreeDocumentedCollectionIndexes()
    {
        // DATABASE.md §4 lists exactly three by-Player collection indexes, and
        // states that "no further indexes are specified — additional indexes
        // should be added only when a real query pattern requires them
        // (anti-overengineering)". The collection read is served by these three
        // over the existing tables, so it adds none.
        using var context = CreateContext(nameof(Model_ShouldStillDeclareExactlyTheThreeDocumentedCollectionIndexes));

        var model = context.GetService<IDesignTimeModel>().Model;

        foreach (var (entityType, ownerProperty) in new[]
                 {
                     (typeof(Pet), nameof(Pet.PlayerId)),
                     (typeof(Relic), nameof(Relic.PlayerId)),
                     (typeof(PlayerUnlockedCard), nameof(PlayerUnlockedCard.PlayerId)),
                 })
        {
            var indexes = model.FindEntityType(entityType)!.GetIndexes().ToArray();

            var ownerIndex = Assert.Single(indexes);

            Assert.Contains(ownerIndex.Properties, property => property.Name == ownerProperty);
            Assert.False(ownerIndex.IsUnique);
        }

        // And the definition tables gain no index from the bulk content reads:
        // DATABASE.md §4 lists none for PetDefinition or RelicDefinition.
        Assert.Empty(model.FindEntityType(typeof(PetDefinition))!.GetIndexes());
        Assert.Empty(model.FindEntityType(typeof(RelicDefinition))!.GetIndexes());
        Assert.Empty(model.FindEntityType(typeof(CardDefinition))!.GetIndexes());
    }

    // -----------------------------------------------------------------------
    // Fixtures
    // -----------------------------------------------------------------------

    private static async Task SeedTwoPlayersAsync(string storeName)
    {
        await using (var seed = CreateContext(storeName))
        {
            seed.Players.Add(NewPlayer(Owner));
            seed.Players.Add(NewPlayer(OtherPlayer));
            seed.PetDefinitions.Add(NewPetDefinition("def_mine", "Mine", Element.Moc));
            seed.PetDefinitions.Add(NewPetDefinition("def_theirs", "Theirs", Element.Kim));
            await seed.SaveChangesAsync();
        }

        await using (var seed = CreateContext(storeName))
        {
            seed.Pets.Add(NewPet("pet_mine", Owner, "def_mine", PetTier.Rare, star: 3, level: 12));
            seed.Pets.Add(NewPet("pet_theirs", OtherPlayer, "def_theirs"));
            await seed.SaveChangesAsync();
        }
    }

    private static Player NewPlayer(string playerId) => new()
    {
        PlayerId = playerId,
        DiscordUserId = $"discord_{playerId}",
        Level = Player.InitialLevel,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static PetDefinition NewPetDefinition(
        string petDefinitionId,
        string identity = "Pet",
        Element element = Element.Moc) => new()
    {
        PetDefinitionId = petDefinitionId,
        Identity = identity,
        Element = element,
        PassiveId = new PassiveId("test-passive"),
        PassiveThreshold = 5,
        SignatureSkillCardId = "card_skill_test",
    };

    private static Pet NewPet(
        string petInstanceId,
        string playerId,
        string petDefinitionId,
        PetTier tier = PetTier.Common,
        int star = 1,
        int level = 1) => new()
    {
        PetInstanceId = petInstanceId,
        PlayerId = playerId,
        PetDefinitionId = petDefinitionId,
        Tier = tier,
        Star = star,
        XP = Pet.InitialXp,
        Level = level,
        AcquiredAt = DateTimeOffset.UtcNow,
    };

    private static RelicDefinition NewRelicDefinition(
        string relicDefinitionId,
        string name = "Relic") => new()
    {
        RelicDefinitionId = relicDefinitionId,
        Name = name,
        Trigger = "OnTurnEnd",
        EffectDefinition = "effect",
    };

    private static Relic NewRelic(
        string relicInstanceId,
        string playerId,
        string relicDefinitionId) => new()
    {
        RelicInstanceId = relicInstanceId,
        PlayerId = playerId,
        RelicDefinitionId = relicDefinitionId,
        AcquiredAt = DateTimeOffset.UtcNow,
    };

    private static CardDefinition NewCardDefinition(string cardDefinitionId) => new()
    {
        CardDefinitionId = cardDefinitionId,
        Name = cardDefinitionId,
        Category = CardCategory.Basic,
        PowerCost = 0,
        EffectDefinition = "effect",
        LoadoutCopyLimit = 1,
    };

    private static PlayerUnlockedCard NewUnlock(string playerId, string cardDefinitionId) => new()
    {
        PlayerId = playerId,
        CardDefinitionId = cardDefinitionId,
    };
}
