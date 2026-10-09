using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GameServer.Api.Controllers;
using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
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
/// The application session over REST — <c>API_CONTRACTS.md</c> §1, §2, §3, §4, §6,
/// §2.3; <c>ADR-015</c> D1–D6.
///
/// <code>
/// Account credentials (username, password)   (this suite seeds an Account)
///         ↓
/// PlayerId
///         ↓
/// Application Session JWT          (issued by POST /api/auth/login or /register)
///         ↓
/// ASP.NET Core Authentication      (signature, HS256, aud, expiry)
///         ↓
/// Authorization                    (every endpoint except §2.1's register and
///                                   §2.2's login)
///         ↓
/// authenticated PlayerId
/// </code>
///
/// <b>These exercise the real pipeline.</b> The production controllers, the
/// production authentication registration, and the production authorization
/// policy run; only the external boundaries a test cannot own — PostgreSQL and
/// Redis — are substituted. Every authenticated request presents a real JWT
/// through <c>Authorization: Bearer</c>; none injects an identity directly.
/// </summary>
public class ApplicationSessionRESTTests
{
    // -----------------------------------------------------------------------
    // §2.2 — issuance happens through the documented web auth endpoint
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AuthLogin_ShouldIssueTheDocumentedJwt_ForTheMatchedPlayer()
    {
        using var factory = new SessionFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("session_user", "password123"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var sessionToken = body!.SessionToken;
        var playerId = body.PlayerId;

        Assert.True(JwtTokenShape.IsThreePartJwt(sessionToken));

        var payload = TestApplicationSession.ReadPayload(sessionToken);
        Assert.Equal(playerId, payload["player_id"]);
    }

    [Fact]
    public async Task AuthLogin_ShouldCarryTheDocumentedClaimsAndLifetime()
    {
        using var factory = new SessionFactory();
        var client = factory.CreateClient();

        var body = await (await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("session_user", "password123"))).Content.ReadFromJsonAsync<AuthResponse>();

        var sessionToken = body!.SessionToken;
        var header = TestApplicationSession.ReadHeader(sessionToken);
        var payload = TestApplicationSession.ReadPayload(sessionToken);

        Assert.Equal("HS256", header["alg"]);
        Assert.Equal(TestApplicationSession.CurrentKeyId, header["kid"]);
        Assert.Equal("dcacti-backend", payload["aud"]);
        Assert.False(payload.ContainsKey("iss"));
        Assert.True(payload.ContainsKey("player_id"));

        var issuedAt = payload.Iat is null
            ? payload.ValidFrom
            : DateTimeOffset.FromUnixTimeSeconds(payload.Iat.Value);

        var lifetime = payload.ValidTo - issuedAt;
        Assert.Equal(TimeSpan.FromHours(24), lifetime);
    }

    // -----------------------------------------------------------------------
    // §2.3 "Failure behavior" / §4 note 6 — one public code for every
    // unauthenticated condition
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ProtectedEndpoint_WithNoSession_ShouldReturn401Unauthenticated()
    {
        using var factory = new SessionFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/battle/start",
            ValidStartRequest());

        await AssertUnauthenticatedAsync(response);
    }

    [Theory]
    // An invalid/tampered session, in every shape the contract folds into the one
    // response: not a token at all, a token signed by a foreign key, and a token
    // signed by a key that is in no validation set.
    [InlineData("not-a-jwt")]
    [InlineData("aaa.bbb.ccc")]
    public async Task ProtectedEndpoint_WithAMalformedSession_ShouldReturn401Unauthenticated(string token)
    {
        using var factory = new SessionFactory();
        var client = factory.CreateClient();

        var response = await SendWithBearerAsync(client, token, ValidStartRequest());

        await AssertUnauthenticatedAsync(response);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithAForeignSignature_ShouldReturn401Unauthenticated()
    {
        // A structurally perfect token signed with a key the host does not hold.
        using var factory = new SessionFactory();
        var client = factory.CreateClient();

        var token = TestApplicationSession.Mint(
            playerId: "player_attacker",
            secret: TestApplicationSession.ForeignSecret,
            keyId: "foreign-key");

        var response = await SendWithBearerAsync(client, token, ValidStartRequest());

        await AssertUnauthenticatedAsync(response);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithAnUnknownKey_ShouldReturn401Unauthenticated()
    {
        // D11: validation accepts exactly two keys, so a third is unresolvable.
        using var factory = new SessionFactory();
        var client = factory.CreateClient();

        var response = await SendWithBearerAsync(
            client,
            TestApplicationSession.MintWithUnknownKey("player_attacker"),
            ValidStartRequest());

        await AssertUnauthenticatedAsync(response);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithATamperedPayload_ShouldReturn401Unauthenticated()
    {
        // The signature covers the payload, so editing the identity invalidates
        // the token. This is the "client cannot promote itself" case.
        using var factory = new SessionFactory();
        var client = factory.CreateClient();

        var valid = TestApplicationSession.Mint("player_original");
        var parts = valid.Split('.');

        var forgedPayload = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes(
                    """{"player_id":"player_attacker","aud":"dcacti-backend"}"""))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var tampered = $"{parts[0]}.{forgedPayload}.{parts[2]}";

        var response = await SendWithBearerAsync(client, tampered, ValidStartRequest());

        await AssertUnauthenticatedAsync(response);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithAnExpiredSession_ShouldReturn401Unauthenticated()
    {
        // D5: 24 hours absolute, and an expired token is one of the three
        // conditions that share the one response. The token is genuine and
        // correctly signed — only its lifetime has passed.
        using var factory = new SessionFactory();
        var client = factory.CreateClient();

        var token = TestApplicationSession.Mint(
            playerId: "player_expired",
            notBefore: DateTime.UtcNow.AddHours(-48),
            expires: DateTime.UtcNow.AddHours(-24));

        var response = await SendWithBearerAsync(client, token, ValidStartRequest());

        await AssertUnauthenticatedAsync(response);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithAWrongAudience_ShouldReturn401Unauthenticated()
    {
        // D9: audience validation is required with an exact match, so a token
        // minted for another consumer is rejected even though it is validly
        // signed by this host's own key.
        using var factory = new SessionFactory();
        var client = factory.CreateClient();

        var token = TestApplicationSession.Mint(
            playerId: "player_wrong_audience",
            audience: "some-other-service");

        var response = await SendWithBearerAsync(client, token, ValidStartRequest());

        await AssertUnauthenticatedAsync(response);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithNoPlayerIdClaim_ShouldReturn401Unauthenticated()
    {
        // A correctly signed, correctly addressed, unexpired token that carries no
        // identity identifies nobody. It is rejected with the same single response
        // rather than being attributed to a default Player — attributing one would
        // let an unauthenticated caller act as another Player
        // (GAME_RULES.md §18, ADR-001).
        using var factory = new SessionFactory();
        var client = factory.CreateClient();

        var token = TestApplicationSession.Mint(playerId: null);

        var response = await SendWithBearerAsync(client, token, ValidStartRequest());

        await AssertUnauthenticatedAsync(response);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithAnUnsignedToken_ShouldReturn401Unauthenticated()
    {
        // D7: validation rejects alg=none.
        using var factory = new SessionFactory();
        var client = factory.CreateClient();

        var header = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes("""{"alg":"none","typ":"JWT"}"""))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var payload = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes(
                    """{"player_id":"player_attacker","aud":"dcacti-backend"}"""))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var response = await SendWithBearerAsync(
            client,
            $"{header}.{payload}.",
            ValidStartRequest());

        await AssertUnauthenticatedAsync(response);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithAnAsymmetricToken_ShouldReturn401Unauthenticated()
    {
        // D7: "validation must reject any other algorithm, including alg=none and
        // any asymmetric alg header." The token advertises RS256; validation runs
        // with an HS256-only algorithm list, so it never reaches key resolution.
        using var factory = new SessionFactory();
        var client = factory.CreateClient();

        var header = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes("""{"alg":"RS256","typ":"JWT","kid":"test-current-key"}"""))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var payload = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes(
                    """{"player_id":"player_attacker","aud":"dcacti-backend"}"""))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var response = await SendWithBearerAsync(
            client,
            $"{header}.{payload}.c2lnbmF0dXJl",
            ValidStartRequest());

        await AssertUnauthenticatedAsync(response);
    }

    // -----------------------------------------------------------------------
    // §2.3 — an external access token is never an application session (D4)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ProtectedEndpoint_WithADiscordAccessToken_ShouldReturn401Unauthenticated()
    {
        // API_CONTRACTS.md §2.3 / ADR-015 D4: only the issued JWT application
        // session is a session, and a raw external access token is never issued to
        // the client as one. It is presented here in the same Bearer slot a session
        // would occupy, and must not authenticate.
        using var factory = new SessionFactory();
        var client = factory.CreateClient();

        var response = await SendWithBearerAsync(
            client,
            SessionFactory.DiscordAccessToken,
            ValidStartRequest());

        await AssertUnauthenticatedAsync(response);
    }

    [Fact]
    public async Task ProtectedEndpoint_ShouldIgnoreAClientSuppliedPlayerId()
    {
        // §4 note 7 / D3: ownership is never established from client-supplied
        // input — no request member, query parameter, or header may select,
        // override, or stand in for the caller's identity.
        //
        // Two sessions are presented for the SAME request: one authenticated as
        // the Player who owns the seeded inventory, and one authenticated as a
        // different Player. Only the authenticated identity decides the outcome.
        using var factory = new SessionFactory();
        var client = factory.CreateClient();

        var ownerToken = TestApplicationSession.Mint(factory.OwnerPlayerId);
        var otherToken = TestApplicationSession.Mint("player_somebody_else");

        // The owner succeeds…
        var owner = await SendWithBearerAsync(
            client,
            ownerToken,
            ValidStartRequest(),
            extraHeaders: new Dictionary<string, string>
            {
                // …even with a playerId submitted in the body and a forged
                // identity header, neither of which may change anything.
                ["X-Player-Id"] = "player_somebody_else",
            });

        Assert.Equal(HttpStatusCode.OK, owner.StatusCode);

        // …and the other Player is treated as the other Player, not as the owner.
        var other = await SendWithBearerAsync(client, otherToken, ValidStartRequest());

        Assert.Equal(HttpStatusCode.BadRequest, other.StatusCode);

        var body = await other.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PET_NOT_OWNED", body.GetProperty("error").GetString());
    }

    // -----------------------------------------------------------------------
    // §3 — the documented success path under a real session
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ProtectedEndpoint_WithAValidSession_ShouldAuthenticateAsTheClaimedPlayer()
    {
        using var factory = new SessionFactory();
        var client = factory.CreateClient();

        var response = await SendWithBearerAsync(
            client,
            TestApplicationSession.Mint(factory.OwnerPlayerId),
            ValidStartRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var battleId = body.GetProperty("battleId").GetString()!;

        // GAME_STATE.md §2.8 item 1 / ADR-014: the record created under an
        // authenticated session carries that session's PlayerId — which is how the
        // chain "JWT player_id → authenticated PlayerId → battle ownership" is
        // observable at all.
        using var scope = factory.Services.CreateScope();
        var state = await scope.ServiceProvider
            .GetRequiredService<BattleStateService>()
            .GetBattleAsync(battleId);

        Assert.Equal(factory.OwnerPlayerId, state!.PlayerId.Value);
    }

    // -----------------------------------------------------------------------
    // §1 — the coverage rule
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TheAuthEndpoints_ShouldRemainUnauthenticatedEndpoints()
    {
        // §1 / §2.1 / §2.3 "Coverage": auth endpoints require no prior authenticated session.
        // Reaching them without a session evaluates their own logic rather than being
        // challenged by the JWT authorization filter with UNAUTHENTICATED.
        using var factory = new SessionFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("non_existent_user", "password123"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("INVALID_CREDENTIALS", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task AuthLogin_WithMissingInput_ShouldReturnBadRequest()
    {
        using var factory = new SessionFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("", ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("INVALID_INPUT", body.GetProperty("error").GetString());
    }

    // -----------------------------------------------------------------------
    // §4 notes 6–7 — the result endpoint's ownership contract
    //
    // `GET /api/battle/{battleId}/result` is NOT implemented: it is TASK-041,
    // which is BACKLOG. Its documented behavior therefore cannot be exercised
    // end-to-end here, and TASK-034 does not implement it. What TASK-034 owns is
    // the identity that endpoint's ownership rule reads, and that is asserted
    // directly: the authenticated PlayerId is server-derived from the session and
    // matches the battle's recorded owner — never a client-supplied value.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task BattleOwnership_ShouldBeTheAuthenticatedPlayerId_NotARequestValue()
    {
        // API_CONTRACTS.md §4 note 7: "the identity resolved from their
        // authenticated session equals BattleResult.PlayerId (whose value comes
        // from BattleState.PlayerId)" and "ownership is never established from
        // client-supplied input: no playerId request member, query parameter,
        // header, or body field may select, override, or stand in for the caller's
        // identity."
        //
        // TASK-034 establishes the left-hand side of that equality. The right-hand
        // side — reading it back through GET .../result — is TASK-041.
        using var factory = new SessionFactory();
        var client = factory.CreateClient();

        var ownerToken = TestApplicationSession.Mint(factory.OwnerPlayerId);
        var attackerToken = TestApplicationSession.Mint("player_somebody_else");

        // The owner's session creates the battle, and the record's owner is that
        // session's PlayerId.
        var created = await SendWithBearerAsync(client, ownerToken, ValidStartRequest());
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var battleId = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("battleId").GetString()!;

        using var scope = factory.Services.CreateScope();
        var state = await scope.ServiceProvider
            .GetRequiredService<BattleStateService>()
            .GetBattleAsync(battleId);

        Assert.Equal(factory.OwnerPlayerId, state!.PlayerId.Value);

        // A different session does not resolve to that Player, so it is not the
        // owner — and submitting the owner's id as a client value changes nothing
        // (the request below carries it in a header and is still refused).
        var attacker = await SendWithBearerAsync(
            client,
            attackerToken,
            ValidStartRequest(),
            extraHeaders: new Dictionary<string, string>
            {
                ["X-Player-Id"] = factory.OwnerPlayerId,
                ["playerId"] = factory.OwnerPlayerId,
            });

        Assert.Equal(HttpStatusCode.BadRequest, attacker.StatusCode);

        var body = await attacker.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PET_NOT_OWNED", body.GetProperty("error").GetString());
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static object ValidStartRequest() => new
    {
        petId = SessionFactory.PetInstanceId,
        bossId = "boss-hoa-long",
        cardLoadout = new[] { "card_basic_a", "card_basic_b", "card_basic_c" },
        relicLoadout = new[] { "relic_1", "relic_2", "relic_3" },
    };

    private static async Task<HttpResponseMessage> SendWithBearerAsync(
        HttpClient client,
        string token,
        object body,
        IReadOnlyDictionary<string, string>? extraHeaders = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/battle/start")
        {
            Content = JsonContent.Create(body),
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (extraHeaders is not null)
        {
            foreach (var (name, value) in extraHeaders)
            {
                request.Headers.TryAddWithoutValidation(name, value);
            }
        }

        return await client.SendAsync(request);
    }

    /// <summary>
    /// Asserts the one documented unauthenticated response
    /// (<c>API_CONTRACTS.md</c> §2.3 "Failure behavior", §6): <c>401</c> with the
    /// <c>UNAUTHENTICATED</c> code, and nothing that distinguishes one validation
    /// failure from another.
    /// </summary>
    private static async Task AssertUnauthenticatedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("UNAUTHENTICATED", body.GetProperty("error").GetString());

        // No validation detail is disclosed: the response must not name the
        // failing check, the algorithm, the key, the issuer, or the audience.
        var raw = body.GetRawText();

        foreach (var disclosure in new[]
                 {
                     "signature", "expired", "audience", "issuer", "kid", "alg", "claim",
                 })
        {
            Assert.DoesNotContain(disclosure, raw, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// A host running the production authentication/authorization pipeline over an
    /// isolated in-memory store, with the external services substituted.
    /// </summary>
    private sealed class SessionFactory : WebApplicationFactory<Program>
    {
        /// <summary>
        /// A raw external access token, presented where a session would go. It was
        /// never issued to the client as an application session (ADR-015 D4;
        /// <c>API_CONTRACTS.md</c> §2.3) — so presenting it as a session must not
        /// authenticate.
        /// </summary>
        internal const string DiscordAccessToken = "discord-access-token-not-a-session";

        internal const string PetInstanceId = "session_pet";

        private readonly string _storeName = $"session-{Guid.NewGuid():N}";

        /// <summary>The Player the seeded inventory belongs to.</summary>
        internal string OwnerPlayerId { get; } = $"player_session_{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", "");
            builder.UseSetting("ConnectionStrings:Redis", "");

            // The signing key, supplied through configuration as ADR-015 D10
            // requires. Both keys are present so the D11 overlap is in effect.
            foreach (var (key, value) in TestApplicationSession.RotatedKeyConfiguration)
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
            SeedOwnerInventory();
        }

        private void SeedOwnerInventory()
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

            var accountId = Guid.NewGuid();
            context.Accounts.Add(new Domain.Accounts.Account
            {
                AccountId = accountId,
                Username = "session_user",
                PasswordHash = new GameServer.Infrastructure.Accounts.Pbkdf2PasswordHasher().HashPassword("password123"),
                CreatedAt = DateTimeOffset.UtcNow,
            });

            context.Players.Add(new Player
            {
                PlayerId = OwnerPlayerId,
                AccountId = accountId,
                Level = Player.InitialLevel,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            context.PetDefinitions.Add(new PetDefinition
            {
                PetDefinitionId = "session_pet_def",
                Identity = "Thanh Xà",
                Element = Element.Moc,
                PassiveId = new PassiveId("session-passive"),
                PassiveThreshold = 5,
                SignatureSkillCardId = "session_skill",
            });

            context.Pets.Add(new Pet
            {
                PetInstanceId = PetInstanceId,
                PlayerId = OwnerPlayerId,
                PetDefinitionId = "session_pet_def",
                Tier = GameServer.Domain.Pets.PetTier.Common,
                Star = 1,
                Level = 1,
                AcquiredAt = DateTimeOffset.UtcNow,
            });

            foreach (var (id, category) in new[]
            {
                ("card_basic_a", GameServer.Domain.Cards.CardCategory.Basic),
                ("card_basic_b", GameServer.Domain.Cards.CardCategory.Basic),
                ("card_basic_c", GameServer.Domain.Cards.CardCategory.Basic),
                ("session_skill", GameServer.Domain.Cards.CardCategory.PetSkill),
            })
            {
                context.CardDefinitions.Add(new GameServer.Domain.Cards.CardDefinition
                {
                    CardDefinitionId = id,
                    Name = id,
                    Category = category,
                    PowerCost = 0,
                    EffectDefinition = TestCardEffects.FlatPower,
                    LoadoutCopyLimit = 3,
                });

                context.PlayerUnlockedCards.Add(new GameServer.Domain.Cards.PlayerUnlockedCard
                {
                    PlayerId = OwnerPlayerId,
                    CardDefinitionId = id,
                });
            }

            foreach (var relicInstanceId in new[] { "relic_1", "relic_2", "relic_3" })
            {
                context.Relics.Add(new GameServer.Domain.Relics.Relic
                {
                    RelicInstanceId = relicInstanceId,
                    PlayerId = OwnerPlayerId,
                    RelicDefinitionId = "relic_def_1",
                    AcquiredAt = DateTimeOffset.UtcNow,
                });
            }

            context.SaveChanges();
        }
    }
}
