using GameServer.Domain.Battle;
using GameServer.Domain.Cards;
using GameServer.Domain.Match3;
using Xunit;

namespace GameServer.Domain.Tests.Cards;

/// <summary>
/// The Boss actor's side of the shared cast contract (<c>CARD_RULES.md</c> §1.1,
/// §3 item 9; <c>BOSS_RULES.md</c> §4.1–§4.2; <c>ADR-029</c> Slice 2).
///
/// <code>
/// Given a Boss with BossState.Power P and its own Boss Card of cost C
/// When the Boss Card cast resolves through CardCastExecutor
/// Then BossState.Power = P − C
/// And  a self effect lands on BossState
/// And  an opponent effect lands on the active Pet
/// And  the Pet's allowance, Power, cost modifiers, loaded Cards, Sequence,
///      SkillCharge, SkillCooldown, Turn and cast events are unchanged
/// </code>
///
/// <b>Every Card definition below is a synthetic FIXTURE.</b> Its
/// <c>CardDefinitionId</c>, <c>PowerCost</c> and effect magnitudes are test values
/// chosen so the assertions can distinguish the actors and the effects. The four
/// Boss-only effect fixtures use the TASK-309 §1 approved shapes
/// (<c>flame-burst</c> AtkBuff 50/3 at cost 40, <c>root</c> AtkDebuff 30/3 at cost
/// 35, <c>drain-power</c> PowerDrain 20 at cost 25, <c>thunder-strike</c>
/// NextAtkBuff 50 at cost 40) so the executor tests exercise the approved
/// contract; the other fixtures remain test values, not authored content.
/// </summary>
public sealed class BossCardCastExecutorTests
{
    // =======================================================================
    // Fixtures — synthetic Boss Card definitions (NOT authored content)
    // =======================================================================

    /// <summary>Self Heal, 20% of the CASTER's Max HP (fixture cost 30).</summary>
    private static readonly CardDefinition BossHealCard = new()
    {
        CardDefinitionId = "fixture-boss-card-heal",
        Name = "Boss Heal Fixture",
        Category = CardCategory.BossCard,
        PowerCost = 30,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.PercentMaxHp, 20)),
    };

    /// <summary>Self Shield, 20% of the CASTER's Max HP (fixture cost 30).</summary>
    private static readonly CardDefinition BossShieldCard = new()
    {
        CardDefinitionId = "fixture-boss-card-shield",
        Name = "Boss Shield Fixture",
        Category = CardCategory.BossCard,
        PowerCost = 30,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Shield, CardEffectValueType.PercentMaxHp, 20)),
    };

    /// <summary>Self Power grant of +25, clamped to the documented 0–100 (fixture cost 10).</summary>
    private static readonly CardDefinition BossPowerCard = new()
    {
        CardDefinitionId = "fixture-boss-card-power",
        Name = "Boss Power Fixture",
        Category = CardCategory.BossCard,
        PowerCost = 10,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Power, CardEffectValueType.Flat, 25)),
    };

    /// <summary>Opponent Burn: 40 per tick for 2 Turns (fixture cost 20).</summary>
    private static readonly CardDefinition BossBurnCard = new()
    {
        CardDefinitionId = "fixture-boss-card-burn",
        Name = "Boss Burn Fixture",
        Category = CardCategory.BossCard,
        PowerCost = 20,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Burn(CardEffectValueType.Flat, 40, 2)),
    };

    /// <summary>
    /// Boss-only self ATK buff: +50 percentage points for 3 Turns (the approved
    /// `flame-burst` effect shape, TASK-309 §1 B-2; fixture cost 40).
    /// </summary>
    private static readonly CardDefinition BossAtkBuffCard = new()
    {
        CardDefinitionId = "flame-burst",
        Name = "Boss AtkBuff Fixture",
        Category = CardCategory.BossCard,
        PowerCost = 40,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.AtkBuff(50, 3)),
    };

    /// <summary>
    /// Boss-only opponent ATK debuff: the applied magnitude 30 for 3 Turns (the
    /// approved `root` effect shape, TASK-309 §1 B-3; fixture cost 35).
    /// </summary>
    private static readonly CardDefinition BossAtkDebuffCard = new()
    {
        CardDefinitionId = "root",
        Name = "Boss AtkDebuff Fixture",
        Category = CardCategory.BossCard,
        PowerCost = 35,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.AtkDebuff(30, 3)),
    };

    /// <summary>
    /// Boss-only instant flat Power drain of 20 (the approved `drain-power` effect
    /// shape, TASK-309 §1; fixture cost 25).
    /// </summary>
    private static readonly CardDefinition BossPowerDrainCard = new()
    {
        CardDefinitionId = "drain-power",
        Name = "Boss PowerDrain Fixture",
        Category = CardCategory.BossCard,
        PowerCost = 25,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.PowerDrain(20)),
    };

    /// <summary>
    /// Boss-only trigger-based next-qualifying-attack buff: +50 percentage points
    /// consumed by the next qualifying attack (the approved `thunder-strike` effect
    /// shape, TASK-309 §1; fixture cost 40).
    /// </summary>
    private static readonly CardDefinition BossNextAtkBuffCard = new()
    {
        CardDefinitionId = "thunder-strike",
        Name = "Boss NextAtkBuff Fixture",
        Category = CardCategory.BossCard,
        PowerCost = 40,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.NextAtkBuff(50)),
    };

    /// <summary>One self Power grant and one opponent Burn in a single cast (fixture cost 10).</summary>
    private static readonly CardDefinition BossPowerAndBurnCard = new()
    {
        CardDefinitionId = "fixture-boss-card-power-and-burn",
        Name = "Boss Power And Burn Fixture",
        Category = CardCategory.BossCard,
        PowerCost = 10,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Power, CardEffectValueType.Flat, 25),
            CardEffectDefinition.Burn(CardEffectValueType.Flat, 40, 2)),
    };

    /// <summary>A zero-cost self Heal — the documented "a cost of 0 is a real cost" case.</summary>
    private static readonly CardDefinition BossFreeHealCard = new()
    {
        CardDefinitionId = "fixture-boss-card-free-heal",
        Name = "Boss Free Heal Fixture",
        Category = CardCategory.BossCard,
        PowerCost = 0,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.Flat, 100)),
    };

    /// <summary>A Boss Card carrying a Damage effect — not representable for this actor.</summary>
    private static readonly CardDefinition BossDamageCard = new()
    {
        CardDefinitionId = "fixture-boss-card-damage",
        Name = "Boss Damage Fixture",
        Category = CardCategory.BossCard,
        PowerCost = 10,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 50)),
    };

    /// <summary>A Boss Card carrying a Crit effect — not representable for this actor.</summary>
    private static readonly CardDefinition BossCritCard = new()
    {
        CardDefinitionId = "fixture-boss-card-crit",
        Name = "Boss Crit Fixture",
        Category = CardCategory.BossCard,
        PowerCost = 10,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Crit(10, "NextAttack")),
    };

    /// <summary>
    /// A Damage element behind an otherwise-resolvable Power element, so the
    /// "throws before anything is written" property is observable: were the guard
    /// per-effect, the Power grant would already have landed.
    /// </summary>
    private static readonly CardDefinition BossPowerThenDamageCard = new()
    {
        CardDefinitionId = "fixture-boss-card-power-then-damage",
        Name = "Boss Power Then Damage Fixture",
        Category = CardCategory.BossCard,
        PowerCost = 10,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Power, CardEffectValueType.Flat, 25),
            CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 50)),
    };

    /// <summary>The Player's Basic Heal Card — a Player category, never castable by the Boss.</summary>
    private static readonly CardDefinition PlayerHealCard = new()
    {
        CardDefinitionId = "card-heal",
        Name = "Heal",
        Category = CardCategory.Basic,
        PowerCost = 20,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.PercentMaxHp, 20)),
    };

    /// <summary>The Player's Pet Skill Card — a Player category, never castable by the Boss.</summary>
    private static readonly CardDefinition PlayerPetSkillCard = new()
    {
        CardDefinitionId = "card-inferno",
        Name = "Inferno",
        Category = CardCategory.PetSkill,
        PowerCost = 40,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 100)),
    };

    // =======================================================================
    // State fixtures
    // =======================================================================

    private const int BossMaxHp = 5000;
    private const int PetMaxHp = 1000;

    /// <summary>
    /// A battle whose Boss and Pet have deliberately DIFFERENT Max HP (5000 vs
    /// 1000), so a <c>PercentMaxHp</c> assertion can tell which actor's Max HP the
    /// percentage was interpreted against.
    /// </summary>
    private static BattleState CreateTestBattleState(
        int bossPower = 50,
        int bossHp = BossMaxHp,
        int petHp = PetMaxHp,
        int petPower = 50,
        IReadOnlyList<StatusEffect>? bossStatusEffects = null,
        IReadOnlyList<StatusEffect>? petStatusEffects = null,
        CardCostModifier[]? petCostModifiers = null,
        EquippedCardIdentity[]? equippedCards = null)
    {
        var baseState = BattleState.CreateWith("battle-bosscast-test", 12345);

        return baseState with
        {
            PetState = baseState.PetState with
            {
                HP = petHp,
                MaxHP = PetMaxHp,
                Power = petPower,
                EquippedCards = equippedCards ?? [],
                ActiveStatusEffects = petStatusEffects?.ToArray() ?? [],
                CardCostModifiers = petCostModifiers ?? [],
            },
            BossState = baseState.BossState with
            {
                HP = bossHp,
                MaxHP = BossMaxHp,
                Power = bossPower,
                ActiveStatusEffects = bossStatusEffects?.ToArray() ?? [],
            },
        };
    }

    // =======================================================================
    // Actor / category compatibility, both directions
    // =======================================================================

    [Fact]
    public void Execute_PlayerActor_CastOfBossCardDefinition_IsRejectedInvalidCard()
    {
        // CARD_RULES.md §1.1 items 4–5 / §3 item 9: a Boss Card never appears in
        // PetState.EquippedCards[] and the Player cannot cast it. The definition is
        // placed in the loadout deliberately, so the CATEGORY gate — not the loadout
        // check that runs earlier — is what must report.
        var state = CreateTestBattleState(
            equippedCards: [new EquippedCardIdentity(BossHealCard.CardDefinitionId)]);

        var result = CardCastExecutor.Execute(state, BossHealCard);

        Assert.True(result.IsRejected);
        Assert.Equal(CardCastRejectionReason.InvalidCard, result.Reason);
    }

    [Fact]
    public void Execute_PlayerActorOverload_CastOfBossCardDefinition_IsRejectedInvalidCard()
    {
        // The actor-aware entry point states the same gate explicitly.
        var state = CreateTestBattleState(
            equippedCards: [new EquippedCardIdentity(BossHealCard.CardDefinitionId)]);

        var result = CardCastExecutor.Execute(state, BossHealCard, CardCastActor.Player);

        Assert.True(result.IsRejected);
        Assert.Equal(CardCastRejectionReason.InvalidCard, result.Reason);
    }

    [Fact]
    public void Execute_BossActor_CastOfBasicCard_IsRejectedInvalidCard()
    {
        // BOSS_RULES.md §4.2 item 8 / CARD_RULES.md §3 item 9: the Boss never casts a
        // Player Card, so a Basic definition is rejected on the category gate.
        var state = CreateTestBattleState(bossPower: 100, petPower: 100);

        var result = CardCastExecutor.Execute(state, PlayerHealCard, CardCastActor.Boss);

        Assert.True(result.IsRejected);
        Assert.Equal(CardCastRejectionReason.InvalidCard, result.Reason);
    }

    [Fact]
    public void Execute_BossActor_CastOfPetSkillCard_IsRejectedInvalidCard()
    {
        var state = CreateTestBattleState(bossPower: 100, petPower: 100);

        var result = CardCastExecutor.Execute(state, PlayerPetSkillCard, CardCastActor.Boss);

        Assert.True(result.IsRejected);
        Assert.Equal(CardCastRejectionReason.InvalidCard, result.Reason);
    }

    [Fact]
    public void Execute_UndefinedActor_IsRejectedRatherThanDefaultedToThePlayer()
    {
        // An undefined acting side names no document's cast contract, so it is an
        // argument error rather than a silent fallback to the Player's contract
        // (AGENTS.md §7).
        var state = CreateTestBattleState();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => CardCastExecutor.Execute(state, PlayerHealCard, (CardCastActor)99));
    }

    // =======================================================================
    // Cost: empty modifier set, sufficiency, exact deduction, zero cost
    // =======================================================================

    [Fact]
    public void Execute_BossActor_ComposesEffectiveCardCostWithAnEmptyModifierSet()
    {
        // CARD_RULES.md §3 item 9 / ADR-029 §2.4 item 11: PetState.CardCostModifiers[]
        // is the Pet-state carrier of a Relic effect and Relics are Player-only, so a
        // 50-point Pet-side reduction must not touch a Boss Card's cost. The authored
        // cost (30) is deducted in full, not the composed-for-the-Pet 15.
        var state = CreateTestBattleState(
            bossPower: 100,
            petCostModifiers: [new CardCostModifier("relic-emergency-core", 50)]);

        var result = CardCastExecutor.Execute(state, BossHealCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        Assert.Equal(70, result.State.BossState.Power); // 100 - 30, NOT 100 - 15
    }

    [Fact]
    public void Execute_BossActor_DeductsExactlyTheEffectiveCost()
    {
        var state = CreateTestBattleState(bossPower: 50);

        var result = CardCastExecutor.Execute(state, BossHealCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        Assert.Equal(20, result.State.BossState.Power); // 50 - 30
    }

    [Fact]
    public void Execute_BossActor_AcceptsACastAtExactlySufficientPower()
    {
        // CARD_RULES.md §3 item 2's check is ">= cost", so equality is sufficient.
        var state = CreateTestBattleState(bossPower: 30);

        var result = CardCastExecutor.Execute(state, BossHealCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        Assert.Equal(0, result.State.BossState.Power); // 30 - 30, never negative
    }

    [Fact]
    public void Execute_BossActor_RejectsACastOnePowerBelowTheCost()
    {
        var state = CreateTestBattleState(bossPower: 29);

        var result = CardCastExecutor.Execute(state, BossHealCard, CardCastActor.Boss);

        Assert.True(result.IsRejected);
        Assert.Equal(CardCastRejectionReason.InsufficientPower, result.Reason);
    }

    [Fact]
    public void Execute_BossActor_ZeroCostCard_IsValidatedAndDeductedAsZero()
    {
        // CARD_RULES.md §3.6 item 5: there is no minimum-cost rule and a cost of 0 is
        // a real cost, so a 0-cost Boss Card is castable at Boss Power 0.
        var state = CreateTestBattleState(bossPower: 0, bossHp: 4000);

        var result = CardCastExecutor.Execute(state, BossFreeHealCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        Assert.Equal(0, result.State.BossState.Power);
        Assert.Equal(4100, result.State.BossState.HP); // its 100-point self Heal landed
    }

    [Fact]
    public void Execute_BossActor_CostComesFromBossPowerNotPetPower()
    {
        // BOSS_RULES.md §4.1 items 4–5 / §4.2 item 3: the Boss spends its own Power,
        // and PetState.Power is a separate resource it never touches.
        var state = CreateTestBattleState(bossPower: 50, petPower: 100);

        var result = CardCastExecutor.Execute(state, BossHealCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        Assert.Equal(20, result.State.BossState.Power); // 50 - 30
        Assert.Equal(100, result.State.PetState.Power); // untouched
    }

    [Fact]
    public void Execute_BossActor_InsufficientBossPower_IsNotRescuedByPetPower()
    {
        // The inverse of the previous test: plenty of Pet Power, none on the Boss.
        var state = CreateTestBattleState(bossPower: 0, petPower: 100);

        var result = CardCastExecutor.Execute(state, BossHealCard, CardCastActor.Boss);

        Assert.True(result.IsRejected);
        Assert.Equal(CardCastRejectionReason.InsufficientPower, result.Reason);
    }

    // =======================================================================
    // Self effects — resolved against the casting actor's own state
    // =======================================================================

    [Fact]
    public void Execute_BossActor_SelfHeal_RestoresBossHpAndLeavesThePetAlone()
    {
        var state = CreateTestBattleState(bossPower: 50, bossHp: 3000, petHp: 400);

        var result = CardCastExecutor.Execute(state, BossHealCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);

        // 20% of the BOSS's Max HP (5000) = 1000; the PET's Max HP (1000) would give
        // 200, so the value identifies which actor the percentage was read from.
        Assert.Equal(4000, result.State.BossState.HP);
        Assert.Equal(400, result.State.PetState.HP); // opponent untouched by a self effect
    }

    [Fact]
    public void Execute_BossActor_SelfHeal_ClampsToBossMaxHp()
    {
        var state = CreateTestBattleState(bossPower: 50, bossHp: 4500);

        var result = CardCastExecutor.Execute(state, BossHealCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        Assert.Equal(BossMaxHp, result.State.BossState.HP); // clamped, overheal discarded
    }

    [Fact]
    public void Execute_BossActor_SelfHeal_AgainstADeadBoss_IsADocumentedNoOp()
    {
        // COMBAT_RULES.md §4 item 1: 0 HP is terminal, so healing a dead target is a
        // no-op — NOT a cast rejection (CARD_RULES.md §3 item 3 rejects only failed
        // validation, and a heal on a dead target is valid gameplay).
        var state = CreateTestBattleState(bossPower: 50, bossHp: 0);

        var result = CardCastExecutor.Execute(state, BossHealCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        Assert.Equal(0, result.State.BossState.HP); // unchanged, not revived
        Assert.Equal(20, result.State.BossState.Power); // the cost was still paid
    }

    [Fact]
    public void Execute_BossActor_SelfShield_GrantsTheShieldToTheBossWithTheBossAsItsSource()
    {
        var state = CreateTestBattleState(bossPower: 50);

        var result = CardCastExecutor.Execute(state, BossShieldCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        Assert.Empty(result.State.PetState.ActiveStatusEffects); // opponent untouched

        var shield = Assert.Single(result.State.BossState.ActiveStatusEffects);
        Assert.Equal("Shield", shield.Id);
        Assert.Equal(StatusEffectType.Shield, shield.Type);
        Assert.Equal(StatusEffectSource.Boss, shield.Source); // the Boss applied it
        Assert.Equal(1000.0, shield.Magnitude); // 20% of the Boss's 5000 Max HP
        Assert.Equal(StatusEffect.ShieldDepletedCondition, shield.ExpiryCondition);
    }

    [Fact]
    public void Execute_BossActor_SelfShield_RefreshesAnExistingBossShieldWithoutStacking()
    {
        // COMBAT_RULES.md §4 item 3 / GAME_STATE.md §2.3.1 item 6: one instance per
        // identity, refresh replaces the magnitude, no additive accumulation.
        var existing = StatusEffect.TriggerBased(
            "Shield",
            StatusEffectType.Shield,
            StatusEffectSource.Boss,
            120.0,
            StatusEffect.ShieldDepletedCondition);
        var state = CreateTestBattleState(bossPower: 50, bossStatusEffects: [existing]);

        var result = CardCastExecutor.Execute(state, BossShieldCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        var shield = Assert.Single(result.State.BossState.ActiveStatusEffects);
        Assert.Equal(1000.0, shield.Magnitude); // replaced, not 1120
    }

    [Fact]
    public void Execute_BossActor_SelfPower_GrantsBossPower()
    {
        var state = CreateTestBattleState(bossPower: 10);

        var result = CardCastExecutor.Execute(state, BossPowerCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        Assert.Equal(25, result.State.BossState.Power); // 10 - 10 cost, then +25
        Assert.Equal(50, result.State.PetState.Power); // Pet Power untouched
    }

    [Fact]
    public void Execute_BossActor_SelfPower_ClampsAt100()
    {
        // GAME_RULES.md §12 / GAME_STATE.md §2.4.3 item 5: the pool is 0–100 and the
        // clamp is applied where the value is written.
        var state = CreateTestBattleState(bossPower: 100);

        var result = CardCastExecutor.Execute(state, BossPowerCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        Assert.Equal(100, result.State.BossState.Power); // 100 - 10 + 25 -> clamped to 100
    }

    [Fact]
    public void Execute_BossActor_PercentMaxHp_IsInterpretedAgainstTheBossMaxHp()
    {
        // The one property that would silently pass if the branch read the wrong
        // actor: Boss MaxHP 5000 -> 20% = 1000, Pet MaxHP 1000 -> 20% = 200.
        var state = CreateTestBattleState(bossPower: 50, bossHp: 1000, petHp: 500);

        var result = CardCastExecutor.Execute(state, BossHealCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        Assert.Equal(2000, result.State.BossState.HP); // 1000 + 1000, not 1000 + 200
        Assert.Equal(500, result.State.PetState.HP);
        Assert.NotEqual(PetMaxHp, result.State.BossState.MaxHP);
    }

    // =======================================================================
    // Opponent effect — Burn onto the active Pet, with the Boss as its source
    // =======================================================================

    [Fact]
    public void Execute_BossActor_OpponentBurn_AppliesBurnToThePetWithTheBossAsItsSource()
    {
        var state = CreateTestBattleState(bossPower: 50);

        var result = CardCastExecutor.Execute(state, BossBurnCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        Assert.Empty(result.State.BossState.ActiveStatusEffects); // caster untouched

        var burn = Assert.Single(result.State.PetState.ActiveStatusEffects);
        Assert.Equal("Burn", burn.Id);
        Assert.Equal(StatusEffectType.DoT, burn.Type);
        Assert.Equal(StatusEffectSource.Boss, burn.Source); // Boss-sourced, ADR-026 item 4
        Assert.Equal(40.0, burn.Magnitude); // the authored per-tick magnitude
        Assert.Equal(2, burn.RemainingTurns); // the authored duration
        Assert.Equal(30, result.State.BossState.Power); // 50 - 20
    }

    [Fact]
    public void Execute_BossActor_OpponentBurn_RefreshesAnExistingPetBurnWithoutStacking()
    {
        var existing = StatusEffect.TurnBased(
            "Burn",
            StatusEffectType.DoT,
            StatusEffectSource.Player,
            120.0,
            5);
        var state = CreateTestBattleState(bossPower: 50, petStatusEffects: [existing]);

        var result = CardCastExecutor.Execute(state, BossBurnCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        var burn = Assert.Single(result.State.PetState.ActiveStatusEffects);
        Assert.Equal(40.0, burn.Magnitude); // replaced, not summed
        Assert.Equal(2, burn.RemainingTurns); // duration reset
        Assert.Equal(StatusEffectSource.Boss, burn.Source);
    }

    [Fact]
    public void Execute_BossActor_OneCastWithASelfAndAnOpponentEffect_WritesBothActors()
    {
        var state = CreateTestBattleState(bossPower: 20);

        var result = CardCastExecutor.Execute(state, BossPowerAndBurnCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);

        // The cost is deducted once and the self Power grant lands on what the cost
        // left: 20 - 10 = 10, then +25 = 35.
        Assert.Equal(35, result.State.BossState.Power);

        var burn = Assert.Single(result.State.PetState.ActiveStatusEffects);
        Assert.Equal("Burn", burn.Id);
        Assert.Equal(StatusEffectSource.Boss, burn.Source);
    }

    // =======================================================================
    // Boss-only effects (TASK-309 D-1) — actor-relative resolution
    // =======================================================================

    [Fact]
    public void Execute_BossActor_AtkBuff_AppliesASelfTurnBasedAtkBuffAndEmitsNoEvent()
    {
        // TASK-309 D-1 / BOSS_RULES.md §6.3.1 item 1 (Flame Burst): a SELF effect —
        // a Turn-based BuffDebuff on the BOSS's own ActiveStatusEffects[], with the
        // Card definition's identity as the instance id, the Boss as the source, the
        // authored positive magnitude (the buff's direction) and duration.
        var state = CreateTestBattleState(bossPower: 100, bossHp: 3000);

        var result = CardCastExecutor.Execute(state, BossAtkBuffCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        Assert.Equal(60, result.State.BossState.Power); // 100 - 40, the approved cost

        Assert.Empty(result.State.PetState.ActiveStatusEffects); // opponent untouched

        var buff = Assert.Single(result.State.BossState.ActiveStatusEffects);
        Assert.Equal(BossAtkBuffCard.CardDefinitionId, buff.Id);
        Assert.Equal(StatusEffectType.BuffDebuff, buff.Type);
        Assert.Equal(StatusEffectSource.Boss, buff.Source);
        Assert.Equal(50.0, buff.Magnitude); // the authored +50 percentage points
        Assert.Equal(3, buff.RemainingTurns); // the approved duration
        Assert.Equal("ATK", buff.TargetStat);
        Assert.Null(buff.ExpiryCondition);

        // COMBAT_RULES.md §5.5.1: a positive magnitude increases Boss ATK (the sign
        // is the direction), so ATK 100 becomes 150 — the buff is not negated.
        Assert.Equal(
            150,
            StatusEffectLifecycle.EffectiveBossAttack(
                100,
                result.State.BossState.ActiveStatusEffects));

        // No Power mutation on the Pet, so the cast emits nothing (its Power SPEND
        // never emits PowerChanged either — GAME_EVENTS.md §2).
        Assert.Empty(result.Events);
    }

    [Fact]
    public void Execute_BossActor_AtkBuff_RefreshesAnExistingInstanceWithoutStacking()
    {
        // GAME_STATE.md §2.3.1 item 6 / StatusEffectLifecycle.Apply: one instance per
        // identity, refreshed in place with the new magnitude and duration, not
        // summed. The identity is the Card definition's own id.
        var existing = StatusEffect.TurnBased(
            BossAtkBuffCard.CardDefinitionId,
            StatusEffectType.BuffDebuff,
            StatusEffectSource.Boss,
            20.0,
            1,
            "ATK");
        var state = CreateTestBattleState(bossPower: 100, bossStatusEffects: [existing]);

        var result = CardCastExecutor.Execute(state, BossAtkBuffCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        var buff = Assert.Single(result.State.BossState.ActiveStatusEffects);
        Assert.Equal(50.0, buff.Magnitude); // replaced, not 70
        Assert.Equal(3, buff.RemainingTurns); // duration reset
    }

    [Fact]
    public void Execute_BossActor_AtkDebuff_AppliesAnOpponentTurnBasedAtkDebuff()
    {
        // TASK-309 D-1 / BOSS_RULES.md §6.3.1 item 3 (Root): an OPPONENT effect — a
        // Turn-based BuffDebuff on the PET's ActiveStatusEffects[], with the Boss as
        // its source. The stored magnitude is the applied magnitude (30), NOT -30:
        // COMBAT_RULES.md §5.6.6 item 2 / EffectivePetATK map a reduction to
        // -|Magnitude|, so the Pet-side rule supplies the sign.
        var state = CreateTestBattleState(bossPower: 100);

        var result = CardCastExecutor.Execute(state, BossAtkDebuffCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        Assert.Equal(65, result.State.BossState.Power); // 100 - 35, the approved cost
        Assert.Empty(result.State.BossState.ActiveStatusEffects); // caster untouched

        var debuff = Assert.Single(result.State.PetState.ActiveStatusEffects);
        Assert.Equal(BossAtkDebuffCard.CardDefinitionId, debuff.Id);
        Assert.Equal(StatusEffectType.BuffDebuff, debuff.Type);
        Assert.Equal(StatusEffectSource.Boss, debuff.Source);
        Assert.Equal(30.0, debuff.Magnitude); // positive applied magnitude
        Assert.Equal(3, debuff.RemainingTurns);
        Assert.Equal("ATK", debuff.TargetStat);
        Assert.Null(debuff.ExpiryCondition);

        // The Pet-side composition maps the positive magnitude to -30, so Pet ATK 100
        // becomes 70 — the debuff is applied, not ignored as a "positive" buff.
        Assert.Equal(
            70,
            StatusEffectLifecycle.EffectiveAttack(
                100,
                Array.Empty<ATKModifier>(),
                result.State.PetState.ActiveStatusEffects));

        Assert.Empty(result.Events);
    }

    [Fact]
    public void Execute_BossActor_PowerDrain_ReducesPetPowerInstantlyAndEmitsOnePowerChanged()
    {
        // TASK-309 D-1 / BOSS_RULES.md §6.3.1 item 2 (Drain Power): an OPPONENT
        // instant effect. PetState.Power = max(0, Power - 20); no StatusEffect
        // instance; no Power transferred to the Boss; the authoritative PowerChanged
        // is the only event.
        var state = CreateTestBattleState(bossPower: 100, petPower: 50);

        var result = CardCastExecutor.Execute(state, BossPowerDrainCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        Assert.Equal(75, result.State.BossState.Power); // 100 - 25; no drained Power gained
        Assert.Equal(30, result.State.PetState.Power); // 50 - 20

        // No instance is created on either side (item 2: "Duration: None").
        Assert.Empty(result.State.PetState.ActiveStatusEffects);
        Assert.Empty(result.State.BossState.ActiveStatusEffects);

        var powerChanged = Assert.Single(result.Events);
        Assert.Equal(BattleEventType.PowerChanged, powerChanged.Type);
        Assert.Equal(PowerChangeSource.Boss, powerChanged.PowerChanged.Source);
        Assert.Equal(-20, powerChanged.PowerChanged.Delta);
        Assert.Equal(30, powerChanged.PowerChanged.Power);

        // The Boss's own Power spend is not a PowerChanged (GAME_EVENTS.md §2,
        // SIGNALR_PROTOCOL.md §3.2.24) — one mutation, one event.
        Assert.DoesNotContain(result.Events, e => e.Type == BattleEventType.CardCast);
        Assert.DoesNotContain(result.Events, e => e.Type == BattleEventType.BossCardCast);
    }

    [Fact]
    public void Execute_BossActor_PowerDrain_ReportsTheClampedDeltaWhenTheFloorAbsorbsTheDrain()
    {
        // GAME_RULES.md §12: Power is 0–100, and PowerChanged.Delta is the CLAMPED
        // change actually applied. A 20-drain against Power 10 is reported as
        // Delta -10 at Power 0, not -20.
        var state = CreateTestBattleState(bossPower: 100, petPower: 10);

        var result = CardCastExecutor.Execute(state, BossPowerDrainCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        Assert.Equal(0, result.State.PetState.Power); // floored at 0

        var powerChanged = Assert.Single(result.Events);
        Assert.Equal(PowerChangeSource.Boss, powerChanged.PowerChanged.Source);
        Assert.Equal(-10, powerChanged.PowerChanged.Delta);
        Assert.Equal(0, powerChanged.PowerChanged.Power);
    }

    [Fact]
    public void Execute_BossActor_PowerDrain_AtZeroPetPower_ChangesNothingAndEmitsNoEvent()
    {
        // A mutation that does not occur produces no event (GAME_EVENTS.md §2 item
        // 4): the floor absorbs the whole drain.
        var state = CreateTestBattleState(bossPower: 100, petPower: 0);

        var result = CardCastExecutor.Execute(state, BossPowerDrainCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        Assert.Equal(0, result.State.PetState.Power);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void Execute_BossActor_NextAtkBuff_AppliesATriggerBasedSelfAtkBuff()
    {
        // TASK-309 D-1 / BOSS_RULES.md §6.3.1 item 5 (Thunder Strike): a SELF
        // trigger-based BuffDebuff whose expiry is the documented
        // NextQualifyingAttack condition — no RemainingTurns, TargetStat ATK, the
        // Card definition as its identity.
        var state = CreateTestBattleState(bossPower: 100);

        var result = CardCastExecutor.Execute(state, BossNextAtkBuffCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        Assert.Equal(60, result.State.BossState.Power); // 100 - 40, the approved cost
        Assert.Empty(result.State.PetState.ActiveStatusEffects); // opponent untouched

        var buff = Assert.Single(result.State.BossState.ActiveStatusEffects);
        Assert.Equal(BossNextAtkBuffCard.CardDefinitionId, buff.Id);
        Assert.Equal(StatusEffectType.BuffDebuff, buff.Type);
        Assert.Equal(StatusEffectSource.Boss, buff.Source);
        Assert.Equal(50.0, buff.Magnitude);
        Assert.Equal("ATK", buff.TargetStat);
        Assert.Equal(StatusEffect.NextQualifyingAttackCondition, buff.ExpiryCondition);
        Assert.Null(buff.RemainingTurns); // the trigger-based model, not a duration
        Assert.False(buff.UsesTurnCountdown);

        // It modifies Boss ATK while it is active — 100 -> 150 — and is consumed by
        // the ConsumptionOwner's qualifying attack, not by this cast
        // (COMBAT_RULES.md §5.5.1; StatusEffectLifecycle.ConsumeNextQualifyingAttackModifiers).
        Assert.Equal(
            150,
            StatusEffectLifecycle.EffectiveBossAttack(
                100,
                result.State.BossState.ActiveStatusEffects));

        Assert.Empty(result.Events);
    }

    [Fact]
    public void Execute_BossActor_NextAtkBuff_RefreshesAnExistingInstanceWithoutStacking()
    {
        var existing = StatusEffect.TriggerBased(
            BossNextAtkBuffCard.CardDefinitionId,
            StatusEffectType.BuffDebuff,
            StatusEffectSource.Boss,
            20.0,
            StatusEffect.NextQualifyingAttackCondition,
            "ATK");
        var state = CreateTestBattleState(bossPower: 100, bossStatusEffects: [existing]);

        var result = CardCastExecutor.Execute(state, BossNextAtkBuffCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        var buff = Assert.Single(result.State.BossState.ActiveStatusEffects);
        Assert.Equal(50.0, buff.Magnitude); // replaced, not summed
        Assert.Equal(StatusEffect.NextQualifyingAttackCondition, buff.ExpiryCondition);
    }

    // =======================================================================
    // Unsupported Boss-actor effects — explicit refusal, no invented value
    // =======================================================================

    [Fact]
    public void Execute_BossActor_DamageEffect_ThrowsRatherThanDerivingDamage()
    {
        // ADR-026 D3: all five MVP abilities have BaseDamage = 0 and the Boss's only
        // damage is the ATK-gated instance. A Boss Card damage value would be a
        // magnitude no document authors (AGENTS.md §7), so it throws.
        var state = CreateTestBattleState(bossPower: 50, bossHp: 3000);

        var exception = Assert.Throws<InvalidOperationException>(
            () => CardCastExecutor.Execute(state, BossDamageCard, CardCastActor.Boss));

        Assert.Contains("Damage", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Execute_BossActor_CritEffect_ThrowsBecauseNoBossCritCarrierExists()
    {
        // GAME_STATE.md §2.4: BossState has no Crit member, so there is neither a
        // base value nor a NextAttack collection to compose or consume.
        var state = CreateTestBattleState(bossPower: 50);

        var exception = Assert.Throws<InvalidOperationException>(
            () => CardCastExecutor.Execute(state, BossCritCard, CardCastActor.Boss));

        Assert.Contains("Crit", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Execute_BossActor_UnsupportedEffect_ThrowsBeforeAnyStateIsWritten()
    {
        // The representability gate runs before the deduction, so a definition this
        // actor cannot resolve writes nothing — including the Power element that
        // precedes the unsupported one. If the guard were per-effect, the +25 grant
        // would already have been applied by the time the throw happened.
        var state = CreateTestBattleState(bossPower: 50, bossHp: 3000);

        Assert.Throws<InvalidOperationException>(
            () => CardCastExecutor.Execute(state, BossPowerThenDamageCard, CardCastActor.Boss));

        // The input state is a value type and nothing observed it: the committed
        // Power the cast would have written cannot be observed anywhere, and the
        // original state is still the one a subsequent valid cast resolves from.
        var afterThrow = CardCastExecutor.Execute(state, BossHealCard, CardCastActor.Boss);
        Assert.True(afterThrow.IsAccepted);
        Assert.Equal(20, afterThrow.State.BossState.Power); // 50 - 30, no leftover +25
    }

    // =======================================================================
    // Rejection writes nothing; a resolved cast changes only what it should
    // =======================================================================

    [Fact]
    public void Execute_BossActor_RejectedCast_ExposesNoStateAndNoEvents()
    {
        var state = CreateTestBattleState(bossPower: 0);

        var result = CardCastExecutor.Execute(state, BossHealCard, CardCastActor.Boss);

        Assert.True(result.IsRejected);
        Assert.Equal(CardCastRejectionReason.InsufficientPower, result.Reason);
        Assert.Throws<InvalidOperationException>(() => _ = result.State);
        Assert.Throws<InvalidOperationException>(() => _ = result.Events);
    }

    [Fact]
    public void Execute_BossActor_RejectedCast_LeavesTheBossAbleToCastOncePowerIsAvailable()
    {
        // CARD_RULES.md §3 item 3: a rejected cast changes nothing. The same state
        // object therefore still casts successfully once its Power is sufficient,
        // and the deduction is made from the raised value — proving the rejection
        // wrote no partial state.
        var state = CreateTestBattleState(bossPower: 29);

        var rejected = CardCastExecutor.Execute(state, BossHealCard, CardCastActor.Boss);
        Assert.True(rejected.IsRejected);

        var raised = state with
        {
            BossState = state.BossState with { Power = 30 },
        };

        var accepted = CardCastExecutor.Execute(raised, BossHealCard, CardCastActor.Boss);
        Assert.True(accepted.IsAccepted);
        Assert.Equal(0, accepted.State.BossState.Power);
    }

    [Fact]
    public void Execute_BossActor_EmitsNoEventsWhenNoPetPowerMutationOccurs()
    {
        // ADR-029 Slice 2 Scope Decision 2, refined by TASK-309: the Boss branch
        // emits no CardCast, PetSkillCast, BossCardCast, outcome event, and no
        // PowerChanged for its own Power spend or its status-effect writes. The ONLY
        // event it produces is the authoritative PowerChanged a PowerDrain effect
        // performs on the Pet's Power, so a cast without that effect emits nothing.
        var state = CreateTestBattleState(bossPower: 50);

        var result = CardCastExecutor.Execute(state, BossPowerAndBurnCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void Execute_BossActor_DoesNotAdvanceSequenceOrSpendThePlayersCastAllowance()
    {
        // GAME_STATE.md §5.1 item 9: the round's single write-back owns Sequence.
        // CARD_RULES.md §3 item 9: item 6's allowance is the Player's and is neither
        // read nor spent by a Boss cast.
        var state = CreateTestBattleState(bossPower: 50);

        var result = CardCastExecutor.Execute(state, BossHealCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        Assert.Equal(state.Sequence, result.State.Sequence);
        Assert.Equal(state.CardCastsUsedThisTurn, result.State.CardCastsUsedThisTurn);
    }

    [Fact]
    public void Execute_BossActor_CastsEvenWhenThePlayersAllowanceIsAlreadySpent()
    {
        // The strongest form of "neither read nor spent": a spent Player allowance
        // would reject a Player cast, and must not affect a Boss cast.
        var state = CreateTestBattleState(bossPower: 50) with
        {
            CardCastsUsedThisTurn = CardCastExecutor.MaxCardCastsPerTurn,
        };

        var result = CardCastExecutor.Execute(state, BossHealCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        Assert.Equal(
            CardCastExecutor.MaxCardCastsPerTurn,
            result.State.CardCastsUsedThisTurn); // unchanged, not incremented
    }

    [Fact]
    public void Execute_BossActor_DoesNotChangeSkillChargeSkillCooldownTurnOrRng()
    {
        // BOSS_RULES.md §4.3 item 3 / GAME_STATE.md §2.4.3: cooldown gating and
        // setting are Slice 3's, and this slice only guarantees it writes neither
        // SkillCharge nor SkillCooldown. Turn, RNG and the board are untouched too.
        var state = CreateTestBattleState(bossPower: 50);

        var result = CardCastExecutor.Execute(state, BossBurnCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        Assert.Equal(state.BossState.SkillCharge, result.State.BossState.SkillCharge);
        Assert.Equal(state.BossState.SkillCooldown, result.State.BossState.SkillCooldown);
        Assert.Equal(state.BossState.State, result.State.BossState.State);
        Assert.Equal(state.Turn, result.State.Turn);
        Assert.Equal(state.RngState, result.State.RngState);
        Assert.Equal(state.Combo, result.State.Combo);
        Assert.Equal(state.MatchCount, result.State.MatchCount);
        Assert.Equal(state.BoardState, result.State.BoardState);
        Assert.Equal(state.LastCommittedSwapPair, result.State.LastCommittedSwapPair);
    }

    [Fact]
    public void Execute_BossActor_DoesNotReadThePlayerLoadout()
    {
        // BOSS_RULES.md §4.2 item 1/8: there is no Boss loadout, so an empty
        // PetState.EquippedCards[] cannot block a Boss cast (whereas it rejects a
        // Player cast with CardNotInLoadout).
        var state = CreateTestBattleState(bossPower: 50, equippedCards: []);

        var result = CardCastExecutor.Execute(state, BossHealCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
    }

    [Fact]
    public void Execute_BossActor_LeavesThePlayersCostModifiersAndLoadoutIntact()
    {
        var state = CreateTestBattleState(
            bossPower: 50,
            petCostModifiers: [new CardCostModifier("relic-emergency-core", 50)],
            equippedCards: [new EquippedCardIdentity("card-heal")]);

        var result = CardCastExecutor.Execute(state, BossHealCard, CardCastActor.Boss);

        Assert.True(result.IsAccepted);
        Assert.Equal(
            state.PetState.CardCostModifiers,
            result.State.PetState.CardCostModifiers);
        Assert.Equal(state.PetState.EquippedCards, result.State.PetState.EquippedCards);
        Assert.Equal(state.PetState.MaxHP, result.State.PetState.MaxHP);
        Assert.Equal(state.PetState.Crit, result.State.PetState.Crit);
        Assert.Empty(result.State.PetState.NextAttackCritModifiers);
    }
}
