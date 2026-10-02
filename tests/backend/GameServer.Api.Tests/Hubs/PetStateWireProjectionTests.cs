using System.Text.Json;
using System.Text.Json.Serialization;
using GameServer.Api.Hubs;
using Xunit;

namespace GameServer.Api.Tests.Hubs;

/// <summary>
/// Unit tests for <see cref="PetStatePayload"/> wire projection
/// (<c>SIGNALR_PROTOCOL.md</c> §4.3 item 2 &amp; item 13).
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
            PassiveResetOverride: null);

        var element = JsonSerializer.SerializeToElement(payload, SerializerOptions);

        var properties = element.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(["equippedCards", "passiveId", "passiveProgress"], properties);

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
            PassiveResetOverride: "NoReset");

        var element = JsonSerializer.SerializeToElement(payload, SerializerOptions);

        var properties = element.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(["current", "equippedCards", "passiveId", "passiveProgress", "passiveResetOverride", "threshold"],
            EnumeratePaths(element).OrderBy(n => n, StringComparer.Ordinal).ToArray());

        Assert.Equal("NoReset", element.GetProperty("passiveResetOverride").GetString());

        // Order preserved exactly as passed
        var cards = element.GetProperty("equippedCards").EnumerateArray().Select(c => c.GetString()!).ToArray();
        Assert.Equal(["card-inferno", "card-heal", "card-shield", "card-power-charge"], cards);
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
