using GameServer.Domain.Battle;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;

namespace GameServer.Domain.Cards;

/// <summary>
/// Executes a Card or Pet Skill cast against authoritative battle state (<c>CARD_RULES.md</c> §2, §3, §4).
/// </summary>
public static class CardCastExecutor
{
    /// <summary>
    /// Validates and applies a Card or Pet Skill cast.
    /// </summary>
    /// <param name="state">The authoritative battle state before the cast.</param>
    /// <param name="cardDefinition">The definition of the Card being cast.</param>
    /// <returns>The committed outcome on success, or a rejection outcome on validation failure.</returns>
    public static CardCastExecutionResult Execute(
        BattleState state,
        CardDefinition cardDefinition)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(cardDefinition);

        // 1. Verify Card is in the active Pet's equipped cards snapshot (CARD_RULES.md §3 item 2)
        var isEquipped = false;
        var equippedCards = state.PetState.EquippedCards;
        if (equippedCards is not null)
        {
            for (var i = 0; i < equippedCards.Length; i++)
            {
                if (string.Equals(equippedCards[i].Value, cardDefinition.CardDefinitionId, StringComparison.Ordinal))
                {
                    isEquipped = true;
                    break;
                }
            }
        }

        if (!isEquipped)
        {
            return CardCastExecutionResult.Rejected(CardCastRejectionReason.CardNotInLoadout);
        }

        // 2. Verify Card category is Basic or PetSkill (CARD_RULES.md §1, §3, §4)
        if (cardDefinition.Category != CardCategory.Basic && cardDefinition.Category != CardCategory.PetSkill)
        {
            return CardCastExecutionResult.Rejected(CardCastRejectionReason.InvalidCard);
        }

        // 3. Verify sufficient Power (CARD_RULES.md §3 item 2)
        if (state.PetState.Power < cardDefinition.PowerCost)
        {
            return CardCastExecutionResult.Rejected(CardCastRejectionReason.InsufficientPower);
        }

        // 4. Deduct Power cost
        var newPower = state.PetState.Power - cardDefinition.PowerCost;
        var newHp = state.PetState.HP;
        var newCrit = state.PetState.Crit;
        var newStatusEffects = state.PetState.ActiveStatusEffects;
        var bossState = state.BossState;
        var currentRngState = state.RngState;

        var events = new List<BattleEvent>
        {
            BattleEvent.CreateCardCast(cardDefinition.CardDefinitionId, cardDefinition.PowerCost),
        };

        if (cardDefinition.Category == CardCategory.PetSkill)
        {
            events.Add(BattleEvent.CreatePetSkillCast(cardDefinition.CardDefinitionId));
        }

        // 5. Apply effects from structured EffectDefinition[]
        for (var i = 0; i < cardDefinition.EffectDefinition.Count; i++)
        {
            var effect = cardDefinition.EffectDefinition[i];
            var value = effect.Value ?? throw new InvalidOperationException($"Effect {effect.EffectType} is missing a magnitude value.");

            switch (effect.EffectType)
            {
                case CardEffectType.Heal:
                {
                    var healAmount = effect.ValueType switch
                    {
                        CardEffectValueType.PercentMaxHp => (state.PetState.MaxHP * value) / 100,
                        CardEffectValueType.Flat => value,
                        _ => throw new InvalidOperationException($"Unsupported valueType {effect.ValueType} for Heal effect."),
                    };
                    newHp = Math.Min(newHp + healAmount, state.PetState.MaxHP);
                    break;
                }

                case CardEffectType.Shield:
                {
                    var shieldAmount = effect.ValueType switch
                    {
                        CardEffectValueType.PercentMaxHp => (state.PetState.MaxHP * value) / 100,
                        CardEffectValueType.Flat => value,
                        _ => throw new InvalidOperationException($"Unsupported valueType {effect.ValueType} for Shield effect."),
                    };
                    var shield = StatusEffect.TriggerBased(
                        "Shield",
                        StatusEffectType.Shield,
                        StatusEffectSource.Player,
                        shieldAmount,
                        StatusEffect.ShieldDepletedCondition);
                    newStatusEffects = StatusEffectLifecycle.ApplyShield(newStatusEffects, shield);
                    break;
                }

                case CardEffectType.Power:
                {
                    var powerAmount = effect.ValueType switch
                    {
                        CardEffectValueType.Flat => value,
                        _ => throw new InvalidOperationException($"Unsupported valueType {effect.ValueType} for Power effect."),
                    };
                    newPower = Math.Clamp(newPower + powerAmount, 0, 100);
                    break;
                }

                case CardEffectType.Crit:
                {
                    var critAmount = effect.ValueType switch
                    {
                        CardEffectValueType.PercentagePoints => value,
                        CardEffectValueType.Flat => value,
                        _ => throw new InvalidOperationException($"Unsupported valueType {effect.ValueType} for Crit effect."),
                    };
                    newCrit += critAmount;
                    break;
                }

                case CardEffectType.Burn:
                {
                    var burnDamage = effect.ValueType switch
                    {
                        CardEffectValueType.Flat => value,
                        _ => throw new InvalidOperationException($"Unsupported valueType {effect.ValueType} for Burn effect."),
                    };
                    var duration = effect.Duration ?? 2;
                    var burn = StatusEffect.TurnBased(
                        "Burn",
                        StatusEffectType.DoT,
                        StatusEffectSource.Player,
                        burnDamage,
                        duration);
                    bossState = bossState with
                    {
                        ActiveStatusEffects = StatusEffectLifecycle.Apply(bossState.ActiveStatusEffects, burn),
                    };
                    break;
                }

                case CardEffectType.Damage:
                {
                    var flatDamage = effect.ValueType switch
                    {
                        CardEffectValueType.Flat => value,
                        _ => throw new InvalidOperationException($"Unsupported valueType {effect.ValueType} for Damage effect."),
                    };

                    var damageResult = DamagePipeline.Calculate(
                        new DamagePipeline.DamageInputs(
                            Attack: flatDamage,
                            BaseDamagePool: 0,
                            Combo: 1,
                            AttackerElement: state.PetState.Element,
                            DefenderElement: bossState.Element,
                            DefenderDefense: bossState.DEF,
                            DefenderHp: bossState.HP,
                            Source: DamageParty.Player,
                            Target: DamageParty.Boss,
                            DefenderShieldPool: StatusEffectLifecycle.ShieldPool(bossState.ActiveStatusEffects),
                            AttackerCrit: state.PetState.Crit,
                            RngState: currentRngState),
                        ComboModifiers.Default,
                        ElementModifiers.Default);

                    currentRngState = damageResult.UpdatedRngState;
                    bossState = bossState with { HP = damageResult.TargetHp };
                    if (damageResult.RemainingShieldPool == 0)
                    {
                        bossState = bossState with
                        {
                            ActiveStatusEffects = StatusEffectLifecycle.RemoveDepletedShield(bossState.ActiveStatusEffects),
                        };
                    }
                    else if (damageResult.AbsorbedDamage > 0)
                    {
                        bossState = bossState with
                        {
                            ActiveStatusEffects = StatusEffectLifecycle.ApplyShield(
                                bossState.ActiveStatusEffects,
                                StatusEffect.TriggerBased(
                                    "Shield",
                                    StatusEffectType.Shield,
                                    StatusEffectSource.Player,
                                    damageResult.RemainingShieldPool,
                                    StatusEffect.ShieldDepletedCondition)),
                        };
                    }

                    events.Add(BattleEvent.ForDamageCalculated(damageResult.Calculation));
                    events.Add(BattleEvent.ForDamageDealt(damageResult.DamageDealt));
                    events.Add(BattleEvent.ForDamageTaken(damageResult.DamageTaken));

                    if (bossState.HP == 0)
                    {
                        events.Add(BattleEvent.ForBattleWon(bossState.HP, newHp));
                    }
                    break;
                }

                default:
                    throw new InvalidOperationException($"Unsupported effectType {effect.EffectType} for Card cast.");
            }
        }

        var newPetState = state.PetState with
        {
            Power = newPower,
            HP = newHp,
            Crit = newCrit,
            ActiveStatusEffects = newStatusEffects,
        };

        var committedState = state with
        {
            Sequence = state.Sequence + 1,
            PetState = newPetState,
            BossState = bossState,
            RngState = currentRngState,
        };

        return CardCastExecutionResult.Committed(committedState, events);
    }
}
