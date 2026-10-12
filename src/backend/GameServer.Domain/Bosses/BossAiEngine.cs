namespace GameServer.Domain.Bosses;

using GameServer.Domain.Match3;

/// <summary>
/// Explicit scoring weights for deterministic Boss move evaluation (<c>ADR-026</c> §8, Scope D).
/// Grounded in <c>COMBAT_RULES.md</c> §2 base output values (ATK: 10, DEF: 5, HP: 20, POWER: 10).
/// </summary>
/// <param name="AtkWeight">Weight for ATK gem matches (default: 10, base damage output).</param>
/// <param name="DefWeight">Weight for DEF gem matches (default: 5, base defense output).</param>
/// <param name="HpWeight">Weight for HP gem matches (default: 20, base heal output).</param>
/// <param name="PowerWeight">Weight for POWER gem matches (default: 10, base power output).</param>
public readonly record struct BossMoveScoringWeights(
    int AtkWeight,
    int DefWeight,
    int HpWeight,
    int PowerWeight)
{
    /// <summary>Creates scoring weights with canonical defaults.</summary>
    public BossMoveScoringWeights() : this(10, 5, 20, 10) { }

    /// <summary>The canonical default weights grounded in COMBAT_RULES.md §2.</summary>
    public static BossMoveScoringWeights Default => new(10, 5, 20, 10);

    /// <summary>Gets the weight for a specific gem type.</summary>
    public int WeightFor(GemType type) => type switch
    {
        GemType.Atk => AtkWeight,
        GemType.Def => DefWeight,
        GemType.Hp => HpWeight,
        GemType.Power => PowerWeight,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown gem type."),
    };
}

/// <summary>
/// A candidate move evaluated by the Boss AI.
/// </summary>
/// <param name="Request">The candidate swap request.</param>
/// <param name="Score">The calculated score for this move.</param>
public readonly record struct BossCandidateMove(SwapRequest Request, int Score);

/// <summary>
/// The outcome of Boss move selection (<c>ADR-026</c> §8, Scope D).
/// </summary>
/// <param name="SelectedMove">The selected swap move.</param>
/// <param name="Score">The score of the selected move.</param>
/// <param name="Reshuffled">True iff a deadlock occurred and the board was reshuffled.</param>
/// <param name="Board">The board to act on (new board if reshuffled, original board otherwise).</param>
/// <param name="RngState">The updated RNG state (advanced if reshuffled, unchanged otherwise).</param>
/// <param name="LegalCandidatesCount">Total number of legal candidate moves evaluated.</param>
public readonly record struct BossMoveSelectionResult(
    SwapRequest SelectedMove,
    int Score,
    bool Reshuffled,
    BoardState Board,
    RngState RngState,
    int LegalCandidatesCount);

/// <summary>
/// Deterministic Boss AI Move Selection Engine (<c>ADR-026</c> §8, Scope D).
///
/// <code>
/// BoardState → Enumerate Legal Swaps (up to 112 adjacent pairs)
///            → Deadlock Check: if 0 legal swaps → Reshuffle via Pcg32 → Re-enumerate
///            → Score candidates deterministically via BossMoveScoringWeights
///            → Tie-breaker: lowest 'From' index, then lowest 'To' index
///            → Selected SwapRequest
/// </code>
///
/// <b>Deterministic:</b> Consumes zero unseeded PRNG for move selection.
/// When deadlock occurs, consumes PRNG strictly through the established <see cref="Pcg32"/>
/// stream for bounded board reshuffle.
///
/// <b>Pure:</b> Does not mutate authoritative battle state during candidate evaluation.
/// </summary>
public static class BossAiEngine
{
    /// <summary>
    /// Total adjacent orthogonal pairs on an 8x8 board:
    /// 56 horizontal (8 rows * 7 columns) + 56 vertical (7 rows * 8 columns) = 112.
    /// </summary>
    public const int TotalCandidatePairs = 112;

    /// <summary>
    /// Enumerates all 112 orthogonal adjacent candidate pairs on the 8x8 board in deterministic order:
    /// row 0..7, column 0..7; horizontal neighbor first, then vertical neighbor.
    /// </summary>
    public static IReadOnlyList<(int From, int To)> EnumerateAdjacentPairs()
    {
        var pairs = new List<(int From, int To)>(TotalCandidatePairs);

        for (var row = 0; row < BoardState.Rows; row++)
        {
            for (var col = 0; col < BoardState.Columns; col++)
            {
                var index = BoardState.ToIndex(row, col);

                // Horizontal neighbor (to the right)
                if (col + 1 < BoardState.Columns)
                {
                    pairs.Add((index, BoardState.ToIndex(row, col + 1)));
                }

                // Vertical neighbor (downwards)
                if (row + 1 < BoardState.Rows)
                {
                    pairs.Add((index, BoardState.ToIndex(row + 1, col)));
                }
            }
        }

        return pairs;
    }

    /// <summary>
    /// Enumerates all legal candidate moves on the board with their deterministic scores.
    /// </summary>
    public static IReadOnlyList<BossCandidateMove> EnumerateLegalMoves(
        BoardState board,
        BossMoveScoringWeights? weights = null)
    {
        ArgumentNullException.ThrowIfNull(board);
        var scoringWeights = weights ?? BossMoveScoringWeights.Default;

        var allPairs = EnumerateAdjacentPairs();
        var legalMoves = new List<BossCandidateMove>();

        for (var i = 0; i < allPairs.Count; i++)
        {
            var (from, to) = allPairs[i];
            var exchanged = board.WithSwapped(from, to);
            var matches = MatchDetector.Detect(exchanged);

            if (matches.Count == 0)
            {
                continue;
            }

            var score = CalculateMoveScore(matches, scoringWeights);
            legalMoves.Add(new BossCandidateMove(new SwapRequest(from, to), score));
        }

        return legalMoves;
    }

    /// <summary>
    /// Calculates the deterministic score of a move based on its immediate first-pass matches.
    /// Score = sum of (weight * tierNumerator / tierDenominator * cellsCount) across all matches.
    /// </summary>
    public static int CalculateMoveScore(
        IReadOnlyList<MatchShape> matches,
        BossMoveScoringWeights weights)
    {
        var totalScore = 0;

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var tier = match.IsLt
                ? MatchTier.Lt
                : MatchTierMultipliers.ForStraightRun((match.HorizontalArm ?? match.VerticalArm)!.Length);

            var numerator = MatchTierMultipliers.NumeratorFor(tier);
            var denominator = MatchTierMultipliers.Denominator;
            var weight = weights.WeightFor(match.GemType);

            // Grounded in COMBAT_RULES.md §2: base rate * tier multiplier * cells count.
            var matchScore = (weight * numerator * match.Cells.Count) / denominator;
            totalScore += matchScore;
        }

        return totalScore;
    }

    /// <summary>
    /// Selects the best legal move on the board deterministically.
    /// If deadlocked (0 legal moves), recovers by reshuffling the board using <paramref name="rng"/>.
    /// </summary>
    /// <param name="board">The current authoritative board.</param>
    /// <param name="rng">The authoritative PRNG stream used strictly if reshuffling is required.</param>
    /// <param name="weights">Optional custom scoring weights.</param>
    /// <returns>The move selection result containing the selected move, score, board, and RNG state.</returns>
    /// <exception cref="BoardGenerationFailedException">
    /// Thrown if deadlock recovery cannot produce a valid board within <see cref="BoardGenerator.MaxAttempts"/> attempts.
    /// </exception>
    public static BossMoveSelectionResult SelectMove(
        BoardState board,
        Pcg32 rng,
        BossMoveScoringWeights? weights = null)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(rng);

        var currentBoard = board;
        var currentRngState = rng.CurrentState;
        var reshuffled = false;

        var candidates = EnumerateLegalMoves(currentBoard, weights);

        // Deadlock detection & recovery (§8, Scope D):
        // If no legal valid swap exists, deterministically reshuffle using Pcg32.
        if (candidates.Count == 0)
        {
            var generationResult = BoardGenerator.Generate(rng);
            currentBoard = generationResult.Board!;
            currentRngState = rng.CurrentState;
            reshuffled = true;

            candidates = EnumerateLegalMoves(currentBoard, weights);

            if (candidates.Count == 0)
            {
                throw new BoardGenerationFailedException(
                    "Post-reshuffle board contains zero legal valid swaps.");
            }
        }

        // Deterministic selection & stable tie-breaker:
        // Highest score wins. If scores are equal, lowest 'From' index, then lowest 'To' index wins.
        var bestMove = candidates[0].Request;
        var bestScore = candidates[0].Score;

        for (var i = 1; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (candidate.Score > bestScore)
            {
                bestScore = candidate.Score;
                bestMove = candidate.Request;
            }
            else if (candidate.Score == bestScore)
            {
                // Tie-breaker: lowest From index, then lowest To index
                if (candidate.Request.From < bestMove.From ||
                    (candidate.Request.From == bestMove.From && candidate.Request.To < bestMove.To))
                {
                    bestMove = candidate.Request;
                }
            }
        }

        return new BossMoveSelectionResult(
            SelectedMove: bestMove,
            Score: bestScore,
            Reshuffled: reshuffled,
            Board: currentBoard,
            RngState: currentRngState,
            LegalCandidatesCount: candidates.Count);
    }
}
