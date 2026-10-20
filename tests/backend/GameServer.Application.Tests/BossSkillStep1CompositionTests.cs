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
/// The Boss's ATK-match-gated attack instance driven through a real atomic round —
/// its Step-1 composition (<c>COMBAT_RULES.md</c> §3.4 item 1, §5.5.1), the gate
/// that decides whether the instance exists at all (<c>ADR-026</c> D1,
/// <c>GAME_RULES.md</c> §17 step 18d), and the Skill's non-damaging secondary
/// effect (<c>ADR-026</c> D3/D5, <c>BOSS_RULES.md</c> §6.3, §6.3.1).
///
/// <code>
/// Rule (COMBAT_RULES.md §3.4 / §5.5.1; ADR-026 D1, D3; BOSS_RULES.md §6.3)
///  ↓
/// Scenario (Given a pinned board whose Boss board action is known, When the round
///           resolves, Then the documented Step-1 value, gate, and effect)
///  ↓
/// Test
/// </code>
///
/// <b>What this suite asserts NOW.</b> The legacy fallback basic attack
/// ("Step 1 — Base Damage = Boss.ATK", independent of the board) is RETIRED
/// (<c>ADR-026</c> §5, "Elimination of Default Fixed-Damage Attacks";
/// <c>COMBAT_RULES.md</c> §3.4 item 1). The legacy <c>BossSkill</c> path's
/// charge-driven secondary effect was retired with ADR-029 Slice 5 / TASK-313,
/// and a Boss ability is now its provisioned Boss Card; that effect's canonical
/// coverage is <c>TASK309BossCardEffectRuntimeTests</c> /
/// <c>BossCardCastRuntimeTests</c>. What survives, and is
/// what this file tests, is:
/// <list type="number">
/// <item><c>EffectiveBossATK</c> — <c>BossState.ATK</c> composed with the Boss's own
/// active <c>TargetStat = "ATK"</c> instances in <c>Id</c>-ordinal order,
/// <c>truncate(ATK × (100 + Magnitude) / 100)</c>, never written back to
/// <c>BossState.ATK</c> (<c>COMBAT_RULES.md</c> §5.5.1 items 3–4, §5.5.4) —
/// reaching the Boss's ATK-gated instance's Step 1, which §3.4 item 1 fixes as
/// that value plus the <c>BaseDamagePool</c> the Boss's own ATK Gems generated
/// (<c>COMBAT_RULES.md</c> §2);</item>
/// <item>selection by <c>Type</c> + <c>TargetStat</c> and never by <c>Id</c>
/// (<c>COMBAT_RULES.md</c> §5.5.3): a DoT, a Shield, a State, or a BuffDebuff
/// naming another stat is not an ATK modifier;</item>
/// <item>the instance's Step 2 Combo Modifier of <c>1</c> and Step 4 of
/// <c>1.0</c> (<c>COMBAT_RULES.md</c> §3.4 item 1).</item>
/// </list>
///
/// <b>Every Boss instance is located by <c>DamageDealt.Source</c>.</b>
/// <c>ADR-026</c> D1 makes a round carry at most one Boss-sourced instance, and
/// only when the Boss's own swap cleared an ATK Gem, so an instance is never
/// identified by position or by a "large base value" heuristic — it is found by
/// the report that says who dealt it (<c>GAME_EVENTS.md</c> §2).
///
/// <b>How a scenario is arranged.</b> The Player's Swap is the pinned
/// non-ATK-Gem fixture (<see cref="RoundTestSupport"/>), so the Player phase emits
/// no attack instance and consumes no crit draw (<c>ADR-026</c> D1), which makes
/// the Boss's own board action a pure function of the pinned board and the
/// round's committed <c>RngState</c> (<c>ADR-026</c> §8). The suite reads that
/// action forward with the Domain stages the production path itself uses, and
/// self-verifies the reading against the committed round before asserting on it:
/// <see cref="RoundTestSupport.BossClearedAtkGem"/> must agree with the reading,
/// so an expectation derived from the Boss's own ATK-Gem output can never be a
/// statement about some other board action.
///
/// <b>Expected values come from the documents, not from the implementation.</b>
/// <c>EffectiveBossATK</c> is compared against <c>StatusEffectLifecycle.EffectiveBossAttack</c>
/// — the §5.5.1 formula's own Domain consumer, as the documents' worked examples
/// (100 at +20% → 120; 100 at −30% → 70) state them — and the pool is the §2 rate
/// table applied by the Domain <c>ResourceGenerator</c> to the Boss's own cleared
/// ATK Gems. Step 2, Step 4, and the report contiguity are asserted from
/// <c>COMBAT_RULES.md</c> §3.4 item 1 and <c>GAME_EVENTS.md</c> §1.
/// </summary>
public class BossSkillStep1CompositionTests
{
    /// <summary>
    /// The Pet these battles carry (<c>GAME_STATE.md</c> §2.3), Xích Lang's MVP
    /// Element and Passive. Hỏa Long's Hỏa Element therefore meets a Hỏa defender,
    /// which <c>ELEMENT_RULES.md</c> §2.1 resolves as Neutral.
    /// </summary>
    private static readonly BattleStateService.PetConfiguration Pet =
        new(new PetId("pet_instance_1"), Element.Hoa, new PassiveId("xich-lang"), PassiveThreshold: 5);

    /// <summary>The owning Player of these battles (<c>GAME_STATE.md</c> §2.8).</summary>
    private static readonly PlayerId Owner = new("player_boss_attack_owner");

    /// <summary>
    /// The arrangement attempt bound. The fixture search is deterministic, so the
    /// first accepted arrangement is the same on every run; the bound only keeps a
    /// future fixture change from looping forever.
    /// </summary>
    private const ulong MaxSeedAttempts = 48;

    /// <summary>
    /// The Gem types the Player-phase fixture may clear. Every one of them is a
    /// non-ATK type, so the Player produces no attack instance of its own
    /// (<c>ADR-026</c> D1) and this suite's round holds the Boss's instance alone.
    /// </summary>
    private static readonly GemType[] PlayerFixtureTypes = [GemType.Def, GemType.Hp, GemType.Power];

    // =======================================================================
    // 1. Step 1 with no active ATK modifier — stored ATK + the ATK-Gem pool
    //    (COMBAT_RULES.md §3.4 item 1, §5.5.1 item 4; ADR-026 D3)
    // =======================================================================

    [Fact]
    public async Task BossAttack_ShouldUseTheStoredBossAtkPlusTheAtkGemPool_WhenNoAtkModifierIsActive()
    {
        // COMBAT_RULES.md §3.4 item 1: "Step 1 — Base Damage = EffectiveBossATK +
        // BaseDamagePool". With no active TargetStat="ATK" instance, §5.5.1 items
        // 3–4 leave EffectiveBossATK the stored stat itself.
        //
        // ADR-026 D3 / BOSS_RULES.md §6.3.1 item 1: Flame Burst's authored Base
        // Damage is 0, so Step 1 has no authored Skill term at all — the retired
        // "100 + 150" spelling of this test's subject no longer exists.
        var round = await ResolveBossAtkRoundAsync("boss-attack-no-modifier", bossClearsAtkGem: true);
        var boss = BossDefinitions.HoaLong;

        // The scenario really is free of a second ATK modifier, so the assertion
        // below is about the stored stat alone — read on the PRE-resolution state,
        // which COMBAT_RULES.md §5.5.1's "Activity" bullet makes the collection the
        // attack's Step 1 consumed (§5.3: activity follows the committed state).
        Assert.Empty(AtkModifiers(round.Before.BossState));

        // BOSS_RULES.md §6.1: Hỏa Long's MVP ATK is 100.
        Assert.Equal(100, boss.ATK);

        // COMBAT_RULES.md §2: the Boss's own ATK match generated the pool, at +10
        // Base Damage per cleared ATK Gem times the shape's tier.
        Assert.True(round.BossGeneration.ClearedGemCounts.Atk > 0);
        Assert.True(round.BossGeneration.BaseDamagePool > 0);

        Assert.Equal(
            boss.ATK + round.BossGeneration.BaseDamagePool,
            BossAttackCalculation(round.Result).Base);

        // Exactly one instance, and it is the Boss's: the gate opened once.
        Assert.Equal(1, BossAttackInstanceCount(round.Result));
    }

    // =======================================================================
    // 2. EffectiveBossATK reaches the instance (COMBAT_RULES.md §3.4 item 1,
    //    §5.5.1's worked example; BOSS_RULES.md §6.2.1)
    // =======================================================================

    [Fact]
    public async Task BossAttack_ShouldUseEffectiveBossAtk_WhenRageIsActive()
    {
        // COMBAT_RULES.md §5.5.1 item 3's worked example, asserted verbatim:
        //   BossState.ATK = 100, active Magnitude = +20
        //   EffectiveBossATK = truncate(100 × (100 + 20) / 100) = 120
        // and §3.4 item 1's Step 1 = 120 + the ATK-Gem BaseDamagePool.
        var round = await ResolveBossAtkRoundAsync(
            "boss-attack-rage",
            bossClearsAtkGem: true,
            arrangeBoss: boss => boss with { ActiveStatusEffects = [Rage()] });

        var boss = BossDefinitions.HoaLong;

        // The one ATK modifier this scenario carries is the pinned Rage instance
        // (BOSS_RULES.md §6.2.1's representation: Turn-based, TargetStat "ATK", +20).
        var active = Assert.Single(AtkModifiers(round.Before.BossState));
        Assert.Equal(20, active.Magnitude);

        Assert.Equal(120, StatusEffectLifecycle.EffectiveBossAttack(boss.ATK, [Rage()]));
        Assert.Equal(
            120 + round.BossGeneration.BaseDamagePool,
            BossAttackCalculation(round.Result).Base);
    }

    [Fact]
    public async Task BossAttack_ShouldDifferByExactlyTheModifiersEffect_WhenRageIsActive()
    {
        // The increase comes entirely from the EffectiveBossATK contribution: §5.5.1
        // item 3 applies the percentage to BossState.ATK ALONE, and §3.4 item 1 keeps
        // the ATK-Gem pool a separate Step-1 contribution that no ATK modifier
        // scales. Both runs therefore act on the same board action and differ by
        // exactly Rage's effect: 120 − 100 = 20.
        var without = await ResolveBossAtkRoundAsync("boss-attack-delta-off", bossClearsAtkGem: true);
        var with = await ResolveBossAtkRoundAsync(
            "boss-attack-delta-on",
            bossClearsAtkGem: true,
            arrangeBoss: boss => boss with { ActiveStatusEffects = [Rage()] });

        // Same pinned board and same Boss move, so the pool is identical and the
        // delta below is the modifier's and nothing else.
        Assert.Equal(without.BossGeneration.BaseDamagePool, with.BossGeneration.BaseDamagePool);

        var withoutBase = BossAttackCalculation(without.Result).Base;
        var withBase = BossAttackCalculation(with.Result).Base;

        Assert.Equal(100 + without.BossGeneration.BaseDamagePool, withoutBase);
        Assert.Equal(120 + with.BossGeneration.BaseDamagePool, withBase);
        Assert.Equal(20, withBase - withoutBase);

        // The pool was never scaled: recovering it from the buffed instance returns
        // the same ATK-Gem contribution the unbuffed instance reported.
        Assert.Equal(
            withoutBase - 100,
            withBase - StatusEffectLifecycle.EffectiveBossAttack(100, [Rage()]));
    }

    // =======================================================================
    // 3. The signed Magnitude's OTHER direction (COMBAT_RULES.md §5.5.1 item 4)
    // =======================================================================

    [Fact]
    public async Task BossAttack_ShouldUseTheDecreasedEffectiveBossAtk_WhenAModifierDecreases()
    {
        // §5.5.1 item 4: "Magnitude < 0 → decrease", through §3.4 item 1's Step 1:
        //   EffectiveBossATK = truncate(100 × (100 + (−30)) / 100) = 70
        //   Step 1 = 70 + BaseDamagePool
        var round = await ResolveBossAtkRoundAsync(
            "boss-attack-decrease",
            bossClearsAtkGem: true,
            arrangeBoss: boss => boss with { ActiveStatusEffects = [AtkDecrease(-30)] });

        var pool = round.BossGeneration.BaseDamagePool;

        Assert.Equal(70, StatusEffectLifecycle.EffectiveBossAttack(100, [AtkDecrease(-30)]));
        Assert.Equal(70 + pool, BossAttackCalculation(round.Result).Base);

        // Emphatically NOT the superseded inverted reading, which applied the
        // magnitude's absolute value as an increase and produced 130 here.
        Assert.NotEqual(130 + pool, BossAttackCalculation(round.Result).Base);

        // And NOT the unmodified stored stat either.
        Assert.NotEqual(100 + pool, BossAttackCalculation(round.Result).Base);

        // COMBAT_RULES.md §5.5.4 applies to both directions: the 70 is derived and
        // discarded, and the committed BossState.ATK is still the stored 100 — not 70,
        // and not a "restored" value. The modifier instance survives unmodified.
        Assert.Equal(100, round.Committed.BossState.ATK);
        Assert.Equal(-30, Assert.Single(AtkModifiers(round.Committed.BossState)).Magnitude);
    }

    [Fact]
    public async Task BossAttack_ShouldTreatAZeroMagnitudeAsUnchanged()
    {
        // §5.5.1 item 4: "Magnitude = 0 → unchanged  EffectiveBossATK =
        // BossState.ATK". A zero is neither an error nor a 100% reduction, so the
        // composed Step 1 is the same as with no instance at all.
        var round = await ResolveBossAtkRoundAsync(
            "boss-attack-zero",
            bossClearsAtkGem: true,
            arrangeBoss: boss => boss with { ActiveStatusEffects = [AtkDecrease(0)] });

        Assert.Equal(100, StatusEffectLifecycle.EffectiveBossAttack(100, [AtkDecrease(0)]));
        Assert.Equal(
            100 + round.BossGeneration.BaseDamagePool,
            BossAttackCalculation(round.Result).Base);
    }

    // =======================================================================
    // 4. The derivation is a function of the stored stat (COMBAT_RULES.md §5.5.1)
    // =======================================================================

    [Fact]
    public async Task BossAttack_ShouldDeriveFromTheStoredStat_ForANonDefaultBossAtk()
    {
        // §5.5.1 item 3: EffectiveBossATK = the modified BossState.ATK — a function of
        // the stored stat, never a constant. A Boss whose ATK is not the MVP default
        // must therefore produce a different composed value, so an implementation
        // that hard-coded 120 cannot pass.
        var boss = BossDefinitions.HoaLong with { ATK = 200 };

        var without = await ResolveBossAtkRoundAsync("boss-attack-nondefault-off", bossClearsAtkGem: true, boss);
        var with = await ResolveBossAtkRoundAsync(
            "boss-attack-nondefault-on",
            bossClearsAtkGem: true,
            boss,
            arrangeBoss: state => state with { ActiveStatusEffects = [Rage()] });

        // truncate(200 × 120 / 100) = 240.
        Assert.Equal(240, StatusEffectLifecycle.EffectiveBossAttack(200, [Rage()]));
        Assert.Equal(without.BossGeneration.BaseDamagePool, with.BossGeneration.BaseDamagePool);

        var withoutBase = BossAttackCalculation(without.Result).Base;
        var withBase = BossAttackCalculation(with.Result).Base;

        Assert.Equal(200 + without.BossGeneration.BaseDamagePool, withoutBase);
        Assert.Equal(240 + with.BossGeneration.BaseDamagePool, withBase);
        Assert.Equal(40, withBase - withoutBase);
    }

    // =======================================================================
    // 5. BossState.ATK immutability (COMBAT_RULES.md §5.5.4)
    // =======================================================================

    [Fact]
    public async Task BossAttack_ShouldNeverOverwriteTheStoredBossAtk()
    {
        // §5.5.4: "BossState.ATK is never overwritten by the modifier, and there is
        // no 'restore' step". The committed stat must therefore be identical before
        // and after the resolution that derived the increased value — and the
        // modifier instance itself must survive unmodified, because the composition
        // only READS it (§5.5.5 duration is the existing step 19a countdown's).
        var round = await ResolveBossAtkRoundAsync(
            "boss-attack-immutability",
            bossClearsAtkGem: true,
            arrangeBoss: boss => boss with { ActiveStatusEffects = [Rage()] });

        Assert.Equal(100, round.Before.BossState.ATK);
        Assert.Equal(120 + round.BossGeneration.BaseDamagePool, BossAttackCalculation(round.Result).Base);

        // Still the base stat, not the derived 120 and not a scaled one.
        Assert.Equal(100, round.Committed.BossState.ATK);

        // The instance was read, not consumed or rewritten: its own magnitude and
        // target survive, with exactly step 19a's one Turn consumed (3 → 2).
        var rage = Assert.Single(AtkModifiers(round.Committed.BossState));

        Assert.Equal(20, rage.Magnitude);
        Assert.Equal("ATK", rage.TargetStat);
        Assert.Equal(2, rage.RemainingTurns);
    }

    // =======================================================================
    // 6. Selection by Type + TargetStat, never by Id (COMBAT_RULES.md §5.5.3)
    // =======================================================================

    [Fact]
    public async Task BossAttack_ShouldApplyAnAtkModifierBearingAnUnrelatedIdentity()
    {
        // §5.5.3 applies §5.4.5's discipline: selection is by Type + TargetStat, so
        // an "ATK" instance whose Id is not "Rage" still applies. This is the
        // positive half of "never dispatch on Id".
        var round = await ResolveBossAtkRoundAsync(
            "boss-attack-unrelated-id",
            bossClearsAtkGem: true,
            arrangeBoss: boss => boss with
            {
                ActiveStatusEffects =
                [
                    StatusEffect.TurnBased(
                        "SomeOtherAtkBuff", StatusEffectType.BuffDebuff, StatusEffectSource.Boss, 20, 3, "ATK"),
                ],
            });

        Assert.Equal(
            120 + round.BossGeneration.BaseDamagePool,
            BossAttackCalculation(round.Result).Base);
    }

    [Fact]
    public async Task BossAttack_ShouldIgnoreInstancesThatAreNotAtkModifiers()
    {
        // §5.5.3's negative set: a BuffDebuff naming another stat, a DoT (the Boss's
        // own Burn tick lives in this very collection), a State, and a Shield are all
        // non-ATK instances and must leave Step 1 at the unmodified stored ATK plus
        // the pool.
        var round = await ResolveBossAtkRoundAsync(
            "boss-attack-non-atk",
            bossClearsAtkGem: true,
            arrangeBoss: boss => boss with
            {
                ActiveStatusEffects =
                [
                    StatusEffect.TurnBased("Guard", StatusEffectType.BuffDebuff, StatusEffectSource.Boss, 50, 3, "DEF"),
                    StatusEffect.TurnBased("Burn", StatusEffectType.DoT, StatusEffectSource.Boss, 50, 2),
                    StatusEffect.TurnBased("Stun", StatusEffectType.State, StatusEffectSource.Boss, 1, 1),
                    StatusEffect.TriggerBased(
                        "Shield",
                        StatusEffectType.Shield,
                        StatusEffectSource.Boss,
                        150,
                        StatusEffect.ShieldDepletedCondition),
                ],
            });

        Assert.Equal(
            100 + round.BossGeneration.BaseDamagePool,
            BossAttackCalculation(round.Result).Base);

        // The instances genuinely were present when the attack's Step 1 was composed —
        // the negative above is not the trivial result of nothing having been pinned.
        // Asserted on the PRE-resolution state, because step 19a legitimately consumes
        // the 1-Turn State within the same round (GAME_STATE.md §5.1.1 items 2–4).
        Assert.Equal(4, round.Before.BossState.ActiveStatusEffects.Length);
        Assert.Empty(AtkModifiers(round.Before.BossState));
    }

    // =======================================================================
    // 7. Damage Pipeline integration, exactly once, with §3.4 item 1's steps 2 and 4
    // =======================================================================

    [Fact]
    public async Task BossAttack_ShouldEnterThePipelineWithComboOneAndStep4Unchanged()
    {
        // §3.4 item 1: a Boss attack is the same six-step Damage Pipeline as the
        // Player's, with "Step 2 — Combo Modifier = 1 (Boss attacks are not part of a
        // Combo chain)" and "Step 4 — Other Modifiers = 1.0". The ATK modifier is a
        // Step-1 input consumed through EffectiveBossATK, never a Step-4 factor
        // (§5.5.3), so it must NOT also appear as 1.2 here.
        var round = await ResolveBossAtkRoundAsync(
            "boss-attack-pipeline",
            bossClearsAtkGem: true,
            arrangeBoss: boss => boss with { ActiveStatusEffects = [Rage()] });

        var result = round.Result;
        var index = BossAttackCalculatedIndex(result);
        var calculation = result.Events[index].DamageCalculated;

        Assert.Equal(120 + round.BossGeneration.BaseDamagePool, calculation.Base);
        Assert.Equal(1.00, calculation.ComboModifier);
        Assert.Equal(1.00, calculation.OtherModifiers);

        // §3.4 item 1 Step 3: the defending target is the Pet, and Hỏa Long is Hỏa
        // against this Pet's Hỏa — ELEMENT_RULES.md §2.1 resolves equal Elements as
        // Neutral, whose factor is §2.2's 1.00.
        Assert.Equal(1.00, calculation.ElementModifier);

        // GAME_EVENTS.md §1: one instance is reported by three contiguous events —
        // DamageCalculated, DamageDealt, DamageTaken — carrying one Final Damage.
        Assert.Equal(BattleEventType.DamageDealt, result.Events[index + 1].Type);
        Assert.Equal(BattleEventType.DamageTaken, result.Events[index + 2].Type);

        var dealt = result.Events[index + 1].DamageDealt;
        var taken = result.Events[index + 2].DamageTaken;

        Assert.Equal(DamageParty.Boss, dealt.Source);
        Assert.Equal(DamageParty.Player, dealt.Target);
        Assert.Equal(calculation.FinalDamage, dealt.Amount);
        Assert.Equal(DamageParty.Player, taken.Target);
        Assert.Equal(calculation.FinalDamage, taken.Amount);

        // The instance is emitted exactly once: two DamageCalculated reports for the
        // composed Base would mean the composition ran twice.
        Assert.Equal(1, BossAttackInstanceCount(result));
        Assert.Equal(1, BossDamageCalculationCount(result));
    }

    // =======================================================================
    // 8. The ATK-match gate (ADR-026 D1; GAME_RULES.md §17 step 18d)
    // =======================================================================

    [Fact]
    public async Task BossAttack_ShouldNotExist_WhenTheBossClearedNoAtkGem()
    {
        // ADR-026 D1 / COMBAT_RULES.md §3.4 item 1: "A Boss attack occurs if and only
        // if the Boss's swap produces a confirmed ATK-gem match ... If zero ATK gems
        // are matched, zero attack damage is dealt." With the legacy fallback basic
        // attack retired (ADR-026 §5), a gate-closed round therefore carries no
        // Boss-sourced instance at all — no DamageCalculated, no DamageDealt, no
        // DamageTaken — rather than an un-gated Basic Attack.
        var round = await ResolveBossAtkRoundAsync(
            "boss-attack-gate-closed",
            bossClearsAtkGem: false,
            playerMatchType: GemType.Def);

        Assert.Equal(0, round.BossGeneration.ClearedGemCounts.Atk);
        Assert.False(RoundTestSupport.BossClearedAtkGem(round.Result));

        Assert.Equal(0, BossAttackInstanceCount(round.Result));
        Assert.Equal(0, BossDamageCalculationCount(round.Result));
        Assert.DoesNotContain(
            round.Result.Events,
            e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Boss);
        Assert.DoesNotContain(
            round.Result.Events,
            e => e.Type == BattleEventType.DamageTaken && e.DamageTaken.Target == DamageParty.Player);

        // The Pet is untouched: the Player phase cleared DEF Gems only, which neither
        // heals nor damages (COMBAT_RULES.md §2), so a Pet HP that did not move is a
        // statement that the Boss dealt nothing.
        Assert.Equal(round.Before.PetState.HP, round.Committed.PetState.HP);
    }

    // =======================================================================
    // Helpers
    // =======================================================================

    /// <summary>
    /// One resolved round, together with the Boss board action that produced it.
    /// </summary>
    /// <param name="Result">The committed round (<c>GAME_STATE.md</c> §5.1).</param>
    /// <param name="BossGeneration">
    /// The resources the Boss's OWN board action generated — read forward from the
    /// pinned board and the round's committed <c>RngState</c> through the same Domain
    /// stages the production path uses (<c>COMBAT_RULES.md</c> §2), and verified
    /// against the committed round's own board events before it is used.
    /// </param>
    /// <param name="Before">
    /// The pinned state the round started from — the state whose Boss
    /// <c>ActiveStatusEffects</c> the attack's Step 1 read.
    /// </param>
    /// <param name="Committed">The state the round committed.</param>
    private readonly record struct BossAttackRound(
        SwapExecutionResult Result,
        ResourceGeneration BossGeneration,
        BattleState Before,
        BattleState Committed);

    /// <summary>
    /// Rage as <c>BOSS_RULES.md</c> §6.2.1 fixes it — a Turn-based
    /// <c>BuffDebuff</c> held in the Boss's own <c>StatusEffects[]</c> with
    /// <c>TargetStat = "ATK"</c>, <c>Magnitude = +20</c>, duration 3 Turns. Built
    /// through the documented factory, so the <c>TargetStat</c>-iff-<c>BuffDebuff</c>
    /// pairing holds by construction (<c>GAME_STATE.md</c> §2.3.1 item 7).
    /// </summary>
    private static StatusEffect Rage(int duration = 3) =>
        StatusEffect.TurnBased(
            "Rage", StatusEffectType.BuffDebuff, StatusEffectSource.Boss, 20, duration, "ATK");

    /// <summary>
    /// A Boss-side ATK modifier expressed as a <b>decrease</b>, which
    /// <c>COMBAT_RULES.md</c> §5.5.1 item 4 carries as the negative
    /// <c>Magnitude</c>: "<c>Magnitude &lt; 0 → decrease</c>".
    /// </summary>
    private static StatusEffect AtkDecrease(double magnitude) =>
        StatusEffect.TurnBased(
            "Weaken", StatusEffectType.BuffDebuff, StatusEffectSource.Boss, magnitude, 3, "ATK");

    /// <summary>
    /// The Boss's active ATK modifiers — the <c>Type</c> + <c>TargetStat</c>
    /// selection <c>COMBAT_RULES.md</c> §5.5.1/§5.5.3 defines, restated here for
    /// assertions only (the production consumer is
    /// <see cref="StatusEffectLifecycle.EffectiveBossAttack"/>).
    /// </summary>
    private static StatusEffect[] AtkModifiers(BossState boss) =>
        [.. boss.ActiveStatusEffects.Where(
            e => e.Type == StatusEffectType.BuffDebuff && e.TargetStat == "ATK")];

    /// <summary>
    /// Resolves one atomic round, searching deterministically for an arrangement
    /// whose Boss board action has the requested ATK-Gem outcome
    /// (<c>ADR-026</c> §8: the Boss's move is a pure function of the board and the
    /// retained <c>RngState</c>).
    ///
    /// <b>The Player phase is the pinned non-ATK fixture.</b> A fixture that clears
    /// no ATK Gem makes the Player phase resolve no attack instance and consume no
    /// crit draw (<c>ADR-026</c> D1), so the Boss's own action is exactly the reading
    /// <see cref="PredictBossBoardGeneration"/> produces from the fixture. The
    /// committed round is then checked against that reading before anything is
    /// asserted on it, so a test can never silently describe a different board.
    /// </summary>
    /// <param name="battleIdPrefix">
    /// Namespace for the generated battle ids, so the arrangements a test searches
    /// are its own.
    /// </param>
    /// <param name="bossClearsAtkGem">
    /// Whether the arrangement must produce a confirmed Boss ATK match — the gate
    /// <c>ADR-026</c> D1 makes the whole difference between "one Damage Pipeline
    /// execution" and "no instance at all".
    /// </param>
    /// <param name="definition">The Boss definition, defaulting to Hỏa Long.</param>
    /// <param name="arrangeBoss">
    /// Pins the Boss combat state — its <c>ActiveStatusEffects</c>, its
    /// <c>ATK</c>, and its <c>SkillCooldown</c> — through
    /// <see cref="RoundTestSupport.PinAsync"/>.
    /// </param>
    /// <param name="playerMatchType">
    /// Restricts the fixture's Gem type, when a scenario needs the Player phase to
    /// have a specific non-damaging effect (e.g. no HP-Gem healing).
    /// </param>
    private static async Task<BossAttackRound> ResolveBossAtkRoundAsync(
        string battleIdPrefix,
        bool bossClearsAtkGem,
        BossDefinition? definition = null,
        Func<BossState, BossState>? arrangeBoss = null,
        GemType? playerMatchType = null)
    {
        var boss = definition ?? BossDefinitions.HoaLong;

        for (var seed = 0UL; seed < MaxSeedAttempts; seed++)
        {
            foreach (var matchType in PlayerFixtureTypes)
            {
                if (playerMatchType is { } required && matchType != required)
                {
                    continue;
                }

                var repository = new InMemoryBattleStateRepository();
                var service = new BattleStateService(repository, new FixedRngSeedSource());
                var battleId = $"{battleIdPrefix}-{matchType}-{seed}";

                var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss, new BattleSeed(seed));

                PinnedPlayerRound pinned;

                try
                {
                    pinned = RoundTestSupport.PlayerMatch(
                        matchType, created.RngState, created.LastCommittedSwapPair);
                }
                catch (InvalidOperationException)
                {
                    // The fixture could not be built for this RngState; the next
                    // arrangement is tried rather than weakening the scenario.
                    continue;
                }

                // The fixture's own contract: the Player cleared no ATK Gem, so the
                // Player phase contributes no instance to this round (ADR-026 D1).
                Assert.Equal(0, pinned.Resources.ClearedGemCounts.Atk);

                var generation = PredictBossBoardGeneration(pinned);

                // Keep the first arrangement whose Boss action lands on the requested
                // side of the gate: "cleared at least one ATK Gem" is the branch under
                // test, and the search is deterministic so the same arrangement is
                // found on every run.
                if (generation.ClearedGemCounts.Atk > 0 != bossClearsAtkGem)
                {
                    continue;
                }

                var bossState = arrangeBoss?.Invoke(created.BossState) ?? created.BossState;

                await RoundTestSupport.PinAsync(repository, created, pinned.Board, bossState);

                var before = (await service.GetBattleAsync(battleId))!;
                var result = (await service.ExecuteSwapAsync(battleId, pinned.Swap))!.Value;

                Assert.True(result.IsAccepted);

                // The reading mirrors the committed round — otherwise every expected
                // value below would be a statement about a different board action.
                Assert.Equal(bossClearsAtkGem, RoundTestSupport.BossClearedAtkGem(result));

                var committed = (await service.GetBattleAsync(battleId))!;

                return new BossAttackRound(result, generation, before, committed);
            }
        }

        throw new InvalidOperationException(
            $"No pinned arrangement produced a Boss board action with clearedAtk={bossClearsAtkGem}. "
            + "This is a test-arrangement defect (ADR-026 §8): the fixture search no longer covers "
            + "the branch under test.");
    }

    /// <summary>
    /// The resources the Boss's own board action generates, read forward from the
    /// Player-phase fixture: the board the Player phase left
    /// (<c>pinned.Resolution.Board</c>) and the retained <c>RngState</c> the round
    /// committed (<c>GAME_STATE.md</c> §2.6.2 item 2).
    ///
    /// <b>This is the documented rate table applied to the Boss's own clears, not a
    /// second implementation of the round.</b> The move is
    /// <see cref="BossAiEngine.SelectMove"/>'s (<c>ADR-026</c> §8), the cascade is
    /// <see cref="CascadeResolver"/>'s, and the pools are
    /// <see cref="ResourceGenerator"/>'s (<c>COMBAT_RULES.md</c> §2) — the same
    /// Domain stages <c>BattleStateService.ResolveSwapAsync</c> calls for the Boss
    /// phase, in the same order.
    /// </summary>
    private static ResourceGeneration PredictBossBoardGeneration(PinnedPlayerRound pinned)
    {
        var rngState = pinned.Resolution.RngState;

        var move = BossAiEngine.SelectMove(
            pinned.Resolution.Board, new Pcg32(rngState.State, rngState.Increment));

        // ADR-026 §8: only a deadlock (zero legal swaps) moves the board and the
        // stream, through the engine's own bounded deterministic reshuffle.
        var resolveState = move.Reshuffled ? move.RngState : rngState;

        var resolution = CascadeResolver.Resolve(
            move.Board.WithSwapped(move.SelectedMove.From, move.SelectedMove.To),
            new Pcg32(resolveState.State, resolveState.Increment));

        return ResourceGenerator.Generate(resolution);
    }

    /// <summary>
    /// The index of the round's Boss damage instance — located by
    /// <c>DamageDealt.Source == DamageParty.Boss</c>, the report that names the
    /// dealer (<c>GAME_EVENTS.md</c> §2), never by position or by a value heuristic.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The round carries no Boss-sourced instance, which <c>ADR-026</c> D1 makes the
    /// correct outcome for a gate-closed round — callers that expect one must have
    /// arranged the gate open.
    /// </exception>
    private static int BossAttackCalculatedIndex(SwapExecutionResult result)
    {
        for (var index = 0; index + 1 < result.Events.Count; index++)
        {
            if (result.Events[index].Type == BattleEventType.DamageCalculated
                && result.Events[index + 1].Type == BattleEventType.DamageDealt
                && result.Events[index + 1].DamageDealt.Source == DamageParty.Boss)
            {
                return index;
            }
        }

        throw new InvalidOperationException(
            "ADR-026 D1: the round carries no Boss-sourced damage instance — its "
            + "DamageDealt.Source == DamageParty.Boss report is absent.");
    }

    /// <summary>The step-1..6 breakdown of the round's Boss instance.</summary>
    private static DamageCalculation BossAttackCalculation(SwapExecutionResult result) =>
        result.Events[BossAttackCalculatedIndex(result)].DamageCalculated;

    /// <summary>
    /// How many Boss-sourced damage instances the round carries — <c>ADR-026</c> D1
    /// makes it zero or one.
    /// </summary>
    private static int BossAttackInstanceCount(SwapExecutionResult result) =>
        result.Events.Count(
            e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Boss);

    /// <summary>How many damage calculations the round reports at all.</summary>
    private static int BossDamageCalculationCount(SwapExecutionResult result) =>
        result.Events.Count(e => e.Type == BattleEventType.DamageCalculated);
}
