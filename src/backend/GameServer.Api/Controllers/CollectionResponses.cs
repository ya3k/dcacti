using System.Text.Json.Serialization;
using GameServer.Application.Collection;

namespace GameServer.Api.Controllers;

/// <summary>
/// One <c>GET /api/pets</c> array element, and the whole body of
/// <c>GET /api/pets/{petId}</c> (<c>API_CONTRACTS.md</c> §5.1, §5.2).
///
/// <code>
/// {
///   "petId":    "string",
///   "identity": "Xích Lang",
///   "element":  "Fire",
///   "tier":     "Common",
///   "star":     1,
///   "level":    12
/// }
/// </code>
///
/// <b>Exactly the six documented members.</b> §5.1 states the member list is
/// binding and exhaustive, so this type carries those six and nothing else: the
/// persisted <c>xp</c>, <c>acquiredAt</c>, <c>playerId</c>, and
/// <c>petDefinitionId</c> are §5.1's explicitly-not-exposed values, and §5.6's
/// equip/loadout members are excluded from every §5 response. A dedicated
/// response record — rather than the Domain entity, or the Application read
/// model — is what makes the member set structural: a forbidden field cannot
/// reach the wire by accident.
///
/// <b>Casing is fixed here.</b> §5.1 spells each member in camelCase, so every
/// member is named with <see cref="JsonPropertyNameAttribute"/> rather than left
/// to a host naming policy — the same technique the SignalR wire projection
/// uses.
///
/// <b>It computes nothing.</b> The projection from the Application read model is
/// a pure field mapping: <c>element</c> already arrives as its documented wire
/// value (<see cref="ElementWireValues"/>), <c>tier</c> as its documented name,
/// and Star/Level as the stored integers. Nothing is re-derived, defaulted, or
/// clamped at the transport boundary.
/// </summary>
/// <param name="PetId"><c>Pet.PetInstanceId</c> (<c>API_CONTRACTS.md</c> §5.1).</param>
/// <param name="Identity"><c>PetDefinition.Identity</c> (§5.1).</param>
/// <param name="Element">
/// <c>PetDefinition.Element</c> as <c>"Fire" | "Water" | "Earth" | "Wood" | "Metal"</c>
/// (§5.1 — never the enum's own member name).
/// </param>
/// <param name="Tier"><c>Pet.Tier</c> as its documented name (§5.1).</param>
/// <param name="Star"><c>Pet.Star</c> (§5.1).</param>
/// <param name="Level"><c>Pet.Level</c> (§5.1).</param>
public sealed record PetResponse(
    [property: JsonPropertyName("petId")] string PetId,
    [property: JsonPropertyName("identity")] string Identity,
    [property: JsonPropertyName("element")] string Element,
    [property: JsonPropertyName("tier")] string Tier,
    [property: JsonPropertyName("star")] int Star,
    [property: JsonPropertyName("level")] int Level)
{
    /// <summary>
    /// Projects one Application read model onto the §5.1/§5.2 wire shape — a pure
    /// field mapping that decides nothing.
    /// </summary>
    /// <param name="item">The projected owned Pet.</param>
    public static PetResponse From(PetCollectionItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new PetResponse(
            item.PetId,
            item.Identity,
            item.Element,
            item.Tier,
            item.Star,
            item.Level);
    }
}

/// <summary>
/// One <c>GET /api/cards</c> array element (<c>API_CONTRACTS.md</c> §5.3).
///
/// <code>
/// {
///   "cardId":   "string",
///   "name":     "string",
///   "category": "Basic"
/// }
/// </code>
///
/// <b>Exactly the three documented members.</b> <c>playerId</c>,
/// <c>powerCost</c>, <c>loadoutCopyLimit</c>, and <c>effectDefinition</c> are
/// §5.3's not-exposed values, and there is deliberately no <c>unlocked</c>
/// member: §5.3 makes presence in the array the unlocked state.
///
/// <b>No equip/loadout member</b> (§5.6).
/// </summary>
/// <param name="CardId"><c>CardDefinition.CardDefinitionId</c> (§5.3).</param>
/// <param name="Name"><c>CardDefinition.Name</c> (§5.3).</param>
/// <param name="Category"><c>"Basic"</c> or <c>"PetSkill"</c> (§5.3).</param>
public sealed record CardResponse(
    [property: JsonPropertyName("cardId")] string CardId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("category")] string Category)
{
    /// <summary>Projects one Application read model onto the §5.3 wire shape.</summary>
    /// <param name="item">The projected unlocked Card.</param>
    public static CardResponse From(CardCollectionItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new CardResponse(item.CardId, item.Name, item.Category);
    }
}

/// <summary>
/// One <c>GET /api/relics</c> array element (<c>API_CONTRACTS.md</c> §5.4).
///
/// <code>
/// {
///   "relicId": "string",
///   "name":    "string"
/// }
/// </code>
///
/// <b>Exactly the two documented members.</b> <c>playerId</c>,
/// <c>acquiredAt</c>, <c>definitionId</c>, and the definition's
/// <c>Trigger</c>/<c>Condition</c>/<c>EffectDefinition</c> are §5.4's
/// not-exposed values.
///
/// <b>No equip/loadout member</b> (§5.6): which Relics are equipped is
/// battle-scoped and unpersisted (<c>DATABASE.md</c> §2, ADR-011).
/// </summary>
/// <param name="RelicId"><c>Relic.RelicInstanceId</c> (§5.4).</param>
/// <param name="Name"><c>RelicDefinition.Name</c> (§5.4).</param>
public sealed record RelicResponse(
    [property: JsonPropertyName("relicId")] string RelicId,
    [property: JsonPropertyName("name")] string Name)
{
    /// <summary>Projects one Application read model onto the §5.4 wire shape.</summary>
    /// <param name="item">The projected owned Relic instance.</param>
    public static RelicResponse From(RelicCollectionItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new RelicResponse(item.RelicId, item.Name);
    }
}
