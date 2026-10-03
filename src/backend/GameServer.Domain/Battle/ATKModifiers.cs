namespace GameServer.Domain.Battle;

/// <summary>
/// The <c>ATKModifiers[]</c> state mutation lifecycle — apply/refresh and removal
/// (<c>GAME_STATE.md</c> §5.1.4; <c>TASK-136</c> D2/D7/D8).
///
/// <code>
/// Apply    append, or refresh the existing element in place (never a duplicate)
/// Remove   delete exactly the identified source's element
/// </code>
///
/// <b>This type owns the state mutation. It does not own the rule.</b>
/// <c>GAME_STATE.md</c> §5.1.4 states the same split §5.1.2 records for
/// <see cref="NextAttackCritModifiers"/>: "The gameplay rule it implements — what
/// the modifier's <c>Battle</c> lifetime means and how the modifiers compose into
/// the Pet ATK the Damage Pipeline reads — is owned by <c>COMBAT_RULES.md</c>'s
/// Effective Pet ATK composition rule (<c>TASK-136</c> D5/D11) and
/// <c>RELIC_RULES.md</c> §8.4 and is <b>not</b> restated or reinterpreted here."
/// Every operation below implements a numbered item of §5.1.4 and cites it.
///
/// <b>Every operation is a pure, deterministic function.</b> No RNG, no clock, no
/// I/O, no framework dependency, and no ambient state (<c>ARCHITECTURE.md</c> §2.1,
/// <c>TDD.md</c> §6). The caller sequences the calls and performs the one
/// post-resolution write-back (<c>GAME_STATE.md</c> §5.1, §5.1.4 item 7).
///
/// <b>What this type deliberately does not do.</b>
/// <list type="bullet">
/// <item>It does not expire anything at a Turn boundary. §5.1.4 item 3 states there
/// is no Turn-based expiry, no timeout, and no cleanup pass: these elements carry no
/// <c>RemainingTurns</c>, so the step 19a pass — a Turn-countdown rule — must not
/// invent a duration for them. The lifetime is <c>Battle</c>, so its boundary is the
/// battle's own end, where the whole <c>BattleState</c> ceases to exist
/// (item 4).</item>
/// <item>It does not compose <c>EffectivePetATK</c>. §5.1.4 item 5 and
/// <c>COMBAT_RULES.md</c> §5.6.6 item 8 leave the composition to the Damage
/// Pipeline and make the composed value derived state that is never stored; no
/// <c>EffectivePetATK</c> member, cache, or field exists.</item>
/// <item>It never writes <see cref="PetState.ATK"/>. §5.1.4 item 6 forbids mutating
/// the permanent stat, resetting it to the configuration default, or adjusting it by
/// an arithmetic inverse — <c>PetState.ATK = DefaultATK</c> is forbidden for the
/// same source-blind reason §2.3.4 item 10 forbids <c>DefaultCrit</c>.</item>
/// <item>It does not serialize. The collection is not a wire member
/// (<c>GAME_STATE.md</c> §2.3.7 item 10); its round trip is the serializer's
/// obligation (§2.3.8).</item>
/// </list>
/// </summary>
public static class ATKModifiers
{
    /// <summary>
    /// Applies or refreshes an ATK modifier on a collection
    /// (<c>GAME_STATE.md</c> §5.1.4 items 1–2; <c>TASK-136</c> D7).
    ///
    /// <b>One element per source identity; a repeat application refreshes.</b>
    /// §2.3.7 item 4 makes this the collection's storage invariant — "Each source has
    /// at most one active entry" and "A collection containing two entries with the
    /// same <c>SourceIdentity</c> is therefore invalid state" — so applying a
    /// modifier appends one element when its identity is not already present, and
    /// otherwise <b>sets that existing element's</b>
    /// <see cref="ATKModifier.ATKModifierPercentage"/> to the new value. It never
    /// duplicates and never accumulates.
    ///
    /// <b>Refresh uses the newly applied value.</b> §5.1.4 item 2 is explicit: a
    /// re-application "replaces the stored <c>ATKModifierPercentage</c> with the value
    /// being applied; it does not preserve the previously stored value, does not add
    /// the two, and does not leave the entry unchanged" (<c>TASK-136</c> D7). That is
    /// what makes the per-source state the source's <i>current</i> contribution
    /// rather than an accumulating history.
    ///
    /// <b>The result is re-sorted by <see cref="ATKModifier.SourceIdentity"/>.</b>
    /// §2.3.7 item 7 fixes the collection's element order as "a deterministic sort on
    /// the identity key, so the serialized order is reproducible from the element set
    /// alone" — unlike the sibling collections, whose order records application
    /// order. The sort is therefore applied on every write rather than preserving the
    /// refreshed element's previous position, and
    /// <c>REDIS_STATE.md</c> §7 item 16 draws the consequence: "A store must therefore
    /// not be relied on to preserve insertion order for this member: the serialized
    /// order is reproducible from the element set alone, so two serializations of the
    /// same state are byte-identical."
    ///
    /// <b>Order carries no gameplay meaning.</b> §2.3.7 item 7 and §2.3.8 item 6 both
    /// state that no rule reads element positions; the ordering exists so a round trip
    /// is a no-op and so the serialized form is deterministic, and nothing here
    /// invents a meaning for it.
    /// </summary>
    /// <param name="modifiers">
    /// The Pet's current active modifiers (<c>GAME_STATE.md</c> §2.3.7). Always a
    /// collection, never <c>null</c>: §2.3.7 item 6 makes "no active ATK modifier" an
    /// <b>empty collection</b>, because the collection always exists and absence of it
    /// is not a representable state.
    /// </param>
    /// <param name="modifier">
    /// The modifier to apply or refresh. Its
    /// <see cref="ATKModifier.SourceIdentity"/> is the identity this operation
    /// de-duplicates on and its <see cref="ATKModifier.ATKModifierPercentage"/> is the
    /// value a refresh sets.
    /// </param>
    /// <returns>
    /// The resulting collection: the same elements with the matching element replaced
    /// in place, or <paramref name="modifier"/> appended when no element carried its
    /// identity, and in every case ordered by
    /// <see cref="ATKModifier.SourceIdentity"/> ordinal ascending (§2.3.7 item 7).
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="modifiers"/> is <c>null</c> — not a representable state
    /// (§2.3.7 item 6) — or <paramref name="modifier"/> carries no usable source
    /// identity (§2.3.7 item 3).
    /// </exception>
    public static ATKModifier[] Apply(
        IReadOnlyList<ATKModifier> modifiers,
        ATKModifier modifier)
    {
        ArgumentNullException.ThrowIfNull(modifiers);

        // §2.3.7 item 3: the identity is the replace/refresh and removal key, so an
        // element without one could not be refreshed or removed source-specifically —
        // the single property the member exists to provide. It is rejected rather
        // than admitted, the same loud-rejection position COMBAT_RULES.md §5.6 and
        // RELIC_RULES.md §8.2 item 5 take for a malformed effect.
        if (!modifier.HasSourceIdentity)
        {
            throw new ArgumentException(
                "An ATK modifier's SourceIdentity is its replace/refresh and removal "
                + "key and must be a non-blank stable source identity (GAME_STATE.md "
                + "§2.3.7 item 3).",
                nameof(modifier));
        }

        var result = new List<ATKModifier>(modifiers.Count + 1);
        var replaced = false;

        for (var index = 0; index < modifiers.Count; index++)
        {
            var existing = modifiers[index];

            // §5.1.4 items 1–2 / TASK-136 D7: an apply of an already-active source
            // identity REFRESHES that element — the newly applied percentage replaces
            // the old, with no second element, no summation, and no accumulation.
            if (!replaced
                && string.Equals(existing.SourceIdentity, modifier.SourceIdentity, StringComparison.Ordinal))
            {
                result.Add(modifier);
                replaced = true;
                continue;
            }

            result.Add(existing);
        }

        if (!replaced)
        {
            result.Add(modifier);
        }

        return SortedBySourceIdentity(result);
    }

    /// <summary>
    /// Removes a specific source's modifier — the removal §5.1.4 item 4 owns.
    ///
    /// <b>Removal removes only that source's element.</b> §5.1.4 item 4: "When a
    /// source's documented condition no longer holds, that source's element is removed
    /// from the collection in that resolution. Removal is the deletion of the
    /// identified element, never an arithmetic inverse and never a recomputation of
    /// the stored stat. It removes <b>only</b> that source's element: another source's
    /// modifier is untouched." So nothing here recomputes, re-derives, or normalizes a
    /// remaining element, and nothing here touches <see cref="PetState.ATK"/>.
    ///
    /// <b>Expiry is a removal, not a stored zero or a stored flag.</b> §5.1.4 item 4
    /// states that absence means "not active" (§2.3.7 item 6) and that no
    /// zero-percentage element stands in for a removed one — so a source carrying a
    /// genuine <c>0</c> is a real modifier and is <b>not</b> removed by this
    /// operation, while the source that is removed leaves nothing behind.
    ///
    /// <b>Removing a source that is not present is an idempotent no-op.</b> The
    /// collection is returned unchanged, which is the documented "already not active"
    /// state rather than an error: §2.3.7 item 6 makes absence the representation of
    /// "not active", so a removal that finds nothing has nothing to do.
    ///
    /// <b>The result is re-sorted by <see cref="ATKModifier.SourceIdentity"/>.</b>
    /// §2.3.7 item 7's deterministic order is a property of the element set, so it is
    /// preserved across removal without depending on the incoming array's order.
    /// </summary>
    /// <param name="modifiers">
    /// The Pet's active modifiers at the moment the source is removed
    /// (<c>GAME_STATE.md</c> §2.3.7). Always a collection, never <c>null</c>
    /// (§2.3.7 item 6).
    /// </param>
    /// <param name="sourceIdentity">
    /// The identity of the source whose element is removed — the value
    /// <see cref="ATKModifier.SourceIdentity"/> carries. Every other source's element
    /// is carried across unchanged.
    /// </param>
    /// <returns>
    /// The resulting collection with the matching element removed, every other element
    /// carried across unchanged, ordered by <see cref="ATKModifier.SourceIdentity"/>
    /// ordinal ascending (§2.3.7 item 7).
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="modifiers"/> is <c>null</c> (§2.3.7 item 6), or
    /// <paramref name="sourceIdentity"/> is <c>null</c>.
    /// </exception>
    public static ATKModifier[] Remove(
        IReadOnlyList<ATKModifier> modifiers,
        string sourceIdentity)
    {
        ArgumentNullException.ThrowIfNull(modifiers);
        ArgumentNullException.ThrowIfNull(sourceIdentity);

        var remaining = new List<ATKModifier>(modifiers.Count);

        foreach (var modifier in modifiers)
        {
            // §5.1.4 item 4: only the identified source's element is deleted; another
            // source's modifier is untouched and needs no recomputation.
            if (!string.Equals(modifier.SourceIdentity, sourceIdentity, StringComparison.Ordinal))
            {
                remaining.Add(modifier);
            }
        }

        return SortedBySourceIdentity(remaining);
    }

    /// <summary>
    /// Whether two modifier collections hold the same elements in the same order —
    /// the structural comparison <c>GAME_STATE.md</c> §2.3.8 item 5's round-trip
    /// obligation requires.
    ///
    /// <b>Why a comparison method is needed.</b> The collections are arrays and a
    /// record's default equality compares them by reference, so two states a round
    /// trip made hold the same elements would compare unequal on that member alone.
    /// This is the same need <see cref="NextAttackCritModifiers.ModifiersEqual"/>
    /// answers for its collection (§2.3.4 item 8) and
    /// <see cref="StatusEffectLifecycle.EffectsEqual"/> answers for
    /// <c>StatusEffects[]</c> (§2.3.2 item 5).
    ///
    /// Order participates because a round trip must return the elements "in the same
    /// element order" (§2.3.8 item 5) — and here the order is additionally the
    /// deterministic <see cref="ATKModifier.SourceIdentity"/> sort of §2.3.7 item 7,
    /// so a correct collection is ordered by construction.
    /// </summary>
    /// <param name="left">One collection.</param>
    /// <param name="right">The other collection.</param>
    /// <returns>Whether the two collections are element-for-element equal.</returns>
    public static bool ModifiersEqual(
        IReadOnlyList<ATKModifier> left,
        IReadOnlyList<ATKModifier> right)
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

        // ATKModifier is a record struct, so its own equality already compares both
        // members.
        for (var index = 0; index < left.Count; index++)
        {
            if (!left[index].Equals(right[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Orders a modifier set by <see cref="ATKModifier.SourceIdentity"/>
    /// (<c>GAME_STATE.md</c> §2.3.7 item 7: "Element order is deterministic: by
    /// <c>SourceIdentity</c>", "a deterministic sort on the identity key, so the
    /// serialized order is reproducible from the element set alone",
    /// <c>TASK-136</c> D2/D9).
    ///
    /// <b>The comparison is <see cref="StringComparer.Ordinal"/>.</b> §2.3.7 item 7
    /// fixes <i>that</i> the order is a sort on the identity key and requires it to be
    /// reproducible, but does not name a comparison. Ordinal is the reading that
    /// satisfies the determinism the section requires without introducing a
    /// dependency the section never mentions: it is a pure code-point comparison, so
    /// it cannot vary with the host's culture, locale, ICU version, or
    /// <c>CurrentCulture</c> — which is exactly what <c>TDD.md</c> §6 needs of a
    /// stored order that must be byte-identical across serializations and across a
    /// recovered battle. A culture-sensitive comparison would make the persisted order
    /// depend on ambient host state, i.e. on something no battle input determines.
    ///
    /// It is also the convention <c>GAME_STATE.md</c> §5.1.1 item 6 already fixes for
    /// the sibling collection ("by <c>Id</c> in ordinal ascending order") and that
    /// <c>StatusEffectLifecycle</c> already implements, so the two deterministic
    /// collection orders in this state tree are spelled the same way.
    ///
    /// The identity values themselves are the ASCII technical tokens
    /// <c>BOSS_RULES.md</c> §6.4 fixes for technical identities, so ordinal and
    /// invariant-culture ordering coincide for every documented value; ordinal is
    /// chosen because it is the one that stays correct if that ever changed.
    /// </summary>
    /// <param name="modifiers">The elements to order.</param>
    /// <returns>The elements sorted by identity, ordinal ascending.</returns>
    private static ATKModifier[] SortedBySourceIdentity(IEnumerable<ATKModifier> modifiers) =>
        [.. modifiers.OrderBy(modifier => modifier.SourceIdentity, StringComparer.Ordinal)];
}
