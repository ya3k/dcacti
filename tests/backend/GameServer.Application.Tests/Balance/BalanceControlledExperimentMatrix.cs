using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using GameServer.Application.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Domain.Relics;

namespace GameServer.Application.Tests.Balance;

/// <summary>
/// Execution record for one cell in the TASK-197 controlled experiment matrix.
/// </summary>
internal sealed record ControlledExperimentRun
{
    public required string ExperimentId { get; init; }
    public required string Variant { get; init; }
    public required string Description { get; init; }
    public required BalanceSimulationResult Result { get; init; }
}

/// <summary>
/// The TASK-197 controlled balance experiment execution matrix (EXP-01 through EXP-05).
///
/// <b>This matrix performs evidence generation only.</b> It drives the production
/// <see cref="BattleStateService"/> pipeline through the <see cref="BalanceSimulator"/>
/// in <see cref="BalanceSimulationMode.ControlledComparison"/> mode.
/// </summary>
internal static class BalanceControlledExperimentMatrix
{
    public const string ArtifactFileName = "TASK-197-controlled-balance-experiments.json";
    public const string ArtifactPathVariable = "TASK197_ARTIFACT_PATH";

    private static readonly IReadOnlyList<ulong> Seeds = BalanceControlledExperimentDefinitions.Seeds;
    private static readonly PlayerId Owner = BalanceControlledExperimentDefinitions.Owner;
    private static readonly IReadOnlyList<BossDefinition> Bosses = BossDefinitions.All;

    // =========================================================================
    // EXP-01: Mộc Yêu Regeneration (Target: Q-8 / B-07)
    // =========================================================================
    // Variants:
    //   1. baseline_5pct_5matches: Authored 5% MaxHP every 5 matches
    //   2. threshold_8matches: 5% MaxHP every 8 matches
    // Note: 2% and 3% magnitude variants are BLOCKED by hardcoded constant 5 in
    // BattleStateService.cs line 1630.
    // Dimensions: 2 variants × 1 Boss (Mộc Yêu) × 2 policies (average, skilled) × 5 seeds = 20 runs
    // =========================================================================

    public static async Task<IReadOnlyList<ControlledExperimentRun>> RunExp01Async()
    {
        var runs = new List<ControlledExperimentRun>();
        var mocYeuAuthored = BalanceControlledExperimentDefinitions.MocYeuAuthored();
        var mocYeuThresh8 = BalanceControlledExperimentDefinitions.MocYeuThreshold8();

        var variants = new (string Variant, string Description, BossDefinition Boss)[]
        {
            ("baseline_5pct_5matches", "Authored regeneration: 5% MaxHP (250 HP) every 5 matches", mocYeuAuthored),
            ("threshold_8matches", "Threshold increase: 5% MaxHP (250 HP) every 8 matches", mocYeuThresh8),
        };

        var policies = new[] { BalancePolicies.AverageName, BalancePolicies.SkilledName };
        var cards = BalanceControlledExperimentDefinitions.BaselineCards();
        var equippedCards = new EquippedCardIdentity[]
        {
            new("card-heal"),
            new("card-shield"),
            new("card-power-charge"),
            new("card-iron-fang"),
        };

        foreach (var (variant, desc, boss) in variants)
        {
            foreach (var policyName in policies)
            {
                foreach (var seed in Seeds)
                {
                    var config = new BalanceSimulationConfiguration
                    {
                        Mode = BalanceSimulationMode.ControlledComparison,
                        Seed = seed,
                        PlayerId = Owner,
                        Pet = BalanceControlledExperimentDefinitions.PetXichLang(equippedCards),
                        Boss = boss,
                        CardDefinitions = cards,
                        RelicDefinitions = null,
                        Policy = BalancePolicies.ByName(policyName),
                    };

                    var result = await new BalanceSimulator(config, new InMemoryBattleStateRepository()).RunAsync().ConfigureAwait(false);
                    runs.Add(new ControlledExperimentRun
                    {
                        ExperimentId = "EXP-01",
                        Variant = variant,
                        Description = desc,
                        Result = result,
                    });
                }
            }
        }

        return runs;
    }

    // =========================================================================
    // EXP-02: Heal vs Shield Isolation & Ordering (Target: Q-5 / B-09)
    // =========================================================================
    // Variants:
    //   1. heal_only: [card-heal, card-power-charge, card-iron-fang]
    //   2. shield_only: [card-shield, card-power-charge, card-iron-fang]
    //   3. heal_then_shield: [card-heal, card-shield, card-power-charge, card-iron-fang] (Ordering A)
    //   4. shield_then_heal: [card-shield, card-heal, card-power-charge, card-iron-fang] (Ordering B)
    //   5. control_no_sustain: [card-power-charge, card-iron-fang]
    // Dimensions: 5 variants × 5 Bosses × 2 policies (average, skilled) × 5 seeds = 250 runs
    // =========================================================================

    public static async Task<IReadOnlyList<ControlledExperimentRun>> RunExp02Async()
    {
        var runs = new List<ControlledExperimentRun>();
        var cards = BalanceControlledExperimentDefinitions.BaselineCards();

        var variants = new (string Variant, string Description, EquippedCardIdentity[] Loadout)[]
        {
            (
                "heal_only",
                "Heal only (no Shield): [Heal, Power Charge, Iron Fang]",
                [new("card-heal"), new("card-power-charge"), new("card-iron-fang")]
            ),
            (
                "shield_only",
                "Shield only (no Heal): [Shield, Power Charge, Iron Fang]",
                [new("card-shield"), new("card-power-charge"), new("card-iron-fang")]
            ),
            (
                "heal_then_shield",
                "Heal + Shield Ordering A: [Heal, Shield, Power Charge, Iron Fang]",
                [new("card-heal"), new("card-shield"), new("card-power-charge"), new("card-iron-fang")]
            ),
            (
                "shield_then_heal",
                "Heal + Shield Ordering B: [Shield, Heal, Power Charge, Iron Fang]",
                [new("card-shield"), new("card-heal"), new("card-power-charge"), new("card-iron-fang")]
            ),
            (
                "control_no_sustain",
                "Control without sustain cards: [Power Charge, Iron Fang]",
                [new("card-power-charge"), new("card-iron-fang")]
            ),
        };

        var policies = new[] { BalancePolicies.AverageName, BalancePolicies.SkilledName };

        foreach (var (variant, desc, loadout) in variants)
        {
            foreach (var boss in Bosses)
            {
                foreach (var policyName in policies)
                {
                    foreach (var seed in Seeds)
                    {
                        var config = new BalanceSimulationConfiguration
                        {
                            Mode = BalanceSimulationMode.ControlledComparison,
                            Seed = seed,
                            PlayerId = Owner,
                            Pet = BalanceControlledExperimentDefinitions.PetXichLang(loadout),
                            Boss = boss,
                            CardDefinitions = cards,
                            RelicDefinitions = null,
                            Policy = BalancePolicies.ByName(policyName),
                        };

                        var result = await new BalanceSimulator(config, new InMemoryBattleStateRepository()).RunAsync().ConfigureAwait(false);
                        runs.Add(new ControlledExperimentRun
                        {
                            ExperimentId = "EXP-02",
                            Variant = variant,
                            Description = desc,
                            Result = result,
                        });
                    }
                }
            }
        }

        return runs;
    }

    // =========================================================================
    // EXP-03: Full Card Damage / Efficiency Sweep (Target: Q-9 / B-08)
    // =========================================================================
    // Variants: 5 canonical Pet Skill cards equipped with native Pets across all 5 Bosses
    //   1. inferno_xich_lang: Pet Xích Lang (Hỏa), card-inferno (Cost 100)
    //   2. tidal_barrier_huyen_quy: Pet Huyền Quy (Thủy), card-tidal-barrier (Cost 80)
    //   3. iron_fang_bach_ho: Pet Bạch Hổ (Kim), card-iron-fang (Cost 100)
    //   4. venomous_bloom_thanh_xa: Pet Thanh Xà (Mộc), card-venomous-bloom (Cost 80)
    //   5. earthshaker_son_hung: Pet Sơn Hùng (Thổ), card-earthshaker (Cost 100)
    // Policy: skilled (casts highest cost affordable card)
    // Dimensions: 5 variants × 5 Bosses × 1 policy (skilled) × 5 seeds = 125 runs
    // =========================================================================

    public static async Task<IReadOnlyList<ControlledExperimentRun>> RunExp03Async()
    {
        var runs = new List<ControlledExperimentRun>();
        var cards = BalanceControlledExperimentDefinitions.CanonicalEightCards();

        var variants = new (string Variant, string Description, Func<EquippedCardIdentity[], BattleStateService.PetConfiguration> PetFactory, string SkillCardId)[]
        {
            (
                "inferno_xich_lang",
                "Inferno (Cost 100, Fire damage 100 + Burn 50x2) on Xích Lang (Hỏa)",
                cards => BalanceControlledExperimentDefinitions.PetXichLang(cards),
                "card-inferno"
            ),
            (
                "tidal_barrier_huyen_quy",
                "Tidal Barrier (Cost 80, Heal 20% + Shield 20%) on Huyền Quy (Thủy)",
                cards => BalanceControlledExperimentDefinitions.PetHuyenQuy(cards),
                "card-tidal-barrier"
            ),
            (
                "iron_fang_bach_ho",
                "Iron Fang (Cost 100, Metal damage 120 + Crit 10pp) on Bạch Hổ (Kim)",
                cards => BalanceControlledExperimentDefinitions.PetBachHo(cards),
                "card-iron-fang"
            ),
            (
                "venomous_bloom_thanh_xa",
                "Venomous Bloom (Cost 80, Wood damage 80 + Burn 25x2) on Thanh Xà (Mộc)",
                cards => BalanceControlledExperimentDefinitions.PetThanhXa(cards),
                "card-venomous-bloom"
            ),
            (
                "earthshaker_son_hung",
                "Earthshaker (Cost 100, Earth damage 150) on Sơn Hùng (Thổ)",
                cards => BalanceControlledExperimentDefinitions.PetSonHung(cards),
                "card-earthshaker"
            ),
        };

        foreach (var (variant, desc, petFactory, skillCardId) in variants)
        {
            var loadout = new EquippedCardIdentity[]
            {
                new("card-heal"),
                new("card-shield"),
                new("card-power-charge"),
                new(skillCardId),
            };

            foreach (var boss in Bosses)
            {
                foreach (var seed in Seeds)
                {
                    var config = new BalanceSimulationConfiguration
                    {
                        Mode = BalanceSimulationMode.ControlledComparison,
                        Seed = seed,
                        PlayerId = Owner,
                        Pet = petFactory(loadout),
                        Boss = boss,
                        CardDefinitions = cards,
                        RelicDefinitions = null,
                        Policy = BalancePolicies.Skilled,
                    };

                    var result = await new BalanceSimulator(config, new InMemoryBattleStateRepository()).RunAsync().ConfigureAwait(false);
                    runs.Add(new ControlledExperimentRun
                    {
                        ExperimentId = "EXP-03",
                        Variant = variant,
                        Description = desc,
                        Result = result,
                    });
                }
            }
        }

        return runs;
    }

    // =========================================================================
    // EXP-04: Relic Ablation Matrix (Target: Q-10 / B-10)
    // =========================================================================
    // Variants: 11 loadouts (1 unequipped control + 10 individual canonical relics)
    // Dimensions: 11 variants × 5 Bosses × 2 policies (average, skilled) × 5 seeds = 550 runs
    // =========================================================================

    public static async Task<IReadOnlyList<ControlledExperimentRun>> RunExp04Async()
    {
        var runs = new List<ControlledExperimentRun>();
        var cards = BalanceControlledExperimentDefinitions.BaselineCards();
        var equippedCards = new EquippedCardIdentity[]
        {
            new("card-heal"),
            new("card-shield"),
            new("card-power-charge"),
            new("card-iron-fang"),
        };

        var variants = new List<(string Variant, string Description, RelicDefinition? Relic)>
        {
            ("unequipped_control", "Unequipped control (0 relics equipped)", null),
        };

        foreach (var relic in BalanceControlledExperimentDefinitions.CanonicalTenRelics)
        {
            variants.Add((
                relic.RelicDefinitionId.Replace("relic-", "relic_").Replace('-', '_'),
                $"{relic.Name} ({relic.Trigger}): {relic.RelicDefinitionId}",
                relic));
        }

        var policies = new[] { BalancePolicies.AverageName, BalancePolicies.SkilledName };

        foreach (var (variant, desc, relic) in variants)
        {
            var equippedRelics = relic is null
                ? null
                : new EquippedRelicIdentity[] { new(relic.RelicDefinitionId) };

            var relicDefinitions = relic is null
                ? null
                : new RelicDefinition[] { relic };

            foreach (var boss in Bosses)
            {
                foreach (var policyName in policies)
                {
                    foreach (var seed in Seeds)
                    {
                        var config = new BalanceSimulationConfiguration
                        {
                            Mode = BalanceSimulationMode.ControlledComparison,
                            Seed = seed,
                            PlayerId = Owner,
                            Pet = BalanceControlledExperimentDefinitions.PetXichLang(equippedCards, equippedRelics),
                            Boss = boss,
                            CardDefinitions = cards,
                            RelicDefinitions = relicDefinitions,
                            Policy = BalancePolicies.ByName(policyName),
                        };

                        var result = await new BalanceSimulator(config, new InMemoryBattleStateRepository()).RunAsync().ConfigureAwait(false);
                        runs.Add(new ControlledExperimentRun
                        {
                            ExperimentId = "EXP-04",
                            Variant = variant,
                            Description = desc,
                            Result = result,
                        });
                    }
                }
            }
        }

        return runs;
    }

    // =========================================================================
    // EXP-05: Power Charge Cost Sensitivity (Target: Q-2 / B-01)
    // =========================================================================
    // Variants: Cost 0 (Baseline), Cost 10, Cost 20
    // Dimensions: 3 variants × 5 Bosses × 2 policies (average, skilled) × 5 seeds = 150 runs
    // =========================================================================

    public static async Task<IReadOnlyList<ControlledExperimentRun>> RunExp05Async()
    {
        var runs = new List<ControlledExperimentRun>();
        var equippedCards = new EquippedCardIdentity[]
        {
            new("card-heal"),
            new("card-shield"),
            new("card-power-charge"),
            new("card-iron-fang"),
        };

        var variants = new (string Variant, string Description, int Cost)[]
        {
            ("cost_0_baseline", "Power Charge cost 0 (baseline authored)", 0),
            ("cost_10", "Power Charge cost 10", 10),
            ("cost_20", "Power Charge cost 20", 20),
        };

        var policies = new[] { BalancePolicies.AverageName, BalancePolicies.SkilledName };

        foreach (var (variant, desc, cost) in variants)
        {
            var cards = BalanceControlledExperimentDefinitions.BaselineCards(powerChargeCost: cost);

            foreach (var boss in Bosses)
            {
                foreach (var policyName in policies)
                {
                    foreach (var seed in Seeds)
                    {
                        var config = new BalanceSimulationConfiguration
                        {
                            Mode = BalanceSimulationMode.ControlledComparison,
                            Seed = seed,
                            PlayerId = Owner,
                            Pet = BalanceControlledExperimentDefinitions.PetXichLang(equippedCards),
                            Boss = boss,
                            CardDefinitions = cards,
                            RelicDefinitions = null,
                            Policy = BalancePolicies.ByName(policyName),
                        };

                        var result = await new BalanceSimulator(config, new InMemoryBattleStateRepository()).RunAsync().ConfigureAwait(false);
                        runs.Add(new ControlledExperimentRun
                        {
                            ExperimentId = "EXP-05",
                            Variant = variant,
                            Description = desc,
                            Result = result,
                        });
                    }
                }
            }
        }

        return runs;
    }

    // =========================================================================
    // Master Execution & Serialization
    // =========================================================================

    public static async Task<IReadOnlyList<ControlledExperimentRun>> RunAllExperimentsAsync()
    {
        var allRuns = new List<ControlledExperimentRun>(1095);

        allRuns.AddRange(await RunExp01Async().ConfigureAwait(false));
        allRuns.AddRange(await RunExp02Async().ConfigureAwait(false));
        allRuns.AddRange(await RunExp03Async().ConfigureAwait(false));
        allRuns.AddRange(await RunExp04Async().ConfigureAwait(false));
        allRuns.AddRange(await RunExp05Async().ConfigureAwait(false));

        return allRuns;
    }

    public static string SerializeEvidence(IReadOnlyList<ControlledExperimentRun> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);

        var exp01Runs = runs.Where(r => r.ExperimentId == "EXP-01").ToArray();
        var exp02Runs = runs.Where(r => r.ExperimentId == "EXP-02").ToArray();
        var exp03Runs = runs.Where(r => r.ExperimentId == "EXP-03").ToArray();
        var exp04Runs = runs.Where(r => r.ExperimentId == "EXP-04").ToArray();
        var exp05Runs = runs.Where(r => r.ExperimentId == "EXP-05").ToArray();

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["task"] = "TASK-197",
            ["title"] = "Controlled Balance Experiment Matrix Execution",
            ["contents"] = "raw simulation evidence for EXP-01 through EXP-05 controlled experiment matrices",
            ["harness"] = "tests/backend/GameServer.Application.Tests/Balance/BalanceSimulator.cs (TASK-193)",
            ["mode"] = BalanceSimulationMode.ControlledComparison.ToString(),
            ["verdicts"] = "none — the artifact records the four technical outcomes and no balance judgement",
            ["runtime"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["framework"] = RuntimeInformation.FrameworkDescription,
                ["osDescription"] = RuntimeInformation.OSDescription,
                ["processArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
            },
            ["experimentValidityAudit"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["EXP-01"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = "PARTIALLY_BLOCKED",
                    ["targetQuestion"] = "Q-8 / B-07",
                    ["executedVariants"] = new[] { "baseline_5pct_5matches", "threshold_8matches" },
                    ["blockedVariants"] = new[] { "2pct_MaxHP", "3pct_MaxHP" },
                    ["blockReason"] = "Hardcoded constant 5 in BattleStateService.cs line 1630 prevents altering regeneration magnitude without modifying production code (src/). In accordance with TASK-197 anti-faking directives, blocked variants are stopped rather than simulated via fake seams.",
                },
                ["EXP-02"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = "VALID",
                    ["targetQuestion"] = "Q-5 / B-09",
                    ["pipelineFaithfulness"] = "100% faithful — isolated loadouts and ordering tested through production CardCastExecutor and DamagePipeline.",
                },
                ["EXP-03"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = "VALID",
                    ["targetQuestion"] = "Q-9 / B-08",
                    ["pipelineFaithfulness"] = "100% faithful — canonical 8-card set and 5 native Pet Skill cards tested across all elemental matchups.",
                },
                ["EXP-04"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = "VALID",
                    ["targetQuestion"] = "Q-10 / B-10",
                    ["pipelineFaithfulness"] = "100% faithful — 10 individual canonical Relic definitions tested against unequipped control.",
                },
                ["EXP-05"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = "VALID",
                    ["targetQuestion"] = "Q-2 / B-01",
                    ["pipelineFaithfulness"] = "100% faithful — PowerCost sensitivity (0, 10, 20) tested through production card deduction pipeline.",
                },
            },
            ["totalRunCount"] = runs.Count,
            ["totalOutcomeCounts"] = TotalOutcomeCounts(runs),
            ["experiments"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["EXP-01"] = ExperimentRecord("EXP-01", "Mộc Yêu regeneration", "Q-8 / B-07", exp01Runs),
                ["EXP-02"] = ExperimentRecord("EXP-02", "Heal vs Shield isolation", "Q-5 / B-09", exp02Runs),
                ["EXP-03"] = ExperimentRecord("EXP-03", "Full card damage/efficiency sweep", "Q-9 / B-08", exp03Runs),
                ["EXP-04"] = ExperimentRecord("EXP-04", "Relic ablation", "Q-10 / B-10", exp04Runs),
                ["EXP-05"] = ExperimentRecord("EXP-05", "Power Charge cost sensitivity", "Q-2 / B-01", exp05Runs),
            },
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static string WriteArtifact(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var path = ArtifactPath();
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, json);
        return path;
    }

    public static string ArtifactPath()
    {
        var configured = Environment.GetEnvironmentVariable(ArtifactPathVariable);

        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(RepositoryRoot(), "tasks", "artifacts", ArtifactFileName)
            : Path.GetFullPath(configured);
    }

    public static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(directory.FullName, "src", "backend", "GameServer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return AppContext.BaseDirectory;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static Dictionary<string, int> TotalOutcomeCounts(IReadOnlyList<ControlledExperimentRun> runs) =>
        Enum.GetValues<BalanceSimulationOutcome>().ToDictionary(
            outcome => outcome.ToString(),
            outcome => runs.Count(r => r.Result.Outcome == outcome),
            StringComparer.Ordinal);

    private static Dictionary<string, object?> ExperimentRecord(
        string expId,
        string title,
        string target,
        IReadOnlyList<ControlledExperimentRun> runs) => new(StringComparer.Ordinal)
    {
        ["experimentId"] = expId,
        ["title"] = title,
        ["target"] = target,
        ["runCount"] = runs.Count,
        ["outcomeCounts"] = TotalOutcomeCounts(runs),
        ["runs"] = runs.Select(RunRecord).ToArray(),
    };

    private static Dictionary<string, object?> RunRecord(ControlledExperimentRun expRun)
    {
        var result = expRun.Result;
        var metrics = result.Metrics;

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["experimentId"] = expRun.ExperimentId,
            ["variant"] = expRun.Variant,
            ["variantDescription"] = expRun.Description,
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
                ["M-13_bossSkillCastCount"] = metrics.BossSkillCastCount,
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
        ["skillBaseDamage"] = boss.SkillBaseDamage,
        ["skillChargeRequirement"] = boss.SkillChargeRequirement,
        ["skillCooldownTurns"] = boss.SkillCooldownTurns,
    };
}
