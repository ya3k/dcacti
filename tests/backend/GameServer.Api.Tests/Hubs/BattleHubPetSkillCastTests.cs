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

public sealed class BattleHubPetSkillCastTests : IClassFixture<ApiIntegrationTests.ApiIntegrationFactory>
{
    private readonly ApiIntegrationTests.ApiIntegrationFactory _factory;

    private static readonly string OwnerSessionToken =
        TestApplicationSession.Mint(playerId: "player_petskill_api_test");

    private static readonly PlayerId Owner = new("player_petskill_api_test");

    private static readonly BattleStateService.PetConfiguration PetConfig = new(
        new PetId("pet_instance_petskill_1"),
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

    public BattleHubPetSkillCastTests(ApiIntegrationTests.ApiIntegrationFactory factory)
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
    public async Task PetSkillCast_Inferno_ReturnsAccepted_AndBroadcastsBattleStateUpdatedAndReceiveEvents()
    {
        var battleId = $"battle-petskill-inferno-{Guid.NewGuid():N}";
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

        var response = await hubConnection.InvokeAsync<PetSkillCastResponse>("PetSkillCast", battleId, "client-seq-1");

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

        // Events validation (SIGNALR_PROTOCOL.md §3.2.20 & §3.2.21)
        Assert.Equal(battleId, eventsPayload.GetProperty("battleId").GetString());
        Assert.Equal(1, eventsPayload.GetProperty("serverSequence").GetInt32());

        var eventsArray = eventsPayload.GetProperty("events");

        // GAME_EVENTS.md §2 item 4 / SIGNALR_PROTOCOL.md §3.2.24 item 6 (D-8): the
        // cast's cost is the FIRST Power mutation, and it is reported by its own
        // PowerChanged (`source = "card"`) in the authoritative mutation order —
        // before the PetSkillCast that reports the Skill cast itself.
        Assert.True(eventsArray.GetArrayLength() >= 3);

        var cardCastWireDto = eventsArray[0];
        Assert.Equal("CardCast", cardCastWireDto.GetProperty("type").GetString());
        Assert.Equal("card-inferno", cardCastWireDto.GetProperty("cardId").GetString());

        var costPowerChangedWireDto = eventsArray[1];
        Assert.Equal("PowerChanged", costPowerChangedWireDto.GetProperty("type").GetString());
        Assert.Equal(-40, costPowerChangedWireDto.GetProperty("delta").GetInt32());
        Assert.Equal(10, costPowerChangedWireDto.GetProperty("power").GetInt32());
        Assert.Equal("card", costPowerChangedWireDto.GetProperty("source").GetString());

        var petSkillCastWireDto = eventsArray[2];
        Assert.Equal("PetSkillCast", petSkillCastWireDto.GetProperty("type").GetString());
        Assert.Equal("card-inferno", petSkillCastWireDto.GetProperty("cardId").GetString());
    }

    [Fact]
    public async Task PetSkillCast_InsufficientPower_ReturnsRejected_AndBroadcastsNothing()
    {
        var battleId = $"battle-petskill-low-power-{Guid.NewGuid():N}";
        await CreateBattleWithPowerAsync(battleId, power: 10, hp: 500, maxHp: 1000);

        var hubConnection = BuildHubConnection();

        var stateUpdatedTcs = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        hubConnection.On<JsonElement>("BattleStateUpdated", payload => stateUpdatedTcs.TrySetResult(payload));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        await stateUpdatedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var response = await hubConnection.InvokeAsync<PetSkillCastResponse>("PetSkillCast", battleId, "client-seq-2");

        Assert.False(response.Accepted);
        Assert.Equal("INSUFFICIENT_POWER", response.Reason);
    }

    [Fact]
    public async Task PetSkillCast_UnknownBattle_ReturnsRejected_BattleNotFound()
    {
        var hubConnection = BuildHubConnection();
        await hubConnection.StartAsync();

        var response = await hubConnection.InvokeAsync<PetSkillCastResponse>("PetSkillCast", "battle-nonexistent", "client-seq-3");

        Assert.False(response.Accepted);
        Assert.Equal("BATTLE_NOT_FOUND", response.Reason);
    }
}
