using Microsoft.Extensions.Configuration;

namespace GameServer.Infrastructure.Discord;

/// <summary>
/// The development-only authentication path's configuration (TASK-181).
///
/// <code>
/// ASPNETCORE_ENVIRONMENT = Development      (one condition)
/// DevelopmentAuthentication:Enabled = true  (the other)
///                    ↓
/// the development identity source is registered
/// </code>
///
/// <b>It is not an authentication contract.</b> The session this path produces is
/// the same one <c>POST /api/auth/discord</c> already produces: the same
/// <c>IDiscordIdentityResolver</c> seam, the same
/// <c>IPlayerRepository.GetOrCreateByDiscordUserIdAsync</c> Player path, and the
/// same <c>ApplicationSessionTokenService</c> issuer (<c>API_CONTRACTS.md</c>
/// §2.8, <c>ADR-015</c> D1/D3/D6). Only the <i>source of the identity</i> is
/// substituted, and only to a fixed server-owned value — no request member
/// participates, so no caller can select an identity (D3, §2.8 "Identity").
///
/// <b>Two conditions, and no default.</b> <see cref="EnabledPath"/> is absent
/// from every tracked file, so a host that does not explicitly opt in fails
/// closed; and the environment condition is evaluated by the composition root
/// from the host's own environment, so no <c>appsettings*.json</c> can enable the
/// path by itself (TASK-181 AC-01/AC-02).
///
/// <b>No secret lives here.</b> The values below are a configuration key name, a
/// fixed non-secret identity, and a request trigger. The application session's
/// signing secret remains configuration-only (<c>ADR-015</c> D10) and is never
/// part of this section.
/// </summary>
public static class DevelopmentAuthenticationOptions
{
    /// <summary>The configuration section this path is configured from.</summary>
    public const string SectionName = "DevelopmentAuthentication";

    /// <summary>
    /// The opt-in switch (<c>DevelopmentAuthentication:Enabled</c>). It is read as
    /// a boolean and is <b>off unless a host explicitly sets it</b>: an absent,
    /// empty, or unparsable value is <c>false</c>, never a default-on.
    /// </summary>
    public const string EnabledPath = $"{SectionName}:Enabled";

    /// <summary>
    /// The authorization code that triggers the development identity — the
    /// development client's stand-in for a Discord authorization code
    /// (<c>API_CONTRACTS.md</c> §2.2).
    ///
    /// It is a fixed, non-secret sentinel, and it is the <b>only</b> code the
    /// development source answers: any other code reaches the unchanged
    /// "exchange unavailable" answer, so the TASK-035 boundary stays exactly as it
    /// is and a real Discord code is never silently resolved to a development
    /// identity. A real Discord authorization code is a long random value, so it
    /// can never collide with this one.
    /// </summary>
    public const string AuthorizationCode = "development";

    /// <summary>
    /// The development identity's <c>DiscordUserId</c> (<c>API_CONTRACTS.md</c>
    /// §2.4) — the fixed, server-owned key the development Player is created and
    /// matched on (<c>DATABASE.md</c> §1).
    ///
    /// It is a <b>constant</b>, deliberately: nothing in the request and nothing
    /// in configuration can change which Player a development authentication
    /// resolves to, which is what makes the identity deterministic across
    /// restarts and repeated runs (TASK-181 AC-05) and what makes "a caller
    /// chooses an arbitrary existing Player" unrepresentable. It is not a Discord
    /// snowflake and is never a real account's identifier.
    /// </summary>
    public const string DiscordUserId = "development-player";

    /// <summary>
    /// Whether the development-only path is enabled for this host
    /// (<see cref="EnabledPath"/>), <c>false</c> on every other input.
    /// </summary>
    public static bool IsEnabled(IConfiguration configuration) =>
        bool.TryParse(configuration[EnabledPath], out var enabled) && enabled;
}
