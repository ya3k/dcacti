using System.Net.Http.Headers;
using System.Text.Json;
using GameServer.Api.Hubs;
using GameServer.Application.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GameServer.Api.Tests;

/// <summary>
/// The server-authoritative reconnect/resync recovery path,
/// <c>GetBattleState(battleId)</c> (<c>SIGNALR_PROTOCOL.md</c> §7, §5;
/// <c>ADR-008</c>; <c>GAME_STATE.md</c> §2.8, §5.3).
///
/// <code>
/// SignalR client                       (authenticated session, §1 items 3–5)
///         ↓
/// GetBattleState(battleId)             §7.1 — request/response, not a broadcast
///         ↓
/// BattleHub                            §7 — identity read, then delegate
///         ↓
/// BattleStateService                   §2.8 — ownership, server-side
///         ↓
/// IBattleStateRepository → Redis       REDIS_STATE.md §1 — battle:{battleId}:state
///         ↓
/// authoritative BattleState
///         ↓
/// §4 wire projection                   (the same mapping the push uses)
///         ↓
/// caller
/// </code>
///
/// <b>Snapshot recovery, not event replay.</b> §7.2 has the client discard its
/// local prediction and re-render from this snapshot; no event log, replay
/// channel, or reconstruction from <c>ReceiveEvents</c> exists (<c>ADR-008</c>,
/// <c>ARCHITECTURE.md</c> §5.2, §8 item 8). The events assertion below proves the
/// recovery response carries state and no events.
///
/// <b>The store is the API suite's documented test double</b>
/// (<see cref="ApiTestBattleStateRepository"/>), which models the
/// <c>Sequence</c> compare-and-set and reports absence for an unknown id exactly
/// as <c>REDIS_STATE.md</c> §3 requires. The real Redis key, its sliding TTL, and
/// its expiry are exercised against a live Redis in
/// <c>GameServer.Infrastructure.Tests.RedisBattleStateRepositoryTests</c> and
/// <c>RedisBattleStateSmokeTest</c>; no production TTL is modified anywhere.
/// </summary>
public sealed class BattleHubReconnectRecoveryTests : IClassFixture<ApiIntegrationTests.ApiIntegrationFactory>
{
    private readonly ApiIntegrationTests.ApiIntegrationFactory _factory;

    /// <summary>The battle's owner — the identity its <c>PlayerId</c> records (§2.8).</summary>
    private static readonly PlayerId Owner = new("player_recovery_api_owner");

    /// <summary>A different authenticated Player, who owns nothing here.</summary>
    private static readonly PlayerId Foreigner = new("player_recovery_api_foreigner");

    private static string OwnerSessionToken => TestApplicationSession.Mint(Owner.Value);

    private static string ForeignerSessionToken => TestApplicationSession.Mint(Foreigner.Value);

    private static readonly BattleStateService.PetConfiguration PetConfig = new(
        new PetId("pet_instance_recovery_api_1"),
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

    public BattleHubReconnectRecoveryTests(ApiIntegrationTests.ApiIntegrationFactory factory)
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

    /// <summary>
    /// A hub connection presenting <paramref name="token"/> as the application
    /// session through SignalR's standard access-token mechanism — the one
    /// documented way (§1 item 3, <c>ADR-015</c> D4).
    /// </summary>
    private HubConnection BuildHubConnection(string? token)
    {
        EnsureSeeded();

        return new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/battle", options =>
            {
                options.HttpMessageHandlerFactory = _ =>
                    new BearerMessageHandler(_factory.Server.CreateHandler(), token!);

                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;

                if (token is not null)
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates a battle owned by <paramref name="owner"/> and stores it in the
    /// active-state record, optionally after a committed resolution's worth of
    /// state change.
    /// </summary>
    private async Task CreateBattleAsync(
        string battleId,
        PlayerId owner,
        int sequence = 0,
        int turn = 0)
    {
        EnsureSeeded();

        using var scope = _factory.Services.CreateScope();
        var battleService = scope.ServiceProvider.GetRequiredService<BattleStateService>();
        var repo = scope.ServiceProvider.GetRequiredService<IBattleStateRepository>();

        var state = await battleService.CreateBattleAsync(
            battleId, owner, PetConfig, BossDefinitions.HoaLong);

        if (sequence == 0 && turn == 0)
        {
            return;
        }

        // Advance the record the way a committed resolution would — one atomic
        // write-back under the Sequence compare-and-set (GAME_STATE.md §5.1), with
        // the counters and the board's first cell changed together. Recovery must
        // report exactly this record.
        var resolved = state with
        {
            Sequence = sequence,
            Turn = turn,
            Combo = 3,
            MatchCount = 3,
        };

        Assert.True(await repo.TryUpdateAsync(resolved, state.Sequence));
    }

    // -----------------------------------------------------------------------
    // 14.1 — successful recovery by the owning, authenticated caller
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetBattleState_ReturnsAcceptedSnapshot_ForTheOwningPlayer()
    {
        var battleId = $"battle-recovery-success-{Guid.NewGuid():N}";
        await CreateBattleAsync(battleId, Owner);

        var hubConnection = BuildHubConnection(OwnerSessionToken);
        await hubConnection.StartAsync();

        var response = await hubConnection.InvokeAsync<GetBattleStateResponse>(
            "GetBattleState", battleId);

        // §7.1 / §5: the snapshot and its Sequence, accepted.
        Assert.True(response.Accepted);
        Assert.Null(response.Reason);
        Assert.NotNull(response.State);

        Assert.Equal(battleId, response.State!.BattleId);
        Assert.Equal(response.State.Sequence, response.ServerSequence);

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task GetBattleState_ReturnsTheCurrentRedisSnapshot_AfterACommittedResolution()
    {
        // The documented edge case: reconnecting after a completed action returns
        // the exact current record, not the pre-resolution state
        // (SIGNALR_PROTOCOL.md §3.1 item 1, GAME_STATE.md §5.3).
        var battleId = $"battle-recovery-progressed-{Guid.NewGuid():N}";
        await CreateBattleAsync(battleId, Owner, sequence: 4, turn: 2);

        var hubConnection = BuildHubConnection(OwnerSessionToken);
        await hubConnection.StartAsync();

        var response = await hubConnection.InvokeAsync<GetBattleStateResponse>(
            "GetBattleState", battleId);

        Assert.True(response.Accepted);

        // §7.1: `Sequence` is the committed value, read from the record — never
        // incremented or recomputed by the recovery request that asks for it
        // (GAME_STATE.md §5.2 item 3).
        Assert.Equal(4, response.ServerSequence);
        Assert.Equal(4, response.State!.Sequence);
        Assert.Equal(2, response.State.Turn);

        // GAME_STATE.md §5.3 item 1: the resolution's values are carried by the
        // snapshot, because everything a committed Swap produces is BattleState.
        Assert.Equal(3, response.State.PlayerState.Combo);
        Assert.Equal(3, response.State.PlayerState.MatchCount);

        await hubConnection.StopAsync();
    }

    /// <summary>
    /// §7.2 / <c>ADR-008</c>: recovery is a snapshot, and the request that asks for
    /// one does not re-publish it. The response carries state and no events, and
    /// no <c>ReceiveEvents</c> batch is broadcast for a read.
    /// </summary>
    [Fact]
    public async Task GetBattleState_DeliversTheSnapshotToTheCallerOnly_AndBroadcastsNoEvents()
    {
        var battleId = $"battle-recovery-caller-only-{Guid.NewGuid():N}";
        await CreateBattleAsync(battleId, Owner);

        var hubConnection = BuildHubConnection(OwnerSessionToken);

        var eventsReceived = false;
        var statePushed = false;

        hubConnection.On<JsonElement>("ReceiveEvents", _ => eventsReceived = true);
        hubConnection.On<JsonElement>("BattleStateUpdated", _ => statePushed = true);

        await hubConnection.StartAsync();

        var response = await hubConnection.InvokeAsync<GetBattleStateResponse>(
            "GetBattleState", battleId);

        Assert.True(response.Accepted);

        // §5 item 4 / §7: the result is the direct return of this call. Recovery
        // resolves nothing, so it neither broadcasts an event batch nor pushes
        // state to a group (GAME_STATE.md §5.1 item 1).
        Assert.False(eventsReceived, "recovery must not deliver a ReceiveEvents batch (§7.2, ADR-008)");
        Assert.False(statePushed, "recovery must not broadcast a BattleStateUpdated push (§4 is the join push)");

        await hubConnection.StopAsync();
    }

    // -----------------------------------------------------------------------
    // 6 — wire projection compatibility with the BattleStateUpdated push
    // -----------------------------------------------------------------------

    /// <summary>
    /// Serializes a payload to the JSON that actually travels on the wire.
    ///
    /// <b>Why not <c>JsonSerializer.SerializeToElement</c> directly.</b> The wire
    /// member names of this contract are not a serializer default: the hub names
    /// every member explicitly through <c>JsonPropertyName</c>
    /// (<c>SIGNALR_PROTOCOL.md</c> §3.2.3 item 2, §8.1) — a payload member is
    /// <c>battleId</c>, never the CLR property's <c>BattleId</c>. Serializing the
    /// CLR type with default options would assert the CLR spelling and say nothing
    /// about the wire, so these tests serialize the way the SignalR JSON protocol
    /// does and assert on the resulting member names.
    /// </summary>
    private static JsonElement ToWireJson<T>(T payload) =>
        SignalRWireJson.ToElement(payload);

    /// <summary>
    /// §7.1: the recovered snapshot is the same §4 projection the
    /// <c>BattleStateUpdated</c> push carries, so it is wire-compatible with an
    /// ordinary state delivery and introduces no recovery-only schema
    /// (§4 item 11, §8 item 7).
    /// </summary>
    [Fact]
    public async Task GetBattleState_Projection_IsTheDocumentedBattleStateUpdatedShape()
    {
        var battleId = $"battle-recovery-projection-{Guid.NewGuid():N}";
        await CreateBattleAsync(battleId, Owner);

        var hubConnection = BuildHubConnection(OwnerSessionToken);
        await hubConnection.StartAsync();

        var response = await hubConnection.InvokeAsync<GetBattleStateResponse>(
            "GetBattleState", battleId);

        Assert.True(response.Accepted);

        // The response envelope is the §5 acknowledgement plus the snapshot and its
        // sequence — the same §5 envelope every documented hub method returns,
        // spelled in the contract's camelCase wire names.
        var envelope = ToWireJson(response);
        Assert.Equal(
            new[] { "accepted", "reason", "serverSequence", "state" },
            envelope.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));

        // §4 item 4: the snapshot carries exactly the implemented stage's fields —
        // the same member set the §4 push enumerates — and no other field. The
        // expected set is exactly the one ApiIntegrationTests asserts for the §4
        // push, which is the compatibility this test exists to prove. §7.1 states
        // that the two paths agree member for member, `petState.statusEffects[]`
        // (§4.3 items 2 and 14) and `bossState` (§4.4) included.
        var state = envelope.GetProperty("state");
        Assert.Equal(
            new[]
            {
                "battleId", "board", "bossState", "petState", "playerState", "rngSeed",
                "rngState", "sequence", "turn",
            },
            state.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));

        // §4.2: `playerState` carries exactly the two Match/Combo members.
        Assert.Equal(
            new[] { "combo", "matchCount" },
            state.GetProperty("playerState")
                .EnumerateObject()
                .Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal));

        // §4.3 items 2, 13, 14 and 15: `petState` carries `passiveId`, `passiveProgress`,
        // the conditional `passiveResetOverride`, `equippedCards` — the loadout
        // snapshot that CardCast/PetSkillCast action paths read, always present,
        // non-empty, and non-nullable — the active Pet's active Status Effects, which
        // are always an array, and the active Pet's three live combat values
        // (`hp`/`maxHp`/`power`).
        var petState = state.GetProperty("petState");
        Assert.Equal(
            new[] { "equippedCards", "hp", "maxHp", "passiveId", "passiveProgress", "power", "statusEffects" },
            petState.EnumerateObject()
                .Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal("xich-lang", petState.GetProperty("passiveId").GetString());
        Assert.Equal(
            new[] { "current", "threshold" },
            petState.GetProperty("passiveProgress")
                .EnumerateObject()
                .Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal));

        // §4.3 item 15: the three live combat values are the authoritative PetState's,
        // read as sent — `maxHp` never derived from `hp`, and an initial `power = 0`
        // published as 0.
        Assert.True(petState.GetProperty("hp").GetInt32() > 0);
        Assert.Equal(
            petState.GetProperty("hp").GetInt32(),
            petState.GetProperty("maxHp").GetInt32());
        Assert.Equal(0, petState.GetProperty("power").GetInt32());

        var equippedCards = petState.GetProperty("equippedCards")
            .EnumerateArray()
            .Select(c => c.GetString())
            .ToArray();

        Assert.Equal(
            new[] { "card-heal", "card-shield", "card-power-charge", "card-inferno" },
            equippedCards);

        // §4.3 item 14: the collection is always present and an active Pet with no
        // active effect is sent an EMPTY array — never an omission and never null.
        Assert.True(petState.TryGetProperty("statusEffects", out var statusEffects));
        Assert.Equal(JsonValueKind.Array, statusEffects.ValueKind);
        Assert.Empty(statusEffects.EnumerateArray());

        // §4.4: the snapshot carries the same three-member Boss projection the push
        // does — the canonical technical Identity plus the live HP pair — so a
        // recovered client re-renders the Boss from either path with one model
        // (§7.1, ADR-008, §4.4 item 10).
        var bossState = state.GetProperty("bossState");
        Assert.Equal(
            new[] { "bossId", "hp", "maxHp" },
            bossState.EnumerateObject()
                .Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal("boss-hoa-long", bossState.GetProperty("bossId").GetString());

        // §4.1: `board` is exactly the 64-cell authoritative board.
        Assert.Equal(64, state.GetProperty("board").GetProperty("cells").GetArrayLength());

        await hubConnection.StopAsync();
    }

    /// <summary>
    /// <c>GAME_STATE.md</c> §2.8 item 3 / <c>ADR-014</c> decision 3: the owner
    /// identity is server-only. It is excluded from the snapshot projection, so no
    /// <c>playerId</c> — nor any other owner-identifying member — appears anywhere
    /// in the recovery response, at any depth.
    /// </summary>
    [Fact]
    public async Task GetBattleState_ExposesNoServerOnlyPlayerId()
    {
        var battleId = $"battle-recovery-owner-excluded-{Guid.NewGuid():N}";
        await CreateBattleAsync(battleId, Owner);

        var hubConnection = BuildHubConnection(OwnerSessionToken);
        await hubConnection.StartAsync();

        var response = await hubConnection.InvokeAsync<GetBattleStateResponse>(
            "GetBattleState", battleId);

        Assert.True(response.Accepted);

        var serialized = SignalRWireJson.ToWireText(response);

        // §2.8 item 3: `PlayerId` is a wire member of no projection. The absence is
        // asserted on the raw JSON, so a member added at any nesting depth is
        // caught.
        foreach (var forbidden in new[] { "playerId", "discordUserId", "userId", "ownerId" })
        {
            Assert.DoesNotContain(forbidden, serialized, StringComparison.OrdinalIgnoreCase);
        }

        // The owner's own identity string is not disclosed either — the member's
        // absence is not merely a naming convention.
        Assert.DoesNotContain(Owner.Value, serialized, StringComparison.Ordinal);

        await hubConnection.StopAsync();
    }

    // -----------------------------------------------------------------------
    // 14.2 — a caller who does not own the battle
    // -----------------------------------------------------------------------

    /// <summary>
    /// Player A requests Player B's battle: the documented rejection, no state
    /// returned, and nothing about the battle's existence or ownership disclosed
    /// (<c>SIGNALR_PROTOCOL.md</c> §7.3, <c>GAME_STATE.md</c> §2.8,
    /// <c>API_CONTRACTS.md</c> §4 note 7).
    /// </summary>
    [Fact]
    public async Task GetBattleState_RejectsACallerWhoDoesNotOwnTheBattle()
    {
        var battleId = $"battle-recovery-foreign-{Guid.NewGuid():N}";
        await CreateBattleAsync(battleId, Owner);

        var hubConnection = BuildHubConnection(ForeignerSessionToken);
        await hubConnection.StartAsync();

        var response = await hubConnection.InvokeAsync<GetBattleStateResponse>(
            "GetBattleState", battleId);

        // §7.3: the documented recovery failure, and no state at all.
        Assert.False(response.Accepted);
        Assert.Equal("BATTLE_NOT_FOUND", response.Reason);
        Assert.Null(response.State);
        Assert.Null(response.ServerSequence);

        await hubConnection.StopAsync();
    }

    /// <summary>
    /// The foreign-battle rejection is byte-for-byte the unknown-battle rejection:
    /// the caller cannot tell the two apart, which is what prevents the response
    /// from confirming that another Player's battle exists
    /// (<c>API_CONTRACTS.md</c> §4 notes 6–7).
    /// </summary>
    [Fact]
    public async Task GetBattleState_ForeignAndUnknownBattles_AreTheSameAnswer()
    {
        var foreignBattleId = $"battle-recovery-mine-{Guid.NewGuid():N}";
        await CreateBattleAsync(foreignBattleId, Owner);

        var hubConnection = BuildHubConnection(ForeignerSessionToken);
        await hubConnection.StartAsync();

        var foreign = await hubConnection.InvokeAsync<GetBattleStateResponse>(
            "GetBattleState", foreignBattleId);

        var unknown = await hubConnection.InvokeAsync<GetBattleStateResponse>(
            "GetBattleState", $"battle-recovery-missing-{Guid.NewGuid():N}");

        Assert.Equal(
            SignalRWireJson.ToWireText(unknown),
            SignalRWireJson.ToWireText(foreign));

        await hubConnection.StopAsync();
    }

    // -----------------------------------------------------------------------
    // 14.3 — a battle that does not exist
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetBattleState_ReturnsBattleNotFound_ForAnUnknownBattle()
    {
        var hubConnection = BuildHubConnection(OwnerSessionToken);
        await hubConnection.StartAsync();

        var response = await hubConnection.InvokeAsync<GetBattleStateResponse>(
            "GetBattleState", $"battle-recovery-never-{Guid.NewGuid():N}");

        Assert.False(response.Accepted);
        Assert.Equal("BATTLE_NOT_FOUND", response.Reason);
        Assert.Null(response.State);

        await hubConnection.StopAsync();
    }

    // -----------------------------------------------------------------------
    // 14.4 — a battle whose active state is no longer available
    // -----------------------------------------------------------------------

    /// <summary>
    /// An unavailable active record — the store's observable equivalent of
    /// <c>REDIS_STATE.md</c> §3's inactivity expiry, and of the abandoned battle
    /// §3 describes — yields the documented <c>BATTLE_NOT_FOUND</c> rather than an
    /// exception or an empty state (§7.3, <c>REDIS_STATE.md</c> §5).
    /// </summary>
    /// <remarks>
    /// Production TTL is not modified: the delete below reaches the same
    /// documented end state a sliding TTL reaches ("the key is gone"), without
    /// redefining the store's expiry. The sliding TTL itself is verified against a
    /// live Redis elsewhere (<c>RedisBattleStateRepositoryTests</c>,
    /// <c>RedisBattleStateSmokeTest</c>).
    /// </remarks>
    [Fact]
    public async Task GetBattleState_ReturnsBattleNotFound_WhenTheActiveStateIsGone()
    {
        var battleId = $"battle-recovery-gone-{Guid.NewGuid():N}";
        await CreateBattleAsync(battleId, Owner);

        // The record is removed, as an elapsed TTL or a completed battle's clear
        // would leave it (REDIS_STATE.md §3).
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<IBattleStateRepository>()
                .DeleteAsync(battleId);
        }

        var hubConnection = BuildHubConnection(OwnerSessionToken);
        await hubConnection.StartAsync();

        var response = await hubConnection.InvokeAsync<GetBattleStateResponse>(
            "GetBattleState", battleId);

        // The owning caller gets the same documented answer as an unknown battle —
        // recovery invents no state and repairs nothing (REDIS_STATE.md §2 item 2).
        Assert.False(response.Accepted);
        Assert.Equal("BATTLE_NOT_FOUND", response.Reason);
        Assert.Null(response.State);

        await hubConnection.StopAsync();
    }

    // -----------------------------------------------------------------------
    // 14.5 — authentication
    // -----------------------------------------------------------------------

    /// <summary>
    /// §1 item 5 / <c>ADR-015</c> D6: a connection presenting no valid session is
    /// rejected before the hub is usable, so <c>GetBattleState</c> is not
    /// reachable without an authenticated session. No new auth flow is introduced
    /// — this is the existing SignalR authentication contract.
    /// </summary>
    [Fact]
    public async Task GetBattleState_IsUnreachableWithoutAnAuthenticatedSession()
    {
        var battleId = $"battle-recovery-unauth-{Guid.NewGuid():N}";
        await CreateBattleAsync(battleId, Owner);

        var hubConnection = BuildHubConnection(token: null);

        // The connection never becomes usable, so it cannot invoke the method.
        await Assert.ThrowsAnyAsync<Exception>(() => hubConnection.StartAsync());
        Assert.Equal(HubConnectionState.Disconnected, hubConnection.State);
    }

    /// <summary>
    /// A session whose <c>player_id</c> identifies nobody does not establish an
    /// identity the hub may act for, and it is refused at the connection
    /// (<c>API_CONTRACTS.md</c> §2.8; the hub's own <c>OnConnectedAsync</c>).
    /// </summary>
    [Fact]
    public async Task GetBattleState_IsUnreachableForASessionWithNoPlayerId()
    {
        var hubConnection = BuildHubConnection(TestApplicationSession.Mint(playerId: null));

        await Assert.ThrowsAnyAsync<Exception>(() => hubConnection.StartAsync());
    }

    /// <summary>
    /// A session for another Player does not act as the owner: the identity the
    /// hub recovers with is the presented token's own claim, so a foreign session
    /// is rejected exactly as any unowned battle is (<c>ADR-015</c> D3).
    /// </summary>
    [Fact]
    public async Task GetBattleState_ActsForTheAuthenticatedClaimedPlayer_NotTheBattleOwner()
    {
        var battleId = $"battle-recovery-claim-{Guid.NewGuid():N}";
        await CreateBattleAsync(battleId, Owner);

        var hubConnection = BuildHubConnection(ForeignerSessionToken);
        await hubConnection.StartAsync();

        // The foreign session cannot recover the owner's battle...
        var foreign = await hubConnection.InvokeAsync<GetBattleStateResponse>(
            "GetBattleState", battleId);

        Assert.False(foreign.Accepted);
        Assert.Null(foreign.State);

        await hubConnection.StopAsync();

        // ...while the owner's own session recovers the very same record.
        var ownerConnection = BuildHubConnection(OwnerSessionToken);
        await ownerConnection.StartAsync();

        var owned = await ownerConnection.InvokeAsync<GetBattleStateResponse>(
            "GetBattleState", battleId);

        Assert.True(owned.Accepted);
        Assert.Equal(battleId, owned.State!.BattleId);

        await ownerConnection.StopAsync();
    }

    /// <summary>
    /// The battle id is a required input: an absent or blank id names no battle,
    /// and the hub rejects it before reading any state — the same guard the other
    /// hub methods apply to their own required ids.
    /// </summary>
    /// <remarks>
    /// The invocation is bounded: a long-polling connection whose hub method
    /// throws does not always settle its request promptly, so the assertion is
    /// "this does not succeed", evaluated under an explicit timeout rather than by
    /// awaiting the transport indefinitely.
    /// </remarks>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetBattleState_RejectsAnAbsentBattleId(string battleId)
    {
        var hubConnection = BuildHubConnection(OwnerSessionToken);
        await hubConnection.StartAsync();

        var rejected = false;

        try
        {
            await hubConnection
                .InvokeAsync<GetBattleStateResponse>("GetBattleState", battleId)
                .WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception)
        {
            // The documented rejection: no snapshot is produced for an id that
            // names no battle.
            rejected = true;
        }

        Assert.True(rejected, "an absent battle id must not yield a snapshot");

        await hubConnection.StopAsync();
    }

    /// <summary>
    /// <c>SIGNALR_PROTOCOL.md</c> §8 item 8 / <c>ADR-008</c>: there is no event log
    /// or replay channel, so no method that would serve one exists. Recovery is the
    /// snapshot method above and nothing else.
    /// </summary>
    [Fact]
    public async Task Hub_DefinesNoEventReplayOrAdditionalRecoveryMethod()
    {
        var hubConnection = BuildHubConnection(OwnerSessionToken);
        await hubConnection.StartAsync();

        foreach (var absent in new[]
                 {
                     "GetEvents", "GetMissedEvents", "ReplayEvents", "ReceiveEvents",
                     "GetBattleSnapshot", "GetSnapshot", "GetBattleStateSince",
                 })
        {
            // Bounded, for the same transport reason as the absent-id case above:
            // an unresolvable method name does not reliably settle a long-polling
            // request, so "it did not succeed" is asserted under a timeout.
            var succeeded = false;

            try
            {
                await hubConnection
                    .InvokeAsync<object>(absent)
                    .WaitAsync(TimeSpan.FromSeconds(5));

                succeeded = true;
            }
            catch (Exception)
            {
                // The expected outcome: no such method exists.
            }

            Assert.False(succeeded, $"the hub must define no '{absent}' method (§8 item 8, ADR-008)");
        }

        await hubConnection.StopAsync();
    }
}
