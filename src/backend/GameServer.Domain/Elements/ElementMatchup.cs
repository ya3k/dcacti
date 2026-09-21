namespace GameServer.Domain.Elements;

/// <summary>
/// The outcome of one matchup between an attacking Element and a defending
/// Element (<c>ELEMENT_RULES.md</c> §2.1).
///
/// <code>
/// IF A counters D (A → D)        → Advantage
/// ELSE IF D counters A (D → A)   → Disadvantage
/// ELSE (A == D, or no element)   → Neutral
/// </code>
///
/// The set is closed: §2.1 states "There is no partial/graduated advantage. A
/// matchup is exactly one of Advantage / Neutral / Disadvantage." No fourth
/// outcome may be introduced (<c>AGENTS.md</c> §7).
///
/// This is a Domain value, not a wire contract: no Battle Event and no protocol
/// message carries a matchup or modifier (<c>GAME_EVENTS.md</c> §2,
/// <c>SIGNALR_PROTOCOL.md</c>), and <c>COMBAT_RULES.md</c> §3 step 3 consumes the
/// modifier as an intermediate damage-pipeline factor only.
/// </summary>
public enum ElementMatchup
{
    /// <summary>
    /// <c>A</c> counters <c>D</c> — <c>A → D</c> in the Tương Khắc cycle
    /// (<c>ELEMENT_RULES.md</c> §2, §2.1). Default modifier
    /// <c>1.50×</c> (§2.2).
    /// </summary>
    Advantage = 0,

    /// <summary>
    /// Equal Elements, or either side absent (<c>ELEMENT_RULES.md</c> §2.1,
    /// §3). Default modifier <c>1.00×</c> (§2.2).
    /// </summary>
    Neutral = 1,

    /// <summary>
    /// <c>D</c> counters <c>A</c> — <c>D → A</c> in the Tương Khắc cycle
    /// (<c>ELEMENT_RULES.md</c> §2, §2.1). Default modifier
    /// <c>0.75×</c> (§2.2).
    /// </summary>
    Disadvantage = 2,
}
