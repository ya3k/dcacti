using System.Text.Json;
using System.Text.Json.Serialization;
using GameServer.Api.Hubs;
using GameServer.Domain.Battle;
using GameServer.Domain.Match3;
using GameServer.Domain.Relics;
using Xunit;

namespace GameServer.Api.Tests;

/// <summary>
/// The Relic and Power wire projections — <c>SIGNALR_PROTOCOL.md</c> §3.2.23
/// (<c>RelicTriggered</c>) and §3.2.24 (<c>PowerChanged</c>).
///
/// <code>
/// Rule (SIGNALR_PROTOCOL.md §3.2.23–§3.2.24, §3.2.5, §3.2.25)
///  ↓
/// Scenario (Given a Domain event, When it is projected,
///           Then the documented members and no others are written)
///  ↓
/// Test
/// </code>
///
/// <b>These are projection tests, not transport tests.</b> They assert what
/// <see cref="BattleEventWireProjection"/> produces for a given Domain event —
/// the members and their presence rules — independently of a live connection.
/// </summary>
public class RelicWireProjectionTests
{
    private static readonly JsonSerializerOptions Options =
        new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private static JsonElement Serialize(BattleEvent battleEvent) =>
        JsonSerializer.SerializeToElement(
            BattleEventWireProjection.Project([battleEvent])[0],
            Options);

    private static string[] Members(JsonElement element) =>
        element.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    // =======================================================================
    // RelicTriggered — §3.2.23
    // =======================================================================

    [Fact]
    public void RelicTriggered_ShouldCarryTypeAndRelicIdOnly()
    {
        // §3.2.23: the member set is `type` and `relicId` and nothing else — the
        // value is the triggered Relic's OWNED INSTANCE identity, the same value
        // PetState.EquippedRelics[] holds (RELIC_RULES.md §2.2 item 3).
        var wire = Serialize(BattleEvent.ForRelicTriggered(new RelicTriggeredEvent("relic_instance_7")));

        Assert.Equal("RelicTriggered", wire.GetProperty("type").GetString());
        Assert.Equal("relic_instance_7", wire.GetProperty("relicId").GetString());
        Assert.Equal(["relicId", "type"], Members(wire));
    }

    [Fact]
    public void RelicTriggered_ShouldOmitTheEffectSummaryAndAnyOrderIndex()
    {
        // §3.2.23 item 2 omits `effect summary` under §3.2.25, and item 3 declines the
        // deterministic order index: RELIC_RULES.md §4's order IS the array position.
        // Neither member exists, so no value can be written for either.
        var wire = Serialize(BattleEvent.ForRelicTriggered(new RelicTriggeredEvent("relic_instance_1")));

        Assert.False(wire.TryGetProperty("effectSummary", out _));
        Assert.False(wire.TryGetProperty("orderIndex", out _));
        Assert.False(wire.TryGetProperty("index", out _));
        Assert.False(wire.TryGetProperty("condition", out _));
        Assert.False(wire.TryGetProperty("effect", out _));
        Assert.False(wire.TryGetProperty("magnitude", out _));
        Assert.False(wire.TryGetProperty("target", out _));
        Assert.False(wire.TryGetProperty("source", out _));
    }

    // =======================================================================
    // PowerChanged — §3.2.24
    // =======================================================================

    [Fact]
    public void PowerChanged_ShouldCarryDeltaPowerAndTheProjectedSourceName()
    {
        // §3.2.24: `delta` (signed), `power` (the resulting value), and `source` —
        // here projected to the documented lowercase contract name "relic", never the
        // Domain ordinal (§3.2.4, §3.2.24 item 3).
        var wire = Serialize(BattleEvent.ForPowerChanged(
            new PowerChangedEvent(PowerChangeSource.Relic, Delta: 10, Power: 45)));

        Assert.Equal("PowerChanged", wire.GetProperty("type").GetString());
        Assert.Equal(10, wire.GetProperty("delta").GetInt32());
        Assert.Equal(45, wire.GetProperty("power").GetInt32());
        Assert.Equal("relic", wire.GetProperty("source").GetString());
        Assert.Equal(["delta", "power", "source", "type"], Members(wire));
    }

    [Theory]
    [InlineData(PowerChangeSource.Match, "match")]
    [InlineData(PowerChangeSource.Card, "card")]
    [InlineData(PowerChangeSource.Relic, "relic")]
    [InlineData(PowerChangeSource.Boss, "boss")]
    public void PowerChanged_ShouldProjectEachDocumentedSourceValue(
        PowerChangeSource source,
        string expected)
    {
        // §3.2.24 item 3 owns the value set — "match", "card", "relic", or "boss" —
        // and the Domain member names are Domain identities that never reach the
        // wire. The four values are the whole set: no "card-power", no fifth value.
        var wire = Serialize(BattleEvent.ForPowerChanged(new PowerChangedEvent(source, 5, 5)));

        Assert.Equal(expected, wire.GetProperty("source").GetString());
        Assert.Equal(["delta", "power", "source", "type"], Members(wire));
    }

    [Fact]
    public void PowerChanged_ShouldCarryExactlyTypeDeltaPowerSource()
    {
        // §3.2.24 fixes the member set at exactly four: `type`, `delta`, `power`,
        // and `source` (D-2). Widening the `source` VALUE set adds no member, and
        // a second parallel event model would be a second representation of one
        // fact.
        var wire = Serialize(BattleEvent.ForPowerChanged(
            new PowerChangedEvent(PowerChangeSource.Boss, Delta: -10, Power: 0)));

        Assert.Equal(["delta", "power", "source", "type"], Members(wire));
        Assert.False(wire.TryGetProperty("cardId", out _));
        Assert.False(wire.TryGetProperty("relicId", out _));
        Assert.False(wire.TryGetProperty("sourceId", out _));
        Assert.False(wire.TryGetProperty("reason", out _));
    }

    [Fact]
    public void PowerChanged_ShouldProjectABossDrain_WithItsActualClampedDelta()
    {
        // §3.2.24 item 5 defines "boss" as a Boss-owned Power mutation, and item 1
        // makes `delta` the change the mutation ACTUALLY applied. A 20 drain against
        // a Power of 10 removes 10, so the projected item is delta -10 at power 0 —
        // never delta -20.
        var wire = Serialize(BattleEvent.ForPowerChanged(
            new PowerChangedEvent(PowerChangeSource.Boss, Delta: -10, Power: 0)));

        Assert.Equal("PowerChanged", wire.GetProperty("type").GetString());
        Assert.Equal(-10, wire.GetProperty("delta").GetInt32());
        Assert.Equal(0, wire.GetProperty("power").GetInt32());
        Assert.Equal("boss", wire.GetProperty("source").GetString());
    }

    [Fact]
    public void PowerChanged_ShouldWriteAZeroDeltaAndAZeroPower()
    {
        // §3.2.24 items 1–2 / §3.2.5: a zero is a real value where it occurs and is
        // sent as 0 — never omitted. The cap absorbing a grant is exactly that case.
        var wire = Serialize(BattleEvent.ForPowerChanged(
            new PowerChangedEvent(PowerChangeSource.Relic, Delta: 0, Power: 0)));

        Assert.Equal(0, wire.GetProperty("delta").GetInt32());
        Assert.Equal(0, wire.GetProperty("power").GetInt32());
    }

    [Fact]
    public void PowerChanged_ShouldWriteANegativeDelta()
    {
        // §3.2.24 item 1: delta is signed and its sign is the mutation's own — a
        // Card cost spend is negative. The source names the owning stage, so the
        // direction is never inferred from it.
        var wire = Serialize(BattleEvent.ForPowerChanged(
            new PowerChangedEvent(PowerChangeSource.Card, Delta: -20, Power: 30)));

        Assert.Equal(-20, wire.GetProperty("delta").GetInt32());
        Assert.Equal(30, wire.GetProperty("power").GetInt32());
    }

    [Fact]
    public void PowerChanged_ShouldWriteAPositiveCardDelta()
    {
        // §3.2.24 item 1 / D-7: `"card"` is not a cost-only value. A Card Power
        // gain is a Card-owned mutation and is projected with its own positive
        // sign under the same source.
        var wire = Serialize(BattleEvent.ForPowerChanged(
            new PowerChangedEvent(PowerChangeSource.Card, Delta: 25, Power: 65)));

        Assert.Equal(25, wire.GetProperty("delta").GetInt32());
        Assert.Equal(65, wire.GetProperty("power").GetInt32());
        Assert.Equal("card", wire.GetProperty("source").GetString());
    }

    // =======================================================================
    // The batch — §3.2.1 item 3, §3.2.12 item 4
    // =======================================================================

    [Fact]
    public void Projection_ShouldPreserveTheRelicBatchsOrderAndArity()
    {
        // §3.2.1 item 3 / §3.2.12 item 4: the projection is one-to-one and
        // order-preserving, and RELIC_RULES.md §3.2.23 item 3 reads the deterministic
        // trigger order from that position. Nothing is sorted, filtered, or merged —
        // so the batch below stays exactly as emitted.
        var events = new[]
        {
            BattleEvent.ForRelicTriggered(new RelicTriggeredEvent("relic_slot_1")),
            BattleEvent.ForPowerChanged(new PowerChangedEvent(PowerChangeSource.Relic, 10, 10)),
            BattleEvent.ForRelicTriggered(new RelicTriggeredEvent("relic_slot_2")),
        };

        var wire = BattleEventWireProjection.Project(events);

        Assert.Equal(3, wire.Count);
        Assert.Equal("RelicTriggered", wire[0].Type);
        Assert.Equal("relic_slot_1", wire[0].RelicId);
        Assert.Equal("PowerChanged", wire[1].Type);
        Assert.Equal(10, wire[1].Delta);
        Assert.Equal("RelicTriggered", wire[2].Type);
        Assert.Equal("relic_slot_2", wire[2].RelicId);
    }

    [Fact]
    public void RelicTriggered_ShouldNotBeDroppedOrDuplicated()
    {
        // §3.1 item 1: the batch is atomic, and dropping or duplicating one item would
        // break the one-to-one projection the batch's integrity depends on.
        var events = Enumerable.Range(0, 5)
            .Select(slot => BattleEvent.ForRelicTriggered(new RelicTriggeredEvent($"relic_slot_{slot}")))
            .ToArray();

        var wire = BattleEventWireProjection.Project(events);

        Assert.Equal(5, wire.Count);
        Assert.All(wire, item => Assert.Equal("RelicTriggered", item.Type));
        Assert.Equal(
            ["relic_slot_0", "relic_slot_1", "relic_slot_2", "relic_slot_3", "relic_slot_4"],
            wire.Select(item => item.RelicId!).ToArray());
    }

    [Fact]
    public void Projection_ShouldPreserveTwoCardPowerMutationsAsTwoOrderedItems()
    {
        // §3.2.1 item 3 / §3.2.12 item 4 / §3.2.24 item 6 (D-8): the projection is
        // one-to-one and order-preserving. Two authoritative Power mutations from one
        // CardCast therefore project as two items in the mutation's own order, each
        // carrying its own delta and resulting power — the collapsed net form
        // (delta +15 at power 65) is what the contract forbids.
        var events = new[]
        {
            BattleEvent.CreateCardCast("card-cost-and-power", 10),
            BattleEvent.ForPowerChanged(new PowerChangedEvent(PowerChangeSource.Card, -10, 40)),
            BattleEvent.ForPowerChanged(new PowerChangedEvent(PowerChangeSource.Card, 25, 65)),
        };

        var wire = BattleEventWireProjection.Project(events);

        Assert.Equal(3, wire.Count);
        Assert.Equal("CardCast", wire[0].Type);

        Assert.Equal("PowerChanged", wire[1].Type);
        Assert.Equal(-10, wire[1].Delta);
        Assert.Equal(40, wire[1].Power);
        Assert.Equal("card", wire[1].Source);

        Assert.Equal("PowerChanged", wire[2].Type);
        Assert.Equal(25, wire[2].Delta);
        Assert.Equal(65, wire[2].Power);
        Assert.Equal("card", wire[2].Source);
    }

    [Fact]
    public void Projection_ShouldCarryAMatchBossAndRelicSourceInOneBatch()
    {
        // §3.2.24 item 3: the value set is per-event and closed at four. A batch
        // carrying the match, relic, and boss sources projects each to its own
        // documented lowercase name — no value is remapped and none is dropped.
        var events = new[]
        {
            BattleEvent.ForPowerChanged(new PowerChangedEvent(PowerChangeSource.Relic, 10, 10)),
            BattleEvent.ForPowerChanged(new PowerChangedEvent(PowerChangeSource.Match, 30, 40)),
            BattleEvent.ForPowerChanged(new PowerChangedEvent(PowerChangeSource.Boss, -10, 30)),
        };

        var wire = BattleEventWireProjection.Project(events);

        Assert.Equal(["relic", "match", "boss"], wire.Select(item => item.Source!).ToArray());
        Assert.Equal([10, 30, -10], wire.Select(item => item.Delta).ToArray());
        Assert.Equal([10, 40, 30], wire.Select(item => item.Power).ToArray());
    }
}
