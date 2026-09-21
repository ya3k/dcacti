namespace GameServer.Domain.Match3;

using GameServer.Domain.Battle;

/// <summary>
/// Gameplay Swap Validation (<c>MATCH3_RULES.md</c> §2, §2.1.2).
///
/// <code>
/// BoardState + SwapRequest + last committed pair  →  SwapValidationResult
/// </code>
///
/// It answers exactly one question: <i>"is this requested swap legal on the
/// current authoritative board?"</i> It decides; it does not act. No exchange is
/// committed, no Turn begins, no <c>Sequence</c> advances, no board is resolved,
/// no Special Gem is created or activated, and no Turn/Combo/Power/Passive/Relic
/// progression occurs (<c>MATCH3_RULES.md</c> §2.1.5).
///
/// <b>Distinct from the Board Generation Validator</b> (<c>MATCH3_RULES.md</c>
/// §1.4.2). <see cref="BoardGenerationValidator"/> enforces the two
/// initialization-time constraints (§1.3/§1.4), runs once at generation, and per
/// §1.4.2 item 2 never processes a player Swap request, never commits or reverts
/// a Swap, and never starts a Turn. This type is the Gameplay Swap Validator and
/// does only that. The two are not interchangeable, and this type does not call
/// that one.
///
/// <b>Pure and non-mutating.</b> Validation draws no RNG — index validation,
/// adjacency, staleness, simulation, and match detection all consume nothing
/// (<c>MATCH3_RULES.md</c> §7.2) — so the invariant holds for an accepted and a
/// rejected request alike:
///
/// <code>
/// Before validation:  BoardState = X   RngState = R
/// After  validation:  BoardState = X   RngState = R
/// </code>
///
/// <see cref="BoardState"/> is immutable by construction and the hypothetical
/// exchange is expressed as a derived board (<see cref="BoardState.WithSwapped"/>),
/// so the caller's board is structurally incapable of being modified. The class
/// takes no RNG and no store, and so cannot consume randomness, advance a
/// counter, or emit an event.
///
/// <b>The committed pair is read, never written.</b> The already-applied check
/// reads <see cref="BattleState.LastCommittedSwapPair"/> — the field
/// <c>GAME_STATE.md</c> §2.1.10 establishes for it — but this type only compares
/// it: <c>BattleState</c> is read for its value and is never rebound here. Writing
/// the record belongs to the Swap-execution stage
/// (<see cref="SwapExecutor"/>), which owns the commit, in the same single
/// post-resolution write-back as <c>Turn</c> and <c>Sequence</c>
/// (<c>GAME_STATE.md</c> §2.1.10 item 5, §5.1).
/// </summary>
public static class SwapValidator
{
    /// <summary>
    /// Validates one requested Swap against the current authoritative board and
    /// the current committed pair, evaluating the documented checks in the
    /// <c>MATCH3_RULES.md</c> §2.1.2 order:
    ///
    /// <code>
    /// 1. Index range and distinctness   → INVALID_CELL_INDEX
    /// 2. Adjacency (§2 item 1)          → INVALID_SWAP
    /// 3. Already applied (§2.1.4)       → STALE_ACTION
    /// 4. Match-producing (§2 item 3)    → NO_MATCH_FROM_SWAP
    /// </code>
    ///
    /// The order is fixed so that two implementations reject the same action with
    /// the same reason when more than one check could fail (§2.1.2). Checks are
    /// evaluated in sequence and an earlier failure short-circuits the rest:
    /// staleness is not evaluated for a pair that is not adjacent, and match
    /// legality is not evaluated for a pair that is already applied — exactly as
    /// the §1.4.1 generation-time validator does not evaluate a swap that is not a
    /// swap.
    ///
    /// <b>Staleness is check 3.</b> §2.1.4 item 2 rejects an action whose unordered
    /// pair <c>{from, to}</c> is the pair most recently committed to the board, and
    /// §2.1.4 states that pair's owner: <c>BattleState.LastCommittedSwapPair</c>
    /// (<c>GAME_STATE.md</c> §2.1.10). This overload therefore takes the current
    /// committed pair as its third input. The comparison is between unordered
    /// pairs, so with the record canonical at <c>(12, 13)</c> both
    /// <c>Validate(board, pair, 12, 13)</c> and <c>Validate(board, pair, 13, 12)</c>
    /// are stale (§2.1.1 item 2). While the pair is <c>null</c> — no Swap has been
    /// committed — check 3 can never fail, so a fresh battle never rejects an
    /// action as already applied (§2.1.10 item 4). The check compares two
    /// cell-index pairs: it draws no RNG and simulates no board (§2.1.4 item 2).
    ///
    /// <b>Symmetry.</b> The order of <c>from</c> and <c>to</c> has no gameplay
    /// meaning (§2.1.1 item 2, §2.1.3 item 3), so <c>Validate(board, pair, a, b)</c>
    /// and <c>Validate(board, pair, b, a)</c> always return the same result.
    /// </summary>
    /// <param name="board">
    /// The current authoritative board. It is read only; the hypothetical
    /// exchange is performed on a derived copy and this instance is never
    /// modified (§2 item 2, §2.1.5 item 1).
    /// </param>
    /// <param name="lastCommittedSwapPair">
    /// The pair most recently committed to the board — the authoritative record
    /// <c>GAME_STATE.md</c> §2.1.10 defines — or <c>null</c> when no Swap has been
    /// committed. It is read for its value and is never written here: recording a
    /// commit is the Swap-execution stage's act, not validation's.
    /// </param>
    /// <param name="request">The two §1.0 cell indices the player is exchanging.</param>
    /// <returns>
    /// The validation decision — accepted, or rejected with the first documented
    /// reason that applies (§2.1.2).
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="board"/> is <c>null</c>. A Swap is validated against an
    /// authoritative board; there is no board-less validation.
    /// </exception>
    public static SwapValidationResult Validate(
        BoardState board,
        CommittedSwapPair? lastCommittedSwapPair,
        SwapRequest request)
    {
        ArgumentNullException.ThrowIfNull(board);

        return Validate(board, lastCommittedSwapPair, request.From, request.To);
    }

    /// <summary>
    /// Validates one requested Swap against the current authoritative board and
    /// the current committed pair, evaluating the documented checks in the
    /// <c>MATCH3_RULES.md</c> §2.1.2 order.
    ///
    /// This overload takes the two cell indices directly. It exists so a caller
    /// holding the pair does not have to construct a <see cref="SwapRequest"/> to
    /// ask the question; it is the same operation as
    /// <see cref="Validate(BoardState, CommittedSwapPair?, SwapRequest)"/> and
    /// implements no second rule.
    /// </summary>
    /// <param name="board">
    /// The current authoritative board. It is read only and is never modified.
    /// </param>
    /// <param name="lastCommittedSwapPair">
    /// The pair most recently committed to the board, or <c>null</c> when none has
    /// been. Read only.
    /// </param>
    /// <param name="from">The §1.0 cell index the player is moving.</param>
    /// <param name="to">The §1.0 cell index it is exchanged with.</param>
    /// <returns>
    /// The validation decision — accepted, or rejected with the first documented
    /// reason that applies (§2.1.2).
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="board"/> is <c>null</c>.
    /// </exception>
    public static SwapValidationResult Validate(
        BoardState board,
        CommittedSwapPair? lastCommittedSwapPair,
        int from,
        int to)
    {
        ArgumentNullException.ThrowIfNull(board);

        // 1. Index range and distinctness (§2.1.2 items 1–2).
        //
        // Both cells must be a §1.0 cell index in 0..63, and from must differ
        // from to: "a swap of a cell with itself is not a Swap". An out-of-range
        // value is rejected rather than clamped, folded, or reinterpreted
        // (§2.1.3 item 2).
        if (!IsCellIndex(from) || !IsCellIndex(to))
        {
            return SwapValidationResult.Rejected(SwapRejectionReason.InvalidCellIndex);
        }

        if (from == to)
        {
            return SwapValidationResult.Rejected(SwapRejectionReason.InvalidCellIndex);
        }

        // 2. Adjacency (§2.1.2 item 3, §2 item 1).
        //
        // Evaluated on the §1.0 row-major mapping by the board's own coordinate
        // helper, never on raw index arithmetic alone: 7 and 8 differ by 1 but
        // are not adjacent, and no wraparound exists across a row edge
        // (§2.1.3 item 1). A diagonal pair and a distant pair are rejected here
        // too — the same check covers all three.
        if (!BoardState.AreOrthogonallyAdjacent(from, to))
        {
            return SwapValidationResult.Rejected(SwapRejectionReason.InvalidSwap);
        }

        // 3. Already applied / idempotency (§2.1.2 item 4, §2.1.4 item 2).
        //
        // The action's unordered pair must not be the pair most recently committed
        // to the board. The stored record is canonical (min, max) and the action's
        // pair is {from, to}, so this is a comparison between unordered pairs:
        // (13, 12) after a committed (12, 13) is the already-applied case exactly as
        // (12, 13) is. A missing record — no Swap committed yet — never matches, so
        // check 3 cannot fail on a fresh battle (§2.1.10 item 4). The record is read
        // and never written: a rejection changes nothing, including clearing
        // nothing (§2.1.10 items 6–7).
        if (CommittedSwapPair.FromCells(from, to).Matches(lastCommittedSwapPair))
        {
            return SwapValidationResult.Rejected(SwapRejectionReason.StaleAction);
        }

        // 4. Match-producing (§2.1.2 item 5, §2 item 3).
        //
        // The swap is simulated on a copy of the board (§2 item 2) and must
        // produce at least one §3 Match. "At least one" is the whole
        // requirement: no minimum number of matches, no tier requirement, and no
        // quality measure beyond this (§2.1.2 item 5).
        if (!ProducesMatch(board, from, to))
        {
            return SwapValidationResult.Rejected(SwapRejectionReason.NoMatchFromSwap);
        }

        return SwapValidationResult.Accepted();
    }

    /// <summary>
    /// True when exchanging the two cells would produce at least one §3 Match
    /// (<c>MATCH3_RULES.md</c> §2 item 3, applying §3).
    ///
    /// The exchange is derived on a copy — <see cref="BoardState.WithSwapped"/>
    /// returns a new board and leaves the argument untouched (§2 item 2) — and
    /// the resulting board is evaluated by <see cref="MatchDetector"/>, the one
    /// authoritative implementation of §3. Match detection is not re-implemented
    /// here; this method only supplies the candidate board and asks whether the
    /// pass found anything.
    ///
    /// The whole board is detected rather than only the neighbourhood of the two
    /// cells. That is deliberately the §3 contract rather than the §1.4.3
    /// minimum: §3.1 item 1 defines a detection pass over a whole fixed board
    /// state, so using <see cref="MatchDetector"/> keeps exactly one
    /// authoritative Match Detection in the codebase (<c>AGENTS.md</c> §9) and
    /// cannot disagree with the detection the resolution itself will run.
    ///
    /// This is a <b>hypothetical</b> exchange for the legality question, not a
    /// commit: the same primitive <see cref="SwapExecutor"/> later uses for the
    /// real exchange, applied to a board the validator never hands back. Detection
    /// draws no RNG and mutates nothing (§7.2), so this consumes no randomness and
    /// leaves both boards as they were.
    /// </summary>
    private static bool ProducesMatch(BoardState board, int firstIndex, int secondIndex)
    {
        var exchanged = board.WithSwapped(firstIndex, secondIndex);

        return MatchDetector.Detect(exchanged).Count > 0;
    }

    /// <summary>
    /// True when <paramref name="index"/> is a §1.0 cell index — an integer in
    /// <c>0..63</c> (<c>MATCH3_RULES.md</c> §1.0).
    ///
    /// This is the range half of §2.1.2 item 1. It is a predicate rather than a
    /// guard because an out-of-range value is a <i>rejected request</i>, not a
    /// thrown error: the validation contract's answer to it is
    /// <see cref="SwapRejectionReason.InvalidCellIndex"/>.
    /// </summary>
    private static bool IsCellIndex(int index) => index >= 0 && index < BoardState.CellCount;
}