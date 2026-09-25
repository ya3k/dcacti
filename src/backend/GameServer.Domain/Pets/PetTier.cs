namespace GameServer.Domain.Pets;

/// <summary>
/// The five Pet Tiers (<c>PET_RULES.md</c> §3,
/// <c>GAME_RULES.md</c> §9.8–9.9, <c>DATABASE.md</c> §3).
///
/// <code>
/// Common → Rare → Epic → Legendary → Mythic
/// </code>
///
/// <b>The set is closed and the order is fixed.</b> §3 lists exactly these
/// five members; no sixth Tier exists in MVP. Member order follows the
/// documented progression so numeric order reads as power progression —
/// a reading aid only: no rule derives from the numeric value, and Tier is
/// never derived from Player Level (<c>PET_RULES.md</c> §5 item 9,
/// ADR-012 item 5).
///
/// <b>Tier does not change Element</b> (<c>PET_RULES.md</c> §3 item 3) and
/// is an independent progression axis from Star and Level (§5 item 9).
/// There is no Tier-up, Evolution, or Tier derivation rule in MVP; the
/// multi-Tier framework in §3 is the rule scaffold for future Pet content,
/// while MVP ships one Tier instance per the five MVP Pets (§3 item 4).
/// </summary>
public enum PetTier
{
    /// <summary>Common — the lowest Tier in §3's progression.</summary>
    Common = 0,

    /// <summary>Rare — above Common.</summary>
    Rare = 1,

    /// <summary>Epic — above Rare.</summary>
    Epic = 2,

    /// <summary>Legendary — above Epic.</summary>
    Legendary = 3,

    /// <summary>Mythic — the highest Tier in §3's progression.</summary>
    Mythic = 4,
}
