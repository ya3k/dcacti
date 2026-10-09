using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace GameServer.Api.Tests;

/// <summary>
/// The test fixture for the application session contract
/// (<c>API_CONTRACTS.md</c> §2.3; <c>ADR-015</c> D1–D11).
///
/// It supplies the two things every authenticated host needs — a configuration
/// provider carrying a test signing key (<c>D10</c>: the secret comes from
/// configuration, never from a tracked file), and helpers that mint and present
/// tokens in the documented shape.
///
/// <b>The keys here are test fixtures, not production material.</b> They exist so
/// the contract can be exercised, and they are deliberately not usable as a real
/// signing secret: the whole point of <c>D10</c> is that the real one is
/// supplied by the environment. They are constants because a test needs a stable
/// key across the host it configures, not because they are acceptable anywhere
/// else.
///
/// The helper deliberately builds tokens with the same primitives the production
/// issuer uses (<c>JwtSecurityToken</c> + <c>SigningCredentials</c>) rather than
/// through <c>ApplicationSessionTokenService</c>, so a test can produce a token
/// the production issuer would refuse — a wrong audience, an expired lifetime, a
/// foreign key — and prove validation rejects it.
/// </summary>
internal static class TestApplicationSession
{
    /// <summary>The current test key's <c>kid</c> (<c>D11</c>).</summary>
    internal const string CurrentKeyId = "test-current-key";

    /// <summary>The current test signing secret (<c>D7</c>: HS256, so ≥ 32 bytes).</summary>
    internal const string CurrentSecret = "test-only-current-signing-secret-0123456789";

    /// <summary>The previous test key's <c>kid</c>, used to exercise the D11 overlap.</summary>
    internal const string PreviousKeyId = "test-previous-key";

    /// <summary>The previous test signing secret, used to exercise the D11 overlap.</summary>
    internal const string PreviousSecret = "test-only-previous-signing-secret-0123456789";

    /// <summary>
    /// A key that is neither the current nor the previous one — the "unknown key"
    /// case validation must reject (<c>D11</c>: validation accepts exactly two
    /// keys).
    /// </summary>
    internal const string UnknownSecret = "test-only-unknown-signing-secret-0123456789";

    /// <summary>A third key, for a tampered-signature token.</summary>
    internal const string ForeignSecret = "test-only-foreign-signing-secret-0123456789";

    /// <summary>
    /// The configuration values that put the current key in place (<c>D10</c>).
    /// A host applies these with <c>UseSetting</c> or an in-memory collection; no
    /// tracked file carries them.
    /// </summary>
    internal static readonly (string Key, string Value)[] CurrentKeyConfiguration =
    [
        ("ApplicationSession:CurrentKey:Secret", CurrentSecret),
        ("ApplicationSession:CurrentKey:KeyId", CurrentKeyId),
    ];

    /// <summary>
    /// The configuration values that put the current key <b>and</b> a previous key
    /// in place — an operator-run rotation overlap (<c>D11</c>).
    /// </summary>
    internal static readonly (string Key, string Value)[] RotatedKeyConfiguration =
    [
        ("ApplicationSession:CurrentKey:Secret", CurrentSecret),
        ("ApplicationSession:CurrentKey:KeyId", CurrentKeyId),
        ("ApplicationSession:PreviousKey:Secret", PreviousSecret),
        ("ApplicationSession:PreviousKey:KeyId", PreviousKeyId),
    ];

    /// <summary>
    /// Builds a key from a secret and <c>kid</c>, as the pair issuance and
    /// validation use it.
    /// </summary>
    internal static SymmetricSecurityKey Key(string secret, string keyId) =>
        new(Encoding.UTF8.GetBytes(secret)) { KeyId = keyId };

    /// <summary>
    /// Signs a token with the given key and <c>kid</c>.
    /// </summary>
    private static SigningCredentials Credentials(string secret, string keyId) =>
        new(Key(secret, keyId), SecurityAlgorithms.HmacSha256, SecurityAlgorithms.HmacSha256Signature);

    /// <summary>
    /// Mints a session token carrying the documented claims, with every value
    /// overridable so a test can produce exactly the token it needs to test.
    /// </summary>
    /// <param name="playerId">
    /// The <c>player_id</c> claim's value (<c>D3</c>), or <c>null</c> to omit the
    /// claim entirely — the "valid signature, no identity" case.
    /// </param>
    /// <param name="audience">The <c>aud</c> claim (<c>D9</c>).</param>
    /// <param name="issuer">
    /// An <c>iss</c> claim to write, if a test wants to prove <c>D8</c> (issuer
    /// validation disabled) rather than merely assume it.
    /// </param>
    /// <param name="notBefore">The <c>nbf</c> claim.</param>
    /// <param name="expires">The <c>exp</c> claim (<c>D5</c>: 24 hours absolute).</param>
    /// <param name="secret">The secret to sign with.</param>
    /// <param name="keyId">The <c>kid</c> to name the key by (<c>D11</c>).</param>
    /// <param name="extraClaims">Any additional claims.</param>
    internal static string Mint(
        string? playerId = "player_under_test",
        string audience = "dcacti-backend",
        string? issuer = null,
        DateTime? notBefore = null,
        DateTime? expires = null,
        string secret = CurrentSecret,
        string keyId = CurrentKeyId,
        IEnumerable<Claim>? extraClaims = null)
    {
        var issuedAt = notBefore ?? DateTime.UtcNow;

        var claims = new List<Claim>();

        if (playerId is not null)
        {
            claims.Add(new Claim("player_id", playerId));
        }

        if (extraClaims is not null)
        {
            claims.AddRange(extraClaims);
        }

        var token = new JwtSecurityToken(
            audience: audience,
            issuer: issuer,
            claims: claims,
            notBefore: issuedAt,
            // D5: 24 hours absolute, so a test that wants a valid token gets the
            // documented lifetime rather than an arbitrary one.
            expires: expires ?? issuedAt.AddHours(24),
            signingCredentials: Credentials(secret, keyId));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Mints a token whose signing key is not in the configured validation set
    /// (<c>D11</c>).
    /// </summary>
    internal static string MintWithUnknownKey(string playerId = "player_under_test") =>
        Mint(playerId: playerId, secret: UnknownSecret, keyId: "unknown-key");

    /// <summary>Signs with the previous key, for the <c>D11</c> overlap cases.</summary>
    internal static string MintWithPreviousKey(string playerId = "player_under_test") =>
        Mint(playerId: playerId, secret: PreviousSecret, keyId: PreviousKeyId);

    /// <summary>
    /// Reads a token's header, so a test can assert the documented <c>kid</c>
    /// (<c>D11</c>) rather than trusting that it was written.
    /// </summary>
    internal static JwtHeader ReadHeader(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token).Header;

    /// <summary>Reads a token's payload, so a test can assert its claims.</summary>
    internal static JwtPayload ReadPayload(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token).Payload;
}

/// <summary>
/// A minimal shape check for the issued session artifact
/// (<c>API_CONTRACTS.md</c> §2.3).
/// </summary>
internal static class JwtTokenShape
{
    /// <summary>
    /// True when the value has the three-part <c>header.payload.signature</c>
    /// structure a signed JWT has — which the former <c>session_{guid}</c>
    /// placeholder did not.
    /// </summary>
    internal static bool IsThreePartJwt(string? value) =>
        value is not null
        && value.Split('.').Length == 3
        && value.Split('.').All(part => part.Length > 0);
}
