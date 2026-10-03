using GameServer.Application.Battle;
using GameServer.Application.Cards;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Domain.Relics;
using Xunit;

namespace GameServer.Application.Tests.Battle;

/// <summary>
/// The Relic-sourced Card-cost modifier as Card casting consumes it:
/// <c>RELIC_RULES.md</c> §8.5's <c>CardCost</c> effect, held in
/// <c>PetState.CardCostModifiers[]</c> (<c>GAME_STATE.md</c> §2.3.5), read by
/// <c>CARD_RULES.md</c> §3.6's <c>EffectiveCardCost</c> and applied to
/// validation, deduction, and reporting as one value (§3.6 item 7).
///
/// <code>
/// Rule (CARD_RULES.md §3.6 items 1–9)
///  ↓
/// Scenario (Given a Pet carrying an applied CardCost modifier,
///           When a Card is cast,
///           Then the cast is validated against, pays, and reports the
///           composed cost, and the modifier itself is unchanged)
///  ↓
/// Test
/// </code>
///
/// <b>The Relic is the source of the modifier, in the real pipeline.</b> The
/// battle is created with a Relic equipped whose effect is CardCost 50% — §8.5
/// item 2's provisioned shape — so the modifier is applied by
/// <c>GAME_RULES.md</c> §17 step 11 during a committed Swap and then read by the
/// cast. Nothing here writes <c>PetState.CardCostModifiers[]</c> directly, so the
/// test cannot pass against a carrier the Relic stage does not fill.
/// </summary>
public sealed class CardCostRelicIntegrationTests
{
    private static readonly PlayerId Owner = new("player-cardcost-relic");

    private const string BattleId = "battle-cardcost-relic";
    private const string RelicSlot = "relic_emergency_core_instance";
    private const string HealCardId = "card-cost-heal";

    private static readonly BossDefinition BossDef = BossDefinitions.HoaLong;

    private static readonly CardDefinition HealCard = new()
    {
        CardDefinitionId = HealCardId,
        Name = "Heal",
        Category = CardCategory.Basic,
        PowerCost = 20,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.Flat, 10)),
    };

    [Fact]
    public async Task CardCast_ShouldUseTheRelicReducedCost_ForValidationDeductionAndReporting()
    {
        var (service, repository) = CreateService();

        var created = await service.CreateBattleAsync(
            BattleId,
            Owner,
            Configuration(),
            BossDef,
            equippedRelicDefinitions: [CardCostRelic()]);

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var swap = await service.ExecuteSwapAsync(BattleId, pair);

        // The Relic's effect is one entry, keyed on the equipped instance identity
        // (RELIC_RULES.md §2.2 item 3, §8.5 item 2).
        Assert.Equal(RelicSlot, Assert.Single(swap!.Value.State.PetState.CardCostModifiers).SourceIdentity);

        // CARD_RULES.md §3 item 2 validates `current Power >= Card Cost` against
        // EffectiveCardCost: 20 at 50% is 10, so a Power pool of exactly 10 passes —
        // which the authored cost of 20 would not.
        var current = await service.GetBattleAsync(BattleId);
        var funded = current! with
        {
            PetState = current.PetState with { Power = 10 },
        };
        await repository.TryUpdateAsync(funded, funded.Sequence);

        var cast = await service.ExecuteCardCastAsync(BattleId, HealCardId);

        Assert.NotNull(cast);
        Assert.False(cast!.Value.IsRejected);

        // §3.6 item 7's second consumer: the amount removed is exactly
        // EffectiveCardCost — 10, not the authored 20.
        Assert.Equal(0, cast.Value.State.PetState.Power);

        // §3.6 item 7's third consumer: CardCast reports the cost actually deducted
        // (GAME_EVENTS.md §2 item 3).
        var cardCast = Assert.Single(cast.Value.Events, e => e.Type == BattleEventType.CardCast);
        Assert.Equal(10, cardCast.CardCast.PowerCost);

        // §3.6 item 8 / GAME_STATE.md §5.1.3: the Card path READS the collection — it
        // creates, refreshes, and removes nothing, so the Relic's one entry is carried
        // through the cast unchanged.
        Assert.Equal(RelicSlot, Assert.Single(cast.Value.State.PetState.CardCostModifiers).SourceIdentity);
        Assert.Equal(50, Assert.Single(cast.Value.State.PetState.CardCostModifiers).CostReductionPercentage);
    }

    [Fact]
    public async Task CardCast_ShouldRejectAgainstTheAuthoredCost_WhenNoModifierIsActive()
    {
        // The control case: with no CardCost modifier active, EffectiveCardCost is
        // CardDefinition.PowerCost itself (§3.6 item 7, "no modifier active"), so the
        // same 10 Power pool cannot pay the authored 20.
        var (service, repository) = CreateService();

        var created = await service.CreateBattleAsync(BattleId, Owner, Configuration(), BossDef);

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        await service.ExecuteSwapAsync(BattleId, pair);

        var current = await service.GetBattleAsync(BattleId);
        var funded = current! with
        {
            PetState = current.PetState with { Power = 10 },
        };
        await repository.TryUpdateAsync(funded, funded.Sequence);

        var cast = await service.ExecuteCardCastAsync(BattleId, HealCardId);

        Assert.NotNull(cast);
        Assert.True(cast!.Value.IsRejected);
        Assert.Equal(CardCastRejectionReason.InsufficientPower, cast.Value.Reason);
        Assert.Equal(10, (await service.GetBattleAsync(BattleId))!.PetState.Power);
    }

    // =======================================================================
    // Harness
    // =======================================================================

    private static (BattleStateService Service, InMemoryBattleStateRepository Repository) CreateService()
    {
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(
            repository,
            new FixedRngSeedSource(),
            battleResults: null,
            cardDefinitions: new StubCardDefinitionLookup(HealCard));

        return (service, repository);
    }

    private static BattleStateService.PetConfiguration Configuration() =>
        new(
            PetId: new PetId("pet_cardcost"),
            Element: Element.Hoa,
            PassiveId: new PassiveId("xich-lang"),
            PassiveThreshold: 5,
            PassiveResetOverride: null,
            EquippedRelics: [new EquippedRelicIdentity(RelicSlot)],
            EquippedCards: [new EquippedCardIdentity(HealCardId)]);

    /// <summary>
    /// A Relic whose effect is §8.5 item 2's provisioned <c>CardCost</c> shape —
    /// <c>CardCost, Percentage, 50, Pet, Battle</c>. Its Trigger and threshold are
    /// scenario data chosen so the first committed Swap is guaranteed to satisfy
    /// them; the magnitude and lifetime are the documented ones.
    /// </summary>
    private static RelicDefinition CardCostRelic() => new()
    {
        RelicDefinitionId = "relic-emergency-core",
        Name = "Fixture Relic",
        Trigger = "OnMatchCount",
        Condition = RelicCondition.Create(RelicConditionType.MatchCountAtLeast, 1),
        EffectDefinition = RelicEffectDefinitions.Create(
            RelicEffectDefinition.Create(
                RelicEffectType.CardCost,
                RelicEffectValueType.Percentage,
                50,
                RelicEffectTarget.Pet,
                RelicEffectLifetime.Battle)),
    };

    private static SwapRequest FindAdjacentPairThatProducesAMatch(BoardState board)
    {
        for (var index = 0; index < BoardState.CellCount; index++)
        {
            var right = BoardState.ToColumn(index) + 1 < BoardState.Columns ? index + 1 : -1;
            var down = index + BoardState.Width < BoardState.CellCount ? index + BoardState.Width : -1;

            if (right >= 0 && MatchDetector.Detect(board.WithSwapped(index, right)).Count > 0)
            {
                return new SwapRequest(index, right);
            }

            if (down >= 0 && MatchDetector.Detect(board.WithSwapped(index, down)).Count > 0)
            {
                return new SwapRequest(index, down);
            }
        }

        throw new InvalidOperationException(
            "A generated board has at least one valid Swap (MATCH3_RULES.md §1.4).");
    }

    private sealed class StubCardDefinitionLookup : ICardDefinitionLookup
    {
        private readonly Dictionary<string, CardDefinition> _definitions = new(StringComparer.Ordinal);

        internal StubCardDefinitionLookup(params CardDefinition[] definitions)
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
