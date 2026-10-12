namespace GameServer.Domain.Tests;

using GameServer.Domain.Bosses;
using GameServer.Domain.Match3;
using Xunit;

/// <summary>
/// Tests for the Deterministic Boss AI Move Selection Engine (<c>ADR-026</c> §8, Scope D).
/// </summary>
public class BossAiEngineTests
{
    private static int I(int row, int col) => TestBoard.I(row, col);

    private static BoardState Background() => TestBoard.Background();

    // =======================================================================
    // 1. Candidate Enumeration
    // =======================================================================

    [Fact]
    public void EnumerateAdjacentPairs_ShouldEnumerateExactly112Pairs()
    {
        // On an 8x8 board: 56 horizontal + 56 vertical = 112 orthogonal pairs
        var pairs = BossAiEngine.EnumerateAdjacentPairs();

        Assert.Equal(BossAiEngine.TotalCandidatePairs, pairs.Count);
        Assert.Equal(112, pairs.Count);

        // Verify all pairs are distinct
        var distinct = pairs.Distinct().Count();
        Assert.Equal(112, distinct);

        // Verify every pair is orthogonally adjacent
        foreach (var (from, to) in pairs)
        {
            Assert.True(BoardState.AreOrthogonallyAdjacent(from, to));
            Assert.True(from < to, $"Expected canonical index ordering from < to, got {from} >= {to}");
        }
    }

    // =======================================================================
    // 2. Candidate Filtering & Legality
    // =======================================================================

    [Fact]
    public void EnumerateLegalMoves_ShouldRejectInvalidSwapsAndAcceptValidSwaps()
    {
        // Setup matching board based on Background()
        var board = Background().WithGems(
            (I(4, 0), GemType.Atk),
            (I(4, 1), GemType.Def),
            (I(4, 2), GemType.Atk),
            (I(3, 1), GemType.Atk));

        var legalMoves = BossAiEngine.EnumerateLegalMoves(board);

        // Out of 112 potential adjacent swaps, only 4 produce matches, rejecting the other 108
        Assert.Equal(4, legalMoves.Count);
        Assert.True(legalMoves.Count < BossAiEngine.TotalCandidatePairs);

        // Swap (25, 33) is accepted as a valid move with positive score
        var completingMove = Assert.Single(legalMoves, m => m.Request.From == 25 && m.Request.To == 33);
        Assert.True(completingMove.Score > 0);

        // Non-match swaps (e.g. 0 <-> 1) are rejected
        Assert.DoesNotContain(legalMoves, m => m.Request.From == 0 && m.Request.To == 1);
    }

    // =======================================================================
    // 3. Deterministic Scoring
    // =======================================================================

    [Fact]
    public void Scoring_ShouldRewardHigherTierAndHigherWeightMatches()
    {
        // Custom weights: ATK = 10, DEF = 5, HP = 20, POWER = 10
        var weights = new BossMoveScoringWeights(AtkWeight: 10, DefWeight: 5, HpWeight: 20, PowerWeight: 10);

        // Base matchless stripe board (no existing matches anywhere)
        var baseBoard = TestBoard.FromRows(
            "ADADADAD",
            "HPHPHPHP",
            "ADADADAD",
            "HPHPHPHP",
            "ADADADAD",
            "HPHPHPHP",
            "ADADADAD",
            "HPHPHPHP");

        // Isolated Match 3 of ATK: (0,0), (0,1), (0,2) = A, A, A
        var boardAtk = baseBoard.WithGems((0, GemType.Atk), (1, GemType.Atk), (2, GemType.Atk));
        var matchesAtk = MatchDetector.Detect(boardAtk);
        Assert.Single(matchesAtk);
        var scoreAtk = BossAiEngine.CalculateMoveScore(matchesAtk, weights);

        // Isolated Match 3 of HP: (0,0), (0,1), (0,2) = H, H, H
        var boardHp = baseBoard.WithGems((0, GemType.Hp), (1, GemType.Hp), (2, GemType.Hp));
        var matchesHp = MatchDetector.Detect(boardHp);
        Assert.Single(matchesHp);
        var scoreHp = BossAiEngine.CalculateMoveScore(matchesHp, weights);

        // ATK Match 3 = 10 * 1 * 3 = 30; HP Match 3 = 20 * 1 * 3 = 60
        Assert.Equal(30, scoreAtk);
        Assert.Equal(60, scoreHp);
        Assert.True(scoreHp > scoreAtk);

        // Isolated Match 4 of ATK: (0,0), (0,1), (0,2), (0,3) = A, A, A, A and col 4 = D
        var boardAtk4 = baseBoard.WithGems(
            (0, GemType.Atk), (1, GemType.Atk), (2, GemType.Atk), (3, GemType.Atk), (4, GemType.Def));
        var matchesAtk4 = MatchDetector.Detect(boardAtk4);
        Assert.Single(matchesAtk4);
        var scoreAtk4 = BossAiEngine.CalculateMoveScore(matchesAtk4, weights);

        // ATK Match 4: (10 * 6 * 4) / 4 = 60
        Assert.Equal(60, scoreAtk4);
        Assert.True(scoreAtk4 > scoreAtk);
    }

    // =======================================================================
    // 4. Stable Tie-Breaking
    // =======================================================================

    [Fact]
    public void SelectMove_WhenScoresAreEqual_ShouldBreakTieByLowestCellIndex()
    {
        // Setup two independent identical Match-3 swaps on a uniform Power background:
        // Move 1: at row 1: exchanging (1,1) [index 9] and (2,1) [index 17] produces Atk Match 3
        // Move 2: at row 5: exchanging (5,1) [index 41] and (6,1) [index 49] produces Atk Match 3
        var board = TestBoard.Uniform(
            GemType.Power,
            // Move 1 setup
            (I(2, 0), GemType.Atk),
            (I(2, 1), GemType.Def),
            (I(2, 2), GemType.Atk),
            (I(1, 1), GemType.Atk),
            // Move 2 setup
            (I(6, 0), GemType.Atk),
            (I(6, 1), GemType.Def),
            (I(6, 2), GemType.Atk),
            (I(5, 1), GemType.Atk));

        var rng = Pcg32.FromSeed(12345);
        var result = BossAiEngine.SelectMove(board, rng);

        // Both moves produce identical score (ATK Match 3).
        // Tie-breaker: Move 1 has lower 'From' index (9 < 41), so Move 1 must be selected.
        Assert.Equal(9, result.SelectedMove.From);
        Assert.Equal(17, result.SelectedMove.To);
        Assert.False(result.Reshuffled);
    }

    // =======================================================================
    // 5. Determinism & Purity
    // =======================================================================

    [Fact]
    public void SelectMove_OnIdenticalBoard_ShouldBe100PercentDeterministic()
    {
        var board = Background().WithGems(
            (I(4, 0), GemType.Atk),
            (I(4, 1), GemType.Def),
            (I(4, 2), GemType.Atk),
            (I(3, 1), GemType.Atk),
            (I(6, 5), GemType.Hp),
            (I(6, 6), GemType.Power),
            (I(6, 7), GemType.Hp),
            (I(7, 6), GemType.Hp));

        var rng1 = Pcg32.FromSeed(42);
        var initialRngState = rng1.CurrentState;
        var rng2 = Pcg32.FromSeed(999999); // Even with different seeds, move selection without deadlock draws 0 RNG

        var result1 = BossAiEngine.SelectMove(board, rng1);
        var result2 = BossAiEngine.SelectMove(board, rng2);

        Assert.Equal(result1.SelectedMove.From, result2.SelectedMove.From);
        Assert.Equal(result1.SelectedMove.To, result2.SelectedMove.To);
        Assert.Equal(result1.Score, result2.Score);
        Assert.Equal(initialRngState.State, rng1.CurrentState.State); // Zero RNG consumed when not deadlocked
    }

    // =======================================================================
    // 6. Deadlock Detection & Deterministic Reshuffle
    // =======================================================================

    [Fact]
    public void SelectMove_WhenBoardIsDeadlocked_ShouldReshuffleAndSelectMove()
    {
        // Construct a deadlocked board (alternating 2-row stripe pattern where no swap can produce a 3-run)
        var deadlockedBoard = TestBoard.FromRows(
            "ADADADAD",
            "HPHPHPHP",
            "ADADADAD",
            "HPHPHPHP",
            "ADADADAD",
            "HPHPHPHP",
            "ADADADAD",
            "HPHPHPHP");

        Assert.False(BoardGenerationValidator.HasMatch(deadlockedBoard));
        Assert.False(BoardGenerationValidator.HasValidSwap(deadlockedBoard));
        Assert.Empty(BossAiEngine.EnumerateLegalMoves(deadlockedBoard));

        // When: SelectMove is called with Pcg32 stream
        var rng = Pcg32.FromSeed(20261011UL);
        var initialRngState = rng.CurrentState;

        var result = BossAiEngine.SelectMove(deadlockedBoard, rng);

        // Then:
        // 1. Reshuffle occurred
        Assert.True(result.Reshuffled);
        // 2. RNG state advanced
        Assert.NotEqual(initialRngState.State, result.RngState.State);
        // 3. New board is valid and has valid moves
        Assert.True(BoardGenerationValidator.IsValidInitialBoard(result.Board));
        Assert.True(result.LegalCandidatesCount > 0);
        // 4. Selected move is legal on the new board
        Assert.True(BoardState.AreOrthogonallyAdjacent(result.SelectedMove.From, result.SelectedMove.To));
        Assert.True(result.Score > 0);
    }
}
