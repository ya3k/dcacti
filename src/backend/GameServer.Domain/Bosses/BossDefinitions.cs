using GameServer.Domain.Elements;
using GameServer.Domain.Passives;

namespace GameServer.Domain.Bosses;

/// <summary>
/// The static MVP Boss definitions (<c>BOSS_RULES.md</c> §6, §6.1–§6.4).
///
/// <code>
/// Boss        Element   HP / MaxHP   ATK   DEF   Enrage   Initial State
/// ---------   -------   ----------   ---   ---   ------   -------------
/// Hỏa Long    Hỏa       5000         100   50    30%      Idle
/// Thủy Ma     Thủy      5000         100   50    30%      Idle
/// Mộc Yêu     Mộc       5000         100   50    30%      Idle
/// </code>
///
/// <b>These are the approved values, transcribed — not computed.</b>
/// <c>BOSS_RULES.md</c> §6.1 owns the table above and states the values are
/// "MVP base configuration — not universal balance invariants. The project owner
/// approved these values." §6.3 states the same for the Skill timing values
/// below. This type therefore holds them as plain data. It defines no stat
/// formula: HP is not derived from the player's HP, and ATK/DEF are not derived
/// from the player's ATK/DEF — no such scaling rule exists in any document, and
/// §6.1 states the values "do not represent formulas or scaling rules". Nothing
/// here varies per Boss either: §6.1 gives all three content-defined Bosses the
/// same base stats, and only their Elements and identities differ.
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
/// <b>The identities are <c>BOSS_RULES.md</c> §6.4's, verbatim.</b> §6.4 is the
/// identity contract for <c>BossId</c>, <c>PassiveId</c>, and <c>SkillId</c> —
/// "they are fixed here so no task invents its own". The <c>BossId</c> is the
/// display name (<c>"Hỏa Long"</c>), not a slug; the <c>PassiveId</c> values
/// follow the kebab-case pattern of the Pet PassiveIds; and the <c>SkillId</c>
/// values are the Skill names §6.4 fixes. No other spelling is used, and none is
/// derived from the display name at run time.
///
/// <b>The Passive/Skill mechanics are configuration; their effects are not
/// implemented.</b> §6.2–§6.3 give each Boss a Passive trigger and a Skill
/// timing, and this type carries those values so the resolution can charge,
/// trigger, and cast. The Passive <i>effects</i> — Hỏa Long's Rage, Thủy Ma's
/// healing reduction, Mộc Yêu's regeneration — and the Skill <i>effects</i> —
/// Burn, Power drain, ATK reduction — remain unimplemented and are owned by their
/// own stages (<c>BOSS_RULES.md</c> §3 item 3, §4 item 4). Nothing here applies
/// one.
///
/// <b>Thủy Ma's <c>PassiveThreshold</c> is <c>0</c> — the Always-Active
/// marker.</b> §6.2 gives its trigger as "Passive (always active)", an alternate
/// trigger (<c>PASSIVE_RULES.md</c> §3) rather than a Match count, and states it
/// "is never charged via <c>PassiveTracker.Charge</c> on Player Matches, and
/// emits no <c>PassiveCharged</c>/<c>PassiveTriggered</c> from match progress".
/// Storing <c>0</c> makes that explicit in the configuration; the resolution
/// reads it as "do not charge", never as "threshold reached immediately".
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
    /// Hỏa Long — <c>Element = Hỏa</c>, Passive <c>"boss-hoa-long-rage"</c> every
    /// 5 Player Matches, Skill <c>"flame-burst"</c> at 5 Matches / 2 Turn cooldown
    /// / 150 base damage (<c>BOSS_RULES.md</c> §6.1–§6.4).
    /// </summary>
    public static readonly BossDefinition HoaLong = new(
        new BossId("Hỏa Long"),
        Element.Hoa,
        MaxHP: 5000,
        ATK: 100,
        DEF: 50,
        PassiveId: new PassiveId("boss-hoa-long-rage"),
        // §6.2: "Every 5 Player Matches" — a match-charged Passive.
        PassiveThreshold: 5,
        SkillId: "flame-burst",
        // §6.3: Charge Req. 5, CD 2T, Skill Base Dmg 150.
        SkillBaseDamage: 150,
        SkillChargeRequirement: 5,
        SkillCooldownTurns: 2,
        // §6.1: "1500 (30%)" of MaxHP 5000.
        EnrageThreshold: 0.30);

    /// <summary>
    /// Thủy Ma — <c>Element = Thủy</c>, Passive <c>"boss-thuy-ma-heal"</c> Always
    /// Active (<c>PassiveThreshold = 0</c>), Skill <c>"drain-power"</c> at 4
    /// Matches / 3 Turn cooldown / 120 base damage
    /// (<c>BOSS_RULES.md</c> §6.1–§6.4).
    /// </summary>
    public static readonly BossDefinition ThuyMa = new(
        new BossId("Thủy Ma"),
        Element.Thuy,
        MaxHP: 5000,
        ATK: 100,
        DEF: 50,
        PassiveId: new PassiveId("boss-thuy-ma-heal"),
        // §6.2: "Passive (always active)" — an alternate trigger, NOT match-based.
        // 0 is the Always-Active marker; the resolution never calls
        // PassiveTracker.Charge for it and emits no match-driven Passive events.
        PassiveThreshold: 0,
        SkillId: "drain-power",
        // §6.3: Charge Req. 4, CD 3T, Skill Base Dmg 120.
        SkillBaseDamage: 120,
        SkillChargeRequirement: 4,
        SkillCooldownTurns: 3,
        // §6.1: "1500 (30%)" of MaxHP 5000.
        EnrageThreshold: 0.30);

    /// <summary>
    /// Mộc Yêu — <c>Element = Mộc</c>, Passive <c>"boss-moc-yeu-regen"</c> every
    /// 5 Player Matches, Skill <c>"root"</c> at 6 Matches / 2 Turn cooldown /
    /// 100 base damage (<c>BOSS_RULES.md</c> §6.1–§6.4).
    /// </summary>
    public static readonly BossDefinition MocYeu = new(
        new BossId("Mộc Yêu"),
        Element.Moc,
        MaxHP: 5000,
        ATK: 100,
        DEF: 50,
        PassiveId: new PassiveId("boss-moc-yeu-regen"),
        // §6.2: "Every 5 Player Matches" — a match-charged Passive.
        PassiveThreshold: 5,
        SkillId: "root",
        // §6.3: Charge Req. 6, CD 2T, Skill Base Dmg 100.
        SkillBaseDamage: 100,
        SkillChargeRequirement: 6,
        SkillCooldownTurns: 2,
        // §6.1: "1500 (30%)" of MaxHP 5000.
        EnrageThreshold: 0.30);

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
