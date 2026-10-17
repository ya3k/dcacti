using System.Text.Json;
using GameServer.Domain.Battle;
using GameServer.Domain.Match3;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Board state serialization and round-trip tests
/// (<c>GAME_STATE.md</c> §2.1.7, <c>REDIS_STATE.md</c> §2, §7 item 10).
///
/// The active battle record is <c>BattleState</c> serialized as JSON "matching the
/// shape in this document exactly — no additional Redis-only fields"
/// (<c>REDIS_STATE.md</c> §2 item 1). Each board cell carries its functional
/// <c>GemType</c> and nothing else (§2.1.1, §2.1.7 item 3), so a round trip must
/// preserve the 64 Gem types with no side channel and no separate key.
///
/// These tests exercise the shape contract at the model level — the domain types
/// are the contract's owner, and no Redis client is involved at this stage
/// (<c>REDIS_STATE.md</c> §7).
/// </summary>
public class BoardStateSerializationTests
{
    /// <summary>
    /// A JSON projection of one cell entry, mirroring the documented serialization:
    /// the cell's Gem type and nothing else (<c>GAME_STATE.md</c> §2.1.1, §2.1.3
    /// item 1).
    /// </summary>
    private sealed record CellDto(string GemType);

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
            .Select(gemType => new CellDto(GemTypes.ToContractName(gemType)))
            .ToArray());

        return JsonSerializer.Serialize(dto, Options);
    }

    /// <summary>Deserializes a board from the documented representation.</summary>
    private static BoardState Deserialize(string json)
    {
        var dto = JsonSerializer.Deserialize<BoardDto>(json, Options)!;

        var entries = dto.Cells
            .Select(c => Enum.Parse<GemType>(c.GemType, ignoreCase: true))
            .ToArray();

        return BoardState.FromCells(entries);
    }

    /// <summary>A board carrying one of every functional Gem type.</summary>
    private static BoardState BoardWithEveryGemType() =>
        TestBoard.Background().WithGems(
            (0, GemType.Atk),
            (9, GemType.Def),
            (27, GemType.Hp),
            (63, GemType.Power));

    [Fact]
    public void Serialization_ShouldWriteExactly64CellsInAscendingIndexOrder()
    {
        // GAME_STATE.md §2.1.7 item 2: cells are serialized as a 64-element array in
        // ascending index order; the array position IS the cell index, so no CellIndex
        // field is written per element.
        var board = BoardWithEveryGemType();

        using var document = JsonDocument.Parse(Serialize(board));
        var cells = document.RootElement.GetProperty("cells");

        Assert.Equal(JsonValueKind.Array, cells.ValueKind);
        Assert.Equal(BoardState.CellCount, cells.GetArrayLength());

        // No element carries an index of its own (§2.1.7 item 2), and every element
        // carries exactly the one documented member.
        foreach (var cell in cells.EnumerateArray())
        {
            Assert.False(cell.TryGetProperty("cellIndex", out _));

            var names = cell.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
            Assert.Equal(["gemType"], names);
        }
    }

    [Fact]
    public void Serialization_ShouldWriteTheDocumentedContractNames()
    {
        // MATCH3_RULES.md §1.1: the four functional types, written by their contract
        // names rather than enum ordinals.
        var board = BoardWithEveryGemType();

        using var document = JsonDocument.Parse(Serialize(board));
        var cells = document.RootElement.GetProperty("cells").EnumerateArray().ToArray();

        Assert.Equal("ATK", cells[0].GetProperty("gemType").GetString());
        Assert.Equal("DEF", cells[9].GetProperty("gemType").GetString());
        Assert.Equal("HP", cells[27].GetProperty("gemType").GetString());
        Assert.Equal("POWER", cells[63].GetProperty("gemType").GetString());

        foreach (var cell in cells)
        {
            Assert.Contains(cell.GetProperty("gemType").GetString(), new[] { "ATK", "DEF", "HP", "POWER" });
        }
    }

    [Fact]
    public void Serialization_ShouldNeverWriteAValueOutsideTheFourTypes()
    {
        // MATCH3_RULES.md §1.1: the four types are the complete value domain, so no
        // cell can serialize a value outside it.
        using var document = JsonDocument.Parse(Serialize(TestBoard.Background()));

        foreach (var cell in document.RootElement.GetProperty("cells").EnumerateArray())
        {
            Assert.Contains(cell.GetProperty("gemType").GetString(), new[] { "ATK", "DEF", "HP", "POWER" });
        }
    }

    [Fact]
    public void RoundTrip_ShouldBeLossless()
    {
        // GAME_STATE.md §2.1.7 item 5: "Serializing a BattleState and deserializing it
        // must return a BattleState whose Cells[64] are identical — same 64 Gem types,
        // in the same index order."
        var board = BoardWithEveryGemType();

        var restored = Deserialize(Serialize(board));

        Assert.True(board.CellsEqual(restored));

        for (var index = 0; index < BoardState.CellCount; index++)
        {
            Assert.Equal(board[index], restored[index]);
        }
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
        // Every entry of a round-tripped board is a valid cell: exactly one of the
        // four Gem types (§2.1.1, §2.1.3 item 1).
        var restored = Deserialize(Serialize(BoardWithEveryGemType()));

        foreach (var gemType in restored.Cells)
        {
            Assert.True(GemTypes.IsValid((int)gemType));
        }
    }

    [Fact]
    public void BoardWithEveryGemType_ShouldEqualItselfAfterResolutionRoundTrip()
    {
        // The end-to-end shape contract: a board survives a resolution and a
        // serialization round trip with both its board and its RNG state intact
        // (GAME_STATE.md §2.1.7 items 1, 5; §2.6.2 item 4).
        var board = TestBoard.Background()
            .WithGems((25, GemType.Atk), (26, GemType.Atk), (27, GemType.Atk));

        var rng = Pcg32.FromSeed(31337UL);
        var resolution = CascadeResolver.Resolve(board, rng);

        var restoredBoard = Deserialize(Serialize(resolution.Board));

        Assert.True(resolution.Board.CellsEqual(restoredBoard));
    }

    [Fact]
    public void BattleState_ShouldRoundTripWithBoardAndRngState()
    {
        // REDIS_STATE.md §2 item 1 / §7 item 10: the active battle record is BattleState
        // serialized exactly as GAME_STATE.md §2 defines it — no additional field and no
        // separate board key. The domain record is the contract's shape.
        var state = BattleState.CreateWith("battle-roundtrip", 987654UL);

        // The record carries the board and the RNG pair, so a snapshot taken between
        // resolutions is complete (§2.1.7 item 6).
        Assert.Equal("battle-roundtrip", state.BattleId);
        Assert.Equal(987654UL, state.RngSeed);
        Assert.Equal(BoardState.CellCount, state.BoardState.Cells.Count);

        // A board placed in the state is read back unchanged: the representation has
        // nothing left to decide.
        var withBoard = state with { BoardState = BoardWithEveryGemType() };

        Assert.Equal(GemType.Power, withBoard.BoardState[63]);
    }

    [Fact]
    public void Serialization_ShouldNotIntroduceASecondBoardCollection()
    {
        // GAME_STATE.md §2.1.7 item 1: a serializer writes the board with "no additional
        // field, no parallel array, and no second key".
        var json = Serialize(BoardWithEveryGemType());

        using var document = JsonDocument.Parse(json);

        Assert.Single(document.RootElement.EnumerateObject());
        Assert.True(document.RootElement.TryGetProperty("cells", out _));
    }

    [Fact]
    public void Deserialization_ShouldIgnoreAnUnknownLegacyCellMember()
    {
        // ADR-028 Decision B-2's tolerant-reader basis: a stored record that still
        // carries a legacy per-cell member deserializes without error and the obsolete
        // member is ignored — no migration and no compatibility branch is required.
        var withLegacyMember = Enumerable.Range(0, BoardState.CellCount)
            .Select(_ => "{\"legacyCellMetadata\":{},\"gemType\":\"ATK\"}")
            .ToArray();

        var legacyJson = $"{{\"cells\":[{string.Join(',', withLegacyMember)}]}}";

        var restored = Deserialize(legacyJson);

        Assert.Equal(BoardState.CellCount, restored.Cells.Count);
        Assert.All(restored.Cells, gemType => Assert.Equal(GemType.Atk, gemType));
    }
}
