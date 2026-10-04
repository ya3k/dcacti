using System.Text.Json;
using System.Text.Json.Serialization;
using GameServer.Api.Hubs;
using Xunit;

namespace GameServer.Api.Tests.Hubs;

/// <summary>
/// Unit tests for <see cref="PetStatePayload"/> wire projection
/// (<c>SIGNALR_PROTOCOL.md</c> §4.3 items 2, 13 and 14).
/// </summary>
public sealed class PetStateWireProjectionTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact]
    public void PetStatePayload_SerializesDocumentedWireMembers_WithDefaultResetOmitted()
    {
        var payload = new PetStatePayload(
            PassiveId: "xich-lang",
            PassiveProgress: new PassiveProgressPayload(Threshold: 5, Current: 2),
            EquippedCards: ["card-heal", "card-shield", "card-power-charge", "card-inferno"],
            StatusEffects: [],
            PassiveResetOverride: null);

        var element = JsonSerializer.SerializeToElement(payload, SerializerOptions);

        var properties = element.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(["equippedCards", "passiveId", "passiveProgress", "statusEffects"], properties);

        Assert.Equal("xich-lang", element.GetProperty("passiveId").GetString());

        var progress = element.GetProperty("passiveProgress");
        Assert.Equal(5, progress.GetProperty("threshold").GetInt32());
        Assert.Equal(2, progress.GetProperty("current").GetInt32());

        var cards = element.GetProperty("equippedCards").EnumerateArray().Select(c => c.GetString()!).ToArray();
        Assert.Equal(4, cards.Length);
        Assert.Equal(["card-heal", "card-shield", "card-power-charge", "card-inferno"], cards);

        Assert.False(element.TryGetProperty("passiveResetOverride", out _));
    }

    [Fact]
    public void PetStatePayload_SerializesDocumentedWireMembers_WithNonDefaultResetIncluded()
    {
        var payload = new PetStatePayload(
            PassiveId: "xich-lang",
            PassiveProgress: new PassiveProgressPayload(Threshold: 5, Current: 0),
            EquippedCards: ["card-inferno", "card-heal", "card-shield", "card-power-charge"],
            StatusEffects: [],
            PassiveResetOverride: "NoReset");

        var element = JsonSerializer.SerializeToElement(payload, SerializerOptions);

        var properties = element.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(["current", "equippedCards", "passiveId", "passiveProgress", "passiveResetOverride", "statusEffects", "threshold"],
            EnumeratePaths(element).OrderBy(n => n, StringComparer.Ordinal).ToArray());

        Assert.Equal("NoReset", element.GetProperty("passiveResetOverride").GetString());

        // Order preserved exactly as passed
        var cards = element.GetProperty("equippedCards").EnumerateArray().Select(c => c.GetString()!).ToArray();
        Assert.Equal(["card-inferno", "card-heal", "card-shield", "card-power-charge"], cards);
    }

    /// <summary>
    /// §4.3 item 14 / <c>GAME_STATE.md</c> §2.3.2 item 1: an active Pet with no
    /// active effect is sent an <b>empty array</b>. The member is always present —
    /// never omitted and never <c>null</c> — so this is the opposite of §3.2.5's
    /// omitted-when-not-applicable convention and a client must not read its
    /// absence as "no effect is active".
    /// </summary>
    [Fact]
    public void PetStatePayload_SerializesAnEmptyStatusEffectsCollection_AsAnEmptyArray()
    {
        var payload = new PetStatePayload(
            PassiveId: "xich-lang",
            PassiveProgress: new PassiveProgressPayload(Threshold: 5, Current: 0),
            EquippedCards: ["card-heal", "card-shield", "card-power-charge", "card-inferno"],
            StatusEffects: []);

        var element = JsonSerializer.SerializeToElement(payload, SerializerOptions);

        Assert.True(element.TryGetProperty("statusEffects", out var statusEffects));
        Assert.Equal(JsonValueKind.Array, statusEffects.ValueKind);
        Assert.Empty(statusEffects.EnumerateArray());
    }

    /// <summary>
    /// §4.3 item 14 / <c>GAME_STATE.md</c> §2.3.2 item 3: an element carries the
    /// documented member set — the four required members plus whichever of the
    /// three optional members apply. <c>GAME_STATE.md</c> §2.3.1 item 3 makes the
    /// Turn countdown and the trigger-based expiry mutually exclusive, so exactly
    /// one of <c>remainingTurns</c>/<c>expiryCondition</c> is present; the other
    /// members that do not apply are <b>omitted</b>, never written as <c>null</c>
    /// (§3.2.5).
    /// </summary>
    [Fact]
    public void StatusEffectPayload_SerializesTheDocumentedElementShape_OmittingNonApplicableMembers()
    {
        var payload = new PetStatePayload(
            PassiveId: "xich-lang",
            PassiveProgress: new PassiveProgressPayload(Threshold: 5, Current: 0),
            EquippedCards: ["card-heal", "card-shield", "card-power-charge", "card-inferno"],
            StatusEffects:
            [
                // A Turn-based instance with no TargetStat: `targetStat` is absent
                // because this is not a stat-modifying BuffDebuff, and
                // `expiryCondition` is absent because the Turn countdown is used.
                new StatusEffectPayload(
                    Id: "Burn",
                    Type: "DoT",
                    Source: "boss",
                    Magnitude: 25,
                    RemainingTurns: 3),
                // A trigger-based instance: `remainingTurns` is absent because the
                // instance does not use the Turn countdown.
                new StatusEffectPayload(
                    Id: "Shield",
                    Type: "Shield",
                    Source: "player",
                    Magnitude: 100,
                    ExpiryCondition: "ShieldDepleted"),
            ]);

        var element = JsonSerializer.SerializeToElement(payload, SerializerOptions);
        var effects = element.GetProperty("statusEffects").EnumerateArray().ToArray();

        Assert.Equal(2, effects.Length);

        // Element order is not semantic (§2.3.1 item 10) but the projection
        // preserves the order the state holds (§2.3.2 item 6).
        Assert.Equal(
            ["id", "magnitude", "remainingTurns", "source", "type"],
            effects[0].EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal("Burn", effects[0].GetProperty("id").GetString());
        Assert.Equal("DoT", effects[0].GetProperty("type").GetString());
        Assert.Equal("boss", effects[0].GetProperty("source").GetString());
        Assert.Equal(25, effects[0].GetProperty("magnitude").GetDouble());
        Assert.Equal(3, effects[0].GetProperty("remainingTurns").GetInt32());
        Assert.False(effects[0].TryGetProperty("targetStat", out _));
        Assert.False(effects[0].TryGetProperty("expiryCondition", out _));

        Assert.Equal(
            ["expiryCondition", "id", "magnitude", "source", "type"],
            effects[1].EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal("ShieldDepleted", effects[1].GetProperty("expiryCondition").GetString());
        Assert.False(effects[1].TryGetProperty("remainingTurns", out _));

        // Neither element writes a null for a member that does not apply.
        foreach (var effect in effects)
        {
            foreach (var member in effect.EnumerateObject())
            {
                Assert.NotEqual(JsonValueKind.Null, member.Value.ValueKind);
            }
        }
    }

    private static IEnumerable<string> EnumeratePaths(JsonElement element)
    {
        foreach (var member in element.EnumerateObject())
        {
            yield return member.Name;
            if (member.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var nested in member.Value.EnumerateObject())
                {
                    yield return nested.Name;
                }
            }
        }
    }
}
