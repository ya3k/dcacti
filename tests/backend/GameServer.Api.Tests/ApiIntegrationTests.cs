using System.Net;
using System.Net.Http.Json;
using GameServer.Api.Controllers;
using GameServer.Api.Hubs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
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
}
