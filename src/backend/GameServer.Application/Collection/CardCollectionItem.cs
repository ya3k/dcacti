using GameServer.Domain.Cards;

namespace GameServer.Application.Collection;

/// <summary>
/// One unlocked Card as <c>GET /api/cards</c> reports it
/// (<c>API_CONTRACTS.md</c> §5.3).
///
/// <code>
/// cardId            string  CardDefinition.CardDefinitionId
/// name              string  CardDefinition.Name
/// category          string  CardDefinition.Category — "Basic" | "PetSkill"
/// effectDefinition  array   CardDefinition.EffectDefinition — the Card's own
///                           structured effect rule, 1..n elements
/// </code>
///
/// <b>Exactly the four documented members.</b> §5.3 defines these four and
/// states the persisted-or-definition values <c>playerId</c>, <c>powerCost</c>,
/// and <c>loadoutCopyLimit</c> are <b>not</b> exposed (they are validated
/// server-side by §3's loadout path). The record is what keeps that structural.
///
/// <b><see cref="EffectDefinition"/> is carried, never interpreted.</b> It is
/// the definition row's own value, read through the repository the read already
/// performs (<c>CardRepository.ListUnlockedAsync</c> joins
/// <c>PlayerUnlockedCard → CardDefinition</c>). Its element member names and
/// present-iff rules are <c>DATABASE.md</c> §1/§3's, and its values are owned by
/// <c>CARD_RULES.md</c> §2/§4.1; this projection neither recomposes, renames,
/// defaults, nor fills in a member the stored payload omits
/// (<c>AGENTS.md</c> §7).
///
/// <b>Unlock state is the array membership, not a member.</b> §5.3: "presence in
/// this array <b>is</b> the unlocked state — there is no <c>unlocked</c>
/// member." So there is deliberately no boolean here.
///
/// <b>No equip/loadout member.</b> §5.6 excludes <c>isEquipped</c>,
/// <c>equipped</c>, <c>slot</c>, <c>loadoutPosition</c>, and <c>active</c> from
/// every §5 response (<c>DATABASE.md</c> §2, ADR-012 item 10).
/// </summary>
/// <param name="CardId">
/// <c>CardDefinition.CardDefinitionId</c> — the identity
/// <c>POST /api/battle/start</c>'s <c>cardLoadout</c> submits (§3).
/// </param>
/// <param name="Name"><c>CardDefinition.Name</c> (<c>CARD_RULES.md</c> §1–§2).</param>
/// <param name="Category">
/// <c>CardDefinition.Category</c> as its documented wire value — one of
/// <c>"Basic"</c> and <c>"PetSkill"</c> (<c>CARD_RULES.md</c> §1,
/// <c>DATABASE.md</c> §3).
/// </param>
/// <param name="EffectDefinition">
/// <c>CardDefinition.EffectDefinition</c> as stored
/// (<c>DATABASE.md</c> §1; TASK-111 D-1b's array shape) — always present and
/// never empty, because the column is NOT NULL and every provisioned Card
/// states at least one effect.
/// </param>
public sealed record CardCollectionItem(
    string CardId,
    string Name,
    string Category,
    CardEffectDefinitions EffectDefinition);
