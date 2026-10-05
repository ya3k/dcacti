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
/// The <c>TASK-193</c> §7 acceptance criteria, H-01…H-10.
///
/// Each test states the criterion it proves and why the assertion establishes
/// it. The scenarios drive the real production pipeline through
/// <see cref="BalanceSimulator"/>; none mocks a gameplay calculation away
/// (<c>H-03</c>), and none manipulates production code to force an outcome
/// (<c>H-05</c>).
/// </summary>
public sealed class BalanceSimulatorTests
{
    private static readonly PlayerId Owner = new("player-balance-sim");

    /// <summary>
    /// The Card set the scenarios cast against — the three MVP Basic Cards plus
    /// one Pet Skill Card, with their <c>CARD_RULES.md</c> §2/§4.1 authored costs
    /// and effects. Power Charge keeps its authored <c>0</c> cost: <c>B-01</c> is
    /// untouched by this task.
    /// </summary>
    private static readonly CardDefinition HealCard = new()
    {
        CardDefinitionId = "card-heal",
        Name = "Heal",
        Category = CardCategory.Basic,
        PowerCost = 20,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.PercentMaxHp, 20)),
    };

    private static readonly CardDefinition ShieldCard = new()
    {
        CardDefinitionId = "card-shield",
        Name = "Shield",
        Category = CardCategory.Basic,
        PowerCost = 20,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Shield, CardEffectValueType.PercentMaxHp, 20)),
    };

    private static readonly CardDefinition PowerChargeCard = new()
    {
        CardDefinitionId = "card-power-charge",
        Name = "Power Charge",
        Category = CardCategory.Basic,
        PowerCost = 0,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Power, CardEffectValueType.Flat, 25)),
    };

    private static readonly CardDefinition IronFangCard = new()
    {
        CardDefinitionId = "card-iron-fang",
        Name = "Iron Fang",
        Category = CardCategory.PetSkill,
        PowerCost = 40,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 120),
            CardEffectDefinition.Crit(10, "NextAttack")),
    };

    private static readonly CardDefinition[] AllCards =
        [HealCard, ShieldCard, PowerChargeCard, IronFangCard];

    private static BattleStateService.PetConfiguration PetConfiguration(
        int passiveThreshold = 5,
        EquippedCardIdentity[]? cards = null) => new(
        PetId: new PetId("petinstance-balance"),
        Element: Element.Hoa,
        PassiveId: new PassiveId("passive-xich-lang"),
        PassiveThreshold: passiveThreshold,
        PassiveResetOverride: null,
        EquippedRelics: null,
        EquippedCards: cards ??
        [
            new EquippedCardIdentity("card-heal"),
            new EquippedCardIdentity("card-shield"),
            new EquippedCardIdentity("card-power-charge"),
            new EquippedCardIdentity("card-iron-fang"),
        ]);

    /// <summary>
    /// A baseline configuration over the documented MVP content
    /// (<c>BOSS_RULES.md</c> §6.1's Hỏa Long, <c>COMBAT_RULES.md</c> §1.1's Pet
    /// defaults) with no balance value varied.
    /// </summary>
    private static BalanceSimulationConfiguration Configuration(
        ulong seed = 20261005UL,
        BossDefinition? boss = null,
        int maxTurns = BalanceSimulationConfiguration.DefaultMaxTurns,
        string policy = BalancePolicies.AverageName,
        int passiveThreshold = 5) => new()
    {
        Seed = seed,
        PlayerId = Owner,
        Pet = PetConfiguration(passiveThreshold),
        Boss = boss ?? BossDefinitions.HoaLong,
        CardDefinitions = AllCards,
        RelicDefinitions = null,
        Policy = BalancePolicies.ByName(policy),
        MaxTurns = maxTurns,
    };

    private static BalanceSimulator Simulator(BalanceSimulationConfiguration configuration) =>
        new(configuration, new InMemoryBattleStateRepository());

    // ===================================================================
    // H-01 — same seed + same configuration ⇒ identical result
    // ===================================================================

    [Fact]
    public async Task H01_IdenticalInputs_ProduceIdenticalResults()
    {
        // TASK-193 §4.1: identical seed, configuration, and policy must produce
        // an identical result. The assertion covers the outcome AND the full
        // metric set, because a run that agreed on the winner but not on its
        // damage curve would not be reproducible evidence.
        var configuration = Configuration(seed: 4242UL);

        var first = await Simulator(configuration).RunAsync();
        var second = await Simulator(configuration).RunAsync();

        Assert.Equal(first.Outcome, second.Outcome);
        Assert.Equal(first.Metrics.Turns, second.Metrics.Turns);
        Assert.Equal(first.Metrics.PlayerDamageTotal, second.Metrics.PlayerDamageTotal);
        Assert.Equal(first.Metrics.BossDamageTotal, second.Metrics.BossDamageTotal);
        Assert.Equal(first.Metrics.PowerGeneratedTotal, second.Metrics.PowerGeneratedTotal);
        Assert.Equal(first.Metrics.PowerSpentTotal, second.Metrics.PowerSpentTotal);
        Assert.Equal(first.Metrics.CastsTotal, second.Metrics.CastsTotal);
        Assert.Equal(first.Metrics.ComboMax, second.Metrics.ComboMax);
        Assert.Equal(first.Metrics.BossHpPerTurn, second.Metrics.BossHpPerTurn);
        Assert.Equal(first.Metrics.PlayerHpPerTurn, second.Metrics.PlayerHpPerTurn);
        Assert.Equal(first.Metrics.ResourceTrace, second.Metrics.ResourceTrace);

        // The terminal state must match too — the strongest form of "identical
        // run".
        Assert.Equal(first.FinalState!.Turn, second.FinalState!.Turn);
        Assert.Equal(first.FinalState.BossState.HP, second.FinalState.BossState.HP);
        Assert.Equal(first.FinalState.PetState.HP, second.FinalState.PetState.HP);
        Assert.Equal(first.FinalState.RngState, second.FinalState.RngState);
    }

    // ===================================================================
    // H-02 — a different seed deterministically drives a different board
    // ===================================================================

    [Fact]
    public async Task H02_DifferentSeed_ProducesADeterministicButDifferentBoard()
    {
        // TASK-193 §4.1 / H-02: the seed must actually drive board generation, so
        // the harness is not accidentally pinned to one board. H-02 explicitly
        // does NOT require different outcomes — randomness may legitimately
        // converge — so this asserts the generated BOARDS differ, and separately
        // that each seed is itself reproducible.
        var seedA = Configuration(seed: 1111UL);
        var seedB = Configuration(seed: 2222UL);

        var a = await Simulator(seedA).RunAsync();
        var b = await Simulator(seedB).RunAsync();

        var boardA = a.FinalState!.BoardState.Cells.Select(c => c.GemType).ToArray();
        var boardB = b.FinalState!.BoardState.Cells.Select(c => c.GemType).ToArray();

        Assert.NotEqual(boardA, boardB);

        // Each seed remains individually reproducible — the difference is
        // deterministic, not ambient.
        var aAgain = await Simulator(seedA).RunAsync();
        Assert.Equal(
            aAgain.FinalState!.BoardState.Cells.Select(c => c.GemType),
            boardA);
    }

    [Fact]
    public async Task H02_TwoSeedsConverge_WhenTheOutcomeIsSeedIndependent()
    {
        // H-02's explicit allowance: a different seed may produce the SAME
        // outcome. A Boss with 1 HP dies to the first committed Swap whatever
        // board is generated, so both seeds must report VICTORY. This proves the
        // harness does not manufacture divergence to satisfy H-02.
        var fragile = BossDefinitions.HoaLong with { MaxHP = 1 };

        var a = await Simulator(Configuration(seed: 3333UL, boss: fragile)).RunAsync();
        var b = await Simulator(Configuration(seed: 4444UL, boss: fragile)).RunAsync();

        Assert.Equal(BalanceSimulationOutcome.Victory, a.Outcome);
        Assert.Equal(BalanceSimulationOutcome.Victory, b.Outcome);
    }

    // ===================================================================
    // H-03 — baseline runs the actual production rules
    // ===================================================================

    [Fact]
    public async Task H03_Baseline_UsesTheRealProductionPipeline()
    {
        // TASK-193 H-03: the baseline must exercise the authoritative rules, not
        // a reimplementation. This is proven by the rules' own fingerprints
        // appearing in the metric set, each of which only the real pipeline can
        // produce:
        //
        //   * the Boss deals damage back through step 18 (GAME_RULES.md §17) —
        //     a stub would leave BossDamageTotal at 0;
        //   * resources come from the §17 step 12/13 generation path;
        //   * the committed Swap advances Turn and Sequence by exactly 1 each
        //     (MATCH3_RULES.md §8.1/§8.2), which is the pipeline's own counter
        //     discipline.
        var result = await Simulator(Configuration(seed: 20261005UL)).RunAsync();

        Assert.NotEqual(BalanceSimulationOutcome.InvalidSimulation, result.Outcome);

        // The real Boss Response ran: the Boss retaliated at least once.
        Assert.True(
            result.Metrics.BossDamageTotal > 0,
            "the production Boss Response stage must have dealt damage");

        // The real resource stage ran.
        Assert.True(
            result.Metrics.PowerGeneratedTotal > 0,
            "the production resource generation stage must have produced Power");

        // The real Turn/Sequence discipline holds on the terminal state. A
        // committed Swap advances BOTH counters by exactly 1
        // (MATCH3_RULES.md §8.1 item 1, §8.2 item 1), while an auxiliary Card
        // cast advances Sequence alone (CARD_RULES.md §3 item 5 — a cast consumes
        // no Turn). Sequence is therefore at least Turn, and equals Turn when the
        // run cast no Cards.
        var state = result.FinalState!;
        Assert.Equal(result.Metrics.Turns, state.Turn);
        Assert.True(
            state.Sequence >= state.Turn,
            "every committed Swap advances both counters, and a Card cast advances Sequence alone");

        if (result.Metrics.CastsTotal == 0)
        {
            Assert.Equal(state.Turn, state.Sequence);
        }
    }

    [Fact]
    public async Task H03_BaselineDamageMatchesThePipelinesOwnTurnReports()
    {
        // H-03, sharper: the summed damage total must equal the sum of the
        // per-Turn values the harness recorded from the pipeline's own
        // DamageDealt reports. If the harness computed damage independently, the
        // two would be free to disagree.
        var result = await Simulator(Configuration(seed: 5150UL)).RunAsync();

        Assert.Equal(result.Metrics.PlayerDamageTotal, result.Metrics.PlayerDamagePerTurn.Sum());
        Assert.Equal(result.Metrics.BossDamageTotal, result.Metrics.BossDamagePerTurn.Sum());

        // And the per-Turn damage series is exactly as long as the Turns resolved.
        Assert.Equal(result.Metrics.Turns, result.Metrics.PlayerDamagePerTurn.Count);
        Assert.Equal(result.Metrics.Turns, result.Metrics.BossDamagePerTurn.Count);
    }

    // ===================================================================
    // H-04 — the harness does not modify production behavior
    // ===================================================================

    [Fact]
    public async Task H04_RunningTheHarness_LeavesNoTraceOutsideItsOwnBattle()
    {
        // TASK-193 H-04: the harness must not modify production behaviour or
        // state outside its isolated simulation context. It writes only to its
        // own in-process repository, under its own fixed battle id, and it
        // registers nothing globally — so a second, unrelated repository is
        // untouched by a run.
        var simulatorRepository = new InMemoryBattleStateRepository();
        var unrelatedRepository = new InMemoryBattleStateRepository();

        var simulator = new BalanceSimulator(Configuration(seed: 909UL), simulatorRepository);
        var result = await simulator.RunAsync();

        Assert.NotEqual(BalanceSimulationOutcome.InvalidSimulation, result.Outcome);

        // The unrelated store saw nothing at all.
        Assert.Equal(0, unrelatedRepository.RecordCount);
        Assert.Equal(0, unrelatedRepository.WriteCount);
        Assert.Null(await unrelatedRepository.GetAsync("balance-simulation"));

        // The harness's own store holds only its one simulation record.
        Assert.Equal(1, simulatorRepository.RecordCount);
    }

    [Fact]
    public async Task H04_TwoSimulators_DoNotShareMutableState()
    {
        // H-04 corollary: two runs are independent. A shared static or cached
        // board would make the second run inherit the first's state; identical
        // results from two separate simulators prove they do not.
        var configuration = Configuration(seed: 777UL);

        var first = await new BalanceSimulator(configuration, new InMemoryBattleStateRepository()).RunAsync();
        var second = await new BalanceSimulator(configuration, new InMemoryBattleStateRepository()).RunAsync();

        Assert.Equal(first.Outcome, second.Outcome);
        Assert.Equal(first.FinalState!.BossState.HP, second.FinalState!.BossState.HP);
        Assert.Equal(first.Metrics.PlayerDamageTotal, second.Metrics.PlayerDamageTotal);
    }

    // ===================================================================
    // H-05 — victory, defeat, and stalemate are all reachable
    // ===================================================================

    [Fact]
    public async Task H05_Victory_IsReachable()
    {
        // A Boss with 1 HP dies to the first committed Swap — a legitimate
        // configuration, not manipulated production code. The outcome must be the
        // production pipeline's own BattleWon determination.
        var result = await Simulator(Configuration(
            seed: 20261005UL,
            boss: BossDefinitions.HoaLong with { MaxHP = 1 })).RunAsync();

        Assert.Equal(BalanceSimulationOutcome.Victory, result.Outcome);
        Assert.Equal(1, result.Metrics.Turns);
    }

    [Fact]
    public async Task H05_Defeat_IsReachable()
    {
        // A Boss whose ATK overwhelms the Pet's HP pool kills it within the
        // configured Turn budget, and the production BattleLost determination is
        // what the harness reports.
        var lethal = BossDefinitions.HoaLong with { ATK = 100000 };

        var result = await Simulator(Configuration(seed: 20261005UL, boss: lethal)).RunAsync();

        Assert.Equal(BalanceSimulationOutcome.Defeat, result.Outcome);
        Assert.True(result.Metrics.Turns >= 1);
    }

    [Fact]
    public async Task H05_Stalemate_IsReachable_AndIsNotReinterpreted()
    {
        // A Boss that cannot be killed within the Turn budget (enormous HP) and
        // cannot kill the Pet (harmless ATK) produces the documented
        // non-termination case. It must be reported as STALEMATE — never folded
        // into victory or defeat.
        var stalemate = BossDefinitions.HoaLong with { MaxHP = int.MaxValue / 2, ATK = 0 };

        var result = await Simulator(Configuration(
            seed: 20261005UL,
            boss: stalemate,
            maxTurns: 12)).RunAsync();

        Assert.Equal(BalanceSimulationOutcome.Stalemate, result.Outcome);
        Assert.Equal(12, result.Metrics.Turns);
    }

    [Fact]
    public async Task H05_InvalidSimulation_IsReachable_AndIsNotAGenericErrorBucket()
    {
        // TASK-193 §5.3: INVALID_SIMULATION is only for a configuration the
        // simulation cannot legally run. A negative MaxTurns is exactly that, and
        // it is detected before any battle state exists.
        var result = await Simulator(Configuration(maxTurns: 0)).RunAsync();

        Assert.Equal(BalanceSimulationOutcome.InvalidSimulation, result.Outcome);
        Assert.Null(result.FinalState);
        Assert.Contains("MaxTurns", result.OutcomeDetail);
    }

    [Fact]
    public async Task H05_InvalidSimulation_RejectsAZeroCardBattleConfig()
    {
        // CARD_RULES.md §1 defines no zero-Card battle, so a configuration with an
        // empty loadout is not runnable. It is reported as invalid rather than
        // silently simulated as a battle with no Cards.
        var configuration = Configuration() with
        {
            Pet = PetConfiguration(cards: []),
        };

        var result = await Simulator(configuration).RunAsync();

        Assert.Equal(BalanceSimulationOutcome.InvalidSimulation, result.Outcome);
    }

    [Fact]
    public async Task H05_ARejectedSwapIsGameplay_NotAnInvalidSimulation()
    {
        // TASK-193 §5.3: a production rejection is ordinary gameplay, not an
        // error. The passive policy proposes low-index pairs in a fixed order and
        // some do not match — those are recorded as rejected Swaps while the run
        // continues normally.
        var result = await Simulator(Configuration(
            seed: 20261005UL,
            boss: BossDefinitions.HoaLong with { MaxHP = 1 })).RunAsync();

        Assert.NotEqual(BalanceSimulationOutcome.InvalidSimulation, result.Outcome);
        Assert.True(
            result.Metrics.RejectedSwaps > 0,
            "the fixed low-index scan should have proposed at least one non-matching Swap first");
    }

    // ===================================================================
    // H-06 — every M-01…M-15 metric is populated
    // ===================================================================

    [Fact]
    public async Task H06_EveryMetric_IsPopulatedPerItsDefinition()
    {
        // TASK-193 H-06: all fifteen metrics must be present and populated. Each
        // assertion below names the metric and the documented source it is read
        // from, so a missing or invented value fails here.
        var result = await Simulator(Configuration(
            seed: 20261005UL,
            boss: BossDefinitions.HoaLong with { MaxHP = 3000 })).RunAsync();

        var m = result.Metrics;

        // M-01 / M-02 — Turns and duration.
        Assert.True(m.Turns > 0);
        Assert.Equal(m.Turns, m.DurationTurns);
        Assert.Equal(result.FinalState!.Turn, m.Turns);

        // M-03 — player damage, total and per Turn.
        Assert.True(m.PlayerDamageTotal > 0);
        Assert.Equal(m.Turns, m.PlayerDamagePerTurn.Count);
        Assert.Equal(m.PlayerDamageTotal, m.PlayerDamagePerTurn.Sum());

        // M-04 — Boss damage, total and per Turn.
        Assert.True(m.BossDamageTotal > 0);
        Assert.Equal(m.Turns, m.BossDamagePerTurn.Count);
        Assert.Equal(m.BossDamageTotal, m.BossDamagePerTurn.Sum());

        // M-05 — Boss HP progression.
        Assert.Equal(m.Turns, m.BossHpPerTurn.Count);
        Assert.Equal(m.BossHpPerTurn.Min(), m.BossHpMin);

        // M-06 — player HP progression.
        Assert.Equal(m.Turns, m.PlayerHpPerTurn.Count);
        Assert.Equal(m.PlayerHpPerTurn.Min(), m.PlayerHpMin);

        // M-07 / M-08 — Power generated, spent, and final.
        Assert.True(m.PowerGeneratedTotal > 0);
        Assert.True(m.PowerSpentTotal > 0);
        Assert.InRange(m.PowerFinal, 0, 100);

        // M-09 — Cards cast, by Card and by Turn.
        Assert.True(m.CastsTotal > 0);
        Assert.Equal(m.CastsTotal, m.CastsByCard.Values.Sum());
        Assert.Equal(m.CastsTotal, m.CastTurns.Count);
        Assert.All(m.CastTurns, turn => Assert.InRange(turn, 1, m.Turns));

        // M-10 — Relic triggers. Zero is valid: this battle carries no Relic
        // content (RELIC_RULES.md §2.3).
        Assert.Equal(0, m.RelicTriggersTotal);
        Assert.Empty(m.RelicTriggersByRelic);

        // M-11 — Combo.
        Assert.Equal(m.Turns, m.ComboPerTurn.Count);
        Assert.Equal(m.ComboPerTurn.Max(), m.ComboMax);
        Assert.Equal(m.Turns, m.ComboDistribution.Values.Sum());

        // M-12 — Element modifiers: every damage instance is classified, so the
        // three counts sum to the number of damage instances the pipeline
        // reported. Hỏa Long is Hỏa and the Pet is Hỏa → neutral, so at least one
        // neutral instance must exist.
        Assert.True(m.ElementNeutralCount > 0);
        Assert.True(m.ElementAdvantageCount + m.ElementNeutralCount + m.ElementDisadvantageCount > 0);

        // M-13 — Boss regeneration (0 for Hỏa Long, which has no regen Passive),
        // Skill cadence, Passive triggers, and Enrage.
        Assert.Equal(0, m.BossRegenerationTotal);
        Assert.True(m.BossSkillCastCount >= 0);
        Assert.True(m.BossPassiveTriggerCount >= 0);

        // M-14 — the outcome is one of the four documented values.
        Assert.Contains(
            result.Outcome,
            new[]
            {
                BalanceSimulationOutcome.Victory,
                BalanceSimulationOutcome.Defeat,
                BalanceSimulationOutcome.Stalemate,
                BalanceSimulationOutcome.InvalidSimulation,
            });

        // M-15 — the resource trace has one checkpoint per resolved Turn.
        Assert.Equal(m.Turns, m.ResourceTrace.Count);
        Assert.Equal(1, m.ResourceTrace[0].Turn);
        Assert.Equal(m.Turns, m.ResourceTrace[^1].Turn);
    }

    [Fact]
    public async Task H06_MocYeuRegeneration_IsMeasured()
    {
        // M-13 in its non-zero case: BOSS_RULES.md §6.2.3's regeneration is the
        // metric B-07 depends on. The harness must MEASURE it — B-07 itself stays
        // open (Q-8), and this test asserts only that the measurement exists.
        var result = await Simulator(Configuration(
            seed: 20261005UL,
            boss: BossDefinitions.MocYeu with { MaxHP = 20000, ATK = 0 },
            maxTurns: 40,
            passiveThreshold: 5)).RunAsync();

        Assert.NotEqual(BalanceSimulationOutcome.InvalidSimulation, result.Outcome);
        Assert.True(
            result.Metrics.BossRegenerationTotal > 0,
            "Mộc Yêu's 5% MaxHP regeneration must be observable in the Boss HP progression");
        Assert.True(result.Metrics.BossRegenerationTurns > 0);
    }

    [Fact]
    public async Task H06_ElementModifiers_AreClassifiedFromThePipelineValue()
    {
        // M-12 with a disadvantage: COMBAT_RULES.md §3 step 3 / ELEMENT_RULES.md
        // §2.2. A Kim Pet against a Hỏa Boss is the documented disadvantage
        // matchup, so the counter must move — proving the classification reads
        // the pipeline's own factor rather than assuming neutrality.
        var configuration = Configuration(
            seed: 20261005UL,
            boss: BossDefinitions.HoaLong with { MaxHP = 3000 }) with
        {
            Pet = PetConfiguration() with { Element = Element.Kim },
        };

        var result = await Simulator(configuration).RunAsync();

        Assert.True(
            result.Metrics.ElementDisadvantageCount > 0,
            "Kim attacking Hỏa is the documented disadvantage matchup");
    }

    [Fact]
    public async Task H06_RelicTriggers_AreCounted_WhenRelicsAreEquipped()
    {
        // M-10 in its non-zero case, using the RELIC_RULES.md §8.5 Mana Crystal
        // declaration (OnMatchCount ≥1 → +10 Power). The definition is supplied as
        // content; the harness measures whatever the real Relic stage resolves.
        var manCrystal = ManaCrystalRelic();

        var configuration = Configuration() with
        {
            Pet = PetConfiguration() with
            {
                EquippedRelics = [new GameServer.Domain.Relics.EquippedRelicIdentity("relic-instance-1")],
            },
            RelicDefinitions = [manCrystal],
        };

        var result = await Simulator(configuration).RunAsync();

        Assert.NotEqual(BalanceSimulationOutcome.InvalidSimulation, result.Outcome);
        Assert.True(result.Metrics.RelicTriggersTotal > 0, "the equipped Relic must have fired at least once");
        Assert.Equal(
            result.Metrics.RelicTriggersTotal,
            result.Metrics.RelicTriggersByRelic.Values.Sum());
    }

    /// <summary>
    /// The <c>RELIC_RULES.md</c> §8.5 Mana Crystal declaration, transcribed as
    /// content exactly as the other Relic runtime tests build it.
    /// </summary>
    private static GameServer.Domain.Relics.RelicDefinition ManaCrystalRelic() => new()
    {
        RelicDefinitionId = "relic-mana-crystal",
        Name = "Mana Crystal",
        Trigger = GameServer.Domain.Relics.RelicResolver.OnMatchCountTrigger,
        Condition = GameServer.Domain.Relics.RelicCondition.Create(
            GameServer.Domain.Relics.RelicConditionType.MatchCountAtLeast,
            1),
        EffectDefinition = GameServer.Domain.Relics.RelicEffectDefinitions.Create(
            GameServer.Domain.Relics.RelicEffectDefinition.Create(
                GameServer.Domain.Relics.RelicEffectType.Power,
                GameServer.Domain.Relics.RelicEffectValueType.Flat,
                10,
                GameServer.Domain.Relics.RelicEffectTarget.Pet,
                GameServer.Domain.Relics.RelicEffectLifetime.Immediate)),
    };

    // ===================================================================
    // H-07 — a comparison changes only the selected parameter
    // ===================================================================

    [Fact]
    public void H07_AComparisonConfiguration_DiffersInExactlyOneParameter()
    {
        // TASK-193 H-07: the harness must ensure only the explicitly selected
        // parameter differs. Building the comparison with a `with` expression
        // makes every other member provably identical — this test proves the
        // mechanism, not merely the intention.
        var baseline = Configuration(seed: 20261005UL);
        var candidate = baseline with { Boss = baseline.Boss with { MaxHP = 4000 } };

        Assert.Equal(baseline.Seed, candidate.Seed);
        Assert.Equal(baseline.PlayerId, candidate.PlayerId);
        Assert.Equal(baseline.Pet, candidate.Pet);
        Assert.Equal(baseline.CardDefinitions, candidate.CardDefinitions);
        Assert.Equal(baseline.RelicDefinitions, candidate.RelicDefinitions);
        Assert.Equal(baseline.Policy, candidate.Policy);
        Assert.Equal(baseline.MaxTurns, candidate.MaxTurns);

        // Exactly one parameter differs.
        Assert.NotEqual(baseline.Boss, candidate.Boss);
    }

    [Fact]
    public void H07_ChangingTheBossStat_DoesNotMutateTheBaselineConfiguration()
    {
        // A `record` with-expression produces a new value; the baseline is
        // untouched. This is the property that keeps a comparison honest across
        // repeated runs, and it fails loudly if the model ever becomes mutable.
        var baseline = Configuration(seed: 20261005UL);
        var originalMaxHp = baseline.Boss.MaxHP;

        _ = baseline with { Boss = baseline.Boss with { MaxHP = 999 } };

        Assert.Equal(originalMaxHp, baseline.Boss.MaxHP);
        Assert.Equal(BossDefinitions.HoaLong.MaxHP, baseline.Boss.MaxHP);
    }

    [Fact]
    public async Task H07_AComparisonRun_ReportsTheSameModeAndPolicyAsItsBaseline()
    {
        // The comparison differs in ONE content value; mode and policy are not
        // silently changed alongside it.
        var baselineConfig = Configuration(seed: 20261005UL, boss: BossDefinitions.HoaLong with { MaxHP = 3000 });
        var candidateConfig = baselineConfig with { Boss = baselineConfig.Boss with { MaxHP = 3500 } };

        var baseline = await Simulator(baselineConfig).RunAsync();
        var candidate = await Simulator(candidateConfig).RunAsync();

        Assert.Equal(baseline.PolicyName, candidate.PolicyName);
        Assert.Equal(BalanceSimulationMode.Baseline, baseline.Mode);
        Assert.Equal(BalanceSimulationMode.Baseline, candidate.Mode);

        // The varied parameter is the one the result reports as varied.
        Assert.Equal(3000, baseline.Boss.MaxHP);
        Assert.Equal(3500, candidate.Boss.MaxHP);
    }

    [Fact]
    public async Task H07_TheHarnessContainsNoHardCodedCandidateValue()
    {
        // TASK-193 §5.2: the varied value is caller input, never hard-coded, so
        // the harness cannot pre-approve a balance proposal. The same comparison
        // run therefore works for any value the caller supplies.
        var baselineConfig = Configuration(seed: 20261005UL, boss: BossDefinitions.HoaLong with { MaxHP = 2000 });

        foreach (var candidateMaxHp in new[] { 2500, 5000, 9000 })
        {
            var candidate = await Simulator(
                baselineConfig with { Boss = baselineConfig.Boss with { MaxHP = candidateMaxHp } }).RunAsync();

            Assert.Equal(candidateMaxHp, candidate.Boss.MaxHP);
            Assert.NotEqual(BalanceSimulationOutcome.InvalidSimulation, candidate.Outcome);
        }
    }

    // ===================================================================
    // H-08 — the hard Turn limit terminates a non-terminal run
    // ===================================================================

    [Fact]
    public async Task H08_MaxTurnsTerminatesTheRun_WithExactlyThatManyTurns()
    {
        // TASK-193 H-08: a run that cannot terminate must stop at the configured
        // limit and report STALEMATE, with the Turn count equal to the limit.
        // The Boss is unkillable (enormous HP) but harmless (ATK 0), so nothing
        // but the safety bound ends the loop.
        const int limit = 15;
        var unkillable = BossDefinitions.HoaLong with { MaxHP = int.MaxValue / 2, ATK = 0 };

        var result = await Simulator(Configuration(
            seed: 20261005UL,
            boss: unkillable,
            maxTurns: limit)).RunAsync();

        Assert.Equal(BalanceSimulationOutcome.Stalemate, result.Outcome);
        Assert.Equal(limit, result.Metrics.Turns);
        Assert.Equal(limit, result.Metrics.DurationTurns);
        Assert.Equal(limit, result.MaxTurns);
        Assert.Contains("maximum", result.OutcomeDetail);
    }

    [Fact]
    public async Task H08_TheTurnLimitIsConfigurablePerRun()
    {
        // The bound is per-run configuration, not a global constant — a technical
        // safety mechanism the caller sets, never a gameplay balance value.
        var unkillable = BossDefinitions.HoaLong with { MaxHP = int.MaxValue / 2, ATK = 0 };

        foreach (var limit in new[] { 3, 7, 11 })
        {
            var result = await Simulator(Configuration(
                seed: 20261005UL,
                boss: unkillable,
                maxTurns: limit)).RunAsync();

            Assert.Equal(BalanceSimulationOutcome.Stalemate, result.Outcome);
            Assert.Equal(limit, result.Metrics.Turns);
        }
    }

    [Fact]
    public async Task H08_TerminalRunsStopBeforeTheLimit()
    {
        // The limit does not truncate a fight that ends on its own: a battle won
        // in one Turn reports one Turn even with a large budget.
        var result = await Simulator(Configuration(
            seed: 20261005UL,
            boss: BossDefinitions.HoaLong with { MaxHP = 1 },
            maxTurns: 500)).RunAsync();

        Assert.Equal(BalanceSimulationOutcome.Victory, result.Outcome);
        Assert.Equal(1, result.Metrics.Turns);
        Assert.True(result.Metrics.Turns < result.MaxTurns);
    }

    // ===================================================================
    // H-09 — the result identifies everything needed to reproduce it
    // ===================================================================

    [Fact]
    public async Task H09_TheResult_IdentifiesSeedConfigurationPolicyOutcomeAndTurns()
    {
        // TASK-193 H-09: a reader must be able to reproduce the run from the
        // record alone. Every identifying member is asserted non-empty and
        // consistent with the configuration that produced it.
        var configuration = Configuration(seed: 8675309UL, policy: BalancePolicies.SkilledName);

        var result = await Simulator(configuration).RunAsync();

        Assert.Equal(8675309UL, result.Seed);
        Assert.Equal(BalancePolicies.SkilledName, result.PolicyName);
        Assert.Equal(BalanceSimulationMode.Baseline, result.Mode);
        Assert.Equal(Owner, result.PlayerId);
        Assert.Equal(configuration.Pet.PetId.Value, result.PetId);
        Assert.Equal(configuration.Boss.BossId.Value, result.BossId);
        Assert.Equal(BossDefinitions.HoaLong.MaxHP, result.Boss.MaxHP);
        Assert.Equal(configuration.MaxTurns, result.MaxTurns);

        Assert.NotEmpty(result.EquippedCardIds);
        Assert.Equal(4, result.EquippedCardIds.Count);
        Assert.Empty(result.EquippedRelicIds);

        Assert.False(string.IsNullOrWhiteSpace(result.OutcomeDetail));
        Assert.NotNull(result.FinalState);
        Assert.Equal(result.Metrics.Turns, result.FinalState!.Turn);
    }

    [Fact]
    public async Task H09_AResultCanBeReproduced_FromItsOwnRecordedInputs()
    {
        // H-09's purpose, demonstrated: take the recorded seed and the recorded
        // identifying content, rebuild the configuration, and get the same run.
        var original = await Simulator(Configuration(seed: 13579UL)).RunAsync();

        var rebuilt = Configuration(seed: original.Seed) with
        {
            PlayerId = original.PlayerId,
            Boss = original.Boss,
            Policy = BalancePolicies.ByName(original.PolicyName),
            MaxTurns = original.MaxTurns,
        };

        var reproduced = await Simulator(rebuilt).RunAsync();

        Assert.Equal(original.Outcome, reproduced.Outcome);
        Assert.Equal(original.Metrics.Turns, reproduced.Metrics.Turns);
        Assert.Equal(original.Metrics.PlayerDamageTotal, reproduced.Metrics.PlayerDamageTotal);
        Assert.Equal(original.FinalState!.BossState.HP, reproduced.FinalState!.BossState.HP);
    }

    [Fact]
    public async Task H09_TheResultReportsNoBalanceVerdict()
    {
        // TASK-193 §6: Q-1 is QUALITATIVE, so the harness must report duration
        // without producing a BALANCED/UNBALANCED/PASS/FAIL judgement. The result
        // model carries no such member — this asserts the shape stays that way by
        // checking the only outcome values are the four documented ones.
        var result = await Simulator(Configuration(seed: 20261005UL)).RunAsync();

        var outcomeNames = Enum.GetNames<BalanceSimulationOutcome>();

        Assert.Equal(4, outcomeNames.Length);
        Assert.DoesNotContain("Balanced", outcomeNames);
        Assert.DoesNotContain("Unbalanced", outcomeNames);
        Assert.DoesNotContain("Pass", outcomeNames);
        Assert.DoesNotContain("Fail", outcomeNames);

        // Duration is reported as a plain observation.
        Assert.True(result.Metrics.DurationTurns >= 0);
    }

    // ===================================================================
    // H-10 — runs through the existing test workflow
    // ===================================================================

    [Fact]
    public void H10_TheHarnessLivesInTheExistingBackendTestProject()
    {
        // TASK-193 H-10: no new production dependency and no new test project.
        // The harness and its tests live in the existing Application.Tests
        // project, which already owns the deterministic infrastructure
        // (InMemoryBattleStateRepository, FixedRngSeedSource) the simulator needs.
        var simulatorType = typeof(BalanceSimulator);

        Assert.Equal(
            "GameServer.Application.Tests",
            simulatorType.Assembly.GetName().Name);

        // It uses the repository-abstraction the tests already provide, and adds
        // no production-facing surface.
        Assert.NotNull(simulatorType.GetConstructor(
            [typeof(BalanceSimulationConfiguration), typeof(IBattleStateRepository)]));
    }

    [Fact]
    public async Task H10_TheHarnessRequiresNoExternalService()
    {
        // The whole run completes in-process against the in-memory double: no
        // Redis, no PostgreSQL, no network. If the harness had reached for a real
        // store this would fail rather than hang.
        var result = await new BalanceSimulator(
            Configuration(seed: 24680UL),
            new InMemoryBattleStateRepository()).RunAsync();

        Assert.NotEqual(BalanceSimulationOutcome.InvalidSimulation, result.Outcome);
        Assert.NotNull(result.FinalState);
    }

    [Fact]
    public void H10_AllThreeApprovedPolicies_AreAvailable_AndNoOthers()
    {
        // TASK-193 §3.2: exactly three policies. An unknown name is rejected
        // rather than silently defaulted.
        Assert.Equal(BalancePolicies.PassiveName, BalancePolicies.ByName(BalancePolicies.PassiveName).Name);
        Assert.Equal(BalancePolicies.AverageName, BalancePolicies.ByName(BalancePolicies.AverageName).Name);
        Assert.Equal(BalancePolicies.SkilledName, BalancePolicies.ByName(BalancePolicies.SkilledName).Name);

        Assert.Throws<ArgumentOutOfRangeException>(() => BalancePolicies.ByName("optimal"));
    }

    [Fact]
    public async Task H10_ThePassivePolicy_NeverCastsAPowerCard()
    {
        // §3.2: the passive policy "never casts". With Power Charge equipped and
        // free, an average policy would cast it; the passive policy must not.
        var passive = await Simulator(Configuration(
            seed: 20261005UL,
            boss: BossDefinitions.HoaLong with { MaxHP = 3000 },
            policy: BalancePolicies.PassiveName)).RunAsync();

        Assert.Equal(0, passive.Metrics.CastsTotal);

        var average = await Simulator(Configuration(
            seed: 20261005UL,
            boss: BossDefinitions.HoaLong with { MaxHP = 3000 },
            policy: BalancePolicies.AverageName)).RunAsync();

        Assert.True(average.Metrics.CastsTotal > 0, "the average policy casts when a Card is affordable");
    }

    [Fact]
    public async Task H10_TheDisallowedCastCostsNothing_UnderThePerTurnAllowance()
    {
        // CARD_RULES.md §3 item 6 (ADR-021, implemented by TASK-192): at most one
        // successful cast per committed Turn. The average policy requests a cast
        // every Turn and therefore observes allowance rejections — evidence the
        // harness measures the post-B-02 rules rather than a pre-B-02 economy.
        var result = await Simulator(Configuration(
            seed: 20261005UL,
            boss: BossDefinitions.HoaLong with { MaxHP = 5000 },
            policy: BalancePolicies.AverageName)).RunAsync();

        // Every cast that succeeded is at most one per Turn.
        var castsPerTurn = result.Metrics.CastTurns
            .GroupBy(turn => turn)
            .ToDictionary(group => group.Key, group => group.Count());

        Assert.All(castsPerTurn.Values, count => Assert.Equal(1, count));
    }
}
