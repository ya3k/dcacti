using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GameServer.Api.Controllers;
using GameServer.Application.Battle;
using GameServer.Domain.Players;
using GameServer.Infrastructure.Postgres;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace GameServer.Api.Tests;

/// <summary>
/// The web authentication ownership boundary — <c>API_CONTRACTS.md</c> §2 and
/// <c>DATABASE.md</c> §1 (ADR-020).
/// </summary>
public class AuthPlayerOwnershipTests
{
    private static string NewUsername() => $"user_{Guid.NewGuid():N}"[..15];

    [Fact]
    public async Task Register_ForANewAccount_ShouldCreateAPlayerAtLevelOne_AndIssueJwt()
    {
        var username = NewUsername();
        const string password = "securePassword123";

        using var factory = new AuthFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(username, password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);

        Assert.True(
            JwtTokenShape.IsThreePartJwt(body.SessionToken),
            "API_CONTRACTS.md §2.3: sessionToken must be the issued JWT");

        Assert.False(string.IsNullOrWhiteSpace(body.PlayerId));
        Assert.Equal(username, body.Username);

        // The Player was genuinely persisted at Level 1, tied to the Account
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        var account = await context.Accounts.SingleAsync(a => a.Username == username);
        var player = await context.Players.SingleAsync(p => p.AccountId == account.AccountId);

        Assert.Equal(body.PlayerId, player.PlayerId);
        Assert.Equal(Player.InitialLevel, player.Level);
        Assert.Equal(1, player.Level);
    }

    [Fact]
    public async Task Login_ForAnExistingAccount_ShouldReuseTheSamePlayer_AndPreserveItsLevel()
    {
        var username = NewUsername();
        const string password = "securePassword123";

        using var factory = new AuthFactory();
        var client = factory.CreateClient();

        // 1. Register
        var regResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(username, password));
        Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);
        var regBody = await regResponse.Content.ReadFromJsonAsync<AuthResponse>();

        // 2. Advance player Level
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            var account = await context.Accounts.SingleAsync(a => a.Username == username);
            var stored = await context.Players.SingleAsync(p => p.AccountId == account.AccountId);

            context.Entry(stored).Property(nameof(Player.Level)).CurrentValue = 23;
            await context.SaveChangesAsync();
        }

        // 3. Login
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(username, password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.Equal(regBody!.PlayerId, loginBody!.PlayerId);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            var account = await context.Accounts.SingleAsync(a => a.Username == username);

            Assert.Equal(1, await context.Players.CountAsync(p => p.AccountId == account.AccountId));

            var player = await context.Players.SingleAsync(p => p.AccountId == account.AccountId);
            Assert.Equal(23, player.Level);
        }
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_ShouldReturnConflict()
    {
        var username = NewUsername();
        const string password = "securePassword123";

        using var factory = new AuthFactory();
        var client = factory.CreateClient();

        var first = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(username, password));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(username, password));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("USERNAME_ALREADY_EXISTS", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Login_WithIncorrectPassword_ShouldReturnUnauthorized()
    {
        var username = NewUsername();
        const string password = "securePassword123";

        using var factory = new AuthFactory();
        var client = factory.CreateClient();

        var reg = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(username, password));
        Assert.Equal(HttpStatusCode.OK, reg.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, "wrongPassword999"));
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("INVALID_CREDENTIALS", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Register_WithInvalidInput_ShouldReturnBadRequest()
    {
        using var factory = new AuthFactory();
        var client = factory.CreateClient();

        // Too short username
        var res1 = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("ab", "pass123456"));
        Assert.Equal(HttpStatusCode.BadRequest, res1.StatusCode);

        // Too short password (< 6 chars)
        var res2 = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("validuser", "123"));
        Assert.Equal(HttpStatusCode.BadRequest, res2.StatusCode);
    }

    private sealed class AuthFactory : WebApplicationFactory<Program>
    {
        private readonly string _storeName = $"auth-ownership-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", "");
            builder.UseSetting("ConnectionStrings:Redis", "");

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

                services.AddSingleton<IBattleStateRepository, ApiTestBattleStateRepository>();
            });
        }

        protected override void ConfigureClient(HttpClient client)
        {
            base.ConfigureClient(client);
            TestProvisionedContent.Seed(Services);
        }
    }
}
