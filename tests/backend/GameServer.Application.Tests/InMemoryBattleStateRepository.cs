using GameServer.Application.Battle;
using GameServer.Domain.Battle;

namespace GameServer.Application.Tests;

/// <summary>
/// An in-memory <see cref="IBattleStateRepository"/> for the Application-layer
/// unit tests.
///
/// <b>It is a TEST DOUBLE, not a production fallback.</b> The real store is
/// <c>GameServer.Infrastructure.Redis.BattleStateRepository</c>, and
/// <c>REDIS_STATE.md</c> §2 item 2 / §7 item 5 permit no in-process substitute
/// for it in the running server — the production composition registers the Redis
/// implementation and nothing else. This type exists so a test that asserts a
/// <i>gameplay</i> rule (damage, Passives, Victory/Defeat, serialization
/// round-trips) can run the real <see cref="BattleStateService"/> pipeline
/// without a live Redis instance, exactly as the other suites substitute the
/// Player/Pet/Card repositories with an in-memory <c>GameDbContext</c>.
///
/// <b>It models the two documented semantics the service depends on</b>, so a
/// test cannot pass against a double that is more permissive than the contract:
/// <list type="bullet">
/// <item><b>The <c>Sequence</c> compare-and-set</b> (<c>REDIS_STATE.md</c> §4
/// items 1–3): <see cref="TryUpdateAsync"/> writes only while the stored
/// <c>Sequence</c> equals the expected one, and reports <c>false</c> otherwise
/// without touching the stored record. <c>Turn</c> is never compared (§4
/// item 6).</item>
/// <item><b>Creation and absence</b> (<c>§3</c>): creation stores the record;
/// an unknown id reads as <c>null</c> rather than as a default state.</item>
/// </list>
///
/// It deliberately does <b>not</b> model the TTL: expiry is a property of the
/// Redis store and is verified against real Redis in the Infrastructure and
/// smoke tests, where the TTL can actually be observed.
/// </summary>
internal sealed class InMemoryBattleStateRepository : IBattleStateRepository
{
    /// <summary>
    /// The stored records, keyed by battle id. A <see cref="Dictionary{TKey,TValue}"/>
    /// is enough because the tests that use this double run one resolution at a
    /// time; the concurrency the contract guards against is exercised against
    /// real Redis, not here.
    /// </summary>
    private readonly Dictionary<string, BattleState> _records = new(StringComparer.Ordinal);

    /// <summary>
    /// The number of writes this double has applied — creations and accepted
    /// updates alike. Creation is a store write of its own
    /// (<c>REDIS_STATE.md</c> §3 "Created"), so a test can assert that a
    /// <b>rejected</b> action performed no write at all (<c>§4</c> item 7) and
    /// that an accepted one performed exactly one more (<c>§4</c> item 5).
    /// </summary>
    public int WriteCount { get; private set; }

    /// <summary>
    /// The number of records this double currently holds, so a test can assert
    /// that a rejected request created no battle.
    /// </summary>
    public int RecordCount => _records.Count;

    /// <summary>
    /// When set, the next <see cref="TryUpdateAsync"/> reports a mismatch
    /// regardless of the stored sequence — used to drive the documented
    /// abort-and-retry path (<c>REDIS_STATE.md</c> §4 item 2) deterministically.
    /// </summary>
    public int ForcedConflicts { get; set; }

    /// <inheritdoc />
    public Task CreateAsync(BattleState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        // REDIS_STATE.md §3 "Created: on POST /api/battle/start": creation is the
        // record's first write. No compare-and-set applies — there is no prior
        // Sequence of this battle's to guard.
        _records[state.BattleId] = state;
        WriteCount++;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<BattleState?> GetAsync(string battleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);

        // REDIS_STATE.md §3: absence (unknown id, or an expired record) is
        // reported as absence — never as an empty or defaulted state.
        return Task.FromResult(
            _records.TryGetValue(battleId, out var state) ? state : null);
    }

    /// <inheritdoc />
    public Task<bool> TryUpdateAsync(
        BattleState state,
        int expectedSequence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (ForcedConflicts > 0)
        {
            ForcedConflicts--;

            return Task.FromResult(false);
        }

        if (!_records.TryGetValue(state.BattleId, out var stored))
        {
            return Task.FromResult(false);
        }

        // §4 item 2: the write succeeds only if the stored Sequence still matches
        // what the resolution read. §4 item 6: Sequence is the only token
        // compared — never Turn — so a state whose Turn moved but whose Sequence
        // matches is still written.
        if (stored.Sequence != expectedSequence)
        {
            return Task.FromResult(false);
        }

        _records[state.BattleId] = state;
        WriteCount++;

        return Task.FromResult(true);
    }

    /// <summary>
    /// Replaces a stored record with the complete value another writer left it as
    /// — the state a <i>concurrent</i> resolution committed — without counting
    /// the replacement as a write of the request under test
    /// (<see cref="WriteCount"/>).
    /// </summary>
    /// <remarks>
    /// It exists for the service-level race regressions
    /// (<c>ContendedBattleStateRepository</c>, <c>BattleStateServiceTests</c>): a
    /// test must be able to make the compare-and-set encounter a record that moved
    /// on between the request's read and its write, deterministically and without
    /// a second live request. The write is not attributed to the request under
    /// test because the request did not perform it — <c>REDIS_STATE.md</c> §4
    /// item 5's "one write-back per resolution" is about the resolution being
    /// asserted, and counting a competitor's commit as one of its writes would
    /// make a "wrote nothing" assertion meaningless.
    ///
    /// It models a writer that got there first; it does not make this double a
    /// concurrency model of its own (see the type's own remarks on that).
    /// </remarks>
    public void ApplyConcurrentWrite(BattleState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _records[state.BattleId] = state;
    }

    /// <summary>
    /// The documented battle-end delete (<c>REDIS_STATE.md</c> §3), recorded so a
    /// test can assert the ordering rule the terminal path must obey: the
    /// durable result is written first, and the delete happens only after it.
    /// </summary>
    /// <remarks>
    /// It records the delete rather than only performing it, because "the delete
    /// was attempted, and attempted after the write" is the contract under test —
    /// a double that merely removed the entry could not distinguish a delete that
    /// happened on the wrong path from one that never happened.
    /// </remarks>
    public Task DeleteAsync(string battleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);

        DeleteCount++;

        if (DeleteFails)
        {
            // REDIS_STATE.md §3: a failing delete after a successful result write
            // is the documented case with no retry and no worker. The double
            // raises it so a test can prove the battle-end path keeps its result
            // rather than unwinding it.
            throw new InvalidOperationException(
                "REDIS_STATE.md §3: this double is configured to fail the battle-end delete.");
        }

        // Deleting an absent record is the documented no-op (§3: an abandoned
        // battle or one a concurrent resolution already cleared).
        _records.Remove(battleId);

        return Task.CompletedTask;
    }

    /// <summary>
    /// How many <see cref="DeleteAsync"/> calls this double has received.
    /// </summary>
    public int DeleteCount { get; private set; }

    /// <summary>
    /// When set, <see cref="DeleteAsync"/> raises instead of clearing the record —
    /// the documented <c>REDIS_STATE.md</c> §3 delete-failure case, which must
    /// leave the durable result standing.
    /// </summary>
    public bool DeleteFails { get; set; }
}
