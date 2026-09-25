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
/// ├── EffectDefinition
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
/// <b><see cref="EffectDefinition"/> is data at this stage.</b> It is an effect
/// <b>reference</b>, matching how <c>RelicDefinition.EffectDefinition</c> and
/// <c>PetDefinition.PassiveId</c> reference their content rather than inlining
/// it. Card casting, effect resolution, targeting, and Power spend are
/// <c>CARD_RULES.md</c> §3's concern and are <b>not</b> implemented by this
/// type or by TASK-028 (TASK-028 Scope: no Card gameplay).
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
    /// The Card's effect reference (<c>DATABASE.md</c> §1:
    /// <c>EffectDefinition</c>; <c>CARD_RULES.md</c> §1).
    ///
    /// It is carried as a reference, not as resolved effect content, and
    /// nothing executes it in TASK-028 (TASK-028 Scope: no Card gameplay, no
    /// Card effects merely because this field exists).
    /// </summary>
    public required string EffectDefinition { get; init; }

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
