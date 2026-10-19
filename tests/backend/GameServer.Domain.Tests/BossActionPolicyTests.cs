using GameServer.Domain.Bosses;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Boss one-action selection policy tests (<c>BOSS_RULES.md</c> §4.3, §4.4;
/// <c>GAME_RULES.md</c> §17 step 18; <c>ADR-029</c> §2.7; TASK-308).
///
/// The contract under test is the deterministic selection the Boss response phase
/// performs after the deadlock recovery and the legal-move enumeration
/// (<c>BOSS_RULES.md</c> §4.3 steps 1–7; <c>GAME_RULES.md</c> §17 step 18a):
///
/// <code>
/// board available only  → the board action, whatever its score
/// cast  available only  → the Boss Card cast, whatever its score
/// both available        → the higher score wins; a tie goes to the BOARD
///                         (a cast must strictly out-score the board action)
/// neither available     → no action; the Round still proceeds
/// </code>
///
/// The board score is the existing <see cref="BossMoveSelectionResult.Score"/> of
/// the selected candidate (weights ATK 10 / DEF 5 / HP 20 / POWER 10 and no tier
/// multiplier, so the smallest realistic score is <c>15</c> = 3 cells x weight 5,
/// <c>BOSS_RULES.md</c> §4.3 item 5). The cast score is the authored
/// <see cref="BossDefinition.CastPriorityScore"/>. Both are inputs this policy
/// <b>reports</b>; it derives neither, and no RNG, clock, or client input
/// participates (<c>BOSS_RULES.md</c> §4.3 items 1–6; <c>ADR-026</c> item 8;
/// <c>TDD.md</c> §6).
///
/// Invalid authored content fails closed: a cast that is available must carry a
/// non-null, non-negative authored score. <c>null</c> is the documented "not
/// authored" marker and is legal only for a Boss with no Boss Card, so both
/// <c>null</c> and a negative value are invalid for a migrated Boss. Selection
/// then throws rather than silently degrading to the board action
/// (<c>BOSS_RULES.md</c> §4.3 item 5, §4.4 item 6; <c>ADR-024</c> §2.4).
///
/// Every fixture here is a pure function call — the policy has no state, no
/// collaborators, and no board — so the five documented cases are asserted
/// exhaustively and deterministically rather than through a battle fixture.
/// </summary>
public class BossActionPolicyTests
{
    /// <summary>
    /// The smallest realistic board score: a 3-cell Match of the weight-5 Gem type
    /// (<c>BOSS_RULES.md</c> §4.3 item 5). It is the threshold the authored
    /// cast-priority score is compared against, not a policy constant.
    /// </summary>
    private const int MinimumBoardScore = 15;

    /// <summary>
    /// The approved <c>earthquake</c> cast-priority score (OD-3b): the smallest
    /// authored value that out-scores a minimum board action, as approved for
    /// TASK-308.
    /// </summary>
    private const int ApprovedCastPriorityScore = 16;

    // =======================================================================
    // 1. Exactly one action available — BOSS_RULES.md §4.3 step 2
    // =======================================================================

    [Fact]
    public void BoardOnly_ShouldSelectTheBoardAction()
    {
        // "If exactly one action is available, it is selected" — a cast that is
        // unavailable (no card, unaffordable, or on cooldown) changes nothing about
        // the board action's availability (BOSS_RULES.md §4.3 step 2).
        var selection = BossActionPolicy.Select(
            boardActionAvailable: true,
            castAvailable: false,
            boardScore: MinimumBoardScore,
            castPriorityScore: null);

        Assert.Equal(BossActionKind.BoardAction, selection.Action);

        // The board score is the selected candidate's own score, reported unchanged;
        // no cast was evaluated, so no cast score is reported (BossActionSelection).
        Assert.Equal(MinimumBoardScore, selection.BoardScore);
        Assert.Null(selection.CastScore);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 0)]
    [InlineData(15, 0)]
    [InlineData(64, 0)]
    public void BoardOnly_ShouldSelectTheBoardActionWhateverItsScore(int boardScore, int castScore)
    {
        // "Whatever its score": the sole available action is selected even when its
        // score is 0, and the cast score input is irrelevant because no cast was
        // evaluated (BOSS_RULES.md §4.3 step 2).
        var selection = BossActionPolicy.Select(
            boardActionAvailable: true,
            castAvailable: false,
            boardScore,
            castPriorityScore: castScore);

        Assert.Equal(BossActionKind.BoardAction, selection.Action);
        Assert.Equal(boardScore, selection.BoardScore);
        Assert.Null(selection.CastScore);
    }

    [Fact]
    public void CastOnly_ScoreZero_ShouldSelectTheCast()
    {
        // The required cast-only case: exactly one action is available, so it is
        // selected whatever its score — a 0 cast-priority score still selects the
        // cast when the board action is unavailable (BOSS_RULES.md §4.3 step 2).
        // The board was not evaluated, so no board score is reported.
        var selection = BossActionPolicy.Select(
            boardActionAvailable: false,
            castAvailable: true,
            boardScore: BoardScoreThatWouldOutScoreEveryCast(),
            castPriorityScore: 0);

        Assert.Equal(BossActionKind.BossCardCast, selection.Action);
        Assert.Null(selection.BoardScore);
        Assert.Equal(0, selection.CastScore);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(100)]
    public void CastOnly_ShouldSelectTheCastWhateverItsScore(int castScore)
    {
        // The cast score's magnitude is a comparison value when both actions are
        // available; on a cast-only phase it is irrelevant and the cast is selected
        // (BOSS_RULES.md §4.3 step 2). Every score is reported as the evaluated
        // authored value.
        var selection = BossActionPolicy.Select(
            boardActionAvailable: false,
            castAvailable: true,
            boardScore: BoardScoreThatWouldOutScoreEveryCast(),
            castPriorityScore: castScore);

        Assert.Equal(BossActionKind.BossCardCast, selection.Action);
        Assert.Null(selection.BoardScore);
        Assert.Equal(castScore, selection.CastScore);
    }

    // =======================================================================
    // 2. Both available — the comparison and the tie-break
    //    BOSS_RULES.md §4.3 steps 5–7, item 4
    // =======================================================================

    [Fact]
    public void BothAvailable_BoardScoreHigher_ShouldSelectTheBoardAction()
    {
        // The higher score wins (BOSS_RULES.md §4.3 step 5). Both scores were
        // evaluated, so both are reported — the cast score is not suppressed just
        // because the board action won (BossActionSelection).
        var selection = BossActionPolicy.Select(
            boardActionAvailable: true,
            castAvailable: true,
            boardScore: 30,
            castPriorityScore: ApprovedCastPriorityScore);

        Assert.Equal(BossActionKind.BoardAction, selection.Action);
        Assert.Equal(30, selection.BoardScore);
        Assert.Equal(ApprovedCastPriorityScore, selection.CastScore);
    }

    [Fact]
    public void BothAvailable_CastScoreHigher_ShouldSelectTheCast()
    {
        // The higher score wins — a cast that out-scores the selected board
        // candidate is selected (BOSS_RULES.md §4.3 step 5; ADR-029 §2.7).
        var selection = BossActionPolicy.Select(
            boardActionAvailable: true,
            castAvailable: true,
            boardScore: 20,
            castPriorityScore: 21);

        Assert.Equal(BossActionKind.BossCardCast, selection.Action);
        Assert.Equal(20, selection.BoardScore);
        Assert.Equal(21, selection.CastScore);
    }

    [Fact]
    public void BothAvailable_EqualScores_ShouldSelectTheBoardAction()
    {
        // A tie is resolved in favour of the board action: the cast must STRICTLY
        // out-score the board (BOSS_RULES.md §4.3 step 6; ADR-029 §2.7 item 20).
        var selection = BossActionPolicy.Select(
            boardActionAvailable: true,
            castAvailable: true,
            boardScore: ApprovedCastPriorityScore,
            castPriorityScore: ApprovedCastPriorityScore);

        Assert.Equal(BossActionKind.BoardAction, selection.Action);
        Assert.Equal(ApprovedCastPriorityScore, selection.BoardScore);
        Assert.Equal(ApprovedCastPriorityScore, selection.CastScore);
    }

    [Fact]
    public void BoardScore15AgainstCastScore16_ShouldSelectTheCast()
    {
        // The approved boundary in both directions: a board action scoring the
        // documented minimum (15) loses to the approved 16, for which 16 is the
        // smallest authored value that can win (BOSS_RULES.md §4.3 item 5;
        // TASK-308 OD-3b).
        var selection = BossActionPolicy.Select(
            boardActionAvailable: true,
            castAvailable: true,
            boardScore: MinimumBoardScore,
            castPriorityScore: ApprovedCastPriorityScore);

        Assert.Equal(BossActionKind.BossCardCast, selection.Action);
        Assert.Equal(MinimumBoardScore, selection.BoardScore);
        Assert.Equal(ApprovedCastPriorityScore, selection.CastScore);
    }

    [Fact]
    public void BoardScore16AgainstCastScore16_ShouldSelectTheBoardAction()
    {
        // The same values at the tie: 16 against 16 is not a win for the cast, so
        // the board action is selected and the board's own tie-break (lowest From
        // then lowest To, BOSS_RULES.md §4.3 item 4) is left to BossAiEngine.
        var selection = BossActionPolicy.Select(
            boardActionAvailable: true,
            castAvailable: true,
            boardScore: ApprovedCastPriorityScore,
            castPriorityScore: ApprovedCastPriorityScore);

        Assert.Equal(BossActionKind.BoardAction, selection.Action);
        Assert.Equal(ApprovedCastPriorityScore, selection.BoardScore);
        Assert.Equal(ApprovedCastPriorityScore, selection.CastScore);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public void CastScore0To15AgainstABoardScoreOf15_ShouldSelectTheBoardAction(int castScore)
    {
        // Authored values 0..15 always lose to a board action scoring the documented
        // minimum 15 — ties go to the board, and every value below it loses
        // (BOSS_RULES.md §4.3 item 5; ADR-029 §2.7).
        var selection = BossActionPolicy.Select(
            boardActionAvailable: true,
            castAvailable: true,
            boardScore: MinimumBoardScore,
            castPriorityScore: castScore);

        Assert.Equal(BossActionKind.BoardAction, selection.Action);
        Assert.Equal(MinimumBoardScore, selection.BoardScore);
        Assert.Equal(castScore, selection.CastScore);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void CastScoreAtLeast16AgainstABoardScoreOf15_ShouldSelectTheCast(int castScore)
    {
        // 16 is the smallest authored value that can out-score a minimum board
        // action; every larger value wins too (BOSS_RULES.md §4.3 item 5).
        var selection = BossActionPolicy.Select(
            boardActionAvailable: true,
            castAvailable: true,
            boardScore: MinimumBoardScore,
            castPriorityScore: castScore);

        Assert.Equal(BossActionKind.BossCardCast, selection.Action);
        Assert.Equal(MinimumBoardScore, selection.BoardScore);
        Assert.Equal(castScore, selection.CastScore);
    }

    // =======================================================================
    // 3. Neither action available — BOSS_RULES.md §4.3 step 1, item 4
    // =======================================================================

    [Fact]
    public void NeitherAvailable_ShouldSelectNone()
    {
        // The documented branch: no board resolution, no cast, no attack, and the
        // Round still proceeds (BOSS_RULES.md §4.3 item 4; GAME_RULES.md §517–523).
        // It is defined but not reachable in the current engine, where a successful
        // recovery guarantees a legal swap and a failed recovery throws
        // (BossAiEngine).
        var selection = BossActionPolicy.Select(
            boardActionAvailable: false,
            castAvailable: false,
            boardScore: MinimumBoardScore,
            castPriorityScore: ApprovedCastPriorityScore);

        Assert.Equal(BossActionKind.None, selection.Action);

        // No action was evaluated, so neither score is reported — the supplied
        // values are not inputs to any comparison on this branch.
        Assert.Null(selection.BoardScore);
        Assert.Null(selection.CastScore);
    }

    // =======================================================================
    // 4. Fail-closed invalid authored content
    //    BOSS_RULES.md §4.3 item 5, §4.4 item 6; ADR-024 §2.4
    // =======================================================================

    [Theory]
    [InlineData(false)] // cast-only: the cast is the sole available action
    [InlineData(true)]  // both available: the comparison would need the score
    public void CastAvailableWithNullScore_ShouldThrow(bool boardActionAvailable)
    {
        // null is the "not authored" marker and is legal only for a Boss with no
        // Boss Card. A migrated Boss whose score is absent is invalid definition
        // data: selection fails closed instead of silently degrading to the board
        // action (BOSS_RULES.md §4.3 item 5, §4.4 item 6; ADR-024 §2.4).
        Assert.Throws<InvalidOperationException>(() => BossActionPolicy.Select(
            boardActionAvailable,
            castAvailable: true,
            boardScore: MinimumBoardScore,
            castPriorityScore: null));
    }

    [Theory]
    [InlineData(false, -1)]
    [InlineData(true, -1)]
    [InlineData(false, int.MinValue)]
    [InlineData(true, int.MinValue)]
    public void CastAvailableWithNegativeScore_ShouldThrow(bool boardActionAvailable, int castScore)
    {
        // Same fail-closed rule for the other invalid value: the authored score must
        // be >= 0 whenever a cast is available (BOSS_RULES.md §4.4 item 6; ADR-024
        // §2.4), so a negative value throws in both the cast-only and both-available
        // cases.
        Assert.Throws<InvalidOperationException>(() => BossActionPolicy.Select(
            boardActionAvailable,
            castAvailable: true,
            boardScore: MinimumBoardScore,
            castPriorityScore: castScore));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void CastUnavailable_ShouldNotValidateOrReportTheCastScore(int? castPriorityScore)
    {
        // An unavailable cast is not evaluated, so its score is not read: a null (or
        // negative) value on that path is not a fail-closed case, and the board
        // action is selected normally (BOSS_RULES.md §4.3 step 2). The result
        // reports no cast score because no cast was evaluated.
        var selection = BossActionPolicy.Select(
            boardActionAvailable: true,
            castAvailable: false,
            boardScore: MinimumBoardScore,
            castPriorityScore);

        Assert.Equal(BossActionKind.BoardAction, selection.Action);
        Assert.Equal(MinimumBoardScore, selection.BoardScore);
        Assert.Null(selection.CastScore);
    }

    // =======================================================================
    // 5. The result exposes the evaluated scores — BossActionSelection
    // =======================================================================

    [Fact]
    public void Selection_ShouldExposeTheEvaluatedScores_AndOnlyThose()
    {
        // BossActionSelection's documented contract: BoardScore is the evaluated
        // board candidate's own score (null only when the board action was not
        // evaluated), and CastScore is the authored cast-priority score (null only
        // when the cast was not evaluated). Both are reported when both were
        // evaluated, whichever action won.
        var both = BossActionPolicy.Select(
            boardActionAvailable: true,
            castAvailable: true,
            boardScore: 30,
            castPriorityScore: ApprovedCastPriorityScore);

        Assert.Equal(30, both.BoardScore);
        Assert.Equal(ApprovedCastPriorityScore, both.CastScore);

        var boardOnly = BossActionPolicy.Select(
            boardActionAvailable: true,
            castAvailable: false,
            boardScore: 30,
            castPriorityScore: ApprovedCastPriorityScore);

        Assert.Equal(30, boardOnly.BoardScore);
        Assert.Null(boardOnly.CastScore);

        var castOnly = BossActionPolicy.Select(
            boardActionAvailable: false,
            castAvailable: true,
            boardScore: 30,
            castPriorityScore: 20);

        Assert.Null(castOnly.BoardScore);
        Assert.Equal(20, castOnly.CastScore);

        var neither = BossActionPolicy.Select(
            boardActionAvailable: false,
            castAvailable: false,
            boardScore: 30,
            castPriorityScore: 20);

        Assert.Null(neither.BoardScore);
        Assert.Null(neither.CastScore);
    }

    // =======================================================================
    // 6. Determinism — BOSS_RULES.md §4.3 items 1–6; ADR-026 item 8;
    //    TDD.md §6
    // =======================================================================

    [Fact]
    public void Selection_ShouldBeRepeatable_ForTheSameInputs()
    {
        // The policy is pure: no PRNG, no clock, no client input and no static
        // state, so the same arguments always produce the same selection and the
        // same reported scores (BOSS_RULES.md §4.3 items 1–6; ADR-026 item 8). The
        // five documented cases are repeated in sequence, then interleaved, to show
        // that no call depends on a previous one.
        var cases = new[]
        {
            (Board: true, Cast: false, BoardScore: MinimumBoardScore, CastScore: (int?)null),
            (Board: false, Cast: true, BoardScore: 0, CastScore: (int?)0),
            (Board: true, Cast: true, BoardScore: 30, CastScore: (int?)ApprovedCastPriorityScore),
            (Board: true, Cast: true, BoardScore: MinimumBoardScore, CastScore: (int?)ApprovedCastPriorityScore),
            (Board: false, Cast: false, BoardScore: 0, CastScore: (int?)0),
        };

        foreach (var item in cases)
        {
            var expected = BossActionPolicy.Select(
                item.Board,
                item.Cast,
                item.BoardScore,
                item.CastScore);

            for (var run = 0; run < 20; run++)
            {
                var repeated = BossActionPolicy.Select(
                    item.Board,
                    item.Cast,
                    item.BoardScore,
                    item.CastScore);

                Assert.Equal(expected, repeated);
                Assert.Equal(expected.Action, repeated.Action);
                Assert.Equal(expected.BoardScore, repeated.BoardScore);
                Assert.Equal(expected.CastScore, repeated.CastScore);
            }
        }

        // Interleaved calls do not change an earlier result either.
        var first = BossActionPolicy.Select(true, true, MinimumBoardScore, ApprovedCastPriorityScore);
        _ = BossActionPolicy.Select(false, true, 0, 0);
        _ = BossActionPolicy.Select(true, false, 30, null);
        _ = BossActionPolicy.Select(false, false, 30, 20);
        var again = BossActionPolicy.Select(true, true, MinimumBoardScore, ApprovedCastPriorityScore);

        Assert.Equal(first, again);
    }

    // ------------------------------------------------------------- helpers ---

    /// <summary>
    /// A board score larger than every cast score the fixtures use, so a cast-only
    /// fixture cannot accidentally exercise the both-available comparison branch. The
    /// board is unavailable on that path, so the supplied value is ignored and the
    /// result reports <c>null</c> for it.
    /// </summary>
    private static int BoardScoreThatWouldOutScoreEveryCast() => int.MaxValue;
}
