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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GameServer.Api.Tests;

/// <summary>
/// TASK-280: the state-push-before-batch window of a committed action's two
/// broadcasts (<c>SIGNALR_PROTOCOL.md</c> §3.1 items 1 and 6, §6 item 4).
///
/// <b>What the window is.</b> Each accepted action path broadcasts the
/// resolution's state first and its event batch second
/// (<c>BattleHub.cs</c>:<c>Swap</c> 1145–1146, <c>CardCast</c> 1297–1298,
/// <c>PetSkillCast</c> 1343–1344). Both calls sit in one <c>try</c>, so a failure
/// after the first has been published leaves the batch undelivered while the
/// caller is still told the action committed (§3.1 item 6: the commit is not
/// undone by a failed broadcast). The client's own admission rule is defined
/// against the state it holds (<c>GAME_STATE.md</c> §5.0; §6 item 3), so once the
/// state frame has advanced that copy, a lost batch is not a gap.
///
/// <b>What is reproduced here.</b> The reachable failure is a delivery failure:
/// the second <c>SendAsync</c> faults after the first completed. This file injects
/// exactly that, deterministically — no timing, no retry, no network — and records
/// the resulting partial broadcast, the acknowledged outcome, and the fact that
/// the server never attempts the batch again (§8 item 8: there is no replay
/// channel; §7's snapshot is the convergence path).
///
/// <b>What is not reproduced, and why.</b> TASK-280's other suspected trigger — a
/// throw from <c>BattleEventWireProjection.Project</c> between the two sends — is
/// not reachable from any value the committed pipeline can produce. The
/// projection is total over the closed discriminator set (18 mapped kinds), its
/// only value-dependent failure is an event whose discriminator and payload
/// disagree (an accessor throw, <c>BattleEvent.cs</c>:625), and no factory builds
/// one because each sets its discriminator together with its own payload
/// (<c>BattleEvent.cs</c>:849–1180). The assertion below pins that boundary.
/// </summary>
public sealed class BattleHubPartialBroadcastTests : IClassFixture<ApiIntegrationTests.ApiIntegrationFactory>
{
    private readonly ApiIntegrationTests.ApiIntegrationFactory _factory;

    private static readonly PlayerId Owner = new("player_partial_broadcast_api_test");

    private static readonly BattleStateService.PetConfiguration PetConfig = new(
        new PetId("pet_instance_partial_broadcast_1"),
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

    public BattleHubPartialBroadcastTests(ApiIntegrationTests.ApiIntegrationFactory factory)
    {
        _factory = factory;
    }

    private void EnsureSeeded() => TestProvisionedContent.Seed(_factory.Services);

    private async Task<SwapRequest> CreateBattleWithValidPairAsync(string battleId)
    {
        EnsureSeeded();

        using var scope = _factory.Services.CreateScope();
        var battleService = scope.ServiceProvider.GetRequiredService<BattleStateService>();

        var state = await battleService.CreateBattleAsync(
            battleId, Owner, PetConfig, BossDefinitions.HoaLong);

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
    public async Task Swap_WhenTheBatchBroadcastFails_LeavesTheCommittedStatePushedAndTheBatchLost()
    {
        var battleId = $"battle-partial-broadcast-{Guid.NewGuid():N}";
        var pair = await CreateBattleWithValidPairAsync(battleId);

        using var scope = _factory.Services.CreateScope();
        var battles = scope.ServiceProvider.GetRequiredService<BattleStateService>();
        var runtime = scope.ServiceProvider.GetRequiredService<RuntimeService>();
        var repo = scope.ServiceProvider.GetRequiredService<IBattleStateRepository>();
        var logger = new RecordingLogger<BattleHub>();

        // The batch broadcast fails; the state broadcast succeeds. That is the one
        // ordering §6 item 4 permits, and the failure is injected at the delivery
        // boundary — after both payloads have been projected.
        var clients = new SelectiveFailureHubCallerClients(failingMethod: "ReceiveEvents");
        var hub = new BattleHub(runtime, battles, logger) { Clients = clients };

        var response = await hub.Swap(battleId, pair.From, pair.To, "0:act-partial-broadcast");

        // 1. The caller is told the committed action was accepted although its batch
        //    never reached the group (§3.1 item 6).
        Assert.True(response.Accepted);
        Assert.Null(response.Reason);

        // 2. Both broadcasts were attempted in the documented order: the state frame
        //    first, the batch second (§6 item 4). Only the state frame was delivered.
        Assert.Equal(
            new[] { "BattleStateUpdated", "ReceiveEvents" },
            clients.GroupProxy.Attempts.Select(attempt => attempt.Method).ToArray());
        Assert.Equal(
            new[] { "BattleStateUpdated" },
            clients.GroupProxy.Delivered.Select(attempt => attempt.Method).ToArray());

        // 3. The state frame the group received carries the committed sequence, so a
        //    client holding it has already advanced past the resolution whose batch
        //    it never received (GAME_STATE.md §5.0).
        var pushed = Assert.IsType<BattleStateUpdated>(clients.GroupProxy.Attempts[0].Args[0]);
        Assert.Equal(1, pushed.Sequence);

        // 4. The batch payload was projected successfully before the failing send: the
        //    loss is a delivery failure, not a projection failure. This is also why
        //    projecting the batch earlier cannot prevent the loss — the projection had
        //    already completed here.
        var lostBatch = Assert.IsType<ReceiveEventsPayload>(clients.GroupProxy.Attempts[1].Args[0]);
        Assert.Equal(battleId, lostBatch.BattleId);
        Assert.Equal(pushed.Sequence, lostBatch.ServerSequence);
        Assert.NotEmpty(lostBatch.Events);

        // 5. The failure is swallowed and logged, and nothing is retried: no second
        //    ReceiveEvents attempt exists anywhere on this path (§8 item 8).
        var logged = Assert.Single(logger.Logs);
        Assert.Equal(LogLevel.Error, logged.Level);
        Assert.Contains(
            "Failed to broadcast post-commit notifications",
            logged.Message,
            StringComparison.Ordinal);
        Assert.Contains(battleId, logged.Message, StringComparison.Ordinal);
        Assert.Equal(2, clients.GroupProxy.Attempts.Count);

        // 6. The committed record is the resolution's complete result at the sequence
        //    the state frame reported (GAME_STATE.md §5.1 item 3): the client's copy is
        //    authoritative, and only the batch is gone.
        var committed = await repo.GetAsync(battleId);
        Assert.NotNull(committed);
        Assert.Equal(pushed.Sequence, committed!.Sequence);
    }

    /// <summary>
    /// Pins the boundary TASK-280's projection-failure hypothesis depends on: the
    /// projection's value-dependent failure needs an event whose discriminator and
    /// payload disagree, and the only value that carries one is
    /// <c>default(BattleEvent)</c> — a discriminator with no payload. Every factory
    /// builds the two together, and the board-side guard keeps the projection's
    /// other value-dependent throw (<c>GemTypes.ToContractName</c>) out of reach
    /// (<c>BoardStateTests.Board_ShouldRejectAnUndocumentedGemValue</c>), so no
    /// committed resolution can hand the projection such a value.
    /// </summary>
    [Fact]
    public void EventProjection_RejectsOnlyADiscriminatorWithoutItsPayload_WhichNoResolutionProduces()
    {
        var thrown = Assert.Throws<InvalidOperationException>(
            () => BattleEventWireProjection.Project([default(BattleEvent)]));

        Assert.Contains("carries no Match", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// One recorded <c>SendCoreAsync</c> call.
    /// </summary>
    private sealed record RecordedSend(string Method, object?[] Args);

    /// <summary>
    /// Records every attempted group send, and fails the one method the test names
    /// — the delivery failure TASK-280's window needs, injected without timing.
    /// </summary>
    private sealed class SelectiveFailureClientProxy : IClientProxy
    {
        private readonly string _failingMethod;

        public SelectiveFailureClientProxy(string failingMethod)
        {
            _failingMethod = failingMethod;
        }

        /// <summary>Every send the hub attempted, in order, delivered or not.</summary>
        public List<RecordedSend> Attempts { get; } = new();

        /// <summary>The sends that were not made to fail, in order.</summary>
        public List<RecordedSend> Delivered { get; } = new();

        public Task SendCoreAsync(
            string method,
            object?[] args,
            CancellationToken cancellationToken = default)
        {
            var attempt = new RecordedSend(method, args);
            Attempts.Add(attempt);

            if (string.Equals(method, _failingMethod, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Simulated SignalR delivery failure.");
            }

            Delivered.Add(attempt);

            return Task.CompletedTask;
        }
    }

    private sealed class SelectiveFailureHubCallerClients : IHubCallerClients
    {
        public SelectiveFailureHubCallerClients(string failingMethod)
        {
            CallerProxy = new SelectiveFailureClientProxy(failingMethod);
            GroupProxy = new SelectiveFailureClientProxy(failingMethod);
        }

        public SelectiveFailureClientProxy CallerProxy { get; }

        public SelectiveFailureClientProxy GroupProxy { get; }

        public IClientProxy Caller => CallerProxy;

        public IClientProxy Group(string groupName) => GroupProxy;

        public IClientProxy All => GroupProxy;

        public IClientProxy Others => GroupProxy;

        public IClientProxy Client(string connectionId) => GroupProxy;

        public IClientProxy User(string userId) => GroupProxy;

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => GroupProxy;

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => GroupProxy;

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => GroupProxy;

        public IClientProxy GroupExcept(
            string groupName,
            IReadOnlyList<string> excludedConnectionIds) => GroupProxy;

        public IClientProxy Users(IReadOnlyList<string> userIds) => GroupProxy;

        public IClientProxy OthersInGroup(string groupName) => GroupProxy;
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Logs { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Logs.Add((logLevel, formatter(state, exception)));
    }
}
