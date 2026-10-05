using GameServer.Domain.Battle;
using GameServer.Domain.Battle.Serialization;
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

        // GAME_EVENTS.md §2 item 4 / SIGNALR_PROTOCOL.md §3.2.24 item 6 (D-8): the
        // cost spend is the cast's first — and here only — Power mutation, so it is
        // reported by its own PowerChanged with `source = "card"` immediately after
        // the CardCast that records the cast itself.
        Assert.Equal(
            [BattleEventType.CardCast, BattleEventType.PowerChanged],
            result.Events.Select(e => e.Type));
        Assert.Equal("card-heal", result.Events[0].CardCast.CardId);
        Assert.Equal(20, result.Events[0].CardCast.PowerCost);

        Assert.Equal(PowerChangeSource.Card, result.Events[1].PowerChanged.Source);
        Assert.Equal(-20, result.Events[1].PowerChanged.Delta);
        Assert.Equal(30, result.Events[1].PowerChanged.Power);
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
                BattleEventType.PowerChanged,
                BattleEventType.PetSkillCast,
                BattleEventType.DamageCalculated,
                BattleEventType.DamageDealt,
                BattleEventType.DamageTaken,
            ],
            result.Events.Select(e => e.Type));

        Assert.Equal("card-inferno", result.Events[0].CardCast.CardId);
        Assert.Equal("card-inferno", result.Events[2].PetSkillCast.CardId);

        // 50 -> 10 is the cost's own mutation, reported once (D-8).
        Assert.Equal(PowerChangeSource.Card, result.Events[1].PowerChanged.Source);
        Assert.Equal(-40, result.Events[1].PowerChanged.Delta);
        Assert.Equal(10, result.Events[1].PowerChanged.Power);
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
                BattleEventType.PowerChanged,
                BattleEventType.PetSkillCast,
            ],
            result.Events.Select(e => e.Type));

        Assert.Equal("card-tidal-barrier", result.Events[0].CardCast.CardId);
        Assert.Equal("card-tidal-barrier", result.Events[2].PetSkillCast.CardId);

        // The card has no Power effect, so the cost is the only mutation (D-8).
        Assert.Equal(PowerChangeSource.Card, result.Events[1].PowerChanged.Source);
        Assert.Equal(-40, result.Events[1].PowerChanged.Delta);
        Assert.Equal(10, result.Events[1].PowerChanged.Power);
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

        // GAME_EVENTS.md §2 item 4 (D-8): the cost is Iron Fang's only Power
        // mutation — the Card has no Power effect — so exactly one PowerChanged
        // follows the CardCast.
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

        var costPowerChange = Assert.Single(
            result.Events,
            e => e.Type == BattleEventType.PowerChanged);

        Assert.Equal(PowerChangeSource.Card, costPowerChange.PowerChanged.Source);
        Assert.Equal(-40, costPowerChange.PowerChanged.Delta);
        Assert.Equal(10, costPowerChange.PowerChanged.Power);
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

        // CARD_RULES.md §3 item 6 / ADR-021: at most one Card cast per committed
        // Match-3 Turn, so the second cast that this refresh-not-stack property is
        // observed through happens in the NEXT committed Turn. The Turn advance is
        // exactly what a committed Swap's write-back performs
        // (MATCH3_RULES.md §8.1 item 1, §8.3) — the allowance is restored there
        // and nowhere else. The property under test (two casts from one source
        // refresh that element rather than stacking a second) is unchanged; only
        // the Turn the two casts occur in is now explicit.
        var nextTurn = first.State with
        {
            Turn = first.State.Turn + 1,
            CardCastsUsedThisTurn = BattleState.InitialCardCastsUsedThisTurn,
        };

        var second = CardCastExecutor.Execute(nextTurn, critOnlyCard);
        Assert.True(second.IsAccepted);

        var modifier = Assert.Single(second.State.PetState.NextAttackCritModifiers);
        Assert.Equal("card-crit-only", modifier.SourceIdentity);
        Assert.Equal(10, modifier.CritContribution);
    }

    [Fact]
    public void Execute_SecondCastInTheSameCommittedTurn_IsRejected()
    {
        // CARD_RULES.md §3 item 6 / ADR-021: a player may successfully cast at
        // most one Card during each committed Match-3 Turn. The cast is rejected
        // on the Turn's spent allowance — a cast-count constraint, not Turn
        // consumption.
        var state = CreateTestBattleState(power: 50);

        var first = CardCastExecutor.Execute(state, HealCard);
        Assert.True(first.IsAccepted);
        Assert.Equal(1, first.State.CardCastsUsedThisTurn);

        var second = CardCastExecutor.Execute(first.State, ShieldCard);

        Assert.True(second.IsRejected);
        Assert.Equal(CardCastRejectionReason.CardCastAlreadyUsedThisTurn, second.Reason);
    }

    [Fact]
    public void Execute_SecondCastInTheSameTurn_LeavesTurnAndStateUnchanged()
    {
        // CARD_RULES.md §3 items 3 and 5 / ADR-021: the rejection is a no-op. The
        // cast neither consumes a Turn nor writes the allowance, and no Power is
        // spent — a rejected cast changes nothing at all.
        var state = CreateTestBattleState(power: 50);
        var first = CardCastExecutor.Execute(state, HealCard);
        Assert.True(first.IsAccepted);

        var powerAfterFirst = first.State.PetState.Power;
        var turnAfterFirst = first.State.Turn;
        var sequenceAfterFirst = first.State.Sequence;

        var second = CardCastExecutor.Execute(first.State, ShieldCard);
        Assert.True(second.IsRejected);

        // The allowance is still exactly one spent cast, and the rejection wrote
        // no state of its own (a rejected cast produces no state at all — the
        // result exposes none).
        Assert.Equal(1, first.State.CardCastsUsedThisTurn);
        Assert.Equal(powerAfterFirst, first.State.PetState.Power);
        Assert.Equal(turnAfterFirst, first.State.Turn);
        Assert.Equal(sequenceAfterFirst, first.State.Sequence);
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
                BattleEventType.PowerChanged,
                BattleEventType.PetSkillCast,
                BattleEventType.DamageCalculated,
                BattleEventType.DamageDealt,
                BattleEventType.DamageTaken,
                BattleEventType.BattleWon,
            ],
            result.Events.Select(e => e.Type));
    }

    // =======================================================================
    // PowerChanged — the Card stage's own mutations (D-7, D-8)
    // =======================================================================

    /// <summary>
    /// A Card that both pays a non-zero cost and grants Power — the composed case
    /// <c>CARD_RULES.md</c> §3 permits and D-8 fixes the reporting shape for. No
    /// MVP-provisioned Card declares both, so the fixture declares it directly:
    /// D-8 is a general-contract rule, not a property of today's content.
    /// </summary>
    private static readonly CardDefinition CostAndPowerCard = new()
    {
        CardDefinitionId = "card-cost-and-power",
        Name = "Cost And Power",
        Category = CardCategory.Basic,
        PowerCost = 10,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Power, CardEffectValueType.Flat, 25)),
    };

    [Fact]
    public void Execute_PowerCharge_EmitsOneCardPowerChanged_WithAPositiveDelta()
    {
        // D-7: "card" identifies a Card-OWNED Power mutation, not only a cost
        // spend. Power Charge's cost is 0 (CARD_RULES.md §2), so its +25 grant is
        // the cast's only Power mutation and it is reported under source "card"
        // with the mutation's own positive sign (SIGNALR_PROTOCOL.md §3.2.24 item 1).
        var state = CreateTestBattleState(power: 10);

        var result = CardCastExecutor.Execute(state, PowerChargeCard);

        Assert.True(result.IsAccepted);
        Assert.Equal(35, result.State.PetState.Power);

        // A cost of 0 performs no mutation, so no event is emitted for it — the
        // grant is the single PowerChanged (GAME_EVENTS.md §2 item 4).
        var powerChanged = Assert.Single(
            result.Events,
            e => e.Type == BattleEventType.PowerChanged);

        Assert.Equal(PowerChangeSource.Card, powerChanged.PowerChanged.Source);
        Assert.Equal(25, powerChanged.PowerChanged.Delta);
        Assert.Equal(35, powerChanged.PowerChanged.Power);
        Assert.Equal(
            [BattleEventType.CardCast, BattleEventType.PowerChanged],
            result.Events.Select(e => e.Type));
    }

    [Fact]
    public void Execute_PowerCharge_ReportsTheClampedGrant_NotTheRequestedMagnitude()
    {
        // GAME_RULES.md §12 makes 0–100 an invariant of PetState.Power, and
        // SIGNALR_PROTOCOL.md §3.2.24 item 1 makes delta the change the mutation
        // ACTUALLY applied. A +25 grant against Power 90 therefore moves the value
        // by 10 and is reported as delta +10 at power 100 — never as +25.
        var state = CreateTestBattleState(power: 90);

        var result = CardCastExecutor.Execute(state, PowerChargeCard);

        Assert.Equal(100, result.State.PetState.Power);

        var powerChanged = Assert.Single(
            result.Events,
            e => e.Type == BattleEventType.PowerChanged);

        Assert.Equal(PowerChangeSource.Card, powerChanged.PowerChanged.Source);
        Assert.Equal(10, powerChanged.PowerChanged.Delta);
        Assert.Equal(100, powerChanged.PowerChanged.Power);
    }

    [Fact]
    public void Execute_CardThatBothPaysACostAndGrantsPower_EmitsOneEventPerMutation_InOrder()
    {
        // D-8, the documented case: one CardCast performing two sequential Power
        // mutations. GAME_EVENTS.md §2 item 4 / SIGNALR_PROTOCOL.md §3.2.24 item 6
        // require one PowerChanged per mutation, in authoritative execution order,
        // and forbid collapsing them into one net event.
        //
        //   Initial Power = 50
        //   Cost  = -10  ->  50 -> 40
        //   Effect = +25 ->  40 -> 65
        //
        // The collapsed form D-8 prohibits would be a single delta +15 at power 65.
        var state = CreateTestBattleState(
            power: 50,
            equippedCards: [new EquippedCardIdentity("card-cost-and-power")]);

        var result = CardCastExecutor.Execute(state, CostAndPowerCard);

        Assert.True(result.IsAccepted);
        Assert.Equal(65, result.State.PetState.Power);

        var powerChanges = result.Events
            .Where(e => e.Type == BattleEventType.PowerChanged)
            .Select(e => e.PowerChanged)
            .ToArray();

        Assert.Equal(2, powerChanges.Length);

        // Event 1 — the cost spend.
        Assert.Equal(PowerChangeSource.Card, powerChanges[0].Source);
        Assert.Equal(-10, powerChanges[0].Delta);
        Assert.Equal(40, powerChanges[0].Power);

        // Event 2 — the Power effect, applied to the state the cost left.
        Assert.Equal(PowerChangeSource.Card, powerChanges[1].Source);
        Assert.Equal(25, powerChanges[1].Delta);
        Assert.Equal(65, powerChanges[1].Power);

        // The order is the authoritative mutation order, and the events sit after
        // the CardCast that records the cast itself.
        Assert.Equal(
            [
                BattleEventType.CardCast,
                BattleEventType.PowerChanged,
                BattleEventType.PowerChanged,
            ],
            result.Events.Select(e => e.Type));

        // Applying the deltas in delivered order reproduces the resulting Power
        // (SIGNALR_PROTOCOL.md §3.2.24 item 6) — the property the collapsed form
        // would also satisfy, which is why the per-mutation shape is asserted above.
        Assert.Equal(result.State.PetState.Power, 50 + powerChanges.Sum(c => c.Delta));
    }

    [Fact]
    public void Execute_CardWithNoPowerMutation_EmitsNoPowerChanged()
    {
        // D-8: "If the authoritative Card execution path determines that no Power
        // mutation occurred, no PowerChanged event is emitted for that operation."
        // A Shield Card at a composed cost of 0 both spends nothing and changes
        // Power by nothing, so the cast emits only its own CardCast.
        var freeShieldCard = new CardDefinition
        {
            CardDefinitionId = "card-free-shield",
            Name = "Free Shield",
            Category = CardCategory.Basic,
            PowerCost = 0,
            LoadoutCopyLimit = 1,
            EffectDefinition = CardEffectDefinitions.Create(
                CardEffectDefinition.Create(CardEffectType.Shield, CardEffectValueType.Flat, 10)),
        };

        var state = CreateTestBattleState(
            power: 50,
            equippedCards: [new EquippedCardIdentity("card-free-shield")]);

        var result = CardCastExecutor.Execute(state, freeShieldCard);

        Assert.True(result.IsAccepted);
        Assert.Equal(50, result.State.PetState.Power);
        Assert.DoesNotContain(result.Events, e => e.Type == BattleEventType.PowerChanged);
        Assert.Equal([BattleEventType.CardCast], result.Events.Select(e => e.Type));
    }

    // ======================================================================
    // B-02 — one successful Card cast per committed Match-3 Turn
    // (CARD_RULES.md §3 item 6, MATCH3_RULES.md §8.1 item 6, ADR-021)
    // ======================================================================

    [Fact]
    public void Execute_FirstCastOfTheTurn_SucceedsAndSpendsTheAllowance()
    {
        // Scenario 1: a new Turn allows one cast, and that cast spends the allowance.
        var state = CreateTestBattleState(power: 50);
        Assert.Equal(BattleState.InitialCardCastsUsedThisTurn, state.CardCastsUsedThisTurn);

        var result = CardCastExecutor.Execute(state, HealCard);

        Assert.True(result.IsAccepted);
        Assert.Equal(1, result.State.CardCastsUsedThisTurn);
    }

    [Fact]
    public void Execute_RejectedForInsufficientPower_DoesNotSpendTheAllowance()
    {
        // Scenario 3: only a SUCCESSFUL cast spends the allowance
        // (CARD_RULES.md §3 item 3 — a rejected cast changes nothing).
        var state = CreateTestBattleState(power: 10); // Heal costs 20

        var rejected = CardCastExecutor.Execute(state, HealCard);
        Assert.True(rejected.IsRejected);
        Assert.Equal(CardCastRejectionReason.InsufficientPower, rejected.Reason);

        // The allowance is untouched: the same Turn still permits one cast.
        var stillUnspent = CardCastExecutor.Execute(
            state with { PetState = state.PetState with { Power = 50 } },
            HealCard);
        Assert.True(stillUnspent.IsAccepted);
        Assert.Equal(1, stillUnspent.State.CardCastsUsedThisTurn);
    }

    [Fact]
    public void Execute_RejectedForCardNotInLoadoutAndInvalidCard_DoNotSpendTheAllowance()
    {
        // Scenario 3, remaining rejection reasons: neither spends the allowance,
        // because the allowance is only consumed by the accepted write-back.
        var state = CreateTestBattleState(
            power: 50,
            equippedCards: [new EquippedCardIdentity("card-heal")]);

        // Not in the loadout — rejected by the loadout check.
        var notEquipped = CardCastExecutor.Execute(state, ShieldCard);
        Assert.Equal(CardCastRejectionReason.CardNotInLoadout, notEquipped.Reason);

        // An undefined category is rejected as an invalid Card. It is placed in
        // the loadout so the category check — not the loadout check, which runs
        // first (CARD_RULES.md §3 item 2's order) — is the one that reports.
        var invalidCard = new CardDefinition
        {
            CardDefinitionId = "card-heal",
            Name = "Invalid",
            Category = (CardCategory)99,
            PowerCost = 10,
            LoadoutCopyLimit = 1,
            EffectDefinition = CardEffectDefinitions.Create(
                CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.Flat, 10)),
        };
        var invalid = CardCastExecutor.Execute(state, invalidCard);
        Assert.Equal(CardCastRejectionReason.InvalidCard, invalid.Reason);

        // Neither rejection spent the allowance: a valid cast still succeeds.
        var valid = CardCastExecutor.Execute(state, HealCard);
        Assert.True(valid.IsAccepted);
        Assert.Equal(1, valid.State.CardCastsUsedThisTurn);
    }

    [Fact]
    public void Execute_AllowanceIsRestoredByTheNextCommittedTurn()
    {
        // Scenario 4: the allowance resets when the next committed Match-3 Turn
        // begins — the committed Swap's write-back (MATCH3_RULES.md §8.1 item 1)
        // restores it to the documented initial value.
        var state = CreateTestBattleState(power: 100);

        var first = CardCastExecutor.Execute(state, HealCard);
        Assert.True(first.IsAccepted);

        var blocked = CardCastExecutor.Execute(first.State, ShieldCard);
        Assert.Equal(CardCastRejectionReason.CardCastAlreadyUsedThisTurn, blocked.Reason);

        // A committed Swap's write-back is the only reset point. It is modelled
        // here exactly as SwapExecution performs it: Turn advanced and the
        // allowance returned to its initial value in the same write-back.
        var nextTurn = first.State with
        {
            Turn = first.State.Turn + 1,
            CardCastsUsedThisTurn = BattleState.InitialCardCastsUsedThisTurn,
        };

        var afterReset = CardCastExecutor.Execute(nextTurn, ShieldCard);
        Assert.True(afterReset.IsAccepted);
        Assert.Equal(1, afterReset.State.CardCastsUsedThisTurn);
    }

    [Fact]
    public void Execute_CardCastDoesNotConsumeATurn()
    {
        // Scenario 7: CARD_RULES.md §3 item 5 — a cast consumes no Turn. The
        // allowance changes; `Turn` does not.
        var state = CreateTestBattleState(power: 50);
        var turnBefore = state.Turn;

        var result = CardCastExecutor.Execute(state, HealCard);

        Assert.True(result.IsAccepted);
        Assert.Equal(turnBefore, result.State.Turn);
        Assert.Equal(1, result.State.CardCastsUsedThisTurn);

        // It does resolve as one authoritative action, so Sequence advances once.
        Assert.Equal(state.Sequence + 1, result.State.Sequence);
    }

    [Fact]
    public void Execute_CardCastAlone_ProducesNoBossResponse()
    {
        // Scenario 8: CARD_RULES.md §3 item 5 — a cast does not independently
        // trigger the normal Boss response. Nothing the cast emits targets the
        // player, and the Boss's HP/cooldown/charge are untouched by a non-damaging
        // cast, so the Step-18 pipeline is never reached (GAME_RULES.md §17).
        var state = CreateTestBattleState(power: 50);
        var bossBefore = state.BossState;

        var result = CardCastExecutor.Execute(state, HealCard);

        Assert.True(result.IsAccepted);

        // No damage was dealt to the player, and no boss skill was cast.
        Assert.DoesNotContain(result.Events, e => e.Type == BattleEventType.DamageDealt);
        Assert.DoesNotContain(result.Events, e => e.Type == BattleEventType.BossSkillCast);

        // The Boss State is carried across unchanged — the cast did not advance
        // its cooldown, charge, or HP.
        Assert.Equal(bossBefore.HP, result.State.BossState.HP);
        Assert.Equal(bossBefore.SkillCooldown, result.State.BossState.SkillCooldown);
        Assert.Equal(bossBefore.SkillCharge, result.State.BossState.SkillCharge);
    }

    [Fact]
    public void Execute_PowerChargeExploit_CannotChainFourCastsIntoADamageCard()
    {
        // REGRESSION — the TASK-191 B-02 exploit, reproduced exactly:
        //
        //     Power Charge ×4 → Damage Card → Boss dies
        //
        // with 0 Turns consumed and 0 Boss responses. Under CARD_RULES.md §3
        // item 6 / ADR-021 the chain is impossible: the FIRST Power Charge
        // succeeds and the second is rejected, so the player can never accumulate
        // the four free Power grants the loop needs inside one committed Turn.
        //
        // The Card definitions and Power Charge's 0 cost are deliberately the
        // real ones — this regression proves the rule removes the loop WITHOUT
        // changing any Card value (B-01 is a separate, undecided item).
        var damageCard = new CardDefinition
        {
            CardDefinitionId = "card-damage",
            Name = "Damage",
            Category = CardCategory.Basic,
            // Costed low enough that the spent allowance — not insufficient
            // Power — is the check that rejects it, so this regression exercises
            // the B-02 rule specifically.
            PowerCost = 20,
            LoadoutCopyLimit = 1,
            EffectDefinition = CardEffectDefinitions.Create(
                CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 150)),
        };

        var state = CreateTestBattleState(
            power: 0,
            equippedCards:
            [
                new EquippedCardIdentity("card-power-charge"),
                new EquippedCardIdentity("card-damage"),
            ]);

        // Power Charge #1 — succeeds. This is the whole of the Turn's allowance.
        var first = CardCastExecutor.Execute(state, PowerChargeCard);
        Assert.True(first.IsAccepted);
        Assert.Equal(25, first.State.PetState.Power);

        // Power Charge #2 — rejected. The old exploit relied on this succeeding.
        var second = CardCastExecutor.Execute(first.State, PowerChargeCard);
        Assert.True(second.IsRejected);
        Assert.Equal(CardCastRejectionReason.CardCastAlreadyUsedThisTurn, second.Reason);

        // Power Charge #3 and #4 — likewise rejected, for the same reason.
        var third = CardCastExecutor.Execute(first.State, PowerChargeCard);
        var fourth = CardCastExecutor.Execute(first.State, PowerChargeCard);
        Assert.Equal(CardCastRejectionReason.CardCastAlreadyUsedThisTurn, third.Reason);
        Assert.Equal(CardCastRejectionReason.CardCastAlreadyUsedThisTurn, fourth.Reason);

        // The damage Card is also rejected: the allowance is spent regardless of
        // which Card was cast first.
        var damage = CardCastExecutor.Execute(first.State, damageCard);
        Assert.True(damage.IsRejected);
        Assert.Equal(CardCastRejectionReason.CardCastAlreadyUsedThisTurn, damage.Reason);

        // The exploit's premise — free Power accumulated inside one Turn — is
        // therefore gone. Power after the only successful cast is 25, not the 100
        // the four-cast chain produced, and the Boss was never touched.
        Assert.Equal(25, first.State.PetState.Power);
        Assert.Equal(0, first.State.Turn);
        Assert.Equal(state.BossState.HP, first.State.BossState.HP);
        Assert.DoesNotContain(first.Events, e => e.Type == BattleEventType.DamageDealt);
    }

    [Fact]
    public void Execute_DamageCardCastThatKillsTheBoss_StillWinsWithinItsOneCast()
    {
        // The rule bounds how many casts a Turn allows; it does not remove a
        // legitimate one-cast win. A damage cast that takes the Boss to 0 still
        // emits BattleWon (unchanged behavior — normal victory is preserved).
        var state = CreateTestBattleState(power: 50) with
        {
            BossState = CreateTestBattleState(power: 50).BossState with { HP = 1 },
        };

        var result = CardCastExecutor.Execute(state, PetSkillCard);

        Assert.True(result.IsAccepted);
        Assert.Contains(result.Events, e => e.Type == BattleEventType.BattleWon);
    }

    [Fact]
    public void Execute_AllowanceSurvivesAStateRoundTrip()
    {
        // The spent allowance is authoritative battle state, so it must survive
        // persistence and reload: a battle re-read from the store still enforces
        // the same Turn's one-cast limit rather than granting a second cast.
        var state = CreateTestBattleState(power: 100);

        var first = CardCastExecutor.Execute(state, HealCard);
        Assert.True(first.IsAccepted);
        Assert.Equal(1, first.State.CardCastsUsedThisTurn);

        var reloaded = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(first.State));
        Assert.Equal(1, reloaded.CardCastsUsedThisTurn);

        var second = CardCastExecutor.Execute(reloaded, ShieldCard);
        Assert.True(second.IsRejected);
        Assert.Equal(CardCastRejectionReason.CardCastAlreadyUsedThisTurn, second.Reason);
    }
}
