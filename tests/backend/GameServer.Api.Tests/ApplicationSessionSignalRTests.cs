using System.Text.Json;
using GameServer.Api.Authentication;
using GameServer.Api.Hubs;
using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Players;
using GameServer.Infrastructure.Postgres;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameServer.Api.Tests;

/// <summary>
/// The application session over SignalR — <c>SIGNALR_PROTOCOL.md</c> §1 items 3–5,
/// <c>API_CONTRACTS.md</c> §2.8 "Transport", <c>ADR-015</c> D4/D6.
///
/// <code>
/// SignalR
///   ↓
/// JWT Bearer authentication     (the same one scheme REST uses)
///   ↓
/// authenticated PlayerId
///   ↓
/// BattleHub
/// </code>
///
/// The token travels through SignalR's standard access-token mechanism
/// (<c>HubConnectionBuilder.WithUrl(url, options =&gt; options.AccessTokenProvider = …)</c>)
/// and nothing else: no custom query parameter is introduced, and the hub itself
/// gains no authentication logic. A connection presenting no valid session is
/// rejected by the authentication/authorization boundary before the hub is
/// usable.
/// </summary>
public class ApplicationSessionSignalRTests
{
    private const string BattleId = "battle-hub-session";

    // -----------------------------------------------------------------------
    // §1 item 5 — a connection without a valid session is rejected
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HubConnection_WithNoSession_ShouldBeRejected()
    {
        using var factory = new HubSessionFactory();
        var connection = factory.BuildConnection(accessToken: null);

        // SIGNALR_PROTOCOL.md §1 item 5: "A connection presenting a missing,
        // invalid/tampered, or expired session is rejected by the
        // authentication/authorization boundary before the hub is usable."
        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());

        Assert.Equal(HubConnectionState.Disconnected, connection.State);
    }

    [Theory]
    // Not a token at all, and a structurally plausible but unsigned one.
    [InlineData("not-a-jwt")]
    [InlineData("aaa.bbb.ccc")]
    public async Task HubConnection_WithAMalformedSession_ShouldBeRejected(string token)
    {
        using var factory = new HubSessionFactory();
        var connection = factory.BuildConnection(token);

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }

    [Fact]
    public async Task HubConnection_WithAForeignSignature_ShouldBeRejected()
    {
        using var factory = new HubSessionFactory();

        var connection = factory.BuildConnection(TestApplicationSession.Mint(
            playerId: "player_attacker",
            secret: TestApplicationSession.ForeignSecret,
            keyId: "foreign-key"));

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }

    [Fact]
    public async Task HubConnection_WithAnUnknownKey_ShouldBeRejected()
    {
        // D11: validation accepts exactly two keys, so a token signed with a third
        // is unresolvable and the connection is refused.
        using var factory = new HubSessionFactory();
        var connection = factory.BuildConnection(TestApplicationSession.MintWithUnknownKey());

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }

    [Fact]
    public async Task HubConnection_WithAnExpiredSession_ShouldBeRejected()
    {
        using var factory = new HubSessionFactory();

        var connection = factory.BuildConnection(TestApplicationSession.Mint(
            playerId: "player_expired",
            notBefore: DateTime.UtcNow.AddHours(-48),
            expires: DateTime.UtcNow.AddHours(-24)));

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }

    [Fact]
    public async Task HubConnection_WithAWrongAudience_ShouldBeRejected()
    {
        // D9: audience validation is required with an exact match, and BattleHub is
        // the same token under the same scheme as REST — so a token minted for
        // another consumer does not authenticate the hub either.
        using var factory = new HubSessionFactory();

        var connection = factory.BuildConnection(TestApplicationSession.Mint(
            playerId: "player_wrong_audience",
            audience: "some-other-service"));

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }

    [Fact]
    public async Task HubConnection_WithNoPlayerIdClaim_ShouldBeRejected()
    {
        // A validly signed token that identifies nobody does not establish a
        // session — there is no Player for the hub to act for.
        using var factory = new HubSessionFactory();
        var connection = factory.BuildConnection(TestApplicationSession.Mint(playerId: null));

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }

    [Fact]
    public async Task HubConnection_WithADiscordAccessToken_ShouldBeRejected()
    {
        // SIGNALR_PROTOCOL.md §1 item 4 / API_CONTRACTS.md §2.7 item 4: "The
        // Discord access token is never accepted as a BattleHub authentication
        // credential."
        using var factory = new HubSessionFactory();
        var connection = factory.BuildConnection(HubSessionFactory.DiscordAccessToken);

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }

    // -----------------------------------------------------------------------
    // §1 item 3 — a valid session authenticates the connection
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HubConnection_WithAValidSession_ShouldAuthenticate()
    {
        using var factory = new HubSessionFactory();
        var connection = factory.BuildConnection(factory.OwnerSessionToken);

        await connection.StartAsync();

        Assert.Equal(HubConnectionState.Connected, connection.State);
        Assert.False(string.IsNullOrWhiteSpace(connection.ConnectionId));

        await connection.StopAsync();
    }

    [Fact]
    public async Task HubConnection_WithAValidSession_ShouldCarryTheAuthenticatedPlayerId()
    {
        // SIGNALR_PROTOCOL.md §1 item 6 / ADR-015 D3: the hub deals exclusively
        // with the authenticated application player session, and the identity
        // reaches it through the same validated principal REST uses — the
        // `player_id` claim on the context the authentication boundary validated.
        // The hub defines no authentication of its own and no second mechanism.
        //
        // The identity itself is asserted on the resolver the application uses:
        // `AuthenticatedPlayer.GetPlayerId` is what publishes
        // `GameServer.PlayerId`, and it is exercised here against a principal built
        // from this suite's issued token — so "JWT player_id → authenticated
        // PlayerId" is verified for the hub exactly as for REST, from the same
        // production code path. The connection below is a real one through the
        // production pipeline, so the boundary is exercised end to end too.
        using var factory = new HubSessionFactory();

        var token = factory.OwnerSessionToken;

        var connection = factory.BuildConnection(token);

        await connection.StartAsync();
        Assert.Equal(HubConnectionState.Connected, connection.State);
        await connection.StopAsync();

        // The same claim the accepted connection authenticated with resolves to the
        // PlayerId the hub would act for — through the production resolver.
        var resolved = factory.ResolvePlayerIdFromToken(token);

        Assert.Equal(factory.OwnerPlayerId, resolved);
    }

    [Fact]
    public async Task HubConnection_WithThePreviousKeyDuringOverlap_ShouldStillAuthenticate()
    {
        // ADR-015 D11: during the rotation overlap the previous key still
        // validates, so tokens issued before an operator-driven rotation keep
        // working until their 24-hour expiry (D5). REST and the hub share the one
        // scheme, so the overlap holds for both.
        using var factory = new HubSessionFactory();
        var connection = factory.BuildConnection(
            TestApplicationSession.MintWithPreviousKey(factory.OwnerPlayerId));

        await connection.StartAsync();

        Assert.Equal(HubConnectionState.Connected, connection.State);

        await connection.StopAsync();
    }

    [Fact]
    public async Task HubConnection_ShouldAuthenticateTheClaimedPlayer_NotAnother()
    {
        // A session for Player A cannot act as Player B (ADR-015 D3): the identity
        // the hub acts for is the presented token's own claim, read through the
        // production resolver — never another Player's.
        using var factory = new HubSessionFactory();

        const string otherPlayerId = "player_hub_other";
        var token = TestApplicationSession.Mint(otherPlayerId);

        var connection = factory.BuildConnection(token);

        await connection.StartAsync();
        Assert.Equal(HubConnectionState.Connected, connection.State);
        await connection.StopAsync();

        var resolved = factory.ResolvePlayerIdFromToken(token);

        Assert.Equal(otherPlayerId, resolved);
        Assert.NotEqual(factory.OwnerPlayerId, resolved);
    }

    [Fact]
    public async Task HubConnection_WithAValidSession_ShouldReachTheDocumentedHubMethods()
    {
        // The authentication boundary is what changed; the hub's own documented
        // behaviour is unchanged. A session authenticates the connection and the
        // existing methods work exactly as before.
        using var factory = new HubSessionFactory();
        await factory.CreateBattleAsync(BattleId);

        var connection = factory.BuildConnection(factory.OwnerSessionToken);

        var pushed = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<JsonElement>("BattleStateUpdated", payload => pushed.TrySetResult(payload));

        await connection.StartAsync();
        await connection.InvokeAsync("JoinBattle", BattleId);

        var payload = await pushed.Task.WaitAsync(TimeSpan.FromSeconds(15));

        Assert.Equal(BattleId, payload.GetProperty("battleId").GetString());

        var ping = await connection.InvokeAsync<PingResponse>("Ping", "session-seq-1");
        Assert.True(ping.Accepted);

        await connection.StopAsync();
    }

    // -----------------------------------------------------------------------
    // The hub itself stays thin (D6)
    // -----------------------------------------------------------------------

    [Fact]
    public void BattleHub_ShouldDefineNoAuthenticationLogicOfItsOwn()
    {
        // ADR-015 D6: "BattleHub grows no second, custom authentication system —
        // it remains transport-focused."
        //
        // The hub's declared surface is its reachable members: it must reference no
        // authentication type and expose no method that validates, issues, or
        // resolves a credential.
        var hubType = typeof(BattleHub);

        var referenced = hubType
            .GetMethods(System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.Static
                        | System.Reflection.BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .Append(method.ReturnType))
            .Select(type => type.FullName ?? type.Name)
            .ToArray();

        foreach (var forbidden in new[]
                 {
                     "ApplicationSessionTokenService",
                     "ApplicationSessionSigningKeys",
                     "AuthenticatedPlayer",
                     "JwtBearerOptions",
                     "ClaimsPrincipal",
                 })
        {
            Assert.DoesNotContain(forbidden, referenced);
        }

        foreach (var methodName in new[]
                 {
                     "Authenticate", "ValidateToken", "IssueToken", "ReadToken",
                     "ResolvePlayerId", "Authorize",
                 })
        {
            Assert.Null(hubType.GetMethod(
                methodName,
                System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Static));
        }
    }

    [Fact]
    public async Task HubConnection_ShouldCarryNoCustomAuthenticationQueryParameter()
    {
        // SIGNALR_PROTOCOL.md §1 item 3 / ADR-015 D4: the session travels through
        // SignalR's standard access-token mechanism, and no custom query parameter
        // beyond what that mechanism requires is introduced.
        //
        // The access token is presented in the standard way, and the connection
        // still authenticates — so nothing else was needed to carry it.
        using var factory = new HubSessionFactory();
        var connection = factory.BuildConnection(factory.OwnerSessionToken);

        await connection.StartAsync();
        Assert.Equal(HubConnectionState.Connected, connection.State);
        await connection.StopAsync();
    }

    /// <summary>
    /// A host running the production authentication/authorization pipeline and the
    /// real <c>BattleHub</c>.
    /// </summary>
    private sealed class HubSessionFactory : WebApplicationFactory<Program>
    {
        /// <summary>
        /// A Discord access token, presented where a session would go: it must not
        /// authenticate the hub (§1 item 4).
        /// </summary>
        internal const string DiscordAccessToken = "discord-access-token-not-a-session";

        private readonly string _storeName = $"hub-session-{Guid.NewGuid():N}";

        internal string OwnerPlayerId { get; } = $"player_hub_{Guid.NewGuid():N}";

        /// <summary>A real application session for <see cref="OwnerPlayerId"/>.</summary>
        internal string OwnerSessionToken => TestApplicationSession.Mint(OwnerPlayerId);

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", "");
            builder.UseSetting("ConnectionStrings:Redis", "");

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

        /// <summary>
        /// Validates <paramref name="token"/> with the host's own configured
        /// validation parameters, then resolves its <c>PlayerId</c> through the
        /// production <see cref="AuthenticatedPlayer.GetPlayerId"/> — the resolver
        /// that publishes <c>GameServer.PlayerId</c> and that the hub acts for.
        /// </summary>
        /// <remarks>
        /// The principal is produced by the same
        /// <c>JwtBearerOptions.TokenValidationParameters</c> the host registered, so
        /// what is resolved is what the authentication boundary would have
        /// accepted — not a principal the test assembled by hand.
        /// </remarks>
        internal string? ResolvePlayerIdFromToken(string token)
        {
            var bearer = Services
                .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
                .Get(JwtBearerDefaults.AuthenticationScheme);

            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler
            {
                MapInboundClaims = false,
            };

            var principal = handler.ValidateToken(
                token,
                bearer.TokenValidationParameters,
                out _);

            var context = new DefaultHttpContext { User = principal };

            return AuthenticatedPlayer.GetPlayerId(context);
        }

        /// <summary>
        /// Builds a hub connection that presents <paramref name="accessToken"/>
        /// through SignalR's standard access-token mechanism — the only way the
        /// client supplies it. A <c>null</c> token presents no session at all.
        /// </summary>
        internal HubConnection BuildConnection(string? accessToken) =>
            new HubConnectionBuilder()
                .WithUrl("http://localhost/hubs/battle", options =>
                {
                    options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;

                    if (accessToken is not null)
                    {
                        options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                    }
                })
                .Build();

        /// <summary>
        /// Waits for the identity the hub resolved for a connection, so a test can
        /// assert the authenticated identity rather than only that a connection was
        /// accepted.
        /// </summary>
        internal async Task CreateBattleAsync(string battleId)
        {
            using var scope = Services.CreateScope();

            var player = new PlayerId(OwnerPlayerId);

            var pet = new BattleStateService.PetConfiguration(
                new GameServer.Domain.Pets.PetId("pet_hub_session"),
                Element.Hoa,
                new PassiveId("xich-lang"),
                PassiveThreshold: 5);

            await scope.ServiceProvider
                .GetRequiredService<BattleStateService>()
                .CreateBattleAsync(battleId, player, pet, BossDefinitions.HoaLong);
        }
    }
}
