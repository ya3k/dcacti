using GameServer.Domain.Match3;

namespace GameServer.Domain.Tests;

/// <summary>
/// Test-only board builders for Special Gem resolution scenarios.
///
/// Boards are written as 8 rows of 8 characters so a scenario reads as the board
/// the documentation describes. The letter maps to a Gem type; the board's
/// indexing is the documented row-major one (<c>MATCH3_RULES.md</c> §1.0), so the
/// character at row <c>r</c>, column <c>c</c> is cell index <c>r * 8 + c</c>.
///
/// These helpers construct state, they do not assert behavior: no gameplay rule is
/// encoded here beyond the documented indexing.
/// </summary>
internal static class TestBoard
{
    /// <summary>
    /// Builds a board from 8 rows of 8 characters. <c>A</c>/<c>D</c>/<c>H</c>/<c>P</c>
    /// map to ATK / DEF / HP / POWER (<c>MATCH3_RULES.md</c> §1.1).
    /// </summary>
    internal static BoardState FromRows(params string[] rows)
    {
        if (rows.Length != BoardState.Rows)
        {
            throw new ArgumentException(
                $"A board has {BoardState.Rows} rows (MATCH3_RULES.md §1.0); received {rows.Length}.",
                nameof(rows));
        }

        var cells = new GemType[BoardState.CellCount];

        for (var row = 0; row < BoardState.Rows; row++)
        {
            var line = rows[row];
            if (line.Length != BoardState.Columns)
            {
                throw new ArgumentException(
                    $"Row {row} has {line.Length} columns; a board row has {BoardState.Columns} "
                    + "(MATCH3_RULES.md §1.0).",
                    nameof(rows));
            }

            for (var column = 0; column < BoardState.Columns; column++)
            {
                cells[BoardState.ToIndex(row, column)] = ToGem(line[column]);
            }
        }

        return BoardState.FromCells(cells);
    }

    /// <summary>
    /// Builds a board whose every cell is the same type unless overridden. Useful
    /// for isolating one region of a scenario.
    /// </summary>
    internal static BoardState Uniform(GemType background, params (int Index, GemType Type)[] overrides)
    {
        var cells = Enumerable.Repeat(background, BoardState.CellCount).ToArray();

        foreach (var (index, type) in overrides)
        {
            cells[index] = type;
        }

        return BoardState.FromCells(cells);
    }

    /// <summary>Cell index shorthand: <c>I(row, column)</c> is <c>row * 8 + column</c>.</summary>
    internal static int I(int row, int column) => BoardState.ToIndex(row, column);

    /// <summary>
    /// A match-free background board built so that any single row/column segment a
    /// fixture overwrites cannot merge with a neighbour of the same type:
    ///
    /// <list type="bullet">
    /// <item>horizontally every row alternates <c>A D H P</c> twice, so no two
    /// adjacent cells are equal and no horizontal run exists;</item>
    /// <item>vertically each column advances by one rotation per row, so a type
    /// repeats only at distance 4 — never at distance 1 or 2, which is what a
    /// vertical run of 3 would require.</item>
    /// </list>
    ///
    /// Verified match-free by the detector itself in <c>TestBoardTests</c>.
    /// </summary>
    internal static BoardState Background() =>
        FromRows(
            "ADHPADHP",
            "DHPADHPA",
            "HPADHPAD",
            "PADHPADH",
            "ADHPADHP",
            "DHPADHPA",
            "HPADHPAD",
            "PADHPADH");

    /// <summary>
    /// A board whose every cell is <paramref name="background"/>, for fixtures that
    /// must not inherit any pre-existing structure at all. Callers declare every
    /// cell of the run they intend to study.
    /// </summary>
    internal static BoardState Flat(GemType background) => Uniform(background);

    /// <summary>
    /// The checkerboard used by the wholly match-free scenarios. Each row is a
    /// rotation of <c>ADHP</c>, so the board holds no 3-run in any line.
    /// </summary>
    internal static BoardState Checkerboard() => Background();

    /// <summary>Replaces cells on an existing board, keeping cell entries otherwise.</summary>
    internal static BoardState WithGems(this BoardState board, params (int Index, GemType Type)[] changes)
    {
        var entries = board.ToCellArray();

        foreach (var (index, type) in changes)
        {
            entries[index] = new Cell(type, entries[index].SpecialGem);
        }

        return BoardState.FromCellEntries(entries);
    }

    /// <summary>Places Special Gems on an existing board at the given cells.</summary>
    internal static BoardState WithSpecial(this BoardState board, params (int Index, SpecialGem Gem)[] gems)
    {
        var entries = board.ToCellArray();

        foreach (var (index, gem) in gems)
        {
            entries[index] = new Cell(entries[index].GemType, gem);
        }

        return BoardState.FromCellEntries(entries);
    }

    private static GemType ToGem(char c) => char.ToUpperInvariant(c) switch
    {
        'A' => GemType.Atk,
        'D' => GemType.Def,
        'H' => GemType.Hp,
        'P' => GemType.Power,
        _ => throw new ArgumentException($"'{c}' is not one of the documented Gem letters A/D/H/P.", nameof(c)),
    };
}