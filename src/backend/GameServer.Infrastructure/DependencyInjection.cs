using GameServer.Infrastructure.Postgres;
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
