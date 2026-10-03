using GameServer.Application;
using GameServer.Application.Battle;
using GameServer.Application.Cards;
using GameServer.Application.Pets;
using GameServer.Application.Players;
using GameServer.Application.Relics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GameServer.Application.Tests;

public class ApplicationRegistrationTests
{
    [Fact]
    public void AddApplicationServices_ShouldRegisterWithoutErrors()
    {
        var services = new ServiceCollection();
        services.AddApplicationServices();

        var serviceProvider = services.BuildServiceProvider();
        Assert.NotNull(serviceProvider);
    }

    [Fact]
    public void AddApplicationServices_ShouldRegisterCardLoadoutService()
    {
        // CARD_RULES.md §1 / API_CONTRACTS.md §3: the battle-start Card loadout
        // validation and snapshot service is registered alongside the other
        // Application services (the same shape as RelicLoadoutService).
        var services = new ServiceCollection();
        services.AddApplicationServices();

        Assert.Contains(
            services,
            d => d.ServiceType == typeof(CardLoadoutService));
    }

    [Fact]
    public void AddApplicationServices_ShouldRegisterBattleStartService()
    {
        // API_CONTRACTS.md §3: the battle-start orchestration boundary is
        // registered so POST /api/battle/start can resolve it. It is scoped,
        // because it depends on the scoped Pet repository and loadout services.
        var services = new ServiceCollection();
        services.AddApplicationServices();

        var descriptor = Assert.Single(
            services,
            d => d.ServiceType == typeof(BattleStartService));

        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddApplicationServices_ShouldRegisterPlayerStarterGrantFactory()
    {
        // DATABASE.md §2 item 1: the starter ownership composition is registered
        // so the Player-creation boundary can resolve it. It is scoped, because
        // it depends on the scoped Pet/Card/Relic repository boundaries.
        var services = new ServiceCollection();
        services.AddApplicationServices();

        var descriptor = Assert.Single(
            services,
            d => d.ServiceType == typeof(PlayerStarterGrantFactory));

        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddApplicationServices_ShouldRegisterRelicDefinitionLookupAsASingletonAdapter()
    {
        // DATABASE.md §1 / RELIC_RULES.md §8: the shared RelicDefinition content
        // read is registered through the scope-factory adapter — the same
        // registration shape as ICardDefinitionLookup — so a singleton consumer
        // reads Relic content without capturing the scoped GameDbContext. It is
        // built by a factory rather than by type, because the adapter is given
        // the container's IServiceScopeFactory.
        var services = new ServiceCollection();
        services.AddApplicationServices();

        var descriptor = Assert.Single(
            services,
            d => d.ServiceType == typeof(IRelicDefinitionLookup));

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.NotNull(descriptor.ImplementationFactory);
    }
}
