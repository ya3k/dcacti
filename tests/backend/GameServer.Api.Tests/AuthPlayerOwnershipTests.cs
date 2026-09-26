using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GameServer.Api.Controllers;
using GameServer.Application.Battle;
using GameServer.Application.Identity;
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
/// The authentication ownership boundary — <c>API_CONTRACTS.md</c> §2.5 and
/// <c>DATABASE.md</c> §1.
///
/// <code>
/// Discord identity
///         ↓
/// DiscordUserId
///         ↓
/// Player repository/service
///         ↓
/// find Player by DiscordUserId
///         ↓
/// existing Player → reuse
/// new DiscordUserId → create Player Level 1
/// </code>
///
/// The Discord exchange itself is not exercised here: it is the TASK-035
/// contract's own implementation and is not owned by this task. These tests
/// supply a verified identity through <see cref="IDiscordIdentityResolver"/> —
/// the seam the endpoint consumes — so the Player ownership behaviour is
/// verified without reimplementing, faking, or bypassing the exchange contract.
///
/// The former `player_dev` stub is asserted gone: the endpoint returns the
/// matched-or-created Player's own <c>PlayerId</c>, and no Player write occurs
/// unless a verified identity was obtained.
///
/// Each test constructs its own host, because the identity a test presents —
/// and the store it is matched against — is that test's own fixture, not
/// shared state.
/// </summary>
public class AuthPlayerOwnershipTests
{
    /// <summary>
    /// A Discord snowflake, shaped as <c>GET /users/@me</c> returns it
    /// (<c>API_CONTRACTS.md</c> §2.3 item 3) and unique per call so tests
    /// never share a Player.
    /// </summary>
    private static string NewDiscordUserId() => $"8035{Random.Shared.NextInt64(1_000_000_000_000_000L):D16}";

    [Fact]
    public async Task AuthDiscord_ForANewIdentity_ShouldCreateAPlayerAtLevelOne()
    {
        var discordUserId = NewDiscordUserId();

        using var factory = new AuthFactory(discordUserId);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/discord",
            new DiscordAuthRequest("authorization-code"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // API_CONTRACTS.md §2.5: the response shape is unchanged —
        // `sessionToken` and `playerId` — and `playerId` is the
        // matched-or-created Player's own PlayerId.
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("sessionToken").GetString()));

        var playerId = body.GetProperty("playerId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(playerId));

        // No longer the hard-coded placeholder.
        Assert.NotEqual("player_dev", playerId);

        // The Player was genuinely persisted, at the documented initial Level
        // (PET_RULES.md §5 item 8), keyed on the verified Discord identity.
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        var player = await context.Players.SingleAsync(p => p.DiscordUserId == discordUserId);

        Assert.Equal(playerId, player.PlayerId);
        Assert.Equal(Player.InitialLevel, player.Level);
        Assert.Equal(1, player.Level);
    }

    [Fact]
    public async Task AuthDiscord_ForAnExistingIdentity_ShouldReuseTheSamePlayer_AndPreserveItsLevel()
    {
        var discordUserId = NewDiscordUserId();

        using var factory = new AuthFactory(discordUserId);
        var client = factory.CreateClient();

        var first = await AuthenticateAsync(client);

        // Bring the Player to a progressed Level within the documented range,
        // as an established account's would be.
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            var stored = await context.Players.SingleAsync(p => p.DiscordUserId == discordUserId);

            context.Entry(stored).Property(nameof(Player.Level)).CurrentValue = 23;
            await context.SaveChangesAsync();
        }

        var second = await AuthenticateAsync(client);

        // DATABASE.md §3: a repeated authentication does not create a
        // duplicate Player row — the same Player is returned.
        Assert.Equal(first, second);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

            Assert.Equal(1, await context.Players.CountAsync(p => p.DiscordUserId == discordUserId));

            // An existing Player's Level is preserved: matching a Player is not
            // an update of it, so it is not reset to the creation value.
            var player = await context.Players.SingleAsync(p => p.DiscordUserId == discordUserId);
            Assert.Equal(23, player.Level);
        }
    }

    [Fact]
    public async Task AuthDiscord_WithAMissingCode_ShouldCreateNoPlayer()
    {
        // API_CONTRACTS.md §2.6 rule 5 / §2.7 item 6: a Player row is written
        // only after the identity is successfully verified, so every failure
        // path produces no Player. §2.6 rule 1 keeps INVALID_CODE (400) for a
        // missing code.
        var discordUserId = NewDiscordUserId();

        using var factory = new AuthFactory(discordUserId);
        var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        var response = await client.PostAsJsonAsync(
            "/api/auth/discord",
            new DiscordAuthRequest(""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("INVALID_CODE", body.GetProperty("error").GetString());
        Assert.False(body.TryGetProperty("playerId", out _));

        Assert.Equal(0, await context.Players.CountAsync());
    }

    [Fact]
    public async Task AuthDiscord_WhenIdentityIsUnverified_ShouldCreateNoPlayer()
    {
        // The companion to the case above for a reachable exchange that
        // rejects the code: §2.6 rule 5 requires no Player on any failure
        // path, and §2.6 rule 2 maps a rejected code to 401
        // DISCORD_AUTH_FAILED.
        var discordUserId = NewDiscordUserId();

        using var factory = new AuthFactory(discordUserId) { RejectIdentity = true };
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/discord",
            new DiscordAuthRequest("revoked-authorization-code"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("DISCORD_AUTH_FAILED", body.GetProperty("error").GetString());
        Assert.False(body.TryGetProperty("playerId", out _));

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        Assert.Equal(0, await context.Players.CountAsync());
    }

    private static async Task<string> AuthenticateAsync(HttpClient client)    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/discord",
            new DiscordAuthRequest("authorization-code"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("playerId").GetString()!;
    }

    /// <summary>
    /// A host with Player persistence wired to an isolated in-memory store and
    /// a fixed verified Discord identity standing in for the TASK-035
    /// exchange, so the ownership behaviour is testable without a live
    /// PostgreSQL instance or a Discord round-trip.
    /// </summary>
    private sealed class AuthFactory : WebApplicationFactory<Program>
    {
        private readonly string _discordUserId;
        private readonly string _storeName = $"auth-ownership-{Guid.NewGuid():N}";

        public AuthFactory(string discordUserId)
        {
            _discordUserId = discordUserId;
        }

        /// <summary>
        /// When set, the stand-in exchange reports the code as rejected
        /// (<c>API_CONTRACTS.md</c> §2.6 rule 2) instead of yielding an
        /// identity.
        /// </summary>
        public bool RejectIdentity { get; init; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Blank the connection strings so AddInfrastructureServices does not
            // register the Npgsql provider. A service provider may hold only one
            // EF Core database provider, and these tests supply the isolated
            // store below instead of requiring a live PostgreSQL instance.
            builder.UseSetting("ConnectionStrings:DefaultConnection", "");
            builder.UseSetting("ConnectionStrings:Redis", "");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<GameDbContext>>();
                services.RemoveAll<GameDbContext>();
                services.AddDbContext<GameDbContext>(options =>
                    options.UseInMemoryDatabase(_storeName));

                // The active-state store (REDIS_STATE.md §1–§4). Blanking
                // ConnectionStrings:Redis above means the production composition
                // registers no IBattleStateRepository at all, so this host supplies
                // the isolated in-memory substitute, exactly as it does for
                // GameDbContext.
                services.AddSingleton<IBattleStateRepository, ApiTestBattleStateRepository>();

                // Stand in for the TASK-035 exchange: it yields the verified
                // identity this test controls, or the documented rejection.
                // The exchange's own protocol behaviour belongs to that
                // contract, not to TASK-023.
                services.RemoveAll<IDiscordIdentityResolver>();
                services.AddSingleton<IDiscordIdentityResolver>(
                    new StubIdentityResolver(_discordUserId, RejectIdentity));
            });
        }
    }

    /// <summary>
    /// A test-only <see cref="IDiscordIdentityResolver"/>. It performs no
    /// exchange and asserts nothing about the protocol — it supplies the
    /// contract's output (or its documented failure) so the Player ownership
    /// layer can be exercised on its own.
    /// </summary>
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
