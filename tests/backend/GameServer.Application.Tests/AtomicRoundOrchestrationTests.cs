using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Battle.Serialization;
using GameServer.Domain.Bosses;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Domain.Relics;
using Xunit;

namespace GameServer.Application.Tests;

/// <summary>
/// Atomic round orchestration and Redis state persistence — <c>TASK-261</c>,
/// <c>ADR-026</c>, <c>GAME_RULES.md</c> §2 and §17, <c>GAME_STATE.md</c> §5.1,
/// <c>MATCH3_RULES.md</c> §8, <c>BOSS_RULES.md</c> §6.3, <c>COMBAT_RULES.md</c> §2–§4.
///
/// <code>
/// Rule (ADR-026 / GAME_RULES.md §17)
///  ↓
/// Scenario (Given a pinned board and a committed Player Swap, When the round
///           resolves, Then the documented state and events)
///  ↓
/// Test
/// </code>
///
/// <b>One round is one atomic transaction.</b> The Player action phase, the Boss
/// response phase, the step 19a Status Effect pass, and the two counters are
/// resolved in local working state and committed by one <c>Sequence</c>
/// compare-and-set (<c>ADR-026</c> D4, <c>REDIS_STATE.md</c> §4).
///
/// <b>Every expectation is read from an authoritative document, never from the
/// implementation.</b> The Gem type a Swap clears is pinned by construction
/// (<see cref="RoundTestSupport"/>), the Boss's own Match-3 cycle is read from the
/// committed event list by position, and every magnitude is the Boss definition's
/// declared one.
/// </summary>
public class AtomicRoundOrchestrationTests
{
    private static readonly BattleStateService.PetConfiguration Pet =
        new(new PetId("pet_instance_1"), Element.Hoa, new PassiveId("xich-lang"), PassiveThreshold: 5);

    private static readonly PlayerId Owner = new("player_atomic_round_owner");

    // =======================================================================
    // 1. Player ATK match damages the Boss; a Player non-ATK match does not
    //    (ADR-026 D1, COMBAT_RULES.md §3)
    // =======================================================================

    [Fact]
    public async Task PlayerAtkMatch_ShouldDamageTheBossThroughTheGatedPipeline()
    {
        // ADR-026 D1 / COMBAT_RULES.md §3: the Player's ATK-Gem match is the
        // qualifying attack that lets the ATK stat enter the Damage Pipeline.
        //
        // The Pet's ATK is pinned high enough that the instance's damage exceeds every
        // Heal pool the Boss's own HP Gems could generate in the same round (64 cells
        // x 20 per HP Gem x the 3.0x Match-6+ tier = 3840, the largest possible), so
        // "the Boss's HP fell" is a statement about the attack rather than about the
        // Boss's later step 18a healing.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        const string battleId = "round-player-atk";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.HoaLong);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Atk, created.RngState, created.LastCommittedSwapPair);
        Assert.True(pinned.Resources.ClearedGemCounts.Atk > 0);

        var petWithHighAtk = created.PetState with { ATK = 20_000 };

        await RoundTestSupport.PinAsync(repository, created, pinned.Board, petState: petWithHighAtk);

        var result = (await service.ExecuteSwapAsync(battleId, pinned.Swap))!.Value;

        Assert.True(result.IsAccepted);

        var playerDealt = Assert.Single(
            result.Events,
            e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Player);

        Assert.Equal(DamageParty.Boss, playerDealt.DamageDealt.Target);
        Assert.True(playerDealt.DamageDealt.Amount > 0);

        // GAME_EVENTS.md §1: the three reports describe one instance, so they carry
        // one Final Damage value (whether or not it was terminal for the Boss).
        var calculatedIndex = IndexOfNth(result, BattleEventType.DamageCalculated, 0);
        Assert.Equal(BattleEventType.DamageDealt, result.Events[calculatedIndex + 1].Type);
        Assert.Equal(BattleEventType.DamageTaken, result.Events[calculatedIndex + 2].Type);

        var calculated = result.Events[calculatedIndex].DamageCalculated;
        var taken = result.Events[calculatedIndex + 2].DamageTaken;

        Assert.Equal(calculated.FinalDamage, playerDealt.DamageDealt.Amount);
        Assert.Equal(calculated.FinalDamage, taken.Amount);
        Assert.Equal(DamageParty.Boss, taken.Target);

        // The Boss's HP fell (the instance is lethal here, so the Boss phase is
        // skipped outright), and the committed state is what the round reported
        // (GAME_STATE.md §5.1).
        Assert.True(result.State.BossState.HP < created.BossState.HP);
        Assert.Equal(result.State.BossState.HP, (await service.GetBattleAsync(battleId))!.BossState.HP);
    }

    [Theory]
    [InlineData(GemType.Def)]
    [InlineData(GemType.Hp)]
    [InlineData(GemType.Power)]
    public async Task PlayerNonAtkMatch_ShouldDealNoDamageAndPreserveNextAttackModifiers(GemType matchType)
    {
        // ADR-026 D1 / COMBAT_RULES.md §3: with zero cleared ATK Gems the Damage
        // Pipeline does not execute for the acting side — no player-sourced
        // DamageCalculated/DamageDealt/DamageTaken is emitted, the Boss's HP is
        // untouched by the Player, and an attack modifier that would have been
        // consumed by a qualifying attack is preserved unconsumed (§3.3 item 8).
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var battleId = $"round-player-non-atk-{matchType}";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.HoaLong);

        var pinned = RoundTestSupport.PlayerMatch(matchType, created.RngState, created.LastCommittedSwapPair);
        Assert.Equal(0, pinned.Resources.ClearedGemCounts.Atk);

        // A NextAttack-lifetime modifier in force when the non-ATK Swap resolves.
        var modifier = new ATKModifier("round-test-next-attack", 25, RelicEffectLifetime.NextAttack);
        var petWithModifier = created.PetState with { ATKModifiers = [modifier] };

        await RoundTestSupport.PinAsync(repository, created, pinned.Board, petState: petWithModifier);

        var result = (await service.ExecuteSwapAsync(battleId, pinned.Swap))!.Value;

        Assert.True(result.IsAccepted);

        Assert.DoesNotContain(
            result.Events,
            e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Player);
        Assert.DoesNotContain(
            result.Events,
            e => e.Type == BattleEventType.DamageTaken && e.DamageTaken.Target == DamageParty.Boss);

        // Only the Player's attack is gated here: the Boss phase may still have
        // damaged the Pet, so the Boss's HP is what the gate is read on, and the
        // round may hold exactly the Boss's own instance — never a second one.
        var bossDamageInstances = result.Events.Count(
            e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Boss);

        Assert.Equal(
            bossDamageInstances,
            result.Events.Count(e => e.Type == BattleEventType.DamageCalculated));
        Assert.Equal(created.BossState.HP, result.State.BossState.HP);

        // The modifier survives: it composed into no attack.
        Assert.Contains(result.State.PetState.ATKModifiers, m => m.SourceIdentity == "round-test-next-attack");
    }

    // =======================================================================
    // 2. Boss ATK match damages the Pet; a Boss non-ATK match does not
    //    (ADR-026 D1, GAME_RULES.md §17 step 18d)
    // =======================================================================

    [Fact]
    public async Task BossBoardAction_ShouldDamageThePet_ExactlyWhenItClearedAnAtkGem()
    {
        // GAME_RULES.md §17 step 18d / ADR-026 D1: the Boss deals attack damage iff
        // its own swap produced a confirmed ATK-Gem match. The legacy fallback basic
        // attack is retired, so a round in which the Boss cleared no ATK Gem
        // produces no Boss-sourced damage instance at all.
        //
        // The property is asserted over several rounds: whichever branch the Boss's
        // deterministic move selection lands on, the equivalence must hold.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var boss = BossDefinitions.HoaLong with
        {
            // No Skill fires in this scenario: the Skill's own (non-damaging) effect
            // must not be confused with the ATK-gated instance under test.
            SkillDefinition = BossDefinitions.HoaLong.SkillDefinition with { ChargeRequirement = 1000 },
        };

        const string battleId = "round-boss-atk-gate";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var state = created;
        var sawAtkMatch = false;
        var sawNonAtkMatch = false;

        for (var round = 0; round < 8; round++)
        {
            var pinned = RoundTestSupport.PlayerMatch(
                round % 2 == 0 ? GemType.Def : GemType.Hp,
                state.RngState,
                state.LastCommittedSwapPair);

            await RoundTestSupport.PinAsync(repository, state, pinned.Board);

            var result = (await service.ExecuteSwapAsync(battleId, pinned.Swap))!.Value;
            Assert.True(result.IsAccepted);

            var bossClearedAtk = RoundTestSupport.BossClearedAtkGem(result);
            var bossDamageInstances = result.Events.Count(
                e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Boss);

            // Exactly one instance when the gate opened, none when it did not.
            Assert.Equal(bossClearedAtk ? 1 : 0, bossDamageInstances);

            if (bossClearedAtk)
            {
                sawAtkMatch = true;
            }
            else
            {
                sawNonAtkMatch = true;
            }

            state = result.State;

            if (state.PetState.HP <= 0)
            {
                break;
            }
        }

        // Coverage: the scenario must actually have exercised both branches, or the
        // equivalence above would be vacuous for one of them.
        Assert.True(sawAtkMatch, "the Boss must have produced at least one ATK match across the rounds");
        Assert.True(sawNonAtkMatch, "the Boss must have produced at least one non-ATK match across the rounds");
    }

    // =======================================================================
    // 3–4. SkillCharge: Boss POWER match groups only, +1 each, clamped
    //      (ADR-026 D2, T1, T2; BOSS_RULES.md §6.3)
    // =======================================================================

    [Fact]
    public async Task PlayerPowerMatch_ShouldNotChargeTheBoss()
    {
        // ADR-026 D2 / GAME_STATE.md §2.4.3: "it increments exclusively from POWER
        // gems matched by the Boss during the Boss's turn (player matches never
        // charge the Boss)".
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var boss = BossDefinitions.HoaLong with
        {
            SkillDefinition = BossDefinitions.HoaLong.SkillDefinition with { ChargeRequirement = 1000 },
        };

        const string battleId = "round-player-power-no-charge";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Power, created.RngState, created.LastCommittedSwapPair);
        Assert.True(pinned.Resources.ClearedGemCounts.Power > 0);

        await RoundTestSupport.PinAsync(repository, created, pinned.Board);

        var result = (await service.ExecuteSwapAsync(battleId, pinned.Swap))!.Value;
        Assert.True(result.IsAccepted);

        // The Player's POWER Gems charged the Pet's Power (GAME_RULES.md §17 step 13)
        // and reached the Boss's counter in no way at all: the Boss's charge is only
        // ever what the Boss's own POWER matches produced (ADR-026 D2).
        Assert.Equal(
            RoundTestSupport.BossPowerMatchGroups(result),
            result.State.BossState.SkillCharge);
        Assert.True(result.State.PetState.Power > created.PetState.Power);
    }

    [Fact]
    public async Task BossPowerMatchGroups_ShouldChargeOneEach_AndClampAtTheChargeRequirement()
    {
        // ADR-026 T1/T2 + D2: +1 SkillCharge per Boss POWER match group, clamped at
        // the Skill's ChargeRequirement. The group count is read from the Boss's own
        // MatchCreated reports, which is the unit
        // ResourceGenerator.CountPowerMatchGroups counts.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var boss = BossDefinitions.HoaLong with
        {
            // Unreachable requirement: the Skill never fires, so the accumulated
            // charge is observable rather than reset.
            SkillDefinition = BossDefinitions.HoaLong.SkillDefinition with { ChargeRequirement = 100_000 },
            // Both sides are given room to survive the observation window: this
            // scenario asserts the counter, not who wins.
            MaxHP = 1_000_000,
        };

        const string battleId = "round-boss-power-charge";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var state = created with
        {
            PetState = created.PetState with { HP = 1_000_000, MaxHP = 1_000_000 },
        };
        await RoundTestSupport.PinAsync(repository, created, created.BoardState, petState: state.PetState);

        var sawPowerGroups = false;

        for (var round = 0; round < 24; round++)
        {
            var pinned = RoundTestSupport.PlayerMatch(
                GemType.Def,
                state.RngState,
                state.LastCommittedSwapPair);

            await RoundTestSupport.PinAsync(repository, state, pinned.Board);

            var previousCharge = state.BossState.SkillCharge;
            var result = (await service.ExecuteSwapAsync(battleId, pinned.Swap))!.Value;
            Assert.True(result.IsAccepted);

            var groups = RoundTestSupport.BossPowerMatchGroups(result);
            var expected = Math.Min(previousCharge + groups, boss.SkillChargeRequirement);

            Assert.Equal(expected, result.State.BossState.SkillCharge);

            sawPowerGroups |= groups > 0;
            state = result.State;

            if (state.BossState.HP <= 0 || state.PetState.HP <= 0)
            {
                break;
            }
        }

        Assert.True(sawPowerGroups, "the Boss must have matched POWER Gems at least once across the rounds");

        // The clamp: with an unreachable requirement the counter never exceeds it,
        // and with a requirement of 1 the counter is held at exactly 1 — it is a
        // floor-of-the-requirement value, never the accumulated total
        // (ADR-026 T2).
        var clamped = BossDefinitions.HoaLong with
        {
            SkillDefinition = BossDefinitions.HoaLong.SkillDefinition with { ChargeRequirement = 1 },
        };

        var clampedRepository = new InMemoryBattleStateRepository();
        var clampedService = new BattleStateService(clampedRepository, new FixedRngSeedSource());
        const string clampedBattleId = "round-boss-power-clamp";
        var clampedCreated = await clampedService.CreateBattleAsync(clampedBattleId, Owner, Pet, clamped);

        var clampedPinned = RoundTestSupport.PlayerMatch(GemType.Def, clampedCreated.RngState, clampedCreated.LastCommittedSwapPair);

        // A running cooldown keeps the Skill from firing, so the clamped charge is
        // what the round leaves observable rather than a reset 0.
        await RoundTestSupport.PinAsync(
            clampedRepository,
            clampedCreated,
            clampedPinned.Board,
            bossState: clampedCreated.BossState with { SkillCharge = 1, SkillCooldown = 2 });

        var clampedResult = (await clampedService.ExecuteSwapAsync(clampedBattleId, clampedPinned.Swap))!.Value;
        Assert.True(clampedResult.IsAccepted);

        Assert.DoesNotContain(clampedResult.Events, e => e.Type == BattleEventType.BossSkillCast);
        Assert.Equal(
            Math.Min(1 + RoundTestSupport.BossPowerMatchGroups(clampedResult), 1),
            clampedResult.State.BossState.SkillCharge);
        Assert.Equal(1, clampedResult.State.BossState.SkillCharge);
    }

    // =======================================================================
    // 5. The five MVP Boss skills apply their approved secondary effect and no
    //    direct damage (ADR-026 D3, D5, D6, D7; BOSS_RULES.md §6.3.1)
    // =======================================================================

    [Fact]
    public async Task FlameBurst_ShouldApplyATemporaryBossAtkBuff_AndNoDirectDamage()
    {
        // ADR-026 D5 (Option 5B) / BOSS_RULES.md §6.3.1 item 1: Flame Burst's direct
        // damage and Burn DoT are removed and replaced by a temporary Boss ATK buff
        // on BossState.ActiveStatusEffects.
        var (result, definition) = await FireTheSkillAsync(
            "round-skill-flame-burst",
            BossDefinitions.HoaLong);

        var buff = Assert.Single(result.State.BossState.ActiveStatusEffects, e => e.Id == "flame-burst");

        Assert.Equal(StatusEffectType.BuffDebuff, buff.Type);
        Assert.Equal(StatusEffectSource.Boss, buff.Source);
        Assert.Equal("ATK", buff.TargetStat);
        Assert.Equal(50, buff.Magnitude);

        // Applied at step 18c and ticked by step 19a in the same round: the second
        // and last of its two Turns of duration is what remains.
        Assert.Equal(1, buff.RemainingTurns);

        // Retired: no Burn instance, of either owner (ADR-026 D3/D5).
        Assert.DoesNotContain(result.State.PetState.ActiveStatusEffects, e => e.Type == StatusEffectType.DoT);
        Assert.DoesNotContain(result.State.BossState.ActiveStatusEffects, e => e.Type == StatusEffectType.DoT);

        AssertNoDirectSkillDamage(result);
        Assert.Equal(0, definition.SkillBaseDamage);
    }

    [Fact]
    public async Task DrainPower_ShouldSubtractFlatPetPower_AndCreateNoStatusEffect()
    {
        // BOSS_RULES.md §6.3.1 item 2 / GAME_STATE.md §2.3.1 item 9: an instant flat
        // Pet Power reduction that creates no Status Effect instance.
        var (result, definition) = await FireTheSkillAsync(
            "round-skill-drain-power",
            BossDefinitions.ThuyMa,
            petPower: 30);

        var drain = Assert.Single(
            result.Events,
            e => e.Type == BattleEventType.PowerChanged && e.PowerChanged.Source == PowerChangeSource.Boss);

        Assert.Equal(-20, drain.PowerChanged.Delta);
        Assert.Equal(10, drain.PowerChanged.Power);
        Assert.Equal(10, result.State.PetState.Power);

        Assert.DoesNotContain(
            result.State.PetState.ActiveStatusEffects,
            e => e.Source == StatusEffectSource.Boss);

        AssertNoDirectSkillDamage(result);
        Assert.Equal(0, definition.SkillBaseDamage);
    }

    [Fact]
    public async Task Root_ShouldApplyAMinusThirtyPercentPetAtkDebuffForTwoTurns()
    {
        // BOSS_RULES.md §6.3.1 item 3: a Turn-based BuffDebuff applied to the active
        // Pet with TargetStat "ATK", magnitude 30, duration 2 Turns.
        var (result, definition) = await FireTheSkillAsync(
            "round-skill-root",
            BossDefinitions.MocYeu);

        var root = Assert.Single(result.State.PetState.ActiveStatusEffects, e => e.Id == "Root");

        Assert.Equal(StatusEffectType.BuffDebuff, root.Type);
        Assert.Equal(StatusEffectSource.Boss, root.Source);
        Assert.Equal("ATK", root.TargetStat);
        Assert.Equal(30, root.Magnitude);

        // Applied at step 18c: step 19a's 2 -> 1 is the first of its two documented
        // consumption Turns (COMBAT_RULES.md §5.3.2).
        Assert.Equal(1, root.RemainingTurns);

        // COMBAT_RULES.md §5.4: the stored stat is never written.
        Assert.Equal(Pet.ToPetState().ATK, result.State.PetState.ATK);

        AssertNoDirectSkillDamage(result);
        Assert.Equal(0, definition.SkillBaseDamage);
    }

    [Fact]
    public async Task Earthquake_ShouldGrantTheBossATemporaryShield_AndNoDirectDamage()
    {
        // ADR-026 D6 (Option 2A) / BOSS_RULES.md §6.3.1 item 4: a temporary Shield on
        // the Boss's own ActiveStatusEffects — the same trigger-based absorption pool
        // COMBAT_RULES.md §4 defines.
        var (result, definition) = await FireTheSkillAsync(
            "round-skill-earthquake",
            BossDefinitions.SonThachVe);

        var shield = Assert.Single(result.State.BossState.ActiveStatusEffects, e => e.Type == StatusEffectType.Shield);

        Assert.Equal(StatusEffectSource.Boss, shield.Source);
        Assert.Equal(150, shield.Magnitude);
        Assert.Equal(StatusEffect.ShieldDepletedCondition, shield.ExpiryCondition);

        // A trigger-based instance is not on the step 19a countdown
        // (GAME_STATE.md §5.1.1 item 7).
        Assert.Null(shield.RemainingTurns);

        AssertNoDirectSkillDamage(result);
        Assert.Equal(0, definition.SkillBaseDamage);
    }

    [Fact]
    public async Task ThunderStrike_ShouldBuffTheNextQualifyingBossAttack_AndBeConsumedOnlyByIt()
    {
        // ADR-026 D7 (Option 3A) / BOSS_RULES.md §6.3.1 item 5: a trigger-based
        // modifier buffing the Boss's next qualifying ATK match. It carries the
        // "NextQualifyingAttack" expiry condition, so it is not ticked by step 19a —
        // it survives every round in which the Boss matched no ATK Gem and is
        // consumed by the round in which the Boss's §18d instance resolves.
        var (result, definition) = await FireTheSkillAsync(
            "round-skill-thunder-strike",
            BossDefinitions.KimLoiVuong);

        var bossQualified = RoundTestSupport.BossClearedAtkGem(result);
        var hasBuff = result.State.BossState.ActiveStatusEffects.Any(e => e.Id == "thunder-strike");

        if (bossQualified)
        {
            // Consumed by the attack instance this same round resolved
            // (AttackResolution.ResolveAndApplyBossAttack).
            Assert.False(hasBuff);
        }
        else
        {
            Assert.True(hasBuff);

            var buff = result.State.BossState.ActiveStatusEffects
                .First(e => e.Id == "thunder-strike");

            Assert.Equal(StatusEffectType.BuffDebuff, buff.Type);
            Assert.Equal(StatusEffectSource.Boss, buff.Source);
            Assert.Equal("ATK", buff.TargetStat);
            Assert.Equal(50, buff.Magnitude);
            Assert.Equal(StatusEffect.NextQualifyingAttackCondition, buff.ExpiryCondition);

            // Not on the step 19a countdown (GAME_STATE.md §5.1.1 item 7).
            Assert.Null(buff.RemainingTurns);
        }

        AssertNoDirectSkillDamage(result);
        Assert.Equal(0, definition.SkillBaseDamage);

        // The invariant over the rounds that follow: whatever is present at the end
        // of a round cannot have survived a qualifying Boss attack — the modifier is
        // consumed exactly by the attack it buffs.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        const string battleId = "round-skill-thunder-strike-follow-up";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.KimLoiVuong);
        var state = created;

        for (var round = 0; round < 6; round++)
        {
            var pinned = RoundTestSupport.PlayerMatch(
                GemType.Def,
                state.RngState,
                state.LastCommittedSwapPair);

            await RoundTestSupport.PinAsync(repository, state, pinned.Board);

            var followUp = (await service.ExecuteSwapAsync(battleId, pinned.Swap))!.Value;
            Assert.True(followUp.IsAccepted);

            var stillActive = followUp.State.BossState.ActiveStatusEffects
                .Any(e => e.Id == "thunder-strike");

            if (stillActive)
            {
                Assert.False(
                    RoundTestSupport.BossClearedAtkGem(followUp),
                    "a qualifying Boss attack consumes the next-qualifying modifier in the same round");
            }

            state = followUp.State;

            if (state.BossState.HP <= 0 || state.PetState.HP <= 0)
            {
                break;
            }
        }
    }

    [Fact]
    public async Task FiredBossSkill_ShouldNeverProduceAnAttackInstanceOfItsOwn()
    {
        // ADR-026 D3 / COMBAT_RULES.md §3.4: all five MVP Skills declare
        // BaseDamage = 0, so a Boss's only damage instance is the ATK-gated board
        // instance. Asserted for the whole MVP set at once.
        foreach (var definition in BossDefinitions.All)
        {
            var (result, _) = await FireTheSkillAsync($"round-skill-no-damage-{definition.SkillId}", definition);

            Assert.Contains(result.Events, e => e.Type == BattleEventType.BossSkillCast);
            AssertNoDirectSkillDamage(result);
        }
    }

    // =======================================================================
    // 6–7. Terminal outcomes (GAME_RULES.md §1.4)
    // =======================================================================

    [Fact]
    public async Task PlayerKillingTheBoss_ShouldSkipTheBossPhase_AndCommitAWin()
    {
        // GAME_RULES.md §1.4 / BOSS_RULES.md §5 item 4: a Boss the Player just
        // killed triggers no Passive, casts no Skill, and makes no attack — the
        // Boss phase is skipped and the round commits BattleWon.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var boss = BossDefinitions.HoaLong with
        {
            MaxHP = 100,
            SkillDefinition = BossDefinitions.HoaLong.SkillDefinition with { ChargeRequirement = 1 },
        };

        const string battleId = "round-player-kills-boss";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Atk, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(repository, created, pinned.Board, bossState: created.BossState with { HP = 1 });

        var result = (await service.ExecuteSwapAsync(battleId, pinned.Swap))!.Value;

        Assert.True(result.IsAccepted);
        Assert.Equal(0, result.State.BossState.HP);

        var won = Assert.Single(result.Events, e => e.Type == BattleEventType.BattleWon);
        Assert.Equal(0, won.BattleWon.FinalBossHp);

        Assert.DoesNotContain(result.Events, e => e.Type == BattleEventType.BossSkillCast);
        Assert.DoesNotContain(result.Events, e => e.Type == BattleEventType.PassiveCharged
            && e.PassiveCharged.Source == PassiveEventSource.Boss);

        // The skipped Boss phase produced no Boss board cycle either.
        Assert.Empty(RoundTestSupport.BossBoardEvents(result));

        // The outcome is the last event of the round, and the committed state is
        // the terminal one (GAME_STATE.md §5.1).
        Assert.Equal(BattleEventType.BattleWon, result.Events[^1].Type);
        Assert.Equal(0, (await service.GetBattleAsync(battleId))!.BossState.HP);
    }

    [Fact]
    public async Task BossKillingThePet_ShouldCommitALoss()
    {
        // GAME_RULES.md §1.4: the battle ends when either side reaches 0 HP. The Boss
        // can only reduce the Pet through its ATK-gated instance (ADR-026 D1), so the
        // scenario runs rounds until that instance lands on a Pet with 1 HP.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var boss = BossDefinitions.HoaLong with
        {
            // Unreachable by the Player: this scenario is about the Pet dying, not
            // about who wins the race.
            MaxHP = 100_000,
            SkillDefinition = BossDefinitions.HoaLong.SkillDefinition with { ChargeRequirement = 1000 },
        };

        const string battleId = "round-boss-kills-pet";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var state = created with { PetState = created.PetState with { HP = 1, MaxHP = 1000 } };
        await RoundTestSupport.PinAsync(repository, created, created.BoardState, petState: state.PetState);

        var lost = false;

        for (var round = 0; round < 40 && !lost; round++)
        {
            var pinned = RoundTestSupport.PlayerMatch(
                round % 2 == 0 ? GemType.Def : GemType.Hp,
                state.RngState,
                state.LastCommittedSwapPair);

            await RoundTestSupport.PinAsync(repository, state, pinned.Board);

            var result = (await service.ExecuteSwapAsync(battleId, pinned.Swap))!.Value;
            Assert.True(result.IsAccepted);

            state = result.State;

            if (result.Events.Any(e => e.Type == BattleEventType.BattleLost))
            {
                var battleLost = result.Events.Single(e => e.Type == BattleEventType.BattleLost);

                Assert.Equal(0, battleLost.BattleLost.FinalPlayerHp);
                Assert.Equal(0, result.State.PetState.HP);
                Assert.DoesNotContain(result.Events, e => e.Type == BattleEventType.BattleWon);

                // The outcome is the resolution's last statement.
                Assert.Equal(BattleEventType.BattleLost, result.Events[^1].Type);

                lost = true;
            }
        }

        Assert.True(lost, "the Boss's ATK-gated instance must be able to defeat a 1-HP Pet within 40 rounds");
        Assert.Equal(0, (await service.GetBattleAsync(battleId))!.PetState.HP);
    }

    // =======================================================================
    // 8–9. Persistence: an exception or a lost compare-and-set persists nothing
    //      (ADR-026 D4, REDIS_STATE.md §4)
    // =======================================================================

    [Fact]
    public async Task ExceptionBeforeTheCommit_ShouldLeaveThePersistedStateUnchanged()
    {
        // ADR-026 D4: "If CAS fails or an exception occurs prior to commit, zero
        // state is persisted." A store that raises on the write therefore leaves the
        // authoritative record exactly as the round read it.
        var store = new InMemoryBattleStateRepository();
        var throwing = new ThrowingBattleStateRepository(store);
        var service = new BattleStateService(throwing, new FixedRngSeedSource());

        const string battleId = "round-store-failure";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.HoaLong);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Atk, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(store, created, pinned.Board);

        var before = (await store.GetAsync(battleId))!;

        throwing.FailWrites = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExecuteSwapAsync(battleId, pinned.Swap));

        var after = (await store.GetAsync(battleId))!;

        Assert.Equal(before.Sequence, after.Sequence);
        Assert.Equal(before.Turn, after.Turn);
        Assert.Equal(before.BossState.HP, after.BossState.HP);
        Assert.Equal(before.PetState.HP, after.PetState.HP);
        Assert.True(before.BoardState.CellsEqual(after.BoardState));
    }

    [Fact]
    public async Task LostCompareAndSet_ShouldPersistNothingAndReportNoCommittedRound()
    {
        // REDIS_STATE.md §4 items 2–3: every write this round attempts is refused, so
        // the record keeps its pre-round value and the caller receives no accepted
        // result — and therefore no events for an uncommitted attempt.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        const string battleId = "round-cas-never-settles";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.HoaLong);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Atk, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(repository, created, pinned.Board);

        var before = (await repository.GetAsync(battleId))!;
        var writesBefore = repository.WriteCount;

        repository.ForcedConflicts = int.MaxValue;

        var result = await service.ExecuteSwapAsync(
            battleId,
            new SwapRequest(pinned.Swap.From, pinned.Swap.To, ExpectedSequence: before.Sequence, ActionId: "cas-round"));

        Assert.Null(result);

        var after = (await repository.GetAsync(battleId))!;

        Assert.Equal(before.Sequence, after.Sequence);
        Assert.Equal(before.Turn, after.Turn);
        Assert.Equal(before.BossState.HP, after.BossState.HP);
        Assert.True(result is null);
        Assert.Equal(writesBefore, repository.WriteCount);
    }
    [Fact]
    public async Task ConcurrentCommit_ShouldSurviveTheRoundsLostCompareAndSet()
    {
        // The documented conflict case: another resolution moved the stored Sequence
        // between this round's read and its write. The loser writes nothing at all —
        // no partial board, no advanced Turn, and no events — and its caller receives
        // the stale-action rejection (MATCH3_RULES.md §2.1.4, REDIS_STATE.md §4
        // items 2–3, 7).
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        const string battleId = "round-cas-conflict";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.HoaLong);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Atk, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(repository, created, pinned.Board);

        // The concurrent winner: a state one Sequence ahead, which the round must not
        // be able to overwrite.
        var winner = created with
        {
            Sequence = created.Sequence + 1,
            Turn = created.Turn + 1,
            BossState = created.BossState with { HP = created.BossState.HP - 7 },
        };
        repository.ApplyConcurrentWrite(winner);

        var result = await service.ExecuteSwapAsync(
            battleId,
            new SwapRequest(pinned.Swap.From, pinned.Swap.To, ExpectedSequence: created.Sequence, ActionId: "conflict-round"));

        Assert.NotNull(result);
        Assert.True(result!.Value.IsRejected);
        Assert.Equal(SwapRejectionReason.StaleAction, result.Value.Reason);
        Assert.Empty(result.Value.Events);

        var after = (await repository.GetAsync(battleId))!;

        Assert.Equal(winner.Sequence, after.Sequence);
        Assert.Equal(winner.Turn, after.Turn);
        Assert.Equal(winner.BossState.HP, after.BossState.HP);
    }

    // =======================================================================
    // 10–11. Idempotency and the state precondition (ADR-025)
    // =======================================================================

    [Fact]
    public async Task DuplicateActionId_ShouldNotExecuteTheRoundASecondTime()
    {
        // ADR-025 §2 / SIGNALR_PROTOCOL.md §3.1 item 6: a retried action id is
        // recognised as a replay — it returns the acceptance response without
        // re-executing the round, re-advancing Turn/Sequence, writing the record, or
        // emitting a second set of events.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        const string battleId = "round-duplicate-action";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.HoaLong);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Atk, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(repository, created, pinned.Board);

        var request = new SwapRequest(
            pinned.Swap.From,
            pinned.Swap.To,
            ExpectedSequence: created.Sequence,
            ActionId: "duplicate-round-action");

        var first = (await service.ExecuteSwapAsync(battleId, request))!.Value;
        Assert.True(first.IsAccepted);
        Assert.False(first.IsIdempotentReplay);

        var writesAfterFirst = repository.WriteCount;
        var committed = (await repository.GetAsync(battleId))!;

        var retry = (await service.ExecuteSwapAsync(battleId, request))!.Value;

        Assert.True(retry.IsAccepted);
        Assert.True(retry.IsIdempotentReplay);
        Assert.Empty(retry.Events);

        var afterRetry = (await repository.GetAsync(battleId))!;

        Assert.Equal(committed.Turn, afterRetry.Turn);
        Assert.Equal(committed.Sequence, afterRetry.Sequence);
        Assert.Equal(committed.BossState.HP, afterRetry.BossState.HP);
        Assert.Equal(writesAfterFirst, repository.WriteCount);
    }

    [Fact]
    public async Task StaleExpectedSequence_ShouldBeRejectedWithoutWriting()
    {
        // MATCH3_RULES.md §2.1.4 / ADR-025 §1: a request whose observed state version
        // is not the stored one is a stale action — it is rejected and writes nothing.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        const string battleId = "round-stale-sequence";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.HoaLong);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Atk, created.RngState, created.LastCommittedSwapPair);
        await RoundTestSupport.PinAsync(repository, created, pinned.Board);

        var writesBefore = repository.WriteCount;

        var result = (await service.ExecuteSwapAsync(
            battleId,
            new SwapRequest(
                pinned.Swap.From,
                pinned.Swap.To,
                ExpectedSequence: created.Sequence + 1,
                ActionId: "stale-round-action")))!.Value;

        Assert.True(result.IsRejected);
        Assert.Equal(SwapRejectionReason.StaleAction, result.Reason);
        Assert.Empty(result.Events);
        Assert.Equal(writesBefore, repository.WriteCount);

        var after = (await repository.GetAsync(battleId))!;
        Assert.Equal(created.Sequence, after.Sequence);
        Assert.Equal(created.Turn, after.Turn);
    }

    // =======================================================================
    // 12. One round advances Turn and Sequence exactly once (ADR-026 D4)
    // =======================================================================

    [Fact]
    public async Task CommittedRound_ShouldAdvanceTurnAndSequenceExactlyOnce()
    {
        // ADR-026 D4 / MATCH3_RULES.md §8.1–§8.2: one atomic round — both actors'
        // board actions, their effects, and step 19a — produces exactly one new Turn
        // and one new Sequence, in one write-back.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        const string battleId = "round-turn-sequence-once";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.HoaLong);

        var state = created;

        for (var round = 1; round <= 3; round++)
        {
            var pinned = RoundTestSupport.PlayerMatch(GemType.Atk, state.RngState, state.LastCommittedSwapPair);
            await RoundTestSupport.PinAsync(repository, state, pinned.Board);

            var writesBefore = repository.WriteCount;
            var result = (await service.ExecuteSwapAsync(battleId, pinned.Swap))!.Value;

            Assert.True(result.IsAccepted);
            Assert.Equal(state.Turn + 1, result.State.Turn);
            Assert.Equal(state.Sequence + 1, result.State.Sequence);

            // Both board actions happened inside the round...
            Assert.NotEmpty(RoundTestSupport.PlayerBoardEvents(result));
            Assert.NotEmpty(RoundTestSupport.BossBoardEvents(result));

            // ...and the whole round was one store write (REDIS_STATE.md §4 item 5).
            Assert.Equal(writesBefore + 1, repository.WriteCount);

            state = result.State;
        }

        Assert.Equal(created.Turn + 3, state.Turn);
        Assert.Equal(created.Sequence + 3, state.Sequence);
    }

    // =======================================================================
    // 13. Status-effect lifecycle boundary (GAME_STATE.md §5.1.1, §17 step 19a)
    // =======================================================================

    [Fact]
    public async Task Step19a_ShouldTickTurnBasedInstancesOnceAndLeaveTriggerBasedOnesAlone()
    {
        // GAME_STATE.md §5.1.1 items 2, 4 and 7: exactly one Turn of duration is
        // consumed per resolved round for every Turn-countdown instance, an instance
        // reaching 0 is removed in that same pass, and a trigger-based instance is
        // not on the countdown at all.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var boss = BossDefinitions.HoaLong with
        {
            // No Skill fires: the only effects present are the ones this scenario pins.
            SkillDefinition = BossDefinitions.HoaLong.SkillDefinition with { ChargeRequirement = 1000 },
            // No Passive triggers either (its Threshold is far beyond one round's Matches).
            PassiveDefinition = BossDefinitions.HoaLong.PassiveDefinition with { Threshold = 1000 },
        };

        const string battleId = "round-step-19a";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var petEffects = new[]
        {
            StatusEffect.TurnBased("Root", StatusEffectType.BuffDebuff, StatusEffectSource.Boss, 30, 2, "ATK"),
            StatusEffect.TurnBased("round-expiring", StatusEffectType.BuffDebuff, StatusEffectSource.Boss, 10, 1, "ATK"),

            // A trigger-based instance whose identity no stage of this round applies
            // or refreshes, so the assertion below is about the countdown boundary
            // and nothing else.
            StatusEffect.TriggerBased(
                "round-trigger",
                StatusEffectType.BuffDebuff,
                StatusEffectSource.Boss,
                10,
                StatusEffect.NextQualifyingAttackCondition,
                "ATK"),
        };

        var bossEffects = new[]
        {
            StatusEffect.TurnBased("boss-hoa-long-rage", StatusEffectType.BuffDebuff, StatusEffectSource.Boss, 20, 1, "ATK"),
        };

        var pinned = RoundTestSupport.PlayerMatch(GemType.Def, created.RngState, created.LastCommittedSwapPair);

        await RoundTestSupport.PinAsync(
            repository,
            created,
            pinned.Board,
            bossState: created.BossState with { ActiveStatusEffects = bossEffects },
            petState: created.PetState with { ActiveStatusEffects = petEffects });

        var result = (await service.ExecuteSwapAsync(battleId, pinned.Swap))!.Value;
        Assert.True(result.IsAccepted);

        // 2 -> 1, still active.
        var root = Assert.Single(result.State.PetState.ActiveStatusEffects, e => e.Id == "Root");
        Assert.Equal(1, root.RemainingTurns);

        // 1 -> 0: removed in the same pass, never observable as 0 (§5.1.1 items 4-5).
        Assert.DoesNotContain(result.State.PetState.ActiveStatusEffects, e => e.Id == "round-expiring");

        // A trigger-based instance is not on the countdown at all (§5.1.1 item 7): it
        // is carried across the round unchanged rather than decremented.
        var trigger = Assert.Single(result.State.PetState.ActiveStatusEffects, e => e.Id == "round-trigger");
        Assert.Equal(10, trigger.Magnitude);
        Assert.Null(trigger.RemainingTurns);
        Assert.Equal(StatusEffect.NextQualifyingAttackCondition, trigger.ExpiryCondition);

        // The Boss's own one-Turn instance expired at the same boundary.
        Assert.DoesNotContain(
            result.State.BossState.ActiveStatusEffects,
            e => e.Id == "boss-hoa-long-rage");
    }

    // =======================================================================
    // 14. Snapshot recovery reads the committed round (ADR-008)
    // =======================================================================

    [Fact]
    public async Task SnapshotRecovery_ShouldReturnTheCommittedRoundState()
    {
        // ADR-026 §7 / ADR-008: Redis CAS does not guarantee event delivery, so a
        // client that lost the broadcast recovers by reading the authoritative
        // snapshot. The record must therefore hold exactly the state the round
        // committed — including the new trigger-based Boss modifier, which must
        // serialize and deserialize losslessly (GAME_STATE.md §2.3.2 item 5).
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var boss = BossDefinitions.KimLoiVuong;

        const string battleId = "round-snapshot-recovery";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Def, created.RngState, created.LastCommittedSwapPair);

        await RoundTestSupport.PinAsync(
            repository,
            created,
            pinned.Board,
            bossState: created.BossState with
            {
                SkillCharge = boss.SkillChargeRequirement,
                SkillCooldown = 0,
            });

        var result = (await service.ExecuteSwapAsync(battleId, pinned.Swap))!.Value;
        Assert.True(result.IsAccepted);
        Assert.Contains(result.Events, e => e.Type == BattleEventType.BossSkillCast);

        var snapshot = (await service.GetBattleAsync(battleId))!;

        Assert.Equal(result.State.Sequence, snapshot.Sequence);
        Assert.Equal(result.State.Turn, snapshot.Turn);
        Assert.Equal(result.State.BossState.HP, snapshot.BossState.HP);
        Assert.Equal(result.State.PetState.HP, snapshot.PetState.HP);
        Assert.True(result.State.BoardState.CellsEqual(snapshot.BoardState));

        // The recovered snapshot is the same value a reconnect would project
        // (ADR-008, GAME_STATE.md §2.3.2 item 5).
        var roundTripped = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(snapshot));

        Assert.Equal(snapshot.Sequence, roundTripped.Sequence);
        Assert.Equal(snapshot.Turn, roundTripped.Turn);
        Assert.True(StatusEffectLifecycle.EffectsEqual(
            snapshot.BossState.ActiveStatusEffects,
            roundTripped.BossState.ActiveStatusEffects));
        Assert.True(snapshot.BoardState.CellsEqual(roundTripped.BoardState));

        // The recovered record also carries the round's new state shape: a
        // trigger-based ATK modifier is one the state model had no use for before
        // ADR-026 D7, so it is the case a recovery path must not lose. It is pinned
        // here rather than observed, because the same round's qualifying attack may
        // legitimately have consumed it.
        var thunderStrike = StatusEffect.TriggerBased(
            "thunder-strike",
            StatusEffectType.BuffDebuff,
            StatusEffectSource.Boss,
            50,
            StatusEffect.NextQualifyingAttackCondition,
            "ATK");

        await RoundTestSupport.PinAsync(
            repository,
            snapshot,
            snapshot.BoardState,
            bossState: snapshot.BossState with
            {
                ActiveStatusEffects = StatusEffectLifecycle.ApplyTriggerBased(
                    snapshot.BossState.ActiveStatusEffects,
                    thunderStrike),
            });

        var recovered = (await service.GetBattleAsync(battleId))!;

        var recoveredBuff = Assert.Single(
            recovered.BossState.ActiveStatusEffects,
            e => e.Id == "thunder-strike");

        Assert.Equal("ATK", recoveredBuff.TargetStat);
        Assert.Equal(StatusEffect.NextQualifyingAttackCondition, recoveredBuff.ExpiryCondition);
        Assert.Null(recoveredBuff.RemainingTurns);

        var recoveredRoundTrip = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(recovered));

        Assert.True(StatusEffectLifecycle.EffectsEqual(
            recovered.BossState.ActiveStatusEffects,
            recoveredRoundTrip.BossState.ActiveStatusEffects));
    }

    // =======================================================================
    // Helpers
    // =======================================================================

    /// <summary>
    /// Commits one atomic round in which the named Boss's Skill fires — the Skill's
    /// charge is pinned to the definition's own ChargeRequirement, a state the game
    /// genuinely produces by accumulating Boss POWER matches — and returns the
    /// committed result.
    /// </summary>
    /// <param name="battleId">The battle session to create.</param>
    /// <param name="definition">The Boss whose Skill is under test.</param>
    /// <param name="petPower">
    /// The Pet's Power before the round, for the instance-less Drain Power scenario.
    /// </param>
    private static async Task<(SwapExecutionResult Result, BossDefinition Definition)> FireTheSkillAsync(
        string battleId,
        BossDefinition definition,
        int petPower = 0)
    {
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var created = await service.CreateBattleAsync(battleId, Owner, Pet, definition);

        // A board whose Swap clears a non-ATK Gem only, so the Skill is the round's
        // only effect under scrutiny and no ATK-gated instance can be mistaken for
        // the Skill's damage.
        var pinned = RoundTestSupport.PlayerMatch(GemType.Def, created.RngState, created.LastCommittedSwapPair);

        await RoundTestSupport.PinAsync(
            repository,
            created,
            pinned.Board,
            bossState: created.BossState with
            {
                SkillCharge = definition.SkillChargeRequirement,
                SkillCooldown = 0,
            },
            petState: created.PetState with { Power = petPower });

        var result = (await service.ExecuteSwapAsync(battleId, pinned.Swap))!.Value;

        Assert.True(result.IsAccepted);
        Assert.Contains(result.Events, e => e.Type == BattleEventType.BossSkillCast);

        return (result, definition);
    }

    /// <summary>
    /// The index of the <paramref name="occurrence"/>-th event of
    /// <paramref name="type"/> in the round's ordered list — the position the
    /// documented ordering is asserted against (<c>GAME_EVENTS.md</c> §1).
    /// </summary>
    private static int IndexOfNth(SwapExecutionResult result, BattleEventType type, int occurrence)
    {
        var seen = 0;

        for (var index = 0; index < result.Events.Count; index++)
        {
            if (result.Events[index].Type != type)
            {
                continue;
            }

            if (seen == occurrence)
            {
                return index;
            }

            seen++;
        }

        throw new InvalidOperationException(
            $"GAME_EVENTS.md §1: the round produced fewer than {occurrence + 1} {type} events.");
    }

    /// <summary>
    /// Asserts that the fired Boss Skill produced no attack instance of its own: the
    /// round carries the ATK-gated Boss instance when the Boss's board action cleared
    /// an ATK Gem, and none otherwise (<c>ADR-026</c> D3, D1).
    /// </summary>
    private static void AssertNoDirectSkillDamage(SwapExecutionResult result)
    {
        var bossDamageInstances = result.Events.Count(
            e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Boss);

        Assert.Equal(RoundTestSupport.BossClearedAtkGem(result) ? 1 : 0, bossDamageInstances);
    }
}

/// <summary>
/// The active-state store with a write that can be made to fail — the
/// <c>REDIS_STATE.md</c> §2 item 2 failure boundary, which an implementation
/// raises rather than absorbs.
///
/// <b>It is a TEST DOUBLE, not a fallback store.</b> The production composition
/// registers the Redis implementation and nothing else; this type exists so a test
/// can prove that an exception before the commit leaves the authoritative record
/// exactly as the round read it (<c>ADR-026</c> D4).
/// </summary>
internal sealed class ThrowingBattleStateRepository : IBattleStateRepository
{
    private readonly IBattleStateRepository _inner;

    public ThrowingBattleStateRepository(IBattleStateRepository inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <summary>When set, every write raises instead of being applied.</summary>
    public bool FailWrites { get; set; }

    /// <inheritdoc />
    public Task CreateAsync(BattleState state, CancellationToken cancellationToken = default) =>
        _inner.CreateAsync(state, cancellationToken);

    /// <inheritdoc />
    public Task<BattleState?> GetAsync(string battleId, CancellationToken cancellationToken = default) =>
        _inner.GetAsync(battleId, cancellationToken);

    /// <inheritdoc />
    public Task<bool> TryUpdateAsync(
        BattleState state,
        int expectedSequence,
        CancellationToken cancellationToken = default) =>
        FailWrites
            ? throw new InvalidOperationException(
                "REDIS_STATE.md §2 item 2: this double is configured to fail the write.")
            : _inner.TryUpdateAsync(state, expectedSequence, cancellationToken);

    /// <inheritdoc />
    public Task DeleteAsync(string battleId, CancellationToken cancellationToken = default) =>
        _inner.DeleteAsync(battleId, cancellationToken);
}
