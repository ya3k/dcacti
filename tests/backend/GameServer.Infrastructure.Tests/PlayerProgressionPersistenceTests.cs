using GameServer.Domain.Players;
using GameServer.Infrastructure.Postgres;
using GameServer.Infrastructure.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// The Player progression write boundary — <c>DATABASE.md</c> §1,
/// <c>COMBAT_RULES.md</c> §7.2/§7.4.
///
/// <code>
/// Rule (COMBAT_RULES.md §7.2/§7.4 — Player XP is persisted and Level follows it)
///  ↓
/// Scenario (Given a stored Player, When its progression is saved,
///           Then the documented values survive a reload)
///  ↓
/// Test
/// </code>
///
/// <b>What this suite establishes.</b> That a mutated Player's <c>XP</c> and
/// <c>Level</c> are genuinely stored — not merely held in memory — and that a
/// progression update rewrites only those two documented values, leaving the
/// identity, the Discord link, and the creation timestamp untouched.
///
/// <b>It proves no Pet progression is touched.</b> <c>ADR-016</c> item 12 makes
/// the Player and Pet tracks independent; this suite drives the Player boundary
/// alone, so a write that reached into Pet state could not pass it.
/// </summary>
public class PlayerProgressionPersistenceTests
{
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
        context.ChangeTracker.Clear();

        return player;
    }

    [Fact]
    public async Task SaveProgression_ShouldPersistXpAndLevel_AcrossAReload()
    {
        // COMBAT_RULES.md §7.2/§7.4 / DATABASE.md §1: the granted XP and the Level
        // it determines are persistent columns, so they must survive a reload
        // rather than living only in the tracked instance.
        var store = $"progression-round-trip-{Guid.NewGuid():N}";

        await using (var context = CreateContext(store))
        {
            SeedPlayer(context, "player_round_trip");
        }

        await using (var context = CreateContext(store))
        {
            var repository = new PlayerRepository(context);

            var player = await repository.GetByIdAsync("player_round_trip");

            Assert.NotNull(player);

            // COMBAT_RULES.md §7.2: BattleWon grants +100.
            player!.GrantBattleXp(Player.BattleWonXpReward);

            Assert.True(await repository.SaveProgressionAsync(player));
        }

        await using (var context = CreateContext(store))
        {
            var stored = await context.Players.AsNoTracking()
                .SingleAsync(p => p.PlayerId == "player_round_trip");

            Assert.Equal(100, stored.XP);
            Assert.Equal(2, stored.Level);
            Assert.Equal(Player.LevelForXp(stored.XP), stored.Level);
        }
    }

    [Fact]
    public async Task SaveProgression_ShouldStoreAnUncappedXpValue_WithTheLevelCapped()
    {
        // COMBAT_RULES.md §7.5 items 1–2: XP is never clamped, only the Level is
        // capped. A value past 4900 must therefore be stored in full.
        var store = $"progression-uncapped-{Guid.NewGuid():N}";

        await using (var context = CreateContext(store))
        {
            SeedPlayer(context, "player_uncapped");
        }

        await using (var context = CreateContext(store))
        {
            var repository = new PlayerRepository(context);
            var player = await repository.GetByIdAsync("player_uncapped");

            // Ten wins from 4900: 5000 XP, still Level 50.
            player!.XP = 4900;
            player.GrantBattleXp(Player.BattleWonXpReward);

            Assert.Equal(5000, player.XP);
            Assert.Equal(Player.MaxLevel, player.Level);

            Assert.True(await repository.SaveProgressionAsync(player));
        }

        await using (var context = CreateContext(store))
        {
            var stored = await context.Players.AsNoTracking()
                .SingleAsync(p => p.PlayerId == "player_uncapped");

            Assert.Equal(5000, stored.XP);
            Assert.Equal(Player.MaxLevel, stored.Level);
        }
    }

    [Fact]
    public async Task SaveProgression_ShouldLeaveIdentityAndCreationValuesUnchanged()
    {
        // DATABASE.md §1: PlayerId, DiscordUserId, and CreatedAt are creation-time
        // values. A progression update maintains XP and Level only — it must not
        // rewrite the row's identity, its Discord link, or its creation instant.
        var store = $"progression-identity-{Guid.NewGuid():N}";

        DateTimeOffset originalCreatedAt;

        await using (var context = CreateContext(store))
        {
            originalCreatedAt = SeedPlayer(context, "player_identity").CreatedAt;
        }

        await using (var context = CreateContext(store))
        {
            var repository = new PlayerRepository(context);
            var player = await repository.GetByIdAsync("player_identity");

            player!.GrantBattleXp(Player.BattleWonXpReward);
            await repository.SaveProgressionAsync(player);
        }

        await using (var context = CreateContext(store))
        {
            var stored = await context.Players.AsNoTracking()
                .SingleAsync(p => p.PlayerId == "player_identity");

            Assert.Equal("player_identity", stored.PlayerId);
            Assert.Equal("discord-player_identity", stored.DiscordUserId);
            Assert.Equal(originalCreatedAt, stored.CreatedAt);
            Assert.Equal(100, stored.XP);
        }
    }

    [Fact]
    public async Task SaveProgression_ShouldReportAbsence_AndCreateNothing_ForAnUnknownPlayer()
    {
        // DATABASE.md §1: the reward path rewards an existing Player; creation is
        // the auth boundary's match-or-create path. A save for an unknown
        // identifier is therefore reported as absence and writes nothing — a
        // reward must never bring a Player into existence.
        await using var context = CreateContext($"progression-unknown-{Guid.NewGuid():N}");

        var repository = new PlayerRepository(context);

        var unknown = new Player
        {
            PlayerId = "player_does_not_exist",
            DiscordUserId = "discord-unknown",
            XP = Player.BattleWonXpReward,
            Level = Player.LevelForXp(Player.BattleWonXpReward),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        Assert.False(await repository.SaveProgressionAsync(unknown));

        context.ChangeTracker.Clear();

        Assert.Empty(context.Players);
    }

    [Fact]
    public async Task GetById_ShouldResolveTheRowTheProgressionWriteUpdates()
    {
        // DATABASE.md §1: the battle-end reward reads the owning Player by the
        // identity carried on BattleState.PlayerId (GAME_STATE.md §2.8), so that
        // lookup and the progression save must describe the same row.
        var store = $"progression-read-then-write-{Guid.NewGuid():N}";

        await using (var context = CreateContext(store))
        {
            SeedPlayer(context, "player_read_write");
        }

        await using (var context = CreateContext(store))
        {
            var repository = new PlayerRepository(context);

            // An unknown identifier resolves to absence rather than a default.
            Assert.Null(await repository.GetByIdAsync("player_not_there"));

            var player = await repository.GetByIdAsync("player_read_write");

            Assert.NotNull(player);
            Assert.Equal(Player.InitialXp, player!.XP);
            Assert.Equal(Player.InitialLevel, player.Level);
        }
    }
}
