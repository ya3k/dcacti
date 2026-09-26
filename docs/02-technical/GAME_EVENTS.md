# Game Events

**Version:** 2.4 (§2 BattleWon/BattleLost `Outcome` value set defined —
exactly `"victory"` (BattleWon) or `"defeat"` (BattleLost), the single
battle-outcome vocabulary shared by `DATABASE.md` §1, `API_CONTRACTS.md`
§4, and `SIGNALR_PROTOCOL.md` §3.2.19 per TASK-050 human Decision C;
prior 2.3: §2 `BossId`/`SourceId` semantics fixed per TASK-046 —
`BattleStarted.BossId`, `PassiveCharged`/`PassiveTriggered.SourceId` for
`source = "boss"`, and `BossSkillCast.SourceId` carry the canonical technical
Boss Identity (`BOSS_RULES.md` §6.4, e.g. `boss-hoa-long`), never a display
name; prior 2.2: §2 BattleWon/BattleLost reward line clarified:
BattleWon-only is the event-payload rule; the REST `rewards` field covers
both outcomes — API_CONTRACTS.md §4, DATABASE.md §1 (TASK-042, ADR-014);
prior 2.1: Player/Pet role model per ADR-011 — BattleWon/BattleLost
trigger: active Pet HP not Player HP; Match/Combo accounting state refs →
BattleState root; wire label `target="player"` documented; prior 2.0: Boss
Response contract resolved — PassiveCharged/PassiveTriggered
source field added, Boss Passive/Skill/Attack timing clarified, Boss→Player
damage events defined)
**Status:** Draft

> This document answers: **"What events exist during gameplay, and in what
> order can they occur?"** The canonical event *names* are listed in
> `GAME_RULES.md` §16; this document owns the detail (purpose, trigger, key
> payload fields, ordering) and is the authority when the two differ on
> anything beyond the name list.

Full handler implementation is not defined here — see `ARCHITECTURE.md`. The
board-resolution mechanics that produce the Match-3 events below are owned by
`MATCH3_RULES.md` §2–§8 and are not restated here.

---

# 1. Event Ordering Within One Resolution

Events are emitted in the same order as the steps in `GAME_RULES.md` §17
(Event Resolution Rules). A single Swap may emit many events; the client
must apply them in the order received (`SIGNALR_PROTOCOL.md`).

```text
BattleStarted            (once, at battle start)
TurnStarted
SwapStarted
SwapResolved
  [per Match, possibly repeated across Cascades:]
  MatchCreated
  MatchResolved
  CascadeCreated          (only if this Match came from a Cascade)
  ComboChanged
  GemMatched               (once per Gem consumed)
PowerChanged
PassiveCharged             (once per Match, per PASSIVE_RULES.md §2)
PassiveTriggered            (only when threshold crossed)
RelicTriggered                (0..N, deterministic order, RELIC_RULES.md §4)
CardCast                       (only for a Card Cast action, not a Swap)
PetSkillCast                    (only when the cast Card is the Signature Skill)
DamageCalculated                (player→boss damage)
DamageDealt                     (player→boss)
DamageTaken                     (player→boss)
  [Boss Response — GAME_RULES.md §17 step 18:]
  PassiveCharged / PassiveTriggered   (Boss Passive, source="boss")
  BossSkillCast                        (only when Skill fires)
  DamageCalculated                     (boss→player, only if Skill or Attack fires)
  DamageDealt                          (boss→player, source="boss", target="player")
  DamageTaken                          (boss→player, source="boss", target="player")
TurnEnded
BattleWon / BattleLost              (only when a HP reaches 0)
```

## 1.1 Board Cycle — The Ordering Contract

The Match-3 part of that list is not a flat sequence: a Swap contains a
repeating cycle, and the list above is the cycle written once. The cycle is
owned by `MATCH3_RULES.md` §4; it is reproduced here **only** to place the
events on it, and the board behaviour must not be read from this diagram.

```text
committed Swap                                  MATCH3_RULES.md §2.1.6
  ├── Turn begins, Combo reset                  §8.1, §6.1
  └── detection pass 1  (depth 1, not a Cascade) §4.2 item 1
        ├── MatchCreated        per Match, in match-set order (§3.2)
        ├── GemMatched          per matched Gem
        ├── ComboChanged        per Match (§6.6 item 2)
        ├── MatchResolved       when that Match's Gems are removed (§4.1)
        ├── PassiveCharged      per Match (PASSIVE_RULES.md §2)
        ├── [PowerChanged]      per resource generation
        ├── GemMatched          per Gem a Special Gem activation clears
        │                       (§5.5.5 item 8: not a Match — no MatchCreated)
        │
        └── gravity + spawn     (no events: MATCH3_RULES.md §4.4 item 7)
              └── detection pass 2 (depth 2)
                    ├── CascadeCreated   once for the pass, first
                    └── … the same cycle, repeated until a pass finds
                          no Match
```

1. **`CascadeCreated` precedes the Matches of its pass.** It reports the pass
   itself, so it is emitted before the cycle it introduces (trigger unchanged:
   "a Match detected after gravity+spawn", §2).
2. **`MatchCreated` precedes `MatchResolved` for the same Match.** Creation is
   detection; resolution is removal (`MATCH3_RULES.md` §3.1 item 2, §4.1
   step 1). Within a pass, both run in the pass's match-set order, so a pass's
   `MatchCreated`/`MatchResolved` pairs appear in that order.
3. **`GemMatched` is per Gem, not per Match**, and is emitted for every Gem
   consumed — including the Gems a Special Gem activation clears, which produce
   `GemMatched` but no `MatchCreated` (`MATCH3_RULES.md` §5.5.5 item 8).
4. **`TurnStarted`/`TurnEnded` bracket the whole resolution** — every Match and
   every Cascade of the Swap sits between them. They mark the Turn, not the
   Match cycle (`GAME_RULES.md` §2).
5. **`ComboChanged` follows the Match it reports** (`MATCH3_RULES.md` §6.6):
   the increment for a Match is emitted after that Match's `MatchCreated` and
   before the next Match's.
6. **A termination pass emits nothing.** The pass that finds no Match
   (`MATCH3_RULES.md` §4.3) is a detection pass with an empty match set: no
   `MatchCreated`, no `CascadeCreated`, no `ComboChanged`
   (`MATCH3_RULES.md` §3.4 item 2).
7. **Symptoms of a wrong order.** `MatchResolved` before its `MatchCreated`,
   `CascadeCreated` after the Matches it introduces, a `ComboChanged` that
   jumps by more than 1, or a `MatchCreated` after the Swap's final Match has
   been resolved are all ordering defects.

## 1.2 Rejected Swap — No Events

1. An action that fails validation emits **no Battle Event at all** — not
   `SwapStarted`, not `SwapResolved`, not any Match-3 event
   (`MATCH3_RULES.md` §2.1.5 item 6). This includes non-adjacent swaps, invalid
   cell indices, already-applied (stale) swaps, and swaps that produce no
   Match.
2. The rejection is reported to the caller only, through the direct invocation
   result (`SIGNALR_PROTOCOL.md` §5). It is not a broadcast and it does not
   appear on the event path.
3. A rejection changes no counters and consumes no RNG
   (`MATCH3_RULES.md` §2.1.5), so nothing downstream can depend on it — the
   event stream of a battle is therefore identical whether or not a rejected
   action was ever sent.

---

## 1.3 `GemMatched` Enumeration Order

**This section owns one question only:** the order of the individual
`GemMatched` events. It does not restate the ordering of any other event, and
it introduces no board rule — detection, Match Resolution, Special Gem
activation and creation, Gravity, and Spawn are owned by `MATCH3_RULES.md`
§2–§5 and are not restated here (`MATCH3_RULES.md` §5.6 is the index of those
orders).

```text
Within one resolved cleared union —
that is, one detection pass's union of matched cells, plus the union of every
activation and chain that pass performed (MATCH3_RULES.md §5.5.5 item 6,
§5.8.2) —

GemMatched is emitted once per cell of the union,
in ascending cell index under MATCH3_RULES.md §1.0:

    lowest index first:  0 → 1 → 2 → … → 63

within each sub-step of the pass, in that sub-step's own order:

    1. the pass's matched cells                        first
    2. the activations of that pass, breadth-first     after
       (MATCH3_RULES.md §5.8.3 level 3, §5.5.5 items 5, 7)
```

1. **The scope is one pass, then one cleared union per sub-step.** A detection
   pass reports its matched cells (`MATCH3_RULES.md` §4.1 step 1) before the
   cells its activations clear (§4.1 step 2), because those are two *sub-steps*
   of the pass and the cycle order is fixed (§1.1). Within each, the cells are
   one union and are enumerated by cell index. The pass is the outer scope: a
   Cascade's next pass (`MATCH3_RULES.md` §4.2) reopens the sequence, and the
   terminating pass emits nothing at all (§1.1 item 6).
2. **Ascending cell index is a total order.** Every cleared cell is a cell in
   `0..63` (`MATCH3_RULES.md` §1.0), no two cells share an index, and there is
   therefore no tie to break — not for an ordinary match, not for overlapping
   effects, not for chains, not at a board edge, and not when several sources
   name the same cell (`MATCH3_RULES.md` §5.5.5 item 6, §5.8.2 item 2: such a
   cell is reported **once**). The order is a property of the **cell**, not of
   the shape, effect, or source that contributed it.
3. **This is an event-enumeration order only.** It is **not** a gameplay
   resolution order. It does not decide which Special Gem a collision creates,
   which effect activates first, or which cells are cleared: those are owned by
   `MATCH3_RULES.md` §3.2, §5.5.1, §5.5.4 item 3, §5.5.5, and §5.8.3, and none
   of them is changed by this section. Because removal, resource generation,
   and reporting are union-based (`MATCH3_RULES.md` §5.8.2), the *set* of
   cleared cells, the resource total, the Match count, the Combo, the board
   state, and the `RngState` are identical under any enumeration order
   (`MATCH3_RULES.md` §5.8.3 item 5); only the byte order of the `GemMatched`
   events depends on it.
4. **It is independent of every unordered source.** The sequence depends only
   on the §1.0 indexing of the cleared cells. It must never depend on
   dictionary or hash iteration order, insertion order, allocation order, set
   or accumulator order, the order in which effects were discovered, the order
   in which shapes contributed a shared cell, filesystem or scheduling order,
   or any other unordered or external source (`MATCH3_RULES.md` §7.2 item 4,
   `AGENTS.md` §11).
5. **It changes no payload and adds no event.** `GemMatched` keeps its payload
   of §2 — cell position and Gem type, plus the Special Gem consumed at that
   cell when the cleared cell held one (§2) — and its once-per-cell trigger.
   This section fixes only the order in which those events appear within a
   cleared union, so the event-stream contract of §1 becomes byte-identical
   between two independent implementations that resolve the same board.
6. **Why an explicit rule is needed.** The ordering levels that were already
   total (match set, activation, chain breadth-first, creation, Gravity, Spawn)
   are indexed by `MATCH3_RULES.md` §5.6 and §5.8.3. The enumeration order
   *inside* one union was not among them: those sections fix the order in which
   shapes and effects **contribute** cells, and §5.8.2 defines the result as a
   **set** whose accounting is by unique cell, without fixing the order of its
   members. Ascending cell index is the smallest rule that closes that gap, and
   it is drawn from the §1.0 indexing every other order in the resolution
   already uses (`MATCH3_RULES.md` §3.2 item 1, §4.4 item 1, §4.5 item 2,
   §5.8.3 level 3a), so it introduces no new convention.
7. **Symptoms of a wrong order.** A `GemMatched` sequence that descends, that
   repeats a cell, that omits a cleared cell, or that depends on the order the
   shapes or effects happened to contribute cells is an ordering defect.
   `MATCH3_RULES.md` §5.8.2 item 1's "removes it *again*" case is the
   corresponding removal defect.

---

# 2. Event Reference

## BattleStarted
```text
Trigger:  Battle session created, before the first Turn
Payload:  BattleId, PetId, BossId, initial BattleState summary
```

`BattleStarted` is a **gameplay event** and requires a created battle with a
Pet and a Boss; its `BossId` payload member is the canonical technical Boss
Identity (`BOSS_RULES.md` §6.4, e.g. `boss-hoa-long`) — never the Boss's
display name. `BattleStarted` is not the foundation-stage state delivery
mechanism: the
initial transmission of Battle State Foundation (`GAME_STATE.md` §2.0) is a
state push on group join, defined in `SIGNALR_PROTOCOL.md` §4 — not an event
on the `ReceiveEvents` path (`GAME_EVENTS.md` §1).

## TurnStarted / TurnEnded
```text
Trigger:  Start/end of a Turn (GAME_RULES.md §2)
Payload:  Turn number
```

A Turn begins when a Swap is **committed**, after validation passes; a
rejected Swap begins no Turn and emits neither event
(`MATCH3_RULES.md` §8.1, §2.1.5 item 6). The Turn covers the whole board
resolution, so `TurnStarted` precedes the Swap's Matches and `TurnEnded`
follows its last Cascade (§1.1).

## SwapStarted / SwapResolved
```text
Trigger:  Client Swap request received / server finished validating+
          committing it (MATCH3_RULES.md §2)
Payload:  SwapStarted: from-cell, to-cell
          SwapResolved: whether the swap was committed or reverted
```

`from-cell`/`to-cell` are the two §1.0 cell indices the action named
(`MATCH3_RULES.md` §2.1.1). `SwapStarted` reports a **received** action,
`SwapResolved` reports its outcome; for a rejected action there is no outcome
to report and both events are omitted (`MATCH3_RULES.md` §2.1.5 item 6).

## MatchCreated / MatchResolved
```text
Trigger:  A Match is detected / its Gems are removed (MATCH3_RULES.md §3)
Payload:  Match shape (cells), Gem type, tier (3/4/5/L-T), Special Gem
          created (if any)
```

Per Match, in the detection pass's match-set order
(`MATCH3_RULES.md` §3.2). A Match is counted when it is detected, which is why
`MatchCreated` and not `MatchResolved` is the event that corresponds to the
Match count (`GAME_RULES.md` §3).

## CascadeCreated
```text
Trigger:  A Match detected after gravity+spawn, i.e. not the first match of
          the Swap (MATCH3_RULES.md §4)
Payload:  Cascade depth index within this Swap
```

Emitted **once per Cascade pass**, before that pass's Matches, and not at all
for the Swap's first pass or for a terminating pass that finds no Match
(`MATCH3_RULES.md` §4.2, §4.3). The depth index is 1 for the Swap's second
pass (`MATCH3_RULES.md` §4.2 item 2).

## ComboChanged
```text
Trigger:  Combo value changes (increment on each Match within one Swap,
          reset to 0 on next Swap — GAME_RULES.md §5)
Payload:  New Combo value
```

The value lifecycle is owned by `MATCH3_RULES.md` §6: it resets to 0 on a
committed Swap before the first Match, and increments by exactly 1 per Match,
in detection order — never by more, never for a Special Gem activation, and
never for a rejected Swap.

## GemMatched
```text
Trigger:  Once per individual Gem consumed by a Match or a Special Gem
          detonation (MATCH3_RULES.md §5.5)
Payload:  Cell position, Gem type, Special Gem consumed at that cell (if any)
```

Once per **cell**, over the union of the cells a step removes — a cell named by
more than one shape or effect is reported once (`MATCH3_RULES.md` §3.3
item 3, §5.5.5 item 6), and the union's events are emitted in ascending cell
index (§1.3). A Gem cleared by a Special Gem activation produces `GemMatched`
without a `MatchCreated` (`MATCH3_RULES.md` §5.5.5 item 8).

1. **`Gem type` is always one of the four §1.1 types, including for a cell that
   held a Special Gem.** A Special Gem adds metadata to a cell's occupant; it
   does not replace the occupant's Gem type (`GAME_STATE.md` §2.1.3 items 1–2,
   §2.1.8 item 6), so this payload is never a Special Gem type in place of a
   Gem type, and it is never absent for a cleared cell.
2. **`Special Gem consumed at that cell` is present only when the cell that was
   cleared held a Special Gem**, and it identifies that Special Gem's type
   (and orientation, for a Line Clear Gem) — the same shape as the cell's
   Special Gem metadata in `GAME_STATE.md` §2.1.4. It is what lets the client
   report and animate an activation, and it is read from the **pre-removal**
   board: the cell is cleared and the Special Gem is consumed by the same act
   (`GAME_STATE.md` §2.1.8 item 2). The member is omitted for an ordinary Gem,
   and its absence is the statement that no Special Gem was consumed there
   (`GAME_STATE.md` §2.1.7 item 3).
3. **This adds no event and changes no ordering.** Activation is reported
   through this existing event and the existing state push — no
   `SpecialGemActivated` event or message exists (`SIGNALR_PROTOCOL.md` §8
   item 7, `MATCH3_RULES.md` §5.9.4 item 7). The trigger, the once-per-cell
   rule, the union scope, and the ascending-index enumeration of §1.3 are all
   unchanged by this member.

## PowerChanged
```text
Trigger:  Power increases or decreases (GAME_RULES.md §12)
Payload:  Delta, new Power value, source (Gem match / Card cost / Relic)
```

## PassiveCharged / PassiveTriggered
```text
Trigger:  Passive progress increases / threshold reached
          (PASSIVE_RULES.md §2, §7; BOSS_RULES.md §3)
Payload:  PassiveCharged: PassiveId, Source (pet | boss),
                          SourceId (PetId | BossId),
                          new progress value, threshold
          PassiveTriggered: PassiveId, Source (pet | boss),
                            SourceId (PetId | BossId),
                            new progress value, threshold,
                            effect summary (deferred — see note)
```

1. **`PassiveId` identifies the Passive that charged or triggered.** A Pet has
   exactly one Passive (`PASSIVE_RULES.md` §1, `GAME_RULES.md` §9), and
   `PetState` names it (`GAME_STATE.md` §2.3). A Boss also has exactly one
   Passive (`BOSS_RULES.md §3`), and `BossState` names it
   (`GAME_STATE.md §2.4`). The field is the same value the owning entity's
   state holds, read and reported — never re-derived, re-numbered, or
   invented by the emitting stage. It is the identity member the sibling
   trigger events already carry (`RelicTriggered`'s `RelicId`, `CardCast`'s
   `CardId`, `BossSkillCast`'s `SkillId`), and it is what lets the client
   attribute a charge or a trigger to the Passive it belongs to.
2. **`Source` discriminates between Pet Passive and Boss Passive.** This
   event is shared by both systems (`PASSIVE_RULES.md` §7, `BOSS_RULES.md`
   §3 item 1). The `Source` field is `"pet"` or `"boss"`, and `SourceId`
   carries the corresponding identity: for `source = "pet"` the Pet
   instance identity `PetState.PetId` (`GAME_STATE.md` §2.3); for
   `source = "boss"` the canonical technical Boss Identity
   `BossState.BossId` (`GAME_STATE.md` §2.4, `BOSS_RULES.md` §6.4 — e.g.
   `boss-hoa-long`, never a display name). Both fields are present in
   every emission — the client uses them to attribute the event to the
   correct entity.
2. **`new progress value` is the progress the increment produced, and `threshold`
   is the Passive's threshold** (`PASSIVE_RULES.md` §1, §2). Both are reported
   as the values the track owns, so the client renders `Progress / Threshold`
   without recomputing either (`PASSIVE_RULES.md` §6 item 1). On a
   `PassiveTriggered`, the progress reported is the value at the moment the
   threshold was crossed — **before** that trigger's own reset
   (`PASSIVE_RULES.md` §2 item 4, §4).
3. **`effect summary` is deferred, and its absence is not an omission.**
   `PASSIVE_RULES.md` §7 defines `PassiveTriggered` as emitted "when the Passive
   activates and its Effect resolves", and what the effect does is owned by the
   Combat/Pet systems (`COMBAT_RULES.md`), not by the Passive tracker. Until
   that stage exists this member is **not populated**, and the event's other
   members are unaffected and fully decodable without it. A reader must not
   treat a missing `effect summary` as "no effect occurred": it means "the
   effect is not yet reported" (`GAME_STATE.md` §0 item 4's staging position,
   applied to an event payload). This is a recorded sequencing position, not a
   scope reduction — the member is added to the emitted value by the Combat
   stage's own task, and the payload list above is not otherwise revised by it.
4. **Boss Passive timing.** Boss Passive fires at Step 18a of
   `GAME_RULES.md` §17, after Player Damage (Steps 15–17) and before Boss
   Skill (Step 18b) and Boss Attack (Step 18c). The Passive evaluates
   against the post-damage battle state. Boss Passive uses the same event
   (`PassiveCharged`/`PassiveTriggered`) with `source = "boss"` — no
   Boss-specific passive event exists (`BOSS_RULES.md` §7).

## RelicTriggered
```text
Trigger:  A Relic's Trigger+Condition is met (RELIC_RULES.md §3, §7)
Payload:  RelicId, effect summary, deterministic order index for this event
```

## CardCast / PetSkillCast
```text
Trigger:  A Card cast is validated and applied (CARD_RULES.md §3, §6)
Payload:  CardId, Power cost paid, effect summary
          PetSkillCast additionally confirms it was the active Pet's
          Signature Skill
```

## DamageCalculated / DamageDealt / DamageTaken
```text
Trigger:  Each stage of the Damage Pipeline (COMBAT_RULES.md §3)
Payload:  DamageCalculated: Base, Combo Modifier, Element Modifier, Other
          Modifiers, Defense, Final Damage (full breakdown, for client
          feedback per GDD Design Philosophy)
          DamageDealt / DamageTaken: source, target, Final Damage amount
```

## BossSkillCast
```text
Trigger:  Boss's own Skill timing rule fires (BOSS_RULES.md §4)
          Charge ≥ Requirement AND Cooldown = 0 (GAME_STATE.md §2.4.3)
Payload:  SkillId, SourceId (BossId), effect summary
```

`SourceId` carries the canonical technical Boss Identity (`BOSS_RULES.md`
§6.4, e.g. `boss-hoa-long`), never the display name — `source` is always
`"boss"` for this event.

The Skill's damage (if any) is reported by separate `DamageCalculated`/
`DamageDealt`/`DamageTaken` events in the same batch, with
`source = "boss"` and `target = "player"` (a fixed wire label meaning the
player's side / active Pet — ADR-011).

## BattleWon / BattleLost
```text
Trigger:  Boss HP or active Pet HP reaches 0 (GAME_RULES.md §1.4 —
          the Pet is the combat character; there is no Player HP pool,
          ADR-011)
Payload:  Outcome, final BattleState summary, reward summary (BattleWon
          only — this is the event-payload rule; the REST response's
          `rewards` field covers both outcomes — API_CONTRACTS.md §4;
          exact reward data shape: DATABASE.md)
```

1. **`Outcome` carries exactly one of two values: `"victory"` (with
   `BattleWon`) or `"defeat"` (with `BattleLost`).** This block owns the
   battle-outcome value set: the persisted `BattleResult.Outcome`
   (`DATABASE.md` §1), the REST `outcome` member (`API_CONTRACTS.md` §4),
   and the SignalR `outcome` wire member (`SIGNALR_PROTOCOL.md` §3.2.19)
   each use these same two values for the same battle.

---

# 3. What This Document Does Not Define

1. Wire-level message envelope (method name, JSON shape sent over SignalR) and
   the **exact wire schema of each event in the batch**: see
   `SIGNALR_PROTOCOL.md` §3 and §3.2. That document is the **single owner** of
   the wire schema (discriminator, property casing, enum representation,
   optionality, and each event's members); this document owns what each event
   means and what its payload contains (§2) and does not restate the wire
   shape. The split is *semantics here, wire schema there* — neither document
   defers to the other, and the schema is not an implementation detail.
2. Whether/how events are persisted as a log: not persisted in MVP (see
   `ARCHITECTURE.md` §5.2 — no event sourcing).
3. Client-side handling of each event: client implementation detail, not a
   documentation concern.
4. The board mechanics behind the Match-3 events — detection, ordering,
   gravity, spawn, Special Gem creation and activation, Combo — owned by
   `MATCH3_RULES.md` §2–§8. How a Special Gem is held in the board state —
   including the type and orientation §2's `GemMatched` payload reports — is
   owned by `GAME_STATE.md` §2.1; this document does not define a
   representation of its own.
5. Which events are internal and which are delivered, and how they are
   batched: `SIGNALR_PROTOCOL.md` §3 owns delivery, and every event defined in
   §2 belongs to the ordered list of §1 — none is defined as presentation-only.
   An event that exists is an event that is delivered in the resolution's
   batch; a value the client needs but no event reports is a gap to be
   resolved by adding an event to `GAME_RULES.md` §16, not by delivering state
   on a side channel.
6. Whether events are authoritative: they are **not** state. The authoritative
   battle state is `BattleState` (`GAME_STATE.md` §2, §5.1); the events
   describe what changed during a resolution, and the client renders them. An
   event is never a substitute for the state write-back, and the state
   write-back is never replaced by an event (`SIGNALR_PROTOCOL.md` §4 item 6,
   ADR-008).
7. **Event emission itself.** The events of §2 are defined here; emitting them
   is a separate stage. The Match / Combo accounting stage
   (`GAME_STATE.md` §2.2) computes and publishes `BattleState.Combo` and
   `BattleState.MatchCount` as **state** and emits no event at all — no
   `MatchCreated`, `MatchResolved`, `CascadeCreated`, or `ComboChanged` is
   produced by it. Nothing in §1.1 or §2 is weakened by that: an event this
   document defines becomes deliverable when the stage that owns emission is
   implemented, and until then the values it would report are read from the
   authoritative state (`SIGNALR_PROTOCOL.md` §4.2). This item records a
   sequencing position, not a scope reduction of §1—§2 — the same position
   `GAME_STATE.md` §0 item 4 takes for absent fields.
