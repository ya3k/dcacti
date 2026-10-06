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
/// MVP Boss Passive Effects tests — <c>GAME_RULES.md</c> §17 step 18a and
/// <c>BOSS_RULES.md</c> §6.2.
/// </summary>
public class BossPassiveEffectsTests
{
    private static readonly BattleStateService.PetConfiguration Pet =
        new(new PetId("pet_instance_1"), Element.Hoa, new PassiveId("xich-lang"), PassiveThreshold: 5);

    private static readonly PlayerId Owner = new("player_boss_passives_owner");

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

    /// <summary>
    /// Swapping (26, 34) — row 3 col 2 with row 4 col 2 — creates a match of the specified gem type.
    /// </summary>
    private static BoardState Match3Board(GemType matchType = GemType.Atk)
    {
        var rows = new[]
        {
            "ADPADPAD",
            "DPADPADP",
            "PADPADPA",
            // row 3: completing gem at col 2
            "PAMMMPAD",
            // row 4: gap at col 2
            "MMDMMDMM",
            "DPADPADP",
            "PADPADPA",
            "ADPADPAD",
        };
        var cells = new GemType[BoardState.CellCount];
        for (var r = 0; r < BoardState.Rows; r++)
        {
            for (var c = 0; c < BoardState.Columns; c++)
            {
                var ch = rows[r][c];
                cells[BoardState.ToIndex(r, c)] = ch switch
                {
                    'M' => matchType,
                    'A' => GemType.Atk,
                    'D' => GemType.Def,
                    'P' => GemType.Power,
                    _ => GemType.Hp,
                };
            }
        }
        return BoardState.FromCells(cells);
    }

    // =======================================================================
    // 1. Hỏa Long — Rage (+20% ATK for 3 turns at Step 18a)
    // =======================================================================

    [Fact]
    public async Task HoaLongRage_ShouldApplyRageStatusEffectAtStep18a()
    {
        var repo = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repo, new FixedRngSeedSource());
        var boss = BossDefinitions.HoaLong with
        {
            PassiveDefinition = BossDefinitions.HoaLong.PassiveDefinition with { Threshold = 1 },
        };

        var created = await service.CreateBattleAsync("battle-rage-1", Owner, Pet, boss);
        var setup = created with { BoardState = Match3Board() };
        await repo.TryUpdateAsync(setup, setup.Sequence);

        var result = await service.ExecuteSwapAsync("battle-rage-1", new SwapRequest(26, 34));
        Assert.NotNull(result);
        Assert.True(result!.Value.IsAccepted);

        var bossState = result.Value.State.BossState;

        // BOSS_RULES.md §6.2.1: Base ATK remains 100, not overwritten
        Assert.Equal(100, bossState.ATK);

        // Rage StatusEffect applied at Step 18a, then decremented at Step 19a (3 -> 2 turns remaining)
        var rage = Assert.Single(bossState.ActiveStatusEffects, e => e.Id == "boss-hoa-long-rage");
        Assert.Equal(StatusEffectType.BuffDebuff, rage.Type);
        Assert.Equal(StatusEffectSource.Boss, rage.Source);
        Assert.Equal(20, rage.Magnitude);
        Assert.Equal("ATK", rage.TargetStat);
        Assert.Equal(2, rage.RemainingTurns); // Decremented from 3 to 2 by step 19a at the end of the turn
    }

    [Fact]
    public async Task HoaLongRage_ReTrigger_ShouldRefreshDurationTo3_WithoutStackingMagnitude()
    {
        var repo = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repo, new FixedRngSeedSource());
        var boss = BossDefinitions.HoaLong with
        {
            PassiveDefinition = BossDefinitions.HoaLong.PassiveDefinition with { Threshold = 1 },
        };

        var created = await service.CreateBattleAsync("battle-rage-refresh", Owner, Pet, boss);
        var setup = created with
        {
            BoardState = Match3Board(),
            // Simulate an already-active Rage with 1 turn remaining
            BossState = created.BossState with
            {
                ActiveStatusEffects =
                [
                    StatusEffect.TurnBased("boss-hoa-long-rage", StatusEffectType.BuffDebuff, StatusEffectSource.Boss, 20, 1, "ATK")
                ]
            }
        };
        await repo.TryUpdateAsync(setup, setup.Sequence);

        var result = await service.ExecuteSwapAsync("battle-rage-refresh", new SwapRequest(26, 34));
        Assert.NotNull(result);

        var bossState = result.Value.State.BossState;

        // Exactly one instance, refreshed to 3 and then decremented to 2 at step 19a
        var rage = Assert.Single(bossState.ActiveStatusEffects, e => e.Id == "boss-hoa-long-rage");
        Assert.Equal(20, rage.Magnitude);
        Assert.Equal(2, rage.RemainingTurns);
    }

    // =======================================================================
    // 2. Mộc Yêu — Regeneration (5% MaxHP at Step 18a)
    // =======================================================================

    [Fact]
    public async Task MocYeuRegen_ShouldHealFivePercentMaxHpAtStep18a()
    {
        var repo = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repo, new FixedRngSeedSource());
        var boss = BossDefinitions.MocYeu with
        {
            PassiveDefinition = BossDefinitions.MocYeu.PassiveDefinition with { Threshold = 1 },
        };

        var created = await service.CreateBattleAsync("battle-regen-1", Owner, Pet, boss);

        // Put Boss at 3000 HP (out of 5000)
        var setup = created with
        {
            BoardState = Match3Board(),
            BossState = created.BossState with { HP = 3000 },
        };
        await repo.TryUpdateAsync(setup, setup.Sequence);

        var result = await service.ExecuteSwapAsync("battle-regen-1", new SwapRequest(26, 34));
        Assert.NotNull(result);

        var playerDmg = result!.Value.Events
            .First(e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Player)
            .DamageDealt.Amount;

        // 5% of 5000 = 250 regen
        var expectedHp = 3000 - playerDmg + 250;
        Assert.Equal(expectedHp, result.Value.State.BossState.HP);

        // No StatusEffect created
        Assert.Empty(result.Value.State.BossState.ActiveStatusEffects);
    }

    [Fact]
    public async Task MocYeuRegen_ShouldClampToMaxHp()
    {
        var repo = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repo, new FixedRngSeedSource());
        var boss = BossDefinitions.MocYeu with
        {
            PassiveDefinition = BossDefinitions.MocYeu.PassiveDefinition with { Threshold = 1 },
        };

        var created = await service.CreateBattleAsync("battle-regen-clamp", Owner, Pet, boss);
        var setup = created with
        {
            BoardState = Match3Board(),
            // HP almost full (4990) and high DEF so player dmg is 1
            BossState = created.BossState with { HP = 4990, DEF = 1000 },
        };
        await repo.TryUpdateAsync(setup, setup.Sequence);

        var result = await service.ExecuteSwapAsync("battle-regen-clamp", new SwapRequest(26, 34));
        Assert.NotNull(result);

        // Clamped at MaxHP 5000
        Assert.Equal(5000, result!.Value.State.BossState.HP);
    }

    // =======================================================================
    // 3. Thủy Ma — Healing Reduction (-50% for 3 turns, Battle Start)
    // =======================================================================

    [Fact]
    public async Task ThuyMaHealingReduction_ShouldBePresentInInitialBossStateAtBattleStart()
    {
        var repo = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repo, new FixedRngSeedSource());
        var boss = BossDefinitions.ThuyMa;

        var created = await service.CreateBattleAsync("battle-thuy-ma-init", Owner, Pet, boss);

        var effect = Assert.Single(created.BossState.ActiveStatusEffects);
        Assert.Equal("boss-thuy-ma-heal", effect.Id);
        Assert.Equal(StatusEffectType.BuffDebuff, effect.Type);
        Assert.Equal(StatusEffectSource.Boss, effect.Source);
        Assert.Equal(-50, effect.Magnitude);
        Assert.Equal(3, effect.RemainingTurns);
        Assert.Null(effect.TargetStat);
    }

    [Fact]
    public void ThuyMaHealingReduction_StatusEffectValidation_OmitsTargetStat_AndForbidsNonNull()
    {
        // 1. Calling TurnBased without TargetStat for boss-thuy-ma-heal succeeds with TargetStat absent
        var effect = StatusEffect.TurnBased(
            "boss-thuy-ma-heal",
            StatusEffectType.BuffDebuff,
            StatusEffectSource.Boss,
            magnitude: -50,
            duration: 3);

        Assert.Equal("boss-thuy-ma-heal", effect.Id);
        Assert.Null(effect.TargetStat);

        // 2. Calling TurnBased with any TargetStat for boss-thuy-ma-heal throws ArgumentException
        Assert.Throws<ArgumentException>(() =>
            StatusEffect.TurnBased(
                "boss-thuy-ma-heal",
                StatusEffectType.BuffDebuff,
                StatusEffectSource.Boss,
                magnitude: -50,
                duration: 3,
                targetStat: "HEAL"));

        Assert.Throws<ArgumentException>(() =>
            StatusEffect.TurnBased(
                "boss-thuy-ma-heal",
                StatusEffectType.BuffDebuff,
                StatusEffectSource.Boss,
                magnitude: -50,
                duration: 3,
                targetStat: "ATK"));

        // 3. Stat-targeted BuffDebuffs (like Root or Rage) require TargetStat — calling without throws
        Assert.Throws<ArgumentException>(() =>
            StatusEffect.TurnBased(
                "boss-hoa-long-rage",
                StatusEffectType.BuffDebuff,
                StatusEffectSource.Boss,
                magnitude: 20,
                duration: 3));

        Assert.Throws<ArgumentException>(() =>
            StatusEffect.TurnBased(
                "Root",
                StatusEffectType.BuffDebuff,
                StatusEffectSource.Boss,
                magnitude: -30,
                duration: 2));

        // 4. Non-BuffDebuffs (DoT, State) forbid TargetStat
        Assert.Throws<ArgumentException>(() =>
            StatusEffect.TurnBased(
                "Burn",
                StatusEffectType.DoT,
                StatusEffectSource.Boss,
                magnitude: 50,
                duration: 2,
                targetStat: "ATK"));
    }

    [Fact]
    public async Task ThuyMaHealingReduction_Serialization_ShouldOmitTargetStatKeyCompletely()
    {
        var repo = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repo, new FixedRngSeedSource());
        var boss = BossDefinitions.ThuyMa;

        var created = await service.CreateBattleAsync("battle-thuy-ma-serial", Owner, Pet, boss);

        // Serialize state to JSON
        var json = GameServer.Domain.Battle.Serialization.BattleStateSerializer.Serialize(created);

        // 1. Raw JSON must NOT contain "targetStat":null anywhere
        Assert.DoesNotContain("\"targetStat\":null", json, StringComparison.Ordinal);

        // 2. The serialized StatusEffect element for boss-thuy-ma-heal must omit "targetStat" completely
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var bossEffects = doc.RootElement
            .GetProperty("bossState")
            .GetProperty("statusEffects");

        var thuyMaElement = bossEffects.EnumerateArray().Single(e => e.GetProperty("id").GetString() == "boss-thuy-ma-heal");
        Assert.False(thuyMaElement.TryGetProperty("targetStat", out _), "targetStat property must be completely absent in JSON");

        // 3. Deserializing and re-serializing preserves absence
        var restored = GameServer.Domain.Battle.Serialization.BattleStateSerializer.Deserialize(json);
        var restoredEffect = Assert.Single(restored.BossState.ActiveStatusEffects);
        Assert.Equal("boss-thuy-ma-heal", restoredEffect.Id);
        Assert.Null(restoredEffect.TargetStat);

        var roundTripJson = GameServer.Domain.Battle.Serialization.BattleStateSerializer.Serialize(restored);
        Assert.DoesNotContain("\"targetStat\"", roundTripJson, StringComparison.Ordinal);
        Assert.Equal(json, roundTripJson);
    }

    [Fact]
    public async Task ThuyMaHealingReduction_ShouldReducePetGemHealingBy50Percent()
    {
        var repo = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repo, new FixedRngSeedSource());
        var boss = BossDefinitions.ThuyMa;

        var created = await service.CreateBattleAsync("battle-thuy-ma-gem-heal", Owner, Pet, boss);

        // Put Pet at 500 HP (MaxHP = 1000, DEF = 1000 to isolate healing) and board with HP gem match (HealPool = 60)
        var setup = created with
        {
            BoardState = Match3Board(GemType.Hp),
            PetState = created.PetState with { HP = 500, MaxHP = 1000, DEF = 1000 },
            BossState = created.BossState with { ATK = 0 }, // 0 ATK so no boss damage obscures healing
        };
        await repo.TryUpdateAsync(setup, setup.Sequence);

        var result = await service.ExecuteSwapAsync("battle-thuy-ma-gem-heal", new SwapRequest(26, 34));
        Assert.NotNull(result);

        Assert.True(result!.Value.Resources.HealPool > 0, "HP gems generated healing");
        var expectedHeal = (result.Value.Resources.HealPool * 50) / 100;
        var bossDmg = result.Value.Events
            .Where(e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Boss)
            .Sum(e => e.DamageDealt.Amount);
        Assert.Equal(500 + expectedHeal - bossDmg, result.Value.State.PetState.HP);
    }

    [Fact]
    public async Task ThuyMaHealingReduction_ShouldReducePetCardHealingBy50Percent()
    {
        var repo = new InMemoryBattleStateRepository();
        var healCard = new CardDefinition
        {
            CardDefinitionId = "card_heal_1",
            Name = "Heal Card",
            Category = CardCategory.Basic,
            PowerCost = 10,
            LoadoutCopyLimit = 1,
            EffectDefinition = CardEffectDefinitions.Create(
                CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.Flat, 100)),
        };
        var cards = new StubCardDefinitionLookup(healCard);

        var service = new BattleStateService(repo, new FixedRngSeedSource(), cardDefinitions: cards);
        var boss = BossDefinitions.ThuyMa;

        var petConfig = Pet with
        {
            EquippedCards = [new EquippedCardIdentity("card_heal_1")]
        };

        var created = await service.CreateBattleAsync("battle-thuy-ma-card-heal", Owner, petConfig, boss);

        var setup = created with
        {
            PetState = created.PetState with { HP = 500, MaxHP = 1000, Power = 50 },
        };
        await repo.TryUpdateAsync(setup, setup.Sequence);

        var castResult = await service.ExecuteCardCastAsync("battle-thuy-ma-card-heal", "card_heal_1");
        Assert.NotNull(castResult);
        Assert.False(castResult!.Value.IsRejected);

        // Raw heal = 100. -50% reduction from active Thủy Ma => 50.
        // HP becomes 500 + 50 = 550.
        Assert.Equal(550, castResult.Value.State.PetState.HP);
    }

    [Fact]
    public async Task ThuyMaHealingReduction_ShouldExpireAfterTurn3Step19a_RestoringNormalHealing()
    {
        var repo = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repo, new FixedRngSeedSource());
        var boss = BossDefinitions.ThuyMa;

        var created = await service.CreateBattleAsync("battle-thuy-ma-expiry", Owner, Pet, boss);

        // Simulate reaching Turn 3 with 1 remaining turn on boss-thuy-ma-heal
        var setup = created with
        {
            Turn = 3,
            BoardState = Match3Board(GemType.Hp),
            PetState = created.PetState with { HP = 500, MaxHP = 1000, DEF = 1000 },
            BossState = created.BossState with
            {
                ATK = 0,
                ActiveStatusEffects =
                [
                    StatusEffect.TurnBased("boss-thuy-ma-heal", StatusEffectType.BuffDebuff, StatusEffectSource.Boss, -50, 1)
                ]
            }
        };
        await repo.TryUpdateAsync(setup, setup.Sequence);

        // Turn 3 swap: healing is reduced by 50% during the turn,
        // and at Step 19a, the effect decrements 1 -> 0 and expires!
        var turn3Result = await service.ExecuteSwapAsync("battle-thuy-ma-expiry", new SwapRequest(26, 34));
        Assert.NotNull(turn3Result);
        Assert.True(turn3Result!.Value.Resources.HealPool > 0);
        var expectedTurn3Heal = (turn3Result.Value.Resources.HealPool * 50) / 100;
        var bossDmg3 = turn3Result.Value.Events
            .Where(e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Boss)
            .Sum(e => e.DamageDealt.Amount);
        Assert.Equal(500 + expectedTurn3Heal - bossDmg3, turn3Result.Value.State.PetState.HP);

        // After Step 19a, boss-thuy-ma-heal is expired and removed
        Assert.Empty(turn3Result.Value.State.BossState.ActiveStatusEffects);

        // Setup Turn 4 swap with another HP match
        var setupTurn4 = turn3Result.Value.State with
        {
            BoardState = Match3Board(GemType.Hp),
            LastCommittedSwapPair = null,
            PetState = turn3Result.Value.State.PetState with { HP = 500 }, // Reset to 500 to clearly measure Turn 4 heal
        };
        await repo.TryUpdateAsync(setupTurn4, setupTurn4.Sequence);

        // Turn 4 swap: effect is gone, healing is at 100%
        var turn4Result = await service.ExecuteSwapAsync("battle-thuy-ma-expiry", new SwapRequest(26, 34));
        Assert.NotNull(turn4Result);
        Assert.True(turn4Result!.Value.Resources.HealPool > 0);
        var bossDmg4 = turn4Result.Value.Events
            .Where(e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Boss)
            .Sum(e => e.DamageDealt.Amount);
        Assert.Equal(500 + turn4Result.Value.Resources.HealPool - bossDmg4, turn4Result.Value.State.PetState.HP);
    }

    // =======================================================================
    // 4. TASK-200 — Mộc Yêu Threshold 8 Regression Suite
    // =======================================================================

    [Fact]
    public async Task TASK200_MocYeuRegen_ShouldNotTriggerBeforeEightQualifyingMatches()
    {
        var repo = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repo, new FixedRngSeedSource());
        var boss = BossDefinitions.MocYeu;

        Assert.Equal(8, boss.PassiveThreshold);

        var created = await service.CreateBattleAsync("task200-under-thresh", Owner, Pet, boss);
        var setup = created with
        {
            BoardState = Match3Board(),
            BossState = created.BossState with
            {
                HP = 3000,
                // Match3Board() swap (26,34) produces 7 matches.
                // Setting initial Current to 0 produces 0 + 7 = 7 matches (< 8).
                PassiveProgress = new PassiveProgress(Current: 0, Threshold: 8),
            },
        };
        await repo.TryUpdateAsync(setup, setup.Sequence);

        var result = await service.ExecuteSwapAsync("task200-under-thresh", new SwapRequest(26, 34));
        Assert.NotNull(result);
        Assert.True(result!.Value.IsAccepted);

        var bossState = result.Value.State.BossState;
        Assert.Equal(7, bossState.PassiveProgress.Current);
        Assert.Equal(8, bossState.PassiveProgress.Threshold);

        // No PassiveTriggered event emitted
        Assert.DoesNotContain(result.Value.Events, e => e.Type == BattleEventType.PassiveTriggered && e.PassiveTriggered.PassiveId.Value == "boss-moc-yeu-regen");

        // Boss HP does NOT receive 250 HP regen
        var playerDmg = result.Value.Events
            .First(e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Player)
            .DamageDealt.Amount;
        Assert.Equal(3000 - playerDmg, bossState.HP);
    }

    [Fact]
    public async Task TASK200_MocYeuRegen_ShouldTriggerAtExactThresholdOfEightMatches()
    {
        var repo = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repo, new FixedRngSeedSource());
        var boss = BossDefinitions.MocYeu;

        var created = await service.CreateBattleAsync("task200-exact-thresh", Owner, Pet, boss);
        var setup = created with
        {
            BoardState = Match3Board(),
            BossState = created.BossState with
            {
                HP = 3000,
                // Match3Board() swap (26,34) produces 7 matches.
                // Setting initial Current to 1 produces 1 + 7 = 8 matches (== 8) -> triggers!
                PassiveProgress = new PassiveProgress(Current: 1, Threshold: 8),
            },
        };
        await repo.TryUpdateAsync(setup, setup.Sequence);

        var result = await service.ExecuteSwapAsync("task200-exact-thresh", new SwapRequest(26, 34));
        Assert.NotNull(result);
        Assert.True(result!.Value.IsAccepted);

        var bossState = result.Value.State.BossState;
        // Default reset behavior resets to 0
        Assert.Equal(0, bossState.PassiveProgress.Current);
        Assert.Equal(8, bossState.PassiveProgress.Threshold);

        // PassiveTriggered event emitted for boss-moc-yeu-regen
        Assert.Contains(result.Value.Events, e => e.Type == BattleEventType.PassiveTriggered && e.PassiveTriggered.PassiveId.Value == "boss-moc-yeu-regen");

        // Boss HP receives exactly 5% MaxHP (250 HP)
        var playerDmg = result.Value.Events
            .First(e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Player)
            .DamageDealt.Amount;
        Assert.Equal(3000 - playerDmg + 250, bossState.HP);
    }

    [Fact]
    public async Task TASK200_MocYeuRegen_Magnitude_RemainsExactlyFivePercentMaxHp()
    {
        var repo = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repo, new FixedRngSeedSource());
        var boss = BossDefinitions.MocYeu;

        var created = await service.CreateBattleAsync("task200-magnitude", Owner, Pet, boss);
        var setup = created with
        {
            BoardState = Match3Board(),
            BossState = created.BossState with
            {
                HP = 2000,
                PassiveProgress = new PassiveProgress(Current: 1, Threshold: 8),
            },
        };
        await repo.TryUpdateAsync(setup, setup.Sequence);

        var result = await service.ExecuteSwapAsync("task200-magnitude", new SwapRequest(26, 34));
        Assert.NotNull(result);

        var playerDmg = result!.Value.Events
            .First(e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Player)
            .DamageDealt.Amount;

        // 5000 * 5 / 100 = 250 exactly
        const int expectedRegen = 250;
        Assert.Equal(2000 - playerDmg + expectedRegen, result.Value.State.BossState.HP);
    }

    [Fact]
    public async Task TASK200_MocYeuRegen_RepeatedThresholds_ShouldBehaveDeterministically()
    {
        var repo = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repo, new FixedRngSeedSource());
        var boss = BossDefinitions.MocYeu;

        var created = await service.CreateBattleAsync("task200-repeated", Owner, Pet, boss);

        // Cycle 1: 1 + 7 -> 8 (trigger 1)
        var setup1 = created with
        {
            BoardState = Match3Board(),
            BossState = created.BossState with
            {
                HP = 3000,
                PassiveProgress = new PassiveProgress(Current: 1, Threshold: 8),
            },
        };
        await repo.TryUpdateAsync(setup1, setup1.Sequence);
        var res1 = await service.ExecuteSwapAsync("task200-repeated", new SwapRequest(26, 34));
        Assert.NotNull(res1);
        Assert.Equal(0, res1!.Value.State.BossState.PassiveProgress.Current);

        // Cycle 2: set to 1 again with fresh board state and LastCommittedSwapPair = null
        var setup2 = res1.Value.State with
        {
            BoardState = Match3Board(),
            LastCommittedSwapPair = null,
            BossState = res1.Value.State.BossState with
            {
                PassiveProgress = new PassiveProgress(Current: 1, Threshold: 8),
            },
        };
        await repo.TryUpdateAsync(setup2, setup2.Sequence);
        var res2 = await service.ExecuteSwapAsync("task200-repeated", new SwapRequest(26, 34));
        Assert.NotNull(res2);
        Assert.Equal(0, res2!.Value.State.BossState.PassiveProgress.Current);
        Assert.Contains(res2.Value.Events, e => e.Type == BattleEventType.PassiveTriggered && e.PassiveTriggered.PassiveId.Value == "boss-moc-yeu-regen");
    }

    [Fact]
    public async Task TASK200_MocYeuRegen_TurnAndSequenceSemantics_RemainUnchanged()
    {
        var repo = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repo, new FixedRngSeedSource());
        var boss = BossDefinitions.MocYeu;

        var created = await service.CreateBattleAsync("task200-turn-seq", Owner, Pet, boss);
        Assert.Equal(0, created.Sequence);
        Assert.Equal(0, created.Turn);

        var setup = created with { BoardState = Match3Board() };
        await repo.TryUpdateAsync(setup, setup.Sequence);

        var result = await service.ExecuteSwapAsync("task200-turn-seq", new SwapRequest(26, 34));
        Assert.NotNull(result);
        Assert.True(result!.Value.IsAccepted);

        // Turn increments by 1; Sequence increments by 1
        Assert.Equal(1, result.Value.State.Turn);
        Assert.Equal(1, result.Value.State.Sequence);
    }

    [Fact]
    public async Task TASK200_MocYeuRegen_RejectedSwap_ShouldProduceZeroProgressAndZeroRegen()
    {
        var repo = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repo, new FixedRngSeedSource());
        var boss = BossDefinitions.MocYeu;

        var created = await service.CreateBattleAsync("task200-rejected", Owner, Pet, boss);
        var initialBossHp = created.BossState.HP;
        var initialProgress = created.BossState.PassiveProgress;

        // Invalid non-adjacent swap
        var result = await service.ExecuteSwapAsync("task200-rejected", new SwapRequest(0, 7));
        Assert.NotNull(result);
        Assert.False(result!.Value.IsAccepted);

        var current = await repo.GetAsync("task200-rejected");
        Assert.NotNull(current);
        Assert.Equal(0, current!.Sequence);
        Assert.Equal(0, current.Turn);
        Assert.Equal(initialBossHp, current.BossState.HP);
        Assert.Equal(initialProgress.Current, current.BossState.PassiveProgress.Current);
    }

    [Fact]
    public async Task TASK200_MocYeuRegen_TerminalResolution_RemainsCorrect()
    {
        var repo = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repo, new FixedRngSeedSource());
        var boss = BossDefinitions.MocYeu;

        var created = await service.CreateBattleAsync("task200-terminal", Owner, Pet, boss);
        var setup = created with
        {
            BoardState = Match3Board(),
            BossState = created.BossState with
            {
                HP = 1, // 1 HP remaining — will die from player match
                DEF = 0,
                PassiveProgress = new PassiveProgress(Current: 7, Threshold: 8), // Threshold reaches 8 on this swap
            },
        };
        await repo.TryUpdateAsync(setup, setup.Sequence);

        var result = await service.ExecuteSwapAsync("task200-terminal", new SwapRequest(26, 34));
        Assert.NotNull(result);
        Assert.True(result!.Value.IsAccepted);

        // Boss reached 0 HP: Battle ends in BattleWon; regeneration does NOT revive the boss
        var wonEvent = Assert.Single(result.Value.Events, e => e.Type == BattleEventType.BattleWon);
        Assert.Equal(0, wonEvent.BattleWon.FinalBossHp);
        Assert.Equal(0, result.Value.State.BossState.HP);

        // No PassiveTriggered event on terminal path (GAME_RULES.md §17 step 18 skipped on boss death)
        Assert.DoesNotContain(result.Value.Events, e => e.Type == BattleEventType.PassiveTriggered && e.PassiveTriggered.PassiveId.Value == "boss-moc-yeu-regen");
    }
}
