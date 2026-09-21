namespace GameServer.Domain.Passives;

/// <summary>
/// The Passive Tracker — match-based Passive charging, threshold evaluation, and
/// reset <c>(PASSIVE_RULES.md</c> §2, §4, §5, §7;
/// <c>ARCHITECTURE.md</c> §3 <c>PassiveTracker</c>, Domain layer).
///
/// <b>It tracks one Passive over one Cascade's Matches.</b> The caller — the
/// Application layer's <c>BattleResolutionService</c> at
/// <c>GAME_RULES.md</c> §17 step 10 "Charge Passive" — supplies the Match
/// occurrences the cascade resolution already produced, in detection order. The
/// tracker performs no detection, reads no board, consumes no RNG, and resolves
/// no effect: it is a pure function of the Matches it is given, the Passive's
/// Threshold, and its Reset Behavior
/// (<c>ARCHITECTURE.md</c> §2.1 item 1: Domain is "testable with plain unit
/// tests, no I/O").
///
/// <code>
/// supplied Matches, in detection order   (MATCH3_RULES.md §3.2, §4.2)
///   └── per Match (PASSIVE_RULES.md §2 item 1: +1 each)
///         └── progress += 1
///               └── emit PassiveCharged      (§2 item 1, §7)  — informational
///   └── after the whole batch                (§2 item 3, §5)
///         └── if progress >= Threshold
///               ├── emit PassiveTriggered    pre-reset progress
///               └── apply Reset Behavior     (§4)  — Default/Partial/NoReset
/// </code>
///
/// <b>The batch is the unit of evaluation, and that is the whole model.</b> §2
/// item 3 is explicit: "Progress accumulates across all Matches within a single
/// Cascade resolution. After all Matches in the Cascade have been counted, the
/// Threshold is evaluated once. If progress ≥ Threshold, the Passive becomes
/// Ready and triggers <b>at most once per Cascade</b>." §5 repeats it ("the Pet's
/// Passive progress increases by N … After all N Matches have been counted, the
/// Threshold is evaluated once … the Passive triggers <b>at most once</b> per
/// Cascade"). So the accumulator is the sum <c>P + N</c> and the comparison
/// happens <b>once</b>, after the loop — never inside it.
///
/// <b>An oversized batch is normal, not an error.</b> Under the batch model
/// progress can legitimately land above the Threshold, which is precisely the
/// situation §4 item 2's partial reset describes and §5 works through: at
/// Threshold 3 a 7-Match Cascade reaches <c>7</c>, triggers once, and leaves
/// <c>4</c> under Partial Reset. The single evaluation is what decides the
/// trigger; the overshoot is only ever consumed by the Reset Behavior.
///
/// <b>One Match is one increment, whatever produced it.</b> §2 item 1 charges
/// for "every Match … regardless of whether it came from a direct Swap-match or
/// a Cascade match", and <c>MATCH3_RULES.md</c> §3 item 5 makes one detected
/// shape one Match however many cells it spans. So progress counts the Match
/// occurrences the caller supplies and never the cells they cleared.
///
/// <b><c>PassiveCharged</c> is informational progress and nothing else.</b> §2
/// item 1 / §7 emit it once per Match, with the running values <c>P+1 … P+N</c>,
/// so the client can render §6 item 1's "7 / 10 Matches" reading. It is <b>not</b>
/// an evaluation point: producing one triggers no threshold check and no reset,
/// and §2 item 3's "evaluated once" is a fact about the Cascade, not about any
/// charge the Cascade emitted. The charges therefore describe intermediate
/// progress that the Threshold comparison deliberately never sees — under Default
/// Reset a charge may report a value at or above the Threshold (the batch's
/// overshoot) on a Cascade that triggered exactly once, and that is the documented
/// model rather than a missed trigger.
///
/// <b>At most one trigger per call, for every Reset Behavior.</b> §2 item 3 and §5
/// bound the trigger to one per Cascade, and §2 item 4 makes the remainder of a
/// reset explicitly non-re-evaluable: "Any overflow remaining after reset is NOT
/// re-evaluated within the same Cascade — it waits for the next Cascade or Match
/// to continue charging." §5's multi-crossing example states the same for the
/// hardest case (Threshold 3, N = 7, Partial Reset): "4 ≥ 3, but trigger already
/// fired → progress carries into next charge." The evaluation is therefore an
/// <c>if</c> that runs once after the loop, and never a <c>while</c> over the
/// post-reset remainder. A later Cascade may of course trigger again — that is a
/// separate call, and <see cref="PassiveResetBehavior.NoReset"/> and
/// <see cref="PassiveResetBehavior.Partial"/> are precisely the behaviors whose
/// surviving progress can make it happen sooner.
///
/// <b>Special Gem detonations are not Matches, and this type does not decide
/// that.</b> §2 item 2 excludes them "consistent with them not counting toward
/// Combo/Match count either", and the board pipeline already enforces exactly
/// that: <c>MATCH3_RULES.md</c> §5.5.5 item 8 gives a Gem cleared by an
/// activation a <c>GemMatched</c> report and no Match, and
/// <c>CascadeResolver.CascadeResult.TotalMatches</c> counts Matches only. The
/// caller therefore supplies Matches, and a detonation is never among them — the
/// tracker has no activation input by which one could be counted, and it
/// introduces no filter, flag, or heuristic to guess at one. Feeding it an
/// activation's cleared cells as if each were a Match would be the caller's
/// defect, not a case this type can detect (<c>GAME_EVENTS.md</c> §2 item 3).
///
/// <b>Threshold = 1 is not a special case.</b> §1 defines a Threshold as "e.g.
/// 'every 5 Matches'" — a count of Matches — and nothing bounds it below. At
/// Threshold <c>1</c> any Cascade with at least one Match satisfies the single
/// evaluation and triggers once, whatever its Match count, which is the batch
/// model applied without a branch.
///
/// <b>The reset is applied after the trigger, and the trigger reports
/// pre-reset progress.</b> §4 item 1 resets "immediately after the Passive
/// triggers", and <c>GAME_EVENTS.md</c> §2 item 2 records the triggered event's
/// progress as the value at the crossing — before that reset. §5's examples read
/// exactly that way: progress <c>7</c> is reported and the reset leaves <c>0</c>
/// (Default) or <c>2</c> (Partial at Threshold 5).
/// </summary>
public static class PassiveTracker
{
    /// <summary>
    /// Charges a Passive over the Matches one Cascade resolution produced,
    /// evaluating the Threshold once after the whole batch and applying the
    /// Passive's Reset Behavior at most once
    /// (<c>PASSIVE_RULES.md</c> §2, §4, §5).
    /// </summary>
    /// <param name="progress">
    /// The Passive's progress entering this Cascade (<c>GAME_STATE.md</c> §2.3
    /// <c>PassiveProgress</c>). It carries the Threshold, which is the Passive's
    /// own definition value (<c>PASSIVE_RULES.md</c> §1), and the Current
    /// progress already accumulated — non-zero when a previous Cascade left
    /// progress behind, whether under <see cref="PassiveResetBehavior.NoReset"/>
    /// (§4 item 2's persistent case), under
    /// <see cref="PassiveResetBehavior.Partial"/> (§2 item 4's overflow), or
    /// because a Cascade ended below the Threshold (§2 item 1 keeps whatever was
    /// accumulated).
    /// </param>
    /// <param name="matchCount">
    /// How many Matches the Cascade produced, in detection order
    /// (<c>PASSIVE_RULES.md</c> §5: "If a single Swap's Cascade chain produces N
    /// matches in one resolution"). This is a count of <b>Matches</b>, never of
    /// cleared cells and never of Special Gem activations (§2 item 2). A Cascade
    /// that produced none — an empty run, which a committed Swap cannot produce —
    /// charges nothing and cannot trigger.
    ///
    /// The tracker is given the occurrence count rather than the Match values
    /// because charging depends on <b>how many</b> Matches happened and on
    /// <b>nothing about them</b>: §2 item 1 is "+1 per Match" with no
    /// dependence on the Match's shape, Gem type, cell, size, tier, cascade
    /// depth, or created Special Gems (<c>MATCH3_RULES.md</c> §3 item 5). No
    /// field of a <c>MatchResolution</c> is read, so accepting the values would
    /// add an input the rule never consults and would misstate the tracker's
    /// dependency as the caller's Match model rather than its Match count.
    /// </param>
    /// <param name="passiveId">
    /// The identity of the Passive being charged
    /// (<c>GAME_STATE.md</c> §2.3 <c>PassiveId</c>), reported on every event.
    /// </param>
    /// <param name="reset">
    /// The Passive's Reset Behavior (<c>PASSIVE_RULES.md</c> §4). Defaults to
    /// <see cref="PassiveResetBehavior.Default"/> — §4 item 1's "progress resets
    /// to 0 immediately after the Passive triggers", which is what §4 item 3
    /// means by behavior not stated on the Passive's definition and what all
    /// five MVP Pet Passives use (§8).
    /// </param>
    /// <returns>
    /// The settled progress and the charge/trigger reports the Cascade produced
    /// (<c>PASSIVE_RULES.md</c> §7, <c>GAME_EVENTS.md</c> §2). The reports are
    /// one <c>PassiveCharged</c> per Match and <b>zero or one</b>
    /// <c>PassiveTriggered</c> — never more than one, for any Reset Behavior.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="progress"/> carries a Threshold that is not a usable
    /// Match count, or <paramref name="matchCount"/> is negative,
    /// or <paramref name="reset"/> is not one of the reset behaviors
    /// <c>PASSIVE_RULES.md</c> §4 defines and this tracker implements
    /// (<see cref="PassiveResetBehavior"/>).
    /// </exception>
    public static PassiveChargeResult Charge(
        PassiveProgress progress,
        int matchCount,
        PassiveId passiveId,
        PassiveResetBehavior reset = PassiveResetBehavior.Default)
    {
        // The Threshold is the Passive's "every N Matches" value (§1). A count of
        // Matches cannot be zero or negative: §2 item 3 compares progress against
        // it and §2 item 4 resets to 0, so a Threshold of 0 would be Ready before
        // any Match and would leave §4 item 2's persistent behavior permanently
        // triggering. No document defines such a Threshold, so rather than invent
        // a meaning for it the tracker rejects it.
        if (progress.Threshold <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(progress),
                progress.Threshold,
                "A Passive's Threshold counts Matches and must be at least 1 "
                + "(PASSIVE_RULES.md §1, §2 item 3).");
        }

        if (progress.Current < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(progress),
                progress.Current,
                "Passive progress counts Matches and cannot be negative "
                + "(PASSIVE_RULES.md §2 item 1).");
        }

        if (matchCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(matchCount),
                matchCount,
                "A Cascade resolution cannot produce a negative number of Matches "
                + "(PASSIVE_RULES.md §5).");
        }

        if (!IsDefined(reset))
        {
            throw new ArgumentOutOfRangeException(
                nameof(reset),
                reset,
                "A Reset Behavior is exactly one of Default / Partial / NoReset "
                + "(PASSIVE_RULES.md §4).");
        }

        var threshold = progress.Threshold;
        var current = progress.Current;
        var charges = new List<PassiveChargedEvent>(matchCount);

        // §2 item 1 / §7: one increment and one PassiveCharged per Match, in the
        // order supplied. The run is over Matches, so this loop counts down the
        // occurrences and holds no Match value: no field of a Match affects the
        // charge.
        //
        // These events are informational (§6 item 1: progress is shown to the
        // player as "7 / 10 Matches"). Nothing here evaluates the Threshold and
        // nothing here resets: the single §2 item 3 evaluation is below, after
        // the batch is complete, and the values reported here are the batch's
        // intermediate progress, not trigger points.
        for (var match = 0; match < matchCount; match++)
        {
            current++;
            charges.Add(new PassiveChargedEvent(passiveId, current, threshold));
        }

        // §2 item 3 / §5: "After all Matches in the Cascade have been counted, the
        // Threshold is evaluated once." Here — and only here — is that evaluation,
        // on the accumulated batch total `P + N`.
        //
        // `if`, not `while`, and that is a rule rather than an optimization: §2
        // item 3 and §5 both bound the trigger to "at most once per Cascade", and
        // §2 item 4 spells out that what a reset leaves behind is not re-checked —
        // "Any overflow remaining after reset is NOT re-evaluated within the same
        // Cascade". §5's multi-crossing example is that rule at its sharpest:
        // under Partial Reset at Threshold 3 the reset leaves 4, which still
        // satisfies the Threshold, and the document's answer is "4 ≥ 3, but
        // trigger already fired → progress carries into next charge". A loop here
        // would fire again on that remainder and contradict both sections.
        if (current >= threshold)
        {
            // Pre-reset progress is reported (GAME_EVENTS.md §2 item 2), so the
            // event is built before the reset below — §5 reports 7 and then
            // settles at 0 (Default) or 2 (Partial at Threshold 5).
            var trigger = new PassiveTriggeredEvent(passiveId, current, threshold);
            current = Reset(current, threshold, reset);

            return new PassiveChargeResult(
                new PassiveProgress(threshold, current),
                charges,
                [trigger]);
        }

        // Below the Threshold: no trigger, and §2 item 3's "progress carries"
        // means the accumulated total is the result as-is.
        return new PassiveChargeResult(
            new PassiveProgress(threshold, current),
            charges,
            []);
    }

    /// <summary>
    /// Applies a Passive's Reset Behavior to progress that has just triggered
    /// (<c>PASSIVE_RULES.md</c> §4).
    /// </summary>
    /// <param name="current">
    /// Progress at the moment of the trigger — the accumulated batch total
    /// <c>P + N</c>, which is at or <b>above</b> the Threshold whenever the
    /// Cascade overshot it (<c>PASSIVE_RULES.md</c> §2 item 3, §5).
    /// </param>
    /// <param name="threshold">
    /// The Passive's Threshold (<c>PASSIVE_RULES.md</c> §1). It is the amount
    /// <see cref="PassiveResetBehavior.Partial"/> removes — "progress reduces by
    /// Threshold rather than to 0" (§4 item 2).
    /// </param>
    /// <param name="reset">The Passive's declared behavior (§4).</param>
    /// <returns>The progress after the reset.</returns>
    private static int Reset(int current, int threshold, PassiveResetBehavior reset) => reset switch
    {
        // §4 item 1: the default — "progress resets to 0 immediately after the
        // Passive triggers". Any overshoot the batch accumulated is discarded.
        PassiveResetBehavior.Default => 0,

        // §4 item 2: "No reset / persistent" — the trigger does not reduce
        // progress. §2 item 3 makes readiness and triggering the same instant, so
        // progress sitting at or above the Threshold after this is the documented
        // persistent behavior, not an untriggered Ready state to re-fire within
        // this Cascade (§2 item 4). A later Cascade may trigger again.
        PassiveResetBehavior.NoReset => current,

        // §4 item 2: "progress reduces by Threshold rather than to 0, allowing
        // 'overflow' matches from a single big Cascade to carry into the next
        // charge". §5's examples fix the arithmetic: 7 − 5 = 2 at Threshold 5 and
        // 7 − 3 = 4 at Threshold 3. The remainder is not re-evaluated here — §2
        // item 4 and §5's multi-crossing example leave even a remainder that still
        // satisfies the Threshold to the next Cascade.
        PassiveResetBehavior.Partial => current - threshold,

        // §4 defines these three behaviors and this tracker implements all three.
        // Rejecting an undefined value keeps the failure explicit instead of
        // silently applying the default reset in its place.
        _ => throw new ArgumentOutOfRangeException(
            nameof(reset),
            reset,
            "A Reset Behavior is exactly one of Default / Partial / NoReset "
            + "(PASSIVE_RULES.md §4)."),
    };

    /// <summary>
    /// Whether the value is one of the reset behaviors
    /// <c>PASSIVE_RULES.md</c> §4 defines and this tracker implements. The set is
    /// closed, so an undefined value has no reset rule and is rejected rather
    /// than defaulted.
    /// </summary>
    private static bool IsDefined(PassiveResetBehavior reset) =>
        reset is PassiveResetBehavior.Default
              or PassiveResetBehavior.Partial
              or PassiveResetBehavior.NoReset;
}
