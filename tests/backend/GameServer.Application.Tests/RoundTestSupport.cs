using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Match3;

namespace GameServer.Application.Tests;

/// <summary>
/// A pinned board plus the one committed Player Swap that acts on it, together
/// with the resolved cascade that Swap produces — the fixture an atomic-round
/// test pins before it calls <see cref="BattleStateService.ExecuteSwapAsync"/>.
/// </summary>
/// <param name="Board">The board the Player acts on (<c>BattleState.BoardState</c>).</param>
/// <param name="Swap">
/// The one adjacent pair that is legal on <paramref name="Board"/>, produces a Match,
/// and clears Gems only of <paramref name="MatchType"/>.
/// </param>
/// <param name="MatchType">The Gem type the Swap's resolved cascade clears.</param>
/// <param name="Resolution">The cascade the Swap produces (derived, not re-detected).</param>
/// <param name="Resources">The resources that cascade generates (<c>GAME_RULES.md</c> §17 step 12).</param>
internal sealed record PinnedPlayerRound(
    BoardState Board,
    SwapRequest Swap,
    GemType MatchType,
    CascadeResolver.CascadeResult Resolution,
    ResourceGeneration Resources);

/// <summary>
/// Test fixtures and event-list readings for the atomic-round contract
/// (<c>ADR-026</c>).
///
/// <b>Why a pinned board is needed.</b> Under <c>ADR-026</c> D1 attack damage is
/// gated on a confirmed ATK-Gem match, so a test that wants the Player's attack to
/// qualify must control <em>which Gem type the committed Swap clears</em> — a
/// board drawn from the battle's own RNG cannot offer that. The fixtures below
/// build such a board by construction and verify their own preconditions against
/// the real pipeline, so a fixture that stopped producing the promised Match
/// fails loudly instead of silently weakening a test.
///
/// <b>No gameplay rule lives here.</b> Every value is produced by the Domain
/// stages the production pipeline uses (<see cref="MatchDetector"/>,
/// <see cref="CascadeResolver"/>, <see cref="ResourceGenerator"/>) — this type
/// decides no rule and reimplements no step.
/// </summary>
internal static class RoundTestSupport
{
    /// <summary>
    /// A board with no pre-existing Match on which exactly one adjacent Swap
    /// produces a Match of <paramref name="matchType"/>, and whose resolved cascade
    /// clears Gems of <paramref name="matchType"/> only.
    ///
    /// <b>It is the fixture the ATK gate is read against.</b> For
    /// <see cref="GemType.Atk"/> the cascade clears at least one ATK Gem, so the
    /// acting side produces a qualifying attack; for every other type it clears
    /// none, so no attack may occur (<c>ADR-026</c> D1, Scope A). The search is
    /// deterministic — the first accepted placement in ascending cell order — so
    /// the same <paramref name="rngState"/> always yields the same fixture.
    /// </summary>
    /// <param name="matchType">The Gem type the fixture's Swap must clear.</param>
    /// <param name="rngState">
    /// The battle's authoritative PRNG state (<c>GAME_STATE.md</c> §2.6.2), which
    /// the fixture resolves its cascade against exactly as the production path does.
    /// </param>
    /// <param name="excluded">
    /// A pair the fixture must not return — normally the battle's
    /// <c>LastCommittedSwapPair</c>, because a Swap already recorded as committed is
    /// rejected as already applied (<c>MATCH3_RULES.md</c> §2.1.4 item 2). A
    /// multi-round scenario passes it so every round's Swap is a new logical action.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// No placement satisfies the fixture's preconditions. It is a fixture defect,
    /// never a production failure.
    /// </exception>
    public static PinnedPlayerRound PlayerMatch(
        GemType matchType,
        RngState rngState,
        CommittedSwapPair? excluded = null) =>
        TryPlayerMatch(matchType, rngState, excluded)
        ?? throw new InvalidOperationException(
            $"ADR-026 fixture for '{matchType}' could not be constructed. This is a test-fixture "
            + "defect: the board builder no longer produces a single-type Match opportunity.");

    /// <summary>
    /// The fixture for a played round, together with the RNG stream the battle must be
    /// holding for it to hold: the first candidate stream, in a fixed deterministic
    /// order, that satisfies <see cref="PlayerMatch"/>'s preconditions.
    ///
    /// <b>Why a stream is chosen rather than assumed.</b> The strict fixture only
    /// accepts a stream whose cascades clear one Gem type, so a scenario that plays
    /// several rounds from one evolving stream may reach a round whose stream admits
    /// no such arrangement. The scenario then re-pins the stream — the
    /// <c>RngState</c> is <b>fixture state</b> (<c>GAME_STATE.md</c> §2.6.2), never an
    /// asserted value, and no gameplay rule is derived from it (<c>ADR-009</c>): the
    /// scenario decides which stream to play and then asserts the documented
    /// contracts on that round.
    /// </summary>
    /// <param name="matchType">The Gem type the fixture's Swap must clear.</param>
    /// <param name="starting">
    /// The stream the scenario would otherwise continue — the search starts there, so a
    /// scenario whose own stream already satisfies the fixture keeps it.
    /// </param>
    /// <param name="excluded">A pair the fixture must not return (<c>MATCH3_RULES.md</c> §2.1.4 item 2).</param>
    /// <exception cref="InvalidOperationException">
    /// No candidate stream satisfies the fixture. It is a fixture defect, never a
    /// production failure.
    /// </exception>
    public static (RngState RngState, PinnedPlayerRound Pinned) PinnedForStream(
        GemType matchType,
        RngState starting,
        CommittedSwapPair? excluded = null)
    {
        for (ulong offset = 0; offset < 256; offset++)
        {
            var candidate = new RngState(starting.State + offset, starting.Increment);

            if (TryPlayerMatch(matchType, candidate, excluded) is { } pinned)
            {
                return (candidate, pinned);
            }
        }

        throw new InvalidOperationException(
            $"ADR-026 fixture for '{matchType}' could not be constructed against any of the 256 "
            + "candidate streams. This is a test-fixture defect, never a production failure.");
    }

    private static PinnedPlayerRound? TryPlayerMatch(
        GemType matchType,
        RngState rngState,
        CommittedSwapPair? excluded)
    {
        for (var row = 0; row + 1 < BoardState.Rows; row++)
        {
            for (var column = 0; column + 2 < BoardState.Columns; column++)
            {
                var board = WithSingleMatchOpportunity(matchType, row, column);

                if (MatchDetector.Detect(board).Count != 0)
                {
                    continue;
                }

                var from = BoardState.ToIndex(row, column + 2);
                var to = BoardState.ToIndex(row + 1, column + 2);

                if (excluded is { } alreadyCommitted
                    && alreadyCommitted.MatchesPair(from, to))
                {
                    continue;
                }

                var swap = new SwapRequest(from, to);

                var resolution = CascadeResolver.Resolve(
                    board.WithSwapped(from, to),
                    new Pcg32(rngState.State, rngState.Increment));

                if (resolution.Passes.Count == 0)
                {
                    continue;
                }

                var resources = ResourceGenerator.Generate(resolution);
                var cleared = resources.ClearedGemCounts;

                // The gate the fixture exists to control: the cascade clears the
                // requested type and, when that type is not ATK, clears no ATK Gem at
                // all — so the acting side cannot be a qualifying attacker.
                if (cleared.For(matchType) == 0)
                {
                    continue;
                }

                if (matchType != GemType.Atk && cleared.Atk != 0)
                {
                    continue;
                }

                // Only the requested type is cleared, so an assertion about "the
                // Swap's Gems" is an assertion about one Gem type.
                if (cleared.Total != cleared.For(matchType))
                {
                    continue;
                }

                return new PinnedPlayerRound(board, swap, matchType, resolution, resources);
            }
        }

        return null;
    }

    /// <summary>
    /// The board the fixture's Swap acts on: a three-way Gem cycle that contains no
    /// Match, with two <paramref name="matchType"/> Gems placed on
    /// <paramref name="row"/> and one immediately below the third column, so
    /// swapping the third cell down completes a three-in-a-row
    /// (<c>MATCH3_RULES.md</c> §2 item 3).
    /// </summary>
    private static BoardState WithSingleMatchOpportunity(GemType matchType, int row, int column)
    {
        // MATCH3_RULES.md §1.1's four Gem types in a repeating cycle: no three
        // consecutive cells of one type occur horizontally or vertically, so the
        // board starts stable.
        GemType[] cycle = [GemType.Atk, GemType.Def, GemType.Power];
        GemType[] filler = [GemType.Def, GemType.Power, GemType.Hp];

        var cells = new GemType[BoardState.CellCount];

        for (var r = 0; r < BoardState.Rows; r++)
        {
            for (var c = 0; c < BoardState.Columns; c++)
            {
                cells[BoardState.ToIndex(r, c)] = cycle[(r + c) % cycle.Length];
            }
        }

        // The first two cells of the would-be Match, deliberately not yet a Match
        // (the third column is not of this type), and the completing Gem one row
        // below it.
        cells[BoardState.ToIndex(row, column)] = matchType;
        cells[BoardState.ToIndex(row, column + 1)] = matchType;
        cells[BoardState.ToIndex(row + 1, column + 2)] = matchType;

        // Keep the surrounding cells off the match type so the placement itself
        // introduces no Match and the arriving Gem completes exactly one.
        foreach (var (r, c) in new[]
        {
            (row, column + 2),
            (row, column - 1),
            (row + 1, column + 1),
            (row + 1, column + 3),
        })
        {
            if (r < 0 || r >= BoardState.Rows || c < 0 || c >= BoardState.Columns)
            {
                continue;
            }

            var index = BoardState.ToIndex(r, c);

            if (cells[index] == matchType)
            {
                cells[index] = filler[(r + c) % filler.Length];
            }
        }

        return BoardState.FromCells(cells);
    }

    /// <summary>
    /// The Match-3 board events of the <b>Player action phase</b> — the first
    /// contiguous run of board events in the committed round's own event list
    /// (<c>GAME_EVENTS.md</c> §1.1).
    /// </summary>
    /// <remarks>
    /// Read from the delivered list, not re-derived: the round's list is
    /// <c>[SwapStarted(player), SwapResolved(player), &lt;player board run&gt;, …,
    /// SwapStarted(boss), SwapResolved(boss), &lt;boss board run&gt;, …]</c>
    /// (<c>GAME_EVENTS.md</c> §1, <c>SIGNALR_PROTOCOL.md</c> §3.2.26–§3.2.27), so
    /// the action's boundary pair is <b>not</b> a board event
    /// (<see cref="IsBoardEvent"/>) and the player's cycle is the first run, not
    /// the list's prefix. Re-deriving it from <c>BattleEventBuilder</c> would
    /// answer a different list than the one the round delivered.
    /// </remarks>
    public static IReadOnlyList<BattleEvent> PlayerBoardEvents(SwapExecutionResult result)
    {
        var runs = BoardEventRuns(result);

        return runs.Count > 0 ? runs[0] : [];
    }

    /// <summary>
    /// The Match-3 board events of the <b>Boss response phase</b> — the second
    /// contiguous run of board events in the committed round's own event list
    /// (<c>GAME_EVENTS.md</c> §1, §1.1; <c>ADR-026</c> §2).
    /// </summary>
    /// <param name="result">The committed round's result.</param>
    /// <remarks>
    /// The Player's board cycle is the list's first run, and the only other board
    /// events in a committed round are the Boss's, so the second run is the Boss's
    /// cycle. Nothing is re-detected: the events are the resolution's own output,
    /// read by position.
    /// </remarks>
    public static IReadOnlyList<BattleEvent> BossBoardEvents(SwapExecutionResult result)
    {
        var runs = BoardEventRuns(result);

        return runs.Count > 1 ? runs[1] : [];
    }

    /// <summary>
    /// The round's contiguous runs of board events, in delivered order — the
    /// Player action's cycle first, the Boss response phase's second
    /// (<c>GAME_EVENTS.md</c> §1.1).
    /// </summary>
    /// <remarks>
    /// A run is maximal: it opens at the first board event that follows a
    /// non-board event (or the list's start) and closes at the first board event
    /// that is followed by a non-board one. The events between two runs — the
    /// action's boundary pair, the Passive/Relic/Power stage reports, the damage
    /// reports, and the terminal outcome — are not board events and are skipped,
    /// never reclassified: the classification stays the four-name set
    /// <see cref="IsBoardEvent"/> has always used, and admitting
    /// <c>SwapStarted</c>/<c>SwapResolved</c> does not add a fifth name to it.
    /// </remarks>
    private static IReadOnlyList<IReadOnlyList<BattleEvent>> BoardEventRuns(SwapExecutionResult result)
    {
        var events = result.Events;
        var runs = new List<IReadOnlyList<BattleEvent>>();
        var index = 0;

        while (index < events.Count)
        {
            if (!IsBoardEvent(events[index]))
            {
                index++;
                continue;
            }

            var start = index;

            while (index < events.Count && IsBoardEvent(events[index]))
            {
                index++;
            }

            var run = new List<BattleEvent>(index - start);

            for (var position = start; position < index; position++)
            {
                run.Add(events[position]);
            }

            runs.Add(run);
        }

        return runs;
    }

    /// <summary>
    /// Whether the Boss's board action cleared at least one ATK Gem — the acting
    /// side's qualifying-attack gate (<c>ADR-026</c> D1).
    /// </summary>
    public static bool BossClearedAtkGem(SwapExecutionResult result) =>
        BossBoardEvents(result).Any(
            e => e.Type == BattleEventType.GemMatched && e.Gem.GemType == GemType.Atk);

    /// <summary>
    /// The number of POWER match groups the Boss's board action produced — one per
    /// detected POWER Match, which is the unit <c>BossState.SkillCharge</c>
    /// accumulates in (<c>ADR-026</c> T1, T2, D2;
    /// <see cref="ResourceGenerator.CountPowerMatchGroups"/>).
    /// </summary>
    public static int BossPowerMatchGroups(SwapExecutionResult result) =>
        BossBoardEvents(result).Count(
            e => e.Type == BattleEventType.MatchCreated && e.Match.Shape.GemType == GemType.Power);

    /// <summary>
    /// Whether a Battle Event is part of a Match-3 board cycle
    /// (<c>GAME_EVENTS.md</c> §1.1).
    /// </summary>
    /// <remarks>
    /// <c>CascadeStepResolved</c> is a board-cycle event like the other four: it is
    /// emitted as its own pass's <b>last</b> item
    /// (<c>GAME_EVENTS.md</c> §1.1 item 8, <c>SIGNALR_PROTOCOL.md</c> §3.2.28), so a
    /// pass's run stays contiguous and the round keeps its two runs — the Player
    /// action's cycle and the Boss response phase's. Leaving it out would split every
    /// pass at its own closing report and make <see cref="BossBoardEvents"/> answer
    /// the Player cycle's tail.
    /// </remarks>
    private static bool IsBoardEvent(BattleEvent battleEvent) =>
        battleEvent.Type
            is BattleEventType.MatchCreated
            or BattleEventType.CascadeCreated
            or BattleEventType.GemMatched
            or BattleEventType.ComboChanged
            or BattleEventType.CascadeStepResolved;

    /// <summary>
    /// Pins <paramref name="board"/> as the battle's authoritative board and
    /// <paramref name="bossState"/>/<paramref name="petState"/> as its combat states.
    ///
    /// The write goes through the store's own <c>Sequence</c> compare-and-set, which
    /// is the idiom the application suites already use for arranging a scenario, so
    /// a test's write accounting is unchanged by the pinning: the pinned state
    /// carries the same <c>Sequence</c> the record already had.
    /// </summary>
    public static async Task PinAsync(
        InMemoryBattleStateRepository repository,
        BattleState state,
        BoardState board,
        BossState? bossState = null,
        PetState? petState = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(state);

        var pinned = state with
        {
            BoardState = board,
            BossState = bossState ?? state.BossState,
            PetState = petState ?? state.PetState,
        };

        if (!await repository.TryUpdateAsync(pinned, state.Sequence).ConfigureAwait(false))
        {
            throw new InvalidOperationException(
                "The scenario's board could not be pinned: the stored Sequence moved on.");
        }
    }
}
