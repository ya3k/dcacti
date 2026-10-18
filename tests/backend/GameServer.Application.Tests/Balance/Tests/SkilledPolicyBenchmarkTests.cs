using GameServer.Application.Tests.Balance;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using Xunit;

namespace GameServer.Application.Tests.Balance.Tests;

public sealed class SkilledPolicyBenchmarkTests
{
    private static BoardState CreateBaseNonMatchingBoard()
    {
        var cells = new GemType[64];
        var types = new[] { GemType.Power, GemType.Def, GemType.Hp, GemType.Atk };
        for (var r = 0; r < 8; r++)
        {
            for (var c = 0; c < 8; c++)
            {
                cells[(r * 8) + c] = types[(r + c) % 4];
            }
        }
        return BoardState.FromCells(cells);
    }

    private static BattleState CreateBattleState(BoardState board) =>
        BattleState.Create(
            "test-battle",
            12345UL,
            new PlayerId("player-test"),
            new PetId("pet-test"),
            Element.Hoa,
            new PassiveId("passive-test"),
            5,
            BossDefinitions.HoaLong) with { BoardState = board };

    [Fact]
    public void ValidMatchScoring_And_ActionSelection()
    {
        var board = CreateBaseNonMatchingBoard();
        var cells = board.Cells.ToArray();

        // Move A: swap (2, 10) puts Atk at 2, creating horizontal 3-match at (0, 1, 2)
        cells[0] = GemType.Atk;
        cells[1] = GemType.Atk;
        cells[2] = GemType.Def;
        cells[3] = GemType.Def; // boundary
        cells[10] = GemType.Atk;
        cells[18] = GemType.Hp; // break vertical Def line in col 2

        var testBoard = BoardState.FromCells(cells);
        Assert.Empty(MatchDetector.Detect(testBoard)); // Board is stable initially

        var score3 = BalancePolicies.MatchedCellCountAfterExchanging(testBoard, 2, 10);
        Assert.Equal(3, score3);

        // Move with no match: (62, 63)
        var score0 = BalancePolicies.MatchedCellCountAfterExchanging(testBoard, 62, 63);
        Assert.Equal(0, score0);
    }

    [Fact]
    public void Determinism_ProducesIdenticalMoveAcrossRepeatedInvocations()
    {
        var board = CreateBaseNonMatchingBoard();
        var cells = board.Cells.ToArray();

        // Candidate 1: swap (2, 10) -> 3-match of Atk
        cells[0] = GemType.Atk;
        cells[1] = GemType.Atk;
        cells[2] = GemType.Def;
        cells[3] = GemType.Def;
        cells[10] = GemType.Atk;
        cells[18] = GemType.Hp;

        // Candidate 2: swap (34, 42) -> 4-match of Hp
        cells[32] = GemType.Hp;
        cells[33] = GemType.Hp;
        cells[34] = GemType.Power;
        cells[35] = GemType.Hp;
        cells[36] = GemType.Def;
        cells[42] = GemType.Hp;
        cells[50] = GemType.Atk;

        var testBoard = BoardState.FromCells(cells);
        Assert.Empty(MatchDetector.Detect(testBoard));

        var state = CreateBattleState(testBoard);

        var emptyAttempted = new HashSet<int>();
        var firstChoice = BalancePolicies.Skilled.ProposeSwap(state, default, emptyAttempted);
        Assert.NotNull(firstChoice);

        for (var i = 0; i < 10; i++)
        {
            var choice = BalancePolicies.Skilled.ProposeSwap(state, default, emptyAttempted);
            Assert.NotNull(choice);
            Assert.Equal(firstChoice.Value.From, choice.Value.From);
            Assert.Equal(firstChoice.Value.To, choice.Value.To);
        }
    }

    [Fact]
    public void NoDependencyOnRetiredTierMultiplierSemantics_PrefersMultiMatchOverSingle4Match()
    {
        // Under retired match-tier rules:
        // A single 4-match had Tier 2 (1.5x) and was preferred over two 3-matches by the old match-length heuristic.
        // Under OD-3 / OD-7 / ADR-028:
        // Tier multipliers are retired. Total resource = BaseOutput * cell count.
        // Candidate A: swap (2, 10) puts Atk at 2, forming horizontal 4-match at (0, 1, 2, 3) -> 4 cells.
        // Candidate B: swap (49, 57) creates two simultaneous 3-matches:
        //   - Row 6: (48, 49, 50) of Hp (3 cells)
        //   - Row 7: (57, 58, 59) of Def (3 cells)
        //   -> double 3-match totaling 6 unique cells!
        var board = CreateBaseNonMatchingBoard();
        var cells = board.Cells.ToArray();

        // Candidate A at (2, 10):
        cells[0] = GemType.Atk;
        cells[1] = GemType.Atk;
        cells[2] = GemType.Def;
        cells[3] = GemType.Atk;
        cells[4] = GemType.Def;
        cells[10] = GemType.Atk;
        cells[18] = GemType.Hp; // break vertical Def line

        // Candidate B at (49, 57):
        cells[48] = GemType.Hp;
        cells[49] = GemType.Def;
        cells[50] = GemType.Hp;
        cells[51] = GemType.Power;

        cells[56] = GemType.Power;
        cells[57] = GemType.Hp;
        cells[58] = GemType.Def;
        cells[59] = GemType.Def;
        cells[60] = GemType.Power;

        var testBoard = BoardState.FromCells(cells);
        Assert.Empty(MatchDetector.Detect(testBoard)); // Pre-swap board is stable

        var scoreA = BalancePolicies.MatchedCellCountAfterExchanging(testBoard, 2, 10);
        var scoreB = BalancePolicies.MatchedCellCountAfterExchanging(testBoard, 49, 57);

        Assert.Equal(4, scoreA); // 4-match
        Assert.Equal(6, scoreB); // double 3-match (6 cells)

        var state = CreateBattleState(testBoard);

        // The corrected skilled policy must select a move with at least 6 matched cells (Candidate B),
        // and must NOT choose Candidate A (4 cells) solely because Candidate A has a longer single run (4)!
        var selected = BalancePolicies.Skilled.ProposeSwap(state, default, new HashSet<int>());
        Assert.NotNull(selected);
        var selectedScore = BalancePolicies.MatchedCellCountAfterExchanging(testBoard, selected.Value.From, selected.Value.To);
        Assert.True(selectedScore >= 6, $"Expected selected score >= 6 (from multi-match), but got {selectedScore} from ({selected.Value.From}, {selected.Value.To})");

        // Verify contrast with legacy heuristic (which mistakenly preferred Candidate A because run length 4 > 3):
        var legacySelected = BalancePolicies.LegacySkilled.ProposeSwap(state, default, new HashSet<int>());
        Assert.NotNull(legacySelected);
        Assert.True(legacySelected.Value.From < 48, $"Legacy policy chose run-4 move ({legacySelected.Value.From}, {legacySelected.Value.To}) over 6-cell multi-match");
    }

    [Fact]
    public void RelevantTieBreakingBehavior_PreservesLowestCellPairOrder()
    {
        var board = CreateBaseNonMatchingBoard();
        var cells = board.Cells.ToArray();

        // Candidate 1: swap (2, 10) puts Atk at 2, creating 3-match at (0, 1, 2) -> 3 cells
        cells[0] = GemType.Atk;
        cells[1] = GemType.Atk;
        cells[2] = GemType.Def;
        cells[3] = GemType.Power;
        cells[10] = GemType.Atk;
        cells[18] = GemType.Hp; // break vertical Def line in col 2

        var testBoard = BoardState.FromCells(cells);
        Assert.Empty(MatchDetector.Detect(testBoard));

        var state = CreateBattleState(testBoard);

        // Find all matching moves on testBoard
        var matchingMoves = new List<SwapRequest>();
        for (var f = 0; f < 64; f++)
        {
            for (var t = f + 1; t < 64; t++)
            {
                if (BalancePolicies.IsAdjacent(f, t) && BalancePolicies.MatchedCellCountAfterExchanging(testBoard, f, t) > 0)
                {
                    matchingMoves.Add(new SwapRequest(f, t));
                }
            }
        }
        Assert.True(matchingMoves.Count >= 2, "Expected at least 2 matching moves for tie-breaking test");

        // Find the top score among these moves
        var maxScore = matchingMoves.Max(m => BalancePolicies.MatchedCellCountAfterExchanging(testBoard, m.From, m.To));
        var topTies = matchingMoves
            .Where(m => BalancePolicies.MatchedCellCountAfterExchanging(testBoard, m.From, m.To) == maxScore)
            .OrderBy(m => m.From)
            .ThenBy(m => m.To)
            .ToList();

        Assert.True(topTies.Count >= 2, "Expected at least 2 tied moves with maximum score");

        var firstExpected = topTies[0];
        var secondExpected = topTies[1];

        // Untried: must pick the first tied move in canonical index order
        var move = BalancePolicies.Skilled.ProposeSwap(state, default, new HashSet<int>());
        Assert.NotNull(move);
        Assert.Equal(firstExpected.From, move.Value.From);
        Assert.Equal(firstExpected.To, move.Value.To);

        // Once the first move is attempted, next must pick the second tied move in canonical index order
        var attempted = new HashSet<int> { BalancePlayerPolicy.PairKey(firstExpected.From, firstExpected.To) };
        var nextMove = BalancePolicies.Skilled.ProposeSwap(state, default, attempted);
        Assert.NotNull(nextMove);
        Assert.Equal(secondExpected.From, nextMove.Value.From);
        Assert.Equal(secondExpected.To, nextMove.Value.To);
    }

    [Fact]
    public void RegressionCoverage_LTShapesAreScoredAs5CellsNot3()
    {
        // An L/T shape match consists of 5 cells (e.g. 3 horizontal and 3 vertical sharing an intersection).
        // Under the old 1D heuristic, it scored only 3 because horizontal=3 and vertical=3, Math.Max(3, 3) = 3.
        // Under the corrected heuristic, all 5 unique cells are counted.
        var lCells = new GemType[64];
        for (var i = 0; i < 64; i++)
        {
            var r = i / 8;
            var c = i % 8;
            lCells[i] = ((r + c) % 2 == 0) ? GemType.Hp : GemType.Power;
        }

        lCells[8] = GemType.Atk;
        lCells[9] = GemType.Def; // swapped to 8
        lCells[10] = GemType.Atk;
        lCells[11] = GemType.Atk;
        lCells[17] = GemType.Atk;
        lCells[25] = GemType.Atk;

        var lBoard = BoardState.FromCells(lCells);
        Assert.Empty(MatchDetector.Detect(lBoard)); // Board is stable before swap

        var score = BalancePolicies.MatchedCellCountAfterExchanging(lBoard, 8, 9);
        Assert.Equal(5, score); // Exactly 5 unique cells in L-shape
    }
}
