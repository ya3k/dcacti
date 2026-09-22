using GameServer.Domain.Elements;

namespace GameServer.Domain.Bosses;

/// <summary>
/// The static MVP Boss definitions (<c>BOSS_RULES.md</c> §6, §6.1).
///
/// <code>
/// Boss        Element   HP / MaxHP   ATK   DEF   Initial State
/// ---------   -------   ----------   ---   ---   -------------
/// Hỏa Long    Hỏa       5000         100   50    Idle
/// Thủy Ma     Thủy      5000         100   50    Idle
/// Mộc Yêu     Mộc       5000         100   50    Idle
/// </code>
///
/// <b>These are the approved values, transcribed — not computed.</b>
/// <c>BOSS_RULES.md</c> §6.1 owns the table above and states the values are
/// "MVP base configuration — not universal balance invariants. The project owner
/// approved these values." This type therefore holds them as plain data. It
/// defines no stat formula: HP is not derived from the player's HP, and ATK/DEF
/// are not derived from the player's ATK/DEF — no such scaling rule exists in
/// any document, and §6.1 states the values "do not represent formulas or
/// scaling rules". Nothing here varies per Boss either: §6.1 gives all three
/// content-defined Bosses the same base stats, and only their Elements and
/// identities differ.
///
/// <b>Only content-defined Bosses appear.</b> <c>BOSS_RULES.md</c> §6 defines
/// exactly these three. §6.1 records that two further MVP Bosses are "not yet
/// content-defined" and lists what each must declare when authored; they are
/// deliberately absent here rather than invented to reach the five-Boss scope of
/// <c>GAME_RULES.md</c> §19. Adding one is a content change owned by the Boss
/// rules, not a value to guess.
///
/// <b>Element assignments are <c>BOSS_RULES.md</c> §6's.</b> Hỏa Long is Hỏa,
/// Thủy Ma is Thủy, and Mộc Yêu is Mộc. Each Boss has exactly one Element and
/// MVP supports no dual/multi element entity (<c>ELEMENT_RULES.md</c> §1.2, §7);
/// the assignments are transcribed from §6.1 and are neither re-derived nor
/// reassigned here.
///
/// <b>Passive and Skill are not represented.</b> §6 defines both for each Boss
/// (Hỏa Long's Rage/Flame Burst, Thủy Ma's healing reduction/Drain Power, Mộc
/// Yêu's regeneration/Root), but they belong to the Boss Passive and Boss Skill
/// systems (<c>BOSS_RULES.md</c> §3–§4) and are <b>not yet implemented</b>, not
/// <b>not required</b>: <see cref="BossDefinition"/> carries no
/// <c>PassiveId</c>/<c>SkillId</c> placeholder for them.
///
/// <b>This is configuration, not a registry.</b> It is one static class holding
/// the three transcribed definitions, per <c>ARCHITECTURE.md</c> §5 item 1
/// ("a concrete Domain type with data-driven configuration (numbers only)").
/// It has no lookup, no indexing, and no discovery mechanism, because nothing in
/// the documented contract requires one yet: resolving a <c>bossId</c> is the
/// initialization pipeline's step (<c>GAME_STATE.md</c> §2.4), and it is not
/// implemented by this task.
/// </summary>
public static class BossDefinitions
{
    /// <summary>
    /// Hỏa Long — <c>Element = Hỏa</c> (<c>BOSS_RULES.md</c> §6.1).
    ///
    /// Its Passive ("Every 5 Player Matches → gain Rage") and Skill ("Flame Burst
    /// → Damage + Burn") are §6's and are not implemented (§3–§4).
    /// </summary>
    public static readonly BossDefinition HoaLong = new(
        new BossId("Hỏa Long"),
        Element.Hoa,
        MaxHP: 5000,
        ATK: 100,
        DEF: 50);

    /// <summary>
    /// Thủy Ma — <c>Element = Thủy</c> (<c>BOSS_RULES.md</c> §6.1).
    ///
    /// Its Passive ("Healing received reduced") and Skill ("Drain Power → Reduce
    /// Player Power") are §6's and are not implemented (§3–§4).
    /// </summary>
    public static readonly BossDefinition ThuyMa = new(
        new BossId("Thủy Ma"),
        Element.Thuy,
        MaxHP: 5000,
        ATK: 100,
        DEF: 50);

    /// <summary>
    /// Mộc Yêu — <c>Element = Mộc</c> (<c>BOSS_RULES.md</c> §6.1).
    ///
    /// Its Passive ("Every 5 Matches → regenerate HP") and Skill ("Root → Reduce
    /// Player ATK") are §6's and are not implemented (§3–§4).
    /// </summary>
    public static readonly BossDefinition MocYeu = new(
        new BossId("Mộc Yêu"),
        Element.Moc,
        MaxHP: 5000,
        ATK: 100,
        DEF: 50);

    /// <summary>
    /// The three content-defined MVP Bosses of <c>BOSS_RULES.md</c> §6, in the
    /// order that document lists them.
    ///
    /// It is a reading aid for a caller that needs every definition (a test
    /// iterating the MVP set, for example). It is not a registry, holds no
    /// lookup, and is not a second source for any of the three values above —
    /// each element is the same instance.
    /// </summary>
    public static readonly IReadOnlyList<BossDefinition> All =
    [
        HoaLong,
        ThuyMa,
        MocYeu,
    ];
}
