using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Domain.Relics;
using Xunit;

namespace GameServer.Application.Tests;

/// <summary>
/// <c>GAME_RULES.md</c> §17 step 11 — "Trigger Relics" — as the Application layer
/// runs it: the stage's position in the resolution's event sequence, the
/// content configuration it reads, the single post-resolution write-back that
/// carries its state, and the reports it hands to the realtime boundary.
///
/// <code>
/// Rule (GAME_RULES.md §17 step 11, RELIC_RULES.md §4/§7/§8, GAME_EVENTS.md §1.1)
///  ↓
/// Scenario (Given a battle with equipped Relics, When a Swap commits,
///           Then the eligible Relics resolved in slot order, their effects are
///           in the committed state, and the events sit where §1.1 places them)
///  ↓
/// Test
/// </code>
///
/// <b>The Relic content is scenario data, and it is declared as such.</b>
/// Driving every provisioned threshold from a generated board would make the
/// assertions depend on which Swap happened to produce enough Matches, so the
/// fixtures below pair the <c>§8.5</c> <b>effects</b> — whose magnitudes and
/// lifetimes are the documented ones — with thresholds the committed Swap is
/// guaranteed to satisfy. The <b>threshold semantics themselves</b>
/// (<c>MatchCountAtLeast</c>, <c>ComboAtLeast</c>, <c>HpPercentageBelow</c>) and
/// the §8.5 rows are asserted in the Domain suite, where the state is given
/// directly. One test below keeps a §8.5 threshold to prove it is read through
/// the real pipeline.
/// </summary>
public class RelicStageResolutionTests
{
    private static readonly PlayerId Owner = new("player_relic_stage");
    private static readonly BossDefinition BossDefinition = BossDefinitions.HoaLong;

    private const string PetInstanceId = "pet_instance_relic_stage";
    private const string Slot1 = "relic_slot_1";
    private const string Slot2 = "relic_slot_2";
    private const string Slot3 = "relic_slot_3";

    /// <summary>The battle id every scenario below creates and resolves.</summary>
    private const string BattleId = "battle-relic-stage";

    // =======================================================================
    // Resolution — trigger, condition, effect, carriers
    // =======================================================================

    [Fact]
    public async Task CommittedSwap_ShouldApplyEachEligibleRelicsEffect_ToItsDocumentedCarrier()
    {
        // RELIC_RULES.md §8.3's allowed combination per effectType, applied through
        // GAME_STATE.md §2.3.5 (CardCost), §2.3.7 (ATK), §2.3.4 (Crit), and the Power
        // write site (GAME_RULES.md §12, §17 steps 11–13).
        var definitions = new RelicDefinition?[]
        {
            Definition("relic-atk", "OnMatchCount", RelicConditionType.MatchCountAtLeast, 1, AtkEffect(5)),
            Definition("relic-power", "OnMatchCount", RelicConditionType.MatchCountAtLeast, 1, PowerEffect(10)),
            Definition("relic-cardcost", "OnMatchCount", RelicConditionType.MatchCountAtLeast, 1, CardCostEffect(50)),
        };

        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var state = await CreateBattleAsync(service, BattleId, definitions);
        var pair = FindAdjacentPairThatProducesAMatch(state.BoardState);

        var result = await service.ExecuteSwapAsync(BattleId, pair);

        Assert.NotNull(result);
        var committed = result!.Value.State;

        // ATK is a Battle-lifetime modifier keyed on the equipped instance identity.
        var atk = Assert.Single(committed.PetState.ATKModifiers);
        Assert.Equal(Slot1, atk.SourceIdentity);
        Assert.Equal(5, atk.ATKModifierPercentage);

        // COMBAT_RULES.md §5.6.4: the permanent base stat is untouched, and the
        // composed value is derived for the pipeline call only — never stored.
        Assert.Equal(PetState.DefaultATK, committed.PetState.ATK);

        // CardCost is a Battle-lifetime modifier on the same shape.
        var cardCost = Assert.Single(committed.PetState.CardCostModifiers);
        Assert.Equal(Slot3, cardCost.SourceIdentity);
        Assert.Equal(50, cardCost.CostReductionPercentage);

        // Power is Immediate: it was granted and left nothing standing. The
        // provisioned Mana Crystal is the documented case (RELIC_RULES.md §8.3 item 3),
        // and the grant is visible in the committed state at the value the write site's
        // own report carries.
        var powerChanged = Assert.Single(result!.Value.Events, e => e.Type == BattleEventType.PowerChanged);
        Assert.Equal(10, powerChanged.PowerChanged.Delta);
        Assert.Equal(committed.PetState.Power, powerChanged.PowerChanged.Power);
        Assert.True(committed.PetState.Power >= 10, "the granted Power is in the committed state");

        // The Crit modifier is NextAttack-lifetime, and the Swap's own player→boss
        // damage instance is the next qualifying owner attack
        // (COMBAT_RULES.md §3.3 item 8) — step 11 runs before steps 15–17, so it was
        // consumed in the same resolution (GAME_STATE.md §5.1.2 item 4).
        Assert.Empty(committed.PetState.NextAttackCritModifiers);
    }

    [Fact]
    public async Task CommittedSwap_ShouldEmitRelicTriggeredAndPowerChanged_InSlotOrder()
    {
        // RELIC_RULES.md §7 / SIGNALR_PROTOCOL.md §3.2.23: one RelicTriggered per
        // Relic whose effect applied, carrying the equipped instance identity and
        // nothing else, in the equip-slot order §4.2 fixes. §3.2.24: PowerChanged
        // reports the Power a Relic changed, with source "relic".
        var definitions = new RelicDefinition?[]
        {
            AtkRelic("relic-atk"),
            PowerRelic("relic-power"),
            CardCostRelic("relic-cardcost"),
        };

        var service = NewService();
        var state = await CreateBattleAsync(service, BattleId, definitions);
        var pair = FindAdjacentPairThatProducesAMatch(state.BoardState);

        var result = await service.ExecuteSwapAsync(BattleId, pair);
        var events = result!.Value.Events;

        var triggered = events.Where(e => e.Type == BattleEventType.RelicTriggered).ToArray();

        Assert.Equal(
            [Slot1, Slot2, Slot3],
            triggered.Select(e => e.RelicTriggered.RelicId).ToArray());

        var powerChanged = Assert.Single(events, e => e.Type == BattleEventType.PowerChanged);
        Assert.Equal(PowerChangeSource.Relic, powerChanged.PowerChanged.Source);
        Assert.Equal(10, powerChanged.PowerChanged.Delta);
        Assert.Equal(result.Value.State.PetState.Power, powerChanged.PowerChanged.Power);
    }

    [Fact]
    public async Task CommittedSwap_ShouldPlaceStepElevenBetweenThePassiveStageAndDamage()
    {
        // GAME_EVENTS.md §1.1 / GAME_RULES.md §17: step 10 (Charge Passive) precedes
        // step 11 (Trigger Relics), which precedes steps 15–17 (the player→boss damage
        // instance). The assembled batch is the stages' own output concatenated, so
        // the position in it is the position in the resolution order.
        var definitions = new RelicDefinition?[]
        {
            AtkRelic("relic-atk"),
            PowerRelic("relic-power"),
            CardCostRelic("relic-cardcost"),
        };

        var service = NewService();
        var state = await CreateBattleAsync(service, BattleId, definitions);
        var pair = FindAdjacentPairThatProducesAMatch(state.BoardState);

        var result = await service.ExecuteSwapAsync(BattleId, pair);
        var events = result!.Value.Events;

        // The Boss's own Passive uses the SAME shared PassiveCharged event with
        // source "boss" (BOSS_RULES.md §7) and fires at step 18a, after the damage — so
        // the comparison is scoped to the PET's charges, which are step 10's.
        var lastPetPassive = IndexOfLast(
            events,
            BattleEventType.PassiveCharged,
            e => e.PassiveCharged.Source == "pet");
        var firstRelic = IndexOfFirst(events, BattleEventType.RelicTriggered);
        var firstDamage = IndexOfFirst(events, BattleEventType.DamageCalculated);

        Assert.True(lastPetPassive >= 0, "the Swap charged the Pet Passive (step 10)");
        Assert.True(firstRelic > lastPetPassive, "step 11 follows step 10");
        Assert.True(firstDamage > firstRelic, "step 11 precedes the damage instance (steps 15–17)");
    }

    [Fact]
    public async Task CommittedSwap_ShouldNotApplyARelicWhoseConditionIsNotMet()
    {
        // RELIC_RULES.md §7: RelicTriggered is emitted each time a Relic's Effect
        // actually applies — a Trigger that fires but whose Condition fails applies
        // nothing and emits nothing. The §8.5 threshold is read through the real
        // pipeline here.
        var berserkerCore = Definition(
            "relic-berserker-core",
            "OnMatchCount",
            RelicConditionType.MatchCountAtLeast,
            100,
            AtkEffect(5));

        var service = NewService();
        var state = await CreateBattleAsync(service, BattleId, [berserkerCore, null, null]);
        var pair = FindAdjacentPairThatProducesAMatch(state.BoardState);

        var result = await service.ExecuteSwapAsync(BattleId, pair);
        var committed = result!.Value.State;

        Assert.True(committed.MatchCount < 100, "the committed Match count cannot reach the threshold");
        Assert.DoesNotContain(result.Value.Events, e => e.Type == BattleEventType.RelicTriggered);
        Assert.Empty(committed.PetState.ATKModifiers);
    }

    [Fact]
    public async Task CommittedSwap_ShouldResolveNoRelic_WhenNoContentIsAttached()
    {
        // A battle created without Relic content resolves no Relic and invents none
        // (AGENTS.md §7): step 11 is a no-op rather than a failure. This is the shape
        // a composition that never resolved definitions produces.
        var service = NewService();
        var state = await service.CreateBattleAsync(
            BattleId,
            Owner,
            Configuration([Slot1, Slot2, Slot3]),
            BossDefinition);

        var pair = FindAdjacentPairThatProducesAMatch(state.BoardState);
        var result = await service.ExecuteSwapAsync(BattleId, pair);

        Assert.DoesNotContain(result!.Value.Events, e => e.Type == BattleEventType.RelicTriggered);
        Assert.Empty(result.Value.State.PetState.ATKModifiers);
        Assert.Empty(result.Value.State.PetState.CardCostModifiers);
        Assert.Empty(result.Value.State.PetState.NextAttackCritModifiers);
    }

    [Fact]
    public async Task CommittedSwap_ShouldLeaveAnUnresolvedSlotsRelicInactive()
    {
        // A definition the content read did not resolve leaves its slot with no
        // declared Trigger, Condition, or Effect, so nothing can be evaluated for it —
        // while a sibling slot whose content did resolve still applies.
        var definitions = new RelicDefinition?[]
        {
            null,
            PowerRelic("relic-power"),
            null,
        };

        var service = NewService();
        var state = await CreateBattleAsync(service, BattleId, definitions);
        var pair = FindAdjacentPairThatProducesAMatch(state.BoardState);

        var result = await service.ExecuteSwapAsync(BattleId, pair);

        var triggered = Assert.Single(
            result!.Value.Events,
            e => e.Type == BattleEventType.RelicTriggered);

        Assert.Equal(Slot2, triggered.RelicTriggered.RelicId);
        Assert.Empty(result.Value.State.PetState.ATKModifiers);
    }

    // =======================================================================
    // Persistence — one write-back under the Sequence compare-and-set
    // =======================================================================

    [Fact]
    public async Task CommittedSwap_ShouldPersistRelicModifiersInTheOneDocumentedWriteBack()
    {
        // GAME_STATE.md §5.1 / REDIS_STATE.md §4 items 2 and 5: one action is one
        // write-back, guarded by the Sequence compare-and-set. The Relic stage adds no
        // persistence path — its state travels in the same committed record.
        var definitions = new RelicDefinition?[]
        {
            AtkRelic("relic-atk"),
            PowerRelic("relic-power"),
            CardCostRelic("relic-cardcost"),
        };

        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var state = await CreateBattleAsync(service, BattleId, definitions);

        var writesAfterCreation = repository.WriteCount;
        var pair = FindAdjacentPairThatProducesAMatch(state.BoardState);

        var result = await service.ExecuteSwapAsync(BattleId, pair);

        Assert.Equal(writesAfterCreation + 1, repository.WriteCount);

        var stored = await repository.GetAsync(BattleId);

        Assert.NotNull(stored);
        Assert.True(result!.Value.State.PetState.ATKModifiersEqual(stored!.PetState));
        Assert.True(result.Value.State.PetState.CardCostModifiersEqual(stored.PetState));
        Assert.True(result.Value.State.PetState.NextAttackCritModifiersEqual(stored.PetState));
        Assert.Single(stored.PetState.ATKModifiers);
    }

    [Fact]
    public async Task CommittedSwap_ShouldRetryAgainstFreshState_AndStillResolveStepEleven()
    {
        // REDIS_STATE.md §4 items 2–3, 6: a refused compare-and-set aborts and retries
        // against fresh state, and the retried resolution re-runs the same
        // deterministic computation — which includes step 11.
        var definitions = new RelicDefinition?[]
        {
            AtkRelic("relic-atk"),
            PowerRelic("relic-power"),
            CardCostRelic("relic-cardcost"),
        };

        var repository = new InMemoryBattleStateRepository { ForcedConflicts = 1 };
        var service = NewService(repository);
        var state = await CreateBattleAsync(service, BattleId, definitions);
        var pair = FindAdjacentPairThatProducesAMatch(state.BoardState);

        var result = await service.ExecuteSwapAsync(BattleId, pair);

        Assert.NotNull(result);
        Assert.Single(result!.Value.State.PetState.ATKModifiers);
        Assert.Contains(result.Value.Events, e => e.Type == BattleEventType.RelicTriggered);
    }

    // =======================================================================
    // Harness
    // =======================================================================

    private static BattleStateService NewService(InMemoryBattleStateRepository? repository = null) =>
        new(repository ?? new InMemoryBattleStateRepository(), new FixedRngSeedSource());

    /// <summary>The index of the first event of <paramref name="type"/>, or -1.</summary>
    private static int IndexOfFirst(IReadOnlyList<BattleEvent> events, BattleEventType type)
    {
        for (var index = 0; index < events.Count; index++)
        {
            if (events[index].Type == type)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>The index of the last event of <paramref name="type"/>, or -1.</summary>
    private static int IndexOfLast(IReadOnlyList<BattleEvent> events, BattleEventType type)
    {
        for (var index = events.Count - 1; index >= 0; index--)
        {
            if (events[index].Type == type)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// The index of the last event of <paramref name="type"/> that also satisfies
    /// <paramref name="predicate"/>, or -1 — the scoped lookup a shared event needs
    /// (<c>PassiveCharged</c> reports the Pet's and the Boss's charges alike,
    /// <c>BOSS_RULES.md</c> §7).
    /// </summary>
    private static int IndexOfLast(
        IReadOnlyList<BattleEvent> events,
        BattleEventType type,
        Func<BattleEvent, bool> predicate)
    {
        for (var index = events.Count - 1; index >= 0; index--)
        {
            if (events[index].Type == type && predicate(events[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static Task<BattleState> CreateBattleAsync(
        BattleStateService service,
        string battleId,
        IReadOnlyList<RelicDefinition?> definitions) =>
        service.CreateBattleAsync(
            battleId,
            Owner,
            Configuration([Slot1, Slot2, Slot3]),
            BossDefinition,
            equippedRelicDefinitions: definitions);

    private static BattleStateService.PetConfiguration Configuration(string[] slots) =>
        new(
            new PetId(PetInstanceId),
            Element.Hoa,
            new PassiveId("xich-lang"),
            PassiveThreshold: 5,
            EquippedRelics: [.. slots.Select(slot => new EquippedRelicIdentity(slot))]);

    private static RelicDefinition AtkRelic(string id) =>
        Definition(id, "OnMatchCount", RelicConditionType.MatchCountAtLeast, 1, AtkEffect(5));

    private static RelicDefinition PowerRelic(string id) =>
        Definition(id, "OnMatchCount", RelicConditionType.MatchCountAtLeast, 1, PowerEffect(10));

    private static RelicDefinition CardCostRelic(string id) =>
        Definition(id, "OnMatchCount", RelicConditionType.MatchCountAtLeast, 1, CardCostEffect(50));

    /// <summary>
    /// A Relic definition with the stated Trigger, Condition form, and threshold, and
    /// one effect using the <c>§8.3</c> combination for that effect type. The effect
    /// magnitudes and lifetimes below are <c>RELIC_RULES.md</c> §8.5's; the thresholds
    /// are scenario data (see the class notes).
    /// </summary>
    private static RelicDefinition Definition(
        string relicDefinitionId,
        string trigger,
        RelicConditionType conditionType,
        int threshold,
        RelicEffectDefinitions effects) => new()
        {
            RelicDefinitionId = relicDefinitionId,
            Name = "Fixture Relic",
            Trigger = trigger,
            Condition = RelicCondition.Create(conditionType, threshold),
            EffectDefinition = effects,
        };

    private static RelicEffectDefinitions AtkEffect(int value) =>
        RelicEffectDefinitions.Create(
            RelicEffectDefinition.Create(
                RelicEffectType.ATK,
                RelicEffectValueType.Percentage,
                value,
                RelicEffectTarget.Pet,
                RelicEffectLifetime.Battle));

    private static RelicEffectDefinitions PowerEffect(int value) =>
        RelicEffectDefinitions.Create(
            RelicEffectDefinition.Create(
                RelicEffectType.Power,
                RelicEffectValueType.Flat,
                value,
                RelicEffectTarget.Pet,
                RelicEffectLifetime.Immediate));

    private static RelicEffectDefinitions CardCostEffect(int value) =>
        RelicEffectDefinitions.Create(
            RelicEffectDefinition.Create(
                RelicEffectType.CardCost,
                RelicEffectValueType.Percentage,
                value,
                RelicEffectTarget.Pet,
                RelicEffectLifetime.Battle));

    private static RelicEffectDefinitions CritEffect(int value) =>
        RelicEffectDefinitions.Create(
            RelicEffectDefinition.Create(
                RelicEffectType.Crit,
                RelicEffectValueType.PercentagePoints,
                value,
                RelicEffectTarget.Pet,
                RelicEffectLifetime.NextAttack));

    /// <summary>
    /// A board where swapping (26, 34) — row 3 col 2 with row 4 col 2 — creates a
    /// 4-match of HP Gems across row 4 (columns 0–3), generating healing.
    /// </summary>
    private static BoardState HpMatch3Board()
    {
        var rows = new[]
        {
            "ADHPADHP",
            "DHPADHPA",
            "HPADHPAD",
            // The completing HP Gem at column 2; every other column breaks the row.
            "PAHHPADH",
            // Row 4: HP Gems at columns 0–1 and 3, the gap at 2, breakers beyond.
            "HHDPPDHP",
            "DHPADHPA",
            "HPADHPAD",
            "PADHPADH",
        };
        var cells = new GemType[BoardState.CellCount];
        for (var r = 0; r < BoardState.Rows; r++)
        {
            for (var c = 0; c < BoardState.Columns; c++)
            {
                cells[BoardState.ToIndex(r, c)] = rows[r][c] switch
                {
                    'A' => GemType.Atk,
                    'D' => GemType.Def,
                    'H' => GemType.Hp,
                    'P' => GemType.Power,
                    _ => GemType.Atk,
                };
            }
        }
        return BoardState.FromCells(cells);
    }

    /// <summary>
    /// A board where swapping (26, 34) — row 3 col 2 with row 4 col 2 — creates a
    /// match of POWER Gems, generating Power into the transient pool.
    /// </summary>
    private static BoardState PowerMatch3Board()
    {
        var rows = new[]
        {
            "ADHPADHP",
            "DHPADHPA",
            "HPADHPAD",
            // The completing POWER Gem at column 2
            "PAPPPADH",
            // Row 4: POWER Gems at columns 0–1 and 3, gap at 2
            "PPDHHDHP",
            "DHPADHPA",
            "HPADHPAD",
            "PADHPADH",
        };
        var cells = new GemType[BoardState.CellCount];
        for (var r = 0; r < BoardState.Rows; r++)
        {
            for (var c = 0; c < BoardState.Columns; c++)
            {
                cells[BoardState.ToIndex(r, c)] = rows[r][c] switch
                {
                    'A' => GemType.Atk,
                    'D' => GemType.Def,
                    'H' => GemType.Hp,
                    'P' => GemType.Power,
                    _ => GemType.Atk,
                };
            }
        }
        return BoardState.FromCells(cells);
    }

    // =======================================================================
    // TASK-142 Ordering Regression Tests (Steps 10–14)
    // =======================================================================

    [Fact]
    public async Task CommittedSwap_HpPercentageBelow_ShouldEvaluateAtStepEleven_BeforeStepFourteenHealing()
    {
        // GAME_RULES.md §17 / TASK-142: Step 11 ("Trigger Relics") evaluates before
        // Step 12 ("Generate Resources"), Step 13 ("Update Power"), and Step 14
        // ("Resolve Player Effects").
        //
        // Scenario required by TASK-142 / Section 17:
        //   Pet Max HP = 100
        //   Pet HP before Step 11 = 25
        //   Relic condition: HpPercentageBelow(30)
        //   Current Swap causes healing at Step 14
        //
        // Expected:
        //   Step 11: HP is 25/100 = 25% < 30% -> condition is TRUE, Relic triggers,
        //            effect applies (CardCost 50% reduction).
        //   Step 14: healing occurs afterwards, raising HP above 30%.
        //
        // If Step 14 had run before Step 11 (the prior defect), HP would already be
        // >= 85 > 30% when Step 11 evaluated, so the Relic would NOT have triggered.
        var relic = Definition(
            "relic-emergency-core",
            "OnMatchCount",
            RelicConditionType.HpPercentageBelow,
            30,
            CardCostEffect(50));

        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await CreateBattleAsync(service, BattleId, [relic, null, null]);

        // Place Pet at HP = 25, MaxHP = 100, DEF = 1000 (to isolate healing from boss attack),
        // and setup board with HP match
        var setupState = created with
        {
            PetState = created.PetState with { HP = 25, MaxHP = 100, DEF = 1000 },
            BossState = created.BossState with { ATK = 0 },
            BoardState = HpMatch3Board(),
        };
        await repository.TryUpdateAsync(setupState, setupState.Sequence);

        var swap = new SwapRequest(26, 34); // row 3 col 2 <-> row 4 col 2 completes HP match
        var result = await service.ExecuteSwapAsync(BattleId, swap);

        Assert.NotNull(result);
        var committed = result!.Value.State;

        // Step 11 verified: Relic condition was TRUE and triggered
        var triggered = Assert.Single(
            result.Value.Events,
            e => e.Type == BattleEventType.RelicTriggered);
        Assert.Equal(Slot1, triggered.RelicTriggered.RelicId);

        // Step 11 effect applied: CardCost reduction is in PetState.CardCostModifiers
        var modifier = Assert.Single(committed.PetState.CardCostModifiers);
        Assert.Equal(50, modifier.CostReductionPercentage);

        // Step 14 verified: healing occurred afterwards, HP is now healed above 30%
        Assert.True(committed.PetState.HP > 30, "healing at step 14 raised HP above 30%");
    }

    [Fact]
    public async Task CommittedSwap_BerserkerCore_ShouldApplyAtStepEleven_AffectCurrentSwapDamage_AndNotMutateBaseAtk()
    {
        // GAME_RULES.md §17 steps 11 → 15–17 / RELIC_RULES.md §8.5:
        // Base Pet ATK = 100
        // Berserker Core +5% triggered at Step 11
        // EffectivePetATK = 105, seen by current Swap's later damage calculation
        // Base PetState.ATK = 100 must remain unchanged.
        var berserkerCore = Definition(
            "relic-berserker-core",
            "OnMatchCount",
            RelicConditionType.MatchCountAtLeast,
            1,
            AtkEffect(5));

        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await CreateBattleAsync(service, BattleId, [berserkerCore, null, null]);

        var setupState = created with
        {
            PetState = created.PetState with { ATK = 100 },
        };
        await repository.TryUpdateAsync(setupState, setupState.Sequence);

        var pair = FindAdjacentPairThatProducesAMatch(setupState.BoardState);
        var result = await service.ExecuteSwapAsync(BattleId, pair);

        Assert.NotNull(result);
        var committed = result!.Value.State;

        // Base ATK must remain 100 — never mutated by Berserker Core
        Assert.Equal(100, committed.PetState.ATK);

        // Berserker Core applied at Step 11
        var modifier = Assert.Single(committed.PetState.ATKModifiers);
        Assert.Equal(5, modifier.ATKModifierPercentage);

        // EffectivePetATK = 105 (StatusEffectLifecycle.EffectiveAttack: 100 * (100 + 5) / 100 = 105)
        var effectiveAtk = StatusEffectLifecycle.EffectiveAttack(
            committed.PetState.ATK,
            committed.PetState.ATKModifiers,
            committed.PetState.ActiveStatusEffects);
        Assert.Equal(105, effectiveAtk);

        // Damage calculated at Step 15 saw the modifier (Player is the first DamageCalculated event)
        var damageEvent = result.Value.Events.First(e => e.Type == BattleEventType.DamageCalculated);
        Assert.True(damageEvent.DamageCalculated.FinalDamage > 0);
        Assert.Equal(105 + result.Value.Resources.BaseDamagePool, damageEvent.DamageCalculated.Base);
    }

    [Fact]
    public async Task CommittedSwap_AssassinEye_ShouldTriggerAtStepEleven_ContributeToCurrentSwap_AndBeConsumed()
    {
        // GAME_RULES.md §17 steps 11 → 15–17 / COMBAT_RULES.md §3.3 item 8:
        // Assassin Eye triggers at Step 11 -> modifier exists ->
        // current Swap attack reaches Damage Pipeline -> modifier contributes ->
        // modifier consumed exactly once in the same resolution.
        var assassinEye = Definition(
            "relic-assassin-eye",
            "OnMatchCount",
            RelicConditionType.MatchCountAtLeast,
            1,
            CritEffect(10));

        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await CreateBattleAsync(service, BattleId, [assassinEye, null, null]);

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = await service.ExecuteSwapAsync(BattleId, pair);

        Assert.NotNull(result);
        var committed = result!.Value.State;

        // Assassin Eye triggered at Step 11
        Assert.Contains(
            result.Value.Events,
            e => e.Type == BattleEventType.RelicTriggered && e.RelicTriggered.RelicId == Slot1);

        // The modifier was consumed by the current Swap's qualifying player attack (Step 15–17)
        Assert.Empty(committed.PetState.NextAttackCritModifiers);

        // Base Crit is unchanged (no DefaultCrit reset, no base mutation)
        Assert.Equal(PetState.DefaultCrit, committed.PetState.Crit);
    }

    [Fact]
    public async Task CommittedSwap_ManaCrystal_ShouldApplyAtStepEleven_AndComposeWithStepThirteenPower()
    {
        // GAME_RULES.md §17 steps 11 → 13:
        // Mana Crystal +10 applies at Step 11.
        // Step 12 generates Power from POWER Gem matches.
        // Step 13 applies generated Power onto post-Step-11 PetState.
        // Single write-back, single clamp site, no double application.
        var manaCrystal = Definition(
            "relic-mana-crystal",
            "OnMatchCount",
            RelicConditionType.MatchCountAtLeast,
            1,
            PowerEffect(10));

        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await CreateBattleAsync(service, BattleId, [manaCrystal, null, null]);

        var setupState = created with
        {
            PetState = created.PetState with { Power = 0 },
            BoardState = PowerMatch3Board(),
        };
        await repository.TryUpdateAsync(setupState, setupState.Sequence);

        var writesBefore = repository.WriteCount;
        var swap = new SwapRequest(26, 34); // matches POWER gems
        var result = await service.ExecuteSwapAsync(BattleId, swap);

        Assert.NotNull(result);
        var committed = result!.Value.State;

        // Exactly one write-back for this swap resolution
        Assert.Equal(writesBefore + 1, repository.WriteCount);

        // Mana Crystal emitted PowerChanged at Step 11
        var powerEvent = Assert.Single(
            result.Value.Events,
            e => e.Type == BattleEventType.PowerChanged && e.PowerChanged.Source == PowerChangeSource.Relic);
        Assert.Equal(10, powerEvent.PowerChanged.Delta);

        // Power gem match generated at least 30 Power at Step 12
        Assert.True(result.Value.Resources.Power >= 30);

        // Step 13 composed them: 10 (relic) + generated Power (gems)
        Assert.Equal(10 + result.Value.Resources.Power, committed.PetState.Power);
    }

    /// <summary>
    /// An adjacent pair of the board whose exchange produces a §3 Match — guaranteed
    /// to exist by <c>MATCH3_RULES.md</c> §1.4, and asserted found rather than assumed.
    /// </summary>
    private static SwapRequest FindAdjacentPairThatProducesAMatch(BoardState board)
    {
        for (var index = 0; index < BoardState.CellCount; index++)
        {
            var right = BoardState.ToColumn(index) + 1 < BoardState.Columns ? index + 1 : -1;
            var down = index + BoardState.Width < BoardState.CellCount ? index + BoardState.Width : -1;

            if (right >= 0 && MatchDetector.Detect(board.WithSwapped(index, right)).Count > 0)
            {
                return new SwapRequest(index, right);
            }

            if (down >= 0 && MatchDetector.Detect(board.WithSwapped(index, down)).Count > 0)
            {
                return new SwapRequest(index, down);
            }
        }

        throw new InvalidOperationException(
            "A generated board has at least one valid Swap (MATCH3_RULES.md §1.4).");
    }
}
