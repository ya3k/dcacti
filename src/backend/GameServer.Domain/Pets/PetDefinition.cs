using GameServer.Domain.Elements;

namespace GameServer.Domain.Pets;

/// <summary>
/// Static Pet content — one row per MVP Pet (<c>DATABASE.md</c> §1,
/// <c>PET_RULES.md</c> §1, §6).
///
/// <code>
/// PetDefinition
/// ├── PetDefinitionId    (PK)
/// ├── Identity           ("Thanh Xà", "Xích Lang", ...)
/// ├── Element            (exactly one of the Five Elements)
/// ├── PetLevelMultiplier (decimal > 0 — configuration, never hard-coded)
/// ├── PassiveId          (the Pet's one Passive identity — PASSIVE_RULES.md §1)
/// └── PassiveThreshold   (the Passive's "every N Matches" threshold)
/// </code>
///
/// <b>Definition and instance are different things.</b> This type is the
/// static content shared by every owned copy; <c>Pet</c> is a Player's
/// owned instance that references this definition
/// (<c>DATABASE.md</c> §2: Pet N ── 1 PetDefinition). Tier, Star, Level,
/// and AcquiredAt live on the instance, not here.
///
/// <b>The multiplier is configuration, not a constant.</b>
/// <c>PetLevelMultiplier</c> is the per-Pet decimal input to
/// <see cref="PetLevelDerivation.Derive"/> (<c>PET_RULES.md</c> §5 item 1,
/// ADR-012 item 3). It is stored, not hard-coded, and concrete MVP values
/// are deferred to balance (<c>PET_RULES.md</c> §5 item 3,
/// <c>MVP_SCOPE.md</c> §1) — this type imposes no default value and invents
/// none.
///
/// <b>PassiveDefinition is an identity plus threshold.</b>
/// <c>DATABASE.md</c> §1 names the field "threshold/effect reference"; the
/// effect itself is resolved from the Passive identity by the Passive
/// system (<c>PASSIVE_RULES.md</c> §1), matching how
/// <c>BossDefinition</c> carries <c>PassiveId</c>/<c>PassiveThreshold</c>
/// without inlining effect content. A Pet has exactly one Passive
/// (<c>PASSIVE_RULES.md</c> §1).
///
/// <b>SignatureSkillCardId is the Pet's one Pet Skill Card.</b>
/// <c>DATABASE.md</c> §1 lists <c>SignatureSkillCardId (FK → CardDefinition)</c>
/// and §2 records <c>PetDefinition 1 ── 1 CardDefinition</c>. TASK-024
/// deliberately left it unmapped because <c>CardDefinition</c> did not exist
/// yet; TASK-028 completes the deferral now that it does.
///
/// It is the <b>source of the battle's derived fourth Card</b>: the active
/// Pet's Signature Skill Card is read through this reference and added to
/// <c>PetState.EquippedCards[]</c> beside the 3 submitted Basic Cards
/// (<c>CARD_RULES.md</c> §1, §4; <c>API_CONTRACTS.md</c> §3). It is never
/// submitted in <c>cardLoadout</c> and is never counted against the submitted
/// Basics' copy limits (§1 item 4).
/// </summary>
public class PetDefinition
{
    /// <summary>
    /// The definition's identifier (<c>DATABASE.md</c> §1:
    /// <c>PetDefinitionId</c> (PK)) — the FK every owned <c>Pet</c> instance
    /// references (<c>DATABASE.md</c> §2).
    /// </summary>
    public required string PetDefinitionId { get; init; }

    /// <summary>
    /// The Pet's display identity (<c>DATABASE.md</c> §1:
    /// <c>Identity</c>; <c>PET_RULES.md</c> §1, §6) — e.g. "Thanh Xà",
    /// "Xích Lang". It is the stable name of the Pet species, not an owned
    /// instance id.
    /// </summary>
    public required string Identity { get; init; }

    /// <summary>
    /// The Pet's one Element (<c>PET_RULES.md</c> §1, §3 item 3;
    /// <c>ELEMENT_RULES.md</c> §1.2; <c>DATABASE.md</c> §1). Tier does not
    /// change it, and Level does not change it (<c>PET_RULES.md</c> §5
    /// item 7).
    /// </summary>
    public Element Element { get; init; }

    /// <summary>
    /// The per-Pet Level Multiplier — decimal configuration, <c>&gt; 0</c>
    /// (<c>PET_RULES.md</c> §5 item 1, <c>DATABASE.md</c> §1, §3,
    /// ADR-012 item 3).
    ///
    /// It is the multiplier half of <see cref="PetLevelDerivation.Derive"/>'s
    /// inputs and is stored on this definition so gameplay logic never
    /// hard-codes it. Values below 1 are legal; zero and negative values are
    /// not. Concrete MVP numbers are balance/config and are not defined
    /// here (<c>PET_RULES.md</c> §5 item 3).
    /// </summary>
    public decimal PetLevelMultiplier { get; init; }

    /// <summary>
    /// The identity of this Pet's one Passive (<c>DATABASE.md</c> §1
    /// "PassiveDefinition … reference"; <c>PASSIVE_RULES.md</c> §1;
    /// <c>GAME_STATE.md</c> §2.3) — an identity reference, not inlined
    /// effect content. Effect resolution is the Passive system's concern.
    /// </summary>
    public required Passives.PassiveId PassiveId { get; init; }

    /// <summary>
    /// The Passive's Threshold — the "every N Matches" value of the
    /// definition (<c>PASSIVE_RULES.md</c> §1, §2;
    /// <c>DATABASE.md</c> §1 "threshold … reference"). Progress charging and
    /// trigger evaluation live in the Passive system, not on this type.
    /// </summary>
    public int PassiveThreshold { get; init; }

    /// <summary>
    /// The identity of this Pet's one Signature Skill Card
    /// (<c>DATABASE.md</c> §1: <c>SignatureSkillCardId</c> (FK →
    /// CardDefinition); §2: <c>PetDefinition 1 ── 1 CardDefinition</c>;
    /// <c>CARD_RULES.md</c> §4 item 1: "Each Pet has exactly one Signature
    /// Skill, expressed as one Pet Skill Card").
    ///
    /// <b>It references the Card's definition; it is not the Card itself.</b>
    /// Name, Category, PowerCost, EffectDefinition, and LoadoutCopyLimit live
    /// on <see cref="Cards.CardDefinition"/> and are read through this
    /// reference, never duplicated here (<c>GAME_STATE.md</c> §0 item 5).
    ///
    /// It is the value the battle-start path follows to <b>derive</b> the
    /// fourth equipped Card: the derived definition is appended to the 3
    /// submitted Basic Cards to form the 4-entry
    /// <c>PetState.EquippedCards[]</c> snapshot (<c>CARD_RULES.md</c> §1;
    /// <c>API_CONTRACTS.md</c> §3). Because the Card is derived and never
    /// submitted, this field is the <b>only</b> source of the Pet Skill slot —
    /// there is no second, client-selected Skill loadout.
    ///
    /// The Card it names must be a <c>PetSkill</c> definition for the loadout
    /// composition to be meaningful (<c>CARD_RULES.md</c> §1 item 4: a
    /// <c>PetSkill</c> Card "keeps its own composition slot"). The reference
    /// is required and non-nullable; concrete MVP Skill Cards are content, and
    /// <c>CARD_RULES.md</c> §4.1 notes that Thanh Xà's and Sơn Hùng's are not
    /// yet authored — their content is not invented here.
    /// </summary>
    public required string SignatureSkillCardId { get; init; }
}
