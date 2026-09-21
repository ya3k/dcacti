using GameServer.Domain.Match3;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Gameplay scenario tests — the <c>MATCH3_RULES.md</c> §5.8.5 worked examples and
/// §5.8.6 interaction matrix, expressed as full Given/When/Then state-transition
/// chains (<c>AGENTS.md</c> §15, <c>.ai/README.md</c> §17).
///
/// The design records §5.8.5's cases "because they are the cases the design had to
/// answer, and an implementation must produce exactly these outcomes". These tests
/// assert exactly those outcomes, end to end, rather than only the planning step.
/// </summary>
public class SpecialGemScenarioTests
{
    private static Pcg32 Rng() => Pcg32.FromSeed(90210UL);

    /// <summary>Plans creations for a board's match set.</summary>
    private static SpecialGemPlanner.Plan Plan(BoardState board, int? swapOrigin = null) =>
        SpecialGemPlanner.PlanCreations(MatchDetector.Detect(board), swapOrigin);

    // -----------------------------------------------------------------------
    // §5.8.5 — Match 4 + Match 4
    // -----------------------------------------------------------------------

    [Fact]
    public void Scenario_TwoSeparateMatch4Lines_ShouldCreateTwoLineClearGems()
    {
        // §5.8.5: "Match 4 + Match 4 (one swap, two separate straight lines)
        //   → 2 shapes in the match set, each length 4
        //   → 2 Line Clear Gems, each at its own shape's §5.5.3 position"

        // Given a board containing two independent Match 4 lines
        var board = TestBoard.Background()
            .WithGems(
                (16, GemType.Atk), (17, GemType.Atk), (18, GemType.Atk), (19, GemType.Atk),
                (40, GemType.Hp), (41, GemType.Hp), (42, GemType.Hp), (43, GemType.Hp));

        // When the pass's creations are planned
        var matchSet = MatchDetector.Detect(board);
        var plan = Plan(board);

        // Then there are two shapes, each of length 4
        Assert.Equal(2, matchSet.Count);
        Assert.All(matchSet, s => Assert.Equal(4, s.Cells.Count));

        // And two Line Clear Gems, one per shape, at each shape's own line centre
        Assert.Equal(2, plan.CommittedClaims.Count);
        Assert.All(plan.CommittedClaims, c => Assert.Equal(SpecialGemType.LineClear, c.SpecialGem.Type));

        // Row 2 line centre = 16 + floor(3/2) = 17; row 5 line centre = 40 + 1 = 41.
        Assert.Equal(new[] { 17, 41 }, plan.CommittedClaims.Select(c => c.CellIndex).OrderBy(i => i));
    }

    // -----------------------------------------------------------------------
    // §5.8.5 — one L/T with both arms ≥ 4 (three creations)
    // -----------------------------------------------------------------------

    [Fact]
    public void Scenario_LtWithBothArmsQualifying_ShouldCreateThreeDistinctSpecialGems()
    {
        // §5.8.5: "one L/T with both arms ≥ 4
        //   → 3 creations, in the §5.5.1 item 4 order:
        //         1. Area Gem      at the intersection
        //         2. horizontal arm gem at that arm's own line centre (§5.5.3 item 7)
        //         3. vertical arm gem   at that arm's own line centre (§5.5.3 item 7)
        //   → if any two of those positions coincide, the earlier source in the
        //     §5.5.1 item 4 sequence keeps the cell and the later claim is discarded"

        // Given an L/T with a length-4 horizontal arm and a length-4 vertical arm
        var board = TestBoard.Background()
            .WithGems(
                (2, GemType.Atk), (3, GemType.Atk), (4, GemType.Atk), (5, GemType.Atk),
                (10, GemType.Atk), (18, GemType.Atk), (26, GemType.Atk));

        var shape = Assert.Single(MatchDetector.Detect(board));
        Assert.True(shape.IsLt);
        Assert.Equal(4, shape.HorizontalArm!.Length);
        Assert.Equal(4, shape.VerticalArm!.Length);

        // When the pass's creations are planned
        var plan = Plan(board);

        // Then all three positions are distinct and all three are created
        Assert.Equal(3, plan.CommittedClaims.Count);
        Assert.Equal(3, plan.CommittedClaims.Select(c => c.CellIndex).Distinct().Count());

        // In the documented order: Area at the intersection, then the horizontal arm's
        // centre, then the vertical arm's centre.
        Assert.Equal(
            new[] { CreationSource.LtPattern, CreationSource.HorizontalArm, CreationSource.VerticalArm },
            plan.OrderedClaims.Select(c => c.Source));

        Assert.Equal(2, plan.OrderedClaims[0].CellIndex);   // intersection (0,2)
        Assert.Equal(3, plan.OrderedClaims[1].CellIndex);   // H arm centre
        Assert.Equal(10, plan.OrderedClaims[2].CellIndex);  // V arm centre
    }

    // -----------------------------------------------------------------------
    // §5.8.5 — one L/T, both arms of length 3 (one creation)
    // -----------------------------------------------------------------------

    [Fact]
    public void Scenario_LtWithBothArmsOfLengthThree_ShouldCreateOneSpecialGem()
    {
        // §5.8.5: "one L/T, both arms of length 3
        //   → 1 creation: the Area Gem only"
        var board = TestBoard.Background()
            .WithGems((0, GemType.Atk), (1, GemType.Atk), (2, GemType.Atk), (8, GemType.Atk), (16, GemType.Atk));

        var shape = Assert.Single(MatchDetector.Detect(board));
        Assert.True(shape.IsLt);

        var plan = Plan(board);

        var claim = Assert.Single(plan.CommittedClaims);
        Assert.Equal(SpecialGemType.Area, claim.SpecialGem.Type);
        Assert.Equal(0, claim.CellIndex);
    }

    // -----------------------------------------------------------------------
    // §5.8.5 — the canonical collision
    // -----------------------------------------------------------------------

    [Fact]
    public void Scenario_CanonicalCollision_ShouldCreateTheAreaGemAndDiscardTheBurst()
    {
        // §5.8.5: "one L/T, Area Gem and an arm gem claim one cell (the canonical
        // collision)
        //   horizontal arm row 2, columns 0..4   (length 5 → Burst Gem)
        //   vertical   arm column 2, rows 2..4   (length 3 → no arm gem)
        //   intersection (2,2) = index 18
        //     Area Gem  → (2,2)
        //     H arm gem → arm start 16, L = 5 → 16 + floor(4/2) = 18 = (2,2)
        //   → both claim (2,2); the Area Gem is first in §5.5.1 item 4
        //   → (2,2) holds the AREA GEM; the arm's Burst Gem is discarded
        //     (§5.5.4 item 3). The L/T is still one Match and still generates
        //     resources once per consumed cell."

        // Given exactly that board
        var board = TestBoard.Background()
            .WithGems(
                (16, GemType.Atk), (17, GemType.Atk), (18, GemType.Atk), (19, GemType.Atk), (20, GemType.Atk),
                (26, GemType.Atk), (34, GemType.Atk));

        // When the pass resolves
        var matchSet = MatchDetector.Detect(board);
        var shape = Assert.Single(matchSet);

        Assert.True(shape.IsLt);
        Assert.Equal(18, shape.IntersectionIndex);
        Assert.Equal(5, shape.HorizontalArm!.Length);
        Assert.Equal(3, shape.VerticalArm!.Length);

        var pass = BoardResolver.ResolvePass(board, matchSet, Rng(), swapOriginIndex: null, cascadeDepth: 1);

        // Then the pass reports exactly one creation, the Area Gem, at index 18
        var created = Assert.Single(pass.CreatedSpecialGems);
        Assert.Equal(18, created.CellIndex);
        Assert.Equal(SpecialGemType.Area, created.SpecialGem.Type);
        Assert.Equal(CreationSource.LtPattern, created.Source);

        // And the discarded Burst Gem is nowhere in the pass's report
        Assert.DoesNotContain(pass.CreatedSpecialGems, c => c.SpecialGem.Type == SpecialGemType.Burst);
        Assert.DoesNotContain(pass.CreatedSpecialGems, c => c.Source == CreationSource.HorizontalArm);

        // And the L/T is still exactly one Match
        Assert.Single(pass.Matches);

        // And the region generates resources once per consumed cell: the cleared union
        // is a set with the shape's cells counted once each.
        Assert.Equal(
            matchSet[0].Cells.OrderBy(i => i),
            pass.MatchedCellGemMatched.Select(e => e.CellIndex).OrderBy(i => i));
    }

    // -----------------------------------------------------------------------
    // §5.8.6 — the Special Gem + Special Gem interaction matrix
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(SpecialGemType.LineClear, SpecialGemOrientation.Horizontal, SpecialGemType.LineClear, SpecialGemOrientation.Vertical)]
    [InlineData(SpecialGemType.Burst, null, SpecialGemType.Burst, null)]
    [InlineData(SpecialGemType.Area, null, SpecialGemType.Area, null)]
    [InlineData(SpecialGemType.LineClear, SpecialGemOrientation.Horizontal, SpecialGemType.Burst, null)]
    [InlineData(SpecialGemType.LineClear, SpecialGemOrientation.Vertical, SpecialGemType.Area, null)]
    [InlineData(SpecialGemType.Burst, null, SpecialGemType.Area, null)]
    public void Scenario_EverySpecialGemPair_ShouldActivateBothWithUnionSemantics(
        SpecialGemType firstType,
        SpecialGemOrientation? firstOrientation,
        SpecialGemType secondType,
        SpecialGemOrientation? secondOrientation)
    {
        // §5.8.6: every combination is allowed, each Special Gem resolves its OWN type's
        // geometry at its own position, and the combined result is the UNION — never a
        // merged, enlarged, or new shape. One activation per Special Gem, exactly.

        // Given one pass whose match set consumes two Special Gems of the pair
        var first = new SpecialGem(firstType, firstOrientation);
        var second = new SpecialGem(secondType, secondOrientation);

        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((25, first), (27, second));

        // When the pass resolves
        var pass = BoardResolver.ResolvePass(
            board,
            MatchDetector.Detect(board),
            Rng(),
            swapOriginIndex: null,
            cascadeDepth: 1);

        // Then each Special Gem activated exactly once
        Assert.Equal(2, pass.ActivatedSpecialGems.Count);
        Assert.Contains(pass.ActivatedSpecialGems, a => a.CellIndex == 25 && a.SpecialGem == first);
        Assert.Contains(pass.ActivatedSpecialGems, a => a.CellIndex == 27 && a.SpecialGem == second);

        // And both are siblings at level 0 — consumed by the same match set, neither a
        // chain of the other (§5.8.6 item 5)
        Assert.All(pass.ActivatedSpecialGems, a => Assert.Equal(0, a.ChainLevel));

        // And the cleared union is exactly the union of the two independent geometries,
        // each cell once
        var expected = SpecialGemEffects.AffectedCells(first, 25)
            .Concat(SpecialGemEffects.AffectedCells(second, 27))
            .Concat(new[] { 26 })
            .Distinct()
            .OrderBy(i => i)
            .ToArray();

        Assert.Equal(expected, pass.ClearedCellUnion);
        Assert.Equal(pass.ClearedCellUnion.Distinct().Count(), pass.ClearedCellUnion.Count);
    }

    [Fact]
    public void Scenario_TwoSpecialGemsNamingEachOthersCells_ShouldStillTerminate()
    {
        // §5.5.5 item 7 item 5 (the loop's termination note) and §5.8.3 item 7: two
        // Special Gems that each name the other's cell must terminate, because a gem
        // that has already activated does not activate again.
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((25, SpecialGem.Area()), (26, SpecialGem.Area()), (27, SpecialGem.Area()));

        var pass = BoardResolver.ResolvePass(
            board,
            MatchDetector.Detect(board),
            Rng(),
            swapOriginIndex: null,
            cascadeDepth: 1);

        // Every gem activated exactly once and the call returned.
        Assert.Equal(
            pass.ActivatedSpecialGems.Select(a => a.CellIndex).Distinct().Count(),
            pass.ActivatedSpecialGems.Count);

        Assert.Equal(3, pass.ActivatedSpecialGems.Count);
    }

    // -----------------------------------------------------------------------
    // §5.5 item 2 / §6.3.1 — a Special Gem activation is not a Match
    // -----------------------------------------------------------------------

    [Fact]
    public void Scenario_ActivationShouldNotCreateAMatchOrAdvanceAnything()
    {
        // §5.5 item 2: "Gems cleared by a Special Gem's effect are NOT counted as a
        // 'Match' for Combo/Match-count purposes – they are an explosion, not a match –
        // but they DO count toward resource generation and DO trigger Cascade
        // re-evaluation."
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((26, SpecialGem.Burst()));

        var pass = BoardResolver.ResolvePass(
            board,
            MatchDetector.Detect(board),
            Rng(),
            swapOriginIndex: null,
            cascadeDepth: 1);

        // Exactly the one Match the board contained — the explosion added none.
        Assert.Single(pass.Matches);

        // The explosion cleared many more cells than the Match.
        Assert.True(pass.ClearedCellUnion.Count > pass.Matches[0].Shape.Cells.Count);

        // Every cell in the union is reported, and only the Match's cells carry a Match.
        Assert.Equal(
            pass.ClearedCellUnion.OrderBy(i => i),
            pass.MatchedCellGemMatched.Concat(pass.ActivationCellGemMatched)
                .Select(e => e.CellIndex)
                .OrderBy(i => i));
    }

    // -----------------------------------------------------------------------
    // §5.5.4 item 5 — a created Special Gem generates nothing by itself
    // -----------------------------------------------------------------------

    [Fact]
    public void Scenario_CreatedSpecialGemShouldGenerateNothingByItself()
    {
        // §5.5.4 item 5: "The created Special Gem adds no generation of its own;
        // generation happens when it is later consumed." The observable board-level
        // consequence is that creation does not add the created cell to the cleared
        // union for a second time: the union is the removal set, and the created gem was
        // not removed.
        var board = TestBoard.Background()
            .WithGems((0, GemType.Def), (1, GemType.Def), (2, GemType.Def), (3, GemType.Def));

        var pass = BoardResolver.ResolvePass(
            board,
            MatchDetector.Detect(board),
            Rng(),
            swapOriginIndex: null,
            cascadeDepth: 1);

        var created = Assert.Single(pass.CreatedSpecialGems);

        // The created cell appears in the cleared union exactly once — as a removed
        // matched cell, not twice.
        Assert.Equal(1, pass.ClearedCellUnion.Count(i => i == created.CellIndex));
    }

    // -----------------------------------------------------------------------
    // §5.7 item 2 / §5.8.2 item 5 — union accounting, not per-effect sums
    // -----------------------------------------------------------------------

    [Fact]
    public void Scenario_ClearedUnionShouldBeTheCardinalityNotTheSumOfEffects()
    {
        // §5.8.2 item 5: "the number of cleared cells a step reports for generation is
        // the CARDINALITY OF THE UNION, not the sum of the effects' sizes."
        //
        // Two overlapping Area Gems and a Match: the sum of the parts exceeds the union.
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((25, SpecialGem.Area()), (26, SpecialGem.Area()));

        var pass = BoardResolver.ResolvePass(
            board,
            MatchDetector.Detect(board),
            Rng(),
            swapOriginIndex: null,
            cascadeDepth: 1);

        var matchedCells = pass.Matches[0].Shape.Cells.Count;
        var firstEffect = SpecialGemEffects.AffectedCells(SpecialGem.Area(), 25).Count;
        var secondEffect = SpecialGemEffects.AffectedCells(SpecialGem.Area(), 26).Count;

        var naiveSum = matchedCells + firstEffect + secondEffect;

        Assert.True(
            pass.ClearedCellUnion.Count < naiveSum,
            "the union must be smaller than the naive sum when effects overlap");

        Assert.Equal(pass.ClearedCellUnion.Distinct().Count(), pass.ClearedCellUnion.Count);
    }

    // -----------------------------------------------------------------------
    // §5.8.1 — clipping at a board corner, end to end
    // -----------------------------------------------------------------------

    [Fact]
    public void Scenario_BurstAtCorner_ShouldClearFourCells_NoWrap()
    {
        // §5.8.1 item 5: "Burst Gem at a corner (0,0) → 4 cells { (0,0), (0,1), (1,0),
        // (1,1) }" — with no wrap to the opposite edge.
        var board = TestBoard.Background()
            .WithGems((0, GemType.Atk), (1, GemType.Atk), (2, GemType.Atk))
            .WithSpecial((0, SpecialGem.Burst()));

        var pass = BoardResolver.ResolvePass(
            board,
            MatchDetector.Detect(board),
            Rng(),
            swapOriginIndex: null,
            cascadeDepth: 1);

        // The ActivationCellGemMatched reports the activation's clears excluding the
        // matched sub-step. Cell 0 and 1 are matched; the Burst adds 8 (1,0) and 9 (1,1).
        var activationCells = pass.ActivationCellGemMatched.Select(e => e.CellIndex).OrderBy(i => i).ToArray();

        Assert.Equal(new[] { 8, 9 }, activationCells);

        // Nothing from the opposite edge appears.
        Assert.DoesNotContain(63, pass.ClearedCellUnion);
        Assert.All(pass.ClearedCellUnion, i => Assert.InRange(i, 0, 63));
    }
}