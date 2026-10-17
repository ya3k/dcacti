namespace GameServer.Domain.Match3;

/// <summary>
/// The authoritative Match-3 board (<c>GAME_STATE.md</c> §2.1).
///
/// <code>
/// BoardState
/// └── Cells[64]           exactly 64 cell entries, each an ordinary GemType
/// </code>
///
/// <c>Cells[64]</c> is the whole of it: the single ordered, index-addressed
/// collection that holds every cell's occupant (<c>GAME_STATE.md</c> §2.1.1).
/// There is <b>no second field</b> and no second collection of board contents, and
/// no field of this type duplicates a cell (§2.1.2 item 1). Each entry is solely
/// the cell's functional <see cref="GemType"/> — Special Gems are retired from the
/// authoritative model (<c>MATCH3_RULES.md</c> §1.A, <c>ADR-028</c>), so no cell
/// entry can express metadata alongside its Gem type.
///
/// Board size, layout, and indexing are owned by <c>MATCH3_RULES.md</c> §1.0 and
/// are implemented here rather than restated:
///
/// <code>
/// Size:      8 x 8 (64 cells)
/// Indexing:  row-major, 0-63, (0,0) = top-left
/// index  = row * 8 + column
/// row    = floor(index / 8)
/// column = index % 8
/// </code>
///
/// This is the board's only coordinate convention. No second convention exists,
/// and this type introduces none: a conversion to screen coordinates is a
/// presentation concern (<c>ARCHITECTURE.md</c> §2.2.2).
///
/// The board is server-authored (<c>GAME_STATE.md</c> §2.1.1 item 4,
/// <c>GAME_RULES.md</c> §18, <c>ADR-001</c>). The client never generates, fills,
/// or repairs it. The client renders what it receives; it never matches or
/// resolves a Gem (<c>GAME_STATE.md</c> §2.1.9 item 4).
///
/// The board is immutable: every mutation of the documented resolution —
/// removal, gravity, spawn — is expressed as a new <see cref="BoardState"/>
/// derived from the previous one, so no caller can observe a partially mutated
/// board (<c>MATCH3_RULES.md</c> §3.3 item 4, §4.1).
/// </summary>
public sealed class BoardState
{
    /// <summary>Board width in cells — 8 (<c>MATCH3_RULES.md</c> §1.0).</summary>
    public const int Width = 8;

    /// <summary>Board height in cells — 8 (<c>MATCH3_RULES.md</c> §1.0).</summary>
    public const int Height = 8;

    /// <summary>
    /// Total cell count — exactly 64 (<c>MATCH3_RULES.md</c> §1.0,
    /// <c>GAME_STATE.md</c> §2.1.1 item 1).
    /// </summary>
    public const int CellCount = Width * Height;

    /// <summary>Number of columns — alias of <see cref="Width"/>.</summary>
    public const int Columns = Width;

    /// <summary>Number of rows — alias of <see cref="Height"/>.</summary>
    public const int Rows = Height;

    private readonly GemType[] _cells;

    private BoardState(GemType[] cells)
    {
        _cells = cells;
    }

    /// <summary>
    /// The board's cell entries, row-major, exactly 64
    /// (<c>GAME_STATE.md</c> §2.1.1 item 1). Index <c>i</c> is
    /// <c>row = i / 8</c>, <c>column = i % 8</c> (<c>MATCH3_RULES.md</c> §1.0).
    ///
    /// Each entry is the cell's Gem type and nothing else (§2.1.2 item 1,
    /// §2.1.3 item 1). The array position <b>is</b> the cell index (§2.1.7 item 2):
    /// no cell carries an index of its own, because the position already states it.
    /// </summary>
    public IReadOnlyList<GemType> Cells => _cells;

    /// <summary>
    /// The Gem type at a cell index — the value Match Detection compares and
    /// <c>GemMatched</c> reports (<c>GAME_STATE.md</c> §2.1.3 item 1).
    /// </summary>
    public GemType this[int index]
    {
        get
        {
            ValidateIndex(index);
            return _cells[index];
        }
    }

    /// <summary>
    /// Builds a board from exactly 64 Gem values in row-major order
    /// (<c>GAME_STATE.md</c> §2.1.1).
    ///
    /// The length and value checks enforce the documented domain contract: a
    /// board always has 64 cells (<c>MATCH3_RULES.md</c> §1.0) and every cell
    /// holds one of the four Gem types (§1.1).
    /// </summary>
    public static BoardState FromCells(IEnumerable<GemType> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);

        var array = cells as GemType[] ?? cells.ToArray();

        if (array.Length != CellCount)
        {
            throw new ArgumentException(
                $"A board holds exactly {CellCount} cells (MATCH3_RULES.md §1.0); received {array.Length}.",
                nameof(cells));
        }

        for (var i = 0; i < array.Length; i++)
        {
            if (!Enum.IsDefined(array[i]))
            {
                throw new ArgumentException(
                    $"Cell {i} holds '{array[i]}', which is not one of the four documented Gem types "
                    + "(MATCH3_RULES.md §1.1).",
                    nameof(cells));
            }
        }

        return new BoardState((GemType[])array.Clone());
    }

    /// <summary>
    /// Builds a board from raw cell values, rejecting any value outside the four
    /// documented Gem types (<c>MATCH3_RULES.md</c> §1.1).
    /// </summary>
    public static BoardState FromValues(IEnumerable<int> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);

        var values = cells as int[] ?? cells.ToArray();

        if (values.Length != CellCount)
        {
            throw new ArgumentException(
                $"A board holds exactly {CellCount} cells (MATCH3_RULES.md §1.0); received {values.Length}.",
                nameof(cells));
        }

        var entries = new GemType[CellCount];

        for (var i = 0; i < values.Length; i++)
        {
            if (!GemTypes.IsValid(values[i]))
            {
                throw new ArgumentException(
                    $"Cell {i} holds '{values[i]}', which is not one of the four documented Gem types "
                    + "(MATCH3_RULES.md §1.1).",
                    nameof(cells));
            }

            entries[i] = (GemType)values[i];
        }

        return new BoardState(entries);
    }

    /// <summary>
    /// The documented row-major index of a cell (<c>MATCH3_RULES.md</c> §1.0):
    /// <c>index = row * 8 + column</c>.
    /// </summary>
    public static int ToIndex(int row, int column)
    {
        if (row is < 0 or >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(row), row, "Row must be within 0..7.");
        }

        if (column is < 0 or >= Width)
        {
            throw new ArgumentOutOfRangeException(nameof(column), column, "Column must be within 0..7.");
        }

        return (row * Width) + column;
    }

    /// <summary>
    /// The documented row of a cell index (<c>MATCH3_RULES.md</c> §1.0):
    /// <c>row = floor(index / 8)</c>.
    /// </summary>
    public static int ToRow(int index)
    {
        ValidateIndex(index);
        return index / Width;
    }

    /// <summary>
    /// The documented column of a cell index (<c>MATCH3_RULES.md</c> §1.0):
    /// <c>column = index % 8</c>.
    /// </summary>
    public static int ToColumn(int index)
    {
        ValidateIndex(index);
        return index % Width;
    }

    /// <summary>
    /// True when <c>(row, column)</c> is a cell that exists on the board
    /// (<c>MATCH3_RULES.md</c> §1.0).
    /// </summary>
    public static bool IsOnBoard(int row, int column) =>
        row >= 0 && row < Height && column >= 0 && column < Width;

    /// <summary>
    /// True when two cell indexes are orthogonally adjacent — up/down/left/right
    /// (<c>MATCH3_RULES.md</c> §2 item 1). Diagonal pairs are not adjacent.
    /// </summary>
    public static bool AreOrthogonallyAdjacent(int firstIndex, int secondIndex)
    {
        ValidateIndex(firstIndex);
        ValidateIndex(secondIndex);

        var rowDelta = Math.Abs(ToRow(firstIndex) - ToRow(secondIndex));
        var columnDelta = Math.Abs(ToColumn(firstIndex) - ToColumn(secondIndex));

        return rowDelta + columnDelta == 1;
    }

    /// <summary>
    /// Returns a new board with the two given cells exchanged, leaving this
    /// board unmodified.
    ///
    /// This is the "simulate the swap on a copy of the board" step of
    /// <c>MATCH3_RULES.md</c> §2 item 2. It performs no validation and commits
    /// nothing — it is a pure derivation used by callers that need a candidate
    /// arrangement. Each cell's <see cref="GemType"/> is exchanged as one unit
    /// (<c>GAME_STATE.md</c> §2.1.5 item 2).
    /// </summary>
    public BoardState WithSwapped(int firstIndex, int secondIndex)
    {
        ValidateIndex(firstIndex);
        ValidateIndex(secondIndex);

        var copy = (GemType[])_cells.Clone();
        (copy[firstIndex], copy[secondIndex]) = (copy[secondIndex], copy[firstIndex]);

        return new BoardState(copy);
    }

    /// <summary>An independent copy of the board's Gem types (row-major order).</summary>
    public GemType[] ToArray() => (GemType[])_cells.Clone();

    /// <summary>
    /// True when this board's 64 entries are identical to another's — same Gem
    /// types, in the same index order (<c>GAME_STATE.md</c> §2.1.7 item 5).
    /// </summary>
    public bool CellsEqual(BoardState other)
    {
        ArgumentNullException.ThrowIfNull(other);

        for (var i = 0; i < CellCount; i++)
        {
            if (_cells[i] != other._cells[i])
            {
                return false;
            }
        }

        return true;
    }

    private static void ValidateIndex(int index)
    {
        if (index is < 0 or >= CellCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                index,
                $"Cell index must be within 0..{CellCount - 1} (MATCH3_RULES.md §1.0).");
        }
    }
}
