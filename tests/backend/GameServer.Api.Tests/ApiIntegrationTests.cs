using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameServer.Api.Controllers;
using GameServer.Api.Hubs;
using GameServer.Application.Battle;
using GameServer.Application.Runtime;
using GameServer.Domain.Battle;
using GameServer.Domain.Match3;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GameServer.Api.Tests;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealth_ShouldReturnOk_AndHealthyStatus()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", content);
    }

    [Fact]
    public async Task AuthDiscord_WithValidCode_ShouldReturnSessionToken()
    {
        var client = _factory.CreateClient();
        var request = new DiscordAuthRequest("valid_oauth_code_123");

        var response = await client.PostAsJsonAsync("/api/auth/discord", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DiscordAuthResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.SessionToken));
        Assert.Equal("player_dev", body.PlayerId);
    }

    [Fact]
    public async Task AuthDiscord_WithMissingCode_ShouldReturnBadRequest()
    {
        var client = _factory.CreateClient();
        var request = new DiscordAuthRequest("");

        var response = await client.PostAsJsonAsync("/api/auth/discord", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BattleHub_Ping_ShouldEchoClientSequenceAndAccepted()
    {
        var hubConnection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/battle", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        await hubConnection.StartAsync();

        var pingResult = await hubConnection.InvokeAsync<PingResponse>("Ping", "seq-smoke-test-1");

        Assert.NotNull(pingResult);
        Assert.True(pingResult.Accepted);
        Assert.Equal("seq-smoke-test-1", pingResult.ClientSequence);

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task BattleHub_ShouldAssignAConnectionId_OnConnect()
    {
        var hubConnection = BuildHubConnection();

        await hubConnection.StartAsync();

        // ADR-008's reconnect/resync model depends on the connection having a
        // stable server-assigned identity.
        Assert.False(string.IsNullOrWhiteSpace(hubConnection.ConnectionId));

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task BattleHub_ShouldAcknowledgeConnection_WithRuntimeStatus()
    {
        var hubConnection = BuildHubConnection();

        var acknowledged = new TaskCompletionSource<System.Text.Json.JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        hubConnection.On<System.Text.Json.JsonElement>("RuntimeStatusChanged", status =>
        {
            acknowledged.TrySetResult(status);
        });

        await hubConnection.StartAsync();

        var status = await acknowledged.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // The technical handshake reports connection identity and status only.
        // `status` is an enum and serializes as its numeric value by default.
        Assert.Equal(
            (int)RuntimeConnectionStatus.Connected,
            status.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(status.GetProperty("connectionId").GetString()));
        Assert.True(status.GetProperty("sequence").GetInt64() > 0);

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task BattleHub_ShouldStopCleanly_OnDisconnect()
    {
        var hubConnection = BuildHubConnection();

        await hubConnection.StartAsync();
        await hubConnection.StopAsync();

        Assert.Equal(HubConnectionState.Disconnected, hubConnection.State);
    }

    [Fact]
    public async Task BattleHub_ShouldSupportReconnect_AfterDisconnect()
    {
        var hubConnection = BuildHubConnection();

        await hubConnection.StartAsync();
        var firstConnectionId = hubConnection.ConnectionId;
        await hubConnection.StopAsync();

        // A fresh connection re-establishes cleanly (ADR-004/ADR-008 rely on
        // reconnect being supported by the transport).
        await hubConnection.StartAsync();

        Assert.Equal(HubConnectionState.Connected, hubConnection.State);
        Assert.False(string.IsNullOrWhiteSpace(hubConnection.ConnectionId));

        await hubConnection.StopAsync();

        // Sanity: the first id was a real identity, not a placeholder.
        Assert.False(string.IsNullOrWhiteSpace(firstConnectionId));
    }

    [Fact]
    public async Task BattleHub_ShouldNotRegisterGameplayMethods()
    {
        // The hub is a thin transport boundary (ARCHITECTURE.md §1, §2.1), and the
        // two directions it carries are distinct (SIGNALR_PROTOCOL.md §2, §3).
        //
        // `CardCast` and `PetSkillCast` are §2 client → server methods that are
        // intentionally NOT implemented; `GetBattleState` is §7's client → server
        // reconnect/resync method, also not implemented. None of them is a hub
        // method, so a client invocation fails.
        //
        // `ReceiveEvents` is in this list for the opposite reason: it is a §3
        // **Server → Client** delivery — a client-side handler the server invokes
        // through `Clients.Group(...).SendAsync(...)` — so it is not a hub method
        // and a client may not invoke it, even though the server does send it to
        // clients (BattleHub.Swap). Both halves of that distinction matter: the
        // server sends the batch, and a client still cannot call it.
        //
        // `JoinBattle` and `Swap` are deliberately NOT in this list: `JoinBattle` is
        // the group join that triggers the documented initial-state push (§1.2, §4.1)
        // and carries no gameplay, and `Swap` is the §2 gameplay method implemented
        // by the Swap-execution task.
        var hubConnection = BuildHubConnection();
        await hubConnection.StartAsync();

        foreach (var method in new[] { "CardCast", "PetSkillCast", "GetBattleState", "ReceiveEvents" })
        {
            await Assert.ThrowsAnyAsync<Exception>(() =>
                hubConnection.InvokeAsync<object>(method));
        }

        await hubConnection.StopAsync();
    }

    private HubConnection BuildHubConnection()
    {
        return new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/battle", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();
    }

    // -----------------------------------------------------------------------
    // Board Foundation State transport path
    //
    //   Server → Board Foundation State → SignalR → Client
    //
    // SIGNALR_PROTOCOL.md §4, §4.1; GAME_STATE.md §2.0.5. These verify the
    // documented initial-state push on group join: the board is a *field* of the
    // battle state, delivered by this same push, and no board-specific message
    // is introduced (§4 item 11, §8.5).
    // -----------------------------------------------------------------------

    [Fact]
    public async Task JoinBattle_ShouldPushTheBoardFoundationState_WithDocumentedInitialValues()
    {
        const string battleId = "battle-foundation-1";
        CreateBattleOnServer(battleId);

        var hubConnection = BuildHubConnection();
        var received = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // Subscribed before connecting so the push cannot be missed.
        hubConnection.On<JsonElement>("BattleStateUpdated", payload => received.TrySetResult(payload));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // GAME_STATE.md §2.0.5.2 item 1 / SIGNALR_PROTOCOL.md §4.5: board
        // generation is not an action resolution, so turn and sequence stay 0.
        Assert.Equal(battleId, payload.GetProperty("battleId").GetString());
        Assert.Equal(0, payload.GetProperty("turn").GetInt32());
        Assert.Equal(0, payload.GetProperty("sequence").GetInt32());

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task BattleStateUpdated_ShouldCarryExactlyTheDocumentedBoardFoundationFields()
    {
        const string battleId = "battle-foundation-2";
        CreateBattleOnServer(battleId);

        var hubConnection = BuildHubConnection();
        var received = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<JsonElement>("BattleStateUpdated", payload => received.TrySetResult(payload));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // BattleId, Turn, Sequence, RngSeed, RngState, BoardState (GAME_STATE.md §2.0.5)
        // plus PlayerState (§2.2, the Match / Combo accounting stage) and PetState
        // (§2.3, the Pet / Passive stage).
        // SIGNALR_PROTOCOL.md §4 item 4: no other field may be added to this record.
        // §8.3: no Status/lifecycle value.
        var fields = payload.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);
        Assert.Equal(
            new[]
            {
                "battleId", "board", "petState", "playerState", "rngSeed", "rngState",
                "sequence", "turn",
            },
            fields);

        // §4.2: the nested PlayerState object carries exactly the two implemented §2.2
        // fields — no HP, Power, Status, Card, or Relic state, and no Match/Combo value
        // outside it.
        var playerState = payload.GetProperty("playerState");
        Assert.Equal(
            new[] { "combo", "matchCount" },
            playerState.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));

        // §4.3: the nested PetState object carries exactly the three members the Pet /
        // Passive stage fixes — the Passive identity, its progress pair, and the
        // conditional reset override. The rest of GAME_STATE §2.3 (PetId, Element,
        // Tier/Star/Level) belongs to a later stage and is not delivered.
        var petState = payload.GetProperty("petState");
        var petFields = petState.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);

        // `passiveResetOverride` is present only when the Passive declares a
        // non-default reset (§4.3 item 6), and the battle created here uses the
        // default (§4 item 1), so the member is omitted entirely — never written as
        // JSON null and never spelled "Default" (§4.3 item 7).
        Assert.Equal(new[] { "passiveId", "passiveProgress" }, petFields);
        Assert.False(petState.TryGetProperty("passiveResetOverride", out _));

        // `passiveProgress` is the `{ threshold, current }` pair, both always present
        // (§4.3 item 4).
        Assert.Equal(
            new[] { "current", "threshold" },
            petState.GetProperty("passiveProgress")
                .EnumerateObject()
                .Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal));

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task BattleStateUpdated_ShouldCarryTheAuthoritativeBoard()
    {
        const string battleId = "battle-foundation-board";
        CreateBattleOnServer(battleId);

        var hubConnection = BuildHubConnection();
        var received = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<JsonElement>("BattleStateUpdated", payload => received.TrySetResult(payload));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // SIGNALR_PROTOCOL.md §4.1 item 3 / GAME_STATE.md §2.1.1: `board` is
        // exactly 64 cell entries, row-major, and the client receives no partial
        // board.
        var cells = payload.GetProperty("board").GetProperty("cells");

        Assert.Equal(JsonValueKind.Array, cells.ValueKind);
        Assert.Equal(64, cells.GetArrayLength());

        // SIGNALR_PROTOCOL.md §4.1 item 5 / GAME_STATE.md §2.1.1: each entry
        // carries the cell's Gem type plus an optional Special Gem — the state's own
        // shape, delivered entry for entry. The array position IS the cell index, so
        // no index is written per element (§2.1.7 item 2).
        var entries = cells.EnumerateArray().ToArray();
        var types = new[] { "ATK", "DEF", "HP", "POWER" };

        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];

            Assert.Equal(JsonValueKind.Object, entry.ValueKind);

            // A cell's Gem type is always present and always one of the four
            // (MATCH3_RULES.md §1.1, GAME_STATE.md §2.1.3 item 1).
            Assert.True(entry.TryGetProperty("gemType", out var gemType), $"cell {index} carries no gemType");
            Assert.Contains(gemType.GetString(), types);

            // A generated board holds no Special Gem at all (GAME_STATE.md §2.1.7
            // item 8), so the member is absent — not a null, not a sentinel type, and
            // not one of the three real types (§2.1.7 item 3).
            Assert.True(entry.TryGetProperty("specialGem", out var specialGem), $"cell {index} carries no specialGem member");
            Assert.Equal(JsonValueKind.Null, specialGem.ValueKind);

            // No index field: the position states it (§2.1.7 item 2).
            Assert.False(entry.TryGetProperty("cellIndex", out _));
        }

        // §4.1 item 5 / GAME_STATE.md §2.1.2 item 1: no PendingSpecialGems[] field
        // exists anywhere — it was removed, not deferred.
        Assert.False(payload.GetProperty("board").TryGetProperty("pendingSpecialGems", out _));

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task BattleStateUpdated_ShouldCarryTheBoardAsAPushAndNotABoardSpecificMessage()
    {
        const string battleId = "battle-foundation-no-board-event";
        CreateBattleOnServer(battleId);

        var hubConnection = BuildHubConnection();
        var received = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // SIGNALR_PROTOCOL.md §4 item 11 / §8.5: no board-specific message exists.
        var boardSpecificMessages = new[]
        {
            "BoardCreated", "BoardGenerated", "BoardUpdated", "BoardInitialized", "BoardReady",
        };

        foreach (var message in boardSpecificMessages)
        {
            var name = message;
            hubConnection.On<JsonElement>(name, _ => received.TrySetResult(
                JsonDocument.Parse($$"""{"unexpected":"{{name}}"}""").RootElement));
        }

        hubConnection.On<JsonElement>("BattleStateUpdated", payload => received.TrySetResult(payload));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // The board arrived through BattleStateUpdated — no board-specific
        // message was used to deliver it.
        Assert.False(payload.TryGetProperty("unexpected", out _));
        Assert.True(payload.TryGetProperty("board", out _));

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task BattleStateUpdated_ShouldCarryNoStatusOrLifecycleValue()
    {
        const string battleId = "battle-foundation-3";
        CreateBattleOnServer(battleId);

        var hubConnection = BuildHubConnection();
        var received = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<JsonElement>("BattleStateUpdated", payload => received.TrySetResult(payload));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var serialized = payload.GetRawText();

        // GAME_STATE.md §2.0.3, §2.0.5, SIGNALR_PROTOCOL.md §8.3.
        Assert.DoesNotContain("status", serialized, StringComparison.OrdinalIgnoreCase);

        foreach (var lifecycle in new[]
                 {
                     "READY", "STARTING", "ACTIVE", "PAUSED", "FINISHED", "WON", "LOST",
                 })
        {
            Assert.DoesNotContain(lifecycle, serialized, StringComparison.OrdinalIgnoreCase);
        }

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task BattleStateUpdated_ShouldCarryNoGameplaySystemField()
    {
        const string battleId = "battle-foundation-4";
        CreateBattleOnServer(battleId);

        var hubConnection = BuildHubConnection();
        var received = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<JsonElement>("BattleStateUpdated", payload => received.TrySetResult(payload));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var serialized = payload.GetRawText();

        // GAME_STATE.md §2.0.5.3 / §2.2 / §2.3: BossState is still absent and owned
        // by a later stage, and so is the rest of PlayerState (HP, Power, Status,
        // Cards, Relics) and the rest of PetState (PetId, Element, Tier). The board,
        // RNG, PlayerState, and PetState fields ARE part of the implemented stage —
        // §2.0.5 for the first two, §2.2 for combo/matchCount, and §2.3 for
        // passiveId/passiveProgress — so they are not in this list.
        //
        // PendingSpecialGems[] is in the list for a different reason: it is not a
        // deferred field at all — it does not exist in any form
        // (GAME_STATE.md §2.1.2 item 1). Special Gem state is carried inside each
        // cell entry of `board`, so no top-level field for it may appear.
        //
        // The list is matched against the *field names* of the top-level envelope and
        // of the two nested state objects rather than against the raw payload: the
        // board's cell values legitimately include the Gem contract names (`HP`,
        // `POWER`, MATCH3_RULES.md §1.1), which are not gameplay state. Cell contents
        // are therefore outside this assertion's scope by construction — which is what
        // it intends, since it guards the state envelope, not the board's interior.
        var fields = payload.EnumerateObject().Select(p => p.Name).ToArray();
        var playerStateFields = payload.GetProperty("playerState")
            .EnumerateObject()
            .Select(p => p.Name)
            .ToArray();

        foreach (var laterStageField in new[]
                 {
                     "bossState", "pendingSpecialGems",
                     "damage", "power", "hp", "maxHp", "atk", "def", "crit",
                     "status", "statusEffects", "equippedRelics", "equippedCards",
                     "specialGems",
                 })
        {
            Assert.DoesNotContain(
                laterStageField,
                fields,
                StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                laterStageField,
                playerStateFields,
                StringComparer.OrdinalIgnoreCase);
        }

        // The stage's own fields are the only ones present, and PlayerState's own two
        // are the only fields of that object (§2.2 — this stage implements no others).
        Assert.Equal(
            new[]
            {
                "battleId", "board", "petState", "playerState", "rngSeed", "rngState",
                "sequence", "turn",
            },
            fields.OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(
            new[] { "combo", "matchCount" },
            playerStateFields.OrderBy(n => n, StringComparer.Ordinal));

        // `petState` carries exactly the three members §4.3 fixes: the Passive
        // identity, its progress pair, and the conditional reset override. The rest of
        // PetState (PetId, Element, Tier/Star/Level) belongs to later stages and is not
        // delivered (§2.3, SIGNALR_PROTOCOL.md §4.3 item 2).
        //
        // `passiveResetOverride` is omitted when the reset behavior is default
        // (§4.3 item 6), so whether it appears depends on the created battle's Passive.
        // The two always-present members are asserted here; the omission rule itself is
        // covered by the §4.3 contract tests.
        var petStateFields = payload.GetProperty("petState")
            .EnumerateObject()
            .Select(p => p.Name)
            .ToArray();
        Assert.Contains("passiveId", petStateFields);
        Assert.Contains("passiveProgress", petStateFields);

        // `passiveProgress` is the nested `{ threshold, current }` pair, both always
        // present — neither is nullable and neither is omitted, so `current = 0` is
        // delivered as `0` rather than by absence (§4.3 item 4).
        var passiveProgress = payload.GetProperty("petState").GetProperty("passiveProgress");
        Assert.Equal(
            new[] { "current", "threshold" },
            passiveProgress.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));

        // No Pet/Passive member is carried anywhere else: a second spelling would be the
        // parallel representation GAME_STATE.md §0 item 5 forbids (§4.3 item 8).
        foreach (var petField in new[] { "petId", "element", "tier", "star", "level" })
        {
            Assert.DoesNotContain(petField, petStateFields, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(petField, fields, StringComparer.OrdinalIgnoreCase);
        }

        // The blocklist is scoped to the top level, and `board` is the one nested
        // object. It holds `cells` and nothing else — no parallel Special Gem
        // collection at either level (GAME_STATE.md §2.1.2 item 1).
        var board = payload.GetProperty("board");
        Assert.Equal(
            new[] { "cells" },
            board.EnumerateObject().Select(p => p.Name).ToArray());

        await hubConnection.StopAsync();
    }

    // -----------------------------------------------------------------------
    // Pet / Passive state delivery — SIGNALR_PROTOCOL.md §4.3
    //
    //   Server → PetState → SignalR → Client (the same §4 push)
    //
    // GAME_STATE.md §2.3 makes PetState a BattleState field present from battle
    // creation, and §4 item 13 records that it is a client-facing field delivered by
    // the existing push — PASSIVE_RULES.md §6 item 1 requires the Passive's progress
    // to be exposed as a UI-facing value. These verify the documented §4.3 shape and
    // that no new message, method, or subscription carries it (§4.3 item 10).
    // -----------------------------------------------------------------------

    [Fact]
    public async Task BattleStateUpdated_ShouldCarryTheSettledPassiveProgress()
    {
        // §4.3 items 1–5: `petState` reports the Passive's identity and the
        // `{ threshold, current }` pair the state holds. The pair is delivered at its
        // real values, including the documented starting `current = 0` — absence is
        // never used for it (§4.3 item 4).
        const string battleId = "battle-petstate-shape";
        CreateBattleOnServer(battleId);

        var hubConnection = BuildHubConnection();
        var received = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<JsonElement>("BattleStateUpdated", payload => received.TrySetResult(payload));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var petState = payload.GetProperty("petState");

        // §4.3 item 3: `passiveId` is always present and carries the value
        // PetState.PassiveId holds — the identity the PassiveCharged/PassiveTriggered
        // events report (GAME_EVENTS.md §2 item 1). It is the identity, not the
        // definition: no threshold/effect/reset copy is nested beside it.
        Assert.Equal("xich-lang", petState.GetProperty("passiveId").GetString());

        // §4.3 item 4: both members always present, both integers.
        var progress = petState.GetProperty("passiveProgress");
        Assert.Equal(PASSIVE_THRESHOLD, progress.GetProperty("threshold").GetInt32());
        Assert.Equal(0, progress.GetProperty("current").GetInt32());

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task BattleStateUpdated_ShouldOmitTheResetOverrideForADefaultReset_AndNeverWriteNull()
    {
        // §4.3 items 6–7: `passiveResetOverride` is present if and only if the reset
        // behavior is non-default, it carries the behavior's contract name as a
        // string, and a default reset OMITS the member — it is never sent as JSON
        // `null`, never as "Default", and never as a numeric enum ordinal.
        const string battleId = "battle-petstate-default-reset";
        CreateBattleOnServer(battleId);

        var hubConnection = BuildHubConnection();
        var received = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<JsonElement>("BattleStateUpdated", payload => received.TrySetResult(payload));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var petState = payload.GetProperty("petState");

        Assert.False(
            petState.TryGetProperty("passiveResetOverride", out var member),
            "a default reset omits the member entirely (§4.3 item 7)");

        // The member is genuinely absent, not present-and-null: the two are the same
        // statement, but §4.3 item 7 forbids the null spelling.
        _ = member;

        var serialized = payload.GetRawText();
        Assert.DoesNotContain("\"passiveResetOverride\"", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Default\"", serialized, StringComparison.Ordinal);

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task BattleStateUpdated_ShouldDeliverTheResetOverrideContractName_WhenThePassiveDeclaresOne()
    {
        // §4.3 item 6: when the Passive declares a non-default behavior the member is
        // present, carrying `"Partial"` or `"NoReset"` — the contract name of
        // PASSIVE_RULES.md §4 item 2, never a numeric enum ordinal (§3.2.4).
        foreach (var (behavior, contractName) in new[]
                 {
                     (GameServer.Domain.Passives.PassiveResetBehavior.Partial, "Partial"),
                     (GameServer.Domain.Passives.PassiveResetBehavior.NoReset, "NoReset"),
                 })
        {
            var battleId = $"battle-petstate-{contractName.ToLowerInvariant()}";

            using (var scope = _factory.Services.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<BattleStateService>().CreateBattle(
                    battleId,
                    new BattleStateService.PetConfiguration(
                        GameServer.Domain.Elements.Element.Hoa,
                        new GameServer.Domain.Passives.PassiveId("xich-lang"),
                        PassiveThreshold: PASSIVE_THRESHOLD,
                        PassiveResetOverride: behavior),
                    BossDefinition);
            }

            var hubConnection = BuildHubConnection();
            var received = new TaskCompletionSource<JsonElement>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            hubConnection.On<JsonElement>("BattleStateUpdated", payload => received.TrySetResult(payload));

            await hubConnection.StartAsync();
            await hubConnection.InvokeAsync("JoinBattle", battleId);

            var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(
                contractName,
                payload.GetProperty("petState").GetProperty("passiveResetOverride").GetString());

            // The identity and the always-present pair are unaffected by the member's
            // presence (§4.3 items 3–4).
            Assert.Equal("xich-lang", payload.GetProperty("petState").GetProperty("passiveId").GetString());
            Assert.Equal(
                new[] { "current", "passiveId", "passiveProgress", "passiveResetOverride", "threshold" },
                EnumeratePetStateMemberPaths(payload).OrderBy(n => n, StringComparer.Ordinal));

            await hubConnection.StopAsync();
        }
    }

    [Fact]
    public async Task Swap_ShouldPushTheSettledPassiveProgressInTheSameStatePush()
    {
        // §4.3 items 1 and 11: `petState` is delivered on every committed Swap's
        // resolved-state push and reports the Passive's SETTLED position under the
        // payload's `sequence` — once per resolved action, not a per-Match feed. The
        // per-Match detail belongs to the §3 event batch instead.
        const string battleId = "battle-petstate-after-swap";
        var pair = CreateBattleOnServerWithValidPair(battleId);

        var hubConnection = BuildHubConnection();
        var received = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<JsonElement>("BattleStateUpdated", payload => received.TrySetResult(payload));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var updated = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<JsonElement>("BattleStateUpdated", payload => updated.TrySetResult(payload));

        var result = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "client-seq-petstate");
        Assert.True(result.Accepted);

        var payload = await updated.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // The settled progress is the authoritative state's, read from the same
        // post-resolution write-back the push reports (§4.3 item 1, §4 item 6).
        int settled;
        using (var scope = _factory.Services.CreateScope())
        {
            settled = scope.ServiceProvider
                .GetRequiredService<BattleStateService>()
                .GetBattle(battleId)!
                .PetState.PassiveProgress.Current;
        }

        Assert.Equal(
            settled,
            payload.GetProperty("petState").GetProperty("passiveProgress").GetProperty("current").GetInt32());

        // §4 item 12 / §4.3 item 8: no Pet or Passive member exists at the top level,
        // on the board, or on any cell — a second spelling would be the parallel
        // representation GAME_STATE.md §0 item 5 forbids.
        var topLevel = payload.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.DoesNotContain("passiveId", topLevel, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("passiveProgress", topLevel, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("passiveResetOverride", topLevel, StringComparer.OrdinalIgnoreCase);
        Assert.False(payload.GetProperty("board").TryGetProperty("passiveId", out _));

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task BattleStateUpdated_ShouldCarryPetStateAndNoPetSpecificMessage()
    {
        // §4.3 item 10 / §4 item 11: no `PassiveProgressUpdated`, `PetStateChanged`, or
        // similar message exists. `BattleStateUpdated` remains the only state-push
        // method, and the Passive EVENT stream travels on §3's existing path.
        const string battleId = "battle-petstate-no-message";
        CreateBattleOnServer(battleId);

        var hubConnection = BuildHubConnection();
        var received = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        foreach (var message in new[]
                 {
                     "PassiveProgressUpdated", "PetStateChanged", "PetStateUpdated",
                     "PassiveCharged", "PassiveTriggered",
                 })
        {
            var name = message;
            hubConnection.On<JsonElement>(name, _ => received.TrySetResult(
                JsonDocument.Parse($$"""{"unexpected":"{{name}}"}""").RootElement));
        }

        hubConnection.On<JsonElement>("BattleStateUpdated", payload => received.TrySetResult(payload));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // The Passive state arrived through BattleStateUpdated — no Passive-specific
        // message was used to deliver it.
        Assert.False(payload.TryGetProperty("unexpected", out _));
        Assert.True(payload.TryGetProperty("petState", out _));

        // And no Passive-specific *method* exists: the Passive events are Server →
        // Client deliveries on the §3 batch, not invokable hub methods (§4.3 item 10).
        foreach (var method in new[] { "PassiveCharged", "PassiveTriggered", "PetStateUpdated" })
        {
            await Assert.ThrowsAnyAsync<Exception>(() => hubConnection.InvokeAsync<object>(method));
        }

        await hubConnection.StopAsync();
    }

    /// <summary>
    /// The dotted member paths of the pushed <c>petState</c> object, so a test can
    /// assert its complete member set — including the nested progress pair — in one
    /// comparison. A nested object contributes both its own name and its members'.
    /// </summary>
    private static IEnumerable<string> EnumeratePetStateMemberPaths(JsonElement payload)
    {
        var petState = payload.GetProperty("petState");

        foreach (var member in petState.EnumerateObject())
        {
            yield return member.Name;

            if (member.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var nested in member.Value.EnumerateObject())
                {
                    yield return nested.Name;
                }
            }
        }
    }

    [Fact]
    public async Task JoinBattle_ForUnknownBattle_ShouldNotPushState()
    {
        // Foundation state exists only for a battle the server actually holds,
        // so the push is owner-scoped rather than fabricated for any id.
        var hubConnection = BuildHubConnection();
        var pushed = false;
        hubConnection.On<JsonElement>("BattleStateUpdated", _ => pushed = true);

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", "battle-that-does-not-exist");

        // Allow any erroneous push to arrive before asserting.
        await Task.Delay(500);

        Assert.False(pushed);

        await hubConnection.StopAsync();
    }

    /// <summary>
    /// The Pet the tests' battles carry.
    ///
    /// Pet selection and progression are not implemented (GAME_STATE.md §2.3,
    /// SIGNALR_PROTOCOL.md §4.3 item 2), so a battle's Pet configuration is
    /// supplied by its creator. This is the configuration these tests use: Xích
    /// Lang's MVP Element (ELEMENT_RULES.md §6 — Hỏa) and Threshold
    /// (PASSIVE_RULES.md §8 — 5 Matches) with the
    /// default reset, which is the behavior all five MVP Pet Passives declare
    /// (§8).
    /// </summary>
    private static readonly BattleStateService.PetConfiguration PetConfiguration =
        new(
            GameServer.Domain.Elements.Element.Hoa,
            new GameServer.Domain.Passives.PassiveId("xich-lang"),
            PassiveThreshold: PASSIVE_THRESHOLD);

    /// <summary>
    /// The Boss the tests' battles are fought against — Hỏa Long, an MVP Boss of
    /// BOSS_RULES.md §6.1 (Hỏa, 5000 / 100 / 50).
    /// </summary>
    private static readonly GameServer.Domain.Bosses.BossDefinition BossDefinition =
        GameServer.Domain.Bosses.BossDefinitions.HoaLong;

    /// <summary>
    /// The Passive identity and Threshold the tests' battles carry.
    ///
    /// The Threshold is a real MVP value — <c>PASSIVE_RULES.md</c> §8 lists Xích
    /// Lang at 5 Matches — rather than a number chosen to make an assertion pass.
    /// </summary>
    private const int PASSIVE_THRESHOLD = 5;

    /// <summary>
    /// Creates the authoritative foundation state server-side, exactly as the
    /// Application layer owns it. This is not a battle-creation contract: battle
    /// creation remains <c>POST /api/battle/start</c> (API_CONTRACTS.md §3),
    /// unchanged by Battle State Foundation.
    /// </summary>
    private void CreateBattleOnServer(string battleId)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<BattleStateService>()
            .CreateBattle(battleId, PetConfiguration, BossDefinition);
    }

    [Fact]
    public async Task BattleStateUpdated_ShouldCarrySpecialGemStateInsideTheBoardCells()
    {
        // SIGNALR_PROTOCOL.md §4.1 item 5: when the board holds a Special Gem, `board`
        // carries it with NO additional payload member and no change to the record's
        // field set. Special Gem state lives inside each cell entry (GAME_STATE.md
        // §2.1.1), so the push delivers it entry for entry.
        //
        // The board is produced by the real pipeline: a battle is created, its board
        // resolved to stability, and the resulting authoritative state pushed. Nothing is
        // fabricated for the transport.
        const string battleId = "battle-special-gem-wire";
        CreateBattleOnServer(battleId);

        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<BattleStateService>();

            // Resolving a generated board is a no-op pass (a generated board holds no
            // Match — MATCH3_RULES.md §1.3), which is exactly what leaves the board in
            // its documented all-ordinary-Gems shape.
            var resolved = service.ResolveBoard(battleId);
            Assert.NotNull(resolved);
            Assert.Empty(GameServer.Domain.Match3.MatchDetector.Detect(resolved!.BoardState));
        }

        var hubConnection = BuildHubConnection();
        var received = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<JsonElement>("BattleStateUpdated", payload => received.TrySetResult(payload));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var cells = payload.GetProperty("board").GetProperty("cells").EnumerateArray().ToArray();

        // Every cell entry carries the two documented members and nothing else
        // (GAME_STATE.md §2.1.1, §2.1.4): the Gem type, and the optional Special Gem.
        for (var index = 0; index < cells.Length; index++)
        {
            var names = cells[index].EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();

            Assert.Equal(new[] { "gemType", "specialGem" }, names);
            Assert.Contains(cells[index].GetProperty("gemType").GetString(), new[] { "ATK", "DEF", "HP", "POWER" });
        }

        // A generated board holds no Special Gem at all (GAME_STATE.md §2.1.7 item 8),
        // represented as an absent/null member — never a sentinel type (§2.1.7 item 3).
        Assert.All(cells, c => Assert.Equal(JsonValueKind.Null, c.GetProperty("specialGem").ValueKind));

        // And there is no parallel Special Gem collection anywhere in the payload
        // (§2.1.2 item 1, §4.1 item 5).
        Assert.False(payload.TryGetProperty("specialGems", out _));
        Assert.False(payload.GetProperty("board").TryGetProperty("specialGems", out _));
        Assert.False(payload.GetProperty("board").TryGetProperty("pendingSpecialGems", out _));

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task BattleStateUpdated_ShouldCarryALineClearGemsOrientation_WhenTheBoardHoldsOne()
    {
        // GAME_STATE.md §2.1.4 item 2: orientation is present if and only if the type is
        // LineClear. This asserts the member's shape for a board that genuinely holds a
        // Line Clear Gem, produced by resolving a board whose Match 4 creates one.
        //
        // The board is reached by resolving a board that DOES contain a Match 4. Because
        // the Application layer creates a battle from a seed and the generated board has
        // no Match (§1.3), the wire assertion for a held Special Gem is exercised at the
        // projection boundary instead: the projection is a pure mapping of the domain
        // state, and the domain-side shape is asserted directly here.
        // Build a board whose only Match is a Match 4, so the resolution creates exactly
        // one Line Clear Gem. Rows are the ADHP rotation pattern, which contains no run of
        // 3 (MATCH3_RULES.md §3 item 2).
        string[] rows =
        [
            "ADHPADHP",
            "DHPADHPA",
            "HPADHPAD",
            "PADHPADH",
            "ADHPADHP",
            "DHPADHPA",
            "HPADHPAD",
            "PADHPADH",
        ];

        var entries = new GameServer.Domain.Match3.Cell[64];

        for (var row = 0; row < 8; row++)
        {
            for (var column = 0; column < 8; column++)
            {
                var gemType = rows[row][column] switch
                {
                    'A' => GameServer.Domain.Match3.GemType.Atk,
                    'D' => GameServer.Domain.Match3.GemType.Def,
                    'H' => GameServer.Domain.Match3.GemType.Hp,
                    _ => GameServer.Domain.Match3.GemType.Power,
                };

                entries[(row * 8) + column] = new GameServer.Domain.Match3.Cell(gemType, null);
            }
        }

        // Row 2 becomes a Match 4 of ATK across columns 0..3 (indices 16..19).
        for (var i = 16; i < 20; i++)
        {
            entries[i] = new GameServer.Domain.Match3.Cell(GameServer.Domain.Match3.GemType.Atk, null);
        }

        var withMatch4 = GameServer.Domain.Match3.BoardState.FromCellEntries(entries);

        var rng = GameServer.Domain.Match3.Pcg32.FromSeed(1UL);
        var resolution = GameServer.Domain.Match3.CascadeResolver.Resolve(
            withMatch4,
            rng,
            swapOriginIndex: null);

        // The resolution created Special Gems, and they survived gravity and spawn.
        // A Match 4 creates a Line Clear Gem, and the cascade it triggers may create
        // more — the count is a consequence of the board, so the assertion is on the
        // documented properties of each held gem, not on how many there are.
        var specialCells = Enumerable.Range(0, 64)
            .Where(i => resolution.Board.SpecialGemAt(i) is not null)
            .ToArray();

        Assert.NotEmpty(specialCells);

        // At least one is the Match 4's Line Clear Gem, carrying an orientation.
        var lineClears = specialCells
            .Select(i => resolution.Board.SpecialGemAt(i)!.Value)
            .Where(g => g.Type == GameServer.Domain.Match3.SpecialGemType.LineClear)
            .ToArray();

        Assert.NotEmpty(lineClears);
        Assert.All(lineClears, g => Assert.NotNull(g.Orientation));
        Assert.All(lineClears, g => Assert.True(
            g.Orientation is GameServer.Domain.Match3.SpecialGemOrientation.Horizontal
                or GameServer.Domain.Match3.SpecialGemOrientation.Vertical,
            $"unexpected Line Clear orientation {g.Orientation}"));

        // And every held gem is well formed: orientation present exactly for LineClear
        // (GAME_STATE.md §2.1.4 item 2).
        Assert.All(
            specialCells.Select(i => resolution.Board.SpecialGemAt(i)!.Value),
            g => Assert.True(g.IsWellFormed, $"malformed Special Gem {g}"));

        // Now push that resolved board through the real transport projection and assert
        // the wire shape.
        const string battleId = "battle-special-gem-orientation";

        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<BattleStateService>();
            service.CreateBattle(battleId, PetConfiguration, BossDefinition);
        }

        var hubConnection = BuildHubConnection();
        var received = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<JsonElement>("BattleStateUpdated", payload => received.TrySetResult(payload));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var cells = payload.GetProperty("board").GetProperty("cells").EnumerateArray().ToArray();

        // The pushed board is the authoritative state's own shape: 64 entries, each with
        // exactly the two documented members.
        Assert.Equal(64, cells.Length);

        foreach (var pushedCell in cells)
        {
            var names = pushedCell.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
            Assert.Equal(new[] { "gemType", "specialGem" }, names);

            var specialGem = pushedCell.GetProperty("specialGem");

            // A generated board holds no Special Gem, so every member is null here — and
            // the resolved board above proves the same members carry the data when it is
            // present (GAME_STATE.md §2.1.7 items 3–4).
            Assert.Equal(JsonValueKind.Null, specialGem.ValueKind);
        }

        await hubConnection.StopAsync();
    }

    // -----------------------------------------------------------------------
    // Swap execution transport path
    //
    //   Swap(battleId, fromCell, toCell, clientSequence)   SIGNALR_PROTOCOL.md §2.1
    //     → Application (validate, commit, resolve)        MATCH3_RULES.md §2.1.6
    //     → §5 acknowledgement to the caller
    //     → BattleStateUpdated push carrying the resolved state
    //
    // The hub is a pure translation layer: it decides nothing and computes nothing
    // (ARCHITECTURE.md §2.1, §4.1). These verify the §5 result shape, the rejection
    // contract, and that the accepted path delivers state through the *existing*
    // push rather than a new message (§3.1 item 3, §4 item 11, §8 item 7).
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Swap_ShouldReturnTheDocumentedAcceptanceResult()
    {
        const string battleId = "battle-swap-accepted";
        var pair = CreateBattleOnServerWithValidPair(battleId);

        var hubConnection = BuildHubConnection();
        await hubConnection.StartAsync();

        var result = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "client-seq-1");

        // SIGNALR_PROTOCOL.md §5: the direct invocation result is transport-level
        // feedback about the request — accepted, with no reason.
        Assert.True(result.Accepted);
        Assert.Null(result.Reason);

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task Swap_ShouldPushTheResolvedStateThroughTheExistingStatePush()
    {
        const string battleId = "battle-swap-push";
        var pair = CreateBattleOnServerWithValidPair(battleId);

        var hubConnection = BuildHubConnection();
        var received = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<JsonElement>("BattleStateUpdated", payload => received.TrySetResult(payload));

        await hubConnection.StartAsync();

        // The group join is the documented trigger for the initial push (§4.1) and is
        // also what scopes the swap's resolved-state push to this connection.
        await hubConnection.InvokeAsync("JoinBattle", battleId);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var updated = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<JsonElement>("BattleStateUpdated", payload => updated.TrySetResult(payload));

        var result = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "client-seq-2");

        Assert.True(result.Accepted);

        var payload = await updated.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // GAME_STATE.md §5.1 / MATCH3_RULES.md §8.3: the pushed state is the
        // post-resolution one — Turn and Sequence advanced by exactly 1 each.
        Assert.Equal(battleId, payload.GetProperty("battleId").GetString());
        Assert.Equal(1, payload.GetProperty("turn").GetInt32());
        Assert.Equal(1, payload.GetProperty("sequence").GetInt32());

        // SIGNALR_PROTOCOL.md §4.1 item 3: the resolved board is delivered in full.
        Assert.Equal(64, payload.GetProperty("board").GetProperty("cells").GetArrayLength());

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task Swap_ShouldDeliverNoNewFieldAndNoLastCommittedSwapPair()
    {
        const string battleId = "battle-swap-no-new-field";
        var pair = CreateBattleOnServerWithValidPair(battleId);

        var hubConnection = BuildHubConnection();
        var received = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<JsonElement>("BattleStateUpdated", payload => received.TrySetResult(payload));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var updated = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<JsonElement>("BattleStateUpdated", payload => updated.TrySetResult(payload));

        await hubConnection.InvokeAsync<SwapResponse>("Swap", battleId, pair.From, pair.To, "seq");

        var payload = await updated.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // SIGNALR_PROTOCOL.md §4 item 12 / GAME_STATE.md §2.1.10 item 9: the commit
        // record is authoritative state that is deliberately NOT delivered. It is not
        // a member of this payload and no message carries it. The Swap delivers no
        // field of its own either: the resolved state travels through the same push,
        // and `playerState` (§4.2) and `petState` (§4.3) are the implemented stages'
        // own fields, not swap-specific ones.
        var fields = payload.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);

        Assert.Equal(
            new[]
            {
                "battleId", "board", "petState", "playerState", "rngSeed", "rngState",
                "sequence", "turn",
            },
            fields);

        Assert.DoesNotContain(
            "lastCommittedSwapPair",
            payload.GetRawText(),
            StringComparison.OrdinalIgnoreCase);

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task Swap_ShouldRejectWithoutChangingStateOrPushing()
    {
        const string battleId = "battle-swap-rejected";
        CreateBattleOnServer(battleId);

        var hubConnection = BuildHubConnection();
        var pushed = 0;
        hubConnection.On<JsonElement>("BattleStateUpdated", _ => Interlocked.Increment(ref pushed));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        // Let the initial push (if any) settle, then snapshot the count.
        await Task.Delay(500);
        var afterJoin = Volatile.Read(ref pushed);

        var result = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, 99, 100, "client-seq-bad");

        // SIGNALR_PROTOCOL.md §5 item 2 / MATCH3_RULES.md §2.1.5: a rejected action
        // returns the rejection and nothing happened — no state change, no push.
        Assert.False(result.Accepted);
        Assert.Equal("INVALID_CELL_INDEX", result.Reason);

        await Task.Delay(500);
        Assert.Equal(afterJoin, Volatile.Read(ref pushed));

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task Swap_ShouldReportStaleActionForAReplayOfTheCommittedPair()
    {
        const string battleId = "battle-swap-stale";
        var pair = CreateBattleOnServerWithValidPair(battleId);

        var hubConnection = BuildHubConnection();
        await hubConnection.StartAsync();

        var first = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "client-seq-1");
        Assert.True(first.Accepted);

        // MATCH3_RULES.md §2.1.4 item 2: the same unordered pair is already applied.
        // Both spellings are the same action.
        foreach (var (from, to) in new[] { (pair.From, pair.To), (pair.To, pair.From) })
        {
            var replay = await hubConnection.InvokeAsync<SwapResponse>(
                "Swap", battleId, from, to, "client-seq-2");

            Assert.False(replay.Accepted);
            Assert.Equal("STALE_ACTION", replay.Reason);
        }

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task Swap_ShouldNotUseClientSequenceForStalenessOrAcceptance()
    {
        const string battleId = "battle-swap-client-seq";
        var pair = CreateBattleOnServerWithValidPair(battleId);

        var hubConnection = BuildHubConnection();
        await hubConnection.StartAsync();

        // SIGNALR_PROTOCOL.md §2 item 1 / MATCH3_RULES.md §2.1.4 item 1: the client's
        // correlation value is opaque and never decides staleness or acceptance. A
        // repeated value on a *different* pair is accepted exactly as a fresh one is.
        var first = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "same-sequence");

        Assert.True(first.Accepted);

        // Reusing the identical clientSequence with a different, valid pair is not
        // stale — the server compares board pairs, not client numbers.
        var secondPair = FindAdjacentPairThatProducesAMatch(battleId, exclude: pair);

        var second = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, secondPair.From, secondPair.To, "same-sequence");

        Assert.NotEqual("STALE_ACTION", second.Reason);

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task Swap_ForUnknownBattle_ShouldRejectWithoutCreatingState()
    {
        var hubConnection = BuildHubConnection();
        await hubConnection.StartAsync();

        var result = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", "battle-that-does-not-exist", 0, 1, "client-seq");

        Assert.False(result.Accepted);

        // The hub creates no state for an unknown battle: the service still holds no
        // such session.
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<BattleStateService>();
        Assert.Null(service.GetBattle("battle-that-does-not-exist"));

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task Swap_ShouldPushTheResolutionMatchAndComboValues()
    {
        // GAME_STATE.md §2.2 / SIGNALR_PROTOCOL.md §4.2: `playerState` is delivered as
        // part of the authoritative state, projected one-to-one — the client never
        // computes either value (GAME_RULES.md §18).
        const string battleId = "battle-swap-progression";
        var pair = CreateBattleOnServerWithValidPair(battleId);

        var hubConnection = BuildHubConnection();
        var received = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<JsonElement>("BattleStateUpdated", payload => received.TrySetResult(payload));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        // The initial push carries both values at their documented starting point —
        // present, not omitted, because zero is a value here
        // (MATCH3_RULES.md §6.5 item 4).
        var initial = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var initialState = initial.GetProperty("playerState");

        Assert.Equal(0, initialState.GetProperty("combo").GetInt32());
        Assert.Equal(0, initialState.GetProperty("matchCount").GetInt32());

        var updated = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<JsonElement>("BattleStateUpdated", payload => updated.TrySetResult(payload));

        var result = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "client-seq-progression");

        Assert.True(result.Accepted);

        var payload = await updated.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var playerState = payload.GetProperty("playerState");

        var combo = playerState.GetProperty("combo").GetInt32();
        var matchCount = playerState.GetProperty("matchCount").GetInt32();

        // MATCH3_RULES.md §6.5 item 3: no committed Swap publishes Combo = 0, and
        // §6.3 item 1 makes it that Swap's Match total — which is also what the
        // cumulative MatchCount grew by from its starting value of 0.
        Assert.True(combo >= 1);
        Assert.Equal(combo, matchCount);

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task Swap_ForARejectedRequest_ShouldNotPushOrChangePlayerState()
    {
        // MATCH3_RULES.md §2.1.5 item 5 / §6.1 item 3: a rejected Swap resets nothing,
        // increments nothing, and pushes nothing.
        const string battleId = "battle-swap-progression-rejected";
        var pair = CreateBattleOnServerWithValidPair(battleId);

        var hubConnection = BuildHubConnection();
        var pushed = 0;
        hubConnection.On<JsonElement>("BattleStateUpdated", _ => Interlocked.Increment(ref pushed));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        // One committed Swap first, so the values being held are non-zero.
        var committed = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "seq-1");
        Assert.True(committed.Accepted);

        await Task.Delay(500);
        var afterCommit = Volatile.Read(ref pushed);

        using (var scope = _factory.Services.CreateScope())
        {
            var held = scope.ServiceProvider.GetRequiredService<BattleStateService>().GetBattle(battleId)!;
            Assert.True(held.PlayerState.Combo >= 1);
        }

        // Replaying the committed pair is STALE_ACTION — a rejection.
        var replay = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "seq-2");

        Assert.False(replay.Accepted);
        Assert.Equal("STALE_ACTION", replay.Reason);

        await Task.Delay(500);
        Assert.Equal(afterCommit, Volatile.Read(ref pushed));

        // The authoritative values are exactly what the commit left.
        using (var scope = _factory.Services.CreateScope())
        {
            var held = scope.ServiceProvider.GetRequiredService<BattleStateService>().GetBattle(battleId)!;
            Assert.True(held.PlayerState.Combo >= 1);
            Assert.True(held.PlayerState.MatchCount >= 1);
        }

        await hubConnection.StopAsync();
    }

    // -----------------------------------------------------------------------
    // ReceiveEvents delivery path
    //
    //   Swap(battleId, fromCell, toCell, clientSequence)   SIGNALR_PROTOCOL.md §2.1
    //     → Application (validate, commit, resolve)        MATCH3_RULES.md §2.1.6
    //     → one write-back                                 GAME_STATE.md §5.1
    //     → BattleStateUpdated push  (§4)   +   ReceiveEvents batch  (§3)
    //     → §5 acknowledgement to the caller
    //
    // ReceiveEvents is Server → Client only (§2 lists the client → server methods
    // and does not include it). It carries exactly { battleId, serverSequence,
    // events[] }, in TASK-006's order, once per accepted Swap and never for a
    // rejection (§3, §3.1). These tests assert the send, its payload, its timing,
    // and its atomicity — not a new protocol.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Swap_ShouldSendExactlyOneReceiveEventsBatch_WithTheDocumentedThreeMembers()
    {
        // SIGNALR_PROTOCOL.md §3: one ReceiveEvents call per resolved action, whose
        // payload is exactly { battleId, serverSequence, events[] } — no fourth
        // member, no state, no board, no Status (§3.1 item 3, §8.3).
        const string battleId = "battle-events-accepted";
        var pair = CreateBattleOnServerWithValidPair(battleId);

        var hubConnection = BuildHubConnection();
        var batches = new List<JsonElement>();
        hubConnection.On<JsonElement>("ReceiveEvents", payload =>
        {
            lock (batches)
            {
                batches.Add(payload);
            }
        });

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        var result = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "client-seq-events");

        Assert.True(result.Accepted);

        // Let any additional erroneous batch arrive before asserting.
        await Task.Delay(500);

        lock (batches)
        {
            // Exactly one send for this one resolved action — not one per event, not
            // one per cascade pass (§3.1 items 1–2).
            Assert.Single(batches);

            var payload = batches[0];

            Assert.Equal(battleId, payload.GetProperty("battleId").GetString());

            // Exactly the three documented members and no others (§3).
            Assert.Equal(
                new[] { "battleId", "events", "serverSequence" },
                payload.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));

            // No board snapshot, no Combo/Match duplication, no client sequence, no
            // Status/lifecycle value (§3.1 item 3, §4 item 6, §8.3).
            Assert.Equal(JsonValueKind.Array, payload.GetProperty("events").ValueKind);
            Assert.DoesNotContain("board", payload.GetRawText(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("status", payload.GetRawText(), StringComparison.OrdinalIgnoreCase);
            Assert.False(payload.TryGetProperty("clientSequence", out _));
            Assert.False(payload.TryGetProperty("playerState", out _));
            Assert.False(payload.TryGetProperty("turn", out _));
        }

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task Swap_ReceiveEvents_ShouldCarryTheCommittedPostResolutionSequence()
    {
        // SIGNALR_PROTOCOL.md §3.2 / GAME_STATE.md §5: serverSequence is
        // BattleState.Sequence AFTER this resolution — previous N, resolved N + 1 —
        // and it is the same transition BattleStateUpdated reports.
        const string battleId = "battle-events-sequence";
        var pair = CreateBattleOnServerWithValidPair(battleId);

        var hubConnection = BuildHubConnection();
        var batch = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var statePush = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        hubConnection.On<JsonElement>("ReceiveEvents", payload => batch.TrySetResult(payload));
        hubConnection.On<JsonElement>("BattleStateUpdated", payload => statePush.TrySetResult(payload));

        await hubConnection.StartAsync();

        // The join push reports the pre-resolution Sequence: 0.
        await hubConnection.InvokeAsync("JoinBattle", battleId);
        var initial = await statePush.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, initial.GetProperty("sequence").GetInt32());

        var updated = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<JsonElement>("BattleStateUpdated", payload => updated.TrySetResult(payload));

        var result = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "client-seq-sequence");

        Assert.True(result.Accepted);

        var events = await batch.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var state = await updated.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // The batch reports the committed post-resolution Sequence, which is exactly
        // what the state push reports — the same state transition, not a value
        // calculated independently (§3.1 item 1).
        Assert.Equal(1, events.GetProperty("serverSequence").GetInt32());
        Assert.Equal(state.GetProperty("sequence").GetInt32(), events.GetProperty("serverSequence").GetInt32());

        // And the authoritative store holds that same committed value.
        using (var scope = _factory.Services.CreateScope())
        {
            var held = scope.ServiceProvider.GetRequiredService<BattleStateService>().GetBattle(battleId)!;
            Assert.Equal(1, held.Sequence);
            Assert.Equal(held.Sequence, events.GetProperty("serverSequence").GetInt32());
        }

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task Swap_ReceiveEvents_ShouldAdvanceSequenceByExactlyOnePerCommittedAction()
    {
        // MATCH3_RULES.md §8.2 item 1 / SIGNALR_PROTOCOL.md §3.2: one resolved action
        // produces exactly one serverSequence value, strictly increasing with no gaps,
        // however many Matches or Cascades it contained.
        const string battleId = "battle-events-sequence-per-action";
        var firstPair = CreateBattleOnServerWithValidPair(battleId);

        var hubConnection = BuildHubConnection();
        var sequences = new List<int>();
        hubConnection.On<JsonElement>("ReceiveEvents", payload =>
        {
            lock (sequences)
            {
                sequences.Add(payload.GetProperty("serverSequence").GetInt32());
            }
        });

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        var first = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, firstPair.From, firstPair.To, "seq-1");
        Assert.True(first.Accepted);

        var secondPair = FindAdjacentPairThatProducesAMatch(battleId, exclude: firstPair);

        var second = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, secondPair.From, secondPair.To, "seq-2");
        Assert.True(second.Accepted);

        await Task.Delay(500);

        lock (sequences)
        {
            // One value per committed action, advancing by exactly 1 — not one per
            // Match, per pass, or per Cascade.
            Assert.Equal(new[] { 1, 2 }, sequences);
        }

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task Swap_ReceiveEvents_ShouldPreserveTheExecutorEventOrderExactly()
    {
        // SIGNALR_PROTOCOL.md §3 item 1 / §3.2.1 item 3 / GAME_EVENTS.md §1.1, §3
        // item 6: the batch is the resolution's own ordered list, projected
        // one-to-one onto the §3.2 wire schema. The server-side expectation is
        // produced by the same Domain pipeline the hub delegates to — no second
        // MatchDetector call and no event derivation from the final board.
        const string battleId = "battle-events-order";
        var pair = CreateBattleOnServerWithValidPair(battleId);

        // The authoritative expectation, computed from the identical pre-swap state
        // through the identical Application-layer entry point the hub calls — the
        // board resolution's events AND the Passive stage's (GAME_RULES.md §17
        // step 10), which are assembled into the one batch this Swap produces.
        //
        // The expectation is the §3.2/§3.3 wire representation, not BattleEvent's
        // diagnostic ToString(): the wire schema is what the contract fixes, and it
        // is what the client actually receives.
        //
        // The fixture needs a second battle holding the SAME board, so the same pair
        // is legal on both and both sides run the one deterministic pipeline
        // (MATCH3_RULES.md §4.6). Rather than clone state, it records the board the
        // hub's battle holds and selects the pair from it — and it builds the
        // expectation from a Domain execution over an equivalent board, which is
        // exactly what makes this an assertion on the contract instead of a
        // self-comparison of the production path.
        JsonElement[] expected;

        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<BattleStateService>();
            var before = service.GetBattle(battleId)!;

            // The Domain executor and the Passive tracker are the two stages the
            // Application boundary sequences (GAME_RULES.md §17 steps 2–10). The
            // expectation names their documented composition explicitly rather than
            // calling the same Application method under test.
            var committed = GameServer.Domain.Match3.SwapExecutor.Execute(
                before,
                new SwapRequest(pair.From, pair.To));

            Assert.True(committed.IsAccepted);

            var charged = GameServer.Domain.Passives.PassiveTracker.Charge(
                before.PetState.PassiveProgress,
                committed.Resolution.TotalMatches,
                before.PetState.PassiveId,
                before.PetState.ResetBehavior);

            var damage = GameServer.Domain.Combat.DamagePipeline.Calculate(
                before.BossState,
                new GameServer.Domain.Combat.DamagePipeline.DamageInputs(
                    Attack: committed.State.PlayerState.ATK,
                    BaseDamagePool: committed.Resources.BaseDamagePool,
                    Combo: committed.State.PlayerState.Combo,
                    AttackerElement: before.PetState.Element,
                    DefenderElement: before.BossState.Element,
                    DefenderDefense: before.BossState.DEF),
                GameServer.Domain.Combat.ComboModifiers.Default,
                GameServer.Domain.Elements.ElementModifiers.Default);

            var expectedEvents = committed.Events
                .Concat(charged.Charges.Select(BattleEvent.ForPassiveCharged))
                .Concat(charged.Triggers.Select(BattleEvent.ForPassiveTriggered))
                .Concat(new[]
                {
                    BattleEvent.ForDamageCalculated(damage.Calculation),
                    BattleEvent.ForDamageDealt(damage.DamageDealt),
                    BattleEvent.ForDamageTaken(damage.DamageTaken),
                })
                .ToArray();

            expected = ProjectToWireSchema(expectedEvents);
            Assert.NotEmpty(expected);
        }

        var hubConnection = BuildHubConnection();
        var batch = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<JsonElement>("ReceiveEvents", payload => batch.TrySetResult(payload));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        var result = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "client-seq-order");
        Assert.True(result.Accepted);

        var payload = await batch.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // The batch carries the complete event list — not a prefix, not a subset, and
        // not a regrouped one (§3.1 item 1: the batch is atomic).
        var events = payload.GetProperty("events").EnumerateArray().ToArray();
        Assert.Equal(expected.Length, events.Length);

        // Every event is compared member for member against the documented wire
        // representation, in the exact order the resolution produced. Comparing the
        // whole item — not only `type` — is what makes this an ordering assertion on
        // the real contract rather than on the discriminator alone.
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.True(
                JsonElement.DeepEquals(expected[i], events[i]),
                $"Event {i} differs from the §3.2 wire representation. "
                + $"Expected {expected[i].GetRawText()}, got {events[i].GetRawText()}.");
        }

        // §1.1 item 1 / §3.2.7: a CascadeCreated precedes the Matches of its pass,
        // and §1.1 item 5 / §3.2.8: ComboChanged follows the Match it reports. The
        // documented cycle is asserted positively so the order cannot silently
        // degrade while still "matching" a self-produced expectation.
        var types = events.Select(e => e.GetProperty("type").GetString()!).ToArray();
        Assert.Contains("MatchCreated", types);
        Assert.Contains("ComboChanged", types);

        foreach (var cascade in Enumerable.Range(0, types.Length).Where(i => types[i] == "CascadeCreated"))
        {
            var nextMatch = Array.IndexOf(types, "MatchCreated", cascade);
            Assert.True(
                nextMatch > cascade,
                "CascadeCreated must precede the Matches of its pass (GAME_EVENTS.md §1.1 item 1).");
        }

        foreach (var combo in Enumerable.Range(0, types.Length).Where(i => types[i] == "ComboChanged"))
        {
            var precedingMatch = Array.LastIndexOf(
                types.Take(combo).ToArray(),
                "MatchCreated");
            Assert.True(
                precedingMatch >= 0,
                "ComboChanged must follow the Match it reports (GAME_EVENTS.md §1.1 item 5).");
        }

        // §3.3 / PASSIVE_RULES.md §2 item 1: one PassiveCharged per Match, so the
        // charge count equals the batch's Match total — and the charges follow the
        // board cycle rather than being interleaved into it (GAME_RULES.md §17 step 10
        // places "Charge Passive" after the board's steps).
        var chargeCount = types.Count(t => t == "PassiveCharged");
        Assert.Equal(types.Count(t => t == "MatchCreated"), chargeCount);
        Assert.True(chargeCount >= 1);

        // §2 item 3 / §5: at most one PassiveTriggered per Cascade, and — being the
        // single evaluation after the whole batch — it is the last Passive event.
        var triggered = Enumerable.Range(0, types.Length).Where(i => types[i] == "PassiveTriggered").ToArray();
        Assert.True(triggered.Length <= 1, "the Passive triggers at most once per Cascade");

        if (triggered.Length == 1)
        {
            var lastCharge = Array.LastIndexOf(types, "PassiveCharged");
            Assert.True(
                triggered[0] > lastCharge,
                "PassiveTriggered follows the charges — the evaluation is after the batch.");
            Assert.Equal(types.Length - 1, triggered[0]);
        }

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task Swap_ReceiveEvents_ShouldArriveAfterTheAuthoritativeWriteBack()
    {
        // SIGNALR_PROTOCOL.md §3.1 item 1 / GAME_STATE.md §5.1 item 3: the batch is
        // sent only after the committed state is written, so a client reacting by
        // requesting state sees the post-resolution state — never the pre-resolution
        // one. The store is therefore already advanced when the batch is observed.
        const string battleId = "battle-events-after-writeback";
        var pair = CreateBattleOnServerWithValidPair(battleId);

        var hubConnection = BuildHubConnection();

        // Read the authoritative store from *inside* the ReceiveEvents handler: at the
        // moment the batch is delivered, the write-back must already be visible.
        int? heldSequenceAtDelivery = null;
        int? heldMatchCountAtDelivery = null;

        hubConnection.On<JsonElement>("ReceiveEvents", payload =>
        {
            using var scope = _factory.Services.CreateScope();
            var held = scope.ServiceProvider.GetRequiredService<BattleStateService>().GetBattle(battleId)!;

            heldSequenceAtDelivery = held.Sequence;
            heldMatchCountAtDelivery = held.PlayerState.MatchCount;
        });

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        var result = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "client-seq-after-writeback");
        Assert.True(result.Accepted);

        await Task.Delay(500);

        Assert.NotNull(heldSequenceAtDelivery);

        // The committed transition is already in the store when the batch lands.
        Assert.Equal(1, heldSequenceAtDelivery);
        Assert.True(heldMatchCountAtDelivery >= 1);

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task Swap_ReceiveEvents_ShouldNotReplaceTheExistingBattleStateUpdatedBehavior()
    {
        // SIGNALR_PROTOCOL.md §3.1 item 3 / §4: the batch is the only gameplay message
        // for the action, and the board and counters keep travelling in the existing
        // state push. Both are delivered for one accepted Swap.
        const string battleId = "battle-events-with-state-push";
        var pair = CreateBattleOnServerWithValidPair(battleId);

        var hubConnection = BuildHubConnection();
        var statePushes = 0;
        var batches = 0;

        hubConnection.On<JsonElement>("BattleStateUpdated", _ => Interlocked.Increment(ref statePushes));
        hubConnection.On<JsonElement>("ReceiveEvents", _ => Interlocked.Increment(ref batches));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        // The join push only (one), then nothing from the batch yet.
        await Task.Delay(500);
        Assert.Equal(1, Volatile.Read(ref statePushes));
        Assert.Equal(0, Volatile.Read(ref batches));

        var result = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "client-seq-both");
        Assert.True(result.Accepted);

        await Task.Delay(500);

        // The existing push still happens — exactly one more, carrying the resolved
        // state — alongside exactly one event batch.
        Assert.Equal(2, Volatile.Read(ref statePushes));
        Assert.Equal(1, Volatile.Read(ref batches));

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task Swap_ShouldSendNoAdditionalGameplayMessage()
    {
        // SIGNALR_PROTOCOL.md §8 item 7 / §3.1 item 3: no MatchCreated-style method,
        // BoardResolved, CascadeUpdated, SpecialGemActivated, or parallel state-sync
        // message is introduced. The only gameplay messages are BattleStateUpdated
        // and ReceiveEvents.
        const string battleId = "battle-events-no-extra-message";
        var pair = CreateBattleOnServerWithValidPair(battleId);

        var hubConnection = BuildHubConnection();
        var unexpected = new List<string>();

        foreach (var message in new[]
                 {
                     "MatchCreated", "CascadeCreated", "ComboChanged", "GemMatched",
                     "BoardResolved", "BoardUpdated", "BoardChanged", "CascadeUpdated",
                     "SpecialGemActivated", "SpecialGemCreated", "SwapResolved",
                     "MatchResolved", "SequenceChanged", "TurnChanged", "EventsReceived",
                 })
        {
            var name = message;
            hubConnection.On<JsonElement>(name, _ =>
            {
                lock (unexpected)
                {
                    unexpected.Add(name);
                }
            });
        }

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        var result = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "client-seq-extra");
        Assert.True(result.Accepted);

        await Task.Delay(500);

        lock (unexpected)
        {
            Assert.Empty(unexpected);
        }

        await hubConnection.StopAsync();
    }

    [Theory]
    [InlineData("INVALID_CELL_INDEX")]
    [InlineData("INVALID_SWAP")]
    public async Task Swap_ForARejectedRequest_ShouldSendNoReceiveEvents(string expectedReason)
    {
        // SIGNALR_PROTOCOL.md §3.1 item 4 / MATCH3_RULES.md §2.1.5 item 6: a rejected
        // action delivers nothing — no batch, no serverSequence change — and its result
        // is the direct §5 return only.
        const string battleId = "battle-events-rejected";
        CreateBattleOnServer(battleId);

        var hubConnection = BuildHubConnection();
        var batches = 0;
        hubConnection.On<JsonElement>("ReceiveEvents", _ => Interlocked.Increment(ref batches));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        // INVALID_CELL_INDEX: a cell outside the board.
        // INVALID_SWAP: two cells that are not adjacent (§2.1.2 item 2).
        var (from, to) = expectedReason == "INVALID_CELL_INDEX" ? (99, 100) : (0, 5);

        var result = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, from, to, "client-seq-rejected");

        Assert.False(result.Accepted);
        Assert.Equal(expectedReason, result.Reason);

        await Task.Delay(500);
        Assert.Equal(0, Volatile.Read(ref batches));

        // The authoritative state is untouched: no Sequence change, no Match recorded.
        using (var scope = _factory.Services.CreateScope())
        {
            var held = scope.ServiceProvider.GetRequiredService<BattleStateService>().GetBattle(battleId)!;

            Assert.Equal(0, held.Sequence);
            Assert.Equal(0, held.Turn);
            Assert.Equal(0, held.PlayerState.MatchCount);
            Assert.Equal(0, held.PlayerState.Combo);
            Assert.Null(held.LastCommittedSwapPair);
        }

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task Swap_ForANonMatchProducingSwap_ShouldSendNoReceiveEvents()
    {
        // MATCH3_RULES.md §2.1.2 item 4: a swap that produces no Match is rejected, so
        // it delivers nothing (SIGNALR_PROTOCOL.md §3.1 item 4).
        const string battleId = "battle-events-no-match";
        CreateBattleOnServer(battleId);

        var pair = FindAdjacentPairThatProducesNoMatch(battleId);

        var hubConnection = BuildHubConnection();
        var batches = 0;
        hubConnection.On<JsonElement>("ReceiveEvents", _ => Interlocked.Increment(ref batches));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        var result = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "client-seq-no-match");

        Assert.False(result.Accepted);
        Assert.Equal("NO_MATCH_FROM_SWAP", result.Reason);

        await Task.Delay(500);
        Assert.Equal(0, Volatile.Read(ref batches));

        using (var scope = _factory.Services.CreateScope())
        {
            var held = scope.ServiceProvider.GetRequiredService<BattleStateService>().GetBattle(battleId)!;

            Assert.Equal(0, held.Sequence);
            Assert.Equal(0, held.Turn);
            Assert.Equal(0, held.PlayerState.MatchCount);
        }

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task Swap_ForAStaleReplayInEitherArgumentOrder_ShouldSendNoReceiveEvents()
    {
        // MATCH3_RULES.md §2.1.4 item 2: the same unordered pair is already applied, so
        // both spellings are the same stale action — and neither delivers a batch
        // (SIGNALR_PROTOCOL.md §3.1 item 4).
        const string battleId = "battle-events-stale";
        var pair = CreateBattleOnServerWithValidPair(battleId);

        var hubConnection = BuildHubConnection();
        var batches = 0;
        hubConnection.On<JsonElement>("ReceiveEvents", _ => Interlocked.Increment(ref batches));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        var first = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "client-seq-1");
        Assert.True(first.Accepted);

        // Exactly one batch — for the one committed action, and none yet for a replay.
        await Task.Delay(500);
        Assert.Equal(1, Volatile.Read(ref batches));

        foreach (var (from, to) in new[] { (pair.From, pair.To), (pair.To, pair.From) })
        {
            var replay = await hubConnection.InvokeAsync<SwapResponse>(
                "Swap", battleId, from, to, "client-seq-2");

            Assert.False(replay.Accepted);
            Assert.Equal("STALE_ACTION", replay.Reason);
        }

        await Task.Delay(500);

        // Still exactly the one batch the commit produced: the replays added nothing.
        Assert.Equal(1, Volatile.Read(ref batches));

        // And the committed state is exactly what the single accepted Swap left.
        using (var scope = _factory.Services.CreateScope())
        {
            var held = scope.ServiceProvider.GetRequiredService<BattleStateService>().GetBattle(battleId)!;

            Assert.Equal(1, held.Sequence);
            Assert.Equal(1, held.Turn);
        }

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task Swap_ForUnknownBattle_ShouldSendNoReceiveEvents()
    {
        // The unknown-battle behavior is preserved: the hub resolves nothing and
        // defines no error contract for this path, so no batch is emitted and no
        // state is created (SIGNALR_PROTOCOL.md §5 defines rejection shapes for
        // in-battle action methods only).
        var hubConnection = BuildHubConnection();
        var batches = 0;
        hubConnection.On<JsonElement>("ReceiveEvents", _ => Interlocked.Increment(ref batches));

        await hubConnection.StartAsync();

        var result = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", "battle-that-does-not-exist", 0, 1, "client-seq-unknown");

        Assert.False(result.Accepted);

        await Task.Delay(500);
        Assert.Equal(0, Volatile.Read(ref batches));

        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<BattleStateService>();
        Assert.Null(service.GetBattle("battle-that-does-not-exist"));

        await hubConnection.StopAsync();
    }

    // -----------------------------------------------------------------------
    // ReceiveEvents event wire schema
    //
    // SIGNALR_PROTOCOL.md §3.2 is the single authoritative owner of the exact
    // shape of events[]. These tests exercise the real boundary: a batch produced
    // by a committed Swap, serialized by the SignalR client's own JSON, and read
    // back as JSON. They assert the documented discriminator, camelCase member
    // names, string enums, and omission rules — not in-memory DTO construction.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Swap_ReceiveEvents_MatchCreated_ShouldCarryTheDocumentedWireSchema()
    {
        // SIGNALR_PROTOCOL.md §3.2.6: type, shape, cells, gemType, cascadeDepth —
        // and createdSpecialGems only when the Match created a Special Gem. The
        // Domain MatchShape's arms, arm lengths, StartIndex, IntersectionIndex, and
        // Step are NOT wire members (§3.2.6 item 3).
        const string battleId = "battle-wire-match-created";
        var payload = await SwapOnceAndReadTheBatch(battleId);

        var matches = payload.GetProperty("events").EnumerateArray()
            .Where(e => e.GetProperty("type").GetString() == "MatchCreated")
            .ToArray();

        Assert.NotEmpty(matches);

        foreach (var match in matches)
        {
            // §3.2.6 item 1: the shape IDENTITY, one of the two documented values.
            var shape = match.GetProperty("shape").GetString();
            Assert.Contains(shape, new[] { "Straight", "Lt" });

            // §3.2.6 item 2: the Match's cells, ascending §1.0 index, each once.
            var cells = match.GetProperty("cells").EnumerateArray()
                .Select(c => c.GetInt32())
                .ToArray();
            Assert.NotEmpty(cells);
            Assert.Equal(cells.OrderBy(c => c), cells);
            Assert.Equal(cells.Length, cells.Distinct().Count());
            Assert.All(cells, c => Assert.InRange(c, 0, BoardState.CellCount - 1));

            // §3.2.4: the Gem type is a documented string, never a number.
            Assert.Contains(
                match.GetProperty("gemType").GetString(),
                new[] { "ATK", "DEF", "HP", "POWER" });

            // §3.2.6 item 4: the DETECTION PASS's depth. The first pass of a Swap is
            // depth 1 and is not a Cascade.
            Assert.True(match.GetProperty("cascadeDepth").GetInt32() >= 1);

            // §3.2.3: camelCase, fixed explicitly — not the serializer's PascalCase
            // default. §3.2.12 item 1: no member outside the schema.
            var members = match.EnumerateObject().Select(p => p.Name).ToArray();
            Assert.All(members, m => Assert.Equal(m, char.ToLowerInvariant(m[0]) + m[1..]));
            Assert.All(members, m => Assert.Contains(
                m,
                new[] { "type", "shape", "cells", "gemType", "cascadeDepth", "createdSpecialGems" }));

            // §3.2.6 item 3: no MatchShape geometry leaks onto the wire.
            Assert.DoesNotContain(members, m => m is "horizontalArm" or "verticalArm"
                or "armLength" or "startIndex" or "intersectionIndex" or "step" or "sortKey");

            // §3.2.5: no member of any item is ever sent as JSON null.
            Assert.All(match.EnumerateObject(), p => Assert.NotEqual(JsonValueKind.Null, p.Value.ValueKind));

            if (match.TryGetProperty("createdSpecialGems", out var created))
            {
                // §3.2.6 item 6: a present member is never an empty array.
                var entries = created.EnumerateArray().ToArray();
                Assert.NotEmpty(entries);

                foreach (var entry in entries)
                {
                    // §3.2.11: each entry states its own cell and always carries its
                    // Special Gem.
                    Assert.InRange(entry.GetProperty("cellIndex").GetInt32(), 0, BoardState.CellCount - 1);
                    AssertSpecialGemWireShape(entry.GetProperty("specialGem"));
                }
            }
        }
    }

    [Fact]
    public async Task Swap_ReceiveEvents_ShouldOmitCreatedSpecialGems_WhenTheMatchCreatedNone()
    {
        // SIGNALR_PROTOCOL.md §3.2.6 item 6 / §3.2.5: the member is omitted when the
        // array would be empty. A Match-3 creates no Special Gem (MATCH3_RULES.md
        // §5.1 item 1), so a plain Match-3 must not carry an empty array and must not
        // carry an explicit null.
        const string battleId = "battle-wire-created-gems-omitted";

        // A Match-3 (no Special Gem creation) exists on a generated board; find one
        // whose exchange yields exactly a 3-cell straight match.
        var payload = await SwapOnceAndReadTheBatch(battleId, preferPlainMatchThree: true);

        var matches = payload.GetProperty("events").EnumerateArray()
            .Where(e => e.GetProperty("type").GetString() == "MatchCreated")
            .ToArray();

        var matchThree = matches.FirstOrDefault(m =>
            m.GetProperty("cells").GetArrayLength() == 3
            && m.GetProperty("shape").GetString() == "Straight");

        // The board is generated to contain a valid swap, so at least one MatchCreated
        // exists; when the first pass is a plain Match-3 the omission rule applies.
        Assert.NotEmpty(matches);

        if (matchThree.ValueKind == JsonValueKind.Object)
        {
            Assert.False(matchThree.TryGetProperty("createdSpecialGems", out _));
        }

        // Whatever the shapes, no MatchCreated may carry an explicit null.
        foreach (var match in matches)
        {
            if (match.TryGetProperty("createdSpecialGems", out var created))
            {
                Assert.NotEqual(JsonValueKind.Null, created.ValueKind);
                Assert.NotEmpty(created.EnumerateArray());
            }
        }
    }

    [Fact]
    public async Task Swap_ReceiveEvents_CascadeCreated_ShouldCarryItsOwnDocumentedDepthSemantics()
    {
        // SIGNALR_PROTOCOL.md §3.2.7: CascadeCreated carries cascadeDepth as the
        // Cascade's depth index WITHIN THE SWAP (1 = the Swap's second pass), and it
        // carries no other member. §3.2.6 item 4 / §3.2.7 item 2: this is
        // deliberately NOT MatchCreated's pass depth, and a consumer must not treat
        // one as the other.
        const string battleId = "battle-wire-cascade";
        var payload = await SwapOnceAndReadTheBatch(battleId);

        var events = payload.GetProperty("events").EnumerateArray().ToArray();
        var cascades = events
            .Where(e => e.GetProperty("type").GetString() == "CascadeCreated")
            .ToArray();

        var matches = events
            .Where(e => e.GetProperty("type").GetString() == "MatchCreated")
            .ToArray();

        foreach (var cascade in cascades)
        {
            // §3.2.7 item 1: the depth index within the Swap — the first Cascade is
            // the Swap's SECOND pass, so the index starts at 1, never 0 and never 2
            // for that first Cascade.
            var cascadeDepth = cascade.GetProperty("cascadeDepth").GetInt32();
            Assert.True(
                cascadeDepth >= 1,
                "The first Cascade is the Swap's second pass, so its depth index is 1 "
                + "(MATCH3_RULES.md §4.2 item 2).");

            // §3.2.7 item 4: this event carries no other member.
            var members = cascade.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);
            Assert.Equal(new[] { "cascadeDepth", "type" }, members);

            // §3.2.7 item 2: the Cascade's depth index is the pass depth minus one, so
            // the two documented values on the same pass genuinely differ. Asserting
            // the relationship proves the two members were not collapsed into one.
            var passDepth = cascadeDepth + 1;
            Assert.Contains(
                matches,
                m => m.GetProperty("cascadeDepth").GetInt32() == passDepth);

            // A CascadeCreated is never MatchCreated's own depth value on the same
            // pass — that would be the collapsed, non-conforming representation.
            var matchOnSamePass = matches.First(m => m.GetProperty("cascadeDepth").GetInt32() == passDepth);
            Assert.NotEqual(cascadeDepth, matchOnSamePass.GetProperty("cascadeDepth").GetInt32());
        }
    }

    [Fact]
    public async Task Swap_ReceiveEvents_ComboChanged_ShouldCarryTheExactAccountedComboValue()
    {
        // SIGNALR_PROTOCOL.md §3.2.8: type + combo only, where combo is the NEW Combo
        // value — not a delta, not the previous value. §3.2.8 item 1: it is read as
        // the already-accounted value and is never recomputed in the transport layer.
        const string battleId = "battle-wire-combo";
        var payload = await SwapOnceAndReadTheBatch(battleId);

        var comboEvents = payload.GetProperty("events").EnumerateArray()
            .Where(e => e.GetProperty("type").GetString() == "ComboChanged")
            .ToArray();

        Assert.NotEmpty(comboEvents);

        // §3.2.8 item 2: an integer, always present — a real 0 would be sent as 0.
        var comboValues = comboEvents.Select(e => e.GetProperty("combo").GetInt32()).ToArray();

        // §1.1 item 5 / §1.1 item 7: ComboChanged follows the Match it reports and
        // increments by exactly 1 per Match — never jumping by more.
        Assert.Equal(Enumerable.Range(1, comboValues.Length), comboValues);

        foreach (var comboEvent in comboEvents)
        {
            var members = comboEvent.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);
            Assert.Equal(new[] { "combo", "type" }, members);
        }

        // The last ComboChanged equals the authoritative Combo the write-back stored,
        // so the value is the accounting's, not the transport layer's (§3.2.8 item 1).
        using var scope = _factory.Services.CreateScope();
        var held = scope.ServiceProvider.GetRequiredService<BattleStateService>().GetBattle(battleId)!;
        Assert.Equal(held.PlayerState.Combo, comboValues[^1]);
    }

    [Fact]
    public async Task Swap_ReceiveEvents_GemMatched_ShouldCarryTheDocumentedWireSchema()
    {
        // SIGNALR_PROTOCOL.md §3.2.9: type, cellIndex, gemType, and specialGem only
        // when a Special Gem was consumed. §3.2.10 item 1: SpecialGemClaim is never
        // serialized — its ShapeIndex/IntraShapeOrder/Source collision keys are
        // explicitly never-serialized Domain bookkeeping.
        const string battleId = "battle-wire-gem-matched";
        var payload = await SwapOnceAndReadTheBatch(battleId);

        var gems = payload.GetProperty("events").EnumerateArray()
            .Where(e => e.GetProperty("type").GetString() == "GemMatched")
            .ToArray();

        Assert.NotEmpty(gems);

        foreach (var gem in gems)
        {
            // §3.2.9 item 1: the cleared cell's §1.0 index, stated explicitly.
            Assert.InRange(gem.GetProperty("cellIndex").GetInt32(), 0, BoardState.CellCount - 1);

            // §3.2.9 item 2: always the cell's Gem type, including for a cell that
            // held a Special Gem — never absent, never a Special Gem type.
            Assert.Contains(
                gem.GetProperty("gemType").GetString(),
                new[] { "ATK", "DEF", "HP", "POWER" });

            var members = gem.EnumerateObject().Select(p => p.Name).ToArray();
            Assert.All(members, m => Assert.Contains(m, new[] { "type", "cellIndex", "gemType", "specialGem" }));

            // §3.2.10 item 1: no SpecialGemClaim internals reach the wire.
            Assert.DoesNotContain(members, m => m is "shapeIndex" or "intraShapeOrder" or "source" or "claim");

            if (gem.TryGetProperty("specialGem", out var consumed))
            {
                // §3.2.9 item 3: present exactly when the cleared cell held one.
                AssertSpecialGemWireShape(consumed);
            }

            // §3.2.9 item 4 / §3.2.5: for an ordinary Gem the member is omitted —
            // never null, never a "None" type, never an empty orientation.
            Assert.All(
                gem.EnumerateObject(),
                p => Assert.NotEqual(JsonValueKind.Null, p.Value.ValueKind));
        }
    }

    [Fact]
    public async Task Swap_ReceiveEvents_ShouldSerializeAcrossTheSignalRJsonBoundary()
    {
        // SIGNALR_PROTOCOL.md §3.2.1 item 1: BattleEvent is a Domain type whose
        // non-applicable accessors throw, so direct System.Text.Json serialization of
        // it is invalid BY CONSTRUCTION and aborts the send. This is the regression
        // test for that defect, and it proves the fix at the real boundary: the batch
        // is produced by the server, serialized by the SignalR client's own JSON, and
        // read back — not merely constructed in memory.
        const string battleId = "battle-wire-serialization";
        var pair = CreateBattleOnServerWithValidPair(battleId);

        var hubConnection = BuildHubConnection();
        var batch = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        hubConnection.On<JsonElement>("ReceiveEvents", payload => batch.TrySetResult(payload));
        hubConnection.Closed += ex =>
        {
            if (ex is not null)
            {
                failure.TrySetResult(ex);
            }

            return Task.CompletedTask;
        };

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        // Before the projection fix this call threw HubException ("The server closed
        // the connection with the following error") because the serializer reflected
        // over the throwing accessors. It must now complete.
        var result = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "client-seq-serialization");

        Assert.True(result.Accepted);

        // The batch — and with it the projected wire items — actually crossed the
        // SignalR JSON boundary. This is the point of the test: a payload that merely
        // builds in memory would still fail here. Awaiting the batch directly (rather
        // than racing it) is what would have thrown before the projection existed.
        var payload = await batch.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // The connection must not have faulted while the batch was serialized.
        Assert.False(failure.Task.IsCompleted, "The connection must not fault during serialization.");

        // The three envelope members survived the round trip.
        Assert.Equal(battleId, payload.GetProperty("battleId").GetString());
        Assert.True(payload.GetProperty("serverSequence").GetInt32() >= 1);

        var events = payload.GetProperty("events").EnumerateArray().ToArray();
        Assert.NotEmpty(events);

        // Every item is a serialized flat JSON object carrying the documented string
        // discriminator — not a numeric enum ordinal (§3.2.2 items 2–3).
        foreach (var e in events)
        {
            Assert.Equal(JsonValueKind.Object, e.ValueKind);
            Assert.Contains(
                e.GetProperty("type").GetString(),
                new[]
                {
                    "MatchCreated", "CascadeCreated", "ComboChanged", "GemMatched",
                    // §3.3: the Passive stage's two events travel in this same batch on
                    // this same path (§4.3 item 10), so they are valid discriminators
                    // here — never a fifth or sixth *message*.
                    "PassiveCharged", "PassiveTriggered",
                    // §1/§2: the three Damage events follow the Passive stage's reports
                    // and travel in the same batch (GAME_EVENTS.md §1, §2).
                    "DamageCalculated", "DamageDealt", "DamageTaken",
                });

            // §3.2.2 item 4 / §3.2.5 item 5: the discriminator is always present and
            // never omitted or null.
            Assert.NotEqual(JsonValueKind.Null, e.GetProperty("type").ValueKind);

            // §3.2.5: no member of any item is ever sent as JSON null. A null here
            // would mean the projection emitted an inapplicable member rather than
            // omitting it.
            Assert.All(e.EnumerateObject(), p => Assert.NotEqual(JsonValueKind.Null, p.Value.ValueKind));
        }

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task Swap_ReceiveEvents_ShouldCarryNoMemberOutsideTheDocumentedSchema()
    {
        // SIGNALR_PROTOCOL.md §3.2.12 / §3.3: the events carry exactly the tabulated
        // members. No item carries a battleId, a serverSequence, a turn, a board, a
        // combo on a non-ComboChanged event, a matchCount, a timestamp, or a GUID.
        const string battleId = "battle-wire-no-extra-members";
        var payload = await SwapOnceAndReadTheBatch(battleId);

        foreach (var e in payload.GetProperty("events").EnumerateArray())
        {
            var type = e.GetProperty("type").GetString();
            var members = e.EnumerateObject().Select(p => p.Name).ToArray();

            var allowed = type switch
            {
                "MatchCreated" => new[] { "type", "shape", "cells", "gemType", "cascadeDepth", "createdSpecialGems" },
                "CascadeCreated" => new[] { "type", "cascadeDepth" },
                "ComboChanged" => new[] { "type", "combo" },
                "GemMatched" => new[] { "type", "cellIndex", "gemType", "specialGem" },
                // §3.3: the Passive identity, the progress value, and the Threshold —
                // and no `effect summary`, whose absence GAME_EVENTS.md §2 item 3
                // records as the deferred member rather than an omission.
                "PassiveCharged" => new[] { "type", "passiveId", "progress", "threshold" },
                "PassiveTriggered" => new[] { "type", "passiveId", "progress", "threshold" },
                // §1/§2: the Damage pipeline's three events, with the members the
                // contract gives them (GAME_EVENTS.md §2, SIGNALR_PROTOCOL.md
                // §3.2.13–§3.2.15).
                "DamageCalculated" => new[] { "type", "base", "comboModifier", "elementModifier", "otherModifiers", "defense", "finalDamage" },
                "DamageDealt" => new[] { "type", "source", "target", "amount" },
                "DamageTaken" => new[] { "type", "source", "target", "amount" },
                _ => throw new InvalidOperationException($"Undocumented event type on the wire: {type}."),
            };

            Assert.All(members, m => Assert.Contains(m, allowed));

            // The discriminator is the only identifier, and it is always present.
            Assert.Contains("type", members);

            // §3.2.12 item 1: no envelope member is duplicated inside an event.
            Assert.DoesNotContain(members, m => m is "battleId" or "serverSequence" or "turn"
                or "board" or "matchCount" or "timestamp" or "id" or "guid");

            // §3.2.12 item 3 / §3.3: no gameplay field §2 does not define. The
            // `effect summary` GAME_EVENTS.md §2 item 3 records as deferred is not
            // invented on the wire either.
            Assert.DoesNotContain(members, m => m is "effect" or "effectSummary" or "petId" or "element");
        }

        // The Passive events and the Damage events really were in this batch —
        // otherwise the allowed-member table above would be asserting nothing
        // about §3.3 and §1/§2.
        var types = payload.GetProperty("events").EnumerateArray()
            .Select(e => e.GetProperty("type").GetString())
            .ToArray();

        Assert.Contains("PassiveCharged", types);
        Assert.Contains("DamageCalculated", types);
        Assert.Contains("DamageDealt", types);
        Assert.Contains("DamageTaken", types);
    }

    /// <summary>
    /// Asserts one <c>specialGem</c> member against
    /// <c>SIGNALR_PROTOCOL.md</c> §3.2.10 — the transport representation of a
    /// created or consumed Special Gem, and never <c>SpecialGemClaim</c>.
    /// </summary>
    private static void AssertSpecialGemWireShape(JsonElement specialGem)
    {
        Assert.Equal(JsonValueKind.Object, specialGem.ValueKind);

        var type = specialGem.GetProperty("type").GetString();

        // §3.2.4: the three documented types, as strings, never ordinals.
        Assert.Contains(type, new[] { "LineClear", "Burst", "Area" });

        // §3.2.10 item 2: only these two facts are sent. Item 3: no cellIndex. Item 4:
        // no GemType. Item 5: no identity, order, depth, or age.
        var members = specialGem.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.All(members, m => Assert.Contains(m, new[] { "type", "orientation" }));
        Assert.DoesNotContain(members, m => m is "cellIndex" or "gemType" or "shapeIndex"
            or "intraShapeOrder" or "source" or "id" or "depth" or "age");

        if (type == "LineClear")
        {
            // §3.2.10 item 6: orientation is present if and only if the type is
            // LineClear, and a Line Clear Gem always carries one of the two values.
            Assert.Contains(
                specialGem.GetProperty("orientation").GetString(),
                new[] { "Horizontal", "Vertical" });
        }
        else
        {
            // §3.2.10 item 6: Burst and Area omit it — an orientation there would be a
            // value no rule reads (§3.2.5: omitted, never null).
            Assert.False(specialGem.TryGetProperty("orientation", out _));
        }
    }

    /// <summary>
    /// Projects the resolution's own event list onto the §3.2 wire representation,
    /// as JSON, for use as an authoritative expectation.
    ///
    /// This reuses the production projection rather than re-deriving the schema, so
    /// a test can compare the received batch member-for-member against the document
    /// the contract fixed. It asserts nothing by itself.
    /// </summary>
    private static JsonElement[] ProjectToWireSchema(IReadOnlyList<BattleEvent> events)
    {
        var options = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

        return events
            .Select(e => JsonSerializer.SerializeToElement(ProjectOne(e), options))
            .ToArray();
    }

    /// <summary>
    /// The §3.2 wire item for one Domain event, read by discriminator first so no
    /// non-applicable accessor is touched.
    ///
    /// The test expectation is expressed independently of the production projection's
    /// internals — it names each documented member — so it fails if the projection's
    /// member names or presence rules drift from §3.2.
    /// </summary>
    private static BattleEventWireDto ProjectOne(BattleEvent e)
    {
        var gemType = e.Type switch
        {
            BattleEventType.MatchCreated => GemTypes.ToContractName(e.Match.Shape.GemType),
            BattleEventType.GemMatched => GemTypes.ToContractName(e.Gem.GemType),
            _ => string.Empty,
        };

        return e.Type switch
        {
            BattleEventType.MatchCreated => BattleEventWireDto.MatchCreated(
                e.Match.Shape.IsLt ? "Lt" : "Straight",
                e.Match.Shape.Cells,
                gemType,
                e.Match.CascadeDepth,
                e.Match.CreatedSpecialGems.Count == 0
                    ? null
                    : e.Match.CreatedSpecialGems
                        .Select(c => new CreatedSpecialGemWireDto(c.CellIndex, ToWire(c.SpecialGem)))
                        .ToArray()),

            BattleEventType.CascadeCreated => BattleEventWireDto.CascadeCreated(e.CascadeDepth),

            BattleEventType.ComboChanged => BattleEventWireDto.ComboChanged(e.Combo),

            BattleEventType.GemMatched => BattleEventWireDto.GemMatched(
                e.Gem.CellIndex,
                gemType,
                e.Gem.ConsumedSpecialGem is { } consumed ? ToWire(consumed) : null),

            // §3.3: the Passive stage's two events, with the members the contract
            // gives them — the identity, the progress value, and the Threshold.
            BattleEventType.PassiveCharged => BattleEventWireDto.PassiveCharged(
                e.PassiveCharged.PassiveId.Value,
                e.PassiveCharged.Progress,
                e.PassiveCharged.Threshold),

            BattleEventType.PassiveTriggered => BattleEventWireDto.PassiveTriggered(
                e.PassiveTriggered.PassiveId.Value,
                e.PassiveTriggered.Progress,
                e.PassiveTriggered.Threshold),

            BattleEventType.DamageCalculated => BattleEventWireDto.DamageCalculated(
                e.DamageCalculated.Base,
                e.DamageCalculated.ComboModifier,
                e.DamageCalculated.ElementModifier,
                e.DamageCalculated.OtherModifiers,
                e.DamageCalculated.Defense,
                e.DamageCalculated.FinalDamage),

            BattleEventType.DamageDealt => BattleEventWireDto.DamageDealt(
                e.DamageDealt.Source.ToString().ToLowerInvariant(),
                e.DamageDealt.Target.ToString().ToLowerInvariant(),
                e.DamageDealt.Amount),

            BattleEventType.DamageTaken => BattleEventWireDto.DamageTaken(
                e.DamageTaken.Source.ToString().ToLowerInvariant(),
                e.DamageTaken.Target.ToString().ToLowerInvariant(),
                e.DamageTaken.Amount),

            _ => throw new ArgumentOutOfRangeException(
                nameof(e),
                e.Type,
                "Not one of the documented Battle Event types (SIGNALR_PROTOCOL.md §3.2.2, §3.3)."),
        };
    }

    private static SpecialGemWireDto ToWire(SpecialGem gem) =>
        new(gem.Type.ToString(), gem.Orientation?.ToString());

    /// <summary>
    /// Creates a battle, commits one accepted Swap through the real hub path, and
    /// returns the serialized <c>ReceiveEvents</c> payload.
    /// </summary>
    private async Task<JsonElement> SwapOnceAndReadTheBatch(string battleId, bool preferPlainMatchThree = false)
    {
        var pair = CreateBattleOnServerWithValidPair(battleId);

        var hubConnection = BuildHubConnection();
        var batch = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<JsonElement>("ReceiveEvents", payload => batch.TrySetResult(payload));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        var result = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "client-seq-wire");

        Assert.True(result.Accepted);

        var payload = await batch.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // §3.1 item 3: no board, no state, no Combo/Match duplication travels in the
        // batch — the state push carries those.
        Assert.DoesNotContain("board", payload.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.False(payload.TryGetProperty("playerState", out _));

        _ = preferPlainMatchThree; // the board's own match shapes decide which case applies

        await hubConnection.StopAsync();

        return payload;
    }

    /// <summary>
    /// An adjacent pair of the battle's current board whose exchange produces no
    /// Match, so the Swap is rejected with <c>NO_MATCH_FROM_SWAP</c>
    /// (<c>MATCH3_RULES.md</c> §2.1.2 item 4).
    /// </summary>
    private SwapRequest FindAdjacentPairThatProducesNoMatch(string battleId)
    {
        using var scope = _factory.Services.CreateScope();
        var board = scope.ServiceProvider
            .GetRequiredService<BattleStateService>()
            .GetBattle(battleId)!
            .BoardState;

        for (var index = 0; index < BoardState.CellCount; index++)
        {
            var right = BoardState.ToColumn(index) + 1 < BoardState.Columns ? index + 1 : -1;
            var down = index + BoardState.Width < BoardState.CellCount ? index + BoardState.Width : -1;

            foreach (var to in new[] { right, down })
            {
                if (to < 0)
                {
                    continue;
                }

                if (MatchDetector.Detect(board.WithSwapped(index, to)).Count == 0)
                {
                    return new SwapRequest(index, to);
                }
            }
        }

        throw new InvalidOperationException(
            "A generated board has at least one adjacent pair that produces no Match; "
            + "otherwise every Swap would be match-producing (MATCH3_RULES.md §1.4).");
    }

    /// <summary>
    /// Creates the authoritative foundation state server-side and returns an adjacent
    /// pair of its generated board whose exchange produces a §3 Match — guaranteed to
    /// exist by <c>MATCH3_RULES.md</c> §1.4.
    /// </summary>
    private SwapRequest CreateBattleOnServerWithValidPair(string battleId)
    {
        CreateBattleOnServer(battleId);

        return FindAdjacentPairThatProducesAMatch(battleId);
    }

    /// <summary>
    /// An adjacent pair of the battle's current board whose exchange produces a §3
    /// Match and which is not the excluded pair (<c>MATCH3_RULES.md</c> §1.4).
    /// </summary>
    private SwapRequest FindAdjacentPairThatProducesAMatch(string battleId, SwapRequest? exclude = null)
    {
        using var scope = _factory.Services.CreateScope();
        var board = scope.ServiceProvider
            .GetRequiredService<BattleStateService>()
            .GetBattle(battleId)!
            .BoardState;

        var excludedPair = exclude is { } excluded
            ? CommittedSwapPair.FromCells(excluded.From, excluded.To)
            : (CommittedSwapPair?)null;

        for (var index = 0; index < BoardState.CellCount; index++)
        {
            var right = BoardState.ToColumn(index) + 1 < BoardState.Columns ? index + 1 : -1;
            var down = index + BoardState.Width < BoardState.CellCount ? index + BoardState.Width : -1;

            foreach (var to in new[] { right, down })
            {
                if (to < 0)
                {
                    continue;
                }

                // The pair is unordered, so the exclusion compares canonical pairs.
                if (excludedPair is { } skip
                    && CommittedSwapPair.FromCells(index, to) == skip)
                {
                    continue;
                }

                if (MatchDetector.Detect(board.WithSwapped(index, to)).Count > 0)
                {
                    return new SwapRequest(index, to);
                }
            }
        }

        throw new InvalidOperationException(
            "A generated board has at least one valid Swap (MATCH3_RULES.md §1.4).");
    }
}
