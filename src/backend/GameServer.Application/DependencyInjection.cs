using GameServer.Application.Battle;
using GameServer.Application.Cards;
using GameServer.Application.Collection;
using GameServer.Application.Pets;
using GameServer.Application.Players;
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

        // A singleton, and it must stay one.
        //
        // It carries two in-process registries — each battle's Pet loadout input
        // and the Boss definition it was created from — which the resolution
        // reads for configuration the mutable state deliberately does not
        // duplicate (GAME_STATE.md §0 item 5). Those registries are populated at
        // creation and consumed by later resolutions of the same battle, so a
        // per-request instance would lose every battle's configuration between
        // the request that created it and the one that resolves it. Nothing here
        // is authoritative battle state: the record in the active-state store is
        // (REDIS_STATE.md §2 item 2), which is why this registration is not the
        // "state in process memory" that §7 item 5 forbids.
        //
        // The durable battle result boundary it invokes on the terminal path is
        // therefore resolved lazily, per call, from the request scope — see
        // below.
        services.AddSingleton<BattleStateService>();

        // The durable battle result boundary (ARCHITECTURE.md §4 item 4,
        // DATABASE.md §1): on the terminal path it writes the battle's
        // BattleResult row and then clears the active state, in that order.
        // Scoped because it resolves the scoped PostgreSQL result repository and
        // the two battle-end lookups; it holds no state of its own.
        //
        // Its clock is the server's (TimeProvider.System): DATABASE.md §1
        // "Duration and completion sourcing" item 2 makes CompletedAt the server
        // clock reading at the durable write, and no other source is permitted.
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<BattleResultService>();

        // The completed-battle result read behind GET /api/battle/{battleId}/result
        // (API_CONTRACTS.md §4). Scoped for the same reason as the boundary above:
        // it resolves the scoped PostgreSQL result repository. It performs the
        // document's owner-only read (note 7), so the caller's authenticated
        // identity is established before any row is returned.
        services.AddScoped<BattleResultQueryService>();

        // Resolves the scoped durable result boundary for the singleton
        // resolution service above, in a scope of its own, so the terminal
        // battle-end step gets the request-scoped PostgreSQL context it needs
        // without the singleton capturing a scope. It is the composition root's
        // one piece of glue for that lifetime difference; the resolution service
        // still depends only on the Application contract.
        services.AddSingleton<IBattleResultPersistence>(provider =>
            new ScopedBattleResultPersistence(
                provider.GetRequiredService<IServiceScopeFactory>()));

        // Resolves the scoped Card repository for the singleton BattleStateService
        // via IServiceScopeFactory so Card definition lookups during CardCast
        // have access to persistence without the singleton capturing a scope.
        services.AddSingleton<ICardDefinitionLookup>(provider =>
            new ScopedCardDefinitionLookup(
                provider.GetRequiredService<IServiceScopeFactory>()));

        // The shared/static RelicDefinition content read (DATABASE.md §1: the
        // RelicDefinition row, by RelicDefinitionId), reached through the same
        // scope-factory adaptation as the Card lookup above so a singleton
        // consumer can read Relic content without capturing the scoped
        // GameDbContext. It exposes the Domain RelicDefinition only — no EF
        // entity, DbContext, or JSON payload crosses this boundary — and it
        // resolves no Relic: trigger evaluation, condition evaluation, and effect
        // application remain unimplemented (RELIC_RULES.md §8.7).
        services.AddSingleton<IRelicDefinitionLookup>(provider =>
            new ScopedRelicDefinitionLookup(
                provider.GetRequiredService<IServiceScopeFactory>()));

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
        // because it resolves the scoped IPetRepository, CardLoadoutService,
        // RelicLoadoutService, and IRelicRepository boundaries (BattleStateService
        // itself is a singleton, and the IRelicDefinitionLookup singleton is safe
        // to consume from a scope). It coordinates the documented flow — Pet
        // resolution and ownership, Boss resolution, Card loadout validation +
        // Signature Skill derivation, Relic loadout validation, the equipped
        // Relics' shared definition resolution, then battle composition — and
        // implements none of those rules itself. It creates a battle only after
        // every validation has passed, so a rejected request creates nothing.
        //
        // The Relic definition resolution happens HERE, at battle start, so that
        // GAME_RULES.md §17 step 11 reads a Relic's declared content from the
        // battle's own content configuration: TDD.md §4 item 3 keeps PostgreSQL off
        // the path that resolves a single Swap, and Relic trigger/condition/effect
        // resolution reads no PostgreSQL of its own.
        services.AddScoped<BattleStartService>();

        // Starter ownership composition (DATABASE.md §2 item 1). Scoped because
        // it resolves the scoped Pet/Card/Relic repository boundaries. It builds
        // the deterministic starter set a newly created Player receives and
        // resolves every row against the provisioned definition content; it
        // writes nothing itself — the Player-creation persistence boundary
        // stages the set and commits it atomically with the Player row
        // (DATABASE.md §2 item 4).
        services.AddScoped<PlayerStarterGrantFactory>();

        // The collection read behind GET /api/pets, /api/pets/{petId},
        // /api/cards, and /api/relics (API_CONTRACTS.md §5.1–§5.6). Scoped
        // because it resolves the scoped Pet/Card/Relic repository boundaries.
        // It is a read-only projection: it writes nothing, computes no gameplay
        // value, and scopes every read to the authenticated Player identity its
        // caller supplies.
        services.AddScoped<CollectionQueryService>();

        return services;
    }
}