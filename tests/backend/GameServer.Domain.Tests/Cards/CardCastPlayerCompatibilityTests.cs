using GameServer.Domain.Battle;
using GameServer.Domain.Cards;
using GameServer.Domain.Match3;
using Xunit;

namespace GameServer.Domain.Tests.Cards;

/// <summary>
/// Player regression coverage for the shared cast contract
/// (<c>CARD_RULES.md</c> §3 items 1–8, §3.6; <c>ADR-029</c> Slice 2's exit
/// condition).
///
/// <code>
/// the Player's CardCast behavior          unchanged
/// its one-per-Turn allowance              unchanged
/// its Relic cost modifiers                unchanged
/// its events and Sequence advance         unchanged
/// its rejection codes                     unchanged
/// </code>
///
/// <b>Why a second suite.</b> <see cref="CardCastExecutorTests"/> is the Player
/// path's own contract suite and is deliberately left unmodified; this file states
/// the properties that the actor-aware refactor could plausibly disturb, against
/// the <b>unchanged</b> two-argument entry point that every existing caller uses.
/// It also pins the one Player-visible consequence of the new
/// <see cref="CardCategory.BossCard"/> value: the Player path rejects it as
/// <see cref="CardCastRejectionReason.InvalidCard"/>.
///
/// Card definitions here are the documented <c>CARD_RULES.md</c> §2/§4.1 content
/// (Heal, Power Charge, Inferno) plus synthetic fixtures where a case needs one.
/// </summary>
public sealed class CardCastPlayerCompatibilityTests
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

    private static readonly CardDefinition InfernoCard = new()
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

    private static readonly CardDefinition BossCardFixture = new()
    {
        CardDefinitionId = "fixture-boss-card-heal",
        Name = "Boss Heal Fixture",
        Category = CardCategory.BossCard,
        PowerCost = 30,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.PercentMaxHp, 20)),
    };

    private static BattleState CreateTestBattleState(
        int hp = 500,
        int maxHp = 1000,
        int power = 50,
        int bossPower = 40,
        IReadOnlyList<CardCostModifier>? costModifiers = null,
        EquippedCardIdentity[]? equippedCards = null)
    {
        equippedCards ??=
        [
            new EquippedCardIdentity("card-heal"),
            new EquippedCardIdentity("card-power-charge"),
            new EquippedCardIdentity("card-inferno"),
        ];

        var baseState = BattleState.CreateWith("battle-player-compat", 555);

        return baseState with
        {
            PetState = baseState.PetState with
            {
                HP = hp,
                MaxHP = maxHp,
                Power = power,
                EquippedCards = equippedCards,
                CardCostModifiers = costModifiers?.ToArray() ?? [],
            },
            BossState = baseState.BossState with { Power = bossPower },
        };
    }

    // =======================================================================
    // The unchanged Player validation contract
    // =======================================================================

    [Fact]
    public void Execute_PlayerEntryPoint_StillRequiresTheCardInTheLoadout()
    {
        var state = CreateTestBattleState(
            equippedCards: [new EquippedCardIdentity("card-power-charge")]);

        var result = CardCastExecutor.Execute(state, HealCard);

        Assert.True(result.IsRejected);
        Assert.Equal(CardCastRejectionReason.CardNotInLoadout, result.Reason);
        Assert.Equal("CARD_NOT_IN_LOADOUT", CardCastRejectionCodes.ToContractCode(result.Reason));
    }

    [Fact]
    public void Execute_PlayerEntryPoint_StillRejectsInsufficientPower()
    {
        var state = CreateTestBattleState(power: 10); // Heal costs 20

        var result = CardCastExecutor.Execute(state, HealCard);

        Assert.True(result.IsRejected);
        Assert.Equal(CardCastRejectionReason.InsufficientPower, result.Reason);
        Assert.Equal("INSUFFICIENT_POWER", CardCastRejectionCodes.ToContractCode(result.Reason));
    }

    [Fact]
    public void Execute_PlayerEntryPoint_StillRejectsAnUndefinedCategory()
    {
        var undefined = new CardDefinition
        {
            CardDefinitionId = "card-heal",
            Name = "Undefined Category",
            Category = (CardCategory)99,
            PowerCost = 10,
            LoadoutCopyLimit = 1,
            EffectDefinition = CardEffectDefinitions.Create(
                CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.Flat, 10)),
        };
        var state = CreateTestBattleState();

        var result = CardCastExecutor.Execute(state, undefined);

        Assert.True(result.IsRejected);
        Assert.Equal(CardCastRejectionReason.InvalidCard, result.Reason);
        Assert.Equal("INVALID_CARD", CardCastRejectionCodes.ToContractCode(result.Reason));
    }

    [Fact]
    public void Execute_PlayerEntryPoint_RejectsABossCardDefinition()
    {
        // CARD_RULES.md §1.1 item 4 / §3 item 9: the Boss Card has no loadout slot and
        // no Player entitlement. The definition is placed in the loadout so the
        // CATEGORY gate is the check that reports, exactly as for an undefined
        // category above.
        var state = CreateTestBattleState(
            equippedCards: [new EquippedCardIdentity(BossCardFixture.CardDefinitionId)]);

        var result = CardCastExecutor.Execute(state, BossCardFixture);

        Assert.True(result.IsRejected);
        Assert.Equal(CardCastRejectionReason.InvalidCard, result.Reason);
    }

    // =======================================================================
    // The unchanged Player cost composition, allowance and events
    // =======================================================================

    [Fact]
    public void Execute_PlayerEntryPoint_StillAppliesPetCardCostModifiers()
    {
        // CARD_RULES.md §3.6: the Player's cast composes EffectiveCardCost from
        // PetState.CardCostModifiers[]. A 50-point reduction makes Heal cost 10, not 20.
        var state = CreateTestBattleState(
            power: 50,
            costModifiers: [new CardCostModifier("relic-emergency-core", 50)]);

        var result = CardCastExecutor.Execute(state, HealCard);

        Assert.True(result.IsAccepted);
        Assert.Equal(40, result.State.PetState.Power); // 50 - 10

        var cardCast = Assert.Single(result.Events, e => e.Type == BattleEventType.CardCast);
        Assert.Equal(10, cardCast.CardCast.PowerCost); // reported cost = deducted cost

        var powerChanged = Assert.Single(result.Events, e => e.Type == BattleEventType.PowerChanged);
        Assert.Equal(PowerChangeSource.Card, powerChanged.PowerChanged.Source);
        Assert.Equal(-10, powerChanged.PowerChanged.Delta);
        Assert.Equal(40, powerChanged.PowerChanged.Power);
    }

    [Fact]
    public void Execute_PlayerEntryPoint_StillSpendsTheAllowanceAndAdvancesSequence()
    {
        var state = CreateTestBattleState(power: 50);

        var result = CardCastExecutor.Execute(state, HealCard);

        Assert.True(result.IsAccepted);
        Assert.Equal(1, result.State.CardCastsUsedThisTurn);
        Assert.Equal(state.Sequence + 1, result.State.Sequence);
        Assert.Equal(state.Turn, result.State.Turn); // a cast consumes no Turn
    }

    [Fact]
    public void Execute_PlayerEntryPoint_StillRejectsASecondCastInTheSameTurn()
    {
        var state = CreateTestBattleState(power: 100);

        var first = CardCastExecutor.Execute(state, HealCard);
        Assert.True(first.IsAccepted);

        var second = CardCastExecutor.Execute(first.State, HealCard);

        Assert.True(second.IsRejected);
        Assert.Equal(CardCastRejectionReason.CardCastAlreadyUsedThisTurn, second.Reason);
        Assert.Equal(
            "CARD_CAST_ALREADY_USED_THIS_TURN",
            CardCastRejectionCodes.ToContractCode(second.Reason));
    }

    [Fact]
    public void Execute_PlayerEntryPoint_StillEmitsCardCastPowerChangedAndPetSkillCast()
    {
        var state = CreateTestBattleState(power: 100);

        var result = CardCastExecutor.Execute(state, InfernoCard);

        Assert.True(result.IsAccepted);
        Assert.Equal(
            [
                BattleEventType.CardCast,
                BattleEventType.PowerChanged,
                BattleEventType.PetSkillCast,
                BattleEventType.DamageCalculated,
                BattleEventType.DamageDealt,
                BattleEventType.DamageTaken,
            ],
            result.Events.Select(e => e.Type));

        Assert.Equal(60, result.State.PetState.Power); // 100 - 40
        Assert.Single(result.State.BossState.ActiveStatusEffects); // Inferno's Burn, on the Boss
        Assert.Equal(
            StatusEffectSource.Player,
            result.State.BossState.ActiveStatusEffects[0].Source);
    }

    [Fact]
    public void Execute_PlayerEntryPoint_StillEmitsPowerChangedForAPowerGrant()
    {
        var state = CreateTestBattleState(power: 10);

        var result = CardCastExecutor.Execute(state, PowerChargeCard);

        Assert.True(result.IsAccepted);
        Assert.Equal(35, result.State.PetState.Power);

        var powerChanged = Assert.Single(result.Events, e => e.Type == BattleEventType.PowerChanged);
        Assert.Equal(PowerChangeSource.Card, powerChanged.PowerChanged.Source);
        Assert.Equal(25, powerChanged.PowerChanged.Delta);
    }

    [Fact]
    public void Execute_PlayerEntryPoint_StillLeavesBossPowerUntouched()
    {
        // BOSS_RULES.md §4.1 items 4–5: the Boss's Power is spent only on its own
        // Boss Card cast, never on a player Card or Pet Skill.
        var state = CreateTestBattleState(power: 100, bossPower: 40);

        var result = CardCastExecutor.Execute(state, InfernoCard);

        Assert.True(result.IsAccepted);
        Assert.Equal(40, result.State.BossState.Power);
    }

    [Fact]
    public void Execute_PlayerEntryPoint_StillThrowsForAnEffectWithNoAuthoredMagnitude()
    {
        // Unchanged behavior: an effect stored with Undetermined carries no value, and
        // the Player branch throws rather than substituting one (DATABASE.md §1 item 9).
        var undetermined = new CardDefinition
        {
            CardDefinitionId = "card-heal",
            Name = "Undetermined",
            Category = CardCategory.Basic,
            PowerCost = 10,
            LoadoutCopyLimit = 1,
            EffectDefinition = CardEffectDefinitions.Create(
                CardEffectDefinition.Undetermined(CardEffectType.Heal)),
        };
        var state = CreateTestBattleState();

        Assert.Throws<InvalidOperationException>(() => CardCastExecutor.Execute(state, undetermined));
    }

    // =======================================================================
    // The actor-aware entry point's Player branch is the same contract
    // =======================================================================

    [Fact]
    public void Execute_ActorAwarePlayerOverload_MatchesTheTwoArgumentEntryPoint()
    {
        var state = CreateTestBattleState(power: 100);

        var legacy = CardCastExecutor.Execute(state, InfernoCard);
        var explicitPlayer = CardCastExecutor.Execute(state, InfernoCard, CardCastActor.Player);

        Assert.True(legacy.IsAccepted);
        Assert.True(explicitPlayer.IsAccepted);
        Assert.Equal(legacy.Reason, explicitPlayer.Reason);
        Assert.Equal(legacy.State.Sequence, explicitPlayer.State.Sequence);
        Assert.Equal(legacy.State.Turn, explicitPlayer.State.Turn);
        Assert.Equal(
            legacy.State.CardCastsUsedThisTurn,
            explicitPlayer.State.CardCastsUsedThisTurn);
        Assert.Equal(legacy.State.PetState.Power, explicitPlayer.State.PetState.Power);
        Assert.Equal(legacy.State.PetState.HP, explicitPlayer.State.PetState.HP);
        Assert.Equal(legacy.State.BossState.HP, explicitPlayer.State.BossState.HP);
        Assert.Equal(legacy.State.BossState.Power, explicitPlayer.State.BossState.Power);
        Assert.Equal(legacy.State.RngState, explicitPlayer.State.RngState);
        Assert.Equal(
            legacy.Events.Select(e => e.Type),
            explicitPlayer.Events.Select(e => e.Type));
    }

    [Fact]
    public void Execute_ActorAwarePlayerOverload_MatchesTheTwoArgumentEntryPointOnRejection()
    {
        var state = CreateTestBattleState(power: 10);

        var legacy = CardCastExecutor.Execute(state, HealCard);
        var explicitPlayer = CardCastExecutor.Execute(state, HealCard, CardCastActor.Player);

        Assert.True(legacy.IsRejected);
        Assert.True(explicitPlayer.IsRejected);
        Assert.Equal(legacy.Reason, explicitPlayer.Reason);
        Assert.Equal(CardCastRejectionReason.InsufficientPower, explicitPlayer.Reason);
    }
}
