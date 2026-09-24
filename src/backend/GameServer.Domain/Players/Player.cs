namespace GameServer.Domain.Players;

/// <summary>
/// The Player — the persistent account/owner of the collection
/// (<c>DATABASE.md</c> §1, <c>MVP_SCOPE.md</c> §1).
///
/// <code>
/// Player
/// ├── PlayerId        (PK)
/// ├── DiscordUserId   (unique)
/// ├── Level           (1–50 — PET_RULES.md §5; persistent account
/// │                    attribute, NO combat stats; ADR-012)
/// └── CreatedAt
/// </code>
///
/// <b>This is the documented field set, not a new decision.</b>
/// <c>DATABASE.md</c> §1 defines exactly these four fields, and §3 records
/// their constraints. No further field is added: in particular there is no
/// XP column and no reward field, because <c>DATABASE.md</c> §1 defines none
/// and TASK-033 owns progression (`AGENTS.md` §17).
///
/// <b>The Player owns no combat statistics.</b> <c>ADR-011</c> item 5 and
/// <c>DATABASE.md</c> §3 state that HP/ATK/DEF/Crit/Power are battle-time
/// <c>PetState</c> values (<c>GAME_STATE.md</c> §2.3), never Player columns.
/// <c>Player.Level</c> is a persistent progression value, not a combat stat
/// (<c>PET_RULES.md</c> §5 item 3).
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
    /// <c>PET_RULES.md</c> §5 item 1 — the <c>1–50</c> range).
    /// </summary>
    public const int MinLevel = 1;

    /// <summary>
    /// The highest legal <see cref="Level"/> (<c>DATABASE.md</c> §3,
    /// <c>PET_RULES.md</c> §5 item 1 — the <c>1–50</c> range).
    /// </summary>
    public const int MaxLevel = 50;

    /// <summary>
    /// The documented initial <see cref="Level"/> of a newly created Player
    /// (<c>PET_RULES.md</c> §5 item 8, <c>PET_RULES.md</c> §5.1 item 4,
    /// <c>DATABASE.md</c> §3).
    ///
    /// It is the initial value only. It defines no XP amount, no XP curve, no
    /// level-up threshold, and no rate of increase — those remain undefined
    /// and are owned by TASK-033 (<c>PET_RULES.md</c> §5 item 2). It is not
    /// derived from <see cref="MinLevel"/>: a range states the legal values a
    /// value may hold, not the value it holds at creation.
    /// </summary>
    public const int InitialLevel = 1;

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
    /// The stable Discord identity this Player belongs to
    /// (<c>DATABASE.md</c> §1: <c>DiscordUserId</c> (unique)).
    ///
    /// It is the Discord User object's <c>id</c> field as returned by
    /// <c>GET https://discord.com/api/users/@me</c> — a snowflake serialized
    /// by Discord as a string (<c>API_CONTRACTS.md</c> §2.3 item 3, §2.4,
    /// <c>ADR-013</c> item 6). It is stored and compared as an opaque string
    /// and is never parsed into a numeric type.
    ///
    /// It is <b>not</b> the authorization code, the Discord access token, or
    /// the application session (<c>API_CONTRACTS.md</c> §2.4), and none of
    /// those may be substituted for it.
    ///
    /// Uniqueness is enforced by the persistence layer
    /// (<c>DATABASE.md</c> §3), which is the authoritative protection against
    /// duplicate ownership records for one Discord account.
    /// </summary>
    public required string DiscordUserId { get; init; }

    /// <summary>
    /// The Player's persistent account progression value, in the documented
    /// <c>1–50</c> range (<c>DATABASE.md</c> §3, <c>PET_RULES.md</c> §5 item
    /// 1, <c>ADR-012</c> item 1).
    ///
    /// A newly created Player carries <see cref="InitialLevel"/>
    /// (<c>PET_RULES.md</c> §5 item 8), and an existing Player's Level is
    /// preserved on subsequent authentication — the match-or-create path
    /// never resets or reassigns it.
    ///
    /// It is a persistent progression value, not a combat stat, and it does
    /// not reach a running battle: combat statistics live on
    /// <c>PetState</c> (<c>GAME_STATE.md</c> §2.3, <c>ADR-011</c>).
    /// </summary>
    public int Level { get; init; } = InitialLevel;

    /// <summary>
    /// When the Player row was created (<c>DATABASE.md</c> §1:
    /// <c>CreatedAt</c>).
    ///
    /// It is set once, at creation, and is not modified by later
    /// authentication: a repeated authentication resolves to the same Player
    /// and therefore to the same creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }
}
