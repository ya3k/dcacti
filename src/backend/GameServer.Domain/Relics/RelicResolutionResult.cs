using GameServer.Domain.Battle;

namespace GameServer.Domain.Relics;

/// <summary>
/// The outcome of evaluating the equipped Relics at <c>GAME_RULES.md</c> §17
/// step 11 — the mutated <c>PetState</c> and the ordered reports the resolution
/// emits for it (<c>RELIC_RULES.md</c> §4, §7, §8).
///
/// <code>
/// RelicResolutionResult
/// ├── PetState       the state after every eligible Relic's effects applied
/// ├── Triggered      one RelicTriggeredEvent per Relic whose effect applied
/// └── PowerChanges   one PowerChangedEvent per applied Power effect
/// </code>
///
/// <b>Every report is an output, never state.</b> The <c>PetState</c> is the
/// authoritative result; the events describe it and are never a substitute for
/// the write-back (<c>GAME_EVENTS.md</c> §3 item 6, <c>SIGNALR_PROTOCOL.md</c>
/// §4 item 6). Reading a report changes nothing.
///
/// <b>The two lists are ordered, and their order is the resolution's.</b>
/// <see cref="Triggered"/> is in equip-slot order — the order
/// <c>RELIC_RULES.md</c> §4.2 fixes and the order <c>SIGNALR_PROTOCOL.md</c>
/// §3.2.23 item 3 reads the deterministic order from, since no order index is a
/// payload member. <see cref="PowerChanges"/> is in the same pass order, and,
/// for a Relic declaring several Power effects, in the effect array's stored
/// order.
///
/// <b>Both are never <c>null</c>.</b> An equipped set in which nothing applied
/// produces empty lists — the absence of a report is a value, so no caller has to
/// distinguish "no Relic applied" from "not evaluated".
/// </summary>
/// <param name="PetState">
/// The <c>PetState</c> the stage produced: the state it was given, with each
/// eligible Relic's effects applied through the documented carriers
/// (<c>GAME_STATE.md</c> §2.3.5, §2.3.7, §2.3.4) and nothing else written.
/// </param>
/// <param name="Triggered">
/// The Relics whose effect actually applied, in equip-slot order
/// (<c>RELIC_RULES.md</c> §4.2, §7). A Relic whose Trigger fired but whose
/// Condition failed, or whose effect was not resolvable, contributes nothing.
/// </param>
/// <param name="PowerChanges">
/// The Power changes the applied <c>Power</c> effects produced, read from the
/// state the write site returned (<c>GAME_EVENTS.md</c> §2,
/// <c>SIGNALR_PROTOCOL.md</c> §3.2.24).
/// </param>
public readonly record struct RelicResolutionResult(
    PetState PetState,
    IReadOnlyList<RelicTriggeredEvent> Triggered,
    IReadOnlyList<PowerChangedEvent> PowerChanges);
