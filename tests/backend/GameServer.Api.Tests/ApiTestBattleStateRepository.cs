using GameServer.Application.Battle;
using GameServer.Domain.Battle;

namespace GameServer.Api.Tests;

/// <summary>
/// An in-memory <see cref="IBattleStateRepository"/> for the API test hosts.
///
/// <b>It is a TEST DOUBLE, not a production fallback.</b> The running server's
/// store is <c>GameServer.Infrastructure.Redis.BattleStateRepository</c>
/// (<c>REDIS_STATE.md</c> §1–§4), and <c>§2</c> item 2 / <c>§7</c> item 5 permit
/// no in-process substitute for it. The API hosts blank
/// <c>ConnectionStrings:Redis</c> so the real composition registers no store at
/// all, then substitute this one — the same established pattern those hosts
/// already use for <c>GameDbContext</c> (an isolated in-memory store in place of
/// Npgsql). It keeps the real
/// <c>BattleStartService</c>/<c>BattleStateService</c>/<c>BattleHub</c> pipeline
/// under test while removing the infrastructure dependency.
///
/// <b>It models the documented compare-and-set</b> (<c>REDIS_STATE.md</c> §4
/// items 1–3, 6), so a test cannot pass against a double more permissive than
/// the contract: a write applies only while the stored <c>Sequence</c> equals
/// the expected one, and <c>Turn</c> is never compared. It does not model the
/// TTL — expiry is verified against real Redis in
/// <c>GameServer.Infrastructure.Tests.RedisBattleStateRepositoryTests</c>, where
/// it can actually be observed.
///
/// It is deliberately thread-safe: the API hosts serve concurrent SignalR and
/// HTTP requests, and the hub resolves this from the singleton service provider.
/// </summary>
public sealed class ApiTestBattleStateRepository : IBattleStateRepository
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, BattleState> _records =
        new(StringComparer.Ordinal);

    /// <summary>Number of battle records currently stored.</summary>
    public int RecordCount => _records.Count;

    /// <summary>
    /// How many <see cref="DeleteAsync"/> calls this double has received, so a test
    /// can assert the documented battle-end clear happened on the terminal path
    /// (<c>REDIS_STATE.md</c> §3).
    /// </summary>
    public int DeleteCount { get; private set; }

    /// <inheritdoc />
    public Task CreateAsync(BattleState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        _records[state.BattleId] = state;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<BattleState?> GetAsync(string battleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);

        // REDIS_STATE.md §3: absence is reported as absence — never as a
        // defaulted or empty state.
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

        if (!_records.TryGetValue(state.BattleId, out var stored))
        {
            return Task.FromResult(false);
        }

        // §4 item 2 / §4 item 6: the write succeeds only while the stored
        // Sequence still matches what the resolution read. Turn is never
        // compared — it is a game value carried inside the record.
        if (stored.Sequence != expectedSequence)
        {
            return Task.FromResult(false);
        }

        _records[state.BattleId] = state;

        return Task.FromResult(true);
    }

    /// <summary>
    /// The documented battle-end delete (<c>REDIS_STATE.md</c> §3). Recorded
    /// rather than merely performed so the API tests can assert that a completed
    /// battle's record was cleared, and that the clear happened on the terminal
    /// path only.
    /// </summary>
    public Task DeleteAsync(string battleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);

        DeleteCount++;

        // §3: deleting an absent record is the documented no-op — an abandoned
        // battle whose TTL already elapsed, or one a concurrent resolution
        // cleared, has reached the same end state.
        _records.TryRemove(battleId, out _);

        return Task.CompletedTask;
    }
}
