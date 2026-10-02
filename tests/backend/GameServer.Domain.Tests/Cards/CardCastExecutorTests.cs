using GameServer.Domain.Battle;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using Xunit;

namespace GameServer.Domain.Tests.Cards;

public sealed class CardCastExecutorTests
{
    private static readonly CardDefinition HealCard = new()
    {
        CardDefinitionId = "card-heal",
        Name = "Heal",
        Category = CardCategory.Basic,
        PowerCost = 20,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.PercentMaxHp, 20)),
    };

    private static readonly CardDefinition ShieldCard = new()
    {
        CardDefinitionId = "card-shield",
        Name = "Shield",
        Category = CardCategory.Basic,
        PowerCost = 20,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Shield, CardEffectValueType.PercentMaxHp, 20)),
    };

    private static readonly CardDefinition PowerChargeCard = new()
    {
        CardDefinitionId = "card-power-charge",
        Name = "Power Charge",
        Category = CardCategory.Basic,
        PowerCost = 0,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Power, CardEffectValueType.Flat, 25)),
    };

    private static readonly CardDefinition PetSkillCard = new()
    {
        CardDefinitionId = "card-inferno",
        Name = "Inferno",
        Category = CardCategory.PetSkill,
        PowerCost = 40,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 100),
            CardEffectDefinition.Burn(CardEffectValueType.Flat, 50, 2)),
    };

    private static readonly CardDefinition TidalBarrierCard = new()
    {
        CardDefinitionId = "card-tidal-barrier",
        Name = "Tidal Barrier",
        Category = CardCategory.PetSkill,
        PowerCost = 40,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.PercentMaxHp, 20),
            CardEffectDefinition.Create(CardEffectType.Shield, CardEffectValueType.PercentMaxHp, 20)),
    };

    private static readonly CardDefinition IronFangCard = new()
    {
        CardDefinitionId = "card-iron-fang",
        Name = "Iron Fang",
        Category = CardCategory.PetSkill,
        PowerCost = 40,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 120),
            CardEffectDefinition.Crit(10, "NextAttack")),
    };

    private static BattleState CreateTestBattleState(
        int hp = 500,
        int maxHp = 1000,
        int power = 50,
        IReadOnlyList<StatusEffect>? statusEffects = null,
        EquippedCardIdentity[]? equippedCards = null)
    {
        equippedCards ??=
        [
            new EquippedCardIdentity("card-heal"),
            new EquippedCardIdentity("card-shield"),
            new EquippedCardIdentity("card-power-charge"),
            new EquippedCardIdentity("card-inferno"),
            new EquippedCardIdentity("card-tidal-barrier"),
            new EquippedCardIdentity("card-iron-fang"),
        ];

        var baseState = BattleState.CreateWith("battle-cardcast-test", 12345);

        var petState = baseState.PetState with
        {
            HP = hp,
            MaxHP = maxHp,
            Power = power,
            EquippedCards = equippedCards,
            ActiveStatusEffects = statusEffects?.ToArray() ?? [],
        };

        return baseState with
        {
            PetState = petState,
        };
    }

    [Fact]
    public void Execute_Heal_RestoresHpAndDeductsPower()
    {
        var state = CreateTestBattleState(hp: 500, maxHp: 1000, power: 50);

        var result = CardCastExecutor.Execute(state, HealCard);

        Assert.True(result.IsAccepted);
        Assert.Equal(CardCastRejectionReason.None, result.Reason);
        Assert.Equal(700, result.State.PetState.HP); // 500 + 20% of 1000 = 700
        Assert.Equal(30, result.State.PetState.Power); // 50 - 20 = 30
        Assert.Equal(1, result.State.Sequence);
        Assert.Single(result.Events);
        Assert.Equal(BattleEventType.CardCast, result.Events[0].Type);
        Assert.Equal("card-heal", result.Events[0].CardCast.CardId);
        Assert.Equal(20, result.Events[0].CardCast.PowerCost);
    }

    [Fact]
    public void Execute_Heal_ClampsToMaxHp_DiscardingOverheal()
    {
        var state = CreateTestBattleState(hp: 950, maxHp: 1000, power: 30);

        var result = CardCastExecutor.Execute(state, HealCard);

        Assert.True(result.IsAccepted);
        Assert.Equal(1000, result.State.PetState.HP); // Clamped at 1000
        Assert.Equal(10, result.State.PetState.Power);
    }

    [Fact]
    public void Execute_Heal_AtFullHp_MaintainsMaxHp()
    {
        var state = CreateTestBattleState(hp: 1000, maxHp: 1000, power: 20);

        var result = CardCastExecutor.Execute(state, HealCard);

        Assert.True(result.IsAccepted);
        Assert.Equal(1000, result.State.PetState.HP);
        Assert.Equal(0, result.State.PetState.Power);
    }

    [Fact]
    public void Execute_Shield_GrantsShieldAndDeductsPower()
    {
        var state = CreateTestBattleState(hp: 500, maxHp: 1000, power: 40);

        var result = CardCastExecutor.Execute(state, ShieldCard);

        Assert.True(result.IsAccepted);
        Assert.Equal(20, result.State.PetState.Power); // 40 - 20 = 20
        Assert.Equal(500, result.State.PetState.HP); // HP unchanged
        Assert.Single(result.State.PetState.ActiveStatusEffects);

        var shield = result.State.PetState.ActiveStatusEffects[0];
        Assert.Equal("Shield", shield.Id);
        Assert.Equal(StatusEffectType.Shield, shield.Type);
        Assert.Equal(StatusEffectSource.Player, shield.Source);
        Assert.Equal(200.0, shield.Magnitude); // 20% of 1000 = 200
        Assert.Equal(StatusEffect.ShieldDepletedCondition, shield.ExpiryCondition);
    }

    [Fact]
    public void Execute_Shield_RefreshesExistingShield_NoAdditiveStacking()
    {
        var initialShield = StatusEffect.TriggerBased(
            "Shield",
            StatusEffectType.Shield,
            StatusEffectSource.Player,
            120.0,
            StatusEffect.ShieldDepletedCondition);

        var state = CreateTestBattleState(hp: 500, maxHp: 1000, power: 40, statusEffects: [initialShield]);

        var result = CardCastExecutor.Execute(state, ShieldCard);

        Assert.True(result.IsAccepted);
        Assert.Single(result.State.PetState.ActiveStatusEffects);
        var refreshedShield = result.State.PetState.ActiveStatusEffects[0];
        Assert.Equal(200.0, refreshedShield.Magnitude); // Replaced with 200, not 320!
    }

    [Fact]
    public void Execute_PowerCharge_Gains25Power_CostsZero()
    {
        var state = CreateTestBattleState(power: 10);

        var result = CardCastExecutor.Execute(state, PowerChargeCard);

        Assert.True(result.IsAccepted);
        Assert.Equal(35, result.State.PetState.Power); // 10 + 25 = 35
        Assert.Equal(1, result.State.Sequence);
    }

    [Fact]
    public void Execute_PowerCharge_CanCastAtZeroPower()
    {
        var state = CreateTestBattleState(power: 0);

        var result = CardCastExecutor.Execute(state, PowerChargeCard);

        Assert.True(result.IsAccepted);
        Assert.Equal(25, result.State.PetState.Power); // 0 + 25 = 25
    }

    [Fact]
    public void Execute_PowerCharge_ClampsTo100Power()
    {
        var state = CreateTestBattleState(power: 90);

        var result = CardCastExecutor.Execute(state, PowerChargeCard);

        Assert.True(result.IsAccepted);
        Assert.Equal(100, result.State.PetState.Power); // Clamped at 100
    }

    [Fact]
    public void Execute_Rejects_WhenCardNotEquippedInLoadout()
    {
        var state = CreateTestBattleState(
            power: 50,
            equippedCards: [new EquippedCardIdentity("card-heal")]);

        var result = CardCastExecutor.Execute(state, ShieldCard);

        Assert.True(result.IsRejected);
        Assert.Equal(CardCastRejectionReason.CardNotInLoadout, result.Reason);
        Assert.Throws<InvalidOperationException>(() => _ = result.State);
        Assert.Throws<InvalidOperationException>(() => _ = result.Events);
    }

    [Fact]
    public void Execute_Rejects_WhenInsufficientPower()
    {
        var state = CreateTestBattleState(power: 10); // Heal requires 20

        var result = CardCastExecutor.Execute(state, HealCard);

        Assert.True(result.IsRejected);
        Assert.Equal(CardCastRejectionReason.InsufficientPower, result.Reason);
    }

    [Fact]
    public void Execute_Rejects_WhenCardIsInvalidCategory()
    {
        var invalidCard = new CardDefinition
        {
            CardDefinitionId = "card-invalid-category",
            Name = "Invalid Category",
            Category = (CardCategory)999,
            PowerCost = 10,
            LoadoutCopyLimit = 1,
            EffectDefinition = CardEffectDefinitions.Create(
                CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.Flat, 10)),
        };
        var state = CreateTestBattleState(
            power: 100,
            equippedCards: [new EquippedCardIdentity("card-invalid-category")]);

        var result = CardCastExecutor.Execute(state, invalidCard);

        Assert.True(result.IsRejected);
        Assert.Equal(CardCastRejectionReason.InvalidCard, result.Reason);
    }

    [Fact]
    public void Execute_CardCast_LeavesTurnComboMatchCountAndBoardUnchanged()
    {
        var state = CreateTestBattleState(hp: 500, maxHp: 1000, power: 50);

        var result = CardCastExecutor.Execute(state, HealCard);

        Assert.True(result.IsAccepted);
        Assert.Equal(state.Turn, result.State.Turn);
        Assert.Equal(state.Combo, result.State.Combo);
        Assert.Equal(state.MatchCount, result.State.MatchCount);
        Assert.Equal(state.RngSeed, result.State.RngSeed);
        Assert.Equal(state.RngState, result.State.RngState);
        Assert.Equal(state.LastCommittedSwapPair, result.State.LastCommittedSwapPair);
        Assert.Equal(state.BoardState, result.State.BoardState);
    }

    [Fact]
    public void Execute_Inferno_DealsDamage_AppliesBurn_AndEmitsPetSkillCastEvent()
    {
        var state = CreateTestBattleState(power: 50);

        var result = CardCastExecutor.Execute(state, PetSkillCard);

        Assert.True(result.IsAccepted);
        Assert.Equal(10, result.State.PetState.Power); // 50 - 40 = 10
        Assert.True(result.State.BossState.HP < state.BossState.HP); // Damaged
        Assert.Single(result.State.BossState.ActiveStatusEffects);

        var burn = result.State.BossState.ActiveStatusEffects[0];
        Assert.Equal("Burn", burn.Id);
        Assert.Equal(StatusEffectType.DoT, burn.Type);
        Assert.Equal(50.0, burn.Magnitude);
        Assert.Equal(2, burn.RemainingTurns);

        Assert.Equal(
            [
                BattleEventType.CardCast,
                BattleEventType.PetSkillCast,
                BattleEventType.DamageCalculated,
                BattleEventType.DamageDealt,
                BattleEventType.DamageTaken,
            ],
            result.Events.Select(e => e.Type));

        Assert.Equal("card-inferno", result.Events[0].CardCast.CardId);
        Assert.Equal("card-inferno", result.Events[1].PetSkillCast.CardId);
    }

    [Fact]
    public void Execute_TidalBarrier_Heals_AppliesShield_AndEmitsPetSkillCastEvent()
    {
        var state = CreateTestBattleState(hp: 500, maxHp: 1000, power: 50);

        var result = CardCastExecutor.Execute(state, TidalBarrierCard);

        Assert.True(result.IsAccepted);
        Assert.Equal(10, result.State.PetState.Power); // 50 - 40 = 10
        Assert.Equal(700, result.State.PetState.HP); // 500 + 20% of 1000 = 700
        Assert.Single(result.State.PetState.ActiveStatusEffects);

        var shield = result.State.PetState.ActiveStatusEffects[0];
        Assert.Equal("Shield", shield.Id);
        Assert.Equal(StatusEffectType.Shield, shield.Type);
        Assert.Equal(200.0, shield.Magnitude);

        Assert.Equal(
            [
                BattleEventType.CardCast,
                BattleEventType.PetSkillCast,
            ],
            result.Events.Select(e => e.Type));

        Assert.Equal("card-tidal-barrier", result.Events[0].CardCast.CardId);
        Assert.Equal("card-tidal-barrier", result.Events[1].PetSkillCast.CardId);
    }

    [Fact]
    public void Execute_IronFang_DealsDamage_GrantsNextAttackCritModifier_AndEmitsPetSkillCastEvent()
    {
        var state = CreateTestBattleState(power: 50);
        var initialCrit = state.PetState.Crit;

        var result = CardCastExecutor.Execute(state, IronFangCard);

        Assert.True(result.IsAccepted);
        Assert.Equal(10, result.State.PetState.Power); // 50 - 40 = 10
        Assert.True(result.State.BossState.HP < state.BossState.HP); // Damaged

        // COMBAT_RULES.md §3.3 item 7 / GAME_STATE.md §2.3.4 item 9: the base Crit
        // stat is the permanent value a temporary modifier is NEVER overwritten by,
        // so the cast must leave it exactly as it was. This is the assertion the old
        // `newCrit += critAmount` write failed.
        Assert.Equal(initialCrit, result.State.PetState.Crit);

        // Iron Fang deals 120 damage AND grants +10 percentage points for the next
        // attack, and this cast is itself a qualifying attack (§3.3 item 8), so the
        // modifier it grants is granted and consumed within this one action — it does
        // not survive to a later Swap. TASK-115's own verification list states the
        // expected behavior as "Iron Fang NextAttack Crit buff consumption on the
        // SUBSEQUENT attack", and the attack this cast performs is that attack.
        //
        // What the collection must NOT show is the modifier lingering: an unconsumed
        // Iron Fang modifier after its own damage would mean the buff applied to a
        // later attack than the one the Card's damage belongs to.
        Assert.Empty(result.State.PetState.NextAttackCritModifiers);

        Assert.Equal(
            [
                BattleEventType.CardCast,
                BattleEventType.PetSkillCast,
                BattleEventType.DamageCalculated,
                BattleEventType.DamageDealt,
                BattleEventType.DamageTaken,
            ],
            result.Events.Select(e => e.Type));
    }

    [Fact]
    public void Execute_IronFang_IsOrderIndependent_BecauseEffectOrderIsNotSemantic()
    {
        // DATABASE.md §3 item 3: the stored EffectDefinition[] order is NOT semantic.
        // Iron Fang stores [Damage, Crit]; a Card storing [Crit, Damage] must produce
        // the same committed state. Consuming inside the per-effect loop would make
        // the two differ, which is the defect this assertion exists to prevent.
        var critFirstCard = new CardDefinition
        {
            CardDefinitionId = "card-iron-fang",
            Name = "Iron Fang",
            Category = CardCategory.PetSkill,
            PowerCost = 40,
            LoadoutCopyLimit = 1,
            EffectDefinition = CardEffectDefinitions.Create(
                CardEffectDefinition.Crit(10, "NextAttack"),
                CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 120)),
        };

        var damageFirstState = CreateTestBattleState(power: 50);
        var critFirstState = CreateTestBattleState(power: 50);

        var damageFirst = CardCastExecutor.Execute(damageFirstState, IronFangCard);
        var critFirst = CardCastExecutor.Execute(critFirstState, critFirstCard);

        Assert.True(damageFirst.IsAccepted);
        Assert.True(critFirst.IsAccepted);

        // Same committed gameplay outcome either way.
        Assert.Equal(
            damageFirst.State.PetState.Crit,
            critFirst.State.PetState.Crit);
        Assert.Equal(
            damageFirst.State.PetState.NextAttackCritModifiers.Length,
            critFirst.State.PetState.NextAttackCritModifiers.Length);
        Assert.Equal(
            damageFirst.State.BossState.HP,
            critFirst.State.BossState.HP);
        Assert.Equal(
            damageFirst.State.PetState.Power,
            critFirst.State.PetState.Power);
    }

    [Fact]
    public void Execute_CritOnlyPetSkill_GrantsModifierWithoutConsumingIt()
    {
        // A Crit element with no damage instance in the same Card is not a
        // qualifying attack, so the modifier it grants stays active
        // (COMBAT_RULES.md §3.3 item 8: consumption requires an attack that enters
        // the Damage Pipeline).
        var critOnlyCard = new CardDefinition
        {
            CardDefinitionId = "card-crit-only",
            Name = "Crit Only",
            Category = CardCategory.PetSkill,
            PowerCost = 10,
            LoadoutCopyLimit = 1,
            EffectDefinition = CardEffectDefinitions.Create(
                CardEffectDefinition.Crit(10, "NextAttack")),
        };

        var state = CreateTestBattleState(power: 50) with
        {
            PetState = CreateTestBattleState(power: 50).PetState with
            {
                EquippedCards = [new EquippedCardIdentity("card-crit-only")],
            },
        };

        var result = CardCastExecutor.Execute(state, critOnlyCard);

        Assert.True(result.IsAccepted);
        Assert.Equal(PetState.DefaultCrit, result.State.PetState.Crit);

        var modifier = Assert.Single(result.State.PetState.NextAttackCritModifiers);
        Assert.Equal("card-crit-only", modifier.SourceIdentity);
        Assert.Equal(10, modifier.CritContribution);
    }

    [Fact]
    public void Execute_IronFangTwice_RefreshesTheSameSourceRatherThanStacking()
    {
        // GAME_STATE.md §5.1.2 item 1: two elements with the same SourceIdentity are
        // never observable in a committed state — a repeat application from one
        // source refreshes that element instead of appending a second one.
        var critOnlyCard = new CardDefinition
        {
            CardDefinitionId = "card-crit-only",
            Name = "Crit Only",
            Category = CardCategory.PetSkill,
            PowerCost = 10,
            LoadoutCopyLimit = 1,
            EffectDefinition = CardEffectDefinitions.Create(
                CardEffectDefinition.Crit(10, "NextAttack")),
        };

        var state = CreateTestBattleState(power: 50) with
        {
            PetState = CreateTestBattleState(power: 50).PetState with
            {
                EquippedCards = [new EquippedCardIdentity("card-crit-only")],
            },
        };

        var first = CardCastExecutor.Execute(state, critOnlyCard);
        Assert.True(first.IsAccepted);

        var second = CardCastExecutor.Execute(first.State, critOnlyCard);
        Assert.True(second.IsAccepted);

        var modifier = Assert.Single(second.State.PetState.NextAttackCritModifiers);
        Assert.Equal("card-crit-only", modifier.SourceIdentity);
        Assert.Equal(10, modifier.CritContribution);
    }

    [Fact]
    public void Execute_TwoDifferentSources_CoexistAsTwoModifiers()
    {
        // COMBAT_RULES.md §3.3 item 10: different sources coexist and stack
        // additively. Iron Fang's Card identity and another source's identity are
        // two distinct elements, so both apply to the same attack and each is
        // individually removable.
        var otherCritSource = new CardDefinition
        {
            CardDefinitionId = "card-other-crit",
            Name = "Other Crit",
            Category = CardCategory.PetSkill,
            PowerCost = 10,
            LoadoutCopyLimit = 1,
            EffectDefinition = CardEffectDefinitions.Create(
                CardEffectDefinition.Crit(10, "NextAttack")),
        };

        var state = CreateTestBattleState(power: 50) with
        {
            PetState = CreateTestBattleState(power: 50).PetState with
            {
                EquippedCards =
                [
                    new EquippedCardIdentity("card-other-crit"),
                    new EquippedCardIdentity("card-crit-only"),
                ],
                NextAttackCritModifiers =
                [
                    new NextAttackCritModifier("card-crit-only", 10),
                ],
            },
        };

        var result = CardCastExecutor.Execute(state, otherCritSource);

        Assert.True(result.IsAccepted);

        // `card-other-crit` has no Damage element, so it grants without consuming —
        // both sources are now active, and neither replaced the other.
        Assert.Equal(2, result.State.PetState.NextAttackCritModifiers.Length);
        Assert.Contains(
            result.State.PetState.NextAttackCritModifiers,
            modifier => modifier.SourceIdentity == "card-crit-only" && modifier.CritContribution == 10);
        Assert.Contains(
            result.State.PetState.NextAttackCritModifiers,
            modifier => modifier.SourceIdentity == "card-other-crit" && modifier.CritContribution == 10);
    }

    [Fact]
    public void Execute_PetSkillCast_TerminalDamage_EmitsBattleWonEvent()
    {
        var state = CreateTestBattleState(power: 50) with
        {
            BossState = CreateTestBattleState(power: 50).BossState with { HP = 1 },
        };

        var result = CardCastExecutor.Execute(state, PetSkillCard);

        Assert.True(result.IsAccepted);
        Assert.Equal(0, result.State.BossState.HP);
        Assert.Equal(
            [
                BattleEventType.CardCast,
                BattleEventType.PetSkillCast,
                BattleEventType.DamageCalculated,
                BattleEventType.DamageDealt,
                BattleEventType.DamageTaken,
                BattleEventType.BattleWon,
            ],
            result.Events.Select(e => e.Type));
    }
}
