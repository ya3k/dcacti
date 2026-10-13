using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;
using GameServer.Api.Hubs;
using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GameServer.Api.Tests;

/// <summary>
/// Verifies the wire contract every committed action must preserve
/// (<c>SIGNALR_PROTOCOL.md</c> §3.1, §6 item 4, <c>GAME_STATE.md</c> §5):
///
/// <list type="number">
/// <item>the resolution's <c>BattleStateUpdated</c> push is delivered
/// <b>before</b> its <c>ReceiveEvents</c> batch, and</item>
/// <item>the two payloads carry the <b>same</b> committed
/// <c>BattleState.Sequence</c> — the push's <c>sequence</c> and the batch's
/// <c>serverSequence</c>.</item>
/// </list>
///
/// These are producer-side invariants, not client behavior: the client's
/// desynchronization detection (<c>TASK-273</c>) compares a batch's
/// <c>serverSequence</c> with the <c>sequence</c> of the state it holds, so a
/// producer that inverted the order or reported two different versions would make
/// every committed resolution look like a delivery-integrity gap. This file is the
/// regression guard for that contract and changes no production behavior.
///
/// All three documented gameplay methods are covered — <c>Swap</c>,
/// <c>CardCast</c> and <c>PetSkillCast</c> (<c>SIGNALR_PROTOCOL.md</c> §2) —
/// because each is its own call site with its own pair of broadcasts.
/// </summary>
public sealed class BattleHubStatePushOrderTests : IClassFixture<ApiIntegrationTests.ApiIntegrationFactory>
{
    private readonly ApiIntegrationTests.ApiIntegrationFactory _factory;

    private static readonly PlayerId Owner = new("player_push_order_api_test");

    private static string OwnerToken => TestApplicationSession.Mint(Owner.Value);

    private static readonly BattleStateService.PetConfiguration PetConfig = new(
        new PetId("pet_instance_push_order_1"),
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

    public BattleHubStatePushOrderTests(ApiIntegrationTests.ApiIntegrationFactory factory)
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

    private HubConnection BuildHubConnection()
    {
        EnsureSeeded();
        return new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/battle", options =>
            {
                options.HttpMessageHandlerFactory = _ =>
                    new BearerMessageHandler(_factory.Server.CreateHandler(), OwnerToken);

                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(OwnerToken);
            })
            .Build();
    }

    /// <summary>
    /// Records the two battle-scoped broadcasts of one committed action in the
    /// order the connection observed them, with the version each carried.
    /// </summary>
    private sealed class PushOrderRecorder
    {
        private readonly ConcurrentQueue<string> _arrivals = new();
        private readonly TaskCompletionSource _bothArrived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int StateSequence { get; private set; } = -1;

        public int BatchSequence { get; private set; } = -1;

        public IReadOnlyList<string> Arrivals => _arrivals.ToArray();

        public Task BothArrived => _bothArrived.Task;

        public void OnStatePush(JsonElement payload)
        {
            StateSequence = payload.GetProperty("sequence").GetInt32();
            _arrivals.Enqueue("BattleStateUpdated");
            SignalIfComplete();
        }

        public void OnReceiveEvents(JsonElement payload)
        {
            BatchSequence = payload.GetProperty("serverSequence").GetInt32();
            _arrivals.Enqueue("ReceiveEvents");
            SignalIfComplete();
        }

        private void SignalIfComplete()
        {
            if (StateSequence >= 0 && BatchSequence >= 0)
            {
                _bothArrived.TrySetResult();
            }
        }
    }

    /// <summary>
    /// Starts the connection and joins the battle, discarding the join push, so
    /// only the committed action's own two broadcasts are observed afterwards.
    /// </summary>
    private static async Task JoinAndConsumeInitialPushAsync(
        HubConnection hubConnection,
        string battleId)
    {
        var joinPushes = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<JsonElement>("BattleStateUpdated", payload => joinPushes.TrySetResult(payload));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        await joinPushes.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private async Task<SwapRequest> FindValidPairAsync(string battleId)
    {
        EnsureSeeded();

        using var scope = _factory.Services.CreateScope();
        var battleService = scope.ServiceProvider.GetRequiredService<BattleStateService>();

        var state = await battleService.CreateBattleAsync(battleId, Owner, PetConfig, BossDefinitions.HoaLong);

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

    private async Task CreateBattleWithPowerAsync(string battleId, int power)
    {
        EnsureSeeded();

        using var scope = _factory.Services.CreateScope();
        var battleService = scope.ServiceProvider.GetRequiredService<BattleStateService>();
        var repo = scope.ServiceProvider.GetRequiredService<IBattleStateRepository>();

        var state = await battleService.CreateBattleAsync(battleId, Owner, PetConfig, BossDefinitions.HoaLong);
        var modified = state with { PetState = state.PetState with { HP = 500, MaxHP = 1000, Power = power } };

        await repo.TryUpdateAsync(modified, state.Sequence);
    }

    [Fact]
    public async Task Swap_PushesBattleStateUpdatedBeforeReceiveEvents_WithTheSameCommittedSequence()
    {
        var battleId = $"battle-order-swap-{Guid.NewGuid():N}";
        var swap = await FindValidPairAsync(battleId);


        var hubConnection = BuildHubConnection();
        await JoinAndConsumeInitialPushAsync(hubConnection, battleId);
        var recorder = new PushOrderRecorder();
        hubConnection.On<JsonElement>("BattleStateUpdated", recorder.OnStatePush);
        hubConnection.On<JsonElement>("ReceiveEvents", recorder.OnReceiveEvents);

        var response = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, swap.From, swap.To, "0:act-order-swap");

        Assert.True(response.Accepted);

        await recorder.BothArrived.WaitAsync(TimeSpan.FromSeconds(10));

        // 1. The state write-back precedes the batch (§3.1: "the resolution
        //    completes and the new BattleState is written first, then the batch is
        //    sent"; §6 item 4: the push is delivered before any ReceiveEvents).
        Assert.Equal(new[] { "BattleStateUpdated", "ReceiveEvents" }, recorder.Arrivals.ToArray());

        // 2. Both report the one committed sequence, read from the committed record
        //    rather than derived at either call site (GAME_STATE.md §5.1 item 1).
        Assert.Equal(1, recorder.StateSequence);
        Assert.Equal(recorder.StateSequence, recorder.BatchSequence);

        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IBattleStateRepository>();
        var committed = await repo.GetAsync(battleId);
        Assert.NotNull(committed);
        Assert.Equal(committed!.Sequence, recorder.StateSequence);

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task CardCast_PushesBattleStateUpdatedBeforeReceiveEvents_WithTheSameCommittedSequence()
    {
        var battleId = $"battle-order-cardcast-{Guid.NewGuid():N}";
        await CreateBattleWithPowerAsync(battleId, power: 50);

        var hubConnection = BuildHubConnection();
        await JoinAndConsumeInitialPushAsync(hubConnection, battleId);
        var recorder = new PushOrderRecorder();
        hubConnection.On<JsonElement>("BattleStateUpdated", recorder.OnStatePush);
        hubConnection.On<JsonElement>("ReceiveEvents", recorder.OnReceiveEvents);

        var response = await hubConnection.InvokeAsync<CardCastResponse>(
            "CardCast", battleId, "card-heal", "client-seq-order-card");

        Assert.True(response.Accepted);

        await recorder.BothArrived.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(new[] { "BattleStateUpdated", "ReceiveEvents" }, recorder.Arrivals.ToArray());
        Assert.Equal(1, recorder.StateSequence);
        Assert.Equal(recorder.StateSequence, recorder.BatchSequence);

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task PetSkillCast_PushesBattleStateUpdatedBeforeReceiveEvents_WithTheSameCommittedSequence()
    {
        var battleId = $"battle-order-petskill-{Guid.NewGuid():N}";
        await CreateBattleWithPowerAsync(battleId, power: 50);

        var hubConnection = BuildHubConnection();
        await JoinAndConsumeInitialPushAsync(hubConnection, battleId);
        var recorder = new PushOrderRecorder();
        hubConnection.On<JsonElement>("BattleStateUpdated", recorder.OnStatePush);
        hubConnection.On<JsonElement>("ReceiveEvents", recorder.OnReceiveEvents);

        var response = await hubConnection.InvokeAsync<PetSkillCastResponse>(
            "PetSkillCast", battleId, "client-seq-order-pet");

        Assert.True(response.Accepted);

        await recorder.BothArrived.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(new[] { "BattleStateUpdated", "ReceiveEvents" }, recorder.Arrivals.ToArray());
        Assert.Equal(1, recorder.StateSequence);
        Assert.Equal(recorder.StateSequence, recorder.BatchSequence);

        await hubConnection.StopAsync();
    }

    /// <summary>
    /// The reconnect/resync snapshot's own envelope agreement
    /// (<c>SIGNALR_PROTOCOL.md</c> §7.1): <c>GetBattleState</c> reports the
    /// committed <c>Sequence</c> as its <c>serverSequence</c> <b>and</b> inside the
    /// snapshot it carries. The client refuses an accepted response whose two
    /// spellings disagree rather than choosing one, so this is the producer-side
    /// half of that rule.
    /// </summary>
    [Fact]
    public async Task GetBattleState_EnvelopeSequenceEqualsTheSnapshotSequence()
    {
        var battleId = $"battle-order-recovery-{Guid.NewGuid():N}";
        var swap = await FindValidPairAsync(battleId);


        var hubConnection = BuildHubConnection();
        await JoinAndConsumeInitialPushAsync(hubConnection, battleId);

        // Commit one resolution, so the snapshot reports a non-zero version.
        var response = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, swap.From, swap.To, "0:act-order-recovery");
        Assert.True(response.Accepted);

        var snapshot = await hubConnection.InvokeAsync<GetBattleStateResponse>("GetBattleState", battleId);

        Assert.True(snapshot.Accepted);
        Assert.NotNull(snapshot.State);
        Assert.Equal(snapshot.State!.Sequence, snapshot.ServerSequence);
        Assert.Equal(1, snapshot.ServerSequence);

        await hubConnection.StopAsync();
    }
}
