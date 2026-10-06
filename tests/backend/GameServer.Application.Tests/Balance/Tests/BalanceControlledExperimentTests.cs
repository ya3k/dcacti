using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace GameServer.Application.Tests.Balance.Tests;

/// <summary>
/// Execution and verification suite for TASK-197: Controlled Balance Experiment Matrix Execution.
///
/// <b>These tests execute empirical simulations and assert measurement integrity.</b>
/// They do NOT make balance judgments or emit BALANCED/UNBALANCED verdicts.
/// Every test asserts structural validity, absence of invalid simulations,
/// preservation of the series.Count == Turns invariant, and bit-identical determinism.
/// </summary>
public sealed class BalanceControlledExperimentTests
{
    private static readonly string[] TechnicalOutcomes =
        ["Victory", "Defeat", "Stalemate", "InvalidSimulation"];

    /// <summary>
    /// EXP-01: Mộc Yêu regeneration controlled sweep (Target: Q-8 / B-07).
    /// Tests 2 valid variants across 2 policies and 5 seeds = 20 runs.
    /// Documents 2% and 3% magnitude variants as blocked by hardcoded constant in BattleStateService.cs.
    /// </summary>
    [Fact]
    public async Task EXP01_MocYeuRegen_ShouldExecuteExactRunCount_AndPreserveInvariants()
    {
        var runs = await BalanceControlledExperimentMatrix.RunExp01Async();

        Assert.Equal(20, runs.Count);
        Assert.All(runs, run =>
        {
            Assert.Contains(run.Result.Outcome.ToString(), TechnicalOutcomes);
            Assert.NotEqual(BalanceSimulationOutcome.InvalidSimulation, run.Result.Outcome);
            Assert.Equal(run.Result.Metrics.Turns, run.Result.Metrics.PlayerDamagePerTurn.Count);
            Assert.Equal(run.Result.Metrics.Turns, run.Result.Metrics.BossDamagePerTurn.Count);
            Assert.Equal(run.Result.Metrics.Turns, run.Result.Metrics.BossHpPerTurn.Count);
            Assert.Equal(run.Result.Metrics.Turns, run.Result.Metrics.PlayerHpPerTurn.Count);
            Assert.Equal(run.Result.Metrics.Turns, run.Result.Metrics.ComboPerTurn.Count);
        });

        // Baseline (5% / 5 matches) under average policy must reproduce 5/5 Stalemates at 1000 turns.
        var baselineAverageRuns = runs.Where(r => r.Variant == "baseline_5pct_5matches" && r.Result.PolicyName == BalancePolicies.AverageName).ToArray();
        Assert.Equal(5, baselineAverageRuns.Length);
        Assert.All(baselineAverageRuns, r =>
        {
            Assert.Equal(BalanceSimulationOutcome.Stalemate, r.Result.Outcome);
            Assert.Equal(1000, r.Result.Metrics.Turns);
        });

        // Threshold 8 runs must be recorded.
        var threshold8Runs = runs.Where(r => r.Variant == "threshold_8matches").ToArray();
        Assert.Equal(10, threshold8Runs.Length);
    }

    /// <summary>
    /// EXP-02: Heal vs Shield isolation and ordering (Target: Q-5 / B-09).
    /// Tests 5 loadouts across 5 Bosses, 2 policies, and 5 seeds = 250 runs.
    /// </summary>
    [Fact]
    public async Task EXP02_HealVsShield_ShouldExecuteExactRunCount_AndIsolateShieldCasts()
    {
        var runs = await BalanceControlledExperimentMatrix.RunExp02Async();

        Assert.Equal(250, runs.Count);
        Assert.All(runs, run =>
        {
            Assert.Contains(run.Result.Outcome.ToString(), TechnicalOutcomes);
            Assert.NotEqual(BalanceSimulationOutcome.InvalidSimulation, run.Result.Outcome);
            Assert.Equal(run.Result.Metrics.Turns, run.Result.Metrics.PlayerDamagePerTurn.Count);
            Assert.Equal(run.Result.Metrics.Turns, run.Result.Metrics.BossDamagePerTurn.Count);
            Assert.Equal(run.Result.Metrics.Turns, run.Result.Metrics.BossHpPerTurn.Count);
            Assert.Equal(run.Result.Metrics.Turns, run.Result.Metrics.PlayerHpPerTurn.Count);
            Assert.Equal(run.Result.Metrics.Turns, run.Result.Metrics.ComboPerTurn.Count);
        });

        // Shield-only variant must demonstrate that Shield is actively cast when Heal is absent.
        var shieldOnlyRuns = runs.Where(r => r.Variant == "shield_only").ToArray();
        Assert.Equal(50, shieldOnlyRuns.Length);

        // Average policy on shield-only: whenever Power >= 20, Shield should be cast.
        var shieldOnlyAverage = shieldOnlyRuns.Where(r => r.Result.PolicyName == BalancePolicies.AverageName).ToArray();
        Assert.All(shieldOnlyAverage, r =>
        {
            r.Result.Metrics.CastsByCard.TryGetValue("card-shield", out var shieldCasts);
            Assert.True(shieldCasts > 0, $"Shield must be cast in shield-only run for {r.Result.BossId}/{r.Result.Seed}");
        });
    }

    /// <summary>
    /// EXP-03: Full Pet Skill card damage / efficiency sweep (Target: Q-9 / B-08).
    /// Tests 5 Pet Skill cards with native Pets across 5 Bosses and 5 seeds = 125 runs.
    /// </summary>
    [Fact]
    public async Task EXP03_CardDamageSweep_ShouldExecuteExactRunCount_AndPreserveInvariants()
    {
        var runs = await BalanceControlledExperimentMatrix.RunExp03Async();

        Assert.Equal(125, runs.Count);
        Assert.All(runs, run =>
        {
            Assert.Contains(run.Result.Outcome.ToString(), TechnicalOutcomes);
            Assert.NotEqual(BalanceSimulationOutcome.InvalidSimulation, run.Result.Outcome);
            Assert.Equal(run.Result.Metrics.Turns, run.Result.Metrics.PlayerDamagePerTurn.Count);
            Assert.Equal(run.Result.Metrics.Turns, run.Result.Metrics.BossDamagePerTurn.Count);
            Assert.Equal(run.Result.Metrics.Turns, run.Result.Metrics.BossHpPerTurn.Count);
            Assert.Equal(run.Result.Metrics.Turns, run.Result.Metrics.PlayerHpPerTurn.Count);
            Assert.Equal(run.Result.Metrics.Turns, run.Result.Metrics.ComboPerTurn.Count);
        });
    }

    /// <summary>
    /// EXP-04: Relic ablation matrix (Target: Q-10 / B-10).
    /// Tests 11 loadouts (1 unequipped control + 10 individual relics) across 5 Bosses, 2 policies, and 5 seeds = 550 runs.
    /// </summary>
    [Fact]
    public async Task EXP04_RelicAblation_ShouldExecuteExactRunCount_AndPreserveInvariants()
    {
        var runs = await BalanceControlledExperimentMatrix.RunExp04Async();

        Assert.Equal(550, runs.Count);
        Assert.All(runs, run =>
        {
            Assert.Contains(run.Result.Outcome.ToString(), TechnicalOutcomes);
            Assert.NotEqual(BalanceSimulationOutcome.InvalidSimulation, run.Result.Outcome);
            Assert.Equal(run.Result.Metrics.Turns, run.Result.Metrics.PlayerDamagePerTurn.Count);
            Assert.Equal(run.Result.Metrics.Turns, run.Result.Metrics.BossDamagePerTurn.Count);
            Assert.Equal(run.Result.Metrics.Turns, run.Result.Metrics.BossHpPerTurn.Count);
            Assert.Equal(run.Result.Metrics.Turns, run.Result.Metrics.PlayerHpPerTurn.Count);
            Assert.Equal(run.Result.Metrics.Turns, run.Result.Metrics.ComboPerTurn.Count);
        });

        // Unequipped control must record exactly 0 relic triggers.
        var controlRuns = runs.Where(r => r.Variant == "unequipped_control").ToArray();
        Assert.Equal(50, controlRuns.Length);
        Assert.All(controlRuns, r =>
        {
            Assert.Equal(0, r.Result.Metrics.RelicTriggersTotal);
            Assert.Empty(r.Result.Metrics.RelicTriggersByRelic);
        });
    }

    /// <summary>
    /// EXP-05: Power Charge cost sensitivity (Target: Q-2 / B-01).
    /// Tests 3 cost variants across 5 Bosses, 2 policies, and 5 seeds = 150 runs.
    /// </summary>
    [Fact]
    public async Task EXP05_PowerChargeCost_ShouldExecuteExactRunCount_AndPreserveInvariants()
    {
        var runs = await BalanceControlledExperimentMatrix.RunExp05Async();

        Assert.Equal(150, runs.Count);
        Assert.All(runs, run =>
        {
            Assert.Contains(run.Result.Outcome.ToString(), TechnicalOutcomes);
            Assert.NotEqual(BalanceSimulationOutcome.InvalidSimulation, run.Result.Outcome);
            Assert.Equal(run.Result.Metrics.Turns, run.Result.Metrics.PlayerDamagePerTurn.Count);
            Assert.Equal(run.Result.Metrics.Turns, run.Result.Metrics.BossDamagePerTurn.Count);
            Assert.Equal(run.Result.Metrics.Turns, run.Result.Metrics.BossHpPerTurn.Count);
            Assert.Equal(run.Result.Metrics.Turns, run.Result.Metrics.PlayerHpPerTurn.Count);
            Assert.Equal(run.Result.Metrics.Turns, run.Result.Metrics.ComboPerTurn.Count);
        });
    }

    /// <summary>
    /// Master execution test: runs all 1,095 experiment cells, verifies all invariants,
    /// serializes evidence to JSON, writes the artifact, and verifies bit-identical determinism.
    /// </summary>
    [Fact]
    public async Task TASK197_MasterMatrix_ShouldGenerateBitIdenticalArtifact()
    {
        // 1. Run all experiments (1,095 runs total).
        var runs1 = await BalanceControlledExperimentMatrix.RunAllExperimentsAsync();
        Assert.Equal(1095, runs1.Count);

        // 2. Verify all outcomes are valid and all per-turn series strictly match Turns.
        foreach (var run in runs1)
        {
            Assert.NotEqual(BalanceSimulationOutcome.InvalidSimulation, run.Result.Outcome);
            var m = run.Result.Metrics;
            Assert.Equal(m.Turns, m.PlayerDamagePerTurn.Count);
            Assert.Equal(m.Turns, m.BossDamagePerTurn.Count);
            Assert.Equal(m.Turns, m.BossHpPerTurn.Count);
            Assert.Equal(m.Turns, m.PlayerHpPerTurn.Count);
            Assert.Equal(m.Turns, m.ComboPerTurn.Count);
        }

        // 3. Serialize and write artifact.
        var json1 = BalanceControlledExperimentMatrix.SerializeEvidence(runs1);
        var artifactPath = BalanceControlledExperimentMatrix.WriteArtifact(json1);
        Assert.True(File.Exists(artifactPath), $"Artifact must exist at {artifactPath}");

        var hash1 = ComputeSha256(json1);

        // 4. Run second complete execution and verify bit-identical serialization.
        var runs2 = await BalanceControlledExperimentMatrix.RunAllExperimentsAsync();
        var json2 = BalanceControlledExperimentMatrix.SerializeEvidence(runs2);
        var hash2 = ComputeSha256(json2);

        Assert.Equal(hash1, hash2);
        Assert.Equal(json1.Length, json2.Length);
    }

    private static string ComputeSha256(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
