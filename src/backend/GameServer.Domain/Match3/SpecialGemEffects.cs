namespace GameServer.Domain.Match3;

/// <summary>
/// Special Gem effect geometry (<c>MATCH3_RULES.md</c> §5.2 item 2, §5.3 item 2,
/// §5.4 item 3) with the board clipping of §5.8.1.
///
/// Every affected set is computed as a set of <c>(row, column)</c> pairs and then
/// <b>clipped to the cells that exist</b>: a cell outside the board is dropped,
/// never wrapped to the other side, never folded onto another row or column,
/// never clamped onto the nearest existing cell, and never counted as an affected
/// cell (§5.8.1 items 1–3).
///
/// The three documented counts — a Burst Gem clearing 9 cells in the interior, 6
/// on an edge, 4 at a corner; an Area Gem clearing 5, 4, 3 — are
/// <b>consequences</b> of this clipping, not a second table an implementation
/// special-cases (§5.3 item 2, §5.4 item 3, §5.8.1 item 5). This type therefore
/// computes one offset set per type and clips it; it contains no
/// corner/edge/interior branch.
///
/// All three geometries include the activated Special Gem's own cell, so
/// consuming a Special Gem always clears the cell it occupied and never leaves it
/// behind (§5.5.5 item 4, §5.8.1 item 4).
///
/// Geometry is a pure function of a cell index and the board size: no RNG, no
/// search, no path-finding (§5.9.2 items 1, 5).
/// </summary>
public static class SpecialGemEffects
{
    /// <summary>
    /// The cells a Special Gem's activation clears — its affected set, clipped to
    /// the board, in ascending §1.0 index order.
    ///
    /// The returned set is the <b>clipped affected set</b>. It is not yet reduced
    /// by the pass's reserved creation cells; that reduction is applied by the
    /// activation step, because it depends on the pass
    /// (<c>MATCH3_RULES.md</c> §5.5.4 item 2, §5.8.2).
    /// </summary>
    /// <param name="specialGem">The activating Special Gem.</param>
    /// <param name="cellIndex">The cell it occupies — the centre of its effect.</param>
    public static IReadOnlyList<int> AffectedCells(SpecialGem specialGem, int cellIndex)
    {
        var row = BoardState.ToRow(cellIndex);
        var column = BoardState.ToColumn(cellIndex);

        var affected = new SortedSet<int>();

        switch (specialGem.Type)
        {
            case SpecialGemType.LineClear:
                // §5.2 item 2: the whole line through its own cell, including its
                // own cell. Exactly one of the two lines is cleared, never both:
                // the orientation selects which. A full row and a full column are
                // each exactly 8 cells, so this effect never overflows the board.
                AddLine(affected, row, column, specialGem.Orientation, cellIndex);
                break;

            case SpecialGemType.Burst:
                // §5.3 item 2: the 3x3 square centred on its own cell — the eight
                // orthogonally and diagonally adjacent cells plus its own cell. The
                // shape is a square, not a plus and not a diamond.
                AddOffsets(affected, row, column, BurstOffsets);
                break;

            case SpecialGemType.Area:
                // §5.4 item 3: the plus/cross of its own cell — itself plus the
                // four orthogonally adjacent cells.
                AddOffsets(affected, row, column, AreaOffsets);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(specialGem),
                    specialGem.Type,
                    "Not one of the three documented Special Gem types (GAME_STATE.md §2.1.4 item 1).");
        }

        return affected.ToArray();
    }

    /// <summary>
    /// The 3×3 square's offsets — <c>dr</c> and <c>dc</c> each in
    /// <c>{ −1, 0, +1 }</c> (<c>MATCH3_RULES.md</c> §5.3 item 2).
    /// </summary>
    private static readonly (int Row, int Column)[] BurstOffsets =
    [
        (-1, -1), (-1, 0), (-1, 1),
        (0, -1), (0, 0), (0, 1),
        (1, -1), (1, 0), (1, 1),
    ];

    /// <summary>
    /// The plus/cross offsets — the centre plus the four orthogonal neighbours
    /// (<c>MATCH3_RULES.md</c> §5.4 item 3).
    /// </summary>
    private static readonly (int Row, int Column)[] AreaOffsets =
    [
        (0, 0), (-1, 0), (1, 0), (0, -1), (0, 1),
    ];

    /// <summary>
    /// Adds the whole row or column through the Gem's own cell
    /// (<c>MATCH3_RULES.md</c> §5.2 item 2).
    ///
    /// The effect is the <b>entire</b> line, in both directions from the Gem,
    /// including the Gem's own cell — not the "remainder of the line" in one
    /// direction. There is no direction-dependent variant and no partially
    /// cleared line.
    /// </summary>
    private static void AddLine(
        SortedSet<int> affected,
        int row,
        int column,
        SpecialGemOrientation? orientation,
        int cellIndex)
    {
        switch (orientation)
        {
            case SpecialGemOrientation.Horizontal:
                for (var c = 0; c < BoardState.Columns; c++)
                {
                    affected.Add(BoardState.ToIndex(row, c));
                }

                return;

            case SpecialGemOrientation.Vertical:
                for (var r = 0; r < BoardState.Rows; r++)
                {
                    affected.Add(BoardState.ToIndex(r, column));
                }

                return;

            default:
                // GAME_STATE.md §2.1.4 item 2: orientation is present if and only
                // if the type is LineClear. A Line Clear Gem without one is a
                // contract violation, not a case any rule defines — fail loudly
                // rather than guess an axis.
                throw new InvalidOperationException(
                    $"A Line Clear Gem at cell {cellIndex} carries no orientation, but GAME_STATE.md "
                    + "§2.1.4 item 2 requires one for exactly this type (MATCH3_RULES.md §5.2 item 2). "
                    + "No rule defines a default axis, so this is a contract/implementation conflict.");
        }
    }

    /// <summary>
    /// Adds each offset, dropping any cell outside the board
    /// (<c>MATCH3_RULES.md</c> §5.8.1 item 1).
    /// </summary>
    private static void AddOffsets(
        SortedSet<int> affected,
        int row,
        int column,
        (int Row, int Column)[] offsets)
    {
        foreach (var (rowOffset, columnOffset) in offsets)
        {
            var targetRow = row + rowOffset;
            var targetColumn = column + columnOffset;

            // Clipping by discard: no wrap, no fold, no clamp, and no phantom cell
            // outside 0..63 (§5.8.1 items 1, 3).
            if (!BoardState.IsOnBoard(targetRow, targetColumn))
            {
                continue;
            }

            affected.Add(BoardState.ToIndex(targetRow, targetColumn));
        }
    }
}