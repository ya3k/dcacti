using System.Text.Json;
using GameServer.Domain.Bosses;
using Xunit;

namespace GameServer.Application.Tests.Balance.Tests;

/// <summary>
/// The <c>TASK-194</c> execution evidence: the §5 baseline matrix is run through
/// the existing <c>TASK-193</c> harness, and the §14/§16 verification
/// requirements are asserted.
///
/// <b>These tests measure; they judge nothing.</b> No test computes a combat
/// value, tunes a balance value, or produces a <c>BALANCED</c>/<c>UNBALANCED</c>/
/// <c>PASS</c>/<c>FAIL</c> verdict. Every assertion is either a structural
/// integrity property of the run records (§14) or a property of the archived raw
/// data (§17).
/// </summary>
public sealed class BalanceBaselineMatrixTests
{
    /// <summary>The §5.1 matrix size: 5 Bosses × 3 policies × 5 seeds.</summary>
    private const int ExpectedRunCount = 75;

    /// <summary>
    /// The four technical outcomes <c>TASK-193</c> §5.3 defines. They are the
    /// only permitted outcome values — no balance verdict exists in this
    /// vocabulary.
    /// </summary>
    private static readonly string[] TechnicalOutcomes =
        ["Victory", "Defeat", "Stalemate", "InvalidSimulation"];

    /// <summary>
    /// Every metric member the artifact must carry for each run (<c>TASK-194</c>
    /// §8, M-01…M-15). A missing key means an omitted measurement, which fails
    /// §14's metric-completeness requirement.
    /// </summary>
    private static readonly string[] RequiredMetricKeys =
    [
        "M-01_turns",
        "M-02_durationTurns",
        "M-03_playerDamageTotal",
        "M-03_playerDamagePerTurn",
        "M-04_bossDamageTotal",
        "M-04_bossDamagePerTurn",
        "M-05_bossHpPerTurn",
        "M-05_bossHpMin",
        "M-06_playerHpPerTurn",
        "M-06_playerHpMin",
        "M-07_powerGeneratedTotal",
        "M-07_powerSpentTotal",
        "M-07_powerFinal",
        "M-08_castsByCard",
        "M-08_castsTotal",
        "M-08_castTurns",
        "M-08_rejectedCasts",
        "M-09_relicTriggersByRelic",
        "M-09_relicTriggersTotal",
        "M-10_comboPerTurn",
        "M-10_comboMax",
        "M-10_comboDistribution",
        "M-11_elementAdvantageCount",
        "M-11_elementNeutralCount",
        "M-11_elementDisadvantageCount",
        "M-12_bossRegenerationTotal",
        "M-12_bossRegenerationTurns",
        "M-13_bossSkillCastCount",
        "M-13_bossPassiveTriggerCount",
        "M-13_bossEnrageTurn",
        "M-15_resourceTrace",
    ];

    /// <summary>
    /// The matrix definition itself: the §5.1 Bosses, policies, and seeds, the
    /// §5.2 baseline configuration, and the 75-cell enumeration order.
    /// </summary>
    [Fact]
    public void TASK194_TheMatrixDefinition_IsTheApprovedBaseline()
    {
        // §5.1/§6.2 — the canonical seed set, in order.
        Assert.Equal(
            new[] { 20261001UL, 20261002UL, 20261003UL, 20261004UL, 20261005UL },
            BalanceBaselineMatrix.Seeds);

        // §5.1 — the three approved policies, in order.
        Assert.Equal(
            new[] { "passive", "average", "skilled" },
            BalanceBaselineMatrix.PolicyNames);

        // §5.1 — the five MVP content Bosses, in BOSS_RULES.md §6.1 order.
        Assert.Equal(
            new[]
            {
                "boss-hoa-long",
                "boss-thuy-ma",
                "boss-moc-yeu",
                "boss-son-thach-ve",
                "boss-kim-loi-vuong",
            },
            BalanceBaselineMatrix.Bosses.Select(boss => boss.BossId.Value));

        // 5 × 3 × 5 = 75, with every (boss, policy, seed) triple present exactly
        // once.
        var cells = BalanceBaselineMatrix.Cells().ToArray();
        Assert.Equal(ExpectedRunCount, cells.Length);
        Assert.Equal(
            ExpectedRunCount,
            cells.Select(cell => (cell.Boss.BossId.Value, cell.PolicyName, cell.Seed)).Distinct().Count());

        // §5.2 — the baseline configuration: the fixed owner, the unequipped
        // Relic snapshot, the 1000-Turn safety bound, and the authored Card set
        // with Power Charge still at its authored cost of 0.
        var configuration = BalanceBaselineMatrix.Configuration(
            BalanceBaselineMatrix.Bosses[0],
            BalancePolicies.AverageName,
            20261001UL);

        Assert.Equal("player-balance-sim", configuration.PlayerId.Value);
        Assert.Equal("petinstance-balance", configuration.Pet.PetId.Value);
        Assert.Equal(5, configuration.Pet.PassiveThreshold);
        Assert.DoesNotContain(
            configuration.CardDefinitions,
            card => card.CardDefinitionId == "card-power-charge" && card.PowerCost != 0);
        Assert.Equal(
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["card-heal"] = 20,
                ["card-shield"] = 20,
                ["card-power-charge"] = 0,
                ["card-iron-fang"] = 40,
            },
            configuration.CardDefinitions.ToDictionary(
                card => card.CardDefinitionId,
                card => card.PowerCost,
                StringComparer.Ordinal));
        Assert.Null(configuration.RelicDefinitions);
        Assert.Equal(1000, configuration.MaxTurns);
        Assert.Equal(BalancePolicies.AverageName, configuration.Policy.Name);
        Assert.Equal("boss-hoa-long", configuration.Boss.BossId.Value);
    }

    /// <summary>
    /// §14 execution integrity: all 75 runs complete, none reports
    /// <c>INVALID_SIMULATION</c>, and M-01…M-15 are populated as their own
    /// definitions require.
    /// </summary>
    [Fact]
    public async Task TASK194_AllSeventyFiveRuns_CompleteWithCompleteMetrics()
    {
        var results = await RunMatrixAsync();

        Assert.Equal(ExpectedRunCount, results.Count);

        // §14 — zero runs may report INVALID_SIMULATION. The failures are named
        // rather than merely counted, so a broken cell is identifiable.
        var invalid = results
            .Where(result => result.Outcome == BalanceSimulationOutcome.InvalidSimulation)
            .Select(result => $"{result.BossId}/{result.PolicyName}/{result.Seed}: {result.OutcomeDetail}")
            .ToArray();

        Assert.True(
            invalid.Length == 0,
            $"TASK-194 §14 requires zero INVALID_SIMULATION runs; observed {invalid.Length}: {string.Join(" | ", invalid)}");

        foreach (var result in results)
        {
            var metrics = result.Metrics;

            // Identifying half of the record (H-09).
            Assert.Equal(BalanceSimulationMode.Baseline, result.Mode);
            Assert.Equal(1000, result.MaxTurns);
            Assert.False(string.IsNullOrWhiteSpace(result.OutcomeDetail));
            Assert.NotNull(result.FinalState);
            Assert.Equal(4, result.EquippedCardIds.Count);
            Assert.Empty(result.EquippedRelicIds);
            Assert.Contains(result.Outcome.ToString(), TechnicalOutcomes);

            // M-01 / M-02 — a completed run resolved at least one Turn, and the
            // terminal state agrees with the recorded Turn count.
            Assert.True(metrics.Turns > 0, $"{result.BossId}/{result.PolicyName}/{result.Seed} resolved no Turn");
            Assert.Equal(metrics.Turns, metrics.DurationTurns);
            Assert.Equal(result.FinalState!.Turn, metrics.Turns);

            // M-03 / M-04 — the per-Turn series each carry one record per Turn the
            // driver flushed, and the totals are the sums of those series.
            var perTurnRecords = ExpectedPerTurnRecordCount(result);

            Assert.Equal(perTurnRecords, metrics.PlayerDamagePerTurn.Count);
            Assert.Equal(perTurnRecords, metrics.BossDamagePerTurn.Count);
            Assert.Equal(metrics.PlayerDamageTotal, metrics.PlayerDamagePerTurn.Sum());
            Assert.Equal(metrics.BossDamageTotal, metrics.BossDamagePerTurn.Sum());

            // M-05 / M-06 — HP series and their minima.
            Assert.Equal(perTurnRecords, metrics.BossHpPerTurn.Count);
            Assert.Equal(perTurnRecords, metrics.PlayerHpPerTurn.Count);
            Assert.Equal(metrics.BossHpPerTurn.Min(), metrics.BossHpMin);
            Assert.Equal(metrics.PlayerHpPerTurn.Min(), metrics.PlayerHpMin);
            Assert.InRange(metrics.BossHpMin, 0, result.Boss.MaxHP);
            Assert.InRange(metrics.PlayerHpMin, 0, result.FinalState.PetState.MaxHP);

            // M-07 — Power is clamped to the documented 0–100 range
            // (GAME_RULES.md §12), and spend never exceeds generation plus the
            // starting value of 0.
            Assert.InRange(metrics.PowerFinal, 0, 100);
            Assert.True(
                metrics.PowerSpentTotal <= metrics.PowerGeneratedTotal,
                $"{result.BossId}/{result.PolicyName}/{result.Seed} spent more Power than it generated");

            // M-08 — cast counts reconcile across their three reports. A cast that
            // ended the battle is recorded in the Turn the driver had opened, so
            // the cast-Turn range is the per-Turn record range.
            Assert.Equal(metrics.CastsTotal, metrics.CastsByCard.Values.Sum());
            Assert.Equal(metrics.CastsTotal, metrics.CastTurns.Count);
            Assert.All(metrics.CastTurns, turn => Assert.InRange(turn, 1, perTurnRecords));
            Assert.True(metrics.RejectedCasts >= 0);

            // M-09 — no Relic content is attached, so no Relic can have fired
            // (RELIC_RULES.md §2.3).
            Assert.Equal(0, metrics.RelicTriggersTotal);
            Assert.Empty(metrics.RelicTriggersByRelic);

            // M-10 — one Combo per flushed Turn, and the histogram covers them.
            Assert.Equal(perTurnRecords, metrics.ComboPerTurn.Count);
            Assert.Equal(metrics.ComboPerTurn.Max(), metrics.ComboMax);
            Assert.Equal(perTurnRecords, metrics.ComboDistribution.Values.Sum());

            // M-11 — every damage instance is classified into exactly one of the
            // three documented element factors, and at least one instance exists.
            Assert.True(
                metrics.ElementAdvantageCount + metrics.ElementNeutralCount + metrics.ElementDisadvantageCount > 0,
                $"{result.BossId}/{result.PolicyName}/{result.Seed} reported no damage instance");

            // M-12 — regeneration is zero for every Boss except Mộc Yêu, which is
            // the only Boss carrying a regeneration Passive
            // (BOSS_RULES.md §6.2.3).
            if (result.BossId != "boss-moc-yeu")
            {
                Assert.Equal(0, metrics.BossRegenerationTotal);
                Assert.Equal(0, metrics.BossRegenerationTurns);
            }

            // M-13 — an Enrage Turn, when recorded, lies inside the run.
            Assert.True(metrics.BossSkillCastCount >= 0);
            Assert.True(metrics.BossPassiveTriggerCount >= 0);

            if (metrics.BossEnrageTurn is { } enrageTurn)
            {
                Assert.InRange(enrageTurn, 1, metrics.Turns);
            }

            // M-15 — one checkpoint per flushed Turn, in Turn order.
            Assert.Equal(perTurnRecords, metrics.ResourceTrace.Count);
            Assert.Equal(1, metrics.ResourceTrace[0].Turn);
            Assert.Equal(perTurnRecords, metrics.ResourceTrace[^1].Turn);
        }
    }

    /// <summary>
    /// §13.1/§14 reproducibility: re-running the 75-cell matrix reproduces every
    /// run — outcome, full M-01…M-15 metric set, and terminal authoritative state
    /// — bit-identically.
    /// </summary>
    [Fact]
    public async Task TASK194_EveryRun_IsReproducedBitIdenticallyOnReRun()
    {
        var first = await RunMatrixAsync();
        var second = await RunMatrixAsync();

        Assert.Equal(first.Count, second.Count);

        for (var index = 0; index < first.Count; index++)
        {
            var cell = $"{first[index].BossId}/{first[index].PolicyName}/{first[index].Seed}";

            Assert.Equal(first[index].BossId, second[index].BossId);
            Assert.Equal(first[index].PolicyName, second[index].PolicyName);
            Assert.Equal(first[index].Seed, second[index].Seed);
            AssertIdentical(first[index], second[index], cell);
        }
    }

    /// <summary>
    /// §17 archival: the raw results are written as a structured JSON artifact,
    /// the artifact carries every M-01…M-15 member for all 75 runs, and it is a
    /// deterministic function of those results — no clock, no <c>Guid</c>, and
    /// no balance verdict.
    /// </summary>
    [Fact]
    public async Task TASK194_RawEvidence_IsArchivedAsDeterministicJson()
    {
        var results = await RunMatrixAsync();

        var json = BalanceBaselineMatrix.SerializeEvidence(results);

        // The writer is pure: serializing the same results twice yields the same
        // bytes, so the artifact cannot drift between runs.
        Assert.Equal(json, BalanceBaselineMatrix.SerializeEvidence(results));

        var path = BalanceBaselineMatrix.WriteArtifact(json);

        Assert.True(File.Exists(path), $"the raw-evidence artifact was not written to {path}");
        Assert.Equal(json, await File.ReadAllTextAsync(path));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("TASK-194", root.GetProperty("task").GetString());
        Assert.Equal("Baseline", root.GetProperty("mode").GetString());
        Assert.Equal(ExpectedRunCount, root.GetProperty("runCount").GetInt32());

        // The matrix definition is recorded, so the experiment is reproducible
        // from the artifact alone.
        var matrix = root.GetProperty("matrix");
        Assert.Equal(5, matrix.GetProperty("bosses").GetArrayLength());
        Assert.Equal(3, matrix.GetProperty("policies").GetArrayLength());
        Assert.Equal(5, matrix.GetProperty("seeds").GetArrayLength());
        Assert.Equal(1000, matrix.GetProperty("configuration").GetProperty("maxTurns").GetInt32());
        Assert.Equal(JsonValueKind.Null, matrix.GetProperty("configuration").GetProperty("equippedRelics").ValueKind);

        // All 75 runs, in matrix order, each with the complete metric set.
        var runs = root.GetProperty("runs");
        Assert.Equal(ExpectedRunCount, runs.GetArrayLength());

        var index = 0;

        foreach (var (boss, policyName, seed) in BalanceBaselineMatrix.Cells())
        {
            var run = runs[index];

            Assert.Equal(boss.BossId.Value, run.GetProperty("bossId").GetString());
            Assert.Equal(policyName, run.GetProperty("policy").GetString());
            Assert.Equal(seed, run.GetProperty("seed").GetUInt64());
            Assert.Contains(run.GetProperty("M-14_outcome").GetString()!, TechnicalOutcomes);

            var metrics = run.GetProperty("metrics");
            var metricKeys = metrics.EnumerateObject().Select(property => property.Name).ToArray();

            Assert.All(RequiredMetricKeys, key => Assert.Contains(key, metricKeys));

            // Per-Turn series each carry one record per Turn the driver flushed —
            // exactly equal to the authoritative Turn count (TASK-195 §5.2).
            var turns = metrics.GetProperty("M-01_turns").GetInt32();
            var perTurnRecords = turns;

            Assert.Equal(perTurnRecords, metrics.GetProperty("M-03_playerDamagePerTurn").GetArrayLength());
            Assert.Equal(perTurnRecords, metrics.GetProperty("M-04_bossDamagePerTurn").GetArrayLength());
            Assert.Equal(perTurnRecords, metrics.GetProperty("M-05_bossHpPerTurn").GetArrayLength());
            Assert.Equal(perTurnRecords, metrics.GetProperty("M-06_playerHpPerTurn").GetArrayLength());
            Assert.Equal(perTurnRecords, metrics.GetProperty("M-10_comboPerTurn").GetArrayLength());
            Assert.Equal(perTurnRecords, metrics.GetProperty("M-15_resourceTrace").GetArrayLength());

            index++;
        }

        // §15/§16 — the artifact records no balance verdict. The four technical
        // outcomes are the whole vocabulary.
        Assert.Equal(
            ExpectedRunCount,
            root.GetProperty("outcomeCounts").EnumerateObject().Sum(entry => entry.Value.GetInt32()));

        foreach (var verdict in new[] { "BALANCED", "UNBALANCED", "\"PASS\"", "\"FAIL\"" })
        {
            Assert.DoesNotContain(verdict, json, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Corrected measurement assertion (TASK-195 §4.1, finding D-194-3).
    ///
    /// <b>Observation.</b> In all non-healing runs, <c>M-03 PlayerDamageTotal</c> must
    /// cover 100% of the Boss's own HP loss over the run (<c>M-05</c>). With the
    /// TASK-195 correction in place, pending Card-cast damage is no longer erased
    /// when subsequent Swap proposals are rejected within the same Turn.
    /// </summary>
    [Fact]
    public async Task TASK195_RecordedPlayerDamage_RetainsCastDamageInTurnsWithRejectedSwaps()
    {
        var results = await RunMatrixAsync();

        var undercounted = new List<string>();
        var castFreeRuns = 0;

        foreach (var result in results)
        {
            // Skip Mộc Yêu because its regeneration restores Boss HP and therefore
            // net Boss HP loss is less than total damage dealt.
            if (result.BossId == "boss-moc-yeu")
            {
                continue;
            }

            var bossHpLoss = result.Boss.MaxHP - result.Metrics.BossHpPerTurn[^1];

            if (result.Metrics.CastsTotal == 0)
            {
                castFreeRuns++;
            }

            if (result.Metrics.PlayerDamageTotal < bossHpLoss)
            {
                undercounted.Add(
                    $"{result.BossId}/{result.PolicyName}/{result.Seed}: recorded {result.Metrics.PlayerDamageTotal} "
                    + $"of {bossHpLoss} HP lost");
            }
        }

        Assert.True(castFreeRuns > 0, "the matrix must contain cast-free (passive) runs");
        Assert.Empty(undercounted);
    }

    /// <summary>
    /// How many records the harness flushed into each per-Turn series for one
    /// run: strictly equal to <see cref="BalanceSimulationMetrics.Turns"/>
    /// without exception (<c>TASK-195</c> §5.2).
    /// </summary>
    private static int ExpectedPerTurnRecordCount(BalanceSimulationResult result) =>
        result.Metrics.Turns;

    /// <summary>
    /// Runs every §5 cell in order and returns the 75 results.
    /// </summary>
    private static async Task<List<BalanceSimulationResult>> RunMatrixAsync()    {
        var results = new List<BalanceSimulationResult>(ExpectedRunCount);

        foreach (var (boss, policyName, seed) in BalanceBaselineMatrix.Cells())
        {
            results.Add(await BalanceBaselineMatrix.RunAsync(boss, policyName, seed));
        }

        return results;
    }

    /// <summary>
    /// Asserts two results of the same cell are identical in every recorded
    /// metric and in the terminal authoritative state. Record equality cannot be
    /// used directly because the metric record carries collection members, whose
    /// default equality is reference equality.
    /// </summary>
    private static void AssertIdentical(
        BalanceSimulationResult first,
        BalanceSimulationResult second,
        string cell)
    {
        Assert.Equal(first.Outcome, second.Outcome);
        Assert.Equal(first.OutcomeDetail, second.OutcomeDetail);

        var a = first.Metrics;
        var b = second.Metrics;

        Assert.Equal(a.Turns, b.Turns);
        Assert.Equal(a.DurationTurns, b.DurationTurns);
        Assert.Equal(a.RejectedSwaps, b.RejectedSwaps);
        Assert.Equal(a.RejectedCasts, b.RejectedCasts);
        Assert.Equal(a.PlayerDamageTotal, b.PlayerDamageTotal);
        Assert.Equal(a.PlayerDamagePerTurn, b.PlayerDamagePerTurn);
        Assert.Equal(a.BossDamageTotal, b.BossDamageTotal);
        Assert.Equal(a.BossDamagePerTurn, b.BossDamagePerTurn);
        Assert.Equal(a.BossHpPerTurn, b.BossHpPerTurn);
        Assert.Equal(a.BossHpMin, b.BossHpMin);
        Assert.Equal(a.PlayerHpPerTurn, b.PlayerHpPerTurn);
        Assert.Equal(a.PlayerHpMin, b.PlayerHpMin);
        Assert.Equal(a.PowerGeneratedTotal, b.PowerGeneratedTotal);
        Assert.Equal(a.PowerSpentTotal, b.PowerSpentTotal);
        Assert.Equal(a.PowerFinal, b.PowerFinal);
        Assert.Equal(a.CastsByCard, b.CastsByCard);
        Assert.Equal(a.CastsTotal, b.CastsTotal);
        Assert.Equal(a.CastTurns, b.CastTurns);
        Assert.Equal(a.RelicTriggersByRelic, b.RelicTriggersByRelic);
        Assert.Equal(a.RelicTriggersTotal, b.RelicTriggersTotal);
        Assert.Equal(a.ComboPerTurn, b.ComboPerTurn);
        Assert.Equal(a.ComboMax, b.ComboMax);
        Assert.Equal(a.ComboDistribution, b.ComboDistribution);
        Assert.Equal(a.ElementAdvantageCount, b.ElementAdvantageCount);
        Assert.Equal(a.ElementNeutralCount, b.ElementNeutralCount);
        Assert.Equal(a.ElementDisadvantageCount, b.ElementDisadvantageCount);
        Assert.Equal(a.BossRegenerationTotal, b.BossRegenerationTotal);
        Assert.Equal(a.BossRegenerationTurns, b.BossRegenerationTurns);
        Assert.Equal(a.BossSkillCastCount, b.BossSkillCastCount);
        Assert.Equal(a.BossPassiveTriggerCount, b.BossPassiveTriggerCount);
        Assert.Equal(a.BossEnrageTurn, b.BossEnrageTurn);
        Assert.Equal(a.ResourceTrace, b.ResourceTrace);

        // The terminal authoritative state — the strongest form of "the same
        // run", including the PRNG state (GAME_STATE.md §2.6).
        Assert.Equal(first.FinalState!.Turn, second.FinalState!.Turn);
        Assert.Equal(first.FinalState.Sequence, second.FinalState.Sequence);
        Assert.Equal(first.FinalState.BossState.HP, second.FinalState.BossState.HP);
        Assert.Equal(first.FinalState.PetState.HP, second.FinalState.PetState.HP);
        Assert.Equal(first.FinalState.PetState.Power, second.FinalState.PetState.Power);
        Assert.Equal(first.FinalState.RngState, second.FinalState.RngState);

        Assert.False(string.IsNullOrWhiteSpace(cell));
    }
}
