namespace GameServer.Domain.Relics;

/// <summary>
/// <b>One equipped Relic, resolved</b> — its owned instance identity together
/// with the shared static definition that instance references
/// (<c>RELIC_RULES.md</c> §2.2, <c>DATABASE.md</c> §1–§2).
///
/// <code>
/// EquippedRelicContent
/// ├── InstanceIdentity   the equip-slot element (RelicInstanceId)
/// └── Definition?        the RelicDefinition it references, or null
/// </code>
///
/// <b>Why the pair exists.</b> <c>RELIC_RULES.md</c> §2.2 item 2 makes a
/// <c>PetState.EquippedRelics[]</c> element an <b>identity, not a definition</b>:
/// the Relic's <c>Trigger</c>, <c>Condition</c>, and <c>EffectDefinition</c> live
/// on <see cref="RelicDefinition"/> and are deliberately not copied into the
/// element (§0 item 5 — no parallel representation). A resolver must therefore
/// carry both — the slot's instance identity (which is the event's
/// <c>relicId</c> and the modifier carriers' source key) and the definition it
/// resolves to — as one aligned value rather than re-deriving one from the other.
///
/// <b>The order of the sequence of these values IS the equip-slot order.</b>
/// <c>RELIC_RULES.md</c> §2.3 makes slot index = submitted array position + 1 and
/// §2.3 item 2 forbids re-sorting by any property; §4.2 resolves a single event's
/// eligible Relics in that order. Nothing in this type imposes, changes, or
/// repairs that order.
///
/// <b><see cref="Definition"/> is nullable, and <c>null</c> is not a fallback.</b>
/// The definition is a PostgreSQL content read (<c>DATABASE.md</c> §1) reached
/// through the Application layer's definition-lookup boundary, whose documented
/// miss outcome is <c>null</c> — a definition identity with no row. A Relic whose
/// content does not resolve has <b>no declared Trigger, Condition, or Effect</b>,
/// so nothing can be evaluated for it and none may be fabricated
/// (<c>AGENTS.md</c> §7): the resolver skips it rather than substituting a
/// default threshold, a fallback effect, or a synthetic Relic.
/// </summary>
/// <param name="InstanceIdentity">
/// The owned Relic instance identity of the equip slot
/// (<c>RELIC_RULES.md</c> §2.2, <c>GAME_STATE.md</c> §2.3) — the value
/// <c>PetState.EquippedRelics[]</c> holds and the value the emitted
/// <see cref="RelicTriggeredEvent"/> reports.
/// </param>
/// <param name="Definition">
/// The resolved static definition that instance references
/// (<c>DATABASE.md</c> §1: <c>Relic N ── 1 RelicDefinition</c>), or <c>null</c>
/// when no definition row carries its identity.
/// </param>
public readonly record struct EquippedRelicContent(
    EquippedRelicIdentity InstanceIdentity,
    RelicDefinition? Definition)
{
    /// <summary>
    /// Whether this slot resolved to a definition whose Trigger, Condition, and
    /// Effect can be read (<c>RELIC_RULES.md</c> §8).
    /// </summary>
    public bool IsResolved => Definition is not null;
}
