using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using Xunit;

namespace GameServer.Application.Tests;

/// <summary>
/// TASK-309 W5 — the four migrated Boss Cards resolved through the real atomic-round
/// pipeline with the provisioned content behind battle creation.
///
/// <code>
/// Rule (BOSS_RULES.md §4.2–§4.4, §6.3, §6.3.1; CARD_RULES.md §1.1/§3/§3.6;
///       GAME_RULES.md §17 steps 18a–18d; GAME_EVENTS.md §2; TASK-309 §1)
///  ↓
/// Scenario (Given a migrated Boss, its provisioned Boss Card and a committed
///           response phase, When the cast resolves, Then the approved cost,
///           cooldown and effect are committed exactly once, and exactly one
///           action resolves)
///  ↓
/// Test
/// </code>
///
/// <b>What is real here.</b> Every scenario drives the production
/// <see cref="BattleStateService"/> over the in-memory active-state double: the
/// real Swap pipeline, the real Boss response phase, the real
/// <c>CardCastExecutor</c> Boss branch, the real status-effect lifecycle and the
/// real event ordering. The content read is the documented
/// <see cref="GameServer.Application.Cards.ICardDefinitionLookup"/> seam, serving
/// <see cref="ProvisionedBossCardContent"/> — the same five <c>BossCard</c> rows the
/// migrations provision.
///
/// <b>The two documented test-side substitutions.</b> The store is the suite's
/// in-memory double (<c>REDIS_STATE.md</c> §1–§4), as everywhere in this project.
/// For the effect scenarios the authored cast-priority score is overridden to
/// <see cref="int.MaxValue"/> so the cast branch is reached deterministically; the
/// selection contract itself is proven separately, through committed rounds, against
/// the real authored score <c>85</c> (see the <c>Selection_*</c> tests below), so no
/// selection contract is weakened to make an effect scenario reachable.
/// </summary>
public class TASK309BossCardEffectRuntimeTests
{
    /// <summary>The owning Player of these battles (<c>GAME_STATE.md</c> §2.8).</summary>
    private static readonly PlayerId Owner = new("player_task309_effect_owner");

    /// <summary>
    /// The active Pet these battles carry (<c>GAME_STATE.md</c> §2.3): Xích Lang's MVP
    /// Element and Passive, at the documented base stats (<c>COMBAT_RULES.md</c> §1.1:
    /// ATK 50, DEF 25, MaxHP 1000).
    /// </summary>
    private static readonly BattleStateService.PetConfiguration Pet =
        new(new PetId("pet_instance_task309_effect"), Element.Hoa, new PassiveId("xich-lang"), PassiveThreshold: 5);

    /// <summary>
    /// The Pet's HP these scenarios pin, so a multi-round observation window is about
    /// the Boss Card's effect rather than about <c>GAME_RULES.md</c> §1.4's terminal
    /// outcome.
    /// </summary>
    private const int ObservationWindowHp = 100_000;

    /// <summary>The approved cast-priority score the four migrated Bosses carry (TASK-309 B-1).</summary>
    private const int ApprovedCastPriorityScore = 85;

    /// <summary>
    /// A migrated Boss fixture: the real MVP definition with only the values a
    /// scenario must control. <c>MaxHP</c>/<c>ATK</c> keep the observation window
    /// non-terminal, and the Passive's Threshold is made unreachable so a triggered
    /// Rage cannot add a second ATK modifier to the effect under test. The Skill's
    /// authored cooldown and the Boss Card's provisioned values are NOT changed — they
    /// are what these tests assert.
    /// </summary>
    private static BossDefinition Migrated(BossDefinition definition) =>
        definition with
        {
            CastPriorityScore = int.MaxValue,
            MaxHP = 1_000_000,
            ATK = 100,
            PassiveDefinition = definition.PassiveDefinition with { Threshold = 100_000 },
        };

    /// <summary>
    /// The same migrated-Boss fixture for the selection scenarios, which must keep the
    /// real authored score <see cref="ApprovedCastPriorityScore"/> so the committed
    /// round exercises the documented threshold rather than a forced branch.
    /// </summary>
    private static BossDefinition MigratedAtTheAuthoredScore(BossDefinition definition) =>
        definition with
        {
            MaxHP = 1_000_000,
            ATK = 100,
            PassiveDefinition = definition.PassiveDefinition with { Threshold = 100_000 },
        };

    // =======================================================================
    // flame-burst — Hỏa Long: cost 40, +50% self ATK, duration 3, cooldown 2
    // =======================================================================

    [Fact]
    public async Task FlameBurst_ShouldSpend40ApplyTheSelfAtkBuffSetCooldown2AndEmitExactlyOneCast()
    {
        // BOSS_RULES.md §4.3/§6.3.1 item 1; CARD_RULES.md §1.1/§3; TASK-309 §1 B-2.
        // The provisioned row is
        // { effectType: AtkBuff, valueType: PercentagePoints, value: 50, duration: 3 }
        // at PowerCost 40, and the ability's authored cooldown is 2 Turns.
        var harness = BossCardTestHarness.CreateProvisioned();
        var boss = Migrated(BossDefinitions.HoaLong);

        const string battleId = "task309-flame-burst-cast";
        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, boss);

        // The active form resolved exactly once, keyed by the ability's SkillId.
        Assert.Equal([ProvisionedBossCardContent.FlameBurstCardId], harness.CardDefinitions.Reads);

        var round = await harness.CastRoundAsync(battleId, created, created.BossState with { Power = 100, SkillCooldown = 0 }, Surviving(created.PetState));

        var committed = round.State;
        var events = round.Events;

        // Cost 40, deducted once: the ONLY Boss Power change is the authored spend
        // (a cast emits no PowerChanged for the Boss's own resource).
        Assert.Equal(100 - ProvisionedBossCardContent.FlameBurstPowerCost, committed.BossState.Power);

        // The authored cooldown set in the same write-back.
        Assert.Equal(2, committed.BossState.SkillCooldown);
        Assert.Equal(2, BossDefinitions.HoaLong.SkillCooldownTurns);

        // The provisioned effect: one Boss-sourced Turn-based BuffDebuff on the
        // Boss's own ActiveStatusEffects with TargetStat "ATK" and +50%.
        var buff = Assert.Single(
            committed.BossState.ActiveStatusEffects,
            e => e.Id == ProvisionedBossCardContent.FlameBurstCardId);

        Assert.Equal(StatusEffectType.BuffDebuff, buff.Type);
        Assert.Equal(StatusEffectSource.Boss, buff.Source);
        Assert.Equal("ATK", buff.TargetStat);
        Assert.Equal(ProvisionedBossCardContent.FlameBurstAtkPercent, buff.Magnitude);
        Assert.Null(buff.ExpiryCondition);

        // Duration 3 applied at 18c, one Turn consumed at that same round's step 19a
        // (GAME_STATE.md §5.1.1 items 2–3; COMBAT_RULES.md §5.3 DR2) — the committed
        // value is therefore 2, and the provisioned duration is the authored 3.
        Assert.Equal(ProvisionedBossCardContent.TimedAtkModifierDuration - 1, buff.RemainingTurns);
        Assert.Equal(
            ProvisionedBossCardContent.TimedAtkModifierDuration,
            ProvisionedBossCardContent.FlameBurst().EffectDefinition[0].Duration);

        // The buff's documented consumption point: the Boss attack's Step-1 ATK input
        // (COMBAT_RULES.md §5.5.1), 100 × 150% = 150. The base stat is never written.
        Assert.Equal(150, StatusEffectLifecycle.EffectiveBossAttack(boss.ATK, committed.BossState.ActiveStatusEffects));
        Assert.Equal(100, committed.BossState.ATK);

        // Exactly one BossCardCast, carrying the two documented identities, and it is
        // the last item of a cast-selected phase.
        var cast = Assert.Single(events, e => e.Type == BattleEventType.BossCardCast);
        Assert.Equal(ProvisionedBossCardContent.FlameBurstCardId, cast.BossCardCast.CardId);
        Assert.Equal(boss.BossId.Value, cast.BossCardCast.SourceId);
        Assert.Equal(BattleEventType.BossCardCast, events[^1].Type);

        // One form per ability: the migrated Boss never emits the legacy event.
        Assert.DoesNotContain(events, e => e.Type == BattleEventType.BossSkillCast);

        // A cast phase suppresses the Boss board action and its §18d attack.
        AssertCastPhaseSuppressesTheBossBoardAction(events);

        // The Boss effect is self-only: nothing is written onto the Pet.
        Assert.DoesNotContain(committed.PetState.ActiveStatusEffects, e => e.Source == StatusEffectSource.Boss);
    }

    // =======================================================================
    // root — Mộc Yêu: cost 35, active Pet ATK -30%, duration 3, cooldown 2
    // =======================================================================

    [Fact]
    public async Task Root_ShouldSpend35ApplyThePetAtkDebuffSetCooldown2AndEmitExactlyOneCast()
    {
        // BOSS_RULES.md §6.3.1 item 3; CARD_RULES.md §1.1/§3; TASK-309 §1 B-3. The
        // provisioned row is
        // { effectType: AtkDebuff, valueType: PercentagePoints, value: 30, duration: 3 }
        // at PowerCost 35, and the ability's authored cooldown is 2 Turns.
        var harness = BossCardTestHarness.CreateProvisioned();
        var boss = Migrated(BossDefinitions.MocYeu);

        const string battleId = "task309-root-cast";
        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, boss);

        Assert.Equal([ProvisionedBossCardContent.RootCardId], harness.CardDefinitions.Reads);

        var round = await harness.CastRoundAsync(battleId, created, created.BossState with { Power = 100, SkillCooldown = 0 }, Surviving(created.PetState));

        var committed = round.State;
        var events = round.Events;

        Assert.Equal(100 - ProvisionedBossCardContent.RootPowerCost, committed.BossState.Power);
        Assert.Equal(2, committed.BossState.SkillCooldown);
        Assert.Equal(2, BossDefinitions.MocYeu.SkillCooldownTurns);

        // The provisioned effect: one Boss-sourced Turn-based BuffDebuff on the ACTIVE
        // PET (opponent effect), TargetStat "ATK", magnitude 30, duration 3.
        var root = Assert.Single(
            committed.PetState.ActiveStatusEffects,
            e => e.Id == ProvisionedBossCardContent.RootCardId);

        Assert.Equal(StatusEffectType.BuffDebuff, root.Type);
        Assert.Equal(StatusEffectSource.Boss, root.Source);
        Assert.Equal("ATK", root.TargetStat);
        Assert.Equal(ProvisionedBossCardContent.RootAtkPercent, root.Magnitude);
        Assert.Null(root.ExpiryCondition);

        // Duration 3 applied at 18c, one Turn consumed at that round's step 19a.
        Assert.Equal(ProvisionedBossCardContent.TimedAtkModifierDuration - 1, root.RemainingTurns);
        Assert.Equal(
            ProvisionedBossCardContent.TimedAtkModifierDuration,
            ProvisionedBossCardContent.Root().EffectDefinition[0].Duration);

        // The Pet-side consumption point (COMBAT_RULES.md §5.6.6 item 2): the authored
        // magnitude maps to -|30|, so ATK 50 composes to 35. The base stat is untouched.
        Assert.Equal(50, committed.PetState.ATK);
        Assert.Equal(
            35,
            StatusEffectLifecycle.EffectiveAttack(
                committed.PetState.ATK,
                committed.PetState.ATKModifiers,
                committed.PetState.ActiveStatusEffects));

        // Nothing is written onto the Boss: root is an opponent effect.
        Assert.DoesNotContain(committed.BossState.ActiveStatusEffects, e => e.Id == ProvisionedBossCardContent.RootCardId);

        Assert.Single(events, e => e.Type == BattleEventType.BossCardCast);
        Assert.Equal(BattleEventType.BossCardCast, events[^1].Type);
        Assert.DoesNotContain(events, e => e.Type == BattleEventType.BossSkillCast);
        AssertCastPhaseSuppressesTheBossBoardAction(events);
    }

    // =======================================================================
    // drain-power — Thủy Ma: cost 25, -20 flat Pet Power, floor 0, cooldown 3
    // =======================================================================

    [Fact]
    public async Task DrainPower_ShouldSpend25Drain20PetPowerEmitOnePowerChangedBeforeTheCastAndGainTheBossNothing()
    {
        // BOSS_RULES.md §6.3.1 item 2; GAME_EVENTS.md §2 item 4; TASK-309 §1 and
        // D-1. The provisioned row is
        // { effectType: PowerDrain, valueType: Flat, value: 20 } at PowerCost 25, no
        // duration and no scope (an instant reduction creates no StatusEffect), and
        // the ability's authored cooldown is 3 Turns.
        var harness = BossCardTestHarness.CreateProvisioned();
        var boss = Migrated(BossDefinitions.ThuyMa);

        const string battleId = "task309-drain-power-cast";
        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, boss);

        Assert.Equal([ProvisionedBossCardContent.DrainPowerCardId], harness.CardDefinitions.Reads);

        var round = await harness.CastRoundAsync(battleId, created, created.BossState with { Power = 60, SkillCooldown = 0 }, Surviving(created.PetState with { Power = 30 }));

        var committed = round.State;
        var events = round.Events;

        // Cost 25 from the BOSS's own resource; the effect transfers nothing to it.
        Assert.Equal(60 - ProvisionedBossCardContent.DrainPowerCost, committed.BossState.Power);
        Assert.Equal(3, committed.BossState.SkillCooldown);
        Assert.Equal(3, BossDefinitions.ThuyMa.SkillCooldownTurns);

        // The effect: exactly 20 flat from the ACTIVE PET's Power.
        Assert.Equal(10, committed.PetState.Power);

        // One authoritative PowerChanged, owned by the stage that performed the
        // mutation (source = Boss) and carrying the applied signed delta and the
        // resulting value — the ONLY such event this cast produces.
        var change = Assert.Single(
            events,
            e => e.Type == BattleEventType.PowerChanged && e.PowerChanged.Source == PowerChangeSource.Boss);

        Assert.Equal(-ProvisionedBossCardContent.DrainPowerAmount, change.PowerChanged.Delta);
        Assert.Equal(10, change.PowerChanged.Power);

        // It is carried in the round's single ordered batch BEFORE the BossCardCast
        // item (TASK-309 W5; GAME_EVENTS.md §2 as amended by TASK-309).
        var changeIndex = IndexOf(
            events,
            e => e.Type == BattleEventType.PowerChanged && e.PowerChanged.Source == PowerChangeSource.Boss);
        var castIndex = IndexOf(events, e => e.Type == BattleEventType.BossCardCast);

        Assert.True(changeIndex >= 0);
        Assert.True(castIndex > changeIndex, $"PowerChanged ({changeIndex}) must precede BossCardCast ({castIndex}).");

        // The Boss's own Power spend emits no PowerChanged: the single Boss-sourced
        // event is the Pet drain above, and no event reports the 25 the Boss spent.
        Assert.Single(events, e => e.Type == BattleEventType.PowerChanged && e.PowerChanged.Source == PowerChangeSource.Boss);
        Assert.DoesNotContain(
            events,
            e => e.Type == BattleEventType.PowerChanged && e.PowerChanged.Power == committed.BossState.Power);

        // It creates no StatusEffect instance (GAME_STATE.md §2.3.1 item 9).
        Assert.DoesNotContain(committed.PetState.ActiveStatusEffects, e => e.Source == StatusEffectSource.Boss);
        Assert.DoesNotContain(committed.BossState.ActiveStatusEffects, e => e.Id == ProvisionedBossCardContent.DrainPowerCardId);

        Assert.Single(events, e => e.Type == BattleEventType.BossCardCast);
        Assert.Equal(BattleEventType.BossCardCast, events[^1].Type);
        Assert.DoesNotContain(events, e => e.Type == BattleEventType.BossSkillCast);
        AssertCastPhaseSuppressesTheBossBoardAction(events);
    }

    [Fact]
    public async Task DrainPower_ShouldClampTheReductionAtZeroWhenThePetHasLessThan20Power()
    {
        // GAME_RULES.md §12's 0–100 range and BOSS_RULES.md §6.3.1 item 2's
        // `max(0, Power - 20)`: a requested 20 against 10 applies the 10 that exists,
        // and the event reports the CLAMPED signed change (GAME_EVENTS.md §2 item 1).
        var harness = BossCardTestHarness.CreateProvisioned();
        var boss = Migrated(BossDefinitions.ThuyMa);

        const string battleId = "task309-drain-power-floor";
        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var round = await harness.CastRoundAsync(battleId, created, created.BossState with { Power = 100, SkillCooldown = 0 }, Surviving(created.PetState with { Power = 10 }));

        Assert.Equal(0, round.State.PetState.Power);

        var change = Assert.Single(
            round.Events,
            e => e.Type == BattleEventType.PowerChanged && e.PowerChanged.Source == PowerChangeSource.Boss);

        Assert.Equal(-10, change.PowerChanged.Delta);
        Assert.Equal(0, change.PowerChanged.Power);
    }

    [Fact]
    public async Task DrainPower_ShouldEmitNoPowerChangedWhenThePetIsAlreadyAtZero()
    {
        // GAME_EVENTS.md §2 item 1: a mutation that does not occur emits nothing. The
        // drain is fully absorbed by the floor, so no PowerChanged is produced while
        // the cast itself still resolves exactly once.
        var harness = BossCardTestHarness.CreateProvisioned();
        var boss = Migrated(BossDefinitions.ThuyMa);

        const string battleId = "task309-drain-power-at-zero";
        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var round = await harness.CastRoundAsync(battleId, created, created.BossState with { Power = 100, SkillCooldown = 0 }, Surviving(created.PetState with { Power = 0 }));

        Assert.Equal(0, round.State.PetState.Power);
        Assert.DoesNotContain(
            round.Events,
            e => e.Type == BattleEventType.PowerChanged && e.PowerChanged.Source == PowerChangeSource.Boss);
        Assert.Single(round.Events, e => e.Type == BattleEventType.BossCardCast);
    }

    // =======================================================================
    // thunder-strike — Kim Lôi Vương: cost 40, cooldown 0, next qualifying attack
    // =======================================================================

    [Fact]
    public async Task ThunderStrike_ShouldSpend40InstallTheNextQualifyingAttackModifierAndSetCooldown0()
    {
        // BOSS_RULES.md §6.3.1 item 5; CARD_RULES.md §1.1/§3; TASK-309 §1 and D-1.
        // The provisioned row is
        // { effectType: NextAtkBuff, valueType: PercentagePoints, value: 50,
        //   scope: "NextAttack" } at PowerCost 40, and the ability's authored cooldown
        // is 0 Turns.
        var harness = BossCardTestHarness.CreateProvisioned();
        var boss = Migrated(BossDefinitions.KimLoiVuong);

        const string battleId = "task309-thunder-strike-cast";
        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, boss);

        Assert.Equal([ProvisionedBossCardContent.ThunderStrikeCardId], harness.CardDefinitions.Reads);

        var round = await harness.CastRoundAsync(battleId, created, created.BossState with { Power = 100, SkillCooldown = 0 }, Surviving(created.PetState));

        var committed = round.State;

        Assert.Equal(100 - ProvisionedBossCardContent.ThunderStrikePowerCost, committed.BossState.Power);
        Assert.Equal(0, committed.BossState.SkillCooldown);
        Assert.Equal(0, BossDefinitions.KimLoiVuong.SkillCooldownTurns);

        // The trigger-based self modifier: a Boss-sourced BuffDebuff whose expiry is
        // the documented NextQualifyingAttack condition (GAME_STATE.md §2.3.1 item 3,
        // §5.1.1 item 7) — so it carries no Turn countdown at all.
        var modifier = Assert.Single(
            committed.BossState.ActiveStatusEffects,
            e => e.Id == ProvisionedBossCardContent.ThunderStrikeCardId);

        Assert.Equal(StatusEffectType.BuffDebuff, modifier.Type);
        Assert.Equal(StatusEffectSource.Boss, modifier.Source);
        Assert.Equal("ATK", modifier.TargetStat);
        Assert.Equal(ProvisionedBossCardContent.ThunderStrikeAtkPercent, modifier.Magnitude);
        Assert.Equal(StatusEffect.NextQualifyingAttackCondition, modifier.ExpiryCondition);
        Assert.Null(modifier.RemainingTurns);

        Assert.Single(round.Events, e => e.Type == BattleEventType.BossCardCast);
        Assert.Equal(BattleEventType.BossCardCast, round.Events[^1].Type);
        Assert.DoesNotContain(round.Events, e => e.Type == BattleEventType.BossSkillCast);
        AssertCastPhaseSuppressesTheBossBoardAction(round.Events);
    }

    [Fact]
    public async Task ThunderStrike_ShouldSurviveUntilTheNextQualifyingBossAttackAndBeConsumedExactlyOnce()
    {
        // ADR-026 D7 / BOSS_RULES.md §6.3.1 item 5 / COMBAT_RULES.md §3.3 item 8: the
        // modifier survives every Turn without a qualifying attack and is consumed by
        // the FIRST qualifying one — the ATK-gated §18d instance — exactly once.
        //
        // The attack fixture pins a deterministic board the Boss will act on with a
        // confirmed ATK match (production BossAiEngine selection), and the Player's
        // Swap clears no ATK Gem, so the round's only attack instance is the Boss's.
        var attackFixture = BossAttackRoundFixture.Value;

        var harness = BossCardTestHarness.CreateProvisioned();
        var boss = Migrated(BossDefinitions.KimLoiVuong);

        const string battleId = "task309-thunder-strike-consumption";
        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var castRound = await harness.CastRoundAsync(battleId, created, created.BossState with { Power = 100, SkillCooldown = 0 }, Surviving(created.PetState));

        // The cast phase suppresses 18d, so the modifier is still active at commit.
        Assert.Single(
            castRound.State.BossState.ActiveStatusEffects,
            e => e.Id == ProvisionedBossCardContent.ThunderStrikeCardId);

        // The next round: the cast is unaffordable (Power 0), so the documented
        // selection resolves the Boss's board action, whose ATK match consumes the
        // modifier.
        var afterCast = castRound.State;
        var consumingRound = await harness.ExecuteFixtureRoundAsync(battleId, attackFixture, afterCast.BossState with { Power = 0 }, afterCast.PetState);

        Assert.True(attackFixture.BossAttackQualifies);
        Assert.True(RoundTestSupport.BossClearedAtkGem(consumingRound));

        var consumingCalculation = Assert.Single(
            consumingRound.Events,
            e => e.Type == BattleEventType.DamageCalculated);

        // COMBAT_RULES.md §3.4: Base = EffectiveBossATK + the Boss's own ATK-Gem pool.
        // EffectiveBossATK with the modifier is 100 × 150% = 150
        // (COMBAT_RULES.md §5.5.1) — the fixture's production cascade gives the pool.
        Assert.Equal(
            150 + attackFixture.BossResources.BaseDamagePool,
            consumingCalculation.DamageCalculated.Base);

        // Consumed exactly once: absent from the committed state, and the next
        // qualifying attack resolves at the unbuffed 100 without it.
        Assert.DoesNotContain(
            consumingRound.State.BossState.ActiveStatusEffects,
            e => e.Id == ProvisionedBossCardContent.ThunderStrikeCardId);

        var laterRound = await harness.ExecuteFixtureRoundAsync(battleId, attackFixture, consumingRound.State.BossState with { Power = 0 }, consumingRound.State.PetState);

        var laterCalculation = Assert.Single(laterRound.Events, e => e.Type == BattleEventType.DamageCalculated);

        Assert.Equal(
            100 + attackFixture.BossResources.BaseDamagePool,
            laterCalculation.DamageCalculated.Base);
    }

    // =======================================================================
    // Selection — the real authored score 85, through committed rounds
    // =======================================================================

    [Fact]
    public async Task Selection_WithTheAuthoredScore85_ShouldCastWhenTheBoardScoresStrictlyBelow85()
    {
        // BOSS_RULES.md §4.3 steps 5–6 / TASK-309 B-1: a Boss Card is selected only
        // when CastPriorityScore STRICTLY out-scores the board action. The fixture is a
        // real generated board whose Boss-selected candidate scores below 85, and the
        // round runs with the authored score (not a test override).
        Assert.Equal(ApprovedCastPriorityScore, BossDefinitions.HoaLong.CastPriorityScore);

        var boardBelowThreshold = BoardBelowThresholdFixture.Value;

        Assert.True(
            boardBelowThreshold.Selection.Score < ApprovedCastPriorityScore,
            $"fixture board score {boardBelowThreshold.Selection.Score} must be below {ApprovedCastPriorityScore}");

        var harness = BossCardTestHarness.CreateProvisioned();
        var boss = MigratedAtTheAuthoredScore(BossDefinitions.HoaLong);

        const string battleId = "task309-selection-cast-wins";
        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var round = await harness.ExecuteFixtureRoundAsync(battleId, boardBelowThreshold, created.BossState with { Power = 100, SkillCooldown = 0 }, created.PetState with { HP = ObservationWindowHp, MaxHP = ObservationWindowHp });

        // The cast is the response phase's ONE action, with the authored cost.
        Assert.Single(round.Events, e => e.Type == BattleEventType.BossCardCast);
        Assert.Equal(100 - ProvisionedBossCardContent.FlameBurstPowerCost, round.State.BossState.Power);
        Assert.Equal(BattleEventType.BossCardCast, round.Events[^1].Type);
        AssertCastPhaseSuppressesTheBossBoardAction(round.Events);
    }

    [Fact]
    public async Task Selection_WithTheAuthoredScore85_ShouldGiveATieAt85ToTheBoardAction()
    {
        // BOSS_RULES.md §4.3 step 6: "a tie is resolved in favour of the board match
        // action — a cast must strictly out-score the board action". The fixture is a
        // real generated board whose Boss-selected candidate scores EXACTLY 85, which
        // is what makes the tie boundary observable rather than assumed.
        var tiedBoard = BoardAtThresholdFixture.Value;

        Assert.Equal(ApprovedCastPriorityScore, BossDefinitions.HoaLong.CastPriorityScore);
        Assert.Equal(ApprovedCastPriorityScore, tiedBoard.Selection.Score);

        var harness = BossCardTestHarness.CreateProvisioned();
        var boss = MigratedAtTheAuthoredScore(BossDefinitions.HoaLong);

        const string battleId = "task309-selection-tie";
        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var round = await harness.ExecuteFixtureRoundAsync(battleId, tiedBoard, created.BossState with { Power = 100, SkillCooldown = 0 }, created.PetState with { HP = ObservationWindowHp, MaxHP = ObservationWindowHp });

        // The board action won: no cast, no cost, and the Boss's own board cycle /
        // attack instead.
        Assert.DoesNotContain(round.Events, e => e.Type == BattleEventType.BossCardCast);
        Assert.Equal(100, round.State.BossState.Power);
        Assert.Equal(0, round.State.BossState.SkillCooldown);
        Assert.Contains(
            round.Events,
            e => e.Type == BattleEventType.SwapStarted && e.SwapStarted.Actor == DamageParty.Boss);
    }

    [Fact]
    public async Task Selection_WithTheAuthoredScore85_ShouldResolveTheBoardActionWhenItScoresAbove85()
    {
        // The other side of the same strict comparison: a board candidate scoring above
        // 85 wins, and the migrated Boss resolves its board action — exactly one action,
        // no cast, no cost.
        var boardAboveThreshold = BoardAboveThresholdFixture.Value;

        Assert.True(
            boardAboveThreshold.Selection.Score > ApprovedCastPriorityScore,
            $"fixture board score {boardAboveThreshold.Selection.Score} must be above {ApprovedCastPriorityScore}");

        var harness = BossCardTestHarness.CreateProvisioned();
        var boss = MigratedAtTheAuthoredScore(BossDefinitions.HoaLong);

        const string battleId = "task309-selection-board-wins";
        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var round = await harness.ExecuteFixtureRoundAsync(battleId, boardAboveThreshold, created.BossState with { Power = 100, SkillCooldown = 0 }, created.PetState with { HP = ObservationWindowHp, MaxHP = ObservationWindowHp });

        Assert.DoesNotContain(round.Events, e => e.Type == BattleEventType.BossCardCast);
        Assert.Equal(100, round.State.BossState.Power);
        Assert.Contains(
            round.Events,
            e => e.Type == BattleEventType.SwapStarted && e.SwapStarted.Actor == DamageParty.Boss);

        // Exactly one action per response phase: the Boss's board pair is the only
        // Boss action, and it never both resolves a board cycle and casts.
        Assert.Single(
            round.Events,
            e => e.Type == BattleEventType.SwapStarted && e.SwapStarted.Actor == DamageParty.Boss);
    }

    // =======================================================================
    // Fixtures and helpers
    // =======================================================================

    /// <summary>
    /// A deterministic real-board fixture on which the Boss's own selected board action
    /// scores strictly below the authored 85, so the cast wins selection.
    /// </summary>
    private static readonly Lazy<BossRoundFixture> BoardBelowThresholdFixture =
        new(() => BossRoundFixtures.Find(f => f.Selection.Score < ApprovedCastPriorityScore));

    /// <summary>The same, at the tie boundary: a Boss-selected board score of exactly 85.</summary>
    private static readonly Lazy<BossRoundFixture> BoardAtThresholdFixture =
        new(() => BossRoundFixtures.Find(f => f.Selection.Score == ApprovedCastPriorityScore));

    /// <summary>The same, on the other side: a Boss-selected board score above 85.</summary>
    private static readonly Lazy<BossRoundFixture> BoardAboveThresholdFixture =
        new(() => BossRoundFixtures.Find(f => f.Selection.Score > ApprovedCastPriorityScore));

    /// <summary>
    /// A deterministic real-board fixture on which the Boss's own board action is a
    /// qualifying attack (a confirmed ATK-Gem match) and the Player's Swap clears no
    /// ATK Gem, so the round's only damage instance is the Boss's.
    /// </summary>
    private static readonly Lazy<BossRoundFixture> BossAttackRoundFixture =
        new(() => BossRoundFixtures.Find(f => !f.PlayerClearsAtkGem && f.BossAttackQualifies));

    /// <summary>
    /// The Pet at its documented base stats with a survivable HP pool
    /// (<c>COMBAT_RULES.md</c> §1.1; <c>GAME_RULES.md</c> §1.4's terminal outcome is
    /// not this suite's subject).
    /// </summary>
    private static PetState Surviving(PetState petState) =>
        petState with { HP = ObservationWindowHp, MaxHP = ObservationWindowHp };

    /// <summary>
    /// A cast-selected response phase performs no Boss board action and therefore no
    /// §18d attack: the Player's boundary pair and cycle are the round's only ones
    /// (<c>GAME_EVENTS.md</c> §1.1; <c>BOSS_RULES.md</c> §4.3 item 4).
    /// </summary>
    private static void AssertCastPhaseSuppressesTheBossBoardAction(IReadOnlyList<BattleEvent> events)
    {
        var swapStarted = Assert.Single(events, e => e.Type == BattleEventType.SwapStarted);
        Assert.Equal(DamageParty.Player, swapStarted.SwapStarted.Actor);

        Assert.Single(events, e => e.Type == BattleEventType.SwapResolved);

        // One board cycle — the Player's — and therefore one MatchedGemSummary.
        Assert.Single(events, e => e.Type == BattleEventType.MatchedGemSummary);

        Assert.DoesNotContain(
            events,
            e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Boss);
        Assert.DoesNotContain(
            events,
            e => e.Type == BattleEventType.DamageTaken && e.DamageTaken.Source == DamageParty.Boss);
    }

    private static int IndexOf(IReadOnlyList<BattleEvent> events, Func<BattleEvent, bool> predicate)
    {
        for (var index = 0; index < events.Count; index++)
        {
            if (predicate(events[index]))
            {
                return index;
            }
        }

        return -1;
    }
}
