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
}

/// <summary>
/// The documented wire spelling of a Card cast rejection reason
/// (<c>SIGNALR_PROTOCOL.md</c> §5 item 3).
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
        _ => throw new ArgumentOutOfRangeException(
            nameof(reason),
            reason,
            "A rejected Card cast names the check that failed (CARD_RULES.md §3); "
            + "None is the accepted result's reason and has no rejection code."),
    };
}
