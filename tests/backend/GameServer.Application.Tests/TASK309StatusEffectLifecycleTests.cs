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
/// TASK-309 V-2 — the Status Effect lifecycle gates for the two Turn-based Boss Card
/// effects, and the preserved multiplicative ATK stacking.
///
/// <code>
/// Rule (GAME_STATE.md §5.1.1; COMBAT_RULES.md §5.3 DR2/DR5, §5.5.1, §5.5.5,
///       §5.6.6 item 2; BOSS_RULES.md §6.2.1, §6.3.1 items 1 and 3;
///       GAME_RULES.md §17 step 19a; TASK-309 §1 B-2/B-3/B-4)
///  ↓
/// Scenario (Given a committed cast of `flame-burst` / `root` with duration 3,
///           When rounds resolve — including a round with no qualifying attack in
///           the window — Then exactly one Turn is consumed at each round's 19a
///           step, the effect influences at most two qualifying attacks, and it is
///           removed in the commit where its counter reaches 0)
///  ↓
/// Test
/// </code>
///
/// <b>Why "at most two".</b> Duration 3 is applied at step 18c, and the same Round's
/// step 19a consumes one Turn. The remaining 2 Turns are live during the next two
/// Rounds' step 18d, so at most two qualifying attacks can observe the effect; the
/// third Round's step 19a removes it. Nothing in this file assumes both attacks
/// occur — <c>flame-burst</c>/<c>root</c> guarantee no attack count
/// (<c>BOSS_RULES.md</c> §6.3.1; TASK-309 §1 B-2/B-3) — it asserts the window and
/// the expiry.
///
/// <b>What is real here.</b> Every value below is read from the committed state or
/// from the round's own <c>DamageCalculated</c> reports, produced by the production
/// <see cref="BattleStateService"/>, <c>CardCastExecutor</c>,
/// <see cref="StatusEffectLifecycle"/> and <c>AttackResolution</c>. The attack
/// opportunities are crafted deterministic boards chosen from production output
/// (<see cref="BossRoundFixtures"/>); the Status Effect application itself is always
/// the one the cast performed, never a test-constructed instance.
/// </summary>
public class TASK309StatusEffectLifecycleTests
{
    private static readonly PlayerId Owner = new("player_task309_lifecycle_owner");

    /// <summary>
    /// The active Pet (<c>GAME_STATE.md</c> §2.3): Xích Lang's Element and Passive at
    /// the documented base stats — ATK 50, the value <c>root</c>'s −30% composes
    /// against (<c>COMBAT_RULES.md</c> §1.1, §5.6.6 item 2).
    /// </summary>
    private static readonly BattleStateService.PetConfiguration Pet =
        new(new PetId("pet_instance_task309_lifecycle"), Element.Hoa, new PassiveId("xich-lang"), PassiveThreshold: 5);

    /// <summary>
    /// The Pet's HP these scenarios pin so a multi-round window is about the effect
    /// rather than about <c>GAME_RULES.md</c> §1.4's terminal outcome.
    /// </summary>
    private const int ObservationWindowHp = 100_000;

    /// <summary>The Pet's documented base ATK (<c>COMBAT_RULES.md</c> §1.1).</summary>
    private const int PetBaseAtk = 50;

    /// <summary>The Pet's ATK while <c>root</c>'s authored −30% is active.</summary>
    private const int PetAtkUnderRoot = 35;

    /// <summary>The Boss's ATK fixture (<c>COMBAT_RULES.md</c> §1.1: ATK 100).</summary>
    private const int BossAtk = 100;

    /// <summary>The Boss's ATK while <c>flame-burst</c>'s +50% is active.</summary>
    private const int BossAtkUnderFlameBurst = 150;

    /// <summary>
    /// The stacking proof's value: rage +20% and flame-burst +50% compose
    /// multiplicatively to 1.20 × 1.50 = 1.80 (<c>COMBAT_RULES.md</c> §5.5.1; TASK-309
    /// §1 B-4), so ATK 100 becomes 180 and never the additive 170.
    /// </summary>
    private const int BossAtkUnderRageAndFlameBurst = 180;

    // =======================================================================
    // flame-burst — duration 3, at most two qualifying Boss attacks
    // =======================================================================

    [Fact]
    public async Task FlameBurst_ShouldAffectAtMostTwoQualifyingBossAttacksAndExpireAtStep19a()
    {
        // TASK-309 §1 B-2: duration 3 yields at most two buffed attack opportunities
        // under the existing lifecycle; no guarantee that both occur. The BuffDebuff is
        // applied at 18c (duration 3), its first 19a consumption runs in the same
        // Round, and the counters below are read from each committed state.
        var harness = BossCardTestHarness.CreateProvisioned();
        var boss = WithoutRage(BossDefinitions.HoaLong);

        const string battleId = "task309-lifecycle-flame-burst-window";
        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, boss);
        var petState = Surviving(created.PetState);

        // Round 1 — the cast. Duration 3 applied at 18c; step 19a consumes one Turn.
        var castRound = await harness.CastRoundAsync(
            battleId,
            created,
            created.BossState with { Power = 100, SkillCooldown = 0 },
            petState);

        Assert.Equal(2, FlameBurst(castRound.State).RemainingTurns);

        // Round 2 — a qualifying Boss attack. The DC order is by Round, so this
        // fixture's only damage instance is the Boss's.
        var attackFixture = BossAttackRoundFixture.Value;

        var secondRound = await harness.ExecuteFixtureRoundAsync(
            battleId,
            attackFixture,
            castRound.State.BossState with { Power = 0 },
            castRound.State.PetState);

        Assert.True(attackFixture.BossAttackQualifies);
        Assert.Equal(
            BossAtkUnderFlameBurst + attackFixture.BossResources.BaseDamagePool,
            Assert.Single(secondRound.Events, e => e.Type == BattleEventType.DamageCalculated)
                .DamageCalculated.Base);

        // The attack above ran at 18d with the 2-Turn instance live; step 19a then
        // consumed the second Turn.
        Assert.Equal(1, FlameBurst(secondRound.State).RemainingTurns);

        // Round 3 — the second and last buffed attack. Its 19a consumption reaches 0,
        // so the instance is removed in the same commit (GAME_STATE.md §5.1.1 items 4–5).
        var thirdRound = await harness.ExecuteFixtureRoundAsync(
            battleId,
            attackFixture,
            secondRound.State.BossState with { Power = 0 },
            secondRound.State.PetState);

        Assert.Equal(
            BossAtkUnderFlameBurst + attackFixture.BossResources.BaseDamagePool,
            Assert.Single(thirdRound.Events, e => e.Type == BattleEventType.DamageCalculated)
                .DamageCalculated.Base);

        Assert.DoesNotContain(
            thirdRound.State.BossState.ActiveStatusEffects,
            e => e.Id == ProvisionedBossCardContent.FlameBurstCardId);

        // Round 4 — the window is over: the same qualifying attack resolves at the
        // unbuffed ATK, which is what makes "at most two" observable rather than
        // assumed.
        var fourthRound = await harness.ExecuteFixtureRoundAsync(
            battleId,
            attackFixture,
            thirdRound.State.BossState with { Power = 0 },
            thirdRound.State.PetState);

        Assert.Equal(
            BossAtk + attackFixture.BossResources.BaseDamagePool,
            Assert.Single(fourthRound.Events, e => e.Type == BattleEventType.DamageCalculated)
                .DamageCalculated.Base);

        Assert.DoesNotContain(
            fourthRound.State.BossState.ActiveStatusEffects,
            e => e.Id == ProvisionedBossCardContent.FlameBurstCardId);
    }

    [Fact]
    public async Task FlameBurst_ShouldExpireAtTheCorrectStepWhenTheWindowContainsNoQualifyingAttack()
    {
        // GAME_RULES.md §17 step 19a's once-per-resolved-Turn consumption and
        // GAME_STATE.md §5.1.1 item 2: a Round in which the Boss produces no qualifying
        // attack still consumes one Turn. The effect therefore expires on schedule, not
        // "after two attacks".
        var harness = BossCardTestHarness.CreateProvisioned();
        var boss = WithoutRage(BossDefinitions.HoaLong);

        const string battleId = "task309-lifecycle-flame-burst-no-attack";
        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, boss);
        var petState = Surviving(created.PetState);

        var castRound = await harness.CastRoundAsync(
            battleId,
            created,
            created.BossState with { Power = 100, SkillCooldown = 0 },
            petState);

        Assert.Equal(2, FlameBurst(castRound.State).RemainingTurns);

        // Round 2 — a committed Round with NO qualifying Boss attack: the Boss's
        // deterministic board action clears no ATK Gem and the Player's Swap clears no
        // ATK Gem, so no damage instance exists.
        var noAttackFixture = BossNoAttackRoundFixture.Value;

        var noAttackRound = await harness.ExecuteFixtureRoundAsync(
            battleId,
            noAttackFixture,
            castRound.State.BossState with { Power = 0 },
            castRound.State.PetState);

        Assert.False(noAttackFixture.BossAttackQualifies);
        Assert.DoesNotContain(noAttackRound.Events, e => e.Type == BattleEventType.DamageDealt);

        // The countdown still ticked: 2 → 1 at that Round's 19a.
        Assert.Equal(1, FlameBurst(noAttackRound.State).RemainingTurns);

        // Round 3 — the first (and only) buffed attack in this window: the attack sees
        // the last Turn, and its 19a consumption removes the instance.
        var attackFixture = BossAttackRoundFixture.Value;

        var attackRound = await harness.ExecuteFixtureRoundAsync(
            battleId,
            attackFixture,
            noAttackRound.State.BossState with { Power = 0 },
            noAttackRound.State.PetState);

        Assert.Equal(
            BossAtkUnderFlameBurst + attackFixture.BossResources.BaseDamagePool,
            Assert.Single(attackRound.Events, e => e.Type == BattleEventType.DamageCalculated)
                .DamageCalculated.Base);

        Assert.DoesNotContain(
            attackRound.State.BossState.ActiveStatusEffects,
            e => e.Id == ProvisionedBossCardContent.FlameBurstCardId);
    }

    // =======================================================================
    // root — duration 3, at most two affected Player attacks
    // =======================================================================

    [Fact]
    public async Task Root_ShouldAffectAtMostTwoQualifyingPlayerAttacksAndExpireAtStep19a()
    {
        // TASK-309 §1 B-3: duration 3 yields at most two affected Player attacks. The
        // Pet-side composition is COMBAT_RULES.md §5.6.6 item 2 — the authored positive
        // magnitude maps to -|30|, so ATK 50 composes to 35 at the Player attack's
        // Step-1 input.
        var harness = BossCardTestHarness.CreateProvisioned();
        var boss = WithoutRage(BossDefinitions.MocYeu);

        const string battleId = "task309-lifecycle-root-window";
        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, boss);
        var petState = Surviving(created.PetState);

        // Round 1 — the cast. Duration 3 applied at 18c; step 19a consumes one Turn.
        var castRound = await harness.CastRoundAsync(
            battleId,
            created,
            created.BossState with { Power = 100, SkillCooldown = 0 },
            petState);

        Assert.Equal(2, Root(castRound.State).RemainingTurns);

        var playerAttackFixture = PlayerAttackRoundFixture.Value;

        // Round 2 — the Player's Match is a qualifying attack; its Step-1 Base carries
        // the debuffed ATK plus the ATK-Gem pool.
        var secondRound = await harness.ExecuteFixtureRoundAsync(
            battleId,
            playerAttackFixture,
            castRound.State.BossState with { Power = 0 },
            castRound.State.PetState);

        AssertPlayerAttackBase(
            secondRound,
            PetAtkUnderRoot + playerAttackFixture.PlayerResources.BaseDamagePool);

        Assert.Equal(1, Root(secondRound.State).RemainingTurns);

        // Round 3 — the second and last affected Player attack; its 19a consumption
        // reaches 0 and removes the instance in the same commit.
        var thirdRound = await harness.ExecuteFixtureRoundAsync(
            battleId,
            playerAttackFixture,
            secondRound.State.BossState with { Power = 0 },
            secondRound.State.PetState);

        AssertPlayerAttackBase(
            thirdRound,
            PetAtkUnderRoot + playerAttackFixture.PlayerResources.BaseDamagePool);

        Assert.DoesNotContain(
            thirdRound.State.PetState.ActiveStatusEffects,
            e => e.Id == ProvisionedBossCardContent.RootCardId);

        // Round 4 — the window is over: the same Player attack resolves at the
        // undebuffed ATK.
        var fourthRound = await harness.ExecuteFixtureRoundAsync(
            battleId,
            playerAttackFixture,
            thirdRound.State.BossState with { Power = 0 },
            thirdRound.State.PetState);

        AssertPlayerAttackBase(
            fourthRound,
            PetBaseAtk + playerAttackFixture.PlayerResources.BaseDamagePool);

        Assert.DoesNotContain(
            fourthRound.State.PetState.ActiveStatusEffects,
            e => e.Id == ProvisionedBossCardContent.RootCardId);
    }

    [Fact]
    public async Task Root_ShouldExpireAtTheCorrectStepWhenTheWindowContainsNoQualifyingPlayerAttack()
    {
        // A Round whose Player Swap clears no ATK Gem resolves no Player attack
        // (ADR-026 D1), but step 19a still consumes one Turn of `root`.
        var harness = BossCardTestHarness.CreateProvisioned();
        var boss = WithoutRage(BossDefinitions.MocYeu);

        const string battleId = "task309-lifecycle-root-no-attack";
        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, boss);
        var petState = Surviving(created.PetState);

        var castRound = await harness.CastRoundAsync(
            battleId,
            created,
            created.BossState with { Power = 100, SkillCooldown = 0 },
            petState);

        Assert.Equal(2, Root(castRound.State).RemainingTurns);

        // Round 2 — the Player's committed Swap is a DEF Match: no ATK Gem cleared, so
        // no Player attack instance (the Boss's own board action is not the Player's).
        var noAttackPinned = RoundTestSupport.PlayerMatch(
            GemType.Def,
            castRound.State.RngState,
            castRound.State.LastCommittedSwapPair);

        var noAttackRound = await harness.ExecutePinnedRoundAsync(
            battleId,
            noAttackPinned,
            castRound.State.RngState,
            castRound.State.BossState with { Power = 0 },
            castRound.State.PetState);

        Assert.DoesNotContain(
            noAttackRound.Events,
            e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Player);

        // The countdown ticked anyway: 2 → 1.
        Assert.Equal(1, Root(noAttackRound.State).RemainingTurns);

        // Round 3 — the first (and only) affected Player attack in this window.
        var playerAttackFixture = PlayerAttackRoundFixture.Value;

        var attackRound = await harness.ExecuteFixtureRoundAsync(
            battleId,
            playerAttackFixture,
            noAttackRound.State.BossState with { Power = 0 },
            noAttackRound.State.PetState);

        AssertPlayerAttackBase(
            attackRound,
            PetAtkUnderRoot + playerAttackFixture.PlayerResources.BaseDamagePool);

        Assert.DoesNotContain(
            attackRound.State.PetState.ActiveStatusEffects,
            e => e.Id == ProvisionedBossCardContent.RootCardId);
    }

    // =======================================================================
    // Multiplicative stacking — TASK-309 §1 B-4 (1.20 × 1.50 = 1.80)
    // =======================================================================

    [Fact]
    public async Task RageAndFlameBurst_ShouldStackMultiplicativelyTo180AndNeverAdditively()
    {
        // COMBAT_RULES.md §5.5.1 / §5.5.5: each active TargetStat "ATK" BuffDebuff
        // applies the documented formula in turn, so rage (+20%) and flame-burst (+50%)
        // compose as 1.20 × 1.50 = 1.80. TASK-309 §1 B-4 explicitly preserves this
        // multiplicative model; additive stacking would read 170 and is rejected.
        //
        // The rage instance is the Boss Passive's documented effect
        // (BOSS_RULES.md §6.2.1: +20% ATK, 3 Turns, Turn-based BuffDebuff), applied at
        // step 18b of this same round; flame-burst is the cast at 18c. The stacking is
        // therefore observed on a genuinely committed round, in event order 18b → 18c.
        var harness = BossCardTestHarness.CreateProvisioned();

        var boss = BossDefinitions.HoaLong with
        {
            CastPriorityScore = int.MaxValue,
            MaxHP = 1_000_000,
            ATK = BossAtk,
        };

        const string battleId = "task309-lifecycle-stacking";
        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, boss);

        // Passive progress one Match away from Hỏa Long's threshold of 5: the Player's
        // committed Match makes step 18b charge and trigger the Rage in this round.
        var bossState = created.BossState with
        {
            Power = 100,
            SkillCooldown = 0,
            PassiveProgress = created.BossState.PassiveProgress with { Current = 4 },
        };

        var castRound = await harness.CastRoundAsync(
            battleId,
            created,
            bossState,
            Surviving(created.PetState));

        // Both modifiers are active on the Boss's own collection at the commit.
        var rage = Assert.Single(
            castRound.State.BossState.ActiveStatusEffects,
            e => e.Id == "boss-hoa-long-rage");
        Assert.Equal(20, rage.Magnitude);
        Assert.Equal("ATK", rage.TargetStat);

        var flameBurst = FlameBurst(castRound.State);
        Assert.Equal(50, flameBurst.Magnitude);
        Assert.Equal("ATK", flameBurst.TargetStat);

        // The documented composition: 100 → 120 → 180, not 170.
        var effective = StatusEffectLifecycle.EffectiveBossAttack(
            boss.ATK,
            castRound.State.BossState.ActiveStatusEffects);

        Assert.Equal(BossAtkUnderRageAndFlameBurst, effective);
        Assert.NotEqual(BossAtk + 20 + 50, effective);

        // The base stat is never written (COMBAT_RULES.md §5.5.4).
        Assert.Equal(BossAtk, castRound.State.BossState.ATK);

        // The cast follows the 18b Passive reports in the round's single ordered batch.
        var triggeredIndex = IndexOf(castRound.Events, e => e.Type == BattleEventType.PassiveTriggered);
        var castIndex = IndexOf(castRound.Events, e => e.Type == BattleEventType.BossCardCast);

        Assert.True(triggeredIndex >= 0, "the Rage trigger must be reported at step 18b");
        Assert.True(castIndex > triggeredIndex, "BossCardCast must follow the 18b Passive events");

        // A qualifying Boss attack in the next Round consumes the composed 180 at its
        // Step-1 input (COMBAT_RULES.md §3.4, §5.5.1) — the composition is the one the
        // damage pipeline actually uses, not merely a readable helper's answer.
        var attackFixture = BossAttackRoundFixture.Value;

        var attackRound = await harness.ExecuteFixtureRoundAsync(
            battleId,
            attackFixture,
            castRound.State.BossState with { Power = 0 },
            castRound.State.PetState);

        Assert.Equal(
            BossAtkUnderRageAndFlameBurst + attackFixture.BossResources.BaseDamagePool,
            Assert.Single(attackRound.Events, e => e.Type == BattleEventType.DamageCalculated)
                .DamageCalculated.Base);
    }

    // =======================================================================
    // Fixtures and helpers
    // =======================================================================

    /// <summary>
    /// A deterministic real-board fixture on which the Boss's own board action is a
    /// qualifying attack (confirmed ATK-Gem match) and the Player's Swap clears no ATK
    /// Gem, so the round holds exactly one damage instance — the Boss's.
    /// </summary>
    private static readonly Lazy<BossRoundFixture> BossAttackRoundFixture =
        new(() => BossRoundFixtures.Find(f => !f.PlayerClearsAtkGem && f.BossAttackQualifies));

    /// <summary>
    /// A deterministic real-board fixture on which neither side's action produces a
    /// qualifying attack: the Player's Swap clears no ATK Gem and the Boss's own
    /// deterministic board action clears no ATK Gem.
    /// </summary>
    private static readonly Lazy<BossRoundFixture> BossNoAttackRoundFixture =
        new(() => BossRoundFixtures.Find(f => !f.PlayerClearsAtkGem && !f.BossAttackQualifies));

    /// <summary>
    /// A deterministic real-board fixture on which the Player's committed Swap clears
    /// at least one ATK Gem — a qualifying Player attack (ADR-026 D1).
    /// </summary>
    private static readonly Lazy<BossRoundFixture> PlayerAttackRoundFixture =
        new(() => BossRoundFixtures.Find(f => f.PlayerClearsAtkGem));

    /// <summary>
    /// A migrated Boss fixture whose Passive can never charge far enough to trigger a
    /// second ATK modifier inside one of the duration scenarios. The Skill's authored
    /// cooldown and the provisioned Boss Card values are unchanged.
    /// </summary>
    private static BossDefinition WithoutRage(BossDefinition definition) =>
        definition with
        {
            CastPriorityScore = int.MaxValue,
            MaxHP = 1_000_000,
            ATK = BossAtk,
            PassiveDefinition = definition.PassiveDefinition with { Threshold = 100_000 },
        };

    /// <summary>The committed <c>flame-burst</c> instance (<c>BOSS_RULES.md</c> §6.3.1 item 1).</summary>
    private static StatusEffect FlameBurst(BattleState state) =>
        Assert.Single(
            state.BossState.ActiveStatusEffects,
            e => e.Id == ProvisionedBossCardContent.FlameBurstCardId);

    /// <summary>The committed <c>root</c> instance (<c>BOSS_RULES.md</c> §6.3.1 item 3).</summary>
    private static StatusEffect Root(BattleState state) =>
        Assert.Single(
            state.PetState.ActiveStatusEffects,
            e => e.Id == ProvisionedBossCardContent.RootCardId);

    /// <summary>
    /// Asserts the round's Player attack carried the expected Step-1 <c>Base</c>
    /// (<c>COMBAT_RULES.md</c> §3.4: Effective ATK + the ATK-Gem pool). The Player's
    /// instance is the round's first damage instance — the Player phase resolves before
    /// the Boss response (<c>GAME_RULES.md</c> §17).
    /// </summary>
    private static void AssertPlayerAttackBase(SwapExecutionResult round, int expectedBase)
    {
        Assert.Contains(
            round.Events,
            e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Player);

        Assert.Equal(expectedBase, round.Events.First(e => e.Type == BattleEventType.DamageCalculated).DamageCalculated.Base);
    }

    /// <summary>
    /// The Pet at its documented base stats with a survivable HP pool
    /// (<c>COMBAT_RULES.md</c> §1.1; <c>GAME_RULES.md</c> §1.4's terminal outcome is
    /// not this suite's subject).
    /// </summary>
    private static PetState Surviving(PetState petState) =>
        petState with { HP = ObservationWindowHp, MaxHP = ObservationWindowHp };

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
