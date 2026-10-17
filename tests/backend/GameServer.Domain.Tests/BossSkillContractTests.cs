namespace GameServer.Domain.Tests;

using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using Xunit;

/// <summary>
/// Tests for all five Boss Skill contracts under ADR-026 (Scope C).
/// </summary>
public class BossSkillContractTests
{
    // =======================================================================
    // 1. Direct BaseDamage = 0 on all 5 MVP Boss Skills (D3)
    // =======================================================================

    [Theory]
    [InlineData("boss-hoa-long", "flame-burst")]
    [InlineData("boss-thuy-ma", "drain-power")]
    [InlineData("boss-moc-yeu", "root")]
    [InlineData("boss-son-thach-ve", "earthquake")]
    [InlineData("boss-kim-loi-vuong", "thunder-strike")]
    public void AllFiveBossSkills_ShouldHaveZeroDirectBaseDamage(string bossId, string expectedSkillId)
    {
        var boss = BossDefinitions.All.Single(b => b.BossId.Value == bossId);

        Assert.Equal(expectedSkillId, boss.SkillId);
        Assert.Equal(0, boss.SkillBaseDamage);
        Assert.Equal(0, boss.SkillDefinition.BaseDamage);
    }

    // =======================================================================
    // 2. Hỏa Long: flame-burst (D5) & Multiplicative Stacking (T3)
    // =======================================================================

    [Fact]
    public void HoaLong_FlameBurst_ShouldDeclareAtkBuffAndNoBurnDot()
    {
        var boss = BossDefinitions.HoaLong;
        var effect = boss.SkillDefinition.SecondaryEffect;

        Assert.NotNull(effect);
        Assert.Equal(BossSkillSecondaryEffectKind.AtkBuff, effect!.Value.Kind);
        Assert.Equal("flame-burst", effect.Value.StatusEffectId);
        Assert.Equal(StatusEffectType.BuffDebuff, effect.Value.StatusEffectType);
        Assert.Equal("ATK", effect.Value.TargetStat);
        Assert.Equal(50, effect.Value.Magnitude);
        Assert.Equal(2, effect.Value.DurationTurns);
        Assert.Null(effect.Value.ExpiryCondition);

        // Convert to status effect instance
        var instance = effect.Value.ToStatusEffect();
        Assert.NotNull(instance);
        Assert.Equal("flame-burst", instance!.Value.Id);
        Assert.Equal(StatusEffectType.BuffDebuff, instance.Value.Type);
        Assert.Equal("ATK", instance.Value.TargetStat);
        Assert.Equal(50, instance.Value.Magnitude);
        Assert.Equal(2, instance.Value.RemainingTurns);
    }

    [Fact]
    public void HoaLong_FlameBurstAndRage_ShouldStackMultiplicatively()
    {
        // ADR-026 T3: Flame Burst (+50%) and Rage (+20%) stack multiplicatively
        // via StatusEffectLifecycle.EffectiveBossAttack.
        // Base ATK = 100
        // Fold in Id ascending order:
        // 1. "boss-hoa-long-rage": 100 * (100 + 20) / 100 = 120
        // 2. "flame-burst": 120 * (100 + 50) / 100 = 180
        var boss = BossDefinitions.HoaLong;

        var rage = StatusEffect.TurnBased(
            "boss-hoa-long-rage",
            StatusEffectType.BuffDebuff,
            StatusEffectSource.Boss,
            magnitude: 20,
            duration: 3,
            targetStat: "ATK");

        var flameBurst = BossDefinitions.HoaLong.SkillDefinition.SecondaryEffect!.Value.ToStatusEffect()!.Value;

        var effectiveAtk = StatusEffectLifecycle.EffectiveBossAttack(
            boss.ATK,
            [rage, flameBurst]);

        Assert.Equal(180, effectiveAtk);
    }

    // =======================================================================
    // 3. Thủy Ma: drain-power (D3)
    // =======================================================================

    [Fact]
    public void ThuyMa_DrainPower_ShouldDeclareFlatPowerDrain()
    {
        var boss = BossDefinitions.ThuyMa;
        var effect = boss.SkillDefinition.SecondaryEffect;

        Assert.NotNull(effect);
        Assert.Equal(BossSkillSecondaryEffectKind.PowerDrain, effect!.Value.Kind);
        Assert.Equal(20, effect.Value.Magnitude);
        Assert.Null(effect.Value.StatusEffectId);
        Assert.Null(effect.Value.StatusEffectType);
        Assert.Null(effect.Value.DurationTurns);

        // Instant stat reduction, creates no StatusEffect instance (GAME_STATE.md §2.3.1 item 9)
        Assert.Null(effect.Value.ToStatusEffect());
    }

    // =======================================================================
    // 4. Mộc Yêu: root (D3)
    // =======================================================================

    [Fact]
    public void MocYeu_Root_ShouldDeclareAtkDebuff()
    {
        var boss = BossDefinitions.MocYeu;
        var effect = boss.SkillDefinition.SecondaryEffect;

        Assert.NotNull(effect);
        Assert.Equal(BossSkillSecondaryEffectKind.AtkDebuff, effect!.Value.Kind);
        Assert.Equal("Root", effect.Value.StatusEffectId);
        Assert.Equal(StatusEffectType.BuffDebuff, effect.Value.StatusEffectType);
        Assert.Equal("ATK", effect.Value.TargetStat);
        Assert.Equal(30, effect.Value.Magnitude);
        Assert.Equal(2, effect.Value.DurationTurns);

        var instance = effect.Value.ToStatusEffect();
        Assert.NotNull(instance);
        Assert.Equal("Root", instance!.Value.Id);
        Assert.Equal(30, instance.Value.Magnitude);
        Assert.Equal(2, instance.Value.RemainingTurns);
    }

    // =======================================================================
    // 5. Sơn Thạch Vệ: earthquake (D6)
    // =======================================================================

    [Fact]
    public void SonThachVe_Earthquake_ShouldDeclareBossShield()
    {
        var boss = BossDefinitions.SonThachVe;
        var effect = boss.SkillDefinition.SecondaryEffect;

        Assert.NotNull(effect);
        Assert.Equal(BossSkillSecondaryEffectKind.BossShield, effect!.Value.Kind);
        Assert.Equal("Shield", effect.Value.StatusEffectId);
        Assert.Equal(StatusEffectType.Shield, effect.Value.StatusEffectType);
        Assert.Equal(150, effect.Value.Magnitude);
        Assert.Equal(StatusEffect.ShieldDepletedCondition, effect.Value.ExpiryCondition);
        Assert.Null(effect.Value.DurationTurns);

        var instance = effect.Value.ToStatusEffect();
        Assert.NotNull(instance);
        Assert.Equal("Shield", instance!.Value.Id);
        Assert.Equal(StatusEffectType.Shield, instance.Value.Type);
        Assert.Equal(150, instance.Value.Magnitude);
        Assert.Equal(StatusEffect.ShieldDepletedCondition, instance.Value.ExpiryCondition);
        Assert.False(instance.Value.UsesTurnCountdown);
    }

    // =======================================================================
    // 6. Kim Lôi Vương: thunder-strike (D7)
    // =======================================================================

    [Fact]
    public void KimLoiVuong_ThunderStrike_ShouldDeclareNextQualifyingAtkBuff()
    {
        var boss = BossDefinitions.KimLoiVuong;
        var effect = boss.SkillDefinition.SecondaryEffect;

        Assert.NotNull(effect);
        Assert.Equal(BossSkillSecondaryEffectKind.NextAtkBuff, effect!.Value.Kind);
        Assert.Equal("thunder-strike", effect.Value.StatusEffectId);
        Assert.Equal(StatusEffectType.BuffDebuff, effect.Value.StatusEffectType);
        Assert.Equal("ATK", effect.Value.TargetStat);
        Assert.Equal(50, effect.Value.Magnitude);
        Assert.Equal(StatusEffect.NextQualifyingAttackCondition, effect.Value.ExpiryCondition);
        Assert.Null(effect.Value.DurationTurns);

        var instance = effect.Value.ToStatusEffect();
        Assert.NotNull(instance);
        Assert.Equal("thunder-strike", instance!.Value.Id);
        Assert.Equal(StatusEffectType.BuffDebuff, instance.Value.Type);
        Assert.Equal("ATK", instance.Value.TargetStat);
        Assert.Equal(50, instance.Value.Magnitude);
        Assert.Equal(StatusEffect.NextQualifyingAttackCondition, instance.Value.ExpiryCondition);
        Assert.False(instance.Value.UsesTurnCountdown);
    }

    [Fact]
    public void KimLoiVuong_ThunderStrike_ShouldNotBeConsumedByStep19aCountdown()
    {
        // Step 19a duration decrement only decrements Turn-based effects (UsesTurnCountdown == true).
        // Trigger-based effects like Thunder Strike must survive step 19a untouched.
        var thunderStrike = BossDefinitions.KimLoiVuong.SkillDefinition.SecondaryEffect!.Value.ToStatusEffect()!.Value;

        var effects = new StatusEffect[] { thunderStrike };
        var remaining = StatusEffectLifecycle.ConsumeAtStep19a(effects);

        var surviving = Assert.Single(remaining);
        Assert.Equal("thunder-strike", surviving.Id);
    }

    [Fact]
    public void KimLoiVuong_ThunderStrike_ShouldBuffNextQualifyingAttackAndThenBeConsumed()
    {
        var boss = BossDefinitions.KimLoiVuong.ToInitialState();
        var thunderStrike = BossDefinitions.KimLoiVuong.SkillDefinition.SecondaryEffect!.Value.ToStatusEffect()!.Value;
        boss = boss with { ActiveStatusEffects = [thunderStrike] };

        // Base ATK is 140. Thunder Strike is +50% ATK -> 140 * 150 / 100 = 210.
        var effectiveAtk = StatusEffectLifecycle.EffectiveBossAttack(boss.ATK, boss.ActiveStatusEffects);
        Assert.Equal(210, effectiveAtk);

        // Case 1: Non-qualifying turn (no confirmed ATK Match) -> modifier preserved
        var nonQualifyingResult = AttackResolution.ResolveBossAttack(
            boss,
            new PetState(new Pets.PetId("pet"), 1000, 1000, 100, 50, 0, 0, Element.Hoa, new Passives.PassiveId("p"), Passives.PassiveProgress.AtStart(5)),
            AttackQualification.None,
            baseDamagePool: 0,
            combo: 1,
            rngState: default);

        var bossAfterNonQualifying = AttackResolution.ConsumeBossNextAttackModifiers(boss, nonQualifyingResult.IsQualifyingAttack);
        Assert.Single(bossAfterNonQualifying.ActiveStatusEffects);

        // Case 2: Qualifying turn (a confirmed ATK Match) -> modifier consumed
        var qualifyingResult = AttackResolution.ResolveBossAttack(
            boss,
            new PetState(new Pets.PetId("pet"), 1000, 1000, 100, 50, 0, 0, Element.Hoa, new Passives.PassiveId("p"), Passives.PassiveProgress.AtStart(5)),
            new AttackQualification(HasConfirmedAtkMatch: true),
            baseDamagePool: 30,
            combo: 1,
            rngState: default);

        var bossAfterQualifying = AttackResolution.ConsumeBossNextAttackModifiers(boss, qualifyingResult.IsQualifyingAttack);
        Assert.Empty(bossAfterQualifying.ActiveStatusEffects);
    }
}
