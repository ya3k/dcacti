using GameServer.Application.Battle;
using GameServer.Application.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Application layer boundary registration
        //
        // RuntimeService holds live connection-only status for the realtime
        // runtime boundary. It is process-local and safe to lose: it is not
        // active battle state (REDIS_STATE.md) and stores no battle-scoped data.
        services.AddSingleton<RuntimeService>();

        // Battle State Foundation lifecycle boundary (GAME_STATE.md §2.0).
        //
        // Holds the authoritative foundation state (BattleId, Turn, Sequence)
        // for a battle session and serves it to the realtime boundary on group
        // join (SIGNALR_PROTOCOL.md §4.1). It is process-local and safe to
        // lose: Foundation State is explicitly NOT persisted to Redis
        // (REDIS_STATE.md §7.1) or PostgreSQL (GAME_STATE.md §2.0.4).
        services.AddSingleton<BattleStateService>();

        return services;
    }
}