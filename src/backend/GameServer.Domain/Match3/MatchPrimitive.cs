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
/// end cell — because shape ordering is a function of exactly those values, and
/// because the shape's cells must be enumerable in ascending §1.0 index order
/// (<c>MATCH3_RULES.md</c> §3.2). No tier value is derived from the length: all
/// match tiers resolve as the same confirmed match (<c>§1.A</c>, §5.7).
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
    /// The run's number of cells — the <b>actual</b> length, never truncated
    /// (<c>MATCH3_RULES.md</c> §3.2).
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// The index step between adjacent cells of the run: <c>1</c> for a
    /// horizontal run, <c>8</c> for a vertical run.
    /// </summary>
    public int Step { get; }

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
/// shapes before vertical shapes (<c>MATCH3_RULES.md</c> §3.2 item 1).
/// </summary>
public enum MatchOrientation
{
    /// <summary>A row run. Ordered first (<c>MATCH3_RULES.md</c> §3.2 item 1).</summary>
    Horizontal = 0,

    /// <summary>A column run. Ordered after every horizontal shape.</summary>
    Vertical = 1,
}
