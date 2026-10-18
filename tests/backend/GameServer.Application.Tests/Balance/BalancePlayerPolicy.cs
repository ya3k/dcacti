using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Match3;

namespace GameServer.Application.Tests.Balance;

/// <summary>
/// A scripted player policy — the <b>only</b> source of player choice in a
/// simulation run (<c>TASK-193</c> §3.2).
///
/// <b>A policy chooses actions; it calculates no outcome.</b> It proposes a Swap
/// or a Card cast and the production pipeline decides what happens. A policy that
/// computed damage, a match result, or a resource value would invalidate the
/// measurement, so nothing here reads a combat value to decide an action beyond
/// the state the rules make public to the player.
///
/// <b>A policy is a measurement instrument, not a balance opinion.</b> None
/// encodes "the intended way to play", and none is an acceptance criterion.
/// </summary>
internal static class BalancePolicies
{
    /// <summary>The policy names a result records (<c>H-09</c>).</summary>
    public const string PassiveName = "passive";
    public const string AverageName = "average";
    public const string SkilledName = "skilled";
    public const string LegacySkilledName = "skilled-v1";

    /// <summary>
    /// Resolves a policy by its documented name — the approved values, and
    /// historical benchmark qualifiers.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The name is not one of the approved policies.</exception>
    public static BalancePlayerPolicy ByName(string name) => name switch
    {
        PassiveName => Passive,
        AverageName => Average,
        SkilledName => Skilled,
        LegacySkilledName or "legacy-skilled" => LegacySkilled,
        _ => throw new ArgumentOutOfRangeException(
            nameof(name),
            name,
            $"TASK-193 §3.2 defines policies: {PassiveName}, {AverageName}, {SkilledName} (and historical benchmark {LegacySkilledName})."),
    };

    /// <summary>
    /// <b>Passive</b> — conservative/basic play: commits the first legal Swap
    /// found in ascending cell-pair order, and never casts a Card.
    ///
    /// The scan order is the documented tie-break (§3.2): lowest legal cell-pair
    /// index first. It is a fixed rule, not a random draw, so the run is
    /// reproducible without consulting any RNG.
    /// </summary>
    public static readonly BalancePlayerPolicy Passive = new(
        PassiveName,
        ProposeSwap: static (_, _, attempted) => FirstUntriedSwapInCellOrder(attempted),
        ProposeCast: static (_, _, _) => null);

    /// <summary>
    /// <b>Average</b> — ordinary play: commits the first legal Swap found, and
    /// casts the first affordable Card in loadout order.
    ///
    /// Affordability is judged from <c>PetState.Power</c> against the Card's
    /// authored <c>PowerCost</c> — the same two inputs the production validator
    /// reads (<c>CARD_RULES.md</c> §3 item 2). The policy proposes; the validator
    /// decides, and a rejection is recorded rather than retried.
    /// </summary>
    public static readonly BalancePlayerPolicy Average = new(
        AverageName,
        ProposeSwap: static (_, _, attempted) => FirstUntriedSwapInCellOrder(attempted),
        ProposeCast: static (state, context, _) => FirstAffordableCard(state, context));

    /// <summary>
    /// <b>Legacy Skilled (Benchmark v1)</b> — pre-correction heuristic that assumed
    /// the retired match-tier multiplier (Match 5 > Match 4 > Match 3). Preserved
    /// strictly for historical comparison against the TASK-296 candidate baseline.
    /// </summary>
    public static readonly BalancePlayerPolicy LegacySkilled = new(
        SkilledName,
        ProposeSwap: static (state, _, attempted) => HighestMatchLengthSwap(state, attempted),
        ProposeCast: static (state, context, _) => MostExpensiveAffordableCard(state, context));

    /// <summary>
    /// <b>Skilled (Benchmark v2)</b> — competent play under match-only rules
    /// (OD-3, OD-4, OD-7, ADR-028): prefers the Swap that completes the greatest
    /// count of unique confirmed matched gems across all valid immediate matches,
    /// and casts the most valuable affordable Card, including the Pet Signature Skill.
    ///
    /// The Swap preference scores each legal cell-pair by its <b>unique confirmed matched gem count</b>
    /// across all valid matches (length &gt;= 3) formed by the exchange. This aligns with
    /// <c>COMBAT_RULES.md</c> §2 and OD-3/OD-7, where match-tier multipliers and Special Gems
    /// are retired, making resource output strictly linear with confirmed matched gem count.
    /// Ties are broken deterministically by lowest cell-pair index.
    /// </summary>
    public static readonly BalancePlayerPolicy Skilled = new(
        SkilledName,
        ProposeSwap: static (state, _, attempted) => HighestMatchedCellCountSwap(state, attempted),
        ProposeCast: static (state, context, _) => MostExpensiveAffordableCard(state, context));

    /// <summary>
    /// Evaluates candidate adjacent swaps and scores each by the count of
    /// <b>unique confirmed matched gems</b> across all immediate first-pass matches
    /// the exchange forms (<c>MATCH3_RULES.md</c> §3, <c>COMBAT_RULES.md</c> §2,
    /// OD-3, OD-4, OD-7, ADR-028).
    ///
    /// <b>Count-only, with no match-tier multiplier.</b> The retired match-tier
    /// multipliers (OD-3) and Special Gems (OD-1, OD-2) no longer grant disproportionate
    /// weight to linear run length alone. A double-3 match (6 unique gems) yields more
    /// resources than a single-4 match (4 unique gems) under match-only rules, so the
    /// heuristic prefers the swap that clears the greatest total unique cells.
    /// Ties are broken by lowest cell-pair index.
    /// </summary>
    internal static SwapRequest? HighestMatchedCellCountSwap(
        BattleState state,
        IReadOnlySet<int> attempted)
    {
        SwapRequest? best = null;
        var bestScore = 0; // Valid matches must have score >= 3; non-matches score 0.

        var board = state.BoardState;

        for (var from = 0; from < BoardState.CellCount; from++)
        {
            for (var to = from + 1; to < BoardState.CellCount; to++)
            {
                if (!IsAdjacent(from, to)
                    || attempted.Contains(BalancePlayerPolicy.PairKey(from, to)))
                {
                    continue;
                }

                var score = MatchedCellCountAfterExchanging(board, from, to);

                // Strictly greater keeps the FIRST pair on a tie, which is the
                // documented lowest-index tie-break.
                if (score > bestScore)
                {
                    bestScore = score;
                    best = new SwapRequest(from, to);
                }
            }
        }

        // If no legal match exists or all have been attempted, fallback to first untried adjacent swap
        return best ?? FirstUntriedSwapInCellOrder(attempted);
    }

    /// <summary>
    /// Calculates the number of unique cells in all valid matches (length &gt;= 3)
    /// formed on the board if <paramref name="from"/> and <paramref name="to"/> are swapped.
    /// </summary>
    internal static int MatchedCellCountAfterExchanging(BoardState board, int from, int to)
    {
        var exchanged = board.WithSwapped(from, to);
        var matches = MatchDetector.Detect(exchanged);
        if (matches.Count == 0)
        {
            return 0;
        }

        var uniqueCells = new HashSet<int>();
        for (var i = 0; i < matches.Count; i++)
        {
            var matchCells = matches[i].Cells;
            for (var j = 0; j < matchCells.Count; j++)
            {
                uniqueCells.Add(matchCells[j]);
            }
        }

        return uniqueCells.Count;
    }

    /// <summary>
    /// The first untried cell pair in ascending <c>(from, to)</c> index order that
    /// is adjacent. Adjacency is the one structural precondition
    /// <c>MATCH3_RULES.md</c> §2.1.2 makes the policy responsible for; whether the
    /// swap <i>matches</i> is the validator's decision, so a pair that does not
    /// match is proposed and legitimately rejected — and the driver's cursor then
    /// advances past it.
    /// </summary>
    private static SwapRequest? FirstUntriedSwapInCellOrder(IReadOnlySet<int> attempted)
    {
        for (var from = 0; from < BoardState.CellCount; from++)
        {
            for (var to = from + 1; to < BoardState.CellCount; to++)
            {
                if (!IsAdjacent(from, to) || attempted.Contains(BalancePlayerPolicy.PairKey(from, to)))
                {
                    continue;
                }

                return new SwapRequest(from, to);
            }
        }

        return null;
    }

    /// <summary>
    /// Adjacency for the documented row-major 8×8 board
    /// (<c>MATCH3_RULES.md</c> §1.0): two cells are adjacent when they share a row
    /// and differ by one column, or share a column and differ by one row.
    /// </summary>
    internal static bool IsAdjacent(int from, int to)
    {
        var fromRow = from / BoardState.Columns;
        var fromColumn = from % BoardState.Columns;
        var toRow = to / BoardState.Columns;
        var toColumn = to % BoardState.Columns;

        var sameRow = fromRow == toRow && Math.Abs(fromColumn - toColumn) == 1;
        var sameColumn = fromColumn == toColumn && Math.Abs(fromRow - toRow) == 1;

        return sameRow || sameColumn;
    }

    /// <summary>
    /// The adjacent, not-yet-attempted swap that completes the longest run, ties
    /// broken by lowest cell-pair index.
    ///
    /// It scores by <b>Gem-type run length</b> using only the board the state
    /// publishes — the same information a player sees. It does not simulate the
    /// swap, detect matches the way the validator does, or predict resources.
    /// </summary>
    private static SwapRequest? HighestMatchLengthSwap(
        BattleState state,
        IReadOnlySet<int> attempted)
    {
        SwapRequest? best = null;
        var bestScore = -1;

        for (var from = 0; from < BoardState.CellCount; from++)
        {
            for (var to = from + 1; to < BoardState.CellCount; to++)
            {
                if (!IsAdjacent(from, to)
                    || attempted.Contains(BalancePlayerPolicy.PairKey(from, to)))
                {
                    continue;
                }

                var score = LongestRunAfterExchanging(state, from, to);

                // Strictly greater keeps the FIRST pair on a tie, which is the
                // documented lowest-index tie-break.
                if (score > bestScore)
                {
                    bestScore = score;
                    best = new SwapRequest(from, to);
                }
            }
        }

        return best;
    }

    /// <summary>
    /// The longest same-type run that would exist through either exchanged cell
    /// if the two cells traded places. It is an upper-bound <i>heuristic</i> for
    /// swap value — not match detection — used only to rank candidate swaps.
    /// </summary>
    private static int LongestRunAfterExchanging(BattleState state, int from, int to)
    {
        var cells = state.BoardState.Cells;
        var fromGem = cells[from];
        var toGem = cells[to];

        var best = 0;

        // The run through `from` if it held `toGem`, and through `to` if it held
        // `fromGem` — the two lines the exchange would create.
        best = Math.Max(best, RunThrough(state, from, toGem, fromGem));
        best = Math.Max(best, RunThrough(state, to, fromGem, toGem));

        return best;
    }

    /// <summary>
    /// The length of the longest horizontal/vertical run of the same Gem type
    /// that passes through <paramref name="index"/> when that cell is treated as
    /// holding <paramref name="gem"/>, ignoring the cell at <paramref name="other"/>
    /// (the cell being exchanged away).
    /// </summary>
    private static int RunThrough(BattleState state, int index, GemType gem, GemType otherGem)
    {
        _ = otherGem;

        var cells = state.BoardState.Cells;
        var row = index / BoardState.Columns;
        var column = index % BoardState.Columns;

        var horizontal = 1
            + CountDirection(cells, row, column, 0, -1, gem, index)
            + CountDirection(cells, row, column, 0, 1, gem, index);
        var vertical = 1
            + CountDirection(cells, row, column, -1, 0, gem, index)
            + CountDirection(cells, row, column, 1, 0, gem, index);

        return Math.Max(horizontal, vertical);
    }

    /// <summary>
    /// Counts consecutive cells of <paramref name="gem"/> from
    /// <c>(row, column)</c> stepping by <c>(dRow, dColumn)</c>.
    /// </summary>
    private static int CountDirection(
        IReadOnlyList<GemType> cells,
        int row,
        int column,
        int dRow,
        int dColumn,
        GemType gem,
        int origin)
    {
        var count = 0;
        var r = row + dRow;
        var c = column + dColumn;

        while (r >= 0 && r < BoardState.Rows && c >= 0 && c < BoardState.Columns)
        {
            var index = (r * BoardState.Columns) + c;

            // The cell being exchanged away is treated as unknown, so it neither
            // extends nor breaks the run being measured.
            if (index != origin && cells[index] != gem)
            {
                break;
            }

            count++;
            r += dRow;
            c += dColumn;
        }

        return count;
    }

    /// <summary>
    /// The first Card in loadout order whose authored <c>PowerCost</c> the active
    /// Pet can currently afford. It returns the Card's <b>definition identity</b>
    /// and lets the production cast path validate it.
    /// </summary>
    private static string? FirstAffordableCard(
        BattleState state,
        BalancePolicyContext context)
    {
        foreach (var cardId in context.EquippedCardIds)
        {
            if (context.PowerCostOf(cardId) <= state.PetState.Power)
            {
                return cardId;
            }
        }

        return null;
    }

    /// <summary>
    /// The affordable Card with the highest authored <c>PowerCost</c>, ties broken
    /// by loadout order. Spending the resource on the strongest defined effect is
    /// the heuristic; the policy still never computes the effect's result.
    /// </summary>
    private static string? MostExpensiveAffordableCard(
        BattleState state,
        BalancePolicyContext context)
    {
        string? best = null;
        var bestCost = -1;

        foreach (var cardId in context.EquippedCardIds)
        {
            var cost = context.PowerCostOf(cardId);

            if (cost > state.PetState.Power)
            {
                continue;
            }

            // Strictly greater keeps the first on a tie — the documented
            // loadout-order tie-break.
            if (cost > bestCost)
            {
                bestCost = cost;
                best = cardId;
            }
        }

        return best;
    }
}

/// <summary>
/// A scripted player policy (<c>TASK-193</c> §3.2).
///
/// <b>Two delegates, one rule.</b> <see cref="ProposeSwap"/> and
/// <see cref="ProposeCast"/> are the only decisions a policy makes. Each is a
/// pure function of the authoritative state, the policy context, and a cursor
/// the driver advances — so a proposal sequence is fully determined by the run's
/// inputs and the simulation stays reproducible.
///
/// <b>Why a cursor is needed.</b> The production validator decides whether a
/// proposed Swap commits (<c>MATCH3_RULES.md</c> §2.1.2). A rejected Swap
/// begins no Turn and changes no state, so a policy that always returned the
/// same first pair would re-propose it forever. The cursor is how a policy
/// <i>enumerates candidates</i> rather than repeating one — it is a search
/// position, not a gameplay value, and it is reset whenever a Turn actually
/// commits.
/// </summary>
/// <param name="Name">The policy name a result records.</param>
/// <param name="ProposeSwap">
/// Proposes the Swap to attempt, given the candidates already attempted this
/// Turn, or <c>null</c> when the policy has no further proposal.
/// </param>
/// <param name="ProposeCast">
/// Proposes a Card definition identity to cast, or <c>null</c> to cast nothing.
/// It receives the same attempted-candidate set as <see cref="ProposeSwap"/> for
/// symmetry; the Card rule needs no cursor because
/// <c>CARD_RULES.md</c> §3 item 6 already bounds a Turn to one successful cast.
/// </param>
internal sealed record BalancePlayerPolicy(
    string Name,
    Func<BattleState, BalancePolicyContext, IReadOnlySet<int>, SwapRequest?> ProposeSwap,
    Func<BattleState, BalancePolicyContext, IReadOnlySet<int>, string?> ProposeCast)
{
    /// <summary>
    /// Encodes one candidate Swap as a single ordering key, so the policy's
    /// "already attempted" set is comparable and deterministic. The pair is
    /// canonical (<c>from &lt; to</c>), matching
    /// <c>CommittedSwapPair</c>'s own canonicalization.
    /// </summary>
    public static int PairKey(int from, int to) =>
        (Math.Min(from, to) * BoardState.CellCount) + Math.Max(from, to);
}

/// <summary>
/// The read-only facts a policy may consult about the run's content — the Card
/// loadout and each Card's authored cost (<c>DATABASE.md</c> §1).
///
/// It carries <b>content</b> only. It exposes no combat value and no projected
/// outcome, so a policy cannot accidentally decide an action from a result the
/// rules have not produced yet.
/// </summary>
/// <param name="EquippedCardIds">The Card definition ids in loadout snapshot order.</param>
/// <param name="PowerCostOf">Reads a Card definition's authored <c>PowerCost</c>.</param>
internal readonly record struct BalancePolicyContext(
    IReadOnlyList<string> EquippedCardIds,
    Func<string, int> PowerCostOf);
