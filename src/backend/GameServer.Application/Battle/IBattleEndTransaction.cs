namespace GameServer.Application.Battle;

/// <summary>
/// The single database transaction the battle-end writes commit in
/// (<c>DATABASE.md</c> §1, <c>ARCHITECTURE.md</c> §4 item 4).
///
/// <code>
/// BattleResultService                  Application — owns the battle-end step
///         ↓  IBattleEndTransaction           this contract
/// BattleEndTransaction (Postgres)      Infrastructure — one EF Core transaction
///         ↓
/// GameDbContext.Database.BeginTransactionAsync
/// </code>
///
/// <b>Why this contract exists.</b> The battle-end step performs three writes that
/// must become durable together or not at all — the <c>BattleResult</c> row
/// (<c>DATABASE.md</c> §1), the Player progression (<c>COMBAT_RULES.md</c> §7.2),
/// and the Pet progression (<c>PET_RULES.md</c> §5.3). Each of those writes is
/// performed by its own persistence boundary, and each of those boundaries commits
/// its own <c>SaveChangesAsync</c>. Without one enclosing transaction the three
/// commits are independent database transactions, so a failure after the result row
/// committed would leave the battle durably recorded while the reward it states was
/// never applied — and the primary-key guard that makes the reward exactly-once
/// (<c>DATABASE.md</c> §1 sourcing item 1) would then refuse the retry that could
/// still have applied it. The Pet XP would be lost permanently and
/// <c>RewardSummary</c> would disagree with the stored progression.
///
/// <b>Why it lives in Application and not beside the DbContext.</b> The
/// transaction has to be begun and committed by the boundary that owns the
/// battle-end ordering rule (<c>ARCHITECTURE.md</c> §4 item 4,
/// <c>REDIS_STATE.md</c> §3): the commit must complete <i>before</i> the active
/// state is cleared, because a cleared <c>battle:{battleId}:state</c> for a write
/// that never became durable would destroy the only authoritative copy of the
/// battle (<c>REDIS_STATE.md</c> §3, citing §2 item 2). That boundary is
/// <see cref="BattleResultService"/>, which is an Application type and names no
/// persistence framework (<c>ARCHITECTURE.md</c> §2.1 item 1), so the transaction
/// is reached through this contract exactly as the result store, the two
/// progression boundaries, and the active-state store already are. The
/// implementation is Infrastructure's.
///
/// <b>All participants must share one <c>GameDbContext</c>.</b> The contract is
/// implemented over the request-scoped context, and the repositories that perform
/// the three writes are constructed from that same scoped context — a scope
/// resolves one context, which is the same fact that makes a single
/// <c>SaveChangesAsync</c> atomic across entities elsewhere
/// (<c>DATABASE.md</c> §2 item 4). SQL issued on that context while this
/// transaction is open participates in it; a write on any other connection would
/// not, so an implementation must never begin a transaction on one context and
/// write through another.
///
/// <b>What this contract deliberately is not.</b> It is not a unit of work, a
/// repository, a retry policy, or a distributed transaction: it begins one
/// database transaction, ends it, and holds nothing else. There is no ambient
/// transaction, no <c>TransactionScope</c>, no compensation, and no second
/// idempotency mechanism — <c>DATABASE.md</c> §1 sourcing item 1 keeps the
/// primary key as the exactly-once guard, and <c>ARCHITECTURE.md</c> §5 /
/// <c>AGENTS.md</c> §9 forbid inventing infrastructure the contract does not
/// require.
///
/// <b>Failure is raised, not absorbed.</b> A transaction that cannot be begun,
/// committed, or rolled back raises to the caller. Nothing here reports a
/// persistence outcome, because the only durable outcome is the one the database
/// itself applies — this contract defines no "unavailable" result a caller could
/// mistake for success.
/// </summary>
public interface IBattleEndTransaction
{
    /// <summary>
    /// Begins the transaction the battle-end writes of one battle will commit in.
    ///
    /// <b>The returned scope is the transaction.</b> Everything written through the
    /// shared context until <see cref="IBattleEndTransactionScope.CommitAsync"/>
    /// completes is part of it, and disposing the scope without committing undoes
    /// it. The transaction's lifetime is deliberately the caller's — it must
    /// enclose every write of the battle-end step and end before anything outside
    /// that write observes success (<c>ARCHITECTURE.md</c> §4 item 4).
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels beginning the transaction. A cancellation here means no transaction
    /// was begun and no battle-end write is in flight.
    /// </param>
    /// <returns>The begun transaction, to be committed or disposed.</returns>
    Task<IBattleEndTransactionScope> BeginAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// One in-flight battle-end transaction — the scope
/// <see cref="IBattleEndTransaction.BeginAsync"/> hands back.
///
/// <b>It mirrors the shape of the framework transaction it wraps</b>
/// (<c>BeginTransactionAsync</c> / <c>CommitAsync</c> / <c>RollbackAsync</c> /
/// disposal) without naming it, because the caller is an Application type
/// (<c>ARCHITECTURE.md</c> §2.1 item 1). Disposal without a commit is the
/// documented undo path as well: an undisposed scope that was never committed
/// leaves nothing of that unit of work durable.
///
/// <b>Commit and rollback are terminal.</b> After either, the transaction is
/// finished: a second call is an error rather than a silent no-op, so a caller
/// that believed it had undone a committed unit of work is not told otherwise.
/// </summary>
public interface IBattleEndTransactionScope : IAsyncDisposable
{
    /// <summary>
    /// Commits every write made through the shared context since
    /// <see cref="IBattleEndTransaction.BeginAsync"/> — the point at which the
    /// battle's result row and both progression tracks become durable together, or
    /// none of them does.
    ///
    /// <b>Only a completed commit may be followed by the active-state delete</b>
    /// (<c>REDIS_STATE.md</c> §3). A commit that fails leaves nothing durable, so
    /// the caller must treat the battle end as not persisted: the authoritative
    /// state stays in the store and the battle remains retryable
    /// (<c>DATABASE.md</c> §1 sourcing item 3).
    /// </summary>
    /// <param name="cancellationToken">Cancels the commit.</param>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Undoes every write made through the shared context since
    /// <see cref="IBattleEndTransaction.BeginAsync"/>, leaving the battle's result
    /// and both progression tracks exactly as they were before the attempt.
    ///
    /// <b>This is the failure path of the battle-end write.</b> After a rollback
    /// the durable result is absent, so the primary-key guard does not block a
    /// later attempt and the whole battle end — result row, both grants, and the
    /// active-state delete — can be re-run (<c>DATABASE.md</c> §1 sourcing item 3).
    /// </summary>
    /// <param name="cancellationToken">Cancels the rollback.</param>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}