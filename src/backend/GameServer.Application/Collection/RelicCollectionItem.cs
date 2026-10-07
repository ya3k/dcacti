using GameServer.Domain.Relics;

namespace GameServer.Application.Collection;

/// <summary>
/// One owned Relic as <c>GET /api/relics</c> reports it
/// (<c>API_CONTRACTS.md</c> §5.4).
///
/// <code>
/// relicId           string   Relic.RelicInstanceId
/// name              string   RelicDefinition.Name
/// trigger           string   RelicDefinition.Trigger
/// condition         object?  RelicDefinition.Condition — structured, nullable
/// effectDefinition  array    RelicDefinition.EffectDefinition — structured, 1..n
/// </code>
///
/// <b>Exactly the five documented members.</b> §5.4 states <c>playerId</c>,
/// <c>acquiredAt</c>, and <c>definitionId</c> are <b>not</b> exposed. The record
/// is what keeps that structural.
///
/// <b>The three content members are carried, never composed.</b>
/// <see cref="Trigger"/>, <see cref="Condition"/>, and
/// <see cref="EffectDefinition"/> are the definition row's own values, read in
/// the bulk definition read this projection already performed to resolve
/// <see cref="Name"/>. Their vocabularies and structures are owned by
/// <c>RELIC_RULES.md</c> §3/§8.1–§8.3 and their storage by <c>DATABASE.md</c>
/// §1; nothing here derives an effect from the name, the ids, the category, or
/// any heuristic, and nothing is defaulted (<c>AGENTS.md</c> §7,
/// <c>RELIC_RULES.md</c> §8.2 item 1).
///
/// <b>It is the instance identity, not the definition.</b> <c>relicId</c> is the
/// owned copy (<c>RELIC_RULES.md</c> §2.2), which is what §3's
/// <c>relicLoadout</c> submits and what <c>PetState.EquippedRelics[]</c>
/// carries; the definition is the source of the display name <b>and</b> of the
/// three content members. The definition's own identity is deliberately not a
/// member: two owned instances of one definition repeat the content exactly as
/// they already repeat the name.
///
/// <b>No equip/loadout member.</b> §5.6 excludes <c>isEquipped</c>,
/// <c>equipped</c>, <c>slot</c>, <c>loadoutPosition</c>, and <c>active</c> from
/// every §5 response: equip state is battle-scoped and unpersisted
/// (<c>DATABASE.md</c> §2, ADR-011).
/// </summary>
/// <param name="RelicId">
/// <c>Relic.RelicInstanceId</c> — the owned instance identity
/// (<c>DATABASE.md</c> §1, <c>RELIC_RULES.md</c> §2.2).
/// </param>
/// <param name="Name">
/// <c>RelicDefinition.Name</c> — the static content's display name
/// (<c>RELIC_RULES.md</c> §1, §6), read through the instance's definition
/// reference.
/// </param>
/// <param name="Trigger">
/// <c>RelicDefinition.Trigger</c> — the Relic's one primary Trigger identity
/// from <c>RELIC_RULES.md</c> §3's closed list. Always present; never null.
/// </param>
/// <param name="Condition">
/// <c>RelicDefinition.Condition</c> — the Trigger's optional extra condition as
/// the structured form-plus-threshold value of <c>RELIC_RULES.md</c> §8.1, or
/// <c>null</c> when the Relic declares none (§8.1 item 4). Nullable by
/// contract, and never defaulted to a sentinel condition.
/// </param>
/// <param name="EffectDefinition">
/// <c>RelicDefinition.EffectDefinition</c> as stored — the structured
/// <c>EffectDefinition[]</c> of <c>RELIC_RULES.md</c> §8.2–§8.3, always present
/// and never empty (<c>DATABASE.md</c> §1 stores the column NOT NULL and every
/// Relic states at least one effect).
/// </param>
public sealed record RelicCollectionItem(
    string RelicId,
    string Name,
    string Trigger,
    RelicCondition? Condition,
    RelicEffectDefinitions EffectDefinition);
