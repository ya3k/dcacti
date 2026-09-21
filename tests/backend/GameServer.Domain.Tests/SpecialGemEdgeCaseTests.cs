using GameServer.Domain.Match3;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Edge-case tests for documented Special Gem interactions that combine several
/// rules at once (<c>MATCH3_RULES.md</c> §5.5.5 item 7, §5.8.2, §5.8.3, §5.8.6).
///
/// These exist because the individual rules are each covered elsewhere: what is
/// tested here is the seam between them, where an implementation is most likely to
/// double-count or double-activate.
/// </summary>
public class SpecialGemEdgeCaseTests
{
    private static Pcg32 Rng() => Pcg32.FromSeed(4711UL);

    private static PassResult Resolve(BoardState board, int depth = 1) =>
        BoardResolver.ResolvePass(board, MatchDetector.Detect(board), Rng(), swapOriginIndex: null, cascadeDepth: depth);

    [Fact]
    public void SpecialGemConsumedByTheMatchSetAndByASiblingsBlast_ShouldActivateOnce()
    {
        // §5.8.2 item 3 / §5.5.5 item 7: "A Special Gem activates at most once per
        // step." A gem that is BOTH a member of the pass's match set AND inside another
        // matched gem's affected set is one activation, at the match-set level — it is
        // not a chain of its sibling, because it was already consumed.
        //
        // Given three matched ATK gems where the outer two are Bursts: cell 27 lies in
        // both the match set and the blast of cell 25.
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((25, SpecialGem.Burst()), (27, SpecialGem.Burst()));

        var pass = Resolve(board);

        // Then cell 27 activated exactly once.
        Assert.Equal(1, pass.ActivatedSpecialGems.Count(a => a.CellIndex == 27));

        // And both bursts are level 0: both were consumed by the match set, so neither is
        // a chain of the other (§5.8.6 item 5).
        Assert.Equal(0, pass.ActivatedSpecialGems.Single(a => a.CellIndex == 27).ChainLevel);
        Assert.Equal(0, pass.ActivatedSpecialGems.Single(a => a.CellIndex == 25).ChainLevel);
    }

    [Fact]
    public void SpecialGemOnlyInASiblingsBlast_ShouldActivateAtTheNextLevel()
    {
        // The contrast to the previous test: a Special Gem that is NOT in the match set
        // but lies inside another's affected set is a CHAIN and resolves at the next
        // breadth-first level (§5.8.6 item 5).
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((26, SpecialGem.Burst()), (35, SpecialGem.Area()));

        var pass = Resolve(board);

        // Cell 35 is not matched; it is consumed by cell 26's blast.
        Assert.DoesNotContain(35, pass.MatchedCellGemMatched.Select(e => e.CellIndex));

        Assert.Equal(0, pass.ActivatedSpecialGems.Single(a => a.CellIndex == 26).ChainLevel);
        Assert.Equal(1, pass.ActivatedSpecialGems.Single(a => a.CellIndex == 35).ChainLevel);
    }

    [Fact]
    public void Chain_ShouldNotReClearACellClearedByAnEarlierLevel()
    {
        // §5.8.2 item 6: "A cell cleared earlier in the same step is no longer on the
        // board, so a later effect naming it has nothing to clear there and generates
        // nothing for it."
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial(
                (26, SpecialGem.Burst()),
                (35, SpecialGem.Area()),
                (44, SpecialGem.Area()));

        var pass = Resolve(board);

        // Whatever the chain did, the union holds each cell once.
        Assert.Equal(pass.ClearedCellUnion.Distinct().Count(), pass.ClearedCellUnion.Count);
        Assert.Equal(pass.ClearedCellUnion.OrderBy(i => i), pass.ClearedCellUnion);
    }

    [Fact]
    public void Reservation_ShouldProtectACreationCellFromAChainActivation()
    {
        // §5.5.4 item 2 applies to the WHOLE step, chains included: every affected set of
        // that step's activations is reduced by the reserved cells, not only the
        // level-0 ones. Otherwise a chain could clear a cell the pass is about to create
        // a Special Gem in.
        //
        // Given a Match 4 (creating at its line centre) whose matched cells hold a Burst
        // whose blast covers that centre, and a second gem whose chain would also cover
        // it.
        var board = TestBoard.Background()
            .WithGems((16, GemType.Def), (17, GemType.Def), (18, GemType.Def), (19, GemType.Def))
            .WithSpecial((16, SpecialGem.Burst()), (24, SpecialGem.Burst()));

        var pass = Resolve(board);

        // The pass created a Special Gem at the Match 4's line centre (17)...
        Assert.Contains(pass.CreatedSpecialGems, c => c.CellIndex == 17);

        // ...and it survived onto the board.
        Assert.Contains(pass.Board.Cells, c => c.HasSpecialGem);
    }

    [Fact]
    public void ClearedUnion_ShouldIncludeActivationCellsNotInTheMatchSet()
    {
        // §5.8.2 item 5 / §5.7 item 2: the cleared union that resource generation reads
        // is the union of the matched cells AND every activation and chain cell — not
        // just the match set.
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((26, SpecialGem.Burst()));

        var pass = Resolve(board);

        var matched = MatchDetector.ClearedCellUnion(MatchDetector.Detect(board));

        // The union strictly contains the match set.
        Assert.All(matched, index => Assert.Contains(index, pass.ClearedCellUnion));
        Assert.True(pass.ClearedCellUnion.Count > matched.Count);
    }

    [Fact]
    public void Match4AtABoardEdge_ShouldStillCreateAtItsLineCentre()
    {
        // §5.5.3 item 3 combined with §5.8.1: the line centre formula needs no clipping
        // and must work at the board's edge.
        //
        // H4 row 0, columns 4..7 (indices 4..7): start 4, L = 4 → 4 + floor(3/2) = 5.
        var board = TestBoard.Background()
            .WithGems((4, GemType.Atk), (5, GemType.Atk), (6, GemType.Atk), (7, GemType.Atk));

        var shape = Assert.Single(MatchDetector.Detect(board));
        Assert.Equal(4, shape.HorizontalArm!.Length);

        var plan = SpecialGemPlanner.PlanCreations(MatchDetector.Detect(board), swapOriginIndex: null);

        var claim = Assert.Single(plan.CommittedClaims);
        Assert.Equal(5, claim.CellIndex);
    }

    [Fact]
    public void BurstAtACorner_ShouldCreateAndActivateWithinTheBoard()
    {
        // A Match 4 in the corner row creates a gem at index 1, and consuming it must
        // clip its blast to the four cells that exist (§5.8.1 item 5).
        var board = TestBoard.Background()
            .WithGems((0, GemType.Atk), (1, GemType.Atk), (2, GemType.Atk), (3, GemType.Atk))
            .WithSpecial((1, SpecialGem.Burst()));

        var pass = Resolve(board);

        // The Burst activated and every cell it named exists.
        Assert.Contains(pass.ActivatedSpecialGems, a => a.CellIndex == 1);
        Assert.All(pass.ClearedCellUnion, i => Assert.InRange(i, 0, BoardState.CellCount - 1));

        // The union did not include a wrapped cell from the opposite edge.
        Assert.DoesNotContain(63, pass.ClearedCellUnion);
    }

    [Fact]
    public void TwoIdenticalSpecialGemsAtDifferentCells_ShouldBothActivateWithTheirOwnGeometry()
    {
        // §5.8.6 item 2: each Special Gem resolves its OWN type's geometry, at its own
        // position. Two gems of one type do not merge into a larger shape.
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((25, SpecialGem.LineClearHorizontal()), (27, SpecialGem.LineClearHorizontal()));

        var pass = Resolve(board);

        // Both activated.
        Assert.Equal(2, pass.ActivatedSpecialGems.Count);

        // Each cleared its own row: both gems are in row 3, so the union is that one row
        // (plus nothing else) — the geometries did not enlarge into a second row.
        var rowThree = Enumerable.Range(24, 8).ToArray();
        Assert.All(pass.ClearedCellUnion, index => Assert.Contains(index, rowThree));
    }

    [Fact]
    public void LineClearAndBurst_ShouldProduceTheUnionOfTheirShapes()
    {
        // §5.8.6: "Line + Burst → Line clears its full row/column; Burst clears its 3x3.
        // Union of the two."
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((25, SpecialGem.LineClearVertical()), (27, SpecialGem.Burst()));

        var pass = Resolve(board);

        // The vertical line through 25 (column 1) and the 3x3 around 27 (3,3) are both
        // represented.
        var columnOne = Enumerable.Range(0, 8).Select(r => BoardState.ToIndex(r, 1)).ToArray();
        foreach (var index in columnOne.Where(i => i != 25))
        {
            Assert.Contains(index, pass.ClearedCellUnion);
        }

        // A diagonal neighbour of 27 proves the Burst's square (not just a plus).
        Assert.Contains(BoardState.ToIndex(4, 4), pass.ClearedCellUnion);
    }

    [Fact]
    public void Pass_ShouldBeIdempotentWhenRunOnItsOwnStableOutput()
    {
        // A resolved board that is stable resolves to itself: the pass's output never
        // contains a match the loop would not have consumed (§4.3 item 1).
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk));

        var resolution = CascadeResolver.Resolve(board, Pcg32.FromSeed(1UL), swapOriginIndex: null);

        Assert.Empty(MatchDetector.Detect(resolution.Board));

        // Re-resolving the stable board changes nothing.
        var again = CascadeResolver.Resolve(resolution.Board, Pcg32.FromSeed(1UL), swapOriginIndex: null);

        Assert.Empty(again.Passes);
        Assert.True(resolution.Board.CellsEqual(again.Board));
    }

    [Fact]
    public void CreatedSpecialGem_ShouldNotBeConsumedByASiblingActivationInTheSamePass()
    {
        // §5.8.3 item 6: a gem created in step 4 is created AFTER the pass's activations,
        // so no effect of that pass can consume it — not even one whose geometry covers
        // its cell.
        //
        // Given a Match 4 in row 1 (line centre index 9) and a Burst at index 11 — a
        // matched cell whose 3x3 covers index 9 (its row-0 and row-2 neighbours).
        var board = TestBoard.Background()
            .WithGems((8, GemType.Atk), (9, GemType.Atk), (10, GemType.Atk), (11, GemType.Atk))
            .WithSpecial((11, SpecialGem.Burst()));

        var pass = Resolve(board);

        // The Match 4's line centre is 9, and its reserved cell is protected from the
        // Burst's blast, so the creation is committed.
        Assert.Contains(pass.CreatedSpecialGems, c => c.CellIndex == 9);

        // The Burst at 11 activated...
        Assert.Contains(pass.ActivatedSpecialGems, a => a.CellIndex == 11);

        // ...and the gem created at 9 did NOT activate in this same pass.
        Assert.DoesNotContain(pass.ActivatedSpecialGems, a => a.CellIndex == 9);

        // And it survived onto the board, carrying its metadata.
        Assert.Contains(pass.Board.Cells, c => c.HasSpecialGem);
    }
}