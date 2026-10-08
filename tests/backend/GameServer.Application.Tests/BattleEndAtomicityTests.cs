using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using System.Text.Json;
using Xunit;

namespace GameServer.Application.Tests;

/// <summary>
/// The atomicity invariant of the battle-end persistence step —
/// <c>DATABASE.md</c> §1, <c>ARCHITECTURE.md</c> §4 item 4,
/// <c>REDIS_STATE.md</c> §3.
///
/// <code>
/// Rule (DATABASE.md §1 records one battle end as one durable result plus the
///       documented reward on each track)
///  ↓
/// Scenario (Given a terminal battle whose result row, Player progression, and
///           Pet progression must become durable together,
///           When any one of those writes fails,
///           Then none of them is durable, the active battle stays recoverable,
///                and the retry records the whole battle once)
///  ↓
/// Test
/// </code>
///
/// <b>The invariant under test.</b>
///
/// <code>
/// BattleResult + Player progression + Pet progression
///         ↓
/// one COMMIT, or none of them
/// </code>
///
/// Every write of the battle-end step is made inside one transaction
/// (<see cref="IBattleEndTransaction"/>), so a failure in any of them rolls the
/// whole unit of work back. That is what makes the existing exactly-once guard
/// safe: <c>BattleResultId</c> is the battle's own <c>BattleId</c>
/// (<c>DATABASE.md</c> §1 sourcing item 1), so a durable row refuses a repeat —
/// which is only correct while a durable row implies that both grants committed
/// with it. Before this invariant held, a Pet write that failed after the result
/// row committed lost that Pet XP permanently.
///
/// <b>Why the failure paths are real rollbacks here.</b> The in-memory stores model
/// the two facts the invariant depends on: a write made inside the unit of work is
/// undone by a rollback, and only a commit makes it stand
/// (<see cref="InMemoryBattleEndTransaction"/>). The database-level proof — that
/// PostgreSQL itself rolls the three writes back — is the Infrastructure
/// integration suite's, because an in-memory store cannot prove a database
/// transaction (<c>AGENTS.md</c> §15).
///
/// <b>No expected value is invented.</b> The two grant amounts are
/// <c>COMBAT_RULES.md</c> §7.2 and <c>PET_RULES.md</c> §5.3; the two Level formulas
/// are §7.4 and §5.4; the ordering rule and the delete's condition are
/// <c>REDIS_STATE.md</c> §3; the fail-closed and retryable behaviour is
/// <c>DATABASE.md</c> §1 sourcing item 3.
/// </summary>
public class BattleEndAtomicityTests
{
    private static readonly PlayerId Owner = new("player_battle_end_atomicity_owner");
    private static readonly PetId ActivePet = new("pet-instance-atomicity-combat");

    /// <summary>
    /// A battle the documented factory created, so the state under test is a real
    /// authoritative <c>BattleState</c> rather than a hand-built record
    /// (<c>GAME_STATE.md</c> §2, §2.7.1).
    /// </summary>
    private static BattleState NewBattle(string battleId) =>
        BattleState.Create(
            battleId,
            rngSeed: 20260927UL,
            Owner,
            ActivePet,
            Element.Hoa,
            new PassiveId("xich-lang"),
            passiveThreshold: 5,
            BossDefinitions.HoaLong);

    /// <summary>
    /// The battle-end boundary over the in-memory stores, with the battle's owning
    /// Player and active combat Pet seeded at the documented initial values and the
    /// battle's active state present in the store — so "the battle is still
    /// recoverable" is an assertion about a real stored record and not about a
    /// fixture that never had one (<c>REDIS_STATE.md</c> §3).
    /// </summary>
    private sealed record Harness(
        BattleResultService Service,
        InMemoryBattleResultRepository Results,
        InMemoryBattleStateRepository Battles,
        InMemoryPlayerRepository Players,
        InMemoryPetRepository Pets,
        InMemoryBattleEndTransaction Transactions,
        BattleState State)
    {
        public static async Task<Harness> CreateAsync(string battleId)
        {
            var results = new InMemoryBattleResultRepository();
            var battles = new InMemoryBattleStateRepository();
            var players = new InMemoryPlayerRepository();
            var pets = new InMemoryPetRepository();

            players.Seed(Owner.Value);
            pets.Seed(ActivePet.Value, playerId: Owner.Value);

            var transactions = new InMemoryBattleEndTransaction(results, players, pets);

            var service = new BattleResultService(
                results,
                ScriptedBossDefinitionLookup.Resolving(BossDefinitions.HoaLong),
                battles,
                players,
                pets,
                transactions,
                TimeProvider.System);

            var state = NewBattle(battleId);

            // The battle exists and is active: the battle-end step is what ends it.
            await battles.CreateAsync(state);

            return new Harness(service, results, battles, players, pets, transactions, state);
        }

        public Player OwnerPlayer => Players.Find(Owner.Value)!;

        public Pet ActiveCombatPet => Pets.Find(ActivePet.Value)!;

        /// <summary>
        /// The stored <c>RewardSummary</c> as the JSON document
        /// <c>DATABASE.md</c> §1 defines, so the summary can be compared against the
        /// progression the same commit stored.
        /// </summary>
        public JsonDocument Summary()
        {
            var row = Assert.Single(Results.Rows);
            return JsonDocument.Parse(row.RewardSummary);
        }
    }

    // =======================================================================
    // A. The happy path — one commit, then the delete
    // =======================================================================

    [Fact]
    public async Task SuccessfulPersistence_ShouldCommitTheRowAndBothTracks_AndThenClearTheActiveState()
    {
        // DATABASE.md §1: a completed battle is one durable result row plus the
        // documented reward on each track; COMBAT_RULES.md §7.2 and PET_RULES.md
        // §5.3 fix the two grants, and §7.4/§5.4 derive the two Levels from them.
        var harness = await Harness.CreateAsync("atomicity-success");

        var written = await harness.Service
            .PersistTerminalResultAsync(harness.State, BattleOutcome.Victory);

        Assert.True(written);

        // The durable result exists.
        var row = Assert.Single(harness.Results.Rows);
        Assert.Equal("atomicity-success", row.BattleResultId);

        // The Player progression is persisted, and the Pet progression is
        // persisted — both, not either.
        Assert.Equal(100, harness.OwnerPlayer.XP);
        Assert.Equal(2, harness.OwnerPlayer.Level);
        Assert.Equal(100, harness.ActiveCombatPet.XP);
        Assert.Equal(2, harness.ActiveCombatPet.Level);

        // The durable row and the stored progression cannot disagree: the summary
        // was projected from the same two grants this commit stored.
        using var summary = harness.Summary();
        Assert.Equal(harness.OwnerPlayer.XP, summary.RootElement.GetProperty("newPlayerXp").GetInt32());
        Assert.Equal(harness.ActiveCombatPet.XP, summary.RootElement.GetProperty("newPetXp").GetInt32());
        Assert.Equal(harness.ActiveCombatPet.Level, summary.RootElement.GetProperty("newPetLevel").GetInt32());

        // One unit of work, committed once, never rolled back: the three writes
        // became durable together.
        Assert.Equal(1, harness.Transactions.BeginCount);
        Assert.Equal(1, harness.Transactions.CommitCount);
        Assert.Equal(0, harness.Transactions.RollbackCount);

        // REDIS_STATE.md §3: the active state is cleared once the battle is
        // durably and completely recorded.
        Assert.Equal(1, harness.Battles.DeleteCount);
        Assert.Null(await harness.Battles.GetAsync("atomicity-success"));
    }

    [Fact]
    public async Task TheActiveStateDelete_ShouldHappenOnlyAfterTheCommit()
    {
        // REDIS_STATE.md §3 conditions the delete on the durable result having been
        // written, and ARCHITECTURE.md §4 item 4 fixes the order. The probe records
        // the delete count at the moment of the commit: if the commit preceded the
        // delete, nothing had been deleted yet.
        var harness = await Harness.CreateAsync("atomicity-order");

        var deleteCountWhenCommitted = -1;

        harness.Transactions.OnCommit = () => deleteCountWhenCommitted = harness.Battles.DeleteCount;

        Assert.True(await harness.Service
            .PersistTerminalResultAsync(harness.State, BattleOutcome.Victory));

        Assert.Equal(0, deleteCountWhenCommitted);
        Assert.Equal(1, harness.Battles.DeleteCount);
    }

    // =======================================================================
    // B. Pet progression failure — everything rolls back
    // =======================================================================

    [Fact]
    public async Task PetProgressionFailure_ShouldRollBackTheResultAndBothTracks_AndLeaveTheBattleRecoverable()
    {
        // DATABASE.md §1 sourcing item 3 / PET_RULES.md §5.3 item 1: the battle end
        // is not complete until the Pet's reward is durable with the result row that
        // states it, so the failure must undo the whole unit of work rather than
        // leave a durable row whose reward was never applied — the case that used to
        // lose Pet XP permanently, because the already-durable row refused the retry.
        var harness = await Harness.CreateAsync("atomicity-pet-fails");

        harness.Pets.ProgressionSaveFails = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.PersistTerminalResultAsync(harness.State, BattleOutcome.Victory));

        // No durable BattleResult.
        Assert.Empty(harness.Results.Rows);

        // No durable Player progression either: the grant that the result row would
        // have stated is undone with it.
        Assert.Equal(Player.InitialXp, harness.OwnerPlayer.XP);
        Assert.Equal(Player.InitialLevel, harness.OwnerPlayer.Level);

        // No partially persisted Pet progression.
        Assert.Equal(Pet.InitialXp, harness.ActiveCombatPet.XP);
        Assert.Equal(Pet.InitialLevel, harness.ActiveCombatPet.Level);

        // The unit of work was rolled back, never committed.
        Assert.Equal(1, harness.Transactions.RollbackCount);
        Assert.Equal(0, harness.Transactions.CommitCount);

        // The active battle is not treated as finalized: the delete is conditioned
        // on the durable write (REDIS_STATE.md §3), and the record that is the only
        // authoritative copy of the battle is still there.
        Assert.Equal(0, harness.Battles.DeleteCount);
        Assert.NotNull(await harness.Battles.GetAsync("atomicity-pet-fails"));
    }

    [Fact]
    public async Task PetProgressionReportedAsAbsent_ShouldFailThePersistence_AndRollBackTheUnitOfWork()
    {
        // DATABASE.md §1: "an absent row is reported as absence". The battle-end
        // path must not read that as a completed persistence — the durable result
        // would state `petXpGained`/`newPetXp` for a Pet whose stored progression
        // never changed — so the reported absence fails the battle end exactly as a
        // raised failure does.
        var harness = await Harness.CreateAsync("atomicity-pet-absent");

        harness.Pets.ProgressionRowAbsentOnSave = true;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.PersistTerminalResultAsync(harness.State, BattleOutcome.Victory));

        // The failure names the Pet track, so the two participating tracks'
        // "nothing persisted" reports are not collapsed into one indistinguishable
        // failure.
        Assert.Contains("Pet", exception.Message, StringComparison.Ordinal);

        Assert.Empty(harness.Results.Rows);
        Assert.Equal(Player.InitialXp, harness.OwnerPlayer.XP);
        Assert.Equal(Pet.InitialXp, harness.ActiveCombatPet.XP);

        Assert.Equal(1, harness.Transactions.RollbackCount);
        Assert.Equal(0, harness.Transactions.CommitCount);
        Assert.Equal(0, harness.Battles.DeleteCount);
        Assert.NotNull(await harness.Battles.GetAsync("atomicity-pet-absent"));
    }

    // =======================================================================
    // C. Player progression failure — the same invariant
    // =======================================================================

    [Fact]
    public async Task PlayerProgressionFailure_ShouldRollBackTheResult_AndPersistNoProgressionAtAll()
    {
        // The same invariant on the other participating track: COMBAT_RULES.md §7.2's
        // grant is part of what the durable row states, so a Player write that did
        // not happen cannot leave the row behind either.
        var harness = await Harness.CreateAsync("atomicity-player-fails");

        harness.Players.ProgressionSaveFails = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.PersistTerminalResultAsync(harness.State, BattleOutcome.Victory));

        Assert.Empty(harness.Results.Rows);

        Assert.Equal(Player.InitialXp, harness.OwnerPlayer.XP);
        Assert.Equal(Player.InitialLevel, harness.OwnerPlayer.Level);

        // The Pet track is not partially persisted either — and its write was never
        // reached, because the failure precedes it.
        Assert.Equal(Pet.InitialXp, harness.ActiveCombatPet.XP);
        Assert.Equal(Pet.InitialLevel, harness.ActiveCombatPet.Level);
        Assert.Equal(0, harness.Pets.ProgressionSaveCount);

        Assert.Equal(1, harness.Transactions.RollbackCount);
        Assert.Equal(0, harness.Transactions.CommitCount);

        Assert.Equal(0, harness.Battles.DeleteCount);
        Assert.NotNull(await harness.Battles.GetAsync("atomicity-player-fails"));
    }

    [Fact]
    public async Task PlayerProgressionReportedAsAbsent_ShouldFailThePersistence_AndRollBackTheUnitOfWork()
    {
        // The Player track's own "nothing persisted" report, handled exactly as the
        // Pet track's is: DATABASE.md §1's absence is a failure of this battle end,
        // not a silent skip.
        var harness = await Harness.CreateAsync("atomicity-player-absent");

        harness.Players.ProgressionRowAbsentOnSave = true;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.PersistTerminalResultAsync(harness.State, BattleOutcome.Victory));

        Assert.Contains("Player", exception.Message, StringComparison.Ordinal);

        Assert.Empty(harness.Results.Rows);
        Assert.Equal(Player.InitialXp, harness.OwnerPlayer.XP);
        Assert.Equal(Pet.InitialXp, harness.ActiveCombatPet.XP);

        Assert.Equal(1, harness.Transactions.RollbackCount);
        Assert.Equal(0, harness.Transactions.CommitCount);
        Assert.Equal(0, harness.Battles.DeleteCount);
        Assert.NotNull(await harness.Battles.GetAsync("atomicity-player-absent"));
    }

    // =======================================================================
    // D. Retry after a rolled-back attempt
    // =======================================================================

    [Fact]
    public async Task RetryAfterARolledBackAttempt_ShouldRecordTheBattleOnce_AndGrantEachTrackExactlyOnce()
    {
        // DATABASE.md §1 sourcing item 3: a failed battle end leaves the battle
        // recoverable, so the retry re-runs the whole unit of work. Because the
        // rollback left no durable row, the primary-key guard (sourcing item 1) does
        // not refuse it — and because the retry is the first durable write, each
        // track's grant is applied exactly once.
        var harness = await Harness.CreateAsync("atomicity-retry");

        harness.Pets.ProgressionSaveFails = true;

        // Attempt 1 → the Pet progression fails → the whole unit of work rolls back.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.PersistTerminalResultAsync(harness.State, BattleOutcome.Victory));

        Assert.Empty(harness.Results.Rows);

        // The cause is resolved (the row exists again), and the battle end is
        // retried from the same still-recoverable state.
        harness.Pets.ProgressionSaveFails = false;

        // Attempt 2 → success.
        var written = await harness.Service
            .PersistTerminalResultAsync(harness.State, BattleOutcome.Victory);

        Assert.True(written);

        // Exactly one durable BattleResult.
        var row = Assert.Single(harness.Results.Rows);
        Assert.Equal("atomicity-retry", row.BattleResultId);

        // The Player XP and the Pet XP are each granted exactly once: 100, not 200.
        Assert.Equal(100, harness.OwnerPlayer.XP);
        Assert.Equal(2, harness.OwnerPlayer.Level);
        Assert.Equal(100, harness.ActiveCombatPet.XP);
        Assert.Equal(2, harness.ActiveCombatPet.Level);

        // The final Pet Level/XP is the documented one for that XP
        // (PET_RULES.md §5.4/§5.5): 100 XP is Level 2, and no second grant is hidden
        // in the instance.
        Assert.Equal(Pet.LevelForXp(harness.ActiveCombatPet.XP), harness.ActiveCombatPet.Level);
        Assert.Equal(
            harness.ActiveCombatPet.XP,
            harness.Summary().RootElement.GetProperty("newPetXp").GetInt32());

        // The retry recorded the battle once and cleared the active state once: two
        // attempts, one commit, one rollback.
        Assert.Equal(2, harness.Transactions.BeginCount);
        Assert.Equal(1, harness.Transactions.CommitCount);
        Assert.Equal(1, harness.Transactions.RollbackCount);
        Assert.Equal(1, harness.Battles.DeleteCount);
        Assert.Null(await harness.Battles.GetAsync("atomicity-retry"));
    }

    [Fact]
    public async Task RetryAfterARolledBackPlayerAttempt_ShouldAlsoGrantEachTrackExactlyOnce()
    {
        // The same retry guarantee when the failing participant is the Player track
        // (COMBAT_RULES.md §7.2): the rollback precedes the Pet write, so nothing of
        // either grant survives attempt 1.
        var harness = await Harness.CreateAsync("atomicity-player-retry");

        harness.Players.ProgressionSaveFails = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.PersistTerminalResultAsync(harness.State, BattleOutcome.Victory));

        harness.Players.ProgressionSaveFails = false;

        Assert.True(await harness.Service
            .PersistTerminalResultAsync(harness.State, BattleOutcome.Victory));

        Assert.Single(harness.Results.Rows);
        Assert.Equal(100, harness.OwnerPlayer.XP);
        Assert.Equal(100, harness.ActiveCombatPet.XP);
        Assert.Equal(Pet.LevelForXp(harness.ActiveCombatPet.XP), harness.ActiveCombatPet.Level);
        Assert.Equal(1, harness.Transactions.CommitCount);
        Assert.Equal(1, harness.Transactions.RollbackCount);
        Assert.Equal(1, harness.Battles.DeleteCount);
    }

    // =======================================================================
    // E. The existing idempotency, after a fully successful battle end
    // =======================================================================

    [Fact]
    public async Task RepeatedTerminalPersistence_AfterACommit_ShouldStayIdempotent_AndGrantNothingTwice()
    {
        // DATABASE.md §1 sourcing item 1: BattleResultId is the battle's own
        // BattleId, so a repeat finds the battle already durably recorded and writes
        // nothing. The invariant makes that guard safe to rely on rather than
        // weakens it: the first commit stored both grants with the row, so there is
        // nothing left to apply.
        var harness = await Harness.CreateAsync("atomicity-repeat");

        Assert.True(await harness.Service
            .PersistTerminalResultAsync(harness.State, BattleOutcome.Victory));

        var firstRow = Assert.Single(harness.Results.Rows);

        // The same terminal persistence, repeated.
        var written = await harness.Service
            .PersistTerminalResultAsync(harness.State, BattleOutcome.Victory);

        Assert.True(written);

        // Still exactly one durable result, and it is the row the first write left —
        // not rewritten from the repeat (DATABASE.md §1: CompletedAt is "one value
        // per battle").
        Assert.Same(firstRow, Assert.Single(harness.Results.Rows));

        // No XP is granted twice on either track.
        Assert.Equal(100, harness.OwnerPlayer.XP);
        Assert.Equal(2, harness.OwnerPlayer.Level);
        Assert.Equal(100, harness.ActiveCombatPet.XP);
        Assert.Equal(2, harness.ActiveCombatPet.Level);

        // The repeat writes nothing, so it opens no transaction and commits
        // nothing — and it clears nothing either (REDIS_STATE.md §3: the first
        // durable write is the record of what happened).
        Assert.Equal(1, harness.Transactions.BeginCount);
        Assert.Equal(1, harness.Transactions.CommitCount);
        Assert.Equal(0, harness.Transactions.RollbackCount);
        Assert.Equal(1, harness.Battles.DeleteCount);
    }
}