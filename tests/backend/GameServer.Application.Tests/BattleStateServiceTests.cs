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
    /// caller. This is the configuration those calls pass: the identity of the
    /// owned Pet instance the battle selected (GAME_STATE.md §2.3 — the
    /// `Pet.PetInstanceId`), Xích Lang's MVP Element
    /// (ELEMENT_RULES.md §6 — Hỏa), its Passive identity, and the Threshold, with
    /// no Reset Behavior override — which is PASSIVE_RULES.md §4 item 1's default
    /// and the behavior of all five MVP Pet Passives (§8).
    /// </summary>
    private static readonly BattleStateService.PetConfiguration PetConfiguration =
        new(new PetId(PetInstanceId), Element.Hoa, new PassiveId("xich-lang"), PassiveThreshold: 5);

    /// <summary>
    /// The owning Player of the tests' battles (GAME_STATE.md §2.8) — the identity
    /// the battle-start composition records at creation. It is deliberately not a
    /// value any other fixture uses, so an assertion about the recorded owner
    /// cannot pass against an unrelated member.
    /// </summary>
    private static readonly PlayerId Owner = new("player_owner_1");

    /// <summary>The owned Pet instance identity the tests' battles select.</summary>
    private const string PetInstanceId = "pet_instance_1";

    /// <summary>
    /// The Boss the tests' battles are fought against — Hỏa Long, an MVP Boss of
    /// BOSS_RULES.md §6.1 (Hỏa, 5000 / 100 / 50).
    /// </summary>
    private static readonly BossDefinition BossDefinition = BossDefinitions.HoaLong;

    /// <summary>
    /// The service under test, composed over the in-memory active-state store
    /// double and a deterministic seed source.
    ///
    /// The repository is a <b>test double standing in for the Redis active-state
    /// store</b> (<c>REDIS_STATE.md</c> §1–§4), which is what the production
    /// composition registers instead
    /// (<c>GameServer.Infrastructure.Redis.BattleStateRepository</c>). The real
    /// store needs a live Redis instance; <c>REDIS_STATE.md</c> §2 item 2 makes the
    /// stored record the single source of truth for a battle's live state and §7
    /// item 5 permits no in-process substitute in the running server, so the
    /// substitution is confined to the tests — the real
    /// <see cref="BattleStateService"/> pipeline runs unchanged over it.
    ///
    /// The seed source pins the battle's seed (<c>GAME_STATE.md</c> §2.6.1), so the
    /// generated board — and therefore the Match count, the generated resources,
    /// and the damage — is the same on every run.
    /// </summary>
    /// <param name="repository">
    /// The store double to compose over, or <c>null</c> for a fresh
    /// <see cref="InMemoryBattleStateRepository"/>. A test that asserts what the
    /// store received — the number of writes it applied
    /// (<see cref="InMemoryBattleStateRepository.WriteCount"/>, counting creations
    /// and accepted updates alike), or the presence and contents of a record —
    /// keeps the reference it passed in. The concurrency regressions pass a
    /// <see cref="ContendedBattleStateRepository"/>, which is the same store with
    /// a read rendezvous and an injectable competitor
    /// (<c>REDIS_STATE.md</c> §4).
    /// </param>
    private static BattleStateService NewService(IBattleStateRepository? repository = null) =>
        new(repository ?? new InMemoryBattleStateRepository(), new FixedRngSeedSource());

    [Fact]
    public async Task CreateBattle_ShouldProduceTheDocumentedInitialState()
    {
        var service = NewService();
        var state = await service.CreateBattleAsync("battle-1", Owner, PetConfiguration, BossDefinition);

        Assert.Equal("battle-1", state.BattleId);
        Assert.Equal(0, state.Turn);
        Assert.Equal(0, state.Sequence);
    }

    [Fact]
    public async Task CreatedState_ShouldBeOwnedAndRetrievableByTheServerRuntime()
    {
        // GAME_STATE.md §2.0.4.1: Foundation State is authoritative server
        // state, produced and held server-side.
        var service = NewService();
        await service.CreateBattleAsync("battle-1", Owner, PetConfiguration, BossDefinition);

        var retrieved = await service.GetBattleAsync("battle-1");

        Assert.NotNull(retrieved);
        Assert.Equal("battle-1", retrieved!.BattleId);
        Assert.Equal(0, retrieved.Turn);
        Assert.Equal(0, retrieved.Sequence);
    }

    [Fact]
    public async Task GetBattle_ForUnknownBattle_ShouldReturnNull()
    {
        var service = NewService();

        Assert.Null(await service.GetBattleAsync("never-created"));
    }

    [Fact]
    public async Task GetInitialStateForGroup_ShouldReturnTheCreatedFoundationState()
    {
        // SIGNALR_PROTOCOL.md §4.1: the state delivered when the client joins
        // the battle group is the current authoritative state.
        var service = NewService();
        await service.CreateBattleAsync("battle-1", Owner, PetConfiguration, BossDefinition);

        var delivered = await service.GetInitialStateForGroupAsync("battle-1");

        Assert.NotNull(delivered);
        Assert.Equal("battle-1", delivered!.BattleId);
        Assert.Equal(0, delivered.Turn);
        Assert.Equal(0, delivered.Sequence);
    }

    [Fact]
    public async Task GetInitialStateForGroup_ForUnknownBattle_ShouldReturnNull()
    {
        var service = NewService();

        Assert.Null(await service.GetInitialStateForGroupAsync("never-created"));
    }

    [Fact]
    public async Task RepeatedReads_ShouldNotAlterTheAuthoritativeState()
    {
        // Foundation State has no resolutions (GAME_STATE.md §2.0.2), so no
        // read, join, or repeat delivery may change Turn or Sequence.
        var service = NewService();
        await service.CreateBattleAsync("battle-1", Owner, PetConfiguration, BossDefinition);

        await service.GetBattleAsync("battle-1");
        await service.GetInitialStateForGroupAsync("battle-1");
        var second = await service.GetInitialStateForGroupAsync("battle-1");

        Assert.Equal(0, second!.Turn);
        Assert.Equal(0, second.Sequence);
        Assert.NotNull(await service.GetBattleAsync("battle-1"));
    }

    [Fact]
    public async Task CreateBattle_ShouldTrackEachBattleIdSeparately()
    {
        var service = NewService();

        await service.CreateBattleAsync("battle-1", Owner, PetConfiguration, BossDefinition);
        await service.CreateBattleAsync("battle-2", Owner, PetConfiguration, BossDefinition);

        Assert.Equal("battle-1", (await service.GetBattleAsync("battle-1"))!.BattleId);
        Assert.Equal("battle-2", (await service.GetBattleAsync("battle-2"))!.BattleId);
        Assert.NotNull(await service.GetBattleAsync("battle-1"));
        Assert.NotNull(await service.GetBattleAsync("battle-2"));
    }

    [Fact]
    public async Task CreateBattle_ShouldRejectAnEmptyBattleId()
    {
        var service = NewService();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateBattleAsync(string.Empty, Owner, PetConfiguration, BossDefinition));
    }

    [Fact]
    public async Task GetBattle_ShouldRejectAnEmptyBattleId()
    {
        var service = NewService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetBattleAsync(string.Empty));
    }

    // -----------------------------------------------------------------------
    // Board resolution boundary (MATCH3_RULES.md §4, ARCHITECTURE.md §4.1)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResolveBoard_ShouldLeaveAStableBoardUnchangedAndConsumeNoRng()
    {
        // A generated board holds no Match (MATCH3_RULES.md §1.3), so the resolution
        // loop terminates immediately: no pass runs, nothing is removed, and Spawn —
        // the only gameplay RNG consumer (§7.2 item 1) — draws nothing.
        var service = NewService();
        var created = await service.CreateBattleAsync("battle-stable", Owner, PetConfiguration, BossDefinition);

        var resolved = await service.ResolveBoardAsync("battle-stable");

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
    public async Task ResolveBoard_ShouldRecordTheResolvedStateAsAuthoritative()
    {
        // GAME_STATE.md §5.1 / §2.0.4.1: the resolved board is the authoritative state
        // the server holds and delivers, so a subsequent read returns the resolved
        // values rather than the pre-resolution ones.
        var service = NewService();
        await service.CreateBattleAsync("battle-resolved", Owner, PetConfiguration, BossDefinition);

        var resolved = await service.ResolveBoardAsync("battle-resolved");
        var reread = await service.GetBattleAsync("battle-resolved");

        Assert.NotNull(resolved);
        Assert.NotNull(reread);

        // The store holds the resolved values, member for member — it is the record
        // of truth and not a copy the resolution kept (REDIS_STATE.md §2 item 2, §4
        // item 5).
        Assert.Equal(resolved!.Turn, reread!.Turn);
        Assert.Equal(resolved.Sequence, reread.Sequence);
        Assert.Equal(resolved.RngState, reread.RngState);
        Assert.Equal(resolved.Combo, reread.Combo);
        Assert.Equal(resolved.MatchCount, reread.MatchCount);
        Assert.True(resolved.BoardState.CellsEqual(reread.BoardState));
    }

    [Fact]
    public async Task ResolveBoard_ShouldBeIdempotentOnAnAlreadyStableBoard()
    {
        // Resolving a stable board is a no-op, so resolving it again changes nothing —
        // the determinism guarantee of MATCH3_RULES.md §4.6 applied at this boundary.
        var service = NewService();
        await service.CreateBattleAsync("battle-idempotent", Owner, PetConfiguration, BossDefinition);

        var first = await service.ResolveBoardAsync("battle-idempotent");
        var second = await service.ResolveBoardAsync("battle-idempotent");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.True(first!.BoardState.CellsEqual(second!.BoardState));
        Assert.Equal(first.RngState, second.RngState);
    }

    [Fact]
    public async Task ResolveBoard_ShouldNotWriteAnyBattleEventOrAddAMessage()
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
    public async Task ResolveBoard_ShouldReturnNullForAnUnknownBattle()
    {
        // Consistent with GetBattle: an unknown battle resolves nothing rather than
        // creating state.
        var service = NewService();

        Assert.Null(await service.ResolveBoardAsync("battle-that-does-not-exist"));
    }

    [Fact]
    public async Task ResolveBoard_ShouldRejectAnEmptyBattleId()
    {
        var service = NewService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.ResolveBoardAsync(string.Empty));
    }

    [Fact]
    public async Task ResolvedBattle_ShouldRetainTheSeedItWasCreatedWith()
    {
        // GAME_STATE.md §2.6.1 item 3: RngSeed records the battle's origin and is never
        // rewritten. Resolution advances RngState, not the seed.
        var service = NewService();
        var created = await service.CreateBattleAsync("battle-seed", Owner, PetConfiguration, BossDefinition);

        var resolved = await service.ResolveBoardAsync("battle-seed");

        Assert.NotNull(resolved);
        Assert.Equal(created.RngSeed, resolved!.RngSeed);
    }

    // -----------------------------------------------------------------------
    // Swap execution boundary (MATCH3_RULES.md §2.1.6, §8.3, GAME_STATE.md §5.1)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteSwap_ShouldRejectEveryRequestOnAFreshGeneratedBoard()
    {
        // A generated board holds no Match (MATCH3_RULES.md §1.3) and is guaranteed at
        // least one valid swap (§1.4), so this asserts the boundary rather than a
        // particular pair: whatever the generated board is, a request that produces no
        // Match is rejected and the authoritative state is left exactly as it was.
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await service.CreateBattleAsync("battle-swap-reject", Owner, PetConfiguration, BossDefinition);

        var result = await service.ExecuteSwapAsync("battle-swap-reject", new SwapRequest(99, 100));

        Assert.NotNull(result);
        Assert.True(result!.Value.IsRejected);
        Assert.Equal(SwapRejectionReason.InvalidCellIndex, result.Value.Reason);

        // §2.1.5: a rejected action writes nothing — creation was the only write
        // (REDIS_STATE.md §3), and the active-state store still holds the created
        // record, unchanged.
        Assert.Equal(1, repository.WriteCount);

        var reread = await service.GetBattleAsync("battle-swap-reject");
        Assert.NotNull(reread);
        Assert.Equal(created.BattleId, reread!.BattleId);
        Assert.Equal(created.Turn, reread.Turn);
        Assert.Equal(created.Sequence, reread.Sequence);
        Assert.Equal(created.RngSeed, reread.RngSeed);
        Assert.Equal(created.RngState, reread.RngState);
        Assert.Equal(created.Combo, reread.Combo);
        Assert.Equal(created.MatchCount, reread.MatchCount);
        Assert.True(created.BoardState.CellsEqual(reread.BoardState));
        Assert.Equal(created.PetState, reread.PetState);
        Assert.Equal(created.BossState, reread.BossState);
        Assert.Equal(BattleState.InitialTurn, reread.Turn);
        Assert.Equal(BattleState.InitialSequence, reread.Sequence);
        Assert.Null(reread.LastCommittedSwapPair);
    }

    [Fact]
    public async Task ExecuteSwap_ShouldRejectARequestThatProducesNoMatch()
    {
        // The service performs no validation of its own: the reason is the validator's,
        // reached through the Domain executor (ARCHITECTURE.md §2.1).
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var state = await service.CreateBattleAsync("battle-swap-nomatch", Owner, PetConfiguration, BossDefinition);

        // Find an adjacent pair of the generated board that produces no Match. §1.4
        // guarantees at least one pair does produce one, so the complement is not
        // empty either — the loop asserts it found one rather than assuming.
        var pair = FindAdjacentPairThatProducesNoMatch(state.BoardState);

        var result = await service.ExecuteSwapAsync("battle-swap-nomatch", pair);

        Assert.NotNull(result);
        Assert.Equal(SwapRejectionReason.NoMatchFromSwap, result!.Value.Reason);

        // MATCH3_RULES.md §2.1.5: a rejected action writes nothing, so creation was
        // the only write and the store still holds the created record unchanged.
        Assert.Equal(1, repository.WriteCount);

        var reread = await service.GetBattleAsync("battle-swap-nomatch");

        Assert.NotNull(reread);
        Assert.Equal(state.BattleId, reread!.BattleId);
        Assert.Equal(state.Turn, reread.Turn);
        Assert.Equal(state.Sequence, reread.Sequence);
        Assert.Equal(state.RngState, reread.RngState);
        Assert.True(state.BoardState.CellsEqual(reread.BoardState));
        Assert.Equal(state.PetState, reread.PetState);
        Assert.Equal(state.BossState, reread.BossState);
    }

    [Fact]
    public async Task ExecuteSwap_ShouldCommitAndRecordTheResolvedState()
    {
        // §2.1.6 / GAME_STATE.md §5.1: an accepted Swap is resolved and the resulting
        // state replaces the authoritative one in a single write-back, so a subsequent
        // read returns the resolved values.
        var service = NewService();
        var created = await service.CreateBattleAsync("battle-swap-commit", Owner, PetConfiguration, BossDefinition);

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = await service.ExecuteSwapAsync("battle-swap-commit", pair);

        Assert.NotNull(result);
        Assert.True(result!.Value.IsAccepted);

        var reread = await service.GetBattleAsync("battle-swap-commit");

        Assert.NotNull(reread);
        Assert.True(result.Value.State.BoardState.CellsEqual(reread!.BoardState));
        Assert.Equal(result.Value.State.RngState, reread.RngState);
        Assert.Equal(result.Value.State.RngSeed, reread.RngSeed);
    }

    [Fact]
    public async Task ExecuteSwap_ShouldAdvanceTurnAndSequenceExactlyOnce()
    {
        // §8.1 item 1 / §8.2 item 1: one committed Swap begins one Turn and increments
        // Sequence by exactly 1, written together in the same write-back.
        var service = NewService();
        var created = await service.CreateBattleAsync("battle-swap-counters", Owner, PetConfiguration, BossDefinition);

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = await service.ExecuteSwapAsync("battle-swap-counters", pair);

        Assert.True(result!.Value.IsAccepted);
        Assert.Equal(created.Turn + 1, result.Value.State.Turn);
        Assert.Equal(created.Sequence + 1, result.Value.State.Sequence);
    }

    [Fact]
    public async Task ExecuteSwap_ShouldRecordTheCanonicalCommittedPair()
    {
        // GAME_STATE.md §2.1.10 item 5: the committed pair is written in the same
        // write-back as the counters, canonically (min, max).
        var service = NewService();
        var created = await service.CreateBattleAsync("battle-swap-pair", Owner, PetConfiguration, BossDefinition);

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = await service.ExecuteSwapAsync("battle-swap-pair", pair);

        Assert.True(result!.Value.IsAccepted);
        Assert.Equal(
            CommittedSwapPair.FromCells(pair.From, pair.To),
            (await service.GetBattleAsync("battle-swap-pair"))!.LastCommittedSwapPair);
    }

    [Fact]
    public async Task ExecuteSwap_ShouldRejectAReplayOfTheCommittedPairAsStale()
    {
        // §2.1.4 item 2: replaying the pair just committed — in either order — is
        // rejected as already applied, and the state the commit produced is untouched.
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await service.CreateBattleAsync("battle-swap-stale", Owner, PetConfiguration, BossDefinition);
        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);

        var committed = await service.ExecuteSwapAsync("battle-swap-stale", pair);
        Assert.True(committed!.Value.IsAccepted);
        var afterCommit = await service.GetBattleAsync("battle-swap-stale");

        // Creation plus the one committed Swap: exactly two writes so far — the
        // documented one write-back per action (REDIS_STATE.md §4 item 5).
        Assert.Equal(2, repository.WriteCount);

        foreach (var replay in new[]
                 {
                     pair,
                     new SwapRequest(pair.To, pair.From),
                 })
        {
            var result = await service.ExecuteSwapAsync("battle-swap-stale", replay);

            Assert.True(result!.Value.IsRejected);
            Assert.Equal(SwapRejectionReason.StaleAction, result.Value.Reason);

            // §2.1.5: unchanged — the rejection performed no write at all, and the
            // record it matched is still the one the commit produced.
            Assert.Equal(2, repository.WriteCount);

            var reread = await service.GetBattleAsync("battle-swap-stale");

            Assert.NotNull(reread);
            Assert.Equal(afterCommit!.Turn, reread!.Turn);
            Assert.Equal(afterCommit.Sequence, reread.Sequence);
            Assert.Equal(afterCommit.RngState, reread.RngState);
            Assert.True(afterCommit.BoardState.CellsEqual(reread.BoardState));
            Assert.Equal(afterCommit.LastCommittedSwapPair, reread.LastCommittedSwapPair);
        }
    }

    // =======================================================================
    // ADR-025: Idempotency and Precondition Verification
    // =======================================================================

    [Fact]
    public async Task ExecuteSwap_IdempotentRetry_ShouldReturnAcceptedWithoutDuplicateExecution()
    {
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var battleId = "battle-idempotent-retry";
        var created = await service.CreateBattleAsync(battleId, Owner, PetConfiguration, BossDefinition);
        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);

        var firstRequest = new SwapRequest(pair.From, pair.To, ExpectedSequence: created.Sequence, ActionId: "client-act-1");
        var firstResult = await service.ExecuteSwapAsync(battleId, firstRequest);

        Assert.True(firstResult!.Value.IsAccepted);
        Assert.False(firstResult.Value.IsIdempotentReplay);
        var writeCountAfterFirst = repository.WriteCount;

        // Retry identical request with same actionId (lost ack scenario)
        var retryRequest = new SwapRequest(pair.From, pair.To, ExpectedSequence: created.Sequence, ActionId: "client-act-1");
        var retryResult = await service.ExecuteSwapAsync(battleId, retryRequest);

        Assert.True(retryResult!.Value.IsAccepted);
        Assert.True(retryResult.Value.IsIdempotentReplay);
        Assert.Equal(writeCountAfterFirst, repository.WriteCount); // No duplicate state mutation or write
    }

    [Fact]
    public async Task ExecuteSwap_ReusedActionIdWithDifferentPayload_ShouldRejectAsInvalidActionPayload()
    {
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var battleId = "battle-reused-action-id";
        var created = await service.CreateBattleAsync(battleId, Owner, PetConfiguration, BossDefinition);
        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);

        var firstRequest = new SwapRequest(pair.From, pair.To, ExpectedSequence: created.Sequence, ActionId: "client-act-reused");
        var firstResult = await service.ExecuteSwapAsync(battleId, firstRequest);
        Assert.True(firstResult!.Value.IsAccepted);

        // Reusing actionId with different coordinates
        var conflictRequest = new SwapRequest(0, 1, ExpectedSequence: created.Sequence, ActionId: "client-act-reused");
        var conflictResult = await service.ExecuteSwapAsync(battleId, conflictRequest);

        Assert.True(conflictResult!.Value.IsRejected);
        Assert.Equal(SwapRejectionReason.InvalidActionPayload, conflictResult.Value.Reason);
    }

    [Fact]
    public async Task ExecuteSwap_StaleExpectedSequence_ShouldRejectAsStaleAction()
    {
        var service = NewService();
        var battleId = "battle-stale-sequence";
        var created = await service.CreateBattleAsync(battleId, Owner, PetConfiguration, BossDefinition);
        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);

        // Request carries obsolete sequence
        var staleRequest = new SwapRequest(pair.From, pair.To, ExpectedSequence: 999, ActionId: "client-act-stale");
        var result = await service.ExecuteSwapAsync(battleId, staleRequest);

        Assert.True(result!.Value.IsRejected);
        Assert.Equal(SwapRejectionReason.StaleAction, result.Value.Reason);
    }

    // =======================================================================
    // TASK-257: the action identity's relationship to the precondition and to
    // the committed sequence, the documented retention window, and the races a
    // compare-and-set refusal can produce (ADR-025 §2/§4, MATCH3_RULES.md
    // §2.1.4, REDIS_STATE.md §4 items 1–3).
    // =======================================================================

    /// <summary>
    /// The action id the injected competitor's committed record carries in the
    /// contention tests, so an assertion can tell "the competitor committed" from
    /// "the request under test committed".
    /// </summary>
    private const string ConcurrentWinnerActionId = "act-concurrent-winner";

    [Fact]
    public async Task ExecuteSwap_RetryCarryingTheCommittedSequence_ShouldStillBeRecognisedAsAReplay()
    {
        // ADR-025 §2 / MATCH3_RULES.md §2.1.4 item 2: identity is evaluated before
        // the state precondition, so a retry is recognised whichever of the two
        // sequences its client last observed. The committed sequence is the one a
        // client that lost the acknowledgement but received the post-commit state
        // push — or resynchronized through GetBattleState — holds.
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var battleId = "battle-retry-observed-sequence";
        var created = await service.CreateBattleAsync(battleId, Owner, PetConfiguration, BossDefinition);
        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        const string actionId = "act-retry-observed";

        var first = await service.ExecuteSwapAsync(
            battleId,
            new SwapRequest(pair.From, pair.To, ExpectedSequence: created.Sequence, ActionId: actionId));

        Assert.True(first!.Value.IsAccepted);
        Assert.False(first.Value.IsIdempotentReplay);

        var committed = await service.GetBattleAsync(battleId);

        Assert.NotNull(committed);
        Assert.Equal(created.Sequence + 1, committed!.Sequence);
        Assert.Equal(committed.Sequence, committed.LastCommittedSwapPair!.Value.Sequence);
        Assert.Equal(actionId, committed.LastCommittedSwapPair.Value.ActionId);

        var writesAfterCommit = repository.WriteCount;

        var retry = await service.ExecuteSwapAsync(
            battleId,
            new SwapRequest(pair.From, pair.To, ExpectedSequence: committed.Sequence, ActionId: actionId));

        Assert.True(retry!.Value.IsAccepted);
        Assert.True(retry.Value.IsIdempotentReplay);
        Assert.Empty(retry.Value.Events);
        Assert.Equal(writesAfterCommit, repository.WriteCount);

        var afterRetry = await service.GetBattleAsync(battleId);

        Assert.Equal(committed.Sequence, afterRetry!.Sequence);
        Assert.Equal(committed.Turn, afterRetry.Turn);
        Assert.True(committed.BoardState.CellsEqual(afterRetry.BoardState));
    }

    [Fact]
    public async Task ExecuteSwap_RetryAfterALaterActionCommitted_ShouldRejectAsStaleNotAsAReplay()
    {
        // ADR-025 §2 / GAME_STATE.md §2.1.10 item 5: exactly one committed Swap is
        // recorded, so the window in which a retry is recognised as a replay is one
        // swap wide. A retry that arrives after a later action committed matches no
        // retained identity and its obsolete precondition rejects it as
        // STALE_ACTION — it is recognised as an action from an old state, never
        // re-executed, and never a duplicate write.
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var battleId = "battle-retry-outside-the-window";
        var created = await service.CreateBattleAsync(battleId, Owner, PetConfiguration, BossDefinition);
        var firstPair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        const string firstActionId = "act-outside-window-first";

        var first = await service.ExecuteSwapAsync(
            battleId,
            new SwapRequest(firstPair.From, firstPair.To, created.Sequence, firstActionId));

        Assert.True(first!.Value.IsAccepted);

        var afterFirst = await service.GetBattleAsync(battleId);
        var secondPair = FindAdjacentPairThatProducesAMatch(afterFirst!.BoardState, exclude: firstPair);

        var second = await service.ExecuteSwapAsync(
            battleId,
            new SwapRequest(secondPair.From, secondPair.To, afterFirst.Sequence, "act-outside-window-second"));

        Assert.True(second!.Value.IsAccepted);

        var writesAfterSecondCommit = repository.WriteCount;

        var retry = await service.ExecuteSwapAsync(
            battleId,
            new SwapRequest(firstPair.From, firstPair.To, created.Sequence, firstActionId));

        Assert.True(retry!.Value.IsRejected);
        Assert.Equal(SwapRejectionReason.StaleAction, retry.Value.Reason);
        Assert.Equal(writesAfterSecondCommit, repository.WriteCount);

        var afterRetry = await service.GetBattleAsync(battleId);

        Assert.Equal(second.Value.State.Sequence, afterRetry!.Sequence);
        Assert.Equal("act-outside-window-second", afterRetry.LastCommittedSwapPair!.Value.ActionId);
    }

    [Fact]
    public async Task ExecuteSwap_TwoConcurrentRequestsWithOneActionIdentity_ShouldCommitOnceAndAnswerTheOtherAsARetry()
    {
        // SIGNALR_PROTOCOL.md §6 item 1 / REDIS_STATE.md §4 items 2–3: two
        // deliveries of ONE logical action — the same action id, payload, and
        // precondition — can only produce one commit. The read rendezvous forces
        // both resolutions to start from the same state version, so the race is
        // the contract's, not the scheduler's.
        var repository = new ContendedBattleStateRepository();
        var service = NewService(repository);
        var battleId = "battle-race-same-identity";
        var created = await service.CreateBattleAsync(battleId, Owner, PetConfiguration, BossDefinition);
        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);

        var request = new SwapRequest(
            pair.From,
            pair.To,
            ExpectedSequence: created.Sequence,
            ActionId: "act-race-identical");

        repository.RendezvousReads(2);

        var results = await Task.WhenAll(
            service.ExecuteSwapAsync(battleId, request),
            service.ExecuteSwapAsync(battleId, request));

        var committed = results.Where(r => r!.Value.IsAccepted && !r.Value.IsIdempotentReplay).ToList();
        var replays = results.Where(r => r!.Value.IsIdempotentReplay).ToList();

        // Exactly one logical action committed, and the other delivery was answered
        // as the retry it is (ADR-025 §2): accepted, without executing again and
        // without a second event batch.
        Assert.Single(committed);
        Assert.Single(replays);
        Assert.Empty(replays[0]!.Value.Events);

        // Creation plus the one accepted write-back: the replay wrote nothing
        // (REDIS_STATE.md §4 items 5, 7).
        Assert.Equal(2, repository.WriteCount);

        var stored = await service.GetBattleAsync(battleId);

        Assert.Equal(created.Sequence + 1, stored!.Sequence);
        Assert.Equal("act-race-identical", stored.LastCommittedSwapPair!.Value.ActionId);
        Assert.Equal(committed[0]!.Value.State.Sequence, stored.Sequence);
    }

    [Fact]
    public async Task ExecuteSwap_TwoConcurrentRequestsWithDifferentActionIdentities_ShouldCommitAtMostOne()
    {
        // ADR-025 §4: two different logical actions that observed the same state
        // version cannot both commit. The winner's commit advances Sequence, so the
        // loser's precondition is consumed and it is rejected as a stale action —
        // it is never resolved against the winner's board.
        var repository = new ContendedBattleStateRepository();
        var service = NewService(repository);
        var battleId = "battle-race-different-identities";
        var created = await service.CreateBattleAsync(battleId, Owner, PetConfiguration, BossDefinition);
        var firstPair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var secondPair = FindAdjacentPairThatProducesAMatch(created.BoardState, exclude: firstPair);

        var firstRequest = new SwapRequest(
            firstPair.From,
            firstPair.To,
            ExpectedSequence: created.Sequence,
            ActionId: "act-race-first");
        var secondRequest = new SwapRequest(
            secondPair.From,
            secondPair.To,
            ExpectedSequence: created.Sequence,
            ActionId: "act-race-second");

        repository.RendezvousReads(2);

        var results = await Task.WhenAll(
            service.ExecuteSwapAsync(battleId, firstRequest),
            service.ExecuteSwapAsync(battleId, secondRequest));

        var committed = results.Where(r => r!.Value.IsAccepted).ToList();
        var rejected = results.Where(r => r!.Value.IsRejected).ToList();

        Assert.Single(committed);
        Assert.Single(rejected);
        Assert.Equal(SwapRejectionReason.StaleAction, rejected[0]!.Value.Reason);
        Assert.Equal(2, repository.WriteCount);

        var stored = await service.GetBattleAsync(battleId);

        Assert.Equal(created.Sequence + 1, stored!.Sequence);
        Assert.Equal(committed[0]!.Value.State.Sequence, stored.Sequence);
    }

    [Fact]
    public async Task ExecuteSwap_WhenAConcurrentWinnerMovesTheState_ShouldRejectAnObsoletePreconditionAndWriteNothing()
    {
        // REDIS_STATE.md §4 items 2–3 / ADR-025 §4: a refused compare-and-set means
        // the state moved on between the read and the write. A request whose
        // precondition names the version it read must not be resolved against the
        // newer state — the precondition is consumed and the action is rejected as
        // stale, with no write and no change to the competitor's record.
        var repository = new ContendedBattleStateRepository();
        var service = NewService(repository);
        var battleId = "battle-obsolete-precondition";
        var created = await service.CreateBattleAsync(battleId, Owner, PetConfiguration, BossDefinition);
        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var winnerPair = FindAdjacentPairThatProducesAMatch(created.BoardState, exclude: pair);

        repository.ConcurrentWinnerBeforeFirstWrite =
            stored => AdvanceSequenceAsAConcurrentWinner(stored, winnerPair);

        var result = await service.ExecuteSwapAsync(
            battleId,
            new SwapRequest(pair.From, pair.To, ExpectedSequence: created.Sequence, ActionId: "act-obsolete"));

        Assert.True(result!.Value.IsRejected);
        Assert.Equal(SwapRejectionReason.StaleAction, result.Value.Reason);

        // Creation only: the refused resolution wrote nothing, and the competitor's
        // write is not attributed to this request's resolution.
        Assert.Equal(1, repository.WriteCount);

        var stored = await service.GetBattleAsync(battleId);

        Assert.Equal(created.Sequence + 1, stored!.Sequence);
        Assert.Equal(ConcurrentWinnerActionId, stored.LastCommittedSwapPair!.Value.ActionId);
    }

    [Fact]
    public async Task ExecuteSwap_WithoutAPrecondition_ShouldStillCommitAgainstTheStateItRead()
    {
        // SIGNALR_PROTOCOL.md §2 item 1: the legacy form that states no
        // precondition is still accepted, and with no competing writer it resolves
        // against the state its read returned exactly as any other request does.
        var repository = new ContendedBattleStateRepository();
        var service = NewService(repository);
        var battleId = "battle-legacy-no-contention";
        var created = await service.CreateBattleAsync(battleId, Owner, PetConfiguration, BossDefinition);
        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);

        var result = await service.ExecuteSwapAsync(
            battleId,
            new SwapRequest(pair.From, pair.To, ExpectedSequence: null, ActionId: "act-legacy-committed"));

        Assert.True(result!.Value.IsAccepted);
        Assert.False(result.Value.IsIdempotentReplay);
        Assert.Equal(2, repository.WriteCount);

        var stored = await service.GetBattleAsync(battleId);

        Assert.Equal(created.Sequence + 1, stored!.Sequence);
        Assert.Equal("act-legacy-committed", stored.LastCommittedSwapPair!.Value.ActionId);
    }

    [Fact]
    public async Task ExecuteSwap_WithoutAPrecondition_ShouldNotBeReExecutedAgainstAStateThatMovedOn()
    {
        // TASK-257 / MATCH3_RULES.md §2.1.4 item 1, ADR-025 §4: the legacy form
        // states no state version it observed, so a refused compare-and-set is
        // terminal for it too. Re-resolving it against the fresh record would
        // execute a swap against a board version its caller never named and never
        // observed — the bypass the state precondition exists to prevent.
        //
        // The competitor's record advances Sequence and records a different
        // committed action while leaving the board unchanged, so a re-resolution
        // WOULD succeed and commit: the test is sensitive to the bypass rather than
        // passing because the re-resolution happens to fail a gameplay check.
        var repository = new ContendedBattleStateRepository();
        var service = NewService(repository);
        var battleId = "battle-legacy-contention";
        var created = await service.CreateBattleAsync(battleId, Owner, PetConfiguration, BossDefinition);
        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var winnerPair = FindAdjacentPairThatProducesAMatch(created.BoardState, exclude: pair);

        repository.ConcurrentWinnerBeforeFirstWrite =
            stored => AdvanceSequenceAsAConcurrentWinner(stored, winnerPair);

        var result = await service.ExecuteSwapAsync(
            battleId,
            new SwapRequest(pair.From, pair.To, ExpectedSequence: null, ActionId: "act-legacy-contended"));

        Assert.True(result!.Value.IsRejected);
        Assert.Equal(SwapRejectionReason.StaleAction, result.Value.Reason);

        // Creation only: nothing was re-executed and nothing was written.
        Assert.Equal(1, repository.WriteCount);

        var stored = await service.GetBattleAsync(battleId);

        Assert.Equal(created.Sequence + 1, stored!.Sequence);
        Assert.Equal(ConcurrentWinnerActionId, stored.LastCommittedSwapPair!.Value.ActionId);
    }

    /// <summary>
    /// The state a <b>concurrent winner</b> leaves behind for the contention tests:
    /// the same board the request under test read, with <c>Sequence</c> advanced by
    /// one and a different committed action recorded.
    ///
    /// The board is deliberately unchanged. What the contract under test turns on
    /// is the <i>state version</i> moving on (<c>REDIS_STATE.md</c> §4 items 2–3):
    /// keeping the board valid for the request's pair is what makes the test able
    /// to tell "refused because the precondition was consumed" from "refused
    /// because the new board happens not to permit the pair", so a bypass that
    /// re-resolves against the newer version would commit and be caught.
    /// </summary>
    private static BattleState AdvanceSequenceAsAConcurrentWinner(
        BattleState stored,
        SwapRequest winnerPair) =>
        stored with
        {
            Sequence = stored.Sequence + 1,
            LastCommittedSwapPair = CommittedSwapPair.FromCells(
                winnerPair.From,
                winnerPair.To,
                stored.Sequence + 1,
                ConcurrentWinnerActionId),
        };

    [Fact]
    public async Task ExecuteSwap_ShouldReturnNullForAnUnknownBattle()
    {
        // Consistent with GetBattle and ResolveBoard: an unknown battle resolves
        // nothing rather than creating state.
        var service = NewService();

        Assert.Null(await service.ExecuteSwapAsync("battle-that-does-not-exist", new SwapRequest(0, 1)));
    }

    [Fact]
    public async Task ExecuteSwap_ShouldRejectAnEmptyBattleId()
    {
        var service = NewService();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ExecuteSwapAsync(string.Empty, new SwapRequest(0, 1)));
    }

    [Fact]
    public async Task ExecuteSwap_ShouldLeaveTheBoardStableAndConsistentWithTheResolution()
    {
        // MATCH3_RULES.md §4.3 item 1 / ADR-026 §2: one atomic round commits the board
        // the BOSS's own §18a cascade produced — the Player's cascade is not the
        // round's last board write — and that board is stable: a fresh detection pass
        // finds nothing. Combo and MatchCount stay the PLAYER's counters
        // (GAME_STATE.md §2.2; BOSS_RULES.md §6.2.5 reads "Player Combo").
        //
        // The committed board and stream are re-derived from the round's own
        // documented pipeline (see ReplayRoundPipeline) rather than copied out of the
        // result, so the assertion is against GAME_RULES.md §17's steps and not
        // against "whatever the code did".
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await service.CreateBattleAsync("battle-swap-stable", Owner, PetConfiguration, BossDefinition);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Atk, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(repository, created, pinned.Board);

        var before = (await service.GetBattleAsync("battle-swap-stable"))!;
        var result = await service.ExecuteSwapAsync("battle-swap-stable", pinned.Swap);

        Assert.True(result!.Value.IsAccepted);

        var committed = result.Value.State;
        var expected = ReplayRoundPipeline(result.Value);

        // §4.3 item 1: a fresh detection pass over the committed board finds nothing.
        Assert.Empty(MatchDetector.Detect(committed.BoardState));

        // The round's last board write is the Boss's own cascade's output.
        Assert.True(committed.BoardState.CellsEqual(expected.Board));

        // §8.4 / §2.6.2 item 3: the committed stream is the state the round's own
        // draws reached.
        Assert.Equal(expected.RngState, committed.RngState);

        // The Swap's own Match total is the PLAYER's action's; the Boss's Matches are
        // not the Player's and never move either counter.
        Assert.Equal(result.Value.Resolution.TotalMatches, committed.Combo);
        Assert.Equal(before.MatchCount + result.Value.Resolution.TotalMatches, committed.MatchCount);
    }

    // -----------------------------------------------------------------------
    // Match / Combo accounting boundary (GAME_STATE.md §2.2, §5.1;
    // MATCH3_RULES.md §6)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateBattle_ShouldStartComboAndMatchCountAtZero()
    {
        // GAME_STATE.md §2.2: MatchCount = 0 and Combo = 0 for a battle that has
        // resolved no action. Both are root members of the state; there is no nested
        // PlayerState node (ADR-011 item 2).
        var service = NewService();
        var state = await service.CreateBattleAsync("battle-progression-initial", Owner, PetConfiguration, BossDefinition);

        Assert.Equal(0, state.Combo);
        Assert.Equal(0, state.MatchCount);
        Assert.Equal(BattleState.InitialCombo, state.Combo);
        Assert.Equal(BattleState.InitialMatchCount, state.MatchCount);
    }

    [Fact]
    public async Task ExecuteSwap_ShouldRecordTheResolutionMatchAndComboValues()
    {
        // GAME_STATE.md §2.2 / §5.1 item 3: the recorded state carries the finished
        // resolution's values, so a subsequent read returns them — not a value
        // derived from the board afterwards.
        var service = NewService();
        var created = await service.CreateBattleAsync("battle-progression-commit", Owner, PetConfiguration, BossDefinition);

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = await service.ExecuteSwapAsync("battle-progression-commit", pair);

        Assert.True(result!.Value.IsAccepted);

        var matches = result.Value.Resolution.Passes.Sum(pass => pass.Matches.Count);

        Assert.True(matches >= 1);
        Assert.Equal(matches, result.Value.State.Combo);
        Assert.Equal(matches, result.Value.State.MatchCount);

        // The active-state store holds the same value the result reports.
        var reread = await service.GetBattleAsync("battle-progression-commit");
        Assert.Equal(result.Value.State.Combo, reread!.Combo);
        Assert.Equal(result.Value.State.MatchCount, reread.MatchCount);
    }

    [Fact]
    public async Task ExecuteSwap_ShouldLeaveComboAndMatchCountUnchangedOnEveryRejection()
    {
        // MATCH3_RULES.md §2.1.5 item 5 / §6.1 item 3: a rejected Swap resets
        // nothing and increments nothing. The active-state store received no write,
        // so it still holds the created record and the progression values cannot
        // have moved.
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await service.CreateBattleAsync("battle-progression-rejected", Owner, PetConfiguration, BossDefinition);

        var result = await service.ExecuteSwapAsync("battle-progression-rejected", new SwapRequest(99, 100));

        Assert.True(result!.Value.IsRejected);

        // Creation was the only write; the rejection added none.
        Assert.Equal(1, repository.WriteCount);

        var reread = await service.GetBattleAsync("battle-progression-rejected");

        Assert.NotNull(reread);
        Assert.Equal(created.Combo, reread!.Combo);
        Assert.Equal(created.MatchCount, reread.MatchCount);
        Assert.Equal(0, reread.Combo);
        Assert.Equal(0, reread.MatchCount);
    }

    [Fact]
    public async Task ResolveBoard_ShouldNotChangeTheProgressionState()
    {
        // Board resolution is not a Swap (MATCH3_RULES.md §8.1 item 4): it resolves no
        // action, so it starts no Turn, counts no Match, and resets no Combo. A
        // generated board holds no Match, so nothing resolves at all.
        var service = NewService();
        var created = await service.CreateBattleAsync("battle-progression-resolution", Owner, PetConfiguration, BossDefinition);

        var resolved = await service.ResolveBoardAsync("battle-progression-resolution");

        Assert.NotNull(resolved);
        Assert.Equal(created.Combo, resolved!.Combo);
        Assert.Equal(created.MatchCount, resolved.MatchCount);
        Assert.Equal(created.PetState, resolved.PetState);
    }

    [Fact]
    public async Task ExecuteSwap_ShouldNotWriteAnyBattleEventOrAddAMessage()
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
    public async Task ExecuteSwap_ShouldNotPersistAnything()
    {
        // REDIS_STATE.md §7 item 8: the Match-3 resolution stage adds no key and no
        // persistence, and §7 leaves PetState's persistence equally unchanged
        // (SIGNALR_PROTOCOL.md §4.3 item 12). The service writes nothing but the one
        // documented active-state record: §7 items 1–2 permit no partial state and no
        // second key, and GAME_STATE.md §2.0.4 item 3 permits nothing to PostgreSQL
        // during an ordinary action resolution — TDD.md §4 item 3 keeps PostgreSQL
        // off the hot path.
        //
        // The three dictionaries are the battle's Pet loadout input, the battle's
        // Boss definition (BOSS_RULES.md §6.2–§6.4's static content, which BossState
        // deliberately does not duplicate), and the battle's equipped Relics'
        // resolved definitions (RELIC_RULES.md §2.2, §8 — the content
        // GAME_RULES.md §17 step 11 reads, which PetState.EquippedRelics[]
        // deliberately does not copy). None is a REDIS_STATE.md store, and none is
        // queried from PostgreSQL during a resolution: that is exactly why the Relic
        // content is attached here at creation (TDD.md §4 item 3). The remaining
        // fields are the ONE documented store the active-state record is written
        // through, the seed source, and the durable battle result boundary the
        // terminal paths invoke (ARCHITECTURE.md §4 item 4, DATABASE.md §1). No
        // cache, bus, logger, or second store was introduced.
        //
        // The consumed-firing record added by TASK-173 is a fourth per-battle
        // session input, not a store: BOSS_RULES.md §6.2.4 / PASSIVE_RULES.md §4
        // make a Persistent Boss Passive eligible to fire at most once per battle,
        // and that eligibility cannot be a BossState member (§6.2.4 adds none) or
        // the presence of the transient instance (which expires at step 19a). It is
        // written once per battle at the activation it authorizes and is read only
        // to answer the eligibility question, so it persists nothing, caches
        // nothing, and reaches no database.
        var fields = typeof(BattleStateService)
            .GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Select(f => f.Name)
            .ToArray();

        Assert.Equal(
            [
                "_battleResults",
                "_bossConfiguration",
                "_cardDefinitions",
                "_consumedBossPassiveFiring",
                "_petConfiguration",
                "_relicConfiguration",
                "_repository",
                "_seedSource",
                "_unpersistedResults",
            ],
            fields.OrderBy(n => n, StringComparer.Ordinal));

        // The durable boundary is reached through the Application contract, not
        // through a PostgreSQL or lifetime-specific type held here: the resolution
        // still names no database, and active-state persistence is still the
        // store's alone.
        Assert.Equal(
            typeof(GameServer.Application.Battle.IBattleResultPersistence),
            typeof(BattleStateService)
                .GetField("_battleResults", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .FieldType);
    }

    // -----------------------------------------------------------------------
    // Passive charge integration (GAME_RULES.md §17 step 10;
    // PASSIVE_RULES.md §2, §4, §5; GAME_STATE.md §2.3;
    // GAME_EVENTS.md §1, §1.1, §2)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateBattle_ShouldInitializePetStateWithTheSuppliedPassive()
    {
        // GAME_STATE.md §2.3 item 3 / SIGNALR_PROTOCOL.md §4.3 item 4: PetState is
        // present from battle creation, carrying the Passive identity and a
        // progress at the start of its first charge.
        var service = NewService();

        var state = await service.CreateBattleAsync("battle-passive-initial", Owner, PetConfiguration, BossDefinition);

        Assert.Equal(PetConfiguration.PassiveId, state.PetState.PassiveId);
        Assert.Equal(PetConfiguration.PassiveThreshold, state.PetState.PassiveProgress.Threshold);
        Assert.Equal(0, state.PetState.PassiveProgress.Current);
    }

    [Fact]
    public async Task ExecuteSwap_ShouldChargeThePassiveOncePerMatchAndWriteTheProgressBack()
    {
        // PASSIVE_RULES.md §2 item 1 / §5: every Match increases progress by 1, so a
        // committed Swap's Cascade charges as many times as it produced Matches.
        // GAME_STATE.md §2.3 / §5.1: the settled progress is written back into
        // PetState in the same post-resolution write-back as the rest of the state.
        var service = NewService();
        var created = await service.CreateBattleAsync("battle-passive-charge", Owner, PetConfiguration, BossDefinition);

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = await service.ExecuteSwapAsync("battle-passive-charge", pair);

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

        // The active-state store holds the same PetState the result reports (GAME_STATE.md §5.1).
        Assert.Equal(
            result.Value.State.PetState,
            (await service.GetBattleAsync("battle-passive-charge"))!.PetState);
    }

    [Fact]
    public async Task ExecuteSwap_ShouldReportEveryChargeWithTheBattlePassiveIdentity()
    {
        // GAME_EVENTS.md §2 item 1: PassiveId identifies the Passive that charged —
        // the value PetState.PassiveId holds, read and reported, never re-derived.
        // The Pet's own charges are the `source="pet"` ones.
        var service = NewService();
        var created = await service.CreateBattleAsync("battle-passive-identity", Owner, PetConfiguration, BossDefinition);

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = await service.ExecuteSwapAsync("battle-passive-identity", pair);

        Assert.True(result!.Value.IsAccepted);
        Assert.All(
            result.Value.Events.Where(e => e.Type == BattleEventType.PassiveCharged
                && e.PassiveCharged.Source == PassiveEventSource.Pet),
            e => Assert.Equal(PetConfiguration.PassiveId, e.PassiveCharged.PassiveId));
    }

    [Fact]
    public async Task ExecuteSwap_ShouldAppendThePassiveEventsAfterTheBoardResolutionEvents()
    {
        // GAME_EVENTS.md §1, §1.1 / GAME_RULES.md §17: the board cycle's events
        // (MatchCreated … ComboChanged) come first and the Passive stage's reports
        // follow them — one PassiveCharged per Match, and then the single
        // PassiveTriggered when the Threshold was crossed. The assembled list keeps
        // both stages' own order; nothing is re-sorted or interleaved.
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await service.CreateBattleAsync("battle-passive-order", Owner, PetConfiguration, BossDefinition);

        // ADR-026 D1: the Player's steps 15–17 run only for a confirmed ATK-Gem match,
        // so the Swap is pinned to clear ATK Gems. That is what fixes the round's
        // FIRST damage instance as the Player's — the window between the Passive run
        // and it is then exactly the Pet's own reports, with no Boss board cycle and
        // no Boss Passive interleaved into it (§6 step 6 places the Boss's after).
        var pinned = RoundTestSupport.PlayerMatch(GemType.Atk, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(repository, created, pinned.Board);

        var result = await service.ExecuteSwapAsync("battle-passive-order", pinned.Swap);

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

        // The window's right-hand boundary is the PLAYER's own instance: the round's
        // first damage report is DamageCalculated for the player→boss direction
        // (GAME_EVENTS.md §1 — the Player phase's reports precede the Boss phase's).
        Assert.Equal(BattleEventType.DamageDealt, events[firstDamage + 1].Type);
        Assert.Equal(DamageParty.Player, events[firstDamage + 1].DamageDealt.Source);

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
    public async Task ExecuteSwap_ShouldNotWriteAnyPassiveEventForARejection()
    {
        // MATCH3_RULES.md §2.1.5 item 6 / GAME_EVENTS.md §1.2: a rejected action
        // emits no Battle Event at all. The Passive stage is not reached either, so
        // no charge and no trigger is produced.
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await service.CreateBattleAsync("battle-passive-rejected", Owner, PetConfiguration, BossDefinition);

        var pair = FindAdjacentPairThatProducesNoMatch(created.BoardState);
        var result = await service.ExecuteSwapAsync("battle-passive-rejected", pair);

        Assert.True(result!.Value.IsRejected);
        Assert.Empty(result.Value.Events);

        // PASSIVE_RULES.md §2 item 1 charges per Match and a rejected Swap produces
        // none, so the Passive is not charged: creation was the only write and the
        // store still holds the created record, unchanged, with its progress exactly
        // where creation left it.
        Assert.Equal(1, repository.WriteCount);

        var reread = await service.GetBattleAsync("battle-passive-rejected");

        Assert.NotNull(reread);
        Assert.Equal(created.PetState, reread!.PetState);
        Assert.Equal(0, reread.PetState.PassiveProgress.Current);
    }

    [Fact]
    public async Task ExecuteSwap_ShouldCarryProgressAcrossSwaps()
    {
        // GAME_STATE.md §2.3: PassiveProgress is state, so it accumulates across
        // Swaps until the Threshold is reached. PASSIVE_RULES.md §2 item 1 adds each
        // Match to the progress already held, so two Swaps of M1 and M2 Matches
        // settle at min(M1 + M2, …) — and below the Threshold nothing resets.
        var service = NewService();
        var created = await service.CreateBattleAsync("battle-passive-carry", Owner, PetConfiguration, BossDefinition);

        var firstPair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var first = await service.ExecuteSwapAsync("battle-passive-carry", firstPair);
        Assert.True(first!.Value.IsAccepted);

        var afterFirst = await service.GetBattleAsync("battle-passive-carry")!;
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
        var second = await service.ExecuteSwapAsync("battle-passive-carry", secondPair);
        Assert.True(second!.Value.IsAccepted);

        var secondMatches = second.Value.Resolution.TotalMatches;
        var afterSecond = await service.GetBattleAsync("battle-passive-carry")!;

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
    public async Task ExecuteSwap_ShouldTriggerAtMostOncePerCascadeAndApplyTheResetBehavior()
    {
        // PASSIVE_RULES.md §2 item 3 / §5: the Threshold is evaluated once, after
        // the whole Cascade's Match batch, and the Passive triggers AT MOST ONCE per
        // Cascade for every Reset Behavior. This drives a Threshold of 1, so any
        // committed Swap — which always produces at least one Match
        // (MATCH3_RULES.md §2.1.2 item 4) — crosses it and triggers exactly once.
        var service = NewService();

        var configuration = new BattleStateService.PetConfiguration(
            new PetId(PetInstanceId),
            Element.Hoa,
            new PassiveId("threshold-one"),
            PassiveThreshold: 1);

        var created = await service.CreateBattleAsync("battle-passive-trigger", Owner, configuration, BossDefinition);
        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = await service.ExecuteSwapAsync("battle-passive-trigger", pair);

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
    public async Task ExecuteSwap_ShouldCarryPartialResetOverflowIntoTheNextSwap()
    {
        // PASSIVE_RULES.md §4 item 2 / §5: Partial Reset reduces progress by the
        // Threshold rather than to 0, so a Cascade that overshoots carries its
        // overflow into the next charge.
        var service = NewService();

        var configuration = new BattleStateService.PetConfiguration(
            new PetId(PetInstanceId),
            Element.Hoa,
            new PassiveId("partial-reset"),
            PassiveThreshold: 1,
            PassiveResetOverride: PassiveResetBehavior.Partial);

        var created = await service.CreateBattleAsync("battle-passive-partial", Owner, configuration, BossDefinition);
        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = await service.ExecuteSwapAsync("battle-passive-partial", pair);

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
    public async Task ExecuteSwap_ShouldKeepProgressUnderNoReset()
    {
        // PASSIVE_RULES.md §4 item 2: "No reset / persistent" — the trigger does not
        // reduce progress, so the batch total survives into the next Cascade.
        var service = NewService();

        var configuration = new BattleStateService.PetConfiguration(
            new PetId(PetInstanceId),
            Element.Hoa,
            new PassiveId("no-reset"),
            PassiveThreshold: 1,
            PassiveResetOverride: PassiveResetBehavior.NoReset);

        var created = await service.CreateBattleAsync("battle-passive-no-reset", Owner, configuration, BossDefinition);
        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = await service.ExecuteSwapAsync("battle-passive-no-reset", pair);

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
    public async Task ExecuteSwap_ShouldRecordTheResetBehaviorTheBattleWasCreatedWith()
    {
        // GAME_STATE.md §2.3 item 2: the Passive's identity is set at battle creation
        // and never changes; PASSIVE_RULES.md §4 makes the Reset Behavior a property
        // of the Passive's definition. Neither is written by a resolution.
        var service = NewService();

        var configuration = new BattleStateService.PetConfiguration(
            new PetId(PetInstanceId),
            Element.Hoa,
            new PassiveId("partial-reset"),
            PassiveThreshold: 5,
            PassiveResetOverride: PassiveResetBehavior.Partial);

        var created = await service.CreateBattleAsync("battle-passive-definition", Owner, configuration, BossDefinition);
        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = await service.ExecuteSwapAsync("battle-passive-definition", pair);

        Assert.True(result!.Value.IsAccepted);

        Assert.Equal(configuration.PassiveId, result.Value.State.PetState.PassiveId);
        Assert.Equal(PassiveResetBehavior.Partial, result.Value.State.PetState.PassiveResetOverride);
        Assert.Equal(configuration.PassiveThreshold, result.Value.State.PetState.PassiveProgress.Threshold);
    }

    [Fact]
    public async Task ResolveBoard_ShouldNotChargeThePassive()
    {
        // Board resolution is not a Swap (MATCH3_RULES.md §8.1 item 4): it resolves
        // no action and produces no Match on a generated board, so PASSIVE_RULES.md
        // §2 item 1 has nothing to charge and the Passive state is untouched.
        var service = NewService();
        var created = await service.CreateBattleAsync("battle-passive-resolution", Owner, PetConfiguration, BossDefinition);

        var resolved = await service.ResolveBoardAsync("battle-passive-resolution");

        Assert.NotNull(resolved);
        Assert.Equal(created.PetState, resolved!.PetState);
    }

    [Fact]
    public async Task ExecuteSwap_ShouldReturnTheSameWriteBackItStores()
    {
        // GAME_STATE.md §5.1: one action produces ONE post-resolution write-back. The
        // result the caller receives must therefore carry the same state the
        // active-state store holds — including the settled Passive progress — so a
        // client rendering the batch's Passive events and a client reading the state
        // cannot disagree (GAME_EVENTS.md §3 item 6: an event is never a substitute
        // for the write-back, and the write-back is never replaced by an event).
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await service.CreateBattleAsync("battle-passive-writeback", Owner, PetConfiguration, BossDefinition);

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = await service.ExecuteSwapAsync("battle-passive-writeback", pair);

        Assert.True(result!.Value.IsAccepted);

        // Creation plus the committed Swap's single write-back — no second write.
        Assert.Equal(2, repository.WriteCount);

        var stored = await service.GetBattleAsync("battle-passive-writeback");

        Assert.NotNull(stored);
        Assert.Equal(result.Value.State.BattleId, stored!.BattleId);
        Assert.Equal(result.Value.State.Turn, stored.Turn);
        Assert.Equal(result.Value.State.Sequence, stored.Sequence);
        Assert.Equal(result.Value.State.RngSeed, stored.RngSeed);
        Assert.Equal(result.Value.State.RngState, stored.RngState);
        Assert.Equal(result.Value.State.Combo, stored.Combo);
        Assert.Equal(result.Value.State.MatchCount, stored.MatchCount);
        Assert.Equal(result.Value.State.LastCommittedSwapPair, stored.LastCommittedSwapPair);
        Assert.True(result.Value.State.BoardState.CellsEqual(stored.BoardState));
        Assert.Equal(result.Value.State.BossState, stored.BossState);
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
    public async Task ExecuteSwap_ShouldReduceBossHpThroughTheDamagePipeline()
    {
        // GAME_RULES.md §17 steps 15–17 / COMBAT_RULES.md §3 / ADR-026 D1: the
        // Application boundary invokes the Domain Damage Pipeline as part of the round,
        // but only for a confirmed ATK-Gem match — so the round's Swap is pinned to
        // clear ATK Gems (RoundTestSupport) and the Boss's HP is asserted against that
        // one instance.
        //
        // The expected damage is derived from the documented formula and the values
        // the resolution itself reports — never from the pipeline's output:
        //   Base  = PetState.ATK + Resources.BaseDamagePool
        //   ×     ComboModifiers.Default for the accounted Combo   (GAME_RULES.md §5)
        //   ×     ElementModifiers.Default for the resolved matchup (ELEMENT_RULES.md §2.2)
        //   ×     1.00 (no Other Modifiers in MVP)
        //   ×     K / (K + BossState.DEF), K = 100                 (COMBAT_RULES.md §3.2)
        //   truncate toward zero, minimum 0                        (COMBAT_RULES.md §3 step 6)
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await service.CreateBattleAsync("battle-damage-hp", Owner, PetConfiguration, BossDefinition);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Atk, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(repository, created, pinned.Board);

        var before = (await service.GetBattleAsync("battle-damage-hp"))!;
        var result = await service.ExecuteSwapAsync("battle-damage-hp", pinned.Swap);

        Assert.True(result!.Value.IsAccepted);

        var state = result.Value.State;
        var generation = result.Value.Resources;

        var expectedDamage = DamagePipeline.Calculate(
            new DamagePipeline.DamageInputs(
                Attack: before.PetState.ATK,
                BaseDamagePool: generation.BaseDamagePool,
                Combo: state.Combo,
                AttackerElement: before.PetState.Element,
                DefenderElement: before.BossState.Element,
                DefenderDefense: before.BossState.DEF,
                DefenderHp: before.BossState.HP,
                Source: DamageParty.Player,
                Target: DamageParty.Boss,
                AttackerCrit: before.PetState.Crit,
                RngState: result.Value.Resolution.RngState),
            ComboModifiers.Default,
            ElementModifiers.Default);

        var expected = expectedDamage.DamageDealt.Amount;

        Assert.True(expected > 0, "this fixture's Swap must deal damage for the assertion to mean anything");

        // The instance is the one the round reported, and the committed Boss HP is
        // that instance applied and then the Boss's own §18a HP-Gem heal, clamped to
        // MaxHP (ADR-026 §4; COMBAT_RULES.md §4 item 1) — the two HP writes of the
        // round, and no others.
        Assert.Equal(
            expected,
            result.Value.Events
                .Single(e => e.Type == BattleEventType.DamageDealt
                    && e.DamageDealt.Source == DamageParty.Player)
                .DamageDealt.Amount);

        Assert.Equal(ExpectedBossHpAfterRound(before, result.Value, expected), state.BossState.HP);
    }

    [Fact]
    public async Task ExecuteSwap_ShouldEmitTheThreeDamageEventsInTheDocumentedOrder()
    {
        // GAME_EVENTS.md §1: DamageCalculated, DamageDealt, DamageTaken — for each
        // damage instance. ADR-026 D1 gates each direction on the acting side's own
        // confirmed ATK-Gem match, so one atomic round carries AT MOST two instances:
        // the Player's steps 15–17 (present here, because the pinned board's cascade
        // clears an ATK Gem) and — iff the Boss's own swap also cleared an ATK Gem —
        // its §17 step 18d instance. The legacy fallback basic attack is retired, so
        // a Boss instance never appears without a Boss ATK match.
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await service.CreateBattleAsync("battle-damage-events", Owner, PetConfiguration, BossDefinition);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Atk, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(repository, created, pinned.Board);

        var result = await service.ExecuteSwapAsync("battle-damage-events", pinned.Swap);

        Assert.True(result!.Value.IsAccepted);

        var damageEvents = result.Value.Events
            .Where(e => e.Type is BattleEventType.DamageCalculated
                or BattleEventType.DamageDealt
                or BattleEventType.DamageTaken)
            .ToArray();

        // The Player's instance is the round's first triple, in the documented
        // per-instance order: COMBAT_RULES.md §3.1 runs steps 1–6 in order for every
        // instance and GAME_EVENTS.md §1 reports Calc/Dealt/Taken per instance.
        Assert.Equal(
            [
                BattleEventType.DamageCalculated,
                BattleEventType.DamageDealt,
                BattleEventType.DamageTaken,
            ],
            damageEvents.Take(3).Select(e => e.Type));

        Assert.Equal(DamageParty.Player, damageEvents[1].DamageDealt.Source);
        Assert.Equal(DamageParty.Boss, damageEvents[1].DamageDealt.Target);

        // The Boss's own instance exists exactly when the Boss's own swap produced a
        // confirmed ATK-Gem match (GAME_RULES.md §17 step 18d, ADR-026 D1).
        var bossClearedAtkGem = RoundTestSupport.BossClearedAtkGem(result.Value);
        Assert.Equal(bossClearedAtkGem ? 6 : 3, damageEvents.Length);

        if (bossClearedAtkGem)
        {
            Assert.Equal(
                [
                    BattleEventType.DamageCalculated,
                    BattleEventType.DamageDealt,
                    BattleEventType.DamageTaken,
                ],
                damageEvents.Skip(3).Select(e => e.Type));

            Assert.Equal(DamageParty.Boss, damageEvents[4].DamageDealt.Source);
            Assert.Equal(DamageParty.Player, damageEvents[4].DamageDealt.Target);
        }

        // GAME_EVENTS.md §1: the Player phase's own reports — its damage instance
        // included — precede every event of the Boss response phase, whose board cycle
        // is the next run of Match-3 events (ADR-026 §2).
        var bossBoardEventIndex = FirstBossBoardEventIndex(result.Value);

        Assert.True(bossBoardEventIndex >= 0, "an alive Boss always acts on the shared board (§17 step 18a)");
        Assert.True(
            result.Value.Events.ToList().FindIndex(
                e => e.Type == BattleEventType.DamageCalculated) < bossBoardEventIndex);
    }

    [Fact]
    public async Task ExecuteSwap_DamageCalculated_ShouldCarryTheFullDocumentedBreakdown()
    {
        // GAME_EVENTS.md §2: "DamageCalculated: Base, Combo Modifier, Element
        // Modifier, Other Modifiers, Defense, Final Damage (full breakdown, for
        // client feedback)". All six members must be reported, including the
        // pass-through Other Modifiers (COMBAT_RULES.md §3 step 4).
        //
        // ADR-026 D1: the Player's instance is the round's first DamageCalculated only
        // when the Player's own Swap cleared an ATK Gem, so the board is pinned to do
        // exactly that; the Boss's own §18d instance (when it exists) follows it.
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await service.CreateBattleAsync("battle-damage-breakdown", Owner, PetConfiguration, BossDefinition);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Atk, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(repository, created, pinned.Board);

        var before = (await service.GetBattleAsync("battle-damage-breakdown"))!;
        var result = await service.ExecuteSwapAsync("battle-damage-breakdown", pinned.Swap);

        // The player's instance is the FIRST DamageCalculated — the Boss's Response
        // instance follows it (GAME_RULES.md §17 steps 15–17 then 18a–18d).
        Assert.True(result!.Value.IsAccepted);

        var calculation = result.Value.Events
            .First(e => e.Type == BattleEventType.DamageCalculated)
            .DamageCalculated;

        var generation = result.Value.Resources;

        Assert.Equal(before.PetState.ATK + generation.BaseDamagePool, calculation.Base);
        Assert.Equal(
            ComboModifiers.Default.NumeratorFor(result.Value.State.Combo) / 100d,
            calculation.ComboModifier);
        Assert.Equal(
            ElementModifiers.Default.For(
                ElementMatchups.Resolve(before.PetState.Element, before.BossState.Element)),
            calculation.ElementModifier);
        var expectedCritRoll = new Pcg32(result.Value.Resolution.RngState.State, result.Value.Resolution.RngState.Increment).NextBounded(100);
        var expectedOtherModifier = expectedCritRoll < before.PetState.Crit ? 1.50 : 1.00;
        Assert.Equal(expectedOtherModifier, calculation.OtherModifiers);
        Assert.True(calculation.Defense > 0d);
        Assert.True(calculation.FinalDamage >= 0);
    }

    [Fact]
    public async Task ExecuteSwap_DamageDealtAndTaken_ShouldAgreeWithTheAppliedHpChange()
    {
        // GAME_EVENTS.md §2 gives both events "source, target, Final Damage amount".
        // They describe ONE application of damage, so the amount both report must be
        // exactly the application the committed state shows — an event is never a
        // substitute for the write-back and never disagrees with it
        // (GAME_EVENTS.md §3 item 6, GAME_STATE.md §5.1).
        //
        // This is the PLAYER's instance: the first DamageDealt, whose source is the
        // player (COMBAT_RULES.md §3) — present because the pinned board's cascade
        // clears an ATK Gem (ADR-026 D1).
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await service.CreateBattleAsync("battle-damage-agree", Owner, PetConfiguration, BossDefinition);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Atk, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(repository, created, pinned.Board);

        var before = (await service.GetBattleAsync("battle-damage-agree"))!;
        var result = await service.ExecuteSwapAsync("battle-damage-agree", pinned.Swap);

        Assert.True(result!.Value.IsAccepted);

        var dealt = result.Value.Events.First(e => e.Type == BattleEventType.DamageDealt).DamageDealt;
        var taken = result.Value.Events.First(e => e.Type == BattleEventType.DamageTaken).DamageTaken;

        Assert.Equal(DamageParty.Player, dealt.Source);
        Assert.Equal(DamageParty.Boss, dealt.Target);
        Assert.Equal(dealt.Amount, taken.Amount);
        Assert.Equal(dealt.Source, taken.Source);
        Assert.Equal(dealt.Target, taken.Target);

        // The Boss's HP is written twice in the round and by nothing else: the player's
        // instance (this one) and the Boss's own §18a HP-Gem heal, clamped at MaxHP
        // (ADR-026 §4; COMBAT_RULES.md §4 item 1). The committed value is exactly that
        // composition of the amount above — so the reports and the write-back agree.
        Assert.Equal(
            ExpectedBossHpAfterRound(before, result.Value, dealt.Amount),
            result.Value.State.BossState.HP);
    }

    [Fact]
    public async Task ExecuteSwap_ShouldStoreTheBossHpItReportsInTheSameWriteBack()
    {
        // GAME_STATE.md §5.1: one action, one write-back. The Boss's post-damage HP is
        // part of that single value, so the active-state store holds exactly the state
        // the result carries — a client reading the DamageDealt event and a client
        // reading the state cannot disagree.
        //
        // The HP the round reports is the Player's ATK-gated instance (ADR-026 D1, so
        // the board is pinned to clear ATK Gems) applied to the Boss, then the Boss's
        // own §18a HP-Gem heal clamped at MaxHP (ADR-026 §4). Those are the round's
        // two Boss-HP writes, so the stored value is the documented composition of
        // them rather than a bare inequality.
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await service.CreateBattleAsync("battle-damage-writeback", Owner, PetConfiguration, BossDefinition);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Atk, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(repository, created, pinned.Board);

        var before = (await service.GetBattleAsync("battle-damage-writeback"))!;
        var result = await service.ExecuteSwapAsync("battle-damage-writeback", pinned.Swap);

        Assert.True(result!.Value.IsAccepted);

        var stored = await service.GetBattleAsync("battle-damage-writeback")!;

        Assert.Equal(result.Value.State.BossState, stored.BossState);

        var playerDamage = result.Value.Events
            .Single(e => e.Type == BattleEventType.DamageDealt
                && e.DamageDealt.Source == DamageParty.Player)
            .DamageDealt.Amount;

        Assert.Equal(ExpectedBossHpAfterRound(before, result.Value, playerDamage), stored.BossState.HP);

        // COMBAT_RULES.md §3 step 6 / GAME_RULES.md §1.4: overkill stops at 0, so the
        // committed Boss HP is never negative.
        Assert.True(stored.BossState.HP >= 0);
    }

    [Fact]
    public async Task ExecuteSwap_ShouldNotDamageTheBossForARejectedSwap()
    {
        // MATCH3_RULES.md §2.1.5: a rejected action writes nothing and emits no Battle
        // Event (GAME_EVENTS.md §1.2). The Damage Pipeline is downstream of the commit,
        // so a rejected Swap deals no damage, produces no Damage event, and leaves the
        // Boss's HP exactly as it was.
        var service = NewService();
        var created = await service.CreateBattleAsync("battle-damage-rejected", Owner, PetConfiguration, BossDefinition);

        var pair = FindAdjacentPairThatProducesNoMatch(created.BoardState);
        var result = await service.ExecuteSwapAsync("battle-damage-rejected", pair);

        Assert.True(result!.Value.IsRejected);
        Assert.Empty(result.Value.Events);

        var stored = await service.GetBattleAsync("battle-damage-rejected")!;

        Assert.Equal(created.BossState.HP, stored.BossState.HP);
    }

    [Fact]
    public async Task ExecuteSwap_ShouldCarryEveryUnrelatedBossFieldAcrossUnchanged()
    {
        // COMBAT_RULES.md §3 applies damage and nothing else; the Boss Response
        // (BOSS_RULES.md §3–§5) additionally charges the Skill and may transition
        // State on Enrage. What must NOT move is every field no step of this action
        // owns: the Boss's identity, its Element, and its base stats are configuration
        // set at battle creation (GAME_STATE.md §2.4, BOSS_RULES.md §6.1) and the
        // resolution rewrites none of them.
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await service.CreateBattleAsync("battle-damage-bossfield", Owner, PetConfiguration, BossDefinition);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Atk, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(repository, created, pinned.Board);

        var before = (await service.GetBattleAsync("battle-damage-bossfield"))!;
        var result = await service.ExecuteSwapAsync("battle-damage-bossfield", pinned.Swap);

        Assert.True(result!.Value.IsAccepted);

        var after = result.Value.State.BossState;

        Assert.Equal(before.BossState.BossId, after.BossId);
        Assert.Equal(before.BossState.Element, after.Element);
        Assert.Equal(before.BossState.MaxHP, after.MaxHP);
        Assert.Equal(before.BossState.ATK, after.ATK);
        Assert.Equal(before.BossState.DEF, after.DEF);
        Assert.Equal(before.BossState.PassiveId, after.PassiveId);

        // The Boss's HP is written by exactly two steps of this round: the Player's
        // ATK-gated instance (ADR-026 D1) and the Boss's own §18a HP-Gem heal clamped
        // at MaxHP. Nothing else in the round reaches it.
        var playerDamage = result.Value.Events
            .Single(e => e.Type == BattleEventType.DamageDealt
                && e.DamageDealt.Source == DamageParty.Player)
            .DamageDealt.Amount;

        Assert.Equal(ExpectedBossHpAfterRound(before, result.Value, playerDamage), after.HP);

        // And the Skill charge followed the documented rule (GAME_STATE.md §2.4.3 /
        // BOSS_RULES.md §6.3 / ADR-026 D2), whichever branch this board lands on:
        //
        //   the Skill did not fire -> the charge accumulated +1 per POWER match group
        //                             the BOSS's own swap produced, clamped at the
        //                             Skill's ChargeRequirement
        //   the Skill fired        -> the charge was reset to 0
        //
        // The Player's POWER matches never charge the Boss (D2), so the PLAYER's Match
        // total is not this counter's unit; the branch is read from the events, not
        // assumed.
        var skillFired = result.Value.Events.Any(e => e.Type == BattleEventType.BossSkillCast);
        var bossPowerMatchGroups = RoundTestSupport.BossPowerMatchGroups(result.Value);

        var expectedSkillCharge = skillFired
            ? 0
            : Math.Min(before.BossState.SkillCharge + bossPowerMatchGroups, BossDefinition.SkillChargeRequirement);

        Assert.Equal(expectedSkillCharge, after.SkillCharge);

        // A fired Skill must have met both documented conditions before it did
        // (GAME_STATE.md §2.4.3: SkillCharge ≥ ChargeRequirement AND SkillCooldown = 0).
        if (skillFired)
        {
            Assert.True(
                before.BossState.SkillCharge + bossPowerMatchGroups
                    >= BossDefinition.SkillChargeRequirement);

            Assert.Equal(BossDefinition.SkillCooldownTurns, after.SkillCooldown);
        }
    }

    [Fact]
    public async Task ExecuteSwap_ShouldAdvanceRngStateForDamageCritEvaluation()
    {
        // AGENTS.md §11 / ADR-009 / COMBAT_RULES.md §3.3 item 2: every Crit evaluation
        // draws exactly one bounded-100 selection from the single PCG32 stream
        // (BattleState.RngState). Under ADR-026 §2 one atomic round makes four kinds of
        // draw:
        //   * the Player Swap's Spawn draws              (MATCH3_RULES.md §4.5 item 4)
        //   * the Player attack's one bound-100 Crit     (COMBAT_RULES.md §3.3 item 2)
        //   * the Boss's own swap's Spawn draws          (GAME_RULES.md §17 step 18a)
        //   * the Boss attack's one bound-100 Crit       (GAME_RULES.md §17 step 18d)
        // Each Crit draw happens iff that side's cascade cleared an ATK Gem, because
        // the Damage Pipeline only runs for a qualifying attack (ADR-026 D1).
        //
        // The stronger, exact form is asserted — the committed RngState equals the
        // state the round's own two board actions and two Crit evaluations reached,
        // re-derived by ReplayRoundPipeline — rather than the weaker, still-true
        // "the state advanced and differs from the pre-round value". GAME_STATE.md
        // §2.6.2 item 3 makes RngState a function of exactly the draws made, so an
        // exact equality is what that contract states, and only it detects a missing
        // or an extra draw (e.g. a revived fallback Crit roll for a non-qualifying
        // attack).
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await service.CreateBattleAsync("battle-damage-rng", Owner, PetConfiguration, BossDefinition);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Atk, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(repository, created, pinned.Board);

        var before = (await service.GetBattleAsync("battle-damage-rng"))!;
        var result = await service.ExecuteSwapAsync("battle-damage-rng", pinned.Swap);

        Assert.True(result!.Value.IsAccepted);

        Assert.Equal(ReplayRoundPipeline(result.Value).RngState, result.Value.State.RngState);
        Assert.NotEqual(before.RngState, result.Value.State.RngState);
    }

    // -----------------------------------------------------------------------
    // End Turn step 19a — Status Effect duration/expiry
    // (GAME_RULES.md §17 step 19a, GAME_STATE.md §5.1.1)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteSwap_ShouldRunStep19aAtItsDocumentedPositionInTheResolution()
    {
        // GAME_RULES.md §17 step 19a: End Turn is step 19 — after the Boss Response
        // (step 18) — and step 19a "fires once per resolved Turn". This asserts the
        // position rather than only the effect: the committed state the resolution
        // returns has already been through the pass, so an instance with duration 1
        // applied before the Turn is consumed and removed in that same resolution
        // (GAME_STATE.md §5.1.1 items 2, 4, 5).
        //
        // The instance is seeded into the created battle's state directly, because
        // no gameplay source applies a Status Effect yet — producing them is the
        // downstream Boss Skill task's act (TASK-095 Scope: this task implements the
        // state lifecycle, not the systems that produce instances).
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await service.CreateBattleAsync(
            "battle-step19a-position", Owner, PetConfiguration, BossDefinition);

        var seeded = created with
        {
            BossState = created.BossState with
            {
                ActiveStatusEffects =
                [
                    StatusEffect.TurnBased(
                        "Root", StatusEffectType.BuffDebuff, StatusEffectSource.Boss,
                        magnitude: -30, duration: 1, targetStat: "ATK"),
                ],
            },
        };

        await repository.TryUpdateAsync(seeded, created.Sequence);

        var pair = FindAdjacentPairThatProducesAMatch(seeded.BoardState);
        var result = await service.ExecuteSwapAsync("battle-step19a-position", pair);

        Assert.True(result!.Value.IsAccepted);

        // One resolved Turn consumed the duration-1 instance entirely: it is gone
        // from the committed state, because expiry is a removal in the same pass
        // and 0 is never observable (§5.1.1 items 4–5, §2.3.1 item 8).
        Assert.Empty(result.Value.State.BossState.ActiveStatusEffects);

        // The pass ran BEFORE the single write-back (§5.1.1 item 9), so the stored
        // record is the post-19a state and not the pre-19a one.
        var reread = await service.GetBattleAsync("battle-step19a-position");

        Assert.NotNull(reread);
        Assert.Empty(reread!.BossState.ActiveStatusEffects);
    }

    [Fact]
    public async Task ExecuteSwap_ShouldConsumeExactlyOneTurnOfDurationPerResolvedTurn()
    {
        // GAME_STATE.md §5.1.1 item 2 / COMBAT_RULES.md §5.3 DR2: exactly one
        // decrement per resolved Turn, and §5.1.1 item 3 makes the apply timing
        // irrelevant — an instance present before step 19a is decremented once at
        // that Turn's step 19a.
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await service.CreateBattleAsync(
            "battle-step19a-once", Owner, PetConfiguration, BossDefinition);

        var seeded = created with
        {
            BossState = created.BossState with
            {
                ActiveStatusEffects =
                [
                    StatusEffect.TurnBased(
                        "Root", StatusEffectType.BuffDebuff, StatusEffectSource.Boss,
                        magnitude: -30, duration: 5, targetStat: "ATK"),
                ],
            },
        };

        await repository.TryUpdateAsync(seeded, created.Sequence);

        var pair = FindAdjacentPairThatProducesAMatch(seeded.BoardState);
        var result = await service.ExecuteSwapAsync("battle-step19a-once", pair);

        Assert.True(result!.Value.IsAccepted);

        // 5 - 1 = 4: exactly one Turn consumed, never two.
        var instance = Assert.Single(result.Value.State.BossState.ActiveStatusEffects);

        Assert.Equal("Root", instance.Id);
        Assert.Equal(4, instance.RemainingTurns);
    }

    [Fact]
    public async Task ExecuteSwap_ShouldNotRunStep19aForARejectedAction()
    {
        // GAME_STATE.md §5.1.1 item 11: "A rejected action is not a resolution
        // (§5.1 item 6), so no instance is applied, decremented, or removed, and
        // step 19a does not run."
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await service.CreateBattleAsync(
            "battle-step19a-rejected", Owner, PetConfiguration, BossDefinition);

        var seeded = created with
        {
            BossState = created.BossState with
            {
                ActiveStatusEffects =
                [
                    StatusEffect.TurnBased(
                        "Root", StatusEffectType.BuffDebuff, StatusEffectSource.Boss,
                        magnitude: -30, duration: 1, targetStat: "ATK"),
                ],
            },
        };

        await repository.TryUpdateAsync(seeded, created.Sequence);

        // An out-of-range request is rejected (MATCH3_RULES.md §2.1.4).
        var result = await service.ExecuteSwapAsync(
            "battle-step19a-rejected", new SwapRequest(99, 100));

        Assert.True(result!.Value.IsRejected);

        // The duration-1 instance is untouched: had step 19a run, it would have
        // expired. A rejected action mutates nothing (§5.1.1 item 11).
        var reread = await service.GetBattleAsync("battle-step19a-rejected");

        Assert.NotNull(reread);
        var instance = Assert.Single(reread!.BossState.ActiveStatusEffects);

        Assert.Equal(1, instance.RemainingTurns);
    }

    [Fact]
    public async Task ExecuteSwap_ShouldNotDecrementATriggerBasedStatusEffect()
    {
        // GAME_STATE.md §5.1.1 item 7 / COMBAT_RULES.md §5.3.2: an instance carrying
        // ExpiryCondition is not touched by the step 19a countdown — it is removed by
        // its own documented trigger, and the pass "must not invent a duration for
        // it". A triggered instance therefore survives the resolution unchanged.
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await service.CreateBattleAsync(
            "battle-step19a-trigger", Owner, PetConfiguration, BossDefinition);

        var seeded = created with
        {
            BossState = created.BossState with
            {
                ActiveStatusEffects =
                [
                    StatusEffect.TriggerBased(
                        "Shield", StatusEffectType.Shield, StatusEffectSource.Player,
                        magnitude: 200, expiryCondition: StatusEffect.ShieldDepletedCondition),
                ],
            },
        };

        await repository.TryUpdateAsync(seeded, created.Sequence);

        var pair = FindAdjacentPairThatProducesAMatch(seeded.BoardState);
        var result = await service.ExecuteSwapAsync("battle-step19a-trigger", pair);

        Assert.True(result!.Value.IsAccepted);

        var instance = Assert.Single(result.Value.State.BossState.ActiveStatusEffects);

        Assert.Equal("Shield", instance.Id);
        Assert.Null(instance.RemainingTurns);
        Assert.Equal(StatusEffect.ShieldDepletedCondition, instance.ExpiryCondition);
    }

    [Fact]
    public async Task ExecuteSwap_ShouldRevertBossStateToIdleWhenStunExpiresAtStep19a()
    {
        // GAME_STATE.md §5.1.1 item 8 / §2.4.5: when a Stun instance expires at step
        // 19a, BossState.State reverts to Idle in the same resolution, so State never
        // disagrees with the instance's presence.
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await service.CreateBattleAsync(
            "battle-step19a-stun", Owner, PetConfiguration, BossDefinition);

        var seeded = created with
        {
            BossState = created.BossState with
            {
                State = BossStateKind.Stunned,
                ActiveStatusEffects =
                [
                    StatusEffect.TurnBased(
                        "Stun", StatusEffectType.State, StatusEffectSource.Boss,
                        magnitude: 0, duration: 1),
                ],
            },
        };

        await repository.TryUpdateAsync(seeded, created.Sequence);

        var pair = FindAdjacentPairThatProducesAMatch(seeded.BoardState);
        var result = await service.ExecuteSwapAsync("battle-step19a-stun", pair);

        Assert.True(result!.Value.IsAccepted);
        Assert.Empty(result.Value.State.BossState.ActiveStatusEffects);
        Assert.Equal(BossStateKind.Idle, result.Value.State.BossState.State);
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

    // -----------------------------------------------------------------------
    // Atomic-round derivation helpers (ADR-026 §2/§8, GAME_RULES.md §17)
    //
    // These do not re-implement a rule: they call the Domain stages the round
    // itself calls, in the order the documents fix, so a test can state the
    // board, the stream, and the Boss phase's resources the round must carry
    // instead of restating whatever the implementation produced.
    // -----------------------------------------------------------------------

    /// <summary>
    /// The board, the Boss phase's generated resources, and the RNG stream one
    /// committed atomic round must have reached — re-derived from the round's own
    /// committed reports by running the documented steps in the order
    /// <c>BattleStateService.ResolveSwapAsync</c> fixes them (<c>GAME_RULES.md</c>
    /// §17, <c>ADR-026</c> §2 and §8).
    ///
    /// <code>
    /// Player resolution's retained stream          MATCH3_RULES.md §4.5 item 4
    ///   + one bound-100 Crit draw iff Atk > 0      COMBAT_RULES.md §3.3 item 2, ADR-026 D1
    ///   + BossAiEngine.SelectMove on that board    GAME_RULES.md §17 step 18a, ADR-026 §8
    ///   + CascadeResolver over the Boss's swap     MATCH3_RULES.md §4
    ///   + ResourceGenerator over that cascade      COMBAT_RULES.md §2
    ///   + one bound-100 Crit draw iff Atk > 0      GAME_RULES.md §17 step 18d
    /// </code>
    ///
    /// <b>The Boss HP terminal check short-circuits the phase.</b> A Boss the Player's
    /// own instance has just reduced to 0 HP makes no swap, generates nothing, and
    /// draws no Crit: <c>BOSS_RULES.md</c> §5 item 4 ends the battle "with no Boss
    /// Response", so the round's board and stream are the Player phase's.
    /// </summary>
    private static RoundPipelineReplay ReplayRoundPipeline(SwapExecutionResult result)
    {
        // The Player resolution's own report is where its Spawn draws left the stream
        // (MATCH3_RULES.md §4.5 item 4), and the Player attack's Crit selection is the
        // first draw the Application layer adds on top of it.
        var playerPhaseRng = new Pcg32(
            result.Resolution.RngState.State,
            result.Resolution.RngState.Increment);

        if (result.Resources.ClearedGemCounts.Atk > 0)
        {
            playerPhaseRng.NextBounded(DamagePipeline.CritRollBound);
        }

        if (result.State.BossState.HP == 0)
        {
            return new RoundPipelineReplay(
                result.Resolution.Board,
                ResourceGeneration.None,
                playerPhaseRng.CurrentState);
        }

        // §17 step 18a: the Boss selects one legal match-producing swap on the shared
        // board. Selection itself draws nothing; only a deadlocked board's bounded
        // deterministic reshuffle consumes the stream, and the engine reports both the
        // board it acted on and the stream it reached (ADR-026 §8,
        // BossMoveSelectionResult).
        var bossMove = BossAiEngine.SelectMove(
            result.Resolution.Board,
            new Pcg32(playerPhaseRng.CurrentState.State, playerPhaseRng.CurrentState.Increment));

        var bossResolution = CascadeResolver.Resolve(
            bossMove.Board.WithSwapped(bossMove.SelectedMove.From, bossMove.SelectedMove.To),
            new Pcg32(bossMove.RngState.State, bossMove.RngState.Increment),
            swapOriginIndex: null);

        var bossGeneration = ResourceGenerator.Generate(bossResolution);

        // §17 step 18d: the Boss's own instance draws its Crit selection iff its
        // cascade cleared an ATK Gem — the same gate the Player's carries (D1).
        var bossPhaseRng = new Pcg32(bossResolution.RngState.State, bossResolution.RngState.Increment);

        if (bossGeneration.ClearedGemCounts.Atk > 0)
        {
            bossPhaseRng.NextBounded(DamagePipeline.CritRollBound);
        }

        return new RoundPipelineReplay(
            bossResolution.Board,
            bossGeneration,
            bossPhaseRng.CurrentState);
    }

    /// <summary>
    /// The Boss's committed HP after one atomic round: the Player's ATK-gated instance
    /// applied to it, then the Boss's own §17 step 18a HP-Gem heal clamped at
    /// <c>MaxHP</c> (<c>ADR-026</c> §4, <c>COMBAT_RULES.md</c> §4 item 1).
    ///
    /// Those are the round's only two Boss-HP writes in these scenarios: the Boss's
    /// DEF-Gem Shield is created after the Player's instance, and the MVP Boss here
    /// (<c>Hỏa Long</c>) declares no HP-restoring Passive (<c>BOSS_RULES.md</c> §6.2).
    /// The <c>Math.Max(0, …)</c> is <c>COMBAT_RULES.md</c> §3 step 6's own "minimum 0"
    /// / <c>GAME_RULES.md</c> §1.4's clamp.
    /// </summary>
    private static int ExpectedBossHpAfterRound(
        BattleState before,
        SwapExecutionResult result,
        int playerFinalDamage)
    {
        var afterPlayerInstance = Math.Max(0, before.BossState.HP - playerFinalDamage);
        var healed = afterPlayerInstance + ReplayRoundPipeline(result).BossResources.HealPool;

        return Math.Min(healed, before.BossState.MaxHP);
    }

    /// <summary>
    /// The position of the first event of the <b>Boss response phase</b> — the first
    /// Match-3 board event after the Player action's own board-events prefix
    /// (<c>GAME_EVENTS.md</c> §1, <c>ADR-026</c> §2), or <c>-1</c> when the phase
    /// produced none because the Boss HP terminal check skipped it
    /// (<c>BOSS_RULES.md</c> §5 item 4).
    /// </summary>
    private static int FirstBossBoardEventIndex(SwapExecutionResult result)
    {
        var events = result.Events;
        var index = RoundTestSupport.PlayerBoardEvents(result).Count;

        while (index < events.Count && !IsBoardEvent(events[index]))
        {
            index++;
        }

        return index < events.Count ? index : -1;
    }

    /// <summary>
    /// Whether a Battle Event belongs to a Match-3 board cycle
    /// (<c>GAME_EVENTS.md</c> §1.1) — the same reading
    /// <see cref="RoundTestSupport.BossBoardEvents"/> delimits the Boss's cycle with.
    /// </summary>
    private static bool IsBoardEvent(BattleEvent battleEvent) =>
        battleEvent.Type
            is BattleEventType.MatchCreated
            or BattleEventType.CascadeCreated
            or BattleEventType.GemMatched
            or BattleEventType.ComboChanged;

    /// <summary>
    /// The board, Boss-phase resources, and stream
    /// <see cref="ReplayRoundPipeline"/> derives for one atomic round.
    /// </summary>
    private readonly record struct RoundPipelineReplay(
        BoardState Board,
        ResourceGeneration BossResources,
        RngState RngState);
}