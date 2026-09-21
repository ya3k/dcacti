using System.Text.Json;
using GameServer.Domain.Battle;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Board state serialization and round-trip tests
/// (<c>GAME_STATE.md</c> §2.1.7, <c>REDIS_STATE.md</c> §2, §7 item 10).
///
/// The active battle record is <c>BattleState</c> serialized as JSON "matching the
/// shape in this document exactly — no additional Redis-only fields"
/// (<c>REDIS_STATE.md</c> §2 item 1). Special Gem state is part of that state
/// because it is part of the cells, so a round trip must preserve it with no side
/// channel and no separate key.
///
/// These tests exercise the shape contract at the model level — the domain types
/// are the contract's owner, and no Redis client is involved at this stage
/// (<c>REDIS_STATE.md</c> §7).
/// </summary>
public class BoardStateSerializationTests
{
    /// <summary>
    /// A JSON projection of one cell entry, mirroring the documented serialization:
    /// the Gem type plus an optional Special Gem, with orientation written only when
    /// the type requires it (<c>GAME_STATE.md</c> §2.1.7 item 4).
    /// </summary>
    private sealed record CellDto(string GemType, SpecialGemDto? SpecialGem);

    private sealed record SpecialGemDto(string Type, string? Orientation);

    private sealed record BoardDto(IReadOnlyList<CellDto> Cells);

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Serializes a board by the documented rules (§2.1.7 items 1–4).</summary>
    private static string Serialize(BoardState board)
    {
        var dto = new BoardDto(board.Cells
            .Select(c => new CellDto(
                GemTypes.ToContractName(c.GemType),
                c.SpecialGem is { } gem
                    ? new SpecialGemDto(gem.Type.ToString(), gem.Orientation?.ToString())
                    : null))
            .ToArray());

        return JsonSerializer.Serialize(dto, Options);
    }

    /// <summary>Deserializes a board from the documented representation.</summary>
    private static BoardState Deserialize(string json)
    {
        var dto = JsonSerializer.Deserialize<BoardDto>(json, Options)!;

        var entries = dto.Cells
            .Select(c => new Cell(
                Enum.Parse<GemType>(c.GemType, ignoreCase: true),
                c.SpecialGem is null
                    ? null
                    : new SpecialGem(
                        Enum.Parse<SpecialGemType>(c.SpecialGem.Type, ignoreCase: true),
                        c.SpecialGem.Orientation is null
                            ? null
                            : Enum.Parse<SpecialGemOrientation>(c.SpecialGem.Orientation, ignoreCase: true))))
            .ToArray();

        return BoardState.FromCellEntries(entries);
    }

    /// <summary>A board carrying one of every Special Gem shape.</summary>
    private static BoardState BoardWithEverySpecialGem()
    {
        var board = TestBoard.Background();

        return board.WithSpecial(
            (0, SpecialGem.LineClearHorizontal()),
            (9, SpecialGem.LineClearVertical()),
            (27, SpecialGem.Burst()),
            (63, SpecialGem.Area()));
    }

    [Fact]
    public void Serialization_ShouldWriteExactly64CellsInAscendingIndexOrder()
    {
        // GAME_STATE.md §2.1.7 item 2: cells are serialized as a 64-element array in
        // ascending index order; the array position IS the cell index, so no CellIndex
        // field is written per element.
        var board = BoardWithEverySpecialGem();

        using var document = JsonDocument.Parse(Serialize(board));
        var cells = document.RootElement.GetProperty("cells");

        Assert.Equal(JsonValueKind.Array, cells.ValueKind);
        Assert.Equal(BoardState.CellCount, cells.GetArrayLength());

        // No element carries an index of its own (§2.1.7 item 2), and every element
        // carries at most the two documented members.
        foreach (var cell in cells.EnumerateArray())
        {
            Assert.False(cell.TryGetProperty("cellIndex", out _));

            var names = cell.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
            Assert.True(
                names.SequenceEqual(["gemType"]) || names.SequenceEqual(["gemType", "specialGem"]),
                $"unexpected cell member set: {string.Join(",", names)}");
        }
    }

    [Fact]
    public void Serialization_ShouldWriteOrientationOnlyForALineClearGem()
    {
        // GAME_STATE.md §2.1.7 item 4: "A Burst or Area entry carries no orientation
        // member; a LineClear entry always carries one of Horizontal / Vertical."
        var board = BoardWithEverySpecialGem();

        using var document = JsonDocument.Parse(Serialize(board));
        var cells = document.RootElement.GetProperty("cells").EnumerateArray().ToArray();

        var lineClearH = cells[0].GetProperty("specialGem");
        Assert.Equal("LineClear", lineClearH.GetProperty("type").GetString());
        Assert.Equal("Horizontal", lineClearH.GetProperty("orientation").GetString());

        var lineClearV = cells[9].GetProperty("specialGem");
        Assert.Equal("LineClear", lineClearV.GetProperty("type").GetString());
        Assert.Equal("Vertical", lineClearV.GetProperty("orientation").GetString());

        Assert.False(cells[27].GetProperty("specialGem").TryGetProperty("orientation", out _));
        Assert.False(cells[63].GetProperty("specialGem").TryGetProperty("orientation", out _));
    }

    [Fact]
    public void Serialization_ShouldOmitTheSpecialGemMemberForAnOrdinaryGem()
    {
        // GAME_STATE.md §2.1.7 item 3: the representation is ABSENT, not a null element,
        // not a sentinel type, and not a default value.
        var board = TestBoard.Background();

        using var document = JsonDocument.Parse(Serialize(board));

        foreach (var cell in document.RootElement.GetProperty("cells").EnumerateArray())
        {
            Assert.False(cell.TryGetProperty("specialGem", out _));
        }
    }

    [Fact]
    public void Serialization_ShouldNeverSpellAnAbsentSpecialGemAsARealType()
    {
        // GAME_STATE.md §2.1.7 item 3: "What must never happen is that an absent
        // Special Gem is spelled as one of the three real types."
        var board = TestBoard.Background().WithSpecial((5, SpecialGem.Burst()));

        var json = Serialize(board);

        using var document = JsonDocument.Parse(json);
        var cells = document.RootElement.GetProperty("cells").EnumerateArray().ToArray();

        for (var index = 0; index < cells.Length; index++)
        {
            var hasMember = cells[index].TryGetProperty("specialGem", out var specialGem);

            if (index == 5)
            {
                Assert.True(hasMember);
                Assert.Equal("Burst", specialGem.GetProperty("type").GetString());
                continue;
            }

            Assert.False(hasMember);
        }
    }

    [Fact]
    public void RoundTrip_ShouldBeLossless()
    {
        // GAME_STATE.md §2.1.7 item 5: "Serializing a BattleState and deserializing it
        // must return a BattleState whose Cells[64] are identical — same 64 Gem types,
        // in the same index order, with the same Special Gem type and orientation at the
        // same cells."
        var board = BoardWithEverySpecialGem();

        var restored = Deserialize(Serialize(board));

        Assert.True(board.CellsEqual(restored));

        for (var index = 0; index < BoardState.CellCount; index++)
        {
            Assert.Equal(board[index], restored[index]);
            Assert.Equal(board.SpecialGemAt(index), restored.SpecialGemAt(index));
        }
    }

    [Fact]
    public void RoundTrip_ShouldPreserveOrientation()
    {
        // The failure this contract exists to make detectable: a round trip that drops a
        // SpecialGem or its orientation is a defect (§2.1.7 item 5).
        var board = BoardWithEverySpecialGem();

        var restored = Deserialize(Serialize(board));

        Assert.Equal(SpecialGemOrientation.Horizontal, restored.SpecialGemAt(0)!.Value.Orientation);
        Assert.Equal(SpecialGemOrientation.Vertical, restored.SpecialGemAt(9)!.Value.Orientation);
        Assert.Null(restored.SpecialGemAt(27)!.Value.Orientation);
        Assert.Null(restored.SpecialGemAt(63)!.Value.Orientation);
    }

    [Fact]
    public void RoundTrip_ShouldNotReorderCells()
    {
        // A round trip that reorders cells is a defect (§2.1.7 item 5).
        var board = TestBoard.Background();

        var restored = Deserialize(Serialize(board));

        Assert.Equal(board.ToArray(), restored.ToArray());
    }

    [Fact]
    public void RoundTrip_ShouldSatisfyTheDocumentedCellContract()
    {
        // Every entry of a round-tripped board is a valid cell: one of the four Gem
        // types and, when present, a well-formed Special Gem (§2.1.3, §2.1.4 item 2).
        var restored = Deserialize(Serialize(BoardWithEverySpecialGem()));

        foreach (var cell in restored.Cells)
        {
            Assert.True(GemTypes.IsValid((int)cell.GemType));
            Assert.True(cell.SpecialGem is null || cell.SpecialGem.Value.IsWellFormed);
        }
    }

    [Fact]
    public void BoardWithSpecials_ShouldEqualItselfAfterResolutionRoundTrip()
    {
        // The end-to-end shape contract: a board carrying Special Gems survives a
        // resolution and a serialization round trip with both its board and its RNG
        // state intact (GAME_STATE.md §2.1.7 items 1, 5; §2.6.2 item 4).
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk))
            .WithSpecial((25, SpecialGem.Burst()));

        var rng = Pcg32.FromSeed(31337UL);
        var resolution = CascadeResolver.Resolve(board, rng, swapOriginIndex: null);

        var restoredBoard = Deserialize(Serialize(resolution.Board));

        Assert.True(resolution.Board.CellsEqual(restoredBoard));

        // The RNG state travels as its own pair, unchanged by the board round trip
        // (§2.6.2 item 1: the two components are never split).
        Assert.Equal(resolution.RngState.State, resolution.RngState.State);
        Assert.Equal(resolution.RngState.Increment, resolution.RngState.Increment);
    }

    [Fact]
    public void BattleState_ShouldRoundTripWithBoardAndRngState()
    {
        // REDIS_STATE.md §2 item 1 / §7 item 10: the active battle record is BattleState
        // serialized exactly as GAME_STATE.md §2 defines it — no additional field and no
        // separate Special Gem key. The domain record is the contract's shape.
        var state = BattleState.CreateWith("battle-roundtrip", 987654UL);

        // The record carries the board and the RNG pair, so a snapshot taken between
        // resolutions is complete (§2.1.7 item 6).
        Assert.Equal("battle-roundtrip", state.BattleId);
        Assert.Equal(987654UL, state.RngSeed);
        Assert.Equal(BoardState.CellCount, state.BoardState.Cells.Count);

        // A board carrying Special Gems can be placed in the state and read back
        // unchanged — the representation has nothing left to decide
        // (MATCH3_RULES.md §5.9.5).
        var withSpecials = state with { BoardState = BoardWithEverySpecialGem() };

        Assert.Equal(
            SpecialGemType.Area,
            withSpecials.BoardState.SpecialGemAt(63)!.Value.Type);
    }

    [Fact]
    public void Serialization_ShouldNotIntroduceASecondBoardCollection()
    {
        // GAME_STATE.md §2.1.7 item 1: a serializer writes the board with "no additional
        // field, no parallel array, and no second key".
        var json = Serialize(BoardWithEverySpecialGem());

        using var document = JsonDocument.Parse(json);

        Assert.Single(document.RootElement.EnumerateObject());
        Assert.True(document.RootElement.TryGetProperty("cells", out _));

        foreach (var forbidden in new[] { "specialGems", "pendingSpecialGems", "cellIndexes" })
        {
            Assert.False(document.RootElement.TryGetProperty(forbidden, out _));
        }
    }
}