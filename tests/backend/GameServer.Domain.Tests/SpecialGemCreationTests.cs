using GameServer.Domain.Match3;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Special Gem creation, placement, collision, and orientation tests
/// (<c>MATCH3_RULES.md</c> §5.2, §5.3, §5.4, §5.5.1–§5.5.4, §5.8.4, §5.8.5).
///
/// Every scenario is a Given/When/Then over documented board states.
///
/// <b>Fixture convention.</b> Boards start from <see cref="TestBoard.Background"/>
/// and use coordinates verified to produce <b>exactly</b> the declared shape — the
/// detector is asserted against the intended geometry in the scenarios that depend
/// on it, so a fixture that drifts fails loudly instead of silently weakening the
/// assertion.
///
/// Verified slots on <c>TestBoard.Background()</c>:
/// <code>
/// straight H4   row 0 cols 0..3   start  0, centre 1,  LineClear horizontal
/// straight H6   row 0 cols 0..5   start  0, centre 2,  Burst
/// straight V4   col 0 rows 0..3   start  0, centre 8,  LineClear vertical
/// straight H8   row 0 cols 0..7   start  0,          Burst
/// L/T H4 V3     H row 0 cols 2..5 + V col 2 rows 0..2, intersection  2
/// L/T H5 V3     H row 0 cols 0..4 + V col 0 rows 0..2, intersection  0
/// L/T H3 V5     H row 0 cols 0..2 + V col 0 rows 0..4, intersection  0
/// L/T H4 V4     H row 0 cols 2..5 + V col 2 rows 0..3, intersection  2
/// L/T H5 V5     H row 0 cols 0..4 + V col 0 rows 0..4, intersection  0
/// L/T H7 V3     H row 0 cols 0..6 + V col 0 rows 0..2, intersection  0
/// canonical     H row 2 cols 0..4 + V col 2 rows 2..4, intersection 18
/// </code>
/// </summary>
public class SpecialGemCreationTests
{
    /// <summary>Plans creations for a board's detected match set.</summary>
    private static SpecialGemPlanner.Plan Plan(BoardState board, int? swapOrigin = null) =>
        SpecialGemPlanner.PlanCreations(MatchDetector.Detect(board), swapOrigin);

    /// <summary>Asserts the board holds exactly one L/T with the given arm lengths.</summary>
    private static MatchShape SingleLt(BoardState board, int horizontalLength, int verticalLength)
    {
        var shape = Assert.Single(MatchDetector.Detect(board));
        Assert.True(shape.IsLt, $"expected an L/T shape, got {shape}");
        Assert.Equal(horizontalLength, shape.HorizontalArm!.Length);
        Assert.Equal(verticalLength, shape.VerticalArm!.Length);
        return shape;
    }

    /// <summary>Asserts the board holds exactly one straight run of the given length.</summary>
    private static MatchShape SingleStraight(BoardState board, int length)
    {
        var shape = Assert.Single(MatchDetector.Detect(board));
        Assert.False(shape.IsLt, $"expected a straight shape, got {shape}");
        Assert.Equal(length, shape.Cells.Count);
        return shape;
    }

    // -----------------------------------------------------------------------
    // §5.2 — Match 4 creates one Line Clear Gem
    // -----------------------------------------------------------------------

    [Fact]
    public void Match4Horizontal_ShouldCreateExactlyOneLineClearGem()
    {
        // Given a straight horizontal Match of exactly 4 — row 0, columns 0..3
        var board = TestBoard.Background()
            .WithGems((0, GemType.Def), (1, GemType.Def), (2, GemType.Def), (3, GemType.Def));

        SingleStraight(board, 4);

        // When creations are planned for the pass, with no swap origin (a Cascade)
        var plan = Plan(board);

        // Then exactly one LineClear Gem is required — §5.2 item 1, §5.5.2
        var claim = Assert.Single(plan.CommittedClaims);
        Assert.Equal(SpecialGemType.LineClear, claim.SpecialGem.Type);
    }

    [Fact]
    public void Match4Vertical_ShouldCreateExactlyOneLineClearGem()
    {
        // §5.2 item 1 applies to a vertical Match 4 identically.
        // Given a vertical Match 4 — column 0, rows 0..3
        var board = TestBoard.Background()
            .WithGems((0, GemType.Def), (8, GemType.Def), (16, GemType.Def), (24, GemType.Def));

        SingleStraight(board, 4);

        var claim = Assert.Single(Plan(board).CommittedClaims);
        Assert.Equal(SpecialGemType.LineClear, claim.SpecialGem.Type);
    }

    [Fact]
    public void Match4_ShouldTakeTheCreatingLinesOrientation()
    {
        // §5.2 item 3: the orientation is fixed at creation from the orientation of
        // the straight line that created the Gem. This is the only orientation
        // source. A horizontal Match 4 yields a horizontal Line Clear Gem and a
        // vertical Match 4 a vertical one.

        // Given a horizontal Match 4 and a vertical Match 4
        var horizontal = TestBoard.Background()
            .WithGems((0, GemType.Def), (1, GemType.Def), (2, GemType.Def), (3, GemType.Def));

        var vertical = TestBoard.Background()
            .WithGems((0, GemType.Def), (8, GemType.Def), (16, GemType.Def), (24, GemType.Def));

        SingleStraight(horizontal, 4);
        SingleStraight(vertical, 4);

        // When creations are planned
        var horizontalClaim = Assert.Single(Plan(horizontal).CommittedClaims);
        var verticalClaim = Assert.Single(Plan(vertical).CommittedClaims);

        // Then each takes its own creating line's orientation
        Assert.Equal(SpecialGemOrientation.Horizontal, horizontalClaim.SpecialGem.Orientation);
        Assert.Equal(SpecialGemOrientation.Vertical, verticalClaim.SpecialGem.Orientation);
    }

    [Fact]
    public void Match3_ShouldCreateNoSpecialGem()
    {
        // §5.1 item 1 / §5.5.2 item 4: a Match 3 produces no Special Gem in MVP, and
        // the pass still resolves normally.
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk));

        SingleStraight(board, 3);

        var plan = Plan(board);

        Assert.Empty(plan.OrderedClaims);
        Assert.Empty(plan.CommittedClaims);
        Assert.Empty(plan.ReservedCells);
    }

    // -----------------------------------------------------------------------
    // §5.3 — Match 5+ creates one Burst Gem, with no extra tier
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void Match5OrMore_ShouldCreateExactlyOneBurstGem(int length)
    {
        // §5.3 items 1, 3: a straight line of 5, 6, 7 or 8 creates exactly ONE Burst
        // Gem. There is no Match-6+, Match-7+, or "super" tier, and a long run never
        // creates more than one Special Gem from being long.
        //
        // Given a straight horizontal run of the given length in row 0
        var board = TestBoard.Background()
            .WithGems(Enumerable.Range(0, length).Select(i => (i, GemType.Atk)).ToArray());

        SingleStraight(board, length);

        // When creations are planned
        var plan = Plan(board);

        // Then exactly one Burst Gem, and no other claim
        var claim = Assert.Single(plan.CommittedClaims);
        Assert.Equal(SpecialGemType.Burst, claim.SpecialGem.Type);

        // And a Burst Gem is orientation-free (GAME_STATE.md §2.1.4 item 2)
        Assert.Null(claim.SpecialGem.Orientation);
    }

    [Fact]
    public void Match5OrMore_ShouldStillCountAsExactlyOneMatch()
    {
        // §5.3 item 3: a run of 6+ remains ONE Match shape (§3 item 2) and is
        // counted as exactly one Match (§3 item 5), incrementing Combo once.
        var board = TestBoard.Background()
            .WithGems(Enumerable.Range(0, 8).Select(i => (i, GemType.Atk)).ToArray());

        Assert.Single(MatchDetector.Detect(board));
        Assert.Single(Plan(board).CommittedClaims);
    }

    // -----------------------------------------------------------------------
    // §5.5.2 / §5.3 item 3 — the documented tier ladder
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(3, null)]
    [InlineData(4, SpecialGemType.LineClear)]
    [InlineData(5, SpecialGemType.Burst)]
    [InlineData(6, SpecialGemType.Burst)]
    [InlineData(7, SpecialGemType.Burst)]
    [InlineData(8, SpecialGemType.Burst)]
    public void StraightLineLadder_ShouldMatchTheDocumentedTable(int length, SpecialGemType? expected)
    {
        // §5.3 item 3's table, verbatim:
        //   length 3 → no Special Gem      length 6 → 1 Burst Gem
        //   length 4 → 1 Line Clear Gem    length 7 → 1 Burst Gem
        //   length 5 → 1 Burst Gem         length 8 → 1 Burst Gem
        //
        // Row 0 is used for every length; for length 4 the run is shifted so it does
        // not abut the background, keeping the fixture exactly `length` cells.
        var start = length == 4 ? 2 : 0;
        var board = TestBoard.Background()
            .WithGems(Enumerable.Range(start, length).Select(i => (i, GemType.Atk)).ToArray());

        SingleStraight(board, length);

        var committed = Plan(board).CommittedClaims;

        if (expected is null)
        {
            Assert.Empty(committed);
            return;
        }

        var claim = Assert.Single(committed);
        Assert.Equal(expected, claim.SpecialGem.Type);
    }

    // -----------------------------------------------------------------------
    // §5.4 — the L/T Area Gem
    // -----------------------------------------------------------------------

    [Fact]
    public void Lt_ShouldCreateOneAreaGemAtTheIntersection()
    {
        // §5.4 item 2 / §5.5.3 item 4: an L/T creates exactly one Area Gem, at the
        // single cell shared by its two arms. The intersection is not displaced by
        // the swap origin.
        //
        // L/T H4 V3: H row 0 cols 2..5, V col 2 rows 0..2, intersection (0,2) = 2.
        var board = TestBoard.Background()
            .WithGems((2, GemType.Atk), (3, GemType.Atk), (4, GemType.Atk), (5, GemType.Atk), (10, GemType.Atk), (18, GemType.Atk));

        var shape = SingleLt(board, horizontalLength: 4, verticalLength: 3);
        Assert.Equal(2, shape.IntersectionIndex);

        // A swap origin that IS a member of the shape must not displace it.
        var plan = Plan(board, swapOrigin: 5);

        var areaClaim = Assert.Single(plan.OrderedClaims, c => c.Source == CreationSource.LtPattern);
        Assert.Equal(SpecialGemType.Area, areaClaim.SpecialGem.Type);
        Assert.Equal(2, areaClaim.CellIndex);
    }

    [Fact]
    public void Lt_WithBothArmsOfLengthThree_ShouldCreateOnlyTheAreaGem()
    {
        // §5.5.1 item 4 item 4 Case 1 / §5.8.5: "one L/T, both arms of length 3 →
        // 1 creation: the Area Gem only".

        // L/T H3 V3: row 0 cols 0..2 + col 0 rows 0..2, intersection 0.
        var board = TestBoard.Background()
            .WithGems((0, GemType.Atk), (1, GemType.Atk), (2, GemType.Atk), (8, GemType.Atk), (16, GemType.Atk));

        SingleLt(board, 3, 3);

        var plan = Plan(board);

        var claim = Assert.Single(plan.OrderedClaims);
        Assert.Equal(CreationSource.LtPattern, claim.Source);
        Assert.Equal(SpecialGemType.Area, claim.SpecialGem.Type);
    }

    [Fact]
    public void LtWithHorizontalArmOfLength4_ShouldCreateALineClearGem()
    {
        // §5.4 item 4: "arm length 4 → 1 Line Clear Gem".
        //
        // L/T H4 V3: H row 0 cols 2..5 (length 4), V col 2 rows 0..2 (length 3),
        // intersection 2. H arm start 2, L = 4 → centre 2 + floor(3/2) = 2 + 1 = 3.
        var board = TestBoard.Background()
            .WithGems((2, GemType.Atk), (3, GemType.Atk), (4, GemType.Atk), (5, GemType.Atk), (10, GemType.Atk), (18, GemType.Atk));

        SingleLt(board, 4, 3);

        var plan = Plan(board);

        Assert.Equal(2, plan.OrderedClaims.Count);
        Assert.Equal(CreationSource.LtPattern, plan.OrderedClaims[0].Source);
        Assert.Equal(SpecialGemType.Area, plan.OrderedClaims[0].SpecialGem.Type);

        var armClaim = plan.OrderedClaims[1];
        Assert.Equal(CreationSource.HorizontalArm, armClaim.Source);
        Assert.Equal(SpecialGemType.LineClear, armClaim.SpecialGem.Type);
        Assert.Equal(3, armClaim.CellIndex);

        // The arm's Line Clear takes the ARM's orientation (§5.2 item 3 last
        // paragraph: a Line Clear Gem created by an L/T shape's qualifying arm takes
        // the orientation of that arm).
        Assert.Equal(SpecialGemOrientation.Horizontal, armClaim.SpecialGem.Orientation);
    }

    [Fact]
    public void LtWithVerticalArmOfLength4_ShouldCreateALineClearGemVertically()
    {
        // §5.4 item 4 for a VERTICAL arm, and §5.2 item 3's arm clause: the gem takes
        // the orientation of that arm.
        //
        // L/T H4 V4: H row 0 cols 2..5, V col 2 rows 0..3 (length 4), intersection 2.
        // V arm start 2, L = 4, step 8 → centre 2 + 1 * 8 = 10.
        var board = TestBoard.Background()
            .WithGems(
                (2, GemType.Atk), (3, GemType.Atk), (4, GemType.Atk), (5, GemType.Atk),
                (10, GemType.Atk), (18, GemType.Atk), (26, GemType.Atk));

        SingleLt(board, 4, 4);

        var armClaim = Assert.Single(Plan(board).OrderedClaims, c => c.Source == CreationSource.VerticalArm);

        Assert.Equal(SpecialGemType.LineClear, armClaim.SpecialGem.Type);
        Assert.Equal(SpecialGemOrientation.Vertical, armClaim.SpecialGem.Orientation);
        Assert.Equal(10, armClaim.CellIndex);
    }

    [Fact]
    public void LtWithHorizontalArmOfLength5_ShouldCreateABurstGem()
    {
        // §5.4 item 4: "arm length ≥ 5 → 1 Burst Gem".
        //
        // L/T H5 V3: H row 0 cols 0..4 (length 5), V col 0 rows 0..2 (length 3),
        // intersection 0. H arm start 0, L = 5 → centre 0 + floor(4/2) = 2.
        var board = TestBoard.Background()
            .WithGems((0, GemType.Atk), (1, GemType.Atk), (2, GemType.Atk), (3, GemType.Atk), (4, GemType.Atk), (8, GemType.Atk), (16, GemType.Atk));

        SingleLt(board, 5, 3);

        var plan = Plan(board);

        Assert.Equal(2, plan.OrderedClaims.Count);
        Assert.Equal(SpecialGemType.Area, plan.OrderedClaims[0].SpecialGem.Type);

        var armClaim = plan.OrderedClaims[1];
        Assert.Equal(CreationSource.HorizontalArm, armClaim.Source);
        Assert.Equal(SpecialGemType.Burst, armClaim.SpecialGem.Type);
        Assert.Equal(2, armClaim.CellIndex);
    }

    [Fact]
    public void LtArmOfLength3_ShouldCreateNoArmGem()
    {
        // §5.4 item 4: "arm length 3 → nothing".
        //
        // L/T H3 V5: H row 0 cols 0..2 (length 3), V col 0 rows 0..4 (length 5),
        // intersection 0. Only the Area Gem and the V arm's gem should exist.
        var board = TestBoard.Background()
            .WithGems(
                (0, GemType.Atk), (1, GemType.Atk), (2, GemType.Atk),
                (8, GemType.Atk), (16, GemType.Atk), (24, GemType.Atk), (32, GemType.Atk));

        SingleLt(board, 3, 5);

        var plan = Plan(board);

        Assert.DoesNotContain(plan.OrderedClaims, c => c.Source == CreationSource.HorizontalArm);
        Assert.Contains(plan.OrderedClaims, c => c.Source == CreationSource.VerticalArm);
    }

    [Fact]
    public void LtArm_ShouldBeClassifiedOnceAtItsHighestQualifyingTier()
    {
        // §5.4 item 4 item 1: an arm is classified once, at its HIGHEST qualifying
        // tier — an arm of length ≥ 5 yields a Burst Gem (not also a Line Clear Gem),
        // and an arm never yields two Special Gems from itself.
        //
        // L/T H5 V3: the length-5 horizontal arm qualifies at both the Match 4 and
        // the Match 5 threshold but must produce exactly ONE gem, and it must be the
        // Burst.
        var board = TestBoard.Background()
            .WithGems((0, GemType.Atk), (1, GemType.Atk), (2, GemType.Atk), (3, GemType.Atk), (4, GemType.Atk), (8, GemType.Atk), (16, GemType.Atk));

        SingleLt(board, 5, 3);

        var armClaims = Plan(board).OrderedClaims
            .Where(c => c.Source == CreationSource.HorizontalArm)
            .ToArray();

        var claim = Assert.Single(armClaims);
        Assert.Equal(SpecialGemType.Burst, claim.SpecialGem.Type);
    }

    [Fact]
    public void LtWithBothArmsQualifying_ShouldCreateThreeSpecialGems()
    {
        // §5.4 item 4 item 2 / §5.8.5: "one L/T with both arms ≥ 4 → 3 creations".
        //
        // L/T H4 V4: H row 0 cols 2..5 (length 4 → Line Clear), V col 2 rows 0..3
        // (length 4 → Line Clear), intersection (0,2) = 2 → Area. Positions:
        //   Area          → 2
        //   H arm centre  → 2 + floor(3/2) * 1 = 3
        //   V arm centre  → 2 + floor(3/2) * 8 = 10
        var board = TestBoard.Background()
            .WithGems(
                (2, GemType.Atk), (3, GemType.Atk), (4, GemType.Atk), (5, GemType.Atk),
                (10, GemType.Atk), (18, GemType.Atk), (26, GemType.Atk));

        SingleLt(board, 4, 4);

        var plan = Plan(board);

        Assert.Equal(3, plan.CommittedClaims.Count);

        // In the §5.5.1 item 4 order: Area, then the HORIZONTAL arm's gem, then the
        // VERTICAL arm's gem.
        Assert.Equal(
            new[] { CreationSource.LtPattern, CreationSource.HorizontalArm, CreationSource.VerticalArm },
            plan.OrderedClaims.Select(c => c.Source));

        Assert.Equal(
            new[] { SpecialGemType.Area, SpecialGemType.LineClear, SpecialGemType.LineClear },
            plan.OrderedClaims.Select(c => c.SpecialGem.Type));

        Assert.Equal(new[] { 2, 3, 10 }, plan.OrderedClaims.Select(c => c.CellIndex));
    }

    [Fact]
    public void Lt_ShouldStillCountAsExactlyOneMatch()
    {
        // §5.4 item 4 item 4: however many Special Gems it creates, an L/T is a
        // single Match shape, counted once.
        var board = TestBoard.Background()
            .WithGems(
                (2, GemType.Atk), (3, GemType.Atk), (4, GemType.Atk), (5, GemType.Atk),
                (10, GemType.Atk), (18, GemType.Atk), (26, GemType.Atk));

        Assert.Single(MatchDetector.Detect(board));
        Assert.Equal(3, Plan(board).CommittedClaims.Count);
    }

    // -----------------------------------------------------------------------
    // §5.5.1 item 4 — the intra-L/T creation order
    // -----------------------------------------------------------------------

    [Fact]
    public void Lt_ShouldOrderCreationsAreaThenHorizontalArmThenVerticalArm()
    {
        // §5.5.1 item 4: the intra-shape sequence is
        //   1. the Area Gem
        //   2. the gem of the HORIZONTAL arm
        //   3. the gem of the VERTICAL arm
        // This is an ordering of SOURCES, not of Special Gem types: no Special Gem
        // type outranks another (§5.5.1 item 4 item 1, §5.8.4 item 4).
        var board = TestBoard.Background()
            .WithGems(
                (2, GemType.Atk), (3, GemType.Atk), (4, GemType.Atk), (5, GemType.Atk),
                (10, GemType.Atk), (18, GemType.Atk), (26, GemType.Atk));

        var sources = Plan(board).OrderedClaims.Select(c => c.Source).ToArray();

        Assert.Equal(
            new[] { CreationSource.LtPattern, CreationSource.HorizontalArm, CreationSource.VerticalArm },
            sources);
    }

    [Fact]
    public void Lt_ShouldOrderHorizontalArmBeforeVerticalArm_EvenWhenTheVerticalArmStartsLower()
    {
        // §5.5.1 item 4 item 3: §3.2 item 1 already fixes horizontal before vertical
        // as the pass's primary ordering key, and this item applies that SAME
        // documented key one level down, inside the shape — so the horizontal arm
        // precedes the vertical arm regardless of their start cells.
        //
        // L/T H4 V4 at intersection 2: the V arm's start and the H arm's start are
        // both in row 0 / column 2; the documented key, not geometry, decides.
        var board = TestBoard.Background()
            .WithGems(
                (2, GemType.Atk), (3, GemType.Atk), (4, GemType.Atk), (5, GemType.Atk),
                (10, GemType.Atk), (18, GemType.Atk), (26, GemType.Atk));

        var sources = Plan(board).OrderedClaims.Select(c => c.Source).ToArray();

        Assert.Equal(CreationSource.HorizontalArm, sources[1]);
        Assert.Equal(CreationSource.VerticalArm, sources[2]);
    }

    [Fact]
    public void LtCreationOrder_ShouldBeTotalAndDeterministic()
    {
        // §5.5.1 item 4 item 6: the order is total and deterministic. Every intra-L/T
        // creation is placed in exactly one of the three positions, no two positions
        // are equal, and the sequence depends only on the shape's own structure and
        // the §1.0 indexing.
        var board = TestBoard.Background()
            .WithGems(
                (2, GemType.Atk), (3, GemType.Atk), (4, GemType.Atk), (5, GemType.Atk),
                (10, GemType.Atk), (18, GemType.Atk), (26, GemType.Atk));

        var first = Plan(board).OrderedClaims.Select(c => (c.CellIndex, c.Source)).ToArray();

        for (var run = 0; run < 20; run++)
        {
            var again = Plan(board).OrderedClaims.Select(c => (c.CellIndex, c.Source)).ToArray();
            Assert.Equal(first, again);
        }

        Assert.Equal(3, first.Length);
        Assert.Equal(first.Length, first.Select(c => c.Source).Distinct().Count());
    }

    // -----------------------------------------------------------------------
    // §5.5.3 item 7 — L/T arm placement
    // -----------------------------------------------------------------------

    [Fact]
    public void LtArmOfEvenLength_ShouldPlaceAtTheLowerIndexedMiddleCell()
    {
        // §5.5.3 item 7 item 3: a length-4 arm has two central cells; the selected
        // one is the LOWER-INDEXED one. floor((4 − 1) / 2) = 1, so the selected cell
        // is ONE cell along from the arm's start, not two.
        //
        // H arm row 0 cols 2..5: start 2, L = 4 → 2 + 1 = 3.
        var board = TestBoard.Background()
            .WithGems((2, GemType.Atk), (3, GemType.Atk), (4, GemType.Atk), (5, GemType.Atk), (10, GemType.Atk), (18, GemType.Atk));

        var shape = SingleLt(board, 4, 3);
        Assert.Equal(4, shape.HorizontalArm!.Length);

        var armClaim = Assert.Single(Plan(board).OrderedClaims, c => c.Source == CreationSource.HorizontalArm);

        Assert.Equal(3, armClaim.CellIndex);
    }

    [Fact]
    public void LtArmOfOddLength_ShouldPlaceAtTheUniqueCentre()
    {
        // §5.5.3 item 7 item 4: a length-5 arm selects the cell TWO along
        // (floor(4 / 2) = 2); a length-7 arm the cell THREE along (floor(6 / 2) = 3).
        // There is no tie for an odd length.
        //
        // L/T H7 V3: H row 0 cols 0..6, start 0, L = 7 → 0 + floor(6/2) = 3.
        var board = TestBoard.Background()
            .WithGems(
                (0, GemType.Atk), (1, GemType.Atk), (2, GemType.Atk), (3, GemType.Atk),
                (4, GemType.Atk), (5, GemType.Atk), (6, GemType.Atk),
                (8, GemType.Atk), (16, GemType.Atk));

        var shape = SingleLt(board, 7, 3);
        Assert.Equal(7, shape.HorizontalArm!.Length);

        var armClaim = Assert.Single(Plan(board).OrderedClaims, c => c.Source == CreationSource.HorizontalArm);

        Assert.Equal(3, armClaim.CellIndex);
    }

    [Fact]
    public void VerticalArm_ShouldStepByEight_NotByOne()
    {
        // §5.5.3 item 7 item 2: §5.5.3 item 3 states the step ("stepping by 1
        // (horizontal) or 8 (vertical)"); the multiplication by step is what that
        // sentence means, and it is stated explicitly so the formula cannot be read
        // as a horizontal-only rule.
        //
        // L/T H4 V4: V arm col 2 rows 0..3, start 2, L = 4, step 8
        //   → 2 + floor(3/2) * 8 = 2 + 8 = 10.  (A step of 1 would wrongly give 3.)
        var board = TestBoard.Background()
            .WithGems(
                (2, GemType.Atk), (3, GemType.Atk), (4, GemType.Atk), (5, GemType.Atk),
                (10, GemType.Atk), (18, GemType.Atk), (26, GemType.Atk));

        var shape = SingleLt(board, 4, 4);
        Assert.Equal(4, shape.VerticalArm!.Length);

        var armClaim = Assert.Single(Plan(board).OrderedClaims, c => c.Source == CreationSource.VerticalArm);

        Assert.Equal(10, armClaim.CellIndex);
    }

    [Fact]
    public void LtArm_ShouldUseItsActualLength_NotATruncatedMatch5Length()
    {
        // §5.5.3 item 7 item 5: §5.3 item 3 treats a run of 6+ as a Match 5 FOR TIER
        // CLASSIFICATION ONLY. It does not shorten the arm. A length-7 arm centres on
        // the cell THREE along; a truncated L = 5 would wrongly select index 2.
        //
        // L/T H7 V3: H arm start 0, L = 7 → 0 + floor(6/2) = 3.
        var board = TestBoard.Background()
            .WithGems(
                (0, GemType.Atk), (1, GemType.Atk), (2, GemType.Atk), (3, GemType.Atk),
                (4, GemType.Atk), (5, GemType.Atk), (6, GemType.Atk),
                (8, GemType.Atk), (16, GemType.Atk));

        var shape = SingleLt(board, 7, 3);
        Assert.Equal(7, shape.HorizontalArm!.Length);

        var armClaim = Assert.Single(Plan(board).OrderedClaims, c => c.Source == CreationSource.HorizontalArm);

        Assert.Equal(3, armClaim.CellIndex);
    }

    [Fact]
    public void LtCreation_ShouldNeverUseASwapOrigin()
    {
        // §5.5.3 item 7 item 6: for an L/T shape, neither the Area Gem nor an arm's
        // gem is placed at a swap origin. An L/T arm gem therefore has exactly ONE
        // possible position, not two.
        var board = TestBoard.Background()
            .WithGems(
                (2, GemType.Atk), (3, GemType.Atk), (4, GemType.Atk), (5, GemType.Atk),
                (10, GemType.Atk), (18, GemType.Atk), (26, GemType.Atk));

        SingleLt(board, 4, 4);

        // With no origin and with an origin that is a member of the shape, the
        // positions are identical.
        var withoutOrigin = Plan(board, swapOrigin: null).OrderedClaims
            .Select(c => (c.CellIndex, c.Source)).ToArray();

        var withOrigin = Plan(board, swapOrigin: 5).OrderedClaims
            .Select(c => (c.CellIndex, c.Source)).ToArray();

        Assert.Equal(withoutOrigin, withOrigin);

        // And they are the documented positions: intersection, H arm centre, V arm centre.
        Assert.Equal(new[] { 2, 3, 10 }, withoutOrigin.Select(c => c.CellIndex));
    }

    // -----------------------------------------------------------------------
    // §5.5.3 items 1–3 — straight-shape placement
    // -----------------------------------------------------------------------

    [Fact]
    public void Match4FromSwap_ShouldBePlacedAtTheSwapOrigin()
    {
        // §5.5.3 item 1: the Special Gem is created at the position of the swapped
        // Gem — the cell that was one of the two cells named by the accepted Swap and
        // whose Gem is a member of the matched line.
        var board = TestBoard.Background()
            .WithGems((0, GemType.Def), (1, GemType.Def), (2, GemType.Def), (3, GemType.Def));

        var claim = Assert.Single(Plan(board, swapOrigin: 2).CommittedClaims);

        Assert.Equal(2, claim.CellIndex);
    }

    [Fact]
    public void Match4FromSwap_ShouldPlaceAtWhicheverMatchedCellIsTheOrigin()
    {
        // §5.5.3 item 6: the player's action carries an unordered pair of cells and
        // no direction (§2.1.1 item 2), so no rule can depend on a direction. Two
        // Swaps of the same pair produce the same origin identification and therefore
        // the same creation location.
        var board = TestBoard.Background()
            .WithGems((0, GemType.Def), (1, GemType.Def), (2, GemType.Def), (3, GemType.Def));

        foreach (var origin in new[] { 0, 1, 2, 3 })
        {
            var claim = Assert.Single(Plan(board, swapOrigin: origin).CommittedClaims);
            Assert.Equal(origin, claim.CellIndex);
        }
    }

    [Fact]
    public void Match4FromCascade_ShouldBePlacedAtTheLineCentre()
    {
        // §5.5.3 item 3: for a Match with no swap origin (a Cascade match), the
        // Special Gem is created at the geometric centre of the matched line.
        //
        // H4 row 0 cols 0..3: start 0, L = 4 → 0 + floor(3/2) = 1.
        var board = TestBoard.Background()
            .WithGems((0, GemType.Def), (1, GemType.Def), (2, GemType.Def), (3, GemType.Def));

        var claim = Assert.Single(Plan(board, swapOrigin: null).CommittedClaims);

        Assert.Equal(1, claim.CellIndex);
        Assert.Equal(SpecialGemType.LineClear, claim.SpecialGem.Type);
    }

    [Fact]
    public void MatchFromCascade_ShouldUseTheLowerIndexedMiddleCell_ForEvenLength()
    {
        // §5.5.3 item 3: for an even-length line the centre is the LOWER-INDEXED of
        // the two middle cells — s + floor((L − 1) / 2).
        //
        // H6 row 0 cols 0..5: start 0, L = 6 → 0 + floor(5/2) = 2.
        // The two middle cells are 2 and 3; 2 is selected.
        var board = TestBoard.Background()
            .WithGems(Enumerable.Range(0, 6).Select(i => (i, GemType.Atk)).ToArray());

        SingleStraight(board, 6);

        var claim = Assert.Single(Plan(board, swapOrigin: null).CommittedClaims);

        Assert.Equal(2, claim.CellIndex);
        Assert.Equal(SpecialGemType.Burst, claim.SpecialGem.Type);
    }

    [Fact]
    public void VerticalMatchFromCascade_ShouldStepByEight()
    {
        // §5.5.3 item 3: step is 1 for a horizontal line and 8 for a vertical one.
        //
        // V4 col 0 rows 0..3: start 0, L = 4 → 0 + floor(3/2) * 8 = 8.
        var board = TestBoard.Background()
            .WithGems((0, GemType.Def), (8, GemType.Def), (16, GemType.Def), (24, GemType.Def));

        SingleStraight(board, 4);

        var claim = Assert.Single(Plan(board, swapOrigin: null).CommittedClaims);

        Assert.Equal(8, claim.CellIndex);
    }

    [Fact]
    public void MatchFromSwap_ShouldUseTheOriginOnlyWhenItIsAMemberOfTheMatchedLine()
    {
        // §5.5.3 item 2: if NEITHER swapped cell is a member of the matched line, the
        // line is a Match that existed independently of this Swap — a Cascade match —
        // and item 3 applies.
        var board = TestBoard.Background()
            .WithGems((0, GemType.Def), (1, GemType.Def), (2, GemType.Def), (3, GemType.Def));

        // Index 40 is not part of the matched line.
        var claim = Assert.Single(Plan(board, swapOrigin: 40).CommittedClaims);

        // So the line centre (1) is used instead of the foreign origin.
        Assert.Equal(1, claim.CellIndex);
    }

    // -----------------------------------------------------------------------
    // §5.5.4 item 3 / §5.8.5 — collision
    // -----------------------------------------------------------------------

    /// <summary>
    /// The canonical collision fixture (<c>MATCH3_RULES.md</c> §5.5.1 item 4 item 5):
    /// horizontal arm row 2 columns 0..4 (length 5 → Burst), vertical arm column 2
    /// rows 2..4 (length 3 → no arm gem), intersection (2,2) = index 18. Both the Area
    /// Gem and the horizontal arm's Burst Gem claim index 18.
    /// </summary>
    private static BoardState CanonicalCollisionBoard() =>
        TestBoard.Background()
            .WithGems(
                (16, GemType.Atk), (17, GemType.Atk), (18, GemType.Atk), (19, GemType.Atk), (20, GemType.Atk),
                (26, GemType.Atk), (34, GemType.Atk));

    [Fact]
    public void Collision_AreaAndHorizontalArmClaimingOneCell_ShouldLetTheAreaGemWin()
    {
        // §5.5.1 item 4 item 5 / §5.8.5 — the canonical collision:
        //   Area Gem  → (2,2) = 18
        //   H arm gem → arm start 16, L = 5 → 16 + floor(4/2) = 18 = (2,2)
        //   ⇒ both claim 18; order positions the Area Gem first
        //   ⇒ 18 holds the AREA GEM and the arm's Burst Gem is discarded.
        var board = CanonicalCollisionBoard();

        var shape = Assert.Single(MatchDetector.Detect(board));
        Assert.True(shape.IsLt);
        Assert.Equal(18, shape.IntersectionIndex);
        Assert.Equal(5, shape.HorizontalArm!.Length);
        Assert.Equal(3, shape.VerticalArm!.Length);

        var plan = Plan(board);

        // Two claims on 18: the Area Gem first, then the arm's Burst Gem.
        Assert.Equal(2, plan.OrderedClaims.Count);
        Assert.All(plan.OrderedClaims, c => Assert.Equal(18, c.CellIndex));
        Assert.Equal(
            new[] { SpecialGemType.Area, SpecialGemType.Burst },
            plan.OrderedClaims.Select(c => c.SpecialGem.Type));

        // Only the first in the §5.5.1 order is created.
        var committed = Assert.Single(plan.CommittedClaims);
        Assert.Equal(CreationSource.LtPattern, committed.Source);
        Assert.Equal(SpecialGemType.Area, committed.SpecialGem.Type);
    }

    [Fact]
    public void Collision_LoserShouldNotBeStoredActivatedOrReported()
    {
        // §5.5.4 item 3: a discarded Special Gem claim has NO further effect — it is
        // not created, not stored, not activated, and not reported as created.
        var plan = Plan(CanonicalCollisionBoard());

        Assert.DoesNotContain(plan.CommittedClaims, c => c.Source == CreationSource.HorizontalArm);
        Assert.DoesNotContain(plan.CommittedClaims, c => c.SpecialGem.Type == SpecialGemType.Burst);
        Assert.Single(plan.CommittedClaims);
    }

    [Fact]
    public void Collision_AreaAndVerticalArmClaimingOneCell_ShouldLetTheAreaGemWin()
    {
        // §5.5.1 item 4 item 4 Case 3: "Area + vertical arm gem (V arm >= 4, H arm = 3)
        // → order: Area, then V arm gem. Collision -> the Area Gem wins."
        //
        // The reachable case is a T whose long VERTICAL arm's own centre is the
        // junction:
        //   H arm row 1 cols 1..3  (length 3 → no arm gem); junction (1,1) = 9
        //   V arm col 1 rows 0..3  (length 4 → Line Clear); start 1, L = 4, step 8
        //     → 1 + floor(3/2) * 8 = 1 + 8 = 9 = the junction. ✓
        var board = TestBoard.Background()
            .WithGems((1, GemType.Atk), (9, GemType.Atk), (10, GemType.Atk), (11, GemType.Atk), (17, GemType.Atk), (25, GemType.Atk));

        var shape = Assert.Single(MatchDetector.Detect(board));
        Assert.True(shape.IsLt);
        Assert.Equal(9, shape.IntersectionIndex);
        Assert.Equal(3, shape.HorizontalArm!.Length);
        Assert.Equal(4, shape.VerticalArm!.Length);

        var plan = Plan(board);

        // Both claim 9; the Area Gem is first in the §5.5.1 item 4 sequence.
        Assert.Equal(2, plan.OrderedClaims.Count);
        Assert.All(plan.OrderedClaims, c => Assert.Equal(9, c.CellIndex));
        Assert.Equal(CreationSource.LtPattern, plan.OrderedClaims[0].Source);
        Assert.Equal(CreationSource.VerticalArm, plan.OrderedClaims[1].Source);
        Assert.Equal(SpecialGemType.LineClear, plan.OrderedClaims[1].SpecialGem.Type);

        var committed = Assert.Single(plan.CommittedClaims);
        Assert.Equal(SpecialGemType.Area, committed.SpecialGem.Type);
    }

    [Fact]
    public void Collision_AreaAndLongVerticalArmClaimingOneCell_ShouldLetTheAreaGemWin()
    {
        // §5.5.1 item 4 item 4 Case 3 with a length-5 vertical arm (a Burst tier),
        // which is the case the ordering must still resolve in favour of the Area Gem:
        //   H arm row 2 cols 0..2  (length 3 → no arm gem); junction (2,0) = 16
        //   V arm col 0 rows 0..4  (length 5 → Burst); start 0, L = 5, step 8
        //     → 0 + floor(4/2) * 8 = 16 = the junction. ✓
        var board = TestBoard.Background()
            .WithGems(
                (0, GemType.Atk), (8, GemType.Atk), (16, GemType.Atk), (17, GemType.Atk), (18, GemType.Atk),
                (24, GemType.Atk), (32, GemType.Atk));

        var shape = Assert.Single(MatchDetector.Detect(board));
        Assert.True(shape.IsLt);
        Assert.Equal(16, shape.IntersectionIndex);
        Assert.Equal(3, shape.HorizontalArm!.Length);
        Assert.Equal(5, shape.VerticalArm!.Length);

        var plan = Plan(board);

        Assert.Equal(2, plan.OrderedClaims.Count);
        Assert.All(plan.OrderedClaims, c => Assert.Equal(16, c.CellIndex));
        Assert.Equal(SpecialGemType.Area, plan.OrderedClaims[0].SpecialGem.Type);
        Assert.Equal(SpecialGemType.Burst, plan.OrderedClaims[1].SpecialGem.Type);

        var committed = Assert.Single(plan.CommittedClaims);
        Assert.Equal(SpecialGemType.Area, committed.SpecialGem.Type);
        Assert.Equal(CreationSource.LtPattern, committed.Source);
    }

    [Fact]
    public void Collision_AcrossShapes_ShouldLetTheEarlierMatchSetShapeWin()
    {
        // §5.5.4 item 3 / §5.8.4 item 4: when two creations want the same cell, the
        // one whose shape appears FIRST in the §5.5.1 item 2 order — which is the §3.2
        // order, horizontal shapes before vertical shapes and then ascending
        // start-cell index — is created, and the later claim is discarded.
        var plan = Plan(CanonicalCollisionBoard());

        // The winner is always the first committed claim in the ordered list, and the
        // ordered list is sorted by the shape's position in the match set.
        Assert.Equal(plan.OrderedClaims[0].CellIndex, plan.CommittedClaims[0].CellIndex);
        Assert.Equal(plan.OrderedClaims[0].Source, plan.CommittedClaims[0].Source);

        // Committed claims preserve the §5.5.1 order.
        var committedShapeIndexes = plan.CommittedClaims.Select(c => c.ShapeIndex).ToArray();
        Assert.Equal(committedShapeIndexes.OrderBy(i => i), committedShapeIndexes);
    }

    [Fact]
    public void Collision_ShouldReserveTheCellRegardlessOfWhichClaimWins()
    {
        // §5.5.4 item 2 step 1: the pass computes its required Special Gem positions
        // FIRST, and those cells are reserved. Every claim's cell is reserved,
        // including a discarded claim's cell — the cell must be protected for the
        // winner.
        var plan = Plan(CanonicalCollisionBoard());

        Assert.Contains(18, plan.ReservedCells);
        Assert.Equal(plan.OrderedClaims.Select(c => c.CellIndex).Distinct().Count(), plan.ReservedCells.Count);
    }

    // -----------------------------------------------------------------------
    // §5.5.4 items 1, 4 — what the created gem carries
    // -----------------------------------------------------------------------

    [Fact]
    public void Creation_ShouldCarryTheGemTypeOfTheGemItReplaced()
    {
        // §5.5.4 item 4 / GAME_STATE.md §2.1.3 item 2: the cell a created Special Gem
        // occupies still holds one of the four §1.1 Gem types — the type of the Gem
        // removed from that cell in this pass.
        var board = TestBoard.Background()
            .WithGems((25, GemType.Hp), (26, GemType.Hp), (27, GemType.Hp), (28, GemType.Hp));

        var claim = Assert.Single(Plan(board).CommittedClaims);

        Assert.Equal(GemType.Hp, claim.GemType);
    }

    [Fact]
    public void Creation_ShouldAlwaysLandOnACellOfItsOwnShape()
    {
        // §5.5.3 item 5: every Special Gem is created in a cell that was part of the
        // shape that created it. No Special Gem is created outside the shape, at an
        // arbitrary empty cell, or at a newly spawned cell.
        var board = TestBoard.Background()
            .WithGems(
                (2, GemType.Atk), (3, GemType.Atk), (4, GemType.Atk), (5, GemType.Atk),
                (10, GemType.Atk), (18, GemType.Atk), (26, GemType.Atk));

        var shape = Assert.Single(MatchDetector.Detect(board));

        foreach (var claim in Plan(board).OrderedClaims)
        {
            Assert.Contains(claim.CellIndex, shape.Cells);
        }
    }

    [Fact]
    public void Creation_ShouldDrawNoRng()
    {
        // §7.2 item 2: Special Gem creation, its type, order, location and collision
        // must NOT consume RNG. Planning is a pure function of the match set, the
        // origin, and the board — so planning the same pass twice yields identical
        // results, with no generator involved at all.
        var board = CanonicalCollisionBoard();

        var first = Plan(board);
        var second = Plan(board);

        Assert.Equal(
            first.OrderedClaims.Select(c => (c.CellIndex, c.SpecialGem, c.Source)),
            second.OrderedClaims.Select(c => (c.CellIndex, c.SpecialGem, c.Source)));
    }

    // -----------------------------------------------------------------------
    // Board-level state gate
    // -----------------------------------------------------------------------

    [Fact]
    public void CreatedSpecialGem_ShouldBeWellFormed()
    {
        // GAME_STATE.md §2.1.4 item 2: orientation is present if and only if the type
        // is LineClear. Burst and Area are orientation-free.
        var boards = new[]
        {
            TestBoard.Background().WithGems((0, GemType.Def), (1, GemType.Def), (2, GemType.Def), (3, GemType.Def)),
            TestBoard.Background().WithGems(Enumerable.Range(0, 6).Select(i => (i, GemType.Atk)).ToArray()),
            CanonicalCollisionBoard(),
        };

        foreach (var board in boards)
        {
            foreach (var claim in Plan(board).CommittedClaims)
            {
                Assert.True(
                    claim.SpecialGem.IsWellFormed,
                    $"malformed Special Gem for {claim.Source}: {claim.SpecialGem}");
            }
        }
    }
}