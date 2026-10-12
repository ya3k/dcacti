using GameServer.Application.Battle;
using GameServer.Domain.Battle;

namespace GameServer.Application.Tests;

/// <summary>
/// The active-state store double for the <b>deterministic</b> race regressions
/// (<c>REDIS_STATE.md</c> §4).
///
/// It stores the same records <see cref="InMemoryBattleStateRepository"/> stores —
/// the two documented semantics the service depends on (the <c>Sequence</c>
/// compare-and-set and creation/absence) are that double's, not a second
/// implementation of them — and adds exactly the two controls a race test needs
/// to be a proof rather than a coincidence:
///
/// <list type="number">
/// <item><b>A read rendezvous.</b> <see cref="RendezvousReads"/> holds the leading
/// <c>n</c> reads open until all <c>n</c> have read. Two resolutions can therefore
/// be made to read the <i>same</i> pre-state, which is the condition an
/// optimistic-concurrency race is defined by — starting two tasks and hoping they
/// interleave tests the scheduler, not the contract.</item>
/// <item><b>An injected competitor.</b>
/// <see cref="ConcurrentWinnerBeforeFirstWrite"/> lets a test make the first
/// compare-and-set meet a record a competitor committed after the request's read
/// — the "the stored <c>Sequence</c> has moved on" case
/// (<c>REDIS_STATE.md</c> §4 item 2) without needing a real second request to win
/// a race at the right moment.</item>
/// </list>
///
/// It is <b>thread-safe</b> (every access to the inner double is serialized), so
/// the service can genuinely be driven from two tasks. It is still a TEST DOUBLE,
/// not a production fallback: <c>REDIS_STATE.md</c> §2 item 2 / §7 item 5 permit
/// no in-process substitute in the running server.
/// </summary>
internal sealed class ContendedBattleStateRepository : IBattleStateRepository
{
    private readonly InMemoryBattleStateRepository _inner = new();
    private readonly object _gate = new();

    /// <summary>
    /// Completed as soon as <see cref="RendezvousReads"/> reads have started, so
    /// every participating read returns only after all of them have read.
    /// </summary>
    private readonly TaskCompletionSource _rendezvous =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>How many leading reads participate in the rendezvous; <c>0</c> = disabled.</summary>
    private int _rendezvousReads;

    /// <summary>How many reads have started, so the rendezvous can tell participants from later reads.</summary>
    private int _readsStarted;

    /// <summary>Whether the injected competitor has already been applied.</summary>
    private bool _concurrentWinnerApplied;

    /// <summary>How many writes the store has applied — creations and accepted updates alike.</summary>
    public int WriteCount
    {
        get
        {
            lock (_gate)
            {
                return _inner.WriteCount;
            }
        }
    }

    /// <summary>How many records the store currently holds.</summary>
    public int RecordCount
    {
        get
        {
            lock (_gate)
            {
                return _inner.RecordCount;
            }
        }
    }

    /// <summary>
    /// When set, the next compare-and-set attempts report a mismatch regardless of
    /// the stored sequence — forwarding to the inner repository's
    /// <see cref="InMemoryBattleStateRepository.ForcedConflicts"/>.
    /// </summary>
    public int ForcedConflicts
    {
        get
        {
            lock (_gate)
            {
                return _inner.ForcedConflicts;
            }
        }
        set
        {
            lock (_gate)
            {
                _inner.ForcedConflicts = value;
            }
        }
    }

    /// <summary>
    /// Replaces a stored record with the complete value another writer left it as,
    /// forwarding to <see cref="InMemoryBattleStateRepository.ApplyConcurrentWrite"/>.
    /// </summary>
    public void ApplyConcurrentWrite(BattleState state)
    {
        lock (_gate)
        {
            _inner.ApplyConcurrentWrite(state);
        }
    }

    /// <summary>
    /// Holds the leading <paramref name="count"/> reads open until all of them have
    /// read, so concurrent resolutions are forced to start from one state version.
    /// A later read (a refused write's fresh re-read, for example) is never held:
    /// it is not part of the race the rendezvous creates.
    /// </summary>
    public void RendezvousReads(int count)
    {
        _rendezvousReads = count;

        if (count <= 0)
        {
            _rendezvous.TrySetResult();
        }
    }

    /// <summary>
    /// When set, the first <see cref="TryUpdateAsync"/> first stores the record this
    /// produces from the currently stored one — a competitor's committed state —
    /// and only then evaluates the compare-and-set, which therefore meets a
    /// <c>Sequence</c> that moved on. The produced record is injected without being
    /// counted as a write of the request under test
    /// (<see cref="InMemoryBattleStateRepository.ApplyConcurrentWrite"/>).
    /// </summary>
    public Func<BattleState, BattleState>? ConcurrentWinnerBeforeFirstWrite { get; set; }

    /// <inheritdoc />
    public Task CreateAsync(BattleState state, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return _inner.CreateAsync(state, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<BattleState?> GetAsync(
        string battleId,
        CancellationToken cancellationToken = default)
    {
        BattleState? state;

        lock (_gate)
        {
            state = _inner.GetAsync(battleId, cancellationToken).GetAwaiter().GetResult();
        }

        if (Volatile.Read(ref _rendezvousReads) > 0
            && Interlocked.Increment(ref _readsStarted) <= _rendezvousReads)
        {
            if (Volatile.Read(ref _readsStarted) >= _rendezvousReads)
            {
                _rendezvous.TrySetResult();
            }

            await _rendezvous.Task.ConfigureAwait(false);
        }

        return state;
    }

    /// <inheritdoc />
    public Task<bool> TryUpdateAsync(
        BattleState state,
        int expectedSequence,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_concurrentWinnerApplied && ConcurrentWinnerBeforeFirstWrite is { } competitor)
            {
                _concurrentWinnerApplied = true;

                if (_inner.GetAsync(state.BattleId, cancellationToken).GetAwaiter().GetResult()
                    is { } stored)
                {
                    _inner.ApplyConcurrentWrite(competitor(stored));
                }
            }

            return _inner.TryUpdateAsync(state, expectedSequence, cancellationToken);
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(string battleId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return _inner.DeleteAsync(battleId, cancellationToken);
        }
    }
}
