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

namespace GameServer.Application.Tests.Balance;

/// <summary>
/// The <c>TASK-194</c> §5 baseline simulation matrix — the canonical 5 Bosses ×
/// 3 policies × 5 deterministic seeds = 75 runs, plus the baseline content
/// snapshot every run is configured with.
///
/// <b>This type defines no gameplay rule and computes no gameplay value.</b> It
/// only assembles the inputs TASK-194 §5.1/§5.2 fixes and hands them to the
/// existing <see cref="BalanceSimulator"/>; the production pipeline then decides
/// everything. Bosses are read from <see cref="BossDefinitions.All"/> and Cards
/// are the authored content of <c>CARD_RULES.md</c> §2/§4 — nothing here invents,
/// tunes, or overrides a value.
///
/// <b>Why it exists.</b> <c>TASK-194</c> is an evidence task: the same matrix must
/// be executable repeatedly (for the raw artifact and for the determinism re-run)
/// from one definition rather than restated per test.
/// </summary>
internal static class BalanceBaselineMatrix
{
    /// <summary>
    /// The canonical <c>TASK-194</c> §5.1/§6.2 seed set. Seed 5 preserves
    /// continuity with the <c>TASK-193</c> fixture tests.
    /// </summary>
    public static readonly IReadOnlyList<ulong> Seeds =
        [20261001UL, 20261002UL, 20261003UL, 20261004UL, 20261005UL];

    /// <summary>
    /// The three approved scripted policies (<c>TASK-193</c> §3.2), in
    /// <c>TASK-194</c> §5.1 order.
    /// </summary>
    public static readonly IReadOnlyList<string> PolicyNames =
        [BalancePolicies.PassiveName, BalancePolicies.AverageName, BalancePolicies.SkilledName];

    /// <summary>
    /// The five MVP content Bosses, in <c>BOSS_RULES.md</c> §6.1 order — the same
    /// instances <c>TASK-194</c> §5.1 names. Preserves the historical baseline
    /// configuration of Mộc Yêu at Threshold 5 for historical baseline reproducibility.
    /// </summary>
    public static readonly IReadOnlyList<BossDefinition> Bosses =
    [
        BossDefinitions.HoaLong,
        BossDefinitions.ThuyMa,
        BossDefinitions.MocYeu with
        {
            PassiveDefinition = new BossPassiveDefinition(
                new PassiveId("boss-moc-yeu-regen"),
                5,
                "Default"),
        },
        BossDefinitions.SonThachVe,
        BossDefinitions.KimLoiVuong,
    ];

    /// <summary>
    /// The per-run safety bound (<c>TASK-194</c> §5.2) — the harness default, a
    /// technical termination bound that approves no duration target (Q-1 is
    /// QUALITATIVE).
    /// </summary>
    public const int MaxTurns = BalanceSimulationConfiguration.DefaultMaxTurns;

    /// <summary>
    /// The battle owner identity TASK-194 §5.2 fixes for every run
    /// (<c>GAME_STATE.md</c> §2.8 — identity only, no combat pool).
    /// </summary>
    public static readonly PlayerId Owner = new("player-balance-sim");

    /// <summary>The raw-evidence artifact TASK-194 §17 requires.</summary>
    public const string ArtifactFileName = "TASK-194-baseline-simulation-results.json";

    /// <summary>An environment variable that redirects the artifact write.</summary>
    public const string ArtifactPathVariable = "TASK194_ARTIFACT_PATH";

    /// <summary>
    /// The Card content the baseline loadout is cast against — the three MVP
    /// Basic Cards plus one Pet Skill, with their <c>CARD_RULES.md</c> §2/§4.1
    /// authored costs and effects. <c>card-power-charge</c> keeps its authored
    /// <c>PowerCost = 0</c> (<c>CARD_RULES.md</c> §2 item 3; <c>B-01</c> is NOT
    /// implemented).
    /// </summary>
    public static readonly IReadOnlyList<CardDefinition> Cards =
    [
        new CardDefinition
        {
            CardDefinitionId = "card-heal",
            Name = "Heal",
            Category = CardCategory.Basic,
            PowerCost = 20,
            LoadoutCopyLimit = 1,
            EffectDefinition = CardEffectDefinitions.Create(
                CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.PercentMaxHp, 20)),
        },
        new CardDefinition
        {
            CardDefinitionId = "card-shield",
            Name = "Shield",
            Category = CardCategory.Basic,
            PowerCost = 20,
            LoadoutCopyLimit = 1,
            EffectDefinition = CardEffectDefinitions.Create(
                CardEffectDefinition.Create(CardEffectType.Shield, CardEffectValueType.PercentMaxHp, 20)),
        },
        new CardDefinition
        {
            CardDefinitionId = "card-power-charge",
            Name = "Power Charge",
            Category = CardCategory.Basic,
            PowerCost = 0,
            LoadoutCopyLimit = 1,
            EffectDefinition = CardEffectDefinitions.Create(
                CardEffectDefinition.Create(CardEffectType.Power, CardEffectValueType.Flat, 25)),
        },
        new CardDefinition
        {
            CardDefinitionId = "card-iron-fang",
            Name = "Iron Fang",
            Category = CardCategory.PetSkill,
            PowerCost = 40,
            LoadoutCopyLimit = 1,
            EffectDefinition = CardEffectDefinitions.Create(
                CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 120),
                CardEffectDefinition.Crit(10, "NextAttack")),
        },
    ];

    /// <summary>
    /// The loadout snapshot in <c>TASK-194</c> §5.2 order
    /// (<c>Heal → Shield → Power Charge → Iron Fang</c>). A fresh array is
    /// produced per configuration so no run can alias another run's snapshot.
    /// </summary>
    public static EquippedCardIdentity[] EquippedCards() =>
    [
        new("card-heal"),
        new("card-shield"),
        new("card-power-charge"),
        new("card-iron-fang"),
    ];

    /// <summary>
    /// The active Pet configuration TASK-194 §5.2 fixes for every run: Xích Lang
    /// (<c>COMBAT_RULES.md</c> §1.1 defaults, <c>PET_RULES.md</c> §8 passive
    /// threshold 5), Hỏa Element, unequipped Relic snapshot
    /// (<c>RELIC_RULES.md</c> §2.3 staging position — <c>null</c>, never an
    /// invented empty loadout).
    /// </summary>
    public static BattleStateService.PetConfiguration PetConfiguration() => new(
        PetId: new PetId("petinstance-balance"),
        Element: Element.Hoa,
        PassiveId: new PassiveId("passive-xich-lang"),
        PassiveThreshold: 5,
        PassiveResetOverride: null,
        EquippedRelics: null,
        EquippedCards: EquippedCards());

    /// <summary>
    /// One cell of the matrix in the order TASK-194 §9.2 iterates it: Boss, then
    /// policy, then seed.
    /// </summary>
    public static IEnumerable<(BossDefinition Boss, string PolicyName, ulong Seed)> Cells()
    {
        foreach (var boss in Bosses)
        {
            foreach (var policyName in PolicyNames)
            {
                foreach (var seed in Seeds)
                {
                    yield return (boss, policyName, seed);
                }
            }
        }
    }

    /// <summary>
    /// Builds the complete input set for one cell (<c>TASK-194</c> §5.2). Every
    /// member is an identity, a seed, or the caller's own selection; no balance
    /// value is varied and the Relic snapshot is <c>null</c> for all 75 runs.
    /// </summary>
    public static BalanceSimulationConfiguration Configuration(
        BossDefinition boss,
        string policyName,
        ulong seed) => new()
    {
        Seed = seed,
        PlayerId = Owner,
        Pet = PetConfiguration(),
        Boss = boss,
        CardDefinitions = Cards,
        RelicDefinitions = null,
        Policy = BalancePolicies.ByName(policyName),
        MaxTurns = MaxTurns,
    };

    /// <summary>
    /// Runs one matrix cell through the existing <c>TASK-193</c> harness (which
    /// drives the production pipeline) against an isolated in-process store.
    /// </summary>
    public static Task<BalanceSimulationResult> RunAsync(
        BossDefinition boss,
        string policyName,
        ulong seed) =>
        new BalanceSimulator(
            Configuration(boss, policyName, seed),
            new InMemoryBattleStateRepository()).RunAsync();

    /// <summary>
    /// Serializes the collected runs into the raw-evidence artifact text
    /// (<c>TASK-194</c> §17).
    ///
    /// <b>It is a pure function of its argument.</b> No clock, no
    /// <c>Guid</c>, no machine-random value is written: the same 75 results
    /// serialize to the same bytes, so the artifact is reproducible evidence
    /// rather than a snapshot that changes on every run
    /// (<c>MATCH3_RULES.md</c> §7.2, <c>AGENTS.md</c> §11).
    /// </summary>
    public static string SerializeEvidence(IReadOnlyList<BalanceSimulationResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["task"] = "TASK-194",
            ["title"] = "Balance Baseline Simulation & Decision Evidence",
            ["contents"] = "raw baseline simulation results for the TASK-194 §5 matrix",
            ["harness"] = "tests/backend/GameServer.Application.Tests/Balance/BalanceSimulator.cs (TASK-193)",
            ["mode"] = BalanceSimulationMode.Baseline.ToString(),
            ["verdicts"] = "none — the artifact records the four technical outcomes and no balance judgement",
            ["runtime"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["framework"] = RuntimeInformation.FrameworkDescription,
                ["osDescription"] = RuntimeInformation.OSDescription,
                ["processArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
            },
            ["matrix"] = MatrixRecord(),
            ["runCount"] = results.Count,
            ["outcomeCounts"] = OutcomeCounts(results),
            ["runs"] = results.Select(RunRecord).ToArray(),
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    /// <summary>
    /// Writes the artifact to <see cref="ArtifactPath"/>, creating the directory
    /// when the archival location does not exist yet.
    /// </summary>
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

    /// <summary>
    /// The artifact's archival location: <c>tasks/artifacts/…</c> under the
    /// repository root, or the path named by
    /// <see cref="ArtifactPathVariable"/> when a caller redirects the write.
    /// </summary>
    public static string ArtifactPath()
    {
        var configured = Environment.GetEnvironmentVariable(ArtifactPathVariable);

        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(RepositoryRoot(), "tasks", "artifacts", ArtifactFileName)
            : Path.GetFullPath(configured);
    }

    /// <summary>
    /// Locates the repository root by walking up from the test output directory
    /// to the directory that holds <c>AGENTS.md</c> and the backend solution —
    /// the two markers that identify this repository rather than any ancestor
    /// directory.
    /// </summary>
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

    /// <summary>
    /// The matrix definition, recorded so a reader can reproduce the whole
    /// experiment from the artifact alone (<c>TASK-193</c> H-09).
    /// </summary>
    private static Dictionary<string, object?> MatrixRecord() => new(StringComparer.Ordinal)
    {
        ["bosses"] = Bosses.Select(BossRecord).ToArray(),
        ["policies"] = PolicyNames.ToArray(),
        ["seeds"] = Seeds.ToArray(),
        ["dimensions"] = $"{Bosses.Count} bosses x {PolicyNames.Count} policies x {Seeds.Count} seeds",
        ["configuration"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["playerId"] = Owner.Value,
            ["petId"] = "petinstance-balance",
            ["petElement"] = Element.Hoa.ToString(),
            ["petPassiveId"] = "passive-xich-lang",
            ["petPassiveThreshold"] = 5,
            ["baseStatSource"] = "COMBAT_RULES.md §1.1 battle defaults (no stat input passes a Level/Star curve)",
            ["equippedCardIds"] = EquippedCards().Select(card => card.Value).ToArray(),
            ["cardPowerCosts"] = Cards.ToDictionary(
                card => card.CardDefinitionId,
                card => card.PowerCost,
                StringComparer.Ordinal),
            ["equippedRelics"] = null,
            ["equippedRelicsNote"] = "RELIC_RULES.md §2.3 — no Relic content attached; the baseline is unequipped",
            ["maxTurns"] = MaxTurns,
        },
    };

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

    /// <summary>
    /// How many runs ended in each of the four <c>TASK-193</c> §5.3 technical
    /// outcomes. It counts; it classifies no run as balanced or unbalanced.
    /// </summary>
    private static Dictionary<string, int> OutcomeCounts(IReadOnlyList<BalanceSimulationResult> results) =>
        Enum.GetValues<BalanceSimulationOutcome>().ToDictionary(
            outcome => outcome.ToString(),
            outcome => results.Count(result => result.Outcome == outcome),
            StringComparer.Ordinal);

    /// <summary>
    /// One run's record: its identifying inputs, its terminal outcome, and the
    /// M-01…M-15 metric set. The metric names carry their <c>TASK-194</c> §8
    /// metric number so a reader can map a value back to its definition without
    /// consulting the code.
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
}
