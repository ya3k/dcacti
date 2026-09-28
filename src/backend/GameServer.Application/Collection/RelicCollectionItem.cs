namespace GameServer.Application.Collection;

/// <summary>
/// One owned Relic as <c>GET /api/relics</c> reports it
/// (<c>API_CONTRACTS.md</c> §5.4).
///
/// <code>
/// relicId   string  Relic.RelicInstanceId
/// name      string  RelicDefinition.Name
/// </code>
///
/// <b>Exactly the two documented members.</b> §5.4 states
/// <c>playerId</c>, <c>acquiredAt</c>, <c>definitionId</c>, and the definition's
/// <c>Trigger</c>/<c>Condition</c>/<c>EffectDefinition</c> are <b>not</b>
/// exposed — the rule texts are owned by <c>RELIC_RULES.md</c>. The record is
/// what keeps that structural.
///
/// <b>It is the instance identity, not the definition.</b> <c>relicId</c> is the
/// owned copy (<c>RELIC_RULES.md</c> §2.2), which is what §3's
/// <c>relicLoadout</c> submits and what <c>PetState.EquippedRelics[]</c>
/// carries; the definition is only the source of the display <c>name</c>.
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
public sealed record RelicCollectionItem(
    string RelicId,
    string Name);
