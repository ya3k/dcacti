using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GameServer.Api.Controllers;
using GameServer.Api.Hubs;
using GameServer.Application.Battle;
using GameServer.Application.Runtime;
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
        // The hub is a thin transport boundary (ARCHITECTURE.md §1, §2.1):
        // these in-battle methods belong to SIGNALR_PROTOCOL.md §2/§7 and
        // require battle resolution, which is out of scope for the runtime
        // foundation.
        //
        // `JoinBattle` is deliberately NOT in this list: it is the group join
        // that triggers the documented initial-state push (§1.2, §4.1) and
        // carries no gameplay.
        var hubConnection = BuildHubConnection();
        await hubConnection.StartAsync();

        foreach (var method in new[] { "Swap", "CardCast", "PetSkillCast", "GetBattleState", "ReceiveEvents" })
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
    // Battle State Foundation transport path
    //
    //   Server → Battle State → SignalR → Client
    //
    // SIGNALR_PROTOCOL.md §4. These verify the documented initial-state push on
    // group join and nothing gameplay-related: the foundation carries no
    // gameplay field and no Status/lifecycle value (§4.4, §8.3).
    // -----------------------------------------------------------------------

    [Fact]
    public async Task JoinBattle_ShouldPushTheFoundationState_WithDocumentedInitialValues()
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

        // GAME_STATE.md §2.0.2 / SIGNALR_PROTOCOL.md §4.5.
        Assert.Equal(battleId, payload.GetProperty("battleId").GetString());
        Assert.Equal(0, payload.GetProperty("turn").GetInt32());
        Assert.Equal(0, payload.GetProperty("sequence").GetInt32());

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task BattleStateUpdated_ShouldCarryExactlyTheDocumentedFields()
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

        // SIGNALR_PROTOCOL.md §4.4: battleId, turn, sequence — no other field
        // may be added to this record. §8.3: no Status/lifecycle value.
        var fields = payload.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);
        Assert.Equal(new[] { "battleId", "sequence", "turn" }, fields);

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

        // GAME_STATE.md §2.0.3, SIGNALR_PROTOCOL.md §8.3.
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
    public async Task BattleStateUpdated_ShouldCarryNoGameplayField()
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

        // GAME_STATE.md §2.0.3: the §2 gameplay fields are not part of §2.0.
        foreach (var gameplayField in new[]
                 {
                     "board", "gem", "player", "pet", "boss", "rng", "hp", "power", "combo",
                 })
        {
            Assert.DoesNotContain(gameplayField, serialized, StringComparison.OrdinalIgnoreCase);
        }

        await hubConnection.StopAsync();
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
    /// Creates the authoritative foundation state server-side, exactly as the
    /// Application layer owns it. This is not a battle-creation contract: battle
    /// creation remains <c>POST /api/battle/start</c> (API_CONTRACTS.md §3),
    /// unchanged by Battle State Foundation.
    /// </summary>
    private void CreateBattleOnServer(string battleId)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<BattleStateService>().CreateBattle(battleId);
    }
}
