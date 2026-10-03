using GameServer.Domain.Battle;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Relics;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// <c>GAME_RULES.md</c> §17 step 11 — "Trigger Relics" — through
/// <see cref="RelicResolver"/>: which Triggers the step evaluates, how each
/// structured Condition is compared, how each <c>effectType</c> reaches its
/// documented state carrier, and the ordering and anti-chain rules
/// <c>RELIC_RULES.md</c> §4–§5 define.
///
/// <code>
/// Rule (RELIC_RULES.md §3–§5, §8)
///  ↓
/// Scenario (Given an equipped Relic and a resolution state,
///           When step 11 runs, Then the documented carrier holds the
///           documented value and the documented reports are produced)
///  ↓
/// Test
/// </code>
///
/// <b>The content is §8.5's, and the runtime reads it as data.</b> The definitions
/// come from <see cref="RelicProvisionedDefinitions"/> — the transcribed
/// provisioned rows — so a test asserts that the resolver honours the stored
/// <c>Trigger</c>/<c>Condition</c>/<c>EffectDefinition[]</c>, never that it
/// recognises a Relic by name. No assertion here depends on a Relic's
/// <c>Name</c> or <c>RelicDefinitionId</c>.
/// </summary>
public class RelicResolverTests
{
    private const string Slot1 = "relic-instance-1";
    private const string Slot2 = "relic-instance-2";

    private static readonly PetId TestPetId = new("pet_instance_1");

    // =======================================================================
    // Conditions — RELIC_RULES.md §8.1
    // =======================================================================

    [Theory]
    // The cumulative Match count reached the threshold or more (§8.1): AtLeast is
    // inclusive, so the exact threshold applies.
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    public void MatchCountAtLeast_ShouldCompareTheCumulativeMatchCountInclusively(
        int matchCount,
        bool expected)
    {
        var result = Resolve(
            Relics(Slot1, RelicProvisionedDefinitions.BerserkerCore()),
            Pet(equipped: [Slot1]),
            matchCount: matchCount,
            combo: 0);

        Assert.Equal(expected, result.Triggered.Count == 1);
        Assert.Equal(expected, result.PetState.ATKModifiers.Length == 1);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    public void ComboAtLeast_ShouldCompareTheCurrentCombosInclusively(int combo, bool expected)
    {
        var result = Resolve(
            Relics(Slot1, RelicProvisionedDefinitions.AssassinEye()),
            Pet(equipped: [Slot1]),
            matchCount: 0,
            combo: combo);

        Assert.Equal(expected, result.PetState.NextAttackCritModifiers.Length == 1);
    }

    [Theory]
    // §8.1 item 6: the percentage form reads the active Pet's HP against its own
    // MaxHP, and "fell below N percent" is exclusive — HP at exactly 30% of MaxHP
    // has not fallen below it.
    [InlineData(1000, 301, false)]
    [InlineData(1000, 300, false)]
    [InlineData(1000, 299, true)]
    [InlineData(1000, 0, true)]
    [InlineData(100, 30, false)]
    [InlineData(100, 29, true)]
    public void HpPercentageBelow_ShouldCompareCurrentHpAgainstMaxHpExclusively(
        int maxHp,
        int hp,
        bool expected)
    {
        var result = Resolve(
            Relics(Slot1, RelicProvisionedDefinitions.EmergencyCore()),
            Pet(equipped: [Slot1], maxHp: maxHp, hp: hp),
            matchCount: 0,
            combo: 0);

        Assert.Equal(expected, result.PetState.CardCostModifiers.Length == 1);
    }

    [Fact]
    public void Condition_ShouldBeSatisfied_WhenTheRelicDeclaresNone()
    {
        // §8.1 item 4 / §1: Condition is optional, and a Relic whose Trigger alone
        // is its complete condition carries none. The Trigger decides on its own.
        var definition = Define(
            "relic-no-condition",
            "OnMatchCount",
            condition: null);

        var result = Resolve(
            Relics(Slot1, definition),
            Pet(equipped: [Slot1]),
            matchCount: 0,
            combo: 0);

        Assert.Single(result.PetState.ATKModifiers);
        Assert.Single(result.Triggered);
    }

    // =======================================================================
    // Trigger evaluation — RELIC_RULES.md §3, §6, §8.5
    // =======================================================================

    [Fact]
    public void OnMatchCount_ShouldApplyBerserkerCoresBattleLifetimeAtkModifier()
    {
        // RELIC_RULES.md §8.5: Berserker Core — ATK, Percentage, 5, Pet, Battle.
        // COMBAT_RULES.md §5.6.6 item 2: the +5 is a signed percentage-point
        // contribution on PetState.ATKModifiers[]. §5.6.4: PetState.ATK itself is
        // never written.
        var result = Resolve(
            Relics(Slot1, RelicProvisionedDefinitions.BerserkerCore()),
            Pet(equipped: [Slot1]),
            matchCount: 3,
            combo: 1);

        var modifier = Assert.Single(result.PetState.ATKModifiers);
        Assert.Equal(Slot1, modifier.SourceIdentity);
        Assert.Equal(5, modifier.ATKModifierPercentage);

        // COMBAT_RULES.md §5.6.4 / §5.6.6 item 8: the permanent base stat is never
        // written and no DefaultATK-style reset exists.
        Assert.Equal(PetState.DefaultATK, result.PetState.ATK);
        Assert.Equal(TestPetId, result.PetState.PetId);
        Assert.Equal(Slot1, Assert.Single(result.Triggered).RelicId);
        Assert.Empty(result.PowerChanges);
    }

    [Fact]
    public void OnCombo_ShouldApplyAssassinEyesNextAttackCritModifier()
    {
        // §8.5 item 1: Assassin Eye's magnitude is +10 percentage points, with the
        // NextAttack lifetime §8.3 item 4 reuses from ADR-017. §5.1.2 item 1 gives the
        // carrier one element per source identity, and PetState.Crit is never written
        // (COMBAT_RULES.md §3.3 item 7).
        var result = Resolve(
            Relics(Slot1, RelicProvisionedDefinitions.AssassinEye()),
            Pet(equipped: [Slot1]),
            matchCount: 0,
            combo: 3);

        var modifier = Assert.Single(result.PetState.NextAttackCritModifiers);
        Assert.Equal(Slot1, modifier.SourceIdentity);
        Assert.Equal(10, modifier.CritContribution);
        Assert.Equal(PetState.DefaultCrit, result.PetState.Crit);
        Assert.Single(result.Triggered);
    }

    [Fact]
    public void OnHpBelow_ShouldApplyEmergencyCoresCardCostModifier()
    {
        // §8.5 item 2: Emergency Core's effect is a Card-cost modifier on the active
        // Pet — CardCost, Percentage, 50, Pet, Battle — held in
        // GAME_STATE.md §2.3.5's PetState.CardCostModifiers[].
        var result = Resolve(
            Relics(Slot1, RelicProvisionedDefinitions.EmergencyCore()),
            Pet(equipped: [Slot1], hp: 299),
            matchCount: 0,
            combo: 0);

        var modifier = Assert.Single(result.PetState.CardCostModifiers);
        Assert.Equal(Slot1, modifier.SourceIdentity);
        Assert.Equal(50, modifier.CostReductionPercentage);
        Assert.Single(result.Triggered);
    }

    [Fact]
    public void Trigger_ShouldApplyNothing_WhenItBelongsToAnotherStagesFiringPoint()
    {
        // RELIC_RULES.md §3 gives each Trigger its own event: OnMatch fires per
        // individual Match, OnCardCast at a Card cast (CARD_RULES.md §3 item 4),
        // OnTurnStart/OnTurnEnd at the Turn boundaries. Step 11's event is the
        // committed Swap's resolution state, so a Relic declaring one of those is not
        // this stage's to evaluate — and nothing is invented for it.
        var definition = Define("relic-on-match", "OnMatch", condition: null);

        var result = Resolve(
            Relics(Slot1, definition),
            Pet(equipped: [Slot1]),
            matchCount: 10,
            combo: 10);

        Assert.Empty(result.Triggered);
        Assert.Empty(result.PetState.ATKModifiers);
        Assert.Empty(result.PetState.CardCostModifiers);
        Assert.Empty(result.PetState.NextAttackCritModifiers);
    }

    [Fact]
    public void Trigger_ShouldApplyNothing_WhenTheContentDoesNotResolve()
    {
        // No definition row means no declared Trigger, Condition, or Effect, so there
        // is nothing to evaluate and no fallback value may stand in for it
        // (AGENTS.md §7). The database-backed lookup's documented miss outcome is
        // null, which is what an unresolved slot carries.
        var result = Resolve(
            [new EquippedRelicContent(new EquippedRelicIdentity(Slot1), Definition: null)],
            Pet(equipped: [Slot1]),
            matchCount: 10,
            combo: 10);

        Assert.Empty(result.Triggered);
        Assert.Empty(result.PetState.ATKModifiers);
    }

    [Fact]
    public void Resolve_ShouldReportABlankEquippedIdentity()
    {
        // A slot with no identity cannot be refreshed or removed source-specifically
        // — the one property every carrier's key exists to provide
        // (GAME_STATE.md §2.3.5 item 3, §2.3.7 item 3) — and could not be reported as
        // a RelicTriggered identity either (RELIC_RULES.md §2.2 item 3).
        Assert.Throws<ArgumentException>(() => Resolve(
            [new EquippedRelicContent(new EquippedRelicIdentity("  "), RelicProvisionedDefinitions.BerserkerCore())],
            Pet(equipped: ["  "]),
            matchCount: 10,
            combo: 10));
    }

    // =======================================================================
    // Effect application and lifetimes — RELIC_RULES.md §8.2–§8.4
    // =======================================================================

    [Fact]
    public void PowerEffect_ShouldGrantFlatPowerAndLeaveNoStandingModification()
    {
        // §8.3 item 3: Immediate "denotes an effect applied once, at the moment it
        // triggers, which leaves no standing modification behind — Mana Crystal's
        // Power grant is applied to PetState.Power and the effect itself then ends."
        var result = Resolve(
            Relics(Slot1, RelicProvisionedDefinitions.ManaCrystal()),
            Pet(equipped: [Slot1], power: 5),
            matchCount: 4,
            combo: 1);

        Assert.Equal(15, result.PetState.Power);
        Assert.Empty(result.PetState.ATKModifiers);
        Assert.Empty(result.PetState.CardCostModifiers);
        Assert.Empty(result.PetState.NextAttackCritModifiers);

        var change = Assert.Single(result.PowerChanges);
        Assert.Equal(PowerChangeSource.Relic, change.Source);
        Assert.Equal(10, change.Delta);
        Assert.Equal(15, change.Power);
    }

    [Fact]
    public void PowerEffect_ShouldReportTheClampedChange_WhenTheCapAbsorbsIt()
    {
        // GAME_RULES.md §12 makes 0–100 an invariant of PetState.Power, and §17
        // step 13 is the single write site that clamps it. SIGNALR_PROTOCOL.md §3.2.24
        // item 1 makes delta "the signed change applied", so a grant the cap absorbs is
        // reported as the smaller change it actually produced — and 0 is a real value
        // that is sent as 0.
        var atCap = Resolve(
            Relics(Slot1, RelicProvisionedDefinitions.ManaCrystal()),
            Pet(equipped: [Slot1], power: 95),
            matchCount: 4,
            combo: 1);

        Assert.Equal(100, atCap.PetState.Power);
        Assert.Equal(5, Assert.Single(atCap.PowerChanges).Delta);

        var absorbed = Resolve(
            Relics(Slot1, RelicProvisionedDefinitions.ManaCrystal()),
            Pet(equipped: [Slot1], power: 100),
            matchCount: 4,
            combo: 1);

        Assert.Equal(100, absorbed.PetState.Power);
        Assert.Equal(0, Assert.Single(absorbed.PowerChanges).Delta);
    }

    [Fact]
    public void UndeterminedEffect_ShouldApplyNothing_AndEmitNothing()
    {
        // §8.2 item 2: Undetermined "records that the owning document states no
        // magnitude yet, and it remains valid for such an effect". §8.2 item 3: it
        // carries no value member at all. A resolver must treat it as an open content
        // gap and never substitute a value, so nothing applies and — because
        // RELIC_RULES.md §7 emits only when an Effect applies — nothing is reported.
        var effects = RelicEffectDefinitions.Create(
            RelicEffectDefinition.Undetermined(
                RelicEffectType.ATK,
                RelicEffectTarget.Pet,
                RelicEffectLifetime.Battle));

        var definition = Define("relic-undetermined", "OnMatchCount", condition: null, effects);

        var result = Resolve(
            Relics(Slot1, definition),
            Pet(equipped: [Slot1]),
            matchCount: 1,
            combo: 1);

        Assert.Empty(result.Triggered);
        Assert.Empty(result.PetState.ATKModifiers);
    }

    // =======================================================================
    // Same-source refresh, coexistence, and removal — RELIC_RULES.md §8.5
    // =======================================================================

    [Fact]
    public void RepeatedApplication_ShouldRefreshTheSameSource_NotAccumulate()
    {
        // GAME_STATE.md §5.1.4 items 1–2: an apply of an already-active source
        // identity REFRESHES that element with the newly applied value; it does not
        // add a second element and does not accumulate.
        var petState = Pet(equipped: [Slot1]);

        var first = Resolve(
            Relics(Slot1, RelicProvisionedDefinitions.BerserkerCore()),
            petState,
            matchCount: 3,
            combo: 1);

        var second = Resolve(
            Relics(Slot1, RelicProvisionedDefinitions.BerserkerCore()),
            first.PetState,
            matchCount: 4,
            combo: 1);

        var modifier = Assert.Single(second.PetState.ATKModifiers);
        Assert.Equal(Slot1, modifier.SourceIdentity);
        Assert.Equal(5, modifier.ATKModifierPercentage);
    }

    [Fact]
    public void RepeatedApplication_ShouldReplaceTheStoredValue_WithTheNewlyAppliedOne()
    {
        // §5.1.4 item 2 is explicit that a refresh "replaces the stored
        // ATKModifierPercentage with the value being applied; it does not preserve the
        // previously stored value, does not add the two, and does not leave the entry
        // unchanged" (TASK-136 D7).
        var stale = Pet(equipped: [Slot1]) with
        {
            ATKModifiers = [new ATKModifier(Slot1, 40)],
        };

        var result = Resolve(
            Relics(Slot1, RelicProvisionedDefinitions.BerserkerCore()),
            stale,
            matchCount: 3,
            combo: 1);

        var modifier = Assert.Single(result.PetState.ATKModifiers);
        Assert.Equal(5, modifier.ATKModifierPercentage);
    }

    [Fact]
    public void DifferentSources_ShouldCoexist()
    {
        // RELIC_RULES.md §2.4 item 3: two distinct owned instances of one definition
        // may be equipped together, and §4 makes the anti-chain rule operate per
        // equipped instance. Each instance therefore keys its own modifier and both
        // coexist.
        var equipped = Pet(equipped: [Slot1, Slot2]);
        var content = Relics(
            Slot1, RelicProvisionedDefinitions.BerserkerCore(),
            Slot2, RelicProvisionedDefinitions.BerserkerCore());

        var result = Resolve(content, equipped, matchCount: 3, combo: 1);

        Assert.Equal(2, result.PetState.ATKModifiers.Length);
        Assert.Contains(result.PetState.ATKModifiers, m => m.SourceIdentity == Slot1);
        Assert.Contains(result.PetState.ATKModifiers, m => m.SourceIdentity == Slot2);
        Assert.Equal([Slot1, Slot2], result.Triggered.Select(t => t.RelicId).ToArray());
    }

    [Fact]
    public void FailedCondition_ShouldRevertOnlyThatSourcesBattleLifetimeModifiers()
    {
        // RELIC_RULES.md §6 note 2 gives the provisioned case: Emergency Core is
        // "armed" whenever HP < 30% and "revert[s] when HP rises back above 30%".
        // §8.5 item 2 fixes the mechanics: continuous re-evaluation resolves to the
        // SAME source identity, so the applied modifier is replaced/refreshed in place
        // — exactly one entry at any time — and when the condition no longer holds it
        // is removed.
        var below = Resolve(
            Relics(Slot1, RelicProvisionedDefinitions.EmergencyCore()),
            Pet(equipped: [Slot1], hp: 299) with
            {
                CardCostModifiers = [new CardCostModifier(Slot1, 10)],
            },
            matchCount: 0,
            combo: 0);

        // Refreshed in place, not accumulated: one entry, carrying the applied value.
        Assert.Equal(50, Assert.Single(below.PetState.CardCostModifiers).CostReductionPercentage);
        Assert.Single(below.Triggered);

        // GAME_STATE.md §5.1.3 item 4 / §5.1.4 item 4: "When the source's documented
        // condition no longer holds, that source's element is removed from the
        // collection in that resolution … It removes only that source's element:
        // another source's modifier is untouched."
        var above = Resolve(
            Relics(Slot1, RelicProvisionedDefinitions.EmergencyCore()),
            Pet(equipped: [Slot1], hp: 500) with
            {
                ATKModifiers = [new ATKModifier(Slot1, 5), new ATKModifier("another-source", -30)],
                CardCostModifiers = [new CardCostModifier(Slot1, 50), new CardCostModifier("another-source", 25)],
            },
            matchCount: 0,
            combo: 0);

        Assert.Equal("another-source", Assert.Single(above.PetState.CardCostModifiers).SourceIdentity);
        Assert.Equal("another-source", Assert.Single(above.PetState.ATKModifiers).SourceIdentity);
        Assert.Empty(above.Triggered);

        // No cooldown, charge, or reset state is introduced for the reverted Relic:
        // §8.4 item 4 adds none, and the Relic is eligible again as soon as the
        // condition holds — which the first assertion above already demonstrated.
        Assert.Equal(50, Assert.Single(
            Resolve(
                Relics(Slot1, RelicProvisionedDefinitions.EmergencyCore()),
                above.PetState with { HP = 100 },
                matchCount: 0,
                combo: 0).PetState.CardCostModifiers,
            m => m.SourceIdentity == Slot1).CostReductionPercentage);
    }

    [Fact]
    public void FailedCondition_ShouldNotRemoveANextAttackModifier()
    {
        // GAME_STATE.md §5.1.2 item 3: for NextAttackCritModifiers[] "there is no
        // expiry of any kind … The only operation that removes an element is
        // Consume". RELIC_RULES.md §8.4 item 3 adds that a Relic whose effect lifetime
        // has ended is not disabled — it re-triggers on later events.
        var state = Pet(equipped: [Slot1]) with
        {
            NextAttackCritModifiers = [new NextAttackCritModifier(Slot1, 10)],
        };

        var result = Resolve(
            Relics(Slot1, RelicProvisionedDefinitions.AssassinEye()),
            state,
            matchCount: 0,
            combo: 2);

        var modifier = Assert.Single(result.PetState.NextAttackCritModifiers);
        Assert.Equal(10, modifier.CritContribution);
        Assert.Empty(result.Triggered);
    }

    [Fact]
    public void Relic_ShouldRemainEligibleToTriggerAgain_AfterItsEffectLifetimeEnded()
    {
        // RELIC_RULES.md §8.4 items 2–3: effect lifetime and trigger re-evaluation are
        // independent, and a Relic whose Immediate effect ended is not disabled.
        var definition = RelicProvisionedDefinitions.ManaCrystal();

        var first = Resolve(
            Relics(Slot1, definition),
            Pet(equipped: [Slot1], power: 0),
            matchCount: 4,
            combo: 1);

        Assert.Equal(10, first.PetState.Power);

        var second = Resolve(
            Relics(Slot1, definition),
            first.PetState,
            matchCount: 8,
            combo: 1);

        Assert.Equal(20, second.PetState.Power);
        Assert.Single(second.Triggered);
        Assert.Single(second.PowerChanges);
    }

    // =======================================================================
    // Ordering and the anti-infinite-chain safeguard — RELIC_RULES.md §4, §5
    // =======================================================================

    [Fact]
    public void EligibleRelics_ShouldResolveInTheEquipSlotOrderTheyAreGiven()
    {
        // §4.2: "For a single event, all eligible Relics resolve in that fixed slot
        // order", and §2.3 item 2 forbids re-sorting the slots by any property. The
        // sequence is the caller's — the equip-slot order — and nothing here re-ranks
        // it.
        var content = Relics(
            Slot2, RelicProvisionedDefinitions.AssassinEye(),
            Slot1, RelicProvisionedDefinitions.BerserkerCore());

        var result = Resolve(
            content,
            Pet(equipped: [Slot2, Slot1]),
            matchCount: 3,
            combo: 3);

        Assert.Equal([Slot2, Slot1], result.Triggered.Select(t => t.RelicId).ToArray());
    }

    [Fact]
    public void RepeatedEquippedInstance_ShouldResolveAtMostOncePerRootEvent()
    {
        // §5 item 2: a single triggering event may cause at most one full pass, and
        // "§5's per-Relic anti-chain rule operates per equipped instance" (§4). A slot
        // list that names one instance twice therefore applies its effect once — the
        // safeguard is stated rather than left implicit in the effect set.
        var content = Relics(
            Slot1, RelicProvisionedDefinitions.BerserkerCore(),
            Slot1, RelicProvisionedDefinitions.BerserkerCore());

        var result = Resolve(
            content,
            Pet(equipped: [Slot1, Slot1]),
            matchCount: 3,
            combo: 1);

        Assert.Single(result.Triggered);
        Assert.Single(result.PetState.ATKModifiers);
    }

    [Fact]
    public void EquippedRelics_ShouldBeIndependent_WhenOnlyOneIsEligible()
    {
        // Each Relic is evaluated on its own Trigger and Condition: Berserker Core's
        // MatchCount condition holds while Assassin Eye's Combo condition does not, so
        // exactly one applies and the other contributes nothing.
        var content = Relics(
            Slot1, RelicProvisionedDefinitions.BerserkerCore(),
            Slot2, RelicProvisionedDefinitions.AssassinEye());

        var result = Resolve(content, Pet(equipped: [Slot1, Slot2]), matchCount: 3, combo: 1);

        Assert.Equal([Slot1], result.Triggered.Select(t => t.RelicId).ToArray());
        Assert.Single(result.PetState.ATKModifiers);
        Assert.Empty(result.PetState.NextAttackCritModifiers);
    }

    // =======================================================================
    // Harness
    // =======================================================================

    private static RelicResolutionResult Resolve(
        IReadOnlyList<EquippedRelicContent> equippedRelics,
        PetState petState,
        int matchCount,
        int combo) =>
        RelicResolver.Resolve(equippedRelics, petState, matchCount, combo);

    private static EquippedRelicContent[] Relics(
        string slot,
        RelicDefinition definition) =>
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
    /// A Relic definition the test builds from an explicitly stated Trigger and
    /// optional Condition, using §8.3's allowed <c>ATK</c> combination as its effect —
    /// the shape §8.5's rows take.
    /// </summary>
    private static RelicDefinition Define(
        string relicDefinitionId,
        string trigger,
        RelicCondition? condition,
        RelicEffectDefinitions? effects = null) => new()
        {
            RelicDefinitionId = relicDefinitionId,
            Name = "Fixture Relic",
            Trigger = trigger,
            Condition = condition,
            EffectDefinition = effects ?? RelicEffectDefinitions.Create(
                RelicEffectDefinition.Create(
                    RelicEffectType.ATK,
                    RelicEffectValueType.Percentage,
                    5,
                    RelicEffectTarget.Pet,
                    RelicEffectLifetime.Battle)),
        };

    /// <summary>
    /// The active Pet's state at step 11 — the documented creation state with the
    /// equipped slots the scenario declares, then the HP, MaxHP, and Power the
    /// scenario needs (<c>GAME_STATE.md</c> §2.3).
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
