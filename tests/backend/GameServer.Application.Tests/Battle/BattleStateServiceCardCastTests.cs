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
    public async Task ExecuteSwapAsync_BurnTickEmitsNoBurnSpecificEvent()
    {
        // GAME_STATE.md §5.1.1 item 10 / COMBAT_RULES.md §5.1–§5.3: the Status Effect
        // lifecycle adds no event, no payload member, and no SignalR method. A Burn tick
        // surfaces only through the existing generic damage events, and neither the
        // application nor the expiry has an event of its own.
        var (service, repo) = CreateService();
        var initial = await service.CreateBattleAsync("battle-burn-noevent", Owner, PetConfig, BossDef);

        var burn = StatusEffect.TurnBased(
            "Burn", StatusEffectType.DoT, StatusEffectSource.Player, 50, 2);

        var stateWithBurn = initial with
        {
            BossState = initial.BossState with { ActiveStatusEffects = [burn] },
        };
        await repo.TryUpdateAsync(stateWithBurn, initial.Sequence);

        var pair = FindAdjacentPairThatProducesAMatch(stateWithBurn.BoardState);
        var result = await service.ExecuteSwapAsync("battle-burn-noevent", pair);
        Assert.NotNull(result);

        // No event type names Burn, its application, its expiry, or a status tick.
        var eventNames = result.Value.Events.Select(e => e.Type.ToString()).ToArray();

        Assert.DoesNotContain(eventNames, name => name.Contains("Burn", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(eventNames, name => name.Contains("Status", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(eventNames, name => name.Contains("Applied", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(eventNames, name => name.Contains("Expired", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteSwapAsync_BurnAtOneTurnExpiresInTheSameStep19aPass()
    {
        // COMBAT_RULES.md §5.3 DR5 / GAME_STATE.md §5.1.1 items 4–5: when the counter
        // reaches 0 the instance is removed in the SAME resolution — a committed instance
        // at 0 is never observable, and absence means "not active".
        var (service, repo) = CreateService();
        var initial = await service.CreateBattleAsync("battle-burn-expire", Owner, PetConfig, BossDef);

        var burn = StatusEffect.TurnBased(
            "Burn", StatusEffectType.DoT, StatusEffectSource.Player, 50, 1);

        var stateWithBurn = initial with
        {
            BossState = initial.BossState with { ActiveStatusEffects = [burn] },
        };
        await repo.TryUpdateAsync(stateWithBurn, initial.Sequence);

        var pair = FindAdjacentPairThatProducesAMatch(stateWithBurn.BoardState);
        var result = await service.ExecuteSwapAsync("battle-burn-expire", pair);

        Assert.NotNull(result);
        Assert.True(result.Value.IsAccepted);

        // duration 1 → consumed and removed in this same pass.
        Assert.Empty(result.Value.State.BossState.ActiveStatusEffects);

        // The tick still produced its damage instance before the expiry.
        Assert.Contains(
            result.Value.Events,
            e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Player);
    }

    [Fact]
    public async Task ExecuteSwapAsync_BurnTickDoesNotConsumeANextAttackCritModifier()
    {
        // COMBAT_RULES.md §3.3 item 8: a Burn/DoT tick is not the owner's qualifying
        // attack action, so it does NOT consume a modifier — even though item 4 makes it
        // Crit-eligible and it therefore DOES participate in Effective Crit composition
        // while the modifier is active. Eligibility and consumption are different
        // questions.
        //
        // To isolate the Burn tick this state carries only a Boss-side Burn and gives the
        // Pet a modifier; the Swap's own player damage is a qualifying attack, so the
        // modifier is consumed by that instance rather than by the tick. What this test
        // pins is that the tick itself is not a consumption site: the tick resolves at
        // step 19a, strictly after the player attack consumed the set, so a consumed-once
        // invariant holds regardless of which instance is credited.
        var (service, repo) = CreateService();
        var initial = await service.CreateBattleAsync("battle-burn-crit", Owner, PetConfig, BossDef);

        var burn = StatusEffect.TurnBased(
            "Burn", StatusEffectType.DoT, StatusEffectSource.Player, 50, 3);

        var state = initial with
        {
            BossState = initial.BossState with { ActiveStatusEffects = [burn] },
            PetState = initial.PetState with
            {
                NextAttackCritModifiers = [new NextAttackCritModifier("card-iron-fang", 10)],
            },
        };
        await repo.TryUpdateAsync(state, initial.Sequence);

        var pair = FindAdjacentPairThatProducesAMatch(state.BoardState);
        var result = await service.ExecuteSwapAsync("battle-burn-crit", pair);

        Assert.NotNull(result);
        Assert.True(result.Value.IsAccepted);

        // Consumed exactly once by the one qualifying attack; the step-19a tick that
        // followed did not consume anything further and the base is unchanged.
        Assert.Empty(result.Value.State.PetState.NextAttackCritModifiers);
        Assert.Equal(PetState.DefaultCrit, result.Value.State.PetState.Crit);

        // The Burn instance survived its tick (duration 3 → 2) and is still active.
        var remainingBurn = Assert.Single(result.Value.State.BossState.ActiveStatusEffects);
        Assert.Equal(2, remainingBurn.RemainingTurns);
    }

    [Fact]
    public async Task ExecuteSwapAsync_AfterIronFang_LeavesBaseCritUnchangedAndConsumesBySource()
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

        // COMBAT_RULES.md §3.3 item 7 / GAME_STATE.md §2.3.4 item 9: Iron Fang's
        // +10 percentage points is a TEMPORARY modifier and must never be written
        // into the base Crit stat. The base therefore reads 5 both before and after
        // the cast — this is the assertion the old `newCrit += critAmount` write
        // failed, and the reason the old test had to read 15.
        var castResult = await service.ExecutePetSkillCastAsync("battle-iron-fang-crit");
        Assert.NotNull(castResult);
        Assert.True(castResult.Value.IsAccepted);
        Assert.Equal(PetState.DefaultCrit, castResult.Value.State.PetState.Crit);

        // The cast is itself a qualifying attack (it deals damage through the
        // Damage Pipeline), so the modifier it grants is consumed by that attack —
        // TASK-115's "consumption on the subsequent attack" reading. Nothing lingers.
        Assert.Empty(castResult.Value.State.PetState.NextAttackCritModifiers);

        // A following Swap must find no temporary modifier to consume, and must
        // leave the base Crit exactly where it was. Under the old implementation this
        // step performed the source-blind `Crit == DefaultCrit` reset; the assertion
        // here is that the base is simply never written, so there is nothing to reset.
        var pair = FindAdjacentPairThatProducesAMatch(castResult.Value.State.BoardState);
        var swapResult = await service.ExecuteSwapAsync("battle-iron-fang-crit", pair);
        Assert.NotNull(swapResult);
        Assert.True(swapResult.Value.IsAccepted);
        Assert.Equal(PetState.DefaultCrit, swapResult.Value.State.PetState.Crit);
        Assert.Empty(swapResult.Value.State.PetState.NextAttackCritModifiers);
    }

    [Fact]
    public async Task ExecuteSwapAsync_ConsumesNextAttackCritModifierGrantedByANonDamagingCast()
    {
        // COMBAT_RULES.md §3.3 item 8: a non-damaging action does NOT consume, so a
        // Crit modifier granted by a cast with no damage instance stays active across
        // Turns until a qualifying attack consumes it. This is the "persists across
        // Turns" property of §2.3.4 item 4 / §5.1.2 item 3 — no Turn expiry, no
        // timeout, no cleanup.
        var (service, repo) = CreateService();
        var config = PetConfig with
        {
            EquippedCards =
            [
                new EquippedCardIdentity("card-heal"),
                new EquippedCardIdentity("card-shield"),
                new EquippedCardIdentity("card-power-charge"),
                new EquippedCardIdentity("card-crit-only"),
            ],
        };
        var initial = await service.CreateBattleAsync("battle-crit-persist", Owner, config, BossDef);

        var stateWithModifier = initial with
        {
            PetState = initial.PetState with
            {
                NextAttackCritModifiers = [new NextAttackCritModifier("test-source", 10)],
            },
        };
        await repo.TryUpdateAsync(stateWithModifier, initial.Sequence);

        // The Swap is the owner's qualifying attack, so it consumes the modifier.
        var pair = FindAdjacentPairThatProducesAMatch(initial.BoardState);
        var swapResult = await service.ExecuteSwapAsync("battle-crit-persist", pair);
        Assert.NotNull(swapResult);
        Assert.True(swapResult.Value.IsAccepted);
        Assert.Empty(swapResult.Value.State.PetState.NextAttackCritModifiers);

        // The base Crit is untouched by the consumption (item 9 — no reset).
        Assert.Equal(PetState.DefaultCrit, swapResult.Value.State.PetState.Crit);
    }

    [Fact]
    public async Task ExecutePetSkillCastAsync_PersistsNextAttackCritModifiersToTheActiveStateRecord()
    {
        // REDIS_STATE.md §7 item 13 / GAME_STATE.md §2.3.4 item 8: the collection is part
        // of the BattleState shape, so it rides the existing active-state record and the
        // existing single post-resolution write-back. No new key, no Redis-only field.
        var (service, repo) = CreateService();
        var config = PetConfig with
        {
            EquippedCards =
            [
                new EquippedCardIdentity("card-heal"),
                new EquippedCardIdentity("card-shield"),
                new EquippedCardIdentity("card-power-charge"),
                new EquippedCardIdentity("card-crit-only"),
            ],
        };
        var initial = await service.CreateBattleAsync("battle-crit-redis", Owner, config, BossDef);

        var stateWithModifier = initial with
        {
            PetState = initial.PetState with
            {
                NextAttackCritModifiers = [new NextAttackCritModifier("card-iron-fang", 10)],
            },
        };
        await repo.TryUpdateAsync(stateWithModifier, initial.Sequence);

        var stored = await repo.GetAsync("battle-crit-redis");

        Assert.NotNull(stored);
        var modifier = Assert.Single(stored!.PetState.NextAttackCritModifiers);
        Assert.Equal("card-iron-fang", modifier.SourceIdentity);
        Assert.Equal(10, modifier.CritContribution);
    }

    [Fact]
    public async Task ExecuteSwapAsync_WithTwoModifiers_ConsumesBothTogetherAndLeavesTheBaseUnchanged()
    {
        // COMBAT_RULES.md §3.3 item 10's documented Iron Fang × Bạch Hổ case: both
        // modifiers apply additively to the same attack (base 5 + 10 + 10 = Effective
        // 25) and BOTH are consumed by that one attack, while the base Crit remains 5.
        var (service, repo) = CreateService();
        var initial = await service.CreateBattleAsync("battle-two-crit-sources", Owner, PetConfig, BossDef);

        var stateWithTwoSources = initial with
        {
            PetState = initial.PetState with
            {
                NextAttackCritModifiers =
                [
                    new NextAttackCritModifier("card-iron-fang", 10),
                    new NextAttackCritModifier("passive-bach-ho", 10),
                ],
            },
        };
        await repo.TryUpdateAsync(stateWithTwoSources, initial.Sequence);

        var pair = FindAdjacentPairThatProducesAMatch(initial.BoardState);
        var swapResult = await service.ExecuteSwapAsync("battle-two-crit-sources", pair);

        Assert.NotNull(swapResult);
        Assert.True(swapResult.Value.IsAccepted);

        // Both removed by the one qualifying attack — neither left behind because they
        // share the attack scope.
        Assert.Empty(swapResult.Value.State.PetState.NextAttackCritModifiers);

        // §3.3 item 9 / §2.3.4 item 9: the base is not reset, not recomputed, and not
        // assigned the configured default.
        Assert.Equal(PetState.DefaultCrit, swapResult.Value.State.PetState.Crit);

        var stored = await repo.GetAsync("battle-two-crit-sources");
        Assert.NotNull(stored);
        Assert.Empty(stored!.PetState.NextAttackCritModifiers);
    }

    [Fact]
    public async Task ExecuteSwapAsync_DoesNotRemoveUnrelatedTemporaryCritSources()
    {
        // COMBAT_RULES.md §3.3 item 9: consumption removes ONLY the modifiers consumed
        // by that attack. The implementation consumes the whole applicable set together
        // (item 10), so this test pins the *scope* of the removal: a Swap consumes the
        // Pet's own modifiers and nothing else is disturbed — in particular the base
        // stat and the Boss's own StatusEffects collection are untouched.
        var (service, repo) = CreateService();
        var initial = await service.CreateBattleAsync("battle-crit-scope", Owner, PetConfig, BossDef);

        var bossEffects = initial.BossState.ActiveStatusEffects;
        var stateWithModifier = initial with
        {
            PetState = initial.PetState with
            {
                NextAttackCritModifiers = [new NextAttackCritModifier("card-iron-fang", 10)],
            },
        };
        await repo.TryUpdateAsync(stateWithModifier, initial.Sequence);

        var pair = FindAdjacentPairThatProducesAMatch(initial.BoardState);
        var swapResult = await service.ExecuteSwapAsync("battle-crit-scope", pair);

        Assert.NotNull(swapResult);
        Assert.True(swapResult.Value.IsAccepted);

        Assert.Equal(bossEffects.Length, swapResult.Value.State.BossState.ActiveStatusEffects.Length);
        Assert.Equal(PetState.DefaultCrit, swapResult.Value.State.PetState.Crit);
        Assert.Equal(initial.PetState.Power, swapResult.Value.State.PetState.Power);
    }

    [Fact]
    public async Task ExecutePetSkillCastAsync_RejectedCast_ConsumesNoModifierAndWritesNothing()
    {
        // CARD_RULES.md §3 item 3 / GAME_STATE.md §5.1.2 item 7: a rejected action is not
        // a resolution, so no modifier is created, refreshed, or consumed, and nothing is
        // written. This is the "CAS/retry mutates nothing on rejection" expectation.
        var (service, repo) = CreateService();
        var initial = await service.CreateBattleAsync("battle-crit-reject", Owner, PetConfig, BossDef);

        var stateWithModifierAndLowPower = initial with
        {
            PetState = initial.PetState with
            {
                Power = 1,
                NextAttackCritModifiers = [new NextAttackCritModifier("card-iron-fang", 10)],
            },
        };
        await repo.TryUpdateAsync(stateWithModifierAndLowPower, initial.Sequence);

        var result = await service.ExecutePetSkillCastAsync("battle-crit-reject");

        Assert.NotNull(result);
        Assert.True(result.Value.IsRejected);

        var stored = await repo.GetAsync("battle-crit-reject");
        Assert.NotNull(stored);
        Assert.Equal(0, stored!.Sequence);

        // The pre-existing modifier survives a rejected action untouched.
        var modifier = Assert.Single(stored.PetState.NextAttackCritModifiers);
        Assert.Equal("card-iron-fang", modifier.SourceIdentity);
        Assert.Equal(1, stored.PetState.Power);
    }

    [Fact]
    public async Task ExecuteSwapAsync_ComposedCritAboveTheCap_StillCritsExactlyOnceAtTheMultiplier()
    {
        // COMBAT_RULES.md §3.3 item 7: the composed value is capped at 100 percentage
        // points, and the cap bounds the STAT — there is no second multiplier for
        // excess. Contributions summing well past 100 must therefore produce a
        // guaranteed Crit at exactly 1.5×, which is what the recorded otherModifiers
        // factor reports (item 6: the Crit outcome's sole representation).
        var (service, repo) = CreateService();
        var initial = await service.CreateBattleAsync("battle-crit-cap", Owner, PetConfig, BossDef);

        var stateOverCap = initial with
        {
            PetState = initial.PetState with
            {
                NextAttackCritModifiers =
                [
                    new NextAttackCritModifier("source-a", 90),
                    new NextAttackCritModifier("source-b", 90),
                ],
            },
        };
        await repo.TryUpdateAsync(stateOverCap, initial.Sequence);

        var pair = FindAdjacentPairThatProducesAMatch(initial.BoardState);
        var swapResult = await service.ExecuteSwapAsync("battle-crit-cap", pair);

        Assert.NotNull(swapResult);
        Assert.True(swapResult.Value.IsAccepted);

        // The player's damage instance is the one immediately preceded by its
        // DamageDealt report with source = player (GAME_EVENTS.md §1 order:
        // DamageCalculated, DamageDealt, DamageTaken). DamageCalculation itself
        // carries no party — the direction lives on the two reports — so the pair is
        // located through the report that does.
        var events = swapResult.Value.Events;
        var playerDamageIndex = -1;
        for (var index = 1; index < events.Count; index++)
        {
            if (events[index].Type == BattleEventType.DamageDealt
                && events[index].DamageDealt.Source == DamageParty.Player)
            {
                playerDamageIndex = index - 1;
                break;
            }
        }

        Assert.True(playerDamageIndex >= 0, "Expected a Player → Boss damage instance.");
        Assert.Equal(BattleEventType.DamageCalculated, events[playerDamageIndex].Type);

        // Base 5 + 90 + 90 = 185 composed, capped to 100, so V < 100 always holds and
        // the multiplier is exactly the documented 1.5× — the cap bounds the stat, with
        // no second multiplier for the excess.
        Assert.Equal(
            DamagePipeline.CritMultiplier,
            events[playerDamageIndex].DamageCalculated.OtherModifiers);

        Assert.Empty(swapResult.Value.State.PetState.NextAttackCritModifiers);
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
