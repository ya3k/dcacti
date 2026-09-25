using GameServer.Application.Relics;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Players;
using GameServer.Domain.Relics;
using GameServer.Infrastructure.Postgres;
using GameServer.Infrastructure.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// The battle-start Relic snapshot against real persistence — TASK-027.
///
/// <c>RELIC_RULES.md</c> §2.5 and ADR-012 item 8 require the snapshot to be
/// battle-scoped and self-contained: once taken, it must survive later changes
/// to the Player's ownership rows, because the battle never re-reads the
/// collection and never writes back to it.
///
/// These tests read ownership from <see cref="GameDbContext"/> through the
/// real <see cref="RelicRepository"/> — so they verify the ownership query and
/// the order-rebuilding behavior together, not a fake.
/// </summary>
public class RelicLoadoutSnapshotTests
{
    private const string Owner = "player_1";
    private const string OtherPlayer = "player_2";
    private const string DefinitionId = "relic_def_fire";

    private static GameDbContext CreateContext(string storeName) =>
        TestGameDbContextFactory.Create(storeName);

    private static async Task SeedAsync(GameDbContext context)
    {
        context.Players.Add(new Player
        {
            PlayerId = Owner,
            DiscordUserId = "discord_1",
            Level = 1,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        context.Players.Add(new Player
        {
            PlayerId = OtherPlayer,
            DiscordUserId = "discord_2",
            Level = 1,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        context.RelicDefinitions.Add(new RelicDefinition
        {
            RelicDefinitionId = DefinitionId,
            Name = "Berserker Core",
            Trigger = "OnMatchCount",
            Condition = "every 3 Matches",
            EffectDefinition = "atk_plus_5_percent",
        });
        await context.SaveChangesAsync();
    }

    private static Relic NewRelic(string instanceId, string playerId) => new()
    {
        RelicInstanceId = instanceId,
        PlayerId = playerId,
        RelicDefinitionId = DefinitionId,
        AcquiredAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task Validate_ShouldReadOwnershipFromPersistenceAndPreserveRequestOrder()
    {
        var storeName = nameof(Validate_ShouldReadOwnershipFromPersistenceAndPreserveRequestOrder);

        await using var context = CreateContext(storeName);
        await SeedAsync(context);

        // Owned in an order unrelated to the requested order.
        context.Relics.Add(NewRelic("R1", Owner));
        context.Relics.Add(NewRelic("R3", Owner));
        context.Relics.Add(NewRelic("R5", Owner));
        await context.SaveChangesAsync();

        var service = new RelicLoadoutService(new RelicRepository(context));

        var result = await service.ValidateAsync(Owner, new[] { "R5", "R1", "R3" });

        Assert.True(result.IsValid);
        Assert.Equal(
            new[] { "R5", "R1", "R3" },
            result.EquippedRelics.Select(i => i.Value).ToArray());
    }

    [Fact]
    public async Task Validate_ShouldRejectAnInstanceOwnedByAnotherPlayerInPersistence()
    {
        var storeName = nameof(Validate_ShouldRejectAnInstanceOwnedByAnotherPlayerInPersistence);

        await using var context = CreateContext(storeName);
        await SeedAsync(context);

        context.Relics.Add(NewRelic("R1", Owner));
        context.Relics.Add(NewRelic("R2", Owner));
        context.Relics.Add(NewRelic("R3", Owner));
        // Same definition, different owner — must not be equippable.
        context.Relics.Add(NewRelic("R4", OtherPlayer));
        await context.SaveChangesAsync();

        var service = new RelicLoadoutService(new RelicRepository(context));

        var result = await service.ValidateAsync(Owner, new[] { "R1", "R2", "R4" });

        Assert.False(result.IsValid);
        Assert.Equal(RelicLoadoutRejectionReason.NotOwned, result.Reason);
    }

    [Fact]
    public async Task Snapshot_ShouldSurviveRemovalOfTheOwnershipRow()
    {
        // ADR-012 item 8 / RELIC_RULES.md §2.5: the snapshot does not change
        // from live DB reads mid-battle. Once taken, deleting the ownership row
        // must not alter it.
        var storeName = nameof(Snapshot_ShouldSurviveRemovalOfTheOwnershipRow);

        await using var context = CreateContext(storeName);
        await SeedAsync(context);

        context.Relics.Add(NewRelic("R3", Owner));
        context.Relics.Add(NewRelic("R1", Owner));
        context.Relics.Add(NewRelic("R5", Owner));
        await context.SaveChangesAsync();

        var service = new RelicLoadoutService(new RelicRepository(context));
        var snapshot = await service.ValidateAsync(Owner, new[] { "R3", "R1", "R5" });

        Assert.True(snapshot.IsValid);

        // The Player's collection changes underneath the battle.
        var owned = await context.Relics.Where(r => r.PlayerId == Owner).ToListAsync();
        context.Relics.RemoveRange(owned);
        await context.SaveChangesAsync();

        // The already-produced snapshot is unaffected — it is a value, not a
        // live view of the collection.
        Assert.Equal(
            new[] { "R3", "R1", "R5" },
            snapshot.EquippedRelics.Select(i => i.Value).ToArray());
    }

    [Fact]
    public async Task Snapshot_ShouldNotWriteBackToOwnershipRows()
    {
        // RELIC_RULES.md §2.5 / ADR-012 item 8: the snapshot changes no
        // ownership data. Validation must be a read-only pass.
        var storeName = nameof(Snapshot_ShouldNotWriteBackToOwnershipRows);

        await using var context = CreateContext(storeName);
        await SeedAsync(context);

        context.Relics.Add(NewRelic("R1", Owner));
        context.Relics.Add(NewRelic("R2", Owner));
        context.Relics.Add(NewRelic("R3", Owner));
        await context.SaveChangesAsync();

        var before = await context.Relics
            .AsNoTracking()
            .Where(r => r.PlayerId == Owner)
            .Select(r => new { r.RelicInstanceId, r.PlayerId, r.RelicDefinitionId })
            .ToListAsync();

        var service = new RelicLoadoutService(new RelicRepository(context));
        var result = await service.ValidateAsync(Owner, new[] { "R1", "R2", "R3" });

        Assert.True(result.IsValid);

        var after = await context.Relics
            .AsNoTracking()
            .Where(r => r.PlayerId == Owner)
            .Select(r => new { r.RelicInstanceId, r.PlayerId, r.RelicDefinitionId })
            .ToListAsync();

        Assert.Equal(before.Count, after.Count);
        Assert.Equal(
            before.OrderBy(r => r.RelicInstanceId, StringComparer.Ordinal)
                .Select(r => r.RelicInstanceId),
            after.OrderBy(r => r.RelicInstanceId, StringComparer.Ordinal)
                .Select(r => r.RelicInstanceId));
    }

    [Fact]
    public async Task Validate_ShouldAcceptMultipleInstancesOfOneDefinitionFromPersistence()
    {
        // RELIC_RULES.md §2.4 item 3: distinct instances of the same definition
        // are both equippable. Every seeded row here shares one definition.
        var storeName = nameof(Validate_ShouldAcceptMultipleInstancesOfOneDefinitionFromPersistence);

        await using var context = CreateContext(storeName);
        await SeedAsync(context);

        context.Relics.Add(NewRelic("R_a", Owner));
        context.Relics.Add(NewRelic("R_b", Owner));
        context.Relics.Add(NewRelic("R_c", Owner));
        await context.SaveChangesAsync();

        var service = new RelicLoadoutService(new RelicRepository(context));

        var result = await service.ValidateAsync(Owner, new[] { "R_a", "R_b", "R_c" });

        Assert.True(result.IsValid);
        Assert.Equal(3, result.EquippedRelics.Length);
    }

    [Fact]
    public async Task Validate_ShouldCarryTheSnapshotIntoPetState()
    {
        // RELIC_RULES.md §2.5 / GAME_STATE.md §2.3: the validated snapshot is
        // what PetState.EquippedRelics[] holds, one instance identity per
        // element, in equip-slot order.
        var storeName = nameof(Validate_ShouldCarryTheSnapshotIntoPetState);

        await using var context = CreateContext(storeName);
        await SeedAsync(context);

        context.Relics.Add(NewRelic("R7", Owner));
        context.Relics.Add(NewRelic("R2", Owner));
        context.Relics.Add(NewRelic("R9", Owner));
        await context.SaveChangesAsync();

        var service = new RelicLoadoutService(new RelicRepository(context));
        var result = await service.ValidateAsync(Owner, new[] { "R7", "R2", "R9" });

        Assert.True(result.IsValid);

        var petState = GameServer.Domain.Battle.PetState.AtBattleCreation(
            Element.Hoa,
            new PassiveId("passive_1"),
            passiveThreshold: 5,
            passiveResetOverride: null,
            equippedRelics: result.EquippedRelics);

        Assert.NotNull(petState.EquippedRelics);
        Assert.Equal(
            new[] { "R7", "R2", "R9" },
            petState.EquippedRelics!.Select(i => i.Value).ToArray());
    }
}
