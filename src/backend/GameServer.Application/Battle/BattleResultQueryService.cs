using GameServer.Domain.Battle;

namespace GameServer.Application.Battle;

/// <summary>
/// The completed-battle result read behind <c>GET /api/battle/{battleId}/result</c>
/// (<c>API_CONTRACTS.md</c> §4).
///
/// <code>
/// authenticated PlayerId            (API_CONTRACTS.md §2.8 — the session claim)
///         ↓
/// BattleResult row                  (DATABASE.md §1 — by the battle's own id)
///         ↓
/// owner-only read                   (API_CONTRACTS.md §4 note 7)
/// </code>
///
/// <b>Ownership is established from the authenticated identity, never from the
/// request.</b> <c>API_CONTRACTS.md</c> §4 note 7: "The caller may read a
/// <c>BattleResult</c> only when the identity resolved from their authenticated
/// session equals <c>BattleResult.PlayerId</c> … <b>Ownership is never
/// established from client-supplied input:</b> no <c>playerId</c> request
/// member, query parameter, header, or body field may select, override, or stand
/// in for the caller's identity." The caller identity therefore arrives as an
/// argument from the authentication boundary, and this service reads no request
/// value at all.
///
/// <b>A row the caller does not own is indistinguishable from one that does not
/// exist.</b> §4 note 7 requires the same documented outcome for both, so that
/// the endpoint never discloses the existence of another Player's battle — which
/// is why this read reports a single <c>null</c> for "missing" and "not yours"
/// rather than a distinguishable status.
///
/// <b>It computes nothing.</b> The outcome, the duration, and the reward summary
/// are the stored row's own values (<c>DATABASE.md</c> §1); nothing is
/// recomputed from the request, from the active battle state, or from the
/// current time.
/// </summary>
public sealed class BattleResultQueryService
{
    private readonly IBattleResultRepository _results;

    public BattleResultQueryService(IBattleResultRepository results)
    {
        _results = results ?? throw new ArgumentNullException(nameof(results));
    }

    /// <summary>
    /// Returns the stored result for <paramref name="battleId"/> when the
    /// authenticated <paramref name="callerPlayerId"/> owns it, or <c>null</c>
    /// otherwise.
    ///
    /// <b>Both "no such result" and "not the caller's" return <c>null</c>.</b>
    /// They are deliberately one answer: <c>API_CONTRACTS.md</c> §4 note 7 makes
    /// a foreign battle return "the same <c>404 BATTLE_NOT_FOUND</c> as a battle
    /// that does not exist, so the endpoint never discloses the existence of
    /// another Player's battle". The comparison is ordinal — both sides are
    /// opaque server-authored identity strings, not culture-sensitive text.
    /// </summary>
    /// <param name="battleId">
    /// The battle to read (<c>GAME_STATE.md</c> §2.0.1). It is also the result
    /// row's key, because <c>BattleResultId</c> is the battle's own
    /// <c>BattleId</c> (<c>DATABASE.md</c> §1 sourcing item 1).
    /// </param>
    /// <param name="callerPlayerId">
    /// The authenticated caller's Player identity (<c>API_CONTRACTS.md</c> §2.8,
    /// §4 note 7) — resolved server-side from the session's <c>player_id</c>
    /// claim, never from request input.
    /// </param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The owned result, or <c>null</c> when none is readable.</returns>
    /// <exception cref="ArgumentException">
    /// The battle id or the caller identity is null, empty, or whitespace. Both
    /// are required: an absent battle id names no battle, and an absent identity
    /// could only be satisfied by trusting the request, which note 7 forbids.
    /// </exception>
    public async Task<BattleResult?> GetOwnedResultAsync(
        string battleId,
        string callerPlayerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(callerPlayerId);

        var result = await _results
            .GetByIdAsync(battleId, cancellationToken)
            .ConfigureAwait(false);

        // API_CONTRACTS.md §4 note 7: the authenticated owner is the only caller
        // who may read a result. A row belonging to another Player is reported as
        // absence — the same answer a missing row gets — so the endpoint
        // discloses nothing about battles the caller does not own.
        return result is not null
            && string.Equals(result.PlayerId, callerPlayerId, StringComparison.Ordinal)
                ? result
                : null;
    }
}
