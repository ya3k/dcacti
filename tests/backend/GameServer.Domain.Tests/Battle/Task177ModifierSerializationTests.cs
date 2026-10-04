using System.Text.Json;
using GameServer.Domain.Battle;
using GameServer.Domain.Battle.Serialization;
using GameServer.Domain.Relics;
using Xunit;

namespace GameServer.Domain.Tests.Battle;

/// <summary>
/// TASK-177's two serialization obligations, at the runtime mapping's own level:
/// the <c>ATKModifiers[]</c> element's required <c>lifetime</c> member
/// (<c>GAME_STATE.md</c> §2.3.7 item 11, §2.3.8 item 3 — TASK-178 Product Owner
/// decision <b>Q-1 = A</b>) and the <c>BurnDamageModifiers[]</c> collection
/// (the applied Relic <c>BurnDamage</c> effect, <c>RELIC_RULES.md</c> §8.2 item 1,
/// §8.5 item 5).
///
/// <code>
/// Rule (GAME_STATE.md §2.3.7, §2.3.8; REDIS_STATE.md §7 item 16)
///  ↓
/// Scenario (Given a state carrying the collection, When it is serialized and
///           deserialized, Then the documented members survive unchanged — and a
///           stored document missing one is rejected rather than defaulted)
///  ↓
/// Test
/// </code>
///
/// <b>Nothing here re-derives a value.</b> The mapping stores what the state holds
/// and reads it back unchanged; a round trip that alters or drops a member is a
/// defect (§2.3.8 item 5).
/// </summary>
public class Task177ModifierSerializationTests
{
    // =======================================================================
    // ATKModifiers[] — the element's required `lifetime`
    // =======================================================================

    [Fact]
    public void AtkElements_ShouldRoundTripBothLifetimesLosslessly()
    {
        // §2.3.8 item 3: `lifetime` is required on every element, and §2.3.7 item 11
        // makes the element's own member what distinguishes the two lifetimes that
        // share this one collection. Both must survive a round trip, including
        // order (§2.3.7 item 7: the SourceIdentity sort).
        var state = WithModifiers(
            BattleState.CreateWith("battle-atk-lifetimes", 4242),
            [
                new ATKModifier("berserker-core", 5, RelicEffectLifetime.Battle),
                new ATKModifier("battle-instinct", 10, RelicEffectLifetime.NextAttack),
            ]);

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(state));

        Assert.Equal(2, restored.PetState.ATKModifiers.Length);
        Assert.Equal(
            RelicEffectLifetime.Battle,
            Assert.Single(restored.PetState.ATKModifiers, m => m.SourceIdentity == "berserker-core").Lifetime);
        Assert.Equal(
            RelicEffectLifetime.NextAttack,
            Assert.Single(restored.PetState.ATKModifiers, m => m.SourceIdentity == "battle-instinct").Lifetime);
        Assert.True(state.PetState.ATKModifiersEqual(restored.PetState));

        // The serialized member is the lifetime's own name, never an ordinal
        // (DATABASE.md §1 item 2's convention).
        Assert.Contains("\"lifetime\":\"NextAttack\"", BattleStateSerializer.Serialize(state), StringComparison.Ordinal);
    }

    [Fact]
    public void AnAtkElement_WithoutALifetimeMember_IsRejectedRatherThanDefaulted()
    {
        // §2.3.8 item 3: "there is no omitted-member and no defaulted-lifetime form,
        // so a reader never infers a lifetime from absence" — and §2.3.8 item 5
        // names a dropped lifetime a defect. A stored element without it is
        // therefore reported, not read as Battle.
        var json = BattleStateSerializer.Serialize(WithModifiers(
            BattleState.CreateWith("battle-atk-no-lifetime", 4242),
            [new ATKModifier("berserker-core", 5, RelicEffectLifetime.Battle)]));

        var withoutLifetime = json.Replace(",\"lifetime\":\"Battle\"", string.Empty, StringComparison.Ordinal);

        Assert.NotEqual(json, withoutLifetime);

        var failure = Assert.Throws<JsonException>(
            () => BattleStateSerializer.Deserialize(withoutLifetime));

        Assert.Contains("lifetime", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAtkElement_DeclaringImmediate_IsRejected()
    {
        // §2.3.7 item 8: Immediate "leaves no standing modification behind and which
        // therefore never produces an element here", so a stored element declaring
        // it is not a state this collection can hold.
        var json = BattleStateSerializer.Serialize(WithModifiers(
            BattleState.CreateWith("battle-atk-immediate", 4242),
            [new ATKModifier("berserker-core", 5, RelicEffectLifetime.Battle)]));

        var immediate = json.Replace(
            "\"lifetime\":\"Battle\"",
            "\"lifetime\":\"Immediate\"",
            StringComparison.Ordinal);

        Assert.NotEqual(json, immediate);
        Assert.Throws<JsonException>(() => BattleStateSerializer.Deserialize(immediate));
    }

    [Fact]
    public void AnAtkElement_DeclaringAnUnknownLifetime_IsRejected()
    {
        // A token outside the lifetime vocabulary is not a lifetime any document
        // authors, and it is rejected rather than silently treated as Battle.
        var json = BattleStateSerializer.Serialize(WithModifiers(
            BattleState.CreateWith("battle-atk-unknown-lifetime", 4242),
            [new ATKModifier("berserker-core", 5, RelicEffectLifetime.Battle)]));

        var unknown = json.Replace(
            "\"lifetime\":\"Battle\"",
            "\"lifetime\":\"Permanent\"",
            StringComparison.Ordinal);

        Assert.Throws<JsonException>(() => BattleStateSerializer.Deserialize(unknown));
    }

    // =======================================================================
    // BurnDamageModifiers[] — the applied BurnDamage effect
    // =======================================================================

    [Fact]
    public void EmptyBurnDamageModifiers_ShouldSerializeAsAnEmptyArray_NeverNull()
    {
        // "No Burn-damage modifier active" is an EMPTY collection, following the
        // sibling modifier collections' always-present convention: the member is
        // present, is an array, and is never the JSON null those rules out.
        var json = BattleStateSerializer.Serialize(BattleState.CreateWith("battle-no-burn-modifiers", 4242));

        using var document = JsonDocument.Parse(json);
        var petState = document.RootElement.GetProperty("petState");
        var burnModifiers = petState.GetProperty("burnDamageModifiers");

        Assert.Equal(JsonValueKind.Array, burnModifiers.ValueKind);
        Assert.Equal(0, burnModifiers.GetArrayLength());
    }

    [Fact]
    public void BurnDamageModifiers_ShouldRoundTripLosslessly()
    {
        var state = WithBurnDamageModifiers(
            BattleState.CreateWith("battle-burn-modifiers", 4242),
            [
                new BurnDamageModifier("relic-instance-1", 30),
                new BurnDamageModifier("relic-instance-2", 20),
            ]);

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(state));

        Assert.Equal(2, restored.PetState.BurnDamageModifiers.Length);
        Assert.Equal(30, restored.PetState.BurnDamageModifiers[0].BurnDamagePercentage);
        Assert.Equal(20, restored.PetState.BurnDamageModifiers[1].BurnDamagePercentage);
        Assert.True(state.PetState.BurnDamageModifiersEqual(restored.PetState));

        // The element carries exactly the documented pair: a source-scoped key and
        // one percentage, with the lifetime fixed as Battle by RELIC_RULES.md §8.3's
        // BurnDamage row and therefore not carried.
        var json = BattleStateSerializer.Serialize(state);

        Assert.Contains("\"burnDamageModifiers\":[", json, StringComparison.Ordinal);
        Assert.Contains("\"burnDamagePercentage\":30", json, StringComparison.Ordinal);

        // An empty collection round-trips as an empty collection too, so the
        // no-modifier state is preserved.
        var empty = BattleStateSerializer.Deserialize(
            BattleStateSerializer.Serialize(BattleState.CreateWith("battle-empty-burn-round-trip", 4242)));

        Assert.Empty(empty.PetState.BurnDamageModifiers);
    }

    [Fact]
    public void ANullBurnDamageModifierCollection_IsRejectedRatherThanReadAsEmpty()
    {
        // The collection's absence is not a representable state, so a stored null is
        // a contract violation and is refused rather than read as empty.
        var json = BattleStateSerializer.Serialize(BattleState.CreateWith("battle-null-burn-modifiers", 4242));

        var nulled = json.Replace(
            "\"burnDamageModifiers\":[]",
            "\"burnDamageModifiers\":null",
            StringComparison.Ordinal);

        Assert.NotEqual(json, nulled);
        Assert.Throws<JsonException>(() => BattleStateSerializer.Deserialize(nulled));
    }

    [Fact]
    public void ABurnDamageModifierWithABlankIdentity_IsRejected()
    {
        var state = WithBurnDamageModifiers(
            BattleState.CreateWith("battle-blank-burn-modifier", 4242),
            [new BurnDamageModifier("relic-instance-1", 30)]);

        var json = BattleStateSerializer.Serialize(state);

        var blanked = json.Replace(
            "\"sourceIdentity\":\"relic-instance-1\"",
            "\"sourceIdentity\":\"  \"",
            StringComparison.Ordinal);

        Assert.NotEqual(json, blanked);
        Assert.Throws<JsonException>(() => BattleStateSerializer.Deserialize(blanked));
    }

    // =======================================================================
    // Fixtures
    // =======================================================================

    /// <summary>
    /// The state under test with its ATK modifier collection set — the
    /// <c>init</c>-only member is set exactly the way the documented write path
    /// sets it, as a new value of the state.
    /// </summary>
    private static BattleState WithModifiers(BattleState state, ATKModifier[] modifiers) =>
        state with
        {
            PetState = state.PetState with { ATKModifiers = modifiers },
        };

    /// <inheritdoc cref="WithModifiers(BattleState, ATKModifier[])"/>
    private static BattleState WithBurnDamageModifiers(
        BattleState state,
        BurnDamageModifier[] modifiers) =>
        state with
        {
            PetState = state.PetState with { BurnDamageModifiers = modifiers },
        };
}
