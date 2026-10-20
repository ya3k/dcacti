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
/// <c>source</c>/<c>sourceId</c>), §3.2.30 (<c>BossCardCast</c>, the sole Boss
/// ability event after ADR-029 Slice 5 / TASK-313 retired §3.2.18), and §3.2.19
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

    // The former `BossSkillCast` wire contract (§3.2.18) was removed with ADR-029
    // Slice 5 / TASK-313 (OD-311-2 = A): the event, its DTO factory and its `skillId`
    // member no longer exist, and `BossCardCast` is the sole Boss ability event. Its
    // two-identity payload is covered by the §3.2.30 tests below.

    // =======================================================================
    // BossCardCast — SIGNALR_PROTOCOL.md §3.2.30 (TASK-308)
    // =======================================================================

    [Fact]
    public void BossCardCast_ShouldCarryCardIdAndSourceId()
    {
        // §3.2.30: exactly two members plus the discriminator — the Boss Card's
        // identity (equal to the ability SkillId, CARD_RULES.md §1.1 item 3) and
        // the Boss's canonical technical Identity (BOSS_RULES.md §6.4).
        var wire = Serialize(BattleEvent.ForBossCardCast("earthquake", "boss-son-thach-ve"));

        Assert.Equal("BossCardCast", wire.GetProperty("type").GetString());
        Assert.Equal("earthquake", wire.GetProperty("cardId").GetString());
        Assert.Equal("boss-son-thach-ve", wire.GetProperty("sourceId").GetString());

        Assert.Equal(["cardId", "sourceId", "type"], Members(wire));
    }

    [Fact]
    public void BossCardCast_ShouldCarryNoCostPowerCooldownOrEffectDetail()
    {
        // §3.2.30 fixes the payload to the two identity members. The spend, the
        // resulting Power/cooldown, and the effect are authoritative state (the
        // `bossState` projection), never members of this event; and no `source`
        // member exists, which belongs to the damage and Passive events.
        var wire = Serialize(BattleEvent.ForBossCardCast("earthquake", "boss-son-thach-ve"));
        var members = Members(wire);

        Assert.DoesNotContain(
            members,
            m => m is "skillId" or "amount" or "finalDamage" or "effect" or "effectSummary"
                or "element" or "source" or "target" or "power" or "cost" or "cooldown"
                or "delta" or "shield");

        Assert.DoesNotContain("null", wire.GetRawText(), StringComparison.Ordinal);
    }

    [Theory]
    // The five provisioned Boss Cards after TASK-308 and TASK-309 Slice 4
    // (DATABASE.md §1; CARD_RULES.md §1.1 item 3): the Card identity is the
    // ability's SkillId and the source identity is the Boss's canonical technical
    // Identity (BOSS_RULES.md §6.4) — the two strings SIGNALR_PROTOCOL.md §3.2.30
    // fixes for the Boss Card event.
    [InlineData("boss-hoa-long", "flame-burst")]
    [InlineData("boss-thuy-ma", "drain-power")]
    [InlineData("boss-moc-yeu", "root")]
    [InlineData("boss-son-thach-ve", "earthquake")]
    [InlineData("boss-kim-loi-vuong", "thunder-strike")]
    public void BossCardCast_ShouldCarryTheDefinitionsIdentities(string bossId, string cardId)
    {
        var definition = BossDefinitions.All.Single(b => b.BossId.Value == bossId);

        Assert.Equal(cardId, definition.SkillId);

        var wire = Serialize(BattleEvent.ForBossCardCast(definition.SkillId, definition.BossId.Value));

        Assert.Equal("BossCardCast", wire.GetProperty("type").GetString());
        Assert.Equal(cardId, wire.GetProperty("cardId").GetString());
        Assert.Equal(bossId, wire.GetProperty("sourceId").GetString());

        Assert.Equal(["cardId", "sourceId", "type"], Members(wire));
    }

    [Fact]
    public void BossCardCast_ShouldProjectTheAbilityIdentityUnderItsOwnMemberName()
    {
        // §3.2.30 item 2: the Boss Card identity IS the ability identity
        // (CARD_RULES.md §1.1 item 3), projected under the Boss Card event's own
        // member name `cardId`. TASK-309's four migrated abilities must therefore
        // project to that same string, and the retired legacy `skillId` member name
        // (removed with ADR-029 Slice 5 / TASK-313) must not appear at all.
        foreach (var ability in new[]
                 {
                     "flame-burst", "drain-power", "root", "thunder-strike",
                 })
        {
            var definition = BossDefinitions.All.Single(b => b.SkillId == ability);

            var cardWire = Serialize(
                BattleEvent.ForBossCardCast(definition.SkillId, definition.BossId.Value));

            Assert.Equal(ability, cardWire.GetProperty("cardId").GetString());
            Assert.Equal(
                definition.BossId.Value,
                cardWire.GetProperty("sourceId").GetString());

            // §3.2.30 item 7: BossCardCast is never a CardCast and carries no `skillId`.
            Assert.False(cardWire.TryGetProperty("skillId", out _));
            Assert.NotEqual("CardCast", cardWire.GetProperty("type").GetString());
        }
    }

    [Fact]
    public void Projection_WithBossCardCast_ShouldRemainOneToOneAndOrderPreserving()
    {
        // §3.2.1 item 3 / §3.2.12 item 4: N Domain events produce N wire items in
        // the same order. The new Boss Card event is not an exception, and it does
        // not displace or merge with the legacy Boss Skill event.
        var events = new[]
        {
            BattleEvent.ForPassiveCharged(
                new PassiveChargedEvent(
                    new PassiveId("son-thach-ve-enrage"), 1, 5,
                    PassiveEventSource.Boss, "boss-son-thach-ve")),
            BattleEvent.ForBossCardCast("earthquake", "boss-son-thach-ve"),
        };

        var wire = BattleEventWireProjection.Project(events);

        Assert.Equal(events.Length, wire.Count);

        Assert.Equal(
            ["PassiveCharged", "BossCardCast"],
            wire.Select(w => w.Type));
    }

    // =======================================================================
    // PowerChanged — SIGNALR_PROTOCOL.md §3.2.24 (TASK-309: the drain's
    // Pet-side mutation travels beside BossCardCast in the round's one batch)
    // =======================================================================

    [Fact]
    public void PowerChanged_ShouldProjectTheBossSourceAndTheSignedPetDelta()
    {
        // §3.2.24: exactly four members — the discriminator, the signed delta, the
        // resulting Power, and the stage that owns the mutation projected to its
        // lowercase contract name. A Power-draining Boss Card cast is the Boss
        // Response stage's mutation, so its value is "boss" and its delta is the
        // negative drain (item 1: the sign is the mutation's, not the source's).
        var wire = Serialize(BattleEvent.ForPowerChanged(new PowerChangedEvent(
            PowerChangeSource.Boss,
            Delta: -20,
            Power: 30)));

        Assert.Equal("PowerChanged", wire.GetProperty("type").GetString());
        Assert.Equal("boss", wire.GetProperty("source").GetString());
        Assert.Equal(-20, wire.GetProperty("delta").GetInt32());
        Assert.Equal(30, wire.GetProperty("power").GetInt32());

        Assert.Equal(["delta", "power", "source", "type"], Members(wire));
    }

    [Fact]
    public void PowerChanged_ShouldWriteTheZeroPowerAsZero_NotOmitIt()
    {
        // §3.2.24 items 1–2 / §3.2.5: a drain clamped at the 0 floor is a real
        // resulting value and is written as 0 — the member's presence never depends
        // on its value. A drain the floor absorbs whole still reports the delta it
        // produced.
        var wire = Serialize(BattleEvent.ForPowerChanged(new PowerChangedEvent(
            PowerChangeSource.Boss,
            Delta: 0,
            Power: 0)));

        Assert.Equal(0, wire.GetProperty("delta").GetInt32());
        Assert.Equal(0, wire.GetProperty("power").GetInt32());
        Assert.DoesNotContain("null", wire.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public void Projection_WithTheDrainPowerChangedBeforeBossCardCast_ShouldRemainOneToOneAndOrdered()
    {
        // GAME_EVENTS.md §1 / §2 item 5: the PowerDrain's PowerChanged is carried in
        // the same ordered batch BEFORE that cast's BossCardCast, after the step-18b
        // Passive items. §3.2.1 item 3 / §3.2.12 item 4 make the projection
        // one-to-one and order-preserving, so the position the resolution produced is
        // the position the client receives — and neither item is merged, reordered, or
        // dropped.
        var events = new[]
        {
            BattleEvent.ForPassiveCharged(
                new PassiveChargedEvent(
                    new PassiveId("boss-moc-yeu-regen"), 1, 8,
                    PassiveEventSource.Boss, "boss-moc-yeu")),
            BattleEvent.ForPowerChanged(new PowerChangedEvent(
                PowerChangeSource.Boss,
                Delta: -20,
                Power: 30)),
            BattleEvent.ForBossCardCast("drain-power", "boss-thuy-ma"),
        };

        var wire = BattleEventWireProjection.Project(events);

        Assert.Equal(events.Length, wire.Count);

        Assert.Equal(
            ["PassiveCharged", "PowerChanged", "BossCardCast"],
            wire.Select(item => item.Type));

        // The two TASK-309 items keep their own documented member sets — the drain is
        // not folded into the cast, and the cast does not repeat the Power value.
        var changed = wire[1];
        Assert.Equal(-20, changed.Delta);
        Assert.Equal(30, changed.Power);
        Assert.Equal("boss", changed.Source);
        Assert.Null(changed.CardId);
        Assert.Null(changed.SourceId);

        var cast = wire[2];
        Assert.Equal("drain-power", cast.CardId);
        Assert.Equal("boss-thuy-ma", cast.SourceId);
        Assert.Null(cast.Delta);
        Assert.Null(cast.Power);
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
    // The committed board action's boundary pair —
    // SIGNALR_PROTOCOL.md §3.2.26–§3.2.27
    // =======================================================================

    [Fact]
    public void SwapStarted_ShouldCarryTheActorAndBothCells_AndNothingElse()
    {
        // §3.2.26: four members — the discriminator, the acting side, and the two
        // §1.0 cells the accepted action named. No battleId, no serverSequence, no
        // turn, no actionId, no board.
        var started = Serialize(BattleEvent.ForSwapStarted(DamageParty.Boss, 27, 28));

        Assert.Equal("SwapStarted", started.GetProperty("type").GetString());
        Assert.Equal("boss", started.GetProperty("actor").GetString());
        Assert.Equal(27, started.GetProperty("fromCell").GetInt32());
        Assert.Equal(28, started.GetProperty("toCell").GetInt32());

        Assert.Equal(
            ["actor", "fromCell", "toCell", "type"],
            Members(started).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void SwapStarted_ShouldProjectBothSidesToTheirLowercaseContractNames()
    {
        // §3.2.26 item 2 / §3.2.4: the two-sided identity reaches the wire as the
        // documented lowercase name — the same two words the damage events'
        // source/target and the Passive events' source already use — and never as
        // the Domain enum's ordinal or a PascalCase variant.
        Assert.Equal(
            "player",
            Serialize(BattleEvent.ForSwapStarted(DamageParty.Player, 25, 33))
                .GetProperty("actor").GetString());

        Assert.Equal(
            "boss",
            Serialize(BattleEvent.ForSwapStarted(DamageParty.Boss, 25, 33))
                .GetProperty("actor").GetString());
    }

    [Fact]
    public void SwapStarted_ShouldWriteCellZero_NotOmitIt()
    {
        // §3.2.26 item 4: 0 is a real §1.0 cell index and is written as 0. Neither
        // cell has an "not applicable" case, so §3.2.5's omission rule never applies:
        // a null here would be an inapplicable member, not a first-column cell.
        var started = Serialize(BattleEvent.ForSwapStarted(DamageParty.Player, 0, 1));

        Assert.Equal(0, started.GetProperty("fromCell").GetInt32());
        Assert.Equal(1, started.GetProperty("toCell").GetInt32());
    }

    [Fact]
    public void SwapResolved_ShouldCarryTheOutcomeAlone()
    {
        // §3.2.27: the delivered outcome, under the member name BattleWon/BattleLost
        // already own, with this event's own value set. On the delivered path the
        // value is always "committed" (a rejected action emits no event at all).
        var resolved = Serialize(BattleEvent.ForSwapResolved(committed: true));

        Assert.Equal("SwapResolved", resolved.GetProperty("type").GetString());
        Assert.Equal("committed", resolved.GetProperty("outcome").GetString());

        Assert.Equal(
            ["outcome", "type"],
            Members(resolved).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void SwapResolved_ShouldCarryNoActorAndNoCells()
    {
        // §3.2.27 item 5: the actor is the sibling SwapStarted's member and the cells
        // are its members too. A second copy here could disagree with it, and §3.2.12
        // item 1 admits no member outside this event's own table.
        var resolved = Serialize(BattleEvent.ForSwapResolved(committed: true));

        foreach (var member in new[] { "actor", "fromCell", "toCell" })
        {
            Assert.False(resolved.TryGetProperty(member, out _), $"SwapResolved must not carry {member}");
        }
    }

    [Fact]
    public void BoundaryPair_ShouldProjectInDeliveredOrderAndOmitNoItem()
    {
        // §3.2.1 item 3 / §3.2.12 item 4: the projection is one-to-one and
        // order-preserving, so the pair the resolution emitted is the pair the batch
        // carries — the Player action's, then the Boss's, each with its own actor.
        var events = new[]
        {
            BattleEvent.ForSwapStarted(DamageParty.Player, 25, 33),
            BattleEvent.ForSwapResolved(committed: true),
            BattleEvent.ForSwapStarted(DamageParty.Boss, 27, 28),
            BattleEvent.ForSwapResolved(committed: true),
        };

        var wire = BattleEventWireProjection.Project(events);

        Assert.Equal(events.Length, wire.Count);

        Assert.Equal(
            ["SwapStarted", "SwapResolved", "SwapStarted", "SwapResolved"],
            wire.Select(w => w.Type));

        Assert.Equal(
            ["player", "boss"],
            wire.Where(w => w.Type == "SwapStarted").Select(w => w.Actor));
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
            BattleEvent.ForBossCardCast("earthquake", "boss-son-thach-ve"),
            BattleEvent.ForBattleWon(0, 85),
            BattleEvent.ForBattleLost(120, 0),
        };

        var wire = BattleEventWireProjection.Project(events);

        Assert.Equal(events.Length, wire.Count);

        Assert.Equal(
            ["BossCardCast", "BattleWon", "BattleLost"],
            wire.Select(w => w.Type));
    }

    [Fact]
    public void Projection_ShouldNotTouchANonApplicableSlot()
    {
        // §3.2.1 item 2: the discriminator is read first and only that event's own
        // payload is then read. A BattleWon carries no card identity and a
        // BossCardCast carries no HP pair — asserted through the serializer, which
        // would otherwise have to read a throwing accessor.
        var won = Serialize(BattleEvent.ForBattleWon(0, 85));
        var cast = Serialize(BattleEvent.ForBossCardCast("earthquake", "boss-son-thach-ve"));

        Assert.False(won.TryGetProperty("cardId", out _));
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
                     (Serialize(BattleEvent.ForBossCardCast("earthquake", "boss-son-thach-ve")),
                         ["cardId", "sourceId"]),
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
            Serialize(BattleEvent.ForBossCardCast("earthquake", "boss-son-thach-ve")),
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
        // BossCardCast carries no outcome and no HP pair, and a BattleWon carries no
        // card identity.
        var cast = items[0];
        var won = items[1];

        Assert.False(cast.TryGetProperty("outcome", out _));
        Assert.False(cast.TryGetProperty("finalBossHp", out _));
        Assert.False(won.TryGetProperty("cardId", out _));
    }
}
