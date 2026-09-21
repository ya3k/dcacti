using GameServer.Domain.Battle;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Swap execution tests (<c>MATCH3_RULES.md</c> §2.1.6, §8.3,
/// <c>GAME_STATE.md</c> §2.1.10, §5.1).
///
/// The execution flow under test is the documented one:
///
/// <code>
/// SwapRequest → SwapValidator (§2.1.2, four checks)
///            → commit the exchange (whole Cell entries, §2.1.6 step 4)
///            → resolve the board to stability (§2.1.6 step 8, §4)
///            → one write-back: Turn, Sequence, LastCommittedSwapPair
///              (GAME_STATE.md §5.1, §8.3)
/// </code>
///
/// Boards are written through <see cref="TestBoard"/> so a scenario reads as the
/// board the documentation describes: the character at row <c>r</c>, column
/// <c>c</c> is cell index <c>r * 8 + c</c> (<c>MATCH3_RULES.md</c> §1.0). The
/// expected outcome comes from the rule, never from the implementation.
///
/// Resolution coverage already exists in TASK-002's tests
/// (<c>CascadeAndDeterminismTests</c>, the Special Gem suites). This file adds
/// only what proves <c>Swap → Resolution → Stable Board</c> is correctly
/// connected, and it does not restate resolution rules.
/// </summary>
public class SwapExecutionTests
{
    private const ulong TestSeed = 20260815UL;

    private static BoardState Background() => TestBoard.Background();

    private static int I(int row, int column) => TestBoard.I(row, column);

    // The canonical fixture for "a swap that completes a match". Row 4 holds
    // 'A','D','A' at columns 0-2 and (3,1) holds the 'A' directly above the 'D',
    // so exchanging (3,1)<->(4,1) makes row 4 'AAA' — the §3 horizontal Match.
    // Neither cell is part of a run before the swap.
    private const int From = 25; // I(3, 1)
    private const int To = 33;   // I(4, 1)

    private static BoardState MatchingBoard() =>
        Background().WithGems(
            (I(4, 0), GemType.Atk),
            (I(4, 1), GemType.Def),
            (I(4, 2), GemType.Atk),
            (I(3, 1), GemType.Atk));

    /// <summary>A battle whose board is the completing-swap fixture.</summary>
    private static BattleState BattleWith(BoardState board, CommittedSwapPair? committed = null) =>
        BattleState.CreateWith("battle-004", TestSeed) with
        {
            BoardState = board,
            LastCommittedSwapPair = committed,
        };

    // =======================================================================
    // Validation integration — the four §2.1.2 checks reach the executor
    // =======================================================================

    [Fact]
    public void Execute_ShouldAcceptAValidSwap()
    {
        // §2 item 3 / §2.1.2 item 5: an adjacent swap producing at least one Match
        // is committed. The completion is the whole requirement — no tier, no
        // minimum count.
        var result = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(From, To));

        Assert.True(result.IsAccepted);
        Assert.Equal(SwapRejectionReason.None, result.Reason);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(64)]
    [InlineData(int.MaxValue)]
    public void Execute_ShouldRejectAnInvalidCellIndex(int index)
    {
        // §2.1.2 item 1: a value outside 0..63 is not a cell. Check 1, so neither
        // adjacency nor match legality is reached.
        var result = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(index, To));

        Assert.True(result.IsRejected);
        Assert.Equal(SwapRejectionReason.InvalidCellIndex, result.Reason);
    }

    [Fact]
    public void Execute_ShouldRejectASwapOfACellWithItself()
    {
        // §2.1.2 item 2: a swap of a cell with itself is not a Swap.
        var result = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(From, From));

        Assert.Equal(SwapRejectionReason.InvalidCellIndex, result.Reason);
    }

    [Theory]
    [InlineData(0, 9)]    // diagonal
    [InlineData(9, 18)]   // diagonal
    [InlineData(7, 8)]    // differs by 1 but wraps a row edge (§2.1.3 item 1)
    [InlineData(0, 2)]    // distant
    public void Execute_ShouldRejectANonAdjacentPair(int first, int second)
    {
        // §2.1.2 item 3: diagonal, distant, and row-wrap pairs are all INVALID_SWAP.
        var result = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(first, second));

        Assert.Equal(SwapRejectionReason.InvalidSwap, result.Reason);
    }

    [Fact]
    public void Execute_ShouldRejectASwapThatProducesNoMatch()
    {
        // §2.1.2 item 5: an adjacent swap producing no §3 Match is rejected and the
        // board reverts. Check 4 is the last one, so this also proves the earlier
        // three passed.
        var board = Background();

        Assert.Empty(MatchDetector.Detect(board));

        var result = SwapExecutor.Execute(BattleWith(board), new SwapRequest(I(0, 0), I(0, 1)));

        Assert.Equal(SwapRejectionReason.NoMatchFromSwap, result.Reason);
    }

    [Fact]
    public void Execute_ShouldRejectAStaleAction()
    {
        // §2.1.2 item 4 / §2.1.4 item 2: the action's unordered pair is the pair
        // most recently committed to the board.
        var state = BattleWith(MatchingBoard(), CommittedSwapPair.FromCells(From, To));

        var result = SwapExecutor.Execute(state, new SwapRequest(From, To));

        Assert.True(result.IsRejected);
        Assert.Equal(SwapRejectionReason.StaleAction, result.Reason);
    }

    [Fact]
    public void Execute_ShouldDecideTheChecksInTheDocumentedOrder()
    {
        // §2.1.2: "checks are evaluated in the order above so that two
        // implementations reject the same action with the same reason when more
        // than one check could fail."
        //
        // A non-adjacent pair that is ALSO the committed pair reports the adjacency
        // reason, not staleness: check 2 precedes check 4. And a stale pair that
        // would also produce no match reports STALE_ACTION, not NO_MATCH_FROM_SWAP:
        // check 4 precedes check 5.
        var state = BattleWith(MatchingBoard(), CommittedSwapPair.FromCells(0, 2));

        // (0,2) is non-adjacent and is the committed pair → check 2 wins.
        Assert.Equal(
            SwapRejectionReason.InvalidSwap,
            SwapExecutor.Execute(state, new SwapRequest(0, 2)).Reason);

        // (From,To) is adjacent, produces a real Match, and is the committed pair
        // → checks 1–3 pass and check 4 rejects it.
        Assert.Equal(
            SwapRejectionReason.StaleAction,
            SwapExecutor.Execute(
                BattleWith(MatchingBoard(), CommittedSwapPair.FromCells(From, To)),
                new SwapRequest(From, To)).Reason);

        // (0,0)/(0,1) is adjacent and produces no match, and is not committed
        // → check 5 rejects it.
        Assert.Equal(
            SwapRejectionReason.NoMatchFromSwap,
            SwapExecutor.Execute(BattleWith(Background()), new SwapRequest(I(0, 0), I(0, 1))).Reason);
    }

    [Fact]
    public void Execute_ShouldTreatTheRequestOverloadAndTheIndexesIdentically()
    {
        // The request is the pair and nothing else (§2.1.1 item 3): the result
        // cannot depend on how the two cells were spelled.
        var state = BattleWith(MatchingBoard());

        var forward = SwapExecutor.Execute(state, new SwapRequest(From, To));
        var reversed = SwapExecutor.Execute(state, new SwapRequest(To, From));

        Assert.Equal(forward.IsAccepted, reversed.IsAccepted);
        Assert.Equal(forward.Reason, reversed.Reason);
        Assert.True(forward.State.BoardState.CellsEqual(reversed.State.BoardState));
        Assert.Equal(forward.State.LastCommittedSwapPair, reversed.State.LastCommittedSwapPair);
    }

    [Fact]
    public void Execute_ShouldRejectANullState()
    {
        // A Swap is executed against an authoritative battle state; there is no
        // state-less execution.
        Assert.Throws<ArgumentNullException>(
            () => SwapExecutor.Execute(null!, new SwapRequest(From, To)));
    }

    // =======================================================================
    // Successful swap — the complete Cell exchange
    // =======================================================================

    [Fact]
    public void Execute_ShouldExchangeTheTwoCompleteCells()
    {
        // §2.1.6 step 4 / GAME_STATE.md §2.1.5 item 2: a cell entry is exchanged as
        // a whole. The task's example — Cell[12] = POWER + Burst, Cell[13] =
        // ATK + null → after the swap Cell[12] = ATK + null, Cell[13] = POWER +
        // Burst — asserted on the pre-resolution exchange, which is the only board
        // the swap itself determines.
        var board = Background().WithGems((12, GemType.Power), (13, GemType.Atk))
            .WithSpecial((12, SpecialGem.Burst()));

        var exchanged = board.WithSwapped(12, 13);

        Assert.Equal(GemType.Atk, exchanged[12]);
        Assert.Null(exchanged.SpecialGemAt(12));

        Assert.Equal(GemType.Power, exchanged[13]);
        Assert.Equal(SpecialGem.Burst(), exchanged.SpecialGemAt(13));
    }

    [Fact]
    public void Execute_ShouldExchangeThroughTheDocumentedBoardPrimitive()
    {
        // §7 of the task: the swap uses the existing authoritative board API rather
        // than reconstructing cells. The executed exchange must therefore equal
        // BoardState.WithSwapped applied to the same board — the single primitive,
        // not a second copy of it.
        var board = MatchingBoard();

        var expected = board.WithSwapped(From, To);
        var result = SwapExecutor.Execute(BattleWith(board), new SwapRequest(From, To));

        // The resolution then ran, so the final board is the resolved one. The
        // identity asserted here is that the executor's own first-pass board is
        // exactly the primitive's output: resolving either produces the same state.
        var viaPrimitive = CascadeResolver.Resolve(
            expected,
            new Pcg32(
                BattleState.CreateWith("battle-004", TestSeed).RngState.State,
                BattleState.CreateWith("battle-004", TestSeed).RngState.Increment),
            swapOriginIndex: null);

        Assert.True(result.State.BoardState.CellsEqual(viaPrimitive.Board));
        Assert.Equal(viaPrimitive.RngState, result.State.RngState);
    }

    [Fact]
    public void Execute_ShouldMoveASpecialGemWithItsCell()
    {
        // GAME_STATE.md §2.1.5 item 2 / MATCH3_RULES.md §2.1.5: the Special Gem
        // travels with its cell, keeping its type, and is not activated merely
        // because it was swapped.
        var board = MatchingBoard().WithSpecial(
            (From, SpecialGem.LineClearVertical()),
            (To, SpecialGem.Burst()));

        var exchanged = board.WithSwapped(From, To);

        // Each gem arrived at the other cell, intact.
        Assert.Equal(SpecialGem.Burst(), exchanged.SpecialGemAt(From));
        Assert.Equal(SpecialGem.LineClearVertical(), exchanged.SpecialGemAt(To));

        // And still well formed: orientation present exactly for LineClear
        // (GAME_STATE.md §2.1.4 item 2).
        Assert.True(exchanged.SpecialGemAt(From)!.Value.IsWellFormed);
        Assert.True(exchanged.SpecialGemAt(To)!.Value.IsWellFormed);
    }

    [Theory]
    [InlineData(SpecialGemType.LineClear)]
    [InlineData(SpecialGemType.Burst)]
    [InlineData(SpecialGemType.Area)]
    public void Execute_ShouldPreserveEverySpecialGemTypeAndOrientationThroughASwap(
        SpecialGemType type)
    {
        // §7 of the task: GemType, SpecialGem, SpecialGem.Type, and
        // SpecialGem.Orientation all survive the swap. Every orientation of both
        // LineClear variants is covered, plus the two orientation-free types.
        foreach (var gem in GemsOf(type))
        {
            var board = MatchingBoard().WithSpecial((From, gem));

            var exchanged = board.WithSwapped(From, To);

            // The cell's Gem type travelled with the Special Gem's metadata, and the
            // whole entry equality below is the complete statement of it.
            Assert.Equal(board[From], exchanged[To]);
            Assert.Equal(gem, exchanged.SpecialGemAt(To));
            Assert.Null(exchanged.SpecialGemAt(From));

            // The whole entry moved: equality is on the record, which is GemType +
            // SpecialGem together (GAME_STATE.md §2.1.1).
            Assert.Equal(board.Cells[From], exchanged.Cells[To]);
            Assert.Equal(board.Cells[To], exchanged.Cells[From]);
        }
    }

    [Fact]
    public void Execute_ShouldLeaveEveryUnrelatedCellCorrect()
    {
        // Only the two named cells change. The other 62 entries are the ones they
        // were, entry for entry.
        var board = MatchingBoard().WithSpecial((5, SpecialGem.Area()), (60, SpecialGem.Burst()));
        var before = board.ToCellArray();

        var exchanged = board.WithSwapped(From, To);
        var after = exchanged.ToCellArray();

        for (var i = 0; i < BoardState.CellCount; i++)
        {
            if (i == From || i == To)
            {
                continue;
            }

            Assert.Equal(before[i], after[i]);
        }
    }

    [Fact]
    public void Execute_ShouldNotActivateASpecialGemMerelyBySwappingIt()
    {
        // The task's explicit rule: "No Special Gem is activated merely because it
        // was swapped." Activation is a Match-consumption act (§5.5.5 item 1), so a
        // swap that moves a Special Gem into a non-matching cell leaves it on the
        // board rather than consuming it.
        var board = Background().WithSpecial((I(0, 0), SpecialGem.Burst()));
        var board2 = board.WithGems((I(0, 0), GemType.Power), (I(0, 1), GemType.Atk));

        // (0,0)<->(0,1): no Match is produced, so nothing resolves at all.
        var result = SwapExecutor.Execute(BattleWith(board2), new SwapRequest(I(0, 0), I(0, 1)));

        Assert.True(result.IsRejected);
        Assert.Equal(SwapRejectionReason.NoMatchFromSwap, result.Reason);
    }

    // =======================================================================
    // State — LastCommittedSwapPair, Turn, Sequence
    // =======================================================================

    [Fact]
    public void Execute_ShouldRecordTheCanonicalCommittedPair()
    {
        // GAME_STATE.md §2.1.10 item 5 / the task's example: Swap(13, 12) records
        // (12, 13). The record is canonical (min, max), so the request's order is
        // not the stored identity (§2.1.10 item 2).
        var result = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(33, 25));

        Assert.Equal(new CommittedSwapPair(25, 33), result.State.LastCommittedSwapPair);
        Assert.Equal(25, result.State.LastCommittedSwapPair!.Value.MinCellIndex);
        Assert.Equal(33, result.State.LastCommittedSwapPair.Value.MaxCellIndex);
    }

    [Fact]
    public void Execute_ShouldRecordTheSamePairForEitherRequestOrder()
    {
        // §2.1.1 item 2: the unordered pair {from, to} is the swap's identity.
        var forward = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(From, To));
        var reversed = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(To, From));

        Assert.Equal(forward.State.LastCommittedSwapPair, reversed.State.LastCommittedSwapPair);
    }

    [Fact]
    public void Execute_ShouldTurnTheInitialAbsentPairIntoACommit()
    {
        // §2.1.10 item 3: before the first committed Swap the record is absent — no
        // sentinel pair stands in for it. The first commit replaces absence with the
        // pair.
        var state = BattleWith(MatchingBoard());

        Assert.Null(state.LastCommittedSwapPair);

        var result = SwapExecutor.Execute(state, new SwapRequest(From, To));

        Assert.NotNull(result.State.LastCommittedSwapPair);
    }

    [Fact]
    public void Execute_ShouldReplaceThePreviouslyCommittedPair()
    {
        // §2.1.10 item 7: applying a NEW pair replaces the stored value. Only the
        // most recent commit is kept (§2.1.4 item 3: no per-Turn queue).
        var state = BattleWith(MatchingBoard(), CommittedSwapPair.FromCells(0, 1));

        var result = SwapExecutor.Execute(state, new SwapRequest(From, To));

        Assert.Equal(new CommittedSwapPair(From, To), result.State.LastCommittedSwapPair);
    }

    [Fact]
    public void Execute_ShouldBeginExactlyOneTurn()
    {
        // §8.1 item 1 / §2 item 5: a committed Swap always begins exactly one new
        // Turn — never one per Match, per pass, or per Cascade. The stored value
        // advances by exactly 1, in the same write-back as the counters.
        var state = BattleWith(MatchingBoard());

        Assert.Equal(BattleState.InitialTurn, state.Turn);

        var result = SwapExecutor.Execute(state, new SwapRequest(From, To));

        Assert.Equal(state.Turn + 1, result.State.Turn);
    }

    [Fact]
    public void Execute_ShouldIncrementSequenceExactlyOnce()
    {
        // §8.2 item 1: one successfully resolved action increments Sequence by
        // exactly 1 — not per Match, per pass, per Cascade, per Special Gem, or per
        // removed cell.
        var state = BattleWith(MatchingBoard());

        var result = SwapExecutor.Execute(state, new SwapRequest(From, To));

        Assert.Equal(state.Sequence + 1, result.State.Sequence);
    }

    [Fact]
    public void Execute_ShouldIncrementSequenceOncePerSwapAcrossRepeatedCommittedSwaps()
    {
        // The counters count actions (§8.2 item 1), so repeated committed Swaps give
        // exactly one increment each — whatever their resolutions contained. Each
        // swap below completes a fresh Match on the board the previous one left, and
        // no pair repeats, so none is stale (§2.1.4 item 3).
        var current = BattleWith(MatchingBoard());
        var committed = 0;

        // A board that always holds a completable pair: two cells of one type with a
        // neighbour the swap can complete the run with. Rows 0-7 are otherwise the
        // match-free ADHP rotation, so each completed Match is the swap's own.
        foreach (var row in new[] { 1, 2, 3, 4, 5, 6 })
        {
            // In row `row`, columns 0 and 2 hold 'A' with 'D' between them, and the
            // cell above the 'D' holds the completing 'A': swapping vertically
            // completes 'AAA' across columns 0-2.
            var board = current.BoardState
                .WithGems(
                    (I(row, 0), GemType.Atk),
                    (I(row, 1), GemType.Def),
                    (I(row, 2), GemType.Atk),
                    (I(row - 1, 1), GemType.Atk));

            var state = current with { BoardState = board };
            var request = new SwapRequest(I(row - 1, 1), I(row, 1));

            // Guard the fixture: the pair must still be valid on this turn's board.
            var result = SwapExecutor.Execute(state, request);

            Assert.True(result.IsAccepted, $"row {row}: expected the completing swap to be accepted");
            committed++;
            current = result.State;

            // Exactly one increment per committed swap.
            Assert.Equal(committed, current.Turn);
            Assert.Equal(committed, current.Sequence);
        }

        Assert.Equal(6, committed);
    }

    [Fact]
    public void Execute_ShouldWriteTheResolvedBoardRngCountersAndRecordTogether()
    {
        // §5.1 item 2 / §8.3: nothing is written mid-resolution. The single result
        // carries the stable board, the retained RngState, the counters, and the
        // commit record — no partial state in which the board is swapped but the
        // record is old, or the record is new but the board is unresolved.
        var state = BattleWith(MatchingBoard());

        var result = SwapExecutor.Execute(state, new SwapRequest(From, To));
        var committed = result.State;

        // The board is the resolution's final board, and it is stable.
        Assert.True(committed.BoardState.CellsEqual(result.Resolution.Board));
        Assert.Empty(MatchDetector.Detect(committed.BoardState));

        // The RngState is the one the resolution's Spawn step produced.
        Assert.Equal(result.Resolution.RngState, committed.RngState);

        // Every authoritative field moved together, and the seed did not.
        Assert.Equal(state.RngSeed, committed.RngSeed);
        Assert.Equal(state.Turn + 1, committed.Turn);
        Assert.Equal(state.Sequence + 1, committed.Sequence);
        Assert.Equal(
            CommittedSwapPair.FromCells(From, To),
            committed.LastCommittedSwapPair);
    }

    [Fact]
    public void Execute_ShouldNotMutateTheInputState()
    {
        // BattleState and BoardState are immutable, so the state passed in cannot
        // be rebound by the call — asserted so the contract is explicit.
        var state = BattleWith(MatchingBoard());
        var before = state.BoardState.ToCellArray();

        SwapExecutor.Execute(state, new SwapRequest(From, To));

        Assert.Equal(BattleState.InitialTurn, state.Turn);
        Assert.Equal(BattleState.InitialSequence, state.Sequence);
        Assert.Null(state.LastCommittedSwapPair);

        var after = state.BoardState.ToCellArray();
        Assert.Equal(before.Length, after.Length);
        for (var i = 0; i < before.Length; i++)
        {
            Assert.Equal(before[i], after[i]);
        }
    }

    [Fact]
    public void Execute_ShouldRejectWithNoStateAccessible()
    {
        // A rejection carries no state, so a caller cannot mistake one for a commit
        // that produced an empty board. MATCH3_RULES.md §2.1.5: a rejected action
        // writes nothing.
        var result = SwapExecutor.Execute(BattleWith(Background()), new SwapRequest(I(0, 0), I(0, 1)));

        Assert.True(result.IsRejected);
        Assert.Throws<InvalidOperationException>(() => result.State);
        Assert.Throws<InvalidOperationException>(() => result.Resolution);
    }

    // =======================================================================
    // Invalid request — every reference leaves state unchanged
    // =======================================================================

    [Fact]
    public void Execute_ShouldLeaveEveryAuthoritativeFieldUnchangedOnRejection()
    {
        // §2.1.5 items 1–5: the board, Turn, Sequence, RngState, RngSeed, and
        // LastCommittedSwapPair are all unchanged for every rejection reason — the
        // rejection is a gameplay no-op, not a resolution that failed.
        // The committed pair must itself be an adjacent pair, otherwise the stale request
        // would be rejected by the adjacency check first (§2.1.2: checks are evaluated
        // in order, so check 2 precedes check 4). (0,1) is adjacent and is kept clear
        // of the fixtures below.
        var state = BattleWith(MatchingBoard(), CommittedSwapPair.FromCells(0, 1));
        var boardBefore = state.BoardState.ToCellArray();

        // Each request fails exactly one check.
        var rejections = new (SwapRequest Request, SwapRejectionReason Reason)[]
        {
            (new SwapRequest(99, To), SwapRejectionReason.InvalidCellIndex),
            (new SwapRequest(From, From), SwapRejectionReason.InvalidCellIndex),
            (new SwapRequest(13, 15), SwapRejectionReason.InvalidSwap),
            (new SwapRequest(0, 9), SwapRejectionReason.InvalidSwap),
            (new SwapRequest(5, 6), SwapRejectionReason.NoMatchFromSwap),
            (new SwapRequest(0, 1), SwapRejectionReason.StaleAction),
            (new SwapRequest(1, 0), SwapRejectionReason.StaleAction),
        };

        foreach (var (request, expectedReason) in rejections)
        {
            var result = SwapExecutor.Execute(state, request);

            // The documented reason, decided by the validator.
            Assert.True(result.IsRejected);
            Assert.Equal(expectedReason, result.Reason);

            // And the input state is untouched: the executor returns no state at
            // all, so there is nothing that could have changed.
            Assert.Equal(BattleState.InitialTurn, state.Turn);
            Assert.Equal(BattleState.InitialSequence, state.Sequence);
            Assert.Equal(BattleState.CreateWith("battle-004", TestSeed).RngState, state.RngState);
            Assert.Equal(TestSeed, state.RngSeed);
            Assert.Equal(new CommittedSwapPair(0, 1), state.LastCommittedSwapPair);

            var boardAfter = state.BoardState.ToCellArray();
            Assert.Equal(boardBefore.Length, boardAfter.Length);
            for (var i = 0; i < boardBefore.Length; i++)
            {
                Assert.Equal(boardBefore[i], boardAfter[i]);
            }
        }
    }

    [Fact]
    public void Execute_ShouldNotClearTheCommittedPairOnARejection()
    {
        // §2.1.10 items 6–7: a rejection neither records a pair nor clears one — a
        // rejection can never make an earlier commit forgettable. The stale request
        // that just matched the record is the sharpest case.
        var committed = CommittedSwapPair.FromCells(From, To);
        var state = BattleWith(MatchingBoard(), committed);

        SwapExecutor.Execute(state, new SwapRequest(From, To));
        SwapExecutor.Execute(state, new SwapRequest(To, From));
        SwapExecutor.Execute(state, new SwapRequest(0, 9));

        Assert.Equal(committed, state.LastCommittedSwapPair);

        // And the commit is still recognised as already applied afterwards.
        Assert.Equal(
            SwapRejectionReason.StaleAction,
            SwapExecutor.Execute(state, new SwapRequest(From, To)).Reason);
    }

    [Fact]
    public void Execute_ShouldNotAdvanceTurnOrSequenceOnARejection()
    {
        // §8.1 item 3 / §8.2 item 3: out-of-range, non-adjacent, already-applied,
        // and no-match-producing actions all leave the counters unchanged.
        var state = BattleWith(MatchingBoard(), CommittedSwapPair.FromCells(0, 1));

        var turned = new[]
        {
            new SwapRequest(70, 71),
            new SwapRequest(From, From),
            new SwapRequest(7, 8),
            new SwapRequest(I(0, 0), I(0, 1)),
            new SwapRequest(0, 1),
        };

        foreach (var request in turned)
        {
            var result = SwapExecutor.Execute(state, request);

            Assert.True(result.IsRejected);
            Assert.Equal(BattleState.InitialTurn, state.Turn);
            Assert.Equal(BattleState.InitialSequence, state.Sequence);
        }
    }

    [Fact]
    public void Execute_ShouldLetADifferentPairStillBeValidAfterACommit()
    {
        // §2.1.4 item 3: a Swap naming a pair that is not the most recently
        // committed pair is validated only against the current board. With (0,1)
        // committed, the completing pair is not stale.
        var state = BattleWith(MatchingBoard(), CommittedSwapPair.FromCells(0, 1));

        var result = SwapExecutor.Execute(state, new SwapRequest(From, To));

        Assert.True(result.IsAccepted);
    }

    // =======================================================================
    // Stale action — the canonical unordered pair
    // =======================================================================

    [Fact]
    public void StaleAction_ShouldRejectBothSpellingsOfTheCommittedPair()
    {
        // The task's example: with LastCommittedSwapPair = (12,13),
        // request (12,13) and request (13,12) are both STALE_ACTION — they are the
        // same unordered pair (§2.1.1 item 2, GAME_STATE.md §2.1.10 item 2).
        var state = BattleWith(
            Background().WithGems((12, GemType.Power), (13, GemType.Atk)),
            CommittedSwapPair.FromCells(12, 13));

        Assert.Equal(SwapRejectionReason.StaleAction, SwapExecutor.Execute(state, new SwapRequest(12, 13)).Reason);
        Assert.Equal(SwapRejectionReason.StaleAction, SwapExecutor.Execute(state, new SwapRequest(13, 12)).Reason);
    }

    [Fact]
    public void StaleAction_ShouldLeaveTheWholeStateUnchanged()
    {
        // "Board unchanged, Turn unchanged, Sequence unchanged,
        // LastCommittedSwapPair unchanged" — the task's explicit minimum.
        var committed = CommittedSwapPair.FromCells(12, 13);
        var state = BattleWith(
            Background().WithGems((12, GemType.Power), (13, GemType.Atk)),
            committed);

        var boardBefore = state.BoardState.ToCellArray();
        var turnBefore = state.Turn;
        var sequenceBefore = state.Sequence;

        foreach (var request in new[] { new SwapRequest(12, 13), new SwapRequest(13, 12) })
        {
            var result = SwapExecutor.Execute(state, request);

            Assert.Equal(SwapRejectionReason.StaleAction, result.Reason);
            Assert.Equal(turnBefore, state.Turn);
            Assert.Equal(sequenceBefore, state.Sequence);
            Assert.Equal(committed, state.LastCommittedSwapPair);

            var boardAfter = state.BoardState.ToCellArray();
            for (var i = 0; i < boardBefore.Length; i++)
            {
                Assert.Equal(boardBefore[i], boardAfter[i]);
            }
        }
    }

    [Fact]
    public void StaleAction_ShouldNotApplyToADifferentPair()
    {
        // The task's example: with (12,13) committed, request (13,14) is not stale.
        // (13,14) is adjacent and produces no match on the background board, so the
        // reason reported is the match one — which proves staleness did not fire.
        var state = BattleWith(Background(), CommittedSwapPair.FromCells(12, 13));

        var result = SwapExecutor.Execute(state, new SwapRequest(13, 14));

        Assert.NotEqual(SwapRejectionReason.StaleAction, result.Reason);
        Assert.Equal(SwapRejectionReason.NoMatchFromSwap, result.Reason);
    }

    [Fact]
    public void StaleAction_ShouldNeverFireOnAFreshBattle()
    {
        // GAME_STATE.md §2.1.10 item 4: while the record is absent, check 3 can
        // never fail. Every adjacent pair of a fresh battle is therefore never
        // stale.
        var state = BattleWith(Background());

        Assert.Null(state.LastCommittedSwapPair);

        foreach (var (from, to) in AllAdjacentPairs())
        {
            var reason = SwapExecutor.Execute(state, new SwapRequest(from, to)).Reason;

            Assert.NotEqual(SwapRejectionReason.StaleAction, reason);
        }
    }

    [Fact]
    public void StaleAction_ShouldBeDetectedAgainstTheBoardTheCommitProduced()
    {
        // The end-to-end shape: a committed Swap records its pair, and immediately
        // replaying that same pair — in either order — is rejected as already
        // applied, without touching the state the commit produced.
        var first = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(From, To));

        Assert.True(first.IsAccepted);
        var committed = first.State;

        foreach (var request in new[] { new SwapRequest(From, To), new SwapRequest(To, From) })
        {
            var replay = SwapExecutor.Execute(committed, request);

            Assert.True(replay.IsRejected);
            Assert.Equal(SwapRejectionReason.StaleAction, replay.Reason);
        }
    }

    [Fact]
    public void StaleAction_ShouldBeDeterministic()
    {
        // §2.1.4 item 6: the same board and the same action always produce the same
        // outcome.
        var state = BattleWith(MatchingBoard(), CommittedSwapPair.FromCells(From, To));

        for (var attempt = 0; attempt < 20; attempt++)
        {
            Assert.Equal(
                SwapRejectionReason.StaleAction,
                SwapExecutor.Execute(state, new SwapRequest(From, To)).Reason);
            Assert.Equal(
                SwapRejectionReason.StaleAction,
                SwapExecutor.Execute(state, new SwapRequest(To, From)).Reason);
        }
    }

    [Fact]
    public void StaleAction_ShouldBeServerAuthoritativeAndNotClientSupplied()
    {
        // §2.1.4 items 1 and 5, SIGNALR_PROTOCOL.md §2 item 1: clientSequence is an
        // opaque correlation id, not the staleness source, and §2.1.1 item 3 forbids
        // the request carrying a gameplay field. The request type therefore has
        // exactly the two cells, so a client cannot declare an action already
        // committed.
        Assert.Equal(
            ["From", "To"],
            typeof(SwapRequest).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));

        // The executor reads the record from the state it is given, and nothing
        // else: its input is the state plus the request.
        var parameters = typeof(SwapExecutor)
            .GetMethod(nameof(SwapExecutor.Execute))!
            .GetParameters()
            .Select(p => p.ParameterType)
            .ToArray();

        Assert.Equal([typeof(BattleState), typeof(SwapRequest)], parameters);
    }

    // =======================================================================
    // Resolution integration — Swap → Resolution → Stable Board
    // =======================================================================

    [Fact]
    public void Execute_ShouldReachTheExistingResolutionPipeline()
    {
        // The connecting assertion: a valid swap which produces a Match reaches the
        // existing board-resolution pipeline, so the final board is the cascade
        // loop's own output for the exchanged board — not a second implementation's.
        var board = MatchingBoard();
        var state = BattleWith(board);

        var result = SwapExecutor.Execute(state, new SwapRequest(From, To));

        var expected = CascadeResolver.Resolve(
            board.WithSwapped(From, To),
            new Pcg32(state.RngState.State, state.RngState.Increment),
            swapOriginIndex: null);

        Assert.True(result.Resolution.Passes.Count > 0);
        Assert.True(result.State.BoardState.CellsEqual(expected.Board));
        Assert.Equal(expected.RngState, result.State.RngState);
        Assert.Equal(expected.TotalMatches, result.Resolution.TotalMatches);
        Assert.Equal(expected.CascadeDepth, result.Resolution.CascadeDepth);
    }

    [Fact]
    public void Execute_ShouldProduceAStableFullBoard()
    {
        // §4.3 item 1 / item 3: the loop ends when a detection pass produces no
        // matches, and the board it ends on holds exactly one Gem per cell. That
        // board is what the commit publishes.
        var result = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(From, To));

        Assert.Empty(MatchDetector.Detect(result.State.BoardState));
        Assert.Equal(BoardState.CellCount, result.State.BoardState.Cells.Count);
        Assert.All(result.State.BoardState.Cells, c => Assert.True(GemTypes.IsValid((int)c.GemType)));
    }

    [Fact]
    public void Execute_ShouldProduceAStableBoardForEveryAcceptedSwapOnARealBoard()
    {
        // Exhaustive over every adjacent pair of a generated board: whichever pairs
        // the validator accepts, the committed board is stable and the counters moved
        // exactly once. This is the integration guarantee stated over the whole
        // board rather than one fixture.
        var state = BattleState.CreateWith("battle-004-exhaustive", TestSeed);
        var accepted = 0;

        foreach (var (from, to) in AllAdjacentPairs())
        {
            var result = SwapExecutor.Execute(state, new SwapRequest(from, to));

            if (result.IsRejected)
            {
                continue;
            }

            accepted++;

            Assert.Empty(MatchDetector.Detect(result.State.BoardState));
            Assert.Equal(state.Turn + 1, result.State.Turn);
            Assert.Equal(state.Sequence + 1, result.State.Sequence);
            Assert.Equal(CommittedSwapPair.FromCells(from, to), result.State.LastCommittedSwapPair);
        }

        Assert.True(accepted > 0, "a generated board has at least one valid Swap (MATCH3_RULES.md §1.4)");
    }

    [Fact]
    public void Execute_ShouldCarryTheSpecialGemsTheResolutionCreatedOntoTheCommittedBoard()
    {
        // The Special Gem the resolution creates reaches the committed board, since
        // the commit publishes the resolution's own board (GAME_STATE.md §2.1.7
        // item 8: generation creates none, resolution does). Row 4 holds 'A','D','A'
        // at columns 0-2 and (3,1) the completing 'A': the swap makes a Match 3, so
        // the fixture proves the board — not a specific gem tier — is delivered.
        var result = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(From, To));

        Assert.True(result.IsAccepted);

        // Whatever Special Gems the resolution produced, each is well formed and
        // lives in the committed board's own cell entry (§2.1.4 item 2).
        for (var i = 0; i < BoardState.CellCount; i++)
        {
            if (result.State.BoardState.SpecialGemAt(i) is { } gem)
            {
                Assert.True(gem.IsWellFormed, $"malformed Special Gem at {i}: {gem}");
            }
        }
    }

    [Fact]
    public void Execute_ShouldResolveASwapThatCreatesALineClearGem()
    {
        // A Match 4 fixture, so the resolution genuinely creates a Special Gem and
        // the committed board carries it. Row 5 holds 'P','P','D','P' at columns
        // 0-3 and (4,2) holds the completing 'P'.
        var board = Background().WithGems(
            (I(5, 0), GemType.Power),
            (I(5, 1), GemType.Power),
            (I(5, 2), GemType.Def),
            (I(5, 3), GemType.Power),
            (I(4, 2), GemType.Power));

        Assert.Empty(MatchDetector.Detect(board));

        var result = SwapExecutor.Execute(BattleWith(board), new SwapRequest(I(4, 2), I(5, 2)));

        Assert.True(result.IsAccepted);

        // The first pass detected the Match 4 and created exactly one Line Clear Gem
        // (§5.2 item 1, §5.5.2).
        var created = result.Resolution.Passes[0].CreatedSpecialGems;
        Assert.Single(created);
        Assert.Equal(SpecialGemType.LineClear, created[0].SpecialGem.Type);
        Assert.NotNull(created[0].SpecialGem.Orientation);

        // It survived gravity and spawn onto the committed board (§5.5.4 item 2).
        var committedSpecials = Enumerable.Range(0, BoardState.CellCount)
            .Where(i => result.State.BoardState.SpecialGemAt(i) is not null)
            .ToArray();

        Assert.Contains(
            committedSpecials,
            i => result.State.BoardState.SpecialGemAt(i)!.Value.Type == SpecialGemType.LineClear);
    }

    // =======================================================================
    // RNG — validation and exchange draw nothing; only resolution may
    // =======================================================================

    [Fact]
    public void Execute_ShouldNotConsumeRngForARejectedSwap()
    {
        // §2.1.5 item 4 / §7.2: a rejected action advances the PRNG by exactly zero
        // selections — no RNG selection is consumed by index validation, adjacency,
        // staleness, simulation, or match detection.
        var state = BattleWith(Background());
        var untouched = new Pcg32(state.RngState.State, state.RngState.Increment);

        SwapExecutor.Execute(state, new SwapRequest(99, 100));
        SwapExecutor.Execute(state, new SwapRequest(5, 5));
        SwapExecutor.Execute(state, new SwapRequest(0, 9));
        SwapExecutor.Execute(state, new SwapRequest(I(0, 0), I(0, 1)));

        Assert.Equal(untouched.CurrentState, state.RngState);
        Assert.Equal(untouched.NextBounded(4), new Pcg32(state.RngState.State, state.RngState.Increment).NextBounded(4));
    }

    [Fact]
    public void Execute_ShouldNotReseedAndShouldAdvanceOnlyThroughResolution()
    {
        // §7.2 item 1 / ADR-009: Spawn is the only gameplay operation that consumes
        // randomness, at one selection per spawned cell. RngSeed records the battle's
        // origin and is never rewritten (§2.6.1 item 3).
        var state = BattleWith(MatchingBoard());

        var result = SwapExecutor.Execute(state, new SwapRequest(From, To));

        Assert.Equal(state.RngSeed, result.State.RngSeed);

        // The committed RngState is exactly what the resolution produced — no extra
        // draw, and no draw the resolution did not make.
        Assert.Equal(result.Resolution.RngState, result.State.RngState);

        // And it is the same value a fresh generator reaches by replaying the same
        // deterministic resolution (§4.6 item 1).
        var replay = new Pcg32(state.RngState.State, state.RngState.Increment);
        var replayed = CascadeResolver.Resolve(
            state.BoardState.WithSwapped(From, To),
            replay,
            swapOriginIndex: null);

        Assert.Equal(replayed.RngState, result.State.RngState);
    }

    [Fact]
    public void Execute_ShouldBeEntirelyDeterministic()
    {
        // §4.6 / §7.1: the same state and the same request always produce the same
        // committed state, the same counters, the same record, and the same RNG.
        var state = BattleWith(MatchingBoard());
        var reference = SwapExecutor.Execute(state, new SwapRequest(From, To));

        for (var run = 0; run < 20; run++)
        {
            var again = SwapExecutor.Execute(state, new SwapRequest(From, To));

            Assert.Equal(reference.IsAccepted, again.IsAccepted);
            Assert.Equal(reference.Reason, again.Reason);
            Assert.True(reference.State.BoardState.CellsEqual(again.State.BoardState));
            Assert.Equal(reference.State.RngState, again.State.RngState);
            Assert.Equal(reference.State.Turn, again.State.Turn);
            Assert.Equal(reference.State.Sequence, again.State.Sequence);
            Assert.Equal(reference.State.LastCommittedSwapPair, again.State.LastCommittedSwapPair);
        }
    }

    // =======================================================================
    // Boundary — the executor resolves one Swap and nothing else
    // =======================================================================

    [Fact]
    public void Executor_ShouldExposeOnlyTheBoundedCapability()
    {
        // This type owns the swap transition. It must not expose a general battle
        // turn system, combat, a second sequence counter, or a persistence hook.
        var methods = typeof(SwapExecutor)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Select(m => m.Name)
            .Distinct()
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Execute"], methods);
    }

    // =======================================================================
    // §5 wire codes — the documented spellings, not cased identifiers
    // =======================================================================

    [Fact]
    public void RejectionCodes_ShouldBeExactlyTheDocumentedFour()
    {
        // SIGNALR_PROTOCOL.md §5 item 3 owns the codes and defers to
        // MATCH3_RULES.md §2.1.2 for the set, so the mapping is asserted against the
        // documented spellings rather than against the enum's own member names.
        Assert.Equal("INVALID_CELL_INDEX", SwapRejectionCodes.ToContractCode(SwapRejectionReason.InvalidCellIndex));
        Assert.Equal("INVALID_SWAP", SwapRejectionCodes.ToContractCode(SwapRejectionReason.InvalidSwap));
        Assert.Equal("NO_MATCH_FROM_SWAP", SwapRejectionCodes.ToContractCode(SwapRejectionReason.NoMatchFromSwap));
        Assert.Equal("STALE_ACTION", SwapRejectionCodes.ToContractCode(SwapRejectionReason.StaleAction));
    }

    [Fact]
    public void RejectionCodes_ShouldCoverEveryRejectionReason()
    {
        // Every reason that can accompany a rejection has a code, and None — the
        // accepted result's reason — is not a rejection and has none.
        foreach (var reason in Enum.GetValues<SwapRejectionReason>())
        {
            if (reason == SwapRejectionReason.None)
            {
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => SwapRejectionCodes.ToContractCode(reason));
                continue;
            }

            var code = SwapRejectionCodes.ToContractCode(reason);

            Assert.False(string.IsNullOrWhiteSpace(code), $"{reason} has no contract code");
            Assert.Equal(code, code.ToUpperInvariant());
            Assert.DoesNotContain(' ', code);
        }
    }

    [Fact]
    public void RejectionCodes_ShouldNotBeAMechanicalTranslationOfTheMemberName()
    {
        // The reason this mapping exists is that the C# member names are not the wire
        // codes: casing the identifier would produce STALEACTION, a code the protocol
        // never defines.
        Assert.NotEqual(
            SwapRejectionReason.StaleAction.ToString().ToUpperInvariant(),
            SwapRejectionCodes.ToContractCode(SwapRejectionReason.StaleAction));
        Assert.NotEqual(
            SwapRejectionReason.InvalidCellIndex.ToString().ToUpperInvariant(),
            SwapRejectionCodes.ToContractCode(SwapRejectionReason.InvalidCellIndex));
    }

    // =======================================================================
    // Result composition — SwapExecutionResult.WithEvents / WithState
    // (GAME_RULES.md §17 step 10, GAME_EVENTS.md §1.1, §3 item 6)
    // =======================================================================

    [Fact]
    public void WithEvents_ShouldReturnANewResultCarryingTheSuppliedEvents()
    {
        // The pipeline step that charges the Passive (GAME_RULES.md §17 step 10)
        // appends its reports to the board resolution's list. The method is a pure
        // factory: it replaces the event list and changes nothing else.
        var committed = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(From, To));
        Assert.True(committed.IsAccepted);

        var replacement = new[]
        {
            BattleEvent.ForPassiveCharged(new PassiveChargedEvent(new PassiveId("xich-lang"), 1, 5)),
        };

        var updated = committed.WithEvents(replacement);

        // The supplied list is the result's event list.
        Assert.Equal(replacement.Length, updated.Events.Count);
        Assert.Equal(BattleEventType.PassiveCharged, updated.Events[0].Type);
        Assert.Equal(1, updated.Events[0].PassiveCharged.Progress);

        // Every other member is preserved: acceptance, reason, state, and resolution.
        Assert.Equal(committed.IsAccepted, updated.IsAccepted);
        Assert.Equal(committed.Reason, updated.Reason);
        Assert.Same(committed.State, updated.State);
        Assert.Equal(committed.Resolution, updated.Resolution);
    }

    [Fact]
    public void WithEvents_ShouldNotMutateTheOriginalResult()
    {
        // The original is an immutable value: composing the Passive stage's events
        // onto it must leave the board resolution's own list intact, so a caller
        // holding the pre-charge result still sees exactly what the executor
        // produced (GAME_EVENTS.md §3 item 6: events are outputs, not state).
        var committed = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(From, To));
        var originalEvents = committed.Events.ToArray();

        _ = committed.WithEvents([]);

        Assert.Equal(originalEvents.Length, committed.Events.Count);
        Assert.Equal(originalEvents, committed.Events);
    }

    [Fact]
    public void WithEvents_ShouldRejectARejectionBecauseItHasNoEventListToReplace()
    {
        // MATCH3_RULES.md §2.1.5 item 6 / GAME_EVENTS.md §1.2: a rejected action emits
        // no Battle Event at all, so there is nothing to replace — and the empty list
        // it carries is the contract, not a gap.
        var rejected = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(0, 1));

        Assert.True(rejected.IsRejected);
        Assert.Empty(rejected.Events);
        Assert.Throws<InvalidOperationException>(() => rejected.WithEvents([]));
    }

    [Fact]
    public void WithEvents_ShouldRejectANullList()
    {
        // The absence of events is the empty list, never null.
        var committed = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(From, To));

        Assert.Throws<ArgumentNullException>(() => committed.WithEvents(null!));
    }

    [Fact]
    public void WithState_ShouldReturnANewResultCarryingTheSuppliedState()
    {
        // GAME_STATE.md §5.1: the state and the events a result carries must belong to
        // the SAME single post-resolution write-back. The pipeline step that extends
        // it therefore replaces the state too, and WithState is that pure factory.
        var committed = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(From, To));

        var extended = committed.State with
        {
            PetState = committed.State.PetState with
            {
                PassiveProgress = new PassiveProgress(
                    committed.State.PetState.PassiveProgress.Threshold,
                    Current: 3),
            },
        };

        var updated = committed.WithState(extended);

        Assert.Same(extended, updated.State);
        Assert.Equal(3, updated.State.PetState.PassiveProgress.Current);

        // Every other member is preserved, including the original event list.
        Assert.Equal(committed.IsAccepted, updated.IsAccepted);
        Assert.Equal(committed.Reason, updated.Reason);
        Assert.Equal(committed.Resolution, updated.Resolution);
        Assert.Equal(committed.Events, updated.Events);
    }

    [Fact]
    public void WithState_ShouldRejectARejectionBecauseItHasNoStateToReplace()
    {
        // MATCH3_RULES.md §2.1.5: a rejected action writes nothing, so a rejection has
        // no resulting state and the caller keeps the one it passed in.
        var rejected = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(0, 1));

        Assert.Throws<InvalidOperationException>(() => rejected.WithState(BattleWith(MatchingBoard())));
    }

    [Fact]
    public void WithState_ShouldRejectANullState()
    {
        var committed = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(From, To));

        Assert.Throws<ArgumentNullException>(() => committed.WithState(null!));
    }

    // ------------------------------------------------------------- helpers ---

    /// <summary>
    /// Every orthogonally adjacent pair of the 8x8 board, each once — the 112
    /// pairs a player can name (<c>MATCH3_RULES.md</c> §1.0).
    /// </summary>
    private static IEnumerable<(int From, int To)> AllAdjacentPairs()
    {
        for (var index = 0; index < BoardState.CellCount; index++)
        {
            var right = BoardState.ToColumn(index) + 1 < BoardState.Columns ? index + 1 : -1;
            var down = index + BoardState.Width < BoardState.CellCount ? index + BoardState.Width : -1;

            if (right >= 0)
            {
                yield return (index, right);
            }

            if (down >= 0)
            {
                yield return (index, down);
            }
        }
    }

    /// <summary>Every well-formed <see cref="SpecialGem"/> of one type.</summary>
    private static IEnumerable<SpecialGem> GemsOf(SpecialGemType type) => type switch
    {
        SpecialGemType.LineClear =>
        [
            SpecialGem.LineClearHorizontal(),
            SpecialGem.LineClearVertical(),
        ],
        SpecialGemType.Burst => [SpecialGem.Burst()],
        SpecialGemType.Area => [SpecialGem.Area()],
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "unknown Special Gem type"),
    };
}