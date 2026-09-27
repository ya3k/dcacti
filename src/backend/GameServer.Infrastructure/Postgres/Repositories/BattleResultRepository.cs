using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Infrastructure.Postgres.Repositories;

/// <summary>
/// The EF Core implementation of the durable battle result boundary
/// (<c>DATABASE.md</c> §1) — <c>ARCHITECTURE.md</c> §3's
/// "PersistenceRepository (Postgres)", Infrastructure layer.
///
/// <b>It writes one row and reads it back by its key, and does nothing else.</b>
/// <c>DATABASE.md</c> §1 sourcing item 1 makes <c>BattleResultId</c> the battle's
/// own <c>BattleId</c>, and the result endpoint looks a row up by that key
/// (<c>API_CONTRACTS.md</c> §4). There is deliberately no history query, no
/// update, and no delete: <c>GET /api/battle/history</c> is listed in
/// <c>API_CONTRACTS.md</c> §1 with no defining section, so no query is invented
/// for it.
///
/// <b>A repeated write for one battle cannot create a second row.</b>
/// <c>BattleResultId</c> is the primary key, so the database's own uniqueness is
/// the duplicate protection <c>REDIS_STATE.md</c> §3 relies on ("a failed delete
/// cannot produce a second result"). This type therefore adds no upsert policy,
/// no conflict handling, and no second idempotency mechanism — but it also does
/// not assume a repeat is impossible: if the row already exists the write is
/// reconciled against it rather than failing, because the documented end state
/// (one row holding the terminal result for that battle) is what the contract
/// requires, and a retried terminal persistence is explicitly contemplated by
/// <c>DATABASE.md</c> §1 sourcing item 3.
///
/// <b>Failure is raised, never absorbed.</b> The battle-end path must fail
/// closed when the durable write does not happen
/// (<c>DATABASE.md</c> §1 sourcing item 3) and must not delete the active state
/// in that case (<c>REDIS_STATE.md</c> §3), so a store failure propagates as the
/// exception it is. There is no "unavailable" result a caller could mistake for
/// a completed write.
/// </summary>
public sealed class BattleResultRepository : IBattleResultRepository
{
    private readonly GameDbContext _dbContext;

    public BattleResultRepository(GameDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task AddAsync(BattleResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        // DATABASE.md §1 sourcing item 1: the row is keyed by the battle's own
        // BattleId, so an existing row for this battle is the same battle's
        // already-durable result — not a second result. Reconciling against it
        // keeps the documented end state (exactly one row per battle) true even
        // when a terminal persistence is retried, without inventing a second
        // idempotency mechanism: the primary key remains the guard.
        var existing = await _dbContext.BattleResults
            .FirstOrDefaultAsync(
                stored => stored.BattleResultId == result.BattleResultId,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            if (existing == result)
            {
                // Already durable, identically: nothing to write.
                return;
            }

            // The row exists with different values. The battle's terminal
            // transition is what the first successful write recorded, and the
            // primary key says there is one row per battle — so the stored row is
            // updated to the result being persisted rather than a second row
            // being created or the write being silently dropped.
            _dbContext.BattleResults.Remove(existing);
        }

        _dbContext.BattleResults.Add(result);

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<BattleResult?> GetByIdAsync(
        string battleResultId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleResultId);

        // DATABASE.md §1 sourcing item 1 / API_CONTRACTS.md §4: the row is looked
        // up by the battle's own id. An absent row is reported as absence — it is
        // never a defaulted or partially reconstructed result, so the endpoint's
        // documented 404 is what a caller sees.
        return await _dbContext.BattleResults
            .AsNoTracking()
            .FirstOrDefaultAsync(
                stored => stored.BattleResultId == battleResultId,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
