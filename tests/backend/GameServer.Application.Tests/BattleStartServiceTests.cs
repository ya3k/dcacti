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
    [InlineData("boss-hoa-long")]
    [InlineData("boss-thuy-ma")]
    [InlineData("boss-moc-yeu")]
    public async Task Start_ShouldAcceptEveryContentDefinedMvpBoss(string bossId)
    {
        // BOSS_RULES.md §6 defines exactly these three.
        var harness = new Harness();

        var result = await harness.StartAsync(
            bossId: bossId,
            cardLoadout: [BasicA, BasicB, BasicC],
            relicLoadout: ["relic_1", "relic_2", "relic_3"]);

        Assert.True(result.Succeeded);
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
            Battles = new BattleStateService(ActiveStateStore, new FixedRngSeedSource());
            Pets = new FakePetRepository(this);
            Cards = new FakeCardRepository(this);
            Relics = new FakeRelicRepository(this);
        }

        public BattleStateService Battles { get; }

        /// <summary>
        /// The store the orchestrator's battles are persisted to, so a test can
        /// assert on the record itself rather than on a process-local copy
        /// (<c>REDIS_STATE.md</c> §2 item 2).
        /// </summary>
        internal InMemoryBattleStateRepository ActiveStateStore { get; }

        internal FakePetRepository Pets { get; }

        internal FakeCardRepository Cards { get; }

        internal FakeRelicRepository Relics { get; }

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
            new BattleStartService(Pets, new CardLoadoutService(Cards), new RelicLoadoutService(Relics), Battles)
                .StartAsync(playerId, request);

        /// <summary>The Pet definition the fake repository serves.</summary>
        internal PetDefinition BuildDefinition() => new()
        {
            PetDefinitionId = PetDefinitionId,
            Identity = "Thanh Xà",
            Element = Element.Moc,
            PetLevelMultiplier = 1.0m,
            PassiveId = new PassiveId("pet-passive-1"),
            PassiveThreshold = 5,
            SignatureSkillCardId = SignatureSkillCardId,
        };
    }

    /// <summary>
    /// The Pet persistence boundary backed by the harness's own flags. Only the
    /// two reads the battle-start path performs are implemented; a write is a
    /// defect and is reported as one.
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

        public Task<IReadOnlyList<Pet>> ListByPlayerIdAsync(
            string playerId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Battle start resolves the Pet by instance identity.");

        public Task<IReadOnlyList<Pet>> ListByDefinitionIdAsync(
            string petDefinitionId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Battle start never lists Pets by definition.");

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
                        Level = 1,
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

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Battle start never saves Pet changes.");
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
                            EffectDefinition = "pet-skill",
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
                    EffectDefinition = "effect",
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

        private static Relic NewRelic(string instanceId) => new()
        {
            RelicInstanceId = instanceId,
            PlayerId = Owner,
            RelicDefinitionId = "relic_def_1",
            AcquiredAt = DateTimeOffset.UtcNow,
        };
    }
}
