using GameServer.Domain.Battle;
using Xunit;

namespace GameServer.Domain.Tests.Battle;

/// <summary>
/// The <c>CardCostModifiers[]</c> state mutation lifecycle — apply/refresh and removal
/// (<c>GAME_STATE.md</c> §2.3.5, §5.1.3; <c>TASK-134</c> D6/D7).
///
/// Every expected value below is derived from the cited section, not from what the
/// implementation currently does.
/// </summary>
public sealed class CardCostModifierTests
{
    // =======================================================================
    // §5.1.3 item 1 — application appends, and never duplicates an identity
    // =======================================================================

    [Fact]
    public void Apply_OnEmptyCollection_AppendsOneElement()
    {
        // §5.1.3 item 1: applying a modifier appends one element when its
        // SourceIdentity is not already present.
        var result = CardCostModifiers.Apply([], new CardCostModifier("emergency-core", 50));

        var modifier = Assert.Single(result);
        Assert.Equal("emergency-core", modifier.SourceIdentity);
        Assert.Equal(50, modifier.CostReductionPercentage);
    }

    [Fact]
    public void Apply_SameSourceIdentity_RefreshesInsteadOfAppending()
    {
        // §5.1.3 item 1: "Two elements with the same SourceIdentity are never observable
        // in a committed state." §5.1.3 item 2 adds that continuous re-evaluation of one
        // source's condition is a replace/refresh of that one element — "There is no
        // counter, no stack, no queue, and no second entry."
        var first = CardCostModifiers.Apply([], new CardCostModifier("emergency-core", 50));

        var second = CardCostModifiers.Apply(first, new CardCostModifier("emergency-core", 50));

        var modifier = Assert.Single(second);
        Assert.Equal(50, modifier.CostReductionPercentage);
        Assert.NotEqual(100, modifier.CostReductionPercentage);
    }

    [Fact]
    public void Apply_SameSourceWithANewValue_SetsTheNewlyAppliedValue()
    {
        // §5.1.3 item 1: a re-application "sets that existing element's
        // CostReductionPercentage to the newly applied value" — a set, not an increment.
        // TASK-140's worked example: [sourceA, 5] then [sourceA, 10] results in
        // [sourceA, 10], not two entries.
        var first = CardCostModifiers.Apply([], new CardCostModifier("sourceA", 5));

        var refreshed = CardCostModifiers.Apply(first, new CardCostModifier("sourceA", 10));

        var modifier = Assert.Single(refreshed);
        Assert.Equal("sourceA", modifier.SourceIdentity);
        Assert.Equal(10, modifier.CostReductionPercentage);
    }

    [Fact]
    public void Apply_SameSourceWithALowerValue_StillSetsTheNewlyAppliedValue()
    {
        // §5.1.3 item 1 makes the refresh a set of the applied value, not a maximize.
        // §5.1.3 item 2's re-evaluation therefore reports the source's CURRENT
        // contribution rather than the strongest one it ever had.
        var first = CardCostModifiers.Apply([], new CardCostModifier("sourceA", 50));

        var refreshed = CardCostModifiers.Apply(first, new CardCostModifier("sourceA", 25));

        Assert.Equal(25, Assert.Single(refreshed).CostReductionPercentage);
    }

    [Fact]
    public void Apply_DifferentSources_CoexistIndependently()
    {
        // §2.3.5 item 3: "two simultaneous elements always mean two distinct sources,
        // which is what makes independent sources independently composable and
        // independently removable."
        var one = CardCostModifiers.Apply([], new CardCostModifier("sourceA", 5));

        var both = CardCostModifiers.Apply(one, new CardCostModifier("sourceB", 50));

        Assert.Equal(2, both.Length);
        Assert.Contains(both, m => m.SourceIdentity == "sourceA" && m.CostReductionPercentage == 5);
        Assert.Contains(both, m => m.SourceIdentity == "sourceB" && m.CostReductionPercentage == 50);
    }

    [Fact]
    public void Apply_RepeatedReEvaluationOfOneSource_NeverAccumulates()
    {
        // §5.1.3 item 2 / §2.3.5 item 5: "In particular there is no stack count: a
        // repeated application from one source is a replace/refresh (§5.1.3 item 1),
        // never an increment of a counter." Emergency Core's §6 note 2 is the
        // provisioned case, so many evaluations must leave exactly one element carrying
        // the source's single value — not 50, 100, 150, or a counter.
        var collection = CardCostModifiers.Apply([], new CardCostModifier("emergency-core", 50));

        for (var evaluation = 0; evaluation < 5; evaluation++)
        {
            collection = CardCostModifiers.Apply(collection, new CardCostModifier("emergency-core", 50));
        }

        var modifier = Assert.Single(collection);
        Assert.Equal(50, modifier.CostReductionPercentage);
    }

    [Fact]
    public void Apply_DoesNotNormalizeASign()
    {
        // §2.3.5 item 4 stores the source's percentage-point reduction and §2.3.5 item 1
        // makes this collection not a second representation of anything: the mapping and
        // the lifecycle copy the value. It is typed but not interpreted, so a negative
        // value is stored as given rather than rewritten to an absolute value — the
        // composition that gives it meaning is CARD_RULES.md §3.6's.
        var result = CardCostModifiers.Apply([], new CardCostModifier("source", -50));

        Assert.Equal(-50, Assert.Single(result).CostReductionPercentage);
    }

    [Fact]
    public void Apply_PreservesAZeroPercentageAsARealMutation()
    {
        // §2.3.5 item 6: an empty collection is the "no modifier is active" statement and
        // "there is no sentinel element, no null, and no omitted member standing in for
        // it". A source that genuinely applies 0 is therefore a real, present modifier —
        // it is appended rather than dropped, and it is not a removed element's stand-in.
        var result = CardCostModifiers.Apply([], new CardCostModifier("neutral-source", 0));

        var modifier = Assert.Single(result);
        Assert.Equal("neutral-source", modifier.SourceIdentity);
        Assert.Equal(0, modifier.CostReductionPercentage);
    }

    [Fact]
    public void Apply_BlankSourceIdentity_IsRejected()
    {
        // §2.3.5 item 7: "SourceIdentity is a non-empty string", and "A malformed element
        // is not silently repaired, defaulted, or dropped". A blank identity would let
        // two sources collide on the replace/refresh key, so it is rejected rather than
        // admitted.
        Assert.Throws<ArgumentException>(() =>
            CardCostModifiers.Apply([], new CardCostModifier("   ", 50)));
    }

    [Fact]
    public void Apply_NullCollection_IsRejected()
    {
        // §2.3.5 item 6: absence of the collection is not a representable state, so null
        // is rejected rather than treated as empty.
        Assert.Throws<ArgumentNullException>(() =>
            CardCostModifiers.Apply(null!, new CardCostModifier("source", 50)));
    }

    // =======================================================================
    // §2.3.5 item 8 / §2.3.6 item 6 — order is preserved, and is NOT sorted
    // =======================================================================

    [Fact]
    public void Apply_PreservesTheRefreshedElementsPosition()
    {
        // §2.3.6 item 6: "Order is preserved for round-trip fidelity, not for semantics."
        // §2.3.5 item 8: "a round trip must still return the elements in the order they
        // were written." A refresh therefore replaces in place and does not reorder.
        // This is the deliberate contrast with ATKModifiers, whose order is the §2.3.7
        // item 7 identity sort.
        var collection = CardCostModifiers.Apply([], new CardCostModifier("source-c", 5));
        collection = CardCostModifiers.Apply(collection, new CardCostModifier("source-a", 10));
        collection = CardCostModifiers.Apply(collection, new CardCostModifier("source-b", 15));

        var refreshed = CardCostModifiers.Apply(collection, new CardCostModifier("source-a", 30));

        Assert.Equal(["source-c", "source-a", "source-b"], refreshed.Select(m => m.SourceIdentity));
        Assert.Equal(30, refreshed[1].CostReductionPercentage);
    }

    [Fact]
    public void Apply_KeepsWrittenOrder_RatherThanSortingBySourceIdentity()
    {
        // §2.3.5 item 8 makes ordering non-semantic and §2.3.6 item 6 makes it
        // preservation rather than sorting — so this collection must NOT adopt the
        // sibling ATKModifiers collection's deterministic identity sort. A sorted
        // implementation would rewrite this array and fail the round-trip obligation.
        var applied = CardCostModifiers.Apply([], new CardCostModifier("source-c", 5));
        applied = CardCostModifiers.Apply(applied, new CardCostModifier("source-a", 10));

        Assert.Equal(["source-c", "source-a"], applied.Select(m => m.SourceIdentity));
    }

    // =======================================================================
    // §5.1.3 item 4 — source-specific removal
    // =======================================================================

    [Fact]
    public void Remove_DeletesOnlyTheNamedSource()
    {
        // §5.1.3 item 4: "It removes only that source's element: another source's
        // modifier is untouched." TASK-140's worked example: [sourceA, 5] and
        // [sourceB, 10], remove sourceA -> [sourceB, 10].
        CardCostModifier[] collection =
        [
            new("sourceA", 5),
            new("sourceB", 10),
        ];

        var remaining = CardCostModifiers.Remove(collection, "sourceA");

        var modifier = Assert.Single(remaining);
        Assert.Equal("sourceB", modifier.SourceIdentity);
        Assert.Equal(10, modifier.CostReductionPercentage);
    }

    [Fact]
    public void Remove_OfASourceThatIsNotPresent_IsAnIdempotentNoOp()
    {
        // TASK-140's edge case: removing a SourceIdentity the array does not hold is an
        // idempotent no-op. §2.3.5 item 6 makes absence the representation of "no
        // modifier is active", so a removal that finds nothing has nothing to do.
        CardCostModifier[] collection =
        [
            new("sourceA", 5),
            new("sourceB", 10),
        ];

        var once = CardCostModifiers.Remove(collection, "absent-source");
        var twice = CardCostModifiers.Remove(once, "absent-source");

        Assert.Equal(2, once.Length);
        Assert.True(CardCostModifiers.ModifiersEqual(collection, once));
        Assert.True(CardCostModifiers.ModifiersEqual(once, twice));
    }

    [Fact]
    public void Remove_OnAnEmptyCollection_YieldsAnEmptyCollection()
    {
        // §2.3.5 item 6: an empty collection is the "no modifier is active" state and is
        // a valid input, not an error.
        Assert.Empty(CardCostModifiers.Remove([], "sourceA"));
    }

    [Fact]
    public void Remove_PreservesTheOrderOfWhatItLeavesBehind()
    {
        // §2.3.6 item 6: the written order must survive, including a removal — elements
        // not removed are carried across unchanged and in order.
        CardCostModifier[] collection =
        [
            new("source-c", 5),
            new("source-a", 10),
            new("source-b", 15),
        ];

        var remaining = CardCostModifiers.Remove(collection, "source-a");

        Assert.Equal(["source-c", "source-b"], remaining.Select(m => m.SourceIdentity));
    }

    [Fact]
    public void Remove_OfAZeroPercentageSource_RemovesItLikeAnyOther()
    {
        // §5.1.3 item 4: "Expiry is a removal, not a stored zero or a stored flag ...
        // no zero-percentage element stands in for a removed one." The removal decision
        // is the source's identity, never its magnitude, so a source presenting 0 is
        // removed on the same terms as one presenting a non-zero value — and a removed
        // source leaves nothing behind.
        CardCostModifier[] collection =
        [
            new("sourceA", 0),
            new("sourceB", 10),
        ];

        var remaining = CardCostModifiers.Remove(collection, "sourceA");

        Assert.Equal(["sourceB"], remaining.Select(m => m.SourceIdentity));
    }

    [Fact]
    public void Remove_NullCollectionOrIdentity_IsRejected()
    {
        // §2.3.5 item 6 rejects a null collection; a null identity is not a source the
        // contract can match on.
        Assert.Throws<ArgumentNullException>(() => CardCostModifiers.Remove(null!, "sourceA"));
        Assert.Throws<ArgumentNullException>(() =>
            CardCostModifiers.Remove([new CardCostModifier("sourceA", 5)], null!));
    }

    // =======================================================================
    // §2.3.6 item 5 — the structural comparison the round trip needs
    // =======================================================================

    [Fact]
    public void ModifiersEqual_ComparesElementsAndOrder()
    {
        // §2.3.6 item 5: a round trip must return "the same elements, the same
        // sourceIdentity and costReductionPercentage values, the same element order, and
        // the same element count".
        CardCostModifier[] left = [new("source-a", 5), new("source-b", 50)];
        CardCostModifier[] same = [new("source-a", 5), new("source-b", 50)];
        CardCostModifier[] reordered = [new("source-b", 50), new("source-a", 5)];
        CardCostModifier[] differentValue = [new("source-a", 5), new("source-b", 51)];
        CardCostModifier[] collapsed = [new("source-a", 5)];

        Assert.True(CardCostModifiers.ModifiersEqual(left, same));
        Assert.False(CardCostModifiers.ModifiersEqual(left, reordered));
        Assert.False(CardCostModifiers.ModifiersEqual(left, differentValue));
        Assert.False(CardCostModifiers.ModifiersEqual(left, collapsed));
    }

    [Fact]
    public void ModifiersEqual_OnTwoEmptyCollections_IsTrue()
    {
        // §2.3.6 item 5: "Because an empty collection round-trips as an empty collection,
        // the no-modifier state is preserved too."
        Assert.True(CardCostModifiers.ModifiersEqual([], []));
    }

    // =======================================================================
    // §2.3.5 item 2 / §5.1.3 item 5 — placement, lifetime, and the untouched cost
    // =======================================================================

    [Fact]
    public void PetState_AtBattleCreation_HoldsAnEmptyCardCostModifierCollection()
    {
        // §2.3.5 item 6 / §5.1.3 item 5: the collection begins empty, because a modifier
        // is applied by a source whose Trigger+Condition is met at GAME_RULES.md §17
        // step 11 during a resolution, and battle creation resolves nothing. §5.1.3
        // item 5 adds that it "needs no separate expiry step: a new battle is a new
        // BattleState with an empty collection".
        var state = BattleState.CreateWith("battle-cardcost-carrier", 4242);

        Assert.NotNull(state.PetState.CardCostModifiers);
        Assert.Empty(state.PetState.CardCostModifiers);
    }

    [Fact]
    public void PetState_ApplyingAModifier_LeavesTheAuthoredPowerCostUntouched()
    {
        // §2.3.5's preamble: the collection "exists so a Relic can reduce a Card's cost
        // for the duration of the battle without mutating authored
        // CardDefinition.PowerCost, without a second cost representation, and without
        // overwriting anything". Applying a modifier adds a contribution to the
        // collection and changes no Card definition and no other PetState member.
        var state = BattleState.CreateWith("battle-cardcost-authored", 4242);
        var before = state.PetState;

        var applied = before with
        {
            CardCostModifiers = CardCostModifiers.Apply(
                before.CardCostModifiers,
                new CardCostModifier("emergency-core", 50)),
        };

        Assert.Equal(before.EquippedCards, applied.EquippedCards);
        Assert.Equal(before.Power, applied.Power);
        Assert.Equal(before.PetId, applied.PetId);
        Assert.Single(applied.CardCostModifiers);
    }
}
