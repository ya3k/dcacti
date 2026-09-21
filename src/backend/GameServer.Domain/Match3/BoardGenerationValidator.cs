namespace GameServer.Domain.Match3;

/// <summary>
/// The Board Generation Validator (<c>MATCH3_RULES.md</c> §1.4.1).
///
/// It exists <b>only</b> to enforce the two initial-board constraints during
/// initial board generation:
///
/// <code>
/// §1.3  no pre-existing Match
/// §1.4  at least one valid Swap
/// </code>
///
/// <c>MATCH3_RULES.md</c> §1.4.1 bounds it to exactly that purpose. It may
/// evaluate a candidate board for a §3 Match shape and evaluate whether an
/// adjacent pair swap would produce a §3 Match. It may <b>not</b>:
///
/// <code>
/// emit Battle Events (GAME_EVENTS.md §2)
/// calculate or modify Combo
/// modify BattleState after initialization
/// resolve player actions
/// perform cascades, gravity, or spawning (§4)
/// create Special Gems (§5)
/// generate resources, deal damage, or affect combat
/// </code>
///
/// It runs once, at initialization, and is not reachable during gameplay
/// (§1.4.1). It is stateless and pure: it reads a candidate board and answers
/// valid/invalid, mutating nothing.
///
/// <b>Distinct from gameplay Match Detection and Swap Validation</b>
/// (§1.4.2). It is not the Gameplay Match Detector (it produces no match
/// results, tiers, shapes, or events) and not the Gameplay Swap Validator (it
/// never processes a player Swap request, commits or reverts a Swap, or starts a
/// Turn). Its answer is used only to accept or reject a candidate board during
/// generation (§1.4.2 item 3).
///
/// Per §1.4.3 this is the minimum evaluation needed to satisfy §1.4. It is not
/// required to be the gameplay algorithm, not required to enumerate all matches,
/// and deliberately not reusable by the future gameplay resolver — the resolver
/// implements §2–§5 in full on its own terms when its stage arrives.
/// </summary>
public static class BoardGenerationValidator
{
    /// <summary>
    /// Minimum run length that constitutes a Match (<c>MATCH3_RULES.md</c> §3
    /// item 2: "Minimum match length is 3").
    /// </summary>
    public const int MinimumMatchLength = 3;

    /// <summary>
    /// True when the candidate board already contains a Match — that is, any
    /// unbroken horizontal or vertical line of 3+ Gems of the same type
    /// (<c>MATCH3_RULES.md</c> §1.3, applying the §3 shape).
    ///
    /// This is the §1.3 constraint, "Initial Board Validity — No Pre-existing
    /// Match". A board for which this returns <c>true</c> must be rejected and
    /// regenerated (§1.5).
    /// </summary>
    public static bool HasMatch(BoardState board)
    {
        ArgumentNullException.ThrowIfNull(board);

        // Horizontal runs, left to right.
        for (var row = 0; row < BoardState.Rows; row++)
        {
            var runLength = 1;

            for (var column = 1; column < BoardState.Columns; column++)
            {
                var index = BoardState.ToIndex(row, column);

                if (board[index] == board[index - 1])
                {
                    runLength++;
                    if (runLength >= MinimumMatchLength)
                    {
                        return true;
                    }
                }
                else
                {
                    runLength = 1;
                }
            }
        }

        // Vertical runs, top to bottom.
        for (var column = 0; column < BoardState.Columns; column++)
        {
            var runLength = 1;

            for (var row = 1; row < BoardState.Rows; row++)
            {
                var index = BoardState.ToIndex(row, column);

                if (board[index] == board[index - BoardState.Width])
                {
                    runLength++;
                    if (runLength >= MinimumMatchLength)
                    {
                        return true;
                    }
                }
                else
                {
                    runLength = 1;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// True when the candidate board contains at least one valid Swap — an
    /// orthogonally adjacent pair whose exchange yields a board containing a
    /// Match (<c>MATCH3_RULES.md</c> §1.4, applying §2 and §3).
    ///
    /// This is the §1.4 constraint, "Initial Board Validity — At Least One Valid
    /// Swap". "At least one" is the whole requirement: the scan returns as soon
    /// as one qualifying pair exists. No minimum count, no distribution
    /// requirement, and no quality measure beyond this is defined (§1.4).
    ///
    /// The evaluation is generation-time only. It simulates an exchange on an
    /// in-memory copy to answer a yes/no question and commits nothing — it is not
    /// the future player swap system (§1.4.2 item 2).
    /// </summary>
    public static bool HasValidSwap(BoardState board)
    {
        ArgumentNullException.ThrowIfNull(board);

        for (var row = 0; row < BoardState.Rows; row++)
        {
            for (var column = 0; column < BoardState.Columns; column++)
            {
                var index = BoardState.ToIndex(row, column);

                // Right neighbour.
                if (column + 1 < BoardState.Columns
                    && SwapProducesMatch(board, index, index + 1))
                {
                    return true;
                }

                // Down neighbour.
                if (row + 1 < BoardState.Rows
                    && SwapProducesMatch(board, index, index + BoardState.Width))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// True when the candidate board satisfies both documented initial-board
    /// constraints — no pre-existing Match (§1.3) and at least one valid Swap
    /// (§1.4).
    ///
    /// This is the single answer the generation loop consults (§1.2.1 step 3).
    /// </summary>
    public static bool IsValidInitialBoard(BoardState board)
    {
        ArgumentNullException.ThrowIfNull(board);

        // §1.3 is checked first: a board with a pre-existing Match is rejected
        // regardless of how many valid swaps it contains.
        return !HasMatch(board) && HasValidSwap(board);
    }

    /// <summary>
    /// Evaluates whether exchanging the two given cells would produce a Match
    /// (<c>MATCH3_RULES.md</c> §1.4, §2 item 3).
    ///
    /// The exchanged pair must be orthogonally adjacent, because only such a pair
    /// is a Swap (§2 item 1: "Diagonal swaps are invalid").
    ///
    /// The check is performed only around the two exchanged cells: an exchange
    /// can create a Match only in a line that contains one of them. That is a
    /// sufficient and necessary condition for this generation-time question and
    /// does not require enumerating the whole board — §1.4.3 explicitly permits
    /// the minimum evaluation.
    /// </summary>
    private static bool SwapProducesMatch(BoardState board, int firstIndex, int secondIndex)
    {
        if (!BoardState.AreOrthogonallyAdjacent(firstIndex, secondIndex))
        {
            return false;
        }

        var exchanged = board.WithSwapped(firstIndex, secondIndex);

        return FormsMatchAt(exchanged, firstIndex) || FormsMatchAt(exchanged, secondIndex);
    }

    /// <summary>
    /// True when the Gem at <paramref name="index"/> is part of a run of 3+ of
    /// its own type, horizontally or vertically (<c>MATCH3_RULES.md</c> §3
    /// item 1).
    /// </summary>
    private static bool FormsMatchAt(BoardState board, int index)
    {
        var type = board[index];

        return CountRun(board, index, rowStep: 0, columnStep: 1, type)
                   + CountRun(board, index, rowStep: 0, columnStep: -1, type)
                   + 1
               >= MinimumMatchLength
            || CountRun(board, index, rowStep: 1, columnStep: 0, type)
                   + CountRun(board, index, rowStep: -1, columnStep: 0, type)
                   + 1
               >= MinimumMatchLength;
    }

    /// <summary>
    /// Counts consecutive cells of <paramref name="type"/> starting one step away
    /// from <paramref name="index"/> in the given direction, stopping at the
    /// board edge or at the first different Gem.
    /// </summary>
    private static int CountRun(BoardState board, int index, int rowStep, int columnStep, GemType type)
    {
        var row = BoardState.ToRow(index) + rowStep;
        var column = BoardState.ToColumn(index) + columnStep;
        var count = 0;

        while (row >= 0 && row < BoardState.Rows && column >= 0 && column < BoardState.Columns)
        {
            if (board[BoardState.ToIndex(row, column)] != type)
            {
                break;
            }

            count++;
            row += rowStep;
            column += columnStep;
        }

        return count;
    }
}