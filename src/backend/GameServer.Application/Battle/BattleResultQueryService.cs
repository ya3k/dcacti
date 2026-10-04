using GameServer.Domain.Battle;

namespace GameServer.Application.Battle;

/// <summary>
/// The completed-battle result reads behind <c>GET /api/battle/{battleId}/result</c>
/// (<c>API_CONTRACTS.md</c> §4) and <c>GET /api/battle/history</c> (§4.5).
///
/// <code>
/// GET /api/battle/{battleId}/result   → GetOwnedResultAsync
/// GET /api/battle/history             → ListHistoryAsync
///
/// authenticated PlayerId            (API_CONTRACTS.md §2.8 — the session claim)
///         ↓
/// BattleResult row(s)               (DATABASE.md §1 — by key, or by Player)
///         ↓
/// owner-only read                   (API_CONTRACTS.md §4 note 7, §4.5 note 8)
/// </code>
///
/// <b>Ownership is established from the authenticated identity, never from the
/// request.</b> <c>API_CONTRACTS.md</c> §4 note 7: "The caller may read a
/// <c>BattleResult</c> only when the identity resolved from their authenticated
/// session equals <c>BattleResult.PlayerId</c> … <b>Ownership is never
/// established from client-supplied input:</b> no <c>playerId</c> request
/// member, query parameter, header, or body field may select, override, or stand
/// in for the caller's identity." §4.5 note 8 applies the same rule to the
/// history read. The caller identity therefore arrives as an argument from the
/// authentication boundary, and neither member here reads a request value at
/// all.
///
/// <b>A row the caller does not own is indistinguishable from one that does not
/// exist.</b> §4 note 7 requires the same documented outcome for both, so that
/// the endpoint never discloses the existence of another Player's battle — which
/// is why the single-result read reports a single <c>null</c> for "missing" and
/// "not yours" rather than a distinguishable status. History needs no such
/// collapse: §4.5 note 8 fixes its scope to the caller outright.
///
/// <b>Neither read computes anything.</b> The outcome, the duration, and the
/// reward summary are the stored rows' own values (<c>DATABASE.md</c> §1), and
/// the history's <c>completedAt</c> is the row's own persisted server instant.
/// Nothing is recomputed from a request, from active battle state, or from the
/// current time — which is why neither read consults a clock.
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

    /// <summary>
    /// Returns the authenticated Player's completed-battle history, most recent
    /// first (<c>API_CONTRACTS.md</c> §4.5).
    ///
    /// <code>
    /// authenticated PlayerId            (§4.5 note 8 — the session claim)
    ///         ↓
    /// player-scoped ordered read         (§4.5 notes 4, 5)
    ///         ↓
    /// [ durable BattleResult rows ]      (DATABASE.md §1 — terminal rows only)
    /// </code>
    ///
    /// <b>The scope is the authenticated identity and nothing else.</b> §4.5
    /// note 8 requires the identity resolved from the authenticated session to
    /// equal <c>BattleResult.PlayerId</c>, and forbids any <c>playerId</c> request
    /// member, query parameter, header, or body field from selecting, overriding,
    /// or standing in for it. This member therefore takes the identity as an
    /// argument from the authentication boundary and reads no request value at
    /// all — there is deliberately no overload that could supply one by another
    /// route, which is what makes reading another Player's history inexpressible
    /// rather than merely unreachable.
    ///
    /// <b>Ownership is the query, not a post-filter.</b> Unlike the single-result
    /// read above — which must resolve a row before it can tell "missing" from
    /// "not yours", because §4 note 7 requires those to be one answer — history
    /// has no such distinction to preserve: §4.5 note 8 fixes the scope to the
    /// caller, so the Player filter belongs in the read itself. The own-row
    /// comparison is repeated on the way out as an invariant check, so a boundary
    /// that returned another Player's row could not reach the response.
    ///
    /// <b>The order is the repository's and is part of the contract.</b> §4.5
    /// note 4 orders by <c>CompletedAt</c> descending with a
    /// <c>BattleResultId</c> descending tie-break, and states clients may rely on
    /// it. That order is expressed by the query
    /// (<see cref="IBattleResultRepository.ListByPlayerIdAsync"/>), so this
    /// service neither re-sorts nor second-guesses it.
    ///
    /// <b>It computes nothing and bounds nothing.</b> Outcome, rewards, and
    /// duration are the stored row's own values (<c>DATABASE.md</c> §1) and
    /// <c>completedAt</c> is the row's own persisted server instant — never a
    /// reading taken now, which is why this read needs no clock. §4.5 note 5 makes
    /// the array the complete history, so no page, limit, offset, cursor, filter,
    /// sort, or search is applied: an unbounded list is the accepted MVP
    /// contract.
    ///
    /// <b>An empty history is an empty list.</b> §4.5 note 9: a Player with no
    /// completed battles receives <c>200</c> with <c>[]</c> — not <c>404</c>, not
    /// <c>204</c>, and not an error.
    /// </summary>
    /// <param name="callerPlayerId">
    /// The authenticated caller's Player identity (<c>API_CONTRACTS.md</c> §2.8,
    /// §4.5 note 8) — resolved server-side from the session's <c>player_id</c>
    /// claim, never from request input.
    /// </param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The caller's own durable results, most recent first, or an empty list when
    /// the caller has none.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// The caller identity is null, empty, or whitespace. It is required for the
    /// same reason the owner-only read requires it: an absent identity could only
    /// be satisfied by trusting the request, which §4.5 note 8 forbids.
    /// </exception>
    public async Task<IReadOnlyList<BattleResult>> ListHistoryAsync(
        string callerPlayerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callerPlayerId);

        var results = await _results
            .ListByPlayerIdAsync(callerPlayerId, cancellationToken)
            .ConfigureAwait(false);

        // API_CONTRACTS.md §4.5 note 8: the caller may read only their own rows.
        // The repository already scopes the query to this identity, so this is an
        // invariant check on the boundary rather than the ownership decision —
        // and it is written so that a boundary returning another Player's row is
        // refused (reported as absence) instead of disclosed.
        foreach (var result in results)
        {
            if (!string.Equals(result.PlayerId, callerPlayerId, StringComparison.Ordinal))
            {
                return [];
            }
        }

        return results;
    }
}
