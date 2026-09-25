using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// The canonical Pet Level derivation — <c>PET_RULES.md</c> §5, ADR-012
/// items 3–4.
///
/// <code>
/// Rule (PET_RULES.md §5)
///  ↓
/// Scenario (Given Player Level L and multiplier M, When Derive, Then
///           clamp(floor(L × M), 1, 50))
///  ↓
/// Test
/// </code>
///
/// The three authoritative worked examples from §5 are asserted exactly as
/// written. No example number is invented; any additional boundary case is
/// derived from the documented operation order (multiply → floor → clamp).
/// </summary>
public class PetLevelDerivationTests
{
    // -----------------------------------------------------------------------
    // PET_RULES.md §5 — the three authoritative worked examples
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(3, 1.5, 4)]    // 3 × 1.5 = 4.5 → floor = 4 → clamp = 4
    [InlineData(40, 2.0, 50)]  // 40 × 2 = 80 → floor = 80 → clamp = 50
    [InlineData(1, 0.5, 1)]    // 1 × 0.5 = 0.5 → floor = 0 → clamp = 1
    public void Derive_ShouldMatchTheAuthoritativeWorkedExamples(
        int playerLevel,
        decimal multiplier,
        int expected)
    {
        Assert.Equal(expected, PetLevelDerivation.Derive(playerLevel, multiplier));
    }

    // -----------------------------------------------------------------------
    // PET_RULES.md §5 item 1 — operation order: multiply → floor → clamp
    // -----------------------------------------------------------------------

    [Fact]
    public void Derive_ShouldFloorNotRound()
    {
        // 3 × 1.5 = 4.5. Rounding would produce 5; floor produces 4.
        // §5 requires floor specifically — never round, never ceil.
        Assert.Equal(4, PetLevelDerivation.Derive(3, 1.5m));
        Assert.Equal(4, PetLevelDerivation.Derive(3, 1.49m)); // 4.47 → 4
        Assert.Equal(4, PetLevelDerivation.Derive(3, 1.6m));  // 4.8 → 4 (round → 5)
    }

    [Fact]
    public void Derive_ShouldFloorTheProductBeforeClamping()
    {
        // Clamp is always last: floor(1 × 0.5) = 0, then clamp to 1.
        // Clamping first would give clamp(0.5) = 0.5 → floor → 0, which is
        // outside the documented range — §5 requires floor-then-clamp.
        Assert.Equal(1, PetLevelDerivation.Derive(1, 0.5m));
    }

    [Fact]
    public void Derive_ShouldClampTheFlooredResultToTheDocumentedRange()
    {
        // Below lower bound: floor(1 × 0.5) = 0 → clamp to 1.
        Assert.Equal(1, PetLevelDerivation.Derive(1, 0.5m));

        // Above upper bound: floor(40 × 2) = 80 → clamp to 50.
        Assert.Equal(50, PetLevelDerivation.Derive(40, 2m));

        // Exactly at the bound: floor(50 × 1) = 50 → no clamp.
        Assert.Equal(50, PetLevelDerivation.Derive(50, 1m));

        // Exactly at the lower bound: floor(1 × 1) = 1 → no clamp.
        Assert.Equal(1, PetLevelDerivation.Derive(1, 1m));
    }

    [Fact]
    public void Derive_ShouldAlwaysReturnAValueWithinTheDocumentedRange()
    {
        // ADR-012 item 4: Pet Level ∈ [1, 50] for any legal input.
        // Both the lowest and highest documented Player Levels are swept
        // across multipliers below, at, and above 1.
        foreach (var playerLevel in new[] { Player.MinLevel, 25, Player.MaxLevel })
        {
            foreach (var multiplier in new[] { 0.1m, 0.5m, 1m, 1.5m, 2m, 3m })
            {
                var result = PetLevelDerivation.Derive(playerLevel, multiplier);

                Assert.InRange(result, Player.MinLevel, Player.MaxLevel);
            }
        }
    }

    [Fact]
    public void Derive_ShouldAllowMultipliersBelowOne()
    {
        // PET_RULES.md §5 item 1 / DATABASE.md §3: multiplier > 0 — values
        // below 1 are legal (a Pet may lag its owner). floor(10 × 0.5) = 5.
        Assert.Equal(5, PetLevelDerivation.Derive(10, 0.5m));
    }

    // -----------------------------------------------------------------------
    // PET_RULES.md §5 item 1 / DATABASE.md §3 — multiplier must be > 0
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    [InlineData(-1)]
    public void Derive_ShouldRejectZeroAndNegativeMultipliers(decimal invalidMultiplier)
    {
        // Zero and negative multipliers are illegal per §5 item 1 and
        // DATABASE.md §3 (PetLevelMultiplier > 0).
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PetLevelDerivation.Derive(1, invalidMultiplier));
    }

    // -----------------------------------------------------------------------
    // ADR-012 item 6 / PET_RULES.md §5 item 4 — no Pet XP path
    // -----------------------------------------------------------------------

    [Fact]
    public void Derive_ShouldHaveNoXpParameterOrLevelUpStep()
    {
        // The canonical derivation takes exactly two inputs — Player Level
        // and the definition multiplier. There is no XP parameter, no
        // cumulative step, and no rate of increase: Pets have no independent
        // XP progression (ADR-012 item 6).
        var parameters = typeof(PetLevelDerivation)
            .GetMethod(nameof(PetLevelDerivation.Derive))!
            .GetParameters()
            .Select(p => p.Name)
            .ToArray();

        Assert.Equal(new[] { "playerLevel", "petLevelMultiplier" }, parameters);
    }

    [Fact]
    public void Pet_ShouldExposeNoXpOrEvolutionField()
    {
        // DATABASE.md §1 / ADR-012 items 5–6: no XP column, no Evolution
        // field, no Tier/Star derivation from Player Level.
        var fields = typeof(Pet).GetProperties().Select(p => p.Name).ToArray();

        foreach (var forbidden in new[] { "XP", "Xp", "Experience", "Evolution", "Evolves" })
        {
            Assert.DoesNotContain(forbidden, fields);
        }
    }

    [Fact]
    public void PetLevelRange_ShouldBeTheDocumentedOneToFifty()
    {
        // ADR-012 item 4: both Player Level and Pet Level share the
        // documented 1–50 range. Neither bound may drift.
        Assert.Equal(1, Player.MinLevel);
        Assert.Equal(50, Player.MaxLevel);
    }
}
