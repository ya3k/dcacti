using System.Text;

namespace GameServer.Api.Authentication;

/// <summary>
/// The application session's server-side security configuration — the values
/// <c>ADR-015</c> D5 and D7–D11 fix.
///
/// <code>
/// D5   lifetime        24 hours, absolute — nothing else
/// D7   algorithm       HS256 (HMAC-SHA-256)
/// D8   issuer          no iss claim; issuer validation disabled
/// D9   audience        aud = "dcacti-backend", exact match required
/// D10  key storage     current/previous secret + kid from configuration only
/// D11  rotation        manual overlap: kid; current + previous key accepted
/// </code>
///
/// Every value that <c>ADR-015</c> decides lives here as a constant or as a
/// configuration key name. None of them is left to a library default, and none
/// is decided by this file's reader (<c>ADR-015</c> "Implementation constraints":
/// "no default may be substituted silently at implementation").
/// </summary>
public static class ApplicationSessionOptions
{
    /// <summary>
    /// The configuration section the signing keys are read from (<c>D10</c>).
    ///
    /// The section holds <b>names and key identifiers only</b> — never a secret
    /// value. The secrets arrive through the configuration providers the host
    /// already composes (a host environment variable in production; an
    /// uncommitted <c>.env</c> or <c>dotnet user-secrets</c> in local
    /// development), so they are never written into <c>appsettings.json</c>,
    /// <c>appsettings.Development.json</c>, <c>.env.example</c>, source, tests,
    /// or any other tracked artifact.
    /// </summary>
    public const string SectionName = "ApplicationSession";

    /// <summary>The configuration key holding the current signing secret (<c>D10</c>).</summary>
    public const string CurrentSecretPath = $"{SectionName}:CurrentKey:Secret";

    /// <summary>The configuration key holding the current key's <c>kid</c> (<c>D11</c>).</summary>
    public const string CurrentKeyIdPath = $"{SectionName}:CurrentKey:KeyId";

    /// <summary>
    /// The configuration key holding the previous signing secret during a
    /// rotation overlap (<c>D11</c>). Empty means no overlap is in effect.
    /// </summary>
    public const string PreviousSecretPath = $"{SectionName}:PreviousKey:Secret";

    /// <summary>
    /// The configuration key holding the previous key's <c>kid</c> (<c>D11</c>).
    /// </summary>
    public const string PreviousKeyIdPath = $"{SectionName}:PreviousKey:KeyId";

    /// <summary>
    /// The session's absolute expiry: <b>24 hours</b> (<c>ADR-015</c> D5,
    /// <c>API_CONTRACTS.md</c> §2.8 "Lifecycle (MVP)").
    ///
    /// Absolute and nothing else: no idle timeout, no renewal, no refresh token,
    /// and no revocation. A token is valid until this instant and invalid after
    /// it, which is exactly what a stateless session (D2) can enforce.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    /// <summary>
    /// The exact validation clock skew. The library default is 5 minutes, which
    /// would silently extend the documented 24-hour absolute lifetime by up to
    /// ten minutes across two clocks. <c>ADR-015</c> D5 fixes the lifetime, so no
    /// library default is substituted for it: skew is zero.
    /// </summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.Zero;

    /// <summary>
    /// Builds the authentication type the <c>player_id</c> claim is loaded under
    /// (<c>ADR-015</c> D3).
    /// </summary>
    public const string AuthenticationType = "ApplicationSession";

    /// <summary>
    /// The name of the single JWT Bearer scheme (<c>ADR-015</c> D6: "Exactly
    /// <b>one</b> application authentication mechanism exists"). Naming it keeps
    /// the one scheme identifiable without introducing a second.
    /// </summary>
    public const string SchemeName = "ApplicationSession";

    /// <summary>
    /// Converts the configured secret to the bytes the HMAC key is built from.
    /// </summary>
    /// <remarks>
    /// UTF-8 is the encoding the configuration string is read as. The bytes are
    /// never logged or returned.
    /// </remarks>
    public static byte[] ToKeyBytes(string secret) => Encoding.UTF8.GetBytes(secret);
}
