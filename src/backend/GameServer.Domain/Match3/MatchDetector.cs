namespace GameServer.Domain.Match3;

/// <summary>
/// Gameplay Match Detection (<c>MATCH3_RULES.md</c> §3).
///
/// One call is <b>one detection pass</b> over one fixed board state: the board
/// immediately after the committed Swap, or the board immediately after a
/// Gravity + Spawn step (§3.1 item 1). All matches present in that state are
/// detected together and reported in the fixed §3.2 order, because Passives,
/// Relics, resource generation, and the event stream all depend on a stable
/// order (§3.1 item 2).
///
/// This is deliberately <b>not</b> <see cref="BoardGenerationValidator"/>. That
/// validator is bounded by §1.4.1/§1.4.2 to the two initialization-time
/// constraints, produces no match results, tiers, or shapes, and is not the
/// gameplay detector. This type implements §3 in full.
///
/// Detection is a pure function of the board: it draws no RNG
/// (<c>MATCH3_RULES.md</c> §7.2 item 2), emits no events, and mutates nothing.
/// </summary>
public static class MatchDetector
{
    /// <summary>
    /// Minimum run length that constitutes a Match
    /// (<c>MATCH3_RULES.md</c> §3 item 2: "Minimum match length is 3").
    /// </summary>
    public const int MinimumMatchLength = 3;

    /// <summary>
    /// Detects the complete match set of one board state and returns it in the
    /// §3.2 deterministic order (§3.1 item 2).
    ///
    /// Shapes are detected as maximal lines first, then grouped: a maximal
    /// horizontal run of 3+ and a maximal vertical run of 3+ of one type are the
    /// primitive shapes, and two primitives of the same type sharing exactly one
    /// cell are one L/T shape (§3.1 item 3, §3.3 item 2). Primitives that share
    /// no cell are independent matches.
    ///
    /// Match Detection compares the cell's <c>GemType</c> only. A Special Gem is
    /// metadata on that occupant and is never a colour of its own, so it never
    /// enters a match set as a fifth type (<c>GAME_STATE.md</c> §2.1.8 item 6).
    /// </summary>
    public static IReadOnlyList<MatchShape> Detect(BoardState board)
    {
        ArgumentNullException.ThrowIfNull(board);

        var horizontals = FindPrimitives(board, MatchOrientation.Horizontal);
        var verticals = FindPrimitives(board, MatchOrientation.Vertical);

        // Group primitives into shapes: an adjacent horizontal/vertical pair of
        // one type sharing exactly one cell is an L/T (§3.1 item 3). Because a
        // cell belongs to at most one horizontal and at most one vertical
        // primitive (§3.3 item 1), each primitive takes part in at most one L/T,
        // so the grouping is unambiguous and order-independent.
        var usedHorizontals = new bool[horizontals.Count];
        var usedVerticals = new bool[verticals.Count];
        var shapes = new List<MatchShape>();

        for (var h = 0; h < horizontals.Count; h++)
        {
            for (var v = 0; v < verticals.Count; v++)
            {
                if (usedHorizontals[h] || usedVerticals[v])
                {
                    continue;
                }

                if (horizontals[h].GemType != verticals[v].GemType)
                {
                    continue;
                }

                var shared = horizontals[h].CellIndexes()
                    .Intersect(verticals[v].CellIndexes())
                    .ToArray();

                // "sharing exactly one cell" (§5.4 item 1). Two same-type
                // perpendicular primitives cannot share more than one cell,
                // because that would require two cells with equal row and
                // column.
                if (shared.Length != 1)
                {
                    continue;
                }

                usedHorizontals[h] = true;
                usedVerticals[v] = true;
                shapes.Add(MatchShape.Lt(horizontals[h], verticals[v]));
            }
        }

        for (var h = 0; h < horizontals.Count; h++)
        {
            if (!usedHorizontals[h])
            {
                shapes.Add(MatchShape.Straight(horizontals[h]));
            }
        }

        for (var v = 0; v < verticals.Count; v++)
        {
            if (!usedVerticals[v])
            {
                shapes.Add(MatchShape.Straight(verticals[v]));
            }
        }

        // §3.2: orientation first (horizontal shapes before vertical shapes),
        // then ascending row-major index of the shape's first cell. The order
        // depends only on the §1.0 indexing — never on dictionary iteration,
        // insertion, allocation, or any unordered collection (§3.2 item 3).
        shapes.Sort(static (left, right) =>
        {
            var byOrientation = left.SortKey.Orientation.CompareTo(right.SortKey.Orientation);
            return byOrientation != 0
                ? byOrientation
                : left.SortKey.StartIndex.CompareTo(right.SortKey.StartIndex);
        });

        return shapes;
    }

    /// <summary>
    /// The union of a match set's cells — each cell once, ascending §1.0 index
    /// (<c>MATCH3_RULES.md</c> §3.3 item 3).
    ///
    /// Removal, resource generation, and <c>GemMatched</c> are all computed over
    /// this union, never per shape, so a cell named by more than one shape is
    /// processed once.
    /// </summary>
    public static IReadOnlyList<int> ClearedCellUnion(IReadOnlyList<MatchShape> matchSet)
    {
        ArgumentNullException.ThrowIfNull(matchSet);

        var union = new SortedSet<int>();

        foreach (var shape in matchSet)
        {
            foreach (var cell in shape.Cells)
            {
                union.Add(cell);
            }
        }

        return union.ToArray();
    }

    /// <summary>
    /// Finds every maximal run of 3+ in one orientation
    /// (<c>MATCH3_RULES.md</c> §3.1 item 3, §3.3 item 1).
    ///
    /// Scanning each line from its first cell and extending while the type
    /// matches yields maximal runs by construction: a cell belongs to at most one
    /// horizontal primitive and at most one vertical primitive, so no
    /// de-duplication is needed (§3.3 item 1).
    /// </summary>
    private static List<MatchPrimitive> FindPrimitives(BoardState board, MatchOrientation orientation)
    {
        var primitives = new List<MatchPrimitive>();
        var step = orientation == MatchOrientation.Horizontal ? 1 : BoardState.Width;

        // Vertical runs: scan each column top to bottom. Horizontal runs: each
        // row left to right. In both cases the scan visits cells in ascending
        // §1.0 index order, so a primitive's first cell is the run's lowest
        // index (§3.2 item 1).
        var lineCount = orientation == MatchOrientation.Horizontal ? BoardState.Rows : BoardState.Columns;

        for (var line = 0; line < lineCount; line++)
        {
            var lineStart = orientation == MatchOrientation.Horizontal
                ? BoardState.ToIndex(line, 0)
                : BoardState.ToIndex(0, line);

            var runStart = lineStart;
            var runType = board[lineStart];
            var runLength = 1;

            for (var offset = 1; offset < BoardState.Width; offset++)
            {
                var index = lineStart + (offset * step);
                var type = board[index];

                if (type == runType)
                {
                    runLength++;
                    continue;
                }

                if (runLength >= MinimumMatchLength)
                {
                    primitives.Add(MatchPrimitive.Create(orientation, runType, runStart, runLength));
                }

                runStart = index;
                runType = type;
                runLength = 1;
            }

            if (runLength >= MinimumMatchLength)
            {
                primitives.Add(MatchPrimitive.Create(orientation, runType, runStart, runLength));
            }
        }

        return primitives;
    }
}