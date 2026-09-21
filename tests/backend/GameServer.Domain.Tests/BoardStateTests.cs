using GameServer.Domain.Match3;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Board contract tests (GAME_STATE.md §2.1, MATCH3_RULES.md §1.0–§1.1).
///
/// These verify the documented board shape, the single coordinate convention, and
/// the four-type Gem set. They do not test gameplay: no match resolution, swap
/// resolution, cascade, gravity, special gem, or combat behavior exists at this
/// stage.
/// </summary>
public class BoardStateTests
{
    /// <summary>Builds a 64-cell board from a repeating pattern of four types.</summary>
    private static BoardState PatternedBoard()
    {
        var cells = new GemType[BoardState.CellCount];
        for (var i = 0; i < cells.Length; i++)
        {
            cells[i] = (GemType)(i % 4);
        }

        return BoardState.FromCells(cells);
    }

    [Fact]
    public void Board_ShouldBeExactlyEightByEight()
    {
        // MATCH3_RULES.md §1.0: Size 8 x 8 (64 cells).
        Assert.Equal(8, BoardState.Width);
        Assert.Equal(8, BoardState.Height);
        Assert.Equal(8, BoardState.Rows);
        Assert.Equal(8, BoardState.Columns);
    }

    [Fact]
    public void Board_ShouldContainExactly64Cells()
    {
        // MATCH3_RULES.md §1.0, GAME_STATE.md §2.1.1 item 1.
        var board = PatternedBoard();

        Assert.Equal(64, BoardState.CellCount);
        Assert.Equal(64, board.Cells.Count);
    }

    [Fact]
    public void Board_ShouldUseRowMajorIndexing()
    {
        // MATCH3_RULES.md §1.0: index = row * 8 + column, (0,0) = top-left.
        Assert.Equal(0, BoardState.ToIndex(0, 0));
        Assert.Equal(7, BoardState.ToIndex(0, 7));
        Assert.Equal(8, BoardState.ToIndex(1, 0));
        Assert.Equal(63, BoardState.ToIndex(7, 7));
    }

    [Fact]
    public void Board_ShouldMapIndexBackToRowAndColumn()
    {
        // MATCH3_RULES.md §1.0: row = floor(index / 8), column = index % 8.
        Assert.Equal(0, BoardState.ToRow(0));
        Assert.Equal(0, BoardState.ToColumn(0));
        Assert.Equal(0, BoardState.ToRow(7));
        Assert.Equal(7, BoardState.ToColumn(7));
        Assert.Equal(1, BoardState.ToRow(8));
        Assert.Equal(0, BoardState.ToColumn(8));
        Assert.Equal(7, BoardState.ToRow(63));
        Assert.Equal(7, BoardState.ToColumn(63));
    }

    [Fact]
    public void Board_IndexRoundTripsForEveryCell()
    {
        // The mapping is the board's only coordinate convention
        // (MATCH3_RULES.md §1.0); every index must round-trip through it.
        for (var index = 0; index < BoardState.CellCount; index++)
        {
            var row = BoardState.ToRow(index);
            var column = BoardState.ToColumn(index);

            Assert.Equal(index, BoardState.ToIndex(row, column));
        }
    }

    [Fact]
    public void Board_ShouldPreserveRowMajorCellOrder()
    {
        // The cell at index i is the (i / 8, i % 8) cell, so a row-major source
        // is read back unchanged.
        var cells = new GemType[BoardState.CellCount];
        for (var i = 0; i < cells.Length; i++)
        {
            cells[i] = (GemType)(i % 4);
        }

        var board = BoardState.FromCells(cells);

        for (var index = 0; index < BoardState.CellCount; index++)
        {
            Assert.Equal(cells[index], board[index]);
        }

        // Spot-check the documented anchors.
        Assert.Equal(GemType.Atk, board[BoardState.ToIndex(0, 0)]);
        Assert.Equal(GemType.Power, board[BoardState.ToIndex(0, 3)]);
        Assert.Equal(GemType.Atk, board[BoardState.ToIndex(4, 0)]);
    }

    [Fact]
    public void Board_ShouldOnlyContainTheFourDocumentedGemTypes()
    {
        // MATCH3_RULES.md §1.1: ATK, DEF, HP, POWER are the complete MVP set.
        Assert.Equal(4, GemTypes.All.Count);
        Assert.Equal(
            new[] { GemType.Atk, GemType.Def, GemType.Hp, GemType.Power },
            GemTypes.All);
        Assert.Equal(4u, GemTypes.Count);
    }

    [Fact]
    public void Board_ShouldDeclareNoElementalGemType()
    {
        // MATCH3_RULES.md §1.1 / ELEMENT_RULES.md §1: Gems are functional only
        // and carry no Element. The Five Elements belong to the combat matchup
        // system, not to the board.
        var declared = Enum.GetNames<GemType>();

        foreach (var element in new[] { "Wood", "Fire", "Earth", "Metal", "Water", "Moc", "Hoa", "Tho", "Kim", "Thuy" })
        {
            Assert.DoesNotContain(element, declared);
        }
    }

    [Fact]
    public void GemTypes_ShouldExposeTheDocumentedContractNames()
    {
        // MATCH3_RULES.md §1.1 names the four types ATK, DEF, HP, POWER.
        Assert.Equal("ATK", GemTypes.ToContractName(GemType.Atk));
        Assert.Equal("DEF", GemTypes.ToContractName(GemType.Def));
        Assert.Equal("HP", GemTypes.ToContractName(GemType.Hp));
        Assert.Equal("POWER", GemTypes.ToContractName(GemType.Power));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(99)]
    public void Board_ShouldRejectAnUndocumentedGemValue(int invalid)
    {
        // A cell holds one of the four documented Gem types (MATCH3_RULES.md
        // §1.1); any other value is outside the domain contract.
        Assert.False(GemTypes.IsValid(invalid));

        var cells = Enumerable.Repeat(0, BoardState.CellCount).ToArray();
        cells[10] = invalid;

        Assert.Throws<ArgumentException>(() => BoardState.FromValues(cells));
    }

    [Fact]
    public void Board_ShouldRejectAWrongCellCount()
    {
        // Exactly 64 cells (MATCH3_RULES.md §1.0, GAME_STATE.md §2.1.1 item 1):
        // "a board is never partially filled at rest".
        var tooFew = Enumerable.Repeat(GemType.Atk, 63).ToArray();
        var tooMany = Enumerable.Repeat(GemType.Atk, 65).ToArray();

        Assert.Throws<ArgumentException>(() => BoardState.FromCells(tooFew));
        Assert.Throws<ArgumentException>(() => BoardState.FromCells(tooMany));
    }

    [Fact]
    public void Board_ShouldAcceptExactly64ValidValues()
    {
        var cells = Enumerable.Range(0, BoardState.CellCount).Select(i => i % 4).ToArray();

        var board = BoardState.FromValues(cells);

        Assert.Equal(64, board.Cells.Count);
    }

    [Fact]
    public void Board_ShouldCarryNoPendingSpecialGemsField()
    {
        // GAME_STATE.md §2.1.2 item 1: the PendingSpecialGems[] placeholder was REMOVED
        // — it is not deferred and not empty-but-present. `BoardState` has exactly one
        // field, `Cells[64]`, and Special Gem state is carried by the cell entries.
        //
        // The assertion is unchanged and still meaningful: no board-level Special Gem
        // collection may exist, whatever shape it might take.
        var properties = typeof(BoardState).GetProperties().Select(p => p.Name).ToArray();

        Assert.DoesNotContain("PendingSpecialGems", properties);
        Assert.DoesNotContain("SpecialGems", properties);
    }

    [Fact]
    public void ToIndex_ShouldRejectOutOfRangeCoordinates()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BoardState.ToIndex(8, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => BoardState.ToIndex(0, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => BoardState.ToIndex(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => BoardState.ToIndex(0, -1));
    }

    [Fact]
    public void Indexer_ShouldRejectAnOutOfRangeIndex()
    {
        var board = PatternedBoard();

        Assert.Throws<ArgumentOutOfRangeException>(() => board[64]);
        Assert.Throws<ArgumentOutOfRangeException>(() => board[-1]);
    }

    [Fact]
    public void AreOrthogonallyAdjacent_ShouldFollowTheFourNeighbourRule()
    {
        // MATCH3_RULES.md §2 item 1: up/down/left/right; diagonals are invalid.
        var origin = BoardState.ToIndex(3, 3);

        Assert.True(BoardState.AreOrthogonallyAdjacent(origin, BoardState.ToIndex(2, 3)));
        Assert.True(BoardState.AreOrthogonallyAdjacent(origin, BoardState.ToIndex(4, 3)));
        Assert.True(BoardState.AreOrthogonallyAdjacent(origin, BoardState.ToIndex(3, 2)));
        Assert.True(BoardState.AreOrthogonallyAdjacent(origin, BoardState.ToIndex(3, 4)));

        // Diagonals are not adjacent.
        Assert.False(BoardState.AreOrthogonallyAdjacent(origin, BoardState.ToIndex(2, 2)));
        Assert.False(BoardState.AreOrthogonallyAdjacent(origin, BoardState.ToIndex(4, 4)));
        Assert.False(BoardState.AreOrthogonallyAdjacent(origin, origin));
    }

    [Fact]
    public void WithSwapped_ShouldNotMutateTheOriginalBoard()
    {
        // The simulation step of MATCH3_RULES.md §2 item 2 works "on a copy of
        // the board" — the source board is left untouched.
        var board = PatternedBoard();
        var first = BoardState.ToIndex(0, 0);
        var second = BoardState.ToIndex(0, 1);

        var swapped = board.WithSwapped(first, second);

        Assert.Equal(board[first], swapped[second]);
        Assert.Equal(board[second], swapped[first]);

        // The original still holds its own values.
        Assert.Equal(GemType.Atk, board[first]);
        Assert.Equal(GemType.Def, board[second]);
    }

    [Fact]
    public void WithSwapped_ShouldExchangeExactlyTwoCells()
    {
        var board = PatternedBoard();
        var swapped = board.WithSwapped(BoardState.ToIndex(2, 2), BoardState.ToIndex(2, 3));

        var differences = Enumerable.Range(0, BoardState.CellCount)
            .Count(i => board[i] != swapped[i]);

        Assert.Equal(2, differences);
    }
}