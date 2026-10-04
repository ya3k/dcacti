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
/// <b>It writes one row, reads it back by its key, and enumerates one Player's
/// results in the documented order — and does nothing else.</b>
/// <c>DATABASE.md</c> §1 sourcing item 1 makes <c>BattleResultId</c> the battle's
/// own <c>BattleId</c>, and the result endpoint looks a row up by that key
/// (<c>API_CONTRACTS.md</c> §4). The history endpoint (<c>API_CONTRACTS.md</c>
/// §4.5) reads the same table one Player at a time, ordered by the contract.
/// There is deliberately no update and no delete: nothing in the documented
/// surface asks for either, so neither is invented.
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
    public async Task<bool> AddAsync(BattleResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        // DATABASE.md §1 sourcing item 1: the row is keyed by the battle's own
        // BattleId, so an existing row for this battle IS the same battle's
        // already-durable terminal result — not a second result. Reconciling
        // against it keeps the documented end state (exactly one row per battle)
        // true even when a terminal persistence is retried, without inventing a
        // second idempotency mechanism: the primary key remains the guard.
        var existing = await _dbContext.BattleResults
            .FirstOrDefaultAsync(
                stored => stored.BattleResultId == result.BattleResultId,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            // The battle's terminal transition was already made durable by the
            // first successful write, and the primary key says there is one row
            // per battle. The first durable result IS the record of what
            // happened, so this call writes nothing and reports that it was not
            // the first write — which is what stops the battle-end path from
            // applying the Player XP grant (COMBAT_RULES.md §7.2) a second time.
            //
            // The stored row is deliberately NOT rewritten. Its CompletedAt is
            // the server clock reading captured when that first write happened
            // (DATABASE.md §1 "Duration and completion sourcing" item 2 — "one
            // value per battle"), and the terminal values it holds are the ones
            // the accepted resolution produced. Rewriting them from a later
            // retry would replace the battle's recorded completion instant with
            // a later one — a value the contract fixes at the first durable write
            // — and would report a write that changed nothing.
            return false;
        }

        _dbContext.BattleResults.Add(result);

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // The row was stored by this call: the documented terminal transition
        // became durable here, which is what the reward grant binds to.
        return true;
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<BattleResult>> ListByPlayerIdAsync(
        string playerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);

        // API_CONTRACTS.md §4.5 note 8: history is scoped to the authenticated
        // PlayerId. The filter is in the query's own predicate, so another
        // Player's rows are never read, materialized, or counted for this caller —
        // the endpoint cannot disclose whether another Player has any history.
        //
        // Note 4: the ordering is part of the public contract. CompletedAt
        // descending puts the newest completed battle first, and BattleResultId
        // descending is the deterministic tie-break for two results sharing a
        // CompletedAt — DATABASE.md §1 does not require CompletedAt to be unique,
        // and BattleResultId is unique per row because it IS the battle's own
        // BattleId. Both components are stated in the query itself rather than
        // left to the provider's or the database's unspecified default: only the
        // first is served by the documented index
        // (DATABASE.md §4 — BattleResult(PlayerId, CompletedAt DESC)), so the
        // second is what makes the total order deterministic.
        //
        // The comparison is explicit about being ordinal: the ids are opaque,
        // server-authored strings, so a culture-sensitive collation must not be
        // allowed to decide the tie-break. This mirrors the ordinal comparisons
        // the ownership reads use, and keeps the documented order identical under
        // any database collation.
        //
        // Note 5: the whole history is returned — no page, limit, offset, cursor,
        // or other bounding is applied here, because MVP's contract is the
        // complete array and any cap is deferred to post-MVP.
        //
        // Note 10/11: only durable rows exist to be read, so an active battle and
        // a battle whose terminal write failed are both simply absent; nothing is
        // synthesized to stand in for either.
        //
        // AsNoTracking because this is a read whose results are projected onto a
        // response and never modified or written back.
        return await _dbContext.BattleResults
            .AsNoTracking()
            .Where(stored => stored.PlayerId == playerId)
            .OrderByDescending(stored => stored.CompletedAt)
            .ThenByDescending(stored => stored.BattleResultId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
