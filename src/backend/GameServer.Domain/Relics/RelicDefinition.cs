namespace GameServer.Domain.Relics;

/// <summary>
/// Static Relic content — one row per MVP Relic (<c>DATABASE.md</c> §1,
/// <c>RELIC_RULES.md</c> §1, §6).
///
/// <code>
/// RelicDefinition
/// ├── RelicDefinitionId  (PK)
/// ├── Name
/// ├── Trigger            a §3 Trigger identity (unchanged — a prose value)
/// ├── Condition          STRUCTURED (RELIC_RULES.md §8.1; optional)
/// └── EffectDefinition   STRUCTURED EffectDefinition[] (RELIC_RULES.md §8.2)
/// </code>
///
/// <b>Definition and instance are different things.</b> This type is the static
/// content shared by every owned copy; <see cref="Relic"/> is a Player's owned
/// instance that references this definition (<c>DATABASE.md</c> §2: Relic N ── 1
/// RelicDefinition). Ownership (<c>PlayerId</c>, <c>AcquiredAt</c>) lives on the
/// instance, not here.
///
/// <b>These are the four members <c>RELIC_RULES.md</c> §1's Relic structure
/// declares.</b> §1 defines a Relic by its <c>Trigger</c> (one supported
/// event/condition, §3), optional <c>Condition</c>, the <c>Effect</c> applied
/// when triggered, and its <c>Reset/Cooldown</c>; <c>DATABASE.md</c> §1 names the
/// stored fields <c>Trigger</c>, <c>Condition</c>, and <c>EffectDefinition</c>.
/// §8 is the canonical owner of how the latter two are <b>represented</b>.
///
/// <b><c>Condition</c> and <c>EffectDefinition</c> are STRUCTURED, not prose.</b>
/// <c>RELIC_RULES.md</c> §8.1 makes the condition a form plus its threshold
/// ("<c>"every 3 Matches"</c> as a string is not a valid <c>Condition</c>"), and
/// §8.2 makes the effect a structured <c>EffectDefinition[]</c> so that "the
/// runtime must never derive a Relic's effect from parsed prose, from the
/// Relic's <c>Name</c>, from <c>RelicDefinitionId</c> mapping, or from hardcoded
/// per-Relic logic". This supersedes TASK-082 decision R2-7 for this member
/// (<c>DATABASE.md</c> §1's Relic note item 2) — there is deliberately no prose
/// fallback and no second representation.
///
/// <b><c>Trigger</c> stays a prose identity.</b> §8.5 item 3 keeps §3's closed
/// list unchanged: "§3 is unchanged and no value is added, removed, or
/// reinterpreted. <c>Trigger</c> stays a single primary Trigger as §3 and §1
/// require." Nothing here structures, enumerates, or reinterprets it.
///
/// <b>No per-instance state is declared.</b> No MVP Relic in
/// <c>RELIC_RULES.md</c> §6 carries charges, stacks, cooldowns, or duration owned
/// by the instance; §1 places <c>Reset/Cooldown</c> on the Relic's definition, and
/// §8.4 item 4 adds no cooldown, charge, or per-Relic reset state. Nothing here
/// adds such state.
///
/// <b>This type reads content; it resolves nothing.</b> Relic trigger
/// evaluation, condition evaluation, and effect application are
/// <c>GAME_RULES.md</c> §17 step 11's resolution stage — <b>NOT IMPLEMENTED</b>
/// (<c>RELIC_RULES.md</c> §8.7) — and are not introduced by this type. Holding a
/// structured condition and effect array is what makes that stage readable, not
/// what performs it.
/// </summary>
public class RelicDefinition
{
    /// <summary>
    /// The definition's identifier (<c>DATABASE.md</c> §1:
    /// <c>RelicDefinitionId</c> (PK)) — the FK every owned
    /// <see cref="Relic"/> instance references (<c>DATABASE.md</c> §2).
    ///
    /// It is the <b>static content</b> identity and is never the
    /// battle-loadout identity: the battle snapshot carries the owned
    /// instance's <see cref="Relic.RelicInstanceId"/>
    /// (<c>RELIC_RULES.md</c> §2.2).
    /// </summary>
    public required string RelicDefinitionId { get; init; }

    /// <summary>
    /// The Relic's display name (<c>DATABASE.md</c> §1: <c>Name</c>) — e.g.
    /// "Berserker Core", "Mana Crystal" (<c>RELIC_RULES.md</c> §6's MVP
    /// reference list). It is the stable name of the Relic, not an owned
    /// instance id.
    ///
    /// <b>No rule reads it.</b> <c>RELIC_RULES.md</c> §8.2 item 1 forbids
    /// deriving a Relic's effect from its <c>Name</c>; the effect identity comes
    /// from <see cref="EffectDefinition"/>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The identity of the Relic's one primary Trigger
    /// (<c>DATABASE.md</c> §1: <c>Trigger</c>; <c>RELIC_RULES.md</c> §1, §3).
    ///
    /// A Relic declares <b>exactly one</b> primary Trigger drawn from §3's
    /// closed list ("A Relic must declare exactly one primary Trigger from this
    /// list"). New trigger types are a rule change and must go through
    /// <c>GAME_RULES.md</c> §20 before implementation — this type stores the
    /// identity only and defines no new trigger.
    ///
    /// <b>It remains a prose value from the closed list.</b> <c>RELIC_RULES.md</c>
    /// §8.5 item 3 (TASK-131 D8) leaves §3 unchanged and states that no trigger
    /// value is added, removed, or reinterpreted — so this member is deliberately
    /// <b>not</b> structured, and it is the one member of this type that §8 does
    /// not restructure.
    ///
    /// It is stored as an identity reference, not as evaluated trigger state:
    /// whether and when the Trigger fires is <c>RELIC_RULES.md</c> §3–§4's
    /// concern and is not implemented here.
    /// </summary>
    public required string Trigger { get; init; }

    /// <summary>
    /// The Trigger's optional extra Condition, <b>structured</b>
    /// (<c>DATABASE.md</c> §1: <c>Condition</c>; <c>RELIC_RULES.md</c> §8.1).
    ///
    /// It carries one of §8.1's three forms together with its threshold as an
    /// integer, so no runtime parses a sentence to learn what the Relic compares
    /// against. §8.1 item 2 places its evaluation at the point
    /// <c>GAME_RULES.md</c> §17 step 11 executes, against the current resolution
    /// state; §8.1 item 3 adds that no Relic counter is introduced.
    ///
    /// <b>It is optional</b> (<c>RELIC_RULES.md</c> §1: "plus an optional
    /// Condition, §1"; §8.1 item 4: "A Relic whose Trigger alone is its complete
    /// condition carries none"). A Relic with no extra condition carries
    /// <see langword="null"/> here rather than a sentinel condition that would
    /// read as a real one.
    ///
    /// This is content; <b>condition evaluation is not implemented here</b>
    /// (<c>RELIC_RULES.md</c> §8.7).
    /// </summary>
    public RelicCondition? Condition { get; init; }

    /// <summary>
    /// The Relic's <b>structured</b> effect declaration
    /// (<c>DATABASE.md</c> §1: <c>EffectDefinition</c>; <c>RELIC_RULES.md</c>
    /// §8.2) — an array of effect objects, one per effect, each carrying its own
    /// <c>effectType</c>/<c>valueType</c>/<c>value</c> triple plus the
    /// <c>target</c> and <c>lifetime</c> members §8.3 defines.
    ///
    /// It replaces TASK-082 R2-7's verbatim prose ("+5% ATK", "Increased Crit
    /// chance"), which carried no effect-type discriminator, no magnitude
    /// carrier, and no target or lifetime — so no resolver could read it without
    /// inventing a prose parser, which §8.2 item 1 forbids. The representation
    /// follows the contract shape <c>DATABASE.md</c> §1 records for
    /// <c>CardDefinition.EffectDefinition</c>, with the Relic vocabularies §8.2
    /// defines.
    ///
    /// It is required: every Relic states at least one effect (<c>DATABASE.md</c>
    /// §1 stores the column NOT NULL), so it is a non-nullable member holding at
    /// least one element.
    ///
    /// <b>Resolving an effect is not implemented here.</b> Applying, targeting,
    /// or reporting these effects is <c>RELIC_RULES.md</c> §8.7's
    /// unimplemented stage; static-modifier Relics such as "Burning Curse" (§6
    /// note 1) apply during a <c>COMBAT_RULES.md</c> calculation step, which is
    /// likewise out of scope and remains deferred with no provisioned row (§6
    /// note 3).
    /// </summary>
    public required RelicEffectDefinitions EffectDefinition { get; init; }
}
