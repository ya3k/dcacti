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

        // Battle State Foundation / Board Foundation State lifecycle boundary
        // (GAME_STATE.md §2.0, §2.0.5).
        //
        // Holds the authoritative staged state (BattleId, Turn, Sequence,
        // RngSeed, RngState, BoardState) for a battle session and serves it to
        // the realtime boundary on group join (SIGNALR_PROTOCOL.md §4.1). It is
        // process-local and safe to lose: this staged state is explicitly NOT
        // persisted to Redis (REDIS_STATE.md §7.1, §7.4) or PostgreSQL
        // (GAME_STATE.md §2.0.5.4).
        //
        // The seed source is the server-side entropy source for RngSeed
        // (GAME_STATE.md §2.6.1); it is never supplied or influenced by the
        // client.
        services.AddSingleton<IRngSeedSource, SystemEntropyRngSeedSource>();
        services.AddSingleton<BattleStateService>();

        return services;
    }
}