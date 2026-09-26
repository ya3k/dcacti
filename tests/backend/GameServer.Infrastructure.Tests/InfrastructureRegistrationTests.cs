using GameServer.Application.Battle;
using GameServer.Application.Cards;
using GameServer.Application.Pets;
using GameServer.Application.Players;
using GameServer.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
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

    [Fact]
    public void AddInfrastructureServices_ShouldRegisterTheRedisBattleStateRepository()
    {
        // REDIS_STATE.md §1–§4 / ARCHITECTURE.md §1, §3: the active battle state
        // store is registered beside the Redis connection boundary, and it is the
        // Redis implementation — no in-process substitute is registered in its
        // place (§2 item 2, §7 item 5).
        //
        // Asserted against the service descriptors rather than by resolving the
        // instance: IConnectionMultiplexer is only registered when a connection
        // string is configured, and the registration itself is what this test
        // verifies.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = "127.0.0.1:6379",
            })
            .Build();

        var services = new ServiceCollection();

        services.AddInfrastructureServices(configuration);

        var descriptor = Assert.Single(
            services,
            d => d.ServiceType == typeof(IBattleStateRepository));

        Assert.Equal(typeof(Redis.BattleStateRepository), descriptor.ImplementationType);
    }

    [Fact]
    public void AddInfrastructureServices_ShouldNotRegisterAnInProcessBattleStateSubstitute_WhenRedisIsUnconfigured()
    {
        // REDIS_STATE.md §2 item 2 makes the stored record the single source of
        // truth for a battle's live state, and §7 item 5 states that nothing
        // permits that state to live in process memory (ADR-005 rejected it). An
        // environment with no Redis therefore has NO battle-state store at all —
        // composing one that silently kept state in memory is exactly the
        // fallback the contract forbids, so its absence is asserted here rather
        // than assumed.
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        services.AddInfrastructureServices(configuration);

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IBattleStateRepository));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IConnectionMultiplexer));

        // The rest of Infrastructure still composes cleanly, so this is the
        // Redis boundary being absent and not the registration failing.
        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider);
    }

    [Fact]
    public void BattleStateService_ShouldRequireAStore_NotFallBackToProcessMemory()
    {
        // REDIS_STATE.md §2 item 2 / §7 item 5 and ADR-005: the authoritative
        // state lives in the store. The Application boundary therefore has no
        // parameterless construction and no default store — composing it without
        // one is a startup failure rather than a service that quietly resolves
        // battles whose state is persisted nowhere.
        var constructors = typeof(BattleStateService).GetConstructors();

        Assert.All(
            constructors,
            constructor => Assert.Contains(
                constructor.GetParameters(),
                parameter => parameter.ParameterType == typeof(IBattleStateRepository)));

        Assert.Throws<ArgumentNullException>(
            () => new BattleStateService(null!, new FixedSeedSource()));
    }

    /// <summary>
    /// A minimal <see cref="IRngSeedSource"/> for the construction assertions
    /// above. The seed's value is irrelevant here: no battle is created.
    /// </summary>
    private sealed class FixedSeedSource : GameServer.Application.Battle.IRngSeedSource
    {
        public ulong CreateSeed() => 1UL;
    }
}
