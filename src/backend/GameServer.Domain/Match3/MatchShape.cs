namespace GameServer.Domain.Match3;

/// <summary>
/// One entry of a detection pass's match set — either a single straight
/// primitive or an L/T shape formed by two primitives sharing exactly one cell
/// (<c>MATCH3_RULES.md</c> §3.1 items 3–4, §5.4 item 1).
///
/// A shape is <b>one</b> Match however many cells it spans and however many
/// Special Gems it creates (<c>MATCH3_RULES.md</c> §3 item 5, §5.4 item 4
/// item 4). Its position in the pass's match set is the §3.2 order: horizontal
/// shapes before vertical shapes, then ascending start-cell index — and for an
/// L/T shape the position of its intersection cell (§3.2 item 2).
/// </summary>
public sealed class MatchShape
{
    private MatchShape(
        MatchPrimitive? horizontalArm,
        MatchPrimitive? verticalArm,
        int[] cells)
    {
        HorizontalArm = horizontalArm;
        VerticalArm = verticalArm;
        Cells = cells;
    }

    /// <summary>
    /// The shape's horizontal primitive, or <c>null</c> for a vertical-only
    /// straight shape.
    /// </summary>
    public MatchPrimitive? HorizontalArm { get; }

    /// <summary>
    /// The shape's vertical primitive, or <c>null</c> for a horizontal-only
    /// straight shape.
    /// </summary>
    public MatchPrimitive? VerticalArm { get; }

    /// <summary>
    /// The shape's Gem type. Both arms of an L/T share it, because a cell holds
    /// exactly one Gem type and the arms share a cell
    /// (<c>MATCH3_RULES.md</c> §3.3 item 2).
    /// </summary>
    public GemType GemType => (HorizontalArm ?? VerticalArm)!.GemType;

    /// <summary>
    /// True when this shape is an L/T — two perpendicular primitives sharing
    /// exactly one cell (<c>MATCH3_RULES.md</c> §5.4 item 1 item 2).
    /// </summary>
    public bool IsLt => HorizontalArm is not null && VerticalArm is not null;

    /// <summary>
    /// The shape's cells — the union of its arms' cells, each cell once
    /// (<c>MATCH3_RULES.md</c> §3.3 items 1–2). Ascending §1.0 index order.
    /// </summary>
    public IReadOnlyList<int> Cells { get; }

    /// <summary>
    /// The single cell shared by the two arms of an L/T — the position of the
    /// shape's Area Gem (<c>MATCH3_RULES.md</c> §5.4 item 2, §5.5.3 item 4).
    /// <c>null</c> for a straight shape.
    /// </summary>
    public int? IntersectionIndex { get; private init; }

    /// <summary>
    /// The shape's start cell — the lowest §1.0 index among its own cells for a
    /// straight shape, and the intersection cell for an L/T
    /// (<c>MATCH3_RULES.md</c> §3.2 items 1–2).
    /// </summary>
    public int StartIndex => IntersectionIndex ?? (HorizontalArm ?? VerticalArm)!.StartIndex;

    /// <summary>
    /// The shape's §3.2 sort key: orientation first (horizontal before vertical),
    /// then ascending start-cell index.
    ///
    /// An L/T occupies the position of its intersection cell (§3.2 item 2). §3.2
    /// defines only two orientation keys and an L/T is neither purely horizontal nor
    /// purely vertical, so the L/T case is an interpretation of a section that does
    /// not name it: this implementation places an L/T in the horizontal group at its
    /// intersection cell.
    ///
    /// The reading is chosen because it is the one that keeps §3.2's primary key
    /// meaningful — an L/T contains a horizontal primitive, so it belongs with the
    /// horizontal shapes rather than being interleaved among vertical ones by index —
    /// and because it is stable: the key depends only on the intersection cell and
    /// the §1.0 indexing.
    ///
    /// This is an implementation decision inside a documented ambiguity, not a rule
    /// this type owns. If the ordering of an L/T against straight shapes is ever
    /// given an explicit rule, that rule replaces this comment and this expression.
    /// </summary>
    public (int Orientation, int StartIndex) SortKey =>
        IsLt
            ? ((int)MatchOrientation.Horizontal, IntersectionIndex!.Value)
            : ((int)(HorizontalArm is not null ? MatchOrientation.Horizontal : MatchOrientation.Vertical),
               (HorizontalArm ?? VerticalArm)!.StartIndex);

    /// <summary>
    /// A straight shape (<c>MATCH3_RULES.md</c> §3 item 1).
    /// </summary>
    internal static MatchShape Straight(MatchPrimitive primitive) =>
        new(
            primitive.Orientation == MatchOrientation.Horizontal ? primitive : null,
            primitive.Orientation == MatchOrientation.Vertical ? primitive : null,
            primitive.CellIndexes().ToArray());

    /// <summary>
    /// An L/T shape from two perpendicular primitives sharing exactly one cell
    /// (<c>MATCH3_RULES.md</c> §5.4 item 1). The arms are held in their
    /// horizontal/vertical roles so the intra-shape creation order of §5.5.1
    /// item 4 can be applied without re-deriving them.
    /// </summary>
    internal static MatchShape Lt(MatchPrimitive horizontal, MatchPrimitive vertical)
    {
        var intersection = horizontal.CellIndexes().Intersect(vertical.CellIndexes()).Single();

        // The union of the arms' cells, each cell once, ascending §1.0 index
        // (§3.3 item 3: a shared cell is a single cell for removal, resource
        // generation, and GemMatched).
        var cells = horizontal.CellIndexes()
            .Concat(vertical.CellIndexes())
            .Distinct()
            .OrderBy(i => i)
            .ToArray();

        return new MatchShape(horizontal, vertical, cells)
        {
            IntersectionIndex = intersection,
        };
    }

    /// <summary>"L/T (5 cells)" or "Horizontal x4" — for test diagnostics only.</summary>
    public override string ToString() =>
        IsLt
            ? $"L/T at {IntersectionIndex} (H x{HorizontalArm!.Length}, V x{VerticalArm!.Length}, {Cells.Count} cells)"
            : $"{(HorizontalArm is not null ? "Horizontal" : "Vertical")} x{(HorizontalArm ?? VerticalArm)!.Length} "
              + $"at {StartIndex} ({GemType})";
}