namespace GameServer.Domain.Match3;

/// <summary>
/// A maximal horizontal or vertical run of 3+ Gems of one type — the primitive
/// Match shape (<c>MATCH3_RULES.md</c> §3.1 item 3, §3 item 1).
///
/// "Maximal" is what makes the match set free of duplicate cells by
/// construction: each cell belongs to at most one horizontal primitive and at
/// most one vertical primitive (<c>MATCH3_RULES.md</c> §3.3 item 1), so the set
/// never needs a de-duplication pass.
///
/// The primitive carries its own geometry — orientation, start cell, length, and
/// end cell — because every Special Gem placement and tier rule is a function of
/// exactly those values: the line centre uses the start and the <b>actual</b>
/// length (<c>MATCH3_RULES.md</c> §5.5.3 item 3, §5.5.3 item 7 item 5), and the
/// orientation selects the Line Clear Gem's effect axis (§5.2 item 3).
/// </summary>
public sealed record MatchPrimitive
{
    private MatchPrimitive(
        MatchOrientation orientation,
        GemType gemType,
        int startIndex,
        int length,
        int step)
    {
        Orientation = orientation;
        GemType = gemType;
        StartIndex = startIndex;
        Length = length;
        Step = step;
    }

    /// <summary>
    /// Whether this primitive runs horizontally (a row) or vertically (a
    /// column) (<c>MATCH3_RULES.md</c> §3.1 item 3).
    /// </summary>
    public MatchOrientation Orientation { get; }

    /// <summary>The Gem type shared by every cell of the run.</summary>
    public GemType GemType { get; }

    /// <summary>
    /// The primitive's first cell — the lowest §1.0 index among its own cells:
    /// the leftmost cell for a horizontal run (same row, lowest column) and the
    /// topmost cell for a vertical run (same column, lowest row)
    /// (<c>MATCH3_RULES.md</c> §3.2 item 1).
    /// </summary>
    public int StartIndex { get; }

    /// <summary>
    /// The run's number of cells. This is the <b>actual</b> length — never
    /// truncated to a Match-5 length, which matters for Special Gem placement
    /// (<c>MATCH3_RULES.md</c> §5.5.3 item 7 item 5).
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// The index step between adjacent cells of the run: <c>1</c> for a
    /// horizontal run, <c>8</c> for a vertical run (<c>MATCH3_RULES.md</c>
    /// §5.5.3 item 3).
    /// </summary>
    public int Step { get; }

    /// <summary>The last cell of the run, inclusive.</summary>
    public int EndIndex => StartIndex + ((Length - 1) * Step);

    /// <summary>
    /// The run's cells in ascending §1.0 index order.
    /// </summary>
    public IEnumerable<int> CellIndexes()
    {
        for (var i = 0; i < Length; i++)
        {
            yield return StartIndex + (i * Step);
        }
    }

    /// <summary>
    /// The run's line centre — the cell at
    /// <c>s + floor((L − 1) / 2) × step</c> (<c>MATCH3_RULES.md</c> §5.5.3
    /// item 3).
    ///
    /// For an odd-length line this is the unique middle cell; for an even-length
    /// line it is the <b>lower-indexed</b> of the two middle cells. The tie-break
    /// is fixed by the owning section because "geometric centre" alone is
    /// ambiguous for an even length, and the choice must be identical in every
    /// implementation. This is the position used for a Cascade Match 4 / Match 5
    /// (§5.5.3 item 3) and for an L/T arm's own Special Gem (§5.5.3 item 7).
    /// </summary>
    public int LineCentreIndex => StartIndex + ((Length - 1) / 2 * Step);

    /// <summary>
    /// True when this run is length 4 — the Match 4 tier, which creates a Line
    /// Clear Gem (<c>MATCH3_RULES.md</c> §5.2 item 1, §5.5.2).
    /// </summary>
    public bool IsExactLengthFour => Length == 4;

    /// <summary>
    /// True when this run is length 5 or more — the Match 5 tier, which creates a
    /// Burst Gem. A run of 6 or more is Match 5 and there is no separate tier
    /// (<c>MATCH3_RULES.md</c> §5.3 items 1, 3).
    /// </summary>
    public bool IsLengthFiveOrMore => Length >= 5;

    /// <summary>
    /// True when the run contains the given cell
    /// (<c>MATCH3_RULES.md</c> §5.5.3 item 2).
    /// </summary>
    public bool Contains(int index)
    {
        if (index < StartIndex || index > EndIndex)
        {
            return false;
        }

        return (index - StartIndex) % Step == 0;
    }

    /// <summary>
    /// Builds a primitive from its orientation and start cell by measuring the
    /// maximal run of <paramref name="gemType"/> that starts there. The step is
    /// derived from the orientation, so the two can never disagree.
    /// </summary>
    internal static MatchPrimitive Create(
        MatchOrientation orientation,
        GemType gemType,
        int startIndex,
        int length) =>
        new(orientation, gemType, startIndex, length, orientation == MatchOrientation.Horizontal ? 1 : BoardState.Width);
}

/// <summary>
/// The orientation of a Match primitive (<c>MATCH3_RULES.md</c> §3.1 item 3).
///
/// It is also the primary key of the deterministic match order — horizontal
/// shapes before vertical shapes (<c>MATCH3_RULES.md</c> §3.2 item 1) — and the
/// source of a Line Clear Gem's effect axis (§5.2 item 3).
/// </summary>
public enum MatchOrientation
{
    /// <summary>A row run. Ordered first (<c>MATCH3_RULES.md</c> §3.2 item 1).</summary>
    Horizontal = 0,

    /// <summary>A column run. Ordered after every horizontal shape.</summary>
    Vertical = 1,
}