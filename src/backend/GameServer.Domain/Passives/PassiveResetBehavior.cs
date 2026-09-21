namespace GameServer.Domain.Passives;

/// <summary>
/// What happens to a Passive's progress after it triggers
/// (<c>PASSIVE_RULES.md</c> §4; <c>GAME_STATE.md</c> §2.3
/// <c>PassiveResetOverride</c>).
///
/// <code>
/// Default    progress resets to 0 immediately after the trigger   (§4 item 1)
/// Partial    progress reduces by Threshold; overflow carries      (§4 item 2)
/// NoReset    progress is persistent; the trigger does not reset it (§4 item 2)
/// </code>
///
/// <b>This is the shape of the documented <c>PassiveResetOverride</c> field.</b>
/// <c>GAME_STATE.md</c> §2.3 declares the field as "only present if this Pet's
/// Passive uses non-default reset behavior", and <see cref="Default"/> is the
/// value its <i>absence</i> means (§4 item 1, §1's "default: resets to 0 after
/// trigger") — so a caller that supplies no override and a caller that supplies
/// <see cref="Default"/> describe the same behavior, deliberately.
///
/// <b>All three behaviors are reachable under the batch-per-Cascade model those
/// documents now define.</b> §2 item 3 accumulates every Match in a Cascade and
/// evaluates the Threshold <b>once</b>, after the whole batch: progress
/// <c>P + N</c> is compared against the Threshold a single time and the Passive
/// triggers at most once per Cascade. Progress can therefore sit <b>above</b> the
/// Threshold at the moment of a trigger whenever a Cascade's Match batch
/// overshoots it, which is exactly the case §4 item 2's partial reset is written
/// for — §5's own example is "Progress before cascade: 0 / Cascade produces 7
/// Matches → progress = 0 + 7 = 7 / 7 ≥ 5 → Passive triggers (once) / Partial
/// Reset → progress = 7 − 5 = 2 (overflow carries into next charge)".
///
/// <b>"Reduce by Threshold", not "reduce to the remainder".</b> §4 item 2 spells
/// the arithmetic as "progress reduces by Threshold rather than to 0", so the
/// reset subtracts one Threshold from the progress at the trigger; §5's examples
/// confirm it (<c>7 − 5 = 2</c> at Threshold 5, <c>7 − 3 = 4</c> at Threshold 3).
/// That remainder is <b>not</b> re-evaluated within the same Cascade even when it
/// still satisfies the Threshold (§2 item 4, §5's multi-crossing example:
/// "4 ≥ 3, but trigger already fired → progress carries into next charge"). It is
/// simply the progress the next Cascade starts from, where it can make that
/// Cascade reach the Threshold sooner.
/// </summary>
public enum PassiveResetBehavior
{
    /// <summary>
    /// Progress resets to <c>0</c> immediately after the Passive triggers
    /// (<c>PASSIVE_RULES.md</c> §4 item 1).
    ///
    /// This is the default, and it is the behavior of all five MVP Pet Passives
    /// (§8). It is also what the absence of a
    /// <c>GAME_STATE.md</c> §2.3 <c>PassiveResetOverride</c> means, so it is a
    /// real, named value and not a placeholder for "unset".
    /// </summary>
    Default = 0,

    /// <summary>
    /// Progress is persistent and survives the trigger — no reset occurs
    /// (<c>PASSIVE_RULES.md</c> §4 item 2, "no reset / persistent").
    ///
    /// §4 item 2 marks this <b>rare</b> and requires it to be explicitly
    /// justified (its example is a one-time Battle Start Passive), and §4 item 3
    /// requires it to be declared on the specific Pet's Passive definition. It
    /// is supported because §4 defines it unambiguously; it is not the behavior
    /// of any MVP Pet Passive (§8: all five use the default full reset).
    ///
    /// Progress is left exactly as the trigger found it, so if it still
    /// satisfies the Threshold it survives into the next Cascade — where that
    /// Cascade's own single evaluation may trigger again (§2 item 3 evaluates
    /// once per Cascade, not once per Passive lifetime).
    /// </summary>
    NoReset = 1,

    /// <summary>
    /// Progress reduces by the Threshold rather than to <c>0</c>, so a Match
    /// batch that overshoots the Threshold carries its overflow into the next
    /// charge (<c>PASSIVE_RULES.md</c> §4 item 2).
    ///
    /// §4 item 2 gives the arithmetic ("progress reduces by Threshold rather than
    /// to 0, allowing 'overflow' matches from a single big Cascade to carry into
    /// the next charge") and §5 gives the worked examples — Threshold 5 with a
    /// 7-Match Cascade settles at <c>7 − 5 = 2</c>, and Threshold 3 with the same
    /// Cascade settles at <c>7 − 3 = 4</c>. The overflow is <b>not</b>
    /// re-evaluated inside the Cascade that produced it, even when it still
    /// satisfies the Threshold (§2 item 4, §5's multi-crossing example). §4 item
    /// 3 requires the behavior to be declared on the specific Pet's Passive
    /// definition; no MVP Pet Passive uses it (§8).
    /// </summary>
    Partial = 2,
}
