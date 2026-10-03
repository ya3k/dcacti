namespace GameServer.Domain.Relics;

/// <summary>
/// The <c>RelicTriggered</c> payload — a Relic's Effect actually applied
/// (<c>RELIC_RULES.md</c> §7, <c>GAME_EVENTS.md</c> §2,
/// <c>SIGNALR_PROTOCOL.md</c> §3.2.23).
///
/// <code>
/// RelicTriggered: relicId   (SIGNALR_PROTOCOL.md §3.2.23)
/// </code>
///
/// <b>One member, and it is an identity.</b> <c>RELIC_RULES.md</c> §7 defines the
/// event as reporting "<b>that</b> a Relic's Effect applied and <b>which</b>
/// Relic applied it", and §2.2 item 3 fixes the identity as the <b>owned Relic
/// instance</b> — the same value <c>PetState.EquippedRelics[]</c> holds. It is
/// therefore the instance identity and never the static
/// <see cref="RelicDefinition.RelicDefinitionId"/>: two owned instances of one
/// definition are two distinct equipped Relics and two distinct reports
/// (<c>RELIC_RULES.md</c> §2.4 item 3).
///
/// <b>No effect summary, and no order index.</b> <c>RELIC_RULES.md</c> §7 fixes
/// the wire shape as <c>{ type, relicId }</c> and states it "carries no effect
/// summary — the resulting state is delivered through the existing
/// <c>BattleState</c> projection" (<c>SIGNALR_PROTOCOL.md</c> §3.2.23 item 2,
/// §3.2.25). The deterministic trigger order is the <b>position of the event in
/// the emitted batch</b>, not a payload member (§3.2.23 item 3, §4.2): carrying
/// an index beside the array order would be a second spelling of one fact
/// (<c>GAME_STATE.md</c> §0 item 5). Neither member is declared here, and this
/// type adds none.
///
/// <b>Domain value, not a wire type</b> (<c>GAME_EVENTS.md</c> §3 item 1): the
/// transport layer owns serialization, exactly as for the other payloads here.
/// </summary>
/// <param name="RelicId">
/// The triggered Relic's <b>owned instance</b> identity (<c>RELIC_RULES.md</c>
/// §2.2 item 3) — the value read from the equip slot that resolved, reported
/// rather than re-derived, re-numbered, or mapped to a definition id.
/// </param>
public readonly record struct RelicTriggeredEvent(string RelicId)
{
    /// <summary>"RelicTriggered (relic-instance-1)" — for test diagnostics only.</summary>
    public override string ToString() => $"RelicTriggered ({RelicId})";
}
