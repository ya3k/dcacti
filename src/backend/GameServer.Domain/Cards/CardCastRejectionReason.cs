namespace GameServer.Domain.Cards;

/// <summary>
/// Why a requested Card cast was rejected (<c>CARD_RULES.md</c> §3).
/// </summary>
public enum CardCastRejectionReason
{
    /// <summary>
    /// The cast is legal; nothing is rejected. The only reason that accompanies
    /// an accepted result (<c>CARD_RULES.md</c> §3).
    /// </summary>
    None = 0,

    /// <summary>
    /// The requested Card is not in the active Pet's equipped cards for this battle
    /// (<c>CARD_RULES.md</c> §3 item 2).
    /// </summary>
    CardNotInLoadout = 1,

    /// <summary>
    /// The active Pet's current Power is less than the Card's PowerCost
    /// (<c>CARD_RULES.md</c> §3 item 2).
    /// </summary>
    InsufficientPower = 2,

    /// <summary>
    /// The Card definition is invalid, unknown, or not a Basic Card
    /// (<c>CARD_RULES.md</c> §1, §3).
    /// </summary>
    InvalidCard = 3,

    /// <summary>
    /// The player has already successfully cast a Card during the current
    /// committed Match-3 Turn (<c>CARD_RULES.md</c> §3 item 6).
    ///
    /// A cast-count constraint, not Turn consumption: this cast is rejected
    /// because the Turn's one-cast allowance is spent, and the allowance is
    /// restored when the next committed Match-3 Turn begins. A rejected cast
    /// never consumes the allowance, so this reason is only produced by a cast
    /// the Turn would otherwise have accepted.
    /// </summary>
    CardCastAlreadyUsedThisTurn = 4,

    /// <summary>
    /// The battle the cast was requested against has already ended, so the cast's
    /// state precondition is violated (<c>CARD_RULES.md</c> §3 item 8,
    /// <c>GAME_RULES.md</c> §1.4).
    ///
    /// The guard runs at the action boundary — before §3 item 2's validation is
    /// consulted — so a cast rejected for this reason spends no Power, changes no
    /// state value, advances no <c>Sequence</c>, consumes no RNG, and emits no Battle
    /// Event: it is a gameplay no-op in the sense of §3 item 3. Because the guard
    /// precedes the other checks, this reason is reported for a terminal battle even
    /// when an earlier §3 item 2 check would also have failed, which is the
    /// precedence <c>MATCH3_RULES.md</c> §2.1.4 item 7 fixes for a Swap.
    ///
    /// It is the cast-side carrier of the established <c>STALE_ACTION</c> code
    /// (<c>MATCH3_RULES.md</c> §2.1.4 item 5, <c>SIGNALR_PROTOCOL.md</c> §5 item 3),
    /// not a new code: the wire spelling is stated once in
    /// <see cref="CardCastRejectionCodes.ToContractCode"/>.
    /// </summary>
    StaleAction = 5,
}

/// <summary>
/// The documented wire spelling of a Card cast rejection reason
/// (<c>SIGNALR_PROTOCOL.md</c> §5 item 3).
///
/// §5 item 3 keeps no parallel code list and delegates each action's rejection
/// codes to the domain document that owns the action, which is
/// <c>CARD_RULES.md</c> §3 for <c>CardCast</c> and <c>PetSkillCast</c>. This is the
/// one place the mapping is stated, so the transport does not derive a code by
/// casing an identifier: a mechanical translation of
/// <see cref="CardCastRejectionReason.StaleAction"/> yields <c>STALEACTION</c>,
/// which is a code the protocol never defines.
/// </summary>
public static class CardCastRejectionCodes
{
    /// <summary>
    /// The documented contract code of a rejection reason
    /// (<c>SIGNALR_PROTOCOL.md</c> §5 item 3, <c>CARD_RULES.md</c> §3).
    /// </summary>
    /// <param name="reason">The rejection reason to spell.</param>
    public static string ToContractCode(CardCastRejectionReason reason) => reason switch
    {
        CardCastRejectionReason.CardNotInLoadout => "CARD_NOT_IN_LOADOUT",
        CardCastRejectionReason.InsufficientPower => "INSUFFICIENT_POWER",
        CardCastRejectionReason.InvalidCard => "INVALID_CARD",
        CardCastRejectionReason.CardCastAlreadyUsedThisTurn => "CARD_CAST_ALREADY_USED_THIS_TURN",
        CardCastRejectionReason.StaleAction => "STALE_ACTION",
        _ => throw new ArgumentOutOfRangeException(
            nameof(reason),
            reason,
            "A rejected Card cast names the check that failed (CARD_RULES.md §3); "
            + "None is the accepted result's reason and has no rejection code."),
    };
}
