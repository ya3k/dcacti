using GameServer.Api.Authentication;
using GameServer.Application.Identity;
using GameServer.Application.Players;
using Microsoft.AspNetCore.Authorization;
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
///         ↓
/// application session JWT (player_id = PlayerId)
/// </code>
///
/// This controller owns the <b>identity → Player → session</b> half of §2. The
/// session itself is the self-contained signed JWT of §2.8, issued by
/// <see cref="ApplicationSessionTokenService"/> after the §2 identity exchange
/// has succeeded — §2.8's "issued by this endpoint only" (D1) is therefore true
/// by construction.
///
/// It remains a thin boundary (<c>ARCHITECTURE.md</c> §2.1 item 4): it
/// translates the wire request, delegates identity resolution, Player ownership,
/// and session issuance, and translates the result back to the wire response.
///
/// The Discord authorization-code exchange is deliberately <b>not</b> implemented
/// here: it is owned by the TASK-035 contract (<c>API_CONTRACTS.md</c> §2.2–§2.4,
/// <c>ADR-013</c>) and is an Infrastructure concern (<c>ADR-013</c> item 13),
/// consumed here through <see cref="IDiscordIdentityResolver"/> rather than
/// reimplemented.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IDiscordIdentityResolver _identityResolver;
    private readonly IPlayerRepository _playerRepository;
    private readonly PlayerStarterGrantFactory _starterGrants;
    private readonly ApplicationSessionTokenService _sessions;

    public AuthController(
        IDiscordIdentityResolver identityResolver,
        IPlayerRepository playerRepository,
        PlayerStarterGrantFactory starterGrants,
        ApplicationSessionTokenService sessions)
    {
        _identityResolver = identityResolver;
        _playerRepository = playerRepository;
        _starterGrants = starterGrants;
        _sessions = sessions;
    }

    /// <summary>
    /// Establishes the application session from a Discord authorization code
    /// (<c>API_CONTRACTS.md</c> §2).
    ///
    /// This is the <b>one</b> endpoint that does not require an authenticated
    /// session — it is the endpoint that establishes one
    /// (<c>API_CONTRACTS.md</c> §2.1, §2.8 "Coverage"; <c>ADR-015</c> D6). That is
    /// why it opts out of the default policy explicitly: no other endpoint shares
    /// the exemption.
    /// </summary>
    /// <param name="request">The Discord authorization code — the only request member.</param>
    /// <param name="cancellationToken">Cancels the exchange with the request.</param>
    [AllowAnonymous]
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
        //
        // The starter ownership composition (DATABASE.md §2 item 1) is supplied
        // as a callback so it is resolved by the creation boundary on the
        // creation branch only — an existing Player is returned unchanged and
        // receives none of it (DATABASE.md §2 item 3). The composition is fixed
        // server-side and no request member participates in it: the client's only
        // observation path is the §5 collection reads (GAME_RULES.md §18,
        // ADR-001).
        //
        // That is why no "has this Player received the starter set?" check exists
        // anywhere: the creation branch is the whole rule, and it is reached at
        // most once per Player.
        var player = await _playerRepository.GetOrCreateByDiscordUserIdAsync(
            identity.Identity.DiscordUserId,

            // DATABASE.md §1: AcquiredAt is a server clock reading, set once. It
            // is never supplied by the client.
            composeStarterGrant: cancellation => _starterGrants.CreateAsync(
                DateTimeOffset.UtcNow,
                cancellation),
            cancellationToken);

        // Step 3 — issue the application session (API_CONTRACTS.md §2.5, §2.8;
        // ADR-015 D1/D3/D5). The claim carries the PlayerId the exchange already
        // resolved and mapped, so the chain stays
        // `verified DiscordUserId → PlayerId → JWT player_id` and no client input
        // participates: the Discord access token is never the session (§2.7
        // item 4), and nothing is written anywhere to record the session (D2).
        //
        // §2.5: the response shape is unchanged — `sessionToken` and `playerId` —
        // and `playerId` is the matched-or-created Player's own PlayerId.
        return Ok(new DiscordAuthResponse(
            SessionToken: _sessions.Issue(player.PlayerId, DateTimeOffset.UtcNow),
            PlayerId: player.PlayerId));
    }
}
