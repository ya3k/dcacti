using System.Text.Json;
using GameServer.Api.Hubs;
using GameServer.Application.Battle;
using GameServer.Domain.Battle.Serialization;
using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Infrastructure.Postgres;
using Microsoft.AspNetCore.Hosting;
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
/// The TASK-143 end-to-end recovery test: <c>GetBattleState(battleId)</c> against a
/// REAL Redis (<c>SIGNALR_PROTOCOL.md</c> §7, <c>ADR-008</c>,
/// <c>REDIS_STATE.md</c> §1–§3).
///
/// <code>
/// authenticated session
///         ↓
/// BattleHub.GetBattleState(battleId)          §7.1 — the recovery request
///         ↓
/// BattleStateService                          §2.3 — ownership, server-side
///         ↓
/// BattleStateRepository (Redis)               the production store
///         ↓
/// battle:{battleId}:state                     REDIS_STATE.md §1 — asserted directly
///         ↓
/// authoritative BattleState
///         ↓
/// §4 wire projection
///         ↓
/// caller
/// </code>
///
/// <b>Why this is not a unit test.</b> Every other recovery suite substitutes the
/// store with an in-memory double so it can assert the hub's contract. This one
/// runs the production composition — the real <c>BattleStateService</c>, the real
/// <c>GameServer.Infrastructure.Redis.BattleStateRepository</c>, and a real Redis
/// connection — and reads the documented key with a raw Redis client. So what is
/// verified is the record that actually exists in Redis, and the recovery path is
/// proven to read <b>that</b> record rather than a value the test itself produced.
///
/// <b>Expiry is exercised for real, without redefining the TTL.</b> §3's sliding
/// expiry is set by production at 30 minutes. To observe the documented
/// "expired/cleared → <c>BATTLE_NOT_FOUND</c>" outcome the test shortens the TTL
/// <i>of its own test key</i> through the Redis client and waits for it to elapse,
/// exactly as the Redis store suite does — production's documented TTL is never
/// changed, and no application code is told about the shortened value.
///
/// <b>Environment.</b> It requires a reachable Redis (the <c>docker-compose.yml</c>
/// instance on 6379 by default; override with <c>DCACTI_TEST_REDIS</c>). Without
/// one the test FAILS with the remedy stated rather than passing vacuously: a green
/// run has to mean the contract was exercised. PostgreSQL is substituted with an
/// in-memory store, because this test's subject is Redis.
/// </summary>
public class RedisBattleRecoverySmokeTest
{
    private readonly ITestOutputHelper _output;

    public RedisBattleRecoverySmokeTest(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// The documented recovery path end to end: the owner recovers the authoritative
    /// snapshot from the real <c>battle:{battleId}:state</c> record, at its real
    /// <c>Sequence</c>, with no <c>PlayerId</c> on the wire; a foreign Player is
    /// refused; and once the record's TTL elapses the documented
    /// <c>BATTLE_NOT_FOUND</c> is returned.
    /// </summary>
    [Fact]
    public async Task GetBattleState_ShouldRecoverFromTheRealRedisRecord_AndRejectWhenItExpires()
    {
        using var factory = new RecoverySmokeFactory();
        using var redis = await factory.ConnectToRedisAsync();

        _output.WriteLine($"Redis: {RecoverySmokeFactory.RedisConnection}");
        _output.WriteLine($"PING: {await redis.GetDatabase().PingAsync()}");

        var ownerId = $"player_recovery_smoke_owner_{Guid.NewGuid():N}";
        var foreignId = $"player_recovery_smoke_foreign_{Guid.NewGuid():N}";

        var ownerToken = TestApplicationSession.Mint(ownerId);
        var foreignToken = TestApplicationSession.Mint(foreignId);

        var battleId = $"battle-recovery-smoke-{Guid.NewGuid():N}";

        // Create the battle through the Application boundary, so the record is
        // written by the production store (REDIS_STATE.md §3 "Created").
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<BattleStateService>().CreateBattleAsync(
                battleId,
                new PlayerId(ownerId),
                new BattleStateService.PetConfiguration(
                    new PetId("pet_recovery_smoke"),
                    Element.Hoa,
                    new PassiveId("xich-lang"),
                    PassiveThreshold: 5,
                    EquippedCards:
                    [
                        new EquippedCardIdentity("card-heal"),
                        new EquippedCardIdentity("card-shield"),
                        new EquippedCardIdentity("card-power-charge"),
                        new EquippedCardIdentity("card-inferno"),
                    ]),
                BossDefinitions.HoaLong);
        }

        var database = redis.GetDatabase();
        var key = $"battle:{battleId}:state";

        // ---- The documented key exists, and it is the ONLY one ---------------
        // REDIS_STATE.md §1 fixes exactly one state key per battle; recovery must
        // not have introduced a second one.
        Assert.True(await database.KeyExistsAsync(key), $"REDIS_STATE.md §1: '{key}' must exist");

        var keys = new List<string>();
        await foreach (var found in redis.GetServer(redis.GetEndPoints().First())
                           .KeysAsync(pattern: $"battle:{battleId}:*"))
        {
            keys.Add(found.ToString());
        }

        keys.Sort(StringComparer.Ordinal);
        Assert.Equal([key], keys);
        _output.WriteLine($"KEYS battle:{battleId}:* = [{string.Join(", ", keys)}]");

        // ===================================================================
        // 1. The owning Player recovers the authoritative snapshot
        // ===================================================================
        var ownerConnection = factory.BuildHubConnection(ownerToken);
        await ownerConnection.StartAsync();

        var recovered = await ownerConnection.InvokeAsync<GetBattleStateResponse>(
            "GetBattleState", battleId);

        Assert.True(recovered.Accepted, "the owning Player must recover its own battle");
        Assert.Null(recovered.Reason);
        Assert.NotNull(recovered.State);

        // ---- It IS the record Redis holds, not a re-derived value -----------
        // §7.1: the snapshot is the current authoritative BattleState plus its
        // Sequence, read from the active-state record.
        var storedRaw = (await database.StringGetAsync(key)).ToString();
        var stored = BattleStateSerializer.Deserialize(storedRaw);

        Assert.Equal(stored.BattleId, recovered.State!.BattleId);
        Assert.Equal(stored.Sequence, recovered.ServerSequence);
        Assert.Equal(stored.Sequence, recovered.State.Sequence);
        Assert.Equal(stored.Turn, recovered.State.Turn);
        // GAME_STATE.md §2.2 / ADR-011 item 6: Combo and MatchCount are BattleState
        // ROOT members — there is no PlayerState node, and `playerState` is only the
        // fixed protocol label for their projection.
        Assert.Equal(stored.Combo, recovered.State.PlayerState.Combo);
        Assert.Equal(stored.MatchCount, recovered.State.PlayerState.MatchCount);

        _output.WriteLine(
            $"GetBattleState accepted: battleId={recovered.State.BattleId} "
            + $"sequence={recovered.ServerSequence} turn={recovered.State.Turn}");

        // ---- The board is the 64-cell authoritative board -------------------
        Assert.Equal(64, recovered.State.Board.Cells.Count);

        // ---- §4.3 item 13: equippedCards survives recovery ------------------
        Assert.Equal(
            new[] { "card-heal", "card-shield", "card-power-charge", "card-inferno" },
            recovered.State.PetState.EquippedCards);

        // ---- GAME_STATE.md §2.8 item 3 / ADR-014 decision 3: PlayerId is not
        // ---- on the wire, even though the RECORD carries it ---------------
        // The stored record does hold `playerId` — that is what ownership is
        // checked against — while the recovered payload must not.
        using (var document = JsonDocument.Parse(storedRaw))
        {
            Assert.True(
                document.RootElement.TryGetProperty("playerId", out _),
                "the authoritative record carries playerId (§2.8 item 2)");
        }

        var wire = SignalRWireJson.ToWireText(recovered);

        foreach (var forbidden in new[] { "playerId", "discordUserId", "userId", "ownerId" })
        {
            Assert.DoesNotContain(forbidden, wire, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(ownerId, wire, StringComparison.Ordinal);

        _output.WriteLine("Wire payload carries no playerId; the record does.");

        await ownerConnection.StopAsync();

        // ===================================================================
        // 2. A different authenticated Player is refused
        // ===================================================================
        // §2.3 / API_CONTRACTS.md §4 note 7: ownership is enforced server-side, and
        // a battle the caller does not own is the same answer as one that does not
        // exist.
        var foreignConnection = factory.BuildHubConnection(foreignToken);
        await foreignConnection.StartAsync();

        var foreign = await foreignConnection.InvokeAsync<GetBattleStateResponse>(
            "GetBattleState", battleId);

        Assert.False(foreign.Accepted, "a foreign Player must not recover another Player's battle");
        Assert.Equal("BATTLE_NOT_FOUND", foreign.Reason);
        Assert.Null(foreign.State);
        Assert.Null(foreign.ServerSequence);

        var unknown = await foreignConnection.InvokeAsync<GetBattleStateResponse>(
            "GetBattleState", $"battle-recovery-smoke-absent-{Guid.NewGuid():N}");

        Assert.Equal(SignalRWireJson.ToWireText(unknown), SignalRWireJson.ToWireText(foreign));

        _output.WriteLine("Foreign rejection is identical to the unknown-battle rejection.");

        await foreignConnection.StopAsync();

        // ===================================================================
        // 3. The record expires → the documented BATTLE_NOT_FOUND
        // ===================================================================
        // REDIS_STATE.md §3: the sliding TTL is the documented lifetime. Collapsing
        // THIS TEST KEY's expiry is how the outcome becomes observable; production's
        // 30-minute TTL is not modified, and no application code is told.
        await database.KeyExpireAsync(key, TimeSpan.FromSeconds(2));

        var collapsed = await database.KeyTimeToLiveAsync(key);
        Assert.NotNull(collapsed);
        _output.WriteLine($"TTL collapsed to {collapsed} to observe the documented expiry");

        await Task.Delay(TimeSpan.FromSeconds(3));

        Assert.False(await database.KeyExistsAsync(key), "the record must have expired");

        var ownerAfterExpiry = factory.BuildHubConnection(ownerToken);
        await ownerAfterExpiry.StartAsync();

        var expired = await ownerAfterExpiry.InvokeAsync<GetBattleStateResponse>(
            "GetBattleState", battleId);

        // §7.3 / REDIS_STATE.md §3: the documented recovery failure, and the same
        // one a foreign caller receives — an expired key discloses nothing beyond
        // "not recoverable", and no Redis detail is exposed.
        Assert.False(expired.Accepted);
        Assert.Equal("BATTLE_NOT_FOUND", expired.Reason);
        Assert.Null(expired.State);

        _output.WriteLine("Expired record → BATTLE_NOT_FOUND, as documented.");

        await ownerAfterExpiry.StopAsync();

        // ---- Cleanup: leave no smoke record behind --------------------------
        await database.KeyDeleteAsync(key);
    }

    /// <summary>
    /// A host composed exactly like the application's, except that Redis is REAL
    /// and PostgreSQL is an isolated in-memory store.
    /// </summary>
    private sealed class RecoverySmokeFactory : WebApplicationFactory<Program>
    {
        /// <summary>
        /// The Redis this smoke test speaks to — the local development instance
        /// (<c>docker-compose.yml</c>, <c>.env.example</c>), overridable.
        /// </summary>
        internal static readonly string RedisConnection =
            Environment.GetEnvironmentVariable("DCACTI_TEST_REDIS") ?? "127.0.0.1:6379";

        private readonly string _storeName = $"recovery-smoke-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // PostgreSQL is substituted; Redis is NOT — the real Redis composition
            // is what this test exists to exercise.
            builder.UseSetting("ConnectionStrings:DefaultConnection", "");
            builder.UseSetting("ConnectionStrings:Redis", RedisConnection);

            // The application session's signing key, from configuration as
            // ADR-015 D10 requires, so both Players authenticate with real tokens.
            foreach (var (key, value) in TestApplicationSession.CurrentKeyConfiguration)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<GameDbContext>>();
                services.RemoveAll<GameDbContext>();
                services.AddDbContext<GameDbContext>(options =>
                    options.UseInMemoryDatabase(_storeName));
            });
        }

        /// <summary>
        /// Opens a client connection to the same Redis, so the assertions read the
        /// documented key directly rather than through the store under test.
        /// </summary>
        internal async Task<IConnectionMultiplexer> ConnectToRedisAsync()
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
                // Reported as a failure, not a pass: without Redis this smoke test
                // has verified nothing.
                throw new InvalidOperationException(
                    $"TASK-143's recovery smoke test requires a reachable Redis at "
                    + $"'{RedisConnection}' (docker compose up -d redis, or set "
                    + "DCACTI_TEST_REDIS). It verifies recovery against the documented "
                    + "battle:{battleId}:state record directly, so it cannot run "
                    + "without one.",
                    exception);
            }
        }

        /// <summary>
        /// A hub connection to this host's server, presenting
        /// <paramref name="sessionToken"/> through SignalR's standard access-token
        /// mechanism (<c>SIGNALR_PROTOCOL.md</c> §1 item 3, <c>ADR-015</c> D4).
        /// </summary>
        internal HubConnection BuildHubConnection(string sessionToken) =>
            new HubConnectionBuilder()
                .WithUrl("http://localhost/hubs/battle", options =>
                {
                    options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
                    options.AccessTokenProvider = () => Task.FromResult<string?>(sessionToken);
                })
                .Build();
    }
}
