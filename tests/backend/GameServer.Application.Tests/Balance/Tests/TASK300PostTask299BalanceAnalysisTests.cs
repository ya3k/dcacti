using System.Security.Cryptography;
using System.Text.Json;
using GameServer.Application.Tests.Balance;
using GameServer.Domain.Bosses;
using Xunit;
using Xunit.Abstractions;

namespace GameServer.Application.Tests.Balance.Tests;

/// <summary>
/// TASK-300: Post-TASK-299 Balance Re-baseline and Regression Analysis.
///
/// Executes controlled balance-analysis over the gameplay change introduced by TASK-299:
/// - DEF gems grant temporary DEF stat buffs instead of persistent Shield pools.
/// - Pet DEF buff duration: 1 Turn.
/// - Boss DEF buff duration: 2 Turns.
/// - Refresh-not-stack policy.
/// - Pinned historical baselines (TASK-194, TASK-296, TASK-298) remain protected and untouched.
/// </summary>
public sealed class TASK300PostTask299BalanceAnalysisTests
{
    private readonly ITestOutputHelper _output;

    public TASK300PostTask299BalanceAnalysisTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Executes the canonical 75-run matrix under current mechanics with Benchmark v1
    /// (using BalancePolicies.LegacySkilledName) and compares run-by-run with TASK-296 candidate.
    /// </summary>
    [Fact]
    public async Task BenchmarkV1_LegacySkilled_Run75Matrix_AndCompareWithTask296Candidate()
    {
        // 1. Load protected TASK-296 candidate
        var task296CandidatePath = Path.Combine(AppContext.BaseDirectory, "TASK-296-canonical-75-candidate.json");
        Assert.True(File.Exists(task296CandidatePath), $"TASK-296 candidate not found at {task296CandidatePath}");

        var candidateBytes = await File.ReadAllBytesAsync(task296CandidatePath);
        var candidateHash = Convert.ToHexString(SHA256.HashData(candidateBytes));
        Assert.Equal("A9E9ABBD9B342C9B8849E5CECB1580D3154915782F539F5A3547252E4EE4FC62", candidateHash);

        using var oldDoc = JsonDocument.Parse(candidateBytes);
        var oldRuns = oldDoc.RootElement.GetProperty("runs").EnumerateArray().ToList();
        Assert.Equal(75, oldRuns.Count);

        var oldOutcomes = new Dictionary<string, (string Outcome, int Turns, string Detail)>(StringComparer.Ordinal);
        var oldDamage = new Dictionary<string, (int PlayerDmg, int BossDmg)>(StringComparer.Ordinal);
        foreach (var r in oldRuns)
        {
            var b = r.GetProperty("bossId").GetString()!;
            var p = r.GetProperty("policy").GetString()!;
            var s = r.GetProperty("seed").GetUInt64();
            var o = r.GetProperty("M-14_outcome").GetString()!;
            var detail = r.GetProperty("M-14_outcomeDetail").GetString()!;
            var m = r.GetProperty("metrics");
            var t = m.GetProperty("M-01_turns").GetInt32();
            var pd = m.GetProperty("M-03_playerDamageTotal").GetInt32();
            var bd = m.GetProperty("M-04_bossDamageTotal").GetInt32();

            var key = $"{b}/{p}/{s}";
            oldOutcomes[key] = (o, t, detail);
            oldDamage[key] = (pd, bd);
        }

        // 2. Run the canonical 75 matrix with LegacySkilled (Benchmark v1 under TASK-299)
        var cells = BalanceBaselineMatrix.Cells().ToArray();
        Assert.Equal(75, cells.Length);

        var newResults = new List<BalanceSimulationResult>(75);
        foreach (var (boss, policyName, seed) in cells)
        {
            var effectivePolicy = policyName == BalancePolicies.SkilledName
                ? BalancePolicies.LegacySkilledName
                : policyName;
            newResults.Add(await BalanceBaselineMatrix.RunAsync(boss, effectivePolicy, seed));
        }

        Assert.Equal(75, newResults.Count);
        Assert.Empty(newResults.Where(r => r.Outcome == BalanceSimulationOutcome.InvalidSimulation));

        // 3. Output comparison summaries
        var victories = newResults.Count(r => r.Outcome == BalanceSimulationOutcome.Victory);
        var defeats = newResults.Count(r => r.Outcome == BalanceSimulationOutcome.Defeat);
        var stalemates = newResults.Count(r => r.Outcome == BalanceSimulationOutcome.Stalemate);

        _output.WriteLine("=== TASK-300 BENCHMARK V1 (LEGACY SKILLED POLICY) SUMMARY ===");
        _output.WriteLine($"Total Runs: {newResults.Count}");
        _output.WriteLine($"Victories: {victories} (Historical TASK-296: 8, Historical TASK-194: 5)");
        _output.WriteLine($"Defeats: {defeats} (Historical TASK-296: 41, Historical TASK-194: 48)");
        _output.WriteLine($"Stalemates: {stalemates} (Historical TASK-296: 26, Historical TASK-194: 22)");

        // Per policy
        foreach (var policy in new[] { BalancePolicies.PassiveName, BalancePolicies.AverageName, BalancePolicies.LegacySkilledName })
        {
            var pRuns = newResults.Where(r => r.PolicyName == policy).ToList();
            var v = pRuns.Count(r => r.Outcome == BalanceSimulationOutcome.Victory);
            var d = pRuns.Count(r => r.Outcome == BalanceSimulationOutcome.Defeat);
            var s = pRuns.Count(r => r.Outcome == BalanceSimulationOutcome.Stalemate);
            _output.WriteLine($"Policy {policy}: V={v}, D={d}, S={s}");
        }

        // Per boss
        foreach (var boss in BalanceBaselineMatrix.Bosses)
        {
            var bRuns = newResults.Where(r => r.BossId == boss.BossId.Value).ToList();
            var v = bRuns.Count(r => r.Outcome == BalanceSimulationOutcome.Victory);
            var d = bRuns.Count(r => r.Outcome == BalanceSimulationOutcome.Defeat);
            var s = bRuns.Count(r => r.Outcome == BalanceSimulationOutcome.Stalemate);
            _output.WriteLine($"Boss {boss.BossId.Value}: V={v}, D={d}, S={s}");
        }

        // Run-by-run differences
        var shiftedOutcomes = 0;
        var shiftedTurns = 0;
        _output.WriteLine("=== RUN-BY-RUN DIFFERENCES AGAINST TASK-296 CANDIDATE ===");
        foreach (var r in newResults)
        {
            // Note: In oldRuns, policy name was "skilled", in newResults it's "legacy-skilled"
            var oldKey = $"{r.BossId}/{(r.PolicyName == BalancePolicies.LegacySkilledName ? "skilled" : r.PolicyName)}/{r.Seed}";
            var old = oldOutcomes[oldKey];
            var newOutcome = r.Outcome.ToString();
            var newTurns = r.Metrics.Turns;

            var outcomeChanged = old.Outcome != newOutcome;
            var turnsChanged = old.Turns != newTurns;

            if (outcomeChanged) shiftedOutcomes++;
            if (turnsChanged) shiftedTurns++;

            if (outcomeChanged || turnsChanged)
            {
                _output.WriteLine($"DIFF: {r.BossId}/{r.PolicyName}/{r.Seed} | Old: {old.Outcome} ({old.Turns}t, {old.Detail}) -> New: {newOutcome} ({newTurns}t, {r.OutcomeDetail}) | Dmg P:{r.Metrics.PlayerDamageTotal} B:{r.Metrics.BossDamageTotal}");
            }
        }
        _output.WriteLine($"Total shifted outcomes: {shiftedOutcomes}, Total shifted turns: {shiftedTurns}");

        // 4. Save candidate artifact to isolated path
        var outputDir = Path.Combine(AppContext.BaseDirectory, "artifacts");
        Directory.CreateDirectory(outputDir);
        var candidateArtifactPath = Path.Combine(outputDir, "TASK-300-candidate-75-benchmark-v1.json");
        var candidateJson = BalanceBaselineMatrix.SerializeEvidence(newResults);
        await File.WriteAllTextAsync(candidateArtifactPath, candidateJson);
        var artifactHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(candidateArtifactPath)));
        _output.WriteLine($"Benchmark v1 Candidate Artifact: {candidateArtifactPath}");
        _output.WriteLine($"Benchmark v1 SHA-256: {artifactHash}");
    }

    /// <summary>
    /// Executes the canonical 75-run matrix under current mechanics with Benchmark v2
    /// (using BalancePolicies.SkilledName, the corrected linear-match policy from TASK-298).
    /// </summary>
    [Fact]
    public async Task BenchmarkV2_CorrectedSkilled_Run75Matrix_AndAnalyzeMetrics()
    {
        var cells = BalanceBaselineMatrix.Cells().ToArray();
        Assert.Equal(75, cells.Length);

        var newResults = new List<BalanceSimulationResult>(75);
        foreach (var (boss, policyName, seed) in cells)
        {
            newResults.Add(await BalanceBaselineMatrix.RunAsync(boss, policyName, seed));
        }

        Assert.Equal(75, newResults.Count);
        Assert.Empty(newResults.Where(r => r.Outcome == BalanceSimulationOutcome.InvalidSimulation));

        var victories = newResults.Count(r => r.Outcome == BalanceSimulationOutcome.Victory);
        var defeats = newResults.Count(r => r.Outcome == BalanceSimulationOutcome.Defeat);
        var stalemates = newResults.Count(r => r.Outcome == BalanceSimulationOutcome.Stalemate);

        _output.WriteLine("=== TASK-300 BENCHMARK V2 (CORRECTED SKILLED POLICY) SUMMARY ===");
        _output.WriteLine($"Total Runs: {newResults.Count}");
        _output.WriteLine($"Victories: {victories}");
        _output.WriteLine($"Defeats: {defeats}");
        _output.WriteLine($"Stalemates: {stalemates}");

        // Per policy
        foreach (var policy in BalanceBaselineMatrix.PolicyNames)
        {
            var pRuns = newResults.Where(r => r.PolicyName == policy).ToList();
            var v = pRuns.Count(r => r.Outcome == BalanceSimulationOutcome.Victory);
            var d = pRuns.Count(r => r.Outcome == BalanceSimulationOutcome.Defeat);
            var s = pRuns.Count(r => r.Outcome == BalanceSimulationOutcome.Stalemate);
            var avgTurns = pRuns.Average(r => r.Metrics.Turns);
            var avgPlayerDmg = pRuns.Average(r => r.Metrics.PlayerDamageTotal);
            var avgBossDmg = pRuns.Average(r => r.Metrics.BossDamageTotal);
            var avgPowerGen = pRuns.Average(r => r.Metrics.PowerGeneratedTotal);
            var avgPowerSpent = pRuns.Average(r => r.Metrics.PowerSpentTotal);
            var avgCasts = pRuns.Average(r => r.Metrics.CastsTotal);
            _output.WriteLine($"Policy {policy}: V={v}, D={d}, S={s} | AvgTurns={avgTurns:F1}, AvgPDmg={avgPlayerDmg:F1}, AvgBDmg={avgBossDmg:F1}, Casts={avgCasts:F1}, PwrGen={avgPowerGen:F1}, PwrSpent={avgPowerSpent:F1}");
        }

        // Per boss
        foreach (var boss in BalanceBaselineMatrix.Bosses)
        {
            var bRuns = newResults.Where(r => r.BossId == boss.BossId.Value).ToList();
            var v = bRuns.Count(r => r.Outcome == BalanceSimulationOutcome.Victory);
            var d = bRuns.Count(r => r.Outcome == BalanceSimulationOutcome.Defeat);
            var s = bRuns.Count(r => r.Outcome == BalanceSimulationOutcome.Stalemate);
            var avgTurns = bRuns.Average(r => r.Metrics.Turns);
            var avgPlayerDmg = bRuns.Average(r => r.Metrics.PlayerDamageTotal);
            var avgBossDmg = bRuns.Average(r => r.Metrics.BossDamageTotal);
            var avgRegen = bRuns.Average(r => r.Metrics.BossRegenerationTotal);
            _output.WriteLine($"Boss {boss.BossId.Value}: V={v}, D={d}, S={s} | AvgTurns={avgTurns:F1}, AvgPDmg={avgPlayerDmg:F1}, AvgBDmg={avgBossDmg:F1}, AvgRegen={avgRegen:F1}");
        }

        // Duration distribution
        var turnsList = newResults.Select(r => r.Metrics.Turns).OrderBy(t => t).ToList();
        var under50 = newResults.Count(r => r.Metrics.Turns <= 50);
        var over200 = newResults.Count(r => r.Metrics.Turns >= 200 && r.Metrics.Turns < 1000);
        var at1000 = newResults.Count(r => r.Metrics.Turns >= 1000);
        var median = turnsList[turnsList.Count / 2];
        _output.WriteLine($"Duration: Min={turnsList[0]}, Median={median}, Max={turnsList[^1]}, <=50t={under50}, 200-999t={over200}, =1000t={at1000}");

        // Save candidate artifact
        var outputDir = Path.Combine(AppContext.BaseDirectory, "artifacts");
        Directory.CreateDirectory(outputDir);
        var candidateArtifactPath = Path.Combine(outputDir, "TASK-300-candidate-75-benchmark-v2.json");
        var candidateJson = BalanceBaselineMatrix.SerializeEvidence(newResults);
        await File.WriteAllTextAsync(candidateArtifactPath, candidateJson);
        var artifactHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(candidateArtifactPath)));
        _output.WriteLine($"Benchmark v2 Candidate Artifact: {candidateArtifactPath}");
        _output.WriteLine($"Benchmark v2 SHA-256: {artifactHash}");
    }

    /// <summary>
    /// Verifies 100% bit-identical determinism of simulations under TASK-299 mechanics.
    /// </summary>
    [Fact]
    public async Task VerifyDeterminism_PostTask299Simulations()
    {
        foreach (var boss in BalanceBaselineMatrix.Bosses)
        {
            foreach (var policy in BalanceBaselineMatrix.PolicyNames)
            {
                var seed = 20261001UL;
                var run1 = await BalanceBaselineMatrix.RunAsync(boss, policy, seed);
                var run2 = await BalanceBaselineMatrix.RunAsync(boss, policy, seed);

                Assert.Equal(run1.Outcome, run2.Outcome);
                Assert.Equal(run1.OutcomeDetail, run2.OutcomeDetail);
                Assert.Equal(run1.Metrics.Turns, run2.Metrics.Turns);
                Assert.Equal(run1.Metrics.PlayerDamageTotal, run2.Metrics.PlayerDamageTotal);
                Assert.Equal(run1.Metrics.BossDamageTotal, run2.Metrics.BossDamageTotal);
                Assert.Equal(run1.Metrics.PowerGeneratedTotal, run2.Metrics.PowerGeneratedTotal);
                Assert.Equal(run1.Metrics.PowerSpentTotal, run2.Metrics.PowerSpentTotal);
                Assert.Equal(run1.Metrics.CastsTotal, run2.Metrics.CastsTotal);
                Assert.Equal(run1.Metrics.BossHpMin, run2.Metrics.BossHpMin);
                Assert.Equal(run1.Metrics.PlayerHpMin, run2.Metrics.PlayerHpMin);
            }
        }
    }

    /// <summary>
    /// Comprehensive multi-dimensional comparison across TASK-194 baseline, TASK-296 candidate,
    /// current TASK-299 Benchmark v1 (LegacySkilled), and current TASK-299 Benchmark v2 (Skilled).
    /// Generates detailed metrics for outcome distribution, duration, damage, economy, and survivability.
    /// </summary>
    [Fact]
    public async Task ComprehensiveBalanceImpactAnalysis()
    {
        // 1. Load TASK-194 baseline
        var task194Path = Path.Combine(BalanceBaselineMatrix.RepositoryRoot(), "tasks", "artifacts", "TASK-194-baseline-simulation-results.json");
        using var doc194 = JsonDocument.Parse(await File.ReadAllTextAsync(task194Path));
        var runs194 = doc194.RootElement.GetProperty("runs").EnumerateArray().ToList();

        // 2. Load TASK-296 candidate
        var task296CandidatePath = Path.Combine(AppContext.BaseDirectory, "TASK-296-canonical-75-candidate.json");
        using var doc296 = JsonDocument.Parse(await File.ReadAllBytesAsync(task296CandidatePath));
        var runs296 = doc296.RootElement.GetProperty("runs").EnumerateArray().ToList();

        // 3. Run Current Benchmark v1 (LegacySkilled)
        var cells = BalanceBaselineMatrix.Cells().ToArray();
        var currentV1 = new List<BalanceSimulationResult>(75);
        foreach (var (boss, policyName, seed) in cells)
        {
            var p = policyName == BalancePolicies.SkilledName ? BalancePolicies.LegacySkilledName : policyName;
            currentV1.Add(await BalanceBaselineMatrix.RunAsync(boss, p, seed));
        }

        // 4. Run Current Benchmark v2 (Skilled)
        var currentV2 = new List<BalanceSimulationResult>(75);
        foreach (var (boss, policyName, seed) in cells)
        {
            currentV2.Add(await BalanceBaselineMatrix.RunAsync(boss, policyName, seed));
        }

        _output.WriteLine("================================================================================");
        _output.WriteLine("           TASK-300 POST-TASK-299 COMPREHENSIVE BALANCE ANALYSIS");
        _output.WriteLine("================================================================================");

        // --- SECTION 1: OUTCOME DISTRIBUTIONS ---
        _output.WriteLine("\n--- SECTION 1: 75-RUN CANONICAL OUTCOME DISTRIBUTIONS ---");
        _output.WriteLine($"{"Baseline / Framework",-35} | {"Victory",7} | {"Defeat",7} | {"Stalemate",9} | {"Win Rate",8}");
        _output.WriteLine(new string('-', 75));

        PrintDistributionRow("TASK-194 Baseline (Original)",
            runs194.Count(r => r.GetProperty("M-14_outcome").GetString() == "Victory"),
            runs194.Count(r => r.GetProperty("M-14_outcome").GetString() == "Defeat"),
            runs194.Count(r => r.GetProperty("M-14_outcome").GetString() == "Stalemate"));

        PrintDistributionRow("TASK-296 Candidate (Match-Only v1)",
            runs296.Count(r => r.GetProperty("M-14_outcome").GetString() == "Victory"),
            runs296.Count(r => r.GetProperty("M-14_outcome").GetString() == "Defeat"),
            runs296.Count(r => r.GetProperty("M-14_outcome").GetString() == "Stalemate"));

        PrintDistributionRow("TASK-299 Current (LegacySkilled / v1)",
            currentV1.Count(r => r.Outcome == BalanceSimulationOutcome.Victory),
            currentV1.Count(r => r.Outcome == BalanceSimulationOutcome.Defeat),
            currentV1.Count(r => r.Outcome == BalanceSimulationOutcome.Stalemate));

        PrintDistributionRow("TASK-299 Current (CorrectedSkilled / v2)",
            currentV2.Count(r => r.Outcome == BalanceSimulationOutcome.Victory),
            currentV2.Count(r => r.Outcome == BalanceSimulationOutcome.Defeat),
            currentV2.Count(r => r.Outcome == BalanceSimulationOutcome.Stalemate));

        // --- SECTION 2: PER-BOSS VICTORY MATRIX ---
        _output.WriteLine("\n--- SECTION 2: PER-BOSS VICTORY BREAKDOWN (Out of 15 runs per Boss) ---");
        _output.WriteLine($"{"Boss",-18} | {"TASK-194",8} | {"TASK-296",8} | {"Current v1",10} | {"Current v2",10}");
        _output.WriteLine(new string('-', 65));

        foreach (var boss in BalanceBaselineMatrix.Bosses)
        {
            var bId = boss.BossId.Value;
            var v194 = runs194.Count(r => r.GetProperty("bossId").GetString() == bId && r.GetProperty("M-14_outcome").GetString() == "Victory");
            var v296 = runs296.Count(r => r.GetProperty("bossId").GetString() == bId && r.GetProperty("M-14_outcome").GetString() == "Victory");
            var vCur1 = currentV1.Count(r => r.BossId == bId && r.Outcome == BalanceSimulationOutcome.Victory);
            var vCur2 = currentV2.Count(r => r.BossId == bId && r.Outcome == BalanceSimulationOutcome.Victory);
            _output.WriteLine($"{bId,-18} | {v194,8} | {v296,8} | {vCur1,10} | {vCur2,10}");
        }

        // --- SECTION 3: PER-POLICY BREAKDOWN (25 runs each) ---
        _output.WriteLine("\n--- SECTION 3: PER-POLICY BREAKDOWN (V / D / S) ---");
        _output.WriteLine($"{"Policy",-15} | {"TASK-194",15} | {"TASK-296",15} | {"Current v1",15} | {"Current v2",15}");
        _output.WriteLine(new string('-', 85));

        foreach (var p in new[] { "passive", "average", "skilled" })
        {
            var row194 = PolicyVDS(runs194, p);
            var row296 = PolicyVDS(runs296, p);
            var rowCur1 = PolicyVDSResults(currentV1, p);
            var rowCur2 = PolicyVDSResults(currentV2, p);
            _output.WriteLine($"{p,-15} | {row194,15} | {row296,15} | {rowCur1,15} | {rowCur2,15}");
        }

        // --- SECTION 4: BATTLE DURATION AND TURN COUNTS ---
        _output.WriteLine("\n--- SECTION 4: BATTLE DURATION DISTRIBUTION ---");
        _output.WriteLine($"{"Metric",-25} | {"TASK-296 (Candidate)",20} | {"Current v1",15} | {"Current v2",15}");
        _output.WriteLine(new string('-', 82));

        var turns296 = runs296.Select(r => r.GetProperty("metrics").GetProperty("M-01_turns").GetInt32()).OrderBy(t => t).ToList();
        var turnsV1 = currentV1.Select(r => r.Metrics.Turns).OrderBy(t => t).ToList();
        var turnsV2 = currentV2.Select(r => r.Metrics.Turns).OrderBy(t => t).ToList();

        _output.WriteLine($"{"Min Turns",-25} | {turns296[0],20} | {turnsV1[0],15} | {turnsV2[0],15}");
        _output.WriteLine($"{"Median Turns",-25} | {turns296[37],20} | {turnsV1[37],15} | {turnsV2[37],15}");
        _output.WriteLine($"{"Mean Turns",-25} | {turns296.Average(),20:F1} | {turnsV1.Average(),15:F1} | {turnsV2.Average(),15:F1}");
        _output.WriteLine($"{"Max Turns",-25} | {turns296[^1],20} | {turnsV1[^1],15} | {turnsV2[^1],15}");
        _output.WriteLine($"{"Runs <= 50 Turns",-25} | {turns296.Count(t => t <= 50),20} | {turnsV1.Count(t => t <= 50),15} | {turnsV2.Count(t => t <= 50),15}");
        _output.WriteLine($"{"Runs 51-199 Turns",-25} | {turns296.Count(t => t > 50 && t < 200),20} | {turnsV1.Count(t => t > 50 && t < 200),15} | {turnsV2.Count(t => t > 50 && t < 200),15}");
        _output.WriteLine($"{"Runs 200-999 Turns",-25} | {turns296.Count(t => t >= 200 && t < 1000),20} | {turnsV1.Count(t => t >= 200 && t < 1000),15} | {turnsV2.Count(t => t >= 200 && t < 1000),15}");
        _output.WriteLine($"{"Stalemates (=1000 Turns)",-25} | {turns296.Count(t => t >= 1000),20} | {turnsV1.Count(t => t >= 1000),15} | {turnsV2.Count(t => t >= 1000),15}");

        // --- SECTION 5: DAMAGE & MITIGATION ANALYSIS ---
        _output.WriteLine("\n--- SECTION 5: AVERAGE DAMAGE PER TURN & TOTALS (Skilled Policy Only) ---");
        var skilled296 = runs296.Where(r => r.GetProperty("policy").GetString() == "skilled").ToList();
        var skilledV1 = currentV1.Where(r => r.PolicyName == "skilled").ToList();
        var skilledV2 = currentV2.Where(r => r.PolicyName == "skilled").ToList();

        var avgP296 = skilled296.Average(r => r.GetProperty("metrics").GetProperty("M-03_playerDamageTotal").GetInt32());
        var avgB296 = skilled296.Average(r => r.GetProperty("metrics").GetProperty("M-04_bossDamageTotal").GetInt32());
        var avgPV1 = skilledV1.Average(r => r.Metrics.PlayerDamageTotal);
        var avgBV1 = skilledV1.Average(r => r.Metrics.BossDamageTotal);
        var avgPV2 = skilledV2.Average(r => r.Metrics.PlayerDamageTotal);
        var avgBV2 = skilledV2.Average(r => r.Metrics.BossDamageTotal);

        _output.WriteLine($"{"Skilled Player Dmg Total",-25} | {avgP296,20:F1} | {avgPV1,15:F1} | {avgPV2,15:F1}");
        _output.WriteLine($"{"Skilled Boss Dmg Total",-25} | {avgB296,20:F1} | {avgBV1,15:F1} | {avgBV2,15:F1}");

        // Average damage per turn
        var pDmgPerTurn296 = skilled296.Average(r => (double)r.GetProperty("metrics").GetProperty("M-03_playerDamageTotal").GetInt32() / r.GetProperty("metrics").GetProperty("M-01_turns").GetInt32());
        var bDmgPerTurn296 = skilled296.Average(r => (double)r.GetProperty("metrics").GetProperty("M-04_bossDamageTotal").GetInt32() / r.GetProperty("metrics").GetProperty("M-01_turns").GetInt32());
        var pDmgPerTurnV1 = skilledV1.Average(r => (double)r.Metrics.PlayerDamageTotal / r.Metrics.Turns);
        var bDmgPerTurnV1 = skilledV1.Average(r => (double)r.Metrics.BossDamageTotal / r.Metrics.Turns);
        var pDmgPerTurnV2 = skilledV2.Average(r => (double)r.Metrics.PlayerDamageTotal / r.Metrics.Turns);
        var bDmgPerTurnV2 = skilledV2.Average(r => (double)r.Metrics.BossDamageTotal / r.Metrics.Turns);

        _output.WriteLine($"{"Player Dmg / Turn (Skilled)",-25} | {pDmgPerTurn296,20:F1} | {pDmgPerTurnV1,15:F1} | {pDmgPerTurnV2,15:F1}");
        _output.WriteLine($"{"Boss Dmg / Turn (Skilled)",-25} | {bDmgPerTurn296,20:F1} | {bDmgPerTurnV1,15:F1} | {bDmgPerTurnV2,15:F1}");

        // --- SECTION 6: SURVIVABILITY FLOOR ANALYSIS ---
        _output.WriteLine("\n--- SECTION 6: SURVIVABILITY FLOOR (MIN HP OBSERVED) ---");
        var avgPetMinHp296 = skilled296.Average(r => r.GetProperty("metrics").GetProperty("M-06_playerHpMin").GetInt32());
        var avgBossMinHp296 = skilled296.Average(r => r.GetProperty("metrics").GetProperty("M-05_bossHpMin").GetInt32());
        var avgPetMinHpV1 = skilledV1.Average(r => r.Metrics.PlayerHpMin);
        var avgBossMinHpV1 = skilledV1.Average(r => r.Metrics.BossHpMin);
        var avgPetMinHpV2 = skilledV2.Average(r => r.Metrics.PlayerHpMin);
        var avgBossMinHpV2 = skilledV2.Average(r => r.Metrics.BossHpMin);

        _output.WriteLine($"{"Avg Pet HP Min (Skilled)",-25} | {avgPetMinHp296,20:F1} | {avgPetMinHpV1,15:F1} | {avgPetMinHpV2,15:F1}");
        _output.WriteLine($"{"Avg Boss HP Min (Skilled)",-25} | {avgBossMinHp296,20:F1} | {avgBossMinHpV1,15:F1} | {avgBossMinHpV2,15:F1}");

        // Passive Pet HP Min & Turns
        var passive296 = runs296.Where(r => r.GetProperty("policy").GetString() == "passive").ToList();
        var passiveV1 = currentV1.Where(r => r.PolicyName == BalancePolicies.PassiveName).ToList();
        var avgPassiveTurns296 = passive296.Average(r => r.GetProperty("metrics").GetProperty("M-01_turns").GetInt32());
        var avgPassiveTurnsV1 = passiveV1.Average(r => r.Metrics.Turns);
        _output.WriteLine($"{"Passive Avg Turns to Defeat",-25} | {avgPassiveTurns296,20:F1} | {avgPassiveTurnsV1,15:F1} | {avgPassiveTurnsV1,15:F1}");
    }

    private void PrintDistributionRow(string label, int v, int d, int s)
    {
        var winRate = (double)v / (v + d + s) * 100.0;
        _output.WriteLine($"{label,-35} | {v,7} | {d,7} | {s,9} | {winRate,7:F1}%");
    }

    private static string PolicyVDS(IEnumerable<JsonElement> runs, string policy)
    {
        var pRuns = runs.Where(r => r.GetProperty("policy").GetString() == policy).ToList();
        var v = pRuns.Count(r => r.GetProperty("M-14_outcome").GetString() == "Victory");
        var d = pRuns.Count(r => r.GetProperty("M-14_outcome").GetString() == "Defeat");
        var s = pRuns.Count(r => r.GetProperty("M-14_outcome").GetString() == "Stalemate");
        return $"{v} / {d} / {s}";
    }

    private static string PolicyVDSResults(IEnumerable<BalanceSimulationResult> runs, string policy)
    {
        var pRuns = runs.Where(r => r.PolicyName == policy).ToList();
        var v = pRuns.Count(r => r.Outcome == BalanceSimulationOutcome.Victory);
        var d = pRuns.Count(r => r.Outcome == BalanceSimulationOutcome.Defeat);
        var s = pRuns.Count(r => r.Outcome == BalanceSimulationOutcome.Stalemate);
        return $"{v} / {d} / {s}";
    }

    /// <summary>
    /// Deep-dive into TASK-195 terminal card-cast reconciliation case:
    /// Kim Lôi Vương on seed 20261002 with LegacySkilled and Skilled policies.
    /// </summary>
    [Fact]
    public async Task Analyze_Task195_TerminalCardCast_AndFindCurrentCardTerminatedRuns()
    {
        var kimLoiVuong = BossDefinitions.All.First(b => b.BossId.Value == "boss-kim-loi-vuong");

        // 1. Check seed 20261002 with LegacySkilled (the exact test case of TASK-195)
        var legacyResult = await BalanceBaselineMatrix.RunAsync(kimLoiVuong, BalancePolicies.LegacySkilledName, 20261002UL);
        _output.WriteLine($"Kim Lôi Vương / legacy-skilled / 20261002: Outcome={legacyResult.Outcome}, Turns={legacyResult.Metrics.Turns}, Detail={legacyResult.OutcomeDetail}");
        _output.WriteLine($"Damage Total P={legacyResult.Metrics.PlayerDamageTotal}, B={legacyResult.Metrics.BossDamageTotal}");
        _output.WriteLine($"Cast count={legacyResult.Metrics.CastsTotal}, CastTurns=[{string.Join(", ", legacyResult.Metrics.CastTurns)}]");

        // 2. Check seed 20261002 with Skilled (Benchmark v2)
        var skilledResult = await BalanceBaselineMatrix.RunAsync(kimLoiVuong, BalancePolicies.SkilledName, 20261002UL);
        _output.WriteLine($"Kim Lôi Vương / skilled / 20261002: Outcome={skilledResult.Outcome}, Turns={skilledResult.Metrics.Turns}, Detail={skilledResult.OutcomeDetail}");
        _output.WriteLine($"Damage Total P={skilledResult.Metrics.PlayerDamageTotal}, B={skilledResult.Metrics.BossDamageTotal}");
        _output.WriteLine($"Cast count={skilledResult.Metrics.CastsTotal}, CastTurns=[{string.Join(", ", skilledResult.Metrics.CastTurns)}]");

        // 3. Search for any runs in the full 75 matrix that DO terminate via Card cast under current mechanics
        _output.WriteLine("=== ALL RUNS TERMINATING VIA CARD CAST UNDER TASK-299 ===");
        var cardTerminatedRuns = new List<string>();
        foreach (var (boss, policyName, seed) in BalanceBaselineMatrix.Cells())
        {
            var res = await BalanceBaselineMatrix.RunAsync(boss, policyName, seed);
            if (res.OutcomeDetail.StartsWith("Card cast resolved", StringComparison.Ordinal))
            {
                var entry = $"{res.BossId}/{res.PolicyName}/{res.Seed} (Turn {res.Metrics.Turns}, {res.Outcome})";
                cardTerminatedRuns.Add(entry);
                _output.WriteLine($"CARD TERMINATION: {entry}");
            }
        }
        _output.WriteLine($"Total card-terminated runs under Benchmark v2: {cardTerminatedRuns.Count}");
    }
}
