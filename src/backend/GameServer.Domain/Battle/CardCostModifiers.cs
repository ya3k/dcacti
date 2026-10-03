namespace GameServer.Domain.Battle;

/// <summary>
/// The <c>CardCostModifiers[]</c> state mutation lifecycle — apply/refresh and
/// removal (<c>GAME_STATE.md</c> §5.1.3; <c>TASK-134</c> D6/D7).
///
/// <code>
/// Apply    append, or set the existing element's reduction (refresh)
/// Remove   delete exactly the identified source's element
/// </code>
///
/// <b>This type owns the state mutation. It does not own the rule.</b>
/// <c>GAME_STATE.md</c> §5.1.3 states the same split §5.1.1 and §5.1.2 record for
/// their collections: "The gameplay rule it implements — what the modifier's
/// <c>Battle</c> lifetime and the source's re-evaluation mean, and how the
/// reductions compose into a Card cost — is owned by <c>CARD_RULES.md</c> §3.6 and
/// <c>RELIC_RULES.md</c> §8.4/§8.5 and is <b>not</b> restated or reinterpreted
/// here." Every operation below implements a numbered item of §5.1.3 and cites it.
///
/// <b>Every operation is a pure, deterministic function.</b> No RNG, no clock, no
/// I/O, no framework dependency, and no ambient state (<c>ARCHITECTURE.md</c> §2.1,
/// <c>TDD.md</c> §6). The caller sequences the calls and performs the one
/// post-resolution write-back (<c>GAME_STATE.md</c> §5.1, §5.1.3 item 6).
///
/// <b>Order is preserved here, and deliberately not sorted.</b> This is the one
/// behavioral difference from the sibling <see cref="ATKModifiers"/> lifecycle, and
/// it is the documented one: §2.3.6 item 1 of the sibling's schema sorts, while
/// §2.3.6 item 6 states for <i>this</i> collection that "Order is preserved for
/// round-trip fidelity, not for semantics" and §2.3.5 item 8 that "a round trip must
/// still return the elements in the order they were written". A refresh therefore
/// keeps the element's existing position rather than moving it, and nothing here
/// imposes an order the contract does not define.
///
/// <b>What this type deliberately does not do.</b>
/// <list type="bullet">
/// <item>It does not expire anything at a Turn boundary. §5.1.3 item 3 states there
/// is no Turn-based expiry, no timeout, and no cleanup pass: these elements carry no
/// <c>RemainingTurns</c>, so the step 19a pass — a Turn-countdown rule — must not
/// invent a duration for them. The declared lifetime is <c>Battle</c>, whose boundary
/// is the battle's own end (item 5).</item>
/// <item>It does not compute or store <c>EffectiveCardCost</c>. §5.1.3 item 9 and
/// <c>CARD_RULES.md</c> §3.6 item 2 leave the cost arithmetic to Card resolution and
/// make the composed cost derived for the cast being resolved — explicitly "not
/// stored state".</item>
/// <item>It does not modify authored <c>CardDefinition.PowerCost</c>, and it does not
/// evaluate any Relic's trigger or condition. Which source applies or removes a
/// modifier is owned by <c>RELIC_RULES.md</c> and <c>CARD_RULES.md</c>; this type only
/// performs the mutation once a caller has determined it.</item>
/// <item>It does not serialize. The collection is not a wire member
/// (<c>GAME_STATE.md</c> §2.3.5 item 10); its round trip is the serializer's
/// obligation (§2.3.6).</item>
/// </list>
/// </summary>
public static class CardCostModifiers
{
    /// <summary>
    /// Applies or refreshes a Card-cost modifier on a collection
    /// (<c>GAME_STATE.md</c> §5.1.3 items 1–2; <c>TASK-134</c> D6).
    ///
    /// <b>One element per source identity; a repeat application refreshes.</b>
    /// §5.1.3 item 1: "Applying a modifier appends one element when its
    /// <c>SourceIdentity</c> is not already present, and otherwise sets that existing
    /// element's <c>CostReductionPercentage</c> to the newly applied value. Two
    /// elements with the same <c>SourceIdentity</c> are never observable in a
    /// committed state" — the same "set, not an increment" operation §5.1.1 item 1
    /// defines for <c>StatusEffects[]</c>.
    ///
    /// <b>Continuous re-evaluation refreshes; it does not accumulate.</b> §5.1.3
    /// item 2 and §2.3.5 item 5 state that a source whose condition is re-evaluated
    /// while it remains satisfied resolves to the <i>same</i>
    /// <c>SourceIdentity</c> every time, so each evaluation is a replace/refresh of
    /// that one element: there is no counter, no stack, no queue, and no second entry.
    /// Emergency Core's <c>RELIC_RULES.md</c> §6 note 2 is the provisioned case, and it
    /// holds exactly one entry however often it re-evaluates
    /// (<c>GAME_STATE.md</c> §2.3.5 item 3).
    ///
    /// <b>The element's position is preserved on refresh.</b> §2.3.5 item 8 makes
    /// ordering non-semantic while §2.3.6 item 6 requires it to be preserved for
    /// round-trip fidelity, so a refresh replaces in place: the operation neither
    /// grows the collection nor reorders it.
    /// </summary>
    /// <param name="modifiers">
    /// The active Pet's current Card-cost modifiers (<c>GAME_STATE.md</c> §2.3.5).
    /// Always a collection, never <c>null</c>: §2.3.5 item 6 makes "no modifier
    /// active" an <b>empty collection</b>, because the collection always exists and
    /// absence of it is not a representable state.
    /// </param>
    /// <param name="modifier">
    /// The modifier to apply or refresh. Its
    /// <see cref="CardCostModifier.SourceIdentity"/> is the identity this operation
    /// de-duplicates on and its <see cref="CardCostModifier.CostReductionPercentage"/>
    /// is the value a refresh sets.
    /// </param>
    /// <returns>
    /// The resulting collection: the same elements in the same order, with the
    /// matching element replaced in place, or <paramref name="modifier"/> appended
    /// when no element carried its identity.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="modifiers"/> is <c>null</c> — not a representable state
    /// (§2.3.5 item 6) — or <paramref name="modifier"/> carries no usable source
    /// identity (§2.3.5 item 3).
    /// </exception>
    public static CardCostModifier[] Apply(
        IReadOnlyList<CardCostModifier> modifiers,
        CardCostModifier modifier)
    {
        ArgumentNullException.ThrowIfNull(modifiers);

        // §2.3.5 items 3 and 7: the identity is the replace/refresh and removal key
        // and is required to be a non-empty string. A blank identity would let two
        // different sources collide on it, which destroys the one property the member
        // exists to provide, and §2.3.5 item 7 states that a malformed element is not
        // silently repaired, defaulted, or dropped — so it is rejected rather than
        // admitted or normalized.
        if (!modifier.HasSourceIdentity)
        {
            throw new ArgumentException(
                "A Card-cost modifier's SourceIdentity is its replace/refresh and "
                + "removal key and must be a non-empty stable source identity "
                + "(GAME_STATE.md §2.3.5 items 3 and 7).",
                nameof(modifier));
        }

        var result = new CardCostModifier[modifiers.Count + 1];
        var replaced = false;

        for (var index = 0; index < modifiers.Count; index++)
        {
            var existing = modifiers[index];

            // §5.1.3 item 1: an apply of an already-active source identity REFRESHES
            // that element — the newly applied reduction replaces the old, in place,
            // with no second element and no accumulation. §2.3.6 item 6 keeps the
            // position stable for round-trip fidelity.
            if (!replaced
                && string.Equals(existing.SourceIdentity, modifier.SourceIdentity, StringComparison.Ordinal))
            {
                result[index] = modifier;
                replaced = true;
                continue;
            }

            result[index] = existing;
        }

        if (!replaced)
        {
            result[modifiers.Count] = modifier;
        }

        return replaced ? result[..^1] : result;
    }

    /// <summary>
    /// Removes a specific source's modifier — the source-specific removal §5.1.3
    /// item 4 owns.
    ///
    /// <b>Removal is driven by the source's own condition, and it removes only that
    /// source's element.</b> §5.1.3 item 4: "When the source's documented condition no
    /// longer holds, that source's element is removed from the collection in that
    /// resolution — … Removal is the deletion of the identified element, never an
    /// arithmetic inverse and never a recomputation. It removes <b>only</b> that
    /// source's element: another source's modifier is untouched." Which condition that
    /// is remains the source's rule — for the provisioned <c>CardCost</c> source it is
    /// Emergency Core's <c>RELIC_RULES.md</c> §6 note 2 reversion when HP rises back
    /// above 30% — and is not evaluated here.
    ///
    /// <b>Expiry is a removal, not a stored zero or a stored flag.</b> §5.1.3 item 4
    /// states that absence means "not active" (§2.3.5 item 6) and that no
    /// zero-percentage element stands in for a removed one. A source whose genuine
    /// reduction is <c>0</c> is therefore a real modifier and is <b>not</b> removed by
    /// this operation.
    ///
    /// <b>Removing a source that is not present is an idempotent no-op.</b> The
    /// collection is returned unchanged, which is the documented "already not active"
    /// state rather than an error: §2.3.5 item 6 makes absence the representation of
    /// "no modifier is active".
    ///
    /// <b>The remaining elements keep their relative order.</b> §2.3.6 item 6 requires
    /// the order to survive, so elements not removed are carried across unchanged and
    /// in order — a removal does not reorder what it left behind.
    /// </summary>
    /// <param name="modifiers">
    /// The active Pet's modifiers at the moment the source stops applying
    /// (<c>GAME_STATE.md</c> §2.3.5). Always a collection, never <c>null</c>
    /// (§2.3.5 item 6).
    /// </param>
    /// <param name="sourceIdentity">
    /// The identity of the source whose element is removed — the value
    /// <see cref="CardCostModifier.SourceIdentity"/> carries. Every other source's
    /// element is carried across unchanged.
    /// </param>
    /// <returns>
    /// The resulting collection with the matching element removed and every other
    /// element carried across unchanged and in order.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="modifiers"/> is <c>null</c> (§2.3.5 item 6), or
    /// <paramref name="sourceIdentity"/> is <c>null</c>.
    /// </exception>
    public static CardCostModifier[] Remove(
        IReadOnlyList<CardCostModifier> modifiers,
        string sourceIdentity)
    {
        ArgumentNullException.ThrowIfNull(modifiers);
        ArgumentNullException.ThrowIfNull(sourceIdentity);

        var remaining = new List<CardCostModifier>(modifiers.Count);

        foreach (var modifier in modifiers)
        {
            // §5.1.3 item 4: only the identified source's element is deleted; another
            // source's modifier is untouched.
            if (!string.Equals(modifier.SourceIdentity, sourceIdentity, StringComparison.Ordinal))
            {
                remaining.Add(modifier);
            }
        }

        return [.. remaining];
    }

    /// <summary>
    /// Whether two modifier collections hold the same elements in the same order —
    /// the structural comparison <c>GAME_STATE.md</c> §2.3.6 item 5's round-trip
    /// obligation requires.
    ///
    /// <b>Why a comparison method is needed.</b> The collections are arrays and a
    /// record's default equality compares them by reference, so two states a round
    /// trip made hold the same elements would compare unequal on that member alone.
    /// This is the same need <see cref="StatusEffectLifecycle.EffectsEqual"/> answers
    /// for <c>StatusEffects[]</c> (§2.3.2 item 5) and
    /// <see cref="ATKModifiers.ModifiersEqual"/> answers for the sibling ATK
    /// collection (§2.3.8 item 5).
    ///
    /// Order participates because a round trip must "return the elements in the order
    /// they were written" (§2.3.6 item 5), even though §2.3.5 item 8 makes that order
    /// non-semantic.
    /// </summary>
    /// <param name="left">One collection.</param>
    /// <param name="right">The other collection.</param>
    /// <returns>Whether the two collections are element-for-element equal.</returns>
    public static bool ModifiersEqual(
        IReadOnlyList<CardCostModifier> left,
        IReadOnlyList<CardCostModifier> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        // CardCostModifier is a record struct, so its own equality already compares
        // both members.
        for (var index = 0; index < left.Count; index++)
        {
            if (!left[index].Equals(right[index]))
            {
                return false;
            }
        }

        return true;
    }
}
