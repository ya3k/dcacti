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
            // No Passive triggers in this window (and this content carries no
            // provisioned Boss Card, so no ability cast can add a modifier either), so
            // the Boss carries no ATK modifier and EffectiveBossATK is the base stat.
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
            PassiveDefinition = BossDefinitions.HoaLong.PassiveDefinition with { Threshold = 100 },
        };

        const string battleId = "boss-basic-defender";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var petState = created.PetState with { HP = 1_000_000, MaxHP = 1_000_000 };
        var state = created with { PetState = petState };

        await RoundTestSupport.PinAsync(repository, created, created.BoardState, petState: petState);

        DamageCalculation? bossDamage = null;
        var roundDefensePool = 0;

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
                roundDefensePool = result.Resources.DefensePool;
            }

            state = result.State;
        }

        Assert.NotNull(bossDamage);

        var expectedMatchup = ElementMatchups.Resolve(boss.Element, Pet.Element);

        Assert.Equal(ElementModifiers.Default.For(expectedMatchup), bossDamage!.Value.ElementModifier);

        // §3.4 step 4: Other Modifiers = 1.0 for a Boss attack (its Crit input is 0).
        Assert.Equal(DamagePipeline.NoOtherModifiers, bossDamage.Value.OtherModifiers);

        // §3.2 / §3.4 step 5: the mitigation factor is K / (K + the active Pet's
        // EFFECTIVE DEF), applied to the instance's own pre-defense damage.
        // COMBAT_RULES.md §5.4.6 (TASK-299): the round's Player DEF-Gem match grants a
        // one-Turn DEF buff whose magnitude is that action's generated DefensePool, so
        // the DEF this Boss instance is mitigated by is the stored base plus this
        // round's pool (the round's own report of it — a HP round generates none).
        var effectivePetDefense = created.PetState.DEF + roundDefensePool;

        var expectedDefense = bossDamage.Value.Base
            * ElementModifiers.Default.For(expectedMatchup)
            * (DamagePipeline.DefenseMitigationConstant
                / (double)(DamagePipeline.DefenseMitigationConstant + effectivePetDefense));

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
    public async Task BossResponse_ShouldDealDamageOnlyThroughItsBoardAction()
    {
        // ADR-026 D3/§5: a Boss ability contributes no direct damage, and there is no
        // fallback basic attack. A round therefore holds one Boss damage instance
        // exactly when the Boss's own swap cleared an ATK Gem (§3.4 item 1). This
        // suite's content carries no provisioned Boss Card, so no ability cast occurs
        // at all; the legacy charge-driven Skill that used to make the cast condition
        // observable was removed with ADR-029 Slice 5 / TASK-313, and the canonical
        // cast's own no-damage rule is covered by TASK309BossCardEffectRuntimeTests.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var boss = BossDefinitions.HoaLong with
        {
            // The larger MaxHP keeps the battle alive inside the observation window.
            MaxHP = 1_000_000,
        };

        const string battleId = "boss-response-damage";
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
                bossState: state.BossState with { SkillCooldown = 0 });

            var result = (await service.ExecuteSwapAsync(battleId, pinned.Swap))!.Value;

            Assert.True(result.IsAccepted);

            // No Boss ability cast of either form can occur in this content.
            Assert.DoesNotContain(result.Events, e => e.Type == BattleEventType.BossCardCast);

            var bossDamageInstances = result.Events.Count(
                e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Boss);
            var calculations = result.Events.Count(e => e.Type == BattleEventType.DamageCalculated);

            var bossQualified = RoundTestSupport.BossClearedAtkGem(result);

            // D3 / D1: the round's damage instances are the Boss's ATK-gated board
            // instance's — one exactly when the gate opened, and none otherwise.
            Assert.Equal(bossQualified ? 1 : 0, bossDamageInstances);
            Assert.Equal(bossDamageInstances, calculations);

            sawQualifyingRound |= bossQualified;
            sawNonQualifyingRound |= !bossQualified;

            state = result.State;
        }

        Assert.True(
            sawQualifyingRound,
            "the Boss's board action must produce a qualifying ATK match within 8 rounds");

        Assert.True(
            sawNonQualifyingRound,
            "the scenario must also exercise a round with no Boss ATK match");
    }

    [Fact]
    public async Task BossResponse_ShouldRunAfterThePlayerPhase_InTheDocumentedOrder()
    {
        // ADR-026 §2 / GAME_RULES.md §17 step 18: the whole Boss response phase
        // follows the Player action phase, and BOSS_RULES.md §3.3 orders its own
        // steps — the Passive "fires once per player action, after all player damage
        // is resolved" (item 1), and before the Boss's attack (item 3).
        //
        // The Player's pinned Swap produces an ATK match, so its own damage instance
        // exists in every round under test; the round in which the Boss's board action
        // also qualifies is the one whose attack instance is ordered after the Passive.
        // A Boss ability cast is not part of this content (no provisioned Boss Card);
        // the canonical cast's own ordering after the Passive events is asserted by
        // BossCardCastRuntimeTests.Round_WhenCastSelected_...
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var boss = BossDefinitions.HoaLong with
        {
            // The larger MaxHP keeps the Player's damage from ending the battle.
            MaxHP = 1_000_000,
        };

        const string battleId = "boss-response-order";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var petState = created.PetState with { HP = 1_000_000, MaxHP = 1_000_000 };
        var state = created with { PetState = petState };

        await RoundTestSupport.PinAsync(repository, created, created.BoardState, petState: petState);

        var observed = false;

        // The window is a search over real rounds for the one whose Boss board action
        // also qualified (ADR-026 D1). Its length only bounds how long the search may
        // run: the assertions below are made on the qualifying round itself, so a
        // wider window finds it without weakening anything.
        for (var round = 0; round < 40 && !observed; round++)
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
                bossState: state.BossState with { SkillCooldown = 0 });

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

            if (RoundTestSupport.BossClearedAtkGem(result))
            {
                var bossDamageIndex = IndexOf(
                    events,
                    e => e.Type == BattleEventType.DamageDealt && e.DamageDealt.Source == DamageParty.Boss);

                // §17 step 18d: the Boss's ATK-gated instance follows the Passive.
                Assert.True(bossDamageIndex > bossPassiveIndex);
                Assert.True(playerDamageIndex < bossDamageIndex);

                observed = true;
            }

            state = result.State;
        }

        Assert.True(
            observed,
            "the scenario must exercise a round in which the Boss's board action cleared an ATK Gem");
    }

    [Fact]
    public async Task BossResponse_ShouldReportNoComboChanged_ForItsOwnBoardCycle()
    {
        // GAME_EVENTS.md §2 / SIGNALR_PROTOCOL.md §3.2.8 item 1: `ComboChanged`'s
        // payload is the NEW value of `BattleState.Combo`, and that counter is the
        // Player action's committed Swap's (GAME_STATE.md §2.2 — written in the
        // round's single write-back and delivered under the fixed `playerState`
        // label; BOSS_RULES.md §6.2/§6.2.5 read "Player Matches" / "Player Combo").
        // Step 18a resolves the Boss's own board action through the same Match-3
        // pipeline without writing either counter, so the Boss's cycle reports its
        // MatchCreated/GemMatched items and no Combo at all: a `ComboChanged` there
        // would carry a value the battle's counter never held, and a client renders
        // one Combo from the events and the published state together
        // (MATCH3_RULES.md §6.6 item 3).
        //
        // The scenario walks real rounds until the Boss's own cycle actually resolved
        // at least one Match, so the "no ComboChanged" assertion is not vacuous.
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var boss = BossDefinitions.HoaLong with { MaxHP = 1_000_000 };

        const string battleId = "boss-response-combo-contract";
        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var petState = created.PetState with { HP = 1_000_000, MaxHP = 1_000_000 };

        await RoundTestSupport.PinAsync(repository, created, created.BoardState, petState: petState);

        var state = created with { PetState = petState };
        var sawBossMatch = false;

        for (var round = 0; round < 8 && !sawBossMatch; round++)
        {
            var pinned = RoundTestSupport.PlayerMatch(
                GemType.Atk,
                state.RngState,
                state.LastCommittedSwapPair);

            await RoundTestSupport.PinAsync(repository, state, pinned.Board, petState: petState);

            var result = (await service.ExecuteSwapAsync(battleId, pinned.Swap))!.Value;

            Assert.True(result.IsAccepted);

            var bossRun = RoundTestSupport.BossBoardEvents(result);

            // The Player action's own cycle is the one the accounting counted, so it
            // does report its run — read from the round's own delivered list.
            Assert.Contains(
                RoundTestSupport.PlayerBoardEvents(result),
                e => e.Type == BattleEventType.ComboChanged);

            // GAME_STATE.md §2.2: `BattleState.Combo` is the Player action's
            // swap-scoped counter; the reported run ends at the committed value.
            Assert.Equal(
                result.State.Combo,
                RoundTestSupport.PlayerBoardEvents(result)
                    .Last(e => e.Type == BattleEventType.ComboChanged).Combo);

            // The Boss's own cycle reports no Combo, whatever it resolved.
            Assert.DoesNotContain(bossRun, e => e.Type == BattleEventType.ComboChanged);

            sawBossMatch |= bossRun.Any(e => e.Type == BattleEventType.MatchCreated);

            state = result.State;
        }

        Assert.True(
            sawBossMatch,
            "the scenario must exercise a round in which the Boss's own board cycle resolved a Match");
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
    public async Task SkillCooldown_ShouldNotDecrementBelowZero()
    {
        // §2.4.3 blocks the cast "while > 0", so 0 is the floor — the decrement cannot
        // drive the cooldown negative.
        //
        // This suite's content carries no provisioned Boss Card, so no ability cast can
        // set the cooldown in this window: the round's decrement is the only writer
        // under test, and it leaves the value at the floor. (A cast's own cooldown
        // RESET is covered canonically by BossCardCastRuntimeTests /
        // TASK309BossCardEffectRuntimeTests; the legacy charge-driven Skill that this
        // test's second branch observed was removed with ADR-029 Slice 5 / TASK-313.)
        var service = NewService();
        var created = await service.CreateBattleAsync("boss-cooldown-floor", Owner, Pet, BossDefinitions.HoaLong);

        Assert.Equal(0, created.BossState.SkillCooldown);

        var pair = FindMatchProducingPair(created);
        var result = await service.ExecuteSwapAsync("boss-cooldown-floor", pair);

        Assert.True(result!.Value.IsAccepted);

        var after = result.Value.State.BossState;

        Assert.DoesNotContain(result.Value.Events, e => e.Type == BattleEventType.BossCardCast);

        // Never negative, and untouched by a cast that cannot occur here.
        Assert.Equal(0, after.SkillCooldown);
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
    /// Creates a fresh battle whose Boss begins with a running <c>SkillCooldown</c>,
    /// and returns the battle id. This suite's content carries no provisioned Boss
    /// Card, so no ability cast can occur; the cooldown is therefore pinned as fixture
    /// state (it is <c>BossState</c> data, <c>GAME_STATE.md</c> §2.4.3) rather than
    /// reached through a legacy cast, which was removed with ADR-029 Slice 5 /
    /// TASK-313. The decrement and floor rules under test are unchanged, and the
    /// canonical cast path is covered by BossCardCastRuntimeTests /
    /// TASK309BossCardEffectRuntimeTests.
    /// </summary>
    private static async Task<(string BattleId, BossDefinition Boss)> BattleWithACoolingBoss(
        InMemoryBattleStateRepository repository,
        BattleStateService service,
        string battleId)
    {
        var boss = BossDefinitions.HoaLong;

        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        Assert.True(boss.SkillCooldownTurns > 0);

        await RoundTestSupport.PinAsync(
            repository,
            created,
            created.BoardState,
            bossState: created.BossState with { SkillCooldown = boss.SkillCooldownTurns });

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
    /// <c>10</c> — <c>BaseOutput × unique matched count</c>, with no match-tier
    /// multiplier (§2 item 6, OD-3). It is the pool <c>COMBAT_RULES.md</c> §3.4 step 1
    /// adds to <c>EffectiveBossATK</c>.
    ///
    /// The walk is per pass, because generation is counted per pass over that pass's
    /// unique cleared cells and accumulated (<c>MATCH3_RULES.md</c> §5.7 item 7): a
    /// cell cleared by pass 1 holds a different Gem by pass 2 and generates again
    /// then.
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

                    foreach (var cell in shape.Cells)
                    {
                        matchedInPass.Add(cell);

                        if (shape.GemType == GemType.Atk)
                        {
                            // COMBAT_RULES.md §2 item 6: BaseOutput per unique cell.
                            pool += ResourceGenerator.AtkGemBaseDamage;
                        }
                    }

                    break;

                case BattleEventType.GemMatched:
                    // A cell the pass's Matches did not consume is an activation's.
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
