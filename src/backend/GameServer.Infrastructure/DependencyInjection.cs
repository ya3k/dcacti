using GameServer.Application.Battle;
using GameServer.Application.Cards;
using GameServer.Application.Identity;
using GameServer.Application.Pets;
using GameServer.Application.Players;
using GameServer.Domain.Players;
using GameServer.Application.Relics;
using GameServer.Infrastructure.Discord;
using GameServer.Infrastructure.Postgres;
using GameServer.Infrastructure.Postgres.Repositories;
using GameServer.Infrastructure.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        // Pet ownership persistence boundary (DATABASE.md §1–§2).
        //
        // Registered unconditionally for the same reason as IPlayerRepository:
        // the repository takes the scoped GameDbContext.
        services.AddScoped<IPetRepository, PetRepository>();

        // Relic ownership persistence boundary (DATABASE.md §1–§2).
        //
        // Ownership rows and static content only — there is no persistent
        // equip table (DATABASE.md §2, ADR-012 item 7). Registered
        // unconditionally for the same reason as IPlayerRepository.
        services.AddScoped<IRelicRepository, RelicRepository>();

        // Card content and Player unlock persistence boundary
        // (DATABASE.md §1–§2).
        //
        // Unlock rows and static content only — there is no persistent equip
        // table and no inventory/quantity column (DATABASE.md §2, ADR-012
        // items 9–10). Registered unconditionally for the same reason as
        // IPlayerRepository.
        services.AddScoped<ICardRepository, CardRepository>();

        // Durable battle result persistence and the two documented battle-end
        // lookups (DATABASE.md §1; ARCHITECTURE.md §3 `PersistenceRepository
        // (Postgres)`).
        //
        // The result row and both lookups are PostgreSQL reads/writes that
        // happen on the terminal path only — TDD.md §4 item 3 keeps PostgreSQL
        // off the hot resolution path, and DATABASE.md §5 item 3 keeps active
        // battle state out of it entirely. Registered unconditionally for the
        // same reason as IPlayerRepository: the repositories take the scoped
        // GameDbContext.
        services.AddScoped<IBattleResultRepository, BattleResultRepository>();

        // DATABASE.md §1 note item 2: the Identity → BossDefinitionId lookup is
        // owned by the Infrastructure layer through this existing persistence
        // boundary — no resolver service, registry, or read model is introduced.
        services.AddScoped<IBossDefinitionLookup, BossDefinitionLookup>();

        // Discord identity exchange boundary (API_CONTRACTS.md §2.2–§2.4).
        //
        // This is the registration point for the TASK-035 contract's exchange
        // client, which is a separate downstream implementation and is not
        // built by TASK-023. Until it is registered, the resolver reports the
        // exchange as unavailable and produces no identity — so no Player is
        // ever written for an unverified caller.
        //
        // It is also the default pair TASK-181 leaves in place: the
        // development-only identity source is substituted here by
        // AddDevelopmentDiscordIdentityResolver, and only when the host is in
        // Development *and* has explicitly opted in.
        services.AddSingleton<IDiscordIdentityResolver, UnconfiguredDiscordIdentityResolver>();

        // Redis connection boundary
        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisConnectionString));

            // Active battle state store (REDIS_STATE.md §1–§4;
            // ARCHITECTURE.md §1, §3 `BattleStateRepository (Redis)`).
            //
            // It is the documented record of truth for an active battle's live
            // state: battle creation writes it, each resolution loads it and
            // saves it back under the Sequence compare-and-set, and the sliding
            // 30-minute expiry is refreshed on a successful resolution. It is
            // registered inside the connection-string branch because it cannot
            // function without a connection, and REDIS_STATE.md §2 item 2 / §7
            // item 5 permit no in-process substitute.
            services.AddSingleton<IBattleStateRepository, BattleStateRepository>();
        }

        return services;
    }

    /// <summary>
    /// Registers the development-only identity source in place of the unavailable
    /// Discord exchange (TASK-181).
    ///
    /// <b>Fail closed, structurally.</b> Both conditions are required — the host's
    /// own Development environment, passed in by the composition root from
    /// <c>IHostEnvironment</c>, and the explicit
    /// <see cref="DevelopmentAuthenticationOptions.EnabledPath"/> opt-in switch —
    /// and either one missing returns the collection untouched, leaving
    /// <c>UnconfiguredDiscordIdentityResolver</c> as the registered implementation.
    /// The gate therefore cannot be armed by configuration alone: a production
    /// <c>appsettings.json</c> that sets the switch achieves nothing, because the
    /// environment condition is not a configuration value this method reads from
    /// the app's own files (TASK-181 AC-01/AC-02).
    ///
    /// The substitution happens at the <see cref="IDiscordIdentityResolver"/> seam
    /// only. No endpoint, authorization policy, or session issuer is involved, so
    /// the development path obtains a real application session through the
    /// existing infrastructure rather than beside it (<c>ADR-015</c> D6).
    /// </summary>
    /// <param name="services">The composition root's service collection.</param>
    /// <param name="configuration">The application configuration (opt-in switch).</param>
    /// <param name="isDevelopmentEnvironment">
    /// Whether the host is running in the Development environment. It is supplied
    /// by the caller because it is a host fact, not an application setting.
    /// </param>
    public static IServiceCollection AddDevelopmentDiscordIdentityResolver(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopmentEnvironment)
    {
        if (!isDevelopmentEnvironment || !DevelopmentAuthenticationOptions.IsEnabled(configuration))
        {
            // Neither condition may be waived, and an un-enabled host gets the
            // composition it has today: no development identity source exists at
            // all, so there is nothing to reach (AC-01/AC-02).
            return services;
        }

        services.Replace(ServiceDescriptor.Singleton<IDiscordIdentityResolver>(
            _ => new DevelopmentDiscordIdentityResolver()));

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
