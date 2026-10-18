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
        // BOSS_RULES.md §6.2.3: the Boss heals 5% of MaxHP per trigger, truncated, at
        // GAME_RULES.md §17 step 18b. Two battles carry the SAME pinned Turn fixture and
        // the SAME Boss HP and differ only in whether the Passive's Threshold is
        // reachable: the firing arm's Threshold is 1 (any batch reaches it) and the
        // control's is 1000 (no batch can), so the HP difference between them IS the
        // §6.2.3 regen — the round's own Player damage and the Boss's own step-18a HP-Gem
        // healing are identical in both arms and cancel.
        //
        // Deriving the value this way is what ADR-026 makes necessary: §17 orders 18a
        // (the Boss's own board action, which heals it from its HP Gems) before 18b, so
        // "3000 − damage + regen" is no longer the whole movement.
        var repo = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repo, new FixedRngSeedSource());

        var firing = await ResolveMocYeuTurnAsync(
            repo,
            service,
            "battle-regen-1",
            boss: BossDefinitions.MocYeu with
            {
                PassiveDefinition = BossDefinitions.MocYeu.PassiveDefinition with { Threshold = 1 },
            },
            bossHp: 3000,
            progressCurrent: 0);

        var control = await ResolveMocYeuTurnAsync(
            repo,
            service,
            "battle-regen-1-control",
            boss: BossDefinitions.MocYeu with
            {
                PassiveDefinition = BossDefinitions.MocYeu.PassiveDefinition with { Threshold = 1000 },
            },
            bossHp: 3000,
            progressCurrent: 0);

        Assert.Contains(
            firing.Events,
            e => e.Type == BattleEventType.PassiveTriggered
                && e.PassiveTriggered.PassiveId.Value == "boss-moc-yeu-regen");
        Assert.DoesNotContain(firing.Events, e => e.Type == BattleEventType.BattleWon);

        Assert.DoesNotContain(
            control.Events,
            e => e.Type == BattleEventType.PassiveTriggered
                && e.PassiveTriggered.PassiveId.Value == "boss-moc-yeu-regen");

        // 5% of 5000 = 250: the ONLY difference between the two arms.
        Assert.Equal(5000, firing.SettledBoss.MaxHP);
        Assert.Equal(250, firing.SettledBoss.HP - control.SettledBoss.HP);

        // No StatusEffect is created by the regen: the firing arm's Boss effect
        // collection is exactly the control's (both carry whatever this Turn's step 18a
        // produced from the Boss's own Gems, and nothing more).
        Assert.True(
            StatusEffectLifecycle.EffectsEqual(
                control.SettledBoss.ActiveStatusEffects,
                firing.SettledBoss.ActiveStatusEffects),
            "§6.2.3's regen is an HP mutation and creates no Status Effect instance");
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

        // COMBAT_RULES.md §4 items 2–3 / §5.4.6 (TASK-299): the DEF Gems this same
        // action cleared grant the Pet a temporary DEF *buff* (raising the effective
        // DEF the Boss's instance is mitigated by), not a Shield absorption pool —
        // so nothing is absorbed and the reported Boss damage is the whole HP
        // reduction. The Pet's effective DEF is already pinned to 1000, so the buff
        // only makes the mitigation slightly stronger.
        var netBossDamage = bossDmg;

        Assert.Equal(500 + expectedHeal - netBossDamage, result.Value.State.PetState.HP);
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

        // COMBAT_RULES.md §4 items 2–3 / §5.4.6 (TASK-299): the DEF Gems this same
        // action cleared grant the Pet a temporary DEF buff, not a Shield absorption
        // pool, so nothing is absorbed and the reported Boss damage is the whole HP
        // reduction.
        var netBossDamage3 = bossDmg3;

        // COMBAT_RULES.md §4 item 1: healing restores HP up to Max HP and the overheal is
        // discarded, so the settled value is the heal less the Boss's unabsorbed instance,
        // bounded by the 1000 pinned here.
        Assert.Equal(
            Math.Min(500 + expectedTurn3Heal - netBossDamage3, 1000),
            turn3Result.Value.State.PetState.HP);

        // After Step 19a, boss-thuy-ma-heal is expired and removed. The assertion is
        // scoped to that instance: §17 step 18a's actor-relative effects put whatever the
        // Boss's own board action produced (its DEF Gems' Shield, its HP Gems' healing)
        // on the same collection in the same round, and none of those is this Passive's
        // healing reduction.
        Assert.DoesNotContain(
            turn3Result.Value.State.BossState.ActiveStatusEffects,
            e => e.Id == "boss-thuy-ma-heal");

        // Setup Turn 4 swap with another HP match
        var setupTurn4 = turn3Result.Value.State with
        {
            BoardState = Match3Board(GemType.Hp),
            LastCommittedSwapPair = null,
            PetState = turn3Result.Value.State.PetState with { HP = 500 }, // Reset to 500 to clearly measure Turn 4 heal
        };
        await repo.TryUpdateAsync(setupTurn4, setupTurn4.Sequence);

        // Turn 4 swap: effect is gone, healing is at 100% — and COMBAT_RULES.md §4
        // item 1's MaxHP ceiling still bounds the restored HP, because the Boss's own
        // step-18a board action advances the RngState, so this Turn's spawned board can
        // cascade far more HP Gems than the Turn-3 board did.
        var turn4Result = await service.ExecuteSwapAsync("battle-thuy-ma-expiry", new SwapRequest(26, 34));
        Assert.NotNull(turn4Result);
        Assert.True(turn4Result!.Value.Resources.HealPool > 0);
        var bossDmg4 = turn4Result.Value.Events
            .Where(e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Boss)
            .Sum(e => e.DamageDealt.Amount);

        // COMBAT_RULES.md §4 items 2–3 / §5.4.6 (TASK-299): the DEF Gems this action
        // cleared grant the Pet a temporary DEF buff, not a Shield absorption pool, so
        // nothing is absorbed and the reported Boss damage is the whole HP reduction.
        var netBossDamage4 = bossDmg4;

        Assert.Equal(
            Math.Min(500 + turn4Result.Value.Resources.HealPool - netBossDamage4, 1000),
            turn4Result.Value.State.PetState.HP);
    }

    // =======================================================================
    // 4. TASK-200 — Mộc Yêu Threshold 8 Regression Suite
    // =======================================================================

    [Fact]
    public async Task TASK200_MocYeuRegen_ShouldNotTriggerBeforeEightQualifyingMatches()
    {
        var repo = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repo, new FixedRngSeedSource());

        Assert.Equal(8, BossDefinitions.MocYeu.PassiveThreshold);

        // The un-triggered arm: progress starts at 0 against the documented Threshold 8,
        // and the Turn's own batch is asserted to be short of it rather than assumed to
        // be.
        var below = await ResolveMocYeuTurnAsync(
            repo,
            service,
            "task200-under-thresh",
            BossDefinitions.MocYeu,
            bossHp: 3000,
            progressCurrent: 0);

        Assert.True(
            below.PlayerMatches < 8,
            $"the fixture Turn must produce fewer than 8 Player Matches but produced {below.PlayerMatches}");

        // §2 item 1 / §2 item 3: below the Threshold nothing resets, so the settled
        // progress is the accumulated batch.
        Assert.Equal(below.PlayerMatches, below.SettledBoss.PassiveProgress.Current);
        Assert.Equal(8, below.SettledBoss.PassiveProgress.Threshold);

        // No PassiveTriggered event emitted
        Assert.DoesNotContain(below.Events, e => e.Type == BattleEventType.PassiveTriggered && e.PassiveTriggered.PassiveId.Value == "boss-moc-yeu-regen");

        // The positive control: the same pinned Turn with progress pinned to the exact
        // crossing (8 − the batch). Its HP is the below arm's plus §6.2.3's one 250-HP
        // regen and nothing else — which is what makes "the Boss HP does NOT receive 250
        // HP regen" below an assertion about the Passive rather than about the Turn's
        // other movements.
        var crossing = await ResolveMocYeuTurnAsync(
            repo,
            service,
            "task200-under-thresh-crossing",
            BossDefinitions.MocYeu,
            bossHp: 3000,
            progressCurrent: 8 - below.PlayerMatches);

        Assert.Contains(
            crossing.Events,
            e => e.Type == BattleEventType.PassiveTriggered
                && e.PassiveTriggered.PassiveId.Value == "boss-moc-yeu-regen");

        Assert.Equal(250, crossing.SettledBoss.HP - below.SettledBoss.HP);

        // Boss HP does NOT receive 250 HP regen on the below arm: it is the crossing
        // arm's HP less that same regen, with every other movement identical.
        Assert.Equal(crossing.SettledBoss.HP - 250, below.SettledBoss.HP);
    }

    [Fact]
    public async Task TASK200_MocYeuRegen_ShouldTriggerAtExactThresholdOfEightMatches()
    {
        var repo = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repo, new FixedRngSeedSource());

        // The exact crossing is arranged from the Turn's OWN batch: the un-triggered arm
        // reports how many Player Matches this pinned Turn produced, and the crossing arm
        // pins progress to exactly 8 − that batch. So the batch carries progress to 8 and
        // no further — the boundary §2 item 3's single post-batch evaluation turns on.
        var below = await ResolveMocYeuTurnAsync(
            repo,
            service,
            "task200-exact-thresh",
            BossDefinitions.MocYeu,
            bossHp: 3000,
            progressCurrent: 0);

        Assert.True(
            below.PlayerMatches < 8,
            $"the fixture Turn must produce fewer than 8 Player Matches but produced {below.PlayerMatches}");

        var crossing = await ResolveMocYeuTurnAsync(
            repo,
            service,
            "task200-exact-thresh-crossing",
            BossDefinitions.MocYeu,
            bossHp: 3000,
            progressCurrent: 8 - below.PlayerMatches);

        var bossState = crossing.SettledBoss;

        // Default reset behavior resets to 0
        Assert.Equal(0, bossState.PassiveProgress.Current);
        Assert.Equal(8, bossState.PassiveProgress.Threshold);

        // PassiveTriggered event emitted for boss-moc-yeu-regen, reporting the
        // pre-reset progress at the crossing (GAME_EVENTS.md §2 item 2).
        var triggered = Assert.Single(
            crossing.Events,
            e => e.Type == BattleEventType.PassiveTriggered
                && e.PassiveTriggered.PassiveId.Value == "boss-moc-yeu-regen");

        Assert.Equal(8, triggered.PassiveTriggered.Progress);

        // Boss HP receives exactly 5% MaxHP (250 HP) — measured against the identical
        // un-triggered arm, so the Turn's own damage and step-18a healing cancel.
        Assert.Equal(250, crossing.SettledBoss.HP - below.SettledBoss.HP);
    }

    [Fact]
    public async Task TASK200_MocYeuRegen_Magnitude_RemainsExactlyFivePercentMaxHp()
    {
        var repo = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repo, new FixedRngSeedSource());

        var below = await ResolveMocYeuTurnAsync(
            repo,
            service,
            "task200-magnitude",
            BossDefinitions.MocYeu,
            bossHp: 2000,
            progressCurrent: 0);

        var crossing = await ResolveMocYeuTurnAsync(
            repo,
            service,
            "task200-magnitude-crossing",
            BossDefinitions.MocYeu,
            bossHp: 2000,
            progressCurrent: 8 - below.PlayerMatches);

        // BOSS_RULES.md §6.2.3: 5000 * 5 / 100 = 250 exactly, truncated toward zero and
        // clamped to MaxHP. The two arms share every other movement of the Turn, so the
        // difference is the regen itself.
        const int expectedRegen = 250;

        Assert.Equal(5000, crossing.SettledBoss.MaxHP);
        Assert.Equal(expectedRegen, crossing.SettledBoss.HP - below.SettledBoss.HP);

        // And the settled value is the pinned HP moved by that regen plus the Turn's own
        // (Boss healing − Player damage), which the un-triggered arm reports exactly.
        Assert.Equal(
            below.SettledBoss.HP + expectedRegen,
            crossing.SettledBoss.HP);
    }

    [Fact]
    public async Task TASK200_MocYeuRegen_RepeatedThresholds_ShouldBehaveDeterministically()
    {
        var repo = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repo, new FixedRngSeedSource());

        // Cycle 1: progress pinned AT the Threshold, so the Turn's own batch carries it
        // across the single post-batch evaluation and the Default reset
        // (PASSIVE_RULES.md §4 item 1 / §2 item 3) settles it at 0.
        var cycle1 = await ResolveMocYeuTurnAsync(
            repo,
            service,
            "task200-repeated",
            BossDefinitions.MocYeu,
            bossHp: 3000,
            progressCurrent: 8);

        Assert.Equal(0, cycle1.SettledBoss.PassiveProgress.Current);
        Assert.Contains(
            cycle1.Events,
            e => e.Type == BattleEventType.PassiveTriggered
                && e.PassiveTriggered.PassiveId.Value == "boss-moc-yeu-regen");

        // Cycle 2: the same battle's next Turn, re-pinned to the same documented
        // arrangement — a fresh Turn fixture (the committed pair may not repeat,
        // MATCH3_RULES.md §2.1.4) and progress at the Threshold again. The crossing is
        // therefore arranged identically rather than left to whatever the advanced RNG
        // stream happened to spawn.
        var cycle2 = await ResolveLaterMocYeuTurnAsync(
            repo,
            service,
            "task200-repeated",
            bossHp: 3000,
            progressCurrent: 8);

        Assert.Equal(0, cycle2.SettledBoss.PassiveProgress.Current);
        Assert.Contains(
            cycle2.Events,
            e => e.Type == BattleEventType.PassiveTriggered
                && e.PassiveTriggered.PassiveId.Value == "boss-moc-yeu-regen");

        // Both cycles produced §6.2.3's one 250-HP regen. Each cycle is compared against
        // a control battle with the SAME history whose Passive progress is pinned below
        // the crossing, so the two arms differ in nothing but whether the regen fired.
        //
        // Cycle 1's control is a first Turn at progress 0; cycle 2's must replay cycle 1
        // as well, because a Turn's own HP movement depends on the RngState the previous
        // Turn's Boss phase left behind.
        var control1 = await ResolveMocYeuTurnAsync(
            repo,
            service,
            "task200-repeated-control",
            BossDefinitions.MocYeu,
            bossHp: 3000,
            progressCurrent: 0);

        Assert.Equal(250, cycle1.SettledBoss.HP - control1.SettledBoss.HP);

        var control2 = await ResolveLaterMocYeuTurnAsync(
            repo,
            service,
            "task200-repeated-control",
            bossHp: 3000,
            progressCurrent: 0);

        Assert.DoesNotContain(
            control2.Events,
            e => e.Type == BattleEventType.PassiveTriggered
                && e.PassiveTriggered.PassiveId.Value == "boss-moc-yeu-regen");

        Assert.Equal(250, cycle2.SettledBoss.HP - control2.SettledBoss.HP);
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

    // =======================================================================
    // Mộc Yêu Turn harness — ADR-026's pinned board and the step-18b HP point
    // =======================================================================

    /// <summary>
    /// One committed Mộc Yêu Turn: the settled Boss state, the round's events, the
    /// Player Match count its Cascade produced, and the Player → Boss damage it dealt.
    /// </summary>
    /// <param name="SettledBoss">The Boss state the Turn settled at.</param>
    /// <param name="Events">The round's ordered events (<c>GAME_EVENTS.md</c> §1).</param>
    /// <param name="PlayerMatches">
    /// The Cascade's own Match total (<c>MATCH3_RULES.md</c> §3 item 5) — the batch
    /// <c>PASSIVE_RULES.md</c> §2 item 1 charges the Boss Passive with.
    /// </param>
    /// <param name="PlayerDamage">
    /// The Player → Boss instance's damage, read from the round's own reports. It is the
    /// Player phase's contribution to the HP movement a Turn reports.
    /// </param>
    private readonly record struct MocYeuTurn(
        BossState SettledBoss,
        IReadOnlyList<BattleEvent> Events,
        int PlayerMatches,
        int PlayerDamage);

    /// <summary>
    /// Creates a Mộc Yêu battle and resolves one pinned Turn of it.
    /// </summary>
    /// <param name="repository">The battle's active-state store.</param>
    /// <param name="service">The pipeline under test.</param>
    /// <param name="battleId">The battle session to create and resolve.</param>
    /// <param name="boss">The Boss definition the Turn is fought against.</param>
    /// <param name="bossHp">The documented state value the Boss's HP is pinned to.</param>
    /// <param name="progressCurrent">
    /// The Boss Passive's progress entering the Turn, against the definition's own
    /// Threshold. <c>PASSIVE_RULES.md</c> §2 item 3 evaluates the Threshold once, after
    /// the whole batch, so pinning this value is what makes an exact crossing (or a
    /// deliberate non-crossing) a property of the scenario.
    /// </param>
    private static async Task<MocYeuTurn> ResolveMocYeuTurnAsync(
        InMemoryBattleStateRepository repository,
        BattleStateService service,
        string battleId,
        BossDefinition boss,
        int bossHp,
        int progressCurrent)
    {
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        return await ResolvePinnedTurnAsync(repository, service, battleId, created, bossHp, progressCurrent);
    }

    /// <summary>
    /// Resolves one further Turn of an existing Mộc Yêu battle, on a fresh Turn fixture
    /// (the committed pair may not repeat — <c>MATCH3_RULES.md</c> §2.1.4) and with the
    /// Boss HP and Passive progress re-pinned to the same documented arrangement.
    /// </summary>
    private static async Task<MocYeuTurn> ResolveLaterMocYeuTurnAsync(
        InMemoryBattleStateRepository repository,
        BattleStateService service,
        string battleId,
        int bossHp,
        int progressCurrent)
    {
        var stored = (await repository.GetAsync(battleId))!;

        return await ResolvePinnedTurnAsync(repository, service, battleId, stored, bossHp, progressCurrent);
    }

    /// <summary>
    /// Pins the Turn's board fixture, the Boss HP, and the Boss Passive's progress on the
    /// stored battle, then resolves the committed Swap.
    ///
    /// <b>Why the board is pinned.</b> <c>ADR-026</c> D1 calculates and applies the
    /// Player's damage only when the committed Swap cleared at least one ATK Gem, so the
    /// fixture pins the self-validating ATK board
    /// (<see cref="RoundTestSupport.PlayerMatch"/>) and verifies its precondition here. A
    /// board drawn from the battle's own RNG leaves the Turn's Player part uncontrolled.
    ///
    /// <b>Why the HP is pinned, and why two arms are compared.</b>
    /// <c>GAME_RULES.md</c> §17 orders 18a (the Boss's own board action, whose HP Gems
    /// heal it) before 18b (the Passive's evaluation), so a Turn's settled HP is the pin
    /// moved by that Turn's own (Player damage − Boss healing) plus any regen. Two arms
    /// pinned identically differ by the regen alone, which is what makes the §6.2.3
    /// magnitude measurable without re-deriving the Turn's other movements.
    /// </summary>
    private static async Task<MocYeuTurn> ResolvePinnedTurnAsync(
        InMemoryBattleStateRepository repository,
        BattleStateService service,
        string battleId,
        BattleState state,
        int bossHp,
        int progressCurrent)
    {
        var pinned = RoundTestSupport.PlayerMatch(
            GemType.Atk,
            state.RngState,
            state.LastCommittedSwapPair);

        Assert.True(
            pinned.Resources.ClearedGemCounts.Atk > 0,
            "the ADR-026 fixture must clear ATK Gems for the Player's attack to resolve");

        await RoundTestSupport.PinAsync(
            repository,
            state,
            pinned.Board,
            bossState: state.BossState with
            {
                HP = bossHp,
                PassiveProgress = new PassiveProgress(
                    Threshold: state.BossState.PassiveProgress.Threshold,
                    Current: progressCurrent),
            });

        var result = await service.ExecuteSwapAsync(battleId, pinned.Swap);

        Assert.True(result!.Value.IsAccepted, $"{battleId} was rejected");

        return new MocYeuTurn(
            result.Value.State.BossState,
            result.Value.Events,
            result.Value.Resolution.TotalMatches,
            result.Value.Events
                .Where(e => e.Type == BattleEventType.DamageDealt
                    && e.DamageDealt.Source == DamageParty.Player)
                .Sum(e => e.DamageDealt.Amount));
    }
}
