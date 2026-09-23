using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using Xunit;

namespace GameServer.Application.Tests;

/// <summary>
/// Boss Response tests — <c>GAME_RULES.md</c> §17 steps 18a–18c, whose rules are
/// <c>BOSS_RULES.md</c> §3 (Passive), §4 (Skill), §5 (State/Enrage), §6 (MVP
/// reference), and <c>GAME_STATE.md</c> §2.4.1–§2.4.3.
///
/// <code>
/// Rule (BOSS_RULES.md §6.2–§6.4)
///  ↓
/// Scenario (Given the documented Boss and a committed Swap, When the response
///           runs, Then the documented state and events)
///  ↓
/// Test
/// </code>
///
/// <b>Every expected value traces to a section, never to the implementation.</b>
/// The Boss configuration is read from <see cref="BossDefinitions"/> (which
/// transcribes <c>BOSS_RULES.md</c> §6) and the Match totals from the
/// resolution's own report, so no assertion encodes "whatever the code does".
///
/// <b>The order this suite asserts.</b> <c>BOSS_RULES.md</c> §3.3 and §5 item 4:
/// the Passive fires once per player action, AFTER player damage and BEFORE the
/// Skill; the Skill is evaluated after the Passive; the Basic Attack is the
/// fallback when the Skill does not fire. Player→Boss damage →
/// Enrage → terminal Boss HP check → Passive → Skill-or-Basic → outcome.
/// </summary>
public class BossResponseTests
{
    /// <summary>
    /// The Pet these battles carry — Xích Lang's MVP Element and Passive
    /// (<c>ELEMENT_RULES.md</c> §6, <c>PASSIVE_RULES.md</c> §8).
    /// </summary>
    private static readonly BattleStateService.PetConfiguration Pet =
        new(Element.Hoa, new PassiveId("xich-lang"), PassiveThreshold: 5);

    // =======================================================================
    // Boss Passive — GAME_RULES.md §17 step 18a, BOSS_RULES.md §3, §6.2
    // =======================================================================

    [Fact]
    public void BossPassive_ShouldChargeOncePerPlayerMatch()
    {
        // BOSS_RULES.md §3.3 item 1 / §6.2: Hỏa Long's Passive charges "Every 5
        // Player Matches" — the same per-Match rate the Pet Passive uses
        // (PASSIVE_RULES.md §2 item 1), over the same Match total. GAME_STATE.md
        // §2.4.2 gives it its own counter.
        var service = new BattleStateService(new FixedRngSeedSource());
        var created = service.CreateBattle("boss-passive-charge", Pet, BossDefinitions.HoaLong);

        var pair = FindMatchProducingPair(created);
        var result = service.ExecuteSwap("boss-passive-charge", pair);

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
    public void BossPassive_ShouldReportTheBossSourceAndDisplayNameIdentity()
    {
        // SIGNALR_PROTOCOL.md §3.2.16 items 1–2 / BOSS_RULES.md §7: the shared events
        // carry source="boss", and sourceId is the DISPLAY-NAME BossState.BossId —
        // §6.4 fixes "Hỏa Long", never a slug.
        var service = new BattleStateService(new FixedRngSeedSource());
        var created = service.CreateBattle("boss-passive-identity", Pet, BossDefinitions.HoaLong);

        var pair = FindMatchProducingPair(created);
        var result = service.ExecuteSwap("boss-passive-identity", pair);

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
            Assert.Equal("Hỏa Long", sourceId);
            Assert.Equal("boss-hoa-long-rage", passiveId);
        }
    }

    [Fact]
    public void BossPassive_ShouldNotEmitMatchDrivenEventsForThuyMa()
    {
        // BOSS_RULES.md §6.2 is explicit: Thủy Ma's trigger is "Passive (always
        // active)" — an alternate trigger (PASSIVE_RULES.md §3), not a Match count —
        // so it "is never charged via PassiveTracker.Charge on Player Matches, and
        // emits no PassiveCharged/PassiveTriggered from match progress". Its stored
        // PassiveThreshold is therefore the Always-Active marker 0, not a threshold.
        var service = new BattleStateService(new FixedRngSeedSource());
        var created = service.CreateBattle("boss-passive-thuyma", Pet, BossDefinitions.ThuyMa);

        var pair = FindMatchProducingPair(created);
        var result = service.ExecuteSwap("boss-passive-thuyma", pair);

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

        // And its progress is untouched — still the Always-Active marker with no
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
    public void BossPassive_ShouldTriggerAtTheThresholdAndReset()
    {
        // PASSIVE_RULES.md §2 item 3 / §4 item 1: the Threshold is evaluated once
        // after the batch and the default reset settles progress at 0. This drives a
        // Threshold-1 Boss by using a Boss definition whose PassiveThreshold is 1 —
        // built from the documented §6.3/§6.4 shape rather than invented — so any
        // committed Swap (which always produces >= 1 Match, MATCH3_RULES.md §2.1.2
        // item 4) crosses it.
        var service = new BattleStateService(new FixedRngSeedSource());
        var boss = BossDefinitions.HoaLong with { PassiveThreshold = 1 };

        var created = service.CreateBattle("boss-passive-trigger", Pet, boss);

        var pair = FindMatchProducingPair(created);
        var result = service.ExecuteSwap("boss-passive-trigger", pair);

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
    public void BossPassive_ShouldApplyNoEffect()
    {
        // BOSS_RULES.md §3 item 3 / §6.2, TASK-022 §3.8: this task implements charging
        // and the trigger event only. Mộc Yêu's regeneration, Hỏa Long's Rage, and
        // Thủy Ma's healing reduction are NOT applied — so a trigger changes no HP and
        // no stat. This asserts the boundary: a triggered Mộc Yêu at Threshold 1 leaves
        // its own HP and every stat exactly as the damage left them.
        var service = new BattleStateService(new FixedRngSeedSource());
        var boss = BossDefinitions.MocYeu with { PassiveThreshold = 1 };

        var created = service.CreateBattle("boss-passive-no-effect", Pet, boss);

        var pair = FindMatchProducingPair(created);
        var result = service.ExecuteSwap("boss-passive-no-effect", pair);

        Assert.True(result!.Value.IsAccepted);
        Assert.Contains(
            result.Value.Events,
            e => e.Type == BattleEventType.PassiveTriggered
                && e.PassiveTriggered.Source == PassiveEventSource.Boss);

        var after = result.Value.State.BossState;

        // The Boss's stats are still the definition's, and its HP is still exactly
        // what the player's damage left — no regeneration was applied.
        Assert.Equal(boss.ATK, after.ATK);
        Assert.Equal(boss.DEF, after.DEF);
        Assert.Equal(boss.MaxHP, after.MaxHP);

        var playerDamage = result.Value.Events
            .First(e => e.Type == BattleEventType.DamageDealt
                && e.DamageDealt.Source == DamageParty.Player)
            .DamageDealt.Amount;

        Assert.Equal(boss.MaxHP - playerDamage, after.HP);
    }

    // =======================================================================
    // Boss Skill — GAME_RULES.md §17 step 18b, BOSS_RULES.md §4, §6.3
    // =======================================================================

    [Fact]
    public void BossSkill_ShouldFireWhenChargeMeetsTheRequirementAndCooldownIsZero()
    {
        // GAME_STATE.md §2.4.3 / BOSS_RULES.md §6.3: "The Skill fires when BOTH
        // conditions are met: SkillCharge >= SkillChargeRequirement AND SkillCooldown
        // = 0". This drives a ChargeRequirement of 1 so the Swap's Match total (>= 1)
        // satisfies it, with the definition's own SkillId and base damage.
        var service = new BattleStateService(new FixedRngSeedSource());
        var boss = BossDefinitions.HoaLong with { SkillChargeRequirement = 1 };

        var created = service.CreateBattle("boss-skill-fires", Pet, boss);

        Assert.Equal(0, created.BossState.SkillCooldown);

        var pair = FindMatchProducingPair(created);
        var result = service.ExecuteSwap("boss-skill-fires", pair);

        Assert.True(result!.Value.IsAccepted);

        var casts = result.Value.Events
            .Where(e => e.Type == BattleEventType.BossSkillCast)
            .ToArray();

        Assert.Single(casts);
        Assert.Equal("flame-burst", casts[0].BossSkillCast.SkillId);
        Assert.Equal("Hỏa Long", casts[0].BossSkillCast.SourceId);
    }

    [Fact]
    public void BossSkill_ShouldResetChargeAndSetTheCooldown()
    {
        // BOSS_RULES.md §6.3 / GAME_STATE.md §2.4.3: "After the Skill fires:
        // SkillCharge resets to 0, SkillCooldown resets to the Boss's cooldown value."
        var service = new BattleStateService(new FixedRngSeedSource());
        var boss = BossDefinitions.HoaLong with { SkillChargeRequirement = 1 };

        var created = service.CreateBattle("boss-skill-reset", Pet, boss);

        var pair = FindMatchProducingPair(created);
        var result = service.ExecuteSwap("boss-skill-reset", pair);

        Assert.True(result!.Value.IsAccepted);

        var after = result.Value.State.BossState;

        Assert.Equal(0, after.SkillCharge);
        Assert.Equal(boss.SkillCooldownTurns, after.SkillCooldown);
    }

    [Fact]
    public void BossSkill_ShouldUseBossAttackPlusSkillBaseDamage()
    {
        // COMBAT_RULES.md §3.4 / BOSS_RULES.md §6.3: the Skill's Step 1 Base Damage is
        // defined per Skill, and the term is ADDITIVE to the Boss's ATK. The
        // documented breakdown is derived here from the published formula and the
        // definition's values, never read back from the implementation.
        var service = new BattleStateService(new FixedRngSeedSource());
        var boss = BossDefinitions.HoaLong with { SkillChargeRequirement = 1 };

        var created = service.CreateBattle("boss-skill-damage", Pet, boss);

        var pair = FindMatchProducingPair(created);
        var result = service.ExecuteSwap("boss-skill-damage", pair);

        Assert.True(result!.Value.IsAccepted);

        var bossDamageCalculated = result.Value.Events
            .Where(e => e.Type == BattleEventType.DamageCalculated)
            .Last()
            .DamageCalculated;

        // Step 1: Boss.ATK + SkillBaseDamage, with no ATK-Gem pool for a Boss.
        Assert.Equal(boss.ATK + boss.SkillBaseDamage, bossDamageCalculated.Base);
        Assert.Equal(250, bossDamageCalculated.Base);

        // Step 2: Combo = 1 -> GAME_RULES.md §5's 1.00x row.
        Assert.Equal(1.00, bossDamageCalculated.ComboModifier);

        // Step 3: Boss.Element vs the active Pet's Element.
        Assert.Equal(
            ElementModifiers.Default.For(ElementMatchups.Resolve(boss.Element, Pet.Element)),
            bossDamageCalculated.ElementModifier);

        // Step 4: the MVP pass-through.
        Assert.Equal(1.00, bossDamageCalculated.OtherModifiers);

        // Step 5: the player's DEF (COMBAT_RULES.md §3.2's target DEF).
        var expectedDefense = (boss.ATK + boss.SkillBaseDamage)
            * bossDamageCalculated.ElementModifier
            * (DamagePipeline.DefenseMitigationConstant
                / (double)(DamagePipeline.DefenseMitigationConstant + created.PlayerState.DEF));

        Assert.Equal(expectedDefense, bossDamageCalculated.Defense, precision: 9);
    }

    [Fact]
    public void BossSkill_ShouldBeBlockedWhileTheCooldownIsActive()
    {
        // BOSS_RULES.md §6.3: "The Skill is blocked while CD > 0." The previous Swap
        // cast the Skill, so this one begins with the cooldown running: the Skill
        // condition fails on its second clause and the Boss falls back to its Basic
        // Attack — no BossSkillCast is emitted.
        var service = new BattleStateService(new FixedRngSeedSource());
        var (battleId, _) = BattleWithACoolingBoss(service, "boss-skill-cooldown-blocks");

        var afterCast = service.GetBattle(battleId)!;

        // MATCH3_RULES.md §2.1.4: the pair just committed is recorded as already
        // applied, so the next Swap must be a different one.
        var pair = FindMatchProducingPair(afterCast);

        var result = service.ExecuteSwap(battleId, pair);

        Assert.True(result!.Value.IsAccepted);

        // The cooldown decremented by one for this Turn and was still > 0 when the
        // Skill was evaluated, so the Skill did not fire.
        Assert.DoesNotContain(result.Value.Events, e => e.Type == BattleEventType.BossSkillCast);

        // The Basic Attack ran instead — the documented fallback (step 18c).
        Assert.Contains(
            result.Value.Events,
            e => e.Type == BattleEventType.DamageDealt
                && e.DamageDealt.Source == DamageParty.Boss);
    }

    [Fact]
    public void BossSkill_ShouldBeBlockedWhileTheChargeIsShort()
    {
        // The first clause of §2.4.3's condition. A ChargeRequirement no single Swap
        // can reach leaves the Skill unfired and the Basic Attack runs — and the
        // charge still accumulates for a later Swap.
        var service = new BattleStateService(new FixedRngSeedSource());
        var boss = BossDefinitions.HoaLong with { SkillChargeRequirement = 100 };

        var created = service.CreateBattle("boss-skill-charge-short", Pet, boss);

        var pair = FindMatchProducingPair(created);
        var result = service.ExecuteSwap("boss-skill-charge-short", pair);

        Assert.True(result!.Value.IsAccepted);

        var matches = result.Value.Resolution.TotalMatches;

        Assert.DoesNotContain(result.Value.Events, e => e.Type == BattleEventType.BossSkillCast);

        // GAME_STATE.md §2.4.3: "It increments per player match". The requirement was
        // not met, so the charge carries the batch total forward.
        Assert.Equal(matches, result.Value.State.BossState.SkillCharge);

        // No cooldown was set: the Skill never fired (BOSS_RULES.md §6.3).
        Assert.Equal(0, result.Value.State.BossState.SkillCooldown);
    }

    [Fact]
    public void BossSkillCharge_ShouldIncrementPerPlayerMatch()
    {
        // BOSS_RULES.md §6.3 / GAME_STATE.md §2.4.3: "Matches increment
        // BossState.SkillCharge". One Match is one increment (MATCH3_RULES.md §3
        // item 5), so the charge equals the resolution's own Match total on a Swap
        // whose requirement was not met.
        var service = new BattleStateService(new FixedRngSeedSource());
        var boss = BossDefinitions.MocYeu with { SkillChargeRequirement = 1000 };

        var created = service.CreateBattle("boss-skill-charge-rate", Pet, boss);

        var pair = FindMatchProducingPair(created);
        var result = service.ExecuteSwap("boss-skill-charge-rate", pair);

        Assert.True(result!.Value.IsAccepted);
        Assert.Equal(
            result.Value.Resolution.TotalMatches,
            result.Value.State.BossState.SkillCharge);
    }

    [Fact]
    public void BossSkill_ShouldNotResetThePassiveProgress()
    {
        // TASK-022 §3.7 / GAME_STATE.md §2.4.3: SkillCharge and PassiveProgress are
        // "independent counter[s]". Firing the Skill resets the charge and sets the
        // cooldown; the Passive's own progress is whatever its own step produced.
        var service = new BattleStateService(new FixedRngSeedSource());
        var boss = BossDefinitions.HoaLong with
        {
            SkillChargeRequirement = 1,
            PassiveThreshold = 100,
        };

        var created = service.CreateBattle("boss-skill-independent", Pet, boss);

        var pair = FindMatchProducingPair(created);
        var result = service.ExecuteSwap("boss-skill-independent", pair);

        Assert.True(result!.Value.IsAccepted);
        Assert.Contains(result.Value.Events, e => e.Type == BattleEventType.BossSkillCast);

        var matches = result.Value.Resolution.TotalMatches;

        // The Skill fired and reset its own counter...
        Assert.Equal(0, result.Value.State.BossState.SkillCharge);

        // ...while the Passive progress still carries the Matches its own step
        // accumulated, unchanged by the cast.
        Assert.Equal(matches, result.Value.State.BossState.PassiveProgress.Current);
    }

    [Fact]
    public void BossPassive_ShouldNotResetTheSkillCharge()
    {
        // The other direction of the same independence (§3.7): a Passive trigger must
        // not clear the Skill's charge.
        var service = new BattleStateService(new FixedRngSeedSource());
        var boss = BossDefinitions.HoaLong with
        {
            SkillChargeRequirement = 1000,
            PassiveThreshold = 1,
        };

        var created = service.CreateBattle("boss-passive-independent", Pet, boss);

        var pair = FindMatchProducingPair(created);
        var result = service.ExecuteSwap("boss-passive-independent", pair);

        Assert.True(result!.Value.IsAccepted);

        // The Passive triggered (Threshold 1) and reset only its own progress.
        Assert.Contains(
            result.Value.Events,
            e => e.Type == BattleEventType.PassiveTriggered
                && e.PassiveTriggered.Source == PassiveEventSource.Boss);

        Assert.Equal(0, result.Value.State.BossState.PassiveProgress.Current);

        // The Skill's charge is untouched by that trigger.
        Assert.Equal(
            result.Value.Resolution.TotalMatches,
            result.Value.State.BossState.SkillCharge);
    }

    // =======================================================================
    // Boss Basic Attack — GAME_RULES.md §17 step 18c, COMBAT_RULES.md §3.4
    // =======================================================================

    [Fact]
    public void BossBasicAttack_ShouldUseBossAttackAndComboOne()
    {
        // COMBAT_RULES.md §3.4: "Boss Basic Attack: Step 1 — Base Damage = Boss.ATK"
        // and "Step 2 — Combo Modifier = 1 (Boss attacks are not part of a Combo
        // chain)". The Skill's base damage is NOT part of this instance.
        var service = new BattleStateService(new FixedRngSeedSource());
        var boss = BossDefinitions.HoaLong with { SkillChargeRequirement = 1000 };

        var created = service.CreateBattle("boss-basic-damage", Pet, boss);

        var pair = FindMatchProducingPair(created);
        var result = service.ExecuteSwap("boss-basic-damage", pair);

        Assert.True(result!.Value.IsAccepted);

        var bossDamage = result.Value.Events
            .Where(e => e.Type == BattleEventType.DamageCalculated)
            .Last()
            .DamageCalculated;

        Assert.Equal(boss.ATK, bossDamage.Base);
        Assert.Equal(100, bossDamage.Base);
        Assert.Equal(1.00, bossDamage.ComboModifier);
    }

    [Fact]
    public void BossBasicAttack_ShouldDefendWithTheActivePetElementAndThePlayerDef()
    {
        // COMBAT_RULES.md §3.4 step 3 / §3.2: the defending Element is the ACTIVE
        // PET's ("the defender is the Pet, not the Player" — a Player has no Element),
        // and the mitigation input is the player's DEF. The matchup the assertion
        // reads is resolved from the two documented Elements.
        var service = new BattleStateService(new FixedRngSeedSource());
        var boss = BossDefinitions.HoaLong with { SkillChargeRequirement = 1000 };

        var created = service.CreateBattle("boss-basic-defender", Pet, boss);

        var pair = FindMatchProducingPair(created);
        var result = service.ExecuteSwap("boss-basic-defender", pair);

        Assert.True(result!.Value.IsAccepted);

        var bossDamage = result.Value.Events
            .Where(e => e.Type == BattleEventType.DamageCalculated)
            .Last()
            .DamageCalculated;

        var expectedMatchup = ElementMatchups.Resolve(boss.Element, Pet.Element);

        Assert.Equal(ElementModifiers.Default.For(expectedMatchup), bossDamage.ElementModifier);

        var expectedDefense = boss.ATK
            * ElementModifiers.Default.For(expectedMatchup)
            * (DamagePipeline.DefenseMitigationConstant
                / (double)(DamagePipeline.DefenseMitigationConstant + created.PlayerState.DEF));

        Assert.Equal(expectedDefense, bossDamage.Defense, precision: 9);
        Assert.Equal((int)Math.Truncate(expectedDefense), bossDamage.FinalDamage);
    }

    [Fact]
    public void BossBasicAttack_ShouldWriteThePlayersHp()
    {
        // COMBAT_RULES.md §3.4 step 6: "Final Damage applied to Player.HP". The
        // player's HP must fall by exactly the amount the Boss→Player instance
        // reports, in the same write-back (GAME_STATE.md §5.1).
        var service = new BattleStateService(new FixedRngSeedSource());
        var boss = BossDefinitions.HoaLong with { SkillChargeRequirement = 1000 };

        var created = service.CreateBattle("boss-basic-playerhp", Pet, boss);

        var pair = FindMatchProducingPair(created);
        var result = service.ExecuteSwap("boss-basic-playerhp", pair);

        Assert.True(result!.Value.IsAccepted);

        var bossDealt = result.Value.Events
            .First(e => e.Type == BattleEventType.DamageDealt
                && e.DamageDealt.Source == DamageParty.Boss)
            .DamageDealt;

        Assert.Equal(DamageParty.Boss, bossDealt.Source);
        Assert.Equal(DamageParty.Player, bossDealt.Target);
        Assert.Equal(created.PlayerState.HP - result.Value.State.PlayerState.HP, bossDealt.Amount);
        Assert.True(result.Value.State.PlayerState.HP < created.PlayerState.HP);
    }

    [Fact]
    public void BossResponse_ShouldBeEitherASkillOrABasicAttack_NotBoth()
    {
        // GAME_RULES.md §17 steps 18b–18c: the two are mutually exclusive — the Skill
        // is taken when eligible and the Basic Attack is the fallback. Exactly one
        // Boss→Player damage instance is produced per action, in either case.
        foreach (var chargeRequirement in new[] { 1, 1000 })
        {
            var service = new BattleStateService(new FixedRngSeedSource());
            var boss = BossDefinitions.HoaLong with { SkillChargeRequirement = chargeRequirement };
            var battleId = $"boss-mutual-{chargeRequirement}";

            var created = service.CreateBattle(battleId, Pet, boss);
            var pair = FindMatchProducingPair(created);
            var result = service.ExecuteSwap(battleId, pair);

            Assert.True(result!.Value.IsAccepted);

            var casts = result.Value.Events.Count(e => e.Type == BattleEventType.BossSkillCast);
            var bossInstances = result.Value.Events.Count(e => e.Type == BattleEventType.DamageDealt
                && e.DamageDealt.Source == DamageParty.Boss);

            // Exactly one Boss damage instance either way, and a cast only when the
            // Skill was the one taken.
            Assert.Equal(1, bossInstances);
            Assert.Equal(chargeRequirement == 1 ? 1 : 0, casts);

            // And a cast, when present, directly precedes its own damage instance —
            // SIGNALR_PROTOCOL.md §3.2.18 item 3: "Effect details are carried by
            // subsequent damage events".
            if (casts == 1)
            {
                var events = result.Value.Events;
                var castIndex = events
                    .Select((e, i) => (e, i))
                    .First(t => t.e.Type == BattleEventType.BossSkillCast)
                    .i;

                Assert.Equal(BattleEventType.DamageCalculated, events[castIndex + 1].Type);

                // The Skill's instance is the Boss's — the base damage includes the
                // Skill's base value (§3.4), which the Basic Attack's does not.
                Assert.Equal(
                    boss.ATK + boss.SkillBaseDamage,
                    events[castIndex + 1].DamageCalculated.Base);
            }
        }
    }

    [Fact]
    public void BossResponse_ShouldRunAfterThePlayersDamage()
    {
        // GAME_RULES.md §17 / BOSS_RULES.md §3.3 item 1: the Boss's damage instance
        // follows the player's, because the Passive "fires once per player action,
        // after all player damage is resolved".
        var service = new BattleStateService(new FixedRngSeedSource());
        var boss = BossDefinitions.HoaLong with { SkillChargeRequirement = 1 };

        var created = service.CreateBattle("boss-response-order", Pet, boss);
        var pair = FindMatchProducingPair(created);
        var result = service.ExecuteSwap("boss-response-order", pair);

        Assert.True(result!.Value.IsAccepted);

        var events = result.Value.Events;

        var playerDamageIndex = events
            .Select((e, i) => (e, i))
            .First(t => t.e.Type == BattleEventType.DamageDealt
                && t.e.DamageDealt.Source == DamageParty.Player)
            .i;

        var bossDamageIndex = events
            .Select((e, i) => (e, i))
            .First(t => t.e.Type == BattleEventType.DamageDealt
                && t.e.DamageDealt.Source == DamageParty.Boss)
            .i;

        Assert.True(
            playerDamageIndex < bossDamageIndex,
            "the player's damage instance must precede the Boss Response (BOSS_RULES.md §3.3 item 1)");

        // And the Boss Passive's events sit between them — §3.3 item 1 places the
        // Passive after player damage and §3.3 item 2 places it before the Skill.
        var bossPassiveIndex = events
            .Select((e, i) => (e, i))
            .First(t => t.e.Type == BattleEventType.PassiveCharged
                && t.e.PassiveCharged.Source == PassiveEventSource.Boss)
            .i;

        Assert.True(playerDamageIndex < bossPassiveIndex);
        Assert.True(bossPassiveIndex < bossDamageIndex);

        // A Skill cast sits between the Passive and its own damage instance
        // (SIGNALR_PROTOCOL.md §3.2.18 item 3).
        var castIndex = events
            .Select((e, i) => (e, i))
            .First(t => t.e.Type == BattleEventType.BossSkillCast)
            .i;

        Assert.True(bossPassiveIndex < castIndex);
        Assert.True(castIndex < bossDamageIndex);
    }

    // =======================================================================
    // Turn and cooldown lifecycle — MATCH3_RULES.md §8.1, BOSS_RULES.md §6.3
    // =======================================================================

    [Fact]
    public void CommittedSwap_ShouldAdvanceTurnExactlyOnce()
    {
        // MATCH3_RULES.md §8.1 item 1 / §8.3: one committed Swap begins exactly one
        // Turn. SwapExecutor already performs the increment in its own write-back
        // (GAME_STATE.md §5.1), and BattleStateService must NOT increment again — so
        // the stored Turn advances by exactly 1, never 2.
        var service = new BattleStateService(new FixedRngSeedSource());
        var created = service.CreateBattle("boss-turn-once", Pet, BossDefinitions.HoaLong);

        var pair = FindMatchProducingPair(created);
        var result = service.ExecuteSwap("boss-turn-once", pair);

        Assert.True(result!.Value.IsAccepted);

        Assert.Equal(BattleState.InitialTurn + 1, result.Value.State.Turn);
        Assert.Equal(created.Turn + 1, result.Value.State.Turn);
        Assert.Equal(created.Sequence + 1, result.Value.State.Sequence);

        // The registry holds the same single write-back (GAME_STATE.md §5.1).
        Assert.Equal(result.Value.State.Turn, service.GetBattle("boss-turn-once")!.Turn);
    }

    [Fact]
    public void CommittedSwaps_ShouldAdvanceTurnOnceEach()
    {
        // Two committed Swaps leave Turn at 2 — one per Swap, never more. This is the
        // regression guard for a second increment being added to the resolution.
        var service = new BattleStateService(new FixedRngSeedSource());
        var created = service.CreateBattle("boss-turn-twice", Pet, BossDefinitions.HoaLong);

        var firstPair = FindMatchProducingPair(created);
        var first = service.ExecuteSwap("boss-turn-twice", firstPair);
        Assert.True(first!.Value.IsAccepted);

        var afterFirst = service.GetBattle("boss-turn-twice")!;
        var secondPair = FindMatchProducingPair(afterFirst);
        var second = service.ExecuteSwap("boss-turn-twice", secondPair);
        Assert.True(second!.Value.IsAccepted);

        Assert.Equal(BattleState.InitialTurn + 2, second.Value.State.Turn);
        Assert.Equal(BattleState.InitialSequence + 2, second.Value.State.Sequence);
    }

    [Fact]
    public void RejectedSwap_ShouldNotAdvanceTurnOrCoolTheBossDown()
    {
        // MATCH3_RULES.md §2.1.5: a rejected action writes nothing. It is not a Turn,
        // so the cooldown does not decrement either — the decrement is tied to the
        // Turn a committed Swap begins (BOSS_RULES.md §6.3).
        var service = new BattleStateService(new FixedRngSeedSource());
        var (battleId, _) = BattleWithACoolingBoss(service, "boss-rejected-no-turn");

        var before = service.GetBattle(battleId)!;
        var turnBefore = before.Turn;
        var cooldownBefore = before.BossState.SkillCooldown;

        Assert.True(cooldownBefore > 0);

        // An out-of-range request is rejected by the validator's index check.
        var result = service.ExecuteSwap(battleId, new SwapRequest(99, 100));

        Assert.True(result!.Value.IsRejected);
        Assert.Empty(result.Value.Events);

        var stored = service.GetBattle(battleId)!;

        Assert.Equal(turnBefore, stored.Turn);
        Assert.Equal(cooldownBefore, stored.BossState.SkillCooldown);
    }

    [Fact]
    public void SkillCooldown_ShouldDecrementOncePerCommittedSwapAfterTheTurn()
    {
        // BOSS_RULES.md §6.3: the cooldown "Decrements by 1 at each Turn increment",
        // and MATCH3_RULES.md §8.1 makes one committed Swap begin exactly one Turn. So
        // one Swap decrements it by exactly 1 — not by 0, and not by 2.
        var service = new BattleStateService(new FixedRngSeedSource());
        var (battleId, _) = BattleWithACoolingBoss(service, "boss-cooldown-decrement");

        var before = service.GetBattle(battleId)!;
        var pair = FindMatchProducingPair(before);
        var result = service.ExecuteSwap(battleId, pair);

        Assert.True(result!.Value.IsAccepted);

        Assert.Equal(before.BossState.SkillCooldown - 1, result.Value.State.BossState.SkillCooldown);
        Assert.Equal(before.Turn + 1, result.Value.State.Turn);
    }

    [Fact]
    public void SkillCooldown_ShouldTickDownOnePerSwapAndLetTheSkillFireAgain()
    {
        // The documented lifecycle end to end (BOSS_RULES.md §6.3, GAME_STATE.md
        // §2.4.3): a cast sets the cooldown to SkillCooldownTurns, each subsequent
        // committed Swap decrements it by exactly 1 *before* eligibility is
        // evaluated, and the Skill fires again on the Swap that brings it to 0.
        //
        // That ordering is the rule, not an accident: §6.3 says the cooldown
        // "Decrements by 1 at each Turn increment" and blocks the Skill "while
        // CD > 0" — so a cooldown of N blocks N−1 Turns and the Nth Turn is the one
        // it is eligible on again.
        var service = new BattleStateService(new FixedRngSeedSource());
        var boss = BossDefinitions.HoaLong with { SkillChargeRequirement = 1 };

        var battleId = "boss-cooldown-lifecycle";

        var created = service.CreateBattle(battleId, Pet, boss);
        var previousPair = FindMatchProducingPair(created);
        var first = service.ExecuteSwap(battleId, previousPair);

        Assert.True(first!.Value.IsAccepted);
        Assert.Contains(first.Value.Events, e => e.Type == BattleEventType.BossSkillCast);
        Assert.Equal(boss.SkillCooldownTurns, first.Value.State.BossState.SkillCooldown);

        // Each blocked Swap decrements by exactly one and fires nothing.
        for (var blocked = 1; blocked < boss.SkillCooldownTurns; blocked++)
        {
            var current = service.GetBattle(battleId)!;

            Assert.Equal(boss.SkillCooldownTurns - (blocked - 1), current.BossState.SkillCooldown);

            var nextPair = FindMatchProducingPair(current);
            var result = service.ExecuteSwap(battleId, nextPair);

            Assert.True(result!.Value.IsAccepted);

            Assert.DoesNotContain(
                result.Value.Events,
                e => e.Type == BattleEventType.BossSkillCast);

            Assert.Equal(
                boss.SkillCooldownTurns - blocked,
                result.Value.State.BossState.SkillCooldown);

            previousPair = nextPair;
        }

        // The cooldown is now 1. The next committed Swap decrements it to 0 and the
        // Skill fires on that same Turn — the first Turn it is eligible.
        var eligibleState = service.GetBattle(battleId)!;
        Assert.Equal(1, eligibleState.BossState.SkillCooldown);

        var finalPair = FindMatchProducingPair(eligibleState);
        var recast = service.ExecuteSwap(battleId, finalPair);

        Assert.True(recast!.Value.IsAccepted);
        Assert.Contains(recast.Value.Events, e => e.Type == BattleEventType.BossSkillCast);
        Assert.Equal(boss.SkillCooldownTurns, recast.Value.State.BossState.SkillCooldown);
    }

    [Fact]
    public void SkillCooldown_ShouldNotDecrementBelowZero()
    {
        // §2.4.3 blocks the Skill "while > 0", so 0 is the floor — the decrement
        // cannot drive the cooldown negative.
        //
        // The branch is read from the events rather than assumed, because whether the
        // Skill fires depends on the board's Match total against the Boss's charge
        // requirement (here 5): a cast SETS the cooldown to SkillCooldownTurns, and
        // only an un-fired Skill leaves the decrement's result. The floor rule holds
        // either way, so both are asserted.
        var service = new BattleStateService(new FixedRngSeedSource());
        var created = service.CreateBattle("boss-cooldown-floor", Pet, BossDefinitions.HoaLong);

        Assert.Equal(0, created.BossState.SkillCooldown);

        var pair = FindMatchProducingPair(created);
        var result = service.ExecuteSwap("boss-cooldown-floor", pair);

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
    /// Commits one Swap on a fresh battle whose Boss casts its Skill immediately, and
    /// returns the battle id. The cooldown is reached through the real path — a Skill
    /// that fires and sets <c>SkillCooldownTurns</c> (<c>BOSS_RULES.md</c> §6.3) — so
    /// no test-only state write is introduced and the scenario starts from a state the
    /// game genuinely produces.
    /// </summary>
    private static (string BattleId, BossDefinition Boss) BattleWithACoolingBoss(
        BattleStateService service,
        string battleId)
    {
        var boss = BossDefinitions.HoaLong with { SkillChargeRequirement = 1 };

        var created = service.CreateBattle(battleId, Pet, boss);
        var pair = FindMatchProducingPair(created);
        var result = service.ExecuteSwap(battleId, pair);

        Assert.True(result!.Value.IsAccepted);
        Assert.Contains(result.Value.Events, e => e.Type == BattleEventType.BossSkillCast);
        Assert.True(service.GetBattle(battleId)!.BossState.SkillCooldown > 0);

        return (battleId, boss);
    }

    /// <summary>
    /// A match-producing adjacent pair that is legal on the battle's current board and
    /// is not the pair already recorded as committed (<c>MATCH3_RULES.md</c> §2.1.4).
    ///
    /// It reads the committed pair from the authoritative state rather than taking a
    /// caller-supplied exclusion, so a scenario cannot accidentally hand the executor
    /// a replay and mistake the staleness rejection for a board problem.
    /// </summary>
    private static SwapRequest NextMatchProducingPair(BattleState state)
    {
        var committed = state.LastCommittedSwapPair;

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
}
