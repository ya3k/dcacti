namespace GameServer.Domain.Match3;

/// <summary>
/// A <c>GemMatched</c> report — the domain-side description of one Gem consumed
/// (<c>GAME_EVENTS.md</c> §2, <c>MATCH3_RULES.md</c> §3.3 item 3).
///
/// It is emitted <b>once per cleared cell</b>, over the union of the cells a
/// sub-step removes, in ascending §1.0 cell index (<c>GAME_EVENTS.md</c> §1.3).
///
/// This is a Domain value, not a wire type: this task introduces no new protocol
/// message and does not define the event's serialization
/// (<c>SIGNALR_PROTOCOL.md</c> §8 item 7, <c>GAME_EVENTS.md</c> §3 item 1).
/// </summary>
/// <param name="CellIndex">
/// The cleared cell's §1.0 index — the event's "cell position".
/// </param>
/// <param name="GemType">
/// The cleared cell's Gem type. It is <b>always</b> one of the four §1.1 types,
/// including for a cell that held a Special Gem: a Special Gem adds metadata to a
/// cell's occupant and does not replace the occupant's Gem type, so this payload
/// is never a Special Gem type in place of a Gem type and is never absent for a
/// cleared cell (<c>GAME_EVENTS.md</c> §2 item 1, <c>GAME_STATE.md</c> §2.1.3
/// items 1–2).
/// </param>
/// <param name="ConsumedSpecialGem">
/// The Special Gem consumed at that cell, or <c>null</c> when the cell held an
/// ordinary Gem. It is read from the <b>pre-removal</b> board: the cell is
/// cleared and the Special Gem is consumed by the same act
/// (<c>GAME_EVENTS.md</c> §2 item 2, <c>GAME_STATE.md</c> §2.1.8 item 2). Its
/// absence is the statement that no Special Gem was consumed there
/// (<c>GAME_STATE.md</c> §2.1.7 item 3).
/// </param>
public readonly record struct GemMatchedEvent(
    int CellIndex,
    GemType GemType,
    SpecialGem? ConsumedSpecialGem);

/// <summary>
/// A Match detected in a pass, as a gameplay outcome
/// (<c>MATCH3_RULES.md</c> §3 item 5, <c>GAME_EVENTS.md</c> §2
/// <c>MatchCreated</c>).
///
/// One shape is one Match however many cells it spans and however many Special
/// Gems it creates: the Special Gems created are a consequence of the Match, never
/// additional Matches (<c>MATCH3_RULES.md</c> §5.4 item 4 item 4).
///
/// This value is Domain-side and is <b>not</b> a Match count: a Special Gem
/// activation is not a Match and is never added to a pass's match set
/// (<c>MATCH3_RULES.md</c> §5.5.5 item 8, §3.4 item 2). The authoritative
/// cumulative total is <c>BattleState.MatchCount</c> (<c>GAME_STATE.md</c> §2.2),
/// and this value is what that total is counted from: one
/// <see cref="MatchResolution"/> is exactly one Match, so the committed-Swap
/// accounting walks the passes and counts one increment per instance.
/// </summary>
/// <param name="Shape">The detected shape.</param>
/// <param name="CascadeDepth">
/// The pass's depth, where depth 1 is the pass run on the board produced by the
/// committed Swap and is <b>not</b> a Cascade, and depth ≥ 2 is a Cascade
/// (<c>MATCH3_RULES.md</c> §4.2 items 1–2).
/// </param>
/// <param name="CreatedSpecialGems">
/// The Special Gems this shape's creations committed, in the §5.5.1 order. A
/// claim discarded by collision resolution is not present: it is "not created,
/// not stored, not activated, and not reported as created"
/// (<c>MATCH3_RULES.md</c> §5.5.4 item 3).
/// </param>
public readonly record struct MatchResolution(
    MatchShape Shape,
    int CascadeDepth,
    IReadOnlyList<SpecialGemClaim> CreatedSpecialGems);