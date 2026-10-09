using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace GameServer.Api.Authentication;

/// <summary>
/// Issues the application session — the self-contained signed JWT of
/// <c>API_CONTRACTS.md</c> §2.3 / <c>ADR-015</c>.
///
/// <code>
/// verified Account (username/password)
///         ↓
/// Player match/create                (owned elsewhere — TASK-023)
///         ↓
/// PlayerId
///         ↓
/// this service                       (the session's only issuer)
///         ↓
/// signed JWT (player_id, 24h absolute, kid, aud = dcacti-backend)
/// </code>
///
/// It is called from exactly one place — <c>AuthController</c>, after
/// <c>POST /api/auth/register</c> (<c>API_CONTRACTS.md</c> §2.1) or
/// <c>POST /api/auth/login</c> (§2.2) has verified the account credentials
/// (<c>ADR-020</c> D3/D4) — which is what makes D1's single-issuer property true
/// by construction.
///
/// <b>Stateless.</b> Nothing is written when a session is issued: no session
/// row, no Redis key, no in-memory record (D2). The token is self-validating,
/// and the only thing the caller receives is the token string.
/// </summary>
public sealed class ApplicationSessionTokenService
{
    private readonly ApplicationSessionSigningKeys _keys;

    public ApplicationSessionTokenService(ApplicationSessionSigningKeys keys)
    {
        _keys = keys;
    }

    /// <summary>
    /// Issues one application session for the given <c>PlayerId</c>.
    /// </summary>
    /// <param name="playerId">
    /// The matched-or-created Player's own <c>PlayerId</c> (<c>DATABASE.md</c> §1)
    /// — the value §2.1/§2.2 return as <c>playerId</c> and the value the
    /// <c>player_id</c> claim carries (D3). It is server-derived: the client never
    /// supplies or overrides it.
    /// </param>
    /// <param name="issuedAt">
    /// The issuing instant, which the 24-hour absolute expiry is measured from
    /// (D5).
    /// </param>
    /// <returns>The signed session token, as the §2.1/§2.2 <c>sessionToken</c> string.</returns>
    public string Issue(string playerId, DateTimeOffset issuedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);

        var token = new JwtSecurityToken(
            // D9: aud = "dcacti-backend", the one value both consumers share.
            audience: ApplicationSessionClaims.Audience,

            // D8: the token carries no iss claim and issuer validation is
            // disabled, so no issuer is set here either. Supplying one would be a
            // claim nobody can meaningfully check.
            issuer: null,

            claims:
            [
                // D3: the identity claim. `player_id` = PlayerId — the
                // account-derived identity, and never a client-supplied value.
                new Claim(ApplicationSessionClaims.PlayerId, playerId),
            ],

            // D5: 24 hours absolute. There is no renewal, so a single absolute
            // expiry is the whole lifecycle; `notBefore` is the issuing instant
            // rather than a defaulted value.
            notBefore: issuedAt.UtcDateTime,
            expires: issuedAt.UtcDateTime.Add(ApplicationSessionOptions.Lifetime),

            // D7: HS256, signed with the current symmetric key. D11: the signing
            // credentials name the key in the `kid` header, so validation can
            // resolve current vs previous without a key-distribution endpoint.
            signingCredentials: _keys.SigningCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
