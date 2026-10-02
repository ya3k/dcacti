using GameServer.Domain.Battle;
using Xunit;

namespace GameServer.Domain.Tests.Battle;

/// <summary>
/// The <c>NextAttackCritModifiers[]</c> state mutation lifecycle
/// (<c>GAME_STATE.md</c> §2.3.4, §5.1.2; <c>COMBAT_RULES.md</c> §3.3 items 7–10;
/// <c>ADR-017</c>).
///
/// Every expected value below is derived from the cited section, not from what the
/// implementation currently does.
/// </summary>
public sealed class NextAttackCritModifierTests
{
    [Fact]
    public void Apply_OnEmptyCollection_AppendsOneElement()
    {
        // §5.1.2 item 1: applying a modifier appends one element when its
        // SourceIdentity is not already present.
        var result = NextAttackCritModifiers.Apply(
            [],
            new NextAttackCritModifier("iron-fang", 10));

        var modifier = Assert.Single(result);
        Assert.Equal("iron-fang", modifier.SourceIdentity);
        Assert.Equal(10, modifier.CritContribution);
    }

    [Fact]
    public void Apply_SameSourceIdentity_RefreshesInsteadOfAppending()
    {
        // §5.1.2 item 1: "Two elements with the same SourceIdentity are never
        // observable in a committed state." A repeat application from one source
        // refreshes its own element — it does not stack a second one. This is the
        // behavior CARD_RULES.md §4.1's "for the next attack only" reading and
        // COMBAT_RULES.md §5.2 item 2's MVP refresh default both require.
        var first = NextAttackCritModifiers.Apply(
            [],
            new NextAttackCritModifier("iron-fang", 10));

        var second = NextAttackCritModifiers.Apply(
            first,
            new NextAttackCritModifier("iron-fang", 10));

        var modifier = Assert.Single(second);
        Assert.Equal(10, modifier.CritContribution);
        Assert.NotEqual(20, modifier.CritContribution);
    }

    [Fact]
    public void Apply_SameSourceWithNewContribution_SetsTheNewValue()
    {
        // §5.1.2 item 1: a refresh "sets that existing element's CritContribution to
        // the new value" — a set, not an increment (the same operation §5.1.1 item 1
        // defines for StatusEffects[]).
        var first = NextAttackCritModifiers.Apply(
            [],
            new NextAttackCritModifier("iron-fang", 10));

        var refreshed = NextAttackCritModifiers.Apply(
            first,
            new NextAttackCritModifier("iron-fang", 25));

        var modifier = Assert.Single(refreshed);
        Assert.Equal(25, modifier.CritContribution);
    }

    [Fact]
    public void Apply_DifferentSourceIdentities_CoexistAsSeparateElements()
    {
        // §2.3.4 item 2: the identity is per SOURCE, so two distinct sources hold two
        // elements. COMBAT_RULES.md §3.3 item 10 needs exactly this in order to apply
        // Iron Fang's and Bạch Hổ's contributions together while removing each
        // individually.
        var ironFang = NextAttackCritModifiers.Apply(
            [],
            new NextAttackCritModifier("card-iron-fang", 10));

        var both = NextAttackCritModifiers.Apply(
            ironFang,
            new NextAttackCritModifier("passive-bach-ho", 10));

        Assert.Equal(2, both.Length);
        Assert.Contains(both, m => m.SourceIdentity == "card-iron-fang" && m.CritContribution == 10);
        Assert.Contains(both, m => m.SourceIdentity == "passive-bach-ho" && m.CritContribution == 10);
    }

    [Fact]
    public void Apply_PreservesTheRefreshedElementsPosition()
    {
        // §2.3.4 item 6 makes ordering non-semantic, and §5.1.2 item 5 makes
        // consumption order-independent; preserving position on refresh keeps both
        // true and keeps the array stable for round-trip fidelity.
        var collection = NextAttackCritModifiers.Apply(
            [],
            new NextAttackCritModifier("source-a", 10));
        collection = NextAttackCritModifiers.Apply(collection, new NextAttackCritModifier("source-b", 20));

        var refreshed = NextAttackCritModifiers.Apply(
            collection,
            new NextAttackCritModifier("source-a", 30));

        Assert.Equal("source-a", refreshed[0].SourceIdentity);
        Assert.Equal(30, refreshed[0].CritContribution);
        Assert.Equal("source-b", refreshed[1].SourceIdentity);
    }

    [Fact]
    public void Apply_BlankSourceIdentity_IsRejected()
    {
        // §2.3.4 item 2: the identity is the removal key. A blank identity would let
        // two different sources collide on it, which destroys the one property the
        // member exists to provide, so it is rejected rather than admitted.
        Assert.Throws<ArgumentException>(() =>
            NextAttackCritModifiers.Apply([], new NextAttackCritModifier("   ", 10)));
    }

    [Fact]
    public void Apply_NullCollection_IsRejected()
    {
        // §2.3.4 item 5: absence of the collection is not a representable state, so
        // null is rejected rather than treated as empty.
        Assert.Throws<ArgumentNullException>(() =>
            NextAttackCritModifiers.Apply(null!, new NextAttackCritModifier("source", 10)));
    }

    [Fact]
    public void Consume_RemovesOnlyTheConsumedIdentities()
    {
        // COMBAT_RULES.md §3.3 item 9 / §5.1.2 item 4: consumption removes ONLY the
        // source-specific modifiers the qualifying attack consumed. A modifier whose
        // source was not consumed survives.
        NextAttackCritModifier[] collection =
        [
            new("card-iron-fang", 10),
            new("passive-bach-ho", 10),
            new("relic-assassin-eye", 15),
        ];

        var remaining = NextAttackCritModifiers.Consume(collection, ["card-iron-fang"]);

        Assert.Equal(2, remaining.Length);
        Assert.DoesNotContain(remaining, m => m.SourceIdentity == "card-iron-fang");
        Assert.Contains(remaining, m => m.SourceIdentity == "passive-bach-ho" && m.CritContribution == 10);
        Assert.Contains(remaining, m => m.SourceIdentity == "relic-assassin-eye" && m.CritContribution == 15);
    }

    [Fact]
    public void Consume_MultipleIdentitiesAtOnce_RemovesAllOfThemTogether()
    {
        // COMBAT_RULES.md §3.3 item 10: a qualifying attack consumes ALL applicable
        // NextAttack modifiers together. Iron Fang's and Bạch Hổ's contributions are
        // both removed — neither is left behind merely because the two share the
        // attack scope.
        NextAttackCritModifier[] collection =
        [
            new("card-iron-fang", 10),
            new("passive-bach-ho", 10),
        ];

        var remaining = NextAttackCritModifiers.Consume(
            collection,
            ["card-iron-fang", "passive-bach-ho"]);

        Assert.Empty(remaining);
    }

    [Fact]
    public void Consume_WithEmptyIdentitySet_RemovesNothing()
    {
        // An attack with no applicable modifier consumes nothing — the documented
        // state for a non-qualifying action (COMBAT_RULES.md §3.3 item 8).
        NextAttackCritModifier[] collection = [new("card-iron-fang", 10)];

        var remaining = NextAttackCritModifiers.Consume(collection, []);

        Assert.Single(remaining);
    }

    [Fact]
    public void Consume_PreservesTheOrderOfWhatItLeavesBehind()
    {
        // §5.1.2 item 5: consumption is order-independent, and the elements it does
        // not consume are carried across in order.
        NextAttackCritModifier[] collection =
        [
            new("source-a", 5),
            new("source-b", 10),
            new("source-c", 15),
        ];

        var remaining = NextAttackCritModifiers.Consume(collection, ["source-b"]);

        Assert.Equal(["source-a", "source-c"], remaining.Select(m => m.SourceIdentity));
    }

    [Fact]
    public void TotalContribution_SumsTheWholeCollection()
    {
        // COMBAT_RULES.md §3.3 item 7: contributions are additive, so the composed
        // temporary contribution is the sum.
        NextAttackCritModifier[] collection =
        [
            new("card-iron-fang", 10),
            new("passive-bach-ho", 10),
        ];

        Assert.Equal(20, NextAttackCritModifiers.TotalContribution(collection));
    }

    [Fact]
    public void TotalContribution_OnEmptyCollection_IsZero()
    {
        // §2.3.4 item 5: no modifier active is an empty collection, whose sum is 0 —
        // the documented "no temporary contribution" value.
        Assert.Equal(0, NextAttackCritModifiers.TotalContribution([]));
    }

    [Fact]
    public void TotalContribution_DoesNotApplyTheCap()
    {
        // COMBAT_RULES.md §3.3 item 7 owns the composition AND the cap, in one place.
        // This helper reports the raw sum so the cap has exactly one owner in code:
        // capping here as well would create a second place the ceiling is decided.
        NextAttackCritModifier[] collection =
        [
            new("source-a", 80),
            new("source-b", 80),
        ];

        Assert.Equal(160, NextAttackCritModifiers.TotalContribution(collection));
    }

    [Fact]
    public void ModifiersEqual_ComparesElementsAndOrder()
    {
        // §2.3.4 item 8 / REDIS_STATE.md §7 item 13: the round trip must preserve the
        // elements, their member values, and their order.
        NextAttackCritModifier[] left = [new("source-a", 10), new("source-b", 20)];
        NextAttackCritModifier[] same = [new("source-a", 10), new("source-b", 20)];
        NextAttackCritModifier[] reordered = [new("source-b", 20), new("source-a", 10)];
        NextAttackCritModifier[] differentValue = [new("source-a", 10), new("source-b", 21)];

        Assert.True(NextAttackCritModifiers.ModifiersEqual(left, same));
        Assert.False(NextAttackCritModifiers.ModifiersEqual(left, reordered));
        Assert.False(NextAttackCritModifiers.ModifiersEqual(left, differentValue));
    }

    [Fact]
    public void PetState_AtBattleCreation_HoldsAnEmptyModifierCollection()
    {
        // §2.3.4 item 5 / §5.1.2 item 1: the collection begins empty, because a
        // modifier is created by a source resolving during a resolution and battle
        // creation resolves nothing. It is never null, so "no modifier" is the empty
        // collection rather than an omission.
        var state = BattleState.CreateWith("battle-crit-lifecycle", 4242);

        Assert.NotNull(state.PetState.NextAttackCritModifiers);
        Assert.Empty(state.PetState.NextAttackCritModifiers);
    }

    [Fact]
    public void PetState_BaseCritIsNeverWrittenByAModifierApplication()
    {
        // §2.3.4 item 9 / COMBAT_RULES.md §3.3 item 7: `Crit` is the permanent/base
        // value and a temporary modifier never overwrites it. Applying a modifier
        // touches only the collection.
        var state = BattleState.CreateWith("battle-crit-base", 4242);
        var baseCrit = state.PetState.Crit;

        var withModifier = state.PetState with
        {
            NextAttackCritModifiers = NextAttackCritModifiers.Apply(
                state.PetState.NextAttackCritModifiers,
                new NextAttackCritModifier("card-iron-fang", 10)),
        };

        Assert.Equal(baseCrit, withModifier.Crit);
        Assert.Single(withModifier.NextAttackCritModifiers);
    }
}
