using GameServer.Domain.Battle;
using GameServer.Domain.Combat;
using GameServer.Domain.Passives;
using GameServer.Domain.Relics;

namespace GameServer.Domain.Match3;

/// <summary>
/// The kind of one Battle Event emitted by a committed Swap's board resolution
/// (<c>GAME_EVENTS.md</c> §1, §1.1, §2; <c>GAME_RULES.md</c> §16).
///
/// The set is <b>closed and contract-owned</b>: it is exactly the event
/// names <c>GAME_RULES.md</c> §16 lists and <c>GAME_EVENTS.md</c> §2 defines, and
/// no member may be added for a name the documentation does not define
/// (<c>AGENTS.md</c> §7). The later stages' events are members here too —
/// <see cref="PowerChanged"/> for every stage that owns a <c>PetState.Power</c>
/// mutation and <see cref="RelicTriggered"/> for the Relic stage
/// (<c>GAME_EVENTS.md</c> §3 item 7) — because the model is the one ordered list
/// a resolution emits, not the board stage's private list.
///
/// <b>It is not one member per stage.</b> The Boss Response stage
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
/// <b>What is deliberately absent, and why.</b> The discriminator set is closed at
/// the events <c>GAME_EVENTS.md</c> §2 defines: no event exists for a rule the
/// match-only resolution does not have (<c>SIGNALR_PROTOCOL.md</c> §8 item 7), and
/// no <c>MatchCountChanged</c> exists — the Match count is
/// <c>BattleState.MatchCount</c>, state and not an event
/// (<c>GAME_STATE.md</c> §2.2, <c>GAME_EVENTS.md</c> §3 item 7). No
/// <c>TurnChanged</c>/<c>SequenceChanged</c>/<c>BoardChanged</c> exists —
/// <c>Turn</c>, <c>Sequence</c>, and the board are delivered as state
/// (<c>SIGNALR_PROTOCOL.md</c> §3.1 item 3, §4), and no Match-count event exists
/// because the count is <c>BattleState.MatchCount</c> (<c>GAME_EVENTS.md</c>
/// §2 item 3).
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
    /// <c>MATCH3_RULES.md</c> §3.3 item 3). Emitted once per cleared cell of a
    /// pass's confirmed matched union, in ascending §1.0 cell index
    /// (<c>GAME_EVENTS.md</c> §1.3). Every cleared cell is a matched cell, so every
    /// <c>GemMatched</c> belongs to a Match that also produces
    /// <see cref="MatchCreated"/>.
    /// </summary>
    GemMatched = 3,

    /// <summary>
    /// Passive progress increased (<c>GAME_EVENTS.md</c> §2
    /// <c>PassiveCharged</c>, <c>PASSIVE_RULES.md</c> §2 item 1, §7). Emitted
    /// <b>once per Match</b> — every Match charges the active Pet's Passive by 1
    /// (<c>PASSIVE_RULES.md</c> §2 item 1). It is an <b>informational</b> progress
    /// report (§6 item 1): it
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
    /// Its payload is the terminal Boss HP and the terminal player-side HP — the
    /// active Pet's (<c>GAME_STATE.md</c> §2.3, <c>ADR-011</c> items 3 and 5),
    /// carried under the fixed protocol label <c>finalPlayerHp</c> — and it is
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
    /// Its payload is the terminal Boss HP and the terminal player-side HP — the
    /// active Pet's (<c>GAME_STATE.md</c> §2.3, <c>ADR-011</c> items 3 and 5),
    /// carried under the fixed protocol label <c>finalPlayerHp</c>. It is
    /// emitted at the post-response outcome check, after the Boss Response has
    /// damaged the Pet, and is the resolution's last event. When both sides
    /// survive, <b>neither</b> outcome event is emitted and the battle continues
    /// (<c>GAME_RULES.md</c> §1.4).
    /// </summary>
    BattleLost = 11,

    /// <summary>
    /// A Card cast was validated and applied (<c>GAME_EVENTS.md</c> §2 <c>CardCast</c>,
    /// <c>CARD_RULES.md</c> §3, §6; <c>SIGNALR_PROTOCOL.md</c> §3.2.20).
    /// </summary>
    CardCast = 12,

    /// <summary>
    /// A Pet Signature Skill cast was validated and applied (<c>GAME_EVENTS.md</c> §2 <c>PetSkillCast</c>,
    /// <c>CARD_RULES.md</c> §4, §6; <c>SIGNALR_PROTOCOL.md</c> §3.2.21).
    /// </summary>
    PetSkillCast = 13,

    /// <summary>
    /// A Relic's Effect actually applied (<c>GAME_EVENTS.md</c> §2
    /// <c>RelicTriggered</c>, <c>RELIC_RULES.md</c> §7;
    /// <c>SIGNALR_PROTOCOL.md</c> §3.2.23).
    ///
    /// It is emitted at <c>GAME_RULES.md</c> §17 step 11 ("Trigger Relics") for
    /// each Relic whose effect actually applies, in the deterministic equip-slot
    /// order <c>RELIC_RULES.md</c> §4.2 fixes — **0..N per resolution**, and
    /// nothing at all for a Trigger that fired but whose Condition failed or whose
    /// effect did not apply (<c>RELIC_RULES.md</c> §7).
    ///
    /// Its payload is one identity — the triggered Relic's <b>owned instance</b>
    /// identity, the value <c>PetState.EquippedRelics[]</c> holds
    /// (<c>RELIC_RULES.md</c> §2.2 item 3) — and it carries no
    /// <c>effect summary</c> (§7; <c>SIGNALR_PROTOCOL.md</c> §3.2.23 item 2,
    /// §3.2.25) and no order index (§3.2.23 item 3: the order <b>is</b> the
    /// position in this list).
    /// </summary>
    RelicTriggered = 14,

    /// <summary>
    /// The active Pet's Power changed (<c>GAME_EVENTS.md</c> §2
    /// <c>PowerChanged</c>, <c>GAME_RULES.md</c> §12;
    /// <c>SIGNALR_PROTOCOL.md</c> §3.2.24).
    ///
    /// Its payload is the signed <c>delta</c>, the resulting <c>power</c>, and the
    /// <c>source</c> that owns the mutation. Every authoritative
    /// <c>PetState.Power</c> mutation emits one of these from the stage that owns
    /// it (<c>GAME_EVENTS.md</c> §2 item 4) — the Power stage's Match generation
    /// (<c>source = "match"</c>, §17 step 13), the Card stage's cost and Power
    /// effects (<c>source = "card"</c>, step 14), the Relic stage's <c>Power</c>
    /// effect (<c>source = "relic"</c>, step 11; <c>RELIC_RULES.md</c> §8.5's Mana
    /// Crystal is the provisioned case), and the Boss Response stage's
    /// Power-draining secondary effect (<c>source = "boss"</c>, step 18b;
    /// <c>BOSS_RULES.md</c> §6.3.1 item 2's Drain Power). One mutation produces
    /// one event, in authoritative mutation order
    /// (<c>SIGNALR_PROTOCOL.md</c> §3.2.24 items 5–6).
    /// </summary>
    PowerChanged = 15,

    /// <summary>
    /// One committed board action was accepted and is about to be reported
    /// (<c>GAME_EVENTS.md</c> §1, §2 <c>SwapStarted</c>;
    /// <c>SIGNALR_PROTOCOL.md</c> §3.2.26).
    ///
    /// Its payload is the side that acted and the two §1.0 cells the accepted
    /// action named. It is emitted <b>once per committed board action</b> — the
    /// Player's committed Swap and the Boss Response Phase's board action
    /// (<c>GAME_RULES.md</c> §17 step 18a, ADR-026 §2) — at
    /// <c>MATCH3_RULES.md</c> §2.1.6 step 3: after validation passed and before
    /// the exchange is reported. A rejected action emits <b>no</b> event at all,
    /// so it never emits this one (<c>GAME_EVENTS.md</c> §1.2,
    /// <c>MATCH3_RULES.md</c> §2.1.5 item 6).
    ///
    /// It <b>opens</b> its board cycle: it precedes <see cref="SwapResolved"/>,
    /// which precedes the <see cref="MatchCreated"/>/<see cref="GemMatched"/>/
    /// <see cref="ComboChanged"/>/<see cref="CascadeCreated"/> items of the same
    /// action. The delivered order therefore correlates a board cycle with its
    /// actor without any member on those events, and without inferring a side
    /// from which cycle happened to come first in the batch.
    /// </summary>
    SwapStarted = 16,

    /// <summary>
    /// One committed board action's exchange is committed
    /// (<c>GAME_EVENTS.md</c> §1, §2 <c>SwapResolved</c>;
    /// <c>SIGNALR_PROTOCOL.md</c> §3.2.27).
    ///
    /// Its payload is the delivered outcome — the value
    /// <c>GAME_EVENTS.md</c> §2 names as "whether the swap was committed or
    /// reverted". It is emitted at <c>MATCH3_RULES.md</c> §2.1.6 step 7: the
    /// exchange is committed, the Turn has begun and Combo has reset, and Match
    /// detection (step 8) has not run. It follows its own
    /// <see cref="SwapStarted"/> and precedes that action's board cycle.
    ///
    /// On the delivered path the outcome is always <c>committed</c>: a rejected
    /// action emits no event at all (<c>GAME_EVENTS.md</c> §1.2), so the
    /// <c>reverted</c> case §2 names is unreachable in a batch. The payload is
    /// carried because §2 defines it; it is not a branch a client may use in
    /// place of the state push.
    /// </summary>
    SwapResolved = 17,

    /// <summary>
    /// One detection pass's Gravity and Spawn transition completed
    /// (<c>GAME_EVENTS.md</c> §1.1, §2 <c>CascadeStepResolved</c>;
    /// <c>SIGNALR_PROTOCOL.md</c> §3.2.28; <c>ADR-027</c>).
    ///
    /// Its payload is the pass's own 1-based depth, every survivor that changed
    /// cell (source → destination), and every Gem its Spawn placed (destination
    /// cell + Gem type). It is emitted <b>once per recorded pass</b> — the
    /// passes that detected a Match, which is every <c>CascadeResult.Passes</c>
    /// entry — as that pass's <b>last</b> item: after the pass's
    /// <see cref="MatchCreated"/> / <see cref="GemMatched"/> /
    /// <see cref="ComboChanged"/> items and before the next pass's
    /// <see cref="CascadeCreated"/>. The terminating pass detects no Match and is
    /// not in <c>Passes</c>, so it emits nothing
    /// (<c>MATCH3_RULES.md</c> §4.3 item 4).
    ///
    /// It is emitted only after the pass's Gravity and Spawn have run
    /// (<c>MATCH3_RULES.md</c> §4.1 steps 3–4), so it reports a completed
    /// transition rather than announcing one. It carries <b>no board</b>: the
    /// board travels in the state push and the recovery snapshot alone
    /// (<c>SIGNALR_PROTOCOL.md</c> §3.1 item 3, §3.2.12 item 1), and the pass's
    /// cleared cells already travel in that pass's <see cref="GemMatched"/> run.
    /// Intermediate board configurations stay Transient Resolution State
    /// (<c>GAME_STATE.md</c> §3).
    /// </summary>
    CascadeStepResolved = 18,

    /// <summary>
    /// One committed board action's matched-Gem counts for its whole board cycle
    /// (<c>GAME_EVENTS.md</c> §1.1 item 9, §2 <c>MatchedGemSummary</c>;
    /// <c>SIGNALR_PROTOCOL.md</c> §3.2.29).
    ///
    /// Its payload is the cycle's matched-Gem count per Gem type — the same counts
    /// the action's resource generation was computed from
    /// (<c>COMBAT_RULES.md</c> §2 item 6, <c>MATCH3_RULES.md</c> §5.7, §5.8.2) —
    /// with a type the cycle did not match absent rather than zero. It is emitted
    /// <b>once per committed board action</b> — the Player's committed Swap and
    /// the Boss Response Phase's step-18a board action
    /// (<c>GAME_RULES.md</c> §17 step 18a, <c>ADR-026</c> §2) — as that cycle's
    /// <b>last</b> item: after the cycle's last recorded pass's
    /// <see cref="CascadeStepResolved"/> and before the following stage's events.
    /// Never emitted per pass, per Match, or per Cascade, and never for a rejected
    /// action (it emits nothing at all).
    ///
    /// It carries <b>counts only</b>. Damage, healing, DEF buff magnitude, Power,
    /// Boss SkillCharge, the board, and the actor are all elsewhere — the actor is
    /// the sibling <see cref="SwapStarted"/>, exactly as for every other
    /// board-cycle event — and none of them is a substitute for a matched-Gem
    /// count (<c>GAME_EVENTS.md</c> §2).
    /// </summary>
    MatchedGemSummary = 19,
}

/// <summary>
/// The <c>CascadeStepResolved</c> payload — one detection pass's completed
/// Gravity + Spawn transition (<c>GAME_EVENTS.md</c> §2;
/// <c>SIGNALR_PROTOCOL.md</c> §3.2.28; <c>ADR-027</c>).
///
/// <code>
/// CascadeStepResolved: passDepth, dropMoves[(fromCell, toCell)], spawnedGems[(cellIndex, gemType)]
/// </code>
///
/// <b>Everything here is a report of a step that already ran</b>, read from the
/// pass's own <see cref="GravityResult.DropMoves"/> and
/// <see cref="PassResult.SpawnedGems"/>. Nothing is recomputed, no board is
/// carried, and no gameplay stage consumes it — it exists so a presentation can
/// move the cells the server actually moved, instead of deriving them
/// (<c>AGENTS.md</c> §10, <c>SIGNALR_PROTOCOL.md</c> §3.1 item 5).
///
/// <b>The two arrays state different things and are not interchangeable.</b>
/// <see cref="DropMoves"/> is about survivors that were already on the board;
/// <see cref="SpawnedGems"/> is about Gems the draw placed. Their order is each
/// step's own documented order (<c>MATCH3_RULES.md</c> §4.4 item 1,
/// §4.5 item 2), preserved as delivered — the projection sorts nothing.
/// </summary>
/// <param name="PassDepth">
/// The pass's own 1-based depth: <c>1</c> for the pass on the committed Swap's
/// board (not a Cascade), <c>≥ 2</c> for a Cascade
/// (<c>MATCH3_RULES.md</c> §4.2 items 1–2). It is the value
/// <see cref="MatchResolution.CascadeDepth"/> carries for the same pass, and it
/// is <c>CascadeCreated.cascadeDepth + 1</c> on any pass that has one. It is
/// stated under this name and never as <c>cascadeDepth</c>, because the two
/// existing <c>cascadeDepth</c> members carry two different documented values
/// (<c>SIGNALR_PROTOCOL.md</c> §3.2.7 item 2).
/// </param>
/// <param name="DropMoves">
/// Every survivor the pass's Gravity relocated, in that step's own read order.
/// Empty when the pass moved nothing.
/// </param>
/// <param name="SpawnedGems">
/// Every Gem the pass's Spawn placed, in that step's own fill order. Empty when
/// the pass spawned nothing.
/// </param>
public readonly record struct CascadeStepResolvedEvent(
    int PassDepth,
    IReadOnlyList<DropMove> DropMoves,
    IReadOnlyList<SpawnedGem> SpawnedGems)
{
    /// <summary>
    /// "CascadeStepResolved (depth 2: 3 drops, 3 spawns)" — for test diagnostics
    /// only.
    /// </summary>
    public override string ToString() =>
        $"CascadeStepResolved (depth {PassDepth}: {DropMoves.Count} drops, {SpawnedGems.Count} spawns)";
}

/// <summary>
/// One entry of a <c>MatchedGemSummary</c>: how many Gems of one type one board
/// cycle matched (<c>GAME_EVENTS.md</c> §2; <c>SIGNALR_PROTOCOL.md</c> §3.2.29).
///
/// It exists only for a type the cycle <b>did</b> match, so <see cref="Count"/> is
/// always <c>&gt;= 1</c>; a type the cycle did not match has no entry at all
/// rather than a zero one (<c>GAME_EVENTS.md</c> §2 item 4).
/// </summary>
/// <param name="GemType">The Gem type, one of the four of <c>MATCH3_RULES.md</c> §1.1.</param>
/// <param name="Count">The number of unique cleared cells of that type, <c>&gt;= 1</c>.</param>
public readonly record struct MatchedGemCount(GemType GemType, int Count)
{
    /// <summary>"ATK x5" — for test diagnostics only.</summary>
    public override string ToString() => $"{GemType} x{Count}";
}

/// <summary>
/// The <c>MatchedGemSummary</c> payload — one committed board action's matched-Gem
/// counts for its whole board cycle (<c>GAME_EVENTS.md</c> §1.1 item 9, §2;
/// <c>SIGNALR_PROTOCOL.md</c> §3.2.29).
///
/// <code>
/// MatchedGemSummary: gems[(gemType, count)]   — types the cycle did not match absent
/// </code>
///
/// <b>Everything here is a report of the action's own cleared-Gem accounting.</b>
/// The entries are the four counts
/// <see cref="ResourceGeneration.ClearedGemCounts"/> already aggregated over the
/// cycle's every cascade pass, filtered to the types with a non-zero count and
/// read in the fixed Gem-type order of <c>MATCH3_RULES.md</c> §1.1. Nothing is
/// re-counted, no board is read, and no gameplay stage consumes the value — it
/// exists so a presentation can show the counts a client would otherwise have to
/// re-derive (<c>AGENTS.md</c> §10, <c>SIGNALR_PROTOCOL.md</c> §3.2.29 item 8).
///
/// <b>It is not a combat report.</b> No damage, healing, DEF buff magnitude,
/// Power, or Boss SkillCharge member exists on it, because those are separate
/// gameplay effects with their own events or state and are not substitutes for a
/// matched-Gem count (<c>GAME_EVENTS.md</c> §2 item 6).
/// </summary>
/// <param name="Gems">
/// One entry per Gem type the cycle matched, in the fixed order ATK, DEF, HP,
/// POWER. Empty only for a cycle that cleared nothing — unreachable for a
/// committed action, which is match-producing by validation
/// (<c>MATCH3_RULES.md</c> §2.1.2 item 4), and an empty list is the wire
/// projection's omission (<c>SIGNALR_PROTOCOL.md</c> §3.2.5, §3.2.29 item 2).
/// </param>
public readonly record struct MatchedGemSummaryEvent(IReadOnlyList<MatchedGemCount> Gems)
{
    /// <summary>
    /// "MatchedGemSummary (ATK x5, HP x2)" — for test diagnostics only.
    /// </summary>
    public override string ToString() =>
        Gems.Count == 0
            ? "MatchedGemSummary (none)"
            : $"MatchedGemSummary ({string.Join(", ", Gems)})";
}

/// <summary>
/// The <c>SwapStarted</c> payload — which side acted on the shared board and the
/// two §1.0 cells its accepted action named (<c>GAME_EVENTS.md</c> §2;
/// <c>SIGNALR_PROTOCOL.md</c> §3.2.26).
///
/// <b>The actor is the existing two-sided identity, not a new vocabulary.</b>
/// <see cref="DamageParty"/> is already the acting side for a board action
/// (<c>ResourceGenerator.ApplyActorMatchEffects(DamageParty.Boss, …)</c> is the
/// Boss's own §17 step 18a resolution), and its two values are projected to the
/// lowercase wire names <c>"player"</c>/<c>"boss"</c> by the same rule
/// <c>PowerChanged.source</c> uses. No second enum and no second spelling is
/// introduced (<c>AGENTS.md</c> §9).
/// </summary>
/// <param name="Actor">The side that acted — the Player's Swap or the Boss's board action.</param>
/// <param name="FromCell">
/// The first §1.0 cell the accepted action named, as the action named it
/// (<c>MATCH3_RULES.md</c> §2.1.1 item 1). It is reported, never re-derived.
/// </param>
/// <param name="ToCell">
/// The second §1.0 cell the accepted action named. The unordered pair is the
/// Swap's identity and the action carries no direction field
/// (<c>MATCH3_RULES.md</c> §2.1.1 item 2), so neither cell is a "primary" one.
/// </param>
public readonly record struct SwapStartedEvent(DamageParty Actor, int FromCell, int ToCell)
{
    /// <summary>"SwapStarted (Player 12->13)" — for test diagnostics only.</summary>
    public override string ToString() => $"SwapStarted ({Actor} {FromCell}->{ToCell})";
}

/// <summary>
/// The <c>SwapResolved</c> payload — the delivered outcome of a committed board
/// action, which <c>GAME_EVENTS.md</c> §2 states as "whether the swap was
/// committed or reverted" (<c>SIGNALR_PROTOCOL.md</c> §3.2.27).
///
/// <b>The Domain value is the fact; the wire member is its documented name.</b>
/// The projection writes the contract word (<c>"committed"</c>/<c>"reverted"</c>,
/// §3.2.27 item 2) from this flag. On the delivered path the flag is always
/// <c>true</c> because a rejected action emits no event at all
/// (<c>GAME_EVENTS.md</c> §1.2) — the same reason the event is only ever built
/// on the committed path.
/// </summary>
/// <param name="Committed">
/// Whether the exchange was committed. <c>false</c> (reverted) is unreachable in
/// a delivered batch: a Swap that fails validation emits neither this event nor
/// its <c>SwapStarted</c>.
/// </param>
public readonly record struct SwapResolvedEvent(bool Committed)
{
    /// <summary>"SwapResolved (committed)" — for test diagnostics only.</summary>
    public override string ToString() =>
        $"SwapResolved ({(Committed ? "committed" : "reverted")})";
}

/// <summary>
/// The <c>PetSkillCast</c> payload — confirms the cast Card was the active Pet's Signature Skill
/// (<c>GAME_EVENTS.md</c> §2, <c>CARD_RULES.md</c> §6; <c>SIGNALR_PROTOCOL.md</c> §3.2.21).
/// </summary>
/// <param name="CardId">The cast Signature Skill Card's definition identity.</param>
public readonly record struct PetSkillCastEvent(string CardId)
{
    /// <summary>"PetSkillCast (cardId)" — for test diagnostics only.</summary>
    public override string ToString() => $"PetSkillCast ({CardId})";
}

/// <summary>
/// The <c>CardCast</c> payload — the Card that was cast
/// (<c>GAME_EVENTS.md</c> §2, <c>CARD_RULES.md</c> §3, §6; <c>SIGNALR_PROTOCOL.md</c> §3.2.20).
/// </summary>
/// <param name="CardId">The cast Card's definition identity.</param>
/// <param name="PowerCost">The Power cost paid for the cast.</param>
public readonly record struct CardCastEvent(string CardId, int PowerCost = 0)
{
    /// <summary>"CardCast (cardId)" — for test diagnostics only.</summary>
    public override string ToString() => $"CardCast ({CardId})";
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
/// the canonical technical Identity (e.g. <c>"boss-hoa-long"</c>), never a
/// display name (<c>§6.4</c>, <c>SIGNALR_PROTOCOL.md</c> §3.2.18 item 2).
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
/// <c>SIGNALR_PROTOCOL.md</c> §3.2.18 item 2) — the canonical technical
/// Identity <c>BossState.BossId</c> (e.g. <c>"boss-hoa-long"</c>), never a
/// display name.
/// </param>
public readonly record struct BossSkillCastEvent(string SkillId, string SourceId)
{
    /// <summary>"BossSkillCast (boss-hoa-long → flame-burst)" — for test diagnostics only.</summary>
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
/// The active Pet's HP at battle end (<c>GAME_STATE.md</c> §2.3, <c>ADR-011</c>
/// items 3 and 5) — the post-action
/// value, which is above <c>0</c> in a victory. The member keeps the fixed
/// protocol label <c>finalPlayerHp</c> (<c>SIGNALR_PROTOCOL.md</c> §3.2.19),
/// which names the Player side; the Pet is that side's combat character.
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
/// The active Pet's HP at battle end (<c>GAME_STATE.md</c> §2.3, <c>ADR-011</c>
/// items 3 and 5) — <c>0</c> for a
/// defeat, since reaching <c>0</c> is what ended the battle. The member keeps the
/// fixed protocol label <c>finalPlayerHp</c> (<c>SIGNALR_PROTOCOL.md</c>
/// §3.2.19), which names the Player side; the Pet is that side's combat
/// character.
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
/// ├── BattleLost         the BattleLost payload, when Type is BattleLost
/// ├── SwapStarted        the SwapStarted payload, when Type is SwapStarted
/// ├── SwapResolved       the SwapResolved payload, when Type is SwapResolved
/// └── MatchedGemSummary  the MatchedGemSummary payload, when Type is
///                        MatchedGemSummary   (GAME_EVENTS.md §2)
/// </code>
///
/// <b>This is an output, never state.</b> Emitting, holding, or reading an event
/// mutates nothing: it does not touch <c>BattleState</c>, <c>PetState</c>,
/// <c>PetState</c>, <c>BoardState</c>, the RNG, <c>Turn</c>, <c>Sequence</c>, or
/// <c>LastCommittedSwapPair</c> (<c>GAME_EVENTS.md</c> §3 item 6). An event is
/// never a substitute for the state write-back
/// (<c>SIGNALR_PROTOCOL.md</c> §4 item 6) — the charged progress an event reports
/// is read back into <c>PetState.PassiveProgress</c> by the owning stage, not by
/// the event.
///
/// <b>The payloads are the existing Domain values, not re-derived ones.</b> The
/// <c>MatchCreated</c> payload is the resolution's own
/// <see cref="MatchResolution"/>, so the shape and its cells are the values the
/// resolver already produced (<c>GAME_EVENTS.md</c> §2: "Match shape (cells), Gem
/// type, cascade depth"). The <c>GemMatched</c> payload is the existing
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
        BattleLostEvent? battleLost,
        CardCastEvent? cardCast,
        PetSkillCastEvent? petSkillCast,
        RelicTriggeredEvent? relicTriggered = null,
        PowerChangedEvent? powerChanged = null,
        SwapStartedEvent? swapStarted = null,
        SwapResolvedEvent? swapResolved = null,
        CascadeStepResolvedEvent? cascadeStepResolved = null,
        MatchedGemSummaryEvent? matchedGemSummary = null)
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
        _cardCast = cardCast;
        _petSkillCast = petSkillCast;
        _relicTriggered = relicTriggered;
        _powerChanged = powerChanged;
        _swapStarted = swapStarted;
        _swapResolved = swapResolved;
        _cascadeStepResolved = cascadeStepResolved;
        _matchedGemSummary = matchedGemSummary;
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
    private readonly CardCastEvent? _cardCast;
    private readonly PetSkillCastEvent? _petSkillCast;
    private readonly RelicTriggeredEvent? _relicTriggered;
    private readonly PowerChangedEvent? _powerChanged;
    private readonly SwapStartedEvent? _swapStarted;
    private readonly SwapResolvedEvent? _swapResolved;
    private readonly CascadeStepResolvedEvent? _cascadeStepResolved;
    private readonly MatchedGemSummaryEvent? _matchedGemSummary;

    /// <summary>Which documented event this is.</summary>
    public BattleEventType Type { get; }

    /// <summary>
    /// The <c>MatchCreated</c> payload — the Match that was detected, carrying its
    /// shape (cells), Gem type, and cascade depth (<c>GAME_EVENTS.md</c> §2).
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
    /// The <c>GemMatched</c> payload — the consumed Gem's cell position and Gem
    /// type (<c>GAME_EVENTS.md</c> §2).
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
    /// The <c>BattleWon</c> payload — the terminal Boss HP and the active Pet's HP
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
    /// The <c>BattleLost</c> payload — the terminal Boss HP and the active Pet's HP
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
    /// The <c>CardCast</c> payload — the Card that was cast
    /// (<c>GAME_EVENTS.md</c> §2, <c>CARD_RULES.md</c> §3, §6; <c>SIGNALR_PROTOCOL.md</c> §3.2.20).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// This event is not a <see cref="BattleEventType.CardCast"/>.
    /// </exception>
    public CardCastEvent CardCast =>
        _cardCast ?? throw new InvalidOperationException(
            $"A {Type} event carries no CardCast payload (GAME_EVENTS.md §2). "
            + "Check Type before reading CardCast.");

    /// <summary>
    /// The <c>PetSkillCast</c> payload — the Pet Signature Skill that was cast
    /// (<c>GAME_EVENTS.md</c> §2, <c>CARD_RULES.md</c> §4, §6; <c>SIGNALR_PROTOCOL.md</c> §3.2.21).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// This event is not a <see cref="BattleEventType.PetSkillCast"/>.
    /// </exception>
    public PetSkillCastEvent PetSkillCast =>
        _petSkillCast ?? throw new InvalidOperationException(
            $"A {Type} event carries no PetSkillCast payload (GAME_EVENTS.md §2). "
            + "Check Type before reading PetSkillCast.");

    /// <summary>
    /// The <c>RelicTriggered</c> payload — the Relic whose Effect applied
    /// (<c>GAME_EVENTS.md</c> §2, <c>RELIC_RULES.md</c> §7,
    /// <c>SIGNALR_PROTOCOL.md</c> §3.2.23).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// This event is not a <see cref="BattleEventType.RelicTriggered"/>.
    /// </exception>
    public RelicTriggeredEvent RelicTriggered =>
        _relicTriggered ?? throw new InvalidOperationException(
            $"A {Type} event carries no RelicTriggered payload (GAME_EVENTS.md §2). "
            + "Check Type before reading RelicTriggered.");

    /// <summary>
    /// The <c>PowerChanged</c> payload — the signed change, the resulting Power,
    /// and what changed it (<c>GAME_EVENTS.md</c> §2,
    /// <c>SIGNALR_PROTOCOL.md</c> §3.2.24).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// This event is not a <see cref="BattleEventType.PowerChanged"/>.
    /// </exception>
    public PowerChangedEvent PowerChanged =>
        _powerChanged ?? throw new InvalidOperationException(
            $"A {Type} event carries no PowerChanged payload (GAME_EVENTS.md §2). "
            + "Check Type before reading PowerChanged.");

    /// <summary>
    /// The <c>SwapStarted</c> payload — the side that acted and the two §1.0
    /// cells its accepted action named (<c>GAME_EVENTS.md</c> §2,
    /// <c>SIGNALR_PROTOCOL.md</c> §3.2.26).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// This event is not a <see cref="BattleEventType.SwapStarted"/>.
    /// </exception>
    public SwapStartedEvent SwapStarted =>
        _swapStarted ?? throw new InvalidOperationException(
            $"A {Type} event carries no SwapStarted payload (GAME_EVENTS.md §2). "
            + "Check Type before reading SwapStarted.");

    /// <summary>
    /// The <c>SwapResolved</c> payload — the delivered outcome of the committed
    /// board action this event closes (<c>GAME_EVENTS.md</c> §2,
    /// <c>SIGNALR_PROTOCOL.md</c> §3.2.27).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// This event is not a <see cref="BattleEventType.SwapResolved"/>.
    /// </exception>
    public SwapResolvedEvent SwapResolved =>
        _swapResolved ?? throw new InvalidOperationException(
            $"A {Type} event carries no SwapResolved payload (GAME_EVENTS.md §2). "
            + "Check Type before reading SwapResolved.");

    /// <summary>
    /// The <c>CascadeStepResolved</c> payload — one detection pass's completed
    /// Gravity and Spawn transition: the pass's depth, the survivors it moved, and
    /// the Gems it spawned (<c>GAME_EVENTS.md</c> §2,
    /// <c>SIGNALR_PROTOCOL.md</c> §3.2.28).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// This event is not a <see cref="BattleEventType.CascadeStepResolved"/>.
    /// </exception>
    public CascadeStepResolvedEvent CascadeStep =>
        _cascadeStepResolved ?? throw new InvalidOperationException(
            $"A {Type} event carries no CascadeStepResolved payload (GAME_EVENTS.md §2). "
            + "Check Type before reading CascadeStep.");

    /// <summary>
    /// The <c>MatchedGemSummary</c> payload — one committed board action's
    /// matched-Gem counts for its whole board cycle, one entry per matched Gem
    /// type and no entry for a type it did not match (<c>GAME_EVENTS.md</c> §2,
    /// <c>SIGNALR_PROTOCOL.md</c> §3.2.29).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// This event is not a <see cref="BattleEventType.MatchedGemSummary"/>.
    /// </exception>
    public MatchedGemSummaryEvent MatchedGemSummary =>
        _matchedGemSummary ?? throw new InvalidOperationException(
            $"A {Type} event carries no MatchedGemSummary payload (GAME_EVENTS.md §2). "
            + "Check Type before reading MatchedGemSummary.");

    /// <summary>
    /// A <c>MatchCreated</c> event for one detected Match.
    /// </summary>
    /// <param name="match">
    /// The resolution's own Match record — never a re-detected or re-ordered one
    /// (<c>GAME_EVENTS.md</c> §1.1 item 2, <c>MATCH3_RULES.md</c> §3.2).
    /// </param>
    internal static BattleEvent ForMatch(MatchResolution match) =>
        new(BattleEventType.MatchCreated, match, null, null, null, null, null, null, null, null, null, null, null, null, null);

    /// <summary>
    /// A <c>CascadeCreated</c> event for one Cascade pass.
    /// </summary>
    /// <param name="cascadeDepth">
    /// The Cascade's depth index within the Swap (<c>GAME_EVENTS.md</c> §2):
    /// 1 for the Swap's second pass (<c>MATCH3_RULES.md</c> §4.2 item 2).
    /// </param>
    internal static BattleEvent ForCascade(int cascadeDepth) =>
        new(BattleEventType.CascadeCreated, null, cascadeDepth, null, null, null, null, null, null, null, null, null, null, null, null);

    /// <summary>
    /// A <c>ComboChanged</c> event carrying the Combo value the Match it follows
    /// produced.
    /// </summary>
    /// <param name="combo">
    /// The <b>new</b> Combo value (<c>GAME_EVENTS.md</c> §2,
    /// <c>MATCH3_RULES.md</c> §6.6 item 2).
    /// </param>
    internal static BattleEvent ForCombo(int combo) =>
        new(BattleEventType.ComboChanged, null, null, combo, null, null, null, null, null, null, null, null, null, null, null);

    /// <summary>
    /// A <c>GemMatched</c> event for one consumed Gem.
    /// </summary>
    /// <param name="gem">
    /// The resolution's own <c>GemMatched</c> report — already in the ascending
    /// §1.0 cell-index enumeration order of <c>GAME_EVENTS.md</c> §1.3.
    /// </param>
    internal static BattleEvent ForGem(GemMatchedEvent gem) =>
        new(BattleEventType.GemMatched, null, null, null, gem, null, null, null, null, null, null, null, null, null, null);

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
        new(BattleEventType.PassiveCharged, null, null, null, null, charged, null, null, null, null, null, null, null, null, null);

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
        new(BattleEventType.PassiveTriggered, null, null, null, null, null, triggered, null, null, null, null, null, null, null, null);

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
        new(BattleEventType.DamageCalculated, null, null, null, null, null, null, calculation, null, null, null, null, null, null, null);

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
        new(BattleEventType.DamageDealt, null, null, null, null, null, null, null, dealt, null, null, null, null, null, null);

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
        new(BattleEventType.DamageTaken, null, null, null, null, null, null, null, null, taken, null, null, null, null, null);

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
    /// The Boss's identity — the canonical technical Identity
    /// <c>BossState.BossId</c> (e.g. <c>"boss-hoa-long"</c>), never a display
    /// name (<c>BOSS_RULES.md</c> §6.4, <c>SIGNALR_PROTOCOL.md</c> §3.2.18
    /// item 2).
    /// </param>
    public static BattleEvent ForBossSkillCast(string skillId, string sourceId) =>
        new(
            BattleEventType.BossSkillCast,
            null, null, null, null, null, null, null, null, null,
            new BossSkillCastEvent(skillId, sourceId),
            null,
            null,
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
    /// The active Pet's HP at battle end (<c>GAME_STATE.md</c> §2.3, <c>ADR-011</c>
    /// items 3 and 5), carried under the fixed protocol label
    /// <c>finalPlayerHp</c>.
    /// </param>
    public static BattleEvent ForBattleWon(int finalBossHp, int finalPlayerHp) =>
        new(
            BattleEventType.BattleWon,
            null, null, null, null, null, null, null, null, null, null,
            new BattleWonEvent(finalBossHp, finalPlayerHp),
            null,
            null,
            null);

    /// <summary>
    /// A <c>BattleLost</c> event for the post-response player-side HP terminal
    /// check — the active Pet's <c>PetState.HP</c> (<c>GAME_STATE.md</c> §2.3,
    /// <c>ADR-011</c> items 3 and 5)
    /// (<c>GAME_EVENTS.md</c> §2, <c>GAME_RULES.md</c> §1.4,
    /// <c>SIGNALR_PROTOCOL.md</c> §3.2.19).
    ///
    /// Public for the same reason as <see cref="ForBattleWon"/>. It is emitted only
    /// after the Boss Response has damaged the Pet
    /// (<c>BOSS_RULES.md</c> §5 item 4's ordering), so the Boss HP it reports is the
    /// post-response value.
    /// </summary>
    /// <param name="finalBossHp">
    /// The Boss's HP at battle end (<c>GAME_STATE.md</c> §2.4) — above <c>0</c> on
    /// this path.
    /// </param>
    /// <param name="finalPlayerHp">
    /// The active Pet's HP at battle end (<c>GAME_STATE.md</c> §2.3, <c>ADR-011</c>
    /// items 3 and 5) — <c>0</c> on this
    /// path, carried under the fixed protocol label <c>finalPlayerHp</c>.
    /// </param>
    public static BattleEvent ForBattleLost(int finalBossHp, int finalPlayerHp) =>
        new(
            BattleEventType.BattleLost,
            null, null, null, null, null, null, null, null, null, null, null,
            new BattleLostEvent(finalBossHp, finalPlayerHp),
            null,
            null);

    /// <summary>
    /// A <c>CardCast</c> event for a successfully cast Card
    /// (<c>GAME_EVENTS.md</c> §2, <c>CARD_RULES.md</c> §3, §6;
    /// <c>SIGNALR_PROTOCOL.md</c> §3.2.20).
    /// </summary>
    /// <param name="cardId">The cast Card's definition identity.</param>
    /// <param name="powerCost">The Power cost paid for the cast.</param>
    public static BattleEvent CreateCardCast(string cardId, int powerCost = 0) =>
        new(
            BattleEventType.CardCast,
            null, null, null, null, null, null, null, null, null, null, null, null,
            new CardCastEvent(cardId, powerCost),
            null);

    /// <summary>
    /// A <c>PetSkillCast</c> event for a successfully cast Pet Signature Skill
    /// (<c>GAME_EVENTS.md</c> §2, <c>CARD_RULES.md</c> §4, §6;
    /// <c>SIGNALR_PROTOCOL.md</c> §3.2.21).
    /// </summary>
    /// <param name="cardId">The cast Signature Skill Card's definition identity.</param>
    public static BattleEvent CreatePetSkillCast(string cardId) =>
        new(
            BattleEventType.PetSkillCast,
            null, null, null, null, null, null, null, null, null, null, null, null, null,
            new PetSkillCastEvent(cardId));

    /// <summary>
    /// A <c>PetSkillCast</c> event for a successfully cast Pet Signature Skill.
    /// </summary>
    public static BattleEvent ForPetSkillCast(string cardId) => CreatePetSkillCast(cardId);

    /// <summary>
    /// A <c>RelicTriggered</c> event for one Relic whose Effect actually applied
    /// (<c>GAME_EVENTS.md</c> §2, <c>RELIC_RULES.md</c> §4.2, §7;
    /// <c>SIGNALR_PROTOCOL.md</c> §3.2.23).
    ///
    /// Public for the same reason as the Passive and Damage factories: the Relic
    /// stage is the Application-layer pipeline step that owns
    /// <c>GAME_RULES.md</c> §17 step 11, and it hands the assembled list back
    /// through <see cref="SwapExecutionResult.WithEvents"/>. The identity is the
    /// resolver's own report carried unchanged — no member is added, and the
    /// equip-slot order the events are appended in <b>is</b> the deterministic
    /// order §3.2.23 item 3 reads from position rather than from a payload member.
    /// </summary>
    /// <param name="triggered">
    /// The Relic stage's own report — the triggered Relic's owned instance
    /// identity.
    /// </param>
    public static BattleEvent ForRelicTriggered(RelicTriggeredEvent triggered) =>
        new(
            BattleEventType.RelicTriggered,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            triggered,
            null);

    /// <summary>
    /// A <c>PowerChanged</c> event for one authoritative Power mutation
    /// (<c>GAME_EVENTS.md</c> §2, <c>SIGNALR_PROTOCOL.md</c> §3.2.24).
    ///
    /// Public for the same reason as the Relic factory: the Power write sites are
    /// the stages that own the mutation — the Application-layer pipeline steps for
    /// <c>GAME_RULES.md</c> §17 steps 11, 13, and 18b, and the Domain
    /// <c>CardCastExecutor</c> for step 14, which owns the Card stage's own write.
    /// Every member is the write site's own value — the source that owns the
    /// mutation, the signed change it applied, and the state it left — carried
    /// unchanged; nothing is re-derived here.
    ///
    /// <b>One mutation, one event</b> (<c>GAME_EVENTS.md</c> §2 item 4): a caller
    /// that performs several mutations in one action calls this once per mutation,
    /// in the order the mutations happened. It never sums them into one net report.
    /// </summary>
    /// <param name="changed">
    /// The write site's own report: the source that owns the mutation, the signed
    /// change applied, and the resulting <c>PetState.Power</c>.
    /// </param>
    public static BattleEvent ForPowerChanged(PowerChangedEvent changed) =>
        new(
            BattleEventType.PowerChanged,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            null,
            changed);

    /// <summary>
    /// A <c>SwapStarted</c> event for one accepted board action
    /// (<c>GAME_EVENTS.md</c> §1, §2; <c>SIGNALR_PROTOCOL.md</c> §3.2.26).
    ///
    /// Public for the same reason as the Boss and Relic factories: the two
    /// emitters live on opposite sides of a layer boundary. The Player's
    /// committed Swap is executed by the Domain <c>SwapExecutor</c>, while the
    /// Boss Response Phase's board action is orchestrated by the Application-layer
    /// pipeline step that owns <c>GAME_RULES.md</c> §17 step 18a. Both call this
    /// once, and each hands in its own accepted cells unchanged — the Player's
    /// from the request it validated, the Boss's from the deterministic AI's
    /// selected move. Nothing is re-derived here, and no cell is recomputed from
    /// the board (<c>MATCH3_RULES.md</c> §2.1.1).
    /// </summary>
    /// <param name="actor">
    /// The side that acted — <see cref="DamageParty.Player"/> for the committed
    /// Swap, <see cref="DamageParty.Boss"/> for the step 18a board action.
    /// </param>
    /// <param name="fromCell">The first §1.0 cell the accepted action named.</param>
    /// <param name="toCell">The second §1.0 cell the accepted action named.</param>
    public static BattleEvent ForSwapStarted(DamageParty actor, int fromCell, int toCell) =>
        new(
            BattleEventType.SwapStarted,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            null,
            null,
            new SwapStartedEvent(actor, fromCell, toCell));

    /// <summary>
    /// A <c>SwapResolved</c> event closing one committed board action
    /// (<c>GAME_EVENTS.md</c> §1, §2; <c>SIGNALR_PROTOCOL.md</c> §3.2.27).
    ///
    /// Public for the same reason as <see cref="ForSwapStarted"/>. It is emitted
    /// only on the committed path — a rejected action emits no event at all
    /// (<c>GAME_EVENTS.md</c> §1.2) — which is why the delivered outcome is
    /// always <c>true</c>: the flag is handed in explicitly rather than assumed
    /// here, so the value the wire reports is the one the emitting stage owns.
    /// </summary>
    /// <param name="committed">
    /// Whether the board action's exchange was committed. The delivered path
    /// passes <c>true</c>; a rejected action never reaches this factory.
    /// </param>
    public static BattleEvent ForSwapResolved(bool committed) =>
        new(
            BattleEventType.SwapResolved,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            null,
            null,
            null,
            new SwapResolvedEvent(committed));

    /// <summary>
    /// A <c>CascadeStepResolved</c> event reporting one detection pass's completed
    /// Gravity and Spawn transition (<c>GAME_EVENTS.md</c> §1.1, §2;
    /// <c>SIGNALR_PROTOCOL.md</c> §3.2.28; <c>ADR-027</c>).
    ///
    /// <b>It is built from the pass's own results and derives nothing.</b>
    /// <paramref name="dropMoves"/> is that pass's
    /// <see cref="GravityResult.DropMoves"/> and <paramref name="spawnedGems"/>
    /// its <see cref="PassResult.SpawnedGems"/>, both as the steps produced them —
    /// so the delivered report is the executed transition, never a recomputation
    /// of it (<c>AGENTS.md</c> §10).
    /// </summary>
    /// <param name="passDepth">
    /// The pass's own 1-based detection depth, read from the pass's
    /// <see cref="MatchResolution.CascadeDepth"/> — <c>1</c> for the committed
    /// Swap's pass, <c>≥ 2</c> for a Cascade.
    /// </param>
    /// <param name="dropMoves">The survivors the pass's Gravity relocated.</param>
    /// <param name="spawnedGems">The Gems the pass's Spawn placed.</param>
    public static BattleEvent ForCascadeStepResolved(
        int passDepth,
        IReadOnlyList<DropMove> dropMoves,
        IReadOnlyList<SpawnedGem> spawnedGems) =>
        new(
            BattleEventType.CascadeStepResolved,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            null,
            null,
            null,
            null,
            new CascadeStepResolvedEvent(passDepth, dropMoves, spawnedGems));

    /// <summary>
    /// A <c>MatchedGemSummary</c> event closing one committed board action's board
    /// cycle with that cycle's matched-Gem counts
    /// (<c>GAME_EVENTS.md</c> §1.1 item 9, §2; <c>SIGNALR_PROTOCOL.md</c> §3.2.29).
    ///
    /// <b>It is built from the counts the resource stage already aggregated and
    /// derives nothing.</b> <paramref name="clearedGemCounts"/> is the action's own
    /// <see cref="ResourceGeneration.ClearedGemCounts"/> — the sum over the
    /// cycle's every cascade pass, one per unique cleared cell
    /// (<c>MATCH3_RULES.md</c> §5.7 item 7, §3.3 item 3) — so this factory filters
    /// it to the types the cycle matched and reads them in the fixed Gem-type order
    /// of <c>MATCH3_RULES.md</c> §1.1. It re-counts no cell, reads no board, and
    /// computes no gameplay value: the counts are reported, never produced here
    /// (<c>AGENTS.md</c> §10).
    ///
    /// Public because the two cycle closers live on opposite sides of a layer
    /// boundary, exactly like <see cref="ForSwapStarted"/>: the Player's committed
    /// Swap is closed by the Application-layer pipeline step that owns the
    /// <c>GAME_RULES.md</c> §17 sequence around the Domain <c>SwapExecutor</c>, and
    /// the Boss Response phase's board action is closed by the same pipeline step at
    /// §17 step 18a. Each hands in its own cycle's counts unchanged.
    /// </summary>
    /// <param name="clearedGemCounts">
    /// The committed action's matched-Gem count per type, over the union of its
    /// every pass's cleared cells — the accounting its resource generation was
    /// computed from (<c>COMBAT_RULES.md</c> §2 item 6).
    /// </param>
    public static BattleEvent ForMatchedGemSummary(ClearedGemCounts clearedGemCounts)
    {
        // The four possible entries, in the fixed order MATCH3_RULES.md §1.1 gives
        // the Gem types. A type the cycle did not match contributes no entry
        // (GAME_EVENTS.md §2 item 4), which is why the zero check is the filter and
        // not a value written into the payload.
        var gems = new List<MatchedGemCount>(4);

        AddIfMatched(gems, GemType.Atk, clearedGemCounts.Atk);
        AddIfMatched(gems, GemType.Def, clearedGemCounts.Def);
        AddIfMatched(gems, GemType.Hp, clearedGemCounts.Hp);
        AddIfMatched(gems, GemType.Power, clearedGemCounts.Power);

        return new(
            BattleEventType.MatchedGemSummary,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            null,
            null,
            null,
            null,
            null,
            new MatchedGemSummaryEvent(gems));
    }

    /// <summary>
    /// Appends one <see cref="MatchedGemCount"/> for a type the cycle matched, and
    /// nothing for a type with a zero count
    /// (<c>GAME_EVENTS.md</c> §2 item 4: a zero-count type is absent, not zero).
    /// </summary>
    private static void AddIfMatched(List<MatchedGemCount> gems, GemType gemType, int count)
    {
        if (count > 0)
        {
            gems.Add(new MatchedGemCount(gemType, count));
        }
    }

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
        BattleEventType.CardCast => CardCast.ToString(),
        BattleEventType.PetSkillCast => PetSkillCast.ToString(),
        BattleEventType.RelicTriggered => RelicTriggered.ToString(),
        BattleEventType.PowerChanged => PowerChanged.ToString(),
        BattleEventType.SwapStarted => SwapStarted.ToString(),
        BattleEventType.SwapResolved => SwapResolved.ToString(),
        BattleEventType.CascadeStepResolved => CascadeStep.ToString(),
        BattleEventType.MatchedGemSummary => MatchedGemSummary.ToString(),
        _ => Type.ToString(),
    };
}