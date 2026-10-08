using GameServer.Application.Battle;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameServer.Infrastructure.Postgres;

/// <summary>
/// The EF Core implementation of the battle-end transaction boundary
/// (<c>DATABASE.md</c> §1, <c>ARCHITECTURE.md</c> §4 item 4) — Infrastructure
/// layer.
///
/// <b>It begins one transaction on the scoped <see cref="GameDbContext"/> and
/// nothing else.</b> The context it is constructed from is the same instance the
/// battle-end repositories are constructed from — one scope resolves one
/// <see cref="GameDbContext"/> — so every <c>SaveChangesAsync</c> the battle-end
/// step performs (the <c>BattleResult</c> insert, the Player progression update,
/// and the Pet progression update) executes on that context while this transaction
/// is open and therefore enlists in it. That is what makes the three writes one
/// atomic unit instead of three independent commits.
///
/// <b>No second context, no ambient transaction.</b> The transaction is not a
/// <c>TransactionScope</c>, it does not enlist anything implicitly, and it does not
/// touch another connection: an implementation that began a transaction here and
/// wrote through a different context would be a transaction in name only
/// (<c>AGENTS.md</c> §9). Nothing is retried here either — a failure raises to the
/// caller, which is the documented behaviour of the battle-end path
/// (<c>DATABASE.md</c> §1 sourcing item 3).
///
/// <b>Disposal is the documented undo path.</b> The scope wraps the framework
/// transaction, which rolls back when it is disposed without having been
/// committed; <see cref="Scope.RollbackAsync"/> states that explicitly for the
/// failure path. Committing or rolling back through the framework transaction
/// completes it, so the wrapper adds no state of its own and cannot disagree with
/// the database about whether the unit of work ended.
/// </summary>
public sealed class BattleEndTransaction : IBattleEndTransaction
{
    private readonly GameDbContext _dbContext;

    public BattleEndTransaction(GameDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public async Task<IBattleEndTransactionScope> BeginAsync(
        CancellationToken cancellationToken = default)
    {
        // DATABASE.md §1: the three battle-end writes are one unit of work, so the
        // transaction is opened on the context that performs all three. Beginning it
        // is itself a database round trip, so its failure propagates: no write of
        // this battle end may proceed without one.
        var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        return new Scope(transaction);
    }

    /// <summary>
    /// One in-flight transaction over the shared context. It forwards to the
    /// framework transaction and adds no bookkeeping: EF Core's own transaction
    /// already reports a second commit or rollback of a completed transaction as an
    /// error (<see cref="IBattleEndTransactionScope"/>), and its disposal is what
    /// undoes an uncommitted unit of work.
    /// </summary>
    private sealed class Scope : IBattleEndTransactionScope
    {
        private readonly IDbContextTransaction _transaction;

        public Scope(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        /// <inheritdoc />
        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            _transaction.CommitAsync(cancellationToken);

        /// <inheritdoc />
        public Task RollbackAsync(CancellationToken cancellationToken = default) =>
            _transaction.RollbackAsync(cancellationToken);

        /// <inheritdoc />
        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }
}