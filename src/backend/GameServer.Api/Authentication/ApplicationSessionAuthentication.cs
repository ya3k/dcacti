using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace GameServer.Api.Authentication;

/// <summary>
/// Registers the application session's authentication and authorization
/// (<c>ADR-015</c> D6).
///
/// <code>
/// JWT Bearer
///         ↓
/// ASP.NET Core Authentication
///         ↓
/// Authorization
/// </code>
///
/// Exactly <b>one</b> application authentication mechanism is registered. There
/// is no cookie scheme, no custom session middleware, no opaque session token,
/// and no second scheme for the <c>BattleHub</c>: REST and the hub's access token
/// are the same token under this one scheme (D4/D6).
/// </summary>
public static class ApplicationSessionAuthentication
{
    /// <summary>
    /// Registers the JWT Bearer scheme and the default authorization policy.
    /// </summary>
    /// <param name="services">The composition root's service collection.</param>
    /// <param name="configuration">The application configuration (D10).</param>
    /// <exception cref="ApplicationSessionSigningKeys.ConfigurationException">
    /// The signing secret is absent or unusable. Configuration is read here,
    /// during composition, so a host that cannot validate sessions refuses to
    /// start instead of failing every request at runtime
    /// (<c>ADR-015</c> "Implementation constraints").
    /// </exception>
    public static IServiceCollection AddApplicationSessionAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // D10/D11: the current key, plus the previous one while an overlap is in
        // effect. Read from configuration only — the secrets are supplied by the
        // host's configuration providers, never by a tracked file.
        var keys = ApplicationSessionSigningKeys.Create(
            configuration[ApplicationSessionOptions.CurrentSecretPath],
            configuration[ApplicationSessionOptions.CurrentKeyIdPath],
            configuration[ApplicationSessionOptions.PreviousSecretPath],
            configuration[ApplicationSessionOptions.PreviousKeyIdPath]);

        services.AddSingleton(keys);
        services.AddSingleton<ApplicationSessionTokenService>();

        // The requirement's handler. Registered as a singleton because it holds no
        // state and the policy it serves is the process-wide default.
        services.AddSingleton<IAuthorizationHandler, PlayerIdClaimRequirementHandler>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => ConfigureBearer(options, keys, configuration));

        // §1 / D6: every endpoint requires an authenticated session unless it
        // opts out, so the default policy is this session policy. A new controller
        // is therefore protected on arrival rather than by remembering an
        // attribute, and the one exception — POST /api/auth/discord, the endpoint
        // that establishes the session — opts out explicitly.
        //
        // The policy requires the identity claim itself, not merely a validated
        // signature. A token that is correctly signed, correctly addressed, and
        // unexpired but carries no player_id identifies nobody, and an endpoint
        // acting for "nobody" would have to invent a Player
        // (API_CONTRACTS.md §2.8 "Identity"; GAME_RULES.md §18, ADR-001).
        var policy = CreateSessionPolicy();

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(policy)
            .SetDefaultPolicy(policy);

        // The single documented unauthenticated response covers a failed
        // requirement as well as a missing session (§2.8 "Failure behavior").
        services.AddApplicationSessionForbiddenResponse();

        return services;
    }

    /// <summary>
    /// The <c>BattleHub</c> negotiate path (<c>SIGNALR_PROTOCOL.md</c> §1 item 2).
    /// </summary>
    /// <remarks>
    /// It is named here because the host has to treat this one request
    /// differently from every other: SignalR's client learns its connection id
    /// from negotiate and attaches <c>Authorization</c> only to the requests that
    /// follow, so the fallback session policy cannot apply to it without making
    /// the hub unreachable. See <c>Program.cs</c> for where the exemption is
    /// applied and why it is scoped to this path alone.
    /// </remarks>
    public const string HubNegotiatePath = "/hubs/battle/negotiate";

    /// <summary>
    /// The policy every endpoint gets unless it opts out: a valid session
    /// (<c>ADR-015</c> D6) that carries the <c>player_id</c> claim (D3).
    /// </summary>
    /// <remarks>
    /// The claim requirement is expressed as a custom requirement rather than
    /// <c>RequireClaim</c> so that a token without an identity produces the
    /// documented response. A failed <c>RequireClaim</c> is a <i>forbidden</i>
    /// outcome, because the framework treats an authenticated principal that lacks
    /// a claim as authenticated-but-unauthorized — which would answer
    /// <c>403</c>. <c>API_CONTRACTS.md</c> §2.8 fixes one response for every
    /// session failure — <c>401 { "error": "UNAUTHENTICATED" }</c> — so "no
    /// session" and "no identity" must both produce it.
    /// </remarks>
    private static AuthorizationPolicy CreateSessionPolicy() =>
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PlayerIdClaimRequirement())
            .Build();

    /// <summary>
    /// Configures what the bearer handler validates, from the ADR-015 values.
    /// </summary>
    /// <remarks>
    /// Every value below is one <c>ADR-015</c> fixes. The parameters are set
    /// explicitly, including the ones whose library default happens to agree, so
    /// that no default stands in for a recorded decision.
    /// </remarks>
    public static void ConfigureBearer(
        JwtBearerOptions options,
        ApplicationSessionSigningKeys keys,
        IConfiguration configuration)
    {
        // The framework keeps the raw JWT claim types on the principal rather than
        // rewriting well-known ones to WS-Federation URIs, and the player_id claim
        // must survive validation under the exact name the contract gives it.
        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            // D7 — algorithm exactly HS256. ValidAlgorithms is the effective
            // restriction: `alg: none` and every asymmetric `alg` header are
            // rejected, regardless of what the token asserts about itself.
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],

            // D11 — the key set is the current key plus, during an overlap, the
            // previous one. Supplying the keys explicitly is also what keeps the
            // handler's configuration-manager key discovery (a JWKS-style
            // mechanism D11 excludes) out of the picture entirely.
            IssuerSigningKeys = keys.ValidationKeys,

            // The signed token handler resolves the kid header against
            // IssuerSigningKeys itself, so the separate resolver (which exists for
            // unsigned-token cases) is deliberately left unset.
            IssuerSigningKeyResolver = null,

            // D8 — no iss claim, and issuer validation disabled. There is no
            // canonical issuer value to check against: the project owns no domain
            // name, and one process both issues and validates (ADR-002).
            ValidateIssuer = false,
            ValidIssuer = null,
            ValidIssuers = null,

            // D9 — aud = "dcacti-backend", exact match required.
            ValidateAudience = true,
            ValidAudience = ApplicationSessionClaims.Audience,

            // D5 — 24 hours absolute. Expiry is validated; `nbf` is validated
            // against a zero skew, so the documented lifetime is not silently
            // extended by clock tolerance.
            ValidateLifetime = true,
            ClockSkew = ApplicationSessionOptions.ClockSkew,

            // A token that has not reached its notBefore yet is not yet valid.
            ValidateActor = false,

            // D3 — the claim this contract reads. The authentication type is named
            // so the identity is identifiably this session's.
            NameClaimType = ApplicationSessionClaims.PlayerId,
            RoleClaimType = "role",
            AuthenticationType = ApplicationSessionOptions.AuthenticationType,
        };

        // §2.8 "Failure behavior" / §4 note 6: a missing, invalid/tampered, or
        // expired session all produce the same `401 { "error":
        // "UNAUTHENTICATED" }`. The default challenge writes no body, so the
        // documented envelope is written here instead. No validation detail is
        // included and the failure is not distinguished.
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await UnauthenticatedResponse.WriteAsync(context.HttpContext);
            },
        };
    }

    /// <summary>
    /// Registers the mapping from an authorization <i>forbid</i> to the documented
    /// unauthenticated response.
    /// </summary>
    /// <remarks>
    /// The framework answers <c>403</c> when an <i>authenticated</i> principal
    /// fails an authorization requirement, and only challenges with <c>401</c>
    /// when the principal is anonymous. A session that is validly signed but
    /// carries no <c>player_id</c> is authenticated, so its failure to identify a
    /// Player would otherwise surface as <c>403</c> — a response
    /// <c>API_CONTRACTS.md</c> §2.8 does not define, and one that would tell a
    /// caller their token was structurally accepted.
    ///
    /// Every failure to establish an application session produces the one
    /// documented outcome, so the forbid result is replaced with it.
    /// </remarks>
    public static IServiceCollection AddApplicationSessionForbiddenResponse(
        this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationMiddlewareResultHandler,
            UnauthenticatedForbidResultHandler>();

        return services;
    }
}
