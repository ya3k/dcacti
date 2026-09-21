namespace GameServer.Domain.Match3;

using System.Diagnostics;

/// <summary>
/// Gravity (<c>MATCH3_RULES.md</c> §4.4).
///
/// Gravity is a server-side state transition, not an animation. It moves the Gems
/// that remain on the board after §4.1 step 1 downward within their own column:
///
/// <code>
/// for column = 0 → 7:                     (fixes the columns' new contents)
///     for row = 7 → 0:                    (bottom to top)
///         if cell(row, column) is empty:
///             pull the nearest non-empty cell above it down into it
/// </code>
///
/// It is a pure rearrangement: it draws no RNG (§4.4 item 6), produces no Match,
/// no Combo, and no events (§4.4 item 7), and it only relocates Gems — the board
/// is re-examined by the next detection pass (§4.1 step 5).
///
/// <b>Interaction with Special Gems (§4.4 item 8).</b> A Special Gem is a Gem
/// occupying a cell and falls under exactly these rules: it is pulled down like
/// any other Gem, keeps its place in the column's order, and never moves between
/// columns. Its effect is not re-triggered by falling. In the <c>Cells[64]</c>
/// representation a "Gem" is a whole cell entry, so gravity moves the
/// <c>GemType</c> <b>and</b> its <c>SpecialGem</c> metadata together as one unit
/// (<c>GAME_STATE.md</c> §2.1.5 item 1): a Line Clear Gem keeps its orientation
/// through gravity (<c>MATCH3_RULES.md</c> §5.2 item 3).
/// </summary>
public static class Gravity
{
    /// <summary>
    /// Applies gravity to the board, compacting each column's survivors into its
    /// lowest cells.
    /// </summary>
    /// <param name="board">
    /// The board after §4.1 step 2 — its matched and activation-cleared cells
    /// still hold their pre-removal entries; they are vacated here.
    /// </param>
    /// <param name="emptyCells">
    /// The cells §4.1 step 1 and the pass's activations emptied. An empty cell is
    /// "the absence of a Gem, not a fifth Gem": no marker Gem type, sentinel
    /// value, or negative index is introduced into the four-type value domain
    /// (<c>MATCH3_RULES.md</c> §4.4 item 5). It exists only inside one resolution's
    /// §4.1 steps 1–4 and never in the published board
    /// (<c>GAME_STATE.md</c> §2.1.5 item 4).
    /// </param>
    public static GravityResult Apply(BoardState board, IReadOnlySet<int> emptyCells)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(emptyCells);

        if (emptyCells.Count == 0)
        {
            return new GravityResult(board, []);
        }

        var entries = board.ToCellArray();

        // The cells that are empty AFTER gravity — the topmost cells of each column,
        // which are exactly the cells Spawn must fill (§4.5 item 1). Reported so the
        // next sub-step never has to reconstruct them, and so a caller cannot pass a
        // stale pre-gravity set to Spawn.
        var vacated = new List<int>(emptyCells.Count);

        // §4.4 item 1: columns are processed in ascending §1.0 column order
        // (0 → 7). Both this and the bottom-up pass below are fixed orders and are
        // not an implementation choice.
        for (var column = 0; column < BoardState.Columns; column++)
        {
            // The column's survivors, bottom to top, in their original order
            // (§4.4 item 3: "the survivors of the column, in their original order,
            // occupying the lowest cells"). Collected first so the compaction and the
            // vacated-cell marking are both driven by one traversal and can never
            // disagree about which cells were empty.
            var survivors = new List<Cell>(BoardState.Rows);

            // §4.4 item 1: within a column, cells are resolved from the bottom up
            // (row 7 → 0).
            for (var row = BoardState.Rows - 1; row >= 0; row--)
            {
                var readIndex = BoardState.ToIndex(row, column);

                if (emptyCells.Contains(readIndex))
                {
                    continue;
                }

                survivors.Add(entries[readIndex]);
            }

            // §4.4 item 3: a Gem never passes another Gem. Gravity is
            // order-preserving, and is not a sort, a shuffle, or a re-draw. The
            // survivors occupy the LOWEST cells, preserving their relative order.
            var survivorCount = survivors.Count;

            for (var i = 0; i < survivorCount; i++)
            {
                var writeRow = BoardState.Rows - 1 - i;
                var writeIndex = BoardState.ToIndex(writeRow, column);

                // The whole entry moves — GemType and SpecialGem together, as one unit
                // (GAME_STATE.md §2.1.5 item 1). A Special Gem that falls keeps its
                // orientation (§4.4 item 8, §5.2 item 3).
                entries[writeIndex] = survivors[i];
            }

            // The cells above the survivors are the column's new empty cells. A
            // BoardState always holds 64 entries, so they carry an ordinary
            // placeholder that Spawn overwrites in the same pass: no placeholder is
            // ever observable in a published board (§4.1 step 4, §4.5 item 1,
            // GAME_STATE.md §2.1.5 item 4).
            //
            // Only cells that are genuinely empty are marked and reported. A cell the
            // column's survivors already reach is never vacated — writing a
            // placeholder there would destroy a survivor, which is precisely the
            // defect this ordering avoids.
            //
            // A vacated cell keeps its old GemType as a PLACEHOLDER, because an empty
            // cell is "the absence of a Gem, not a fifth Gem" and no sentinel value may
            // enter the four-type domain (§4.4 item 5). That placeholder is not a board
            // value: GravityResult.VacatedCells names exactly these cells and Spawn
            // overwrites every one of them in the same pass. A caller that inspects
            // GravityResult.Board without spawning is reading a transient intermediate
            // state, which §4.1 never publishes.
            for (var row = BoardState.Rows - 1 - survivorCount; row >= 0; row--)
            {
                var index = BoardState.ToIndex(row, column);
                entries[index] = Cell.Ordinary(entries[index].GemType);
                vacated.Add(index);
            }
        }

        var result = BoardState.FromCellEntries(entries);

        // The placeholders above are only sound while Spawn overwrites exactly the
        // reported cells. Assert the pairing that makes them sound, so a future change
        // to either step fails loudly here instead of silently publishing a stale Gem
        // type (§4.5 item 1).
        Debug.Assert(
            vacated.Count == emptyCells.Count,
            "Gravity must vacate exactly as many cells as were empty before it ran: a "
            + "column's survivors compact into its lowest cells, so the vacated cells are "
            + "the same count as the holes it filled (MATCH3_RULES.md §4.4 item 3).");

        Debug.Assert(
            vacated.Distinct().Count() == vacated.Count,
            "A cell may be vacated only once (MATCH3_RULES.md §4.4 item 3).");

        return new GravityResult(result, vacated);
    }
}

/// <summary>
/// The outcome of one Gravity step (<c>MATCH3_RULES.md</c> §4.4).
/// </summary>
/// <param name="Board">
/// The board after gravity. It is <b>not yet a publishable board</b>: the cells named
/// by <paramref name="VacatedCells"/> are still empty, and carry a placeholder Gem
/// type only because a <c>BoardState</c> always holds 64 entries and no sentinel may
/// enter the four-type domain (<c>MATCH3_RULES.md</c> §4.4 item 5). Spawn overwrites
/// every one of them in the same pass (§4.1 step 4); a caller that reads this board
/// without spawning is observing transient intermediate state that §4.1 never
/// publishes.
/// </param>
/// <param name="VacatedCells">
/// The cells that are empty after gravity — the topmost cells of each column, and
/// exactly the cells Spawn must fill (§4.5 item 1). They are a subset of the cells
/// that were empty before gravity: a gem that fell has moved into a cell that was
/// empty, so that cell is no longer empty. Spawn must use <b>this</b> set, not the
/// pre-gravity one, or it would overwrite the gems gravity just moved —
/// including any Special Gem the same pass created (§4.1 item 2).
/// </param>
public readonly record struct GravityResult(
    BoardState Board,
    IReadOnlyList<int> VacatedCells);

/// <summary>
/// Spawn (<c>MATCH3_RULES.md</c> §4.5).
///
/// Spawn fills the cells that Gravity left empty at the top of each column:
///
/// <code>
/// for column = 0 → 7:
///     for each still-empty cell in that column, top to bottom:
///         draw one Gem and place it
/// </code>
///
/// It is a gameplay draw, not initial board generation. A cascade spawn is
/// <b>not</b> the §1.2.1 constrained fill: it draws independently from all four
/// §1.1 types with no candidate exclusion, because a spawn has no build-up order
/// and is allowed to form a match — which is precisely what makes cascades
/// possible (§1.2.1.5, §4 item 2, §4.5 item 3). <see cref="BoardGenerator"/>'s
/// constrained fill applies to initial board generation only and is not reused
/// here.
/// </summary>
public static class Spawn
{
    /// <summary>
    /// Fills the empty cells after Gravity with freshly drawn ordinary Gems,
    /// consuming exactly one RNG selection per spawned cell
    /// (<c>MATCH3_RULES.md</c> §4.5 item 4).
    ///
    /// The empty-cell set is required rather than inferred: Spawn must fill
    /// <b>exactly</b> the cells Gravity left empty (§4.5 item 1), and a caller that
    /// cannot name them cannot honour that rule. A "fill everything" overload would
    /// silently overwrite cells that still hold a Gem — including the Special Gems
    /// the same pass just created — so none is offered.
    /// </summary>
    /// <param name="board">The board after Gravity.</param>
    /// <param name="rng">
    /// The authoritative generator (<c>ADR-009</c>). This is the <b>only</b>
    /// gameplay operation that consumes randomness (<c>MATCH3_RULES.md</c> §7.2
    /// item 1): a spawn of <c>s</c> cells consumes exactly <c>s</c> selections —
    /// one per cell, in the §4.5 item 2 order, with no draw for the fill order, the
    /// position, the weights, the cell's emptiness, or anything else. A pass that
    /// spawns nothing draws nothing.
    /// </param>
    /// <param name="emptyCells">
    /// The cells still empty after Gravity. They are always the topmost cells of
    /// their column (§4.5 item 1).
    /// </param>
    public static SpawnResult Fill(BoardState board, Pcg32 rng, IReadOnlySet<int> emptyCells)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(emptyCells);

        var entries = board.ToCellArray();
        var created = new List<(int Index, Cell Cell)>();
        var spawned = 0;

        // §4.5 item 2: column order is ascending §1.0 column order (0 → 7), and
        // within a column the empty cells are filled top to bottom (row 0 →
        // row k−1). Both orders are fixed, so the draw order runs from the highest
        // empty cell downward.
        for (var column = 0; column < BoardState.Columns; column++)
        {
            for (var row = 0; row < BoardState.Rows; row++)
            {
                var index = BoardState.ToIndex(row, column);

                // §4.5 item 1: spawn fills exactly the cells that are empty after
                // Gravity, and no cell is filled twice. A column with k survivors
                // spawns exactly k Gems into its top k cells.
                if (!emptyCells.Contains(index))
                {
                    continue;
                }

                // §4.5 item 5 / §7.3: the selection is reduced onto the four-type
                // value domain in the fixed §1.1 order. This is the general
                // four-type case of the same reduction the constrained fill uses,
                // not a second mechanism. Exactly one selection per spawned cell.
                var selection = rng.NextBounded(GemTypes.Count);

                var gemType = GemTypes.All[(int)selection];

                // §4.5 item 7: a spawned Gem is an ordinary Gem of one of the four
                // types. Spawn NEVER creates a Special Gem, whatever the cell held
                // before — including a cell whose Special Gem was just consumed.
                var cell = Cell.Ordinary(gemType);

                entries[index] = cell;
                created.Add((index, cell));
                spawned++;
            }
        }

        return new SpawnResult(BoardState.FromCellEntries(entries), created, spawned);
    }
}

/// <summary>
/// The outcome of one Spawn step (<c>MATCH3_RULES.md</c> §4.5).
/// </summary>
/// <param name="Board">The board after the spawn, full again.</param>
/// <param name="CreatedCells">The cells spawned into, in the §4.5 item 2 order.</param>
/// <param name="SpawnedCount">
/// How many selections the step consumed — exactly the number of spawned cells
/// (§4.5 item 4).
/// </param>
public readonly record struct SpawnResult(
    BoardState Board,
    IReadOnlyList<(int Index, Cell Cell)> CreatedCells,
    int SpawnedCount);