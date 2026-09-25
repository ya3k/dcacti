using GameServer.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// Shared InMemory <see cref="GameDbContext"/> factory for Infrastructure
/// tests.
///
/// FK-index suppression is handled by <c>GameDbContext.ConfigureConventions</c>
/// (EF Core 7+ documented API), which runs as part of model building on the
/// <see cref="DbContext"/> itself — so the design-time model the tests
/// inspect automatically matches the model used by <c>dotnet ef
/// migrations</c> and by the API host, with no DI-registered plugin.
///
/// Each test still gets its own named store so no test observes another's
/// rows.
/// </summary>
internal static class TestGameDbContextFactory
{
    private static readonly Lazy<ServiceProvider> SharedProvider = new(() =>
        new ServiceCollection()
            .AddEntityFrameworkInMemoryDatabase()
            .BuildServiceProvider());

    public static GameDbContext Create(string storeName) =>
        new(new DbContextOptionsBuilder<GameDbContext>()
            .UseInternalServiceProvider(SharedProvider.Value)
            .UseInMemoryDatabase(storeName)
            .Options);
}
