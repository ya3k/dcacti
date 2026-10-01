using GameServer.Domain.Battle;
using GameServer.Domain.Match3;

namespace GameServer.Domain.Cards;

/// <summary>
/// The outcome of executing one requested Card cast (<c>CARD_RULES.md</c> §3).
/// </summary>
public readonly record struct CardCastExecutionResult
{
    private readonly BattleState? _state;
    private readonly IReadOnlyList<BattleEvent>? _events;

    private CardCastExecutionResult(
        bool isAccepted,
        CardCastRejectionReason reason,
        BattleState? state,
        IReadOnlyList<BattleEvent>? events)
    {
        IsAccepted = isAccepted;
        Reason = reason;
        _state = state;
        _events = events;
    }

    /// <summary>
    /// True when the Card cast was accepted and resolved (<c>CARD_RULES.md</c> §3).
    /// </summary>
    public bool IsAccepted { get; }

    /// <summary>
    /// True when the Card cast was rejected and no state changed (<c>CARD_RULES.md</c> §3).
    /// </summary>
    public bool IsRejected => !IsAccepted;

    /// <summary>
    /// The documented rejection reason, or <see cref="CardCastRejectionReason.None"/>
    /// when the Card cast was accepted.
    /// </summary>
    public CardCastRejectionReason Reason { get; }

    /// <summary>
    /// The authoritative state after the resolution.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The Card cast was rejected, so no resulting state exists.
    /// </exception>
    public BattleState State =>
        _state ?? throw new InvalidOperationException(
            "A rejected Card cast produces no state: a rejected action writes nothing (CARD_RULES.md §3).");

    /// <summary>
    /// The ordered Battle Events emitted by the Card cast.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The Card cast was rejected, so no events exist.
    /// </exception>
    public IReadOnlyList<BattleEvent> Events =>
        _events ?? throw new InvalidOperationException(
            "A rejected Card cast produces no events: a rejected action emits nothing (CARD_RULES.md §3).");

    /// <summary>
    /// Creates a rejected Card cast outcome.
    /// </summary>
    public static CardCastExecutionResult Rejected(CardCastRejectionReason reason)
    {
        if (reason == CardCastRejectionReason.None)
        {
            throw new ArgumentOutOfRangeException(nameof(reason), "A rejected Card cast must provide a rejection reason.");
        }

        return new CardCastExecutionResult(false, reason, null, null);
    }

    /// <summary>
    /// Creates an accepted and committed Card cast outcome.
    /// </summary>
    public static CardCastExecutionResult Committed(BattleState state, IReadOnlyList<BattleEvent> events)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(events);

        return new CardCastExecutionResult(true, CardCastRejectionReason.None, state, events);
    }
}
