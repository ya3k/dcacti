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
/// The documented Pet XP grant on the canonical battle-end path —
/// <c>PET_RULES.md</c> §5.3/§5.4/§5.5, <c>GAME_EVENTS.md</c> §2,
/// <c>DATABASE.md</c> §1, <c>ARCHITECTURE.md</c> §4 item 4.
///
/// <code>
/// Rule (PET_RULES.md §5.3, §5.4)
///  ↓
/// Scenario (Given a terminal battle whose active combat Pet is known,
///           When the battle end is persisted,
///           Then the documented Pet XP grant and Level, exactly once,
///                to that one Pet only)
///  ↓
/// Test
/// </code>
///
/// <b>Every expected value traces to a document.</b> The two outcome amounts and
/// the targeting rule are <c>§5.3</c>; the Level formula is <c>§5.4</c>; the hard
/// cap and its no-overflow rule are <c>§5.5</c>; the exactly-once requirement
/// comes from the existing primary-key guard (<c>DATABASE.md</c> §1 sourcing
/// item 1) rather than from a new mechanism.
///
/// <b>The recipient is never chosen by a test.</b> The active combat Pet is the
/// one the battle's own <c>PetState.PetId</c> names
/// (<c>GAME_STATE.md</c> §2.3, <c>PET_RULES.md</c> §5.3 item 1), so no test here
/// can pass by selecting a Pet out of collection order.
/// </summary>
public class PetXpBattleRewardTests
{
    private static readonly PlayerId Owner = new("player_pet_xp_reward_owner");
    private static readonly PetId ActivePet = new("pet-instance-active-combat");

    /// <summary>
    /// A second owned Pet, to prove the grant reaches exactly one instance
    /// (<c>PET_RULES.md</c> §5.3 item 3: every inactive owned Pet receives +0).
    /// </summary>
    private static readonly PetId InactivePet = new("pet-instance-inactive-owned");

    /// <summary>
    /// A battle the documented factory created, so the state under test is a real
    /// authoritative <c>BattleState</c> rather than a hand-built record
    /// (<c>GAME_STATE.md</c> §2, §2.7.1). The combat Pet is
    /// <see cref="ActivePet"/> — the state's own identity, which is what the
    /// grant must use.
    /// </summary>
    private static BattleState NewBattle(string battleId, BossDefinition? boss = null) =>
        BattleState.Create(
            battleId,
            rngSeed: 20260927UL,
            Owner,
            ActivePet,
            Element.Hoa,
            new PassiveId("xich-lang"),
            passiveThreshold: 5,
            boss ?? BossDefinitions.HoaLong);

    /// <summary>
    /// The battle-end boundary over scripted doubles, with the battle's owning
    /// Player seeded at the documented initial values.
    /// </summary>
    private sealed record Harness(
        BattleResultService Service,
        InMemoryBattleResultRepository Results,
        InMemoryBattleStateRepository Battles,
        InMemoryPlayerRepository Players,
        InMemoryPetRepository Pets)
    {
        public static Harness Create(int activePetXp = Pet.InitialXp, int ownerXp = Player.InitialXp)
        {
            var results = new InMemoryBattleResultRepository();
            var battles = new InMemoryBattleStateRepository();
            var players = new InMemoryPlayerRepository();
            var pets = new InMemoryPetRepository();

            players.Seed(Owner.Value, ownerXp);

            // The active combat Pet — the instance BattleState.PetState.PetId
            // names (GAME_STATE.md §2.3).
            pets.Seed(ActivePet.Value, activePetXp, playerId: Owner.Value);

            var service = new BattleResultService(
                results,
                ScriptedBossDefinitionLookup.Resolving(BossDefinitions.HoaLong),
                battles,
                players,
                pets,
                TimeProvider.System);

            return new Harness(service, results, battles, players, pets);
        }

        public Pet ActiveCombatPet => Pets.Find(ActivePet.Value)!;

        public Player OwnerPlayer => Players.Find(Owner.Value)!;

        public RewardSummaryDocument Summary(string battleId) =>
            RewardSummaryDocument.Parse(Results.Rows.Single(row => row.BattleResultId == battleId).RewardSummary);
    }

    /// <summary>
    /// The stored <c>RewardSummary</c> as the JSON document
    /// <c>DATABASE.md</c> §1 defines, so a test reads real members rather than
    /// matching on raw text.
    /// </summary>
    private sealed class RewardSummaryDocument : IDisposable
    {
        private readonly JsonDocument _document;

        private RewardSummaryDocument(JsonDocument document)
        {
            _document = document;
        }

        public static RewardSummaryDocument Parse(string rewardSummary) =>
            new(JsonDocument.Parse(rewardSummary));

        public int Int(string member) => _document.RootElement.GetProperty(member).GetInt32();

        public bool Bool(string member) => _document.RootElement.GetProperty(member).GetBoolean();

        public void Dispose() => _document.Dispose();
    }

    // =======================================================================
    // PET_RULES.md §5.3 — BattleWon → active combat Pet +100
    // =======================================================================

    [Fact]
    public async Task BattleWon_ShouldGrantExactlyOneHundredPetXp_ToTheActiveCombatPet()
    {
        // PET_RULES.md §5.3 item 1: BattleWon grants +100 Pet XP to the active
        // combat Pet — the Pet associated with that battle's combat state, which
        // BattleState.PetState.PetId carries (GAME_STATE.md §2.3).
        var harness = Harness.Create();

        var written = await harness.Service
            .PersistTerminalResultAsync(NewBattle("pet-xp-won"), BattleOutcome.Victory);

        Assert.True(written);

        Assert.Equal(100, harness.ActiveCombatPet.XP);

        // §5.4: the Level follows the XP — 100 XP is Level 2.
        Assert.Equal(2, harness.ActiveCombatPet.Level);
        Assert.Equal(Pet.LevelForXp(harness.ActiveCombatPet.XP), harness.ActiveCombatPet.Level);
    }

    [Fact]
    public async Task BattleWon_ShouldAccumulateAcrossBattles()
    {
        // §5.1 item 6: Pet XP persists permanently with the instance and is not
        // reset by battle outcome, so each won battle adds its own grant to the
        // same Pet.
        var harness = Harness.Create();

        await harness.Service.PersistTerminalResultAsync(NewBattle("pet-xp-accumulate-1"), BattleOutcome.Victory);
        await harness.Service.PersistTerminalResultAsync(NewBattle("pet-xp-accumulate-2"), BattleOutcome.Victory);
        await harness.Service.PersistTerminalResultAsync(NewBattle("pet-xp-accumulate-3"), BattleOutcome.Victory);

        Assert.Equal(300, harness.ActiveCombatPet.XP);
        Assert.Equal(4, harness.ActiveCombatPet.Level);
    }

    // =======================================================================
    // PET_RULES.md §5.3 — BattleLost → active combat Pet +0
    // =======================================================================

    [Fact]
    public async Task BattleLost_ShouldGrantNoPetXp_AndChangeNoLevel()
    {
        // PET_RULES.md §5.3 item 2: a BattleLost grants the active combat Pet +0
        // Pet XP — an explicit Pet decision, not an inheritance of the Player
        // track's +0 — so the XP and the Level both stand exactly as they were.
        var harness = Harness.Create(activePetXp: 150);

        var before = harness.ActiveCombatPet.Level;

        var written = await harness.Service
            .PersistTerminalResultAsync(NewBattle("pet-xp-lost"), BattleOutcome.Defeat);

        Assert.True(written);

        Assert.Equal(150, harness.ActiveCombatPet.XP);
        Assert.Equal(before, harness.ActiveCombatPet.Level);
    }

    [Fact]
    public async Task BattleLost_ShouldStillPersistTheDurableResult()
    {
        // DATABASE.md §1 / GAME_EVENTS.md §2: the result is written for both
        // outcomes, and RewardSummary carries both tracks' members for both
        // (TASK-068 Option A). A defeat grants no Pet XP but is still a completed
        // battle with a durable record.
        var harness = Harness.Create();

        await harness.Service.PersistTerminalResultAsync(NewBattle("pet-xp-lost-row"), BattleOutcome.Defeat);

        var row = Assert.Single(harness.Results.Rows);

        Assert.Equal(BattleOutcome.Defeat, row.Outcome);
        Assert.Equal(Pet.InitialXp, harness.ActiveCombatPet.XP);

        using var summary = harness.Summary("pet-xp-lost-row");

        Assert.Equal(0, summary.Int("petXpGained"));
        Assert.False(summary.Bool("petLeveledUp"));
    }

    // =======================================================================
    // PET_RULES.md §5.3 items 1, 3 — exactly one recipient
    // =======================================================================

    [Fact]
    public async Task BattleWon_ShouldLeaveEveryInactiveOwnedPetUntouched()
    {
        // PET_RULES.md §5.3 item 3: inactive owned Pets receive +0 Pet XP from
        // that battle — there is no passive XP, no shared XP, no party-wide XP, and
        // no account-wide distribution. Item 4: to train a Pet it must be the
        // active combat Pet.
        var harness = Harness.Create();

        harness.Pets.Seed(InactivePet.Value, xp: 700, playerId: Owner.Value);

        await harness.Service.PersistTerminalResultAsync(NewBattle("pet-xp-inactive"), BattleOutcome.Victory);

        // The active combat Pet was credited …
        Assert.Equal(100, harness.ActiveCombatPet.XP);

        // … and the other owned Pet is exactly where it was.
        var inactive = harness.Pets.Find(InactivePet.Value)!;

        Assert.Equal(700, inactive.XP);
        Assert.Equal(Pet.LevelForXp(700), inactive.Level);

        // Only the recipient instance was written, so no second Pet can have been
        // reached by a bulk pass.
        Assert.Equal(1, harness.Pets.ProgressionSaveCount);
    }

    [Fact]
    public async Task BattleWon_ShouldCreditThePetTheBattleStateNames_NotAnotherOwnedPet()
    {
        // PET_RULES.md §5.3 item 1 / GAME_STATE.md §2.3: the recipient is the Pet
        // the battle's own combat state resolved. This battle's PetState.PetId is
        // ActivePet, so a grant that guessed from collection order, from the
        // Player, or from the request would credit the wrong instance and fail
        // here.
        var harness = Harness.Create();

        harness.Pets.Seed(InactivePet.Value, xp: 0, playerId: Owner.Value);

        var state = NewBattle("pet-xp-recipient-identity");

        Assert.Equal(ActivePet.Value, state.PetState.PetId.Value);

        await harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Victory);

        Assert.Equal(100, harness.Pets.Find(ActivePet.Value)!.XP);
        Assert.Equal(0, harness.Pets.Find(InactivePet.Value)!.XP);
    }

    [Fact]
    public async Task PetGrant_ShouldTargetTheRowTheStateResolved_EvenWhenItIsNotTheOldest()
    {
        // The same property under a seeded ordering that would break a
        // "first/oldest owned Pet" heuristic: the other instance is seeded first
        // and the state names the second. The grant must still follow the state.
        var harness = Harness.Create();

        harness.Pets.Seed("pet-instance-seeded-first", xp: 500, playerId: Owner.Value);

        var state = NewBattle("pet-xp-not-oldest");

        await harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Victory);

        Assert.Equal(100, harness.Pets.Find(ActivePet.Value)!.XP);
        Assert.Equal(500, harness.Pets.Find("pet-instance-seeded-first")!.XP);
    }

    // =======================================================================
    // PET_RULES.md §5.5 — the hard cap through the battle path
    // =======================================================================

    [Fact]
    public async Task BattleWon_AtTheCap_ShouldLeaveThePetAtFortyNineHundred()
    {
        // PET_RULES.md §5.5 items 2-3: a Pet already at 4900 is granted nothing on
        // a further win — no overflow, no hidden XP, no post-Level-50
        // accumulation. The reward is "neither awarded nor stored", so a stored
        // 5000 is impossible.
        var harness = Harness.Create(activePetXp: Pet.MaxXp);

        Assert.Equal(4900, harness.ActiveCombatPet.XP);
        Assert.Equal(Pet.MaxLevel, harness.ActiveCombatPet.Level);

        await harness.Service.PersistTerminalResultAsync(NewBattle("pet-xp-at-cap"), BattleOutcome.Victory);

        Assert.Equal(4900, harness.ActiveCombatPet.XP);
        Assert.NotEqual(5000, harness.ActiveCombatPet.XP);
        Assert.Equal(Pet.MaxLevel, harness.ActiveCombatPet.Level);
    }

    [Theory]
    [InlineData(4800)]
    [InlineData(4850)]
    [InlineData(4899)]
    public async Task BattleWon_CrossingTheCap_ShouldLandExactlyOnFortyNineHundred(int startingXp)
    {
        // PET_RULES.md §5.5 item 1: the cap is a hard maximum, so a grant that
        // crosses it stores exactly 4900 regardless of how far past it would
        // otherwise have gone.
        var harness = Harness.Create(activePetXp: startingXp);

        await harness.Service.PersistTerminalResultAsync(
            NewBattle($"pet-xp-cross-cap-{startingXp}"),
            BattleOutcome.Victory);

        Assert.Equal(Pet.MaxXp, harness.ActiveCombatPet.XP);
        Assert.Equal(Pet.MaxLevel, harness.ActiveCombatPet.Level);
    }

    [Fact]
    public async Task BattleWon_JustBelowTheCap_ShouldStoreTheExactSum()
    {
        // The other side of the boundary: 4700 + 100 is stored in full as 4800, so
        // the clamp cannot be mistaken for "always 4900".
        var harness = Harness.Create(activePetXp: 4700);

        await harness.Service.PersistTerminalResultAsync(NewBattle("pet-xp-below-cap"), BattleOutcome.Victory);

        Assert.Equal(4800, harness.ActiveCombatPet.XP);
        Assert.Equal(49, harness.ActiveCombatPet.Level);
    }

    // =======================================================================
    // PET_RULES.md §5.4 — the Level-50 boundary
    // =======================================================================

    [Fact]
    public async Task BattleWon_ShouldCrossTheLevelBoundary_LikeTheFormulaSays()
    {
        // §5.4: at 99 XP the Pet is Level 1; the next win takes it to 199, which is
        // Level 2. The grant and the formula are verified together, so a Level that
        // did not follow the XP would fail.
        var harness = Harness.Create(activePetXp: 99);

        Assert.Equal(1, harness.ActiveCombatPet.Level);

        await harness.Service.PersistTerminalResultAsync(NewBattle("pet-xp-cross-100"), BattleOutcome.Victory);

        Assert.Equal(199, harness.ActiveCombatPet.XP);
        Assert.Equal(2, harness.ActiveCombatPet.Level);
    }

    // =======================================================================
    // Exactly-once — bound to the first durable write
    // =======================================================================

    [Fact]
    public async Task RepeatedTerminalPersistence_ShouldGrantPetXpOnlyOnce()
    {
        // DATABASE.md §1 sourcing item 1 / REDIS_STATE.md §3: BattleResultId is
        // the battle's own BattleId, so a repeated terminal persistence of the
        // SAME battle reconciles against the already-durable row instead of
        // writing again. The reward grant is bound to the first durable write, so
        // the retry must not grant a second +100. No new idempotency mechanism is
        // introduced — the existing primary-key guard is what proves it.
        var harness = Harness.Create();
        var state = NewBattle("pet-xp-exactly-once");

        await harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Victory);

        Assert.Equal(100, harness.ActiveCombatPet.XP);

        // The same terminal result, retried — the documented retry/reconcile case.
        await harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Victory);

        Assert.Equal(100, harness.ActiveCombatPet.XP);
        Assert.Equal(2, harness.ActiveCombatPet.Level);

        // The retry is still exactly one row: the primary key remains the guard.
        Assert.Single(harness.Results.Rows);
    }

    [Fact]
    public async Task ManyRepeatedTerminalPersistences_ShouldStillGrantPetXpOnce()
    {
        // The same property under repeated retries, so an implementation that
        // granted on any call but the last, or on every other call, cannot pass.
        var harness = Harness.Create();
        var state = NewBattle("pet-xp-exactly-once-many");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Victory);
        }

        Assert.Equal(100, harness.ActiveCombatPet.XP);
        Assert.Equal(2, harness.ActiveCombatPet.Level);
        Assert.Single(harness.Results.Rows);
    }

    [Fact]
    public async Task RepeatedTerminalPersistence_ShouldNotRewriteTheStoredRewardSummary()
    {
        // TASK-067 §5.5 (b): a retry must not rewrite an already-durable row's
        // stored result. The second call re-reads and re-grants the same entities
        // in memory, but its write is skipped — so the stored summary still
        // describes the first, exactly-once grant rather than a second one.
        var harness = Harness.Create();
        var state = NewBattle("pet-xp-summary-rewrite");

        await harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Victory);

        var stored = Assert.Single(harness.Results.Rows).RewardSummary;

        await harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Victory);

        var afterRetry = Assert.Single(harness.Results.Rows).RewardSummary;

        Assert.Equal(stored, afterRetry);

        using var summary = harness.Summary("pet-xp-summary-rewrite");

        Assert.Equal(100, summary.Int("newPetXp"));
        Assert.Equal(100, summary.Int("newPlayerXp"));
    }

    [Fact]
    public async Task RepeatedTerminalPersistence_AfterAFailedWrite_ShouldGrantPetXpOnce()
    {
        // DATABASE.md §1 sourcing item 3: a failed durable write is absorbed and
        // the battle stays recoverable, so the documented recoverable case is a
        // later successful persistence of the same terminal result. That
        // successful write is the FIRST durable write, so it grants the XP once —
        // and a following retry must not grant it again.
        var harness = Harness.Create();
        var state = NewBattle("pet-xp-failed-then-recovered");

        harness.Results.WriteFails = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Victory));

        // No durable result ⇒ no grant: Pet XP is bound to the durable write, not
        // to the attempt.
        Assert.Equal(Pet.InitialXp, harness.ActiveCombatPet.XP);
        Assert.Empty(harness.Results.Rows);

        // The recovered write is the first durable one, so it grants once.
        harness.Results.WriteFails = false;

        await harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Victory);

        Assert.Equal(100, harness.ActiveCombatPet.XP);

        // And the subsequent retry does not grant again.
        await harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Victory);

        Assert.Equal(100, harness.ActiveCombatPet.XP);
        Assert.Single(harness.Results.Rows);
    }

    // =======================================================================
    // Failure and absence — no grant without a durable write
    // =======================================================================

    [Fact]
    public async Task FailedDurableWrite_ShouldGrantNoPetXp()
    {
        // DATABASE.md §1 sourcing item 3: the battle-end write fails closed. The
        // Pet XP grant rides the durable write, so a write that did not happen
        // grants nothing — the battle remains retryable and the eventual
        // successful write is what awards the XP (verified above).
        var harness = Harness.Create();

        harness.Results.WriteFails = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.PersistTerminalResultAsync(NewBattle("pet-xp-write-fails"), BattleOutcome.Victory));

        Assert.Equal(Pet.InitialXp, harness.ActiveCombatPet.XP);
        Assert.Equal(Pet.InitialLevel, harness.ActiveCombatPet.Level);
        Assert.Equal(0, harness.Pets.ProgressionSaveCount);
    }

    [Fact]
    public async Task UnresolvedBossDefinition_ShouldGrantNoPetXp()
    {
        // DATABASE.md §1 sourcing item 3: an unresolved BossDefinition writes no
        // row and deletes no key. With no durable write there is no reward either.
        var results = new InMemoryBattleResultRepository();
        var battles = new InMemoryBattleStateRepository();
        var players = new InMemoryPlayerRepository();
        var pets = new InMemoryPetRepository();

        players.Seed(Owner.Value);
        pets.Seed(ActivePet.Value, playerId: Owner.Value);

        var service = new BattleResultService(
            results,
            new ScriptedBossDefinitionLookup(),
            battles,
            players,
            pets,
            TimeProvider.System);

        var written = await service
            .PersistTerminalResultAsync(NewBattle("pet-xp-no-boss"), BattleOutcome.Victory);

        Assert.False(written);
        Assert.Equal(Pet.InitialXp, pets.Find(ActivePet.Value)!.XP);
    }

    [Fact]
    public async Task TerminalPersistence_ForAnUnknownPet_ShouldNotCreateOne()
    {
        // DATABASE.md §1: the reward updates the Pet the battle belonged to. When
        // no such row exists there is no progression to maintain — the reward must
        // not bring a Pet instance into existence, so the durable result is still
        // written and nothing is created.
        var results = new InMemoryBattleResultRepository();
        var battles = new InMemoryBattleStateRepository();
        var players = new InMemoryPlayerRepository();
        var pets = new InMemoryPetRepository();

        players.Seed(Owner.Value);

        var service = new BattleResultService(
            results,
            ScriptedBossDefinitionLookup.Resolving(BossDefinitions.HoaLong),
            battles,
            players,
            pets,
            TimeProvider.System);

        var written = await service
            .PersistTerminalResultAsync(NewBattle("pet-xp-no-pet-row"), BattleOutcome.Victory);

        Assert.True(written);
        Assert.Single(results.Rows);
        Assert.Empty(pets.Pets);
        Assert.Equal(0, pets.ProgressionSaveCount);
    }

    // =======================================================================
    // Track independence — PET_RULES.md §5.1 item 5, ADR-016 item 12
    // =======================================================================

    [Fact]
    public async Task PetGrant_ShouldNotChangeThePlayersProgression()
    {
        // ADR-016 item 12 / PET_RULES.md §5.1 item 5: the two tracks are separate
        // pools that neither read nor modify each other. The Pet grant touches no
        // Player member — the values below are the outcome's own documented Player
        // grant and nothing else.
        var harness = Harness.Create();

        await harness.Service.PersistTerminalResultAsync(NewBattle("pet-xp-player-independence"), BattleOutcome.Victory);

        // The Player track's own documented +100 (COMBAT_RULES.md §7.2) — not
        // doubled, and not derived from the Pet's grant.
        Assert.Equal(100, harness.OwnerPlayer.XP);
        Assert.Equal(2, harness.OwnerPlayer.Level);

        // The two grants are numerically equal because both tracks document +100
        // (PET_RULES.md §5.4 "Relationship to the Player Curve" item 3), which is
        // exactly why the assertions must name both sources.
        Assert.Equal(100, harness.ActiveCombatPet.XP);
    }

    [Fact]
    public async Task PlayerGrant_ShouldRemainExactlyOnce_WhilePetXpIsGranted()
    {
        // The Pet grant must not disturb the Player track's established
        // exactly-once behaviour (TASK-065). Both grants ride the same
        // first-durable-write gate, so a retry grants neither track twice.
        var harness = Harness.Create();
        var state = NewBattle("pet-xp-both-tracks-once");

        await harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Victory);
        await harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Victory);

        Assert.Equal(100, harness.OwnerPlayer.XP);
        Assert.Equal(100, harness.ActiveCombatPet.XP);
    }

    [Fact]
    public async Task Defeat_ShouldLeaveBothTracksUnchanged()
    {
        // PET_RULES.md §5.3 item 2 and COMBAT_RULES.md §7.5 item 4: a defeat
        // changes nothing on either track — both grant +0.
        var harness = Harness.Create(activePetXp: 250, ownerXp: 250);

        await harness.Service.PersistTerminalResultAsync(NewBattle("pet-xp-defeat-both"), BattleOutcome.Defeat);

        Assert.Equal(250, harness.ActiveCombatPet.XP);
        Assert.Equal(250, harness.OwnerPlayer.XP);
        Assert.Equal(Pet.LevelForXp(250), harness.ActiveCombatPet.Level);
        Assert.Equal(Player.LevelForXp(250), harness.OwnerPlayer.Level);
    }

    // =======================================================================
    // Server authority — GAME_RULES.md §18, ADR-001
    // =======================================================================

    [Fact]
    public async Task RewardAmount_ShouldBeOutcomeDerived_AndServerAuthoritative()
    {
        // GAME_RULES.md §18 / ADR-001 / AGENTS.md §10: the amount is chosen from
        // the resolution's own terminal report — the boundary accepts no client
        // input at all, so no caller can influence the grant. The two outcomes and
        // their two documented amounts are the whole contract.
        Assert.Equal(100, Pet.BattleWonXpReward);
        Assert.Equal(0, Pet.BattleLostXpReward);

        foreach (var (outcome, expected) in new[]
                 {
                     (BattleOutcome.Victory, 100),
                     (BattleOutcome.Defeat, 0),
                 })
        {
            var harness = Harness.Create();

            await harness.Service.PersistTerminalResultAsync(
                NewBattle($"pet-xp-authority-{outcome}"),
                outcome);

            Assert.Equal(expected, harness.ActiveCombatPet.XP);
        }
    }

    [Fact]
    public async Task PetGrant_ShouldMaintainThePersistedLevelForEveryOutcome()
    {
        // DATABASE.md §1/§3 + PET_RULES.md §5.4: Level is a PERSISTED column that
        // must agree with the XP formula after the grant — it is not left stale in
        // the database and not derived only in the client.
        foreach (var outcome in new[] { BattleOutcome.Victory, BattleOutcome.Defeat })
        {
            var harness = Harness.Create(activePetXp: 250);

            await harness.Service.PersistTerminalResultAsync(
                NewBattle($"pet-xp-level-maintained-{outcome}"),
                outcome);

            Assert.Equal(Pet.LevelForXp(harness.ActiveCombatPet.XP), harness.ActiveCombatPet.Level);
        }
    }
}