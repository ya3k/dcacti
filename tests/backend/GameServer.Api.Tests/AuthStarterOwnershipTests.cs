using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GameServer.Api.Controllers;
using GameServer.Application.Battle;
using GameServer.Domain.Pets;
using GameServer.Infrastructure.Postgres;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace GameServer.Api.Tests;

/// <summary>
/// Verifies the atomic starter ownership provisioning during web registration (TASK-083, ADR-020).
/// </summary>
public class AuthStarterOwnershipTests
{
    private static string NewUsername() => $"user_{Guid.NewGuid():N}"[..15];
    private const string Password = "securePassword123";

    [Fact]
    public async Task RegisteringANewAccount_ShouldCreateTheDocumentedStarterOwnership()
    {
        var username = NewUsername();

        using var factory = new StarterFactory();
        var client = factory.CreateClient();

        var auth = await RegisterAsync(client, username);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        // DATABASE.md §2 item 1: exactly 1 Pet, 3 Cards, 3 Relics.
        var pet = await context.Pets.SingleAsync(p => p.PlayerId == auth.PlayerId);

        Assert.Equal("pet-xich-lang", pet.PetDefinitionId);
        Assert.Equal(3, await context.PlayerUnlockedCards.CountAsync(c => c.PlayerId == auth.PlayerId));
        Assert.Equal(3, await context.Relics.CountAsync(r => r.PlayerId == auth.PlayerId));

        var account = await context.Accounts.SingleAsync(a => a.Username == username);
        Assert.Equal(1, await context.Players.CountAsync(p => p.AccountId == account.AccountId));
    }

    [Fact]
    public async Task RegisteringANewAccount_ShouldCreateNoSecondStarterSetOnSubsequentLogin()
    {
        var username = NewUsername();

        using var factory = new StarterFactory();
        var client = factory.CreateClient();

        var first = await RegisterAsync(client, username);
        var second = await LoginAsync(client, username);

        Assert.Equal(first.PlayerId, second.PlayerId);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        var account = await context.Accounts.SingleAsync(a => a.Username == username);
        Assert.Equal(1, await context.Players.CountAsync(p => p.AccountId == account.AccountId));
        Assert.Equal(1, await context.Pets.CountAsync(p => p.PlayerId == first.PlayerId));
        Assert.Equal(3, await context.PlayerUnlockedCards.CountAsync(c => c.PlayerId == first.PlayerId));
        Assert.Equal(3, await context.Relics.CountAsync(r => r.PlayerId == first.PlayerId));
    }

    [Fact]
    public async Task RegisteringANewAccount_ShouldStartTheStarterPetAtTheDocumentedValues()
    {
        var username = NewUsername();

        using var factory = new StarterFactory();
        var client = factory.CreateClient();

        var auth = await RegisterAsync(client, username);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        var pet = await context.Pets.SingleAsync(p => p.PlayerId == auth.PlayerId);

        // DATABASE.md §2 item 1 / §3: Common / Star 1 / XP 0 / Level 1.
        Assert.Equal(PetTier.Common, pet.Tier);
        Assert.Equal(1, pet.Star);
        Assert.Equal(0, pet.XP);
        Assert.Equal(1, pet.Level);
    }

    [Fact]
    public async Task RegisteringANewAccount_ShouldGrantTheDocumentedThreeBasicCards()
    {
        var username = NewUsername();

        using var factory = new StarterFactory();
        var client = factory.CreateClient();

        var auth = await RegisterAsync(client, username);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        var cards = await context.PlayerUnlockedCards
            .Where(c => c.PlayerId == auth.PlayerId)
            .OrderBy(c => c.CardDefinitionId)
            .ToListAsync();

        Assert.Equal(3, cards.Count);
        Assert.Equal(["card-heal", "card-power-charge", "card-shield"], cards.Select(c => c.CardDefinitionId).ToArray());
    }

    [Fact]
    public async Task RegisteringANewAccount_ShouldGrantTheDocumentedThreeRelics()
    {
        var username = NewUsername();

        using var factory = new StarterFactory();
        var client = factory.CreateClient();

        var auth = await RegisterAsync(client, username);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        var relics = await context.Relics
            .Where(r => r.PlayerId == auth.PlayerId)
            .OrderBy(r => r.RelicDefinitionId)
            .ToListAsync();

        Assert.Equal(3, relics.Count);
        Assert.Equal(["relic-assassin-eye", "relic-berserker-core", "relic-mana-crystal"], relics.Select(r => r.RelicDefinitionId).ToArray());
    }

    [Fact]
    public async Task RegisteringANewAccount_ShouldReturnTheDocumentedResponseShape()
    {
        var username = NewUsername();

        using var factory = new StarterFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(username, Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            ["playerId", "sessionToken", "username"],
            body.EnumerateObject()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());

        Assert.True(JwtTokenShape.IsThreePartJwt(body.GetProperty("sessionToken").GetString()));
    }

    [Fact]
    public async Task RegistrationFailure_ShouldCreateNoPlayerAndNoStarterOwnership()
    {
        using var factory = new StarterFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("u", "123")); // Invalid short credentials

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        Assert.Equal(0, await context.Players.CountAsync());
        Assert.Equal(0, await context.Pets.CountAsync());
        Assert.Equal(0, await context.PlayerUnlockedCards.CountAsync());
        Assert.Equal(0, await context.Relics.CountAsync());
    }

    [Fact]
    public async Task RegisteredAccount_CanQueryStarterCollectionsImmediately()
    {
        var username = NewUsername();

        using var factory = new StarterFactory();
        var client = factory.CreateClient();

        var auth = await RegisterAsync(client, username);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.SessionToken);

        var pets = await GetArrayAsync(client, "/api/pets");
        Assert.Equal(1, pets.GetArrayLength());

        var cards = await GetArrayAsync(client, "/api/cards");
        Assert.Equal(3, cards.GetArrayLength());

        var relics = await GetArrayAsync(client, "/api/relics");
        Assert.Equal(3, relics.GetArrayLength());
    }

    private static async Task<AuthResponse> RegisterAsync(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(username, Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private static async Task<AuthResponse> LoginAsync(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(username, Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private static async Task<JsonElement> GetArrayAsync(HttpClient client, string route)
    {
        var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private sealed class StarterFactory : WebApplicationFactory<Program>
    {
        private readonly string _storeName = $"starter-{Guid.NewGuid():N}";

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
