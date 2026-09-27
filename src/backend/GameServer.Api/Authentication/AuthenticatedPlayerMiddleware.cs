namespace GameServer.Api.Authentication;

/// <summary>
/// Publishes the authenticated session's <c>PlayerId</c> into the request context
/// (<c>ADR-015</c> D3; <c>API_CONTRACTS.md</c> §2.8).
///
/// <code>
/// ASP.NET Core authentication      (JWT Bearer — the one scheme, D6)
///         ↓
/// validated principal
///         ↓
/// this middleware
///         ↓
/// HttpContext.Items["GameServer.PlayerId"]
///         ↓
/// the endpoints that act for the Player
/// </code>
///
/// It runs after <c>UseAuthentication</c> so it only ever republishes an
/// already-validated identity, and it writes nothing when there is none. It
/// decides nothing: no token parsing, no validation, no authorization, and no
/// fallback to client input — <see cref="AuthenticatedPlayer.GetPlayerId"/> is a
/// read of the validated principal.
///
/// It exists because <c>HttpContext.Items</c> is the request-context
/// representation the endpoint boundary already reads, and populating it in one
/// place keeps the authorization decision (the framework's) and the identity
/// (the claim's) from being re-derived independently at each call site.
/// </summary>
public sealed class AuthenticatedPlayerMiddleware
{
    private readonly RequestDelegate _next;

    public AuthenticatedPlayerMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var playerId = AuthenticatedPlayer.GetPlayerId(context);

        if (playerId is not null)
        {
            context.Items[AuthenticatedPlayer.RequestContextKey] = playerId;
        }

        return _next(context);
    }
}
