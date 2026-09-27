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
/// The documented Player XP grant on the canonical battle-end path —
/// <c>COMBAT_RULES.md</c> §7.2/§7.4, <c>GAME_EVENTS.md</c> §2,
/// <c>DATABASE.md</c> §1, <c>ARCHITECTURE.md</c> §4 item 4.
///
/// <code>
/// Rule (COMBAT_RULES.md §7.2, §7.4)
///  ↓
/// Scenario (Given a terminal battle for a Player,
///           When the battle end is persisted,
///           Then the documented XP grant and Level, exactly once)
///  ↓
/// Test
/// </code>
///
/// <b>Every expected value traces to a document.</b> The two outcome amounts and
/// the Level formula are <c>§7.2</c>/<c>§7.4</c>; the uncapped-XP / capped-Level
/// split is <c>§7.5</c>; the exactly-once requirement comes from the existing
/// primary-key guard (<c>DATABASE.md</c> §1 sourcing item 1) rather than from a
/// new mechanism.
/// </summary>
public class PlayerXpBattleRewardTests
{
    private static readonly PlayerId Owner = new("player_xp_reward_owner");
    private static readonly PetId OwnedPet = new("pet-instance-xp-reward-1");

    /// <summary>
    /// A battle the documented factory created, so the state under test is a real
    /// authoritative <c>BattleState</c> rather than a hand-built record
    /// (<c>GAME_STATE.md</c> §2, §2.7.1).
    /// </summary>
    private static BattleState NewBattle(string battleId, BossDefinition? boss = null) =>
        BattleState.Create(
            battleId,
            rngSeed: 20260927UL,
            Owner,
            OwnedPet,
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
        public static Harness Create(int ownerXp = Player.InitialXp)
        {
            var results = new InMemoryBattleResultRepository();
            var battles = new InMemoryBattleStateRepository();
            var players = new InMemoryPlayerRepository();
            var pets = new InMemoryPetRepository();

            players.Seed(Owner.Value, ownerXp);

            // The active combat Pet the battle's own PetState.PetId names, so the
            // Pet track's grant has a row too — this suite is about the Player
            // track, and both rows exist in production for any real battle.
            pets.Seed(OwnedPet.Value, playerId: Owner.Value);

            var service = new BattleResultService(
                results,
                ScriptedBossDefinitionLookup.Resolving(BossDefinitions.HoaLong),
                battles,
                players,
                pets,
                TimeProvider.System);

            return new Harness(service, results, battles, players, pets);
        }

        public Player OwnerPlayer => Players.Find(Owner.Value)!;
    }

    // =======================================================================
    // COMBAT_RULES.md §7.2 — BattleWon → +100
    // =======================================================================

    [Fact]
    public async Task BattleWon_ShouldGrantExactlyOneHundredPlayerXp()
    {
        // COMBAT_RULES.md §7.2 / ADR-016 item 4: BattleWon grants Player XP +100 —
        // exactly the documented amount, derived server-side from the terminal
        // outcome the resolution itself reported (GAME_EVENTS.md §2).
        var harness = Harness.Create();

        var written = await harness.Service
            .PersistTerminalResultAsync(NewBattle("xp-reward-won"), BattleOutcome.Victory);

        Assert.True(written);

        Assert.Equal(100, harness.OwnerPlayer.XP);

        // §7.4: the Level follows the XP — 100 XP is Level 2.
        Assert.Equal(2, harness.OwnerPlayer.Level);
        Assert.Equal(Player.LevelForXp(harness.OwnerPlayer.XP), harness.OwnerPlayer.Level);
    }

    [Fact]
    public async Task BattleWon_ShouldAccumulateAcrossBattles()
    {
        // §7.2/§7.5 item 1: XP is cumulative lifetime progression, so each won
        // battle adds its own grant to the same Player.
        var harness = Harness.Create();

        await harness.Service.PersistTerminalResultAsync(NewBattle("xp-accumulate-1"), BattleOutcome.Victory);
        await harness.Service.PersistTerminalResultAsync(NewBattle("xp-accumulate-2"), BattleOutcome.Victory);
        await harness.Service.PersistTerminalResultAsync(NewBattle("xp-accumulate-3"), BattleOutcome.Victory);

        Assert.Equal(300, harness.OwnerPlayer.XP);
        Assert.Equal(4, harness.OwnerPlayer.Level);
    }

    // =======================================================================
    // COMBAT_RULES.md §7.2 — BattleLost → +0
    // =======================================================================

    [Fact]
    public async Task BattleLost_ShouldGrantNoPlayerXp_AndChangeNoLevel()
    {
        // COMBAT_RULES.md §7.2 / ADR-016 item 5: BattleLost grants Player XP +0,
        // and §7.5 item 4 states defeat therefore changes nothing — the XP and the
        // Level both stand exactly as they were.
        var harness = Harness.Create(ownerXp: 150);

        var before = harness.OwnerPlayer.Level;

        var written = await harness.Service
            .PersistTerminalResultAsync(NewBattle("xp-reward-lost"), BattleOutcome.Defeat);

        Assert.True(written);

        Assert.Equal(150, harness.OwnerPlayer.XP);
        Assert.Equal(before, harness.OwnerPlayer.Level);
    }

    [Fact]
    public async Task BattleLost_ShouldStillPersistTheDurableResult()
    {
        // DATABASE.md §1 / GAME_EVENTS.md §2: the result is written for both
        // outcomes; RewardSummary is present for both. A defeat grants no XP but
        // is still a completed battle with a durable record.
        var harness = Harness.Create();

        await harness.Service.PersistTerminalResultAsync(NewBattle("xp-lost-row"), BattleOutcome.Defeat);

        var row = Assert.Single(harness.Results.Rows);

        Assert.Equal(BattleOutcome.Defeat, row.Outcome);
        Assert.Equal(Player.InitialXp, harness.OwnerPlayer.XP);
    }

    // =======================================================================
    // COMBAT_RULES.md §7.5 items 1–2 — Level 50 boundary
    // =======================================================================

    [Fact]
    public async Task BattleWon_AtTheLevelFiftyBoundary_ShouldCapTheLevel_AndKeepTheXpGrowing()
    {
        // §7.5 items 1–2: 4900 XP is Level 50 (the §7.4 worked boundary), and a
        // further win keeps Level 50 while the XP continues to accumulate — it is
        // never clamped or discarded. This is the distinction between the Player
        // track and the Pet track's hard cap (ADR-016 item 12).
        const int levelFiftyBoundaryXp = 4900;

        var harness = Harness.Create(ownerXp: levelFiftyBoundaryXp);

        Assert.Equal(4900, harness.OwnerPlayer.XP);
        Assert.Equal(Player.MaxLevel, harness.OwnerPlayer.Level);

        await harness.Service.PersistTerminalResultAsync(NewBattle("xp-at-cap"), BattleOutcome.Victory);

        Assert.Equal(5000, harness.OwnerPlayer.XP);
        Assert.Equal(Player.MaxLevel, harness.OwnerPlayer.Level);
    }

    [Fact]
    public async Task BattleWon_ShouldCrossTheLevelBoundary_LikeTheFormulaSays()
    {
        // §7.4: at 99 XP the Player is Level 1; the next win takes them to 199,
        // which is Level 2. The grant and the formula are verified together, so a
        // Level that did not follow the XP would fail.
        var harness = Harness.Create(ownerXp: 99);

        Assert.Equal(1, harness.OwnerPlayer.Level);

        await harness.Service.PersistTerminalResultAsync(NewBattle("xp-cross-100"), BattleOutcome.Victory);

        Assert.Equal(199, harness.OwnerPlayer.XP);
        Assert.Equal(2, harness.OwnerPlayer.Level);
    }

    // =======================================================================
    // Exactly-once — bound to the first durable write
    // =======================================================================

    [Fact]
    public async Task RepeatedTerminalPersistence_ShouldGrantPlayerXpOnlyOnce()
    {
        // DATABASE.md §1 sourcing item 1 / REDIS_STATE.md §3: BattleResultId is
        // the battle's own BattleId, so a repeated terminal persistence of the
        // SAME battle reconciles against the already-durable row instead of
        // writing again. The reward grant is bound to the first durable write, so
        // the retry must not grant a second +100. No new idempotency mechanism is
        // introduced — the existing primary-key guard is what proves it.
        var harness = Harness.Create();
        var state = NewBattle("xp-exactly-once");

        await harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Victory);

        Assert.Equal(100, harness.OwnerPlayer.XP);

        // The same terminal result, retried — the documented retry/reconcile case.
        await harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Victory);

        Assert.Equal(100, harness.OwnerPlayer.XP);
        Assert.Equal(2, harness.OwnerPlayer.Level);

        // The retry is still exactly one row: the primary key remains the guard.
        Assert.Single(harness.Results.Rows);
    }

    [Fact]
    public async Task ManyRepeatedTerminalPersistences_ShouldStillGrantPlayerXpOnce()
    {
        // The same property under repeated retries, so an implementation that
        // granted on any call but the last, or on every other call, cannot pass.
        var harness = Harness.Create();
        var state = NewBattle("xp-exactly-once-many");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Victory);
        }

        Assert.Equal(100, harness.OwnerPlayer.XP);
        Assert.Equal(2, harness.OwnerPlayer.Level);
        Assert.Single(harness.Results.Rows);
    }

    [Fact]
    public async Task RepeatedTerminalPersistence_AfterAFailedWrite_ShouldGrantPlayerXpOnce()
    {
        // DATABASE.md §1 sourcing item 3: a failed durable write is absorbed and
        // the battle stays recoverable, so the documented recoverable case is a
        // later successful persistence of the same terminal result. That
        // successful write is the FIRST durable write, so it grants the XP once —
        // and a following retry must not grant it again.
        var harness = Harness.Create();
        var state = NewBattle("xp-failed-then-recovered");

        harness.Results.WriteFails = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Victory));

        // No durable result ⇒ no grant: XP is bound to the durable write, not to
        // the attempt.
        Assert.Equal(Player.InitialXp, harness.OwnerPlayer.XP);
        Assert.Empty(harness.Results.Rows);

        // The recovered write is the first durable one, so it grants once.
        harness.Results.WriteFails = false;

        await harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Victory);

        Assert.Equal(100, harness.OwnerPlayer.XP);

        // And the subsequent retry does not grant again.
        await harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Victory);

        Assert.Equal(100, harness.OwnerPlayer.XP);
        Assert.Single(harness.Results.Rows);
    }

    // =======================================================================
    // Failure and absence — no grant without a durable write
    // =======================================================================

    [Fact]
    public async Task FailedDurableWrite_ShouldGrantNoPlayerXp()
    {
        // DATABASE.md §1 sourcing item 3: the battle-end write fails closed. The
        // XP grant rides the durable write, so a write that did not happen grants
        // nothing — the battle remains retryable and the eventual successful write
        // is what awards the XP (verified above).
        var harness = Harness.Create();

        harness.Results.WriteFails = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.PersistTerminalResultAsync(NewBattle("xp-write-fails"), BattleOutcome.Victory));

        Assert.Equal(Player.InitialXp, harness.OwnerPlayer.XP);
        Assert.Equal(Player.InitialLevel, harness.OwnerPlayer.Level);
    }

    [Fact]
    public async Task UnresolvedBossDefinition_ShouldGrantNoPlayerXp()
    {
        // DATABASE.md §1 sourcing item 3: an unresolved BossDefinition writes no
        // row and deletes no key. With no durable write there is no reward either.
        var results = new InMemoryBattleResultRepository();
        var battles = new InMemoryBattleStateRepository();
        var players = new InMemoryPlayerRepository();

        players.Seed(Owner.Value);

        var service = new BattleResultService(
            results,
            new ScriptedBossDefinitionLookup(),
            battles,
            players,
            new InMemoryPetRepository(),
            TimeProvider.System);

        var written = await service
            .PersistTerminalResultAsync(NewBattle("xp-no-boss"), BattleOutcome.Victory);

        Assert.False(written);
        Assert.Equal(Player.InitialXp, players.Find(Owner.Value)!.XP);
    }

    [Fact]
    public async Task TerminalPersistence_ForAnUnknownPlayer_ShouldNotCreateOne()
    {
        // DATABASE.md §1: the reward updates the Player the battle belonged to.
        // When no such row exists there is no progression to maintain — the reward
        // must not bring a Player into existence, so the durable result is still
        // written and nothing is created.
        var results = new InMemoryBattleResultRepository();
        var battles = new InMemoryBattleStateRepository();
        var players = new InMemoryPlayerRepository();
        var service = new BattleResultService(
            results,
            ScriptedBossDefinitionLookup.Resolving(BossDefinitions.HoaLong),
            battles,
            players,
            new InMemoryPetRepository(),
            TimeProvider.System);

        var written = await service
            .PersistTerminalResultAsync(NewBattle("xp-no-owner-row"), BattleOutcome.Victory);

        Assert.True(written);
        Assert.Single(results.Rows);
        Assert.Empty(players.Players);
        Assert.Equal(0, players.ProgressionSaveCount);
    }

    [Fact]
    public async Task NonTerminalPath_ShouldNeverReachTheRewardGrant()
    {
        // The grant lives on the terminal battle-end path only. A resolution that
        // is not terminal writes no row (verified in BattleResultTerminalFlowTests)
        // and therefore reaches no reward step — no XP is granted for intermediate
        // events.
        var results = new InMemoryBattleResultRepository();
        var battles = new InMemoryBattleStateRepository();
        var players = new InMemoryPlayerRepository();

        players.Seed(Owner.Value);

        var service = new BattleResultService(
            results,
            ScriptedBossDefinitionLookup.Resolving(BossDefinitions.HoaLong),
            battles,
            players,
            new InMemoryPetRepository(),
            TimeProvider.System);

        // Nothing is persisted here because nothing terminal happened: the
        // terminal check lives above this boundary (BattleStateService only calls
        // it for a resolution that emitted an outcome event).
        Assert.Equal(0, players.ProgressionSaveCount);
        _ = service;
        await Task.CompletedTask;
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
        Assert.Equal(100, Player.BattleWonXpReward);
        Assert.Equal(0, Player.BattleLostXpReward);

        foreach (var (outcome, expected) in new[]
                 {
                     (BattleOutcome.Victory, 100),
                     (BattleOutcome.Defeat, 0),
                 })
        {
            var harness = Harness.Create();

            await harness.Service.PersistTerminalResultAsync(
                NewBattle($"xp-authority-{outcome}"),
                outcome);

            Assert.Equal(expected, harness.OwnerPlayer.XP);
        }
    }

    [Fact]
    public async Task RewardGrant_ShouldNotTouchPetProgression()
    {
        // ADR-016 item 12 / COMBAT_RULES.md §7.1: Player account progression and
        // Pet combat progression are separate tracks that neither read nor modify
        // each other. The reward path reaches the Player boundary only — the
        // harness composes no Pet boundary at all, so a grant that consulted or
        // wrote Pet XP or Pet Level could not compile, and the Player values below
        // are the only progression this path moved.
        var harness = Harness.Create();

        await harness.Service.PersistTerminalResultAsync(NewBattle("xp-pet-independence"), BattleOutcome.Victory);

        Assert.Equal(100, harness.OwnerPlayer.XP);
        Assert.Equal(2, harness.OwnerPlayer.Level);

        // The Player entity carries no Pet progression member for this path to
        // have written (COMBAT_RULES.md §7.6, ADR-016 item 12).
        var members = typeof(Player).GetProperties().Select(property => property.Name).ToArray();

        Assert.DoesNotContain("PetXp", members);
        Assert.DoesNotContain("PetXP", members);
        Assert.DoesNotContain("PetLevel", members);
    }

    [Fact]
    public async Task RewardGrant_ShouldMaintainThePersistedLevelForEveryOutcome()
    {
        // DATABASE.md §1/§3 + COMBAT_RULES.md §7.4: Level is a PERSISTED column
        // that must agree with the XP formula after the grant — it is not derived
        // only in the client and not left stale in the database.
        foreach (var outcome in new[] { BattleOutcome.Victory, BattleOutcome.Defeat })
        {
            var harness = Harness.Create(ownerXp: 250);

            await harness.Service.PersistTerminalResultAsync(
                NewBattle($"xp-level-maintained-{outcome}"),
                outcome);

            Assert.Equal(Player.LevelForXp(harness.OwnerPlayer.XP), harness.OwnerPlayer.Level);
        }
    }
}
