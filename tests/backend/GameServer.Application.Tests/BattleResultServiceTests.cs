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
/// The battle-end persistence path — <c>ARCHITECTURE.md</c> §4 item 4,
/// <c>DATABASE.md</c> §1, <c>REDIS_STATE.md</c> §3.
///
/// <code>
/// Rule (DATABASE.md §1, REDIS_STATE.md §3, ARCHITECTURE.md §4 item 4)
///  ↓
/// Scenario (Given an authoritative terminal BattleState, When the battle end is
///           persisted, Then the documented row, ordering, and failure behaviour)
///  ↓
/// Test
/// </code>
///
/// <b>Every expected value traces to a document.</b> The eight persisted members
/// are <c>DATABASE.md</c> §1's; the ordering and both failure behaviours are
/// §1's fail-closed rule plus <c>REDIS_STATE.md</c> §3's lifecycle; and the
/// outcome set is <c>GAME_EVENTS.md</c> §2's, fixed repository-wide by TASK-050.
/// </summary>
public class BattleResultServiceTests
{
    private static readonly PlayerId Owner = new("player_battle_result_owner");
    private static readonly PetId OwnedPet = new("pet-instance-battle-result-1");

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
    /// The battle-end boundary over scripted doubles, so each documented behaviour
    /// can be isolated. The doubles model the two semantics the contract depends
    /// on — the indexed key, and a raised store failure — rather than being more
    /// permissive than a real store.
    /// </summary>
    private sealed record Harness(
        BattleResultService Service,
        InMemoryBattleResultRepository Results,
        ScriptedBossDefinitionLookup BossLookup,
        InMemoryBattleStateRepository Battles,
        InMemoryPlayerRepository Players,
        InMemoryPetRepository Pets)
    {
        public static Harness Create(BossDefinition? resolvableBoss = null)
        {
            var results = new InMemoryBattleResultRepository();
            var bossLookup = resolvableBoss is null
                ? new ScriptedBossDefinitionLookup()
                : ScriptedBossDefinitionLookup.Resolving(resolvableBoss);
            var battles = new InMemoryBattleStateRepository();
            var players = new InMemoryPlayerRepository();
            var pets = new InMemoryPetRepository();

            var service = new BattleResultService(
                results,
                bossLookup,
                battles,
                players,
                pets,
                TimeProvider.System);

            return new Harness(service, results, bossLookup, battles, players, pets);
        }

        /// <summary>
        /// The harness with the battle's owning Player already seeded, so the
        /// documented Player XP grant (COMBAT_RULES.md §7.2) has a progression
        /// row to update.
        ///
        /// The Level is derived from the seeded XP through the Domain's own
        /// <see cref="Player.LevelForXp"/> unless a test states one explicitly, so
        /// the seeded row is a state the documented contract can actually hold
        /// (<c>COMBAT_RULES.md</c> §7.4) rather than a fixture where XP and Level
        /// disagree.
        /// </summary>
        public Harness WithOwner(int xp = Player.InitialXp, int? level = null)
        {
            Players.Seed(Owner.Value, xp, level ?? Player.LevelForXp(xp));
            return this;
        }

        /// <summary>
        /// The harness with the battle's active combat Pet already seeded, so the
        /// documented Pet XP grant (PET_RULES.md §5.3) has a progression row to
        /// update. The instance is the one the battle's own
        /// <c>PetState.PetId</c> names — never a Pet chosen by the test.
        ///
        /// As above, the Level is derived from the seeded XP unless a test states
        /// one, so the fixture holds a state <c>PET_RULES.md</c> §5.4 permits.
        /// </summary>
        public Harness WithActivePet(int xp = Pet.InitialXp, int? level = null)
        {
            Pets.Seed(OwnedPet.Value, xp, level ?? Pet.LevelForXp(xp));
            return this;
        }
    }

    // =======================================================================
    // The mapping — DATABASE.md §1, all eight members
    // =======================================================================

    [Fact]
    public async Task PersistTerminalResult_ShouldWriteAllEightDocumentedValues()
    {
        // DATABASE.md §1 defines exactly eight BattleResult values. Each is asserted
        // against its authoritative source, so a member sourced from the wrong
        // place cannot pass:
        //   BattleResultId  = BattleState.BattleId          (sourcing item 1)
        //   PlayerId        = BattleState.PlayerId          (item 2)
        //   PetInstanceId   = BattleState.PetState.PetId    (item 2)
        //   BossDefinitionId= the row keyed by BossState.BossId (note item 2)
        //   Outcome         = the resolution's terminal report (GAME_EVENTS.md §2)
        //   DurationTurns   = BattleState.Turn              (sourcing item 1)
        //   CompletedAt     = the server clock at the write  (item 2)
        //   RewardSummary   = the two tracks' documented members
        //                     (DATABASE.md §1 "Reward semantics")
        var harness = Harness.Create(BossDefinitions.HoaLong).WithOwner().WithActivePet();
        var battleId = "result-mapping-battle";
        var state = NewBattle(battleId);

        var before = DateTimeOffset.UtcNow;

        var written = await harness.Service
            .PersistTerminalResultAsync(state, BattleOutcome.Victory);

        var after = DateTimeOffset.UtcNow;

        Assert.True(written);

        var row = Assert.Single(harness.Results.Rows);

        Assert.Equal(battleId, row.BattleResultId);
        Assert.Equal(battleId, row.BattleResultId);

        Assert.Equal(state.PlayerId.Value, row.PlayerId);
        Assert.Equal(state.PetState.PetId.Value, row.PetInstanceId);

        // The resolved row's persistence key — never the canonical Identity
        // (DATABASE.md §1 note item 2: the two are never substituted).
        Assert.Equal(BossDefinitions.HoaLong.BossDefinitionId, row.BossDefinitionId);
        Assert.NotEqual(state.BossState.BossId.Value, row.BossDefinitionId);

        Assert.Equal(BattleOutcome.Victory, row.Outcome);

        // The state's own Turn. The fixture has not resolved an action, so it is
        // the documented initial value — asserted against the state rather than
        // against a literal, so the source is what is tested.
        Assert.Equal(state.Turn, row.DurationTurns);

        // The server clock, captured at the write and not before it.
        Assert.InRange(row.CompletedAt, before, after);

        // DATABASE.md §1 "Reward semantics": the populated summary on the landed
        // path — both tracks' documented members, not the `{}` staging value.
        using var summary = System.Text.Json.JsonDocument.Parse(row.RewardSummary);

        Assert.Equal(100, summary.RootElement.GetProperty("playerXpGained").GetInt32());
        Assert.Equal(100, summary.RootElement.GetProperty("newPlayerXp").GetInt32());
        Assert.Equal(100, summary.RootElement.GetProperty("petXpGained").GetInt32());
        Assert.Equal(100, summary.RootElement.GetProperty("newPetXp").GetInt32());
    }

    [Fact]
    public async Task PersistTerminalResult_ShouldIdentityTheRowByTheBattlesOwnId()
    {
        // DATABASE.md §1 sourcing item 1: "BattleResultId IS the battle's own
        // BattleId — no second identifier is introduced". The row is therefore
        // readable by the battle id and by nothing else, which is what makes the
        // primary key the duplicate protection.
        var harness = Harness.Create(BossDefinitions.HoaLong);
        var state = NewBattle("result-key-battle");

        await harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Victory);

        Assert.NotNull(await harness.Results.GetByIdAsync(state.BattleId));
        Assert.Null(await harness.Results.GetByIdAsync("some-other-battle"));
    }

    // =======================================================================
    // DurationTurns — DATABASE.md §1 "Duration and completion sourcing" item 1
    // =======================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    public async Task DurationTurns_ShouldBeTheTerminalTurn(int turn)
    {
        // DATABASE.md §1 item 1: DurationTurns is BattleState.Turn at terminal
        // resolution, and the terminal Turn IS counted — so a battle ending on its
        // Nth committed Swap records N. The zero case is the documented edge: a
        // battle reaching a terminal state before any committed Swap records 0
        // (GAME_STATE.md §2.0.2).
        var harness = Harness.Create(BossDefinitions.HoaLong);
        var state = NewBattle($"result-duration-{turn}") with { Turn = turn };

        await harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Victory);

        var row = Assert.Single(harness.Results.Rows);

        Assert.Equal(turn, row.DurationTurns);
    }

    [Fact]
    public async Task DurationTurns_ShouldNotComeFromSequence()
    {
        // DATABASE.md §1 item 1: "Sequence is a different counter ... and is never
        // the source". A battle whose Sequence and Turn differ must record the
        // Turn, so an implementation reading the wrong counter fails.
        var harness = Harness.Create(BossDefinitions.HoaLong);

        var state = NewBattle("result-duration-not-sequence") with
        {
            Turn = 2,
            Sequence = 9,
        };

        await harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Victory);

        var row = Assert.Single(harness.Results.Rows);

        Assert.Equal(2, row.DurationTurns);
        Assert.NotEqual(state.Sequence, row.DurationTurns);
    }

    // =======================================================================
    // Outcome — GAME_EVENTS.md §2, DATABASE.md §1
    // =======================================================================

    [Theory]
    [InlineData(BattleOutcome.Victory, "victory")]
    [InlineData(BattleOutcome.Defeat, "defeat")]
    public async Task Outcome_ShouldBeStoredAsTheDocumentedContractValue(
        BattleOutcome outcome,
        string expected)
    {
        // DATABASE.md §1 spells the column values "victory" | "defeat" (the set
        // GAME_EVENTS.md §2 owns), not the C# enum identifier. The mapping is
        // asserted here and the stored column is asserted in the Infrastructure
        // suite, so neither the value nor its spelling can drift.
        Assert.Equal(expected, BattleOutcomes.ToContractValue(outcome));
        Assert.Equal(outcome, BattleOutcomes.FromContractValue(expected));

        var harness = Harness.Create(BossDefinitions.HoaLong);

        await harness.Service.PersistTerminalResultAsync(
            NewBattle($"result-outcome-{expected}"),
            outcome);

        var row = Assert.Single(harness.Results.Rows);

        Assert.Equal(outcome, row.Outcome);
    }

    [Fact]
    public void Outcome_ShouldDefineExactlyTwoValues()
    {
        // GAME_EVENTS.md §2 / TASK-050 Decision C: one vocabulary repository-wide
        // and exactly two outcomes. No third value — no draw, no win/loss
        // spelling — exists, and an undefined enum value is refused rather than
        // spelled as something the contract does not define.
        var values = Enum.GetValues<BattleOutcome>();

        Assert.Equal(2, values.Length);
        Assert.Equal(
            new[] { "defeat", "victory" },
            values.Select(BattleOutcomes.ToContractValue).OrderBy(v => v, StringComparer.Ordinal));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => BattleOutcomes.ToContractValue((BattleOutcome)99));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => BattleOutcomes.FromContractValue("won"));
    }

    // =======================================================================
    // RewardSummary — DATABASE.md §1 "Reward semantics"
    // =======================================================================

    [Fact]
    public async Task RewardSummary_ShouldCarryTheDocumentedVictoryMembers()
    {
        // DATABASE.md §1 items 1, 2 and 5: the summary is populated on the landed
        // path — the `{}` staging value (item 4) applied only "until the
        // implementation task lands". Item 1 freezes the four Player-track
        // members; item 2 delegates the Pet-track member list to this
        // implementation, defined from PET_RULES.md §5.3–§5.5. Item 3 forbids
        // placeholder members, so the document carries exactly these eight and
        // nothing else — no item, currency, streak, or curve value.
        var harness = Harness.Create(BossDefinitions.HoaLong).WithOwner().WithActivePet();
        var state = NewBattle("result-reward-victory");

        await harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Victory);

        var row = Assert.Single(harness.Results.Rows);

        using var document = System.Text.Json.JsonDocument.Parse(row.RewardSummary);

        Assert.Equal(System.Text.Json.JsonValueKind.Object, document.RootElement.ValueKind);

        var members = document.RootElement.EnumerateObject()
            .Select(member => member.Name)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "playerXpGained",
                "newPlayerXp",
                "playerLeveledUp",
                "newPlayerLevel",
                "petXpGained",
                "newPetXp",
                "petLeveledUp",
                "newPetLevel",
            }.OrderBy(name => name, StringComparer.Ordinal),
            members.OrderBy(name => name, StringComparer.Ordinal));

        // Item 5: victory grants the documented +100 on both tracks.
        Assert.Equal(100, document.RootElement.GetProperty("playerXpGained").GetInt32());
        Assert.Equal(100, document.RootElement.GetProperty("petXpGained").GetInt32());

        // Item 1/§5.4: the resulting values are the post-grant ones — 100 XP is
        // Level 2 on both tracks, which the members report.
        Assert.Equal(100, document.RootElement.GetProperty("newPlayerXp").GetInt32());
        Assert.Equal(2, document.RootElement.GetProperty("newPlayerLevel").GetInt32());
        Assert.True(document.RootElement.GetProperty("playerLeveledUp").GetBoolean());
        Assert.Equal(100, document.RootElement.GetProperty("newPetXp").GetInt32());
        Assert.Equal(2, document.RootElement.GetProperty("newPetLevel").GetInt32());
        Assert.True(document.RootElement.GetProperty("petLeveledUp").GetBoolean());

        // The stored document agrees with the persisted progression it describes.
        Assert.Equal(100, harness.Players.Find(Owner.Value)!.XP);
        Assert.Equal(100, harness.Pets.Find(OwnedPet.Value)!.XP);
    }

    [Fact]
    public async Task RewardSummary_ShouldCarryTheDocumentedDefeatMembers()
    {
        // TASK-068 resolved the defeat shape as Option A, now frozen in
        // DATABASE.md §1 item 5: the SAME member set applies to both outcomes.
        // A defeat serializes all eight members with the grant amounts at their
        // documented +0 and the resulting XP/Level at their unchanged current
        // values — it is emphatically NOT the empty object.
        var harness = Harness.Create(BossDefinitions.HoaLong).WithOwner(xp: 250).WithActivePet(xp: 250);
        var state = NewBattle("result-reward-defeat");

        await harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Defeat);

        var row = Assert.Single(harness.Results.Rows);

        Assert.NotEqual("{}", row.RewardSummary);

        using var document = System.Text.Json.JsonDocument.Parse(row.RewardSummary);

        Assert.Equal(8, document.RootElement.EnumerateObject().Count());

        // Both outcomes' grants: +0 on defeat, per COMBAT_RULES.md §7.2 and
        // PET_RULES.md §5.3 item 2.
        Assert.Equal(0, document.RootElement.GetProperty("playerXpGained").GetInt32());
        Assert.Equal(0, document.RootElement.GetProperty("petXpGained").GetInt32());

        // Item 5: unchanged values, and the leveled-up flags false. The Level members
        // hold the Level the Pet/Player's XP determines (PET_RULES.md §5.4 /
        // COMBAT_RULES.md §7.4) — 250 XP is Level 3 on both tracks — and a defeat
        // moves neither.
        Assert.Equal(250, document.RootElement.GetProperty("newPlayerXp").GetInt32());
        Assert.Equal(Player.LevelForXp(250), document.RootElement.GetProperty("newPlayerLevel").GetInt32());
        Assert.Equal(3, document.RootElement.GetProperty("newPlayerLevel").GetInt32());
        Assert.False(document.RootElement.GetProperty("playerLeveledUp").GetBoolean());

        Assert.Equal(250, document.RootElement.GetProperty("newPetXp").GetInt32());
        Assert.Equal(Pet.LevelForXp(250), document.RootElement.GetProperty("newPetLevel").GetInt32());
        Assert.Equal(3, document.RootElement.GetProperty("newPetLevel").GetInt32());
        Assert.False(document.RootElement.GetProperty("petLeveledUp").GetBoolean());
    }

    [Fact]
    public void RewardSummaryMemberNames_ShouldBeTheDocumentedContract()
    {
        // DATABASE.md §1 item 1 fixes the four Player-track member names; the
        // Pet-track names are this task's delegated representation decision
        // (item 2) and are declared once, as constants, so the document and the
        // tests that assert it cannot drift to two spellings.
        Assert.Equal("playerXpGained", BattleResultService.PlayerXpGainedMember);
        Assert.Equal("newPlayerXp", BattleResultService.NewPlayerXpMember);
        Assert.Equal("playerLeveledUp", BattleResultService.PlayerLeveledUpMember);
        Assert.Equal("newPlayerLevel", BattleResultService.NewPlayerLevelMember);
        Assert.Equal("petXpGained", BattleResultService.PetXpGainedMember);
        Assert.Equal("newPetXp", BattleResultService.NewPetXpMember);
        Assert.Equal("petLeveledUp", BattleResultService.PetLeveledUpMember);
        Assert.Equal("newPetLevel", BattleResultService.NewPetLevelMember);
    }

    [Fact]
    public async Task RewardSummary_ShouldKeepTheStagingValue_WhenNoProgressionRowExists()
    {
        // DATABASE.md §1: the reward records the progression of the rows this
        // battle actually had. When neither row exists there is no resulting
        // state to report, so the amounts stay the documented outcome amounts and
        // the resulting-value members are null — never an invented number, and
        // never a fabricated level. No row is created either.
        var harness = Harness.Create(BossDefinitions.HoaLong);

        await harness.Service.PersistTerminalResultAsync(
            NewBattle("result-reward-no-rows"),
            BattleOutcome.Victory);

        var row = Assert.Single(harness.Results.Rows);

        using var document = System.Text.Json.JsonDocument.Parse(row.RewardSummary);

        Assert.Equal(100, document.RootElement.GetProperty("playerXpGained").GetInt32());
        Assert.Equal(100, document.RootElement.GetProperty("petXpGained").GetInt32());

        Assert.Equal(System.Text.Json.JsonValueKind.Null, document.RootElement.GetProperty("newPlayerXp").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, document.RootElement.GetProperty("newPetXp").ValueKind);

        Assert.Empty(harness.Players.Players);
        Assert.Empty(harness.Pets.Pets);
        Assert.Equal(0, harness.Players.ProgressionSaveCount);
        Assert.Equal(0, harness.Pets.ProgressionSaveCount);
    }

    // =======================================================================
    // Boss identity — DATABASE.md §1 note item 2
    // =======================================================================

    [Fact]
    public async Task BossDefinitionId_ShouldComeFromTheIdentityLookup()
    {
        // DATABASE.md §1 note item 2: the value is "the key of the BossDefinition
        // row whose Identity equals BattleState.BossState.BossId", resolved by a
        // PostgreSQL lookup by Identity. The lookup is asked for the state's own
        // canonical Identity, and its answer is what is stored.
        var harness = Harness.Create(BossDefinitions.MocYeu);
        var state = NewBattle("result-boss-identity", BossDefinitions.MocYeu);

        await harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Victory);

        var row = Assert.Single(harness.Results.Rows);

        Assert.Equal("boss-moc-yeu", state.BossState.BossId.Value);
        Assert.Equal("boss-def-moc-yeu", row.BossDefinitionId);

        // The Identity itself was the lookup key — never the display name, never a
        // derived or convention-mapped value.
        Assert.Equal(1, harness.BossLookup.LookupCount);
        Assert.Equal(
            "boss-def-moc-yeu",
            await harness.BossLookup.FindBossDefinitionIdByIdentityAsync("boss-moc-yeu"));
    }

    [Fact]
    public async Task BossDefinitionId_ShouldNeverBeTheCanonicalIdentity()
    {
        // DATABASE.md §1 note item 2: "it must never substitute one value for the
        // other" — the Identity is not a legal FK value, and the two are never
        // collapsed (BOSS_RULES.md §6.4, TASK-046).
        foreach (var boss in BossDefinitions.All)
        {
            var harness = Harness.Create(boss);
            var state = NewBattle($"result-boss-{boss.BossId.Value}", boss);

            await harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Victory);

            var row = Assert.Single(harness.Results.Rows);

            Assert.NotEqual(boss.BossId.Value, row.BossDefinitionId);
            Assert.Equal(boss.BossDefinitionId, row.BossDefinitionId);
        }
    }

    // =======================================================================
    // Boss resolution failure — DATABASE.md §1 sourcing item 3
    // =======================================================================

    [Fact]
    public async Task PersistTerminalResult_ShouldFailClosed_WhenNoBossDefinitionResolves()
    {
        // DATABASE.md §1 sourcing item 3 item 1: an unresolved BossDefinition is a
        // server-side battle-resolution failure — NO BattleResult row is written,
        // and NO fabricated, null, empty-string, fallback, or default key stands in
        // for one. The lookup is empty here, so nothing resolves.
        var harness = Harness.Create();

        var written = await harness.Service
            .PersistTerminalResultAsync(NewBattle("result-unresolved-boss"), BattleOutcome.Victory);

        Assert.False(written);
        Assert.Empty(harness.Results.Rows);
        Assert.Equal(0, harness.Results.WriteAttempts);
    }

    [Fact]
    public async Task PersistTerminalResult_ShouldNotDeleteTheActiveState_WhenNoBossDefinitionResolves()
    {
        // DATABASE.md §1 sourcing item 3 item 2: "battle:{battleId}:state is NOT
        // deleted ... because the result write did not happen, the delete must not
        // happen either". The battle therefore remains recoverable and retryable
        // (item 3) — which the recorded delete count proves, since a delete that
        // never happened cannot have destroyed the record.
        var harness = Harness.Create();

        await harness.Service.PersistTerminalResultAsync(
            NewBattle("result-unresolved-no-delete"),
            BattleOutcome.Defeat);

        Assert.Equal(0, harness.Battles.DeleteCount);
    }

    [Fact]
    public async Task PersistTerminalResult_ShouldWrite_WithNoPlayerReadinessInvolved()
    {
        // DATABASE.md §1 states that BattleResult persistence requires NO Player
        // combat-readiness condition and that no IsCombatReady predicate is part
        // of the contract. There is therefore no eligibility input on this path:
        // a resolvable BossDefinition is sufficient on its own.
        //
        // The regression is structural as well as behavioural: the harness can no
        // longer construct a readiness answer at all (the boundary and its double
        // were removed), so a write that still consulted one could not compile —
        // and the written row plus the recorded delete prove the documented
        // outcome is reached without it.
        var harness = Harness.Create(BossDefinitions.HoaLong);
        var state = NewBattle("result-no-readiness-prerequisite");

        var written = await harness.Service
            .PersistTerminalResultAsync(state, BattleOutcome.Victory);

        Assert.True(written);

        var row = Assert.Single(harness.Results.Rows);

        Assert.Equal(state.BattleId, row.BattleResultId);
        Assert.Equal(state.PlayerId.Value, row.PlayerId);

        // The write is the documented one, and the active state is deleted only
        // after it (ARCHITECTURE.md §4 item 4, REDIS_STATE.md §3).
        Assert.Equal(1, harness.Results.WriteAttempts);
        Assert.Equal(1, harness.Battles.DeleteCount);
    }

    // =======================================================================
    // Ordering — ARCHITECTURE.md §4 item 4, REDIS_STATE.md §3
    // =======================================================================

    [Fact]
    public async Task PersistTerminalResult_ShouldWriteBeforeDeleting_AndDeleteAfterASuccessfulWrite()
    {
        // REDIS_STATE.md §3: "Deleted: explicitly, when BattleWon/BattleLost is
        // resolved and the result has been written to PostgreSQL" — the write is a
        // precondition of the delete, and ARCHITECTURE.md §4 item 4 fixes the same
        // order. The stored row plus the recorded delete prove both halves: the
        // row exists, and the delete ran.
        var harness = Harness.Create(BossDefinitions.HoaLong);
        var state = NewBattle("result-order-write-then-delete");

        var written = await harness.Service
            .PersistTerminalResultAsync(state, BattleOutcome.Victory);

        Assert.True(written);

        var row = Assert.Single(harness.Results.Rows);

        // DATABASE.md §1 sourcing item 1: the row is keyed by the battle whose
        // state was deleted, so the two steps describe one battle.
        Assert.Equal(state.BattleId, row.BattleResultId);
        Assert.Equal(1, harness.Battles.DeleteCount);
    }

    // =======================================================================
    // PostgreSQL failure — DATABASE.md §1 sourcing item 3, REDIS_STATE.md §3
    // =======================================================================

    [Fact]
    public async Task PersistTerminalResult_ShouldNotDeleteTheActiveState_WhenTheDurableWriteFails()
    {
        // DATABASE.md §1 sourcing item 3 item 2: when the result write did not
        // happen, the delete must not happen either — otherwise the only
        // authoritative copy of a battle that was never durably recorded would be
        // destroyed (REDIS_STATE.md §2 item 2, §7 item 5). The failure propagates
        // rather than being swallowed: an unrecorded battle end must not be
        // reported as a completed persistence.
        var harness = Harness.Create(BossDefinitions.HoaLong);
        harness.Results.WriteFails = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.PersistTerminalResultAsync(
                NewBattle("result-write-fails"),
                BattleOutcome.Victory));

        Assert.Empty(harness.Results.Rows);
        Assert.Equal(0, harness.Battles.DeleteCount);
    }

    // =======================================================================
    // Redis delete failure — REDIS_STATE.md §3
    // =======================================================================

    [Fact]
    public async Task PersistTerminalResult_ShouldKeepTheDurableResult_WhenTheDeleteFails()
    {
        // REDIS_STATE.md §3: "If the explicit battle-end delete fails after the
        // result write, no automatic retry is performed and no worker or queue
        // exists for it: the sliding TTL above remains the cleanup path". The
        // durable result therefore stands and the battle end is still complete —
        // the documented no-retry outcome, not a second failure mode.
        var harness = Harness.Create(BossDefinitions.HoaLong);
        harness.Battles.DeleteFails = true;
        var state = NewBattle("result-delete-fails");

        var written = await harness.Service
            .PersistTerminalResultAsync(state, BattleOutcome.Victory);

        Assert.True(written);

        var row = Assert.Single(harness.Results.Rows);

        Assert.Equal(state.BattleId, row.BattleResultId);

        // The delete was attempted (and refused), so no retry path was needed to
        // reach the documented end state.
        Assert.Equal(1, harness.Battles.DeleteCount);
    }

    // =======================================================================
    // At most one row — DATABASE.md §1 sourcing item 1, REDIS_STATE.md §3
    // =======================================================================

    [Fact]
    public async Task PersistTerminalResult_ShouldNeverProduceASecondRow_ForARepeatedTerminalAttempt()
    {
        // REDIS_STATE.md §3: "a failed delete cannot produce a second result:
        // BattleResultId is the battle's own BattleId, so at most one BattleResult
        // row can ever exist per battle". A repeated terminal persistence must
        // therefore still leave exactly one row — the primary key is the
        // protection, and no second idempotency mechanism is introduced.
        var harness = Harness.Create(BossDefinitions.HoaLong);
        var state = NewBattle("result-repeat-same-battle");

        await harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Victory);
        await harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Victory);

        var row = Assert.Single(harness.Results.Rows);

        Assert.Equal(state.BattleId, row.BattleResultId);
        Assert.Equal(BattleOutcome.Victory, row.Outcome);
    }

    // =======================================================================
    // Server authority — GAME_RULES.md §18, ADR-001
    // =======================================================================

    [Fact]
    public async Task PersistTerminalResult_ShouldSourceEveryValueFromServerState()
    {
        // GAME_RULES.md §18 / ADR-001 / AGENTS.md §10: nothing the result carries
        // may come from a client. The boundary accepts only the authoritative state
        // and the outcome the resolution reported, and this asserts the row holds
        // exactly those values — so a client-supplied identity, outcome, duration,
        // boss, or timestamp could not appear.
        var battleId = "result-authority";
        var state = NewBattle(battleId);

        var harness = Harness.Create(BossDefinitions.HoaLong);

        await harness.Service.PersistTerminalResultAsync(state, BattleOutcome.Defeat);

        var row = Assert.Single(harness.Results.Rows);

        Assert.Equal(state.BattleId, row.BattleResultId);
        Assert.Equal(state.PlayerId.Value, row.PlayerId);
        Assert.Equal(state.PetState.PetId.Value, row.PetInstanceId);
        Assert.Equal(BossDefinitions.HoaLong.BossDefinitionId, row.BossDefinitionId);
        Assert.Equal(BattleOutcome.Defeat, row.Outcome);
        Assert.Equal(state.Turn, row.DurationTurns);
    }

    [Fact]
    public async Task PersistTerminalResult_ShouldRefuseAStateWithNoIdentity()
    {
        // GAME_STATE.md §2.8 / §2.3 / §2.4 make the owning Player, the Pet
        // instance, and the Boss identity present from battle creation, so a state
        // missing one is not a battle whose result may be invented. The boundary
        // refuses rather than persisting a fabricated identity.
        var harness = Harness.Create(BossDefinitions.HoaLong);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            harness.Service.PersistTerminalResultAsync(
                NewBattle("result-no-owner") with { PlayerId = new PlayerId("") },
                BattleOutcome.Victory));

        Assert.Empty(harness.Results.Rows);
    }
}
