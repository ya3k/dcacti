using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace GameServer.Api.Authentication;

/// <summary>
/// Replaces the framework's <c>403 Forbidden</c> with the documented
/// unauthenticated response (<c>API_CONTRACTS.md</c> §2.8 "Failure behavior",
/// §6; <c>ADR-015</c> D5).
///
/// <b>Why this exists.</b> The framework reports a failed authorization
/// requirement as a <i>forbid</i> when the principal is authenticated, and as a
/// <i>challenge</i> only when it is anonymous. A session that is validly signed,
/// correctly addressed, and unexpired but carries no <c>player_id</c> is
/// authenticated, so its failure to identify a Player would surface as
/// <c>403</c> — a status this contract does not define for a session failure, and
/// one that would disclose that the token itself was accepted.
///
/// This contract has exactly one failure outcome for every session condition —
/// missing, invalid, tampered, expired, or identifying nobody —
/// <c>401 { "error": "UNAUTHENTICATED" }</c>, with no validation detail. The
/// handler routes the forbid result to that response and leaves the challenge
/// path (the JWT bearer scheme's own <c>OnChallenge</c>) unchanged.
/// </summary>
public sealed class UnauthenticatedForbidResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            // A session failure is never "authenticated but not permitted" under
            // this contract: the only reason the session policy forbids is that
            // the caller's validated token identifies no Player. That is an
            // unauthenticated outcome (ADR-015 D3/D5).
            return UnauthenticatedResponse.WriteAsync(context);
        }

        return _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
