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
using Xunit;

namespace GameServer.Application.Tests.Battle;

public sealed class BattleStateServiceCardCastTests
{
    private static readonly PlayerId Owner = new("player-cardcast-test");

    private static readonly BattleStateService.PetConfiguration PetConfig = new(
        PetId: new PetId("pet-test"),
        Element: Element.Hoa,
        PassiveId: new PassiveId("passive-test"),
        PassiveThreshold: 10,
        PassiveResetOverride: null,
        EquippedRelics: null,
        EquippedCards:
        [
            new EquippedCardIdentity("card-heal"),
            new EquippedCardIdentity("card-shield"),
            new EquippedCardIdentity("card-power-charge"),
            new EquippedCardIdentity("card-inferno"),
        ]);

    private static readonly BossDefinition BossDef = BossDefinitions.HoaLong;

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

    private static readonly CardDefinition InfernoCard = new()
    {
        CardDefinitionId = "card-inferno",
        Name = "Inferno",
        Category = CardCategory.PetSkill,
        PowerCost = 40,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 100),
            CardEffectDefinition.Burn(CardEffectValueType.Flat, 50, 2)),
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

    private sealed class StubCardDefinitionLookup : ICardDefinitionLookup
    {
        private readonly Dictionary<string, CardDefinition> _definitions = new(StringComparer.Ordinal);

        public StubCardDefinitionLookup(params CardDefinition[] definitions)
        {
            foreach (var def in definitions)
            {
                _definitions[def.CardDefinitionId] = def;
            }
        }

        public Task<CardDefinition?> GetDefinitionAsync(string cardDefinitionId, CancellationToken cancellationToken = default)
        {
            _definitions.TryGetValue(cardDefinitionId, out var definition);
            return Task.FromResult(definition);
        }
    }

    private static (BattleStateService Service, InMemoryBattleStateRepository Repository) CreateService(
        params CardDefinition[] definitions)
    {
        var repository = new InMemoryBattleStateRepository();
        var lookup = new StubCardDefinitionLookup(
            definitions.Length > 0 ? definitions : [HealCard, ShieldCard, PowerChargeCard, InfernoCard, IronFangCard]);
        var service = new BattleStateService(
            repository,
            new FixedRngSeedSource(),
            battleResults: null,
            cardDefinitions: lookup);

        return (service, repository);
    }

    [Fact]
    public async Task ExecuteCardCastAsync_Heal_PersistsAuthoritativeStateAndAdvancesSequence()
    {
        var (service, repo) = CreateService();
        var initial = await service.CreateBattleAsync("battle-heal", Owner, PetConfig, BossDef);

        // Set HP to 500 and Power to 50
        var stateWithPower = initial with
        {
            PetState = initial.PetState with { HP = 500, MaxHP = 1000, Power = 50 },
        };
        await repo.TryUpdateAsync(stateWithPower, initial.Sequence);

        var result = await service.ExecuteCardCastAsync("battle-heal", "card-heal");

        Assert.NotNull(result);
        Assert.True(result.Value.IsAccepted);
        Assert.Equal(700, result.Value.State.PetState.HP);
        Assert.Equal(30, result.Value.State.PetState.Power);
        Assert.Equal(1, result.Value.State.Sequence);

        // Verify written to repository
        var stored = await repo.GetAsync("battle-heal");
        Assert.NotNull(stored);
        Assert.Equal(1, stored!.Sequence);
        Assert.Equal(700, stored.PetState.HP);
        Assert.Equal(30, stored.PetState.Power);
    }

    [Fact]
    public async Task ExecuteCardCastAsync_Shield_AppliesShieldAndPersistsToRepository()
    {
        var (service, repo) = CreateService();
        var initial = await service.CreateBattleAsync("battle-shield", Owner, PetConfig, BossDef);

        var stateWithPower = initial with
        {
            PetState = initial.PetState with { Power = 40 },
        };
        await repo.TryUpdateAsync(stateWithPower, initial.Sequence);

        var result = await service.ExecuteCardCastAsync("battle-shield", "card-shield");

        Assert.NotNull(result);
        Assert.True(result.Value.IsAccepted);
        Assert.Equal(20, result.Value.State.PetState.Power);
        Assert.Single(result.Value.State.PetState.ActiveStatusEffects);

        var stored = await repo.GetAsync("battle-shield");
        Assert.NotNull(stored);
        Assert.Single(stored!.PetState.ActiveStatusEffects);
        Assert.Equal("Shield", stored.PetState.ActiveStatusEffects[0].Id);
        Assert.Equal(200.0, stored.PetState.ActiveStatusEffects[0].Magnitude);
    }

    [Fact]
    public async Task ExecuteCardCastAsync_PowerCharge_GainsPowerAndPersists()
    {
        var (service, repo) = CreateService();
        var initial = await service.CreateBattleAsync("battle-power", Owner, PetConfig, BossDef);

        var stateWithPower = initial with
        {
            PetState = initial.PetState with { Power = 10 },
        };
        await repo.TryUpdateAsync(stateWithPower, initial.Sequence);

        var result = await service.ExecuteCardCastAsync("battle-power", "card-power-charge");

        Assert.NotNull(result);
        Assert.True(result.Value.IsAccepted);
        Assert.Equal(35, result.Value.State.PetState.Power);

        var stored = await repo.GetAsync("battle-power");
        Assert.NotNull(stored);
        Assert.Equal(35, stored!.PetState.Power);
    }

    [Fact]
    public async Task ExecuteCardCastAsync_Rejected_WritesNothingToRepository()
    {
        var (service, repo) = CreateService();
        var initial = await service.CreateBattleAsync("battle-reject", Owner, PetConfig, BossDef);

        // Power is 0, Heal requires 20
        var stateWithZeroPower = initial with
        {
            PetState = initial.PetState with { Power = 0 },
        };
        await repo.TryUpdateAsync(stateWithZeroPower, initial.Sequence);

        var result = await service.ExecuteCardCastAsync("battle-reject", "card-heal");

        Assert.NotNull(result);
        Assert.True(result.Value.IsRejected);
        Assert.Equal(CardCastRejectionReason.InsufficientPower, result.Value.Reason);

        // Repository must be completely unchanged
        var stored = await repo.GetAsync("battle-reject");
        Assert.NotNull(stored);
        Assert.Equal(0, stored!.Sequence);
        Assert.Equal(0, stored.PetState.Power);
        Assert.Empty(stored.PetState.ActiveStatusEffects);
    }

    [Fact]
    public async Task ExecuteCardCastAsync_UnknownBattle_ReturnsNull()
    {
        var (service, _) = CreateService();

        var result = await service.ExecuteCardCastAsync("nonexistent-battle", "card-heal");

        Assert.Null(result);
    }

    [Fact]
    public async Task ExecuteCardCastAsync_UnknownCard_ReturnsRejectedInvalidCard()
    {
        var (service, _) = CreateService();
        await service.CreateBattleAsync("battle-unknown-card", Owner, PetConfig, BossDef);

        var result = await service.ExecuteCardCastAsync("battle-unknown-card", "card-nonexistent");

        Assert.NotNull(result);
        Assert.True(result.Value.IsRejected);
        Assert.Equal(CardCastRejectionReason.InvalidCard, result.Value.Reason);
    }

    [Fact]
    public async Task ExecutePetSkillCastAsync_Inferno_AppliesDamageAndBurn_PersistsToRepository()
    {
        var (service, repo) = CreateService();
        var initial = await service.CreateBattleAsync("battle-petskill-inferno", Owner, PetConfig, BossDef);

        var stateWithPower = initial with
        {
            PetState = initial.PetState with { Power = 50 },
        };
        await repo.TryUpdateAsync(stateWithPower, initial.Sequence);

        var result = await service.ExecutePetSkillCastAsync("battle-petskill-inferno");

        Assert.NotNull(result);
        Assert.True(result.Value.IsAccepted);
        Assert.Equal(10, result.Value.State.PetState.Power);
        Assert.True(result.Value.State.BossState.HP < initial.BossState.HP);
        Assert.Single(result.Value.State.BossState.ActiveStatusEffects);
        Assert.Equal("Burn", result.Value.State.BossState.ActiveStatusEffects[0].Id);

        var stored = await repo.GetAsync("battle-petskill-inferno");
        Assert.NotNull(stored);
        Assert.Equal(1, stored!.Sequence);
        Assert.Equal(10, stored.PetState.Power);
        Assert.Single(stored.BossState.ActiveStatusEffects);
        Assert.Equal("Burn", stored.BossState.ActiveStatusEffects[0].Id);
    }

    [Fact]
    public async Task ExecutePetSkillCastAsync_InsufficientPower_RejectsWithoutWriting()
    {
        var (service, repo) = CreateService();
        var initial = await service.CreateBattleAsync("battle-petskill-reject", Owner, PetConfig, BossDef);

        var stateWithLowPower = initial with
        {
            PetState = initial.PetState with { Power = 20 },
        };
        await repo.TryUpdateAsync(stateWithLowPower, initial.Sequence);

        var result = await service.ExecutePetSkillCastAsync("battle-petskill-reject");

        Assert.NotNull(result);
        Assert.True(result.Value.IsRejected);
        Assert.Equal(CardCastRejectionReason.InsufficientPower, result.Value.Reason);

        var stored = await repo.GetAsync("battle-petskill-reject");
        Assert.NotNull(stored);
        Assert.Equal(0, stored!.Sequence);
        Assert.Equal(20, stored.PetState.Power);
    }

    [Fact]
    public async Task ExecuteSwapAsync_WithActiveBurnOnBoss_TicksDamageAtStep19a_AndDecrementsDuration()
    {
        var (service, repo) = CreateService();
        var initial = await service.CreateBattleAsync("battle-burn-tick", Owner, PetConfig, BossDef);

        // Apply a Burn instance (50 dmg, 2 turns)
        var burn = StatusEffect.TurnBased(
            "Burn",
            StatusEffectType.DoT,
            StatusEffectSource.Player,
            50,
            2);

        var stateWithBurn = initial with
        {
            BossState = initial.BossState with
            {
                ActiveStatusEffects = [burn],
            },
        };
        await repo.TryUpdateAsync(stateWithBurn, initial.Sequence);

        var pair = FindAdjacentPairThatProducesAMatch(stateWithBurn.BoardState);
        var result = await service.ExecuteSwapAsync("battle-burn-tick", pair);

        Assert.NotNull(result);
        Assert.True(result.Value.IsAccepted);

        // Burn duration decremented from 2 to 1
        Assert.Single(result.Value.State.BossState.ActiveStatusEffects);
        Assert.Equal(1, result.Value.State.BossState.ActiveStatusEffects[0].RemainingTurns);

        // Damage events should include player direct damage (3 events), boss response damage (3 events), and burn tick damage (3 events)
        var damageEvents = result.Value.Events
            .Where(e => e.Type is BattleEventType.DamageCalculated or BattleEventType.DamageDealt or BattleEventType.DamageTaken)
            .ToList();

        Assert.Equal(9, damageEvents.Count); // 3 instances x 3 events
    }

    [Fact]
    public async Task ExecuteSwapAsync_AfterIronFang_ConsumesCritModifierOnNextAttack()
    {
        var (service, repo) = CreateService();
        var ironFangConfig = PetConfig with
        {
            EquippedCards =
            [
                new EquippedCardIdentity("card-heal"),
                new EquippedCardIdentity("card-shield"),
                new EquippedCardIdentity("card-power-charge"),
                new EquippedCardIdentity("card-iron-fang"),
            ],
        };
        var initial = await service.CreateBattleAsync("battle-iron-fang-crit", Owner, ironFangConfig, BossDef);

        var stateWithPower = initial with
        {
            PetState = initial.PetState with { Power = 50 },
        };
        await repo.TryUpdateAsync(stateWithPower, initial.Sequence);

        // 1. Cast Iron Fang (+10% Crit)
        var castResult = await service.ExecutePetSkillCastAsync("battle-iron-fang-crit");
        Assert.NotNull(castResult);
        Assert.True(castResult.Value.IsAccepted);
        Assert.Equal(15, castResult.Value.State.PetState.Crit); // 5 + 10 = 15%

        // 2. Perform Swap attack -> Crit modifier consumed back to default (5%)
        var pair = FindAdjacentPairThatProducesAMatch(castResult.Value.State.BoardState);
        var swapResult = await service.ExecuteSwapAsync("battle-iron-fang-crit", pair);
        Assert.NotNull(swapResult);
        Assert.True(swapResult.Value.IsAccepted);
        Assert.Equal(PetState.DefaultCrit, swapResult.Value.State.PetState.Crit); // Back to 5%
    }

    private static SwapRequest FindAdjacentPairThatProducesAMatch(BoardState board)
    {
        for (var from = 0; from < 64; from++)
        {
            var row = from / 8;
            var col = from % 8;

            if (col + 1 < 8)
            {
                var to = from + 1;
                if (MatchDetector.Detect(board.WithSwapped(from, to)).Count > 0)
                {
                    return new SwapRequest(from, to);
                }
            }

            if (row + 1 < 8)
            {
                var to = from + 8;
                if (MatchDetector.Detect(board.WithSwapped(from, to)).Count > 0)
                {
                    return new SwapRequest(from, to);
                }
            }
        }

        throw new InvalidOperationException("No valid swap found on test board.");
    }
}
