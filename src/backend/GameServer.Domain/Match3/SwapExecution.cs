namespace GameServer.Domain.Match3;

using GameServer.Domain.Battle;

/// <summary>
/// The outcome of executing one requested Swap
/// (<c>MATCH3_RULES.md</c> §2.1.6, §8.3).
///
/// <code>
/// BattleState + SwapRequest  →  SwapExecutionResult
/// </code>
///
/// It is either a <b>rejection</b>, carrying the documented reason and nothing
/// else, or a <b>commit</b>, carrying the authoritative <c>BattleState</c> the
/// resolution produced together with the ordered reports of that resolution.
///
/// A rejection carries no state. That is the contract, not an omission: a
/// rejected Swap is a gameplay no-op (<c>MATCH3_RULES.md</c> §2.1.5) — the
/// board, <c>Turn</c>, <c>Sequence</c>, <c>RngState</c>,
/// <c>LastCommittedSwapPair</c>, and the player's <c>MatchCount</c> and
/// <c>Combo</c> are all unchanged — so there is no resulting
/// state to hand back and the caller keeps the one it passed in. A rejection
/// therefore cannot be mistaken for a commit that produced an empty board.
///
/// This is a Domain value and not a wire contract: the rejection reason is
/// reported to the caller only (<c>MATCH3_RULES.md</c> §2.1.5 item 6,
/// <c>SIGNALR_PROTOCOL.md</c> §5).
/// </summary>
public readonly record struct SwapExecutionResult
{
    private SwapExecutionResult(
        bool isAccepted,
        SwapRejectionReason reason,
        BattleState? state,
        CascadeResolver.CascadeResult resolution,
        IReadOnlyList<BattleEvent> events,
        ResourceGeneration? resources)
    {
        IsAccepted = isAccepted;
        Reason = reason;
        _state = state;
        _resolution = resolution;
        _events = events;
        _resources = resources;
    }

    private readonly BattleState? _state;
    private readonly CascadeResolver.CascadeResult _resolution;
    private readonly IReadOnlyList<BattleEvent>? _events;
    private readonly ResourceGeneration? _resources;

    /// <summary>
    /// True when the Swap was accepted, committed, and resolved
    /// (<c>MATCH3_RULES.md</c> §2.1.6).
    /// </summary>
    public bool IsAccepted { get; }

    /// <summary>
    /// True when the Swap was rejected and nothing was committed
    /// (<c>MATCH3_RULES.md</c> §2.1.5).
    /// </summary>
    public bool IsRejected => !IsAccepted;

    /// <summary>
    /// The documented rejection reason, or <see cref="SwapRejectionReason.None"/>
    /// when the Swap was accepted. The value comes from
    /// <see cref="SwapValidator"/>; this type does not re-decide it.
    /// </summary>
    public SwapRejectionReason Reason { get; }

    /// <summary>
    /// The authoritative state after the resolution — the committed board, the
    /// advanced <c>RngState</c>, the begun <c>Turn</c>, the incremented
    /// <c>Sequence</c>, the recorded <c>LastCommittedSwapPair</c>, and the
    /// player's <c>MatchCount</c> and <c>Combo</c>, all
    /// together in one value (<c>GAME_STATE.md</c> §5.1).
    ///
    /// It is the single post-resolution write-back: no partial state exists in
    /// which the board is exchanged but the record is old, or the record is new
    /// but the board is unresolved (<c>GAME_STATE.md</c> §2.1.10 item 5,
    /// §5.1 item 2).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The Swap was rejected, so no resulting state exists
    /// (<c>MATCH3_RULES.md</c> §2.1.5: a rejected action writes nothing).
    /// </exception>
    public BattleState State =>
        _state ?? throw new InvalidOperationException(
            "A rejected Swap produces no state: a rejected action writes nothing "
            + "(MATCH3_RULES.md §2.1.5). Check IsAccepted before reading State.");

    /// <summary>
    /// The cascade loop's own result for the committed Swap — the passes that ran,
    /// the final stable board, and the retained <c>RngState</c>
    /// (<c>MATCH3_RULES.md</c> §4.3). It is the existing resolution's report,
    /// surfaced unchanged so a caller does not have to re-run the loop to learn
    /// what it did.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The Swap was rejected; nothing was resolved.
    /// </exception>
    public CascadeResolver.CascadeResult Resolution =>
        IsAccepted
            ? _resolution
            : throw new InvalidOperationException(
                "A rejected Swap resolves nothing (MATCH3_RULES.md §2.1.5). "
                + "Check IsAccepted before reading Resolution.");

    /// <summary>
    /// The ordered Battle Events this committed Swap produced
    /// (<c>GAME_EVENTS.md</c> §1, §1.1, §2), in the order the client must apply
    /// them.
    ///
    /// They <b>describe</b> the resolution that <see cref="State"/> already holds;
    /// they are not a second source of state and reading them changes nothing
    /// (<c>GAME_EVENTS.md</c> §3 item 6, <c>SIGNALR_PROTOCOL.md</c> §4 item 6).
    ///
    /// A <b>rejected</b> Swap produces no gameplay Battle Event at all
    /// (<c>MATCH3_RULES.md</c> §2.1.5 item 6, <c>GAME_EVENTS.md</c> §1.2): the list
    /// is empty, and the rejection itself is reported through
    /// <see cref="Reason"/> to the caller only.
    ///
    /// It is empty, not <c>null</c>, for a rejection — the absence of events is a
    /// value, so a caller never has to distinguish "no events" from "not
    /// applicable".
    /// </summary>
    public IReadOnlyList<BattleEvent> Events => _events ?? [];

    /// <summary>
    /// The resources this committed Swap's cleared Gems generated
    /// (<c>GAME_RULES.md</c> §17 step 12, <c>COMBAT_RULES.md</c> §2).
    ///
    /// <b>The persistent half has already been applied.</b>
    /// <see cref="State"/> carries the resulting <c>PlayerState.Power</c>, clamped
    /// to the documented 0–100 range (<c>GAME_RULES.md</c> §12). This value reports
    /// what the Swap generated, whose <c>Power</c> is the <b>unclamped</b> pool —
    /// so a gain the cap absorbed is visible here as a larger number than the
    /// change in the state.
    ///
    /// <b>The transient half is carried here and nowhere else.</b> Base Damage
    /// Pool, Defense Pool, and Heal Pool are Transient Resolution State
    /// (<c>GAME_STATE.md</c> §3): the downstream resolution steps of this same
    /// Swap (15–17) consume them from this value, and they are not written into
    /// <c>PlayerState</c> or <c>BattleState</c>, not persisted, and not delivered
    /// on the wire. The Heal Pool is one of them even though its effect
    /// <b>has been applied</b>: it is <i>consumed</i> by step 14
    /// (<see cref="ResourceGenerator.ApplyHeal"/>, which wrote the resulting
    /// <c>PlayerState.HP</c>) and is still <b>not stored</b> anywhere — reading it
    /// does not make it state.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The Swap was rejected. A rejected action generates nothing and writes
    /// nothing (<c>MATCH3_RULES.md</c> §2.1.5 item 5: no Match, no Combo, no
    /// Power, no Relic progression occurs).
    /// </exception>
    public ResourceGeneration Resources =>
        _resources ?? throw new InvalidOperationException(
            "A rejected Swap generates no resources (MATCH3_RULES.md §2.1.5 item 5). "
            + "Check IsAccepted before reading Resources.");

    /// <summary>
    /// The same committed result with its ordered event list replaced by
    /// <paramref name="events"/> — a new immutable value; the original is not
    /// mutated and its own list is unchanged.
    ///
    /// <b>Why it exists.</b> The Swap resolution is assembled from two stages
    /// that produce events: the board resolution's own Match-3 events
    /// (<see cref="BattleEventBuilder"/>, called inside
    /// <see cref="SwapExecutor.Execute"/>) and the Passive stage's
    /// <c>PassiveCharged</c>/<c>PassiveTriggered</c> reports, which
    /// <c>GAME_RULES.md</c> §17 places at step 10 — <b>after</b> "Count Matches"
    /// and before "Trigger Relics" (<c>GAME_EVENTS.md</c> §1.1). The latter belongs
    /// to the Application-layer pipeline step, which calls
    /// <see cref="GameServer.Domain.Passives.PassiveTracker.Charge"/> only for a
    /// committed Swap, so the assembled list is not known when
    /// <see cref="SwapExecutor"/> builds its result.
    ///
    /// This method is a pure factory over the value: it carries
    /// <see cref="IsAccepted"/>, <see cref="Reason"/>, <see cref="State"/>, and
    /// <see cref="Resolution"/> across unchanged and sets only the event list. It
    /// computes no gameplay value, orders nothing, and adds no member — the order
    /// of <paramref name="events"/> is the caller's, and the assembled list
    /// <b>is</b> the resolution's order (<c>GAME_EVENTS.md</c> §1.1,
    /// <c>SIGNALR_PROTOCOL.md</c> §3.2.12 item 4).
    /// </summary>
    /// <param name="events">
    /// The complete ordered event list for this resolution — the interface's
    /// existing events followed by the events the later pipeline steps produced.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The Swap was rejected. A rejected action emits no Battle Event at all
    /// (<c>MATCH3_RULES.md</c> §2.1.5 item 6, <c>GAME_EVENTS.md</c> §1.2), so a
    /// rejection has no event list to replace and keeps the empty one it carries.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="events"/> is <c>null</c>. A replacement list is a value; the
    /// absence of events is the empty list, never <c>null</c>.
    /// </exception>
    public SwapExecutionResult WithEvents(IReadOnlyList<BattleEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (!IsAccepted)
        {
            throw new InvalidOperationException(
                "A rejected Swap emits no Battle Event, so it has no event list to replace "
                + "(MATCH3_RULES.md §2.1.5 item 6, GAME_EVENTS.md §1.2).");
        }

        // Every other member is carried across unchanged: this replaces the event
        // list and nothing else.
        return new SwapExecutionResult(true, Reason, _state, _resolution, events, _resources!.Value);
    }

    /// <summary>
    /// The same committed result with its authoritative state replaced by
    /// <paramref name="state"/> — a new immutable value; the original is not
    /// mutated.
    ///
    /// It exists so the Application-layer pipeline step that extends the
    /// post-resolution write-back (the Passive stage's charge,
    /// <c>GAME_RULES.md</c> §17 step 10) can carry BOTH the extended state and the
    /// assembled event list in one result, without the state and the events
    /// describing different write-backs (<c>GAME_STATE.md</c> §5.1: one action, one
    /// write-back; <c>GAME_EVENTS.md</c> §3 item 6: an event is never a substitute
    /// for the state write-back).
    ///
    /// It is a pure factory over the value: <see cref="IsAccepted"/>,
    /// <see cref="Reason"/>, <see cref="Resolution"/>, and <see cref="Events"/> are
    /// carried across unchanged. It computes no gameplay value.
    /// </summary>
    /// <param name="state">
    /// The resolved authoritative state — the same single post-resolution value the
    /// caller stores.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The Swap was rejected. A rejected action writes nothing, so a rejection has
    /// no resulting state to replace (<c>MATCH3_RULES.md</c> §2.1.5).
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is <c>null</c>.</exception>
    public SwapExecutionResult WithState(BattleState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!IsAccepted)
        {
            throw new InvalidOperationException(
                "A rejected Swap produces no state: a rejected action writes nothing "
                + "(MATCH3_RULES.md §2.1.5).");
        }

        return new SwapExecutionResult(true, Reason, state, _resolution, _events ?? [], _resources!.Value);
    }

    /// <summary>
    /// The rejection result for a request that failed a documented check
    /// (<c>MATCH3_RULES.md</c> §2.1.2).
    ///
    /// It carries no state, no events, and no resources: a rejected action writes
    /// nothing and emits nothing (<c>MATCH3_RULES.md</c> §2.1.5 items 5–6).
    /// </summary>
    /// <param name="reason">The failing check, as decided by the validator.</param>
    internal static SwapExecutionResult Rejected(SwapRejectionReason reason) =>
        new(false, reason, null, default, [], null);

    /// <summary>
    /// The commit result for an accepted request: the resolved authoritative state,
    /// the resolution that produced it, the ordered events describing it, and the
    /// resources its cleared Gems generated
    /// (<c>GAME_RULES.md</c> §17 step 12).
    ///
    /// It is <b>internal</b>: <see cref="SwapExecutor.Execute"/> is where a commit
    /// originates, and the Application-layer pipeline step that extends the same
    /// post-resolution write-back with the later stages' values composes
    /// <see cref="WithEvents"/> and <see cref="WithState"/> over it rather than
    /// constructing a result of its own — so the state it hands back is the state
    /// it stays committed to.
    /// </summary>
    internal static SwapExecutionResult Committed(
        BattleState state,
        CascadeResolver.CascadeResult resolution,
        IReadOnlyList<BattleEvent> events,
        ResourceGeneration resources) =>
        new(true, SwapRejectionReason.None, state, resolution, events, resources);
}

/// <summary>
/// Swap execution — the authoritative transition from an accepted Swap request to
/// the resolved, committed <c>BattleState</c> (<c>MATCH3_RULES.md</c> §2.1.6,
/// §8.3).
///
/// <code>
/// SwapRequest
///     ↓
/// SwapValidator.Validate(...)          §2.1.2, all four checks
///     ├── rejected → state unchanged   §2.1.5
///     └── accepted
///             ↓
///         BoardState.WithSwapped(...)  §2.1.6 step 4 — whole Cell entries
///             ↓
///         CascadeResolver.Resolve(...) §2.1.6 step 8 — §4 loop to stability
///             ↓
///         one write-back: board + RngState + Turn + Sequence
///                         + LastCommittedSwapPair + PlayerState
///                                                      §8.3, GAME_STATE.md §5.1
///         ↓
///         Charge Passive + PetState write-back  §17 step 10, PASSIVE_RULES.md §2
///                                               — owned by the Application step,
///                                                 composed via WithEvents/WithState
/// </code>
///
/// <b>The §17 steps this type owns.</b> The board stages (steps 1–9), the
/// resource generation of step 12 in its two persistent halves — Power
/// (<see cref="ResourceGenerator.ApplyPower"/>) and player-effect healing
/// (<see cref="ResourceGenerator.ApplyHeal"/>, step 14) — and the single
/// write-back. Steps 13 and 14 are state writes over the same <c>PlayerState</c>
/// this type already builds, so they are applied here rather than split into a
/// second write-back (<c>GAME_STATE.md</c> §5.1 item 2). The Damage Pipeline
/// (steps 15–17) is a separate stage and is not implemented.
///
/// <b>No second pipeline.</b> Validation is <see cref="SwapValidator"/>'s, the
/// exchange is <see cref="BoardState.WithSwapped"/>, and the resolution is
/// <see cref="CascadeResolver.Resolve"/> over the existing
/// <see cref="MatchDetector"/>, <see cref="SpecialGemPlanner"/>,
/// <see cref="SpecialGemEffects"/>, <see cref="Gravity"/>, and
/// <see cref="Spawn"/>. This type sequences those calls in the documented order
/// and owns nothing that already exists; it re-implements no rule and no step.
/// Match and Combo accounting is likewise not a second Match-3 pass: it reads
/// the passes the resolution already ran (§6 below).
///
/// <b>The commit is one value.</b> Every authoritative field is set in a single
/// <c>with</c> expression over the input state, so no caller can observe a state
/// in which the board is swapped but <c>LastCommittedSwapPair</c> is still the
/// old pair, or the record is written but the board is unresolved
/// (<c>GAME_STATE.md</c> §2.1.10 item 5, §5.1 item 2).
///
/// <b>Turn is not incremented here.</b> <c>MATCH3_RULES.md</c> §8.1 says a Turn
/// <i>begins</i> when a Swap is committed; it does not say the stored number
/// advances. §8.3 step 4 places "begin the Turn" before the resolution and §8.4
/// holds <c>Turn</c> "in effect" (not written) throughout it, and §8.3's
/// write of the counters is item 8–9 of the single write-back. The stored value
/// is therefore advanced exactly once by the resolution owner, in the same
/// write-back as <c>Sequence</c> — see <c>BattleStateService.ExecuteSwap</c>.
///
/// <b>RNG.</b> Validation draws nothing and the exchange draws nothing; only the
/// resolution's Spawn step advances <c>RngState</c>, by exactly one selection per
/// spawned cell and by nothing else (<c>MATCH3_RULES.md</c> §7.2 item 1, §4.5
/// item 4). This type adds no draw, no reseed, and no second mechanism
/// (<c>ADR-009</c>).
///
/// <b>Match / Combo accounting uses the resolution's own output.</b> The
/// accounting reads <see cref="CascadeResolver.CascadeResult.Passes"/> — the
/// authoritative pass-by-pass report — and walks it once, counting one Match per
/// <see cref="MatchResolution"/> (<c>MATCH3_RULES.md</c> §6.2 item 1, §6.3
/// item 1). It performs no detection, sorts nothing, counts no cell, and derives
/// nothing from <c>CascadeDepth</c> or from the pass count. See
/// <see cref="AccountMatches"/>.
///
/// <b>Battle Events describe that same resolution.</b> <see cref="BattleEventBuilder"/>
/// reads the identical <c>Passes</c> and produces the ordered events of
/// <c>GAME_EVENTS.md</c> §1.1 — no second detection pass, no re-sort, and no
/// mutation: the events are outputs only and never an alternative source of state
/// (<c>GAME_EVENTS.md</c> §3 item 6). They are carried on the result, so the
/// caller publishes the events of the resolution it just committed without
/// re-running anything.
///
/// <b>The Passive stage's events are not built here.</b> <c>GAME_RULES.md</c> §17
/// places "Charge Passive" at step 10 — after "Count Matches" (step 9) and before
/// "Trigger Relics" (step 11) — and charging belongs to the Passive domain
/// (<c>ARCHITECTURE.md</c> §3 <c>PassiveTracker</c>), driven by the
/// Application-layer pipeline step that owns the <c>§17</c> sequence. This type
/// therefore builds no <c>PassiveCharged</c>/<c>PassiveTriggered</c> event and
/// invents no ordering for it; the stage that charges the Passive hands the
/// assembled list back through <see cref="SwapExecutionResult.WithEvents"/> and
/// <see cref="SwapExecutionResult.WithState"/>, which replace the event list and
/// the state and change nothing else.
/// </summary>
public static class SwapExecutor
{
    /// <summary>
    /// Validates and, when the request is accepted, commits and resolves the Swap
    /// against the battle's current authoritative state
    /// (<c>MATCH3_RULES.md</c> §2.1.6).
    /// </summary>
    /// <param name="state">
    /// The current authoritative state. It is never mutated — it is immutable and
    /// the exchange and resolution are expressed as derived boards
    /// (<c>MATCH3_RULES.md</c> §2 item 2).
    /// </param>
    /// <param name="request">The two §1.0 cell indices the player is exchanging.</param>
    /// <returns>
    /// The rejection (with the validator's reason and no state) or the commit
    /// (with the resolved authoritative state and the resolution's report).
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="state"/> is <c>null</c>. A Swap is executed against an
    /// authoritative battle state; there is no state-less execution.
    /// </exception>
    public static SwapExecutionResult Execute(BattleState state, SwapRequest request)
    {
        ArgumentNullException.ThrowIfNull(state);

        // Step 1–2 (§2.1.6, §8.3 step 1): validate, and stop there when any check
        // fails. The validator owns all four §2.1.2 checks — index range and
        // distinctness, adjacency, already-applied against
        // BattleState.LastCommittedSwapPair, and match-producing — in the
        // documented order, and it draws nothing (§7.2).
        //
        // The committed pair is read from the authoritative state and handed in as
        // the check's input, exactly as §2.1.4 item 2 defines it. The validator
        // only compares it; this type does not evaluate staleness itself, so there
        // is no second implementation of the rule.
        var validation = SwapValidator.Validate(
            state.BoardState,
            state.LastCommittedSwapPair,
            request);

        if (validation.IsRejected)
        {
            // §2.1.5: a rejected action writes nothing, so the state this is
            // evaluated against is the caller's, unchanged. The committed record is
            // neither replaced nor cleared (§2.1.10 items 6–7).
            return SwapExecutionResult.Rejected(validation.Reason);
        }

        // Step 3 (§2.1.6 step 4): commit the exchange. A cell entry is exchanged as
        // a whole (§2.1.5 item 2) — GemType and its SpecialGem metadata together —
        // so a swapped Special Gem keeps its type and orientation at its new cell.
        // The exchange draws no RNG and creates, consumes, and activates no Special
        // Gem: no Special Gem is activated merely because it was swapped.
        var exchanged = state.BoardState.WithSwapped(request.From, request.To);

        // Step 4 (§2.1.6 step 8, §4): resolve the exchanged board until it is
        // stable, using the battle's retained RngState as the point the stream
        // resumes from (GAME_STATE.md §2.6.2 item 2). Spawn is the only step that
        // advances it (§4.5 item 4).
        var rng = new Pcg32(state.RngState.State, state.RngState.Increment);

        // The swap origin is the cell the accepted Swap moved a Gem into
        // (§5.5.3 items 1–2). Both exchanged cells are candidates; the planner
        // resolves, per shape, which of them is a member of that shape's matched
        // line — exactly the documented identification, and why the action's
        // direction is never consulted (§5.5.3 item 6, §2.1.1 item 2). A cell that
        // is not a member gives the shape no origin, and the planner falls back to
        // the line centre (§5.5.3 items 2–3).
        var resolution = CascadeResolver.Resolve(exchanged, rng, swapOriginIndex: null);

        // Step 5 (§6.1 item 2, §6.2–§6.3, §8.3 step 5): the committed Swap's Match
        // and Combo accounting, computed from the resolution's own passes. Combo is
        // swap-scoped — it starts again from 0 for this Swap and ends at the number
        // of Matches this Swap produced — while MatchCount carries the battle's
        // cumulative total forward.
        var playerState = AccountMatches(state.PlayerState, resolution);

        // Step 6 (GAME_RULES.md §17 step 12, COMBAT_RULES.md §2): Generate
        // Resources. The cleared Gems the resolution already recorded are converted
        // into the documented pools — permanently for Power, transiently for the
        // three pools the downstream steps of this same resolution consume.
        //
        // §17 places this after "Charge Passive" (step 10, owned by the
        // Application-layer pipeline step) and before "Relics"/"Update Power". The
        // board stages are sequenced inside this assembly, so the conversion runs
        // here, in the same resolution and before the single write-back — it is not
        // a second pass over the board, it reads the passes the resolution ran.
        var generation = ResourceGenerator.Generate(resolution);

        // COMBAT_RULES.md §2 / GAME_RULES.md §12: the generated Power is written
        // into the persistent PlayerState, clamped to the documented 0–100 range.
        // Base Damage Pool and Defense Pool have no persistent home and are carried
        // on the result instead — they are Transient Resolution State
        // (GAME_STATE.md §3) and are deliberately not written to PlayerState or
        // BattleState. The HealPool is transient too, but it does have a documented
        // consumer in this same resolution: §17 step 14, below.
        playerState = ResourceGenerator.ApplyPower(playerState, generation);

        // Step 6b (GAME_RULES.md §17 step 14, COMBAT_RULES.md §4 item 1): Resolve
        // Player Effects. The HealPool this Swap's HP Gems generated is applied to
        // the persistent HP, clamped to MaxHP — overheal is discarded. It is a pure
        // state write with no event (GAME_EVENTS.md §2 has no heal event) and no
        // RNG: reading the transient pool here does not move it off the result, and
        // it is still not stored in PlayerState or BattleState (GAME_STATE.md §3).
        //
        // It runs in the same resolution, after the generation that produced the
        // pool and before the single write-back below, so `playerState` carries
        // Power and HP together in the one value that is written.
        playerState = ResourceGenerator.ApplyHeal(playerState, generation);

        // Step 7 (GAME_EVENTS.md §1, §1.1, §2): the ordered Battle Events that
        // describe the resolution just performed. They are produced from
        // `resolution` — the same passes the accounting walked — and from the
        // accounted Combo, so they report the committed resolution and nothing
        // else. No detection pass is run, nothing is re-sorted, and the builder
        // mutates nothing: the events are outputs only (GAME_EVENTS.md §3 item 6).
        //
        // They are built AFTER the board is stable and the accounting is complete,
        // so every value they report is a finished resolution's
        // (GAME_STATE.md §5.1 item 2, SIGNALR_PROTOCOL.md §3.1 item 1).
        var events = BattleEventBuilder.Build(resolution, playerState.Combo);

        // Step 7 (§8.3 steps 3–4, 8 and GAME_STATE.md §5.1): the single
        // post-resolution write-back. Board, RngState, Turn, Sequence, the
        // committed pair, and the player's Match/Combo state are written together,
        // after the board is stable, so the new Sequence describes the finished
        // resolution and no reader can observe an in-progress one (§5.1 items 2–3,
        // §8.3 item 1).
        var resolved = state with
        {
            BoardState = resolution.Board,

            // §8.4: Spawn is the step that advances RngState, and the resolution
            // ends with the state Spawn produced.
            RngState = resolution.RngState,

            // §8.1 item 1 / §2 item 5: a committed Swap always begins exactly one
            // new Turn — never one per Match, per pass, or per Cascade.
            Turn = state.Turn + 1,

            // §8.2 item 1: one successfully resolved action increments Sequence by
            // exactly 1, after the board is stable. Not per Match, per pass, or per
            // Cascade.
            Sequence = state.Sequence + 1,

            // §2.2 / §5.3 item 1: the resolution's Match and Combo values, written in
            // the same write-back as the board and the counters — never mid-resolution
            // (§5.1 item 2).
            PlayerState = playerState,

            // §2.1.6 step 4 / GAME_STATE.md §2.1.10 item 5: the committed pair,
            // canonically (min, max) so either argument order records one value
            // (§2.1.10 item 2).
            LastCommittedSwapPair = CommittedSwapPair.FromCells(request.From, request.To),
        };

        return SwapExecutionResult.Committed(resolved, resolution, events, generation);
    }

    /// <summary>
    /// Accounts one committed Swap's Matches into the player's progression state
    /// (<c>GAME_STATE.md</c> §2.2, <c>MATCH3_RULES.md</c> §6).
    ///
    /// <code>
    /// Combo      = 0                       §6.1 item 2 — reset on the committed
    ///                                      Swap, before its first Match is counted
    /// MatchCount = previous MatchCount     §3 — cumulative for the whole battle
    ///         ↓  for every Match, in resolution order
    /// Combo      += 1                      §6.2 item 1
    /// MatchCount += 1                      §6.3 item 1
    /// </code>
    ///
    /// <b>One Match is one <see cref="MatchResolution"/>.</b> The traversal is
    /// <c>Passes</c> in order and, within each pass, its <c>Matches</c> in order —
    /// the resolution's own deterministic ordering (<c>MATCH3_RULES.md</c> §3.2
    /// within a pass, §4.2 across passes). Nothing is re-sorted, and no ordering
    /// is derived from a cell index, a Special Gem type, a cascade depth, or an
    /// animation order.
    ///
    /// <b>What is not counted.</b> <see cref="PassResult.ClearedCellUnion"/> (N
    /// cleared cells are not N Matches), <see cref="PassResult.ActivatedSpecialGems"/>
    /// (an activation, a chain activation, and the cells its effect clears are not
    /// Matches — §5.5.5 item 8, §6.3.1), and <see cref="PassResult.CreatedSpecialGems"/>
    /// (creation is a consequence of an already-counted Match — §6.3 item 3) are all
    /// excluded by construction: only <see cref="PassResult.Matches"/> is read. The
    /// terminating no-match pass is likewise not present in
    /// <see cref="CascadeResolver.CascadeResult.Passes"/>, so it neither counts nor
    /// resets anything (§4.3 item 4, §6.4).
    ///
    /// <b>Not derived from the resolution's shape.</b> <c>Combo</c> is not
    /// <c>Passes.Count</c>, <c>CascadeDepth</c>, or <c>CascadeDepth + 1</c>, and
    /// <c>MatchCount</c> is not a cleared-cell total: several Matches can occur in
    /// one pass and no Match need occur in a later one, so only the per-Match walk
    /// below produces the documented values (<c>MATCH3_RULES.md</c> §6.2 item 2,
    /// §6.3 item 1).
    ///
    /// <b>A committed Swap always leaves Combo ≥ 1</b> (<c>MATCH3_RULES.md</c> §6.5
    /// item 3): a committed Swap is match-producing by validation (§2.1.2 item 4),
    /// so at least one Match is walked. The transient <c>Combo = 0</c> between the
    /// reset and the first Match is internal to this calculation and is never
    /// written to the state or published (§5.1 item 2).
    ///
    /// <b>The combat stats are carried forward, not computed.</b>
    /// <c>GAME_STATE.md</c> §2.2 puts <c>HP</c>/<c>MaxHP</c>, <c>ATK</c>/<c>DEF</c>/
    /// <c>Crit</c>, and <c>Power</c> on the same state this method returns, and the
    /// returned value replaces the previous one wholesale. The Match / Combo stage
    /// owns only <c>Combo</c> and <c>MatchCount</c>, so the rest are copied from
    /// <paramref name="previous"/> unchanged: this method neither re-derives them
    /// (<c>COMBAT_RULES.md</c> §3's Damage Pipeline and §4's healing are still
    /// unimplemented) nor resets them. <c>Power</c> is carried forward here and
    /// then updated by the Resource Generation step that follows
    /// (<c>GAME_RULES.md</c> §17 step 12, <see cref="ResourceGenerator.ApplyPower"/>),
    /// which is the stage that owns it.
    /// </summary>
    /// <param name="previous">
    /// The player's progression state before this Swap — the source of the
    /// cumulative <c>MatchCount</c> and of the Combo value a rejected Swap would
    /// have left in place.
    /// </param>
    /// <param name="resolution">
    /// The committed Swap's resolution, whose <c>Passes</c> are the authoritative
    /// Match boundaries (<c>GAME_STATE.md</c> §3 item 2, TASK-005A §6).
    /// </param>
    /// <returns>
    /// The progression state the committed Swap leaves: this Swap's Match total in
    /// <c>Combo</c> and the battle's cumulative total in <c>MatchCount</c>.
    /// </returns>
    private static PlayerState AccountMatches(
        PlayerState previous,
        CascadeResolver.CascadeResult resolution)
    {
        // §6.1 item 2: the reset belongs to the committed Swap and happens before
        // its first Match is counted. It is applied here — after validation has
        // accepted the action and after the board is resolved — so a rejected Swap
        // never reaches it (§2.1.5 item 5, §6.1 item 3).
        var combo = PlayerState.InitialCombo;

        // §3: MatchCount carries forward; it never resets within a battle.
        var matchCount = previous.MatchCount;

        // §3.2 / §4.2: the passes and their match sets are already in the documented
        // order, so one walk in that order is the authoritative traversal.
        foreach (var pass in resolution.Passes)
        {
            foreach (var match in pass.Matches)
            {
                _ = match; // one MatchResolution is exactly one Match (§5.4 item 4)

                matchCount++;
                combo++;
            }
        }

        return new PlayerState(
            // GAME_STATE.md §2.2 / COMBAT_RULES.md §1.1: resolving a Swap produces
            // Match/Combo accounting only. The combat stats are not an input to this
            // calculation and no Match, Cascade, or Special Gem changes them, so the
            // committed Swap carries the previous values forward unchanged rather than
            // reinitializing them — the write-back is a whole-state replacement, so a
            // value not restated here would be silently reset to its default.
            HP: previous.HP,
            MaxHP: previous.MaxHP,
            ATK: previous.ATK,
            DEF: previous.DEF,
            Power: previous.Power,
            Crit: previous.Crit,
            Combo: combo,
            MatchCount: matchCount);
    }
}