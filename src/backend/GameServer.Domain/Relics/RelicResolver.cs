using GameServer.Domain.Battle;
using GameServer.Domain.Match3;

namespace GameServer.Domain.Relics;

/// <summary>
/// The <c>GAME_RULES.md</c> §17 step 11 stage — <b>"Trigger Relics"</b>: evaluate
/// each equipped Relic's Trigger and structured Condition against the current
/// resolution state, apply its structured <c>EffectDefinition[]</c> when eligible,
/// and report the Relics whose effect actually applied
/// (<c>RELIC_RULES.md</c> §3–§5, §8; <c>ADR-018</c>).
///
/// <code>
/// equipped Relics (equip-slot order)          RELIC_RULES.md §2.2, §2.3, §4.2
///         ↓  definition lookup (already resolved)
/// Trigger    is this the event step 11 processes?      §3
///         ↓
/// Condition  MatchCountAtLeast | ComboAtLeast | HpPercentageBelow   §8.1
///         ↓
/// EffectDefinition[]                                  §8.2–§8.4
///         ↓  through the documented state carriers
/// ATKModifiers[] / CardCostModifiers[] / NextAttackCritModifiers[] / Power
///         ↓
/// RelicTriggered per Relic whose effect applied       §7
/// </code>
///
/// <b>It reads declared content, never content names.</b> Every decision below
/// comes from the Relic's stored <c>Trigger</c>, <c>Condition</c>, and
/// <c>EffectDefinition[]</c>. Nothing here compares a
/// <see cref="RelicDefinition.Name"/>, a <see cref="RelicDefinition.RelicDefinitionId"/>,
/// or a display string, and there is no per-Relic branch: <c>RELIC_RULES.md</c>
/// §8.2 item 1 forbids exactly that ("the runtime must never derive a Relic's
/// effect from parsed prose, from the Relic's <c>Name</c>, from
/// <c>RelicDefinitionId</c> mapping, or from hardcoded per-Relic logic").
///
/// <b>It is a pure, deterministic function.</b> No RNG, no clock, no I/O, no
/// framework dependency, and no ambient state (<c>ARCHITECTURE.md</c> §2.1,
/// <c>TDD.md</c> §6, <c>AGENTS.md</c> §11). A Relic effect introduces no
/// randomness: the only RNG in a Swap is the cascade Spawn's
/// (<c>MATCH3_RULES.md</c> §7). No Relic counter, cooldown, or per-instance state
/// is created, because <c>RELIC_RULES.md</c> §8.1 item 3 and <c>ADR-018</c> item 5
/// forbid one and §8.4 item 4 adds none.
///
/// <b>It resolves one root event.</b> Step 11 processes the committed Swap's
/// resolution state, and <c>RELIC_RULES.md</c> §5 item 2 allows a Relic at most
/// one fire per root event. §4.3's chain queue is therefore exactly this pass:
/// the four <c>effectType</c> values §8.2 defines — an ATK modifier, a Power
/// grant, a Crit modifier, and a Card-cost modifier — produce no
/// <c>OnMatchCount</c>, <c>OnCombo</c>, or <c>OnHpBelow</c> event, so no applied
/// effect can make a Relic eligible again inside the same root event and the
/// queue never holds a second entry. <see cref="HandledSources"/> states that
/// safeguard explicitly rather than relying on the closure being permanent.
/// </summary>
public static class RelicResolver
{
    /// <summary>
    /// The <c>OnMatchCount</c> Trigger identity (<c>RELIC_RULES.md</c> §3): fires
    /// when the battle's cumulative Match count crosses a threshold —
    /// Berserker Core's and Mana Crystal's declared Trigger (§8.5).
    /// </summary>
    public const string OnMatchCountTrigger = "OnMatchCount";

    /// <summary>
    /// The <c>OnCombo</c> Trigger identity (<c>RELIC_RULES.md</c> §3): fires when
    /// Combo reaches/crosses a threshold within one Swap — Assassin Eye's declared
    /// Trigger (§8.5).
    /// </summary>
    public const string OnComboTrigger = "OnCombo";

    /// <summary>
    /// The <c>OnHpBelow</c> Trigger identity (<c>RELIC_RULES.md</c> §3): fires
    /// when the active Pet's HP crosses below a configured percentage —
    /// Emergency Core's declared Trigger (§8.5).
    /// </summary>
    public const string OnHpBelowTrigger = "OnHpBelow";

    /// <summary>
    /// Evaluates the equipped Relics for one committed Swap's resolution state —
    /// <c>GAME_RULES.md</c> §17 step 11.
    ///
    /// <b>The order is the given order and nothing else.</b> The sequence is the
    /// equip-slot order <c>RELIC_RULES.md</c> §2.3 derives from the submitted
    /// loadout and §4.2 fixes for a single event's eligible Relics; this method
    /// never sorts, groups, or re-ranks it (§2.3 item 2 forbids ordering slots by
    /// <c>RelicInstanceId</c>, <c>RelicDefinitionId</c>, <c>AcquiredAt</c>, or
    /// database order).
    ///
    /// <b>Each Relic is evaluated independently.</b> A Relic whose Trigger is not
    /// this stage's, or whose content did not resolve, contributes nothing and
    /// does not affect any other Relic's evaluation; two Relics eligible from the
    /// same event both apply, and their modifiers coexist on their own carriers
    /// (<c>GAME_STATE.md</c> §2.3.5 item 3, §2.3.7 item 4).
    /// </summary>
    /// <param name="equippedRelics">
    /// The battle's equipped Relics in equip-slot order, each carrying its owned
    /// instance identity and the resolved static definition it references
    /// (<c>RELIC_RULES.md</c> §2.2, §8). An empty sequence is the documented
    /// "nothing equipped" case and resolves nothing.
    /// </param>
    /// <param name="petState">
    /// The active Pet's state at step 11 (<c>GAME_STATE.md</c> §2.3) — the state
    /// the effects are applied to and the state the conditions' HP reading comes
    /// from. It is never mutated: the result carries the state the effects
    /// produced.
    /// </param>
    /// <param name="matchCount">
    /// The battle's cumulative Match count at step 11
    /// (<c>GAME_STATE.md</c> §2.2, <c>GAME_RULES.md</c> §3) — the existing value
    /// <c>RELIC_RULES.md</c> §8.1 item 3 reads for <c>MatchCountAtLeast</c>. No
    /// Relic-owned counter is introduced or consulted.
    /// </param>
    /// <param name="combo">
    /// The current Swap's Combo value at step 11 (<c>GAME_STATE.md</c> §2.2,
    /// <c>GAME_RULES.md</c> §5) — the existing value §8.1 item 3 reads for
    /// <c>ComboAtLeast</c>.
    /// </param>
    /// <returns>
    /// The resulting <c>PetState</c> and the ordered reports of the Relics whose
    /// effect applied.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="equippedRelics"/> is <c>null</c>.
    /// </exception>
    public static RelicResolutionResult Resolve(
        IReadOnlyList<EquippedRelicContent> equippedRelics,
        PetState petState,
        int matchCount,
        int combo)
    {
        ArgumentNullException.ThrowIfNull(equippedRelics);

        var resolved = petState;
        var triggered = new List<RelicTriggeredEvent>();
        var powerChanges = new List<PowerChangedEvent>();

        // RELIC_RULES.md §5 item 2: one equipped Relic instance fires at most once
        // per root event. The set is the same per-instance safeguard the section
        // states — "§5's per-Relic anti-chain rule operates per equipped instance"
        // (§4) — and it is keyed on the instance identity, never on a definition,
        // because two equipped instances of one definition are two Relics.
        var handledSources = new HashSet<string>(StringComparer.Ordinal);

        foreach (var equipped in equippedRelics)
        {
            var sourceIdentity = equipped.InstanceIdentity.Value;

            if (string.IsNullOrWhiteSpace(sourceIdentity))
            {
                // A slot with no identity could not be refreshed or removed
                // source-specifically, which is the one property every carrier's key
                // exists to provide (GAME_STATE.md §2.3.5 item 3, §2.3.7 item 3).
                throw new ArgumentException(
                    "An equipped Relic's instance identity is its RelicTriggered identity and "
                    + "its modifier sources' replace/refresh key, and must be non-blank "
                    + "(RELIC_RULES.md §2.2 item 3).",
                    nameof(equippedRelics));
            }

            // §5 item 2: the same instance does not fire again within this root event.
            if (!handledSources.Add(sourceIdentity))
            {
                continue;
            }

            // A definition that did not resolve declares no Trigger, Condition, or
            // Effect, so there is nothing to evaluate and nothing may be invented for
            // it (AGENTS.md §7). The slot is skipped; the Relic simply does nothing.
            if (equipped.Definition is not { } definition)
            {
                continue;
            }

            if (!IsEvaluatedByStep11(definition.Trigger))
            {
                // §3 gives this Trigger its own firing point, so it is not this
                // stage's event. Nothing is applied and nothing is reverted: the
                // Relic's standing modifiers, if any, were created at that Trigger's
                // own point and are that point's to maintain.
                continue;
            }

            if (!ConditionHolds(definition.Condition, resolved, matchCount, combo))
            {
                // GAME_STATE.md §5.1.3 item 4 / §5.1.4 item 4: "When the source's
                // documented condition no longer holds, that source's element is
                // removed from the collection in that resolution", and
                // RELIC_RULES.md §8.5 item 2 gives that reversion for Emergency Core
                // explicitly (§6 note 2: "reverting when HP rises back above 30%").
                // The source's documented condition IS its Trigger+Condition, so a
                // failed evaluation reverts this source's Battle-lifetime modifiers
                // and nothing else — never another source's, and never a
                // NextAttack-lifetime element, whose only removal is consumption
                // (§5.1.2 item 3).
                resolved = RevertBattleLifetimeModifiers(resolved, sourceIdentity);
                continue;
            }

            var applied = Apply(
                resolved,
                definition.EffectDefinition,
                sourceIdentity,
                powerChanges);

            resolved = applied.PetState;

            if (applied.AnyEffectApplied)
            {
                // RELIC_RULES.md §7: the event reports that a Relic's Effect applied
                // and which Relic applied it — one report per Relic, in slot order
                // (§4.2). A Trigger that fired but whose Condition failed, or whose
                // effect was not resolvable, emits nothing.
                triggered.Add(new RelicTriggeredEvent(sourceIdentity));
            }
        }

        return new RelicResolutionResult(resolved, triggered, powerChanges);
    }

    /// <summary>
    /// Whether <paramref name="trigger"/> is one of the §3 identities step 11
    /// processes — the Trigger values whose documented event is this stage's
    /// (<c>RELIC_RULES.md</c> §3, §6, §8.5).
    ///
    /// <b>Only three of §3's twelve values are step 11's.</b> §3 gives each Trigger
    /// its own firing point: <c>OnCardCast</c> fires at a Card cast
    /// (<c>CARD_RULES.md</c> §3 item 4), <c>OnDamageTaken</c> when the Pet takes
    /// damage (<c>GAME_RULES.md</c> §17 step 18), <c>OnTurnStart</c>/<c>OnTurnEnd</c>
    /// at the Turn boundaries, <c>OnBattleStart</c> at battle start, and so on.
    /// Step 11's event is the committed Swap's resolution state, so a Relic whose
    /// Trigger belongs to another point is simply not this stage's to evaluate, and
    /// its standing modifiers — if any — belong to that point's maintenance. No
    /// provisioned row declares one (<c>RELIC_RULES.md</c> §8.5).
    ///
    /// <b>A value outside §3's list is not evaluated either, and nothing is
    /// invented for it.</b> <c>Trigger</c> is the one member §8.5 item 3
    /// deliberately leaves unstructured (it stays §3's prose identity), so there is
    /// no reader that validates it and no documented behavior for a value the list
    /// does not contain. This stage therefore applies nothing for such a Relic
    /// rather than failing the Swap on a content typo or substituting a trigger it
    /// was not given — the same "invent nothing" position <c>AGENTS.md</c> §7 takes,
    /// and the one <c>RELIC_RULES.md</c> §8.7 records for this stage.
    /// </summary>
    private static bool IsEvaluatedByStep11(string trigger) =>
        string.Equals(trigger, OnMatchCountTrigger, StringComparison.Ordinal)
        || string.Equals(trigger, OnComboTrigger, StringComparison.Ordinal)
        || string.Equals(trigger, OnHpBelowTrigger, StringComparison.Ordinal);

    /// <summary>
    /// Evaluates a structured <c>Condition</c> against the current resolution state
    /// (<c>RELIC_RULES.md</c> §8.1).
    ///
    /// <code>
    /// MatchCountAtLeast(N)   matchCount &gt;= N          §8.1
    /// ComboAtLeast(N)        combo      &gt;= N          §8.1
    /// HpPercentageBelow(N)   HP / MaxHP &lt;  N percent  §8.1 item 6
    /// </code>
    ///
    /// <b>The comparison is exact integer arithmetic.</b> §8.1 item 1 makes the
    /// threshold an integer and §8.2 item 2 forbids pre-resolving a value against
    /// an assumed stat, so the HP form is evaluated as
    /// <c>HP × 100 &lt; N × MaxHP</c> rather than as a fraction — the same
    /// "integer arithmetic, no floating-point representation" discipline
    /// <c>COMBAT_RULES.md</c> §5.4.2 and <c>TDD.md</c> §6 require, and it makes the
    /// boundary exact at every HP value.
    ///
    /// <b>The form names fix the direction.</b> <c>AtLeast</c> is inclusive and
    /// <c>Below</c> is exclusive, which is what §8.1's own wording states ("the
    /// cumulative Match count reached N or more", "the active Pet's HP fell below N
    /// percent"). No third direction is defined and none is inferred.
    ///
    /// <b>The HP form reads the active Pet's current HP against its own MaxHP.</b>
    /// §8.1 item 6 fixes the subject (the active Pet, whose HP members are
    /// <c>GAME_STATE.md</c> §2.3's <c>HP</c> and <c>MaxHP</c>) and states that
    /// whether the comparison resolves against current HP or a maximum is this
    /// stage's statement: a <i>percentage</i> of HP requires a base, and the only
    /// documented one is the Pet's own maximum — there is no battle-start HP
    /// snapshot anywhere in <c>BattleState</c>. The value read is the resolution
    /// state's, so the reading is the live one §6 note 2's continuous
    /// re-evaluation requires.
    /// </summary>
    /// <param name="condition">
    /// The Relic's optional Condition. <c>null</c> means the Trigger alone is the
    /// complete condition (§8.1 item 4), which is satisfied.
    /// </param>
    /// <param name="petState">The active Pet's state at step 11.</param>
    /// <param name="matchCount">The battle's cumulative Match count at step 11.</param>
    /// <param name="combo">The current Swap's Combo value at step 11.</param>
    private static bool ConditionHolds(
        RelicCondition? condition,
        PetState petState,
        int matchCount,
        int combo)
    {
        if (condition is not { } required)
        {
            return true;
        }

        return required.ConditionType switch
        {
            RelicConditionType.MatchCountAtLeast => matchCount >= required.Threshold,
            RelicConditionType.ComboAtLeast => combo >= required.Threshold,

            // Widened to long because both products can exceed an int for a large
            // threshold or a large MaxHP pool; the comparison itself is unaffected.
            RelicConditionType.HpPercentageBelow =>
                (long)petState.HP * 100 < (long)required.Threshold * petState.MaxHP,

            _ => throw new ArgumentOutOfRangeException(
                nameof(condition),
                required.ConditionType,
                "The condition form must be a RelicConditionType member; RELIC_RULES.md §8.1 "
                + "closes the grammar at MatchCountAtLeast | ComboAtLeast | HpPercentageBelow."),
        };
    }

    /// <summary>
    /// Applies every effect of one eligible Relic, in the stored array order
    /// (<c>RELIC_RULES.md</c> §8.2), through the documented state carriers.
    /// </summary>
    /// <param name="petState">The active Pet's state before this Relic's effects.</param>
    /// <param name="effects">The Relic's structured effect declaration (§8.2).</param>
    /// <param name="sourceIdentity">
    /// The equipped instance identity this Relic's modifiers are keyed on
    /// (<c>RELIC_RULES.md</c> §2.2 item 3) — the replace/refresh and removal key
    /// every carrier's <c>SourceIdentity</c> is (<c>GAME_STATE.md</c> §2.3.5
    /// item 3, §2.3.7 item 3). Because it is per <b>equipped instance</b>, one
    /// source re-applying refreshes its own entry, two instances of one definition
    /// coexist as two entries (§2.4 item 3), and Emergency Core's continuous
    /// re-evaluation replaces its one element rather than accumulating (§8.5
    /// item 2).
    /// </param>
    /// <param name="powerChanges">
    /// Collects one report per applied Power effect, in application order.
    /// </param>
    private static (PetState PetState, bool AnyEffectApplied) Apply(
        PetState petState,
        RelicEffectDefinitions effects,
        string sourceIdentity,
        List<PowerChangedEvent> powerChanges)
    {
        var state = petState;
        var applied = false;

        for (var index = 0; index < effects.Count; index++)
        {
            var effect = effects[index];

            // RELIC_RULES.md §8.2 item 2/3: an Undetermined element records that the
            // owning document states no magnitude for the effect, so it is NOT
            // resolvable. It is an open content gap and is never substituted with a
            // value — no fallback magnitude, no default, and no silent invention
            // (AGENTS.md §7). The element is therefore passed over, and the Relic's
            // other effects still apply.
            if (effect.ValueType == RelicEffectValueType.Undetermined)
            {
                continue;
            }

            // §8.2 item 3 / the element reader: an interpreting valueType always
            // carries a positive magnitude, so this is unreachable for a value the
            // contract's factories or persisted-payload reader produced.
            var value = effect.Value
                ?? throw new InvalidOperationException(
                    $"A Relic effect element with valueType '{effect.ValueType}' carries no "
                    + "magnitude. RELIC_RULES.md §8.2 item 3 requires an interpreting value "
                    + "type to state one, and an absent magnitude is recorded with "
                    + "RelicEffectValueType.Undetermined.");

            switch (effect.EffectType)
            {
                case RelicEffectType.ATK:
                    // §8.3's table: ATK is target Pet, lifetime Battle,
                    // valueType Percentage. The modifier is a signed percentage-point
                    // contribution (COMBAT_RULES.md §5.6.6 item 2) and the element's
                    // value is a positive magnitude — §8.5 item 4 transcribes
                    // Berserker Core's "+5%" as the value 5 — so it contributes +value.
                    // PetState.ATK is deliberately NOT written: §5.6.4 and §5.6.6
                    // item 8 keep it the permanent base stat, and the composed
                    // EffectivePetATK is derived at attack resolution.
                    state = state with
                    {
                        ATKModifiers = ATKModifiers.Apply(
                            state.ATKModifiers,
                            new ATKModifier(sourceIdentity, value)),
                    };
                    applied = true;
                    break;

                case RelicEffectType.Power:
                    // §8.3 item 3: Immediate "denotes an effect applied once, at the
                    // moment it triggers, which leaves no standing modification behind
                    // — Mana Crystal's Power grant is applied to PetState.Power and
                    // the effect itself then ends." Nothing is persisted for it.
                    //
                    // The write goes through GAME_RULES.md §17 step 13's own Power
                    // write site, which is the single point GAME_RULES.md §12's 0–100
                    // range is clamped at, so the report below carries the change the
                    // cap actually allowed.
                    var before = state.Power;
                    state = ResourceGenerator.ApplyPower(state, value);
                    powerChanges.Add(new PowerChangedEvent(
                        PowerChangeSource.Relic,
                        state.Power - before,
                        state.Power));
                    applied = true;
                    break;

                case RelicEffectType.Crit:
                    // §8.3's table: Crit is lifetime NextAttack. §8.3 item 4 makes
                    // that the established ADR-017 boundary — a modification consumed
                    // by the next qualifying owner attack — so the effect creates or
                    // refreshes an element of the existing NextAttackCritModifiers[]
                    // carrier rather than a second temporary-Crit representation.
                    // PetState.Crit is deliberately NOT written (COMBAT_RULES.md §3.3
                    // item 7: a temporary modifier never overwrites the base stat), and
                    // the modifier is NOT consumed here — consumption belongs to the
                    // qualifying attack (§5.1.2 item 4).
                    state = state with
                    {
                        NextAttackCritModifiers = NextAttackCritModifiers.Apply(
                            state.NextAttackCritModifiers,
                            new NextAttackCritModifier(sourceIdentity, value)),
                    };
                    applied = true;
                    break;

                case RelicEffectType.CardCost:
                    // §8.3's table: CardCost is lifetime Battle. §8.5 item 2 records
                    // the carrier (GAME_STATE.md §2.3.5) and its mutation lifecycle
                    // (§5.1.3); the cost composition that reads it is
                    // CARD_RULES.md §3.6's and is not restated here. No card
                    // definition is touched.
                    state = state with
                    {
                        CardCostModifiers = CardCostModifiers.Apply(
                            state.CardCostModifiers,
                            new CardCostModifier(sourceIdentity, value)),
                    };
                    applied = true;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(effects),
                        effect.EffectType,
                        "The effect identity must be a RelicEffectType member; RELIC_RULES.md "
                        + "§8.2 item 1 closes the set at ATK | Power | Crit | CardCost.");
            }
        }

        return (state, applied);
    }

    /// <summary>
    /// Removes one source's standing <c>Battle</c>-lifetime modifiers — its
    /// <c>ATKModifiers[]</c> and <c>CardCostModifiers[]</c> elements
    /// (<c>GAME_STATE.md</c> §5.1.3 item 4, §5.1.4 item 4).
    ///
    /// <b>Only this source's elements are removed.</b> Another source's modifier is
    /// carried across untouched, no stored stat is recomputed or reset, and no
    /// arithmetic inverse is applied — removal is the deletion of the identified
    /// element. Removing a source that is not present is an idempotent no-op.
    ///
    /// <b><c>NextAttackCritModifiers[]</c> is deliberately not touched.</b>
    /// <c>GAME_STATE.md</c> §5.1.2 item 3 states there is no expiry of any kind for
    /// that collection — "The only operation that removes an element is
    /// <c>Consume</c>" — so a Relic whose <c>NextAttack</c> effect has not yet been
    /// consumed keeps it, per <c>RELIC_RULES.md</c> §8.4 item 3 ("a Relic whose
    /// effect lifetime has ended is not disabled").
    /// </summary>
    private static PetState RevertBattleLifetimeModifiers(
        PetState petState,
        string sourceIdentity) =>
        petState with
        {
            ATKModifiers = ATKModifiers.Remove(petState.ATKModifiers, sourceIdentity),
            CardCostModifiers = CardCostModifiers.Remove(petState.CardCostModifiers, sourceIdentity),
        };
}
