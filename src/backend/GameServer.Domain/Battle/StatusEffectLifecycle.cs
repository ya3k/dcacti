namespace GameServer.Domain.Battle;

/// <summary>
/// The <c>StatusEffects[]</c> state mutation lifecycle — apply, refresh, consume,
/// and expire (<c>GAME_STATE.md</c> §5.1.1).
///
/// <code>
/// Apply   RemainingTurns = duration   (§5.1.1 item 1 — a "set", not an increment)
/// Refresh RemainingTurns = duration   (§5.1.1 item 1 — the same mechanism, DR3)
/// Consume RemainingTurns -= 1         (§5.1.1 item 2 — once per resolved Turn)
/// Expire  RemainingTurns == 0 → remove (§5.1.1 item 4 — a removal, not a stored 0)
/// </code>
///
/// <b>This type owns the state mutation. It does not own the rule.</b>
/// <c>GAME_STATE.md</c> §5.1.1 states it plainly: "This subsection owns the
/// <b>state mutation</b> of <c>StatusEffects[]</c> instances (§2.3.1). The
/// gameplay rule it implements — when one Turn of duration is consumed — is owned
/// by <c>COMBAT_RULES.md</c> §5.3 and is <b>not</b> restated or reinterpreted
/// here." The resolution position it runs at is <c>GAME_RULES.md</c> §17 step 19a.
/// Nothing below decides a rule; every operation implements a numbered item of
/// §5.1.1 and cites it.
///
/// <b>Every operation is a pure, deterministic function.</b> There is no RNG, no
/// clock, no I/O, no framework dependency, and no ambient state
/// (<c>ARCHITECTURE.md</c> §2.1, <c>TDD.md</c> §6): a collection goes in and the
/// resulting collection comes out. The caller sequences the calls and performs
/// the one post-resolution write-back (<c>GAME_STATE.md</c> §5.1, §5.1.1 item 9);
/// neither happens here.
///
/// <b>What this type deliberately does not do.</b>
/// <list type="bullet">
/// <item>It does not tick Burn's damage. §17 step 19a's damage-over-time tick runs
/// through the Damage Pipeline (<c>COMBAT_RULES.md</c> §5.2 item 3) and is a
/// separate concern; what is implemented here is the <b>duration countdown and
/// expiry</b>. Burn's schedule and magnitude are unchanged
/// (<c>BOSS_RULES.md</c> §6.3.1 item 1, <c>COMBAT_RULES.md</c> §5.3.4).</item>
/// <item>It does not apply gameplay effects. No Boss Skill, Card, Passive, or any
/// other producer creates instances here — only the operations that act on
/// instances a producer supplied.</item>
/// <item>It does not serialize. The runtime record's round trip is a separate
/// task's obligation (<c>GAME_STATE.md</c> §2.3.2), and this type owns only the
/// apply/refresh/consume/expire mutations on the collection.</item>
/// <item>It does not adopt <c>BossState.SkillCooldown</c>'s "decrements by 1 at
/// each Turn increment" rule. §5.1.1 item 2 excludes it explicitly and
/// <c>COMBAT_RULES.md</c> §5.3.4 repeats the exclusion; the Turn increment does
/// <b>not</b> mutate <see cref="StatusEffect.RemainingTurns"/>.</item>
/// </list>
/// </summary>
public static class StatusEffectLifecycle
{
    /// <summary>
    /// Applies or refreshes a Status Effect instance on a collection
    /// (<c>GAME_STATE.md</c> §5.1.1 item 1; <c>COMBAT_RULES.md</c> §5.3 DR1, DR3,
    /// DR4).
    ///
    /// <b>Apply and Refresh are the same operation.</b> DR3 makes them one
    /// mechanism — "an initial Apply is not semantically different from a
    /// Refresh; Refresh simply re-executes the same 'set remaining' operation on
    /// an already-active effect instance". §5.1.1 item 1 states the mutation:
    /// applying an effect that is not active appends one element with
    /// <c>RemainingTurns = duration</c>, and applying one that <i>is</i> active
    /// refreshes that existing instance by re-setting <c>RemainingTurns =
    /// duration</c> — it does not append a second element and does not add to the
    /// current value.
    ///
    /// <b>At most one instance per identity.</b> §2.3.1 item 6 makes this the
    /// collection's invariant: "the array holds at most one element per
    /// <c>Id</c>". A refresh therefore replaces in place, preserving the existing
    /// element's position so the array is not reordered by an unrelated operation
    /// (§2.3.2 item 6 preserves order for round-trip fidelity, and §2.3.1 item 10
    /// makes position non-semantic — so preserving it is the behavior that keeps
    /// both true).
    ///
    /// <b>Reapplying within the same Turn resets and consumes nothing extra.</b>
    /// DR4: same-Turn reapplication "resets <c>remaining</c> to the new duration
    /// value and does NOT trigger an additional consumption in that Turn. Only
    /// step 19a consumes." That falls out of this operation being a pure set: it
    /// performs no decrement at all, so any number of applies and refreshes before
    /// <see cref="ConsumeAtStep19a"/> still leave exactly one decrement to that
    /// pass (DR2, §5.1.1 item 3).
    ///
    /// <b>The magnitude is not stacked.</b> §5.1.1 item 1 and
    /// <c>COMBAT_RULES.md</c> §5.2 item 2 fix reapplication as "refresh duration,
    /// do not stack magnitude", so a refresh carries the new instance's magnitude
    /// and never sums it with the old one. A magnitude modifier on an existing
    /// effect is a different, explicitly-authored case (§5.2 item 2's "Burning
    /// Curse") and is not a stacking behavior this operation invents.
    /// </summary>
    /// <param name="effects">
    /// The entity's current active instances (<c>GAME_STATE.md</c> §2.3.1). It is
    /// the collection as held — always a collection, never <c>null</c>, because
    /// §2.3.2 item 1 makes "no active effect" an <b>empty array</b> and states the
    /// collection "always exists … it is never omitted and never null".
    /// </param>
    /// <param name="effect">
    /// The instance to apply or refresh. Its <see cref="StatusEffect.Id"/> is the
    /// identity item 6 de-duplicates on, and its
    /// <see cref="StatusEffect.RemainingTurns"/> is the duration this operation
    /// sets — which is why the instance is built by
    /// <see cref="StatusEffect.TurnBased"/> with the applied or refreshed duration
    /// value already in place (DR1).
    /// </param>
    /// <returns>
    /// The resulting collection: the same elements in the same order, with the
    /// matching instance replaced in place, or <paramref name="effect"/> appended
    /// when no active instance carried its <see cref="StatusEffect.Id"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="effects"/> is <c>null</c>. A <c>null</c> collection is not a
    /// representable state (§2.3.2 item 1), so it is rejected rather than treated
    /// as empty.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="effect"/> is trigger-based. A <c>Shield</c> instance carries
    /// an <see cref="StatusEffect.ExpiryCondition"/> and no Turn countdown, so
    /// there is no duration for this operation to set; §5.1.1 item 7 keeps such an
    /// instance on its own documented trigger path instead.
    /// </exception>
    public static StatusEffect[] Apply(
        IReadOnlyList<StatusEffect> effects,
        StatusEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effects);

        // §5.1.1 item 1 / DR3: Apply and Refresh are the same "set remaining =
        // duration" operation, so there must be a duration to set. A trigger-based
        // instance has none (ExpiryCondition model, §2.3.1 item 3) and §5.1.1
        // item 7 keeps it off the countdown entirely.
        if (!effect.UsesTurnCountdown)
        {
            throw new ArgumentException(
                "Apply and Refresh set RemainingTurns = duration (GAME_STATE.md §5.1.1 "
                + "item 1, COMBAT_RULES.md §5.3 DR1/DR3), so the instance must be "
                + "Turn-based. A trigger-based instance expires by its own condition "
                + "(§5.1.1 item 7).",
                nameof(effect));
        }

        var result = new StatusEffect[effects.Count + 1];
        var replaced = false;

        for (var index = 0; index < effects.Count; index++)
        {
            var existing = effects[index];

            // §2.3.1 item 6: "the array holds at most one element per Id". An
            // apply of an already-active identity refreshes THAT instance rather
            // than appending a second one, and in place — so the operation neither
            // grows the collection nor reorders it (item 10, §2.3.2 item 6).
            if (!replaced && string.Equals(existing.Id, effect.Id, StringComparison.Ordinal))
            {
                result[index] = effect;
                replaced = true;
                continue;
            }

            result[index] = existing;
        }

        if (!replaced)
        {
            result[effects.Count] = effect;
        }

        return replaced ? result[..^1] : result;
    }

    /// <summary>
    /// Applies or refreshes a trigger-based Standalone Shield instance — the
    /// absorption pool <c>COMBAT_RULES.md</c> §4 items 2–3 defines.
    ///
    /// <b>Shield has no duration, so it does not go through
    /// <see cref="Apply"/>.</b> §2.3.1 item 3 makes <c>Shield</c> the
    /// trigger-based type ("until Shield is depleted") and §5.1.1 item 7 keeps it
    /// off the step 19a countdown, so there is no <c>RemainingTurns</c> for that
    /// operation to set. This is the same documented operation applied to the
    /// other model: an instance that is not active is appended, and one that
    /// <i>is</i> active is refreshed in place.
    ///
    /// <b>Refresh replaces the magnitude; it never accumulates.</b>
    /// <c>COMBAT_RULES.md</c> §4 item 3 is explicit: the refreshed pool is "set to
    /// the magnitude of the new application", it is "not summed with the existing
    /// value", and "no additive accumulation occurs under any MVP condition".
    /// Because <see cref="StatusEffect.Id"/> is the uniqueness key (§2.3.1
    /// item 6), the replaced element <i>is</i> the refresh — the resulting
    /// collection holds exactly one <c>"Shield"</c> instance carrying the new
    /// magnitude, whether the new magnitude is smaller, equal, or larger than the
    /// old one (set, not maximized, not compared). Two different Shield sources
    /// therefore refresh one another rather than forming a second pool (§4
    /// item 3), and this method needs no notion of a source to achieve that.
    ///
    /// <b>The instance's own <c>Magnitude</c> is the pool.</b> §2.3.1 item 2 makes
    /// it the applied value whose meaning — an absorption pool — is owned by
    /// <c>COMBAT_RULES.md</c> §4 and not interpreted by the state model. The
    /// consumption of that pool is the Damage Pipeline's absorption step
    /// (<see cref="GameServer.Domain.Combat.DamagePipeline"/>), and the removal at
    /// exactly 0 is <see cref="RemoveDepletedShield"/>'s.
    /// </summary>
    /// <param name="effects">
    /// The entity's current active instances (<c>GAME_STATE.md</c> §2.3.1). Always
    /// a collection, never <c>null</c> (§2.3.2 item 1).
    /// </param>
    /// <param name="effect">
    /// The Shield instance to apply or refresh. It must be trigger-based with
    /// <see cref="StatusEffectType.Shield"/> — <see cref="StatusEffect.TriggerBased"/>
    /// is the only construction path, so that pairing holds by construction.
    /// </param>
    /// <returns>
    /// The resulting collection: the same elements in the same order, with the
    /// matching <c>"Shield"</c> instance replaced in place by
    /// <paramref name="effect"/>, or <paramref name="effect"/> appended when no
    /// active instance carried its <see cref="StatusEffect.Id"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="effects"/> is <c>null</c> (§2.3.2 item 1).
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="effect"/> is Turn-based, or its magnitude is not a positive
    /// pool. A Turn-based instance has no trigger-based expiry for
    /// <see cref="RemoveDepletedShield"/> to honour and belongs to
    /// <see cref="Apply"/>; and a Shield of magnitude <c>0</c> would be the
    /// committed-zero Shield <c>COMBAT_RULES.md</c> §4 item 4 states is never
    /// observable (§2.3.1 item 8), so it is rejected rather than applied and
    /// immediately removed.
    /// </exception>
    public static StatusEffect[] ApplyShield(
        IReadOnlyList<StatusEffect> effects,
        StatusEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effects);

        // §2.3.1 item 3 / §5.1.1 item 7: the trigger-based model is Shield's, and
        // it is the model this operation applies. A Turn-based instance's refresh
        // is Apply's "set RemainingTurns" and is not this operation's.
        if (effect.UsesTurnCountdown || effect.Type != StatusEffectType.Shield)
        {
            throw new ArgumentException(
                "ApplyShield applies a trigger-based Shield instance (GAME_STATE.md "
                + "§2.3.1 item 3, COMBAT_RULES.md §4 item 3). A Turn-based instance "
                + "is refreshed by StatusEffectLifecycle.Apply instead.",
                nameof(effect));
        }

        // COMBAT_RULES.md §4 item 4 / GAME_STATE.md §2.3.1 item 8: a Shield at
        // exactly 0 is removed in the resolution that produced the 0 and is never
        // an observable committed value, so there is no state in which applying a
        // 0-magnitude Shield is meaningful — it would be immediately depleted.
        if (effect.Magnitude <= 0)
        {
            throw new ArgumentException(
                "A Shield's Magnitude is its absorption pool and must be positive "
                + "(COMBAT_RULES.md §4 item 2); a pool at 0 is removed in the same "
                + "resolution and is never observable (COMBAT_RULES.md §4 item 4, "
                + "GAME_STATE.md §2.3.1 item 8).",
                nameof(effect));
        }

        var result = new StatusEffect[effects.Count + 1];
        var replaced = false;

        for (var index = 0; index < effects.Count; index++)
        {
            var existing = effects[index];

            // §2.3.1 item 6 / COMBAT_RULES.md §4 item 3: an apply of an
            // already-active "Shield" identity REFRESHES that instance — the new
            // magnitude replaces the old, in place, with no second element and no
            // summation. Item 10 / §2.3.2 item 6 keep the position stable.
            if (!replaced && string.Equals(existing.Id, effect.Id, StringComparison.Ordinal))
            {
                result[index] = effect;
                replaced = true;
                continue;
            }

            result[index] = existing;
        }

        if (!replaced)
        {
            result[effects.Count] = effect;
        }

        return replaced ? result[..^1] : result;
    }

    /// <summary>
    /// Removes a Shield whose absorption pool reached exactly 0 — the depletion
    /// trigger <c>COMBAT_RULES.md</c> §4 item 4 defines, evaluated during the
    /// damage resolution that depleted it.
    ///
    /// <b>The removal happens in the same resolution, not later.</b> §4 item 4:
    /// "When damage reduces the pool, the pool reaches exactly 0, and the Shield
    /// is removed in that same resolution — a committed Shield value of 0 is never
    /// observable as an active Shield." §2.3.1 item 8 states the same for state:
    /// absence of the element means "not active", never "active with 0".
    ///
    /// <b>This is the <c>ShieldDepleted</c> trigger, and it is not an event.</b>
    /// §4 item 4 names the <c>"ShieldDepleted"</c> expiry condition
    /// (<see cref="StatusEffect.ShieldDepletedCondition"/>) as "the trigger that
    /// removes the instance" and states outright that "<c>ShieldDepleted</c> is
    /// <b>not</b> a Battle Event". §5.1.1 item 10 repeats that the lifecycle adds
    /// no event, no payload member, and no SignalR method. Nothing is emitted here.
    ///
    /// <b>Only a depleted pool is removed.</b> A Shield still carrying magnitude is
    /// left active — its pool's consumption is the pipeline's absorption step, not
    /// this operation — and an entity with no Shield is unchanged. A pool above 0
    /// is therefore never removed by a call that did not deplete it, and a Shield
    /// that is merely present is never dropped.
    /// </summary>
    /// <param name="effects">
    /// The entity's active instances after the damage resolution absorbed from the
    /// pool (<c>GAME_STATE.md</c> §2.3.1). Always a collection, never <c>null</c>
    /// (§2.3.2 item 1).
    /// </param>
    /// <returns>
    /// The resulting collection with every Shield instance whose magnitude is 0
    /// removed, the remaining elements in their existing order. Every non-Shield
    /// instance and every Shield still carrying a positive pool is carried across
    /// unchanged.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="effects"/> is <c>null</c> (§2.3.2 item 1).
    /// </exception>
    public static StatusEffect[] RemoveDepletedShield(IReadOnlyList<StatusEffect> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);

        var remaining = new List<StatusEffect>(effects.Count);

        foreach (var effect in effects)
        {
            // COMBAT_RULES.md §4 item 4: exactly 0 is the depletion. A Shield still
            // carrying a positive pool is active and stays; any other instance is
            // not this trigger's concern at all.
            var depleted = effect.Type == StatusEffectType.Shield
                && effect.Magnitude <= 0;

            if (!depleted)
            {
                remaining.Add(effect);
            }
        }

        return [.. remaining];
    }
    /// <summary>
    /// Runs the <c>GAME_RULES.md</c> §17 step 19a pass: one Turn of duration is
    /// consumed for every active Turn-countdown instance, and instances that reach
    /// <c>0</c> are removed in the same pass (<c>GAME_STATE.md</c> §5.1.1
    /// items 2–5; <c>COMBAT_RULES.md</c> §5.3 DR2, DR5).
    ///
    /// <code>
    /// for each active instance using the Turn countdown:
    ///     RemainingTurns -= 1
    ///     if RemainingTurns == 0: remove the instance
    /// </code>
    ///
    /// <b>Exactly one decrement per resolved Turn.</b> §5.1.1 item 2: "Every
    /// active Turn-countdown instance loses exactly one Turn of duration at step
    /// 19a, regardless of how many apply/refresh operations occurred earlier in
    /// that same Turn." This method consumes one Turn and is called once per
    /// resolved Turn — it is not a loop over Turns, and no other resolution step
    /// mutates the counter (DR2, §5.1.1 item 2's exclusion of the Turn increment).
    ///
    /// <b>Apply timing within the Turn does not change the count.</b> §5.1.1
    /// item 3 and DR6: an instance applied or refreshed before step 19a is
    /// decremented at that same Turn's step 19a — once. A same-Turn reapplication
    /// produces no second decrement (DR4), because <see cref="Apply"/> is a pure
    /// set and this pass is the only consumer.
    ///
    /// <b>Expiry is a removal, not a stored zero.</b> §5.1.1 item 4 and DR5: when
    /// the counter reaches <c>0</c> the instance is removed "in the same
    /// resolution", so an instance at <c>0</c> is "never observable in a committed
    /// state" and absence means "not active" (§2.3.1 item 8). The decrement and the
    /// resulting removal therefore happen in one pass and only once (§5.1.1
    /// item 5): a <c>duration = 1</c> instance applied during Turn N is both
    /// consumed and removed at Turn N's step 19a and is inactive from Turn N+1.
    ///
    /// <b>Trigger-based instances are not touched.</b> §5.1.1 item 7: an instance
    /// carrying <see cref="StatusEffect.ExpiryCondition"/> instead of
    /// <see cref="StatusEffect.RemainingTurns"/> "is not touched by the step 19a
    /// countdown; it is removed by its own documented trigger … The step 19a pass
    /// must not invent a duration for it." Such instances are carried across
    /// unchanged.
    ///
    /// <b>The pass order is deterministic and is not array order.</b> §5.1.1
    /// item 6 fixes processing to "<c>Id</c> in ordinal ascending order", so the
    /// resulting state is reproducible for a given input state and does not depend
    /// on array insertion order (§2.3.1 item 11, <c>TDD.md</c> §6). The order is
    /// part of the contract precisely because <c>GAME_STATE.md</c> §2.3.1 item 10
    /// makes array position non-semantic and it "must not become one by accident".
    /// The result is written back in that same deterministic order, so the
    /// collection's order after a pass is a function of the contract rather than
    /// of how the elements were previously arranged.
    /// </summary>
    /// <param name="effects">
    /// The entity's active instances at the moment step 19a runs. It is the
    /// collection as held — always a collection, never <c>null</c>
    /// (<c>GAME_STATE.md</c> §2.3.2 item 1).
    /// </param>
    /// <returns>
    /// The resulting collection: every trigger-based instance unchanged, every
    /// Turn-countdown instance decremented by one, and every instance whose
    /// counter reached <c>0</c> removed. The elements are ordered by
    /// <see cref="StatusEffect.Id"/> ordinal ascending (§5.1.1 item 6). An empty
    /// input yields an empty result.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="effects"/> is <c>null</c>. A <c>null</c> collection is not a
    /// representable state (§2.3.2 item 1), so it is rejected rather than treated
    /// as empty.
    /// </exception>
    public static StatusEffect[] ConsumeAtStep19a(IReadOnlyList<StatusEffect> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);

        // §5.1.1 item 6: the pass processes instances "by Id in ordinal ascending
        // order", so the order is fixed by the contract and never by whatever
        // order the array happens to hold (§2.3.1 item 11).
        var ordered = effects
            .Select((effect, index) => (effect, index))
            .OrderBy(entry => entry.effect.Id, StringComparer.Ordinal)
            .ThenBy(entry => entry.index)
            .Select(entry => entry.effect)
            .ToArray();

        var consumed = new List<StatusEffect>(ordered.Length);

        foreach (var effect in ordered)
        {
            // §5.1.1 item 7: a trigger-based instance is not decremented and is
            // not removed here — its own documented trigger ends it, and this pass
            // "must not invent a duration for it".
            if (!effect.UsesTurnCountdown)
            {
                consumed.Add(effect);
                continue;
            }

            // §5.1.1 item 2 / DR2: exactly one decrement per resolved Turn.
            var remaining = effect.RemainingTurns!.Value - 1;

            // §5.1.1 items 4–5 / DR5: expiry is a removal in the same pass, so 0
            // is never stored and never observable (§2.3.1 item 8).
            if (remaining <= 0)
            {
                continue;
            }

            consumed.Add(effect with { RemainingTurns = remaining });
        }

        return [.. consumed];
    }

    /// <summary>
    /// The step 19a pass applied to a whole <c>BattleState</c>, including the
    /// documented <c>BossState.State</c> agreement
    /// (<c>GAME_STATE.md</c> §5.1.1 item 8, §2.4.5).
    ///
    /// <b>Why the Boss's <c>State</c> is part of this pass.</b> §5.1.1 item 8:
    /// "When a Stun instance expires at step 19a, <c>BossState.State</c> reverts to
    /// <c>Idle</c> in the same resolution, so <c>State</c> never disagrees with the
    /// presence of the Stun instance (§2.4.5). <c>RemainingTurns</c> is
    /// authoritative; <c>State</c> reflects it." The reversion is therefore a
    /// consequence of the same pass rather than a second mechanism, and it happens
    /// in the same resolution — not on a later Turn.
    ///
    /// <b>It reverts only what the Stun owned.</b> The rule is stated for the Stun
    /// instance, and §2.4.5 makes <c>Stunned</c> the state that instance tracks.
    /// A Boss that is <see cref="BossStateKind.Enraged"/> is not a Stun and is not
    /// reverted: <c>BOSS_RULES.md</c> §5 item 4 makes Enrage "permanent … no timer,
    /// no duration field", so it is untouched. A Boss whose <c>Stunned</c> state
    /// has no Stun instance — a state the contract does not produce — is likewise
    /// left alone rather than repaired, because no rule authorizes this pass to
    /// invent the instances that would justify a transition.
    ///
    /// <b>Nothing here is published.</b> §5.1.1 item 10: the lifecycle adds no
    /// event, no payload member, and no SignalR method. The returned state is the
    /// value the caller writes back in the single post-resolution write-back
    /// (item 9).
    /// </summary>
    /// <param name="state">
    /// The battle state whose <c>PetState.StatusEffects[]</c> and
    /// <c>BossState.StatusEffects[]</c> are consumed, and whose
    /// <c>BossState.State</c> follows an expired Stun (§2.3, §2.4).
    /// </param>
    /// <returns>
    /// The state after one step 19a pass: both collections consumed by
    /// <see cref="ConsumeAtStep19a"/>, and the Boss's <c>State</c> reverted to
    /// <see cref="BossState.InitialState"/> when a Stun instance expired in that
    /// pass. Every other member is carried across unchanged.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is <c>null</c>.</exception>
    public static BattleState ConsumeAtStep19a(BattleState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // §2.3.2 item 1: the collection always exists, so the non-null accessor is
        // the documented reading and null is not a representable state.
        var petEffects = ConsumeAtStep19a(state.PetState.ActiveStatusEffects);
        var bossEffects = ConsumeAtStep19a(state.BossState.ActiveStatusEffects);

        var bossState = state.BossState with { ActiveStatusEffects = bossEffects };

        // §5.1.1 item 8 / §2.4.5: State reflects the Stun instance's presence, and
        // the presence check is made against the POST-pass collection — the state
        // the pass produced is what the reversion must agree with.
        if (bossState.State == BossStateKind.Stunned && !HoldsStun(bossEffects))
        {
            bossState = bossState with { State = BossState.InitialState };
        }

        return state with
        {
            PetState = state.PetState with { ActiveStatusEffects = petEffects },
            BossState = bossState,
        };
    }

    /// <summary>
    /// The absorption pool of an entity's active Shield, or <c>0</c> when it holds
    /// none — the value <c>COMBAT_RULES.md</c> §4 items 2–5 consume.
    ///
    /// <b>It reads the existing instance; it is not a second representation.</b>
    /// §2.3.1 item 2 makes <see cref="StatusEffect.Magnitude"/> the applied value,
    /// and for <c>Type = Shield</c> the owning rule document gives that value the
    /// meaning "absorption pool" (§4 item 2). No <c>ShieldPoints</c> member exists
    /// and none is needed (<c>GAME_STATE.md</c> §0 item 5).
    ///
    /// <b>At most one Shield is active per entity</b> (§4 item 3), so this returns
    /// a single number and there is no pool ordering to resolve (§4 item 2). The
    /// scan is by identity <see cref="StatusEffect.ShieldDepletedCondition"/>'s
    /// instance — the documented <c>"Shield"</c> identity — and by
    /// <see cref="StatusEffectType.Shield"/>, matching
    /// <see cref="HoldsStun"/>'s precedent for a trigger-based-instance lookup. A
    /// depleted pool is <c>0</c>, which is the same "nothing to absorb" reading as
    /// an absent Shield: §4 item 4 removes a pool at exactly 0 rather than storing
    /// it (§2.3.1 item 8).
    /// </summary>
    /// <param name="effects">The entity's active instances (<c>GAME_STATE.md</c> §2.3.1).</param>
    /// <returns>The active Shield's magnitude, or <c>0</c> when there is none.</returns>
    public static int ShieldPool(IReadOnlyList<StatusEffect> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);

        for (var index = 0; index < effects.Count; index++)
        {
            var effect = effects[index];

            if (effect.Type == StatusEffectType.Shield
                && string.Equals(effect.Id, ShieldId, StringComparison.Ordinal))
            {
                return effect.Magnitude > 0 ? (int)effect.Magnitude : 0;
            }
        }

        return 0;
    }

    /// <summary>
    /// The <c>EffectivePetATK</c> an owning Pet's own attack consumes — the
    /// <b>unified</b> composition of the permanent base ATK with every applicable
    /// Pet ATK modifier (<c>COMBAT_RULES.md</c> §5.6.1, §5.6.6).
    ///
    /// <b>This is §5.6.6's Step-1 value, not a second representation of ATK.</b>
    /// <c>COMBAT_RULES.md</c> §5.4.1 fixes the modifiers' consumption point at the
    /// <b>Player → Boss Damage Pipeline Step 1 <c>Attack</c> input</b>: the value
    /// returned here is passed as that argument and is never written back to
    /// <c>PetState.ATK</c>. §5.6.4 and §5.6.6 item 8 require the base stat to be
    /// untouched and the derived value to be used within one pipeline execution and
    /// discarded, which is exactly what a pure function returning an <c>int</c>
    /// expresses — no <c>EffectiveATK</c> member exists on any state type
    /// (<c>GAME_STATE.md</c> §2.3.1 item 12, §2.3).
    ///
    /// <b>It delegates; it does not author a formula.</b> §5.6.6 is the canonical
    /// composition model for <i>all</i> applicable Pet ATK modifiers — the
    /// Relic-sourced entries in <c>PetState.ATKModifiers[]</c> and the Turn-based
    /// <c>TargetStat = "ATK"</c> <c>BuffDebuff</c> entries in
    /// <c>PetState.StatusEffects[]</c> — including the case where only one of the
    /// two carriers holds anything (TASK-137 <b>D1</b>, TASK-138 <b>D1</b>/<b>D2</b>).
    /// §5.4.1's historical absolute-value calculation path is superseded/narrowed
    /// for ATK composition and is <b>not</b> implemented here: this method forwards
    /// to the one composition, <see cref="EffectivePetATK.Compose"/>, so there is
    /// exactly one formula in code, one signed sum, and one truncation point
    /// (<c>COMBAT_RULES.md</c> §5.6.6 items 2–5).
    ///
    /// <b>The percentage applies to the stat alone.</b> §5.6.1 item 3 and §5.4.1
    /// item 2 make the composed value the <i>ATK term only</i>: step 1's other
    /// contributions — the Skill/Card base value and the ATK-Gem-generated damage
    /// pool — are separate and are not modified by this rule. The caller therefore
    /// sums the returned value with the pool, so <c>ATK 100</c> with a pool of
    /// <c>40</c> at <c>−30%</c> yields Step 1 <c>= 70 + 40 = 110</c> and never
    /// <c>(100 + 40) × 70% = 98</c>.
    ///
    /// <b><c>PetState.ATK</c> is read and never written.</b> §5.4.4 and §5.6.4
    /// forbid overwriting it, forbid resetting it to a configuration default, and
    /// forbid an arithmetic inverse — an unconsumed modifier therefore needs no
    /// "restore" step, because nothing was ever changed.
    ///
    /// <b>Selection is by <c>Type</c> and <c>TargetStat</c>, never by <c>Id</c></b>
    /// (§5.4.5, §5.6.6 item 2), and an expired instance is already gone: §5.4.3
    /// makes activity follow the committed <c>StatusEffects[]</c> state, and §5.3
    /// DR5 / §2.3.1 item 8 remove an instance whose count reaches <c>0</c> in the
    /// same step-19a resolution, so the collection this method reads is already the
    /// active set.
    /// </summary>
    /// <param name="attack">
    /// The stored base value (<c>PetState.ATK</c>). It is read and never written:
    /// §5.4.4 and §5.6.4 forbid overwriting it and forbid resetting it to a
    /// configuration default.
    /// </param>
    /// <param name="atkModifiers">
    /// The applied, Battle-scoped Relic ATK modifiers
    /// (<c>GAME_STATE.md</c> §2.3.7), always a collection and never <c>null</c>
    /// (§2.3.7 item 6). Each entry is a signed percentage-point contribution and is
    /// summed with its own sign (<c>COMBAT_RULES.md</c> §5.6.6 item 2).
    /// </param>
    /// <param name="effects">
    /// The attacking entity's active instances (<c>GAME_STATE.md</c> §2.3.1),
    /// always a collection and never <c>null</c> (§2.3.2 item 1).
    /// </param>
    /// <returns>
    /// The Step-1 <c>Attack</c> input: <paramref name="attack"/> unchanged when no
    /// applicable modifier is present, and otherwise the once-truncated result of
    /// the combined signed percentage (<c>COMBAT_RULES.md</c> §5.6.6 items 4–5).
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="atkModifiers"/> (§2.3.7 item 6) or <paramref name="effects"/>
    /// (§2.3.2 item 1) is <c>null</c>.
    /// </exception>
    public static int EffectiveAttack(
        int attack,
        IReadOnlyList<ATKModifier> atkModifiers,
        IReadOnlyList<StatusEffect> effects) =>
        EffectivePetATK.Compose(attack, atkModifiers, effects);

    /// <summary>
    /// The effective Boss ATK a Boss attack's Step-1 input consumes —
    /// <c>BossState.ATK</c> modified by every active <c>TargetStat = "ATK"</c>
    /// Turn-based <c>BuffDebuff</c> instance held in the Boss's own
    /// <c>StatusEffects[]</c> (<c>COMBAT_RULES.md</c> §5.5.1).
    ///
    /// <b>This is the Boss-side counterpart of
    /// <see cref="EffectiveAttack"/>, and it is §5.5's Step-1 value, not a second
    /// representation of ATK.</b> §5.5.1 fixes the modifier's consumption point at
    /// the <b>Boss Damage Pipeline Step 1 <c>Attack</c> input</b>: the value
    /// returned here is passed as that argument and is never written back to
    /// <c>BossState.ATK</c>. §5.5.4 requires the base stat to be untouched and the
    /// derived value to be used within one pipeline execution and discarded, which
    /// is exactly what a pure function returning an <c>int</c> expresses — no
    /// <c>EffectiveBossATK</c> member exists on any state type
    /// (<c>GAME_STATE.md</c> §2.4/§2.4.1, §0 item 5).
    ///
    /// <b>The percentage applies to the stat alone.</b> §5.5.1 item 3 makes the
    /// modified value the <i>ATK term only</i>: a Boss Skill's authored Base Damage
    /// is a separate Step-1 contribution and is not modified by this rule
    /// (§5.5.2, §3.4 "Boss Skill Step-1 composition"). The caller therefore
    /// composes the Skill's Step-1 input by summing the returned value with the
    /// Skill's authored Base Damage, and a Boss attack contributes no ATK-Gem pool
    /// at all (§3.4).
    ///
    /// <b>Direction is the sign of <c>Magnitude</c> itself, and the formula is
    /// §5.5.1's own.</b> §5.5.1 authors
    /// <c>EffectiveBossATK = truncate( BossState.ATK × (100 + Magnitude) / 100 )</c>
    /// and states that the <c>Magnitude</c> is used <b>WITH ITS OWN SIGN</b>:
    /// <c>&gt; 0</c> increases, <c>&lt; 0</c> decreases, <c>= 0</c> leaves the stat
    /// unchanged. The sign therefore enters the single arithmetic expression as the
    /// addend's sign — there is no separate buff/debuff flag, no direction field,
    /// and no branch on an inferred semantic. This is deliberately <b>not</b>
    /// §5.4.1's Pet-side reading: the Pet-side rule uses <c>|Magnitude|</c> and only
    /// ever reduces, and §5.5.1 states the two conventions must not be collapsed
    /// into one shared formula.
    ///
    /// <b>Rounding is truncation toward zero, in integers only.</b> §5.5.1 applies
    /// the same convention §5.4.2 states for the Pet side and §3 step 6 uses for
    /// Final Damage. The arithmetic below is integer division rather than a
    /// <c>double</c> multiply-then-cast, so the result does not depend on
    /// floating-point representation. §3.4's worked example falls out of it exactly:
    /// <c>ATK 100</c> at <c>+20%</c> → <c>120</c>, and <c>ATK 100</c> at
    /// <c>-30%</c> → <c>70</c>.
    ///
    /// <b>Selection is by <c>Type</c> and <c>TargetStat</c>, never by
    /// <c>Id</c>.</b> §5.5.3 applies §5.4.5's discipline to the Boss side: the
    /// consumer reads <c>TargetStat</c> explicitly, and <c>Rage</c> is the MVP
    /// <i>instance</i> of the rule rather than the rule's identity
    /// (<c>BOSS_RULES.md</c> §6.2.1). A <c>DoT</c> (the Boss's own Burn tick), a
    /// <c>Shield</c>, a <c>State</c>, or a <c>BuffDebuff</c> naming any other stat
    /// is therefore not an ATK modifier.
    ///
    /// <b>Multiple active modifiers each apply the one decided formula in turn.</b>
    /// §5.5.5 fixes the existing Boss contract — at most one instance per effect
    /// identity, refreshed rather than stacked, with refresh-not-stack as the MVP
    /// default (§5.2 item 2, <c>GAME_STATE.md</c> §2.3.1 item 6) — and §5.5.1
    /// authors no stacking model. Where more than one distinct active instance is
    /// present, each therefore applies §5.5.1's single formula in the collection's
    /// existing committed order; the fold below uses the <c>Id</c>-ordinal order
    /// <c>GAME_STATE.md</c> §5.1.1 item 6 already fixes for the collection's passes.
    /// No additive/multiplicative/strongest-only choice is invented, and no
    /// aggregation type is introduced.
    /// </summary>
    /// <param name="attack">
    /// The stored base value (<c>BossState.ATK</c>). It is read and never written:
    /// §5.5.4 forbids overwriting it and forbids resetting it to a configuration
    /// default.
    /// </param>
    /// <param name="effects">
    /// The <b>Boss's</b> active instances (<c>GAME_STATE.md</c> §2.4.1) — not the
    /// Pet's. Always a collection and never <c>null</c> (§2.3.2 item 1).
    /// </param>
    /// <returns>
    /// The Step-1 <c>Attack</c> input: <paramref name="attack"/> unchanged when no
    /// active <c>TargetStat = "ATK"</c> <c>BuffDebuff</c> instance is present, and
    /// otherwise the value each such instance's magnitude modifies it to.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="effects"/> is <c>null</c> (§2.3.2 item 1).
    /// </exception>
    public static int EffectiveBossAttack(
        int attack,
        IReadOnlyList<StatusEffect> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);

        var effective = attack;

        // §5.1.1 item 6 fixes the collection's processing order to Id ordinal
        // ascending, so a fold over several instances is reproducible rather than
        // dependent on array position (§2.3.1 item 10, TDD.md §6).
        foreach (var effect in effects
            .Where(IsAtkBuffDebuff)
            .OrderBy(effect => effect.Id, StringComparer.Ordinal))
        {
            // §5.5.1 item 3's own formula, with §5.5.1 item 4's direction semantics:
            //
            //   EffectiveBossATK = truncate( BossState.ATK × (100 + Magnitude) / 100 )
            //
            // The Magnitude is used WITH ITS OWN SIGN and is the direction signal —
            //   Magnitude > 0  ->  increase
            //   Magnitude < 0  ->  decrease
            //   Magnitude = 0  ->  unchanged
            // so the sign needs no separate interpretation: it is carried by the
            // addend itself, and no buff/debuff flag or direction branch exists.
            // §5.5.1 also states the truncation convention: the result is an integer
            // truncated toward zero (§5.4.2, §3 step 6), and the arithmetic must not
            // depend on floating-point representation. Integer division over the
            // integer-cast magnitude gives exactly that on every platform and in
            // every evaluation order.
            var magnitude = (int)effect.Magnitude;

            effective = effective * (100 + magnitude) / 100;
        }

        return effective;
    }

    /// <summary>
    /// Whether an instance is one of the ATK modifiers
    /// <see cref="EffectiveAttack"/> consumes — a Turn-based
    /// <see cref="StatusEffectType.BuffDebuff"/> whose
    /// <see cref="StatusEffect.TargetStat"/> is <c>"ATK"</c>
    /// (<c>COMBAT_RULES.md</c> §5.4.1, §5.4.5).
    ///
    /// <b>It delegates; the selector has one owner.</b> The rule is
    /// <see cref="EffectivePetATK.IsAtkStatModifier"/>'s — that is where the
    /// unified composition reads <c>Type</c> and <c>TargetStat</c> — and this
    /// method exists so the <b>Boss-side</b> consumer
    /// (<see cref="EffectiveBossAttack"/>) selects instances by exactly the same
    /// predicate: §5.5.3 applies §5.4.5's discipline to the Boss side, so the two
    /// entities share one selection rule and differ only in the collection they
    /// read (<c>GAME_STATE.md</c> §2.3.1's "identical element shape").
    ///
    /// <b>Identity is deliberately not consulted.</b> §5.4.5 makes <c>Root</c> the
    /// MVP instance of the rule rather than the rule's name, so dispatching on
    /// <c>Id</c> would encode a second, undocumented definition of which effects
    /// modify ATK.
    /// </summary>
    private static bool IsAtkBuffDebuff(StatusEffect effect) =>
        EffectivePetATK.IsAtkStatModifier(effect);

    /// <summary>
    /// Whether two Status Effect collections hold the same instances in the same
    /// order — the structural comparison <c>GAME_STATE.md</c> §2.3.2 item 5's
    /// round-trip obligation requires.
    ///
    /// <b>Why a comparison method is needed.</b> The collections are arrays, and a
    /// record's default equality compares them by reference. §2.3.2 item 5 requires
    /// a round trip to return "the same elements, the same member values, the same
    /// optional-member presence/absence, and the same element order" — a statement
    /// about the <i>contents</i>, which reference equality cannot express. This is
    /// the same need <see cref="Match3.BoardState.CellsEqual"/> answers for the
    /// board (§2.1.7 item 5), applied to this collection.
    ///
    /// Order participates because §2.3.2 item 6 requires the order to round-trip,
    /// even though §2.3.1 item 10 makes it non-semantic.
    /// </summary>
    /// <param name="left">One collection.</param>
    /// <param name="right">The other collection.</param>
    /// <returns>Whether the two collections are element-for-element equal.</returns>
    public static bool EffectsEqual(
        IReadOnlyList<StatusEffect> left,
        IReadOnlyList<StatusEffect> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        // StatusEffect is a record struct, so its own equality already compares
        // every member — including the presence/absence of the two optional
        // duration members that §2.3.1 item 3 makes mutually exclusive.
        for (var index = 0; index < left.Count; index++)
        {
            if (!left[index].Equals(right[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether a collection holds an active <c>Stun</c> instance — the presence
    /// <c>BossState.State = Stunned</c> reflects (<c>GAME_STATE.md</c> §2.4.5,
    /// §5.1.1 item 8).
    ///
    /// §2.4.5 records that Stun is tracked through <c>StatusEffects[]</c> with
    /// <c>Type = "State"</c> and the Turn-countdown model, and that
    /// <c>RemainingTurns</c> is the authoritative counter this value follows. The
    /// identity compared against is <see cref="StunId"/>, the documented Stun
    /// identity <c>GAME_STATE.md</c> §2.3.1 item 1 names alongside Burn, Root, and
    /// Shield.
    /// </summary>
    private static bool HoldsStun(IReadOnlyList<StatusEffect> effects)
    {
        for (var index = 0; index < effects.Count; index++)
        {
            if (effects[index].Type == StatusEffectType.State
                && string.Equals(effects[index].Id, StunId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The documented <c>Stun</c> identity (<c>GAME_STATE.md</c> §2.3.1 item 1).
    /// </summary>
    private const string StunId = "Stun";

    /// <summary>
    /// The documented <c>Shield</c> identity (<c>GAME_STATE.md</c> §2.3.1 item 1;
    /// <c>COMBAT_RULES.md</c> §4 item 3 — "the Status Effect identity
    /// <c>"Shield"</c>", which is what makes two Shield sources refresh one another
    /// rather than form a second pool).
    /// </summary>
    private const string ShieldId = "Shield";
}
