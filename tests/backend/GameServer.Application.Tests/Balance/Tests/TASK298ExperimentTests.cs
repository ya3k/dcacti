using GameServer.Domain.Bosses;
using Xunit;

namespace GameServer.Application.Tests.Balance.Tests;

/// <summary>
/// The <c>TASK-298</c> evidence tests: every controlled tuning experiment runs the
/// complete canonical 75-run cell set through the unchanged TASK-193 harness, and
/// the run records are checked for structural integrity.
///
/// <b>These tests measure; they judge nothing.</b> No test computes a combat
/// value, tunes a balance value, or produces a <c>BALANCED</c>/<c>UNBALANCED</c>/
/// <c>PASS</c>/<c>FAIL</c> verdict. Every assertion is either a structural
/// integrity property of the run records or the determinism contract
/// <c>MATCH3_RULES.md</c> §7 / <c>AGENTS.md</c> §11 already requires. The
/// acceptance targets of <c>TASK-298</c> §5 are <b>reported</b> in the artifact,
/// never asserted here — §5 states they are "proposed evaluation targets, not
/// permission to distort gameplay solely to satisfy percentages".
/// </summary>
public sealed class TASK298ExperimentTests
{
    /// <summary>The canonical matrix size: 5 Bosses × 3 policies × 5 seeds.</summary>
    private const int ExpectedRunCount = 75;

    /// <summary>
    /// The four technical outcomes <c>TASK-193</c> §5.3 defines. They are the only
    /// permitted outcome values — no balance verdict exists in this vocabulary.
    /// </summary>
    private static readonly string[] TechnicalOutcomes =
        ["Victory", "Defeat", "Stalemate", "InvalidSimulation"];

    /// <summary>
    /// Every experiment the task evaluates must run exactly the canonical cell
    /// set, in canonical order, and produce records of the documented shape.
    ///
    /// <b>The cell set is the comparison contract.</b> If an experiment ran a
    /// different Boss, policy, or seed than its TASK-296 counterpart, its outcome
    /// would not be comparable, and the whole task's evidence would be void
    /// (<c>TASK-193</c> H-07, <c>TASK-298</c> §4 "record all 75 outcomes").
    /// </summary>
    [Fact]
    public async Task TASK298_EveryExperiment_RunsTheCompleteCanonicalCellSet()
    {
        var expectedCells = BalanceBaselineMatrix.Cells().ToArray();

        Assert.Equal(ExpectedRunCount, expectedCells.Length);

        foreach (var experiment in TASK298ExperimentMatrix.All)
        {
            var results = await TASK298ExperimentRunner.RunAsync(experiment).ConfigureAwait(false);

            Assert.Equal(ExpectedRunCount, results.Count);

            for (var index = 0; index < expectedCells.Length; index++)
            {
                var (boss, policyName, seed) = expectedCells[index];
                var result = results[index];
                var cell = $"{experiment.Id}/{boss.BossId.Value}/{policyName}/{seed}";

                Assert.Equal(boss.BossId.Value, result.BossId);
                Assert.Equal(policyName, result.PolicyName);
                Assert.Equal(seed, result.Seed);
                Assert.Contains(result.Outcome.ToString(), TechnicalOutcomes);
                Assert.Equal(BalanceBaselineMatrix.MaxTurns, result.MaxTurns);

                // TASK-298 §6: no InvalidSimulation outcome may appear. The
                // harness reports an unrunnable configuration rather than running
                // it, so an InvalidSimulation here would mean an experiment delta
                // produced an illegal configuration.
                Assert.NotEqual(BalanceSimulationOutcome.InvalidSimulation, result.Outcome);

                AssertStructuralIntegrity(result, cell);
            }
        }
    }

    /// <summary>
    /// The zero-delta control reproduces the canonical matrix. This is the
    /// property that makes every later difference attributable to the experiment's
    /// named delta rather than to the TASK-298 harness.
    /// </summary>
    [Fact]
    public async Task TASK298_ControlExperiment_MatchesTheCanonicalMatrix()
    {
        var canonical = await RunCanonicalAsync().ConfigureAwait(false);
        var control = await TASK298ExperimentRunner
            .RunAsync(TASK298ExperimentMatrix.E0BaselineReproduction)
            .ConfigureAwait(false);

        Assert.Equal(canonical.Count, control.Count);

        for (var index = 0; index < canonical.Count; index++)
        {
            var cell = $"{control[index].BossId}/{control[index].PolicyName}/{control[index].Seed}";

            AssertIdentical(canonical[index], control[index], cell);
        }
    }

    /// <summary>
    /// <c>TASK-298</c> §4/§6 — determinism. Each experiment, re-run from scratch,
    /// reproduces every run bit-identically: outcome, the full M-01…M-15 metric
    /// set, and the terminal authoritative state.
    /// </summary>
    [Fact]
    public async Task TASK298_EveryExperiment_IsDeterministic()
    {
        foreach (var experiment in TASK298ExperimentMatrix.All)
        {
            var first = await TASK298ExperimentRunner.RunAsync(experiment).ConfigureAwait(false);
            var second = await TASK298ExperimentRunner.RunAsync(experiment).ConfigureAwait(false);

            Assert.Equal(first.Count, second.Count);

            for (var index = 0; index < first.Count; index++)
            {
                var cell = $"{experiment.Id}/{first[index].BossId}/{first[index].PolicyName}/{first[index].Seed}";

                AssertIdentical(first[index], second[index], cell);
            }
        }
    }

    /// <summary>
    /// <c>TASK-298</c> §6 — Power accounting, Turn-series, and terminal-state
    /// invariants hold for every experiment's every run.
    ///
    /// <b>The Power invariant is checked as a bound, not an identity.</b>
    /// <c>PetState.Power</c> is written by two documented stages — generation
    /// (step 12/13, <c>ResourceGenerator.ApplyPower</c>) and Card cast
    /// (<c>CardCastExecutor</c>) — and read down by a third (Boss Drain Power).
    /// Total generated Power and total spent Power are therefore <b>not</b>
    /// required to cancel; what the rules do require is that the value stays
    /// inside the documented 0–100 range (<c>GAME_RULES.md</c> §12) and that the
    /// M-07 accounting members are non-negative in the direction each is defined.
    /// </summary>
    [Fact]
    public async Task TASK298_EveryExperiment_HoldsTheCombatInvariants()
    {
        foreach (var experiment in TASK298ExperimentMatrix.All)
        {
            var results = await TASK298ExperimentRunner.RunAsync(experiment).ConfigureAwait(false);

            foreach (var result in results)
            {
                var cell = $"{experiment.Id}/{result.BossId}/{result.PolicyName}/{result.Seed}";
                var metrics = result.Metrics;

                // GAME_RULES.md §12 — Power's documented 0–100 range.
                Assert.InRange(metrics.PowerFinal, 0, 100);

                // A generated pool and a spent amount are both magnitudes.
                Assert.True(metrics.PowerGeneratedTotal >= 0, $"{cell}: PowerGeneratedTotal < 0");
                Assert.True(metrics.PowerSpentTotal >= 0, $"{cell}: PowerSpentTotal < 0");

                // M-03/M-04 are accumulated Final Damage, which §3 step 6 floors at 0.
                Assert.True(metrics.PlayerDamageTotal >= 0, $"{cell}: PlayerDamageTotal < 0");
                Assert.True(metrics.BossDamageTotal >= 0, $"{cell}: BossDamageTotal < 0");

                // COMBAT_RULES.md §4 item 1 — heal restores up to MaxHP; a Boss's
                // HP never exceeds MaxHP and never goes below 0.
                Assert.InRange(metrics.BossHpMin, 0, result.Boss.MaxHP);
                Assert.InRange(metrics.PlayerHpMin, 0, 1000);

                // ADR-026 D1 — the HP terminal condition is a 0, never below.
                Assert.True(result.FinalState!.BossState.HP >= 0);
                Assert.True(result.FinalState.PetState.HP >= 0);

                // A Victory leaves the Boss at 0 and the Pet alive; a Defeat the
                // reverse. Both are the pipeline's own terminal conditions
                // (GAME_RULES.md §1.4) read from the committed state.
                switch (result.Outcome)
                {
                    case BalanceSimulationOutcome.Victory:
                        Assert.Equal(0, result.FinalState.BossState.HP);
                        break;
                    case BalanceSimulationOutcome.Defeat:
                        Assert.Equal(0, result.FinalState.PetState.HP);
                        break;
                    default:
                        break;
                }
            }
        }
    }

    /// <summary>
    /// <c>TASK-298</c> §4/§8 — the raw experiment evidence is written to a
    /// task-specific artifact under <c>tasks/artifacts/</c>, and the artifact is a
    /// deterministic function of the results.
    ///
    /// <b>It writes only <see cref="TASK298ExperimentRunner.ArtifactFileName"/>.</b>
    /// The pinned <c>TASK-194</c> and <c>TASK-197</c> artifacts are never named,
    /// read for modification, or written by this test, and no candidate is
    /// promoted to <c>TASK-194</c> (<c>TASK-298</c> §6).
    /// </summary>
    [Fact]
    public async Task TASK298_RawEvidence_IsArchivedAsDeterministicJson()
    {
        var experiments = new List<(TASK298ExperimentDefinition, IReadOnlyList<BalanceSimulationResult>)>(
            TASK298ExperimentMatrix.All.Count);

        foreach (var experiment in TASK298ExperimentMatrix.All)
        {
            experiments.Add((experiment, await TASK298ExperimentRunner.RunAsync(experiment).ConfigureAwait(false)));
        }

        // The comparison baseline is the unchanged TASK-296 candidate's own cell
        // set, measured by this harness so the "before" column is computed the
        // same way as the "after" column. The candidate artifact itself is only
        // ever read as a file; it is never written.
        var task296 = await RunCanonicalAsync().ConfigureAwait(false);

        var json = TASK298ExperimentRunner.SerializeEvidence(experiments, task296);

        // The writer is pure: serializing the same results twice yields the same
        // bytes, so the artifact cannot drift between runs.
        Assert.Equal(json, TASK298ExperimentRunner.SerializeEvidence(experiments, task296));

        var path = TASK298ExperimentRunner.WriteArtifact(json);

        Assert.True(File.Exists(path), $"the TASK-298 evidence artifact was not written to {path}");
        Assert.Equal(TASK298ExperimentRunner.ArtifactFileName, Path.GetFileName(path));
        Assert.Equal(json, await File.ReadAllTextAsync(path).ConfigureAwait(false));

        // The artifact records the four technical outcomes and no verdict.
        foreach (var verdict in new[] { "BALANCED", "UNBALANCED", "\"PASS\"", "\"FAIL\"" })
        {
            Assert.DoesNotContain(verdict, json, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The canonical 75 runs, measured by this harness with no delta applied.
    /// </summary>
    private static async Task<IReadOnlyList<BalanceSimulationResult>> RunCanonicalAsync()
    {
        var cells = BalanceBaselineMatrix.Cells().Select(cell =>
            BalanceBaselineMatrix.RunAsync(cell.Boss, cell.PolicyName, cell.Seed));

        return await Task.WhenAll(cells).ConfigureAwait(false);
    }

    /// <summary>
    /// The structural integrity properties every run record must hold
    /// (<c>TASK-194</c> §14): complete metric set, a Victory or Defeat at a Turn
    /// the run actually took, a Stalemate exactly at the Turn bound, and the
    /// dimensional series invariant — one record per flushed Turn in every
    /// per-Turn series.
    /// </summary>
    private static void AssertStructuralIntegrity(BalanceSimulationResult result, string cell)
    {
        var metrics = result.Metrics;

        Assert.True(metrics.Turns >= 1, $"{cell}: a run resolved no Turn");
        Assert.Equal(metrics.Turns, metrics.DurationTurns);

        if (result.Outcome == BalanceSimulationOutcome.Stalemate)
        {
            // TASK-193 H-05/H-08: the bound is checked BEFORE proposing an action,
            // so a non-terminal run stops with exactly MaxTurns resolved Turns.
            Assert.Equal(result.MaxTurns, metrics.Turns);
        }
        else
        {
            Assert.True(metrics.Turns <= result.MaxTurns, $"{cell}: terminal after the Turn bound");
        }

        // TASK-195 §4.2 / finding D-194-4: the dimensional invariant. Every
        // per-Turn series carries one record per Turn the driver flushed.
        var perTurnRecords = metrics.Turns;

        Assert.Equal(perTurnRecords, metrics.PlayerDamagePerTurn.Count);
        Assert.Equal(perTurnRecords, metrics.BossDamagePerTurn.Count);
        Assert.Equal(perTurnRecords, metrics.BossHpPerTurn.Count);
        Assert.Equal(perTurnRecords, metrics.PlayerHpPerTurn.Count);
        Assert.Equal(perTurnRecords, metrics.ComboPerTurn.Count);
        Assert.Equal(perTurnRecords, metrics.ResourceTrace.Count);

        // TASK-194 §8 — the summed series and the reported total cannot disagree.
        Assert.Equal(metrics.PlayerDamageTotal, metrics.PlayerDamagePerTurn.Sum());
        Assert.Equal(metrics.BossDamageTotal, metrics.BossDamagePerTurn.Sum());

        // M-08 — a cast count is a count, and the per-card breakdown sums to it.
        Assert.Equal(metrics.CastsTotal, metrics.CastsByCard.Values.Sum());

        // M-12 — boss regeneration is a measured non-negative quantity.
        Assert.True(metrics.BossRegenerationTotal >= 0, $"{cell}: BossRegenerationTotal < 0");
    }

    /// <summary>
    /// Two runs of the same cell are identical: outcome, detail, every metric
    /// value and series, and the terminal authoritative state.
    /// </summary>
    private static void AssertIdentical(
        BalanceSimulationResult expected,
        BalanceSimulationResult actual,
        string cell)
    {
        Assert.Equal(expected.Outcome, actual.Outcome);
        Assert.Equal(expected.OutcomeDetail, actual.OutcomeDetail);

        var left = expected.Metrics;
        var right = actual.Metrics;

        Assert.Equal(left.Turns, right.Turns);
        Assert.Equal(left.DurationTurns, right.DurationTurns);
        Assert.Equal(left.PlayerDamageTotal, right.PlayerDamageTotal);
        Assert.Equal(left.BossDamageTotal, right.BossDamageTotal);
        Assert.Equal(left.BossHpMin, right.BossHpMin);
        Assert.Equal(left.PlayerHpMin, right.PlayerHpMin);
        Assert.Equal(left.PowerGeneratedTotal, right.PowerGeneratedTotal);
        Assert.Equal(left.PowerSpentTotal, right.PowerSpentTotal);
        Assert.Equal(left.PowerFinal, right.PowerFinal);
        Assert.Equal(left.CastsTotal, right.CastsTotal);
        Assert.Equal(left.RejectedCasts, right.RejectedCasts);
        Assert.Equal(left.RelicTriggersTotal, right.RelicTriggersTotal);
        Assert.Equal(left.ComboMax, right.ComboMax);
        Assert.Equal(left.ElementAdvantageCount, right.ElementAdvantageCount);
        Assert.Equal(left.ElementNeutralCount, right.ElementNeutralCount);
        Assert.Equal(left.ElementDisadvantageCount, right.ElementDisadvantageCount);
        Assert.Equal(left.BossRegenerationTotal, right.BossRegenerationTotal);
        Assert.Equal(left.BossRegenerationTurns, right.BossRegenerationTurns);
        Assert.Equal(left.BossCardCastCount, right.BossCardCastCount);
        Assert.Equal(left.BossPassiveTriggerCount, right.BossPassiveTriggerCount);
        Assert.Equal(left.BossEnrageTurn, right.BossEnrageTurn);
        Assert.Equal(left.RejectedSwaps, right.RejectedSwaps);

        Assert.Equal(left.PlayerDamagePerTurn, right.PlayerDamagePerTurn);
        Assert.Equal(left.BossDamagePerTurn, right.BossDamagePerTurn);
        Assert.Equal(left.BossHpPerTurn, right.BossHpPerTurn);
        Assert.Equal(left.PlayerHpPerTurn, right.PlayerHpPerTurn);
        Assert.Equal(left.ComboPerTurn, right.ComboPerTurn);
        Assert.Equal(left.CastTurns, right.CastTurns);
        Assert.Equal(left.CastsByCard, right.CastsByCard);

        Assert.Equal(left.ResourceTrace.Count, right.ResourceTrace.Count);

        for (var index = 0; index < left.ResourceTrace.Count; index++)
        {
            Assert.Equal(left.ResourceTrace[index], right.ResourceTrace[index]);
        }

        // The terminal authoritative state is part of the reproduction contract.
        Assert.Equal(expected.FinalState!.BossState.HP, actual.FinalState!.BossState.HP);
        Assert.Equal(expected.FinalState.BossState.Power, actual.FinalState.BossState.Power);
        Assert.Equal(expected.FinalState.BossState.SkillCooldown, actual.FinalState.BossState.SkillCooldown);
        Assert.Equal(expected.FinalState.PetState.HP, actual.FinalState.PetState.HP);
        Assert.Equal(expected.FinalState.PetState.Power, actual.FinalState.PetState.Power);
        Assert.Equal(expected.FinalState.BoardState.Cells, actual.FinalState.BoardState.Cells);
        Assert.Equal(expected.FinalState.RngState, actual.FinalState.RngState);
        Assert.Equal(expected.FinalState.MatchCount, actual.FinalState.MatchCount);
        _ = cell;
    }
}
