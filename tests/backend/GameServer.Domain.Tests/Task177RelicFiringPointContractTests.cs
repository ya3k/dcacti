using GameServer.Domain.Battle;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Relics;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// TASK-177 — the approved MVP Relic runtime contracts, at
/// <see cref="RelicResolver"/>'s own level: the four firing points TASK-176's
/// Relics declare beyond <c>GAME_RULES.md</c> §17 step 11 (<c>OnBattleStart</c>,
/// <c>OnCascade</c>, <c>OnPowerGain</c>, <c>OnDamageTaken</c>), the
/// <c>BurnDamage</c> effect identity, the <c>NextAttack</c> lifetime on the
/// ATK carrier, and the recursion/ownership boundaries
/// <c>RELIC_RULES.md</c> §3.1, §3.2, §5, §6 note 1, and §8.5 define.
///
/// <code>
/// Rule (RELIC_RULES.md §3, §3.1, §3.2, §5, §6, §8.2–§8.5;
///       COMBAT_RULES.md §3.3, §5.2 item 4;
///       GAME_STATE.md §2.3.7, §2.3.8, §5.1.4)
///  ↓
/// Scenario (Given an equipped Relic and a resolution state at a firing point,
///           When that firing point runs, Then the documented carrier holds the
///           documented value and no other point resolves it)
///  ↓
/// Test
/// </code>
///
/// <b>The six new rows are declared once, as data, from §8.5's table.</b> None of
/// them is provisioned (TASK-177's database boundary), so the tests build the
/// <c>RelicDefinition</c> in memory exactly as the provisioning task will — the
/// same pattern <see cref="RelicProvisionedDefinitions"/> uses for the four rows
/// that are provisioned. Every magnitude, threshold, target, and lifetime below
/// is §8.5's transcription; the runtime never recognises a Relic by name or id.
/// </summary>
public class Task177RelicFiringPointContractTests
{
    private const string Slot1 = "relic-instance-1";
    private const string Slot2 = "relic-instance-2";

    private static readonly PetId TestPetId = new("pet_instance_1");

    // =======================================================================
    // RELIC_RULES.md §8.2 item 1 / §8.3 — the BurnDamage row, and ATK's two rows
    // =======================================================================

    [Fact]
    public void BurnDamage_ShouldAcceptExactlyItsDocumentedCombination()
    {
        // RELIC_RULES.md §8.3's table: BurnDamage | Pet | Battle | Percentage —
        // §8.5 item 5's Burning Curse row, declared as TASK-176 approved it.
        var effect = RelicEffectDefinition.Create(
            RelicEffectType.BurnDamage,
            RelicEffectValueType.Percentage,
            30,
            RelicEffectTarget.Pet,
            RelicEffectLifetime.Battle);

        Assert.Equal(RelicEffectType.BurnDamage, effect.EffectType);
        Assert.Equal(RelicEffectValueType.Percentage, effect.ValueType);
        Assert.Equal(30, effect.Value);
        Assert.Equal(RelicEffectLifetime.Battle, effect.Lifetime);
        Assert.Equal(
            RelicEffectValueType.Percentage,
            RelicEffectDefinition.RequiredValueTypeFor(RelicEffectType.BurnDamage));
        Assert.Equal(
            [RelicEffectLifetime.Battle],
            RelicEffectDefinition.AllowedLifetimesFor(RelicEffectType.BurnDamage));
    }

    [Theory]
    [InlineData(RelicEffectValueType.Flat)]
    [InlineData(RelicEffectValueType.PercentagePoints)]
    [InlineData(RelicEffectValueType.Undetermined)]
    public void BurnDamage_ShouldRejectAValueTypeTheTableDoesNotList(RelicEffectValueType valueType)
    {
        // §8.3's closing rule: "a combination not listed is not defined and may not
        // be inferred". BurnDamage's row states Percentage alone.
        Assert.ThrowsAny<ArgumentException>(() => RelicEffectDefinition.Create(
            RelicEffectType.BurnDamage,
            valueType,
            30,
            RelicEffectTarget.Pet,
            RelicEffectLifetime.Battle));
    }

    [Theory]
    [InlineData(RelicEffectLifetime.Immediate)]
    [InlineData(RelicEffectLifetime.NextAttack)]
    public void BurnDamage_ShouldRejectALifetimeTheTableDoesNotList(RelicEffectLifetime lifetime)
    {
        Assert.Throws<ArgumentException>(() => RelicEffectDefinition.Create(
            RelicEffectType.BurnDamage,
            RelicEffectValueType.Percentage,
            30,
            RelicEffectTarget.Pet,
            lifetime));
    }

    [Fact]
    public void BurnDamage_ShouldRoundTripThroughItsPersistedPayload()
    {
        // RELIC_RULES.md §8.5's Burning Curse element, member for member — the
        // representation a downstream provisioning task would store.
        var effects = RelicEffectDefinitions.Create(RelicEffectDefinition.Create(
            RelicEffectType.BurnDamage,
            RelicEffectValueType.Percentage,
            30,
            RelicEffectTarget.Pet,
            RelicEffectLifetime.Battle));

        var payload = effects.ToPersistedPayload();

        Assert.Equal(
            """[{"effectType":"BurnDamage","valueType":"Percentage","value":30,"target":"Pet","lifetime":"Battle"}]""",
            payload);

        var read = RelicEffectDefinitions.FromPersistedPayload(payload);

        Assert.Equal(effects, read);
        Assert.Equal(30, read[0].Value);
        Assert.Equal(RelicEffectLifetime.Battle, read[0].Lifetime);
    }

    [Theory]
    [InlineData(RelicEffectLifetime.Battle)]
    [InlineData(RelicEffectLifetime.NextAttack)]
    public void Atk_ShouldAcceptBothLifetimesTheTableLists(RelicEffectLifetime lifetime)
    {
        // RELIC_RULES.md §8.3's table lists ATK twice — Battle (§8.5 item 4,
        // Berserker Core) and NextAttack (§8.5 item 10, Battle Instinct;
        // TASK-178 Product Owner decision Q-1 = A).
        Assert.Contains(lifetime, RelicEffectDefinition.AllowedLifetimesFor(RelicEffectType.ATK));

        var effect = RelicEffectDefinition.Create(
            RelicEffectType.ATK,
            RelicEffectValueType.Percentage,
            10,
            RelicEffectTarget.Pet,
            lifetime);

        Assert.Equal(lifetime, effect.Lifetime);
    }

    [Fact]
    public void Atk_ShouldStillRejectImmediate()
    {
        // Validation is widened to the table's rows, never removed: ATK's two rows
        // are Battle and NextAttack, so Immediate remains undefined for it (§8.3
        // item 2).
        Assert.Throws<ArgumentException>(() => RelicEffectDefinition.Create(
            RelicEffectType.ATK,
            RelicEffectValueType.Percentage,
            10,
            RelicEffectTarget.Pet,
            RelicEffectLifetime.Immediate));
    }

    // =======================================================================
    // RELIC_RULES.md §3 / §8.5 — the firing points, one Trigger each
    // =======================================================================

    [Fact]
    public void BattleStart_ShouldApplyBurningCursesStandingBurnDamageModifier()
    {
        // RELIC_RULES.md §3: OnBattleStart "fires once, at battle start"; §8.5
        // item 5: BurnDamage +30%, Percentage, target Pet, Battle lifetime.
        var result = Resolve(
            Relics(Slot1, BurningCurse()),
            Pet(equipped: [Slot1]),
            RelicFiringPoint.BattleStart);

        var modifier = Assert.Single(result.PetState.BurnDamageModifiers);
        Assert.Equal(Slot1, modifier.SourceIdentity);
        Assert.Equal(30, modifier.BurnDamagePercentage);
        Assert.Equal([Slot1], result.Triggered.Select(t => t.RelicId).ToArray());
        Assert.Empty(result.PowerChanges);
    }

    [Fact]
    public void BattleStart_ShouldNotStackASecondEntry_WhenEvaluatedAgain()
    {
        // §8.5 item 5 / §6 note 1: non-stacking, once per battle. A repeated
        // evaluation resolves to the same equipped-instance identity, so the
        // applied modifier is replaced/refreshed in place rather than accumulated.
        var relics = Relics(Slot1, BurningCurse());

        var once = Resolve(relics, Pet(equipped: [Slot1]), RelicFiringPoint.BattleStart);
        var twice = Resolve(relics, once.PetState, RelicFiringPoint.BattleStart);

        var modifier = Assert.Single(twice.PetState.BurnDamageModifiers);
        Assert.Equal(30, modifier.BurnDamagePercentage);
    }

    [Fact]
    public void BattleStart_ShouldNotEvaluateTheStepElevenTriggers()
    {
        // §3 gives each Trigger its own firing point. Berserker Core declares
        // OnMatchCount, whose event is the committed Swap's resolution state, so
        // the battle-start point applies nothing for it — and does not revert a
        // standing modifier it did not create either.
        var standing = Pet(equipped: [Slot1]) with
        {
            ATKModifiers = [new ATKModifier(Slot1, 5, RelicEffectLifetime.Battle)],
        };

        var result = Resolve(
            Relics(Slot1, RelicProvisionedDefinitions.BerserkerCore()),
            standing,
            RelicFiringPoint.BattleStart,
            matchCount: 9,
            combo: 9);

        Assert.Empty(result.Triggered);
        var modifier = Assert.Single(result.PetState.ATKModifiers);
        Assert.Equal(5, modifier.ATKModifierPercentage);
    }

    [Fact]
    public void BoardResolution_ShouldNotEvaluateOnBattleStart()
    {
        // The other direction of the same rule: Burning Curse's OnBattleStart is
        // not the committed Swap's event, so step 11 applies nothing for it.
        var result = Resolve(
            Relics(Slot1, BurningCurse()),
            Pet(equipped: [Slot1]),
            RelicFiringPoint.BoardResolution,
            matchCount: 9,
            combo: 9);

        Assert.Empty(result.Triggered);
        Assert.Empty(result.PetState.BurnDamageModifiers);
    }

    [Theory]
    // Every §8.5 row, at the firing point that is NOT its own: nothing resolves.
    // (Berserker Core / Mana Crystal / Emergency Core are OnMatchCount and
    // OnHpBelow — the step-11 point; Assassin Eye / Combo Fang are OnCombo.)
    [InlineData(RelicFiringPoint.BattleStart)]
    [InlineData(RelicFiringPoint.CascadeIteration)]
    [InlineData(RelicFiringPoint.PowerGain)]
    [InlineData(RelicFiringPoint.DamageTaken)]
    public void ForeignFiringPoints_ShouldNotResolveAStepElevenRelic(RelicFiringPoint firingPoint)
    {
        var result = Resolve(
            Relics(Slot1, RelicProvisionedDefinitions.BerserkerCore()),
            Pet(equipped: [Slot1]),
            firingPoint,
            matchCount: 99,
            combo: 99);

        Assert.Empty(result.Triggered);
        Assert.Empty(result.PetState.ATKModifiers);
    }

    [Fact]
    public void PowerGain_ShouldApplyArcaneBatterysFlatPower()
    {
        // RELIC_RULES.md §3.1 / §8.5 item 7: OnPowerGain, no Condition, Power +5,
        // Flat, Immediate. §8.3 item 3: Immediate leaves no standing modification,
        // so the grant is visible on PetState.Power and on the report alone.
        var result = Resolve(
            Relics(Slot1, ArcaneBattery()),
            Pet(equipped: [Slot1], power: 40),
            RelicFiringPoint.PowerGain,
            powerGainSource: PowerChangeSource.Match);

        Assert.Equal(45, result.PetState.Power);

        var change = Assert.Single(result.PowerChanges);
        Assert.Equal(PowerChangeSource.Relic, change.Source);
        Assert.Equal(5, change.Delta);
        Assert.Equal(45, change.Power);
    }

    [Theory]
    [InlineData(PowerChangeSource.Match, true)]
    [InlineData(PowerChangeSource.Card, true)]
    [InlineData(PowerChangeSource.Boss, true)]
    [InlineData(PowerChangeSource.Relic, false)]
    public void PowerGain_ShouldQualifyOnlyNonRelicGeneratedGains(
        PowerChangeSource source,
        bool expected)
    {
        // RELIC_RULES.md §3.1 items 1–2 / §8.5 item 7 — the canonical qualification:
        // "a Power gain qualifies iff it originates outside Relic effect
        // resolution". The exclusion is a property of the gain's origin, so it
        // covers Arcane Battery, Cascade Core, Mana Crystal, and any future Relic
        // that grants Power, without enumerating them.
        Assert.Equal(expected, RelicResolver.IsQualifyingPowerGain(source));

        var result = Resolve(
            Relics(Slot1, ArcaneBattery()),
            Pet(equipped: [Slot1], power: 40),
            RelicFiringPoint.PowerGain,
            powerGainSource: source);

        Assert.Equal(expected, result.Triggered.Count == 1);
        Assert.Equal(expected ? 45 : 40, result.PetState.Power);
        Assert.Equal(expected, result.PowerChanges.Count == 1);
    }

    [Fact]
    public void PowerGain_ShouldNotRecurseThroughThePowerTheRelicItselfGrants()
    {
        // RELIC_RULES.md §3.1 item 2 / §8.5 item 7: the blocked loop is
        //   OnPowerGain → Arcane Battery → +5 Power → OnPowerGain  ← BLOCKED
        // The gain Arcane Battery grants originates in Relic effect resolution, so
        // it is not a qualifying event — and re-resolving the point for that gain
        // resolves nothing at all, so there is no second activation and no depth
        // limit anywhere.
        var relics = Relics(Slot1, ArcaneBattery());

        var first = Resolve(
            relics,
            Pet(equipped: [Slot1], power: 40),
            RelicFiringPoint.PowerGain,
            powerGainSource: PowerChangeSource.Match);

        Assert.Single(first.Triggered);

        var second = Resolve(
            relics,
            first.PetState,
            RelicFiringPoint.PowerGain,
            powerGainSource: PowerChangeSource.Relic);

        Assert.Empty(second.Triggered);
        Assert.Empty(second.PowerChanges);
        Assert.Equal(first.PetState.Power, second.PetState.Power);
    }

    [Fact]
    public void CascadeIteration_ShouldApplyCascadeCoresFlatPowerOncePerIteration()
    {
        // RELIC_RULES.md §3.2 item 2 / §8.5 item 9: each actual cascade iteration is
        // an independent OnCascade event, so Cascade Core resolves once per
        // iteration — Cascade #1 → +5, Cascade #2 → +5, Cascade #3 → +5 — and the
        // cascades of one Swap are not collapsed into one application.
        var relics = Relics(Slot1, CascadeCore());
        var state = Pet(equipped: [Slot1], power: 0);

        foreach (var expectedPower in new[] { 5, 10, 15 })
        {
            var iteration = Resolve(relics, state, RelicFiringPoint.CascadeIteration);

            Assert.Single(iteration.Triggered);
            var change = Assert.Single(iteration.PowerChanges);
            Assert.Equal(5, change.Delta);
            Assert.Equal(expectedPower, iteration.PetState.Power);

            state = iteration.PetState;
        }
    }

    [Fact]
    public void CascadeIteration_ShouldNotResolveAtTheStepElevenPoint()
    {
        // The Swap-level point and a cascade iteration are different events
        // (RELIC_RULES.md §3.2 item 3), so an OnCascade Relic does not resolve from
        // the former.
        var result = Resolve(
            Relics(Slot1, CascadeCore()),
            Pet(equipped: [Slot1]),
            RelicFiringPoint.BoardResolution,
            matchCount: 3,
            combo: 1);

        Assert.Empty(result.Triggered);
        Assert.Equal(0, result.PetState.Power);
    }

    [Fact]
    public void DamageTaken_ShouldApplyBattleInstinctsNextAttackAtkModifier()
    {
        // RELIC_RULES.md §3 / §8.5 item 10: OnDamageTaken, no Condition, ATK +10%,
        // Percentage, target Pet, NextAttack lifetime. TASK-178 Product Owner
        // decision Q-1 = A: it rides the ONE ATK carrier, distinguished by the
        // element's own declared lifetime.
        var result = Resolve(
            Relics(Slot1, BattleInstinct()),
            Pet(equipped: [Slot1]),
            RelicFiringPoint.DamageTaken);

        var modifier = Assert.Single(result.PetState.ATKModifiers);
        Assert.Equal(Slot1, modifier.SourceIdentity);
        Assert.Equal(10, modifier.ATKModifierPercentage);
        Assert.Equal(RelicEffectLifetime.NextAttack, modifier.Lifetime);

        // §5.1.4 item 7: the permanent base stat is never written by a NextAttack
        // modifier, and the modifier is not consumed by the resolver — consumption
        // belongs to the qualifying attack (COMBAT_RULES.md §3.3 items 7–11).
        Assert.Equal(PetState.DefaultATK, result.PetState.ATK);
        Assert.Single(result.PetState.ATKModifiers);
    }

    [Fact]
    public void DamageTaken_ShouldRefreshItsOneElement_WhenTheSourceReTriggers()
    {
        // §8.5 item 10 / §2.3.7 item 4: non-stacking — a re-trigger by the same
        // source refreshes that source's one element rather than accumulating.
        var relics = Relics(Slot1, BattleInstinct());

        var first = Resolve(relics, Pet(equipped: [Slot1]), RelicFiringPoint.DamageTaken);
        var second = Resolve(relics, first.PetState, RelicFiringPoint.DamageTaken);

        var modifier = Assert.Single(second.PetState.ATKModifiers);
        Assert.Equal(10, modifier.ATKModifierPercentage);
        Assert.Equal(RelicEffectLifetime.NextAttack, modifier.Lifetime);
    }

    [Fact]
    public void ComboFang_ShouldReTriggerAfterItsModifierIsConsumed()
    {
        // §8.4 item 3: "A Relic whose effect lifetime has ended is not disabled" —
        // its Trigger is still re-evaluated on later events. §8.5 item 6 states the
        // same for Combo Fang: "Once consumed by the next qualifying attack, it
        // re-fires when the trigger condition is met again."
        var relics = Relics(Slot1, ComboFang());

        var applied = Resolve(
            relics,
            Pet(equipped: [Slot1]),
            RelicFiringPoint.BoardResolution,
            matchCount: 0,
            combo: 5);

        var crit = Assert.Single(applied.PetState.NextAttackCritModifiers);
        Assert.Equal(20, crit.CritContribution);

        // The qualifying attack's consumption (GAME_STATE.md §5.1.2 item 4) — the
        // only operation that removes such an element.
        var consumed = applied.PetState with
        {
            NextAttackCritModifiers = NextAttackCritModifiers.Consume(
                applied.PetState.NextAttackCritModifiers,
                [Slot1]),
        };

        Assert.Empty(consumed.NextAttackCritModifiers);

        var reApplied = Resolve(
            relics,
            consumed,
            RelicFiringPoint.BoardResolution,
            matchCount: 0,
            combo: 5);

        Assert.Equal(20, Assert.Single(reApplied.PetState.NextAttackCritModifiers).CritContribution);
    }

    [Theory]
    // §8.1 item 6 / §8.5 item 8: Execution Mark's Condition is the existing
    // HpPercentageBelow(30) form, and "fell below N percent" is exclusive — HP at
    // exactly 30% of MaxHP has not fallen below it.
    [InlineData(1000, 300, false)]
    [InlineData(1000, 299, true)]
    [InlineData(100, 30, false)]
    [InlineData(100, 29, true)]
    public void ExecutionMark_ShouldReadTheExistingHpPercentageBelowBoundary(
        int maxHp,
        int hp,
        bool expected)
    {
        var result = Resolve(
            Relics(Slot1, ExecutionMark()),
            Pet(equipped: [Slot1], maxHp: maxHp, hp: hp),
            RelicFiringPoint.BoardResolution,
            matchCount: 0,
            combo: 0);

        Assert.Equal(expected, result.Triggered.Count == 1);
        Assert.Equal(expected, result.PetState.NextAttackCritModifiers.Length == 1);

        if (expected)
        {
            Assert.Equal(15, Assert.Single(result.PetState.NextAttackCritModifiers).CritContribution);
        }
    }

    [Fact]
    public void DamageTaken_ShouldCoexistWithABattleLifetimeSource_OnTheOneCarrier()
    {
        // Q-1 = A: both lifetimes live in PetState.ATKModifiers[] and are
        // distinguished by the element's own Lifetime — Berserker Core's Battle
        // element (item 4) and Battle Instinct's NextAttack element (item 10).
        var heritage = Pet(equipped: [Slot1, Slot2]) with
        {
            ATKModifiers = [new ATKModifier(Slot1, 5, RelicEffectLifetime.Battle)],
        };

        var result = Resolve(
            Relics(Slot1, RelicProvisionedDefinitions.BerserkerCore(), Slot2, BattleInstinct()),
            heritage,
            RelicFiringPoint.DamageTaken);

        Assert.Equal(2, result.PetState.ATKModifiers.Length);
        Assert.Equal(
            RelicEffectLifetime.Battle,
            Assert.Single(result.PetState.ATKModifiers, m => m.SourceIdentity == Slot1).Lifetime);
        Assert.Equal(
            RelicEffectLifetime.NextAttack,
            Assert.Single(result.PetState.ATKModifiers, m => m.SourceIdentity == Slot2).Lifetime);

        // The generic composition sums both while they are active: 50 × (100 + 5 +
        // 10) / 100 = 57 (COMBAT_RULES.md §5.6.6 item 5). The NextAttack element is
        // not excluded from the composition while it is active — it is consumed by
        // the qualifying attack, which is a different question (§3.3 item 8).
        Assert.Equal(
            57,
            EffectivePetATK.Compose(
                result.PetState.ATK,
                result.PetState.ATKModifiers,
                result.PetState.ActiveStatusEffects));
    }

    // =======================================================================
    // RELIC_RULES.md §6 note 1 / COMBAT_RULES.md §5.2 item 4 — Burn ownership
    // =======================================================================

    [Fact]
    public void BurnDamageModifier_ShouldApplyOnlyToPetOwnedBurn()
    {
        // TASK-178 Product Owner decision Q-4 = C: Burning Curse modifies Burn
        // damage the PET applied/owns; Burn the Boss applied/owns receives nothing.
        // The distinction is the Burn instance's source/ownership — never the
        // entity receiving the tick's damage.
        var petOwned = Burn(StatusEffectSource.Player);
        var bossOwned = Burn(StatusEffectSource.Boss);

        Assert.True(BurnDamageModifiers.AppliesTo(petOwned));
        Assert.False(BurnDamageModifiers.AppliesTo(bossOwned));
    }

    [Fact]
    public void BurnDamageModifier_ShouldNotBeAStatusEffectOfAnotherType()
    {
        // COMBAT_RULES.md §5.1 gives the damage-over-time category to DoT alone, so
        // a Shield (or any non-DoT instance) is not a Burn tick even where its
        // source is the Pet's.
        var petShield = StatusEffect.TriggerBased(
            "Shield",
            StatusEffectType.Shield,
            StatusEffectSource.Player,
            magnitude: 50,
            StatusEffect.ShieldDepletedCondition);

        Assert.False(BurnDamageModifiers.AppliesTo(petShield));
    }

    [Fact]
    public void BurnDamageModifiers_ShouldRefreshNotStack_AndSumAcrossDistinctSources()
    {
        // The collection's storage invariant (one element per source identity, the
        // sibling modifiers' rule) plus its applied percentage: one source refreshes
        // its own element, two distinct sources both contribute.
        var applied = BurnDamageModifiers.Apply([], new BurnDamageModifier(Slot1, 30));
        applied = BurnDamageModifiers.Apply(applied, new BurnDamageModifier(Slot1, 30));

        Assert.Single(applied);
        Assert.Equal(30, BurnDamageModifiers.AppliedPercentage(applied));

        applied = BurnDamageModifiers.Apply(applied, new BurnDamageModifier(Slot2, 20));

        Assert.Equal(2, applied.Length);
        Assert.Equal(50, BurnDamageModifiers.AppliedPercentage(applied));

        var removed = BurnDamageModifiers.Remove(applied, Slot1);

        var remaining = Assert.Single(removed);
        Assert.Equal(Slot2, remaining.SourceIdentity);
        Assert.Equal(20, BurnDamageModifiers.AppliedPercentage(removed));
    }

    [Fact]
    public void RevertedStepElevenSource_ShouldDropItsBurnDamageModifier()
    {
        // GAME_STATE.md §5.1.4 item 5: a source whose documented condition no longer
        // holds has its element removed in that resolution. The ruling applies to
        // the BurnDamage carrier exactly as it does to the other Battle-lifetime
        // carriers.
        var standing = Pet(equipped: [Slot1], hp: 900) with
        {
            BurnDamageModifiers = [new BurnDamageModifier(Slot1, 30)],
        };

        var result = Resolve(
            Relics(Slot1, RelicProvisionedDefinitions.EmergencyCore()),
            standing,
            RelicFiringPoint.BoardResolution,
            matchCount: 0,
            combo: 0);

        Assert.Empty(result.Triggered);
        Assert.Empty(result.PetState.BurnDamageModifiers);
    }

    // =======================================================================
    // GAME_STATE.md §2.3.7 item 11 / §5.1.4 item 4 — the ATK carrier's lifetimes
    // =======================================================================

    [Fact]
    public void AtkCarrier_ShouldConsumeOnlyTheNextAttackLifetime()
    {
        // §5.1.4 item 4: consumption is lifetime-scoped. The qualifying attack
        // removes the NextAttack elements it consumed and "must NOT remove a
        // Battle-lifetime element".
        var collection = ATKModifiers.Apply([], new ATKModifier(Slot1, 5, RelicEffectLifetime.Battle));
        collection = ATKModifiers.Apply(collection, new ATKModifier(Slot2, 10, RelicEffectLifetime.NextAttack));

        var consumed = ATKModifiers.ConsumeForQualifyingAttack(collection);

        var remaining = Assert.Single(consumed);
        Assert.Equal(Slot1, remaining.SourceIdentity);
        Assert.Equal(RelicEffectLifetime.Battle, remaining.Lifetime);
    }

    [Fact]
    public void AtkCarrier_ShouldRemoveOnlyTheNamedLifetimeOfOneSource()
    {
        // §5.1.4 item 4 and item 5: removal is source-specific AND
        // lifetime-scoped, so a Battle source's condition reversion cannot disturb
        // the same source's NextAttack element.
        var collection = ATKModifiers.Apply([], new ATKModifier(Slot1, 5, RelicEffectLifetime.Battle));
        collection = ATKModifiers.Apply(collection, new ATKModifier(Slot1, 10, RelicEffectLifetime.NextAttack));

        var reverted = ATKModifiers.RemoveLifetime(collection, Slot1, RelicEffectLifetime.Battle);

        var remaining = Assert.Single(reverted);
        Assert.Equal(RelicEffectLifetime.NextAttack, remaining.Lifetime);
        Assert.Equal(10, remaining.ATKModifierPercentage);
    }

    [Fact]
    public void AtkCarrier_ShouldRejectImmediateLifetime()
    {
        // §2.3.7 item 8: Immediate "leaves no standing modification behind and
        // therefore never produces an element here", so it is reported rather than
        // stored.
        Assert.Throws<ArgumentException>(() => ATKModifiers.Apply(
            [],
            new ATKModifier(Slot1, 5, RelicEffectLifetime.Immediate)));
    }

    // =======================================================================
    // RELIC_RULES.md §8.5 items 1–4 / §8.4 — the four existing Relics unchanged
    // =======================================================================

    [Fact]
    public void ExistingFourRelics_ShouldResolveUnchanged_AtTheStepElevenPoint()
    {
        // RELIC_RULES.md §8.5 items 1–4, transcribed: Berserker Core's Battle ATK,
        // Mana Crystal's Immediate Power, Assassin Eye's NextAttack Crit, and
        // Emergency Core's Battle CardCost. Each still reaches its own carrier, in
        // slot order, and NextAttack is still not consumed by the stage.
        var result = Resolve(
            [
                new EquippedRelicContent(new EquippedRelicIdentity(Slot1), RelicProvisionedDefinitions.BerserkerCore()),
                new EquippedRelicContent(new EquippedRelicIdentity(Slot2), RelicProvisionedDefinitions.AssassinEye()),
            ],
            Pet(equipped: [Slot1, Slot2]),
            RelicFiringPoint.BoardResolution,
            matchCount: 3,
            combo: 3);

        Assert.Equal([Slot1, Slot2], result.Triggered.Select(t => t.RelicId).ToArray());

        var atk = Assert.Single(result.PetState.ATKModifiers);
        Assert.Equal(Slot1, atk.SourceIdentity);
        Assert.Equal(5, atk.ATKModifierPercentage);
        Assert.Equal(RelicEffectLifetime.Battle, atk.Lifetime);

        var crit = Assert.Single(result.PetState.NextAttackCritModifiers);
        Assert.Equal(Slot2, crit.SourceIdentity);
        Assert.Equal(10, crit.CritContribution);

        Assert.Empty(result.PowerChanges);
        Assert.Equal(PetState.DefaultATK, result.PetState.ATK);
    }

    [Fact]
    public void ExistingFourRelics_ShouldProduceNothing_AtAnyNewFiringPoint()
    {
        // AC-10's regression surface: the four provisioned rows declare
        // OnMatchCount / OnCombo / OnHpBelow, so none of the four new firing points
        // resolves any of them.
        foreach (var firingPoint in new[]
                 {
                     RelicFiringPoint.BattleStart,
                     RelicFiringPoint.CascadeIteration,
                     RelicFiringPoint.PowerGain,
                     RelicFiringPoint.DamageTaken,
                 })
        {
            var result = Resolve(
                [
                    new EquippedRelicContent(new EquippedRelicIdentity(Slot1), RelicProvisionedDefinitions.BerserkerCore()),
                    new EquippedRelicContent(new EquippedRelicIdentity(Slot2), RelicProvisionedDefinitions.AssassinEye()),
                ],
                Pet(equipped: [Slot1, Slot2]),
                firingPoint,
                matchCount: 99,
                combo: 99);

            Assert.Empty(result.Triggered);
            Assert.Empty(result.PetState.ATKModifiers);
            Assert.Empty(result.PetState.NextAttackCritModifiers);
            Assert.Empty(result.PetState.BurnDamageModifiers);
            Assert.Empty(result.PowerChanges);
        }
    }

    // =======================================================================
    // Fixtures — RELIC_RULES.md §8.5's six new rows, transcribed as data
    // =======================================================================

    /// <summary>
    /// §8.5: Trigger <c>OnBattleStart</c>; Condition <c>null</c>;
    /// <c>BurnDamage, Percentage, 30, Pet, Battle</c> — §6's "+30% Burn damage".
    /// </summary>
    private static RelicDefinition BurningCurse() => Define(
        "relic-burning-curse",
        RelicResolver.OnBattleStartTrigger,
        condition: null,
        RelicEffectDefinition.Create(
            RelicEffectType.BurnDamage,
            RelicEffectValueType.Percentage,
            30,
            RelicEffectTarget.Pet,
            RelicEffectLifetime.Battle));

    /// <summary>
    /// §8.5: Trigger <c>OnCombo</c>; Condition <c>ComboAtLeast(5)</c>;
    /// <c>Crit, PercentagePoints, 20, Pet, NextAttack</c>.
    /// </summary>
    private static RelicDefinition ComboFang() => Define(
        "relic-combo-fang",
        RelicResolver.OnComboTrigger,
        RelicCondition.Create(RelicConditionType.ComboAtLeast, 5),
        RelicEffectDefinition.Create(
            RelicEffectType.Crit,
            RelicEffectValueType.PercentagePoints,
            20,
            RelicEffectTarget.Pet,
            RelicEffectLifetime.NextAttack));

    /// <summary>
    /// §8.5: Trigger <c>OnPowerGain</c>; Condition <c>null</c>;
    /// <c>Power, Flat, 5, Pet, Immediate</c>.
    /// </summary>
    private static RelicDefinition ArcaneBattery() => Define(
        "relic-arcane-battery",
        RelicResolver.OnPowerGainTrigger,
        condition: null,
        RelicEffectDefinition.Create(
            RelicEffectType.Power,
            RelicEffectValueType.Flat,
            5,
            RelicEffectTarget.Pet,
            RelicEffectLifetime.Immediate));

    /// <summary>
    /// §8.5: Trigger <c>OnHpBelow</c>; Condition <c>HpPercentageBelow(30)</c>;
    /// <c>Crit, PercentagePoints, 15, Pet, NextAttack</c>.
    /// </summary>
    private static RelicDefinition ExecutionMark() => Define(
        "relic-execution-mark",
        RelicResolver.OnHpBelowTrigger,
        RelicCondition.Create(RelicConditionType.HpPercentageBelow, 30),
        RelicEffectDefinition.Create(
            RelicEffectType.Crit,
            RelicEffectValueType.PercentagePoints,
            15,
            RelicEffectTarget.Pet,
            RelicEffectLifetime.NextAttack));

    /// <summary>
    /// §8.5: Trigger <c>OnCascade</c>; Condition <c>null</c>;
    /// <c>Power, Flat, 5, Pet, Immediate</c>.
    /// </summary>
    private static RelicDefinition CascadeCore() => Define(
        "relic-cascade-core",
        RelicResolver.OnCascadeTrigger,
        condition: null,
        RelicEffectDefinition.Create(
            RelicEffectType.Power,
            RelicEffectValueType.Flat,
            5,
            RelicEffectTarget.Pet,
            RelicEffectLifetime.Immediate));

    /// <summary>
    /// §8.5: Trigger <c>OnDamageTaken</c>; Condition <c>null</c>;
    /// <c>ATK, Percentage, 10, Pet, NextAttack</c>.
    /// </summary>
    private static RelicDefinition BattleInstinct() => Define(
        "relic-battle-instinct",
        RelicResolver.OnDamageTakenTrigger,
        condition: null,
        RelicEffectDefinition.Create(
            RelicEffectType.ATK,
            RelicEffectValueType.Percentage,
            10,
            RelicEffectTarget.Pet,
            RelicEffectLifetime.NextAttack));

    /// <summary>
    /// One in-memory <c>RelicDefinition</c> with the stated Trigger, optional
    /// Condition, and one effect element. The runtime reads exactly these members,
    /// so the fixture is the same shape a provisioned row holds.
    /// </summary>
    private static RelicDefinition Define(
        string relicDefinitionId,
        string trigger,
        RelicCondition? condition,
        RelicEffectDefinition effect) => new()
        {
            RelicDefinitionId = relicDefinitionId,
            Name = "Fixture Relic",
            Trigger = trigger,
            Condition = condition,
            EffectDefinition = RelicEffectDefinitions.Create(effect),
        };

    /// <summary>
    /// A Burn-instance fixture: a Turn-based <c>DoT</c> with the stated source
    /// (<c>GAME_STATE.md</c> §2.3.1 item 7 — <c>Player</c> is the Pet's side).
    /// </summary>
    private static StatusEffect Burn(StatusEffectSource source) =>
        StatusEffect.TurnBased("Burn", StatusEffectType.DoT, source, magnitude: 50, duration: 2);

    private static RelicResolutionResult Resolve(
        IReadOnlyList<EquippedRelicContent> equippedRelics,
        PetState petState,
        RelicFiringPoint firingPoint,
        int matchCount = 0,
        int combo = 0,
        PowerChangeSource? powerGainSource = null) =>
        RelicResolver.Resolve(
            equippedRelics,
            petState,
            firingPoint,
            matchCount,
            combo,
            powerGainSource);

    private static EquippedRelicContent[] Relics(string slot, RelicDefinition definition) =>
        [new EquippedRelicContent(new EquippedRelicIdentity(slot), definition)];

    private static EquippedRelicContent[] Relics(
        string firstSlot,
        RelicDefinition firstDefinition,
        string secondSlot,
        RelicDefinition secondDefinition) =>
        [
            new EquippedRelicContent(new EquippedRelicIdentity(firstSlot), firstDefinition),
            new EquippedRelicContent(new EquippedRelicIdentity(secondSlot), secondDefinition),
        ];

    /// <summary>
    /// The active Pet's state at a firing point — the documented creation state
    /// with the equipped slots the scenario declares, then the HP, MaxHP, and Power
    /// the scenario needs (<c>GAME_STATE.md</c> §2.3).
    /// </summary>
    private static PetState Pet(
        string[] equipped,
        int hp = PetState.DefaultHP,
        int maxHp = PetState.DefaultMaxHP,
        int power = PetState.DefaultPower) =>
        PetState.AtBattleCreation(
            TestPetId,
            Element.Hoa,
            new PassiveId("xich-lang"),
            passiveThreshold: 5,
            equippedRelics: [.. equipped.Select(slot => new EquippedRelicIdentity(slot))]) with
        {
            HP = hp,
            MaxHP = maxHp,
            Power = power,
        };
}
