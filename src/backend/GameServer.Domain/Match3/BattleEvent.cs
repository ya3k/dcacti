using GameServer.Domain.Combat;
using GameServer.Domain.Passives;

namespace GameServer.Domain.Match3;

/// <summary>
/// The kind of one Battle Event emitted by a committed Swap's board resolution
/// (<c>GAME_EVENTS.md</c> §1, §1.1, §2; <c>GAME_RULES.md</c> §16).
///
/// The set is <b>closed and contract-owned</b>: it is exactly the event
/// names <c>GAME_RULES.md</c> §16 lists and <c>GAME_EVENTS.md</c> §2 defines, and
/// no member may be added for a name the documentation does not define
/// (<c>AGENTS.md</c> §7). Events for later stages — <c>PowerChanged</c>,
/// <c>RelicTriggered</c>, <c>BossSkillCast</c>, <c>BattleWon</c>/<c>BattleLost</c>
/// — belong to their own owning tasks (<c>GAME_EVENTS.md</c> §3 item 7).
///
/// These eight are the events the board resolution, its Passive stage, and the
/// Damage Pipeline produce. They are listed in the order <c>GAME_EVENTS.md</c> §1
/// places them; the enumeration value is a stable identity and is not itself the
/// ordering (ordering is owned by the assembled list, see <see cref="BattleEvent"/>).
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
/// (<c>SIGNALR_PROTOCOL.md</c> §3.1 item 3, §4). No <c>RelicTriggered</c>,
/// <c>CardCast</c>, <c>PetSkillCast</c>, or <c>BossSkillCast</c> exists here —
/// those are other owning stages (<c>GAME_EVENTS.md</c> §2).
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

    /// <summary>
    /// Passive progress increased (<c>GAME_EVENTS.md</c> §2
    /// <c>PassiveCharged</c>, <c>PASSIVE_RULES.md</c> §2 item 1, §7). Emitted
    /// <b>once per Match</b> — every Match charges the active Pet's Passive by 1,
    /// and a Special Gem detonation is not a Match and charges nothing (§2
    /// item 2). It is an <b>informational</b> progress report (§6 item 1): it
    /// triggers no threshold evaluation and no reset, and §2 item 3 evaluates the
    /// Threshold once, after the Cascade's whole batch. Its payload is the
    /// <c>PassiveId</c>, the new progress value, and the Threshold.
    /// </summary>
    PassiveCharged = 4,

    /// <summary>
    /// The Passive's Threshold was reached, so it became Ready and triggered
    /// (<c>GAME_EVENTS.md</c> §2 <c>PassiveTriggered</c>, <c>PASSIVE_RULES.md</c>
    /// §2 item 3, §7). Emitted <b>at most once per Cascade</b>: §2 item 3 and §5
    /// evaluate the Threshold a single time, after all of the Cascade's Matches
    /// have been counted, and §2 item 4 does not re-evaluate what a reset leaves
    /// behind. Its payload is the <c>PassiveId</c>, the progress at the trigger
    /// (the batch total, before the reset), and the Threshold; the effect
    /// summary is deferred to the Combat stage (§2 item 3).
    /// </summary>
    PassiveTriggered = 5,

    /// <summary>
    /// The Damage Pipeline finished pricing one damage instance
    /// (<c>GAME_EVENTS.md</c> §2 <c>DamageCalculated</c>,
    /// <c>COMBAT_RULES.md</c> §3, <c>GAME_RULES.md</c> §17 steps 15–17).
    ///
    /// Its payload is the full documented breakdown — Base, Combo Modifier,
    /// Element Modifier, Other Modifiers, Defense, Final Damage — reported "for
    /// client feedback per GDD Design Philosophy" (§2). It is emitted
    /// <b>once per damage instance</b>, after every step of §3 has run, and it
    /// precedes <see cref="DamageDealt"/> and <see cref="DamageTaken"/> for the
    /// same instance (<c>GAME_EVENTS.md</c> §1).
    /// </summary>
    DamageCalculated = 6,

    /// <summary>
    /// One damage instance's Final Damage was dealt by a source to a target
    /// (<c>GAME_EVENTS.md</c> §2 <c>DamageDealt</c>).
    ///
    /// Its payload is source, target, and the Final Damage amount. It is emitted
    /// for the same instance <see cref="DamageCalculated"/> reported, so the two
    /// carry one <c>FinalDamage</c> value (<c>GAME_EVENTS.md</c> §1).
    /// </summary>
    DamageDealt = 7,

    /// <summary>
    /// One damage instance's Final Damage was taken by a target from a source
    /// (<c>GAME_EVENTS.md</c> §2 <c>DamageTaken</c>).
    ///
    /// Its payload is source, target, and the Final Damage amount — the same
    /// members and the same instance as <see cref="DamageDealt"/>, reported from
    /// the receiving side. It is <b>not</b> a second application of damage: Boss
    /// HP is reduced once, by the pipeline, and these two reports describe that
    /// one reduction (<c>GAME_EVENTS.md</c> §3 item 6).
    /// </summary>
    DamageTaken = 8,
}

/// <summary>
/// One Battle Event produced by a committed Swap's board resolution
/// (<c>GAME_EVENTS.md</c> §1, §1.1, §2).
///
/// <code>
/// BattleEvent
/// ├── Type               which documented event this is
/// ├── Match              the MatchCreated payload, when Type is MatchCreated
/// ├── CascadeDepth       the CascadeCreated payload, when Type is CascadeCreated
/// ├── Combo              the ComboChanged payload (the NEW value), when Type is
/// │                      ComboChanged
/// ├── Gem                the GemMatched payload, when Type is GemMatched
/// ├── PassiveCharged     the PassiveCharged payload, when Type is PassiveCharged
/// ├── PassiveTriggered   the PassiveTriggered payload, when Type is
/// │                      PassiveTriggered
/// ├── DamageCalculated   the DamageCalculated breakdown, when Type is
/// │                      DamageCalculated          (GAME_EVENTS.md §2)
/// ├── DamageDealt        the DamageDealt payload, when Type is DamageDealt
/// └── DamageTaken        the DamageTaken payload, when Type is DamageTaken
/// </code>
///
/// <b>This is an output, never state.</b> Emitting, holding, or reading an event
/// mutates nothing: it does not touch <c>BattleState</c>, <c>PlayerState</c>,
/// <c>PetState</c>, <c>BoardState</c>, the RNG, <c>Turn</c>, <c>Sequence</c>, or
/// <c>LastCommittedSwapPair</c> (<c>GAME_EVENTS.md</c> §3 item 6). An event is
/// never a substitute for the state write-back
/// (<c>SIGNALR_PROTOCOL.md</c> §4 item 6) — the charged progress an event reports
/// is read back into <c>PetState.PassiveProgress</c> by the owning stage, not by
/// the event.
///
/// <b>The payloads are the existing Domain values, not re-derived ones.</b> The
/// <c>MatchCreated</c> payload is the resolution's own
/// <see cref="MatchResolution"/>, so the shape, its cells, its tier, and the
/// Special Gems it created are the values the resolver already produced
/// (<c>GAME_EVENTS.md</c> §2: "Match shape (cells), Gem type, tier (3/4/5/L-T),
/// Special Gem created (if any)"). The <c>GemMatched</c> payload is the existing
/// <see cref="GemMatchedEvent"/>, and the two Passive payloads are the values
/// <c>PassiveTracker</c> produced. Nothing is recomputed, re-sorted, or read from
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
        GemMatchedEvent? gem,
        PassiveChargedEvent? passiveCharged,
        PassiveTriggeredEvent? passiveTriggered,
        DamageCalculation? damageCalculated,
        DamageDealtEvent? damageDealt,
        DamageTakenEvent? damageTaken)
    {
        Type = type;
        _match = match;
        _cascadeDepth = cascadeDepth;
        _combo = combo;
        _gem = gem;
        _passiveCharged = passiveCharged;
        _passiveTriggered = passiveTriggered;
        _damageCalculated = damageCalculated;
        _damageDealt = damageDealt;
        _damageTaken = damageTaken;
    }

    private readonly MatchResolution? _match;
    private readonly int? _cascadeDepth;
    private readonly int? _combo;
    private readonly GemMatchedEvent? _gem;
    private readonly PassiveChargedEvent? _passiveCharged;
    private readonly PassiveTriggeredEvent? _passiveTriggered;
    private readonly DamageCalculation? _damageCalculated;
    private readonly DamageDealtEvent? _damageDealt;
    private readonly DamageTakenEvent? _damageTaken;

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
    /// The <c>PassiveCharged</c> payload — the Passive that charged, the new
    /// progress value, and its Threshold (<c>GAME_EVENTS.md</c> §2).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// This event is not a <see cref="BattleEventType.PassiveCharged"/>.
    /// </exception>
    public PassiveChargedEvent PassiveCharged =>
        _passiveCharged ?? throw new InvalidOperationException(
            $"A {Type} event carries no PassiveCharged payload (GAME_EVENTS.md §2). "
            + "Check Type before reading PassiveCharged.");

    /// <summary>
    /// The <c>PassiveTriggered</c> payload — the Passive that triggered, the
    /// progress at the threshold crossing (before that trigger's reset), and the
    /// Threshold (<c>GAME_EVENTS.md</c> §2).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// This event is not a <see cref="BattleEventType.PassiveTriggered"/>.
    /// </exception>
    public PassiveTriggeredEvent PassiveTriggered =>
        _passiveTriggered ?? throw new InvalidOperationException(
            $"A {Type} event carries no PassiveTriggered payload (GAME_EVENTS.md §2). "
            + "Check Type before reading PassiveTriggered.");

    /// <summary>
    /// The <c>DamageCalculated</c> payload — the full pipeline breakdown: Base,
    /// Combo Modifier, Element Modifier, Other Modifiers, Defense, Final Damage
    /// (<c>GAME_EVENTS.md</c> §2).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// This event is not a <see cref="BattleEventType.DamageCalculated"/>.
    /// </exception>
    public DamageCalculation DamageCalculated =>
        _damageCalculated ?? throw new InvalidOperationException(
            $"A {Type} event carries no DamageCalculated breakdown (GAME_EVENTS.md §2). "
            + "Check Type before reading DamageCalculated.");

    /// <summary>
    /// The <c>DamageDealt</c> payload — source, target, and the Final Damage
    /// amount (<c>GAME_EVENTS.md</c> §2).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// This event is not a <see cref="BattleEventType.DamageDealt"/>.
    /// </exception>
    public DamageDealtEvent DamageDealt =>
        _damageDealt ?? throw new InvalidOperationException(
            $"A {Type} event carries no DamageDealt payload (GAME_EVENTS.md §2). "
            + "Check Type before reading DamageDealt.");

    /// <summary>
    /// The <c>DamageTaken</c> payload — source, target, and the Final Damage
    /// amount (<c>GAME_EVENTS.md</c> §2).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// This event is not a <see cref="BattleEventType.DamageTaken"/>.
    /// </exception>
    public DamageTakenEvent DamageTaken =>
        _damageTaken ?? throw new InvalidOperationException(
            $"A {Type} event carries no DamageTaken payload (GAME_EVENTS.md §2). "
            + "Check Type before reading DamageTaken.");

    /// <summary>
    /// A <c>MatchCreated</c> event for one detected Match.
    /// </summary>
    /// <param name="match">
    /// The resolution's own Match record — never a re-detected or re-ordered one
    /// (<c>GAME_EVENTS.md</c> §1.1 item 2, <c>MATCH3_RULES.md</c> §3.2).
    /// </param>
    internal static BattleEvent ForMatch(MatchResolution match) =>
        new(BattleEventType.MatchCreated, match, null, null, null, null, null, null, null, null);

    /// <summary>
    /// A <c>CascadeCreated</c> event for one Cascade pass.
    /// </summary>
    /// <param name="cascadeDepth">
    /// The Cascade's depth index within the Swap (<c>GAME_EVENTS.md</c> §2):
    /// 1 for the Swap's second pass (<c>MATCH3_RULES.md</c> §4.2 item 2).
    /// </param>
    internal static BattleEvent ForCascade(int cascadeDepth) =>
        new(BattleEventType.CascadeCreated, null, cascadeDepth, null, null, null, null, null, null, null);

    /// <summary>
    /// A <c>ComboChanged</c> event carrying the Combo value the Match it follows
    /// produced.
    /// </summary>
    /// <param name="combo">
    /// The <b>new</b> Combo value (<c>GAME_EVENTS.md</c> §2,
    /// <c>MATCH3_RULES.md</c> §6.6 item 2).
    /// </param>
    internal static BattleEvent ForCombo(int combo) =>
        new(BattleEventType.ComboChanged, null, null, combo, null, null, null, null, null, null);

    /// <summary>
    /// A <c>GemMatched</c> event for one consumed Gem.
    /// </summary>
    /// <param name="gem">
    /// The resolution's own <c>GemMatched</c> report — already in the ascending
    /// §1.0 cell-index enumeration order of <c>GAME_EVENTS.md</c> §1.3.
    /// </param>
    internal static BattleEvent ForGem(GemMatchedEvent gem) =>
        new(BattleEventType.GemMatched, null, null, null, gem, null, null, null, null, null);

    /// <summary>
    /// A <c>PassiveCharged</c> event for one Match's charge
    /// (<c>GAME_EVENTS.md</c> §2, <c>PASSIVE_RULES.md</c> §2 item 1).
    ///
    /// It is <b>public</b>, unlike this type's Match-3 factories: the Passive
    /// stage's events are produced by the Application-layer pipeline step that owns
    /// <c>GAME_RULES.md</c> §17 step 10 "Charge Passive" (the board stages are
    /// sequenced inside this assembly's own <see cref="SwapExecutor"/>, so their
    /// factories can stay internal). The value it builds is the tracker's own
    /// report carried unchanged — no field is added, reordered, or re-derived
    /// here.
    /// </summary>
    /// <param name="charged">
    /// The tracker's own charge report — the Passive identity, the progress this
    /// increment produced, and the Threshold.
    /// </param>
    public static BattleEvent ForPassiveCharged(PassiveChargedEvent charged) =>
        new(BattleEventType.PassiveCharged, null, null, null, null, charged, null, null, null, null);

    /// <summary>
    /// A <c>PassiveTriggered</c> event for one threshold crossing
    /// (<c>GAME_EVENTS.md</c> §2, <c>PASSIVE_RULES.md</c> §2 item 3, §5).
    ///
    /// Public for the same reason as <see cref="ForPassiveCharged"/>: the stage
    /// that charges the Passive hands the assembled list back through
    /// <see cref="SwapExecutionResult.WithEvents"/>.
    /// </summary>
    /// <param name="triggered">
    /// The tracker's own trigger report — the Passive identity, the progress at
    /// the crossing (before that trigger's reset), and the Threshold. It carries
    /// no effect summary: that member is deferred to the Combat stage
    /// (<c>GAME_EVENTS.md</c> §2 item 3).
    /// </param>
    public static BattleEvent ForPassiveTriggered(
        PassiveTriggeredEvent triggered) =>
        new(BattleEventType.PassiveTriggered, null, null, null, null, null, triggered, null, null, null);

    /// <summary>
    /// A <c>DamageCalculated</c> event carrying the full pipeline breakdown
    /// (<c>GAME_EVENTS.md</c> §2, <c>COMBAT_RULES.md</c> §3).
    ///
    /// It is <b>public</b>, like the two Passive factories: the Damage Pipeline is
    /// <c>GAME_RULES.md</c> §17 steps 15–17 and the Application-layer pipeline
    /// step that owns the <c>§17</c> sequence hands the assembled list back
    /// through <see cref="SwapExecutionResult.WithEvents"/>. The breakdown is the
    /// pipeline's own <see cref="DamageCalculation"/> carried unchanged — no
    /// member is added, reordered, or re-derived here.
    /// </summary>
    /// <param name="calculation">
    /// The pipeline's own result: Base, Combo Modifier, Element Modifier, Other
    /// Modifiers, Defense, Final Damage.
    /// </param>
    public static BattleEvent ForDamageCalculated(DamageCalculation calculation) =>
        new(BattleEventType.DamageCalculated, null, null, null, null, null, null, calculation, null, null);

    /// <summary>
    /// A <c>DamageDealt</c> event for one damage instance
    /// (<c>GAME_EVENTS.md</c> §2).
    ///
    /// Public for the same reason as <see cref="ForDamageCalculated"/>.
    /// </summary>
    /// <param name="dealt">
    /// The pipeline's own report — source, target, and the Final Damage amount.
    /// </param>
    public static BattleEvent ForDamageDealt(DamageDealtEvent dealt) =>
        new(BattleEventType.DamageDealt, null, null, null, null, null, null, null, dealt, null);

    /// <summary>
    /// A <c>DamageTaken</c> event for one damage instance
    /// (<c>GAME_EVENTS.md</c> §2).
    ///
    /// Public for the same reason as <see cref="ForDamageCalculated"/>.
    /// </summary>
    /// <param name="taken">
    /// The pipeline's own report — source, target, and the Final Damage amount.
    /// </param>
    public static BattleEvent ForDamageTaken(DamageTakenEvent taken) =>
        new(BattleEventType.DamageTaken, null, null, null, null, null, null, null, null, taken);

    /// <summary>"MatchCreated (Horizontal x3 at 25 (Atk))" — for test diagnostics only.</summary>
    public override string ToString() => Type switch
    {
        BattleEventType.MatchCreated => $"MatchCreated ({Match})",
        BattleEventType.CascadeCreated => $"CascadeCreated (depth {CascadeDepth})",
        BattleEventType.ComboChanged => $"ComboChanged (combo {Combo})",
        BattleEventType.GemMatched => $"GemMatched (cell {Gem.CellIndex}, {Gem.GemType})",
        BattleEventType.PassiveCharged => PassiveCharged.ToString(),
        BattleEventType.PassiveTriggered => PassiveTriggered.ToString(),
        BattleEventType.DamageCalculated =>
            $"DamageCalculated (base {DamageCalculated.Base}, final {DamageCalculated.FinalDamage})",
        BattleEventType.DamageDealt => DamageDealt.ToString(),
        BattleEventType.DamageTaken => DamageTaken.ToString(),
        _ => Type.ToString(),
    };
}