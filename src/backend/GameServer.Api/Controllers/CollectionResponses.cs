using System.Text.Json.Serialization;
using GameServer.Application.Collection;
using GameServer.Domain.Cards;
using GameServer.Domain.Relics;

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
///   "level":    12,
///   "signatureSkill": {
///     "cardId":   "card-inferno",
///     "name":     "Inferno",
///     "category": "PetSkill"
///   }
/// }
/// </code>
///
/// <b>Exactly the seven documented members.</b> §5.1 states the member list is
/// binding and exhaustive, so this type carries those seven and nothing else: the
/// persisted <c>xp</c>, <c>acquiredAt</c>, <c>playerId</c>, and
/// <c>petDefinitionId</c> are §5.1's explicitly-not-exposed values, and §5.6's
/// equip/loadout members are excluded from every §5 response. A dedicated
/// response record — rather than the Domain entity, or the Application read
/// model — is what makes the member set structural: a forbidden field cannot
/// reach the wire by accident.
///
/// <b><c>signatureSkill</c> is the Pet's derived Signature Skill reference.</b>
/// §5.1 makes it the member that identifies which Card a Pet's Signature Skill
/// is — <c>PetDefinition.SignatureSkillCardId</c> and the definition it points
/// at — so a client never has to read a <c>Category</c> out of the unlocked Card
/// collection, which can never contain a <c>PetSkill</c> row
/// (<c>SIGNALR_PROTOCOL.md</c> §4.3 item 13, <c>CARD_RULES.md</c> §1 item 4,
/// ADR-012 item 9). It states no ownership: nothing here is a
/// <c>PlayerUnlockedCard</c> row and <c>§5.3</c>'s membership is untouched. It is
/// carried by <see cref="PetSignatureSkillResponse"/>, which owns the nested
/// member set.
///
/// <b>Casing is fixed here.</b> §5.1 spells each member in camelCase, so every
/// member is named with <see cref="JsonPropertyNameAttribute"/> rather than left
/// to a host naming policy — the same technique the SignalR wire projection
/// uses.
///
/// <b>It computes nothing.</b> The projection from the Application read model is
/// a pure field mapping: <c>element</c> already arrives as its documented wire
/// value (<see cref="ElementWireValues"/>), <c>tier</c> as its documented name,
/// Star/Level as the stored integers, and <c>signatureSkill</c> as the stored
/// reference and its definition's own name and category. Nothing is re-derived,
/// defaulted, or clamped at the transport boundary.
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
/// <param name="SignatureSkill">
/// The Pet's derived Signature Skill reference (§5.1) —
/// <c>PetDefinition.SignatureSkillCardId</c> and the <c>CardDefinition</c> it
/// names. Always present; it is a required FK and every Pet has exactly one
/// Signature Skill.
/// </param>
public sealed record PetResponse(
    [property: JsonPropertyName("petId")] string PetId,
    [property: JsonPropertyName("identity")] string Identity,
    [property: JsonPropertyName("element")] string Element,
    [property: JsonPropertyName("tier")] string Tier,
    [property: JsonPropertyName("star")] int Star,
    [property: JsonPropertyName("level")] int Level,
    [property: JsonPropertyName("signatureSkill")]
    PetSignatureSkillResponse SignatureSkill)
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
            item.Level,
            PetSignatureSkillResponse.From(item.SignatureSkill));
    }
}

/// <summary>
/// One Pet's <c>signatureSkill</c> object (<c>API_CONTRACTS.md</c> §5.1).
///
/// <code>
/// {
///   "cardId":   "card-inferno",
///   "name":     "Inferno",
///   "category": "PetSkill"
/// }
/// </code>
///
/// <b>Exactly the three documented members</b>, and no member §5.1 does not bind:
/// there is deliberately no cost, affordability, or legality member and no
/// <c>effectDefinition</c> copy — the composed cast value belongs to the realtime
/// projection (<c>SIGNALR_PROTOCOL.md</c> §4 item 15) and what the Skill changes
/// is <c>§5.3</c>'s content question.
///
/// <b>It is the Pet's derived reference, not a Card the Player owns.</b> §5.1
/// reports <c>PetDefinition.SignatureSkillCardId</c> here precisely so the
/// Signature Skill is identified from the Pet that derives it
/// (<c>CARD_RULES.md</c> §4 item 1) rather than from the unlocked Card
/// collection, which never contains a <c>PetSkill</c> row
/// (<c>CARD_RULES.md</c> §1 item 4, ADR-012 item 9). Nothing here widens
/// <c>§5.3</c>.
/// </summary>
/// <param name="CardId">
/// <c>PetDefinition.SignatureSkillCardId</c> (<c>API_CONTRACTS.md</c> §5.1) — the
/// definition the Pet's Signature Skill is expressed as, and the value the battle
/// loadout derives its 4th equipped entry from (§3).
/// </param>
/// <param name="Name">That <c>CardDefinition</c>'s <c>Name</c> (§5.1).</param>
/// <param name="Category">
/// That <c>CardDefinition</c>'s <c>Category</c> as its documented wire name —
/// <c>"PetSkill"</c> for a Pet's Signature Skill (§5.1, <c>CARD_RULES.md</c>
/// §4 item 1).
/// </param>
public sealed record PetSignatureSkillResponse(
    [property: JsonPropertyName("cardId")] string CardId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("category")] string Category)
{
    /// <summary>Projects one Application read model onto the §5.1 wire shape.</summary>
    /// <param name="item">The projected derived Signature Skill reference.</param>
    public static PetSignatureSkillResponse From(PetSignatureSkillItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new PetSignatureSkillResponse(item.CardId, item.Name, item.Category);
    }
}

/// <summary>
/// One <c>GET /api/cards</c> array element (<c>API_CONTRACTS.md</c> §5.3).
///
/// <code>
/// {
///   "cardId":   "string",
///   "name":     "string",
///   "category": "Basic",
///   "effectDefinition": [
///     { "effectType": "Heal", "valueType": "PercentMaxHp", "value": 20 }
///   ]
/// }
/// </code>
///
/// <b>Exactly the four documented members.</b> <c>playerId</c>,
/// <c>powerCost</c>, and <c>loadoutCopyLimit</c> are §5.3's not-exposed values,
/// and there is deliberately no <c>unlocked</c> member: §5.3 makes presence in
/// the array the unlocked state. There is likewise no cost, affordability, or
/// legality member — that question belongs to the realtime projection and is
/// answered nowhere here (<c>SIGNALR_PROTOCOL.md</c> §4 item 15).
///
/// <b>No equip/loadout member</b> (§5.6).
///
/// <b>It composes nothing.</b> <c>effectDefinition</c> is the stored definition
/// carried through unchanged, element for element
/// (<see cref="CardEffectResponse"/>).
/// </summary>
/// <param name="CardId"><c>CardDefinition.CardDefinitionId</c> (§5.3).</param>
/// <param name="Name"><c>CardDefinition.Name</c> (§5.3).</param>
/// <param name="Category"><c>"Basic"</c> or <c>"PetSkill"</c> (§5.3).</param>
/// <param name="EffectDefinition">
/// <c>CardDefinition.EffectDefinition</c> (§5.3) — the Card's own structured
/// effect rule, one element per effect, in stored order.
/// </param>
public sealed record CardResponse(
    [property: JsonPropertyName("cardId")] string CardId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("effectDefinition")]
    IReadOnlyList<CardEffectResponse> EffectDefinition)
{
    /// <summary>Projects one Application read model onto the §5.3 wire shape.</summary>
    /// <param name="item">The projected unlocked Card.</param>
    public static CardResponse From(CardCollectionItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var effects = new CardEffectResponse[item.EffectDefinition.Count];

        for (var index = 0; index < effects.Length; index++)
        {
            effects[index] = CardEffectResponse.From(item.EffectDefinition[index]);
        }

        return new CardResponse(item.CardId, item.Name, item.Category, effects);
    }
}

/// <summary>
/// One element of a Card's <c>effectDefinition</c> array (<c>API_CONTRACTS.md</c>
/// §5.3).
///
/// <b>The member names are the definition's own storage names</b>
/// (<c>DATABASE.md</c> §1 item 2) — <c>effectType</c>, <c>valueType</c>,
/// <c>value</c>, <c>duration</c>, <c>scope</c> — so §5.3 adds no vocabulary of
/// its own. Each is transcribed from the stored element; the type members are
/// written as their member <b>names</b>, never as ordinals (<c>DATABASE.md</c>
/// §1 item 2's self-describing-token rule).
///
/// <b>The optional members are absent, not null.</b> <c>DATABASE.md</c> §3
/// states the present-iff conditions: <c>value</c> is present iff the
/// <c>valueType</c> interprets one, <c>duration</c> iff
/// <c>effectType</c> is <c>Burn</c>, and <c>scope</c> iff it is <c>Crit</c>.
/// The stored payload omits what the contract does not define, so the response
/// omits it too rather than emitting a <c>null</c> that would be a second
/// representation of the same absence (<see cref="JsonIgnoreCondition"/>).
/// </summary>
/// <param name="EffectType">The stored <c>effectType</c> token (<c>DATABASE.md</c> §1 item 1).</param>
/// <param name="ValueType">The stored <c>valueType</c> token (<c>DATABASE.md</c> §3).</param>
/// <param name="Value">
/// The stored <c>value</c>, present iff the <c>valueType</c> interprets one
/// (<c>DATABASE.md</c> §3). Never <c>0</c>.
/// </param>
/// <param name="Duration">
/// The stored <c>duration</c> in Turns, present iff <c>effectType</c> is
/// <c>Burn</c> (<c>DATABASE.md</c> §3).
/// </param>
/// <param name="Scope">
/// The stored <c>scope</c>, present iff <c>effectType</c> is <c>Crit</c>
/// (<c>DATABASE.md</c> §3).
/// </param>
public sealed record CardEffectResponse(
    [property: JsonPropertyName("effectType")] string EffectType,
    [property: JsonPropertyName("valueType")] string ValueType,
    [property: JsonPropertyName("value")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? Value,
    [property: JsonPropertyName("duration")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? Duration,
    [property: JsonPropertyName("scope")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Scope)
{
    /// <summary>Projects one stored Card effect element onto the §5.3 wire shape.</summary>
    /// <param name="effect">The stored element, already validated by the Domain reader.</param>
    public static CardEffectResponse From(CardEffectDefinition effect)
    {
        return new CardEffectResponse(
            effect.EffectType.ToString(),
            effect.ValueType.ToString(),
            effect.Value,
            effect.Duration,
            effect.Scope);
    }
}

/// <summary>
/// One <c>GET /api/relics</c> array element (<c>API_CONTRACTS.md</c> §5.4).
///
/// <code>
/// {
///   "relicId": "string",
///   "name":    "string",
///   "trigger": "OnMatchCount",
///   "condition": { "conditionType": "MatchCountAtLeast", "threshold": 3 },
///   "effectDefinition": [
///     { "effectType": "ATK", "valueType": "Percentage", "value": 5,
///       "target": "Pet", "lifetime": "Battle" }
///   ]
/// }
/// </code>
///
/// <b>Exactly the five documented members.</b> <c>playerId</c>,
/// <c>acquiredAt</c>, and <c>definitionId</c> are §5.4's not-exposed values.
/// There is deliberately no cost, equip, or acquisition member.
///
/// <b>No equip/loadout member</b> (§5.6): which Relics are equipped is
/// battle-scoped and unpersisted (<c>DATABASE.md</c> §2, ADR-011).
///
/// <b>It composes nothing.</b> The three content members are the definition's
/// own values carried through the projection unchanged — never derived from the
/// name, an id, a category, or a client-side heuristic
/// (<c>RELIC_RULES.md</c> §8.2 item 1).
/// </summary>
/// <param name="RelicId"><c>Relic.RelicInstanceId</c> (§5.4).</param>
/// <param name="Name"><c>RelicDefinition.Name</c> (§5.4).</param>
/// <param name="Trigger"><c>RelicDefinition.Trigger</c> (§5.4) — always present.</param>
/// <param name="Condition">
/// <c>RelicDefinition.Condition</c> (§5.4) — <c>null</c> when the Relic
/// declares no extra condition (<c>RELIC_RULES.md</c> §8.1 item 4). The member
/// is always written, so its presence is fixed and exactly assertable and a
/// <c>null</c> cannot be confused with an absent member.
/// </param>
/// <param name="EffectDefinition">
/// <c>RelicDefinition.EffectDefinition</c> (§5.4) — the Relic's own structured
/// effect rule, one element per effect, in stored order.
/// </param>
public sealed record RelicResponse(
    [property: JsonPropertyName("relicId")] string RelicId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("trigger")] string Trigger,
    [property: JsonPropertyName("condition")] RelicConditionResponse? Condition,
    [property: JsonPropertyName("effectDefinition")]
    IReadOnlyList<RelicEffectResponse> EffectDefinition)
{
    /// <summary>Projects one Application read model onto the §5.4 wire shape.</summary>
    /// <param name="item">The projected owned Relic instance.</param>
    public static RelicResponse From(RelicCollectionItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var effects = new RelicEffectResponse[item.EffectDefinition.Count];

        for (var index = 0; index < effects.Length; index++)
        {
            effects[index] = RelicEffectResponse.From(item.EffectDefinition[index]);
        }

        return new RelicResponse(
            item.RelicId,
            item.Name,
            item.Trigger,

            // A definition with no extra condition carries no value to project,
            // and `null` is the contract's own spelling of that
            // (RELIC_RULES.md §8.1 item 4) — never a sentinel condition object.
            item.Condition is { } condition ? RelicConditionResponse.From(condition) : null,

            effects);
    }
}

/// <summary>
/// A Relic's <c>condition</c> object (<c>API_CONTRACTS.md</c> §5.4).
///
/// <b>The member names are the definition's own storage names</b>
/// (<c>DATABASE.md</c> §1) — <c>conditionType</c> and <c>threshold</c> — so
/// §5.4 adds no vocabulary of its own. The condition form is written as its
/// member <b>name</b>, never as an ordinal.
/// </summary>
/// <param name="ConditionType">The stored <c>conditionType</c> token (<c>RELIC_RULES.md</c> §8.1).</param>
/// <param name="Threshold">The stored <c>threshold</c>, positive (<c>RELIC_RULES.md</c> §8.1 item 1).</param>
public sealed record RelicConditionResponse(
    [property: JsonPropertyName("conditionType")] string ConditionType,
    [property: JsonPropertyName("threshold")] int Threshold)
{
    /// <summary>Projects one stored Relic condition onto the §5.4 wire shape.</summary>
    /// <param name="condition">The stored condition, already validated by the Domain reader.</param>
    public static RelicConditionResponse From(RelicCondition condition) =>
        new(condition.ConditionType.ToString(), condition.Threshold);
}

/// <summary>
/// One element of a Relic's <c>effectDefinition</c> array
/// (<c>API_CONTRACTS.md</c> §5.4).
///
/// <b>The member names are the definition's own storage names</b>
/// (<c>DATABASE.md</c> §1) — <c>effectType</c>, <c>valueType</c>, <c>value</c>,
/// <c>target</c>, <c>lifetime</c> — and <c>target</c>/<c>lifetime</c> are
/// <c>RELIC_RULES.md</c> §8.3's. Each is transcribed from the stored element;
/// the type members are written as their member <b>names</b>, never as
/// ordinals.
///
/// <b><c>value</c> is absent, not null, when no magnitude is authored.</b>
/// <c>RELIC_RULES.md</c> §8.2 item 3 states that an <c>Undetermined</c> element
/// "carries no <c>value</c> member at all, never <c>0</c> and never
/// <c>null</c>", so the response omits it rather than emitting a <c>null</c>
/// (<see cref="JsonIgnoreCondition"/>).
/// </summary>
/// <param name="EffectType">The stored <c>effectType</c> token (<c>RELIC_RULES.md</c> §8.2 item 1).</param>
/// <param name="ValueType">The stored <c>valueType</c> token (<c>RELIC_RULES.md</c> §8.2 item 2).</param>
/// <param name="Value">
/// The stored <c>value</c>, present iff the <c>valueType</c> interprets one
/// (<c>RELIC_RULES.md</c> §8.2 item 3). Never <c>0</c>.
/// </param>
/// <param name="Target">The stored <c>target</c> token (<c>RELIC_RULES.md</c> §8.3 item 1).</param>
/// <param name="Lifetime">The stored <c>lifetime</c> token (<c>RELIC_RULES.md</c> §8.3 item 2).</param>
public sealed record RelicEffectResponse(
    [property: JsonPropertyName("effectType")] string EffectType,
    [property: JsonPropertyName("valueType")] string ValueType,
    [property: JsonPropertyName("value")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? Value,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("lifetime")] string Lifetime)
{
    /// <summary>Projects one stored Relic effect element onto the §5.4 wire shape.</summary>
    /// <param name="effect">The stored element, already validated by the Domain reader.</param>
    public static RelicEffectResponse From(RelicEffectDefinition effect)
    {
        return new RelicEffectResponse(
            effect.EffectType.ToString(),
            effect.ValueType.ToString(),
            effect.Value,
            effect.Target.ToString(),
            effect.Lifetime.ToString());
    }
}
