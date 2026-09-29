using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GameServer.Api.Controllers;
using GameServer.Application.Battle;
using GameServer.Application.Identity;
using GameServer.Domain.Pets;
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
/// The starter ownership bootstrap observed through the authentication boundary
/// — <c>API_CONTRACTS.md</c> §2, <c>DATABASE.md</c> §2 items 1–4.
///
/// <code>
/// POST /api/auth/discord
///         ↓
/// verified DiscordUserId
///         ↓
/// Player created
///         ↓
/// 1 Pet + 3 PlayerUnlockedCard + 3 Relic rows, one atomic commit
///         ↓
/// §2.5 response (sessionToken, playerId) — unchanged
/// </code>
///
/// <b>What is established here.</b> That the bootstrap is reachable through the
/// documented endpoint and nowhere else, that it produces the documented starter
/// contents observable through the §5 collection reads, that a repeated
/// authentication grants nothing further, and that the §2.5 response shape is
/// unchanged by all of it.
///
/// The composition itself is not re-implemented in these tests: the host runs the
/// production <c>PlayerStarterGrantFactory</c> over the provisioned content
/// <see cref="TestProvisionedContent"/> supplies, so a drift in the composition
/// fails here as well as in the Application suite.
/// </summary>
public class AuthStarterOwnershipTests
{
    private static string NewDiscordUserId() =>
        $"8035{Random.Shared.NextInt64(1_000_000_000_000_000L):D16}";

    // -----------------------------------------------------------------------
    // A/B — first creation produces the documented starter set
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AuthenticatingANewIdentity_ShouldCreateTheDocumentedStarterOwnership()
    {
        var discordUserId = NewDiscordUserId();

        using var factory = new StarterFactory(discordUserId);
        var client = factory.CreateClient();

        var playerId = await AuthenticateAsync(client);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        // DATABASE.md §2 item 1: exactly 1 Pet, 3 Cards, 3 Relics.
        var pet = await context.Pets.SingleAsync(p => p.PlayerId == playerId);

        Assert.Equal("pet-xich-lang", pet.PetDefinitionId);
        Assert.Equal(3, await context.PlayerUnlockedCards.CountAsync(c => c.PlayerId == playerId));
        Assert.Equal(3, await context.Relics.CountAsync(r => r.PlayerId == playerId));

        // And exactly one Player row.
        Assert.Equal(1, await context.Players.CountAsync(p => p.DiscordUserId == discordUserId));
    }

    [Fact]
    public async Task AuthenticatingANewIdentity_ShouldCreateNoSecondStarterSetOnRepeatAuthentication()
    {
        var discordUserId = NewDiscordUserId();

        using var factory = new StarterFactory(discordUserId);
        var client = factory.CreateClient();

        var first = await AuthenticateAsync(client);
        var second = await AuthenticateAsync(client);

        Assert.Equal(first, second);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        // DATABASE.md §2 item 3: authenticating an existing Player grants nothing.
        Assert.Equal(1, await context.Players.CountAsync(p => p.DiscordUserId == discordUserId));
        Assert.Equal(1, await context.Pets.CountAsync(p => p.PlayerId == first));
        Assert.Equal(3, await context.PlayerUnlockedCards.CountAsync(c => c.PlayerId == first));
        Assert.Equal(3, await context.Relics.CountAsync(r => r.PlayerId == first));
    }

    [Fact]
    public async Task AuthenticatingANewIdentity_ShouldStartTheStarterPetAtTheDocumentedValues()
    {
        var discordUserId = NewDiscordUserId();

        using var factory = new StarterFactory(discordUserId);
        var client = factory.CreateClient();

        var playerId = await AuthenticateAsync(client);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        var pet = await context.Pets.SingleAsync(p => p.PlayerId == playerId);

        // DATABASE.md §2 item 1 / §3: Common / Star 1 / XP 0 / Level 1.
        Assert.Equal(PetTier.Common, pet.Tier);
        Assert.Equal(1, pet.Star);
        Assert.Equal(0, pet.XP);
        Assert.Equal(1, pet.Level);
        Assert.NotEqual(default, pet.AcquiredAt);
    }

    [Fact]
    public async Task AuthenticatingANewIdentity_ShouldMintDistinctStarterRelicInstanceIds()
    {
        var discordUserId = NewDiscordUserId();

        using var factory = new StarterFactory(discordUserId);
        var client = factory.CreateClient();

        var playerId = await AuthenticateAsync(client);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        var relics = await context.Relics
            .Where(r => r.PlayerId == playerId)
            .ToListAsync();

        Assert.Equal(3, relics.Select(r => r.RelicInstanceId).Distinct(StringComparer.Ordinal).Count());

        Assert.All(relics, relic =>
            Assert.NotEqual(relic.RelicDefinitionId, relic.RelicInstanceId));
    }

    // -----------------------------------------------------------------------
    // §5 — the client observes the result only through the collection reads
    // -----------------------------------------------------------------------

    [Fact]
    public async Task StarterOwnership_ShouldBeObservableThroughTheCollectionReads()
    {
        var discordUserId = NewDiscordUserId();

        using var factory = new StarterFactory(discordUserId);
        var client = factory.CreateClient();

        var sessionToken = await AuthenticateWithSessionAsync(client);

        // API_CONTRACTS.md §2.8 "Transport": the collection reads are protected
        // and present the issued session as a bearer token.
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", sessionToken);

        // API_CONTRACTS.md §5.1/§5.3/§5.4: the client's only observation path.
        // Before this bootstrap these returned 200 [] (TASK-071 §5.5); now they
        // return the granted collection.
        var pets = await GetArrayAsync(client, "/api/pets");
        var cards = await GetArrayAsync(client, "/api/cards");
        var relics = await GetArrayAsync(client, "/api/relics");

        Assert.Single(pets.EnumerateArray());
        Assert.Equal("Xích Lang", pets[0].GetProperty("identity").GetString());

        Assert.Equal(3, cards.GetArrayLength());

        // §5.3's `cardId` is CardDefinition.CardDefinitionId.
        Assert.Equal(
            ["card-heal", "card-power-charge", "card-shield"],
            cards.EnumerateArray()
                .Select(card => card.GetProperty("cardId").GetString()!)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray());

        Assert.Equal(3, relics.GetArrayLength());
    }

    [Fact]
    public async Task StarterOwnership_ShouldBeSufficientForTheDocumentedBattleStartLoadout()
    {
        var discordUserId = NewDiscordUserId();

        using var factory = new StarterFactory(discordUserId);
        var client = factory.CreateClient();

        var sessionToken = await AuthenticateWithSessionAsync(client);

        // API_CONTRACTS.md §3 / TASK-083 §25: a starter-only Player can submit
        // `petId` = the created Pet instance, a 3-entry `cardLoadout` of the
        // starter Basic Cards, and a `relicLoadout` of 3 distinct starter
        // RelicInstanceId values without failing the ownership, category,
        // copy-limit, count, or distinctness checks.
        //
        // The loadout is built from what the §5 reads actually returned, exactly
        // as the client would.
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", sessionToken);

        var pets = await GetArrayAsync(client, "/api/pets");
        var cards = await GetArrayAsync(client, "/api/cards");
        var relics = await GetArrayAsync(client, "/api/relics");

        var petId = pets[0].GetProperty("petId").GetString()!;

        var cardLoadout = cards.EnumerateArray()
            .Select(card => card.GetProperty("cardId").GetString()!)
            .ToArray();

        var relicLoadout = relics.EnumerateArray()
            .Select(relic => relic.GetProperty("relicId").GetString()!)
            .ToArray();

        Assert.Equal(3, cardLoadout.Length);
        Assert.Equal(3, relicLoadout.Length);
        Assert.Equal(3, relicLoadout.Distinct(StringComparer.Ordinal).Count());

        var response = await client.PostAsJsonAsync(
            "/api/battle/start",
            new
            {
                petId,
                bossId = "boss-hoa-long",
                cardLoadout,
                relicLoadout,
            });

        // The loadout is accepted: no INVALID_LOADOUT, no PET_NOT_OWNED. A 400
        // here would mean the starter set is insufficient for the documented
        // battle-start validation — the exact condition TASK-083 exists to
        // remove.
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Fail(
                "API_CONTRACTS.md §3 rejected the starter-only loadout: "
                + body.GetProperty("error").GetString());
        }

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // §2.5 — the response shape is unchanged
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AuthenticatingANewIdentity_ShouldReturnTheUnchangedResponseShape()
    {
        var discordUserId = NewDiscordUserId();

        using var factory = new StarterFactory(discordUserId);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/discord",
            new DiscordAuthRequest("authorization-code"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // API_CONTRACTS.md §2.5: exactly `sessionToken` and `playerId`. The
        // starter ownership is not reported by a new member — no request or
        // response member was added by this task.
        Assert.Equal(
            ["playerId", "sessionToken"],
            body.EnumerateObject()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());

        Assert.True(JwtTokenShape.IsThreePartJwt(body.GetProperty("sessionToken").GetString()));
    }

    [Fact]
    public async Task IdentityResolutionFailure_ShouldCreateNoPlayerAndNoStarterOwnership()
    {
        var discordUserId = NewDiscordUserId();

        using var factory = new StarterFactory(discordUserId) { RejectIdentity = true };
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/discord",
            new DiscordAuthRequest("rejected-authorization-code"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        // API_CONTRACTS.md §2.6 rule 5: no Player is created or matched on any
        // failure path — and therefore no starter ownership either.
        Assert.Equal(0, await context.Players.CountAsync());
        Assert.Equal(0, await context.Pets.CountAsync());
        Assert.Equal(0, await context.PlayerUnlockedCards.CountAsync());
        Assert.Equal(0, await context.Relics.CountAsync());
    }

    // -----------------------------------------------------------------------
    // I — the deferred and unselected content is never granted
    // -----------------------------------------------------------------------

    [Fact]
    public async Task StarterOwnership_ShouldNeverIncludeDeferredOrUnselectedContent()
    {
        var discordUserId = NewDiscordUserId();

        using var factory = new StarterFactory(discordUserId);
        var client = factory.CreateClient();

        var playerId = await AuthenticateAsync(client);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        // DATABASE.md §2 item 1: no PetSkill Card is granted, and the deferred /
        // unselected definitions never appear as ownership.
        var cardDefinitionIds = await context.PlayerUnlockedCards
            .Where(c => c.PlayerId == playerId)
            .Select(c => c.CardDefinitionId)
            .ToListAsync();

        Assert.DoesNotContain("card-inferno", cardDefinitionIds);
        Assert.DoesNotContain("card-tidal-barrier", cardDefinitionIds);
        Assert.DoesNotContain("card-iron-fang", cardDefinitionIds);

        var relicDefinitionIds = await context.Relics
            .Where(r => r.PlayerId == playerId)
            .Select(r => r.RelicDefinitionId)
            .ToListAsync();

        Assert.DoesNotContain("relic-emergency-core", relicDefinitionIds);
        Assert.DoesNotContain("relic-burning-curse", relicDefinitionIds);

        var petDefinitionIds = await context.Pets
            .Where(p => p.PlayerId == playerId)
            .Select(p => p.PetDefinitionId)
            .ToListAsync();

        Assert.DoesNotContain("pet-bach-ho", petDefinitionIds);
        Assert.DoesNotContain("pet-huyen-quy", petDefinitionIds);
        Assert.DoesNotContain("pet-thanh-xa", petDefinitionIds);
        Assert.DoesNotContain("pet-son-hung", petDefinitionIds);

        // Every granted Card is Category = Basic (CARD_RULES.md §1 item 4).
        foreach (var cardDefinitionId in cardDefinitionIds)
        {
            var definition = await context.CardDefinitions
                .SingleAsync(d => d.CardDefinitionId == cardDefinitionId);

            Assert.Equal(GameServer.Domain.Cards.CardCategory.Basic, definition.Category);
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static async Task<string> AuthenticateAsync(HttpClient client)
    {
        var body = await AuthenticateBodyAsync(client);
        return body.GetProperty("playerId").GetString()!;
    }

    /// <summary>
    /// Authenticates and returns the issued session token, so a test can present
    /// it through <c>API_CONTRACTS.md</c> §2.8's documented transport
    /// (<c>Authorization: Bearer &lt;sessionToken&gt;</c>) rather than injecting
    /// an identity.
    /// </summary>
    private static async Task<string> AuthenticateWithSessionAsync(HttpClient client)
    {
        var body = await AuthenticateBodyAsync(client);
        return body.GetProperty("sessionToken").GetString()!;
    }

    private static async Task<JsonElement> AuthenticateBodyAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/discord",
            new DiscordAuthRequest("authorization-code"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> GetArrayAsync(HttpClient client, string route)
    {
        var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>
    /// A host running the production authentication pipeline over an isolated
    /// in-memory store, with the provisioned starter content present so the
    /// bootstrap resolves its definitions exactly as it does in production.
    /// </summary>
    private sealed class StarterFactory : WebApplicationFactory<Program>
    {
        private readonly string _discordUserId;
        private readonly string _storeName = $"starter-{Guid.NewGuid():N}";

        public StarterFactory(string discordUserId)
        {
            _discordUserId = discordUserId;
        }

        /// <summary>
        /// When set, the stand-in exchange reports the code as rejected
        /// (<c>API_CONTRACTS.md</c> §2.6 rule 2) instead of yielding an identity.
        /// </summary>
        public bool RejectIdentity { get; init; }

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

                services.RemoveAll<IDiscordIdentityResolver>();
                services.AddSingleton<IDiscordIdentityResolver>(
                    new StubIdentityResolver(_discordUserId, RejectIdentity));
            });
        }

        protected override void ConfigureClient(HttpClient client)
        {
            base.ConfigureClient(client);

            // The provisioned content the starter set references. Production has
            // these rows because TASK-085 provisioned them by migration; an
            // in-memory store applies no migration.
            TestProvisionedContent.Seed(Services);
        }
    }

    private sealed class StubIdentityResolver : IDiscordIdentityResolver
    {
        private readonly string _discordUserId;
        private readonly bool _reject;

        public StubIdentityResolver(string discordUserId, bool reject)
        {
            _discordUserId = discordUserId;
            _reject = reject;
        }

        public Task<DiscordIdentityResolution> ResolveAsync(
            string code,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_reject
                ? DiscordIdentityResolution.Failure(401, "DISCORD_AUTH_FAILED", "Discord authentication failed.")
                : DiscordIdentityResolution.Success(new DiscordIdentity(_discordUserId)));
    }
}
