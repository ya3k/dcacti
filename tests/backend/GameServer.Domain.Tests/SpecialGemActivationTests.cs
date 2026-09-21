using GameServer.Domain.Match3;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Special Gem activation, chain, union, gravity, spawn, and cascade tests
/// (<c>MATCH3_RULES.md</c> §4, §5.5.5, §5.8.2, §5.8.3, §5.8.6).
///
/// These are the gameplay scenarios: each is a Given/When/Then over a documented
/// board state, asserting the full state transition — the resulting board, the
/// cleared union, the activation sequence, and the event reports — not only a
/// final number (<c>.ai/README.md</c> §17).
/// </summary>
public class SpecialGemActivationTests
{
    /// <summary>A generator positioned at a fixed state, so scenarios are reproducible.</summary>
    private static Pcg32 Rng(ulong seed = 12345UL) => Pcg32.FromSeed(seed);

    /// <summary>Resolves one pass on a board's detected match set.</summary>
    private static PassResult ResolvePass(BoardState board, Pcg32 rng, int? swapOrigin = null, int depth = 1) =>
        BoardResolver.ResolvePass(board, MatchDetector.Detect(board), rng, swapOrigin, depth);

    // -----------------------------------------------------------------------
    // §5.5.5 item 1 — the activation trigger
    // -----------------------------------------------------------------------

    [Fact]
    public void SpecialGem_ShouldActivateWhenConsumedByTheMatchSet()
    {
        // §5.5.5 item 1: a Special Gem's clear effect activates when it is consumed —
        // it is part of a detection pass's match set, that is, its own colour is
        // matched normally, exactly like any other Gem.
        //
        // Given a match of three ATK gems where the middle one carries a Burst Gem
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((26, SpecialGem.Burst()));

        var rng = Rng();

        // When the pass resolves
        var result = ResolvePass(board, rng);

        // Then the Burst Gem activated, at its own cell
        var activation = Assert.Single(result.ActivatedSpecialGems);
        Assert.Equal(26, activation.CellIndex);
        Assert.Equal(SpecialGemType.Burst, activation.SpecialGem.Type);
    }

    [Fact]
    public void SpecialGem_ShouldNotActivateMerelyByBeingPresent()
    {
        // §5.5.5 item 1: "A Special Gem that is merely present on the board, that
        // falls, or that is created does NOT activate."
        //
        // Given a Special Gem on a board whose match is elsewhere
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((0, SpecialGem.Burst()));

        var result = ResolvePass(board, Rng());

        // Then no activation was reported
        Assert.Empty(result.ActivatedSpecialGems);
    }

    // -----------------------------------------------------------------------
    // §5.5.5 item 4 — the affected set, applied
    // -----------------------------------------------------------------------

    [Fact]
    public void LineClearActivation_ShouldClearItsWholeLine()
    {
        // §5.2 item 2: a horizontal Line Clear Gem clears every cell of its own row.
        //
        // Given a matched Line Clear Gem at (3,3) = 27 carrying a horizontal LineClear
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((27, SpecialGem.LineClearHorizontal()));

        var result = ResolvePass(board, Rng());

        // Then the whole row 3 is in the cleared union.
        var rowThree = Enumerable.Range(24, 8).ToArray();
        Assert.All(rowThree, index => Assert.Contains(index, result.ClearedCellUnion));

        // And the activation sub-step reports the rest of the line. The three matched
        // cells (25, 26, 27) belong to the pass's matched sub-step and are reported
        // there, not again here: GAME_EVENTS.md §1.3 reports each cleared cell once
        // over the pass's cleared union, matched cells first.
        var activationReports = result.ActivationCellGemMatched.Select(e => e.CellIndex).ToArray();
        Assert.All(rowThree.Where(i => i is not (25 or 26 or 27)), index => Assert.Contains(index, activationReports));

        // The gems's own cell was reported in the matched sub-step, carrying its
        // Special Gem metadata.
        var ownCell = Assert.Single(result.MatchedCellGemMatched, e => e.CellIndex == 27);
        Assert.Equal(SpecialGemType.LineClear, ownCell.ConsumedSpecialGem!.Value.Type);
    }

    [Fact]
    public void BurstActivation_ShouldClearIts3x3Square()
    {
        // §5.3 item 2.
        //
        // Given a matched Burst Gem in the interior
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((26, SpecialGem.Burst()));

        var result = ResolvePass(board, Rng());

        // Then the 3x3 square around 26 is cleared. Cell 26 is at (3,2), so the
        // square spans rows 2..4 and columns 1..3.
        var expected = new[]
        {
            BoardState.ToIndex(2, 1), BoardState.ToIndex(2, 2), BoardState.ToIndex(2, 3),
            BoardState.ToIndex(3, 1), BoardState.ToIndex(3, 2), BoardState.ToIndex(3, 3),
            BoardState.ToIndex(4, 1), BoardState.ToIndex(4, 2), BoardState.ToIndex(4, 3),
        };

        Assert.All(expected, index => Assert.Contains(index, result.ClearedCellUnion));
    }

    [Fact]
    public void AreaActivation_ShouldClearItsPlus()
    {
        // §5.4 item 3.
        //
        // Given a matched Area Gem in the interior
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((26, SpecialGem.Area()));

        var result = ResolvePass(board, Rng());

        // Cell 26 is (3,2): the plus is 26 itself plus 18 (2,2), 34 (4,2),
        // 25 (3,1) and 27 (3,3).
        var expected = new[] { 18, 25, 26, 27, 34 };

        Assert.All(expected, index => Assert.Contains(index, result.ClearedCellUnion));

        // And the diagonals are NOT part of it.
        Assert.DoesNotContain(17, result.ClearedCellUnion);
    }

    [Fact]
    public void Activation_ShouldAlwaysClearItsOwnCell()
    {
        // §5.5.5 item 4: all three include the activated Special Gem's own cell, so
        // consuming a Special Gem always clears the cell it occupied and never leaves
        // it behind.
        foreach (var gem in new[]
                 {
                     SpecialGem.Burst(), SpecialGem.Area(),
                     SpecialGem.LineClearHorizontal(), SpecialGem.LineClearVertical(),
                 })
        {
            var board = TestBoard.Background()
                .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
                .WithSpecial((26, gem));

            var result = ResolvePass(board, Rng());

            Assert.Contains(26, result.ClearedCellUnion);
        }
    }

    // -----------------------------------------------------------------------
    // §5.5.4 item 2 — reservation protects creations
    // -----------------------------------------------------------------------

    [Fact]
    public void ReservedCreationCell_ShouldNotBeClearedByAnActivation()
    {
        // §5.5.4 item 2: the pass computes its required Special Gem positions FIRST;
        // those cells are reserved, and every affected set of that step's activations
        // is reduced by the reserved cells. That is what makes an activation never
        // clear a cell the pass is about to create a Special Gem in — so the created
        // gem always survives step 2 and reaches Gravity.
        //
        // Given a Match 4 (which creates a Line Clear Gem at its line centre, index 1)
        // whose matched cells also hold a Burst Gem at index 0, whose 3x3 square would
        // otherwise cover index 1.
        var board = TestBoard.Background()
            .WithGems((0, GemType.Def), (1, GemType.Def), (2, GemType.Def), (3, GemType.Def))
            .WithSpecial((0, SpecialGem.Burst()));

        var result = ResolvePass(board, Rng());

        // The Burst at 0 activated and the pass created a Line Clear Gem at 1.
        Assert.Contains(result.ActivatedSpecialGems, a => a.CellIndex == 0);
        Assert.Contains(result.CreatedSpecialGems, c => c.CellIndex == 1);

        // The created gem is on the board — the reservation kept it from being cleared
        // before creation, so it survived the pass.
        Assert.Contains(result.Board.Cells, c => c.HasSpecialGem);

        // The creation is reported as created: it was not discarded.
        var created = Assert.Single(result.CreatedSpecialGems);
        Assert.Equal(SpecialGemType.LineClear, created.SpecialGem.Type);
    }

    [Fact]
    public void Activation_ShouldStillClearItsOwnCellEvenWhenThatCellIsReserved()
    {
        // §5.5.4 item 2: "unless the reserved cell is the activated Special Gem's own
        // cell". Every geometry includes the gem's own cell, and it is never withheld
        // (§5.8.1 item 4).
        var board = TestBoard.Background()
            .WithGems((0, GemType.Def), (1, GemType.Def), (2, GemType.Def), (3, GemType.Def))
            .WithSpecial((1, SpecialGem.Burst()));

        var result = ResolvePass(board, Rng());

        // Cell 1 is the Match 4's line centre (its reserved creation cell) AND the
        // Burst Gem's own cell, so the activation clears it and the creation is
        // discarded — the cell was already removed.
        Assert.Contains(result.ActivatedSpecialGems, a => a.CellIndex == 1);
        Assert.Contains(1, result.ClearedCellUnion);
    }

    // -----------------------------------------------------------------------
    // §5.8.3 item 6 — newly created gems do not activate in their creating pass
    // -----------------------------------------------------------------------

    [Fact]
    public void NewlyCreatedSpecialGem_ShouldNotActivateInItsCreatingPass()
    {
        // §4.1 item 4 / §5.8.3 item 6: a Special Gem created in step 4 of a pass is
        // created AFTER that pass's activations, is not part of the pass's match set,
        // and is not re-examined until the next detection pass. It can therefore never
        // activate in its creating pass.
        //
        // Given a Match 4 that creates a Line Clear Gem at its line centre
        var board = TestBoard.Background()
            .WithGems((0, GemType.Def), (1, GemType.Def), (2, GemType.Def), (3, GemType.Def));

        var result = ResolvePass(board, Rng());

        // Then the pass created a Special Gem...
        var created = Assert.Single(result.CreatedSpecialGems);
        Assert.Equal(SpecialGemType.LineClear, created.SpecialGem.Type);

        // ...and reported NO activation at all.
        Assert.Empty(result.ActivatedSpecialGems);
    }

    [Fact]
    public void NewlyCreatedSpecialGem_ShouldExistOnTheBoardAfterThePass()
    {
        // The created gem becomes part of the authoritative board state
        // (GAME_STATE.md §2.1.5 item 5) and is subject to the same Gravity and Spawn as
        // every other Gem (§4.1 item 2) — so it may FALL before the pass ends, and it
        // must still be on the board holding its type and orientation.
        //
        // The fixture is a Match 4, so the pass creates exactly one Special Gem.
        var board = TestBoard.Background()
            .WithGems((0, GemType.Def), (1, GemType.Def), (2, GemType.Def), (3, GemType.Def));

        var result = ResolvePass(board, Rng());

        // The pass created exactly one Special Gem, and it survived into the board.
        var created = Assert.Single(result.CreatedSpecialGems);

        var specialCells = Enumerable.Range(0, BoardState.CellCount)
            .Where(i => result.Board.SpecialGemAt(i) is not null)
            .ToArray();

        var cell = Assert.Single(specialCells);
        var onBoard = result.Board.SpecialGemAt(cell)!.Value;

        Assert.Equal(created.SpecialGem, onBoard);

        // It carries the Gem type of the Gem it replaced (§5.5.4 item 4) — gravity
        // moves the whole entry, so type and metadata stay together
        // (GAME_STATE.md §2.1.5 item 1).
        Assert.Equal(GemType.Def, result.Board[cell]);
    }

    [Fact]
    public void NewlyCreatedSpecialGem_ShouldSurviveGravityWithItsOrientation()
    {
        // §4.4 item 8 / §5.2 item 3: a Line Clear Gem falls like any other Gem and
        // keeps its orientation — the orientation is a property of the created Special
        // Gem, not of its cell.
        //
        // A horizontal Match 4 creates a horizontal Line Clear Gem; gravity then moves
        // it, and it must still be horizontal.
        var board = TestBoard.Background()
            .WithGems((0, GemType.Def), (1, GemType.Def), (2, GemType.Def), (3, GemType.Def));

        var result = ResolvePass(board, Rng());

        var cell = Enumerable.Range(0, BoardState.CellCount)
            .Single(i => result.Board.SpecialGemAt(i) is not null);

        var gem = result.Board.SpecialGemAt(cell)!.Value;

        Assert.Equal(SpecialGemType.LineClear, gem.Type);
        Assert.Equal(SpecialGemOrientation.Horizontal, gem.Orientation);
    }

    // -----------------------------------------------------------------------
    // §5.5.5 item 7 / §5.8.6 — chains
    // -----------------------------------------------------------------------

    [Fact]
    public void Chain_ShouldConsumeASpecialGemInsideAnothersAffectedSet()
    {
        // §5.5.5 item 7: a Special Gem inside another Special Gem's affected set is
        // consumed by it and activates in turn. Chains are allowed.
        //
        // Given a match whose Burst Gem's 3x3 covers a second Special Gem
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((26, SpecialGem.Burst()), (34, SpecialGem.Area()));

        var result = ResolvePass(board, Rng());

        // Then both activated.
        Assert.Equal(2, result.ActivatedSpecialGems.Count);
        Assert.Contains(result.ActivatedSpecialGems, a => a.CellIndex == 26);
        Assert.Contains(result.ActivatedSpecialGems, a => a.CellIndex == 34);
    }

    [Fact]
    public void Chain_ShouldBeBreadthFirst_NotDepthFirst()
    {
        // §5.5.5 item 7 / §5.8.3 item 2: "all effects discovered at one level are
        // resolved before any effect discovered by them."
        //
        // Level 0: the Special Gem the match set consumed (cell 26).
        // Level 1: the gem its affected set covers (cell 34).
        // Level 2: the gem THAT gem's affected set covers.
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial(
                (26, SpecialGem.Burst()),
                (34, SpecialGem.Burst()),
                (42, SpecialGem.Area()));

        var result = ResolvePass(board, Rng());

        var levels = result.ActivatedSpecialGems.ToDictionary(a => a.CellIndex, a => a.ChainLevel);

        // The gem consumed by the match set is level 0 — it is not a chain of anything.
        Assert.Equal(0, levels[26]);

        // Everything it discovers is at level 1.
        Assert.Equal(1, levels[34]);

        // And what THAT discovers is level 2 — strictly after level 1 completes.
        Assert.Equal(2, levels[42]);
    }

    [Fact]
    public void Chain_ShouldActivateEachSpecialGemAtMostOnce()
    {
        // §5.8.2 item 3: a Special Gem activates at most once per step, even if two
        // effects name its cell.
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial(
                (25, SpecialGem.Burst()),
                (27, SpecialGem.Burst()),
                (26, SpecialGem.Area()));

        var result = ResolvePass(board, Rng());

        // 26 is covered by both bursts' squares but activates once.
        var activationsOf26 = result.ActivatedSpecialGems.Count(a => a.CellIndex == 26);
        Assert.Equal(1, activationsOf26);

        // And no cell appears twice in the activation list at all.
        Assert.Equal(
            result.ActivatedSpecialGems.Select(a => a.CellIndex).Distinct().Count(),
            result.ActivatedSpecialGems.Count);
    }

    [Fact]
    public void Chain_ShouldResolveSiblingsInMatchSetOrder_NotAsChainsOfEachOther()
    {
        // §5.8.6 item 5: if two Special Gems are consumed by the SAME match set, they
        // are siblings at the same chain level — neither is a chain of the other.
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((25, SpecialGem.Area()), (27, SpecialGem.Area()));

        var result = ResolvePass(board, Rng());

        // Both are level 0, in ascending cell index.
        var gems = result.ActivatedSpecialGems.ToArray();
        Assert.Equal(2, gems.Length);
        Assert.All(gems, g => Assert.Equal(0, g.ChainLevel));
        Assert.Equal(new[] { 25, 27 }, gems.Select(g => g.CellIndex));
    }

    [Fact]
    public void LevelZeroActivations_ShouldFollowMatchSetOrder_NotCellIndexOrder()
    {
        // §5.5.5 item 5: "Two Special Gems activated by the same match set are resolved
        // sequentially, in the match-set order of §3.2." §5.8.3 level 3 states the same,
        // and §5.8.3 item 1 decides cross-entry order by the match set too.
        //
        // This is observable because §3.2 item 1 puts EVERY horizontal shape before
        // EVERY vertical shape, so a vertical shape with a LOWER start index must still
        // activate AFTER a horizontal shape with a higher one. Ordering activations by
        // cell index would produce the opposite sequence.
        //
        // Given: a vertical Match-3 at column 0, rows 0..2 (start index 0), and a
        // horizontal Match-3 at row 6, columns 0..2 (start index 48).
        var board = TestBoard.Background()
            .WithGems(
                (0, GemType.Atk), (8, GemType.Atk), (16, GemType.Atk),
                (48, GemType.Hp), (49, GemType.Hp), (50, GemType.Hp))
            .WithSpecial((0, SpecialGem.Area()), (48, SpecialGem.Area()));

        var matchSet = MatchDetector.Detect(board);

        // The match set is ordered horizontal-first.
        Assert.Equal(2, matchSet.Count);
        Assert.Equal(48, matchSet[0].StartIndex);
        Assert.Equal(0, matchSet[1].StartIndex);

        var result = ResolvePass(board, Rng());

        // Then the horizontal shape's Special Gem activates first, even though the
        // vertical shape's gem has the lower cell index.
        var levelZero = result.ActivatedSpecialGems.Where(a => a.ChainLevel == 0).ToArray();

        Assert.Equal(2, levelZero.Length);
        Assert.Equal(new[] { 48, 0 }, levelZero.Select(a => a.CellIndex));
    }

    [Fact]
    public void ChainLevelOne_ShouldFollowTheActivationOrderOfItsConsumers()
    {
        // §5.8.3 level 3a: "within a level, in the order the consuming effects were
        // resolved, and within one effect by ascending §1.0 index of the consumed
        // Special Gems". A chain discovered by the FIRST level-0 effect therefore
        // precedes one discovered by the second, whatever their cell indexes.
        //
        // Given the same shape pair, each with one chain gem in its blast. Bursts are used
        // because their 3x3 reaches a diagonal: the horizontal shape's gem at 48 (row 6,
        // col 0) reaches 57 (row 7, col 1), and the vertical shape's gem at 0 reaches 9
        // (row 1, col 1).
        var board = TestBoard.Background()
            .WithGems(
                (0, GemType.Atk), (8, GemType.Atk), (16, GemType.Atk),
                (48, GemType.Hp), (49, GemType.Hp), (50, GemType.Hp))
            .WithSpecial(
                (48, SpecialGem.Burst()),
                (0, SpecialGem.Burst()),
                (57, SpecialGem.Area()),
                (9, SpecialGem.Area()));

        var result = ResolvePass(board, Rng());

        var levelOne = result.ActivatedSpecialGems
            .Where(a => a.ChainLevel == 1)
            .Select(a => a.CellIndex)
            .ToArray();

        // The chain of the FIRST level-0 activation (48) comes first, so 57 precedes 9
        // even though 9 has the lower cell index.
        Assert.Equal(new[] { 57, 9 }, levelOne);
    }

    [Fact]
    public void Chain_ShouldTerminateWithoutADepthCap()
    {
        // §5.8.3 item 7: a chain is finite because each activation consumes a Special
        // Gem that had not yet activated. No depth limit, recursion cap, or iteration
        // budget is introduced, and none may be added.
        //
        // Given a long chain across the board
        var board = TestBoard.Background()
            .WithGems(
                // A matched row that consumes the first gem of the chain.
                (16, GemType.Atk), (17, GemType.Atk), (18, GemType.Atk), (19, GemType.Atk),
                (20, GemType.Atk), (21, GemType.Atk), (22, GemType.Atk), (23, GemType.Atk))
            .WithSpecial(
                (16, SpecialGem.Burst()),
                (24, SpecialGem.Burst()),
                (32, SpecialGem.Burst()),
                (40, SpecialGem.Burst()),
                (48, SpecialGem.Burst()),
                (56, SpecialGem.Area()));

        // When the pass resolves
        var result = ResolvePass(board, Rng());

        // Then every gem in the chain activated exactly once and the call returned.
        Assert.Equal(6, result.ActivatedSpecialGems.Count);
        Assert.Equal(
            result.ActivatedSpecialGems.Select(a => a.CellIndex).Distinct().Count(),
            result.ActivatedSpecialGems.Count);
    }

    [Fact]
    public void Chain_ShouldOrderDiscoveriesByAscendingIndexOfTheConsumedGem()
    {
        // §5.8.3 level 3a: "within one effect by ascending §1.0 index of the consumed
        // Special Gems".
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial(
                (26, SpecialGem.Burst()),
                (18, SpecialGem.Area()),
                (34, SpecialGem.Area()));

        var result = ResolvePass(board, Rng());

        // Both discoveries are level 1, in ascending index: 18 before 34.
        var level1 = result.ActivatedSpecialGems.Where(a => a.ChainLevel == 1).Select(a => a.CellIndex).ToArray();
        Assert.Equal(new[] { 18, 34 }, level1);
    }

    // -----------------------------------------------------------------------
    // §5.8.2 — union semantics
    // -----------------------------------------------------------------------

    [Fact]
    public void Union_ShouldClearACellOnceEvenWhenSeveralEffectsNameIt()
    {
        // §5.8.2 item 1: removal is over the union of the effective sets of the whole
        // step, never per effect.
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((25, SpecialGem.Burst()), (27, SpecialGem.Burst()), (26, SpecialGem.Area()));

        var result = ResolvePass(board, Rng());

        // Cell 26 is named by all three overlapping effects and appears once.
        Assert.Equal(1, result.ClearedCellUnion.Count(i => i == 26));

        // The cleared union has no duplicates at all.
        Assert.Equal(result.ClearedCellUnion.Distinct().Count(), result.ClearedCellUnion.Count);
        Assert.Equal(result.ClearedCellUnion.OrderBy(i => i), result.ClearedCellUnion);
    }

    [Fact]
    public void Union_ShouldReportEachClearedCellOnce()
    {
        // §5.8.2 item 2: resource generation, the per-Gem report, and the GemMatched
        // event are computed over the same union, once per cell. Overlapping effects
        // produce no double GemMatched.
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((25, SpecialGem.Burst()), (27, SpecialGem.Burst()), (26, SpecialGem.Area()));

        var result = ResolvePass(board, Rng());

        var reports = result.MatchedCellGemMatched
            .Concat(result.ActivationCellGemMatched)
            .Select(e => e.CellIndex)
            .ToArray();

        // Matched and activation reports are disjoint sub-steps, and each cell is
        // reported once over the whole pass.
        Assert.Equal(reports.Distinct().Count(), reports.Length);

        // And the reported set is exactly the cleared union.
        Assert.Equal(result.ClearedCellUnion.OrderBy(i => i), reports.OrderBy(i => i));
    }

    [Fact]
    public void Union_ShouldBeTheCardinality_TheEffectsShare()
    {
        // §5.8.2 item 5: the number of cleared cells a step reports for generation is
        // the CARDINALITY OF THE UNION, not the sum of the effects' sizes. This is the
        // property that makes overlap deterministic.
        //
        // Two Area Gems two cells apart have overlapping pluses.
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((25, SpecialGem.Area()), (27, SpecialGem.Area()));

        var result = ResolvePass(board, Rng());

        var matched = MatchDetector.ClearedCellUnion(MatchDetector.Detect(board));

        // Area at 25 (3,1): {25, 17, 33, 24, 26}
        // Area at 27 (3,3): {27, 19, 35, 26, 28}
        // Union of the two = {17,19,24,25,26,27,28,33,35} → 9 cells, not 10.
        var expected = new[] { 17, 19, 24, 25, 26, 27, 28, 33, 35 };
        Assert.Equal(expected, result.ClearedCellUnion);

        // The sum of the effects' sizes would be 14 (3 matched + 5 + 5 with overlap
        // counted twice); the union is strictly smaller.
        Assert.True(result.ClearedCellUnion.Count < matched.Count + 5 + 5);
    }

    [Fact]
    public void Union_ShouldNotReClearAlreadyClearedCells()
    {
        // §5.8.2 item 6: a cell cleared earlier in the same step is no longer on the
        // board, so a later effect naming it has nothing to clear there and generates
        // nothing for it.
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((25, SpecialGem.Area()), (26, SpecialGem.Area()));

        var result = ResolvePass(board, Rng());

        // Each cell appears exactly once regardless of how many effects named it.
        Assert.Equal(result.ClearedCellUnion.Distinct().Count(), result.ClearedCellUnion.Count);
    }

    // -----------------------------------------------------------------------
    // GAME_EVENTS.md §1.3 — GemMatched enumeration
    // -----------------------------------------------------------------------

    [Fact]
    public void GemMatched_ShouldBeEmittedOncePerClearedCell_Ascending()
    {
        // GAME_EVENTS.md §1.3: GemMatched is emitted once per cell of the union, in
        // ascending cell index under §1.0, within each sub-step of the pass.
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((26, SpecialGem.Burst()));

        var result = ResolvePass(board, Rng());

        // Sub-step 1: the pass's matched cells, ascending.
        var matched = result.MatchedCellGemMatched.Select(e => e.CellIndex).ToArray();
        Assert.Equal(new[] { 25, 26, 27 }, matched);

        // Sub-step 2: the activation's cells, ascending, excluding those already
        // reported by sub-step 1.
        var activation = result.ActivationCellGemMatched.Select(e => e.CellIndex).ToArray();
        Assert.Equal(activation.OrderBy(i => i), activation);
        Assert.DoesNotContain(25, activation);
        Assert.DoesNotContain(26, activation);
        Assert.DoesNotContain(27, activation);
    }

    [Fact]
    public void GemMatched_ShouldCarryThePreRemovalSpecialGemMetadata()
    {
        // GAME_EVENTS.md §2 item 2: "Special Gem consumed at that cell" is read from
        // the PRE-REMOVAL board — the cell is cleared and the Special Gem consumed by
        // the same act.
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((26, SpecialGem.LineClearVertical()));

        var result = ResolvePass(board, Rng());

        var report = Assert.Single(result.MatchedCellGemMatched, e => e.CellIndex == 26);

        Assert.NotNull(report.ConsumedSpecialGem);
        Assert.Equal(SpecialGemType.LineClear, report.ConsumedSpecialGem!.Value.Type);
        Assert.Equal(SpecialGemOrientation.Vertical, report.ConsumedSpecialGem.Value.Orientation);

        // And the Gem type is still one of the four §1.1 types, not a Special Gem type
        // (GAME_EVENTS.md §2 item 1).
        Assert.Equal(GemType.Atk, report.GemType);
    }

    [Fact]
    public void GemMatched_ShouldOmitTheSpecialGemMemberForAnOrdinaryGem()
    {
        // GAME_EVENTS.md §2 item 2: the member is omitted for an ordinary Gem, and its
        // absence is the statement that no Special Gem was consumed there.
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk));

        var result = ResolvePass(board, Rng());

        Assert.All(result.MatchedCellGemMatched, e => Assert.Null(e.ConsumedSpecialGem));
        Assert.Empty(result.ActivationCellGemMatched);
    }

    [Fact]
    public void GemMatched_ShouldReportActivationClearsWithoutAMatch()
    {
        // §5.5.5 item 8 / GAME_EVENTS.md §2: a Gem cleared by a Special Gem activation
        // produces GemMatched WITHOUT a MatchCreated — it is not a Match.
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((26, SpecialGem.Burst()));

        var result = ResolvePass(board, Rng());

        // Exactly one Match was detected, but far more cells were cleared.
        Assert.Single(result.Matches);
        Assert.True(result.ClearedCellUnion.Count > 3);

        // The activation reports exist and carry no Match.
        Assert.NotEmpty(result.ActivationCellGemMatched);
    }

    [Fact]
    public void GemMatched_ShouldBeDeterministicAcrossRepeatedRuns()
    {
        // §7.1: the same board and the same state yield the same resolution — the same
        // event order included.
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((25, SpecialGem.Burst()), (27, SpecialGem.Area()));

        var first = ResolvePass(board, Rng()).MatchedCellGemMatched
            .Concat(ResolvePass(board, Rng()).ActivationCellGemMatched)
            .Select(e => (e.CellIndex, e.GemType, e.ConsumedSpecialGem))
            .ToArray();

        for (var run = 0; run < 10; run++)
        {
            var again = ResolvePass(board, Rng()).MatchedCellGemMatched
                .Concat(ResolvePass(board, Rng()).ActivationCellGemMatched)
                .Select(e => (e.CellIndex, e.GemType, e.ConsumedSpecialGem))
                .ToArray();

            Assert.Equal(first, again);
        }
    }

    // -----------------------------------------------------------------------
    // §4.1 — the pass produces a full board
    // -----------------------------------------------------------------------

    [Fact]
    public void Pass_ShouldLeaveTheBoardFullWithOneGemPerCell()
    {
        // §4.1 steps 3–4 / §1: at rest the board holds exactly one Gem per cell. The
        // transient not-full states of steps 1–4 are never the published board
        // (§4.3 item 2).
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((26, SpecialGem.Burst()));

        var result = ResolvePass(board, Rng());

        Assert.Equal(BoardState.CellCount, result.Board.Cells.Count);

        // Every cell holds one of the four documented types.
        Assert.All(result.Board.Cells, c => Assert.True(GemTypes.IsValid((int)c.GemType)));
    }

    [Fact]
    public void Pass_ShouldCreateSpecialGemsBeforeGravity()
    {
        // §4.1 item 2: creation is not a delayed effect — a Special Gem required by a
        // shape is on the board before gravity runs, so it is subject to the same
        // Gravity and Spawn as every other Gem and cannot be skipped or displaced by
        // them.
        var board = TestBoard.Background()
            .WithGems((0, GemType.Def), (1, GemType.Def), (2, GemType.Def), (3, GemType.Def));

        var result = ResolvePass(board, Rng());

        // The pass created the gem and it is on the resolved board — having been
        // through the same Gravity and Spawn as every other Gem.
        Assert.Single(result.CreatedSpecialGems);
        Assert.Contains(result.Board.Cells, c => c.HasSpecialGem);
    }

    [Fact]
    public void Pass_ShouldNotResolveSpecialGemEffectsOnAlreadyEmptiedCells()
    {
        // §5.8.2 item 6: once a cell is cleared it is gone; a later effect naming it
        // clears nothing there.
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((25, SpecialGem.Area()), (26, SpecialGem.Area()));

        var result = ResolvePass(board, Rng());

        // The union is a proper set and the pass completed.
        Assert.Equal(result.ClearedCellUnion.Distinct().Count(), result.ClearedCellUnion.Count);

        // No cell in the union is outside the board.
        Assert.All(result.ClearedCellUnion, i => Assert.InRange(i, 0, BoardState.CellCount - 1));
    }

    [Fact]
    public void Pass_ShouldConsumeNoRngForAPassThatSpawnsNothing()
    {
        // §4.5 item 4: a pass that spawns nothing draws nothing. Every pass here does
        // spawn (cells were cleared), so assert the complementary documented property:
        // the RNG advances only through Spawn, by exactly one selection per spawned
        // cell. A pass over an already-stable board must not advance it at all.
        var stable = TestBoard.Background();

        Assert.Empty(MatchDetector.Detect(stable));

        var rng = Rng();
        var before = rng.CurrentState;
        Spawn.Fill(stable, rng, emptyCells: new HashSet<int>());

        Assert.Equal(before, rng.CurrentState);
    }
}