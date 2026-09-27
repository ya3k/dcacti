using Microsoft.AspNetCore.Authorization;

namespace GameServer.Api.Authentication;

/// <summary>
/// Requires the validated session to carry the <c>player_id</c> claim — the
/// identity contract of <c>API_CONTRACTS.md</c> §2.8 / <c>ADR-015</c> D3.
///
/// A signature-valid, correctly-addressed, unexpired token with no
/// <c>player_id</c> identifies nobody. Such a request must not reach an endpoint
/// that acts for a Player, because the endpoint would have to invent one
/// (<c>GAME_RULES.md</c> §18, ADR-001).
///
/// <b>Why a requirement rather than <c>RequireClaim</c>.</b> A failed
/// <c>RequireClaim</c> is a <i>forbidden</i> outcome — the framework treats an
/// authenticated principal missing a claim as authenticated-but-unauthorized and
/// answers <c>403</c>. <c>API_CONTRACTS.md</c> §2.8 fixes a single response for
/// every session failure, <c>401 { "error": "UNAUTHENTICATED" }</c>, so
/// "no session" and "no identity" must be indistinguishable. Marking the failure
/// as a challenge (rather than a forbid) is what produces the documented answer.
/// </summary>
public sealed class PlayerIdClaimRequirement : IAuthorizationRequirement;

/// <summary>
/// The handler for <see cref="PlayerIdClaimRequirement"/>.
/// </summary>
public sealed class PlayerIdClaimRequirementHandler
    : AuthorizationHandler<PlayerIdClaimRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PlayerIdClaimRequirement requirement)
    {
        var playerId = context.User.FindFirst(ApplicationSessionClaims.PlayerId)?.Value;

        if (string.IsNullOrWhiteSpace(playerId))
        {
            // Deliberately not `context.Fail()`: a failure would be reported as
            // `403`, and §2.8 documents one response — the same
            // `401 UNAUTHENTICATED` a missing session produces. Leaving the
            // requirement unmet makes authorization raise a challenge, which the
            // JWT bearer scheme answers with that documented response.
            return Task.CompletedTask;
        }

        context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
