using GameServer.Api.Authentication;
using GameServer.Application.Battle;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Api.Controllers;

/// <summary>
/// The battle-start boundary (<c>API_CONTRACTS.md</c> §3).
///
/// <code>
/// POST /api/battle/start
///         ↓
/// BattleStartService              (Application — orchestration)
///         ↓
/// Player / Pet / Boss resolution
/// CardLoadoutService
/// RelicLoadoutService
/// BattleStateService
///         ↓
/// { battleId, signalrHub, initialState }
/// </code>
///
/// <b>It is a thin boundary</b> (<c>ARCHITECTURE.md</c> §2.1 item 4): it
/// translates the wire request, delegates the whole orchestration to
/// <see cref="BattleStartService"/>, and translates the outcome back to the wire
/// response. It contains no Card copy-limit logic, no Relic duplicate logic, no
/// Pet combat initialization, and no Boss logic — each is owned by the service
/// this controller calls.
///
/// <b>The client supplies selection only.</b> The four documented request
/// members are Pet, Boss, and the two loadouts; no Damage, HP, Power, Match, or
/// Combo value is accepted, and no <c>battleId</c> is submitted
/// (<c>API_CONTRACTS.md</c> §1, <c>GAME_RULES.md</c> §18, ADR-001). The server
/// authors the resulting <c>BattleState</c>.
///
/// <b>The requesting Player is the authenticated session's</b>
/// (<c>API_CONTRACTS.md</c> §1, §2.8; <c>ADR-015</c> D3): the
/// <c>player_id</c> claim, published as the request context's
/// <c>GameServer.PlayerId</c>. No request member, query parameter, or header
/// selects, overrides, or stands in for it.
/// </summary>
[ApiController]
[Authorize]
[Route("api/battle")]
public class BattleController : ControllerBase
{
    /// <summary>
    /// The hub path the client connects to for this battle
    /// (<c>SIGNALR_PROTOCOL.md</c> §1 item 2 — the <c>BattleHub</c> group scoped
    /// to <c>battleId</c>).
    ///
    /// It is the route <c>Program.cs</c> maps the hub on, stated here as the
    /// §3 response's <c>signalrHub</c> member. The value is a transport address,
    /// not battle state: it selects no Pet, Boss, or loadout, and the client uses
    /// it only to open the connection it then joins the battle group on
    /// (<c>§1</c> items 2–3).
    /// </summary>
    private const string BattleHubPath = "/hubs/battle";

    private readonly BattleStartService _battleStart;
    private readonly BattleStateService _battles;

    public BattleController(BattleStartService battleStart, BattleStateService battles)
    {
        _battleStart = battleStart;
        _battles = battles;
    }

    /// <summary>
    /// Creates the authoritative battle for the submitted selection
    /// (<c>API_CONTRACTS.md</c> §3).
    ///
    /// <b>Success (200).</b> Returns exactly the documented response shape —
    /// <c>battleId</c>, <c>signalrHub</c>, and <c>initialState</c> — where
    /// <c>initialState</c> is a summary of the created <c>BattleState</c>
    /// (<c>GAME_STATE.md</c> §2).
    ///
    /// <b>Failure (400).</b> Returns the §6 envelope with the documented code
    /// for the rejection: <c>INVALID_LOADOUT</c> for an invalid Card or Relic
    /// loadout (<c>§3</c>), <c>PET_NOT_OWNED</c> for a Pet that is not owned by
    /// the requesting Player (<c>§3</c>), or <c>BOSS_NOT_FOUND</c> for a
    /// <c>bossId</c> that is not a valid MVP Boss (<c>§3</c>). No battle exists
    /// after any of them.
    ///
    /// <b>The requesting Player</b> is the authenticated caller
    /// (<c>§1</c>: "All endpoints … require an authenticated session"; <c>§3</c>:
    /// "the requesting Player"). The identity is the application session's
    /// <c>player_id</c> claim, republished as the request context's
    /// <c>GameServer.PlayerId</c> by the authentication boundary
    /// (<c>API_CONTRACTS.md</c> §2.8 "Identity", <c>ADR-015</c> D3) — never a
    /// client-supplied value. The class-level <c>[Authorize]</c> is what makes
    /// an unauthenticated caller receive the documented
    /// <c>401 UNAUTHENTICATED</c> before this method runs; the check below is the
    /// identity contract's own guard, so a principal that carries no
    /// <c>player_id</c> is refused rather than attributed to a default Player.
    /// </summary>
    /// <param name="request">The four documented selection members.</param>
    /// <param name="cancellationToken">Cancels the orchestration with the request.</param>
    [HttpPost("start")]
    public async Task<IActionResult> Start(
        [FromBody] BattleStartRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new
            {
                error = "INVALID_LOADOUT",
                message = "A battle start request is required.",
            });
        }

        var playerId = ResolveRequestingPlayerId();

        if (string.IsNullOrWhiteSpace(playerId))
        {
            // API_CONTRACTS.md §1 / §3 require an authenticated session. A request
            // that reached here therefore presented a validated token whose
            // player_id claim is missing or empty — an identity that identifies
            // nobody. It is rejected with the §2.8 unauthenticated response rather
            // than attributed to a default Player: attributing one would let such
            // a caller act as another Player (GAME_RULES.md §18, ADR-001).
            return Unauthorized(new
            {
                error = UnauthenticatedResponse.ErrorCode,
                message = "An authenticated session is required to start a battle.",
            });
        }

        var result = await _battleStart.StartAsync(playerId, request, cancellationToken);

        if (!result.Succeeded)
        {
            // API_CONTRACTS.md §6: the machine-readable code plus human-readable
            // detail. The code is the documented one for the outcome — the
            // internal Card/Relic rejection reasons are never surfaced as codes.
            return BadRequest(new
            {
                error = ToErrorCode(result.Outcome),
                message = result.Detail,
            });
        }

        return Ok(new BattleStartResponse(
            BattleId: result.BattleId!,
            SignalrHub: BattleHubPath,
            InitialState: await BattleStartStateSummary.ForAsync(
                result.BattleId!,
                _battles,
                cancellationToken)));
    }

    /// <summary>
    /// The documented <c>§6</c> error code for a rejection.
    /// </summary>
    private static string ToErrorCode(BattleStartOutcome outcome) => outcome switch
    {
        BattleStartOutcome.PetNotOwned => "PET_NOT_OWNED",
        BattleStartOutcome.BossNotFound => "BOSS_NOT_FOUND",
        BattleStartOutcome.InvalidLoadout => "INVALID_LOADOUT",

        // Unreachable by construction: every rejection the service produces is
        // one of the documented outcomes above, and a success never reaches the
        // error path. The fallback is the endpoint's own invalid-request code
        // rather than a newly invented one.
        _ => "INVALID_LOADOUT",
    };

    /// <summary>
    /// The resolved requesting Player, or <c>null</c> when the caller presents no
    /// authenticated identity.
    ///
    /// It reads the request-context item the authentication boundary publishes
    /// from the validated session's <c>player_id</c> claim
    /// (<see cref="AuthenticatedPlayer"/>, <c>API_CONTRACTS.md</c> §2.8,
    /// <c>ADR-015</c> D3). The key is this endpoint's own contract and is
    /// unchanged; what changed is that it now has a production writer.
    /// </summary>
    private string? ResolveRequestingPlayerId() =>
        HttpContext.Items[AuthenticatedPlayerItemKey] as string;

    /// <summary>
    /// The request-context key under which the authenticated session's
    /// <c>Player.PlayerId</c> is carried to this endpoint
    /// (<c>API_CONTRACTS.md</c> §2.8: "<c>GameServer.PlayerId</c> may be used as
    /// the server-internal request-context representation of that identity; it is
    /// never a client input").
    ///
    /// It is public because it is a boundary contract, not a private detail: the
    /// authentication boundary writes it and this endpoint reads it. The single
    /// definition lives on <see cref="AuthenticatedPlayer"/>, so the writer and
    /// the reader cannot drift to two different keys.
    /// </summary>
    public const string AuthenticatedPlayerItemKey = AuthenticatedPlayer.RequestContextKey;
}
