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
/// <c>RelicTriggered</c> — belong to their own owning tasks
/// (<c>GAME_EVENTS.md</c> §3 item 7).
///
/// <b>It is deliberately twelve members, not fifteen.</b> The Boss Response stage
/// adds <see cref="BossSkillCast"/>, <see cref="BattleWon"/>, and
/// <see cref="BattleLost"/>, and <b>no</b> Boss-specific Passive or State event:
/// <c>BOSS_RULES.md</c> §7 states that Boss Passive triggers "use the general
/// Passive events (<c>PASSIVE_RULES.md</c> §7) with <c>source = "boss"</c>", that
/// "no Boss-specific passive event name is needed", and that Boss state changes
/// are inferable from the sequence <c>DamageDealt</c>, <c>DamageTaken</c>,
/// <c>BossSkillCast</c>, and <c>PassiveTriggered</c>. There is therefore no
/// <c>BossPassiveCharged</c>, <c>BossPassiveTriggered</c>, <c>BossBasicAttack</c>,
/// or <c>BossEnraged</c>. A Boss Basic Attack is reported by its damage instance
/// (<c>COMBAT_RULES.md</c> §3.4), and Enrage is state, not an event
/// (<c>BOSS_RULES.md</c> §5 item 4).
///
/// These are the events the board resolution, its Passive stage, the Damage
/// Pipeline, and the Boss Response produce. They are listed in the order
/// <c>GAME_EVENTS.md</c> §1 places them; the enumeration value is a stable
/// identity and is not itself the ordering (ordering is owned by the assembled
/// list, see <see cref="BattleEvent"/>).
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
    /// the receiving side. It is <b>not</b> a second application of damage: the
    /// target's HP is reduced once, by the pipeline, and these two reports
    /// describe that one reduction (<c>GAME_EVENTS.md</c> §3 item 6).
    /// </summary>
    DamageTaken = 8,

    /// <summary>
    /// A Boss Skill resolved (<c>GAME_EVENTS.md</c> §2 <c>BossSkillCast</c>,
    /// <c>BOSS_RULES.md</c> §4, §7; <c>SIGNALR_PROTOCOL.md</c> §3.2.18).
    ///
    /// Its payload is the Skill's identity and the Boss's identity. It is emitted
    /// <b>only when the Skill actually fires</b> — when
    /// <c>SkillCharge ≥ SkillChargeRequirement</c> and <c>SkillCooldown == 0</c>
    /// (<c>GAME_STATE.md</c> §2.4.3, <c>BOSS_RULES.md</c> §6.3) — and it
    /// <b>precedes</b> the damage instance the Skill produced, whose reports carry
    /// <c>source = "boss"</c> and <c>target = "player"</c>
    /// (<c>SIGNALR_PROTOCOL.md</c> §3.2.18 item 3). A Boss Basic Attack emits
    /// <b>no</b> <c>BossSkillCast</c>: it is reported by its damage instance
    /// alone, because no such event as <c>BossBasicAttack</c> exists
    /// (<c>BOSS_RULES.md</c> §7).
    /// </summary>
    BossSkillCast = 9,

    /// <summary>
    /// The battle ended with the Boss defeated (<c>GAME_EVENTS.md</c> §2
    /// <c>BattleWon</c>, <c>GAME_RULES.md</c> §1.4, <c>BOSS_RULES.md</c> §7;
    /// <c>SIGNALR_PROTOCOL.md</c> §3.2.19).
    ///
    /// Its payload is the terminal Boss HP and the terminal Player HP. It is
    /// emitted at the Boss HP terminal check, <b>after</b> the Enrage evaluation
    /// and <b>before</b> any Boss Response: a Boss reduced to 0 HP does not
    /// trigger its Passive, cast its Skill, or make a Basic Attack, so this event
    /// is the resolution's last
    /// (<c>BOSS_RULES.md</c> §5 item 4).
    ///
    /// There is no separate <c>BossEnraged</c> event: Enrage is a state
    /// transition and is inferred from the event sequence
    /// (<c>BOSS_RULES.md</c> §5 item 4, §7).
    /// </summary>
    BattleWon = 10,

    /// <summary>
    /// The battle ended with the player defeated (<c>GAME_EVENTS.md</c> §2
    /// <c>BattleLost</c>, <c>GAME_RULES.md</c> §1.4, <c>BOSS_RULES.md</c> §7;
    /// <c>SIGNALR_PROTOCOL.md</c> §3.2.19).
    ///
    /// Its payload is the terminal Boss HP and the terminal Player HP. It is
    /// emitted at the post-response outcome check, after the Boss Response has
    /// damaged the player, and is the resolution's last event. When both sides
    /// survive, <b>neither</b> outcome event is emitted and the battle continues
    /// (<c>GAME_RULES.md</c> §1.4).
    /// </summary>
    BattleLost = 11,
}

/// <summary>
/// The <c>BossSkillCast</c> payload — the Boss Skill that resolved
/// (<c>GAME_EVENTS.md</c> §2, <c>BOSS_RULES.md</c> §4, §7;
/// <c>SIGNALR_PROTOCOL.md</c> §3.2.18).
///
/// <code>
/// BossSkillCast: skillId, sourceId   (SIGNALR_PROTOCOL.md §3.2.18)
/// </code>
///
/// <b>Both members are identities, and neither is re-derived.</b> <c>SkillId</c>
/// is the value the Boss's definition carries (<c>BOSS_RULES.md</c> §6.4 — e.g.
/// <c>"flame-burst"</c>), which the client uses to look up the Skill's visual and
/// effect description; <c>SourceId</c> is the Boss's <c>BossState.BossId</c> —
/// the display name (e.g. <c>"Hỏa Long"</c>), never a slug (<c>§6.4</c>,
/// <c>SIGNALR_PROTOCOL.md</c> §3.2.18 item 2).
///
/// <b>It carries no effect detail.</b> §3.2.18 item 3 leaves the Skill's damage to
/// the <c>DamageCalculated</c>/<c>DamageDealt</c>/<c>DamageTaken</c> events that
/// follow it in the same batch, with <c>source = "boss"</c> and
/// <c>target = "player"</c>. The Skill's non-damage effects are unimplemented
/// (<c>BOSS_RULES.md</c> §4 item 4), so there is no effect summary member to
/// populate.
///
/// <b>Domain value, not a wire type</b> (<c>GAME_EVENTS.md</c> §3 item 1): the
/// transport layer owns serialization, exactly as for the other payloads here.
/// </summary>
/// <param name="SkillId">
/// The Boss Skill's identity (<c>BOSS_RULES.md</c> §4, §6.4) — the definition's
/// own <c>SkillId</c>, reported rather than recomputed.
/// </param>
/// <param name="SourceId">
/// The Boss's identity (<c>BOSS_RULES.md</c> §6.4,
/// <c>SIGNALR_PROTOCOL.md</c> §3.2.18 item 2) — the display-name
/// <c>BossState.BossId</c>.
/// </param>
public readonly record struct BossSkillCastEvent(string SkillId, string SourceId)
{
    /// <summary>"BossSkillCast (Hỏa Long → flame-burst)" — for test diagnostics only.</summary>
    public override string ToString() => $"BossSkillCast ({SourceId} -> {SkillId})";
}

/// <summary>
/// The <c>BattleWon</c> payload — the battle ended with the Boss defeated
/// (<c>GAME_EVENTS.md</c> §2, <c>GAME_RULES.md</c> §1.4, <c>BOSS_RULES.md</c> §7;
/// <c>SIGNALR_PROTOCOL.md</c> §3.2.19).
///
/// <code>
/// BattleWon: outcome, finalBossHp, finalPlayerHp   (SIGNALR_PROTOCOL.md §3.2.19)
/// </code>
///
/// <b>The two HP values are the terminal ones.</b> <c>GAME_RULES.md</c> §1.4 ends
/// the battle when either side reaches <c>0</c> HP, so this event is emitted at
/// the Boss HP terminal check with the Boss at <c>0</c> and the player at
/// whatever HP the action left. §3.2.19 item 2 makes them "the state values at
/// the moment the battle ended, after all damage from the final action has been
/// applied".
///
/// <b>The <c>outcome</c> member is not carried here.</b> §3.2.19 fixes it as the
/// string <c>"victory"</c> for this event and <c>"defeat"</c> for
/// <see cref="BattleLostEvent"/> — the same names <c>GAME_EVENTS.md</c> §2 item 9
/// uses. Because it is a constant of the event's own type, the wire projection
/// derives it from which event this is; storing it here would be a second
/// spelling of the discriminator (<c>GAME_STATE.md</c> §0 item 5). The reward
/// summary is deferred (§2 item 9) and is not a member.
/// </summary>
/// <param name="FinalBossHp">
/// The Boss's HP at battle end (<c>GAME_STATE.md</c> §2.4) — <c>0</c> for a
/// victory, since reaching <c>0</c> is what ended the battle.
/// </param>
/// <param name="FinalPlayerHp">
/// The player's HP at battle end (<c>GAME_STATE.md</c> §2.2) — the post-action
/// value, which is above <c>0</c> in a victory.
/// </param>
public readonly record struct BattleWonEvent(int FinalBossHp, int FinalPlayerHp)
{
    /// <summary>"BattleWon (boss 0, player 85)" — for test diagnostics only.</summary>
    public override string ToString() => $"BattleWon (boss {FinalBossHp}, player {FinalPlayerHp})";
}

/// <summary>
/// The <c>BattleLost</c> payload — the battle ended with the player defeated
/// (<c>GAME_EVENTS.md</c> §2, <c>GAME_RULES.md</c> §1.4, <c>BOSS_RULES.md</c> §7;
/// <c>SIGNALR_PROTOCOL.md</c> §3.2.19).
///
/// <code>
/// BattleLost: outcome, finalBossHp, finalPlayerHp   (SIGNALR_PROTOCOL.md §3.2.19)
/// </code>
///
/// <b>It is the same shape as <see cref="BattleWonEvent"/>, reported from the
/// other terminal side.</b> §3.2.19 fixes both events' members identically and
/// distinguishes them by <c>outcome</c>; here the player is at <c>0</c> and the
/// Boss carries whatever HP the action left after the Boss Response.
///
/// <b>It is emitted only after the Boss Response.</b> <c>GAME_RULES.md</c> §1.4
/// and <c>BOSS_RULES.md</c> §5 item 4 order the terminal checks as Boss first,
/// then Player: a Boss killed by the player's damage ends the battle before it
/// can respond, so this event cannot be produced on that path. When both sides
/// survive, neither outcome event is emitted.
/// </summary>
/// <param name="FinalBossHp">
/// The Boss's HP at battle end (<c>GAME_STATE.md</c> §2.4) — the post-action
/// value, which is above <c>0</c> (a Boss at <c>0</c> would have ended the
/// battle as a victory before it could respond).
/// </param>
/// <param name="FinalPlayerHp">
/// The player's HP at battle end (<c>GAME_STATE.md</c> §2.2) — <c>0</c> for a
/// defeat, since reaching <c>0</c> is what ended the battle.
/// </param>
public readonly record struct BattleLostEvent(int FinalBossHp, int FinalPlayerHp)
{
    /// <summary>"BattleLost (boss 120, player 0)" — for test diagnostics only.</summary>
    public override string ToString() => $"BattleLost (boss {FinalBossHp}, player {FinalPlayerHp})";
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
/// ├── DamageTaken        the DamageTaken payload, when Type is DamageTaken
/// ├── BossSkillCast      the BossSkillCast payload, when Type is BossSkillCast
/// ├── BattleWon          the BattleWon payload, when Type is BattleWon
/// └── BattleLost         the BattleLost payload, when Type is BattleLost
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
        DamageTakenEvent? damageTaken,
        BossSkillCastEvent? bossSkillCast,
        BattleWonEvent? battleWon,
        BattleLostEvent? battleLost)
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
        _bossSkillCast = bossSkillCast;
        _battleWon = battleWon;
        _battleLost = battleLost;
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
    private readonly BossSkillCastEvent? _bossSkillCast;
    private readonly BattleWonEvent? _battleWon;
    private readonly BattleLostEvent? _battleLost;

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
    /// The <c>BossSkillCast</c> payload — the Skill's identity and the Boss's
    /// identity (<c>GAME_EVENTS.md</c> §2, <c>SIGNALR_PROTOCOL.md</c> §3.2.18).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// This event is not a <see cref="BattleEventType.BossSkillCast"/>.
    /// </exception>
    public BossSkillCastEvent BossSkillCast =>
        _bossSkillCast ?? throw new InvalidOperationException(
            $"A {Type} event carries no BossSkillCast payload (GAME_EVENTS.md §2). "
            + "Check Type before reading BossSkillCast.");

    /// <summary>
    /// The <c>BattleWon</c> payload — the terminal Boss HP and Player HP
    /// (<c>GAME_EVENTS.md</c> §2, <c>SIGNALR_PROTOCOL.md</c> §3.2.19).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// This event is not a <see cref="BattleEventType.BattleWon"/>.
    /// </exception>
    public BattleWonEvent BattleWon =>
        _battleWon ?? throw new InvalidOperationException(
            $"A {Type} event carries no BattleWon payload (GAME_EVENTS.md §2). "
            + "Check Type before reading BattleWon.");

    /// <summary>
    /// The <c>BattleLost</c> payload — the terminal Boss HP and Player HP
    /// (<c>GAME_EVENTS.md</c> §2, <c>SIGNALR_PROTOCOL.md</c> §3.2.19).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// This event is not a <see cref="BattleEventType.BattleLost"/>.
    /// </exception>
    public BattleLostEvent BattleLost =>
        _battleLost ?? throw new InvalidOperationException(
            $"A {Type} event carries no BattleLost payload (GAME_EVENTS.md §2). "
            + "Check Type before reading BattleLost.");

    /// <summary>
    /// A <c>MatchCreated</c> event for one detected Match.
    /// </summary>
    /// <param name="match">
    /// The resolution's own Match record — never a re-detected or re-ordered one
    /// (<c>GAME_EVENTS.md</c> §1.1 item 2, <c>MATCH3_RULES.md</c> §3.2).
    /// </param>
    internal static BattleEvent ForMatch(MatchResolution match) =>
        new(BattleEventType.MatchCreated, match, null, null, null, null, null, null, null, null, null, null, null);

    /// <summary>
    /// A <c>CascadeCreated</c> event for one Cascade pass.
    /// </summary>
    /// <param name="cascadeDepth">
    /// The Cascade's depth index within the Swap (<c>GAME_EVENTS.md</c> §2):
    /// 1 for the Swap's second pass (<c>MATCH3_RULES.md</c> §4.2 item 2).
    /// </param>
    internal static BattleEvent ForCascade(int cascadeDepth) =>
        new(BattleEventType.CascadeCreated, null, cascadeDepth, null, null, null, null, null, null, null, null, null, null);

    /// <summary>
    /// A <c>ComboChanged</c> event carrying the Combo value the Match it follows
    /// produced.
    /// </summary>
    /// <param name="combo">
    /// The <b>new</b> Combo value (<c>GAME_EVENTS.md</c> §2,
    /// <c>MATCH3_RULES.md</c> §6.6 item 2).
    /// </param>
    internal static BattleEvent ForCombo(int combo) =>
        new(BattleEventType.ComboChanged, null, null, combo, null, null, null, null, null, null, null, null, null);

    /// <summary>
    /// A <c>GemMatched</c> event for one consumed Gem.
    /// </summary>
    /// <param name="gem">
    /// The resolution's own <c>GemMatched</c> report — already in the ascending
    /// §1.0 cell-index enumeration order of <c>GAME_EVENTS.md</c> §1.3.
    /// </param>
    internal static BattleEvent ForGem(GemMatchedEvent gem) =>
        new(BattleEventType.GemMatched, null, null, null, gem, null, null, null, null, null, null, null, null);

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
        new(BattleEventType.PassiveCharged, null, null, null, null, charged, null, null, null, null, null, null, null);

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
        new(BattleEventType.PassiveTriggered, null, null, null, null, null, triggered, null, null, null, null, null, null);

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
        new(BattleEventType.DamageCalculated, null, null, null, null, null, null, calculation, null, null, null, null, null);

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
        new(BattleEventType.DamageDealt, null, null, null, null, null, null, null, dealt, null, null, null, null);

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
        new(BattleEventType.DamageTaken, null, null, null, null, null, null, null, null, taken, null, null, null);

    /// <summary>
    /// A <c>BossSkillCast</c> event for one Boss Skill that fired
    /// (<c>GAME_EVENTS.md</c> §2, <c>BOSS_RULES.md</c> §4, §7,
    /// <c>SIGNALR_PROTOCOL.md</c> §3.2.18).
    ///
    /// Public for the same reason as the Damage factories: the Boss Response is the
    /// Application-layer step that owns <c>GAME_RULES.md</c> §17 step 18b, and it
    /// hands the assembled list back through
    /// <see cref="SwapExecutionResult.WithEvents"/>. Both members are identities
    /// carried unchanged from the Boss's configuration and state — neither is
    /// derived here.
    /// </summary>
    /// <param name="skillId">
    /// The Boss Skill's identity, from the Boss's definition
    /// (<c>BOSS_RULES.md</c> §6.4).
    /// </param>
    /// <param name="sourceId">
    /// The Boss's identity — the display-name <c>BossState.BossId</c>
    /// (<c>BOSS_RULES.md</c> §6.4, <c>SIGNALR_PROTOCOL.md</c> §3.2.18 item 2).
    /// </param>
    public static BattleEvent ForBossSkillCast(string skillId, string sourceId) =>
        new(
            BattleEventType.BossSkillCast,
            null, null, null, null, null, null, null, null, null,
            new BossSkillCastEvent(skillId, sourceId),
            null,
            null);

    /// <summary>
    /// A <c>BattleWon</c> event for the Boss HP terminal check
    /// (<c>GAME_EVENTS.md</c> §2, <c>GAME_RULES.md</c> §1.4,
    /// <c>SIGNALR_PROTOCOL.md</c> §3.2.19).
    ///
    /// Public for the same reason as the Damage factories: the outcome check is the
    /// Application layer's, at the terminal step of the resolution. The two HP
    /// values are the resolution's own terminal state, read and reported.
    /// </summary>
    /// <param name="finalBossHp">
    /// The Boss's HP at battle end (<c>GAME_STATE.md</c> §2.4) — <c>0</c> on this
    /// path.
    /// </param>
    /// <param name="finalPlayerHp">
    /// The player's HP at battle end (<c>GAME_STATE.md</c> §2.2).
    /// </param>
    public static BattleEvent ForBattleWon(int finalBossHp, int finalPlayerHp) =>
        new(
            BattleEventType.BattleWon,
            null, null, null, null, null, null, null, null, null, null,
            new BattleWonEvent(finalBossHp, finalPlayerHp),
            null);

    /// <summary>
    /// A <c>BattleLost</c> event for the post-response Player HP terminal check
    /// (<c>GAME_EVENTS.md</c> §2, <c>GAME_RULES.md</c> §1.4,
    /// <c>SIGNALR_PROTOCOL.md</c> §3.2.19).
    ///
    /// Public for the same reason as <see cref="ForBattleWon"/>. It is emitted only
    /// after the Boss Response has damaged the player
    /// (<c>BOSS_RULES.md</c> §5 item 4's ordering), so the Boss HP it reports is the
    /// post-response value.
    /// </summary>
    /// <param name="finalBossHp">
    /// The Boss's HP at battle end (<c>GAME_STATE.md</c> §2.4) — above <c>0</c> on
    /// this path.
    /// </param>
    /// <param name="finalPlayerHp">
    /// The player's HP at battle end (<c>GAME_STATE.md</c> §2.2) — <c>0</c> on this
    /// path.
    /// </param>
    public static BattleEvent ForBattleLost(int finalBossHp, int finalPlayerHp) =>
        new(
            BattleEventType.BattleLost,
            null, null, null, null, null, null, null, null, null, null, null,
            new BattleLostEvent(finalBossHp, finalPlayerHp));

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
        BattleEventType.BossSkillCast => BossSkillCast.ToString(),
        BattleEventType.BattleWon => BattleWon.ToString(),
        BattleEventType.BattleLost => BattleLost.ToString(),
        _ => Type.ToString(),
    };
}