using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Authoritative DEF mechanics verification test suite.
/// Verifies that DEF actually affects combat calculations according to COMBAT_RULES.md §3.2,
/// §5.4.6, §5.5.6, and GAME_RULES.md §17.
///
/// Formula: Mitigated Damage = Pre-Defense Damage * ( K / (K + DEF) ), where K = 100.
/// Final Damage = (int)Math.Truncate(Mitigated Damage), clamped to minimum 0.
/// Shield absorbs Final Damage before remaining damage reduces HP.
/// </summary>
public class DefenseMechanicsVerificationTests
{
    private static BossState CreateBoss(
        int hp = 1000,
        int def = 50,
        Element element = Element.Tho) =>
        BossState.Initial(
            new BossId("boss-test"),
            element,
            maxHp: hp,
            atk: 100,
            def: def,
            passiveId: new PassiveId("passive-test"),
            passiveThreshold: 5) with { HP = hp };

    private static DamagePipeline.DamageInputs Inputs(
        int attack = 100,
        int baseDamagePool = 0,
        int combo = 1,
        Element? attacker = Element.Hoa,
        Element? defender = Element.Hoa,
        int defense = 0,
        int defenderHp = 1000,
        DamageParty source = DamageParty.Player,
        DamageParty target = DamageParty.Boss,
        int shieldPool = 0,
        double otherModifiers = 1.0) =>
        new(
            Attack: attack,
            BaseDamagePool: baseDamagePool,
            Combo: combo,
            AttackerElement: attacker,
            DefenderElement: defender,
            DefenderDefense: defense,
            DefenderHp: defenderHp,
            Source: source,
            Target: target,
            DefenderShieldPool: shieldPool,
            OtherModifiers: otherModifiers);

    private static DamageResult Calculate(DamagePipeline.DamageInputs inputs) =>
        DamagePipeline.Calculate(inputs, ComboModifiers.Default, ElementModifiers.Default);

    // =======================================================================
    // 1. Baseline damage with zero DEF
    // =======================================================================

    [Fact]
    public void BaselineDamage_WithZeroDefense_ShouldApplyNoMitigation()
    {
        // COMBAT_RULES.md §3.2: K / (K + DEF) with K=100 and DEF=0 is 100/100 = 1.00.
        // Pre-Defense damage: 100 (ATK 100, Combo 1 = 1.0x, Neutral Element = 1.0x, Other = 1.0x)
        var result = Calculate(Inputs(attack: 100, combo: 1, defense: 0));

        Assert.Equal(100.0, result.Calculation.Defense, precision: 6);
        Assert.Equal(100, result.Calculation.FinalDamage);
        Assert.Equal(900, result.TargetHp);
    }

    // =======================================================================
    // 2. Damage against positive DEF
    // =======================================================================

    [Theory]
    [InlineData(25, 80.0, 80)]     // Pet default DEF: 100 * (100 / 125) = 80.0 -> 80
    [InlineData(50, 66.666667, 66)] // Boss base DEF (Hoa Long): 100 * (100 / 150) = 66.667 -> 66
    [InlineData(100, 50.0, 50)]    // 100 DEF: 100 * (100 / 200) = 50.0 -> 50
    [InlineData(300, 25.0, 25)]    // 300 DEF: 100 * (100 / 400) = 25.0 -> 25
    public void DamageAgainstPositiveDefense_ShouldMitigatePerHyperbolicFormula(
        int defense,
        double expectedMitigated,
        int expectedFinalDamage)
    {
        var result = Calculate(Inputs(attack: 100, combo: 1, defense: defense));

        Assert.Equal(expectedMitigated, result.Calculation.Defense, precision: 5);
        Assert.Equal(expectedFinalDamage, result.Calculation.FinalDamage);
        Assert.Equal(1000 - expectedFinalDamage, result.TargetHp);
    }

    // =======================================================================
    // 3. Tests fail if DEF is accidentally ignored
    // =======================================================================

    [Fact]
    public void TestMustFail_IfDefenseIsIgnored()
    {
        const int positiveDef = 50;
        var result = Calculate(Inputs(attack: 100, combo: 1, defense: positiveDef));

        // If DEF were ignored (DEF = 0), damage would be 100.
        Assert.NotEqual(100, result.Calculation.FinalDamage);
        Assert.Equal(66, result.Calculation.FinalDamage);
    }

    // =======================================================================
    // 4. Tests fail if DEF is accidentally applied twice
    // =======================================================================

    [Fact]
    public void TestMustFail_IfDefenseIsAppliedTwice()
    {
        const int positiveDef = 25;
        var result = Calculate(Inputs(attack: 100, combo: 1, defense: positiveDef));

        // If DEF factor (100/125 = 0.8) were squared (0.64 -> 64) or DEF doubled (100/150 -> 66):
        Assert.NotEqual(64, result.Calculation.FinalDamage);
        Assert.NotEqual(66, result.Calculation.FinalDamage);
        // Correct single application is 80:
        Assert.Equal(80, result.Calculation.FinalDamage);
    }

    // =======================================================================
    // 5. DEF Increase via Status Effect (Buff)
    // =======================================================================

    [Fact]
    public void DefenseIncrease_ViaBuffDebuffStatusEffect_ShouldIncreaseEffectiveDefense()
    {
        const int baseDef = 25;
        var buff = StatusEffect.TurnBased(
            id: StatusEffectLifecycle.DefenseBuffId,
            type: StatusEffectType.BuffDebuff,
            source: StatusEffectSource.Player,
            magnitude: 25,
            duration: 1,
            targetStat: StatusEffectLifecycle.DefenseStat);

        var effectiveDef = StatusEffectLifecycle.EffectivePetDefense(baseDef, [buff]);

        // Base DEF + magnitude: 25 + 25 = 50.
        Assert.Equal(50, effectiveDef);

        // Pre-Defense damage = 100.
        // With base DEF alone (25): mitigated = 80.
        var baseResult = Calculate(Inputs(attack: 100, defense: baseDef));
        Assert.Equal(80, baseResult.Calculation.FinalDamage);

        // With buffed DEF (50): mitigated = 100 * (100 / 150) = 66.67 -> 66.
        var buffedResult = Calculate(Inputs(attack: 100, defense: effectiveDef));
        Assert.Equal(66, buffedResult.Calculation.FinalDamage);

        Assert.True(buffedResult.Calculation.FinalDamage < baseResult.Calculation.FinalDamage);
    }

    // =======================================================================
    // 6. DEF Decrease via Status Effect (Debuff)
    // =======================================================================

    [Fact]
    public void DefenseDecrease_ViaNegativeMagnitudeStatusEffect_ShouldDecreaseEffectiveDefense()
    {
        const int baseDef = 50;
        var debuff = StatusEffect.TurnBased(
            id: "defense-break",
            type: StatusEffectType.BuffDebuff,
            source: StatusEffectSource.Player,
            magnitude: -20,
            duration: 2,
            targetStat: StatusEffectLifecycle.DefenseStat);

        var effectiveDef = StatusEffectLifecycle.EffectiveBossDefense(baseDef, [debuff]);

        // Base DEF + signed magnitude: 50 + (-20) = 30.
        Assert.Equal(30, effectiveDef);

        // Pre-Defense damage = 100.
        // With base DEF alone (50): mitigated = 66.
        var baseResult = Calculate(Inputs(attack: 100, defense: baseDef));
        Assert.Equal(66, baseResult.Calculation.FinalDamage);

        // With debuffed DEF (30): mitigated = 100 * (100 / 130) = 76.92 -> 76.
        var debuffedResult = Calculate(Inputs(attack: 100, defense: effectiveDef));
        Assert.Equal(76, debuffedResult.Calculation.FinalDamage);

        Assert.True(debuffedResult.Calculation.FinalDamage > baseResult.Calculation.FinalDamage);
    }

    // =======================================================================
    // 7. Multiple DEF modifiers and deterministic composition order
    // =======================================================================

    [Fact]
    public void MultipleDefModifiers_ShouldComposeAdditivelyAndDeterministically()
    {
        const int baseDef = 25;
        var buffA = StatusEffect.TurnBased("buff-a", StatusEffectType.BuffDebuff, StatusEffectSource.Player, 15, 1, "DEF");
        var buffB = StatusEffect.TurnBased("buff-b", StatusEffectType.BuffDebuff, StatusEffectSource.Player, 20, 1, "DEF");
        var debuffC = StatusEffect.TurnBased("debuff-c", StatusEffectType.BuffDebuff, StatusEffectSource.Boss, -10, 1, "DEF");
        // Non-DEF modifier must NOT affect DEF
        var atkBuff = StatusEffect.TurnBased("atk-buff", StatusEffectType.BuffDebuff, StatusEffectSource.Player, 50, 1, "ATK");
        var shield = StatusEffect.TriggerBased("shield-1", StatusEffectType.Shield, StatusEffectSource.Player, 100, StatusEffect.ShieldDepletedCondition);

        var effects = new[] { shield, debuffC, atkBuff, buffB, buffA };
        var effective = StatusEffectLifecycle.EffectivePetDefense(baseDef, effects);

        // 25 + 15 + 20 - 10 = 50. atkBuff and shield must be ignored.
        Assert.Equal(50, effective);
    }

    // =======================================================================
    // 8. Mitigation ordering: Step 4 Other -> Step 5 DEF -> Final -> Shield
    // =======================================================================

    [Fact]
    public void MitigationOrder_OtherModifiersThenDefenseThenShieldAbsorption()
    {
        // Step 1: Base = 80
        // Step 2: Combo = 1 (1.0x)
        // Step 3: Element = Neutral (1.0x)
        // Step 4: Other Modifiers = 1.25x -> Pre-defense damage = 100
        // Step 5: DEF = 25 -> Mitigated = 100 * (100 / 125) = 80.0
        // Step 6: Final Damage = 80
        // Shield absorption: ShieldPool = 30 -> Absorbed = 30, Unabsorbed = 50
        // Target HP = 500 - 50 = 450
        var result = Calculate(Inputs(
            attack: 80,
            combo: 1,
            defense: 25,
            otherModifiers: 1.25,
            shieldPool: 30,
            defenderHp: 500));

        Assert.Equal(80.0, result.Calculation.Defense, precision: 6);
        Assert.Equal(80, result.Calculation.FinalDamage);
        Assert.Equal(30, result.AbsorbedDamage);
        Assert.Equal(0, result.RemainingShieldPool);
        Assert.Equal(450, result.TargetHp);
    }

    // =======================================================================
    // 9. Boundary conditions: Zero damage, truncation, lethal damage, shields
    // =======================================================================

    [Fact]
    public void Boundary_ZeroPreDefenseDamage_ProducesZeroFinalDamage()
    {
        var result = Calculate(Inputs(attack: 0, baseDamagePool: 0, defense: 50));
        Assert.Equal(0.0, result.Calculation.Defense);
        Assert.Equal(0, result.Calculation.FinalDamage);
        Assert.Equal(1000, result.TargetHp);
    }

    [Fact]
    public void Boundary_FractionalTruncationTowardZero()
    {
        // 96 * (100 / 140) = 68.5714... Truncated to 68, not rounded to 69.
        var result = Calculate(Inputs(attack: 96, defense: 40));
        Assert.Equal(68.571428, result.Calculation.Defense, precision: 5);
        Assert.Equal(68, result.Calculation.FinalDamage);
    }

    [Fact]
    public void Boundary_LethalDamage_ClampsTargetHpToZero()
    {
        var result = Calculate(Inputs(attack: 500, defense: 25, defenderHp: 100));
        // 500 * (100 / 125) = 400. Target HP: 100 - 400 clamped to 0.
        Assert.Equal(400, result.Calculation.FinalDamage);
        Assert.Equal(0, result.TargetHp);
    }

    [Fact]
    public void Boundary_ShieldAbsorbsEntireFinalDamage_HpUnchanged()
    {
        var result = Calculate(Inputs(attack: 100, defense: 25, shieldPool: 100, defenderHp: 500));
        // Final damage = 80. Shield absorbs 80, leaving 20. Target HP untouched.
        Assert.Equal(80, result.Calculation.FinalDamage);
        Assert.Equal(80, result.AbsorbedDamage);
        Assert.Equal(20, result.RemainingShieldPool);
        Assert.Equal(500, result.TargetHp);
    }

    [Fact]
    public void Boundary_InvalidDefenseDomain_ThrowsArgumentOutOfRangeException()
    {
        // K + DEF <= 0 throws
        Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(Inputs(defense: -100)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(Inputs(defense: -150)));
    }

    // =======================================================================
    // 10. A move that does not change DEF preserves the correct value
    // =======================================================================

    [Fact]
    public void MoveWithoutDefMatch_PreservesStoredBaseDefense()
    {
        var initialPet = PetState.AtBattleCreation(
            new PetId("pet-test"),
            Element.Hoa,
            new PassiveId("passive-test"),
            passiveThreshold: 5);

        Assert.Equal(PetState.DefaultDEF, initialPet.DEF);

        // ApplyDefenseBuff with 0 defense pool returns original state untouched
        var afterZero = ResourceGenerator.ApplyDefenseBuff(initialPet, 0);
        Assert.Same(initialPet.ActiveStatusEffects, afterZero.ActiveStatusEffects);
        Assert.Equal(PetState.DefaultDEF, afterZero.DEF);

        // EffectivePetDefense with no buffs returns the stored base DEF
        Assert.Equal(PetState.DefaultDEF, StatusEffectLifecycle.EffectivePetDefense(afterZero.DEF, afterZero.ActiveStatusEffects));
    }

    // =======================================================================
    // 11. Server result and emitted event agreement
    // =======================================================================

    [Fact]
    public void ServerResult_EventsMustMatchCalculationExactly()
    {
        var result = Calculate(Inputs(attack: 100, defense: 25, defenderHp: 500));

        var calcEvent = result.Events.Calculated;
        var dealtEvent = result.Events.Dealt;
        var takenEvent = result.Events.Taken;

        Assert.Equal(80.0, calcEvent.Defense, precision: 6);
        Assert.Equal(80, calcEvent.FinalDamage);
        Assert.Equal(80, dealtEvent.Amount);
        Assert.Equal(80, takenEvent.Amount);
    }
}
