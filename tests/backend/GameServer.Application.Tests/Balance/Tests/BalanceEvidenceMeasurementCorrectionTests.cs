using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using GameServer.Application.Battle;
using GameServer.Application.Tests.Balance;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using Xunit;

namespace GameServer.Application.Tests.Balance.Tests;

/// <summary>
/// Dedicated regression tests for TASK-195: Balance Evidence Measurement Audit & Correction.
///
/// Asserts resolution of all measurement and harness defects identified during TASK-194:
/// 1. M-03: Damage retention across rejected Swap proposals (Finding D-194-3).
/// 2. Per-Turn Series: Strict dimensional invariant series.Count == Turns on cast-terminated runs (Finding D-194-4).
/// 3. Mode identity: BalanceSimulationConfiguration.Mode threading and default Baseline behavior (Finding D-194-2).
/// 4. PowerSpentTotal: a Boss ability's drain reaches M-07 only through its negative
///    `PowerChanged` delta, and M-13 counts the canonical `BossCardCast` exactly once per
///    event (Finding D-194-7, retargeted under ADR-029 Slice 5 / TASK-313).
/// 5. Card damage element: Verification of Pet-element attribution for Card damage (Finding D-194-8).
/// 6. Pet passives: Isolation of BossPassiveTriggerCount from Pet passive triggers (Finding D-194-6).
/// </summary>
public sealed class BalanceEvidenceMeasurementCorrectionTests
{
    private static readonly PlayerId Owner = new("player-balance-sim");

    /// <summary>
    /// Requirement 1 / Finding D-194-3:
    /// Assert that when a Card cast deals damage and is followed by one or more rejected Swap
    /// proposals before a valid Swap commits, 100% of the Card cast damage is retained in
    /// PlayerDamageTotal and PlayerDamagePerTurn.
    /// Assert that PlayerDamageTotal >= Boss.MaxHP - BossHpMin for all non-healing runs.
    /// </summary>
    [Fact]
    public async Task TASK195_M03_RetainsCardCastDamageAcrossRejectedSwapProposals()
    {
        // Run boss-hoa-long x skilled-v1 x seed 20261001:
        // In TASK-194/TASK-195, this run used the legacy match-length heuristic which had 6 rejected swaps and lost 80 damage.
        var hoaLong = BossDefinitions.All.First(b => b.BossId.Value == "boss-hoa-long");
        var result = await BalanceBaselineMatrix.RunAsync(hoaLong, BalancePolicies.LegacySkilledName, 20261001UL);

        Assert.True(result.Metrics.RejectedSwaps > 0, "must exercise rejected swap path");
        Assert.True(result.Metrics.CastsTotal > 0, "must exercise card cast path");

        var bossHpLoss = hoaLong.MaxHP - result.Metrics.BossHpMin;

        // PlayerDamageTotal must fully cover the Boss HP lost (no damage undercounted).
        Assert.True(
            result.Metrics.PlayerDamageTotal >= bossHpLoss,
            $"Expected PlayerDamageTotal ({result.Metrics.PlayerDamageTotal}) >= Boss HP lost ({bossHpLoss})");

        // Sum of per-turn damage series must strictly equal PlayerDamageTotal.
        Assert.Equal(result.Metrics.PlayerDamageTotal, result.Metrics.PlayerDamagePerTurn.Sum());

        // In addition, verify all 19 runs previously affected in TASK-194 now retain 100% of damage.
        var affectedCells = new (string BossId, ulong Seed)[]
        {
            ("boss-hoa-long", 20261001UL),
            ("boss-hoa-long", 20261002UL),
            ("boss-hoa-long", 20261003UL),
            ("boss-hoa-long", 20261004UL),
            ("boss-hoa-long", 20261005UL),
            ("boss-thuy-ma", 20261001UL),
            ("boss-thuy-ma", 20261002UL),
            ("boss-thuy-ma", 20261004UL),
            ("boss-thuy-ma", 20261005UL),
            ("boss-son-thach-ve", 20261001UL),
            ("boss-son-thach-ve", 20261002UL),
            ("boss-son-thach-ve", 20261003UL),
            ("boss-son-thach-ve", 20261004UL),
            ("boss-son-thach-ve", 20261005UL),
            ("boss-kim-loi-vuong", 20261001UL),
            ("boss-kim-loi-vuong", 20261002UL),
            ("boss-kim-loi-vuong", 20261003UL),
            ("boss-kim-loi-vuong", 20261004UL),
            ("boss-kim-loi-vuong", 20261005UL),
        };

        foreach (var (bossId, seed) in affectedCells)
        {
            var boss = BossDefinitions.All.First(b => b.BossId.Value == bossId);
            var run = await BalanceBaselineMatrix.RunAsync(boss, BalancePolicies.LegacySkilledName, seed);
            var loss = boss.MaxHP - run.Metrics.BossHpMin;

            Assert.True(
                run.Metrics.PlayerDamageTotal >= loss,
                $"{bossId}/skilled/{seed} dealt {run.Metrics.PlayerDamageTotal} but boss lost {loss}");
            Assert.Equal(run.Metrics.PlayerDamageTotal, run.Metrics.PlayerDamagePerTurn.Sum());
        }
    }

    /// <summary>
    /// Requirement 2 / Finding D-194-4:
    /// Assert that all per-Turn collections have exactly Turns records (series.Count == metrics.Turns)
    /// and preserve the strict dimensional invariant across candidate victory runs.
    /// </summary>
    [Theory]
    [InlineData(20261002UL)]
    [InlineData(20261003UL)]
    [InlineData(20261005UL)]
    public async Task TASK195_PerTurnSeries_SatisfiesDimensionalInvariant(ulong seed)
    {
        var kimLoiVuong = BossDefinitions.All.First(b => b.BossId.Value == "boss-kim-loi-vuong");
        var result = await BalanceBaselineMatrix.RunAsync(kimLoiVuong, BalancePolicies.SkilledName, seed);

        // Authoritative Turn contract verification:
        Assert.Equal(BalanceSimulationOutcome.Victory, result.Outcome);
        Assert.Equal(result.FinalState!.Turn, result.Metrics.Turns);
        Assert.Equal(result.Metrics.Turns, result.Metrics.DurationTurns);

        var turns = result.Metrics.Turns;

        // Strict invariant: series.Count == Turns for all per-Turn collections.
        Assert.Equal(turns, result.Metrics.PlayerDamagePerTurn.Count);
        Assert.Equal(turns, result.Metrics.BossDamagePerTurn.Count);
        Assert.Equal(turns, result.Metrics.BossHpPerTurn.Count);
        Assert.Equal(turns, result.Metrics.PlayerHpPerTurn.Count);
        Assert.Equal(turns, result.Metrics.ComboPerTurn.Count);
        Assert.Equal(turns, result.Metrics.ResourceTrace.Count);

        // Sum and boundary checks:
        Assert.Equal(result.Metrics.PlayerDamageTotal, result.Metrics.PlayerDamagePerTurn.Sum());
        Assert.Equal(result.Metrics.BossDamageTotal, result.Metrics.BossDamagePerTurn.Sum());
        Assert.Equal(0, result.Metrics.BossHpMin);
        Assert.Equal(0, result.Metrics.BossHpPerTurn[^1]);
        Assert.Equal(0, result.Metrics.ResourceTrace[^1].BossHp);

        // CastTurns values must all be <= Turns:
        Assert.All(result.Metrics.CastTurns, turn => Assert.InRange(turn, 1, turns));
    }

    /// <summary>
    /// Requirement 2 / Finding D-194-4:
    /// Assert that when a simulation terminates through a Card cast, the terminal card cast
    /// resolution is correctly reconciled without emitting an uncommitted phantom Turn record.
    ///
    /// Seed 20261001UL terminates on Turn 27 via a Player Card cast (Iron Fang).
    /// Note: Pre-Slice-5 code used seed 20261002UL, which was then a card-terminated victory.
    /// Following the retirement of the legacy Boss Skill fallback in ADR-029 Slice 5 / TASK-313,
    /// seed 20261002UL's run shifted to terminate on a Turn 40 Swap, while seed 20261001UL terminates
    /// on a Turn 27 Card cast and faithfully exercises Finding D-194-4's invariant without phantom turns.
    /// </summary>
    [Fact]
    public async Task TASK195_PerTurnSeries_ReconcilesTerminalCardCastWithoutPhantomTurn()
    {
        var kimLoiVuong = BossDefinitions.All.First(b => b.BossId.Value == "boss-kim-loi-vuong");
        var result = await BalanceBaselineMatrix.RunAsync(kimLoiVuong, BalancePolicies.LegacySkilledName, 20261001UL);

        Assert.Equal(BalanceSimulationOutcome.Victory, result.Outcome);
        Assert.StartsWith("Card cast resolved", result.OutcomeDetail, StringComparison.Ordinal);
        Assert.Equal(result.FinalState!.Turn, result.Metrics.Turns);
        Assert.Equal(result.Metrics.Turns, result.Metrics.DurationTurns);

        var turns = result.Metrics.Turns;

        // Series counts strictly equal Turns without phantom turn:
        Assert.Equal(turns, result.Metrics.PlayerDamagePerTurn.Count);
        Assert.Equal(turns, result.Metrics.BossDamagePerTurn.Count);
        Assert.Equal(turns, result.Metrics.BossHpPerTurn.Count);
        Assert.Equal(turns, result.Metrics.PlayerHpPerTurn.Count);
        Assert.Equal(turns, result.Metrics.ComboPerTurn.Count);
        Assert.Equal(turns, result.Metrics.ResourceTrace.Count);

        // Damage totals match sums:
        Assert.Equal(result.Metrics.PlayerDamageTotal, result.Metrics.PlayerDamagePerTurn.Sum());
        Assert.Equal(result.Metrics.BossDamageTotal, result.Metrics.BossDamagePerTurn.Sum());

        // Terminal state reconciliation checks:
        Assert.Equal(0, result.FinalState.BossState.HP);
        Assert.Equal(0, result.Metrics.BossHpMin);
        Assert.Equal(0, result.Metrics.BossHpPerTurn[^1]);
        Assert.Equal(turns, result.Metrics.ResourceTrace[^1].Turn);
        Assert.Equal(0, result.Metrics.ResourceTrace[^1].BossHp);
        Assert.Contains(turns, result.Metrics.CastTurns);
    }

    /// <summary>
    /// Requirement 3 / Finding D-194-2:
    /// Assert that BalanceSimulationConfiguration exposes Mode with default Baseline,
    /// accepts ControlledComparison, and threads the value through to BalanceSimulationResult.Mode.
    /// </summary>
    [Fact]
    public async Task TASK195_ConfigurationMode_AcceptsControlledComparisonAndThreadsToResult()
    {
        var boss = BossDefinitions.All[0];
        var baseConfig = BalanceBaselineMatrix.Configuration(boss, BalancePolicies.PassiveName, 20261001UL);

        // Default configuration must be Baseline.
        Assert.Equal(BalanceSimulationMode.Baseline, baseConfig.Mode);

        var baselineResult = await new BalanceSimulator(baseConfig, new InMemoryBattleStateRepository()).RunAsync();
        Assert.Equal(BalanceSimulationMode.Baseline, baselineResult.Mode);

        // ControlledComparison configuration must flow to result.
        var comparisonConfig = baseConfig with { Mode = BalanceSimulationMode.ControlledComparison };
        Assert.Equal(BalanceSimulationMode.ControlledComparison, comparisonConfig.Mode);

        var comparisonResult = await new BalanceSimulator(comparisonConfig, new InMemoryBattleStateRepository()).RunAsync();
        Assert.Equal(BalanceSimulationMode.ControlledComparison, comparisonResult.Mode);

        // Invalid simulation must also preserve configured Mode.
        var invalidConfig = comparisonConfig with { MaxTurns = 0 };
        var invalidResult = await new BalanceSimulator(invalidConfig, new InMemoryBattleStateRepository()).RunAsync();
        Assert.Equal(BalanceSimulationOutcome.InvalidSimulation, invalidResult.Outcome);
        Assert.Equal(BalanceSimulationMode.ControlledComparison, invalidResult.Mode);
    }

    /// <summary>
    /// Requirement 4 / Finding D-194-7, retargeted under ADR-029 Slice 5 / TASK-313.
    ///
    /// M-13 counts the canonical <c>BossCardCast</c> event <b>exactly once per event</b>,
    /// and a Boss ability's drain reaches M-07's <c>PowerSpentTotal</c> only through the
    /// negative <c>PowerChanged</c> delta the same round reports — so a zero-effect /
    /// zero-damage cast still counts as a cast while adding nothing to
    /// <c>PowerSpentTotal</c>, and a real drain contributes exactly its negative delta.
    ///
    /// <b>Why this is accumulated directly from events.</b> The retired legacy path made
    /// the offline balance harness produce a <c>BossSkillCast</c> by itself. With that
    /// path removed (ADR-029 Slice 5 / TASK-313) and no provisioned Boss Card definitions
    /// in <c>BalanceBaselineMatrix.Cards</c>, the Boss cannot cast inside a simulated
    /// battle at all — and adding Boss Cards to that content set would change combat
    /// balance, which ADR-029 §4 places out of scope. The metric's own contract is
    /// therefore asserted on the events the production pipeline emits, through the same
    /// <see cref="MetricAccumulator"/> the matrix uses:
    ///
    /// <code>
    /// BossCardCast              -> BossCardCastCount +1 (M-13), never CastsTotal (M-08)
    /// PowerChanged(Boss, -20)   -> PowerSpentTotal +20 (M-07)
    /// PowerChanged(Boss,   0)   -> neither spent nor generated
    /// (no PowerChanged at all on a no-op drain: GAME_EVENTS.md §2 — "a mutation that
    ///  does not occur emits nothing")
    /// </code>
    /// </summary>
    [Theory]
    [InlineData("one-cast-no-drain-report", 1, null, 0)]
    [InlineData("three-casts-no-drain-report", 3, null, 0)]
    [InlineData("zero-delta-drain-report", 1, 0, 0)]
    [InlineData("real-drain-of-20", 1, -20, 20)]
    public void TASK195_PowerSpentTotal_RecordsTheDrainDeltaAndCountsEachBossCardCastOnce(
        string scenario,
        int castCount,
        int? drainDelta,
        int expectedPowerSpent)
    {
        _ = scenario;

        var configuration = BalanceBaselineMatrix.Configuration(
            BossDefinitions.ThuyMa, BalancePolicies.PassiveName, seed: 20261001UL);
        var accumulator = new MetricAccumulator(configuration);

        const int petPowerBeforeDrain = 30;

        var events = new List<BattleEvent>(castCount + 1);

        for (var cast = 0; cast < castCount; cast++)
        {
            // The canonical Boss ability event (SIGNALR_PROTOCOL.md §3.2.30): the
            // ability's identity and the Boss's canonical technical Identity.
            events.Add(BattleEvent.ForBossCardCast("drain-power", "boss-thuy-ma"));
        }

        if (drainDelta is { } delta)
        {
            // The drain's own authoritative mutation report (GAME_EVENTS.md §2,
            // SIGNALR_PROTOCOL.md §3.2.24): the signed pet-Power delta and the
            // resulting value. A zero delta is the no-op mutation the metric must not
            // read as a spend.
            events.Add(BattleEvent.ForPowerChanged(new PowerChangedEvent(
                PowerChangeSource.Boss,
                delta,
                Math.Max(0, petPowerBeforeDrain + delta))));
        }

        accumulator.BeginTurn(1);
        accumulator.RecordResolution(events);

        var powerFinal = Math.Max(0, petPowerBeforeDrain + (drainDelta ?? 0));
        var metrics = accumulator.Build(StateWithPetPower(powerFinal), BalanceSimulationOutcome.Victory);

        // M-13: one count per canonical cast event — no more, no fewer — and the same
        // event is never also read as an M-08 Player Card cast.
        Assert.Equal(castCount, metrics.BossCardCastCount);
        Assert.Equal(0, metrics.CastsTotal);
        Assert.Empty(metrics.CastsByCard);

        // M-07: only a negative delta is a spend; a zero delta moves neither total.
        Assert.Equal(expectedPowerSpent, metrics.PowerSpentTotal);
        Assert.Equal(0, metrics.PowerGeneratedTotal);
        Assert.Equal(powerFinal, metrics.PowerFinal);
    }

    /// <summary>
    /// A committed state carrying <paramref name="petPower"/>, used only as the state
    /// carrier <see cref="MetricAccumulator.Build"/> reads its per-Turn values from (the
    /// event-derived metrics under test do not depend on it).
    /// </summary>
    private static BattleState StateWithPetPower(int petPower)
    {
        var board = BoardState.FromCells(
            [.. Enumerable.Repeat(GemType.Atk, BoardState.CellCount)]);

        return new BattleState(
            BattleId: "balance-m13-metric",
            PlayerId: Owner,
            Turn: 1,
            Sequence: 1,
            RngSeed: 0UL,
            RngState: default,
            BoardState: board,
            Combo: 0,
            MatchCount: 0,
            PetState: new PetState(
                new PetId("petinstance-balance"),
                HP: 1000,
                MaxHP: 1000,
                ATK: 50,
                DEF: 50,
                Crit: 0,
                Power: petPower,
                Element.Hoa,
                new PassiveId("passive-xich-lang"),
                PassiveProgress.AtStart(5)),
            BossState: BossDefinitions.ThuyMa.ToInitialState());
    }

    /// <summary>
    /// Requirement 5 / Finding D-194-8:
    /// Assert that Card damage correctly uses the active Pet's Element (state.PetState.Element)
    /// when calculating Element Modifiers against the Boss.
    /// </summary>
    [Fact]
    public async Task TASK195_CardDamage_CalculatesModifierUsingPetElement()
    {
        // Baseline active Pet is Hỏa (Fire).
        // Against Kim Lôi Vương (Kim / Metal): Hỏa -> Kim is Advantage (x1.50).
        var kimLoiVuong = BossDefinitions.All.First(b => b.BossId.Value == "boss-kim-loi-vuong");
        var skilledKim = await BalanceBaselineMatrix.RunAsync(kimLoiVuong, BalancePolicies.SkilledName, 20261001UL);

        Assert.True(skilledKim.Metrics.CastsTotal > 0);
        Assert.True(skilledKim.Metrics.ElementAdvantageCount > 0, "Card damage with Hỏa Pet against Kim boss has advantage");

        // Against Thủy Ma (Thủy / Water): Hỏa -> Thủy is Disadvantage (x0.75).
        var thuyMa = BossDefinitions.All.First(b => b.BossId.Value == "boss-thuy-ma");
        var skilledThuy = await BalanceBaselineMatrix.RunAsync(thuyMa, BalancePolicies.SkilledName, 20261001UL);

        Assert.True(skilledThuy.Metrics.CastsTotal > 0);
        Assert.True(skilledThuy.Metrics.ElementDisadvantageCount > 0, "Card damage with Hỏa Pet against Thủy boss has disadvantage");
    }

    /// <summary>
    /// Requirement 6 / Finding D-194-6:
    /// Assert that Pet passive firings do not pollute M-13 BossPassiveTriggerCount
    /// and Pet passive effects remain unapplied by the production pipeline.
    /// </summary>
    [Fact]
    public async Task TASK195_PetPassives_DoNotPolluteBossPassiveTriggerCount()
    {
        // Sơn Thạch Vệ emits no match-charged passive triggers.
        // Active Pet (Xích Lang) charges every 5 matches and emits PassiveTriggered,
        // but MetricAccumulator filters source == Boss for M-13.
        var sonThachVe = BossDefinitions.All.First(b => b.BossId.Value == "boss-son-thach-ve");
        var result = await BalanceBaselineMatrix.RunAsync(sonThachVe, BalancePolicies.PassiveName, 20261001UL);

        Assert.True(result.Metrics.Turns > 5, "matches occurred to trigger pet passive");
        // M-13 must be 0 for Sơn Thạch Vệ (no match-charged Boss passive):
        Assert.Equal(0, result.Metrics.BossPassiveTriggerCount);
    }

    /// <summary>
    /// Requirement / Review C:
    /// Asserts that M-12 BossRegenerationTotal accurately measures known positive inter-turn HP
    /// increases, produces zero delta on non-increases, and does not record passive triggers at
    /// MaxHP as applied healing.
    /// </summary>
    [Fact]
    public async Task TASK195_M12_BossRegenerationTotal_MeasuresKnownIncreasesAndIgnoresClampedTriggers()
    {
        var mocYeu = BossDefinitions.All.First(b => b.BossId.Value == "boss-moc-yeu");
        var config = BalanceBaselineMatrix.Configuration(mocYeu, BalancePolicies.PassiveName, 20261001UL);
        var run = await BalanceBaselineMatrix.RunAsync(mocYeu, BalancePolicies.PassiveName, 20261001UL);
        var baseState = run.FinalState!;

        var accumulator = new MetricAccumulator(config);

        // Turn 1: Initial state at HP = 2000 (below MaxHP 5000).
        var stateT1 = baseState with { BossState = baseState.BossState with { HP = 2000, MaxHP = 5000 } };
        accumulator.BeginTurn(1);
        accumulator.EndTurn(stateT1);

        var m1 = accumulator.Build(stateT1, BalanceSimulationOutcome.Victory);
        Assert.Equal(0, m1.BossRegenerationTotal);
        Assert.Equal(0, m1.BossRegenerationTurns);

        // Turn 2: Known positive increase from 2000 to 2250 (+250 delta).
        var stateT2 = baseState with { BossState = baseState.BossState with { HP = 2250, MaxHP = 5000 } };
        accumulator.BeginTurn(2);
        accumulator.EndTurn(stateT2);

        var m2 = accumulator.Build(stateT2, BalanceSimulationOutcome.Victory);
        Assert.Equal(250, m2.BossRegenerationTotal);
        Assert.Equal(1, m2.BossRegenerationTurns);

        // Turn 3: HP decreases from 2250 to 1900 (-350 delta) -> No positive delta recorded.
        var stateT3 = baseState with { BossState = baseState.BossState with { HP = 1900, MaxHP = 5000 } };
        accumulator.BeginTurn(3);
        accumulator.EndTurn(stateT3);

        var m3 = accumulator.Build(stateT3, BalanceSimulationOutcome.Victory);
        Assert.Equal(250, m3.BossRegenerationTotal);
        Assert.Equal(1, m3.BossRegenerationTurns);

        // Turn 4: HP unchanged at 1900 (0 delta) -> No positive delta recorded.
        var stateT4 = baseState with { BossState = baseState.BossState with { HP = 1900, MaxHP = 5000 } };
        accumulator.BeginTurn(4);
        accumulator.EndTurn(stateT4);

        var m4 = accumulator.Build(stateT4, BalanceSimulationOutcome.Victory);
        Assert.Equal(250, m4.BossRegenerationTotal);
        Assert.Equal(1, m4.BossRegenerationTurns);

        // Turn 5 & 6: Passive triggers at MaxHP (5000).
        // Triggers fire, but Boss HP remains clamped to 5000 -> 0 applied healing.
        var stateT5 = baseState with { BossState = baseState.BossState with { HP = 5000, MaxHP = 5000 } };
        accumulator.BeginTurn(5);
        accumulator.RecordResolution([
            BattleEvent.ForPassiveTriggered(new PassiveTriggeredEvent(new PassiveId("boss-moc-yeu-regen"), 5, 5, PassiveEventSource.Boss, "boss-moc-yeu"))
        ]);
        accumulator.EndTurn(stateT5);

        var stateT6 = baseState with { BossState = baseState.BossState with { HP = 5000, MaxHP = 5000 } };
        accumulator.BeginTurn(6);
        accumulator.RecordResolution([
            BattleEvent.ForPassiveTriggered(new PassiveTriggeredEvent(new PassiveId("boss-moc-yeu-regen"), 5, 5, PassiveEventSource.Boss, "boss-moc-yeu"))
        ]);
        accumulator.EndTurn(stateT6);

        var m6 = accumulator.Build(stateT6, BalanceSimulationOutcome.Victory);
        // BossPassiveTriggerCount recorded both triggers:
        Assert.Equal(2, m6.BossPassiveTriggerCount);
        // But BossRegenerationTotal only increased by the earlier +3100 from 1900 -> 5000 on Turn 5,
        // and on Turn 6 (5000 -> 5000) delta was 0, so Turn 6 added 0 healing despite the passive trigger:
        Assert.Equal(250 + 3100, m6.BossRegenerationTotal);
        Assert.Equal(2, m6.BossRegenerationTurns);
    }

    /// <summary>
    /// Requirement / Review C:
    /// Asserts that under ADR-026 shared-board rules, non-Mộc Yêu bosses can match HP gems
    /// and produce observed inter-turn HP increases (BossRegenerationTotal > 0).
    /// </summary>
    [Fact]
    public async Task TASK195_M12_NonMocYeuBosses_CanHealFromSharedBoardHpGemMatches()
    {
        var hoaLong = BossDefinitions.All.First(b => b.BossId.Value == "boss-hoa-long");
        var result = await BalanceBaselineMatrix.RunAsync(hoaLong, BalancePolicies.SkilledName, 20261001UL);

        // Hỏa Long has no regen passive, but matches HP gems on the shared board under ADR-026:
        Assert.True(result.Metrics.BossRegenerationTotal > 0, "Hỏa Long heals from shared-board HP gem matches");
        Assert.True(result.Metrics.BossRegenerationTurns > 0, "Hỏa Long has non-zero regeneration turns");
    }

    /// <summary>
    /// Requirement / Review D Candidate Verification & Historical Baseline Preservation (OD-314-1 = C):
    ///
    /// Distinguishes the historical reproducibility verification of the TASK-296 Candidate D
    /// proposal from the current-runtime offline matrix behavior:
    ///
    /// 1. <b>Historical Candidate D Verification:</b> When the preserved candidate artifact
    ///    (<c>TASK-296-canonical-75-candidate.json</c>) is available, asserts its exact pinned
    ///    distribution (8 victories, 41 defeats, 26 stalemates), its nine reported shifted runs,
    ///    its 66 unshifted runs against <c>TASK-194-baseline-simulation-results.json</c>, and its
    ///    SHA-256 hash (<c>A9E9ABBD9B342C9B8849E5CECB1580D3154915782F539F5A3547252E4EE4FC62</c>).
    ///
    /// 2. <b>Current-Runtime Offline Matrix Behavior:</b> Executes the canonical 75 matrix runs under
    ///    the post-Slice-5 engine (ADR-029 Slice 5 / TASK-313). Because the legacy Boss Skill fallback
    ///    was removed and <see cref="BalanceBaselineMatrix.Cards"/> contains no provisioned Boss Card rows,
    ///    the Boss never casts an ability in this offline harness. Consequently, two runs
    ///    (<c>boss-son-thach-ve/skilled/20261003</c> at Turn 127 and <c>boss-son-thach-ve/skilled/20261005</c>
    ///    at Turn 41) shift from Defeat to Victory due to the absence of legacy boss skill damage, yielding
    ///    10 victories, 39 defeats, 26 stalemates (the distribution documented in TASK-300 / TASK-313).
    ///
    /// 3. <b>M-13 Boss Ability Cadence Telemetry:</b> In this offline scenario lacking Boss Cards,
    ///    <c>BossCardCastCount</c> is 0 across all 75 runs. Per OD-314-1, this is verified and classified
    ///    as <i>unmeasured or structurally unavailable</i>, not as zero live cadence.
    ///
    /// 4. <b>Protected Artifact Guard:</b> Pinned historical artifacts and candidate archives are protected
    ///    from in-place mutation and must never be overwritten with live post-Slice-5 results.
    /// </summary>
    [Fact]
    public async Task TASK296_ReviewD_Canonical75RunCandidate_Verification()
    {
        // 1. Historical Candidate D artifact verification (when present):
        var candidatePath = Path.Combine(AppContext.BaseDirectory, "TASK-296-canonical-75-candidate.json");
        if (File.Exists(candidatePath))
        {
            var candidateBytes = await File.ReadAllBytesAsync(candidatePath);
            var candidateHash = Convert.ToHexString(SHA256.HashData(candidateBytes));
            Assert.Equal("A9E9ABBD9B342C9B8849E5CECB1580D3154915782F539F5A3547252E4EE4FC62", candidateHash);

            using var candidateDoc = JsonDocument.Parse(candidateBytes);
            var historicalCandidateRuns = candidateDoc.RootElement.GetProperty("runs").EnumerateArray().ToList();
            Assert.Equal(75, historicalCandidateRuns.Count);

            var histVictories = historicalCandidateRuns.Count(r => r.GetProperty("M-14_outcome").GetString() == "Victory");
            var histDefeats = historicalCandidateRuns.Count(r => r.GetProperty("M-14_outcome").GetString() == "Defeat");
            var histStalemates = historicalCandidateRuns.Count(r => r.GetProperty("M-14_outcome").GetString() == "Stalemate");

            Assert.Equal(8, histVictories);
            Assert.Equal(41, histDefeats);
            Assert.Equal(26, histStalemates);

            // Verify the other 66 outcomes against the pinned TASK-194 historical baseline artifact:
            var artifactPath = Path.Combine(BalanceBaselineMatrix.RepositoryRoot(), "tasks", "artifacts", "TASK-194-baseline-simulation-results.json");
            using var task194Doc = JsonDocument.Parse(await File.ReadAllTextAsync(artifactPath));
            var task194Runs = task194Doc.RootElement.GetProperty("runs");

            var shiftedKeys = new HashSet<string>(StringComparer.Ordinal)
            {
                "boss-thuy-ma/average/20261001",
                "boss-thuy-ma/average/20261003",
                "boss-thuy-ma/average/20261005",
                "boss-moc-yeu/skilled/20261004",
                "boss-son-thach-ve/skilled/20261001",
                "boss-son-thach-ve/skilled/20261002",
                "boss-son-thach-ve/skilled/20261003",
                "boss-son-thach-ve/skilled/20261004",
                "boss-kim-loi-vuong/skilled/20261004",
            };

            var candidateMap = historicalCandidateRuns.ToDictionary(
                r => $"{r.GetProperty("bossId").GetString()}/{r.GetProperty("policy").GetString()}/{r.GetProperty("seed").GetUInt64()}",
                r => r.GetProperty("M-14_outcome").GetString()!,
                StringComparer.Ordinal);

            var unshiftedCount = 0;
            foreach (var runElem in task194Runs.EnumerateArray())
            {
                var bId = runElem.GetProperty("bossId").GetString()!;
                var pName = runElem.GetProperty("policy").GetString()!;
                var seed = runElem.GetProperty("seed").GetUInt64();
                var histOutcome = runElem.GetProperty("M-14_outcome").GetString()!;
                var key = $"{bId}/{pName}/{seed}";

                if (!shiftedKeys.Contains(key))
                {
                    Assert.Equal(histOutcome, candidateMap[key]);
                    unshiftedCount++;
                }
            }
            Assert.Equal(66, unshiftedCount);
        }

        // 2. Current-runtime offline matrix execution:
        var cells = BalanceBaselineMatrix.Cells().ToArray();
        Assert.Equal(75, cells.Length);

        var results = new List<BalanceSimulationResult>(75);
        foreach (var (boss, policyName, seed) in cells)
        {
            var effectivePolicy = policyName == BalancePolicies.SkilledName
                ? BalancePolicies.LegacySkilledName
                : policyName;
            results.Add(await BalanceBaselineMatrix.RunAsync(boss, effectivePolicy, seed));
        }

        // 3. Confirm all 75 complete and InvalidSimulation == 0:
        Assert.Equal(75, results.Count);
        var invalid = results.Where(r => r.Outcome == BalanceSimulationOutcome.InvalidSimulation).ToArray();
        Assert.Empty(invalid);

        // 4. Current-runtime outcome distribution under post-Slice-5 engine:
        // Due to the retirement of legacy Boss Skill fallback and absence of Boss Cards in BalanceBaselineMatrix.Cards,
        // Sơn Thạch Vệ seeds 20261003 (Turn 127) and 20261005 (Turn 41) shift to Victory (total: 10 victories, 39 defeats, 26 stalemates).
        var currentVictories = results.Count(r => r.Outcome == BalanceSimulationOutcome.Victory);
        var currentDefeats = results.Count(r => r.Outcome == BalanceSimulationOutcome.Defeat);
        var currentStalemates = results.Count(r => r.Outcome == BalanceSimulationOutcome.Stalemate);

        Assert.Equal(10, currentVictories);
        Assert.Equal(39, currentDefeats);
        Assert.Equal(26, currentStalemates);

        // 5. Verify the two post-Slice-5 shifted victory runs:
        var currentMap = results.ToDictionary(
            r => $"{r.BossId}/{r.PolicyName}/{r.Seed}",
            StringComparer.Ordinal);

        var r3 = currentMap["boss-son-thach-ve/skilled/20261003"];
        Assert.Equal(BalanceSimulationOutcome.Victory, r3.Outcome);
        Assert.Equal(127, r3.Metrics.Turns);

        var r5 = currentMap["boss-son-thach-ve/skilled/20261005"];
        Assert.Equal(BalanceSimulationOutcome.Victory, r5.Outcome);
        Assert.Equal(41, r5.Metrics.Turns);

        // 6. Verify M-13 Boss ability cadence is structurally unavailable (0 casts) in offline matrix:
        // OD-314-1 constraint: report zero observed casts as structurally unavailable, never as live cadence = 0.
        var totalBossCardCasts = results.Sum(r => r.Metrics.BossCardCastCount);
        Assert.Equal(0, totalBossCardCasts);
    }
}
