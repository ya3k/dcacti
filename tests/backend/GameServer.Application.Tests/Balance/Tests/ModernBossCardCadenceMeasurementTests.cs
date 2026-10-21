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
/// TASK-318 strengthens its in-battle evidence from "a response phase occurred" to
/// "a Boss Card cast actually resolved".
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
/// 5. (**TASK-318**) Proves an actual cast from one committed round, at the real
///    authored cast-priority score and the provisioned content, so the suite can no
///    longer pass on a response phase that only resolved a board action. Sơn Thạch
///    Vệ is the case that needs the proof most: its authored score is the one value
///    that can only win against the documented minimum board score.
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
    /// TASK-318: the Sơn Thạch Vệ cast is proven from a single committed round, so
    /// the fixture cannot pass on a response phase that merely resolved a board
    /// action.
    ///
    /// <code>
    /// Rule (BOSS_RULES.md §4.3 steps 4–6, item 5; §6.3.1 item 4; CARD_RULES.md §3
    ///       item 9; GAME_EVENTS.md §2; SIGNALR_PROTOCOL.md §3.2.30)
    ///  ↓
    /// Scenario (Given Sơn Thạch Vệ at its authored cast-priority score 16, its
    ///           provisioned earthquake Boss Card, enough Power and a zero cooldown,
    ///           and a committed board whose deterministic board action scores the
    ///           documented minimum 15, When one round is committed, Then the Boss
    ///           casts exactly once, and the cast carries the canonical identity,
    ///           costs the authored Power, sets the authored cooldown, and is the
    ///           response phase's only action)
    ///  ↓
    /// Test
    /// </code>
    ///
    /// <b>No gameplay value is overridden.</b> The definition is the real
    /// <see cref="BossDefinitions.SonThachVe"/> — no cast-priority override, no
    /// HP/ATK/Passive override, no cooldown change — and the content is the
    /// provisioned <c>earthquake</c> row (<see cref="ProvisionedBossCardContent"/>).
    /// The two documented test-side substitutions are the in-memory active-state
    /// store and the fixture's own board/stream (<c>GAME_STATE.md</c> §2.6.2), plus
    /// the Boss's Power and cooldown, which are the scenario's "Given" rather than a
    /// selection bypass — the selection itself is the production policy's.
    /// </summary>
    [Fact]
    public async Task TASK318_SonThachVe_ShouldCastEarthquakeAtTheAuthoredScoreAndKeepTheOneActionPolicy()
    {
        // BOSS_RULES.md §6.1/§6.3: the real MVP definition, transcribed, unchanged.
        var boss = BossDefinitions.SonThachVe;

        // The authored values this scenario is about — read from the content
        // carriers, never restated as magic numbers.
        var authoredScore = boss.CastPriorityScore;
        var authoredCooldown = boss.SkillDefinition.CooldownTurns;
        var provisionedCard = ProvisionedBossCardContent.Earthquake();

        Assert.NotNull(authoredScore);
        Assert.Equal(16, authoredScore.Value);
        Assert.Equal(0, authoredCooldown);
        Assert.Equal(ProvisionedBossCardContent.EarthquakeCardId, boss.SkillDefinition.SkillId);
        Assert.Equal(ProvisionedBossCardContent.EarthquakePowerCost, provisionedCard.PowerCost);

        var (service, store) = NewProvisionedHarness();
        var castRound = await CommitCraftedCastRoundAsync(service, store, boss);

        // -------------------------------------------------------------------
        // The cast HAPPENED. This is the assertion TASK-314's suite lacked: a
        // response phase alone is not evidence of a cast.
        // -------------------------------------------------------------------
        var casts = castRound.Events.Where(e => e.Type == BattleEventType.BossCardCast).ToArray();
        Assert.Single(casts);

        var observedCast = casts[0].BossCardCast;

        // GAME_EVENTS.md §2 / BOSS_RULES.md §4.2: the canonical event carries the
        // Boss Card identity (equal to the ability SkillId) and the Boss's
        // canonical technical Identity.
        Assert.Equal(ProvisionedBossCardContent.EarthquakeCardId, observedCast.CardId);
        Assert.Equal("boss-son-thach-ve", observedCast.SourceId);

        // BOSS_RULES.md §4.3 item 5: the cast was authorized by the authored score
        // strictly out-scoring the board action — not by an override. The board
        // action the policy compared against is production output.
        Assert.Equal(MinimumBoardActionScore, castRound.BoardActionScore);
        Assert.True(
            authoredScore.Value > castRound.BoardActionScore,
            $"the authored score {authoredScore.Value} must strictly out-score the board action "
            + $"{castRound.BoardActionScore} (BOSS_RULES.md §4.3 item 5)");

        // BOSS_RULES.md §6.3.1 item 4 / CARD_RULES.md §3 item 9: the authored cost
        // was deducted exactly once. The Player's cascade also banks Boss Power
        // through the Boss's own actor-relative generation only on a Boss board
        // action — this phase took none — so the round's Power movement above the
        // Player-side generation is the cast's authored spend alone.
        var powerSpentByCast = PinnedBossPower + castRound.Events
            .Where(e => e.Type == BattleEventType.PowerChanged && e.PowerChanged.Source == PowerChangeSource.Boss)
            .Sum(e => e.PowerChanged.Delta)
            - castRound.State.BossState.Power;

        Assert.Equal(ProvisionedBossCardContent.EarthquakePowerCost, powerSpentByCast);

        // The authored cooldown was set in the same write-back; the cooldown gate is
        // 0 (BOSS_RULES.md §6.3.1 item 4), so the post-round value is unambiguous.
        Assert.Equal(authoredCooldown, castRound.State.BossState.SkillCooldown);

        // The provisioned Shield effect resolved against the Boss's own state
        // (BOSS_RULES.md §6.3.1 item 4 — no direct damage).
        var shield = Assert.Single(castRound.State.BossState.ActiveStatusEffects, e => e.Type == StatusEffectType.Shield);
        Assert.Equal(StatusEffectSource.Boss, shield.Source);
        Assert.Equal(EarthquakeShieldMagnitude, (int)shield.Magnitude);

        // -------------------------------------------------------------------
        // Telemetry: M-13 counts each canonical cast exactly once; M-08 stays
        // player-card-only.
        // -------------------------------------------------------------------
        var accumulator = new MetricAccumulator(
            BalanceBaselineMatrix.Configuration(boss, BalancePolicies.PassiveName, 20261001UL));

        accumulator.BeginTurn(castRound.State.Turn);
        accumulator.RecordResolution(castRound.Events);

        var metrics = accumulator.Build(castRound.State, BalanceSimulationOutcome.Victory);

        Assert.Equal(1, metrics.BossCardCastCount);
        Assert.Equal(0, metrics.CastsTotal);
        Assert.Empty(metrics.CastsByCard);

        // The counting split is exact — one canonical cast is one M-13 increment,
        // never one more and never one less.
        Assert.Equal(casts.Length, metrics.BossCardCastCount);

        // -------------------------------------------------------------------
        // BOSS_RULES.md §4.3 item 4: a cast-selected phase resolves no Boss board
        // action, so the Boss forgoes its 18d attack and generates no board
        // resources that phase. Exactly ONE action resolved in this phase: the
        // cast — there is no second Boss action to pair with it.
        //
        // §1 (GAME_EVENTS.md) fixes the boundary pair as SwapStarted followed by
        // SwapResolved, and §1.1 item 9 / §3.2.29 give each board cycle its own
        // MatchedGemSummary. The Player's action contributes exactly one of each;
        // a Boss board action would add a second. Counting them is therefore the
        // direct statement of "one action", independent of actor labelling.
        // -------------------------------------------------------------------
        Assert.DoesNotContain(
            castRound.Events,
            e => e.Type == BattleEventType.SwapStarted && e.SwapStarted.Actor == DamageParty.Boss);

        Assert.Single(castRound.Events, e => e.Type == BattleEventType.SwapStarted);
        Assert.Single(castRound.Events, e => e.Type == BattleEventType.SwapResolved);
        Assert.Single(castRound.Events, e => e.Type == BattleEventType.MatchedGemSummary);

        Assert.Equal(
            DamageParty.Player,
            Assert.Single(castRound.Events, e => e.Type == BattleEventType.SwapStarted).SwapStarted.Actor);

        // The Boss's own Power gain in this phase was zero: it resolved no board
        // cycle, so the shared actor-relative generation rule produced nothing for
        // it (Boss Power only moves by the cast's spend, which emits no
        // PowerChanged — TASK-307 §2.2 C-6). The Player's own generation is
        // untouched by this assertion.
        Assert.DoesNotContain(
            castRound.Events,
            e => e.Type == BattleEventType.PowerChanged && e.PowerChanged.Source == PowerChangeSource.Boss);

        // The response phase's ONLY action is the cast: no Boss-sourced damage was
        // resolved in it.
        Assert.DoesNotContain(
            castRound.Events,
            e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Boss);

        _output.WriteLine(
            $"Sơn Thạch Vệ cast '{observedCast.CardId}' → '{observedCast.SourceId}' at authored score "
            + $"{authoredScore.Value} > board score {castRound.BoardActionScore}; "
            + $"M-13 BossCardCastCount=1, M-08 CastsTotal=0, cooldown={castRound.State.BossState.SkillCooldown}.");
    }

    /// <summary>
    /// TASK-318: the same crafted fixture replayed produces the same committed round
    /// and the same cast — the evidence is deterministic, not a one-off.
    /// </summary>
    [Fact]
    public async Task TASK318_SonThachVeCastFixture_ShouldBeDeterministic()
    {
        var boss = BossDefinitions.SonThachVe;

        var (firstService, firstStore) = NewProvisionedHarness();
        var first = await CommitCraftedCastRoundAsync(firstService, firstStore, boss);

        var (secondService, secondStore) = NewProvisionedHarness();
        var second = await CommitCraftedCastRoundAsync(secondService, secondStore, boss);

        var firstCast = Assert.Single(first.Events, e => e.Type == BattleEventType.BossCardCast).BossCardCast;
        var secondCast = Assert.Single(second.Events, e => e.Type == BattleEventType.BossCardCast).BossCardCast;

        Assert.Equal(firstCast, secondCast);
        Assert.Equal(first.State.BossState.Power, second.State.BossState.Power);
        Assert.Equal(first.State.BossState.SkillCooldown, second.State.BossState.SkillCooldown);
        Assert.Equal(first.State.Sequence, second.State.Sequence);
    }

    /// <summary>
    /// The documented minimum board-action score: a 3-cell DEF match
    /// (3 × the DEF weight 5 — <c>BOSS_RULES.md</c> §4.3 item 5). It is the only
    /// board score the authored <c>16</c> can strictly beat.
    /// </summary>
    private const int MinimumBoardActionScore = 15;

    /// <summary>
    /// The Boss Power the cast fixture replenishes before the round. It is above the
    /// authored <c>earthquake</c> cost so the cast gate is satisfied whatever the
    /// Player's cascade banks on the way in; the round's Power movement is asserted
    /// as a delta, never as this fixture value.
    /// </summary>
    private const int PinnedBossPower = 100;

    /// <summary>The provisioned <c>earthquake</c> Shield magnitude (TASK-308 §1).</summary>
    private const int EarthquakeShieldMagnitude = 150;

    /// <summary>
    /// The fixture stream. It is fixture state (<c>GAME_STATE.md</c> §2.6.2), not a
    /// gameplay value: it selects which deterministic spawn stream the crafted board's
    /// cleared cells are refilled from.
    /// </summary>
    private const ulong FixtureSeed = 3UL;

    /// <summary>
    /// The Player's committed Swap of the crafted fixture — cells <c>3</c> and
    /// <c>4</c>, a 3-cell DEF match on the top row.
    /// </summary>
    private static readonly SwapRequest CraftedSwap = new(3, 4);

    /// <summary>
    /// A committed round in which Sơn Thạch Vệ's Boss Card was the response
    /// phase's one action, together with the board-action evidence that authorized
    /// it.
    /// </summary>
    private sealed record BossCastRound(
        int BoardActionScore,
        IReadOnlyList<BattleEvent> Events,
        BattleState State);

    /// <summary>
    /// The post-migration composition: the production service over the in-memory
    /// store, with the provisioned Boss Card content behind the creation-time form
    /// resolution (<see cref="TestCardDefinitionLookup.WithProvisionedBossCards"/>).
    /// The composition matches <c>BossCardTestHarness.CreateProvisioned</c>, so the
    /// fixture drives the same content seam the other Boss Card runtime suites use.
    /// </summary>
    private static (BattleStateService Service, InMemoryBattleStateRepository Store) NewProvisionedHarness()
    {
        var store = new InMemoryBattleStateRepository();

        return (
            new BattleStateService(
                store,
                new FixedRngSeedSource(),
                cardDefinitions: new TestCardDefinitionLookup().WithProvisionedBossCards()),
            store);
    }

    /// <summary>
    /// Commits the crafted fixture's one round and returns it with the board-action
    /// evidence that authorized the cast.
    ///
    /// <code>
    /// pre-swap board (stable 8x8 pattern, exactly the legal swaps the fixture needs)
    ///         ↓  the Player commits the 3-cell DEF Swap (cells 3–4)
    /// post-Player board, whose enumerable first-pass candidates score the documented
    ///   minimum 15 (3 DEF cells × the DEF weight 5)
    ///         ↓  the Boss's deterministic enumeration + scoring (BossAiEngine)
    /// Boss board action = the lowest-index 15-point candidate
    ///         ↓  the policy's tie-independent comparison (BOSS_RULES.md §4.3 steps 5–6)
    /// authored cast-priority score 16 > 15  →  the CAST is selected, not the board action
    /// </code>
    ///
    /// <b>Why a crafted board is required, and why it is not a cheat.</b>
    /// A first-pass Match always clears at least three same-type cells, and the
    /// lightest Gem weight is DEF's 5 (<see cref="BossMoveScoringWeights"/>), so a
    /// board action never scores below <see cref="MinimumBoardActionScore"/> = 15.
    /// The authored score of <c>16</c> therefore out-scores a board action only when
    /// a legal candidate scores exactly 15 — a configuration the generator almost
    /// never produces (measured over 500 generated boards and ~13.7k evaluated
    /// candidates, the observed minimum selected score was 30). Under this content
    /// Sơn Thạch Vệ does cast — but on the game's own generated boards the cast is a
    /// near-exceptional case, which is a cadence observation for the balance owner
    /// and not something this test may manufacture by changing a value.
    ///
    /// <b>No gameplay value is decided here.</b> The board is fixture state
    /// (<c>GAME_STATE.md</c> §2.6.2); the definition is the real
    /// <see cref="BossDefinitions.SonThachVe"/> with no cast-priority, HP, ATK, or
    /// Passive override; the content is the provisioned <c>earthquake</c> row; and the
    /// selection is made by the production <see cref="BossActionPolicy"/> from the
    /// production <see cref="BossAiEngine"/> enumeration. The test only supplies the
    /// "Given" and then reads back what the rules did.
    /// </summary>
    private static async Task<BossCastRound> CommitCraftedCastRoundAsync(
        BattleStateService service,
        InMemoryBattleStateRepository store,
        BossDefinition boss)
    {
        const string battleId = "task318-son-thach-ve-authored-score-cast";

        var created = await service.CreateBattleAsync(
            battleId,
            TestPlayerId,
            StandardPet,
            boss,
            seed: new BattleSeed(FixtureSeed));

        // The scenario's "Given": enough Boss Power to clear the authored cost and a
        // zero cooldown, so the only question the round decides is which ONE action
        // the policy selects. Both are written through the store's own Sequence
        // compare-and-set, so the round starts from a state the game genuinely stores.
        var pinned = created with
        {
            BoardState = CraftedPreSwapBoard(),
            BossState = created.BossState with { Power = PinnedBossPower, SkillCooldown = 0 },
        };

        Assert.True(await store.TryUpdateAsync(pinned, created.Sequence));

        // Reproduce the Boss's own board action exactly as the production round does
        // (BattleStateService step 9): the Player's cascade first, then the Boss's
        // deterministic enumeration and scoring over the board that cascade left.
        var playerResolution = CascadeResolver.Resolve(
            pinned.BoardState.WithSwapped(CraftedSwap.From, CraftedSwap.To),
            new Pcg32(pinned.RngState.State, pinned.RngState.Increment));

        var bossBoardAction = BossAiEngine.SelectMove(
            playerResolution.Board,
            new Pcg32(playerResolution.RngState.State, playerResolution.RngState.Increment));

        Assert.True(bossBoardAction.LegalCandidatesCount > 0);
        Assert.Equal(MinimumBoardActionScore, bossBoardAction.Score);

        // The cast is authorized by the documented selection inequality itself
        // (BOSS_RULES.md §4.3 steps 5–6): the authored score must STRICTLY exceed the
        // board action's, or the board action wins.
        Assert.True(
            boss.CastPriorityScore > bossBoardAction.Score,
            $"the authored cast-priority score {boss.CastPriorityScore} must strictly out-score the "
            + $"Boss board action {bossBoardAction.Score} for the cast to be selected");

        var round = await service.ExecuteSwapAsync(
            battleId,
            CraftedSwap with { ExpectedSequence = pinned.Sequence });

        Assert.NotNull(round);
        Assert.True(round.Value.IsAccepted, $"reason={round.Value.Reason}");

        return new BossCastRound(bossBoardAction.Score, round.Value.Events, round.Value.State);
    }

    /// <summary>
    /// The fixture's pre-swap board, from eight row strings where <c>A</c> = ATK,
    /// <c>B</c> = DEF, <c>C</c> = HP, <c>D</c> = POWER.
    ///
    /// It carries no existing Match, and its only match-producing Swap — the
    /// fixture's committed one — creates a 3-cell DEF Match, which the documented
    /// per-Gem weights price at exactly <see cref="MinimumBoardActionScore"/>.
    /// </summary>
    private static BoardState CraftedPreSwapBoard() => BoardFromRows(
        "ABCBDBBD",
        "BCDABCDA",
        "CDABCDAB",
        "DABCDABC",
        "ABCDABCD",
        "BCDABCDA",
        "CDABCDAB",
        "DABCDABC");

    /// <summary>Reads a board from eight row strings (see <see cref="CraftedPreSwapBoard"/>).</summary>
    private static BoardState BoardFromRows(params string[] rows)
    {
        var cells = new GemType[BoardState.CellCount];

        for (var row = 0; row < BoardState.Rows; row++)
        {
            for (var column = 0; column < BoardState.Columns; column++)
            {
                cells[BoardState.ToIndex(row, column)] = rows[row][column] switch
                {
                    'A' => GemType.Atk,
                    'B' => GemType.Def,
                    'C' => GemType.Hp,
                    _ => GemType.Power,
                };
            }
        }

        return BoardState.FromCells(cells);
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
