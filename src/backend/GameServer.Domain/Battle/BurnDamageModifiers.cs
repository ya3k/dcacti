namespace GameServer.Domain.Battle;

/// <summary>
/// The <c>BurnDamageModifiers[]</c> state mutation lifecycle — apply/refresh,
/// source-specific removal, and the applied percentage a Burn tick reads.
///
/// <code>
/// Apply    append, or refresh the existing element in place (never a duplicate)
/// Remove   delete exactly the identified source's element
/// Applied  the combined percentage points a Pet-owned Burn tick scales by
/// </code>
///
/// <b>What this collection is.</b> The Pet's applied <c>BurnDamage</c>
/// modifications — the runtime form of the Relic effect
/// <c>RELIC_RULES.md</c> §8.2 item 1 defines and §8.5 item 5 gives Burning
/// Curse. It is the carrier for a <c>Battle</c>-lifetime modification
/// (<c>RELIC_RULES.md</c> §8.3 item 4: "a standing modification for the
/// remainder of the battle"), so its element mirrors the sibling
/// <see cref="CardCostModifiers"/> collection: one <c>SourceIdentity</c> key and
/// one percentage, in written order, with the writing source replacing its own
/// element rather than accumulating.
///
/// <b>Why the carrier is a Pet-scoped collection.</b> The effect's declared
/// <c>target</c> is <c>Pet</c> (<c>RELIC_RULES.md</c> §8.3's
/// <c>BurnDamage</c> row), and §6 note 1 reads that value as the <b>owner/source
/// context</b>: the modifier is the Pet's own, and it scales the Burn damage the
/// Pet owns. It is therefore held on <see cref="PetState"/> beside the other
/// applied Relic modifiers, not on the Boss and not on a Burn instance — which
/// is also what keeps it out of <c>COMBAT_RULES.md</c> §5.2 item 4's forbidden
/// shapes (it creates no Burn event, no Burn instance, and no duration change).
///
/// <b>Ownership scoping is the Burn instance's own source.</b>
/// <c>COMBAT_RULES.md</c> §5.2 item 4: "a <c>BurnDamage</c> modifier applies only
/// to Burn instances <b>owned by the source that modifier belongs to</b>", and
/// §6 note 1 fixes which instances those are — Pet-owned Burn is modified,
/// Boss-owned Burn is not, and the distinction is never the entity receiving the
/// tick's damage (TASK-178 Product Owner decision <b>Q-4 = C</b>).
/// <see cref="AppliesTo"/> is that rule in one predicate: it is the smallest
/// existing source/owner representation (<see cref="StatusEffect.Source"/>) and
/// introduces no second ownership field.
///
/// <b>Every operation is a pure, deterministic function.</b> No RNG, no clock,
/// no I/O, no framework dependency, and no ambient state
/// (<c>ARCHITECTURE.md</c> §2.1, <c>TDD.md</c> §6). The caller sequences the
/// calls and performs the one post-resolution write-back
/// (<c>GAME_STATE.md</c> §5.1).
///
/// <b>What this type deliberately does not do.</b>
/// <list type="bullet">
/// <item>It does not expire anything at a Turn boundary. The declared lifetime is
/// <c>Battle</c> and neither §2.3.7 nor §5.1.4 makes a <c>Battle</c> modifier
/// Turn-based; its boundary is the source's removal or the battle's end.</item>
/// <item>It does not touch a Burn instance: it neither creates, refreshes,
/// extends, nor consumes one, and it emits no event
/// (<c>COMBAT_RULES.md</c> §5.2 item 4). The Burn instance and its duration
/// remain <c>StatusEffectLifecycle</c>'s.</item>
/// <item>It does not serialize. The collection rides the authoritative
/// <c>BattleState</c> record; the round trip is the serializer's obligation, on
/// the same terms as the sibling modifier collections.</item>
/// </list>
/// </summary>
public static class BurnDamageModifiers
{
    /// <summary>
    /// Which side's Burn a Pet-held <c>BurnDamage</c> modifier owns.
    ///
    /// <b>It is the Pet's own side, and that is the whole of the ownership
    /// test.</b> <c>RELIC_RULES.md</c> §6 note 1: "the Pet applies Burn to Boss →
    /// the Pet is the source/owner → Burning Curse applies", and "<c>target:
    /// Pet</c> identifies the Pet as the owned/source context of this modifier".
    /// A Status Effect's source is the side that applied it
    /// (<see cref="StatusEffect.Source"/>, <c>GAME_STATE.md</c> §2.3.1 item 7:
    /// <c>"player" | "boss"</c>, where <c>player</c> is the Pet's side —
    /// <c>ADR-011</c> items 3 and 5), so a Burn the Pet owns is the one labelled
    /// <see cref="StatusEffectSource.Player"/>.
    /// </summary>
    public const StatusEffectSource OwnedBurnSource = StatusEffectSource.Player;

    /// <summary>
    /// Whether a Pet-held <c>BurnDamage</c> modifier reaches
    /// <paramref name="burn"/> — the ownership scoping of
    /// <c>COMBAT_RULES.md</c> §5.2 item 4 and <c>RELIC_RULES.md</c> §6 note 1.
    ///
    /// <code>
    /// Pet-owned Burn   (Source = Player)  → the modifier applies
    /// Boss-owned Burn  (Source = Boss)    → it does NOT apply
    /// </code>
    ///
    /// <b>The test is the instance's source, never the damage recipient.</b>
    /// §6 note 1 is explicit that "the distinction is Burn source/ownership, not
    /// the damage recipient", so a Boss-owned Burn ticking <i>on the Pet</i> is
    /// outside a Pet-scoped modifier while a Pet-owned Burn ticking <i>on the
    /// Boss</i> is inside it. Reading the recipient — or the collection the
    /// instance happens to sit in — would invert the documented result.
    ///
    /// <b>Only a damage-over-time Burn is a Burn tick.</b> The instance's
    /// <see cref="StatusEffect.Type"/> must be <see cref="StatusEffectType.DoT"/>:
    /// <c>COMBAT_RULES.md</c> §5.2 item 4 modifies "Burn damage", and §5.1 gives
    /// the damage-over-time category to <c>DoT</c> alone, so a Shield or a
    /// BuffDebuff is not a Burn tick whatever its identity token.
    /// </summary>
    /// <param name="burn">The Status Effect instance a tick would resolve against.</param>
    public static bool AppliesTo(StatusEffect burn) =>
        burn.Type == StatusEffectType.DoT
        && burn.Source == OwnedBurnSource;

    /// <summary>
    /// Applies or refreshes a Burn-damage modifier on a collection.
    ///
    /// <b>One element per source identity; a repeat application refreshes.</b>
    /// The collection's storage invariant is the sibling modifiers': applying
    /// appends one element when its identity is not already present, and
    /// otherwise <b>sets that existing element's</b>
    /// <see cref="BurnDamageModifier.BurnDamagePercentage"/> to the newly applied
    /// value. It never duplicates and never accumulates, which is what makes
    /// Burning Curse's "non-stacking, once per battle" contract
    /// (<c>RELIC_RULES.md</c> §6 note 1, §8.5 item 5) hold at this layer: the
    /// source's re-evaluation resolves to the same identity and therefore
    /// refreshes its one element.
    ///
    /// <b>Refresh uses the newly applied value.</b> A re-application replaces the
    /// stored percentage with the value being applied; it does not preserve the
    /// previous value, does not add the two, and does not leave the entry
    /// unchanged — the same operation §5.1.3 items 1–2 define for
    /// <c>CardCostModifiers[]</c> and §5.1.4 items 1–2 for
    /// <c>ATKModifiers[]</c>.
    ///
    /// <b>The element's position is preserved on refresh.</b> The order is
    /// written order, as <c>GAME_STATE.md</c> §2.3.6 item 6 fixes for the sibling
    /// Card-cost collection: no rule reads element positions, and preserving them
    /// is what makes a round trip a no-op and two serializations of one state
    /// byte-identical.
    /// </summary>
    /// <param name="modifiers">
    /// The Pet's current active modifiers. Always a collection, never
    /// <c>null</c>: "no modifier active" is an <b>empty collection</b>, and
    /// absence of the collection is not a representable state.
    /// </param>
    /// <param name="modifier">
    /// The modifier to apply or refresh. Its
    /// <see cref="BurnDamageModifier.SourceIdentity"/> is the identity this
    /// operation de-duplicates on and its
    /// <see cref="BurnDamageModifier.BurnDamagePercentage"/> is the value a
    /// refresh sets.
    /// </param>
    /// <returns>
    /// The resulting collection: the same elements in the same order, with the
    /// matching element replaced in place, or <paramref name="modifier"/>
    /// appended when no element carried its identity.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="modifiers"/> is <c>null</c>, or <paramref name="modifier"/>
    /// carries no usable source identity.
    /// </exception>
    public static BurnDamageModifier[] Apply(
        IReadOnlyList<BurnDamageModifier> modifiers,
        BurnDamageModifier modifier)
    {
        ArgumentNullException.ThrowIfNull(modifiers);

        if (!modifier.HasSourceIdentity)
        {
            throw new ArgumentException(
                "A Burn-damage modifier's SourceIdentity is its replace/refresh and "
                + "removal key and must be a non-empty stable source identity.",
                nameof(modifier));
        }

        var result = new BurnDamageModifier[modifiers.Count + 1];
        var replaced = false;

        for (var index = 0; index < modifiers.Count; index++)
        {
            var existing = modifiers[index];

            // An apply of an already-active source identity REFRESHES that element
            // — the newly applied percentage replaces the old, in place, with no
            // second element and no accumulation.
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
    /// Removes a specific source's modifier.
    ///
    /// <b>Removal removes only that source's element.</b> Removing a modifier
    /// deletes the identified element; it never recomputes, re-derives, or
    /// normalizes a remaining element, and it touches no Burn instance and no
    /// stored stat. Another source's modifier is carried across untouched, in
    /// order.
    ///
    /// <b>Expiry is a removal, not a stored zero or a stored flag.</b> Absence
    /// means "not active", so no zero-percentage element stands in for a removed
    /// one — a source carrying a genuine <c>0</c> is a real modifier and is not
    /// removed by this operation.
    ///
    /// <b>Removing a source that is not present is an idempotent no-op.</b> The
    /// collection is returned unchanged, which is the documented "already not
    /// active" state rather than an error.
    /// </summary>
    /// <param name="modifiers">The Pet's active modifiers.</param>
    /// <param name="sourceIdentity">
    /// The identity of the source whose element is removed. Every other source's
    /// element is carried across unchanged.
    /// </param>
    /// <returns>The resulting collection with the matching element removed.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="modifiers"/> is <c>null</c>, or
    /// <paramref name="sourceIdentity"/> is <c>null</c>.
    /// </exception>
    public static BurnDamageModifier[] Remove(
        IReadOnlyList<BurnDamageModifier> modifiers,
        string sourceIdentity)
    {
        ArgumentNullException.ThrowIfNull(modifiers);
        ArgumentNullException.ThrowIfNull(sourceIdentity);

        var remaining = new List<BurnDamageModifier>(modifiers.Count);

        foreach (var modifier in modifiers)
        {
            if (!string.Equals(modifier.SourceIdentity, sourceIdentity, StringComparison.Ordinal))
            {
                remaining.Add(modifier);
            }
        }

        return [.. remaining];
    }

    /// <summary>
    /// The combined percentage points every active modifier contributes to a
    /// Pet-owned Burn tick — the value a tick's damage is scaled by.
    ///
    /// <b>Multiple active sources compose additively.</b> The collection admits
    /// one element per source, and two elements therefore mean two distinct
    /// sources. They sum, which is the composition both already-decided Relic
    /// modifier types use — <c>CARD_RULES.md</c> §3.6's additive
    /// <c>CardCost</c> reduction and <c>COMBAT_RULES.md</c> §5.6.2/§5.6.6's
    /// additive signed <c>ATK</c> percentage — so a modifier's contribution is
    /// independent of every other source's and removing one source disturbs the
    /// other's contribution not at all. (An empty collection sums to <c>0</c>,
    /// which is the documented "no modifier active" contribution.)
    ///
    /// <b>It is the percentage, not the multiplier.</b> This is the value
    /// <c>RELIC_RULES.md</c> §8.2 item 2's <c>Percentage</c> interpretation is
    /// read against — "a proportion of the stat's own value" — so the caller
    /// scales the tick's own damage by <c>(100 + this) / 100</c> and the stored
    /// magnitudes are never pre-resolved (§8.2 item 2). No value is clamped:
    /// nothing here authors a cap for it.
    /// </summary>
    /// <param name="modifiers">The Pet's active modifiers.</param>
    /// <returns>The signed sum of the active contributions, in percentage points.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="modifiers"/> is <c>null</c>.</exception>
    public static int AppliedPercentage(IReadOnlyList<BurnDamageModifier> modifiers)
    {
        ArgumentNullException.ThrowIfNull(modifiers);

        var total = 0;

        for (var index = 0; index < modifiers.Count; index++)
        {
            total += modifiers[index].BurnDamagePercentage;
        }

        return total;
    }

    /// <summary>
    /// Whether two modifier collections hold the same elements in the same order
    /// — the structural comparison a round-trip obligation requires.
    ///
    /// <b>Why a comparison method is needed.</b> The collections are arrays and a
    /// record's default equality compares them by reference, so two states a
    /// round trip made hold the same elements would compare unequal on that
    /// member alone. This is the same need
    /// <see cref="CardCostModifiers.ModifiersEqual"/> answers for its collection
    /// and <see cref="ATKModifiers.ModifiersEqual"/> for the ATK one.
    /// </summary>
    /// <param name="left">One collection.</param>
    /// <param name="right">The other collection.</param>
    /// <returns>Whether the two collections are element-for-element equal.</returns>
    public static bool ModifiersEqual(
        IReadOnlyList<BurnDamageModifier> left,
        IReadOnlyList<BurnDamageModifier> right)
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

        // BurnDamageModifier is a record struct, so its own equality already
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
