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
/// Boss Skill secondary effect tests — <c>GAME_RULES.md</c> §17 step 18b's
/// "apply non-damage effects", whose rule is <c>BOSS_RULES.md</c> §6.3.1.
///
/// <code>
/// Rule (BOSS_RULES.md §6.3.1 items 1–3)
///  ↓
/// Scenario (Given the documented Boss and a committed Swap, When the Skill
///           fires, Then the documented effect is applied)
///  ↓
/// Test
/// </code>
///
/// <b>Every expected value traces to §6.3.1, never to the implementation.</b>
/// Burn's 50 × 2 Turns, Drain Power's −20 flat with its floor at 0, and Root's
/// −30% ATK for 2 Turns are read from the document and asserted against the
/// resulting authoritative state and the declared configuration.
///
/// <b>Scope boundary.</b> Root's <i>instance</i> is asserted here. Root's
/// <i>ATK consumption</i> is deliberately not: no document states that a
/// <c>BuffDebuff</c> magnitude modifies a stat, and <c>ADR-017</c> records that
/// the state model has "no attack-consumption path at all". That contract is
/// owned by TASK-119, and asserting a guessed behavior here would encode an
/// invented rule as expected behavior (<c>AGENTS.md</c> §15).
/// </summary>
public class BossSkillSecondaryEffectTests
{
    /// <summary>
    /// The Pet these battles carry (<c>GAME_STATE.md</c> §2.3), Xích Lang's MVP
    /// Element and Passive.
    /// </summary>
    private static readonly BattleStateService.PetConfiguration Pet =
        new(new PetId("pet_instance_1"), Element.Hoa, new PassiveId("xich-lang"), PassiveThreshold: 5);

    /// <summary>The owning Player of these battles (<c>GAME_STATE.md</c> §2.8).</summary>
    private static readonly PlayerId Owner = new("player_boss_secondary_owner");

    // =======================================================================
    // Flame Burst → Burn — BOSS_RULES.md §6.3.1 item 1
    // =======================================================================

    [Fact]
    public async Task FlameBurst_ShouldApplyExactlyOneBossSourcedBurn()
    {
        // §6.3.1 item 1: "Applies Burn status effect to the active Pet", fixed 50
        // damage per tick, 2 Turns, and the Boss is the source.
        var harness = Harness.Create();
        var service = harness.Service;
        var battleId = "boss-burn-applied";

        var result = await FireTheSkillAsync(harness, battleId, BossDefinitions.HoaLong);

        var burns = BurnsOn(result.State.PetState);

        Assert.Single(burns);
        Assert.Equal("Burn", burns[0].Id);
        Assert.Equal(StatusEffectType.DoT, burns[0].Type);
        Assert.Equal(StatusEffectSource.Boss, burns[0].Source);
        Assert.Equal(50, burns[0].Magnitude);

        // §6.3.1 item 1 applies Burn at step 18b of Turn N and consumes its first
        // tick at Turn N's OWN step 19a, so the committed RemainingTurns is the
        // applied 2 minus that Turn's documented single decrement
        // (COMBAT_RULES.md §5.3 DR2/DR3, GAME_STATE.md §5.1.1 item 2). The applied
        // duration of 2 is asserted directly on the declaration instead — see
        // EachSkill_ShouldDeclareItsOwnDocumentedEffect.
        Assert.Equal(1, burns[0].RemainingTurns);
    }

    [Fact]
    public async Task FlameBurst_ShouldTickBurnAtTheResolvingTurnsStep19a()
    {
        // §6.3.1 item 1's timing: applied "during Turn N Boss Response (step 18b)",
        // Burn tick #1 occurs "at Turn N step 19a (End Turn)". Applying and ticking
        // in the SAME resolution is what this asserts — the tick is a Boss→Player
        // damage instance of fixed magnitude 50.
        var harness = Harness.Create();
        var service = harness.Service;
        var battleId = "boss-burn-same-turn-tick";

        var result = await FireTheSkillAsync(harness, battleId, BossDefinitions.HoaLong);

        // The step 19a tick reports a damage instance whose Step 1 base is the
        // Burn magnitude — not the Boss's ATK and not the Skill's base damage.
        var ticks = result.Events
            .Where(e => e.Type == BattleEventType.DamageCalculated
                && e.DamageCalculated.Base == 50)
            .ToArray();

        Assert.NotEmpty(ticks);

        // And the Pet actually lost HP to it: the Skill's own instance plus the
        // tick both reduced PetState.HP within this one resolution.
        Assert.True(result.State.PetState.HP < 1000);

        // §6.3.1 item 1: "Burn tick #2 (50 damage) occurs at Turn N+1 step 19a,
        // after which the Burn expires before Turn N+2." After the resolving
        // Turn's step 19a the instance has consumed one Turn, so exactly 1 remains.
        var burns = BurnsOn(result.State.PetState);
        Assert.Single(burns);
        Assert.Equal(1, burns[0].RemainingTurns);
    }

    [Fact]
    public async Task FlameBurst_ShouldRefreshRatherThanDuplicateAnActiveBurn()
    {
        // COMBAT_RULES.md §5.2 item 2 / GAME_STATE.md §2.3.1 item 6: a re-application
        // "refresh[es] duration, do[es] not stack magnitude", and "the array holds at
        // most one element per Id". §5.3 DR3 makes Apply and Refresh the same "set
        // remaining = duration" operation.
        var harness = Harness.Create();
        var service = harness.Service;
        var battleId = "boss-burn-refresh";

        // Two consecutive Swaps, each firing Flame Burst (ChargeRequirement 1, and
        // CooldownTurns 0 so the second Swap is also eligible). The second
        // application must refresh the one instance, not add a second.
        var boss = BossDefinitions.HoaLong with
        {
            SkillDefinition = BossDefinitions.HoaLong.SkillDefinition
                with { ChargeRequirement = 1, CooldownTurns = 0 },
        };

        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var first = (await service.ExecuteSwapAsync(battleId, FindMatchProducingPair(created))) ?? throw new InvalidOperationException("the first Swap must resolve");
        Assert.True(first.IsAccepted);
        Assert.Contains(first.Events, e => e.Type == BattleEventType.BossSkillCast);

        var afterFirst = (await service.GetBattleAsync(battleId))!;
        Assert.Single(BurnsOn(afterFirst.PetState));

        var second = (await service.ExecuteSwapAsync(battleId, FindMatchProducingPair(afterFirst))) ?? throw new InvalidOperationException("the second Swap must resolve");
        Assert.True(second.IsAccepted);
        Assert.Contains(second.Events, e => e.Type == BattleEventType.BossSkillCast);

        var afterSecond = (await service.GetBattleAsync(battleId))!;
        var burns = BurnsOn(afterSecond.PetState);

        // One instance — never two, and never a stacked magnitude. Because the
        // refresh re-SETS RemainingTurns to the applied 2 and that same Turn's step
        // 19a then consumes its single decrement, the committed value is exactly 1
        // (COMBAT_RULES.md §5.3 DR2/DR3/DR4, §5.3.3's "refreshed in Turn N+1"
        // worked example). A stacking implementation would read 2 here and a
        // magnitude-summing one would read 100 — so this pins both forbidden
        // behaviors, not merely "some value in range".
        Assert.Single(burns);
        Assert.Equal(50, burns[0].Magnitude);
        Assert.Equal(1, burns[0].RemainingTurns);
    }

    [Fact]
    public async Task FlameBurst_ShouldEmitNoBurnSpecificEvent()
    {
        // GAME_RULES.md §16's canonical event list is closed and BOSS_RULES.md §7
        // enumerates step 18's events. The effect is authoritative state mutation,
        // not a transport event — so no event names it.
        var harness = Harness.Create();
        var service = harness.Service;
        var battleId = "boss-burn-no-event";

        var result = await FireTheSkillAsync(harness, battleId, BossDefinitions.HoaLong);

        var types = result.Events.Select(e => e.Type.ToString()).ToArray();

        foreach (var forbidden in new[]
                 {
                     "BurnApplied", "BurnTicked", "StatusEffectApplied",
                     "StatusTicked", "BossSkillEffect", "DebuffApplied",
                     "RootApplied", "PowerDrained",
                 })
        {
            Assert.DoesNotContain(forbidden, types);
        }

        // Step 18b's emitted set is unchanged from TASK-022's.
        var bossEvents = result.Events
            .Where(e => e.Type == BattleEventType.BossSkillCast)
            .ToArray();
        Assert.Single(bossEvents);
    }

    [Fact]
    public async Task FlameBurst_BurnTick_ShouldConsumeFromTheSingleRngStream()
    {
        // COMBAT_RULES.md §3.3 item 4 makes every damage instance Crit-eligible, and
        // TDD.md §6 / ADR-009 fix ONE server-authoritative stream. The Burn tick
        // draws from it like any other instance, so the retained RngState advances
        // across the resolution.
        var harness = Harness.Create();
        var service = harness.Service;
        var battleId = "boss-burn-rng";

        var created = await service.CreateBattleAsync(battleId, Owner, Pet, BossDefinitions.HoaLong);
        var beforeResolution = created.RngState;

        Assert.True(await service.ExecuteSwapAsync(battleId, FindMatchProducer(created)) is not null);

        var after = (await service.GetBattleAsync(battleId))!;

        Assert.NotEqual(beforeResolution, after.RngState);
    }

    // =======================================================================
    // Drain Power → instant Power reduction — BOSS_RULES.md §6.3.1 item 2
    // =======================================================================

    [Theory]
    [InlineData(30, 10)]
    [InlineData(21, 1)]
    [InlineData(20, 0)]
    [InlineData(10, 0)]
    [InlineData(0, 0)]
    public async Task DrainPower_ShouldApplyTheFlattenedReduction(int before, int expected)
    {
        // §6.3.1 item 2: "Instantly subtracts 20 flat Power from the active Pet
        // (PetState.Power = max(0, PetState.Power - 20))". The floor at 0 is that
        // item's own formula; GAME_RULES.md §12 fixes the range 0–100.
        var harness = Harness.Create();
        var service = harness.Service;
        var battleId = $"boss-drain-{before}";

        var powerAfter = await DrainPowerScenarioAsync(harness, battleId, before);

        Assert.Equal(expected, powerAfter);
    }

    [Fact]
    public async Task DrainPower_ShouldCreateNoStatusEffectInstance()
    {
        // GAME_STATE.md §2.3.1 item 9: "Instant, non-duration effects create no
        // instance. Drain Power is an immediate PetState.Power mutation, not a Status
        // Effect ... so it never produces an element here."
        var harness = Harness.Create();
        var service = harness.Service;
        var battleId = "boss-drain-no-instance";

        var created = await service.CreateBattleAsync(
            battleId, Owner, Pet, ThuyMaFiringImmediately());

        var result = (await service.ExecuteSwapAsync(battleId, FindMatchProducer(created))) ?? throw new InvalidOperationException("the Swap must resolve");
        Assert.True(result.IsAccepted);
        Assert.Contains(result.Events, e => e.Type == BattleEventType.BossSkillCast);

        var effects = result.State.PetState.ActiveStatusEffects;

        Assert.Empty(effects);
        Assert.DoesNotContain(effects, e => e.Id == "Power");
        Assert.DoesNotContain(effects, e => e.Id == "DrainPower");
    }

    [Fact]
    public async Task DrainPower_ShouldApplyToTheGivenBossOnly()
    {
        // §6.3's Secondary Effect column gives Drain Power to Thủy Ma alone; §6.3.1
        // item 2's reduction belongs to that Skill's resolution.
        var harness = Harness.Create();
        var service = harness.Service;
        var battleId = "boss-drain-binding";

        // Thủy Ma declares PowerDrain and nothing else.
        var effect = BossDefinitions.ThuyMa.SkillDefinition.SecondaryEffect;

        Assert.NotNull(effect);
        Assert.Equal(BossSkillSecondaryEffectKind.PowerDrain, effect.Value.Kind);
        Assert.Equal(20, effect.Value.Magnitude);
        Assert.Null(effect.Value.StatusEffectId);
        Assert.Null(effect.Value.DurationTurns);

        // Hỏa Long and Mộc Yêu declare no PowerDrain.
        Assert.NotEqual(
            BossSkillSecondaryEffectKind.PowerDrain,
            BossDefinitions.HoaLong.SkillDefinition.SecondaryEffect!.Value.Kind);
        Assert.NotEqual(
            BossSkillSecondaryEffectKind.PowerDrain,
            BossDefinitions.MocYeu.SkillDefinition.SecondaryEffect!.Value.Kind);
    }

    // =======================================================================
    // Root → ATK debuff instance — BOSS_RULES.md §6.3.1 item 3
    // =======================================================================

    [Fact]
    public async Task Root_ShouldApplyExactlyOneBossSourcedAtkDebuff()
    {
        // §6.3.1 item 3: "-30% Pet ATK debuff", "Percentage-based ATK reduction
        // (-30% active Pet ATK)", 2 Turns, applied as a Turn-based Buff/Debuff
        // (COMBAT_RULES.md §5.1). GAME_STATE.md §2.3.1's schema names "ATK" as the
        // TargetStat of a BuffDebuff.
        var harness = Harness.Create();
        var service = harness.Service;
        var battleId = "boss-root-applied";

        var result = await FireTheSkillAsync(harness, battleId, BossDefinitions.MocYeu);

        var roots = RootsOn(result.State.PetState);

        Assert.Single(roots);
        Assert.Equal("Root", roots[0].Id);
        Assert.Equal(StatusEffectType.BuffDebuff, roots[0].Type);
        Assert.Equal(StatusEffectSource.Boss, roots[0].Source);
        Assert.Equal("ATK", roots[0].TargetStat);
        Assert.Equal(30, roots[0].Magnitude);

        // §6.3.1 item 3's 2-Turn duration is the APPLIED value; the committed value
        // is one less because the resolving Turn consumes its single decrement at
        // step 19a (COMBAT_RULES.md §5.3.2/§5.3 DR2, GAME_STATE.md §5.1.1 item 2).
        // The applied 2 is asserted on the declaration in
        // EachSkill_ShouldDeclareItsOwnDocumentedEffect.
        Assert.Equal(1, roots[0].RemainingTurns);
    }

    [Fact]
    public async Task Root_ShouldConsumeOneTurnOfDurationAtStep19a()
    {
        // COMBAT_RULES.md §5.3.2 makes Root a Turn-based Buff/Debuff whose
        // one-Turn-of-duration consumption is §5.3's; GAME_STATE.md §5.1.1 item 2
        // puts that single decrement at step 19a of the resolving Turn.
        var harness = Harness.Create();
        var service = harness.Service;
        var battleId = "boss-root-consumes";

        var result = await FireTheSkillAsync(harness, battleId, BossDefinitions.MocYeu);

        var roots = RootsOn(result.State.PetState);

        Assert.Single(roots);
        Assert.Equal(1, roots[0].RemainingTurns);
    }

    [Fact]
    public async Task Root_ShouldRefreshRatherThanDuplicateOnReapplication()
    {
        // COMBAT_RULES.md §5.3 DR3/DR4 and GAME_STATE.md §2.3.1 item 6: re-applying
        // an active effect re-sets RemainingTurns and appends no second element.
        // §5.3.3's worked example ("duration = 2, refreshed in Turn N+1") is the
        // documented shape.
        var harness = Harness.Create();
        var service = harness.Service;
        var battleId = "boss-root-refresh";

        var boss = BossDefinitions.MocYeu with
        {
            SkillDefinition = BossDefinitions.MocYeu.SkillDefinition
                with { ChargeRequirement = 1, CooldownTurns = 0 },
        };

        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var first = (await service.ExecuteSwapAsync(battleId, FindMatchProducer(created))) ?? throw new InvalidOperationException("the first Swap must resolve");
        Assert.True(first.IsAccepted);
        Assert.Contains(first.Events, e => e.Type == BattleEventType.BossSkillCast);

        var afterFirst = (await service.GetBattleAsync(battleId))!;
        Assert.Single(RootsOn(afterFirst.PetState));

        var second = (await service.ExecuteSwapAsync(battleId, FindMatchProducer(afterFirst))) ?? throw new InvalidOperationException("the second Swap must resolve");
        Assert.True(second.IsAccepted);
        Assert.Contains(second.Events, e => e.Type == BattleEventType.BossSkillCast);

        var afterSecond = (await service.GetBattleAsync(battleId))!;
        var roots = RootsOn(afterSecond.PetState);

        // One element with the same identity — refreshed, never duplicated and never
        // summed. Exactly 1 remains: the refresh re-SETS RemainingTurns to the
        // applied 2 and that Turn's step 19a consumes its single decrement
        // (COMBAT_RULES.md §5.3 DR2/DR3/DR4).
        Assert.Single(roots);
        Assert.Equal(30, roots[0].Magnitude);
        Assert.Equal(1, roots[0].RemainingTurns);
    }

    [Fact]
    public async Task Root_ShouldEmitNoRootSpecificEvent()
    {
        // GAME_RULES.md §16's list is closed; StatusEffects[] is not a wire member
        // (GAME_STATE.md §2.3.1's wire note).
        var harness = Harness.Create();
        var service = harness.Service;
        var battleId = "boss-root-no-event";

        var result = await FireTheSkillAsync(harness, battleId, BossDefinitions.MocYeu);

        var types = result.Events.Select(e => e.Type.ToString()).ToArray();

        foreach (var forbidden in new[] { "RootApplied", "DebuffApplied", "StatusEffectApplied" })
        {
            Assert.DoesNotContain(forbidden, types);
        }

        Assert.Single(result.Events.Where(e => e.Type == BattleEventType.BossSkillCast));
    }

    // =======================================================================
    // Per-Boss binding — BOSS_RULES.md §6.3's Secondary Effect column
    // =======================================================================

    [Fact]
    public void EachSkill_ShouldDeclareItsOwnDocumentedEffect()
    {
        // §6.3/§6.3.1, transcribed per Boss. The declaration is on the Boss's own
        // definition, so no resolution site dispatches on the SkillId string.
        var flameBurst = BossDefinitions.HoaLong.SkillDefinition.SecondaryEffect!.Value;
        Assert.Equal(BossSkillSecondaryEffectKind.Burn, flameBurst.Kind);
        Assert.Equal("Burn", flameBurst.StatusEffectId);
        Assert.Equal(StatusEffectType.DoT, flameBurst.StatusEffectType);
        Assert.Equal(50, flameBurst.Magnitude);
        Assert.Equal(2, flameBurst.DurationTurns);
        Assert.Null(flameBurst.TargetStat);

        var drainPower = BossDefinitions.ThuyMa.SkillDefinition.SecondaryEffect!.Value;
        Assert.Equal(BossSkillSecondaryEffectKind.PowerDrain, drainPower.Kind);
        Assert.Equal(20, drainPower.Magnitude);
        Assert.Null(drainPower.StatusEffectId);
        Assert.Null(drainPower.StatusEffectType);
        Assert.Null(drainPower.DurationTurns);

        var root = BossDefinitions.MocYeu.SkillDefinition.SecondaryEffect!.Value;
        Assert.Equal(BossSkillSecondaryEffectKind.AtkDebuff, root.Kind);
        Assert.Equal("Root", root.StatusEffectId);
        Assert.Equal(StatusEffectType.BuffDebuff, root.StatusEffectType);
        Assert.Equal("ATK", root.TargetStat);
        Assert.Equal(30, root.Magnitude);
        Assert.Equal(2, root.DurationTurns);
    }

    [Fact]
    public async Task HoaLongSkill_ShouldApplyBurnOnly()
    {
        // §6.3's table gives each Skill exactly one secondary effect, so a Hỏa Long
        // resolution applies no Root and drains no Power.
        var harness = Harness.Create();
        var service = harness.Service;
        var battleId = "boss-binding-hoa-long";

        var powerBefore = 42;
        var result = await FireTheSkillWithPowerAsync(
            harness, battleId, BossDefinitions.HoaLong, powerBefore);

        var pet = result.State.PetState;

        Assert.Single(BurnsOn(pet));
        Assert.Empty(RootsOn(pet));
        Assert.Equal(
            Math.Max(0, powerBefore - 0),
            pet.Power);
    }

    [Fact]
    public async Task MocYeuSkill_ShouldApplyRootOnly()
    {
        // The mirror of the above: a Mộc Yêu resolution applies Root, not Burn, and
        // drains no Power.
        var harness = Harness.Create();
        var service = harness.Service;
        var battleId = "boss-binding-moc-yeu";

        var powerBefore = 42;
        var result = await FireTheSkillWithPowerAsync(
            harness, battleId, BossDefinitions.MocYeu, powerBefore);

        var pet = result.State.PetState;

        Assert.Single(RootsOn(pet));
        Assert.Empty(BurnsOn(pet));
        Assert.Equal(powerBefore, pet.Power);
    }

    [Fact]
    public async Task ThuyMaSkill_ShouldDrainPowerOnly()
    {
        // The mirror for Thủy Ma: Power is reduced by exactly 20 and neither a Burn
        // nor a Root instance appears.
        var harness = Harness.Create();
        var service = harness.Service;
        var battleId = "boss-binding-thuy-ma";

        var result = await FireTheSkillWithPowerAsync(
            harness, battleId, BossDefinitions.ThuyMa, powerBefore: 42);

        var pet = result.State.PetState;

        Assert.Equal(22, pet.Power);
        Assert.Empty(BurnsOn(pet));
        Assert.Empty(RootsOn(pet));
    }

    [Fact]
    public async Task BasicAttackFallback_ShouldApplyNoSecondaryEffect()
    {
        // §17 step 18c / §6.3: the Basic Attack is the fallback when the Skill does
        // not fire. It has no secondary effect, so a Boss whose charge is never
        // reached applies none of the three.
        foreach (var boss in BossDefinitions.All)
        {
            var harness = Harness.Create();
        var service = harness.Service;
            var battleId = $"boss-no-skill-{boss.BossId.Value}";

            // A charge requirement no single Swap's Matches can reach.
            var unreachable = boss with
            {
                SkillDefinition = boss.SkillDefinition with { ChargeRequirement = 100_000 },
            };

            var created = await service.CreateBattleAsync(battleId, Owner, Pet, unreachable);
            var result = (await service.ExecuteSwapAsync(battleId, FindMatchProducer(created))) ?? throw new InvalidOperationException("the Swap must resolve");

            Assert.True(result.IsAccepted);
            Assert.DoesNotContain(result.Events, e => e.Type == BattleEventType.BossSkillCast);

            var pet = result.State.PetState;

            Assert.Empty(BurnsOn(pet));
            Assert.Empty(RootsOn(pet));

            // The Boss dealt its Basic Attack damage, so the response genuinely ran.
            Assert.Contains(
                result.Events,
                e => e.Type == BattleEventType.DamageDealt
                    && e.DamageDealt.Source == DamageParty.Boss);
        }
    }

    // =======================================================================
    // Write-back and rejected-action boundary — GAME_STATE.md §5.1, §5.1.1
    // =======================================================================

    [Fact]
    public async Task RejectedSwap_ShouldApplyNoSecondaryEffect()
    {
        // GAME_STATE.md §5.1 item 6 / §5.1.1 item 11: a rejected action is not a
        // resolution, so step 19a does not run and nothing is applied.
        var harness = Harness.Create();
        var service = harness.Service;
        var battleId = "boss-effect-rejected";

        var created = await service.CreateBattleAsync(
            battleId, Owner, Pet, ThuyMaFiringImmediately());

        // A non-adjacent pair is rejected by the Swap rules (MATCH3_RULES.md
        // §2.1.2); it can never be a resolution.
        var rejected = await service.ExecuteSwapAsync(battleId, new SwapRequest(0, BoardState.CellCount - 1));

        Assert.True(rejected is null || !rejected.Value.IsAccepted);

        var after = (await service.GetBattleAsync(battleId))!;

        Assert.Empty(after.PetState.ActiveStatusEffects);
        Assert.Equal(created.PetState.Power, after.PetState.Power);
        Assert.Equal(created.PetState.ATK, after.PetState.ATK);
        Assert.Equal(created.Sequence, after.Sequence);
    }

    [Fact]
    public async Task ExhaustedCompareAndSet_ShouldCommitNoSecondaryEffect()
    {
        // REDIS_STATE.md §4 items 2–3 / GAME_STATE.md §5.1.1 item 9: the effects are
        // intermediate values of ONE resolution and exist only inside the single
        // gated write-back. When that write-back never lands, no effect may be
        // observable — there is no partial commit and no second write.
        var harness = Harness.Create();
        var service = harness.Service;
        var battleId = "boss-effect-cas-exhausted";

        var created = await service.CreateBattleAsync(
            battleId, Owner, Pet, ThuyMaFiringImmediately());

        var writesAfterCreation = harness.Store.WriteCount;

        // Every compare-and-set attempt is refused (more than the service's bounded
        // retry budget), so the resolution can never be committed.
        harness.Store.ForcedConflicts = 64;

        var pair = FindMatchProducer(created);
        var result = await service.ExecuteSwapAsync(battleId, pair);

        // The action was not committed, so it must not be reported as accepted.
        Assert.False(result is { IsAccepted: true });

        // The stored record is exactly what creation wrote: no Burn/Root instance,
        // no Power reduction, no Sequence or Turn advance.
        var after = (await service.GetBattleAsync(battleId))!;

        Assert.Empty(after.PetState.ActiveStatusEffects);
        Assert.Equal(created.PetState.Power, after.PetState.Power);
        Assert.Equal(created.PetState.ATK, after.PetState.ATK);
        Assert.Equal(created.Sequence, after.Sequence);
        Assert.Equal(created.Turn, after.Turn);

        // And the refused attempts performed no write at all (§4 item 7).
        Assert.Equal(writesAfterCreation, harness.Store.WriteCount);
    }

    [Fact]
    public async Task RetriedCompareAndSet_ShouldCommitTheEffectsExactlyOnce()
    {
        // REDIS_STATE.md §4 item 5: one action, one write-back. A refused first
        // attempt that the service retries must leave exactly one committed
        // resolution — the effects are not applied twice by the retry.
        var harness = Harness.Create();
        var service = harness.Service;
        var battleId = "boss-effect-cas-retry";

        var created = await service.CreateBattleAsync(
            battleId, Owner, Pet, ThuyMaFiringImmediately());

        var writesAfterCreation = harness.Store.WriteCount;

        harness.Store.ForcedConflicts = 1;

        var result = await service.ExecuteSwapAsync(battleId, FindMatchProducer(created));
        Assert.True(result!.Value.IsAccepted);

        var after = (await service.GetBattleAsync(battleId))!;

        // Exactly one resolution committed, and the Drain Power reduction was
        // applied once — a double-applied effect would floor differently here.
        Assert.Equal(created.Sequence + 1, after.Sequence);
        Assert.Equal(Math.Max(0, created.PetState.Power - 20), after.PetState.Power);
        Assert.Empty(after.PetState.ActiveStatusEffects);

        Assert.Equal(writesAfterCreation + 1, harness.Store.WriteCount);
    }

    [Fact]
    public async Task SuccessfulResolution_ShouldCommitAllEffectsInOneWriteBack()
    {
        // GAME_STATE.md §5.1.1 item 9 / §5.1: the application (steps 1–18) and the
        // step 19a consumption are intermediate values of ONE resolution, committed
        // by the single write-back under the Sequence compare-and-set. One accepted
        // Swap therefore advances Sequence by exactly one while carrying the effect.
        var harness = Harness.Create();
        var service = harness.Service;
        var battleId = "boss-effect-one-write-back";

        // Same firing setup as the other scenarios: one Match satisfies the Charge
        // Requirement, so this Swap's resolution carries the effect.
        var boss = BossDefinitions.HoaLong with
        {
            SkillDefinition = BossDefinitions.HoaLong.SkillDefinition with { ChargeRequirement = 1 },
        };

        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var result = (await service.ExecuteSwapAsync(battleId, FindMatchProducer(created))) ?? throw new InvalidOperationException("the Swap must resolve");
        Assert.True(result.IsAccepted);
        Assert.Contains(result.Events, e => e.Type == BattleEventType.BossSkillCast);

        var after = (await service.GetBattleAsync(battleId))!;

        Assert.Equal(created.Sequence + 1, after.Sequence);

        // The committed state is the post-19a state and carries the instance
        // (§5.1.1 item 9: "A reader never observes an instance mid-count").
        Assert.Single(BurnsOn(after.PetState));

        // And the resolution's own returned state agrees with what was stored —
        // there is no second, divergent commit.
        Assert.Equal(after.Sequence, result.State.Sequence);
        Assert.Equal(
            after.PetState.ActiveStatusEffects.Length,
            result.State.PetState.ActiveStatusEffects.Length);
    }

    // =======================================================================
    // Helpers
    // =======================================================================

    /// <summary>
    /// Fires the Boss's Skill on a fresh battle by making one Swap's Matches satisfy
    /// its Charge Requirement, and returns the accepted resolution.
    /// </summary>
    private static async Task<SwapExecutionResult> FireTheSkillAsync(
        Harness harness,
        string battleId,
        BossDefinition boss) =>
        await FireTheSkillWithPowerAsync(harness, battleId, boss, powerBefore: null);

    /// <summary>
    /// The same, optionally seeding the active Pet's Power first so a Power
    /// reduction can be observed from a documented starting value
    /// (<c>GAME_RULES.md</c> §12's 0–100 range).
    /// </summary>
    private static async Task<SwapExecutionResult> FireTheSkillWithPowerAsync(
        Harness harness,
        string battleId,
        BossDefinition boss,
        int? powerBefore)
    {
        var service = harness.Service;

        var firing = boss with
        {
            // One Match satisfies the requirement, so this very Swap fires the Skill
            // (GAME_STATE.md §2.4.3).
            SkillDefinition = boss.SkillDefinition with { ChargeRequirement = 1 },
        };

        var created = await service.CreateBattleAsync(battleId, Owner, Pet, firing);

        if (powerBefore is { } power)
        {
            await SetPetPowerAsync(harness.Store, battleId, power);
        }

        var pair = FindMatchProducer(created);

        var result = (await service.ExecuteSwapAsync(battleId, pair)) ?? throw new InvalidOperationException("the Swap must resolve");
        Assert.True(result.IsAccepted);
        Assert.Contains(result.Events, e => e.Type == BattleEventType.BossSkillCast);

        return result;
    }

    /// <summary>
    /// Runs the documented Drain Power scenario and returns the resulting Power:
    /// a Thủy Ma battle whose Pet is seeded to <paramref name="powerBefore"/>, then
    /// one Swap that fires Drain Power.
    /// </summary>
    private static async Task<int> DrainPowerScenarioAsync(
        Harness harness,
        string battleId,
        int powerBefore)
    {
        var result = await FireTheSkillWithPowerAsync(
            harness, battleId, ThuyMaFiringImmediately(), powerBefore);

        return result.State.PetState.Power;
    }

    /// <summary>
    /// Thủy Ma with a Charge Requirement one Match satisfies, so its Skill fires on
    /// the resolving Swap.
    /// </summary>
    private static BossDefinition ThuyMaFiringImmediately() =>
        BossDefinitions.ThuyMa with
        {
            SkillDefinition = BossDefinitions.ThuyMa.SkillDefinition with { ChargeRequirement = 1 },
        };

    private static StatusEffect[] BurnsOn(PetState pet) =>
        [.. pet.ActiveStatusEffects.Where(e => e.Id == "Burn")];

    private static StatusEffect[] RootsOn(PetState pet) =>
        [.. pet.ActiveStatusEffects.Where(e => e.Id == "Root")];

    private static SwapRequest FindMatchProducer(BattleState state) =>
        FindMatchProducingPair(state);

    /// <summary>
    /// Seeds the battle's Pet Power to a documented starting value so the
    /// reduction's floor can be observed from a specific point, preserving every
    /// other member and leaving <c>Sequence</c> untouched.
    ///
    /// <b>Why the store is written directly.</b> <c>Power</c> is a
    /// <c>PetState</c> combat stat (<c>GAME_STATE.md</c> §2.3) with no
    /// battle-start parameter on <see cref="BattleStateService.CreateBattleAsync"/>,
    /// so a scenario that must begin at a specific Power seeds the state it just
    /// created. This applies no game rule: the reduction under test still runs
    /// through the real resolution, and no production API is widened for a test.
    /// </summary>
    private static async Task SetPetPowerAsync(
        IBattleStateRepository repository,
        string battleId,
        int power)
    {
        var stored = (await repository.GetAsync(battleId))!;

        var applied = await repository.TryUpdateAsync(
            stored with { PetState = stored.PetState with { Power = power } },
            stored.Sequence);

        Assert.True(applied);
    }

    /// <summary>
    /// A battle service over an in-memory store, plus the store itself, so a
    /// scenario can both run the real resolution, seed a documented starting value
    /// the battle-start API does not parameterize, and drive the documented
    /// compare-and-set paths.
    ///
    /// The double models the documented creation, <c>Sequence</c>
    /// compare-and-set, and forced-conflict semantics (<c>REDIS_STATE.md</c> §4),
    /// so no test here passes against a more permissive contract than the store
    /// offers.
    /// </summary>
    private sealed record Harness(
        BattleStateService Service,
        InMemoryBattleStateRepository Store)
    {
        /// <summary>The store as the service's repository (<c>REDIS_STATE.md</c> §2).</summary>
        public IBattleStateRepository Repository => Store;

        public static Harness Create()
        {
            var store = new InMemoryBattleStateRepository();

            return new Harness(new BattleStateService(store, new FixedRngSeedSource()), store);
        }
    }

    private static BattleStateService NewService(IBattleStateRepository repository) =>
        new(repository, new FixedRngSeedSource());

    private static BattleStateService NewService() =>
        new(new InMemoryBattleStateRepository(), new FixedRngSeedSource());

    private static SwapRequest FindMatchProducingPair(BattleState state)
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
