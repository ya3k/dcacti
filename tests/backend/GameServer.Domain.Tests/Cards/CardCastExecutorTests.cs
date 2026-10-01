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
    public void Execute_IronFang_DealsDamage_IncreasesCrit_AndEmitsPetSkillCastEvent()
    {
        var state = CreateTestBattleState(power: 50);
        var initialCrit = state.PetState.Crit;

        var result = CardCastExecutor.Execute(state, IronFangCard);

        Assert.True(result.IsAccepted);
        Assert.Equal(10, result.State.PetState.Power); // 50 - 40 = 10
        Assert.True(result.State.BossState.HP < state.BossState.HP); // Damaged
        Assert.Equal(initialCrit + 10, result.State.PetState.Crit); // +10 percentage points

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
