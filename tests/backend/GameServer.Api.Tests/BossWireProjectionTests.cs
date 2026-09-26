using System.Text.Json;
using System.Text.Json.Serialization;
using GameServer.Api.Hubs;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using Xunit;

namespace GameServer.Api.Tests;

/// <summary>
/// The TASK-022 wire projections — <c>SIGNALR_PROTOCOL.md</c> §3.2.16 (Passive
/// <c>source</c>/<c>sourceId</c>), §3.2.18 (<c>BossSkillCast</c>), and §3.2.19
/// (<c>BattleWon</c>/<c>BattleLost</c>).
///
/// <code>
/// Rule (SIGNALR_PROTOCOL.md §3.2.16–§3.2.19)
///  ↓
/// Scenario (Given a Domain event, When it is projected, Then the documented
///           members and no others)
///  ↓
/// Test
/// </code>
///
/// <b>These are projection tests, not transport tests.</b> They assert what
/// <see cref="BattleEventWireProjection"/> produces for a given Domain event —
/// the members and their presence rules — independently of a live connection.
/// The end-to-end path is covered by the integration and contract tests.
/// </summary>
public class BossWireProjectionTests
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
    // PassiveCharged / PassiveTriggered — §3.2.16, §3.2.17
    // =======================================================================

    [Fact]
    public void PassiveCharged_ShouldCarrySourceAndSourceId()
    {
        // §3.2.16: `source` ("pet" or "boss") and `sourceId` (the owning entity) are
        // ALWAYS present, alongside the identity, the progress, and the Threshold.
        var charged = new PassiveChargedEvent(
            new PassiveId("boss-hoa-long-rage"),
            Progress: 3,
            Threshold: 5,
            Source: PassiveEventSource.Boss,
            SourceId: "boss-hoa-long");

        var wire = Serialize(BattleEvent.ForPassiveCharged(charged));

        Assert.Equal("PassiveCharged", wire.GetProperty("type").GetString());
        Assert.Equal("boss-hoa-long-rage", wire.GetProperty("passiveId").GetString());
        Assert.Equal("boss", wire.GetProperty("source").GetString());
        Assert.Equal("boss-hoa-long", wire.GetProperty("sourceId").GetString());
        Assert.Equal(3, wire.GetProperty("progress").GetInt32());
        Assert.Equal(5, wire.GetProperty("threshold").GetInt32());
    }

    [Fact]
    public void PassiveTriggered_ShouldCarrySourceAndSourceId()
    {
        // §3.2.17 gives the trigger the same members with the same semantics —
        // shared with the Pet Passive, discriminated by `source`.
        var triggered = new PassiveTriggeredEvent(
            new PassiveId("boss-thuy-ma-heal"),
            Progress: 4,
            Threshold: 4,
            Source: PassiveEventSource.Boss,
            SourceId: "boss-thuy-ma");

        var wire = Serialize(BattleEvent.ForPassiveTriggered(triggered));

        Assert.Equal("PassiveTriggered", wire.GetProperty("type").GetString());
        Assert.Equal("boss-thuy-ma-heal", wire.GetProperty("passiveId").GetString());
        Assert.Equal("boss", wire.GetProperty("source").GetString());
        Assert.Equal("boss-thuy-ma", wire.GetProperty("sourceId").GetString());
        Assert.Equal(4, wire.GetProperty("progress").GetInt32());
        Assert.Equal(4, wire.GetProperty("threshold").GetInt32());
    }

    [Fact]
    public void PassiveEvents_ShouldCarryExactlyTheDocumentedMembers()
    {
        // §3.2.16/§3.2.17 fix the member set. `effect summary` is the member
        // GAME_EVENTS.md §2 item 3 records as DEFERRED, so its absence is the
        // documented position rather than an omission.
        var charged = Serialize(BattleEvent.ForPassiveCharged(
            new PassiveChargedEvent(
                new PassiveId("xich-lang"), 1, 5,
                PassiveEventSource.Pet, SourceId: null)));

        Assert.Equal(
            ["passiveId", "progress", "source", "threshold", "type"],
            Members(charged));

        var triggered = Serialize(BattleEvent.ForPassiveTriggered(
            new PassiveTriggeredEvent(
                new PassiveId("xich-lang"), 5, 5,
                PassiveEventSource.Pet, SourceId: null)));

        Assert.Equal(
            ["passiveId", "progress", "source", "threshold", "type"],
            Members(triggered));
    }

    [Fact]
    public void PassiveEvents_ShouldOmitSourceId_WhenTheEmitterHadNone()
    {
        // §3.2.5: a member that is not applicable is OMITTED, never written as JSON
        // null. The Pet Passive stage has no PetId in this stage (GAME_STATE.md
        // §2.3), so sourceId is genuinely absent there — while `source` is always
        // present, because it always has a value.
        var charged = Serialize(BattleEvent.ForPassiveCharged(
            new PassiveChargedEvent(
                new PassiveId("xich-lang"), 3, 5,
                PassiveEventSource.Pet, SourceId: null)));

        Assert.False(charged.TryGetProperty("sourceId", out _));
        Assert.Equal("pet", charged.GetProperty("source").GetString());

        // And nothing is ever written as JSON null.
        Assert.DoesNotContain("null", charged.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public void PassiveEvents_ShouldProjectThePetSourceValue()
    {
        // The Pet Passive's own value is "pet" (§3.2.16 item 1) — the projection
        // passes the payload's source through unchanged rather than substituting one.
        var charged = Serialize(BattleEvent.ForPassiveCharged(
            new PassiveChargedEvent(
                new PassiveId("xich-lang"), 3, 5,
                PassiveEventSource.Pet, SourceId: "xich-lang")));

        Assert.Equal("pet", charged.GetProperty("source").GetString());
        Assert.Equal("xich-lang", charged.GetProperty("sourceId").GetString());
    }

    // =======================================================================
    // BossSkillCast — §3.2.18
    // =======================================================================

    [Fact]
    public void BossSkillCast_ShouldCarrySkillIdAndSourceId()
    {
        // §3.2.18: exactly two members plus the discriminator — the Skill's identity
        // and the Boss's canonical technical Identity (BOSS_RULES.md §6.4).
        var wire = Serialize(BattleEvent.ForBossSkillCast("flame-burst", "boss-hoa-long"));

        Assert.Equal("BossSkillCast", wire.GetProperty("type").GetString());
        Assert.Equal("flame-burst", wire.GetProperty("skillId").GetString());
        Assert.Equal("boss-hoa-long", wire.GetProperty("sourceId").GetString());

        Assert.Equal(["skillId", "sourceId", "type"], Members(wire));
    }

    [Theory]
    // BOSS_RULES.md §6.4's identity contract, as the wire examples of
    // SIGNALR_PROTOCOL.md §3.2.18 use them.
    [InlineData("flame-burst", "boss-hoa-long")]
    [InlineData("drain-power", "boss-thuy-ma")]
    [InlineData("root", "boss-moc-yeu")]
    public void BossSkillCast_ShouldCarryTheDefinitionsIdentities(string skillId, string bossId)
    {
        var definition = BossDefinitions.All.Single(b => b.SkillId == skillId);

        Assert.Equal(bossId, definition.BossId.Value);

        var wire = Serialize(BattleEvent.ForBossSkillCast(definition.SkillId, definition.BossId.Value));

        Assert.Equal(skillId, wire.GetProperty("skillId").GetString());
        Assert.Equal(bossId, wire.GetProperty("sourceId").GetString());
    }

    [Fact]
    public void BossSkillCast_ShouldCarryNoEffectOrDamageDetail()
    {
        // §3.2.18 item 3: "Effect details are carried by subsequent damage events."
        // So the cast itself carries no amount, no element, and no effect summary —
        // and no `source` member, which belongs to the damage and Passive events.
        var wire = Serialize(BattleEvent.ForBossSkillCast("flame-burst", "boss-hoa-long"));
        var members = Members(wire);

        Assert.DoesNotContain(members, m => m is "amount" or "finalDamage" or "effect" or "effectSummary"
            or "element" or "source" or "target" or "burn" or "damage");
    }

    // =======================================================================
    // BattleWon / BattleLost — §3.2.19
    // =======================================================================

    [Fact]
    public void BattleWon_ShouldCarryTheOutcomeAndFinalHp()
    {
        // §3.2.19: `outcome` is the string "victory" (item 1: a string, not a
        // boolean) and both terminal HP values are always present.
        var wire = Serialize(BattleEvent.ForBattleWon(finalBossHp: 0, finalPlayerHp: 85));

        Assert.Equal("BattleWon", wire.GetProperty("type").GetString());
        Assert.Equal("victory", wire.GetProperty("outcome").GetString());
        Assert.Equal(0, wire.GetProperty("finalBossHp").GetInt32());
        Assert.Equal(85, wire.GetProperty("finalPlayerHp").GetInt32());

        Assert.Equal(["finalBossHp", "finalPlayerHp", "outcome", "type"], Members(wire));
    }

    [Fact]
    public void BattleLost_ShouldCarryTheOutcomeAndFinalHp()
    {
        // §3.2.19's second example: "defeat", with the Boss above 0 and the player at
        // 0.
        var wire = Serialize(BattleEvent.ForBattleLost(finalBossHp: 120, finalPlayerHp: 0));

        Assert.Equal("BattleLost", wire.GetProperty("type").GetString());
        Assert.Equal("defeat", wire.GetProperty("outcome").GetString());
        Assert.Equal(120, wire.GetProperty("finalBossHp").GetInt32());
        Assert.Equal(0, wire.GetProperty("finalPlayerHp").GetInt32());

        Assert.Equal(["finalBossHp", "finalPlayerHp", "outcome", "type"], Members(wire));
    }

    [Fact]
    public void OutcomeEvents_ShouldWriteZeroHp_NotOmitIt()
    {
        // §3.2.5 omits a member only when it does not belong to the event. A terminal
        // 0 HP is a real value the client renders, so it is written as 0 — the
        // member's presence never depends on its value.
        var won = Serialize(BattleEvent.ForBattleWon(0, 0));
        var lost = Serialize(BattleEvent.ForBattleLost(0, 0));

        Assert.Equal(0, won.GetProperty("finalBossHp").GetInt32());
        Assert.Equal(0, won.GetProperty("finalPlayerHp").GetInt32());
        Assert.Equal(0, lost.GetProperty("finalBossHp").GetInt32());
        Assert.Equal(0, lost.GetProperty("finalPlayerHp").GetInt32());

        Assert.DoesNotContain("null", won.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("null", lost.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public void OutcomeEvents_ShouldCarryNoRewardSummary()
    {
        // §3.2.19 item 3: "`reward summary` is deferred per GAME_EVENTS.md §2 item 9
        // — it is not a wire member yet." So no reward/progression member exists.
        foreach (var wire in new[]
                 {
                     Serialize(BattleEvent.ForBattleWon(0, 85)),
                     Serialize(BattleEvent.ForBattleLost(120, 0)),
                 })
        {
            Assert.DoesNotContain(
                Members(wire),
                m => m.Contains("reward", StringComparison.OrdinalIgnoreCase)
                    || m.Contains("xp", StringComparison.OrdinalIgnoreCase)
                    || m.Contains("loot", StringComparison.OrdinalIgnoreCase)
                    || m.Contains("progression", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Outcome_ShouldBeDerivedFromTheEventType_NotFromTheHpValues()
    {
        // §3.2.19 item 1 gives the outcome as the same names GAME_EVENTS.md §2 item 9
        // uses, and it is a constant of which outcome event this is. Held at a fixed
        // HP pair, the two events still report opposite outcomes — so the member is
        // not a function of the numbers.
        var won = Serialize(BattleEvent.ForBattleWon(0, 0));
        var lost = Serialize(BattleEvent.ForBattleLost(0, 0));

        Assert.Equal("victory", won.GetProperty("outcome").GetString());
        Assert.Equal("defeat", lost.GetProperty("outcome").GetString());
    }

    // =======================================================================
    // The projection stays one-to-one and side-effect free
    // =======================================================================

    [Fact]
    public void Projection_ShouldBeOneToOneAndOrderPreserving()
    {
        // §3.2.1 item 3 / §3.2.12 item 4: N Domain events produce N wire events in
        // the same order. The three new types must not be an exception.
        var events = new[]
        {
            BattleEvent.ForBossSkillCast("flame-burst", "boss-hoa-long"),
            BattleEvent.ForBattleWon(0, 85),
            BattleEvent.ForBattleLost(120, 0),
        };

        var wire = BattleEventWireProjection.Project(events);

        Assert.Equal(events.Length, wire.Count);

        Assert.Equal(
            ["BossSkillCast", "BattleWon", "BattleLost"],
            wire.Select(w => w.Type));
    }

    [Fact]
    public void Projection_ShouldNotTouchANonApplicableSlot()
    {
        // §3.2.1 item 2: the discriminator is read first and only that event's own
        // payload is then read. A BattleWon carries no SkillId and a BossSkillCast
        // carries no HP pair — asserted through the serializer, which would otherwise
        // have to read a throwing accessor.
        var won = Serialize(BattleEvent.ForBattleWon(0, 85));
        var cast = Serialize(BattleEvent.ForBossSkillCast("flame-burst", "boss-hoa-long"));

        Assert.False(won.TryGetProperty("skillId", out _));
        Assert.False(cast.TryGetProperty("finalBossHp", out _));
        Assert.False(cast.TryGetProperty("outcome", out _));
    }

    [Fact]
    public void Projection_ShouldStillProjectEveryEarlierEventType()
    {
        // The three new members must not have disturbed the nine that already
        // existed — each still projects to its own documented member set.
        var charged = Serialize(BattleEvent.ForPassiveCharged(
            new PassiveChargedEvent(new PassiveId("p"), 1, 5, PassiveEventSource.Pet, null)));

        var dealt = Serialize(BattleEvent.ForDamageDealt(
            new DamageDealtEvent(DamageParty.Boss, DamageParty.Player, 42)));

        Assert.Equal("PassiveCharged", charged.GetProperty("type").GetString());
        Assert.Equal("DamageDealt", dealt.GetProperty("type").GetString());

        // §3.2.14 item 3: the Boss→Player instance reports source="boss",
        // target="player" — the widened case the protocol records for §3.4.
        Assert.Equal("boss", dealt.GetProperty("source").GetString());
        Assert.Equal("player", dealt.GetProperty("target").GetString());
        Assert.Equal(42, dealt.GetProperty("amount").GetInt32());
    }

    [Fact]
    public void WireDto_ShouldUseTheContractMemberNames()
    {
        // §3.2.3 fixes camelCase explicitly and warns that a serializer default must
        // not be relied on, so every new member is named with JsonPropertyName. The
        // names are read off serialized items (the contract's own observable form),
        // because a positional record's attributes live on its parameters as well as
        // its properties.
        foreach (var (wire, expected) in new (JsonElement Wire, string[] Expected)[]
                 {
                     (Serialize(BattleEvent.ForBossSkillCast("flame-burst", "boss-hoa-long")),
                         ["skillId", "sourceId"]),
                     (Serialize(BattleEvent.ForBattleWon(0, 85)),
                         ["outcome", "finalBossHp", "finalPlayerHp"]),
                     (Serialize(BattleEvent.ForBattleLost(120, 0)),
                         ["outcome", "finalBossHp", "finalPlayerHp"]),
                     (Serialize(BattleEvent.ForPassiveCharged(
                         new PassiveChargedEvent(
                             new PassiveId("p"), 1, 5, PassiveEventSource.Boss, "boss-hoa-long"))),
                         ["source", "sourceId"]),
                 })
        {
            foreach (var name in expected)
            {
                Assert.True(
                    wire.TryGetProperty(name, out _),
                    $"the wire item is missing the §3.2 contract member '{name}': {wire.GetRawText()}");
            }
        }
    }

    [Fact]
    public void WireDto_ShouldOmitInapplicableMembersNeverWriteNull()
    {
        // §3.2.5: no member of any item is ever sent as JSON null, which is what the
        // WhenWritingNull condition on each one-of slot produces. This asserts the
        // observable rule directly: an inapplicable member is absent, and no item's
        // JSON contains a null value at all.
        var items = new[]
        {
            Serialize(BattleEvent.ForBossSkillCast("flame-burst", "boss-hoa-long")),
            Serialize(BattleEvent.ForBattleWon(0, 85)),
            Serialize(BattleEvent.ForBattleLost(120, 0)),
            Serialize(BattleEvent.ForPassiveCharged(
                new PassiveChargedEvent(
                    new PassiveId("xich-lang"), 1, 5, PassiveEventSource.Pet, SourceId: null))),
        };

        foreach (var wire in items)
        {
            // No member is present-and-null...
            Assert.All(
                wire.EnumerateObject(),
                p => Assert.NotEqual(JsonValueKind.Null, p.Value.ValueKind));

            // ...and the raw JSON contains no null literal at all.
            Assert.DoesNotContain("null", wire.GetRawText(), StringComparison.Ordinal);
        }

        // The inapplicable members are absent rather than null on a concrete item: a
        // BossSkillCast carries no outcome and no HP pair, and a BattleWon carries no
        // skillId.
        var cast = items[0];
        var won = items[1];

        Assert.False(cast.TryGetProperty("outcome", out _));
        Assert.False(cast.TryGetProperty("finalBossHp", out _));
        Assert.False(won.TryGetProperty("skillId", out _));
    }
}
