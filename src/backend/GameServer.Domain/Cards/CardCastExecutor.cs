using GameServer.Domain.Battle;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;

namespace GameServer.Domain.Cards;
/// <summary>
/// Executes a Card, Pet Skill or Boss Card cast against authoritative battle state
/// (<c>CARD_RULES.md</c> §2, §3, §4, §1.1; <c>BOSS_RULES.md</c> §4.2).
///
/// <b>This is the shared cast contract, and there is exactly one of it.</b>
/// <c>CARD_RULES.md</c> §3 item 9 owns the actor-relative split: the
/// <i>shared</i> contract — the validation order, the <c>EffectiveCardCost</c>
/// composition (§3.6) and the effect-resolution semantics — serves both actors,
/// while §3 item 6's one-cast-per-committed-Turn allowance, §3 item 4's Relic
/// trigger step, §3.6's Relic <c>CardCost</c> modifiers and §1's
/// loadout/ownership model are the Player's alone. Each actor is served by one
/// branch of this type, selected by an explicit <see cref="CardCastActor"/>, so no
/// second executor, dispatcher, or duplicated effect-resolution path exists
/// (<c>ADR-029</c> §2.3 items 8–9 and §5, <c>AGENTS.md</c> §9).
///
/// <code>
/// Execute(state, card)                 → Execute(state, card, CardCastActor.Player)
/// Execute(state, card, Player)         → the pre-existing Player cast, unchanged
/// Execute(state, card, Boss)           → a Boss Card cast against BossState
/// </code>
///
/// <b>The two entry points differ in more than their arguments.</b> The Player cast
/// is a committed action outside the Match-3 Turn: it advances <c>Sequence</c>,
/// spends the Turn's allowance, and emits <c>CardCast</c>/<c>PetSkillCast</c>/
/// <c>PowerChanged</c>/damage/outcome events. The Boss cast is resolved inside the
/// round's Boss response phase, so it advances no <c>Sequence</c>, spends no
/// allowance, and emits no event: its result is the state the round's single
/// write-back carries (<c>GAME_STATE.md</c> §5.1 item 9, <c>CARD_RULES.md</c> §3
/// item 9, <c>ADR-029</c>).
/// </summary>
public static class CardCastExecutor
{
    /// <summary>
    /// How many Card casts one committed Match-3 Turn allows
    /// (<c>CARD_RULES.md</c> §3 item 6: "at most one Card during each committed
    /// Match-3 Turn", <c>ADR-021</c>).
    ///
    /// It is a cast-count limit, not a Turn cost: the allowance is spent by a
    /// successful cast and restored by the next committed Swap's write-back.
    ///
    /// <b>It is the Player's allowance.</b> <c>CARD_RULES.md</c> §3 item 9 and
    /// §1.1 item 4 keep item 6 out of the shared contract, so a Boss Card cast
    /// neither reads nor spends it; the Boss's cast cadence is bounded by its own
    /// Power, its cooldown and its action policy (<c>BOSS_RULES.md</c> §4.3).
    /// </summary>
    public const int MaxCardCastsPerTurn = 1;

    /// <summary>
    /// Validates and applies a Card or Pet Skill cast by the Player
    /// (<c>CARD_RULES.md</c> §3 items 1–8).
    ///
    /// This is the pre-existing Player entry point, kept unchanged so that no
    /// caller and no existing test needs editing: it is exactly
    /// <see cref="Execute(BattleState, CardDefinition, CardCastActor)"/> with
    /// <see cref="CardCastActor.Player"/>.
    /// </summary>
    /// <param name="state">The authoritative battle state before the cast.</param>
    /// <param name="cardDefinition">The definition of the Card being cast.</param>
    /// <returns>The committed outcome on success, or a rejection outcome on validation failure.</returns>
    public static CardCastExecutionResult Execute(
        BattleState state,
        CardDefinition cardDefinition) =>
        Execute(state, cardDefinition, CardCastActor.Player);

    /// <summary>
    /// Validates and applies a cast by the named acting side — the shared cast
    /// contract of <c>CARD_RULES.md</c> §3 item 9 (<c>BOSS_RULES.md</c> §4.2
    /// item 2, <c>ADR-029</c> §2.3 item 9).
    ///
    /// <b><see cref="CardCastActor.Player"/></b> runs §3's Player contract
    /// unchanged: the battle loadout check, the <c>Basic</c>/<c>PetSkill</c>
    /// category gate, the <c>PetState.CardCostModifiers[]</c> cost composition,
    /// <c>PetState.Power</c> sufficiency, the committed Turn's one-cast allowance,
    /// deduction from <c>PetState.Power</c>, actor-relative effect resolution, and
    /// the cast's own events, allowance spend and <c>Sequence</c> advance.
    ///
    /// <b><see cref="CardCastActor.Boss"/></b> runs the same contract against the
    /// Boss's own state (<c>BOSS_RULES.md</c> §4.2, §4.1): the definition must be
    /// the Boss's own <see cref="CardCategory.BossCard"/>, the cost is composed
    /// with an <b>empty</b> modifier set, validated against
    /// <c>BossState.Power</c> and deducted from it, and effects resolve with the
    /// Boss's own state as <i>self</i> and the active Pet as <i>opponent</i>. It
    /// reads and writes no Player loadout, allowance, Relic or Pet cost modifier,
    /// emits no event, and advances no <c>Sequence</c>.
    ///
    /// <b>A rejected cast of either actor changes nothing</b> (<c>CARD_RULES.md</c>
    /// §3 item 3): the returned result carries no state and no events, the input
    /// state is untouched, and no Power is spent.
    /// </summary>
    /// <param name="state">The authoritative battle state before the cast.</param>
    /// <param name="cardDefinition">The definition of the Card being cast.</param>
    /// <param name="actor">Which side is casting.</param>
    /// <returns>The committed outcome on success, or a rejection outcome on validation failure.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="state"/> or <paramref name="cardDefinition"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="actor"/> is not a <see cref="CardCastActor"/> member: an
    /// undefined acting side names no document's cast contract, so it is rejected
    /// rather than defaulted to the Player's.
    /// </exception>
    public static CardCastExecutionResult Execute(
        BattleState state,
        CardDefinition cardDefinition,
        CardCastActor actor)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(cardDefinition);

        return actor switch
        {
            CardCastActor.Player => ExecutePlayerCast(state, cardDefinition),
            CardCastActor.Boss => ExecuteBossCast(state, cardDefinition),
            _ => throw new ArgumentOutOfRangeException(
                nameof(actor),
                actor,
                "The cast actor must name a documented acting side (CARD_RULES.md §3 "
                + "item 9): Player or Boss. An undefined value names no side's cast "
                + "contract and must not be defaulted to the Player's (AGENTS.md §7)."),
        };
    }

    /// <summary>
    /// The Player branch of the shared cast contract — <c>CARD_RULES.md</c> §3
    /// items 1–8, unchanged.
    /// </summary>
    private static CardCastExecutionResult ExecutePlayerCast(
        BattleState state,
        CardDefinition cardDefinition)
    {
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

        var effectiveCost = EffectiveCardCost.Compose(cardDefinition.PowerCost, state.PetState.CardCostModifiers);

        // 3. Verify sufficient Power (CARD_RULES.md §3 item 2)
        if (state.PetState.Power < effectiveCost)
        {
            return CardCastExecutionResult.Rejected(CardCastRejectionReason.InsufficientPower);
        }

        // 4. Verify the current committed Match-3 Turn's one-cast allowance is
        //    unspent (CARD_RULES.md §3 item 6, ADR-021).
        //
        // This is a CAST-COUNT constraint, not Turn consumption: the cast still
        // consumes no Turn (item 5) and begins no Turn (MATCH3_RULES.md §8.1
        // item 5). The allowance is restored only by the committed Swap's
        // write-back, which is the same write-back that advances `Turn`
        // (SwapExecution, §8.3).
        //
        // The check is placed AFTER every other validation so that a cast which
        // would have been rejected for another reason reports that reason: only a
        // cast the Turn would otherwise have accepted consumes the allowance
        // (§3 item 3 — a rejected cast changes nothing, including this counter).
        if (state.CardCastsUsedThisTurn >= MaxCardCastsPerTurn)
        {
            return CardCastExecutionResult.Rejected(CardCastRejectionReason.CardCastAlreadyUsedThisTurn);
        }

        // 5. Deduct Power cost
        var newPower = state.PetState.Power - effectiveCost;
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
            BattleEvent.CreateCardCast(cardDefinition.CardDefinitionId, effectiveCost),
        };

        if (effectiveCost > 0)
        {
            events.Add(BattleEvent.ForPowerChanged(new PowerChangedEvent(
                PowerChangeSource.Card,
                -effectiveCost,
                newPower)));
        }

        if (cardDefinition.Category == CardCategory.PetSkill)
        {
            events.Add(BattleEvent.CreatePetSkillCast(cardDefinition.CardDefinitionId));
        }

        // 6. Apply effects from structured EffectDefinition[]
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
                    var healed = Combat.HealResolution.ApplyPetHeal(
                        state.PetState with { HP = newHp },
                        healAmount,
                        bossState.ActiveStatusEffects);
                    newHp = healed.HP;
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
                    var before = newPower;
                    newPower = Math.Clamp(newPower + powerAmount, 0, 100);
                    var delta = newPower - before;
                    if (delta != 0)
                    {
                        events.Add(BattleEvent.ForPowerChanged(new PowerChangedEvent(
                            PowerChangeSource.Card,
                            delta,
                            newPower)));
                    }
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
                            // COMBAT_RULES.md §3.2 / §5.5.6: the mitigation input is
                            // the Boss's EFFECTIVE DEF (TASK-299), so an active
                            // Boss DEF-Gem buff reduces this Card's damage too — it
                            // is a damage instance against the Boss in the buff's
                            // covered window.
                            DefenderDefense: StatusEffectLifecycle.EffectiveBossDefense(
                                bossState.DEF,
                                bossState.ActiveStatusEffects),
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

            // CARD_RULES.md §3 item 6 / ADR-021: this cast is the committed Turn's
            // one successful cast, so the allowance is now spent. The counter is
            // written here — the single write-back for an accepted cast — and is
            // never written on any rejection path above, which is what makes a
            // rejected cast consume nothing.
            //
            // `Turn` is deliberately NOT written: a Card cast consumes no Turn
            // (§3 item 5), so this transition changes the allowance only.
            CardCastsUsedThisTurn = state.CardCastsUsedThisTurn + 1,
            PetState = newPetState,
            BossState = bossState,
            RngState = currentRngState,
        };

        return CardCastExecutionResult.Committed(committedState, events);
    }

    /// <summary>
    /// The Boss branch of the shared cast contract — a Boss Card cast
    /// (<c>BOSS_RULES.md</c> §4.1, §4.2; <c>CARD_RULES.md</c> §1.1, §3 item 9).
    ///
    /// <code>
    /// category gate                 BossCard only, else INVALID_CARD
    ///         ↓
    /// EffectiveCardCost             composed with an EMPTY modifier set
    ///         ↓
    /// BossState.Power ≥ cost        else INSUFFICIENT_POWER
    ///         ↓
    /// representability gate         unsupported effects throw, nothing is written
    ///         ↓
    /// deduct cost from BossState.Power
    ///         ↓
    /// resolve effects               self → BossState, opponent → PetState
    /// </code>
    ///
    /// <b>What this branch deliberately does not do.</b> It reads no Player
    /// loadout (<c>CardNotInLoadout</c> is not a Boss rejection), reads and spends
    /// no one-cast allowance (<c>CARD_RULES.md</c> §3 item 6 is Player-only,
    /// §3 item 9), runs no Relic trigger (§3 item 4), composes no Relic
    /// <c>CardCost</c> modifier (§3.6's modifiers are the Pet-state carrier of a
    /// Relic effect and Relics are Player-only, <c>ADR-029</c> §2.4 item 11),
    /// advances no <c>Sequence</c> (<c>GAME_STATE.md</c> §5.1 item 9 — the round's
    /// single write-back owns it), writes neither <c>SkillCharge</c> nor
    /// <c>SkillCooldown</c> (<c>BOSS_RULES.md</c> §4.3 item 3 is the policy's
    /// concern, <c>ADR-029</c> Slice 3), and emits no event at all: the
    /// <c>BossCardCast</c> event is Slice 3's, and <c>CardCast</c>/<c>PetSkillCast</c>
    /// are the Player's (<c>GAME_EVENTS.md</c> §2, <c>CARD_RULES.md</c> §6).
    /// </summary>
    private static CardCastExecutionResult ExecuteBossCast(
        BattleState state,
        CardDefinition cardDefinition)
    {
        // 1. Actor identity and category. A Boss cast resolves the Boss's own Boss
        //    Card and nothing else: BOSS_RULES.md §4.2 item 1 gives a Boss exactly
        //    one authored Boss Card with no loadout, no equip step and no ownership
        //    row, so the category is the identity this layer can check — there is no
        //    Boss Card lookup list to consult, and none is invented here (ADR-029
        //    §2.5 item 15 leaves the persistence carrier undefined). Item 8's
        //    "no Player entitlement" and CARD_RULES.md §1.1 item 4 are the other
        //    direction of the same gate: a Boss never casts a Basic or Pet Skill
        //    Card. Both directions reject as INVALID_CARD, the reason that already
        //    means "not a category this actor may cast" — no new rejection reason
        //    and no wire change.
        if (cardDefinition.Category != CardCategory.BossCard)
        {
            return CardCastExecutionResult.Rejected(CardCastRejectionReason.InvalidCard);
        }

        // 2. EffectiveCardCost with an EMPTY modifier set (CARD_RULES.md §3 item 9,
        //    BOSS_RULES.md §4.2 item 3). PetState.CardCostModifiers[] is the
        //    Pet-state carrier of a Relic CardCost effect (GAME_STATE.md §2.3.5) and
        //    Relics are Player-only, so no modifier participates and the composed
        //    cost equals the authored PowerCost. The formula, the truncation rule and
        //    §3.6 item 7's single-calculation point are unchanged: this value is what
        //    validation, deduction and (in Slice 3) reporting all read.
        var effectiveCost = EffectiveCardCost.Compose(
            cardDefinition.PowerCost,
            Array.Empty<CardCostModifier>());

        // 3. Power sufficiency against the Boss's OWN resource (BOSS_RULES.md §4.2
        //    item 3, §4.1 items 1–2/5, GAME_STATE.md §2.4.3 item 4). PetState.Power
        //    is a separate resource and is neither read nor charged here.
        if (state.BossState.Power < effectiveCost)
        {
            return CardCastExecutionResult.Rejected(CardCastRejectionReason.InsufficientPower);
        }

        // 4. Representability gate — BEFORE the deduction, so a definition this
        //    actor's contract cannot resolve writes nothing at all.
        RequireRepresentableBossEffects(cardDefinition);

        // 5. Deduct the cost from BossState.Power — CARD_RULES.md §3 item 4's
        //    "Deduct Cost from Power → Apply Effect" order, applied to the Boss's
        //    carrier. Validation above guarantees the result is not negative, and the
        //    0–100 range is the documented invariant either way (GAME_STATE.md
        //    §2.4.3 items 1 and 5).
        var bossState = state.BossState with
        {
            Power = state.BossState.Power - effectiveCost,
        };

        // The active Pet is only written by opponent effects; it starts as the
        // committed state's own value and is otherwise carried across unchanged.
        var petState = state.PetState;

        // 6. Resolve effects relative to the caster and the opponent
        //    (BOSS_RULES.md §4.2 item 4, ADR-026 item 4 unchanged): an effect a Boss
        //    Card applies to the Boss is a SELF effect against BossState, and an
        //    effect it applies to the active Pet is an OPPONENT effect against
        //    PetState. Neither crosses entities by accident, and PercentMaxHp is
        //    interpreted against the CASTING actor's MaxHP (CARD_RULES.md §2/§4.1's
        //    proportion semantics, applied to the actor whose effect it is).
        for (var i = 0; i < cardDefinition.EffectDefinition.Count; i++)
        {
            var effect = cardDefinition.EffectDefinition[i];
            var value = effect.Value ?? throw new InvalidOperationException($"Effect {effect.EffectType} is missing a magnitude value.");

            switch (effect.EffectType)
            {
                case CardEffectType.Heal:
                {
                    // SELF effect: the Boss restores its own HP. The write site is the
                    // existing Boss-side heal (COMBAT_RULES.md §4 item 1's clamp,
                    // ADR-026 D2's Boss pool), whose dead-target rule makes a heal at
                    // HP <= 0 a documented no-op rather than a cast rejection.
                    var healAmount = effect.ValueType switch
                    {
                        CardEffectValueType.PercentMaxHp => (bossState.MaxHP * value) / 100,
                        CardEffectValueType.Flat => value,
                        _ => throw new InvalidOperationException($"Unsupported valueType {effect.ValueType} for Heal effect."),
                    };
                    bossState = HealResolution.ApplyBossHeal(bossState, healAmount);
                    break;
                }

                case CardEffectType.Shield:
                {
                    // SELF effect: the Boss gains its own absorption pool. The one
                    // instance-per-identity / refresh-replaces / no-stacking contract
                    // is StatusEffectLifecycle.ApplyShield's (COMBAT_RULES.md §4
                    // items 2–4); the instance records the Boss as its source
                    // (GAME_STATE.md §2.3.1 item 7).
                    var shieldAmount = effect.ValueType switch
                    {
                        CardEffectValueType.PercentMaxHp => (bossState.MaxHP * value) / 100,
                        CardEffectValueType.Flat => value,
                        _ => throw new InvalidOperationException($"Unsupported valueType {effect.ValueType} for Shield effect."),
                    };
                    var shield = StatusEffect.TriggerBased(
                        "Shield",
                        StatusEffectType.Shield,
                        StatusEffectSource.Boss,
                        shieldAmount,
                        StatusEffect.ShieldDepletedCondition);
                    bossState = bossState with
                    {
                        ActiveStatusEffects = StatusEffectLifecycle.ApplyShield(
                            bossState.ActiveStatusEffects,
                            shield),
                    };
                    break;
                }

                case CardEffectType.Power:
                {
                    // SELF effect: the Boss's own resource. GAME_RULES.md §12's range
                    // is an invariant of the pool and GAME_STATE.md §2.4.3 item 5 fixes
                    // the clamp at the write site, so a grant is clamped to 0–100
                    // exactly as the Player's Power Charge is.
                    //
                    // No PowerChanged is emitted: that event reports PetState.Power
                    // (GAME_EVENTS.md §2, SIGNALR_PROTOCOL.md §3.2.24), and a Boss-side
                    // Power change is delivered by the bossState.power state projection
                    // (SIGNALR_PROTOCOL.md §4.4 item 12, ADR-029 §2.11), never by
                    // PowerChanged.
                    var powerAmount = effect.ValueType switch
                    {
                        CardEffectValueType.Flat => value,
                        _ => throw new InvalidOperationException($"Unsupported valueType {effect.ValueType} for Power effect."),
                    };
                    bossState = bossState with
                    {
                        Power = Math.Clamp(bossState.Power + powerAmount, 0, 100),
                    };
                    break;
                }

                case CardEffectType.Burn:
                {
                    // OPPONENT effect: the active Pet is burned. The instance records
                    // the Boss as its source (GAME_STATE.md §2.3.1 item 7), and its
                    // Turn countdown, one-instance-per-identity identity and
                    // refresh-not-stack behavior are StatusEffectLifecycle.Apply's
                    // (COMBAT_RULES.md §5.1–§5.3). The tick itself is step 19a's, not
                    // this cast's.
                    var burnDamage = effect.ValueType switch
                    {
                        CardEffectValueType.Flat => value,
                        _ => throw new InvalidOperationException($"Unsupported valueType {effect.ValueType} for Burn effect."),
                    };
                    var duration = effect.Duration ?? 2;
                    var burn = StatusEffect.TurnBased(
                        "Burn",
                        StatusEffectType.DoT,
                        StatusEffectSource.Boss,
                        burnDamage,
                        duration);
                    petState = petState with
                    {
                        ActiveStatusEffects = StatusEffectLifecycle.Apply(
                            petState.ActiveStatusEffects,
                            burn),
                    };
                    break;
                }

                default:
                    // Unreachable: RequireRepresentableBossEffects rejects every effect
                    // this branch cannot resolve, and the CardEffectType set is closed.
                    // It is stated so the switch stays total rather than silently
                    // ignoring an effect no document authors (AGENTS.md §7).
                    throw new InvalidOperationException($"Unsupported effectType {effect.EffectType} for a Boss Card cast.");
            }
        }

        // 7. The committed result — the state the round's single write-back carries.
        //    Sequence, Turn, CardCastsUsedThisTurn, RngState, BoardState,
        //    LastCommittedSwapPair, Combo and MatchCount are carried across exactly as
        //    they were: this cast is a phase of a round, not a round
        //    (GAME_STATE.md §5.1 items 1 and 9), it spends no Player allowance
        //    (CARD_RULES.md §3 item 9), and none of its supported effects consumes the
        //    RNG. Both actors' states are written through the one result object.
        var committedState = state with
        {
            PetState = petState,
            BossState = bossState,
        };

        // No events: see this method's summary. The empty collection is the
        // documented "this cast emitted nothing" value, not a placeholder.
        return CardCastExecutionResult.Committed(committedState, Array.Empty<BattleEvent>());
    }

    /// <summary>
    /// The representability gate for a Boss Card cast's effects
    /// (<c>CARD_RULES.md</c> §3 item 9, <c>BOSS_RULES.md</c> §4.2 item 4).
    ///
    /// <b>The Boss actor's supported set is the closed one this contract can
    /// supply inputs for:</b> the self effects <see cref="CardEffectType.Heal"/>,
    /// <see cref="CardEffectType.Shield"/> and <see cref="CardEffectType.Power"/>
    /// (inputs: <c>BossState.HP</c>/<c>MaxHP</c>, <c>BossState.ActiveStatusEffects</c>,
    /// <c>BossState.Power</c>) and the opponent effect
    /// <see cref="CardEffectType.Burn"/> (inputs: the active Pet's
    /// <c>ActiveStatusEffects</c>, and the Boss as the instance's
    /// <see cref="StatusEffectSource"/>).
    ///
    /// <b><see cref="CardEffectType.Damage"/> and <see cref="CardEffectType.Crit"/>
    /// are refused explicitly instead of being given a derived value.</b>
    /// <c>ADR-026</c> D3 keeps all five MVP abilities at <c>BaseDamage = 0</c> and
    /// <c>BOSS_RULES.md</c> §4 item 3 records that no MVP Boss Card or Boss Skill
    /// deals direct damage, the Boss's only damage being the ATK-gated instance;
    /// there is also no <c>BossState.Crit</c> member at all
    /// (<c>GAME_STATE.md</c> §2.4), so a Boss Crit modifier has neither a carrier
    /// nor an authored magnitude. Deriving either would author a gameplay rule and
    /// a magnitude no document states (<c>AGENTS.md</c> §7), so the cast throws.
    ///
    /// <b>Throwing is not a cast rejection.</b> It signals that the definition is
    /// not representable for this actor at all — a content/contract error, not a
    /// transient gameplay state — so it is not conditioned on the cast's Power or
    /// on any other gameplay value, and it runs before the deduction so nothing is
    /// written. It is deliberately not converted into an
    /// <see cref="CardCastRejectionReason"/>: no rejection code is added for it and
    /// the wire vocabulary is unchanged.
    /// </summary>
    /// <param name="cardDefinition">The Boss Card definition being cast.</param>
    /// <exception cref="InvalidOperationException">
    /// The definition carries an effect the Boss actor's contract cannot resolve —
    /// <see cref="CardEffectType.Damage"/>, <see cref="CardEffectType.Crit"/>, or an
    /// effect identity outside the closed <see cref="CardEffectType"/> set.
    /// </exception>
    private static void RequireRepresentableBossEffects(CardDefinition cardDefinition)
    {
        for (var i = 0; i < cardDefinition.EffectDefinition.Count; i++)
        {
            var effectType = cardDefinition.EffectDefinition[i].EffectType;

            switch (effectType)
            {
                case CardEffectType.Heal:
                case CardEffectType.Shield:
                case CardEffectType.Power:
                case CardEffectType.Burn:
                    break;

                case CardEffectType.Damage:
                case CardEffectType.Crit:
                    throw new InvalidOperationException(
                        $"A Boss Card cast cannot resolve a {effectType} effect: "
                        + "BOSS_RULES.md §4 item 3 and ADR-026 D3 author no Boss Card "
                        + "damage (all five MVP abilities have BaseDamage = 0; the Boss's "
                        + "only damage is the ATK-gated instance), and no BossState.Crit "
                        + "member exists (GAME_STATE.md §2.4). Deriving a value here would "
                        + "author a gameplay rule and a magnitude no document states "
                        + "(AGENTS.md §7). The supported Boss-actor effects are Heal, "
                        + "Shield and Power (self) and Burn (opponent) "
                        + "(CARD_RULES.md §3 item 9).");

                default:
                    throw new InvalidOperationException(
                        $"Unsupported effectType {effectType} for a Boss Card cast: the "
                        + "CardEffectType set is closed (DATABASE.md §1 item 1), so an "
                        + "undefined identity is rejected rather than ignored "
                        + "(AGENTS.md §7).");
            }
        }
    }
}
