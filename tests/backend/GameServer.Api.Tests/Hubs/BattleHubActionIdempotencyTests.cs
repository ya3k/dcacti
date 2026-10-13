using System.Net.Http.Headers;
using System.Text.Json;
using GameServer.Api.Hubs;
using GameServer.Application.Battle;
using GameServer.Application.Runtime;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GameServer.Api.Tests;

/// <summary>
/// Verifies P0.2 requirements for action idempotency, submission settlement,
/// and post-commit recovery coordination (ADR-025, ADR-026, SIGNALR_PROTOCOL.md §2.1, §5).
/// </summary>
public sealed class BattleHubActionIdempotencyTests : IClassFixture<ApiIntegrationTests.ApiIntegrationFactory>
{
    private readonly ApiIntegrationTests.ApiIntegrationFactory _factory;

    private static readonly PlayerId Owner = new("player_idempotency_api_test");
    private static readonly PlayerId Peer = new("player_idempotency_peer_test");

    private static string OwnerToken => TestApplicationSession.Mint(Owner.Value);
    private static string PeerToken => TestApplicationSession.Mint(Peer.Value);

    private static readonly BattleStateService.PetConfiguration PetConfig = new(
        new PetId("pet_instance_idempotency_1"),
        Element.Hoa,
        new PassiveId("xich-lang"),
        PassiveThreshold: 5,
        EquippedCards:
        [
            new EquippedCardIdentity("card-heal"),
            new EquippedCardIdentity("card-shield"),
            new EquippedCardIdentity("card-power-charge"),
            new EquippedCardIdentity("card-inferno"),
        ]);

    public BattleHubActionIdempotencyTests(ApiIntegrationTests.ApiIntegrationFactory factory)
    {
        _factory = factory;
    }

    private void EnsureSeeded() => TestProvisionedContent.Seed(_factory.Services);

    private sealed class BearerMessageHandler : DelegatingHandler
    {
        private readonly string _token;

        public BearerMessageHandler(HttpMessageHandler inner, string token)
            : base(inner)
        {
            _token = token;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            return base.SendAsync(request, cancellationToken);
        }
    }

    private HubConnection BuildHubConnection(string token)
    {
        EnsureSeeded();
        return new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/battle", options =>
            {
                options.HttpMessageHandlerFactory = _ =>
                    new BearerMessageHandler(_factory.Server.CreateHandler(), token);

                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
    }

    private async Task<SwapRequest> CreateBattleWithValidPairAsync(string battleId, PlayerId owner)
    {
        EnsureSeeded();

        using var scope = _factory.Services.CreateScope();
        var battleService = scope.ServiceProvider.GetRequiredService<BattleStateService>();

        var state = await battleService.CreateBattleAsync(
            battleId, owner, PetConfig, BossDefinitions.HoaLong);

        for (var index = 0; index < BoardState.CellCount; index++)
        {
            var right = BoardState.ToColumn(index) + 1 < BoardState.Columns ? index + 1 : -1;
            var down = index + BoardState.Width < BoardState.CellCount ? index + BoardState.Width : -1;

            foreach (var to in new[] { right, down })
            {
                if (to < 0) continue;

                if (MatchDetector.Detect(state.BoardState.WithSwapped(index, to)).Count > 0)
                {
                    return new SwapRequest(index, to);
                }
            }
        }

        throw new InvalidOperationException("A generated board has at least one valid Swap.");
    }

    [Fact]
    public async Task DuplicateSubmission_CommitsAtMostOnce_AndReturnsConsistentResult()
    {
        const string battleId = "battle-p02-dup-once";
        var pair = await CreateBattleWithValidPairAsync(battleId, Owner);

        var hubConnection = BuildHubConnection(OwnerToken);
        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        // First submission with explicit actionId token
        var first = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "0:act-p02-dup-test");
        Assert.True(first.Accepted);

        using (var scope = _factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IBattleStateRepository>();
            var state = await repo.GetAsync(battleId);
            Assert.NotNull(state);
            Assert.Equal(1, state.Sequence);
            Assert.Equal("act-p02-dup-test", state.LastCommittedSwapPair?.ActionId);
        }

        // Duplicate submission with same action token
        var retry = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "0:act-p02-dup-test");
        Assert.True(retry.Accepted);
        Assert.Null(retry.Reason);

        using (var scope = _factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IBattleStateRepository>();
            var state = await repo.GetAsync(battleId);
            Assert.NotNull(state);
            // Sequence remains 1 — not incremented again!
            Assert.Equal(1, state.Sequence);
        }

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task IdempotentReplay_DeliversStateToCaller_WithoutDuplicatingEventBatch()
    {
        const string battleId = "battle-p02-replay-delivery";
        var pair = await CreateBattleWithValidPairAsync(battleId, Owner);

        var hubConnection = BuildHubConnection(OwnerToken);
        var eventBatches = 0;
        var statePushes = 0;

        hubConnection.On<JsonElement>("ReceiveEvents", _ => Interlocked.Increment(ref eventBatches));
        hubConnection.On<JsonElement>("BattleStateUpdated", _ => Interlocked.Increment(ref statePushes));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        // Join push
        await Task.Delay(100);
        var initialPushes = Volatile.Read(ref statePushes);

        // 1. Newly committed action
        var first = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "0:act-replay-deliver");
        Assert.True(first.Accepted);

        await Task.Delay(200);
        Assert.Equal(1, Volatile.Read(ref eventBatches));
        Assert.Equal(initialPushes + 1, Volatile.Read(ref statePushes));

        // 2. Idempotent replay of same action
        var retry = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "0:act-replay-deliver");
        Assert.True(retry.Accepted);
        Assert.Null(retry.Reason);

        await Task.Delay(200);
        // Events are NOT duplicated!
        Assert.Equal(1, Volatile.Read(ref eventBatches));
        // Authoritative state IS delivered to the retrying caller so it can recover!
        Assert.Equal(initialPushes + 2, Volatile.Read(ref statePushes));

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task StaleAction_SequenceMismatch_RejectsAsStaleActionWithoutModifyingState()
    {
        const string battleId = "battle-p02-stale-seq";
        var pair = await CreateBattleWithValidPairAsync(battleId, Owner);

        var hubConnection = BuildHubConnection(OwnerToken);
        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        // Mismatched expected sequence 99 vs actual 0
        var stale = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "99:act-stale-1");

        Assert.False(stale.Accepted);
        Assert.Equal("STALE_ACTION", stale.Reason);

        using (var scope = _factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IBattleStateRepository>();
            var state = await repo.GetAsync(battleId);
            Assert.NotNull(state);
            Assert.Equal(0, state.Sequence);
            Assert.Null(state.LastCommittedSwapPair);
        }

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task PvP_NewCommittedAction_DeliversToGroup_WhileReplayDeliversToCallerOnly()
    {
        const string battleId = "battle-p02-pvp-group-semantics";
        var pair = await CreateBattleWithValidPairAsync(battleId, Owner);

        var ownerConn = BuildHubConnection(OwnerToken);
        var peerConn = BuildHubConnection(PeerToken);

        var ownerPushes = 0;
        var ownerEvents = 0;
        var peerPushes = 0;
        var peerEvents = 0;

        ownerConn.On<JsonElement>("BattleStateUpdated", _ => Interlocked.Increment(ref ownerPushes));
        ownerConn.On<JsonElement>("ReceiveEvents", _ => Interlocked.Increment(ref ownerEvents));

        peerConn.On<JsonElement>("BattleStateUpdated", _ => Interlocked.Increment(ref peerPushes));
        peerConn.On<JsonElement>("ReceiveEvents", _ => Interlocked.Increment(ref peerEvents));

        await ownerConn.StartAsync();
        await peerConn.StartAsync();

        await ownerConn.InvokeAsync("JoinBattle", battleId);
        await peerConn.InvokeAsync("JoinBattle", battleId);

        await Task.Delay(200);
        var ownerInitialPushes = Volatile.Read(ref ownerPushes);
        var peerInitialPushes = Volatile.Read(ref peerPushes);

        // 1. Newly committed action: group broadcast (both receive)
        var first = await ownerConn.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "0:act-pvp-test");
        Assert.True(first.Accepted);

        await Task.Delay(200);
        Assert.Equal(1, Volatile.Read(ref ownerEvents));
        Assert.Equal(1, Volatile.Read(ref peerEvents));
        Assert.Equal(ownerInitialPushes + 1, Volatile.Read(ref ownerPushes));
        Assert.Equal(peerInitialPushes + 1, Volatile.Read(ref peerPushes));

        // 2. Idempotent replay: retried by Owner only
        var retry = await ownerConn.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "0:act-pvp-test");
        Assert.True(retry.Accepted);

        await Task.Delay(200);
        // Owner receives updated state; no new events
        Assert.Equal(ownerInitialPushes + 2, Volatile.Read(ref ownerPushes));
        Assert.Equal(1, Volatile.Read(ref ownerEvents));

        // Peer does NOT receive duplicate presentation or state push
        Assert.Equal(peerInitialPushes + 1, Volatile.Read(ref peerPushes));
        Assert.Equal(1, Volatile.Read(ref peerEvents));

        await ownerConn.StopAsync();
        await peerConn.StopAsync();
    }

    [Fact]
    public async Task PostCommitNotificationFailure_ReturnsAcceptedTrue_AndDoesNotReexecute()
    {
        const string battleId = "battle-p02-post-commit-fail";
        var pair = await CreateBattleWithValidPairAsync(battleId, Owner);

        using var scope = _factory.Services.CreateScope();
        var battles = scope.ServiceProvider.GetRequiredService<BattleStateService>();
        var runtime = scope.ServiceProvider.GetRequiredService<RuntimeService>();
        var repo = scope.ServiceProvider.GetRequiredService<IBattleStateRepository>();
        var logger = new TestLogger<BattleHub>();

        var testClients = new TestHubCallerClients();
        // Configure group broadcast to throw, simulating transport / broadcast failure
        testClients.GroupProxy.OnSend = (_, _) => throw new InvalidOperationException("SignalR network drop");

        var hub = new BattleHub(runtime, battles, logger)
        {
            Clients = testClients,
        };

        // Execution of swap
        var response = await hub.Swap(battleId, pair.From, pair.To, "0:act-post-commit-fail");

        // The action committed to Redis, so the response accurately reports Accepted: true
        Assert.True(response.Accepted);
        Assert.Null(response.Reason);

        // Verification: action was indeed committed in repository
        var state = await repo.GetAsync(battleId);
        Assert.NotNull(state);
        Assert.Equal(1, state.Sequence);
        Assert.Equal("act-post-commit-fail", state.LastCommittedSwapPair?.ActionId);

        // Verification: notification failure was logged as an Error with context
        Assert.Contains(logger.Logs, l =>
            l.Level == LogLevel.Error
            && l.Message.Contains("Failed to broadcast post-commit notifications")
            && l.Message.Contains(battleId)
            && l.Message.Contains("act-post-commit-fail"));

        // Retrying with the same actionId returns idempotent replay without re-executing
        var retryResponse = await hub.Swap(battleId, pair.From, pair.To, "0:act-post-commit-fail");
        Assert.True(retryResponse.Accepted);
        Assert.Null(retryResponse.Reason);

        var stateAfterRetry = await repo.GetAsync(battleId);
        Assert.NotNull(stateAfterRetry);
        Assert.Equal(1, stateAfterRetry.Sequence);
    }

    private sealed class TestClientProxy : IClientProxy
    {
        public List<(string Method, object?[] Args)> Invocations { get; } = new();
        public Func<string, object?[], Task>? OnSend { get; set; }

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            Invocations.Add((method, args));
            if (OnSend is not null)
            {
                return OnSend(method, args);
            }
            return Task.CompletedTask;
        }
    }

    private sealed class TestHubCallerClients : IHubCallerClients
    {
        public TestClientProxy CallerProxy { get; } = new();
        public TestClientProxy GroupProxy { get; } = new();

        public IClientProxy Caller => CallerProxy;
        public IClientProxy Group(string groupName) => GroupProxy;
        public IClientProxy All => GroupProxy;
        public IClientProxy Others => GroupProxy;
        public IClientProxy Client(string connectionId) => GroupProxy;
        public IClientProxy User(string userId) => GroupProxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => GroupProxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => GroupProxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => GroupProxy;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => GroupProxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => GroupProxy;
        public IClientProxy OthersInGroup(string groupName) => GroupProxy;
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Logs { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Logs.Add((logLevel, formatter(state, exception), exception));
        }
    }
}
