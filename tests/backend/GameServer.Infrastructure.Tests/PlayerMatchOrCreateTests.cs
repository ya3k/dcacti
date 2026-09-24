using GameServer.Domain.Players;
using GameServer.Infrastructure.Postgres;
using GameServer.Infrastructure.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// The ownership semantics of match-or-create, keyed on the verified
/// <c>DiscordUserId</c> (<c>DATABASE.md</c> §1, §3).
///
/// <code>
/// existing DiscordUserId  →  find Player  →  return it, unchanged
/// new DiscordUserId       →  no Player    →  create at Level 1
/// </code>
///
/// Nothing here resets an existing Player's Level, overwrites a Player with a
/// different Discord identity, or creates a duplicate ownership record.
/// </summary>
public class PlayerMatchOrCreateTests
{
    private static GameDbContext CreateContext(string storeName) =>
        new(new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(storeName)
            .Options);

    [Fact]
    public async Task NewDiscordUserId_ShouldCreateAPlayerAtLevelOne()
    {
        await using var context = CreateContext(nameof(NewDiscordUserId_ShouldCreateAPlayerAtLevelOne));
        var repository = new PlayerRepository(context);

        var player = await repository.GetOrCreateByDiscordUserIdAsync("80351110224678912");

        // DATABASE.md §1: the row carries the identity it was created for, a
        // PlayerId, and a creation timestamp.
        Assert.Equal("80351110224678912", player.DiscordUserId);
        Assert.False(string.IsNullOrWhiteSpace(player.PlayerId));

        // PET_RULES.md §5 item 8 / DATABASE.md §3: a newly created Player
        // starts at Level 1.
        Assert.Equal(Player.InitialLevel, player.Level);
        Assert.Equal(1, player.Level);

        // It is genuinely persisted, not merely returned.
        var stored = await context.Players.SingleAsync();
        Assert.Equal(player.PlayerId, stored.PlayerId);
        Assert.Equal(Player.InitialLevel, stored.Level);
    }

    [Fact]
    public async Task ExistingDiscordUserId_ShouldReturnTheSamePlayer_WithoutCreatingADuplicate()
    {
        // DATABASE.md §3: a repeated authentication for the same Discord
        // identity does not create a duplicate Player row. ADR-013 item 7
        // makes this reachable: the identity exchange yields the same
        // DiscordUserId for one Discord account on every successful exchange.
        await using var context = CreateContext(nameof(ExistingDiscordUserId_ShouldReturnTheSamePlayer_WithoutCreatingADuplicate));
        var repository = new PlayerRepository(context);

        const string discordUserId = "80351110224678912";

        var first = await repository.GetOrCreateByDiscordUserIdAsync(discordUserId);
        var second = await repository.GetOrCreateByDiscordUserIdAsync(discordUserId);

        Assert.Equal(first.PlayerId, second.PlayerId);
        Assert.Equal(first.DiscordUserId, second.DiscordUserId);
        Assert.Equal(first.Level, second.Level);
        Assert.Equal(first.CreatedAt, second.CreatedAt);

        Assert.Equal(1, await context.Players.CountAsync());
    }

    [Fact]
    public async Task ExistingPlayer_ShouldPreserveItsLevel()
    {
        // An existing Player's Level is not reset by authentication. Only a
        // newly created Player carries the initial value; a Player that has
        // progressed keeps the Level it holds.
        await using var context = CreateContext(nameof(ExistingPlayer_ShouldPreserveItsLevel));
        var repository = new PlayerRepository(context);

        const string discordUserId = "80351110224678912";

        // A Player already at a progressed Level, within the documented range.
        context.Players.Add(new Player
        {
            PlayerId = "player_progressed",
            DiscordUserId = discordUserId,
            Level = 37,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync();

        var matched = await repository.GetOrCreateByDiscordUserIdAsync(discordUserId);

        Assert.Equal("player_progressed", matched.PlayerId);
        Assert.Equal(37, matched.Level);

        // And the stored row is untouched: no second row, no rewound Level.
        Assert.Equal(1, await context.Players.CountAsync());
        Assert.Equal(37, (await context.Players.SingleAsync()).Level);
    }

    [Fact]
    public async Task DistinctDiscordUserIds_ShouldResolveToDistinctPlayers()
    {
        // The lookup key is the Discord identity, so two different identities
        // own two different Players — one identity is never returned for
        // another (DATABASE.md §1: DiscordUserId is the unique matching key).
        await using var context = CreateContext(nameof(DistinctDiscordUserIds_ShouldResolveToDistinctPlayers));
        var repository = new PlayerRepository(context);

        var first = await repository.GetOrCreateByDiscordUserIdAsync("80351110224678912");
        var second = await repository.GetOrCreateByDiscordUserIdAsync("197198264217354241");

        Assert.NotEqual(first.PlayerId, second.PlayerId);
        Assert.NotEqual(first.DiscordUserId, second.DiscordUserId);
        Assert.Equal(2, await context.Players.CountAsync());
    }

    [Fact]
    public async Task DiscordUserId_ShouldBeTreatedAsAnOpaqueString()
    {
        // API_CONTRACTS.md §2.3 item 3: `id` is a snowflake Discord serializes
        // as a string to avoid integer overflow, so it is stored and compared
        // as an opaque string and never parsed into a numeric type.
        await using var context = CreateContext(nameof(DiscordUserId_ShouldBeTreatedAsAnOpaqueString));
        var repository = new PlayerRepository(context);

        // A value beyond Int64 would break any numeric parsing.
        const string snowflakeBeyondInt64 = "99999999999999999999999";

        var created = await repository.GetOrCreateByDiscordUserIdAsync(snowflakeBeyondInt64);
        var matched = await repository.GetOrCreateByDiscordUserIdAsync(snowflakeBeyondInt64);

        Assert.Equal(snowflakeBeyondInt64, created.DiscordUserId);
        Assert.Equal(created.PlayerId, matched.PlayerId);
        Assert.Equal(1, await context.Players.CountAsync());
    }

    [Fact]
    public async Task CreatedAt_ShouldBeSetAtCreation_AndNotRewrittenOnMatch()
    {
        // DATABASE.md §1: CreatedAt is a creation timestamp. Matching an
        // existing Player is not an update of it, so a later authentication
        // leaves it unchanged.
        await using var context = CreateContext(nameof(CreatedAt_ShouldBeSetAtCreation_AndNotRewrittenOnMatch));
        var repository = new PlayerRepository(context);

        const string discordUserId = "80351110224678912";

        var created = await repository.GetOrCreateByDiscordUserIdAsync(discordUserId);
        var matched = await repository.GetOrCreateByDiscordUserIdAsync(discordUserId);

        Assert.NotEqual(default, created.CreatedAt);
        Assert.Equal(created.CreatedAt, matched.CreatedAt);
    }
}
