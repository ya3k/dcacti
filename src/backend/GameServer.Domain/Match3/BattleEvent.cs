namespace GameServer.Domain.Match3;

/// <summary>
/// The kind of one Battle Event emitted by a committed Swap's board resolution
/// (<c>GAME_EVENTS.md</c> §1, §1.1, §2; <c>GAME_RULES.md</c> §16).
///
/// The set is <b>closed and contract-owned</b>: it is exactly the Match-3 event
/// names <c>GAME_RULES.md</c> §16 lists and <c>GAME_EVENTS.md</c> §2 defines, and
/// no member may be added for a name the documentation does not define
/// (<c>AGENTS.md</c> §7). Events for later stages — <c>PowerChanged</c>,
/// <c>PassiveCharged</c>, <c>RelicTriggered</c>, the Damage events,
/// <c>BattleWon</c>/<c>BattleLost</c> — belong to their own owning tasks
/// (<c>GAME_EVENTS.md</c> §3 item 7).
///
/// These three are the events the Match-3 resolution itself produces. They are
/// listed in the order <c>GAME_EVENTS.md</c> §1.1 places them; the enumeration
/// value is a stable identity and is not itself the ordering (ordering is owned
/// by the assembled list, see <see cref="BattleEvent"/>).
///
/// <b>What is deliberately absent, and why.</b> A Special Gem activation is
/// reported through <see cref="GemMatched"/> and the existing state push — no
/// <c>SpecialGemActivated</c> event exists (<c>GAME_EVENTS.md</c> §2 item 3,
/// <c>SIGNALR_PROTOCOL.md</c> §8 item 7), and a creation is reported as part of
/// the <c>MatchCreated</c> payload (§2). No <c>MatchCountChanged</c> exists —
/// the Match count is <c>PlayerState.MatchCount</c>, state and not an event
/// (<c>GAME_STATE.md</c> §2.2, <c>GAME_EVENTS.md</c> §3 item 7). No
/// <c>TurnChanged</c>/<c>SequenceChanged</c>/<c>BoardChanged</c> exists —
/// <c>Turn</c>, <c>Sequence</c>, and the board are delivered as state
/// (<c>SIGNALR_PROTOCOL.md</c> §3.1 item 3, §4).
/// </summary>
public enum BattleEventType
{
    /// <summary>
    /// A Match was detected (<c>GAME_EVENTS.md</c> §2 <c>MatchCreated</c>,
    /// <c>MATCH3_RULES.md</c> §3). A Match is counted when it is detected, which
    /// is why this — and not <c>MatchResolved</c> — is the event that corresponds
    /// to the Match count (<c>GAME_RULES.md</c> §3).
    /// </summary>
    MatchCreated = 0,

    /// <summary>
    /// A detection pass produced by Gravity+Spawn found a Match, i.e. not the
    /// first Match of the Swap (<c>GAME_EVENTS.md</c> §2 <c>CascadeCreated</c>,
    /// <c>MATCH3_RULES.md</c> §4.2). Emitted <b>once per Cascade pass</b>, before
    /// that pass's Matches, and not at all for the Swap's first pass or for the
    /// terminating pass that finds no Match (<c>MATCH3_RULES.md</c> §4.2, §4.3).
    /// </summary>
    CascadeCreated = 1,

    /// <summary>
    /// The Combo value changed (<c>GAME_EVENTS.md</c> §2 <c>ComboChanged</c>,
    /// <c>MATCH3_RULES.md</c> §6). Its payload is the <b>new</b> Combo value.
    /// </summary>
    ComboChanged = 2,

    /// <summary>
    /// One Gem was consumed (<c>GAME_EVENTS.md</c> §2 <c>GemMatched</c>,
    /// <c>MATCH3_RULES.md</c> §5.5). Emitted once per cleared cell of a sub-step's
    /// union, in ascending §1.0 cell index (<c>GAME_EVENTS.md</c> §1.3). A Gem
    /// cleared by a Special Gem activation produces this event and
    /// <b>no</b> <see cref="MatchCreated"/> (<c>MATCH3_RULES.md</c> §5.5.5
    /// item 8).
    /// </summary>
    GemMatched = 3,
}

/// <summary>
/// One Battle Event produced by a committed Swap's board resolution
/// (<c>GAME_EVENTS.md</c> §1, §1.1, §2).
///
/// <code>
/// BattleEvent
/// ├── Type           which documented event this is
/// ├── Match          the MatchCreated payload, when Type is MatchCreated
/// ├── CascadeDepth   the CascadeCreated payload, when Type is CascadeCreated
/// ├── Combo          the ComboChanged payload (the NEW value), when Type is
/// │                  ComboChanged
/// ── Gem            the GemMatched payload, when Type is GemMatched
/// </code>
///
/// <b>This is an output, never state.</b> Emitting, holding, or reading an event
/// mutates nothing: it does not touch <c>BattleState</c>, <c>PlayerState</c>,
/// <c>BoardState</c>, the RNG, <c>Turn</c>, <c>Sequence</c>, or
/// <c>LastCommittedSwapPair</c> (<c>GAME_EVENTS.md</c> §3 item 6). An event is
/// never a substitute for the state write-back
/// (<c>SIGNALR_PROTOCOL.md</c> §4 item 6).
///
/// <b>The payloads are the existing Domain values, not re-derived ones.</b> The
/// <c>MatchCreated</c> payload is the resolution's own
/// <see cref="MatchResolution"/>, so the shape, its cells, its tier, and the
/// Special Gems it created are the values the resolver already produced
/// (<c>GAME_EVENTS.md</c> §2: "Match shape (cells), Gem type, tier (3/4/5/L-T),
/// Special Gem created (if any)"). The <c>GemMatched</c> payload is the existing
/// <see cref="GemMatchedEvent"/>. Nothing is recomputed, re-sorted, or read from
/// a second detection pass.
///
/// <b>Only the members the event's own definition carries are populated.</b> The
/// members that do not belong to <see cref="Type"/> are <c>null</c>, and the
/// accessors for them throw rather than returning a plausible default — a
/// <c>CascadeCreated</c> has no Match and a <c>MatchCreated</c> has no cascade
/// depth, and neither may be read as if it did.
///
/// <b>This type is not a wire contract.</b> <c>GAME_EVENTS.md</c> §3 item 1 and
/// <c>SIGNALR_PROTOCOL.md</c> §8 item 1 leave the envelope and the exact JSON
/// schema to the delivery stage; this value is the Domain description of one
/// event, and this task introduces no new SignalR message
/// (<c>SIGNALR_PROTOCOL.md</c> §8 item 7).
/// </summary>
public readonly record struct BattleEvent
{
    private BattleEvent(
        BattleEventType type,
        MatchResolution? match,
        int? cascadeDepth,
        int? combo,
        GemMatchedEvent? gem)
    {
        Type = type;
        _match = match;
        _cascadeDepth = cascadeDepth;
        _combo = combo;
        _gem = gem;
    }

    private readonly MatchResolution? _match;
    private readonly int? _cascadeDepth;
    private readonly int? _combo;
    private readonly GemMatchedEvent? _gem;

    /// <summary>Which documented event this is.</summary>
    public BattleEventType Type { get; }

    /// <summary>
    /// The <c>MatchCreated</c> payload — the Match that was detected, carrying its
    /// shape (cells), Gem type, tier, and the Special Gems it created
    /// (<c>GAME_EVENTS.md</c> §2).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// This event is not a <see cref="BattleEventType.MatchCreated"/>.
    /// </exception>
    public MatchResolution Match =>
        _match ?? throw new InvalidOperationException(
            $"A {Type} event carries no Match (GAME_EVENTS.md §2). "
            + "Check Type before reading Match.");

    /// <summary>
    /// The <c>CascadeCreated</c> payload — the Cascade's depth index within this
    /// Swap, where 1 is the Swap's second pass (<c>GAME_EVENTS.md</c> §2,
    /// <c>MATCH3_RULES.md</c> §4.2 item 2).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// This event is not a <see cref="BattleEventType.CascadeCreated"/>.
    /// </exception>
    public int CascadeDepth =>
        _cascadeDepth ?? throw new InvalidOperationException(
            $"A {Type} event carries no Cascade depth (GAME_EVENTS.md §2). "
            + "Check Type before reading CascadeDepth.");

    /// <summary>
    /// The <c>ComboChanged</c> payload — the <b>new</b> Combo value
    /// (<c>GAME_EVENTS.md</c> §2: "Payload: New Combo value").
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// This event is not a <see cref="BattleEventType.ComboChanged"/>.
    /// </exception>
    public int Combo =>
        _combo ?? throw new InvalidOperationException(
            $"A {Type} event carries no Combo value (GAME_EVENTS.md §2). "
            + "Check Type before reading Combo.");

    /// <summary>
    /// The <c>GemMatched</c> payload — the consumed Gem's cell position, Gem type,
    /// and the Special Gem consumed at that cell when it held one
    /// (<c>GAME_EVENTS.md</c> §2).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// This event is not a <see cref="BattleEventType.GemMatched"/>.
    /// </exception>
    public GemMatchedEvent Gem =>
        _gem ?? throw new InvalidOperationException(
            $"A {Type} event carries no GemMatched payload (GAME_EVENTS.md §2). "
            + "Check Type before reading Gem.");

    /// <summary>
    /// A <c>MatchCreated</c> event for one detected Match.
    /// </summary>
    /// <param name="match">
    /// The resolution's own Match record — never a re-detected or re-ordered one
    /// (<c>GAME_EVENTS.md</c> §1.1 item 2, <c>MATCH3_RULES.md</c> §3.2).
    /// </param>
    internal static BattleEvent ForMatch(MatchResolution match) =>
        new(BattleEventType.MatchCreated, match, null, null, null);

    /// <summary>
    /// A <c>CascadeCreated</c> event for one Cascade pass.
    /// </summary>
    /// <param name="cascadeDepth">
    /// The Cascade's depth index within the Swap (<c>GAME_EVENTS.md</c> §2):
    /// 1 for the Swap's second pass (<c>MATCH3_RULES.md</c> §4.2 item 2).
    /// </param>
    internal static BattleEvent ForCascade(int cascadeDepth) =>
        new(BattleEventType.CascadeCreated, null, cascadeDepth, null, null);

    /// <summary>
    /// A <c>ComboChanged</c> event carrying the Combo value the Match it follows
    /// produced.
    /// </summary>
    /// <param name="combo">
    /// The <b>new</b> Combo value (<c>GAME_EVENTS.md</c> §2,
    /// <c>MATCH3_RULES.md</c> §6.6 item 2).
    /// </param>
    internal static BattleEvent ForCombo(int combo) =>
        new(BattleEventType.ComboChanged, null, null, combo, null);

    /// <summary>
    /// A <c>GemMatched</c> event for one consumed Gem.
    /// </summary>
    /// <param name="gem">
    /// The resolution's own <c>GemMatched</c> report — already in the ascending
    /// §1.0 cell-index enumeration order of <c>GAME_EVENTS.md</c> §1.3.
    /// </param>
    internal static BattleEvent ForGem(GemMatchedEvent gem) =>
        new(BattleEventType.GemMatched, null, null, null, gem);

    /// <summary>"MatchCreated (Horizontal x3 at 25 (Atk))" — for test diagnostics only.</summary>
    public override string ToString() => Type switch
    {
        BattleEventType.MatchCreated => $"MatchCreated ({Match})",
        BattleEventType.CascadeCreated => $"CascadeCreated (depth {CascadeDepth})",
        BattleEventType.ComboChanged => $"ComboChanged (combo {Combo})",
        BattleEventType.GemMatched => $"GemMatched (cell {Gem.CellIndex}, {Gem.GemType})",
        _ => Type.ToString(),
    };
}