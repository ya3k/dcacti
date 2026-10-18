using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameServer.Application.Tests.Balance;
using Xunit;
using Xunit.Abstractions;

namespace GameServer.Application.Tests.Balance.Tests;

public sealed class CorrectedBenchmarkMatrixTests
{
    private readonly ITestOutputHelper _output;

    public CorrectedBenchmarkMatrixTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task RunCorrectedBenchmark75Matrix_AndCompareWithTask296Candidate()
    {
        // 1. Verify TASK-296 candidate exists and has expected hash
        var task296CandidatePath = Path.Combine(AppContext.BaseDirectory, "TASK-296-canonical-75-candidate.json");
        Assert.True(File.Exists(task296CandidatePath), $"Candidate artifact not found at {task296CandidatePath}");

        var candidateBytes = await File.ReadAllBytesAsync(task296CandidatePath);
        var candidateHash = Convert.ToHexString(SHA256.HashData(candidateBytes));
        Assert.Equal("A9E9ABBD9B342C9B8849E5CECB1580D3154915782F539F5A3547252E4EE4FC62", candidateHash);

        // Parse TASK-296 runs
        using var oldDoc = JsonDocument.Parse(candidateBytes);
        var oldRuns = oldDoc.RootElement.GetProperty("runs").EnumerateArray().ToList();
        Assert.Equal(75, oldRuns.Count);

        var oldOutcomes = new Dictionary<string, (string Outcome, int Turns)>(StringComparer.Ordinal);
        foreach (var r in oldRuns)
        {
            var b = r.GetProperty("bossId").GetString()!;
            var p = r.GetProperty("policy").GetString()!;
            var s = r.GetProperty("seed").GetUInt64();
            var o = r.GetProperty("M-14_outcome").GetString()!;
            var t = r.GetProperty("metrics").GetProperty("M-01_turns").GetInt32();
            oldOutcomes[$"{b}/{p}/{s}"] = (o, t);
        }

        // 2. Run the canonical 75 matrix with the corrected skilled policy (Benchmark v2)
        var cells = BalanceBaselineMatrix.Cells().ToArray();
        Assert.Equal(75, cells.Length);

        var newResults = new List<BalanceSimulationResult>(75);
        foreach (var (boss, policyName, seed) in cells)
        {
            newResults.Add(await BalanceBaselineMatrix.RunAsync(boss, policyName, seed));
        }

        Assert.Equal(75, newResults.Count);
        Assert.Empty(newResults.Where(r => r.Outcome == BalanceSimulationOutcome.InvalidSimulation));

        // 3. Save new benchmark results to an isolated path
        var outputDir = Path.Combine(AppContext.BaseDirectory, "artifacts");
        Directory.CreateDirectory(outputDir);
        var newArtifactPath = Path.Combine(outputDir, "TASK-298F-canonical-75-corrected-benchmark.json");

        var newJson = BalanceBaselineMatrix.SerializeEvidence(newResults);
        await File.WriteAllTextAsync(newArtifactPath, newJson);
        Assert.True(File.Exists(newArtifactPath));

        var newArtifactBytes = await File.ReadAllBytesAsync(newArtifactPath);
        var newArtifactHash = Convert.ToHexString(SHA256.HashData(newArtifactBytes));

        // 4. Summarize results
        var victories = newResults.Count(r => r.Outcome == BalanceSimulationOutcome.Victory);
        var defeats = newResults.Count(r => r.Outcome == BalanceSimulationOutcome.Defeat);
        var stalemates = newResults.Count(r => r.Outcome == BalanceSimulationOutcome.Stalemate);

        _output.WriteLine("=== BENCHMARK V2 (CORRECTED SKILLED POLICY) SUMMARY ===");
        _output.WriteLine($"Total Runs: {newResults.Count}");
        _output.WriteLine($"Victories: {victories}");
        _output.WriteLine($"Defeats: {defeats}");
        _output.WriteLine($"Stalemates: {stalemates}");
        _output.WriteLine($"Artifact Path: {newArtifactPath}");
        _output.WriteLine($"Artifact SHA-256: {newArtifactHash}");

        // Compare per policy
        foreach (var policy in BalanceBaselineMatrix.PolicyNames)
        {
            var pRuns = newResults.Where(r => r.PolicyName == policy).ToList();
            var v = pRuns.Count(r => r.Outcome == BalanceSimulationOutcome.Victory);
            var d = pRuns.Count(r => r.Outcome == BalanceSimulationOutcome.Defeat);
            var s = pRuns.Count(r => r.Outcome == BalanceSimulationOutcome.Stalemate);
            _output.WriteLine($"Policy {policy}: V={v}, D={d}, S={s}");
        }

        // Compare per boss
        foreach (var boss in BalanceBaselineMatrix.Bosses)
        {
            var bRuns = newResults.Where(r => r.BossId == boss.BossId.Value).ToList();
            var v = bRuns.Count(r => r.Outcome == BalanceSimulationOutcome.Victory);
            var d = bRuns.Count(r => r.Outcome == BalanceSimulationOutcome.Defeat);
            var s = bRuns.Count(r => r.Outcome == BalanceSimulationOutcome.Stalemate);
            _output.WriteLine($"Boss {boss.BossId.Value}: V={v}, D={d}, S={s}");
        }

        // Detailed diff against TASK-296 candidate (Benchmark v1)
        var shifted = 0;
        _output.WriteLine("=== RUN-BY-RUN DIFFERENCES AGAINST TASK-296 CANDIDATE (V1) ===");
        foreach (var r in newResults)
        {
            var key = $"{r.BossId}/{r.PolicyName}/{r.Seed}";
            var old = oldOutcomes[key];
            var newOutcome = r.Outcome.ToString();
            var newTurns = r.Metrics.Turns;

            if (old.Outcome != newOutcome || old.Turns != newTurns)
            {
                shifted++;
                _output.WriteLine($"SHIFT: {key} | Old: {old.Outcome} ({old.Turns}t) -> New: {newOutcome} ({newTurns}t) | Detail: {r.OutcomeDetail}");
            }
        }
        _output.WriteLine($"Total shifted runs (outcome or turns): {shifted}");
    }
}
