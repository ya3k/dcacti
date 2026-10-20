using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using GameServer.Domain.Bosses;

namespace GameServer.Application.Tests.Balance;

/// <summary>
/// The <c>TASK-298</c> experiment runner and artifact writer.
///
/// <code>
/// TASK298ExperimentMatrix.Configuration  (per cell, per experiment)
///         ↓  BalanceSimulator — the unchanged TASK-193 harness
///         ↓  the production pipeline (BattleStateService and every Domain stage)
/// 75 result records per experiment
///         ↓
/// outcome counts, stalemate rate, turn distribution, per-Boss and per-policy
/// breakdowns, and the full M-01…M-15 metric set
///         ↓
/// tasks/artifacts/TASK-298-match-only-combat-economy-experiments.json
/// </code>
///
/// <b>It measures; it judges nothing.</b> Every number it reports is either a
/// count of the pipeline's own terminal outcomes or a value the pipeline's own
/// metrics already carry. It computes no damage, no HP, no match result, and no
/// resource value — the same contract <see cref="BalanceSimulator"/> holds.
///
/// <b>It never writes a pinned artifact.</b> The artifact path is
/// <see cref="ArtifactFileName"/>, a TASK-298-specific name under
/// <c>tasks/artifacts/</c>. <c>TASK-194-baseline-simulation-results.json</c>
/// (and <c>TASK-197-controlled-balance-experiments.json</c>) are never named by
/// this type and are never written, staged, or read for modification
/// (<c>TASK-298</c> §1 "Preserve", §6 "Do not promote any candidate to TASK-194").
/// </summary>
internal static class TASK298ExperimentRunner
{
    /// <summary>
    /// The raw-evidence artifact this task requires
    /// (<c>TASK-298</c> §8 items 2–3). It is a new, task-specific filename; no
    /// existing artifact is reused or overwritten.
    /// </summary>
    public const string ArtifactFileName = "TASK-298-match-only-combat-economy-experiments.json";

    /// <summary>
    /// An environment variable that redirects the artifact write — the same
    /// escape hatch the TASK-194 writer provides, so a caller can divert the
    /// write away from the repository when it only wants the measurements.
    /// </summary>
    public const string ArtifactPathVariable = "TASK298_ARTIFACT_PATH";

    /// <summary>
    /// The exact TASK-296 candidate every experiment is compared against, and
    /// the committed container the artifact records it in.
    /// </summary>
    public const string Task296CandidateSHA256 =
        "A9E9ABBD9B342C9B8849E5CECB1580D3154915782F539F5A3547252E4EE4FC62";

    /// <summary>
    /// Runs one experiment across the complete canonical 75-run cell set, in
    /// <see cref="BalanceBaselineMatrix.Cells"/> order.
    ///
    /// <b>The cell set is the contract.</b> Every experiment runs exactly the
    /// cells <see cref="BalanceBaselineMatrix"/> enumerates — the same Bosses, the
    /// same three policies, the same five seeds, in the same order — so run
    /// <c>i</c> of one experiment is directly comparable to run <c>i</c> of
    /// another and to run <c>i</c> of the TASK-296 candidate.
    /// </summary>
    public static async Task<IReadOnlyList<BalanceSimulationResult>> RunAsync(
        TASK298ExperimentDefinition experiment)
    {
        ArgumentNullException.ThrowIfNull(experiment);

        var results = new List<BalanceSimulationResult>(75);

        foreach (var (boss, policyName, seed) in BalanceBaselineMatrix.Cells())
        {
            results.Add(await TASK298ExperimentMatrix
                .RunAsync(experiment, boss, policyName, seed)
                .ConfigureAwait(false));
        }

        return results;
    }

    /// <summary>
    /// Runs every experiment of <see cref="TASK298ExperimentMatrix.All"/>.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, IReadOnlyList<BalanceSimulationResult>>> RunAllAsync()
    {
        var results = new Dictionary<string, IReadOnlyList<BalanceSimulationResult>>(StringComparer.Ordinal);

        foreach (var experiment in TASK298ExperimentMatrix.All)
        {
            results[experiment.Id] = await RunAsync(experiment).ConfigureAwait(false);
        }

        return results;
    }

    /// <summary>
    /// Serializes one experiment's 75 runs plus the comparison summary into the
    /// raw-evidence artifact.
    ///
    /// <b>It is a pure function of its arguments.</b> No clock, no <c>Guid</c>,
    /// and no machine-random value is written, so the same results serialize to
    /// the same bytes and the artifact is reproducible evidence rather than a
    /// snapshot that drifts between runs (<c>AGENTS.md</c> §11).
    /// </summary>
    public static string SerializeEvidence(
        IReadOnlyList<(TASK298ExperimentDefinition Experiment, IReadOnlyList<BalanceSimulationResult> Results)> experiments,
        IReadOnlyList<BalanceSimulationResult> task296Candidate)
    {
        ArgumentNullException.ThrowIfNull(experiments);
        ArgumentNullException.ThrowIfNull(task296Candidate);

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["task"] = "TASK-298",
            ["title"] = "Match-Only Combat Economy and Balance Tuning",
            ["contents"] = "controlled tuning experiments over the canonical TASK-194 §5 "
                + "75-run cell set, under the match-only gameplay rules (ADR-026, ADR-028, "
                + "TASK-290..TASK-293)",
            ["harness"] = "tests/backend/GameServer.Application.Tests/Balance/BalanceSimulator.cs (TASK-193), "
                + "driven by TASK298ExperimentMatrix / TASK298ExperimentRunner (TASK-298)",
            ["mode"] = BalanceSimulationMode.Baseline.ToString(),
            ["verdicts"] = "none — the artifact records the four technical outcomes and the "
                + "reported metrics, and no balance judgement",
            ["comparison"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["baselineArtifact"] = "tests/backend/GameServer.Application.Tests/bin/Debug/net10.0/"
                    + "TASK-296-canonical-75-candidate.json",
                ["baselineDescription"] = "the unchanged TASK-296 re-baseline candidate for the "
                    + "match-only gameplay rules — the historical comparison artifact",
                ["baselineSHA256"] = Task296CandidateSHA256,
                ["note"] = "the TASK-296 candidate and the pinned TASK-194 / TASK-197 artifacts are "
                    + "read-only inputs to this task and were not modified",
            },
            ["runtime"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["framework"] = RuntimeInformation.FrameworkDescription,
                ["osDescription"] = RuntimeInformation.OSDescription,
                ["processArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
            },
            ["matrix"] = MatrixRecord(),
            ["experiments"] = experiments.Select(ExperimentRecord).ToArray(),
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    /// <summary>
    /// Writes the artifact to <see cref="ArtifactPath"/>, creating the directory
    /// when it does not exist yet.
    /// </summary>
    public static string WriteArtifact(string json, string? destinationPath = null)
    {
        ArgumentNullException.ThrowIfNull(json);

        var path = destinationPath ?? ArtifactPath();
        ProtectedArtifactGuard.EnsureNotProtected(path);
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, json);

        return path;
    }

    /// <summary>
    /// The canonical, pinned historical artifact location in the repository.
    /// </summary>
    public static string CanonicalArtifactPath() =>
        Path.Combine(BalanceBaselineMatrix.RepositoryRoot(), "tasks", "artifacts", ArtifactFileName);

    /// <summary>
    /// The artifact's output location: an isolated test output directory under
    /// <see cref="AppContext.BaseDirectory"/> by default, or the path named by
    /// <see cref="ArtifactPathVariable"/> when a caller redirects the write.
    /// </summary>
    public static string ArtifactPath()
    {
        var configured = Environment.GetEnvironmentVariable(ArtifactPathVariable);

        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(AppContext.BaseDirectory, "artifacts", ArtifactFileName)
            : Path.GetFullPath(configured);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// The matrix definition, recorded so a reader can reproduce the whole
    /// experiment from the artifact alone (<c>TASK-193</c> H-09). It restates the
    /// canonical cell set and the baseline configuration the deltas start from.
    /// </summary>
    private static Dictionary<string, object?> MatrixRecord() => new(StringComparer.Ordinal)
    {
        ["bosses"] = BalanceBaselineMatrix.Bosses.Select(BossRecord).ToArray(),
        ["policies"] = BalanceBaselineMatrix.PolicyNames.ToArray(),
        ["seeds"] = BalanceBaselineMatrix.Seeds.ToArray(),
        ["dimensions"] = $"{BalanceBaselineMatrix.Bosses.Count} bosses x "
            + $"{BalanceBaselineMatrix.PolicyNames.Count} policies x "
            + $"{BalanceBaselineMatrix.Seeds.Count} seeds",
        ["cells"] = 75,
        ["configuration"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["playerId"] = BalanceBaselineMatrix.Owner.Value,
            ["petId"] = "petinstance-balance",
            ["petElement"] = "Hoa",
            ["petPassiveId"] = "passive-xich-lang",
            ["petPassiveThreshold"] = 5,
            ["baseStatSource"] = "COMBAT_RULES.md §1.1 battle defaults "
                + "(no stat input passes a Level/Star curve)",
            ["equippedCardIds"] = BalanceBaselineMatrix.EquippedCards().Select(card => card.Value).ToArray(),
            ["baselineCardPowerCosts"] = BalanceBaselineMatrix.Cards.ToDictionary(
                card => card.CardDefinitionId,
                card => card.PowerCost,
                StringComparer.Ordinal),
            ["baselineCardEffects"] = BalanceBaselineMatrix.Cards.ToDictionary(
                card => card.CardDefinitionId,
                CardEffectRecord,
                StringComparer.Ordinal),
            ["equippedRelics"] = null,
            ["equippedRelicsNote"] = "RELIC_RULES.md §2.3 — no Relic content attached; "
                + "every run, baseline and experiment alike, is unequipped",
            ["maxTurns"] = BalanceBaselineMatrix.MaxTurns,
        },
    };

    private static string CardEffectRecord(Domain.Cards.CardDefinition card)
    {
        var parts = new List<string>(card.EffectDefinition.Count);

        for (var index = 0; index < card.EffectDefinition.Count; index++)
        {
            var effect = card.EffectDefinition[index];
            parts.Add($"{effect.EffectType}/{effect.ValueType}/{(effect.Value is { } value ? value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "null")}");
        }

        return string.Join(";", parts);
    }

    private static Dictionary<string, object?> BossRecord(BossDefinition boss) => new(StringComparer.Ordinal)
    {
        ["bossId"] = boss.BossId.Value,
        ["bossDefinitionId"] = boss.BossDefinitionId,
        ["element"] = boss.Element.ToString(),
        ["maxHP"] = boss.MaxHP,
        ["atk"] = boss.ATK,
        ["def"] = boss.DEF,
        ["enrageThreshold"] = boss.EnrageThreshold,
        ["passiveId"] = boss.PassiveId.Value,
        ["passiveThreshold"] = boss.PassiveDefinition.Threshold,
        ["passiveResetBehavior"] = boss.PassiveDefinition.ResetBehavior,
        ["skillId"] = boss.SkillId,
        ["skillCooldownTurns"] = boss.SkillCooldownTurns,
    };

    /// <summary>
    /// One experiment's record: its stated hypothesis, its exact parameter deltas,
    /// its 75-run outcome summary, and its full per-run records.
    /// </summary>
    private static Dictionary<string, object?> ExperimentRecord(
        (TASK298ExperimentDefinition Experiment, IReadOnlyList<BalanceSimulationResult> Results) experiment)
    {
        var definition = experiment.Experiment;
        var results = experiment.Results;

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["experimentId"] = definition.Id,
            ["parameterGroup"] = definition.ParameterGroup,
            ["hypothesis"] = definition.Hypothesis,
            ["intendedPlayerFacingEffect"] = definition.IntendedPlayerFacingEffect,
            ["parameterDeltas"] = Deltas(definition),
            ["runCount"] = results.Count,
            ["outcomeCounts"] = OutcomeCounts(results),
            ["stalemateRate"] = StalemateRate(results),
            ["policySummary"] = GroupSummary(results, result => result.PolicyName),
            ["bossSummary"] = GroupSummary(results, result => result.BossId),
            ["bossPolicySummary"] = GroupSummary(
                results,
                result => $"{result.BossId}/{result.PolicyName}"),
            ["turnStatistics"] = TurnStatistics(results),
            ["runs"] = results.Select(RunRecord).ToArray(),
        };
    }

    /// <summary>
    /// The experiment's exact parameter deltas, stated as <c>from → to</c> with
    /// the Canonical baseline value resolved, so the artifact is self-describing
    /// and a reviewer does not have to diff code to learn what moved.
    /// </summary>
    private static Dictionary<string, object?> Deltas(TASK298ExperimentDefinition definition)
    {
        // Resolve each "from" value from the canonical inputs the deltas start
        // from, rather than restating it: the artifact then cannot drift from the
        // baseline the run actually used.
        var healCard = BalanceBaselineMatrix.Cards
            .Single(card => card.CardDefinitionId == "card-heal");
        var powerCard = BalanceBaselineMatrix.Cards
            .Single(card => card.CardDefinitionId == "card-power-charge");
        var mocYeu = BalanceBaselineMatrix.Bosses
            .Single(boss => boss.BossId.Value == "boss-moc-yeu");

        var deltas = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (definition.CardHealPercentMaxHp is { } healPercent)
        {
            deltas["card-heal.effect.heal.percentMaxHp"] = $"{healCard.EffectDefinition[0].Value} → {healPercent}";
        }

        if (definition.CardHealPowerCost is { } healCost)
        {
            deltas["card-heal.powerCost"] = $"{healCard.PowerCost} → {healCost}";
        }

        if (definition.CardPowerChargePowerCost is { } chargeCost)
        {
            deltas["card-power-charge.powerCost"] = $"{powerCard.PowerCost} → {chargeCost}";
        }

        if (definition.CardPowerChargeGrant is { } grant)
        {
            deltas["card-power-charge.effect.power.flat"] = $"{powerCard.EffectDefinition[0].Value} → {grant}";
        }

        // The DEF and MaxHP deltas are scoped to the Bosses whose §6.1 row
        // actually authors the value being moved, so the recorded delta names
        // exactly the Bosses that change — and states the untouched ones
        // explicitly, so a reader can see the scoping was deliberate rather than
        // an omission.
        if (definition.DefensiveBossDefense is { } defense)
        {
            deltas["boss.def (the §6.1 shared DEF = 50 row)"] = ScopedBossDelta(
                boss => boss.DEF,
                boss => boss.DEF > 0,
                defense);
        }

        if (definition.SharedRowBossMaxHp is { } maxHp)
        {
            deltas["boss.maxHP (the §6.1 shared 5000 row)"] = ScopedBossDelta(
                boss => boss.MaxHP,
                boss => boss.MaxHP == BossRules.BaseMaxHP,
                maxHp);
        }

        if (definition.MocYeuPassiveThreshold is { } regenThreshold)
        {
            deltas["boss-moc-yeu.passive.threshold (boss-moc-yeu-regen cadence in Player Matches)"] =
                $"{mocYeu.PassiveDefinition.Threshold} → {regenThreshold}";
        }

        if (deltas.Count == 0)
        {
            deltas["none"] = "control run — every canonical input is used unchanged";
        }

        return deltas;
    }

    /// <summary>
    /// Renders one Boss-scoped delta: <c>from → to</c> for every Boss the delta
    /// applies to, and <c>unchanged</c> for the ones it does not.
    /// </summary>
    private static string ScopedBossDelta(
        Func<BossDefinition, int> value,
        Func<BossDefinition, bool> applies,
        int target) =>
        string.Join(
            " | ",
            BalanceBaselineMatrix.Bosses.Select(boss =>
            {
                var from = value(boss);

                return applies(boss)
                    ? $"{boss.BossId.Value}: {from} → {target}"
                    : $"{boss.BossId.Value}: {from} (unchanged)";
            }));

    private static Dictionary<string, int> OutcomeCounts(IReadOnlyList<BalanceSimulationResult> results) =>
        Enum.GetValues<BalanceSimulationOutcome>().ToDictionary(
            outcome => outcome.ToString(),
            outcome => results.Count(result => result.Outcome == outcome),
            StringComparer.Ordinal);

    /// <summary>
    /// The reported stalemate rate, as a decimal fraction of the run count, plus
    /// the numerator and denominator so no reader has to invert the rounding.
    /// </summary>
    private static Dictionary<string, object?> StalemateRate(IReadOnlyList<BalanceSimulationResult> results)
    {
        var stalemates = results.Count(result => result.Outcome == BalanceSimulationOutcome.Stalemate);

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["stalemates"] = stalemates,
            ["runs"] = results.Count,
            ["rate"] = results.Count == 0 ? 0d : (double)stalemates / results.Count,
        };
    }

    /// <summary>
    /// Per-group outcome counts and Turn statistics. It counts and summarizes; it
    /// classifies no group as balanced or unbalanced.
    /// </summary>
    private static Dictionary<string, object?> GroupSummary(
        IReadOnlyList<BalanceSimulationResult> results,
        Func<BalanceSimulationResult, string> key)
    {
        var groups = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var group in results.GroupBy(key, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var groupResults = group.ToArray();

            groups[group.Key] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["runs"] = groupResults.Length,
                ["outcomeCounts"] = OutcomeCounts(groupResults),
                ["turnStatistics"] = TurnStatistics(groupResults),
            };
        }

        return groups;
    }

    /// <summary>
    /// Turn-count descriptive statistics over the run set. It reports the
    /// distribution's shape and never applies a pass/fail threshold to it
    /// (<c>TASK-193</c> Q-1 = QUALITATIVE; <c>TASK-298</c> §5 treats the duration
    /// target as a proposal, not an acceptance gate).
    /// </summary>
    private static Dictionary<string, object?> TurnStatistics(IReadOnlyList<BalanceSimulationResult> results)
    {
        if (results.Count == 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["runs"] = 0 };
        }

        var turns = results.Select(result => result.Metrics.Turns).OrderBy(value => value).ToArray();

        // Nearest-rank percentiles over the sample, so every reported value is an
        // observed Turn count rather than an interpolation.
        static int Percentile(int[] ordered, double fraction)
        {
            var rank = (int)Math.Ceiling(fraction * ordered.Length);
            return ordered[Math.Clamp(rank, 1, ordered.Length) - 1];
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["min"] = turns[0],
            ["p25"] = Percentile(turns, 0.25),
            ["median"] = Percentile(turns, 0.50),
            ["p75"] = Percentile(turns, 0.75),
            ["max"] = turns[^1],
            ["mean"] = Math.Round(turns.Average(), 2),
            ["atMaxTurns"] = results.Count(result => result.Metrics.Turns >= BalanceBaselineMatrix.MaxTurns),
            ["under50Turns"] = turns.Count(value => value < 50),
            ["under100Turns"] = turns.Count(value => value < 100),
            ["atLeast200Turns"] = turns.Count(value => value >= 200),
        };
    }

    /// <summary>
    /// One run's record: the canonical cell identity, the terminal outcome, and
    /// the complete M-01…M-15 metric set. The metric names carry their
    /// <c>TASK-194</c> §8 metric number so a reader can map a value back to its
    /// definition without consulting code.
    /// </summary>
    private static Dictionary<string, object?> RunRecord(BalanceSimulationResult result)
    {
        var metrics = result.Metrics;

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["bossId"] = result.BossId,
            ["policy"] = result.PolicyName,
            ["seed"] = result.Seed,
            ["mode"] = result.Mode.ToString(),
            ["maxTurns"] = result.MaxTurns,
            ["playerId"] = result.PlayerId.Value,
            ["petId"] = result.PetId,
            ["equippedCardIds"] = result.EquippedCardIds.ToArray(),
            ["equippedRelicIds"] = result.EquippedRelicIds.ToArray(),
            ["bossDefinition"] = BossRecord(result.Boss),
            ["M-14_outcome"] = result.Outcome.ToString(),
            ["M-14_outcomeDetail"] = result.OutcomeDetail,
            ["metrics"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["M-01_turns"] = metrics.Turns,
                ["M-02_durationTurns"] = metrics.DurationTurns,
                ["M-03_playerDamageTotal"] = metrics.PlayerDamageTotal,
                ["M-03_playerDamagePerTurn"] = metrics.PlayerDamagePerTurn.ToArray(),
                ["M-04_bossDamageTotal"] = metrics.BossDamageTotal,
                ["M-04_bossDamagePerTurn"] = metrics.BossDamagePerTurn.ToArray(),
                ["M-05_bossHpPerTurn"] = metrics.BossHpPerTurn.ToArray(),
                ["M-05_bossHpMin"] = metrics.BossHpMin,
                ["M-06_playerHpPerTurn"] = metrics.PlayerHpPerTurn.ToArray(),
                ["M-06_playerHpMin"] = metrics.PlayerHpMin,
                ["M-07_powerGeneratedTotal"] = metrics.PowerGeneratedTotal,
                ["M-07_powerSpentTotal"] = metrics.PowerSpentTotal,
                ["M-07_powerFinal"] = metrics.PowerFinal,
                ["M-08_castsByCard"] = new Dictionary<string, int>(metrics.CastsByCard, StringComparer.Ordinal),
                ["M-08_castsTotal"] = metrics.CastsTotal,
                ["M-08_castTurns"] = metrics.CastTurns.ToArray(),
                ["M-08_rejectedCasts"] = metrics.RejectedCasts,
                ["M-09_relicTriggersByRelic"] = new Dictionary<string, int>(
                    metrics.RelicTriggersByRelic,
                    StringComparer.Ordinal),
                ["M-09_relicTriggersTotal"] = metrics.RelicTriggersTotal,
                ["M-10_comboPerTurn"] = metrics.ComboPerTurn.ToArray(),
                ["M-10_comboMax"] = metrics.ComboMax,
                ["M-10_comboDistribution"] = metrics.ComboDistribution.ToDictionary(
                    entry => entry.Key.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    entry => entry.Value,
                    StringComparer.Ordinal),
                ["M-11_elementAdvantageCount"] = metrics.ElementAdvantageCount,
                ["M-11_elementNeutralCount"] = metrics.ElementNeutralCount,
                ["M-11_elementDisadvantageCount"] = metrics.ElementDisadvantageCount,
                ["M-12_bossRegenerationTotal"] = metrics.BossRegenerationTotal,
                ["M-12_bossRegenerationTurns"] = metrics.BossRegenerationTurns,
                ["M-13_bossCardCastCount"] = metrics.BossCardCastCount,
                ["M-13_bossPassiveTriggerCount"] = metrics.BossPassiveTriggerCount,
                ["M-13_bossEnrageTurn"] = metrics.BossEnrageTurn,
                ["M-15_resourceTrace"] = metrics.ResourceTrace.Select(checkpoint => new Dictionary<string, object?>(
                    StringComparer.Ordinal)
                {
                    ["turn"] = checkpoint.Turn,
                    ["playerHp"] = checkpoint.PlayerHp,
                    ["playerMaxHp"] = checkpoint.PlayerMaxHp,
                    ["playerPower"] = checkpoint.PlayerPower,
                    ["bossHp"] = checkpoint.BossHp,
                    ["playerStatusCount"] = checkpoint.PlayerStatusCount,
                    ["bossStatusCount"] = checkpoint.BossStatusCount,
                }).ToArray(),
                ["rejectedSwaps"] = metrics.RejectedSwaps,
            },
        };
    }
}
