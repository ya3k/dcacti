using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GameServer.Api.Controllers;
using GameServer.Application.Battle;
using GameServer.Domain.Battle.Serialization;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Domain.Relics;
using GameServer.Infrastructure.Postgres;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using Xunit;
using Xunit.Abstractions;

namespace GameServer.Api.Tests;

/// <summary>
/// The TASK-040 end-to-end smoke test: the documented flow against a REAL Redis
/// (<c>REDIS_STATE.md</c> §1–§4).
///
/// <code>
/// POST /api/battle/start
///         ↓
/// BattleStartService
///         ↓
/// BattleStateService.CreateBattleAsync
///         ↓
/// Redis: battle:{battleId}:state          ← asserted directly, on the real key
///         ↓
/// committed Swap (the real resolution pipeline)
///         ↓
/// Sequence compare-and-set write-back
///         ↓
/// Redis updated, TTL slid
///         ↓
/// BattleStateUpdated push (SignalR → GameRuntime → BattleScene)
/// </code>
///
/// <b>Why this is not a unit test.</b> Every other suite substitutes the store
/// (an in-memory double) so it can assert something else. This one uses the
/// production composition — the real <c>BattleStartService</c>, the real
/// <c>BattleStateService</c>, the real <c>GameServer.Infrastructure.Redis.
/// BattleStateRepository</c>, and a real Redis connection — and then reads the
/// documented key with a raw Redis client, so what is verified is the record
/// that actually exists in Redis rather than a value the test itself produced.
///
/// <b>Environment.</b> It requires a reachable Redis (the <c>docker-compose.yml</c>
/// instance on 6379 by default; override with <c>DCACTI_TEST_REDIS</c>). Without
/// one the test FAILS with the remedy stated rather than passing vacuously: a
/// green run has to mean the contract was exercised. PostgreSQL is still
/// substituted with an isolated in-memory store, because this smoke test's
/// subject is Redis and <c>DATABASE.md</c> §5 item 3 puts no battle state there.
/// </summary>
public class RedisBattleStateSmokeTest
{
    private const string PetInstanceId = "smoke_pet";
    private const string PetDefinitionId = "smoke_pet_def";
    private const string HealCardId = "heal";
    private const string ShieldCardId = "shield";
    private const string PowerChargeCardId = "power_charge";
    private const string SignatureSkillCardId = "thanh_xa_skill";

    /// <summary>The documented expiry (<c>REDIS_STATE.md</c> §3): 30 minutes.</summary>
    private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(30);

    /// <summary>Read tolerance for the TTL comparison; see the Infrastructure suite.</summary>
    private static readonly TimeSpan TtlTolerance = TimeSpan.FromSeconds(30);

    private readonly ITestOutputHelper _output;

    public RedisBattleStateSmokeTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task SmokeTest_BattleStartAndSwap_ShouldPersistTheAuthoritativeStateInRedis()
    {
        using var factory = new SmokeFactory();
        var client = factory.CreateClient();

        // ---- The Redis this run actually used, reported so the evidence is
        // ---- attributable to a real instance rather than assumed.
        using var redis = await factory.ConnectToRedisAsync();

        _output.WriteLine($"Redis: {SmokeFactory.RedisConnection}");
        _output.WriteLine($"PING: {await redis.GetDatabase().PingAsync()}");

        var playerId = await factory.SeedAsync();

        // ===================================================================
        // 1. POST /api/battle/start  →  battle:{battleId}:state
        // ===================================================================
        var request = new
        {
            petId = PetInstanceId,
            bossId = "boss-hoa-long",
            cardLoadout = new[] { HealCardId, ShieldCardId, PowerChargeCardId },
            relicLoadout = new[] { "relic_a", "relic_b", "relic_c" },
        };

        var message = new HttpRequestMessage(HttpMethod.Post, "/api/battle/start")
        {
            Content = JsonContent.Create(request),
        };
        message.Headers.Add("X-Smoke-PlayerId", playerId);

        var response = await client.SendAsync(message);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var battleId = body.GetProperty("battleId").GetString()!;

        _output.WriteLine($"POST /api/battle/start → 200 battleId={battleId}");

        var database = redis.GetDatabase();
        var key = $"battle:{battleId}:state";

        // ---- The documented key exists, immediately after creation ---------
        // REDIS_STATE.md §1 (key) and §3 ("Created: on POST /api/battle/start").
        Assert.True(await database.KeyExistsAsync(key), $"REDIS_STATE.md §1: '{key}' must exist");
        _output.WriteLine($"EXISTS {key} = true");

        // ---- It is the ONLY key this battle created -----------------------
        // §1 defines one state key; no per-resolution, board, or lock key is
        // introduced, and §2 item 1 forbids a Redis-only field.
        var keys = new List<string>();
        await foreach (var found in redis.GetServer(redis.GetEndPoints().First())
                           .KeysAsync(pattern: $"battle:{battleId}:*"))
        {
            keys.Add(found.ToString());
        }

        keys.Sort(StringComparer.Ordinal);
        Assert.Equal([key], keys);
        _output.WriteLine($"KEYS battle:{battleId}:* = [{string.Join(", ", keys)}]");

        // ---- The value is the TASK-029 authoritative runtime JSON ----------
        // §2 item 1: the GAME_STATE.md §2 shape exactly, with no Redis-only
        // field and no wire projection. Round-tripped through the same
        // serializer the store uses, and its JSON members checked directly.
        var storedRaw = (await database.StringGetAsync(key)).ToString();

        Assert.False(string.IsNullOrWhiteSpace(storedRaw));

        var persisted = BattleStateSerializer.Deserialize(storedRaw);

        Assert.Equal(battleId, persisted.BattleId);
        Assert.Equal(0, persisted.Turn);
        Assert.Equal(0, persisted.Sequence);
        Assert.Equal(64, persisted.BoardState.Cells.Count);

        // ---- Both battle identities survived the whole real flow -----------
        // GAME_STATE.md §2.8 items 1–2 / §2.3 / ADR-014 decisions 1 and 4: the
        // record written by the real POST /api/battle/start carries the
        // authenticated requesting Player's identity and the owned Pet INSTANCE
        // the request selected, so the battle-end persistence path can source
        // BattleResult.PlayerId and BattleResult.PetInstanceId from it
        // (DATABASE.md §1) without a session lookup or client input.
        Assert.Equal(new GameServer.Domain.Players.PlayerId(playerId), persisted.PlayerId);
        Assert.Equal(playerId, persisted.PlayerId.Value);

        Assert.Equal(new GameServer.Domain.Pets.PetId(PetInstanceId), persisted.PetState.PetId);
        Assert.Equal(PetInstanceId, persisted.PetState.PetId.Value);

        _output.WriteLine($"playerId={persisted.PlayerId.Value} petId={persisted.PetState.PetId.Value}");

        using (var document = JsonDocument.Parse(storedRaw))
        {
            var rootNames = document.RootElement.EnumerateObject().Select(p => p.Name).ToArray();

            // The documented §2 root member set. `lastCommittedSwapPair` is
            // absent because no Swap has been committed yet (§2.1.10 item 3).
            // `playerId` is present: it is the Battle Identity member §2.8
            // documents, recorded at creation and carried in the record so the
            // battle-end persistence path can source BattleResult.PlayerId
            // (DATABASE.md §1, ADR-014 decision 1).
            Assert.Equal(
                [
                    "battleId", "playerId", "turn", "sequence", "rngSeed", "rngState",
                    "boardState", "combo", "matchCount", "petState", "bossState",
                ],
                rootNames);

            // No Redis-only member and no transport member leaked in.
            foreach (var forbidden in
                     new[] { "key", "ttl", "expire", "lock", "connectionId", "state", "board", "playerState" })
            {
                Assert.DoesNotContain(forbidden, rootNames);
            }

            _output.WriteLine($"JSON root members: {string.Join(", ", rootNames)}");
        }

        // ---- Sequence is 0, and a 30-minute TTL is present -----------------
        Assert.Equal(0, persisted.Sequence);

        var ttlAtCreation = await database.KeyTimeToLiveAsync(key);
        Assert.NotNull(ttlAtCreation);
        AssertWithinDocumentedTtl(ttlAtCreation!.Value, "at creation");
        _output.WriteLine($"TTL at creation = {ttlAtCreation}");

        // ===================================================================
        // 2. A committed Swap  →  one CAS write-back, TTL slid
        // ===================================================================
        // The real hub drives the real resolution. The battle group is joined
        // first so the resolved-state push is delivered to this connection —
        // the documented SignalR → GameRuntime → BattleScene path.
        var pair = FindAdjacentPairThatProducesAMatch(factory, battleId);

        _output.WriteLine($"Swap {pair.From}→{pair.To}");

        // Collapse the expiry so the §3 refresh is observable as a real change
        // rather than as the creation value being re-read.
        await database.KeyExpireAsync(key, TimeSpan.FromSeconds(60));
        var ttlBeforeSwap = await database.KeyTimeToLiveAsync(key);
        Assert.NotNull(ttlBeforeSwap);
        Assert.True(ttlBeforeSwap!.Value < TimeSpan.FromMinutes(5));
        _output.WriteLine($"TTL before Swap (deliberately shortened) = {ttlBeforeSwap}");

        var hubConnection = factory.BuildHubConnection();
        var statePush = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<JsonElement>("BattleStateUpdated", payload => statePush.TrySetResult(payload));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);
        await statePush.Task.WaitAsync(TimeSpan.FromSeconds(15));

        var pushed = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<JsonElement>("BattleStateUpdated", payload => pushed.TrySetResult(payload));

        var swapResult = await hubConnection.InvokeAsync<GameServer.Api.Hubs.SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "smoke-seq-1");

        Assert.True(swapResult.Accepted, "the smoke Swap must be accepted");
        _output.WriteLine("Swap accepted=true");

        // ---- Redis now holds the RESOLVED state, at Sequence 1 -------------
        var afterSwapRaw = (await database.StringGetAsync(key)).ToString();

        Assert.NotEqual(storedRaw, afterSwapRaw);

        var afterSwap = BattleStateSerializer.Deserialize(afterSwapRaw);

        Assert.Equal(1, afterSwap.Sequence);
        Assert.Equal(1, afterSwap.Turn);
        Assert.NotNull(afterSwap.LastCommittedSwapPair);
        Assert.True(afterSwap.MatchCount >= 1);

        // ---- §4 item 5: ONE write-back, not one per Match or per pass -----
        // The stored record is the stable post-resolution state: the counters
        // advanced once for this one action, and the commit record names the
        // pair this Swap committed, canonically ordered (§2.1.10 item 2).
        Assert.Equal(
            (Math.Min(pair.From, pair.To), Math.Max(pair.From, pair.To)),
            (afterSwap.LastCommittedSwapPair!.Value.MinCellIndex,
                afterSwap.LastCommittedSwapPair.Value.MaxCellIndex));

        _output.WriteLine(
            $"REDIS after Swap: sequence={afterSwap.Sequence} turn={afterSwap.Turn} "
            + $"combo={afterSwap.Combo} matchCount={afterSwap.MatchCount} "
            + $"committedPair=({afterSwap.LastCommittedSwapPair.Value.MinCellIndex},"
            + $"{afterSwap.LastCommittedSwapPair.Value.MaxCellIndex}) "
            + $"bossHp={afterSwap.BossState.HP} petHp={afterSwap.PetState.HP}");

        // ---- §3: the TTL slid back to the documented 30 minutes -----------
        var ttlAfterSwap = await database.KeyTimeToLiveAsync(key);

        Assert.NotNull(ttlAfterSwap);
        AssertWithinDocumentedTtl(ttlAfterSwap!.Value, "after a successful resolution");
        Assert.True(
            ttlAfterSwap!.Value > ttlBeforeSwap.Value,
            "REDIS_STATE.md §3: a successful resolution must slide the expiry forward");

        _output.WriteLine($"TTL after Swap = {ttlAfterSwap} (slid forward)");

        // ---- The delivered state matches the stored record ----------------
        // The push reports the same committed transition the record holds, so
        // the client renders what Redis carries.
        var pushedPayload = await pushed.Task.WaitAsync(TimeSpan.FromSeconds(15));

        Assert.Equal(battleId, pushedPayload.GetProperty("battleId").GetString());
        Assert.Equal(afterSwap.Sequence, pushedPayload.GetProperty("sequence").GetInt32());
        Assert.Equal(afterSwap.Turn, pushedPayload.GetProperty("turn").GetInt32());
        Assert.Equal(
            afterSwap.MatchCount,
            pushedPayload.GetProperty("playerState").GetProperty("matchCount").GetInt32());
        Assert.Equal(
            afterSwap.Combo,
            pushedPayload.GetProperty("playerState").GetProperty("combo").GetInt32());

        _output.WriteLine(
            $"BattleStateUpdated push: sequence={pushedPayload.GetProperty("sequence").GetInt32()} "
            + $"matchCount={pushedPayload.GetProperty("playerState").GetProperty("matchCount").GetInt32()}");

        await hubConnection.StopAsync();

        // ===================================================================
        // 3. A stale compare-and-set cannot overwrite the newer state
        // ===================================================================
        // REDIS_STATE.md §4 items 2–3: the write succeeds only while the stored
        // Sequence still matches what the resolution read. An update built from
        // the PRE-Swap state (Sequence 0) must be refused, leaving the committed
        // Sequence-1 record untouched.
        var repository = factory.Services.GetRequiredService<IBattleStateRepository>();

        var staleState = persisted with { Sequence = 1, MatchCount = 4242 };

        Assert.False(
            await repository.TryUpdateAsync(staleState, expectedSequence: persisted.Sequence),
            "§4 item 2: a write whose expected Sequence no longer matches must be refused");

        var afterStaleAttempt = BattleStateSerializer.Deserialize(
            (await database.StringGetAsync(key)).ToString());

        Assert.Equal(1, afterStaleAttempt.Sequence);
        Assert.NotEqual(4242, afterStaleAttempt.MatchCount);
        Assert.Equal(afterSwap.MatchCount, afterStaleAttempt.MatchCount);

        _output.WriteLine(
            $"Stale CAS refused; stored matchCount still {afterStaleAttempt.MatchCount} (not 4242)");

        // ---- Cleanup: leave no smoke record behind ------------------------
        await database.KeyDeleteAsync(key);
    }

    /// <summary>
    /// Asserts an observed TTL is the documented 30 minutes
    /// (<c>REDIS_STATE.md</c> §3), within the read tolerance.
    /// </summary>
    private static void AssertWithinDocumentedTtl(TimeSpan observed, string when)
    {
        Assert.True(
            observed <= StateTtl && observed >= StateTtl - TtlTolerance,
            $"REDIS_STATE.md §3 fixes the active state's expiry at 30 minutes of "
            + $"inactivity; observed {observed} {when}.");
    }

    /// <summary>
    /// Finds an adjacent pair the production validator accepts, so the smoke
    /// Swap exercises the real resolution rather than a hand-picked pair.
    /// </summary>
    private static GameServer.Domain.Match3.SwapRequest FindAdjacentPairThatProducesAMatch(
        SmokeFactory factory,
        string battleId)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<BattleStateService>();

        // The pair is chosen from the stored record, so the smoke test reads the
        // same authoritative state Redis holds.
        var state = service.GetBattleAsync(battleId).GetAwaiter().GetResult();
        Assert.NotNull(state);

        for (var row = 0; row < GameServer.Domain.Match3.BoardState.Rows; row++)
        {
            for (var column = 0; column < GameServer.Domain.Match3.BoardState.Columns; column++)
            {
                var from = GameServer.Domain.Match3.BoardState.ToIndex(row, column);

                var candidates = new List<int>(2);

                if (column + 1 < GameServer.Domain.Match3.BoardState.Columns)
                {
                    candidates.Add(GameServer.Domain.Match3.BoardState.ToIndex(row, column + 1));
                }

                if (row + 1 < GameServer.Domain.Match3.BoardState.Rows)
                {
                    candidates.Add(GameServer.Domain.Match3.BoardState.ToIndex(row + 1, column));
                }

                foreach (var to in candidates)
                {
                    // Asking the production validator is what makes this a real
                    // scenario: the chosen pair is one the server will accept.
                    if (GameServer.Domain.Match3.SwapValidator
                        .Validate(
                            state!.BoardState,
                            state.LastCommittedSwapPair,
                            new GameServer.Domain.Match3.SwapRequest(from, to))
                        .IsAccepted)
                    {
                        return new GameServer.Domain.Match3.SwapRequest(from, to);
                    }
                }
            }
        }

        throw new InvalidOperationException("The smoke battle's board accepted no adjacent Swap.");
    }

    /// <summary>
    /// A host composed exactly like the application's, except that Redis is REAL
    /// and PostgreSQL is an isolated in-memory store.
    ///
    /// This is the one API host that does not blank
    /// <c>ConnectionStrings:Redis</c>: leaving it configured is what makes the
    /// production Redis registration and the production
    /// <c>BattleStateRepository</c> the subject of the test.
    /// </summary>
    private sealed class SmokeFactory : WebApplicationFactory<Program>
    {

        /// <summary>
        /// The Redis the smoke test speaks to — the local development instance
        /// (<c>docker-compose.yml</c>, <c>.env.example</c>), overridable.
        /// </summary>
        internal static readonly string RedisConnection =
            Environment.GetEnvironmentVariable("DCACTI_TEST_REDIS") ?? "127.0.0.1:6379";

        private readonly string _storeName = $"redis-smoke-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // PostgreSQL is substituted; Redis is NOT — the real Redis
            // composition is what this smoke test exists to exercise.
            builder.UseSetting("ConnectionStrings:DefaultConnection", "");
            builder.UseSetting("ConnectionStrings:Redis", RedisConnection);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<GameDbContext>>();
                services.RemoveAll<GameDbContext>();
                services.AddDbContext<GameDbContext>(options =>
                    options.UseInMemoryDatabase(_storeName));

                services.AddSingleton<IStartupFilter>(new SmokeIdentityStartupFilter());
            });
        }

        /// <summary>
        /// Opens a client connection to the same Redis, so the assertions read
        /// the documented key directly rather than through the store under test.
        /// </summary>
        public async Task<IConnectionMultiplexer> ConnectToRedisAsync()
        {
            try
            {
                return await ConnectionMultiplexer.ConnectAsync(
                    new ConfigurationOptions
                    {
                        EndPoints = { RedisConnection },
                        AbortOnConnectFail = true,
                        ConnectTimeout = 3000,
                    });
            }
            catch (RedisConnectionException exception)
            {
                // Reported as a failure, not a pass: without Redis this smoke
                // test has verified nothing.
                throw new InvalidOperationException(
                    $"TASK-040's smoke test requires a reachable Redis at "
                    + $"'{RedisConnection}' (docker compose up -d redis, or set "
                    + "DCACTI_TEST_REDIS). It verifies the documented "
                    + "battle:{battleId}:state record directly, so it cannot run "
                    + "without one.",
                    exception);
            }
        }

        /// <summary>
        /// A hub connection to this host's server, for the documented realtime
        /// path.
        /// </summary>
        public Microsoft.AspNetCore.SignalR.Client.HubConnection BuildHubConnection() =>
            new Microsoft.AspNetCore.SignalR.Client.HubConnectionBuilder()
                .WithUrl("http://localhost/hubs/battle", options =>
                {
                    options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
                })
                .Build();

        /// <summary>
        /// Supplies the authenticated Player identity the endpoint requires,
        /// standing in for the TASK-034 session mechanism so the documented flow
        /// can be walked end to end. It defines no session format.
        /// </summary>
        private sealed class SmokeIdentityStartupFilter : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
                app =>
                {
                    app.Use(async (context, inner) =>
                    {
                        if (context.Request.Headers.TryGetValue("X-Smoke-PlayerId", out var playerId))
                        {
                            context.Items[BattleController.AuthenticatedPlayerItemKey] =
                                playerId.ToString();
                        }

                        await inner();
                    });

                    next(app);
                };
        }

        /// <summary>
        /// Seeds the documented battle-start inputs: one Player owning one Pet
        /// (with its definition), three unlocked Basic Cards plus the Pet's
        /// Signature Skill Card, and three owned Relic instances.
        /// </summary>
        public async Task<string> SeedAsync()
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

            var playerId = $"player_smoke_{Guid.NewGuid():N}";

            context.Players.Add(new Player
            {
                PlayerId = playerId,
                DiscordUserId = $"8035{Random.Shared.NextInt64(1_000_000_000_000_000L):D16}",
                Level = Player.InitialLevel,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            context.PetDefinitions.Add(new PetDefinition
            {
                PetDefinitionId = PetDefinitionId,
                Identity = "Thanh Xà",
                Element = Element.Moc,
                PetLevelMultiplier = 1.0m,
                PassiveId = new PassiveId("thanh-xa-regen"),
                PassiveThreshold = 5,
                SignatureSkillCardId = SignatureSkillCardId,
            });

            context.Pets.Add(new Pet
            {
                PetInstanceId = PetInstanceId,
                PlayerId = playerId,
                PetDefinitionId = PetDefinitionId,
                Tier = PetTier.Common,
                Star = 1,
                Level = 1,
                AcquiredAt = DateTimeOffset.UtcNow,
            });

            foreach (var (id, category) in new[]
            {
                (HealCardId, CardCategory.Basic),
                (ShieldCardId, CardCategory.Basic),
                (PowerChargeCardId, CardCategory.Basic),
                (SignatureSkillCardId, CardCategory.PetSkill),
            })
            {
                context.CardDefinitions.Add(new CardDefinition
                {
                    CardDefinitionId = id,
                    Name = id,
                    Category = category,
                    PowerCost = 0,
                    EffectDefinition = "effect",
                    LoadoutCopyLimit = id == HealCardId ? 1 : 3,
                });

                context.PlayerUnlockedCards.Add(new PlayerUnlockedCard
                {
                    PlayerId = playerId,
                    CardDefinitionId = id,
                });
            }

            foreach (var relicInstanceId in new[] { "relic_a", "relic_b", "relic_c" })
            {
                context.Relics.Add(new Relic
                {
                    RelicInstanceId = relicInstanceId,
                    PlayerId = playerId,
                    RelicDefinitionId = "relic_def_1",
                    AcquiredAt = DateTimeOffset.UtcNow,
                });
            }

            await context.SaveChangesAsync();

            return playerId;
        }
    }
}
