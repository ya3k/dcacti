using System.Collections.Concurrent;

namespace GameServer.Application.Runtime;

/// <summary>
/// Application-layer boundary for the realtime runtime connection lifecycle.
///
/// Responsibility: track the technical lifecycle of a hub connection
/// (established / reconnecting / re-established / ended) and report it to the
/// caller. It is the Application-layer counterpart to the thin
/// <c>BattleHub</c> (ARCHITECTURE.md §1: "Hub — thin, delegates to
/// Application"; §2.1: "Application orchestrates, contains no game rule
/// logic").
///
/// This service must never:
/// <list type="bullet">
/// <item>implement Match-3, combat, or any domain rule (ARCHITECTURE.md §2.1),</item>
/// <item>compute authoritative gameplay values (GAME_RULES.md §18, ADR-001),</item>
/// <item>read or write active battle state (REDIS_STATE.md — that is
/// <c>IBattleStateRepository</c>'s, not this boundary's),</item>
/// <item>emit Battle Events (GAME_EVENTS.md — owned by battle resolution).</item>
/// </list>
///
/// The in-memory connection registry here is deliberately not a battle-state
/// store. It holds no <c>battleId</c>-scoped data and is not a substitute for
/// <c>REDIS_STATE.md</c>: it is destroyed with the process and is safe to lose.
/// </summary>
public sealed class RuntimeService
{
    private readonly ConcurrentDictionary<string, RuntimeStatus> _connections = new();
    private long _connectionSequence;

    /// <summary>
    /// Records that a hub connection was established and returns its status.
    /// </summary>
    public RuntimeStatus OnConnected(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        var status = new RuntimeStatus(
            ConnectionId: connectionId,
            Status: RuntimeConnectionStatus.Connected,
            Sequence: Interlocked.Increment(ref _connectionSequence),
            ObservedAt: DateTimeOffset.UtcNow);

        _connections[connectionId] = status;
        return status;
    }

    /// <summary>
    /// Records that a hub connection ended. Returns the last known status for
    /// the connection, or <c>null</c> if it was never tracked.
    /// </summary>
    public RuntimeStatus? OnDisconnected(string connectionId, bool faulted = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        if (!_connections.TryRemove(connectionId, out var previous))
        {
            return null;
        }

        return previous with
        {
            Status = faulted
                ? RuntimeConnectionStatus.Faulted
                : RuntimeConnectionStatus.Disconnected,
            Sequence = Interlocked.Increment(ref _connectionSequence),
            ObservedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Returns the tracked status for a connection, or <c>null</c> if unknown.
    /// </summary>
    public RuntimeStatus? GetStatus(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        return _connections.TryGetValue(connectionId, out var status) ? status : null;
    }

    /// <summary>Number of connections currently tracked as established.</summary>
    public int ActiveConnectionCount => _connections.Count;
}