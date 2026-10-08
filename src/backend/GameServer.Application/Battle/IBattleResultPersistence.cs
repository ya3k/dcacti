using GameServer.Domain.Battle;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Application.Battle;

/// <summary>
/// The battle-end durable persistence step as the resolution boundary sees it
/// (<c>ARCHITECTURE.md</c> §4 item 4, <c>DATABASE.md</c> §1).
///
/// <code>
/// BattleStateService              singleton — owns the staging sequence
///         ↓  IBattleResultPersistence     this contract
/// BattleResultService             scoped — owns the PostgreSQL write and the
///         ↓                                documented write-then-delete order
/// BattleResult (DATABASE.md §1)
/// </code>
///
/// <b>Why the indirection exists.</b> The resolution boundary is a singleton
/// because it carries each battle's loadout and Boss configuration between the
/// request that created the battle and the ones that resolve it, while the
/// durable result boundary is scoped because it resolves the request-scoped
/// PostgreSQL context. A singleton cannot depend on a scoped service directly,
/// so the terminal step is reached through this one-method contract, which the
/// composition root implements by resolving the scoped boundary from the current
/// scope. That keeps the resolution pipeline wording the same whether the
/// terminal step is composed or not, and keeps it free of any lifetime or
/// persistence-framework detail (<c>ARCHITECTURE.md</c> §2.1 item 1 — Domain and
/// Application name no infrastructure type).
///
/// <b>It adds no behaviour.</b> The contract is one call with the same arguments
/// and the same result as <see cref="BattleResultService.PersistTerminalResultAsync"/>;
/// the ordering rule, the fail-closed rule, and the reward staging value all
/// live in that boundary.
/// </summary>
public interface IBattleResultPersistence
{
    /// <summary>
    /// Persists the durable result for a terminal resolution and then clears the
    /// battle's active state — in that order.
    /// </summary>
    /// <param name="state">
    /// The authoritative post-resolution state the store accepted
    /// (<c>GAME_STATE.md</c> §2, §5.1).
    /// </param>
    /// <param name="outcome">
    /// The terminal outcome the resolution reported (<c>GAME_EVENTS.md</c> §2).
    /// </param>
    /// <param name="cancellationToken">Cancels the lookups, the write, and the delete.</param>
    /// <returns>
    /// <c>true</c> when the durable result was written; <c>false</c> when the
    /// battle-end write failed closed because the Boss definition did not
    /// resolve (<c>DATABASE.md</c> §1 sourcing item 3).
    /// </returns>
    Task<bool> PersistTerminalResultAsync(
        BattleState state,
        BattleOutcome outcome,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The composition-root implementation of <see cref="IBattleResultPersistence"/>:
/// it resolves the scoped <see cref="BattleResultService"/> from a scope of its
/// own and forwards the call unchanged.
///
/// <b>Why it is a thin resolver and not a second service.</b> The lifetime
/// mismatch between the singleton resolution boundary and the scoped persistence
/// boundary is a composition detail, so it is solved in the composition root.
/// This type decides nothing: it adds no retry, fallback, or error handling of
/// its own, and it holds no reference to a resolved service.
///
/// <b>It opens its own scope, and that is required rather than incidental.</b>
/// The resolution boundary is a singleton, so it is reachable from the root
/// provider — and ASP.NET Core's own validation refuses to resolve a scoped
/// service from the root. A scope is therefore created per battle-end step. The
/// battle-end step is one terminal write with no request-scoped state of its own
/// (the values it persists are all carried on the argument), so a short-lived
/// scope with its own <c>GameDbContext</c> is exactly the lifetime the write
/// needs; the alternative — capturing the request scope — would both break the
/// singleton boundary and pin a context for the process's lifetime.
///
/// <b>One scope means one <c>GameDbContext</c>, which is what makes the battle-end
/// transaction cover every participating write.</b> The scoped boundary resolved
/// below, the result repository and both progression boundaries it writes
/// through, and the <see cref="IBattleEndTransaction"/> it commits through all
/// come from this same scope, so they share one context and one connection. A
/// transaction begun on it therefore encloses all three writes
/// (<c>DATABASE.md</c> §1) — a second context would make the transaction a name
/// rather than a guarantee (<c>AGENTS.md</c> §9).
///
/// <b>The scope is disposed with the call.</b> A failure inside leaves nothing
/// half-open: the failure propagates to the caller, which is the documented
/// fail-closed behaviour of <c>DATABASE.md</c> §1 sourcing item 3.
/// </summary>
internal sealed class ScopedBattleResultPersistence : IBattleResultPersistence
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ScopedBattleResultPersistence(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    /// <inheritdoc />
    public async Task<bool> PersistTerminalResultAsync(
        BattleState state,
        BattleOutcome outcome,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<BattleResultService>()
            .PersistTerminalResultAsync(state, outcome, cancellationToken)
            .ConfigureAwait(false);
    }
}
