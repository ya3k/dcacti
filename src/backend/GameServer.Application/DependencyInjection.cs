using GameServer.Application.Battle;
using GameServer.Application.Cards;
using GameServer.Application.Pets;
using GameServer.Application.Relics;
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

        // Battle lifecycle boundary (GAME_STATE.md §2; REDIS_STATE.md §1–§4).
        //
        // Creates a battle, loads it at the start of a resolution, runs the
        // documented Domain pipeline, and stores the result under the Sequence
        // compare-and-set. It holds no authoritative state of its own: that is
        // the active-state record's, reached through IBattleStateRepository
        // (REDIS_STATE.md §2 item 2), whose implementation is registered by the
        // Infrastructure layer.
        //
        // The seed source is the server-side entropy source for RngSeed
        // (GAME_STATE.md §2.6.1); it is never supplied or influenced by the
        // client.
        services.AddSingleton<IRngSeedSource, SystemEntropyRngSeedSource>();
        services.AddSingleton<BattleStateService>();

        // Denormalized Pet Level recompute hook (DATABASE.md §1;
        // ADR-012 Consequences; PET_RULES.md §5 item 2). Scoped because it
        // resolves the scoped IPetRepository/IPlayerRepository boundaries.
        services.AddScoped<PetLevelService>();

        // Battle-start Relic loadout validation and snapshot preparation
        // (RELIC_RULES.md §2.1–§2.5; API_CONTRACTS.md §3). Scoped because it
        // resolves the scoped IRelicRepository boundary. It validates
        // ownership against persistence and returns the ordered snapshot the
        // battle-creation path copies into PetState.EquippedRelics[]; it
        // writes nothing and implements no Relic trigger or effect
        // (RELIC_RULES.md §4–§5 are out of TASK-027's scope).
        services.AddScoped<RelicLoadoutService>();

        // Battle-start Card loadout validation and snapshot preparation
        // (CARD_RULES.md §1; API_CONTRACTS.md §3). Scoped because it resolves
        // the scoped ICardRepository boundary. It validates ownership,
        // category, and the per-CardDefinition loadout copy limit, derives the
        // active Pet's Signature Skill Card, and returns the 4-entry snapshot
        // the battle-creation path copies into PetState.EquippedCards[]; it
        // writes nothing and implements no Card cast, effect, or Power spend
        // (CARD_RULES.md §3 is out of TASK-028's scope).
        services.AddScoped<CardLoadoutService>();

        // Battle-start orchestration boundary (API_CONTRACTS.md §3). Scoped
        // because it resolves the scoped IPetRepository, CardLoadoutService, and
        // RelicLoadoutService boundaries (BattleStateService itself is a
        // singleton). It coordinates the documented flow — Pet resolution and
        // ownership, Boss resolution, Card loadout validation + Signature Skill
        // derivation, Relic loadout validation, then battle composition — and
        // implements none of those rules itself. It creates a battle only after
        // every validation has passed, so a rejected request creates nothing.
        services.AddScoped<BattleStartService>();

        return services;
    }
}