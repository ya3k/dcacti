namespace GameServer.Domain.Combat;

using GameServer.Domain.Battle;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;

/// <summary>
/// The confirmed ATK-match qualification evidence for one attack instance
/// (<c>COMBAT_RULES.md</c> §2 item 6, §3; <c>ADR-026</c> D1; OD-6).
///
/// <code>
/// acting actor's own CascadeResult
///     └── Passes[i].Matches[j].Shape.GemType == GemType.Atk   →  HasConfirmedAtkMatch
/// </code>
///
/// <b>It is derived from match-resolution evidence, never from accounting.</b>
/// The predicate is "at least one detected Match whose Gem type is ATK in the
/// acting actor's OWN resolution" — the same
/// <see cref="CascadeResolver.CascadeResult.Passes"/> the cascade loop already
/// produced, read for its confirmed Matches. It is <b>not</b> derived from
/// <see cref="ResourceGeneration.ClearedGemCounts"/>, from a cleared-cell total,
/// from the ATK damage pool, from a Combo value, or from a Battle Event: those
/// are reporting quantities (<c>COMBAT_RULES.md</c> §2 item 6 "Attack
/// qualification (OD-6)"), and a positive cleared ATK count neither proves nor
/// is required for a confirmed ATK Match.
///
/// <b>It carries the evidence and no gameplay value.</b> Only the boolean
/// qualification travels; the Damage Pipeline's inputs remain the separate
/// values its own steps read (<c>COMBAT_RULES.md</c> §3.1), so passing this
/// value can decide the branch and cannot perturb the calculation.
///
/// <b>It is per-resolution, so it cannot leak across resolutions or actors.</b>
/// It is derived at the call site from the one resolution the acting actor just
/// produced (<c>BattleStateService</c> supplies the Player's and the Boss's own
/// resolutions separately), it is not stored in <c>BattleState</c>,
/// <see cref="PetState"/>, or <see cref="BossState"/>, and it is not serialized.
/// An earlier resolution's qualification is therefore never available to
/// authorize a later attack, and one actor's resolution is never read to
/// qualify another actor's attack (<c>ADR-026</c> item 4).
/// </summary>
/// <param name="HasConfirmedAtkMatch">
/// True iff the acting actor's own resolution contains at least one confirmed
/// valid ATK-gem match. False means no attack may occur.
/// </param>
public readonly record struct AttackQualification(bool HasConfirmedAtkMatch)
{
    /// <summary>
    /// The absence of qualifying evidence — no confirmed ATK-gem match. It is the
    /// value of a resolution that produced none, and the value a caller must
    /// supply when it has no resolution evidence to offer.
    /// </summary>
    public static AttackQualification None => new(HasConfirmedAtkMatch: false);

    /// <summary>
    /// Reads the qualification evidence out of one actor's committed resolution.
    ///
    /// <b>No detection is performed.</b> The confirmed Matches are the ones the
    /// cascade loop already recorded (<see cref="CascadeResolver.CascadeResult"/>,
    /// <c>MATCH3_RULES.md</c> §4.3); this method walks them once in their own
    /// order and answers whether any of them is an ATK Match. It reads no board,
    /// re-resolves nothing, sorts nothing, and derives nothing from a cascade
    /// depth, a cleared-cell count, or a Battle Event. A resolution with no passes
    /// — and the default value of the struct — qualifies nothing.
    /// </summary>
    /// <param name="resolution">
    /// The acting actor's own committed cascade resolution — the Player's for the
    /// Player attack, the Boss's for the Boss attack.
    /// </param>
    public static AttackQualification FromResolution(CascadeResolver.CascadeResult resolution)
    {
        if (resolution.Passes is null)
        {
            return None;
        }

        foreach (var pass in resolution.Passes)
        {
            foreach (var match in pass.Matches)
            {
                if (match.Shape.GemType == GemType.Atk)
                {
                    return new AttackQualification(HasConfirmedAtkMatch: true);
                }
            }
        }

        return None;
    }
}

/// <summary>
/// The result of resolving an ATK-gated attack instance (<c>ADR-026</c> D1, Scope A).
/// </summary>
/// <param name="IsQualifyingAttack">
/// True iff the acting actor's own resolution contains a confirmed valid ATK-gem
/// match (<see cref="AttackQualification"/>).
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
/// <param name="DefenderDefenseBase">
/// The defender's stored base DEF the pipeline's step 5 was given, or <c>0</c>
/// when no damage instance was priced (<c>CombatTrace</c> reporting only —
/// never a gameplay input; <c>COMBAT_RULES.md</c> §3.2).
/// </param>
/// <param name="DefenderDefenseEffective">
/// The effective DEF — base plus every active <c>TargetStat = "DEF"</c> modifier —
/// that the pipeline's step 5 was actually given, or <c>0</c> when no instance
/// was priced. It is the exact value used for mitigation, echoed so telemetry
/// needs no second computation (<c>COMBAT_RULES.md</c> §5.4.6, §5.5.6).
/// </param>
public readonly record struct AttackResolutionResult(
    bool IsQualifyingAttack,
    DamageResult? DamageResult,
    int FinalDamage,
    int DefenderHp,
    int DefenderShieldPool,
    RngState RngState,
    IReadOnlyList<BattleEvent> Events,
    int DefenderDefenseBase = 0,
    int DefenderDefenseEffective = 0)
{
    /// <summary>
    /// Creates a non-qualifying attack result when the acting actor's own resolution
    /// contains no confirmed valid ATK-gem match (<see cref="AttackQualification"/>).
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
/// ATK-Gated Attack Resolution (<c>ADR-026</c> D1, <c>COMBAT_RULES.md</c> §2 item 6
/// and §3, <c>GAME_RULES.md</c> §17).
///
/// <code>
/// confirmed valid ATK match     →  Qualifying Attack  →  DamagePipeline.Calculate  →  Consume Modifiers
/// no confirmed valid ATK match  →  No Attack          →  Zero Damage               →  Preserve Modifiers
/// </code>
///
/// <b>Gating Rule (OD-6).</b> Attack damage is calculated and applied only when the
/// acting actor's own resolution contains at least one confirmed valid ATK-gem
/// match, as carried by <see cref="AttackQualification"/>. The predicate is
/// authoritative match-resolution evidence: a confirmed ATK Match, not a cleared
/// ATK-gem count, not the ATK damage pool, and not another actor's resolution. With
/// no such match the Damage Pipeline does not run, zero damage is dealt, zero damage
/// events are emitted, and all <c>NextAttack</c> / <c>NextQualifyingAttack</c>
/// modifiers are preserved (<c>COMBAT_RULES.md</c> §3, §3.3 item 8).
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
        AttackQualification qualification,
        int baseDamagePool,
        int combo,
        RngState rngState,
        ComboModifiers? comboModifiers = null,
        ElementModifiers? elementModifiers = null)
    {
        var defenderShieldPool = StatusEffectLifecycle.ShieldPool(bossState.ActiveStatusEffects);

        if (!qualification.HasConfirmedAtkMatch)
        {
            return AttackResolutionResult.NonQualifying(bossState.HP, defenderShieldPool, rngState);
        }

        // COMBAT_RULES.md §3.2 / §5.5.6: step 5's mitigation input is the Boss's
        // EFFECTIVE DEF — the stored base plus every active TargetStat = "DEF"
        // BuffDebuff contribution (TASK-299). The stored stat is read, never
        // written. The two values are computed once here and echoed on the result
        // for combat tracing, so telemetry never re-derives what step 5 read.
        var defenderDefenseBase = bossState.DEF;
        var defenderDefenseEffective = StatusEffectLifecycle.EffectiveBossDefense(
            bossState.DEF,
            bossState.ActiveStatusEffects);
        var attackInputs = new DamagePipeline.DamageInputs(
            Attack: StatusEffectLifecycle.EffectiveAttack(
                petState.ATK,
                petState.ATKModifiers,
                petState.ActiveStatusEffects),
            BaseDamagePool: baseDamagePool,
            Combo: combo,
            AttackerElement: petState.Element,
            DefenderElement: bossState.Element,
            DefenderDefense: defenderDefenseEffective,
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
            Events: events,
            DefenderDefenseBase: defenderDefenseBase,
            DefenderDefenseEffective: defenderDefenseEffective);
    }

    /// <summary>
    /// Resolves a Boss -> Player attack with ATK-match gating (<c>ADR-026</c> D1).
    /// </summary>
    public static AttackResolutionResult ResolveBossAttack(
        BossState bossState,
        PetState petState,
        AttackQualification qualification,
        int baseDamagePool,
        int combo,
        RngState rngState,
        ComboModifiers? comboModifiers = null,
        ElementModifiers? elementModifiers = null)
    {
        var defenderShieldPool = StatusEffectLifecycle.ShieldPool(petState.ActiveStatusEffects);

        if (!qualification.HasConfirmedAtkMatch)
        {
            return AttackResolutionResult.NonQualifying(petState.HP, defenderShieldPool, rngState);
        }

        // COMBAT_RULES.md §3.2 / §5.4.6: step 5's mitigation input is the Pet's
        // EFFECTIVE DEF — the stored base plus every active TargetStat = "DEF"
        // BuffDebuff contribution (TASK-299). The Player's DEF-Gem buff is applied
        // at the same Round's step 12/14, before this Boss attack, and is what
        // makes it "one-turn defense". The values are computed once and echoed on
        // the result for combat tracing.
        var defenderDefenseBase = petState.DEF;
        var defenderDefenseEffective = StatusEffectLifecycle.EffectivePetDefense(
            petState.DEF,
            petState.ActiveStatusEffects);
        var attackInputs = new DamagePipeline.DamageInputs(
            Attack: StatusEffectLifecycle.EffectiveBossAttack(
                bossState.ATK,
                bossState.ActiveStatusEffects),
            BaseDamagePool: baseDamagePool,
            Combo: combo,
            AttackerElement: bossState.Element,
            DefenderElement: petState.Element,
            DefenderDefense: defenderDefenseEffective,
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
            Events: events,
            DefenderDefenseBase: defenderDefenseBase,
            DefenderDefenseEffective: defenderDefenseEffective);
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
        AttackQualification qualification,
        int baseDamagePool,
        int combo,
        RngState rngState,
        ComboModifiers? comboModifiers = null,
        ElementModifiers? elementModifiers = null)
    {
        var result = ResolvePlayerAttack(
            petState,
            bossState,
            qualification,
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
        AttackQualification qualification,
        int baseDamagePool,
        int combo,
        RngState rngState,
        ComboModifiers? comboModifiers = null,
        ElementModifiers? elementModifiers = null)
    {
        var result = ResolveBossAttack(
            bossState,
            petState,
            qualification,
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
