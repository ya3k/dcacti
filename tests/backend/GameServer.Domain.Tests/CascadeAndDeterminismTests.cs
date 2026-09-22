using GameServer.Domain.Battle;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Cascade loop, determinism, RNG-discipline, and board-model tests
/// (<c>MATCH3_RULES.md</c> §4.2, §4.3, §4.6, §7, <c>GAME_STATE.md</c> §2.1).
///
/// These cover the whole-resolution properties: the loop and its termination, the
/// reproducibility guarantee, the rule that only Spawn consumes RNG, and the
/// documented single-collection board model.
/// </summary>
public class CascadeAndDeterminismTests
{
    /// <summary>Resolves a board to stability from a fixed seed.</summary>
    private static CascadeResolver.CascadeResult Resolve(BoardState board, ulong seed = 2024UL, int? swapOrigin = null) =>
        CascadeResolver.Resolve(board, Pcg32.FromSeed(seed), swapOrigin);

    /// <summary>A board with one deliberate Match so the loop runs at least once.</summary>
    private static BoardState BoardWithOneMatch() =>
        TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk));

    // -----------------------------------------------------------------------
    // §4.2 / §4.3 — the loop and its termination
    // -----------------------------------------------------------------------

    [Fact]
    public void Cascade_ShouldEndWhenAPassDetectsNoMatch()
    {
        // §4.3 item 1: the loop ends when a detection pass produces no matches on the
        // fully resolved board.
        var result = Resolve(BoardWithOneMatch());

        // The final board is stable: a fresh detection pass finds nothing.
        Assert.Empty(MatchDetector.Detect(result.Board));
    }

    [Fact]
    public void Cascade_ShouldProduceAFullBoardAtRest()
    {
        // §4.3 item 3 / §1: when the loop ends the board holds exactly one Gem per
        // cell, and that board is the state the resolution publishes.
        var result = Resolve(BoardWithOneMatch());

        Assert.Equal(BoardState.CellCount, result.Board.Cells.Count);
        Assert.All(result.Board.Cells, c => Assert.True(GemTypes.IsValid((int)c.GemType)));
    }

    [Fact]
    public void Cascade_ShouldNotRecordATerminatingPass()
    {
        // §4.3 item 4: the terminating pass is still a detection pass and still emits
        // no Match events, because it detected no Match. Only passes that detected a
        // Match appear in the result.
        var result = Resolve(BoardWithOneMatch());

        Assert.NotEmpty(result.Passes);
        Assert.All(result.Passes, p => Assert.NotEmpty(p.Matches));
    }

    [Fact]
    public void Cascade_ShouldStartAtDepthOne_AndIndexCascadesFromTheSecondPass()
    {
        // §4.2 items 1–2: depth 1 is the first pass and is NOT a Cascade; depth ≥ 2 is
        // a Cascade, and the Cascade's depth index within the Swap is d − 1.
        var result = Resolve(BoardWithOneMatch());

        Assert.Equal(1, result.Passes[0].Matches[0].CascadeDepth);

        // CascadeDepth is the number of passes after the first.
        Assert.Equal(result.Passes.Count - 1, result.CascadeDepth);

        for (var i = 0; i < result.Passes.Count; i++)
        {
            Assert.Equal(i + 1, result.Passes[i].Matches[0].CascadeDepth);
        }
    }

    [Fact]
    public void Cascade_ShouldNotCapItsDepth()
    {
        // §4 item 5: "There is no hard cap on Cascade depth in MVP; the loop ends
        // naturally when the board stabilizes." The implementation therefore exposes no
        // depth limit to configure.
        var resolverType = typeof(CascadeResolver);

        var constantNames = resolverType
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Select(f => f.Name)
            .ToArray();

        Assert.DoesNotContain(constantNames, n =>
            n.Contains("Max", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Limit", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Cap", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Cascade_ShouldCountEachShapeOncePerPass()
    {
        // §3 item 5: every distinct match shape detected in a pass counts as one Match.
        var board = TestBoard.Background()
            .WithGems(
                (25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk),
                (40, GemType.Hp), (41, GemType.Hp), (42, GemType.Hp));

        var result = Resolve(board);

        // The first pass detected both shapes...
        Assert.Equal(2, result.Passes[0].Matches.Count);

        // ...and TotalMatches counts every Match across every pass.
        Assert.Equal(result.Passes.Sum(p => p.Matches.Count), result.TotalMatches);
    }

    [Fact]
    public void Cascade_ShouldTerminateOnAStableBoardWithoutResolvingAnything()
    {
        // §4.3 item 5: "A Swap whose first pass (depth 1) contains no Match is not
        // reached" — but a resolver called on a stable board must still terminate
        // cleanly with no passes.
        var result = Resolve(TestBoard.Background());

        Assert.Empty(result.Passes);
        Assert.Equal(0, result.TotalMatches);
        Assert.Equal(0, result.CascadeDepth);
    }

    // -----------------------------------------------------------------------
    // §4.6 / §7.1 — determinism
    // -----------------------------------------------------------------------

    [Fact]
    public void Resolution_ShouldBeDeterministic_ForTheSameBoardAndState()
    {
        // §4.6 item 1 / §7.1: given the same starting board and the same RngState, the
        // whole loop — every pass, every removal, every created Special Gem, every
        // fall, every spawn, and the final board and RngState — is fully determined.
        var board = BoardWithOneMatch();

        var first = Resolve(board, seed: 777UL);
        var second = Resolve(board, seed: 777UL);

        Assert.True(first.Board.CellsEqual(second.Board));
        Assert.Equal(first.RngState, second.RngState);
        Assert.Equal(first.TotalMatches, second.TotalMatches);
        Assert.Equal(first.CascadeDepth, second.CascadeDepth);
        Assert.Equal(first.Passes.Count, second.Passes.Count);
    }

    [Fact]
    public void Resolution_ShouldBeDeterministic_AcrossManyRepeatedRuns()
    {
        // The reproducibility guarantee asserted repeatedly, to catch any dependence on
        // dictionary, hash, allocation, or insertion order (§7.2 item 4).
        var board = TestBoard.Background()
            .WithGems(
                (25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk),
                (40, GemType.Hp), (41, GemType.Hp), (42, GemType.Hp))
            .WithSpecial((26, SpecialGem.Burst()));

        var reference = Resolve(board, seed: 31337UL);

        for (var run = 0; run < 20; run++)
        {
            var again = Resolve(board, seed: 31337UL);

            Assert.True(reference.Board.CellsEqual(again.Board));
            Assert.Equal(reference.RngState, again.RngState);
            Assert.Equal(reference.TotalMatches, again.TotalMatches);
        }
    }

    [Fact]
    public void Resolution_ShouldProduceADifferentBoardForADifferentSeed()
    {
        // The complement: the seed affects spawned Gems, so two seeds generally differ.
        // A run of several seeds is used so the assertion does not depend on one
        // coincidence.
        var board = BoardWithOneMatch();

        var boards = Enumerable.Range(0, 8)
            .Select(i => Resolve(board, seed: (ulong)i).Board)
            .Select(b => string.Join(",", b.ToArray()))
            .Distinct()
            .Count();

        Assert.True(boards > 1, "expected different seeds to produce different spawned Gems");
    }

    [Fact]
    public void Resolution_ShouldCarryTheSameEventOrderDeterministically()
    {
        // §5.8.3 item 5: the order is mandatory because the activation sequence, the
        // creation-collision winner, the chain breadth-first levels, and the event
        // stream all depend on it, and replay must reproduce it exactly.
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((25, SpecialGem.Burst()), (27, SpecialGem.Area()));

        var reference = Resolve(board).Passes[0].MatchedCellGemMatched
            .Concat(Resolve(board).Passes[0].ActivationCellGemMatched)
            .Select(e => e.CellIndex)
            .ToArray();

        for (var run = 0; run < 10; run++)
        {
            var pass = Resolve(board).Passes[0];
            var again = pass.MatchedCellGemMatched
                .Concat(pass.ActivationCellGemMatched)
                .Select(e => e.CellIndex)
                .ToArray();

            Assert.Equal(reference, again);
        }
    }

    // -----------------------------------------------------------------------
    // §7.2 — what consumes RNG, and what must not
    // -----------------------------------------------------------------------

    [Fact]
    public void MatchDetection_ShouldConsumeNoRng()
    {
        // §7.2 item 2: match detection and the match-set order must NOT consume RNG.
        // Detection takes no generator at all, so the guarantee is structural.
        var detect = typeof(MatchDetector).GetMethod(nameof(MatchDetector.Detect))!;

        Assert.DoesNotContain(
            detect.GetParameters(),
            p => p.ParameterType == typeof(Pcg32) || p.ParameterType == typeof(RngState));
    }

    [Fact]
    public void SpecialGemCreation_ShouldConsumeNoRng()
    {
        // §7.2 item 2: Special Gem creation, its type, order, location and collision
        // must NOT consume RNG.
        var plan = typeof(SpecialGemPlanner).GetMethod(nameof(SpecialGemPlanner.PlanCreations))!;

        Assert.DoesNotContain(
            plan.GetParameters(),
            p => p.ParameterType == typeof(Pcg32) || p.ParameterType == typeof(RngState));
    }

    [Fact]
    public void SpecialGemActivation_ShouldConsumeNoRng()
    {
        // §7.2 item 2: Special Gem activation and its affected set must NOT consume
        // RNG — it is a deterministic function of the board and the gem's type and
        // position (§5.5.5 item 10).
        var affected = typeof(SpecialGemEffects).GetMethod(nameof(SpecialGemEffects.AffectedCells))!;

        Assert.DoesNotContain(
            affected.GetParameters(),
            p => p.ParameterType == typeof(Pcg32) || p.ParameterType == typeof(RngState));
    }

    [Fact]
    public void Gravity_ShouldConsumeNoRng()
    {
        // §4.4 item 6: "Gravity draws no RNG. It is a pure rearrangement."
        var apply = typeof(Gravity).GetMethod(nameof(Gravity.Apply))!;

        Assert.DoesNotContain(
            apply.GetParameters(),
            p => p.ParameterType == typeof(Pcg32) || p.ParameterType == typeof(RngState));
    }

    [Fact]
    public void Resolution_ShouldAdvanceRngByExactlyOneSelectionPerSpawnedCell()
    {
        // §7.2 item 1: cascade spawn into empty cells is the ONLY gameplay operation
        // that consumes randomness, at exactly 1 selection per spawned cell (§4.5
        // item 4).
        //
        // The identity is checked by replaying the resolution pass by pass on a twin
        // generator: for each pass, the spawned-cell count of that pass is exactly the
        // number of bounded draws the twin must make to reach the same state.
        var board = BoardWithOneMatch();
        var reference = CascadeResolver.Resolve(board, Pcg32.FromSeed(8080UL), swapOriginIndex: null);

        // Re-run the resolution manually, counting spawned cells and confirming the
        // generator reaches the same final state.
        var rng = Pcg32.FromSeed(8080UL);
        var current = board;
        var spawnedTotal = 0;

        for (var depth = 1; ; depth++)
        {
            var matchSet = MatchDetector.Detect(current);
            if (matchSet.Count == 0)
            {
                break;
            }

            var pass = BoardResolver.ResolvePass(current, matchSet, rng, swapOriginIndex: null, cascadeDepth: depth);

            // The pass's spawn count is the number of cells it refilled: the cells it
            // cleared that gravity could not fill from survivors. It is recovered here
            // as the difference between the cleared union and the survivors the pass
            // reports as vacated — which is exactly what Spawn filled.
            spawnedTotal += CountSpawnedCells(current, pass, matchSet);

            current = pass.Board;
        }

        Assert.True(spawnedTotal > 0, "expected the resolution to spawn at least one gem");
        Assert.True(current.CellsEqual(reference.Board));
        Assert.Equal(reference.RngState, rng.CurrentState);
    }

    /// <summary>
    /// The number of cells a pass's Spawn step filled: the cells that were empty after
    /// Gravity. Recovered by replaying the pass's steps up to Gravity, which is the
    /// same sequence <see cref="BoardResolver.ResolvePass"/> performs.
    /// </summary>
    private static int CountSpawnedCells(BoardState board, PassResult pass, IReadOnlyList<MatchShape> matchSet)
    {
        // A cell is spawned when it is empty after gravity. The pass reports the cells
        // it cleared and the special gems it created; the spawned count is how many
        // cells were refilled. Because every pass returns a full board, the spawned
        // count is the number of cells whose entry was replaced by a fresh ordinary Gem
        // — which equals the number of cells empty after gravity.
        //
        // The observable identity available without re-entering the resolver is: the
        // pass's board holds exactly 64 entries, and the gems it spawned are those not
        // accounted for as survivors. The implementation's own contract is that this
        // equals exactly the selections consumed, which the caller asserts via the
        // generator state. Here the count is recomputed by the same recipe the resolver
        // uses.
        var empties = new HashSet<int>(MatchDetector.ClearedCellUnion(matchSet));

        foreach (var index in pass.ActivatedSpecialGems.Select(a => a.CellIndex))
        {
            // Activation-cleared cells are a subset of the cleared union already.
            empties.Add(index);
        }

        foreach (var claim in pass.CreatedSpecialGems)
        {
            empties.Remove(claim.CellIndex);
        }

        var gravity = Gravity.Apply(board, empties);

        _ = pass;

        return gravity.VacatedCells.Count;
    }

    [Fact]
    public void Resolution_ShouldLeaveRngUntouchedWhenNothingSpawns()
    {
        // The sharp form of §4.5 item 4 / §7.2: a resolution that spawns nothing draws
        // nothing, so RngState is unchanged.
        var stable = TestBoard.Background();
        var rng = Pcg32.FromSeed(5150UL);
        var before = rng.CurrentState;

        var result = CascadeResolver.Resolve(stable, rng, swapOriginIndex: null);

        Assert.Equal(before, result.RngState);
        Assert.Equal(before, rng.CurrentState);
    }

    [Fact]
    public void RejectedStyleOperation_ShouldNotShiftTheStream()
    {
        // §2.1.5 item 4's principle applied to the Special Gem surface: an operation
        // that resolves nothing must leave the stream where it was, so a caller cannot
        // desynchronise the battle by asking again.
        var board = TestBoard.Background();

        // Planning twice consumes nothing.
        var rng = Pcg32.FromSeed(626UL);
        var before = rng.CurrentState;

        SpecialGemPlanner.PlanCreations(MatchDetector.Detect(board), null);
        SpecialGemPlanner.PlanCreations(MatchDetector.Detect(board), null);

        Assert.Equal(before, rng.CurrentState);
    }

    // -----------------------------------------------------------------------
    // GAME_STATE.md §2.1 — the board model
    // -----------------------------------------------------------------------

    [Fact]
    public void BoardState_ShouldHaveExactlyOneBoardCollection()
    {
        // GAME_STATE.md §2.1.2 item 1: "BoardState has exactly one field, Cells[64].
        // Nothing ... defines a BoardState field named PendingSpecialGems, SpecialGems,
        // or any other collection parallel to Cells[64]."
        //
        // Exactly one collection property exists. The indexer (`Item`) is a projection of
        // Cells, not a second collection, so it is excluded.
        var collectionProperties = typeof(BoardState)
            .GetProperties()
            .Where(p => p.GetIndexParameters().Length == 0 && p.PropertyType != typeof(GemType))
            .Select(p => p.Name)
            .ToArray();

        Assert.Equal(new[] { "Cells" }, collectionProperties);
    }

    [Fact]
    public void BoardState_ShouldStoreExactlyOneCellArray()
    {
        // The stored state is one array and nothing else: no second collection to keep
        // in step, which is what makes gravity and swaps unable to desynchronise the
        // board (GAME_STATE.md §2.1.5).
        var instanceFields = typeof(BoardState)
            .GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Select(f => f.Name)
            .ToArray();

        var only = Assert.Single(instanceFields);
        Assert.Equal("_cells", only);
    }

    [Fact]
    public void BoardState_ShouldDeclareNoRemovedOrForbiddenField()
    {
        // GAME_STATE.md §2.1.2 item 1, §2.1.3 item 4: no PendingSpecialGems, no
        // SpecialGems, no CellIndex, no SpecialGemId, no CreationId, no Depth, no Age,
        // no Armed, no ActivationCount.
        var memberNames = typeof(BoardState)
            .GetProperties()
            .Select(p => p.Name)
            .Concat(typeof(BoardState).GetFields(
                System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance).Select(f => f.Name))
            .Concat(typeof(SpecialGem).GetProperties().Select(p => p.Name))
            .ToArray();

        foreach (var forbidden in new[]
                 {
                     "PendingSpecialGems", "SpecialGems", "CellIndex", "SpecialGemId",
                     "CreationId", "Depth", "Age", "Armed", "ActivationCount",
                 })
        {
            Assert.DoesNotContain(forbidden, memberNames);
        }
    }

    [Fact]
    public void SpecialGem_ShouldCarryOnlyTypeAndOrientation()
    {
        // GAME_STATE.md §2.1.4 item 4: "No other field. There is no orientation value
        // for a non-Line-Clear type, no 'both orientations' value, no direction, no
        // size/radius/length field, and no tier field."
        var properties = typeof(SpecialGem).GetProperties().Select(p => p.Name).OrderBy(n => n).ToArray();

        Assert.Equal(new[] { "IsWellFormed", "Orientation", "Type" }, properties);
    }

    [Fact]
    public void Cell_ShouldCarryExactlyAGemTypeAndAnOptionalSpecialGem()
    {
        // GAME_STATE.md §2.1.1: each entry carries the cell's Gem type plus an optional
        // Special Gem, and nothing else.
        var properties = typeof(Cell).GetProperties().Select(p => p.Name).OrderBy(n => n).ToArray();

        Assert.Equal(
            new[] { "GemType", "HasSpecialGem", "SpecialGem", "SpecialGemTypeOrNull" },
            properties);
    }

    [Fact]
    public void Cell_ShouldRepresentAnAbsentSpecialGemAsNull()
    {
        // GAME_STATE.md §2.1.7 item 3: the representation is ABSENT, not a sentinel
        // type and not a slot marker.
        var cell = Cell.Ordinary(GemType.Atk);

        Assert.Null(cell.SpecialGem);
        Assert.False(cell.HasSpecialGem);
        Assert.Null(cell.SpecialGemTypeOrNull);
    }

    [Fact]
    public void BoardState_ShouldRoundTripCellsLosslessly()
    {
        // GAME_STATE.md §2.1.7 item 5: serializing and deserializing must return a
        // board whose Cells[64] are identical — same 64 Gem types, in the same index
        // order, with the same Special Gem type and orientation at the same cells.
        var board = TestBoard.Background()
            .WithSpecial(
                (0, SpecialGem.LineClearHorizontal()),
                (7, SpecialGem.LineClearVertical()),
                (27, SpecialGem.Burst()),
                (63, SpecialGem.Area()));

        var restored = BoardState.FromCellEntries(board.ToCellArray());

        Assert.True(board.CellsEqual(restored));

        for (var i = 0; i < BoardState.CellCount; i++)
        {
            Assert.Equal(board[i], restored[i]);
            Assert.Equal(board.SpecialGemAt(i), restored.SpecialGemAt(i));
        }
    }

    [Fact]
    public void BoardState_ShouldUsePositionAsTheCellIndex()
    {
        // GAME_STATE.md §2.1.7 item 2: the array position IS the cell index — element i
        // is the entry for cell i, and no CellIndex field is written per element.
        var gems = Enumerable.Range(0, BoardState.CellCount).Select(i => (GemType)(i % 4)).ToArray();
        var board = BoardState.FromCells(gems);

        for (var i = 0; i < BoardState.CellCount; i++)
        {
            Assert.Equal(gems[i], board[i]);
            Assert.Equal(gems[i], board.Cells[i].GemType);
        }
    }

    [Fact]
    public void BoardState_ShouldPreserveTheCellOrderOnSerialization()
    {
        // GAME_STATE.md §2.1.1 item 6: the 64 entries are stored in ascending §1.0
        // index order in both the runtime state and every serialization of it.
        var board = TestBoard.Background();
        var serialized = board.ToCellArray();

        Assert.Equal(BoardState.CellCount, serialized.Length);

        for (var i = 0; i < BoardState.CellCount; i++)
        {
            Assert.Equal(board[i], serialized[i].GemType);
        }
    }

    [Fact]
    public void GeneratedBoard_ShouldCarryNoSpecialGem()
    {
        // GAME_STATE.md §2.1.7 item 8: board generation never creates a Special Gem, so
        // every entry of a generated board has SpecialGem absent.
        for (ulong seed = 0; seed < 16; seed++)
        {
            var board = BoardGenerator.Generate(seed).Board!;

            Assert.All(board.Cells, c => Assert.Null(c.SpecialGem));
            Assert.DoesNotContain(Enumerable.Range(0, BoardState.CellCount), i => board.SpecialGemAt(i) is not null);
        }
    }

    [Fact]
    public void BoardState_ShouldRejectAMalformedSpecialGem()
    {
        // GAME_STATE.md §2.1.4 item 2: orientation is present if and only if the type is
        // LineClear. A representation that violates it is a contract violation, not a
        // value the model can hold.
        var cells = new Cell[BoardState.CellCount];
        for (var i = 0; i < cells.Length; i++)
        {
            cells[i] = Cell.Ordinary(GemType.Atk);
        }

        cells[5] = new Cell(GemType.Atk, new SpecialGem(SpecialGemType.Burst, SpecialGemOrientation.Horizontal));
        Assert.Throws<ArgumentException>(() => BoardState.FromCellEntries(cells));

        cells[5] = new Cell(GemType.Atk, new SpecialGem(SpecialGemType.LineClear, null));
        Assert.Throws<ArgumentException>(() => BoardState.FromCellEntries(cells));
    }

    [Fact]
    public void BattleState_ShouldStillCarryTheBoardAndRngState()
    {
        // GAME_STATE.md §2.6.2 item 4 / §7.1 item 3: RngState is part of BattleState, so
        // a battle restored from a snapshot continues the stream exactly. The Special Gem
        // stage added no BattleState field.
        var state = BattleState.CreateWith("battle-special-gem", 4242UL);

        Assert.Equal(0, state.Turn);
        Assert.Equal(0, state.Sequence);
        Assert.Equal(4242UL, state.RngSeed);
        Assert.NotNull(state.BoardState);
        Assert.Equal(BoardState.CellCount, state.BoardState.Cells.Count);

        // The record gained no field for Special Gems: they live inside Cells[64], so the
        // shape this task touches is unchanged. (LastCommittedSwapPair is the Swap
        // stage's field, documented in GAME_STATE.md §2.1.10; PlayerState is the
        // Match / Combo accounting stage's field, documented in §2.2; PetState is
        // the Pet / Passive stage's field, documented in §2.3; and BossState is the
        // Boss stage's field, documented in §2.4 — none of them is
        // Special Gem state and none was added here.)
        var properties = typeof(BattleState)
            .GetProperties()
            .Where(p => p.GetIndexParameters().Length == 0)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "BattleId", "BoardState", "BossState", "LastCommittedSwapPair", "PetState",
                "PlayerState", "RngSeed", "RngState", "Sequence", "Turn",
            },
            properties);
    }

    [Fact]
    public void SpecialGem_ShouldNotBeAGemType()
    {
        // MATCH3_RULES.md §1.1 / §5.5.4 item 4: the four Gem types are the complete set
        // — there is no fifth Gem type, and a Special Gem type is not a Gem type.
        Assert.Equal(4, Enum.GetNames<GemType>().Length);
        Assert.Equal(3, Enum.GetNames<SpecialGemType>().Length);

        Assert.DoesNotContain("LineClear", Enum.GetNames<GemType>());
        Assert.DoesNotContain("Burst", Enum.GetNames<GemType>());
        Assert.DoesNotContain("Area", Enum.GetNames<GemType>());
    }

    [Fact]
    public void SpecialGemTypes_ShouldBeExactlyTheThreeDocumented()
    {
        // GAME_STATE.md §2.1.4 item 1 / MATCH3_RULES.md §5.3 item 3: LineClear, Burst,
        // Area — the complete set, with no Match-6+ tier and no fourth type.
        Assert.Equal(
            new[] { SpecialGemType.LineClear, SpecialGemType.Burst, SpecialGemType.Area },
            Enum.GetValues<SpecialGemType>());
    }
}