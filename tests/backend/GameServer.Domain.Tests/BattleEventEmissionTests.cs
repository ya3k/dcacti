using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Combat;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Battle Event emission tests (<c>GAME_EVENTS.md</c> §1, §1.1, §1.2, §1.3, §2;
/// <c>GAME_RULES.md</c> §16, §17; <c>MATCH3_RULES.md</c> §3.2, §4, §5.5.5, §6).
///
/// The contract under test is the documented event stream of one committed Swap:
///
/// <code>
/// per pass, in Resolution.Passes order
///   CascadeCreated          once, when the pass is a Cascade — first
///   per Match, in the pass's §3.2 order
///     MatchCreated
///     GemMatched            the pass's matched cells — ONE union for the pass,
///                           ascending §1.0 index, once per cell, emitted with
///                           the pass's first MatchCreated (§1.3 items 1-2)
///     ComboChanged          the NEW Combo value for that Match
///   GemMatched              the pass's activation sub-step, ascending index
/// </code>
///
/// Expected values come from the rule and from the resolution's own report, never
/// from the implementation: a Match total is read from <c>Resolution.Passes</c>,
/// so each fixture asserts the documented identity rather than a hard-coded board
/// shape. Fixtures that need a particular shape (several Matches in one pass, a
/// Special Gem activation) search the pair space for a pair whose resolution really
/// has that property, and assert the property before using it — the fixture is
/// never allowed to silently degrade into a test of something else.
/// </summary>
public class BattleEventEmissionTests
{
    private const ulong TestSeed = 20260815UL;

    private static int I(int row, int column) => TestBoard.I(row, column);

    // The canonical completing-swap fixture (shared with the accounting tests):
    // row 4 holds 'A','D','A' at columns 0-2 and (3,1) holds the 'A' directly above
    // the 'D', so exchanging (3,1)<->(4,1) makes row 4 'AAA' — the §3 horizontal
    // Match. The board is match-free before the swap.
    private const int From = 25; // I(3, 1)
    private const int To = 33;   // I(4, 1)

    private static BoardState MatchingBoard() =>
        TestBoard.Background().WithGems(
            (I(4, 0), GemType.Atk),
            (I(4, 1), GemType.Def),
            (I(4, 2), GemType.Atk),
            (I(3, 1), GemType.Atk));

    /// <summary>
    /// A battle whose board is the single-Match fixture and whose Match/Combo
    /// accounting is the one under test — at the <c>BattleState</c> root, where
    /// <c>GAME_STATE.md</c> §2.2 places it (ADR-011 item 2); there is no
    /// <c>PlayerState</c> type or node in the Domain (ADR-011 items 1 and 5).
    /// </summary>
    private static BattleState BattleWith(
        BoardState board,
        (int Combo, int MatchCount)? accounting = null)
    {
        var (combo, matchCount) = accounting ?? (BattleState.InitialCombo, BattleState.InitialMatchCount);

        return BattleState.CreateWith("battle-006", TestSeed) with
        {
            BoardState = board,
            Combo = combo,
            MatchCount = matchCount,
        };
    }

    /// <summary>
    /// A board — generated from <see cref="CascadeSeed"/> and quoted exactly as the
    /// authority produced it — whose adjacent pair (22, 23) produces several Matches
    /// across a genuine cascade (<c>MATCH3_RULES.md</c> §4.2, §6.7's worked-example
    /// shape).
    ///
    /// <code>
    /// D D A H H A A H
    /// P H A A D H A A
    /// D D P H P P H A
    /// H P D P H A H P
    /// D A P D A A P D
    /// A P D A H H A P
    /// D D A D D A H H
    /// D D H A A D A H
    /// </code>
    /// </summary>
    private const ulong CascadeSeed = 1UL;

    private const int CascadeFrom = 22; // I(2, 6)
    private const int CascadeTo = 23;   // I(2, 7)

    private static BoardState CascadeBoard() =>
        TestBoard.FromRows(
            "DDAHHAAH",
            "PHAADHAA",
            "DDPHPPHA",
            "HPDPHAHP",
            "DAPDAAPD",
            "APDAHHAP",
            "DDADDAHH",
            "DDHAADAH");

    private static BattleState CascadeBattle((int Combo, int MatchCount)? accounting = null)
    {
        var (combo, matchCount) = accounting ?? (BattleState.InitialCombo, BattleState.InitialMatchCount);

        return BattleState.CreateWith("battle-006-cascade", CascadeSeed) with
        {
            BoardState = CascadeBoard(),
            Combo = combo,
            MatchCount = matchCount,
        };
    }

    // =======================================================================
    // One Match — GAME_EVENTS.md §1.1, §2
    // =======================================================================

    [Fact]
    public void SingleMatch_ShouldEmitMatchCreatedAndComboChanged()
    {
        // §2 MatchCreated: "A Match is detected". §2 ComboChanged: the Combo value
        // changes — one increment per Match (MATCH3_RULES.md §6.2 item 1).
        var result = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(From, To));

        Assert.True(result.IsAccepted);

        // The fixture is a genuine single-pass, single-Match resolution.
        Assert.Equal(1, result.Resolution.TotalMatches);
        Assert.Single(result.Resolution.Passes);

        var matches = result.Events.Where(e => e.Type == BattleEventType.MatchCreated).ToArray();
        var combos = result.Events.Where(e => e.Type == BattleEventType.ComboChanged).ToArray();

        Assert.Single(matches);
        Assert.Single(combos);

        // §2: ComboChanged's payload is the NEW Combo value — 1 for a Swap's first
        // Match, never 0 and never 2 (MATCH3_RULES.md §6.2 item 1).
        Assert.Equal(1, combos[0].Combo);

        // The MatchCreated payload is the resolution's own Match — same shape, same
        // cells, same created Special Gems (§2 "Match shape (cells), Gem type, tier,
        // Special Gem created (if any)").
        Assert.Same(result.Resolution.Passes[0].Matches[0].Shape, matches[0].Match.Shape);
        Assert.Equal(result.Resolution.Passes[0].Matches[0].CascadeDepth, matches[0].Match.CascadeDepth);
    }

    [Fact]
    public void SingleMatch_ShouldEmitNoCascadeCreated()
    {
        // §1.1 item 1 / §2: CascadeCreated reports a pass produced by Gravity+Spawn —
        // depth 1 is NOT a Cascade (MATCH3_RULES.md §4.2 item 1).
        var result = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(From, To));

        Assert.True(result.IsAccepted);
        Assert.DoesNotContain(result.Events, e => e.Type == BattleEventType.CascadeCreated);
    }

    [Fact]
    public void SingleMatch_ShouldEmitOneGemMatchedPerMatchedCell()
    {
        // §2 GemMatched: "Once per individual Gem consumed by a Match". §1.3: once
        // per cell of the union, ascending §1.0 cell index.
        var result = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(From, To));

        Assert.True(result.IsAccepted);

        var gems = result.Events.Where(e => e.Type == BattleEventType.GemMatched).ToArray();
        var expected = result.Resolution.Passes[0].GemMatched;

        Assert.Equal(expected.Count, gems.Length);

        // The enumeration is ascending cell index and the cell is reported once.
        Assert.Equal(gems.Select(g => g.Gem.CellIndex).OrderBy(i => i), gems.Select(g => g.Gem.CellIndex));
        Assert.Equal(gems.Select(g => g.Gem.CellIndex).Distinct().Count(), gems.Length);
    }

    [Fact]
    public void SingleMatch_ShouldEmitMatchCreatedBeforeComboChanged()
    {
        // §1.1 item 5: ComboChanged follows the Match it reports — after that Match's
        // MatchCreated and before the next Match's.
        var result = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(From, To));

        Assert.True(result.IsAccepted);

        var matchCreated = IndexOfType(result.Events, BattleEventType.MatchCreated);
        var comboChanged = IndexOfType(result.Events, BattleEventType.ComboChanged);

        Assert.True(matchCreated >= 0);
        Assert.True(comboChanged > matchCreated);
    }

    // =======================================================================
    // Several Matches in one pass — MATCH3_RULES.md §3 item 5, GAME_EVENTS.md §1.1
    // =======================================================================

    /// <summary>
    /// A pair of a generated board whose <b>first</b> detection pass produces more
    /// than one Match, found by asking the existing resolver rather than by guessing
    /// a board shape (<c>MATCH3_RULES.md</c> §1.4 guarantees a valid swap exists).
    /// </summary>
    private static (BattleState State, SwapRequest Request, int MatchesInFirstPass) FindMultiMatchInOnePass()
    {
        var state = BattleState.CreateWith("battle-006-multi", TestSeed);

        foreach (var (from, to) in AllAdjacentPairs())
        {
            var request = new SwapRequest(from, to);

            if (SwapValidator.Validate(state.BoardState, state.LastCommittedSwapPair, request).IsRejected)
            {
                continue;
            }

            var resolution = CascadeResolver.Resolve(
                state.BoardState.WithSwapped(from, to),
                Pcg32.FromSeed(state.RngState.State));

            if (resolution.Passes.Count > 0 && resolution.Passes[0].Matches.Count >= 2)
            {
                return (state, request, resolution.Passes[0].Matches.Count);
            }
        }

        throw new InvalidOperationException(
            "Expected an adjacent pair whose first pass holds several Matches "
            + "(MATCH3_RULES.md §3 item 5 allows it; §1.4 guarantees a valid Swap).");
    }

    [Fact]
    public void MultipleMatchesInOnePass_ShouldEmitOneMatchCreatedEachInMatchSetOrder()
    {
        // §1.1: "MatchCreated per Match, in match-set order (§3.2)". Two shapes in
        // one pass are two Matches, each with its own event (MATCH3_RULES.md §3
        // item 5).
        var (state, request, expectedInFirstPass) = FindMultiMatchInOnePass();

        var result = SwapExecutor.Execute(state, request);

        Assert.True(result.IsAccepted);

        // The fixture really does produce more than one Match in its first pass.
        Assert.Equal(expectedInFirstPass, result.Resolution.Passes[0].Matches.Count);
        Assert.True(expectedInFirstPass >= 2);

        var expectedShapes = result.Resolution.Passes[0].Matches;
        var firstPassEvents = result.Events
            .Where(e => e.Type == BattleEventType.MatchCreated && e.Match.CascadeDepth == 1)
            .ToArray();

        Assert.Equal(expectedShapes.Count, firstPassEvents.Length);

        // §3.2 order, reproduced exactly — not re-sorted by cell index or anything
        // else (GAME_EVENTS.md §1.1, MATCH3_RULES.md §3.2).
        Assert.Equal(
            expectedShapes.Select(m => m.Shape.StartIndex),
            firstPassEvents.Select(e => e.Match.Shape.StartIndex));

        Assert.Equal(
            expectedShapes.Select(m => m.Shape.GemType),
            firstPassEvents.Select(e => e.Match.Shape.GemType));

        // And the whole resolution's Match events are still one per Match, in
        // resolution order.
        var allMatches = result.Resolution.Passes.SelectMany(p => p.Matches).ToArray();
        var allEvents = result.Events.Where(e => e.Type == BattleEventType.MatchCreated).ToArray();

        Assert.Equal(allMatches.Length, allEvents.Length);

        for (var i = 0; i < allMatches.Length; i++)
        {
            Assert.Same(allMatches[i].Shape, allEvents[i].Match.Shape);
        }
    }

    [Fact]
    public void MultipleMatchesInOnePass_ShouldIncrementComboByExactlyOnePerMatch()
    {
        // §1.1 item 7: "a ComboChanged that jumps by more than 1" is an ordering
        // defect. §1.1 item 5: the increment for a Match is emitted after that
        // Match's MatchCreated and before the next Match's.
        //
        // The expected run is derived from the resolution's own Match sequence, so
        // the fixture is not required to produce a particular number.
        var (state, request, _) = FindMultiMatchInOnePass();

        var result = SwapExecutor.Execute(state, request);

        Assert.True(result.IsAccepted);

        var matchEvents = result.Events.Where(e => e.Type == BattleEventType.MatchCreated).ToArray();
        var comboEvents = result.Events.Where(e => e.Type == BattleEventType.ComboChanged).ToArray();

        Assert.Equal(matchEvents.Length, comboEvents.Length);

        // Exactly 1, 2, 3, … — never 0, never a jump (MATCH3_RULES.md §6.2).
        Assert.Equal(
            Enumerable.Range(1, matchEvents.Length),
            comboEvents.Select(e => e.Combo));

        // Each ComboChanged sits immediately after its own MatchCreated and before
        // the next MatchCreated (the pass's single matched GemMatched run, when there
        // are any, sits after the MatchCreated that opens the pass's match sequence).
        for (var i = 0; i < comboEvents.Length; i++)
        {
            var comboIndex = IndexOf(result.Events, comboEvents[i]);
            var nextMatchIndex = i + 1 < matchEvents.Length
                ? IndexOf(result.Events, matchEvents[i + 1])
                : result.Events.Count;

            Assert.True(comboIndex > IndexOf(result.Events, matchEvents[i]));
            Assert.True(comboIndex < nextMatchIndex);
        }
    }

    [Fact]
    public void MultipleMatchesInOnePass_ShouldCountMatches_NotClearedCells()
    {
        // §3.4 item 1 / §6.2 item 2: Matches are counted when detected, per shape —
        // never per cleared cell. A pass that clears N cells still emits exactly one
        // ComboChanged per Match.
        var (state, request, _) = FindMultiMatchInOnePass();

        var result = SwapExecutor.Execute(state, request);

        Assert.True(result.IsAccepted);

        var matches = result.Events.Count(e => e.Type == BattleEventType.MatchCreated);
        var combos = result.Events.Count(e => e.Type == BattleEventType.ComboChanged);
        var cleared = result.Resolution.Passes.Sum(p => p.ClearedCellUnion.Count);

        Assert.Equal(matches, combos);

        // The identities are distinct: neither event count is a cleared-cell total.
        // (A Match of 3 clears at least 3 cells, so a resolution with any Match has
        // strictly more cleared cells than Matches.)
        Assert.True(
            cleared > matches,
            "cleared cells must outnumber Matches, or the counts would be indistinguishable");
    }

    // =======================================================================
    // Several Matches, one cleared union — GAME_EVENTS.md §1.3
    //
    // A pass's matched cells are ONE union, reported once per cell (§1.3 items 1
    // and 2): the sub-step is a property of the pass, not of each Match in it.
    // Every pass of the `CascadeBattle()` fixture holds a single Match, so that
    // fixture cannot observe the difference; these fixtures each hold two Matches in
    // one pass and pin the union semantics directly.
    // =======================================================================

    /// <summary>
    /// A deterministic two-Match fixture: one adjacent exchange produces two
    /// <b>disjoint</b> horizontal Match 3s in the same detection pass — row 3
    /// columns 0-2 making ATK, row 4 columns 0-2 making HP.
    ///
    /// <code>
    /// A D H P A D H P
    /// D H P A D H P A
    /// H P A D H P A D
    /// A H A H P A D H      row 3: (3,0)=A (3,1)=H (3,2)=A  →  AAA after the swap
    /// H A H P A D H P      row 4: (4,0)=H (4,1)=A (4,2)=H  →  HHH after the swap
    /// D H P A D H P A
    /// H P A D H P A D
    /// P A D H P A D H
    /// </code>
    ///
    /// Exchanging <c>(3,1)&lt;-&gt;(4,1)</c> puts the 'A' at (3,1) and the 'H' at (4,1),
    /// so both rows complete at once: the pass holds two Matches of three cells each
    /// with no shared cell and no Special Gem (<c>MATCH3_RULES.md</c> §3 item 5,
    /// §3.2 order). The board is match-free before the exchange, which every test
    /// using this fixture asserts rather than assumes.
    ///
    /// The two Matches then cascade: the fixture's later passes are single-Match,
    /// so the multi-Match shape and the Cascade shape are both present in one Swap
    /// (<c>MATCH3_RULES.md</c> §4.2).
    /// </summary>
    private static BoardState TwoDisjointMatchesBoard() =>
        TestBoard.FromRows(
            "ADHPADHP",
            "DHPADHPA",
            "HPADHPAD",
            "AHAHPADH",
            "HAHPADHP",
            "DHPADHPA",
            "HPADHPAD",
            "PADHPADH");

    private const int TwoMatchFrom = 25; // I(3, 1)
    private const int TwoMatchTo = 33;   // I(4, 1)

    [Fact]
    public void SeveralMatchesInOnePass_ShouldEmitEachMatchedCellExactlyOnce()
    {
        // §1.3 item 1: "the pass's matched cells first" — the matched sub-step is one
        // union per pass, ascending §1.0 index. §1.3 item 2: no two cells share an
        // index and a cell named by several sources is reported once. §1.3 item 7: a
        // GemMatched sequence "that repeats a cell" is an ordering defect.
        //
        // The defect this pins: the matched run is emitted once per Match, so a pass
        // with M Matches reports every matched cell M times.
        var state = BattleWith(TwoDisjointMatchesBoard());

        // The fixture is a match-free board whose completing exchange produces two
        // disjoint Matches (asserted, not assumed: a fixture that silently stopped
        // doing so would test nothing).
        Assert.Empty(MatchDetector.Detect(state.BoardState));

        var result = SwapExecutor.Execute(state, new SwapRequest(TwoMatchFrom, TwoMatchTo));

        Assert.True(result.IsAccepted);

        var firstPass = result.Resolution.Passes[0];

        Assert.Equal(2, firstPass.Matches.Count);
        Assert.Equal(6, firstPass.GemMatched.Count);

        var expectedCells = firstPass.GemMatched.Select(g => g.CellIndex).ToArray();

        Assert.Equal(expectedCells.OrderBy(i => i), expectedCells);
        Assert.Equal(expectedCells.Length, expectedCells.Distinct().Count());

        var matchCreatedIndices = result.Events
            .Select((e, index) => (e, index))
            .Where(x => x.e.Type == BattleEventType.MatchCreated)
            .Select(x => x.index)
            .ToArray();

        Assert.Equal(result.Resolution.Passes.Sum(p => p.Matches.Count), matchCreatedIndices.Length);

        // Every expected matched-cell event is emitted exactly once over the whole
        // Swap, and no unexpected one is. The unfixed builder emitted this pass's six
        // cells once per Match — twelve GemMatched events where six are expected.
        Assert.Equal(
            result.Resolution.Passes.Sum(p => p.GemMatched.Count),
            result.Events.Count(e => e.Type == BattleEventType.GemMatched));

        // The pass's matched run is emitted once, ascending, immediately after the
        // MatchCreated that opens the pass's match sequence (§1.1: MatchCreated
        // precedes the GemMatched run it introduces).
        var matchedRun = result.Events
            .Skip(matchCreatedIndices[0] + 1)
            .Take(expectedCells.Length)
            .ToArray();

        Assert.All(matchedRun, e => Assert.Equal(BattleEventType.GemMatched, e.Type));
        Assert.Equal(expectedCells, matchedRun.Select(e => e.Gem.CellIndex));

        // The pass's second Match adds no second copy of that run: its MatchCreated
        // is followed by its own ComboChanged (§1.1 item 5), not by the six cells
        // again.
        Assert.Equal(BattleEventType.ComboChanged, result.Events[matchCreatedIndices[1] + 1].Type);

        // And no cell is reported at an unexpected place: the emitted GemMatched
        // sequence is exactly the resolver's own per-pass reports.
        Assert.Equal(
            result.Resolution.Passes
                .SelectMany(p => p.GemMatched)
                .Select(g => g.CellIndex),
            result.Events
                .Where(e => e.Type == BattleEventType.GemMatched)
                .Select(e => e.Gem.CellIndex));
    }

    [Fact]
    public void SeveralMatchesInOnePass_ShouldFollowTheDocumentedCycleOrder()
    {
        // §1.1's cycle over a Swap whose first pass holds two Matches: the pass's
        // single matched run sits between the MatchCreated that opens the pass's
        // match sequence and the ComboChanged of each Match, which item 5 keeps one
        // per Match and inside the pass's match-set order.
        var result = SwapExecutor.Execute(
            BattleWith(TwoDisjointMatchesBoard()),
            new SwapRequest(TwoMatchFrom, TwoMatchTo));

        Assert.True(result.IsAccepted);

        var firstPass = result.Resolution.Passes[0];
        Assert.Equal(2, firstPass.Matches.Count);

        // The expected sequence is built from the resolution's own report — the
        // documented rendering of this resolution, never a literal list for this
        // board.
        var expectedTypes = new List<BattleEventType>
        {
            BattleEventType.SwapStarted,
            BattleEventType.SwapResolved,
        };

        foreach (var pass in result.Resolution.Passes)
        {
            // §1.1 item 1: CascadeCreated is emitted once for its pass, first.
            if (pass.Matches[0].CascadeDepth >= 2)
            {
                expectedTypes.Add(BattleEventType.CascadeCreated);
            }

            var clearedUnionAdded = false;

            foreach (var _ in pass.Matches)
            {
                // §1.1 item 2: MatchCreated per Match, in the pass's match-set order.
                expectedTypes.Add(BattleEventType.MatchCreated);

                // §1.3 item 1: the pass's cleared union is one union, emitted
                // once — never once per Match.
                if (!clearedUnionAdded)
                {
                    for (var i = 0; i < pass.GemMatched.Count; i++)
                    {
                        expectedTypes.Add(BattleEventType.GemMatched);
                    }

                    clearedUnionAdded = true;
                }

                // §1.1 item 5: ComboChanged follows the Match it reports.
                expectedTypes.Add(BattleEventType.ComboChanged);
            }

            // §1.1 item 8 / §3.2.28: the pass's completed Gravity + Spawn transition
            // is its last item, whatever number of Matches the pass held — one report
            // per PASS, never one per Match.
            expectedTypes.Add(BattleEventType.CascadeStepResolved);
        }

        Assert.Equal(expectedTypes, result.Events.Select(e => e.Type));

        // One transition report per pass, at that pass's own depth: the count is the
        // pass count, not the Match count (SIGNALR_PROTOCOL.md §3.2.28 item 5).
        var stepReports = result.Events
            .Where(e => e.Type == BattleEventType.CascadeStepResolved)
            .ToArray();

        Assert.Equal(result.Resolution.Passes.Count, stepReports.Length);
        Assert.Equal(
            result.Resolution.Passes.Select(p => p.Matches[0].CascadeDepth),
            stepReports.Select(e => e.CascadeStep.PassDepth));

        // The same facts asserted without the projection above: the two Matches of the
        // first pass, in §3.2 match-set order, each keeping its own Combo increment.
        var firstPassMatchEvents = result.Events
            .Where(e => e.Type == BattleEventType.MatchCreated && e.Match.CascadeDepth == 1)
            .ToArray();

        Assert.Equal(2, firstPassMatchEvents.Length);
        Assert.Equal(
            firstPass.Matches.Select(m => m.Shape.StartIndex),
            firstPassMatchEvents.Select(e => e.Match.Shape.StartIndex));

        var combos = result.Events
            .Where(e => e.Type == BattleEventType.ComboChanged)
            .Select(e => e.Combo)
            .ToArray();

        Assert.Equal(Enumerable.Range(1, combos.Length), combos);
        Assert.Equal(result.State.Combo, combos[^1]);

        // Cascade depth is unaffected by the number of Matches in a pass: one
        // CascadeCreated per pass at depth ≥ 2, depths 1..n−1 in order, and every
        // MatchCreated keeps its own pass's depth (MATCH3_RULES.md §4.2 item 2).
        var cascadePasses = result.Resolution.Passes.Count(p => p.Matches[0].CascadeDepth >= 2);

        Assert.True(cascadePasses >= 1, "the fixture must cascade, or the depth assertions would be vacuous");
        Assert.Equal(result.Resolution.CascadeDepth, cascadePasses);
        Assert.Equal(
            Enumerable.Range(1, cascadePasses),
            result.Events
                .Where(e => e.Type == BattleEventType.CascadeCreated)
                .Select(e => e.CascadeDepth));

        Assert.Equal(
            result.Resolution.Passes.SelectMany(p => p.Matches).Select(m => m.CascadeDepth),
            result.Events
                .Where(e => e.Type == BattleEventType.MatchCreated)
                .Select(e => e.Match.CascadeDepth));
    }

    // =======================================================================
    // Multiple cascade passes — MATCH3_RULES.md §4.2, GAME_EVENTS.md §2
    // =======================================================================

    [Fact]
    public void CascadeResolution_ShouldEmitOneCascadeCreatedPerCascadePass()
    {
        // §2 CascadeCreated: "Emitted once per Cascade pass". §4.2 item 2: a pass at
        // depth d ≥ 2 is a Cascade and its depth index within the Swap is d − 1 —
        // so a resolution with n recorded passes emits n − 1 CascadeCreated events,
        // indexed 1..n−1.
        var result = SwapExecutor.Execute(CascadeBattle(), new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(result.IsAccepted);

        // The fixture is a genuine multi-pass cascade (asserted, not assumed).
        Assert.True(result.Resolution.Passes.Count >= 3, "the fixture must run several passes");

        var cascades = result.Events.Where(e => e.Type == BattleEventType.CascadeCreated).ToArray();

        Assert.Equal(result.Resolution.Passes.Count - 1, cascades.Length);
        Assert.Equal(
            Enumerable.Range(1, result.Resolution.Passes.Count - 1),
            cascades.Select(e => e.CascadeDepth));
    }

    [Fact]
    public void CascadeCreated_ShouldPrecedeTheMatchesOfItsPass()
    {
        // §1.1 item 1: "CascadeCreated precedes the Matches of its pass. It reports
        // the pass itself, so it is emitted before the cycle it introduces."
        // §1.1 item 7: "CascadeCreated after the Matches it introduces" is an
        // ordering defect.
        //
        // Asserted as the whole structural shape: each pass contributes one block,
        // and a Cascade pass's block opens with its CascadeCreated.
        var result = SwapExecutor.Execute(CascadeBattle(), new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(result.IsAccepted);
        Assert.True(result.Resolution.Passes.Count >= 2);

        var expectedTypes = new List<BattleEventType>();
        var expectedDepths = new List<int>();

        foreach (var pass in result.Resolution.Passes)
        {
            if (pass.Matches[0].CascadeDepth >= 2)
            {
                expectedTypes.Add(BattleEventType.CascadeCreated);
                expectedDepths.Add(pass.Matches[0].CascadeDepth - 1);
            }

            foreach (var _ in pass.Matches)
            {
                expectedTypes.Add(BattleEventType.MatchCreated);
            }
        }

        var actualCascades = result.Events
            .Select((e, index) => (e, index))
            .Where(x => x.e.Type == BattleEventType.CascadeCreated)
            .ToArray();

        Assert.Equal(expectedDepths, actualCascades.Select(x => x.e.CascadeDepth));

        // Each CascadeCreated precedes every MatchCreated of a strictly deeper pass,
        // and follows every MatchCreated of an earlier one.
        foreach (var (cascade, cascadeIndex) in actualCascades)
        {
            var deeperMatches = result.Events
                .Select((e, index) => (e, index))
                .Where(x => x.e.Type == BattleEventType.MatchCreated
                            && x.e.Match.CascadeDepth > cascade.CascadeDepth)
                .ToArray();

            Assert.NotEmpty(deeperMatches);
            Assert.All(deeperMatches, x => Assert.True(x.index > cascadeIndex));

            var shallowerMatches = result.Events
                .Select((e, index) => (e, index))
                .Where(x => x.e.Type == BattleEventType.MatchCreated
                            && x.e.Match.CascadeDepth <= cascade.CascadeDepth)
                .ToArray();

            Assert.All(shallowerMatches, x => Assert.True(x.index < cascadeIndex));
        }
    }

    [Fact]
    public void CascadeResolution_ShouldEmitOneMatchCreatedPerMatchAcrossEveryPass()
    {
        // §2 MatchCreated is per Match, and §4.2 item 5 counts every Match in every
        // pass identically. The event count is the resolution's own Match total.
        var result = SwapExecutor.Execute(CascadeBattle(), new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(result.IsAccepted);

        var expected = result.Resolution.Passes.Sum(p => p.Matches.Count);
        var actual = result.Events.Count(e => e.Type == BattleEventType.MatchCreated);

        Assert.Equal(expected, actual);
        Assert.Equal(result.Resolution.TotalMatches, actual);
    }

    [Fact]
    public void CascadeResolution_ShouldReachTheAccountedComboForItsLastMatch()
    {
        // §2: ComboChanged's payload is the new Combo value. §6.3 item 1: Combo is
        // the running total of Matches the Swap produced, so the last ComboChanged
        // equals the accounted BattleState.Combo — the already-updated authoritative
        // value, not a recomputed one (GAME_STATE.md §2.2).
        var result = SwapExecutor.Execute(CascadeBattle(), new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(result.IsAccepted);

        var combos = result.Events.Where(e => e.Type == BattleEventType.ComboChanged).ToArray();

        Assert.NotEmpty(combos);
        Assert.Equal(result.State.Combo, combos[^1].Combo);
        Assert.Equal(
            result.Resolution.Passes.Sum(p => p.Matches.Count),
            combos[^1].Combo);
    }

    // =======================================================================
    // Match event ordering — GAME_EVENTS.md §1.1
    // =======================================================================

    [Fact]
    public void Events_ShouldFollowTheDocumentedCycleOrder()
    {
        // §1.1's cycle asserted structurally over a real multi-pass resolution. Every
        // pass of this fixture holds a single Match, so the union and per-Match
        // readings of the matched sub-step coincide here; the several-Matches-in-one-
        // pass fixtures below are what distinguish them.
        //
        // The committed action's own boundary pair opens the list, before the cycle
        // it resolved (GAME_EVENTS.md §1, MATCH3_RULES.md §2.1.6 steps 3 and 7,
        // SIGNALR_PROTOCOL.md §3.2.26–§3.2.27).
        //
        // The expected sequence is built from the resolution's own report, so the
        // assertion is "the events are exactly the documented rendering of this
        // resolution" rather than "this board produces this literal list".
        var result = SwapExecutor.Execute(CascadeBattle(), new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(result.IsAccepted);

        var expectedTypes = new List<BattleEventType>
        {
            BattleEventType.SwapStarted,
            BattleEventType.SwapResolved,
        };

        foreach (var pass in result.Resolution.Passes)
        {
            if (pass.Matches[0].CascadeDepth >= 2)
            {
                expectedTypes.Add(BattleEventType.CascadeCreated);
            }

            var clearedUnionAdded = false;

            foreach (var _ in pass.Matches)
            {
                expectedTypes.Add(BattleEventType.MatchCreated);

                // §1.3 item 1: the pass's cleared cells are ONE union, emitted once —
                // never once per Match (see the two-Match fixtures above).
                if (!clearedUnionAdded)
                {
                    for (var i = 0; i < pass.GemMatched.Count; i++)
                    {
                        expectedTypes.Add(BattleEventType.GemMatched);
                    }

                    clearedUnionAdded = true;
                }

                expectedTypes.Add(BattleEventType.ComboChanged);
            }

            // §1.1 item 8 / §3.2.28: the pass's completed Gravity + Spawn transition
            // is its last item — after every Match, Gem and Combo report of the pass
            // and before the next pass's CascadeCreated.
            expectedTypes.Add(BattleEventType.CascadeStepResolved);
        }

        Assert.Equal(expectedTypes, result.Events.Select(e => e.Type));

        // The depth of every reported transition is its own pass's depth: the same
        // value the pass's MatchCreated events carry, and CascadeCreated's + 1 on
        // every pass that has one (SIGNALR_PROTOCOL.md §3.2.28 item 2).
        Assert.Equal(
            result.Resolution.Passes.Select(p => p.Matches[0].CascadeDepth),
            result.Events
                .Where(e => e.Type == BattleEventType.CascadeStepResolved)
                .Select(e => e.CascadeStep.PassDepth));

        // And each report carries its own pass's own step results, in order.
        Assert.Equal(
            result.Resolution.Passes.SelectMany(p => p.DropMoves)
                .Select(m => $"{m.FromCell}>{m.ToCell}"),
            result.Events
                .Where(e => e.Type == BattleEventType.CascadeStepResolved)
                .SelectMany(e => e.CascadeStep.DropMoves)
                .Select(m => $"{m.FromCell}>{m.ToCell}"));

        Assert.Equal(
            result.Resolution.Passes.SelectMany(p => p.SpawnedGems)
                .Select(s => $"{s.CellIndex}:{s.GemType}"),
            result.Events
                .Where(e => e.Type == BattleEventType.CascadeStepResolved)
                .SelectMany(e => e.CascadeStep.SpawnedGems)
                .Select(s => $"{s.CellIndex}:{s.GemType}"));
    }

    [Fact]
    public void Events_ShouldReportTheResolutionTheyDescribe()
    {
        // The events are a rendering of the resolution, not an independent account of
        // it: every MatchCreated names a Match the resolution actually reported, in
        // that order, and every GemMatched names a cell it actually cleared, in the
        // resolver's own order.
        var result = SwapExecutor.Execute(CascadeBattle(), new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(result.IsAccepted);

        var resolutionMatches = result.Resolution.Passes
            .SelectMany(p => p.Matches)
            .ToArray();

        var eventMatches = result.Events
            .Where(e => e.Type == BattleEventType.MatchCreated)
            .Select(e => e.Match)
            .ToArray();

        Assert.Equal(resolutionMatches.Length, eventMatches.Length);

        for (var i = 0; i < resolutionMatches.Length; i++)
        {
            Assert.Equal(resolutionMatches[i].CascadeDepth, eventMatches[i].CascadeDepth);
            Assert.Same(resolutionMatches[i].Shape, eventMatches[i].Shape);
        }

        var reportedCells = result.Resolution.Passes
            .SelectMany(p => p.GemMatched)
            .Select(g => g.CellIndex)
            .ToArray();

        Assert.Equal(
            reportedCells,
            result.Events
                .Where(e => e.Type == BattleEventType.GemMatched)
                .Select(e => e.Gem.CellIndex));
    }

    // =======================================================================
    // CascadeStepResolved — the pass's completed Gravity + Spawn report
    // (GAME_EVENTS.md §1.1 item 8, §2; SIGNALR_PROTOCOL.md §3.2.28; ADR-027)
    // =======================================================================

    [Fact]
    public void CascadeStepResolved_ShouldSitLastInItsOwnPass_OncePerPass()
    {
        // §3.2.28 item 5: exactly one report per recorded pass, and the position
        // §1.1 item 8 fixes —
        // the pass's LAST item, after its Match/Gem/Combo reports and immediately
        // before the next pass's CascadeCreated. The terminating pass detects no
        // Match, is not in `Passes`, and therefore reports nothing at all
        // (MATCH3_RULES.md §4.3 item 4).
        var result = SwapExecutor.Execute(CascadeBattle(), new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(result.IsAccepted);
        Assert.True(result.Resolution.Passes.Count >= 2, "the fixture must cascade, or the assertion would be vacuous");

        var indices = Enumerable.Range(0, result.Events.Count)
            .Where(i => result.Events[i].Type == BattleEventType.CascadeStepResolved)
            .ToArray();

        Assert.Equal(result.Resolution.Passes.Count, indices.Length);

        // Pass identification: one report per pass, ascending, 1-based, and the same
        // depth the pass's own MatchCreated events carry (§3.2.28 item 2).
        Assert.Equal(
            Enumerable.Range(1, result.Resolution.Passes.Count),
            indices.Select(i => result.Events[i].CascadeStep.PassDepth));

        Assert.Equal(
            result.Resolution.Passes.Select(p => p.Matches[0].CascadeDepth),
            indices.Select(i => result.Events[i].CascadeStep.PassDepth));

        foreach (var index in indices)
        {
            // The action's boundary pair opens the list, so a report is never first —
            // and the item before it belongs to the same pass.
            Assert.True(index > 0, "a pass report never opens the batch");
            Assert.True(
                result.Events[index - 1].Type is BattleEventType.MatchCreated
                    or BattleEventType.GemMatched
                    or BattleEventType.ComboChanged
                    or BattleEventType.CascadeCreated,
                $"the item before a pass report is {result.Events[index - 1].Type}");

            if (index + 1 < result.Events.Count)
            {
                // §1.1 item 1: the next pass opens with its own CascadeCreated, so the
                // report can only be the item that precedes a pass boundary.
                Assert.Equal(BattleEventType.CascadeCreated, result.Events[index + 1].Type);
            }
        }
    }

    [Fact]
    public void CascadeStepResolved_ShouldReportEachPassesOwnMovesAndSpawns()
    {
        // The report is the pass's own step results, value for value and in order —
        // and every coordinate it states is a §1.0 cell of this board, with the
        // structural invariants Gravity and Spawn guarantee (§3.2.28 item 3).
        var result = SwapExecutor.Execute(CascadeBattle(), new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(result.IsAccepted);

        var reports = result.Events
            .Where(e => e.Type == BattleEventType.CascadeStepResolved)
            .ToArray();

        Assert.Equal(result.Resolution.Passes.Count, reports.Length);

        for (var i = 0; i < reports.Length; i++)
        {
            var pass = result.Resolution.Passes[i];
            var step = reports[i].CascadeStep;

            Assert.Equal(pass.Matches[0].CascadeDepth, step.PassDepth);

            Assert.Equal(
                pass.DropMoves.Select(m => $"{m.FromCell}>{m.ToCell}"),
                step.DropMoves.Select(m => $"{m.FromCell}>{m.ToCell}"));

            Assert.Equal(
                pass.SpawnedGems.Select(s => $"{s.CellIndex}:{s.GemType}"),
                step.SpawnedGems.Select(s => $"{s.CellIndex}:{s.GemType}"));

            foreach (var move in step.DropMoves)
            {
                Assert.InRange(move.FromCell, 0, BoardState.CellCount - 1);
                Assert.InRange(move.ToCell, 0, BoardState.CellCount - 1);
                Assert.NotEqual(move.FromCell, move.ToCell);

                // §4.4 item 2: Gravity never moves a Gem between columns.
                Assert.Equal(BoardState.ToColumn(move.FromCell), BoardState.ToColumn(move.ToCell));
            }

            Assert.Equal(
                step.DropMoves.Count,
                step.DropMoves.Select(m => m.ToCell).Distinct().Count());
            Assert.Equal(
                step.DropMoves.Count,
                step.DropMoves.Select(m => m.FromCell).Distinct().Count());

            foreach (var spawn in step.SpawnedGems)
            {
                Assert.InRange(spawn.CellIndex, 0, BoardState.CellCount - 1);

                // §4.5 item 5 / §1.1: always one of the four documented Gem types.
                Assert.Contains(spawn.GemType, GemTypes.All);
            }

            Assert.Equal(
                step.SpawnedGems.Count,
                step.SpawnedGems.Select(s => s.CellIndex).Distinct().Count());

            // §4.5 item 1: Spawn fills what Gravity left empty, so no spawned Gem
            // lands in a cell a survivor fell into.
            Assert.Empty(step.SpawnedGems
                .Select(s => s.CellIndex)
                .Intersect(step.DropMoves.Select(m => m.ToCell)));

            // No board: the report states the transition, never the resulting state
            // (§3.2.12 item 1, §3.2.28 item 6).
            Assert.DoesNotContain(
                typeof(CascadeStepResolvedEvent)
                    .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance),
                p => p.Name.Contains("Board", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void CascadeStepResolved_ShouldReconstructEveryPassBoardFromTheDeliveredBatch()
    {
        // §3.2.28's sufficiency, asserted end to end: the pass's delivered
        // GemMatched run (its cleared cells) plus its delivered dropMoves and
        // spawnedGems rebuild the board that pass published. This is the invariant
        // the client's per-pass Fall/Refill presentation depends on (ADR-027), and it
        // is asserted without any client-side gravity, drawn Gem identity, or board
        // on the wire.
        var board = TestBoard.Background().WithGems(
            (I(5, 0), GemType.Atk),
            (I(5, 1), GemType.Atk),
            (I(5, 2), GemType.Atk));

        var battle = BattleState.CreateWith("battle-006-reconstruction", TestSeed);
        var rng = new Pcg32(battle.RngState.State, battle.RngState.Increment);
        var resolution = CascadeResolver.Resolve(board, rng);
        var events = BattleEventBuilder.Build(resolution, accountedCombo: resolution.TotalMatches);

        Assert.NotEmpty(resolution.Passes);
        Assert.Equal(
            resolution.Passes.Count,
            events.Count(e => e.Type == BattleEventType.CascadeStepResolved));

        var cells = board.ToArray().Select(t => (GemType?)t).ToArray();
        var cleared = new List<int>();
        var passIndex = 0;

        foreach (var delivered in events)
        {
            if (delivered.Type == BattleEventType.GemMatched)
            {
                // The pass's cleared cells, exactly as the batch delivered them.
                cleared.Add(delivered.Gem.CellIndex);
                continue;
            }

            if (delivered.Type != BattleEventType.CascadeStepResolved)
            {
                continue;
            }

            var step = delivered.CascadeStep;

            // 1. the cleared cells are emptied (§4.1 step 1).
            foreach (var index in cleared)
            {
                cells[index] = null;
            }

            // 2. Gravity: every survivor the report named moves to the cell it named.
            //    Sources are emptied first, so a cell that is one move's destination
            //    and another move's source takes the arriving Gem (§4.4 item 3).
            var before = (GemType?[])cells.Clone();

            foreach (var move in step.DropMoves)
            {
                cells[move.FromCell] = null;
            }

            foreach (var move in step.DropMoves)
            {
                cells[move.ToCell] = before[move.FromCell];
            }

            // 3. Spawn: every Gem the report named lands on the cell it named (§4.1
            //    step 4, §4.5 item 1).
            foreach (var spawn in step.SpawnedGems)
            {
                cells[spawn.CellIndex] = spawn.GemType;
            }

            // No hole may survive a pass: the board is always full again (§4.5
            // item 1, GAME_STATE.md §2.1.5 item 4).
            Assert.DoesNotContain(cells, c => c is null);

            // And the reconstruction is the pass's own published board, cell for cell.
            var expected = resolution.Passes[passIndex].Board.ToArray();

            for (var index = 0; index < expected.Length; index++)
            {
                Assert.True(
                    expected[index] == cells[index],
                    $"cell {index} of pass {passIndex + 1} was rebuilt as "
                    + $"{cells[index]?.ToString() ?? "empty"}, but the pass published "
                    + $"{expected[index]}");
            }

            cleared.Clear();
            passIndex++;
        }

        Assert.Equal(resolution.Passes.Count, passIndex);
    }

    // =======================================================================
    // Match tiers resolve as match-only — MATCH3_RULES.md §1.A, §5.7
    // =======================================================================

    /// <summary>
    /// A Match 4 fixture: row 5 holds 'P','P','D','P' at columns 0-3 with the
    /// completing 'P' at (4,2). The completing Swap makes row 5 'PPPP' — one
    /// confirmed Match of four cells, which resolves as any other Match.
    /// </summary>
    private static BoardState MatchFourBoard() =>
        TestBoard.Background().WithGems(
            (I(5, 0), GemType.Power),
            (I(5, 1), GemType.Power),
            (I(5, 2), GemType.Def),
            (I(5, 3), GemType.Power),
            (I(4, 2), GemType.Power));

    private const int MatchFourFrom = 34; // I(4, 2)
    private const int MatchFourTo = 42;   // I(5, 2)

    [Fact]
    public void MatchFour_ShouldEmitOneMatchCreatedForTheShape_AndClearOnlyItsCells()
    {
        // §2 MatchCreated / MATCH3_RULES.md §3 item 5, §5.7: one shape is one Match
        // however many cells it spans. A Match 4 is therefore exactly one Match and
        // one Combo increment, and the pass clears exactly the four matched cells —
        // nothing is added to the board or cleared beyond the match set.
        var result = SwapExecutor.Execute(BattleWith(MatchFourBoard()), new SwapRequest(MatchFourFrom, MatchFourTo));

        Assert.True(result.IsAccepted);

        var firstPass = result.Resolution.Passes[0];

        var match = Assert.Single(firstPass.Matches);
        Assert.Equal(
            firstPass.Matches.Count,
            result.Events.Count(e => e.Type == BattleEventType.MatchCreated));
        Assert.Equal(
            firstPass.Matches.Count,
            result.Events.Count(e => e.Type == BattleEventType.ComboChanged));

        // The Match is the four-cell row run, and the pass's cleared union is exactly
        // those cells — no extra cell was cleared as a side effect.
        Assert.Equal(4, match.Shape.Cells.Count);
        Assert.Equal(match.Shape.Cells, firstPass.ClearedCellUnion);
        Assert.Equal(firstPass.ClearedCellUnion, firstPass.GemMatched.Select(g => g.CellIndex));

        // The resolution creates nothing: every cell of the published board is an
        // ordinary Gem type (MATCH3_RULES.md §1.A, §4.5 item 7).
        Assert.All(result.State.BoardState.Cells, gemType => Assert.True(GemTypes.IsValid((int)gemType)));
    }

    // =======================================================================
    // Rejected Swap — GAME_EVENTS.md §1.2, MATCH3_RULES.md §2.1.5 item 6
    // =======================================================================

    [Theory]
    // INVALID_CELL_INDEX — out of range, and a cell swapped with itself.
    [InlineData(99, To)]
    [InlineData(From, From)]
    // INVALID_SWAP — diagonal, and non-adjacent across a row edge.
    [InlineData(13, 15)]
    [InlineData(0, 9)]
    // NO_MATCH_FROM_SWAP — adjacent but producing no Match.
    [InlineData(5, 6)]
    public void RejectedSwap_ShouldProduceNoGameplayEvents(int from, int to)
    {
        // §1.2 item 1: "An action that fails validation emits no Battle Event at all
        // — not SwapStarted, not SwapResolved, not any Match-3 event … This includes
        // non-adjacent swaps, invalid cell indices, already-applied (stale) swaps,
        // and swaps that produce no Match."
        var result = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(from, to));

        Assert.True(result.IsRejected);

        // The rejection carries no events — an empty list, not a list of the wrong
        // kind, and not null.
        Assert.NotNull(result.Events);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void StaleAction_ShouldProduceNoGameplayEvents()
    {
        // §1.2 item 1 names already-applied (stale) swaps explicitly; §1.2 item 3:
        // "the event stream of a battle is therefore identical whether or not a
        // rejected action was ever sent."
        var state = BattleWith(MatchingBoard());

        var committed = SwapExecutor.Execute(state, new SwapRequest(From, To));
        Assert.True(committed.IsAccepted);
        Assert.NotEmpty(committed.Events);

        // The same unordered pair again is STALE_ACTION, in either argument order.
        var forward = SwapExecutor.Execute(committed.State, new SwapRequest(From, To));
        var reversed = SwapExecutor.Execute(committed.State, new SwapRequest(To, From));

        Assert.Equal(SwapRejectionReason.StaleAction, forward.Reason);
        Assert.Equal(SwapRejectionReason.StaleAction, reversed.Reason);
        Assert.Empty(forward.Events);
        Assert.Empty(reversed.Events);
    }

    [Fact]
    public void RejectedSwap_ShouldNotDisturbTheEventStreamOfTheNextCommittedSwap()
    {
        // §1.2 item 3: a rejection consumes no RNG and changes no counters, so it is
        // invisible on the event path. The stream after a rejection is identical to
        // the stream without one.
        var state = BattleWith(CascadeBoard());

        // With no rejection at all.
        var clean = SwapExecutor.Execute(state, new SwapRequest(CascadeFrom, CascadeTo));

        // After a run of rejections covering every reason.
        foreach (var (from, to) in new[] { (99, CascadeTo), (CascadeFrom, CascadeFrom), (13, 15), (0, 9), (5, 6) })
        {
            var rejected = SwapExecutor.Execute(state, new SwapRequest(from, to));

            Assert.True(rejected.IsRejected);
            Assert.Empty(rejected.Events);
        }

        var afterRejections = SwapExecutor.Execute(state, new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(clean.IsAccepted);
        Assert.True(afterRejections.IsAccepted);
        Assert.Equal(Describe(clean.Events), Describe(afterRejections.Events));
        Assert.Equal(DescribeState(clean.State), DescribeState(afterRejections.State));
    }

    // =======================================================================
    // No mutation of authoritative state — GAME_EVENTS.md §3 item 6
    // =======================================================================

    [Fact]
    public void EventGeneration_ShouldNotMutateBattleState()
    {
        // §3 item 6: "events are not state … An event is never a substitute for the
        // state write-back." Building them must therefore be a pure read: the input
        // state is unchanged, field for field.
        var state = BattleWith(CascadeBoard(), (Combo: 3, MatchCount: 11));

        var result = SwapExecutor.Execute(state, new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(result.IsAccepted);
        Assert.NotEmpty(result.Events);

        // The input state object is untouched: every authoritative field is exactly
        // as it was (GAME_STATE.md §5.1, MATCH3_RULES.md §2.1.5).
        Assert.Equal("battle-006", state.BattleId);
        Assert.Equal(BattleState.InitialTurn, state.Turn);
        Assert.Equal(BattleState.InitialSequence, state.Sequence);
        Assert.Equal(TestSeed, state.RngSeed);
        Assert.Equal(3, state.Combo);
        Assert.Null(state.LastCommittedSwapPair);
        Assert.True(state.BoardState.CellsEqual(CascadeBoard()));

        // And the events describe the resulting state without being it: the state's
        // own accounted Combo is what the event run ends at.
        Assert.Equal(ComboCount(result), result.State.Combo);
    }

    [Fact]
    public void EventGeneration_ShouldNotConsumeRngOrChangeTheSeed()
    {
        // §3 item 6 / MATCH3_RULES.md §7.2 item 4: event generation is not a gameplay
        // step and consumes no randomness. Only Spawn advances RngState.
        var state = BattleWith(CascadeBoard());

        var result = SwapExecutor.Execute(state, new SwapRequest(CascadeFrom, CascadeTo));
        Assert.True(result.IsAccepted);

        // The seed is never rewritten (GAME_STATE.md §2.6.1 item 3).
        Assert.Equal(state.RngSeed, result.State.RngSeed);

        // The advanced RngState is the resolution's own — exactly what the resolver
        // reported, with nothing consumed by event generation.
        Assert.Equal(result.Resolution.RngState, result.State.RngState);
    }

    [Fact]
    public void EventGeneration_ShouldNotChangeTurnOrSequenceBeyondTheResolution()
    {
        // §3 item 6: events must not change Turn or Sequence. The committed Swap
        // still advances each by exactly 1 (MATCH3_RULES.md §8.1, §8.2) — no more,
        // whatever number of events it produced.
        var state = BattleWith(CascadeBoard()) with { Turn = 20, Sequence = 7 };

        var result = SwapExecutor.Execute(state, new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(result.IsAccepted);
        Assert.Equal(21, result.State.Turn);
        Assert.Equal(8, result.State.Sequence);
        Assert.True(result.Events.Count > 2, "the fixture emits many events");
    }

    [Fact]
    public void EventGeneration_ShouldLeaveLastCommittedSwapPairToTheSwap()
    {
        // §3 item 6: events must not modify LastCommittedSwapPair. It is written by
        // the committed Swap, canonically (min, max) (GAME_STATE.md §2.1.10 item 2).
        var state = BattleWith(MatchingBoard());

        var result = SwapExecutor.Execute(state, new SwapRequest(From, To));

        Assert.True(result.IsAccepted);
        Assert.Equal(
            CommittedSwapPair.FromCells(From, To),
            result.State.LastCommittedSwapPair);
    }

    [Fact]
    public void EventGeneration_ShouldNotWriteAnythingIntoThePassResult()
    {
        // The events are built from the resolution, not into it: the passes the
        // resolver produced are the same objects with the same reports, so the
        // resolution remains the only account of what the board did.
        //
        // The comparison resolution is produced from the same seed the battle
        // continues from, so an unrelated difference in the Spawn draws cannot be
        // mistaken for a mutation.
        var state = CascadeBattle();

        var rng = new Pcg32(state.RngState.State, state.RngState.Increment);
        var reference = CascadeResolver.Resolve(
            state.BoardState.WithSwapped(CascadeFrom, CascadeTo),
            rng);

        var result = SwapExecutor.Execute(state, new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(result.IsAccepted);
        Assert.Equal(reference.Passes.Count, result.Resolution.Passes.Count);

        for (var i = 0; i < reference.Passes.Count; i++)
        {
            Assert.Equal(reference.Passes[i].Matches.Count, result.Resolution.Passes[i].Matches.Count);
            Assert.Equal(
                reference.Passes[i].GemMatched.Count,
                result.Resolution.Passes[i].GemMatched.Count);
            Assert.Equal(
                reference.Passes[i].ClearedCellUnion,
                result.Resolution.Passes[i].ClearedCellUnion);
            Assert.True(reference.Passes[i].Board.CellsEqual(result.Resolution.Passes[i].Board));
        }
    }

    // =======================================================================
    // Determinism — MATCH3_RULES.md §7.1, §7.2 item 4
    // =======================================================================

    [Fact]
    public void EventSequence_ShouldBeDeterministicAcrossRepeatedIdenticalExecutions()
    {
        // §7.1: "Given the same board state and the same Swap input, match detection
        // and cascade resolution must be fully deterministic".
        // The events describe that resolution, so the sequence is identical too —
        // no timestamp, GUID, random id, or unordered collection participates
        // (§7.2 item 4, AGENTS.md §11).
        var state = CascadeBattle();

        var reference = SwapExecutor.Execute(state, new SwapRequest(CascadeFrom, CascadeTo));
        Assert.True(reference.IsAccepted);
        Assert.NotEmpty(reference.Events);

        // Compared by value, not by reference, and as a whole sequence.
        var expected = Describe(reference.Events);

        for (var run = 0; run < 20; run++)
        {
            var repeated = SwapExecutor.Execute(state, new SwapRequest(CascadeFrom, CascadeTo));

            Assert.True(repeated.IsAccepted);
            Assert.Equal(expected, Describe(repeated.Events));
        }
    }

    [Fact]
    public void EventSequence_ShouldNotDependOnTheRequestArgumentOrder()
    {
        // §2.1.1 item 2: the pair is unordered — (from, to) and (to, from) name the
        // same swap, resolve the same board cycle, and produce the same state.
        //
        // The committed action's own boundary pair is the one part of the list that
        // does depend on the argument order, and deliberately so: GAME_EVENTS.md §2
        // has SwapStarted report the cells "the action named", so the event echoes the
        // request's two cells in the order the request supplied them rather than
        // canonicalizing a pair the action itself does not order (§2.1.1 item 2
        // makes the pair unordered for *gameplay*, and fixes no reporting order).
        // Everything the pair opens — the board cycle — is order-independent, which
        // is the invariant §2.1.1 item 2 actually states.
        var state = CascadeBattle();

        var forward = SwapExecutor.Execute(state, new SwapRequest(CascadeFrom, CascadeTo));
        var reversed = SwapExecutor.Execute(state, new SwapRequest(CascadeTo, CascadeFrom));

        Assert.True(forward.IsAccepted);
        Assert.True(reversed.IsAccepted);

        // The boundary pair is the list's only order-sensitive part, and it echoes
        // each request's own cells.
        Assert.Equal(BattleEventType.SwapStarted, forward.Events[0].Type);
        Assert.Equal(BattleEventType.SwapStarted, reversed.Events[0].Type);
        Assert.Equal(
            (CascadeFrom, CascadeTo),
            (forward.Events[0].SwapStarted.FromCell, forward.Events[0].SwapStarted.ToCell));
        Assert.Equal(
            (CascadeTo, CascadeFrom),
            (reversed.Events[0].SwapStarted.FromCell, reversed.Events[0].SwapStarted.ToCell));

        // The whole rest of the sequence — the board cycle the pair opens — is
        // identical, as is the state the two executions commit.
        Assert.Equal(Describe(forward.Events.Skip(2).ToArray()), Describe(reversed.Events.Skip(2).ToArray()));
        Assert.Equal(DescribeState(forward.State), DescribeState(reversed.State));
    }

    [Fact]
    public void EventSequence_ShouldBeDeterministicOverEveryCommittingPair()
    {
        // The guarantee is not fixture-specific: for every adjacent pair of a
        // generated board, two executions of the same request produce the same
        // sequence and the same resulting state.
        var state = BattleState.CreateWith("battle-006-exhaustive", TestSeed);
        var committed = 0;

        foreach (var (from, to) in AllAdjacentPairs())
        {
            var request = new SwapRequest(from, to);
            var first = SwapExecutor.Execute(state, request);

            if (first.IsRejected)
            {
                Assert.Empty(first.Events);
                continue;
            }

            committed++;

            var second = SwapExecutor.Execute(state, request);

            Assert.Equal(Describe(first.Events), Describe(second.Events));
            Assert.Equal(DescribeState(first.State), DescribeState(second.State));
        }

        Assert.True(committed > 0, "a generated board has at least one valid Swap (MATCH3_RULES.md §1.4)");
    }

    [Fact]
    public void EventValue_ShouldCarryNoTimeGuidOrGeneratedIdentifier()
    {
        // §7.2 item 4 / AGENTS.md §11: nothing in the sequence may depend on a
        // timestamp, GUID, random id, or hash. The event value therefore has exactly
        // the documented payload members and no identifier of its own.
        var members = typeof(BattleEvent)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "BattleLost",
                "BattleWon",
                "BossCardCast",
                "CardCast",
                "CascadeDepth",
                "CascadeStep",
                "Combo",
                "DamageCalculated",
                "DamageDealt",
                "DamageTaken",
                "Gem",
                "Match",
                "MatchedGemSummary",
                "PassiveCharged",
                "PassiveTriggered",
                "PetSkillCast",
                "PowerChanged",
                "RelicTriggered",
                "SwapResolved",
                "SwapStarted",
                "Type",
            ],
            members);

        // And no nested payload carries one either. The Passive payloads are
        // deliberately not in this loop: they carry PassiveId, which is a documented
        // identity (GAME_EVENTS.md §2 item 1, GAME_STATE.md §2.3) and not a generated
        // identifier — the same kind of member RelicId / CardId / SkillId are for
        // their own events. It is asserted directly instead: the identity is the
        // caller-supplied PassiveId and nothing derived.
        //
        // The Damage payloads are here because GAME_EVENTS.md §2 gives them no
        // identity at all: the three members below plus the breakdown carry source,
        // target, and amount, so a generated id, timestamp, or hash would be an
        // undocumented member.
        foreach (var type in new[]
                 {
                     typeof(BattleEvent),
                     typeof(GemMatchedEvent),
                     typeof(MatchResolution),
                     typeof(CascadeStepResolvedEvent),
                     typeof(MatchedGemSummaryEvent),
                     typeof(MatchedGemCount),
                     typeof(GameServer.Domain.Combat.DamageCalculation),
                     typeof(DamageDealtEvent),
                     typeof(DamageTakenEvent),
                     typeof(DamageEvents),
                     typeof(DamageResult),
                 })
        {
            var names = type
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Select(p => p.Name)
                .ToArray();

            Assert.DoesNotContain(names, n =>
                n.Contains("Id", StringComparison.Ordinal)
                || n.Contains("Time", StringComparison.Ordinal)
                || n.Contains("Guid", StringComparison.Ordinal)
                || n.Contains("Hash", StringComparison.Ordinal));
        }

        // The Passive payloads carry exactly the documented members — the identity the
        // state already holds, the progress value, the Threshold, and the shared
        // event's source/sourceId (GAME_EVENTS.md §2, SIGNALR_PROTOCOL.md §3.2.16)
        // — and no generated identifier beyond those identities. SourceId is the
        // owning entity's documented identity (PetState.PetId / BossState.BossId,
        // §3.2.16 item 2), the same kind of member PassiveId is, so the `Id` filter
        // above deliberately does not apply here; the time/guid/hash filter does.
        foreach (var type in new[] { typeof(PassiveChargedEvent), typeof(PassiveTriggeredEvent) })
        {
            var names = type
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(["PassiveId", "Progress", "Source", "SourceId", "Threshold"], names);

            Assert.DoesNotContain(names, n =>
                n.Contains("Time", StringComparison.Ordinal)
                || n.Contains("Guid", StringComparison.Ordinal)
                || n.Contains("Hash", StringComparison.Ordinal));
        }

        // The Boss Response's payloads are here for the same reason the Damage
        // payloads are: GAME_EVENTS.md §2 gives them documented identities and terminal
        // values, and nothing generated. The Boss Card form's payload is the same two
        // documented identities under the names SIGNALR_PROTOCOL.md §3.2.30 fixes —
        // cardId and sourceId — and nothing else: no cost, Power value, cooldown, or
        // effect summary, because the Boss Power/cooldown it produces are
        // authoritative state in the bossState projection (GAME_EVENTS.md §2, §4.4
        // item 12), and a Boss Power spend emits no PowerChanged. (The legacy
        // BossSkillCast payload was removed with ADR-029 Slice 5 / TASK-313.)
        foreach (var type in new[] { typeof(BossCardCastEvent) })
        {
            var names = type
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(["CardId", "SourceId"], names);

            Assert.DoesNotContain(names, n =>
                n.Contains("Time", StringComparison.Ordinal)
                || n.Contains("Guid", StringComparison.Ordinal)
                || n.Contains("Hash", StringComparison.Ordinal));
        }

        foreach (var type in new[] { typeof(BattleWonEvent), typeof(BattleLostEvent) })
        {
            var names = type
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            // SIGNALR_PROTOCOL.md §3.2.19: the two terminal HP values, and nothing
            // else — the reward summary is deferred (item 3) and `outcome` is the
            // wire constant of the event's own type rather than a stored member.
            Assert.Equal(["FinalBossHp", "FinalPlayerHp"], names);

            Assert.DoesNotContain(names, n =>
                n.Contains("Time", StringComparison.Ordinal)
                || n.Contains("Guid", StringComparison.Ordinal)
                || n.Contains("Hash", StringComparison.Ordinal));
        }
    }

    // =======================================================================
    // Counts, duplicates, and the terminating pass — GAME_EVENTS.md §1.1 item 6
    // =======================================================================

    [Fact]
    public void MatchEventCount_ShouldMatchTheResolutionPasses()
    {
        // The Match events are a projection of Resolution.Passes → PassResult.Matches:
        // exactly one MatchCreated and one ComboChanged per MatchResolution, over
        // every pass, in the resolution's own order.
        var result = SwapExecutor.Execute(CascadeBattle(), new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(result.IsAccepted);

        var matchTotal = result.Resolution.Passes.Sum(p => p.Matches.Count);

        Assert.Equal(matchTotal, result.Events.Count(e => e.Type == BattleEventType.MatchCreated));
        Assert.Equal(matchTotal, result.Events.Count(e => e.Type == BattleEventType.ComboChanged));
    }

    [Fact]
    public void GemMatchedCount_ShouldMatchTheResolutionsOwnReports()
    {
        // §2 GemMatched is once per cleared cell over each pass's union; the
        // resolver already reported exactly those cells, so the event count is their
        // count — no cell added, none dropped.
        var result = SwapExecutor.Execute(CascadeBattle(), new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(result.IsAccepted);

        var expected = result.Resolution.Passes.Sum(p => p.GemMatched.Count);

        Assert.Equal(expected, result.Events.Count(e => e.Type == BattleEventType.GemMatched));
    }

    [Fact]
    public void CascadeEventCount_ShouldMatchTheCascadePasses()
    {
        // §2: CascadeCreated is emitted once per Cascade pass — the passes after the
        // first, and never for the terminating pass.
        var result = SwapExecutor.Execute(CascadeBattle(), new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(result.IsAccepted);

        var cascadePasses = result.Resolution.Passes
            .Count(p => p.Matches[0].CascadeDepth >= 2);

        Assert.Equal(cascadePasses, result.Events.Count(e => e.Type == BattleEventType.CascadeCreated));
        Assert.Equal(result.Resolution.CascadeDepth, cascadePasses);
    }

    [Fact]
    public void Events_ShouldContainNoDuplicates()
    {
        // §1.1 item 7 / §1.3 item 7: a repeated cell, a duplicated Match, or a
        // duplicated Cascade is an ordering defect.
        var result = SwapExecutor.Execute(CascadeBattle(), new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(result.IsAccepted);

        // No two MatchCreated events describe the same Match in the same pass: a
        // Match is one shape at one depth, reported once (§3.3 item 1).
        var matchKeys = result.Events
            .Where(e => e.Type == BattleEventType.MatchCreated)
            .Select(e => (e.Match.CascadeDepth, e.Match.Shape.StartIndex))
            .ToArray();

        Assert.Equal(matchKeys.Length, matchKeys.Distinct().Count());

        // No Cascade depth is reported twice.
        var depths = result.Events
            .Where(e => e.Type == BattleEventType.CascadeCreated)
            .Select(e => e.CascadeDepth)
            .ToArray();

        Assert.Equal(depths.Length, depths.Distinct().Count());

        // Within each pass's cleared union, no cell is reported as GemMatched twice:
        // a cell is cleared once per pass and reported once (§1.3 item 2). Asserted
        // per pass, because a cell matched in one pass can legitimately be matched
        // again by a later pass.
        foreach (var pass in result.Resolution.Passes)
        {
            Assert.Equal(
                pass.GemMatched.Count,
                pass.GemMatched.Select(g => g.CellIndex).Distinct().Count());
        }

        // And within one pass the cleared union reports each cell once.
        var firstPass = result.Resolution.Passes[0];
        var firstPassCells = firstPass.GemMatched
            .Select(g => g.CellIndex)
            .ToArray();

        Assert.Equal(firstPassCells.Length, firstPassCells.Distinct().Count());
    }

    [Fact]
    public void TerminatingPass_ShouldEmitNothing()
    {
        // §1.1 item 6: "A termination pass emits nothing. The pass that finds no
        // Match is a detection pass with an empty match set: no MatchCreated, no
        // CascadeCreated, no ComboChanged."
        //
        // The terminating pass is absent from Resolution.Passes by construction, and
        // the events describe exactly the recorded passes — depths 1..n with no gap
        // and no empty pass among them.
        var result = SwapExecutor.Execute(CascadeBattle(), new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(result.IsAccepted);

        var describedPasses = result.Events
            .Where(e => e.Type == BattleEventType.MatchCreated)
            .Select(e => e.Match.CascadeDepth)
            .Distinct()
            .OrderBy(d => d)
            .ToArray();

        Assert.Equal(
            result.Resolution.Passes.Select(p => p.Matches[0].CascadeDepth),
            describedPasses);

        // Every described pass has at least one Match, and they run 1..n with no
        // extra pass — in particular not the terminating one.
        Assert.Equal(Enumerable.Range(1, result.Resolution.Passes.Count), describedPasses);
        Assert.All(result.Resolution.Passes, p => Assert.NotEmpty(p.Matches));

        // The pass after the last recorded one found nothing, so it produced nothing.
        Assert.Empty(MatchDetector.Detect(result.State.BoardState));

        // No CascadeCreated claims a depth beyond the passes that ran.
        Assert.All(
            result.Events.Where(e => e.Type == BattleEventType.CascadeCreated),
            e => Assert.True(e.CascadeDepth <= result.Resolution.Passes.Count - 1));
    }

    [Fact]
    public void CommittedSwap_ShouldAlwaysEmitAtLeastOneMatchEvent()
    {
        // §4.3 item 5: a Swap whose first pass contains no Match is rejected before it
        // is committed, so the loop always begins with at least one Match. A committed
        // Swap therefore always has at least one MatchCreated and one ComboChanged —
        // never an empty stream, and never a ComboChanged of 0 (§6.5 item 3).
        var state = BattleState.CreateWith("battle-006-floor", TestSeed);
        var committed = 0;

        foreach (var (from, to) in AllAdjacentPairs())
        {
            var result = SwapExecutor.Execute(state, new SwapRequest(from, to));

            if (result.IsRejected)
            {
                Assert.Empty(result.Events);
                continue;
            }

            committed++;

            var matches = result.Events.Where(e => e.Type == BattleEventType.MatchCreated).ToArray();
            var combos = result.Events.Where(e => e.Type == BattleEventType.ComboChanged).ToArray();

            Assert.NotEmpty(matches);
            Assert.NotEmpty(combos);

            // The run starts at 1 and never reaches 0 (MATCH3_RULES.md §6.5 item 3).
            Assert.Equal(1, combos[0].Combo);
            Assert.DoesNotContain(combos, c => c.Combo == 0);
            Assert.Equal(matches.Length, combos.Length);
        }

        Assert.True(committed > 0, "a generated board has at least one valid Swap (MATCH3_RULES.md §1.4)");
    }

    [Fact]
    public void ComboChanged_ShouldUseTheDocumentedNewComboValueAcrossASwapChain()
    {
        // §2: "Payload: New Combo value". §6.1 item 2: a committed Swap resets Combo
        // to 0 before its first Match, so a later Swap's run starts at 1 again while
        // MatchCount keeps accumulating (§3) — the events report the new value, never
        // the accumulated one.
        var state = BattleState.CreateWith("battle-006-chain", TestSeed);

        for (var turn = 0; turn < 4; turn++)
        {
            var pair = FindAdjacentPairThatProducesAMatch(state, state.LastCommittedSwapPair);
            var result = SwapExecutor.Execute(state, pair);

            Assert.True(result.IsAccepted, $"turn {turn}: expected the completing swap to be accepted");

            var combos = result.Events.Where(e => e.Type == BattleEventType.ComboChanged).ToArray();

            // This Swap's own Match count — never the battle-cumulative MatchCount
            // (§6.2 item 2, §6.3 item 1).
            Assert.Equal(
                Enumerable.Range(1, result.Resolution.Passes.Sum(p => p.Matches.Count)),
                combos.Select(c => c.Combo));

            Assert.Equal(result.State.Combo, combos[^1].Combo);

            // The cumulative total is state, not an event value (§2.2, §3 item 7).
            if (turn > 0)
            {
                Assert.True(result.State.MatchCount > result.State.Combo);
            }

            state = result.State;
        }
    }

    [Fact]
    public void AResolutionTheComboAccountingDidNotCount_ShouldEmitNoComboChanged()
    {
        // GAME_EVENTS.md §2: ComboChanged's payload is the NEW value of
        // `BattleState.Combo`, and SIGNALR_PROTOCOL.md §3.2.8 item 1 fixes it as that
        // new value and nothing else. That counter is the Player action's committed
        // Swap's (GAME_STATE.md §2.2 — written in the round's single write-back and
        // delivered under the `playerState` label; BOSS_RULES.md §6.2/§6.2.5 read
        // "Player Matches" / "Player Combo"), and the Boss Response phase's own board
        // action is resolved by this same pipeline **without** writing it
        // (BattleStateService step 18a). A resolution that is not the one the Combo
        // accounting counted must therefore report no Combo at all: an event carrying
        // that cycle's own Match total would assert a Combo the battle's counter never
        // took, and the client renders one Combo from the events and the published
        // state together (MATCH3_RULES.md §6.6 item 3).
        var result = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(From, To));

        Assert.True(result.IsAccepted);

        var accounted = BattleEventBuilder.Build(result.Resolution, result.Resolution.TotalMatches);
        var notAccounted = BattleEventBuilder.Build(result.Resolution, accountedCombo: null);

        // Not vacuous: the same resolution does report its run when it is the one the
        // accounting counted.
        Assert.Contains(accounted, e => e.Type == BattleEventType.ComboChanged);
        Assert.DoesNotContain(notAccounted, e => e.Type == BattleEventType.ComboChanged);

        // Every other item is reported unchanged: the cycle's Matches, its cleared
        // cells and its pass transitions all belong to this resolution, and only the
        // counter the phase does not own is dropped — nothing is reordered, filtered
        // or re-derived.
        Assert.Equal(
            accounted.Where(e => e.Type != BattleEventType.ComboChanged).Select(e => e.Type),
            notAccounted.Select(e => e.Type));
        Assert.Equal(
            accounted.Count - result.Resolution.TotalMatches,
            notAccounted.Count);
    }

    // =======================================================================
    // The committed board action's boundary — GAME_EVENTS.md §1, §2;
    // SIGNALR_PROTOCOL.md §3.2.26–§3.2.27
    // =======================================================================

    [Fact]
    public void CommittedSwap_ShouldOpenWithItsOwnBoundaryPair_BeforeTheBoardCycle()
    {
        // GAME_EVENTS.md §1 places SwapStarted and SwapResolved at the head of the
        // committed action's events, and MATCH3_RULES.md §2.1.6 fixes the same two
        // points (step 3 after validation, step 7 before Match detection at step 8).
        // The pair reports the acting side and the cells the accepted action named,
        // so a board cycle is attributable to its actor from a delivered member
        // rather than from its position in the batch.
        var request = new SwapRequest(From, To);

        var result = SwapExecutor.Execute(BattleWith(MatchingBoard()), request);

        Assert.True(result.IsAccepted);
        Assert.True(result.Events.Count >= 3, "the boundary pair precedes the board cycle");

        Assert.Equal(BattleEventType.SwapStarted, result.Events[0].Type);
        Assert.Equal(DamageParty.Player, result.Events[0].SwapStarted.Actor);
        Assert.Equal(From, result.Events[0].SwapStarted.FromCell);
        Assert.Equal(To, result.Events[0].SwapStarted.ToCell);

        Assert.Equal(BattleEventType.SwapResolved, result.Events[1].Type);
        Assert.True(result.Events[1].SwapResolved.Committed);

        // The board cycle follows the pair, and the pair is not one of its events.
        Assert.Equal(BattleEventType.MatchCreated, result.Events[2].Type);
    }

    [Fact]
    public void SwapStarted_ShouldReportTheCellsTheActionNamed_InTheOrderItNamedThem()
    {
        // MATCH3_RULES.md §2.1.1 item 2: the unordered pair is the Swap's identity
        // and the action carries no direction field, so the two cells are reported as
        // the action supplied them — the same Swap is the same Swap either way, and
        // the event does not impose a canonical order on the wire.
        var forward = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(From, To));
        var reversed = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(To, From));

        Assert.True(forward.IsAccepted);
        Assert.True(reversed.IsAccepted);

        Assert.Equal((From, To), (forward.Events[0].SwapStarted.FromCell, forward.Events[0].SwapStarted.ToCell));
        Assert.Equal((To, From), (reversed.Events[0].SwapStarted.FromCell, reversed.Events[0].SwapStarted.ToCell));

        // The same unordered pair is one identity: the resolved board and the Combo
        // are the same, so only the reported order differs (MATCH3_RULES.md §2.1.1
        // item 2, §2.1.3 item 3). The board is compared field-by-field because
        // BoardState has reference equality (`DescribeState`).
        Assert.Equal(DescribeState(forward.State), DescribeState(reversed.State));
        Assert.Equal(forward.State.Combo, reversed.State.Combo);
    }

    [Theory]
    // INVALID_CELL_INDEX — out of range, and a cell swapped with itself.
    [InlineData(99, To)]
    [InlineData(From, From)]
    // INVALID_SWAP — diagonal, and non-adjacent across a row edge.
    [InlineData(13, 15)]
    [InlineData(0, 9)]
    // NO_MATCH_FROM_SWAP — adjacent but producing no Match.
    [InlineData(5, 6)]
    public void RejectedSwap_ShouldEmitNeitherSwapStartedNorSwapResolved(int from, int to)
    {
        // GAME_EVENTS.md §1.2 item 1 / MATCH3_RULES.md §2.1.5 item 6: a rejected
        // action emits no Battle Event at all — "not SwapStarted, not SwapResolved,
        // not any Match-3 event". The boundary pair is therefore absent by the same
        // rule that removes the board cycle, and not by a second check.
        var result = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(from, to));

        Assert.True(result.IsRejected);
        Assert.Empty(result.Events);
        Assert.DoesNotContain(result.Events, e => e.Type == BattleEventType.SwapStarted);
        Assert.DoesNotContain(result.Events, e => e.Type == BattleEventType.SwapResolved);
    }

    [Fact]
    public void BoundaryPayloads_ShouldBeReadableOnlyOnTheirOwnEvent()
    {
        // GAME_EVENTS.md §2 + SIGNALR_PROTOCOL.md §3.2.26–§3.2.27: the payload
        // accessors throw rather than returning a plausible default, so a wrong-kind
        // read is a defect and never a silent zero coordinate or a guessed actor.
        var result = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(From, To));

        var swapStarted = result.Events[0];
        var swapResolved = result.Events[1];
        var matchCreated = result.Events[2];

        Assert.Equal(BattleEventType.SwapStarted, swapStarted.Type);
        Assert.Equal(BattleEventType.SwapResolved, swapResolved.Type);

        Assert.Throws<InvalidOperationException>(() => swapStarted.SwapResolved);
        Assert.Throws<InvalidOperationException>(() => swapResolved.SwapStarted);
        Assert.Throws<InvalidOperationException>(() => matchCreated.SwapStarted);
        Assert.Throws<InvalidOperationException>(() => matchCreated.SwapResolved);
    }

    // =======================================================================
    // BossCardCast — the Boss Card action's own event (GAME_EVENTS.md §2;
    // BOSS_RULES.md §4.2 item 6, §7; SIGNALR_PROTOCOL.md §3.2.30; ADR-029 §2.8)
    // =======================================================================

    // The two approved identities of the Slice 3 fixture: the Boss Card definition
    // identity (the ability SkillId, CARD_RULES.md §1.1 item 3) and the Boss's
    // canonical technical Identity (BossState.BossId) — never a display name.
    private const string BossCardId = "earthquake";
    private const string BossSourceId = "boss-son-thach-ve";

    [Fact]
    public void BossCardCast_ShouldEmitTheDocumentedTypeAndOrdinal()
    {
        // GAME_EVENTS.md §2 names the event BossCardCast, and the enum's value is its
        // stable identity. It is member 20 — the next free ordinal after
        // MatchedGemSummary (19) when the Boss Card action was added (TASK-308).
        Assert.Equal(20, (int)BattleEventType.BossCardCast);

        var cardCast = BattleEvent.ForBossCardCast(BossCardId, BossSourceId);

        Assert.Equal(BattleEventType.BossCardCast, cardCast.Type);
        Assert.Equal(20, (int)cardCast.Type);

        // It is its own event kind: not the Player CardCast (GAME_EVENTS.md §2;
        // SIGNALR_PROTOCOL.md §3.2.30).
        Assert.NotEqual(BattleEventType.CardCast, cardCast.Type);
    }

    [Fact]
    public void BossCardCast_ShouldCarryExactlyTheTwoIdentitiesPassed()
    {
        // SIGNALR_PROTOCOL.md §3.2.30: cardId is the Boss Card definition identity
        // (the ability SkillId) and sourceId is the Boss's canonical technical
        // Identity BossState.BossId. Both are the caller's values, reported
        // unchanged — "earthquake" and "boss-son-thach-ve".
        var eventValue = BattleEvent.ForBossCardCast(BossCardId, BossSourceId);

        var payload = eventValue.BossCardCast;

        Assert.Equal(BossCardId, payload.CardId);
        Assert.Equal(BossSourceId, payload.SourceId);
        Assert.Equal(new BossCardCastEvent(BossCardId, BossSourceId), payload);

        // One identity, one spelling: the value the Boss Card event reports as
        // cardId is the ability's canonical SkillId (BOSS_RULES.md §6.4;
        // SIGNALR_PROTOCOL.md §3.2.30 item 1) — "earthquake" for Sơn Thạch Vệ.
        Assert.Equal(BossDefinitions.SonThachVe.SkillId, payload.CardId);
    }

    [Fact]
    public void BossCardCast_ShouldDescribeBothIdentitiesInToString()
    {
        // The diagnostic rendering names the event and both identities, so a failed
        // assertion run identifies the card and the Boss without extra inspection.
        var text = BattleEvent.ForBossCardCast(BossCardId, BossSourceId).ToString();

        Assert.Contains("BossCardCast", text, StringComparison.Ordinal);
        Assert.Contains(BossCardId, text, StringComparison.Ordinal);
        Assert.Contains(BossSourceId, text, StringComparison.Ordinal);
    }

    [Fact]
    public void BossCardCast_ShouldCarryNoCostPowerCooldownOrEffectMember()
    {
        // SIGNALR_PROTOCOL.md §3.2.30 item 2 / §4.4 item 12: the payload has the two
        // identities only. The Boss Power spend and the resulting Power/cooldown are
        // authoritative state in the bossState projection; a Boss Power spend emits
        // no PowerChanged (GAME_EVENTS.md §2). The factory therefore derives
        // nothing — no cost, Power value, cooldown, or effect summary.
        var payloadProperties = typeof(BossCardCastEvent)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["CardId", "SourceId"], payloadProperties);

        foreach (var forbidden in new[]
                 {
                     "Cost", "Power", "Cooldown", "Effect", "Magnitude", "Amount",
                     "Damage", "Shield", "Duration", "Turn",
                 })
        {
            Assert.DoesNotContain(payloadProperties, n => n.Contains(forbidden, StringComparison.Ordinal));
        }

        // And the event populates no other payload: only BossCardCast reads, and
        // every accessor belonging to a different event kind throws.
        var eventValue = BattleEvent.ForBossCardCast(BossCardId, BossSourceId);

        Assert.Throws<InvalidOperationException>(() => eventValue.Match);
        Assert.Throws<InvalidOperationException>(() => eventValue.CascadeDepth);
        Assert.Throws<InvalidOperationException>(() => eventValue.Combo);
        Assert.Throws<InvalidOperationException>(() => eventValue.Gem);
        Assert.Throws<InvalidOperationException>(() => eventValue.PassiveCharged);
        Assert.Throws<InvalidOperationException>(() => eventValue.PassiveTriggered);
        Assert.Throws<InvalidOperationException>(() => eventValue.DamageCalculated);
        Assert.Throws<InvalidOperationException>(() => eventValue.DamageDealt);
        Assert.Throws<InvalidOperationException>(() => eventValue.DamageTaken);
        Assert.Throws<InvalidOperationException>(() => eventValue.BattleWon);
        Assert.Throws<InvalidOperationException>(() => eventValue.BattleLost);
        Assert.Throws<InvalidOperationException>(() => eventValue.CardCast);
        Assert.Throws<InvalidOperationException>(() => eventValue.PetSkillCast);
        Assert.Throws<InvalidOperationException>(() => eventValue.RelicTriggered);
        Assert.Throws<InvalidOperationException>(() => eventValue.PowerChanged);
        Assert.Throws<InvalidOperationException>(() => eventValue.SwapStarted);
        Assert.Throws<InvalidOperationException>(() => eventValue.SwapResolved);
        Assert.Throws<InvalidOperationException>(() => eventValue.CascadeStep);
        Assert.Throws<InvalidOperationException>(() => eventValue.MatchedGemSummary);

        Assert.Equal(BossCardId, eventValue.BossCardCast.CardId);
        Assert.Equal(BossSourceId, eventValue.BossCardCast.SourceId);
    }

    [Fact]
    public void BossCardCast_ShouldBeReadableOnlyOnItsOwnEvent()
    {
        // The payload accessors throw rather than returning a plausible default, so a
        // wrong-kind read is a defect and never a silent empty cast — the same
        // contract BoundaryPayloads_ShouldBeReadableOnlyOnTheirOwnEvent pins for the
        // board boundary.
        var playerCardCast = BattleEvent.CreateCardCast("card-x", powerCost: 5);
        var swapStarted = BattleEvent.ForSwapStarted(DamageParty.Player, 25, 33);

        Assert.Throws<InvalidOperationException>(() => playerCardCast.BossCardCast);
        Assert.Throws<InvalidOperationException>(() => swapStarted.BossCardCast);

        // The Player CardCast payload and the Boss Card payload are different
        // contracts: reading CardCast on a Boss Card event throws too, so the Boss
        // cast is never mistaken for a Player cast (SIGNALR_PROTOCOL.md §3.2.30).
        var bossCardCast = BattleEvent.ForBossCardCast(BossCardId, BossSourceId);

        Assert.Throws<InvalidOperationException>(() => bossCardCast.CardCast);
    }

    // =======================================================================
    // Boundary — no second pipeline, no undocumented event, no event infrastructure
    // =======================================================================

    [Fact]
    public void EventTypes_ShouldBeExactlyTheDocumentedOnes()
    {
        // GAME_RULES.md §16 is the canonical event-name list and GAME_EVENTS.md §2
        // owns the detail. The Match-3 resolution produces the four names its cycle
        // places on it — MatchCreated, CascadeCreated, ComboChanged, GemMatched — and
        // the Passive stage adds the two names the same §16 list carries and
        // PASSIVE_RULES.md §7 defines: PassiveCharged and PassiveTriggered. The
        // Damage Pipeline (TASK-021) adds the three GAME_RULES.md §16 / GAME_EVENTS.md
        // §2 Damage names — DamageCalculated, DamageDealt, DamageTaken. The Boss
        // Response stage (TASK-022) adds the two BOSS_RULES.md §7 names —
        // BattleWon, BattleLost. The Card stages add the two
        // CARD_RULES.md §6 names — CardCast and PetSkillCast. The Relic stage
        // (GAME_RULES.md §17 step 11, RELIC_RULES.md §7) adds RelicTriggered, and the
        // Power stage's PowerChanged is its payload (GAME_EVENTS.md §2,
        // SIGNALR_PROTOCOL.md §3.2.23–§3.2.24) — both names are on §16's canonical
        // list, so this set is exactly that list. The committed board action's own
        // boundary adds the two names §16 has always carried and GAME_EVENTS.md §2
        // defines — SwapStarted and SwapResolved (SIGNALR_PROTOCOL.md
        // §3.2.26–§3.2.27) — which this task formalized rather than invented. The
        // per-pass Gravity + Spawn report adds the one name the operator authorized
        // for it — CascadeStepResolved (GAME_EVENTS.md §1.1 item 8, §2;
        // SIGNALR_PROTOCOL.md §3.2.28; ADR-027) — and the Match-3 cycle immediately
        // above is its home. The per-board-cycle matched-Gem report adds the one name
        // GAME_RULES.md §16 gained for it — MatchedGemSummary (GAME_EVENTS.md §1.1
        // item 9, §2; SIGNALR_PROTOCOL.md §3.2.29) — and the same cycle is its home.
        // The Boss Card action completes the Boss Response's two-form surface with the
        // one name GAME_EVENTS.md §2 and SIGNALR_PROTOCOL.md §3.2.30 already define —
        // BossCardCast (TASK-308, ADR-029 Slice 3); after ADR-029 Slice 5 / TASK-313
        // it is the Boss's sole cast event, replacing the retired BossSkillCast rather
        // than adding a second Boss event. No
        // undocumented name may be added
        // (AGENTS.md §7). In particular no MatchCountChanged, TurnChanged,
        // SequenceChanged, or BoardChanged exists.
        //
        // BOSS_RULES.md §7 is explicit about the three names the Boss stage did NOT
        // add: Boss Passive triggers reuse the shared PassiveCharged/PassiveTriggered
        // with source="boss" ("No Boss-specific passive event name is needed"), a Boss
        // Basic Attack is reported by its damage instance, and Boss state changes are
        // inferable from the event sequence. So BossPassiveCharged,
        // BossPassiveTriggered, BossBasicAttack, and BossEnraged are all absent — and
        // absent from this list by construction, which is what the assertions below
        // state directly. §3.2.24 item 4 and §3.2.23 item 5 likewise add no
        // CardCostChanged, CardCostModifierApplied, or ATKChanged: a Relic's resulting
        // state reaches the client through the BattleState projection.
        var names = Enum.GetNames<BattleEventType>().OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.Equal(
            [
                "BattleLost",
                "BattleWon",
                "BossCardCast",
                "CardCast",
                "CascadeCreated",
                "CascadeStepResolved",
                "ComboChanged",
                "DamageCalculated",
                "DamageDealt",
                "DamageTaken",
                "GemMatched",
                "MatchCreated",
                "MatchedGemSummary",
                "PassiveCharged",
                "PassiveTriggered",
                "PetSkillCast",
                "PowerChanged",
                "RelicTriggered",
                "SwapResolved",
                "SwapStarted",
            ],
            names);

        // The forbidden Boss-specific names are absent, stated explicitly rather than
        // left to the set comparison above.
        foreach (var forbidden in new[]
                 {
                     "BossPassiveCharged", "BossPassiveTriggered", "BossBasicAttack",
                     "BossEnraged", "BossStateChanged",
                 })
        {
            Assert.DoesNotContain(forbidden, names);
        }
    }

    [Fact]
    public void EventEmission_ShouldIntroduceNoEventInfrastructure()
    {
        // TASK-006 / AGENTS.md §9: the smallest domain representation only. No event
        // bus, broker, emitter, publisher, subscription framework, or persistence may
        // be introduced merely to support this task.
        var domainTypes = typeof(BattleEvent).Assembly
            .GetTypes()
            .Select(t => t.Name)
            .ToArray();

        foreach (var forbidden in new[]
                 {
                     "IEventBus", "EventBus", "EventEmitter", "IEventEmitter",
                     "IEventPublisher", "EventPublisher", "IEventStore", "EventLog",
                     "IMessageBroker", "MessageBroker", "IEventHandler", "EventSubscriber",
                     // Undocumented event names (GAME_EVENTS.md §2, GAME_RULES.md §16).
                     "MatchCountChanged", "TurnChanged", "SequenceChanged",
                     "BoardChanged", "BoardResolved", "CascadeUpdated",
                 })
        {
            Assert.DoesNotContain(forbidden, domainTypes);
        }
    }

    [Fact]
    public void EventBuilder_ShouldRunNoSecondDetectionPass()
    {
        // The builder reads Resolution.Passes and nothing else: it must not expose a
        // way to detect Matches, resolve a board, or assemble its own pass. Its only
        // public operation is Build.
        var methods = typeof(BattleEventBuilder)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Select(m => m.Name)
            .Distinct()
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Build"], methods);
    }

    [Fact]
    public void Executor_ShouldStillExposeOnlyItsSingleEntryPoint()
    {
        // No second resolution pipeline exists: event emission is inside the existing
        // Execute, so the executor still exposes exactly one public operation.
        var methods = typeof(SwapExecutor)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Select(m => m.Name)
            .Distinct()
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Execute"], methods);
    }

    [Fact]
    public void Events_ShouldBeCarriedOnTheExistingResultNotASecondPipeline()
    {
        // The events ride the result the executor already returns — the same value
        // that carries the committed state and the resolution. There is no separate
        // event-producing call a caller could run instead of resolving.
        var result = SwapExecutor.Execute(CascadeBattle(), new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(result.IsAccepted);

        // One resolution, one state, one event stream — all from one call.
        Assert.NotEmpty(result.Resolution.Passes);
        Assert.NotNull(result.State);
        Assert.NotEmpty(result.Events);

        // The events' Match total is the resolution's own.
        Assert.Equal(
            result.Resolution.Passes.Sum(p => p.Matches.Count),
            result.Events.Count(e => e.Type == BattleEventType.MatchCreated));
    }

    [Fact]
    public void EventPayloadAccessors_ShouldRejectTheWrongEventKind()
    {
        // A CascadeCreated has no Match and a MatchCreated has no cascade depth:
        // reading the payload that does not belong to the event's kind is a defect,
        // not a plausible default.
        var result = SwapExecutor.Execute(CascadeBattle(), new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(result.IsAccepted);

        var cascade = result.Events.First(e => e.Type == BattleEventType.CascadeCreated);
        var match = result.Events.First(e => e.Type == BattleEventType.MatchCreated);
        var combo = result.Events.First(e => e.Type == BattleEventType.ComboChanged);
        var gem = result.Events.First(e => e.Type == BattleEventType.GemMatched);

        Assert.Throws<InvalidOperationException>(() => cascade.Match);
        Assert.Throws<InvalidOperationException>(() => cascade.Combo);
        Assert.Throws<InvalidOperationException>(() => match.CascadeDepth);
        Assert.Throws<InvalidOperationException>(() => combo.Gem);
        Assert.Throws<InvalidOperationException>(() => gem.Match);

        // The right payload for each kind reads back.
        Assert.Equal(1, cascade.CascadeDepth);
        Assert.Equal(1, combo.Combo);
        Assert.True(gem.Gem.CellIndex >= 0);
        Assert.Equal(1, match.Match.CascadeDepth);
    }

    [Fact]
    public void Events_ShouldBeAnEmptyListForARejectionNeverNull()
    {
        // The absence of events is a value, not a missing one: a rejection's Events is
        // an empty list, so a caller never has to distinguish "no events" from "not
        // applicable" (GAME_EVENTS.md §1.2).
        var result = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(99, To));

        Assert.True(result.IsRejected);
        Assert.NotNull(result.Events);
        Assert.Empty(result.Events);
        Assert.Same(result.Events, result.Events);
    }

    // ------------------------------------------------------------- helpers ---

    /// <summary>
    /// The number of <c>ComboChanged</c> events — the Swap's Match total as the
    /// event stream reports it (<c>MATCH3_RULES.md</c> §6.2 item 1).
    /// </summary>
    private static int ComboCount(SwapExecutionResult result) =>
        result.Events.Count(e => e.Type == BattleEventType.ComboChanged);

    /// <summary>
    /// A value-level description of an event sequence, so two runs are compared as
    /// whole sequences rather than by reference (<c>AGENTS.md</c> §11).
    /// </summary>
    private static string[] Describe(IReadOnlyList<BattleEvent> events) =>
        events.Select(e => e.Type switch
        {
            BattleEventType.MatchCreated =>
                $"MatchCreated depth={e.Match.CascadeDepth} start={e.Match.Shape.StartIndex} "
                + $"type={e.Match.Shape.GemType} cells={string.Join(',', e.Match.Shape.Cells)}",
            BattleEventType.CascadeCreated => $"CascadeCreated depth={e.CascadeDepth}",
            BattleEventType.ComboChanged => $"ComboChanged combo={e.Combo}",
            // The committed board action's own boundary (GAME_EVENTS.md §2,
            // SIGNALR_PROTOCOL.md §3.2.26–§3.2.27). It has its own arms because the
            // fallback arm below reads `Gem`, which a boundary event does not carry.
            BattleEventType.SwapStarted =>
                $"SwapStarted actor={e.SwapStarted.Actor} "
                + $"from={e.SwapStarted.FromCell} to={e.SwapStarted.ToCell}",
            BattleEventType.SwapResolved => $"SwapResolved committed={e.SwapResolved.Committed}",
            // The pass's completed Gravity + Spawn transition (GAME_EVENTS.md §1.1,
            // §2; SIGNALR_PROTOCOL.md §3.2.28). It has its own arm because the
            // fallback arm below reads `Gem`, which it does not carry — and because
            // two runs must be compared on the movement and spawn report too, not
            // only on the fact that the event occurred.
            BattleEventType.CascadeStepResolved =>
                $"CascadeStepResolved depth={e.CascadeStep.PassDepth} "
                + $"drops={string.Join(',', e.CascadeStep.DropMoves.Select(m => $"{m.FromCell}>{m.ToCell}"))} "
                + $"spawns={string.Join(',', e.CascadeStep.SpawnedGems.Select(s => $"{s.CellIndex}:{s.GemType}"))}",
            _ => $"GemMatched cell={e.Gem.CellIndex} type={e.Gem.GemType}",
        }).ToArray();

    /// <summary>
    /// A value-level description of a state, for field-by-field comparison without
    /// relying on <c>BoardState</c>'s reference equality.
    /// </summary>
    private static string DescribeState(BattleState state) =>
        $"{state.BattleId}|{state.Turn}|{state.Sequence}|{state.RngSeed}|{state.RngState}"
        + $"|{state.Combo}|{state.MatchCount}|{state.PetState}|{state.LastCommittedSwapPair}"
        + $"|{string.Join(',', state.BoardState.Cells)}";

    /// <summary>The index of the first event of a kind, or <c>-1</c>.</summary>
    private static int IndexOfType(IReadOnlyList<BattleEvent> events, BattleEventType type)
    {
        for (var i = 0; i < events.Count; i++)
        {
            if (events[i].Type == type)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The index of one specific event value, or <c>-1</c>.</summary>
    private static int IndexOf(IReadOnlyList<BattleEvent> events, BattleEvent target)
    {
        for (var i = 0; i < events.Count; i++)
        {
            if (events[i].Equals(target))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// An adjacent pair of the battle's current board whose exchange produces a §3
    /// Match and which is not the excluded (already committed) pair
    /// (<c>MATCH3_RULES.md</c> §1.4, §2.1.4).
    /// </summary>
    private static SwapRequest FindAdjacentPairThatProducesAMatch(
        BattleState state,
        CommittedSwapPair? exclude)
    {
        foreach (var (from, to) in AllAdjacentPairs())
        {
            if (exclude is { } skip && skip.Matches(CommittedSwapPair.FromCells(from, to)))
            {
                continue;
            }

            if (MatchDetector.Detect(state.BoardState.WithSwapped(from, to)).Count > 0)
            {
                return new SwapRequest(from, to);
            }
        }

        throw new InvalidOperationException(
            "A generated board has at least one valid Swap (MATCH3_RULES.md §1.4).");
    }

    /// <summary>
    /// Every orthogonally adjacent pair of the 8x8 board, each once — the 112 pairs
    /// a player can name (<c>MATCH3_RULES.md</c> §1.0).
    /// </summary>
    private static IEnumerable<(int From, int To)> AllAdjacentPairs()
    {
        for (var index = 0; index < BoardState.CellCount; index++)
        {
            var right = BoardState.ToColumn(index) + 1 < BoardState.Columns ? index + 1 : -1;
            var down = index + BoardState.Width < BoardState.CellCount ? index + BoardState.Width : -1;

            if (right >= 0)
            {
                yield return (index, right);
            }

            if (down >= 0)
            {
                yield return (index, down);
            }
        }
    }
}