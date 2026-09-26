namespace GameServer.Application.Runtime;

/// <summary>
/// Technical session/connection status reported to a client.
///
/// This is runtime infrastructure only. It deliberately carries no gameplay
/// state: authoritative battle state is <c>BattleState</c> (<c>GAME_STATE.md</c>
/// §2), persisted as the active-state record <c>battle:{battleId}:state</c>
/// (<c>REDIS_STATE.md</c> §1–§2) — a different boundary from this one, and
/// nothing here is a substitute for it.
/// </summary>
public enum RuntimeConnectionStatus
{
    /// <summary>The connection is established and the hub accepted it.</summary>
    Connected = 0,

    /// <summary>The connection is being established.</summary>
    Connecting = 1,

    /// <summary>The connection ended and has not been re-established.</summary>
    Disconnected = 2,

    /// <summary>The connection ended because of an error.</summary>
    Faulted = 3,
}

/// <summary>
/// Immutable technical snapshot of a hub connection's runtime status.
///
/// <paramref name="Sequence"/> is the runtime connection sequence — a monotonic
/// counter of technical lifecycle transitions. It is NOT
/// <c>BattleState.Sequence</c> (GAME_STATE.md §5), which orders gameplay
/// resolutions and is the active-state record's own concurrency token
/// (<c>REDIS_STATE.md</c> §4 item 6).
/// </summary>
public sealed record RuntimeStatus(
    string ConnectionId,
    RuntimeConnectionStatus Status,
    long Sequence,
    DateTimeOffset ObservedAt);