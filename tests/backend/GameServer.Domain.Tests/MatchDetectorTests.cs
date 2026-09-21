using GameServer.Domain.Match3;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Match Detection tests (<c>MATCH3_RULES.md</c> §3, §3.1, §3.2, §3.3).
///
/// These verify the documented match set and its deterministic order — the order
/// Passives, Relics, resource generation, and the event stream depend on (§3.1
/// item 2), and the order Special Gem creation is derived from (§5.5.1 item 2).
///
/// <b>Fixture convention.</b> Boards are built from
/// <see cref="TestBoard.Background"/>, a pattern verified to contain no match
/// anywhere, and every scenario uses coordinates verified to produce <b>exactly</b>
/// the declared run and nothing else: no fixture inherits a neighbouring run from
/// the background, so a failure in these tests is a detection defect and never a
/// fixture artifact.
/// </summary>
public class MatchDetectorTests
{
    // Verified isolated slots on TestBoard.Background():
    //   horizontal 3   row 3 columns 1..3   -> 25,26,27  (ATK)
    //   horizontal 4   row 3 columns 1..4   -> 25..28    (DEF)
    //   horizontal 8   row 2 columns 0..7   -> 16..23    (ATK)
    //   vertical   3   column 1 rows 3..5   -> 25,33,41  (ATK)
    //   vertical   5   column 1 rows 1..5   -> 9,17,25,33,41 (ATK)
    //   L/T            row 1 cols 1..3 + col 1 rows 1..3, intersection 9 (ATK)

    [Fact]
    public void Detect_ShouldFindAHorizontalRunOfThree()
    {
        // Given a board whose only run is three ATK Gems in an unbroken horizontal
        // line — row 3, columns 1..3 (indices 25, 26, 27)
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk));

        // When match detection runs
        var matchSet = MatchDetector.Detect(board);

        // Then one Match is detected — §3 item 1, §3 item 5
        var shape = Assert.Single(matchSet);
        Assert.False(shape.IsLt);
        Assert.Equal(MatchOrientation.Horizontal, shape.HorizontalArm!.Orientation);
        Assert.Equal(3, shape.HorizontalArm.Length);
        Assert.Equal(25, shape.StartIndex);
        Assert.Equal(new[] { 25, 26, 27 }, shape.Cells);
    }

    [Fact]
    public void Detect_ShouldFindAVerticalRunOfThree()
    {
        // §3 item 1: an unbroken vertical line is a Match. Column 1, rows 3..5
        // (indices 25, 33, 41).
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (33, GemType.Atk), (41, GemType.Atk));

        var shape = Assert.Single(MatchDetector.Detect(board));

        Assert.False(shape.IsLt);
        Assert.Equal(MatchOrientation.Vertical, shape.VerticalArm!.Orientation);
        Assert.Equal(3, shape.VerticalArm.Length);
        Assert.Equal(25, shape.StartIndex);
        Assert.Equal(new[] { 25, 33, 41 }, shape.Cells);
    }

    [Fact]
    public void Detect_ShouldNotReportARunOfTwo()
    {
        // §3 item 2: the minimum match length is 3.
        var board = TestBoard.Background().WithGems((25, GemType.Atk), (26, GemType.Atk));

        Assert.Empty(MatchDetector.Detect(board));
    }

    [Fact]
    public void Detect_ShouldTreatARunOfSixOrMoreAsOneShape()
    {
        // §3 item 2: "a run of 6+ identical Gems in one line is still evaluated as
        // a single match shape". Whole row 2 (indices 16..23), length 8.
        var board = TestBoard.Background()
            .WithGems(Enumerable.Range(16, 8).Select(i => (i, GemType.Atk)).ToArray());

        var shape = Assert.Single(MatchDetector.Detect(board));

        // One Match, with its ACTUAL length 8 — not truncated to 5
        // (§5.5.3 item 7 item 5).
        Assert.Equal(8, shape.HorizontalArm!.Length);
        Assert.Equal(8, shape.Cells.Count);
    }

    [Fact]
    public void Detect_ShouldReportTheRunAsMaximal_NotAsOverlappingTriples()
    {
        // §3.3 item 1: primitives are maximal runs, so the match set has no
        // duplicate cells and no duplicate shapes by construction. Row 3,
        // columns 1..4 (indices 25..28).
        var board = TestBoard.Background()
            .WithGems((25, GemType.Def), (26, GemType.Def), (27, GemType.Def), (28, GemType.Def));

        var shape = Assert.Single(MatchDetector.Detect(board));

        Assert.Equal(4, shape.HorizontalArm!.Length);
        Assert.Equal(new[] { 25, 26, 27, 28 }, shape.Cells);
    }

    [Fact]
    public void Detect_ShouldGroupTwoPrimitivesSharingOneCellIntoOneLtShape()
    {
        // §3.1 item 3 / §3.3 item 2: two primitives of the same type sharing
        // exactly one cell are ONE L/T shape, not two overlapping matches.
        //
        //   horizontal arm: row 1, columns 1..3   (indices  9, 10, 11)
        //   vertical arm:   column 1, rows 1..3   (indices  9, 17, 25)
        //   intersection:   (1,1) = index 9, shared by both arms
        //
        // The union is 5 cells: the shared cell belongs to both primitives and is
        // counted once.
        var board = TestBoard.Background()
            .WithGems((9, GemType.Atk), (10, GemType.Atk), (11, GemType.Atk), (17, GemType.Atk), (25, GemType.Atk));

        var shape = Assert.Single(MatchDetector.Detect(board));

        Assert.True(shape.IsLt);
        Assert.Equal(9, shape.IntersectionIndex);
        Assert.Equal(3, shape.HorizontalArm!.Length);
        Assert.Equal(3, shape.VerticalArm!.Length);
        Assert.Equal(new[] { 9, 10, 11, 17, 25 }, shape.Cells);
    }

    [Fact]
    public void Detect_ShouldKeepPrimitivesThatShareNoCellAsIndependentMatches()
    {
        // §3.1 item 3: primitives that share no cell are independent matches.
        var board = TestBoard.Background()
            .WithGems(
                (25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk),
                (16, GemType.Hp), (17, GemType.Hp), (18, GemType.Hp));

        var matchSet = MatchDetector.Detect(board);

        Assert.Equal(2, matchSet.Count);
        Assert.All(matchSet, s => Assert.False(s.IsLt));
        Assert.Equal(new[] { 16, 25 }, matchSet.Select(s => s.StartIndex));
    }

    [Fact]
    public void Detect_ShouldNotGroupPerpendicularPrimitivesOfDifferentTypes()
    {
        // §3.3 item 2: two primitives of DIFFERENT Gem types cannot share a cell at
        // all, because a cell holds exactly one Gem type — so a crossing of two
        // types is two independent straight matches, never an L/T.
        //
        //   horizontal arm: row 1, columns 1..3   ATK  (indices 9, 10, 11)
        //   vertical arm:   column 1, rows 4..6   HP   (indices 33, 41, 49)
        // The two runs are in the same column but do not share a cell.
        var board = TestBoard.Background()
            .WithGems(
                (9, GemType.Atk), (10, GemType.Atk), (11, GemType.Atk),
                (33, GemType.Hp), (41, GemType.Hp), (49, GemType.Hp));

        var matchSet = MatchDetector.Detect(board);

        Assert.Equal(2, matchSet.Count);
        Assert.All(matchSet, s => Assert.False(s.IsLt));
    }

    // -----------------------------------------------------------------------
    // §3.2 — the deterministic match order
    // -----------------------------------------------------------------------

    [Fact]
    public void Detect_ShouldOrderHorizontalShapesBeforeVerticalShapes()
    {
        // §3.2 item 1: orientation first — horizontal shapes before vertical shapes.
        // The vertical shape starts at a LOWER index than the horizontal one, so a
        // start-index-first order would produce the opposite result.
        //
        //   vertical   run: column 0, rows 1..3  -> start 8,  cells 8, 16, 24
        //   horizontal run: row 6, columns 1..3 -> start 49, cells 49, 50, 51
        var board = TestBoard.Background()
            .WithGems(
                (8, GemType.Def), (16, GemType.Def), (24, GemType.Def),
                (49, GemType.Atk), (50, GemType.Atk), (51, GemType.Atk));

        var matchSet = MatchDetector.Detect(board);

        Assert.Equal(2, matchSet.Count);

        // The horizontal shape is first even though its start index (49) is higher
        // than the vertical shape's (8).
        Assert.Equal(MatchOrientation.Horizontal, matchSet[0].HorizontalArm!.Orientation);
        Assert.Equal(49, matchSet[0].StartIndex);
        Assert.Equal(MatchOrientation.Vertical, matchSet[1].VerticalArm!.Orientation);
        Assert.Equal(8, matchSet[1].StartIndex);
    }

    [Fact]
    public void Detect_ShouldOrderSameOrientationShapesByAscendingStartIndex()
    {
        // §3.2 item 2: within an orientation, ascending row-major index of the
        // shape's first cell.
        //
        //   row 2, columns 0..7  -> start 16
        //   row 3, columns 1..3  -> start 25
        //   row 5, columns 0..2  -> start 40
        var board = TestBoard.Background()
            .WithGems(
                (40, GemType.Atk), (41, GemType.Atk), (42, GemType.Atk),
                (25, GemType.Def), (26, GemType.Def), (27, GemType.Def));

        var starts = MatchDetector.Detect(board).Select(s => s.StartIndex).ToArray();

        Assert.Equal(new[] { 25, 40 }, starts);
    }

    [Fact]
    public void Detect_ShouldOrderAnLtShapeByItsIntersectionCell()
    {
        // §3.2 item 2: an L/T occupies the position of its intersection cell.
        // The L/T intersection is 9; a straight horizontal run starts at 40.
        var board = TestBoard.Background()
            .WithGems(
                // L/T: row 1 cols 1..3 plus col 1 rows 1..3; intersection (1,1) = 9.
                (9, GemType.Atk), (10, GemType.Atk), (11, GemType.Atk), (17, GemType.Atk), (25, GemType.Atk),
                // Straight run: row 5, columns 0..2.
                (40, GemType.Hp), (41, GemType.Hp), (42, GemType.Hp));

        var matchSet = MatchDetector.Detect(board);

        Assert.Equal(2, matchSet.Count);
        Assert.True(matchSet[0].IsLt);
        Assert.Equal(9, matchSet[0].StartIndex);
        Assert.False(matchSet[1].IsLt);
        Assert.Equal(40, matchSet[1].StartIndex);
    }

    [Fact]
    public void Detect_ShouldBeDeterministicAcrossRepeatedRuns()
    {
        // §3.2 item 3: the order depends only on the §1.0 indexing — never on
        // dictionary iteration order, insertion order, allocation order, or any
        // unordered collection. Repeated detection of one board must agree exactly.
        var board = TestBoard.Background()
            .WithGems(
                (9, GemType.Atk), (10, GemType.Atk), (11, GemType.Atk), (17, GemType.Atk), (25, GemType.Atk),
                (40, GemType.Hp), (41, GemType.Hp), (42, GemType.Hp));

        var first = MatchDetector.Detect(board)
            .Select(s => (s.StartIndex, s.IsLt, s.Cells.Count))
            .ToArray();

        Assert.Equal(2, first.Length);

        for (var run = 0; run < 25; run++)
        {
            var again = MatchDetector.Detect(board)
                .Select(s => (s.StartIndex, s.IsLt, s.Cells.Count))
                .ToArray();

            Assert.Equal(first, again);
        }
    }

    [Fact]
    public void Detect_ShouldIgnoreSpecialGemMetadata()
    {
        // GAME_STATE.md §2.1.8 item 6: a Special Gem never enters a match set as a
        // colour of its own — detection compares the cell's GemType, which is still
        // one of the four §1.1 types.
        var plain = TestBoard.Background().WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk));
        var decorated = plain.WithSpecial((26, SpecialGem.Burst()));

        var plainSet = MatchDetector.Detect(plain);
        var decoratedSet = MatchDetector.Detect(decorated);

        Assert.Equal(
            plainSet.Select(s => (s.StartIndex, s.Cells.Count)),
            decoratedSet.Select(s => (s.StartIndex, s.Cells.Count)));
    }

    // -----------------------------------------------------------------------
    // §3.3 — the cleared union
    // -----------------------------------------------------------------------

    [Fact]
    public void ClearedCellUnion_ShouldRemoveASharedCellOnce()
    {
        // §3.3 item 3: removal happens once per cell — the union of the cells in
        // the pass's shapes, ascending §1.0 index. The L/T's shared intersection (9)
        // is named by both arms and appears once.
        var board = TestBoard.Background()
            .WithGems((9, GemType.Atk), (10, GemType.Atk), (11, GemType.Atk), (17, GemType.Atk), (25, GemType.Atk));

        var union = MatchDetector.ClearedCellUnion(MatchDetector.Detect(board));

        Assert.Equal(new[] { 9, 10, 11, 17, 25 }, union);
        Assert.Equal(union.OrderBy(i => i), union);
        Assert.Equal(5, union.Distinct().Count());
    }

    [Fact]
    public void ClearedCellUnion_ShouldMergeOverlappingShapesIntoOneSet()
    {
        // §3.3 item 3: the union of the cells in the pass's shapes — a cell named by
        // more than one shape is a single cell for removal, resource generation, and
        // GemMatched.
        var board = TestBoard.Background()
            .WithGems(
                (25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk),
                (16, GemType.Hp), (17, GemType.Hp), (18, GemType.Hp));

        var union = MatchDetector.ClearedCellUnion(MatchDetector.Detect(board));

        Assert.Equal(new[] { 16, 17, 18, 25, 26, 27 }, union);
    }

    [Fact]
    public void MinimumMatchLength_ShouldBeThree()
    {
        // §3 item 2.
        Assert.Equal(3, MatchDetector.MinimumMatchLength);
    }
}