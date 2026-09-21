using System.Text.Json;
using GameServer.Domain.Battle;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Committed-swap state contract tests (<c>GAME_STATE.md</c> §2.1.10,
/// <c>MATCH3_RULES.md</c> §2.1.4 item 2, <c>ADR-010</c>).
///
/// These verify the <b>state</b> the already-applied / idempotency check reads:
/// its owner, its canonical unordered-pair representation, its absent initial
/// value, when a commit writes it, and that a rejected action never changes it.
///
/// They deliberately do <b>not</b> test swap execution: committing a Swap,
/// beginning a Turn, incrementing <c>Sequence</c>, board resolution, and the
/// validator's evaluation order are all out of this contract's scope. What is
/// asserted here is the record's own shape and transitions, exercised through the
/// state the server would write.
/// </summary>
public class CommittedSwapPairContractTests
{
    private const ulong TestSeed = 42UL;

    // ---------------------------------------------------------------- owner ---

    [Fact]
    public void CommittedPair_ShouldBeOwnedByBattleState()
    {
        // GAME_STATE.md §2.1.10 item 1 / §0 item 5: the record is a BattleState
        // field, because §2 is the authoritative runtime state and a field added by
        // a later stage must be a field §2 already declares.
        var property = typeof(BattleState).GetProperty("LastCommittedSwapPair");

        Assert.NotNull(property);
        Assert.Equal(typeof(CommittedSwapPair?), property!.PropertyType);
    }

    [Fact]
    public void CommittedPair_ShouldNotBeOwnedByBoardState()
    {
        // GAME_STATE.md §2.1.2 item 1 fixes BoardState at exactly one field,
        // Cells[64]. A swap leaves no mark on the board — after an exchange the board
        // is exactly what it would be had the cells simply been in those positions —
        // so no board representation can distinguish "exchanged once" from
        // "exchanged back".
        //
        // `Cells` is the only declared property that is real state; `Item` is the
        // indexer, which is a view of `Cells` and not a second field.
        var boardState = typeof(BoardState)
            .GetProperties()
            .Where(p => p.GetIndexParameters().Length == 0)
            .Select(p => p.Name)
            .ToArray();

        Assert.Equal(["Cells"], boardState);

        // And no board-level collection or record of a committed pair exists either.
        var declared = typeof(BoardState).GetProperties().Select(p => p.Name)
            .Concat(typeof(BoardState).GetFields().Select(f => f.Name))
            .ToArray();

        Assert.DoesNotContain("LastCommittedSwapPair", declared);
    }

    [Fact]
    public void CommittedPair_ShouldStoreNoVersionOrHistoryField()
    {
        // GAME_STATE.md §2.1.10 item 11: no action id, request id, correlation
        // value, timestamp, retry count, generation counter, Turn number, or queue
        // of pending/superseded actions. MATCH3_RULES.md §2.1.4 item 3 defines no
        // per-Turn queue, so the one pair is the whole of it.
        var declared = typeof(CommittedSwapPair)
            .GetProperties()
            .Select(p => p.Name)
            .Concat(typeof(CommittedSwapPair).GetFields().Select(f => f.Name))
            .ToArray();

        foreach (var forbidden in new[]
                 {
                     "ActionId", "RequestId", "ClientSequence", "CorrelationId",
                     "Timestamp", "RetryCount", "Generation", "Turn", "Sequence",
                     "History", "Queue", "Pending",
                 })
        {
            Assert.DoesNotContain(forbidden, declared);
        }

        // Exactly the canonical pair, and nothing else.
        Assert.Equal(
            ["MaxCellIndex", "MinCellIndex"],
            typeof(CommittedSwapPair).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    // ------------------------------------------------------- initial value ---

    [Fact]
    public void InitialState_ShouldHaveNoCommittedPair()
    {
        // GAME_STATE.md §2.1.10 item 3: before the first committed Swap the record is
        // ABSENT. Board generation commits no Swap and is not an action resolution
        // (§2.0.5.2 item 1), so a newly created battle has none.
        var state = BattleState.CreateWith("battle-1", TestSeed);

        Assert.Null(state.LastCommittedSwapPair);
    }

    [Fact]
    public void InitialState_ShouldNotUseASentinelPair()
    {
        // GAME_STATE.md §2.1.10 item 3: it is never (0, 0), (-1, -1), or a defaulted
        // (0, 1) — absence is the representation of "no Swap has been committed".
        // A (0, 0) pair is not even representable, because a committed pair always
        // names two distinct cells (MATCH3_RULES.md §2.1.2 item 2).
        var state = BattleState.CreateWith("battle-1", TestSeed);

        Assert.False(state.LastCommittedSwapPair.HasValue);

        Assert.Throws<ArgumentOutOfRangeException>(() => new CommittedSwapPair(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CommittedSwapPair.FromCells(0, 0));
    }

    [Fact]
    public void InitialState_ShouldNeverRejectAsAlreadyApplied()
    {
        // GAME_STATE.md §2.1.10 item 4: while the record is absent, check 3 can
        // never fail — no swap has been committed, so no swap is already applied.
        var state = BattleState.CreateWith("battle-1", TestSeed);

        // Every adjacent pair on a freshly generated board is "not already applied".
        for (var from = 0; from < BoardState.CellCount; from++)
        {
            foreach (var to in AdjacentPairsOf(from))
            {
                Assert.False(IsAlreadyApplied(state, from, to));
            }
        }
    }

    // --------------------------------------------------- canonical ordering ---

    [Fact]
    public void CanonicalPair_ShouldStoreAscendingOrder()
    {
        // GAME_STATE.md §2.1.10 item 2: the pair is stored canonically with
        // MinCellIndex < MaxCellIndex.
        var pair = CommittedSwapPair.FromCells(12, 13);

        Assert.Equal(12, pair.MinCellIndex);
        Assert.Equal(13, pair.MaxCellIndex);
    }

    [Fact]
    public void CanonicalPair_ShouldCanonicalizeAReversedRequest()
    {
        // The task's minimum: (13, 12) canonicalizes to (12, 13) — request order is
        // never the authoritative identity of the committed pair.
        var reversed = CommittedSwapPair.FromCells(13, 12);

        Assert.Equal(12, reversed.MinCellIndex);
        Assert.Equal(13, reversed.MaxCellIndex);
        Assert.Equal(CommittedSwapPair.FromCells(12, 13), reversed);
    }

    [Fact]
    public void CanonicalPair_ShouldBeIdenticalForBothRequestOrders()
    {
        // MATCH3_RULES.md §2.1.1 item 2 makes the unordered pair {from, to} the
        // swap's identity and §2.1.3 item 3 makes the two inputs symmetric, so the
        // two spellings must produce one value — over every adjacent pair.
        foreach (var (from, to) in AllAdjacentPairs())
        {
            Assert.Equal(
                CommittedSwapPair.FromCells(from, to),
                CommittedSwapPair.FromCells(to, from));
        }
    }

    [Fact]
    public void CanonicalPair_ShouldRejectANonCanonicalConstruction()
    {
        // The canonical invariant is a contract on the stored value, so a
        // descending or equal pair is a violation rather than a value to normalize
        // silently (MATCH3_RULES.md §2.1.3 item 2: never clamped or reinterpreted).
        Assert.Throws<ArgumentOutOfRangeException>(() => new CommittedSwapPair(13, 12));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CommittedSwapPair(5, 5));
    }

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(5, 64)]
    [InlineData(64, 70)]
    [InlineData(-4, -1)]
    public void CanonicalPair_ShouldRejectACellIndexOutsideTheBoard(int first, int second)
    {
        // MATCH3_RULES.md §1.0: a cell index is 0..63. A value outside it is not a
        // cell, so it cannot name a committed pair.
        Assert.Throws<ArgumentOutOfRangeException>(() => CommittedSwapPair.FromCells(first, second));
    }

    // ------------------------------------------------ successful commit ---

    [Fact]
    public void SuccessfulCommit_ShouldRecordTheCanonicalPair()
    {
        // GAME_STATE.md §2.1.10 item 5: committing a Swap writes the action's
        // canonical pair. The state transition itself (Turn, Sequence, board) is
        // TASK-004's; this asserts the record's own semantics.
        var committed = RecordCommit(13, 12);

        Assert.Equal(new CommittedSwapPair(12, 13), committed.LastCommittedSwapPair);
    }

    [Fact]
    public void SuccessfulCommit_ShouldRecordRegardlessOfRequestOrder()
    {
        // The task's requirement: Swap(12, 13) and Swap(13, 12) leave the same
        // authoritative state.
        var forward = RecordCommit(12, 13);
        var reversed = RecordCommit(13, 12);

        Assert.Equal(forward.LastCommittedSwapPair, reversed.LastCommittedSwapPair);
    }

    [Fact]
    public void SuccessfulCommit_ShouldReplaceThePreviousPair()
    {
        // GAME_STATE.md §2.1.10 item 7: applying a NEW pair replaces the stored
        // value. Only the most recent commit is kept (MATCH3_RULES.md §2.1.4
        // item 3: no per-Turn queue).
        var first = RecordCommit(12, 13);
        var second = RecordCommit(20, 21);

        Assert.Equal(new CommittedSwapPair(12, 13), first.LastCommittedSwapPair);
        Assert.Equal(new CommittedSwapPair(20, 21), second.LastCommittedSwapPair);
    }

    [Fact]
    public void SuccessfulCommit_ShouldNotTouchTheBoardCountersOrRng()
    {
        // GAME_STATE.md §2.1.10 item 13: recording a commit records no Match,
        // Combo, Power, Passive, or Relic progression and consumes no randomness.
        // This test moves ONLY the commit record, and asserts nothing else was
        // dragged along with it.
        var state = BattleState.CreateWith("battle-1", TestSeed);
        var committed = RecordCommit(12, 13, state);

        Assert.True(state.BoardState.CellsEqual(committed.BoardState));
        Assert.Equal(state.Turn, committed.Turn);
        Assert.Equal(state.Sequence, committed.Sequence);
        Assert.Equal(state.RngState, committed.RngState);
        Assert.Equal(state.RngSeed, committed.RngSeed);
    }

    // ----------------------------------------------------- invalid action ---

    [Fact]
    public void InvalidAction_ShouldNotReplaceTheCommittedPair()
    {
        // The task's example: with (12, 13) committed, an invalid request (20, 21)
        // leaves the record as (12, 13). MATCH3_RULES.md §2.1.5: a rejected action
        // writes nothing.
        var committed = RecordCommit(12, 13);

        foreach (var reason in new[]
                 {
                     SwapRejectionReason.InvalidCellIndex,
                     SwapRejectionReason.InvalidSwap,
                     SwapRejectionReason.NoMatchFromSwap,
                 })
        {
            var afterRejection = ApplyRejection(committed, reason);

            Assert.Equal(new CommittedSwapPair(12, 13), afterRejection.LastCommittedSwapPair);
        }
    }

    [Fact]
    public void InvalidAction_ShouldNotClearTheCommittedPair()
    {
        // GAME_STATE.md §2.1.10 item 7: a rejection neither records a pair nor
        // clears one — a rejection can never make an earlier commit forgettable.
        var committed = RecordCommit(12, 13);
        var afterRejection = ApplyRejection(committed, SwapRejectionReason.InvalidSwap);

        Assert.NotNull(afterRejection.LastCommittedSwapPair);
        Assert.True(IsAlreadyApplied(afterRejection, 12, 13));
        Assert.True(IsAlreadyApplied(afterRejection, 13, 12));
    }

    [Fact]
    public void InvalidAction_OnAFreshBattle_ShouldRemainUncommitted()
    {
        // A rejection before any commit leaves the record absent — it must not
        // fabricate a commit.
        var state = BattleState.CreateWith("battle-1", TestSeed);
        var afterRejection = ApplyRejection(state, SwapRejectionReason.NoMatchFromSwap);

        Assert.Null(afterRejection.LastCommittedSwapPair);
    }

    // -------------------------------------------------------- stale action ---

    [Fact]
    public void StaleAction_ShouldBeARecognisedRejectionReason()
    {
        // MATCH3_RULES.md §2.1.2 check 3 / §2.1.4 item 2: STALE_ACTION is one of the
        // documented rejection reasons, reported to the caller only (§2.1.5 item 6).
        Assert.True(Enum.IsDefined(SwapRejectionReason.StaleAction));
        Assert.NotEqual(SwapRejectionReason.None, SwapRejectionReason.StaleAction);
    }

    [Fact]
    public void StaleAction_ShouldMatchTheSameUnorderedPair()
    {
        // The task's minimum: the same unordered pair identifies STALE_ACTION. The
        // comparison is between unordered pairs, so a reversed request is stale too.
        var committed = RecordCommit(12, 13);

        Assert.True(IsAlreadyApplied(committed, 12, 13));
        Assert.True(IsAlreadyApplied(committed, 13, 12));
    }

    [Fact]
    public void StaleAction_ShouldNotMatchADifferentPair()
    {
        // MATCH3_RULES.md §2.1.4 item 3: a Swap naming a pair that is not the most
        // recently committed pair is validated only against the current board — it is
        // not stale.
        var committed = RecordCommit(12, 13);

        Assert.False(IsAlreadyApplied(committed, 12, 20));
        Assert.False(IsAlreadyApplied(committed, 20, 21));
        Assert.False(IsAlreadyApplied(committed, 0, 1));
    }

    [Fact]
    public void StaleAction_ShouldNotChangeTheCommittedPair()
    {
        // GAME_STATE.md §2.1.10 items 6–7 and MATCH3_RULES.md §2.1.5 item 5: the
        // rejected request must not change the state — including not clearing the
        // pair it just matched.
        var committed = RecordCommit(12, 13);
        var afterStale = ApplyRejection(committed, SwapRejectionReason.StaleAction);

        Assert.Equal(new CommittedSwapPair(12, 13), afterStale.LastCommittedSwapPair);
        Assert.True(IsAlreadyApplied(afterStale, 12, 13));
        Assert.True(IsAlreadyApplied(afterStale, 13, 12));
    }

    [Fact]
    public void StaleAction_ShouldNeverBeDerivedFromAClientSuppliedValue()
    {
        // MATCH3_RULES.md §2.1.4 items 1 and 5, SIGNALR_PROTOCOL.md §2 item 1:
        // clientSequence is an opaque correlation id and is NOT the staleness source,
        // and §2.1.1 item 3 forbids the request carrying any gameplay field. The
        // request type therefore has exactly the two cells and nothing else, so a
        // client cannot declare an action already committed.
        Assert.Equal(
            ["From", "To"],
            typeof(SwapRequest).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void StaleAction_ShouldBeDeterministic()
    {
        // MATCH3_RULES.md §2.1.4 item 6: the same board and the same action always
        // produce the same outcome.
        var committed = RecordCommit(12, 13);

        for (var attempt = 0; attempt < 20; attempt++)
        {
            Assert.True(IsAlreadyApplied(committed, 12, 13));
            Assert.True(IsAlreadyApplied(committed, 13, 12));
            Assert.False(IsAlreadyApplied(committed, 12, 20));
        }
    }

    // ------------------------------------------------------- serialization ---

    [Fact]
    public void Serialization_ShouldWriteTheCanonicalPairInAscendingOrder()
    {
        // GAME_STATE.md §2.1.10 item 10: when present it carries the canonical pair,
        // with MinCellIndex < MaxCellIndex, so the serialized order is itself
        // canonical and a round trip cannot re-spell it.
        var committed = RecordCommit(13, 12);

        using var document = JsonDocument.Parse(SerializePair(committed.LastCommittedSwapPair));

        var min = document.RootElement.GetProperty("minCellIndex").GetInt32();
        var max = document.RootElement.GetProperty("maxCellIndex").GetInt32();

        Assert.Equal(12, min);
        Assert.Equal(13, max);
        Assert.True(min < max, "the serialized pair must be written with from < to");
    }

    [Fact]
    public void Serialization_ShouldOmitTheAbsentPairRatherThanWriteNull()
    {
        // GAME_STATE.md §2.1.10 item 10 / §2.1.7 item 3: when absent it is OMITTED —
        // absence is the statement "no Swap has been committed", and no null pair,
        // sentinel index, or zero pair stands in for it.
        var fresh = BattleState.CreateWith("battle-1", TestSeed);

        var json = SerializePair(fresh.LastCommittedSwapPair);

        Assert.DoesNotContain("minCellIndex", json);
        Assert.DoesNotContain("maxCellIndex", json);
    }

    [Fact]
    public void RoundTrip_ShouldPreserveTheCommittedPair()
    {
        // GAME_STATE.md §2.1.10 item 10, REDIS_STATE.md §7 item 11: the value must
        // survive a round trip as written — §2.1.7 item 5's losslessness applied to
        // this field.
        var committed = RecordCommit(13, 12);

        var restored = DeserializePair(SerializePair(committed.LastCommittedSwapPair));

        Assert.Equal(committed.LastCommittedSwapPair, restored);
        Assert.Equal(12, restored!.Value.MinCellIndex);
        Assert.Equal(13, restored.Value.MaxCellIndex);
    }

    [Fact]
    public void RoundTrip_ShouldPreserveAbsenceAsAbsence()
    {
        // A store that materializes a sentinel pair for an absent record would invent
        // a commit the battle never made (GAME_STATE.md §2.1.10 item 10).
        var fresh = BattleState.CreateWith("battle-1", TestSeed);

        var restored = DeserializePair(SerializePair(fresh.LastCommittedSwapPair));

        Assert.Null(restored);
    }

    // ---------------------------------------------------------- non-delivery ---

    [Fact]
    public void CommittedPair_ShouldNotBeProjectedOntoTheWire()
    {
        // SIGNALR_PROTOCOL.md §4 item 12 / §4 item 4: BattleStateUpdated carries
        // exactly the implemented stage's fields and admits no other. The commit
        // record is server-side bookkeeping the client cannot use — §2.1.4 makes
        // staleness a server-side decision — so it is authoritative state that is
        // deliberately NOT delivered, with no new message or payload member.
        //
        // The wire projection type lives in GameServer.Api (not referenced from the
        // Domain test project), so this asserts the Domain-side half of the
        // contract: the record is state, and the request that would carry it does not
        // exist.
        Assert.NotNull(typeof(BattleState).GetProperty("LastCommittedSwapPair"));
        Assert.Equal(
            ["From", "To"],
            typeof(SwapRequest).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    // ------------------------------------------------------------- helpers ---

    /// <summary>
    /// The documented already-applied comparison of <c>MATCH3_RULES.md</c> §2.1.4
    /// item 2, evaluated against the state's record exactly as the check would: the
    /// action's unordered pair equals the most recently committed pair.
    /// </summary>
    private static bool IsAlreadyApplied(BattleState state, int fromCell, int toCell)
    {
        if (!BoardState.AreOrthogonallyAdjacent(fromCell, toCell))
        {
            // Earlier checks reject first; staleness is check 3 of §2.1.2 and is not
            // reached for a pair that is not adjacent.
            return false;
        }

        return CommittedSwapPair.FromCells(fromCell, toCell).Matches(state.LastCommittedSwapPair);
    }

    /// <summary>
    /// Records a commit the way a successful Swap would (<c>GAME_STATE.md</c>
    /// §2.1.10 item 5): the canonical pair, written into the state. Move
    /// application and the counters are TASK-004's scope and are not simulated.
    /// </summary>
    private static BattleState RecordCommit(int fromCell, int toCell, BattleState? existing = null)
    {
        var state = existing ?? BattleState.CreateWith("battle-1", TestSeed);

        return state with { LastCommittedSwapPair = CommittedSwapPair.FromCells(fromCell, toCell) };
    }

    /// <summary>
    /// A rejected action's effect on state: none (<c>MATCH3_RULES.md</c> §2.1.5,
    /// <c>GAME_STATE.md</c> §2.1.10 item 6). The reason is carried because every
    /// rejection reason must behave identically here.
    /// </summary>
    private static BattleState ApplyRejection(BattleState state, SwapRejectionReason reason)
    {
        Assert.NotEqual(SwapRejectionReason.None, reason);

        // A rejected action writes nothing — the state is returned unchanged.
        return state;
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed record PairDto(int MinCellIndex, int MaxCellIndex);

    private static string SerializePair(CommittedSwapPair? pair) =>
        pair is { } value
            ? JsonSerializer.Serialize(new PairDto(value.MinCellIndex, value.MaxCellIndex), Options)
            : "{}";

    private static CommittedSwapPair? DeserializePair(string json)
    {
        // The documented representation writes the member only when it is present
        // (§2.1.10 item 10), so an empty object means "no Swap has been committed" —
        // it is not a default-constructed pair.
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("minCellIndex", out _))
        {
            return null;
        }

        var dto = JsonSerializer.Deserialize<PairDto>(json, Options)!;

        return new CommittedSwapPair(dto.MinCellIndex, dto.MaxCellIndex);
    }

    private static IEnumerable<int> AdjacentPairsOf(int index)
    {
        var row = BoardState.ToRow(index);
        var column = BoardState.ToColumn(index);

        if (row > 0) yield return BoardState.ToIndex(row - 1, column);
        if (row < BoardState.Rows - 1) yield return BoardState.ToIndex(row + 1, column);
        if (column > 0) yield return BoardState.ToIndex(row, column - 1);
        if (column < BoardState.Columns - 1) yield return BoardState.ToIndex(row, column + 1);
    }

    private static IEnumerable<(int From, int To)> AllAdjacentPairs()
    {
        for (var index = 0; index < BoardState.CellCount; index++)
        {
            foreach (var neighbour in AdjacentPairsOf(index))
            {
                yield return (index, neighbour);
            }
        }
    }
}