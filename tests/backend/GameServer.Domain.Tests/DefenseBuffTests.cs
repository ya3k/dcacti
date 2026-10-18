namespace GameServer.Domain.Tests;

using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using Xunit;

/// <summary>
/// The DEF-Gem buff — a temporary <c>TargetStat = "DEF"</c> stat increase that
/// raises the defender's <b>effective DEF</b> for the opponent's next attack
/// (<c>COMBAT_RULES.md</c> §2, §3.2, §5.4.6, §5.5.6; TASK-299).
///
/// These tests cover the Domain half of the rule: the effective-DEF composition,
/// the instance's effect on the authoritative Damage Pipeline, and the Turn
/// boundary the buff expires at. The end-to-end Round behavior (application by a
/// committed Swap, the opponent's attack, and the step-19a pass) is covered by
/// <c>DefenseBuffLifecycleTests</c> in the Application test project.
/// </summary>
public class DefenseBuffTests
{
    private static PetState CreatePet(int defense = 50, int hp = 1000, int maxHp = 1000) =>
        new(
            PetId: new Pets.PetId("pet-defense-test"),
            Element: Element.Hoa,
            HP: hp,
            MaxHP: maxHp,
            ATK: 100,
            DEF: defense,
            Crit: 0,
            Power: 0,
            PassiveId: new Passives.PassiveId("test-passive"),
            PassiveProgress: Passives.PassiveProgress.AtStart(5));

    private static BossState CreateBoss(int defense = 50, int hp = 5000, int maxHp = 5000) =>
        BossDefinitions.HoaLong.ToInitialState() with
        {
            HP = hp,
            MaxHP = maxHp,
            ATK = 100,
            DEF = defense,
        };

    private static StatusEffect DefBuff(double magnitude, StatusEffectSource source, int duration) =>
        StatusEffect.TurnBased(
            StatusEffectLifecycle.DefenseBuffId,
            StatusEffectType.BuffDebuff,
            source,
            magnitude,
            duration,
            StatusEffectLifecycle.DefenseStat);

    // =======================================================================
    // 1. Effective DEF composition (COMBAT_RULES.md §5.4.6 item 2, §5.5.6)
    // =======================================================================

    [Fact]
    public void EffectivePetDefense_NoActiveBuff_EqualsStoredBase()
    {
        Assert.Equal(50, StatusEffectLifecycle.EffectivePetDefense(50, []));
    }

    [Fact]
    public void EffectivePetDefense_ActiveBuff_RaisesEffectiveValueButNotStoredBase()
    {
        // Given: Pet DEF 50 with an active +15 DEF buff
        var pet = CreatePet(defense: 50);
        var buffed = ResourceGenerator.ApplyDefenseBuff(pet, defensePool: 15);

        // Then: the effective value is 50 + 15, and the stored base stat is
        // untouched (§5.4.4's non-destructive rule applied to DEF).
        Assert.Equal(65, StatusEffectLifecycle.EffectivePetDefense(
            buffed.DEF, buffed.ActiveStatusEffects));
        Assert.Equal(50, buffed.DEF);
        Assert.Equal(pet.DEF, buffed.DEF);
    }

    [Fact]
    public void EffectiveBossDefense_ActiveBuff_RaisesEffectiveValueButNotStoredBase()
    {
        var boss = CreateBoss(defense: 50);
        var buffed = ResourceGenerator.ApplyDefenseBuff(boss, defensePool: 20);

        Assert.Equal(70, StatusEffectLifecycle.EffectiveBossDefense(
            buffed.DEF, buffed.ActiveStatusEffects));
        Assert.Equal(50, buffed.DEF);
    }

    [Fact]
    public void EffectiveDefense_IgnoresShieldsAndAtkModifiers()
    {
        // COMBAT_RULES.md §5.4.6 item 3: selection is by Type + TargetStat only.
        var shield = StatusEffect.TriggerBased(
            "Shield",
            StatusEffectType.Shield,
            StatusEffectSource.Player,
            500,
            StatusEffect.ShieldDepletedCondition);
        var atkRoot = StatusEffect.TurnBased(
            "Root",
            StatusEffectType.BuffDebuff,
            StatusEffectSource.Boss,
            -30,
            2,
            "ATK");

        Assert.Equal(50, StatusEffectLifecycle.EffectivePetDefense(50, [shield, atkRoot]));
    }

    [Fact]
    public void EffectiveDefense_SumsEveryDistinctDefInstance()
    {
        // Two distinct DEF effect identities compose additively (§5.4.6 item 2 —
        // the same source identity refreshes, distinct identities sum).
        var first = StatusEffect.TurnBased(
            StatusEffectLifecycle.DefenseBuffId,
            StatusEffectType.BuffDebuff,
            StatusEffectSource.Player,
            15,
            1,
            StatusEffectLifecycle.DefenseStat);
        var second = StatusEffect.TurnBased(
            "CardDefBuff",
            StatusEffectType.BuffDebuff,
            StatusEffectSource.Player,
            10,
            1,
            StatusEffectLifecycle.DefenseStat);

        Assert.Equal(75, StatusEffectLifecycle.EffectivePetDefense(50, [first, second]));
    }

    // =======================================================================
    // 2. The authoritative Damage Pipeline reads the effective value
    //    (COMBAT_RULES.md §3.2, §5.4.6 item 1, §5.5.6 item 1)
    // =======================================================================

    [Fact]
    public void BossAttack_AgainstBuffedPet_IsReducedThroughTheDamagePipeline()
    {
        // Given: a Boss->Pet attack of 100 pre-defense damage, Pet DEF 50.
        var unbuffedPet = CreatePet(defense: 50);
        var byBoss = CreateBoss();

        // When: the same attack resolves with and without a +15 Pet DEF buff.
        var withoutBuff = AttackResolution.ResolveBossAttack(
            byBoss,
            unbuffedPet,
            new AttackQualification(HasConfirmedAtkMatch: true),
            baseDamagePool: 0,
            combo: 1,
            rngState: default);

        var buffedPet = ResourceGenerator.ApplyDefenseBuff(unbuffedPet, defensePool: 15);
        var withBuff = AttackResolution.ResolveBossAttack(
            byBoss,
            buffedPet,
            new AttackQualification(HasConfirmedAtkMatch: true),
            baseDamagePool: 0,
            combo: 1,
            rngState: default);

        // Then: the mitigation factor uses 100/(100+50) vs 100/(100+65), so the
        // buffed instance is strictly smaller and matches the documented formula.
        Assert.Equal(66, withoutBuff.FinalDamage);   // truncate(100 × 100/150)
        Assert.Equal(60, withBuff.FinalDamage);      // truncate(100 × 100/165)
        Assert.True(withBuff.FinalDamage < withoutBuff.FinalDamage);
        Assert.Equal(1000 - 60, withBuff.DefenderHp);
    }

    [Fact]
    public void PlayerAttack_AgainstBuffedBoss_IsReducedThroughTheDamagePipeline()
    {
        var pet = CreatePet();
        var unbuffedBoss = CreateBoss(defense: 50);

        var withoutBuff = AttackResolution.ResolvePlayerAttack(
            pet,
            unbuffedBoss,
            new AttackQualification(HasConfirmedAtkMatch: true),
            baseDamagePool: 0,
            combo: 1,
            rngState: default);

        var buffedBoss = ResourceGenerator.ApplyDefenseBuff(unbuffedBoss, defensePool: 20);
        var withBuff = AttackResolution.ResolvePlayerAttack(
            pet,
            buffedBoss,
            new AttackQualification(HasConfirmedAtkMatch: true),
            baseDamagePool: 0,
            combo: 1,
            rngState: default);

        Assert.Equal(66, withoutBuff.FinalDamage);   // truncate(100 × 100/150)
        Assert.Equal(58, withBuff.FinalDamage);      // truncate(100 × 100/170)
        Assert.Equal(5000 - 58, withBuff.DefenderHp);
    }

    [Fact]
    public void DefBuff_IsNotAnAbsorptionPool_NoDamageIsShieldAbsorbed()
    {
        // COMBAT_RULES.md §4 item 3 / TASK-299: the DEF buff is not a Shield. The
        // damage reduction is mitigation (step 5), not absorption (step 5's
        // post-pipeline §4 Shield pass), so nothing is absorbed and no Shield pool
        // is reported.
        var pet = ResourceGenerator.ApplyDefenseBuff(CreatePet(defense: 50), defensePool: 40);

        var result = AttackResolution.ResolveBossAttack(
            CreateBoss(),
            pet,
            new AttackQualification(HasConfirmedAtkMatch: true),
            baseDamagePool: 0,
            combo: 1,
            rngState: default);

        Assert.Equal(0, StatusEffectLifecycle.ShieldPool(pet.ActiveStatusEffects));
        Assert.Equal(0, result.DefenderShieldPool);
        Assert.NotNull(result.DamageResult);
        Assert.Equal(0, result.DamageResult!.Value.AbsorbedDamage);
        // The whole mitigated amount reaches HP: 100 × 100/(100+90) = 52.63 → 52.
        Assert.Equal(52, result.FinalDamage);
        Assert.Equal(1000 - 52, result.DefenderHp);
    }

    [Fact]
    public void DefBuff_AndACardShield_AreIndependentMechanisms()
    {
        // COMBAT_RULES.md §4 items 2–5 / §5.4.6: a Shield (here the one a Shield
        // Card grants) still absorbs, and the DEF buff still mitigates. They are not
        // substitutes: the buff changes the number step 5 produces, the Shield is
        // then consumed from that Final Damage, and neither consumes the other.
        var pet = ResourceGenerator.ApplyDefenseBuff(CreatePet(defense: 50), defensePool: 20);
        var withShield = pet with
        {
            ActiveStatusEffects = StatusEffectLifecycle.ApplyShield(
                pet.ActiveStatusEffects,
                StatusEffect.TriggerBased(
                    "Shield",
                    StatusEffectType.Shield,
                    StatusEffectSource.Player,
                    100,
                    StatusEffect.ShieldDepletedCondition)),
        };

        var result = AttackResolution.ResolveBossAttack(
            CreateBoss(),
            withShield,
            new AttackQualification(HasConfirmedAtkMatch: true),
            baseDamagePool: 0,
            combo: 1,
            rngState: default);

        // 100 × 100/(100 + 50 + 20) = 58.82 → 58, fully absorbed by the 100 pool.
        Assert.Equal(58, result.FinalDamage);
        Assert.Equal(1000, result.DefenderHp);
        Assert.Equal(58, result.DamageResult!.Value.AbsorbedDamage);
        Assert.Equal(42, result.DefenderShieldPool);
        // The DEF buff is untouched by absorption: only the Shield pool moved.
        Assert.Contains(withShield.ActiveStatusEffects, e => e.Id == StatusEffectLifecycle.DefenseBuffId);
    }

    [Fact]
    public void DefBuff_SurvivesTheBattleStateSerializationRoundTrip()
    {
        // GAME_STATE.md §2.3.2 item 5 / REDIS_STATE.md: the buff is carried by the
        // existing StatusEffect element, so it must round-trip like any other
        // instance — the Redis write and the client state push depend on it.
        var battle = BattleState.CreateWith("battle-def-buff-round-trip", 20261018UL);
        var pet = ResourceGenerator.ApplyDefenseBuff(battle.PetState, defensePool: 20);
        var original = battle with { PetState = pet };

        var restored = GameServer.Domain.Battle.Serialization.BattleStateSerializer.Deserialize(
            GameServer.Domain.Battle.Serialization.BattleStateSerializer.Serialize(original));

        var buff = Assert.Single(
            restored.PetState.ActiveStatusEffects,
            e => e.Id == StatusEffectLifecycle.DefenseBuffId);
        Assert.Equal(StatusEffectType.BuffDebuff, buff.Type);
        Assert.Equal(StatusEffectSource.Player, buff.Source);
        Assert.Equal(20, buff.Magnitude);
        Assert.Equal(StatusEffectLifecycle.DefenseStat, buff.TargetStat);
        Assert.Equal(ResourceGenerator.PlayerDefBuffDurationTurns, buff.RemainingTurns);
        Assert.True(StatusEffectLifecycle.EffectsEqual(
            original.PetState.ActiveStatusEffects,
            restored.PetState.ActiveStatusEffects));
        Assert.Equal(
            StatusEffectLifecycle.EffectivePetDefense(original.PetState.DEF, original.PetState.ActiveStatusEffects),
            StatusEffectLifecycle.EffectivePetDefense(restored.PetState.DEF, restored.PetState.ActiveStatusEffects));
    }

    // =======================================================================
    // 3. The Turn boundary (COMBAT_RULES.md §5.4.6 duration table, §5.3)
    // =======================================================================

    [Fact]
    public void PetDefBuff_ExpiresAtTheStep19aOfTheTurnItCovers()
    {
        // Given: the Pet rows of §5.4.6's table — applied at step 12/14, the Boss
        // attacks in the same Turn's step 18, step 19a consumes the single Turn.
        var pet = ResourceGenerator.ApplyDefenseBuff(CreatePet(), defensePool: 15);
        var buff = Assert.Single(pet.ActiveStatusEffects);
        Assert.Equal(1, buff.RemainingTurns);

        // Then: active for the Boss attack of the same Turn...
        Assert.Equal(65, StatusEffectLifecycle.EffectivePetDefense(
            pet.DEF, pet.ActiveStatusEffects));

        // ...and gone at that Turn's step 19a, never leaking into the next Turn.
        var afterStep19a = StatusEffectLifecycle.ConsumeAtStep19a(pet.ActiveStatusEffects);
        Assert.Empty(afterStep19a);
        Assert.Equal(50, StatusEffectLifecycle.EffectivePetDefense(pet.DEF, afterStep19a));
    }

    [Fact]
    public void BossDefBuff_SurvivesItsOwnStep19a_AndExpiresAfterTheNextTurn()
    {
        // Given: the Boss rows of §5.4.6's table — applied at step 18a of Turn N,
        // after the Player's attack phase of that Turn.
        var boss = ResourceGenerator.ApplyDefenseBuff(CreateBoss(), defensePool: 20);
        var buff = Assert.Single(boss.ActiveStatusEffects);
        Assert.Equal(2, buff.RemainingTurns);

        // Then: Turn N's step 19a consumes 2 -> 1 and the buff stays active...
        var afterTurnN = StatusEffectLifecycle.ConsumeAtStep19a(boss.ActiveStatusEffects);
        var surviving = Assert.Single(afterTurnN);
        Assert.Equal(1, surviving.RemainingTurns);
        Assert.Equal(70, StatusEffectLifecycle.EffectiveBossDefense(boss.DEF, afterTurnN));

        // ...so the Player's attack in Turn N+1 is mitigated, and that Turn's
        // step 19a consumes 1 -> 0 and removes the instance.
        var afterTurnNPlus1 = StatusEffectLifecycle.ConsumeAtStep19a(afterTurnN);
        Assert.Empty(afterTurnNPlus1);
        Assert.Equal(50, StatusEffectLifecycle.EffectiveBossDefense(boss.DEF, afterTurnNPlus1));
    }

    [Fact]
    public void DefBuff_RefreshResetsDurationAndReplacesMagnitude()
    {
        var pet = ResourceGenerator.ApplyDefenseBuff(CreatePet(), defensePool: 15);
        var afterStep19a = StatusEffectLifecycle.ConsumeAtStep19a(pet.ActiveStatusEffects);
        Assert.Empty(afterStep19a);

        // A re-application creates the instance again with the new magnitude and
        // a full duration — it never accumulates a counter or a magnitude.
        var reapplied = ResourceGenerator.ApplyDefenseBuff(pet, defensePool: 30);
        var buff = Assert.Single(reapplied.ActiveStatusEffects);
        Assert.Equal(30, buff.Magnitude);
        Assert.Equal(1, buff.RemainingTurns);
    }

    [Fact]
    public void BossDefBuff_WhenAlreadyActive_RefreshesDurationAndReplacesMagnitudeWithoutStacking()
    {
        // Given: Boss with an active DEF buff of 20 that has already elapsed 1 turn (remaining = 1)
        var boss = ResourceGenerator.ApplyDefenseBuff(CreateBoss(), defensePool: 20);
        var consumed = boss with
        {
            ActiveStatusEffects = StatusEffectLifecycle.ConsumeAtStep19a(boss.ActiveStatusEffects),
        };
        var initialBuff = Assert.Single(consumed.ActiveStatusEffects);
        Assert.Equal(20, initialBuff.Magnitude);
        Assert.Equal(1, initialBuff.RemainingTurns);

        // When: a subsequent DEF match generates 45
        var refreshed = ResourceGenerator.ApplyDefenseBuff(consumed, defensePool: 45);

        // Then: exactly one instance; magnitude is replaced with 45 (never 20 + 45 = 65),
        // and duration is reset to the full BossDefBuffDurationTurns (2).
        var buff = Assert.Single(refreshed.ActiveStatusEffects);
        Assert.Equal(StatusEffectLifecycle.DefenseBuffId, buff.Id);
        Assert.Equal(45, buff.Magnitude);
        Assert.Equal(ResourceGenerator.BossDefBuffDurationTurns, buff.RemainingTurns);
        Assert.Equal(50 + 45, StatusEffectLifecycle.EffectiveBossDefense(refreshed.DEF, refreshed.ActiveStatusEffects));
    }

    [Fact]
    public void BossDefBuff_AfterTurnNPlus1Expiry_DoesNotProtectTurnNPlus2PlayerAttack()
    {
        // Turn N (step 18a): Boss matches DEF -> applied with duration 2
        var boss = ResourceGenerator.ApplyDefenseBuff(CreateBoss(defense: 50), defensePool: 20);
        // Turn N (step 19a): consumed 2 -> 1
        var turnNStep19a = boss with
        {
            ActiveStatusEffects = StatusEffectLifecycle.ConsumeAtStep19a(boss.ActiveStatusEffects),
        };
        var pet = CreatePet();

        // Turn N+1: Player attacks -> protected by active buff (DEF 50 + 20 = 70)
        var turnNPlus1Attack = AttackResolution.ResolvePlayerAttack(
            pet,
            turnNStep19a,
            new AttackQualification(HasConfirmedAtkMatch: true),
            baseDamagePool: 0,
            combo: 1,
            rngState: default);
        Assert.Equal(58, turnNPlus1Attack.FinalDamage);

        // Turn N+1 (step 19a): consumed 1 -> 0 and removed
        var turnNPlus1Step19a = turnNStep19a with
        {
            ActiveStatusEffects = StatusEffectLifecycle.ConsumeAtStep19a(turnNStep19a.ActiveStatusEffects),
        };
        Assert.Empty(turnNPlus1Step19a.ActiveStatusEffects);

        // Turn N+2: Player attacks -> NO buff active; damage is unmitigated base DEF 50 (66 damage)
        var turnNPlus2Attack = AttackResolution.ResolvePlayerAttack(
            pet,
            turnNPlus1Step19a,
            new AttackQualification(HasConfirmedAtkMatch: true),
            baseDamagePool: 0,
            combo: 1,
            rngState: default);
        Assert.Equal(66, turnNPlus2Attack.FinalDamage);
    }

    [Fact]
    public void CardDamage_AgainstBuffedBoss_IsReducedThroughEffectiveDefense()
    {
        var damageCard = new CardDefinition
        {
            CardDefinitionId = "card-damage",
            Name = "Flat Damage",
            Category = CardCategory.Basic,
            PowerCost = 0,
            LoadoutCopyLimit = 1,
            EffectDefinition = CardEffectDefinitions.Create(
                CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 100)),
        };

        var initial = BattleState.CreateWith("battle-card-def-test", 20261018UL);
        var unbuffedBoss = CreateBoss(defense: 50);
        var stateWithoutBuff = initial with
        {
            BossState = unbuffedBoss,
            PetState = initial.PetState with
            {
                EquippedCards = [new EquippedCardIdentity("card-damage")],
            },
        };

        var withoutBuff = CardCastExecutor.Execute(stateWithoutBuff, damageCard);
        Assert.True(withoutBuff.IsAccepted);

        var buffedBoss = ResourceGenerator.ApplyDefenseBuff(unbuffedBoss, defensePool: 20);
        var stateWithBuff = stateWithoutBuff with { BossState = buffedBoss };

        var withBuff = CardCastExecutor.Execute(stateWithBuff, damageCard);
        Assert.True(withBuff.IsAccepted);

        // Mitigation formula: truncate(100 * 100 / (100 + DEF))
        // Without buff (DEF 50): 100 * 100 / 150 = 66 -> Boss HP: 5000 - 66 = 4934
        // With buff (DEF 70): 100 * 100 / 170 = 58 -> Boss HP: 5000 - 58 = 4942
        Assert.Equal(5000 - 66, withoutBuff.State.BossState.HP);
        Assert.Equal(5000 - 58, withBuff.State.BossState.HP);
        Assert.True(withBuff.State.BossState.HP > withoutBuff.State.BossState.HP);
    }

    [Fact]
    public void EffectiveDefense_ReducesDoTDamageAgainstBuffedTarget()
    {
        // COMBAT_RULES.md §3.2 / §5.4.6 / §5.5.6: step 5 reads effective DEF for all
        // damage instances against the target, including DoT ticks (BattleStateService step 19a).
        var unbuffedInputs = new DamagePipeline.DamageInputs(
            Attack: 100,
            BaseDamagePool: 0,
            Combo: 1,
            AttackerElement: Element.Hoa,
            DefenderElement: Element.Thuy,
            DefenderDefense: StatusEffectLifecycle.EffectiveBossDefense(50, []),
            DefenderHp: 5000,
            Source: DamageParty.Player,
            Target: DamageParty.Boss,
            DefenderShieldPool: 0,
            AttackerCrit: 0,
            RngState: default);

        var buff = DefBuff(20, StatusEffectSource.Boss, 1);
        var buffedInputs = unbuffedInputs with
        {
            DefenderDefense = StatusEffectLifecycle.EffectiveBossDefense(50, [buff]),
        };

        var withoutBuff = DamagePipeline.Calculate(unbuffedInputs, ComboModifiers.Default, ElementModifiers.Default);
        var withBuff = DamagePipeline.Calculate(buffedInputs, ComboModifiers.Default, ElementModifiers.Default);

        // Base 100 * Element 0.75 = 75 pre-mitigation
        // Without buff (DEF 50): truncate(75 * 100 / 150) = 50
        // With buff (DEF 70): truncate(75 * 100 / 170) = 44
        Assert.Equal(50, withoutBuff.Calculation.FinalDamage);
        Assert.Equal(44, withBuff.Calculation.FinalDamage);
        Assert.True(withBuff.Calculation.FinalDamage < withoutBuff.Calculation.FinalDamage);
    }
}
