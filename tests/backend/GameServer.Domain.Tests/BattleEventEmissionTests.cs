using GameServer.Domain.Battle;
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
///     GemMatched            the pass's matched cells, ascending §1.0 index
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

    /// <summary>A battle whose board is the single-Match fixture.</summary>
    private static BattleState BattleWith(BoardState board, PlayerState? playerState = null) =>
        BattleState.CreateWith("battle-006", TestSeed) with
        {
            BoardState = board,
            PlayerState = playerState ?? PlayerState.Initial,
        };

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

    private static BattleState CascadeBattle(PlayerState? playerState = null) =>
        BattleState.CreateWith("battle-006-cascade", CascadeSeed) with
        {
            BoardState = CascadeBoard(),
            PlayerState = playerState ?? PlayerState.Initial,
        };

    /// <summary>
    /// A battle whose board is <see cref="MatchingBoard"/> with a horizontal Line
    /// Clear Gem held at <c>I(4,0)</c> — a cell the completing swap's match set
    /// consumes (<c>MATCH3_RULES.md</c> §5.5.5 item 1), so the swap activates it and
    /// its effect clears the rest of the row.
    /// </summary>
    private static BattleState BoardWithAHeldLineClearGem() =>
        BattleWith(MatchingBoard().WithSpecial(
            (I(4, 0), SpecialGem.LineClear(SpecialGemOrientation.Horizontal))));

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
        var expected = result.Resolution.Passes[0].MatchedCellGemMatched;

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
                Pcg32.FromSeed(state.RngState.State),
                swapOriginIndex: null);

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

        // Each ComboChanged sits immediately after its own MatchCreated (the last
        // GemMatched of that Match's matched cells, when there are any, comes
        // between them) and before the next MatchCreated.
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
        // equals the accounted PlayerState.Combo — the already-updated authoritative
        // value, not a recomputed one (GAME_STATE.md §2.2).
        var result = SwapExecutor.Execute(CascadeBattle(), new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(result.IsAccepted);

        var combos = result.Events.Where(e => e.Type == BattleEventType.ComboChanged).ToArray();

        Assert.NotEmpty(combos);
        Assert.Equal(result.State.PlayerState.Combo, combos[^1].Combo);
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
        // §1.1's cycle, asserted structurally over a real multi-pass resolution:
        //
        //   per pass:  [CascadeCreated]  MatchCreated, GemMatched…, ComboChanged …
        //   GemMatched for the pass's activations last
        //
        // The expected sequence is built from the resolution's own report, so the
        // assertion is "the events are exactly the documented rendering of this
        // resolution" rather than "this board produces this literal list".
        var result = SwapExecutor.Execute(CascadeBattle(), new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(result.IsAccepted);

        var expectedTypes = new List<BattleEventType>();

        foreach (var pass in result.Resolution.Passes)
        {
            if (pass.Matches[0].CascadeDepth >= 2)
            {
                expectedTypes.Add(BattleEventType.CascadeCreated);
            }

            foreach (var _ in pass.Matches)
            {
                expectedTypes.Add(BattleEventType.MatchCreated);

                for (var i = 0; i < pass.MatchedCellGemMatched.Count; i++)
                {
                    expectedTypes.Add(BattleEventType.GemMatched);
                }

                expectedTypes.Add(BattleEventType.ComboChanged);
            }

            for (var i = 0; i < pass.ActivationCellGemMatched.Count; i++)
            {
                expectedTypes.Add(BattleEventType.GemMatched);
            }
        }

        Assert.Equal(expectedTypes, result.Events.Select(e => e.Type));
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
            .SelectMany(p => p.MatchedCellGemMatched.Concat(p.ActivationCellGemMatched))
            .Select(g => g.CellIndex)
            .ToArray();

        Assert.Equal(
            reportedCells,
            result.Events
                .Where(e => e.Type == BattleEventType.GemMatched)
                .Select(e => e.Gem.CellIndex));
    }

    // =======================================================================
    // Special Gems — MATCH3_RULES.md §5.5.5 item 8, GAME_EVENTS.md §2 item 3
    // =======================================================================

    /// <summary>
    /// A Match 4 fixture: row 5 holds 'P','P','D','P' at columns 0-3 with the
    /// completing 'P' at (4,2). The completing Swap makes row 5 'PPPP' — a Match 4,
    /// which creates exactly one Line Clear Gem (<c>MATCH3_RULES.md</c> §5.2).
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
    public void SpecialGemCreation_ShouldNotEmitAnAdditionalMatchCreated()
    {
        // §2 MatchCreated / MATCH3_RULES.md §5.4 item 4: one shape is one Match
        // however many Special Gems it creates — the creation is a consequence of
        // the Match, never an additional one. §6.3 item 3: creation never increments
        // Combo either.
        var result = SwapExecutor.Execute(BattleWith(MatchFourBoard()), new SwapRequest(MatchFourFrom, MatchFourTo));

        Assert.True(result.IsAccepted);

        var created = result.Resolution.Passes.Sum(p => p.CreatedSpecialGems.Count);
        Assert.True(created > 0, "the fixture must create a Special Gem");

        var matches = result.Resolution.Passes.Sum(p => p.Matches.Count);
        Assert.Equal(matches, result.Events.Count(e => e.Type == BattleEventType.MatchCreated));
        Assert.Equal(matches, result.Events.Count(e => e.Type == BattleEventType.ComboChanged));
    }

    [Fact]
    public void SpecialGemActivation_ShouldNotGenerateAnUndocumentedMatchEvent()
    {
        // §2 item 3: "Activation is reported through this existing event and the
        // existing state push — no SpecialGemActivated event or message exists."
        // MATCH3_RULES.md §5.5.5 item 8: a Gem cleared by an activation produces
        // GemMatched but no MatchCreated, and §6.3.1: no Combo change.
        //
        // The held Line Clear Gem at I(4,0) sits in a cell the completing swap's
        // match set consumes, so the pass activates it and its effect clears the rest
        // of row 4 — cells the match set did not name.
        var result = SwapExecutor.Execute(BoardWithAHeldLineClearGem(), new SwapRequest(From, To));

        Assert.True(result.IsAccepted);

        // The pass activated the held Special Gem...
        var activated = result.Resolution.Passes.Sum(p => p.ActivatedSpecialGems.Count);
        Assert.True(activated > 0, "the fixture must activate a Special Gem");

        // ...and the cells its activation cleared beyond the match set produced
        // GemMatched events rather than Match events.
        var activationReports = result.Resolution.Passes.Sum(p => p.ActivationCellGemMatched.Count);
        Assert.True(activationReports > 0, "the fixture's activation must clear extra cells");

        // The Match events still number exactly the Matches — the activation added
        // none, and no undocumented event kind appeared.
        var matches = result.Resolution.Passes.Sum(p => p.Matches.Count);
        Assert.Equal(matches, result.Events.Count(e => e.Type == BattleEventType.MatchCreated));
        Assert.Equal(matches, result.Events.Count(e => e.Type == BattleEventType.ComboChanged));

        // The activation's cells are reported as GemMatched, once each.
        var activationCells = result.Resolution.Passes
            .SelectMany(p => p.ActivationCellGemMatched)
            .Select(g => g.CellIndex)
            .ToArray();

        Assert.Equal(
            activationCells,
            result.Events
                .Where(e => e.Type == BattleEventType.GemMatched)
                .Select(e => e.Gem.CellIndex)
                .Where(i => activationCells.Contains(i)));
    }

    [Fact]
    public void GemMatched_ShouldReportTheSpecialGemConsumedAtThatCell()
    {
        // §2 item 2: "`Special Gem consumed at that cell` is present only when the
        // cell that was cleared held a Special Gem … read from the pre-removal
        // board". §2 item 1: the Gem type is still one of the four types.
        var result = SwapExecutor.Execute(BoardWithAHeldLineClearGem(), new SwapRequest(From, To));

        Assert.True(result.IsAccepted);

        var consumed = result.Events
            .Where(e => e.Type == BattleEventType.GemMatched && e.Gem.ConsumedSpecialGem is not null)
            .ToArray();

        Assert.NotEmpty(consumed);
        Assert.Equal(I(4, 0), consumed[0].Gem.CellIndex);
        Assert.Equal(SpecialGemType.LineClear, consumed[0].Gem.ConsumedSpecialGem!.Value.Type);

        // The Gem type is always a real §1.1 type, never a Special Gem type in its
        // place (§2 item 1).
        Assert.All(
            result.Events.Where(e => e.Type == BattleEventType.GemMatched),
            e => Assert.True(GemTypes.IsValid((int)e.Gem.GemType)));

        // An ordinary Gem reports no consumed Special Gem — its absence is the
        // statement that none was consumed there.
        Assert.Contains(
            result.Events,
            e => e.Type == BattleEventType.GemMatched && e.Gem.ConsumedSpecialGem is null);
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
        var state = BattleWith(CascadeBoard(), PlayerState.Initial with { Combo = 3, MatchCount = 11 });

        var result = SwapExecutor.Execute(state, new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(result.IsAccepted);
        Assert.NotEmpty(result.Events);

        // The input state object is untouched: every authoritative field is exactly
        // as it was (GAME_STATE.md §5.1, MATCH3_RULES.md §2.1.5).
        Assert.Equal("battle-006", state.BattleId);
        Assert.Equal(BattleState.InitialTurn, state.Turn);
        Assert.Equal(BattleState.InitialSequence, state.Sequence);
        Assert.Equal(TestSeed, state.RngSeed);
        Assert.Equal(PlayerState.Initial with { Combo = 3, MatchCount = 11 }, state.PlayerState);
        Assert.Null(state.LastCommittedSwapPair);
        Assert.True(state.BoardState.CellsEqual(CascadeBoard()));

        // And the events describe the resulting state without being it: the state's
        // own accounted Combo is what the event run ends at.
        Assert.Equal(ComboCount(result), result.State.PlayerState.Combo);
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
            rng,
            swapOriginIndex: null);

        var result = SwapExecutor.Execute(state, new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(result.IsAccepted);
        Assert.Equal(reference.Passes.Count, result.Resolution.Passes.Count);

        for (var i = 0; i < reference.Passes.Count; i++)
        {
            Assert.Equal(reference.Passes[i].Matches.Count, result.Resolution.Passes[i].Matches.Count);
            Assert.Equal(
                reference.Passes[i].MatchedCellGemMatched.Count,
                result.Resolution.Passes[i].MatchedCellGemMatched.Count);
            Assert.Equal(
                reference.Passes[i].ActivationCellGemMatched.Count,
                result.Resolution.Passes[i].ActivationCellGemMatched.Count);
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
        // §7.1: "Given the same board state and the same Swap input, match detection,
        // cascade resolution and special gem creation must be fully deterministic".
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
        // same swap, and the event sequence reports the resolution, which is the same.
        var state = CascadeBattle();

        var forward = SwapExecutor.Execute(state, new SwapRequest(CascadeFrom, CascadeTo));
        var reversed = SwapExecutor.Execute(state, new SwapRequest(CascadeTo, CascadeFrom));

        Assert.True(forward.IsAccepted);
        Assert.True(reversed.IsAccepted);
        Assert.Equal(Describe(forward.Events), Describe(reversed.Events));
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
                "CascadeDepth",
                "Combo",
                "DamageCalculated",
                "DamageDealt",
                "DamageTaken",
                "Gem",
                "Match",
                "PassiveCharged",
                "PassiveTriggered",
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
                     typeof(ActivatedSpecialGem),
                     typeof(SpecialGemClaim),
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
        // state already holds, the progress value, and the Threshold (GAME_EVENTS.md
        // §2) — and no generated identifier beyond that identity.
        foreach (var type in new[] { typeof(PassiveChargedEvent), typeof(PassiveTriggeredEvent) })
        {
            var names = type
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(["PassiveId", "Progress", "Threshold"], names);

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
        // §2 GemMatched is once per cleared cell over each sub-step's union; the
        // resolver already reported exactly those cells, so the event count is their
        // count — no cell added, none dropped.
        var result = SwapExecutor.Execute(CascadeBattle(), new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(result.IsAccepted);

        var expected = result.Resolution.Passes
            .Sum(p => p.MatchedCellGemMatched.Count + p.ActivationCellGemMatched.Count);

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
        // a cell is cleared once per step and reported once (§5.8.2 item 2,
        // §1.3 item 2). Asserted per sub-step, because a cell matched in one pass can
        // legitimately be cleared again by a later pass's activation.
        foreach (var pass in result.Resolution.Passes)
        {
            Assert.Equal(
                pass.MatchedCellGemMatched.Count,
                pass.MatchedCellGemMatched.Select(g => g.CellIndex).Distinct().Count());

            Assert.Equal(
                pass.ActivationCellGemMatched.Count,
                pass.ActivationCellGemMatched.Select(g => g.CellIndex).Distinct().Count());

            // The activation sub-step excludes what the matched sub-step reported
            // (§1.3 item 1: the pass's matched cells first, then the activations).
            Assert.Empty(
                pass.ActivationCellGemMatched
                    .Select(g => g.CellIndex)
                    .Intersect(pass.MatchedCellGemMatched.Select(g => g.CellIndex)));
        }

        // And within one pass the two sub-steps together report each cell once.
        var firstPass = result.Resolution.Passes[0];
        var firstPassCells = firstPass.MatchedCellGemMatched
            .Concat(firstPass.ActivationCellGemMatched)
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

            Assert.Equal(result.State.PlayerState.Combo, combos[^1].Combo);

            // The cumulative total is state, not an event value (§2.2, §3 item 7).
            if (turn > 0)
            {
                Assert.True(result.State.PlayerState.MatchCount > result.State.PlayerState.Combo);
            }

            state = result.State;
        }
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
        // §2 Damage names — DamageCalculated, DamageDealt, DamageTaken. No
        // undocumented name may be added (AGENTS.md §7). In particular no
        // MatchCountChanged, SpecialGemActivated, SpecialGemCreated, TurnChanged,
        // SequenceChanged, or BoardChanged exists, and the stages that own
        // PowerChanged, RelicTriggered, CardCast, PetSkillCast, BossSkillCast, and
        // BattleWon/BattleLost have not added theirs here.
        var names = Enum.GetNames<BattleEventType>().OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.Equal(
            [
                "CascadeCreated",
                "ComboChanged",
                "DamageCalculated",
                "DamageDealt",
                "DamageTaken",
                "GemMatched",
                "MatchCreated",
                "PassiveCharged",
                "PassiveTriggered",
            ],
            names);
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
                     "MatchCountChanged", "SpecialGemActivated", "SpecialGemCreated",
                     "SpecialGemDestroyed", "TurnChanged", "SequenceChanged",
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
                + $"type={e.Match.Shape.GemType} cells={string.Join(',', e.Match.Shape.Cells)} "
                + $"created={string.Join(',', e.Match.CreatedSpecialGems.Select(c => c.CellIndex))}",
            BattleEventType.CascadeCreated => $"CascadeCreated depth={e.CascadeDepth}",
            BattleEventType.ComboChanged => $"ComboChanged combo={e.Combo}",
            _ => $"GemMatched cell={e.Gem.CellIndex} type={e.Gem.GemType} "
                 + $"special={e.Gem.ConsumedSpecialGem?.Type.ToString() ?? "none"}",
        }).ToArray();

    /// <summary>
    /// A value-level description of a state, for field-by-field comparison without
    /// relying on <c>BoardState</c>'s reference equality.
    /// </summary>
    private static string DescribeState(BattleState state) =>
        $"{state.BattleId}|{state.Turn}|{state.Sequence}|{state.RngSeed}|{state.RngState}"
        + $"|{state.PlayerState}|{state.LastCommittedSwapPair}"
        + $"|{string.Join(',', state.BoardState.Cells.Select(c => $"{c.GemType}:{c.SpecialGem?.Type.ToString() ?? "-"}"))}";

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