namespace GameServer.Domain.Match3;

/// <summary>
/// A requested player Swap action — the two cells the player is exchanging
/// (<c>MATCH3_RULES.md</c> §2.1.1).
///
/// <code>
/// from   0..63   cell index of the Gem the player is moving (MATCH3_RULES.md §1.0)
/// to     0..63   cell index of the adjacent cell it is exchanged with
/// </code>
///
/// The action carries both cells and <b>no direction field</b>: the order of
/// <paramref name="From"/> and <paramref name="To"/> has no gameplay meaning,
/// and the unordered pair <c>{from, to}</c> is the swap's identity
/// (<c>MATCH3_RULES.md</c> §2.1.1 item 2).
///
/// It carries no Gem type, match result, Combo value, Turn, or <c>Sequence</c>:
/// every such value is server-determined (<c>MATCH3_RULES.md</c> §2.1.1 item 3,
/// <c>GAME_RULES.md</c> §18, <c>ADR-001</c>). The client sends the two cells it
/// is exchanging and nothing that could be authoritative.
///
/// The type deliberately does <b>not</b> validate its own values. A request
/// naming a cell outside <c>0..63</c> is a request that is rejected by
/// <see cref="SwapValidator"/> with <see cref="SwapRejectionReason.InvalidCellIndex"/>
/// (<c>MATCH3_RULES.md</c> §2.1.2 item 1) — it is not an exception, because
/// rejecting a malformed request is part of the documented validation contract,
/// not a programming error.
///
/// This is a Domain type and not a wire contract. How a Swap is framed on the
/// transport is owned by <c>SIGNALR_PROTOCOL.md</c> §2 and is unchanged by this
/// type.
/// </summary>
/// <param name="From">The §1.0 cell index the player is moving.</param>
/// <param name="To">The §1.0 cell index it is exchanged with.</param>
public readonly record struct SwapRequest(int From, int To);