namespace GameServer.Domain.Battle;

/// <summary>
/// What changed the active Pet's Power — the <c>source</c> member of
/// <c>GAME_EVENTS.md</c> §2's <c>PowerChanged</c> payload and of
/// <c>SIGNALR_PROTOCOL.md</c> §3.2.24's wire item.
///
/// <code>
/// Match   a Gem match's resource generation     (GAME_RULES.md §17 step 13)
/// Card    a Card-owned Power mutation           (GAME_RULES.md §17 step 14)
/// Relic   a Relic-owned Power mutation          (GAME_RULES.md §17 step 11)
/// Boss    a Boss-owned Power mutation           (GAME_RULES.md §17 step 18b)
/// </code>
///
/// <b>The four members are the whole documented value set.</b>
/// <c>GAME_EVENTS.md</c> §2 owns it — "source (Gem match / Card / Relic / Boss)" —
/// and <c>SIGNALR_PROTOCOL.md</c> §3.2.24 item 3 projects it to the lowercase
/// contract names <c>"match"</c>, <c>"card"</c>, <c>"relic"</c>, and
/// <c>"boss"</c>. The set is complete here even though not every stage emits
/// yet: a value set is not a list of what happens to be implemented, and
/// omitting a value would make the wire projection a partial enum that a later
/// stage could not use without redefining this contract.
///
/// <b>Each value names the STAGE that owns the mutation, not the direction of
/// the change.</b> <c>"card"</c> therefore covers both a cast's cost spend and
/// its Power-granting effect (item 2, §3.2.24 item 1) — the signed
/// <c>Delta</c> says whether that mutation was a gain or a spend.
///
/// <b><c>source</c> answers "what changed Power", not "which side".</b>
/// §3.2.24 item 3 states the boundary explicitly: this is not
/// <c>DamageParty</c>'s <c>"player"</c>/<c>"boss"</c> and not
/// <c>PassiveEventSource</c>'s entity owner. The member <i>name</i> is shared
/// with those events; the value set is this one.
///
/// <b>Member order carries no documented meaning</b> — the persisted/wire
/// contract carries the name, never the ordinal
/// (<c>SIGNALR_PROTOCOL.md</c> §3.2.4). The order below follows §2's listing.
/// </summary>
public enum PowerChangeSource
{
    /// <summary>
    /// A Match's resource generation (<c>GAME_RULES.md</c> §17 step 13,
    /// <c>COMBAT_RULES.md</c> §2) — a Match-owned Power mutation.
    /// </summary>
    Match = 0,

    /// <summary>
    /// A Card-owned Power mutation (<c>GAME_RULES.md</c> §17 step 14,
    /// <c>CARD_RULES.md</c> §3 item 4). It covers both the cast's cost spend and
    /// any Power its effects grant: the value names the owning stage, and the
    /// signed <c>Delta</c> carries the direction.
    /// </summary>
    Card = 1,

    /// <summary>
    /// A Relic's <c>Power</c> effect — the provisioned source
    /// <c>RELIC_RULES.md</c> §8.5 gives Mana Crystal's <c>+10 Power</c>,
    /// applied at <c>GAME_RULES.md</c> §17 step 11.
    /// </summary>
    Relic = 2,

    /// <summary>
    /// A Boss-owned Power mutation — the Boss Response stage's Power-draining
    /// secondary effect at <c>GAME_RULES.md</c> §17 step 18b, whose provisioned
    /// case is <c>BOSS_RULES.md</c> §6.3.1 item 2's Drain Power.
    /// </summary>
    Boss = 3,
}

/// <summary>
/// The <c>PowerChanged</c> payload — the active Pet's Power changed
/// (<c>GAME_EVENTS.md</c> §2, <c>GAME_RULES.md</c> §12,
/// <c>SIGNALR_PROTOCOL.md</c> §3.2.24).
///
/// <code>
/// PowerChanged: delta, power, source   (SIGNALR_PROTOCOL.md §3.2.24)
/// </code>
///
/// <b>Both numbers are reported, and neither is derived from the other.</b>
/// <c>Delta</c> is the <b>signed</b> change actually applied to
/// <c>PetState.Power</c> and <see cref="Power"/> is that member's value
/// <b>after</b> the change, so a reader renders the change and the resulting
/// value without recomputing either (§2 item 1). A <c>Delta</c> of <c>0</c> is a
/// real value where it occurs and is carried as <c>0</c>, never omitted
/// (§3.2.24 item 1).
///
/// <b><c>Delta</c> is the clamped change, not the requested one.</b>
/// <c>GAME_RULES.md</c> §12 makes 0–100 an invariant of <c>PetState.Power</c>.
/// A grant the cap absorbs, or a drain the floor absorbs, is therefore reported
/// as the smaller change it actually produced — a drain of 20 against a Power of
/// 10 is reported as <c>Delta -10</c> at <c>Power 0</c>.
///
/// <b>One authoritative Power mutation produces exactly one of these.</b>
/// <c>GAME_EVENTS.md</c> §2 item 4 / <c>SIGNALR_PROTOCOL.md</c> §3.2.24 item 6:
/// when one action performs several mutations — a Card cast that both pays a
/// cost and applies a Power effect — each mutation emits its own report carrying
/// that mutation's <c>Delta</c> and the resulting <c>Power</c>, in the
/// authoritative mutation order. They are never combined into one net report,
/// and a mutation that does not occur produces none.
///
/// <b>It is not the Card cost member.</b> <c>CardCast</c> deliberately carries no
/// cost (§3.2.20 item 2, §3.2.24 item 4); where a Power change must be reported,
/// it is reported here. This payload adds no member to that event.
///
/// <b>Domain value, not a wire type</b> (<c>GAME_EVENTS.md</c> §3 item 1): the
/// transport layer owns serialization, exactly as for the other payloads here.
/// </summary>
/// <param name="Source">
/// Which stage owns the mutation (<c>GAME_EVENTS.md</c> §2 item 2) — the value
/// set <see cref="PowerChangeSource"/> carries. It never states the direction:
/// the signed <see cref="Delta"/> does.
/// </param>
/// <param name="Delta">
/// The signed change the write applied to <c>PetState.Power</c>
/// (<c>SIGNALR_PROTOCOL.md</c> §3.2.24 item 1) — positive for a gain, negative
/// for a spend or a drain, and <c>0</c> where the cap or the floor absorbed the
/// whole change.
/// </param>
/// <param name="Power">
/// <c>PetState.Power</c> after the change (§3.2.24 item 2, <c>GAME_STATE.md</c>
/// §2.3) — the resulting value, never the previous one and never a delta.
/// </param>
public readonly record struct PowerChangedEvent(
    PowerChangeSource Source,
    int Delta,
    int Power)
{
    /// <summary>
    /// "PowerChanged (relic +10 -> 10)" — for test diagnostics only.
    /// </summary>
    public override string ToString() =>
        $"PowerChanged ({Source.ToString().ToLowerInvariant()} {(Delta >= 0 ? "+" : string.Empty)}{Delta} -> {Power})";
}
