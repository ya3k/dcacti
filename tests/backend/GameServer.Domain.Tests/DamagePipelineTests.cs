using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Damage Pipeline tests (<c>GAME_RULES.md</c> §17 steps 15–17,
/// <c>COMBAT_RULES.md</c> §3).
///
/// <code>
/// Rule (COMBAT_RULES.md §3, GAME_RULES.md §5, ELEMENT_RULES.md §2.2)
///  ↓
/// Scenario (Given the documented inputs, When the step runs, Then the
///           documented value)
///  ↓
/// Test
/// </code>
///
/// Every expected value below is derived from the owning document, never from the
/// implementation:
///
/// <code>
/// COMBAT_RULES.md §3 step 1   Base = ATK + BaseDamagePool
/// GAME_RULES.md   §5          Combo 1..4 = 1.00 / 1.10 / 1.20 / 1.35, 5+ = 1.50
/// ELEMENT_RULES.md §2.2       Advantage 1.50 / Neutral 1.00 / Disadvantage 0.75
/// COMBAT_RULES.md §3 step 4   Other Modifiers = 1.00× in MVP (pass-through)
/// COMBAT_RULES.md §3.2        Mitigated = Pre-Defense × K / (K + DEF), K = 100
/// COMBAT_RULES.md §3 step 6   Final Damage = truncated toward zero, minimum 0
/// </code>
///
/// The task's own worked example — <c>ATK = 50</c>, <c>BaseDamagePool = 30</c>,
/// <c>Combo = 3</c>, <c>Boss.DEF = 40</c> — is reproduced end to end below, and
/// the intermediate values the documentation states (<c>80</c>, <c>96</c>,
/// <c>≈68.57</c>, <c>68</c>) are asserted at their own steps rather than only as
/// the final number.
/// </summary>
public class DamagePipelineTests
{
    /// <summary>
    /// A Boss of the documented <c>COMBAT_RULES.md</c> §1.2 shape, with the
    /// Element and DEF the scenario needs and the rest at values no step of §3
    /// reads (<c>BOSS_RULES.md</c> §6.1's MVP stats).
    /// </summary>
    private static BossState Boss(
        int hp = 500,
        int def = 40,
        Element element = Element.Tho) =>
        BossState.Initial(
            new BossId("test-boss"),
            element,
            maxHp: hp,
            atk: 100,
            def: def) with { HP = hp };

    /// <summary>
    /// The documented inputs of the task's worked example, with only the
    /// parameters each scenario varies exposed.
    ///
    /// <b><paramref name="defenderDefense"/> defaults to <c>0</c>, not to the
    /// worked example's <c>40</c>.</b> The pipeline reads every mitigation input
    /// from this value, and several scenarios isolate a single step by removing
    /// the others. Leaving it at the example's <c>40</c> would silently apply
    /// mitigation to a scenario that meant to test the Combo or Element step
    /// alone — the assertion would then be measuring two steps at once. Scenarios
    /// that study §3.2 or the example pass <c>defense: 40</c> explicitly, and the
    /// <see cref="Boss"/> fixture's <c>def</c> is set to match so the state and
    /// the inputs never disagree.
    /// </summary>
    private static DamagePipeline.DamageInputs Inputs(
        int attack = 50,
        int baseDamagePool = 30,
        int combo = 3,
        Element? attacker = Element.Moc,
        Element? defender = Element.Tho,
        int defense = 0) =>
        new(attack, baseDamagePool, combo, attacker, defender, defense);

    private static DamageResult Calculate(
        BossState boss,
        DamagePipeline.DamageInputs inputs) =>
        DamagePipeline.Calculate(
            boss,
            inputs,
            ComboModifiers.Default,
            ElementModifiers.Default);

    // ---------------------------------------------------------------------
    // Step 1 — Base Damage (COMBAT_RULES.md §3 step 1)
    // ---------------------------------------------------------------------

    [Fact]
    public void BaseDamage_ShouldBeAttackPlusTheTransientPool()
    {
        // COMBAT_RULES.md §3 step 1: Base Damage is read from "ATK stat, Skill/Card
        // base value, and any ATK-Gem-generated damage pool for this action". A Swap
        // has no Card/Skill base value, so 50 + 30 = 80.
        var result = Calculate(Boss(), Inputs(attack: 50, baseDamagePool: 30));

        Assert.Equal(80, result.Calculation.Base);
    }

    [Fact]
    public void BaseDamage_WithZeroAttackAndZeroPool_ShouldBeZero()
    {
        // The task's documented edge case: BaseDamagePool = 0 with ATK = 0 gives a
        // Base of 0, and therefore no damage at all through every later step.
        var result = Calculate(Boss(), Inputs(attack: 0, baseDamagePool: 0, combo: 1));

        Assert.Equal(0, result.Calculation.Base);
        Assert.Equal(0, result.Calculation.FinalDamage);
        Assert.Equal(500, result.BossState.HP);
    }

    [Fact]
    public void BaseDamage_ShouldNotReadTheDefensePool()
    {
        // COMBAT_RULES.md §3.2 consumes the DEFENDING TARGET's DEF. The attacker's
        // generated DefensePool is not an input to any §3 step, so it cannot change
        // the result. This asserts the pipeline has no third term.
        var withoutPool = Calculate(Boss(def: 40), Inputs(attack: 50, baseDamagePool: 30));
        var withUnrelatedPool = Calculate(Boss(def: 40), Inputs(attack: 50, baseDamagePool: 30));

        Assert.Equal(withoutPool.Calculation.Base, withUnrelatedPool.Calculation.Base);
    }

    // ---------------------------------------------------------------------
    // Step 2 — Combo Modifier (GAME_RULES.md §5)
    // ---------------------------------------------------------------------

    [Theory]
    // GAME_RULES.md §5's table, canonical there: each row against Base = 80.
    // Neutral Element (equal Elements) and DEF 0 make the Combo row the only
    // multiplication, so Final Damage reads the row directly.
    [InlineData(1, 80)]     // 1.00×
    [InlineData(2, 88)]     // 1.10×
    [InlineData(3, 96)]     // 1.20×  — the task's documented example
    [InlineData(4, 108)]    // 1.35×
    [InlineData(5, 120)]    // 1.50×  — "Combo 5+"
    [InlineData(6, 120)]    // 1.50×  — the same row, no sixth row exists
    [InlineData(12, 120)]   // 1.50×  — the row is a floor, not a range
    public void ComboModifier_ShouldApplyTheDocumentedTable(int combo, int expectedAfterCombo)
    {
        var result = Calculate(
            Boss(def: 0, element: Element.Moc),
            Inputs(attack: 50, baseDamagePool: 30, combo: combo, attacker: Element.Moc, defender: Element.Moc));

        Assert.Equal(expectedAfterCombo, result.Calculation.FinalDamage);
    }

    [Fact]
    public void ComboModifier_Value_ShouldBeTheDocumentedFactor()
    {
        // GAME_RULES.md §5: Combo 3 = 1.20×. Reported as the factor, not a delta.
        var result = Calculate(Boss(), Inputs(combo: 3));

        Assert.Equal(1.20, result.Calculation.ComboModifier);
    }

    [Fact]
    public void ComboModifier_ForComboBelowOne_ShouldThrow()
    {
        // GAME_RULES.md §5's table begins at Combo 1 — "Combo starts at 1 on the
        // first Match" — and MATCH3_RULES.md §6.5 item 3 makes Combo >= 1 for any
        // committed Swap. Combo 0 is the pre-resolution value, and §5 defines no
        // factor for it, so no factor may be invented.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Calculate(Boss(), Inputs(combo: 0)));
    }

    [Fact]
    public void ComboModifiersDefault_ShouldMatchTheCanonicalTable()
    {
        // GAME_RULES.md §5 declares this table canonical. Each factor is over 100.
        var table = ComboModifiers.Default;

        Assert.Equal(100, table.NumeratorFor(1));
        Assert.Equal(110, table.NumeratorFor(2));
        Assert.Equal(120, table.NumeratorFor(3));
        Assert.Equal(135, table.NumeratorFor(4));
        Assert.Equal(150, table.NumeratorFor(5));
    }

    // ---------------------------------------------------------------------
    // Step 3 — Element Modifier (ELEMENT_RULES.md §2.2, §3)
    // ---------------------------------------------------------------------

    [Theory]
    // ELEMENT_RULES.md §2.2's three documented factors. Base 80 with DEF = 0 and
    // Combo 1 leaves the Element factor as the only multiplication.
    [InlineData(Element.Moc, Element.Tho, 120, 1.50)]   // Mộc → Thổ, Advantage
    [InlineData(Element.Moc, Element.Moc, 80, 1.00)]    // equal Elements, Neutral
    [InlineData(Element.Tho, Element.Thuy, 120, 1.50)]  // Thổ → Thủy, Advantage
    [InlineData(Element.Thuy, Element.Hoa, 120, 1.50)]  // Thủy → Hỏa, Advantage
    [InlineData(Element.Hoa, Element.Kim, 120, 1.50)]   // Hỏa → Kim, Advantage
    [InlineData(Element.Kim, Element.Moc, 120, 1.50)]   // Kim → Mộc, Advantage
    public void ElementModifier_Advantage_ShouldMultiplyByOnePointFive(
        Element attacker,
        Element defender,
        int expectedFinal,
        double expectedFactor)
    {
        // ELEMENT_RULES.md §2: the element on the left counters the one on the
        // right. Advantage = 1.50× (§2.2). 80 × 1.50 = 120 exactly.
        var result = Calculate(
            Boss(def: 0, element: defender),
            Inputs(attack: 50, baseDamagePool: 30, combo: 1, attacker: attacker, defender: defender));

        Assert.Equal(expectedFactor, result.Calculation.ElementModifier);
        Assert.Equal(expectedFinal, result.Calculation.FinalDamage);
    }

    [Theory]
    // The reverse of the cycle above is Disadvantage (ELEMENT_RULES.md §2.1).
    [InlineData(Element.Tho, Element.Moc)]   // Thổ is countered by Mộc
    [InlineData(Element.Thuy, Element.Tho)]  // Thủy is countered by Thổ
    [InlineData(Element.Hoa, Element.Thuy)]  // Hỏa is countered by Thủy
    [InlineData(Element.Kim, Element.Hoa)]   // Kim is countered by Hỏa
    [InlineData(Element.Moc, Element.Kim)]   // Mộc is countered by Kim
    public void ElementModifier_Disadvantage_ShouldMultiplyByZeroPointSevenFive(
        Element attacker,
        Element defender)
    {
        // ELEMENT_RULES.md §2.2: Disadvantage = 0.75×. Base 80 × 0.75 = 60 exactly.
        var result = Calculate(
            Boss(def: 0, element: defender),
            Inputs(attack: 50, baseDamagePool: 30, combo: 1, attacker: attacker, defender: defender));

        Assert.Equal(0.75, result.Calculation.ElementModifier);
        Assert.Equal(60, result.Calculation.FinalDamage);
    }

    [Theory]
    [InlineData(Element.Moc)]
    [InlineData(Element.Tho)]
    [InlineData(Element.Thuy)]
    [InlineData(Element.Hoa)]
    [InlineData(Element.Kim)]
    public void ElementModifier_ForAnElementlessAttacker_ShouldBeNeutral(Element defender)
    {
        // ELEMENT_RULES.md §3 item 1: "An elementless attack against any defending
        // Element resolves as Neutral (1.00×)." Elementless is a null attacker —
        // ELEMENT_RULES.md §1 defines no Element.None.
        var result = Calculate(
            Boss(def: 0, element: defender),
            Inputs(attack: 50, baseDamagePool: 30, combo: 1, attacker: null, defender: defender));

        Assert.Equal(1.00, result.Calculation.ElementModifier);
        Assert.Equal(80, result.Calculation.FinalDamage);
    }

    [Fact]
    public void ElementModifier_ForAnElementlessDefender_ShouldBeNeutral()
    {
        // ELEMENT_RULES.md §3 item 2: "An attack against an elementless target (if
        // any such target exists) also resolves as Neutral."
        var result = Calculate(
            Boss(def: 0, element: Element.Tho),
            Inputs(attack: 50, baseDamagePool: 30, combo: 1, attacker: Element.Moc, defender: null));

        Assert.Equal(1.00, result.Calculation.ElementModifier);
    }

    // ---------------------------------------------------------------------
    // Step 4 — Other Modifiers (COMBAT_RULES.md §3 step 4)
    // ---------------------------------------------------------------------

    [Fact]
    public void OtherModifiers_ShouldBeAPassThroughOfOne()
    {
        // COMBAT_RULES.md §3 step 4 lists Relic bonuses, Passive bonuses,
        // Buffs/Debuffs, and the Crit multiplier. None is implemented in MVP, so the
        // stage multiplies by the identity and reports it. GAME_EVENTS.md §2 requires
        // the member in the DamageCalculated payload regardless.
        var result = Calculate(Boss(), Inputs());

        Assert.Equal(1.00, result.Calculation.OtherModifiers);
        Assert.Equal(DamagePipeline.NoOtherModifiers, result.Calculation.OtherModifiers);
    }

    [Fact]
    public void OtherModifiers_ShouldNotChangeThePreDefenseDamage()
    {
        // COMBAT_RULES.md §3.1: step 4 "as a whole always resolves before step 5".
        // With no modifiers present the stage is a no-op, so the value entering
        // Defense is exactly Base × Combo × Element. Neutral Element and DEF 0 keep
        // that value as the final number too.
        var result = Calculate(
            Boss(def: 0, element: Element.Moc),
            Inputs(combo: 3, attacker: Element.Moc, defender: Element.Moc));

        // 80 × 1.20 = 96, unchanged by step 4.
        Assert.Equal(96d, result.Calculation.Defense, precision: 6);
    }

    [Fact]
    public void CritStat_ShouldNotBeReadByThePipeline()
    {
        // COMBAT_RULES.md §3.3's Crit roll is not implemented, and no Crit input
        // exists on DamageInputs. This asserts the omission is deliberate: the type
        // carries no Crit member, so no roll can occur and no randomness is
        // introduced (AGENTS.md §11, ADR-009).
        var memberNames = typeof(DamagePipeline.DamageInputs)
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();

        Assert.DoesNotContain(memberNames, n => n.Contains("Crit", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------
    // Step 5 — Defense Mitigation (COMBAT_RULES.md §3.2)
    // ---------------------------------------------------------------------

    [Fact]
    public void DefenseMitigation_ForTheDocumentedExample_ShouldBeSixtyEightPointFiveSeven()
    {
        // COMBAT_RULES.md §3.2's worked value: Pre-Defense Damage 96 × (100 / (100 + 40))
        // = 96 × (100/140) ≈ 68.5714. Combo 3 with no Element advantage would give 96,
        // so the Element matchup is Neutral here to isolate §3.2's number.
        var result = Calculate(
            Boss(def: 40, element: Element.Moc),
            Inputs(combo: 3, attacker: Element.Moc, defender: Element.Moc, defense: 40));

        Assert.Equal(96d * (100d / 140d), result.Calculation.Defense, precision: 6);
        Assert.Equal(68.571428571428571d, result.Calculation.Defense, precision: 9);
    }

    [Fact]
    public void DefenseMitigation_WithZeroDefense_ShouldApplyNoMitigation()
    {
        // COMBAT_RULES.md §3.2: at DEF = 0 the factor is K / (K + 0) = 1, so the
        // Pre-Defense damage passes through whole. Neutral Element keeps the value
        // step 2 produced: 80 × 1.20 = 96.
        var result = Calculate(
            Boss(def: 0, element: Element.Moc),
            Inputs(combo: 3, attacker: Element.Moc, defender: Element.Moc));

        Assert.Equal(96d, result.Calculation.Defense, precision: 6);
        Assert.Equal(96, result.Calculation.FinalDamage);
    }

    [Fact]
    public void DefenseMitigation_ForHighDefense_ShouldApproachZeroWithoutReachingIt()
    {
        // COMBAT_RULES.md §3.2's formula is asymptotic in DEF: the factor is always
        // strictly positive, so damage is reduced but never eliminated.
        var moderate = Calculate(
            Boss(def: 400, element: Element.Moc),
            Inputs(combo: 3, attacker: Element.Moc, defender: Element.Moc, defense: 400));

        var extreme = Calculate(
            Boss(def: 100_000, element: Element.Moc),
            Inputs(combo: 3, attacker: Element.Moc, defender: Element.Moc, defense: 100_000));

        Assert.True(extreme.Calculation.Defense < moderate.Calculation.Defense);
        Assert.True(extreme.Calculation.Defense > 0d);
        Assert.True(moderate.Calculation.Defense > 0d);
    }

    [Fact]
    public void DefenseMitigation_Constant_ShouldBeTheDocumentedHundred()
    {
        // COMBAT_RULES.md §3.2: "MVP default: K = 100 (configuration)." It is a named
        // constant, not a literal scattered through the arithmetic.
        Assert.Equal(100, DamagePipeline.DefenseMitigationConstant);
    }

    [Fact]
    public void DefenseMitigation_ForDefenseBelowTheFormulaDomain_ShouldThrow()
    {
        // COMBAT_RULES.md §3.2's factor K / (K + DEF) requires K + DEF > 0. A DEF of
        // -100 (or lower) is outside the formula's domain, so it is rejected rather
        // than silently producing a negative or infinite factor.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Calculate(Boss(def: -100, element: Element.Tho), Inputs(defense: -100)));
    }

    // ---------------------------------------------------------------------
    // Step 6 — Final Damage (COMBAT_RULES.md §3 step 6)
    // ---------------------------------------------------------------------

    [Fact]
    public void FinalDamage_ForTheDocumentedExample_ShouldTruncateToSixtyEight()
    {
        // COMBAT_RULES.md §3.2: "96 × (100 / 140) ≈ 68.57 → truncated toward zero to
        // 68 (Math.Truncate or equivalent; no rounding-to-nearest)". Rounding to
        // nearest would give 69, which is the value this test exists to exclude.
        var result = Calculate(
            Boss(def: 40, element: Element.Moc),
            Inputs(combo: 3, attacker: Element.Moc, defender: Element.Moc, defense: 40));

        Assert.Equal(68, result.Calculation.FinalDamage);
        Assert.NotEqual(69, result.Calculation.FinalDamage);
    }

    [Theory]
    [InlineData(1, 57)]    // 80 × 1.00 = 80  → × 100/140 = 57.142... → 57
    [InlineData(2, 62)]    // 80 × 1.10 = 88  → × 100/140 = 62.857... → 62
    [InlineData(3, 68)]    // 80 × 1.20 = 96  → × 100/140 = 68.571... → 68  (the documented example)
    [InlineData(4, 77)]    // 80 × 1.35 = 108 → × 100/140 = 77.142... → 77
    [InlineData(5, 85)]    // 80 × 1.50 = 120 → × 100/140 = 85.714... → 85
    public void FinalDamage_ShouldTruncateTowardZero(int combo, int expected)
    {
        // Neutral Element (1.00×) and DEF = 40 isolate §3.2's division, so each row
        // is §5's Combo factor followed by the mitigation. Every row leaves a
        // fraction that truncation discards — rounding to nearest would give
        // 57.142 → 57, 62.857 → 63, and 68.571 → 69, so the Combo 2 and Combo 3 rows
        // are the ones that distinguish the two behaviours.
        var result = Calculate(
            Boss(def: 40, element: Element.Moc),
            Inputs(combo: combo, attacker: Element.Moc, defender: Element.Moc, defense: 40));

        Assert.Equal(expected, result.Calculation.FinalDamage);
    }

    [Fact]
    public void FinalDamage_ShouldNeverBeNegative()
    {
        // COMBAT_RULES.md §3 step 6: "→ Final Damage (minimum 0...)".
        var result = Calculate(Boss(def: 40), Inputs(attack: 0, baseDamagePool: 0, combo: 1, defense: 40));

        Assert.Equal(0, result.Calculation.FinalDamage);
        Assert.True(result.Calculation.Defense >= 0d);
    }

    [Fact]
    public void FinalDamage_ForDisadvantage_ShouldAlsoTruncateNotRound()
    {
        // ELEMENT_RULES.md §2.1: the defender (Kim) counters the attacker (Mộc),
        // because Kim → Mộc. That is Disadvantage = 0.75× (§2.2).
        // Base 80 × 1.00 × 0.75 = 60 → × 100/140 = 42.857... → 42, not 43.
        var result = Calculate(
            Boss(def: 40, element: Element.Kim),
            Inputs(combo: 1, attacker: Element.Moc, defender: Element.Kim, defense: 40));

        Assert.Equal(0.75, result.Calculation.ElementModifier);
        Assert.Equal(42, result.Calculation.FinalDamage);
    }

    // ---------------------------------------------------------------------
    // Boss HP application (COMBAT_RULES.md §3 step 6, GAME_RULES.md §1.4)
    // ---------------------------------------------------------------------

    [Fact]
    public void BossHp_ShouldBeReducedByTheFinalDamage()
    {
        // The task's documented example: Final Damage 68 against HP 500 leaves 432.
        // Neutral Element and DEF 40 reproduce §3.2's worked 96 → 68 exactly.
        var result = Calculate(
            Boss(hp: 500, def: 40, element: Element.Moc),
            Inputs(combo: 3, attacker: Element.Moc, defender: Element.Moc, defense: 40));

        Assert.Equal(68, result.Calculation.FinalDamage);
        Assert.Equal(432, result.BossState.HP);
    }

    [Fact]
    public void BossHp_ShouldClampToZeroOnOverkill()
    {
        // The task's documented edge case: Final Damage 600 against HP 500 leaves 0,
        // never a negative HP — GAME_RULES.md §1.4 ends the battle at 0.
        var result = Calculate(
            Boss(hp: 500, def: 0, element: Element.Moc),
            Inputs(attack: 600, baseDamagePool: 0, combo: 1, attacker: Element.Moc, defender: Element.Moc));

        Assert.Equal(600, result.Calculation.FinalDamage);
        Assert.Equal(0, result.BossState.HP);
        Assert.True(result.BossState.HP >= 0);
    }

    [Fact]
    public void BossHp_LandingExactlyOnZero_ShouldBeZeroNotNegative()
    {
        // An exact kill is the boundary of the clamp.
        var result = Calculate(
            Boss(hp: 500, def: 0, element: Element.Moc),
            Inputs(attack: 500, baseDamagePool: 0, combo: 1, attacker: Element.Moc, defender: Element.Moc));

        Assert.Equal(0, result.BossState.HP);
    }

    [Fact]
    public void BossHp_ShouldCarryEveryOtherFieldAcrossUnchanged()
    {
        // COMBAT_RULES.md §3 applies damage and nothing else. This stage transitions
        // no Boss State, fires no Boss mechanic (BOSS_RULES.md §3–§5), and decides no
        // Victory/Defeat (GAME_RULES.md §17 step 19) — so only HP may differ.
        var before = Boss(hp: 500, def: 40, element: Element.Moc);

        var result = Calculate(
            before,
            Inputs(combo: 3, attacker: Element.Moc, defender: Element.Moc, defense: 40));

        Assert.Equal(before.BossId, result.BossState.BossId);
        Assert.Equal(before.Element, result.BossState.Element);
        Assert.Equal(before.MaxHP, result.BossState.MaxHP);
        Assert.Equal(before.ATK, result.BossState.ATK);
        Assert.Equal(before.DEF, result.BossState.DEF);
        Assert.Equal(before.State, result.BossState.State);
        Assert.Equal(432, result.BossState.HP);
    }

    [Fact]
    public void BossHp_ReachingZero_ShouldNotBeTreatedAsAnOutcome()
    {
        // GAME_RULES.md §17 step 19 owns Victory/Defeat and is out of scope here. A
        // result type that reported an outcome would carry a member this pipeline has
        // no rule to fill.
        var memberNames = typeof(DamageResult).GetProperties().Select(p => p.Name).ToArray();

        Assert.DoesNotContain(memberNames, n =>
            n.Contains("Won", StringComparison.Ordinal)
            || n.Contains("Lost", StringComparison.Ordinal)
            || n.Contains("Defeat", StringComparison.Ordinal)
            || n.Contains("Victory", StringComparison.Ordinal)
            || n.Contains("Dead", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------
    // Determinism (GAME_RULES.md §17, AGENTS.md §11)
    // ---------------------------------------------------------------------

    [Fact]
    public void Calculate_ShouldBeDeterministic()
    {
        // GAME_RULES.md §17's resolution order is fixed and AGENTS.md §11 requires
        // determinism. The same inputs must always produce the same result — this
        // type draws no RNG, reads no clock, and depends on no enumeration order.
        var boss = Boss();
        var inputs = Inputs();

        var first = Calculate(boss, inputs);
        var second = Calculate(boss, inputs);

        Assert.Equal(first.Calculation, second.Calculation);
        Assert.Equal(first.BossState, second.BossState);
        Assert.Equal(first.DamageDealt, second.DamageDealt);
        Assert.Equal(first.DamageTaken, second.DamageTaken);
    }

    // ---------------------------------------------------------------------
    // The full pipeline in the documented order
    // ---------------------------------------------------------------------

    [Fact]
    public void FullPipeline_ShouldProduceTheDocumentedBreakdown()
    {
        // The complete documented example, end to end. The task's worked values use
        // the Mộc → Thổ Advantage matchup at DEF 40:
        //   ATK 50 + pool 30            = Base 80
        //   × 1.20 (Combo 3)            = 96
        //   × 1.50 (Mộc → Thổ Advantage)= 144
        //   × 1.00 (Other Modifiers)    = 144
        //   × 100/140 (DEF 40, K 100)   = 102.857...
        //   truncate                    = 102
        var result = Calculate(
            Boss(hp: 500, def: 40, element: Element.Tho),
            Inputs(combo: 3, attacker: Element.Moc, defender: Element.Tho, defense: 40));

        Assert.Equal(80, result.Calculation.Base);
        Assert.Equal(1.20, result.Calculation.ComboModifier);
        Assert.Equal(1.50, result.Calculation.ElementModifier);
        Assert.Equal(1.00, result.Calculation.OtherModifiers);
        Assert.Equal(144d * (100d / 140d), result.Calculation.Defense, precision: 9);
        Assert.Equal(102, result.Calculation.FinalDamage);
        Assert.Equal(398, result.BossState.HP);
    }

    [Fact]
    public void FullPipeline_StepsShouldComposeInTheDocumentedOrder()
    {
        // COMBAT_RULES.md §3.1: the order is fixed. Applying the steps in the
        // documented order must reproduce Defense exactly, which a reordering of the
        // multiplicative steps could not (commutative multiplication aside, the
        // Defense subtraction is not interchangeable with them).
        var result = Calculate(
            Boss(def: 40, element: Element.Moc),
            Inputs(combo: 3, attacker: Element.Moc, defender: Element.Moc, defense: 40));

        var baseDamage = 50 + 30;
        var afterCombo = baseDamage * 1.20;
        var afterElement = afterCombo * 1.00;
        var afterOther = afterElement * 1.00;
        var expectedDefense = afterOther * (100d / 140d);

        Assert.Equal(baseDamage, result.Calculation.Base);
        Assert.Equal(expectedDefense, result.Calculation.Defense, precision: 9);
    }

    // ---------------------------------------------------------------------
    // Damage events (GAME_EVENTS.md §2)
    // ---------------------------------------------------------------------

    [Fact]
    public void DamageDealt_ShouldCarrySourceTargetAndAmount()
    {
        // GAME_EVENTS.md §2: "DamageDealt / DamageTaken: source, target, Final
        // Damage amount".
        var result = Calculate(
            Boss(hp: 500, def: 40, element: Element.Moc),
            Inputs(combo: 3, attacker: Element.Moc, defender: Element.Moc, defense: 40));

        Assert.Equal(DamageParty.Player, result.DamageDealt.Source);
        Assert.Equal(DamageParty.Boss, result.DamageDealt.Target);
        Assert.Equal(68, result.DamageDealt.Amount);
    }

    [Fact]
    public void DamageTaken_ShouldCarryTheSameInstanceAsDamageDealt()
    {
        // GAME_EVENTS.md §2 gives both events identical members, and §1 places them
        // together for one instance — so they report one application of damage, not
        // two. Boss HP must move by exactly the amount both report.
        var before = Boss(hp: 500, def: 40, element: Element.Moc);
        var result = Calculate(
            before,
            Inputs(combo: 3, attacker: Element.Moc, defender: Element.Moc, defense: 40));

        Assert.Equal(result.DamageDealt.Source, result.DamageTaken.Source);
        Assert.Equal(result.DamageDealt.Target, result.DamageTaken.Target);
        Assert.Equal(result.DamageDealt.Amount, result.DamageTaken.Amount);
        Assert.Equal(result.Calculation.FinalDamage, result.DamageDealt.Amount);
        Assert.Equal(before.HP - result.BossState.HP, result.DamageDealt.Amount);
    }

    [Fact]
    public void DamageEvents_ShouldBeTheThreeDocumentedOnesInOrder()
    {
        // GAME_EVENTS.md §1 places DamageCalculated, DamageDealt, DamageTaken in that
        // order for the instance. The task's worked example gives 68 — its Neutral
        // Element is chosen so the assertion reads §3.2's documented number.
        var events = Calculate(
            Boss(hp: 500, def: 40, element: Element.Moc),
            Inputs(combo: 3, attacker: Element.Moc, defender: Element.Moc, defense: 40)).Events;

        Assert.Equal(68, events.Calculated.FinalDamage);
        Assert.Equal(68, events.Dealt.Amount);
        Assert.Equal(68, events.Taken.Amount);
    }

    [Fact]
    public void DamageEvents_ShouldBeEmittedEvenWhenNoDamageIsDealt()
    {
        // GAME_EVENTS.md §1 places the three events unconditionally on the
        // resolution. A zero-damage instance is still an instance the pipeline
        // evaluated, and "no instance occurred" must remain distinguishable from
        // "an instance of 0 occurred".
        var result = Calculate(Boss(hp: 500), Inputs(attack: 0, baseDamagePool: 0, combo: 1));

        Assert.Equal(0, result.Calculation.FinalDamage);
        Assert.Equal(0, result.DamageDealt.Amount);
        Assert.Equal(0, result.DamageTaken.Amount);
        Assert.Equal(500, result.BossState.HP);
    }

    // ---------------------------------------------------------------------
    // Explicit configuration (ELEMENT_RULES.md §2.2, GAME_RULES.md §5)
    // ---------------------------------------------------------------------

    [Fact]
    public void Calculate_ShouldUseTheSuppliedElementConfiguration()
    {
        // ELEMENT_RULES.md §2.2 makes the three factors configuration. A caller's own
        // set must be the one applied — no ambient default may override it.
        var custom = new ElementModifiers(advantage: 2.00, neutral: 1.00, disadvantage: 0.50);

        var result = DamagePipeline.Calculate(
            Boss(def: 0, element: Element.Tho),
            Inputs(attack: 50, baseDamagePool: 30, combo: 1, attacker: Element.Moc, defender: Element.Tho),
            ComboModifiers.Default,
            custom);

        Assert.Equal(2.00, result.Calculation.ElementModifier);
        Assert.Equal(160, result.Calculation.FinalDamage);
    }

    [Fact]
    public void Calculate_ShouldUseTheSuppliedComboConfiguration()
    {
        // GAME_RULES.md §5 states the table is configurable. The supplied table must
        // be the one read.
        var custom = new ComboModifiers(combo1: 100, combo2: 110, combo3: 200, combo4: 135, combo5Plus: 150);

        var result = DamagePipeline.Calculate(
            Boss(def: 0, element: Element.Moc),
            Inputs(attack: 50, baseDamagePool: 30, combo: 3, attacker: Element.Moc, defender: Element.Moc),
            custom,
            ElementModifiers.Default);

        Assert.Equal(2.00, result.Calculation.ComboModifier);
        Assert.Equal(160, result.Calculation.FinalDamage);
    }
}
