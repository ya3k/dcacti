using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
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
    /// <summary>
    /// The Pet the tests' battles carry.
    ///
    /// Pet selection and progression are not implemented (GAME_STATE.md §2.3,
    /// SIGNALR_PROTOCOL.md §4.3 item 2), so a battle's Pet is supplied by its
    /// caller. This is the configuration those calls pass: Xích Lang's MVP Element
    /// (ELEMENT_RULES.md §6 — Hỏa), its Passive identity, and the Threshold, with
    /// no Reset Behavior override — which is PASSIVE_RULES.md §4 item 1's default
    /// and the behavior of all five MVP Pet Passives (§8).
    /// </summary>
    private static readonly BattleStateService.PetConfiguration PetConfiguration =
        new(Element.Hoa, new PassiveId("xich-lang"), PassiveThreshold: 5);

    /// <summary>
    /// The Boss the tests' battles are fought against — Hỏa Long, an MVP Boss of
    /// BOSS_RULES.md §6.1 (Hỏa, 5000 / 100 / 50).
    /// </summary>
    private static readonly BossDefinition BossDefinition = BossDefinitions.HoaLong;

    [Fact]
    public void CreateBattle_ShouldProduceTheDocumentedInitialState()
    {
        var service = new BattleStateService();
        var state = service.CreateBattle("battle-1", PetConfiguration, BossDefinition);

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
        service.CreateBattle("battle-1", PetConfiguration, BossDefinition);

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
        service.CreateBattle("battle-1", PetConfiguration, BossDefinition);

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
        service.CreateBattle("battle-1", PetConfiguration, BossDefinition);

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

        service.CreateBattle("battle-1", PetConfiguration, BossDefinition);
        service.CreateBattle("battle-2", PetConfiguration, BossDefinition);

        Assert.Equal("battle-1", service.GetBattle("battle-1")!.BattleId);
        Assert.Equal("battle-2", service.GetBattle("battle-2")!.BattleId);
        Assert.Equal(2, service.ActiveBattleCount);
    }

    [Fact]
    public void CreateBattle_ShouldRejectAnEmptyBattleId()
    {
        var service = new BattleStateService();

        Assert.Throws<ArgumentException>(() => service.CreateBattle(string.Empty, PetConfiguration, BossDefinition));
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
        var created = service.CreateBattle("battle-stable", PetConfiguration, BossDefinition);

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
        service.CreateBattle("battle-resolved", PetConfiguration, BossDefinition);

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
        service.CreateBattle("battle-idempotent", PetConfiguration, BossDefinition);

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
        var created = service.CreateBattle("battle-seed", PetConfiguration, BossDefinition);

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
        var created = service.CreateBattle("battle-swap-reject", PetConfiguration, BossDefinition);

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
        var state = service.CreateBattle("battle-swap-nomatch", PetConfiguration, BossDefinition);

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
        var created = service.CreateBattle("battle-swap-commit", PetConfiguration, BossDefinition);

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
        var created = service.CreateBattle("battle-swap-counters", PetConfiguration, BossDefinition);

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
        var created = service.CreateBattle("battle-swap-pair", PetConfiguration, BossDefinition);

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
        var created = service.CreateBattle("battle-swap-stale", PetConfiguration, BossDefinition);
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
        var created = service.CreateBattle("battle-swap-stable", PetConfiguration, BossDefinition);

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
    public void CreateBattle_ShouldStartComboAndMatchCountAtZero()
    {
        // GAME_STATE.md §2.2: MatchCount = 0 and Combo = 0 for a battle that has
        // resolved no action. Both are root members of the state; there is no nested
        // PlayerState node (ADR-011 item 2).
        var service = new BattleStateService();
        var state = service.CreateBattle("battle-progression-initial", PetConfiguration, BossDefinition);

        Assert.Equal(0, state.Combo);
        Assert.Equal(0, state.MatchCount);
        Assert.Equal(BattleState.InitialCombo, state.Combo);
        Assert.Equal(BattleState.InitialMatchCount, state.MatchCount);
    }

    [Fact]
    public void ExecuteSwap_ShouldRecordTheResolutionMatchAndComboValues()
    {
        // GAME_STATE.md §2.2 / §5.1 item 3: the recorded state carries the finished
        // resolution's values, so a subsequent read returns them — not a value
        // derived from the board afterwards.
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-progression-commit", PetConfiguration, BossDefinition);

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = service.ExecuteSwap("battle-progression-commit", pair);

        Assert.True(result!.Value.IsAccepted);

        var matches = result.Value.Resolution.Passes.Sum(pass => pass.Matches.Count);

        Assert.True(matches >= 1);
        Assert.Equal(matches, result.Value.State.Combo);
        Assert.Equal(matches, result.Value.State.MatchCount);

        // The registry holds the same value the result reports.
        var reread = service.GetBattle("battle-progression-commit");
        Assert.Equal(result.Value.State.Combo, reread!.Combo);
        Assert.Equal(result.Value.State.MatchCount, reread.MatchCount);
    }

    [Fact]
    public void ExecuteSwap_ShouldLeaveComboAndMatchCountUnchangedOnEveryRejection()
    {
        // MATCH3_RULES.md §2.1.5 item 5 / §6.1 item 3: a rejected Swap resets
        // nothing and increments nothing. The registry keeps the state object it
        // already held, so the progression values cannot have moved.
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-progression-rejected", PetConfiguration, BossDefinition);

        var result = service.ExecuteSwap("battle-progression-rejected", new SwapRequest(99, 100));

        Assert.True(result!.Value.IsRejected);

        var reread = service.GetBattle("battle-progression-rejected");
        Assert.Same(created, reread);
        Assert.Equal(0, reread!.Combo);
        Assert.Equal(0, reread.MatchCount);
    }

    [Fact]
    public void ResolveBoard_ShouldNotChangeTheProgressionState()
    {
        // Board resolution is not a Swap (MATCH3_RULES.md §8.1 item 4): it resolves no
        // action, so it starts no Turn, counts no Match, and resets no Combo. A
        // generated board holds no Match, so nothing resolves at all.
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-progression-resolution", PetConfiguration, BossDefinition);

        var resolved = service.ResolveBoard("battle-progression-resolution");

        Assert.NotNull(resolved);
        Assert.Equal(created.Combo, resolved!.Combo);
        Assert.Equal(created.MatchCount, resolved.MatchCount);
        Assert.Equal(created.PetState, resolved.PetState);
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
        // persistence, and §7 leaves PetState's persistence equally unchanged
        // (SIGNALR_PROTOCOL.md §4.3 item 12). The service holds no repository and
        // touches no store — the process-local registry is the same staged,
        // safe-to-lose boundary it already had.
        //
        // The three dictionaries are the battle's state, the battle's Pet loadout
        // input, and the battle's Boss definition (BOSS_RULES.md §6.2–§6.4's static
        // content, which BossState deliberately does not duplicate); none is a
        // REDIS_STATE.md store, and no fourth field (repository, cache, bus, logger)
        // was introduced.
        var fields = typeof(BattleStateService)
            .GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Select(f => f.Name)
            .ToArray();

        Assert.Equal(
            ["_battles", "_bossConfiguration", "_petConfiguration", "_seedSource"],
            fields.OrderBy(n => n, StringComparer.Ordinal));
    }

    // -----------------------------------------------------------------------
    // Passive charge integration (GAME_RULES.md §17 step 10;
    // PASSIVE_RULES.md §2, §4, §5; GAME_STATE.md §2.3;
    // GAME_EVENTS.md §1, §1.1, §2)
    // -----------------------------------------------------------------------

    [Fact]
    public void CreateBattle_ShouldInitializePetStateWithTheSuppliedPassive()
    {
        // GAME_STATE.md §2.3 item 3 / SIGNALR_PROTOCOL.md §4.3 item 4: PetState is
        // present from battle creation, carrying the Passive identity and a
        // progress at the start of its first charge.
        var service = new BattleStateService();

        var state = service.CreateBattle("battle-passive-initial", PetConfiguration, BossDefinition);

        Assert.Equal(PetConfiguration.PassiveId, state.PetState.PassiveId);
        Assert.Equal(PetConfiguration.PassiveThreshold, state.PetState.PassiveProgress.Threshold);
        Assert.Equal(0, state.PetState.PassiveProgress.Current);
    }

    [Fact]
    public void ExecuteSwap_ShouldChargeThePassiveOncePerMatchAndWriteTheProgressBack()
    {
        // PASSIVE_RULES.md §2 item 1 / §5: every Match increases progress by 1, so a
        // committed Swap's Cascade charges as many times as it produced Matches.
        // GAME_STATE.md §2.3 / §5.1: the settled progress is written back into
        // PetState in the same post-resolution write-back as the rest of the state.
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-passive-charge", PetConfiguration, BossDefinition);

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = service.ExecuteSwap("battle-passive-charge", pair);

        Assert.True(result!.Value.IsAccepted);

        // The expected number of charges is the resolution's own Match total — the
        // rule-level value, not a number copied from the implementation
        // (MATCH3_RULES.md §3 item 5: one Match is one detected shape).
        var matches = result.Value.Resolution.TotalMatches;
        Assert.True(matches >= 1);

        // GAME_EVENTS.md §2 / SIGNALR_PROTOCOL.md §3.2.16 item 1: the shared
        // PassiveCharged event is emitted by BOTH Passive systems, discriminated by
        // `source`. This stage is the Pet's, so the charges counted here are the
        // `source="pet"` ones; the Boss Passive's are asserted separately.
        var charges = result.Value.Events
            .Where(e => e.Type == BattleEventType.PassiveCharged
                && e.PassiveCharged.Source == PassiveEventSource.Pet)
            .ToArray();

        Assert.Equal(matches, charges.Length);
        Assert.Equal(
            Enumerable.Range(1, matches),
            charges.Select(c => c.PassiveCharged.Progress));

        Assert.All(charges, c => Assert.Equal(PetConfiguration.PassiveThreshold, c.PassiveCharged.Threshold));

        // PASSIVE_RULES.md §2 item 3 / §4 item 1: the Threshold is evaluated ONCE,
        // after the whole batch. Whether this Cascade triggered — and therefore what
        // the settled progress is — is decided by the documented comparison of the
        // batch total against the Threshold, not by the implementation.
        var triggers = result.Value.Events
            .Where(e => e.Type == BattleEventType.PassiveTriggered
                && e.PassiveTriggered.Source == PassiveEventSource.Pet)
            .ToArray();

        if (matches >= PetConfiguration.PassiveThreshold)
        {
            // §4 item 1: the default reset settles at 0.
            Assert.Single(triggers);
            Assert.Equal(0, result.Value.State.PetState.PassiveProgress.Current);
        }
        else
        {
            // §2 item 3: below the Threshold there is no trigger and "progress
            // carries" — the accumulated total is the settled value.
            Assert.Empty(triggers);
            Assert.Equal(matches, result.Value.State.PetState.PassiveProgress.Current);
        }

        // The registry holds the same PetState the result reports (GAME_STATE.md §5.1).
        Assert.Equal(
            result.Value.State.PetState,
            service.GetBattle("battle-passive-charge")!.PetState);
    }

    [Fact]
    public void ExecuteSwap_ShouldReportEveryChargeWithTheBattlePassiveIdentity()
    {
        // GAME_EVENTS.md §2 item 1: PassiveId identifies the Passive that charged —
        // the value PetState.PassiveId holds, read and reported, never re-derived.
        // The Pet's own charges are the `source="pet"` ones.
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-passive-identity", PetConfiguration, BossDefinition);

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = service.ExecuteSwap("battle-passive-identity", pair);

        Assert.True(result!.Value.IsAccepted);
        Assert.All(
            result.Value.Events.Where(e => e.Type == BattleEventType.PassiveCharged
                && e.PassiveCharged.Source == PassiveEventSource.Pet),
            e => Assert.Equal(PetConfiguration.PassiveId, e.PassiveCharged.PassiveId));
    }

    [Fact]
    public void ExecuteSwap_ShouldAppendThePassiveEventsAfterTheBoardResolutionEvents()
    {
        // GAME_EVENTS.md §1, §1.1 / GAME_RULES.md §17: the board cycle's events
        // (MatchCreated … ComboChanged) come first and the Passive stage's reports
        // follow them — one PassiveCharged per Match, and then the single
        // PassiveTriggered when the Threshold was crossed. The assembled list keeps
        // both stages' own order; nothing is re-sorted or interleaved.
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-passive-order", PetConfiguration, BossDefinition);

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = service.ExecuteSwap("battle-passive-order", pair);

        Assert.True(result!.Value.IsAccepted);

        var events = result.Value.Events;

        // GAME_EVENTS.md §1 / §6 places the resolution's phases in the documented
        // order: board resolution, Player Passive, Player→Boss damage, Boss Passive,
        // Boss Response damage, outcome. This stage's assertion is on the Pet Passive
        // run and the FIRST damage instance (the player's); the Boss Response that
        // follows is asserted by the Boss Response tests.
        var firstDamage = events
            .Select((e, index) => (e.Type, index))
            .First(t => t.Type is BattleEventType.DamageCalculated
                or BattleEventType.DamageDealt
                or BattleEventType.DamageTaken)
            .index;

        // The Pet's Passive run precedes the player's damage instance, and the Boss's
        // Passive events come after it (GAME_EVENTS.md §1, §6 step 6) — so the run
        // between the board events and the first damage is exactly the Pet's.
        var firstPassive = events
            .Select((e, index) => (e.Type, index))
            .First(t => t.Type is BattleEventType.PassiveCharged or BattleEventType.PassiveTriggered)
            .index;

        // Every event before the first Passive event belongs to the board
        // resolution — no PassiveCharged is interleaved into the Match-3 cycle
        // (GAME_EVENTS.md §1.1, the documented follow-up refinement).
        Assert.DoesNotContain(
            events.Take(firstPassive),
            e => e.Type is BattleEventType.PassiveCharged or BattleEventType.PassiveTriggered);

        // After it, the list is the tracker's own output in order — charges first
        // (one per Match, ascending), then at most one trigger — followed by the
        // three Damage events. Both Passive systems emit into this window, so the
        // Pet and Boss runs are separated by their documented `source` discriminators.
        var passiveRun = events.Skip(firstPassive).Take(firstDamage - firstPassive).ToArray();
        var (chargeRun, triggerRun) = SplitAtFirstTrigger(passiveRun);

        Assert.Equal(result.Value.Resolution.TotalMatches, chargeRun.Length);
        Assert.True(triggerRun.Length <= 1);
        Assert.All(chargeRun, e => Assert.Equal(BattleEventType.PassiveCharged, e.Type));
        Assert.Equal(
            Enumerable.Range(1, chargeRun.Length),
            chargeRun.Select(e => e.PassiveCharged.Progress));

        // The run before the first damage instance is the PET's, because §6 step 6
        // places the Boss Passive after the player's damage. Its charges all carry
        // source="pet".
        Assert.All(chargeRun, e => Assert.Equal(PassiveEventSource.Pet, e.PassiveCharged.Source));
    }

    [Fact]
    public void ExecuteSwap_ShouldNotWriteAnyPassiveEventForARejection()
    {
        // MATCH3_RULES.md §2.1.5 item 6 / GAME_EVENTS.md §1.2: a rejected action
        // emits no Battle Event at all. The Passive stage is not reached either, so
        // no charge and no trigger is produced.
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-passive-rejected", PetConfiguration, BossDefinition);

        var pair = FindAdjacentPairThatProducesNoMatch(created.BoardState);
        var result = service.ExecuteSwap("battle-passive-rejected", pair);

        Assert.True(result!.Value.IsRejected);
        Assert.Empty(result.Value.Events);

        // PASSIVE_RULES.md §2 item 1 charges per Match and a rejected Swap produces
        // none, so the Passive is not charged: the registry still holds the created
        // state, unchanged, and its progress is exactly where creation left it.
        var reread = service.GetBattle("battle-passive-rejected");
        Assert.Same(created, reread);
        Assert.Equal(0, reread!.PetState.PassiveProgress.Current);
    }

    [Fact]
    public void ExecuteSwap_ShouldCarryProgressAcrossSwaps()
    {
        // GAME_STATE.md §2.3: PassiveProgress is state, so it accumulates across
        // Swaps until the Threshold is reached. PASSIVE_RULES.md §2 item 1 adds each
        // Match to the progress already held, so two Swaps of M1 and M2 Matches
        // settle at min(M1 + M2, …) — and below the Threshold nothing resets.
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-passive-carry", PetConfiguration, BossDefinition);

        var firstPair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var first = service.ExecuteSwap("battle-passive-carry", firstPair);
        Assert.True(first!.Value.IsAccepted);

        var afterFirst = service.GetBattle("battle-passive-carry")!;
        var firstMatches = first.Value.Resolution.TotalMatches;

        // The first Swap's own settled progress is the documented result of its batch:
        // below the Threshold it carries M1, and at or above it the default reset
        // settled it at 0 (PASSIVE_RULES.md §2 item 3, §4 item 1). Read from the state
        // rather than assumed, because M1 depends on the generated board.
        var firstPetTriggered = first.Value.Events.Any(
            e => e.Type == BattleEventType.PassiveTriggered
                && e.PassiveTriggered.Source == PassiveEventSource.Pet);

        Assert.Equal(
            firstPetTriggered ? 0 : firstMatches,
            afterFirst.PetState.PassiveProgress.Current);

        var carriedProgress = afterFirst.PetState.PassiveProgress.Current;

        var secondPair = FindAdjacentPairThatProducesAMatch(afterFirst.BoardState, exclude: firstPair);
        var second = service.ExecuteSwap("battle-passive-carry", secondPair);
        Assert.True(second!.Value.IsAccepted);

        var secondMatches = second.Value.Resolution.TotalMatches;
        var afterSecond = service.GetBattle("battle-passive-carry")!;

        // The second Swap's charges continue from the CARRIED progress — they do not
        // restart per Swap (PASSIVE_RULES.md §2 item 1). Only the PET's charges are
        // read here; the Boss Passive emits its own source="boss" charges into the
        // same batch (GAME_EVENTS.md §2, SIGNALR_PROTOCOL.md §3.2.16 item 1).
        var charges = second.Value.Events
            .Where(e => e.Type == BattleEventType.PassiveCharged
                && e.PassiveCharged.Source == PassiveEventSource.Pet)
            .Select(e => e.PassiveCharged.Progress)
            .ToArray();

        Assert.Equal(
            Enumerable.Range(carriedProgress + 1, secondMatches),
            charges);

        // Below the Threshold (5) nothing triggered, so the settled value is the
        // running total.
        if (firstMatches + secondMatches < PetConfiguration.PassiveThreshold)
        {
            Assert.DoesNotContain(
                second.Value.Events,
                e => e.Type == BattleEventType.PassiveTriggered
                    && e.PassiveTriggered.Source == PassiveEventSource.Pet);

            Assert.Equal(
                firstMatches + secondMatches,
                afterSecond.PetState.PassiveProgress.Current);
        }
    }

    [Fact]
    public void ExecuteSwap_ShouldTriggerAtMostOncePerCascadeAndApplyTheResetBehavior()
    {
        // PASSIVE_RULES.md §2 item 3 / §5: the Threshold is evaluated once, after
        // the whole Cascade's Match batch, and the Passive triggers AT MOST ONCE per
        // Cascade for every Reset Behavior. This drives a Threshold of 1, so any
        // committed Swap — which always produces at least one Match
        // (MATCH3_RULES.md §2.1.2 item 4) — crosses it and triggers exactly once.
        var service = new BattleStateService();

        var configuration = new BattleStateService.PetConfiguration(
            Element.Hoa,
            new PassiveId("threshold-one"),
            PassiveThreshold: 1);

        var created = service.CreateBattle("battle-passive-trigger", configuration, BossDefinition);
        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = service.ExecuteSwap("battle-passive-trigger", pair);

        Assert.True(result!.Value.IsAccepted);

        var matches = result.Value.Resolution.TotalMatches;
        Assert.True(matches >= 1);

        var triggers = result.Value.Events
            .Where(e => e.Type == BattleEventType.PassiveTriggered
                && e.PassiveTriggered.Source == PassiveEventSource.Pet)
            .ToArray();

        // At most one trigger, whatever the Match count (the multi-crossing case).
        Assert.Single(triggers);

        // GAME_EVENTS.md §2 item 2: the trigger reports the progress at the crossing
        // — the batch total, before that trigger's own reset.
        Assert.Equal(matches, triggers[0].PassiveTriggered.Progress);
        Assert.Equal(1, triggers[0].PassiveTriggered.Threshold);

        // PASSIVE_RULES.md §4 item 1 / §2 item 4: the default reset settles progress
        // at 0, and any overflow is not re-evaluated within the same Cascade.
        Assert.Equal(0, result.Value.State.PetState.PassiveProgress.Current);
    }

    [Fact]
    public void ExecuteSwap_ShouldCarryPartialResetOverflowIntoTheNextSwap()
    {
        // PASSIVE_RULES.md §4 item 2 / §5: Partial Reset reduces progress by the
        // Threshold rather than to 0, so a Cascade that overshoots carries its
        // overflow into the next charge.
        var service = new BattleStateService();

        var configuration = new BattleStateService.PetConfiguration(
            Element.Hoa,
            new PassiveId("partial-reset"),
            PassiveThreshold: 1,
            PassiveResetOverride: PassiveResetBehavior.Partial);

        var created = service.CreateBattle("battle-passive-partial", configuration, BossDefinition);
        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = service.ExecuteSwap("battle-passive-partial", pair);

        Assert.True(result!.Value.IsAccepted);

        var matches = result.Value.Resolution.TotalMatches;
        Assert.Single(
            result.Value.Events,
            e => e.Type == BattleEventType.PassiveTriggered
                && e.PassiveTriggered.Source == PassiveEventSource.Pet);

        // §4 item 2: "progress reduces by Threshold rather than to 0" — the worked
        // arithmetic is current − Threshold, which at Threshold 1 is matches − 1.
        Assert.Equal(matches - 1, result.Value.State.PetState.PassiveProgress.Current);
    }

    [Fact]
    public void ExecuteSwap_ShouldKeepProgressUnderNoReset()
    {
        // PASSIVE_RULES.md §4 item 2: "No reset / persistent" — the trigger does not
        // reduce progress, so the batch total survives into the next Cascade.
        var service = new BattleStateService();

        var configuration = new BattleStateService.PetConfiguration(
            Element.Hoa,
            new PassiveId("no-reset"),
            PassiveThreshold: 1,
            PassiveResetOverride: PassiveResetBehavior.NoReset);

        var created = service.CreateBattle("battle-passive-no-reset", configuration, BossDefinition);
        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = service.ExecuteSwap("battle-passive-no-reset", pair);

        Assert.True(result!.Value.IsAccepted);

        var matches = result.Value.Resolution.TotalMatches;
        Assert.Single(
            result.Value.Events,
            e => e.Type == BattleEventType.PassiveTriggered
                && e.PassiveTriggered.Source == PassiveEventSource.Pet);

        // §4 item 2: progress is left exactly as the trigger found it — the batch
        // total, which at Threshold 1 is the Match count. It therefore still
        // satisfies the Threshold, which §2 item 4 deliberately does not
        // re-evaluate inside this Cascade.
        Assert.Equal(matches, result.Value.State.PetState.PassiveProgress.Current);
        Assert.True(result.Value.State.PetState.PassiveProgress.IsReady);
    }

    [Fact]
    public void ExecuteSwap_ShouldRecordTheResetBehaviorTheBattleWasCreatedWith()
    {
        // GAME_STATE.md §2.3 item 2: the Passive's identity is set at battle creation
        // and never changes; PASSIVE_RULES.md §4 makes the Reset Behavior a property
        // of the Passive's definition. Neither is written by a resolution.
        var service = new BattleStateService();

        var configuration = new BattleStateService.PetConfiguration(
            Element.Hoa,
            new PassiveId("partial-reset"),
            PassiveThreshold: 5,
            PassiveResetOverride: PassiveResetBehavior.Partial);

        var created = service.CreateBattle("battle-passive-definition", configuration, BossDefinition);
        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = service.ExecuteSwap("battle-passive-definition", pair);

        Assert.True(result!.Value.IsAccepted);

        Assert.Equal(configuration.PassiveId, result.Value.State.PetState.PassiveId);
        Assert.Equal(PassiveResetBehavior.Partial, result.Value.State.PetState.PassiveResetOverride);
        Assert.Equal(configuration.PassiveThreshold, result.Value.State.PetState.PassiveProgress.Threshold);
    }

    [Fact]
    public void ResolveBoard_ShouldNotChargeThePassive()
    {
        // Board resolution is not a Swap (MATCH3_RULES.md §8.1 item 4): it resolves
        // no action and produces no Match on a generated board, so PASSIVE_RULES.md
        // §2 item 1 has nothing to charge and the Passive state is untouched.
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-passive-resolution", PetConfiguration, BossDefinition);

        var resolved = service.ResolveBoard("battle-passive-resolution");

        Assert.NotNull(resolved);
        Assert.Equal(created.PetState, resolved!.PetState);
    }

    [Fact]
    public void ExecuteSwap_ShouldReturnTheSameWriteBackItStores()
    {
        // GAME_STATE.md §5.1: one action produces ONE post-resolution write-back. The
        // result the caller receives must therefore carry the same state the registry
        // holds — including the settled Passive progress — so a client rendering the
        // batch's Passive events and a client reading the state cannot disagree
        // (GAME_EVENTS.md §3 item 6: an event is never a substitute for the
        // write-back, and the write-back is never replaced by an event).
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-passive-writeback", PetConfiguration, BossDefinition);

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = service.ExecuteSwap("battle-passive-writeback", pair);

        Assert.True(result!.Value.IsAccepted);

        var stored = service.GetBattle("battle-passive-writeback")!;

        Assert.Same(result.Value.State, stored);
        Assert.Equal(stored.PetState, result.Value.State.PetState);

        // The last charge the batch reports and the settled progress are consistent:
        // under Default reset a trigger settles at 0, otherwise the last charge is the
        // settled value (PASSIVE_RULES.md §2 item 4, §4 item 1).
        var lastCharge = result.Value.Events
            .Where(e => e.Type == BattleEventType.PassiveCharged)
            .Select(e => e.PassiveCharged.Progress)
            .Last();

        var triggered = result.Value.Events.Any(e => e.Type == BattleEventType.PassiveTriggered
            && e.PassiveTriggered.Source == PassiveEventSource.Pet);

        Assert.Equal(
            triggered ? 0 : lastCharge,
            result.Value.State.PetState.PassiveProgress.Current);
    }

    // -------------------------------------------------------------------------
    // Damage Pipeline integration (GAME_RULES.md §17 steps 15–17)
    // -------------------------------------------------------------------------

    [Fact]
    public void ExecuteSwap_ShouldReduceBossHpThroughTheDamagePipeline()
    {
        // GAME_RULES.md §17 steps 15–17 / COMBAT_RULES.md §3: the Application
        // boundary invokes the Domain Damage Pipeline as part of the swap resolution,
        // so a committed Swap reduces Boss HP. Before this stage no damage reached the
        // Boss at all (BossState.HP was never written).
        //
        // The expected damage is derived from the documented formula and the values
        // the resolution itself reports — never from the pipeline's output:
        //   Base  = PetState.ATK + Resources.BaseDamagePool
        //   ×     ComboModifiers.Default for the accounted Combo   (GAME_RULES.md §5)
        //   ×     ElementModifiers.Default for the resolved matchup (ELEMENT_RULES.md §2.2)
        //   ×     1.00 (no Other Modifiers in MVP)
        //   ×     K / (K + BossState.DEF), K = 100                 (COMBAT_RULES.md §3.2)
        //   truncate toward zero, minimum 0                        (COMBAT_RULES.md §3 step 6)
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-damage-hp", PetConfiguration, BossDefinition);

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = service.ExecuteSwap("battle-damage-hp", pair);

        Assert.True(result!.Value.IsAccepted);

        var state = result.Value.State;
        var generation = result.Value.Resources;

        var baseDamage = created.PetState.ATK + generation.BaseDamagePool;
        var comboFactor = ComboModifiers.Default.NumeratorFor(state.Combo) / 100d;
        var matchup = ElementMatchups.Resolve(
            created.PetState.Element,
            created.BossState.Element);
        var elementFactor = ElementModifiers.Default.For(matchup);
        var preDefense = baseDamage * comboFactor * elementFactor * DamagePipeline.NoOtherModifiers;
        var expected = (int)Math.Truncate(
            preDefense * (DamagePipeline.DefenseMitigationConstant
                / (double)(DamagePipeline.DefenseMitigationConstant + created.BossState.DEF)));

        Assert.True(expected > 0, "this fixture's Swap must deal damage for the assertion to mean anything");
        Assert.Equal(expected, created.BossState.HP - state.BossState.HP);
        Assert.Equal(created.BossState.HP - expected, state.BossState.HP);
    }

    [Fact]
    public void ExecuteSwap_ShouldEmitTheThreeDamageEventsInTheDocumentedOrder()
    {
        // GAME_EVENTS.md §1: DamageCalculated, DamageDealt, DamageTaken — for each
        // damage instance. GAME_RULES.md §17 / COMBAT_RULES.md §3.4 put TWO instances
        // on one committed Swap now: the player's (steps 15–17) and the Boss's
        // Response (step 18b/18c). Each instance emits its own three events in the
        // documented order, and the player's precedes the Boss's.
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-damage-events", PetConfiguration, BossDefinition);

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = service.ExecuteSwap("battle-damage-events", pair);

        Assert.True(result!.Value.IsAccepted);

        var damageEvents = result.Value.Events
            .Where(e => e.Type is BattleEventType.DamageCalculated
                or BattleEventType.DamageDealt
                or BattleEventType.DamageTaken)
            .ToArray();

        // Two instances of three events, in the documented per-instance order, with
        // the player's instance first — the Boss Response runs only after the player's
        // damage has been applied (BOSS_RULES.md §3.3 item 1).
        Assert.Equal(
            [
                BattleEventType.DamageCalculated,
                BattleEventType.DamageDealt,
                BattleEventType.DamageTaken,
                BattleEventType.DamageCalculated,
                BattleEventType.DamageDealt,
                BattleEventType.DamageTaken,
            ],
            damageEvents.Select(e => e.Type));

        // The first instance is the player's and the second the Boss's, read from the
        // source/target the events themselves carry (GAME_EVENTS.md §2).
        Assert.Equal(DamageParty.Player, damageEvents[1].DamageDealt.Source);
        Assert.Equal(DamageParty.Boss, damageEvents[1].DamageDealt.Target);
        Assert.Equal(DamageParty.Boss, damageEvents[4].DamageDealt.Source);
        Assert.Equal(DamageParty.Player, damageEvents[4].DamageDealt.Target);
    }

    [Fact]
    public void ExecuteSwap_DamageCalculated_ShouldCarryTheFullDocumentedBreakdown()
    {
        // GAME_EVENTS.md §2: "DamageCalculated: Base, Combo Modifier, Element
        // Modifier, Other Modifiers, Defense, Final Damage (full breakdown, for
        // client feedback)". All six members must be reported, including the
        // pass-through Other Modifiers (COMBAT_RULES.md §3 step 4).
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-damage-breakdown", PetConfiguration, BossDefinition);

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = service.ExecuteSwap("battle-damage-breakdown", pair);

        // The player's instance is the FIRST DamageCalculated — the Boss's Response
        // instance follows it (GAME_RULES.md §17 steps 15–17 then 18b/18c).
        Assert.True(result!.Value.IsAccepted);

        var calculation = result.Value.Events
            .First(e => e.Type == BattleEventType.DamageCalculated)
            .DamageCalculated;

        var generation = result.Value.Resources;

        Assert.Equal(created.PetState.ATK + generation.BaseDamagePool, calculation.Base);
        Assert.Equal(
            ComboModifiers.Default.NumeratorFor(result.Value.State.Combo) / 100d,
            calculation.ComboModifier);
        Assert.Equal(
            ElementModifiers.Default.For(
                ElementMatchups.Resolve(created.PetState.Element, created.BossState.Element)),
            calculation.ElementModifier);
        Assert.Equal(1.00, calculation.OtherModifiers);
        Assert.True(calculation.Defense > 0d);
        Assert.True(calculation.FinalDamage >= 0);
    }

    [Fact]
    public void ExecuteSwap_DamageDealtAndTaken_ShouldAgreeWithTheAppliedHpChange()
    {
        // GAME_EVENTS.md §2 gives both events "source, target, Final Damage amount".
        // They describe ONE application of damage, so the amount both report must be
        // exactly the change the committed state shows — an event is never a
        // substitute for the write-back and never disagrees with it
        // (GAME_EVENTS.md §3 item 6, GAME_STATE.md §5.1).
        //
        // This is the PLAYER's instance: the first DamageDealt, whose source is the
        // player (COMBAT_RULES.md §3). It must equal the Boss's HP loss.
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-damage-agree", PetConfiguration, BossDefinition);

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = service.ExecuteSwap("battle-damage-agree", pair);

        Assert.True(result!.Value.IsAccepted);

        var dealt = result.Value.Events.First(e => e.Type == BattleEventType.DamageDealt).DamageDealt;
        var taken = result.Value.Events.First(e => e.Type == BattleEventType.DamageTaken).DamageTaken;

        // The Boss's total HP loss across the action is the player's damage minus
        // nothing — the Boss takes damage only from the player's instance.
        var applied = created.BossState.HP - result.Value.State.BossState.HP;

        Assert.Equal(DamageParty.Player, dealt.Source);
        Assert.Equal(DamageParty.Boss, dealt.Target);
        Assert.Equal(dealt.Amount, taken.Amount);
        Assert.Equal(dealt.Source, taken.Source);
        Assert.Equal(dealt.Target, taken.Target);
        Assert.Equal(applied, dealt.Amount);
    }

    [Fact]
    public void ExecuteSwap_ShouldStoreTheBossHpItReportsInTheSameWriteBack()
    {
        // GAME_STATE.md §5.1: one action, one write-back. The Boss's post-damage HP is
        // part of that single value, so the registry holds exactly the state the
        // result carries — a client reading the DamageDealt event and a client reading
        // the state cannot disagree.
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-damage-writeback", PetConfiguration, BossDefinition);

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = service.ExecuteSwap("battle-damage-writeback", pair);

        Assert.True(result!.Value.IsAccepted);

        var stored = service.GetBattle("battle-damage-writeback")!;

        Assert.Equal(result.Value.State.BossState, stored.BossState);
        Assert.True(stored.BossState.HP < created.BossState.HP);
        Assert.True(stored.BossState.HP >= 0);
    }

    [Fact]
    public void ExecuteSwap_ShouldNotDamageTheBossForARejectedSwap()
    {
        // MATCH3_RULES.md §2.1.5: a rejected action writes nothing and emits no Battle
        // Event (GAME_EVENTS.md §1.2). The Damage Pipeline is downstream of the commit,
        // so a rejected Swap deals no damage, produces no Damage event, and leaves the
        // Boss's HP exactly as it was.
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-damage-rejected", PetConfiguration, BossDefinition);

        var pair = FindAdjacentPairThatProducesNoMatch(created.BoardState);
        var result = service.ExecuteSwap("battle-damage-rejected", pair);

        Assert.True(result!.Value.IsRejected);
        Assert.Empty(result.Value.Events);

        var stored = service.GetBattle("battle-damage-rejected")!;

        Assert.Equal(created.BossState.HP, stored.BossState.HP);
    }

    [Fact]
    public void ExecuteSwap_ShouldCarryEveryUnrelatedBossFieldAcrossUnchanged()
    {
        // COMBAT_RULES.md §3 applies damage and nothing else; the Boss Response
        // (BOSS_RULES.md §3–§5) additionally charges the Skill and may transition
        // State on Enrage. What must NOT move is every field no step of this action
        // owns: the Boss's identity, its Element, and its base stats are configuration
        // set at battle creation (GAME_STATE.md §2.4, BOSS_RULES.md §6.1) and the
        // resolution rewrites none of them.
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-damage-bossfield", PetConfiguration, BossDefinition);

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = service.ExecuteSwap("battle-damage-bossfield", pair);

        Assert.True(result!.Value.IsAccepted);

        var after = result.Value.State.BossState;

        Assert.Equal(created.BossState.BossId, after.BossId);
        Assert.Equal(created.BossState.Element, after.Element);
        Assert.Equal(created.BossState.MaxHP, after.MaxHP);
        Assert.Equal(created.BossState.ATK, after.ATK);
        Assert.Equal(created.BossState.DEF, after.DEF);
        Assert.Equal(created.BossState.PassiveId, after.PassiveId);

        // The HP fell by exactly the player's damage instance — the Boss takes damage
        // from no other source in this action.
        var playerDamage = result.Value.Events
            .First(e => e.Type == BattleEventType.DamageDealt
                && e.DamageDealt.Source == DamageParty.Player)
            .DamageDealt.Amount;

        Assert.Equal(created.BossState.HP - playerDamage, after.HP);

        // And the Skill charge followed the documented rule (GAME_STATE.md §2.4.3 /
        // BOSS_RULES.md §6.3), whichever branch this board's Match total lands on:
        //
        //   the Skill did not fire -> the charge accumulated this Swap's Matches
        //   the Skill fired        -> the charge was reset to 0
        //
        // The branch is read from the events, not assumed, because the Match count
        // depends on the generated board — and the rule is a rule for both cases.
        var skillFired = result.Value.Events.Any(e => e.Type == BattleEventType.BossSkillCast);

        var expectedSkillCharge = skillFired
            ? 0
            : created.BossState.SkillCharge + result.Value.Resolution.TotalMatches;

        Assert.Equal(expectedSkillCharge, after.SkillCharge);

        // A fired Skill must have met both documented conditions before it did
        // (GAME_STATE.md §2.4.3).
        if (skillFired)
        {
            Assert.True(
                created.BossState.SkillCharge + result.Value.Resolution.TotalMatches
                    >= BossDefinition.SkillChargeRequirement);

            Assert.Equal(BossDefinition.SkillCooldownTurns, after.SkillCooldown);
        }
    }

    [Fact]
    public void ExecuteSwap_ShouldDrawNoAdditionalRandomnessForDamage()
    {
        // AGENTS.md §11 / ADR-009: the Damage Pipeline performs no Crit roll
        // (COMBAT_RULES.md §3.3) and draws no RNG, so the retained RngState after a
        // committed Swap is exactly the one the board resolution's Spawn produced
        // (MATCH3_RULES.md §4.5 item 4). A second randomization mechanism would move
        // it, or would live outside the documented stream.
        var service = new BattleStateService();
        var created = service.CreateBattle("battle-damage-rng", PetConfiguration, BossDefinition);

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = service.ExecuteSwap("battle-damage-rng", pair);

        Assert.True(result!.Value.IsAccepted);
        Assert.Equal(result.Value.Resolution.RngState, result.Value.State.RngState);
    }

    /// <summary>
    /// Splits the tracker's reports at the single trigger: the charges that precede
    /// it, and the trigger run itself (<c>PASSIVE_RULES.md</c> §2 item 3 evaluates
    /// the Threshold once, after the whole batch, so the trigger is the Cascade's
    /// last Passive event).
    /// </summary>
    private static (BattleEvent[] Charges, BattleEvent[] Triggers) SplitAtFirstTrigger(
        BattleEvent[] passiveRun)
    {
        var triggerIndex = Array.FindIndex(passiveRun, e => e.Type == BattleEventType.PassiveTriggered);

        return triggerIndex < 0
            ? (passiveRun, [])
            : (passiveRun[..triggerIndex], passiveRun[triggerIndex..]);
    }

    /// <summary>
    /// An adjacent pair of the board whose exchange produces a §3 Match — guaranteed
    /// to exist by <c>MATCH3_RULES.md</c> §1.4, and asserted found rather than assumed.
    /// </summary>
    private static SwapRequest FindAdjacentPairThatProducesAMatch(
        BoardState board,
        SwapRequest? exclude = null)
    {
        foreach (var (from, to) in AllAdjacentPairs())
        {
            if (exclude is { } excluded
                && CommittedSwapPair.FromCells(from, to) == CommittedSwapPair.FromCells(excluded.From, excluded.To))
            {
                continue;
            }

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