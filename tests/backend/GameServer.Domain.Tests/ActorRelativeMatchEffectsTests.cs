namespace GameServer.Domain.Tests;

using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using Xunit;

/// <summary>
/// Tests for actor-relative match effects (DEF Shield, HP Heal, POWER) (<c>ADR-026</c> Scope B, Scope E).
/// </summary>
public class ActorRelativeMatchEffectsTests
{
    private static PetState CreatePet(int hp = 800, int maxHp = 1000, int power = 0) =>
        new(
            PetId: new Pets.PetId("pet-test"),
            Element: Element.Hoa,
            HP: hp,
            MaxHP: maxHp,
            ATK: 100,
            DEF: 50,
            Crit: 0,
            Power: power,
            PassiveId: new Passives.PassiveId("test-passive"),
            PassiveProgress: Passives.PassiveProgress.AtStart(5));

    private static BossState CreateBoss(int hp = 4000, int maxHp = 5000, int skillCharge = 0) =>
        BossDefinitions.HoaLong.ToInitialState() with
        {
            HP = hp,
            MaxHP = maxHp,
            SkillCharge = skillCharge,
        };

    // =======================================================================
    // 1. DEF Match -> Temporary Shield
    // =======================================================================

    [Fact]
    public void ApplyShield_PlayerDefMatch_ShouldGrantShieldToPet()
    {
        // Given: Pet with no shield and 50 DefensePool
        var pet = CreatePet();
        Assert.Empty(pet.ActiveStatusEffects);

        // When: DEF match shield is applied
        var updated = ResourceGenerator.ApplyShield(pet, defensePool: 50);

        // Then: Pet gains Shield of magnitude 50 with ShieldDepleted trigger
        var shield = Assert.Single(updated.ActiveStatusEffects);
        Assert.Equal("Shield", shield.Id);
        Assert.Equal(StatusEffectType.Shield, shield.Type);
        Assert.Equal(StatusEffectSource.Player, shield.Source);
        Assert.Equal(50, shield.Magnitude);
        Assert.Equal(StatusEffect.ShieldDepletedCondition, shield.ExpiryCondition);
    }

    [Fact]
    public void ApplyShield_BossDefMatch_ShouldGrantShieldToBoss()
    {
        // Given: Boss with no shield and 75 DefensePool
        var boss = CreateBoss();
        Assert.Empty(boss.ActiveStatusEffects);

        // When: Boss DEF match shield is applied
        var updated = ResourceGenerator.ApplyShield(boss, defensePool: 75);

        // Then: Boss gains Shield of magnitude 75 with ShieldDepleted trigger
        var shield = Assert.Single(updated.ActiveStatusEffects);
        Assert.Equal("Shield", shield.Id);
        Assert.Equal(StatusEffectType.Shield, shield.Type);
        Assert.Equal(StatusEffectSource.Boss, shield.Source);
        Assert.Equal(75, shield.Magnitude);
        Assert.Equal(StatusEffect.ShieldDepletedCondition, shield.ExpiryCondition);
    }

    [Fact]
    public void ApplyShield_WhenZeroDefensePool_ShouldNotAddShield()
    {
        var pet = CreatePet();
        var boss = CreateBoss();

        var updatedPet = ResourceGenerator.ApplyShield(pet, defensePool: 0);
        var updatedBoss = ResourceGenerator.ApplyShield(boss, defensePool: 0);

        Assert.Empty(updatedPet.ActiveStatusEffects);
        Assert.Empty(updatedBoss.ActiveStatusEffects);
    }

    [Fact]
    public void ApplyShield_WhenShieldAlreadyActive_ShouldRefreshInPlace()
    {
        // Given: Pet with existing Shield of 30
        var pet = ResourceGenerator.ApplyShield(CreatePet(), 30);
        Assert.Equal(30, pet.ActiveStatusEffects[0].Magnitude);

        // When: New DEF match yields 60 shield
        var updated = ResourceGenerator.ApplyShield(pet, 60);

        // Then: Shield is refreshed in place with new magnitude, at most one element
        var shield = Assert.Single(updated.ActiveStatusEffects);
        Assert.Equal(60, shield.Magnitude);
    }

    // =======================================================================
    // 2. HP Match -> Healing up to MaxHP
    // =======================================================================

    [Fact]
    public void ApplyHeal_PlayerHpMatch_ShouldHealPetUpToMaxHp()
    {
        // Given: Pet at 800 / 1000 HP, raw heal of 100
        var pet = CreatePet(hp: 800, maxHp: 1000);

        // When: Heal applied
        var updated = ResourceGenerator.ApplyHeal(pet, rawHeal: 100);

        // Then: HP is 900
        Assert.Equal(900, updated.HP);
    }

    [Fact]
    public void ApplyHeal_PlayerHpMatch_ShouldClampOverhealAtMaxHp()
    {
        // Given: Pet at 950 / 1000 HP, raw heal of 100
        var pet = CreatePet(hp: 950, maxHp: 1000);

        // When: Heal applied
        var updated = ResourceGenerator.ApplyHeal(pet, rawHeal: 100);

        // Then: HP is clamped to MaxHP 1000
        Assert.Equal(1000, updated.HP);
    }

    [Fact]
    public void ApplyBossHeal_BossHpMatch_ShouldHealBossUpToMaxHp()
    {
        // Given: Boss at 4000 / 5000 HP, raw heal of 500
        var boss = CreateBoss(hp: 4000, maxHp: 5000);

        // When: Boss heal applied
        var updated = ResourceGenerator.ApplyBossHeal(boss, rawHeal: 500);

        // Then: Boss HP is 4500
        Assert.Equal(4500, updated.HP);
    }

    [Fact]
    public void ApplyBossHeal_BossHpMatch_ShouldClampOverhealAtMaxHp()
    {
        // Given: Boss at 4800 / 5000 HP, raw heal of 400
        var boss = CreateBoss(hp: 4800, maxHp: 5000);

        // When: Boss heal applied
        var updated = ResourceGenerator.ApplyBossHeal(boss, rawHeal: 400);

        // Then: Boss HP is clamped to 5000
        Assert.Equal(5000, updated.HP);
    }

    [Fact]
    public void ApplyBossHeal_WhenAlreadyAtFullHp_ShouldRemainAtMaxHp()
    {
        // Given: Boss already at MaxHP
        var boss = CreateBoss(hp: 5000, maxHp: 5000);

        // When: Heal applied
        var updated = ResourceGenerator.ApplyBossHeal(boss, rawHeal: 200);

        // Then: HP safely remains at MaxHP
        Assert.Equal(5000, updated.HP);
    }

    // =======================================================================
    // 3. POWER Match -> Actor Relative Semantics (D2, T1, T2)
    // =======================================================================

    [Fact]
    public void ApplyActorMatchEffects_PlayerTurn_ShouldOnlyChargePetPower()
    {
        // Given: Player turn with POWER generation (30 power)
        var pet = CreatePet(power: 20);
        var boss = CreateBoss(skillCharge: 1);
        var generation = new ResourceGeneration(
            BaseDamagePool: 0,
            DefensePool: 0,
            HealPool: 0,
            Power: 30,
            ClearedGemCounts: new ClearedGemCounts(0, 0, 0, 3));

        // When: Actor match effects applied for Player
        var (updatedPet, updatedBoss) = ResourceGenerator.ApplyActorMatchEffects(
            DamageParty.Player,
            pet,
            boss,
            generation,
            resolution: default,
            bossSkillChargeRequirement: 5);

        // Then: Pet Power increases (20 + 30 = 50); Boss SkillCharge is UNCHANGED (1)
        Assert.Equal(50, updatedPet.Power);
        Assert.Equal(1, updatedBoss.SkillCharge);
    }

    [Fact]
    public void ApplyActorMatchEffects_BossTurn_ShouldOnlyChargeBossSkillCharge()
    {
        // Given: Boss turn with 2 POWER match groups
        var pet = CreatePet(power: 40);
        var boss = CreateBoss(skillCharge: 2);
        var generation = new ResourceGeneration(
            BaseDamagePool: 0,
            DefensePool: 0,
            HealPool: 0,
            Power: 60,
            ClearedGemCounts: new ClearedGemCounts(0, 0, 0, 6));

        // Create a board with 2 separate horizontal POWER matches
        var board = TestBoard.Background().WithGems(
            (0, GemType.Power), (1, GemType.Power), (2, GemType.Power),
            (16, GemType.Power), (17, GemType.Power), (18, GemType.Power));
        var resolution = CascadeResolver.Resolve(board, Pcg32.FromSeed(1));

        // When: Actor match effects applied for Boss
        var (updatedPet, updatedBoss) = ResourceGenerator.ApplyActorMatchEffects(
            DamageParty.Boss,
            pet,
            boss,
            generation,
            resolution,
            bossSkillChargeRequirement: 5);

        // Then: Pet Power is UNCHANGED (40); Boss SkillCharge increments by +2 (2 + 2 = 4)
        Assert.Equal(40, updatedPet.Power);
        Assert.Equal(4, updatedBoss.SkillCharge);
    }

    [Fact]
    public void BossSkillCharge_ShouldClampToRequirement()
    {
        // Given: Boss with 4 charge, requirement 5, matching 3 POWER match groups
        var boss = CreateBoss(skillCharge: 4);

        // When: 3 POWER match groups added
        var updated = ResourceGenerator.ApplyBossSkillCharge(boss, powerMatchGroups: 3, chargeRequirement: 5);

        // Then: Charge clamps to 5, never overflows (ADR-026 T2)
        Assert.Equal(5, updated.SkillCharge);
    }
}
