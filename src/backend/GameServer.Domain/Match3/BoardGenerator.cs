namespace GameServer.Domain.Match3;

/// <summary>
/// The outcome of initial board generation (<c>MATCH3_RULES.md</c> §1.2.1,
/// §1.5).
/// </summary>
/// <param name="Board">
/// The accepted initial board. Non-null only when generation succeeded.
/// </param>
/// <param name="RngState">
/// The RNG state after all consumption used by the accepted attempt
/// (<c>GAME_STATE.md</c> §2.7.1 step 6, §2.6.2). The state is retained, never
/// reset and never re-derived from the board.
/// </param>
/// <param name="Attempts">
/// How many candidate boards were drawn before one was accepted. At least 1 on
/// success.
/// </param>
public readonly record struct BoardGenerationResult(BoardState? Board, RngState RngState, int Attempts)
{
    /// <summary>True when a board satisfying §1.3 and §1.4 was accepted.</summary>
    public bool Succeeded => Board is not null;
}

/// <summary>
/// Raised when generation fails within the documented retry bound
/// (<c>MATCH3_RULES.md</c> §1.5 item 3).
///
/// A failed generation is a creation-time error, not a Match, cascade, or
/// gameplay outcome, and it involves no Turn, Combo, Power, or damage (§1.5
/// item 5). No fallback construction is defined: there is no partial fill, no
/// forced pattern, no repair pass, and no "best effort" board. A battle is never
/// created with an initial board that violates §1.3 or §1.4.
/// </summary>
public sealed class BoardGenerationFailedException : Exception
{
    public BoardGenerationFailedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// The number of attempts made before failing — always the documented bound
    /// (<c>MATCH3_RULES.md</c> §1.5 item 2).
    /// </summary>
    public int Attempts { get; init; }
}

/// <summary>
/// Deterministic initial board generation (<c>MATCH3_RULES.md</c> §1.2.1):
/// a <b>deterministic row-major constrained random fill</b>.
///
/// The documented contract, implemented in order:
///
/// <code>
/// 1. Server obtains the battle's RNG state (GAME_STATE.md §2.6).
/// 2. Fill the 8x8 board in deterministic row-major order (§1.2.1.1),
///    drawing each cell from its constrained candidate set (§1.2.1.2).
/// 3. Validate the completed candidate against §1.3 and §1.4.
/// 4. If invalid, retry per §1.5.
/// 5. Accept the valid board as the initial board.
/// 6. Retain the resulting RNG state (GAME_STATE.md §2.6.2).
/// </code>
///
/// The fill is still random — the draw is random, the candidate set it draws
/// from is constrained (§1.2 item 1). "Randomly filled" never means independent
/// unrestricted uniform selection per cell (§1.2).
///
/// Generation is deterministic: the same RNG state always yields the same
/// candidate board and the same sequence of retries, so the same seed always
/// produces the same initial board (§1.2.1, §7 item 4). A rejected candidate is
/// discarded and the next candidate is drawn by continuing the same PRNG stream
/// — the generator is never re-seeded, restarted, or perturbed per attempt
/// (§1.5 item 1).
///
/// This constrained fill applies to <b>initial board generation only</b>
/// (§1.2.1.5). Cascade spawns into empty cells (§4 step 3) are unconstrained
/// independent draws and are <b>not</b> produced by this algorithm; that is a
/// separate gameplay rule and is not implemented here.
///
/// This is an <b>initialization-time</b> concern (<c>GAME_STATE.md</c> §2.7.2):
/// it runs once, before any player action, emits no Battle Events, consumes no
/// Turn and no Sequence, and does not run again after initialization — nothing
/// at this stage re-rolls or repairs a board once accepted.
/// </summary>
public static class BoardGenerator
{
    /// <summary>
    /// The documented hard retry bound (<c>MATCH3_RULES.md</c> §1.5 item 2):
    /// generation makes at most <b>64</b> attempts.
    ///
    /// This is an engineering safety limit expressed as a rule, not a gameplay
    /// mechanic (§1.5 item 4). It makes termination unconditional — the loop
    /// cannot run forever.
    /// </summary>
    public const int MaxAttempts = 64;

    /// <summary>
    /// Generates the initial board from a battle's seed, or fails
    /// deterministically within the documented bound.
    /// </summary>
    /// <param name="seed">
    /// The battle's server-chosen <c>RngSeed</c> (<c>GAME_STATE.md</c> §2.6.1).
    /// </param>
    /// <returns>The accepted board together with the retained resulting RNG state.</returns>
    /// <exception cref="BoardGenerationFailedException">
    /// No candidate satisfied §1.3 and §1.4 within <see cref="MaxAttempts"/>
    /// attempts (§1.5 item 3).
    /// </exception>
    public static BoardGenerationResult Generate(ulong seed) =>
        Generate(Pcg32.FromSeed(seed));

    /// <summary>
    /// Generates the initial board from an existing generator, continuing the
    /// caller's stream.
    ///
    /// This overload is how a battle resumes generation from a stored
    /// <c>RngState</c> (<c>GAME_STATE.md</c> §2.6.2: "where the stream resumes").
    /// The generator is advanced in place and is never re-seeded here.
    /// </summary>
    public static BoardGenerationResult Generate(Pcg32 rng)
    {
        ArgumentNullException.ThrowIfNull(rng);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            // §1.2.1 FIFO: one attempt = generate 64 cells, then validate the
            // completed candidate. Each attempt consumes exactly 64 selections
            // from the same continuing stream (§1.2.1.4 items 3–4).
            var candidate = GenerateCandidate(rng);

            // §1.3 (no pre-existing Match) and §1.4 (at least one valid Swap).
            // Both are required (§1.4.1). Validation draws nothing: it reads the
            // completed board only, so it cannot advance the stream (§1.2.1.4
            // item 3: "no separate validation draw").
            if (BoardGenerationValidator.IsValidInitialBoard(candidate))
            {
                // §1.5 item 1: the stream continues, so the retained state is the
                // state after the accepted attempt's consumption — not a reset
                // and not a value derived from the board.
                return new BoardGenerationResult(candidate, rng.CurrentState, attempt);
            }

            // §1.5 item 1: retry by continuing the same PRNG stream. The
            // generator is never re-seeded, restarted, or perturbed per attempt —
            // which is why nothing reseeds `rng` here.
        }

        // §1.5 item 3: fail deterministically. The seed is not changed, no other
        // RNG is substituted, no invalid board is accepted, and there is no
        // fallback construction.
        throw new BoardGenerationFailedException(
            $"Initial board generation failed: no candidate satisfied {nameof(BoardGenerationValidator)} "
            + $"constraints (MATCH3_RULES.md §1.3, §1.4) within {MaxAttempts} attempts (§1.5 item 2).")
        {
            Attempts = MaxAttempts,
        };
    }

    /// <summary>
    /// Draws one candidate 8x8 board by the documented deterministic row-major
    /// constrained random fill (<c>MATCH3_RULES.md</c> §1.2.1).
    ///
    /// Cells are filled one at a time in deterministic row-major order —
    /// <c>index 0 → 63</c> (§1.2.1.1). The order is fixed and does not depend on
    /// RNG output.
    ///
    /// For each cell (§1.2.1.2): exclude the types that would extend an
    /// already-filled horizontal or vertical run of 3, then draw uniformly from
    /// the remaining candidates and write the result.
    ///
    /// Exactly <b>one</b> RNG selection is consumed per cell — 64 per attempt —
    /// regardless of how many candidates remain (§1.2.1.4 item 1). Nothing else
    /// in this method draws: not the exclusion check, not the fill order, not
    /// the indexing, not validation.
    /// </summary>
    private static BoardState GenerateCandidate(Pcg32 rng)
    {
        var cells = new GemType[BoardState.CellCount];

        for (var index = 0; index < cells.Length; index++)
        {
            // §1.2.1.2 steps 1–3: determine the excluded types and build the
            // candidate set in the fixed §1.1 order.
            var candidates = GetCandidateTypes(cells, index);

            // §1.2.1.2 step 4 / §1.2.1.4 item 2: the uniform selection is a
            // documented reduction onto the candidate set. `NextBounded` is the
            // canonical `pcg32_boundedrand_r`, which is exactly the documented
            // "selection mod |candidate set|" reduction (ADR-009 owns the
            // generator's output and advancement semantics; this rule does not
            // change them).
            var selection = rng.NextBounded((uint)candidates.Count);

            // §1.2.1.2 steps 5–6: write the selected Gem and continue.
            cells[index] = candidates[(int)selection];
        }

        return BoardState.FromCells(cells);
    }

    /// <summary>
    /// The candidate Gem types for a cell — the §1.1 types in canonical order
    /// minus the §1.2.1.2 exclusions.
    ///
    /// Only <b>already-filled</b> cells are consulted (§1.2.1.2), so the check is
    /// exactly the completed portion of the run the current cell would extend:
    ///
    /// <code>
    /// Horizontal: column >= 2 and (row, column-1) == (row, column-2) == T
    ///             → T would create a horizontal run of 3; exclude T.
    /// Vertical:   row    >= 2 and (row-1, column) == (row-2, column) == T
    ///             → T would create a vertical run of 3; exclude T.
    /// </code>
    ///
    /// The exclusion set is the <b>union</b> of both lines. Each line can exclude
    /// at most one type, so at most two types are excluded and the candidate set
    /// always holds at least two of the four types — as a property of the
    /// constraint, not as a fallback. <c>MATCH3_RULES.md</c> §1.2.1.2 defines no
    /// rule for an empty candidate set because the algorithm cannot produce one.
    ///
    /// This consults the §3 Match shape but is <b>not</b> Match Detection
    /// (§1.2.1.3): it produces no match results, tiers, shapes, or events, and
    /// detects nothing during gameplay. It only prevents the generator from
    /// creating a match while it builds the board.
    /// </summary>
    /// <param name="cells">
    /// The partially filled board; entries at or after <paramref name="index"/>
    /// are not yet filled and are never read.
    /// </param>
    /// <param name="index">The row-major index of the cell being filled.</param>
    private static List<GemType> GetCandidateTypes(GemType[] cells, int index)
    {
        var row = BoardState.ToRow(index);
        var column = BoardState.ToColumn(index);

        // up to two excluded types — the union of the horizontal and vertical
        // line exclusions (§1.2.1.2).
        GemType? horizontalExcluded = null;
        GemType? verticalExcluded = null;

        if (column >= 2)
        {
            var left = cells[index - 1];
            if (left == cells[index - 2])
            {
                horizontalExcluded = left;
            }
        }

        if (row >= 2)
        {
            var above = cells[index - BoardState.Width];
            if (above == cells[index - (BoardState.Width * 2)])
            {
                verticalExcluded = above;
            }
        }

        var candidates = new List<GemType>(GemTypes.All.Count);

        // Iterated in the fixed §1.1 order (ATK, DEF, HP, POWER), because the
        // selection is reduced by index into this list (§1.2.1.4 item 2)
        // and the order is therefore part of the deterministic result.
        foreach (var type in GemTypes.All)
        {
            if (type == horizontalExcluded || type == verticalExcluded)
            {
                continue;
            }

            candidates.Add(type);
        }

        // §1.2.1.2: "The candidate set is never empty." This is a property of
        // the constraint, not a fallback, and no rule defines behavior for an
        // empty set. Reaching this means the documented contract and the
        // implementation disagree — fail loudly rather than invent behavior.
        if (candidates.Count < 2)
        {
            throw new InvalidOperationException(
                $"Cell {index} (row {row}, column {column}) produced a candidate set of "
                + $"{candidates.Count}, but MATCH3_RULES.md §1.2.1.2 guarantees at least two "
                + "candidates for every cell of the initial 8x8 board. No rule defines an empty "
                + "or single-candidate set, so this is a contract/implementation conflict.");
        }

        return candidates;
    }
}