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
    /// The requested Card is not in the active Pet's equipped cards for this
    /// battle (<c>CARD_RULES.md</c> §3 item 2).
    ///
    /// <b>It is the Player's check only.</b> There is no Boss loadout and no
    /// ownership row for a Boss Card (<c>BOSS_RULES.md</c> §4.2 items 1, 8), so a
    /// Boss-actor cast performs no loadout lookup and this reason is never
    /// produced for one; a Boss cast that is not the Boss's own
    /// <see cref="CardCategory.BossCard"/> definition reports
    /// <see cref="InvalidCard"/> instead (<c>CARD_RULES.md</c> §3 item 9).
    /// </summary>
    CardNotInLoadout = 1,

    /// <summary>
    /// The acting side's current Power is less than the Card's
    /// <c>EffectiveCardCost</c> (<c>CARD_RULES.md</c> §3 item 2, §3.6).
    ///
    /// <b>It is reported for both actors, against the acting side's own
    /// resource.</b> A Player cast compares <c>PetState.Power</c>; a Boss-actor
    /// cast compares <c>BossState.Power</c> (<c>BOSS_RULES.md</c> §4.2 item 3,
    /// <c>GAME_STATE.md</c> §2.4.3 item 4). The two resources are separate and
    /// neither is read on the other's behalf (<c>BOSS_RULES.md</c> §4.1 item 5,
    /// <c>ADR-029</c> §2.1). The cost compared is the composed
    /// <c>EffectiveCardCost</c> in both cases — with the Player's active modifier
    /// set and, for the Boss, an empty one (<c>CARD_RULES.md</c> §3 item 9).
    /// </summary>
    InsufficientPower = 2,

    /// <summary>
    /// The Card definition is invalid, unknown, or not a category the acting side
    /// may cast (<c>CARD_RULES.md</c> §1, §1.1, §3).
    ///
    /// <b>It carries the actor/category gate for both actors.</b> A Player-actor
    /// cast accepts only <see cref="CardCategory.Basic"/> and
    /// <see cref="CardCategory.PetSkill"/> (§1, §3, §4); a Boss-actor cast accepts
    /// only the Boss's own <see cref="CardCategory.BossCard"/> (§1.1, §3 item 9,
    /// <c>BOSS_RULES.md</c> §4.2 items 2 and 8). Each side therefore rejects the
    /// other's categories with this reason, and no rejection reason is added for
    /// the Boss side: the wire vocabulary is unchanged.
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
    ///
    /// <b>It is the Player's allowance only.</b> §3 item 9 keeps item 6's
    /// allowance out of the shared contract: a Boss-actor cast neither reads nor
    /// spends it, so this reason is never produced for one. The Boss's cast
    /// cadence is bounded by its own Power, its cooldown and its action policy
    /// (<c>CARD_RULES.md</c> §1.1 item 4, <c>BOSS_RULES.md</c> §4.3).
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
