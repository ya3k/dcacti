using System.Security.Claims;

namespace GameServer.Api.Authentication;

/// <summary>
/// Resolves the authenticated request's <c>PlayerId</c> from the validated
/// application session (<c>ADR-015</c> D3; <c>API_CONTRACTS.md</c> §2.8
/// "Identity").
///
/// <code>
/// validated JWT
///         ↓
/// player_id claim
///         ↓
/// PlayerId            ← the server-internal request identity
/// </code>
///
/// This is the one place the claim is turned back into a <c>PlayerId</c>, so
/// every consumer reads the same server-derived value. It is <b>not</b> a second
/// authentication mechanism: it reads only what <c>UseAuthentication</c> has
/// already validated, and it has no fallback — no header, query parameter, body
/// field, or default Player can supply an identity. A request whose validated
/// principal carries no <c>player_id</c> has no <c>PlayerId</c>
/// (<c>GAME_RULES.md</c> §18, ADR-001, <c>API_CONTRACTS.md</c> §4 note 7).
/// </summary>
public static class AuthenticatedPlayer
{
    /// <summary>
    /// The request-context key under which the authenticated <c>PlayerId</c> is
    /// carried.
    ///
    /// <c>API_CONTRACTS.md</c> §2.8: "<c>GameServer.PlayerId</c> may be used as
    /// the server-internal request-context representation of that identity; it is
    /// never a client input." The key is unchanged — it is the boundary contract
    /// <c>BattleController</c> already reads — and this type is its writer.
    /// </summary>
    public const string RequestContextKey = "GameServer.PlayerId";

    /// <summary>
    /// The authenticated <c>PlayerId</c> of the request's validated session, or
    /// <c>null</c> when the request carries no valid application session.
    /// </summary>
    /// <param name="context">The request being served.</param>
    /// <remarks>
    /// A non-null result always means "the JWT signature, algorithm, audience,
    /// and expiry validated, and this <c>PlayerId</c> came from its
    /// <c>player_id</c> claim."
    /// </remarks>
    public static string? GetPlayerId(HttpContext context) =>
        GetPlayerIdFromPrincipal(context.User);

    /// <summary>
    /// The authenticated <c>PlayerId</c> of a validated principal, or <c>null</c>
    /// when it carries none.
    /// </summary>
    /// <param name="principal">
    /// A principal produced by the application's authentication scheme. A
    /// principal this contract did not validate will not be authenticated and
    /// therefore resolves to <c>null</c>.
    /// </param>
    /// <remarks>
    /// It is expressed on the principal rather than on <see cref="HttpContext"/>
    /// because the <c>BattleHub</c> connection exposes the validated principal as
    /// <c>HubCallerContext.User</c> rather than through an <c>HttpContext</c>. Both
    /// callers read the same one claim.
    /// </remarks>
    public static string? GetPlayerIdFromPrincipal(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var playerId = principal.FindFirst(ApplicationSessionClaims.PlayerId)?.Value;

        // A token whose player_id is absent or empty identifies nobody. It is
        // rejected here rather than coerced into a default identity.
        return string.IsNullOrWhiteSpace(playerId) ? null : playerId;
    }
}
