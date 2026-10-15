using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using Xunit;

namespace GameServer.Application.Tests;

/// <summary>
/// Boss Response tests — the Boss response phase of the atomic round
/// (<c>ADR-026</c>, <c>GAME_RULES.md</c> §17 steps 18a–18d), whose rules are
/// <c>BOSS_RULES.md</c> §3 (Passive), §4 (Skill), §5 (State/Enrage), §6 (MVP
/// reference), <c>COMBAT_RULES.md</c> §2–§4, and <c>GAME_STATE.md</c>
/// §2.4.1–§2.4.5.
///
/// <code>
/// Rule (BOSS_RULES.md §6.2–§6.4, ADR-026)
///  ↓
/// Scenario (Given the documented Boss and a committed Swap, When the round
///           resolves, Then the documented state and events)
///  ↓
/// Test
/// </code>
///
/// <b>Every expected value traces to a section, never to the implementation.</b>
/// The Boss configuration is read from <see cref="BossDefinitions"/> (which
/// transcribes <c>BOSS_RULES.md</c> §6) and the Match totals from the
/// resolution's own report, so no assertion encodes "whatever the code does".
/// Where the shared board decides an outcome — whether the Boss's own swap
/// cleared an ATK Gem (<c>ADR-026</c> D1) or how many POWER match groups it
/// produced (D2) — the expectation is read from the round's own committed events
/// through <see cref="RoundTestSupport"/> rather than assumed.
///
/// <b>The order this suite asserts.</b> <c>ADR-026</c> §2 and <c>BOSS_RULES.md</c>
/// §5 item 4 fix one atomic round: Player action phase → Boss response phase →
/// step 19a. Inside the Boss response, step 18a is the Boss's own swap on the
/// shared board, then the Passive (18b), then the Skill (18c), then the
/// ATK-gated Boss→Pet attack (18d). Player→Boss damage → Enrage → terminal Boss
/// HP check (which skips 18a–18d) → 18a → 18b → 18c → 18d → terminal Pet HP
/// check → step 19a. <b>The legacy fallback basic attack is retired</b>
/// (<c>ADR-026</c> §5, <c>COMBAT_RULES.md</c> §3.4 item 1): the Boss deals attack
/// damage if and only if its own swap cleared at least one ATK Gem, and all five
/// MVP Skills deal zero direct damage (<c>ADR-026</c> D3, <c>BOSS_RULES.md</c>
/// §6.3).
/// </summary>
public class BossResponseTests
{
    /// <summary>
    /// The Pet these battles carry — the owned instance identity
    /// (<c>GAME_STATE.md</c> §2.3), Xích Lang's MVP Element and Passive
    /// (<c>ELEMENT_RULES.md</c> §6, <c>PASSIVE_RULES.md</c> §8).
    /// </summary>
    private static readonly BattleStateService.PetConfiguration Pet =
        new(new PetId("pet_instance_1"), Element.Hoa, new PassiveId("xich-lang"), PassiveThreshold: 5);

    /// <summary>
    /// The owning Player of these battles (<c>GAME_STATE.md</c> §2.8) — the
    /// identity the creation path records. These suites assert Boss response,
    /// damage, and the write-back, not identity, so one fixture value is supplied
    /// in one place.
    /// </summary>
    private static readonly PlayerId Owner = new("player_boss_response_owner");

    // =======================================================================
    // Boss Passive — GAME_RULES.md §17 step 18b, BOSS_RULES.md §3, §6.2
    // =======================================================================

    [Fact]
    public async Task BossPassive_ShouldChargeOncePerPlayerMatch()
    {
        // BOSS_RULES.md §3.3 item 1 / §6.2: Hỏa Long's Passive charges "Every 5
        // Player Matches" — the same per-Match rate the Pet Passive uses
        // (PASSIVE_RULES.md §2 item 1), over the same Match total. GAME_STATE.md
        // §2.4.2 gives it its own counter. ADR-026 D2 changed the SKILL's counter's
        // source, not this one: the Boss Passive's charge is still the Player's
        // Match total.
        var service = NewService();
        var created = await service.CreateBattleAsync("boss-passive-charge", Owner, Pet, BossDefinitions.HoaLong);

        var pair = FindMatchProducingPair(created);
        var result = await service.ExecuteSwapAsync("boss-passive-charge", pair);

        Assert.True(result!.Value.IsAccepted);

        var bossDefinition = BossDefinitions.HoaLong;
        var matches = result.Value.Resolution.TotalMatches;

        var bossCharges = result.Value.Events
            .Where(e => e.Type == BattleEventType.PassiveCharged
                && e.PassiveCharged.Source == PassiveEventSource.Boss)
            .ToArray();

        Assert.Equal(matches, bossCharges.Length);
        Assert.Equal(
            Enumerable.Range(1, matches),
            bossCharges.Select(c => c.PassiveCharged.Progress));

        Assert.All(
            bossCharges,
            c => Assert.Equal(bossDefinition.PassiveThreshold, c.PassiveCharged.Threshold));

        // The settled progress is the tracker's own result for that batch
        // (PASSIVE_RULES.md §2 item 3, §4 item 1).
        var expectedProgress = matches >= bossDefinition.PassiveThreshold ? 0 : matches;

        Assert.Equal(expectedProgress, result.Value.State.BossState.PassiveProgress.Current);
    }

    [Fact]
    public async Task BossPassive_ShouldReportTheBossSourceAndCanonicalIdentity()
    {
        // SIGNALR_PROTOCOL.md §3.2.16 items 1–2 / BOSS_RULES.md §7: the shared events
        // carry source="boss", and sourceId is the canonical technical Identity
        // BossState.BossId — §6.4 fixes "boss-hoa-long", never a display name.
        var service = NewService();
        var created = await service.CreateBattleAsync("boss-passive-identity", Owner, Pet, BossDefinitions.HoaLong);

        var pair = FindMatchProducingPair(created);
        var result = await service.ExecuteSwapAsync("boss-passive-identity", pair);

        Assert.True(result!.Value.IsAccepted);

        var bossPassiveEvents = result.Value.Events
            .Where(e => e.Type == BattleEventType.PassiveCharged
                && e.PassiveCharged.Source == PassiveEventSource.Boss
                || e.Type == BattleEventType.PassiveTriggered
                && e.PassiveTriggered.Source == PassiveEventSource.Boss)
            .ToArray();

        Assert.NotEmpty(bossPassiveEvents);

        foreach (var e in bossPassiveEvents)
        {
            var (passiveId, source, sourceId) = e.Type == BattleEventType.PassiveCharged
                ? (e.PassiveCharged.PassiveId.Value, e.PassiveCharged.Source, e.PassiveCharged.SourceId)
                : (e.PassiveTriggered.PassiveId.Value, e.PassiveTriggered.Source, e.PassiveTriggered.SourceId);

            Assert.Equal(PassiveEventSource.Boss, source);
            Assert.Equal("boss-hoa-long", sourceId);
            Assert.Equal("boss-hoa-long-rage", passiveId);
        }
    }

    [Fact]
    public async Task BossPassive_ShouldNotEmitMatchDrivenEventsForThuyMa()
    {
        // BOSS_RULES.md §6.2 / §6.2.2 is explicit: Thủy Ma's trigger is "Battle
        // Start" — an alternate trigger (PASSIVE_RULES.md §3), not a Match count —
        // so it "is never charged via PassiveTracker.Charge on Player Matches, and
        // emits no PassiveCharged/PassiveTriggered from match progress". Its stored
        // PassiveThreshold is therefore the non-charged marker (null, read back as
        // 0), not a threshold.
        var service = NewService();
        var created = await service.CreateBattleAsync("boss-passive-thuyma", Owner, Pet, BossDefinitions.ThuyMa);

        var pair = FindMatchProducingPair(created);
        var result = await service.ExecuteSwapAsync("boss-passive-thuyma", pair);

        Assert.True(result!.Value.IsAccepted);
        Assert.True(result.Value.Resolution.TotalMatches >= 1);

        // No Boss Passive event of any kind, from match progress.
        Assert.DoesNotContain(
            result.Value.Events,
            e => e.Type == BattleEventType.PassiveCharged
                && e.PassiveCharged.Source == PassiveEventSource.Boss);

        Assert.DoesNotContain(
            result.Value.Events,
            e => e.Type == BattleEventType.PassiveTriggered
                && e.PassiveTriggered.Source == PassiveEventSource.Boss);

        // And its progress is untouched — still the non-charged marker with no
        // charging, which is what "skip the charge" means in state.
        Assert.Equal(0, result.Value.State.BossState.PassiveProgress.Threshold);
        Assert.Equal(0, result.Value.State.BossState.PassiveProgress.Current);

        // The Pet's own Passive is unaffected: the shared event is still emitted for
        // the Pet on the same Swap (GAME_EVENTS.md §2 item 1).
        Assert.Contains(
            result.Value.Events,
            e => e.Type == BattleEventType.PassiveCharged
                && e.PassiveCharged.Source == PassiveEventSource.Pet);
    }

    [Fact]
    public async Task BossPassive_ShouldTriggerAtTheThresholdAndReset()
    {
        // PASSIVE_RULES.md §2 item 3 / §4 item 1: the Threshold is evaluated once
        // after the batch and the default reset settles progress at 0. This drives a
        // Threshold-1 Boss by using a Boss definition whose PassiveThreshold is 1 —
        // built from the documented §6.3/§6.4 shape rather than invented — so any
        // committed Swap (which always produces >= 1 Match, MATCH3_RULES.md §2.1.2
        // item 4) crosses it.
        var service = NewService();
        var boss = BossDefinitions.HoaLong with
        {
            PassiveDefinition = BossDefinitions.HoaLong.PassiveDefinition with { Threshold = 1 },
        };

        var created = await service.CreateBattleAsync("boss-passive-trigger", Owner, Pet, boss);

        var pair = FindMatchProducingPair(created);
        var result = await service.ExecuteSwapAsync("boss-passive-trigger", pair);

        Assert.True(result!.Value.IsAccepted);

        var triggers = result.Value.Events
            .Where(e => e.Type == BattleEventType.PassiveTriggered
                && e.PassiveTriggered.Source == PassiveEventSource.Boss)
            .ToArray();

        // At most once per Cascade, whatever the Match count (PASSIVE_RULES.md §2
        // item 3, §5).
        Assert.Single(triggers);

        // GAME_EVENTS.md §2 item 2: pre-reset progress — the batch total.
        Assert.Equal(result.Value.Resolution.TotalMatches, triggers[0].PassiveTriggered.Progress);
        Assert.Equal(1, triggers[0].PassiveTriggered.Threshold);

        // §4 item 1: the default reset settles at 0.
        Assert.Equal(0, result.Value.State.BossState.PassiveProgress.Current);
    }

    [Fact]
    public async Task BossPassive_MocYeuRegen_ShouldApplyRegenAtStep18a()
    {
        // BOSS_RULES.md §6.2.3, TASK-153: Step 18a applies Mộc Yêu's regeneration —
        // exactly 5% of its MaxHP, truncated toward zero — directly to
        // BossState.HP, clamped to MaxHP.
        //
        // A round has two other Boss-HP writes, and both are read from its own
        // committed output rather than assumed: the Boss's own board action heals it
        // from the HP Gems its swap cleared (ADR-026 §4, COMBAT_RULES.md §2), and the
        // Player's instance damages it only when the Player's Swap cleared an ATK Gem
        // (ADR-026 D1). This scenario pins the Player's Swap to clear no ATK Gem and
        // asserts the regeneration's own formula in a round in which the Boss cleared
        // no HP Gem — so the regeneration is the round's only Boss-HP change.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var boss = BossDefinitions.MocYeu with
        {
            PassiveDefinition = BossDefinitions.MocYeu.PassiveDefinition with { Threshold = 1 },
        };

        // §6.2.3: heals exactly 5% of MaxHP → truncate(5000 × 5 / 100) = 250.
        var regeneration = boss.MaxHP * 5 / 100;

        const string battleId = "boss-passive-moc-yeu";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var state = created;
        var observed = false;

        for (var round = 0; round < 12 && !observed; round++)
        {
            var pinned = RoundTestSupport.PlayerMatch(
                round % 2 == 0 ? GemType.Def : GemType.Power,
                state.RngState,
                state.LastCommittedSwapPair);

            Assert.Equal(0, pinned.Resources.ClearedGemCounts.Atk);

            // The Boss is placed at or below half MaxHP, so the documented clamp is
            // not what the assertion observes: HP + regeneration stays under MaxHP.
            var hpBefore = Math.Min(state.BossState.HP, boss.MaxHP / 2);

            await RoundTestSupport.PinAsync(
                repository,
                state,
                pinned.Board,
                bossState: state.BossState with { HP = hpBefore });

            var result = (await service.ExecuteSwapAsync(battleId, pinned.Swap))!.Value;

            Assert.True(result.IsAccepted);

            // The Threshold is 1 and a committed Swap always produces at least one
            // Match (MATCH3_RULES.md §2.1.2 item 4), so the Passive triggers
            // (PASSIVE_RULES.md §2 item 3).
            Assert.Contains(
                result.Events,
                e => e.Type == BattleEventType.PassiveTriggered
                    && e.PassiveTriggered.Source == PassiveEventSource.Boss);

            if (!BossClearedHpGem(result))
            {
                Assert.True(hpBefore + regeneration <= boss.MaxHP);

                // §6.2.3: Final HP = min(CurrentHP + RegenAmount, MaxHP), and no other
                // Boss-HP write occurred this round.
                Assert.Equal(hpBefore + regeneration, result.State.BossState.HP);
                observed = true;
            }

            state = result.State;
        }

        Assert.True(
            observed,
            "at least one pinned round must isolate Mộc Yêu's regeneration from the Boss's own HP-Gem healing");
    }

    // =======================================================================
    // Boss Skill — GAME_RULES.md §17 step 18c, BOSS_RULES.md §4, §6.3
    // =======================================================================

    [Fact]
    public async Task BossSkill_ShouldFireWhenChargeMeetsTheRequirementAndCooldownIsZero()
    {
        // GAME_STATE.md §2.4.3 / BOSS_RULES.md §6.3: "The Skill fires when BOTH
        // conditions are met: SkillCharge >= SkillChargeRequirement AND SkillCooldown
        // = 0". The charge is pinned to the definition's own requirement — ADR-026 D2
        // makes SkillCharge the Boss's OWN POWER matches' counter, which no single
        // board can be relied upon to fill — and the cooldown begins at 0.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var boss = BossDefinitions.HoaLong;

        const string battleId = "boss-skill-fires";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        Assert.Equal(0, created.BossState.SkillCharge);
        Assert.Equal(0, created.BossState.SkillCooldown);

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

        var casts = result.Events
            .Where(e => e.Type == BattleEventType.BossSkillCast)
            .ToArray();

        Assert.Single(casts);
        Assert.Equal("flame-burst", casts[0].BossSkillCast.SkillId);
        Assert.Equal("boss-hoa-long", casts[0].BossSkillCast.SourceId);
    }

    [Fact]
    public async Task BossSkill_ShouldResetChargeAndSetTheCooldown()
    {
        // BOSS_RULES.md §6.3 / GAME_STATE.md §2.4.3: "After the Skill fires:
        // SkillCharge resets to 0, SkillCooldown resets to the Boss's cooldown value."
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var boss = BossDefinitions.HoaLong;

        const string battleId = "boss-skill-reset";
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

        var after = result.State.BossState;

        Assert.Equal(0, after.SkillCharge);
        Assert.Equal(boss.SkillCooldownTurns, after.SkillCooldown);
    }

    [Fact]
    public async Task BossSkill_ShouldDealNoDirectDamage_AndReachDamageOnlyThroughEffectiveBossAtk()
    {
        // ADR-026 D3 / BOSS_RULES.md §6.3: all five MVP Skills declare
        // BaseDamage = 0, so a fired Skill produces no damage instance of its own.
        // D5 makes Hỏa Long's Flame Burst a temporary Boss ATK buff (+50% for 2 Turns,
        // §6.3.1 item 1), and COMBAT_RULES.md §3.4 composes that buff into the Boss's
        // next ATK-gated board attack through EffectiveBossATK — never as a
        // Skill-owned instance.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        // §6.3: the authored Skill Base Damage is 0 for every MVP Boss.
        Assert.Equal(0, BossDefinitions.HoaLong.SkillBaseDamage);

        var boss = BossDefinitions.HoaLong with
        {
            // The match-charged Passive is held out of the window, so the Flame Burst
            // buff is the only Boss ATK modifier in play.
            PassiveDefinition = BossDefinitions.HoaLong.PassiveDefinition with { Threshold = 100 },
        };

        const string battleId = "boss-skill-damage";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var petState = created.PetState with { HP = 1_000_000, MaxHP = 1_000_000 };
        var state = created with { PetState = petState };

        await RoundTestSupport.PinAsync(repository, created, created.BoardState, petState: petState);

        var observed = false;

        for (var round = 0; round < 8 && !observed; round++)
        {
            var pinned = RoundTestSupport.PlayerMatch(
                round % 2 == 0 ? GemType.Def : GemType.Hp,
                state.RngState,
                state.LastCommittedSwapPair);

            await RoundTestSupport.PinAsync(
                repository,
                state,
                pinned.Board,
                bossState: state.BossState with
                {
                    SkillCharge = boss.SkillChargeRequirement,
                    SkillCooldown = 0,
                });

            var result = (await service.ExecuteSwapAsync(battleId, pinned.Swap))!.Value;

            Assert.True(result.IsAccepted);

            // The pinned charge satisfies both §2.4.3 conditions on every round.
            Assert.Contains(result.Events, e => e.Type == BattleEventType.BossSkillCast);

            var bossAfter = result.State.BossState;

            // D5 / §6.3.1 item 1: the fired Skill applied the temporary Boss ATK buff.
            var buff = Assert.Single(bossAfter.ActiveStatusEffects, e => e.Id == "flame-burst");

            Assert.Equal("ATK", buff.TargetStat);
            Assert.Equal(50, buff.Magnitude);

            // COMBAT_RULES.md §5.5.1: EffectiveBossATK =
            // truncate(BossState.ATK × (100 + Magnitude) / 100) = truncate(100 × 150 /
            // 100) = 150. It is derived from the committed state's own instances, so a
            // buff that had expired would be read as the base stat.
            var effectiveBossAtk = StatusEffectLifecycle.EffectiveBossAttack(
                bossAfter.ATK,
                bossAfter.ActiveStatusEffects);

            Assert.Equal(150, effectiveBossAtk);

            // The Skill's own instance: none. The Player's pinned Swap cleared no ATK
            // Gem, so §17 steps 15–17 produced no Player instance; every damage
            // instance in this round is therefore the Boss's ATK-gated board instance —
            // or there is none, when the Boss's own swap cleared no ATK Gem (D1).
            var calculations = result.Events
                .Where(e => e.Type == BattleEventType.DamageCalculated)
                .Select(e => e.DamageCalculated)
                .ToArray();

            var bossDamageInstances = result.Events.Count(
                e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Boss);

            Assert.Equal(bossDamageInstances, calculations.Length);

            if (!RoundTestSupport.BossClearedAtkGem(result))
            {
                Assert.Empty(calculations);
                state = result.State;
                continue;
            }

            var bossDamage = Assert.Single(calculations);

            // COMBAT_RULES.md §3.4 step 1: Step 1 = EffectiveBossATK + the ATK-Gem
            // BaseDamagePool the Boss's own swap generated. The Skill's authored Base
            // Damage is 0 and adds nothing; the buff reaches the instance only through
            // EffectiveBossATK.
            Assert.Equal(effectiveBossAtk + BossAtkGemPool(result), bossDamage.Base);

            // §3.4 step 2: a Boss attack is not part of a Combo chain.
            Assert.Equal(1.00, bossDamage.ComboModifier);

            observed = true;
            state = result.State;
        }

        Assert.True(observed, "the Boss's board action must produce a qualifying ATK match within 8 rounds");
    }

    [Fact]
    public async Task BossSkill_ShouldBeBlockedWhileTheCooldownIsActive()
    {
        // BOSS_RULES.md §6.3: "The Skill is blocked while CD > 0." The previous round
        // cast the Skill, so this one begins with the cooldown running: the Skill
        // condition fails on its second clause and no BossSkillCast is emitted.
        //
        // ADR-026 §5 retires the fallback attack that used to replace a blocked Skill:
        // the Boss's only damage is its own ATK-gated board instance (§3.4 item 1), so
        // a blocked Skill means no Skill, not a substitute attack.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var (battleId, _) = await BattleWithACoolingBoss(repository, service, "boss-skill-cooldown-blocks");

        var afterCast = (await service.GetBattleAsync(battleId))!;

        // MATCH3_RULES.md §2.1.4: the pair just committed is recorded as already
        // applied, so the next Swap must be a different one.
        var pair = FindMatchProducingPair(afterCast);

        var result = await service.ExecuteSwapAsync(battleId, pair);

        Assert.True(result!.Value.IsAccepted);

        // The cooldown decremented by one for this round and was still > 0 when the
        // Skill was evaluated, so the Skill did not fire.
        Assert.DoesNotContain(result.Value.Events, e => e.Type == BattleEventType.BossSkillCast);

        // And no attack replaced it: the round holds one Boss damage instance exactly
        // when the Boss's own swap cleared an ATK Gem (ADR-026 D1).
        var bossDamageInstances = result.Value.Events.Count(
            e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Boss);

        Assert.Equal(RoundTestSupport.BossClearedAtkGem(result.Value) ? 1 : 0, bossDamageInstances);
    }

    [Fact]
    public async Task BossSkill_ShouldBeBlockedWhileTheChargeIsShort()
    {
        // The first clause of §2.4.3's condition. A ChargeRequirement no single round
        // can reach leaves the Skill unfired — no cast, no cooldown set — and the
        // charge still accumulates for a later round.
        //
        // ADR-026 D2 / GAME_STATE.md §2.4.3: the counter increments from POWER gems
        // matched by the BOSS during the Boss's turn; Player matches never charge the
        // Boss. The Player's Swap here clears POWER Gems and charges the Pet's Power,
        // yet contributes nothing to SkillCharge.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var boss = BossDefinitions.HoaLong with
        {
            SkillDefinition = BossDefinitions.HoaLong.SkillDefinition with { ChargeRequirement = 1000 },
        };

        const string battleId = "boss-skill-charge-short";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Power, created.RngState, created.LastCommittedSwapPair);

        Assert.True(pinned.Resources.ClearedGemCounts.Power > 0);

        var previousCharge = 900;

        await RoundTestSupport.PinAsync(
            repository,
            created,
            pinned.Board,
            bossState: created.BossState with { SkillCharge = previousCharge, SkillCooldown = 0 });

        var result = (await service.ExecuteSwapAsync(battleId, pinned.Swap))!.Value;

        Assert.True(result.IsAccepted);
        Assert.DoesNotContain(result.Events, e => e.Type == BattleEventType.BossSkillCast);

        // GAME_STATE.md §2.4.3 / ADR-026 D2: the settled charge is the documented sum —
        // the previous charge plus the Boss's own POWER match groups, clamped at the
        // requirement. The Player's matches appear in no term.
        Assert.Equal(
            Math.Min(previousCharge + RoundTestSupport.BossPowerMatchGroups(result), boss.SkillChargeRequirement),
            result.State.BossState.SkillCharge);

        // The Player's POWER Gems charged the Pet's Power instead (GAME_RULES.md §17
        // step 13).
        Assert.True(result.State.PetState.Power > created.PetState.Power);

        // No cooldown was set: the Skill never fired (BOSS_RULES.md §6.3).
        Assert.Equal(0, result.State.BossState.SkillCooldown);
    }

    [Fact]
    public async Task BossSkillCharge_ShouldIncrementOnlyPerBossPowerMatchGroup()
    {
        // BOSS_RULES.md §6.3 / GAME_STATE.md §2.4.3 under ADR-026 D2: SkillCharge
        // "increments exclusively from POWER gems matched by the Boss during the
        // Boss's turn (player matches never charge the Boss)". The unit is one POWER
        // match group per increment, clamped at the ChargeRequirement.
        //
        // The Player's Swap is pinned to clear POWER Gems — so it produces Matches and
        // charges the Pet's Power — while the Boss's counter is asserted to move by the
        // Boss's own match groups alone. The unreachable requirement keeps the
        // accumulated charge observable rather than reset by a cast.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var boss = BossDefinitions.HoaLong with
        {
            SkillDefinition = BossDefinitions.HoaLong.SkillDefinition with { ChargeRequirement = 1000 },
        };

        const string battleId = "boss-skill-charge-rate";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        // Both sides are given room to survive the observation window: this scenario
        // asserts the counter, not who wins.
        var petState = created.PetState with { HP = 1_000_000, MaxHP = 1_000_000 };
        var state = created with { PetState = petState };

        await RoundTestSupport.PinAsync(repository, created, created.BoardState, petState: petState);

        var sawBossPowerGroups = false;

        for (var round = 0; round < 16; round++)
        {
            var pinned = RoundTestSupport.PlayerMatch(
                GemType.Power,
                state.RngState,
                state.LastCommittedSwapPair);

            Assert.True(pinned.Resources.ClearedGemCounts.Power > 0);

            await RoundTestSupport.PinAsync(repository, state, pinned.Board);

            var previousCharge = state.BossState.SkillCharge;
            var result = (await service.ExecuteSwapAsync(battleId, pinned.Swap))!.Value;

            Assert.True(result.IsAccepted);
            Assert.True(result.Resolution.TotalMatches >= 1);

            var groups = RoundTestSupport.BossPowerMatchGroups(result);

            Assert.Equal(
                Math.Min(previousCharge + groups, boss.SkillChargeRequirement),
                result.State.BossState.SkillCharge);

            sawBossPowerGroups |= groups > 0;
            state = result.State;

            if (state.BossState.HP <= 0 || state.PetState.HP <= 0)
            {
                break;
            }
        }

        Assert.True(
            sawBossPowerGroups,
            "the Boss's board action must match POWER Gems at least once across the rounds");
    }

    [Fact]
    public async Task BossSkill_ShouldNotResetThePassiveProgress()
    {
        // TASK-022 §3.7 / GAME_STATE.md §2.4.3: SkillCharge and PassiveProgress are
        // "independent counter[s]". Firing the Skill resets the charge and sets the
        // cooldown; the Passive's own progress is whatever its own step produced — the
        // Player Matches it charged over (BOSS_RULES.md §6.2: Hỏa Long, "every 5 Player
        // Matches").
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var boss = BossDefinitions.HoaLong with
        {
            PassiveDefinition = BossDefinitions.HoaLong.PassiveDefinition with { Threshold = 100 },
        };

        const string battleId = "boss-skill-independent";
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

        var matches = result.Resolution.TotalMatches;

        // The Skill fired and reset its own counter...
        Assert.Equal(0, result.State.BossState.SkillCharge);

        // ...while the Passive progress still carries the Player Matches its own step
        // accumulated, unchanged by the cast.
        Assert.Equal(matches, result.State.BossState.PassiveProgress.Current);
    }

    [Fact]
    public async Task BossPassive_ShouldNotResetTheSkillCharge()
    {
        // The other direction of the same independence (§3.7): a Passive trigger must
        // not clear the Skill's charge.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var boss = BossDefinitions.HoaLong with
        {
            SkillDefinition = BossDefinitions.HoaLong.SkillDefinition with { ChargeRequirement = 1000 },
            PassiveDefinition = BossDefinitions.HoaLong.PassiveDefinition with { Threshold = 1 },
        };

        const string battleId = "boss-passive-independent";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var pinned = RoundTestSupport.PlayerMatch(GemType.Def, created.RngState, created.LastCommittedSwapPair);

        var previousCharge = 3;

        await RoundTestSupport.PinAsync(
            repository,
            created,
            pinned.Board,
            bossState: created.BossState with { SkillCharge = previousCharge, SkillCooldown = 0 });

        var result = (await service.ExecuteSwapAsync(battleId, pinned.Swap))!.Value;

        Assert.True(result.IsAccepted);

        // The Passive triggered (Threshold 1) and reset only its own progress.
        Assert.Contains(
            result.Events,
            e => e.Type == BattleEventType.PassiveTriggered
                && e.PassiveTriggered.Source == PassiveEventSource.Boss);

        Assert.Equal(0, result.State.BossState.PassiveProgress.Current);

        // The Skill's charge moved only by the Boss's own POWER match groups
        // (ADR-026 D2) — that trigger cleared nothing.
        Assert.Equal(
            Math.Min(previousCharge + RoundTestSupport.BossPowerMatchGroups(result), boss.SkillChargeRequirement),
            result.State.BossState.SkillCharge);
    }

    // =======================================================================
    // Boss Basic Attack — GAME_RULES.md §17 step 18d, COMBAT_RULES.md §3.4
    // (ADR-026 D1 / §5: the fallback attack is retired and this is the Boss's
    // only damage instance)
    // =======================================================================

    [Fact]
    public async Task BossBoardAttack_ShouldUseEffectiveBossAtkPlusTheAtkGemPool_AndComboOne()
    {
        // COMBAT_RULES.md §3.4 item 1: a Boss attack occurs if and only if the Boss's
        // swap produced a confirmed ATK-Gem match, and its Step 1 is "Base Damage =
        // EffectiveBossATK + BaseDamagePool". The retired fallback — "Base Damage =
        // Boss.ATK" independently of the board (ADR-026 §5) — is gone: with no Boss ATK
        // modifier active, EffectiveBossATK is BossState.ATK and the pool is what the
        // ATK Gems the Boss's own swap cleared generated.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var boss = BossDefinitions.HoaLong with
        {
            // No Skill fires and no Passive triggers in this window, so the Boss carries
            // no ATK modifier and EffectiveBossATK is the base stat.
            SkillDefinition = BossDefinitions.HoaLong.SkillDefinition with { ChargeRequirement = 1000 },
            PassiveDefinition = BossDefinitions.HoaLong.PassiveDefinition with { Threshold = 100 },
        };

        const string battleId = "boss-basic-damage";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var petState = created.PetState with { HP = 1_000_000, MaxHP = 1_000_000 };
        var state = created with { PetState = petState };

        await RoundTestSupport.PinAsync(repository, created, created.BoardState, petState: petState);

        var observed = false;

        for (var round = 0; round < 8 && !observed; round++)
        {
            var pinned = RoundTestSupport.PlayerMatch(
                round % 2 == 0 ? GemType.Def : GemType.Hp,
                state.RngState,
                state.LastCommittedSwapPair);

            await RoundTestSupport.PinAsync(repository, state, pinned.Board);

            var result = (await service.ExecuteSwapAsync(battleId, pinned.Swap))!.Value;

            Assert.True(result.IsAccepted);

            // The Player's pinned Swap cleared no ATK Gem, so §17 steps 15–17 produced
            // no Player instance: every damage instance here is the Boss's, or there is
            // none at all (D1).
            var calculations = result.Events
                .Where(e => e.Type == BattleEventType.DamageCalculated)
                .Select(e => e.DamageCalculated)
                .ToArray();

            var bossDamageInstances = result.Events.Count(
                e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Boss);

            Assert.Equal(bossDamageInstances, calculations.Length);

            if (!RoundTestSupport.BossClearedAtkGem(result))
            {
                // D1: no ATK match, so no attack instance at all — the pipeline does not
                // run and the ATK stat is not consumed.
                Assert.Empty(calculations);
                state = result.State;
                continue;
            }

            var bossDamage = Assert.Single(calculations);

            var effectiveBossAtk = StatusEffectLifecycle.EffectiveBossAttack(
                result.State.BossState.ATK,
                result.State.BossState.ActiveStatusEffects);

            // No Boss ATK modifier is active in this scenario (§6.2/§5.5.1).
            Assert.Equal(boss.ATK, effectiveBossAtk);

            // The ATK Gems the Boss's own swap cleared generated a non-zero pool, and
            // Step 1 is EffectiveBossATK + that pool (COMBAT_RULES.md §3.4 item 1).
            var pool = BossAtkGemPool(result);

            Assert.True(pool > 0);
            Assert.Equal(effectiveBossAtk + pool, bossDamage.Base);
            Assert.Equal(boss.ATK + pool, bossDamage.Base);

            // §3.4 item 2: Combo Modifier = 1 — a Boss attack is not part of a Combo
            // chain.
            Assert.Equal(1.00, bossDamage.ComboModifier);

            observed = true;
            state = result.State;
        }

        Assert.True(observed, "the Boss's board action must produce a qualifying ATK match within 8 rounds");
    }

    [Fact]
    public async Task BossBoardAttack_ShouldDefendWithTheActivePetElementAndThePetDef()
    {
        // COMBAT_RULES.md §3.4 step 3 / §3.2: the defending Element is the ACTIVE
        // PET's ("the defender is the Pet, not the Player" — a Player has no Element),
        // and the mitigation input is the active Pet's DEF (GAME_STATE.md §2.3,
        // ADR-011 items 3 and 5). The matchup the assertion reads is resolved from the
        // two documented Elements.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var boss = BossDefinitions.HoaLong with
        {
            SkillDefinition = BossDefinitions.HoaLong.SkillDefinition with { ChargeRequirement = 1000 },
            PassiveDefinition = BossDefinitions.HoaLong.PassiveDefinition with { Threshold = 100 },
        };

        const string battleId = "boss-basic-defender";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var petState = created.PetState with { HP = 1_000_000, MaxHP = 1_000_000 };
        var state = created with { PetState = petState };

        await RoundTestSupport.PinAsync(repository, created, created.BoardState, petState: petState);

        DamageCalculation? bossDamage = null;

        for (var round = 0; round < 8 && bossDamage is null; round++)
        {
            var pinned = RoundTestSupport.PlayerMatch(
                round % 2 == 0 ? GemType.Def : GemType.Hp,
                state.RngState,
                state.LastCommittedSwapPair);

            await RoundTestSupport.PinAsync(repository, state, pinned.Board);

            var result = (await service.ExecuteSwapAsync(battleId, pinned.Swap))!.Value;

            Assert.True(result.IsAccepted);

            if (RoundTestSupport.BossClearedAtkGem(result))
            {
                bossDamage = Assert.Single(
                    result.Events,
                    e => e.Type == BattleEventType.DamageCalculated).DamageCalculated;
            }

            state = result.State;
        }

        Assert.NotNull(bossDamage);

        var expectedMatchup = ElementMatchups.Resolve(boss.Element, Pet.Element);

        Assert.Equal(ElementModifiers.Default.For(expectedMatchup), bossDamage!.Value.ElementModifier);

        // §3.4 step 4: Other Modifiers = 1.0 for a Boss attack (its Crit input is 0).
        Assert.Equal(DamagePipeline.NoOtherModifiers, bossDamage.Value.OtherModifiers);

        // §3.2 / §3.4 step 5: the mitigation factor is K / (K + the active Pet's DEF),
        // applied to the instance's own pre-defense damage.
        var expectedDefense = bossDamage.Value.Base
            * ElementModifiers.Default.For(expectedMatchup)
            * (DamagePipeline.DefenseMitigationConstant
                / (double)(DamagePipeline.DefenseMitigationConstant + created.PetState.DEF));

        Assert.Equal(expectedDefense, bossDamage.Value.Defense, precision: 9);

        // §3 step 6: Final Damage is the truncation of that value toward zero.
        Assert.Equal((int)Math.Truncate(expectedDefense), bossDamage.Value.FinalDamage);
    }

    [Fact]
    public async Task BossBoardAttack_ShouldWriteThePetsHp_ExactlyWhenTheBoardQualified()
    {
        // COMBAT_RULES.md §3.4 step 6: "Final Damage applied to active Pet HP" names
        // the Player side of the instance — and the Pet is that side's combat character
        // (ADR-011 items 3 and 5), so the HP written is the active Pet's PetState.HP
        // (GAME_STATE.md §2.3, §5.1). ADR-026 D1/§5 makes that write conditional: a
        // round in which the Boss's own swap cleared no ATK Gem deals no damage and
        // writes no HP.
        //
        // The Player's swapped Gems are pinned to POWER, so its phase writes the Pet's
        // Power only — no HP-Gem healing and no DEF-Gem Shield — and the Pet's HP change
        // in the round is the Boss's instance alone.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var boss = BossDefinitions.HoaLong with
        {
            SkillDefinition = BossDefinitions.HoaLong.SkillDefinition with { ChargeRequirement = 1000 },
        };

        const string battleId = "boss-basic-playerhp";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var petState = created.PetState with { HP = 1_000_000, MaxHP = 1_000_000 };
        var state = created with { PetState = petState };

        await RoundTestSupport.PinAsync(repository, created, created.BoardState, petState: petState);

        var sawQualifyingRound = false;
        var sawNonQualifyingRound = false;

        for (var round = 0; round < 8; round++)
        {
            var pinned = RoundTestSupport.PlayerMatch(
                GemType.Power,
                state.RngState,
                state.LastCommittedSwapPair);

            await RoundTestSupport.PinAsync(repository, state, pinned.Board);

            var before = state;
            var result = (await service.ExecuteSwapAsync(battleId, pinned.Swap))!.Value;

            Assert.True(result.IsAccepted);

            var bossDealt = result.Events
                .Where(e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Boss)
                .Select(e => e.DamageDealt)
                .ToArray();

            if (RoundTestSupport.BossClearedAtkGem(result))
            {
                var dealt = Assert.Single(bossDealt);

                Assert.Equal(DamageParty.Boss, dealt.Source);
                Assert.Equal(DamageParty.Player, dealt.Target);
                Assert.True(dealt.Amount > 0);

                // The Pet's HP must fall by exactly the amount the Boss→Player instance
                // reports, in the same write-back (GAME_STATE.md §5.1).
                Assert.Equal(before.PetState.HP - dealt.Amount, result.State.PetState.HP);
                Assert.True(result.State.PetState.HP < before.PetState.HP);

                sawQualifyingRound = true;
            }
            else
            {
                // ADR-026 D1: no ATK Gem cleared ⇒ no instance, no damage, no HP write.
                Assert.Empty(bossDealt);
                Assert.Equal(before.PetState.HP, result.State.PetState.HP);

                sawNonQualifyingRound = true;
            }

            state = result.State;
        }

        Assert.True(sawQualifyingRound, "the Boss's board action must produce a qualifying ATK match within 8 rounds");
        Assert.True(sawNonQualifyingRound, "the scenario must also exercise a round in which the Boss cleared no ATK Gem");
    }

    [Fact]
    public async Task BossResponse_ShouldCastTheSkillOnlyWhenEligible_AndDealDamageOnlyThroughItsBoardAction()
    {
        // GAME_STATE.md §2.4.3 / BOSS_RULES.md §6.3: the Skill is taken when
        // SkillCharge >= SkillChargeRequirement AND SkillCooldown == 0.
        //
        // ADR-026 D3/§5 retires the two things this suite used to assert about the
        // response's damage: the Skill deals no direct damage at all (all five MVP
        // Skills declare BaseDamage = 0), and there is no fallback basic attack. A
        // round therefore holds one Boss damage instance exactly when the Boss's own
        // swap cleared an ATK Gem (§3.4 item 1) — whether or not the Skill fired.
        foreach (var skillEligible in new[] { true, false })
        {
            var repository = new InMemoryBattleStateRepository();
            var service = new BattleStateService(repository, new FixedRngSeedSource());

            var boss = BossDefinitions.HoaLong with
            {
                // The requirement is raised to an unreachable value, so the Skill's
                // charge clause is decided by this scenario's pinned charge alone.
                SkillDefinition = BossDefinitions.HoaLong.SkillDefinition with { ChargeRequirement = 1000 },
            };

            var battleId = $"boss-response-damage-{(skillEligible ? "casting" : "silent")}";
            var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

            var petState = created.PetState with { HP = 1_000_000, MaxHP = 1_000_000 };
            var state = created with { PetState = petState };

            await RoundTestSupport.PinAsync(repository, created, created.BoardState, petState: petState);

            var sawQualifyingRound = false;
            var sawNonQualifyingRound = false;

            for (var round = 0; round < 8; round++)
            {
                var pinned = RoundTestSupport.PlayerMatch(
                    round % 2 == 0 ? GemType.Def : GemType.Hp,
                    state.RngState,
                    state.LastCommittedSwapPair);

                await RoundTestSupport.PinAsync(
                    repository,
                    state,
                    pinned.Board,
                    bossState: state.BossState with
                    {
                        SkillCharge = skillEligible ? boss.SkillChargeRequirement : 0,
                        SkillCooldown = 0,
                    });

                var result = (await service.ExecuteSwapAsync(battleId, pinned.Swap))!.Value;

                Assert.True(result.IsAccepted);

                var casts = result.Events.Count(e => e.Type == BattleEventType.BossSkillCast);
                var bossDamageInstances = result.Events.Count(
                    e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Boss);
                var calculations = result.Events.Count(e => e.Type == BattleEventType.DamageCalculated);

                var bossQualified = RoundTestSupport.BossClearedAtkGem(result);

                // §2.4.3: both conditions, and nothing else, decide the cast.
                Assert.Equal(skillEligible ? 1 : 0, casts);

                // D3: a fired Skill contributes no damage instance, and the Player's
                // pinned Swap cleared no ATK Gem — so the round's damage instances are
                // the Boss's ATK-gated board instance's, one exactly when the gate
                // opened, and none otherwise (D1).
                Assert.Equal(bossQualified ? 1 : 0, bossDamageInstances);
                Assert.Equal(bossDamageInstances, calculations);

                sawQualifyingRound |= bossQualified;
                sawNonQualifyingRound |= !bossQualified;

                state = result.State;
            }

            Assert.True(
                sawQualifyingRound,
                $"the Boss's board action must produce a qualifying ATK match within 8 rounds (skillEligible: {skillEligible})");

            Assert.True(
                sawNonQualifyingRound,
                $"the scenario must also exercise a round with no Boss ATK match (skillEligible: {skillEligible})");
        }
    }

    [Fact]
    public async Task BossResponse_ShouldRunAfterThePlayerPhase_InTheDocumentedOrder()
    {
        // ADR-026 §2 / GAME_RULES.md §17 step 18: the whole Boss response phase
        // follows the Player action phase, and BOSS_RULES.md §3.3 orders its own
        // steps — the Passive "fires once per player action, after all player damage
        // is resolved" (item 1), before the Skill (item 2), and before the Boss's
        // attack (item 3).
        //
        // The Player's pinned Swap produces an ATK match, so its own damage instance
        // exists in every round under test; the Boss is pinned to cast, and the round
        // in which its board action also qualifies is the one whose attack instance is
        // ordered after the cast.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var boss = BossDefinitions.HoaLong with
        {
            // The Skill is made eligible by the pinned charge; the larger MaxHP keeps
            // the Player's damage from ending the battle inside the window.
            SkillDefinition = BossDefinitions.HoaLong.SkillDefinition with { ChargeRequirement = 1000 },
            MaxHP = 1_000_000,
        };

        const string battleId = "boss-response-order";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var petState = created.PetState with { HP = 1_000_000, MaxHP = 1_000_000 };
        var state = created with { PetState = petState };

        await RoundTestSupport.PinAsync(repository, created, created.BoardState, petState: petState);

        var observed = false;

        for (var round = 0; round < 8 && !observed; round++)
        {
            var pinned = RoundTestSupport.PlayerMatch(
                GemType.Atk,
                state.RngState,
                state.LastCommittedSwapPair);

            Assert.True(pinned.Resources.ClearedGemCounts.Atk > 0);

            await RoundTestSupport.PinAsync(
                repository,
                state,
                pinned.Board,
                bossState: state.BossState with
                {
                    SkillCharge = boss.SkillChargeRequirement,
                    SkillCooldown = 0,
                });

            var result = (await service.ExecuteSwapAsync(battleId, pinned.Swap))!.Value;

            Assert.True(result.IsAccepted);

            var events = result.Events;

            var playerDamageIndex = IndexOf(
                events,
                e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Player);
            var bossBoardStartIndex = BossBoardStartIndex(result);
            var bossPassiveIndex = IndexOf(
                events,
                e => e.Type == BattleEventType.PassiveCharged && e.PassiveCharged.Source == PassiveEventSource.Boss);
            var castIndex = IndexOf(events, e => e.Type == BattleEventType.BossSkillCast);

            Assert.True(playerDamageIndex >= 0, "the Player's ATK-gated instance must exist (ADR-026 D1)");
            Assert.True(bossBoardStartIndex >= 0, "the Boss's own board action must be reported (GAME_EVENTS.md §1.1)");

            // The Boss response phase follows the Player's damage (§17 step 18).
            Assert.True(
                playerDamageIndex < bossBoardStartIndex,
                "the Boss's board action must follow the Player phase (ADR-026 §2, GAME_RULES.md §17)");

            // §3.3 item 1: the Passive resolves at step 18b, after the Boss's board
            // action (18a) and after the Player's damage.
            Assert.True(bossBoardStartIndex < bossPassiveIndex);
            Assert.True(playerDamageIndex < bossPassiveIndex);

            // §3.3 item 2: the Skill is evaluated after the Passive resolves.
            Assert.True(bossPassiveIndex < castIndex);

            if (RoundTestSupport.BossClearedAtkGem(result))
            {
                var bossDamageIndex = IndexOf(
                    events,
                    e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Boss);

                // §17 step 18d: the Boss's ATK-gated instance follows its own Skill.
                Assert.True(bossDamageIndex > castIndex);
                Assert.True(playerDamageIndex < bossDamageIndex);

                observed = true;
            }

            state = result.State;
        }

        Assert.True(
            observed,
            "the scenario must exercise a round in which the Boss cast its Skill and cleared an ATK Gem");
    }

    // =======================================================================
    // Turn and cooldown lifecycle — MATCH3_RULES.md §8.1, BOSS_RULES.md §6.3
    // =======================================================================

    [Fact]
    public async Task CommittedSwap_ShouldAdvanceTurnExactlyOnce()
    {
        // MATCH3_RULES.md §8.1 item 1 / §8.3: one committed Swap begins exactly one
        // Turn. SwapExecutor already performs the increment in its own write-back
        // (GAME_STATE.md §5.1), and BattleStateService must NOT increment again — so
        // the stored Turn advances by exactly 1, never 2. ADR-026 D4 extends that to
        // the whole atomic round: the Boss response phase writes Turn not at all.
        var service = NewService();
        var created = await service.CreateBattleAsync("boss-turn-once", Owner, Pet, BossDefinitions.HoaLong);

        var pair = FindMatchProducingPair(created);
        var result = await service.ExecuteSwapAsync("boss-turn-once", pair);

        Assert.True(result!.Value.IsAccepted);

        Assert.Equal(BattleState.InitialTurn + 1, result.Value.State.Turn);
        Assert.Equal(created.Turn + 1, result.Value.State.Turn);
        Assert.Equal(created.Sequence + 1, result.Value.State.Sequence);

        // The registry holds the same single write-back (GAME_STATE.md §5.1).
        Assert.Equal(result.Value.State.Turn, (await service.GetBattleAsync("boss-turn-once"))!.Turn);
    }

    [Fact]
    public async Task CommittedSwaps_ShouldAdvanceTurnOnceEach()
    {
        // Two committed Swaps leave Turn at 2 — one per Swap, never more. This is the
        // regression guard for a second increment being added to the resolution.
        var service = NewService();
        var created = await service.CreateBattleAsync("boss-turn-twice", Owner, Pet, BossDefinitions.HoaLong);

        var firstPair = FindMatchProducingPair(created);
        var first = await service.ExecuteSwapAsync("boss-turn-twice", firstPair);
        Assert.True(first!.Value.IsAccepted);

        var afterFirst = (await service.GetBattleAsync("boss-turn-twice"))!;
        var secondPair = FindMatchProducingPair(afterFirst);
        var second = await service.ExecuteSwapAsync("boss-turn-twice", secondPair);
        Assert.True(second!.Value.IsAccepted);

        Assert.Equal(BattleState.InitialTurn + 2, second.Value.State.Turn);
        Assert.Equal(BattleState.InitialSequence + 2, second.Value.State.Sequence);
    }

    [Fact]
    public async Task RejectedSwap_ShouldNotAdvanceTurnOrCoolTheBossDown()
    {
        // MATCH3_RULES.md §2.1.5: a rejected action writes nothing. It is not a Turn,
        // so the cooldown does not decrement either — the decrement is tied to the
        // Turn a committed Swap begins (BOSS_RULES.md §6.3).
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());
        var (battleId, _) = await BattleWithACoolingBoss(repository, service, "boss-rejected-no-turn");

        var before = (await service.GetBattleAsync(battleId))!;
        var turnBefore = before.Turn;
        var cooldownBefore = before.BossState.SkillCooldown;

        Assert.True(cooldownBefore > 0);

        // An out-of-range request is rejected by the validator's index check.
        var result = await service.ExecuteSwapAsync(battleId, new SwapRequest(99, 100));

        Assert.True(result!.Value.IsRejected);
        Assert.Empty(result.Value.Events);

        var stored = (await service.GetBattleAsync(battleId))!;

        Assert.Equal(turnBefore, stored.Turn);
        Assert.Equal(cooldownBefore, stored.BossState.SkillCooldown);
    }

    [Fact]
    public async Task SkillCooldown_ShouldDecrementOncePerCommittedSwapAfterTheTurn()
    {
        // BOSS_RULES.md §6.3: the cooldown "Decrements by 1 at each Turn increment",
        // and MATCH3_RULES.md §8.1 makes one committed Swap begin exactly one Turn. So
        // one Swap decrements it by exactly 1 — not by 0, and not by 2. ADR-026 D4's
        // atomic round keeps that: the decrement runs once, at the start of the round,
        // before the Boss's own board action.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());
        var (battleId, _) = await BattleWithACoolingBoss(repository, service, "boss-cooldown-decrement");

        var before = (await service.GetBattleAsync(battleId))!;
        var pair = FindMatchProducingPair(before);
        var result = await service.ExecuteSwapAsync(battleId, pair);

        Assert.True(result!.Value.IsAccepted);

        Assert.Equal(before.BossState.SkillCooldown - 1, result.Value.State.BossState.SkillCooldown);
        Assert.Equal(before.Turn + 1, result.Value.State.Turn);
    }

    [Fact]
    public async Task SkillCooldown_ShouldTickDownOnePerSwapAndLetTheSkillFireAgain()
    {
        // The documented lifecycle end to end (BOSS_RULES.md §6.3, GAME_STATE.md
        // §2.4.3): a cast sets the cooldown to SkillCooldownTurns, each subsequent
        // committed round decrements it by exactly 1 *before* eligibility is
        // evaluated, and the Skill fires again on the round that brings it to 0.
        //
        // That ordering is the rule, not an accident: §6.3 says the cooldown
        // "Decrements by 1 at each Turn increment" and blocks the Skill "while
        // CD > 0" — so a cooldown of N blocks N−1 rounds and the Nth round is the one
        // it is eligible on again.
        //
        // The charge clause is arranged rather than asserted here, because ADR-026 D2
        // makes SkillCharge the Boss's own POWER matches' counter and no single board
        // can be relied upon to refill it after a cast's reset. The cooldown side of
        // the condition runs entirely through the real path.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var boss = BossDefinitions.HoaLong;

        const string battleId = "boss-cooldown-lifecycle";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var state = created;

        var firstPair = RoundTestSupport.PlayerMatch(GemType.Def, state.RngState, state.LastCommittedSwapPair);

        await RoundTestSupport.PinAsync(
            repository,
            state,
            firstPair.Board,
            bossState: state.BossState with { SkillCharge = boss.SkillChargeRequirement, SkillCooldown = 0 });

        var first = (await service.ExecuteSwapAsync(battleId, firstPair.Swap))!.Value;

        Assert.True(first.IsAccepted);
        Assert.Contains(first.Events, e => e.Type == BattleEventType.BossSkillCast);
        Assert.Equal(boss.SkillCooldownTurns, first.State.BossState.SkillCooldown);

        state = first.State;

        // Each blocked round decrements by exactly one and fires nothing.
        for (var blocked = 1; blocked < boss.SkillCooldownTurns; blocked++)
        {
            Assert.Equal(boss.SkillCooldownTurns - (blocked - 1), state.BossState.SkillCooldown);

            var nextPair = RoundTestSupport.PlayerMatch(GemType.Def, state.RngState, state.LastCommittedSwapPair);

            await RoundTestSupport.PinAsync(
                repository,
                state,
                nextPair.Board,
                bossState: state.BossState with { SkillCharge = boss.SkillChargeRequirement });

            var result = (await service.ExecuteSwapAsync(battleId, nextPair.Swap))!.Value;

            Assert.True(result.IsAccepted);

            Assert.DoesNotContain(
                result.Events,
                e => e.Type == BattleEventType.BossSkillCast);

            Assert.Equal(
                boss.SkillCooldownTurns - blocked,
                result.State.BossState.SkillCooldown);

            state = result.State;
        }

        // The cooldown is now 1. The next committed round decrements it to 0 and the
        // Skill fires on that same round — the first round it is eligible.
        Assert.Equal(1, state.BossState.SkillCooldown);

        var finalPair = RoundTestSupport.PlayerMatch(GemType.Def, state.RngState, state.LastCommittedSwapPair);

        await RoundTestSupport.PinAsync(
            repository,
            state,
            finalPair.Board,
            bossState: state.BossState with { SkillCharge = boss.SkillChargeRequirement });

        var recast = (await service.ExecuteSwapAsync(battleId, finalPair.Swap))!.Value;

        Assert.True(recast.IsAccepted);
        Assert.Contains(recast.Events, e => e.Type == BattleEventType.BossSkillCast);
        Assert.Equal(boss.SkillCooldownTurns, recast.State.BossState.SkillCooldown);
    }

    [Fact]
    public async Task SkillCooldown_ShouldNotDecrementBelowZero()
    {
        // §2.4.3 blocks the Skill "while > 0", so 0 is the floor — the decrement
        // cannot drive the cooldown negative.
        //
        // The branch is read from the events rather than assumed, because whether the
        // Skill fires depends on whether the Boss's own POWER matches filled the charge
        // to the Skill's requirement during this round (ADR-026 D2, GAME_STATE.md
        // §2.4.3): a cast SETS the cooldown to SkillCooldownTurns, and only an un-fired
        // Skill leaves the decrement's result. The floor rule holds either way, so both
        // are asserted.
        var service = NewService();
        var created = await service.CreateBattleAsync("boss-cooldown-floor", Owner, Pet, BossDefinitions.HoaLong);

        Assert.Equal(0, created.BossState.SkillCooldown);

        var pair = FindMatchProducingPair(created);
        var result = await service.ExecuteSwapAsync("boss-cooldown-floor", pair);

        Assert.True(result!.Value.IsAccepted);

        var after = result.Value.State.BossState;
        var skillFired = result.Value.Events.Any(e => e.Type == BattleEventType.BossSkillCast);

        // Never negative, in either case.
        Assert.True(after.SkillCooldown >= 0);

        if (skillFired)
        {
            // A cast set it to the Boss's own cooldown value (BOSS_RULES.md §6.3).
            Assert.Equal(BossDefinitions.HoaLong.SkillCooldownTurns, after.SkillCooldown);
        }
        else
        {
            // No cooldown was running, so the decrement left it at the floor.
            Assert.Equal(0, after.SkillCooldown);
        }
    }

    // =======================================================================
    // Helpers
    // =======================================================================

    /// <summary>
    /// Builds the service under test over an <see cref="InMemoryBattleStateRepository"/>.
    ///
    /// <b>The repository is a TEST DOUBLE for the active-battle-state store.</b>
    /// <c>REDIS_STATE.md</c> §1–§4 make Redis the owner of Active Battle State, and
    /// §2 item 2 / §7 item 5 permit no in-process substitute for it in the running
    /// server — the production composition registers
    /// <c>GameServer.Infrastructure.Redis.BattleStateRepository</c> instead. This
    /// helper exists only so a test that asserts a <i>gameplay</i> rule can run the
    /// real <see cref="BattleStateService"/> pipeline without a live Redis instance;
    /// the double still models the documented creation/absence and Sequence
    /// compare-and-set semantics, so a test cannot pass against a more permissive
    /// contract than the store actually offers.
    /// </summary>
    private static BattleStateService NewService() =>
        new(new InMemoryBattleStateRepository(), new FixedRngSeedSource());

    /// <summary>
    /// Commits one Swap on a fresh battle whose Boss casts its Skill on that Swap, and
    /// returns the battle id. The cooldown is reached through the real path — a Skill
    /// that fires and sets <c>SkillCooldownTurns</c> (<c>BOSS_RULES.md</c> §6.3) — so
    /// the scenario starts from a state the game genuinely produces. The one arranged
    /// value is the documented <c>SkillCharge</c> (<c>GAME_STATE.md</c> §2.4.3), pinned
    /// to the definition's own requirement because ADR-026 D2 makes the counter the
    /// Boss's own POWER matches', which no single board can be relied upon to fill.
    /// </summary>
    private static async Task<(string BattleId, BossDefinition Boss)> BattleWithACoolingBoss(
        InMemoryBattleStateRepository repository,
        BattleStateService service,
        string battleId)
    {
        var boss = BossDefinitions.HoaLong;

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
        Assert.True((await service.GetBattleAsync(battleId))!.BossState.SkillCooldown > 0);

        return (battleId, boss);
    }

    /// <summary>
    /// A match-producing adjacent pair on <paramref name="state"/>'s board that is
    /// <b>not</b> the pair already recorded as committed
    /// (<c>MATCH3_RULES.md</c> §2.1.4).
    ///
    /// The committed pair is read from the authoritative state rather than taken from
    /// the caller, so a scenario can never accidentally hand the executor a replay and
    /// mistake the staleness rejection for a board problem.
    /// </summary>
    private static SwapRequest FindMatchProducingPair(BattleState state) =>
        FindMatchProducingPair(state, state.LastCommittedSwapPair);

    private static SwapRequest FindMatchProducingPair(
        BattleState state,
        CommittedSwapPair? committed)
    {
        foreach (var (from, to) in AllAdjacentPairs())
        {
            if (committed is { } pair
                && CommittedSwapPair.FromCells(from, to)
                    == CommittedSwapPair.FromCells(pair.MinCellIndex, pair.MaxCellIndex))
            {
                continue;
            }

            if (MatchDetector.Detect(state.BoardState.WithSwapped(from, to)).Count > 0)
            {
                return new SwapRequest(from, to);
            }
        }

        throw new InvalidOperationException(
            "A generated board has at least one valid Swap (MATCH3_RULES.md §1.4).");
    }

    private static IEnumerable<(int From, int To)> AllAdjacentPairs()
    {
        for (var index = 0; index < BoardState.CellCount; index++)
        {
            var right = BoardState.ToColumn(index) + 1 < BoardState.Columns ? index + 1 : -1;
            var down = index + BoardState.Width < BoardState.CellCount ? index + BoardState.Width : -1;

            if (right >= 0)
            {
                yield return (index, right);
            }

            if (down >= 0)
            {
                yield return (index, down);
            }
        }
    }

    /// <summary>
    /// Whether the Boss's own board action cleared an HP Gem — the other Boss-HP write
    /// of a round, beside the Passive's regeneration (<c>ADR-026</c> §4,
    /// <c>COMBAT_RULES.md</c> §2: HP Gems heal the acting actor).
    /// </summary>
    private static bool BossClearedHpGem(SwapExecutionResult result) =>
        RoundTestSupport.BossBoardEvents(result).Any(
            e => e.Type == BattleEventType.GemMatched && e.Gem.GemType == GemType.Hp);

    /// <summary>
    /// The <c>BaseDamagePool</c> the Boss's own board action generated from ATK Gems,
    /// read from that action's own committed events and from the rates
    /// <c>COMBAT_RULES.md</c> §2 owns: each ATK Gem a Match consumed generates
    /// <c>10 × that shape's tier</c>, and an ATK Gem a Special Gem activation cleared
    /// generates at the Match-3 base rate because an activation is not a Match (§2
    /// items 1–2). It is the pool <c>COMBAT_RULES.md</c> §3.4 step 1 adds to
    /// <c>EffectiveBossATK</c>.
    ///
    /// The walk is per pass, because generation is per cleared cell and not per cell
    /// index (<c>MATCH3_RULES.md</c> §5.8.2 item 5): a cell cleared by pass 1 holds a
    /// different Gem by pass 2 and generates again then, at that pass's tier.
    /// </summary>
    private static int BossAtkGemPool(SwapExecutionResult result)
    {
        var matchedInPass = new HashSet<int>();
        var pool = 0;

        foreach (var boardEvent in RoundTestSupport.BossBoardEvents(result))
        {
            switch (boardEvent.Type)
            {
                case BattleEventType.CascadeCreated:
                    // A new pass begins (MATCH3_RULES.md §4.2): its cleared cells are a
                    // fresh set.
                    matchedInPass.Clear();
                    break;

                case BattleEventType.MatchCreated:
                    var shape = boardEvent.Match.Shape;

                    var tier = shape.IsLt
                        ? MatchTier.Lt
                        : MatchTierMultipliers.ForStraightRun((shape.HorizontalArm ?? shape.VerticalArm)!.Length);
                    var numerator = MatchTierMultipliers.NumeratorFor(tier);

                    foreach (var cell in shape.Cells)
                    {
                        matchedInPass.Add(cell);

                        if (shape.GemType == GemType.Atk)
                        {
                            pool += (ResourceGenerator.AtkGemBaseDamage * numerator)
                                / MatchTierMultipliers.Denominator;
                        }
                    }

                    break;

                case BattleEventType.GemMatched:
                    // A cell the pass's Matches did not consume is an activation's, and
                    // generates at the base rate (§2 item 1).
                    if (boardEvent.Gem.GemType == GemType.Atk
                        && !matchedInPass.Contains(boardEvent.Gem.CellIndex))
                    {
                        pool += ResourceGenerator.AtkGemBaseDamage;
                    }

                    break;
            }
        }

        return pool;
    }

    /// <summary>
    /// The position of the first event satisfying <paramref name="predicate"/>, or
    /// <c>-1</c> when the round produced none.
    /// </summary>
    private static int IndexOf(IReadOnlyList<BattleEvent> events, Func<BattleEvent, bool> predicate)
    {
        for (var index = 0; index < events.Count; index++)
        {
            if (predicate(events[index]))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// The position at which the <b>Boss response phase's</b> Match-3 cycle begins in
    /// the committed list — the start of the round's second contiguous board-event
    /// run (<see cref="RoundTestSupport.PlayerBoardEvents"/>,
    /// <see cref="RoundTestSupport.BossBoardEvents"/>, <c>GAME_EVENTS.md</c> §1, §1.1).
    /// </summary>
    /// <remarks>
    /// The Player's cycle is the first board-event run, not the list's prefix: the
    /// committed list opens with the action's own <c>SwapStarted</c>/<c>SwapResolved</c>
    /// pair, which is not a board event (<c>SIGNALR_PROTOCOL.md</c>
    /// §3.2.26–§3.2.27).
    /// </remarks>
    private static int BossBoardStartIndex(SwapExecutionResult result)
    {
        var events = result.Events;
        var index = 0;

        while (index < events.Count && !IsBoardEvent(events[index]))
        {
            index++;
        }

        while (index < events.Count && IsBoardEvent(events[index]))
        {
            index++;
        }

        for (; index < events.Count; index++)
        {
            if (IsBoardEvent(events[index]))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Whether a Battle Event is part of a Match-3 board cycle
    /// (<c>GAME_EVENTS.md</c> §1.1), including the pass's own closing
    /// <c>CascadeStepResolved</c> (§1.1 item 8).
    /// </summary>
    private static bool IsBoardEvent(BattleEvent battleEvent) =>
        battleEvent.Type
            is BattleEventType.MatchCreated
            or BattleEventType.CascadeCreated
            or BattleEventType.GemMatched
            or BattleEventType.ComboChanged
            or BattleEventType.CascadeStepResolved;
}
