namespace GameServer.Domain.Match3;

/// <summary>
/// The canonical record of a committed Swap — the unordered pair of §1.0 cell
/// indices exchanged by the most recent committed Swap
/// (<c>GAME_STATE.md</c> §2.1.10).
///
/// <code>
/// LastCommittedSwapPair?
/// └── (MinCellIndex, MaxCellIndex)    both §1.0 indices, MinCellIndex &lt; MaxCellIndex
/// </code>
///
/// <b>Canonical ordering.</b> <c>MATCH3_RULES.md</c> §2.1.1 item 2 makes the
/// unordered pair <c>{from, to}</c> the swap's identity and states the order of
/// <c>from</c> and <c>to</c> has no gameplay meaning, and §2.1.3 item 3 makes
/// the two inputs symmetric. This type therefore stores the pair with its two
/// indices in <b>ascending</b> order, so a request's argument order can never
/// make the recorded value ambiguous and two spellings of one pair always
/// compare equal:
///
/// <code>
/// Swap(12, 13)  →  LastCommittedSwapPair { MinCellIndex = 12, MaxCellIndex = 13 }
/// Swap(13, 12)  →  LastCommittedSwapPair { MinCellIndex = 12, MaxCellIndex = 13 }
/// </code>
///
/// Request order is never the authoritative identity of the committed pair
/// (<c>GAME_STATE.md</c> §2.1.10 item 2). Use <see cref="FromCells"/> to build
/// the canonical value from a request's two cells; the primary constructor is
/// public so a stored value can be reconstructed by a serializer, and it
/// enforces the same canonical invariant.
///
/// <b>Absence, not a sentinel.</b> "No Swap has been committed" is expressed by
/// the <i>absence</i> of this value — a <c>null</c>
/// <see cref="Battle.BattleState.LastCommittedSwapPair"/> — never by a
/// stand-in pair (<c>GAME_STATE.md</c> §2.1.10 item 3). There is deliberately no
/// <c>None</c> member, no <c>(0, 0)</c>, and no <c>(-1, -1)</c>: <c>(0, 0)</c>
/// is not even representable here, because a committed pair always names two
/// <i>distinct</i> cells (<c>MATCH3_RULES.md</c> §2.1.2 item 2 — a swap of a
/// cell with itself is not a Swap).
///
/// <b>Not a version.</b> This is not a <c>Sequence</c>, a Turn number, a
/// request id, a correlation value, or a retry/generation counter
/// (<c>GAME_STATE.md</c> §2.1.10 item 11). It records which pair the board's
/// current arrangement came from and nothing else, and it is never used for
/// concurrency control — <c>Sequence</c> remains the only concurrency token
/// (<c>GAME_STATE.md</c> §5 item 3, <c>REDIS_STATE.md</c> §4 item 6).
///
/// <b>Not delivered to the client.</b> The value is server-side bookkeeping
/// inside authoritative state: it is never a member of the
/// <c>BattleStateUpdated</c> payload and is never carried by a Swap request
/// (<c>SIGNALR_PROTOCOL.md</c> §4 item 12, <c>MATCH3_RULES.md</c> §2.1.1
/// item 3). The client must never author, adjust, or recompute it
/// (<c>GAME_RULES.md</c> §18, <c>ADR-001</c>).
/// </summary>
/// <param name="MinCellIndex">
/// The lower of the two exchanged §1.0 cell indices. Must be in <c>0..63</c>.
/// </param>
/// <param name="MaxCellIndex">
/// The higher of the two exchanged §1.0 cell indices. Must be in <c>0..63</c>
/// and strictly greater than <paramref name="MinCellIndex"/>.
/// </param>
/// <exception cref="ArgumentOutOfRangeException">
/// Either index is outside <c>0..63</c>, or the two are equal or not in
/// ascending order. Those are contract violations — the canonical form of a
/// committed pair, not a rejected request — so they are rejected rather than
/// represented (<c>MATCH3_RULES.md</c> §2.1.3 item 2: never clamped, folded, or
/// reinterpreted).
/// </exception>
public readonly record struct CommittedSwapPair
{
    /// <summary>
    /// The lower of the two exchanged §1.0 cell indices (<c>0..63</c>).
    /// </summary>
    public int MinCellIndex { get; }

    /// <summary>
    /// The higher of the two exchanged §1.0 cell indices (<c>0..63</c>),
    /// always strictly greater than <see cref="MinCellIndex"/>.
    /// </summary>
    public int MaxCellIndex { get; }

    /// <summary>
    /// The sequence number at which this swap was committed (ADR-025).
    /// </summary>
    public int Sequence { get; }

    /// <summary>
    /// The client-generated action idempotency identifier (ADR-025).
    /// </summary>
    public string? ActionId { get; }

    public static CommittedSwapPair FromCells(int firstIndex, int secondIndex) =>
        FromCells(firstIndex, secondIndex, 0, null);

    public static CommittedSwapPair FromCells(int firstIndex, int secondIndex, int sequence, string? actionId = null)
    {
        if (!IsCellIndex(firstIndex) || !IsCellIndex(secondIndex))
        {
            throw new ArgumentOutOfRangeException(
                nameof(firstIndex),
                firstIndex,
                $"A committed Swap names two §1.0 cell indices in 0..{BoardState.CellCount - 1} "
                + $"(MATCH3_RULES.md §1.0); received ({firstIndex}, {secondIndex}).");
        }

        if (firstIndex == secondIndex)
        {
            throw new ArgumentOutOfRangeException(
                nameof(secondIndex),
                secondIndex,
                "A committed Swap exchanges two distinct cells; a swap of a cell with itself is "
                + "not a Swap (MATCH3_RULES.md §2.1.2 item 2).");
        }

        return firstIndex < secondIndex
            ? new CommittedSwapPair(firstIndex, secondIndex, sequence, actionId)
            : new CommittedSwapPair(secondIndex, firstIndex, sequence, actionId);
    }

    public CommittedSwapPair(int MinCellIndex, int MaxCellIndex)
        : this(MinCellIndex, MaxCellIndex, 0, null)
    {
    }

    public CommittedSwapPair(int MinCellIndex, int MaxCellIndex, int Sequence, string? ActionId = null)
    {
        if (!IsCellIndex(MinCellIndex) || !IsCellIndex(MaxCellIndex))
        {
            throw new ArgumentOutOfRangeException(
                nameof(MinCellIndex),
                MinCellIndex,
                $"A committed Swap names two §1.0 cell indices in 0..{BoardState.CellCount - 1} "
                + $"(MATCH3_RULES.md §1.0); received ({MinCellIndex}, {MaxCellIndex}).");
        }

        if (MinCellIndex >= MaxCellIndex)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxCellIndex),
                MaxCellIndex,
                "A committed Swap's pair is stored canonically with MinCellIndex < MaxCellIndex "
                + $"(GAME_STATE.md §2.1.10 item 2); received ({MinCellIndex}, {MaxCellIndex}).");
        }

        this.MinCellIndex = MinCellIndex;
        this.MaxCellIndex = MaxCellIndex;
        this.Sequence = Sequence;
        this.ActionId = ActionId;
    }

    private static bool IsCellIndex(int index) => index >= 0 && index < BoardState.CellCount;

    public bool Matches(CommittedSwapPair? candidate) =>
        candidate is { } other && other.MinCellIndex == MinCellIndex && other.MaxCellIndex == MaxCellIndex;

    public bool MatchesPair(int firstIndex, int secondIndex)
    {
        var min = Math.Min(firstIndex, secondIndex);
        var max = Math.Max(firstIndex, secondIndex);
        return min == MinCellIndex && max == MaxCellIndex;
    }

    public bool Equals(CommittedSwapPair other) =>
        MinCellIndex == other.MinCellIndex && MaxCellIndex == other.MaxCellIndex;

    public override int GetHashCode() => HashCode.Combine(MinCellIndex, MaxCellIndex);
}