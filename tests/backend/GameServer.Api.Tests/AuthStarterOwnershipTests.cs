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

        // MVP_SCOPE.md §1 / DATABASE.md §2 item 1: 5 Pets, 3 Basic Cards,
        // 10 Relics — the whole MVP content set, because MVP has no
        // post-creation acquisition system.
        Assert.Equal(5, await context.Pets.CountAsync(p => p.PlayerId == auth.PlayerId));
        Assert.Equal(3, await context.PlayerUnlockedCards.CountAsync(c => c.PlayerId == auth.PlayerId));
        Assert.Equal(10, await context.Relics.CountAsync(r => r.PlayerId == auth.PlayerId));

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
        Assert.Equal(5, await context.Pets.CountAsync(p => p.PlayerId == first.PlayerId));
        Assert.Equal(3, await context.PlayerUnlockedCards.CountAsync(c => c.PlayerId == first.PlayerId));
        Assert.Equal(10, await context.Relics.CountAsync(r => r.PlayerId == first.PlayerId));
    }

    [Fact]
    public async Task RegisteringANewAccount_ShouldStartEveryStarterPetAtTheDocumentedValues()
    {
        var username = NewUsername();

        using var factory = new StarterFactory();
        var client = factory.CreateClient();

        var auth = await RegisterAsync(client, username);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        var pets = await context.Pets
            .Where(p => p.PlayerId == auth.PlayerId)
            .ToListAsync();

        // DATABASE.md §2 item 1 / §3: Common / Star 1 / XP 0 / Level 1 — on all
        // five owned instances (PET_RULES.md §3, §4, §5.2).
        Assert.Equal(5, pets.Count);
        Assert.All(pets, pet =>
        {
            Assert.Equal(PetTier.Common, pet.Tier);
            Assert.Equal(1, pet.Star);
            Assert.Equal(0, pet.XP);
            Assert.Equal(1, pet.Level);
        });
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
    public async Task RegisteringANewAccount_ShouldGrantEveryProvisionedMvpRelic()
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

        // MVP_SCOPE.md §1 / RELIC_RULES.md §6: all ten, one owned instance each —
        // which is what makes the 3–5 Relic loadout a real build decision.
        Assert.Equal(
            [
                "relic-arcane-battery",
                "relic-assassin-eye",
                "relic-battle-instinct",
                "relic-berserker-core",
                "relic-burning-curse",
                "relic-cascade-core",
                "relic-combo-fang",
                "relic-emergency-core",
                "relic-execution-mark",
                "relic-mana-crystal",
            ],
            relics.Select(r => r.RelicDefinitionId).ToArray());

        // RELIC_RULES.md §2.2: every instance carries its own identity.
        Assert.Equal(10, relics.Select(r => r.RelicInstanceId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task RegisteringANewAccount_ShouldOwnEveryProvisionedMvpPet()
    {
        var username = NewUsername();

        using var factory = new StarterFactory();
        var client = factory.CreateClient();

        var auth = await RegisterAsync(client, username);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        var petDefinitionIds = await context.Pets
            .Where(p => p.PlayerId == auth.PlayerId)
            .Select(p => p.PetDefinitionId)
            .ToListAsync();

        // MVP_SCOPE.md §1 / PET_RULES.md §8: all five, one owned instance each.
        Assert.Equal(
            ["pet-bach-ho", "pet-huyen-quy", "pet-son-hung", "pet-thanh-xa", "pet-xich-lang"],
            petDefinitionIds.OrderBy(id => id, StringComparer.Ordinal).ToArray());
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
        Assert.Equal(5, pets.GetArrayLength());

        var cards = await GetArrayAsync(client, "/api/cards");
        Assert.Equal(3, cards.GetArrayLength());

        var relics = await GetArrayAsync(client, "/api/relics");
        Assert.Equal(10, relics.GetArrayLength());
    }

    /// <summary>
    /// Every one of the five canonical MVP Bosses is fightable by a brand-new
    /// account with a loadout built only from what account creation granted.
    ///
    /// <c>MVP_SCOPE.md</c> §1 records Bosses as having no ownership relationship
    /// — "all five are always selectable" — so the grant's side of the contract
    /// is that the five Bosses are reachable with the granted Pet, the granted
    /// Basic Cards, and three of the granted Relics (<c>BOSS_RULES.md</c> §6;
    /// <c>API_CONTRACTS.md</c> §3).
    /// </summary>
    [Fact]
    public async Task RegisteredAccount_CanStartABattleAgainstEveryCanonicalBoss()
    {
        var username = NewUsername();

        using var factory = new StarterFactory();
        var client = factory.CreateClient();

        var auth = await RegisterAsync(client, username);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.SessionToken);

        var pets = await GetArrayAsync(client, "/api/pets");
        var cards = await GetArrayAsync(client, "/api/cards");
        var relics = await GetArrayAsync(client, "/api/relics");

        var petId = pets[0].GetProperty("petId").GetString();

        // The three Basic Cards, in the order the read returned them: the
        // loadout is exactly three (CARD_RULES.md §1).
        var cardLoadout = cards
            .EnumerateArray()
            .Take(3)
            .Select(card => card.GetProperty("cardId").GetString())
            .ToArray();

        // Exactly three of the ten owned Relic instances, in read order: a
        // legal 3-Relic loadout (RELIC_RULES.md §2.1 item 1 — the documented
        // bound is 3–5, so three is the smallest legal selection).
        var relicLoadout = relics
            .EnumerateArray()
            .Take(3)
            .Select(relic => relic.GetProperty("relicId").GetString())
            .ToArray();

        Assert.Equal(3, relicLoadout.Length);

        foreach (var bossId in new[]
        {
            "boss-hoa-long",
            "boss-thuy-ma",
            "boss-moc-yeu",
            "boss-son-thach-ve",
            "boss-kim-loi-vuong",
        })
        {
            var response = await client.PostAsJsonAsync(
                "/api/battle/start",
                new
                {
                    petId,
                    bossId,
                    cardLoadout,
                    relicLoadout,
                });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var state = body.GetProperty("initialState");

            Assert.Equal(bossId, state.GetProperty("bossState").GetProperty("bossId").GetString());

            // The submitted selection is what the battle carries, in order
            // (RELIC_RULES.md §2.3: element i is equip slot i + 1).
            var equippedRelics = state
                .GetProperty("petState")
                .GetProperty("equippedRelics")
                .EnumerateArray()
                .Select(entry => entry.GetString())
                .ToArray();

            Assert.Equal(relicLoadout, equippedRelics);
        }
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
