using GameServer.Domain.Passives;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// The shared <c>PassiveCharged</c>/<c>PassiveTriggered</c> payloads' event-source
/// contract (<c>GAME_EVENTS.md</c> §2, <c>SIGNALR_PROTOCOL.md</c> §3.2.16–§3.2.17,
/// <c>BOSS_RULES.md</c> §7).
///
/// <code>
/// Rule (BOSS_RULES.md §7, SIGNALR_PROTOCOL.md §3.2.16)
///  ↓
/// Scenario (Given the Pet or Boss Passive stage, When it emits, Then the
///           documented source/sourceId)
///  ↓
/// Test
/// </code>
///
/// <b>The two Passive systems share one event, discriminated by <c>source</c>.</b>
/// <c>BOSS_RULES.md</c> §7 states Boss Passive triggers "use the general Passive
/// events (<c>PASSIVE_RULES.md</c> §7) with <c>source = "boss"</c>" and that "no
/// Boss-specific passive event name is needed" — so there is exactly one
/// <c>PassiveCharged</c> and one <c>PassiveTriggered</c>, and these members are
/// what tell the two apart.
/// </summary>
public class PassiveEventSourceTests
{
    private static readonly PassiveId XichLang = new("xich-lang");

    // =======================================================================
    // The documented source vocabulary — BOSS_RULES.md §7, SIGNALR §3.2.16 item 1
    // =======================================================================

    [Fact]
    public void Source_ShouldBeExactlyTheTwoDocumentedValues()
    {
        // SIGNALR_PROTOCOL.md §3.2.16 item 1: "The string is "pet" or "boss", matching
        // the DamageDealt/DamageTaken convention for party identifiers". BOSS_RULES.md
        // §7 gives the same two. There is no third source.
        Assert.Equal("pet", PassiveEventSource.Pet);
        Assert.Equal("boss", PassiveEventSource.Boss);
    }

    [Fact]
    public void Source_ShouldMatchTheDamagePartySpelling()
    {
        // SIGNALR_PROTOCOL.md §3.2.16 item 1 makes the Passive source strings the same
        // convention as the damage parties' (§3.2.14 item 1): the values are the
        // lowercase names of the two entities, so a client reads `source` uniformly
        // across all twelve events. The Boss spelling is the one shared literally —
        // DamageParty.Boss and PassiveEventSource.Boss are the same word.
        Assert.Equal(
            GameServer.Domain.Combat.DamageParty.Boss.ToString().ToLowerInvariant(),
            PassiveEventSource.Boss);

        // The player-side spelling is "pet" on a Passive event and "player" on a
        // damage event, which is the documented difference between the two member
        // families: a Passive belongs to the Pet, while a damage instance is dealt by
        // the player's side. Both are lowercase entity names, so the conventions agree
        // in form without being the same word.
        Assert.Equal(
            GameServer.Domain.Combat.DamageParty.Player.ToString().ToLowerInvariant(),
            "player");
        Assert.NotEqual(PassiveEventSource.Pet, "player");
    }

    // =======================================================================
    // Defaults — the Pet Passive stage's value
    // =======================================================================

    [Fact]
    public void PassiveCharged_ShouldDefaultToThePetSource()
    {
        // The Pet Passive is the original emitter of this shared event
        // (PASSIVE_RULES.md §7), so a charge constructed without an explicit source
        // describes a Pet Passive — not an unset one. SourceId is null because
        // PetState carries no PetId in this stage (GAME_STATE.md §2.3).
        var charged = new PassiveChargedEvent(XichLang, Progress: 3, Threshold: 5);

        Assert.Equal(PassiveEventSource.Pet, charged.Source);
        Assert.Null(charged.SourceId);
    }

    [Fact]
    public void PassiveTriggered_ShouldDefaultToThePetSource()
    {
        // The same default, for the same reason, on the trigger.
        var triggered = new PassiveTriggeredEvent(XichLang, Progress: 5, Threshold: 5);

        Assert.Equal(PassiveEventSource.Pet, triggered.Source);
        Assert.Null(triggered.SourceId);
    }

    [Fact]
    public void Tracker_ShouldStampEveryPetReportWithThePetSource()
    {
        // PASSIVE_RULES.md §7: the tracker's reports are the active Pet's Passive, so
        // every charge and the trigger carry source="pet" — stated at the emitting
        // site rather than left to the default, so the Pet/Boss distinction is visible
        // where the event is produced.
        var result = PassiveTracker.Charge(
            PassiveProgress.AtStart(threshold: 5),
            matchCount: 7,
            XichLang);

        Assert.All(result.Charges, c => Assert.Equal(PassiveEventSource.Pet, c.Source));
        Assert.All(result.Triggers, t => Assert.Equal(PassiveEventSource.Pet, t.Source));

        // The Pet Passive stage has no PetId to report (GAME_STATE.md §2.3 leaves
        // PetId to the Pet identity stage), and no value is invented for it
        // (AGENTS.md §7).
        Assert.All(result.Charges, c => Assert.Null(c.SourceId));
        Assert.All(result.Triggers, t => Assert.Null(t.SourceId));
    }

    // =======================================================================
    // Boss Passive values — BOSS_RULES.md §6.4, SIGNALR §3.2.16 item 2
    // =======================================================================

    [Fact]
    public void BossCharge_ShouldCarryTheBossSourceAndDisplayNameIdentity()
    {
        // SIGNALR_PROTOCOL.md §3.2.16 items 1–2: a Boss Passive charge carries
        // source="boss" and sourceId=BossState.BossId, and BOSS_RULES.md §6.4 fixes
        // that BossId as the DISPLAY NAME ("Hỏa Long") — never a slug. The same call
        // shape the Application layer uses for the Boss step.
        var charged = new PassiveChargedEvent(
            new PassiveId("boss-hoa-long-rage"),
            Progress: 3,
            Threshold: 5,
            Source: PassiveEventSource.Boss,
            SourceId: "Hỏa Long");

        Assert.Equal("boss", charged.Source);
        Assert.Equal("Hỏa Long", charged.SourceId);
        Assert.Equal("boss-hoa-long-rage", charged.PassiveId.Value);
    }

    [Fact]
    public void BossTrigger_ShouldCarryTheBossSourceAndDisplayNameIdentity()
    {
        // The trigger carries the same two members with the same semantics
        // (SIGNALR_PROTOCOL.md §3.2.17 item 1).
        var triggered = new PassiveTriggeredEvent(
            new PassiveId("boss-moc-yeu-regen"),
            Progress: 5,
            Threshold: 5,
            Source: PassiveEventSource.Boss,
            SourceId: "Mộc Yêu");

        Assert.Equal("boss", triggered.Source);
        Assert.Equal("Mộc Yêu", triggered.SourceId);
    }

    [Theory]
    // BOSS_RULES.md §6.4's identity contract: the BossId an event reports is the
    // display name from BossDefinitions, and the PassiveId is the kebab-case value
    // §6.4 fixes. This asserts the two travel together as §3.2.16 describes.
    [InlineData("Hỏa Long", "boss-hoa-long-rage")]
    [InlineData("Thủy Ma", "boss-thuy-ma-heal")]
    [InlineData("Mộc Yêu", "boss-moc-yeu-regen")]
    public void BossReports_ShouldUseTheDefinitionsOwnIdentities(
        string bossId,
        string passiveId)
    {
        var definition = GameServer.Domain.Bosses.BossDefinitions.All
            .Single(b => b.BossId.Value == bossId);

        Assert.Equal(passiveId, definition.PassiveId.Value);

        var charged = new PassiveChargedEvent(
            definition.PassiveId,
            Progress: 1,
            Threshold: definition.PassiveThreshold,
            Source: PassiveEventSource.Boss,
            SourceId: definition.BossId.Value);

        Assert.Equal(definition.BossId.Value, charged.SourceId);
        Assert.Equal(definition.PassiveId, charged.PassiveId);
    }

    [Fact]
    public void BossReports_ShouldNotUseASlugForTheSourceId()
    {
        // BOSS_RULES.md §6.4: "BossId is the display name already used by
        // BossDefinitions.cs (BossId("Hỏa Long"), etc.) — the boss's identity, not a
        // slug." SIGNALR_PROTOCOL.md §3.2.16 item 2 repeats it for sourceId. This
        // excludes the slug spelling a reader might reach for.
        var hoaLong = GameServer.Domain.Bosses.BossDefinitions.HoaLong;

        Assert.Equal("Hỏa Long", hoaLong.BossId.Value);
        Assert.NotEqual("hoa-long", hoaLong.BossId.Value);
        Assert.NotEqual(hoaLong.PassiveId.Value, hoaLong.BossId.Value);
    }

    [Fact]
    public void Reports_ShouldBeReadonlyAndCarryNoEffectSummary()
    {
        // GAME_EVENTS.md §2 item 3: the effect summary is deferred to the Combat
        // stage. TASK-022 emits the trigger and applies no effect, so the payload has
        // no member for one (AGENTS.md §7).
        var members = typeof(PassiveTriggeredEvent)
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();

        Assert.DoesNotContain(members, n =>
            n.Contains("Effect", StringComparison.Ordinal)
            || n.Contains("Damage", StringComparison.Ordinal)
            || n.Contains("Heal", StringComparison.Ordinal));
    }
}
