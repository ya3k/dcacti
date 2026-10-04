using GameServer.Api.Authentication;
using GameServer.Application.Battle;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Api.Controllers;

/// <summary>
/// The battle boundary (<c>API_CONTRACTS.md</c> §3, §4).
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
///
/// GET /api/battle/{battleId}/result
///         ↓
/// BattleResultQueryService        (Application — owner-scoped read)
///         ↓
/// { battleId, outcome, rewards, durationTurns }
///
/// GET /api/battle/history
///         ↓
/// BattleResultQueryService        (Application — player-scoped ordered read)
///         ↓
/// [ { battleId, outcome, rewards, durationTurns, completedAt } ]
/// </code>
///
/// <b>It is a thin boundary</b> (<c>ARCHITECTURE.md</c> §2.1 item 4): it
/// translates the wire request, delegates the whole orchestration to the service
/// it calls, and translates the outcome back to the wire response. It contains no
/// Card copy-limit logic, no Relic duplicate logic, no Pet combat initialization,
/// no Boss logic, and no ownership rule of its own — each is owned by the service
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
/// selects, overrides, or stands in for it — on either endpoint.
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
    private readonly BattleResultQueryService _battleResults;
    private readonly BattleStateService _battles;

    public BattleController(
        BattleStartService battleStart,
        BattleResultQueryService battleResults,
        BattleStateService battles)
    {
        _battleStart = battleStart;
        _battleResults = battleResults;
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
    /// Returns the completed battle's result for its owning Player
    /// (<c>API_CONTRACTS.md</c> §4).
    ///
    /// <b>Success (200).</b> Exactly the §4 response shape — <c>battleId</c>,
    /// <c>outcome</c>, <c>rewards</c>, and <c>durationTurns</c> — for a battle
    /// that has already ended and that the authenticated caller owns. While a
    /// battle is active its state is available only through the SignalR
    /// connection, not here, and no row exists for it yet, so this endpoint
    /// answers the same <c>404</c> an unknown battle gets.
    ///
    /// <b>Not found (404).</b> The §6 envelope with <c>BATTLE_NOT_FOUND</c>, for
    /// a result that does not exist <b>or</b> that belongs to another Player.
    /// §4 note 7 makes those one answer deliberately, so the endpoint never
    /// discloses whether another Player's battle exists.
    ///
    /// <b>Unauthenticated (401).</b> The §6 envelope with <c>UNAUTHENTICATED</c>,
    /// produced by the class-level <c>[Authorize]</c> and the session pipeline
    /// before this method runs — one outcome for a missing, invalid, tampered, or
    /// expired session, with no validation detail disclosed
    /// (<c>API_CONTRACTS.md</c> §2.8, §4 note 6; <c>ADR-015</c> D5). The guard
    /// below covers the remaining case the pipeline allows through: a principal
    /// that carries no <c>player_id</c>, which identifies nobody and must
    /// therefore not be answered with a result.
    ///
    /// <b>The caller's identity comes from the session, never the request.</b>
    /// §4 note 7 forbids any request member, query parameter, header, or body
    /// field from selecting, overriding, or standing in for the caller's
    /// identity, so this action binds no identity input at all: the only value it
    /// accepts is the battle id in the route, and the identity is read from the
    /// request context the authentication boundary published
    /// (<see cref="AuthenticatedPlayer"/>, <c>ADR-015</c> D3).
    /// </summary>
    /// <param name="battleId">
    /// The battle to read (<c>GAME_STATE.md</c> §2.0.1) — the result row's own
    /// key, since <c>BattleResultId</c> is the battle's <c>BattleId</c>
    /// (<c>DATABASE.md</c> §1).
    /// </param>
    /// <param name="cancellationToken">Cancels the read with the request.</param>
    [HttpGet("{battleId}/result")]
    public async Task<IActionResult> Result(string battleId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(battleId))
        {
            return NotFound(new { error = BattleNotFoundErrorCode });
        }

        var playerId = ResolveRequestingPlayerId();

        if (string.IsNullOrWhiteSpace(playerId))
        {
            // API_CONTRACTS.md §1 / §2.8 / §4 note 6: an identity that identifies
            // nobody is not an authenticated session, and §4 note 7 permits no
            // request-supplied substitute for it. The response is the documented
            // unauthenticated one — not BATTLE_NOT_FOUND, which note 6 reserves
            // for an authenticated caller and which would make an authorization
            // failure indistinguishable from a missing row.
            return Unauthorized(new
            {
                error = UnauthenticatedResponse.ErrorCode,
                message = "An authenticated session is required to read a battle result.",
            });
        }

        var result = await _battleResults.GetOwnedResultAsync(battleId, playerId, cancellationToken);

        if (result is null)
        {
            // §4: the documented failure for a result that does not exist or is
            // not the caller's. The two are one answer by design (note 7).
            return NotFound(new { error = BattleNotFoundErrorCode });
        }

        return Ok(BattleResultResponse.From(result));
    }

    /// <summary>
    /// Returns the authenticated Player's completed-battle history, most recent
    /// first (<c>API_CONTRACTS.md</c> §4.5).
    ///
    /// <b>Success (200).</b> A <b>bare JSON array</b> — that is, the array itself
    /// is the response body. §4.5 note 1: "No <c>{ "battles": [...] }</c>
    /// envelope, no <c>total</c>, no <c>nextCursor</c>, and no other metadata
    /// member exists." Each element is the §4 result shape plus <c>completedAt</c>
    /// (note 2, note 3), and carries no Boss or Pet identifier (note 12). The
    /// array is the <b>complete</b> history: note 5 makes the unbounded array the
    /// accepted MVP contract.
    ///
    /// <b>Empty history (200).</b> §4.5 note 9: a Player with no completed battles
    /// receives <c>200</c> and <c>[]</c> — not <c>404</c>, not <c>204</c>, and not
    /// an error. This endpoint has no not-found outcome at all: a history is a
    /// collection, and an empty one is a valid answer.
    ///
    /// <b>No request input.</b> §4.5 note 6: "The endpoint has no query parameters
    /// at all" — no <c>bossId</c>, <c>outcome</c>, or date filter, and no sort or
    /// search parameter is accepted or invented. There is no route parameter
    /// either: §4.5 note 8 makes another Player's history inexpressible in this
    /// contract, so no <c>{playerId}</c> segment exists.
    ///
    /// <b>Unauthenticated (401).</b> The §6 envelope with <c>UNAUTHENTICATED</c>,
    /// produced by the class-level <c>[Authorize]</c> and the session pipeline
    /// before this method runs — one outcome for a missing, invalid, tampered, or
    /// expired session (<c>API_CONTRACTS.md</c> §2.8, §4.5 note 7; <c>ADR-015</c>
    /// D5), and never a <c>404</c>. The guard below covers the remaining case the
    /// pipeline allows through: a principal that carries no <c>player_id</c>,
    /// which identifies nobody and must therefore not be answered with a history.
    ///
    /// <b>The caller's identity comes from the session, never the request.</b>
    /// §4.5 note 8 forbids any <c>playerId</c> request member, query parameter,
    /// header, or body field from selecting, overriding, or standing in for the
    /// caller's identity, so this action binds no input at all and reads the
    /// identity from the request context the authentication boundary published
    /// (<see cref="AuthenticatedPlayer"/>, <c>ADR-015</c> D3).
    ///
    /// <b>It is a read of durable results only.</b> Only persisted terminal
    /// <c>BattleResult</c> rows are returned, so an active battle never appears
    /// (note 10) and a battle whose durable write failed is simply absent (note
    /// 11). Nothing here resolves a Swap, replays a battle, calculates a reward,
    /// or reconstructs an outcome: the response is the stored rows' own values.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read with the request.</param>
    [HttpGet("history")]
    public async Task<IActionResult> History(CancellationToken cancellationToken)
    {
        var playerId = ResolveRequestingPlayerId();

        if (string.IsNullOrWhiteSpace(playerId))
        {
            // API_CONTRACTS.md §1 / §2.8 / §4.5 note 7: an identity that
            // identifies nobody is not an authenticated session, and §4.5 note 8
            // permits no request-supplied substitute for it. The response is the
            // documented unauthenticated one — there is no BATTLE_NOT_FOUND-style
            // alternative here, since this endpoint has no not-found outcome.
            return Unauthorized(new
            {
                error = UnauthenticatedResponse.ErrorCode,
                message = "An authenticated session is required to read battle history.",
            });
        }

        var history = await _battleResults.ListHistoryAsync(playerId, cancellationToken);

        // API_CONTRACTS.md §4.5 note 1: the array IS the response body — no
        // envelope wraps it, and Ok() over the sequence serializes exactly that.
        // An empty history serializes as [] (note 9).
        return Ok(history.Select(BattleHistoryItemResponse.From));
    }

    /// <summary>
    /// The documented <c>§4</c> error code for a result the caller cannot read —
    /// a battle that does not exist, or one that is not theirs
    /// (<c>API_CONTRACTS.md</c> §4, §6).
    /// </summary>
    private const string BattleNotFoundErrorCode = "BATTLE_NOT_FOUND";

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
