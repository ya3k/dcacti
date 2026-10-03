using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Domain.Relics;
using GameServer.Infrastructure.Postgres;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using StackExchange.Redis;
using Xunit;
using Xunit.Abstractions;

namespace GameServer.Api.Tests;

/// <summary>
/// The TASK-041 end-to-end smoke test: the documented battle-completion pipeline
/// against a REAL PostgreSQL and a REAL Redis.
///
/// <code>
/// Authenticated client
///       ↓
/// POST /api/battle/start                    (real pipeline, real Player/Pet rows)
///       ↓
/// authoritative BattleState in Redis        (battle:{battleId}:state)
///       ↓
/// committed Swap resolving to a terminal outcome
///       ↓
/// BattleResult written to PostgreSQL        (DATABASE.md §1 — asserted with SQL)
///       ↓
/// Redis active-state deletion               (REDIS_STATE.md §3)
///       ↓
/// GET /api/battle/{battleId}/result
///       ↓
/// authenticated owner receives the result
/// </code>
///
/// <b>Why this is not a unit test.</b> Every other suite substitutes one of the
/// two stores. This one uses the production composition for both — the real
/// PostgreSQL provider, the real Redis-backed active-state store, the real
/// authentication pipeline, and a real issued session — and then inspects the
/// documented key with a raw Redis client and the documented row with raw SQL, so
/// what is verified is what actually exists in each store rather than a value the
/// test itself produced.
///
/// <b>Environment.</b> It requires a reachable PostgreSQL
/// (<c>docker-compose.yml</c>'s <c>localhost:5433</c>) and Redis
/// (<c>localhost:6379</c>), both overridable by environment variable. Without
/// them the test FAILS with the remedy stated rather than passing vacuously: a
/// green run has to mean the contract was exercised. PostgreSQL is used exactly
/// as the application uses it; the schema must already be migrated
/// (<c>dotnet ef database update</c>).
/// </summary>
public class BattleResultSmokeTest
{
    private const string PetInstanceIdPrefix = "smoke_result_pet";
    private const string PetDefinitionId = "smoke_result_pet_def";
    private const string HealCardId = "heal";
    private const string ShieldCardId = "shield";
    private const string PowerChargeCardId = "power_charge";
    private const string SignatureSkillCardId = "thanh_xa_skill";

    private readonly ITestOutputHelper _output;

    public BattleResultSmokeTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task SmokeTest_AuthoritativeBattleActionToResultRead_ShouldWalkTheWholeDocumentedPath()
    {
        using var factory = new SmokeFactory();
        var client = factory.CreateClient();

        // ---- The two real services this run actually used, reported so the
        // ---- evidence is attributable rather than assumed.
        using var redis = await factory.ConnectToRedisAsync();

        _output.WriteLine($"PostgreSQL: {SmokeFactory.PostgresConnection}");
        _output.WriteLine($"Redis:      {SmokeFactory.RedisConnection}");
        _output.WriteLine($"Redis PING: {await redis.GetDatabase().PingAsync()}");

        var playerId = await factory.SeedAsync();

        _output.WriteLine($"playerId = {playerId}");

        // ===================================================================
        // 1. POST /api/battle/start  →  battle:{battleId}:state
        // ===================================================================
        // The real endpoint, authenticated with a real session (ADR-015 D4/D10).
        var startMessage = new HttpRequestMessage(HttpMethod.Post, "/api/battle/start")
        {
            Content = JsonContent.Create(new
            {
                petId = factory.PetInstanceId,
                bossId = "boss-hoa-long",
                cardLoadout = new[] { HealCardId, ShieldCardId, PowerChargeCardId },

                // The relic instance ids this run actually seeded (RELIC_RULES.md
                // §2.1: every element must be an owned instance of the requesting
                // Player).
                relicLoadout = factory.OwnedRelicInstanceIds.ToArray(),
            }),
        };

        startMessage.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            factory.SessionToken);

        var startResponse = await client.SendAsync(startMessage);

        if (startResponse.StatusCode != HttpStatusCode.OK)
        {
            _output.WriteLine($"POST /api/battle/start → {(int)startResponse.StatusCode} "
                + await startResponse.Content.ReadAsStringAsync());
        }

        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);

        var startBody = await startResponse.Content.ReadFromJsonAsync<JsonElement>();
        var battleId = startBody.GetProperty("battleId").GetString()!;
        var key = $"battle:{battleId}:state";

        _output.WriteLine($"POST /api/battle/start → 200 battleId={battleId}");

        // The authoritative record exists in REAL Redis.
        Assert.True(await redis.GetDatabase().KeyExistsAsync(key), $"REDIS_STATE.md §1: '{key}' must exist");
        _output.WriteLine($"EXISTS {key} = true");

        var hubConnection = factory.BuildHubConnection();
        var statePushes = new List<JsonElement>();

        await hubConnection.StartAsync();

        // ===================================================================
        // 2. Committed Swaps until the resolution reaches the terminal outcome
        // ===================================================================
        // The battle is played through the real hub, over the real session, against
        // the real content Boss — the documented pipeline throughout.
        hubConnection.On<JsonElement>("BattleStateUpdated", payload =>
        {
            lock (statePushes)
            {
                statePushes.Add(payload);
            }
        });

        await hubConnection.InvokeAsync("JoinBattle", battleId);

        factory.Track(battleId);

        var pair = await factory.PlayToTerminalAsync(hubConnection, _output);
        var playedSwaps = factory.CommittedSwapCount;

        _output.WriteLine($"Terminal committed Swap {pair.From}→{pair.To}");

        await hubConnection.StopAsync();

        // ===================================================================
        // 3. BattleResult in REAL PostgreSQL  (DATABASE.md §1)
        // ===================================================================
        var row = await factory.ReadResultRowAsync(battleId);

        Assert.NotNull(row);

        _output.WriteLine("BattleResult row:");
        _output.WriteLine($"  BattleResultId  = {row!.BattleResultId}");
        _output.WriteLine($"  PlayerId        = {row.PlayerId}");
        _output.WriteLine($"  PetInstanceId   = {row.PetInstanceId}");
        _output.WriteLine($"  BossDefinitionId= {row.BossDefinitionId}");
        _output.WriteLine($"  Outcome         = {row.Outcome}");
        _output.WriteLine($"  DurationTurns   = {row.DurationTurns}");
        _output.WriteLine($"  CompletedAt     = {row.CompletedAt:O}");
        _output.WriteLine($"  RewardSummary   = {row.RewardSummary}");

        // DATABASE.md §1 sourcing item 1: the row's key IS the battle's own id.
        Assert.Equal(battleId, row.BattleResultId);

        // §1 sourcing item 2: both identities from authoritative state.
        Assert.Equal(playerId, row.PlayerId);
        Assert.Equal(factory.PetInstanceId, row.PetInstanceId);

        // §1 note item 2: the provisioned row's persistence key — never the
        // canonical Identity and never a display name.
        Assert.Equal(BossDefinitions.HoaLong.BossDefinitionId, row.BossDefinitionId);
        Assert.NotEqual(BossDefinitions.HoaLong.BossId.Value, row.BossDefinitionId);

        // GAME_EVENTS.md §2 / TASK-050 Decision C: the one documented vocabulary.
        // Which of the two values applies is the resolution's own decision
        // (GAME_RULES.md §1.4 ends the battle when either side reaches 0 HP), so
        // the assertion is on the closed set and on the value being one the
        // contract defines — not on which side happened to win this run.
        Assert.Contains(row.Outcome, new[] { "victory", "defeat" });

        // §1 "Duration and completion sourcing" item 1: the terminal Turn IS
        // counted, so the row records the Turn the terminal committed Swap left.
        Assert.Equal(playedSwaps, row.DurationTurns);
        Assert.True(row.DurationTurns >= 1);

        // Item 2: a server clock reading captured at the write.
        Assert.InRange(
            row.CompletedAt,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddMinutes(5));

        // §1 "Reward semantics": the populated summary on the landed path — both
        // tracks' documented members, replacing the `{}` staging value that item 4
        // kept in force only until the implementation task landed. Every value is
        // server-derived from the battle's own outcome.
        using (var summary = JsonDocument.Parse(row.RewardSummary))
        {
            Assert.Equal(JsonValueKind.Object, summary.RootElement.ValueKind);
            Assert.NotEmpty(summary.RootElement.EnumerateObject());

            // GAME_EVENTS.md §2 / PET_RULES.md §5.3: a victory grants +100 on both
            // tracks and a defeat grants +0 on both, so the members are the same
            // eight either way and hold the outcome's documented amounts.
            var expectedGain = row.Outcome == "victory" ? 100 : 0;

            Assert.Equal(expectedGain, summary.RootElement.GetProperty("playerXpGained").GetInt32());
            Assert.Equal(expectedGain, summary.RootElement.GetProperty("petXpGained").GetInt32());
        }

        // ===================================================================
        // 4. Redis active-state deletion  (REDIS_STATE.md §3)
        // ===================================================================
        Assert.False(
            await redis.GetDatabase().KeyExistsAsync(key),
            $"REDIS_STATE.md §3: a completed battle's '{key}' must be deleted after the result write");

        _output.WriteLine($"EXISTS {key} = false  (deleted after the durable write)");

        // ===================================================================
        // 5. GET /api/battle/{battleId}/result  →  the authenticated owner
        // ===================================================================
        var resultMessage = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/battle/{battleId}/result");

        resultMessage.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            factory.SessionToken);

        var resultResponse = await client.SendAsync(resultMessage);

        Assert.Equal(HttpStatusCode.OK, resultResponse.StatusCode);

        var body = await resultResponse.Content.ReadFromJsonAsync<JsonElement>();

        _output.WriteLine("GET /api/battle/{battleId}/result → 200");
        _output.WriteLine($"  {body}");

        // API_CONTRACTS.md §4: exactly the four documented members.
        Assert.Equal(
            new[] { "battleId", "durationTurns", "outcome", "rewards" },
            body.EnumerateObject().Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal));

        Assert.Equal(battleId, body.GetProperty("battleId").GetString());
        Assert.Equal(row.Outcome, body.GetProperty("outcome").GetString());
        Assert.Equal(row.DurationTurns, body.GetProperty("durationTurns").GetInt32());

        // §4 note 1: rewards is always present, and is the stored RewardSummary as
        // the object §4's shape shows. TASK-068 resolved the defeat shape as Option
        // A, so both outcomes carry the same member set — the value the row holds is
        // what the wire member reports, unchanged (note 1).
        var rewards = body.GetProperty("rewards");
        Assert.Equal(JsonValueKind.Object, rewards.ValueKind);
        Assert.NotEmpty(rewards.EnumerateObject());

        using (var storedSummary = JsonDocument.Parse(row.RewardSummary))
        {
            // Compared member-by-member rather than as raw text: the two documents
            // are the same JSON value, but the wire encoding need not reproduce the
            // stored string's incidental whitespace.
            Assert.Equal(
                storedSummary.RootElement.EnumerateObject()
                    .OrderBy(member => member.Name, StringComparer.Ordinal)
                    .Select(member => $"{member.Name}={member.Value.GetRawText()}"),
                rewards.EnumerateObject()
                    .OrderBy(member => member.Name, StringComparer.Ordinal)
                    .Select(member => $"{member.Name}={member.Value.GetRawText()}"));
        }

        // ===================================================================
        // 6. The same read as an unauthenticated and as a foreign caller
        // ===================================================================
        var anonymous = await client.GetAsync($"/api/battle/{battleId}/result");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var anonymousBody = await anonymous.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("UNAUTHENTICATED", anonymousBody.GetProperty("error").GetString());
        _output.WriteLine("GET without a session → 401 UNAUTHENTICATED");

        var strangerMessage = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/battle/{battleId}/result");

        strangerMessage.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestApplicationSession.Mint($"player_smoke_stranger_{Guid.NewGuid():N}"));

        var stranger = await client.SendAsync(strangerMessage);

        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);

        var strangerBody = await stranger.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("BATTLE_NOT_FOUND", strangerBody.GetProperty("error").GetString());
        _output.WriteLine("GET as a foreign player → 404 BATTLE_NOT_FOUND (same answer as missing)");

        // ---- Cleanup: leave no smoke rows or keys behind ------------------
        await factory.CleanupAsync(battleId, playerId);

        await redis.GetDatabase().KeyDeleteAsync(key);
    }

    /// <summary>
    /// A host composed exactly like the application's: REAL PostgreSQL and REAL
    /// Redis, with neither store substituted.
    /// </summary>
    private sealed class SmokeFactory : WebApplicationFactory<Program>
    {
        /// <summary>The Redis the smoke test speaks to (docker-compose, overridable).</summary>
        internal static readonly string RedisConnection =
            Environment.GetEnvironmentVariable("DCACTI_TEST_REDIS") ?? "127.0.0.1:6379";

        /// <summary>The PostgreSQL the smoke test speaks to (docker-compose, overridable).</summary>
        internal static readonly string PostgresConnection =
            Environment.GetEnvironmentVariable("DCACTI_TEST_POSTGRES")
            ?? "Host=localhost;Port=5433;Database=dcacti_db;Username=dcacti;Password=dcacti_dev_password";

        /// <summary>The Boss the walk fights — the canonical content-defined Hỏa Long.</summary>
        internal static BossDefinition BossUnderTest => BossDefinitions.HoaLong;

        internal string PetInstanceId { get; private set; } = string.Empty;

        internal string SessionToken { get; private set; } = string.Empty;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // NEITHER connection string is blanked: the production composition —
            // the Npgsql GameDbContext and the Redis BattleStateRepository — is
            // exactly what this smoke test exists to exercise.
            builder.UseSetting("ConnectionStrings:DefaultConnection", PostgresConnection);
            builder.UseSetting("ConnectionStrings:Redis", RedisConnection);

            // The application session's signing key, from configuration as
            // ADR-015 D10 requires, so the walk authenticates with a real session.
            foreach (var (key, value) in TestApplicationSession.CurrentKeyConfiguration)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureLogging(logging => logging.ClearProviders());
        }

        /// <summary>
        /// Opens a raw Redis connection to the same instance, so the assertions
        /// read the documented key directly rather than through the store under
        /// test.
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
                throw new InvalidOperationException(
                    $"TASK-041's smoke test requires a reachable Redis at "
                    + $"'{RedisConnection}' (docker compose up -d redis, or set "
                    + "DCACTI_TEST_REDIS). It verifies the documented "
                    + "battle:{battleId}:state lifecycle directly, so it cannot run "
                    + "without one.",
                    exception);
            }
        }

        /// <summary>
        /// Confirms PostgreSQL is reachable before the walk starts, so an
        /// unreachable database is reported as the environment limitation it is
        /// rather than surfacing later as a misleading endpoint failure.
        /// </summary>
        private static async Task RequirePostgresAsync()
        {
            try
            {
                await using var dataSource = new NpgsqlDataSourceBuilder(PostgresConnection).Build();

                await using var command = dataSource.CreateCommand("SELECT 1");
                await command.ExecuteScalarAsync();
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"TASK-041's smoke test requires a reachable PostgreSQL at "
                    + $"'{PostgresConnection}' (docker compose up -d postgres, or set "
                    + "DCACTI_TEST_POSTGRES), with the schema migrated "
                    + "(dotnet ef database update). It verifies the documented "
                    + "BattleResult row directly, so it cannot run without one.",
                    exception);
            }
        }

        /// <summary>
        /// A hub connection to this host's server, authenticated with the same
        /// application session REST uses (SIGNALR_PROTOCOL.md §1 item 3).
        /// </summary>
        public HubConnection BuildHubConnection() =>
            new HubConnectionBuilder()
                .WithUrl("http://localhost/hubs/battle", options =>
                {
                    options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
                    options.AccessTokenProvider = () => Task.FromResult<string?>(SessionToken);
                })
                .Build();

        /// <summary>
        /// Seeds the documented battle-start inputs against REAL PostgreSQL: one
        /// Player owning one Pet (with its definition and its Signature Skill
        /// Card), three unlocked Basic Cards, and three owned Relic instances.
        /// </summary>
        public async Task<string> SeedAsync()
        {
            await RequirePostgresAsync();

            using var scope = Services.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

            var playerId = $"player_smoke_result_{Guid.NewGuid():N}";
            PetInstanceId = $"{PetInstanceIdPrefix}_{Guid.NewGuid():N}";

            // The Pet definition is per-run too: the shared development database
            // outlives a test run, so a fixed id would collide with a previous
            // run's row. The cards it references are shared content and are reused
            // (see the Ensure* helpers below).
            var petDefinitionId = $"{PetDefinitionId}_{Guid.NewGuid():N}";

            context.Players.Add(new Player
            {
                PlayerId = playerId,
                DiscordUserId = $"8035{Random.Shared.NextInt64(1_000_000_000_000_000L):D16}",
                Level = Player.InitialLevel,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            // The battle-start inputs are static content the endpoint validates
            // against: the Pet's Signature Skill Card and the three Basic Cards.
            // They are keyed by fixed ids and the shared development database
            // outlives a test run, so an id already present is reused rather than
            // re-inserted — the walk must be repeatable without hand-cleaning the
            // database first.
            await EnsureCardDefinitionAsync(context, SignatureSkillCardId, CardCategory.PetSkill, copyLimit: 1);

            foreach (var id in new[] { HealCardId, ShieldCardId, PowerChargeCardId })
            {
                await EnsureCardDefinitionAsync(context, id, CardCategory.Basic, copyLimit: 3);
            }

            await EnsureRelicDefinitionAsync(context, "relic_def_smoke");

            context.PetDefinitions.Add(new PetDefinition
            {
                PetDefinitionId = petDefinitionId,
                Identity = "Thanh Xà",
                Element = Element.Moc,
                PassiveId = new PassiveId("thanh-xa-regen"),
                PassiveThreshold = 5,
                SignatureSkillCardId = SignatureSkillCardId,
            });

            context.Pets.Add(new Pet
            {
                PetInstanceId = PetInstanceId,
                PlayerId = playerId,
                PetDefinitionId = petDefinitionId,
                Tier = PetTier.Common,
                Star = 1,
                Level = 1,
                AcquiredAt = DateTimeOffset.UtcNow,
            });

            foreach (var id in new[] { HealCardId, ShieldCardId, PowerChargeCardId })
            {
                context.PlayerUnlockedCards.Add(new PlayerUnlockedCard
                {
                    PlayerId = playerId,
                    CardDefinitionId = id,
                });
            }

            foreach (var relicInstanceId in new[]
                     {
                         $"relic_a_{Guid.NewGuid():N}",
                         $"relic_b_{Guid.NewGuid():N}",
                         $"relic_c_{Guid.NewGuid():N}",
                     })
            {
                context.Relics.Add(new Relic
                {
                    RelicInstanceId = relicInstanceId,
                    PlayerId = playerId,
                    RelicDefinitionId = "relic_def_smoke",
                    AcquiredAt = DateTimeOffset.UtcNow,
                });

                OwnedRelicInstanceIds.Add(relicInstanceId);
            }

            await context.SaveChangesAsync();

            // A real application session for the Player just seeded — the same
            // token REST and the hub present.
            SessionToken = TestApplicationSession.Mint(playerId);

            return playerId;
        }

        /// <summary>
        /// Inserts a CardDefinition unless the shared database already holds it, so
        /// repeated runs do not collide on the primary key. Card content is not
        /// provisioned (<c>DATABASE.md</c> §5 item 4 keeps other content tables
        /// open), so the rows battle start validates against are supplied here.
        /// </summary>
        private static async Task EnsureCardDefinitionAsync(
            GameDbContext context,
            string cardDefinitionId,
            CardCategory category,
            int copyLimit)
        {
            if (await context.CardDefinitions.AnyAsync(card => card.CardDefinitionId == cardDefinitionId))
            {
                return;
            }

            context.CardDefinitions.Add(new CardDefinition
            {
                CardDefinitionId = cardDefinitionId,
                Name = cardDefinitionId,
                Category = category,
                PowerCost = 0,
                EffectDefinition = TestCardEffects.FlatPower,
                LoadoutCopyLimit = copyLimit,
            });
        }

        /// <summary>
        /// Seeds the scenario's <c>RelicDefinition</c> row — the row the owned Relic
        /// instances' foreign key needs, and the row the battle-start path now reads
        /// through <c>IRelicDefinitionLookup</c> to make each equipped Relic's
        /// declared content readable (<c>RELIC_RULES.md</c> §8;
        /// <c>GAME_RULES.md</c> §17 step 11).
        ///
        /// <b>The row is re-asserted on every run, not merely inserted once.</b> The
        /// shared development database outlives a test run, and a row an earlier run
        /// wrote predates TASK-132's structured <c>jsonb</c> columns: that migration
        /// converts a row it does not re-encode into a JSON <i>string</i>
        /// (<c>to_jsonb(prose)</c>), which the structured reader rejects loudly
        /// because <c>RELIC_RULES.md</c> §8.2 item 5 defines no prose compatibility
        /// reader. The content this fixture owns is therefore written to the
        /// documented structured shape every run, so the scenario does not depend on
        /// the database's history — and, because the row already exists after the
        /// first run, the write is an upsert rather than an insert.
        /// </summary>
        private static async Task EnsureRelicDefinitionAsync(
            GameDbContext context,
            string relicDefinitionId) =>
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "RelicDefinition"
                    ("RelicDefinitionId", "Name", "Trigger", "Condition", "EffectDefinition")
                VALUES
                    ({0}, {1}, {2}, {3}::jsonb, {4}::jsonb)
                ON CONFLICT ("RelicDefinitionId") DO UPDATE
                   SET "Name" = EXCLUDED."Name",
                       "Trigger" = EXCLUDED."Trigger",
                       "Condition" = EXCLUDED."Condition",
                       "EffectDefinition" = EXCLUDED."EffectDefinition";
                """,
                relicDefinitionId,
                "Smoke Relic",
                // A RELIC_RULES.md §3 Trigger identity — the value form §8.5 item 3
                // leaves as the prose §3 identity. OnMatchCount is one of the three
                // GAME_RULES.md §17 step 11 evaluates, so the scenario exercises the
                // step with real content present.
                "OnMatchCount",
                TestRelicEffects.Condition.ToPersistedPayload(),
                TestRelicEffects.Effect.ToPersistedPayload());

        /// <summary>The relic instance ids this run seeded, for the start request.</summary>
        internal List<string> OwnedRelicInstanceIds { get; } = [];

        /// <summary>
        /// Plays the battle through the real hub — committing real Swaps on the
        /// real board with the real content Boss — until a resolution reaches the
        /// documented terminal outcome (<c>GAME_RULES.md</c> §1.4).
        ///
        /// <b>Nothing is short-circuited.</b> The Boss is the canonical
        /// content-defined Hỏa Long with its real <c>BOSS_RULES.md</c> §6.1 stats,
        /// because <c>POST /api/battle/start</c> resolves it from transcribed
        /// content. So the walk plays until the Boss's 5000 HP is spent, exactly as
        /// a real player would, and the outcome observed is the resolution's own.
        ///
        /// The walk ends when the battle's active-state record is gone — the
        /// documented terminal side effect of <c>REDIS_STATE.md</c> §3, once the
        /// durable result has been written.
        /// </summary>
        public async Task<(int From, int To)> PlayToTerminalAsync(
            HubConnection hubConnection,
            ITestOutputHelper output)
        {
            using var scope = Services.CreateScope();

            var battles = scope.ServiceProvider.GetRequiredService<BattleStateService>();
            var action = 0;

            while (true)
            {
                var state = await battles.GetBattleAsync(_battleId!);

                // No record means the battle ended: the terminal path wrote the
                // durable result and then cleared the record (REDIS_STATE.md §3).
                if (state is null)
                {
                    output.WriteLine(
                        $"  battle ended after {action} committed Swaps "
                        + $"(active state cleared)");

                    return LastAcceptedPair;
                }

                action++;

                Assert.True(
                    action <= MaxActions,
                    $"The battle did not reach a terminal outcome within {MaxActions} committed Swaps.");

                var pair = FindMatchProducingPair(state.BoardState, state.LastCommittedSwapPair);

                var acknowledgement = await hubConnection.InvokeAsync<GameServer.Api.Hubs.SwapResponse>(
                    "Swap", _battleId, pair.From, pair.To, $"smoke-result-seq-{action}");

                if (acknowledgement.Accepted)
                {
                    LastAcceptedPair = pair;
                    CommittedSwapCount++;
                }
                else
                {
                    output.WriteLine($"  action {action}: rejected ({acknowledgement.Reason}) — retrying");

                    // A rejected action writes nothing (REDIS_STATE.md §4 item 7),
                    // so the retry re-reads the same board; the rejected pair is
                    // remembered so a different one is chosen.
                    RejectedPairs.Add((pair.From, pair.To));
                }
            }
        }

        /// <summary>How many committed Swaps the walk will attempt before giving up.</summary>
        private const int MaxActions = 400;

        private string? _battleId;

        /// <summary>The last pair the walk committed, for reporting.</summary>
        internal (int From, int To) LastAcceptedPair { get; private set; }

        /// <summary>How many committed Swaps the walk actually played.</summary>
        internal int CommittedSwapCount { get; private set; }

        /// <summary>Pairs the server rejected, so the walk does not repeat them.</summary>
        private readonly HashSet<(int From, int To)> RejectedPairs = [];

        /// <summary>The battle the walk is playing.</summary>
        internal void Track(string battleId) => _battleId = battleId;

        /// <summary>
        /// An adjacent pair the production validator accepts and the server has not
        /// already rejected — chosen from the battle's real board so the walk
        /// exercises the real resolution rather than a hand-picked pair.
        /// </summary>
        private (int From, int To) FindMatchProducingPair(
            BoardState board,
            CommittedSwapPair? lastCommitted)
        {
            foreach (var index in Enumerable.Range(0, BoardState.CellCount))
            {
                foreach (var adjacent in Adjacent(index))
                {
                    // The documented staleness check (MATCH3_RULES.md §2.1.4) rejects
                    // an immediate replay, so the walk skips the pair it just played
                    // and any pair the server has already refused.
                    if (RejectedPairs.Contains((index, adjacent)))
                    {
                        continue;
                    }

                    var validation = SwapValidator.Validate(
                        board,
                        lastCommitted,
                        new SwapRequest(index, adjacent));

                    if (validation.IsAccepted)
                    {
                        var canonical = (Math.Min(index, adjacent), Math.Max(index, adjacent));

                        if (!RejectedPairs.Contains(canonical))
                        {
                            return (index, adjacent);
                        }
                    }
                }
            }

            throw new InvalidOperationException("The board accepted no adjacent Swap.");

            static IEnumerable<int> Adjacent(int index)
            {
                var column = BoardState.ToColumn(index);

                if (column + 1 < BoardState.Columns)
                {
                    yield return index + 1;
                }

                if (index + BoardState.Width < BoardState.CellCount)
                {
                    yield return index + BoardState.Width;
                }
            }
        }

        /// <summary>
        /// Reads the documented row straight out of REAL PostgreSQL with SQL, so
        /// the assertion is on what the database holds rather than on a value the
        /// application layer returned.
        /// </summary>
        public async Task<StoredResult?> ReadResultRowAsync(string battleId)
        {
            await using var context = Services.CreateScope().ServiceProvider
                .GetRequiredService<GameDbContext>();

            return await context.Database
                .SqlQuery<StoredResult>($"""
                    SELECT "BattleResultId", "PlayerId", "PetInstanceId",
                           "BossDefinitionId", "Outcome", "DurationTurns",
                           "CompletedAt", "RewardSummary"
                    FROM "BattleResult"
                    WHERE "BattleResultId" = {battleId}
                    """)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Removes the rows this run created, so the shared development database is
        /// left as it was found. The FK chain is unwound in dependency order:
        /// BattleResult first (it references the others), then the owned instances,
        /// then the definitions they reference, then the Player.
        /// </summary>
        public async Task CleanupAsync(string battleId, string playerId)
        {
            await using var context = Services.CreateScope().ServiceProvider
                .GetRequiredService<GameDbContext>();

            // BattleResult references Player, Pet, and BossDefinition (§1, §2), so
            // it goes first.
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"BattleResult\" WHERE \"BattleResultId\" = {0}", battleId);

            foreach (var relicId in OwnedRelicInstanceIds.ToArray())
            {
                await context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM \"Relic\" WHERE \"RelicInstanceId\" = {0}", relicId);
            }

            // Pet references PetDefinition and Player; PetDefinition references
            // CardDefinition (DATABASE.md §2), so the owned instance goes before
            // either definition. The definition delete is additionally guarded on
            // having no remaining Pet: the development database may hold a row from
            // an earlier run, and deleting a referenced definition must not fail the
            // test that just verified the contract.
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"Pet\" WHERE \"PetInstanceId\" = {0}", PetInstanceId);

            await context.Database.ExecuteSqlRawAsync(
                """
                DELETE FROM "PetDefinition"
                WHERE "PetDefinitionId" LIKE {0}
                  AND NOT EXISTS (
                      SELECT 1 FROM "Pet" WHERE "PetDefinitionId" = "PetDefinition"."PetDefinitionId")
                """,
                $"{PetDefinitionId}_%");

            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"PlayerUnlockedCard\" WHERE \"PlayerId\" = {0}", playerId);

            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"Player\" WHERE \"PlayerId\" = {0}", playerId);

            // The shared content rows go last, and only when nothing else
            // references them: the development database may hold rows another run
            // left (or still needs), so a card that is still referenced is kept
            // rather than deleted out from under it.
            await context.Database.ExecuteSqlRawAsync(
                """
                DELETE FROM "RelicDefinition"
                WHERE "RelicDefinitionId" = 'relic_def_smoke'
                  AND NOT EXISTS (SELECT 1 FROM "Relic" WHERE "RelicDefinitionId" = 'relic_def_smoke')
                """);

            foreach (var cardId in new[] { HealCardId, ShieldCardId, PowerChargeCardId, SignatureSkillCardId })
            {
                await context.Database.ExecuteSqlRawAsync(
                    """
                    DELETE FROM "CardDefinition"
                    WHERE "CardDefinitionId" = {0}
                      AND NOT EXISTS (
                          SELECT 1 FROM "PetDefinition" WHERE "SignatureSkillCardId" = {0})
                      AND NOT EXISTS (
                          SELECT 1 FROM "PlayerUnlockedCard" WHERE "CardDefinitionId" = {0})
                    """,
                    cardId);
            }
        }

        /// <summary>The documented <c>DATABASE.md</c> §1 row, read by SQL.</summary>
        public sealed record StoredResult(
            string BattleResultId,
            string PlayerId,
            string PetInstanceId,
            string BossDefinitionId,
            string Outcome,
            int DurationTurns,
            DateTimeOffset CompletedAt,
            string RewardSummary);
    }
}
