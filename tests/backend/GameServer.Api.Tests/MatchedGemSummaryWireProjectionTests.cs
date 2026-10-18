using System.Text.Json;
using System.Text.Json.Serialization;
using GameServer.Api.Hubs;
using GameServer.Domain.Match3;
using Xunit;

namespace GameServer.Api.Tests;

/// <summary>
/// The <c>MatchedGemSummary</c> wire projection — <c>SIGNALR_PROTOCOL.md</c>
/// §3.2.29.
///
/// <code>
/// Rule (SIGNALR_PROTOCOL.md §3.2.29)
///  ↓
/// Scenario (Given a committed cycle's matched-Gem counts, When they are
///           projected, Then the documented members and no others)
///  ↓
/// Test
/// </code>
///
/// <b>These are projection tests, not transport tests.</b> They assert what
/// <see cref="BattleEventWireProjection"/> produces for the Domain event — the
/// members, their presence rules, and the values — independently of a live
/// connection. The end-to-end path is covered by the integration suite.
/// </summary>
public class MatchedGemSummaryWireProjectionTests
{
    private static readonly JsonSerializerOptions Options =
        new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private static JsonElement Serialize(BattleEvent battleEvent) =>
        JsonSerializer.SerializeToElement(
            BattleEventWireProjection.Project([battleEvent])[0],
            Options);

    private static string[] Members(JsonElement element) =>
        element.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    [Fact]
    public void MatchedGemSummary_ShouldCarryTheDiscriminatorAndTheGemsArrayOnly()
    {
        // §3.2.29: the discriminator plus the per-type counts. No actor (the sibling
        // SwapStarted owns it), no board, no clearedCells, and no combat value.
        var wire = Serialize(BattleEvent.ForMatchedGemSummary(new ClearedGemCounts(4, 2, 0, 1)));

        Assert.Equal("MatchedGemSummary", wire.GetProperty("type").GetString());
        Assert.Equal(["gems", "type"], Members(wire));

        var gems = wire.GetProperty("gems").EnumerateArray().ToArray();
        Assert.Equal(3, gems.Length);

        Assert.Equal("ATK", gems[0].GetProperty("gemType").GetString());
        Assert.Equal(4, gems[0].GetProperty("count").GetInt32());

        Assert.Equal("DEF", gems[1].GetProperty("gemType").GetString());
        Assert.Equal(2, gems[1].GetProperty("count").GetInt32());

        Assert.Equal("POWER", gems[2].GetProperty("gemType").GetString());
        Assert.Equal(1, gems[2].GetProperty("count").GetInt32());
    }

    [Fact]
    public void MatchedGemSummary_ShouldOmitAZeroCountTypeAndKeepTheFixedOrder()
    {
        // §3.2.29 items 3–4: a type the cycle did not match is ABSENT, never present
        // as 0, and the surviving entries keep the fixed ATK, DEF, HP, POWER order.
        var wire = Serialize(BattleEvent.ForMatchedGemSummary(new ClearedGemCounts(0, 0, 5, 0)));

        var gems = wire.GetProperty("gems").EnumerateArray().ToArray();

        var entry = Assert.Single(gems);
        Assert.Equal("HP", entry.GetProperty("gemType").GetString());
        Assert.Equal(5, entry.GetProperty("count").GetInt32());

        // The absent types are not written as zero members or zero entries.
        Assert.DoesNotContain(gems, g => g.GetProperty("count").GetInt32() == 0);
    }

    [Fact]
    public void MatchedGemSummary_ShouldWriteEachEntryWithExactlyTwoMembers()
    {
        // §3.2.29 item 5 / §3.2.12 item 1: one entry is the type and the count, and
        // nothing else — no cell, no identity, no timestamp.
        var wire = Serialize(BattleEvent.ForMatchedGemSummary(new ClearedGemCounts(3, 3, 3, 3)));
        var gems = wire.GetProperty("gems").EnumerateArray().ToArray();

        Assert.Equal(4, gems.Length);

        foreach (var entry in gems)
        {
            Assert.Equal(["count", "gemType"], Members(entry));
        }
    }

    [Fact]
    public void MatchedGemSummary_ShouldOmitTheArrayWhenTheCycleMatchedNoGemType()
    {
        // §3.2.29 item 2 / §3.2.5 item 1: an empty array is omitted, never written as
        // `[]` and never as null.
        var wire = Serialize(BattleEvent.ForMatchedGemSummary(ClearedGemCounts.None));

        Assert.Equal(["type"], Members(wire));
        Assert.False(wire.TryGetProperty("gems", out _));
    }

    [Fact]
    public void MatchedGemSummary_ShouldProjectEveryEntryInThePayloadsOwnOrder()
    {
        // §3.2.1 item 3: the projection sorts nothing, so the array it writes is the
        // payload's own order and length. The only producer is the Domain factory,
        // which reads the four counts in the fixed MATCH3_RULES.md §1.1 order, so
        // every projected array runs ATK → DEF → HP → POWER.
        var wire = Serialize(BattleEvent.ForMatchedGemSummary(new ClearedGemCounts(9, 8, 7, 6)));
        var gems = wire.GetProperty("gems").EnumerateArray().ToArray();

        Assert.Equal(
            ["ATK", "DEF", "HP", "POWER"],
            gems.Select(g => g.GetProperty("gemType").GetString()).ToArray());
        Assert.Equal(
            [9, 8, 7, 6],
            gems.Select(g => g.GetProperty("count").GetInt32()).ToArray());
    }
}
