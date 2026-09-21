using GameServer.Domain.Match3;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Gravity and Spawn tests (<c>MATCH3_RULES.md</c> §4.4, §4.5).
///
/// These verify the two sub-steps that move Gems: gravity's fixed column and row
/// order, its order-preserving compaction, and its whole-entry movement of Special
/// Gem metadata; and spawn's position, draw count, and its documented refusal to
/// create a Special Gem.
/// </summary>
public class GravityAndSpawnTests
{
    /// <summary>A board where every cell is distinct enough to track movement.</summary>
    private static BoardState Board() => TestBoard.Background();

    private static HashSet<int> Empties(params int[] cells) => [.. cells];

    // -----------------------------------------------------------------------
    // §4.4 — gravity
    // -----------------------------------------------------------------------

    [Fact]
    public void Gravity_ShouldCompactAColumnIntoItsLowestCells()
    {
        // §4.4 item 3: the survivors of a column, in their original order, occupy the
        // LOWEST cells. A hole therefore only moves gems that are ABOVE it.
        //
        // Given column 0 with row 2 (index 16) emptied: the two survivors above the
        // hole fall one row and the five below do not move, so the column's new empty
        // cell is its top cell.
        var board = Board();
        var hole = BoardState.ToIndex(2, 0);

        // When gravity runs
        var result = Gravity.Apply(board, Empties(hole));

        // The vacated cell is the top of the column, not the hole.
        var vacated = Assert.Single(result.VacatedCells);
        Assert.Equal(BoardState.ToIndex(0, 0), vacated);

        // Rows 0 and 1 moved down by exactly one; rows 3..7 are untouched.
        Assert.Equal(board[BoardState.ToIndex(0, 0)], result.Board[BoardState.ToIndex(1, 0)]);
        Assert.Equal(board[BoardState.ToIndex(1, 0)], result.Board[BoardState.ToIndex(2, 0)]);

        for (var row = 3; row < BoardState.Rows; row++)
        {
            Assert.Equal(board[BoardState.ToIndex(row, 0)], result.Board[BoardState.ToIndex(row, 0)]);
        }
    }

    [Fact]
    public void Gravity_ShouldLeaveAColumnUntouchedWhenItsHolesAreAtTheTop()
    {
        // §4.4 item 3: survivors occupy the LOWEST cells. When a column's holes are
        // already at its top, every survivor is already as low as it can be and the
        // column does not move at all. The vacated cells are still reported, because
        // they are what Spawn must fill (§4.5 item 1).
        var board = Board();
        var topThree = new[]
        {
            BoardState.ToIndex(0, 0), BoardState.ToIndex(1, 0), BoardState.ToIndex(2, 0),
        };

        var result = Gravity.Apply(board, Empties(topThree));

        // Nothing moved.
        for (var row = 0; row < BoardState.Rows; row++)
        {
            var index = BoardState.ToIndex(row, 0);
            if (topThree.Contains(index))
            {
                continue;
            }

            Assert.Equal(board[index], result.Board[index]);
        }

        // And the three top cells are reported as the cells Spawn must fill.
        Assert.Equal(topThree, result.VacatedCells.OrderBy(i => i));
    }

    [Fact]
    public void Gravity_ShouldPreserveRelativeOrderWithinAColumn()
    {
        // §4.4 item 3: "Within a column, the surviving Gems keep their original
        // top-to-bottom sequence and compact into the lowest cells; a Gem never passes
        // another Gem. Gravity is order-preserving, and is not a sort, a shuffle, or a
        // re-draw."
        var board = Board();

        // Empty one interior cell of column 2 (index 18 = row 2, column 2).
        var result = Gravity.Apply(board, Empties(18));

        // The gems above the hole fall one row; the gems below do not move.
        // Original (row, col 2) sequence survives in order.
        var expectedOrder = Enumerable.Range(0, BoardState.Rows)
            .Where(r => r != 2)
            .Select(r => board[BoardState.ToIndex(r, 2)])
            .ToArray();

        var actualOrder = Enumerable.Range(1, BoardState.Rows - 1)
            .Select(r => result.Board[BoardState.ToIndex(r, 2)])
            .ToArray();

        Assert.Equal(expectedOrder, actualOrder);
    }

    [Fact]
    public void Gravity_ShouldNeverMoveAGemBetweenColumns()
    {
        // §4.4 item 2 / §4 item 1: "Each column is independent. A Gem never leaves its
        // own column."
        var board = Board();

        // Empty the top two cells of column 3 and the bottom two of column 5.
        var empty = Empties(
            BoardState.ToIndex(0, 3), BoardState.ToIndex(1, 3),
            BoardState.ToIndex(7, 5), BoardState.ToIndex(6, 5));

        var result = Gravity.Apply(board, empty);

        // The decisive property: every surviving gem is still in the column it started
        // in. Gravity only rearranges within a column, so each column's surviving Gem
        // multiset is preserved (the vacated cells become placeholders that Spawn later
        // overwrites, so only non-vacated cells are compared).
        for (var column = 0; column < BoardState.Columns; column++)
        {
            var survivingIndexes = Enumerable.Range(0, BoardState.Rows)
                .Select(r => BoardState.ToIndex(r, column))
                .Where(i => !empty.Contains(i))
                .ToArray();

            var before = survivingIndexes.Select(i => board[i]).OrderBy(t => t).ToArray();
            var after = survivingIndexes.Select(i => result.Board[i]).OrderBy(t => t).ToArray();

            // The same gems are present in the column: nothing entered and nothing left.
            Assert.Equal(before, after);
        }

        // And no untouched column changed at all.
        foreach (var column in new[] { 0, 1, 2, 4, 6, 7 })
        {
            for (var row = 0; row < BoardState.Rows; row++)
            {
                var index = BoardState.ToIndex(row, column);
                Assert.Equal(board[index], result.Board[index]);
            }
        }
    }

    [Fact]
    public void Gravity_ShouldNotMoveGemsFromOneColumnIntoAnothersHole()
    {
        // The decisive form of §4.4 item 2: a hole in column 5 is filled only by a gem
        // from column 5 — never by a gem from a neighbouring column, even when that
        // column has gems to spare.
        var board = Board();

        // Empty row 4 of column 5 (index 37).
        var hole = BoardState.ToIndex(4, 5);
        var result = Gravity.Apply(board, Empties(hole));

        // The hole is filled by column 5's own gem from row 3.
        Assert.Equal(board[BoardState.ToIndex(3, 5)], result.Board[hole]);

        // Column 4 and column 6 are completely unchanged.
        foreach (var column in new[] { 4, 6 })
        {
            for (var row = 0; row < BoardState.Rows; row++)
            {
                var index = BoardState.ToIndex(row, column);
                Assert.Equal(board[index], result.Board[index]);
            }
        }
    }

    [Fact]
    public void Gravity_ShouldMoveTheWholeCellEntry_TypeAndSpecialGemTogether()
    {
        // §4.4 item 8 / GAME_STATE.md §2.1.5 item 1: a Special Gem is pulled down like
        // any other Gem — gravity moves the GemType AND its SpecialGem metadata
        // together, as one unit.
        //
        // Given a Special Gem at row 3 of its column and a hole BELOW it, so it falls.
        var cellIndex = BoardState.ToIndex(3, 2);
        var holeBelow = BoardState.ToIndex(4, 2);
        var board = Board().WithSpecial((cellIndex, SpecialGem.LineClearVertical()));

        var result = Gravity.Apply(board, Empties(holeBelow));

        // The gem fell into the hole, carrying its type and metadata.
        var landed = result.Board.SpecialGemAt(holeBelow);
        Assert.NotNull(landed);
        Assert.Equal(SpecialGemType.LineClear, landed!.Value.Type);

        // The Gem type travelled with it: the cell now holds exactly what the gem's
        // original cell held.
        Assert.Equal(board[cellIndex], result.Board[holeBelow]);
    }

    [Fact]
    public void Gravity_ShouldNotChangeASpecialGemsOrientation()
    {
        // §4.4 item 8 / §5.2 item 3: "a Line Clear Gem that falls keeps its
        // orientation". The orientation is a property of the created Special Gem, not
        // of its cell.
        var cellIndex = BoardState.ToIndex(3, 2);
        var holeBelow = BoardState.ToIndex(4, 2);

        foreach (var orientation in new[] { SpecialGemOrientation.Horizontal, SpecialGemOrientation.Vertical })
        {
            var board = Board().WithSpecial((cellIndex, SpecialGem.LineClear(orientation)));

            var result = Gravity.Apply(board, Empties(holeBelow));

            var fell = result.Board.SpecialGemAt(holeBelow);

            Assert.NotNull(fell);
            Assert.Equal(SpecialGemType.LineClear, fell!.Value.Type);
            Assert.Equal(orientation, fell.Value.Orientation);
        }
    }

    [Fact]
    public void Gravity_ShouldNotActivateASpecialGemByFalling()
    {
        // §4.4 item 8: "Its effect is not re-triggered by falling; activation happens
        // only as §5.5.5 defines." Gravity is a pure rearrangement that returns a
        // board — it has no activation output at all.
        var cellIndex = BoardState.ToIndex(3, 2);
        var holeBelow = BoardState.ToIndex(4, 2);
        var board = Board().WithSpecial((cellIndex, SpecialGem.Burst()));

        var result = Gravity.Apply(board, Empties(holeBelow));

        // The gem moved into the hole and is still present, un-activated.
        Assert.NotNull(result.Board.SpecialGemAt(holeBelow));
    }

    [Fact]
    public void Gravity_ShouldDrawNoRng()
    {
        // §4.4 item 6: "Gravity draws no RNG. It is a pure rearrangement." Gravity's
        // signature takes no generator, and applying it twice yields the same board.
        var board = Board();
        var empty = Empties(0, 1, 8);

        var first = Gravity.Apply(board, empty);
        var second = Gravity.Apply(board, empty);

        Assert.True(first.Board.CellsEqual(second.Board));
        Assert.Equal(first.VacatedCells, second.VacatedCells);
    }

    [Fact]
    public void Gravity_ShouldBeANoOpWhenNothingIsEmpty()
    {
        var board = Board();

        var result = Gravity.Apply(board, Empties());

        Assert.True(board.CellsEqual(result.Board));
        Assert.Empty(result.VacatedCells);
    }

    [Fact]
    public void Gravity_ShouldReportTheCellsThatAreEmptyAfterItRuns()
    {
        // The reported vacated set is what Spawn must fill (§4.5 item 1): a gem that
        // fell has moved INTO a cell that was empty, so that cell is no longer empty.
        //
        // Emptying index 2 (row 0, column 2) leaves the column's top cell empty: the
        // gems below never move because nothing below them is empty. The vacated cell
        // is therefore index 2 itself, and index 2 is what Spawn must fill.
        var board = Board();

        var result = Gravity.Apply(board, Empties(2));

        var vacated = Assert.Single(result.VacatedCells);
        Assert.Equal(2, vacated);
    }

    [Fact]
    public void Gravity_ShouldMoveTheHoleUpward_WhenTheGemAboveFallsIntoIt()
    {
        // Emptying an INTERIOR cell makes the gem directly above it fall in, so the
        // empty cell moves UP by one row — but only while the cells below the hole are
        // intact, so the survivors below do not move.
        var board = Board();

        // Empty row 1 of column 2 (index 10). The gem at row 0 falls into it, so the
        // vacated cell is row 0 of column 2 (index 2).
        var result = Gravity.Apply(board, Empties(10));

        var vacated = Assert.Single(result.VacatedCells);
        Assert.Equal(BoardState.ToIndex(0, 2), vacated);

        // The gem from row 0 now sits at row 1, carrying its type.
        Assert.Equal(board[BoardState.ToIndex(0, 2)], result.Board[BoardState.ToIndex(1, 2)]);
    }

    [Fact]
    public void Gravity_ShouldProcessColumnOrderDeterministically()
    {
        // §4.4 item 1: columns are processed in ascending §1.0 column order (0 → 7),
        // and within a column from the bottom up (row 7 → 0). Both orders are fixed.
        // The observable consequence is that the vacated-cell report is grouped by
        // column and, within a column, by ascending row.
        var board = Board();

        var result = Gravity.Apply(board, Empties(0, 1, 8, 9));

        // Column 0 loses rows 0,1; column 1 loses rows 0,1 too (indices 8,9).
        Assert.Equal(new[] { 0, 1, 8, 9 }, result.VacatedCells.OrderBy(i => i));
    }

    // -----------------------------------------------------------------------
    // §4.5 — spawn
    // -----------------------------------------------------------------------

    [Fact]
    public void Spawn_ShouldFillExactlyTheVacatedCells()
    {
        // §4.5 item 1: spawn fills exactly the cells that are empty after Gravity, and
        // no other cell is ever filled by Spawn.
        var board = Board();
        var gravity = Gravity.Apply(board, Empties(0, 8, 9));

        var spawn = Spawn.Fill(gravity.Board, Pcg32.FromSeed(7UL), new HashSet<int>(gravity.VacatedCells));

        Assert.Equal(gravity.VacatedCells.Count, spawn.SpawnedCount);
        Assert.Equal(
            gravity.VacatedCells.OrderBy(i => i),
            spawn.CreatedCells.Select(c => c.Index).OrderBy(i => i));
    }

    [Fact]
    public void Spawn_ShouldFillFromTheTopOfEachColumnDownward()
    {
        // §4.5 item 2: column order is ascending §1.0 column order (0 → 7), and within
        // a column the empty cells are filled top to bottom (row 0 → row k−1).
        var board = Board();

        // Empty the top two cells of column 4 (indices 4 and 12).
        var gravity = Gravity.Apply(board, Empties(4, 12));
        var spawn = Spawn.Fill(gravity.Board, Pcg32.FromSeed(7UL), new HashSet<int>(gravity.VacatedCells));

        // The draw order runs from the highest empty cell downward: 4 then 12.
        Assert.Equal(new[] { 4, 12 }, spawn.CreatedCells.Select(c => c.Index));
    }

    [Fact]
    public void Spawn_ShouldConsumeExactlyOneSelectionPerSpawnedCell()
    {
        // §4.5 item 4: "A spawn of s cells consumes exactly s selections — one per
        // cell, in the order of item 2, with no draw for the fill order, the position,
        // the weights, the cell's emptiness, or anything else."
        var board = Board();

        // Empty the top three cells of one column: an s = 3 spawn.
        var gravity = Gravity.Apply(board, Empties(0, 8, 16));
        var rng = Pcg32.FromSeed(7UL);

        var before = rng.CurrentState;
        var spawn = Spawn.Fill(gravity.Board, rng, new HashSet<int>(gravity.VacatedCells));

        Assert.Equal(3, spawn.SpawnedCount);

        // Replaying three bounded draws from the same start reproduces exactly the
        // gems that were spawned — the draw count and the reduction are the documented
        // ones.
        var replay = Pcg32.FromSeed(7UL);
        var expected = spawn.CreatedCells
            .Select(_ => GemTypes.All[(int)replay.NextBounded(GemTypes.Count)])
            .ToArray();

        Assert.Equal(expected, spawn.CreatedCells.Select(c => c.Cell.GemType));
        Assert.Equal(replay.CurrentState, rng.CurrentState);
        Assert.NotEqual(before, rng.CurrentState);
    }

    [Fact]
    public void Spawn_ShouldDrawNothingWhenNoCellIsEmpty()
    {
        // §4.5 item 4: "A pass that spawns nothing draws nothing."
        var board = Board();
        var rng = Pcg32.FromSeed(7UL);
        var before = rng.CurrentState;

        var spawn = Spawn.Fill(board, rng, Empties());

        Assert.Equal(0, spawn.SpawnedCount);
        Assert.Empty(spawn.CreatedCells);
        Assert.Equal(before, rng.CurrentState);
    }

    [Fact]
    public void Spawn_ShouldNeverCreateASpecialGem()
    {
        // §4.5 item 7: "Spawn never creates a Special Gem." A spawned entry always has
        // SpecialGem absent, whatever the cell held before — including a cell whose
        // Special Gem was just consumed (GAME_STATE.md §2.1.5 item 3).
        var board = Board().WithSpecial((0, SpecialGem.Burst()));

        var gravity = Gravity.Apply(board, Empties(0));
        var spawn = Spawn.Fill(gravity.Board, Pcg32.FromSeed(7UL), new HashSet<int>(gravity.VacatedCells));

        Assert.NotEmpty(spawn.CreatedCells);
        Assert.All(spawn.CreatedCells, c => Assert.Null(c.Cell.SpecialGem));
        Assert.All(spawn.CreatedCells, c => Assert.True(c.Cell.SpecialGem is null));
    }

    [Fact]
    public void Spawn_ShouldOnlyProduceTheFourDocumentedGemTypes()
    {
        // §4.5 item 3: each spawned Gem is an independent uniform draw from the four
        // §1.1 types. No fifth type and no Special Gem type appears.
        var board = Board();
        var gravity = Gravity.Apply(board, Empties(0, 1, 2, 3, 4, 5, 6, 7));
        var spawn = Spawn.Fill(gravity.Board, Pcg32.FromSeed(99UL), new HashSet<int>(gravity.VacatedCells));

        Assert.All(spawn.CreatedCells, c => Assert.True(GemTypes.IsValid((int)c.Cell.GemType)));
    }

    [Fact]
    public void Spawn_ShouldBeDeterministic()
    {
        // §4.5 item 8: for a given board-after-Gravity and a given RngState, the spawn
        // step produces exactly one board and one resulting RngState.
        var board = Board();
        var gravity = Gravity.Apply(board, Empties(0, 8, 16));

        var first = Spawn.Fill(gravity.Board, Pcg32.FromSeed(4242UL), new HashSet<int>(gravity.VacatedCells));
        var second = Spawn.Fill(gravity.Board, Pcg32.FromSeed(4242UL), new HashSet<int>(gravity.VacatedCells));

        Assert.True(first.Board.CellsEqual(second.Board));
        Assert.Equal(first.SpawnedCount, second.SpawnedCount);
    }

    [Fact]
    public void Spawn_ShouldNeverFillACellThatStillHoldsAGem()
    {
        // The composite invariant that the pre/post-gravity distinction protects: a
        // cell that still holds a survivor — including a Special Gem — is never
        // overwritten by Spawn (§4.5 item 1).
        //
        // Given an Area Gem at the bottom of its column with a hole ABOVE it: nothing
        // falls into the gem's cell, and Spawn must not overwrite it either.
        var gemCell = BoardState.ToIndex(7, 2);
        var holeAbove = BoardState.ToIndex(6, 2);
        var board = Board().WithSpecial((gemCell, SpecialGem.Area()));

        var gravity = Gravity.Apply(board, Empties(holeAbove));
        var spawn = Spawn.Fill(gravity.Board, Pcg32.FromSeed(5UL), new HashSet<int>(gravity.VacatedCells));

        // The Area Gem stayed at the bottom and survived the spawn untouched.
        var gem = spawn.Board.SpecialGemAt(gemCell);
        Assert.NotNull(gem);
        Assert.Equal(SpecialGemType.Area, gem!.Value.Type);
    }

    [Fact]
    public void Pass_ShouldNotOverwriteAFallenSpecialGemDuringSpawn()
    {
        // The defect this ordering exists to prevent: a Special Gem that fell into a
        // cell that was empty BEFORE gravity must not then be overwritten by Spawn
        // filling that stale set. Spawn fills the cells empty AFTER gravity, so the
        // gem survives into the resolved board.
        var gemCell = BoardState.ToIndex(2, 2);
        var board = Board()
            .WithGems((20, GemType.Def), (21, GemType.Def), (22, GemType.Def), (23, GemType.Def))
            .WithSpecial((gemCell, SpecialGem.Burst()));

        var result = BoardResolver.ResolvePass(
            board,
            MatchDetector.Detect(board),
            Pcg32.FromSeed(11UL),
            swapOriginIndex: null,
            cascadeDepth: 1);

        Assert.Contains(result.Board.Cells, c => c.HasSpecialGem);
    }

    [Fact]
    public void Spawn_ShouldRequireTheEmptyCellSet()
    {
        // The API makes the documented rule unmissable: Spawn must fill "exactly the
        // cells that are empty after Gravity" (§4.5 item 1), so it cannot be called
        // without naming them. A "fill everything" overload would silently overwrite
        // cells that still hold gems.
        var overloads = typeof(Spawn)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(m => m.Name == nameof(Spawn.Fill))
            .ToArray();

        var fill = Assert.Single(overloads);
        Assert.Equal(3, fill.GetParameters().Length);
    }
}