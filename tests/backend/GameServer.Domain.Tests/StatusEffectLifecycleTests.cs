using GameServer.Domain.Battle;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// The <c>StatusEffects[]</c> lifecycle — <c>GAME_STATE.md</c> §5.1.1, with the
/// gameplay rule owned by <c>COMBAT_RULES.md</c> §5.3 (DR1–DR6).
///
/// Every scenario below is one of the worked examples of
/// <c>COMBAT_RULES.md</c> §5.3.3 or a numbered item of <c>GAME_STATE.md</c>
/// §5.1.1. The tests assert the documented state transition, not merely a final
/// number: where the rule counts decrements (DR2, DR4), the decrement is asserted
/// as a count.
/// </summary>
public class StatusEffectLifecycleTests
{
    private static StatusEffect Root(int duration) =>
        StatusEffect.TurnBased(
            "Root", StatusEffectType.BuffDebuff, StatusEffectSource.Boss, -30, duration, "ATK");

    private static StatusEffect Burn(int duration) =>
        StatusEffect.TurnBased(
            "Burn", StatusEffectType.DoT, StatusEffectSource.Boss, 50, duration);

    private static StatusEffect Shield() =>
        StatusEffect.TriggerBased(
            "Shield", StatusEffectType.Shield, StatusEffectSource.Player, 200,
            StatusEffect.ShieldDepletedCondition);

    private static StatusEffect Stun(int duration) =>
        StatusEffect.TurnBased(
            "Stun", StatusEffectType.State, StatusEffectSource.Boss, 0, duration);

    /// <summary>The active instance for an identity, or <c>null</c> when inactive.</summary>
    private static StatusEffect? Find(IReadOnlyList<StatusEffect> effects, string id)
    {
        foreach (var effect in effects)
        {
            if (effect.Id == id)
            {
                return effect;
            }
        }

        return null;
    }

    /// <summary>
    /// The remaining Turns of an identity that is expected to be active. The
    /// active/inactive distinction is itself asserted where it matters, so this
    /// helper fails loudly rather than silently reading a default.
    /// </summary>
    private static int Turns(IReadOnlyList<StatusEffect> effects, string id)
    {
        var effect = Find(effects, id);

        Assert.True(effect is not null, $"'{id}' is expected to be active.");
        Assert.True(
            effect!.Value.UsesTurnCountdown,
            $"'{id}' is expected to use the Turn countdown (GAME_STATE.md §2.3.1 item 3).");

        return effect.Value.RemainingTurns!.Value;
    }

    /// <summary>Whether an identity is active in the collection.</summary>
    private static bool IsActive(IReadOnlyList<StatusEffect> effects, string id) =>
        Find(effects, id) is not null;

    // =====================================================================
    // §5.1.1 item 1 / DR1, DR3: Apply sets RemainingTurns = duration
    // =====================================================================

    [Fact]
    public void Apply_ShouldInitializeDurationOnAnEmptyCollection()
    {
        // GAME_STATE.md §5.1.1 item 1 / COMBAT_RULES.md §5.3 DR1, DR3: applying an
        // inactive effect appends one element with RemainingTurns = duration.
        var applied = StatusEffectLifecycle.Apply([], Root(2));

        Assert.Single(applied);
        Assert.Equal("Root", applied[0].Id);
        Assert.Equal(2, applied[0].RemainingTurns);
    }

    [Fact]
    public void Apply_ShouldRefreshInPlaceRatherThanAppendASecondInstance()
    {
        // GAME_STATE.md §2.3.1 item 6: "the array holds at most one element per
        // Id" — an apply of an already-active identity refreshes that instance
        // rather than appending a second one. §5.1.1 item 1 repeats it, and DR3
        // makes Refresh re-execute the same "set remaining" operation.
        var applied = StatusEffectLifecycle.Apply([], Root(2));
        var refreshed = StatusEffectLifecycle.Apply(applied, Root(5));

        Assert.Single(refreshed);
        Assert.Equal(5, refreshed[0].RemainingTurns);
    }

    [Fact]
    public void Refresh_ShouldNotAddToTheCurrentValue()
    {
        // GAME_STATE.md §5.1.1 item 1 / COMBAT_RULES.md §5.3 DR3: Apply is a "set",
        // not an increment — refreshing "does not add to the current value". After
        // one decrement leaves RemainingTurns at 1, a refresh to 2 must be 2, not 3.
        var applied = StatusEffectLifecycle.Apply([], Root(2));
        var consumed = StatusEffectLifecycle.ConsumeAtStep19a(applied);

        Assert.Equal(1, consumed[0].RemainingTurns);

        var refreshed = StatusEffectLifecycle.Apply(consumed, Root(2));

        Assert.Single(refreshed);
        Assert.Equal(2, refreshed[0].RemainingTurns);
    }

    [Fact]
    public void Refresh_ShouldPreserveTheCollectionOrder()
    {
        // GAME_STATE.md §2.3.1 item 10 makes position non-semantic and §2.3.2
        // item 6 preserves order for round-trip fidelity, so refreshing an existing
        // instance must not move it.
        var effects = new[] { Burn(2), Root(2), Shield() };

        var refreshed = StatusEffectLifecycle.Apply(effects, Root(5));

        Assert.Equal(["Burn", "Root", "Shield"], refreshed.Select(effect => effect.Id));
        Assert.Equal(5, refreshed[1].RemainingTurns);
    }

    [Fact]
    public void Apply_ShouldRejectATriggerBasedInstance()
    {
        // GAME_STATE.md §5.1.1 item 7: a trigger-based instance is not on the Turn
        // countdown, so there is no duration for Apply/Refresh to set.
        Assert.Throws<ArgumentException>(() => StatusEffectLifecycle.Apply([], Shield()));
    }

    // =====================================================================
    // COMBAT_RULES.md §5.3.3: worked example — duration = 2, no refresh
    // =====================================================================

    [Fact]
    public void DurationTwo_ShouldFollowTheDocumentedTwoTurnExample()
    {
        // COMBAT_RULES.md §5.3.3:
        //   Turn N:   Apply(2)      -> remaining = 2
        //             §17 step 19a  -> remaining = 1
        //   Turn N+1: active
        //             §17 step 19a  -> remaining = 0 -> expires
        //   Turn N+2: inactive
        var turnN = StatusEffectLifecycle.Apply([], Root(2));

        Assert.Equal(2, Turns(turnN, "Root"));

        var afterTurnN = StatusEffectLifecycle.ConsumeAtStep19a(turnN);

        Assert.Equal(1, Turns(afterTurnN, "Root"));

        var afterTurnN1 = StatusEffectLifecycle.ConsumeAtStep19a(afterTurnN);

        Assert.False(IsActive(afterTurnN1, "Root"));
        Assert.Empty(afterTurnN1);
    }

    // =====================================================================
    // COMBAT_RULES.md §5.3.3: worked example — duration = 1, no refresh
    // =====================================================================

    [Fact]
    public void DurationOne_ShouldExpireAtTheApplicationTurnsStep19a()
    {
        // COMBAT_RULES.md §5.3.3:
        //   Turn N:   Apply(1)      -> remaining = 1
        //             §17 step 19a  -> remaining = 0 -> expires
        //   Turn N+1: inactive
        // GAME_STATE.md §5.1.1 item 5: the decrement and the removal occur within
        // the single step 19a pass of one resolved Turn.
        var turnN = StatusEffectLifecycle.Apply([], Root(1));

        Assert.Equal(1, Turns(turnN, "Root"));

        var afterTurnN = StatusEffectLifecycle.ConsumeAtStep19a(turnN);

        Assert.False(IsActive(afterTurnN, "Root"));
        Assert.Empty(afterTurnN);
    }

    // =====================================================================
    // COMBAT_RULES.md §5.3.3: worked example — refreshed in Turn N+1
    // =====================================================================

    [Fact]
    public void DurationTwo_RefreshedInTurnNPlusOne_ShouldExpireAfterThatRefresh()
    {
        // COMBAT_RULES.md §5.3.3:
        //   Turn N:   Apply(2)      -> remaining = 2
        //             §17 step 19a  -> remaining = 1
        //   Turn N+1: Refresh(2)    -> remaining = 2
        //             §17 step 19a  -> remaining = 1
        //   Turn N+2: §17 step 19a  -> remaining = 0 -> expires
        var turnN = StatusEffectLifecycle.Apply([], Root(2));
        var afterTurnN = StatusEffectLifecycle.ConsumeAtStep19a(turnN);

        var refreshed = StatusEffectLifecycle.Apply(afterTurnN, Root(2));

        Assert.Equal(2, Turns(refreshed, "Root"));

        var afterTurnN1 = StatusEffectLifecycle.ConsumeAtStep19a(refreshed);

        Assert.Equal(1, Turns(afterTurnN1, "Root"));

        var afterTurnN2 = StatusEffectLifecycle.ConsumeAtStep19a(afterTurnN1);

        Assert.False(IsActive(afterTurnN2, "Root"));
    }

    // =====================================================================
    // COMBAT_RULES.md §5.3.3 / DR4: same-Turn apply + refresh
    // =====================================================================

    [Fact]
    public void SameTurnApplyAndRefresh_ShouldCauseExactlyOneDecrement()
    {
        // COMBAT_RULES.md §5.3.3 / DR4:
        //   Turn N:   Apply(2)      -> remaining = 2
        //             Refresh(2)    -> remaining = 2 (reset; no extra consumption)
        //             §17 step 19a  -> remaining = 1
        //   Turn N+1: active
        //             §17 step 19a  -> remaining = 0 -> expires
        //
        // DR4 states reapplication "resets remaining to the new duration value and
        // does NOT trigger an additional consumption in that Turn. Only step 19a
        // consumes." The count is asserted, not just the value: exactly one
        // decrement occurred, so the result is 2 - 1 = 1 and never 2 - 2 = 0.
        var applied = StatusEffectLifecycle.Apply([], Root(2));
        var reapplied = StatusEffectLifecycle.Apply(applied, Root(2));

        Assert.Equal(2, Turns(reapplied, "Root"));

        var afterTurnN = StatusEffectLifecycle.ConsumeAtStep19a(reapplied);

        // Exactly one decrement occurred (2 - 1 = 1), never two (2 - 2 = 0).
        Assert.True(IsActive(afterTurnN, "Root"));
        Assert.Equal(1, Turns(afterTurnN, "Root"));

        var afterTurnN1 = StatusEffectLifecycle.ConsumeAtStep19a(afterTurnN);

        Assert.False(IsActive(afterTurnN1, "Root"));
    }

    [Fact]
    public void SameTurnReapplication_ShouldNotDecrementAtAllBeforeStep19a()
    {
        // COMBAT_RULES.md §5.3 DR4: "Only step 19a consumes." Apply and Refresh
        // therefore perform no consumption themselves, so any number of them
        // before the pass leaves the countdown where the last one set it.
        var effects = StatusEffectLifecycle.Apply([], Root(2));
        effects = StatusEffectLifecycle.Apply(effects, Root(2));
        effects = StatusEffectLifecycle.Apply(effects, Root(2));

        Assert.Single(effects);
        Assert.Equal(2, effects[0].RemainingTurns);
    }

    // =====================================================================
    // §5.1.1 item 2 / DR2: exactly one decrement per resolved Turn
    // =====================================================================

    [Fact]
    public void ConsumeAtStep19a_ShouldDecrementEachActiveInstanceExactlyOnce()
    {
        // GAME_STATE.md §5.1.1 item 2 / COMBAT_RULES.md §5.3 DR2: "Every active
        // Turn-countdown instance loses exactly one Turn of duration at step 19a."
        // A single pass decrements each instance by exactly 1 — never 2.
        var effects = new[] { Burn(3), Root(3) };

        var consumed = StatusEffectLifecycle.ConsumeAtStep19a(effects);

        Assert.Equal(2, consumed.Length);
        Assert.Equal(2, Turns(consumed, "Burn"));
        Assert.Equal(2, Turns(consumed, "Root"));
    }

    [Fact]
    public void ConsumeAtStep19a_OnAnEmptyCollection_ShouldReturnEmpty()
    {
        // GAME_STATE.md §2.3.2 item 1: an entity with no active effect holds an
        // empty collection, and a pass over it produces the same empty collection.
        var consumed = StatusEffectLifecycle.ConsumeAtStep19a([]);

        Assert.Empty(consumed);
    }

    // =====================================================================
    // §5.1.1 item 7 / COMBAT_RULES.md §5.3.2: trigger-based effects are excluded
    // =====================================================================

    [Fact]
    public void ConsumeAtStep19a_ShouldNotDecrementATriggerBasedInstance()
    {
        // GAME_STATE.md §5.1.1 item 7: an instance carrying ExpiryCondition "is not
        // touched by the step 19a countdown; it is removed by its own documented
        // trigger ... The step 19a pass must not invent a duration for it."
        // COMBAT_RULES.md §5.3.2 states the same scope rule.
        var effects = new[] { Shield() };

        // Several Turns pass; the Shield is untouched by every one of them.
        var after = effects;

        for (var turn = 0; turn < 5; turn++)
        {
            after = StatusEffectLifecycle.ConsumeAtStep19a(after);

            Assert.Single(after);
            Assert.Equal("Shield", after[0].Id);
            Assert.Null(after[0].RemainingTurns);
            Assert.Equal(StatusEffect.ShieldDepletedCondition, after[0].ExpiryCondition);
        }
    }

    [Fact]
    public void ConsumeAtStep19a_ShouldDecrementTurnBasedInstancesAndLeaveTriggers()
    {
        // GAME_STATE.md §5.1.1 items 2 and 7 together: in one pass, a Turn-based
        // instance is decremented and a trigger-based instance alongside it is not
        // — the two duration models must not be conflated by the pass.
        var effects = new[] { Burn(2), Shield() };

        var consumed = StatusEffectLifecycle.ConsumeAtStep19a(effects);

        Assert.Equal(2, consumed.Length);
        Assert.Equal(1, Turns(consumed, "Burn"));
        Assert.Null(Find(consumed, "Shield")!.Value.RemainingTurns);
    }

    // =====================================================================
    // §5.1.1 item 6 / §2.3.1 item 11: deterministic Id-ascending pass order
    // =====================================================================

    [Fact]
    public void ConsumeAtStep19a_ShouldProcessInstancesInIdAscendingOrder()
    {
        // GAME_STATE.md §5.1.1 item 6: "Instances are processed in a fixed order —
        // by Id in ordinal ascending order — so the resulting state is reproducible
        // for a given input state and does not depend on array insertion order."
        // The order is asserted directly, because §2.3.1 item 11 makes it part of
        // the contract rather than an implementation detail.
        var effects = new[] { Root(2), Burn(2), Shield(), Stun(2) };

        var consumed = StatusEffectLifecycle.ConsumeAtStep19a(effects);

        Assert.Equal(
            ["Burn", "Root", "Shield", "Stun"],
            consumed.Select(effect => effect.Id));
    }

    [Fact]
    public void ConsumeAtStep19a_ShouldNotDependOnInsertionOrder()
    {
        // GAME_STATE.md §5.1.1 item 6 / §2.3.1 item 11: the result "does not depend
        // on array insertion order". Two collections holding the same instances in
        // different orders must therefore produce the same state.
        var ascending = new[] { Burn(2), Root(2), Stun(2) };
        var shuffled = new[] { Stun(2), Burn(2), Root(2) };

        var fromAscending = StatusEffectLifecycle.ConsumeAtStep19a(ascending);
        var fromShuffled = StatusEffectLifecycle.ConsumeAtStep19a(shuffled);

        Assert.Equal(fromAscending, fromShuffled);
    }

    // =====================================================================
    // §5.1.1 item 11: a rejected action mutates nothing
    // =====================================================================

    [Fact]
    public void Lifecycle_ShouldRejectANullCollection()
    {
        // GAME_STATE.md §2.3.2 item 1: the collection "always exists ... it is
        // never omitted and never null", so a null collection is not a
        // representable state and is rejected rather than treated as empty.
        Assert.Throws<ArgumentNullException>(
            () => StatusEffectLifecycle.Apply(null!, Root(2)));

        Assert.Throws<ArgumentNullException>(
            () => StatusEffectLifecycle.ConsumeAtStep19a((IReadOnlyList<StatusEffect>)null!));
    }

    // =====================================================================
    // §5.1.1 item 8 / §2.4.5: Stun expiry reverts BossState.State to Idle
    // =====================================================================

    [Fact]
    public void ConsumeAtStep19a_ShouldRevertBossStateToIdleWhenStunExpires()
    {
        // GAME_STATE.md §5.1.1 item 8: "When a Stun instance expires at step 19a,
        // BossState.State reverts to Idle in the same resolution, so State never
        // disagrees with the presence of the Stun instance (§2.4.5).
        // RemainingTurns is authoritative; State reflects it."
        var state = BattleStateWithBossEffects(
            bossState: TestBossState() with
            {
                State = BossStateKind.Stunned,
                ActiveStatusEffects = [Stun(1)],
            });

        var after = StatusEffectLifecycle.ConsumeAtStep19a(state);

        Assert.Empty(after.BossState.ActiveStatusEffects);
        Assert.Equal(BossStateKind.Idle, after.BossState.State);
    }

    [Fact]
    public void ConsumeAtStep19a_ShouldKeepTheBossStunnedWhileTheStunIsActive()
    {
        // GAME_STATE.md §5.1.1 item 8: State "reflects" RemainingTurns. A Stun with
        // duration 2 survives its first pass, so State must still be Stunned.
        var state = BattleStateWithBossEffects(
            bossState: TestBossState() with
            {
                State = BossStateKind.Stunned,
                ActiveStatusEffects = [Stun(2)],
            });

        var after = StatusEffectLifecycle.ConsumeAtStep19a(state);

        Assert.Single(after.BossState.ActiveStatusEffects);
        Assert.Equal(1, after.BossState.ActiveStatusEffects[0].RemainingTurns);
        Assert.Equal(BossStateKind.Stunned, after.BossState.State);
    }

    [Fact]
    public void ConsumeAtStep19a_ShouldNotRevertEnrage()
    {
        // GAME_STATE.md §5.1.1 item 8 reverts only the Stun's state. BOSS_RULES.md
        // §5 item 4 makes Enrage "a permanent state transition ... no timer, no
        // duration field", so an Enraged Boss with no Stun instance is untouched:
        // this pass must not invent a transition no rule authorizes.
        var state = BattleStateWithBossEffects(
            bossState: TestBossState() with { State = BossStateKind.Enraged });

        var after = StatusEffectLifecycle.ConsumeAtStep19a(state);

        Assert.Equal(BossStateKind.Enraged, after.BossState.State);
    }

    // =====================================================================
    // §5.1.1: the pass covers both entities and touches nothing else
    // =====================================================================

    [Fact]
    public void ConsumeAtStep19a_ShouldConsumeBothPetAndBossCollections()
    {
        // GAME_STATE.md §2.3.1: the same schema and the same lifecycle serve
        // PetState.StatusEffects[] and BossState.StatusEffects[], so one step 19a
        // pass consumes both.
        var state = BattleStateWithBossEffects(
            petEffects: [Burn(2)],
            bossState: TestBossState() with { ActiveStatusEffects = [Stun(2)] });

        var after = StatusEffectLifecycle.ConsumeAtStep19a(state);

        Assert.Equal(1, after.PetState.ActiveStatusEffects[0].RemainingTurns);
        Assert.Equal(1, after.BossState.ActiveStatusEffects[0].RemainingTurns);
    }

    [Fact]
    public void ConsumeAtStep19a_ShouldChangeNothingElseInTheState()
    {
        // GAME_STATE.md §5.1.1 item 9: the consumption is one intermediate value of
        // the resolution; the pass mutates the two collections (and the Stun-driven
        // State) and carries every other member across unchanged. It advances no
        // counter: Turn and Sequence are not this operation's to write.
        var state = BattleStateWithBossEffects(
            petEffects: [Burn(2)],
            bossState: TestBossState() with { SkillCooldown = 3 });

        var after = StatusEffectLifecycle.ConsumeAtStep19a(state);

        Assert.Equal(state.Turn, after.Turn);
        Assert.Equal(state.Sequence, after.Sequence);
        Assert.Equal(state.BattleId, after.BattleId);
        Assert.Equal(state.RngSeed, after.RngSeed);
        Assert.Equal(state.BossState.HP, after.BossState.HP);
        Assert.Equal(state.BossState.SkillCharge, after.BossState.SkillCharge);

        // GAME_STATE.md §5.1.1 item 2 / COMBAT_RULES.md §5.3.4: SkillCooldown's
        // "decrements by 1 at each Turn increment" rule is a separate Boss Skill
        // counter and is NOT adopted by the Status Effect countdown.
        Assert.Equal(3, after.BossState.SkillCooldown);

        // The Pet's combat stats are likewise untouched.
        Assert.Equal(state.PetState.HP, after.PetState.HP);
        Assert.Equal(state.PetState.ATK, after.PetState.ATK);
        Assert.Equal(state.PetState.Power, after.PetState.Power);
    }

    // =====================================================================
    // Shield apply / refresh / depletion — COMBAT_RULES.md §4 items 2–5
    // (TASK-102)
    //
    // Shield is the trigger-based instance (§2.3.1 item 3), so it does not go
    // through Apply's "set RemainingTurns" operation. The refresh is the same
    // documented operation on the other duration model: one instance per
    // identity, refreshed in place, magnitude REPLACED and never summed.
    // =====================================================================

    /// <summary>
    /// A Shield instance of the given absorption pool, on the player's side —
    /// the Shield Basic Card's and Huyền Quy's Passive's shared effect identity
    /// (<c>COMBAT_RULES.md</c> §4 item 3).
    /// </summary>
    private static StatusEffect ShieldPool(int magnitude) =>
        StatusEffect.TriggerBased(
            "Shield", StatusEffectType.Shield, StatusEffectSource.Player, magnitude,
            StatusEffect.ShieldDepletedCondition);

    [Fact]
    public void ApplyShield_WithNoActiveShield_ShouldAppendOneInstanceOfThatMagnitude()
    {
        // COMBAT_RULES.md §4 item 3: applying a Shield to an entity that has none
        // produces one active Shield of the applied magnitude.
        var applied = StatusEffectLifecycle.ApplyShield([], ShieldPool(200));

        Assert.Single(applied);
        Assert.Equal(200, applied[0].Magnitude);
        Assert.Equal(StatusEffectType.Shield, applied[0].Type);
        Assert.Equal(StatusEffect.ShieldDepletedCondition, applied[0].ExpiryCondition);
    }

    [Fact]
    public void ApplyShield_WhileActive_ShouldRefreshInPlaceAndSetTheNewMagnitude()
    {
        // COMBAT_RULES.md §4 item 3: applying a Shield while one is active
        // "refreshes that existing Shield — the refreshed pool is set to the
        // magnitude of the new application". 200 then 350 leaves ONE instance at
        // 350 — not two instances, and not the sum 550.
        var first = StatusEffectLifecycle.ApplyShield([], ShieldPool(200));
        var refreshed = StatusEffectLifecycle.ApplyShield(first, ShieldPool(350));

        Assert.Single(refreshed);
        Assert.Equal(350, refreshed[0].Magnitude);
        Assert.NotEqual(550, refreshed[0].Magnitude);
    }

    [Fact]
    public void ApplyShield_RefreshWithASmallerMagnitude_ShouldSetNotMaximize()
    {
        // COMBAT_RULES.md §4 item 3 fixes the refreshed value deterministically:
        // it is SET to the new application's magnitude, and is explicitly "not
        // summed, not maximized, and not compared". 350 then 100 is 100.
        var refreshed = StatusEffectLifecycle.ApplyShield(
            StatusEffectLifecycle.ApplyShield([], ShieldPool(350)),
            ShieldPool(100));

        Assert.Single(refreshed);
        Assert.Equal(100, refreshed[0].Magnitude);
    }

    [Fact]
    public void ApplyShield_RefreshWithAnEqualMagnitude_ShouldBeIdempotent()
    {
        // The equal case of §4 item 3 is deterministic and idempotent: setting the
        // same magnitude twice leaves the same single instance.
        var once = StatusEffectLifecycle.ApplyShield([], ShieldPool(200));
        var twice = StatusEffectLifecycle.ApplyShield(once, ShieldPool(200));

        Assert.Single(twice);
        Assert.Equal(once[0], twice[0]);
    }

    [Fact]
    public void ApplyShield_FromTwoDifferentSources_ShouldRefreshRatherThanFormASecondPool()
    {
        // COMBAT_RULES.md §4 item 3: two different Shield sources (the Shield
        // Basic Card, a Shield-granting Passive) share the effect identity
        // "Shield", so the second "refresh[es] one another rather than forming a
        // second pool". The identity is what de-duplicates; the Source label
        // differs and does not.
        var fromCard = StatusEffectLifecycle.ApplyShield([], ShieldPool(200));

        var fromPassive = StatusEffectLifecycle.ApplyShield(
            fromCard,
            StatusEffect.TriggerBased(
                "Shield", StatusEffectType.Shield, StatusEffectSource.Player, 150,
                StatusEffect.ShieldDepletedCondition));

        Assert.Single(fromPassive);
        Assert.Equal(150, fromPassive[0].Magnitude);
    }

    [Fact]
    public void ApplyShield_ShouldNotDisturbOtherInstancesOrTheirOrder()
    {
        // GAME_STATE.md §2.3.2 item 6 / §2.3.1 item 10: the refresh replaces in
        // place, so an unrelated apply/refresh neither reorders the array nor
        // touches another instance.
        var effects = new[] { Burn(2), Root(2) };
        var withShield = StatusEffectLifecycle.ApplyShield(effects, ShieldPool(200));
        var refreshed = StatusEffectLifecycle.ApplyShield(withShield, ShieldPool(300));

        Assert.Equal(["Burn", "Root", "Shield"], refreshed.Select(e => e.Id));
        Assert.Equal(2, Turns(refreshed, "Burn"));
        Assert.Equal(2, Turns(refreshed, "Root"));
    }

    [Fact]
    public void ApplyShield_ShouldRejectANonPositivePool()
    {
        // COMBAT_RULES.md §4 item 4 / GAME_STATE.md §2.3.1 item 8: a Shield at
        // exactly 0 is removed in the resolution that produced the 0 and is never
        // observable, so a 0-magnitude Shield is not an applicable state.
        Assert.Throws<ArgumentException>(
            () => StatusEffectLifecycle.ApplyShield([], ShieldPool(0)));

        Assert.Throws<ArgumentException>(
            () => StatusEffectLifecycle.ApplyShield([], ShieldPool(-50)));
    }

    [Fact]
    public void ApplyShield_ShouldRejectATurnBasedInstance()
    {
        // GAME_STATE.md §2.3.1 item 3 gives Shield the trigger-based model, and a
        // Turn-based instance's refresh is Apply's "set RemainingTurns" — a
        // different operation. The guard keeps each model on its own path.
        Assert.Throws<ArgumentException>(
            () => StatusEffectLifecycle.ApplyShield([], Burn(2)));
    }

    [Fact]
    public void RemoveDepletedShield_AtZeroMagnitude_ShouldRemoveTheShield()
    {
        // COMBAT_RULES.md §4 item 4: "the pool reaches exactly 0, and the Shield is
        // removed in that same resolution — a committed Shield value of 0 is never
        // observable as an active Shield." §2.3.1 item 8: absence means "not
        // active".
        var remaining = StatusEffectLifecycle.RemoveDepletedShield([ShieldPool(0)]);

        Assert.Empty(remaining);
        Assert.Null(Find(remaining, "Shield"));
    }

    [Fact]
    public void RemoveDepletedShield_WithAPositivePool_ShouldKeepTheShield()
    {
        // The trigger fires on depletion only (§4 item 4). A Shield still carrying
        // a pool is active and is not dropped by a call that did not deplete it.
        var remaining = StatusEffectLifecycle.RemoveDepletedShield([ShieldPool(1)]);

        Assert.Single(remaining);
        Assert.Equal(1, remaining[0].Magnitude);
    }

    [Fact]
    public void RemoveDepletedShield_ShouldLeaveOtherInstancesAlone()
    {
        // §4 item 4's trigger removes the depleted Shield and nothing else — the
        // Burn and Root instances are not this trigger's concern.
        var remaining = StatusEffectLifecycle.RemoveDepletedShield(
            [Burn(2), ShieldPool(0), Root(2)]);

        Assert.Equal(["Burn", "Root"], remaining.Select(e => e.Id));
    }

    [Fact]
    public void ShieldPool_ShouldReadTheActiveShieldsMagnitudeOrZero()
    {
        // COMBAT_RULES.md §4 items 2–3: the active Shield's magnitude IS the
        // absorption pool the Damage Pipeline consumes — there is no separate
        // Shield field (GAME_STATE.md §0 item 5). With no Shield, or with a
        // depleted one, the pool is 0.
        Assert.Equal(200, StatusEffectLifecycle.ShieldPool([ShieldPool(200)]));
        Assert.Equal(200, StatusEffectLifecycle.ShieldPool([Burn(2), ShieldPool(200)]));

        Assert.Equal(0, StatusEffectLifecycle.ShieldPool([]));
        Assert.Equal(0, StatusEffectLifecycle.ShieldPool([Burn(2)]));
        Assert.Equal(0, StatusEffectLifecycle.ShieldPool([ShieldPool(0)]));
    }

    [Fact]
    public void Shield_ShouldNotBeDecrementedByTheStep19aCountdown()
    {
        // GAME_STATE.md §5.1.1 item 7 / COMBAT_RULES.md §4: Shield is trigger-based
        // and acquires no duration, so the step 19a pass must leave it alone — its
        // removal is the depletion trigger, not a Turn countdown.
        var consumed = StatusEffectLifecycle.ConsumeAtStep19a([ShieldPool(200)]);

        Assert.Single(consumed);
        Assert.Equal(200, consumed[0].Magnitude);
        Assert.Null(consumed[0].RemainingTurns);
    }

    // =====================================================================
    // Fixtures
    // =====================================================================

    private static BossState TestBossState() =>
        BossState.Initial(
            new GameServer.Domain.Bosses.BossId("boss-hoa-long"),
            GameServer.Domain.Elements.Element.Hoa,
            maxHp: 5000,
            atk: 100,
            def: 50,
            passiveId: new GameServer.Domain.Passives.PassiveId("boss-hoa-long-rage"),
            passiveThreshold: 5);

    /// <summary>
    /// A battle state carrying the given Status Effect collections, for the
    /// state-level assertions of <c>GAME_STATE.md</c> §5.1.1 items 8 and 9.
    /// </summary>
    private static BattleState BattleStateWithBossEffects(
        StatusEffect[]? petEffects = null,
        BossState? bossState = null)
    {
        var state = BattleState.Create(
            "battle-1",
            rngSeed: 42UL,
            new GameServer.Domain.Players.PlayerId("player-1"),
            GameServer.Domain.Battle.PetState.AtBattleCreation(
                new GameServer.Domain.Pets.PetId("pet-1"),
                GameServer.Domain.Elements.Element.Hoa,
                new GameServer.Domain.Passives.PassiveId("passive-1"),
                passiveThreshold: 5,
                passiveResetOverride: null,
                equippedRelics: null,
                equippedCards: null,
                statusEffects: petEffects ?? []),
            bossState ?? TestBossState());

        return state;
    }
}
