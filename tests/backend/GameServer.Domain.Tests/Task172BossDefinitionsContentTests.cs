using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Content fidelity for the two MVP Bosses authored by TASK-172 — Sơn Thạch Vệ
/// and Kim Lôi Vương — as the Domain <c>BossDefinitions</c> carries them.
///
/// <b>The contract asserted here is <c>BOSS_RULES.md</c> §6/§6.1–§6.4's</b>, which
/// is the canonical owner of every value below: §6.1 the base-stat row, §6.2 and
/// §6.2.4/§6.2.5 the Passive row and its per-Passive detail, §6.3 and §6.3.1 the
/// Skill row and its magnitude/duration semantics, and §6.4 the identity table.
/// This file transcribes, and authors nothing: it is the verification half of the
/// provisioning task (<c>DATABASE.md</c> §1 note item 5 — a provisioned row's
/// columns are "sourced per column, with no value computed or invented at
/// provisioning time").
///
/// <b>Both new Passives reuse the existing contracts unchanged.</b> Each is a
/// Turn-based <c>BuffDebuff</c> Status Effect with <c>TargetStat = "ATK"</c> and
/// magnitude <c>+20</c> — the §6.2.1 Rage shape under <c>COMBAT_RULES.md</c>
/// §5.5.1 — and each Skill is direct damage through the unchanged Damage
/// Pipeline using the Boss's own Element. Nothing here introduces a new status
/// type, a new <c>TargetStat</c>, a new trigger category, a new Element, a new
/// Boss State, or a new event.
///
/// <b>What is deliberately NOT implemented, and so not asserted as behavior.</b>
/// The two Passives' <i>effect application</i> — creating the Rage instance at
/// Boss Response step 18a — and the two Skills' <i>execution</i> are separate
/// resolution stages. This file pins the declared content the resolution reads,
/// exactly as the existing suites pin the three earlier Bosses.
/// </summary>
public class Task172BossDefinitionsContentTests
{
    // =======================================================================
    // Identity — BOSS_RULES.md §6.4, DATABASE.md §1 note item 2
    // =======================================================================

    [Fact]
    public void SonThachVe_ShouldCarryTheDocumentedIdentityContract()
    {
        // BOSS_RULES.md §6.4's row, recorded verbatim: the BossId follows the
        // `boss-<ascii-kebab-case-name>` convention, the display name is
        // presentation-only, and the PassiveId / SkillId are the values the
        // Product Owner fixed — NOT normalized to the three earlier rows'
        // observed spellings (TASK-171; §6.4's closing bullet).
        var boss = BossDefinitions.SonThachVe;

        Assert.Equal("boss-son-thach-ve", boss.BossId.Value);
        Assert.Equal("son-thach-ve-enrage", boss.PassiveId.Value);
        Assert.Equal("earthquake", boss.SkillId);

        // DATABASE.md §1 note item 2: the persistence key is an INDEPENDENT third
        // concept — never derived from, and never equal to, the technical Identity.
        Assert.Equal("boss-def-son-thach-ve", boss.BossDefinitionId);
        Assert.NotEqual(boss.BossDefinitionId, boss.BossId.Value);
    }

    [Fact]
    public void KimLoiVuong_ShouldCarryTheDocumentedIdentityContract()
    {
        var boss = BossDefinitions.KimLoiVuong;

        Assert.Equal("boss-kim-loi-vuong", boss.BossId.Value);
        Assert.Equal("kim-loi-vuong-combo", boss.PassiveId.Value);
        Assert.Equal("thunder-strike", boss.SkillId);

        Assert.Equal("boss-def-kim-loi-vuong", boss.BossDefinitionId);
        Assert.NotEqual(boss.BossDefinitionId, boss.BossId.Value);
    }

    // =======================================================================
    // Base stats and Element — BOSS_RULES.md §6.1
    // =======================================================================

    [Fact]
    public void SonThachVe_ShouldCarryTheDocumentedBaseStats()
    {
        // §6.1: Thổ · HP/MaxHP 3000 · ATK 120 · DEF 0 · EnrageThreshold 1500 · Idle.
        var boss = BossDefinitions.SonThachVe;

        Assert.Equal(Element.Tho, boss.Element);
        Assert.Equal(3000, boss.MaxHP);
        Assert.Equal(120, boss.ATK);
        Assert.Equal(0, boss.DEF);

        // §6.1 states the threshold as "1500 (50%)" of MaxHP — the fraction and
        // the absolute value are the same rule, so the product must reproduce it.
        Assert.Equal(1500d, boss.MaxHP * boss.EnrageThreshold, precision: 9);
    }

    [Fact]
    public void KimLoiVuong_ShouldCarryTheDocumentedBaseStats()
    {
        // §6.1: Kim · HP/MaxHP 2800 · ATK 140 · DEF 0 · EnrageThreshold 2100 · Idle.
        var boss = BossDefinitions.KimLoiVuong;

        Assert.Equal(Element.Kim, boss.Element);
        Assert.Equal(2800, boss.MaxHP);
        Assert.Equal(140, boss.ATK);
        Assert.Equal(0, boss.DEF);

        // §6.1 states the threshold as "2100 (75%)" of MaxHP.
        Assert.Equal(2100d, boss.MaxHP * boss.EnrageThreshold, precision: 9);
    }

    [Fact]
    public void BothNewBosses_ShouldCarryTheDocumentedInitialStateAtFullHealth()
    {
        // §6.1's Initial State is `Idle` for both, and a battle begins with the
        // Boss at full health with its Passive's identity and the start of that
        // Passive's progress (GAME_STATE.md §2.4.1–§2.4.3; BossState.Initial).
        foreach (var boss in new[] { BossDefinitions.SonThachVe, BossDefinitions.KimLoiVuong })
        {
            var state = boss.ToInitialState();

            Assert.Equal(BossStateKind.Idle, state.State);
            Assert.Equal(boss.MaxHP, state.HP);
            Assert.Equal(boss.MaxHP, state.MaxHP);
            Assert.Equal(boss.ATK, state.ATK);
            Assert.Equal(boss.DEF, state.DEF);
            Assert.Equal(boss.Element, state.Element);
            Assert.Equal(boss.BossId, state.BossId);

            // §2.4.2/§2.4.3: the Passive's identity is carried and the Skill
            // begins neither charged nor on cooldown.
            Assert.Equal(boss.PassiveId, state.PassiveId);
            Assert.Equal(0, state.PassiveProgress.Current);
            Assert.Equal(0, state.SkillCharge);
            Assert.Equal(0, state.SkillCooldown);
        }
    }

    [Fact]
    public void BothNewBosses_ShouldApplyNoPassiveEffectAtBattleCreation()
    {
        // Unlike Thủy Ma (whose Battle Start trigger fires at creation,
        // BOSS_RULES.md §6.2.2), both new Passives are threshold-triggered at Boss
        // Response step 18a (§6.2.4/§6.2.5). Battle creation is not a resolution
        // and starts no Turn, so neither may create a Status Effect instance yet —
        // their §6.2 rows state no Battle Start trigger.
        foreach (var boss in new[] { BossDefinitions.SonThachVe, BossDefinitions.KimLoiVuong })
        {
            Assert.Empty(boss.ToInitialState().ActiveStatusEffects);
        }
    }

    // =======================================================================
    // Passive declaration — BOSS_RULES.md §6.2, §6.2.4, §6.2.5
    // =======================================================================

    [Fact]
    public void BothNewPassives_ShouldBeNonMatchChargedWithNullThreshold()
    {
        // §6.2: Sơn Thạch Vệ's trigger is `Boss HP ≤ 50%` and Kim Lôi Vương's is
        // `Player Combo ≥ 4` — both alternate trigger categories (§3 item 2's
        // closed list: `Boss HP`, `Combo`), NOT Match counts. DATABASE.md §1 note
        // item 3 / §3 therefore require `threshold = null`, and state that `null`
        // means "no match-charging threshold" and is NOT a statement that the
        // Passive is always-active: each is threshold-triggered.
        foreach (var boss in new[] { BossDefinitions.SonThachVe, BossDefinitions.KimLoiVuong })
        {
            Assert.Null(boss.PassiveDefinition.Threshold);

            // The Domain's projection: 0 is the non-charged marker the resolution
            // reads as "do not charge", never as "threshold reached immediately".
            Assert.Equal(0, boss.PassiveThreshold);

            // PassiveTracker.Charge rejects a Threshold below 1 (PASSIVE_RULES.md
            // §1 defines one as a Match count), so charging is skipped outright.
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PassiveTracker.Charge(
                    PassiveProgress.AtStart(boss.PassiveThreshold),
                    matchCount: 1,
                    boss.PassiveId));
        }
    }

    [Fact]
    public void SonThachVePassive_ShouldDeclareTheOneTimeNoRetriggerBehavior()
    {
        // §6.2.4: the Passive is authored ONE-TIME — "it does not re-trigger once
        // it has activated" — which is the non-default No reset / persistent form
        // (PASSIVE_RULES.md §4 items 2–3). §4 item 3 requires that be documented on
        // the specific definition, and DATABASE.md §1 note item 3 fixes its storage
        // token as exactly `Persistent` (the Domain's NoReset). No new token exists.
        var passive = BossDefinitions.SonThachVe.PassiveDefinition;

        Assert.Equal("Persistent", passive.ResetBehavior);
        Assert.Equal(PassiveResetBehavior.NoReset, BossDefinitions.SonThachVe.PassiveResetBehavior);

        Assert.Contains(
            passive.ResetBehavior,
            new[] { "Default", "Partial", "Persistent" });
    }

    [Fact]
    public void KimLoiVuongPassive_ShouldDeclareTheDefaultResetBehavior()
    {
        // §6.2.5: Reset Behavior is the documented Default (PASSIVE_RULES.md §4
        // item 1), and a re-trigger follows the existing refresh-not-stack default
        // (COMBAT_RULES.md §5.2 item 2, §5.5.5) — no new stacking behavior is
        // authored, and no non-default token is declared.
        var passive = BossDefinitions.KimLoiVuong.PassiveDefinition;

        Assert.Equal("Default", passive.ResetBehavior);

        // The Domain reads `Default` as the absent override.
        Assert.Null(BossDefinitions.KimLoiVuong.PassiveResetBehavior);
    }

    [Fact]
    public void BothNewPassives_ShouldReuseTheExistingBossAtkModifierShape()
    {
        // §6.2.4/§6.2.5 both state the effect is a Turn-based `BuffDebuff` Status
        // Effect in `BossState.StatusEffects[]` with `TargetStat = "ATK"` and
        // `Magnitude = +20%` — the §6.2.1 Rage shape, consumed by the existing
        // COMBAT_RULES.md §5.5 rule. Durations differ (3 Turns vs 1 Turn).
        //
        // No new status Type and no new TargetStat is introduced: the values are
        // the ones the existing §6.2.1 Rage and Mộc Yêu's Root already use.
        var rage = StatusEffect.TurnBased(
            "son-thach-ve-enrage",
            StatusEffectType.BuffDebuff,
            StatusEffectSource.Boss,
            magnitude: 20,
            duration: 3,
            targetStat: "ATK");

        Assert.Equal(StatusEffectType.BuffDebuff, rage.Type);
        Assert.Equal("ATK", rage.TargetStat);
        Assert.Equal(20, rage.Magnitude);
        Assert.Equal(3, rage.RemainingTurns);

        var combo = StatusEffect.TurnBased(
            "kim-loi-vuong-combo",
            StatusEffectType.BuffDebuff,
            StatusEffectSource.Boss,
            magnitude: 20,
            duration: 1,
            targetStat: "ATK");

        Assert.Equal(StatusEffectType.BuffDebuff, combo.Type);
        Assert.Equal("ATK", combo.TargetStat);
        Assert.Equal(20, combo.Magnitude);
        Assert.Equal(1, combo.RemainingTurns);

        // Both reuse the ONE documented `BuffDebuff` type and the ONE documented
        // `"ATK"` TargetStat — neither widens the closed vocabularies.
        Assert.Equal(
            new[] { "ATK" },
            new[] { rage.TargetStat, combo.TargetStat }.Distinct(StringComparer.Ordinal));
        Assert.Equal(
            new[] { StatusEffectType.BuffDebuff },
            new[] { rage.Type, combo.Type }.Distinct());
    }

    // =======================================================================
    // Skill declaration — BOSS_RULES.md §6.3, §6.3.1 items 4–5
    // =======================================================================

    [Theory]
    // §6.3's Skill table, transcribed per Boss: Charge Req. / CD (T) / Base Dmg (ADR-026 D3: BaseDamage = 0).
    [InlineData("boss-son-thach-ve", "earthquake", 5, 0, 0)]
    [InlineData("boss-kim-loi-vuong", "thunder-strike", 5, 0, 0)]
    public void BothNewSkills_ShouldCarryTheDocumentedTimingAndBaseDamage(
        string bossId,
        string skillId,
        int chargeRequirement,
        int cooldownTurns,
        int baseDamage)
    {
        var boss = BossDefinitions.All.Single(b => b.BossId.Value == bossId);

        Assert.Equal(skillId, boss.SkillId);
        Assert.Equal(chargeRequirement, boss.SkillChargeRequirement);
        Assert.Equal(cooldownTurns, boss.SkillCooldownTurns);
        Assert.Equal(baseDamage, boss.SkillBaseDamage);
    }

    [Fact]
    public void BothNewSkills_ShouldDeclareTheirApprovedSecondaryEffects()
    {
        // ADR-026 D6 / D7:
        // Sơn Thạch Vệ declares BossShield (150 magnitude).
        // Kim Lôi Vương declares NextAtkBuff (50 magnitude, NextQualifyingAttack).
        var sonThachVeEffect = BossDefinitions.SonThachVe.SkillDefinition.SecondaryEffect;
        Assert.NotNull(sonThachVeEffect);
        Assert.Equal(BossSkillSecondaryEffectKind.BossShield, sonThachVeEffect!.Value.Kind);
        Assert.Equal(150, sonThachVeEffect.Value.Magnitude);

        var kimLoiVuongEffect = BossDefinitions.KimLoiVuong.SkillDefinition.SecondaryEffect;
        Assert.NotNull(kimLoiVuongEffect);
        Assert.Equal(BossSkillSecondaryEffectKind.NextAtkBuff, kimLoiVuongEffect!.Value.Kind);
        Assert.Equal(50, kimLoiVuongEffect.Value.Magnitude);
        Assert.Equal(StatusEffect.NextQualifyingAttackCondition, kimLoiVuongEffect.Value.ExpiryCondition);
    }

    [Fact]
    public void BothNewSkills_ShouldCarryExpectedSecondaryEffectKinds()
    {
        // ADR-026 D3, D5, D6, D7:
        // Flame Burst: AtkBuff, Drain Power: PowerDrain, Root: AtkDebuff,
        // Earthquake: BossShield, Thunder Strike: NextAtkBuff.
        var documented = new Dictionary<string, BossSkillSecondaryEffectKind>(StringComparer.Ordinal)
        {
            ["boss-hoa-long"] = BossSkillSecondaryEffectKind.AtkBuff,
            ["boss-thuy-ma"] = BossSkillSecondaryEffectKind.PowerDrain,
            ["boss-moc-yeu"] = BossSkillSecondaryEffectKind.AtkDebuff,
            ["boss-son-thach-ve"] = BossSkillSecondaryEffectKind.BossShield,
            ["boss-kim-loi-vuong"] = BossSkillSecondaryEffectKind.NextAtkBuff,
        };

        foreach (var boss in BossDefinitions.All)
        {
            var effect = boss.SkillDefinition.SecondaryEffect;

            Assert.NotNull(effect);
            Assert.Equal(documented[boss.BossId.Value], effect!.Value.Kind);
        }
    }

    // =======================================================================
    // Cast-priority score — BOSS_RULES.md §4.3 item 5, §4.4 item 5 (TASK-308)
    // =======================================================================

    [Fact]
    public void SonThachVe_ShouldCarryTheApprovedCastPriorityScore()
    {
        // BOSS_RULES.md §4.3 item 5 / §4.4 item 5 and TASK-308 §1 (OD-3b): the
        // approved initial cast-priority score for the one migrated Boss Card
        // (`earthquake`) is 16. It is content, not a formula — the value is not
        // derived from the board weights or from another Boss.
        Assert.Equal(16, BossDefinitions.SonThachVe.CastPriorityScore);

        // DATABASE.md §1/§3 (OD-3a): the carrier is nullable — `NULL` is the
        // explicit "no cast-priority score authored" marker, distinct from a real
        // 0 score.
        Assert.Equal(typeof(int?), typeof(BossDefinition).GetProperty(
            nameof(BossDefinition.CastPriorityScore))!.PropertyType);
    }

    [Fact]
    public void TheOtherFourBosses_ShouldLeaveTheCastPriorityScoreUnauthored()
    {
        // ADR-029 §3 / BOSS_RULES.md §4.4 item 5: only Sơn Thạch Vệ's ability is
        // migrated in Slice 3. The other four abilities stay on the transitional
        // BossSkill path and their Power costs, magnitudes, and cast-priority scores
        // remain unresolved until Slice 4 — so each carries NULL, and no guessed
        // score is seeded for an unmigrated ability.
        foreach (var boss in BossDefinitions.All)
        {
            if (boss.BossId.Value == "boss-son-thach-ve")
            {
                continue;
            }

            Assert.Null(boss.CastPriorityScore);
        }
    }
}
