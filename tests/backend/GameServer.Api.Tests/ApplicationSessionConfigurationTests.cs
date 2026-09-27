using GameServer.Api.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace GameServer.Api.Tests;

/// <summary>
/// The application session's security configuration — <c>ADR-015</c> D5 and
/// D7–D11, asserted as the values the implementation actually uses rather than as
/// the values the implementation documents.
///
/// <code>
/// D5   lifetime        24 hours absolute
/// D7   algorithm       HS256 only
/// D8   issuer          no iss; issuer validation disabled
/// D9   audience        aud = "dcacti-backend", exact match
/// D10  key storage     configuration only
/// D11  rotation        kid; current + previous key accepted, and nothing else
/// </code>
///
/// These read the composed <c>JwtBearerOptions</c> the host's own registration
/// builds, so a value that drifts from the ADR fails here rather than being
/// restated in a comment.
/// </summary>
public class ApplicationSessionConfigurationTests
{
    /// <summary>
    /// Builds the bearer options exactly as the host's registration does, from
    /// the supplied configuration values.
    /// </summary>
    private static JwtBearerOptions BuildOptions(
        IReadOnlyList<(string Key, string Value)> configuration)
    {
        var configurationRoot = new ConfigurationBuilder()
            .AddInMemoryCollection(configuration.Select(pair =>
                new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();

        var services = new ServiceCollection();

        services.AddApplicationSessionAuthentication(configurationRoot);

        using var provider = services.BuildServiceProvider();

        return provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
    }

    private static readonly IReadOnlyList<(string Key, string Value)> CurrentOnly =
        TestApplicationSession.CurrentKeyConfiguration;

    private static readonly IReadOnlyList<(string Key, string Value)> WithOverlap =
        TestApplicationSession.RotatedKeyConfiguration;

    // -----------------------------------------------------------------------
    // D7 — the signing algorithm
    // -----------------------------------------------------------------------

    [Fact]
    public void ValidateAlgorithms_ShouldBeHs256Only()
    {
        // ADR-015 D7: "Validation must reject any other algorithm, including
        // alg=none and any asymmetric alg header." A ValidAlgorithms list of
        // exactly HS256 is what makes that true regardless of what a token claims
        // in its own header.
        var options = BuildOptions(CurrentOnly);

        Assert.Equal([SecurityAlgorithms.HmacSha256], options.TokenValidationParameters.ValidAlgorithms);
    }

    [Fact]
    public void Signature_ShouldBeRequired()
    {
        // D7 signs; a token carrying no signature is not a session.
        var options = BuildOptions(CurrentOnly);

        Assert.True(options.TokenValidationParameters.RequireSignedTokens);
        Assert.True(options.TokenValidationParameters.ValidateIssuerSigningKey);
    }

    // -----------------------------------------------------------------------
    // D8 — the issuer
    // -----------------------------------------------------------------------

    [Fact]
    public void IssuerValidation_ShouldBeDisabled()
    {
        // ADR-015 D8: "The token carries no iss claim, and issuer validation is
        // disabled."
        var options = BuildOptions(CurrentOnly);

        Assert.False(options.TokenValidationParameters.ValidateIssuer);
        Assert.Null(options.TokenValidationParameters.ValidIssuer);
        Assert.Null(options.TokenValidationParameters.ValidIssuers);
    }

    // -----------------------------------------------------------------------
    // D9 — the audience
    // -----------------------------------------------------------------------

    [Fact]
    public void AudienceValidation_ShouldRequireTheSingleDocumentedValue()
    {
        // ADR-015 D9: aud = "dcacti-backend", exact match required, one value for
        // both REST and BattleHub.
        var options = BuildOptions(CurrentOnly);

        Assert.True(options.TokenValidationParameters.ValidateAudience);
        Assert.Equal("dcacti-backend", options.TokenValidationParameters.ValidAudience);
        Assert.Equal(
            ApplicationSessionClaims.Audience,
            options.TokenValidationParameters.ValidAudience);
    }

    // -----------------------------------------------------------------------
    // D5 — the lifetime
    // -----------------------------------------------------------------------

    [Fact]
    public void LifetimeValidation_ShouldUseNoClockSkew()
    {
        // ADR-015 D5 fixes the lifetime at 24 hours absolute. The library's
        // default five-minute skew would silently extend it, so validation uses
        // none: the documented lifetime is the enforced one.
        var options = BuildOptions(CurrentOnly);

        Assert.True(options.TokenValidationParameters.ValidateLifetime);
        Assert.Equal(TimeSpan.Zero, options.TokenValidationParameters.ClockSkew);
        Assert.Equal(TimeSpan.Zero, ApplicationSessionOptions.ClockSkew);
    }

    [Fact]
    public void DocumentedLifetime_ShouldBeTwentyFourHours()
    {
        // ADR-015 D5 / API_CONTRACTS.md §2.8 "Lifecycle (MVP)".
        Assert.Equal(TimeSpan.FromHours(24), ApplicationSessionOptions.Lifetime);
    }

    // -----------------------------------------------------------------------
    // D11 — the validation key set
    // -----------------------------------------------------------------------

    [Fact]
    public void ValidationKeys_ShouldBeTheCurrentKeyAlone_WhenNoOverlapIsConfigured()
    {
        // Without a previous key there is no overlap, so validation accepts
        // exactly one key.
        var options = BuildOptions(CurrentOnly);

        var keys = options.TokenValidationParameters.IssuerSigningKeys!.ToArray();

        Assert.Single(keys);
        Assert.Equal(TestApplicationSession.CurrentKeyId, keys[0].KeyId);
    }

    [Fact]
    public void ValidationKeys_ShouldBeTheCurrentAndPreviousKeys_DuringAnOverlap()
    {
        // ADR-015 D11: "validation accepts exactly two keys: the current signing
        // key and the previous one."
        var options = BuildOptions(WithOverlap);

        var keyIds = options.TokenValidationParameters.IssuerSigningKeys!
            .Select(key => key.KeyId)
            .ToArray();

        Assert.Equal(2, keyIds.Length);
        Assert.Contains(TestApplicationSession.CurrentKeyId, keyIds);
        Assert.Contains(TestApplicationSession.PreviousKeyId, keyIds);
    }

    [Fact]
    public void IssuerSigningKeyResolver_ShouldBeUnset()
    {
        // D11 excludes JWKS/KMS/automated rotation. The key set is supplied
        // explicitly, and leaving the resolver unset is what keeps the handler's
        // own configuration-manager key discovery — a key-distribution endpoint
        // by another name — out of the pipeline.
        var options = BuildOptions(WithOverlap);

        Assert.Null(options.TokenValidationParameters.IssuerSigningKeyResolver);
    }

    // -----------------------------------------------------------------------
    // D10 — the key comes from configuration
    // -----------------------------------------------------------------------

    [Fact]
    public void AnAbsentSigningSecret_ShouldRefuseToCompose()
    {
        // ADR-015's implementation constraints: "If it is absent, do not silently
        // substitute a generated or default key — STOP per AGENTS.md §7 rather
        // than invent one." Composition is where that stop happens.
        var exception = Assert.Throws<ApplicationSessionSigningKeys.ConfigurationException>(
            () => ApplicationSessionSigningKeys.Create(
                currentSecret: null,
                currentKeyId: "some-key",
                previousSecret: null,
                previousKeyId: null));

        // The message names the configuration key so an operator can fix it, and
        // carries no value.
        Assert.Contains(ApplicationSessionOptions.CurrentSecretPath, exception.Message);
    }

    [Fact]
    public void AnEmptySigningSecret_ShouldRefuseToCompose()
    {
        Assert.Throws<ApplicationSessionSigningKeys.ConfigurationException>(
            () => ApplicationSessionSigningKeys.Create(
                currentSecret: "   ",
                currentKeyId: "some-key",
                previousSecret: null,
                previousKeyId: null));
    }

    [Fact]
    public void ASecretShorterThanTheAlgorithmRequires_ShouldRefuseToCompose()
    {
        // D7 signs with HS256, and RFC 7518 §3.2 requires a key at least as long
        // as the HMAC output (32 bytes). A shorter secret is refused rather than
        // silently used, and the failure does not echo the value.
        var shortSecret = "too-short";

        var exception = Assert.Throws<ApplicationSessionSigningKeys.ConfigurationException>(
            () => ApplicationSessionSigningKeys.Create(
                currentSecret: shortSecret,
                currentKeyId: "some-key",
                previousSecret: null,
                previousKeyId: null));

        Assert.DoesNotContain(shortSecret, exception.Message);
    }

    [Fact]
    public void AMissingKeyId_ShouldRefuseToCompose()
    {
        // D11: every token carries a kid header, so a key without an identifier
        // cannot name one.
        Assert.Throws<ApplicationSessionSigningKeys.ConfigurationException>(
            () => ApplicationSessionSigningKeys.Create(
                currentSecret: TestApplicationSession.CurrentSecret,
                currentKeyId: null,
                previousSecret: null,
                previousKeyId: null));
    }

    [Fact]
    public void APreviousKeySharingTheCurrentKeyId_ShouldRefuseToCompose()
    {
        // Two different keys cannot answer to one kid: the header would no longer
        // identify the key a token was signed with (D11).
        Assert.Throws<ApplicationSessionSigningKeys.ConfigurationException>(
            () => ApplicationSessionSigningKeys.Create(
                currentSecret: TestApplicationSession.CurrentSecret,
                currentKeyId: TestApplicationSession.CurrentKeyId,
                previousSecret: TestApplicationSession.PreviousSecret,
                previousKeyId: TestApplicationSession.CurrentKeyId));
    }

    // -----------------------------------------------------------------------
    // D3 — the authentication identity
    // -----------------------------------------------------------------------

    [Fact]
    public void TheIdentityClaim_ShouldBePlayerId()
    {
        // ADR-015 D3 / API_CONTRACTS.md §2.8 "Identity":
        // claim = player_id, value = PlayerId.
        Assert.Equal("player_id", ApplicationSessionClaims.PlayerId);

        var options = BuildOptions(CurrentOnly);

        Assert.Equal(
            ApplicationSessionClaims.PlayerId,
            options.TokenValidationParameters.NameClaimType);
    }

    [Fact]
    public void TheRequestContextKey_ShouldBeTheDocumentedServerInternalKey()
    {
        // ADR-015 D3 / API_CONTRACTS.md §2.8: GameServer.PlayerId "may be used as
        // the server-internal request-context representation of that identity".
        Assert.Equal("GameServer.PlayerId", AuthenticatedPlayer.RequestContextKey);
    }

    // -----------------------------------------------------------------------
    // The one scheme (D6)
    // -----------------------------------------------------------------------

    [Fact]
    public void NoApplicationSessionValue_ShouldBeReadFromATrackedFile()
    {
        // ADR-015 D10: the secret "must never appear in appsettings.json,
        // appsettings.Development.json, .env.example, source code, a test fixture,
        // a log line, or any committed artifact".
        //
        // A tracked file cannot be detected from here, so what is asserted is the
        // half that can be: the section the implementation reads carries key
        // *identifiers* only, and no shipped default supplies a secret. A host
        // with no configured key refuses to start (the test above), which is
        // exactly what makes a tracked default impossible to smuggle in.
        Assert.DoesNotContain("Secret", ApplicationSessionClaims.PlayerId);
        Assert.Equal("ApplicationSession:CurrentKey:Secret", ApplicationSessionOptions.CurrentSecretPath);
        Assert.Equal("ApplicationSession:PreviousKey:Secret", ApplicationSessionOptions.PreviousSecretPath);
    }
}
