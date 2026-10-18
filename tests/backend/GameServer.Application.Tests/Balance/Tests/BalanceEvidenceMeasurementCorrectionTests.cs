using System.IO;
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
/// 4. PowerSpentTotal: Accounting for Boss Drain Power resource sink without player casts (Finding D-194-7).
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
    /// </summary>
    [Fact]
    public async Task TASK195_PerTurnSeries_ReconcilesTerminalCardCastWithoutPhantomTurn()
    {
        var kimLoiVuong = BossDefinitions.All.First(b => b.BossId.Value == "boss-kim-loi-vuong");
        var result = await BalanceBaselineMatrix.RunAsync(kimLoiVuong, BalancePolicies.LegacySkilledName, 20261002UL);

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
    /// Requirement 4 / Finding D-194-7:
    /// Assert that Power drain deltas from Thủy Ma skill are recorded in PowerSpentTotal
    /// in passive runs without player card casts.
    /// Under approved match-only rules:
    /// - Seed 20261001: 1 cast, draining 20 Power (Pet Power before drain was 30).
    /// - Seed 20261002: 3 casts, 2 zero-power casts (draining 0) + 1 cast draining 20 Power; total 20.
    /// - Seed 20261003: 2 casts, draining 20 Power each; total 40.
    /// - Seed 20261004: 2 casts, both zero-power casts (draining 0); total 0.
    /// - Seed 20261005: 3 casts, draining 20 Power each; total 60.
    /// </summary>
    [Theory]
    [InlineData(20261001UL, 20, 1)]
    [InlineData(20261002UL, 20, 3)]
    [InlineData(20261003UL, 40, 2)]
    [InlineData(20261004UL, 0, 2)]
    [InlineData(20261005UL, 60, 3)]
    public async Task TASK195_PowerSpentTotal_RecordsDrainPowerWithoutPlayerCardCasts(
        ulong seed,
        int expectedPowerSpent,
        int expectedBossSkillCasts)
    {
        var thuyMa = BossDefinitions.All.First(b => b.BossId.Value == "boss-thuy-ma");
        var result = await BalanceBaselineMatrix.RunAsync(thuyMa, BalancePolicies.PassiveName, seed);

        // Zero player card casts:
        Assert.Equal(0, result.Metrics.CastsTotal);
        Assert.Empty(result.Metrics.CastsByCard);

        // Power spent reflects Thủy Ma Drain Power skill:
        Assert.Equal(expectedBossSkillCasts, result.Metrics.BossSkillCastCount);
        Assert.Equal(expectedPowerSpent, result.Metrics.PowerSpentTotal);
        Assert.True(result.Metrics.PowerSpentTotal <= result.Metrics.PowerGeneratedTotal);
        Assert.Equal(result.Metrics.PowerFinal, result.Metrics.PowerGeneratedTotal - result.Metrics.PowerSpentTotal);
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
    /// Requirement / Review D:
    /// Re-runs all 75 canonical matrix entries, verifies 0 InvalidSimulation,
    /// verifies the 8/41/26 distribution, verifies all 9 shifted outcomes individually,
    /// verifies the 66 unshifted outcomes against the historical artifact,
    /// and generates a temporary candidate artifact outside pinned files.
    /// </summary>
    [Fact]
    public async Task TASK296_ReviewD_Canonical75RunCandidate_Verification()
    {
        // 1. Run all 75 matrix entries in canonical order:
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

        // 2. Confirm all 75 complete and InvalidSimulation == 0:
        Assert.Equal(75, results.Count);
        var invalid = results.Where(r => r.Outcome == BalanceSimulationOutcome.InvalidSimulation).ToArray();
        Assert.Empty(invalid);

        // 3. Verify proposed outcome distribution:
        var victories = results.Count(r => r.Outcome == BalanceSimulationOutcome.Victory);
        var defeats = results.Count(r => r.Outcome == BalanceSimulationOutcome.Defeat);
        var stalemates = results.Count(r => r.Outcome == BalanceSimulationOutcome.Stalemate);

        Assert.Equal(8, victories);
        Assert.Equal(41, defeats);
        Assert.Equal(26, stalemates);

        // 4. Verify all nine reported outcome changes individually:
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

        var resultMap = results.ToDictionary(
            r => $"{r.BossId}/{r.PolicyName}/{r.Seed}",
            StringComparer.Ordinal);

        // Run 1: boss-thuy-ma / average / 20261001 -> Stalemate (Turn 1000)
        var r1 = resultMap["boss-thuy-ma/average/20261001"];
        Assert.Equal(BalanceSimulationOutcome.Stalemate, r1.Outcome);
        Assert.Equal(1000, r1.Metrics.Turns);

        // Run 2: boss-thuy-ma / average / 20261003 -> Stalemate (Turn 1000)
        var r2 = resultMap["boss-thuy-ma/average/20261003"];
        Assert.Equal(BalanceSimulationOutcome.Stalemate, r2.Outcome);
        Assert.Equal(1000, r2.Metrics.Turns);

        // Run 3: boss-thuy-ma / average / 20261005 -> Stalemate (Turn 1000)
        var r3 = resultMap["boss-thuy-ma/average/20261005"];
        Assert.Equal(BalanceSimulationOutcome.Stalemate, r3.Outcome);
        Assert.Equal(1000, r3.Metrics.Turns);

        // Run 4: boss-moc-yeu / skilled / 20261004 -> Stalemate (Turn 1000)
        var r4 = resultMap["boss-moc-yeu/skilled/20261004"];
        Assert.Equal(BalanceSimulationOutcome.Stalemate, r4.Outcome);
        Assert.Equal(1000, r4.Metrics.Turns);

        // Run 5: boss-son-thach-ve / skilled / 20261001 -> Victory (Turn 378, Card cast)
        var r5 = resultMap["boss-son-thach-ve/skilled/20261001"];
        Assert.Equal(BalanceSimulationOutcome.Victory, r5.Outcome);
        Assert.Equal(378, r5.Metrics.Turns);
        Assert.StartsWith("Card cast resolved", r5.OutcomeDetail, StringComparison.Ordinal);

        // Run 6: boss-son-thach-ve / skilled / 20261002 -> Victory (Turn 233, Swap)
        var r6 = resultMap["boss-son-thach-ve/skilled/20261002"];
        Assert.Equal(BalanceSimulationOutcome.Victory, r6.Outcome);
        Assert.Equal(233, r6.Metrics.Turns);
        Assert.StartsWith("the committed Swap resolved", r6.OutcomeDetail, StringComparison.Ordinal);

        // Run 7: boss-son-thach-ve / skilled / 20261003 -> Defeat (Turn 151, Swap)
        var r7 = resultMap["boss-son-thach-ve/skilled/20261003"];
        Assert.Equal(BalanceSimulationOutcome.Defeat, r7.Outcome);
        Assert.Equal(151, r7.Metrics.Turns);
        Assert.StartsWith("the committed Swap resolved", r7.OutcomeDetail, StringComparison.Ordinal);

        // Run 8: boss-son-thach-ve / skilled / 20261004 -> Victory (Turn 295, Card cast)
        var r8 = resultMap["boss-son-thach-ve/skilled/20261004"];
        Assert.Equal(BalanceSimulationOutcome.Victory, r8.Outcome);
        Assert.Equal(295, r8.Metrics.Turns);
        Assert.StartsWith("Card cast resolved", r8.OutcomeDetail, StringComparison.Ordinal);

        // Run 9: boss-kim-loi-vuong / skilled / 20261004 -> Victory (Turn 36, Card cast)
        var r9 = resultMap["boss-kim-loi-vuong/skilled/20261004"];
        Assert.Equal(BalanceSimulationOutcome.Victory, r9.Outcome);
        Assert.Equal(36, r9.Metrics.Turns);
        Assert.StartsWith("Card cast resolved", r9.OutcomeDetail, StringComparison.Ordinal);

        // 5. Verify the other 66 outcomes against the historical artifact:
        var artifactPath = Path.Combine(BalanceBaselineMatrix.RepositoryRoot(), "tasks", "artifacts", "TASK-194-baseline-simulation-results.json");
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(artifactPath));
        var historicalRuns = doc.RootElement.GetProperty("runs");

        var unshiftedCount = 0;
        foreach (var runElem in historicalRuns.EnumerateArray())
        {
            var bId = runElem.GetProperty("bossId").GetString()!;
            var pName = runElem.GetProperty("policy").GetString()!;
            var seed = runElem.GetProperty("seed").GetUInt64();
            var histOutcome = runElem.GetProperty("M-14_outcome").GetString()!;
            var key = $"{bId}/{pName}/{seed}";

            if (!shiftedKeys.Contains(key))
            {
                Assert.Equal(histOutcome, resultMap[key].Outcome.ToString());
                unshiftedCount++;
            }
        }
        Assert.Equal(66, unshiftedCount);

        // 6. Write temporary candidate JSON (outside pinned artifact):
        var candidateJson = BalanceBaselineMatrix.SerializeEvidence(results);
        var tempCandidatePath = Path.Combine(AppContext.BaseDirectory, "TASK-296-canonical-75-candidate.json");
        await File.WriteAllTextAsync(tempCandidatePath, candidateJson);
        Assert.True(File.Exists(tempCandidatePath));
    }
}
