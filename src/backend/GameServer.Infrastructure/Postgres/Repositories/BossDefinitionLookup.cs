using GameServer.Application.Battle;
using GameServer.Domain.Bosses;
using GameServer.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Infrastructure.Postgres.Repositories;

/// <summary>
/// The PostgreSQL implementation of the battle-end Boss Identity lookup
/// (<c>DATABASE.md</c> §1 note item 2) — <c>ARCHITECTURE.md</c> §3's
/// "PersistenceRepository (Postgres)", Infrastructure layer.
///
/// <b>It is the resolver owner TASK-051 named.</b> <c>DATABASE.md</c> §1 note
/// item 2: "The resolution <c>Identity</c> → <c>BossDefinitionId</c> is a
/// <b>PostgreSQL lookup by <c>Identity</c></b>, performed on the battle-end path
/// … The <b><c>Infrastructure</c> layer owns the lookup</b> … This introduces
/// <b>no new abstraction, resolver service, registry, or read model</b>: the
/// lookup is a query on the existing persistence boundary." This type is
/// exactly that query.
///
/// <b>It returns a key, not a definition.</b> The battle-end path needs the
/// row's persistence key for the foreign key, and the battle's combat values
/// were already composed from the authoritative Domain content at battle
/// creation (<c>DATABASE.md</c> §1 note item 1 — the combat stats remain Domain
/// configuration and are not columns). Returning a full definition would invite
/// reading values from a second source and would be the duplicate
/// representation <c>GAME_STATE.md</c> §0 item 5 forbids.
///
/// <b>The lookup is by <c>Identity</c> alone.</b> <c>DATABASE.md</c> §3 makes
/// <c>BossDefinition.Identity NOT NULL, UNIQUE</c> — "the unique target of the
/// FK lookup in §1" — so the query is a single-row read. It never matches on
/// <c>BossDefinitionId</c> (the two are separate values that must never be
/// substituted for each other, §1 note item 2), never on a display name (which
/// is not a column, §1 note item 1), and never on a pattern or a convention.
///
/// <b>Not found returns <c>null</c>.</b> That is the defined outcome the
/// battle-end path fails closed on (<c>DATABASE.md</c> §1 sourcing item 3): no
/// result row, no active-state delete, battle recoverable. No fallback key is
/// produced here.
/// </summary>
public sealed class BossDefinitionLookup : IBossDefinitionLookup
{
    private readonly GameDbContext _dbContext;

    public BossDefinitionLookup(GameDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<string?> FindBossDefinitionIdByIdentityAsync(
        string bossIdentity,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bossIdentity);

        // DATABASE.md §1 note item 2 / §3: the row whose Identity equals the
        // battle's canonical technical Boss Identity. `Identity` is UNIQUE, so
        // this is the documented single-row lookup; the projection reads only the
        // persistence key, because that is the value the foreign key needs.
        //
        // The comparison is against the mapped property itself — not against the
        // wrapper's `Value` member — because the property conversion is what the
        // provider translates. Comparing through the wrapper's member would make
        // the expression untranslatable, and client-evaluating it would read the
        // whole table to find one row.
        var identity = new BossId(bossIdentity);

        return await _dbContext.BossDefinitions
            .AsNoTracking()
            .Where(definition => definition.BossId == identity)
            .Select(definition => definition.BossDefinitionId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
