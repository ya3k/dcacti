namespace GameServer.Application.Tests.Balance.TASK309;

using GameServer.Application.Battle;
using GameServer.Application.Cards;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// TASK-309 V-1 (post-implementation) — the <b>in-battle Boss Card cast-frequency
/// analysis</b> for the four migrated Bosses (<c>flame-burst</c>, <c>drain-power</c>,
/// <c>root</c>, <c>thunder-strike</c>), measured through the real production
/// pipeline.
///
/// <para>
/// <b>What is measured, and against what denominator.</b> A battle is created with
/// <see cref="BattleStateService.CreateBattleAsync"/> from a fixed
/// <see cref="BattleSeed"/> and then played with committed Player Swaps until it
/// ends or the documented round cap is reached. Every committed round runs exactly
/// one Boss response phase (<c>BOSS_RULES.md</c> §4.3), so the measurement denominator
/// is the number of <b>response phases</b>, not Turns or battles:
/// </para>
/// <code>
/// cast frequency            = BossCardCast rounds / response phases
/// Power eligibility         = phases whose phase-start Boss Power >= authored cost
/// cooldown eligibility      = phases whose phase-start Boss cooldown == 0
/// both eligible             = phases satisfying both hard gates
/// </code>
/// <para>
/// A round in which the Player's attack reduced the Boss to 0 HP ends before the
/// Boss response (<c>BOSS_RULES.md</c> §5 item 4: no Boss Response), so that round is
/// counted as a player-phase terminal, never as a response phase.
/// </para>
///
/// <para>
/// <b>Where the numbers come from (read-only).</b> Every value is read from the
/// committed state and the ordered events the production stages emitted:
/// </para>
/// <list type="bullet">
/// <item>
/// A phase is a cast when the round's event list contains exactly one
/// <c>BossCardCast</c>, and a board action when it contains exactly one
/// <c>SwapStarted(actor: boss)</c>; neither round form is re-derived or replayed.
/// </item>
/// <item>
/// <b>Phase-start Boss Power</b> is the committed pre-round <c>BossState.Power</c>.
/// The Player action phase writes no Boss resource and step 18b's Boss Passive
/// effects change HP / ATK status / SkillCharge / PassiveProgress only, so the value
/// the 18a selection reads is the committed value entering the round. This is
/// cross-checked on every cast: the committed post-round Power must equal it minus
/// the authored cost exactly.
/// </item>
/// <item>
/// <b>Phase-start cooldown</b> is the decremented value the 18a selection reads
/// (<c>BOSS_RULES.md</c> §6.3: cooldown decrements once per begun Turn, at the
/// pipeline's step 3, before the Boss response): <c>max(0, committed pre-round
/// SkillCooldown - 1)</c>. It is cross-checked on every non-cast response phase
/// (nothing else writes cooldown there, so the committed post-round value must equal
/// it) and on every cast (a cast requires 0).
/// </item>
/// </list>
///
/// <para>
/// <b>Conditions held constant (documented fixture inputs).</b>
/// </para>
/// <list type="bullet">
/// <item>
/// The four MVP Bosses are the real <see cref="BossDefinitions"/> entries, so the
/// approved <c>CastPriorityScore = 85</c> (<c>TASK-309</c> §1 B-1) is the authored
/// value — not a test override — and their real authored cooldowns apply.
/// </item>
/// <item>
/// The four provisioned Boss Card rows come from the suite's single transcription of
/// the TASK-309 approved content (<see cref="ProvisionedBossCardContent"/>) behind
/// the production <see cref="ICardDefinitionLookup"/> seam: costs 40/25/35/40 and the
/// approved effects.
/// </item>
/// <item>
/// The active Pet is Xích Lang at its documented base stats (Element Hỏa, Passive
/// threshold 5); the Player never casts a Card, so the only Player choice is the
/// Swap. The Swap is chosen by the same deterministic scripted heuristic the
/// TASK-193 <c>skilled</c> policy uses for swaps (highest unique matched-cell count,
/// lowest cell-pair tie-break) — a measurement instrument that proposes actions, not
/// a gameplay rule and not a balance opinion.
/// </item>
/// <item>
/// Battles are bounded by <see cref="MaxRoundsPerBattle"/> so a non-terminal game is
/// a recorded stalemate rather than an unbounded run; the bound and the per-boss
/// sample sizes are engineering parameters, recorded in the artifact.
/// </item>
/// </list>
///
/// <para>
/// <b>Relationship to the 10,000-board baseline (do not conflate).</b> The separate
/// reproducible baseline
/// (<c>tasks/artifacts/TASK-309-boss-cast-priority-baseline.json</c>, produced by
/// <c>TASK309BossCastPriorityBoardSimulationTests</c>) measured
/// <c>P(boardScore &lt; 85) = 36.97%</c> on freshly generated initial boards. That is
/// a <b>board-only</b> gate probability at generation time. This analysis measures a
/// different quantity: the actual in-battle cast frequency, where boards have been
/// played, where the cast is gated on Power and cooldown as well as the board score,
/// and where the Boss's board score is whatever the live board offers. The two
/// numbers are not interchangeable, neither one predicts the other, and in
/// particular the baseline's 36.97% is <b>not</b> an expected in-battle cast rate.
/// </para>
///
/// <para>
/// <b>What this test asserts — and what it deliberately does not.</b> Only
/// contractually guaranteed invariants are asserted: fractions lie in [0, 1]; every
/// cast round emits exactly one <c>BossCardCast</c>, no <c>BossSkillCast</c>, and
/// spends exactly the authored Power cost; every response phase resolves exactly one
/// Boss action; cast eligibility implies both hard gates; the eligibility accounting
/// sums to the phase count; and the module's own cross-checks found no violation.
/// <b>No tuned target cast rate is asserted.</b> <c>CastPriorityScore = 85</c> is the
/// operator-approved starting value to monitor, not a globally optimal value, and
/// this analysis authorizes no cast-rate guarantee.
/// </para>
/// </summary>
public class BossCardCastFrequencyAnalysisTests
{
    /// <summary>
    /// The operator-approved Boss cast-priority score the four migrated Bosses carry
    /// (<c>TASK-309</c> §1 B-1). It is the authored definition value, read here only
    /// to assert the fixture is the approved content.
    /// </summary>
    internal const int ApprovedCastPriorityScore = 85;

    /// <summary>
    /// The fixed number of battles played per Boss. It is an engineering sample-size
    /// parameter, not a gameplay value.
    /// </summary>
    internal const int BattlesPerBoss = 40;

    /// <summary>
    /// The fixed maximum number of committed rounds per battle. A battle still alive
    /// at the cap is recorded as a non-terminal (stalemate) outcome; the cap exists
    /// only so a non-terminal game cannot run unbounded.
    /// </summary>
    internal const int MaxRoundsPerBattle = 80;

    /// <summary>
    /// The battle-seed base. Battle <c>b</c> of every Boss uses
    /// <c>FirstSeed + b</c> (b = 0 … <see cref="BattlesPerBoss"/> − 1), so the four
    /// Bosses fight the same 40 generated battle seeds and the run is fully
    /// reproducible (<c>GAME_STATE.md</c> §2.6.1).
    /// </summary>
    internal const ulong FirstSeed = 0x5EED030900000001UL;

    /// <summary>The owning Player of every analysis battle (<c>GAME_STATE.md</c> §2.8).</summary>
    private static readonly PlayerId Owner = new("player_task309_frequency_owner");

    /// <summary>
    /// The active Pet: Xích Lang's MVP Element and Passive at the documented base
    /// stats (<c>COMBAT_RULES.md</c> §1.1). No Card loadout is supplied because the
    /// Player policy never casts a Card — the analysis isolates Boss response phases
    /// from Player Card effects.
    /// </summary>
    private static readonly BattleStateService.PetConfiguration PetConfiguration =
        new(new PetId("pet_instance_task309_frequency"), Element.Hoa, new PassiveId("xich-lang"), PassiveThreshold: 5);

    /// <summary>
    /// The four migrated Boss units under measurement. Each pairs the real MVP Boss
    /// definition (authored score 85, authored cooldown) with the provisioned Boss
    /// Card identity and its approved Power cost.
    /// </summary>
    private static readonly BossFixture[] Bosses =
    [
        new("flame-burst", BossDefinitions.HoaLong, ProvisionedBossCardContent.FlameBurstCardId, AuthoredCost(ProvisionedBossCardContent.FlameBurst())),
        new("drain-power", BossDefinitions.ThuyMa, ProvisionedBossCardContent.DrainPowerCardId, AuthoredCost(ProvisionedBossCardContent.DrainPower())),
        new("root", BossDefinitions.MocYeu, ProvisionedBossCardContent.RootCardId, AuthoredCost(ProvisionedBossCardContent.Root())),
        new("thunder-strike", BossDefinitions.KimLoiVuong, ProvisionedBossCardContent.ThunderStrikeCardId, AuthoredCost(ProvisionedBossCardContent.ThunderStrike())),
    ];

    /// <summary>
    /// The analysis runs once per test process and is shared by every assertion
    /// (including the summary written to the test output).
    /// </summary>
    private static readonly Lazy<Task<Analysis>> CachedAnalysis = new(
        RunAnalysisAsync,
        LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly ITestOutputHelper _output;

    public BossCardCastFrequencyAnalysisTests(ITestOutputHelper output) => _output = output;

    // =======================================================================
    // Fixture identity — the approved content, not a test override
    // =======================================================================

    [Fact]
    public void V1_ContentFixtures_CarryTheApprovedScoresCostsAndCardIdentities()
    {
        // BOSS_RULES.md §4.3 item 5 / TASK-309 §1 B-1: the four migrated Bosses
        // carry the operator-approved authored score 85; the analysis uses those
        // definitions unchanged, so the selection gate it measures is the real one.
        var provisioned = ProvisionedBossCardContent.All.ToDictionary(
            card => card.CardDefinitionId,
            StringComparer.Ordinal);

        foreach (var boss in Bosses)
        {
            Assert.Equal(ApprovedCastPriorityScore, boss.Definition.CastPriorityScore);

            // CARD_RULES.md §1.1 item 3 / ADR-029 §2.5: the Boss Card's identity is
            // the ability's SkillId, its category is BossCard, and its authored
            // PowerCost is the approved cost this analysis uses as the eligibility
            // threshold.
            Assert.Equal(boss.Definition.SkillId, boss.CardId);
            Assert.True(provisioned.TryGetValue(boss.CardId, out var card), $"No provisioned Boss Card for '{boss.CardId}'.");
            Assert.Equal(CardCategory.BossCard, card!.Category);
            Assert.Equal(boss.PowerCost, card.PowerCost);
        }
    }

    // =======================================================================
    // Measured cast frequency and eligibility — contractual invariants only
    // =======================================================================

    [Fact]
    public async Task V1_InBattleCastFrequency_SatisfiesTheContractualInvariants()
    {
        var analysis = await CachedAnalysis.Value;

        foreach (var boss in analysis.Bosses)
        {
            // A phase denominator must exist for any fraction to be meaningful.
            Assert.True(boss.ResponsePhases > 0, $"{boss.Ability}: no Boss response phase was observed.");

            // Every reported fraction is a fraction.
            Assert.InRange(boss.CastFrequency, 0.0, 1.0);
            Assert.InRange(boss.PowerEligibility, 0.0, 1.0);
            Assert.InRange(boss.CooldownEligibility, 0.0, 1.0);
            Assert.InRange(boss.BothEligibility, 0.0, 1.0);

            // Eligibility accounting partitions the phase count (the two hard gates
            // are independent, so `both` is a subset of each; casts are a subset of
            // `both` because a cast is only selectable when both hold).
            Assert.Equal(boss.ResponsePhases, boss.PowerEligible + boss.PowerIneligible);
            Assert.Equal(boss.ResponsePhases, boss.CooldownEligible + boss.CooldownIneligible);
            Assert.Equal(boss.ResponsePhases, boss.BothEligible + boss.NotBothEligible);
            Assert.Equal(boss.ResponsePhases, boss.Casts + boss.NonCasts);
            Assert.True(boss.Casts <= boss.BothEligible, $"{boss.Ability}: {boss.Casts} casts exceed {boss.BothEligible} both-gate-eligible phases.");
            Assert.True(boss.BothEligible <= boss.PowerEligible);
            Assert.True(boss.BothEligible <= boss.CooldownEligible);

            // Committed rounds partition into response phases and player-phase
            // terminals (BOSS_RULES.md §5 item 4), and every battle reaches exactly
            // one recorded outcome (victory, defeat, or the documented round-cap
            // non-terminal).
            Assert.Equal(boss.Rounds, boss.ResponsePhases + boss.PlayerPhaseTerminals);
            Assert.Equal(boss.Battles, boss.Victories + boss.Defeats + boss.Stalemates);

            // The module's own per-phase cross-checks: one Boss action per response
            // phase, exactly one BossCardCast and no BossSkillCast per cast, the
            // authored cost spent once, the authored cooldown set, and the
            // phase-start readings consistent with committed state.
            Assert.True(
                boss.Violations.Count == 0,
                $"{boss.Ability}: {boss.Violations.Count} contractual violation(s): "
                + string.Join(" | ", boss.Violations.Take(5)));
        }

        // The aggregate is the exact sum of the per-Boss measurements, so the
        // aggregate fraction cannot be a different quantity computed another way.
        Assert.Equal(analysis.Bosses.Sum(boss => boss.ResponsePhases), analysis.TotalPhases);
        Assert.Equal(analysis.Bosses.Sum(boss => boss.Casts), analysis.TotalCasts);
        Assert.Equal(analysis.Bosses.Sum(boss => boss.PowerEligible), analysis.TotalPowerEligible);
        Assert.Equal(analysis.Bosses.Sum(boss => boss.CooldownEligible), analysis.TotalCooldownEligible);
        Assert.Equal(analysis.Bosses.Sum(boss => boss.BothEligible), analysis.TotalBothEligible);
        Assert.InRange(analysis.AggregateCastFrequency, 0.0, 1.0);

        // The engineering record (the JSON artifact) needs a stable, readable
        // summary; it is written to the test output only and asserts nothing.
        foreach (var boss in analysis.Bosses)
        {
            _output.WriteLine(
                $"{boss.Ability}: battles={boss.Battles} rounds={boss.Rounds} responsePhases={boss.ResponsePhases} "
                + $"casts={boss.Casts} castFrequency={boss.CastFrequency:F6} "
                + $"powerEligible={boss.PowerEligible} ({boss.PowerEligibility:F6}) "
                + $"cooldownEligible={boss.CooldownEligible} ({boss.CooldownEligibility:F6}) "
                + $"bothEligible={boss.BothEligible} ({boss.BothEligibility:F6}) "
                + $"castShareAmongEligible={(boss.BothEligible == 0 ? 0.0 : (double)boss.Casts / boss.BothEligible):F6} "
                + $"playerPhaseTerminals={boss.PlayerPhaseTerminals} "
                + $"victories={boss.Victories} defeats={boss.Defeats} stalemates={boss.Stalemates} "
                + $"violations={boss.Violations.Count}");
        }

        _output.WriteLine(
            $"aggregate: responsePhases={analysis.TotalPhases} casts={analysis.TotalCasts} "
            + $"castFrequency={analysis.AggregateCastFrequency:F6} "
            + $"powerEligibility={(double)analysis.TotalPowerEligible / analysis.TotalPhases:F6} "
            + $"cooldownEligibility={(double)analysis.TotalCooldownEligible / analysis.TotalPhases:F6} "
            + $"bothEligibility={(double)analysis.TotalBothEligible / analysis.TotalPhases:F6} "
            + $"castShareAmongEligible={(analysis.TotalBothEligible == 0 ? 0.0 : (double)analysis.TotalCasts / analysis.TotalBothEligible):F6}");
    }

    // =======================================================================
    // The measurement driver
    // =======================================================================

    /// <summary>
    /// Plays the fixed battle set through the production pipeline and returns the
    /// read-only measurements.
    /// </summary>
    private static async Task<Analysis> RunAnalysisAsync()
    {
        var results = new List<BossAnalysis>(Bosses.Length);

        foreach (var boss in Bosses)
        {
            var store = new InMemoryBattleStateRepository();
            var cardDefinitions = new TestCardDefinitionLookup().WithProvisionedBossCards();

            // The real production pipeline over the suite's documented in-memory
            // active-state double (REDIS_STATE.md §1–§4); no battleEnd persistence
            // is composed because this analysis records no durable result.
            var service = new BattleStateService(
                store,
                new FixedRngSeedSource(),
                battleResults: null,
                cardDefinitions: cardDefinitions);

            var conversations = new Counters();

            for (var battleIndex = 0; battleIndex < BattlesPerBoss; battleIndex++)
            {
                conversations.Battles++;
                var battleId = $"task309-v1-{boss.Ability}-{battleIndex:D2}";

                // GAME_STATE.md §2.6.1: the seed is explicit so the board stream and
                // every later RNG consumption are reproducible.
                var state = await service.CreateBattleAsync(
                    battleId,
                    Owner,
                    PetConfiguration,
                    boss.Definition,
                    new BattleSeed(FirstSeed + (ulong)battleIndex));

                var attemptedSwaps = new HashSet<int>();
                var roundsThisBattle = 0;
                var terminal = false;

                while (!terminal && roundsThisBattle < MaxRoundsPerBattle)
                {
                    // The Player's only choice: the deterministic swap heuristic of
                    // the TASK-193 `skilled` policy. It proposes an adjacent pair and
                    // the production validator decides whether it commits.
                    var proposal = BalancePolicies.HighestMatchedCellCountSwap(state, attemptedSwaps);

                    if (proposal is not { } swap)
                    {
                        conversations.ExhaustedProposals++;
                        break;
                    }

                    var previous = state;
                    var result = await service.ExecuteSwapAsync(battleId, swap);

                    if (result is null)
                    {
                        conversations.Violations.Add($"{battleId}: the battle record disappeared.");
                        break;
                    }

                    if (result.Value.IsRejected)
                    {
                        attemptedSwaps.Add(BalancePlayerPolicy.PairKey(swap.From, swap.To));
                        conversations.RejectedSwaps++;
                        continue;
                    }

                    attemptedSwaps.Clear();
                    roundsThisBattle++;
                    conversations.Rounds++;

                    state = result.Value.State;

                    var events = result.Value.Events;
                    var won = events.Any(e => e.Type == BattleEventType.BattleWon);
                    var lost = events.Any(e => e.Type == BattleEventType.BattleLost);

                    MeasureResponsePhase(conversations, battleId, roundsThisBattle, boss, previous, state, events, won);

                    if (won)
                    {
                        conversations.Victories++;
                        terminal = true;
                    }
                    else if (lost)
                    {
                        conversations.Defeats++;
                        terminal = true;
                    }
                }

                if (!terminal)
                {
                    conversations.Stalemates++;
                }
            }

            results.Add(conversations.ToAnalysis(boss));
        }

        return new Analysis(results);
    }

    /// <summary>
    /// Reads one committed round's Boss response phase from the delivered state and
    /// events, records the phase classification and the eligibility readings, and
    /// records — rather than asserts — every contractual inconsistency so the test
    /// reports all of them at once.
    /// </summary>
    private static void MeasureResponsePhase(
        Counters counters,
        string battleId,
        int round,
        BossFixture boss,
        BattleState previous,
        BattleState committed,
        IReadOnlyList<BattleEvent> events,
        bool won)
    {
        foreach (var legacy in events.Where(e => e.Type == BattleEventType.BossSkillCast))
        {
            counters.Violations.Add($"{battleId} round {round}: a migrated Boss emitted BossSkillCast ('{legacy.BossSkillCast.SkillId}').");
        }

        var castEvents = events.Where(e => e.Type == BattleEventType.BossCardCast).ToArray();
        var bossSwapStarts = events
            .Where(e => e.Type == BattleEventType.SwapStarted && e.SwapStarted.Actor == DamageParty.Boss)
            .ToArray();

        var bossActions = castEvents.Length + bossSwapStarts.Length;

        if (bossActions == 0)
        {
            // BOSS_RULES.md §5 item 4: a Boss the Player killed takes no response,
            // so this is the only legal no-Boss-action round — and it must be the
            // Player-phase BattleWon, not an unrecorded silent phase.
            if (won)
            {
                counters.PlayerPhaseTerminals++;
                return;
            }

            counters.Violations.Add(
                $"{battleId} round {round}: committed round reached no Boss action and did not end in the player phase.");
            return;
        }

        if (bossActions > 1)
        {
            // BOSS_RULES.md §4.3 item 4 / ADR-029 §2.7: exactly one action per
            // response phase — never a cast and a board action together, and never
            // two casts.
            counters.Violations.Add(
                $"{battleId} round {round}: response phase resolved {bossActions} Boss actions "
                + $"(casts={castEvents.Length}, boardActions={bossSwapStarts.Length}).");
            return;
        }

        counters.ResponsePhases++;

        // Phase-start readings: Boss Power is invariant across the Player phase and
        // the 18b Passives (they write HP / ATK status / SkillCharge / PassiveProgress,
        // never Power), and the cooldown the 18a selection reads is step 3's
        // once-per-begun-Turn decrement (BOSS_RULES.md §6.3).
        var phaseStartPower = previous.BossState.Power;
        var phaseStartCooldown = Math.Max(0, previous.BossState.SkillCooldown - 1);
        var powerEligible = phaseStartPower >= boss.PowerCost;
        var cooldownEligible = phaseStartCooldown == 0;

        if (powerEligible)
        {
            counters.PowerEligible++;
        }

        if (cooldownEligible)
        {
            counters.CooldownEligible++;
        }

        if (powerEligible && cooldownEligible)
        {
            counters.BothEligible++;
        }

        if (castEvents.Length == 1)
        {
            counters.Casts++;
            var cast = castEvents[0];

            if (cast.BossCardCast.CardId != boss.CardId)
            {
                counters.Violations.Add(
                    $"{battleId} round {round}: BossCardCast cardId '{cast.BossCardCast.CardId}' is not the authored '{boss.CardId}'.");
            }

            if (cast.BossCardCast.SourceId != boss.Definition.BossId.Value)
            {
                counters.Violations.Add(
                    $"{battleId} round {round}: BossCardCast sourceId '{cast.BossCardCast.SourceId}' is not '{boss.Definition.BossId.Value}'.");
            }

            if (!powerEligible)
            {
                counters.Violations.Add(
                    $"{battleId} round {round}: cast was selected with phase-start Power {phaseStartPower} < authored cost {boss.PowerCost}.");
            }

            if (!cooldownEligible)
            {
                counters.Violations.Add(
                    $"{battleId} round {round}: cast was selected with phase-start cooldown {phaseStartCooldown} != 0.");
            }

            // "Each cast spends exactly the authored cost": the committed Boss
            // Power is the phase-start value minus the one authored spend, with no
            // second mutation in the phase (a cast resolves no board action).
            var expectedPower = phaseStartPower - boss.PowerCost;
            if (committed.BossState.Power != expectedPower)
            {
                counters.Violations.Add(
                    $"{battleId} round {round}: committed Boss Power {committed.BossState.Power} != phase-start {phaseStartPower} - cost {boss.PowerCost} ({expectedPower}).");
            }

            // The authored cooldown is set in the same write-back.
            if (committed.BossState.SkillCooldown != boss.CooldownTurns)
            {
                counters.Violations.Add(
                    $"{battleId} round {round}: committed cooldown {committed.BossState.SkillCooldown} != authored {boss.CooldownTurns}.");
            }
        }
        else
        {
            // The board action resolves no cast; cooldown is written only by a cast,
            // so the committed value must equal the phase-start value, and the Boss
            // Power can only have grown by its OWN board action's POWER matches.
            if (committed.BossState.SkillCooldown != phaseStartCooldown)
            {
                counters.Violations.Add(
                    $"{battleId} round {round}: non-cast phase moved cooldown {phaseStartCooldown} -> {committed.BossState.SkillCooldown}.");
            }

            if (committed.BossState.Power < phaseStartPower)
            {
                counters.Violations.Add(
                    $"{battleId} round {round}: non-cast phase reduced Boss Power {phaseStartPower} -> {committed.BossState.Power}.");
            }

            if (committed.BossState.Power > ResourceGenerator.MaxPower)
            {
                counters.Violations.Add(
                    $"{battleId} round {round}: Boss Power {committed.BossState.Power} exceeds the documented 0–100 range.");
            }
        }
    }

    /// <summary>
    /// The measured outcome of the fixed battle set: per-Boss analyses whose
    /// aggregate is their exact sum.
    /// </summary>
    private sealed record Analysis(IReadOnlyList<BossAnalysis> Bosses)
    {
        public int TotalPhases => Bosses.Sum(boss => boss.ResponsePhases);

        public int TotalCasts => Bosses.Sum(boss => boss.Casts);

        public int TotalPowerEligible => Bosses.Sum(boss => boss.PowerEligible);

        public int TotalCooldownEligible => Bosses.Sum(boss => boss.CooldownEligible);

        public int TotalBothEligible => Bosses.Sum(boss => boss.BothEligible);

        public double AggregateCastFrequency => (double)TotalCasts / TotalPhases;
    }

    /// <summary>
    /// One Boss's measured counts. Fractions are computed from the counts only —
    /// no gameplay value is recomputed anywhere in this file.
    /// </summary>
    private sealed record BossAnalysis(
        string Ability,
        string CardId,
        int AuthoredCost,
        int AuthoredCooldown,
        int Battles,
        int Rounds,
        int ResponsePhases,
        int Casts,
        int PowerEligible,
        int CooldownEligible,
        int BothEligible,
        int PlayerPhaseTerminals,
        int Victories,
        int Defeats,
        int Stalemates,
        IReadOnlyList<string> Violations)
    {
        public int NonCasts => ResponsePhases - Casts;

        public int PowerIneligible => ResponsePhases - PowerEligible;

        public int CooldownIneligible => ResponsePhases - CooldownEligible;

        public int NotBothEligible => ResponsePhases - BothEligible;

        public double CastFrequency => (double)Casts / ResponsePhases;

        public double PowerEligibility => (double)PowerEligible / ResponsePhases;

        public double CooldownEligibility => (double)CooldownEligible / ResponsePhases;

        public double BothEligibility => (double)BothEligible / ResponsePhases;
    }

    /// <summary>The mutable per-Boss accumulation, converted to a <see cref="BossAnalysis"/>.</summary>
    private sealed class Counters
    {
        public int Battles;
        public int Rounds;
        public int ResponsePhases;
        public int Casts;
        public int PowerEligible;
        public int CooldownEligible;
        public int BothEligible;
        public int PlayerPhaseTerminals;
        public int Victories;
        public int Defeats;
        public int Stalemates;
        public int RejectedSwaps;
        public int ExhaustedProposals;
        public List<string> Violations { get; } = [];

        public BossAnalysis ToAnalysis(BossFixture boss) => new(
            boss.Ability,
            boss.CardId,
            boss.PowerCost,
            boss.CooldownTurns,
            Battles,
            Rounds,
            ResponsePhases,
            Casts,
            PowerEligible,
            CooldownEligible,
            BothEligible,
            PlayerPhaseTerminals,
            Victories,
            Defeats,
            Stalemates,
            Violations);
    }

    /// <summary>
    /// One Boss unit under measurement: the real MVP definition (authored score and
    /// cooldown), the provisioned Boss Card identity, and its approved Power cost.
    /// </summary>
    private sealed record BossFixture(
        string Ability,
        BossDefinition Definition,
        string CardId,
        int PowerCost)
    {
        public int CooldownTurns => Definition.SkillCooldownTurns;
    }

    /// <summary>
    /// Reads the authored Power cost off the provisioned Boss Card instead of
    /// retyping an approved value — the content fixture remains the single
    /// transcription (<see cref="ProvisionedBossCardContent"/>), and a transcription
    /// change fails the fixture-identity test above rather than silently retuning
    /// this analysis.
    /// </summary>
    private static int AuthoredCost(CardDefinition card) => card.PowerCost;
}
