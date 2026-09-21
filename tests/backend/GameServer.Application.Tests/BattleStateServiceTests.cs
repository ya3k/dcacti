using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Match3;
using Xunit;

namespace GameServer.Application.Tests;

/// <summary>
/// Battle State Foundation lifecycle tests (GAME_STATE.md §2.0.4).
///
/// The Application layer owns the lifecycle of the authoritative foundation
/// state: it creates it and hands it to the realtime boundary on group join. It
/// performs no gameplay calculation and defines no Turn increment rule —
/// GAME_STATE.md §2.0.2 leaves that to the task that implements action
/// resolution.
/// </summary>
public class BattleStateServiceTests
{
    [Fact]
    public void CreateBattle_ShouldProduceTheDocumentedInitialState()
    {
        var service = new BattleStateService();

        var state = service.CreateBattle("battle-1");

        Assert.Equal("battle-1", state.BattleId);
        Assert.Equal(0, state.Turn);
        Assert.Equal(0, state.Sequence);
    }

    [Fact]
    public void CreatedState_ShouldBeOwnedAndRetrievableByTheServerRuntime()
    {
        // GAME_STATE.md §2.0.4.1: Foundation State is authoritative server
        // state, produced and held server-side.
        var service = new BattleStateService();
        service.CreateBattle("battle-1");

        var retrieved = service.GetBattle("battle-1");

        Assert.NotNull(retrieved);
        Assert.Equal("battle-1", retrieved!.BattleId);
        Assert.Equal(0, retrieved.Turn);
        Assert.Equal(0, retrieved.Sequence);
    }

    [Fact]
    public void GetBattle_ForUnknownBattle_ShouldReturnNull()
    {
        var service = new BattleStateService();

        Assert.Null(service.GetBattle("never-created"));
    }

    [Fact]
    public void GetInitialStateForGroup_ShouldReturnTheCreatedFoundationState()
    {
        // SIGNALR_PROTOCOL.md §4.1: the state delivered when the client joins
        // the battle group is the current authoritative state.
        var service = new BattleStateService();
        service.CreateBattle("battle-1");

        var delivered = service.GetInitialStateForGroup("battle-1");

        Assert.NotNull(delivered);
        Assert.Equal("battle-1", delivered!.BattleId);
        Assert.Equal(0, delivered.Turn);
        Assert.Equal(0, delivered.Sequence);
    }

    [Fact]
    public void GetInitialStateForGroup_ForUnknownBattle_ShouldReturnNull()
    {
        var service = new BattleStateService();

        Assert.Null(service.GetInitialStateForGroup("never-created"));
    }

    [Fact]
    public void RepeatedReads_ShouldNotAlterTheAuthoritativeState()
    {
        // Foundation State has no resolutions (GAME_STATE.md §2.0.2), so no
        // read, join, or repeat delivery may change Turn or Sequence.
        var service = new BattleStateService();
        service.CreateBattle("battle-1");

        service.GetBattle("battle-1");
        service.GetInitialStateForGroup("battle-1");
        var second = service.GetInitialStateForGroup("battle-1");

        Assert.Equal(0, second!.Turn);
        Assert.Equal(0, second.Sequence);
        Assert.Equal(1, service.ActiveBattleCount);
    }

    [Fact]
    public void CreateBattle_ShouldTrackEachBattleIdSeparately()
    {
        var service = new BattleStateService();

        service.CreateBattle("battle-1");
        service.CreateBattle("battle-2");

        Assert.Equal("battle-1", service.GetBattle("battle-1")!.BattleId);
        Assert.Equal("battle-2", service.GetBattle("battle-2")!.BattleId);
        Assert.Equal(2, service.ActiveBattleCount);
    }

    [Fact]
    public void CreateBattle_ShouldRejectAnEmptyBattleId()
    {
        var service = new BattleStateService();

        Assert.Throws<ArgumentException>(() => service.CreateBattle(string.Empty));
    }

    [Fact]
    public void GetBattle_ShouldRejectAnEmptyBattleId()
    {
        var service = new BattleStateService();

        Assert.Throws<ArgumentException>(() => service.GetBattle(string.Empty));
    }

    // -----------------------------------------------------------------------
    // Board resolution boundary (MATCH3_RULES.md §4, ARCHITECTURE.md §4.1)
    // -----------------------------------------------------------------------

    [Fact]
    public void ResolveBoard_ShouldLeaveAStableBoardUnchangedAndConsumeNoRng()
    {
        // A generated board holds no Match (MATCH3_RULES.md §1.3), so the resolution
        // loop terminates immediately: no pass runs, nothing is removed, and Spawn —
        // the only gameplay RNG consumer (§7.2 item 1) — draws nothing.
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-stable");

        var resolved = service.ResolveBoard("battle-stable");

        Assert.NotNull(resolved);
        Assert.True(created.BoardState.CellsEqual(resolved!.BoardState));

        // §8.4: RngState advances only at Spawn. A resolution that spawned nothing
        // leaves it exactly where it was.
        Assert.Equal(created.RngState, resolved.RngState);

        // The counters are not this boundary's concern: board resolution is not a Swap
        // (MATCH3_RULES.md §8.1 item 4), so Turn and Sequence are unchanged.
        Assert.Equal(created.Turn, resolved.Turn);
        Assert.Equal(created.Sequence, resolved.Sequence);
    }

    [Fact]
    public void ResolveBoard_ShouldRecordTheResolvedStateAsAuthoritative()
    {
        // GAME_STATE.md §5.1 / §2.0.4.1: the resolved board is the authoritative state
        // the server holds and delivers, so a subsequent read returns the resolved
        // values rather than the pre-resolution ones.
        var service = new BattleStateService();
        service.CreateBattle("battle-resolved");

        var resolved = service.ResolveBoard("battle-resolved");
        var reread = service.GetBattle("battle-resolved");

        Assert.NotNull(resolved);
        Assert.NotNull(reread);
        Assert.Same(resolved, reread);
        Assert.Equal(resolved!.RngState, reread!.RngState);
        Assert.True(resolved.BoardState.CellsEqual(reread.BoardState));
    }

    [Fact]
    public void ResolveBoard_ShouldBeIdempotentOnAnAlreadyStableBoard()
    {
        // Resolving a stable board is a no-op, so resolving it again changes nothing —
        // the determinism guarantee of MATCH3_RULES.md §4.6 applied at this boundary.
        var service = new BattleStateService();
        service.CreateBattle("battle-idempotent");

        var first = service.ResolveBoard("battle-idempotent");
        var second = service.ResolveBoard("battle-idempotent");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.True(first!.BoardState.CellsEqual(second!.BoardState));
        Assert.Equal(first.RngState, second.RngState);
    }

    [Fact]
    public void ResolveBoard_ShouldNotWriteAnyBattleEventOrAddAMessage()
    {
        // GAME_EVENTS.md §1 / SIGNALR_PROTOCOL.md §8 item 7: this boundary returns state
        // and emits nothing. The service exposes no event surface at all — no
        // SpecialGemActivated, SpecialGemCreated, or SpecialGemDestroyed event, message,
        // or method may exist.
        var surface = typeof(BattleStateService)
            .GetMethods()
            .Select(m => m.Name)
            .ToArray();

        foreach (var forbidden in new[]
                 {
                     "SpecialGemActivated", "SpecialGemCreated", "SpecialGemDestroyed",
                     "EmitEvent", "PublishEvent", "RaiseEvent", "OnGemMatched",
                 })
        {
            Assert.DoesNotContain(forbidden, surface);
        }
    }

    [Fact]
    public void ResolveBoard_ShouldReturnNullForAnUnknownBattle()
    {
        // Consistent with GetBattle: an unknown battle resolves nothing rather than
        // creating state.
        var service = new BattleStateService();

        Assert.Null(service.ResolveBoard("battle-that-does-not-exist"));
    }

    [Fact]
    public void ResolveBoard_ShouldRejectAnEmptyBattleId()
    {
        var service = new BattleStateService();

        Assert.Throws<ArgumentException>(() => service.ResolveBoard(string.Empty));
    }

    [Fact]
    public void ResolvedBattle_ShouldRetainTheSeedItWasCreatedWith()
    {
        // GAME_STATE.md §2.6.1 item 3: RngSeed records the battle's origin and is never
        // rewritten. Resolution advances RngState, not the seed.
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-seed");

        var resolved = service.ResolveBoard("battle-seed");

        Assert.NotNull(resolved);
        Assert.Equal(created.RngSeed, resolved!.RngSeed);
    }

    // -----------------------------------------------------------------------
    // Swap execution boundary (MATCH3_RULES.md §2.1.6, §8.3, GAME_STATE.md §5.1)
    // -----------------------------------------------------------------------

    [Fact]
    public void ExecuteSwap_ShouldRejectEveryRequestOnAFreshGeneratedBoard()
    {
        // A generated board holds no Match (MATCH3_RULES.md §1.3) and is guaranteed at
        // least one valid swap (§1.4), so this asserts the boundary rather than a
        // particular pair: whatever the generated board is, a request that produces no
        // Match is rejected and the authoritative state is left exactly as it was.
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-swap-reject");

        var result = service.ExecuteSwap("battle-swap-reject", new SwapRequest(99, 100));

        Assert.NotNull(result);
        Assert.True(result!.Value.IsRejected);
        Assert.Equal(SwapRejectionReason.InvalidCellIndex, result.Value.Reason);

        // §2.1.5: a rejected action writes nothing — the registry still holds the
        // created state, unchanged.
        var reread = service.GetBattle("battle-swap-reject");
        Assert.Same(created, reread);
        Assert.Equal(BattleState.InitialTurn, reread!.Turn);
        Assert.Equal(BattleState.InitialSequence, reread.Sequence);
        Assert.Null(reread.LastCommittedSwapPair);
    }

    [Fact]
    public void ExecuteSwap_ShouldRejectARequestThatProducesNoMatch()
    {
        // The service performs no validation of its own: the reason is the validator's,
        // reached through the Domain executor (ARCHITECTURE.md §2.1).
        var service = new BattleStateService();
        var state = service.CreateBattle("battle-swap-nomatch");

        // Find an adjacent pair of the generated board that produces no Match. §1.4
        // guarantees at least one pair does produce one, so the complement is not
        // empty either — the loop asserts it found one rather than assuming.
        var pair = FindAdjacentPairThatProducesNoMatch(state.BoardState);

        var result = service.ExecuteSwap("battle-swap-nomatch", pair);

        Assert.NotNull(result);
        Assert.Equal(SwapRejectionReason.NoMatchFromSwap, result!.Value.Reason);
        Assert.Same(state, service.GetBattle("battle-swap-nomatch"));
    }

    [Fact]
    public void ExecuteSwap_ShouldCommitAndRecordTheResolvedState()
    {
        // §2.1.6 / GAME_STATE.md §5.1: an accepted Swap is resolved and the resulting
        // state replaces the authoritative one in a single write-back, so a subsequent
        // read returns the resolved values.
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-swap-commit");

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = service.ExecuteSwap("battle-swap-commit", pair);

        Assert.NotNull(result);
        Assert.True(result!.Value.IsAccepted);

        var reread = service.GetBattle("battle-swap-commit");

        Assert.NotNull(reread);
        Assert.True(result.Value.State.BoardState.CellsEqual(reread!.BoardState));
        Assert.Equal(result.Value.State.RngState, reread.RngState);
        Assert.Equal(result.Value.State.RngSeed, reread.RngSeed);
    }

    [Fact]
    public void ExecuteSwap_ShouldAdvanceTurnAndSequenceExactlyOnce()
    {
        // §8.1 item 1 / §8.2 item 1: one committed Swap begins one Turn and increments
        // Sequence by exactly 1, written together in the same write-back.
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-swap-counters");

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = service.ExecuteSwap("battle-swap-counters", pair);

        Assert.True(result!.Value.IsAccepted);
        Assert.Equal(created.Turn + 1, result.Value.State.Turn);
        Assert.Equal(created.Sequence + 1, result.Value.State.Sequence);
    }

    [Fact]
    public void ExecuteSwap_ShouldRecordTheCanonicalCommittedPair()
    {
        // GAME_STATE.md §2.1.10 item 5: the committed pair is written in the same
        // write-back as the counters, canonically (min, max).
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-swap-pair");

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = service.ExecuteSwap("battle-swap-pair", pair);

        Assert.True(result!.Value.IsAccepted);
        Assert.Equal(
            CommittedSwapPair.FromCells(pair.From, pair.To),
            service.GetBattle("battle-swap-pair")!.LastCommittedSwapPair);
    }

    [Fact]
    public void ExecuteSwap_ShouldRejectAReplayOfTheCommittedPairAsStale()
    {
        // §2.1.4 item 2: replaying the pair just committed — in either order — is
        // rejected as already applied, and the state the commit produced is untouched.
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-swap-stale");
        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);

        var committed = service.ExecuteSwap("battle-swap-stale", pair);
        Assert.True(committed!.Value.IsAccepted);
        var afterCommit = service.GetBattle("battle-swap-stale");

        foreach (var replay in new[]
                 {
                     pair,
                     new SwapRequest(pair.To, pair.From),
                 })
        {
            var result = service.ExecuteSwap("battle-swap-stale", replay);

            Assert.True(result!.Value.IsRejected);
            Assert.Equal(SwapRejectionReason.StaleAction, result.Value.Reason);

            // §2.1.5: unchanged — including the record the rejection matched.
            Assert.Same(afterCommit, service.GetBattle("battle-swap-stale"));
        }
    }

    [Fact]
    public void ExecuteSwap_ShouldReturnNullForAnUnknownBattle()
    {
        // Consistent with GetBattle and ResolveBoard: an unknown battle resolves
        // nothing rather than creating state.
        var service = new BattleStateService();

        Assert.Null(service.ExecuteSwap("battle-that-does-not-exist", new SwapRequest(0, 1)));
    }

    [Fact]
    public void ExecuteSwap_ShouldRejectAnEmptyBattleId()
    {
        var service = new BattleStateService();

        Assert.Throws<ArgumentException>(
            () => service.ExecuteSwap(string.Empty, new SwapRequest(0, 1)));
    }

    [Fact]
    public void ExecuteSwap_ShouldLeaveTheBoardStableAndConsistentWithTheResolution()
    {
        // §4.3 item 1: the committed board is stable — a fresh detection pass finds
        // nothing — and the counters describe that finished resolution.
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-swap-stable");

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = service.ExecuteSwap("battle-swap-stable", pair);

        Assert.True(result!.Value.IsAccepted);
        Assert.Empty(MatchDetector.Detect(result.Value.State.BoardState));
        Assert.Equal(result.Value.Resolution.RngState, result.Value.State.RngState);
    }

    // -----------------------------------------------------------------------
    // Match / Combo accounting boundary (GAME_STATE.md §2.2, §5.1;
    // MATCH3_RULES.md §6)
    // -----------------------------------------------------------------------

    [Fact]
    public void CreateBattle_ShouldStartPlayerStateAtZero()
    {
        // GAME_STATE.md §2.2: MatchCount = 0 and Combo = 0 for a battle that has
        // resolved no action.
        var service = new BattleStateService();

        var state = service.CreateBattle("battle-progression-initial");

        Assert.Equal(PlayerState.Initial, state.PlayerState);
        Assert.Equal(0, state.PlayerState.MatchCount);
        Assert.Equal(0, state.PlayerState.Combo);
    }

    [Fact]
    public void ExecuteSwap_ShouldRecordTheResolutionMatchAndComboValues()
    {
        // GAME_STATE.md §2.2 / §5.1 item 3: the recorded state carries the finished
        // resolution's values, so a subsequent read returns them — not a value
        // derived from the board afterwards.
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-progression-commit");

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = service.ExecuteSwap("battle-progression-commit", pair);

        Assert.True(result!.Value.IsAccepted);

        var matches = result.Value.Resolution.Passes.Sum(pass => pass.Matches.Count);

        Assert.True(matches >= 1);
        Assert.Equal(matches, result.Value.State.PlayerState.Combo);
        Assert.Equal(matches, result.Value.State.PlayerState.MatchCount);

        // The registry holds the same value the result reports.
        var reread = service.GetBattle("battle-progression-commit");
        Assert.Equal(result.Value.State.PlayerState, reread!.PlayerState);
    }

    [Fact]
    public void ExecuteSwap_ShouldLeavePlayerStateUnchangedOnEveryRejection()
    {
        // MATCH3_RULES.md §2.1.5 item 5 / §6.1 item 3: a rejected Swap resets
        // nothing and increments nothing. The registry keeps the state object it
        // already held, so the progression values cannot have moved.
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-progression-rejected");

        var result = service.ExecuteSwap("battle-progression-rejected", new SwapRequest(99, 100));

        Assert.True(result!.Value.IsRejected);

        var reread = service.GetBattle("battle-progression-rejected");
        Assert.Same(created, reread);
        Assert.Equal(PlayerState.Initial, reread!.PlayerState);
        Assert.Equal(0, reread.PlayerState.MatchCount);
        Assert.Equal(0, reread.PlayerState.Combo);
    }

    [Fact]
    public void ResolveBoard_ShouldNotChangeTheProgressionState()
    {
        // Board resolution is not a Swap (MATCH3_RULES.md §8.1 item 4): it resolves no
        // action, so it starts no Turn, counts no Match, and resets no Combo. A
        // generated board holds no Match, so nothing resolves at all.
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-progression-resolution");

        var resolved = service.ResolveBoard("battle-progression-resolution");

        Assert.NotNull(resolved);
        Assert.Equal(created.PlayerState, resolved!.PlayerState);
    }

    [Fact]
    public void ExecuteSwap_ShouldNotWriteAnyBattleEventOrAddAMessage()
    {
        // GAME_EVENTS.md §1 / SIGNALR_PROTOCOL.md §8 item 7: this boundary returns
        // state and emits nothing. The service exposes no event surface at all.
        var surface = typeof(BattleStateService)
            .GetMethods()
            .Select(m => m.Name)
            .ToArray();

        foreach (var forbidden in new[]
                 {
                     "SwapStarted", "SwapResolved", "MatchCreated", "MatchResolved",
                     "CascadeCreated", "ComboChanged", "GemMatched", "PowerChanged",
                     "EmitEvent", "PublishEvent", "RaiseEvent",
                 })
        {
            Assert.DoesNotContain(forbidden, surface);
        }
    }

    [Fact]
    public void ExecuteSwap_ShouldNotPersistAnything()
    {
        // REDIS_STATE.md §7 item 8: the Match-3 resolution stage adds no key and no
        // persistence. The service holds no repository and touches no store — the
        // process-local registry is the same staged, safe-to-lose boundary it already
        // had, and it gained no field for the resolution.
        var fields = typeof(BattleStateService)
            .GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Select(f => f.Name)
            .ToArray();

        Assert.Equal(["_battles", "_seedSource"], fields.OrderBy(n => n, StringComparer.Ordinal));
    }

    /// <summary>
    /// An adjacent pair of the board whose exchange produces a §3 Match — guaranteed
    /// to exist by <c>MATCH3_RULES.md</c> §1.4, and asserted found rather than assumed.
    /// </summary>
    private static SwapRequest FindAdjacentPairThatProducesAMatch(BoardState board)
    {
        foreach (var (from, to) in AllAdjacentPairs())
        {
            if (MatchDetector.Detect(board.WithSwapped(from, to)).Count > 0)
            {
                return new SwapRequest(from, to);
            }
        }

        throw new InvalidOperationException(
            "A generated board has at least one valid Swap (MATCH3_RULES.md §1.4).");
    }

    /// <summary>
    /// An adjacent pair of the board whose exchange produces no §3 Match — the
    /// complement of the pairs §1.4 guarantees.
    /// </summary>
    private static SwapRequest FindAdjacentPairThatProducesNoMatch(BoardState board)
    {
        foreach (var (from, to) in AllAdjacentPairs())
        {
            if (MatchDetector.Detect(board.WithSwapped(from, to)).Count == 0)
            {
                return new SwapRequest(from, to);
            }
        }

        throw new InvalidOperationException(
            "Expected at least one adjacent pair that produces no Match.");
    }

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
}