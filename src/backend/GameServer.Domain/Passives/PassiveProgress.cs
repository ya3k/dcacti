namespace GameServer.Domain.Passives;

/// <summary>
/// The identity of a Pet's Passive (<c>PASSIVE_RULES.md</c> §1;
/// <c>GAME_STATE.md</c> §2.3 <c>PassiveId</c>).
///
/// <b>A Pet has exactly one Passive</b> (<c>PASSIVE_RULES.md</c> §1,
/// <c>GAME_RULES.md</c> §10), so this value does not select among several: it
/// names which Passive definition the active Pet carries. <c>GAME_STATE.md</c>
/// §2.3 owns that field, and it is the value the
/// <c>PassiveCharged</c>/<c>PassiveTriggered</c> payload reports as
/// <c>PassiveId</c> (<c>GAME_EVENTS.md</c> §2) — the same identity member the
/// sibling trigger events carry (<c>RelicId</c>, <c>CardId</c>,
/// <c>SkillId</c>).
///
/// <b>It is an identity, not a definition.</b> The Passive's Threshold, Trigger
/// Type, Effect, and Reset Behavior are the definition
/// (<c>PASSIVE_RULES.md</c> §1) and are supplied to
/// <see cref="PassiveTracker"/> as their own values. Nothing about the
/// definition is stored here, and no second copy of it is introduced.
///
/// <b>Why a value type and not a bare <c>string</c>.</b> The field is an
/// identity that travels into an event payload, so it is named so a caller
/// cannot pass a Pet id, a Threshold, or any other string where a Passive
/// identity belongs. There is no id format, scheme, or validation rule in any
/// document — <c>GAME_STATE.md</c> §2.3 defines the field's meaning, not its
/// spelling — so this type deliberately imposes none and holds the identifier
/// verbatim.
/// </summary>
/// <param name="Value">
/// The Passive's identifier, exactly as the active Pet's
/// <c>PetState.PassiveId</c> holds it (<c>GAME_STATE.md</c> §2.3). It is
/// reported and never re-derived, re-numbered, or invented by the tracker
/// (<c>GAME_EVENTS.md</c> §2 item 1).
/// </param>
public readonly record struct PassiveId(string Value)
{
    /// <summary>
    /// The documented identity, for diagnostics and for a caller that has one.
    /// </summary>
    public override string ToString() => Value;
}

/// <summary>
/// A Passive's charging position: the progress reached and the Threshold it is
/// measured against (<c>PASSIVE_RULES.md</c> §1, §2, §6;
/// <c>GAME_STATE.md</c> §2.3 <c>PassiveProgress</c>).
///
/// <code>
/// PassiveProgress
/// ├── Threshold    the "every N Matches" value of the Passive's definition
/// └── Current      progress so far, 0 … Threshold after a resolution settles
/// </code>
///
/// <b>This is the shape of the documented <c>PassiveProgress</c> field.</b>
/// <c>GAME_STATE.md</c> §2.3 describes it as "current count vs. threshold", and
/// <c>PASSIVE_RULES.md</c> §6 item 1 requires progress to be exposed as a
/// UI-facing value such as <c>7 / 10 Matches</c>. Both members are therefore
/// carried together, so a reader renders the documented
/// <c>Current / Threshold</c> pair without supplying either from elsewhere.
///
/// <b>Progress counts Matches, not cells and not events</b>
/// (<c>PASSIVE_RULES.md</c> §2 item 1: "Every Match increases the active Pet's
/// Passive progress by 1"). The unit is one Match, so the value is a plain
/// count and carries no Gem type, cell, or element.
///
/// <b>The value's range is a consequence, not a clamp.</b> After a Cascade
/// settles, <see cref="Current"/> is below <see cref="Threshold"/> for
/// <see cref="PassiveResetBehavior.Default"/> and for
/// <see cref="PassiveResetBehavior.Partial"/> — <c>PASSIVE_RULES.md</c> §2 item 4
/// and §5 settle the first at <c>0</c> and the second at the batch's overflow
/// (<c>7 − 5 = 2</c>), both strictly below the Threshold. Under
/// <see cref="PassiveResetBehavior.NoReset"/> it may legitimately sit at or
/// above it (§4 item 2's persistent behavior, §3's one-time Battle Start
/// example). This type therefore imposes no upper bound and does not
/// re-interpret one.
///
/// <b>During</b> a Cascade, progress may exceed the Threshold — that is §2
/// item 3's batch accumulation (<c>P + N</c>) and the overshoot §4 item 2's
/// partial reset is defined to carry. The Threshold is what a trigger is
/// evaluated against, not a ceiling the accumulator cannot pass.
/// </summary>
/// <param name="Threshold">
/// The Passive's Threshold — "e.g. 'every 5 Matches'" (<c>PASSIVE_RULES.md</c>
/// §1), the value §2 item 3 compares progress against. It comes from the
/// Passive's definition, not from this type's construction site, and it is the
/// <c>threshold</c> member the <c>PassiveCharged</c> payload reports
/// (<c>GAME_EVENTS.md</c> §2 item 2).
/// </param>
/// <param name="Current">
/// Progress accumulated toward <paramref name="Threshold"/>
/// (<c>GAME_STATE.md</c> §2.3, <c>PASSIVE_RULES.md</c> §2's
/// <c>0 → 1 → 2 → … → Threshold</c>). <c>0</c> is the documented starting point
/// and a real value — it is what §2 item 4 resets progress to and what a battle
/// begins with — not an absence convention.
/// </param>
public readonly record struct PassiveProgress(int Threshold, int Current)
{
    /// <summary>
    /// The progress a Passive begins with, at the Passive's own Threshold
    /// (<c>PASSIVE_RULES.md</c> §2 item 4, §4 item 1: progress resets to
    /// <c>0</c>).
    /// </summary>
    public static PassiveProgress AtStart(int threshold) => new(threshold, 0);

    /// <summary>
    /// Whether progress has reached the Threshold — the state
    /// <c>PASSIVE_RULES.md</c> §2 item 3 calls <b>Ready</b>.
    ///
    /// §2 item 3 makes readiness and triggering the same moment ("becomes Ready
    /// and triggers immediately"), so there is no armed-but-untriggered state to
    /// represent and no separate flag: this is a question asked of the two
    /// values, not a third one.
    /// </summary>
    public bool IsReady => Current >= Threshold;

    /// <summary>"7 / 10 Matches" — the UI-facing reading of PASSIVE_RULES.md §6 item 1.</summary>
    public override string ToString() => $"{Current} / {Threshold} Matches";
}
