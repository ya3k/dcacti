namespace GameServer.Domain.Match3;

/// <summary>
/// Special Gem creation planning (<c>MATCH3_RULES.md</c> §5.5.1–§5.5.4).
///
/// Given a detection pass's match set, this computes the pass's required Special
/// Gem positions <b>first</b> — before any activation of that step runs — which
/// is what reserves those cells (§5.5.4 item 2). It then resolves creation
/// collisions by first-in-§5.5.1-order-wins (§5.5.4 item 3).
///
/// Planning is a pure function of the match set, the swap origin, and the board:
/// it draws no RNG (§7.2 item 2), emits no events, and mutates nothing.
/// </summary>
public static class SpecialGemPlanner
{
    /// <summary>
    /// The result of planning one detection pass's creations: the claims in the
    /// §5.5.1 creation order, the winning claims after collision resolution, and
    /// the reserved cells.
    /// </summary>
    /// <param name="OrderedClaims">
    /// Every creation the pass's shapes require, in the §5.5.1 order — shapes by
    /// §3.2 positions, and within one L/T by the §5.5.1 item 4 sequence (Area
    /// Gem, then the horizontal arm's gem, then the vertical arm's gem). This is
    /// the order of creation and therefore of any deterministic reporting of
    /// created Special Gems (§5.5.1 items 3, 7).
    /// </param>
    /// <param name="CommittedClaims">
    /// The winning claims, in the same order, after collisions are resolved.
    /// These are the creations that are actually committed to the board.
    /// </param>
    /// <param name="ReservedCells">
    /// The cells the pass reserves for creation
    /// (<c>GAME_STATE.md</c> §3). An activation never clears a reserved cell,
    /// unless the reserved cell is the activated Special Gem's own cell
    /// (<c>MATCH3_RULES.md</c> §5.5.4 item 2).
    /// </param>
    public readonly record struct Plan(
        IReadOnlyList<SpecialGemClaim> OrderedClaims,
        IReadOnlyList<SpecialGemClaim> CommittedClaims,
        IReadOnlySet<int> ReservedCells);

    /// <summary>
    /// Plans a pass's Special Gem creations (<c>MATCH3_RULES.md</c> §5.5.1,
    /// §5.5.2, §5.5.3, §5.5.4).
    /// </summary>
    /// <param name="matchSet">
    /// The pass's match set, already in §3.2 order
    /// (<see cref="MatchDetector.Detect"/>).
    /// </param>
    /// <param name="swapOriginIndex">
    /// The cell that was one of the two cells named by the accepted Swap and whose
    /// Gem is a member of the matched line — the "swap origin"
    /// (<c>MATCH3_RULES.md</c> §5.5.3 item 1). <c>null</c> for a pass with no swap
    /// (a Cascade), in which case a Match 4 / Match 5 is placed at its line centre
    /// (§5.5.3 item 3).
    ///
    /// The origin is resolved <b>per shape</b>: of the two swapped cells, the
    /// origin is the one whose Gem is a member of that shape's cells
    /// (§5.5.3 item 2). If neither is a member, the shape has no origin and its
    /// gem goes to the line centre; if both are members, the shape is an L/T and
    /// the intersection is the position (§5.5.3 items 2, 4).
    /// </param>
    public static Plan PlanCreations(
        IReadOnlyList<MatchShape> matchSet,
        int? swapOriginIndex)
    {
        ArgumentNullException.ThrowIfNull(matchSet);

        var ordered = new List<SpecialGemClaim>();

        for (var shapeIndex = 0; shapeIndex < matchSet.Count; shapeIndex++)
        {
            var shape = matchSet[shapeIndex];

            if (shape.IsLt)
            {
                // §5.4 item 4 item 3: the L/T pattern is never suppressed — an
                // L/T always creates its Area Gem at the intersection, whatever
                // its arms classify as.
                ordered.Add(new SpecialGemClaim(
                    CellIndex: shape.IntersectionIndex!.Value,
                    SpecialGem: Match3.SpecialGem.Area(),
                    ShapeIndex: shapeIndex,
                    IntraShapeOrder: (int)CreationSource.LtPattern,
                    Source: CreationSource.LtPattern,
                    GemType: shape.GemType));

                // §5.4 item 4: the tier ladder is evaluated per arm as well, and
                // both arms can qualify. An arm is classified once, at its
                // highest qualifying tier (§5.4 item 4 item 1), and its gem goes
                // to that arm's OWN line centre (§5.5.3 item 7) — never the
                // intersection and never a swap origin, which is not reachable
                // for an L/T creation (§5.5.3 item 7 item 6).
                AddArmClaim(ordered, shape.HorizontalArm, shapeIndex, CreationSource.HorizontalArm, shape.GemType);
                AddArmClaim(ordered, shape.VerticalArm, shapeIndex, CreationSource.VerticalArm, shape.GemType);
                continue;
            }

            var primitive = (shape.HorizontalArm ?? shape.VerticalArm)!;

            // §5.5.2 ladder, evaluated per line: 5+ → Burst, exactly 4 → Line
            // Clear, exactly 3 → nothing. A run of 6+ is Match 5, not a new tier
            // (§5.3 item 3).
            if (!primitive.IsExactLengthFour && !primitive.IsLengthFiveOrMore)
            {
                continue;
            }

            var position = ResolveStraightShapePosition(shape, primitive, swapOriginIndex);

            ordered.Add(new SpecialGemClaim(
                CellIndex: position,
                SpecialGem: primitive.IsLengthFiveOrMore
                    ? Match3.SpecialGem.Burst()
                    : Match3.SpecialGem.LineClear(ToOrientation(primitive.Orientation)),
                ShapeIndex: shapeIndex,
                IntraShapeOrder: 0,
                Source: CreationSource.StraightLine,
                GemType: shape.GemType));
        }

        // §5.5.4 item 3: when one pass gives two or more Special Gems the same
        // cell, only the first in the order of §5.5.1 is created. The later claim
        // is discarded and has no further effect: it is not created, not stored,
        // not activated, and not reported as created.
        var committed = new List<SpecialGemClaim>(ordered.Count);
        var reserved = new HashSet<int>();
        var claimed = new HashSet<int>();

        foreach (var claim in ordered)
        {
            // §5.5.4 item 2 step 1: "compute the pass's required Special Gem positions
            // → reserved cells". Every required position is reserved, and a collision is
            // only ever same-cell (line below), so the reservation set is exactly the
            // set of creation positions.
            reserved.Add(claim.CellIndex);

            // §5.5.4 item 3: only the first claim on a cell is created; the later claim
            // is discarded and has no further effect.
            if (claimed.Add(claim.CellIndex))
            {
                committed.Add(claim);
            }
        }

        return new Plan(ordered, committed, reserved);
    }

    /// <summary>
    /// The position of a straight Match 4 / Match 5 shape's Special Gem
    /// (<c>MATCH3_RULES.md</c> §5.5.3 items 1–3).
    ///
    /// A shape produced by the accepted Swap is placed at the swap origin — that
    /// cell of the two the action named whose Gem is a member of the matched
    /// line (§5.5.3 items 1–2). A shape with no swap origin is a Cascade match and
    /// is placed at the line centre (§5.5.3 item 3).
    /// </summary>
    private static int ResolveStraightShapePosition(
        MatchShape shape,
        MatchPrimitive primitive,
        int? swapOriginIndex)
    {
        // §5.5.3 item 2: of the two cells named by the accepted Swap, the origin
        // is the one whose Gem is a member of the matched line. Exactly one is
        // the origin when the shape was created by the Swap; if neither is a
        // member, the shape is a Cascade match and item 3 applies.
        if (swapOriginIndex is { } origin && shape.Cells.Contains(origin))
        {
            return origin;
        }

        // §5.5.3 item 3: s + floor((L − 1) / 2) × step, with the lower-indexed of
        // the two middle cells for an even length.
        return primitive.LineCentreIndex;
    }

    /// <summary>
    /// Adds an L/T arm's Special Gem claim when that arm independently qualifies
    /// (<c>MATCH3_RULES.md</c> §5.4 item 4).
    ///
    /// An arm of length 4 creates a Line Clear Gem, an arm of length 5 or more a
    /// Burst Gem, and an arm of length 3 nothing. The arm is classified once, at
    /// its highest qualifying tier, so it never yields two Special Gems from
    /// itself (§5.4 item 4 item 1). Its position is that arm's own line centre,
    /// computed from the arm's own primitive and its <b>actual</b> length
    /// (§5.5.3 item 7).
    /// </summary>
    private static void AddArmClaim(
        List<SpecialGemClaim> ordered,
        MatchPrimitive? arm,
        int shapeIndex,
        CreationSource source,
        GemType gemType)
    {
        if (arm is null)
        {
            return;
        }

        if (!arm.IsExactLengthFour && !arm.IsLengthFiveOrMore)
        {
            // arm length 3 → no arm Special Gem (§5.4 item 4).
            return;
        }

        ordered.Add(new SpecialGemClaim(
            CellIndex: arm.LineCentreIndex,
            SpecialGem: arm.IsLengthFiveOrMore
                ? Match3.SpecialGem.Burst()
                : Match3.SpecialGem.LineClear(ToOrientation(arm.Orientation)),
            ShapeIndex: shapeIndex,
            IntraShapeOrder: (int)source,
            Source: source,
            GemType: gemType));
    }

    /// <summary>
    /// The Line Clear Gem orientation a creating primitive implies
    /// (<c>MATCH3_RULES.md</c> §5.2 item 3): the orientation is fixed at creation
    /// from the orientation of the straight line that created the Gem — a
    /// horizontal primitive yields a horizontal Line Clear, a vertical primitive
    /// a vertical one. This is the only orientation source.
    /// </summary>
    private static SpecialGemOrientation ToOrientation(MatchOrientation orientation) =>
        orientation == MatchOrientation.Horizontal
            ? SpecialGemOrientation.Horizontal
            : SpecialGemOrientation.Vertical;
}