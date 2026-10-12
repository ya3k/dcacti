namespace GameServer.Domain.Combat;

using GameServer.Domain.Battle;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;

/// <summary>
/// The result of resolving an ATK-gated attack instance (<c>ADR-026</c> D1, Scope A).
/// </summary>
/// <param name="IsQualifyingAttack">
/// True iff the acting side produced at least one valid ATK match (<c>ClearedGemCounts.Atk > 0</c>).
/// When false, no damage is calculated or dealt, and attack-triggered modifiers are preserved.
/// </param>
/// <param name="DamageResult">
/// The result of <see cref="DamagePipeline.Calculate"/> when <paramref name="IsQualifyingAttack"/> is true;
/// <c>null</c> when <paramref name="IsQualifyingAttack"/> is false.
/// </param>
/// <param name="FinalDamage">
/// The final damage dealt (0 when not a qualifying attack, or when mitigation/shield absorbs all damage).
/// </param>
/// <param name="DefenderHp">The defending target's HP after resolution.</param>
/// <param name="DefenderShieldPool">The defending target's remaining Shield pool after resolution.</param>
/// <param name="RngState">The authoritative PRNG state after resolution.</param>
/// <param name="Events">The damage events produced (<c>DamageCalculated</c>, <c>DamageDealt</c>, <c>DamageTaken</c>).</param>
public readonly record struct AttackResolutionResult(
    bool IsQualifyingAttack,
    DamageResult? DamageResult,
    int FinalDamage,
    int DefenderHp,
    int DefenderShieldPool,
    RngState RngState,
    IReadOnlyList<BattleEvent> Events)
{
    /// <summary>
    /// Creates a non-qualifying attack result when zero ATK gems were matched (<c>ClearedGemCounts.Atk == 0</c>).
    /// </summary>
    public static AttackResolutionResult NonQualifying(int defenderHp, int defenderShieldPool, RngState rngState) =>
        new(
            IsQualifyingAttack: false,
            DamageResult: null,
            FinalDamage: 0,
            DefenderHp: defenderHp,
            DefenderShieldPool: defenderShieldPool,
            RngState: rngState,
            Events: []);
}

/// <summary>
/// ATK-Gated Attack Resolution (<c>ADR-026</c> D1, <c>COMBAT_RULES.md</c> §3, <c>GAME_RULES.md</c> §17).
///
/// <code>
/// ClearedGemCounts.Atk > 0  →  Qualifying Attack  →  DamagePipeline.Calculate  →  Consume Modifiers
/// ClearedGemCounts.Atk == 0 →  No Attack          →  Zero Damage               →  Preserve Modifiers
/// </code>
///
/// <b>Gating Rule:</b> Attack damage is calculated and applied only when the acting side produces
/// a valid ATK match (<c>ClearedGemCounts.Atk > 0</c>). Zero ATK matches deal zero damage, emit zero
/// damage events, and preserve all <c>NextAttack</c> and <c>NextQualifyingAttack</c> modifiers.
///
/// <b>Zero Damage vs Non-Qualifying:</b> If an ATK match qualifies but mitigation or Shield absorption
/// reduces final damage to zero, the attack is still a <b>qualifying attack</b>: damage events are
/// emitted and applicable modifiers are consumed according to established qualifying-attack rules.
/// </summary>
public static class AttackResolution
{
    /// <summary>
    /// Resolves a Player -> Boss attack with ATK-match gating (<c>ADR-026</c> D1).
    /// </summary>
    public static AttackResolutionResult ResolvePlayerAttack(
        PetState petState,
        BossState bossState,
        ClearedGemCounts clearedGems,
        int baseDamagePool,
        int combo,
        RngState rngState,
        ComboModifiers? comboModifiers = null,
        ElementModifiers? elementModifiers = null)
    {
        var defenderShieldPool = StatusEffectLifecycle.ShieldPool(bossState.ActiveStatusEffects);

        if (clearedGems.Atk == 0)
        {
            return AttackResolutionResult.NonQualifying(bossState.HP, defenderShieldPool, rngState);
        }

        var attackInputs = new DamagePipeline.DamageInputs(
            Attack: StatusEffectLifecycle.EffectiveAttack(
                petState.ATK,
                petState.ATKModifiers,
                petState.ActiveStatusEffects),
            BaseDamagePool: baseDamagePool,
            Combo: combo,
            AttackerElement: petState.Element,
            DefenderElement: bossState.Element,
            DefenderDefense: bossState.DEF,
            DefenderHp: bossState.HP,
            Source: DamageParty.Player,
            Target: DamageParty.Boss,
            DefenderShieldPool: defenderShieldPool,
            AttackerCrit: petState.Crit,
            RngState: rngState,
            NextAttackCritContribution: NextAttackCritModifiers.TotalContribution(petState.NextAttackCritModifiers));

        var damageResult = DamagePipeline.Calculate(
            attackInputs,
            comboModifiers ?? ComboModifiers.Default,
            elementModifiers ?? ElementModifiers.Default);

        var events = new List<BattleEvent>(3)
        {
            BattleEvent.ForDamageCalculated(damageResult.Calculation),
            BattleEvent.ForDamageDealt(damageResult.DamageDealt),
            BattleEvent.ForDamageTaken(damageResult.DamageTaken),
        };

        return new AttackResolutionResult(
            IsQualifyingAttack: true,
            DamageResult: damageResult,
            FinalDamage: damageResult.Calculation.FinalDamage,
            DefenderHp: damageResult.TargetHp,
            DefenderShieldPool: damageResult.RemainingShieldPool,
            RngState: damageResult.UpdatedRngState,
            Events: events);
    }

    /// <summary>
    /// Resolves a Boss -> Player attack with ATK-match gating (<c>ADR-026</c> D1).
    /// </summary>
    public static AttackResolutionResult ResolveBossAttack(
        BossState bossState,
        PetState petState,
        ClearedGemCounts clearedGems,
        int baseDamagePool,
        int combo,
        RngState rngState,
        ComboModifiers? comboModifiers = null,
        ElementModifiers? elementModifiers = null)
    {
        var defenderShieldPool = StatusEffectLifecycle.ShieldPool(petState.ActiveStatusEffects);

        if (clearedGems.Atk == 0)
        {
            return AttackResolutionResult.NonQualifying(petState.HP, defenderShieldPool, rngState);
        }

        var attackInputs = new DamagePipeline.DamageInputs(
            Attack: StatusEffectLifecycle.EffectiveBossAttack(
                bossState.ATK,
                bossState.ActiveStatusEffects),
            BaseDamagePool: baseDamagePool,
            Combo: combo,
            AttackerElement: bossState.Element,
            DefenderElement: petState.Element,
            DefenderDefense: petState.DEF,
            DefenderHp: petState.HP,
            Source: DamageParty.Boss,
            Target: DamageParty.Player,
            DefenderShieldPool: defenderShieldPool,
            AttackerCrit: 0,
            RngState: rngState);

        var damageResult = DamagePipeline.Calculate(
            attackInputs,
            comboModifiers ?? ComboModifiers.Default,
            elementModifiers ?? ElementModifiers.Default);

        var events = new List<BattleEvent>(3)
        {
            BattleEvent.ForDamageCalculated(damageResult.Calculation),
            BattleEvent.ForDamageDealt(damageResult.DamageDealt),
            BattleEvent.ForDamageTaken(damageResult.DamageTaken),
        };

        return new AttackResolutionResult(
            IsQualifyingAttack: true,
            DamageResult: damageResult,
            FinalDamage: damageResult.Calculation.FinalDamage,
            DefenderHp: damageResult.TargetHp,
            DefenderShieldPool: damageResult.RemainingShieldPool,
            RngState: damageResult.UpdatedRngState,
            Events: events);
    }

    /// <summary>
    /// Consumes the active Pet's <c>NextAttack</c> modifiers (Crit and ATK) iff a qualifying attack occurred
    /// (<c>COMBAT_RULES.md</c> §3.3 items 8–10, <c>GAME_STATE.md</c> §5.1.2 item 4, §5.1.4 item 4).
    /// </summary>
    public static PetState ConsumePetNextAttackModifiers(PetState petState, bool isQualifyingAttack)
    {
        if (!isQualifyingAttack)
        {
            return petState;
        }

        var critModifiers = petState.NextAttackCritModifiers;
        var atkModifiers = petState.ATKModifiers;

        if (critModifiers.Length == 0 && atkModifiers.Length == 0)
        {
            return petState;
        }

        var consumedIdentities = new string[critModifiers.Length];
        for (var index = 0; index < critModifiers.Length; index++)
        {
            consumedIdentities[index] = critModifiers[index].SourceIdentity;
        }

        return petState with
        {
            NextAttackCritModifiers = NextAttackCritModifiers.Consume(critModifiers, consumedIdentities),
            ATKModifiers = ATKModifiers.ConsumeForQualifyingAttack(atkModifiers),
        };
    }

    /// <summary>
    /// Consumes the Boss's <c>NextQualifyingAttack</c> status effect modifiers iff a qualifying attack occurred
    /// (<c>ADR-026</c> D7).
    /// </summary>
    public static BossState ConsumeBossNextAttackModifiers(BossState bossState, bool isQualifyingAttack)
    {
        if (!isQualifyingAttack)
        {
            return bossState;
        }

        var updatedEffects = StatusEffectLifecycle.ConsumeNextQualifyingAttackModifiers(bossState.ActiveStatusEffects);
        return bossState with { ActiveStatusEffects = updatedEffects };
    }

    /// <summary>
    /// Resolves a Player -> Boss attack, updating defender HP and Shield, consuming Pet modifiers
    /// iff qualifying, and returning the updated states and emitted events.
    /// </summary>
    public static (PetState Pet, BossState Boss, AttackResolutionResult Result) ResolveAndApplyPlayerAttack(
        PetState petState,
        BossState bossState,
        ClearedGemCounts clearedGems,
        int baseDamagePool,
        int combo,
        RngState rngState,
        ComboModifiers? comboModifiers = null,
        ElementModifiers? elementModifiers = null)
    {
        var result = ResolvePlayerAttack(
            petState,
            bossState,
            clearedGems,
            baseDamagePool,
            combo,
            rngState,
            comboModifiers,
            elementModifiers);

        var updatedPet = ConsumePetNextAttackModifiers(petState, result.IsQualifyingAttack);

        var updatedBoss = bossState with { HP = result.DefenderHp };
        if (result.DamageResult is { } damageResult)
        {
            updatedBoss = updatedBoss with
            {
                ActiveStatusEffects = StatusEffectLifecycle.ApplyDamageShieldAbsorption(
                    updatedBoss.ActiveStatusEffects,
                    damageResult.RemainingShieldPool,
                    damageResult.AbsorbedDamage,
                    StatusEffectSource.Boss),
            };
        }

        return (updatedPet, updatedBoss, result);
    }

    /// <summary>
    /// Resolves a Boss -> Player attack, updating defender HP and Shield, consuming Boss modifiers
    /// iff qualifying, and returning the updated states and emitted events.
    /// </summary>
    public static (BossState Boss, PetState Pet, AttackResolutionResult Result) ResolveAndApplyBossAttack(
        BossState bossState,
        PetState petState,
        ClearedGemCounts clearedGems,
        int baseDamagePool,
        int combo,
        RngState rngState,
        ComboModifiers? comboModifiers = null,
        ElementModifiers? elementModifiers = null)
    {
        var result = ResolveBossAttack(
            bossState,
            petState,
            clearedGems,
            baseDamagePool,
            combo,
            rngState,
            comboModifiers,
            elementModifiers);

        var updatedBoss = ConsumeBossNextAttackModifiers(bossState, result.IsQualifyingAttack);

        var updatedPet = petState with { HP = result.DefenderHp };
        if (result.DamageResult is { } damageResult)
        {
            updatedPet = updatedPet with
            {
                ActiveStatusEffects = StatusEffectLifecycle.ApplyDamageShieldAbsorption(
                    updatedPet.ActiveStatusEffects,
                    damageResult.RemainingShieldPool,
                    damageResult.AbsorbedDamage,
                    StatusEffectSource.Player),
            };
        }

        return (updatedBoss, updatedPet, result);
    }
}
