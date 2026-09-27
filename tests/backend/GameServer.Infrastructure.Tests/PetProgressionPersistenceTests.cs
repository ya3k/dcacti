using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Infrastructure.Postgres;
using GameServer.Infrastructure.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// The Pet progression write boundary — <c>DATABASE.md</c> §1,
/// <c>PET_RULES.md</c> §5.1/§5.4/§5.5.
///
/// <code>
/// Rule (PET_RULES.md §5.1 item 1, §5.4 — Pet XP is persisted per instance
///       and Level follows it)
///  ↓
/// Scenario (Given a stored Pet, When its progression is saved,
///           Then the documented values survive a reload)
///  ↓
/// Test
/// </code>
///
/// <b>What this suite establishes.</b> That a mutated Pet's <c>XP</c> and
/// <c>Level</c> are genuinely stored — not merely held in memory — that a
/// progression update rewrites only those two documented values, and that the
/// write is per instance, so no other owned Pet is touched
/// (<c>PET_RULES.md</c> §5.3 items 1–3).
///
/// <b>It proves no Player progression is touched.</b> <c>ADR-016</c> item 12 and
/// <c>PET_RULES.md</c> §5.1 item 5 make the tracks independent; this suite drives
/// the Pet boundary alone, so a write that reached into Player state could not
/// pass it.
/// </summary>
public class PetProgressionPersistenceTests
{
    private const string DefinitionId = "pet-definition-progression";

    private static GameDbContext CreateContext(string storeName) =>
        TestGameDbContextFactory.Create(storeName);

    private static Player SeedPlayer(GameDbContext context, string playerId)
    {
        var player = new Player
        {
            PlayerId = playerId,
            DiscordUserId = $"discord-{playerId}",
            XP = Player.InitialXp,
            Level = Player.InitialLevel,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        context.Players.Add(player);
        context.SaveChanges();

        return player;
    }

    private static Pet SeedPet(
        GameDbContext context,
        string petInstanceId,
        string playerId,
        int xp = Pet.InitialXp,
        int? level = null)
    {
        var pet = new Pet
        {
            PetInstanceId = petInstanceId,
            PlayerId = playerId,
            PetDefinitionId = DefinitionId,
            Tier = PetTier.Common,
            Star = Pet.MinStar,
            XP = xp,
            Level = level ?? Pet.LevelForXp(xp),
            AcquiredAt = DateTimeOffset.UtcNow,
        };

        context.Pets.Add(pet);
        context.SaveChanges();
        context.ChangeTracker.Clear();

        return pet;
    }

    /// <summary>
    /// Seeds the definition row the Pet's FK requires. CardDefinition content is
    /// not what this suite asserts, so the signature skill is named only to
    /// satisfy PetDefinition's own required member (<c>DATABASE.md</c> §1) — no
    /// Card behaviour is exercised here.
    /// </summary>
    private static void SeedDefinition(GameDbContext context)
    {
        context.PetDefinitions.Add(new PetDefinition
        {
            PetDefinitionId = DefinitionId,
            Identity = "pet-progression-test",
            Element = Element.Hoa,
            PassiveId = new PassiveId("xich-lang"),
            PassiveThreshold = 5,
            SignatureSkillCardId = "card-skill-progression-test",
        });

        context.SaveChanges();
    }

    [Fact]
    public void NewPet_ShouldReceiveXpZeroAndLevelOne_FromTheColumnDefaults()
    {
        // PET_RULES.md §5.2 / DATABASE.md §3: a newly created PlayerPet has
        // Pet.XP = 0 and Pet.Level = 1. The Domain defaults supply both, so a
        // construction that states neither still produces the documented pair.
        var store = $"pet-progression-defaults-{Guid.NewGuid():N}";

        using (var context = CreateContext(store))
        {
            SeedPlayer(context, "player_defaults");
            SeedDefinition(context);

            context.Pets.Add(new Pet
            {
                PetInstanceId = "pet_defaults",
                PlayerId = "player_defaults",
                PetDefinitionId = DefinitionId,
                Tier = PetTier.Common,
                Star = Pet.MinStar,
                AcquiredAt = DateTimeOffset.UtcNow,
            });

            context.SaveChanges();
        }

        using (var context = CreateContext(store))
        {
            var stored = context.Pets.AsNoTracking().Single(p => p.PetInstanceId == "pet_defaults");

            Assert.Equal(Pet.InitialXp, stored.XP);
            Assert.Equal(Pet.InitialLevel, stored.Level);
            Assert.Equal(0, stored.XP);
            Assert.Equal(1, stored.Level);
        }
    }

    [Fact]
    public async Task SaveProgression_ShouldPersistXpAndLevel_AcrossAReload()
    {
        // PET_RULES.md §5.3/§5.4 / DATABASE.md §1: the granted XP and the Level it
        // determines are persistent columns, so they must survive a reload rather
        // than living only in the tracked instance.
        var store = $"pet-progression-round-trip-{Guid.NewGuid():N}";

        await using (var context = CreateContext(store))
        {
            SeedPlayer(context, "player_pet_round_trip");
            SeedDefinition(context);
            SeedPet(context, "pet_round_trip", "player_pet_round_trip");
        }

        await using (var context = CreateContext(store))
        {
            var repository = new PetRepository(context);

            var pet = await repository.GetByIdAsync("pet_round_trip");

            Assert.NotNull(pet);

            // PET_RULES.md §5.3 item 1: BattleWon grants the active combat Pet +100.
            pet!.GrantBattleXp(Pet.BattleWonXpReward);

            Assert.True(await repository.SaveProgressionAsync(pet));
        }

        await using (var context = CreateContext(store))
        {
            var stored = await context.Pets.AsNoTracking()
                .SingleAsync(p => p.PetInstanceId == "pet_round_trip");

            Assert.Equal(100, stored.XP);
            Assert.Equal(2, stored.Level);
            Assert.Equal(Pet.LevelForXp(stored.XP), stored.Level);
        }
    }

    [Fact]
    public async Task SaveProgression_ShouldStoreACappedXpValue_AtTheDocumentedMaximum()
    {
        // PET_RULES.md §5.5 items 1-3: Pet XP is hard-capped at 4900 and no
        // overflow is retained, so a crossing grant stores exactly 4900 — never
        // 5000. This is the sharpest divergence from the Player track, whose XP is
        // never clamped (COMBAT_RULES.md §7.5 item 1).
        var store = $"pet-progression-capped-{Guid.NewGuid():N}";

        await using (var context = CreateContext(store))
        {
            SeedPlayer(context, "player_pet_capped");
            SeedDefinition(context);
            SeedPet(context, "pet_capped", "player_pet_capped", xp: 4850);
        }

        await using (var context = CreateContext(store))
        {
            var repository = new PetRepository(context);
            var pet = await repository.GetByIdAsync("pet_capped");

            pet!.GrantBattleXp(Pet.BattleWonXpReward);

            Assert.Equal(Pet.MaxXp, pet.XP);
            Assert.Equal(Pet.MaxLevel, pet.Level);

            Assert.True(await repository.SaveProgressionAsync(pet));
        }

        await using (var context = CreateContext(store))
        {
            var stored = await context.Pets.AsNoTracking()
                .SingleAsync(p => p.PetInstanceId == "pet_capped");

            Assert.Equal(4900, stored.XP);
            Assert.NotEqual(5000, stored.XP);
            Assert.Equal(Pet.MaxLevel, stored.Level);
        }
    }

    [Fact]
    public async Task SaveProgression_ShouldLeaveIdentityAndOwnershipValuesUnchanged()
    {
        // DATABASE.md §1: PetInstanceId, PlayerId, PetDefinitionId, Tier, Star,
        // and AcquiredAt are creation-time values. A progression update maintains
        // XP and Level only — it must not rewrite the row's identity or its
        // collection ownership.
        var store = $"pet-progression-identity-{Guid.NewGuid():N}";

        DateTimeOffset originalAcquiredAt;

        await using (var context = CreateContext(store))
        {
            SeedPlayer(context, "player_pet_identity");
            SeedDefinition(context);
            originalAcquiredAt = SeedPet(context, "pet_identity", "player_pet_identity", xp: 40).AcquiredAt;
        }

        await using (var context = CreateContext(store))
        {
            var repository = new PetRepository(context);
            var pet = await repository.GetByIdAsync("pet_identity");

            pet!.GrantBattleXp(Pet.BattleWonXpReward);
            await repository.SaveProgressionAsync(pet);
        }

        await using (var context = CreateContext(store))
        {
            var stored = await context.Pets.AsNoTracking()
                .SingleAsync(p => p.PetInstanceId == "pet_identity");

            Assert.Equal("pet_identity", stored.PetInstanceId);
            Assert.Equal("player_pet_identity", stored.PlayerId);
            Assert.Equal(DefinitionId, stored.PetDefinitionId);
            Assert.Equal(PetTier.Common, stored.Tier);
            Assert.Equal(Pet.MinStar, stored.Star);
            Assert.Equal(originalAcquiredAt, stored.AcquiredAt);
            Assert.Equal(140, stored.XP);
        }
    }

    [Fact]
    public async Task SaveProgression_ShouldReportAbsence_AndCreateNothing_ForAnUnknownPet()
    {
        // DATABASE.md §1 / PET_RULES.md §5.3 item 1: the reward updates the Pet
        // that fought. A save for an unknown identifier is therefore reported as
        // absence and writes nothing — a reward must never bring a Pet instance
        // into existence.
        await using var context = CreateContext($"pet-progression-unknown-{Guid.NewGuid():N}");

        SeedPlayer(context, "player_pet_unknown");
        SeedDefinition(context);

        var repository = new PetRepository(context);

        var unknown = new Pet
        {
            PetInstanceId = "pet_does_not_exist",
            PlayerId = "player_pet_unknown",
            PetDefinitionId = DefinitionId,
            Tier = PetTier.Common,
            Star = Pet.MinStar,
            XP = Pet.BattleWonXpReward,
            Level = Pet.LevelForXp(Pet.BattleWonXpReward),
            AcquiredAt = DateTimeOffset.UtcNow,
        };

        Assert.False(await repository.SaveProgressionAsync(unknown));

        context.ChangeTracker.Clear();

        Assert.Empty(context.Pets);
    }

    [Fact]
    public async Task SaveProgression_ShouldWriteOnlyTheAddressedInstance()
    {
        // PET_RULES.md §5.3 items 1-3: exactly one Pet — the active combat Pet —
        // receives battle XP, and every other owned Pet receives +0. The boundary
        // is instance-scoped, so no bulk or party-wide write exists to reach the
        // sibling.
        var store = $"pet-progression-single-instance-{Guid.NewGuid():N}";

        await using (var context = CreateContext(store))
        {
            SeedPlayer(context, "player_pet_single");
            SeedDefinition(context);
            SeedPet(context, "pet_active", "player_pet_single");
            SeedPet(context, "pet_inactive", "player_pet_single", xp: 600);
        }

        await using (var context = CreateContext(store))
        {
            var repository = new PetRepository(context);
            var active = await repository.GetByIdAsync("pet_active");

            active!.GrantBattleXp(Pet.BattleWonXpReward);

            await repository.SaveProgressionAsync(active);
        }

        await using (var context = CreateContext(store))
        {
            var active = await context.Pets.AsNoTracking().SingleAsync(p => p.PetInstanceId == "pet_active");
            var inactive = await context.Pets.AsNoTracking().SingleAsync(p => p.PetInstanceId == "pet_inactive");

            Assert.Equal(100, active.XP);
            Assert.Equal(600, inactive.XP);
            Assert.Equal(Pet.LevelForXp(600), inactive.Level);
        }
    }

    [Fact]
    public async Task GetById_ShouldResolveTheRowTheProgressionWriteUpdates()
    {
        // DATABASE.md §1 / GAME_STATE.md §2.3: the battle-end reward reads the
        // active combat Pet by the identity carried on BattleState.PetState.PetId,
        // so that lookup and the progression save must describe the same row.
        var store = $"pet-progression-read-then-write-{Guid.NewGuid():N}";

        await using (var context = CreateContext(store))
        {
            SeedPlayer(context, "player_pet_read_write");
            SeedDefinition(context);
            SeedPet(context, "pet_read_write", "player_pet_read_write");
        }

        await using (var context = CreateContext(store))
        {
            var repository = new PetRepository(context);

            // An unknown identifier resolves to absence rather than a default.
            Assert.Null(await repository.GetByIdAsync("pet_not_there"));

            var pet = await repository.GetByIdAsync("pet_read_write");

            Assert.NotNull(pet);
            Assert.Equal(Pet.InitialXp, pet!.XP);
            Assert.Equal(Pet.InitialLevel, pet.Level);
        }
    }

    [Fact]
    public async Task SaveProgression_ShouldPersistTheHardCap_AcrossRepeatedGrants()
    {
        // PET_RULES.md §5.5 item 2: repeated wins at the cap store nothing further.
        // Driven through the real repository, so the persisted value — not only the
        // in-memory one — is what is proven to stay at 4900.
        var store = $"pet-progression-repeat-cap-{Guid.NewGuid():N}";

        await using (var context = CreateContext(store))
        {
            SeedPlayer(context, "player_pet_repeat");
            SeedDefinition(context);
            SeedPet(context, "pet_repeat", "player_pet_repeat", xp: Pet.MaxXp);
        }

        await using (var context = CreateContext(store))
        {
            var repository = new PetRepository(context);

            for (var win = 0; win < 3; win++)
            {
                var pet = await repository.GetByIdAsync("pet_repeat");

                pet!.GrantBattleXp(Pet.BattleWonXpReward);

                Assert.Equal(Pet.MaxXp, pet.XP);

                await repository.SaveProgressionAsync(pet);
            }
        }

        await using (var context = CreateContext(store))
        {
            var stored = await context.Pets.AsNoTracking()
                .SingleAsync(p => p.PetInstanceId == "pet_repeat");

            Assert.Equal(Pet.MaxXp, stored.XP);
            Assert.Equal(Pet.MaxLevel, stored.Level);
        }
    }
}