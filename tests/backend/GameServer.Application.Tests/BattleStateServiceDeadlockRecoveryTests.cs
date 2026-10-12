using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using Xunit;

namespace GameServer.Application.Tests;

/// <summary>
/// Commit-boundary deadlock recovery — <c>MATCH3_RULES.md</c> §1.6 (the
/// playable-board invariant), <c>GAME_RULES.md</c> §17,
/// <c>GAME_STATE.md</c> §5.1 item 8, <c>ADR-026</c> §8.
///
/// <code>
/// Rule (MATCH3_RULES.md §1.6.1–§1.6.6)
///  ↓
/// Scenario (Given a pinned board and one committed Player Swap, When the atomic
///           round commits, Then the committed board and RngState)
///  ↓
/// Test
/// </code>
///
/// <b>Every fixture is deterministic and self-verifying.</b> A scenario pins a
/// board built by <see cref="RoundTestSupport.PlayerMatch"/> and a battle
/// <c>RngState</c> fixed by a seed, so the round — including the Boss's
/// deterministic move selection, its cascades, and any recovery — is the same on
/// every run. Each scenario re-derives the pre-recovery boards from the production
/// Domain stages (<see cref="SwapExecutor"/>, <see cref="BossAiEngine"/>,
/// <see cref="CascadeResolver"/>) and asserts the deadlock precondition outright,
/// so a fixture that stopped producing the promised deadlock fails loudly instead
/// of silently weakening the test. No test hopes that a cascade deadlocks.
///
/// <b>The RNG chain is reconstructed exactly, and only where it is
/// reconstructible.</b> A non-ATK Player Swap produces no qualifying attack
/// (<c>ADR-026</c> D1), so steps 15–17 do not run and consume no RNG; the first
/// RNG draw after the Player's cascades is therefore the Boss's own cascade. The
/// fixtures that compare an <c>RngState</c> are all non-ATK and assert that
/// precondition, so the compared stream is the stream the production pipeline
/// used. The Boss's own attack draws after its cascade and never affects a
/// cascade's board.
/// </summary>
public class BattleStateServiceDeadlockRecoveryTests
{
    private static readonly BattleStateService.PetConfiguration Pet =
        new(new PetId("pet_instance_1"), Element.Hoa, new PassiveId("xich-lang"), PassiveThreshold: 5);

    private static readonly PlayerId Owner = new("player_deadlock_recovery_owner");

    /// <summary>
    /// A fixture whose Player cascade leaves a playable board and whose Boss's own
    /// board action and cascades settle into a **deadlocked** board — the case
    /// <c>MATCH3_RULES.md</c> §1.6 exists for. Established by the deterministic
    /// search recorded for this task; the scenario asserts every property it relies
    /// on.
    /// </summary>
    private const ulong PostBossDeadlockSeed = 47UL;

    /// <summary>
    /// A fixture whose Player cascade leaves a **deadlocked** board, so the Boss's
    /// existing pre-move recovery (§1.6.5) runs and the Boss can still act.
    /// </summary>
    private const ulong PreBossDeadlockSeed = 8UL;

    /// <summary>
    /// A fixture with no deadlock at any point: the Player cascade leaves a playable
    /// board, the Boss does not reshuffle, and the finished board is playable — the
    /// "no unnecessary reshuffle" scenario (§1.6.2 item 3).
    /// </summary>
    private const ulong PlayableRoundSeed = 1UL;

    /// <summary>
    /// A fixture whose Boss's board action clears an ATK Gem and whose cascades
    /// settle into a **deadlocked** board — the terminal-loss scenario (§1.6.4),
    /// where the Boss's ATK-gated instance kills a 1-HP Pet.
    /// </summary>
    private const ulong TerminalLossSeed = 352UL;

    /// <summary>
    /// A Boss a single ATK-gated Player instance destroys — the terminal-win
    /// scenario. Its other values are the MVP configuration; stats are
    /// configuration, not invariants (<c>BOSS_RULES.md</c> §6.1).
    /// </summary>
    private static BossDefinition FragileBoss(int maxHp) => BossDefinitions.HoaLong with { MaxHP = maxHp };

    /// <summary>
    /// A Boss whose own <c>GAME_RULES.md</c> §17 step 18d instance is lethal to a
    /// 1-HP Pet. The legacy fallback basic attack is retired (<c>ADR-026</c> §5), so
    /// the Boss must still clear an ATK Gem for the instance to exist at all.
    /// </summary>
    private static BossDefinition LethalBoss() =>
        BossDefinitions.HoaLong with
        {
            ATK = 100_000,
            SkillDefinition = BossDefinitions.HoaLong.SkillDefinition with { ChargeRequirement = int.MaxValue },
            PassiveDefinition = BossDefinitions.HoaLong.PassiveDefinition with { Threshold = null },
        };

    // =======================================================================
    // 1. Post-Boss deadlock — §1.6.2 item 4
    // =======================================================================

    [Fact]
    public async Task PostBossDeadlock_ShouldCommitAPlayableRecoveredBoardInTheSameWriteBack()
    {
        // §1.6.2: the Round resolves normally, the finished board is evaluated, and
        // a board the Boss's cascades left deadlocked is replaced by the documented
        // deterministic generation over the battle's own stream — with the advanced
        // RngState, in the Round's single write-back.
        var arranged = await ArrangeAsync(
            "recovery-post-boss-deadlock",
            PostBossDeadlockSeed,
            GemType.Power,
            BossDefinitions.HoaLong);

        var replay = ReplayPreCommitStages(arranged);

        // The precondition, asserted rather than assumed: the Boss's own action and
        // cascades leave a stable board with no legal match-producing swap.
        Assert.Empty(BossAiEngine.EnumerateLegalMoves(replay.Board));
        AssertBossConsumedNoCritDraw(replay);

        // What §1.6.3's mechanism must produce from exactly that stream position.
        var recoveryRng = new Pcg32(replay.RngState.State, replay.RngState.Increment);
        var expected = BoardGenerator.Generate(recoveryRng);

        var writesBefore = arranged.Store.WriteCount;

        var result = (await arranged.Service.ExecuteSwapAsync(arranged.BattleId, arranged.Pinned.Swap))!.Value;

        Assert.True(result.IsAccepted);
        Assert.False(result.IsIdempotentReplay);

        // The Round is non-terminal, so the invariant applies (GAME_RULES.md §1.4).
        Assert.DoesNotContain(result.Events, e => e.Type == BattleEventType.BattleWon);
        Assert.DoesNotContain(result.Events, e => e.Type == BattleEventType.BattleLost);

        // §1.6.3: the replacement board is generation's accepted board, and the
        // committed stream is where generation left it.
        Assert.Equal(expected.Board!.Cells, result.State.BoardState.Cells);
        Assert.Equal(recoveryRng.CurrentState, result.State.RngState);

        // §1.6.1: the committed board is playable, and it is not the deadlocked one.
        Assert.NotEmpty(BossAiEngine.EnumerateLegalMoves(result.State.BoardState));
        Assert.False(result.State.BoardState.CellsEqual(replay.Board));

        // GAME_STATE.md §5.1 item 8: the board and the RngState it consumed are the
        // record the store holds, and the Round wrote exactly once.
        Assert.Equal(result.State, (await arranged.Service.GetBattleAsync(arranged.BattleId))!);
        Assert.Equal(writesBefore + 1, arranged.Store.WriteCount);
    }

    [Fact]
    public async Task PostBossDeadlock_ShouldBeDeterministic()
    {
        // §1.6.3 item 3 / §7.1: the same fixture, board, and RNG state produce the
        // same replacement board and the same committed stream — twice.
        var first = await ArrangeAsync("recovery-determinism-a", PostBossDeadlockSeed, GemType.Power, BossDefinitions.HoaLong);
        var second = await ArrangeAsync("recovery-determinism-b", PostBossDeadlockSeed, GemType.Power, BossDefinitions.HoaLong);

        var firstResult = (await first.Service.ExecuteSwapAsync(first.BattleId, first.Pinned.Swap))!.Value;
        var secondResult = (await second.Service.ExecuteSwapAsync(second.BattleId, second.Pinned.Swap))!.Value;

        Assert.True(firstResult.IsAccepted);
        Assert.True(secondResult.IsAccepted);

        Assert.Equal(firstResult.State.BoardState.Cells, secondResult.State.BoardState.Cells);
        Assert.Equal(firstResult.State.RngState, secondResult.State.RngState);
    }

    [Fact]
    public async Task PostBossDeadlock_ShouldCommitTheRecoveryOnly_WhenTheBossDidNotAlreadyRecover()
    {
        // §1.6.5 item 2: the two recoveries cover different boards. Here the Player
        // cascade leaves a playable board, so the Boss's pre-move recovery does NOT
        // run, and the deadlock is the Boss's own cascades' doing — which is exactly
        // the case the commit-boundary check exists for.
        var arranged = await ArrangeAsync(
            "recovery-boss-not-reshuffled",
            PostBossDeadlockSeed,
            GemType.Power,
            BossDefinitions.HoaLong);

        var replay = ReplayPreCommitStages(arranged);

        Assert.False(replay.BossMove.Reshuffled);
        Assert.NotEmpty(BossAiEngine.EnumerateLegalMoves(replay.Player.State.BoardState));
        Assert.Empty(BossAiEngine.EnumerateLegalMoves(replay.Board));

        var result = (await arranged.Service.ExecuteSwapAsync(arranged.BattleId, arranged.Pinned.Swap))!.Value;

        Assert.True(result.IsAccepted);
        Assert.NotEmpty(BossAiEngine.EnumerateLegalMoves(result.State.BoardState));
    }

    // =======================================================================
    // 2. Pre-Boss deadlock — the retained Boss pre-move recovery, §1.6.5
    // =======================================================================

    [Fact]
    public async Task PreBossDeadlock_ShouldBeRecoveredByTheBossPreMovePath_AndNotAgainAtTheCommit()
    {
        // §1.6.5: the Boss's pre-move recovery is retained. The Player's cascades
        // leave a deadlocked board; the Boss reshuffles it on the same stream, selects
        // on the reshuffled board, and its cascades leave a playable board — so the
        // commit-boundary check must preserve that board and draw nothing further
        // (§1.6.2 item 3).
        var arranged = await ArrangeAsync(
            "recovery-pre-boss-deadlock",
            PreBossDeadlockSeed,
            GemType.Power,
            BossDefinitions.HoaLong);

        var replay = ReplayPreCommitStages(arranged);

        // The precondition: the board the Boss faced was deadlocked.
        Assert.Empty(BossAiEngine.EnumerateLegalMoves(replay.Player.State.BoardState));

        // The retained recovery really ran, and the board it produced is playable.
        Assert.True(replay.BossMove.Reshuffled);
        Assert.NotEmpty(BossAiEngine.EnumerateLegalMoves(replay.Board));
        Assert.False(replay.Board.CellsEqual(replay.Player.State.BoardState));
        AssertBossConsumedNoCritDraw(replay);

        var result = (await arranged.Service.ExecuteSwapAsync(arranged.BattleId, arranged.Pinned.Swap))!.Value;

        Assert.True(result.IsAccepted);

        // The Boss acted (the pre-move recovery did not skip its action), and the
        // committed board is the one the Boss's own resolution produced — not a
        // second, independent reshuffle.
        Assert.Contains(
            result.Events,
            e => e.Type == BattleEventType.SwapStarted && e.SwapStarted.Actor == DamageParty.Boss);
        Assert.Equal(replay.Board.Cells, result.State.BoardState.Cells);
        Assert.Equal(replay.RngState, result.State.RngState);
        Assert.NotEmpty(BossAiEngine.EnumerateLegalMoves(result.State.BoardState));
    }

    // =======================================================================
    // 3. No unnecessary reshuffle — §1.6.2 item 3
    // =======================================================================

    [Fact]
    public async Task PlayableRound_ShouldPreserveTheBoardAndConsumeNoRecoveryRng()
    {
        // §1.6.2 item 3: a finished board that already carries a legal match-producing
        // swap is preserved exactly. Neither the board nor the RngState may move, so
        // the committed pair is the one the existing pipeline produced — asserted
        // against the same production stages replayed from the pinned state.
        var arranged = await ArrangeAsync(
            "recovery-no-unnecessary-reshuffle",
            PlayableRoundSeed,
            GemType.Power,
            BossDefinitions.HoaLong);

        var replay = ReplayPreCommitStages(arranged);

        Assert.False(replay.BossMove.Reshuffled);
        Assert.NotEmpty(BossAiEngine.EnumerateLegalMoves(replay.Board));
        AssertBossConsumedNoCritDraw(replay);

        var result = (await arranged.Service.ExecuteSwapAsync(arranged.BattleId, arranged.Pinned.Swap))!.Value;

        Assert.True(result.IsAccepted);
        Assert.Equal(replay.Board.Cells, result.State.BoardState.Cells);
        Assert.Equal(replay.RngState, result.State.RngState);
    }

    // =======================================================================
    // 4. Terminal win — §1.6.4
    // =======================================================================

    [Fact]
    public async Task TerminalWin_AtTheBossCheck_ShouldNotChangeTheCommittedBoard()
    {
        // §1.6.4: a Round that ends at the Boss terminal check performs no recovery.
        // Its committed board is the board the Player's own resolution produced, so it
        // is the Player cascade's own board — the Boss phase never runs.
        var arranged = await ArrangeAsync(
            "recovery-terminal-win-step8",
            2UL,
            GemType.Atk,
            FragileBoss(maxHp: 1));

        var player = SwapExecutor.Execute(arranged.Before, arranged.Pinned.Swap);
        Assert.True(player.IsAccepted);

        var result = (await arranged.Service.ExecuteSwapAsync(arranged.BattleId, arranged.Pinned.Swap))!.Value;

        Assert.True(result.IsAccepted);
        Assert.Contains(result.Events, e => e.Type == BattleEventType.BattleWon);
        Assert.Equal(0, result.State.BossState.HP);
        Assert.Equal(player.State.BoardState.Cells, result.State.BoardState.Cells);
    }

    [Fact]
    public async Task TerminalWin_OnADeadlockedBoard_ShouldBeExemptFromRecovery()
    {
        // §1.6.4 item 2: a deadlocked TERMINAL board is committed as it is. The battle
        // is ended by the step 19a damage-over-time tick, which runs after the Boss
        // phase, so the finished board is the deadlocked one the Boss's cascades left —
        // and recovery must not touch it, because the Player never acts again.
        //
        // The DoT is a Burn the Pet owns (COMBAT_RULES.md §5.2, applied through the
        // Damage Pipeline at step 19a); its magnitude is lethal to the 1-HP Boss
        // whatever the same Round's HP matches healed.
        var arranged = await ArrangeAsync(
            "recovery-terminal-win-dot",
            PostBossDeadlockSeed,
            GemType.Power,
            BossDefinitions.HoaLong,
            bossHp: 1,
            bossEffect: StatusEffect.TurnBased(
                "Burn",
                StatusEffectType.DoT,
                StatusEffectSource.Player,
                magnitude: 100_000,
                duration: 3));

        var replay = ReplayPreCommitStages(arranged);
        Assert.Empty(BossAiEngine.EnumerateLegalMoves(replay.Board));

        var result = (await arranged.Service.ExecuteSwapAsync(arranged.BattleId, arranged.Pinned.Swap))!.Value;

        Assert.True(result.IsAccepted);
        Assert.Contains(result.Events, e => e.Type == BattleEventType.BattleWon);
        Assert.Equal(0, result.State.BossState.HP);

        // The deadlocked board is committed unchanged: no recovery ran, so the board is
        // still deadlocked and is exactly the pre-recovery board.
        Assert.Equal(replay.Board.Cells, result.State.BoardState.Cells);
        Assert.Empty(BossAiEngine.EnumerateLegalMoves(result.State.BoardState));
    }

    // =======================================================================
    // 5. Terminal loss — §1.6.4
    // =======================================================================

    [Fact]
    public async Task TerminalLoss_OnADeadlockedBoard_ShouldBeExemptFromRecovery()
    {
        // §1.6.4: the same exemption on the other terminal path. The Boss's ATK-gated
        // instance ends the battle at the Player terminal check, and the deadlocked
        // board the Boss's cascades left — a board the commit-boundary check would
        // otherwise replace — is committed as it is.
        var arranged = await ArrangeAsync(
            "recovery-terminal-loss",
            TerminalLossSeed,
            GemType.Power,
            LethalBoss(),
            petHp: 1);

        var replay = ReplayPreCommitStages(arranged);
        Assert.Empty(BossAiEngine.EnumerateLegalMoves(replay.Board));

        var result = (await arranged.Service.ExecuteSwapAsync(arranged.BattleId, arranged.Pinned.Swap))!.Value;

        Assert.True(result.IsAccepted);
        Assert.Contains(result.Events, e => e.Type == BattleEventType.BattleLost);
        Assert.DoesNotContain(result.Events, e => e.Type == BattleEventType.BattleWon);
        Assert.True(result.State.PetState.HP <= 0);

        Assert.Equal(replay.Board.Cells, result.State.BoardState.Cells);
        Assert.Empty(BossAiEngine.EnumerateLegalMoves(result.State.BoardState));
    }

    // =======================================================================
    // 6. Idempotent replay — ADR-025, §1.6.2
    // =======================================================================

    [Fact]
    public async Task IdempotentReplay_OfAnActionThatTriggeredRecovery_ShouldConsumeNoFurtherRng()
    {
        // ADR-025 / §1.6.2: a retry of the committed action is recognised by action
        // identity before anything is recomputed, so the reconciliation returns the
        // committed state — recovery included — and performs no second resolution, no
        // second recovery, and no write.
        var arranged = await ArrangeAsync(
            "recovery-idempotent-replay",
            PostBossDeadlockSeed,
            GemType.Power,
            BossDefinitions.HoaLong);

        var request = new SwapRequest(
            arranged.Pinned.Swap.From,
            arranged.Pinned.Swap.To,
            ExpectedSequence: arranged.Before.Sequence,
            ActionId: "client-act-recovery");

        var committed = (await arranged.Service.ExecuteSwapAsync(arranged.BattleId, request))!.Value;
        Assert.True(committed.IsAccepted);
        Assert.False(committed.IsIdempotentReplay);

        var writesAfterCommit = arranged.Store.WriteCount;

        var replay = (await arranged.Service.ExecuteSwapAsync(arranged.BattleId, request))!.Value;

        Assert.True(replay.IsIdempotentReplay);
        Assert.Equal(committed.State.BoardState.Cells, replay.State.BoardState.Cells);
        Assert.Equal(committed.State.RngState, replay.State.RngState);
        Assert.Empty(replay.Events);

        // No second write, and the authoritative record is untouched by the retry.
        Assert.Equal(writesAfterCommit, arranged.Store.WriteCount);
        Assert.Equal(committed.State, (await arranged.Service.GetBattleAsync(arranged.BattleId))!);
    }

    [Fact]
    public async Task SnapshotRecovery_ShouldReturnTheCommittedRecoveredBoard()
    {
        // ADR-008 / §1.6.6 item 2: snapshot recovery reads the committed state, which
        // already carries the recovery. No second reshuffle happens and no RNG is
        // consumed, so repeated reads are identical (GAME_STATE.md §5.3).
        var arranged = await ArrangeAsync(
            "recovery-snapshot",
            PostBossDeadlockSeed,
            GemType.Power,
            BossDefinitions.HoaLong);

        var committed = (await arranged.Service.ExecuteSwapAsync(arranged.BattleId, arranged.Pinned.Swap))!.Value;
        var writesAfterCommit = arranged.Store.WriteCount;

        var first = (await arranged.Service.GetBattleAsync(arranged.BattleId))!;
        var second = (await arranged.Service.GetBattleAsync(arranged.BattleId))!;

        Assert.Equal(committed.State, first);
        Assert.Equal(first, second);
        Assert.NotEmpty(BossAiEngine.EnumerateLegalMoves(first.BoardState));
        Assert.Equal(writesAfterCommit, arranged.Store.WriteCount);
    }

    // =======================================================================
    // 7. CAS conflict — REDIS_STATE.md §4 item 2, §1.6.2 item 1
    // =======================================================================

    [Fact]
    public async Task CASConflict_ShouldRecomputeAgainstFreshState_AndCommitTheRecoveryOnce()
    {
        // REDIS_STATE.md §4 item 2 / §4 item 6: a refused compare-and-set re-reads the
        // fresh authoritative state and re-runs the same deterministic resolution —
        // recovery included — rather than partially applying anything. The logical
        // recovery therefore happens once and is committed once.
        var arranged = await ArrangeAsync(
            "recovery-cas-conflict",
            PostBossDeadlockSeed,
            GemType.Power,
            BossDefinitions.HoaLong);

        var replay = ReplayPreCommitStages(arranged);
        Assert.Empty(BossAiEngine.EnumerateLegalMoves(replay.Board));
        AssertBossConsumedNoCritDraw(replay);

        var recoveryRng = new Pcg32(replay.RngState.State, replay.RngState.Increment);
        var expected = BoardGenerator.Generate(recoveryRng);

        // One refused write: the first attempt loses, the retry resolves against the
        // fresh record (which the double leaves at the same Sequence) and commits.
        arranged.Store.ForcedConflicts = 1;

        var writesBefore = arranged.Store.WriteCount;

        // The request states the precondition it observed, which is what lets a refused
        // write be retried against fresh state (MATCH3_RULES.md §2.1.4 item 1: the
        // legacy no-precondition form is rejected as stale instead).
        var request = new SwapRequest(
            arranged.Pinned.Swap.From,
            arranged.Pinned.Swap.To,
            ExpectedSequence: arranged.Before.Sequence);

        var result = (await arranged.Service.ExecuteSwapAsync(arranged.BattleId, request))!.Value;

        Assert.True(result.IsAccepted);
        Assert.Equal(expected.Board!.Cells, result.State.BoardState.Cells);
        Assert.Equal(recoveryRng.CurrentState, result.State.RngState);

        // Exactly one commit for the logical action: no double commit, no duplicate
        // recovery.
        Assert.Equal(writesBefore + 1, arranged.Store.WriteCount);
        Assert.Equal(result.State, (await arranged.Service.GetBattleAsync(arranged.BattleId))!);
    }

    [Fact]
    public async Task CommitThatNeverLands_ShouldPersistNoBoardOrRngMutation()
    {
        // §1.6.2 item 1 and §1.6.3 item 4: the recovered board and the advanced RNG
        // state are only ever *computed* in the resolution's working state — they
        // reach the store through the one existing compare-and-set and by no other
        // path. When that compare-and-set never lands, nothing at all is persisted:
        // the stored board and RngState remain the pre-round ones.
        //
        // This is the fail-closed accounting of the commit boundary. The generation
        // bound's own exhaustion path (§1.6.3 item 4) throws before this point is
        // reached and cannot be driven from a chosen PRNG state, so the failure
        // contract is covered directly in the Domain suite
        // (RuntimeBoardDeadlockTests) — as the generation suite already covers the
        // same unreachable path (BoardGeneratorFailureTests).
        var arranged = await ArrangeAsync(
            "recovery-commit-never-lands",
            PostBossDeadlockSeed,
            GemType.Power,
            BossDefinitions.HoaLong);

        var replay = ReplayPreCommitStages(arranged);
        Assert.Empty(BossAiEngine.EnumerateLegalMoves(replay.Board));

        var writesBefore = arranged.Store.WriteCount;

        // Nine refusals: the eight compare-and-set attempts of the documented retry
        // bound, then the resolution's own last attempt.
        arranged.Store.ForcedConflicts = 9;

        var request = new SwapRequest(
            arranged.Pinned.Swap.From,
            arranged.Pinned.Swap.To,
            ExpectedSequence: arranged.Before.Sequence);

        var result = await arranged.Service.ExecuteSwapAsync(arranged.BattleId, request);

        Assert.Null(result);

        // Nothing was written, and the record is exactly the pre-round state — the
        // deadlocked board and the un-advanced stream, not a half-applied recovery.
        Assert.Equal(writesBefore, arranged.Store.WriteCount);

        var stored = (await arranged.Service.GetBattleAsync(arranged.BattleId))!;
        Assert.Equal(arranged.Before.BoardState.Cells, stored.BoardState.Cells);
        Assert.Equal(arranged.Before.RngState, stored.RngState);
        Assert.Equal(arranged.Before.Sequence, stored.Sequence);

        // The record still holds the pinned pre-round board — the one the Round read —
        // so the pre-commit deadlock of `replay.Board` was never written anywhere and
        // the recovery's replacement board was never persisted either.
        Assert.True(stored.BoardState.CellsEqual(arranged.Before.BoardState));
    }

    // =======================================================================
    // 8. No extra events — §1.6.6
    // =======================================================================

    [Fact]
    public async Task Recovery_ShouldAddNoEventAndNoWireDiscriminator()
    {
        // §1.6.6 item 1 / GAME_EVENTS.md §3 item 8 / TASK-265 Option A: recovery is
        // server-internal and reported by the state it commits. No event is added for
        // it, no reshuffle member exists, and the closed discriminator set is
        // unchanged.
        var arranged = await ArrangeAsync(
            "recovery-no-extra-events",
            PostBossDeadlockSeed,
            GemType.Power,
            BossDefinitions.HoaLong);

        var replay = ReplayPreCommitStages(arranged);
        Assert.Empty(BossAiEngine.EnumerateLegalMoves(replay.Board));

        var result = (await arranged.Service.ExecuteSwapAsync(arranged.BattleId, arranged.Pinned.Swap))!.Value;

        Assert.True(result.IsAccepted);

        // The canonical event-name list is exactly the closed set (GAME_RULES.md §16,
        // GAME_EVENTS.md §2, SIGNALR_PROTOCOL.md §3.2.2) — no BoardReshuffled and no
        // other name was added for this operation.
        var names = Enum.GetNames<BattleEventType>().OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.Equal(
            [
                "BattleLost",
                "BattleWon",
                "BossSkillCast",
                "CardCast",
                "CascadeCreated",
                "ComboChanged",
                "DamageCalculated",
                "DamageDealt",
                "DamageTaken",
                "GemMatched",
                "MatchCreated",
                "PassiveCharged",
                "PassiveTriggered",
                "PetSkillCast",
                "PowerChanged",
                "RelicTriggered",
                "SwapResolved",
                "SwapStarted",
            ],
            names);

        Assert.DoesNotContain("BoardReshuffled", names);

        // Every delivered event is one of those names, and the Boss's board cycle is
        // reported exactly as the cascades the Boss's own resolution produced — the
        // recovery did not alter or re-report any part of the cycle.
        Assert.All(result.Events, e => Assert.Contains(e.Type.ToString(), names));

        var expectedBossBoardEvents = BattleEventBuilder.Build(replay.BossResolution, replay.BossResolution.TotalMatches);

        Assert.Equal(
            expectedBossBoardEvents.Select(e => e.Type),
            RoundTestSupport.BossBoardEvents(result).Select(e => e.Type));

        // The Round still reports exactly the two board actions the shared-board
        // contract fixes: the Player's and the Boss's (SIGNALR_PROTOCOL.md
        // §3.2.26–§3.2.27). Recovery adds no third.
        Assert.Equal(
            2,
            result.Events.Count(e => e.Type == BattleEventType.SwapStarted));
    }

    // =======================================================================
    // Fixtures and the pre-commit replay
    // =======================================================================

    /// <summary>
    /// One arranged scenario: the battle session, the store double it runs over, the
    /// pinned Player Swap, and the authoritative pre-round record.
    /// </summary>
    private sealed record Arranged(
        string BattleId,
        BattleStateService Service,
        InMemoryBattleStateRepository Store,
        PinnedPlayerRound Pinned,
        BattleState Before);

    /// <summary>
    /// The production stages' own output for the two board phases of one Round, up
    /// to — and excluding — the commit-boundary check: the Player's resolution, the
    /// Boss's deterministic move selection, and the Boss's cascade resolution. It is
    /// the value <c>EnsurePlayableBoard</c> receives, re-derived with the Domain
    /// stages the pipeline calls, and is what every deadlock precondition and
    /// "unchanged" assertion in this suite is read against.
    /// </summary>
    private sealed record PreCommitReplay(
        SwapExecutionResult Player,
        BossMoveSelectionResult BossMove,
        CascadeResolver.CascadeResult BossResolution,
        BoardState Board,
        RngState RngState);

    /// <summary>
    /// Creates a battle, pins the scenario's board and the RNG state its fixture seed
    /// fixes, and returns the record the Round will read.
    ///
    /// <b>The fixture is installed without being counted as a write of the Round
    /// under test.</b> Arranging is not part of the behaviour being asserted —
    /// <c>REDIS_STATE.md</c> §4 item 5's "one write-back per resolution" is a
    /// statement about the resolution — so the write count a scenario asserts is
    /// measured from before the Round, not from creation.
    ///
    /// <b>Why the RNG state is pinned too.</b> The pinned board decides *which* swap
    /// is legal; the stream decides what the cascades spawn into it. Both must be
    /// fixed for a scenario to be deterministic, and the search recorded for this
    /// task established the pairs used above.
    /// </summary>
    private static async Task<Arranged> ArrangeAsync(
        string battleId,
        ulong seed,
        GemType matchType,
        BossDefinition boss,
        int? bossHp = null,
        int? petHp = null,
        StatusEffect? bossEffect = null)
    {
        var store = new InMemoryBattleStateRepository();
        var service = new BattleStateService(store, new FixedRngSeedSource());

        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var rngState = Pcg32.FromSeed(seed).CurrentState;

        // The fixture resolves its own cascade against the pinned stream, so the swap
        // it returns really clears the Gem types it promises on this stream.
        var pinned = RoundTestSupport.PlayerMatch(matchType, rngState);

        var bossState = created.BossState with
        {
            HP = bossHp ?? created.BossState.HP,
            ActiveStatusEffects = bossEffect is { } effect
                ? [.. created.BossState.ActiveStatusEffects, effect]
                : created.BossState.ActiveStatusEffects,
        };

        var petState = petHp is { } hitPoints
            ? created.PetState with { HP = hitPoints }
            : created.PetState;

        store.ApplyConcurrentWrite(created with
        {
            BoardState = pinned.Board,
            RngState = rngState,
            BossState = bossState,
            PetState = petState,
        });

        var before = (await store.GetAsync(battleId))!;

        return new Arranged(battleId, service, store, pinned, before);
    }

    /// <summary>
    /// Replays the Round's two board phases from the pinned pre-round record with the
    /// production Domain stages, ending at the state the commit-boundary check
    /// receives.
    ///
    /// <b>It asserts the one property that makes the replayed stream authoritative.</b>
    /// A non-ATK Player Swap produces no qualifying attack, so steps 15–17 consume no
    /// RNG and the first draw after the Player's cascades is the Boss's own cascade —
    /// which is what makes the replayed <c>RngState</c> comparable to the committed
    /// one. A fixture that cleared an ATK Gem would need the crit draw replicated, so
    /// this helper refuses such a fixture instead of comparing a stream it did not
    /// reproduce.
    /// </summary>
    private static PreCommitReplay ReplayPreCommitStages(Arranged arranged)
    {
        var player = SwapExecutor.Execute(arranged.Before, arranged.Pinned.Swap);

        Assert.True(player.IsAccepted, "the pinned Swap must be legal on the pinned board (MATCH3_RULES.md §2.1.2)");
        Assert.Equal(0, player.Resources.ClearedGemCounts.Atk);

        var bossMove = BossAiEngine.SelectMove(
            player.State.BoardState,
            new Pcg32(player.State.RngState.State, player.State.RngState.Increment));

        // §1.6.5 / GAME_RULES.md §17 step 18a: the Boss's own pre-move recovery moves
        // the board and the stream only when it reshuffles; otherwise the stream is the
        // one the Player's phase retained.
        var bossRngState = bossMove.Reshuffled ? bossMove.RngState : player.State.RngState;

        var bossResolution = CascadeResolver.Resolve(
            bossMove.Board.WithSwapped(bossMove.SelectedMove.From, bossMove.SelectedMove.To),
            new Pcg32(bossRngState.State, bossRngState.Increment),
            swapOriginIndex: null);

        return new PreCommitReplay(player, bossMove, bossResolution, bossResolution.Board, bossResolution.RngState);
    }

    /// <summary>
    /// Asserts the second half of the stream-reconstruction precondition: the Boss's
    /// own board action cleared no ATK Gem, so its ATK-gated instance did not run and
    /// consumed no Crit draw after its cascade.
    ///
    /// <c>ADR-026</c> D1 / <c>COMBAT_RULES.md</c> §3: the draw that would break the
    /// reconstruction happens <b>after</b> the cascades and therefore never affects the
    /// board — but it does advance <c>RngState</c>, so a scenario that compares the
    /// committed stream to the replayed one must state this precondition rather than
    /// assume it. A fixture whose Boss started clearing ATK Gems fails here loudly.
    /// </summary>
    private static void AssertBossConsumedNoCritDraw(PreCommitReplay replay) =>
        Assert.Equal(0, ResourceGenerator.Generate(replay.BossResolution).ClearedGemCounts.Atk);
}
