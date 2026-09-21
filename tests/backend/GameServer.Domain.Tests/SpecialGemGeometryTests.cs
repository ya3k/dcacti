using GameServer.Domain.Match3;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Special Gem activation geometry tests (<c>MATCH3_RULES.md</c> §5.2 item 2,
/// §5.3 item 2, §5.4 item 3, §5.8.1).
///
/// These verify the affected set each type produces and the clipping rule. The
/// documented cell counts — 8 for a line, 9/6/4 for a Burst, 5/4/3 for an Area —
/// are <b>consequences</b> of clipping one offset set, not special cases
/// (§5.8.1 item 5), so they are asserted as outcomes rather than as branches.
/// </summary>
public class SpecialGemGeometryTests
{
    /// <summary>The affected set of a Special Gem at a cell, as sorted indices.</summary>
    private static int[] Affected(SpecialGem gem, int cellIndex) =>
        SpecialGemEffects.AffectedCells(gem, cellIndex).ToArray();

    // -----------------------------------------------------------------------
    // §5.2 item 2 — Line Clear: the whole row or column
    // -----------------------------------------------------------------------

    [Fact]
    public void LineClearHorizontal_ShouldClearTheWholeRowIncludingItsOwnCell()
    {
        // §5.2 item 2: a horizontal Line Clear Gem clears every cell of its own row
        // — all 8 cells — INCLUDING the cell it occupies, in both directions.
        var row = 4;
        var cellIndex = BoardState.ToIndex(row, 3);

        var affected = Affected(SpecialGem.LineClearHorizontal(), cellIndex);

        Assert.Equal(8, affected.Length);
        Assert.Equal(Enumerable.Range(row * 8, 8), affected);
        Assert.Contains(cellIndex, affected);
    }

    [Fact]
    public void LineClearVertical_ShouldClearTheWholeColumnIncludingItsOwnCell()
    {
        // §5.2 item 2 for the vertical orientation.
        var column = 5;
        var cellIndex = BoardState.ToIndex(2, column);

        var affected = Affected(SpecialGem.LineClearVertical(), cellIndex);

        Assert.Equal(8, affected.Length);
        Assert.Equal(Enumerable.Range(0, 8).Select(r => BoardState.ToIndex(r, column)), affected);
        Assert.Contains(cellIndex, affected);
    }

    [Fact]
    public void LineClear_ShouldClearExactlyOneLineNeverBoth()
    {
        // §5.2 item 2: "Exactly one of the two lines is cleared, never both: the
        // Gem's orientation selects which."
        var cellIndex = BoardState.ToIndex(3, 3);

        var horizontal = Affected(SpecialGem.LineClearHorizontal(), cellIndex);
        var vertical = Affected(SpecialGem.LineClearVertical(), cellIndex);

        Assert.Equal(8, horizontal.Length);
        Assert.Equal(8, vertical.Length);

        // They share exactly the gem's own cell.
        Assert.Single(horizontal.Intersect(vertical));
        Assert.Equal(cellIndex, horizontal.Intersect(vertical).Single());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 7)]
    [InlineData(7, 0)]
    [InlineData(7, 7)]
    [InlineData(3, 4)]
    public void LineClear_ShouldAlwaysClearExactlyEightCells(int row, int column)
    {
        // §5.2 item 2 / §5.8.1 item 5: a full row and a full column are each exactly
        // 8 cells on this board, so a Line Clear Gem never overflows the board and
        // never needs wrapping or folding — at a corner, at an edge, or in the
        // interior.
        var cellIndex = BoardState.ToIndex(row, column);

        Assert.Equal(8, Affected(SpecialGem.LineClearHorizontal(), cellIndex).Length);
        Assert.Equal(8, Affected(SpecialGem.LineClearVertical(), cellIndex).Length);
    }

    // -----------------------------------------------------------------------
    // §5.3 item 2 — Burst: the 3x3 square centred on its own cell
    // -----------------------------------------------------------------------

    [Fact]
    public void Burst_ShouldClearThe3x3SquareIncludingDiagonals()
    {
        // §5.3 item 2: a Burst Gem clears every cell in the 3x3 square of which its
        // own cell is the centre — the eight orthogonally AND diagonally adjacent
        // cells plus its own cell. The shape is a square, not a plus and not a
        // diamond.
        var cellIndex = BoardState.ToIndex(3, 3);

        var affected = Affected(SpecialGem.Burst(), cellIndex);

        Assert.Equal(9, affected.Length);
        Assert.Contains(cellIndex, affected);

        // Both a diagonal and an orthogonal neighbour are present.
        Assert.Contains(BoardState.ToIndex(2, 2), affected);
        Assert.Contains(BoardState.ToIndex(2, 3), affected);
        Assert.Contains(BoardState.ToIndex(4, 4), affected);
    }

    [Fact]
    public void BurstAtCorner_ShouldClearFourCells()
    {
        // §5.3 item 2 / §5.8.1 item 5: a Burst Gem at a corner clears 4 cells —
        // { (0,0), (0,1), (1,0), (1,1) } at the top-left.
        var affected = Affected(SpecialGem.Burst(), BoardState.ToIndex(0, 0));

        Assert.Equal(4, affected.Length);
        Assert.Equal(
            new[]
            {
                BoardState.ToIndex(0, 0), BoardState.ToIndex(0, 1),
                BoardState.ToIndex(1, 0), BoardState.ToIndex(1, 1),
            },
            affected);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(7, 3)]
    [InlineData(3, 0)]
    [InlineData(3, 7)]
    public void BurstOnEdge_ShouldClearSixCells(int row, int column)
    {
        // §5.3 item 2: a Burst Gem on an edge (non-corner) clears 6 cells.
        Assert.Equal(6, Affected(SpecialGem.Burst(), BoardState.ToIndex(row, column)).Length);
    }

    [Fact]
    public void BurstInInterior_ShouldClearNineCells()
    {
        // §5.3 item 2: a Burst Gem in the interior clears 9 cells.
        Assert.Equal(9, Affected(SpecialGem.Burst(), BoardState.ToIndex(3, 3)).Length);
        Assert.Equal(9, Affected(SpecialGem.Burst(), BoardState.ToIndex(4, 2)).Length);
    }

    // -----------------------------------------------------------------------
    // §5.4 item 3 — Area: the plus/cross
    // -----------------------------------------------------------------------

    [Fact]
    public void Area_ShouldClearItsOwnCellAndTheFourOrthogonalNeighbours()
    {
        // §5.4 item 3: an Area Gem clears its own cell plus the four orthogonally
        // adjacent cells — the plus/cross of its position. It does NOT include the
        // diagonals.
        var cellIndex = BoardState.ToIndex(3, 3);

        var affected = Affected(SpecialGem.Area(), cellIndex);

        Assert.Equal(5, affected.Length);
        Assert.Equal(
            new[]
            {
                BoardState.ToIndex(2, 3), BoardState.ToIndex(3, 2), BoardState.ToIndex(3, 3),
                BoardState.ToIndex(3, 4), BoardState.ToIndex(4, 3),
            },
            affected);

        // No diagonal is present.
        Assert.DoesNotContain(BoardState.ToIndex(2, 2), affected);
        Assert.DoesNotContain(BoardState.ToIndex(4, 4), affected);
    }

    [Fact]
    public void AreaAtCorner_ShouldClearThreeCells()
    {
        // §5.4 item 3 / §5.8.1 item 5: an Area Gem at a corner clears 3 cells —
        // { (0,0), (0,1), (1,0) } at the top-left.
        var affected = Affected(SpecialGem.Area(), BoardState.ToIndex(0, 0));

        Assert.Equal(3, affected.Length);
        Assert.Equal(
            new[]
            {
                BoardState.ToIndex(0, 0), BoardState.ToIndex(0, 1), BoardState.ToIndex(1, 0),
            },
            affected);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(7, 3)]
    [InlineData(3, 0)]
    [InlineData(3, 7)]
    public void AreaOnEdge_ShouldClearFourCells(int row, int column)
    {
        // §5.4 item 3: an Area Gem on an edge (non-corner) clears 4 cells.
        Assert.Equal(4, Affected(SpecialGem.Area(), BoardState.ToIndex(row, column)).Length);
    }

    [Fact]
    public void AreaInInterior_ShouldClearFiveCells()
    {
        // §5.4 item 3: an Area Gem in the interior clears 5 cells.
        Assert.Equal(5, Affected(SpecialGem.Area(), BoardState.ToIndex(3, 3)).Length);
    }

    // -----------------------------------------------------------------------
    // §5.8.1 — clipping
    // -----------------------------------------------------------------------

    [Fact]
    public void Clipping_ShouldDiscardOutOfBoardCells_NeverWrapOrClamp()
    {
        // §5.8.1 item 1: a cell outside the board is DROPPED. It is never wrapped to
        // the other side, never folded onto another row or column, never clamped onto
        // the nearest existing cell.
        //
        // A Burst Gem at (0,0) would reach (-1,-1), (-1,0), (0,-1) — none of which
        // exists. Crucially, no cell from the OPPOSITE edge (row 7 / column 7)
        // appears, which is what wrapping would produce.
        var affected = Affected(SpecialGem.Burst(), BoardState.ToIndex(0, 0));

        Assert.DoesNotContain(BoardState.ToIndex(7, 7), affected);
        Assert.DoesNotContain(BoardState.ToIndex(0, 7), affected);
        Assert.DoesNotContain(BoardState.ToIndex(7, 0), affected);

        // And every affected cell is on the board.
        Assert.All(affected, i => Assert.InRange(i, 0, BoardState.CellCount - 1));
    }

    [Fact]
    public void Clipping_ShouldNeverCrossARowEdge()
    {
        // §5.8.1 item 1: "index 7 and index 8 are not neighbours (§2.1.3 item 1), so
        // no effect may cross a row edge."
        //
        // An Area Gem at (0,7) — index 7 — has a right neighbour that does not exist.
        // Index 8 (row 1, column 0) must not be treated as adjacent.
        var affected = Affected(SpecialGem.Area(), BoardState.ToIndex(0, 7));

        Assert.DoesNotContain(BoardState.ToIndex(1, 0), affected);
    }

    [Fact]
    public void Clipping_ShouldAlwaysKeepTheGemsOwnCell()
    {
        // §5.8.1 item 4: the activated Special Gem's own cell is always on the board,
        // so clipping never removes it, and every activation clears the cell the Gem
        // occupied (§5.5.5 item 4).
        foreach (var index in new[] { 0, 7, 56, 63, 27 })
        {
            Assert.Contains(index, Affected(SpecialGem.Burst(), index));
            Assert.Contains(index, Affected(SpecialGem.Area(), index));
            Assert.Contains(index, Affected(SpecialGem.LineClearHorizontal(), index));
            Assert.Contains(index, Affected(SpecialGem.LineClearVertical(), index));
        }
    }

    [Fact]
    public void Geometry_ShouldBeAscendingIndexOrder()
    {
        // The affected set is returned in ascending §1.0 index order, which is the
        // enumeration order the GemMatched contract uses (GAME_EVENTS.md §1.3).
        var affected = Affected(SpecialGem.Burst(), BoardState.ToIndex(3, 3));

        Assert.Equal(affected.OrderBy(i => i), affected);
    }

    [Fact]
    public void Geometry_ShouldBeDeterministic()
    {
        // §5.9.2 item 1 / §7.2 item 2: each geometry is a small set of (row, column)
        // offsets plus one clipping test — no search, no RNG. Repeated evaluation is
        // identical.
        foreach (var gem in new[] { SpecialGem.Burst(), SpecialGem.Area(), SpecialGem.LineClearHorizontal(), SpecialGem.LineClearVertical() })
        {
            foreach (var index in new[] { 0, 7, 8, 27, 56, 63 })
            {
                var first = Affected(gem, index);

                for (var run = 0; run < 10; run++)
                {
                    Assert.Equal(first, Affected(gem, index));
                }
            }
        }
    }

    [Fact]
    public void Geometry_ShouldRejectALineClearGemWithoutOrientation()
    {
        // GAME_STATE.md §2.1.4 item 2: orientation is present if and only if the type
        // is LineClear. A Line Clear Gem without one is a contract violation, and no
        // rule defines a default axis — so the geometry must fail loudly rather than
        // guess.
        var malformed = new SpecialGem(SpecialGemType.LineClear, null);

        Assert.Throws<InvalidOperationException>(
            () => SpecialGemEffects.AffectedCells(malformed, 27));
    }

    [Fact]
    public void SpecialGem_ShouldBeWellFormedOnlyWhenOrientationMatchesType()
    {
        // GAME_STATE.md §2.1.4 item 2.
        Assert.True(SpecialGem.LineClearHorizontal().IsWellFormed);
        Assert.True(SpecialGem.LineClearVertical().IsWellFormed);
        Assert.True(SpecialGem.Burst().IsWellFormed);
        Assert.True(SpecialGem.Area().IsWellFormed);

        Assert.False(new SpecialGem(SpecialGemType.LineClear, null).IsWellFormed);
        Assert.False(new SpecialGem(SpecialGemType.Burst, SpecialGemOrientation.Horizontal).IsWellFormed);
        Assert.False(new SpecialGem(SpecialGemType.Area, SpecialGemOrientation.Vertical).IsWellFormed);
    }
}