using GameServer.Application.Pets;
using GameServer.Application.Players;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Infrastructure.Postgres;
using GameServer.Infrastructure.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// The denormalized Pet Level recompute hook — <c>DATABASE.md</c> §1,
/// ADR-012 Consequences, <c>PET_RULES.md</c> §5 item 2.
///
/// <code>
/// Rule (ADR-012 Consequences / DATABASE.md §1)
///  ↓
/// Scenario (Given Player Level L and multiplier M stored on Pet.Level,
///           When either input changes, Then Pet.Level is rewritten to
///           clamp(floor(L × M), 1, 50))
///  ↓
/// Test
/// </code>
///
/// Both recompute paths are exercised: Player Level change (the TASK-033
/// Level-up integration point) and multiplier change.
/// </summary>
public class PetLevelRecomputeTests
{
    private static GameDbContext CreateContext(string storeName) =>
        TestGameDbContextFactory.Create(storeName);

    private static PetDefinition MakeDefinition(
        string id = "def_1",
        decimal multiplier = 1.5m) =>
        new()
        {
            PetDefinitionId = id,
            Identity = "Thanh Xà",
            Element = Element.Thuy,
            PetLevelMultiplier = multiplier,
            PassiveId = new PassiveId("thanh-xa-passive"),
            PassiveThreshold = 5,
            // DATABASE.md §1: the Pet's one Signature Skill Card FK, mapped by
            // TASK-028. The fixture supplies a reference; the Card content
            // itself is not required by these Level-derivation tests.
            SignatureSkillCardId = "card_thanh_xa_skill",
        };

    private static Player MakePlayer(string id = "player_1", int level = 1) =>
        new()
        {
            PlayerId = id,
            DiscordUserId = "discord_1",
            Level = level,
        };

    private static Pet MakePet(
        string instanceId = "pet_1",
        string playerId = "player_1",
        string definitionId = "def_1",
        int level = 1) =>
        new()
        {
            PetInstanceId = instanceId,
            PlayerId = playerId,
            PetDefinitionId = definitionId,
            Tier = PetTier.Common,
            Star = 1,
            Level = level,
            AcquiredAt = DateTimeOffset.UtcNow,
        };

    // -----------------------------------------------------------------------
    // RecomputeForPlayerAsync — the TASK-033 Level-up integration point
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RecomputeForPlayer_ShouldRewriteStoredLevel_WhenPlayerLevelChanges()
    {
        // Given a Player at Level 3 with an owned Pet at multiplier 1.5,
        // the stored snapshot is floor(3 × 1.5) = 4.
        // When the Player Level changes to 40 (outside battle),
        // Then Pet.Level is rewritten to clamp(floor(40 × 1.5), 1, 50) = 50.
        var storeName = nameof(RecomputeForPlayer_ShouldRewriteStoredLevel_WhenPlayerLevelChanges);
        await using var writeContext = CreateContext(storeName);

        writeContext.Players.Add(MakePlayer(level: 3));
        writeContext.PetDefinitions.Add(MakeDefinition(multiplier: 1.5m));
        writeContext.Pets.Add(MakePet(level: 4));
        await writeContext.SaveChangesAsync();

        // Simulate the Level-up path changing Player.Level (init-only setter
        // is bypassed via the EF property entry, as TASK-033 will do through
        // its own save path).
        await using var updateContext = CreateContext(storeName);
        var player = await updateContext.Players.SingleAsync(p => p.PlayerId == "player_1");
        updateContext.Entry(player).Property(p => p.Level).CurrentValue = 40;
        await updateContext.SaveChangesAsync();

        // When the recompute hook runs after the Level change.
        await using var recomputeContext = CreateContext(storeName);
        var service = new PetLevelService(
            new PetRepository(recomputeContext),
            new PlayerRepository(recomputeContext));

        await service.RecomputeForPlayerAsync("player_1");

        // Then the stored snapshot matches the canonical derivation.
        await using var assertContext = CreateContext(storeName);
        var pet = await assertContext.Pets.SingleAsync(p => p.PetInstanceId == "pet_1");

        Assert.Equal(50, pet.Level); // floor(40 × 1.5) = 60 → clamp = 50
    }

    [Fact]
    public async Task RecomputeForPlayer_ShouldApplyFloorNotRound()
    {
        // Given Player Level 3 and multiplier 1.5, floor(4.5) = 4 — never 5.
        var storeName = nameof(RecomputeForPlayer_ShouldApplyFloorNotRound);
        await using var writeContext = CreateContext(storeName);

        writeContext.Players.Add(MakePlayer(level: 3));
        writeContext.PetDefinitions.Add(MakeDefinition(multiplier: 1.5m));
        writeContext.Pets.Add(MakePet(level: 1)); // stale snapshot
        await writeContext.SaveChangesAsync();

        await using var recomputeContext = CreateContext(storeName);
        var service = new PetLevelService(
            new PetRepository(recomputeContext),
            new PlayerRepository(recomputeContext));

        await service.RecomputeForPlayerAsync("player_1");

        await using var assertContext = CreateContext(storeName);
        var pet = await assertContext.Pets.SingleAsync(p => p.PetInstanceId == "pet_1");

        Assert.Equal(4, pet.Level);
    }

    [Fact]
    public async Task RecomputeForPlayer_ShouldClampToTheDocumentedRange()
    {
        // Given a very high Player Level and a multiplier > 1, the stored
        // snapshot is clamped to 50 — never exceeds the documented range.
        var storeName = nameof(RecomputeForPlayer_ShouldClampToTheDocumentedRange);
        await using var writeContext = CreateContext(storeName);

        writeContext.Players.Add(MakePlayer(level: 50));
        writeContext.PetDefinitions.Add(MakeDefinition(multiplier: 3m));
        writeContext.Pets.Add(MakePet(level: 1)); // stale snapshot
        await writeContext.SaveChangesAsync();

        await using var recomputeContext = CreateContext(storeName);
        var service = new PetLevelService(
            new PetRepository(recomputeContext),
            new PlayerRepository(recomputeContext));

        await service.RecomputeForPlayerAsync("player_1");

        await using var assertContext = CreateContext(storeName);
        var pet = await assertContext.Pets.SingleAsync(p => p.PetInstanceId == "pet_1");

        Assert.Equal(50, pet.Level);
    }

    [Fact]
    public async Task RecomputeForPlayer_ShouldClampToTheLowerBound()
    {
        // Given a very low multiplier, floor(Player.Level × M) may fall
        // below 1 — the stored snapshot is clamped up to 1.
        var storeName = nameof(RecomputeForPlayer_ShouldClampToTheLowerBound);
        await using var writeContext = CreateContext(storeName);

        writeContext.Players.Add(MakePlayer(level: 1));
        writeContext.PetDefinitions.Add(MakeDefinition(multiplier: 0.5m));
        writeContext.Pets.Add(MakePet(level: 50)); // stale snapshot above bound
        await writeContext.SaveChangesAsync();

        await using var recomputeContext = CreateContext(storeName);
        var service = new PetLevelService(
            new PetRepository(recomputeContext),
            new PlayerRepository(recomputeContext));

        await service.RecomputeForPlayerAsync("player_1");

        await using var assertContext = CreateContext(storeName);
        var pet = await assertContext.Pets.SingleAsync(p => p.PetInstanceId == "pet_1");

        Assert.Equal(1, pet.Level); // floor(1 × 0.5) = 0 → clamp = 1
    }

    [Fact]
    public async Task RecomputeForPlayer_ShouldThrowWhenPlayerIsMissing()
    {
        // The snapshot cannot be derived without Player.Level.
        var storeName = nameof(RecomputeForPlayer_ShouldThrowWhenPlayerIsMissing);
        await using var writeContext = CreateContext(storeName);

        writeContext.PetDefinitions.Add(MakeDefinition());
        writeContext.Pets.Add(MakePet()); // references a Player that does not exist
        await writeContext.SaveChangesAsync();

        await using var recomputeContext = CreateContext(storeName);
        var service = new PetLevelService(
            new PetRepository(recomputeContext),
            new PlayerRepository(recomputeContext));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RecomputeForPlayerAsync("player_missing"));
    }

    // -----------------------------------------------------------------------
    // RecomputeForDefinitionAsync — the multiplier-change integration point
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RecomputeForDefinition_ShouldRewriteStoredLevel_WhenMultiplierChanges()
    {
        // Given Player Level 10 with multiplier 1.0 → stored snapshot = 10.
        // When the multiplier changes to 0.5 (outside battle),
        // Then Pet.Level is rewritten to clamp(floor(10 × 0.5), 1, 50) = 5.
        var storeName = nameof(RecomputeForDefinition_ShouldRewriteStoredLevel_WhenMultiplierChanges);
        await using var writeContext = CreateContext(storeName);

        writeContext.Players.Add(MakePlayer(level: 10));
        writeContext.PetDefinitions.Add(MakeDefinition(multiplier: 1.0m));
        writeContext.Pets.Add(MakePet(level: 10));
        await writeContext.SaveChangesAsync();

        // Simulate a configuration change to the multiplier.
        await using var updateContext = CreateContext(storeName);
        var definition = await updateContext.PetDefinitions
            .SingleAsync(d => d.PetDefinitionId == "def_1");
        updateContext.Entry(definition)
            .Property(d => d.PetLevelMultiplier)
            .CurrentValue = 0.5m;
        await updateContext.SaveChangesAsync();

        await using var recomputeContext = CreateContext(storeName);
        var service = new PetLevelService(
            new PetRepository(recomputeContext),
            new PlayerRepository(recomputeContext));

        await service.RecomputeForDefinitionAsync("def_1");

        await using var assertContext = CreateContext(storeName);
        var pet = await assertContext.Pets.SingleAsync(p => p.PetInstanceId == "pet_1");

        Assert.Equal(5, pet.Level);
    }

    [Fact]
    public async Task RecomputeForDefinition_ShouldThrowWhenDefinitionIsMissing()
    {
        // The snapshot cannot be derived without PetLevelMultiplier.
        var storeName = nameof(RecomputeForDefinition_ShouldThrowWhenDefinitionIsMissing);
        await using var writeContext = CreateContext(storeName);

        writeContext.Players.Add(MakePlayer());
        await writeContext.SaveChangesAsync();

        await using var recomputeContext = CreateContext(storeName);
        var service = new PetLevelService(
            new PetRepository(recomputeContext),
            new PlayerRepository(recomputeContext));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RecomputeForDefinitionAsync("def_missing"));
    }

    // -----------------------------------------------------------------------
    // Repository boundary — DATABASE.md §1–§2
    // -----------------------------------------------------------------------

    [Fact]
    public async Task PetRepository_ShouldPersistAndListByPlayerId()
    {
        var storeName = nameof(PetRepository_ShouldPersistAndListByPlayerId);
        await using var seedContext = CreateContext(storeName);

        seedContext.Players.Add(MakePlayer());
        seedContext.PetDefinitions.Add(MakeDefinition());
        await seedContext.SaveChangesAsync();

        await using var context = CreateContext(storeName);
        var repository = new PetRepository(context);

        await repository.AddAsync(MakePet(level: 4));

        var pets = await repository.ListByPlayerIdAsync("player_1");

        Assert.Single(pets);
        Assert.Equal(4, pets[0].Level);
    }

    [Fact]
    public async Task PetRepository_ShouldListByDefinitionId()
    {
        var storeName = nameof(PetRepository_ShouldListByDefinitionId);
        await using var seedContext = CreateContext(storeName);

        seedContext.Players.Add(MakePlayer());
        seedContext.PetDefinitions.Add(MakeDefinition(id: "def_a"));
        seedContext.PetDefinitions.Add(MakeDefinition(id: "def_b"));
        await seedContext.SaveChangesAsync();

        await using var context = CreateContext(storeName);
        var repository = new PetRepository(context);

        await repository.AddAsync(MakePet(instanceId: "pet_a", definitionId: "def_a"));
        await repository.AddAsync(MakePet(instanceId: "pet_b", definitionId: "def_b"));

        var petsForA = await repository.ListByDefinitionIdAsync("def_a");
        var petsForB = await repository.ListByDefinitionIdAsync("def_b");

        Assert.Single(petsForA);
        Assert.Equal("pet_a", petsForA[0].PetInstanceId);
        Assert.Single(petsForB);
        Assert.Equal("pet_b", petsForB[0].PetInstanceId);
    }

    [Fact]
    public async Task PetRepository_ShouldReadDefinitionMultiplier()
    {
        var storeName = nameof(PetRepository_ShouldReadDefinitionMultiplier);
        await using var seedContext = CreateContext(storeName);

        seedContext.PetDefinitions.Add(MakeDefinition(multiplier: 1.5m));
        await seedContext.SaveChangesAsync();

        await using var context = CreateContext(storeName);
        var repository = new PetRepository(context);

        var definition = await repository.GetDefinitionAsync("def_1");

        Assert.NotNull(definition);
        Assert.Equal(1.5m, definition!.PetLevelMultiplier);
    }

    [Fact]
    public async Task PetRepository_ShouldReturnNullForUnknownDefinition()
    {
        var storeName = nameof(PetRepository_ShouldReturnNullForUnknownDefinition);
        await using var context = CreateContext(storeName);
        var repository = new PetRepository(context);

        var definition = await repository.GetDefinitionAsync("def_missing");

        Assert.Null(definition);
    }
}
