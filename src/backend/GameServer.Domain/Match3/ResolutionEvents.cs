namespace GameServer.Domain.Match3;

/// <summary>
/// A <c>GemMatched</c> report — the domain-side description of one Gem consumed
/// (<c>GAME_EVENTS.md</c> §2, <c>MATCH3_RULES.md</c> §3.3 item 3).
///
/// It is emitted <b>once per cleared cell</b>, over the union of the cells a pass
/// removes, in ascending §1.0 cell index (<c>GAME_EVENTS.md</c> §1.3).
///
/// This is a Domain value, not a wire type: this task introduces no new protocol
/// message and does not define the event's serialization
/// (<c>SIGNALR_PROTOCOL.md</c> §8 item 7, <c>GAME_EVENTS.md</c> §3 item 1).
/// </summary>
/// <param name="CellIndex">
/// The cleared cell's §1.0 index — the event's "cell position".
/// </param>
/// <param name="GemType">
/// The cleared cell's Gem type. It is <b>always</b> one of the four §1.1 types and
/// is read from the board the pass was resolved against, in which the cell still
/// held the Gem that was consumed (<c>GAME_EVENTS.md</c> §2 item 2).
/// </param>
public readonly record struct GemMatchedEvent(
    int CellIndex,
    GemType GemType);

/// <summary>
/// A Match detected in a pass, as a gameplay outcome
/// (<c>MATCH3_RULES.md</c> §3 item 5, <c>GAME_EVENTS.md</c> §2
/// <c>MatchCreated</c>).
///
/// One shape is one Match however many cells it spans: all match tiers — Match 3,
/// Match 4, Match 5, and L/T — resolve as confirmed matches and no shape produces
/// any additional board object (<c>MATCH3_RULES.md</c> §1.A, §5.7).
///
/// This value is Domain-side and is <b>not</b> a Match count. The authoritative
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
public readonly record struct MatchResolution(
    MatchShape Shape,
    int CascadeDepth);
