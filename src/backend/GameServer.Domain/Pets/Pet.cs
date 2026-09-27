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
/// ├── XP                (0–4900 — this instance's own Pet XP;
/// │                      PET_RULES.md §5.5)
/// ├── Level             (stored 1–50; derived from this instance's own XP)
/// └── AcquiredAt
/// </code>
///
/// <b>Ownership, not combat.</b> The <c>PlayerId</c> FK is collection
/// ownership only (<c>DATABASE.md</c> §3, ADR-011 item 5, ADR-012): the
/// Player owns the instance; battle-time combat statistics live on
/// <c>PetState</c> (<c>GAME_STATE.md</c> §2.3), never on this row. This
/// type carries no HP/ATK/DEF/Crit/Power column.
///
/// <b>XP and Level are this instance's own progression values.</b>
/// <see cref="XP"/> is the Pet instance's own XP (<c>PET_RULES.md</c> §5.1
/// item 1) and <see cref="Level"/> is its documented function
/// (<c>PET_RULES.md</c> §5.4), within the documented 1–50 range. The retired
/// <c>Player.Level × PetLevelMultiplier</c> derivation has been removed
/// (<c>PET_RULES.md</c> §5.6 item 1, ADR-016 item 13), so nothing on this
/// path derives Level from the owner's account Level: Pet Level is
/// independent of Player Level (<c>PET_RULES.md</c> §5.1 item 3) and is
/// derived from this Pet's own XP instead (<see cref="GrantBattleXp"/>).
///
/// <b>Tier, Star, and Level are independent axes.</b> Tier and Star are not
/// derived from Player Level (<c>PET_RULES.md</c> §5.7 item 4, ADR-012 item
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
    /// The lowest legal <see cref="Level"/> (<c>DATABASE.md</c> §3,
    /// <c>PET_RULES.md</c> §5 — the <c>1–50</c> range).
    /// </summary>
    public const int MinLevel = 1;

    /// <summary>
    /// The highest legal <see cref="Level"/> (<c>DATABASE.md</c> §3,
    /// <c>PET_RULES.md</c> §5.5 item 4 — the Pet Level cap).
    /// </summary>
    public const int MaxLevel = 50;

    /// <summary>
    /// The documented initial <see cref="Level"/> of a newly created Pet
    /// (<c>PET_RULES.md</c> §5.2, <c>DATABASE.md</c> §3).
    ///
    /// It is the initial value only, and it is exactly
    /// <see cref="LevelForXp"/> at <see cref="InitialXp"/>, so the creation
    /// value and the formula cannot disagree (<c>PET_RULES.md</c> §5.2: the
    /// two initial values are "consistent by construction"). It is not
    /// derived from <see cref="MinLevel"/> — a range states the legal values
    /// a value may hold, not the value it holds at creation.
    /// </summary>
    public const int InitialLevel = 1;

    /// <summary>
    /// The documented initial <see cref="XP"/> of a newly created Pet
    /// (<c>PET_RULES.md</c> §5.2, <c>DATABASE.md</c> §3 — <c>Pet.XP = 0</c>
    /// for a newly created PlayerPet).
    /// </summary>
    public const int InitialXp = 0;

    /// <summary>
    /// The Pet XP progression curve constant — the divisor in the Pet Level
    /// formula (<c>PET_RULES.md</c> §5.4, "Reward Amount vs. Curve
    /// Constant").
    ///
    /// <b>It is a gameplay formula constant, not the reward amount.</b>
    /// <c>PET_RULES.md</c> §5.4 keeps the two as independent concepts that
    /// merely happen to be equal today: this one defines the shape of
    /// progression, while <see cref="BattleWonXpReward"/> is a configuration
    /// value. Neither may be derived from the other, and neither is derived
    /// from the Player track's corresponding value
    /// (<c>PET_RULES.md</c> §5.4 "Relationship to the Player Curve" item 3).
    /// </summary>
    public const int XpPerLevelCurveConstant = 100;

    /// <summary>
    /// The Pet XP a won battle grants to the active combat Pet
    /// (<c>PET_RULES.md</c> §5.3 item 1 — <c>BattleWon</c> → active combat
    /// Pet <c>+100</c> Pet XP).
    ///
    /// <b>It is a reward configuration value, not the curve constant.</b>
    /// <c>PET_RULES.md</c> §5.4 states the two are independent and must never
    /// be collapsed into one value even while they are numerically equal. It
    /// is a single server-side amount: no client input influences it
    /// (<c>GAME_RULES.md</c> §18, <c>ADR-001</c>, <c>AGENTS.md</c> §10).
    /// </summary>
    public const int BattleWonXpReward = 100;

    /// <summary>
    /// The Pet XP a lost battle grants to the active combat Pet
    /// (<c>PET_RULES.md</c> §5.3 item 2 — <c>BattleLost</c> → active combat
    /// Pet <c>+0</c> Pet XP).
    ///
    /// It is an explicit Pet decision, not an inheritance of the Player
    /// track's <c>+0</c> (<c>PET_RULES.md</c> §5.3 item 2): defeat changes
    /// nothing, so neither the Pet XP nor the Pet Level moves.
    /// </summary>
    public const int BattleLostXpReward = 0;

    /// <summary>
    /// The documented hard maximum of <see cref="XP"/>
    /// (<c>PET_RULES.md</c> §5.5 item 1, <c>DATABASE.md</c> §3 —
    /// <c>Pet.XP ∈ [0, 4900]</c>).
    ///
    /// <b>It is a hard cap, not a soft ceiling.</b> Once a Pet reaches
    /// Level 50 its stored XP is <c>4900</c> and further Pet XP rewards are
    /// "neither awarded nor stored" (<c>PET_RULES.md</c> §5.5 item 2): there
    /// is no overflow, no hidden XP, no prestige XP, and no post-Level-50
    /// accumulation (item 3). It is deliberately <b>not</b> the Player
    /// track's policy — <c>Player.XP</c> is uncapped and keeps accumulating
    /// after Level 50 (<c>COMBAT_RULES.md</c> §7.5 item 1); neither track's
    /// cap rule may be applied to the other (<c>PET_RULES.md</c> §5.5
    /// "Deliberate Divergence From the Player Track").
    /// </summary>
    public const int MaxXp = 4900;

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
    /// PetDefinition). Element and Passive configuration are read through
    /// this reference, not duplicated on the instance.
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
    /// This instance's own Pet XP — the persistent per-instance progression
    /// pool that <see cref="Level"/> is derived from (<c>DATABASE.md</c> §1,
    /// §3; <c>PET_RULES.md</c> §5.1 item 1, §5.4).
    ///
    /// <b>It is this Pet instance's own pool.</b> <c>PET_RULES.md</c> §5.1
    /// item 1 places it on the owned Pet instance — not on
    /// <c>PetDefinition</c> and not on the Player (item 2). It persists
    /// permanently with the instance (item 6) and is not reset by battle
    /// outcome or by changing the active Pet.
    ///
    /// <b>It is hard-capped at <see cref="MaxXp"/>.</b> <c>PET_RULES.md</c>
    /// §5.5 makes the cap a hard maximum with no overflow, no hidden XP, no
    /// prestige XP, and no post-Level-50 accumulation: rewards at the cap are
    /// neither awarded nor stored. <see cref="GrantBattleXp"/> enforces that
    /// on write, so a stored value can never exceed <see cref="MaxXp"/>.
    ///
    /// <b>It is independent of Player XP.</b> <c>PET_RULES.md</c> §5.1 item
    /// 5 / <c>ADR-016</c> item 12 make the two tracks separate pools that
    /// neither read nor modify each other: <c>Player.XP</c> is never an input
    /// here, and the Player track's uncapped policy
    /// (<c>COMBAT_RULES.md</c> §7.5 item 1) applies nowhere on this member.
    ///
    /// A newly created Pet carries <see cref="InitialXp"/>
    /// (<c>PET_RULES.md</c> §5.2).
    /// </summary>
    public int XP { get; set; } = InitialXp;

    /// <summary>
    /// The stored Pet Level — this owned Pet's own per-instance progression
    /// value, always within the documented 1–50 range (<c>DATABASE.md</c> §1,
    /// §3; <c>PET_RULES.md</c> §5.5).
    ///
    /// <b>It is the documented function of this Pet's own <see cref="XP"/>.</b>
    /// <c>PET_RULES.md</c> §5.4 defines
    /// <c>Pet.Level = min(floor(Pet.XP / 100) + 1, 50)</c>, which
    /// <see cref="LevelForXp"/> implements. It stays a persisted column
    /// (<c>DATABASE.md</c> §1/§3) rather than a computed member: the persisted
    /// value remains the documented Pet Level field, and there is no second
    /// Level property and no hidden stored level.
    ///
    /// Pet Level is independent of Player Level (<c>PET_RULES.md</c> §5.1
    /// item 3) and is not derived from it: the retired
    /// <c>Player.Level × PetLevelMultiplier</c> derivation was removed
    /// (<c>PET_RULES.md</c> §5.6 item 1, ADR-016 item 13).
    ///
    /// It is settable because the stored value must be maintained when
    /// <see cref="XP"/> changes (<c>PET_RULES.md</c> §5.4). Widening the
    /// setter is the mechanical consequence of that contract, not a new rule;
    /// callers apply <see cref="LevelForXp"/> rather than choosing a value.
    ///
    /// A newly created Pet carries <see cref="InitialLevel"/>, which is
    /// <see cref="LevelForXp"/> at <see cref="InitialXp"/>
    /// (<c>PET_RULES.md</c> §5.2).
    /// </summary>
    public int Level { get; set; } = InitialLevel;

    /// <summary>
    /// When the Player acquired this instance (<c>DATABASE.md</c> §1:
    /// <c>AcquiredAt</c>) — a creation timestamp, set once.
    /// </summary>
    public DateTimeOffset AcquiredAt { get; init; }

    /// <summary>
    /// The Pet Level that <paramref name="xp"/> determines — the documented
    /// formula <c>PET_RULES.md</c> §5.4 owns:
    ///
    /// <code>
    /// Pet.Level = min(floor(Pet.XP / 100), 49) + 1
    ///           = min(floor(Pet.XP / 100) + 1, 50)
    /// </code>
    ///
    /// <b>Deterministic and total.</b> <c>§5.4</c> states the formula is
    /// deterministic and total over the valid Pet XP domain <c>[0, 4900]</c>
    /// (<c>§5.5</c>), so this is a pure function of the XP value with no
    /// state of its own. Only the Level is capped, by the <c>min(…, 50)</c>
    /// term (<c>§5.5</c> item 4); the XP cap is a separate fact enforced
    /// where XP is written.
    ///
    /// The worked boundaries of <c>§5.4</c> are reproduced exactly:
    /// <c>0 → 1</c>, <c>100 → 2</c>, <c>4900 → 50</c>.
    ///
    /// <b>Why it is a pure function and not a computed property.</b>
    /// <c>DATABASE.md</c> §1/§3 make <see cref="Level"/> a persisted column,
    /// so it must be assigned when XP changes rather than shadowed by a
    /// derived member — a derived member would be a second source of truth
    /// for one field, which the persistence model forbids. This function is
    /// that assignment's only source of the value.
    ///
    /// The formula shares its <i>shape</i> with the Player curve but shares
    /// no variable, no pool, and no stored value with it
    /// (<c>PET_RULES.md</c> §5.4 "Relationship to the Player Curve",
    /// <c>ADR-016</c> item 12). It is deliberately not generalized into a
    /// shared progression abstraction, and it reads no Player member: it
    /// currently has exactly one implementation (<c>AGENTS.md</c> §9,
    /// <c>ARCHITECTURE.md</c> §5).
    /// </summary>
    /// <param name="xp">
    /// This Pet instance's own XP (<c>PET_RULES.md</c> §5.4). The documented
    /// domain is the integers in <c>[0, 4900]</c>; a negative value is not a
    /// Pet XP the contract defines, so it is refused rather than mapped to a
    /// Level.
    /// </param>
    /// <returns>
    /// The Pet Level <paramref name="xp"/> determines, in
    /// <c>[<see cref="MinLevel"/>, <see cref="MaxLevel"/>]</c>.
    /// </returns>
    public static int LevelForXp(int xp)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(xp);

        // §5.4: min(floor(XP / 100) + 1, 50). The divisor is the curve
        // constant, read from the one constant that owns it so the formula has
        // a single spelling.
        //
        // Integer division of a non-negative value IS floor division, so the
        // guard above is what makes this the documented floor rather than a
        // truncation toward zero.
        return Math.Min((xp / XpPerLevelCurveConstant) + 1, MaxLevel);
    }

    /// <summary>
    /// Applies a battle's documented Pet XP grant to the active combat Pet and
    /// maintains <see cref="Level"/> accordingly — the reward step of
    /// <c>PET_RULES.md</c> §5.3/§5.4 on the battle-end path.
    ///
    /// <code>
    /// BattleWon   →  Pet.XP += 100   (Pet.BattleWonXpReward)
    /// BattleLost  →  Pet.XP += 0     (Pet.BattleLostXpReward)
    ///                   ↓
    /// Pet.XP    = min(Pet.XP + xpGained, 4900)     (§5.5 — cap on write)
    /// Pet.Level = min(floor(Pet.XP / 100) + 1, 50) (§5.4)
    /// </code>
    ///
    /// <b>The caller supplies an outcome-derived amount, never a choice.</b>
    /// <c>§5.3</c> fixes the two outcomes' amounts
    /// (<see cref="BattleWonXpReward"/>, <see cref="BattleLostXpReward"/>),
    /// and this method only adds what it is given and re-derives the Level: it
    /// evaluates no outcome itself and invents no third reward case
    /// (<c>§5.3</c> item 5).
    ///
    /// <b>The hard cap is enforced on write, not deferred.</b> <c>§5.5</c>
    /// makes <c>4900</c> a hard maximum: a grant crossing the boundary leaves
    /// exactly <c>4900</c>, and a grant at <c>4900</c> is neither awarded nor
    /// stored — no overflow is retained "to clamp later", and there is no
    /// hidden, prestige, or post-Level-50 accumulation. This is the only
    /// place <see cref="XP"/> is written on the progression path, which is
    /// what keeps a stored value inside the documented <c>[0, 4900]</c>
    /// domain that <c>DATABASE.md</c> §3 constrains.
    ///
    /// <b>It touches no Player member.</b> <c>PET_RULES.md</c> §5.1 item 5 and
    /// <c>ADR-016</c> item 12 make the two tracks independent pools: the
    /// Player's XP and Level are neither read nor written here.
    /// </summary>
    /// <param name="xpGained">
    /// The documented amount for the battle's outcome. A negative amount is
    /// not a documented grant and is refused rather than applied.
    /// </param>
    public void GrantBattleXp(int xpGained)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(xpGained);

        // §5.5 items 1-3: the hard cap is applied to the stored value, so
        // nothing past 4900 is ever retained. The cap is a hard maximum rather
        // than a pre-check: an already-capped Pet is granted nothing and stores
        // nothing, and a crossing grant lands exactly on the cap.
        XP = Math.Min(XP + xpGained, MaxXp);

        // §5.4: Level follows the XP. It is persisted, not derived on read
        // (DATABASE.md §1/§3), so it is recomputed here whenever XP changes.
        Level = LevelForXp(XP);
    }
}
