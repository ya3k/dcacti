using GameServer.Application.Cards;
using GameServer.Application.Pets;
using GameServer.Application.Players;
using GameServer.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GameServer.Infrastructure.Tests;

public class InfrastructureRegistrationTests
{
    [Fact]
    public void AddInfrastructureServices_ShouldRegisterCleanly_WhenConnectionStringsEmpty()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        services.AddInfrastructureServices(configuration);
        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider);
    }

    [Fact]
    public void AddInfrastructureServices_ShouldRegisterPetRepository()
    {
        // DATABASE.md §1–§2: the Pet persistence boundary is registered with
        // the other Infrastructure services (alongside IPlayerRepository).
        //
        // Asserted against the service descriptors rather than by resolving
        // the instance: GameDbContext is only registered when a connection
        // string is configured, and resolving PetRepository without it would
        // throw — the registration itself is what this test verifies.
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        services.AddInfrastructureServices(configuration);

        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IPetRepository) &&
                descriptor.ImplementationType == typeof(Postgres.Repositories.PetRepository));
    }

    [Fact]
    public void AddInfrastructureServices_ShouldRegisterCardRepository()
    {
        // DATABASE.md §1–§2: the Card content and Player-unlock persistence
        // boundary is registered with the other Infrastructure services. There
        // is no persistent Card-equip table and no inventory column
        // (ADR-012 items 9–10).
        //
        // Asserted against the service descriptors for the same reason as
        // IPetRepository: GameDbContext only exists when a connection string is
        // configured, and the registration itself is what this test verifies.
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        services.AddInfrastructureServices(configuration);

        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(ICardRepository) &&
                descriptor.ImplementationType == typeof(Postgres.Repositories.CardRepository));
    }
}
