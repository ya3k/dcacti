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
/// The Boss Skill Step-1 damage composition driven through a real Turn —
/// <c>COMBAT_RULES.md</c> §3.4's "Boss Skill Step-1 composition" (decided by
/// TASK-125, authored by TASK-126), whose consumption rule is §5.5.
///
/// <code>
/// Rule (COMBAT_RULES.md §3.4's composition, §5.5.1's Step-1 consumption point)
///  ↓
/// Scenario (Given a Boss with a documented ATK modifier state, When step 18b
///           resolves, Then the Skill's Step-1 Base Damage is the documented value)
///  ↓
/// Test
/// </code>
///
/// <b>Why this suite exists separately from the Domain consumer tests.</b> §3.4 is
/// a statement about what the <i>resolution</i> feeds the Damage Pipeline: the
/// composed value must arrive at the existing step-18b <c>DamagePipeline.Calculate</c>
/// call exactly once, the Basic Attack must be untouched, and <c>BossState.ATK</c>
/// must survive unchanged. Only a real resolution can demonstrate those, so these
/// scenarios drive <see cref="BattleStateService"/> and read the composed value out
/// of the <c>DamageCalculated</c> breakdown the resolution itself reports.
///
/// <b>Every expected value traces to §3.4 / §5.5 / §6.3.1, not to the
/// implementation.</b> §3.4's worked example (<c>100 + 150 = 250</c> with Rage
/// inactive; <c>120 + 150 = 270</c> with Rage active) is asserted verbatim, and the
/// authored <c>150</c> is read from the Boss definition's own declaration.
///
/// <b>How the Rage-active case is arranged without implementing step 18a.</b>
/// Hỏa Long's Rage <i>application</i> is the step-18a Boss Passive half and is not
/// implemented by this task; no production path creates a Rage instance. The
/// instance is therefore seeded into the Boss's own
/// <c>BossState.StatusEffects[]</c> through the documented public
/// <see cref="StatusEffectLifecycle.Apply"/> — the same technique
/// <c>RootAttackConsumptionTests</c> uses for the Pet side — and a real Turn is
/// then resolved over it. No production code path is widened for a test.
/// </summary>
public class BossSkillStep1CompositionTests
{
    /// <summary>
    /// The Pet these battles carry (<c>GAME_STATE.md</c> §2.3), Xích Lang's MVP
    /// Element and Passive. Its DEF and HP are the documented MVP values
    /// (<c>COMBAT_RULES.md</c> §1.1).
    /// </summary>
    private static readonly BattleStateService.PetConfiguration Pet =
        new(new PetId("pet_instance_1"), Element.Hoa, new PassiveId("xich-lang"), PassiveThreshold: 5);

    /// <summary>The owning Player of these battles (<c>GAME_STATE.md</c> §2.8).</summary>
    private static readonly PlayerId Owner = new("player_boss_step1_owner");

    /// <summary>
    /// Rage as <c>BOSS_RULES.md</c> §6.2.1 fixes it — a Turn-based
    /// <c>BuffDebuff</c> held in the Boss's own <c>StatusEffects[]</c> with
    /// <c>TargetStat = "ATK"</c>, <c>Magnitude = +20%</c>, <c>RemainingTurns = 3</c>.
    /// </summary>
    private static StatusEffect Rage(int duration = 3) =>
        StatusEffect.TurnBased(
            "Rage", StatusEffectType.BuffDebuff, StatusEffectSource.Boss, 20, duration, "ATK");

    /// <summary>
    /// A Boss-side ATK modifier expressed as a <b>decrease</b>, which
    /// <c>COMBAT_RULES.md</c> §5.5.1 item 4 carries as the negative
    /// <c>Magnitude</c>: "<c>Magnitude &lt; 0 → decrease</c>". Built through the
    /// documented factory so the <c>TargetStat</c>-iff-<c>BuffDebuff</c> pairing
    /// holds by construction (<c>GAME_STATE.md</c> §2.3.1 item 7).
    /// </summary>
    private static StatusEffect AtkDecrease(double magnitude) =>
        StatusEffect.TurnBased(
            "Weaken", StatusEffectType.BuffDebuff, StatusEffectSource.Boss, magnitude, 3, "ATK");

    // =======================================================================
    // Scenario 1 — Boss Skill with no ATK modifier (COMBAT_RULES.md §3.4)
    // =======================================================================

    [Fact]
    public async Task BossSkill_ShouldUseBossAtkPlusSkillBaseDamage_WhenNoAtkModifierIsActive()
    {
        // §3.4's composition with the modifier inactive:
        //   EffectiveBossATK = BossState.ATK = 100
        //   Step 1 = 100 + 150 = 250
        //
        // The expected breakdown is derived from the definition's own values and
        // §3.4's composition, never read back from the implementation.
        var harness = Harness.Create();
        var boss = CastingHoaLong();

        var turn = await ResolveCastingTurnAsync(harness, boss, "boss-step1-no-modifier");

        Assert.Equal(250, turn.SkillBase);

        // The composed sum is exactly the two documented contributions — the
        // stored stat and the authored value — and the authored value is unchanged.
        Assert.Equal(boss.ATK + boss.SkillBaseDamage, turn.SkillBase);
        Assert.Equal(150, boss.SkillBaseDamage);
    }

    // =======================================================================
    // Scenario 2 — Hỏa Long Rage active (COMBAT_RULES.md §3.4's worked example)
    // =======================================================================

    [Fact]
    public async Task BossSkill_ShouldUseEffectiveBossAtk_WhenRageIsActive()
    {
        // §3.4's worked example, asserted verbatim:
        //   BossState.ATK = 100, Hỏa Long Rage = +20%
        //   EffectiveBossATK = truncate(100 × (100 + 20) / 100) = 120
        //   Flame Burst authored Base Damage = 150  (unchanged by the modifier)
        //   Step 1 Base Damage = 120 + 150 = 270
        var harness = Harness.Create();
        var boss = CastingHoaLong();

        var turn = await ResolveCastingTurnAsync(harness, boss, "boss-step1-rage", seedRage: true);

        Assert.Equal(270, turn.SkillBase);
    }

    [Fact]
    public async Task BossSkill_ShouldDifferByExactlyTheModifiersEffect_WhenRageIsActive()
    {
        // The increase comes entirely from the EffectiveBossATK contribution: the
        // authored term is identical in both cases, so the two composed values
        // differ by exactly Rage's effect on the stat (120 − 100 = 20) and by
        // nothing else. This is the assertion that fails if the implementation
        // scaled the authored 150 as well.
        var harness = Harness.Create();
        var boss = CastingHoaLong();

        var without = await ResolveCastingTurnAsync(harness, boss, "boss-step1-delta-off");
        var with = await ResolveCastingTurnAsync(harness, boss, "boss-step1-delta-on", seedRage: true);

        Assert.Equal(250, without.SkillBase);
        Assert.Equal(270, with.SkillBase);
        Assert.Equal(20, with.SkillBase - without.SkillBase);

        // The authored value was never transformed: neither 150 → 180 (the modifier
        // reaching the authored term) nor a dropped ATK contribution. The recovered
        // authored contribution is the declared value in both cases.
        var effectiveBossAtk = StatusEffectLifecycle.EffectiveBossAttack(boss.ATK, [Rage()]);

        Assert.Equal(150, boss.SkillBaseDamage);
        Assert.Equal(boss.SkillBaseDamage, without.SkillBase - boss.ATK);
        Assert.Equal(boss.SkillBaseDamage, with.SkillBase - effectiveBossAtk);
    }

    // =======================================================================
    // Scenario 2b — the signed Magnitude's OTHER direction (COMBAT_RULES.md §5.5.1)
    // =======================================================================

    [Fact]
    public async Task BossSkill_ShouldUseTheDecreasedEffectiveBossAtk_WhenAModifierDecreases()
    {
        // §5.5.1 item 4: "Magnitude < 0 -> decrease", through §3.4's composition.
        //   EffectiveBossATK = truncate(100 × (100 + (−30)) / 100) = 70
        //   Step 1 = 70 + 150 = 220
        //
        // This is the direction the rework corrects. The superseded implementation
        // read |Magnitude| and inferred "debuff" from the sign, so it applied a
        // REDUCTION as an INCREASE and produced 130 + 150 = 280 here. Asserting only
        // the +20 case is what let that defect ship.
        var harness = Harness.Create();
        var boss = CastingHoaLong();

        var turn = await ResolveCastingTurnAsync(
            harness, boss, "boss-step1-decrease", seedRage: false, AtkDecrease(-30));

        Assert.Equal(220, turn.SkillBase);

        // The decrease is the EffectiveBossATK contribution alone: the authored 150
        // is recovered unchanged by subtracting the derived ATK term, exactly as in
        // the increase case (§5.5.2).
        Assert.Equal(150, turn.SkillBase - 70);

        // And it is emphatically NOT the old inverted reading.
        Assert.NotEqual(280, turn.SkillBase);
        Assert.NotEqual(130, turn.SkillBase - 150);
    }

    [Fact]
    public async Task BossSkill_ShouldLeaveTheStoredStatUnchanged_ForADecrease()
    {
        // §5.5.4 applies to both directions: the 70 is derived and discarded, and
        // the committed BossState.ATK is still the stored 100 — not 70, and not a
        // "restored" value.
        var harness = Harness.Create();
        var boss = CastingHoaLong();
        const string battleId = "boss-step1-decrease-immutability";

        await harness.Service.CreateBattleAsync(battleId, Owner, Pet, boss);
        await SeedBossStatusEffectAsync(harness.Store, battleId, AtkDecrease(-30));

        var before = (await harness.Service.GetBattleAsync(battleId))!;

        Assert.Equal(100, before.BossState.ATK);

        var result = (await harness.Service.ExecuteSwapAsync(
            battleId, FindMatchProducer(before)))!.Value;

        Assert.True(result.IsAccepted);

        var committed = (await harness.Service.GetBattleAsync(battleId))!;

        Assert.Equal(220, result.Events
            .Where(e => e.Type == BattleEventType.DamageCalculated)
            .Select(e => e.DamageCalculated)
            .Where(c => c.Base > boss.ATK + 50)
            .Max(c => c.Base));

        Assert.Equal(100, committed.BossState.ATK);
    }

    [Fact]
    public async Task BossSkill_ShouldTreatAZeroMagnitudeAsUnchanged()
    {
        // §5.5.1 item 4: "Magnitude = 0 -> unchanged  EffectiveBossATK =
        // BossState.ATK". A zero is not an error and not a 100% reduction: the
        // composed value is the same as with no instance at all.
        var harness = Harness.Create();
        var boss = CastingHoaLong();

        var turn = await ResolveCastingTurnAsync(
            harness, boss, "boss-step1-zero", seedRage: false, AtkDecrease(0));

        Assert.Equal(250, turn.SkillBase);
    }

    // =======================================================================
    // Scenario 3 — the authored Skill Base Damage is not modified
    // =======================================================================

    [Fact]
    public async Task BossSkill_ShouldLeaveTheAuthoredBaseDamageUnmodified_WhileRageApplies()
    {
        // §5.5.2: "Does NOT reach the Skill's authored Base Damage. That authored
        // value is a separate Step-1 contribution and does not receive the
        // modifier." The definition's declared value is read directly, and the
        // Step-1 value minus the derived ATK term recovers the contribution the
        // pipeline actually received — which must still be the authored 150.
        var harness = Harness.Create();
        var boss = CastingHoaLong();

        var turn = await ResolveCastingTurnAsync(harness, boss, "boss-step1-authored", seedRage: true);

        // §3.4: EffectiveBossATK = 120, so the authored contribution is 270 − 120.
        var effectiveBossAtk = StatusEffectLifecycle.EffectiveBossAttack(boss.ATK, [Rage()]);

        Assert.Equal(120, effectiveBossAtk);
        Assert.Equal(270, turn.SkillBase);

        // The recovered authored contribution is exactly the declared 150 — not 180
        // (which would mean the modifier reached it) and not 270 (which would mean
        // the ATK term was dropped).
        Assert.Equal(boss.SkillBaseDamage, turn.SkillBase - effectiveBossAtk);
        Assert.Equal(150, turn.SkillBase - effectiveBossAtk);
    }

    // =======================================================================
    // Scenario 4 — BossState.ATK immutability (COMBAT_RULES.md §5.5.4)
    // =======================================================================

    [Fact]
    public async Task BossSkill_ShouldNeverOverwriteTheStoredBossAtk()
    {
        // §5.5.4: "BossState.ATK is never overwritten by the modifier, and there is
        // no 'restore' step". The committed stat must therefore be identical before
        // and after the resolution that derived the increased value — and the
        // modifier instance itself must survive unmodified, since the composition
        // only READS it.
        var harness = Harness.Create();
        var boss = CastingHoaLong();
        const string battleId = "boss-step1-immutability";

        // Created first, then seeded, then re-read — the store's Sequence
        // compare-and-set can only apply to an existing battle (REDIS_STATE.md §4).
        await harness.Service.CreateBattleAsync(battleId, Owner, Pet, boss);
        await SeedBossStatusEffectAsync(harness.Store, battleId, Rage());

        var before = (await harness.Service.GetBattleAsync(battleId))!;

        Assert.Equal(100, before.BossState.ATK);
        Assert.Single(RageOn(before.BossState));

        var result = (await harness.Service.ExecuteSwapAsync(
            battleId, FindMatchProducer(before)))!.Value;

        Assert.True(result.IsAccepted);
        Assert.Contains(result.Events, e => e.Type == BattleEventType.BossSkillCast);

        var committed = (await harness.Service.GetBattleAsync(battleId))!;

        var skillBase = result.Events
            .Where(e => e.Type == BattleEventType.DamageCalculated)
            .Select(e => e.DamageCalculated)
            .Where(c => c.Base > boss.ATK + 50)
            .Max(c => c.Base);

        Assert.Equal(270, skillBase);

        // Still the base stat, not the derived value (120) and not a scaled one.
        Assert.Equal(100, committed.BossState.ATK);

        // The instance was read, not consumed or rewritten: it survives with its
        // own magnitude intact, decremented by exactly step 19a's one Turn.
        var rage = Assert.Single(RageOn(committed.BossState));

        Assert.Equal(20, rage.Magnitude);
        Assert.Equal("ATK", rage.TargetStat);
    }

    // =======================================================================
    // Scenario 5 — Damage Pipeline integration, exactly once
    // =======================================================================

    [Fact]
    public async Task BossSkill_ShouldEnterTheExistingPipelineExactlyOnce_WithNoPoolAndStep4Unchanged()
    {
        // §3.4: the composed value is the Skill's Step-1 Base Damage and it reaches
        // the EXISTING pipeline (steps 1-6) — there is no second damage path. A Boss
        // Skill contributes no ATK-Gem-generated damage pool (Bosses match no Gems),
        // and the modifier is a Step-1 input, never a Step-4 factor (§5.5.3).
        var harness = Harness.Create();
        var boss = CastingHoaLong();

        var turn = await ResolveCastingTurnAsync(harness, boss, "boss-step1-pipeline", seedRage: true);

        // Exactly one DamageCalculated carries the Skill's own instance. The
        // composition is identified by its Step-1 value, which no other instance in
        // this resolution can produce (the Burn tick ticks later at step 19a with
        // base 50).
        var skillCalculation = Assert.Single(
            turn.Events
                .Where(e => e.Type == BattleEventType.DamageCalculated)
                .Select(e => e.DamageCalculated),
            c => c.Base == 270);

        Assert.Equal(270, skillCalculation.Base);

        // §3.4 step 4: the Boss side's Other Modifiers stays 1.0 — the +20% is a
        // Step-1 input, so it must NOT also appear as a Step-4 factor (120 × 1.0,
        // not 120 × 1.2).
        Assert.Equal(1.00, skillCalculation.OtherModifiers);

        // No ATK-Gem pool contributed: §3.4 states a Boss Skill contributes none.
        Assert.Equal(1.00, skillCalculation.ComboModifier);

        // And the Skill's own damage instance is emitted exactly once, which is what
        // "no double application" means observably: two instances carrying the
        // composed base would mean the modifier was applied twice.
        Assert.Single(
            turn.Events
                .Where(e => e.Type == BattleEventType.DamageCalculated)
                .Select(e => e.DamageCalculated),
            c => c.Base == 270);
    }

    // =======================================================================
    // Scenario 6 — Basic Attack is unchanged (COMBAT_RULES.md §3.4)
    // =======================================================================

    [Fact]
    public async Task BossBasicAttack_ShouldUseBossAtkAlone_WithNoSkillBaseDamage()
    {
        // §3.4's Basic Attack clause: "Step 1 — Base Damage = Boss.ATK". The Skill's
        // authored base value contributes NOTHING here, and the fallback must remain
        // exactly what TASK-022 implemented.
        var harness = Harness.Create();

        // A Charge Requirement no single Swap can reach leaves the Skill unfired, so
        // step 18c's Basic Attack is the path taken.
        var boss = BossDefinitions.HoaLong with
        {
            SkillDefinition = BossDefinitions.HoaLong.SkillDefinition with { ChargeRequirement = 100 },
        };

        var created = await harness.Service.CreateBattleAsync(
            "boss-step1-basic-attack", Owner, Pet, boss);

        var result = (await harness.Service.ExecuteSwapAsync(
            "boss-step1-basic-attack", FindMatchProducer(created)))!.Value;

        Assert.True(result.IsAccepted);
        Assert.DoesNotContain(result.Events, e => e.Type == BattleEventType.BossSkillCast);

        var bossCalculation = Assert.Single(
            result.Events
                .Where(e => e.Type == BattleEventType.DamageCalculated)
                .Select(e => e.DamageCalculated),
            c => c.Base == boss.ATK);

        // Step 1 is the stored stat alone: not 100 + 150 = 250.
        Assert.Equal(100, bossCalculation.Base);
        Assert.Equal(boss.ATK, bossCalculation.Base);
    }

    [Fact]
    public async Task BossBasicAttack_ShouldNotChange_WhenAModifierIsActive()
    {
        // §5.5.2: the modifier "Reaches the Boss BASIC ATTACK". The Basic Attack's
        // Step-1 input is therefore the derived ATK ALONE — it gains the +20% but
        // still receives no authored Skill Base Damage, so it is 120 and never 270.
        var harness = Harness.Create();
        var boss = BossDefinitions.HoaLong with
        {
            SkillDefinition = BossDefinitions.HoaLong.SkillDefinition with { ChargeRequirement = 100 },
        };

        const string battleId = "boss-step1-basic-modifier";

        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, boss);

        await SeedBossStatusEffectAsync(harness.Store, battleId, Rage());

        var result = (await harness.Service.ExecuteSwapAsync(
            battleId, FindMatchProducer(created)))!.Value;

        Assert.True(result.IsAccepted);
        Assert.DoesNotContain(result.Events, e => e.Type == BattleEventType.BossSkillCast);

        // 120 — the derived stat. NOT 270 (no Skill term) and NOT 100 (the modifier
        // does reach this path).
        var bossCalculation = Assert.Single(
            result.Events
                .Where(e => e.Type == BattleEventType.DamageCalculated)
                .Select(e => e.DamageCalculated),
            c => c.Base == 120);

        Assert.Equal(120, bossCalculation.Base);
        Assert.Equal(1.00, bossCalculation.OtherModifiers);
    }

    [Fact]
    public async Task BossBasicAttack_ShouldUseTheDecreasedStat_WhenAModifierDecreases()
    {
        // §5.5.1 item 4's other direction on the Basic Attack path: §5.5.2 states
        // the modifier "Reaches the Boss BASIC ATTACK", so the Step-1 input is the
        // derived ATK alone — here 70, never 220 (no Skill term) and never 130.
        var harness = Harness.Create();
        var boss = BossDefinitions.HoaLong with
        {
            SkillDefinition = BossDefinitions.HoaLong.SkillDefinition with { ChargeRequirement = 100 },
        };

        const string battleId = "boss-step1-basic-decrease";

        var created = await harness.Service.CreateBattleAsync(battleId, Owner, Pet, boss);

        await SeedBossStatusEffectAsync(harness.Store, battleId, AtkDecrease(-30));

        var result = (await harness.Service.ExecuteSwapAsync(
            battleId, FindMatchProducer(created)))!.Value;

        Assert.True(result.IsAccepted);
        Assert.DoesNotContain(result.Events, e => e.Type == BattleEventType.BossSkillCast);

        var bossCalculation = Assert.Single(
            result.Events
                .Where(e => e.Type == BattleEventType.DamageCalculated)
                .Select(e => e.DamageCalculated),
            c => c.Base == 70);

        Assert.Equal(70, bossCalculation.Base);
        Assert.Equal(1.00, bossCalculation.OtherModifiers);
    }

    // =======================================================================
    // Scenario 7 — modifier selection (COMBAT_RULES.md §5.5.3, §5.4.5)
    // =======================================================================

    [Fact]
    public async Task BossSkill_ShouldIgnoreAnAtkModifierBearingAnUnrelatedIdentity()
    {
        // §5.5.3 applies §5.4.5's discipline: selection is by Type + TargetStat, so
        // an "ATK" instance whose Id is NOT "Rage" still applies. This is the
        // positive half of "never dispatch on Id".
        var harness = Harness.Create();
        var boss = CastingHoaLong();

        var turn = await ResolveCastingTurnAsync(
            harness,
            boss,
            "boss-step1-unrelated-id",
            seedRage: false,
            StatusEffect.TurnBased(
                "SomeOtherAtkBuff", StatusEffectType.BuffDebuff, StatusEffectSource.Boss, 20, 3, "ATK"));

        Assert.Equal(270, turn.SkillBase);
    }

    [Fact]
    public async Task BossSkill_ShouldIgnoreInstancesThatAreNotAtkModifiers()
    {
        // §5.5.3's negative set: a BuffDebuff naming another stat, a DoT (the Boss's
        // own Burn tick lives in this very collection), a Shield, and a State are
        // all non-ATK instances and must leave the composed value at its
        // unmodified 250.
        var harness = Harness.Create();
        var boss = CastingHoaLong();

        var turn = await ResolveCastingTurnAsync(
            harness,
            boss,
            "boss-step1-non-atk",
            seedRage: false,
            StatusEffect.TurnBased("Guard", StatusEffectType.BuffDebuff, StatusEffectSource.Boss, 50, 3, "DEF"),
            StatusEffect.TurnBased("Burn", StatusEffectType.DoT, StatusEffectSource.Player, 50, 2),
            StatusEffect.TurnBased("Stun", StatusEffectType.State, StatusEffectSource.Player, 1, 1));

        Assert.Equal(250, turn.SkillBase);

        // The instances genuinely were present at attack resolution — the negative
        // above is not the trivial result of nothing having been seeded. Asserted on
        // the PRE-resolution state, because the 1-Turn Stun is consumed and removed
        // by that same Turn's step 19a (§5.3 DR5) and so is correctly absent from
        // the committed state.
        Assert.Equal(3, turn.Before.BossState.ActiveStatusEffects.Length);
        Assert.DoesNotContain(
            turn.Before.BossState.ActiveStatusEffects,
            e => e.Type == StatusEffectType.BuffDebuff && e.TargetStat == "ATK");
    }

    // =======================================================================
    // Scenario 8 — the derivation is a function of the stored stat, not a constant
    // =======================================================================

    [Fact]
    public async Task BossSkill_ShouldDeriveFromTheStoredStat_ForANonDefaultBossAtk()
    {
        // §5.5.1 item 3: "EffectiveBossATK = the modified BossState.ATK" — the
        // derivation is a function of the stored stat. A Boss whose ATK is not the
        // MVP default must therefore produce a different composed value, so an
        // implementation that hard-coded the documented 120 cannot pass.
        var harness = Harness.Create();

        // ATK 200 with the same authored 150 and the same +20%.
        var boss = CastingHoaLong() with { ATK = 200 };

        var turn = await ResolveCastingTurnAsync(harness, boss, "boss-step1-nondefault-atk", seedRage: true);

        // truncate(200 × 120 / 100) = 240, so Step 1 = 240 + 150 = 390.
        Assert.Equal(390, turn.SkillBase);
    }

    // =======================================================================
    // Scenario 9 — modifier filtering keeps TASK-118's effects intact
    // =======================================================================

    [Fact]
    public async Task FiredBossSkill_ShouldStillApplyItsSecondaryEffect_AlongsideTheComposition()
    {
        // TASK-118's step-18b ordering must be undisturbed: the Skill's damage is
        // composed by this task, and its declared secondary effect (Flame Burst's
        // Burn, BOSS_RULES.md §6.3.1 item 1) is applied exactly as before. Both
        // happen in the same resolution's single write-back.
        var harness = Harness.Create();
        var boss = CastingHoaLong();

        var turn = await ResolveCastingTurnAsync(harness, boss, "boss-step1-secondary", seedRage: true);

        Assert.Equal(270, turn.SkillBase);

        // The Burn instance is still applied to the PET, with its authored
        // magnitude and duration — unaffected by the Boss-side ATK composition.
        var burn = Assert.Single(
            turn.Committed.PetState.ActiveStatusEffects,
            e => e.Id == "Burn");

        Assert.Equal(50, burn.Magnitude);
        Assert.Equal(StatusEffectType.DoT, burn.Type);
        Assert.Equal(StatusEffectSource.Boss, burn.Source);
    }

    [Fact]
    public async Task BossSkill_ShouldNotTreatTheBossOwnBurnDotAsAnAtkModifier()
    {
        // The Boss's OWN collection can legitimately hold a DoT (its step-19a tick
        // path reads one). §5.5.3 excludes DoT instances from the ATK consumer, so a
        // Boss-sourced Burn sitting on the BOSS must not scale the Skill's damage —
        // the composed value stays at the unmodified 250.
        var harness = Harness.Create();
        var boss = CastingHoaLong();

        var turn = await ResolveCastingTurnAsync(
            harness,
            boss,
            "boss-step1-boss-dot",
            seedRage: false,
            StatusEffect.TurnBased("Burn", StatusEffectType.DoT, StatusEffectSource.Boss, 50, 2));

        Assert.Equal(250, turn.SkillBase);
    }

    // =======================================================================
    // Helpers
    // =======================================================================

    /// <summary>
    /// One resolved Turn's Boss Skill damage instance and the resulting committed
    /// state.
    /// </summary>
    /// <param name="SkillBase">
    /// The Boss Skill's Step-1 Base Damage as the pipeline itself reported it —
    /// read from the resolution's own <c>DamageCalculated</c> event, never
    /// recomputed by the test.
    /// </param>
    /// <param name="Events">The resolution's events (<c>GAME_EVENTS.md</c> §1).</param>
    /// <param name="Before">
    /// The committed state the Turn started from — the post-seed, pre-resolution
    /// state. It is what a negative assertion about a seeded instance's
    /// <i>presence</i> must read, because step 19a may legitimately remove a
    /// short-duration instance within the same Turn.
    /// </param>
    /// <param name="Committed">The battle state the resolution committed.</param>
    private readonly record struct ResolvedSkillTurn(
        int SkillBase,
        IReadOnlyList<BattleEvent> Events,
        BattleState Before,
        BattleState Committed);

    /// <summary>
    /// Hỏa Long configured so Flame Burst fires on the Turn's own Matches: a Charge
    /// Requirement one Match satisfies. The authored values — ATK 100 and the
    /// Skill's 150 — are <c>BOSS_RULES.md</c> §6.1/§6.3's and are deliberately not
    /// overridden, so the scenarios assert the documents' own numbers.
    /// </summary>
    private static BossDefinition CastingHoaLong() =>
        BossDefinitions.HoaLong with
        {
            SkillDefinition = BossDefinitions.HoaLong.SkillDefinition with { ChargeRequirement = 1 },
        };

    /// <summary>
    /// Resolves one Turn in which the Boss Skill fires, optionally seeding the
    /// documented Rage instance first, and reports the Skill's own Step-1 value.
    ///
    /// <b>The battle is created before the seed and re-read after it.</b> The seed
    /// writes through the repository's <c>Sequence</c> compare-and-set
    /// (<c>REDIS_STATE.md</c> §4), so it can only apply to a battle that exists, and
    /// the Turn must start from the POST-seed state rather than the creation
    /// snapshot — the same ordering <c>RootAttackConsumptionTests</c> uses.
    ///
    /// <b>The Skill's instance is identified structurally, not positionally.</b>
    /// Flame Burst applies a Pet Burn that ticks at step 19a with base 50, and the
    /// Boss's own DoT ticks are separate instances, so "the last" or "the only"
    /// <c>DamageCalculated</c> would not reliably identify the Skill. The Skill's
    /// instance is selected by its Step-1 value, which for every MVP Boss is at
    /// least the stored ATK plus the authored value (≥ 200), while the step-19a
    /// Burn tick's base is the flat 50 (<c>BOSS_RULES.md</c> §6.3.1 item 1).
    /// </summary>
    private static async Task<ResolvedSkillTurn> ResolveCastingTurnAsync(
        Harness harness,
        BossDefinition boss,
        string battleId,
        bool seedRage = false,
        params StatusEffect[] seeds)
    {
        await harness.Service.CreateBattleAsync(battleId, Owner, Pet, boss);

        var toSeed = seedRage ? [Rage(), .. seeds] : seeds;

        foreach (var effect in toSeed)
        {
            await SeedBossStatusEffectAsync(harness.Store, battleId, effect);
        }

        // The seed wrote through the store, so the Turn must start from the
        // POST-seed state — re-reading it keeps the seeded instances in scope.
        var state = (await harness.Service.GetBattleAsync(battleId))!;

        if (seedRage)
        {
            Assert.Single(RageOn(state.BossState));
        }

        var result = (await harness.Service.ExecuteSwapAsync(
            battleId, FindMatchProducer(state)))!.Value;

        Assert.True(result.IsAccepted);
        Assert.Contains(result.Events, e => e.Type == BattleEventType.BossSkillCast);

        var committed = (await harness.Service.GetBattleAsync(battleId))!;

        var skillBase = result.Events
            .Where(e => e.Type == BattleEventType.DamageCalculated)
            .Select(e => e.DamageCalculated)
            .Where(c => c.Base > boss.ATK + 50)
            .Max(c => c.Base);

        return new ResolvedSkillTurn(skillBase, result.Events, state, committed);
    }

    private static StatusEffect[] RageOn(BossState boss) =>
        [.. boss.ActiveStatusEffects.Where(e => e.Id == "Rage")];

    /// <summary>
    /// Seeds a Boss Status Effect instance through the documented Domain operation
    /// (<c>GAME_STATE.md</c> §5.1.1 item 1), preserving every other member and
    /// leaving <c>Sequence</c> untouched.
    ///
    /// <b>Why the store is written directly.</b> Hỏa Long's Rage <i>application</i>
    /// is step 18a's Boss Passive effect and is not implemented — it is the separate
    /// Passive half of <c>ROADMAP.md</c> Phase 1 and is explicitly out of this
    /// task's scope. The instance is therefore arranged with the same public
    /// <see cref="StatusEffectLifecycle.Apply"/> the resolution itself uses, so the
    /// scenarios can demonstrate the composition §3.4 defines without a synthetic
    /// production path. This applies no game rule and widens no production API.
    /// </summary>
    private static async Task SeedBossStatusEffectAsync(
        IBattleStateRepository repository,
        string battleId,
        StatusEffect effect)
    {
        var stored = (await repository.GetAsync(battleId))!;

        var applied = await repository.TryUpdateAsync(
            stored with
            {
                BossState = stored.BossState with
                {
                    ActiveStatusEffects = StatusEffectLifecycle.Apply(
                        stored.BossState.ActiveStatusEffects,
                        effect),
                },
            },
            stored.Sequence);

        Assert.True(applied);
    }

    /// <summary>
    /// A battle service over an in-memory store, plus the store itself, so a
    /// scenario can both run real Turns and arrange the one documented state no
    /// implemented code path produces.
    ///
    /// The double models the documented creation and <c>Sequence</c>
    /// compare-and-set (<c>REDIS_STATE.md</c> §4), so no test here passes against a
    /// more permissive contract than the store offers.
    /// </summary>
    private sealed record Harness(BattleStateService Service, InMemoryBattleStateRepository Store)
    {
        public static Harness Create()
        {
            var store = new InMemoryBattleStateRepository();

            return new Harness(new BattleStateService(store, new FixedRngSeedSource()), store);
        }
    }

    private static SwapRequest FindMatchProducer(BattleState state) =>
        FindMatchProducingPair(state);

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
