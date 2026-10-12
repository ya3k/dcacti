namespace GameServer.Domain.Tests;

using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Relics;
using Xunit;

/// <summary>
/// Domain ATK-gated damage resolution tests (<c>ADR-026</c> D1, Scope A).
/// </summary>
public class AttackResolutionTests
{
    private static PetState DefaultPet() =>
        new(
            PetId: new Pets.PetId("pet-test"),
            Element: Element.Hoa,
            HP: 1000,
            MaxHP: 1000,
            ATK: 100,
            DEF: 50,
            Crit: 0,
            Power: 0,
            PassiveId: new Passives.PassiveId("test-passive"),
            PassiveProgress: Passives.PassiveProgress.AtStart(5));

    private static BossState DefaultBoss() =>
        BossDefinitions.HoaLong.ToInitialState();

    // =======================================================================
    // 1. Player Attack Gating
    // =======================================================================

    [Fact]
    public void PlayerAttack_WhenZeroAtkGemsCleared_ShouldDealZeroDamageAndEmitNoEvents()
    {
        // Given: 0 ATK gems cleared (DEF or HP match only)
        var pet = DefaultPet();
        var boss = DefaultBoss();
        var cleared = new ClearedGemCounts(Atk: 0, Def: 3, Hp: 0, Power: 0);

        // When: Attack is resolved
        var result = AttackResolution.ResolvePlayerAttack(
            pet,
            boss,
            cleared,
            baseDamagePool: 0,
            combo: 1,
            rngState: default);

        // Then: Not a qualifying attack, 0 damage dealt, no damage events emitted
        Assert.False(result.IsQualifyingAttack);
        Assert.Null(result.DamageResult);
        Assert.Equal(0, result.FinalDamage);
        Assert.Equal(boss.HP, result.DefenderHp);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void PlayerAttack_WhenAtkGemsCleared_ShouldCalculateDamageAndEmitEvents()
    {
        // Given: 3 ATK gems cleared
        var pet = DefaultPet();
        var boss = DefaultBoss();
        var cleared = new ClearedGemCounts(Atk: 3, Def: 0, Hp: 0, Power: 0);

        // When: Attack is resolved
        var result = AttackResolution.ResolvePlayerAttack(
            pet,
            boss,
            cleared,
            baseDamagePool: 30,
            combo: 1,
            rngState: default);

        // Then: Qualifying attack, damage calculated, events emitted
        Assert.True(result.IsQualifyingAttack);
        Assert.NotNull(result.DamageResult);
        Assert.True(result.FinalDamage > 0);
        Assert.True(result.DefenderHp < boss.HP);
        Assert.Equal(3, result.Events.Count);
        Assert.Contains(result.Events, e => e.Type == BattleEventType.DamageCalculated);
        Assert.Contains(result.Events, e => e.Type == BattleEventType.DamageDealt);
        Assert.Contains(result.Events, e => e.Type == BattleEventType.DamageTaken);
    }

    // =======================================================================
    // 2. Boss Attack Gating
    // =======================================================================

    [Fact]
    public void BossAttack_WhenZeroAtkGemsCleared_ShouldDealZeroDamageAndNotExecuteBasicAttack()
    {
        // Given: Boss cleared 0 ATK gems
        var boss = DefaultBoss();
        var pet = DefaultPet();
        var cleared = new ClearedGemCounts(Atk: 0, Def: 3, Hp: 0, Power: 0);

        // When: Attack is resolved
        var result = AttackResolution.ResolveBossAttack(
            boss,
            pet,
            cleared,
            baseDamagePool: 0,
            combo: 1,
            rngState: default);

        // Then: Not a qualifying attack, zero damage, Pet HP unchanged
        Assert.False(result.IsQualifyingAttack);
        Assert.Null(result.DamageResult);
        Assert.Equal(0, result.FinalDamage);
        Assert.Equal(pet.HP, result.DefenderHp);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void BossAttack_WhenAtkGemsCleared_ShouldDealDamageToPet()
    {
        // Given: Boss cleared 3 ATK gems
        var boss = DefaultBoss();
        var pet = DefaultPet();
        var cleared = new ClearedGemCounts(Atk: 3, Def: 0, Hp: 0, Power: 0);

        // When: Attack is resolved
        var result = AttackResolution.ResolveBossAttack(
            boss,
            pet,
            cleared,
            baseDamagePool: 30,
            combo: 1,
            rngState: default);

        // Then: Qualifying attack, damage dealt to Pet
        Assert.True(result.IsQualifyingAttack);
        Assert.NotNull(result.DamageResult);
        Assert.True(result.FinalDamage > 0);
        Assert.True(result.DefenderHp < pet.HP);
        Assert.Equal(3, result.Events.Count);
    }

    // =======================================================================
    // 3. Modifier Preservation vs. Consumption
    // =======================================================================

    [Fact]
    public void PlayerNextAttackModifiers_WhenZeroAtkGems_ShouldBePreserved()
    {
        // Given: Pet with NextAttack Crit and ATK modifiers
        var pet = DefaultPet() with
        {
            NextAttackCritModifiers = [new NextAttackCritModifier("source_crit", 25)],
            ATKModifiers = [new ATKModifier("source_atk", 20, RelicEffectLifetime.NextAttack)],
        };
        var boss = DefaultBoss();
        var cleared = new ClearedGemCounts(Atk: 0, Def: 3, Hp: 0, Power: 0);

        // When: Attack is resolved and applied
        var (updatedPet, _, result) = AttackResolution.ResolveAndApplyPlayerAttack(
            pet,
            boss,
            cleared,
            baseDamagePool: 0,
            combo: 1,
            rngState: default);

        // Then: Not qualifying, modifiers are preserved intact
        Assert.False(result.IsQualifyingAttack);
        Assert.Single(updatedPet.NextAttackCritModifiers);
        Assert.Single(updatedPet.ATKModifiers);
        Assert.Equal(25, updatedPet.NextAttackCritModifiers[0].CritContribution);
    }

    [Fact]
    public void PlayerNextAttackModifiers_WhenAtkGemsMatched_ShouldBeConsumed()
    {
        // Given: Pet with NextAttack Crit and ATK modifiers
        var pet = DefaultPet() with
        {
            NextAttackCritModifiers = [new NextAttackCritModifier("source_crit", 25)],
            ATKModifiers = [new ATKModifier("source_atk", 20, RelicEffectLifetime.NextAttack)],
        };
        var boss = DefaultBoss();
        var cleared = new ClearedGemCounts(Atk: 3, Def: 0, Hp: 0, Power: 0);

        // When: Attack is resolved and applied
        var (updatedPet, _, result) = AttackResolution.ResolveAndApplyPlayerAttack(
            pet,
            boss,
            cleared,
            baseDamagePool: 30,
            combo: 1,
            rngState: default);

        // Then: Qualifying attack, modifiers are consumed
        Assert.True(result.IsQualifyingAttack);
        Assert.Empty(updatedPet.NextAttackCritModifiers);
        Assert.Empty(updatedPet.ATKModifiers);
    }

    [Fact]
    public void BossNextQualifyingAttackModifier_WhenZeroAtkGems_ShouldBePreserved()
    {
        // Given: Boss with Thunder Strike NextQualifyingAttack modifier
        var bossModifier = StatusEffect.TriggerBased(
            "thunder-strike",
            StatusEffectType.BuffDebuff,
            StatusEffectSource.Boss,
            magnitude: 50,
            StatusEffect.NextQualifyingAttackCondition,
            targetStat: "ATK");
        var boss = DefaultBoss() with
        {
            ActiveStatusEffects = [bossModifier],
        };
        var pet = DefaultPet();
        var cleared = new ClearedGemCounts(Atk: 0, Def: 3, Hp: 0, Power: 0);

        // When: Boss attack resolved and applied
        var (updatedBoss, _, result) = AttackResolution.ResolveAndApplyBossAttack(
            boss,
            pet,
            cleared,
            baseDamagePool: 0,
            combo: 1,
            rngState: default);

        // Then: Not qualifying, modifier preserved
        Assert.False(result.IsQualifyingAttack);
        Assert.Single(updatedBoss.ActiveStatusEffects);
        Assert.Equal("thunder-strike", updatedBoss.ActiveStatusEffects[0].Id);
    }

    [Fact]
    public void BossNextQualifyingAttackModifier_WhenAtkGemsMatched_ShouldBeConsumed()
    {
        // Given: Boss with Thunder Strike NextQualifyingAttack modifier
        var bossModifier = StatusEffect.TriggerBased(
            "thunder-strike",
            StatusEffectType.BuffDebuff,
            StatusEffectSource.Boss,
            magnitude: 50,
            StatusEffect.NextQualifyingAttackCondition,
            targetStat: "ATK");
        var boss = DefaultBoss() with
        {
            ActiveStatusEffects = [bossModifier],
        };
        var pet = DefaultPet();
        var cleared = new ClearedGemCounts(Atk: 3, Def: 0, Hp: 0, Power: 0);

        // When: Boss attack resolved and applied
        var (updatedBoss, _, result) = AttackResolution.ResolveAndApplyBossAttack(
            boss,
            pet,
            cleared,
            baseDamagePool: 30,
            combo: 1,
            rngState: default);

        // Then: Qualifying attack, modifier consumed
        Assert.True(result.IsQualifyingAttack);
        Assert.Empty(updatedBoss.ActiveStatusEffects);
    }

    // =======================================================================
    // 4. Qualifying Attack with Zero Final Damage (Mitigation / Shield Absorption)
    // =======================================================================

    [Fact]
    public void QualifyingAttack_WhenFullyAbsorbedByShield_ShouldBeQualifyingAndConsumeModifiers()
    {
        // Given: Pet with NextAttack modifier, attacking Boss with massive Shield (5000)
        var pet = DefaultPet() with
        {
            NextAttackCritModifiers = [new NextAttackCritModifier("source_crit", 20)],
        };
        var shield = StatusEffect.TriggerBased(
            "Shield",
            StatusEffectType.Shield,
            StatusEffectSource.Boss,
            magnitude: 5000,
            StatusEffect.ShieldDepletedCondition);
        var boss = DefaultBoss() with
        {
            ActiveStatusEffects = [shield],
        };
        var cleared = new ClearedGemCounts(Atk: 3, Def: 0, Hp: 0, Power: 0);

        // When: Attack is resolved and applied
        var (updatedPet, updatedBoss, result) = AttackResolution.ResolveAndApplyPlayerAttack(
            pet,
            boss,
            cleared,
            baseDamagePool: 30,
            combo: 1,
            rngState: default);

        // Then:
        // 1. It IS a qualifying attack despite HP damage being 0
        Assert.True(result.IsQualifyingAttack);
        Assert.Equal(0, result.DamageResult!.Value.Calculation.FinalDamage - result.DamageResult.Value.AbsorbedDamage);
        Assert.Equal(boss.HP, updatedBoss.HP); // HP undamaged because Shield absorbed it
        // 2. Shield pool is reduced by the absorbed damage
        Assert.True(result.DefenderShieldPool < 5000);
        // 3. Damage events ARE emitted
        Assert.Equal(3, result.Events.Count);
        // 4. NextAttack modifier IS consumed
        Assert.Empty(updatedPet.NextAttackCritModifiers);
    }
}
