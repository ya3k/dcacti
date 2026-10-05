using GameServer.Domain.Players;
using GameServer.Infrastructure.Postgres;
using GameServer.Infrastructure.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// Verifies the Player match-or-create ownership boundary against real
/// persistence (DATABASE.md §1, §3, ADR-020).
/// </summary>
public class PlayerMatchOrCreateTests
{
    private static GameDbContext CreateContext(string storeName) =>
        TestGameDbContextFactory.Create(storeName);

    [Fact]
    public async Task NewAccountId_ShouldCreateAPlayerAtLevelOne()
    {
        await using var context = CreateContext(nameof(NewAccountId_ShouldCreateAPlayerAtLevelOne));
        var repository = new PlayerRepository(context);

        var accountId = Guid.NewGuid();
        var player = await repository.GetOrCreateForAccountAsync(accountId, TestStarterGrants.StagedCallback);

        Assert.Equal(accountId, player.AccountId);
        Assert.False(string.IsNullOrWhiteSpace(player.PlayerId));

        Assert.Equal(Player.InitialLevel, player.Level);
        Assert.Equal(1, player.Level);

        var stored = await context.Players.SingleAsync();
        Assert.Equal(player.PlayerId, stored.PlayerId);
        Assert.Equal(Player.InitialLevel, stored.Level);
    }

    [Fact]
    public async Task ExistingAccountId_ShouldReturnTheSamePlayer_WithoutCreatingADuplicate()
    {
        await using var context = CreateContext(nameof(ExistingAccountId_ShouldReturnTheSamePlayer_WithoutCreatingADuplicate));
        var repository = new PlayerRepository(context);

        var accountId = Guid.NewGuid();

        var first = await repository.GetOrCreateForAccountAsync(accountId, TestStarterGrants.StagedCallback);
        var second = await repository.GetOrCreateForAccountAsync(accountId, TestStarterGrants.StagedCallback);

        Assert.Equal(first.PlayerId, second.PlayerId);
        Assert.Equal(first.AccountId, second.AccountId);
        Assert.Equal(first.Level, second.Level);
        Assert.Equal(first.CreatedAt, second.CreatedAt);

        Assert.Equal(1, await context.Players.CountAsync());
    }

    [Fact]
    public async Task ExistingPlayer_ShouldPreserveItsLevel()
    {
        await using var context = CreateContext(nameof(ExistingPlayer_ShouldPreserveItsLevel));
        var repository = new PlayerRepository(context);

        var accountId = Guid.NewGuid();

        context.Players.Add(new Player
        {
            PlayerId = "player_progressed",
            AccountId = accountId,
            Level = 37,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync();

        var matched = await repository.GetOrCreateForAccountAsync(accountId, TestStarterGrants.StagedCallback);

        Assert.Equal("player_progressed", matched.PlayerId);
        Assert.Equal(37, matched.Level);

        Assert.Equal(1, await context.Players.CountAsync());
        Assert.Equal(37, (await context.Players.SingleAsync()).Level);
    }

    [Fact]
    public async Task DistinctAccountIds_ShouldResolveToDistinctPlayers()
    {
        await using var context = CreateContext(nameof(DistinctAccountIds_ShouldResolveToDistinctPlayers));
        var repository = new PlayerRepository(context);

        var account1 = Guid.NewGuid();
        var account2 = Guid.NewGuid();

        var first = await repository.GetOrCreateForAccountAsync(account1, TestStarterGrants.StagedCallback);
        var second = await repository.GetOrCreateForAccountAsync(account2, TestStarterGrants.StagedCallback);

        Assert.NotEqual(first.PlayerId, second.PlayerId);
        Assert.NotEqual(first.AccountId, second.AccountId);
        Assert.Equal(2, await context.Players.CountAsync());
    }

    [Fact]
    public async Task CreatedAt_ShouldBeSetAtCreation_AndNotRewrittenOnMatch()
    {
        await using var context = CreateContext(nameof(CreatedAt_ShouldBeSetAtCreation_AndNotRewrittenOnMatch));
        var repository = new PlayerRepository(context);

        var accountId = Guid.NewGuid();

        var created = await repository.GetOrCreateForAccountAsync(accountId, TestStarterGrants.StagedCallback);
        var matched = await repository.GetOrCreateForAccountAsync(accountId, TestStarterGrants.StagedCallback);

        Assert.NotEqual(default, created.CreatedAt);
        Assert.Equal(created.CreatedAt, matched.CreatedAt);
    }
}
