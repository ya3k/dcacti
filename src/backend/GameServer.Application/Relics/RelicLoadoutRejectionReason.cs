namespace GameServer.Application.Relics;

/// <summary>
/// The reasons a submitted <c>relicLoadout</c> is rejected
/// (<c>RELIC_RULES.md</c> §2.1, §2.4; <c>API_CONTRACTS.md</c> §3).
///
/// Every member maps to the <b>single</b> documented invalid-loadout error
/// code — <c>INVALID_LOADOUT</c> (<c>API_CONTRACTS.md</c> §3, §6). The
/// distinction exists so the server can log and diagnose a rejection without
/// widening the wire contract; it is <b>not</b> a set of new client-facing
/// error codes, and no API response may expose a distinct code per member
/// (<c>API_CONTRACTS.md</c> §6 does not enumerate an exhaustive global list,
/// and TASK-027 is not authorized to add one).
/// </summary>
public enum RelicLoadoutRejectionReason
{
    /// <summary>
    /// The selection was accepted; no rejection occurred. Present so a
    /// successful result needs no nullable reason.
    /// </summary>
    None = 0,

    /// <summary>
    /// The selection was outside the documented 3–5 bound
    /// (<c>RELIC_RULES.md</c> §2.1 item 1, <c>API_CONTRACTS.md</c> §3). The
    /// count is of selected <b>instances</b>: a request that is out of range
    /// after de-duplication is still out of range, because a duplicate is
    /// rejected rather than collapsed (<c>RELIC_RULES.md</c> §2.4 item 4).
    /// </summary>
    CountOutOfRange = 1,

    /// <summary>
    /// A selected identity is not an owned Relic instance of the requesting
    /// Player (<c>RELIC_RULES.md</c> §2.1 item 2) — it does not exist, or it
    /// belongs to another Player. The server establishes ownership from
    /// persistence, never from a client-supplied claim
    /// (<c>GAME_RULES.md</c> §18, ADR-001).
    /// </summary>
    NotOwned = 2,

    /// <summary>
    /// The same <c>RelicInstanceId</c> appeared more than once in one
    /// selection (<c>RELIC_RULES.md</c> §2.4 items 1–2). A Relic instance
    /// occupies at most one slot.
    /// </summary>
    DuplicateInstance = 3,
}
