using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GameServer.Application.Battle;
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
/// The Discord credential's environment-sensitive presence rule
/// (<c>ADR-019</c> D3/D4) and the hygiene of the files that carry the keys
/// (<c>ADR-019</c> D1/D3).
///
/// <code>
/// non-Development + ClientId or ClientSecret absent (null | "" | "   ")
///         ↓
/// the host refuses to start          (composition, before it serves traffic)
///
/// Development + either absent
///         ↓
/// the host starts
///         ↓
/// POST /api/auth/discord → unchanged 503 DISCORD_UNAVAILABLE
///         ↓
/// no Player created, no session issued        (API_CONTRACTS.md §2.6 rule 5)
/// </code>
///
/// <b>Presence is the whole contract.</b> Nothing here asserts anything about a
/// credential's format, length, or shape, because no document fixes one and the
/// implementation must not invent one (<c>AGENTS.md</c> §7). The dummies in
/// <see cref="TestDiscordCredentials"/> are supplied through configuration in the
/// tests that need a configured value; no test performs a Discord exchange, and
/// no test prints a credential.
/// </summary>
public class DiscordCredentialConfigurationTests
{
    // =======================================================================
    // D4 — the composition rule, per environment
    // =======================================================================

    /// <summary>
    /// Outside Development an absent client id is fatal, whatever shape the
    /// absence takes (<c>ADR-019</c> D3: null, empty, or whitespace-only).
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnAbsentClientId_ShouldRefuseToCompose_OutsideDevelopment(string? clientId)
    {
        var exception = Assert.Throws<DiscordCredentialOptions.ConfigurationException>(() =>
            DiscordCredentialOptions.ValidateForEnvironment(
                BuildConfiguration(clientId, TestDiscordCredentials.ClientSecret),
                isDevelopmentEnvironment: false));

        // The failure names the key an operator has to set, so it is actionable
        // without consulting source.
        Assert.Contains(DiscordCredentialOptions.ClientIdPath, exception.Message);
    }

    /// <summary>
    /// Outside Development an absent client secret is fatal — including the
    /// whitespace-only placeholder shape a misconfigured environment variable
    /// produces.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnAbsentClientSecret_ShouldRefuseToCompose_OutsideDevelopment(string? clientSecret)
    {
        var exception = Assert.Throws<DiscordCredentialOptions.ConfigurationException>(() =>
            DiscordCredentialOptions.ValidateForEnvironment(
                BuildConfiguration(TestDiscordCredentials.ClientId, clientSecret),
                isDevelopmentEnvironment: false));

        Assert.Contains(DiscordCredentialOptions.ClientSecretPath, exception.Message);
    }

    /// <summary>
    /// Both absent keys are named in one failure, so an operator fixes the
    /// deployment in one pass rather than one key per restart.
    /// </summary>
    [Fact]
    public void BothAbsentCredentials_ShouldBothBeNamed()
    {
        var exception = Assert.Throws<DiscordCredentialOptions.ConfigurationException>(() =>
            DiscordCredentialOptions.ValidateForEnvironment(
                BuildConfiguration(null, null),
                isDevelopmentEnvironment: false));

        Assert.Contains(DiscordCredentialOptions.ClientIdPath, exception.Message);
        Assert.Contains(DiscordCredentialOptions.ClientSecretPath, exception.Message);
    }

    /// <summary>
    /// The failure never echoes a credential. The client id is supplied and the
    /// secret is the absent one here; the reverse case — a configured secret with
    /// a missing client id — is the one where a careless message would leak the
    /// value, and it must not appear anywhere in the failure text
    /// (<c>ADR-019</c> security invariants).
    /// </summary>
    [Fact]
    public void TheFailure_ShouldNeverEchoAConfiguredCredentialValue()
    {
        var exception = Assert.Throws<DiscordCredentialOptions.ConfigurationException>(() =>
            DiscordCredentialOptions.ValidateForEnvironment(
                BuildConfiguration(clientId: null, TestDiscordCredentials.ClientSecret),
                isDevelopmentEnvironment: false));

        Assert.Contains(DiscordCredentialOptions.ClientIdPath, exception.Message);
        Assert.DoesNotContain(TestDiscordCredentials.ClientSecret, exception.Message);
    }

    /// <summary>
    /// Presence is enough: a configured pair composes, and no format, length, or
    /// shape is validated. The application never second-guesses an
    /// operator-supplied credential (<c>ADR-019</c> D4).
    /// </summary>
    [Fact]
    public void AConfiguredCredential_ShouldCompose_OutsideDevelopment()
    {
        var failure = Record.Exception(() =>
            DiscordCredentialOptions.ValidateForEnvironment(
                BuildConfiguration(TestDiscordCredentials.ClientId, TestDiscordCredentials.ClientSecret),
                isDevelopmentEnvironment: false));

        Assert.Null(failure);
    }

    /// <summary>
    /// Development stays permissive for every absence shape, so TASK-181's local
    /// browser path works with no credential at all (<c>ADR-019</c> D4).
    /// </summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    [InlineData(TestDiscordCredentials.ClientId, null)]
    public void Development_ShouldCompose_WithoutTheCredential(string? clientId, string? clientSecret)
    {
        var failure = Record.Exception(() =>
            DiscordCredentialOptions.ValidateForEnvironment(
                BuildConfiguration(clientId, clientSecret),
                isDevelopmentEnvironment: true));

        Assert.Null(failure);
    }

    /// <summary>
    /// The absence test itself: null, empty, and whitespace-only are all
    /// "absent" (<c>ADR-019</c> D3), and any other value is present.
    /// </summary>
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("\t", false)]
    [InlineData("value", true)]
    public void IsConfigured_ShouldTreatNullEmptyAndWhitespaceAsAbsent(string? value, bool expected)
    {
        var configuration = BuildConfiguration(value, clientSecret: null);

        Assert.Equal(expected, DiscordCredentialOptions.IsConfigured(configuration, DiscordCredentialOptions.ClientIdPath));
    }

    // =======================================================================
    // D4 — the host, not merely the composition rule
    // =======================================================================

    /// <summary>
    /// A non-Development host with no usable credential <b>fails to start</b>,
    /// for every shape of absence — a missing client id (blanked, since the
    /// tracked <c>appsettings.json</c> supplies the public id by design) as well
    /// as a missing, empty, or whitespace-only client secret.
    /// </summary>
    [Theory]
    [InlineData(TestDiscordCredentials.ClientId, null)]
    [InlineData(TestDiscordCredentials.ClientId, "")]
    [InlineData(TestDiscordCredentials.ClientId, "   ")]
    [InlineData("", TestDiscordCredentials.ClientSecret)]
    [InlineData("   ", TestDiscordCredentials.ClientSecret)]
    public void ANonDevelopmentHost_WithoutAUsableCredential_ShouldRefuseToStart(
        string? clientId,
        string? clientSecret)
    {
        using var factory = new DiscordCredentialFactory
        {
            EnvironmentName = "Production",
            ClientId = clientId,
            ClientSecret = clientSecret,
        };

        var failure = Assert.ThrowsAny<Exception>(() => { factory.CreateClient(); });

        Assert.NotNull(FindCredentialConfigurationFailure(failure));
    }

    /// <summary>
    /// "Not Development" is the boundary, not "Production": any other environment
    /// is held to the same requirement (<c>ADR-019</c> D4).
    /// </summary>
    [Fact]
    public void AnyNonDevelopmentEnvironment_WithoutTheCredential_ShouldRefuseToStart()
    {
        using var factory = new DiscordCredentialFactory { EnvironmentName = "Staging" };

        var failure = Assert.ThrowsAny<Exception>(() => { factory.CreateClient(); });

        var configurationFailure = FindCredentialConfigurationFailure(failure);

        Assert.NotNull(configurationFailure);
        Assert.Contains(DiscordCredentialOptions.ClientSecretPath, configurationFailure!.Message);
    }

    /// <summary>
    /// The same non-Development host starts once the credential is present, which
    /// is what makes the failure above a presence check rather than an
    /// unconditional production refusal.
    /// </summary>
    [Fact]
    public void ANonDevelopmentHost_WithTheCredentialPresent_ShouldStart()
    {
        using var factory = new DiscordCredentialFactory
        {
            EnvironmentName = "Production",
            ClientId = TestDiscordCredentials.ClientId,
            ClientSecret = TestDiscordCredentials.ClientSecret,
        };

        var failure = Record.Exception(() => { factory.CreateClient(); });

        Assert.Null(failure);
    }

    /// <summary>
    /// Development with no credential at all starts and answers the
    /// <b>unchanged</b> <c>API_CONTRACTS.md</c> §2.6 contract: <c>503
    /// DISCORD_UNAVAILABLE</c> in the §6 envelope, with no session issued and no
    /// Player created (<c>ADR-019</c> D4; §2.6 rule 5, §2.7 item 6).
    /// </summary>
    [Fact]
    public async Task Development_WithoutTheCredential_ShouldStart_AndKeepTheUnchangedUnavailableAnswer()
    {
        using var factory = new DiscordCredentialFactory { EnvironmentName = "Development" };
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/discord",
            new { code = "an-ordinary-discord-authorization-code" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // The unchanged error code and envelope (§2.6, §6) — not a new contract.
        Assert.Equal("DISCORD_UNAVAILABLE", body.GetProperty("error").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()));

        // No session is issued and no Player identity is returned: the failure
        // carries no identity at all, so the caller cannot become anybody.
        Assert.False(body.TryGetProperty("sessionToken", out _));
        Assert.False(body.TryGetProperty("playerId", out _));

        // …and no Player row was written, because the exchange did not verify an
        // identity (§2.6 rule 5).
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        Assert.Equal(0, await context.Players.CountAsync());
    }

    // =======================================================================
    // D3 / D1 — the files that carry the keys
    // =======================================================================

    /// <summary>
    /// The tracked <c>appsettings.json</c> keeps the empty placeholder and no
    /// usable value (<c>ADR-019</c> D3) — the key stays, so the shape is
    /// documented, and it can never authenticate.
    /// </summary>
    [Fact]
    public void TheTrackedAppSettings_ShouldKeepTheEmptyClientSecretPlaceholder()
    {
        var path = Path.Combine(
            RepositoryRoot(),
            "src", "backend", "GameServer.Api", "appsettings.json");

        Assert.True(File.Exists(path), $"The tracked appsettings.json was not found at '{path}'.");

        using var document = JsonDocument.Parse(File.ReadAllText(path));

        var discord = document.RootElement.GetProperty("Discord");

        // The placeholder is present (D3 keeps it) and carries no credential.
        Assert.True(discord.TryGetProperty("ClientSecret", out var clientSecret));
        Assert.Equal(JsonValueKind.String, clientSecret.ValueKind);
        Assert.True(string.IsNullOrWhiteSpace(clientSecret.GetString()));
    }

    /// <summary>
    /// No local <c>appsettings.Development.json</c> carries a credential either:
    /// the developer-local channel is the user-secrets store, which lives outside
    /// the working tree (<c>ADR-019</c> D1). A fresh clone has no such file, and
    /// that is not a failure — there is nothing to guard.
    /// </summary>
    [Fact]
    public void TheLocalDevelopmentSettings_ShouldCarryNoCredential()
    {
        var path = Path.Combine(
            RepositoryRoot(),
            "src", "backend", "GameServer.Api", "appsettings.Development.json");

        if (!File.Exists(path))
        {
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));

        if (!document.RootElement.TryGetProperty("Discord", out var discord)
            || !discord.TryGetProperty("ClientSecret", out var clientSecret))
        {
            return;
        }

        Assert.True(
            string.IsNullOrWhiteSpace(clientSecret.GetString()),
            "appsettings.Development.json must not carry a Discord ClientSecret: the local channel is "
            + "the user-secrets store (ADR-019 D1).");
    }

    // =======================================================================
    // Helpers
    // =======================================================================

    /// <summary>Builds a configuration carrying exactly the two keys under test.</summary>
    private static IConfiguration BuildConfiguration(string? clientId, string? clientSecret) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>(DiscordCredentialOptions.ClientIdPath, clientId),
                new KeyValuePair<string, string?>(DiscordCredentialOptions.ClientSecretPath, clientSecret),
            ])
            .Build();

    /// <summary>
    /// Finds the credential-composition failure in a startup exception, however
    /// the test host wrapped it, so the assertion is on the cause rather than on a
    /// wrapper's type.
    /// </summary>
    private static DiscordCredentialOptions.ConfigurationException? FindCredentialConfigurationFailure(
        Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DiscordCredentialOptions.ConfigurationException configurationFailure)
            {
                return configurationFailure;
            }
        }

        return null;
    }

    /// <summary>
    /// The repository root, located from the test assembly's own location (the
    /// directory holding <c>src/</c> and <c>tests/</c>).
    /// </summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null
               && !Directory.Exists(Path.Combine(directory.FullName, "src", "backend")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return directory!.FullName;
    }

    /// <summary>
    /// A host running the production composition and pipeline over an isolated
    /// in-memory store, with the environment and the Discord credential under the
    /// test's control. The credential values are supplied (or withheld) through
    /// configuration, exactly as an environment supplies them
    /// (<c>ADR-019</c> D1) — never through a tracked file, and never a real value.
    /// </summary>
    private sealed class DiscordCredentialFactory : WebApplicationFactory<Program>
    {
        private readonly string _storeName = $"discord-credential-{Guid.NewGuid():N}";

        /// <summary>The host environment the credential requirement is evaluated against.</summary>
        public string EnvironmentName { get; init; } = "Development";

        /// <summary>
        /// The client id to configure, or <c>null</c> to leave the tracked
        /// <c>appsettings.json</c> value in place. The tracked file supplies the
        /// public client id by design (D3), so an <i>absent</i> client id is
        /// produced by an explicit blank override, exactly as a misconfigured
        /// environment variable would.
        /// </summary>
        public string? ClientId { get; init; }

        /// <summary>The client secret to configure, or <c>null</c> to leave it absent.</summary>
        public string? ClientSecret { get; init; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(EnvironmentName);

            // Blank the connection strings so AddInfrastructureServices does not
            // register the Npgsql provider; this host supplies the isolated store
            // below instead.
            builder.UseSetting("ConnectionStrings:DefaultConnection", "");
            builder.UseSetting("ConnectionStrings:Redis", "");

            // The application session's signing key, supplied through configuration
            // exactly as ADR-015 D10 requires — without it the host could not
            // compose at all, which would mask the Discord assertion.
            foreach (var (key, value) in TestApplicationSession.CurrentKeyConfiguration)
            {
                builder.UseSetting(key, value);
            }

            if (ClientId is not null)
            {
                builder.UseSetting(DiscordCredentialOptions.ClientIdPath, ClientId);
            }

            if (ClientSecret is not null)
            {
                builder.UseSetting(DiscordCredentialOptions.ClientSecretPath, ClientSecret);
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
    }
}
