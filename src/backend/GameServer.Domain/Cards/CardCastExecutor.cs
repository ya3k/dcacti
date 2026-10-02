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
        var newStatusEffects = state.PetState.ActiveStatusEffects;

        // GAME_STATE.md §2.3.4 / §5.1.2 item 1: the modifier collection is carried
        // through the cast and only a `Crit` element writes it. It is never null
        // (§2.3.4 item 5), so the state's own collection is the starting value and
        // no empty collection is substituted for it.
        var newNextAttackCritModifiers = state.PetState.NextAttackCritModifiers;

        // COMBAT_RULES.md §3.3 item 8: whether this action is an explicit owner attack
        // action that enters the Damage Pipeline — i.e. a qualifying attack. It is set
        // by the Damage arm and read once, after every effect has resolved, by the
        // consumption step. A cast with no damage instance leaves it false.
        var isQualifyingAttack = false;

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

                    // COMBAT_RULES.md §3.3 items 7–10 / GAME_STATE.md §2.3.4: a
                    // temporary Crit increase is NOT written into the base stat.
                    // §3.3 item 7 makes `Crit` the permanent/base value that a
                    // temporary modifier "is NEVER overwritten" by, precisely so
                    // that consumption can remove one source without disturbing the
                    // base (item 9). The effect therefore creates or refreshes an
                    // element of `NextAttackCritModifiers[]` instead.
                    //
                    // GAME_STATE.md §2.3.4 item 2 makes the identity per SOURCE, so
                    // re-casting this Card before the qualifying attack refreshes
                    // its own element rather than stacking a second one (§5.1.2
                    // item 1) — which is the behavior CARD_RULES.md §4.1's "for the
                    // next attack only" reading and COMBAT_RULES.md §5.2 item 2's
                    // MVP refresh default both call for.
                    //
                    // The identity is the Card definition's own identity: it is
                    // stable across battles and casts, deterministic, and never
                    // derived from a clock, a GUID, an allocation order, or an
                    // array position (§2.3.4 item 2). `DATABASE.md` §3 item 1 makes
                    // `scope = "NextAttack"` a storage member carrying no gameplay,
                    // so the scope selects no identity of its own and none is
                    // invented here.
                    newNextAttackCritModifiers = NextAttackCritModifiers.Apply(
                        newNextAttackCritModifiers,
                        new NextAttackCritModifier(
                            cardDefinition.CardDefinitionId,
                            critAmount));
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

                    // COMBAT_RULES.md §3.3 items 7–8: this Card/Pet Skill cast is an
                    // explicit owner attack action entering the Damage Pipeline, so
                    // it is a QUALIFYING ATTACK. The modifier it grants and any
                    // modifier already active for this Pet therefore participate in
                    // Effective Crit (item 7) and are consumed at this attack's
                    // first qualifying damage instance (item 8) — which is this
                    // instance.
                    //
                    // Item 7's composition: the base stat is supplied as
                    // `AttackerCrit` and the temporary contributions separately as
                    // `NextAttackCritContribution`, so the base is never written by a
                    // temporary modifier. The cap and the addition both live in the
                    // pipeline, which item 7 makes the single owner of the composed
                    // value.
                    var applicableModifiers = newNextAttackCritModifiers;
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
                            RngState: currentRngState,
                            NextAttackCritContribution: NextAttackCritModifiers.TotalContribution(applicableModifiers)),
                        ComboModifiers.Default,
                        ElementModifiers.Default);

                    currentRngState = damageResult.UpdatedRngState;

                    // COMBAT_RULES.md §3.3 items 7–8: the modifiers this instance
                    // composed against participate in its Effective Crit, and that is
                    // all this instance does with them. Consumption is NOT performed
                    // here — it happens once, below, after the whole
                    // EffectDefinition[] loop.
                    //
                    // Why: `DATABASE.md` §3 item 3 makes the stored element order
                    // "NOT semantic", so consuming inside this loop would make the
                    // outcome depend on whether a Card happens to store its Damage
                    // element before or after its Crit element (Iron Fang stores
                    // Damage first). A rule whose result depends on a non-semantic
                    // storage sequence is not deterministic against the contract, and
                    // a Card is one action — its effects belong to one attack.
                    //
                    // Consuming once after the loop also keeps "this cast grants and
                    // this cast's attack consumes" order-independent: every element's
                    // composition sees the same pre-consumption set, exactly as
                    // item 8's "all damage instances belong to the same attack"
                    // requires.
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

                    // COMBAT_RULES.md §3.3 item 8: this Card/Pet Skill cast is an
                    // explicit owner attack action that entered the Damage Pipeline,
                    // so the cast as a whole is a QUALIFYING ATTACK. The flag is
                    // recorded here and the consumption it authorizes is performed
                    // once, after the loop — see the note at the consumption site.
                    // A cast with no Damage element never sets it and therefore
                    // consumes nothing, which is item 8's "a non-damaging action does
                    // not consume" and is what lets a Crit-only Card grant a modifier
                    // that survives to the next attack.
                    isQualifyingAttack = true;

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

        // ===================================================================
        // COMBAT_RULES.md §3.3 items 8–10 / GAME_STATE.md §5.1.2 item 4 —
        // NextAttack Crit modifier consumption, once per qualifying attack.
        // ===================================================================
        // Consumption happens HERE, after every effect of the action has resolved,
        // and not inside the Damage arm above. Two contract properties require that
        // placement:
        //
        //  1. Consumption is per ATTACK, not per damage instance. Item 8: "If one
        //     qualifying attack action produces multiple damage instances, all of
        //     them belong to the same attack" and the modifier is consumed once, "at
        //     the FIRST qualifying damage instance", and "must NOT be consumed again
        //     by later damage instances belonging to the same attack". One removal
        //     after the loop is that statement: every instance of the action composes
        //     against the same pre-consumption set, and exactly one removal follows.
        //
        //  2. The outcome must not depend on the stored element order. `DATABASE.md`
        //     §3 item 3 makes the EffectDefinition[] sequence "NOT semantic", so a
        //     consumption inside the loop would behave differently for a Card storing
        //     [Damage, Crit] (Iron Fang) than for the same Card storing
        //     [Crit, Damage] — a result decided by a non-semantic storage sequence.
        //
        // Item 10: ALL applicable modifiers are consumed together, so the removal set
        // is the whole collection — the same set the composition summed. Iron Fang's
        // and Bạch Hổ's contributions are both removed by the one attack, and neither
        // is left behind because they share the attack scope.
        //
        // Item 9 / §5.1.2 item 4: consumption removes ONLY the identified elements.
        // It does not write PetState.Crit, does not touch Passive or Relic Crit, and
        // is not an arithmetic inverse. It is never expressed as assigning the
        // configured default — the DefaultCrit reset ADR-017 records as forbidden.
        //
        // Item 8: a non-damaging action does not consume, so a cast that produced no
        // damage instance consumes nothing and leaves its granted modifier active for
        // the next qualifying attack.
        if (isQualifyingAttack && newNextAttackCritModifiers.Length > 0)
        {
            var consumedIdentities = new string[newNextAttackCritModifiers.Length];
            for (var index = 0; index < newNextAttackCritModifiers.Length; index++)
            {
                consumedIdentities[index] = newNextAttackCritModifiers[index].SourceIdentity;
            }

            newNextAttackCritModifiers = NextAttackCritModifiers.Consume(
                newNextAttackCritModifiers,
                consumedIdentities);
        }

        var newPetState = state.PetState with
        {
            Power = newPower,
            HP = newHp,

            // GAME_STATE.md §2.3.4 item 9 / COMBAT_RULES.md §3.3 item 7: `Crit` is
            // deliberately NOT written here. It is the permanent/base Crit value, and
            // §3.3 item 7 makes it a value a temporary NextAttack modifier is "NEVER
            // overwritten" by. A cast that grants a Crit modifier writes only the
            // collection below; the base stat is carried across unchanged.
            ActiveStatusEffects = newStatusEffects,
            NextAttackCritModifiers = newNextAttackCritModifiers,
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
