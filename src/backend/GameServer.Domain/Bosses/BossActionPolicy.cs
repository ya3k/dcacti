namespace GameServer.Domain.Bosses;

/// <summary>
/// Which action the Boss response phase selected
/// (<c>BOSS_RULES.md</c> §4.3; <c>ADR-029</c> §2.7; TASK-308).
///
/// <b>Exactly one action per response phase.</b> The Boss either performs its
/// board match action or casts its Boss Card — never both — or, when neither is
/// available, takes no action at all and the Round still proceeds
/// (<c>BOSS_RULES.md</c> §4.3 step 1 and item 4).
/// </summary>
public enum BossActionKind
{
    /// <summary>
    /// Neither action is available: no board resolution, no cast, no attack.
    /// The documented branch is defined but not reachable in the current engine
    /// because a successful deadlock recovery guarantees a legal swap and a
    /// failed recovery throws (<c>BOSS_RULES.md</c> §4.3).
    /// </summary>
    None = 0,

    /// <summary>The Boss's board match action (step 18a).</summary>
    BoardAction = 1,

    /// <summary>The Boss's Boss Card cast (step 18c).</summary>
    BossCardCast = 2,
}

/// <summary>
/// The deterministic outcome of one Boss action selection
/// (<c>BOSS_RULES.md</c> §4.3; <c>ADR-029</c> §2.7 item 18; TASK-308).
/// </summary>
/// <param name="Action">The one selected action.</param>
/// <param name="BoardScore">
/// The evaluated board candidate's score when a board action was evaluated, or
/// <c>null</c> when the board action was unavailable. It is the existing
/// <see cref="BossMoveSelectionResult.Score"/> — reported, never recomputed.
/// </param>
/// <param name="CastScore">
/// The authored <see cref="BossDefinition.CastPriorityScore"/> when a cast was
/// evaluated, or <c>null</c> when no cast was available. It is content read
/// from the Boss definition, never derived by the policy.
/// </param>
public readonly record struct BossActionSelection(
    BossActionKind Action,
    int? BoardScore,
    int? CastScore)
{
    /// <summary>
    /// "BossCardCast (board 15, cast 16)" — for test diagnostics only.
    /// </summary>
    public override string ToString() =>
        $"{Action} (board {BoardScore?.ToString() ?? "-"}, cast {CastScore?.ToString() ?? "-"})";
}

/// <summary>
/// The deterministic one-action policy for the Boss response phase
/// (<c>BOSS_RULES.md</c> §4.3; <c>ADR-029</c> §2.7; TASK-308).
///
/// <code>
/// eligibility (hard gates, ordered)
///   board available iff the legal-move enumeration found a match-producing swap
///   cast available iff the Boss has its Boss Card, BossState.Power >= cost,
///     and the decremented cooldown is 0
///         ↓
/// selection (deterministic, pure)
///   exactly one available  → that action, whatever its score
///   both available         → higher score wins; the cast must STRICTLY out-score
///                            the board action (a tie goes to the board)
///   neither available      → no action; the Round proceeds
/// </code>
///
/// <b>Pure and deterministic.</b> The policy consumes no PRNG, reads no clock
/// and no client input, and is a function only of its arguments. Candidate
/// enumeration order, the board score and the recovered board (including the
/// pre-selection deadlock reshuffle and its seeded RNG consumption) remain
/// <see cref="BossAiEngine"/>'s, unchanged.
///
/// <b>Invalid content fails closed.</b> A cast that is available must carry a
/// non-null, non-negative authored score. <c>null</c> is the documented "not
/// authored" marker and is legal only for a Boss with no Boss Card; a migrated
/// Boss whose score is <c>null</c> or negative is invalid definition data, so
/// this policy throws rather than silently selecting the board action
/// (<c>BOSS_RULES.md</c> §4.3 item 5, §4.4 item 6; <c>ADR-024</c> §2.4). Battle
/// creation performs the same validation earlier, so this throw is the
/// selection-time invariant rather than the primary gate.
/// </summary>
public static class BossActionPolicy
{
    /// <summary>
    /// Selects exactly one action for one response phase.
    /// </summary>
    /// <param name="boardActionAvailable">
    /// Whether the post-recovery legal-move enumeration found at least one
    /// match-producing adjacent swap (<c>BOSS_RULES.md</c> §4.3 step 1).
    /// </param>
    /// <param name="castAvailable">
    /// Whether the Boss's Boss Card is present, its <c>EffectiveCardCost</c> is
    /// affordable from <c>BossState.Power</c>, and the decremented cooldown is
    /// <c>0</c> (<c>BOSS_RULES.md</c> §4.3 step 1).
    /// </param>
    /// <param name="boardScore">
    /// The selected board candidate's <see cref="BossMoveSelectionResult.Score"/>
    /// on the post-recovery board. Ignored when the board action is unavailable.
    /// </param>
    /// <param name="castPriorityScore">
    /// The Boss definition's authored <see cref="BossDefinition.CastPriorityScore"/>.
    /// It must be non-null and <c>&gt;= 0</c> whenever a cast is available.
    /// </param>
    /// <returns>The one selected action and the evaluated scores.</returns>
    /// <exception cref="InvalidOperationException">
    /// A cast is available but the authored score is <c>null</c> or negative —
    /// invalid definition data that must fail closed rather than degrading to
    /// another action form.
    /// </exception>
    public static BossActionSelection Select(
        bool boardActionAvailable,
        bool castAvailable,
        int boardScore,
        int? castPriorityScore)
    {
        if (!boardActionAvailable && !castAvailable)
        {
            return new BossActionSelection(BossActionKind.None, null, null);
        }

        // "If exactly one action is available → that action is selected,
        // whatever its score" (BOSS_RULES.md §4.3 step 2). The cast still has
        // to carry a valid authored score: availability without a score is the
        // migrated-content-invalid case, and it fails closed.
        if (!boardActionAvailable)
        {
            return new BossActionSelection(
                BossActionKind.BossCardCast,
                BoardScore: null,
                CastScore: RequireCastPriorityScore(castPriorityScore));
        }

        if (!castAvailable)
        {
            return new BossActionSelection(
                BossActionKind.BoardAction,
                BoardScore: boardScore,
                CastScore: null);
        }

        var castScore = RequireCastPriorityScore(castPriorityScore);

        // "The higher score wins; a tie is resolved in favour of the BOARD MATCH
        // ACTION — a cast must strictly out-score the board action"
        // (BOSS_RULES.md §4.3 steps 5–6). A tie therefore selects the board,
        // whose own tie-break (lowest From then lowest To) is BossAiEngine's
        // and is not changed here.
        return castScore > boardScore
            ? new BossActionSelection(BossActionKind.BossCardCast, boardScore, castScore)
            : new BossActionSelection(BossActionKind.BoardAction, boardScore, castScore);
    }

    /// <summary>
    /// Requires the authored cast-priority score a selected or evaluated cast
    /// needs, and names the fail-closed reason when it is absent or negative.
    /// </summary>
    private static int RequireCastPriorityScore(int? castPriorityScore)
    {
        if (castPriorityScore is not >= 0)
        {
            throw new InvalidOperationException(
                "A Boss Card cast requires a non-null, non-negative CastPriorityScore. "
                + "NULL is the 'not authored' marker and is legal only for a Boss with no "
                + "Boss Card; a migrated Boss whose value is NULL or negative is invalid "
                + "definition data (BOSS_RULES.md §4.3 item 5, §4.4 item 6; ADR-024 §2.4). "
                + "Battle creation fails closed rather than selecting a different action.");
        }

        return castPriorityScore.Value;
    }
}
