namespace GameServer.Domain.Match3;

/// <summary>
/// The four MVP Gem types (<c>MATCH3_RULES.md</c> §1.1, <c>GAME_RULES.md</c> §6).
///
/// Gems are functional only and carry <b>no Element</b>: a Gem type is not an
/// Element, does not map to one, and confers no elemental advantage or
/// disadvantage (<c>MATCH3_RULES.md</c> §1.1, <c>ELEMENT_RULES.md</c> §1). Element
/// belongs to the Pet/Boss combat system, not to the board.
///
/// These four are the complete MVP set — no other Gem type exists. The Five
/// Elements are deliberately absent.
/// </summary>
public enum GemType
{
    /// <summary>ATK Gem (<c>MATCH3_RULES.md</c> §1.1).</summary>
    Atk = 0,

    /// <summary>DEF Gem (<c>MATCH3_RULES.md</c> §1.1).</summary>
    Def = 1,

    /// <summary>HP Gem (<c>MATCH3_RULES.md</c> §1.1).</summary>
    Hp = 2,

    /// <summary>POWER Gem (<c>MATCH3_RULES.md</c> §1.1).</summary>
    Power = 3,
}

/// <summary>
/// Helpers for <see cref="GemType"/> that keep the documented type set in one
/// place.
/// </summary>
public static class GemTypes
{
    /// <summary>
    /// The four MVP Gem types in their canonical order
    /// (<c>MATCH3_RULES.md</c> §1.1: ATK, DEF, HP, POWER).
    /// </summary>
    public static readonly IReadOnlyList<GemType> All =
    [
        GemType.Atk,
        GemType.Def,
        GemType.Hp,
        GemType.Power,
    ];

    /// <summary>
    /// Number of Gem types (<c>MATCH3_RULES.md</c> §1.1). Used as the exclusive
    /// bound when drawing a Gem type from the PRNG.
    /// </summary>
    public const uint Count = 4;

    /// <summary>
    /// True when the underlying value is one of the four documented Gem types.
    ///
    /// A board cell always holds exactly one Gem type
    /// (<c>MATCH3_RULES.md</c> §1), so this is the domain contract check for an
    /// out-of-range value.
    /// </summary>
    public static bool IsValid(int value) => value is >= 0 and < (int)Count;

    /// <summary>
    /// The documented wire/display name of a Gem type
    /// (<c>MATCH3_RULES.md</c> §1.1): <c>ATK</c>, <c>DEF</c>, <c>HP</c>,
    /// <c>POWER</c>.
    /// </summary>
    public static string ToContractName(GemType type) => type switch
    {
        GemType.Atk => "ATK",
        GemType.Def => "DEF",
        GemType.Hp => "HP",
        GemType.Power => "POWER",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Not a documented Gem type."),
    };
}