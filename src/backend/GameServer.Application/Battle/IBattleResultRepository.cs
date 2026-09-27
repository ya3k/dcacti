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
/// <b>Two operations, because the contract has two.</b> The battle-end path
/// writes one row and the result endpoint reads one row by its key
/// (<c>API_CONTRACTS.md</c> §4, <c>DATABASE.md</c> §1 sourcing item 1). There is
/// deliberately no more surface than that: no enumeration, no history query, no
/// update, and no delete. <c>GET /api/battle/history</c> is listed in
/// <c>API_CONTRACTS.md</c> §1 but no section defines its contract, so it is not
/// implemented and no query is invented for it.
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
}
