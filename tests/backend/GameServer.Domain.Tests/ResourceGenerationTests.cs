using GameServer.Domain.Battle;
using GameServer.Domain.Match3;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Resource Generation tests (<c>GAME_RULES.md</c> §17 step 12,
/// <c>COMBAT_RULES.md</c> §2).
///
/// <code>
/// Rule (COMBAT_RULES.md §2)
///  ↓
/// Scenario (Given cleared Gems, When generation runs, Then the pools hold X)
///  ↓
/// Test
/// </code>
///
/// Every expected value below is derived from <c>COMBAT_RULES.md</c> §2's table
/// and its multiplier table, never from the implementation:
///
/// <code>
/// ATK Gem    +10 Base Damage      Match 3   → 1.0×
/// DEF Gem    +5  Defense pool     Match 4   → 1.5×
/// HP Gem     +20 Heal pool        Match 5   → 2.0×
/// POWER Gem  +10 Power            L/T       → 1.25×
/// </code>
///
/// Boards are written through <see cref="TestBoard"/>, so a scenario reads as the
/// board the documentation describes: the character at row <c>r</c>, column
/// <c>c</c> is cell index <c>r * 8 + c</c> (<c>MATCH3_RULES.md</c> §1.0).
///
/// <b>Fixtures declare every cell of the region they study.</b> The background
/// board is match-free, but a run that grows past the cells a fixture names would
/// silently change the tier — so each scenario states the whole row(s) its Match
/// occupies, and the swap is chosen so the run's length is exactly the tier under
/// test.
/// </summary>
public class ResourceGenerationTests
{
    private const ulong TestSeed = 20260815UL;

    private static int I(int row, int column) => TestBoard.I(row, column);

    private static BattleState BattleWith(BoardState board, PlayerState? player = null) =>
        BattleState.CreateWith("battle-018", TestSeed) with
        {
            BoardState = board,
            PlayerState = player ?? PlayerState.Initial,
        };

    /// <summary>
    /// A board whose only Match is a horizontal run of exactly
    /// <paramref name="length"/> Gems of <paramref name="type"/> on row 4, made by
    /// swapping the completing Gem above the gap with the gap cell.
    ///
    /// Built from explicit rows like <see cref="Match3Board"/>, so the run is
    /// exactly <paramref name="length"/> long and the tier under test is the tier
    /// applied. The completing Gem sits at row 3, directly above the gap at row 4.
    /// </summary>
    private static BoardState RunBoard(GemType type, int length, out int from, out int to)
    {
        // The run is left-aligned from column 0 and the gap is its last column, so
        // closing the gap yields a run of exactly `length`.
        var gapColumn = length - 1;
        to = I(4, gapColumn);
        from = I(3, gapColumn);

        return TestBoard.FromRows(
            "ADHPADHP",
            "DHPADHPA",
            "HPADHPAD",
            Row3For(type, gapColumn),
            RunRow(type, length, gapColumn),
            "DHPADHPA",
            "HPADHPAD",
            "PADHPADH");
    }

    /// <summary>
    /// Row 3 with the completing Gem at <paramref name="column"/> — the cell swapped
    /// into the run's gap on row 4.
    /// </summary>
    private static string Row3For(GemType type, int column)
    {
        var cells = "PADHPADH".ToCharArray();
        cells[column] = Letter(type);
        return new string(cells);
    }

    /// <summary>
    /// A board carrying one horizontal Match-3 run of <paramref name="type"/> on
    /// row 4, completed by swapping the completing Gem in row 3 into the run's gap.
    ///
    /// The fixture is built from explicit rows, not by patching the background: the
    /// run's own row and the row holding the completing Gem are written out in full,
    /// so the run's length is exactly three whatever <paramref name="type"/> is.
    /// Patching the shared background would let a run merge with the background Gem
    /// beside it and silently become a Match 4 — a fixture defect that would look
    /// like a multiplier bug.
    ///
    /// <code>
    /// row 3   P A X H P A D H     the completing Gem at column 2
    /// row 4   A A ? A D H P A     the run's two Gems (0,1), the gap at 2, breakers
    /// </code>
    ///
    /// The swap exchanges (3,2)↔(4,2), so the gap closes with the run's type and row
    /// 4 becomes a run of exactly three.
    /// </summary>
    private static BoardState Match3Board(GemType type)
    {
        const int gapColumn = 2;

        return TestBoard.FromRows(
            "ADHPADHP",
            "DHPADHPA",
            "HPADHPAD",
            Row3For(type, gapColumn),
            RunRow(type, length: 3, gapColumn),
            "DHPADHPA",
            "HPADHPAD",
            "PADHPADH");
    }

    /// <summary>
    /// A run row: the run occupies columns <c>0 … length−1</c> of row 4 except the
    /// gap at <paramref name="gapColumn"/>, and every other column is a breaker that
    /// cannot extend the run.
    ///
    /// Keeping the run left-aligned from column 0 and making the gap the run's last
    /// column is what lets the completing Gem close the gap into a run of exactly
    /// <paramref name="length"/>.
    /// </summary>
    private static string RunRow(GemType type, int length, int gapColumn)
    {
        var cells = "ADHPADHP".ToCharArray();
        var runType = Letter(type);
        var breaker = Breaker(type);

        // The run's own columns, except the gap, which must not hold the run's type.
        for (var column = 0; column < length; column++)
        {
            cells[column] = column == gapColumn ? breaker : runType;
        }

        // Every column beyond the run must break it, or the run would be longer than
        // the tier under test.
        for (var column = length; column < 8; column++)
        {
            if (cells[column] == runType)
            {
                cells[column] = breaker;
            }
        }

        return new string(cells);
    }

    /// <summary>A Gem letter that is not <paramref name="type"/>.</summary>
    private static char Breaker(GemType type) => type == GemType.Atk ? 'D' : 'A';

    private static char Letter(GemType type) => type switch
    {
        GemType.Atk => 'A',
        GemType.Def => 'D',
        GemType.Hp => 'H',
        GemType.Power => 'P',
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Not a documented Gem type."),
    };

    private static int From => 26; // I(3, 2) — the completing Gem's cell
    private static int To => 34;   // I(4, 2) — the gap

    // =======================================================================
    // 1–4. Per-Gem base rates — one Gem type at a time, Match-3 tier
    //
    // Each scenario asserts the Swap's FIRST pass, which is the pass the committed
    // Swap produced and is fully determined by the fixture: its size, its tier, and
    // its Gem type are all chosen by the test. A later pass is a Cascade whose board
    // depends on the Gems Spawn draws, so a Swap-level total cannot isolate a single
    // Gem type's rate — the cascade-accumulation property has its own tests below.
    // =======================================================================

    /// <summary>
    /// The resource a resolution's <b>first</b> pass generated, computed from that
    /// pass's own cleared Gems. This is the deterministic part of a Swap: pass 1 runs
    /// on the board the committed Swap produced (<c>MATCH3_RULES.md</c> §4.2 item 1).
    /// </summary>
    private static ResourceGeneration FirstPassResources(CascadeResolver.CascadeResult resolution)
    {
        var pass = resolution.Passes[0];
        var baseDamage = 0;
        var defense = 0;
        var heal = 0;
        var power = 0;
        var counts = ClearedGemCounts.None;

        foreach (var gem in pass.ClearedGems)
        {
            var generated = (ResourceGenerator.BaseOutputFor(gem.GemType)
                             * MatchTierMultipliers.NumeratorFor(gem.Tier))
                            / MatchTierMultipliers.Denominator;

            switch (gem.GemType)
            {
                case GemType.Atk: baseDamage += generated; break;
                case GemType.Def: defense += generated; break;
                case GemType.Hp: heal += generated; break;
                case GemType.Power: power += generated; break;
            }

            counts = counts.Increment(gem.GemType);
        }

        return new ResourceGeneration(baseDamage, defense, heal, power, counts);
    }

    [Fact]
    public void Generate_ShouldProduceBaseDamageFromAtkGemsAtMatchTier()
    {
        // COMBAT_RULES.md §2 / MATCH3_RULES.md §5.1: a Match 3 of three ATK Gems
        // generates 3 × 10 × 1.0× = 30 Base Damage, and nothing in the other pools.
        var result = SwapExecutor.Execute(BattleWith(Match3Board(GemType.Atk)), new SwapRequest(From, To));

        Assert.True(result.IsAccepted);

        var first = FirstPassResources(result.Resolution);
        Assert.Equal(30, first.BaseDamagePool);
        Assert.Equal(0, first.DefensePool);
        Assert.Equal(0, first.HealPool);
        Assert.Equal(0, first.Power);
        Assert.Equal(3, first.ClearedGemCounts.Atk);

        // The Swap's total is at least its first pass — the first pass always
        // generates, and later passes add (§5.7 item 7).
        Assert.True(result.Resources.BaseDamagePool >= 30);
    }

    [Fact]
    public void Generate_ShouldProduceDefenseFromDefGemsAtMatchTier()
    {
        // §2: DEF Gem → +5 Defense pool. Three DEF Gems at the Match-3 tier:
        // 3 × 5 × 1.0× = 15.
        var result = SwapExecutor.Execute(BattleWith(Match3Board(GemType.Def)), new SwapRequest(From, To));

        Assert.True(result.IsAccepted);

        var first = FirstPassResources(result.Resolution);
        Assert.Equal(15, first.DefensePool);
        Assert.Equal(0, first.BaseDamagePool);
        Assert.Equal(0, first.HealPool);
        Assert.Equal(0, first.Power);
        Assert.Equal(3, first.ClearedGemCounts.Def);
    }

    [Fact]
    public void Generate_ShouldProduceHealFromHpGemsAtMatchTier()
    {
        // §2: HP Gem → +20 Heal pool. Three HP Gems at the Match-3 tier:
        // 3 × 20 × 1.0× = 60.
        var result = SwapExecutor.Execute(BattleWith(Match3Board(GemType.Hp)), new SwapRequest(From, To));

        Assert.True(result.IsAccepted);

        var first = FirstPassResources(result.Resolution);
        Assert.Equal(60, first.HealPool);
        Assert.Equal(0, first.BaseDamagePool);
        Assert.Equal(0, first.DefensePool);
        Assert.Equal(0, first.Power);
        Assert.Equal(3, first.ClearedGemCounts.Hp);
    }

    [Fact]
    public void Generate_ShouldProducePowerFromPowerGemsAtMatchTier()
    {
        // §2: POWER Gem → +10 Power. Three POWER Gems at the Match-3 tier:
        // 3 × 10 × 1.0× = 30, and Power is the one pool that reaches the state.
        var result = SwapExecutor.Execute(BattleWith(Match3Board(GemType.Power)), new SwapRequest(From, To));

        Assert.True(result.IsAccepted);

        var first = FirstPassResources(result.Resolution);
        Assert.Equal(30, first.Power);
        Assert.Equal(0, first.BaseDamagePool);
        Assert.Equal(0, first.DefensePool);
        Assert.Equal(0, first.HealPool);

        // GAME_RULES.md §12 / GAME_STATE.md §2.2: Power is persistent, and it equals
        // the Swap's generated Power when the cap does not bind (a battle starts at
        // 0 — PlayerState.DefaultPower).
        Assert.Equal(result.Resources.Power, result.State.PlayerState.Power);
    }

    // =======================================================================
    // 5. Match-tier multipliers
    // =======================================================================

    [Fact]
    public void MatchTierMultipliers_ShouldCarryTheDocumentedNumerators()
    {
        // COMBAT_RULES.md §2's multiplier table, read as exact rationals over 4.
        // The L/T rate is 1.25× — lower than Match 4's 1.5× — which §2 owns and
        // MATCH3_RULES.md §5.4 item 3 relies on.
        Assert.Equal(4, MatchTierMultipliers.NumeratorFor(MatchTier.Match3)); // 1.00×
        Assert.Equal(6, MatchTierMultipliers.NumeratorFor(MatchTier.Match4)); // 1.50×
        Assert.Equal(8, MatchTierMultipliers.NumeratorFor(MatchTier.Match5)); // 2.00×
        Assert.Equal(5, MatchTierMultipliers.NumeratorFor(MatchTier.Lt));     // 1.25×
    }

    [Fact]
    public void MatchTierMultipliers_ShouldPlaceMatch6AndAboveAtTheMatch5Tier()
    {
        // MATCH3_RULES.md §5.3 item 3 / COMBAT_RULES.md §2 item 4: a run of 6 or
        // more is a Match 5 and there is no separate tier.
        Assert.Equal(MatchTier.Match3, MatchTierMultipliers.ForStraightRun(3));
        Assert.Equal(MatchTier.Match4, MatchTierMultipliers.ForStraightRun(4));
        Assert.Equal(MatchTier.Match5, MatchTierMultipliers.ForStraightRun(5));
        Assert.Equal(MatchTier.Match5, MatchTierMultipliers.ForStraightRun(6));
        Assert.Equal(MatchTier.Match5, MatchTierMultipliers.ForStraightRun(8));
    }

    [Fact]
    public void MatchTierMultipliers_ShouldRejectARunShorterThanAMatch()
    {
        // MATCH3_RULES.md §3 item 2: the minimum match length is 3, so a run of 2
        // is not a Match and has no tier.
        Assert.Throws<ArgumentOutOfRangeException>(() => MatchTierMultipliers.ForStraightRun(2));
    }

    [Fact]
    public void Generate_ShouldApplyTheMatch4MultiplierToAMatch4()
    {
        // COMBAT_RULES.md §2: Match 4 → 1.5×. Four ATK Gems generate
        // 4 × 10 × 1.5 = 60 Base Damage.
        var board = RunBoard(GemType.Atk, length: 4, out var from, out var to);

        var result = SwapExecutor.Execute(BattleWith(board), new SwapRequest(from, to));

        Assert.True(result.IsAccepted);
        Assert.Equal(4, result.Resources.ClearedGemCounts.Atk);
        Assert.Equal(60, result.Resources.BaseDamagePool);
    }

    [Fact]
    public void Generate_ShouldApplyTheMatch5MultiplierToAMatch5()
    {
        // COMBAT_RULES.md §2: Match 5 → 2.0×. Five ATK Gems generate
        // 5 × 10 × 2.0 = 100 Base Damage.
        var board = RunBoard(GemType.Atk, length: 5, out var from, out var to);

        var result = SwapExecutor.Execute(BattleWith(board), new SwapRequest(from, to));

        Assert.True(result.IsAccepted);
        Assert.Equal(5, result.Resources.ClearedGemCounts.Atk);
        Assert.Equal(100, result.Resources.BaseDamagePool);
    }

    [Fact]
    public void Generate_ShouldUseTheMatch5RateForARunOfSixOrMore()
    {
        // MATCH3_RULES.md §5.3 item 3 / COMBAT_RULES.md §2 item 4: a run of 6+ is a
        // Match 5 at 2.0× — not a sixth multiplier. Six ATK Gems generate
        // 6 × 10 × 2.0 = 120.
        var board = RunBoard(GemType.Atk, length: 6, out var from, out var to);

        var result = SwapExecutor.Execute(BattleWith(board), new SwapRequest(from, to));

        Assert.True(result.IsAccepted);
        Assert.Equal(6, result.Resources.ClearedGemCounts.Atk);
        Assert.Equal(120, result.Resources.BaseDamagePool);
    }

    [Fact]
    public void Generate_ShouldApplyTheLtMultiplierOncePerConsumedCell()
    {
        // COMBAT_RULES.md §2: L/T → 1.25×, applied once per consumed cell over the
        // union of the shape's cells (MATCH3_RULES.md §5.4 item 6).
        //
        // The fixture forms a T: a horizontal run of four ATK on row 4 (columns
        // 0–3) crossing a vertical run of three ATK in column 2 (rows 3–5). The
        // union is 4 + 3 − 1 shared = 6 cells, each at 10 × 1.25.
        //
        // §2 applies the rate "per Gem consumed in that match", so the per-Gem
        // value is what is summed: (10 × 5) / 4 = 12 per Gem, and 6 × 12 = 72.
        // The L/T rate is applied ONCE per cell — not once per arm and not twice
        // for the shared cell (MATCH3_RULES.md §5.4 item 6: "an arm's higher
        // classification does not apply a second, higher multiplier to the cells
        // it shares with the L/T pattern").
        // Patched onto the verified match-free background. The T's arms:
        //   horizontal arm  row 4, columns 0–3  (four ATK; the gap is column 3)
        //   vertical arm    column 2, rows 3–5  (three ATK; (4,2) is the intersection)
        //   completing Gem  (3,3), swapped into the horizontal arm's gap
        // Every cell that could extend an arm is declared, so the arms are exactly the
        // lengths above and the union is 4 + 3 − 1 = 6 cells.
        var board = TestBoard.Background()
            .WithGems(
                // The horizontal arm's own cells, except the gap at column 3.
                (I(4, 0), GemType.Atk),
                (I(4, 1), GemType.Atk),
                (I(4, 2), GemType.Atk),
                // The vertical arm's remaining cells: rows 3 and 5 of column 2.
                (I(3, 2), GemType.Atk),
                (I(5, 2), GemType.Atk),
                // The completing Gem above the horizontal arm's gap.
                (I(3, 3), GemType.Atk),
                // Breakers so neither arm grows: column 4 of row 4, and row 2/6 of
                // column 2 are already background cells of other types — asserted.
                (I(4, 4), GemType.Def),
                (I(2, 2), GemType.Def),
                (I(6, 2), GemType.Def));

        var result = SwapExecutor.Execute(BattleWith(board), new SwapRequest(I(3, 3), I(4, 3)));

        Assert.True(result.IsAccepted);

        // The L/T is the first pass's single shape, so its own contribution is read
        // from that pass: 6 cells at 10 × 1.25 each.
        var first = FirstPassResources(result.Resolution);
        Assert.Equal(6, first.ClearedGemCounts.Atk);

        // §2 applies the rate per Gem consumed, and each Gem yields
        // (10 × 5) / 4 = 12 (the documented multiplier is 1.25×, so a Gem contributes
        // 12.5 and the integer result of applying the rate to that Gem is 12).
        // 6 × 12 = 72.
        Assert.Equal(72, first.BaseDamagePool);

        // §5.4 item 6: the multiplier is applied ONCE per consumed cell. Applying it
        // per arm, or twice for the shared (4,2) cell, would count more than 6 cells'
        // worth and exceed 72.
        Assert.Equal(
            6 * (ResourceGenerator.AtkGemBaseDamage * 5 / MatchTierMultipliers.Denominator),
            first.BaseDamagePool);
    }

    // =======================================================================
    // 6. Multiple matches / cascades in the same committed Swap
    // =======================================================================

    [Fact]
    public void Generate_ShouldSumBothMatchesOfAPass()
    {
        // MATCH3_RULES.md §3 item 5: every distinct shape in one pass is one Match.
        // A pass holding two separate Match-3 shapes of different types generates
        // from both: 3 ATK Gems → 30 Base Damage, 3 DEF Gems → 15 Defense pool.
        //
        // The ATK Match is made by the swap; the DEF Match sits untouched, so both are
        // present in the first (depth 1) pass and both are at the Match-3 tier.
        //
        // Patched onto the verified match-free background, and every cell that could
        // join either run is declared: the ATK run's neighbours on its own row and the
        // completing Gem's, and the DEF run's breakers.
        var board = TestBoard.Background()
            .WithGems(
                // The ATK run: row 4 columns 0–1, with the gap at column 2.
                (I(4, 0), GemType.Atk),
                (I(4, 1), GemType.Atk),
                // The completing Gem above the gap.
                (I(3, 2), GemType.Atk),
                // The DEF run: row 6 columns 0–2, made three by an explicit breaker.
                (I(6, 0), GemType.Def),
                (I(6, 1), GemType.Def),
                (I(6, 2), GemType.Def),
                (I(6, 3), GemType.Power));

        var result = SwapExecutor.Execute(BattleWith(board), new SwapRequest(I(3, 2), I(4, 2)));

        Assert.True(result.IsAccepted);

        // Both Matches are depth 1, so both are at the Match-3 tier, and both are in
        // the first pass's cleared union: 3 ATK Gems → 30 Base Damage, 3 DEF Gems →
        // 15 Defense pool.
        var first = FirstPassResources(result.Resolution);
        Assert.Equal(3, first.ClearedGemCounts.Atk);
        Assert.Equal(3, first.ClearedGemCounts.Def);
        Assert.Equal(30, first.BaseDamagePool);
        Assert.Equal(15, first.DefensePool);
    }

    [Fact]
    public void Generate_ShouldAccumulateAcrossCascadesInOneSwap()
    {
        // MATCH3_RULES.md §5.7 item 7: every Match detected in a Cascade pass
        // generates at its own tier, and the Swap's total is the sum. No "cascade
        // bonus" multiplier exists.
        //
        // The equivalence that makes accumulation checkable without depending on
        // which gems a spawn happens to draw: generating over a resolution equals
        // generating pass by pass over that resolution's own passes and summing, at
        // each pass's own tiers.
        var result = SwapExecutor.Execute(
            BattleWith(Match3Board(GemType.Atk)),
            new SwapRequest(From, To));

        Assert.True(result.IsAccepted);

        var expectedBaseDamage = 0;
        var expectedDefense = 0;
        var expectedHeal = 0;
        var expectedPower = 0;
        var expectedCleared = 0;

        foreach (var pass in result.Resolution.Passes)
        {
            foreach (var gem in pass.ClearedGems)
            {
                var numerator = MatchTierMultipliers.NumeratorFor(gem.Tier);
                var generated = (ResourceGenerator.BaseOutputFor(gem.GemType) * numerator)
                    / MatchTierMultipliers.Denominator;

                switch (gem.GemType)
                {
                    case GemType.Atk: expectedBaseDamage += generated; break;
                    case GemType.Def: expectedDefense += generated; break;
                    case GemType.Hp: expectedHeal += generated; break;
                    case GemType.Power: expectedPower += generated; break;
                }

                expectedCleared++;
            }
        }

        Assert.Equal(expectedBaseDamage, result.Resources.BaseDamagePool);
        Assert.Equal(expectedDefense, result.Resources.DefensePool);
        Assert.Equal(expectedHeal, result.Resources.HealPool);
        Assert.Equal(expectedPower, result.Resources.Power);
        Assert.Equal(expectedCleared, result.Resources.ClearedGemCounts.Total);

        // §5.7 item 7: the Swap's own first-pass Match always generates, so the
        // total is never below what that pass alone produced.
        Assert.True(result.Resources.BaseDamagePool >= 30);
    }

    [Fact]
    public void Generate_ShouldAddRatherThanReplaceAcrossPasses()
    {
        // The accumulation property stated as monotonicity: a Swap whose cascade
        // runs deeper can only generate at least as much as the same Swap's first
        // pass. Stated over the resolution the executor produced, so it holds
        // whatever the spawn draws.
        var result = SwapExecutor.Execute(
            BattleWith(Match3Board(GemType.Atk)),
            new SwapRequest(From, To));

        Assert.True(result.IsAccepted);

        var firstPass = result.Resolution.Passes[0];
        var firstPassTotal = firstPass.ClearedGems
            .Where(g => g.GemType == GemType.Atk)
            .Sum(g => (ResourceGenerator.AtkGemBaseDamage * MatchTierMultipliers.NumeratorFor(g.Tier))
                      / MatchTierMultipliers.Denominator);

        Assert.True(
            result.Resources.BaseDamagePool >= firstPassTotal,
            "Generation accumulates across passes; a later pass never removes what an earlier one generated.");
    }

    [Fact]
    public void Generate_ShouldSplitAMatchByGemType()
    {
        // A shape is one Gem type (MATCH3_RULES.md §3.1 item 3: a run is of one
        // type), but a pass can hold shapes of different types. Mixed clearing is
        // therefore split by type across the pass's shapes, and each type feeds its
        // own pool.
        //
        // The swap makes a Match-3 of HP on row 4; row 6 holds an untouched Match-3
        // of POWER, so one Swap generates into two pools at once. Row 6 is written in
        // full so the POWER run is exactly three and the two rows cannot merge.
        var board = TestBoard.FromRows(
            "ADHPADHP",
            "DHPADHPA",
            "HPADHPAD",
            Row3For(GemType.Hp, column: 2),
            RunRow(GemType.Hp, length: 3, gapColumn: 2),
            "DHPADHPA",
            // Row 6: the untouched POWER Match-3 at columns 0–2, broken at column 3.
            "PPPAHPAD",
            "PADHPADH");

        var result = SwapExecutor.Execute(BattleWith(board), new SwapRequest(From, To));

        Assert.True(result.IsAccepted);

        // 3 HP Gems → 3 × 20 = 60 Heal; 3 POWER Gems → 3 × 10 = 30 Power. Both Matches
        // are in the first pass, so the split is read there.
        var first = FirstPassResources(result.Resolution);
        Assert.Equal(3, first.ClearedGemCounts.Hp);
        Assert.Equal(3, first.ClearedGemCounts.Power);
        Assert.Equal(60, first.HealPool);
        Assert.Equal(30, first.Power);
    }

    // =======================================================================
    // 7. Zero cleared gems
    // =======================================================================

    [Fact]
    public void Generate_ShouldProduceNothingForAResolutionWithNoPasses()
    {
        // MATCH3_RULES.md §4.3: the terminating pass that detects no Match is not a
        // pass that ran and is not in Passes, so a resolution with no passes has no
        // cleared cells and generates nothing.
        var resolution = new CascadeResolver.CascadeResult(
            Board: TestBoard.Background(),
            RngState: default,
            Passes: [],
            CascadeDepth: 0,
            TotalMatches: 0);

        var generation = ResourceGenerator.Generate(resolution);

        Assert.True(generation.IsEmpty);
        Assert.Equal(0, generation.ClearedGemCounts.Total);
        Assert.Equal(0, generation.BaseDamagePool);
        Assert.Equal(0, generation.DefensePool);
        Assert.Equal(0, generation.HealPool);
        Assert.Equal(0, generation.Power);
    }

    [Fact]
    public void Generate_ShouldProduceNothingWhenNothingWasCleared()
    {
        // The zero-cleared contract stated directly against the empty counts the
        // generator sums over: no cell cleared means no resource in any pool.
        Assert.Equal(0, ClearedGemCounts.None.Total);
        Assert.True(ResourceGeneration.None.IsEmpty);
        Assert.Equal(0, ResourceGeneration.None.ClearedGemCounts.Total);
    }

    [Fact]
    public void RejectedSwap_ShouldGenerateNoResources()
    {
        // MATCH3_RULES.md §2.1.5 item 5: a rejected action writes nothing — no
        // Match is counted, no Combo is set, and no Power, Passive, or Relic
        // progression occurs.
        var result = SwapExecutor.Execute(
            BattleWith(Match3Board(GemType.Power)),
            new SwapRequest(0, 63));

        Assert.True(result.IsRejected);
        Assert.Throws<InvalidOperationException>(() => result.Resources);
    }

    // =======================================================================
    // 8. Accumulation across the documented resolution flow
    // =======================================================================

    [Fact]
    public void Power_ShouldAccumulateAcrossCommittedSwaps()
    {
        // GAME_STATE.md §2.2: Power is persistent battle state. §5.1's single
        // write-back carries it forward, so successive committed Swaps accumulate
        // into it rather than replacing it.
        var battle = BattleWith(Match3Board(GemType.Power));

        var first = SwapExecutor.Execute(battle, new SwapRequest(From, To));
        Assert.True(first.IsAccepted);
        var afterFirst = first.State.PlayerState.Power;

        // The first Swap generates 30 Power at its Match-3, so the persistent value
        // is that amount (the cap does not bind at 30). Cascades may add more, so the
        // property asserted is the accumulation, not a fixed total.
        Assert.Equal(first.Resources.Power, afterFirst);
        Assert.True(afterFirst >= 30);

        // The second Swap runs against the state the first committed. Its exact
        // gain depends on the spawned board, so the property asserted is the
        // accumulation itself: the persistent value equals the first Swap's Power
        // plus whatever the second generated, capped at 100.
        var second = SwapExecutor.Execute(first.State, new SwapRequest(From, To));

        if (second.IsAccepted)
        {
            Assert.Equal(
                Math.Min(afterFirst + second.Resources.Power, ResourceGenerator.MaxPower),
                second.State.PlayerState.Power);
        }
    }

    [Fact]
    public void Power_ShouldBeClampedToTheDocumentedRange()
    {
        // GAME_RULES.md §12 / COMBAT_RULES.md §1.1: the range is 0–100 and Power
        // "must never exceed the configured maximum". A gain that would cross the
        // cap leaves the state at the cap, while the pool still reports what the
        // Swap generated — the pool is the amount generated, the state is the
        // amount in force.
        var nearCap = PlayerState.Initial with { Power = 95 };
        var battle = BattleWith(Match3Board(GemType.Power), nearCap);

        var result = SwapExecutor.Execute(battle, new SwapRequest(From, To));

        Assert.True(result.IsAccepted);
        Assert.True(result.Resources.Power >= 30, "The Swap's own Match-3 always generates.");
        Assert.Equal(ResourceGenerator.MaxPower, result.State.PlayerState.Power); // clamped state
        Assert.True(
            result.Resources.Power > ResourceGenerator.MaxPower - 95,
            "The pool is the unclamped amount generated, so it exceeds the room the cap left.");
    }

    [Fact]
    public void Power_ShouldFillUpToTheCapWithoutExceedingIt()
    {
        // A gain that lands exactly on the cap is not reduced: §12's ceiling is
        // inclusive of 100. Driven through the write itself so the generated amount
        // is exact rather than dependent on which Gems Spawn draws.
        var state = PlayerState.Initial with { Power = 70 };

        var updated = ResourceGenerator.ApplyPower(
            state,
            new ResourceGeneration(0, 0, 0, 30, ClearedGemCounts.None));

        Assert.Equal(100, updated.Power);

        // And a Swap at the same starting Power can never exceed the cap either.
        var result = SwapExecutor.Execute(
            BattleWith(Match3Board(GemType.Power), state),
            new SwapRequest(From, To));

        Assert.True(result.IsAccepted);
        Assert.True(result.State.PlayerState.Power <= ResourceGenerator.MaxPower);
    }

    [Fact]
    public void ApplyPower_ShouldClampAtBothEndsOfTheRange()
    {
        // GAME_RULES.md §12: the range is 0–100. Generation never decreases Power,
        // so the floor is not reached by a Swap, but the single operation that
        // writes the persistent value enforces the whole documented range.
        var state = PlayerState.Initial with { Power = 40 };

        Assert.Equal(
            100,
            ResourceGenerator.ApplyPower(state, new ResourceGeneration(0, 0, 0, 1000, ClearedGemCounts.None)).Power);

        Assert.Equal(
            0,
            ResourceGenerator.ApplyPower(state, new ResourceGeneration(0, 0, 0, -1000, ClearedGemCounts.None)).Power);
    }

    [Fact]
    public void ApplyPower_ShouldChangeOnlyPower()
    {
        // The write-back replaces PlayerState wholesale, so every field this stage
        // does not own has to be carried across rather than reinitialized.
        var state = new PlayerState(
            HP: 777,
            MaxHP: 900,
            ATK: 61,
            DEF: 33,
            Power: 10,
            Crit: 9,
            Combo: 4,
            MatchCount: 12);

        var updated = ResourceGenerator.ApplyPower(
            state,
            new ResourceGeneration(0, 0, 0, 25, ClearedGemCounts.None));

        Assert.Equal(35, updated.Power);
        Assert.Equal(777, updated.HP);
        Assert.Equal(900, updated.MaxHP);
        Assert.Equal(61, updated.ATK);
        Assert.Equal(33, updated.DEF);
        Assert.Equal(9, updated.Crit);
        Assert.Equal(4, updated.Combo);
        Assert.Equal(12, updated.MatchCount);
    }

    [Fact]
    public void TransientPools_ShouldNotBeStoredInPlayerState()
    {
        // The task's boundary: Base Damage Pool, Defense Pool, and Heal Pool are
        // Transient Resolution State (GAME_STATE.md §3). They live on the execution
        // result for the downstream steps of the same resolution and are never
        // written into PlayerState or BattleState.
        var result = SwapExecutor.Execute(
            BattleWith(Match3Board(GemType.Atk)),
            new SwapRequest(From, To));

        Assert.True(result.IsAccepted);

        // The pool exists for the resolution.
        Assert.True(result.Resources.BaseDamagePool >= 30);

        // ATK Gems feed no persistent field, so the state's combat values are
        // exactly the ones the battle started with: the transient pools are not
        // stored as ATK, DEF, or HP.
        Assert.Equal(PlayerState.Initial.Power, result.State.PlayerState.Power);
        Assert.Equal(PlayerState.Initial.HP, result.State.PlayerState.HP);
        Assert.Equal(PlayerState.Initial.MaxHP, result.State.PlayerState.MaxHP);
        Assert.Equal(PlayerState.Initial.ATK, result.State.PlayerState.ATK);
        Assert.Equal(PlayerState.Initial.DEF, result.State.PlayerState.DEF);
        Assert.Equal(PlayerState.Initial.Crit, result.State.PlayerState.Crit);
    }

    [Fact]
    public void DefAndHpPools_ShouldNotBeStoredInPersistentStats()
    {
        // The same boundary for the two other transient pools: DEF Gems add to the
        // Defense pool, not to PlayerState.DEF (COMBAT_RULES.md §3.2 reads the
        // persistent DEF for mitigation), and HP Gems add to the Heal pool, not to
        // PlayerState.HP (healing is applied by §4's player-effects step, which is
        // not this stage).
        var defResult = SwapExecutor.Execute(
            BattleWith(Match3Board(GemType.Def)),
            new SwapRequest(From, To));

        Assert.True(defResult.IsAccepted);
        Assert.Equal(15, FirstPassResources(defResult.Resolution).DefensePool);
        Assert.Equal(PlayerState.Initial.DEF, defResult.State.PlayerState.DEF);

        var hpResult = SwapExecutor.Execute(
            BattleWith(Match3Board(GemType.Hp)),
            new SwapRequest(From, To));

        Assert.True(hpResult.IsAccepted);
        Assert.Equal(60, FirstPassResources(hpResult.Resolution).HealPool);
        Assert.Equal(PlayerState.Initial.HP, hpResult.State.PlayerState.HP);
    }

    [Fact]
    public void ResourceGeneration_ShouldBeDeterministicForTheSameResolution()
    {
        // MATCH3_RULES.md §7.2: generation consumes no RNG and is a pure function of
        // the cleared cells and their tiers. The same state and Swap therefore
        // always produce the same pools.
        var battle = BattleWith(Match3Board(GemType.Atk));

        var first = SwapExecutor.Execute(battle, new SwapRequest(From, To));
        var second = SwapExecutor.Execute(battle, new SwapRequest(From, To));

        Assert.True(first.IsAccepted);
        Assert.True(second.IsAccepted);
        Assert.Equal(first.Resources, second.Resources);
        Assert.Equal(first.State.PlayerState.Power, second.State.PlayerState.Power);
    }

    [Fact]
    public void Generate_ShouldNotConsumeRng()
    {
        // §7.2 item 1 / §4.5 item 4: Spawn is the only operation that advances the
        // RNG. Reading the cleared Gems and converting them draws nothing, so the
        // generation step cannot change the stream.
        var result = SwapExecutor.Execute(
            BattleWith(Match3Board(GemType.Atk)),
            new SwapRequest(From, To));

        Assert.True(result.IsAccepted);

        // The retained state is exactly the state the resolution's Spawn produced.
        Assert.Equal(result.Resolution.RngState, result.State.RngState);
    }

    [Fact]
    public void ClearedGems_ShouldCarryTheTypeTheCellHeldBeforeRemoval()
    {
        // GAME_EVENTS.md §2 item 2: the Gem type is read from the pre-removal board,
        // because the cell is cleared and its Gem consumed by the same act. This is
        // what makes the type available to resource generation after the fact — the
        // post-resolution board holds spawned Gems in those cells.
        var result = SwapExecutor.Execute(
            BattleWith(Match3Board(GemType.Hp)),
            new SwapRequest(From, To));

        Assert.True(result.IsAccepted);

        var pass = Assert.Single(result.Resolution.Passes);
        Assert.All(pass.ClearedGems, gem =>
        {
            Assert.Equal(GemType.Hp, gem.GemType);
            Assert.Equal(MatchTier.Match3, gem.Tier);
        });

        // The cleared cells are the union, each once, ascending index (§5.8.2 item 5).
        var indexes = pass.ClearedGems.Select(g => g.CellIndex).ToArray();
        Assert.Equal(indexes.OrderBy(i => i).ToArray(), indexes);
        Assert.Equal(indexes.Distinct().Count(), indexes.Length);

        // The union is also the reported ClearedCellUnion, so the two views agree.
        Assert.Equal(pass.ClearedCellUnion.OrderBy(i => i).ToArray(), indexes);
    }

    // =======================================================================
    // 9. Existing Match-3 behavior is unchanged
    // =======================================================================

    [Fact]
    public void ExistingMatchAndComboAccounting_ShouldBeUnchanged()
    {
        // The Match / Combo accounting of TASK-005 is not touched by this stage: a
        // committed Swap still counts exactly its Matches, and Combo is the number
        // of Matches the Swap produced.
        var result = SwapExecutor.Execute(
            BattleWith(Match3Board(GemType.Atk)),
            new SwapRequest(From, To));

        Assert.True(result.IsAccepted);

        var matchTotal = result.Resolution.Passes.Sum(p => p.Matches.Count);
        Assert.Equal(matchTotal, result.State.PlayerState.Combo);
        Assert.Equal(matchTotal, result.State.PlayerState.MatchCount);
    }

    [Fact]
    public void ExistingSwapCounters_ShouldBeUnchanged()
    {
        // MATCH3_RULES.md §8.1–§8.2: one committed Swap begins exactly one Turn and
        // increments Sequence by exactly 1, whatever it generated.
        var result = SwapExecutor.Execute(
            BattleWith(Match3Board(GemType.Atk)),
            new SwapRequest(From, To));

        Assert.True(result.IsAccepted);
        Assert.Equal(1, result.State.Turn);
        Assert.Equal(1, result.State.Sequence);
        Assert.Equal(CommittedSwapPair.FromCells(From, To), result.State.LastCommittedSwapPair);
    }

    [Fact]
    public void ExistingBoardResolution_ShouldBeUnchanged()
    {
        // The resource stage reads the resolution and adds nothing to the board: the
        // published board is still the stable one the cascade loop produced
        // (MATCH3_RULES.md §4.3 item 3).
        var result = SwapExecutor.Execute(
            BattleWith(Match3Board(GemType.Atk)),
            new SwapRequest(From, To));

        Assert.True(result.IsAccepted);
        Assert.Equal(result.Resolution.Board, result.State.BoardState);

        // §1: at rest the board holds exactly one Gem per cell.
        Assert.Equal(BoardState.CellCount, result.State.BoardState.ToCellArray().Length);
    }

    [Fact]
    public void ExistingEventStream_ShouldBeUnchanged()
    {
        // This stage introduces no event: GAME_EVENTS.md §3 item 7 gives PowerChanged
        // to its own owning task, and SIGNALR_PROTOCOL.md §3.2.2 item 2 closes the
        // wire event set. The events of a committed Swap are still exactly the
        // Match-3 ones the resolution produced.
        var result = SwapExecutor.Execute(
            BattleWith(Match3Board(GemType.Atk)),
            new SwapRequest(From, To));

        Assert.True(result.IsAccepted);
        Assert.NotEmpty(result.Events);
        Assert.All(result.Events, e => Assert.True(
            e.Type is BattleEventType.MatchCreated
                or BattleEventType.CascadeCreated
                or BattleEventType.ComboChanged
                or BattleEventType.GemMatched,
            $"Unexpected event type {e.Type}: this stage adds no event."));
    }

    [Fact]
    public void ExistingRoundTripFactories_ShouldCarryResourcesAcross()
    {
        // SwapExecutionResult.WithEvents / WithState replace one member each and must
        // carry the generated resources across unchanged, so a later pipeline stage
        // composing them cannot lose the transient pools.
        var result = SwapExecutor.Execute(
            BattleWith(Match3Board(GemType.Atk)),
            new SwapRequest(From, To));

        Assert.True(result.IsAccepted);

        var withEvents = result.WithEvents([.. result.Events]);
        Assert.Equal(result.Resources, withEvents.Resources);

        var withState = result.WithState(result.State);
        Assert.Equal(result.Resources, withState.Resources);
    }
}
