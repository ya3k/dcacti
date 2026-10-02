using System.Net.Http.Headers;
using System.Text.Json;
using GameServer.Api.Hubs;
using GameServer.Application.Battle;
using GameServer.Domain.Battle;
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

public sealed class BattleHubCardCastTests : IClassFixture<ApiIntegrationTests.ApiIntegrationFactory>
{
    private readonly ApiIntegrationTests.ApiIntegrationFactory _factory;

    private static readonly string OwnerSessionToken =
        TestApplicationSession.Mint(playerId: "player_cardcast_api_test");

    private static readonly PlayerId Owner = new("player_cardcast_api_test");

    private static readonly BattleStateService.PetConfiguration PetConfig = new(
        new PetId("pet_instance_cardcast_1"),
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

    private static readonly BossDefinition BossDef = BossDefinitions.HoaLong;

    public BattleHubCardCastTests(ApiIntegrationTests.ApiIntegrationFactory factory)
    {
        _factory = factory;
    }

    private void EnsureSeeded()
    {
        _ = _factory.CreateClient();
        TestProvisionedContent.Seed(_factory.Services);
    }

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
                    new BearerMessageHandler(
                        _factory.Server.CreateHandler(),
                        OwnerSessionToken);

                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(OwnerSessionToken);
            })
            .Build();
    }

    private async Task CreateBattleWithPowerAsync(string battleId, int power, int hp = 500, int maxHp = 1000)
    {
        EnsureSeeded();
        using var scope = _factory.Services.CreateScope();
        var battleService = scope.ServiceProvider.GetRequiredService<BattleStateService>();
        var repo = scope.ServiceProvider.GetRequiredService<IBattleStateRepository>();

        var state = await battleService.CreateBattleAsync(battleId, Owner, PetConfig, BossDef);

        var updatedPetState = state.PetState with
        {
            HP = hp,
            MaxHP = maxHp,
            Power = power,
        };

        var modifiedState = state with { PetState = updatedPetState };
        await repo.TryUpdateAsync(modifiedState, state.Sequence);
    }

    [Fact]
    public async Task CardCast_Heal_ReturnsAccepted_AndBroadcastsBattleStateUpdatedAndReceiveEvents()
    {
        var battleId = $"battle-cardcast-heal-{Guid.NewGuid():N}";
        await CreateBattleWithPowerAsync(battleId, power: 50, hp: 500, maxHp: 1000);

        var hubConnection = BuildHubConnection();

        var stateUpdatedTcs = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var eventsTcs = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        hubConnection.On<JsonElement>("BattleStateUpdated", payload => stateUpdatedTcs.TrySetResult(payload));
        hubConnection.On<JsonElement>("ReceiveEvents", payload => eventsTcs.TrySetResult(payload));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        // Reset stateUpdatedTcs after JoinBattle's initial state push
        await stateUpdatedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var castStateUpdatedTcs = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<JsonElement>("BattleStateUpdated", payload => castStateUpdatedTcs.TrySetResult(payload));

        var response = await hubConnection.InvokeAsync<CardCastResponse>("CardCast", battleId, "card-heal", "client-seq-1");

        Assert.True(response.Accepted);
        Assert.Null(response.Reason);

        var statePayload = await castStateUpdatedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var eventsPayload = await eventsTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // State validation
        Assert.Equal(1, statePayload.GetProperty("sequence").GetInt32());
        var petState = statePayload.GetProperty("petState");
        Assert.True(petState.TryGetProperty("equippedCards", out var equippedCardsProp));
        Assert.Equal(
            new[] { "card-heal", "card-shield", "card-power-charge", "card-inferno" },
            equippedCardsProp.EnumerateArray().Select(c => c.GetString()).ToArray());

        // Events validation (SIGNALR_PROTOCOL.md §3.2.20)
        Assert.Equal(battleId, eventsPayload.GetProperty("battleId").GetString());
        Assert.Equal(1, eventsPayload.GetProperty("serverSequence").GetInt32());

        var eventsArray = eventsPayload.GetProperty("events");
        Assert.Equal(1, eventsArray.GetArrayLength());

        var cardCastWireDto = eventsArray[0];
        Assert.Equal("CardCast", cardCastWireDto.GetProperty("type").GetString());
        Assert.Equal("card-heal", cardCastWireDto.GetProperty("cardId").GetString());

        // Wire schema must NOT contain powerCost or effect summary per §3.2.20 and §3.2.25
        Assert.False(cardCastWireDto.TryGetProperty("powerCost", out _));
        Assert.False(cardCastWireDto.TryGetProperty("cost", out _));
        Assert.False(cardCastWireDto.TryGetProperty("effects", out _));
        Assert.False(cardCastWireDto.TryGetProperty("effectSummary", out _));

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task CardCast_Shield_ReturnsAccepted_AndPushesReceiveEvents()
    {
        var battleId = $"battle-cardcast-shield-{Guid.NewGuid():N}";
        await CreateBattleWithPowerAsync(battleId, power: 40);

        var hubConnection = BuildHubConnection();

        var eventsTcs = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<JsonElement>("ReceiveEvents", payload => eventsTcs.TrySetResult(payload));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        var response = await hubConnection.InvokeAsync<CardCastResponse>("CardCast", battleId, "card-shield", "client-seq-2");

        Assert.True(response.Accepted);
        Assert.Null(response.Reason);

        var eventsPayload = await eventsTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var cardCastWireDto = eventsPayload.GetProperty("events")[0];
        Assert.Equal("CardCast", cardCastWireDto.GetProperty("type").GetString());
        Assert.Equal("card-shield", cardCastWireDto.GetProperty("cardId").GetString());

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task CardCast_PowerCharge_ReturnsAccepted()
    {
        var battleId = $"battle-cardcast-power-{Guid.NewGuid():N}";
        await CreateBattleWithPowerAsync(battleId, power: 10);

        var hubConnection = BuildHubConnection();

        var eventsTcs = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        hubConnection.On<JsonElement>("ReceiveEvents", payload => eventsTcs.TrySetResult(payload));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        var response = await hubConnection.InvokeAsync<CardCastResponse>("CardCast", battleId, "card-power-charge", "client-seq-3");

        Assert.True(response.Accepted);
        Assert.Null(response.Reason);

        var eventsPayload = await eventsTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var cardCastWireDto = eventsPayload.GetProperty("events")[0];
        Assert.Equal("CardCast", cardCastWireDto.GetProperty("type").GetString());
        Assert.Equal("card-power-charge", cardCastWireDto.GetProperty("cardId").GetString());

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task CardCast_InsufficientPower_ReturnsRejectedInsufficientPower()
    {
        var battleId = $"battle-cardcast-nopower-{Guid.NewGuid():N}";
        await CreateBattleWithPowerAsync(battleId, power: 0);

        var hubConnection = BuildHubConnection();
        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        var response = await hubConnection.InvokeAsync<CardCastResponse>("CardCast", battleId, "card-heal", "client-seq-4");

        Assert.False(response.Accepted);
        Assert.Equal("INSUFFICIENT_POWER", response.Reason);

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task CardCast_UnknownBattle_ReturnsRejectedBattleNotFound()
    {
        var hubConnection = BuildHubConnection();
        await hubConnection.StartAsync();

        var response = await hubConnection.InvokeAsync<CardCastResponse>("CardCast", "nonexistent-battle-id", "card-heal", "client-seq-5");

        Assert.False(response.Accepted);
        Assert.Equal("BATTLE_NOT_FOUND", response.Reason);

        await hubConnection.StopAsync();
    }
}
