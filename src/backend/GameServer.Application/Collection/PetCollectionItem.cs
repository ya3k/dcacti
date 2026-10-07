namespace GameServer.Application.Collection;

/// <summary>
/// One owned Pet as the collection read reports it — the projection behind one
/// <c>GET /api/pets</c> element and the whole of
/// <c>GET /api/pets/{petId}</c> (<c>API_CONTRACTS.md</c> §5.1, §5.2).
///
/// <code>
/// petId           string  Pet.PetInstanceId
/// identity        string  PetDefinition.Identity
/// element         string  PetDefinition.Element, via ElementWireValues
/// tier            string  Pet.Tier
/// star            int     Pet.Star
/// level           int     Pet.Level
/// signatureSkill  object  the Pet's DERIVED Signature Skill reference —
///                         PetDefinition.SignatureSkillCardId and the
///                         CardDefinition it points at
/// </code>
///
/// <b>Exactly the seven documented members.</b> §5.1 states the member list "is
/// binding and exhaustive", so this record carries those seven and nothing else:
/// the persisted <c>xp</c>, <c>acquiredAt</c>, <c>playerId</c>, and
/// <c>petDefinitionId</c> are §5.1's explicitly-not-exposed values and are not
/// projected. A record rather than the entity is what makes that structural —
/// a forbidden field cannot reach the wire by accident.
///
/// <b><c>SignatureSkill</c> is not ownership and not an equip member.</b> It is
/// derived from the Pet's own definition (<c>CARD_RULES.md</c> §4), so it grants
/// nothing, is never a <c>PlayerUnlockedCard</c> row, and never appears in
/// <c>GET /api/cards</c> (<c>API_CONTRACTS.md</c> §5.1, §5.3). §5.6 excludes
/// <c>isEquipped</c>, <c>equipped</c>, <c>slot</c>, <c>loadoutPosition</c>, and
/// <c>active</c> from every §5 response: equip state is battle-scoped and
/// unpersisted (<c>DATABASE.md</c> §2, ADR-011) and none of it is read here.
/// </summary>
/// <param name="PetId">
/// <c>Pet.PetInstanceId</c> — the owned instance's identity
/// (<c>DATABASE.md</c> §1), which is also the value <c>POST /api/battle/start</c>
/// submits as <c>petId</c> (§3).
/// </param>
/// <param name="Identity">
/// <c>PetDefinition.Identity</c> — the Pet species' display identity
/// (<c>PET_RULES.md</c> §1), read through the instance's definition reference.
/// </param>
/// <param name="Element">
/// <c>PetDefinition.Element</c> as its documented wire value
/// (<c>API_CONTRACTS.md</c> §5.1), produced by
/// <see cref="ElementWireValues.ToWireValue"/>.
/// </param>
/// <param name="Tier">
/// <c>Pet.Tier</c> (<c>PET_RULES.md</c> §3) — the documented string member, one
/// of the five Tier names.
/// </param>
/// <param name="Star"><c>Pet.Star</c> (<c>PET_RULES.md</c> §4) — an integer.</param>
/// <param name="Level"><c>Pet.Level</c> (<c>PET_RULES.md</c> §5) — an integer.</param>
/// <param name="SignatureSkill">
/// The Pet's derived Signature Skill reference (<c>API_CONTRACTS.md</c> §5.1):
/// <c>PetDefinition.SignatureSkillCardId</c> and the definition it points at.
/// It identifies the Skill the Pet derives; it is never an owned Card and never
/// an equip member.
/// </param>
public sealed record PetCollectionItem(
    string PetId,
    string Identity,
    string Element,
    string Tier,
    int Star,
    int Level,
    PetSignatureSkillItem SignatureSkill);
