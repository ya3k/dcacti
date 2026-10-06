using GameServer.Application.Battle;
using GameServer.Application.Tests.Balance;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
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
        // Run boss-hoa-long x skilled x seed 20261001:
        // In TASK-194, this run had 6 rejected swaps and lost 80 damage (recorded 733 vs 813 HP lost).
        var hoaLong = BossDefinitions.All.First(b => b.BossId.Value == "boss-hoa-long");
        var result = await BalanceBaselineMatrix.RunAsync(hoaLong, BalancePolicies.SkilledName, 20261001UL);

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
            var run = await BalanceBaselineMatrix.RunAsync(boss, BalancePolicies.SkilledName, seed);
            var loss = boss.MaxHP - run.Metrics.BossHpMin;

            Assert.True(
                run.Metrics.PlayerDamageTotal >= loss,
                $"{bossId}/skilled/{seed} dealt {run.Metrics.PlayerDamageTotal} but boss lost {loss}");
            Assert.Equal(run.Metrics.PlayerDamageTotal, run.Metrics.PlayerDamagePerTurn.Sum());
        }
    }

    /// <summary>
    /// Requirement 2 / Finding D-194-4:
    /// Assert that for cast-terminated runs (Kim Lôi Vương × skilled, seeds 20261002, 20261003, 20261005),
    /// all per-Turn collections have exactly Turns records (series.Count == metrics.Turns).
    /// </summary>
    [Theory]
    [InlineData(20261002UL)]
    [InlineData(20261003UL)]
    [InlineData(20261005UL)]
    public async Task TASK195_PerTurnSeries_SatisfiesInvariantOnCastTerminatedRuns(ulong seed)
    {
        var kimLoiVuong = BossDefinitions.All.First(b => b.BossId.Value == "boss-kim-loi-vuong");
        var result = await BalanceBaselineMatrix.RunAsync(kimLoiVuong, BalancePolicies.SkilledName, seed);

        // Authoritative Turn contract verification:
        Assert.Equal(BalanceSimulationOutcome.Victory, result.Outcome);
        Assert.StartsWith("Card cast resolved", result.OutcomeDetail, StringComparison.Ordinal);
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
    /// </summary>
    [Theory]
    [InlineData(20261001UL, 30, 2)]
    [InlineData(20261002UL, 20, 2)]
    [InlineData(20261003UL, 40, 2)]
    [InlineData(20261004UL, 60, 3)]
    [InlineData(20261005UL, 40, 2)]
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
}
