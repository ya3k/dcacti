namespace GameServer.Domain.Battle;

/// <summary>
/// The <c>NextAttackCritModifiers[]</c> state mutation lifecycle — create,
/// refresh, and consume (<c>GAME_STATE.md</c> §5.1.2, <c>ADR-017</c>).
///
/// <code>
/// Apply    append, or set the existing element's contribution (refresh)
/// Consume  remove exactly the identified elements the attack consumed
/// </code>
///
/// <b>This type owns the state mutation. It does not own the rule.</b>
/// <c>GAME_STATE.md</c> §5.1.2 makes the same split §5.1.1 makes for
/// <see cref="StatusEffectLifecycle"/>: "The gameplay rule it implements — what a
/// qualifying attack is, and how the contributions compose — is owned by
/// <c>COMBAT_RULES.md</c> §3.3 and is <b>not</b> restated or reinterpreted here."
/// Every operation below implements a numbered item of §5.1.2 and cites it.
///
/// <b>Every operation is a pure, deterministic function.</b> No RNG, no clock, no
/// I/O, no framework dependency, and no ambient state (<c>ARCHITECTURE.md</c>
/// §2.1, <c>TDD.md</c> §6). The caller sequences the calls and performs the one
/// post-resolution write-back (<c>GAME_STATE.md</c> §5.1, §5.1.2 item 6).
///
/// <b>What this type deliberately does not do.</b>
/// <list type="bullet">
/// <item>It does not expire anything. §5.1.2 item 3 states there is no expiry of
/// any kind and no cleanup pass — no Turn expiry, no timeout, no end-of-turn
/// removal, no periodic sweep, and no removal on Turn increment. The only
/// operation that removes an element is <see cref="Consume"/>, and an unconsumed
/// element persists across Turns indefinitely by design.</item>
/// <item>It does not participate in the step 19a pass. §5.1.2 item 2 excludes
/// these elements from the Turn countdown, because they carry no
/// <c>RemainingTurns</c>; an implementation that decrements, expires, or sweeps
/// them at step 19a is inventing a Turn-based lifetime the contract does not
/// define.</item>
/// <item>It does not decide which attack qualifies or when consumption happens.
/// §5.1.2 item 4 defers that to <c>COMBAT_RULES.md</c> §3.3; this type only
/// performs the removal once the caller has determined it.</item>
/// <item>It does not serialize. The collection is not a wire member
/// (<c>GAME_STATE.md</c> §2.3.4 item 8); its round trip is the serializer's
/// obligation.</item>
/// </list>
/// </summary>
public static class NextAttackCritModifiers
{
    /// <summary>
    /// Applies or refreshes a NextAttack Crit modifier on a collection
    /// (<c>GAME_STATE.md</c> §5.1.2 item 1).
    ///
    /// <b>One element per source identity; a repeat application refreshes.</b>
    /// §2.3.4 item 2 makes the identity per <i>source</i>, so creating a modifier
    /// appends one element when its <see cref="NextAttackCritModifier.SourceIdentity"/>
    /// is not already present, and otherwise <b>sets that existing element's</b>
    /// contribution to the new value. §5.1.2 item 1 states the invariant plainly:
    /// "Two elements with the same <c>SourceIdentity</c> are never observable in a
    /// committed state" — the same "set, not an increment" operation §5.1.1 item 1
    /// defines for <c>StatusEffects[]</c>.
    ///
    /// <b>This is why repeated casts of one Card do not stack</b>, which is the
    /// behavior <c>CARD_RULES.md</c> §4.1's "for the next attack only" reading and
    /// <c>COMBAT_RULES.md</c> §5.2 item 2's MVP refresh default both call for —
    /// reusing that documented default rather than authoring a new stacking rule
    /// (<c>ADR-017</c> "Trade-offs"). Two <i>different</i> sources still coexist as
    /// two elements, which is what §3.3 item 10 requires.
    ///
    /// <b>The element's position is preserved on refresh.</b> §2.3.4 item 6 makes
    /// ordering non-semantic, and preserving position keeps addition and
    /// consumption order-independent while leaving the array stable for
    /// round-trip fidelity — the same behavior
    /// <see cref="StatusEffectLifecycle.Apply"/> has for the same two reasons.
    /// </summary>
    /// <param name="modifiers">
    /// The entity's current active modifiers (<c>GAME_STATE.md</c> §2.3.4). Always
    /// a collection, never <c>null</c>: §2.3.4 item 5 makes "no modifier active" an
    /// <b>empty collection</b>, because the collection always exists.
    /// </param>
    /// <param name="modifier">
    /// The modifier to apply or refresh. Its
    /// <see cref="NextAttackCritModifier.SourceIdentity"/> is the identity this
    /// operation de-duplicates on and its
    /// <see cref="NextAttackCritModifier.CritContribution"/> is the value a refresh
    /// sets.
    /// </param>
    /// <returns>
    /// The resulting collection: the same elements in the same order, with the
    /// matching element replaced in place, or <paramref name="modifier"/> appended
    /// when no active element carried its identity.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="modifiers"/> is <c>null</c> — not a representable state
    /// (§2.3.4 item 5) — or <paramref name="modifier"/> carries no usable source
    /// identity (§2.3.4 item 2).
    /// </exception>
    public static NextAttackCritModifier[] Apply(
        IReadOnlyList<NextAttackCritModifier> modifiers,
        NextAttackCritModifier modifier)
    {
        ArgumentNullException.ThrowIfNull(modifiers);

        // §2.3.4 item 2: the identity is the removal key, so an element without one
        // could not be consumed source-specifically — the single property the
        // member exists to provide. Rejected rather than admitted.
        if (!modifier.HasSourceIdentity)
        {
            throw new ArgumentException(
                "A NextAttack Crit modifier's SourceIdentity is its removal key and "
                + "must be a non-blank stable source identity (GAME_STATE.md §2.3.4 "
                + "item 2).",
                nameof(modifier));
        }

        var result = new NextAttackCritModifier[modifiers.Count + 1];
        var replaced = false;

        for (var index = 0; index < modifiers.Count; index++)
        {
            var existing = modifiers[index];

            // §5.1.2 item 1: an apply of an already-active source identity REFRESHES
            // that element — the new contribution replaces the old, in place, with
            // no second element and no summation. §2.3.4 item 6 keeps the position
            // non-semantic but stable.
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
    /// Removes exactly the modifiers a qualifying attack consumed
    /// (<c>GAME_STATE.md</c> §5.1.2 item 4; <c>COMBAT_RULES.md</c> §3.3 item 9).
    ///
    /// <b>Consumption is a removal of identified elements, and nothing else.</b>
    /// §5.1.2 item 4 enumerates what consumption must never do: it never writes
    /// <c>PetState.Crit</c> and never assigns it the configuration default (§2.3.4
    /// items 9–10); it never removes a modifier whose source was not consumed by
    /// that attack; and it never removes, resets, or adjusts any other Crit source,
    /// so the base value, Passive Crit, and Relic Crit remain exactly as they were.
    /// It is <b>not</b> an arithmetic inverse and <b>not</b> a recomputation from
    /// the base: it is the deletion of identified elements.
    ///
    /// <b>Consumption is order-independent by construction.</b> §5.1.2 item 5:
    /// elements are matched by <see cref="NextAttackCritModifier.SourceIdentity"/>,
    /// not by position, so the result does not depend on array order (§2.3.4
    /// item 6) and no ordering rule has to be — or may be — invented to make
    /// consumption deterministic.
    ///
    /// <b>The remaining elements keep their relative order.</b> Elements not
    /// consumed are carried across unchanged and in order, so a consumption does
    /// not reorder what it left behind.
    /// </summary>
    /// <param name="modifiers">
    /// The entity's active modifiers at the moment the qualifying attack consumes
    /// (<c>GAME_STATE.md</c> §2.3.4). Always a collection, never <c>null</c>
    /// (§2.3.4 item 5).
    /// </param>
    /// <param name="consumedSourceIdentities">
    /// The identities the qualifying attack consumed — the value
    /// <c>COMBAT_RULES.md</c> §3.3 item 10 produces when it determines that all
    /// applicable modifiers for that attack are consumed together. An empty set
    /// removes nothing, which is the documented state for an attack with no
    /// applicable modifier.
    /// </param>
    /// <returns>
    /// The resulting collection with every element whose identity was consumed
    /// removed, and every other element carried across unchanged and in order.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="modifiers"/> is <c>null</c> (§2.3.4 item 5), or
    /// <paramref name="consumedSourceIdentities"/> is <c>null</c>.
    /// </exception>
    public static NextAttackCritModifier[] Consume(
        IReadOnlyList<NextAttackCritModifier> modifiers,
        IReadOnlyCollection<string> consumedSourceIdentities)
    {
        ArgumentNullException.ThrowIfNull(modifiers);
        ArgumentNullException.ThrowIfNull(consumedSourceIdentities);

        var remaining = new List<NextAttackCritModifier>(modifiers.Count);

        foreach (var modifier in modifiers)
        {
            // §5.1.2 items 4–5: only the consumed source-specific elements are
            // removed, matched by identity rather than by position.
            var wasConsumed = false;

            foreach (var consumed in consumedSourceIdentities)
            {
                if (string.Equals(modifier.SourceIdentity, consumed, StringComparison.Ordinal))
                {
                    wasConsumed = true;
                    break;
                }
            }

            if (!wasConsumed)
            {
                remaining.Add(modifier);
            }
        }

        return [.. remaining];
    }

    /// <summary>
    /// The Crit contribution the given source identities would add to Effective
    /// Crit — the sum over the elements an attack finds applicable
    /// (<c>COMBAT_RULES.md</c> §3.3 item 7).
    ///
    /// <b>It reports the contributions; it does not apply the cap.</b>
    /// <c>COMBAT_RULES.md</c> §3.3 item 7 owns the composition <i>and</i> the cap,
    /// and owns them in one place: "Effective Crit is capped at 100 percentage
    /// points". Capping here as well would create a second place where the ceiling
    /// is decided, which is the duplication
    /// <c>documentation-change.md</c> §2 forbids in documentation and which invites
    /// the two from drifting in code. The composition and the cap are therefore both
    /// performed by <see cref="GameServer.Domain.Combat.DamagePipeline"/>, which is
    /// the one place §3.3 item 7's value is consumed.
    ///
    /// This is a reading helper for the caller that must decide <i>which</i>
    /// identities a qualifying attack consumes (<c>GAME_STATE.md</c> §5.1.2 item 4);
    /// it decides no rule of its own.
    /// </summary>
    /// <param name="modifiers">The entity's active modifiers (<c>GAME_STATE.md</c> §2.3.4).</param>
    /// <returns>The summed contribution of the supplied elements, in percentage points.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="modifiers"/> is <c>null</c>.</exception>
    public static int TotalContribution(IReadOnlyList<NextAttackCritModifier> modifiers)
    {
        ArgumentNullException.ThrowIfNull(modifiers);

        var total = 0;

        for (var index = 0; index < modifiers.Count; index++)
        {
            total += modifiers[index].CritContribution;
        }

        return total;
    }

    /// <summary>
    /// Whether two modifier collections hold the same elements in the same order —
    /// the structural comparison <c>GAME_STATE.md</c> §2.3.4 item 8's round-trip
    /// obligation requires.
    ///
    /// <b>Why a comparison method is needed.</b> The collections are arrays and a
    /// record's default equality compares them by reference, so two states a round
    /// trip made hold the same elements would compare unequal on that member alone.
    /// This is the same need <see cref="StatusEffectLifecycle.EffectsEqual"/>
    /// answers for <c>StatusEffects[]</c> (§2.3.2 item 5) and
    /// <see cref="Match3.BoardState.CellsEqual"/> answers for the board (§2.1.7
    /// item 5), applied to this collection.
    ///
    /// Order participates because <c>REDIS_STATE.md</c> §7 item 13 requires the
    /// order to round-trip, even though §2.3.4 item 6 makes it non-semantic.
    /// </summary>
    /// <param name="left">One collection.</param>
    /// <param name="right">The other collection.</param>
    /// <returns>Whether the two collections are element-for-element equal.</returns>
    public static bool ModifiersEqual(
        IReadOnlyList<NextAttackCritModifier> left,
        IReadOnlyList<NextAttackCritModifier> right)
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

        // NextAttackCritModifier is a record struct, so its own equality already
        // compares both members.
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
