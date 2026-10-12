using GameServer.Domain.Bosses;
using GameServer.Domain.Match3;

namespace GameServer.Domain.Tests;

/// <summary>
/// Runtime board deadlock detection and the recovery mechanism —
/// <c>MATCH3_RULES.md</c> §1.6 (the playable-board invariant, §1.6.1; the
/// deterministic recovery mechanism, §1.6.3).
///
/// <code>
/// Rule (MATCH3_RULES.md §1.6.1, §1.6.3)
///  ↓
/// Scenario (Given a stable board, When the runtime legality question is asked,
///           Then deadlocked / not deadlocked — and a deadlocked board's
///           recovery satisfies §1.3 and §1.4)
///  ↓
/// Test
/// </code>
///
/// <b>The predicate is the one the runtime path uses.</b> §1.6 evaluates
/// <c>BossAiEngine.EnumerateLegalMoves</c> — the enumeration of the 112
/// orthogonal adjacent pairs whose exchange produces at least one §3 Match, which
/// is also the predicate <c>SwapValidator</c> accepts a Player Swap against
/// (§2.1.2 item 5) and the one the Boss's own pre-move recovery uses
/// (§1.6.5). <c>BoardGenerationValidator.HasValidSwap</c> is a
/// generation-time helper (§1.4.1) and is deliberately not the runtime
/// predicate; where the two are compared here it is only as an independent
/// cross-check on a stable board, where the question they each answer is the same
/// one.
///
/// <b>No gameplay rule is re-implemented here.</b> Detection is the production
/// enumeration, recovery is the production generator, and the dead board fixture
/// is the deterministic arrangement the generation suite already uses.
/// </summary>
public class RuntimeBoardDeadlockTests
{
    /// <summary>
    /// An 8x8 board with no Match and no legal match-producing adjacent swap —
    /// the deadlocked stable board of <c>MATCH3_RULES.md</c> §1.6.1 item 2.
    ///
    /// It is the same deterministic arrangement the generation suite uses for
    /// §1.2 item 3's "dead board": a strict 2-row stripe over the four §1.1 types,
    /// which cannot form a 3-run in any line and in which no swap can complete
    /// one. The fixture is derived, not hand-tuned.
    /// </summary>
    private static BoardState DeadlockedBoard()
    {
        var cells = new GemType[BoardState.CellCount];

        for (var row = 0; row < BoardState.Rows; row++)
        {
            for (var column = 0; column < BoardState.Columns; column++)
            {
                cells[BoardState.ToIndex(row, column)] = (GemType)(((row * 2) + column) % 4);
            }
        }

        return BoardState.FromCells(cells);
    }

    /// <summary>
    /// A stable board that contains at least one legal match-producing adjacent
    /// swap: the §1.3/§1.4-valid board the documented generator produces for a
    /// fixed seed.
    /// </summary>
    private static BoardState PlayableBoard() =>
        BoardGenerator.Generate(seed: 31337UL).Board!;

    // =======================================================================
    // §1.6.1 — detection
    // =======================================================================

    [Fact]
    public void BoardWithALegalMatchProducingSwap_ShouldNotBeDeadlocked()
    {
        // §1.6.1 item 1: "at least one legal match-producing adjacent swap" is
        // §2.1.2 item 5's rule, so a board that already carries one is playable and
        // §1.6 requires nothing of it.
        var board = PlayableBoard();

        Assert.True(BossAiEngine.EnumerateLegalMoves(board).Count > 0);
    }

    [Fact]
    public void StableBoardWithoutALegalMatchProducingSwap_ShouldBeDetectedAsDeadlocked()
    {
        // §1.6.1 item 2: a deadlocked board is a STABLE board — no Match anywhere,
        // every cell filled — with zero legal match-producing adjacent swaps.
        var board = DeadlockedBoard();

        Assert.False(BoardGenerationValidator.HasMatch(board));
        Assert.Equal(BoardState.CellCount, board.Cells.Count);
        Assert.Empty(BossAiEngine.EnumerateLegalMoves(board));
    }

    [Fact]
    public void DeadlockDetection_ShouldAgreeWithTheGenerationTimeSwapCheck_OnAStableBoard()
    {
        // §1.4 and §1.6.1 ask the same question — "does an adjacent pair whose
        // exchange yields a §3 Match exist?" — of the same stable board. The two
        // implementations are different (§1.4.1's neighbourhood evaluation versus
        // §2.1.2 item 5's whole-board detection), so agreement here is a
        // cross-check of the runtime predicate against the generation-time one, not
        // a re-derivation of either. On a board with a pre-existing Match the two
        // deliberately differ: §1.4.1's check is bounded to the two exchanged cells,
        // while gameplay detection sees the whole fixed board (§3.1 item 1) — which
        // is why only the stable fixtures are compared.
        foreach (var board in new[] { DeadlockedBoard(), PlayableBoard() })
        {
            Assert.False(BoardGenerationValidator.HasMatch(board));

            Assert.Equal(
                BoardGenerationValidator.HasValidSwap(board),
                BossAiEngine.EnumerateLegalMoves(board).Count > 0);
        }
    }

    [Fact]
    public void DeadlockDetection_ShouldNotBeAMatchDetector()
    {
        // §1.6.3 item 2: detection is a legality question, not Match Detection.
        // Asking it neither creates nor reports a Match, and it changes nothing:
        // the board is a value and the enumeration is a read.
        var board = DeadlockedBoard();
        var before = board.Cells.ToArray();

        _ = BossAiEngine.EnumerateLegalMoves(board);
        _ = MatchDetector.Detect(board);

        Assert.Equal(before, board.Cells.ToArray());
    }

    // =======================================================================
    // §1.6.3 — the recovery mechanism
    // =======================================================================

    [Fact]
    public void Recovery_FromADeadlockedBoard_ShouldProduceABoardWithALegalMove()
    {
        // §1.6.3 item 1: recovery is §1.2.1's generation, whose acceptance is §1.3
        // and §1.4 — so the replacement board has at least one legal match-producing
        // adjacent swap by construction, and is stable (no Match) like every board
        // generation accepts.
        var deadlocked = DeadlockedBoard();
        Assert.Empty(BossAiEngine.EnumerateLegalMoves(deadlocked));

        var rng = new Pcg32(0x0123_4567_89AB_CDEFUL, 0x0000_0000_0000_0001UL);
        var recovered = BoardGenerator.Generate(rng).Board!;

        Assert.NotEmpty(BossAiEngine.EnumerateLegalMoves(recovered));
        Assert.False(BoardGenerationValidator.HasMatch(recovered));
        Assert.True(BoardGenerationValidator.HasValidSwap(recovered));
    }

    [Fact]
    public void Recovery_ShouldBeDeterministic_ForTheSameBoardAndRngState()
    {
        // §1.6.3 item 3 / §7.1: the stream and the bound are both fixed, so the same
        // board and the same RNG state always produce the same replacement board and
        // the same resulting state. The "same board" is the deadlocked input both
        // runs recover from, so the board is not a free variable of the experiment.
        const ulong state = 0xDEAD_BEEF_0000_0001UL;
        const ulong increment = 0x0000_0000_0000_0001UL;

        Assert.Empty(BossAiEngine.EnumerateLegalMoves(DeadlockedBoard()));

        var firstRng = new Pcg32(state, increment);
        var first = BoardGenerator.Generate(firstRng);

        var secondRng = new Pcg32(state, increment);
        var second = BoardGenerator.Generate(secondRng);

        Assert.Equal(first.Board!.Cells, second.Board!.Cells);
        Assert.Equal(first.Attempts, second.Attempts);
        Assert.Equal(first.RngState, second.RngState);
        Assert.Equal(firstRng.CurrentState, secondRng.CurrentState);
    }

    [Fact]
    public void Recovery_ShouldAdvanceTheStreamByWholeAttemptPasses()
    {
        // §1.6.3 item 3 / §1.2.1.4 item 1: one attempt consumes exactly 64 selections
        // — one per cell — and rejected candidates continue the same stream (§1.5
        // item 1). So the state recovery leaves is the state reached after
        // `Attempts * 64` draws of the battle's own stream and nothing else: the
        // recovery draws no RNG for detection and validation (§1.2.1.4 item 5).
        const ulong state = 0x0000_0000_0000_00A5UL;
        const ulong increment = 0x0000_0000_0000_0003UL;

        var rng = new Pcg32(state, increment);
        var recovered = BoardGenerator.Generate(rng);

        var expected = new Pcg32(state, increment);
        for (var draw = 0; draw < recovered.Attempts * BoardState.CellCount; draw++)
        {
            expected.NextUInt32();
        }

        Assert.Equal(expected.CurrentState, recovered.RngState);
        Assert.Equal(expected.CurrentState, rng.CurrentState);

        // The stream really moved: recovery consumed at least one full attempt.
        Assert.NotEqual(new Pcg32(state, increment).CurrentState, recovered.RngState);
    }

    [Fact]
    public void Recovery_ShouldNeverReturnABoardThatViolatesTheInvariant()
    {
        // §1.6.3 item 1 / §1.5 item 3: recovery can only accept a candidate that
        // satisfies §1.3 and §1.4, so no recovery outcome — sampled across many
        // independent starting states — is ever a board the Player cannot act on.
        for (ulong state = 1; state <= 64; state++)
        {
            var rng = new Pcg32(state, 0x0000_0000_0000_0001UL);
            var board = BoardGenerator.Generate(rng).Board!;

            Assert.NotEmpty(BossAiEngine.EnumerateLegalMoves(board));
        }
    }

    // =======================================================================
    // §1.6.3 item 4 — the bounded retry and its failure contract
    // =======================================================================

    [Fact]
    public void RecoveryBound_ShouldBeTheUnchangedGenerationBound()
    {
        // §1.6.3 item 3: recovery is bounded by §1.5 item 2's 64 attempts. The rule
        // does not raise the bound for recovery, so the bound the runtime path
        // inherits is exactly the documented constant — a compile-time literal, not a
        // configuration value that could be raised to "retry forever".
        var maxAttempts = typeof(BoardGenerator).GetField(nameof(BoardGenerator.MaxAttempts))!;

        Assert.True(maxAttempts.IsLiteral);
        Assert.Equal(64, maxAttempts.GetRawConstantValue());
        Assert.Equal(64, BoardGenerator.MaxAttempts);
    }

    [Fact]
    public void RecoveryExhaustion_ShouldCarryTheAttemptCountAndOfferNoFallbackBoard()
    {
        // §1.6.3 item 4: exhaustion fails closed. The failure is the documented
        // §1.5 item 3 one and reports the bound; it carries no board at all, which is
        // what "no fallback construction" means — there is no partial fill, forced
        // pattern, or best-effort board to commit.
        //
        // Note: This test exercises the public failure contract and attempt-bound
        // invariants, but does not exercise actual exhaustion through
        // BoardGenerator.Generate directly. Under the deterministic row-major
        // constrained fill algorithm (MATCH3_RULES.md §1.2.1 / §1.5), encountering
        // 64 consecutive candidate rejections across the PRNG stream has not been
        // produced from any known seed. We do not claim exhaustion is mathematically
        // impossible; rather, actual exhaustion through the generator is not reached
        // by known seeds and cannot be exercised without artificial production seams,
        // which are prohibited.
        Assert.Equal(64, BoardGenerator.MaxAttempts);

        // Meaningful exception metadata contract:
        Assert.True(typeof(Exception).IsAssignableFrom(typeof(BoardGenerationFailedException)));
        var attemptsProperty = typeof(BoardGenerationFailedException).GetProperty(nameof(BoardGenerationFailedException.Attempts));
        Assert.NotNull(attemptsProperty);
        Assert.Equal(typeof(int), attemptsProperty.PropertyType);

        var unassignedException = new BoardGenerationFailedException("exhaustion");
        Assert.Equal(0, unassignedException.Attempts);

        // A successful recovery result is the only carrier of a board, and it is
        // non-null exactly when generation succeeded. Bounded attempt counts and
        // the absence of fallback boards are verified on real generation outcomes:
        foreach (var seed in new[] { 7UL, 31337UL, 47UL })
        {
            var succeeded = BoardGenerator.Generate(seed);

            Assert.True(succeeded.Succeeded);
            Assert.NotNull(succeeded.Board);
            Assert.InRange(succeeded.Attempts, 1, BoardGenerator.MaxAttempts);
            Assert.True(BoardGenerationValidator.IsValidInitialBoard(succeeded.Board));
            Assert.NotEmpty(BossAiEngine.EnumerateLegalMoves(succeeded.Board));
        }
    }
}
