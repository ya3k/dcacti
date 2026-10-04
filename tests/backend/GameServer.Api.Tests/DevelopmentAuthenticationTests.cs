using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GameServer.Application.Battle;
using GameServer.Application.Identity;
using GameServer.Application.Players;
using GameServer.Domain.Players;
using GameServer.Infrastructure;
using GameServer.Infrastructure.Discord;
using GameServer.Infrastructure.Postgres;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace GameServer.Api.Tests;

/// <summary>
/// The development-only authentication path (TASK-181).
///
/// <code>
/// Development + explicit opt-in
///         ↓
/// the fixed development DiscordUserId         (nothing in the request selects it)
///         ↓
/// IDiscordIdentityResolver seam               (the existing boundary)
///         ↓
/// IPlayerRepository.GetOrCreateByDiscordUserIdAsync   (the real Player path)
///         ↓
/// ApplicationSessionTokenService.Issue        (the existing issuer)
///         ↓
/// the normal authorization pipeline
/// </code>
///
/// <b>What is asserted, and why it is asserted this way.</b> The development path
/// is a way to <i>obtain</i> a session, so nothing here inspects how it is
/// implemented: the assertions are on the observable contract — the session is a
/// real JWT carrying the documented <c>player_id</c>, the Player row is real and
/// carries the documented starter ownership, the session authenticates through the
/// production pipeline, and every condition that should disable the path (not
/// Development, no opt-in, an ordinary authorization code, no session at all)
/// produces the unchanged answers.
/// </summary>
public class DevelopmentAuthenticationTests
{
    // =======================================================================
    // The switch
    // =======================================================================

    /// <summary>
    /// The development switch is off unless a host sets it (TASK-181 AC-02:
    /// "never a default").
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("false")]
    [InlineData("False")]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData("enabled")]
    public void TheDevelopmentSwitch_ShouldBeOff_ForEveryValueOtherThanAnExplicitTrue(string? value)
    {
        var configuration = BuildConfiguration(value);

        Assert.False(DevelopmentAuthenticationOptions.IsEnabled(configuration));
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    public void TheDevelopmentSwitch_ShouldBeOn_WhenExplicitlyEnabled(string value)
    {
        var configuration = BuildConfiguration(value);

        Assert.True(DevelopmentAuthenticationOptions.IsEnabled(configuration));
    }

    // =======================================================================
    // Activation is Development AND opt-in — nothing else
    // =======================================================================

    /// <summary>
    /// The four activation combinations, asserted on the resolver the composition
    /// actually hands to <c>AuthController</c>.
    ///
    /// The Development identity is reachable in exactly one of them. In particular
    /// a host that is <b>not</b> Development cannot reach it <i>even with the
    /// switch set</i>, which is the property that makes the switch safe to leave in
    /// a configuration file (AC-01/AC-02).
    /// </summary>
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public async Task TheDevelopmentIdentity_ShouldBeReachable_OnlyInDevelopmentWithTheOptIn(
        bool isDevelopmentEnvironment,
        bool enabled,
        bool expected)
    {
        var resolver = BuildResolver(isDevelopmentEnvironment, enabled);

        var resolution = await resolver.ResolveAsync(DevelopmentAuthenticationOptions.AuthorizationCode);

        Assert.Equal(expected, resolution.Succeeded);

        if (expected)
        {
            Assert.Equal(
                DevelopmentAuthenticationOptions.DiscordUserId,
                resolution.Identity.DiscordUserId);
        }
        else
        {
            // The unchanged exchange answer — the same one the unconfigured
            // boundary gives. No identity, so no Player can be written from it
            // (API_CONTRACTS.md §2.6 rule 5).
            Assert.Equal(503, resolution.StatusCode);
            Assert.Equal("DISCORD_UNAVAILABLE", resolution.Error);
        }
    }

    /// <summary>
    /// The development identity is the fixed server-owned value: it is not the
    /// authorization code, and the code cannot influence it (AC-05).
    /// </summary>
    [Fact]
    public async Task TheDevelopmentIdentity_ShouldBeFixed_AndNeverDerivedFromTheCode()
    {
        var resolver = BuildResolver(isDevelopmentEnvironment: true, enabled: true);

        var resolution = await resolver.ResolveAsync(DevelopmentAuthenticationOptions.AuthorizationCode);

        Assert.True(resolution.Succeeded);
        Assert.Equal(DevelopmentAuthenticationOptions.DiscordUserId, resolution.Identity.DiscordUserId);
        Assert.NotEqual(DevelopmentAuthenticationOptions.AuthorizationCode, resolution.Identity.DiscordUserId);

        // Two resolutions are the same identity, so repeated runs address the same
        // Player (AC-05).
        var again = await resolver.ResolveAsync(DevelopmentAuthenticationOptions.AuthorizationCode);
        Assert.Equal(resolution.Identity.DiscordUserId, again.Identity.DiscordUserId);
    }

    /// <summary>
    /// An ordinary authorization code keeps the unchanged "exchange unavailable"
    /// answer even while the development path is enabled, so the TASK-035 boundary
    /// is not shadowed by it in Development.
    /// </summary>
    [Fact]
    public async Task AnOrdinaryAuthorizationCode_ShouldRemainUnavailable_EvenInDevelopment()
    {
        var resolver = BuildResolver(isDevelopmentEnvironment: true, enabled: true);

        var resolution = await resolver.ResolveAsync("a-real-discord-authorization-code");

        Assert.False(resolution.Succeeded);
        Assert.Equal(503, resolution.StatusCode);
        Assert.Equal("DISCORD_UNAVAILABLE", resolution.Error);
    }

    // =======================================================================
    // The session, the Player, and the pipeline
    // =======================================================================

    /// <summary>
    /// The development path issues the documented session for a real persisted
    /// Player (AC-06/AC-07).
    /// </summary>
    [Fact]
    public async Task DevelopmentAuthentication_ShouldIssueTheDocumentedSession_ForARealPersistedPlayer()
    {
        using var factory = new DevelopmentAuthFactory();
        var client = factory.CreateClient();

        var response = await AuthenticateDevelopmentAsync(client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var sessionToken = body.GetProperty("sessionToken").GetString();
        var playerId = body.GetProperty("playerId").GetString();

        // API_CONTRACTS.md §2.8: the artifact is the issued JWT, not a placeholder.
        Assert.True(JwtTokenShape.IsThreePartJwt(sessionToken));

        var header = TestApplicationSession.ReadHeader(sessionToken!);
        var payload = TestApplicationSession.ReadPayload(sessionToken!);

        // ADR-015 D7/D11: the same algorithm and the same kid as any other session.
        Assert.Equal("HS256", header["alg"]);
        Assert.Equal(TestApplicationSession.CurrentKeyId, header["kid"]);

        // ADR-015 D3/D8/D9: player_id is the Player's own id, aud is the one
        // documented value, and no iss exists.
        Assert.Equal(playerId, payload["player_id"]);
        Assert.Equal("dcacti-backend", payload["aud"]);
        Assert.False(payload.ContainsKey("iss"));

        // D5: 24 hours absolute, measured from the issuing instant the issuer
        // writes as `nbf`.
        Assert.Equal(TimeSpan.FromHours(24), payload.ValidTo - payload.ValidFrom);

        // The identity is the development Player's — it is neither the Discord
        // identity nor the authorization code (§2.4, D3).
        Assert.NotEqual(DevelopmentAuthenticationOptions.AuthorizationCode, payload["player_id"]?.ToString());
        Assert.NotEqual(DevelopmentAuthenticationOptions.DiscordUserId, payload["player_id"]?.ToString());

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        // A real Player row, keyed on the fixed development identity, created
        // through the normal repository path (DATABASE.md §1).
        var player = await context.Players.SingleAsync(
            candidate => candidate.DiscordUserId == DevelopmentAuthenticationOptions.DiscordUserId);

        Assert.Equal(playerId, player.PlayerId);
        Assert.Equal(Player.InitialLevel, player.Level);
        Assert.Equal(1, player.Level);

        // DATABASE.md §2 item 1: the creation branch composes the documented
        // starter ownership set, unmodified and not duplicated here.
        Assert.Equal(1, await context.Pets.CountAsync(pet => pet.PlayerId == playerId));
        Assert.Equal(3, await context.PlayerUnlockedCards.CountAsync(card => card.PlayerId == playerId));
        Assert.Equal(3, await context.Relics.CountAsync(relic => relic.PlayerId == playerId));

        var starterPet = await context.Pets.SingleAsync(pet => pet.PlayerId == playerId);

        Assert.Equal(PlayerStarterGrantFactory.StarterPetDefinitionId, starterPet.PetDefinitionId);
    }

    /// <summary>
    /// The development identity is deterministic across repeated runs and across
    /// host restarts: the same fixed identity, and therefore at most one
    /// development Player (AC-05, DATABASE.md §3).
    /// </summary>
    [Fact]
    public async Task DevelopmentAuthentication_ShouldBeDeterministic_AndCreateNoDuplicatePlayer()
    {
        using var first = new DevelopmentAuthFactory();
        var firstClient = first.CreateClient();

        var firstPlayerId = await AuthenticateDevelopmentPlayerIdAsync(firstClient);
        var secondPlayerId = await AuthenticateDevelopmentPlayerIdAsync(firstClient);

        Assert.Equal(firstPlayerId, secondPlayerId);

        using (var scope = first.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

            Assert.Equal(
                1,
                await context.Players.CountAsync(
                    candidate => candidate.DiscordUserId == DevelopmentAuthenticationOptions.DiscordUserId));
        }

        // A second, independent host (a restart, with its own empty store) resolves
        // the same development identity — so the local account is stable across
        // restarts, not merely within one process.
        using var second = new DevelopmentAuthFactory();
        var secondClient = second.CreateClient();

        await AuthenticateDevelopmentAsync(secondClient);

        using (var scope = second.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            var player = await context.Players.SingleAsync();

            Assert.Equal(DevelopmentAuthenticationOptions.DiscordUserId, player.DiscordUserId);
        }
    }

    /// <summary>
    /// The development session is accepted by the normal authorization pipeline,
    /// and the endpoints around it keep their normal requirements (AC-08).
    /// </summary>
    [Fact]
    public async Task TheDevelopmentSession_ShouldAuthenticateThroughTheNormalPipeline()
    {
        using var factory = new DevelopmentAuthFactory();
        var client = factory.CreateClient();

        var sessionToken = await AuthenticateDevelopmentTokenAsync(client);

        // §1 / §2.8 "Coverage": the collection endpoints require the session and
        // act for the Player it identifies.
        foreach (var path in new[] { "/api/pets", "/api/cards", "/api/relics" })
        {
            var authenticated = await GetWithBearerAsync(client, path, sessionToken);

            Assert.Equal(HttpStatusCode.OK, authenticated.StatusCode);
        }

        // …and the same endpoints without a session are still the one documented
        // unauthenticated answer — no endpoint became anonymous, and no policy was
        // relaxed for the development path.
        foreach (var path in new[] { "/api/pets", "/api/cards", "/api/relics" })
        {
            var anonymous = await client.GetAsync(path);

            await AssertUnauthenticatedAsync(anonymous);
        }

        var anonymousStart = await client.PostAsJsonAsync("/api/battle/start", new { });
        await AssertUnauthenticatedAsync(anonymousStart);
    }

    /// <summary>
    /// The development authentication cannot be pointed at an existing Player: no
    /// request member participates in the identity (AC-05, §2.8 "Identity").
    /// </summary>
    [Fact]
    public async Task DevelopmentAuthentication_ShouldIgnoreClientSuppliedIdentity()
    {
        using var factory = new DevelopmentAuthFactory();
        var client = factory.CreateClient();

        // An unrelated, already-existing account, whose identifiers the request
        // below offers as its own.
        const string otherDiscordUserId = "80351110224678999";
        const string otherPlayerId = "player_somebody_else";

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

            context.Players.Add(new Player
            {
                PlayerId = otherPlayerId,
                DiscordUserId = otherDiscordUserId,
                Level = Player.InitialLevel,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            await context.SaveChangesAsync();
        }

        var response = await AuthenticateDevelopmentAsync(
            client,
            body: new
            {
                code = DevelopmentAuthenticationOptions.AuthorizationCode,
                playerId = otherPlayerId,
                discordUserId = otherDiscordUserId,
            },
            extraHeaders: new Dictionary<string, string> { ["X-Player-Id"] = otherPlayerId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var playerId = body.GetProperty("playerId").GetString();
        var sessionToken = body.GetProperty("sessionToken").GetString();

        // The caller did not become the account it named…
        Assert.NotEqual(otherPlayerId, playerId);
        Assert.Equal(playerId, TestApplicationSession.ReadPayload(sessionToken!)["player_id"]);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

            // …the development identity resolved to the development Player…
            var resolved = await context.Players.SingleAsync(candidate => candidate.PlayerId == playerId);
            Assert.Equal(DevelopmentAuthenticationOptions.DiscordUserId, resolved.DiscordUserId);

            // …and the named account is untouched.
            var untouched = await context.Players.SingleAsync(candidate => candidate.PlayerId == otherPlayerId);
            Assert.Equal(otherDiscordUserId, untouched.DiscordUserId);
        }
    }

    /// <summary>
    /// The documented loop is reachable with the development session: the
    /// collection reads return the Player's own starter ownership, and the battle
    /// starts under that same session with the battle owned by that Player
    /// (AC-09/AC-10/AC-11).
    /// </summary>
    [Fact]
    public async Task CollectionsAndBattleStart_ShouldWorkUnderTheDevelopmentSession()
    {
        using var factory = new DevelopmentAuthFactory();
        var client = factory.CreateClient();

        var sessionToken = await AuthenticateDevelopmentTokenAsync(client);

        var pets = await GetJsonAsync<JsonElement>(client, "/api/pets", sessionToken);
        var cards = await GetJsonAsync<JsonElement>(client, "/api/cards", sessionToken);
        var relics = await GetJsonAsync<JsonElement>(client, "/api/relics", sessionToken);

        // The starter ownership set the creation branch granted, read back through
        // the normal §5 reads — not fabricated client-side data.
        Assert.Equal(1, pets.GetArrayLength());
        Assert.Equal(3, cards.GetArrayLength());
        Assert.Equal(3, relics.GetArrayLength());

        var petId = pets[0].GetProperty("petId").GetString();
        var cardLoadout = cards.EnumerateArray().Select(card => card.GetProperty("cardId").GetString()!).ToArray();
        var relicLoadout = relics.EnumerateArray().Select(relic => relic.GetProperty("relicId").GetString()!).ToArray();

        var start = new HttpRequestMessage(HttpMethod.Post, "/api/battle/start")
        {
            Content = JsonContent.Create(new
            {
                petId,
                bossId = "boss-hoa-long",
                cardLoadout,
                relicLoadout,
            }),
        };

        start.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);

        var response = await client.SendAsync(start);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var startBody = await response.Content.ReadFromJsonAsync<JsonElement>();
        var battleId = startBody.GetProperty("battleId").GetString()!;

        // GAME_STATE.md §2.8 item 1 / ADR-014: the battle's owner is the session's
        // PlayerId — the development session established ownership exactly as any
        // other session does.
        using var scope = factory.Services.CreateScope();
        var state = await scope.ServiceProvider
            .GetRequiredService<BattleStateService>()
            .GetBattleAsync(battleId);

        Assert.NotNull(state);
        Assert.Equal(
            await ReadDevelopmentPlayerIdAsync(factory),
            state!.PlayerId.Value);
    }

    // =======================================================================
    // Production safety
    // =======================================================================

    /// <summary>
    /// Outside Development the development path is unreachable — <b>even when the
    /// opt-in setting is present</b> (AC-01/AC-02). This is the case a
    /// configuration-only switch could not defend: the setting is set, the
    /// environment is not Development, and the answer is the unchanged exchange
    /// failure with no Player written.
    /// </summary>
    [Fact]
    public async Task Production_ShouldNotAcceptTheDevelopmentAuthenticationPath()
    {
        using var factory = new DevelopmentAuthFactory
        {
            EnvironmentName = "Production",
            EnableDevelopmentAuthentication = true,
        };

        var client = factory.CreateClient();

        var response = await AuthenticateDevelopmentAsync(client);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("DISCORD_UNAVAILABLE", body.GetProperty("error").GetString());
        Assert.False(body.TryGetProperty("sessionToken", out _));
        Assert.False(body.TryGetProperty("playerId", out _));

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        // No Player is created on any failure path (API_CONTRACTS.md §2.6 rule 5).
        Assert.Equal(0, await context.Players.CountAsync());

        // And the covered endpoints are still covered.
        await AssertUnauthenticatedAsync(await client.PostAsJsonAsync("/api/battle/start", new { }));
    }

    /// <summary>
    /// Without the opt-in, Development behaves exactly as it does today: the
    /// development identity is not reachable and the Discord boundary is unchanged.
    /// </summary>
    [Fact]
    public async Task DevelopmentWithoutTheOptIn_ShouldNotAcceptTheDevelopmentAuthenticationPath()
    {
        using var factory = new DevelopmentAuthFactory { EnableDevelopmentAuthentication = false };
        var client = factory.CreateClient();

        var response = await AuthenticateDevelopmentAsync(client);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        Assert.Equal(0, await context.Players.CountAsync());
    }

    // =======================================================================
    // Helpers
    // =======================================================================

    private static IConfiguration BuildConfiguration(string? enabledValue) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>(DevelopmentAuthenticationOptions.EnabledPath, enabledValue)])
            .Build();

    /// <summary>
    /// The resolver the production composition hands to <c>AuthController</c> for
    /// one activation combination — composed through the real
    /// <c>AddInfrastructureServices</c> plus the development gate, so the
    /// substitution this asserts is the substitution the host performs.
    /// </summary>
    private static IDiscordIdentityResolver BuildResolver(bool isDevelopmentEnvironment, bool enabled)
    {
        var configuration = BuildConfiguration(enabled ? "true" : null);

        var services = new ServiceCollection();

        services.AddInfrastructureServices(configuration);
        services.AddDevelopmentDiscordIdentityResolver(configuration, isDevelopmentEnvironment);

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IDiscordIdentityResolver>();
    }

    private static async Task<HttpResponseMessage> AuthenticateDevelopmentAsync(
        HttpClient client,
        object? body = null,
        IReadOnlyDictionary<string, string>? extraHeaders = null)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/auth/discord")
        {
            Content = JsonContent.Create(body ?? new { code = DevelopmentAuthenticationOptions.AuthorizationCode }),
        };

        if (extraHeaders is not null)
        {
            foreach (var (name, value) in extraHeaders)
            {
                message.Headers.TryAddWithoutValidation(name, value);
            }
        }

        return await client.SendAsync(message);
    }

    /// <summary>Authenticates through the development path and returns the session token.</summary>
    private static async Task<string> AuthenticateDevelopmentTokenAsync(HttpClient client)
    {
        var response = await AuthenticateDevelopmentAsync(client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        return body.GetProperty("sessionToken").GetString()!;
    }

    /// <summary>Authenticates through the development path and returns the resolved PlayerId.</summary>
    private static async Task<string> AuthenticateDevelopmentPlayerIdAsync(HttpClient client)
    {
        var response = await AuthenticateDevelopmentAsync(client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        return body.GetProperty("playerId").GetString()!;
    }

    private static async Task<HttpResponseMessage> GetWithBearerAsync(
        HttpClient client,
        string path,
        string sessionToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);

        return await client.SendAsync(request);
    }

    private static async Task<T> GetJsonAsync<T>(HttpClient client, string path, string sessionToken)
    {
        var response = await GetWithBearerAsync(client, path, sessionToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<string> ReadDevelopmentPlayerIdAsync(DevelopmentAuthFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        var player = await context.Players.SingleAsync(
            candidate => candidate.DiscordUserId == DevelopmentAuthenticationOptions.DiscordUserId);

        return player.PlayerId;
    }

    /// <summary>
    /// The one documented unauthenticated response (<c>API_CONTRACTS.md</c> §2.8
    /// "Failure behavior", §6).
    /// </summary>
    private static async Task AssertUnauthenticatedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("UNAUTHENTICATED", body.GetProperty("error").GetString());
    }

    /// <summary>
    /// A host running the production composition and pipeline over an isolated
    /// in-memory store, with the environment and the development opt-in under the
    /// test's control. Only the external stores are substituted (PostgreSQL and
    /// Redis); the authentication registration, the development gate, the
    /// controllers, and the authorization pipeline are the production ones.
    /// </summary>
    private sealed class DevelopmentAuthFactory : WebApplicationFactory<Program>
    {
        private readonly string _storeName = $"development-auth-{Guid.NewGuid():N}";

        /// <summary>The host environment the development gate is evaluated against.</summary>
        public string EnvironmentName { get; init; } = "Development";

        /// <summary>
        /// Whether the development opt-in setting is present. It is set explicitly
        /// in the Production case too, so the environment condition is what the
        /// assertion rests on.
        /// </summary>
        public bool EnableDevelopmentAuthentication { get; init; } = true;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(EnvironmentName);

            // Blank the connection strings so AddInfrastructureServices does not
            // register the Npgsql provider; this host supplies the isolated store
            // below instead.
            builder.UseSetting("ConnectionStrings:DefaultConnection", "");
            builder.UseSetting("ConnectionStrings:Redis", "");

            // The application session's signing key, supplied through configuration
            // exactly as ADR-015 D10 requires.
            foreach (var (key, value) in TestApplicationSession.CurrentKeyConfiguration)
            {
                builder.UseSetting(key, value);
            }

            // The Discord credential's presence, which a non-Development host now
            // requires before it will start (ADR-019 D4). The Production case below
            // deliberately enables the development opt-in to prove the switch alone
            // cannot arm this path, so its host has to compose — and it can only do
            // so with a credential present. The values are dummies: nothing here
            // performs a Discord exchange, and the gate under test is untouched.
            foreach (var (key, value) in TestDiscordCredentials.Configuration)
            {
                builder.UseSetting(key, value);
            }

            if (EnableDevelopmentAuthentication)
            {
                builder.UseSetting(DevelopmentAuthenticationOptions.EnabledPath, "true");
            }

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<GameDbContext>>();
                services.RemoveAll<GameDbContext>();
                services.AddDbContext<GameDbContext>(options =>
                    options.UseInMemoryDatabase(_storeName));

                // The active-state store, substituted for the same reason the
                // production composition registers none without a Redis
                // connection string.
                services.AddSingleton<IBattleStateRepository, ApiTestBattleStateRepository>();
            });
        }

        /// <summary>
        /// Supplies the provisioned content the starter-ownership bootstrap
        /// references (DATABASE.md §2 item 1). Production has these rows because
        /// TASK-085 provisioned them by migration; an in-memory store applies no
        /// migration.
        /// </summary>
        protected override void ConfigureClient(HttpClient client)
        {
            base.ConfigureClient(client);
            TestProvisionedContent.Seed(Services);
        }
    }
}
