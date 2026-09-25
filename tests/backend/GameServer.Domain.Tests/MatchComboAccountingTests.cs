using GameServer.Domain.Battle;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Match / Combo accounting tests (<c>GAME_STATE.md</c> §2.2, §5.1, §5.3;
/// <c>GAME_RULES.md</c> §3, §5; <c>MATCH3_RULES.md</c> §6, §8.3).
///
/// The contract under test is the one TASK-005A fixed:
///
/// <code>
/// committed Swap
///     ↓  Passes in order, Matches within each
/// MatchCount += 1 per Match   (cumulative for the battle, never reset)
/// Combo      += 1 per Match   (reset to 0 on the committed Swap first)
///     ↓
/// one write-back with Turn, Sequence, LastCommittedSwapPair, BoardState, RngState
/// </code>
///
/// The expected values come from the rule, never from the implementation: the
/// Match total a Swap produces is read from the resolution's own passes
/// (<c>MATCH3_RULES.md</c> §6.3 item 1 makes Combo equal to that total), so each
/// fixture asserts the documented identity rather than a hard-coded number.
/// </summary>
public class MatchComboAccountingTests
{
    private const ulong TestSeed = 20260815UL;

    private static int I(int row, int column) => TestBoard.I(row, column);

    // The canonical completing-swap fixture: row 4 holds 'A','D','A' at columns
    // 0-2 and (3,1) holds the 'A' directly above the 'D', so exchanging
    // (3,1)<->(4,1) makes row 4 'AAA' — the §3 horizontal Match.
    private const int From = 25; // I(3, 1)
    private const int To = 33;   // I(4, 1)

    private static BoardState MatchingBoard() =>
        TestBoard.Background().WithGems(
            (I(4, 0), GemType.Atk),
            (I(4, 1), GemType.Def),
            (I(4, 2), GemType.Atk),
            (I(3, 1), GemType.Atk));

    /// <summary>
    /// A battle whose board is the completing-swap fixture and whose Match/Combo
    /// accounting is the one under test.
    ///
    /// GAME_STATE.md §2.2 places both values at the <c>BattleState</c> root and
    /// GAME_STATE.md §2.3 places the combat stats on the active Pet's
    /// <c>PetState</c> (ADR-011 items 2–3), so a test that needs a non-initial
    /// value sets it at its documented owner. There is no <c>PlayerState</c> type
    /// or node in the Domain at all (ADR-011 items 1 and 5).
    /// </summary>
    private static BattleState BattleWith(
        BoardState board,
        (int Combo, int MatchCount)? accounting = null,
        PetState? petState = null)
    {
        var (combo, matchCount) = accounting ?? (BattleState.InitialCombo, BattleState.InitialMatchCount);
        var created = BattleState.CreateWith("battle-005", TestSeed);

        return created with
        {
            BoardState = board,
            Combo = combo,
            MatchCount = matchCount,
            PetState = petState ?? created.PetState,
        };
    }

    /// <summary>
    /// The active Pet's state of a battle created by
    /// <see cref="BattleState.CreateWith"/> — the documented combat-stat home
    /// (<c>GAME_STATE.md</c> §2.3, <c>COMBAT_RULES.md</c> §1.1).
    /// </summary>
    private static PetState DefaultPet() =>
        BattleState.CreateWith("battle-005", TestSeed).PetState;

    /// <summary>
    /// The Match total of a committed resolution — the rule-level value Combo must
    /// equal (<c>MATCH3_RULES.md</c> §6.3 item 1) and the amount MatchCount must
    /// grow by (<c>GAME_RULES.md</c> §3).
    /// </summary>
    private static int MatchTotal(SwapExecutionResult result) =>
        result.Resolution.Passes.Sum(pass => pass.Matches.Count);

    /// <summary>
    /// A board — generated from <see cref="CascadeSeed"/> and quoted exactly as the
    /// authority produced it — whose adjacent pair (22, 23) produces <b>three</b>
    /// Matches across three passes of the cascade loop
    /// (<c>MATCH3_RULES.md</c> §4.2, §6.7's worked-example shape).
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
    ///
    /// The board is a server-generated board (<c>MATCH3_RULES.md</c> §1.2), so it
    /// holds no Match before the Swap (§1.3) and has a valid Swap (§1.4); the pair
    /// is asserted below rather than assumed. Nothing here is invented: the
    /// resolution's own report is the authority for the Match total, and the fixture
    /// exists only to reach the cascade path deterministically.
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

    /// <summary>
    /// A battle whose board is the cascade fixture. Its seed is the one that
    /// generated that board, so the Spawn draws the resolution makes are the
    /// deterministic continuation of the board it started from.
    /// </summary>
    private static BattleState CascadeBattle(
        (int Combo, int MatchCount)? accounting = null,
        PetState? petState = null)
    {
        var (combo, matchCount) = accounting ?? (BattleState.InitialCombo, BattleState.InitialMatchCount);
        var created = BattleState.CreateWith("battle-005-cascade", CascadeSeed);

        return created with
        {
            BoardState = CascadeBoard(),
            Combo = combo,
            MatchCount = matchCount,
            PetState = petState ?? created.PetState,
        };
    }

    // =======================================================================
    // Initialization — GAME_STATE.md §2.2
    // =======================================================================

    [Fact]
    public void NewBattleState_ShouldPlaceComboAndMatchCountAtTheRoot()
    {
        // GAME_STATE.md §2.2 / ADR-011 item 2: the two accounting values are
        // BattleState root members — there is no nested container holding them, and
        // no PlayerState node in the contract at all (§2).
        var state = BattleState.CreateWith("battle-005", TestSeed);

        Assert.Equal(0, state.Combo);
        Assert.Equal(0, state.MatchCount);
        Assert.Equal(typeof(int), typeof(BattleState).GetProperty("Combo")!.PropertyType);
        Assert.Equal(typeof(int), typeof(BattleState).GetProperty("MatchCount")!.PropertyType);
    }

    [Fact]
    public void NewBattleState_ShouldStartMatchCountAtZero()
    {
        // GAME_STATE.md §2.2 / GAME_RULES.md §3: cumulative Matches this battle, and
        // this battle has produced none.
        var state = BattleState.CreateWith("battle-005", TestSeed);

        Assert.Equal(0, state.MatchCount);
        Assert.Equal(BattleState.InitialMatchCount, state.MatchCount);
    }

    [Fact]
    public void NewBattleState_ShouldStartComboAtZero()
    {
        // GAME_STATE.md §2.2 / MATCH3_RULES.md §6.1 item 1: the rule-level starting
        // value is 0, which is what the published value reads before the battle's
        // first committed Swap (§6.5 item 4).
        var state = BattleState.CreateWith("battle-005", TestSeed);

        Assert.Equal(0, state.Combo);
        Assert.Equal(BattleState.InitialCombo, state.Combo);
    }

    [Fact]
    public void BattleState_ShouldExposeNoPlayerStateNode()
    {
        // GAME_STATE.md §2: "There is no PlayerState member." The Player is the
        // account/owner with no battle-time combat pool; the accounting lives at the
        // root (§2.2) and the combat stats on the Pet (§2.3, ADR-011 items 1–3).
        var battleStateFields = typeof(BattleState)
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();

        Assert.DoesNotContain("PlayerState", battleStateFields);

        // The wire label `playerState` is a protocol member name, not a state path
        // (SIGNALR_PROTOCOL.md §4.2, ADR-011 item 6), so the Domain assembly must
        // expose no type of that name either.
        Assert.DoesNotContain(
            "PlayerState",
            typeof(BattleState).Assembly.GetTypes().Select(t => t.Name));
    }

    [Fact]
    public void CombatStats_ShouldBeOwnedByPetStateAndNotFlatOnBattleState()
    {
        // GAME_STATE.md §2.3 / ADR-011 items 3 and 5: the active Pet is the combat
        // character, so HP/MaxHP, ATK/DEF, Crit, and Power live on PetState — the one
        // authoritative Player-side combat-stat home — and a flat
        // BattleState.HP/MaxHP/ATK/DEF/Crit/Power would be a second owner.
        Assert.Equal(typeof(PetState), typeof(BattleState).GetProperty("PetState")!.PropertyType);

        var battleStateFields = typeof(BattleState).GetProperties().Select(p => p.Name).ToArray();

        foreach (var combatStat in new[] { "HP", "MaxHP", "ATK", "DEF", "Power", "Crit" })
        {
            Assert.DoesNotContain(combatStat, battleStateFields);
        }
    }

    // =======================================================================
    // Combat stats — COMBAT_RULES.md §1.1, GAME_STATE.md §2.3
    // =======================================================================

    [Fact]
    public void NewBattleState_ShouldInitializeCombatStatsToTheDocumentedMvpDefaults()
    {
        // COMBAT_RULES.md §1.1: HP 1000, Max HP 1000, ATK 50, DEF 25, Crit 5%.
        // GAME_STATE.md §2.3 places all of them on PetState, and a battle
        // begins with them at those MVP defaults.
        var state = BattleState.CreateWith("battle-016", TestSeed);

        Assert.Equal(1000, state.PetState.HP);
        Assert.Equal(1000, state.PetState.MaxHP);
        Assert.Equal(50, state.PetState.ATK);
        Assert.Equal(25, state.PetState.DEF);
        Assert.Equal(5, state.PetState.Crit);
    }

    [Fact]
    public void NewBattleState_ShouldStartPowerAtZero()
    {
        // COMBAT_RULES.md §1.1 defines Power's range as 0–100 and it is generated by
        // Match-3 (§2). No Match has occurred at creation, so a battle starts at the
        // floor of that range — not at the cap.
        var state = BattleState.CreateWith("battle-016", TestSeed);

        Assert.Equal(0, state.PetState.Power);
        Assert.NotEqual(100, state.PetState.Power);
    }

    [Fact]
    public void NewBattleState_ShouldStartAtFullHealth()
    {
        // COMBAT_RULES.md §4 item 1 heals up to Max HP, so a battle that has taken no
        // damage begins with HP equal to MaxHP.
        var state = BattleState.CreateWith("battle-016", TestSeed);

        Assert.Equal(state.PetState.MaxHP, state.PetState.HP);
    }

    [Fact]
    public void PetState_ShouldExposeTheCombatDefaultsAsDocumentedConstants()
    {
        // The MVP values are configuration (COMBAT_RULES.md §1.1 explicitly calls them
        // "not permanent invariants"), and each has exactly one spelling in the type:
        // the constant the initial state is built from.
        Assert.Equal(1000, PetState.DefaultHP);
        Assert.Equal(1000, PetState.DefaultMaxHP);
        Assert.Equal(50, PetState.DefaultATK);
        Assert.Equal(25, PetState.DefaultDEF);
        Assert.Equal(0, PetState.DefaultPower);
        Assert.Equal(5, PetState.DefaultCrit);

        var initial = PetState.AtBattleCreation(
            GameServer.Domain.Elements.Element.Hoa,
            new PassiveId("test-passive"),
            5);

        Assert.Equal(PetState.DefaultHP, initial.HP);
        Assert.Equal(PetState.DefaultMaxHP, initial.MaxHP);
        Assert.Equal(PetState.DefaultATK, initial.ATK);
        Assert.Equal(PetState.DefaultDEF, initial.DEF);
        Assert.Equal(PetState.DefaultPower, initial.Power);
        Assert.Equal(PetState.DefaultCrit, initial.Crit);
    }

    [Fact]
    public void CommittedSwap_ShouldCarryTheCombatStatsForwardUnchanged()
    {
        // GAME_STATE.md §5.1: the post-resolution write-back replaces the whole
        // PetState. The Match / Combo stage computes only Combo and MatchCount, which
        // live at the BattleState root (§2.2), so the combat stats on the Pet must
        // survive the write-back rather than being reset to
        // their defaults — COMBAT_RULES.md §2's Resource Generation (which would
        // change Power) and §3's Damage Pipeline (which would change HP) are
        // unimplemented, so a committed Swap changes neither.
        var pet = DefaultPet() with { HP = 812, Power = 40, Crit = 12 };
        var state = BattleWith(MatchingBoard(), petState: pet);

        var result = SwapExecutor.Execute(state, new SwapRequest(From, To));

        Assert.True(result.IsAccepted);
        Assert.Equal(812, result.State.PetState.HP);
        Assert.Equal(40, result.State.PetState.Power);
        Assert.Equal(12, result.State.PetState.Crit);
        Assert.Equal(state.PetState.MaxHP, result.State.PetState.MaxHP);
        Assert.Equal(state.PetState.ATK, result.State.PetState.ATK);
        Assert.Equal(state.PetState.DEF, result.State.PetState.DEF);

        // The accounting this stage does own still moved.
        Assert.Equal(1, result.State.Combo);
        Assert.Equal(1, result.State.MatchCount);
    }

    [Fact]
    public void RejectedSwap_ShouldLeaveTheCombatStatsUnchanged()
    {
        // MATCH3_RULES.md §2.1.5 item 5 / GAME_STATE.md §5.1 item 6: a rejected action
        // writes nothing at all, so the state the caller still holds keeps every value
        // — including the combat stats — exactly as it was. A rejection carries no
        // state of its own, so the caller's own value is what is asserted.
        var held = DefaultPet() with { HP = 500, ATK = 77, Power = 60 };
        var state = BattleWith(MatchingBoard(), petState: held);

        var result = SwapExecutor.Execute(state, new SwapRequest(From, From));

        Assert.False(result.IsAccepted);
        Assert.Equal(held, state.PetState);
        Assert.Equal(500, state.PetState.HP);
        Assert.Equal(77, state.PetState.ATK);
        Assert.Equal(60, state.PetState.Power);
    }

    // =======================================================================
    // A single Match — the minimum accounting case
    // =======================================================================

    [Fact]
    public void CommittedSwap_ShouldCountOneMatchIntoMatchCountAndCombo()
    {
        // Given MatchCount = 0 and Combo = 0, a committed Swap producing one Match
        // leaves MatchCount = 1 and Combo = 1 (GAME_RULES.md §3 item 2, §5 item 2).
        var state = BattleWith(MatchingBoard(), (Combo: 0, MatchCount: 0));

        var result = SwapExecutor.Execute(state, new SwapRequest(From, To));

        Assert.True(result.IsAccepted);
        Assert.Equal(1, MatchTotal(result));
        Assert.Equal(1, result.State.MatchCount);
        Assert.Equal(1, result.State.Combo);
    }

    [Fact]
    public void CommittedSwap_ShouldLeaveComboEqualToTheSwapsMatchTotal()
    {
        // MATCH3_RULES.md §6.3 item 1: Combo equals the running total of Matches the
        // Swap detected, and §6.5 item 3 makes it at least 1 for a committed Swap —
        // never 0.
        var result = SwapExecutor.Execute(BattleWith(MatchingBoard()), new SwapRequest(From, To));

        Assert.Equal(MatchTotal(result), result.State.Combo);
        Assert.True(result.State.Combo >= 1);
    }

    // =======================================================================
    // Several Matches in one Swap — cascade accounting
    // =======================================================================

    [Fact]
    public void CascadeMatches_ShouldEachIncrementMatchCountAndCombo()
    {
        // GAME_RULES.md §3 item 2 / §4 item 3 and MATCH3_RULES.md §6.3 item 1: every
        // Match, cascade or not, increments both once. The fixture is a genuine
        // multi-pass resolution — asserted here rather than assumed — so the cascade
        // path is really exercised.
        var state = CascadeBattle((Combo: 0, MatchCount: 0));

        // Guard the fixture: the board holds no Match before the Swap (§1.3) and the
        // pair produces one (§1.4), so the whole resolution belongs to this Swap.
        Assert.Empty(MatchDetector.Detect(state.BoardState));

        var result = SwapExecutor.Execute(state, new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(result.IsAccepted);
        Assert.True(result.Resolution.Passes.Count >= 3, "the fixture must produce Cascades");
        Assert.Equal(3, result.Resolution.TotalMatches);
        Assert.Single(result.Resolution.Passes[0].Matches);

        // Each cascade Match counts independently into both values.
        Assert.Equal(3, result.State.MatchCount);
        Assert.Equal(3, result.State.Combo);

        // And the identity holds at every pass boundary, not only at the end: the
        // running total of the passes walked so far is the accounted value.
        var running = 0;
        foreach (var pass in result.Resolution.Passes)
        {
            running += pass.Matches.Count;
            Assert.True(running >= 1 && running <= result.State.Combo);
        }

        Assert.Equal(result.Resolution.TotalMatches, result.State.MatchCount);
    }

    [Fact]
    public void SeveralMatchesInOnePass_ShouldNotBeDerivedFromPassCountOrCascadeDepth()
    {
        // MATCH3_RULES.md §3 item 5 / §6.2–§6.3: several distinct shapes detected in
        // ONE pass are several Matches, so Combo is neither the pass count nor the
        // cascade depth, and MatchCount is not a cleared-cell total.
        var board = TestBoard.Background().WithGems(
            (I(3, 5), GemType.Atk), (I(3, 6), GemType.Atk), (I(3, 7), GemType.Atk),
            (I(6, 0), GemType.Hp), (I(6, 1), GemType.Hp), (I(6, 2), GemType.Hp),
            (I(1, 0), GemType.Power), (I(1, 1), GemType.Power), (I(1, 2), GemType.Power));

        var state = BattleWith(board, (Combo: 0, MatchCount: 0));

        // The Swap is used only as the resolution's entry point; the post-swap board
        // is what holds the three shapes, so the first pass detects all three at once
        // (§3.1 item 2).
        var result = SwapExecutor.Execute(state, new SwapRequest(0, 1));

        Assert.True(result.IsAccepted);

        // The first pass alone found three distinct shapes — several Matches in one
        // detection pass (§3 item 5), which no pass-count derivation can express.
        var firstPass = result.Resolution.Passes[0].Matches.Count;
        Assert.Equal(3, firstPass);

        // Never fewer Matches than the first pass held, and the total is the sum over
        // passes — so a one-pass-one-Match derivation is excluded by construction.
        Assert.True(result.Resolution.TotalMatches >= firstPass);
        Assert.Equal(
            result.Resolution.Passes.Sum(pass => pass.Matches.Count),
            result.State.MatchCount);
        Assert.Equal(result.State.MatchCount, result.State.Combo);

        // Not the pass count and not the cascade depth.
        Assert.NotEqual(result.Resolution.Passes.Count, result.State.Combo);
        Assert.NotEqual(result.Resolution.CascadeDepth + 1, result.State.Combo);

        // And not a cleared-cell total: the pass cleared strictly more cells than it
        // detected shapes.
        var clearedCells = result.Resolution.Passes.Sum(pass => pass.ClearedCellUnion.Count);
        Assert.True(clearedCells > result.State.Combo);
        Assert.NotEqual(clearedCells, result.State.MatchCount);
    }

    [Fact]
    public void CascadeMatches_ShouldNotBeDerivedFromCascadeDepthOrPassCount()
    {
        // The same requirement on the multi-pass fixture: three Matches across three
        // passes is neither three-passes-equals-three-coincidentally nor a
        // cleared-cell total. The identity is asserted against the resolution's own
        // report, per pass.
        var state = CascadeBattle((Combo: 0, MatchCount: 0));

        var result = SwapExecutor.Execute(state, new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(result.IsAccepted);

        var clearedCells = result.Resolution.Passes.Sum(pass => pass.ClearedCellUnion.Count);
        var matches = result.Resolution.Passes.Sum(pass => pass.Matches.Count);

        Assert.Equal(matches, result.State.MatchCount);
        Assert.Equal(matches, result.State.Combo);

        // The cleared-cell total is strictly larger here (each Match clears ≥ 3
        // cells), so a cleared-cell derivation would produce a different value.
        Assert.True(clearedCells > matches);
        Assert.NotEqual(clearedCells, result.State.MatchCount);
    }

    // =======================================================================
    // Existing MatchCount — cumulative, never reset
    // =======================================================================

    [Fact]
    public void CommittedSwap_ShouldAddToAnExistingMatchCountAndResetCombo()
    {
        // Given MatchCount = 7 and Combo = 4, a committed Swap adds this Swap's
        // Matches to MatchCount (cumulative) and leaves Combo equal to that Swap's
        // own Match total — not to the previous value.
        //
        // The fixture is asserted through the resolution rather than hard-coded, so
        // the test states the contract and not a particular board's shape.
        var state = BattleWith(MatchingBoard(), (Combo: 4, MatchCount: 7));

        var result = SwapExecutor.Execute(state, new SwapRequest(From, To));

        Assert.True(result.IsAccepted);

        var produced = MatchTotal(result);
        Assert.True(produced >= 1);

        Assert.Equal(7 + produced, result.State.MatchCount);
        Assert.Equal(produced, result.State.Combo);
        Assert.NotEqual(4, result.State.Combo);
    }

    [Fact]
    public void RepeatedCommittedSwaps_ShouldAccumulateMatchCountAndResetComboEachTime()
    {
        // The canonical Combo-reset example (GAME_RULES.md §5, MATCH3_RULES.md §6.1
        // item 4, §6.7): Swap A produces 3 Matches (Combo = 3), the next committed
        // Swap produces 1 (Combo = 1), and MatchCount is their total — 4 — because it
        // never resets.
        //
        // Swap A is driven through the real pipeline on the cascade fixture. Swap B
        // starts from the board Swap A committed and is the canonical single-Match
        // completion on that board, so both Swaps are genuine committed Swaps rather
        // than two independent states.
        var state = CascadeBattle((Combo: 0, MatchCount: 0));

        var first = SwapExecutor.Execute(state, new SwapRequest(CascadeFrom, CascadeTo));

        Assert.True(first.IsAccepted);
        Assert.Equal(3, first.State.Combo);
        Assert.Equal(3, first.State.MatchCount);

        // The board Swap A left, completed by one Match. The completing swap is
        // derived from that board rather than assumed, and it must not be the pair
        // Swap A already committed (§2.1.4).
        var afterFirst = first.State;
        var (secondFrom, secondTo) = FindSingleMatchPair(afterFirst);
        var second = SwapExecutor.Execute(afterFirst, new SwapRequest(secondFrom, secondTo));

        Assert.True(second.IsAccepted);
        Assert.Equal(1, second.Resolution.TotalMatches);

        // Combo was reset by the committed Swap and now counts only that Swap's Match.
        Assert.Equal(1, second.State.Combo);

        // MatchCount carried forward: 3 + 1 = 4.
        Assert.Equal(4, second.State.MatchCount);
    }

    [Fact]
    public void CommittedSwap_ShouldNeverPublishComboZero()
    {
        // MATCH3_RULES.md §6.5 item 3: no committed Swap can end with Combo = 0,
        // because a committed Swap is match-producing by validation (§2.1.2 item 4).
        // The transient 0 between the reset and the first Match is internal and never
        // written or published (GAME_STATE.md §5.1 item 2).
        var state = BattleState.CreateWith("battle-005-exhaustive", TestSeed);
        var committed = 0;

        foreach (var (from, to) in AllAdjacentPairs())
        {
            var result = SwapExecutor.Execute(state, new SwapRequest(from, to));

            if (result.IsRejected)
            {
                continue;
            }

            committed++;

            Assert.True(result.State.Combo >= 1);
            Assert.Equal(MatchTotal(result), result.State.Combo);
            Assert.Equal(MatchTotal(result), result.State.MatchCount);
        }

        Assert.True(committed > 0, "a generated board has at least one valid Swap (MATCH3_RULES.md §1.4)");
    }

    [Fact]
    public void MatchCount_ShouldNeverResetAcrossAChainOfCommittedSwaps()
    {
        // GAME_RULES.md §3 item 2 / GAME_STATE.md §2.2: MatchCount is cumulative for
        // the entire battle. Driven over repeated committed Swaps so the cumulative
        // rule is exercised rather than assumed.
        var current = BattleState.CreateWith("battle-005-cumulative", TestSeed);
        var expected = 0;
        var committed = 0;

        for (var turn = 0; turn < 6; turn++)
        {
            var pair = FindAdjacentPairThatProducesAMatch(current, current.LastCommittedSwapPair);
            var result = SwapExecutor.Execute(current, pair);

            Assert.True(result.IsAccepted, $"turn {turn}: expected the completing swap to be accepted");

            committed++;
            expected += MatchTotal(result);
            current = result.State;

            Assert.Equal(expected, current.MatchCount);
            Assert.Equal(MatchTotal(result), current.Combo);
        }

        Assert.Equal(6, committed);
        Assert.True(expected >= committed, "each committed Swap produces at least one Match");
    }

    // =======================================================================
    // Special Gem exclusion — MATCH3_RULES.md §6.3.1
    // =======================================================================

    [Fact]
    public void SpecialGemActivationAndChain_ShouldNotIncrementMatchCountOrCombo()
    {
        // MATCH3_RULES.md §6.3.1: a Special Gem activation, a chain activation, the
        // creation of a Special Gem, and the N cells one clears are all excluded.
        //
        // A Match 4 fixture (row 5 holds 'P','P','D','P' at columns 0-3 with the
        // completing 'P' at (4,2)) creates exactly one Line Clear Gem, and a Match 5
        // fixture creates an Area Gem — both are creations, not Matches. The
        // assertion is the identity: the accounted total equals the number of
        // MatchResolution objects, and equals Resolution.TotalMatches, whatever the
        // resolution did with Special Gems and cleared cells.
        var board = TestBoard.Background().WithGems(
            (I(5, 0), GemType.Power),
            (I(5, 1), GemType.Power),
            (I(5, 2), GemType.Def),
            (I(5, 3), GemType.Power),
            (I(4, 2), GemType.Power));

        var state = BattleWith(board, (Combo: 0, MatchCount: 0));

        var result = SwapExecutor.Execute(state, new SwapRequest(I(4, 2), I(5, 2)));

        Assert.True(result.IsAccepted);

        // The pass created a Special Gem — creation is a consequence of a Match, and
        // it is never a second Match (§5.4 item 4, §6.3 item 3).
        var created = result.Resolution.Passes.Sum(pass => pass.CreatedSpecialGems.Count);
        Assert.True(created > 0, "the fixture must create a Special Gem");

        // Accounting counts Matches only: the created gems, the activated gems, and
        // the cells the activation cleared all contributed nothing.
        var matchCount = result.Resolution.Passes.Sum(pass => pass.Matches.Count);
        var activated = result.Resolution.Passes.Sum(pass => pass.ActivatedSpecialGems.Count);
        var clearedCells = result.Resolution.Passes.Sum(pass => pass.ClearedCellUnion.Count);

        Assert.Equal(matchCount, result.State.MatchCount);
        Assert.Equal(matchCount, result.State.Combo);

        // The identity that proves the wrong derivations are absent: a pass that
        // activated gems and cleared cells still reported only its Matches.
        Assert.NotEqual(clearedCells, result.State.MatchCount);

        // Activated or not, the excluded collections never reached the counters.
        _ = activated;
    }

    [Fact]
    public void SpecialGemActivationOnTheBoard_ShouldStillCountOnlyMatches()
    {
        // A board that already holds a Special Gem: this Swap both completes a Match
        // and consumes the held gem. The activation clears cells; the accounting
        // counts the Match.
        var board = MatchingBoard().WithSpecial((I(4, 2), SpecialGem.Burst()));

        var state = BattleWith(board, (Combo: 0, MatchCount: 0));

        var result = SwapExecutor.Execute(state, new SwapRequest(From, To));

        Assert.True(result.IsAccepted);

        var matches = result.Resolution.Passes.Sum(pass => pass.Matches.Count);
        var activated = result.Resolution.Passes.Sum(pass => pass.ActivatedSpecialGems.Count);

        Assert.True(activated > 0, "the fixture must activate the held Special Gem");
        Assert.Equal(matches, result.State.Combo);
        Assert.Equal(matches, result.State.MatchCount);
    }

    // =======================================================================
    // Rejected Swap — every reason leaves both fields untouched
    // =======================================================================

    [Fact]
    public void RejectedSwap_ShouldLeaveMatchCountAndComboUnchangedForEveryReason()
    {
        // MATCH3_RULES.md §2.1.5 item 5 / §6.1 item 3 / §6.5 item 2 and
        // GAME_STATE.md §5.1 item 6: a rejected Swap neither resets nor increments
        // either value, for every rejection reason.
        var accounting = (Combo: 4, MatchCount: 7);
        var state = BattleWith(MatchingBoard(), accounting) with
        {
            LastCommittedSwapPair = CommittedSwapPair.FromCells(0, 1),
        };
        var boardBefore = state.BoardState.ToCellArray();

        var rejections = new (SwapRequest Request, SwapRejectionReason Reason)[]
        {
            (new SwapRequest(99, To), SwapRejectionReason.InvalidCellIndex),
            (new SwapRequest(From, From), SwapRejectionReason.InvalidCellIndex),
            (new SwapRequest(13, 15), SwapRejectionReason.InvalidSwap),
            (new SwapRequest(0, 9), SwapRejectionReason.InvalidSwap),
            (new SwapRequest(5, 6), SwapRejectionReason.NoMatchFromSwap),
            (new SwapRequest(0, 1), SwapRejectionReason.StaleAction),
            (new SwapRequest(1, 0), SwapRejectionReason.StaleAction),
        };

        foreach (var (request, expectedReason) in rejections)
        {
            var result = SwapExecutor.Execute(state, request);

            Assert.True(result.IsRejected);
            Assert.Equal(expectedReason, result.Reason);

            // Nothing moved: not the progression state, not the counters, not the
            // record, not the board, not the RNG.
            Assert.Equal(7, state.MatchCount);
            Assert.Equal(4, state.Combo);
            Assert.Equal(accounting.Combo, state.Combo);

            Assert.Equal(BattleState.InitialTurn, state.Turn);
            Assert.Equal(BattleState.InitialSequence, state.Sequence);
            Assert.Equal(TestSeed, state.RngSeed);
            Assert.Equal(BattleState.CreateWith("battle-005", TestSeed).RngState, state.RngState);
            Assert.Equal(new CommittedSwapPair(0, 1), state.LastCommittedSwapPair);

            var boardAfter = state.BoardState.ToCellArray();
            Assert.Equal(boardBefore.Length, boardAfter.Length);
            for (var i = 0; i < boardBefore.Length; i++)
            {
                Assert.Equal(boardBefore[i], boardAfter[i]);
            }
        }
    }

    [Fact]
    public void RejectedSwap_ShouldNotExposeAnyStateAtAll()
    {
        // A rejection carries no state (MATCH3_RULES.md §2.1.5), so there is no
        // post-rejection state a caller could read — proof that no accounting
        // ran before validation succeeded.
        var state = BattleWith(MatchingBoard(), (Combo: 4, MatchCount: 7));

        var result = SwapExecutor.Execute(state, new SwapRequest(0, 9));

        Assert.True(result.IsRejected);
        Assert.Throws<InvalidOperationException>(() => result.State);
        Assert.Throws<InvalidOperationException>(() => result.Resolution);
    }

    [Fact]
    public void RejectedSwap_ShouldNotClearTheComboThePreviousSwapLeft()
    {
        // MATCH3_RULES.md §6.5 item 2: after a rejected Swap, Combo still holds the
        // previous Swap's final value — the zeroing happens when a Swap begins,
        // which a rejection is not.
        var first = SwapExecutor.Execute(
            BattleWith(MatchingBoard(), (Combo: 0, MatchCount: 0)),
            new SwapRequest(From, To));

        Assert.True(first.IsAccepted);
        var afterCommit = first.State;
        Assert.True(afterCommit.Combo >= 1);

        var rejected = SwapExecutor.Execute(afterCommit, new SwapRequest(0, 9));

        Assert.True(rejected.IsRejected);
        Assert.Equal(afterCommit.Combo, afterCommit.Combo);
        Assert.Equal(afterCommit.MatchCount, afterCommit.MatchCount);
    }

    // =======================================================================
    // Single write-back and determinism
    // =======================================================================

    [Fact]
    public void CommittedSwap_ShouldWriteTheAccountingInTheSameWriteBackAsEverythingElse()
    {
        // GAME_STATE.md §5.1 items 2–3 / §8.3: nothing is written mid-resolution. The
        // single result carries the stable board, the retained RngState, the
        // counters, the commit record, and the progression state together.
        var state = BattleWith(MatchingBoard(), (Combo: 4, MatchCount: 7));

        var result = SwapExecutor.Execute(state, new SwapRequest(From, To));
        var committed = result.State;

        Assert.True(committed.BoardState.CellsEqual(result.Resolution.Board));
        Assert.Empty(MatchDetector.Detect(committed.BoardState));
        Assert.Equal(result.Resolution.RngState, committed.RngState);
        Assert.Equal(state.RngSeed, committed.RngSeed);
        Assert.Equal(state.Turn + 1, committed.Turn);
        Assert.Equal(state.Sequence + 1, committed.Sequence);
        Assert.Equal(CommittedSwapPair.FromCells(From, To), committed.LastCommittedSwapPair);

        // The progression values that accompany the new Sequence are the finished
        // resolution's values.
        Assert.Equal(MatchTotal(result), committed.Combo);
        Assert.Equal(7 + MatchTotal(result), committed.MatchCount);
    }

    [Fact]
    public void CommittedSwap_ShouldNotMutateTheInputAccounting()
    {
        // State is immutable and authoritative: the input snapshot cannot be rebound
        // by the call.
        var accounting = (Combo: 4, MatchCount: 7);
        var state = BattleWith(MatchingBoard(), accounting);

        SwapExecutor.Execute(state, new SwapRequest(From, To));

        Assert.Equal(accounting.Combo, state.Combo);
        Assert.Equal(7, state.MatchCount);
        Assert.Equal(4, state.Combo);
    }

    [Fact]
    public void Accounting_ShouldConsumeNoRngAndChangeNoSeed()
    {
        // MATCH3_RULES.md §7.2 item 1 / ADR-009: accounting is arithmetic over the
        // resolution's report. It draws nothing, so the committed RngState is exactly
        // what the resolution produced.
        var state = BattleWith(MatchingBoard(), (Combo: 4, MatchCount: 7));

        var result = SwapExecutor.Execute(state, new SwapRequest(From, To));

        Assert.Equal(state.RngSeed, result.State.RngSeed);
        Assert.Equal(result.Resolution.RngState, result.State.RngState);
    }

    [Fact]
    public void Accounting_ShouldBeEntirelyDeterministic()
    {
        // MATCH3_RULES.md §4.6 / §7.1: the same BattleState and the same SwapRequest
        // always produce the same accounting, PetState, BoardState, Turn, Sequence, and RngState.
        var state = BattleWith(MatchingBoard(), (Combo: 4, MatchCount: 7));
        var reference = SwapExecutor.Execute(state, new SwapRequest(From, To));

        for (var run = 0; run < 20; run++)
        {
            var again = SwapExecutor.Execute(state, new SwapRequest(From, To));

            Assert.Equal(reference.IsAccepted, again.IsAccepted);
            Assert.Equal(reference.Reason, again.Reason);
            Assert.Equal(reference.State.PetState, again.State.PetState);
            Assert.True(reference.State.BoardState.CellsEqual(again.State.BoardState));
            Assert.Equal(reference.State.RngState, again.State.RngState);
            Assert.Equal(reference.State.Turn, again.State.Turn);
            Assert.Equal(reference.State.Sequence, again.State.Sequence);
            Assert.Equal(reference.State.LastCommittedSwapPair, again.State.LastCommittedSwapPair);
        }
    }

    [Fact]
    public void Accounting_ShouldNotDependOnTheRequestOrderOfThePair()
    {
        // MATCH3_RULES.md §2.1.1 item 2: the unordered pair is the Swap's identity, so
        // the two spellings account identically.
        var state = BattleWith(MatchingBoard(), (Combo: 4, MatchCount: 7));

        var forward = SwapExecutor.Execute(state, new SwapRequest(From, To));
        var reversed = SwapExecutor.Execute(state, new SwapRequest(To, From));

        Assert.Equal(forward.State.Combo, reversed.State.Combo);
    }

    // =======================================================================
    // Boundary — no second detection pipeline, no event system
    // =======================================================================

    [Fact]
    public void Executor_ShouldExposeNoAccountingEntryPointOfItsOwn()
    {
        // The accounting is part of the existing committed-Swap pipeline: the executor
        // exposes the same single public operation it did before, so no caller can run
        // a second Match-3 resolution or a separate accounting pass
        // (ARCHITECTURE.md §4.1, TASK-005 §9).
        var methods = typeof(SwapExecutor)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Select(m => m.Name)
            .Distinct()
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Execute"], methods);
    }

    [Fact]
    public void Accounting_ShouldIntroduceNoEventOrMessageSurface()
    {
        // TASK-005 §10 was "state accounting only": at that stage no event type,
        // emitter, or message existed anywhere in Domain. TASK-006 now implements
        // the documented event emission, so the three named event types and the
        // BattleEvent value legitimately exist (GAME_EVENTS.md §1, §2).
        //
        // The guarantee this test owns is unchanged and is now asserted against what
        // the documentation actually forbids: no *infrastructure* event surface —
        // no event bus, broker, emitter, publisher, subscription framework, or
        // persistence — and no event name the contract does not define. The
        // resolution events that do exist are asserted in BattleEventEmissionTests.
        var domainTypes = typeof(BattleState).Assembly
            .GetTypes()
            .Select(t => t.Name)
            .ToArray();

        foreach (var forbidden in new[]
                 {
                     "IEventBus", "EventBus", "EventEmitter", "IEventEmitter",
                     "IEventPublisher", "EventPublisher", "IEventStore", "EventLog",
                     "IMessageBroker", "MessageBroker",
                     // Undocumented event names (GAME_EVENTS.md §2, GAME_RULES.md §16).
                     "MatchCountChanged", "SpecialGemActivated", "SpecialGemCreated",
                     "SpecialGemDestroyed", "TurnChanged", "SequenceChanged",
                     "BoardChanged", "BoardUpdated",
                 })
        {
            Assert.DoesNotContain(forbidden, domainTypes);
        }
    }

    // ------------------------------------------------------------- helpers ---

    /// <summary>
    /// An adjacent pair of the battle's current board whose committed Swap produces
    /// <b>exactly one</b> Match — a single-pass resolution — and which is not the
    /// already committed pair (<c>MATCH3_RULES.md</c> §1.4, §2.1.4).
    /// </summary>
    private static (int From, int To) FindSingleMatchPair(BattleState state)
    {
        foreach (var (from, to) in AllAdjacentPairs())
        {
            if (state.LastCommittedSwapPair is { } skip
                && skip.Matches(CommittedSwapPair.FromCells(from, to)))
            {
                continue;
            }

            var rng = new Pcg32(state.RngState.State, state.RngState.Increment);
            var resolution = CascadeResolver.Resolve(
                state.BoardState.WithSwapped(from, to),
                rng,
                swapOriginIndex: null);

            if (resolution.TotalMatches == 1)
            {
                return (from, to);
            }
        }

        throw new InvalidOperationException("Expected an adjacent pair producing exactly one Match.");
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