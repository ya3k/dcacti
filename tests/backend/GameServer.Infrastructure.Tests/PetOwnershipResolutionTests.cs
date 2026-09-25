using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Infrastructure.Postgres;
using GameServer.Infrastructure.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// The Pet ownership-resolution read the battle-start path performs —
/// <c>API_CONTRACTS.md</c> §3 ("petId must be owned by the player — <c>PET_RULES.md</c> §2"),
/// <c>DATABASE.md</c> §1 (TASK-030).
///
/// <c>POST /api/battle/start</c> resolves the submitted <c>petId</c> to its owned
/// Pet instance and compares that instance's <c>PlayerId</c> with the requesting
/// Player. The read is deliberately <b>not</b> Player-filtered, so the caller can
/// distinguish "no such Pet" from "another Player's Pet" — both of which the
/// endpoint rejects, but neither of which this boundary should collapse into a
/// single absent result on the caller's behalf.
/// </summary>
public class PetOwnershipResolutionTests
{
    private const string PetOwner = "player_owner";
    private const string OtherPlayer = "player_other";
    private const string PetInstanceId = "pet_instance_1";
    private const string PetDefinitionId = "pet_def_1";

    private static GameDbContext CreateContext(string storeName) =>
        TestGameDbContextFactory.Create(storeName);

    /// <summary>Seeds one Player, one definition, and one owned Pet.</summary>
    private static async Task SeedAsync(string storeName)
    {
        using var context = CreateContext(storeName);

        context.Players.Add(new Player
        {
            PlayerId = PetOwner,
            DiscordUserId = "80351110224678912",
            Level = Player.InitialLevel,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        context.PetDefinitions.Add(new PetDefinition
        {
            PetDefinitionId = PetDefinitionId,
            Identity = "Thanh Xà",
            Element = Element.Moc,
            PetLevelMultiplier = 1.0m,
            PassiveId = new PassiveId("pet-passive-1"),
            PassiveThreshold = 5,
            SignatureSkillCardId = "card_skill_1",
        });

        context.Pets.Add(new Pet
        {
            PetInstanceId = PetInstanceId,
            PlayerId = PetOwner,
            PetDefinitionId = PetDefinitionId,
            Tier = PetTier.Common,
            Star = 1,
            Level = 1,
            AcquiredAt = DateTimeOffset.UtcNow,
        });

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetByIdAsync_ForAnExistingInstance_ShouldReturnIt()
    {
        var storeName = nameof(GetByIdAsync_ForAnExistingInstance_ShouldReturnIt);
        await SeedAsync(storeName);

        using var context = CreateContext(storeName);
        var repository = new PetRepository(context);

        var pet = await repository.GetByIdAsync(PetInstanceId);

        Assert.NotNull(pet);
        Assert.Equal(PetInstanceId, pet!.PetInstanceId);
        Assert.Equal(PetOwner, pet.PlayerId);
        Assert.Equal(PetDefinitionId, pet.PetDefinitionId);
    }

    [Fact]
    public async Task GetByIdAsync_ForAnUnknownInstance_ShouldReturnNull()
    {
        var storeName = nameof(GetByIdAsync_ForAnUnknownInstance_ShouldReturnNull);
        await SeedAsync(storeName);

        using var context = CreateContext(storeName);
        var repository = new PetRepository(context);

        Assert.Null(await repository.GetByIdAsync("pet_does_not_exist"));
    }

    [Fact]
    public async Task GetByIdAsync_ShouldNotFilterByPlayer_SoTheCallerCanJudgeOwnership()
    {
        // The read reports what the store holds; the ownership comparison is the
        // battle-start path's (API_CONTRACTS.md §3). A Player-filtered query
        // would make "another Player's Pet" indistinguishable from "no such
        // Pet", which is a distinction the endpoint's author needs.
        var storeName = nameof(GetByIdAsync_ShouldNotFilterByPlayer_SoTheCallerCanJudgeOwnership);
        await SeedAsync(storeName);

        using var context = CreateContext(storeName);
        var repository = new PetRepository(context);

        var pet = await repository.GetByIdAsync(PetInstanceId);

        Assert.NotNull(pet);

        // The instance resolves, and its owner is observably not this caller —
        // which is exactly the condition the endpoint rejects as PET_NOT_OWNED.
        Assert.NotEqual(OtherPlayer, pet!.PlayerId);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldNotWriteAnything()
    {
        // Resolution is a read: it must not add, mutate, or save an entity.
        var storeName = nameof(GetByIdAsync_ShouldNotWriteAnything);
        await SeedAsync(storeName);

        using (var context = CreateContext(storeName))
        {
            var repository = new PetRepository(context);

            var before = await context.Pets.CountAsync();
            await repository.GetByIdAsync(PetInstanceId);
            var after = await context.Pets.CountAsync();

            Assert.Equal(before, after);
        }

        // The stored row is unchanged.
        using (var context = CreateContext(storeName))
        {
            var stored = await context.Pets.SingleAsync(p => p.PetInstanceId == PetInstanceId);
            Assert.Equal(PetOwner, stored.PlayerId);
        }
    }

    [Fact]
    public async Task GetByIdAsync_ShouldResolveThePetsDefinitionReference()
    {
        // The battle-start path reads the definition through this instance's
        // PetDefinitionId (DATABASE.md §1–§2: Pet N ── 1 PetDefinition), so the
        // reference must round-trip.
        var storeName = nameof(GetByIdAsync_ShouldResolveThePetsDefinitionReference);
        await SeedAsync(storeName);

        using var context = CreateContext(storeName);
        var repository = new PetRepository(context);

        var pet = await repository.GetByIdAsync(PetInstanceId);
        var definition = await repository.GetDefinitionAsync(pet!.PetDefinitionId);

        Assert.NotNull(definition);
        Assert.Equal(PetDefinitionId, definition!.PetDefinitionId);

        // The Pet's combat-character configuration and the Signature Skill
        // reference the battle is composed from live here — not on the instance
        // (ADR-011 item 5).
        Assert.Equal(Element.Moc, definition.Element);
        Assert.Equal("pet-passive-1", definition.PassiveId.Value);
        Assert.Equal(5, definition.PassiveThreshold);
        Assert.Equal("card_skill_1", definition.SignatureSkillCardId);
    }

    [Fact]
    public async Task GetByIdAsync_ForANonPlayerOwnedInstance_ShouldStillResolveIt()
    {
        // Ownership is a column, not a guard: the row resolves whoever owns it,
        // so a Pet whose owner is another Player is reported — and the endpoint
        // is the layer that rejects it.
        var storeName = nameof(GetByIdAsync_ForANonPlayerOwnedInstance_ShouldStillResolveIt);

        using (var context = CreateContext(storeName))
        {
            context.Players.Add(new Player
            {
                PlayerId = OtherPlayer,
                DiscordUserId = "80351110224678913",
                Level = Player.InitialLevel,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            context.PetDefinitions.Add(new PetDefinition
            {
                PetDefinitionId = PetDefinitionId,
                Identity = "Thanh Xà",
                Element = Element.Moc,
                PetLevelMultiplier = 1.0m,
                PassiveId = new PassiveId("pet-passive-1"),
                PassiveThreshold = 5,
                SignatureSkillCardId = "card_skill_1",
            });

            context.Pets.Add(new Pet
            {
                PetInstanceId = PetInstanceId,
                PlayerId = OtherPlayer,
                PetDefinitionId = PetDefinitionId,
                Tier = PetTier.Common,
                Star = 1,
                Level = 1,
                AcquiredAt = DateTimeOffset.UtcNow,
            });

            await context.SaveChangesAsync();
        }

        using var readContext = CreateContext(storeName);
        var repository = new PetRepository(readContext);

        var pet = await repository.GetByIdAsync(PetInstanceId);

        Assert.NotNull(pet);
        Assert.Equal(OtherPlayer, pet!.PlayerId);
    }
}
