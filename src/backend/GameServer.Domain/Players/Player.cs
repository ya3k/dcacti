namespace GameServer.Domain.Players;

/// <summary>
/// The Player — the persistent account/owner of the collection
/// (<c>DATABASE.md</c> §1, <c>MVP_SCOPE.md</c> §1).
///
/// <code>
/// Player
/// ├── PlayerId        (PK)
/// ├── AccountId       (FK → Account, unique; ADR-020)
/// ├── XP              (int — persistent account progression; uncapped;
/// │                    COMBAT_RULES.md §7)
/// ├── Level           (1–50 — the documented function of XP; persistent
/// │                    account attribute, NO combat stats; ADR-016)
/// └── CreatedAt
/// </code>
///
/// <b>This is the documented field set, not a new decision.</b>
/// <c>DATABASE.md</c> §1 defines exactly these five fields, and §3 records
/// their constraints. <see cref="XP"/> is the Player's own progression pool and
/// <see cref="Level"/> is its documented function (<c>COMBAT_RULES.md</c> §7.4);
/// no further field is added — in particular there is no reward field, no
/// second progression entity, and no combat-stat member.
///
/// <b>The Player owns no combat statistics.</b> <c>ADR-011</c> item 5,
/// <c>COMBAT_RULES.md</c> §7.6 and <c>DATABASE.md</c> §3 state that
/// HP/ATK/DEF/Crit/Power are battle-time <c>PetState</c> values
/// (<c>GAME_STATE.md</c> §2.3), never Player columns. <see cref="Level"/> is a
/// persistent progression value that grants no combat modifier of any kind.
///
/// <b>Player progression is separate from Pet progression.</b> <c>ADR-016</c>
/// item 12 makes the two tracks independent: nothing here reads or modifies a
/// Pet's XP or Level, and no Pet attribute is an input to either member.
///
/// <b>Only portable domain data is carried.</b> <c>PlayerId</c> is the
/// Player's identifier, not a persistence concern; the storage mapping
/// (column types, key generation) lives in Infrastructure
/// (<c>ARCHITECTURE.md</c> §2.1 — Domain has no persistence dependency).
/// </summary>
public class Player
{
    /// <summary>
    /// The lowest legal <see cref="Level"/> (<c>DATABASE.md</c> §3,
    /// <c>COMBAT_RULES.md</c> §7 — the <c>1–50</c> range).
    /// </summary>
    public const int MinLevel = 1;

    /// <summary>
    /// The highest legal <see cref="Level"/> (<c>DATABASE.md</c> §3,
    /// <c>COMBAT_RULES.md</c> §7.5 item 2 — Player Level is capped at 50).
    /// </summary>
    public const int MaxLevel = 50;

    /// <summary>
    /// The documented initial <see cref="Level"/> of a newly created Player
    /// (<c>COMBAT_RULES.md</c> §7.5 item 3, <c>DATABASE.md</c> §3).
    ///
    /// It is the initial value only, and it is exactly
    /// <see cref="LevelForXp"/> at <see cref="InitialXp"/>, so the creation
    /// value and the formula cannot disagree. It is not derived from
    /// <see cref="MinLevel"/> — a range states the legal values a value may
    /// hold, not the value it holds at creation.
    /// </summary>
    public const int InitialLevel = 1;

    /// <summary>
    /// The documented initial <see cref="XP"/> of a newly created Player
    /// (<c>COMBAT_RULES.md</c> §7.5 item 3, <c>DATABASE.md</c> §3).
    /// </summary>
    public const int InitialXp = 0;

    /// <summary>
    /// The Player XP progression curve constant — the divisor in the Player
    /// Level formula (<c>COMBAT_RULES.md</c> §7.3, §7.4; <c>ADR-016</c> item
    /// 7).
    ///
    /// <b>It is a gameplay formula constant, not the reward amount.</b>
    /// <c>COMBAT_RULES.md</c> §7.3 keeps the two as independent concepts that
    /// merely happen to be equal today: this one defines the shape of
    /// progression, while <see cref="BattleWonXpReward"/> is a configuration
    /// value. Neither may be derived from the other.
    /// </summary>
    public const int XpPerLevelCurveConstant = 100;

    /// <summary>
    /// The Player XP a won battle grants (<c>COMBAT_RULES.md</c> §7.2;
    /// <c>ADR-016</c> item 4 — <c>BattleWon</c> → Player XP <c>+100</c>).
    ///
    /// <b>It is a reward configuration value, not the curve constant.</b>
    /// <c>COMBAT_RULES.md</c> §7.3 states the two are independent and must
    /// never be collapsed into one value even while they are numerically
    /// equal. It is a single server-side amount: no client input influences it
    /// (<c>GAME_RULES.md</c> §18, <c>ADR-001</c>).
    /// </summary>
    public const int BattleWonXpReward = 100;

    /// <summary>
    /// The Player XP a lost battle grants (<c>COMBAT_RULES.md</c> §7.2;
    /// <c>ADR-016</c> item 5 — <c>BattleLost</c> → Player XP <c>+0</c>).
    ///
    /// Defeat changes nothing: no XP is granted and therefore no Level changes
    /// (<c>COMBAT_RULES.md</c> §7.5 item 4).
    /// </summary>
    public const int BattleLostXpReward = 0;

    /// <summary>
    /// The Player's identifier (<c>DATABASE.md</c> §1: <c>PlayerId</c> (PK)).
    ///
    /// It is the value the authentication boundary returns as the response's
    /// <c>playerId</c> (<c>API_CONTRACTS.md</c> §2.5), so it is a string
    /// rather than a numeric surrogate: the wire contract types the member as
    /// a string, and the previous placeholder already had that shape.
    /// </summary>
    public required string PlayerId { get; init; }

    /// <summary>
    /// The unique Account identity this Player belongs to
    /// (<c>DATABASE.md</c> §1: <c>AccountId</c> (unique, FK → Account)).
    /// </summary>
    public required Guid AccountId { get; init; }

    /// <summary>
    /// The Player's persistent account XP — cumulative lifetime progression
    /// (<c>DATABASE.md</c> §1, <c>COMBAT_RULES.md</c> §7.2).
    ///
    /// <b>It is uncapped.</b> <c>COMBAT_RULES.md</c> §7.5 item 1 makes XP
    /// cumulative progression with no ceiling, no reset, and no discard at the
    /// cap: it keeps accumulating after Level 50. Only <see cref="Level"/> is
    /// capped (item 2), so a value beyond the Level-50 boundary is legal and
    /// is kept as given — it is never clamped, truncated, or discarded.
    ///
    /// <b>It is Player progression, not Pet progression.</b> The Pet track
    /// owns its own XP on its own instance (<c>ADR-016</c> items 11–12); this
    /// member is neither sourced from nor written to a Pet, and the Pet
    /// track's different cap policy (<c>PET_RULES.md</c> §5.5) applies nowhere
    /// here.
    ///
    /// A newly created Player carries <see cref="InitialXp"/>
    /// (<c>COMBAT_RULES.md</c> §7.5 item 3).
    /// </summary>
    public int XP { get; set; } = InitialXp;

    /// <summary>
    /// The Player's persistent account progression value, in the documented
    /// <c>1–50</c> range (<c>DATABASE.md</c> §3, <c>COMBAT_RULES.md</c> §7;
    /// <c>ADR-016</c> item 2).
    ///
    /// <b>It is the documented function of <see cref="XP"/>.</b>
    /// <c>COMBAT_RULES.md</c> §7.4 defines
    /// <c>Player.Level = min(floor(Player.XP / 100) + 1, 50)</c>, which
    /// <see cref="LevelForXp"/> implements. It stays a persisted column
    /// (<c>DATABASE.md</c> §1/§3) rather than a computed member: the persisted
    /// value remains the documented Player Level field, and there is no second
    /// Level property and no hidden stored level.
    ///
    /// It is settable because the stored value must be maintained when
    /// <see cref="XP"/> changes (<c>COMBAT_RULES.md</c> §7.4). Widening the
    /// setter is the mechanical consequence of that contract, not a new rule;
    /// callers apply <see cref="LevelForXp"/> rather than choosing a value.
    ///
    /// A newly created Player carries <see cref="InitialLevel"/>, which is
    /// <see cref="LevelForXp"/> at <see cref="InitialXp"/>
    /// (<c>COMBAT_RULES.md</c> §7.5 item 3).
    ///
    /// It is a persistent progression value, not a combat stat, and it grants
    /// no combat modifier of any kind: combat statistics live on
    /// <c>PetState</c> (<c>GAME_STATE.md</c> §2.3, <c>COMBAT_RULES.md</c> §7.6,
    /// <c>ADR-011</c>).
    /// </summary>
    public int Level { get; set; } = InitialLevel;

    /// <summary>
    /// When the Player row was created (<c>DATABASE.md</c> §1:
    /// <c>CreatedAt</c>).
    ///
    /// It is set once, at creation, and is not modified by later
    /// authentication: a repeated authentication resolves to the same Player
    /// and therefore to the same creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// The Player Level that <paramref name="xp"/> determines — the documented
    /// formula <c>COMBAT_RULES.md</c> §7.4 owns:
    ///
    /// <code>
    /// Player.Level = min(floor(Player.XP / 100), 49) + 1
    ///              = min(floor(Player.XP / 100) + 1, 50)
    /// </code>
    ///
    /// <b>Deterministic and total.</b> <c>§7.4</c> states that every
    /// non-negative integer <c>Player.XP</c> yields exactly one Player Level,
    /// so this is a pure function of the XP value with no state of its own.
    /// Only the Level is capped, by the <c>min(…, 50)</c> term
    /// (<c>§7.5</c> item 2); the XP it is given is never clamped, truncated,
    /// or discarded (<c>§7.5</c> item 1).
    ///
    /// <b>Why it is a pure function and not a computed property.</b>
    /// <c>DATABASE.md</c> §1/§3 make <see cref="Level"/> a persisted column, so
    /// it must be assigned when XP changes rather than shadowed by a derived
    /// member — a derived member would be a second source of truth for one
    /// field, which the persistence model forbids. This function is that
    /// assignment's only source of the value.
    ///
    /// The formula shares its <i>shape</i> with the Pet curve but shares no
    /// variable, no pool, and no stored value with it (<c>PET_RULES.md</c>
    /// §5.4, <c>ADR-016</c> item 12). It is deliberately not generalized into
    /// a shared progression abstraction: it currently has exactly one
    /// implementation (<c>AGENTS.md</c> §9, <c>ARCHITECTURE.md</c> §5).
    /// </summary>
    /// <param name="xp">
    /// The Player's XP (<c>COMBAT_RULES.md</c> §7.4). The documented domain is
    /// the non-negative integers; a negative value is not a Player XP the
    /// contract defines, so it is refused rather than mapped to a Level.
    /// </param>
    /// <returns>
    /// The Player Level <paramref name="xp"/> determines, in
    /// <c>[<see cref="MinLevel"/>, <see cref="MaxLevel"/>]</c>.
    /// </returns>
    public static int LevelForXp(int xp)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(xp);

        // §7.4: min(floor(XP / 100) + 1, 50). The divisor is the curve constant
        // (§7.3), read from the one constant that owns it so the formula has a
        // single spelling.
        //
        // Integer division of a non-negative value IS floor division, so the
        // guard above is what makes this the documented floor rather than a
        // truncation toward zero.
        return Math.Min((xp / XpPerLevelCurveConstant) + 1, MaxLevel);
    }

    /// <summary>
    /// Applies a battle's documented Player XP grant and maintains
    /// <see cref="Level"/> accordingly — the reward step of
    /// <c>COMBAT_RULES.md</c> §7.2/§7.4 on the battle-end path.
    ///
    /// <b>The caller supplies an outcome-derived amount, never a choice.</b>
    /// <c>§7.2</c> fixes the two outcomes' amounts
    /// (<see cref="BattleWonXpReward"/>, see <see cref="BattleLostXpReward"/>),
    /// and this method only adds what it is given and re-derives the Level: it
    /// evaluates no outcome itself and invents no third reward case.
    /// </summary>
    /// <param name="xpGained">
    /// The documented amount for the battle's outcome. A negative amount is not
    /// a documented grant and is refused rather than applied.
    /// </param>
    public void GrantBattleXp(int xpGained)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(xpGained);

        // §7.5 item 1: XP is uncapped and accumulates, so the grant is a plain
        // addition with no ceiling and no discard.
        XP += xpGained;

        // §7.4: Level follows the XP. It is persisted, not derived on read
        // (DATABASE.md §1/§3), so it is recomputed here whenever XP changes.
        Level = LevelForXp(XP);
    }
}
