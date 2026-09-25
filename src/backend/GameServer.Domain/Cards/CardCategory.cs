namespace GameServer.Domain.Cards;

/// <summary>
/// The two MVP Card categories (<c>CARD_RULES.md</c> §1,
/// <c>GAME_RULES.md</c> §11, <c>DATABASE.md</c> §3).
///
/// <code>
/// Basic      shared across all Pets
/// PetSkill   tied to the active Pet's Signature Skill (exactly one per Pet)
/// </code>
///
/// <b>The set is closed.</b> §1 defines exactly these two categories, and
/// <c>DATABASE.md</c> §3 constrains <c>CardDefinition.Category</c> to
/// <c>{Basic, PetSkill}</c>. No third category exists in MVP; adding one is a
/// gameplay rule change (<c>GAME_RULES.md</c> §20, <c>AGENTS.md</c> §7) and
/// must not be introduced here.
///
/// <b>The distinction is loadout-bearing.</b> A battle loadout always contains
/// exactly 3 <see cref="Basic"/> Cards plus 1 <see cref="PetSkill"/> Card
/// (<c>CARD_RULES.md</c> §1): the three submitted <c>cardLoadout</c> entries
/// must every one of them be <see cref="Basic"/>, while the
/// <see cref="PetSkill"/> Card is the active Pet's derived Signature Skill and
/// is never submitted. A <see cref="PetSkill"/> definition therefore can never
/// satisfy a submitted Basic slot (<c>API_CONTRACTS.md</c> §3 step 3).
///
/// <b>Member order carries no documented meaning</b> and is a stable reading
/// aid only: §1 presents Basic first because it is the shared category.
/// Numeric values are identifiers, and no rule derives from them.
/// </summary>
public enum CardCategory
{
    /// <summary>
    /// Basic — shared across all Pets, and the only category the submitted
    /// three-card <c>cardLoadout</c> may contain (<c>CARD_RULES.md</c> §1).
    /// MVP ships three: Heal, Shield, and Power Charge (§2).
    ///
    /// It is <c>0</c> so an unset enum value is <b>not</b> a silently valid
    /// category: <c>default(CardCategory)</c> reading as Basic would let
    /// missing definition data pass the category rule. Persistence stores the
    /// column as required and non-nullable, and the loadout's category check
    /// accepts this member only for a definition that actually exists
    /// (<c>API_CONTRACTS.md</c> §3 step 3).
    /// </summary>
    Basic = 0,

    /// <summary>
    /// PetSkill — the active Pet's Signature Skill Card
    /// (<c>CARD_RULES.md</c> §4). Exactly one per Pet, referenced by
    /// <see cref="Pets.PetDefinition.SignatureSkillCardId"/>, derived at
    /// battle start rather than submitted, and never counted against the
    /// submitted Basic loadout or its copy limits (<c>CARD_RULES.md</c> §1
    /// item 4).
    /// </summary>
    PetSkill = 1,
}
