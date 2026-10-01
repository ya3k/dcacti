namespace GameServer.Domain.Cards;

/// <summary>
/// Static Card content — one row per MVP Card (<c>DATABASE.md</c> §1,
/// <c>CARD_RULES.md</c> §1–§2, §4).
///
/// <code>
/// CardDefinition
/// ├── CardDefinitionId   (PK)
/// ├── Name
/// ├── Category           (Basic | PetSkill)
/// ├── PowerCost
/// ├── EffectDefinition   (structured ARRAY — CardEffectDefinitions)
/// └── LoadoutCopyLimit   (required — no default)
/// </code>
///
/// <b>Definition and ownership are different things, and there is no
/// instance.</b> This type is the static content; a Player owns a Card by
/// holding an unlock row (<see cref="PlayerUnlockedCard"/>) that references
/// this definition (<c>DATABASE.md</c> §2: PlayerUnlockedCard N ── 1
/// CardDefinition; ADR-012 item 9). MVP Cards have no Tier/Star/Level, so an
/// unlock flag is sufficient and <b>no</b> <c>CardInstance</c>,
/// <c>CardQuantity</c>, <c>CardInventory</c>, or <c>Pet.CardInventory</c>
/// exists or may be introduced (ADR-012 item 9; <c>DATABASE.md</c> §2).
///
/// <b>The battle snapshot carries this identity, not an instance identity.</b>
/// <c>PetState.EquippedCards[]</c> holds <see cref="CardDefinitionId"/> values
/// (<c>GAME_STATE.md</c> §2.3): repeated entries are this same definition
/// selected more than once, never separate owned entities.
///
/// <b><see cref="EffectDefinition"/> is the Card's structured effect rule.</b>
/// It carries which domain effects the Card applies — one element per effect —
/// and each effect's value with its interpretation
/// (<see cref="CardEffectDefinition"/>, held in a
/// <see cref="CardEffectDefinitions"/>), so the runtime can identify and read a
/// Card's effects <b>without</b> parsing prose, inferring from
/// <see cref="Name"/>, mapping from <see cref="CardDefinitionId"/>, or
/// hardcoding card-specific logic — TASK-108 decision D-1 and TASK-111 decision
/// D-1's array shape, recorded in <c>DATABASE.md</c> §1. <b>Always an array</b>,
/// even for a Card with one effect (TASK-111 D-1b), so there is one shape for
/// every Card and no arity-dependent representation. TASK-108's decision
/// supersedes TASK-082 decision R2-7 ("store the owning document's effect rule
/// text verbatim, and introduce no effect-reference identifier system") <b>for
/// <c>CardDefinition</c> only</b>; <c>RelicDefinition.EffectDefinition</c> still
/// carries R2-7's verbatim text.
///
/// <b>Element order carries no gameplay meaning</b> (TASK-111 D-5,
/// <c>DATABASE.md</c> §1 item 3): the sequence is a storage sequence only, and no
/// rule, resolver, or serializer may read an index as a resolution step. The
/// stored order is nevertheless preserved rather than normalized, because the
/// contract fixes no canonical order.
///
/// <b>It is data, and it is not executed.</b> Card casting, effect resolution,
/// targeting, Crit rolling, Burn ticking, and Power spend are
/// <c>CARD_RULES.md</c> §3's concern and are <b>not</b> implemented by this type
/// or by TASK-028/TASK-109/TASK-112 (TASK-112 Scope: no CardCast, no PetSkillCast,
/// no resolver that mutates battle state). Nothing reads, applies, or dispatches
/// on this value here.
/// </summary>
public class CardDefinition
{
    /// <summary>
    /// The definition's identifier (<c>DATABASE.md</c> §1:
    /// <c>CardDefinitionId</c> (PK)) — the identity an unlock row references
    /// (<see cref="PlayerUnlockedCard.CardDefinitionId"/>) and the identity the
    /// battle snapshot carries (<c>GAME_STATE.md</c> §2.3).
    /// </summary>
    public required string CardDefinitionId { get; init; }

    /// <summary>
    /// The Card's display name (<c>DATABASE.md</c> §1: <c>Name</c>) — e.g.
    /// "Heal", "Shield", "Power Charge" (<c>CARD_RULES.md</c> §2).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The Card's category (<c>DATABASE.md</c> §1: <c>Category</c>; §3
    /// constrains it to <c>{Basic, PetSkill}</c>).
    ///
    /// Only <see cref="CardCategory.Basic"/> may satisfy a submitted
    /// <c>cardLoadout</c> entry; the <see cref="CardCategory.PetSkill"/> Card
    /// is the active Pet's derived Signature Skill
    /// (<c>CARD_RULES.md</c> §1, §4; <c>API_CONTRACTS.md</c> §3 step 3).
    /// </summary>
    public CardCategory Category { get; init; }

    /// <summary>
    /// The Power the Card costs to cast (<c>DATABASE.md</c> §1:
    /// <c>PowerCost</c>; <c>CARD_RULES.md</c> §2, §4.1).
    ///
    /// Concrete MVP costs are content/balance values owned by
    /// <c>CARD_RULES.md</c> §2/§4.1 and the future content task — this type
    /// stores the value and defines none. No cost is read or enforced here:
    /// cast validation and Power deduction are <c>CARD_RULES.md</c> §3's
    /// concern and are not implemented.
    /// </summary>
    public int PowerCost { get; init; }

    /// <summary>
    /// The Card's structured effect rules (<c>DATABASE.md</c> §1:
    /// <c>EffectDefinition</c>; <c>CARD_RULES.md</c> §2/§4.1 owns the values).
    ///
    /// It is carried as data — which domain effects apply, and each effect's value
    /// with the value's interpretation (<see cref="CardEffectDefinitions"/>) — and
    /// nothing executes it in TASK-028/TASK-109/TASK-112 (no Card gameplay, no
    /// Card effects merely because this field exists; no Crit roll, no Burn tick,
    /// no CardCast/PetSkillCast). TASK-108 decision D-1 chose this structured form
    /// over TASK-082 R2-7's verbatim prose, and TASK-111 decision D-1 extended it
    /// to an <b>array</b> of effect objects so a Card with several effects is
    /// representable; both are recorded in <c>DATABASE.md</c> §1, and the
    /// supersession applies to <c>CardDefinition</c> only.
    /// </summary>
    public required CardEffectDefinitions EffectDefinition { get; init; }

    /// <summary>
    /// The number of times this definition may appear in <b>one submitted
    /// 3-card Basic loadout</b> (<c>DATABASE.md</c> §1; <c>CARD_RULES.md</c> §1
    /// "Loadout copy limit (per CardDefinition)").
    ///
    /// <code>
    /// occurrence count of this CardDefinitionId in cardLoadout
    ///     ≤  LoadoutCopyLimit
    /// </code>
    ///
    /// <b>It is required and has no default.</b> <c>CARD_RULES.md</c> §1 item 2
    /// states every CardDefinition must define its limit explicitly and that "a
    /// missing value is invalid definition data. There is no default."
    /// <c>DATABASE.md</c> §1 repeats "explicit value required, no default".
    /// A limit of 1 makes the Card loadout-unique; duplicates are permitted up
    /// to the limit (§1 item 1). The limit is therefore deliberately a plain,
    /// required <see cref="int"/> and no fallback of 1, 3, or any other value
    /// is applied anywhere — the loadout validator reads this value and rejects
    /// when it cannot be satisfied (<c>API_CONTRACTS.md</c> §3 step 4).
    ///
    /// <b>The limit is a loadout bound, not inventory.</b> §1 item 3: it "is
    /// not inventory quantity, ownership quantity, or a collection limit".
    /// Ownership remains one unlock row per (Player, CardDefinition) regardless
    /// of allowed copies.
    ///
    /// <b>Concrete values are content/balance configuration.</b> §1 item 5
    /// defers them to a future balance/content task; this type defines none and
    /// invents none.
    /// </summary>
    public int LoadoutCopyLimit { get; init; }
}
