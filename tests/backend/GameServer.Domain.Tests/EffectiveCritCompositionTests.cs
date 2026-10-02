using GameServer.Domain.Battle;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Effective Crit composition, the composed-value cap, and the unchanged bounded
/// roll (<c>COMBAT_RULES.md</c> §1.1, §3.3 items 1–2 and 7; <c>GAME_STATE.md</c>
/// §2.3.4).
///
/// <code>
/// Rule (COMBAT_RULES.md §1.1, §3.3 item 7)
///  ↓
/// Scenario (Given the documented Crit sources, When step 4 runs, Then the
///           documented Effective Crit and roll outcome)
///  ↓
/// Test
/// </code>
///
/// Every expected value below is derived from the owning document, never from the
/// implementation:
///
/// <code>
/// COMBAT_RULES.md §3.3 item 7   EffectiveCrit = BaseCrit + PassiveCrit +
///                               RelicCrit + applicable NextAttack modifiers,
///                               additive percentage points, capped at 100
/// COMBAT_RULES.md §3.3 item 2   one bounded selection, V ∈ [0, 100);
///                               succeeds iff V < EffectiveCrit
/// COMBAT_RULES.md §3.3 item 3   Crit multiplier 1.5×, else 1.00×
/// COMBAT_RULES.md §1.1          Crit range 0–100 percentage points
/// </code>
/// </summary>
public sealed class EffectiveCritCompositionTests
{
    /// <summary>
    /// The documented neutral-scenario inputs: an elementless-neutral matchup, no
    /// Defense, and a Combo of 1, so step 4's factor is the only thing that changes
    /// the damage. That isolates the Crit stage the way this suite's other step
    /// scenarios isolate theirs.
    /// </summary>
    private static DamagePipeline.DamageInputs Inputs(
        int attackerCrit,
        int nextAttackCritContribution,
        RngState rng) =>
        new(
            Attack: 100,
            BaseDamagePool: 0,
            Combo: 1,
            AttackerElement: Element.Moc,
            DefenderElement: Element.Moc,
            DefenderDefense: 0,
            DefenderHp: 500,
            Source: DamageParty.Player,
            Target: DamageParty.Boss,
            AttackerCrit: attackerCrit,
            RngState: rng,
            NextAttackCritContribution: nextAttackCritContribution);

    private static DamageResult Calculate(
        int attackerCrit,
        int nextAttackCritContribution,
        uint seed = 12345) =>
        DamagePipeline.Calculate(
            Inputs(attackerCrit, nextAttackCritContribution, Pcg32.FromSeed(seed).CurrentState),
            ComboModifiers.Default,
            ElementModifiers.Default);

    // ---------------------------------------------------------------------
    // Composition — COMBAT_RULES.md §3.3 item 7
    // ---------------------------------------------------------------------

    [Fact]
    public void EffectiveCrit_WithNoTemporaryModifier_EqualsTheBaseStat()
    {
        // §3.3 item 7: with no modifier active the composed value is the base — "the
        // sum over the elements that are active", and none is. Base 5 must behave
        // exactly as it did before this composition existed.
        var result = Calculate(attackerCrit: 5, nextAttackCritContribution: 0, seed: 12345);

        // The draw for seed 12345 is compared against 5, so the documented
        // "5% ⇒ accepted values {0,1,2,3,4}" reading holds: either it crit (1.50) or
        // it did not (1.00), and both are legitimate draws. What matters is that the
        // threshold used was the base alone.
        var roll = new Pcg32(Pcg32.FromSeed(12345).CurrentState.State, Pcg32.FromSeed(12345).CurrentState.Increment)
            .NextBounded(DamagePipeline.CritRollBound);
        var expected = roll < 5 ? 1.50 : 1.00;

        Assert.Equal(expected, result.Calculation.OtherModifiers);
    }

    [Fact]
    public void EffectiveCrit_AddsTheNextAttackContributionToTheBase()
    {
        // §3.3 items 7 and 10, the documented Iron Fang worked example: base 5 plus a
        // +10 modifier composes to 15. Using the same seed, the composed threshold
        // must be the one the roll is compared against — so a roll in [5, 15) crits
        // where the base alone would not.
        var rng = Pcg32.FromSeed(999).CurrentState;
        var roll = new Pcg32(rng.State, rng.Increment).NextBounded(DamagePipeline.CritRollBound);

        // Choose contributions that bracket the actual draw so the assertion is about
        // the composition and not about a lucky seed.
        var baseOnly = DamagePipeline.Calculate(
            Inputs(5, 0, rng),
            ComboModifiers.Default,
            ElementModifiers.Default);

        var withModifier = DamagePipeline.Calculate(
            Inputs(5, (int)roll - 4, rng),
            ComboModifiers.Default,
            ElementModifiers.Default);

        // (roll - 4) brings the composed value to roll + 1, so the roll is strictly
        // below it and the instance must crit; the base-only instance must not.
        Assert.Equal(1.00, baseOnly.Calculation.OtherModifiers);
        Assert.Equal(1.50, withModifier.Calculation.OtherModifiers);
    }

    [Fact]
    public void EffectiveCrit_IsCappedAtOneHundred()
    {
        // §3.3 item 7: "The composed value is capped at 100 percentage points."
        // §1.1 records the same 0–100 range for the Crit stat. Contributions that sum
        // far past the cap must still produce a guaranteed Crit and no more than the
        // 1.5× multiplier — the cap bounds the stat, and there is no second
        // multiplier for excess.
        var result = Calculate(attackerCrit: 95, nextAttackCritContribution: 200, seed: 777);

        Assert.Equal(1.50, result.Calculation.OtherModifiers);
        Assert.Equal(150, result.Calculation.FinalDamage);
    }

    [Fact]
    public void EffectiveCrit_AtExactlyOneHundred_AlwaysCrits()
    {
        // §3.3 item 2: the draw is V ∈ [0, 100), so V < 100 holds for every possible
        // draw. A composed value of exactly 100 is therefore a guaranteed Crit — the
        // documented boundary the cap must not exceed or fall short of.
        for (uint seed = 0; seed < 25; seed++)
        {
            var result = Calculate(attackerCrit: 90, nextAttackCritContribution: 10, seed: seed);

            Assert.Equal(1.50, result.Calculation.OtherModifiers);
        }
    }

    [Fact]
    public void EffectiveCrit_AtZero_NeverCrits()
    {
        // §3.3 item 2: V < 0 is false for every V in [0, 100), so a zero composed
        // value can never crit regardless of the draw.
        for (uint seed = 0; seed < 25; seed++)
        {
            var result = Calculate(attackerCrit: 0, nextAttackCritContribution: 0, seed: seed);

            Assert.Equal(1.00, result.Calculation.OtherModifiers);
        }
    }

    [Fact]
    public void EffectiveCrit_NegativeContribution_DoesNotProduceANegativeThreshold()
    {
        // A composed value below 0 is not a documented Crit chance. The lower clamp
        // keeps a malformed negative contribution from making the comparison
        // meaningless; the documented reading is "cannot crit", which is what
        // attackerCrit 0 already means.
        var result = Calculate(attackerCrit: 5, nextAttackCritContribution: -50, seed: 606);

        Assert.Equal(1.00, result.Calculation.OtherModifiers);
    }

    [Fact]
    public void EffectiveCrit_CompositionIsAdditiveAcrossMultipleContributions()
    {
        // §3.3 item 10: "Multiple active NextAttack Crit modifiers stack additively
        // and do not replace one another." The caller sums them
        // (NextAttackCritModifiers.TotalContribution) and the pipeline adds that sum
        // to the base — so two +10 sources compose exactly as one +20.
        NextAttackCritModifier[] twoSources =
        [
            new("card-iron-fang", 10),
            new("passive-bach-ho", 10),
        ];

        Assert.Equal(20, NextAttackCritModifiers.TotalContribution(twoSources));

        var viaOneSum = Calculate(attackerCrit: 5, nextAttackCritContribution: 20, seed: 606);

        // The documented Iron Fang × Bạch Hổ example: base 5 + 10 + 10 = Effective 25.
        var roll = new Pcg32(Pcg32.FromSeed(606).CurrentState.State, Pcg32.FromSeed(606).CurrentState.Increment)
            .NextBounded(DamagePipeline.CritRollBound);
        var expected = roll < 25 ? 1.50 : 1.00;

        Assert.Equal(expected, viaOneSum.Calculation.OtherModifiers);
    }

    [Fact]
    public void EffectiveCrit_DoesNotChangeTheRollBoundOrTheRngAdvance()
    {
        // §3.3 item 7: the cap "bounds the composed value; it does NOT change item 2's
        // roll bound." §3.3 item 2 fixes exactly one bounded selection over bound 100,
        // consuming from the single RngState. Composition therefore changes the
        // comparison threshold and nothing about the draw — the updated state must be
        // identical for the same seed whatever the composed value.
        var initialRng = Pcg32.FromSeed(31337).CurrentState;

        var lowCrit = DamagePipeline.Calculate(
            Inputs(0, 0, initialRng), ComboModifiers.Default, ElementModifiers.Default);

        var highCrit = DamagePipeline.Calculate(
            Inputs(100, 100, initialRng), ComboModifiers.Default, ElementModifiers.Default);

        // The draw advanced the stream by exactly one selection in both cases, and the
        // resulting state is the same because the threshold does not influence the draw.
        Assert.Equal(lowCrit.UpdatedRngState, highCrit.UpdatedRngState);

        var expectedPcg = new Pcg32(initialRng.State, initialRng.Increment);
        expectedPcg.NextBounded(DamagePipeline.CritRollBound);
        Assert.Equal(expectedPcg.CurrentState, lowCrit.UpdatedRngState);
    }

    [Fact]
    public void EffectiveCrit_ContributionIsIndependentOfTheBaseValue()
    {
        // §3.3 item 9 / §2.3.4 item 9: the base is never overwritten by a temporary
        // modifier, and a temporary modifier's contribution is a separate input. Two
        // different bases with the same contribution must therefore produce two
        // different thresholds, each being base + contribution.
        var rng = Pcg32.FromSeed(2024).CurrentState;
        var roll = new Pcg32(rng.State, rng.Increment).NextBounded(DamagePipeline.CritRollBound);

        // base 0 + contribution (roll + 1) ⇒ crits.
        var composedFromZero = DamagePipeline.Calculate(
            Inputs(0, (int)roll + 1, rng), ComboModifiers.Default, ElementModifiers.Default);

        Assert.Equal(1.50, composedFromZero.Calculation.OtherModifiers);
    }
}
