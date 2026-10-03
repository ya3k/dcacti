using GameServer.Domain.Battle;
using Xunit;

namespace GameServer.Domain.Tests.Battle;

/// <summary>
/// The <c>ATKModifiers[]</c> state mutation lifecycle — apply/refresh and removal
/// (<c>GAME_STATE.md</c> §2.3.7, §5.1.4; <c>TASK-136</c> D2/D7/D8).
///
/// Every expected value below is derived from the cited section, not from what the
/// implementation currently does.
/// </summary>
public sealed class ATKModifierTests
{
    // =======================================================================
    // §5.1.4 item 1 — application appends, and never duplicates an identity
    // =======================================================================

    [Fact]
    public void Apply_OnEmptyCollection_AppendsOneElement()
    {
        // §5.1.4 item 1: applying a modifier appends one element when its
        // SourceIdentity is not already present.
        var result = ATKModifiers.Apply([], new ATKModifier("berserker-core", 5));

        var modifier = Assert.Single(result);
        Assert.Equal("berserker-core", modifier.SourceIdentity);
        Assert.Equal(5, modifier.ATKModifierPercentage);
    }

    [Fact]
    public void Apply_SameSourceIdentity_RefreshesInsteadOfAppending()
    {
        // §2.3.7 item 4: "A collection containing two entries with the same
        // SourceIdentity is therefore invalid state." §5.1.4 item 1: "Two elements
        // with the same SourceIdentity are never observable in a committed state."
        var first = ATKModifiers.Apply([], new ATKModifier("berserker-core", 5));

        var second = ATKModifiers.Apply(first, new ATKModifier("berserker-core", 5));

        var modifier = Assert.Single(second);
        Assert.Equal(5, modifier.ATKModifierPercentage);
        Assert.NotEqual(10, modifier.ATKModifierPercentage);
    }

    [Fact]
    public void Apply_SameSourceWithANewValue_SetsTheNewlyAppliedValue()
    {
        // §5.1.4 item 2 / TASK-136 D7: a re-application "replaces the stored
        // ATKModifierPercentage with the value being applied; it does not preserve the
        // previously stored value, does not add the two, and does not leave the entry
        // unchanged." TASK-140's worked example: [sourceA, 5] then [sourceA, 10]
        // results in [sourceA, 10], not two entries.
        var first = ATKModifiers.Apply([], new ATKModifier("sourceA", 5));

        var refreshed = ATKModifiers.Apply(first, new ATKModifier("sourceA", 10));

        var modifier = Assert.Single(refreshed);
        Assert.Equal("sourceA", modifier.SourceIdentity);
        Assert.Equal(10, modifier.ATKModifierPercentage);
    }

    [Fact]
    public void Apply_SameSourceWithALowerValue_StillSetsTheNewlyAppliedValue()
    {
        // §5.1.4 item 2 makes the refresh a set of the applied value, not a maximize
        // and not an accumulate — so a smaller re-application wins. This is the
        // property that makes the per-source state the source's CURRENT contribution.
        var first = ATKModifiers.Apply([], new ATKModifier("sourceA", 10));

        var refreshed = ATKModifiers.Apply(first, new ATKModifier("sourceA", 5));

        Assert.Equal(5, Assert.Single(refreshed).ATKModifierPercentage);
    }

    [Fact]
    public void Apply_DifferentSources_CoexistIndependently()
    {
        // §2.3.7 item 3: "Two different sources must coexist (their identities differ),
        // and a source re-applying must not accumulate (its own identity collides)."
        // TASK-140's requirement that SourceA -> +5% and SourceB -> -30% be
        // representable simultaneously.
        var one = ATKModifiers.Apply([], new ATKModifier("sourceA", 5));

        var both = ATKModifiers.Apply(one, new ATKModifier("sourceB", -30));

        Assert.Equal(2, both.Length);
        Assert.Contains(both, m => m.SourceIdentity == "sourceA" && m.ATKModifierPercentage == 5);
        Assert.Contains(both, m => m.SourceIdentity == "sourceB" && m.ATKModifierPercentage == -30);
    }

    [Fact]
    public void Apply_PreservesASignedNegativePercentageExactly()
    {
        // §2.3.7 item 5: "ATKModifierPercentage is a signed value". A decrease is a
        // negative contribution and must survive unchanged — it is NOT interpreted,
        // stored as an absolute value, or clamped here.
        var result = ATKModifiers.Apply([], new ATKModifier("debuffer", -30));

        Assert.Equal(-30, Assert.Single(result).ATKModifierPercentage);
    }

    [Fact]
    public void Apply_PreservesAZeroPercentageAsARealMutation()
    {
        // §2.3.7 item 6: "never represented by a stored zero or a stored inactive
        // flag" — the prohibition is on a zero STANDING IN for "not active". A source
        // that genuinely applies 0 is a real, present modifier, so it is appended and
        // is not silently dropped or treated as a removal.
        var result = ATKModifiers.Apply([], new ATKModifier("neutral-source", 0));

        var modifier = Assert.Single(result);
        Assert.Equal("neutral-source", modifier.SourceIdentity);
        Assert.Equal(0, modifier.ATKModifierPercentage);
    }

    [Fact]
    public void Apply_BlankSourceIdentity_IsRejected()
    {
        // §2.3.7 item 3: the identity is the replace/refresh and removal key. A blank
        // identity would let two different sources collide on it, which destroys the
        // one property the member exists to provide, so it is rejected rather than
        // admitted.
        Assert.Throws<ArgumentException>(() =>
            ATKModifiers.Apply([], new ATKModifier("   ", 5)));
    }

    [Fact]
    public void Apply_NullCollection_IsRejected()
    {
        // §2.3.7 item 6: absence of the collection is not a representable state, so
        // null is rejected rather than treated as empty.
        Assert.Throws<ArgumentNullException>(() =>
            ATKModifiers.Apply(null!, new ATKModifier("source", 5)));
    }

    // =======================================================================
    // §2.3.7 item 7 — element order is deterministic: by SourceIdentity
    // =======================================================================

    [Fact]
    public void Apply_OrdersTheCollectionBySourceIdentity()
    {
        // §2.3.7 item 7: "Element order is deterministic: by SourceIdentity ... this
        // collection's order is a deterministic sort on the identity key, so the
        // serialized order is reproducible from the element set alone."
        var applied = ATKModifiers.Apply([], new ATKModifier("source-c", 5));
        applied = ATKModifiers.Apply(applied, new ATKModifier("source-a", 10));
        applied = ATKModifiers.Apply(applied, new ATKModifier("source-b", -30));

        Assert.Equal(["source-a", "source-b", "source-c"], applied.Select(m => m.SourceIdentity));
    }

    [Fact]
    public void Apply_ProducesTheSameOrderRegardlessOfApplicationOrder()
    {
        // §2.3.7 item 7 / REDIS_STATE.md §7 item 16: the order is "reproducible from the
        // element set alone", so two different application orders that produce the same
        // element SET must produce the same ARRAY — which is what makes two
        // serializations of the same state byte-identical and lets a store be relieved
        // of preserving insertion order.
        var ascending = ATKModifiers.Apply([], new ATKModifier("alpha", 1));
        ascending = ATKModifiers.Apply(ascending, new ATKModifier("beta", 2));
        ascending = ATKModifiers.Apply(ascending, new ATKModifier("gamma", 3));

        var shuffled = ATKModifiers.Apply([], new ATKModifier("gamma", 3));
        shuffled = ATKModifiers.Apply(shuffled, new ATKModifier("alpha", 1));
        shuffled = ATKModifiers.Apply(shuffled, new ATKModifier("beta", 2));

        Assert.Equal(
            ascending.Select(m => m.SourceIdentity),
            shuffled.Select(m => m.SourceIdentity));
    }

    [Fact]
    public void Apply_RefreshingASourceKeepsTheCollectionOrdered()
    {
        // §2.3.7 item 7's order is a property of the element set, not of when each
        // element was last written — so a refresh cannot leave the collection out of
        // order. This is the deliberate difference from the sibling collections, whose
        // order records application order and whose Apply therefore preserves position.
        var first = ATKModifiers.Apply([], new ATKModifier("source-a", 5));
        var second = ATKModifiers.Apply(first, new ATKModifier("source-b", 10));

        var refreshed = ATKModifiers.Apply(second, new ATKModifier("source-b", 20));

        Assert.Equal(["source-a", "source-b"], refreshed.Select(m => m.SourceIdentity));
        Assert.Equal(20, refreshed[1].ATKModifierPercentage);
    }

    [Fact]
    public void Apply_OrdersByOrdinalCodePoints_NotByCulture()
    {
        // §2.3.7 item 7 requires the order to be reproducible; TDD.md §6 requires the
        // resulting state to be reproducible for a given input state. A culture-
        // sensitive comparison would make the persisted order depend on the host's
        // ambient culture rather than on battle input, so the comparison is ordinal.
        // 'Z' (U+005A) sorts before 'a' (U+0061) ordinally, but after it under most
        // culture-aware comparisons.
        var applied = ATKModifiers.Apply([], new ATKModifier("a-source", 1));
        applied = ATKModifiers.Apply(applied, new ATKModifier("Z-source", 2));

        Assert.Equal(["Z-source", "a-source"], applied.Select(m => m.SourceIdentity));
    }

    // =======================================================================
    // §5.1.4 item 4 — source-specific removal
    // =======================================================================

    [Fact]
    public void Remove_DeletesOnlyTheNamedSource()
    {
        // §5.1.4 item 4: "It removes only that source's element: another source's
        // modifier is untouched." TASK-140's worked example: [sourceA, 5] and
        // [sourceB, 10], remove sourceA -> [sourceB, 10].
        ATKModifier[] collection =
        [
            new("sourceA", 5),
            new("sourceB", 10),
        ];

        var remaining = ATKModifiers.Remove(collection, "sourceA");

        var modifier = Assert.Single(remaining);
        Assert.Equal("sourceB", modifier.SourceIdentity);
        Assert.Equal(10, modifier.ATKModifierPercentage);
    }

    [Fact]
    public void Remove_OfASourceThatIsNotPresent_IsAnIdempotentNoOp()
    {
        // TASK-140's edge case: removing a SourceIdentity the array does not hold is an
        // idempotent no-op. §2.3.7 item 6 makes absence the representation of "not
        // active", so a removal that finds nothing has nothing to do — it is not an
        // error and it changes neither the elements nor their values.
        ATKModifier[] collection =
        [
            new("sourceA", 5),
            new("sourceB", 10),
        ];

        var once = ATKModifiers.Remove(collection, "absent-source");
        var twice = ATKModifiers.Remove(once, "absent-source");

        Assert.Equal(2, once.Length);
        Assert.True(ATKModifiers.ModifiersEqual(collection, once));
        Assert.True(ATKModifiers.ModifiersEqual(once, twice));
    }

    [Fact]
    public void Remove_OnAnEmptyCollection_YieldsAnEmptyCollection()
    {
        // §2.3.7 item 6: an empty collection is the "no active ATK modifier" state and
        // is a valid input, not an error — so removing from it stays empty rather than
        // producing null or a sentinel element.
        Assert.Empty(ATKModifiers.Remove([], "sourceA"));
    }

    [Fact]
    public void Remove_KeepsTheCollectionOrderedBySourceIdentity()
    {
        // §2.3.7 item 7: the deterministic order is a property of the element set, so it
        // survives a removal and does not depend on the incoming array's arrangement.
        ATKModifier[] unordered =
        [
            new("source-c", 30),
            new("source-a", 10),
            new("source-b", 20),
        ];

        var remaining = ATKModifiers.Remove(unordered, "source-b");

        Assert.Equal(["source-a", "source-c"], remaining.Select(m => m.SourceIdentity));
    }

    [Fact]
    public void Remove_OfAZeroPercentageSource_RemovesItLikeAnyOther()
    {
        // §5.1.4 item 4: removal is the deletion of the identified element — the
        // decision is the source's identity, never its magnitude. A source presenting 0
        // is a present modifier (§2.3.7 item 6) and is removed on the same terms as one
        // presenting a non-zero value; no zero-percentage element is left standing in
        // for a removed one.
        ATKModifier[] collection =
        [
            new("sourceA", 0),
            new("sourceB", 10),
        ];

        var remaining = ATKModifiers.Remove(collection, "sourceA");

        Assert.Equal(["sourceB"], remaining.Select(m => m.SourceIdentity));
    }

    [Fact]
    public void Remove_NullCollectionOrIdentity_IsRejected()
    {
        // §2.3.7 item 6 rejects a null collection; a null identity is not a source the
        // contract can match on.
        Assert.Throws<ArgumentNullException>(() => ATKModifiers.Remove(null!, "sourceA"));
        Assert.Throws<ArgumentNullException>(() =>
            ATKModifiers.Remove([new ATKModifier("sourceA", 5)], null!));
    }

    // =======================================================================
    // §2.3.8 item 5 — the structural comparison the round trip needs
    // =======================================================================

    [Fact]
    public void ModifiersEqual_ComparesElementsAndOrder()
    {
        // §2.3.8 item 5: a round trip must return "the same elements, the same
        // sourceIdentity and atkModifierPercentage values, the same element order, and
        // the same element count".
        ATKModifier[] left = [new("source-a", 5), new("source-b", -30)];
        ATKModifier[] same = [new("source-a", 5), new("source-b", -30)];
        ATKModifier[] reordered = [new("source-b", -30), new("source-a", 5)];
        ATKModifier[] differentValue = [new("source-a", 5), new("source-b", -31)];
        ATKModifier[] collapsed = [new("source-a", 5)];

        Assert.True(ATKModifiers.ModifiersEqual(left, same));
        Assert.False(ATKModifiers.ModifiersEqual(left, reordered));
        Assert.False(ATKModifiers.ModifiersEqual(left, differentValue));
        Assert.False(ATKModifiers.ModifiersEqual(left, collapsed));
    }

    [Fact]
    public void ModifiersEqual_OnTwoEmptyCollections_IsTrue()
    {
        // §2.3.8 item 1: an empty collection round-trips as an empty collection, so the
        // no-modifier state must compare equal to itself.
        Assert.True(ATKModifiers.ModifiersEqual([], []));
    }

    // =======================================================================
    // §2.3.7 item 9 / §5.1.4 item 6 — the permanent stat is untouched
    // =======================================================================

    [Fact]
    public void PetState_AtBattleCreation_HoldsAnEmptyATKModifierCollection()
    {
        // §2.3.7 item 6 / §5.1.4 item 1: the collection begins empty, because a modifier
        // is applied by a source whose Trigger+Condition is met at GAME_RULES.md §17
        // step 11 during a resolution, and battle creation resolves nothing. It is never
        // null, so "no modifier" is the empty collection rather than an omission.
        var state = BattleState.CreateWith("battle-atk-carrier", 4242);

        Assert.NotNull(state.PetState.ATKModifiers);
        Assert.Empty(state.PetState.ATKModifiers);
    }

    [Fact]
    public void PetState_BaseATKIsNeverWrittenByAModifierApplication()
    {
        // §2.3.7 item 9 / §5.1.4 item 6: "PetState.ATK remains the permanent/base ATK
        // and is never mutated by an entry here", and the configured default is an
        // initialization value only — `PetState.ATK = DefaultATK` is forbidden.
        // Applying a modifier touches only the collection.
        var state = BattleState.CreateWith("battle-atk-base", 4242);
        var baseATK = state.PetState.ATK;

        var withModifier = state.PetState with
        {
            ATKModifiers = ATKModifiers.Apply(
                state.PetState.ATKModifiers,
                new ATKModifier("berserker-core", 5)),
        };

        Assert.Equal(baseATK, withModifier.ATK);
        Assert.Equal(PetState.DefaultATK, withModifier.ATK);
        Assert.Single(withModifier.ATKModifiers);
    }

    [Fact]
    public void PetState_BaseATKIsNeverWrittenByAModifierRemoval()
    {
        // §5.1.4 items 5–6: removal "recalculates the effective value; it never restores
        // a stored stat", because "the permanent stat was never changed by the modifier,
        // so there is nothing to undo". Removing therefore leaves ATK exactly as it was.
        var state = BattleState.CreateWith("battle-atk-removal", 4242);

        var withModifier = state.PetState with
        {
            ATKModifiers = ATKModifiers.Apply(
                state.PetState.ATKModifiers,
                new ATKModifier("berserker-core", 5)),
        };

        var afterRemoval = withModifier with
        {
            ATKModifiers = ATKModifiers.Remove(withModifier.ATKModifiers, "berserker-core"),
        };

        Assert.Equal(state.PetState.ATK, afterRemoval.ATK);
        Assert.Empty(afterRemoval.ATKModifiers);
    }
}
