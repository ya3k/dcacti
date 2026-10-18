namespace GameServer.Domain.Tests;

using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using Xunit;

/// <summary>
/// Tests for actor-relative match effects (DEF stat buff, HP Heal, POWER)
/// (<c>ADR-026</c> Scope B, Scope E; <c>COMBAT_RULES.md</c> §5.4.6, §5.5.6).
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
    // 1. DEF Match -> Temporary DEF stat buff (COMBAT_RULES.md §5.4.6 / §5.5.6)
    // =======================================================================

    [Fact]
    public void ApplyDefenseBuff_PlayerDefMatch_ShouldGrantDefBuffToPet()
    {
        // Given: Pet with no status effect and 50 DefensePool
        var pet = CreatePet();
        Assert.Empty(pet.ActiveStatusEffects);

        // When: the DEF buff is applied
        var updated = ResourceGenerator.ApplyDefenseBuff(pet, defensePool: 50);

        // Then: the Pet gains ONE Turn-based "DefenseBuff" BuffDebuff with
        // TargetStat "DEF", magnitude 50, sourced from the Player, and
        // COMBAT_RULES.md §5.4.6's Pet duration of 1 Turn.
        var buff = Assert.Single(updated.ActiveStatusEffects);
        Assert.Equal(StatusEffectLifecycle.DefenseBuffId, buff.Id);
        Assert.Equal(StatusEffectType.BuffDebuff, buff.Type);
        Assert.Equal(StatusEffectSource.Player, buff.Source);
        Assert.Equal(50, buff.Magnitude);
        Assert.Equal(StatusEffectLifecycle.DefenseStat, buff.TargetStat);
        Assert.Equal(ResourceGenerator.PlayerDefBuffDurationTurns, buff.RemainingTurns);
        Assert.Null(buff.ExpiryCondition);
    }

    [Fact]
    public void ApplyDefenseBuff_PlayerDefMatch_ShouldNotCreateShield()
    {
        // COMBAT_RULES.md §4 item 3 / TASK-299: DEF Gems grant no Shield.
        var updated = ResourceGenerator.ApplyDefenseBuff(CreatePet(), defensePool: 50);

        Assert.DoesNotContain(updated.ActiveStatusEffects, e => e.Type == StatusEffectType.Shield);
        Assert.Equal(0, StatusEffectLifecycle.ShieldPool(updated.ActiveStatusEffects));
    }

    [Fact]
    public void ApplyDefenseBuff_BossDefMatch_ShouldGrantDefBuffToBoss()
    {
        // Given: Boss with no status effect and 75 DefensePool
        var boss = CreateBoss();
        Assert.Empty(boss.ActiveStatusEffects);

        // When: the Boss DEF buff is applied
        var updated = ResourceGenerator.ApplyDefenseBuff(boss, defensePool: 75);

        // Then: the Boss gains the DEF buff sourced from the Boss, with
        // COMBAT_RULES.md §5.4.6's Boss duration of 2 Turns (it must survive its
        // own Turn's step 19a to cover the next Turn's Player attack).
        var buff = Assert.Single(updated.ActiveStatusEffects);
        Assert.Equal(StatusEffectLifecycle.DefenseBuffId, buff.Id);
        Assert.Equal(StatusEffectType.BuffDebuff, buff.Type);
        Assert.Equal(StatusEffectSource.Boss, buff.Source);
        Assert.Equal(75, buff.Magnitude);
        Assert.Equal(StatusEffectLifecycle.DefenseStat, buff.TargetStat);
        Assert.Equal(ResourceGenerator.BossDefBuffDurationTurns, buff.RemainingTurns);
        Assert.DoesNotContain(updated.ActiveStatusEffects, e => e.Type == StatusEffectType.Shield);
    }

    [Fact]
    public void ApplyDefenseBuff_WhenZeroDefensePool_ShouldNotAddBuff()
    {
        var pet = CreatePet();
        var boss = CreateBoss();

        var updatedPet = ResourceGenerator.ApplyDefenseBuff(pet, defensePool: 0);
        var updatedBoss = ResourceGenerator.ApplyDefenseBuff(boss, defensePool: 0);

        Assert.Empty(updatedPet.ActiveStatusEffects);
        Assert.Empty(updatedBoss.ActiveStatusEffects);
    }

    [Fact]
    public void ApplyDefenseBuff_WhenBuffAlreadyActive_ShouldRefreshWithoutStacking()
    {
        // Given: Pet with an active DEF buff of 30
        var pet = ResourceGenerator.ApplyDefenseBuff(CreatePet(), 30);
        Assert.Equal(30, Assert.Single(pet.ActiveStatusEffects).Magnitude);

        // When: a later DEF match yields 60
        var updated = ResourceGenerator.ApplyDefenseBuff(pet, 60);

        // Then: COMBAT_RULES.md §5.4.6's refresh-not-stack rule (§5.2 item 2):
        // exactly one instance, its magnitude SET to 60 — never 30 + 60 — and its
        // duration reset to the applied duration.
        var buff = Assert.Single(updated.ActiveStatusEffects);
        Assert.Equal(60, buff.Magnitude);
        Assert.Equal(ResourceGenerator.PlayerDefBuffDurationTurns, buff.RemainingTurns);
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
