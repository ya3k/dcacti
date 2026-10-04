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
/// TASK-177's approved MVP Relic runtime contracts as the Application layer runs
/// them: the four firing points the six new Relics declare beyond
/// <c>GAME_RULES.md</c> §17 step 11, the <c>BurnDamage</c> effect reaching the
/// Burn tick, the <c>NextAttack</c> ATK lifetime reaching — and being consumed by —
/// the qualifying attack, and the recursion/ownership boundaries
/// <c>RELIC_RULES.md</c> §3.1, §3.2, §5, §6 note 1, and §8.5 define.
///
/// <code>
/// Rule (RELIC_RULES.md §3, §3.1, §3.2, §5, §6, §8.5; GAME_RULES.md §17;
///       COMBAT_RULES.md §3.3, §5.2 item 4; GAME_STATE.md §2.3.7, §5.1.4)
///  ↓
/// Scenario (Given a battle whose committed Swap resolves at a firing point,
///           When the swap commits, Then the documented carrier holds the
///           documented value, the documented events are emitted where §17
///           places them, and nothing recurses)
///  ↓
/// Test
/// </code>
///
/// <b>The six new Relics are content-defined, so they are declared here as
/// data.</b> TASK-177 provisions no database row (its database boundary), so the
/// definitions below are the §8.5 rows built in memory through the same
/// <c>RelicDefinition</c> the resolver reads — exactly the pattern the existing
/// Relic suites use. Nothing here recognises a Relic by name or id.
/// </summary>
public class Task177RelicFiringPointRuntimeTests
{
    private static readonly PlayerId Owner = new("player_task177");
    private static readonly BossDefinition Boss = BossDefinitions.HoaLong;

    private const string PetInstanceId = "pet_instance_task177";
    private const string Slot1 = "relic_task177_slot_1";
    private const string Slot2 = "relic_task177_slot_2";
    private const string Slot3 = "relic_task177_slot_3";
    private const string Slot4 = "relic_task177_slot_4";

    // =======================================================================
    // OnBattleStart — Burning Curse (AC-1)
    // =======================================================================

    [Fact]
    public async Task CreateBattle_BurningCurse_ShouldApplyItsStandingModifierAtBattleStart()
    {
        // RELIC_RULES.md §3: OnBattleStart "fires once, at battle start"; §6 note 1
        // and §8.5 item 5: a standing +30% BurnDamage modifier with Battle lifetime.
        // The battle-start firing point runs at creation, so the modifier is part of
        // the created record rather than a value a later action looks for.
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await CreateBattleAsync(
            service,
            "battle-burning-curse-start",
            [BurningCurse(), null, null]);

        var applied = Assert.Single(created.PetState.BurnDamageModifiers);
        Assert.Equal(Slot1, applied.SourceIdentity);
        Assert.Equal(30, applied.BurnDamagePercentage);

        // The created state is the stored record — one write, and the standing
        // modifier is in it from its first moment (REDIS_STATE.md §3).
        var stored = await repository.GetAsync("battle-burning-curse-start");
        Assert.NotNull(stored);
        Assert.True(created.PetState.BurnDamageModifiersEqual(stored!.PetState));
    }

    [Fact]
    public async Task CommittedSwap_ShouldLeaveBurningCursesOneModifierUntouched_AndNotReFireIt()
    {
        // §8.5 item 5 / §6 note 1: non-stacking, once per battle. The battle-start
        // Trigger is not the committed Swap's event (§3), so step 11 neither
        // re-applies nor re-evaluates it — and the one standing element survives
        // every later action.
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await CreateBattleAsync(
            service,
            "battle-burning-curse-standing",
            [BurningCurse(), null, null]);

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = await service.ExecuteSwapAsync("battle-burning-curse-standing", pair);

        Assert.NotNull(result);
        var committed = result!.Value.State;

        var applied = Assert.Single(committed.PetState.BurnDamageModifiers);
        Assert.Equal(30, applied.BurnDamagePercentage);

        Assert.DoesNotContain(
            result.Value.Events,
            e => e.Type == BattleEventType.RelicTriggered && e.RelicTriggered.RelicId == Slot1);

        // §8.3 item 4: Battle lifetime is "a standing modification for the remainder
        // of the battle", so a later Swap leaves the same one element in place.
        var stored = await repository.GetAsync("battle-burning-curse-standing");
        Assert.NotNull(stored);

        var second = await service.ExecuteSwapAsync(
            "battle-burning-curse-standing",
            FindMatchProducingPair(stored!));

        Assert.NotNull(second);
        Assert.True(second!.Value.IsAccepted);

        var afterTwoSwaps = Assert.Single(second.Value.State.PetState.BurnDamageModifiers);
        Assert.Equal(Slot1, afterTwoSwaps.SourceIdentity);
        Assert.Equal(30, afterTwoSwaps.BurnDamagePercentage);
    }

    [Fact]
    public async Task BurnTicks_ShouldApplyBurningCurseToPetOwnedBurnOnly()
    {
        // TASK-178 Product Owner decision Q-4 = C, as COMBAT_RULES.md §5.2 item 4 and
        // RELIC_RULES.md §6 note 1 own it: the modifier reaches Burn the PET
        // applied/owns and not Burn the Boss applied/owns — the distinction is the
        // instance's source, never the entity receiving the tick's damage.
        //
        // Two battles with identical states, one with the Relic and one without, so
        // the tick damage itself is compared rather than asserted from arithmetic.
        var withCurse = await RunBurnTickBattleAsync("battle-burn-with-curse", withBurningCurse: true);
        var withoutCurse = await RunBurnTickBattleAsync("battle-burn-without-curse", withBurningCurse: false);

        // The last two DamageCalculated instances are step 19a's two Burn ticks —
        // the Boss's own dots first, then the Pet's (GAME_RULES.md §17 step 19a).
        // Each carries Base = the instance's own magnitude (50), which is what
        // identifies it as a DoT tick rather than an attack
        // (COMBAT_RULES.md §5.2 item 3).
        var petOwnedTick = withCurse.Calculations[^2];
        var bossOwnedTick = withCurse.Calculations[^1];

        Assert.Equal(50, petOwnedTick.Base);
        Assert.Equal(50, bossOwnedTick.Base);

        // Pet-owned Burn (the Pet applied it, so it ticks on the Boss): the Relic's
        // +30% is applied as step 4's factor, and no Crit contribution is composed
        // here because the scenario's Pet Crit is 0 — so the factor is exactly 1.3.
        Assert.Equal(1.30, petOwnedTick.OtherModifiers, precision: 9);

        // Boss-owned Burn (the Boss applied it, so it ticks on the Pet): unmodified,
        // even though the Pet is the entity taking this damage.
        Assert.Equal(DamagePipeline.NoOtherModifiers, bossOwnedTick.OtherModifiers, precision: 9);

        // The modifier measurably changes the tick it reaches...
        Assert.True(
            petOwnedTick.FinalDamage > withoutCurse.Calculations[^2].FinalDamage,
            "the Pet-owned Burn tick is larger with Burning Curse than without it");

        // ...and changes nothing at all for the Boss-owned tick, which resolves to
        // the same damage in both battles.
        Assert.Equal(withoutCurse.Calculations[^1].FinalDamage, bossOwnedTick.FinalDamage);

        // COMBAT_RULES.md §5.2 item 4: the modifier changes damage only. Each side
        // still holds exactly its one Burn instance, one Turn of its duration was
        // consumed by step 19a, and no Burn event or second instance was created.
        var petBurn = Assert.Single(withCurse.State.BossState.ActiveStatusEffects, e => e.Id == "Burn");
        Assert.Equal(StatusEffectSource.Player, petBurn.Source);
        Assert.Equal(4, petBurn.RemainingTurns);

        var bossBurn = Assert.Single(withCurse.State.PetState.ActiveStatusEffects, e => e.Id == "Burn");
        Assert.Equal(StatusEffectSource.Boss, bossBurn.Source);
        Assert.Equal(4, bossBurn.RemainingTurns);
    }

    // =======================================================================
    // OnPowerGain — Arcane Battery (AC-3, AC-9)
    // =======================================================================

    [Fact]
    public async Task CommittedSwap_ArcaneBattery_ShouldResolveOncePerQualifyingPowerGain()
    {
        // RELIC_RULES.md §3.1 / §8.5 item 7: OnPowerGain, no Condition, Power +5,
        // Flat, Immediate. Step 12's generated Power is written at step 13 and is
        // the qualifying gain this Trigger observes.
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var definitions = new RelicDefinition?[]
        {
            ArcaneBattery(),
            ManaCrystal(),
            null,
        };

        var created = await PrepareAsync(
            service,
            repository,
            "battle-arcane-battery",
            definitions,
            PowerGainMatchBoard(),
            pet => pet with { Power = 0, Crit = 0 });

        var pair = SwapRequestForPowerBoard();
        var result = await service.ExecuteSwapAsync("battle-arcane-battery", pair);

        Assert.NotNull(result);
        Assert.True(result!.Value.IsAccepted);

        var committed = result.Value.State;

        // One activation from the Match's Power gain — and exactly one, so the +5
        // Arcane Battery itself granted did not become a second qualifying gain.
        // Mana Crystal resolves first because step 11 precedes step 13
        // (GAME_RULES.md §17), and Arcane Battery resolves at the gain.
        var triggered = result.Value.Events
            .Where(e => e.Type == BattleEventType.RelicTriggered)
            .Select(e => e.RelicTriggered.RelicId)
            .ToArray();

        Assert.Equal([Slot2, Slot1], triggered);

        var relicPowerChanges = result.Value.Events
            .Where(e => e.Type == BattleEventType.PowerChanged
                && e.PowerChanged.Source == PowerChangeSource.Relic)
            .Select(e => e.PowerChanged.Delta)
            .ToArray();

        // Mana Crystal's +10 (Relic-generated, so not a qualifying gain) and Arcane
        // Battery's +5 (the Arcane Battery activation), in resolution order.
        Assert.Equal([10, 5], relicPowerChanges);

        Assert.Single(result.Value.Events, e => e.Type == BattleEventType.PowerChanged
            && e.PowerChanged.Source == PowerChangeSource.Match);

        // §8.3 item 3: Immediate leaves no standing modification — the grants are on
        // PetState.Power and on the reports, and the Match's own generation is
        // composed into the same single write-back.
        Assert.Equal(
            Math.Min(Math.Min(10 + result.Value.Resources.Power, ResourceGenerator.MaxPower) + 5,
                ResourceGenerator.MaxPower),
            committed.PetState.Power);
    }

    [Fact]
    public async Task CommittedSwap_ArcaneBattery_ShouldNotResolve_WhenNoQualifyingGainOccurs()
    {
        // RELIC_RULES.md §3: the Trigger "fires when the active Pet's Power
        // INCREASES from a qualifying non-Relic-generated Power gain". A Swap whose
        // generation the 0–100 range wholly absorbs (GAME_RULES.md §12) leaves Power
        // unchanged, so there is no increase and no qualifying event — the Swap does
        // generate Power, which is what makes this the boundary case rather than a
        // board with no POWER Gems.
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        await PrepareAsync(
            service,
            repository,
            "battle-arcane-battery-no-gain",
            [ArcaneBattery(), null, null],
            PowerGainMatchBoard(),
            pet => pet with { Power = ResourceGenerator.MaxPower, Crit = 0 });

        var result = await service.ExecuteSwapAsync(
            "battle-arcane-battery-no-gain",
            SwapRequestForPowerBoard());

        Assert.NotNull(result);
        Assert.True(result!.Value.IsAccepted);

        // The Swap did generate Power; the cap absorbed all of it.
        Assert.True(result.Value.Resources.Power > 0, "the fixture Swap generates Power");
        Assert.Equal(ResourceGenerator.MaxPower, result.Value.State.PetState.Power);
        Assert.DoesNotContain(
            result.Value.Events,
            e => e.Type == BattleEventType.PowerChanged
                && e.PowerChanged.Source == PowerChangeSource.Match);
        Assert.DoesNotContain(
            result.Value.Events,
            e => e.Type == BattleEventType.RelicTriggered);
    }

    // =======================================================================
    // OnCascade — Cascade Core (AC-5, AC-9)
    // =======================================================================

    [Fact]
    public async Task CommittedSwap_CascadeCore_ShouldResolveOncePerCascadeIteration()
    {
        // RELIC_RULES.md §3.2 items 2–3 / §8.5 item 9: each actual cascade iteration
        // is an independent OnCascade event, so Cascade Core resolves once per
        // iteration — Cascade #1 → +5, Cascade #2 → +5, … — and the cascades of one
        // Swap are never collapsed into one application.
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await PrepareAsync(
            service,
            repository,
            "battle-cascade-core",
            [CascadeCore(), null, null],
            CascadeBoard(),
            pet => pet with { Power = 0, Crit = 0 });

        var result = await service.ExecuteSwapAsync("battle-cascade-core", CascadeSwap());

        Assert.NotNull(result);
        Assert.True(result!.Value.IsAccepted);

        // The Swap's own resolution report is the authoritative iteration boundary:
        // its passes are the passes that detected a Match, in depth order, and
        // MATCH3_RULES.md §4.2 item 1 makes depth 1 not a Cascade — so the cascades
        // are passes 2..n.
        var cascadeIterations = result.Value.Resolution.Passes.Count - 1;

        Assert.True(cascadeIterations >= 2, "the fixture Swap produces a multi-step Cascade");

        var triggered = result.Value.Events
            .Where(e => e.Type == BattleEventType.RelicTriggered)
            .Select(e => e.RelicTriggered.RelicId)
            .ToArray();

        // Exactly one activation per iteration: neither collapsed into one, nor
        // synthesized again by the Power the Relic granted.
        Assert.Equal(cascadeIterations, triggered.Length);
        Assert.All(triggered, relicId => Assert.Equal(Slot1, relicId));

        var relicPowerChanges = result.Value.Events
            .Where(e => e.Type == BattleEventType.PowerChanged
                && e.PowerChanged.Source == PowerChangeSource.Relic)
            .Select(e => e.PowerChanged.Delta)
            .ToArray();

        Assert.Equal(cascadeIterations, relicPowerChanges.Length);
        Assert.All(relicPowerChanges, delta => Assert.Equal(5, delta));

        Assert.Equal(
            Math.Min(result.Value.Resources.Power + (5 * cascadeIterations), ResourceGenerator.MaxPower),
            result.Value.State.PetState.Power);
    }

    // =======================================================================
    // OnDamageTaken — Battle Instinct (AC-6, AC-9)
    // =======================================================================

    [Fact]
    public async Task CommittedSwap_BattleInstinct_ShouldApplyItsNextAttackAtkModifier_WhenThePetTakesDamage()
    {
        // RELIC_RULES.md §3 / §8.5 item 10: OnDamageTaken, no Condition, ATK +10%,
        // Percentage, Pet, NextAttack. TASK-178 product decision Q-1 = A carries it
        // on the ONE ATK collection, distinguished by the element's own lifetime.
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);
        var created = await PrepareAsync(
            service,
            repository,
            "battle-battle-instinct",
            [BattleInstinct(), null, null],
            CascadeBoard(),
            pet => pet with { Crit = 0 });

        var result = await service.ExecuteSwapAsync("battle-battle-instinct", CascadeSwap());

        Assert.NotNull(result);
        Assert.True(result!.Value.IsAccepted);

        // The Pet took the Boss Response's damage (step 18c), and the Trigger
        // resolved after that instance's own DamageTaken report.
        var damageTakenIndex = IndexOfLast(
            result.Value.Events,
            BattleEventType.DamageTaken,
            e => e.DamageTaken.Target == DamageParty.Player);
        var relicIndex = IndexOfLast(
            result.Value.Events,
            BattleEventType.RelicTriggered,
            e => e.RelicTriggered.RelicId == Slot1);

        Assert.True(damageTakenIndex >= 0, "the Boss Response damaged the active Pet");
        Assert.True(relicIndex > damageTakenIndex, "the Trigger resolves after the damage instance");

        var modifier = Assert.Single(result.Value.State.PetState.ATKModifiers);
        Assert.Equal(Slot1, modifier.SourceIdentity);
        Assert.Equal(10, modifier.ATKModifierPercentage);
        Assert.Equal(RelicEffectLifetime.NextAttack, modifier.Lifetime);

        // §5.1.4 item 7: the permanent base stat is never written.
        Assert.Equal(PetState.DefaultATK, result.Value.State.PetState.ATK);
    }

    [Fact]
    public async Task CommittedSwap_BattleInstinct_ShouldReachTheNextAttackAndThenBeConsumed()
    {
        // AC-6 and the "NextAttack sharing" requirement: the ATK modifier reaches
        // the damage calculation through the GENERIC composition
        // (COMBAT_RULES.md §5.6/§5.6.6) and is consumed by the SAME qualifying-attack
        // boundary the Crit modifier uses (§3.3 items 7–11, GAME_STATE.md §5.1.4
        // item 4) — never by the Relic stage.
        //
        // The Boss is left at 10 HP for the second Swap, so that Swap's player attack
        // kills it and the resolution returns before any Boss Response — i.e. nothing
        // can re-apply the modifier after the attack consumed it.
        var withInstinct = await RunBattleInstinctSecondSwapAsync(
            "battle-battle-instinct-consuming",
            withBattleInstinct: true);
        var control = await RunBattleInstinctSecondSwapAsync(
            "battle-battle-instinct-control",
            withBattleInstinct: false);

        // Step 1's `Attack` input is the composed EffectivePetATK: base 50 at +10%
        // is truncate(50 × 110 / 100) = 55, and 50 without it. The transient
        // ATK-Gem pool is a separate Step-1 contribution and is added unchanged
        // (§5.6.1 item 3).
        Assert.Equal(
            55 + withInstinct.Pool,
            withInstinct.PlayerDamage.Base);
        Assert.Equal(
            PetState.DefaultATK + control.Pool,
            control.PlayerDamage.Base);

        // The Boss died from that attack, so the resolution ended before any later
        // damage instance: the consumed element is simply gone.
        Assert.Empty(withInstinct.State.PetState.ATKModifiers);
        Assert.Contains(withInstinct.Events, e => e.Type == BattleEventType.BattleWon);

        // §5.1.4 item 6: nothing was written back to the Pet's permanent stat.
        Assert.Equal(PetState.DefaultATK, withInstinct.State.PetState.ATK);
    }

    // =======================================================================
    // OnHpBelow — Execution Mark, with Emergency Core's regression (AC-4, AC-10)
    // =======================================================================

    [Fact]
    public async Task CommittedSwap_ExecutionMark_ShouldApplyItsNextAttackCrit_AndLeaveEmergencyCoreUnchanged()
    {
        // RELIC_RULES.md §8.5 item 8 / §6 note 3: Execution Mark is OnHpBelow with
        // HpPercentageBelow(30) and +15 percentage points of Crit with NextAttack
        // lifetime — the EXISTING OnHpBelow semantics, not a new reading. §8.5
        // item 2 / §6 note 2: Emergency Core keeps its -50% CardCost on the same
        // Trigger and the same Condition.
        //
        // The scenario's base Crit is one point below the fixture's own deterministic
        // Crit roll: with the fixed seed every test battle here uses
        // (FixedRngSeedSource.Seed, GAME_STATE.md §2.6.2 item 4 / ADR-009) the
        // bound-100 selection at this board and Swap yields V = 56, observed by
        // sweeping the base stat. Base Crit 56 therefore misses (V < 56 is false) and
        // 56 + 15 = 71 hits — so the two arms differ only by the +15 under test.
        const int baseCrit = 56;

        var execution = await RunExecutionMarkBattleAsync(
            "battle-execution-mark",
            withExecutionMark: true,
            baseCrit);
        var control = await RunExecutionMarkBattleAsync(
            "battle-execution-mark-control",
            withExecutionMark: false,
            baseCrit);

        // Both Relics are eligible at the same step 11 point, and they resolve in
        // equip-slot order (RELIC_RULES.md §4.2).
        Assert.Equal(
            [Slot1, Slot2],
            execution.Events
                .Where(e => e.Type == BattleEventType.RelicTriggered)
                .Select(e => e.RelicTriggered.RelicId)
                .ToArray());

        // Emergency Core's applied modifier is unchanged by this task: one Battle
        // -lifetime CardCost element carrying its §8.5 magnitude.
        var cardCost = Assert.Single(execution.State.PetState.CardCostModifiers);
        Assert.Equal(Slot2, cardCost.SourceIdentity);
        Assert.Equal(50, cardCost.CostReductionPercentage);

        // Execution Mark's +15 composed into the same Swap's Qualifying Attack
        // (step 11 → steps 15–17): 56 + 15 = 71 percentage points against this
        // fixture's V = 56 roll, so the attack Crits (COMBAT_RULES.md §3.3 item 2's
        // strict `V < EffectiveCrit`).
        Assert.Equal(DamagePipeline.CritMultiplier, execution.PlayerDamage.OtherModifiers, precision: 9);

        // The control is the same Swap against the same state without the Relic:
        // the base Crit alone misses, so the +15 is what produced the Crit — and the
        // composed value is what item 7's additions produce, not a rewritten base
        // stat.
        Assert.Equal(DamagePipeline.NonCritMultiplier, control.PlayerDamage.OtherModifiers, precision: 9);
        Assert.True(
            execution.PlayerDamage.FinalDamage > control.PlayerDamage.FinalDamage,
            "the Execution Mark Crit increased the consuming attack's damage");

        // The modifier is NextAttack-lifetime and the Swap's own attack consumed it
        // (GAME_STATE.md §5.1.2 item 4, §8.5 item 8).
        Assert.Empty(execution.State.PetState.NextAttackCritModifiers);
        Assert.Equal(baseCrit, execution.State.PetState.Crit);
    }

    // =======================================================================
    // OnCombo — Combo Fang (AC-2, AC-9)
    // =======================================================================

    [Fact]
    public async Task CommittedSwap_ComboFang_ShouldApplyAtComboFiveAndAbove_AndNothingBelowIt()
    {
        // RELIC_RULES.md §8.5 item 6 / §6: Combo Fang is OnCombo with
        // ComboAtLeast(5) and +20 percentage points of Crit with NextAttack
        // lifetime. The condition is the existing ComboAtLeast form, read from the
        // committed Swap's own Combo (§8.1 item 3, §8.1 item 8) — so the test drives
        // real Match-3 Combo output and asserts the boundary on it.
        //
        // Base Crit 85 + 20 reaches the 100-point cap, so a Swap whose Combo meets
        // the threshold must Crit and a Swap below it cannot be shown to have
        // received the +20 at all: the two arms are distinguishable without
        // depending on the seeded roll's value.
        const int baseCrit = 85;

        var met = 0;
        var notMet = 0;

        foreach (var pair in MatchProducingPairs(CascadeBoard()))
        {
            var battleId = $"battle-combo-fang-{pair.From}-{pair.To}";

            var repository = new InMemoryBattleStateRepository();
            var service = NewService(repository);
            await PrepareAsync(
                service,
                repository,
                battleId,
                [ComboFang(), null, null],
                CascadeBoard(),
                pet => pet with { Crit = baseCrit });

            var result = await service.ExecuteSwapAsync(battleId, pair);

            Assert.NotNull(result);

            if (!result!.Value.IsAccepted)
            {
                continue;
            }

            var combo = result.Value.State.Combo;
            var triggered = result.Value.Events.Any(
                e => e.Type == BattleEventType.RelicTriggered && e.RelicTriggered.RelicId == Slot1);

            Assert.Equal(combo >= 5, triggered);

            if (combo >= 5)
            {
                met++;

                // The +20 reached the same Swap's Qualifying Attack, which then
                // consumed the modifier.
                var playerDamage = result.Value.Events
                    .First(e => e.Type == BattleEventType.DamageCalculated);
                Assert.Equal(
                    DamagePipeline.CritMultiplier,
                    playerDamage.DamageCalculated.OtherModifiers,
                    precision: 9);
                Assert.Empty(result.Value.State.PetState.NextAttackCritModifiers);
            }
            else
            {
                notMet++;
                Assert.Empty(result.Value.State.PetState.NextAttackCritModifiers);
            }
        }

        Assert.True(met > 0, "the fixture board produces a Swap whose Combo meets ComboAtLeast(5)");
        Assert.True(notMet > 0, "the fixture board produces a Swap whose Combo is below it");
    }

    // =======================================================================
    // AC-8/AC-14 — the ONE NextAttack consumption boundary, over both carriers
    // =======================================================================

    [Fact]
    public async Task CommittedSwap_ShouldConsumeBothNextAttackLifetimes_WithTheOneQualifyingAttack()
    {
        // GAME_STATE.md §5.1.4 item 4 and COMBAT_RULES.md §3.3 items 7–11, as
        // TASK-178 Product Owner decision Q-1 = A records them: the
        // NextAttack-lifetime Crit element (§2.3.4) and the NextAttack-lifetime ATK
        // element (§2.3.7 item 11) are consumed at the SAME qualifying-attack
        // boundary — one shared consumption rule, never two.
        //
        // Swap 1 leaves Battle Instinct's ATK element standing (the Pet takes the
        // Boss Response's damage at step 18c, after that Swap's own attack). Swap 2
        // applies a Crit element at step 11 and its own attack consumes BOTH; the
        // Boss dies from that attack, so nothing re-applies either of them and the
        // committed state shows the consumption directly.
        //
        // The Crit fixture keeps §8.5's effect and lifetime with a
        // ComboAtLeast(1) scenario threshold, so the element is present on whatever
        // Swap the fixture produces — the same scenario-data convention the existing
        // Relic stage suite states.
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);

        var created = await PrepareAsync(
            service,
            repository,
            "battle-next-attack-sharing",
            [
                RelicRow("relic-assassin-eye", "OnCombo", RelicConditionType.ComboAtLeast, 1,
                    RelicEffectDefinition.Create(RelicEffectType.Crit, RelicEffectValueType.PercentagePoints, 10,
                        RelicEffectTarget.Pet, RelicEffectLifetime.NextAttack)),
                BattleInstinct(),
                null,
            ],
            CascadeBoard(),
            pet => pet with { Crit = 0, HP = 1000, MaxHP = 1000 });

        var first = await service.ExecuteSwapAsync("battle-next-attack-sharing", CascadeSwap());

        Assert.NotNull(first);
        Assert.True(first!.Value.IsAccepted);

        var standingAtk = Assert.Single(first.Value.State.PetState.ATKModifiers);
        Assert.Equal(Slot2, standingAtk.SourceIdentity);
        Assert.Equal(RelicEffectLifetime.NextAttack, standingAtk.Lifetime);

        var stored = await repository.GetAsync("battle-next-attack-sharing");
        Assert.NotNull(stored);

        var prepared = stored! with { BossState = stored.BossState with { HP = 10 } };
        await repository.TryUpdateAsync(prepared, prepared.Sequence);

        var second = await service.ExecuteSwapAsync(
            "battle-next-attack-sharing",
            FindMatchProducingPair(prepared));

        Assert.NotNull(second);
        Assert.True(second!.Value.IsAccepted);

        // Both Relics applied at step 11 (the Crit one) and earlier (the ATK one)...
        Assert.Contains(
            second.Value.Events,
            e => e.Type == BattleEventType.RelicTriggered && e.RelicTriggered.RelicId == Slot1);

        // ...and the one qualifying attack consumed both NextAttack elements. The
        // Battle-lifetime carriers are untouched: there are none in this scenario,
        // and the permanent stats are never written.
        Assert.Contains(second.Value.Events, e => e.Type == BattleEventType.BattleWon);
        Assert.Empty(second.Value.State.PetState.NextAttackCritModifiers);
        Assert.Empty(second.Value.State.PetState.ATKModifiers);
        Assert.Equal(0, second.Value.State.PetState.Crit);
        Assert.Equal(PetState.DefaultATK, second.Value.State.PetState.ATK);
    }

    // =======================================================================
    // AC-10 — the four existing Relics, end to end through the real pipeline
    // =======================================================================

    [Fact]
    public async Task CommittedSwap_ExistingFourRelics_ShouldResolveExactlyAsTheirDocumentedContracts()
    {
        // RELIC_RULES.md §8.5 items 1–4, with the committed Swap driven to satisfy
        // each §8.5 condition: the two MatchCount thresholds (cumulative, so the
        // stored count is set above them), ComboAtLeast(3), and
        // HpPercentageBelow(30). Each Relic then reaches its own carrier, and none
        // of this task's firing points changes anything for them.
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);

        var definitions = new RelicDefinition?[]
        {
            RelicRow("relic-berserker-core", "OnMatchCount", RelicConditionType.MatchCountAtLeast, 3,
                RelicEffectDefinition.Create(RelicEffectType.ATK, RelicEffectValueType.Percentage, 5,
                    RelicEffectTarget.Pet, RelicEffectLifetime.Battle)),
            RelicRow("relic-mana-crystal", "OnMatchCount", RelicConditionType.MatchCountAtLeast, 4,
                RelicEffectDefinition.Create(RelicEffectType.Power, RelicEffectValueType.Flat, 10,
                    RelicEffectTarget.Pet, RelicEffectLifetime.Immediate)),
            RelicRow("relic-assassin-eye", "OnCombo", RelicConditionType.ComboAtLeast, 3,
                RelicEffectDefinition.Create(RelicEffectType.Crit, RelicEffectValueType.PercentagePoints, 10,
                    RelicEffectTarget.Pet, RelicEffectLifetime.NextAttack)),
            RelicRow("relic-emergency-core", "OnHpBelow", RelicConditionType.HpPercentageBelow, 30,
                RelicEffectDefinition.Create(RelicEffectType.CardCost, RelicEffectValueType.Percentage, 50,
                    RelicEffectTarget.Pet, RelicEffectLifetime.Battle)),
        };

        var created = await service.CreateBattleAsync(
            "battle-existing-four-relics",
            Owner,
            Configuration([Slot1, Slot2, Slot3, Slot4]),
            Boss,
            equippedRelicDefinitions: definitions);

        var prepared = created with
        {
            BoardState = CascadeBoard(),
            MatchCount = 10,
            PetState = created.PetState with { HP = 25, MaxHP = 100, Crit = 0, Power = 0 },
        };
        await repository.TryUpdateAsync(prepared, prepared.Sequence);

        var result = await service.ExecuteSwapAsync("battle-existing-four-relics", CascadeSwap());

        Assert.NotNull(result);
        Assert.True(result!.Value.IsAccepted);

        var committed = result.Value.State;

        Assert.Equal(
            [Slot1, Slot2, Slot3, Slot4],
            result.Value.Events
                .Where(e => e.Type == BattleEventType.RelicTriggered)
                .Select(e => e.RelicTriggered.RelicId)
                .ToArray());

        // Berserker Core — a Battle-lifetime ATK element on the one ATK carrier;
        // §5.6.4 keeps the permanent stat.
        var atk = Assert.Single(committed.PetState.ATKModifiers);
        Assert.Equal(Slot1, atk.SourceIdentity);
        Assert.Equal(5, atk.ATKModifierPercentage);
        Assert.Equal(RelicEffectLifetime.Battle, atk.Lifetime);
        Assert.Equal(PetState.DefaultATK, committed.PetState.ATK);

        // Mana Crystal — Immediate, so the grant is on Power and on its report and
        // leaves nothing standing.
        Assert.Equal(
            10,
            Assert.Single(
                result.Value.Events,
                e => e.Type == BattleEventType.PowerChanged
                    && e.PowerChanged.Source == PowerChangeSource.Relic).PowerChanged.Delta);

        // Assassin Eye — NextAttack lifetime, consumed by the same Swap's own
        // Qualifying Attack.
        Assert.Empty(committed.PetState.NextAttackCritModifiers);
        Assert.Equal(0, committed.PetState.Crit);

        // Emergency Core — a Battle-lifetime CardCost element.
        var cardCost = Assert.Single(committed.PetState.CardCostModifiers);
        Assert.Equal(Slot4, cardCost.SourceIdentity);
        Assert.Equal(50, cardCost.CostReductionPercentage);

        Assert.Empty(committed.PetState.BurnDamageModifiers);
    }

    // =======================================================================
    // Harness
    // =======================================================================

    private static BattleStateService NewService(InMemoryBattleStateRepository repository) =>
        new(repository, new FixedRngSeedSource());

    private static Task<BattleState> CreateBattleAsync(
        BattleStateService service,
        string battleId,
        IReadOnlyList<RelicDefinition?> definitions) =>
        service.CreateBattleAsync(
            battleId,
            Owner,
            Configuration([Slot1, Slot2, Slot3]),
            Boss,
            equippedRelicDefinitions: definitions);

    /// <summary>
    /// Creates a battle with the given Relic content and then stores the resolution
    /// state the scenario needs — the fixed board, and the Pet/Boss values the
    /// scenario declares.
    /// </summary>
    private static async Task<BattleState> PrepareAsync(
        BattleStateService service,
        InMemoryBattleStateRepository repository,
        string battleId,
        IReadOnlyList<RelicDefinition?> definitions,
        BoardState board,
        Func<PetState, PetState>? pet = null,
        Func<BossState, BossState>? boss = null)
    {
        var created = await service.CreateBattleAsync(
            battleId,
            Owner,
            Configuration([Slot1, Slot2, Slot3]),
            Boss,
            equippedRelicDefinitions: definitions);

        var prepared = created with
        {
            BoardState = board,
            PetState = pet is null ? created.PetState : pet(created.PetState),
            BossState = boss is null ? created.BossState : boss(created.BossState),
        };

        await repository.TryUpdateAsync(prepared, prepared.Sequence);

        return prepared;
    }

    private static BattleStateService.PetConfiguration Configuration(string[] slots) =>
        new(
            new PetId(PetInstanceId),
            Element.Hoa,
            new PassiveId("xich-lang"),
            PassiveThreshold: 5,
            EquippedRelics: [.. slots.Select(slot => new EquippedRelicIdentity(slot))]);

    // -----------------------------------------------------------------------
    // Scenario runners
    // -----------------------------------------------------------------------

    /// <summary>
    /// One battle whose two Burn instances are already active on the two entities —
    /// a Pet-owned Burn on the Boss and a Boss-owned Burn on the Pet — so that the
    /// Swap's step 19a pass resolves exactly one tick of each
    /// (<c>COMBAT_RULES.md</c> §5.2 item 4's two ownership cases).
    ///
    /// <b>The comparison arm is a second, identical battle.</b> Both use the same
    /// seed, the same board, and the same Swap, so the only difference between them
    /// is whether Burning Curse is equipped — which is what makes the tick damage
    /// comparison evidence rather than a restatement of the arithmetic.
    /// </summary>
    private static async Task<BurnTickBattle> RunBurnTickBattleAsync(
        string battleId,
        bool withBurningCurse)
    {
        var (state, calculations) = await RunBurnTickScenarioAsync(battleId, withBurningCurse);

        return new BurnTickBattle(state, calculations);
    }

    private static async Task<(BattleState State, DamageCalculation[] Calculations)> RunBurnTickScenarioAsync(
        string battleId,
        bool withBurningCurse)
    {
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);

        await PrepareAsync(
            service,
            repository,
            battleId,
            [withBurningCurse ? BurningCurse() : null, null, null],
            CascadeBoard(),
            pet => pet with
            {
                HP = 1000,
                MaxHP = 1000,
                // Crit 0 removes the step-4 Crit roll from the comparison, so the two
                // battles differ only by the Burn damage modifier under test.
                Crit = 0,
                ActiveStatusEffects = [Burn(StatusEffectSource.Boss)],
            },
            boss => boss with
            {
                ActiveStatusEffects = [Burn(StatusEffectSource.Player)],
                // The Skill would refresh the Pet's Burn instance with its own
                // duration (BOSS_RULES.md §6.3.1 item 1), which is a different
                // resolution's concern; holding the cooldown keeps this scenario to
                // the two pre-applied instances.
                SkillCooldown = 9,
            });

        var result = await service.ExecuteSwapAsync(battleId, CascadeSwap());

        Assert.NotNull(result);
        Assert.True(result!.Value.IsAccepted);

        var calculations = result.Value.Events
            .Where(e => e.Type == BattleEventType.DamageCalculated)
            .Select(e => e.DamageCalculated)
            .ToArray();

        // The scenario's own guard: the two Burn ticks really are the last two
        // damage instances of the resolution, and they run Boss-side (the Pet-owned
        // Burn) before Pet-side (the Boss-owned Burn).
        Assert.True(calculations.Length >= 3, "the swap resolved its own attacks and both Burn ticks");

        var dealt = result.Value.Events
            .Where(e => e.Type == BattleEventType.DamageDealt)
            .Select(e => e.DamageDealt)
            .ToArray();

        Assert.Equal(DamageParty.Player, dealt[^2].Source);
        Assert.Equal(DamageParty.Boss, dealt[^2].Target);
        Assert.Equal(DamageParty.Boss, dealt[^1].Source);
        Assert.Equal(DamageParty.Player, dealt[^1].Target);

        return (result.Value.State, calculations);
    }

    /// <summary>
    /// A battle left one swap away from killing the Boss, with the second Swap's own
    /// player attack reported — the attack that must consume the active
    /// <c>NextAttack</c> ATK modifier, and, because the Boss dies, the resolution
    /// that cannot re-apply it afterwards.
    /// </summary>
    private static async Task<SecondSwapBattle> RunBattleInstinctSecondSwapAsync(
        string battleId,
        bool withBattleInstinct)
    {
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);

        var created = await PrepareAsync(
            service,
            repository,
            battleId,
            [withBattleInstinct ? BattleInstinct() : null, null, null],
            CascadeBoard(),
            pet => pet with { Crit = 0 });

        // First Swap: the Pet takes the Boss Response's damage, which is the
        // OnDamageTaken event the Relic reacts to.
        var first = await service.ExecuteSwapAsync(battleId, CascadeSwap());

        Assert.NotNull(first);
        Assert.True(first!.Value.IsAccepted);

        if (withBattleInstinct)
        {
            var applied = Assert.Single(first.Value.State.PetState.ATKModifiers);
            Assert.Equal(RelicEffectLifetime.NextAttack, applied.Lifetime);
        }

        // Leave the Boss one hit from death, so the second Swap's player attack ends
        // the resolution before any Boss Response could re-apply the modifier.
        var stored = await repository.GetAsync(battleId);
        Assert.NotNull(stored);

        var prepared = stored! with
        {
            BossState = stored.BossState with { HP = 10 },
        };
        await repository.TryUpdateAsync(prepared, prepared.Sequence);

        var pair = FindMatchProducingPair(prepared);
        var second = await service.ExecuteSwapAsync(battleId, pair);

        Assert.NotNull(second);
        Assert.True(second!.Value.IsAccepted);

        return new SecondSwapBattle(
            second.Value.State,
            second.Value.Events,
            second.Value.Resources.BaseDamagePool,
            second.Value.Events.First(e => e.Type == BattleEventType.DamageCalculated).DamageCalculated);
    }

    /// <summary>
    /// One battle at <c>HpPercentageBelow(30)</c> with Execution Mark in slot 1 and
    /// Emergency Core in slot 2 — the two <c>OnHpBelow</c> Relics of §8.5 items 2
    /// and 8 — resolved and reported.
    /// </summary>
    private static async Task<ExecutionMarkBattle> RunExecutionMarkBattleAsync(
        string battleId,
        bool withExecutionMark,
        int baseCrit)
    {
        var repository = new InMemoryBattleStateRepository();
        var service = NewService(repository);

        var definitions = new RelicDefinition?[]
        {
            withExecutionMark ? ExecutionMark() : null,
            RelicRow("relic-emergency-core", "OnHpBelow", RelicConditionType.HpPercentageBelow, 30,
                RelicEffectDefinition.Create(RelicEffectType.CardCost, RelicEffectValueType.Percentage, 50,
                    RelicEffectTarget.Pet, RelicEffectLifetime.Battle)),
            null,
        };

        var created = await PrepareAsync(
            service,
            repository,
            battleId,
            definitions,
            CascadeBoard(),
            pet => pet with { HP = 25, MaxHP = 100, Crit = baseCrit });

        var result = await service.ExecuteSwapAsync(battleId, CascadeSwap());

        Assert.NotNull(result);
        Assert.True(result!.Value.IsAccepted);

        return new ExecutionMarkBattle(
            result.Value.State,
            result.Value.Events,
            result.Value.Events.First(e => e.Type == BattleEventType.DamageCalculated).DamageCalculated);
    }

    // -----------------------------------------------------------------------
    // Fixtures — RELIC_RULES.md §8.5's rows, transcribed as data
    // -----------------------------------------------------------------------

    private static RelicDefinition BurningCurse() => RelicRow(
        "relic-burning-curse",
        "OnBattleStart",
        conditionType: null,
        threshold: 0,
        RelicEffectDefinition.Create(RelicEffectType.BurnDamage, RelicEffectValueType.Percentage, 30,
            RelicEffectTarget.Pet, RelicEffectLifetime.Battle));

    private static RelicDefinition ComboFang() => RelicRow(
        "relic-combo-fang",
        "OnCombo",
        RelicConditionType.ComboAtLeast,
        5,
        RelicEffectDefinition.Create(RelicEffectType.Crit, RelicEffectValueType.PercentagePoints, 20,
            RelicEffectTarget.Pet, RelicEffectLifetime.NextAttack));

    private static RelicDefinition ArcaneBattery() => RelicRow(
        "relic-arcane-battery",
        "OnPowerGain",
        conditionType: null,
        threshold: 0,
        RelicEffectDefinition.Create(RelicEffectType.Power, RelicEffectValueType.Flat, 5,
            RelicEffectTarget.Pet, RelicEffectLifetime.Immediate));

    private static RelicDefinition ExecutionMark() => RelicRow(
        "relic-execution-mark",
        "OnHpBelow",
        RelicConditionType.HpPercentageBelow,
        30,
        RelicEffectDefinition.Create(RelicEffectType.Crit, RelicEffectValueType.PercentagePoints, 15,
            RelicEffectTarget.Pet, RelicEffectLifetime.NextAttack));

    private static RelicDefinition CascadeCore() => RelicRow(
        "relic-cascade-core",
        "OnCascade",
        conditionType: null,
        threshold: 0,
        RelicEffectDefinition.Create(RelicEffectType.Power, RelicEffectValueType.Flat, 5,
            RelicEffectTarget.Pet, RelicEffectLifetime.Immediate));

    private static RelicDefinition BattleInstinct() => RelicRow(
        "relic-battle-instinct",
        "OnDamageTaken",
        conditionType: null,
        threshold: 0,
        RelicEffectDefinition.Create(RelicEffectType.ATK, RelicEffectValueType.Percentage, 10,
            RelicEffectTarget.Pet, RelicEffectLifetime.NextAttack));

    /// <summary>
    /// §8.5's Mana Crystal with a <c>MatchCountAtLeast(1)</c> scenario threshold, so
    /// the Q-2 boundary can be driven from any committed Swap: what is under test
    /// there is the ORIGIN of a Power gain, not the Relic's own threshold. Its
    /// effect is the §8.5 one — <c>Power, Flat, 10, Immediate</c>.</summary>
    private static RelicDefinition ManaCrystal() => RelicRow(
        "relic-mana-crystal",
        "OnMatchCount",
        RelicConditionType.MatchCountAtLeast,
        1,
        RelicEffectDefinition.Create(RelicEffectType.Power, RelicEffectValueType.Flat, 10,
            RelicEffectTarget.Pet, RelicEffectLifetime.Immediate));

    /// <summary>
    /// One in-memory <c>RelicDefinition</c> with the stated Trigger, optional
    /// Condition, and one effect element — the shape a provisioned §8.5 row holds.
    /// </summary>
    private static RelicDefinition RelicRow(
        string relicDefinitionId,
        string trigger,
        RelicConditionType? conditionType,
        int threshold,
        RelicEffectDefinition effect) => new()
        {
            RelicDefinitionId = relicDefinitionId,
            Name = "Fixture Relic",
            Trigger = trigger,
            Condition = conditionType is { } form
                ? RelicCondition.Create(form, threshold)
                : null,
            EffectDefinition = RelicEffectDefinitions.Create(effect),
        };

    /// <summary>
    /// A Burn-instance fixture — a Turn-based <c>DoT</c> whose <c>Source</c> is the
    /// side that applied it (<c>GAME_STATE.md</c> §2.3.1 item 7; <c>Player</c> is
    /// the Pet's side, ADR-011 items 3 and 5). Its duration outlasts the resolved
    /// Turn, so the scenario observes a tick rather than an expiry.
    /// </summary>
    private static StatusEffect Burn(StatusEffectSource source) =>
        StatusEffect.TurnBased("Burn", StatusEffectType.DoT, source, magnitude: 50, duration: 5);

    // -----------------------------------------------------------------------
    // Boards and Swaps
    // -----------------------------------------------------------------------

    /// <summary>
    /// The fixed cascade shape the existing Boss Passive suite uses: the Swap below
    /// completes a horizontal match of three in row 3 while the forced column match
    /// in row 4 makes the resolution a multi-step Cascade, which is what gives the
    /// <c>OnCascade</c> and Combo scenarios genuine Match-3 output
    /// (<c>MATCH3_RULES.md</c> §3–§4).
    /// </summary>
    private static BoardState CascadeBoard()
    {
        var rows = new[]
        {
            "ADPADPAD",
            "DPADPADP",
            "PADPADPA",
            "PAMMMPAD",
            "MMDMMDMM",
            "DPADPADP",
            "PADPADPA",
            "ADPADPAD",
        };

        return BoardFromRows(rows);
    }

    /// <summary>
    /// A board whose Swap at <c>(26, 34)</c> matches POWER Gems, generating the
    /// qualifying Power gain the <c>OnPowerGain</c> scenario needs.
    /// </summary>
    private static BoardState PowerGainMatchBoard()
    {
        var rows = new[]
        {
            "ADHPADHP",
            "DHPADHPA",
            "HPADHPAD",
            "PAPPPADH",
            "PPDHHDHP",
            "DHPADHPA",
            "HPADHPAD",
            "PADHPADH",
        };

        return BoardFromRows(rows);
    }

    private static BoardState BoardFromRows(string[] rows)
    {
        var cells = new GemType[BoardState.CellCount];

        for (var r = 0; r < BoardState.Rows; r++)
        {
            for (var c = 0; c < BoardState.Columns; c++)
            {
                cells[BoardState.ToIndex(r, c)] = rows[r][c] switch
                {
                    'A' or 'M' => GemType.Atk,
                    'D' => GemType.Def,
                    'H' => GemType.Hp,
                    'P' => GemType.Power,
                    _ => GemType.Atk,
                };
            }
        }

        return BoardState.FromCells(cells);
    }

    /// <summary>The Cascade board's documented multi-step Swap.</summary>
    private static SwapRequest CascadeSwap() => new(26, 34);

    /// <summary>The Power board's POWER-Gem Swap.</summary>
    private static SwapRequest SwapRequestForPowerBoard() => new(26, 34);

    private static IEnumerable<SwapRequest> MatchProducingPairs(BoardState board)
    {
        for (var index = 0; index < BoardState.CellCount; index++)
        {
            var right = BoardState.ToColumn(index) + 1 < BoardState.Columns ? index + 1 : -1;
            var down = index + BoardState.Width < BoardState.CellCount ? index + BoardState.Width : -1;

            if (right >= 0 && MatchDetector.Detect(board.WithSwapped(index, right)).Count > 0)
            {
                yield return new SwapRequest(index, right);
            }

            if (down >= 0 && MatchDetector.Detect(board.WithSwapped(index, down)).Count > 0)
            {
                yield return new SwapRequest(index, down);
            }
        }
    }

    private static SwapRequest FindAdjacentPairThatProducesAMatch(BoardState board)
    {
        foreach (var pair in MatchProducingPairs(board))
        {
            return pair;
        }

        throw new InvalidOperationException(
            "A generated board has at least one valid Swap (MATCH3_RULES.md §1.4).");
    }

    /// <summary>
    /// A match-producing pair other than the one already recorded as committed
    /// (<c>MATCH3_RULES.md</c> §2.1.4 rejects a repeat of it).
    /// </summary>
    private static SwapRequest FindMatchProducingPair(BattleState state)
    {
        foreach (var pair in MatchProducingPairs(state.BoardState))
        {
            if (state.LastCommittedSwapPair is { } committed
                && CommittedSwapPair.FromCells(pair.From, pair.To)
                    == CommittedSwapPair.FromCells(committed.MinCellIndex, committed.MaxCellIndex))
            {
                continue;
            }

            return pair;
        }

        throw new InvalidOperationException(
            "The fixture board has more than one match-producing Swap.");
    }

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

    // -----------------------------------------------------------------------
    // Scenario result shapes
    // -----------------------------------------------------------------------

    private readonly record struct BurnTickBattle(
        BattleState State,
        IReadOnlyList<DamageCalculation> Calculations);

    private readonly record struct SecondSwapBattle(
        BattleState State,
        IReadOnlyList<BattleEvent> Events,
        int Pool,
        DamageCalculation PlayerDamage);

    private readonly record struct ExecutionMarkBattle(
        BattleState State,
        IReadOnlyList<BattleEvent> Events,
        DamageCalculation PlayerDamage);
}
