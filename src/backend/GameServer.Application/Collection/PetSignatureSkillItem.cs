namespace GameServer.Application.Collection;

/// <summary>
/// One Pet's **derived Signature Skill reference** as the collection read reports
/// it — the nested <c>signatureSkill</c> member of one <c>GET /api/pets</c>
/// element and of <c>GET /api/pets/{petId}</c> (<c>API_CONTRACTS.md</c> §5.1,
/// §5.2).
///
/// <code>
/// cardId    string  PetDefinition.SignatureSkillCardId
/// name      string  that CardDefinition's Name
/// category  string  that CardDefinition's Category ("PetSkill")
/// </code>
///
/// <b>It is derived from the Pet and states no ownership.</b> A Signature Skill
/// is never owned and never acquired: it is derived from the active Pet at battle
/// start and appended to <c>PetState.EquippedCards[]</c>
/// (<c>API_CONTRACTS.md</c> §3, <c>CARD_RULES.md</c> §4). This record reports the
/// reference the Pet's required <c>SignatureSkillCardId</c> FK names and the
/// definition it points at — it is <b>not</b> a member of
/// <c>GET /api/cards</c>, whose membership stays the Player's unlock rows
/// (<c>API_CONTRACTS.md</c> §5.3, <c>CARD_RULES.md</c> §1 item 4, ADR-012
/// item 9).
///
/// <b>It carries the definition's own values, unchanged.</b> <c>name</c> is the
/// stored <c>CardDefinition.Name</c> and <c>category</c> is the stored
/// <c>CardDefinition.Category</c> as its documented wire name — the same
/// two-member spelling <c>API_CONTRACTS.md</c> §5.3 fixes. No cost, no
/// affordability state, no cast-legality judgment, and no
/// <c>effectDefinition</c> element is composed here (<c>CARD_RULES.md</c> §3.6,
/// <c>SIGNALR_PROTOCOL.md</c> §4 item 15).
/// </summary>
/// <param name="CardId">
/// <c>PetDefinition.SignatureSkillCardId</c> (<c>DATABASE.md</c> §1) — the
/// definition the Pet's Signature Skill is expressed as, and the value the battle
/// loadout derives its 4th equipped entry from (<c>CARD_RULES.md</c> §4 item 1).
/// </param>
/// <param name="Name">That definition's <c>Name</c> (<c>API_CONTRACTS.md</c> §5.1).</param>
/// <param name="Category">
/// That definition's <c>Category</c> as its documented wire name
/// (<c>API_CONTRACTS.md</c> §5.3, <c>CARD_RULES.md</c> §1) — <c>"PetSkill"</c>
/// for a Pet's Signature Skill (<c>CARD_RULES.md</c> §4 item 1).
/// </param>
public sealed record PetSignatureSkillItem(
    string CardId,
    string Name,
    string Category);
