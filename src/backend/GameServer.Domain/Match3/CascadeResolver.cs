namespace GameServer.Domain.Match3;

/// <summary>
/// The cascade loop (<c>MATCH3_RULES.md</c> §4, §4.2, §4.3).
///
/// <code>
/// Detection pass 1   (board after the committed Swap)     → depth 1
///         ↓  remove / activate+create / gravity / spawn
/// Detection pass 2   (board after pass 1's Gravity+Spawn)  → depth 2, a Cascade
///         ↓  remove / activate+create / gravity / spawn
/// Detection pass 3   → depth 3, a Cascade
///         ↓
///       … until a pass detects no Match
/// </code>
///
/// Depth 1 is the first pass — the one run on the board produced by the committed
/// Swap — and is <b>not</b> a Cascade. Depth ≥ 2 is a Cascade, and the Cascade's
/// depth index within the Swap is <c>d − 1</c> (§4.2 items 1–2). Passes are
/// strictly sequential: pass <c>d + 1</c> runs only after pass <c>d</c>'s Gravity
/// and Spawn have completed, and two passes never run against the same board state
/// (§4.2 item 3).
///
/// There is <b>no hard cap on cascade depth</b> in MVP: the loop ends naturally
/// when a detection pass produces no matches on the fully resolved board (§4 item
/// 5, §4.3 item 1). This type therefore contains no cascade limit, no iteration
/// budget, and no "best effort" truncation — adding one would contradict
/// <c>MATCH3_RULES.md</c> §4 item 5.
/// </summary>
public static class CascadeResolver
{
    /// <summary>
    /// The outcome of resolving a board until it is stable
    /// (<c>MATCH3_RULES.md</c> §4.3).
    /// </summary>
    /// <param name="Board">
    /// The final board. When the loop ends it holds exactly one Gem per cell and is
    /// the board the resolution publishes (§4.3 item 3, §1).
    /// </param>
    /// <param name="RngState">
    /// The RNG state after every pass's Spawn step
    /// (<c>GAME_STATE.md</c> §2.6.2 item 2).
    /// </param>
    /// <param name="Passes">
    /// Every pass that detected a Match, in order. The terminating pass is not
    /// present: it detected no Match, so it emits nothing at all
    /// (<c>MATCH3_RULES.md</c> §4.3 item 4, <c>GAME_EVENTS.md</c> §1.1 item 6).
    /// </param>
    /// <param name="CascadeDepth">
    /// How many Cascade passes ran — the number of passes after the first (§4.2).
    /// </param>
    /// <param name="TotalMatches">
    /// The number of Matches detected across every pass. Each shape counts once
    /// however many cells it spans and however many Special Gems it creates
    /// (§3 item 5, §5.4 item 4 item 4). Gems cleared by a Special Gem activation
    /// are not Matches and are not counted here (§5.5.5 item 8).
    /// </param>
    public readonly record struct CascadeResult(
        BoardState Board,
        RngState RngState,
        IReadOnlyList<PassResult> Passes,
        int CascadeDepth,
        int TotalMatches);

    /// <summary>
    /// Resolves a board until no Match remains, starting from the board a committed
    /// Swap produced (<c>MATCH3_RULES.md</c> §4).
    /// </summary>
    /// <param name="board">
    /// The board of the first detection pass — for a committed Swap, the board after
    /// the exchange (§2.1.6 steps 4, 8).
    /// </param>
    /// <param name="rng">
    /// The authoritative generator. Spawn is the only operation that advances it
    /// (§4.5 item 4, §7.2 item 1).
    /// </param>
    /// <param name="swapOriginIndex">
    /// The cell the committed Swap moved a Gem into, when that cell's Gem is a
    /// member of a matched line — the "swap origin" of
    /// <c>MATCH3_RULES.md</c> §5.5.3 item 1. It applies to the <b>first pass
    /// only</b>: every later pass is a Cascade and has no swap origin, so its Match
    /// 4 / Match 5 Special Gems are placed at their line centre (§5.5.3 item 3).
    /// </param>
    public static CascadeResult Resolve(BoardState board, Pcg32 rng, int? swapOriginIndex)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(rng);

        var currentBoard = board;
        var passes = new List<PassResult>();
        var depth = 0;
        var totalMatches = 0;

        // §4.1 step 5 / §4.2 item 3: re-detect on the completed board; if a match
        // exists, this is a Cascade and the loop runs again. The loop's only exit is
        // a pass that finds no match (§4.3 item 1) — there is no other bound.
        while (true)
        {
            var matchSet = MatchDetector.Detect(currentBoard);
            depth++;

            if (matchSet.Count == 0)
            {
                // §4.3 item 1: the loop ends when a detection pass produces no
                // matches on the fully resolved board. The terminating pass emits
                // no Match events because it detected no Match (§4.3 item 4).
                break;
            }

            // §5.5.3 item 3: only the Swap's first pass has a swap origin. A Cascade
            // match has none, so its gem is placed at the line centre.
            var pass = BoardResolver.ResolvePass(
                currentBoard,
                matchSet,
                rng,
                depth == 1 ? swapOriginIndex : null,
                depth);

            passes.Add(pass);
            totalMatches += matchSet.Count;
            currentBoard = pass.Board;
        }

        return new CascadeResult(
            currentBoard,
            rng.CurrentState,
            passes,
            // §4.2 item 2: depth ≥ 2 is a Cascade, and the Cascade's depth index within
            // the Swap is d − 1. The count reported here is therefore the number of
            // passes that ran as Cascades — the passes after the first — because the
            // terminating pass detects no Match and is not a pass that ran (§4.3
            // item 1). With n recorded passes, passes 2..n are Cascades and the deepest
            // Cascade index is n − 1.
            CascadeDepth: Math.Max(0, passes.Count - 1),
            TotalMatches: totalMatches);
    }
}