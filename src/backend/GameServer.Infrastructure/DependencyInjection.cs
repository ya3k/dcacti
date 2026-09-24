using GameServer.Application.Identity;
using GameServer.Application.Players;
using GameServer.Infrastructure.Discord;
using GameServer.Infrastructure.Postgres;
using GameServer.Infrastructure.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace GameServer.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // PostgreSQL DbContext boundary
        var pgConnectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(pgConnectionString))
        {
            services.AddDbContext<GameDbContext>(options =>
                options.UseNpgsql(pgConnectionString));
        }

        // Player ownership persistence boundary (DATABASE.md §1).
        //
        // Registered unconditionally: the repository takes the scoped
        // GameDbContext, so it is only resolvable where that context is
        // registered (i.e. when a connection string is configured), while an
        // unconfigured environment still composes cleanly.
        services.AddScoped<IPlayerRepository, PlayerRepository>();

        // Discord identity exchange boundary (API_CONTRACTS.md §2.2–§2.4).
        //
        // This is the registration point for the TASK-035 contract's exchange
        // client, which is a separate downstream implementation and is not
        // built by TASK-023. Until it is registered, the resolver reports the
        // exchange as unavailable and produces no identity — so no Player is
        // ever written for an unverified caller.
        services.AddSingleton<IDiscordIdentityResolver, UnconfiguredDiscordIdentityResolver>();

        // Redis connection boundary
        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisConnectionString));
        }

        return services;
    }

    /// <summary>
    /// Registers infrastructure-level health checks for PostgreSQL and Redis.
    /// Called from Program.cs after AddHealthChecks() so checks compose correctly.
    /// Services that are not configured (empty connection string) are skipped gracefully.
    /// </summary>
    public static IHealthChecksBuilder AddInfrastructureHealthChecks(
        this IHealthChecksBuilder builder,
        IConfiguration configuration)
    {
        var pgConnectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(pgConnectionString))
        {
            builder.AddNpgSql(
                connectionString: pgConnectionString,
                name: "postgresql",
                failureStatus: HealthStatus.Degraded,
                tags: ["db", "infrastructure"]);
        }

        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            builder.AddRedis(
                redisConnectionString: redisConnectionString,
                name: "redis",
                failureStatus: HealthStatus.Degraded,
                tags: ["cache", "infrastructure"]);
        }

        return builder;
    }
}
