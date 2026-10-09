using GameServer.Domain.Battle;

namespace GameServer.Application.Battle;

/// <summary>
/// The durable battle result persistence boundary (<c>DATABASE.md</c> §1,
/// <c>ARCHITECTURE.md</c> §3 <c>PersistenceRepository (Postgres)</c>).
///
/// <code>
/// BattleStateService                 Application — sequencing only
///         ↓  IBattleResultRepository      this contract
/// BattleResultRepository (Postgres)   Infrastructure
///         ↓
/// BattleResult (DATABASE.md §1)
/// </code>
///
/// <b>Three operations, because the contract has three.</b> The battle-end path
/// writes one row, the result endpoint reads one row by its key
/// (<c>API_CONTRACTS.md</c> §4, <c>DATABASE.md</c> §1 sourcing item 1), and the
/// history endpoint enumerates one Player's rows in the contract's order
/// (<c>API_CONTRACTS.md</c> §4.5). There is deliberately no more surface than
/// that: no update and no delete.
///
/// <b>It is not a PostgreSQL contract.</b> No table name, column type, index,
/// connection, or EF Core type appears here: the members are expressed in terms
/// of the authoritative <see cref="BattleResult"/> the callers own, exactly as
/// <see cref="IBattleStateRepository"/> is for Redis
/// (<c>ARCHITECTURE.md</c> §2.1 item 3).
///
/// <b>Failure is not this contract's to soften.</b> <c>DATABASE.md</c> §1
/// "Identity and reward sourcing" item 3 requires the battle-end path to fail
/// closed when the durable write does not happen, so no implementation may
/// report a failed or refused write as a success: an implementation raises its
/// failure to the caller rather than returning an "unavailable" result a caller
/// could mistake for a completed write. The active Redis state is what remains
/// recoverable in that case (<c>REDIS_STATE.md</c> §3).
/// </summary>
public interface IBattleResultRepository
{
    /// <summary>
    /// Writes a battle's durable result (<c>DATABASE.md</c> §1).
    ///
    /// <b>One row per battle, enforced by the key.</b>
    /// <c>BattleResultId</c> <i>is</i> the battle's own <c>BattleId</c>
    /// (<c>DATABASE.md</c> §1 sourcing item 1), so the row's primary key is the
    /// duplicate protection: a repeated terminal persistence attempt for one
    /// battle must not produce a second row, and no separate idempotency
    /// mechanism, upsert policy, or retry queue is introduced
    /// (<c>REDIS_STATE.md</c> §3 — a failed battle-end delete cannot produce a
    /// second result).
    ///
    /// How a repeat is handled is the storage's to express against that key —
    /// the contract requires only that the end state is at most one row for the
    /// battle, holding the terminal result.
    ///
    /// <b>The return value reports whether this call was the first durable
    /// write.</b> <c>true</c> means the row was newly stored by this call — the
    /// documented terminal transition became durable here. <c>false</c> means
    /// the result was already durably stored, identically, so nothing was
    /// written. It is reported because the battle-end path's Player XP grant
    /// (<c>COMBAT_RULES.md</c> §7.2) must be bound to the <i>first</i> durable
    /// write and must not be re-applied by a retry or reconciliation of the
    /// same battle's result. That reconciles the grant against the existing
    /// primary-key guard instead of adding a second idempotency mechanism
    /// (<c>DATABASE.md</c> §1 sourcing item 1, <c>ARCHITECTURE.md</c> §5).
    ///
    /// It is an observation of what the store did, never a decision: an
    /// implementation that cannot distinguish the two cases must not report
    /// <c>true</c> for an already-stored row, because that would turn a retry
    /// into a duplicate grant.
    /// </summary>
    /// <param name="result">
    /// The finished result to persist — every value already resolved from
    /// authoritative server state by the caller.
    /// </param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>
    /// <c>true</c> when this call performed the first durable write of the
    /// result; <c>false</c> when an identical row was already stored and
    /// nothing was written.
    /// </returns>
    Task<bool> AddAsync(BattleResult result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a battle's durable result by its key, or <c>null</c> when no row
    /// exists (<c>API_CONTRACTS.md</c> §4).
    ///
    /// <b>Absence is reported as absence.</b> A battle that is still active, one
    /// whose result could not be written, and one that never existed all read as
    /// <c>null</c> — the endpoint's documented <c>404 BATTLE_NOT_FOUND</c>
    /// covers "does not exist or is not theirs" without disclosing which
    /// (<c>API_CONTRACTS.md</c> §4 notes 6–7).
    /// </summary>
    /// <param name="battleResultId">
    /// The row's primary key — the battle's own <c>BattleId</c>
    /// (<c>DATABASE.md</c> §1).
    /// </param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The stored result, or <c>null</c> when none is stored.</returns>
    Task<BattleResult?> GetByIdAsync(
        string battleResultId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one Player's completed-battle history in the documented order
    /// (<c>API_CONTRACTS.md</c> §4.5).
    ///
    /// <b>The Player is the only scope, and the caller supplies it from the
    /// authenticated session.</b> §4.5 note 8 requires the read to be scoped to
    /// the identity resolved from the authenticated session, so the filter is
    /// part of the query itself: another Player's rows are never materialized for
    /// this caller (note 8: "the endpoint discloses nothing about whether another
    /// Player has any history"; <c>GAME_RULES.md</c> §18, ADR-001, ADR-014). No
    /// request-supplied identity may reach this member — it takes what the
    /// authentication boundary resolved, exactly as the owner-only read does
    /// (§4 note 7).
    ///
    /// <b>The ordering is the contract's, not a storage convenience.</b> §4.5
    /// note 4 orders elements by <c>CompletedAt</c> <b>descending</b>, with
    /// <c>BattleResultId</c> <b>descending</b> as the deterministic tie-break when
    /// two results share a <c>CompletedAt</c>; clients may rely on it. An
    /// implementation must therefore express both components in the query rather
    /// than depend on unspecified database or provider default ordering, and it
    /// must not sort in memory after loading. The documented
    /// <c>BattleResult(PlayerId, CompletedAt DESC)</c> index is this query's
    /// consumer (<c>DATABASE.md</c> §4).
    ///
    /// <b>The whole history is returned.</b> §4.5 note 5 makes the array the
    /// <i>complete</i> history — no <c>page</c>, <c>limit</c>, <c>offset</c>,
    /// <c>cursor</c>, or other bounding parameter exists in MVP, and no filter,
    /// sort, or search parameter does either (note 6). This member therefore takes
    /// no paging, filtering, or sorting argument, so no such surface can be
    /// introduced here.
    ///
    /// <b>Only durable results can be returned.</b> §4.5 note 10: only persisted
    /// terminal <c>BattleResult</c> rows exist, so an active battle never appears;
    /// note 11: a battle whose durable write failed is simply absent — no
    /// <c>pending</c>, <c>partial</c>, <c>failed</c>, or placeholder entry is
    /// synthesized here.
    ///
    /// <b>An empty history is an empty sequence, not an error.</b> §4.5 note 9: a
    /// Player with no completed battles receives <c>200</c> with <c>[]</c>, so a
    /// Player with no rows reads as no rows.
    /// </summary>
    /// <param name="playerId">
    /// The authenticated Player whose own results are read
    /// (<c>API_CONTRACTS.md</c> §2.3, §4.5 note 8) — resolved server-side from the
    /// session's <c>player_id</c> claim, never from request input.
    /// </param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The Player's durable results, most recent first — ordered by
    /// <c>CompletedAt</c> descending and then <c>BattleResultId</c> descending —
    /// or an empty sequence when the Player has none.
    /// </returns>
    Task<IReadOnlyList<BattleResult>> ListByPlayerIdAsync(
        string playerId,
        CancellationToken cancellationToken = default);
}
