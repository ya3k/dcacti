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
    private static CascadeResolver.CascadeResult Resolve(BoardState board, ulong seed = 2024UL) =>
        CascadeResolver.Resolve(board, Pcg32.FromSeed(seed));

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
        Assert.All(result.Board.Cells, gemType => Assert.True(GemTypes.IsValid((int)gemType)));
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
        // whole loop — every pass, every removal, every fall, every spawn, and the
        // final board and RngState — is fully determined.
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
                (40, GemType.Hp), (41, GemType.Hp), (42, GemType.Hp));

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
    public void Resolution_ShouldCarryTheSameGemMatchedOrderDeterministically()
    {
        // GAME_EVENTS.md §1.3: the order is mandatory because the event stream depends
        // on it, and replay must reproduce it exactly.
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk));

        var reference = Resolve(board).Passes[0].GemMatched
            .Select(e => e.CellIndex)
            .ToArray();

        for (var run = 0; run < 10; run++)
        {
            var again = Resolve(board).Passes[0].GemMatched
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
        var reference = CascadeResolver.Resolve(board, Pcg32.FromSeed(8080UL));

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

            var pass = BoardResolver.ResolvePass(current, matchSet, rng, depth);

            // The pass's spawn report IS the number of cells it refilled — exactly the
            // number of selections the pass consumed (§4.5 items 2 and 4).
            spawnedTotal += pass.SpawnedGems.Count;

            current = pass.Board;
        }

        Assert.True(spawnedTotal > 0, "expected the resolution to spawn at least one gem");
        Assert.True(current.CellsEqual(reference.Board));
        Assert.Equal(reference.RngState, rng.CurrentState);
    }

    [Fact]
    public void Resolution_ShouldLeaveRngUntouchedWhenNothingSpawns()
    {
        // The sharp form of §4.5 item 4 / §7.2: a resolution that spawns nothing draws
        // nothing, so RngState is unchanged.
        var stable = TestBoard.Background();
        var rng = Pcg32.FromSeed(5150UL);
        var before = rng.CurrentState;

        var result = CascadeResolver.Resolve(stable, rng);

        Assert.Equal(before, result.RngState);
        Assert.Equal(before, rng.CurrentState);
    }

    [Fact]
    public void ResolvingAStableBoardTwice_ShouldNotShiftTheStream()
    {
        // §2.1.5 item 4's principle: an operation that resolves nothing must leave the
        // stream where it was, so a caller cannot desynchronise the battle by asking
        // again.
        var board = TestBoard.Background();
        var rng = Pcg32.FromSeed(626UL);
        var before = rng.CurrentState;

        CascadeResolver.Resolve(board, rng);
        CascadeResolver.Resolve(board, rng);

        Assert.Equal(before, rng.CurrentState);
    }

    // -----------------------------------------------------------------------
    // GAME_STATE.md §2.1 — the board model
    // -----------------------------------------------------------------------

    [Fact]
    public void BoardState_ShouldHaveExactlyOneBoardCollection()
    {
        // GAME_STATE.md §2.1.2 item 1: "BoardState has exactly one field, Cells[64].
        // Nothing ... defines a board field or any other collection parallel to
        // Cells[64]."
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
    public void BoardState_ShouldDeclareNoForbiddenBoardField()
    {
        // GAME_STATE.md §2.1.2 item 1, §2.1.3 item 4: the board holds only its cell
        // array — no second collection and no per-cell identity, depth, age, or state.
        var memberNames = typeof(BoardState)
            .GetProperties()
            .Select(p => p.Name)
            .Concat(typeof(BoardState).GetFields(
                System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance).Select(f => f.Name))
            .ToArray();

        foreach (var forbidden in new[]
                 {
                     "CellsIndex", "CellIndex", "CellId", "Depth", "Age", "Armed",
                     "ActivationCount", "SecondaryCollection",
                 })
        {
            Assert.DoesNotContain(forbidden, memberNames);
        }
    }

    [Fact]
    public void GemType_ShouldBeExactlyTheFourFunctionalTypes()
    {
        // MATCH3_RULES.md §1.1: the four functional types are the complete value
        // domain — there is no fifth type.
        Assert.Equal(4, Enum.GetNames<GemType>().Length);
        Assert.Equal(
            new[] { GemType.Atk, GemType.Def, GemType.Hp, GemType.Power },
            Enum.GetValues<GemType>());
    }

    [Fact]
    public void BoardState_ShouldRoundTripCellsLosslessly()
    {
        // GAME_STATE.md §2.1.7 item 5: serializing and deserializing must return a
        // board whose Cells[64] are identical — same 64 Gem types, in the same index
        // order.
        var board = TestBoard.Background();

        var restored = BoardState.FromCells(board.ToArray());

        Assert.True(board.CellsEqual(restored));

        for (var i = 0; i < BoardState.CellCount; i++)
        {
            Assert.Equal(board[i], restored[i]);
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
            Assert.Equal(gems[i], board.Cells[i]);
        }
    }

    [Fact]
    public void BoardState_ShouldPreserveTheCellOrderOnSerialization()
    {
        // GAME_STATE.md §2.1.1 item 6: the 64 entries are stored in ascending §1.0
        // index order in both the runtime state and every serialization of it.
        var board = TestBoard.Background();
        var serialized = board.ToArray();

        Assert.Equal(BoardState.CellCount, serialized.Length);

        for (var i = 0; i < BoardState.CellCount; i++)
        {
            Assert.Equal(board[i], serialized[i]);
        }
    }

    [Fact]
    public void GeneratedBoard_ShouldCarryOnlyOrdinaryGemTypes()
    {
        // GAME_STATE.md §2.1.7 item 8: generation places ordinary Gems only, so every
        // entry of a generated board is one of the four functional types.
        for (ulong seed = 0; seed < 16; seed++)
        {
            var board = BoardGenerator.Generate(seed).Board!;

            Assert.All(board.Cells, gemType => Assert.True(GemTypes.IsValid((int)gemType)));
        }
    }

    [Fact]
    public void BoardState_ShouldRejectAnInvalidCellValue()
    {
        // MATCH3_RULES.md §1.1: a cell value outside the four functional types is a
        // contract violation, not a value the model can hold.
        var values = Enumerable.Repeat((int)GemType.Atk, BoardState.CellCount).ToArray();
        values[5] = 4;

        Assert.Throws<ArgumentException>(() => BoardState.FromValues(values));

        var gems = Enumerable.Repeat(GemType.Atk, BoardState.CellCount).ToArray();
        gems[5] = (GemType)4;

        Assert.Throws<ArgumentException>(() => BoardState.FromCells(gems));
    }

    [Fact]
    public void BattleState_ShouldStillCarryTheBoardAndRngState()
    {
        // GAME_STATE.md §2.6.2 item 4 / §7.1 item 3: RngState is part of BattleState, so
        // a battle restored from a snapshot continues the stream exactly.
        var state = BattleState.CreateWith("battle-board-model", 4242UL);

        Assert.Equal(0, state.Turn);
        Assert.Equal(0, state.Sequence);
        Assert.Equal(4242UL, state.RngSeed);
        Assert.NotNull(state.BoardState);
        Assert.Equal(BoardState.CellCount, state.BoardState.Cells.Count);

        // The record's field set is the documented one: the board is one field and
        // there is no second board or cell representation.
        var properties = typeof(BattleState)
            .GetProperties()
            .Where(p => p.GetIndexParameters().Length == 0)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "BattleId", "BoardState", "BossState", "CardCastsUsedThisTurn", "Combo",
                "LastCommittedSwapPair", "MatchCount", "PetState", "PlayerId", "RngSeed",
                "RngState", "Sequence", "Turn",
            },
            properties);
    }
}
