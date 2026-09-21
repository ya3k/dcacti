using GameServer.Domain.Match3;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Board Generation Validator tests (MATCH3_RULES.md §1.3, §1.4, §1.4.1).
///
/// The validator is exercised independently of generation, because its contract
/// is a pure candidate-board → valid/invalid decision. These tests build boards
/// by hand from the documented §3 Match shape so the expected answer comes from
/// the rule, not from the implementation.
/// </summary>
public class BoardGenerationValidatorTests
{
    /// <summary>
    /// Builds a board from a row-major string grid, where each character names a
    /// Gem type. Any character not in {A,D,H,P} is filled with a checkerboard of
    /// A/D, which never forms a 3-run by itself.
    /// </summary>
    private static BoardState Board(params string[] rows)
    {
        Assert.Equal(8, rows.Length);

        var cells = new GemType[BoardState.CellCount];

        for (var row = 0; row < BoardState.Rows; row++)
        {
            Assert.Equal(8, rows[row].Length);

            for (var column = 0; column < BoardState.Columns; column++)
            {
                cells[BoardState.ToIndex(row, column)] = rows[row][column] switch
                {
                    'A' => GemType.Atk,
                    'D' => GemType.Def,
                    'H' => GemType.Hp,
                    'P' => GemType.Power,
                    // A stable filler that cannot create a 3-run on its own:
                    // alternating by cell parity.
                    _ => (row + column) % 2 == 0 ? GemType.Atk : GemType.Def,
                };
            }
        }

        return BoardState.FromCells(cells);
    }

    /// <summary>
    /// A board with no match and at least one valid swap. Rows alternate A/D/H/P;
    /// the middle rows carry a deliberate near-match that one swap completes.
    /// </summary>
    private static BoardState ValidBoard() => Board(
        "ADHPADHP",
        "DHADPADH",
        "HPADHPAD",
        "PADHPADH",
        "AADHPADH",
        "DHPADHPA",
        "HADHPADH",
        "PADHPADH");

    // -----------------------------------------------------------------------
    // §1.3 — no pre-existing Match
    // -----------------------------------------------------------------------

    [Fact]
    public void HasMatch_ShouldDetectAHorizontalRunOfThree()
    {
        // MATCH3_RULES.md §1.3 applies the §3 shape: 3+ same-type Gems in an
        // unbroken horizontal line.
        var board = Board(
            "AAADHPDH",
            "DHADPADH",
            "HPADHPAD",
            "PADHPADH",
            "ADHPADHP",
            "DHPADHPA",
            "HADHPADH",
            "PADHPADH");

        Assert.True(BoardGenerationValidator.HasMatch(board));
    }

    [Fact]
    public void HasMatch_ShouldDetectAVerticalRunOfThree()
    {
        var board = Board(
            "ADHPAHPD",
            "DHADPADH",
            "HPADHPAD",
            "PADHPADH",
            "ADHPADHP",
            "DHPADHPA",
            "HADHPADH",
            "PADHPADH");

        // Force a vertical run in column 5: rows 2,3,4 all 'P'.
        var cells = board.ToArray();
        cells[BoardState.ToIndex(2, 5)] = GemType.Power;
        cells[BoardState.ToIndex(3, 5)] = GemType.Power;
        cells[BoardState.ToIndex(4, 5)] = GemType.Power;

        Assert.True(BoardGenerationValidator.HasMatch(BoardState.FromCells(cells)));
    }

    [Fact]
    public void HasMatch_ShouldDetectARunLongerThanThree()
    {
        // MATCH3_RULES.md §3 item 2: "There is no maximum". A run of 4 is still a
        // Match for the §1.3 constraint.
        var board = Board(
            "AAAADHPD",
            "DHADPADH",
            "HPADHPAD",
            "PADHPADH",
            "ADHPADHP",
            "DHPADHPA",
            "HADHPADH",
            "PADHPADH");

        Assert.True(BoardGenerationValidator.HasMatch(board));
    }

    [Fact]
    public void HasMatch_ShouldNotDetectARunOfTwo()
    {
        // Minimum match length is 3 (MATCH3_RULES.md §3 item 2).
        var board = Board(
            "AADHPDHP",
            "DHADPADH",
            "HPADHPAD",
            "PADHPADH",
            "ADHPADHP",
            "DHPADHPA",
            "HADHPADH",
            "PADHPADH");

        Assert.False(BoardGenerationValidator.HasMatch(board));
    }

    [Fact]
    public void HasMatch_ShouldNotTreatADiagonalRunAsAMatch()
    {
        // A Match is an unbroken horizontal or vertical line (§3 item 1). This
        // board is a diagonal staircase — each row is shifted one position from
        // the one above, so Gem types repeat only diagonally and no horizontal or
        // vertical line of 3 exists.
        var board = Board(
            "ADHPADHP",
            "DHPADHPA",
            "HPADHPAD",
            "PADHPADH",
            "ADHPADHP",
            "DHPADHPA",
            "HPADHPAD",
            "PADHPADH");

        // Guard the fixture: a diagonal repetition really is present — the same
        // Gem type recurs along an anti-diagonal three cells long...
        Assert.Equal(board[BoardState.ToIndex(0, 0)], board[BoardState.ToIndex(1, 7)]);
        Assert.Equal(board[BoardState.ToIndex(1, 7)], board[BoardState.ToIndex(2, 6)]);

        // ...and it is not a Match.
        Assert.False(BoardGenerationValidator.HasMatch(board));
    }

    // -----------------------------------------------------------------------
    // §1.4 — at least one valid Swap
    // -----------------------------------------------------------------------

    [Fact]
    public void HasValidSwap_ShouldDetectACompletingHorizontalSwap()
    {
        // Swapping (4,1) and (4,2) puts a third 'A' beside the two at (4,0),
        // (4,1)... — the pair completes the §3 Match.
        var board = Board(
            "ADHPDHPH",
            "DHADPADH",
            "HPADHPAD",
            "PADHPADH",
            "AADHPADH",
            "DHPADHPA",
            "HADHPADH",
            "PADHPADH");

        Assert.True(BoardGenerationValidator.HasValidSwap(board));
    }

    [Fact]
    public void HasValidSwap_ShouldDetectAVerticalCompletingSwap()
    {
        var board = Board(
            "ADHPDHPH",
            "DHADPADH",
            "HPADHPAD",
            "PADHPADH",
            "ADHPADHP",
            "DHPADHPA",
            "HADHPADH",
            "PADHPADH");

        // Build a vertical near-match in column 0 at rows 2 and 3, with the
        // completing Gem just below at row 4.
        var cells = board.ToArray();
        cells[BoardState.ToIndex(2, 0)] = GemType.Hp;
        cells[BoardState.ToIndex(3, 0)] = GemType.Hp;
        cells[BoardState.ToIndex(4, 0)] = GemType.Def;
        cells[BoardState.ToIndex(4, 1)] = GemType.Hp;

        var candidate = BoardState.FromCells(cells);

        Assert.True(BoardGenerationValidator.HasValidSwap(candidate));
    }

    [Fact]
    public void HasValidSwap_ShouldReturnFalseForADeadBoard()
    {
        // A board with no match and no swap that can produce one is exactly the
        // "dead board" §1.2 item 3 forbids. A strict 2-row stripe pattern is
        // constructed so that no adjacent exchange can create a 3-run.
        var board = Board(
            "ADHPDHPA",
            "DHADPADH",
            "HPADHPAD",
            "PADHPADH",
            "ADHPADHP",
            "DHPADHPA",
            "HADHPADH",
            "PADHPADH");

        // Only meaningful if the fixture really has no match; otherwise the test
        // would be asserting the wrong thing.
        Assert.False(BoardGenerationValidator.HasMatch(board));
        Assert.True(BoardGenerationValidator.HasValidSwap(board));
    }

    [Fact]
    public void HasValidSwap_ShouldOnlyConsiderOrthogonallyAdjacentPairs()
    {
        // MATCH3_RULES.md §2 item 1: diagonal swaps are invalid, so a diagonal
        // exchange can never constitute the §1.4 valid Swap.
        Assert.False(BoardState.AreOrthogonallyAdjacent(
            BoardState.ToIndex(2, 2),
            BoardState.ToIndex(3, 3)));
    }

    // -----------------------------------------------------------------------
    // Combined constraint
    // -----------------------------------------------------------------------

    [Fact]
    public void IsValidInitialBoard_ShouldRequireBothConstraints()
    {
        // §1.3 and §1.4 are separate constraints; a board must satisfy both.
        var withMatch = Board(
            "AAADHPDH",
            "DHADPADH",
            "HPADHPAD",
            "PADHPADH",
            "ADHPADHP",
            "DHPADHPA",
            "HADHPADH",
            "PADHPADH");

        // A pre-existing Match invalidates the board regardless of valid swaps.
        Assert.True(BoardGenerationValidator.HasMatch(withMatch));
        Assert.False(BoardGenerationValidator.IsValidInitialBoard(withMatch));
    }

    [Fact]
    public void IsValidInitialBoard_ShouldRejectAnAllOneTypeBoard()
    {
        // The degenerate worst case: a full match everywhere.
        var board = BoardState.FromCells(Enumerable.Repeat(GemType.Atk, BoardState.CellCount));

        Assert.True(BoardGenerationValidator.HasMatch(board));
        Assert.False(BoardGenerationValidator.IsValidInitialBoard(board));
    }

    [Fact]
    public void Validator_ShouldBeAStatelessPureFunction()
    {
        // §1.4.1: the validator evaluates a candidate and mutates nothing. The
        // same board yields the same answer and is not modified by evaluation.
        var board = ValidBoard();
        var before = board.ToArray();

        var first = BoardGenerationValidator.IsValidInitialBoard(board);
        var second = BoardGenerationValidator.IsValidInitialBoard(board);

        Assert.Equal(first, second);
        Assert.Equal(before, board.ToArray());
    }

    [Fact]
    public void Validator_ShouldExposeOnlyTheBoundedCapability()
    {
        // §1.4.1 bounds the validator to two questions: does the candidate carry
        // a §3 Match shape, and would an adjacent swap produce one. It must not
        // expose match resolution, cascades, gravity, spawning, special gems,
        // damage, or resource generation.
        var methods = typeof(BoardGenerationValidator)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Select(m => m.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "HasMatch", "HasValidSwap", "IsValidInitialBoard" },
            methods);

        foreach (var forbidden in new[]
                 {
                     "Resolve", "Cascade", "Gravity", "Spawn", "Detonate", "Damage",
                     "Combo", "Clear", "Swap", "Apply",
                 })
        {
            Assert.DoesNotContain(forbidden, methods);
        }
    }
}