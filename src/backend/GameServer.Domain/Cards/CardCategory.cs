namespace GameServer.Domain.Cards;

/// <summary>
/// The three MVP Card categories (<c>CARD_RULES.md</c> §1, §1.1,
/// <c>GAME_RULES.md</c> §11, <c>DATABASE.md</c> §3).
///
/// <code>
/// Basic      shared across all Pets
/// PetSkill   tied to the active Pet's Signature Skill (exactly one per Pet)
/// BossCard   the Boss's own Card (exactly one per Boss — ADR-029)
/// </code>
///
/// <b>The set is closed at these three.</b> §1 defines exactly these three
/// categories and §1.1 owns the Boss Card category's contract. A fourth
/// category is a gameplay rule change (<c>GAME_RULES.md</c> §20,
/// <c>AGENTS.md</c> §7) and must not be introduced here.
///
/// <b>Two of the three are Player categories and one is not.</b> A battle
/// loadout always contains exactly 3 <see cref="Basic"/> Cards plus 1
/// <see cref="PetSkill"/> Card (<c>CARD_RULES.md</c> §1): the three submitted
/// <c>cardLoadout</c> entries must every one of them be <see cref="Basic"/>,
/// while the <see cref="PetSkill"/> Card is the active Pet's derived Signature
/// Skill and is never submitted. <see cref="BossCard"/> is the Boss's own
/// category and never part of that model: it has no loadout slot, no ownership
/// row, no collection visibility and no Player entitlement (§1.1 items 1, 4, 5;
/// <c>BOSS_RULES.md</c> §4.2 items 1, 8). The acting side is therefore part of
/// the category contract, and <see cref="CardCastActor"/>'s cast contract is
/// what enforces it in both directions (§3 item 9).
///
/// <b>Member order carries no documented meaning</b> and is a stable reading
/// aid only: §1 presents Basic first because it is the shared category. Numeric
/// values are identifiers, and no rule derives from them.
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

    /// <summary>
    /// BossCard — the Boss's own Card, the third category
    /// (<c>CARD_RULES.md</c> §1, §1.1; <c>BOSS_RULES.md</c> §4.2; <c>ADR-029</c>
    /// §2.5 items 12–15).
    ///
    /// <b>Domain-only: it has no persistence contract, no loadout slot, no
    /// collection visibility, and no Player entitlement.</b> §1.1 items 1 and 5
    /// leave the Boss Card's persistence carrier deliberately undefined — no
    /// database column, table, migration or row type is added by this category,
    /// and <c>DATABASE.md</c> §3's stored set for a provisioned
    /// <c>CardDefinition</c> row remains <c>{Basic, PetSkill}</c>. Provisioning a
    /// Boss Card row is a separate future task, so no persisted definition carries
    /// this value yet. There is no Boss loadout, no equip step, no acquisition
    /// path, no <see cref="PlayerUnlockedCard"/> ownership row, and the Player can
    /// neither own, equip, see in a collection, nor cast it (§1.1 item 4;
    /// <c>BOSS_RULES.md</c> §4.2 items 1, 8).
    ///
    /// <b>It is a Card, not a Passive and not a Boss Skill.</b> A Boss Card is an
    /// actively selected Boss action resolved through the shared cast contract
    /// (<c>CARD_RULES.md</c> §3 item 9); encounter mechanics are never modelled as
    /// Boss Cards (<c>BOSS_RULES.md</c> §4.2 item 7, <c>ADR-029</c> §2.6 item 16).
    ///
    /// <b>The acting side is enforced in both directions.</b> Only
    /// <see cref="CardCastActor"/>'s Boss actor may cast a <c>BossCard</c>
    /// definition, and a Boss actor may cast no other category: a Player-actor cast
    /// of this category is rejected <c>INVALID_CARD</c>, as is a Boss-actor cast of
    /// a <see cref="Basic"/> or <see cref="PetSkill"/> definition (§1.1 item 4,
    /// <c>BOSS_RULES.md</c> §4.2 item 8).
    /// </summary>
    BossCard = 2,
}
