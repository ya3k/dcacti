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
    /// The <c>OnBattleStart</c> Trigger identity (<c>RELIC_RULES.md</c> §3):
    /// "fires once, at battle start" — Burning Curse's declared Trigger
    /// (§6 note 1, §8.5 item 5).
    /// </summary>
    public const string OnBattleStartTrigger = "OnBattleStart";

    /// <summary>
    /// The <c>OnCascade</c> Trigger identity (<c>RELIC_RULES.md</c> §3): "fires on
    /// each Cascade iteration (<c>MATCH3_RULES.md</c> §4)" — Cascade Core's
    /// declared Trigger (§3.2, §8.5 item 9). Each actual cascade iteration is an
    /// independent event, so this Trigger fires once per iteration and never once
    /// per Swap.
    /// </summary>
    public const string OnCascadeTrigger = "OnCascade";

    /// <summary>
    /// The <c>OnPowerGain</c> Trigger identity (<c>RELIC_RULES.md</c> §3): "fires
    /// when the active Pet's Power increases from a qualifying non-Relic-generated
    /// Power gain (§3.1)" — Arcane Battery's declared Trigger (§8.5 item 7).
    /// </summary>
    public const string OnPowerGainTrigger = "OnPowerGain";

    /// <summary>
    /// The <c>OnDamageTaken</c> Trigger identity (<c>RELIC_RULES.md</c> §3):
    /// "fires when the active Pet takes damage" — Battle Instinct's declared
    /// Trigger (§8.5 item 10).
    /// </summary>
    public const string OnDamageTakenTrigger = "OnDamageTaken";

    /// <summary>
    /// Whether a Power change with the given <c>source</c> is a <b>qualifying</b>
    /// Power gain for <c>OnPowerGain</c> (<c>RELIC_RULES.md</c> §3.1).
    ///
    /// <code>
    /// Match / Card / other non-Relic gain  → qualifying
    /// Relic effect grants Power            → NOT qualifying
    /// </code>
    ///
    /// <b>§3.1 owns the rule, and it is one sentence.</b> "A Power gain qualifies
    /// iff it originates <b>outside</b> Relic effect resolution. Power that a
    /// Relic's own effect grants — Arcane Battery's <c>+5</c>, Cascade Core's
    /// <c>+5</c>, Mana Crystal's <c>+10</c>, or any future Relic's — does not
    /// qualify, and therefore does not fire <c>OnPowerGain</c>." §8.5 item 7
    /// records the consequence for Arcane Battery: the <c>+5</c> it grants
    /// originates in Relic effect resolution, so it is not a qualifying
    /// <c>OnPowerGain</c> event "not for this Relic instance and not for any other
    /// <c>OnPowerGain</c> Relic either".
    ///
    /// <b>This is what makes the Relic chain terminate, and it is the canonical
    /// mechanism rather than a depth limit.</b> §3.1 item 2 states the blocked
    /// shape explicitly — <c>OnPowerGain → Arcane Battery → +5 Power →
    /// OnPowerGain</c> is blocked because a Relic-generated gain does not qualify.
    ///
    /// <b>The test is the gain's origin, and the existing source vocabulary
    /// already carries it.</b> <see cref="PowerChangeSource"/> is the
    /// <c>source</c> member <c>GAME_EVENTS.md</c> §2 gives <c>PowerChanged</c>,
    /// and its <see cref="PowerChangeSource.Relic"/> value names exactly the
    /// mutations §3.1 excludes — "a Relic-owned Power mutation
    /// (<c>GAME_RULES.md</c> §17 step 11)". No new event, field, or source value is
    /// introduced: the exclusion is a predicate over a value the contract already
    /// carries, so a future <c>OnPowerGain</c> Relic is covered without amending
    /// anything (§3.1 item 3).
    /// </summary>
    /// <param name="source">
    /// The stage that owns the Power mutation, from the mutation's own
    /// <c>PowerChanged</c> report.
    /// </param>
    public static bool IsQualifyingPowerGain(PowerChangeSource source) =>
        source != PowerChangeSource.Relic;

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
    ///
    /// This is the <see cref="RelicFiringPoint.BoardResolution"/> pass, kept as its
    /// own overload because it is the one firing point that reads the committed
    /// Swap's Match/Combo accounting.
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
        int combo) =>
        Resolve(
            equippedRelics,
            petState,
            RelicFiringPoint.BoardResolution,
            matchCount,
            combo);

    /// <summary>
    /// Evaluates the equipped Relics for one <b>firing point</b> — one documented
    /// event of <c>RELIC_RULES.md</c> §3.
    ///
    /// <b>The order is the given order and nothing else.</b> The sequence is the
    /// equip-slot order <c>RELIC_RULES.md</c> §2.3 derives from the submitted
    /// loadout and §4.2 fixes for a single event's eligible Relics; this method
    /// never sorts, groups, or re-ranks it (§2.3 item 2 forbids ordering slots by
    /// <c>RelicInstanceId</c>, <c>RelicDefinitionId</c>, <c>AcquiredAt</c>, or
    /// database order).
    ///
    /// <b>One call is one root event.</b> <c>RELIC_RULES.md</c> §5 item 2 allows a
    /// Relic at most one fire per root event, and the per-instance safeguard below
    /// is scoped to this call — so a caller that fires
    /// <see cref="RelicFiringPoint.CascadeIteration"/> once per iteration, or
    /// <see cref="RelicFiringPoint.DamageTaken"/> once per resolved damage
    /// instance, gives the Relic exactly the once-per-occurrence firing §3.2 and
    /// §5 item 2 describe. Aggregating several occurrences into one call would
    /// under-fire; calling one occurrence twice would over-fire.
    /// </summary>
    /// <param name="equippedRelics">
    /// The battle's equipped Relics in equip-slot order (<c>RELIC_RULES.md</c>
    /// §2.2, §8). An empty sequence resolves nothing.
    /// </param>
    /// <param name="petState">
    /// The active Pet's state at this firing point (<c>GAME_STATE.md</c> §2.3) —
    /// the state the effects are applied to. It is never mutated: the result
    /// carries the state the effects produced.
    /// </param>
    /// <param name="firingPoint">Which documented event this pass processes.</param>
    /// <param name="matchCount">
    /// The battle's cumulative Match count at this point, read by
    /// <c>MatchCountAtLeast</c> (<c>RELIC_RULES.md</c> §8.1 item 3). It is
    /// meaningful at <see cref="RelicFiringPoint.BoardResolution"/>; the default
    /// <c>0</c> is the "no Match count is read here" value for every other point,
    /// whose eligible Triggers carry no Condition form this value is part of.
    /// </param>
    /// <param name="combo">
    /// The current Swap's Combo value at this point, read by <c>ComboAtLeast</c>
    /// (§8.1 item 3). Meaningful at
    /// <see cref="RelicFiringPoint.BoardResolution"/>, on the same basis as
    /// <paramref name="matchCount"/>.
    /// </param>
    /// <param name="powerGainSource">
    /// For <see cref="RelicFiringPoint.PowerGain"/>, the stage that owns the Power
    /// mutation the gain came from (<see cref="PowerChangeSource"/>) — the existing
    /// <c>source</c> metadata <c>RELIC_RULES.md</c> §3.1 qualifies on. It is
    /// ignored by every other firing point (no other Trigger observes Power), and
    /// <c>null</c> means "this pass is not qualified by a stated source".
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
        RelicFiringPoint firingPoint,
        int matchCount = 0,
        int combo = 0,
        PowerChangeSource? powerGainSource = null)
    {
        ArgumentNullException.ThrowIfNull(equippedRelics);

        if (!Enum.IsDefined(firingPoint))
        {
            throw new ArgumentOutOfRangeException(
                nameof(firingPoint),
                firingPoint,
                "The firing point must be a RelicFiringPoint member.");
        }

        // RELIC_RULES.md §3.1 items 1–2 / §8.5 item 7: Power granted by a Relic
        // effect is NOT a qualifying OnPowerGain event, so a gain whose source is
        // Relic effect resolution resolves no Relic at all. This is the canonical
        // source/qualification semantics rather than a recursion-depth limit: the
        // excluded gain never becomes an event, so
        // OnPowerGain → Arcane Battery → +5 Power → OnPowerGain cannot be
        // constructed. The check sits at the one place OnPowerGain events are
        // evaluated, so every caller inherits it.
        if (firingPoint == RelicFiringPoint.PowerGain
            && powerGainSource is { } gainSource
            && !IsQualifyingPowerGain(gainSource))
        {
            return new RelicResolutionResult(petState, [], []);
        }

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

            if (!IsEvaluatedAt(definition.Trigger, firingPoint))
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
    /// Whether <paramref name="trigger"/> is one of the §3 identities the given
    /// <paramref name="firingPoint"/> processes (<c>RELIC_RULES.md</c> §3, §6,
    /// §8.5).
    ///
    /// <b>Only the Triggers that point's own event belongs to are evaluated.</b>
    /// §3 gives each Trigger its own firing point: <c>OnCardCast</c> fires at a
    /// Card cast (<c>CARD_RULES.md</c> §3 item 4), <c>OnMatch</c> and
    /// <c>OnDamageDealt</c> at their own sites, <c>OnTurnStart</c>/<c>OnTurnEnd</c>
    /// at the Turn boundaries, <c>OnBattleStart</c> at battle start, and so on. A
    /// Relic whose Trigger belongs to another point is simply not this pass's to
    /// evaluate, and its standing modifiers — if any — belong to that point's
    /// maintenance. No provisioned row declares an unimplemented Trigger
    /// (<c>RELIC_RULES.md</c> §8.5).
    ///
    /// <b>The three step-11 Triggers share one point, and that is §8.1 item 8's
    /// grouping.</b> <c>GAME_RULES.md</c> §17 step 11 is a single step, and §8.1
    /// item 8 fixes the point at which <c>MatchCountAtLeast</c>,
    /// <c>ComboAtLeast</c>, and <c>HpPercentageBelow</c> are all read, so
    /// <c>OnMatchCount</c>, <c>OnCombo</c>, and <c>OnHpBelow</c> are all this
    /// point's. Each other §3 Trigger has its own, which is why
    /// <c>OnDamageTaken</c> is evaluated where the Pet's damage instance actually
    /// resolves (§3: "fires when the active Pet takes damage") and
    /// <c>OnCascade</c> once per iteration rather than once per Swap (§3.2).
    ///
    /// <b>A value outside §3's list is not evaluated either, and nothing is
    /// invented for it.</b> <c>Trigger</c> is the one member §8.5 item 3
    /// deliberately leaves unstructured (it stays §3's prose identity), so there is
    /// no reader that validates it and no documented behavior for a value the list
    /// does not contain. This stage therefore applies nothing for such a Relic
    /// rather than failing an action on a content typo or substituting a trigger it
    /// was not given — the same "invent nothing" position <c>AGENTS.md</c> §7 takes,
    /// and the one <c>RELIC_RULES.md</c> §8.7 records for this stage.
    /// </summary>
    private static bool IsEvaluatedAt(string trigger, RelicFiringPoint firingPoint) =>
        firingPoint switch
        {
            RelicFiringPoint.BoardResolution =>
                string.Equals(trigger, OnMatchCountTrigger, StringComparison.Ordinal)
                || string.Equals(trigger, OnComboTrigger, StringComparison.Ordinal)
                || string.Equals(trigger, OnHpBelowTrigger, StringComparison.Ordinal),

            RelicFiringPoint.BattleStart =>
                string.Equals(trigger, OnBattleStartTrigger, StringComparison.Ordinal),

            RelicFiringPoint.CascadeIteration =>
                string.Equals(trigger, OnCascadeTrigger, StringComparison.Ordinal),

            RelicFiringPoint.PowerGain =>
                string.Equals(trigger, OnPowerGainTrigger, StringComparison.Ordinal),

            RelicFiringPoint.DamageTaken =>
                string.Equals(trigger, OnDamageTakenTrigger, StringComparison.Ordinal),

            _ => throw new ArgumentOutOfRangeException(
                nameof(firingPoint),
                firingPoint,
                "The firing point must be a RelicFiringPoint member."),
        };

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
                    // §8.3's table: ATK is target Pet, valueType Percentage, and the
                    // two lifetimes it lists are Battle (Berserker Core, §8.5 item 4)
                    // and NextAttack (Battle Instinct, §8.5 item 10). The modifier is
                    // a signed percentage-point contribution (COMBAT_RULES.md §5.6.6
                    // item 2) and the element's value is a positive magnitude — §8.5
                    // item 4 transcribes Berserker Core's "+5%" as the value 5 — so it
                    // contributes +value.
                    //
                    // The element's declared lifetime is carried onto the modifier
                    // (GAME_STATE.md §2.3.7 item 11, §2.3.8 item 3): both lifetimes
                    // ride the one ATKModifiers[] carrier, and the element's own
                    // Lifetime is what distinguishes them, so the collection is not
                    // Battle-only and no NextAttackATKModifiers[] exists.
                    //
                    // PetState.ATK is deliberately NOT written: §5.6.4 and §5.6.6
                    // item 8 keep it the permanent base stat, and the composed
                    // EffectivePetATK is derived at attack resolution. The modifier is
                    // also NOT consumed here: consumption belongs to the qualifying
                    // owner attack (§5.1.4 item 4, COMBAT_RULES.md §3.3 items 7–11).
                    state = state with
                    {
                        ATKModifiers = ATKModifiers.Apply(
                            state.ATKModifiers,
                            new ATKModifier(sourceIdentity, value, effect.Lifetime)),
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

                case RelicEffectType.BurnDamage:
                    // §8.3's table: BurnDamage is target Pet, lifetime Battle,
                    // valueType Percentage. The applied modification is the Pet's own
                    // standing Burn-damage modifier (§6 note 1: "target: Pet
                    // identifies the Pet as the owned/source context"), so it is
                    // carried on the Pet's BurnDamageModifiers[] and keyed on the
                    // equipped instance identity like every other applied Relic
                    // modifier.
                    //
                    // Nothing else happens here, and that is COMBAT_RULES.md §5.2
                    // item 4's contract: the modifier "changes damage and nothing
                    // else" — it does not emit, create, re-enter, or refresh a Burn
                    // event or Burn instance, it does not extend or consume an
                    // instance's duration, and it re-enters no Burn calculation. The
                    // tick site applies it when a Pet-owned Burn tick resolves, so no
                    // recursion and no second Burn system exists.
                    state = state with
                    {
                        BurnDamageModifiers = BurnDamageModifiers.Apply(
                            state.BurnDamageModifiers,
                            new BurnDamageModifier(sourceIdentity, value)),
                    };
                    applied = true;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(effects),
                        effect.EffectType,
                        "The effect identity must be a RelicEffectType member; RELIC_RULES.md "
                        + "§8.2 item 1 closes the set at ATK | Power | Crit | CardCost | "
                        + "BurnDamage.");
            }
        }

        return (state, applied);
    }

    /// <summary>
    /// Removes one source's standing <c>Battle</c>-lifetime modifiers — its
    /// <c>Battle</c>-lifetime <c>ATKModifiers[]</c> elements, its
    /// <c>CardCostModifiers[]</c> element, and its
    /// <c>BurnDamageModifiers[]</c> element (<c>GAME_STATE.md</c> §5.1.3 item 4,
    /// §5.1.4 items 4–5).
    ///
    /// <b>Only this source's elements are removed.</b> Another source's modifier is
    /// carried across untouched, no stored stat is recomputed or reset, and no
    /// arithmetic inverse is applied — removal is the deletion of the identified
    /// element. Removing a source that is not present is an idempotent no-op.
    ///
    /// <b>It is lifetime-scoped.</b> <c>GAME_STATE.md</c> §5.1.4 item 4 states that
    /// a failed condition's removal must not touch a <c>NextAttack</c> element:
    /// "an unconsumed element persists across Turns until a qualifying attack
    /// consumes it", and §5.1.2 item 3 states that consumption is the <b>only</b>
    /// operation that removes such an element. The ATK removal below therefore
    /// names the <c>Battle</c> lifetime explicitly, so a Battle Instinct
    /// <c>NextAttack</c> element survives a reversion that belongs to a
    /// <c>Battle</c> source's condition.
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
            ATKModifiers = ATKModifiers.RemoveLifetime(
                petState.ATKModifiers,
                sourceIdentity,
                RelicEffectLifetime.Battle),
            CardCostModifiers = CardCostModifiers.Remove(petState.CardCostModifiers, sourceIdentity),
            BurnDamageModifiers = BurnDamageModifiers.Remove(
                petState.BurnDamageModifiers,
                sourceIdentity),
        };
}
