using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace GameServer.Api.Authentication;

/// <summary>
/// The symmetric signing keys the application session JWT is signed and
/// validated with — the current key and, during a rotation overlap, the previous
/// one (<c>ADR-015</c> D7/D11).
///
/// <code>
/// current key   → new tokens are signed with it
/// previous key  → its still-unexpired tokens stay valid (overlap)
/// </code>
///
/// The key material itself is configuration (<see cref="ApplicationSessionOptions"/>,
/// <c>ADR-015</c> D10). This type only pairs each secret with the <c>kid</c> the
/// token header names it by, and builds the two validation keys the handler
/// accepts.
///
/// <b>Nothing here is a key store.</b> There are exactly two keys, chosen by an
/// operator through configuration, and there is no rotation schedule, no
/// background worker, and no key-distribution endpoint: rotation is a manual
/// configuration change (<c>D11</c>).
/// </summary>
public sealed class ApplicationSessionSigningKeys
{
    private ApplicationSessionSigningKeys(
        SigningCredentials signingCredentials,
        SecurityKey currentValidationKey,
        string currentKeyId,
        SecurityKey? previousValidationKey,
        string? previousKeyId)
    {
        SigningCredentials = signingCredentials;
        CurrentValidationKey = currentValidationKey;
        CurrentKeyId = currentKeyId;
        PreviousValidationKey = previousValidationKey;
        PreviousKeyId = previousKeyId;
    }

    /// <summary>
    /// The current key, as the credentials new tokens are signed with. It is
    /// always the current key that signs — never the previous one
    /// (<c>D11</c>: new tokens → current signing key).
    /// </summary>
    public SigningCredentials SigningCredentials { get; }

    /// <summary>The current key, as a validation key.</summary>
    public SecurityKey CurrentValidationKey { get; }

    /// <summary>The <c>kid</c> the current key is named by.</summary>
    public string CurrentKeyId { get; }

    /// <summary>
    /// The previous key, or <c>null</c> when no rotation overlap is configured.
    /// It validates only; it never signs.
    /// </summary>
    public SecurityKey? PreviousValidationKey { get; }

    /// <summary>The <c>kid</c> the previous key is named by, or <c>null</c>.</summary>
    public string? PreviousKeyId { get; }

    /// <summary>
    /// The keys validation accepts — the current key now, plus the previous key
    /// while an overlap is in effect (<c>D11</c>: "validation accepts exactly two
    /// keys: the current signing key and the previous one").
    ///
    /// This is the whole validation set. A key that is neither of the two is
    /// unresolvable, so a token signed with it fails; and because the set is
    /// supplied explicitly, the handler's own issuers/keys discovery (which would
    /// be a JWKS-style mechanism <c>D11</c> forbids) is never consulted.
    /// </summary>
    public IReadOnlyList<SecurityKey> ValidationKeys =>
        PreviousValidationKey is null
            ? [CurrentValidationKey]
            : [CurrentValidationKey, PreviousValidationKey];

    /// <summary>
    /// Builds the current + previous key pair from configuration.
    /// </summary>
    /// <param name="currentSecret">
    /// The current HS256 signing secret, read from configuration (<c>D10</c>).
    /// </param>
    /// <param name="currentKeyId">
    /// The <c>kid</c> the current key is named by (<c>D11</c>). It is a
    /// configuration value too, because it has to change when the secret does.
    /// </param>
    /// <param name="previousSecret">
    /// The previous key's secret during a rotation overlap, or empty for none.
    /// </param>
    /// <param name="previousKeyId">The previous key's <c>kid</c>, or empty for none.</param>
    /// <exception cref="ConfigurationException">
    /// A required value is missing or too short (<c>AGENTS.md</c> §7 — the
    /// implementation does not invent or default a signing key).
    /// </exception>
    public static ApplicationSessionSigningKeys Create(
        string? currentSecret,
        string? currentKeyId,
        string? previousSecret,
        string? previousKeyId)
    {
        var currentKey = CreateHmacKey(currentSecret, currentKeyId, ApplicationSessionOptions.CurrentSecretPath);
        var currentBytes = currentKey.Key;

        // D11: every token carries a kid header. The signing key states which key
        // it is, and SigningCredentials publishes it as the token's `kid` — so the
        // header is set from the key rather than left to any default.
        var currentSecurityKey = new SymmetricSecurityKey(currentBytes)
        {
            KeyId = currentKey.KeyId,
        };

        var currentCredentials = new SigningCredentials(
            currentSecurityKey,
            SecurityAlgorithms.HmacSha256,
            SecurityAlgorithms.HmacSha256Signature);

        var currentValidationKey = CreateValidationKey(currentBytes, currentKey.KeyId);

        // The previous key is optional: no previous secret means no overlap is in
        // effect, and validation accepts the current key alone.
        if (string.IsNullOrWhiteSpace(previousSecret) && string.IsNullOrWhiteSpace(previousKeyId))
        {
            return new ApplicationSessionSigningKeys(
                currentCredentials,
                currentValidationKey,
                currentKey.KeyId,
                previousValidationKey: null,
                previousKeyId: null);
        }

        var previousKey = CreateHmacKey(previousSecret, previousKeyId, ApplicationSessionOptions.PreviousSecretPath);

        // D11's overlap is deliberate, so a half-configured previous key is a
        // configuration error rather than a silently-ignored value: an operator
        // who supplies one half believes old tokens are still accepted.
        if (string.Equals(previousKey.KeyId, currentKey.KeyId, StringComparison.Ordinal))
        {
            throw new ConfigurationException(
                $"'{ApplicationSessionOptions.PreviousKeyIdPath}' must differ from "
                + $"'{ApplicationSessionOptions.CurrentKeyIdPath}': the kid names the key a token was "
                + "signed with, so two different keys cannot share one (ADR-015 D11).");
        }

        var previousValidationKey = CreateValidationKey(previousKey.Key, previousKey.KeyId);

        return new ApplicationSessionSigningKeys(
            currentCredentials,
            currentValidationKey,
            currentKey.KeyId,
            previousValidationKey,
            previousKey.KeyId);
    }

    /// <summary>
    /// Builds one validation key from its raw bytes and <c>kid</c>.
    /// </summary>
    /// <remarks>
    /// The key carries the key identifier in the same header the token names it
    /// with, which is what lets the token handler resolve a presented token's
    /// <c>kid</c> against the validation set (<c>D11</c>). A key whose
    /// <c>kid</c> is not in that set resolves to nothing, so a token signed with
    /// an unknown key fails rather than being accepted under a default.
    /// </remarks>
    private static SecurityKey CreateValidationKey(byte[] keyBytes, string keyId) =>
        new SymmetricSecurityKey(keyBytes)
        {
            KeyId = keyId,
        };

    /// <summary>
    /// Resolves one configured key pair (secret + <c>kid</c>) into its raw bytes.
    /// </summary>
    private static (byte[] Key, string KeyId) CreateHmacKey(
        string? secret,
        string? keyId,
        string configurationPath)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            // The exact failure ADR-015's implementation constraints require: the
            // secret comes from configuration (D10), and if it is absent the
            // application stops rather than generating or defaulting one.
            throw new ConfigurationException(
                $"The application session signing secret is not configured. Set "
                + $"'{configurationPath}' (ADR-015 D10: the secret comes from "
                + "configuration — a host environment variable in production, an uncommitted .env or "
                + "dotnet user-secrets locally — never from a tracked file). No generated or default key "
                + "is substituted.");
        }

        if (string.IsNullOrWhiteSpace(keyId))
        {
            throw new ConfigurationException(
                $"'{configurationPath}KeyId' is not configured. Every application session token carries a "
                + "kid header (ADR-015 D11), so the key the secret belongs to has to be named.");
        }

        var key = System.Text.Encoding.UTF8.GetBytes(secret);

        // HS256's security rests on the secret's entropy, and RFC 7518 §3.2
        // requires a key at least as long as the HMAC output for this algorithm.
        // A shorter one is refused rather than silently used.
        if (key.Length < MinimumSecretBytes)
        {
            throw new ConfigurationException(
                $"The configured application session signing secret is too short for HS256 (ADR-015 D7): "
                + $"{key.Length} bytes, at least {MinimumSecretBytes} required (RFC 7518 §3.2). The value "
                + "is never echoed here.");
        }

        return (key, keyId);
    }

    /// <summary>
    /// HS256's minimum key length in bytes — the algorithm's output size
    /// (RFC 7518 §3.2; <c>ADR-015</c> D7 signs with HS256).
    /// </summary>
    private const int MinimumSecretBytes = 32;

    /// <summary>
    /// The signing-key configuration is missing or unusable
    /// (<c>ADR-015</c> D10/D11).
    /// </summary>
    public sealed class ConfigurationException(string message) : Exception(message);
}
