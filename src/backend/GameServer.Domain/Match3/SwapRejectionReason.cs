namespace GameServer.Domain.Match3;

/// <summary>
/// Why a requested Swap was rejected (<c>MATCH3_RULES.md</c> §2.1.2).
///
/// Each member is one of the documented rejection reasons. The set is closed:
/// §2.1.2 defines exactly these, and no additional restriction may be invented
/// (<c>AGENTS.md</c> §7).
///
/// The reason is reported <b>to the caller only</b> — a rejected Swap emits no
/// Battle Event (<c>MATCH3_RULES.md</c> §2.1.5 item 6,
/// <c>GAME_EVENTS.md</c> §1.2) — so this enum is a Domain value and not a wire
/// contract.
/// </summary>
public enum SwapRejectionReason
{
    /// <summary>
    /// The swap is legal; nothing is rejected. The only reason that accompanies
    /// an accepted result (<c>MATCH3_RULES.md</c> §2.1.2).
    /// </summary>
    None = 0,

    /// <summary>
    /// <c>from</c> or <c>to</c> is outside <c>0..63</c>, or the two name the
    /// same cell (<c>MATCH3_RULES.md</c> §2.1.2 items 1–2):
    ///
    /// <code>
    /// 1. Index range  — a value outside 0..63 is not a cell
    /// 2. Distinctness — a swap of a cell with itself is not a Swap
    /// </code>
    ///
    /// Both are reported as this single reason because §2.1.2 names one
    /// rejection code for both checks.
    /// </summary>
    InvalidCellIndex = 1,

    /// <summary>
    /// The two cells are not orthogonally adjacent — a diagonal pair, a distant
    /// pair, or a pair that differs by one index across a row boundary
    /// (<c>MATCH3_RULES.md</c> §2.1.2 item 3, §2.1.3 item 1).
    ///
    /// Adjacency is evaluated on the §1.0 row-major mapping, never on raw index
    /// arithmetic alone: index <c>7</c> and index <c>8</c> differ by 1 but are
    /// not adjacent (row 0 column 7 vs. row 1 column 0), and no wraparound
    /// exists across a row edge.
    /// </summary>
    InvalidSwap = 2,

    /// <summary>
    /// The cells are orthogonally adjacent, but exchanging them produces no §3
    /// Match (<c>MATCH3_RULES.md</c> §2.1.2 item 4, §2 item 3).
    ///
    /// A Swap that produces no Match is not a resolved Swap with a zero-Match
    /// result: it is a rejected action and the board reverts (§2.1.2 item 4).
    /// </summary>
    NoMatchFromSwap = 3,

    /// <summary>
    /// The action's unordered pair <c>{from, to}</c> is exactly the pair most
    /// recently committed to the board, so the action has already been applied
    /// and is rejected rather than applied a second time
    /// (<c>MATCH3_RULES.md</c> §2.1.4 item 2).
    ///
    /// The previous commit already exchanged those two cells, so applying it
    /// again would revert it — which is never a Swap (§2 item 3 requires a
    /// produced Match). The two cells are exchanged once per Turn at most.
    ///
    /// The check reads <c>BattleState.LastCommittedSwapPair</c>
    /// (<c>GAME_STATE.md</c> §2.1.10), the authoritative record of the most
    /// recently committed pair. It is a server-side decision: the client's
    /// correlation value is <b>not</b> the source of truth and is never
    /// consulted (<c>MATCH3_RULES.md</c> §2.1.4 items 1 and 5,
    /// <c>SIGNALR_PROTOCOL.md</c> §2 item 1). Because the stored pair is
    /// canonical, <c>(13, 12)</c> and <c>(12, 13)</c> are the same action here
    /// (§2.1.1 item 2).
    ///
    /// A stale request commits nothing: the board and every counter are
    /// unchanged and <c>LastCommittedSwapPair</c> keeps its value
    /// (<c>MATCH3_RULES.md</c> §2.1.5, <c>GAME_STATE.md</c> §2.1.10 items 6–7).
    /// </summary>
    StaleAction = 4,
}

/// <summary>
/// The documented wire spelling of a Swap rejection reason
/// (<c>SIGNALR_PROTOCOL.md</c> §5 item 3).
///
/// The protocol's rejection codes are the upper snake-case names of
/// <c>MATCH3_RULES.md</c> §2.1.2's checks — <c>INVALID_CELL_INDEX</c>,
/// <c>INVALID_SWAP</c>, <c>STALE_ACTION</c>, <c>NO_MATCH_FROM_SWAP</c> — which are
/// not the C# member names. This is the one place the mapping is stated, so the
/// transport does not derive a code by casing an identifier: a mechanical
/// translation of <see cref="SwapRejectionReason.StaleAction"/> yields
/// <c>STALEACTION</c>, which is a code the protocol never defines.
/// </summary>
public static class SwapRejectionCodes
{
    /// <summary>
    /// The documented contract code of a rejection reason
    /// (<c>SIGNALR_PROTOCOL.md</c> §5 item 3, <c>MATCH3_RULES.md</c> §2.1.2).
    /// </summary>
    /// <param name="reason">The rejection reason to spell.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="reason"/> is <see cref="SwapRejectionReason.None"/>, which is
    /// the accepted result's reason and not a rejection, so it has no code.
    /// </exception>
    public static string ToContractCode(SwapRejectionReason reason) => reason switch
    {
        SwapRejectionReason.InvalidCellIndex => "INVALID_CELL_INDEX",
        SwapRejectionReason.InvalidSwap => "INVALID_SWAP",
        SwapRejectionReason.NoMatchFromSwap => "NO_MATCH_FROM_SWAP",
        SwapRejectionReason.StaleAction => "STALE_ACTION",
        _ => throw new ArgumentOutOfRangeException(
            nameof(reason),
            reason,
            "A rejected Swap names the check that failed (MATCH3_RULES.md §2.1.2); "
            + "None is the accepted result's reason and has no rejection code."),
    };
}