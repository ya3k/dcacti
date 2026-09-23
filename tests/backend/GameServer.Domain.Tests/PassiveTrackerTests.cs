using GameServer.Domain.Battle;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Passive Tracker tests (<c>PASSIVE_RULES.md</c> §1, §2, §4, §5, §6, §7, §8;
/// <c>GAME_EVENTS.md</c> §1, §2; <c>GAME_STATE.md</c> §2.3;
/// <c>GAME_RULES.md</c> §10, §16, §17 step 10).
///
/// The contract under test is the batch-per-Cascade model of §2 item 3 and §5:
///
/// <code>
/// one Cascade = one Match batch
///   per Match, in detection order        §2 item 1
///     progress += 1
///     emit PassiveCharged                §7 — informational only
///   after the whole batch                §2 item 3, §5
///     if progress >= Threshold
///       emit PassiveTriggered            (pre-reset progress)
///       apply Reset Behavior             §4
///       └── Default → 0 · Partial → current − Threshold · NoReset → unchanged
///   one evaluation, at most one trigger, per Cascade
/// </code>
///
/// Expected values come from the rule, never from the implementation: a charge
/// count is asserted to equal the Match count the Cascade was given (§2 item 1),
/// a trigger count is derived from the documented Threshold/batch arithmetic
/// (§5), and the reset values are §5's own worked examples rather than numbers
/// copied out of the tracker.
/// </summary>
public class PassiveTrackerTests
{
    private static readonly PassiveId XichLang = new("xich-lang");

    /// <summary>A Passive starting at progress 0 with the given Threshold.</summary>
    private static PassiveProgress AtThreshold(int threshold) => PassiveProgress.AtStart(threshold);

    /// <summary>Charges a Passive over one Cascade of <paramref name="matches"/> Matches.</summary>
    private static PassiveChargeResult Charge(
        int threshold,
        int matches,
        PassiveResetBehavior reset = PassiveResetBehavior.Default,
        int from = 0) =>
        PassiveTracker.Charge(
            new PassiveProgress(threshold, from),
            matches,
            XichLang,
            reset);

    // =======================================================================
    // Batch accumulation — PASSIVE_RULES.md §2 item 3, §5
    // =======================================================================

    [Fact]
    public void Batch_ShouldEvaluateTheThresholdOnceAfterTheWholeCascade()
    {
        // §2 item 3: "Progress accumulates across all Matches within a single
        // Cascade resolution. After all Matches in the Cascade have been counted,
        // the Threshold is evaluated once." The documented Default example (§5) is
        // Threshold=3 with N=7:
        //
        //   Progress before cascade: 0
        //   Cascade produces 7 Matches → progress = 0 + 7 = 7
        //   7 ≥ 3 → Passive triggers (once)
        //   Default Reset → progress = 0
        var result = Charge(threshold: 3, matches: 7);

        var trigger = Assert.Single(result.Triggers);
        Assert.Equal(7, trigger.Progress);
        Assert.Equal(3, trigger.Threshold);
        Assert.Equal(0, result.Progress.Current);
    }

    [Fact]
    public void Batch_ShouldEmitOneChargePerMatchRegardlessOfTriggers()
    {
        // §2 item 1 charges for every Match, so a Cascade's charge count equals its
        // Match count even when the single evaluation triggered: the charges are
        // per-Match progress reports, not trigger reports. The 7-Match §5 example
        // therefore emits 7 charges running 1..7.
        var result = Charge(threshold: 3, matches: 7);

        Assert.Equal(7, result.Charges.Count);
        Assert.Equal(Enumerable.Range(1, 7), result.Charges.Select(c => c.Progress));
    }

    [Fact]
    public void Batch_ShouldChargeForTheMatchesBeyondTheThreshold()
    {
        // The heart of the batch model: the Matches after the Threshold is
        // arithmetically passed still charge. Under the old match-by-match model
        // this Cascade settled at 1 (crossing at Match 3 and again at Match 6);
        // §2 item 3 and §5 accumulate all 7 first, so the accumulated total — and
        // therefore the value the single evaluation sees — is 7.
        var result = Charge(threshold: 3, matches: 7);

        Assert.Equal(7, result.Charges[^1].Progress);
        Assert.Equal(7, Assert.Single(result.Triggers).Progress);
    }

    [Fact]
    public void Batch_ShouldContinueFromProgressSuppliedByTheCaller()
    {
        // GAME_STATE.md §2.3: PassiveProgress is state carried between Cascades. A
        // Cascade that starts at 2 with 2 more Matches accumulates to 4 — §2 item 3
        // adds the batch to the progress already held, it does not restart it.
        var result = Charge(threshold: 5, matches: 2, from: 2);

        Assert.Equal([3, 4], result.Charges.Select(c => c.Progress));
        Assert.Equal(4, result.Progress.Current);
        Assert.Empty(result.Triggers);
    }

    [Fact]
    public void Batch_ShouldNotTriggerWhenTheAccumulatedTotalStaysBelowTheThreshold()
    {
        // §2 item 3's evaluation is inclusive at the Threshold, so a batch that
        // settles one below it produces charges, no trigger, and carries its
        // progress (§2 item 3's "No → no trigger, progress carries").
        var result = Charge(threshold: 5, matches: 4, from: 0);

        Assert.Equal(4, result.Progress.Current);
        Assert.Empty(result.Triggers);
    }

    [Fact]
    public void ZeroMatches_ShouldChargeNothingAndLeaveProgressUnchanged()
    {
        // §2 item 1 charges per Match, so a Cascade with no Match produces no
        // charge. A committed Swap cannot produce such a Cascade
        // (MATCH3_RULES.md §4.3 item 5), but the tracker's contract is
        // Match-driven and must not invent a charge for a Match that did not
        // occur — nor a trigger, since the batch total is unchanged.
        var result = Charge(threshold: 5, matches: 0, from: 3);

        Assert.Empty(result.Charges);
        Assert.Empty(result.Triggers);
        Assert.Equal(new PassiveProgress(5, 3), result.Progress);
    }

    // =======================================================================
    // Threshold = 1 — PASSIVE_RULES.md §1, §2 item 3
    // =======================================================================

    [Fact]
    public void ThresholdOne_ShouldTriggerOnceOnAOneMatchCascade()
    {
        // §1 defines a Threshold as "e.g. 'every 5 Matches'" — a Match count — and
        // bounds it below nowhere. At Threshold 1 a single Match already satisfies
        // §2 item 3's one evaluation.
        var result = Charge(threshold: 1, matches: 1);

        Assert.Single(result.Charges);
        Assert.Single(result.Triggers);

        Assert.Equal(1, result.Charges[0].Progress);
        Assert.Equal(1, result.Triggers[0].Progress);
    }

    [Fact]
    public void ThresholdOne_ShouldStillTriggerOnlyOncePerCascade()
    {
        // The batch model's sharpest case: at Threshold 1 every Match would cross
        // the Threshold, but §2 item 4 and §5 cap the Cascade at one trigger. The
        // batch accumulates to N, triggers once, and a Default reset discards the
        // overshoot.
        for (var matches = 1; matches <= 6; matches++)
        {
            var result = Charge(threshold: 1, matches: matches);

            Assert.Equal(matches, result.Charges.Count);
            Assert.Single(result.Triggers);
            Assert.Equal(matches, result.Triggers[0].Progress);
            Assert.Equal(0, result.Progress.Current);
        }
    }

    [Fact]
    public void Threshold_ShouldBeRejectedWhenNotAUsableMatchCount()
    {
        // A Threshold that counts Matches cannot be 0 or negative: §2 item 3
        // compares progress against it, so Threshold 0 would be satisfied before
        // any Match was charged, and under §4 item 2's persistent behavior the
        // comparison would never clear. No document defines such a Threshold, so
        // it is rejected rather than given an invented meaning.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PassiveTracker.Charge(AtThreshold(0), 1, XichLang));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => PassiveTracker.Charge(AtThreshold(-5), 1, XichLang));
    }

    [Fact]
    public void Charging_ShouldRejectANegativeMatchCountOrNegativeProgress()
    {
        // Neither is a number of Matches §2 item 1 could have counted.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PassiveTracker.Charge(AtThreshold(5), -1, XichLang));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => PassiveTracker.Charge(new PassiveProgress(5, -1), 1, XichLang));
    }

    // =======================================================================
    // Default Reset — PASSIVE_RULES.md §4 item 1, §5
    // =======================================================================

    [Fact]
    public void DefaultReset_ShouldResetToZeroAfterTheTrigger()
    {
        // §4 item 1: "progress resets to 0 immediately after the Passive
        // triggers". §8 confirms all five MVP Pet Passives use this default, and
        // §5's Default example settles at 0.
        var result = Charge(threshold: 5, matches: 5);

        Assert.Single(result.Triggers);
        Assert.Equal(0, result.Progress.Current);
    }

    [Fact]
    public void DefaultReset_ShouldDiscardTheBatchOvershoot()
    {
        // §5's Default example again: 7 Matches at Threshold 3 settle at 0. The
        // four Matches beyond the Threshold are not carried anywhere — Default
        // reset clears progress entirely (§5's closing paragraph).
        var result = Charge(threshold: 3, matches: 7, PassiveResetBehavior.Default);

        Assert.Single(result.Triggers);
        Assert.Equal(0, result.Progress.Current);
    }

    [Fact]
    public void DefaultReset_ShouldBeTheBehaviorWhenNoOverrideIsSupplied()
    {
        // §4 item 3: any non-default reset must be documented on the specific
        // Pet's Passive definition, "not assumed". GAME_STATE.md §2.3 makes the
        // override field present "only present if this Pet's Passive uses
        // non-default reset behavior" — so its absence means §4 item 1. A caller
        // that passes no behavior and one that passes Default must therefore
        // agree.
        var implicitDefault = Charge(threshold: 5, matches: 12);
        var explicitDefault = Charge(threshold: 5, matches: 12, PassiveResetBehavior.Default);

        Assert.Equal(explicitDefault.Progress, implicitDefault.Progress);
        Assert.Equal(
            explicitDefault.Charges.Select(c => c.Progress),
            implicitDefault.Charges.Select(c => c.Progress));
        Assert.Equal(
            explicitDefault.Triggers.Select(t => t.Progress),
            implicitDefault.Triggers.Select(t => t.Progress));
    }

    [Fact]
    public void DefaultReset_ShouldCarryNoProgressIntoTheNextCascade()
    {
        // §4 item 1 resets to 0, so whatever the batch ran up to is gone: a
        // 10-Match Cascade at Threshold 5 triggers once and leaves nothing for the
        // next Cascade to build on.
        var result = Charge(threshold: 5, matches: 10);

        Assert.Single(result.Triggers);
        Assert.Equal(10, result.Triggers[0].Progress);
        Assert.Equal(0, result.Progress.Current);
    }

    [Fact]
    public void DefaultReset_ShouldTriggerForABatchExactlyAtTheThreshold()
    {
        // §2 item 3 triggers when progress "reaches" the Threshold — the boundary
        // is inclusive, so a batch summing exactly to it triggers.
        var result = Charge(threshold: 5, matches: 5);

        var trigger = Assert.Single(result.Triggers);
        Assert.Equal(5, trigger.Progress);
        Assert.Equal(5, trigger.Threshold);
        Assert.Equal(0, result.Progress.Current);
    }

    // =======================================================================
    // Partial Reset — PASSIVE_RULES.md §4 item 2, §5
    // =======================================================================

    [Fact]
    public void PartialReset_ShouldPreserveOverflowAboveTheThreshold()
    {
        // §5's Partial Reset example, asserted verbatim:
        //
        //   Progress before cascade: 0
        //   Cascade produces 7 Matches → progress = 0 + 7 = 7
        //   7 ≥ 5 → Passive triggers (once)
        //   Partial Reset → progress = 7 − 5 = 2  (overflow carries into next charge)
        var result = Charge(threshold: 5, matches: 7, PassiveResetBehavior.Partial);

        var trigger = Assert.Single(result.Triggers);
        Assert.Equal(7, trigger.Progress);
        Assert.Equal(5, trigger.Threshold);
        Assert.Equal(2, result.Progress.Current);
    }

    [Fact]
    public void PartialReset_ShouldNotReEvaluateTheOverflowWithinTheSameCascade()
    {
        // §5's Multi-crossing example, asserted verbatim — the case TASK-011
        // settled:
        //
        //   Progress before cascade: 0
        //   Cascade produces 7 Matches → progress = 0 + 7 = 7
        //   7 ≥ 3 → Passive triggers (once)
        //   Partial Reset → progress = 7 − 3 = 4
        //   4 ≥ 3, but trigger already fired → progress carries into next charge
        //
        // The remainder 4 still satisfies the Threshold, and the documented answer
        // is still a single trigger: §2 item 4 says overflow "is NOT re-evaluated
        // within the same Cascade".
        var result = Charge(threshold: 3, matches: 7, PassiveResetBehavior.Partial);

        var trigger = Assert.Single(result.Triggers);
        Assert.Equal(7, trigger.Progress);
        Assert.Equal(4, result.Progress.Current);
    }

    [Fact]
    public void PartialReset_ShouldProduceAtMostOneTriggerForAnyBatchAndThreshold()
    {
        // §2 item 3 / §5: "at most once per Cascade" holds whatever the overshoot
        // ratio, including Threshold 1 — the case where every single Match in the
        // batch would cross on its own.
        foreach (var threshold in new[] { 1, 2, 3, 5, 7 })
        {
            foreach (var matches in new[] { 1, 2, 3, 7, 12, 25 })
            {
                var result = Charge(threshold, matches, PassiveResetBehavior.Partial);

                var expectedTriggers = matches >= threshold ? 1 : 0;
                Assert.Equal(expectedTriggers, result.Triggers.Count);
            }
        }
    }

    [Fact]
    public void PartialReset_ShouldSettleAtTheArithmeticRemainder()
    {
        // §4 item 2's "progress reduces by Threshold rather than to 0" is plain
        // subtraction on the batch total (P + N − Threshold), clamped by the
        // trigger condition. Asserted against the documented formula, not the
        // implementation.
        foreach (var (threshold, from, matches) in new[]
                 {
                     (5, 0, 7), (3, 0, 7), (5, 4, 3), (2, 0, 7), (10, 9, 5), (1, 0, 6),
                 })
        {
            var result = Charge(threshold, matches, PassiveResetBehavior.Partial, from);

            var batchTotal = from + matches;

            if (batchTotal >= threshold)
            {
                Assert.Single(result.Triggers);
                Assert.Equal(batchTotal - threshold, result.Progress.Current);
            }
            else
            {
                Assert.Empty(result.Triggers);
                Assert.Equal(batchTotal, result.Progress.Current);
            }
        }
    }

    [Fact]
    public void PartialReset_ShouldLetCarriedOverflowReachTheNextCascadeSooner()
    {
        // §5: the difference between the Reset variants is "what progress remains
        // after the trigger … Partial Reset preserves overflow, allowing a future
        // Cascade to reach Threshold faster". Two Cascades at Threshold 5: the
        // first settles at 2, so the second needs only 3 more Matches to trigger.
        var first = Charge(threshold: 5, matches: 7, PassiveResetBehavior.Partial);
        Assert.Equal(2, first.Progress.Current);

        var second = PassiveTracker.Charge(
            first.Progress,
            3,
            XichLang,
            PassiveResetBehavior.Partial);

        var trigger = Assert.Single(second.Triggers);
        Assert.Equal(5, trigger.Progress);
        Assert.Equal(0, second.Progress.Current);
    }

    [Fact]
    public void PartialReset_ShouldSettleBelowTheThresholdWhenTheOvershootIsSmaller()
    {
        // §4 item 2's arithmetic: the remainder is batch total − Threshold, so it is
        // below the Threshold exactly when the batch overshot it by less than one
        // Threshold. §5's examples are both of that kind (7 − 5 = 2 < 5, 7 − 3 = 4
        // is not, and §5 says so explicitly). §2 item 4 means a remainder that
        // still qualifies is simply carried, not re-evaluated.
        var small = Charge(threshold: 5, matches: 7, PassiveResetBehavior.Partial);
        Assert.Equal(2, small.Progress.Current);
        Assert.True(small.Progress.Current < small.Progress.Threshold);

        // A large overshoot leaves a remainder that still qualifies — §5's
        // multi-crossing case, carried rather than re-triggered.
        var large = Charge(threshold: 3, matches: 7, PassiveResetBehavior.Partial);
        Assert.Equal(4, large.Progress.Current);
        Assert.True(large.Progress.Current >= large.Progress.Threshold);
        Assert.Single(large.Triggers);
    }

    [Fact]
    public void PassiveResetBehavior_ShouldDefineExactlyDefaultPartialAndNoReset()
    {
        // §4 defines three behaviors: the default full reset (item 1), partial
        // reset, and no reset / persistent (item 2). All three are reachable under
        // the batch model of §2 item 3, which allows progress to exceed the
        // Threshold within a Cascade. This asserts the closed set, so dropping or
        // renaming a member without a documentation change fails here.
        var members = Enum.GetNames<PassiveResetBehavior>()
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Default", "NoReset", "Partial"], members);

        // The numeric values are part of the value's identity and are relied on by
        // GAME_STATE.md §2.3's PassiveResetOverride field.
        Assert.Equal(0, (int)PassiveResetBehavior.Default);
        Assert.Equal(1, (int)PassiveResetBehavior.NoReset);
        Assert.Equal(2, (int)PassiveResetBehavior.Partial);
    }

    // =======================================================================
    // No reset / persistent — PASSIVE_RULES.md §4 item 2
    // =======================================================================

    [Fact]
    public void NoReset_ShouldNotReduceProgress()
    {
        // §4 item 2: "No reset / persistent" — the trigger does not reset
        // progress. §4 item 2 marks it rare and §4 item 3 requires it to be
        // declared per-Pet; its documented example is a one-time Battle Start
        // Passive (§3).
        var result = Charge(threshold: 5, matches: 5, PassiveResetBehavior.NoReset);

        var trigger = Assert.Single(result.Triggers);
        Assert.Equal(5, trigger.Progress);
        Assert.Equal(5, result.Progress.Current);
    }

    [Fact]
    public void NoReset_ShouldStillTriggerOnlyOnceWithinTheCascade()
    {
        // The batch is evaluated once, so a NoReset Cascade triggers once however
        // far past the Threshold it ran: progress is left exactly as the batch
        // accumulated it, and the trigger is not re-asked within this Cascade
        // (§2 item 3, §2 item 4, §5).
        var result = Charge(threshold: 3, matches: 7, PassiveResetBehavior.NoReset);

        var trigger = Assert.Single(result.Triggers);
        Assert.Equal(7, trigger.Progress);
        Assert.Equal(7, result.Progress.Current);
    }

    [Fact]
    public void NoReset_ShouldTriggerAgainOnALaterCascadeWhileProgressStillQualifies()
    {
        // §2 item 3 evaluates once *per Cascade*, and §4 item 2 leaves progress
        // intact — so a later Cascade whose accumulated total still satisfies the
        // Threshold triggers again (§2 item 4: what survives "waits for the next
        // Cascade … to continue charging").
        var first = Charge(threshold: 5, matches: 5, PassiveResetBehavior.NoReset);
        Assert.Single(first.Triggers);
        Assert.Equal(5, first.Progress.Current);

        var second = PassiveTracker.Charge(
            first.Progress,
            1,
            XichLang,
            PassiveResetBehavior.NoReset);

        var trigger = Assert.Single(second.Triggers);
        Assert.Equal(6, trigger.Progress);
        Assert.Equal(6, second.Progress.Current);
    }

    [Fact]
    public void NoReset_ShouldNotTriggerOnACascadeThatStaysBelowTheThreshold()
    {
        // §2 item 3's evaluation is the same for every Reset Behavior: a batch that
        // does not reach the Threshold does not trigger, and its progress carries.
        var result = Charge(threshold: 5, matches: 4, PassiveResetBehavior.NoReset);

        Assert.Empty(result.Triggers);
        Assert.Equal(4, result.Progress.Current);
    }

    [Fact]
    public void NoReset_ShouldRejectAnUndefinedResetBehavior()
    {
        // §4 defines the behaviors this tracker implements. An undefined value has
        // no reset rule, so it is rejected rather than defaulted to one.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PassiveTracker.Charge(AtThreshold(5), 1, XichLang, (PassiveResetBehavior)99));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => PassiveTracker.Charge(AtThreshold(5), 1, XichLang, (PassiveResetBehavior)3));
    }

    // =======================================================================
    // One trigger per Cascade, across all Reset Behaviors — §2 item 3/4, §5
    // =======================================================================

    [Fact]
    public void OneTriggerPerCascade_ShouldHoldForDefaultPartialAndNoReset()
    {
        // The acceptance criterion the batch model exists for (§2 item 3, §5, and
        // the multi-crossing example): Threshold 3 with a 7-Match Cascade triggers
        // exactly once under every Reset Behavior. Only the settled progress
        // differs — 0 / 4 / 7 per §4.
        var expected = new (PassiveResetBehavior Reset, int Settled)[]
        {
            (PassiveResetBehavior.Default, 0),
            (PassiveResetBehavior.Partial, 4),
            (PassiveResetBehavior.NoReset, 7),
        };

        foreach (var (reset, settled) in expected)
        {
            var result = Charge(threshold: 3, matches: 7, reset);

            Assert.Single(result.Triggers);
            Assert.Equal(3, result.Triggers[0].Threshold);
            Assert.Equal(7, result.Triggers[0].Progress);
            Assert.Equal(settled, result.Progress.Current);

            // The charges are identical under all three: §2 item 1 charges per
            // Match, and the Reset Behavior only decides what survives.
            Assert.Equal(7, result.Charges.Count);
            Assert.Equal(Enumerable.Range(1, 7), result.Charges.Select(c => c.Progress));
        }
    }

    [Fact]
    public void OneTriggerPerCascade_ShouldHoldAcrossThresholdsAndBatchSizes()
    {
        // The documented bound is per Cascade and unconditional ("at most once per
        // Cascade"), so the trigger count is exactly 1 when the batch reaches the
        // Threshold and 0 when it does not — for every Reset Behavior.
        var resets = new[]
        {
            PassiveResetBehavior.Default,
            PassiveResetBehavior.Partial,
            PassiveResetBehavior.NoReset,
        };

        foreach (var reset in resets)
        {
            foreach (var threshold in new[] { 1, 2, 4, 5, 6, 7, 10 })
            {
                foreach (var matches in new[] { 0, 1, threshold, threshold + 1, threshold * 3 })
                {
                    var result = Charge(threshold, matches, reset);

                    var expectedTriggers = matches >= threshold ? 1 : 0;

                    Assert.Equal(expectedTriggers, result.Triggers.Count);
                    Assert.Equal(matches, result.Charges.Count);
                }
            }
        }
    }

    [Fact]
    public void MultipleTriggers_ShouldNoLongerHappenWithinOneCascade()
    {
        // The regression this task exists for. Under the old match-by-match model a
        // 7-Match Cascade at Threshold 3 triggered twice — at Match 3 (progress
        // 3→0) and Match 6 (progress 3→0), settling at 1. §2 item 3 and §5 now
        // accumulate all 7 Matches first, evaluate once, and trigger once,
        // settling at 0 under Default Reset.
        var result = Charge(threshold: 3, matches: 7);

        Assert.Single(result.Triggers);
        Assert.Equal(0, result.Progress.Current);

        // And the old model's two-trigger signature is gone: the charge run never
        // restarts mid-Cascade, because no reset happens mid-Cascade.
        Assert.Equal(Enumerable.Range(1, 7), result.Charges.Select(c => c.Progress));
    }

    [Fact]
    public void MultipleTriggers_ShouldNeverExceedOneForAnyBatchSize()
    {
        // Exhaustive over batch sizes: one Cascade can never report more than one
        // trigger, for any Reset Behavior or Threshold. This is the invariant the
        // batch model establishes.
        var resets = new[]
        {
            PassiveResetBehavior.Default,
            PassiveResetBehavior.Partial,
            PassiveResetBehavior.NoReset,
        };

        foreach (var reset in resets)
        {
            for (var threshold = 1; threshold <= 8; threshold++)
            {
                for (var matches = 0; matches <= 40; matches++)
                {
                    var result = Charge(threshold, matches, reset, from: matches % 4);

                    Assert.True(
                        result.Triggers.Count <= 1,
                        $"Threshold {threshold}, {matches} matches, {reset}: "
                        + $"{result.Triggers.Count} triggers in one Cascade");
                }
            }
        }
    }

    // =======================================================================
    // PassiveCharged is informational only — PASSIVE_RULES.md §6 item 1, §7
    // =======================================================================

    [Fact]
    public void PassiveCharged_ShouldBeEmittedInMatchDetectionOrder()
    {
        // §2: PassiveCharged is emitted "once per Match" and, per §1.1, sits in the
        // per-Match cycle after the Match's other events. §5 fixes the charge order
        // as the detection order the caller supplies, so the reported progress runs
        // strictly upward in steps of exactly 1 across the Cascade's whole batch.
        var result = Charge(threshold: 5, matches: 4);

        Assert.Equal([1, 2, 3, 4], result.Charges.Select(c => c.Progress));
    }

    [Fact]
    public void PassiveCharged_ShouldReportTheBatchOvershootEvenUnderDefaultReset()
    {
        // The boundary this task's brief calls out: charges are informational and
        // MUST NOT gate or trigger anything. Under the batch model a Default-reset
        // Cascade reports charge values above the Threshold — the Matches beyond it
        // still charged — while the single evaluation still produced exactly one
        // trigger. A tracker that stopped charging at the Threshold, or that reset
        // on a charge, would fail here.
        var result = Charge(threshold: 3, matches: 7, PassiveResetBehavior.Default);

        Assert.Equal([1, 2, 3, 4, 5, 6, 7], result.Charges.Select(c => c.Progress));
        Assert.Single(result.Triggers);
        Assert.Equal(0, result.Progress.Current);
    }

    [Fact]
    public void PassiveCharged_ProgressShouldIncreaseByExactlyOne()
    {
        // §2 item 1 is "+1 per Match" — never more, never less. With no mid-Cascade
        // reset, a Cascade's charge run is strictly increasing by exactly 1 from the
        // progress it entered with; a backwards step or a jump would be the same
        // defect §1.1 item 7 describes for Combo.
        foreach (var reset in new[]
                 {
                     PassiveResetBehavior.Default,
                     PassiveResetBehavior.Partial,
                     PassiveResetBehavior.NoReset,
                 })
        {
            var result = Charge(threshold: 4, matches: 11, reset, from: 2);

            var expected = Enumerable.Range(3, 11);

            Assert.Equal(expected, result.Charges.Select(c => c.Progress));
        }
    }

    [Fact]
    public void PassiveCharged_ShouldReportTheThresholdOnEveryCharge()
    {
        // GAME_EVENTS.md §2: PassiveCharged carries "new progress value,
        // threshold" — the threshold is reported alongside so the client renders
        // the §6 item 1 "7 / 10 Matches" reading without recomputing it.
        var result = Charge(threshold: 5, matches: 3);

        Assert.All(result.Charges, c => Assert.Equal(5, c.Threshold));
    }

    [Fact]
    public void PassiveCharged_ShouldReportThePassiveIdentityOnEveryEvent()
    {
        // GAME_EVENTS.md §2 item 1: PassiveId identifies the Passive that charged
        // — the active Pet's PetState.PassiveId (GAME_STATE.md §2.3). Every event
        // of the Cascade reports that identity, unchanged.
        var result = Charge(threshold: 5, matches: 7);

        Assert.All(result.Charges, c => Assert.Equal(XichLang, c.PassiveId));
        Assert.All(result.Triggers, t => Assert.Equal(XichLang, t.PassiveId));
    }

    // =======================================================================
    // Special Gem detonations are not Matches — PASSIVE_RULES.md §2 item 2
    // =======================================================================

    [Fact]
    public void SpecialGemDetonation_ShouldNotChargeBecauseItIsNotAMatch()
    {
        // §2 item 2: "Special Gem detonations (MATCH3_RULES.md §5.5) do NOT count
        // as a Match for Passive progress, consistent with them not counting
        // toward Combo/Match count either."
        //
        // The exclusion is enforced upstream where Matches are counted, not here.
        // This test asserts it from the tracker's side: the only quantity that
        // charges it is the Match count, so a Cascade that produced M Matches
        // charges exactly M times — never once per cleared cell.
        var result = Charge(threshold: 5, matches: 3);

        Assert.Equal(3, result.Charges.Count);

        // The charges correspond 1:1 with the Matches supplied and with nothing
        // else the board might have done.
        Assert.Equal(
            Enumerable.Range(1, 3),
            result.Charges.Select(c => c.Progress));
    }

    [Fact]
    public void SpecialGemDetonation_ShouldBeExcludedByTheBoardPipelinesOwnMatchCount()
    {
        // The upstream half of §2 item 2, asserted so the exclusion has a tested
        // owner rather than resting on this type's silence: a board whose
        // completing Swap activates a held Special Gem clears cells beyond its
        // match set, and the resolution reports those cells as GemMatched while
        // its Match count and the activation count remain different things.
        //
        // (The tracker's input is exactly the Match count this asserts on.)
        var state = BattleState.CreateWith("battle-009", Seed) with
        {
            BoardState = TestBoard.Background().WithGems(
                (I(4, 0), GemType.Atk),
                (I(4, 1), GemType.Def),
                (I(4, 2), GemType.Atk),
                (I(3, 1), GemType.Atk))
                .WithSpecial((I(4, 0), SpecialGem.LineClear(SpecialGemOrientation.Horizontal))),
        };

        var swap = SwapExecutor.Execute(state, new SwapRequest(From, To));

        Assert.True(swap.IsAccepted);

        // The pass activated the held Special Gem and cleared extra cells...
        var activationCells = swap.Resolution.Passes.Sum(p => p.ActivationCellGemMatched.Count);
        Assert.True(activationCells > 0, "the fixture's activation must clear extra cells");
        Assert.True(swap.Resolution.Passes.Sum(p => p.ActivatedSpecialGems.Count) > 0);

        // ...and those cells are not Matches: the resolution's Match total is its
        // match sets' total, independent of the cleared cells.
        var matchTotal = swap.Resolution.Passes.Sum(p => p.Matches.Count);
        Assert.Equal(matchTotal, swap.Resolution.TotalMatches);

        // Charging on the Match total — the §2 item 2-correct input — is therefore
        // charging on Matches only. Charging on the cleared cells would be a
        // different, larger number, which is the defect §2 item 2 rules out.
        var clearedCells = swap.Resolution.Passes.Sum(p => p.ClearedCellUnion.Count);
        Assert.True(
            clearedCells > matchTotal,
            "cleared cells must outnumber Matches, or the distinction would be untestable");

        var charged = PassiveTracker.Charge(AtThreshold(5), swap.Resolution.TotalMatches, XichLang);
        Assert.Equal(matchTotal, charged.Charges.Count);
    }

    private const ulong Seed = 20260815UL;
    private const int From = 25; // I(3, 1)
    private const int To = 33;   // I(4, 1)

    private static int I(int row, int column) => TestBoard.I(row, column);

    // =======================================================================
    // PassiveTriggered payload — GAME_EVENTS.md §2
    // =======================================================================

    [Fact]
    public void PassiveTriggered_ShouldReportPreResetProgress()
    {
        // GAME_EVENTS.md §2 item 2: PassiveTriggered reports the progress at the
        // moment the threshold was reached — before that trigger's own reset. Under
        // the batch model that is the accumulated total, so §5's Partial example
        // reports 7 and settles at 2, and its Default example reports 7 and settles
        // at 0.
        var partial = Charge(threshold: 5, matches: 7, PassiveResetBehavior.Partial);
        Assert.Equal(7, Assert.Single(partial.Triggers).Progress);
        Assert.Equal(2, partial.Progress.Current);

        var @default = Charge(threshold: 5, matches: 7, PassiveResetBehavior.Default);
        Assert.Equal(7, Assert.Single(@default.Triggers).Progress);
        Assert.Equal(0, @default.Progress.Current);
    }

    [Fact]
    public void PassiveTriggered_ShouldCarryThePassiveIdentityProgressAndThreshold()
    {
        // GAME_EVENTS.md §2: PassiveTriggered's payload is PassiveId, the progress
        // at the trigger, and the Threshold. This asserts the payload the event
        // contract defines and nothing more.
        var result = Charge(threshold: 5, matches: 5);

        var trigger = Assert.Single(result.Triggers);

        Assert.Equal(XichLang, trigger.PassiveId);
        Assert.Equal(5, trigger.Progress);
        Assert.Equal(5, trigger.Threshold);
    }

    [Fact]
    public void PassiveTriggered_ShouldCarryNoEffectSummaryMember()
    {
        // GAME_EVENTS.md §2 item 3: the effect summary is DEFERRED to the Combat
        // stage — what Burn/Shield/Crit/Defense do is owned by COMBAT_RULES.md,
        // not by this tracker. The trigger value therefore carries the five
        // documented members — the identity, the progress pair, and the shared
        // event's source/sourceId (SIGNALR_PROTOCOL.md §3.2.17) — and no
        // effect-summary field of an invented shape (AGENTS.md §7: do not invent
        // what the docs do not define).
        var members = typeof(PassiveTriggeredEvent)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["PassiveId", "Progress", "Source", "SourceId", "Threshold"], members);
    }

    [Fact]
    public void PassiveTriggered_ShouldNotBeEmittedWhenTheThresholdIsNotReached()
    {
        // §2: PassiveTriggered is emitted "only when threshold crossed". A Cascade
        // whose batch stayed below produces charges and no trigger.
        var result = Charge(threshold: 10, matches: 9);

        Assert.Equal(9, result.Charges.Count);
        Assert.Empty(result.Triggers);
    }

    // =======================================================================
    // Purity and determinism — GAME_EVENTS.md §3 item 6, AGENTS.md §11
    // =======================================================================

    [Fact]
    public void Tracker_ShouldBeDeterministicAndFreeOfSharedState()
    {
        // The tracker is a pure function of its inputs (ARCHITECTURE.md §2.1
        // item 1: Domain has no framework dependencies and no I/O; AGENTS.md §11:
        // no uncontrolled randomness). Repeated identical calls must agree
        // exactly, and no call may affect another's result.
        var first = Charge(threshold: 5, matches: 12);
        var second = Charge(threshold: 5, matches: 12);

        Assert.Equal(first.Progress, second.Progress);
        Assert.Equal(
            first.Charges.Select(c => (c.PassiveId, c.Progress, c.Threshold)),
            second.Charges.Select(c => (c.PassiveId, c.Progress, c.Threshold)));
        Assert.Equal(
            first.Triggers.Select(t => (t.PassiveId, t.Progress, t.Threshold)),
            second.Triggers.Select(t => (t.PassiveId, t.Progress, t.Threshold)));

        // An unrelated interleaved call with different inputs does not disturb it.
        _ = Charge(threshold: 2, matches: 7, PassiveResetBehavior.NoReset);

        var third = Charge(threshold: 5, matches: 12);

        Assert.Equal(first.Progress, third.Progress);
        Assert.Equal(
            first.Charges.Select(c => c.Progress),
            third.Charges.Select(c => c.Progress));
    }

    [Fact]
    public void Tracker_ShouldNotMutateTheProgressItWasGiven()
    {
        // GAME_EVENTS.md §3 item 6: producing events and progress is not a state
        // write. The caller's value is a readonly record struct and must come back
        // unchanged; the new progress is the returned one.
        var input = new PassiveProgress(5, 3);

        var result = PassiveTracker.Charge(input, 7, XichLang);

        Assert.Equal(new PassiveProgress(5, 3), input);
        Assert.NotEqual(input, result.Progress);
        Assert.Equal(0, result.Progress.Current);
    }

    [Fact]
    public void Tracker_ShouldFeedItsSettledProgressBackIntoTheNextCascade()
    {
        // GAME_STATE.md §2.3: PassiveProgress is the state carried between
        // Cascades, so the tracker's returned value must be a valid input to the
        // next call. §2 item 4 settles a Default-reset Passive at 0, so a split
        // Cascade sequence with nothing pending must agree with one call of the
        // same total under NoReset.
        var single = Charge(threshold: 5, matches: 4, PassiveResetBehavior.NoReset);
        var split = PassiveTracker.Charge(
            PassiveTracker.Charge(
                AtThreshold(5),
                2,
                XichLang,
                PassiveResetBehavior.NoReset).Progress,
            2,
            XichLang,
            PassiveResetBehavior.NoReset);

        Assert.Equal(single.Progress, split.Progress);
        Assert.Empty(single.Triggers);
        Assert.Empty(split.Triggers);
    }
}
