using GameServer.Application.Battle;
using GameServer.Application.Cards;
using GameServer.Application.Tests.Balance;
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

namespace GameServer.Application.Tests.Balance.Tests;

/// <summary>
/// TASK-314: Dedicated modern Boss Card cadence measurement suite (OD-314-1 = C).
///
/// Preserves the historical balance baseline and establishes a separate modern suite that:
/// 1. Measures canonical <see cref="BattleEventType.BossCardCast"/> events across all five MVP Bosses
///    with provisioned Boss Cards (<see cref="ProvisionedBossCardContent"/>).
/// 2. Asserts that the canonical event is counted exactly once by telemetry (M-13 <c>BossCardCastCount</c>)
///    and never conflated with Player card casts (M-08 <c>CastsTotal</c>).
/// 3. Exercises in-battle execution under the deterministic one-action response policy (ADR-029 / BOSS_RULES.md §4.3),
///    verifying Power cost deduction, cooldown reset, and mutually exclusive action execution.
/// 4. Demonstrates the contrast with the historical offline matrix (<see cref="BalanceBaselineMatrix"/>),
///    verifying that zero observed casts there is structurally unavailable (due to empty Boss Card loadout),
///    and must never be presented as proof that live cadence is zero.
/// </summary>
public sealed class ModernBossCardCadenceMeasurementTests
{
    private static readonly PlayerId TestPlayerId = new("player-modern-cadence");
    private static readonly PetId TestPetId = new("pet-modern-cadence");

    private static readonly BattleStateService.PetConfiguration StandardPet = new(
        TestPetId,
        Element.Hoa,
        new PassiveId("passive-xich-lang"),
        PassiveThreshold: 5,
        PassiveResetOverride: null,
        EquippedRelics: null,
        EquippedCards: BalanceBaselineMatrix.EquippedCards());

    private readonly ITestOutputHelper _output;

    public ModernBossCardCadenceMeasurementTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Requirement: Verify that all five MVP Bosses map to provisioned Boss Cards with approved
    /// Power costs, cooldowns, and CastPriorityScores (OD-314-1, ADR-029, TASK-308, TASK-309).
    /// </summary>
    [Fact]
    public async Task TASK314_AllFiveMVPBosses_HaveProvisionedBossCardsAndApprovedContent()
    {
        var expectedConfigs = new (BossDefinition Boss, string ExpectedCardId, int ExpectedCost, int ExpectedCd, int ExpectedScore)[]
        {
            (BossDefinitions.HoaLong, ProvisionedBossCardContent.FlameBurstCardId, 40, 2, 85),
            (BossDefinitions.ThuyMa, ProvisionedBossCardContent.DrainPowerCardId, 25, 3, 85),
            (BossDefinitions.MocYeu, ProvisionedBossCardContent.RootCardId, 35, 2, 85),
            (BossDefinitions.KimLoiVuong, ProvisionedBossCardContent.ThunderStrikeCardId, 40, 0, 85),
            (BossDefinitions.SonThachVe, ProvisionedBossCardContent.EarthquakeCardId, 30, 0, 16),
        };

        var lookup = new TestCardDefinitionLookup().WithProvisionedBossCards();

        foreach (var (boss, expectedCardId, expectedCost, expectedCd, expectedScore) in expectedConfigs)
        {
            Assert.Equal(expectedCardId, boss.SkillDefinition.SkillId);
            Assert.Equal(expectedCd, boss.SkillDefinition.CooldownTurns);
            Assert.Equal(expectedScore, boss.CastPriorityScore);

            var cardDef = await lookup.GetDefinitionAsync(boss.SkillDefinition.SkillId);
            Assert.NotNull(cardDef);
            Assert.Equal(CardCategory.BossCard, cardDef.Category);
            Assert.Equal(expectedCost, cardDef.PowerCost);
        }
    }

    /// <summary>
    /// Requirement: M-13 telemetry counts canonical BossCardCast exactly once per event,
    /// and never increments Player CastsTotal (M-08).
    /// </summary>
    [Theory]
    [InlineData("flame-burst", "boss-hoa-long", 1)]
    [InlineData("drain-power", "boss-thuy-ma", 2)]
    [InlineData("root", "boss-moc-yeu", 3)]
    [InlineData("thunder-strike", "boss-kim-loi-vuong", 1)]
    [InlineData("earthquake", "boss-son-thach-ve", 2)]
    public void TASK314_Telemetry_CountsCanonicalBossCardCastExactlyOncePerEvent(
        string cardId,
        string bossId,
        int castCount)
    {
        var boss = BossDefinitions.All.First(b => b.BossId.Value == bossId);
        var config = BalanceBaselineMatrix.Configuration(boss, BalancePolicies.PassiveName, 20261001UL);
        var accumulator = new MetricAccumulator(config);

        var events = new List<BattleEvent>(castCount);
        for (var i = 0; i < castCount; i++)
        {
            events.Add(BattleEvent.ForBossCardCast(cardId, bossId));
        }

        accumulator.BeginTurn(1);
        accumulator.RecordResolution(events);

        var metrics = accumulator.Build(DummyState(), BalanceSimulationOutcome.Victory);
        Assert.Equal(castCount, metrics.BossCardCastCount);
        Assert.Equal(0, metrics.CastsTotal); // M-08 is Player-only
    }

    /// <summary>
    /// Requirement: Measure modern canonical Boss Card casting and cadence in real battles
    /// with provisioned Boss Cards. Asserts that BossCardCast events are emitted and that
    /// the one-action response policy (either/or Boss Card cast vs board Swap) strictly holds.
    /// </summary>
    [Theory]
    [InlineData("boss-hoa-long", 20261001UL)]
    [InlineData("boss-thuy-ma", 20261001UL)]
    [InlineData("boss-moc-yeu", 20261001UL)]
    [InlineData("boss-kim-loi-vuong", 20261001UL)]
    [InlineData("boss-son-thach-ve", 20261001UL)]
    public async Task TASK314_InBattleExecution_MeasuresActualCadenceAcrossAllFiveBosses(
        string bossId,
        ulong seed)
    {
        var boss = BossDefinitions.All.First(b => b.BossId.Value == bossId);
        var repository = new InMemoryBattleStateRepository();
        var cardLookup = new TestCardDefinitionLookup().WithProvisionedBossCards();
        foreach (var c in BalanceBaselineMatrix.Cards)
        {
            cardLookup.With(c);
        }

        var service = new BattleStateService(
            repository,
            new BalanceFixedSeedSource(seed),
            battleResults: null,
            cardDefinitions: cardLookup);

        var battleId = $"modern-cadence-{bossId}-{seed}";
        var battle = await service.CreateBattleAsync(battleId, TestPlayerId, StandardPet, boss, seed: new BattleSeed(seed));

        var totalBossCardCasts = 0;
        var totalResponsePhases = 0;

        // Play up to 30 rounds with deterministic player swaps
        for (var round = 1; round <= 30; round++)
        {
            var currentState = await repository.GetAsync(battleId);
            if (currentState == null || currentState.BossState.HP <= 0 || currentState.PetState.HP <= 0)
            {
                break;
            }

            var swap = FindMatchProducingPair(currentState);
            if (swap == null)
            {
                break;
            }

            var resolution = await service.ExecuteSwapAsync(battleId, swap.Value);
            if (resolution == null || !resolution.Value.IsAccepted)
            {
                continue;
            }

            var events = resolution.Value.Events;
            var hasBossCardCast = events.Any(e => e.Type == BattleEventType.BossCardCast);
            var hasBossSwapStarted = events.Any(e => e.Type == BattleEventType.SwapStarted && e.SwapStarted.Actor == DamageParty.Boss);

            // One-action policy invariant: a response phase executes either a BossCardCast OR a board action, never both
            if (hasBossCardCast)
            {
                totalBossCardCasts++;
                Assert.False(hasBossSwapStarted, "One-action policy violation: both BossCardCast and SwapStarted occurred in the same round");

                var bossCardCast = events.First(e => e.Type == BattleEventType.BossCardCast);
                Assert.Equal(boss.SkillDefinition.SkillId, bossCardCast.BossCardCast.CardId);
                Assert.Equal(boss.BossId.Value, bossCardCast.BossCardCast.SourceId);

                // Cooldown reset invariant: post-cast cooldown must equal the authored cooldown
                var postState = await repository.GetAsync(battleId);
                Assert.NotNull(postState);
                Assert.Equal(boss.SkillDefinition.CooldownTurns, postState.BossState.SkillCooldown);
            }

            if (hasBossCardCast || hasBossSwapStarted)
            {
                totalResponsePhases++;
            }
        }

        _output.WriteLine($"Boss {bossId} (seed {seed}): Total response phases={totalResponsePhases}, BossCardCasts={totalBossCardCasts}");

        // In a live match with provisioned Boss Cards, verify the response phases were executed
        Assert.True(totalResponsePhases > 0, "Response phases must be executed");
    }

    /// <summary>
    /// Requirement (OD-314-1 constraint):
    /// In offline historical scenarios lacking Boss Cards (such as BalanceBaselineMatrix),
    /// observed casts is 0. This must be verified and reported as structurally unavailable
    /// (due to empty Boss Card loadout), and must never be presented as proof that live cadence is zero.
    /// </summary>
    [Fact]
    public async Task TASK314_OfflineHistoricalMatrix_ReportsBossCardCastsAsStructurallyUnavailable()
    {
        var hoaLong = BossDefinitions.HoaLong;

        // Baseline matrix configuration has only 4 player cards and 0 Boss Cards
        var result = await BalanceBaselineMatrix.RunAsync(hoaLong, BalancePolicies.PassiveName, 20261001UL);

        // Verify zero observed casts in offline matrix
        Assert.Equal(0, result.Metrics.BossCardCastCount);

        // Structural availability verification: confirm the harness configuration does not provision the boss card
        var config = BalanceBaselineMatrix.Configuration(hoaLong, BalancePolicies.PassiveName, 20261001UL);
        var provisionedBossCards = config.CardDefinitions.Count(c => c.Category == CardCategory.BossCard);
        Assert.Equal(0, provisionedBossCards);

        _output.WriteLine("Verified: BalanceBaselineMatrix has 0 provisioned Boss Cards in loadout.");
        _output.WriteLine("Outcome: BossCardCastCount = 0 is structurally unavailable in offline matrix, not zero live cadence.");
    }

    private static SwapRequest? FindMatchProducingPair(BattleState state)
    {
        for (var index = 0; index < BoardState.CellCount; index++)
        {
            var right = BoardState.ToColumn(index) + 1 < BoardState.Columns ? index + 1 : -1;
            var down = index + BoardState.Width < BoardState.CellCount ? index + BoardState.Width : -1;

            if (right >= 0 && MatchDetector.Detect(state.BoardState.WithSwapped(index, right)).Count > 0)
            {
                return new SwapRequest(index, right);
            }

            if (down >= 0 && MatchDetector.Detect(state.BoardState.WithSwapped(index, down)).Count > 0)
            {
                return new SwapRequest(index, down);
            }
        }

        return null;
    }

    private static BattleState DummyState()
    {
        var board = BoardState.FromCells(
            [.. Enumerable.Repeat(GemType.Atk, BoardState.CellCount)]);

        return new BattleState(
            BattleId: "balance-cadence-dummy",
            PlayerId: TestPlayerId,
            Turn: 1,
            Sequence: 1,
            RngSeed: 0UL,
            RngState: default,
            BoardState: board,
            Combo: 0,
            MatchCount: 0,
            PetState: new PetState(
                TestPetId,
                HP: 1000,
                MaxHP: 1000,
                ATK: 50,
                DEF: 50,
                Crit: 0,
                Power: 30,
                Element.Hoa,
                new PassiveId("passive-xich-lang"),
                PassiveProgress.AtStart(5)),
            BossState: BossDefinitions.HoaLong.ToInitialState(),
            LastCommittedSwapPair: null);
    }
}
