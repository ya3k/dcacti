namespace GameServer.Domain.Pets;

/// <summary>
/// A Player's owned instance of a Pet (<c>DATABASE.md</c> §1, §2,
/// <c>PET_RULES.md</c> §1–§2, ADR-011 item 7).
///
/// <code>
/// Pet
/// ├── PetInstanceId     (PK)
/// ├── PlayerId          (FK → Player — collection ownership)
/// ├── PetDefinitionId   (FK → PetDefinition)
/// ├── Tier              (Common / Rare / Epic / Legendary / Mythic)
/// ├── Star              (1–5)
/// ├── Level             (denormalized snapshot of PetLevelDerivation.Derive)
/// └── AcquiredAt
/// </code>
///
/// <b>Ownership, not combat.</b> The <c>PlayerId</c> FK is collection
/// ownership only (<c>DATABASE.md</c> §3, ADR-011 item 5, ADR-012): the
/// Player owns the instance; battle-time combat statistics live on
/// <c>PetState</c> (<c>GAME_STATE.md</c> §2.3), never on this row. This
/// type carries no HP/ATK/DEF/Crit/Power column.
///
/// <b>Level is a denormalized snapshot, not an XP store.</b>
/// <see cref="Level"/> holds the value <see cref="PetLevelDerivation.Derive"/>
/// produced for the owner's current Player Level and this instance's
/// definition multiplier (<c>DATABASE.md</c> §1, ADR-012 Consequences).
/// It is recomputed when Player Level or the multiplier changes outside
/// battle; it is never incremented by XP, because Pets have no independent
/// XP progression (<c>PET_RULES.md</c> §5 item 4, ADR-012 item 6). The
/// setter exists so the recompute path can write the derived value back —
/// it is not a public level-up API.
///
/// <b>Tier, Star, and Level are independent axes.</b> Tier and Star are not
/// derived from Player Level (<c>PET_RULES.md</c> §5 item 9, ADR-012 item
/// 5); there is no Evolution field and no Tier/Star derivation rule in MVP
/// (ADR-012 items 5–6).
/// </summary>
public class Pet
{
    /// <summary>
    /// The lowest legal <see cref="Star"/> (<c>DATABASE.md</c> §3,
    /// <c>PET_RULES.md</c> §4).
    /// </summary>
    public const int MinStar = 1;

    /// <summary>
    /// The highest legal <see cref="Star"/> (<c>DATABASE.md</c> §3,
    /// <c>PET_RULES.md</c> §4).
    /// </summary>
    public const int MaxStar = 5;

    /// <summary>
    /// The owned instance's identifier (<c>DATABASE.md</c> §1:
    /// <c>PetInstanceId</c> (PK)). Distinct from
    /// <see cref="PetDefinitionId"/>, which identifies the species this
    /// instance is a copy of.
    /// </summary>
    public required string PetInstanceId { get; init; }

    /// <summary>
    /// The owning Player (<c>DATABASE.md</c> §1: <c>PlayerId</c> (FK →
    /// Player), §2: Player 1 ── N Pet). Collection ownership only — not a
    /// combat-stat holder (<c>DATABASE.md</c> §3, ADR-011 item 5).
    /// </summary>
    public required string PlayerId { get; init; }

    /// <summary>
    /// The definition this instance is a copy of (<c>DATABASE.md</c> §1:
    /// <c>PetDefinitionId</c> (FK → PetDefinition), §2: Pet N ── 1
    /// PetDefinition). Element, Passive configuration, and
    /// <c>PetLevelMultiplier</c> are read through this reference, not
    /// duplicated on the instance.
    /// </summary>
    public required string PetDefinitionId { get; init; }

    /// <summary>
    /// This instance's Tier (<c>PET_RULES.md</c> §1, §3;
    /// <c>DATABASE.md</c> §1, §3: Tier ∈ the five documented members). An
    /// independent progression axis — never derived from Player Level
    /// (<c>PET_RULES.md</c> §5 item 9, ADR-012 item 5).
    /// </summary>
    public PetTier Tier { get; init; }

    /// <summary>
    /// This instance's Star, in the documented 1–5 range
    /// (<c>PET_RULES.md</c> §1, §4; <c>DATABASE.md</c> §1, §3). An
    /// independent progression axis — never derived from Player Level
    /// (<c>PET_RULES.md</c> §5 item 9, ADR-012 item 5).
    /// </summary>
    public int Star { get; init; } = MinStar;

    /// <summary>
    /// The stored Pet Level — a denormalized snapshot of
    /// <see cref="PetLevelDerivation.Derive"/>, always within the
    /// documented 1–50 range (<c>DATABASE.md</c> §1, §3;
    /// <c>PET_RULES.md</c> §5, ADR-012 item 4).
    ///
    /// The setter exists solely so the recompute path can write the derived
    /// value when Player Level or the definition multiplier changes
    /// (<c>DATABASE.md</c> §1; ADR-012 Consequences). There is no XP column
    /// and no path that adds to this value: Pets have no independent XP
    /// progression (<c>PET_RULES.md</c> §5 item 4, ADR-012 item 6).
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// When the Player acquired this instance (<c>DATABASE.md</c> §1:
    /// <c>AcquiredAt</c>) — a creation timestamp, set once.
    /// </summary>
    public DateTimeOffset AcquiredAt { get; init; }
}
