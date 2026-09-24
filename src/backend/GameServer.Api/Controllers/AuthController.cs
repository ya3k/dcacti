using GameServer.Application.Identity;
using GameServer.Application.Players;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Api.Controllers;

public record DiscordAuthRequest(string Code);
public record DiscordAuthResponse(string SessionToken, string PlayerId);

/// <summary>
/// The authentication boundary (<c>API_CONTRACTS.md</c> §2).
///
/// <code>
/// Discord identity
///         ↓
/// DiscordUserId
///         ↓
/// Player repository/service
///         ↓
/// find Player by DiscordUserId
///         ↓
/// existing Player → reuse
/// new DiscordUserId → create Player Level 1
/// </code>
///
/// This controller owns the <b>identity → Player</b> half of §2 only.
/// It remains a thin boundary (<c>ARCHITECTURE.md</c> §2.1 item 4): it
/// translates the wire request, delegates identity resolution and Player
/// ownership, and translates the result back to the wire response.
///
/// Two adjacent responsibilities are deliberately <b>not</b> implemented here,
/// because neither is decided by an authoritative document yet:
///
/// <list type="bullet">
/// <item>
/// <b>The Discord authorization-code exchange</b> — owned by the TASK-035
/// contract (<c>API_CONTRACTS.md</c> §2.2–§2.4, <c>ADR-013</c>). It is an
/// Infrastructure concern (<c>ADR-013</c> item 13), consumed here through
/// <see cref="IDiscordIdentityResolver"/> rather than reimplemented
/// (<c>AGENTS.md</c> §7, §18).
/// </item>
/// <item>
/// <b>The application session mechanism</b> — owned by TASK-034
/// (<c>ADR-007</c> item 4). §2.5 defines no format, claims, lifetime, or
/// validation strategy, so <c>sessionToken</c> continues to be the opaque,
/// unvalidated placeholder it already was. This type introduces no token
/// format and no session storage.
/// </item>
/// </list>
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IDiscordIdentityResolver _identityResolver;
    private readonly IPlayerRepository _playerRepository;

    public AuthController(
        IDiscordIdentityResolver identityResolver,
        IPlayerRepository playerRepository)
    {
        _identityResolver = identityResolver;
        _playerRepository = playerRepository;
    }

    [HttpPost("discord")]
    public async Task<IActionResult> AuthenticateDiscord(
        [FromBody] DiscordAuthRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new { error = "INVALID_CODE", message = "Discord authorization code is required." });
        }

        // Step 1 — resolve the verified Discord identity from the
        // authorization code (API_CONTRACTS.md §2.2–§2.4, ADR-013).
        //
        // The resulting DiscordUserId is the contract's sole output to Player
        // persistence, and it is the only value the Player layer keys on. The
        // authorization code is never that key: it is short-lived and
        // single-use (§2.4).
        var identity = await _identityResolver.ResolveAsync(request.Code, cancellationToken);

        if (!identity.Succeeded)
        {
            // §2.6 rule 5 / §2.7 item 6: no Player is created or matched on
            // any failure path, so the write below is reached only with a
            // verified identity. The failure carries the §6 error envelope and
            // no upstream Discord detail (§2.7 item 5).
            return StatusCode(identity.StatusCode, new { error = identity.Error, message = identity.Message });
        }

        // Step 2 — match or create the Player that owns this Discord identity
        // (DATABASE.md §1). This is TASK-023's responsibility: an existing
        // Player is reused unchanged, and a new one starts at the documented
        // initial Level (PET_RULES.md §5 item 8).
        var player = await _playerRepository.GetOrCreateByDiscordUserIdAsync(
            identity.Identity.DiscordUserId,
            cancellationToken);

        // §2.5: the response shape is unchanged, and `playerId` is the
        // matched-or-created Player's own PlayerId. `sessionToken` remains the
        // opaque placeholder until TASK-034 defines the session mechanism.
        return Ok(new DiscordAuthResponse(
            SessionToken: $"session_{Guid.NewGuid():N}",
            PlayerId: player.PlayerId));
    }
}