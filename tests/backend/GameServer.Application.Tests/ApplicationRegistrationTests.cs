using GameServer.Application;
using GameServer.Application.Battle;
using GameServer.Application.Cards;
using GameServer.Application.Pets;
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
    public void AddApplicationServices_ShouldRegisterPetLevelService()
    {
        // ADR-012 Consequences: the recompute hook is an Application service
        // registered alongside the other Application services.
        var services = new ServiceCollection();
        services.AddApplicationServices();

        var serviceProvider = services.BuildServiceProvider();

        Assert.Contains(
            services,
            d => d.ServiceType == typeof(PetLevelService));
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
}
