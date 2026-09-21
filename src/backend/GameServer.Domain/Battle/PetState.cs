using GameServer.Domain.Passives;

namespace GameServer.Domain.Battle;

/// <summary>
/// The active Pet's battle state, through the Pet / Passive stage
/// (<c>GAME_STATE.md</c> §2.3).
///
/// <code>
/// BattleState
/// └── PetState
///     ├── PassiveId              the Pet's one Passive — the identity the
///     │                          Passive events report (PASSIVE_RULES.md §1)
///     ├── PassiveProgress        current count vs. threshold                (§2)
///     └── PassiveResetOverride?  only present for a non-default reset      (§4)
/// </code>
///
/// <b>This is the documented owner, not a new decision.</b> <c>GAME_STATE.md</c>
/// §2.3 places <c>PassiveId</c>, <c>PassiveProgress</c>, and
/// <c>PassiveResetOverride</c> here, and §2 nests <c>PetState</c> inside
/// <c>BattleState</c> (§2.3, §2). All three are therefore ordinary <b>Active
/// Battle State</b>: authoritative, server-produced, and written in the same
/// single post-resolution write-back as <c>Turn</c>, <c>Sequence</c>,
/// <c>BoardState</c>, <c>RngState</c>, and <c>PlayerState</c> (§5.1).
///
/// <b>Only the fields this stage requires exist.</b> §2.3 also lists
/// <c>PetId</c>/Identity, <c>Element</c>, and <c>Tier</c>/<c>Star</c>/<c>Level</c>.
/// Those belong to the Pet identity and progression stage and are <b>not yet
/// implemented</b>, not <b>not required</b> (§0 item 4, §2.0.5.3,
/// <c>SIGNALR_PROTOCOL.md</c> §4.3 item 2): each is added by its own owning task,
/// exactly as this stage adds these three. They are not stubbed, defaulted, or
/// represented by a placeholder, because a placeholder for a field no rule yet
/// reads would be a representation of its own (§0 item 5).
///
/// <b>No second representation of the Passive.</b> The Passive's
/// <c>Threshold</c>, <c>Trigger Type</c>, <c>Effect</c>, and <c>Reset Behavior</c>
/// are its <b>definition</b> (<c>PASSIVE_RULES.md</c> §1) and are not stored
/// beside it (§2.3 item 1). <see cref="PassiveProgress"/> carries the Threshold
/// because the documented progress <i>pair</i> is "current count vs. threshold"
/// (§2.3, §2.5) and <c>PASSIVE_RULES.md</c> §6 item 1 requires the pair to be
/// rendered together; the identity travels separately and names which definition
/// the values are read from (§2.3: "it is an identity, not a definition").
///
/// <b>No <c>Status</c> field and no lifecycle value.</b> A battle has no
/// lifecycle state machine (<c>GAME_STATE.md</c> §2.0.3), and this stage adds
/// none.
///
/// This type is deliberately minimal and framework-independent
/// (<c>ARCHITECTURE.md</c> §2.1): it references no ASP.NET Core, SignalR, EF
/// Core, Redis, HTTP, Phaser, or Discord concern.
/// </summary>
/// <param name="PassiveId">
/// Which Passive definition the active Pet carries (<c>GAME_STATE.md</c> §2.3).
/// A Pet has <b>exactly one</b> Passive (<c>PASSIVE_RULES.md</c> §1,
/// <c>GAME_RULES.md</c> §9.2 item 2), so this names one Passive — it does not
/// select among several, and there is no collection, slot, or ordering of
/// Passives here.
///
/// It is <b>set at battle creation and never changes</b> (§2.3 item 2): selecting
/// a Pet locks in its Passive for the duration of the battle
/// (<c>PET_RULES.md</c> §2 item 3), so no resolution writes it and no event
/// changes it. It is the value <c>GAME_EVENTS.md</c> §2's
/// <c>PassiveCharged</c>/<c>PassiveTriggered</c> report, read and reported rather
/// than re-derived (§2 item 1).
///
/// It is <b>not</b> an absent-when-unset convention: a battle always has its one
/// active Pet and therefore its one Passive (§2.3 item 3), so it is present from
/// battle creation with no null or "no Passive yet" form. <see cref="PetState"/>
/// is therefore not nullable on <see cref="BattleState"/>.
/// </param>
/// <param name="PassiveProgress">
/// The Passive's charging position — the progress reached and the Threshold it is
/// measured against (<c>GAME_STATE.md</c> §2.3, §2.5;
/// <c>PASSIVE_RULES.md</c> §2).
///
/// It starts at <see cref="PassiveProgress.AtStart"/> — the Passive's own
/// Threshold with <c>Current = 0</c> (<c>GAME_STATE.md</c> §2.3 item 3,
/// <c>SIGNALR_PROTOCOL.md</c> §4.3 item 4: <c>current = 0</c> "is what a battle
/// begins with") — and is written after each committed Swap's resolution
/// (§5.1, <c>GAME_EVENTS.md</c> §2, <c>PassiveChargeResult.Progress</c>).
///
/// <c>Current = 0</c> is a real publishable value here, so absence is never used
/// for it: neither member is nullable and neither is omitted
/// (<c>SIGNALR_PROTOCOL.md</c> §4.3 item 4).
/// </param>
/// <param name="PassiveResetOverride">
/// The Passive's non-default <b>Reset Behavior</b>, or <c>null</c> for the
/// default (<c>GAME_STATE.md</c> §2.3, <c>PASSIVE_RULES.md</c> §4).
///
/// It is "only present if this Pet's Passive uses non-default reset behavior"
/// (§2.3), so <c>null</c> is not "unset" and not a third behavior: it is the
/// documented representation of <see cref="PassiveResetBehavior.Default"/> —
/// §4 item 1's "progress resets to 0 immediately after the Passive triggers",
/// which is what §4 item 3 means by a behavior not declared on the Passive's
/// definition and what all five MVP Pet Passives use (§8). A caller that supplies
/// no override and a caller that supplies
/// <see cref="PassiveResetBehavior.Default"/> therefore describe the same
/// behavior, deliberately.
///
/// It is the value <see cref="PassiveTracker.Charge"/> receives as its
/// <c>reset</c> argument, and it is delivered on the wire only when non-default —
/// the member is omitted, never written as JSON <c>null</c>
/// (<c>SIGNALR_PROTOCOL.md</c> §4.3 items 6–7).
/// </param>
public readonly record struct PetState(
    PassiveId PassiveId,
    PassiveProgress PassiveProgress,
    PassiveResetBehavior? PassiveResetOverride = null)
{
    /// <summary>
    /// The Reset Behavior the tracker applies for this Pet's Passive
    /// (<c>PASSIVE_RULES.md</c> §4).
    ///
    /// An absent <see cref="PassiveResetOverride"/> reads as
    /// <see cref="PassiveResetBehavior.Default"/> — §2.3 makes absence the
    /// representation of the default, so this is the documented reading rather than
    /// a fallback chosen here.
    /// </summary>
    public PassiveResetBehavior ResetBehavior => PassiveResetOverride ?? PassiveResetBehavior.Default;

    /// <summary>
    /// Whether this Pet's Passive declares a non-default Reset Behavior — the
    /// condition under which <c>GAME_STATE.md</c> §2.3 says
    /// <c>PassiveResetOverride</c> is present, and under which
    /// <c>SIGNALR_PROTOCOL.md</c> §4.3 makes the wire member present.
    /// </summary>
    public bool HasResetOverride => PassiveResetOverride is not null;

    /// <summary>
    /// The documented <c>PetState</c> of a newly created battle: the active Pet's
    /// Passive identity, progress at the start of its first charge, and the
    /// declared Reset Behavior (<c>GAME_STATE.md</c> §2.3;
    /// <c>SIGNALR_PROTOCOL.md</c> §4.3 item 4).
    /// </summary>
    /// <param name="passiveId">The active Pet's Passive identity (§2.3 item 2).</param>
    /// <param name="passiveThreshold">
    /// The Passive's Threshold — "e.g. 'every 5 Matches'" (<c>PASSIVE_RULES.md</c>
    /// §1). It comes from the Passive's definition, which is why it is supplied by
    /// the battle-creation caller rather than invented here.
    /// </param>
    /// <param name="passiveResetOverride">
    /// The declared non-default Reset Behavior, or <c>null</c> for the default
    /// (§4 items 1–3).
    /// </param>
    public static PetState AtBattleCreation(
        PassiveId passiveId,
        int passiveThreshold,
        PassiveResetBehavior? passiveResetOverride = null) =>
        // §2.3 item 3 / §4.3 item 4: progress begins at 0 against the Passive's own
        // Threshold. It is never absent, never lazily initialized, and never
        // defaulted with an invented value.
        new(passiveId, PassiveProgress.AtStart(passiveThreshold), passiveResetOverride);
}
