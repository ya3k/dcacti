using System.Text.Json;
using GameServer.Domain.Battle;
using GameServer.Domain.Battle.Serialization;
using Xunit;

namespace GameServer.Domain.Tests.Battle;

/// <summary>
/// The runtime <c>BattleState</c> JSON round trip for the two TASK-140 carrier
/// collections — <c>PetState.ATKModifiers[]</c> and
/// <c>PetState.CardCostModifiers[]</c>
/// (<c>GAME_STATE.md</c> §2.3.5, §2.3.6, §2.3.7, §2.3.8;
/// <c>REDIS_STATE.md</c> §7 items 15–16).
///
/// <code>
/// Domain BattleState
///       ↓  BattleStateSerializer.Serialize
/// JSON
///       ↓  BattleStateSerializer.Deserialize
/// Domain BattleState
/// </code>
///
/// Every expected value below is derived from the cited section, not from what the
/// implementation currently does. The assertions inspect the serialized DOCUMENT as
/// well as the restored object, so "the deserializer returned something the test
/// still held" cannot be mistaken for a verified round trip.
/// </summary>
public sealed class BattleStateModifierSerializationTests
{
    // =======================================================================
    // §2.3.8 item 1 / §2.3.6 item 1 — empty collections serialize as [], always
    // =======================================================================

    [Fact]
    public void EmptyCollections_SerializeAsEmptyArrays_AndNeverAsNull()
    {
        // §2.3.8 item 1: "A Pet with no active modifier serializes an empty array — []
        // — because the collection always exists (§2.3.7 item 6). It is never omitted
        // and never null." §2.3.6 item 1 states the same for the Card-cost collection.
        // The member must therefore be PRESENT and be an ARRAY, not absent and not null.
        var json = BattleStateSerializer.Serialize(BattleState.CreateWith("battle-empty-modifiers", 4242));

        using var document = JsonDocument.Parse(json);
        var petState = document.RootElement.GetProperty("petState");

        var atkModifiers = petState.GetProperty("atkModifiers");
        var cardCostModifiers = petState.GetProperty("cardCostModifiers");

        Assert.Equal(JsonValueKind.Array, atkModifiers.ValueKind);
        Assert.Equal(JsonValueKind.Array, cardCostModifiers.ValueKind);
        Assert.Equal(0, atkModifiers.GetArrayLength());
        Assert.Equal(0, cardCostModifiers.GetArrayLength());

        // And they are not the JSON null that §2.3.8 item 1 rules out.
        Assert.NotEqual(JsonValueKind.Null, atkModifiers.ValueKind);
        Assert.NotEqual(JsonValueKind.Null, cardCostModifiers.ValueKind);
    }

    [Fact]
    public void EmptyCollections_RoundTripAsNonNullEmptyCollections()
    {
        // §2.3.8 item 1: "Because an empty collection round-trips as an empty
        // collection, the no-modifier state is preserved too." §2.3.5 item 6: there is
        // "no sentinel element, no null, and no omitted member standing in for it".
        var state = BattleState.CreateWith("battle-empty-modifiers-roundtrip", 4242);

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(state));

        Assert.NotNull(restored.PetState.ATKModifiers);
        Assert.NotNull(restored.PetState.CardCostModifiers);
        Assert.Empty(restored.PetState.ATKModifiers);
        Assert.Empty(restored.PetState.CardCostModifiers);
        Assert.True(state.PetState.ATKModifiersEqual(restored.PetState));
        Assert.True(state.PetState.CardCostModifiersEqual(restored.PetState));
    }

    [Fact]
    public void ANullModifierCollection_IsRejectedRatherThanReadAsEmpty()
    {
        // §2.3.7 item 6 / §2.3.5 item 6: absence of either collection "is not a
        // representable state", so a stored null is a contract violation. It is rejected
        // with a message naming the rule rather than being read as empty — which would
        // admit the spelling the contract rules out.
        var json = BattleStateSerializer.Serialize(BattleState.CreateWith("battle-null-modifiers", 4242));

        var nulledAtk = json.Replace("\"atkModifiers\":[]", "\"atkModifiers\":null");
        var nulledCardCost = json.Replace("\"cardCostModifiers\":[]", "\"cardCostModifiers\":null");

        Assert.NotEqual(json, nulledAtk);
        Assert.NotEqual(json, nulledCardCost);

        var atkFailure = Assert.Throws<JsonException>(() => BattleStateSerializer.Deserialize(nulledAtk));
        Assert.Contains("2.3.7 item 6", atkFailure.Message);

        var cardCostFailure = Assert.Throws<JsonException>(() => BattleStateSerializer.Deserialize(nulledCardCost));
        Assert.Contains("2.3.5 item 6", cardCostFailure.Message);
    }

    [Fact]
    public void AnOmittedModifierCollection_IsRejected()
    {
        // §2.3.8 item 1 states the collection "is never omitted", so a document without
        // the member is not a valid record: the required member surfaces as a read
        // failure rather than being defaulted to an empty collection.
        var json = BattleStateSerializer.Serialize(BattleState.CreateWith("battle-omitted-modifiers", 4242));

        var withoutAtk = json.Replace(",\"atkModifiers\":[]", string.Empty);

        Assert.NotEqual(json, withoutAtk);
        Assert.Throws<JsonException>(() => BattleStateSerializer.Deserialize(withoutAtk));
    }

    // =======================================================================
    // §2.3.8 item 3 / §2.3.6 item 3 — the element member set
    // =======================================================================

    [Fact]
    public void AnElement_SerializesExactlyTheThreeDocumentedMembers()
    {
        // §2.3.8 item 3, as TASK-178 extended it (Product Owner decision Q-1 = A):
        // "An element serializes exactly these members, with these types:
        // sourceIdentity string required; atkModifierPercentage number required;
        // lifetime string required (`Battle` | `NextAttack`)." §2.3.6 item 3 fixes
        // the Card-cost element's pair. Every member is always present (§2.3.8
        // item 1, §2.3.5 item 6), so none carries an ignore condition — and no
        // fourth member is written.
        var state = WithModifiers(
            BattleState.CreateWith("battle-element-members", 4242),
            atkModifiers: [new ATKModifier("berserker-core", 5)],
            cardCostModifiers: [new CardCostModifier("emergency-core", 50)]);

        using var document = JsonDocument.Parse(BattleStateSerializer.Serialize(state));
        var petState = document.RootElement.GetProperty("petState");

        var atkElement = Assert.Single(petState.GetProperty("atkModifiers").EnumerateArray().ToArray());
        Assert.Equal(
            ["atkModifierPercentage", "lifetime", "sourceIdentity"],
            atkElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal("berserker-core", atkElement.GetProperty("sourceIdentity").GetString());
        Assert.Equal(5, atkElement.GetProperty("atkModifierPercentage").GetInt32());
        Assert.Equal("Battle", atkElement.GetProperty("lifetime").GetString());

        var cardCostElement = Assert.Single(petState.GetProperty("cardCostModifiers").EnumerateArray().ToArray());
        Assert.Equal(
            ["costReductionPercentage", "sourceIdentity"],
            cardCostElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal("emergency-core", cardCostElement.GetProperty("sourceIdentity").GetString());
        Assert.Equal(50, cardCostElement.GetProperty("costReductionPercentage").GetInt32());
    }

    // =======================================================================
    // §2.3.8 item 5 / §2.3.6 item 5 — lossless round trip
    // =======================================================================

    [Fact]
    public void OneModifierInEachCollection_RoundTripsLosslessly()
    {
        // §2.3.8 item 5 / §2.3.6 item 5: the same elements, the same member values, the
        // same order, and the same count must return.
        var state = WithModifiers(
            BattleState.CreateWith("battle-one-modifier", 4242),
            atkModifiers: [new ATKModifier("berserker-core", 5)],
            cardCostModifiers: [new CardCostModifier("emergency-core", 50)]);

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(state));

        var atkModifier = Assert.Single(restored.PetState.ATKModifiers);
        Assert.Equal("berserker-core", atkModifier.SourceIdentity);
        Assert.Equal(5, atkModifier.ATKModifierPercentage);

        var cardCostModifier = Assert.Single(restored.PetState.CardCostModifiers);
        Assert.Equal("emergency-core", cardCostModifier.SourceIdentity);
        Assert.Equal(50, cardCostModifier.CostReductionPercentage);

        Assert.True(state.PetState.ATKModifiersEqual(restored.PetState));
        Assert.True(state.PetState.CardCostModifiersEqual(restored.PetState));
    }

    [Fact]
    public void MultipleModifiers_RoundTripLosslessly_WithCountAndValuesIntact()
    {
        // §2.3.8 item 5: "A round trip that drops an element, alters a percentage,
        // collapses two distinct source identities into one, or reorders elements is a
        // defect." Both distinct sources must survive as two elements with their own
        // values — including a signed negative ATK contribution (§2.3.7 item 5).
        var state = WithModifiers(
            BattleState.CreateWith("battle-multiple-modifiers", 4242),
            atkModifiers: [new ATKModifier("sourceA", 5), new ATKModifier("sourceB", -30)],
            cardCostModifiers: [new CardCostModifier("sourceA", 5), new CardCostModifier("sourceB", 50)]);

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(state));

        Assert.Equal(2, restored.PetState.ATKModifiers.Length);
        Assert.Contains(restored.PetState.ATKModifiers, m => m.SourceIdentity == "sourceA" && m.ATKModifierPercentage == 5);
        Assert.Contains(restored.PetState.ATKModifiers, m => m.SourceIdentity == "sourceB" && m.ATKModifierPercentage == -30);

        Assert.Equal(2, restored.PetState.CardCostModifiers.Length);
        Assert.Contains(restored.PetState.CardCostModifiers, m => m.SourceIdentity == "sourceA" && m.CostReductionPercentage == 5);
        Assert.Contains(restored.PetState.CardCostModifiers, m => m.SourceIdentity == "sourceB" && m.CostReductionPercentage == 50);

        Assert.True(state.PetState.ATKModifiersEqual(restored.PetState));
        Assert.True(state.PetState.CardCostModifiersEqual(restored.PetState));
    }

    [Fact]
    public void AZeroPercentageModifier_RoundTripsAsAPresentElement()
    {
        // §2.3.7 item 6 / §2.3.5 item 6: a zero must never stand in for "not active" —
        // but a source that genuinely contributes 0 is a present modifier, so it must
        // survive as an element rather than being dropped as if it were absence.
        var state = WithModifiers(
            BattleState.CreateWith("battle-zero-modifier", 4242),
            atkModifiers: [new ATKModifier("neutral-source", 0)],
            cardCostModifiers: [new CardCostModifier("neutral-source", 0)]);

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(state));

        var atkModifier = Assert.Single(restored.PetState.ATKModifiers);
        Assert.Equal("neutral-source", atkModifier.SourceIdentity);
        Assert.Equal(0, atkModifier.ATKModifierPercentage);

        var cardCostModifier = Assert.Single(restored.PetState.CardCostModifiers);
        Assert.Equal("neutral-source", cardCostModifier.SourceIdentity);
        Assert.Equal(0, cardCostModifier.CostReductionPercentage);
    }

    [Fact]
    public void ABlankSourceIdentityInAStoredElement_IsRejected()
    {
        // §2.3.7 item 3 makes the ATK identity the replace/refresh and removal key, and
        // §2.3.5 item 7 requires the Card-cost identity to be a non-empty string and
        // states that a malformed element "is not silently repaired, defaulted, or
        // dropped". A stored element that could not be matched source-specifically is
        // therefore rejected at the read rather than admitted into the state.
        var state = WithModifiers(
            BattleState.CreateWith("battle-blank-identity", 4242),
            atkModifiers: [new ATKModifier("sourceA", 5)],
            cardCostModifiers: [new CardCostModifier("sourceA", 50)]);

        var json = BattleStateSerializer.Serialize(state);

        var atkFailure = Assert.Throws<JsonException>(() =>
            BattleStateSerializer.Deserialize(json.Replace("\"sourceIdentity\":\"sourceA\"", "\"sourceIdentity\":\"  \"")));
        Assert.Contains("2.3.7", atkFailure.Message);
    }

    // =======================================================================
    // §2.3.7 item 7 / §2.3.6 item 6 — deterministic vs. preserved order
    // =======================================================================

    [Fact]
    public void ATKModifiers_SerializeInTheDocumentedSourceIdentityOrder()
    {
        // §2.3.7 item 7 fixes the collection's element order as the SourceIdentity sort,
        // so the serialized order is a property of the element set. §2.3.8 item 5 requires
        // that order back. TASK-136's D9 supplies it through the documented write path.
        var state = BattleState.CreateWith("battle-atk-order", 4242).PetState;

        var applied = ATKModifiers.Apply(state.ATKModifiers, new ATKModifier("source-c", 5));
        applied = ATKModifiers.Apply(applied, new ATKModifier("source-a", 10));
        applied = ATKModifiers.Apply(applied, new ATKModifier("source-b", -30));

        var serialized = BattleStateSerializer.Serialize(
            BattleState.CreateWith("battle-atk-order", 4242) with { PetState = state with { ATKModifiers = applied } });

        using var document = JsonDocument.Parse(serialized);

        Assert.Equal(
            ["source-a", "source-b", "source-c"],
            document.RootElement
                .GetProperty("petState")
                .GetProperty("atkModifiers")
                .EnumerateArray()
                .Select(e => e.GetProperty("sourceIdentity").GetString()));
    }

    [Fact]
    public void ATKModifiers_SerializeIdentically_RegardlessOfInsertionOrder()
    {
        // REDIS_STATE.md §7 item 16: "A store must therefore not be relied on to preserve
        // insertion order for this member: the serialized order is reproducible from the
        // element set alone, so two serializations of the same state are byte-identical."
        var first = ATKModifiers.Apply([], new ATKModifier("alpha", 1));
        first = ATKModifiers.Apply(first, new ATKModifier("beta", 2));
        first = ATKModifiers.Apply(first, new ATKModifier("gamma", 3));

        var second = ATKModifiers.Apply([], new ATKModifier("gamma", 3));
        second = ATKModifiers.Apply(second, new ATKModifier("alpha", 1));
        second = ATKModifiers.Apply(second, new ATKModifier("beta", 2));

        Assert.Equal(
            SerializedWithATKModifiers(first),
            SerializedWithATKModifiers(second));
    }

    [Fact]
    public void CardCostModifiers_SerializeInWrittenOrder_NotInSourceIdentityOrder()
    {
        // §2.3.6 item 6: "Order is preserved for round-trip fidelity, not for semantics."
        // §2.3.5 item 8: "a round trip must still return the elements in the order they
        // were written." The writer therefore imposes no order of its own — a sorting
        // writer would rewrite this array and break the obligation.
        var state = BattleState.CreateWith("battle-cardcost-order", 4242).PetState;

        var applied = CardCostModifiers.Apply(state.CardCostModifiers, new CardCostModifier("source-c", 5));
        applied = CardCostModifiers.Apply(applied, new CardCostModifier("source-a", 50));

        var serialized = BattleStateSerializer.Serialize(
            BattleState.CreateWith("battle-cardcost-order", 4242) with { PetState = state with { CardCostModifiers = applied } });

        using var document = JsonDocument.Parse(serialized);

        Assert.Equal(
            ["source-c", "source-a"],
            document.RootElement
                .GetProperty("petState")
                .GetProperty("cardCostModifiers")
                .EnumerateArray()
                .Select(e => e.GetProperty("sourceIdentity").GetString()));
    }

    [Fact]
    public void CardCostModifiers_RoundTripPreservingWrittenOrder()
    {
        // §2.3.6 item 5: the same element order must return. With a written order that
        // differs from the identity order, only a genuinely order-preserving mapping can
        // round-trip it.
        CardCostModifier[] written = [new("source-c", 5), new("source-a", 50), new("source-b", 25)];

        var state = WithModifiers(
            BattleState.CreateWith("battle-cardcost-order-roundtrip", 4242),
            atkModifiers: [],
            cardCostModifiers: written);

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(state));

        Assert.Equal(
            ["source-c", "source-a", "source-b"],
            restored.PetState.CardCostModifiers.Select(m => m.SourceIdentity));
        Assert.Equal([5, 50, 25], restored.PetState.CardCostModifiers.Select(m => m.CostReductionPercentage));
    }

    // =======================================================================
    // §5.1.4 item 1 / §5.1.3 item 1 — the refreshed result round-trips
    // =======================================================================

    [Fact]
    public void ARefreshedSource_SerializesAsTheSingleRefreshedElement()
    {
        // §5.1.4 item 1 / §5.1.3 item 1: a re-application refreshes that source's own
        // element and never appends a second one — "Two elements with the same
        // SourceIdentity are never observable in a committed state." The serialized
        // document must therefore carry exactly one entry for that source, holding the
        // newly applied value. This is TASK-140's same-source replacement case.
        var state = BattleState.CreateWith("battle-refresh-roundtrip", 4242).PetState;

        var applied = ATKModifiers.Apply(state.ATKModifiers, new ATKModifier("sourceA", 5));
        applied = ATKModifiers.Apply(applied, new ATKModifier("sourceA", 10));

        var cardCostApplied = CardCostModifiers.Apply(state.CardCostModifiers, new CardCostModifier("sourceA", 5));
        cardCostApplied = CardCostModifiers.Apply(cardCostApplied, new CardCostModifier("sourceA", 50));

        var restored = BattleStateSerializer.Deserialize(
            BattleStateSerializer.Serialize(
                BattleState.CreateWith("battle-refresh-roundtrip", 4242) with
                {
                    PetState = state with
                    {
                        ATKModifiers = applied,
                        CardCostModifiers = cardCostApplied,
                    },
                }));

        var atkModifier = Assert.Single(restored.PetState.ATKModifiers);
        Assert.Equal("sourceA", atkModifier.SourceIdentity);
        Assert.Equal(10, atkModifier.ATKModifierPercentage);

        var cardCostModifier = Assert.Single(restored.PetState.CardCostModifiers);
        Assert.Equal("sourceA", cardCostModifier.SourceIdentity);
        Assert.Equal(50, cardCostModifier.CostReductionPercentage);
    }

    [Fact]
    public void ASourceSpecificRemoval_SerializesWithoutTheRemovedSource()
    {
        // §5.1.4 item 4 / §5.1.3 item 4: removal deletes only the identified source's
        // element and leaves every other source untouched. The serialized document must
        // carry exactly the surviving entries — TASK-140's source-specific removal case.
        var state = BattleState.CreateWith("battle-removal-roundtrip", 4242).PetState;

        ATKModifier[] atkCollection = [new("sourceA", 5), new("sourceB", 10)];
        CardCostModifier[] cardCostCollection = [new("sourceA", 5), new("sourceB", 50)];

        var atkRemaining = ATKModifiers.Remove(atkCollection, "sourceA");
        var cardCostRemaining = CardCostModifiers.Remove(cardCostCollection, "sourceA");

        var restored = BattleStateSerializer.Deserialize(
            BattleStateSerializer.Serialize(
                BattleState.CreateWith("battle-removal-roundtrip", 4242) with
                {
                    PetState = state with
                    {
                        ATKModifiers = atkRemaining,
                        CardCostModifiers = cardCostRemaining,
                    },
                }));

        var atkModifier = Assert.Single(restored.PetState.ATKModifiers);
        Assert.Equal("sourceB", atkModifier.SourceIdentity);
        Assert.Equal(10, atkModifier.ATKModifierPercentage);

        var cardCostModifier = Assert.Single(restored.PetState.CardCostModifiers);
        Assert.Equal("sourceB", cardCostModifier.SourceIdentity);
        Assert.Equal(50, cardCostModifier.CostReductionPercentage);
    }

    // =======================================================================
    // §2.3.7 item 9 / §2.3.5 item 4 — no derived value is stored
    // =======================================================================

    [Fact]
    public void TheRoundTrip_IntroducesNoEffectivATKOrCardCostMember()
    {
        // §2.3.7 item 9 / COMBAT_RULES.md §5.6.6 item 8: the composed EffectivePetATK is
        // derived at attack resolution and "is not stored in BattleState, is not a member
        // of this collection, and is not a second representation of the ATK stat".
        // CARD_RULES.md §3.6 item 2 states the same for EffectiveCardCost. Neither term
        // may therefore appear as a serialized member anywhere in the record.
        var state = WithModifiers(
            BattleState.CreateWith("battle-no-derived-members", 4242),
            atkModifiers: [new ATKModifier("berserker-core", 5)],
            cardCostModifiers: [new CardCostModifier("emergency-core", 50)]);

        var json = BattleStateSerializer.Serialize(state);

        Assert.DoesNotContain("effectivePetATK", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("effectiveATK", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("effectiveCardCost", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("defaultATK", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("totalReduction", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stackCount", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("remainingTurns", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expiresAt", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheBaseATKMember_IsSerializedUnchangedAlongsideAModifier()
    {
        // §2.3.7 item 9 / §5.1.4 item 6: "PetState.ATK remains the permanent/base ATK
        // and is never mutated by an entry here", and the base value must round-trip
        // exactly as it was while a modifier is present — the carrier does not fold the
        // modifier into the stat.
        var state = WithModifiers(
            BattleState.CreateWith("battle-base-atk-preserved", 4242),
            atkModifiers: [new ATKModifier("berserker-core", 5)],
            cardCostModifiers: []);

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(state));

        Assert.Equal(PetState.DefaultATK, state.PetState.ATK);
        Assert.Equal(PetState.DefaultATK, restored.PetState.ATK);
        Assert.NotEqual(
            state.PetState.ATK * 105 / 100,
            restored.PetState.ATK);
    }

    // =======================================================================
    // Fixtures
    // =======================================================================

    /// <summary>
    /// The state under test with both carrier collections set. The collections are
    /// <c>init</c>-only members outside the constructor, so they are set exactly the way
    /// the documented write path sets them — as a new value of the state.
    /// </summary>
    private static BattleState WithModifiers(
        BattleState state,
        ATKModifier[] atkModifiers,
        CardCostModifier[] cardCostModifiers) =>
        state with
        {
            PetState = state.PetState with
            {
                ATKModifiers = atkModifiers,
                CardCostModifiers = cardCostModifiers,
            },
        };

    /// <summary>
    /// One battle's document with the given ATK modifier collection, so two application
    /// orders producing the same element set can be compared byte for byte
    /// (<c>REDIS_STATE.md</c> §7 item 16).
    /// </summary>
    private static string SerializedWithATKModifiers(ATKModifier[] modifiers) =>
        BattleStateSerializer.Serialize(
            WithModifiers(
                BattleState.CreateWith("battle-byte-identical", 4242),
                atkModifiers: modifiers,
                cardCostModifiers: []));
}
