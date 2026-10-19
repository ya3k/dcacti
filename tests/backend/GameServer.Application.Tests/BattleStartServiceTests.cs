using GameServer.Application.Battle;
using GameServer.Application.Cards;
using GameServer.Application.Pets;
using GameServer.Application.Relics;
using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Domain.Relics;

namespace GameServer.Application.Tests;

/// <summary>
/// The <c>POST /api/battle/start</c> orchestration contract —
/// <c>API_CONTRACTS.md</c> §3 (TASK-030).
///
/// <code>
/// BattleStartRequest
///         ↓
/// resolve selected Pet + ownership
///         ↓
/// resolve selected Boss
///         ↓
/// CardLoadoutService        → EquippedCards[4]
///         ↓
/// RelicLoadoutService       → EquippedRelics[3–5]
///         ↓
/// PetConfiguration
///         ↓
/// BattleStateService.CreateBattle
/// </code>
///
/// These tests prove the orchestration <b>uses</b> the completed TASK-027/028
/// services rather than reimplementing them: the Card and Relic rules are
/// exercised through this boundary, and the resulting authoritative battle state
/// is asserted to carry both snapshots. The services' own unit suites remain the
/// exhaustive statement of their rules and are not duplicated here.
/// </summary>
public class BattleStartServiceTests
{
    private const string Owner = "player_1";
    private const string OtherPlayer = "player_2";
    private const string PetInstanceId = "pet_instance_1";
    private const string PetDefinitionId = "pet_def_1";
    private const string SignatureSkillCardId = "card_skill_1";
    private const string BasicA = "card_basic_a";
    private const string BasicB = "card_basic_b";
    private const string BasicC = "card_basic_c";

    /// <summary>
    /// The <c>RelicDefinitionId</c> every fixture's owned Relic instance
    /// references (<c>DATABASE.md</c> §1: <c>Relic.RelicDefinitionId</c>), so the
    /// battle-start path's definition resolution has a real reference to follow.
    /// </summary>
    private const string RelicDefinitionId = "relic_def_1";

    /// <summary>
    /// A valid MVP Boss canonical technical Identity (<c>BOSS_RULES.md</c> §6.4) —
    /// never the Boss's display name.
    /// </summary>
    private const string ValidBossId = "boss-hoa-long";

    // -----------------------------------------------------------------------
    // Success — API_CONTRACTS.md §3
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Start_WithThreeValidBasicCardsAndAValidRelicLoadout_ShouldCreateTheBattle()
    {
        var harness = new Harness();

        var result = await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        Assert.True(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.BattleId));

        // The battle genuinely exists in the authoritative store.
        var state = await harness.Battles.GetBattleAsync(result.BattleId!);
        Assert.NotNull(state);
        Assert.Equal(result.BattleId, state!.BattleId);
    }

    [Fact]
    public async Task Start_WithTheProvisionedEarthquakeBossCard_ShouldResolveTheMigratedForm()
    {
        // TASK-308 / OD-1c: battle start resolves the Boss active form from
        // provisioned-content presence. The Boss Card is read ONCE, by the ability
        // SkillId, and cached with the Boss content input.
        var harness = new Harness();
        harness.CardDefinitions.Definitions["earthquake"] = EarthquakeBossCard();

        var result = await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"],
            bossId: "boss-son-thach-ve");

        Assert.True(result.Succeeded);

        // Creation completed with the migrated form configured and the content was
        // resolved exactly once at creation, keyed by SkillId.
        Assert.Equal(["earthquake"], harness.CardDefinitions.Reads);
        Assert.Equal(1, harness.ActiveStateStore.RecordCount);
    }

    [Fact]
    public async Task Start_WithAProvisionedBossCardOfTheWrongCategory_ShouldFailClosed()
    {
        // Present-but-invalid content must fail the creation rather than falling
        // back to the legacy Skill form (TASK-308 §1, OD-1c; AGENTS.md §7).
        var harness = new Harness();
        harness.CardDefinitions.Definitions["earthquake"] =
            EarthquakeBossCard(category: CardCategory.Basic);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.StartAsync(
                cardLoadout: [BasicA, BasicB, BasicC],
                relicLoadout: ["relic_1", "relic_2", "relic_3"],
                bossId: "boss-son-thach-ve"));

        // A rejected creation writes no battle state.
        Assert.Equal(0, harness.ActiveStateStore.RecordCount);
    }

    [Fact]
    public async Task Start_WithAProvisionedBossCardButNoCastPriorityScore_ShouldFailClosed()
    {
        // The nullable score is an explicit "not authored" marker, legal only
        // while the Boss has no Boss Card (BOSS_RULES.md §4.3 item 5).
        var harness = new Harness();
        harness.CardDefinitions.Definitions["earthquake"] = EarthquakeBossCard();
        harness.BossLookup = ScriptedBossDefinitionLookup.Resolving(
            BossDefinitions.SonThachVe with { CastPriorityScore = null });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.StartAsync(
                cardLoadout: [BasicA, BasicB, BasicC],
                relicLoadout: ["relic_1", "relic_2", "relic_3"],
                bossId: "boss-son-thach-ve"));

        Assert.Equal(0, harness.ActiveStateStore.RecordCount);
    }

    [Fact]
    public async Task Start_ShouldSnapshotExactlyFourEquippedCards_IncludingTheDerivedSignatureSkill()
    {
        // CARD_RULES.md §1 / API_CONTRACTS.md §3: the snapshot is the 3 submitted
        // Basics plus the DERIVED Signature Skill — 4 entries, with the Skill
        // never submitted.
        var harness = new Harness();

        var result = await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        var petState = (await harness.Battles.GetBattleAsync(result.BattleId!))!.PetState;

        Assert.NotNull(petState.EquippedCards);
        Assert.Equal(4, petState.EquippedCards!.Length);

        // The three submitted Basics, in submitted order.
        Assert.Equal(BasicA, petState.EquippedCards[0].Value);
        Assert.Equal(BasicB, petState.EquippedCards[1].Value);
        Assert.Equal(BasicC, petState.EquippedCards[2].Value);

        // The derived fourth is the Pet definition's Signature Skill Card — read
        // through the Pet, never supplied by the caller.
        Assert.Equal(SignatureSkillCardId, petState.EquippedCards[3].Value);
    }

    [Fact]
    public async Task Start_ShouldSnapshotTheRequestedRelics_InSubmittedOrder()
    {
        // RELIC_RULES.md §2.3, §2.5: the request array order IS the equip-slot
        // order, so element i is slot i + 1 — never re-sorted. The submitted
        // order is deliberately not the sorted order, so a re-sorting
        // implementation cannot pass.
        string[] submitted = ["relic_z", "relic_a", "relic_m", "relic_b", "relic_c"];

        var harness = new Harness { OwnedRelics = submitted };

        var result = await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: submitted);

        var petState = (await harness.Battles.GetBattleAsync(result.BattleId!))!.PetState;

        Assert.NotNull(petState.EquippedRelics);
        Assert.Equal(submitted, petState.EquippedRelics!.Select(r => r.Value).ToArray());
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Start_ShouldAcceptEveryDocumentedRelicLoadoutSize(int relicCount)
    {
        // RELIC_RULES.md §2.1 item 1: 3–5 Relics. The Player owns every instance
        // the test submits, so the loadout size is the only variable.
        var relicLoadout = Enumerable.Range(1, relicCount).Select(i => $"relic_{i}").ToArray();

        var harness = new Harness { OwnedRelics = relicLoadout };

        var result = await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: relicLoadout);

        Assert.True(result.Succeeded);

        var petState = (await harness.Battles.GetBattleAsync(result.BattleId!))!.PetState;
        Assert.Equal(relicCount, petState.EquippedRelics!.Length);
    }

    [Fact]
    public async Task Start_ShouldComposeTheBoss_FromTheSubmittedBossId()
    {
        var harness = new Harness();

        var result = await harness.StartAsync(
            bossId: "boss-thuy-ma",
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        var state = (await harness.Battles.GetBattleAsync(result.BattleId!))!;

        Assert.Equal("boss-thuy-ma", state.BossState.BossId.Value);

        // GAME_STATE.md §2.4: created at full health in the Initial State.
        Assert.Equal(BossDefinitions.ThuyMa.MaxHP, state.BossState.HP);
        Assert.Equal(BossDefinitions.ThuyMa.MaxHP, state.BossState.MaxHP);
    }

    [Fact]
    public async Task Start_ShouldComposePetState_FromTheSelectedPetsDefinition()
    {
        var harness = new Harness();

        var result = await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        var petState = (await harness.Battles.GetBattleAsync(result.BattleId!))!.PetState;

        // GAME_STATE.md §2.3: the Element and Passive come from the Pet's
        // DEFINITION, and progress starts at 0 against its own Threshold.
        Assert.Equal(Element.Moc, petState.Element);
        Assert.Equal("pet-passive-1", petState.PassiveId.Value);
        Assert.Equal(5, petState.PassiveProgress.Threshold);
        Assert.Equal(0, petState.PassiveProgress.Current);

        // COMBAT_RULES.md §1.1: the documented MVP combat defaults at creation.
        Assert.Equal(1000, petState.HP);
        Assert.Equal(1000, petState.MaxHP);
        Assert.Equal(50, petState.ATK);
        Assert.Equal(25, petState.DEF);
        Assert.Equal(5, petState.Crit);
        Assert.Equal(0, petState.Power);
    }

    [Fact]
    public async Task Start_ShouldAuthorEveryBattleStateValue_ServerSide()
    {
        // GAME_STATE.md §2.0.2, §2.0.5.2 item 1 / GAME_RULES.md §18: creating a
        // battle resolves no action, so Turn and Sequence are 0, and the board,
        // seed, and RNG state are all server-produced.
        var harness = new Harness();

        var result = await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        var state = (await harness.Battles.GetBattleAsync(result.BattleId!))!;

        Assert.Equal(0, state.Turn);
        Assert.Equal(0, state.Sequence);
        Assert.Equal(0, state.Combo);
        Assert.Equal(0, state.MatchCount);
        Assert.Equal(64, state.BoardState.Cells.Count);

        // No Swap is committed at creation.
        Assert.Null(state.LastCommittedSwapPair);
    }

    // -----------------------------------------------------------------------
    // Pet ownership — API_CONTRACTS.md §3 (PET_NOT_OWNED)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Start_WhenThePetBelongsToAnotherPlayer_ShouldRejectWithPetNotOwned()
    {
        var harness = new Harness { PetOwner = OtherPlayer };

        var result = await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        Assert.Equal(BattleStartOutcome.PetNotOwned, result.Outcome);
        Assert.Null(result.BattleId);
    }

    [Fact]
    public async Task Start_WhenThePetDoesNotExist_ShouldRejectWithPetNotOwned()
    {
        var harness = new Harness { PetExists = false };

        var result = await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        Assert.Equal(BattleStartOutcome.PetNotOwned, result.Outcome);
    }

    [Fact]
    public async Task Start_WithNoPetId_ShouldRejectWithPetNotOwned()
    {
        var harness = new Harness();

        var result = await harness.StartAsync(
            petId: "",
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        Assert.Equal(BattleStartOutcome.PetNotOwned, result.Outcome);
    }

    [Fact]
    public async Task Start_WhenThePetsDefinitionDoesNotResolve_ShouldReject()
    {
        // A Pet whose definition is missing cannot supply the Element, Passive,
        // or Signature Skill — no value may be invented for them, so no battle
        // is created (AGENTS.md §7).
        var harness = new Harness { PetDefinitionExists = false };

        var result = await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        Assert.False(result.Succeeded);
    }

    // -----------------------------------------------------------------------
    // Boss resolution — API_CONTRACTS.md §3
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Start_WithAnUnknownBossId_ShouldRejectWithBossNotFound()
    {
        var harness = new Harness();

        var result = await harness.StartAsync(
            bossId: "Not A Real Boss",
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        Assert.Equal(BattleStartOutcome.BossNotFound, result.Outcome);
        Assert.Null(result.BattleId);
    }

    [Fact]
    public async Task Start_WithAnEmptyBossId_ShouldRejectWithBossNotFound()
    {
        var harness = new Harness();

        var result = await harness.StartAsync(
            bossId: "",
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        Assert.Equal(BattleStartOutcome.BossNotFound, result.Outcome);
    }

    [Theory]
    // BOSS_RULES.md §6 defines exactly five content-defined MVP Bosses; §6.4
    // fixes each canonical technical Identity, and the resolution compares
    // against BossId.Value ("fixed here so no task invents its own"). TASK-172
    // added the last two, so all five must resolve — a Boss the set contains may
    // never be rejected.
    [InlineData("boss-hoa-long")]
    [InlineData("boss-thuy-ma")]
    [InlineData("boss-moc-yeu")]
    [InlineData("boss-son-thach-ve")]
    [InlineData("boss-kim-loi-vuong")]
    public async Task Start_ShouldAcceptEveryContentDefinedMvpBoss(string bossId)
    {
        var harness = new Harness();

        var result = await harness.StartAsync(
            bossId: bossId,
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        Assert.True(result.Succeeded);
    }

    // -----------------------------------------------------------------------
    // Boss balance authority — DATABASE.md §1, ADR-024 §2.1–§2.4
    //
    // PostgreSQL is authoritative for the four runtime balance values at battle
    // creation, and the static Domain baseline is never a fallback for them. The
    // lookup's own overlay behavior is verified directly in
    // GameServer.Infrastructure.Tests (BossDefinitionBalanceLookupTests); these
    // tests verify the orchestration USES the resolved definition and does not
    // paper over a failure.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Start_ShouldCreateTheBattleFromTheOverlaidBalanceValues()
    {
        // ADR-024 §2.2–§2.3: the values the battle is created with are the
        // resolved definition's, not the static baseline's. The overlaid values
        // are deliberately different from BossDefinitions.HoaLong's canonical
        // 5000 / 100 / 50 / 0.30, so an orchestration that ignored the lookup and
        // read the static set would fail every assertion below.
        var overlaid = BossDefinitions.HoaLong with
        {
            MaxHP = 1234,
            ATK = 77,
            DEF = 12,
            EnrageThreshold = 0.42,
        };

        var harness = new Harness
        {
            BossLookup = ScriptedBossDefinitionLookup.Resolving(overlaid),
        };

        var result = await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        Assert.True(result.Succeeded);

        var bossState = (await harness.Battles.GetBattleAsync(result.BattleId!))!.BossState;

        // GAME_STATE.md §2.4 / BOSS_RULES.md §6.1: the Initial State carries the
        // definition's own stats, and a battle starts at full health.
        Assert.Equal(overlaid.BossId, bossState.BossId);
        Assert.Equal(1234, bossState.MaxHP);
        Assert.Equal(1234, bossState.HP);
        Assert.Equal(77, bossState.ATK);
        Assert.Equal(12, bossState.DEF);

        // The identity is the canonical one even though the balance came from the
        // row: the overlay replaces the four values and nothing else.
        Assert.Equal("boss-hoa-long", bossState.BossId.Value);

        // The canonical baseline was NOT used for any of the three stats that
        // reach BossState.
        Assert.NotEqual(BossDefinitions.HoaLong.MaxHP, bossState.MaxHP);
        Assert.NotEqual(BossDefinitions.HoaLong.ATK, bossState.ATK);
        Assert.NotEqual(BossDefinitions.HoaLong.DEF, bossState.DEF);
    }

    [Fact]
    public async Task Start_ShouldResolveTheBossDefinitionOncePerBattle()
    {
        // ADR-024 §2.3 / TDD.md §4 item 3: the definition is resolved at battle
        // creation and snapshotted, so the read is per battle — never repeated
        // per resolution.
        var lookup = ScriptedBossDefinitionLookup.ResolvingAll();
        var harness = new Harness { BossLookup = lookup };

        await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        Assert.Equal(1, lookup.DefinitionLookupCount);

        await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        Assert.Equal(2, lookup.DefinitionLookupCount);
    }

    [Fact]
    public void TurnResolutionPath_ShouldNotDependOnTheBossDefinitionLookup()
    {
        // ADR-024 §2.3: "Do not query PostgreSQL on the hot swap/turn-resolution
        // path." The service that owns ExecuteSwapAsync/CastCardAsync consumes the
        // battle-scoped configuration attached at creation, so it must hold no
        // dependency on the Boss definition read at all — a constructor parameter
        // or field of that type is exactly the shape this guards against.
        var constructorParameterTypes = typeof(BattleStateService)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        var fieldTypes = typeof(BattleStateService)
            .GetFields(
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public)
            .Select(field => field.FieldType)
            .ToArray();

        Assert.DoesNotContain(typeof(IBossDefinitionLookup), constructorParameterTypes);
        Assert.DoesNotContain(typeof(IBossDefinitionLookup), fieldTypes);
    }

    [Fact]
    public async Task Start_WhenTheProvisionedBalanceRowIsMissing_ShouldFailClosed()
    {
        // ADR-024 §2.1 item 3: a valid canonical MVP Boss whose provisioned row is
        // absent is a server-side provisioning failure. It must not be answered
        // from the static baseline and must not be reported as the client's
        // BOSS_NOT_FOUND.
        var harness = new Harness
        {
            // A lookup that knows the Identity is canonical but has no row for it.
            BossLookup = new ScriptedBossDefinitionLookup(),
        };

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.StartAsync(
                bossId: ValidBossId,
                cardLoadout: [BasicA, BasicB, BasicC],
                relicLoadout: ["relic_1", "relic_2", "relic_3"]));

        Assert.Contains(ValidBossId, thrown.Message, StringComparison.Ordinal);

        // §3: "A rejected request equips nothing and writes no battle state" — the
        // failure raised before creation, so no battle exists.
        Assert.Equal(0, harness.ActiveStateStore.RecordCount);
    }

    [Theory]
    // ADR-024 §2.4: each documented invariant, violated on its own.
    [InlineData("MaxHP = 0")]
    [InlineData("ATK = -1")]
    [InlineData("DEF = -1")]
    [InlineData("EnrageThreshold = 0")]
    [InlineData("EnrageThreshold = 1")]
    public async Task Start_WhenTheBalanceRowIsOutOfRange_ShouldFailClosed(string violation)
    {
        // ADR-024 §2.4: a row violating a documented range invariant fails closed
        // with an InvalidOperationException that is neither caught nor masked. The
        // Infrastructure lookup owns the validation and is verified directly in
        // GameServer.Infrastructure.Tests (BossDefinitionBalanceLookupTests); what
        // this asserts is that the orchestrator propagates that failure rather
        // than repairing, clamping, or defaulting the value — and that it never
        // turns it into the client's BOSS_NOT_FOUND.
        var harness = new Harness
        {
            BossLookup = new ThrowingBossDefinitionLookup(
                new InvalidOperationException(
                    $"The provisioned BossDefinition row carries an invalid balance value: {violation}.")),
        };

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.StartAsync(
                cardLoadout: [BasicA, BasicB, BasicC],
                relicLoadout: ["relic_1", "relic_2", "relic_3"]));

        Assert.Contains("invalid balance value", thrown.Message, StringComparison.Ordinal);

        // No partial battle: the failure raised before creation reached the store.
        Assert.Equal(0, harness.ActiveStateStore.RecordCount);
    }

    /// <summary>
    /// A stand-in for a PostgreSQL infrastructure failure — the
    /// <c>DbException</c> family (<c>NpgsqlException</c>) <c>ADR-024</c> §2.1
    /// item 2 names. It exists so the Application layer can prove the orchestrator
    /// neither catches nor translates such a failure.
    /// </summary>
    private sealed class SimulatedDatabaseFailure : System.Data.Common.DbException
    {
        public SimulatedDatabaseFailure(string message)
            : base(message)
        {
        }
    }

    [Fact]
    public async Task Start_WhenPostgresIsUnreachable_ShouldPropagateTheFailure()
    {
        // ADR-024 §2.1 item 2: a PostgreSQL connectivity failure is never caught,
        // masked, or translated into a client 400. It bubbles up so ASP.NET Core's
        // pipeline answers 500.
        var harness = new Harness
        {
            BossLookup = new ThrowingBossDefinitionLookup(
                new SimulatedDatabaseFailure("connection to server at \"localhost\" failed")),
        };

        var thrown = await Assert.ThrowsAsync<SimulatedDatabaseFailure>(() =>
            harness.StartAsync(
                cardLoadout: [BasicA, BasicB, BasicC],
                relicLoadout: ["relic_1", "relic_2", "relic_3"]));

        Assert.IsAssignableFrom<System.Data.Common.DbException>(thrown);

        // No battle was created for a request that failed server-side.
        Assert.Equal(0, harness.ActiveStateStore.RecordCount);
    }

    /// <summary>
    /// A lookup whose battle-start read always raises the supplied exception, so a
    /// test can assert an infrastructure failure is propagated with its own type
    /// intact (<c>ADR-024</c> §2.1 item 2 / item 3).
    /// </summary>
    private sealed class ThrowingBossDefinitionLookup : IBossDefinitionLookup
    {
        private readonly Exception _failure;

        internal ThrowingBossDefinitionLookup(Exception failure)
        {
            _failure = failure;
        }

        public Task<string?> FindBossDefinitionIdByIdentityAsync(
            string bossIdentity,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<BossDefinition?> GetDefinitionByIdentityAsync(
            string bossIdentity,
            CancellationToken cancellationToken = default) =>
            Task.FromException<BossDefinition?>(_failure);
    }

    [Theory]
    // The Boss's display name is presentation-only content (§6.4) and is never a
    // technical identifier in state, events, persistence, or the API — so it must
    // not be accepted where a bossId belongs.
    [InlineData("Hỏa Long")]
    [InlineData("Thủy Ma")]
    [InlineData("Mộc Yêu")]
    [InlineData("Sơn Thạch Vệ")]
    [InlineData("Kim Lôi Vương")]
    public async Task Start_WithADisplayName_ShouldRejectWithBossNotFound(string displayName)
    {
        var harness = new Harness();

        var result = await harness.StartAsync(
            bossId: displayName,
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        Assert.Equal(BattleStartOutcome.BossNotFound, result.Outcome);
    }

    [Fact]
    public async Task Start_ShouldResolveEveryDefinitionInTheMvpSet()
    {
        // The set the resolution reads is BossDefinitions.All, so every member
        // must resolve — this closes the gap an All/BossId mismatch would open.
        var harness = new Harness();

        foreach (var boss in BossDefinitions.All)
        {
            var result = await harness.StartAsync(
                bossId: boss.BossId.Value,
                cardLoadout: [BasicA, BasicB, BasicC],
                relicLoadout: ["relic_1", "relic_2", "relic_3"]);

            Assert.True(result.Succeeded);
        }
    }

    // -----------------------------------------------------------------------
    // Card validation integration — API_CONTRACTS.md §3 (INVALID_LOADOUT)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public async Task Start_WithAnInvalidCardCount_ShouldRejectWithInvalidLoadout(int count)
    {
        // CARD_RULES.md §1 / API_CONTRACTS.md §3 step 1: exactly 3.
        var harness = new Harness();

        var cardLoadout = Enumerable.Range(0, count)
            .Select(i => $"card_basic_{i}")
            .ToArray();

        var result = await harness.StartAsync(
            cardLoadout: cardLoadout,
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        Assert.Equal(BattleStartOutcome.InvalidLoadout, result.Outcome);
        Assert.Null(result.BattleId);
    }

    [Fact]
    public async Task Start_WhenACardIsNotUnlockedByThePlayer_ShouldRejectWithInvalidLoadout()
    {
        // API_CONTRACTS.md §3 step 2 (ownership).
        var harness = new Harness { UnlockedCards = [BasicA, BasicB] };

        var result = await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        Assert.Equal(BattleStartOutcome.InvalidLoadout, result.Outcome);
    }

    [Fact]
    public async Task Start_WhenACardIsNotBasicCategory_ShouldRejectWithInvalidLoadout()
    {
        // API_CONTRACTS.md §3 step 3 (category): a PetSkill Card can never
        // satisfy one of the three submitted Basic slots (CARD_RULES.md §1
        // item 4).
        var harness = new Harness { CategoryOfBasicC = CardCategory.PetSkill };

        var result = await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        Assert.Equal(BattleStartOutcome.InvalidLoadout, result.Outcome);
    }

    [Fact]
    public async Task Start_WhenTheCopyLimitIsExceeded_ShouldRejectWithInvalidLoadout()
    {
        // API_CONTRACTS.md §3 step 4 (copy limit): occurrence count must not
        // exceed the definition's LoadoutCopyLimit.
        var harness = new Harness { CopyLimitOfBasicA = 1 };

        var result = await harness.StartAsync(
            cardLoadout: [BasicA, BasicA, BasicB],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        Assert.Equal(BattleStartOutcome.InvalidLoadout, result.Outcome);
    }

    [Fact]
    public async Task Start_WithinTheCopyLimit_ShouldAcceptRepeatedBasicCards()
    {
        // CARD_RULES.md §1 item 1: a repeated definition is a legitimate entry
        // up to the definition's limit, and repeats are NOT de-duplicated.
        var harness = new Harness { CopyLimitOfBasicA = 2 };

        var result = await harness.StartAsync(
            cardLoadout: [BasicA, BasicA, BasicB],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        Assert.True(result.Succeeded);

        var petState = (await harness.Battles.GetBattleAsync(result.BattleId!))!.PetState;
        Assert.Equal([BasicA, BasicA, BasicB, SignatureSkillCardId],
            petState.EquippedCards!.Select(c => c.Value).ToArray());
    }

    [Fact]
    public async Task Start_WhenTheSignatureSkillCannotBeResolved_ShouldRejectWithInvalidLoadout()
    {
        // The derived fourth Card is unavailable, so the documented 4-card
        // composition cannot be produced (CARD_RULES.md §1).
        var harness = new Harness { SignatureSkillExists = false };

        var result = await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        Assert.Equal(BattleStartOutcome.InvalidLoadout, result.Outcome);
    }

    // -----------------------------------------------------------------------
    // Relic validation integration — API_CONTRACTS.md §3 (INVALID_LOADOUT)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(6)]
    public async Task Start_WithAnOutOfRangeRelicCount_ShouldRejectWithInvalidLoadout(int count)
    {
        // RELIC_RULES.md §2.1 item 1: 3–5.
        var harness = new Harness();

        var relicLoadout = Enumerable.Range(1, count).Select(i => $"relic_{i}").ToArray();

        var result = await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: relicLoadout);

        Assert.Equal(BattleStartOutcome.InvalidLoadout, result.Outcome);
    }

    [Fact]
    public async Task Start_WithADuplicateRelicInstance_ShouldRejectWithInvalidLoadout()
    {
        // RELIC_RULES.md §2.4 items 1–2: one instance occupies at most one slot.
        var harness = new Harness();

        var result = await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_1", "relic_2"]);

        Assert.Equal(BattleStartOutcome.InvalidLoadout, result.Outcome);
    }

    [Fact]
    public async Task Start_WithAnUnownedRelicInstance_ShouldRejectWithInvalidLoadout()
    {
        // RELIC_RULES.md §2.1 item 2 (ownership).
        var harness = new Harness { OwnedRelics = ["relic_1", "relic_2"] };

        var result = await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        Assert.Equal(BattleStartOutcome.InvalidLoadout, result.Outcome);
    }

    [Fact]
    public async Task Start_ShouldAcceptTwoDistinctInstancesOfTheSameRelicDefinition()
    {
        // RELIC_RULES.md §2.4 item 3: distinct instances of one definition MAY
        // be equipped together — the rule constrains instance identity.
        var harness = new Harness();

        var result = await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        Assert.True(result.Succeeded);
    }

    // -----------------------------------------------------------------------
    // Failure atomicity — no partial battle
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Start_OnEveryRejection_ShouldCreateNoBattleState()
    {
        // API_CONTRACTS.md §3: "A rejected request equips nothing and writes no
        // battle state." Every documented rejection is exercised here, and the
        // authoritative store must be untouched by all of them.
        var cases = new (string Name, Func<Harness> Build, Func<Harness, BattleStartRequest> Request)[]
        {
            ("unowned pet",
                () => new Harness { PetOwner = OtherPlayer },
                h => h.Request([BasicA, BasicB, BasicC], ["relic_1", "relic_2", "relic_3"])),
            ("unknown boss",
                () => new Harness(),
                h => h.Request([BasicA, BasicB, BasicC], ["relic_1", "relic_2", "relic_3"], bossId: "nope")),
            ("bad card count",
                () => new Harness(),
                h => h.Request([BasicA], ["relic_1", "relic_2", "relic_3"])),
            ("bad card ownership",
                () => new Harness { UnlockedCards = [BasicA, BasicB] },
                h => h.Request([BasicA, BasicB, BasicC], ["relic_1", "relic_2", "relic_3"])),
            ("bad card category",
                () => new Harness { CategoryOfBasicC = CardCategory.PetSkill },
                h => h.Request([BasicA, BasicB, BasicC], ["relic_1", "relic_2", "relic_3"])),
            ("copy limit exceeded",
                () => new Harness { CopyLimitOfBasicA = 1 },
                h => h.Request([BasicA, BasicA, BasicB], ["relic_1", "relic_2", "relic_3"])),
            ("bad relic count",
                () => new Harness(),
                h => h.Request([BasicA, BasicB, BasicC], ["relic_1"])),
            ("duplicate relic",
                () => new Harness(),
                h => h.Request([BasicA, BasicB, BasicC], ["relic_1", "relic_1", "relic_2"])),
            ("unowned relic",
                () => new Harness { OwnedRelics = ["relic_1", "relic_2"] },
                h => h.Request([BasicA, BasicB, BasicC], ["relic_1", "relic_2", "relic_3"])),
        };

        foreach (var (name, build, request) in cases)
        {
            var harness = build();

            var result = await harness.StartAsync(request(harness));

            Assert.False(result.Succeeded);
            Assert.Null(result.BattleId);

            // No record exists: no partial BattleState was stored
            // (REDIS_STATE.md §3 "Created" writes the record, so a rejected
            // request leaves the store empty).
            Assert.Equal(0, harness.ActiveStateStore.RecordCount);
        }
    }

    [Fact]
    public async Task Start_ShouldNotCreateABattle_UntilEveryValidationHasPassed()
    {
        // The atomicity property stated directly: an invalid RELIC loadout is
        // only detected after the Card loadout has already validated, and it
        // must still leave the store empty.
        var harness = new Harness { OwnedRelics = [] };

        var result = await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        Assert.Equal(BattleStartOutcome.InvalidLoadout, result.Outcome);
        Assert.Equal(0, harness.ActiveStateStore.RecordCount);
    }

    // -----------------------------------------------------------------------
    // Snapshot immutability — RELIC_RULES.md §2.5, CARD_RULES.md §1
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Start_ShouldProduceASnapshot_ThatDoesNotChangeWhenOwnershipChangesAfterwards()
    {
        // The loadouts are battle snapshots, not live inventory views: changing
        // the Player's unlocks and relics AFTER creation must not alter the
        // created battle's PetState (ADR-012 items 8 and 10).
        var harness = new Harness();

        var result = await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        var before = (await harness.Battles.GetBattleAsync(result.BattleId!))!.PetState;

        // Mutate the backing store: revoke unlocks, remove relics, change the
        // Signature Skill reference.
        harness.UnlockedCards = [];
        harness.OwnedRelics = [];
        harness.SignatureSkillCardId = "card_skill_replaced";

        var after = (await harness.Battles.GetBattleAsync(result.BattleId!))!.PetState;

        Assert.Equal(
            before.EquippedCards!.Select(c => c.Value).ToArray(),
            after.EquippedCards!.Select(c => c.Value).ToArray());

        Assert.Equal(
            before.EquippedRelics!.Select(r => r.Value).ToArray(),
            after.EquippedRelics!.Select(r => r.Value).ToArray());

        // And the snapshot is the documented composition, not the mutated store.
        Assert.Equal(SignatureSkillCardId, after.EquippedCards![3].Value);
    }

    [Fact]
    public async Task Start_ShouldNotWriteToTheCardOrRelicOwnershipStores()
    {
        // Both loadout services are read-only passes (API_CONTRACTS.md §3,
        // ADR-012 items 8 and 10): creation must not write an unlock, an equip
        // row, or a relic ownership row.
        var harness = new Harness();

        await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        Assert.Equal(0, harness.CardWrites);
        Assert.Equal(0, harness.RelicWrites);
    }

    [Fact]
    public async Task Start_ShouldCreateExactlyOneBattlePerRequest()
    {
        var harness = new Harness();

        await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        Assert.Equal(1, harness.ActiveStateStore.RecordCount);
    }

    // -----------------------------------------------------------------------
    // Identity carriage — GAME_STATE.md §2.8, §2.3, ADR-014
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Start_ShouldRecordTheRequestingPlayerAsTheBattleOwner()
    {
        // GAME_STATE.md §2.8 items 1–2 / ADR-014 decision 1: the battle's owner
        // identity is recorded at creation from the authenticated battle-start
        // context — the same `playerId` the orchestration was called with — and is
        // carried in the created state so the battle-end persistence path can source
        // BattleResult.PlayerId from it (DATABASE.md §1).
        var harness = new Harness();

        var result = await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        var state = await harness.Battles.GetBattleAsync(result.BattleId!);

        Assert.NotNull(state);
        Assert.Equal(new PlayerId(Owner), state!.PlayerId);
        Assert.Equal(Owner, state.PlayerId.Value);
    }

    [Fact]
    public async Task Start_ShouldRecordTheOwnedPetInstanceAsPetStatePetId()
    {
        // GAME_STATE.md §2.3 / ADR-014 decision 4: PetState.PetId denotes the owned
        // Pet INSTANCE — the same identity the request selected and whose ownership
        // the orchestration validated — and not the Pet's definition id. The two are
        // deliberately different strings here, so a member populated from the
        // definition could not pass.
        var harness = new Harness();

        var result = await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        var petState = (await harness.Battles.GetBattleAsync(result.BattleId!))!.PetState;

        Assert.Equal(new PetId(PetInstanceId), petState.PetId);
        Assert.Equal(PetInstanceId, petState.PetId.Value);
        Assert.NotEqual(PetDefinitionId, petState.PetId.Value);
    }

    [Fact]
    public async Task Start_ShouldRecordTheOwnerIdentityFromTheCaller_NotFromTheSubmittedPetId()
    {
        // GAME_RULES.md §18 / ADR-001 / AGENTS.md §10: the client supplies a
        // selection, never an identity. The request body carries no owner at all, so
        // the recorded owner can only have come from the server-side creation
        // context — and the Pet the request selected belongs to exactly that Player
        // (the ownership check the orchestration performed).
        var harness = new Harness { PetOwner = OtherPlayer };

        // A different Player submits the same Pet instance: ownership fails, so no
        // battle — and therefore no identity — is created for it.
        var rejected = await harness.StartAsync(
            playerId: Owner,
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        Assert.False(rejected.Succeeded);
        Assert.Equal(0, harness.ActiveStateStore.RecordCount);

        // The owning Player's own request creates the battle, and the recorded owner
        // is that Player — read from the creation context, not re-derived from the
        // Pet row it happens to own.
        var accepted = await harness.StartAsync(
            playerId: OtherPlayer,
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        Assert.True(accepted.Succeeded);

        var state = await harness.Battles.GetBattleAsync(accepted.BattleId!);

        Assert.Equal(new PlayerId(OtherPlayer), state!.PlayerId);
        Assert.Equal(new PetId(PetInstanceId), state.PetState.PetId);
    }

    [Fact]
    public async Task Start_ShouldCarryBothIdentitiesThroughTheRuntimeSerialization()
    {
        // GAME_STATE.md §2.8 item 2 / §2.3 / REDIS_STATE.md §2 item 1: both
        // identities are members of the state record and survive the runtime
        // serialize → deserialize cycle unchanged, which is the precondition the
        // active-state store's round trip relies on.
        var harness = new Harness();

        var result = await harness.StartAsync(
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        var state = (await harness.Battles.GetBattleAsync(result.BattleId!))!;

        var restored = GameServer.Domain.Battle.Serialization.BattleStateSerializer
            .Deserialize(GameServer.Domain.Battle.Serialization.BattleStateSerializer.Serialize(state));

        Assert.Equal(state.PlayerId, restored.PlayerId);
        Assert.Equal(Owner, restored.PlayerId.Value);
        Assert.Equal(state.PetState.PetId, restored.PetState.PetId);
        Assert.Equal(PetInstanceId, restored.PetState.PetId.Value);
    }

    // -----------------------------------------------------------------------
    // Server authority
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Start_ShouldRejectAMissingPlayerIdentity()
    {
        var harness = new Harness();

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            harness.StartAsync(
                playerId: "",
                cardLoadout: [BasicA, BasicB, BasicC],
                relicLoadout: ["relic_1", "relic_2", "relic_3"]));
    }

    // -----------------------------------------------------------------------
    // TASK-308 fixtures
    // -----------------------------------------------------------------------

    /// <summary>
    /// The provisioned <c>earthquake</c> Boss Card definition
    /// (TASK-308 §1; <c>CARD_RULES.md</c> §1.1): identity = the ability
    /// <c>SkillId</c>, <c>BossCard</c> category, <c>PowerCost 30</c>, one
    /// <c>Shield</c>/<c>Flat</c>/<c>150</c> effect, <c>LoadoutCopyLimit 1</c>.
    /// </summary>
    private static CardDefinition EarthquakeBossCard(
        CardCategory category = CardCategory.BossCard) =>
        new()
        {
            CardDefinitionId = "earthquake",
            Name = "Earthquake",
            Category = category,
            PowerCost = 30,
            EffectDefinition = CardEffectDefinitions.Create(
                CardEffectDefinition.Create(CardEffectType.Shield, CardEffectValueType.Flat, 150)),
            LoadoutCopyLimit = 1,
        };

    // -----------------------------------------------------------------------
    // Harness
    // -----------------------------------------------------------------------

    /// <summary>
    /// A battle-start orchestrator wired to in-memory persistence stand-ins and
    /// the real completed loadout services, so the orchestration is exercised
    /// end to end without a database.
    /// </summary>
    private sealed class Harness
    {
        public Harness()
        {
            // The active-state store is a test double: the real store is the
            // Redis repository (REDIS_STATE.md §1–§4), which needs a live Redis
            // instance and is verified against one in the Infrastructure and
            // smoke suites. This orchestrator test asserts the loadout
            // composition, so the substitution keeps the real BattleStartService
            // and the real BattleStateService pipeline while removing the
            // infrastructure dependency.
            ActiveStateStore = new InMemoryBattleStateRepository();

            // The Boss Card content read the creation path uses to resolve the
            // active form (TASK-308). Empty by default, so every existing
            // scenario keeps the transitional BossSkill form; a test seeds the
            // provisioned `earthquake` definition to exercise the migrated form.
            CardDefinitions = new FakeBossCardDefinitionLookup();

            Battles = new BattleStateService(
                ActiveStateStore,
                new FixedRngSeedSource(),
                cardDefinitions: CardDefinitions);
            Pets = new FakePetRepository(this);
            Cards = new FakeCardRepository(this);
            Relics = new FakeRelicRepository(this);
            RelicDefinitions = new FakeRelicDefinitionLookup(this);

            // Every canonical MVP Boss at its Domain baseline by default — what a
            // migrated database holds — so the orchestration tests resolve the
            // Boss they submit without a database. A test that exercises the
            // fail-closed or database-authoritative paths replaces this
            // (ADR-024 §2.1/§2.2).
            BossLookup = ScriptedBossDefinitionLookup.ResolvingAll();
        }

        public BattleStateService Battles { get; }

        /// <summary>
        /// The Boss Card content read (<c>CARD_RULES.md</c> §1.1;
        /// <c>BOSS_RULES.md</c> §4.2; <c>DATABASE.md</c> §1). Battle creation
        /// resolves the Boss's active form through it exactly once (TASK-308), so
        /// an empty map is the transitional form and a seeded
        /// <c>earthquake</c> row is the migrated form.
        /// </summary>
        internal FakeBossCardDefinitionLookup CardDefinitions { get; }

        /// <summary>
        /// The store the orchestrator's battles are persisted to, so a test can
        /// assert on the record itself rather than on a process-local copy
        /// (<c>REDIS_STATE.md</c> §2 item 2).
        /// </summary>
        internal InMemoryBattleStateRepository ActiveStateStore { get; }

        internal FakePetRepository Pets { get; }

        internal FakeCardRepository Cards { get; }

        internal FakeRelicRepository Relics { get; }

        /// <summary>
        /// The Relic definition content read (<c>RELIC_RULES.md</c> §8,
        /// <c>DATABASE.md</c> §1). The battle-start path resolves each equipped
        /// instance's definition through it, once per battle, so
        /// <c>GAME_RULES.md</c> §17 step 11 never queries PostgreSQL during a Swap
        /// (<c>TDD.md</c> §4 item 3).
        /// </summary>
        internal FakeRelicDefinitionLookup RelicDefinitions { get; }

        /// <summary>
        /// The Boss definition read (<c>DATABASE.md</c> §1, <c>ADR-024</c> §2.2) —
        /// the stand-in for the Domain Overlay lookup. A test replaces it (or
        /// empties its definition map) to drive the client <c>BOSS_NOT_FOUND</c>
        /// miss, the missing-row fail-closed path, and the
        /// database-authoritative balance values.
        /// </summary>
        internal IBossDefinitionLookup BossLookup { get; set; }

        /// <summary>Whether the Relic definition rows resolve.</summary>
        public bool RelicDefinitionsExist { get; set; } = true;

        /// <summary>The <c>RelicDefinitionId</c> values the content read was asked for.</summary>
        public List<string> RelicDefinitionReads { get; } = [];

        /// <summary>The owner of the selected Pet instance.</summary>
        public string PetOwner { get; set; } = Owner;

        /// <summary>Whether the selected Pet instance exists at all.</summary>
        public bool PetExists { get; set; } = true;

        /// <summary>Whether the Pet's definition row exists.</summary>
        public bool PetDefinitionExists { get; set; } = true;

        /// <summary>The Pet definition's Signature Skill reference.</summary>
        public string SignatureSkillCardId { get; set; } = BattleStartServiceTests.SignatureSkillCardId;

        /// <summary>Whether the Signature Skill definition resolves.</summary>
        public bool SignatureSkillExists { get; set; } = true;

        /// <summary>The Basic definitions the Player has unlocked.</summary>
        public string[] UnlockedCards { get; set; } = [BasicA, BasicB, BasicC];

        /// <summary>The Player's owned Relic instance identities.</summary>
        public string[] OwnedRelics { get; set; } = ["relic_1", "relic_2", "relic_3"];

        /// <summary>The Category of the <see cref="BasicC"/> definition.</summary>
        public CardCategory CategoryOfBasicC { get; set; } = CardCategory.Basic;

        /// <summary>The <c>LoadoutCopyLimit</c> of the <see cref="BasicA"/> definition.</summary>
        public int CopyLimitOfBasicA { get; set; } = 3;

        /// <summary>Counts writes attempted through the Card boundary.</summary>
        public int CardWrites { get; private set; }

        /// <summary>Counts writes attempted through the Relic boundary.</summary>
        public int RelicWrites { get; private set; }

        internal void RecordCardWrite() => CardWrites++;

        internal void RecordRelicWrite() => RelicWrites++;

        public BattleStartRequest Request(
            string[] cardLoadout,
            string[] relicLoadout,
            string petId = PetInstanceId,
            string bossId = ValidBossId) =>
            new(petId, bossId, cardLoadout, relicLoadout);

        public Task<BattleStartResult> StartAsync(
            string[] cardLoadout,
            string[] relicLoadout,
            string petId = PetInstanceId,
            string bossId = ValidBossId,
            string playerId = Owner) =>
            StartAsync(Request(cardLoadout, relicLoadout, petId, bossId), playerId);

        public Task<BattleStartResult> StartAsync(
            BattleStartRequest request,
            string playerId = Owner) =>
            new BattleStartService(
                Pets,
                new CardLoadoutService(Cards),
                new RelicLoadoutService(Relics),
                Battles,
                Relics,
                RelicDefinitions,
                BossLookup)
                .StartAsync(playerId, request);

        /// <summary>The Pet definition the fake repository serves.</summary>
        internal PetDefinition BuildDefinition() => new()
        {
            PetDefinitionId = PetDefinitionId,
            Identity = "Thanh Xà",
            Element = Element.Moc,
            PassiveId = new PassiveId("pet-passive-1"),
            PassiveThreshold = 5,
            SignatureSkillCardId = SignatureSkillCardId,
        };
    }

    /// <summary>
    /// The Pet persistence boundary backed by the harness's own flags. Only the
    /// reads the battle-start path performs are implemented; a write is a
    /// defect and is reported as one.
    ///
    /// <b>Both writes are defects here.</b> Pet creation is not a battle-start
    /// step, and Pet XP progression is granted only on the terminal battle-end
    /// path (<c>PET_RULES.md</c> §5.3) — never while starting a battle — so
    /// neither write may be reached from this boundary's caller.
    /// </summary>
    private sealed class FakePetRepository : IPetRepository
    {
        private readonly Harness _harness;

        internal FakePetRepository(Harness harness)
        {
            _harness = harness;
        }

        public Task AddAsync(Pet pet, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Battle start never writes Pet rows.");

        public Task<Pet?> GetByIdAsync(
            string petInstanceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                _harness.PetExists && petInstanceId == PetInstanceId
                    ? new Pet
                    {
                        PetInstanceId = PetInstanceId,
                        PlayerId = _harness.PetOwner,
                        PetDefinitionId = PetDefinitionId,
                        Tier = PetTier.Common,
                        Star = 1,
                        XP = Pet.InitialXp,
                        Level = Pet.InitialLevel,
                        AcquiredAt = DateTimeOffset.UtcNow,
                    }
                    : null);

        public Task<PetDefinition?> GetDefinitionAsync(
            string petDefinitionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                _harness.PetDefinitionExists && petDefinitionId == PetDefinitionId
                    ? _harness.BuildDefinition()
                    : null);

        /// <summary>
        /// The collection read (<c>API_CONTRACTS.md</c> §5.1) is not a
        /// battle-start step, so it is unimplemented here: reaching it from this
        /// boundary's caller is a defect, exactly as the two writes are.
        /// </summary>
        public Task<IReadOnlyList<Pet>> ListByPlayerIdAsync(
            string playerId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "Battle start never lists a collection; GET /api/pets is a read-only collection endpoint.");

        /// <summary>
        /// As <see cref="ListByPlayerIdAsync"/>: the battle-start path resolves
        /// the one selected definition, never a collection's worth of them.
        /// </summary>
        public Task<IReadOnlyList<PetDefinition>> ListDefinitionsAsync(
            IReadOnlyCollection<string> petDefinitionIds,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "Battle start resolves one selected definition; the bulk content read serves GET /api/pets.");

        public Task<bool> SaveProgressionAsync(
            Pet pet,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "Battle start never grants Pet XP; the reward is applied on the terminal battle-end path.");
    }

    /// <summary>
    /// The Card persistence boundary backed by the harness, so the real
    /// <see cref="CardLoadoutService"/> rules (count, ownership, category, copy
    /// limit, Signature Skill derivation) run against it unchanged.
    /// </summary>
    private sealed class FakeCardRepository : ICardRepository
    {
        private readonly Harness _harness;

        internal FakeCardRepository(Harness harness)
        {
            _harness = harness;
        }

        public Task AddDefinitionAsync(
            CardDefinition definition,
            CancellationToken cancellationToken = default)
        {
            _harness.RecordCardWrite();
            return Task.CompletedTask;
        }

        public Task AddUnlockAsync(
            PlayerUnlockedCard unlockedCard,
            CancellationToken cancellationToken = default)
        {
            _harness.RecordCardWrite();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CardDefinition>> ListUnlockedAsync(
            string playerId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CardDefinition>>(
                _harness.UnlockedCards.Select(DefinitionOf).ToList());

        public Task<IReadOnlyList<CardDefinition>> ListUnlockedDefinitionsAsync(
            string playerId,
            IReadOnlyCollection<string> cardDefinitionIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CardDefinition>>(
                _harness.UnlockedCards
                    .Where(cardDefinitionIds.Contains)
                    .Select(DefinitionOf)
                    .ToList());

        public Task<IReadOnlyList<CardDefinition>> ListDefinitionsAsync(
            IReadOnlyCollection<string> cardDefinitionIds,
            CancellationToken cancellationToken = default) =>
            // The unrestricted content read the collection projection uses for
            // each Pet's derived Signature Skill (API_CONTRACTS.md §5.1). The
            // loadout validator does not call it; it is implemented so this double
            // stays a faithful ICardRepository.
            Task.FromResult<IReadOnlyList<CardDefinition>>(
                cardDefinitionIds
                    .Select(DefinitionOfOrNull)
                    .Where(definition => definition is not null)
                    .Select(definition => definition!)
                    .ToList());

        public Task<CardDefinition?> GetDefinitionAsync(
            string cardDefinitionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CardDefinition?>(
                cardDefinitionId == _harness.SignatureSkillCardId
                    ? _harness.SignatureSkillExists
                        ? new CardDefinition
                        {
                            CardDefinitionId = cardDefinitionId,
                            Name = "Signature Skill",
                            Category = CardCategory.PetSkill,
                            PowerCost = 0,
                            EffectDefinition = TestCardEffects.FlatPower,
                            LoadoutCopyLimit = 1,
                        }
                        : null
                    : DefinitionOfOrNull(cardDefinitionId));

        private CardDefinition DefinitionOf(string cardDefinitionId) =>
            DefinitionOfOrNull(cardDefinitionId)
            ?? throw new InvalidOperationException($"No definition for '{cardDefinitionId}'.");

        private CardDefinition? DefinitionOfOrNull(string cardDefinitionId) =>
            cardDefinitionId is BasicA or BasicB or BasicC
                ? new CardDefinition
                {
                    CardDefinitionId = cardDefinitionId,
                    Name = cardDefinitionId,
                    // The category is Basic except for the deliberate
                    // non-Basic case, so the category step is exercised.
                    Category = cardDefinitionId == BasicC
                        ? _harness.CategoryOfBasicC
                        : CardCategory.Basic,
                    PowerCost = 0,
                    EffectDefinition = TestCardEffects.FlatPower,
                    LoadoutCopyLimit = cardDefinitionId == BasicA
                        ? _harness.CopyLimitOfBasicA
                        : 3,
                }
                : null;
    }

    /// <summary>
    /// The Relic persistence boundary backed by the harness, so the real
    /// <see cref="RelicLoadoutService"/> rules (count, distinctness, ownership,
    /// order) run against it unchanged. The returned rows are deliberately
    /// reverse-ordered, so a service that used the repository's order instead of
    /// the request order would fail the ordering assertions.
    /// </summary>
    private sealed class FakeRelicRepository : IRelicRepository
    {
        private readonly Harness _harness;

        internal FakeRelicRepository(Harness harness)
        {
            _harness = harness;
        }

        public Task AddAsync(Relic relic, CancellationToken cancellationToken = default)
        {
            _harness.RecordRelicWrite();
            return Task.CompletedTask;
        }

        public Task AddDefinitionAsync(
            RelicDefinition definition,
            CancellationToken cancellationToken = default)
        {
            _harness.RecordRelicWrite();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Relic>> ListByPlayerIdAsync(
            string playerId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Relic>>(
                _harness.OwnedRelics.Select(NewRelic).ToList());

        public Task<IReadOnlyList<Relic>> ListOwnedInstancesAsync(
            string playerId,
            IReadOnlyCollection<string> relicInstanceIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Relic>>(
                _harness.OwnedRelics
                    .Where(relicInstanceIds.Contains)
                    .OrderByDescending(id => id, StringComparer.Ordinal)
                    .Select(NewRelic)
                    .ToList());

        public Task<RelicDefinition?> GetDefinitionAsync(
            string relicDefinitionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<RelicDefinition?>(null);

        /// <summary>
        /// As <see cref="GetDefinitionAsync"/>: this boundary resolves no Relic
        /// definition through the repository — the battle-start path reads content
        /// through <see cref="IRelicDefinitionLookup"/>. The bulk read serves
        /// <c>GET /api/relics</c> (<c>API_CONTRACTS.md</c> §5.4) rather than this
        /// boundary's caller.
        /// </summary>
        public Task<IReadOnlyList<RelicDefinition>> ListDefinitionsAsync(
            IReadOnlyCollection<string> relicDefinitionIds,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "Battle start reads no Relic definition content from this boundary; the content read is IRelicDefinitionLookup's.");

        private static Relic NewRelic(string instanceId) => new()
        {
            RelicInstanceId = instanceId,
            PlayerId = Owner,
            RelicDefinitionId = RelicDefinitionId,
            AcquiredAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// The Relic definition content boundary (<c>DATABASE.md</c> §1), backed by the
    /// harness's own flags.
    ///
    /// <b>The battle-start path reads it once per equipped slot.</b> Every equipped
    /// instance references <see cref="RelicDefinitionId"/>, so a valid loadout
    /// produces that many reads — and the returned definition is a well-formed
    /// structured value (<c>RELIC_RULES.md</c> §8.1/§8.2), because a definition the
    /// contract's own reader would reject could not stand in for provisioned
    /// content.
    /// </summary>
    private sealed class FakeRelicDefinitionLookup : IRelicDefinitionLookup
    {
        private readonly Harness _harness;

        internal FakeRelicDefinitionLookup(Harness harness)
        {
            _harness = harness;
        }

        public Task<RelicDefinition?> GetDefinitionAsync(
            string relicDefinitionId,
            CancellationToken cancellationToken = default)
        {
            _harness.RelicDefinitionReads.Add(relicDefinitionId);

            return Task.FromResult<RelicDefinition?>(
                _harness.RelicDefinitionsExist ? NewDefinition(relicDefinitionId) : null);
        }

        private static RelicDefinition NewDefinition(string relicDefinitionId) => new()
        {
            RelicDefinitionId = relicDefinitionId,
            Name = "Test Relic",
            Trigger = "OnMatchCount",
            Condition = TestRelicEffects.Condition,
            EffectDefinition = TestRelicEffects.Effect,
        };
    }

    /// <summary>
    /// The Boss Card content read the creation path uses to resolve a Boss
    /// ability's active form (TASK-308; <c>CARD_RULES.md</c> §1.1,
    /// <c>BOSS_RULES.md</c> §4.2). It records every requested identity so a test
    /// can assert the creation-time resolution happened exactly once and was
    /// keyed by the ability <c>SkillId</c>. An empty map is the transitional
    /// form; a seeded entry is the migrated form.
    /// </summary>
    private sealed class FakeBossCardDefinitionLookup : ICardDefinitionLookup
    {
        internal Dictionary<string, CardDefinition> Definitions { get; } = new(StringComparer.Ordinal);

        internal List<string> Reads { get; } = [];

        public Task<CardDefinition?> GetDefinitionAsync(
            string cardDefinitionId,
            CancellationToken cancellationToken = default)
        {
            Reads.Add(cardDefinitionId);

            Definitions.TryGetValue(cardDefinitionId, out var definition);

            return Task.FromResult(definition);
        }
    }
}
