using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using Xunit;

namespace GameServer.Application.Tests;

/// <summary>
/// The full battle-end lifecycle through the real resolution pipeline —
/// <c>ARCHITECTURE.md</c> §4 item 4, <c>DATABASE.md</c> §1,
/// <c>REDIS_STATE.md</c> §3, <c>GAME_EVENTS.md</c> §2.
///
/// <code>
/// Redis BattleState
///       ↓
/// terminal action          (a committed Swap that ends the battle)
///       ↓
/// BattleResult             (DATABASE.md §1)
///       ↓
/// PostgreSQL               (the durable write)
///       ↓
/// Redis delete             (REDIS_STATE.md §3 — only after the write)
/// </code>
///
/// <b>Why this suite exists beside <see cref="BattleResultServiceTests"/>.</b>
/// That suite drives the battle-end boundary directly. This one drives the real
/// <see cref="BattleStateService"/> over the real Domain pipeline, so what is
/// verified is that a resolution which actually emits
/// <c>BattleWon</c>/<c>BattleLost</c> reaches the documented persistence step —
/// and that a resolution which emits neither does not. Both facts are contract
/// requirements (<c>DATABASE.md</c> §1, <c>GAME_EVENTS.md</c> §2) that a
/// boundary-only test cannot establish.
/// </summary>
public class BattleResultTerminalFlowTests
{
    private static readonly BattleStateService.PetConfiguration Pet =
        new(new PetId("pet_instance_1"), Element.Hoa, new PassiveId("xich-lang"), PassiveThreshold: 5);

    private static readonly PlayerId Owner = new("player_terminal_flow_owner");

    /// <summary>
    /// A Boss one committed Swap's damage kills — the documented terminal path of
    /// <c>GAME_RULES.md</c> §1.4. Its stats are configuration, not invariants
    /// (<c>BOSS_RULES.md</c> §6.1), so restating the HP for a scenario is what the
    /// document permits; no rule is invented.
    /// </summary>
    private static BossDefinition FragileBoss() => BossDefinitions.HoaLong with { MaxHP = 1 };

    /// <summary>
    /// A Boss that kills the player in one Basic Attack — the other terminal path.
    /// </summary>
    private static BossDefinition LethalBoss() =>
        BossDefinitions.HoaLong with
        {
            ATK = 100_000,
            SkillDefinition = BossDefinitions.HoaLong.SkillDefinition
                with { ChargeRequirement = int.MaxValue },
            PassiveDefinition = BossDefinitions.HoaLong.PassiveDefinition
                with { Threshold = null },
        };

    private sealed record Harness(
        BattleStateService Battles,
        InMemoryBattleStateRepository Store,
        InMemoryBattleResultRepository Results,
        ScriptedBossDefinitionLookup BossLookup,
        IBattleResultPersistence Persistence)
    {
        public static Harness Create(BossDefinition? resolvableBoss = null)
        {
            var store = new InMemoryBattleStateRepository();
            var results = new InMemoryBattleResultRepository();
            var bossLookup = resolvableBoss is null
                ? new ScriptedBossDefinitionLookup()
                : ScriptedBossDefinitionLookup.Resolving(resolvableBoss);

            var persistence = new DirectBattleResultPersistence(
                new BattleResultService(results, bossLookup, store, TimeProvider.System));

            return new Harness(
                new BattleStateService(store, new FixedRngSeedSource(), persistence),
                store,
                results,
                bossLookup,
                persistence);
        }
    }

    /// <summary>
    /// The Application-side durable step, wired directly: the production
    /// composition reaches the scoped service through the composition root's
    /// resolver, and this is the same call without the DI scope.
    /// </summary>
    private sealed class DirectBattleResultPersistence : IBattleResultPersistence
    {
        private readonly BattleResultService _service;

        public DirectBattleResultPersistence(BattleResultService service)
        {
            _service = service;
        }

        public Task<bool> PersistTerminalResultAsync(
            BattleState state,
            BattleOutcome outcome,
            CancellationToken cancellationToken = default) =>
            _service.PersistTerminalResultAsync(state, outcome, cancellationToken);
    }

    // =======================================================================
    // A terminal action persists exactly one row and clears the active state
    // =======================================================================

    [Fact]
    public async Task TerminalVictory_ShouldWriteOneResultRow_AndDeleteTheActiveState()
    {
        // GAME_RULES.md §1.4 / GAME_EVENTS.md §2: a committed Swap that reduces the
        // Boss to 0 HP emits BattleWon and ends the battle. DATABASE.md §1 then
        // requires exactly one row from authoritative server state, and
        // REDIS_STATE.md §3 requires the active-state record to be cleared — after
        // the write.
        var harness = Harness.Create(BossDefinitions.HoaLong);
        var battleId = "terminal-flow-victory";

        var created = await harness.Battles.CreateBattleAsync(
            battleId,
            Owner,
            Pet,
            FragileBoss());

        Assert.Equal(0, harness.Results.WriteAttempts);

        var pair = FindMatchProducingPair(created.BoardState);

        // The committed terminal state is captured before the swap result is
        // inspected: the terminal path clears the battle's active record
        // (REDIS_STATE.md §3), so a later action against that id resolves nothing.
        // The event batch proves this action was the terminal one.
        var result = await harness.Battles.ExecuteSwapAsync(battleId, pair);

        Assert.NotNull(result);
        Assert.True(result!.Value.IsAccepted);
        Assert.Contains(result.Value.Events, e => e.Type == BattleEventType.BattleWon);

        var committed = result.Value.State;

        var row = Assert.Single(harness.Results.Rows);

        // DATABASE.md §1: every value from authoritative server state.
        Assert.Equal(battleId, row.BattleResultId);
        Assert.Equal(Owner.Value, row.PlayerId);
        Assert.Equal(Pet.PetId.Value, row.PetInstanceId);
        Assert.Equal(BossDefinitions.HoaLong.BossDefinitionId, row.BossDefinitionId);
        Assert.Equal(BattleOutcome.Victory, row.Outcome);

        // Item 1: the terminal Turn IS counted, so the row records the Turn the
        // committed Swap advanced the state to.
        Assert.Equal(committed.Turn, row.DurationTurns);
        Assert.Equal(1, row.DurationTurns);

        Assert.Equal("{}", row.RewardSummary);

        // REDIS_STATE.md §3: the active state was deleted once, on this terminal
        // path.
        Assert.Equal(1, harness.Store.DeleteCount);
    }

    [Fact]
    public async Task TerminalDefeat_ShouldWriteOneResultRow_WithTheDefeatOutcome()
    {
        // The other terminal path: GAME_RULES.md §1.4 ends the battle when the
        // player's side reaches 0 HP, reported as BattleLost (GAME_EVENTS.md §2) —
        // stored as the documented "defeat".
        var harness = Harness.Create(BossDefinitions.HoaLong);
        var battleId = "terminal-flow-defeat";

        var created = await harness.Battles.CreateBattleAsync(
            battleId,
            Owner,
            Pet,
            LethalBoss());

        var pair = FindMatchProducingPair(created.BoardState);
        var result = await harness.Battles.ExecuteSwapAsync(battleId, pair);

        Assert.NotNull(result);
        Assert.True(result!.Value.IsAccepted);
        Assert.Contains(result.Value.Events, e => e.Type == BattleEventType.BattleLost);
        Assert.DoesNotContain(result.Value.Events, e => e.Type == BattleEventType.BattleWon);

        var row = Assert.Single(harness.Results.Rows);

        Assert.Equal(BattleOutcome.Defeat, row.Outcome);
        Assert.Equal(BattleOutcome.Defeat, BattleOutcomes.FromContractValue("defeat"));
        Assert.Equal("defeat", BattleOutcomes.ToContractValue(row.Outcome));
        Assert.Equal(1, harness.Store.DeleteCount);
    }

    [Fact]
    public async Task TerminalResult_ShouldRecordTheCommittedStatesTerminalValues()
    {
        // The row must describe the transition that became authoritative: the state
        // the Sequence compare-and-set accepted, not one a concurrent resolution
        // refused (REDIS_STATE.md §4 items 2–3). The assertion is against the
        // resolution's own returned state — the one the store stored.
        var harness = Harness.Create(BossDefinitions.HoaLong);
        var battleId = "terminal-flow-committed-state";

        var created = await harness.Battles.CreateBattleAsync(
            battleId,
            Owner,
            Pet,
            FragileBoss());

        var pair = FindMatchProducingPair(created.BoardState);
        var result = await harness.Battles.ExecuteSwapAsync(battleId, pair);

        Assert.NotNull(result);

        var row = Assert.Single(harness.Results.Rows);
        var committed = result!.Value.State;

        Assert.Equal(committed.BattleId, row.BattleResultId);
        Assert.Equal(committed.PlayerId.Value, row.PlayerId);
        Assert.Equal(committed.PetState.PetId.Value, row.PetInstanceId);
        Assert.Equal(committed.Turn, row.DurationTurns);

        // The terminal HP the outcome event reports is the committed state's own
        // value (GAME_EVENTS.md §2, SIGNALR_PROTOCOL.md §3.2.19 item 2).
        var won = result.Value.Events.Single(e => e.Type == BattleEventType.BattleWon).BattleWon;

        Assert.Equal(committed.BossState.HP, won.FinalBossHp);
    }

    // =======================================================================
    // A non-terminal resolution writes no row and deletes no key
    // =======================================================================

    [Fact]
    public async Task NonTerminalResolution_ShouldWriteNoResultRow_AndDeleteNoKey()
    {
        // GAME_RULES.md §1.4 / GAME_STATE.md §2.0.3: when neither side reaches 0 HP
        // the resolution emits no outcome event, and that absence IS the documented
        // statement that the action was not terminal. No row is written and the
        // active state stays.
        var harness = Harness.Create(BossDefinitions.HoaLong);
        var battleId = "terminal-flow-none";

        var created = await harness.Battles.CreateBattleAsync(
            battleId,
            Owner,
            Pet,
            BossDefinitions.HoaLong);

        var pair = FindMatchProducingPair(created.BoardState);
        var result = await harness.Battles.ExecuteSwapAsync(battleId, pair);

        Assert.True(result!.Value.IsAccepted);
        Assert.DoesNotContain(result.Value.Events, e => e.Type == BattleEventType.BattleWon);
        Assert.DoesNotContain(result.Value.Events, e => e.Type == BattleEventType.BattleLost);

        // The authoritative state advanced normally…
        Assert.True(result.Value.State.BossState.HP > 0);
        Assert.True(result.Value.State.PetState.HP > 0);
        Assert.Equal(1, result.Value.State.Turn);

        // …and nothing durable was written or cleared.
        Assert.Empty(harness.Results.Rows);
        Assert.Equal(0, harness.Results.WriteAttempts);
        Assert.Equal(0, harness.Store.DeleteCount);
    }

    [Fact]
    public async Task RejectedResolution_ShouldWriteNoResultRow_AndDeleteNoKey()
    {
        // REDIS_STATE.md §4 item 7: a rejected action writes nothing at all. It is
        // not a Turn and cannot be terminal, so it reaches no persistence step.
        var harness = Harness.Create(BossDefinitions.HoaLong);
        var battleId = "terminal-flow-rejected";

        var created = await harness.Battles.CreateBattleAsync(
            battleId,
            Owner,
            Pet,
            FragileBoss());

        // An out-of-range index is rejected by MATCH3_RULES.md §2.1.2 validation.
        var rejected = await harness.Battles.ExecuteSwapAsync(
            battleId,
            new SwapRequest(BoardState.CellCount + 5, BoardState.CellCount + 6));

        Assert.True(rejected!.Value.IsRejected);
        Assert.Empty(harness.Results.Rows);
        Assert.Equal(0, harness.Store.DeleteCount);
    }

    // =======================================================================
    // PostgreSQL failure on the terminal path — DATABASE.md §1 sourcing item 3
    // =======================================================================

    [Fact]
    public async Task TerminalResolution_WhenTheDurableWriteFails_ShouldLeaveTheActiveStateIntact()
    {
        // DATABASE.md §1 sourcing item 3 item 2 / REDIS_STATE.md §3: no result row,
        // NO Redis deletion, battle remains recoverable. The resolution itself has
        // already committed to the store, so its outcome events are still returned
        // to the caller — the action happened; only the durable record did not,
        // and the state is what a retry can be built from.
        var harness = Harness.Create(BossDefinitions.HoaLong);
        harness.Results.WriteFails = true;
        var battleId = "terminal-flow-write-fails";

        var created = await harness.Battles.CreateBattleAsync(
            battleId,
            Owner,
            Pet,
            FragileBoss());

        var pair = FindMatchProducingPair(created.BoardState);
        var result = await harness.Battles.ExecuteSwapAsync(battleId, pair);

        Assert.True(result!.Value.IsAccepted);
        Assert.Contains(result.Value.Events, e => e.Type == BattleEventType.BattleWon);

        // No durable row, and the delete never ran.
        Assert.Empty(harness.Results.Rows);
        Assert.Equal(0, harness.Store.DeleteCount);

        // The authoritative state is still stored, so the battle is recoverable.
        var stored = await harness.Store.GetAsync(battleId);

        Assert.NotNull(stored);
        Assert.Equal(result.Value.State, stored);
    }

    // =======================================================================
    // Boss definition resolution failure — DATABASE.md §1 sourcing item 3
    // =======================================================================

    [Fact]
    public async Task TerminalResolution_WhenNoBossDefinitionResolves_ShouldFailClosed()
    {
        // DATABASE.md §1 sourcing item 3: an unresolved BossDefinition means no
        // BattleResult row AND no battle:{battleId}:state delete, because an
        // absent definition must never produce a fabricated, fallback, or default
        // key. The battle stays recoverable and the write can be retried once a
        // row exists for that Identity.
        var harness = Harness.Create();
        var battleId = "terminal-flow-no-boss";

        var created = await harness.Battles.CreateBattleAsync(
            battleId,
            Owner,
            Pet,
            FragileBoss());

        var pair = FindMatchProducingPair(created.BoardState);
        var result = await harness.Battles.ExecuteSwapAsync(battleId, pair);

        Assert.True(result!.Value.IsAccepted);
        Assert.Contains(result.Value.Events, e => e.Type == BattleEventType.BattleWon);

        Assert.Empty(harness.Results.Rows);
        Assert.Equal(0, harness.Store.DeleteCount);

        var stored = await harness.Store.GetAsync(battleId);

        Assert.NotNull(stored);
        Assert.Equal(result.Value.State, stored);
    }

    // =======================================================================
    // Redis delete failure — REDIS_STATE.md §3
    // =======================================================================

    [Fact]
    public async Task TerminalResolution_WhenTheDeleteFails_ShouldKeepTheResult_AndNeedNoRetry()
    {
        // REDIS_STATE.md §3: after a successful result write, a failed delete has
        // no retry and no worker — the sliding TTL remains the cleanup path, and
        // BattleResultId = BattleId makes a second row impossible. The documented
        // end state is therefore: the durable result stands, the delete was
        // attempted, and no second result exists.
        var harness = Harness.Create(BossDefinitions.HoaLong);
        harness.Store.DeleteFails = true;
        var battleId = "terminal-flow-delete-fails";

        var created = await harness.Battles.CreateBattleAsync(
            battleId,
            Owner,
            Pet,
            FragileBoss());

        var pair = FindMatchProducingPair(created.BoardState);
        var result = await harness.Battles.ExecuteSwapAsync(battleId, pair);

        Assert.True(result!.Value.IsAccepted);

        var row = Assert.Single(harness.Results.Rows);

        Assert.Equal(battleId, row.BattleResultId);
        Assert.Equal(BattleOutcome.Victory, row.Outcome);

        // The delete was attempted — once — and no second result was produced.
        Assert.Equal(1, harness.Store.DeleteCount);
        Assert.Single(harness.Results.Rows);
    }

    // =======================================================================
    // Two concurrent terminal resolutions
    // =======================================================================

    [Fact]
    public async Task TwoConcurrentTerminalResolutions_ShouldProduceOneRow()
    {
        // REDIS_STATE.md §4 items 2–3: only the Sequence-guarded write-back
        // commits, so only one terminal resolution can become authoritative. The
        // loser is refused and retried against the fresh state — which, for the
        // original pre-resolution board, no longer offers the same commit.
        //
        // The documented duplicate protection is the row's key: because
        // BattleResultId is the battle's own BattleId, a second terminal
        // persistence for one battle cannot create a second row (DATABASE.md §1
        // sourcing item 1). This asserts that property over two completed
        // resolutions of the same battle.
        var harness = Harness.Create(BossDefinitions.HoaLong);
        var battleId = "terminal-flow-concurrent";

        var created = await harness.Battles.CreateBattleAsync(
            battleId,
            Owner,
            Pet,
            FragileBoss());

        var pair = FindMatchProducingPair(created.BoardState);

        var first = await harness.Battles.ExecuteSwapAsync(battleId, pair);

        Assert.NotNull(first);
        Assert.True(first!.Value.IsAccepted);

        // The terminal path cleared the record (REDIS_STATE.md §3), so the replay
        // resolves nothing: REDIS_STATE.md §3's documented "a second terminal
        // action arriving after the key is removed" case. Either way the second
        // action reaches no persistence step and cannot produce a second row.
        var second = await harness.Battles.ExecuteSwapAsync(battleId, pair);

        Assert.Null(second);

        var row = Assert.Single(harness.Results.Rows);

        Assert.Equal(battleId, row.BattleResultId);
    }

    [Fact]
    public async Task RepeatedTerminalPersistence_ShouldStillProduceOneRow()
    {
        // REDIS_STATE.md §3: "a failed delete cannot produce a second result:
        // BattleResultId is the battle's own BattleId, so at most one BattleResult
        // row can ever exist per battle". A battle whose record survived a failed
        // delete can therefore be terminated again without ever acquiring a second
        // row — the primary key is the protection, and no second idempotency
        // mechanism is introduced.
        var harness = Harness.Create(BossDefinitions.HoaLong);
        harness.Store.DeleteFails = true;
        var battleId = "terminal-flow-repeat-terminal";

        var created = await harness.Battles.CreateBattleAsync(
            battleId,
            Owner,
            Pet,
            FragileBoss());

        var pair = FindMatchProducingPair(created.BoardState);
        var result = await harness.Battles.ExecuteSwapAsync(battleId, pair);

        Assert.NotNull(result);
        Assert.True(result!.Value.IsAccepted);

        // The record is still there — the delete failed and REDIS_STATE.md §3
        // performs no retry — so the documented battle-end step can run again from
        // that same authoritative state.
        var stored = await harness.Store.GetAsync(battleId);

        Assert.NotNull(stored);

        var secondAttempt = await harness.Persistence
            .PersistTerminalResultAsync(stored!, BattleOutcome.Victory);

        Assert.True(secondAttempt);

        // Still exactly one row, keyed by the battle.
        var row = Assert.Single(harness.Results.Rows);

        Assert.Equal(battleId, row.BattleResultId);
        Assert.Equal(BattleOutcome.Victory, row.Outcome);
    }

    // =======================================================================
    // Helpers
    // =======================================================================

    private static SwapRequest FindMatchProducingPair(BoardState board)
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
