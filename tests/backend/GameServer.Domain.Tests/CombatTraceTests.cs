using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Pets;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// TASK-321 Domain suite for the authoritative combat-trace inputs
/// (<c>SIGNALR_PROTOCOL.md</c> §3 item 5, §3.3).
///
/// <code>
/// Rule (COMBAT_RULES.md §3.2, §5.4.6, §5.5.6; §4 item 7)
///  ↓
/// Scenario (one damage instance / one heal in the real Domain stages)
///  ↓
/// Test (the trace's inputs are the values the pipeline actually read)
/// </code>
///
/// <b>Why these tests matter.</b> The trace must carry the base DEF, the
/// effective DEF step 5 read, and the Pet heal — without recomputing any of them
/// in telemetry. These tests prove the echoed values are the ones the calculation
/// used: the echoed effective DEF reproduces the returned breakdown exactly
/// through the same public pipeline, so an echo that disagreed with the
/// calculation, or a defensive stat applied twice, fails the equality.
/// </summary>
public class CombatTraceTests
{
    private const ulong TestSeed = 20260816UL;

    private static int I(int row, int column) => TestBoard.I(row, column);

    private static BattleState BaseBattle() => BattleState.CreateWith("battle-0321", TestSeed);

    private static BattleState BattleWith(BoardState board, PetState? pet = null)
    {
        var created = BaseBattle();

        return created with
        {
            BoardState = board,
            PetState = pet ?? created.PetState,
        };
    }

    // =======================================================================
    // 1. AttackResolution echoes the DEF values step 5 actually read
    // =======================================================================

    [Fact]
    public void PlayerAttack_ShouldEchoTheBaseAndEffectiveDefenseUsedForMitigation()
    {
        // COMBAT_RULES.md §3.2 / §5.5.6: step 5 reads the Boss's EFFECTIVE DEF.
        // The stored base is 50 (Hỏa Long); an active DEF buff of +25 makes the
        // effective value 75. A trace must report both, without re-applying either.
        var battle = BaseBattle();

        var defenseBuff = StatusEffect.TurnBased(
            id: StatusEffectLifecycle.DefenseBuffId,
            type: StatusEffectType.BuffDebuff,
            source: StatusEffectSource.Player,
            magnitude: 25,
            duration: 1,
            targetStat: StatusEffectLifecycle.DefenseStat);

        var boss = battle.BossState with
        {
            DEF = 50,
            ActiveStatusEffects = [defenseBuff],
        };

        var result = AttackResolution.ResolvePlayerAttack(
            battle.PetState,
            boss,
            new AttackQualification(HasConfirmedAtkMatch: true),
            baseDamagePool: 0,
            combo: 1,
            rngState: battle.RngState);

        Assert.True(result.IsQualifyingAttack);
        var damage = result.DamageResult!.Value;

        // The echo: base and effective DEF as the pipeline read them.
        Assert.Equal(50, result.DefenderDefenseBase);
        Assert.Equal(75, result.DefenderDefenseEffective);

        // The echo is not a re-derivation: running the same public pipeline with the
        // echoed effective DEF reproduces the returned breakdown exactly, so the DEF
        // was applied once and the formula is unchanged.
        var reference = DamagePipeline.Calculate(
            new DamagePipeline.DamageInputs(
                Attack: StatusEffectLifecycle.EffectiveAttack(
                    battle.PetState.ATK,
                    battle.PetState.ATKModifiers,
                    battle.PetState.ActiveStatusEffects),
                BaseDamagePool: 0,
                Combo: 1,
                AttackerElement: battle.PetState.Element,
                DefenderElement: boss.Element,
                DefenderDefense: result.DefenderDefenseEffective,
                DefenderHp: boss.HP,
                Source: DamageParty.Player,
                Target: DamageParty.Boss,
                DefenderShieldPool: StatusEffectLifecycle.ShieldPool(boss.ActiveStatusEffects),
                AttackerCrit: battle.PetState.Crit,
                RngState: battle.RngState,
                NextAttackCritContribution: NextAttackCritModifiers.TotalContribution(
                    battle.PetState.NextAttackCritModifiers)),
            ComboModifiers.Default,
            ElementModifiers.Default);

        Assert.Equal(reference.Calculation, damage.Calculation);
        Assert.Equal(reference.TargetHp, damage.TargetHp);
    }

    [Fact]
    public void BossAttack_ShouldEchoThePetBaseAndEffectiveDefenseUsedForMitigation()
    {
        // COMBAT_RULES.md §3.2 / §5.4.6: the Boss→Pet instance reads the Pet's
        // EFFECTIVE DEF — the stored 25 plus the Player's one-round DEF-Gem buff.
        var battle = BaseBattle();

        var defenseBuff = StatusEffect.TurnBased(
            id: StatusEffectLifecycle.DefenseBuffId,
            type: StatusEffectType.BuffDebuff,
            source: StatusEffectSource.Player,
            magnitude: 15,
            duration: 1,
            targetStat: StatusEffectLifecycle.DefenseStat);

        var pet = battle.PetState with
        {
            DEF = 25,
            ActiveStatusEffects = [defenseBuff],
        };

        var result = AttackResolution.ResolveBossAttack(
            battle.BossState,
            pet,
            new AttackQualification(HasConfirmedAtkMatch: true),
            baseDamagePool: 0,
            combo: 1,
            rngState: battle.RngState);

        Assert.True(result.IsQualifyingAttack);
        Assert.Equal(25, result.DefenderDefenseBase);
        Assert.Equal(40, result.DefenderDefenseEffective);

        var damage = result.DamageResult!.Value;
        var reference = DamagePipeline.Calculate(
            new DamagePipeline.DamageInputs(
                Attack: StatusEffectLifecycle.EffectiveBossAttack(
                    battle.BossState.ATK,
                    battle.BossState.ActiveStatusEffects),
                BaseDamagePool: 0,
                Combo: 1,
                AttackerElement: battle.BossState.Element,
                DefenderElement: pet.Element,
                DefenderDefense: result.DefenderDefenseEffective,
                DefenderHp: pet.HP,
                Source: DamageParty.Boss,
                Target: DamageParty.Player,
                DefenderShieldPool: StatusEffectLifecycle.ShieldPool(pet.ActiveStatusEffects),
                AttackerCrit: 0,
                RngState: battle.RngState),
            ComboModifiers.Default,
            ElementModifiers.Default);

        Assert.Equal(reference.Calculation, damage.Calculation);
    }

    [Fact]
    public void NonQualifyingAttack_ShouldPriceNoInstanceAndEchoNoDefense()
    {
        var battle = BaseBattle();

        var result = AttackResolution.ResolvePlayerAttack(
            battle.PetState,
            battle.BossState,
            AttackQualification.None,
            baseDamagePool: 10,
            combo: 2,
            rngState: battle.RngState);

        Assert.False(result.IsQualifyingAttack);
        Assert.Null(result.DamageResult);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.DefenderDefenseBase);
        Assert.Equal(0, result.DefenderDefenseEffective);
    }

    // =======================================================================
    // 2. The Pet heal record the Swap carries
    // =======================================================================

    [Fact]
    public void CommittedSwap_ShouldCaptureThePetHealItApplied()
    {
        // COMBAT_RULES.md §4 item 7: no Battle Event reports a heal, so the trace
        // record is the only delivery of it. The executor captures the applied amount
        // at the one place it applies the pool.
        var pet = BaseBattle().PetState with { HP = 800, MaxHP = 1000 };

        var result = SwapExecutor.Execute(
            BattleWith(HpMatch3Board(), pet),
            new SwapRequest(From, To));

        Assert.True(result.IsAccepted);
        Assert.True(result.Resources.HealPool >= 60, "the fixture must generate healing");

        var heal = result.PetHeal;
        Assert.NotNull(heal);
        var value = heal!.Value;

        Assert.Equal(result.Resources.HealPool, value.RawAmount);
        Assert.Equal(result.State.PetState.HP - 800, value.Amount);
        Assert.True(value.Amount > 0, "a Pet below MaxHP must be healed by the pool");
        Assert.Equal(800, value.HpBefore);
        Assert.Equal(result.State.PetState.HP, value.HpAfter);
        Assert.Equal(Math.Min(800 + result.Resources.HealPool, 1000), value.HpAfter);
    }

    [Fact]
    public void CommittedSwap_AtFullHp_ShouldReportTheHealWithZeroAppliedAmount()
    {
        // A heal that restores nothing is still a heal: the pool is reported and the
        // applied amount is an explicit 0 — never omitted, never a positive delta.
        var result = SwapExecutor.Execute(
            BattleWith(HpMatch3Board()),
            new SwapRequest(From, To));

        Assert.True(result.IsAccepted);
        Assert.True(result.Resources.HealPool >= 60, "the fixture must generate healing");

        var heal = result.PetHeal!.Value;
        Assert.Equal(result.Resources.HealPool, heal.RawAmount);
        Assert.Equal(0, heal.Amount);
        Assert.Equal(PetState.DefaultMaxHP, heal.HpBefore);
        Assert.Equal(PetState.DefaultMaxHP, heal.HpAfter);
    }

    [Fact]
    public void CommittedSwap_WithoutHpGems_ShouldCarryNoHealRecord()
    {
        // No heal was attempted, so there is no record to carry — which is exactly
        // how a consumer distinguishes "no heal" from "a heal of 0".
        var board = TestBoard.FromRows(
            "ADHPADHP",
            "DHPADHPA",
            "HPADHPAD",
            "PAHAPADH",
            "AADAPDHP",
            "DHPADHPA",
            "HPADHPAD",
            "PADHPADH");

        var result = SwapExecutor.Execute(BattleWith(board), new SwapRequest(From, To));

        Assert.True(result.IsAccepted);
        Assert.Equal(0, result.Resources.HealPool);
        Assert.Null(result.PetHeal);
    }

    [Fact]
    public void RejectedSwap_ShouldCarryNoHealAndNoTrace()
    {
        var result = SwapExecutor.Execute(
            BattleWith(HpMatch3Board()),
            new SwapRequest(0, 63));

        Assert.True(result.IsRejected);
        Assert.Null(result.PetHeal);
        Assert.Null(result.CombatTrace);
    }

    // =======================================================================
    // 3. The result's factories preserve the trace members
    // =======================================================================

    [Fact]
    public void ResultFactories_ShouldPreserveThePetHealAndTheAttachedTrace()
    {
        // The Application stage composes WithEvents/WithState over the executor's
        // result before it attaches the round's trace; neither factory may drop the
        // heal, and the trace must survive them unchanged.
        var result = SwapExecutor.Execute(
            BattleWith(HpMatch3Board(), BaseBattle().PetState with { HP = 800, MaxHP = 1000 }),
            new SwapRequest(From, To));

        Assert.True(result.IsAccepted);
        Assert.NotNull(result.PetHeal);

        var trace = new CombatTrace(
            BattleId: result.State.BattleId,
            ServerSequence: result.State.Sequence,
            Move: new CombatTraceMove(CombatTraceMoveKind.Swap, From, To),
            Cycles: [],
            Damage: [],
            Healing: result.PetHeal is { } heal ? [heal] : [],
            Pet: new CombatTraceTarget(800, result.State.PetState.HP, result.State.PetState.DEF, 0, result.State.PetState.Power),
            Boss: new CombatTraceTarget(result.State.BossState.HP, result.State.BossState.HP, result.State.BossState.DEF, 0, result.State.BossState.Power));

        var withEvents = result.WithEvents([.. result.Events]);
        Assert.Equal(result.PetHeal, withEvents.PetHeal);

        var withState = withEvents.WithState(result.State);
        Assert.Equal(result.PetHeal, withState.PetHeal);

        var withTrace = withState.WithCombatTrace(trace);
        Assert.Equal(trace, withTrace.CombatTrace);
        Assert.Equal(result.PetHeal, withTrace.PetHeal);
        Assert.Equal(result.State, withTrace.State);
        Assert.Equal(result.Events, withTrace.Events);
    }

    // =======================================================================
    // Fixtures
    // =======================================================================

    private static int From => I(3, 2); // the completing HP Gem
    private static int To => I(4, 2);   // the run's gap

    /// <summary>
    /// A board whose only Match is a horizontal run of exactly three HP Gems on
    /// row 4, completed by swapping the HP Gem at (3,2) into the gap at (4,2).
    /// The same fixture <c>PlayerEffectHealingTests</c> uses, so the heal pool is
    /// the documented 3 × <c>ResourceGenerator.HpGemBaseHeal</c>.
    /// </summary>
    private static BoardState HpMatch3Board() =>
        TestBoard.FromRows(
            "ADHPADHP",
            "DHPADHPA",
            "HPADHPAD",
            "PAHHPADH",
            "HHDPPDHP",
            "DHPADHPA",
            "HPADHPAD",
            "PADHPADH");
}
