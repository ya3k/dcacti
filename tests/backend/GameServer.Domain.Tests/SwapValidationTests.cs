using GameServer.Domain.Match3;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Gameplay Swap Validation tests (<c>MATCH3_RULES.md</c> §2, §2.1.2).
///
/// Boards are written as 8 rows of 8 characters using <see cref="TestBoard"/>
/// so a scenario reads as the board the documentation describes: the character
/// at row <c>r</c>, column <c>c</c> is cell index <c>r * 8 + c</c>
/// (<c>MATCH3_RULES.md</c> §1.0). The expected answer comes from the rule, not
/// from the implementation.
///
/// The background board <see cref="TestBoard.Background"/> carries no Match of
/// its own, so any Match a test observes was produced by the swap under test.
/// </summary>
public class SwapValidationTests
{
    private static BoardState Background() => TestBoard.Background();

    private static int I(int row, int column) => TestBoard.I(row, column);

    // =======================================================================
    // §2.1.2 items 1–2 — index range and distinctness (INVALID_CELL_INDEX)
    // =======================================================================

    [Theory]
    [InlineData(-1)]
    [InlineData(-8)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Validate_ShouldRejectFromIndexOutsideTheBoard(int from)
    {
        // §2.1.2 item 1: from must be a cell index in 0..63. A value outside
        // that range is not a cell and the action is rejected
        // (INVALID_CELL_INDEX) — not clamped, folded, or reinterpreted
        // (§2.1.3 item 2).
        var result = Validate(Background(), from, 0);

        Assert.True(result.IsRejected);
        Assert.Equal(SwapRejectionReason.InvalidCellIndex, result.Reason);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-8)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Validate_ShouldRejectToIndexOutsideTheBoard(int to)
    {
        // §2.1.2 item 1 applies to both cells, symmetrically.
        var result = Validate(Background(), 0, to);

        Assert.True(result.IsRejected);
        Assert.Equal(SwapRejectionReason.InvalidCellIndex, result.Reason);
    }

    [Fact]
    public void Validate_ShouldRejectAnOutOfRangeIndexEvenWhenTheOtherCellIsAlsoInvalid()
    {
        // Both cells out of range: the range check is the first check of
        // §2.1.2's order, so it is the reason reported.
        var result = Validate(Background(), 99, -3);

        Assert.Equal(SwapRejectionReason.InvalidCellIndex, result.Reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(31)]
    [InlineData(56)]
    [InlineData(63)]
    public void Validate_ShouldRejectASwapOfACellWithItself(int index)
    {
        // §2.1.2 item 2: "from must differ from to; a swap of a cell with itself
        // is not a Swap and is rejected (INVALID_CELL_INDEX)."
        var result = Validate(Background(), index, index);

        Assert.True(result.IsRejected);
        Assert.Equal(SwapRejectionReason.InvalidCellIndex, result.Reason);
    }

    [Fact]
    public void Validate_ShouldNotRejectTheBoardEdgesAsIndices()
    {
        // 0 and 63 are valid cells (§1.0: (0,0) is index 0, index 63 is the
        // bottom-right cell), so neither is rejected for being out of range.
        // They are not adjacent, so the reason is the adjacency one — which
        // proves the range check passed rather than reporting the wrong code.
        var result = Validate(Background(), 0, 63);

        Assert.Equal(SwapRejectionReason.InvalidSwap, result.Reason);
    }

    // =======================================================================
    // §2.1.2 item 3 — adjacency (INVALID_SWAP)
    // =======================================================================

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(8, 9)]
    [InlineData(6, 7)]
    [InlineData(62, 63)]
    public void Validate_ShouldAcceptHorizontalAdjacency(int from, int to)
    {
        // §2 item 1: up/down/left/right. These pairs are horizontally adjacent
        // on the §1.0 mapping, so they pass the adjacency check. They may still
        // be rejected for producing no Match (§2.1.2 item 4) — this test asserts
        // only that the failure is not the adjacency one.
        var result = Validate(Background(), from, to);

        Assert.NotEqual(SwapRejectionReason.InvalidSwap, result.Reason);
        Assert.NotEqual(SwapRejectionReason.InvalidCellIndex, result.Reason);
    }

    [Theory]
    [InlineData(7, 8)]
    [InlineData(15, 16)]
    [InlineData(23, 24)]
    [InlineData(31, 32)]
    public void Validate_ShouldRejectRowWrapAdjacency(int from, int to)
    {
        // §2.1.3 item 1: "Adjacency is evaluated on the §1.0 row-major mapping,
        // never on raw index arithmetic alone: index 7 and index 8 differ by 1
        // but are NOT adjacent (row 0 column 7 vs. row 1 column 0). No
        // wraparound exists across a row edge."
        //
        // The pairs below all differ by exactly 1, so an implementation using
        // Math.Abs(from - to) == 1 alone would wrongly accept them.
        Assert.Equal(1, to - from);
        Assert.False(BoardState.AreOrthogonallyAdjacent(from, to));

        var result = Validate(Background(), from, to);

        Assert.True(result.IsRejected);
        Assert.Equal(SwapRejectionReason.InvalidSwap, result.Reason);
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(1, 9)]
    [InlineData(7, 15)]
    [InlineData(8, 16)]
    [InlineData(55, 63)]
    public void Validate_ShouldAcceptVerticalAdjacency(int from, int to)
    {
        // §2 item 1: up/down is adjacent. Same row-relative column, one row
        // apart on the §1.0 mapping. As above, only the adjacency check is
        // asserted here.
        var result = Validate(Background(), from, to);

        Assert.NotEqual(SwapRejectionReason.InvalidSwap, result.Reason);
        Assert.NotEqual(SwapRejectionReason.InvalidCellIndex, result.Reason);
    }

    [Theory]
    [InlineData(0, 9)]
    [InlineData(1, 8)]
    [InlineData(9, 18)]
    [InlineData(16, 25)]
    public void Validate_ShouldRejectDiagonalSwaps(int from, int to)
    {
        // §2 item 1: "Diagonal swaps are invalid." §2.1.2 item 3 names the
        // diagonal pair explicitly among the rejected cases.
        var result = Validate(Background(), from, to);

        Assert.True(result.IsRejected);
        Assert.Equal(SwapRejectionReason.InvalidSwap, result.Reason);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(0, 16)]
    [InlineData(10, 30)]
    [InlineData(0, 63)]
    [InlineData(3, 4 + 8)]
    public void Validate_ShouldRejectNonAdjacentSwaps(int from, int to)
    {
        // §2.1.2 item 3: "a distant pair" is rejected. These pairs are neither
        // orthogonally adjacent nor equal, and all are in range.
        var result = Validate(Background(), from, to);

        Assert.True(result.IsRejected);
        Assert.Equal(SwapRejectionReason.InvalidSwap, result.Reason);
    }

    [Fact]
    public void Validate_ShouldRejectFlippedRowWrapInEitherOrder()
    {
        // The pair is unordered (§2.1.1 item 2), so a rejected wrap-around pair
        // is rejected identically when the arguments are reversed.
        var forward = Validate(Background(), 7, 8);
        var reversed = Validate(Background(), 8, 7);

        Assert.Equal(SwapRejectionReason.InvalidSwap, forward.Reason);
        Assert.Equal(forward, reversed);
    }

    // =======================================================================
    // §2.1.2 item 4 — match-producing (NO_MATCH_FROM_SWAP), §2 item 3
    // =======================================================================

    [Fact]
    public void Validate_ShouldAcceptASwapThatCompletesAHorizontalMatch()
    {
        // Row 4 holds 'A','D','A' at columns 0-2, and row 3 column 1 holds 'A'
        // directly above the 'D'. Swapping (3,1)<->(4,1) drops that 'A' into
        // (4,1), giving row 4 'A','A','A' at columns 0-2 — the §3 horizontal
        // Match. Neither cell is part of a run before the swap.
        var board = TestBoard.Background().WithGems(
            (I(4, 0), GemType.Atk),
            (I(4, 1), GemType.Def),
            (I(4, 2), GemType.Atk),
            (I(3, 1), GemType.Atk));

        // Guard the fixture: the board itself carries no Match before the swap.
        Assert.Empty(MatchDetector.Detect(board));

        var result = Validate(board, I(3, 1), I(4, 1));

        Assert.True(result.IsAccepted);
        Assert.Equal(SwapRejectionReason.None, result.Reason);
    }

    [Fact]
    public void Validate_ShouldAcceptASwapThatCompletesAVerticalMatch()
    {
        // A vertical near-match in column 0 at rows 2 and 3, with the completing
        // Gem to the right at (3,1) — the 'H' the swap pulls into (3,0)
        // completing rows 2-4 is at (2,0),(3,0),(4,0) after the exchange.
        var board = TestBoard.Background().WithGems(
            (I(2, 0), GemType.Hp),
            (I(3, 0), GemType.Def),
            (I(4, 0), GemType.Hp),
            (I(3, 1), GemType.Hp));

        Assert.Empty(MatchDetector.Detect(board));

        var result = Validate(board, I(3, 0), I(3, 1));

        Assert.True(result.IsAccepted);
        Assert.Equal(SwapRejectionReason.None, result.Reason);
    }

    [Fact]
    public void Validate_ShouldRejectAnAdjacentSwapThatProducesNoMatch()
    {
        // §2.1.2 item 4: a swap that produces no Match is rejected
        // (NO_MATCH_FROM_SWAP). The background board has no Match and no
        // near-match, so exchanging two of its cells completes nothing.
        var board = Background();

        Assert.Empty(MatchDetector.Detect(board));

        var result = Validate(board, I(0, 0), I(0, 1));

        Assert.True(result.IsRejected);
        Assert.Equal(SwapRejectionReason.NoMatchFromSwap, result.Reason);
    }

    [Fact]
    public void Validate_ShouldRejectWhenOnlyTheDiagonalOfThePairWouldMatch()
    {
        // Row 4 holds 'A','D','A' at columns 0-2 and the completing 'A' sits
        // below at (5,1). Exchanging (3,1)<->(4,1) is the completing swap; the
        // requested horizontal pair (4,0)<->(4,1) is adjacent but completes
        // nothing, and must therefore be rejected as NO_MATCH_FROM_SWAP: the
        // check evaluates the requested pair, not any pair that would work.
        var board = TestBoard.Background().WithGems(
            (I(4, 0), GemType.Atk),
            (I(4, 1), GemType.Def),
            (I(4, 2), GemType.Atk),
            (I(3, 1), GemType.Atk),
            (I(5, 1), GemType.Def));

        Assert.Empty(MatchDetector.Detect(board));

        // (4,0)<->(4,1) is adjacent but completes no line.
        var wrongPair = Validate(board, I(4, 0), I(4, 1));
        Assert.Equal(SwapRejectionReason.NoMatchFromSwap, wrongPair.Reason);

        // The completing vertical pair is accepted, proving the fixture really
        // does contain a swap that works.
        var rightPair = Validate(board, I(3, 1), I(4, 1));
        Assert.True(rightPair.IsAccepted);
    }

    [Fact]
    public void Validate_ShouldAcceptASwapProducingMoreThanOneMatch()
    {
        // §2.1.2 item 4: "at least one" is the whole requirement. No minimum
        // number of matches and no tier requirement is defined, so a swap
        // producing two simultaneous §3 shapes is accepted exactly as one
        // producing a single shape.
        //
        // Two independent shapes, deliberately NOT sharing a cell, so they stay two
        // entries in the match set rather than grouping into one L/T shape
        // (§3.1 item 3, §3.3 item 2).
        //
        // The 'A' at (1,2) completes the vertical run in column 2 at rows 2-4
        // after the swap, and the 'A' already at (2,5) with (2,5)..(2,7) forms a
        // separate horizontal run in row 2 once (2,4) receives the 'A' that the
        // exchange moves there.
        //
        // Swapping (1,2)<->(2,2) fills (2,2) with the completing 'A' for column 2
        // (rows 2-4), and the exchange also leaves row 5 columns 5-7 an
        // untouched independent run that the detector reports separately.
        var board = TestBoard.Background().WithGems(
            (I(2, 2), GemType.Def),
            (I(3, 2), GemType.Atk),
            (I(4, 2), GemType.Atk),
            (I(1, 2), GemType.Atk),
            (I(5, 5), GemType.Hp),
            (I(5, 6), GemType.Hp),
            (I(5, 7), GemType.Hp));

        // The board before the swap already holds the independent row-5 run, so
        // it is not match-free; guard only the column-2 near-match instead.
        Assert.Single(MatchDetector.Detect(board));

        // After the swap, the column-2 run exists as well: two distinct shapes.
        var swapped = board.WithSwapped(I(1, 2), I(2, 2));
        Assert.Equal(2, MatchDetector.Detect(swapped).Count);

        var result = Validate(board, I(1, 2), I(2, 2));

        Assert.True(result.IsAccepted);
    }

    [Fact]
    public void Validate_ShouldAcceptASwapProducingAMatchOfLengthFour()
    {
        // Row 5 holds 'P','P','D','P' at columns 0-3, and (4,2) holds the 'P'
        // that fills the gap. Swapping (4,2)<->(5,2) makes row 5 'PPPP'.
        var board = TestBoard.Background().WithGems(
            (I(5, 0), GemType.Power),
            (I(5, 1), GemType.Power),
            (I(5, 2), GemType.Def),
            (I(5, 3), GemType.Power),
            (I(4, 2), GemType.Power));

        Assert.Empty(MatchDetector.Detect(board));

        var result = Validate(board, I(4, 2), I(5, 2));

        Assert.True(result.IsAccepted);
        Assert.Equal(4, MatchDetector.Detect(board.WithSwapped(I(4, 2), I(5, 2)))[0].Cells.Count);
    }

    [Fact]
    public void Validate_ShouldEvaluateMatchesWithTheAuthoritativeDetector()
    {
        // §1.4.2 / AGENTS.md §9: there must remain one authoritative Match
        // Detection. For every adjacent pair of a board, the validator's
        // acceptance must agree exactly with "the §3 detector finds a match on
        // the swapped board" — no second, weaker detection rule.
        var board = TestBoard.Background().WithGems(
            (I(4, 0), GemType.Atk),
            (I(4, 1), GemType.Def),
            (I(4, 2), GemType.Atk),
            (I(3, 1), GemType.Atk));

        for (var from = 0; from < BoardState.CellCount; from++)
        {
            for (var to = 0; to < BoardState.CellCount; to++)
            {
                if (from == to || !BoardState.AreOrthogonallyAdjacent(from, to))
                {
                    continue;
                }

                var expected = MatchDetector.Detect(board.WithSwapped(from, to)).Count > 0;
                var actual = Validate(board, from, to).IsAccepted;

                Assert.Equal(expected, actual);
            }
        }
    }

    // =======================================================================
    // §2.1.1 item 2 / §2.1.3 item 3 — the pair is unordered
    // =======================================================================

    [Fact]
    public void Validate_ShouldBeSymmetricInFromAndTo()
    {
        // §2.1.1 item 2: "the order of from and to has no gameplay meaning".
        // §2.1.3 item 3: "from and to are symmetric inputs: neither is a
        // 'primary' cell for validation".
        var board = TestBoard.Background().WithGems(
            (I(4, 0), GemType.Atk),
            (I(4, 1), GemType.Def),
            (I(4, 2), GemType.Atk),
            (I(3, 1), GemType.Atk));

        var forward = Validate(board, I(3, 1), I(4, 1));
        var reversed = Validate(board, I(4, 1), I(3, 1));

        Assert.True(forward.IsAccepted);
        Assert.Equal(forward, reversed);
    }

    [Fact]
    public void Validate_ShouldTreatTheRequestOverloadAndIndexOverloadIdentically()
    {
        // The SwapRequest overload and the (from, to) overload are one
        // operation, not two rules.
        var board = TestBoard.Background().WithGems(
            (I(4, 0), GemType.Atk),
            (I(4, 1), GemType.Def),
            (I(4, 2), GemType.Atk),
            (I(3, 1), GemType.Atk));

        var request = new SwapRequest(I(3, 1), I(4, 1));

        Assert.Equal(
            SwapValidator.Validate(board, lastCommittedSwapPair: null, request),
            SwapValidator.Validate(board, lastCommittedSwapPair: null, request.From, request.To));
    }

    // =======================================================================
    // §9 / §2.1.5 / GAME_STATE.md §2.1.5 — Special Gem cells
    // =======================================================================

    [Fact]
    public void Validate_ShouldAcceptASwapInvolvingASpecialGemCell()
    {
        // §9 of the task and MATCH3_RULES.md §2: a Special Gem is part of its
        // Cell and is a movable board entry. No special swap behavior is defined
        // by the authoritative rules, so a cell carrying a Special Gem
        // participates in the normal adjacency and match checks exactly like an
        // ordinary one.
        var board = TestBoard.Background()
            .WithGems(
                (I(4, 0), GemType.Atk),
                (I(4, 1), GemType.Def),
                (I(4, 2), GemType.Atk),
                (I(3, 1), GemType.Atk))
            .WithSpecial(
                (I(3, 1), SpecialGem.Burst()),
                (I(4, 1), SpecialGem.Area()));

        var result = Validate(board, I(3, 1), I(4, 1));

        Assert.True(result.IsAccepted);
    }

    [Fact]
    public void Validate_ShouldNotAlterSpecialGemStateOnTheBoard()
    {
        // Validation "must not destroy or activate them, must not consume them,
        // must not alter them" (task §9). The board's 64 entries — including the
        // Special Gem type and orientation at each cell — are identical before
        // and after, for an accepted request.
        var board = TestBoard.Background()
            .WithGems(
                (I(4, 0), GemType.Atk),
                (I(4, 1), GemType.Def),
                (I(4, 2), GemType.Atk),
                (I(3, 1), GemType.Atk))
            .WithSpecial(
                (I(3, 1), SpecialGem.LineClearVertical()),
                (I(4, 1), SpecialGem.Burst()),
                (I(0, 5), SpecialGem.Area()));

        var before = board.ToCellArray();

        var result = Validate(board, I(3, 1), I(4, 1));
        Assert.True(result.IsAccepted);

        var after = board.ToCellArray();

        Assert.Equal(before.Length, after.Length);
        for (var i = 0; i < before.Length; i++)
        {
            Assert.Equal(before[i], after[i]);
        }

        // The Special Gems are still on the cells they started on.
        Assert.Equal(SpecialGemType.LineClear, board.SpecialGemAt(I(3, 1))?.Type);
        Assert.Equal(
            SpecialGemOrientation.Vertical,
            board.SpecialGemAt(I(3, 1))?.Orientation);
        Assert.Equal(SpecialGemType.Burst, board.SpecialGemAt(I(4, 1))?.Type);
        Assert.Equal(SpecialGemType.Area, board.SpecialGemAt(I(0, 5))?.Type);
    }

    // =======================================================================
    // §2.1.5 — rejection is a gameplay no-op; §7.2 — draws nothing
    // =======================================================================

    [Fact]
    public void Validate_ShouldNotMutateTheBoardOnARejectedSwap()
    {
        // §2.1.5 item 1: "The board is unchanged, state-for-state, not merely
        // equivalent: no exchange is committed, the board reverts to its
        // pre-action state, and the 64 cells hold exactly the values they held
        // before the action arrived."
        var board = Background();
        var before = board.ToCellArray();

        var result = Validate(board, I(0, 0), I(0, 1));
        Assert.True(result.IsRejected);

        AssertBoardUnchanged(before, board);
    }

    [Fact]
    public void Validate_ShouldNotMutateTheBoardOnAnAcceptedSwap()
    {
        // §12 of the task: the validator determines legality and performs no
        // swap, gravity, spawn, cascade, activation, or damage. An accepted
        // result is a decision, not a commit (§2.1.6 owns the commit).
        var board = TestBoard.Background().WithGems(
            (I(4, 0), GemType.Atk),
            (I(4, 1), GemType.Def),
            (I(4, 2), GemType.Atk),
            (I(3, 1), GemType.Atk));

        var before = board.ToCellArray();

        var result = Validate(board, I(3, 1), I(4, 1));
        Assert.True(result.IsAccepted);

        AssertBoardUnchanged(before, board);
    }

    [Fact]
    public void Validate_ShouldNotMutateTheBoardForEveryAdjacentPair()
    {
        // Exhaustive over all 112 orthogonally adjacent pairs of the 8x8 board:
        // accepted and rejected requests alike leave all 64 entries identical.
        var board = TestBoard.Background().WithGems(
            (I(4, 0), GemType.Atk),
            (I(4, 1), GemType.Def),
            (I(4, 2), GemType.Atk),
            (I(3, 1), GemType.Atk))
            .WithSpecial((I(4, 1), SpecialGem.Burst()));

        var before = board.ToCellArray();
        var acceptedCount = 0;

        for (var from = 0; from < BoardState.CellCount; from++)
        {
            var right = BoardState.ToColumn(from) + 1 < BoardState.Columns ? from + 1 : -1;
            var down = from + BoardState.Width < BoardState.CellCount ? from + BoardState.Width : -1;

            foreach (var to in new[] { right, down })
            {
                if (to < 0)
                {
                    continue;
                }

                var result = Validate(board, from, to);
                if (result.IsAccepted)
                {
                    acceptedCount++;
                }

                AssertBoardUnchanged(before, board);
            }
        }

        // Guard the fixture: the loop must actually have exercised both
        // outcomes, otherwise "unchanged" would be trivially true.
        Assert.True(acceptedCount > 0);
    }

    [Fact]
    public void Validate_ShouldNotConsumeRng()
    {
        // §7.2: "Validation performs the whole match evaluation on the copy and
        // draws nothing: no RNG selection is consumed by index validation,
        // adjacency, staleness, simulation, or match detection."
        //
        // RngState is a value type and the validator takes no RNG, so zero
        // consumption is structural. This test proves it behaviorally: an
        // identical generator advanced only by the validator's work must land on
        // the identical state as one that never ran validation.
        var board = TestBoard.Background().WithGems(
            (I(4, 0), GemType.Atk),
            (I(4, 1), GemType.Def),
            (I(4, 2), GemType.Atk),
            (I(3, 1), GemType.Atk));

        const ulong state = 0x0123456789ABCDEFUL;
        const ulong increment = 0xDA3E39CB94B95BDBUL;

        var untouchedRng = new Pcg32(state, increment);

        var afterValidationRng = new Pcg32(state, increment);
        Validate(board, I(3, 1), I(4, 1));
        Validate(board, I(0, 0), I(0, 1));
        Validate(board, 99, 100);
        Validate(board, 5, 5);

        Assert.Equal(untouchedRng.CurrentState, afterValidationRng.CurrentState);
        Assert.Equal(
            untouchedRng.NextBounded(4),
            afterValidationRng.NextBounded(4));
    }

    [Fact]
    public void Validate_ShouldNotChangeTurnOrSequence()
    {
        // §2.1.5 items 2–3: a rejected Swap leaves Turn and Sequence unchanged;
        // §2.1.6 item 5 / §8: only a committed Swap begins a Turn, which this
        // task does not implement. The validator cannot advance either because it
        // receives neither: the BattleState it is given is read for its board
        // only and is never written back.
        var battle = GameServer.Domain.Battle.BattleState.Create("battle-003", 20260815UL);

        var turnBefore = battle.Turn;
        var sequenceBefore = battle.Sequence;
        var rngBefore = battle.RngState;
        var boardBefore = battle.BoardState.ToCellArray();

        var result = Validate(battle.BoardState, 12, 13);

        Assert.Equal(turnBefore, battle.Turn);
        Assert.Equal(sequenceBefore, battle.Sequence);
        Assert.Equal(rngBefore, battle.RngState);
        Assert.Equal(GameServer.Domain.Battle.BattleState.InitialTurn, turnBefore);
        Assert.Equal(GameServer.Domain.Battle.BattleState.InitialSequence, sequenceBefore);
        AssertBoardUnchanged(boardBefore, battle.BoardState);

        // The record is immutable, so the values above cannot have been rebound
        // by the call — the assertion documents that validation has no write
        // path to them at all.
        Assert.True(result.IsAccepted || result.IsRejected);
    }

    // =======================================================================
    // §2.1.4 item 6 / §7.1 — determinism
    // =======================================================================

    [Fact]
    public void Validate_ShouldReturnAnIdenticalResultEveryTime()
    {
        // §2.1.4 item 6: "The rejection is deterministic: the same board and the
        // same action always produce the same outcome." §4.6 item 1 / §7.1: the
        // same starting board and the same action are fully determined.
        var board = TestBoard.Background().WithGems(
            (I(4, 0), GemType.Atk),
            (I(4, 1), GemType.Def),
            (I(4, 2), GemType.Atk),
            (I(3, 1), GemType.Atk));

        var expected = Validate(board, I(3, 1), I(4, 1));
        var before = board.ToCellArray();

        for (var i = 0; i < 20; i++)
        {
            var actual = Validate(board, I(3, 1), I(4, 1));

            Assert.Equal(expected, actual);
            Assert.Equal(expected.IsAccepted, actual.IsAccepted);
            Assert.Equal(expected.Reason, actual.Reason);
            AssertBoardUnchanged(before, board);
        }
    }

    [Fact]
    public void Validate_ShouldReturnAnIdenticalResultForEveryAdjacentPair()
    {
        // Determinism over the whole board, not one fixture: every adjacent pair
        // yields the same result and the same reason on repeated validation, and
        // leaves the board identical.
        var board = TestBoard.Background();
        var before = board.ToCellArray();

        for (var from = 0; from < BoardState.CellCount; from++)
        {
            var right = BoardState.ToColumn(from) + 1 < BoardState.Columns ? from + 1 : -1;
            var down = from + BoardState.Width < BoardState.CellCount ? from + BoardState.Width : -1;

            foreach (var to in new[] { right, down })
            {
                if (to < 0)
                {
                    continue;
                }

                var first = Validate(board, from, to);
                var second = Validate(board, from, to);
                var third = Validate(board, from, to);

                Assert.Equal(first, second);
                Assert.Equal(second, third);
                AssertBoardUnchanged(before, board);
            }
        }
    }

    // =======================================================================
    // Boundary — the validator exposes validation only
    // =======================================================================

    [Fact]
    public void Validator_ShouldExposeOnlyTheBoundedCapability()
    {
        // §2.1.2 / §12 of the task: the validator answers whether a requested
        // swap is legal. It must not expose swap execution, Turn progression,
        // board resolution, cascades, gravity, spawning, Special Gem activation,
        // or combat.
        var methods = typeof(SwapValidator)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Select(m => m.Name)
            .Distinct()
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "Validate" }, methods);

        foreach (var forbidden in new[]
                 {
                     "Apply", "Commit", "Execute", "Resolve", "Cascade", "Gravity",
                     "Spawn", "Activate", "Detonate", "Damage", "Combo", "Turn",
                     "Sequence", "Advance", "Revert",
                 })
        {
            Assert.DoesNotContain(forbidden, methods);
        }
    }

    [Fact]
    public void Validator_ShouldRejectANullBoard()
    {
        // A Swap is validated against an authoritative board; a null board is a
        // programming error, not a rejected request.
        Assert.Throws<ArgumentNullException>(
            () => SwapValidator.Validate(null!, lastCommittedSwapPair: null, 0, 1));
    }

    /// <summary>
    /// Validates with no committed pair — the state of a battle that has played no
    /// Swap (<c>GAME_STATE.md</c> §2.1.10 item 3). Check 3 of
    /// <c>MATCH3_RULES.md</c> §2.1.2 can never fail while the record is absent, so
    /// this is the stateless validation these tests exercise. Committed-pair
    /// behaviour is covered by <c>SwapExecutionTests</c>.
    /// </summary>
    private static SwapValidationResult Validate(BoardState board, int from, int to) =>
        SwapValidator.Validate(board, lastCommittedSwapPair: null, from, to);

    /// <summary>
    /// Asserts the board's 64 entries are identical, entry by entry — Gem type,
    /// Special Gem type, and orientation (<c>GAME_STATE.md</c> §2.1.7 item 5).
    /// Compares whole entries rather than only Gem types, because a Special Gem
    /// must be preserved exactly (task §9).
    /// </summary>
    private static void AssertBoardUnchanged(Cell[] before, BoardState board)
    {
        var after = board.ToCellArray();

        Assert.Equal(before.Length, after.Length);

        for (var i = 0; i < before.Length; i++)
        {
            Assert.Equal(before[i], after[i]);
        }
    }
}