using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// The Boss-side <c>BuffDebuff</c> ATK consumption rule —
/// <c>COMBAT_RULES.md</c> §5.5.1 (the Boss-side counterpart of §5.4), whose MVP
/// instance is Hỏa Long's Rage (<c>BOSS_RULES.md</c> §6.2.1).
///
/// <code>
/// Rule (COMBAT_RULES.md §5.5.1-§5.5.4)
///  ↓
/// Scenario (Given BossState.ATK and the Boss's active instances, When a Boss
///           attack's Step-1 input is derived, Then the documented value)
///  ↓
/// Test
/// </code>
///
/// <b>Every expected value is read from §5.5 / §3.4, never from the
/// implementation.</b> §5.5.1's authored formula and its direction semantics are
/// asserted verbatim — <c>ATK 100</c> at <c>+20</c> → <c>120</c>, at <c>-30</c> →
/// <c>70</c>, at <c>0</c> → <c>100</c> — together with its truncate-toward-zero
/// convention. The <c>-30</c> case is asserted explicitly because it is the defect
/// the rework corrects: the superseded implementation inferred a buff/debuff from
/// the sign instead of evaluating §5.5.1's formula with the signed value, which
/// inverted a documented decrease into an increase. Where §5.5.3/§5.5.4 fix a
/// negative — that a non-<c>"ATK"</c> instance modifies nothing, that <c>Id</c> is
/// not consulted, that the stored stat is not overwritten, that the Pet-side
/// convention is not reused — the negative is asserted explicitly, so the test
/// fails if the implementation drifts to a rejected reading rather than merely to
/// a different number.
///
/// <b>Scope.</b> This file covers the Domain-level consumer
/// (<see cref="StatusEffectLifecycle.EffectiveBossAttack"/>): selection, the signed
/// percentage, truncation in both directions, the excluded instance types, base
/// preservation, and the separation from the Pet-side convention. The step-18b
/// composition it feeds — and the Basic Attack it must not change — is asserted
/// end-to-end in the Application suite, where a real Turn is resolved.
/// </summary>
public class EffectiveBossAttackTests
{
    /// <summary>
    /// Rage as <c>BOSS_RULES.md</c> §6.2.1 fixes it: a Turn-based
    /// <c>BuffDebuff</c> with <c>TargetStat = "ATK"</c> and <c>Magnitude = +20%</c>,
    /// held in the Boss's own <c>StatusEffects[]</c>. Built through the documented
    /// factory (<c>GAME_STATE.md</c> §2.3.1 item 7 enforces the
    /// <c>TargetStat</c>-iff-<c>BuffDebuff</c> pairing at construction).
    /// </summary>
    private static StatusEffect Rage(int duration = 3) =>
        StatusEffect.TurnBased(
            "Rage", StatusEffectType.BuffDebuff, StatusEffectSource.Boss, 20, duration, "ATK");

    /// <summary>
    /// A <c>BuffDebuff</c> naming a stat other than <c>"ATK"</c> — the case
    /// §5.5.3 states modifies no ATK.
    /// </summary>
    private static StatusEffect BuffDebuff(string id, string targetStat, double magnitude, int duration) =>
        StatusEffect.TurnBased(
            id, StatusEffectType.BuffDebuff, StatusEffectSource.Boss, magnitude, duration, targetStat);

    // =======================================================================
    // No modifier active — the unreduced value
    // =======================================================================

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(101)]
    [InlineData(0)]
    public void EffectiveBossAttack_ShouldBeTheStoredStat_WhenNoAtkInstanceIsActive(int attack)
    {
        // §5.5.1 consumes the modifier only while an instance is active; with no
        // TargetStat = "ATK" BuffDebuff present the Step-1 Attack input is the
        // stored stat itself (GAME_STATE.md §2.3.2 item 1: "no active effect" is an
        // empty array, never null).
        Assert.Equal(attack, StatusEffectLifecycle.EffectiveBossAttack(attack, []));
    }

    // =======================================================================
    // Rage — the +20% increase, COMBAT_RULES.md §3.4's worked example
    // =======================================================================

    [Fact]
    public void EffectiveBossAttack_ShouldBeOneTwenty_ForTheDocumentsWorkedExample()
    {
        // COMBAT_RULES.md §3.4's worked example, asserted verbatim:
        //   BossState.ATK = 100, Hỏa Long Rage = +20%
        //   EffectiveBossATK = truncate(100 × (100 + 20) / 100) = 120
        Assert.Equal(120, StatusEffectLifecycle.EffectiveBossAttack(100, [Rage()]));
    }

    [Theory]
    [InlineData(100, 120)]
    [InlineData(50, 60)]
    [InlineData(200, 240)]
    [InlineData(0, 0)]
    public void EffectiveBossAttack_ShouldApplyTheActivePercentageToTheStoredStat(int attack, int expected)
    {
        // §5.5.1 item 3: "EffectiveBossATK = the modified BossState.ATK" — the
        // percentage applies to the stat ALONE. Asserting several stat values keeps
        // the derivation a function of the stored value rather than a constant.
        Assert.Equal(expected, StatusEffectLifecycle.EffectiveBossAttack(attack, [Rage()]));
    }

    [Theory]
    [InlineData(50, 60)]    // truncate(50 × 120 / 100) = 60   — exact
    [InlineData(51, 61)]    // truncate(51 × 120 / 100) = 61   — exact
    [InlineData(7, 8)]      // truncate(7 × 120 / 100) = 8.4 → 8
    [InlineData(3, 3)]      // truncate(3 × 120 / 100) = 3.6 → 3
    public void EffectiveBossAttack_ShouldTruncateTowardZero(int attack, int expected)
    {
        // §5.5.1 applies the integer convention §5.4.2 states for the Pet side and
        // §3 step 6 uses for Final Damage — truncation toward zero, not rounding,
        // not ceiling, and not dependent on floating-point representation. The 7 → 8
        // and 3 → 3 cases are the ones a rounding implementation would get wrong
        // (8.4 → 8 either way, but 3.6 would round to 4).
        Assert.Equal(expected, StatusEffectLifecycle.EffectiveBossAttack(attack, [Rage()]));
    }

    // =======================================================================
    // Selection — COMBAT_RULES.md §5.5.3 (Type + TargetStat, never Id)
    // =======================================================================

    [Fact]
    public void EffectiveBossAttack_ShouldSelectByTypeAndTargetStat_NotById()
    {
        // §5.5.3 applies §5.4.5's discipline to the Boss side: the consumer reads
        // TargetStat explicitly, and Rage is the MVP INSTANCE of the rule rather
        // than the rule's identity. A differently-named "ATK" instance therefore
        // applies — selecting on Id would be a second, undocumented definition.
        var differentlyNamed = BuffDebuff("SomeOtherAtkBuff", "ATK", 20, 3);

        Assert.Equal(120, StatusEffectLifecycle.EffectiveBossAttack(100, [differentlyNamed]));
    }

    [Fact]
    public void EffectiveBossAttack_ShouldIgnoreABuffDebuffNamingAnotherStat()
    {
        // §5.5.3: "Does NOT apply to a BuffDebuff naming any stat other than
        // 'ATK'". A defence buff must leave the ATK term exactly as it was.
        Assert.Equal(100, StatusEffectLifecycle.EffectiveBossAttack(100, [BuffDebuff("Guard", "DEF", 50, 3)]));
    }

    [Fact]
    public void EffectiveBossAttack_ShouldIgnoreDoTShieldAndStateInstances()
    {
        // §5.5.3: "Does NOT apply to a DoT tick or a Shield: those Types have
        // their own rules". The Boss's own step-19a Burn tick is a DoT in this very
        // collection, and Stun is a State — none is an ATK modifier.
        var dot = StatusEffect.TurnBased("Burn", StatusEffectType.DoT, StatusEffectSource.Player, 50, 2);
        var shield = StatusEffect.TriggerBased(
            "Shield", StatusEffectType.Shield, StatusEffectSource.Player, 80, StatusEffect.ShieldDepletedCondition);
        var state = StatusEffect.TurnBased("Stun", StatusEffectType.State, StatusEffectSource.Player, 1, 1);

        Assert.Equal(100, StatusEffectLifecycle.EffectiveBossAttack(100, [dot, shield, state]));
    }

    // =======================================================================
    // Debuffs — a negative magnitude reduces, using the same single formula
    // =======================================================================

    [Fact]
    public void EffectiveBossAttack_ShouldReduceTheStat_ForANegativeMagnitude()
    {
        // COMBAT_RULES.md §5.5.1 item 4: "Magnitude < 0 -> decrease", and the
        // formula is §5.5.1 item 3's with the signed value as its addend:
        //   truncate(100 × (100 + (−30)) / 100) = truncate(70) = 70
        //
        // This is THE case the rework exists to fix. The superseded implementation
        // read |Magnitude| and treated the sign as an inferred buff/debuff flag, so
        // it produced 130 here — the exact inversion of the documented decrease.
        var bossAtkDebuff = BuffDebuff("Weaken", "ATK", -30, 2);

        Assert.Equal(70, StatusEffectLifecycle.EffectiveBossAttack(100, [bossAtkDebuff]));
    }

    [Theory]
    // §5.5.1 item 4's three documented directions, on one stored stat.
    [InlineData(20, 120)]    // Magnitude > 0 -> increase
    [InlineData(0, 100)]     // Magnitude = 0 -> unchanged
    [InlineData(-30, 70)]    // Magnitude < 0 -> decrease
    public void EffectiveBossAttack_ShouldCarryTheMagnitudeSignAsDirection(int magnitude, int expected)
    {
        // One formula, one arithmetic expression, the sign of the instance's own
        // Magnitude: §5.5.1 item 4 states direction "is carried by the sign of
        // `Magnitude` itself — no separate direction field, flag, operation, or
        // member exists or is introduced". A branch on an inferred semantic is
        // therefore not this rule.
        Assert.Equal(expected, StatusEffectLifecycle.EffectiveBossAttack(100, [BuffDebuff("Mod", "ATK", magnitude, 2)]));
    }

    [Theory]
    [InlineData(100, -30, 70)]      // truncate(70)   — exact
    [InlineData(51, -30, 35)]       // truncate(35.7) — 35, not 36
    [InlineData(7, -30, 4)]         // truncate(4.9)  — 4, not 5
    [InlineData(13, -30, 9)]        // truncate(9.1)  — 9
    public void EffectiveBossAttack_ShouldTruncateTowardZero_ForADecrease(int attack, int magnitude, int expected)
    {
        // §5.5.1's truncation-toward-zero convention applies to both directions —
        // the decrease is not rounded either. Truncation and rounding differ here
        // (35.7 → 35 vs 36; 4.9 → 4 vs 5), so a rounding implementation fails.
        Assert.Equal(expected, StatusEffectLifecycle.EffectiveBossAttack(attack, [BuffDebuff("Weaken", "ATK", magnitude, 2)]));
    }

    // =======================================================================
    // Multiple active modifiers — §5.1.1 item 6's deterministic order
    // =======================================================================

    [Fact]
    public void EffectiveBossAttack_ShouldBeIndependentOfCollectionOrder()
    {
        // §5.5.1 authors the arithmetic for one instance and §5.5.5 fixes the
        // existing multiplicity contract (one instance per effect identity, refresh
        // not stack), so no new stacking rule is available to order anything by.
        // Each active instance applies §5.5.1's single formula in turn, in §5.1.1
        // item 6's Id-ordinal order, so the result is a function of the instances
        // rather than of how the array happens to be arranged (§2.3.1 item 10).
        var rage = Rage();
        var weaken = BuffDebuff("Weaken", "ATK", -30, 2);

        var inOneOrder = StatusEffectLifecycle.EffectiveBossAttack(100, [rage, weaken]);
        var inTheOther = StatusEffectLifecycle.EffectiveBossAttack(100, [weaken, rage]);

        Assert.Equal(inOneOrder, inTheOther);

        // And each instance really did contribute, with its own sign:
        //   100 → (+20%) → 120 → (−30%) → truncate(120 × 70 / 100) = 84
        // which is neither 100, 120, nor 70. Neither instance is reduced to an
        // absolute-value factor, so a sign-blind fold cannot reach this value.
        Assert.Equal(84, inOneOrder);

        // The two instances' own single-instance results bracket it, which is what
        // "applied in turn" means observably.
        Assert.Equal(120, StatusEffectLifecycle.EffectiveBossAttack(100, [rage]));
        Assert.Equal(70, StatusEffectLifecycle.EffectiveBossAttack(100, [weaken]));
    }

    [Fact]
    public void EffectiveBossAttack_ShouldApplyTheSingleFormulaToANetNegativePair()
    {
        // Two active ATK instances whose magnitudes net negative: the fold is still
        // §5.5.1's one formula applied per instance, in Id order.
        //   100 → ("AWeaken" −20%) → 80 → ("BRage" +10%) → 88
        var first = BuffDebuff("AWeaken", "ATK", -20, 2);
        var second = BuffDebuff("BRage", "ATK", 10, 2);

        Assert.Equal(80, StatusEffectLifecycle.EffectiveBossAttack(100, [first]));
        Assert.Equal(110, StatusEffectLifecycle.EffectiveBossAttack(100, [second]));
        Assert.Equal(88, StatusEffectLifecycle.EffectiveBossAttack(100, [second, first]));
    }

    // =======================================================================
    // Non-destructiveness — COMBAT_RULES.md §5.5.4
    // =======================================================================

    [Fact]
    public void EffectiveBossAttack_ShouldNotModifyTheStoredStatOrTheCollection()
    {
        // §5.5.4: "BossState.ATK is never overwritten by the modifier, and there is
        // no 'restore' step: the stored stat is unchanged throughout." The function
        // takes the stat by value and returns a derived int, so both the stat AND
        // the instance collection must be untouched — an implementation that wrote
        // the reduced value back would fail here.
        var effects = new[] { Rage() };
        var attack = 100;

        var derived = StatusEffectLifecycle.EffectiveBossAttack(attack, effects);

        Assert.Equal(120, derived);
        Assert.Equal(100, attack);
        Assert.Equal(20, Assert.Single(effects).Magnitude);
        Assert.Equal(3, Assert.Single(effects).RemainingTurns);
    }

    [Fact]
    public void EffectiveBossAttack_ShouldNotCarryTheModifier_WhenTheInstanceExpired()
    {
        // §5.3 DR5 / GAME_STATE.md §2.3.1 item 8: an instance at 0 is removed in the
        // same step-19a resolution, so an expired modifier is simply ABSENT from the
        // collection this consumer reads — the following attack uses the unreduced
        // stat with no residual state.
        var consumption = StatusEffectLifecycle.ConsumeAtStep19a([Rage(duration: 1)]);

        Assert.Empty(consumption);
        Assert.Equal(100, StatusEffectLifecycle.EffectiveBossAttack(100, consumption));
    }

    // =======================================================================
    // The composition this value feeds — COMBAT_RULES.md §3.4
    // =======================================================================

    [Fact]
    public void BossSkillStepOne_ShouldBeEffectiveBossAtkPlusTheAuthoredBaseDamage()
    {
        // COMBAT_RULES.md §3.4 "Boss Skill Step-1 composition", asserted against
        // the authored content rather than literals where possible:
        //   Step 1 Base Damage = EffectiveBossATK + authored Skill Base Damage
        //
        // The authored value comes from the Boss definition (§6.3/§6.3.1 of
        // BOSS_RULES.md), and it is passed through UNCHANGED — §5.5.2 makes the
        // modifier reach the Skill's Step-1 damage only through the
        // EffectiveBossATK contribution.
        var boss = BossDefinitions.HoaLong;

        Assert.Equal(150, boss.SkillBaseDamage);   // §6.3.1 item 1's authored value

        // Rage inactive (§3.4: "100 + 150 = 250").
        var withoutRage = StatusEffectLifecycle.EffectiveBossAttack(boss.ATK, []) + boss.SkillBaseDamage;

        Assert.Equal(250, withoutRage);

        // Rage active (§3.4's worked example: "120 + 150 = 270").
        var withRage = StatusEffectLifecycle.EffectiveBossAttack(boss.ATK, [Rage()]) + boss.SkillBaseDamage;

        Assert.Equal(270, withRage);

        // The entire +20 increase is the EffectiveBossATK contribution: the delta
        // between the two composed values is Rage's own effect on the stat, and the
        // authored term is identical in both.
        Assert.Equal(20, withRage - withoutRage);
    }

    [Fact]
    public void BossSkillStepOne_ShouldCarryADecreaseThroughEffectiveBossAtkAlone()
    {
        // §5.5.1 item 4's decrease, composed under §3.4 exactly as the increase is:
        // an active Magnitude = -30 gives EffectiveBossATK = 70, so the same Skill's
        // Step 1 is 70 + 150 = 220 — and the authored 150 is still 150, because
        // §5.5.2 lets the modifier reach the Skill's damage only through the
        // EffectiveBossATK contribution.
        var boss = BossDefinitions.HoaLong;
        var decrease = BuffDebuff("Weaken", "ATK", -30, 2);

        Assert.Equal(70, StatusEffectLifecycle.EffectiveBossAttack(boss.ATK, [decrease]));
        Assert.Equal(220, StatusEffectLifecycle.EffectiveBossAttack(boss.ATK, [decrease]) + boss.SkillBaseDamage);
        Assert.Equal(150, boss.SkillBaseDamage);
    }

    // =======================================================================
    // The Pet-side convention is NOT reused — COMBAT_RULES.md §5.5.1
    // =======================================================================

    [Fact]
    public void BossAndPetSideConventions_ShouldRemainDeliberatelyDifferent()
    {
        // §5.5.1 states the two rules "must not be collapsed into one shared
        // formula": the Boss side uses the SIGNED Magnitude and supports an
        // increase, while §5.4.1's Pet side takes |Magnitude| and only ever reduces.
        // The SAME instance is therefore read differently by the two consumers, and
        // each consumer reads its own collection. A shared implementation would have
        // to agree here; these two must not.
        var positiveThirty = BuffDebuff("Root", "ATK", 30, 2);

        // Boss side: Magnitude > 0 -> increase.
        Assert.Equal(130, StatusEffectLifecycle.EffectiveBossAttack(100, [positiveThirty]));

        // Pet side (§5.4.1 item 3, unchanged): |30| always reduces, so Root's
        // authored -30% Pet ATK debuff stays exactly as BOSS_RULES.md §6.3.1 item 3
        // declares it.
        Assert.Equal(70, StatusEffectLifecycle.EffectiveAttack(100, [], [positiveThirty]));

        // A Boss-side ATK modifier that a future source declares as a decrease is
        // written as the negative 30 and is NOT reinterpreted as an increase.
        Assert.Equal(70, StatusEffectLifecycle.EffectiveBossAttack(100, [BuffDebuff("Weaken", "ATK", -30, 2)]));
    }
}
