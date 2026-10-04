using GameServer.Domain.Elements;
using GameServer.Domain.Passives;

namespace GameServer.Domain.Bosses;

/// <summary>
/// The static MVP Boss definitions (<c>BOSS_RULES.md</c> §6, §6.1–§6.4).
///
/// <code>
/// Boss          Element   HP / MaxHP   ATK   DEF   Enrage   Initial State
/// -----------   -------   ----------   ---   ---   ------   -------------
/// Hỏa Long      Hỏa       5000         100   50    30%      Idle
/// Thủy Ma       Thủy      5000         100   50    30%      Idle
/// Mộc Yêu       Mộc       5000         100   50    30%      Idle
/// Sơn Thạch Vệ  Thổ       3000         120   0     50%      Idle
/// Kim Lôi Vương Kim       2800         140   0     75%      Idle
/// </code>
///
/// <b>These are the approved values, transcribed — not computed.</b>
/// <c>BOSS_RULES.md</c> §6.1 owns the table above and states the values are
/// "MVP base configuration — not universal balance invariants. The project owner
/// approved these values." §6.3 states the same for the Skill timing values
/// below. This type therefore holds them as plain data. It defines no stat
/// formula: HP is not derived from the active Pet's HP, and ATK/DEF are not derived
/// from the active Pet's ATK/DEF — no such scaling rule exists in any document, and
/// §6.1 states the values "do not represent formulas or scaling rules". The first
/// three Bosses share one base-stat row; the two authored by TASK-172 carry their
/// own, as §6.1 records.
///
/// <b>Only content-defined Bosses appear.</b> <c>BOSS_RULES.md</c> §6 defines
/// exactly these five. §6.1's earlier "two further MVP Bosses are not yet
/// content-defined" note was retired by TASK-172, which applied the TASK-171
/// Product Owner decisions for Sơn Thạch Vệ and Kim Lôi Vương; the MVP Boss set
/// is now complete at five (<c>MVP_SCOPE.md</c> §1, <c>ROADMAP.md</c> §1 Phase 2).
/// The two newest entries are transcribed from §6.1–§6.4 and are never invented or
/// derived from the three earlier ones.
///
/// <b>Element assignments are <c>BOSS_RULES.md</c> §6's.</b> Hỏa Long is Hỏa,
/// Thủy Ma is Thủy, Mộc Yêu is Mộc, Sơn Thạch Vệ is Thổ, and Kim Lôi Vương is
/// Kim. Each Boss has exactly one Element and MVP supports no dual/multi element
/// entity (<c>ELEMENT_RULES.md</c> §1.2, §7); the assignments are transcribed from
/// §6.1 and are neither re-derived nor reassigned here.
///
/// <b>The identities are <c>BOSS_RULES.md</c> §6.4's, verbatim.</b> §6.4 is the
/// identity contract for <c>BossId</c>, <c>PassiveId</c>, and <c>SkillId</c> —
/// "they are fixed here so no task invents its own". The <c>BossId</c> is the
/// Boss's canonical technical Identity, following the §6.4 convention
/// <c>boss-&lt;ascii-kebab-case-name&gt;</c> (ASCII, lowercase, kebab-case, no
/// diacritics) — e.g. <c>"boss-hoa-long"</c>; it is <b>not</b> the display name,
/// which is presentation-only content. The <c>PassiveId</c> values follow the
/// kebab-case pattern of the Pet PassiveIds; and the <c>SkillId</c> values are
/// the Skill names §6.4 fixes. No other spelling is used, and none is derived
/// from the display name at run time.
///
/// <b>The persistence keys are <c>DATABASE.md</c> §1's, verbatim.</b> Each
/// definition carries the independent <c>BossDefinitionId</c> that identifies
/// its persisted <c>BossDefinition</c> row — the <c>boss-def-…</c> values §1
/// fixes (TASK-049). They are a <b>third</b> distinct concept: neither the
/// canonical technical Identity above nor the display name, never derived from
/// either, and never database-generated. Only the identity/configuration subset
/// is persisted; the combat stats remain Domain-only (<c>DATABASE.md</c> §1 note
/// item 1).
///
/// <b>The Passive mechanics are configuration; their effects are not
/// implemented here.</b> §6.2–§6.3 give each Boss a Passive trigger and a Skill
/// timing, and this type carries those values so the resolution can charge,
/// trigger, and cast. For the three earlier Bosses the Passive <i>effects</i> —
/// Hỏa Long's Rage, Thủy Ma's healing reduction, Mộc Yêu's regeneration — are
/// owned by their own stages (<c>BOSS_RULES.md</c> §3 item 3). The two Bosses
/// authored by TASK-172 declare their effects in §6.2.4/§6.2.5, and their Passive
/// <i>effects</i> — both a Turn-based +20% ATK Rage modifier — are likewise a
/// separate implementation stage; this type carries the declaration only. The Skill
/// <i>effects</i> — Burn, Power drain, ATK reduction — are carried here as each
/// Skill's <see cref="BossSkillDefinition.SecondaryEffect"/> declaration and
/// are applied by the step 18b resolution (<c>BOSS_RULES.md</c> §4 item 4,
/// §6.3.1); the Root declaration's <c>-30%</c> is consumed by the Pet's own
/// attack through <c>COMBAT_RULES.md</c> §5.4. Earthquake and Thunder Strike
/// declare no secondary effect (§6.3.1 items 4–5), so their
/// <see cref="BossSkillDefinition.SecondaryEffect"/> is absent — an explicit
/// declaration of "None", not an omission. Nothing here applies an effect.
///
/// <b><c>PassiveThreshold</c> is <c>null</c> for every non-match-charged
/// Passive.</b> §6.2 gives Thủy Ma the <b>Battle Start</b> trigger, Sơn Thạch Vệ
/// the <b>Boss HP ≤ 50%</b> trigger, and Kim Lôi Vương the <b>Player Combo ≥ 4</b>
/// trigger — every one an alternate trigger (<c>PASSIVE_RULES.md</c> §3) rather
/// than a Match count, so each "is never charged via
/// <c>PassiveTracker.Charge</c> on Player Matches, and emits no
/// <c>PassiveCharged</c>/<c>PassiveTriggered</c> from match progress". Storage
/// records the documented <c>null</c> for that case, never a <c>0</c> sentinel
/// (<c>DATABASE.md</c> §1 note item 3). <c>null</c> means <b>no match-charging
/// threshold</b>; it is <b>not</b> a statement that the Passive is always-active —
/// every one of the three is threshold- or event-triggered. The Domain reads it
/// back as its non-charged marker, so the resolution never calls
/// <c>PassiveTracker.Charge</c> for such a Boss. Only Hỏa Long and Mộc Yêu carry a
/// real threshold (<c>5</c>).
///
/// <b>This is configuration, not a registry.</b> It is one static class holding
/// the five transcribed definitions, per <c>ARCHITECTURE.md</c> §5 item 1
/// ("a concrete Domain type with data-driven configuration (numbers only)").
/// It has no lookup, no indexing, and no discovery mechanism, because nothing in
/// the documented contract requires one yet: resolving a <c>bossId</c> is the
/// initialization pipeline's step (<c>GAME_STATE.md</c> §2.4), and it is not
/// implemented by this task.
/// </summary>
public static class BossDefinitions
{
    /// <summary>
    /// Hỏa Long — persistence key <c>"boss-def-hoa-long"</c>, canonical technical
    /// Identity <c>"boss-hoa-long"</c> (display name "Hỏa Long"),
    /// <c>Element = Hỏa</c>, Passive <c>"boss-hoa-long-rage"</c> every 5 Player
    /// Matches, Skill <c>"flame-burst"</c> at 5 Matches / 2 Turn cooldown / 150
    /// base damage (<c>BOSS_RULES.md</c> §6.1–§6.4; <c>DATABASE.md</c> §1).
    /// </summary>
    public static readonly BossDefinition HoaLong = new(
        BossDefinitionId: "boss-def-hoa-long",
        new BossId("boss-hoa-long"),
        Element.Hoa,
        // §6.2/§6.4: "Every 5 Player Matches" — a match-charged Passive, so the
        // stored threshold is 5 (never null) with the documented default reset.
        PassiveDefinition: new BossPassiveDefinition(
            new PassiveId("boss-hoa-long-rage"), 5, "Default"),
        SkillDefinition: new BossSkillDefinition(
            // §6.3: Charge Req. 5, CD 2T, Skill Base Dmg 150.
            SkillId: "flame-burst",
            BaseDamage: 150,
            ChargeRequirement: 5,
            CooldownTurns: 2)
        {
            // §6.3.1 item 1: "Burn: 50 fixed damage/tick for 2 Turns". The
            // magnitude is fixed — §6.3.1 item 1 states it "does not scale with
            // Boss ATK, Pet ATK, percentage MaxHP, or elemental multipliers".
            SecondaryEffect = new BossSkillSecondaryEffect
            {
                Kind = BossSkillSecondaryEffectKind.Burn,
                StatusEffectId = "Burn",
                StatusEffectType = Battle.StatusEffectType.DoT,
                Magnitude = 50,
                DurationTurns = 2,
            },
        })
    {
        // §6.1: MaxHP 5000, ATK 100, DEF 50, Enrage "1500 (30%)" — the shared
        // MVP base configuration (the Domain defaults, not persisted columns).
        MaxHP = 5000,
        ATK = 100,
        DEF = 50,
        EnrageThreshold = 0.30,
    };

    /// <summary>
    /// Thủy Ma — persistence key <c>"boss-def-thuy-ma"</c>, canonical technical
    /// Identity <c>"boss-thuy-ma"</c> (display name "Thủy Ma"),
    /// <c>Element = Thủy</c>, Passive <c>"boss-thuy-ma-heal"</c> Always Active
    /// (<c>PassiveThreshold = 0</c>), Skill <c>"drain-power"</c> at 4 Matches /
    /// 3 Turn cooldown / 120 base damage (<c>BOSS_RULES.md</c> §6.1–§6.4;
    /// <c>DATABASE.md</c> §1).
    /// </summary>
    public static readonly BossDefinition ThuyMa = new(
        BossDefinitionId: "boss-def-thuy-ma",
        new BossId("boss-thuy-ma"),
        Element.Thuy,
        // §6.2: "Passive (always active)" — an alternate trigger, NOT
        // match-based, so storage records the documented `null` threshold
        // (never 0 — DATABASE.md §1 note item 3). The Domain reads that back as
        // its Always-Active marker, so the resolution never calls
        // PassiveTracker.Charge for it and emits no match-driven Passive events.
        PassiveDefinition: new BossPassiveDefinition(
            new PassiveId("boss-thuy-ma-heal"), null, "Default"),
        SkillDefinition: new BossSkillDefinition(
            // §6.3: Charge Req. 4, CD 3T, Skill Base Dmg 120.
            SkillId: "drain-power",
            BaseDamage: 120,
            ChargeRequirement: 4,
            CooldownTurns: 3)
        {
            // §6.3.1 item 2: "Instantly subtracts 20 flat Power from the active
            // Pet (PetState.Power = max(0, PetState.Power - 20))". No
            // StatusEffectId/Type/TargetStat and no DurationTurns: §6.3.1 item 2
            // states the representation is a flat reduction and the duration is
            // "None (instant stat reduction, not a persistent status effect)",
            // which GAME_STATE.md §2.3.1 item 9 records as creating no instance.
            SecondaryEffect = new BossSkillSecondaryEffect
            {
                Kind = BossSkillSecondaryEffectKind.PowerDrain,
                Magnitude = 20,
            },
        })
    {
        // §6.1 base stats — the shared MVP configuration.
        MaxHP = 5000,
        ATK = 100,
        DEF = 50,
        EnrageThreshold = 0.30,
    };

    /// <summary>
    /// Mộc Yêu — persistence key <c>"boss-def-moc-yeu"</c>, canonical technical
    /// Identity <c>"boss-moc-yeu"</c> (display name "Mộc Yêu"),
    /// <c>Element = Mộc</c>, Passive <c>"boss-moc-yeu-regen"</c> every 5 Player
    /// Matches, Skill <c>"root"</c> at 6 Matches / 2 Turn cooldown / 100 base
    /// damage (<c>BOSS_RULES.md</c> §6.1–§6.4; <c>DATABASE.md</c> §1).
    /// </summary>
    public static readonly BossDefinition MocYeu = new(
        BossDefinitionId: "boss-def-moc-yeu",
        new BossId("boss-moc-yeu"),
        Element.Moc,
        // §6.2/§6.4: "Every 5 Player Matches" — a match-charged Passive.
        PassiveDefinition: new BossPassiveDefinition(
            new PassiveId("boss-moc-yeu-regen"), 5, "Default"),
        SkillDefinition: new BossSkillDefinition(
            // §6.3: Charge Req. 6, CD 2T, Skill Base Dmg 100.
            SkillId: "root",
            BaseDamage: 100,
            ChargeRequirement: 6,
            CooldownTurns: 2)
        {
            // §6.3.1 item 3: "-30% Pet ATK debuff for 2 Turns ... Percentage-based
            // ATK reduction (-30% active Pet ATK)", applied as a Turn-based
            // Buff/Debuff (COMBAT_RULES.md §5.1) whose TargetStat is "ATK"
            // (GAME_STATE.md §2.3.1's example for Type = BuffDebuff).
            //
            // How that magnitude reaches the Pet's own damage is owned by
            // COMBAT_RULES.md §5.4 (TASK-119): §5.4.1 consumes it at the
            // Player → Boss Damage Pipeline Step 1 Attack input, §5.4.2 fixes
            // truncate-toward-zero rounding, and §5.4.4 keeps PetState.ATK
            // untouched. The consumer is StatusEffectLifecycle.EffectiveAttack.
            // This declaration therefore carries the instance's values only and
            // still decides no rule.
            SecondaryEffect = new BossSkillSecondaryEffect
            {
                Kind = BossSkillSecondaryEffectKind.AtkDebuff,
                StatusEffectId = "Root",
                StatusEffectType = Battle.StatusEffectType.BuffDebuff,
                TargetStat = "ATK",
                Magnitude = 30,
                DurationTurns = 2,
            },
        })
    {
        // §6.1 base stats — the shared MVP configuration.
        MaxHP = 5000,
        ATK = 100,
        DEF = 50,
        EnrageThreshold = 0.30,
    };

    /// <summary>
    /// Sơn Thạch Vệ — persistence key <c>"boss-def-son-thach-ve"</c>, canonical
    /// technical Identity <c>"boss-son-thach-ve"</c> (display name
    /// "Sơn Thạch Vệ"), <c>Element = Thổ</c>, Passive
    /// <c>"son-thach-ve-enrage"</c> on the <c>Boss HP ≤ 50%</c> trigger, Skill
    /// <c>"earthquake"</c> at 5 Matches / 0 Turn cooldown / 150 base damage
    /// (<c>BOSS_RULES.md</c> §6.1–§6.4; <c>DATABASE.md</c> §1).
    /// </summary>
    public static readonly BossDefinition SonThachVe = new(
        BossDefinitionId: "boss-def-son-thach-ve",
        new BossId("boss-son-thach-ve"),
        Element.Tho,
        // §6.2/§6.2.4: the trigger is "Boss HP ≤ 50%" — the alternate Boss HP
        // category (§3 item 2), NOT a Match count — so storage records the
        // documented `null` threshold (never 0 — DATABASE.md §1 note item 3); the
        // Domain reads that back as its non-charged marker.
        //
        // §6.2.4: the reset behavior is the one documented NON-default override —
        // "No reset / persistent": the Passive is authored one-time and does not
        // re-trigger once it has activated. PASSIVE_RULES.md §4 item 3 requires
        // that be declared on the specific definition, and DATABASE.md §1 note
        // item 3 / §3 fix its storage token as exactly `Persistent` (the Domain's
        // PassiveResetBehavior.NoReset). No new token is introduced.
        PassiveDefinition: new BossPassiveDefinition(
            new PassiveId("son-thach-ve-enrage"), null, "Persistent"),
        SkillDefinition: new BossSkillDefinition(
            // §6.3: Charge Req. 5, CD 0T, Skill Base Dmg 150.
            SkillId: "earthquake",
            BaseDamage: 150,
            ChargeRequirement: 5,
            CooldownTurns: 0))
    {
        // §6.1: MaxHP 3000, ATK 120, DEF 0, Enrage "1500 (50%)".
        // §6.3.1 item 4: Earthquake declares NO secondary effect ("The Skill
        // applies no debuff, no status effect, and no resource change") and NO
        // board effect, so SecondaryEffect stays absent — the explicit "None" of
        // §6.3's Secondary Effect column, not an omission.
        MaxHP = 3000,
        ATK = 120,
        DEF = 0,
        EnrageThreshold = 0.50,
    };

    /// <summary>
    /// Kim Lôi Vương — persistence key <c>"boss-def-kim-loi-vuong"</c>, canonical
    /// technical Identity <c>"boss-kim-loi-vuong"</c> (display name
    /// "Kim Lôi Vương"), <c>Element = Kim</c>, Passive
    /// <c>"kim-loi-vuong-combo"</c> on the <c>Player Combo ≥ 4</c> trigger, Skill
    /// <c>"thunder-strike"</c> at 5 Matches / 0 Turn cooldown / 180 base damage
    /// (<c>BOSS_RULES.md</c> §6.1–§6.4; <c>DATABASE.md</c> §1).
    /// </summary>
    public static readonly BossDefinition KimLoiVuong = new(
        BossDefinitionId: "boss-def-kim-loi-vuong",
        new BossId("boss-kim-loi-vuong"),
        Element.Kim,
        // §6.2/§6.2.5: the trigger is "Player Combo ≥ 4" — the alternate Combo
        // category (§3 item 2), NOT a Match count — so storage records the
        // documented `null` threshold (never 0 — DATABASE.md §1 note item 3).
        //
        // §6.2.5: Reset Behavior is the documented Default (PASSIVE_RULES.md §4
        // item 1), and a re-trigger follows the existing refresh-not-stack default
        // (COMBAT_RULES.md §5.2 item 2, §5.5.5) — so no non-default token is
        // declared, exactly as the three earlier Bosses leave it.
        PassiveDefinition: new BossPassiveDefinition(
            new PassiveId("kim-loi-vuong-combo"), null, "Default"),
        SkillDefinition: new BossSkillDefinition(
            // §6.3: Charge Req. 5, CD 0T, Skill Base Dmg 180.
            SkillId: "thunder-strike",
            BaseDamage: 180,
            ChargeRequirement: 5,
            CooldownTurns: 0))
    {
        // §6.1: MaxHP 2800, ATK 140, DEF 0, Enrage "2100 (75%)".
        // §6.3.1 item 5: Thunder Strike declares NO secondary effect ("The Skill
        // applies no debuff, no status effect, and no resource change") and NO
        // board effect, so SecondaryEffect stays absent.
        MaxHP = 2800,
        ATK = 140,
        DEF = 0,
        EnrageThreshold = 0.75,
    };

    /// <summary>
    /// The five content-defined MVP Bosses of <c>BOSS_RULES.md</c> §6, in the
    /// order that document lists them.
    ///
    /// It is a reading aid for a caller that needs every definition (a test
    /// iterating the MVP set, for example). It is not a registry, holds no
    /// lookup, and is not a second source for any of the five values above —
    /// each element is the same instance.
    /// </summary>
    public static readonly IReadOnlyList<BossDefinition> All =
    [
        HoaLong,
        ThuyMa,
        MocYeu,
        SonThachVe,
        KimLoiVuong,
    ];
}
