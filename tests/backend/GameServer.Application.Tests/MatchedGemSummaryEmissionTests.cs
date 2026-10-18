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

namespace GameServer.Application.Tests;

/// <summary>
/// The <c>MatchedGemSummary</c> emission contract as the Application layer runs it
/// — <c>GAME_EVENTS.md</c> §1.1 item 9 and §2, <c>SIGNALR_PROTOCOL.md</c>
/// §3.2.29.
///
/// <code>
/// Rule (GAME_EVENTS.md §1.1 item 9, §2)
///  ↓
/// Scenario (Given a committed board action, When the round resolves, Then one
///           MatchedGemSummary closes that cycle with its own counts)
///  ↓
/// Test
/// </code>
///
/// <b>One cycle, one summary, its own counts.</b> The event is emitted exactly
/// once per committed board action — the Player's committed Swap and the Boss
/// Response Phase's board action are two cycles and therefore two items — it
/// aggregates every cascade pass of the cycle it closes, it reports the counts the
/// resource stage already generated (never a recalculated number), it omits the
/// Gem types the cycle did not match, and it reports counts only: it is not damage,
/// healing, DEF buff magnitude, Power, or Boss SkillCharge.
/// </summary>
public class MatchedGemSummaryEmissionTests
{
    private static readonly BattleStateService.PetConfiguration Pet =
        new(new PetId("pet_instance_summary"), Element.Hoa, new PassiveId("xich-lang"), PassiveThreshold: 5);

    private static readonly PlayerId Owner = new("player_matched_gem_summary_owner");

    private static readonly CardDefinition PowerChargeCard = new()
    {
        CardDefinitionId = "card-summary-power-charge",
        Name = "Power Charge",
        Category = CardCategory.Basic,
        PowerCost = 0,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Power, CardEffectValueType.Flat, 25)),
    };

    // =======================================================================
    // 1. One summary per cycle, carrying that cycle's own counts
    // =======================================================================

    [Fact]
    public async Task CommittedPlayerSwap_ShouldEmitExactlyOneSummaryCarryingItsCycleCounts()
    {
        // GAME_EVENTS.md §1.1 item 9 / §2 items 1–2: exactly one item per committed
        // board action, and its counts are the cycle's own aggregate.
        var (service, repository) = NewService();
        var created = await service.CreateBattleAsync("summary-player-once", Owner, Pet, BossDefinitions.HoaLong);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Def, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(repository, created, pinned.Board);

        var result = (await service.ExecuteSwapAsync("summary-player-once", pinned.Swap))!.Value;
        Assert.True(result.IsAccepted);

        // The Player's cycle is the first committed board action, so the first
        // summary in the batch is the Player's.
        var summary = FirstSummary(result);

        AssertSummaryCarries(summary, pinned.Resources.ClearedGemCounts);
    }

    [Fact]
    public async Task CommittedPlayerSwap_ShouldEmitTheSummaryAsTheCyclesLastItem()
    {
        // GAME_EVENTS.md §1.1 item 9: the item closes the cycle — after the cycle's
        // last recorded pass's CascadeStepResolved and before the following stage's
        // events.
        var (service, repository) = NewService();
        var created = await service.CreateBattleAsync("summary-position", Owner, Pet, BossDefinitions.HoaLong);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Hp, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(repository, created, pinned.Board);

        var result = (await service.ExecuteSwapAsync("summary-position", pinned.Swap))!.Value;

        var runEnd = PlayerCycleEndIndex(result);
        Assert.True(runEnd >= 0, "the committed Swap delivered a board cycle");
        Assert.Equal(BattleEventType.CascadeStepResolved, result.Events[runEnd].Type);
        Assert.Equal(BattleEventType.MatchedGemSummary, result.Events[runEnd + 1].Type);
    }

    [Fact]
    public async Task MatchedGemSummary_ShouldCarryCountsOnlyAndNoCombatValue()
    {
        // GAME_EVENTS.md §2 item 6: counts only. The event's member set is the
        // discriminator plus the per-type entries; no damage, healing, DEF buff, Power,
        // SkillCharge, Combo, or Match count travels on it.
        var (service, repository) = NewService();
        var created = await service.CreateBattleAsync("summary-members", Owner, Pet, BossDefinitions.HoaLong);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Def, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(repository, created, pinned.Board);

        var result = (await service.ExecuteSwapAsync("summary-members", pinned.Swap))!.Value;
        var summary = FirstSummary(result);

        Assert.Equal(typeof(MatchedGemSummaryEvent), summary.GetType());
        Assert.All(summary.Gems, gem => Assert.True(gem.Count >= 1));

        // The counts are reporting only: nothing on the event is a pool, a state
        // value, or a combat magnitude, and the four kinds of count are Gems.
        Assert.All(
            summary.Gems,
            gem => Assert.Contains(
                gem.GemType,
                new[] { GemType.Atk, GemType.Def, GemType.Hp, GemType.Power }));
    }

    // =======================================================================
    // 2. Aggregation across passes and across Gem types
    // =======================================================================

    [Fact]
    public async Task MultiPassCascade_ShouldBeAggregatedIntoOneSummary()
    {
        // GAME_EVENTS.md §2 item 1: the counts are the sum over EVERY pass of the
        // cycle, with each pass's cleared cells counted once. A cascade is therefore
        // aggregated into one item, never one item per pass.
        var (service, repository) = NewService();
        var created = await service.CreateBattleAsync("summary-cascade", Owner, Pet, BossDefinitions.HoaLong);

        var (rng, pinned) = RoundTestSupport.PlayerCascadeMatch(
            GemType.Hp,
            created.RngState,
            created.LastCommittedSwapPair);

        Assert.True(pinned.Resolution.Passes.Count >= 2, "the fixture resolves in a cascade");

        await RoundTestSupport.PinAsync(repository, created with { RngState = rng }, pinned.Board);

        var result = (await service.ExecuteSwapAsync("summary-cascade", pinned.Swap))!.Value;

        var summary = FirstSummary(result);

        AssertSummaryCarries(summary, pinned.Resources.ClearedGemCounts);

        // ...and it is ONE item for the whole multi-pass cycle, not one per pass. The
        // player's cycle still reports one CascadeStepResolved per pass, so the single
        // summary at its end is the aggregate of them (the round's later Boss cycle is
        // a separate run with its own item).
        Assert.Equal(
            pinned.Resolution.Passes.Count,
            RoundTestSupport.PlayerBoardEvents(result)
                .Count(e => e.Type == BattleEventType.CascadeStepResolved));

        Assert.Single(SummariesOfFirstCommittedAction(result));

        // The aggregate is the whole cycle's cleared cells, so its total is the sum of
        // every pass's unique cleared cells — not the last pass's.
        Assert.Equal(
            pinned.Resolution.Passes.Sum(pass => pass.ClearedGems.Count),
            summary.Gems.Sum(gem => gem.Count));
    }

    [Fact]
    public async Task MultipleGemTypes_ShouldBeReportedInOneSummaryInTheFixedOrder()
    {
        // GAME_EVENTS.md §2 item 5: one entry per matched type, in the fixed
        // MATCH3_RULES.md §1.1 order — a cycle that matched several types reports them
        // all in that one item.
        var (service, repository) = NewService();
        var created = await service.CreateBattleAsync("summary-multi-type", Owner, Pet, BossDefinitions.HoaLong);

        var (rng, pinned) = RoundTestSupport.PlayerMultiTypeMatch(
            GemType.Hp,
            created.RngState,
            created.LastCommittedSwapPair);

        Assert.True(
            pinned.Resources.ClearedGemCounts.Total > pinned.Resources.ClearedGemCounts.Hp,
            "the fixture clears more than one Gem type");

        await RoundTestSupport.PinAsync(repository, created with { RngState = rng }, pinned.Board);

        var result = (await service.ExecuteSwapAsync("summary-multi-type", pinned.Swap))!.Value;
        var summary = FirstSummary(result);

        AssertSummaryCarries(summary, pinned.Resources.ClearedGemCounts);
        Assert.True(summary.Gems.Count >= 2, "several Gem types are reported by one summary");

        var order = summary.Gems.Select(gem => gem.GemType).ToArray();
        var expectedOrder = new[] { GemType.Atk, GemType.Def, GemType.Hp, GemType.Power }
            .Where(type => pinned.Resources.ClearedGemCounts.For(type) > 0)
            .ToArray();

        Assert.Equal(expectedOrder, order);
    }

    [Fact]
    public async Task ZeroCountGemTypes_ShouldBeOmittedFromTheSummary()
    {
        // GAME_EVENTS.md §2 item 4: a type the cycle did not match is absent, never
        // present as a zero count.
        var (service, repository) = NewService();
        var created = await service.CreateBattleAsync("summary-zero", Owner, Pet, BossDefinitions.HoaLong);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Def, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(repository, created, pinned.Board);

        var result = (await service.ExecuteSwapAsync("summary-zero", pinned.Swap))!.Value;
        var summary = FirstSummary(result);

        // The fixture clears DEF Gems and no other type, so the item has exactly that
        // one entry and no zero-count entries at all.
        var entry = Assert.Single(summary.Gems);
        Assert.Equal(GemType.Def, entry.GemType);
        Assert.Equal(pinned.Resources.ClearedGemCounts.Def, entry.Count);
        Assert.DoesNotContain(summary.Gems, gem => gem.Count == 0);
    }

    // =======================================================================
    // 3. Player and Boss are separate cycles
    // =======================================================================

    [Fact]
    public async Task PlayerAndBossCycles_ShouldEachEmitTheirOwnSummary_PlayerFirst()
    {
        // ADR-026 §2 / GAME_EVENTS.md §1.1 item 9: one round carries the Player's board
        // cycle and the Boss Response phase's, and each closes with its OWN summary.
        // A non-ATK Swap leaves the Boss alive, so the Boss takes its board action.
        var (service, repository) = NewService();
        var created = await service.CreateBattleAsync("summary-two-cycles", Owner, Pet, BossDefinitions.HoaLong);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Def, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(repository, created, pinned.Board);

        var result = (await service.ExecuteSwapAsync("summary-two-cycles", pinned.Swap))!.Value;

        // Two committed board actions, and exactly one summary per action.
        var boundaries = result.Events.Count(e => e.Type == BattleEventType.SwapStarted);
        var summaries = Summaries(result);

        Assert.Equal(2, boundaries);
        Assert.Equal(2, summaries.Count);

        // The Player's cycle is delivered first, so its summary is the earlier item and
        // carries the Player's own counts.
        AssertSummaryCarries(summaries[0], pinned.Resources.ClearedGemCounts);

        // The Boss's summary carries the BOSS cycle's counts: the delivered per-cell
        // GemMatched run of the Boss's own board cycle, tallied by type. It is not the
        // Player's counts and not a combined total.
        var bossRun = RoundTestSupport.BossBoardEvents(result);
        var bossTally = new ClearedGemCounts(
            bossRun.Count(e => e.Type == BattleEventType.GemMatched && e.Gem.GemType == GemType.Atk),
            bossRun.Count(e => e.Type == BattleEventType.GemMatched && e.Gem.GemType == GemType.Def),
            bossRun.Count(e => e.Type == BattleEventType.GemMatched && e.Gem.GemType == GemType.Hp),
            bossRun.Count(e => e.Type == BattleEventType.GemMatched && e.Gem.GemType == GemType.Power));

        Assert.True(bossTally.Total > 0, "the Boss's cycle cleared at least one Gem");
        AssertSummaryCarries(summaries[1], bossTally);

        // The two cycles' counts are never mixed: the Boss summary cannot report the
        // Player's counts (and vice versa) — the Player's total is not part of it.
        Assert.NotEqual(
            pinned.Resources.ClearedGemCounts.Total + bossTally.Total,
            summaries[1].Gems.Sum(gem => gem.Count));
    }

    [Fact]
    public async Task VictoryBeforeTheBossCycle_ShouldEmitOnlyThePlayersSummary()
    {
        // BOSS_RULES.md §5 item 4 / GAME_RULES.md §1.4: a Boss killed by the Player's
        // attack does not take its step-18a board action, so there is no Boss cycle to
        // close and no Boss summary. The outcome stays the batch's last item.
        var (service, repository) = NewService();
        var created = await service.CreateBattleAsync("summary-victory", Owner, Pet, BossDefinitions.HoaLong);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Atk, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(
            repository,
            created,
            pinned.Board,
            petState: created.PetState with { ATK = 20_000 });

        var result = (await service.ExecuteSwapAsync("summary-victory", pinned.Swap))!.Value;

        // Exactly one committed board action (the Player's) and therefore one summary.
        Assert.Single(result.Events, e => e.Type == BattleEventType.SwapStarted);
        var summary = Assert.Single(Summaries(result));
        AssertSummaryCarries(summary, pinned.Resources.ClearedGemCounts);

        // The outcome is still the batch's last item — no summary is emitted for a
        // cycle that never ran.
        Assert.Equal(BattleEventType.BattleWon, result.Events[^1].Type);
    }

    // =======================================================================
    // 4. Actions that present no board cycle present no summary
    // =======================================================================

    [Fact]
    public async Task RejectedSwap_ShouldEmitNoSummary()
    {
        // GAME_EVENTS.md §1.2 / MATCH3_RULES.md §2.1.5 item 6: a rejected action emits
        // no Battle Event at all, so it closes no cycle and reports no counts.
        var (service, repository) = NewService();
        var created = await service.CreateBattleAsync("summary-rejected", Owner, Pet, BossDefinitions.HoaLong);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Def, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(repository, created, pinned.Board);

        var committed = await service.ExecuteSwapAsync("summary-rejected", pinned.Swap);
        Assert.True(committed!.Value.IsAccepted);

        // The same pair again is the already-applied action — rejected, and therefore
        // silent.
        var rejected = await service.ExecuteSwapAsync("summary-rejected", pinned.Swap);

        Assert.NotNull(rejected);
        Assert.True(rejected!.Value.IsRejected);
        Assert.Empty(rejected.Value.Events);
        Assert.DoesNotContain(rejected.Value.Events, e => e.Type == BattleEventType.MatchedGemSummary);
    }

    [Fact]
    public async Task CardCast_ShouldEmitNoSummary()
    {
        // GAME_RULES.md §17: a Card cast is resolved between Swaps and resolves no
        // board, so it presents no board cycle and closes none. Its batch therefore
        // carries no MatchedGemSummary.
        var (service, repository) = NewService(PowerChargeCard);
        var created = await service.CreateBattleAsync(
            "summary-card-cast",
            Owner,
            Pet with
            {
                EquippedCards =
                [
                    new EquippedCardIdentity(PowerChargeCard.CardDefinitionId),
                ],
            },
            BossDefinitions.HoaLong);

        await RoundTestSupport.PinAsync(
            repository,
            created,
            created.BoardState,
            petState: created.PetState with { Power = 0 });

        var cast = await service.ExecuteCardCastAsync("summary-card-cast", PowerChargeCard.CardDefinitionId);

        Assert.NotNull(cast);
        Assert.True(cast!.Value.IsAccepted);
        Assert.NotEmpty(cast.Value.Events);
        Assert.DoesNotContain(cast.Value.Events, e => e.Type == BattleEventType.MatchedGemSummary);
        Assert.DoesNotContain(cast.Value.Events, e => e.Type == BattleEventType.SwapStarted);
    }

    // =======================================================================
    // Helpers
    // =======================================================================

    private static (BattleStateService Service, InMemoryBattleStateRepository Repository) NewService(
        params CardDefinition[] definitions)
    {
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(
            repository,
            new FixedRngSeedSource(),
            battleResults: null,
            cardDefinitions: new StubCardDefinitionLookup(definitions));

        return (service, repository);
    }

    private static MatchedGemSummaryEvent FirstSummary(SwapExecutionResult result) =>
        Assert.Single(SummariesOfFirstCommittedAction(result));

    private static IReadOnlyList<MatchedGemSummaryEvent> Summaries(SwapExecutionResult result) =>
        result.Events
            .Where(e => e.Type == BattleEventType.MatchedGemSummary)
            .Select(e => e.MatchedGemSummary)
            .ToArray();

    /// <summary>
    /// The summaries of the FIRST committed board action in the batch — the Player's
    /// cycle. Exactly one item belongs to it, whatever the round's later cycles are.
    /// </summary>
    private static IReadOnlyList<MatchedGemSummaryEvent> SummariesOfFirstCommittedAction(
        SwapExecutionResult result)
    {
        var collected = new List<MatchedGemSummaryEvent>();

        foreach (var battleEvent in result.Events)
        {
            if (battleEvent.Type == BattleEventType.MatchedGemSummary)
            {
                collected.Add(battleEvent.MatchedGemSummary);

                // The first summary closes the first cycle; a second one closes the
                // next cycle.
                break;
            }
        }

        return collected;
    }

    /// <summary>
    /// The index of the last board-cycle event of the Player's cycle — the first
    /// contiguous run of board events in the delivered list.
    /// </summary>
    private static int PlayerCycleEndIndex(SwapExecutionResult result)
    {
        var end = -1;
        var inRun = false;

        for (var index = 0; index < result.Events.Count; index++)
        {
            if (IsBoardCycleEvent(result.Events[index].Type))
            {
                inRun = true;
                end = index;
                continue;
            }

            if (inRun)
            {
                break;
            }
        }

        return end;
    }

    private static bool IsBoardCycleEvent(BattleEventType type) =>
        type
            is BattleEventType.MatchCreated
            or BattleEventType.CascadeCreated
            or BattleEventType.GemMatched
            or BattleEventType.ComboChanged
            or BattleEventType.CascadeStepResolved;

    /// <summary>
    /// Asserts the summary reports exactly the given aggregate: one entry per type
    /// with a non-zero count, in the fixed order, and no entry for a zero-count type.
    /// </summary>
    private static void AssertSummaryCarries(
        MatchedGemSummaryEvent summary,
        ClearedGemCounts expected)
    {
        var expectedTypes = new[] { GemType.Atk, GemType.Def, GemType.Hp, GemType.Power }
            .Where(type => expected.For(type) > 0)
            .ToArray();

        Assert.Equal(expectedTypes, summary.Gems.Select(gem => gem.GemType).ToArray());

        foreach (var gem in summary.Gems)
        {
            Assert.Equal(expected.For(gem.GemType), gem.Count);
            Assert.True(gem.Count >= 1);
        }

        Assert.Equal(expected.Total, summary.Gems.Sum(gem => gem.Count));
    }

    private sealed class StubCardDefinitionLookup : ICardDefinitionLookup
    {
        private readonly Dictionary<string, CardDefinition> _definitions = new(StringComparer.Ordinal);

        public StubCardDefinitionLookup(params CardDefinition[] definitions)
        {
            foreach (var definition in definitions)
            {
                _definitions[definition.CardDefinitionId] = definition;
            }
        }

        public Task<CardDefinition?> GetDefinitionAsync(
            string cardDefinitionId,
            CancellationToken cancellationToken = default)
        {
            _definitions.TryGetValue(cardDefinitionId, out var definition);
            return Task.FromResult(definition);
        }
    }
}
