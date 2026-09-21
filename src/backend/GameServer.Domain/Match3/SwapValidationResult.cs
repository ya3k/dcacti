namespace GameServer.Domain.Match3;

/// <summary>
/// The outcome of validating one requested Swap against the current
/// authoritative board (<c>MATCH3_RULES.md</c> §2.1.2).
///
/// <code>
/// BoardState + SwapRequest  →  SwapValidationResult
/// </code>
///
/// The result answers one question — <i>"is this requested swap legal on the
/// current authoritative board?"</i> — and nothing more. It is a decision, not
/// a resolution: it carries no board, no post-swap state, no match set, no
/// Combo, no Turn, and no <c>Sequence</c> value. Executing an accepted Swap is a
/// separate stage (<c>MATCH3_RULES.md</c> §2.1.6) and is not part of this type.
///
/// The result is <b>not</b> persisted, broadcast, or attached to any wire
/// message. A rejected Swap is reported to the caller only
/// (<c>MATCH3_RULES.md</c> §2.1.5 item 6, <c>SIGNALR_PROTOCOL.md</c> §5), which
/// is exactly the caller of <see cref="SwapValidator.Validate"/>.
/// </summary>
public readonly record struct SwapValidationResult
{
    private SwapValidationResult(bool isAccepted, SwapRejectionReason reason)
    {
        IsAccepted = isAccepted;
        Reason = reason;
    }

    /// <summary>
    /// True when the swap satisfies every documented check and would be
    /// committed (<c>MATCH3_RULES.md</c> §2.1.2: "all checks pass before the
    /// swap is committed").
    ///
    /// Acceptance says the action is <i>legal</i>. It does not commit anything,
    /// begin a Turn, or resolve the board.
    /// </summary>
    public bool IsAccepted { get; }

    /// <summary>
    /// True when the swap is rejected (<c>MATCH3_RULES.md</c> §2.1.5).
    /// </summary>
    public bool IsRejected => !IsAccepted;

    /// <summary>
    /// The documented rejection reason, or
    /// <see cref="SwapRejectionReason.None"/> when <see cref="IsAccepted"/>.
    ///
    /// <c>Reason == None</c> and <see cref="IsAccepted"/> always agree: an
    /// accepted result carries no rejection reason, and a rejected one always
    /// names the check that failed. Checks are evaluated in the §2.1.2 order, so
    /// when more than one check could fail the reason is the first one that did —
    /// two implementations reject the same action with the same reason (§2.1.2).
    /// </summary>
    public SwapRejectionReason Reason { get; }

    /// <summary>
    /// The accepted result — every documented check passed
    /// (<c>MATCH3_RULES.md</c> §2.1.2).
    /// </summary>
    public static SwapValidationResult Accepted() => new(true, SwapRejectionReason.None);

    /// <summary>
    /// A rejected result naming the check that failed
    /// (<c>MATCH3_RULES.md</c> §2.1.2).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="reason"/> is <see cref="SwapRejectionReason.None"/>, which
    /// is the accepted result's reason and not a rejection. Constructing that
    /// pair would make <see cref="IsRejected"/> and <see cref="Reason"/>
    /// disagree, so it is rejected as a contract violation rather than
    /// represented.
    /// </exception>
    public static SwapValidationResult Rejected(SwapRejectionReason reason)
    {
        if (reason == SwapRejectionReason.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reason),
                reason,
                "A rejected Swap must name the check that failed (MATCH3_RULES.md §2.1.2); "
                + "None is the accepted result's reason.");
        }

        return new SwapValidationResult(false, reason);
    }
}