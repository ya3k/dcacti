# Game Events

**Version:** 2.20 (§2 `BossSkillCast` gate terminology reconciled to the Slice 5 removal gate and future removal requirements annotated per **TASK-311 / ADR-029 Slice 5 Removal-Readiness Reconciliation**; no event definitions or wire payloads changed). Prior 2.19: (§2 `PowerChanged` extended per **ADR-029 / TASK-309**, applying
the operator-approved Slice 4 behavior: a Boss Card cast's `PowerDrain` effect
emits the existing authoritative `PowerChanged` for the **active Pet's** Power,
inside the round's single ordered batch and before that cast's `BossCardCast`,
while the Boss's own Power spend mutates `BossState.Power` and therefore emits
**no** `PowerChanged`; no new discriminator, no new payload member, and no Player
`CardCast`/`PetSkillCast` contract change. `BossCardCast` remains the only Boss
cast event, and the `drain-power` ability's transitional legacy `BossSkill` path
keeps its existing event behavior until Slice 5 retirement. **No other event,
payload member, ordering rule of an existing event, delivery path, or gameplay
rule is changed.** Prior 2.18: (§1, §1.1 and §2 extended per **ADR-029 / TASK-303**: the
`BossCardCast` event is defined — the dedicated Boss action event reporting that
the Boss chose and resolved a Boss Card cast in its response phase, carrying the
Boss Card identity and the Boss identity — and §1's Boss Response block records
the Boss's **one-action-per-response-phase** policy (a board match action or a
Boss Card cast, never both). `CardCast`/`PetSkillCast` are confirmed as the
Player's events with their payloads unchanged, and `BossSkillCast` is recorded as
the transitional legacy path's event. **No other event, payload member, ordering
rule of an existing event, delivery path, or gameplay rule is changed**, and no
numeric balance value is authored. Runtime implementation is staged behind
`ADR-029`'s Slice 3. Prior 2.17: (§1, §1.1, §2 extended: `MatchedGemSummary` is defined — one
additive Battle Event reporting, once per committed board action, how many Gems of
each Gem type that action's board cycle matched across all of its cascade passes,
emitted as the cycle's last item after its whole cascade resolution. It reports
the existing matched-Gem accounting (`COMBAT_RULES.md` §2 item 6,
`MATCH3_RULES.md` §5.7, §5.8.2) and carries no damage, healing, DEF buff, Power,
or Boss SkillCharge value. **No other event, payload member, ordering rule,
delivery path, or gameplay rule is changed.** Prior 2.16: (§1.1, §1.3, §2, §3 reconciled per `TASK-289` and `ADR-028`: Special Gems are retired from event definitions and payloads following operator design decisions OD-1 through OD-7. `MatchCreated` removes created special gems; `GemMatched` removes consumed special gems. All event definitions align with match-only resolution without Special Gem activation or detonation. Prior 2.15: §1.1, §1, and §2 updated per the Operator Decision approving TASK-278 A-1(i) and A-4: `CascadeStepResolved` is defined — one additive Battle Event reporting a detection pass's completed Gravity and Spawn, with `passDepth`, `dropMoves` and `spawnedGems`, emitted as that pass's last item inside the round's existing single `ReceiveEvents` batch, and emitting nothing for the terminating pass. **No other event, payload member, ordering rule of an existing event, delivery path, or gameplay rule is changed**: the cycle's own order is unchanged and one pass-closing report is placed on it. Prior 2.14: (§3 item 8 extended per ADR-026 / TASK-267: the operation that emits no Battle Event has a **second** documented case — `MATCH3_RULES.md` §1.6's commit-boundary board recovery — recorded alongside the Boss Response phase's deadlock reshuffle, as the same deterministic regeneration at a second evaluation point. **No event, payload member, ordering rule, delivery path, game rule or implementation is changed**: the item's rule is unchanged and only its example set is completed, so the closed discriminator set (`SIGNALR_PROTOCOL.md` §3.2.2) and `GAME_RULES.md` §16 remain exactly as they were (TASK-268). Prior 2.13 (§3 item 8 added per TASK-265: the `BoardReshuffled` event `ADR-026` §8 promised is recorded as **not existing** — the shared board's deadlock reshuffle is a server-internal operation of the Boss Response phase, it emits no Battle Event, and the board that follows from it reaches the client as the Round's committed board in that Round's single state push. **No event, payload member, ordering rule, delivery path, game rule or implementation is changed**: `GAME_RULES.md` §16, §2 of this document, `SIGNALR_PROTOCOL.md` §3.2.2's closed discriminator set and the code already agree that the event does not exist; only the document that promised it (`ADR-026` §8) is corrected, and this document's own §3 now records the absence and the path a future event would have to take. Prior 2.12: §1 updated per ADR-026 / TASK-259: Event ordering synchronized with shared-board round execution — damage events occur iff acting actor produced a valid ATK-gem match (D1); Boss response phase incorporates Boss board swap and cascades; Boss skill deals zero direct damage (D3); fixed basic attack fallback retired. Prior 2.11: §2 `PowerChanged` reconciled with the TASK-150 Product Owner
decisions D-1–D-8: the `source` value set is now the four values
`"match"`, `"card"`, `"relic"`, and `"boss"`; the owning-stage emission rule
replaces the stale "the `"match"` and `"card"` sources remain their own stages'"
wording; and the one-event-per-mutation and authoritative-ordering rules are
stated. **No event, payload member, or delivery path is changed** — the payload
is still `Delta, new Power value, source`, this revision adds a `source`
*value* and not a member, and no new event or ordering rule is introduced.
Applying the decisions to the wire owner is
`SIGNALR_PROTOCOL.md` §3.2.24; implementing the emission is a separate
downstream task. Prior 2.10: (§2 `RelicTriggered` and `PowerChanged` emission items
synchronized with the landed Relic stage — `GAME_RULES.md` §17 step 11 emits one
`RelicTriggered` per Relic whose effect actually applies, in the equip-slot order
`RELIC_RULES.md` §4.2 fixes, and emits `PowerChanged` with `source: "relic"` where
a Relic's `Power` effect applies (TASK-133). **No event, payload member, ordering
rule, or gameplay semantic is changed by this revision**: both payload lines were
already authored and both emissions conform to them, so only the items that
recorded the emission as *not implemented* are corrected. `RelicTriggered`'s
`effect summary` remains unpopulated and omitted per `SIGNALR_PROTOCOL.md`
§3.2.25. That entry also recorded that the Match and Card `PowerChanged`
sources remained their own stages' — **superseded by 2.11**, which states the
owning-stage emission rule for all four sources.
Prior 2.9: (§2 `CardCast`'s cost item reworded — the value a cast pays is
now the `EffectiveCardCost` `CARD_RULES.md` §3.6 owns (TASK-134 D4 plus the
Product Owner `EffectiveCardCost` truncation decision), not the authored
`CardDefinition.PowerCost` alone; `CardDefinition.PowerCost` remains the
definition-side base value. This is a wording correction only: `CardCast`'s
member set on the wire is unchanged, and the change authorizes no new member,
event, or ordering. Prior 2.8: §2 `CardCast`/`PetSkillCast`, `RelicTriggered`, and
`PowerChanged` payload items authored, and `PassiveTriggered` item 3 reworded —
the wire schema these events are delivered by is now fixed by
`SIGNALR_PROTOCOL.md` §3.2.20–§3.2.25 per the TASK-104 A-1A/A-2C/A-3/A-4B/A-5A
Product Owner rulings, so this document's §2 payload lines were reconciled with
it: the `effect summary` element is described as "not populated yet" rather than
"deferred" (the wire owner's §3.2.25 owns the omission convention, and this
document keeps no second one), the Card cost is confirmed as a definition value
rather than a wire member (A-2C), `PetSkillCast`'s Signature Skill confirmation
is recorded as carried by `CardId` (A-4B), the `CardCast` → `PetSkillCast`
emission order is cross-referenced (A-5A), and `RelicTriggered`'s
"deterministic order index" is recorded as an emission-sequence fact rather
than a payload member (§3.2.23 item 3). No event, trigger, ordering rule, or
gameplay semantic is changed. Prior 2.7: §2 `BattleWon`/`BattleLost` payload pointer updated per
TASK-068 human decision (**Option A**) — the `RewardSummary` defeat-shape
contradiction in `DATABASE.md` §1 is resolved: the Player-track member set
applies to both outcomes, and the deferred part is the Pet-track member list
alone (the earlier "still blocked on unresolved Pet XP decisions" wording was
stale — those decisions were finalized by TASK-062). Prior 2.6: §2 reward-summary item updated per TASK-062 — the Pet XP
reward semantics are now decided, so the item no longer describes the Pet
track as "not finalizable"; the `RewardSummary` Pet member list remains
deferred to the implementation task. Prior 2.5: §2 BattleWon/BattleLost reward-summary item added per
TASK-059 — the payload's reward summary is the single `RewardSummary`
contract owned by `DATABASE.md` §1, whose Player track is decided
(`COMBAT_RULES.md` §7) and whose Pet track is explicitly not finalizable
until the Pet XP reward decisions (`PET_RULES.md` §5.2 items 5–9) are made;
no Pet reward member is frozen. Prior 2.4: §2 BattleWon/BattleLost
`Outcome` value set defined —
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
  CascadeStepResolved      (once per recorded pass, last in that pass — §1.1 item 8)
MatchedGemSummary          (once per board cycle, after its whole cascade — §2)
PowerChanged
PassiveCharged             (once per Match, per PASSIVE_RULES.md §2)
PassiveTriggered            (only when threshold crossed)
RelicTriggered                (0..N, deterministic order, RELIC_RULES.md §4)
CardCast                       (only for a Card Cast action, not a Swap)
PetSkillCast                    (only when the cast Card is the Signature Skill)
DamageCalculated                (player→boss damage, only iff valid ATK match produced; D1)
DamageDealt                     (player→boss, only iff valid ATK match produced)
DamageTaken                     (player→boss, only iff valid ATK match produced)
  [Boss Response Phase — GAME_RULES.md §17 step 18, ADR-026, ADR-029:]
  [the Boss selects exactly ONE action this phase (BOSS_RULES.md §4.3):]
    -- if the selected action is a board match action:
  SwapStarted                          (Boss move on shared board, actor="boss")
  SwapResolved                         (Boss swap resolved)
  [Boss Match / Cascade cycle: MatchCreated, GemMatched, etc.]
  MatchedGemSummary                    (once, after the Boss cycle's whole cascade)
    -- if the selected action is a Boss Card cast: no Boss board cycle happens
  PassiveCharged / PassiveTriggered   (Boss Passive, source="boss"; evaluated
                                       whichever action was selected)
  BossCardCast                        (only when a Boss Card cast resolves; ADR-029)
  BossSkillCast                       (only on the transitional legacy path; BOSS_RULES.md §4.4)
  DamageCalculated                     (boss→player, only iff Boss produced valid ATK match; D1)
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
        │
        ├── gravity + spawn     (Gravity and Spawn emit nothing themselves:
        │                        MATCH3_RULES.md §4.4 item 7, §4.5)
        ├── CascadeStepResolved once, and last in this pass, only after that
        │                       pass's Gravity and Spawn have completed (§2)
        │
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
   consumed by a confirmed match.
4. **`TurnStarted`/`TurnEnded` bracket the whole resolution** — every Match and
   every Cascade of the Swap sits between them. They mark the Turn, not the
   Match cycle (`GAME_RULES.md` §2).
5. **`ComboChanged` follows the Match it reports** (`MATCH3_RULES.md` §6.6):
   the increment for a Match is emitted after that Match's `MatchCreated` and
   before the next Match's.
6. **A termination pass emits nothing.** The pass that finds no Match
   (`MATCH3_RULES.md` §4.3) is a detection pass with an empty match set: no
   `MatchCreated`, no `CascadeCreated`, no `ComboChanged`, and no
   `CascadeStepResolved` — there is no pass transition to report
   (`MATCH3_RULES.md` §3.4 item 2).
7. **Symptoms of a wrong order.** `MatchResolved` before its `MatchCreated`,
   `CascadeCreated` after the Matches it introduces, a `ComboChanged` that
   jumps by more than 1, or a `MatchCreated` after the Swap's final Match has
   been resolved are all ordering defects.
8. **A recorded pass is closed by its `CascadeStepResolved`.** The event is
   emitted **once per recorded pass** and is that pass's **last** item: after
   the pass's `MatchCreated`/`GemMatched`/`ComboChanged` items and after that
   pass's Gravity and Spawn have completed, and therefore before the next
   pass's `CascadeCreated` (or before the following stage's events). It reports
   the transition that closes the pass; it opens nothing, reorders nothing, and
   repeats no other item of the cycle. What it carries and what it deliberately
   omits is §2's; its wire schema is `SIGNALR_PROTOCOL.md` §3.2.28.
9. **A board cycle is closed by its `MatchedGemSummary`.** The event is emitted
   **once per committed board action** — never once per pass, per Match, or per
   `CascadeCreated` — and it is that cycle's **last** item: after the cycle's
   last recorded pass's `CascadeStepResolved`, and therefore before the
   following stage's events (the Player cycle's step 10–17 items, the Boss
   Response's step 18b–18d items). It aggregates every pass of that one cycle
   and nothing else, so two cycles in one round (the Player's and the Boss
   Response Phase's, `ADR-026` §2) produce two summaries whose counts are never
   combined. It reports counts only — not damage, healing, DEF buff magnitude,
   Power, or Boss SkillCharge — and it is the only event that reports a
   per-cycle matched-Gem total. What it carries is §2's; its wire schema is
   `SIGNALR_PROTOCOL.md` §3.2.29.

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
it introduces no board rule — detection, Match Resolution, Gravity, and Spawn
are owned by `MATCH3_RULES.md` §2–§4 and are not restated here. Special Gems are
retired under ADR-028.

```text
Within one resolved cleared union —
that is, one detection pass's union of confirmed matched cells (MATCH3_RULES.md §4) —

GemMatched is emitted once per cell of the union,
in ascending cell index under MATCH3_RULES.md §1.0:

    lowest index first:  0 → 1 → 2 → … → 63
```

1. **The scope is one pass, then one cleared union.** A detection pass reports
   its confirmed matched cells (`MATCH3_RULES.md` §4.1). Within each pass, the
   cells are one union and are enumerated by cell index. The pass is the outer scope: a
   Cascade's next pass (`MATCH3_RULES.md` §4.2) reopens the sequence, and the
   terminating pass emits nothing at all (§1.1 item 6).
2. **Ascending cell index is a total order.** Every cleared cell is a cell in
   `0..63` (`MATCH3_RULES.md` §1.0), no two cells share an index, and there is
   therefore no tie to break — not for an ordinary match, not for overlapping shapes, not at a board edge,
   and not when several shapes name the same cell (`MATCH3_RULES.md` §3.3 item 3:
   such a cell is reported **once**). The order is a property of the **cell**,
   not of the shape that contributed it.
3. **This is an event-enumeration order only.** It is **not** a gameplay
   resolution order. Match detection and confirmed match clearing are owned by
   `MATCH3_RULES.md` §3–§4. Special Gems are retired under ADR-028. Because removal, resource generation,
   and reporting are union-based, the *set* of
   cleared cells, the resource total, the Match count, the Combo, the board
   state, and the `RngState` are identical under any enumeration order; only the byte order of the `GemMatched`
   events depends on it.
4. **It is independent of every unordered source.** The sequence depends only
   on the §1.0 indexing of the cleared cells. It must never depend on
   dictionary or hash iteration order, insertion order, allocation order, set
   or accumulator order, the order in which effects were discovered, the order
   in which shapes contributed a shared cell, filesystem or scheduling order,
   or any other unordered or external source (`MATCH3_RULES.md` §7.2 item 4,
   `AGENTS.md` §11).
5. **It changes no payload and adds no event.** `GemMatched` keeps its payload
   of §2 — cell position and Gem type — and its once-per-cell trigger. Special
   Gems are retired under ADR-028.
   This section fixes only the order in which those events appear within a
   cleared union, so the event-stream contract of §1 becomes byte-identical
   between two independent implementations that resolve the same board.
6. **Why an explicit rule is needed.** The ordering levels that were already
   total (match set, Gravity, Spawn) are defined by `MATCH3_RULES.md` §3–§4.
   The enumeration order *inside* one union was not among them: those sections fix the order in which
   shapes contribute cells, and define the result as a union.
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
Trigger:  SwapStarted: the action passed validation, before its exchange is
          committed; SwapResolved: the exchange is committed, the Turn has
          begun and Combo has reset, before Match detection
          (MATCH3_RULES.md §2.1.6 steps 3 and 7)
Payload:  SwapStarted: actor, from-cell, to-cell
          SwapResolved: whether the swap was committed or reverted
```

`from-cell`/`to-cell` are the two §1.0 cell indices the action named, reported
in the order the action named them: the unordered pair is the Swap's identity
and the action carries no direction field (`MATCH3_RULES.md` §2.1.1 items 1–2).

`actor` is the side that acted on the shared board — `"player"` for the
Player's committed Swap and `"boss"` for the Boss Response Phase's board action
(`GAME_RULES.md` §17 step 18a, `§1` of this document). It is the same two-value
vocabulary segment the damage events and the Passive events use, and it is what
makes a board cycle attributable to the actor that opened it rather than to its
position in the batch.

`SwapStarted` reports an **accepted** action. An action that fails validation
emits no Battle Event at all — not `SwapStarted`, not `SwapResolved`
(`§1.2`, `MATCH3_RULES.md` §2.1.5 item 6) — so the event is never a report of a
request merely received, and `SwapResolved` never reports `reverted` on the
delivered path: the only Swap whose outcome is delivered is one that committed.
Both events are omitted together for a rejected action.

## MatchCreated / MatchResolved
```text
Trigger:  A Match is detected / its Gems are removed (MATCH3_RULES.md §3)
Payload:  Match shape (cells), Gem type, tier (3/4/5/L-T)
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
in detection order — never by more, and never for a rejected Swap.

## GemMatched
```text
Trigger:  Once per individual Gem consumed by a confirmed Match (MATCH3_RULES.md §4)
Payload:  Cell position, Gem type
```

Once per **cell**, over the union of the cells a step removes — a cell named by
more than one shape is reported once (`MATCH3_RULES.md` §3.3 item 3), and the
union's events are emitted in ascending cell index (§1.3).

1. **`Gem type` is always one of the four §1.1 types.** Special Gems are retired
   under ADR-028; every cell occupant is an ordinary `GemType` (`ATK`, `DEF`, `HP`,
   `POWER`).
2. **No `specialGem` payload.** Special Gems are retired from wire contracts and
   event definitions under ADR-028 and OD-1–OD-2. `GemMatched` delivers solely cell
   position and `gemType`.

## CascadeStepResolved
```text
Trigger:  One detection pass's Gravity and Spawn have completed
          (MATCH3_RULES.md §4.4, §4.5)
Payload:  passDepth, dropMoves[] (fromCell, toCell),
          spawnedGems[] (cellIndex, gemType)
```

It was added to `GAME_RULES.md` §16 by the Operator Decision approving TASK-278
A-1(i) and A-4, and it is a **report of a detection pass's gravity+spawn
transition**.

1. **It reports the pass's transition and not the board.** `passDepth` is the
   detection pass reported; `dropMoves` are the surviving Gems that changed
   cell during that pass's Gravity, each entry the authoritative origin and
   destination; `spawnedGems` are the Gems that pass's Spawn placed, each entry
   the destination cell and the Gem type placed there. The intermediate board
   configurations themselves are Transient Resolution State (`GAME_STATE.md`
   §3) and this event carries none of them.
2. **`passDepth` is the pass's own 1-based depth.** `1` is the pass run on the
   board the committed Swap produced, which is **not** a Cascade; `>= 2` is a
   pass produced by Gravity+Spawn (`MATCH3_RULES.md` §4.2 items 1–2). It is the
   same value `MatchCreated`'s `cascadeDepth` carries on that same pass, and on
   a pass preceded by a `CascadeCreated` it is that event's `cascadeDepth + 1`.
   The member name is deliberately new: `cascadeDepth` keeps its two existing
   meanings and is never reused for a third (`SIGNALR_PROTOCOL.md` §3.2.6
   item 4, §3.2.7 item 2).
3. **Ordering.** Exactly one item per **recorded** pass, and it is that pass's
   **last** item: after the pass's `MatchCreated`/`GemMatched`/`ComboChanged`
   and after the pass's Gravity and Spawn have completed, and before the next
   pass's `CascadeCreated` — or before the following stage's events (§1.1
   item 8). A pass that detects no Match is the terminating pass and emits none
   (§1.1 item 6). Within the payload, each array is in the read order of the
   step it reports: `dropMoves` by column ascending and, within a column,
   descending `fromCell` (bottom-to-top — `MATCH3_RULES.md` §4.4 item 1);
   `spawnedGems` by column ascending and, within a column, ascending
   `cellIndex` (top-to-bottom — `MATCH3_RULES.md` §4.5 item 2).
4. **What it deliberately omits.** An empty array is omitted, never sent as
   `[]` (`SIGNALR_PROTOCOL.md` §3.2.5) — so a pass whose Gravity moved nothing
   carries no `dropMoves`, and a pass whose Spawn placed nothing carries no
   `spawnedGems`. It carries no board, no `actor`, no `clearedCells`, and no
   `specialGem` inside a `spawnedGems` entry: the pass's cleared cells are
   already delivered by that pass's `GemMatched` run (which is one union per
   pass), the acting side by the sibling `SwapStarted.actor`, and the board
   only by the state push (`SIGNALR_PROTOCOL.md` §3.2.28, §4). It is not a
   second authoritative source of board state, and the client derives no
   gravity destination, no spawned identity, and no board of its own from it
   (`GAME_RULES.md` §18, `SIGNALR_PROTOCOL.md` §3.2.28).
5. **It adds no message, no method, no subscription, and no state member.** It
   is an item of the round's existing single `ReceiveEvents` batch
   (`SIGNALR_PROTOCOL.md` §3, §3.1), and it is emitted only after the pass it
   reports has completed Gravity and Spawn. No per-pass board, no mid-resolution
   push, and no second batch for one round is introduced by it
   (`GAME_STATE.md` §3, §5.1 item 2).

## MatchedGemSummary
```text
Trigger:  One committed board action's whole cascade resolution has completed
          (MATCH3_RULES.md §4.3 — the board is stable and the action's passes are final)
Payload:  gems[] (gemType, count) — one entry per Gem type the cycle matched,
          in the fixed order ATK, DEF, HP, POWER (MATCH3_RULES.md §1.1)
```

It is the report of **how many Gems of each type one actor's board cycle
matched**. It is the smallest event that exposes, for presentation, the
per-cycle matched-Gem total the resource stage already computes
(`COMBAT_RULES.md` §2 item 6; `MATCH3_RULES.md` §5.7, §5.8.2) — not a second
accounting of its own, and not a combat report.

1. **It aggregates exactly one board cycle.** A board cycle is one committed
   board action's Match-3 resolution: the Player's committed Swap
   (`GAME_RULES.md` §17 steps 2–8) or the Boss Response Phase's own step-18a
   board action (`§17` step 18a, `ADR-026` §2). The counts are the sum over
   **every pass of that cycle**, with the cycle's matching described by
   `MATCH3_RULES.md` §4.2 and §5.7 item 7 (a Cascade accumulates), and each
   cleared cell counted once (`MATCH3_RULES.md` §3.3 item 3, §5.7 item 2): a
   cell named by two shapes of one pass is one cell, and a later pass's new
   cells add. It therefore equals that action's own cleared-Gem accounting and
   no wider total.
2. **It is emitted once per cycle, and closes it.** Exactly one item per
   committed board action, and it is that cycle's **last** item: after the
   cycle's last recorded pass's `CascadeStepResolved` and before the following
   stage's events (`§1.1` item 9). It is **never** emitted per pass, per Match,
   or per `CascadeCreated`, and a rejected action emits none at all — the
   rejection emits nothing (`§1.2`, `MATCH3_RULES.md` §2.1.5 item 6) and has no
   cycle to close.
3. **Two cycles, two summaries, never one combined total.** A round carries the
   Player's cycle and the Boss Response Phase's (`ADR-026` §2), and each emits
   its own item with only its own side's counts (`§1.1` item 9). Nothing here
   sums across actors, and the event carries no side member: the acting side is
   the delivered `SwapStarted.actor` that opens the cycle it closes, exactly as
   for the cycle's other board events (`§1.1` item 3, `SIGNALR_PROTOCOL.md`
   §3.2.26 item 2). **The counts are not mixed, and a round in which the Boss
   never takes its board action emits only the Player's.**
4. **Zero-count Gem types are absent, not zero.** An entry exists only for a Gem
   type the cycle actually matched, and every `count` is therefore `>= 1`. The
   payload's member set and the empty case are the wire owner's
   (`SIGNALR_PROTOCOL.md` §3.2.5, §3.2.29 item 2).
5. **The values are read, not re-derived.** `count(type)` is the number of
   unique matched cells of that type the cycle cleared, and the four entries are
   the same four counts the action's resource generation was computed from
   (`COMBAT_RULES.md` §2 item 6) — read and reported, never re-counted from the
   board, a state snapshot, or a client-side match rule (`GAME_RULES.md` §18,
   `AGENTS.md` §10). The member order is the fixed display/read order of
   `MATCH3_RULES.md` §1.1 and is not an implementation choice.
6. **It reports counts only.** It carries no damage, healing, DEF buff
   magnitude, Power delta or resulting Power, Boss SkillCharge, Combo, Match
   count, board, or actor. Those are separate gameplay effects with their own
   events or state (`§2` `DamageCalculated`/`DamageDealt`/`DamageTaken`,
   `PowerChanged`; `GAME_STATE.md` §2.4.3 for SkillCharge) and are not
   substitutes for a matched-Gem count; presenting them from this event would
   report a value it does not carry.
7. **It is presentation input, not gameplay input.** No stage consumes it, it
   changes no state and no counter, and it is not a second source of any
   authoritative value (`§3` item 6, `SIGNALR_PROTOCOL.md` §3.2.29 item 6). It
   adds no message, no method, no subscription, and no state member: it is an
   item of the round's existing single `ReceiveEvents` batch (`§3` item 5).

## PowerChanged
```text
Trigger:  Power increases or decreases (GAME_RULES.md §12)
Payload:  Delta, new Power value, source (Gem match / Card / Relic / Boss)
```

1. **`Delta` is the signed change and `new Power value` is the resulting
   `PetState.Power`** (`GAME_STATE.md` §2.3) — the two travel together so a
   reader renders the change and the resulting value without recomputing either.
   `GAME_RULES.md` §12 owns the 0–100 range; this event reports the state's
   value, it does not re-derive the invariant.
2. **`source` answers "what changed Power"** — and it identifies the **stage
   that owns the mutation**, not the direction of the change. Its four values
   are owned by this payload line:

   ```text
   "match"   a Match-owned Power mutation       (GAME_RULES.md §17 step 13)
   "card"    a Card-owned Power mutation        (GAME_RULES.md §17 step 14)
   "relic"   a Relic-owned Power mutation       (GAME_RULES.md §17 step 11)
   "boss"    a Boss-owned Power mutation        (GAME_RULES.md §17 step 18c)
   ```

   `"card"` is **not** a cost-only value: it covers any Power mutation a Card
   cast owns, whether the cast pays its cost or applies a Power-granting effect
   (`CARD_RULES.md` §2, §3.6). The signed `Delta` — not the source — says
   whether that mutation was a gain or a spend.

   It shares the member *name* `source` with the damage events and the Passive
   events, which answer different questions and own different value sets; that
   is the same shared-name convention `SIGNALR_PROTOCOL.md` §3.2.7 item 2
   records for `cascadeDepth`, and §3.2.24 item 3 states it for this event on
   the wire.
3. **This event is the authoritative report of a Card cast's Power change.**
   `SIGNALR_PROTOCOL.md` §3.2.20 item 2 records that `CardCast` carries no cost
   member in MVP; where the Power change itself must be reported, it is reported
   here rather than by re-adding a cost member to `CardCast`. A cast's Power
   change is reported as the mutations it owns — the cost it pays and any Power
   its effects apply — and each is reported by its own emission (item 4).
4. **Emission is owned by the stage that owns the mutation.** Every
   authoritative gameplay mutation of `PetState.Power` emits this event, and the
   stage that performs the mutation is the stage that emits it: the Power stage
   for a Match's resource generation, the Card stage for a cast's cost and its
   Power effects, the Relic stage for a Relic's `Power` effect
   (`GAME_RULES.md` §17 step 11), and the Boss Response stage for a Boss
   ability's Power-draining effect — the `drain-power` ability, whether it
   resolves through the transitional `BossSkill` path (§17 step 18c) or through
   its Boss Card form's `PowerDrain` effect (§17 step 18c; TASK-309). No
   separate or centralized emission step exists, and no stage emits on another
   stage's behalf.
   `SIGNALR_PROTOCOL.md` §3.2.24 fixes only this event's wire shape; the
   sequencing position is `GAME_EVENTS.md` §3 item 7's.

   **One mutation, one event.** When one action performs several Power
   mutations — a Card cast that both pays a cost and applies a Power effect is
   the documented case — each mutation emits **its own** `PowerChanged`
   carrying that mutation's `Delta` and the resulting `Power`. They are never
   combined into a single net event. The events preserve the authoritative
   mutation order `GAME_RULES.md` §17 fixes, so a reader applying them in
   delivered order reaches the same `Power` the state holds
   (`SIGNALR_PROTOCOL.md` §3.2.24 item 6). A mutation that does not occur emits
   nothing.
5. **A Boss Card cast's Power drain is a Pet-side mutation; the Boss's own Power
   spend is not.** `PowerDrain` mutates the active Pet's `PetState.Power`, so it
   emits this event for that mutation — carried by the Boss Response stage inside
   the round's single ordered batch, before that cast's `BossCardCast` — under
   item 4's one-mutation-one-event rule. The Boss's own cast cost mutates
   `BossState.Power`, a different resource (`BOSS_RULES.md` §4.1); this event
   reports the Player-side `PetState.Power` only, so the Boss's spend emits
   **no** `PowerChanged`. No new discriminator and no new payload member are
   introduced, and the Player `CardCast`/`PetSkillCast` contracts are unchanged
   (`CARD_RULES.md` §6, `ADR-029`).

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
                            effect summary (not populated yet — see note)
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
3. **`effect summary` is not populated yet, and its absence is not an
   omission.** `PASSIVE_RULES.md` §7 defines `PassiveTriggered` as emitted "when
   the Passive activates and its Effect resolves", and what the effect does is
   owned by the Combat/Pet systems (`COMBAT_RULES.md`), not by the Passive
   tracker. Until that stage exists this member is **not populated**, and the
   event's other members are unaffected and fully decodable without it. A reader
   must not treat a missing `effect summary` as "no effect occurred": it means
   "the effect is not yet reported" (`GAME_STATE.md` §0 item 4's staging
   position, applied to an event payload). This is a recorded sequencing
   position, not a scope reduction — the member is added to the emitted value by
   the Combat stage's own task, and the payload list above is not otherwise
   revised by it.
   **How such an element appears — or does not appear — on the wire is owned by
   `SIGNALR_PROTOCOL.md` §3.2.25**, which fixes the single governing convention
   (omission) for a §2 payload element the wire cannot yet carry. This item
   states only *why* the element is unpopulated and what its absence means; it
   is not a wire rule, and this document does not keep a second convention
   (`GAME_EVENTS.md` §3 item 1).
4. **Boss Passive timing.** Boss Passive fires at step 18b of
   `GAME_RULES.md` §17 — after the Player Damage steps (15–17) and the 18a
   selection/board action, before the 18c Boss Card cast or legacy Skill, and
   before the 18d Boss attack. The Passive evaluates
   against the post-damage battle state. Boss Passive uses the same event
   (`PassiveCharged`/`PassiveTriggered`) with `source = "boss"` — no
   Boss-specific passive event exists (`BOSS_RULES.md` §7).

## RelicTriggered
```text
Trigger:  A Relic's Trigger+Condition is met (RELIC_RULES.md §3, §7)
Payload:  RelicId, effect summary, deterministic order index for this event
```

1. **`RelicId` is the triggered Relic's identity** — the same value
   `PetState.EquippedRelics[]` holds (`RELIC_RULES.md` §2.2 item 3, which also
   records that this identity member and the array element are one identity).
   It is read and reported, never re-derived (`GAME_EVENTS.md` §2 item 1's
   convention for `PassiveCharged`).
2. **`effect summary` is not populated yet**, on the same basis as
   `PassiveTriggered` item 3 — the member is added by the stage that implements
   Relic effects, and its absence means "not yet reported", never "no effect
   occurred".
3. **The "deterministic order index" is an emission-sequence fact, not a
   payload member.** `RELIC_RULES.md` §4 owns the deterministic trigger order,
   and that order **is** the position of each `RelicTriggered` in the
   `ReceiveEvents` batch (`SIGNALR_PROTOCOL.md` §3.2.1 item 3, §3.2.12 item 4).
   `SIGNALR_PROTOCOL.md` §3.2.23 item 3 declines it as a wire member because
   carrying an index beside the array order would be a second spelling of one
   fact (`GAME_STATE.md` §0 item 5). This document's payload line above names
   the order as something the event carries by position, not as a member; it is
   not restated as a wire member here (`GAME_EVENTS.md` §3 item 1).
4. **Whether a member is emitted is the wire owner's question.** For this event
   the answer is `SIGNALR_PROTOCOL.md` §3.2.23 for the members and §3.2.25 for
   any §2 element the wire cannot yet carry.
5. **Emission point.** `GAME_RULES.md` §17 step 11's Relic stage emits one
   `RelicTriggered` per Relic whose Effect actually applies, carrying the
   triggered Relic's owned instance identity (`RELIC_RULES.md` §2.2 item 3), in
   the equip-slot order §4.2 fixes. A Trigger that fires but whose Condition
   fails, or whose effect cannot be resolved, emits nothing — the payload line's
   trigger is "A Relic's Trigger+Condition is met" <b>and</b> its effect applied
   (`RELIC_RULES.md` §7).

## CardCast / PetSkillCast
```text
Trigger:  A Player Card cast is validated and applied (CARD_RULES.md §3, §6)
Payload:  CardId, Power cost paid, effect summary
          PetSkillCast additionally confirms it was the active Pet's
          Signature Skill
```

1. **`CardId` is the cast Card's definition identity** — the same value
   `PetState.EquippedCards[]` holds and the same id the
   `CardCast(battleId, cardId, clientSequence)` request carries
   (`SIGNALR_PROTOCOL.md` §2, `CARD_RULES.md` §1). There are no Card instances
   (`GAME_STATE.md` §2.3, ADR-012 item 9), so it is a definition identity.
2. **`PetSkillCast`'s confirmation that the cast Card was the Signature Skill is
   carried by that same `CardId`.** Each Pet has exactly one Signature Skill
   expressed as one Pet Skill Card (`CARD_RULES.md` §4 item 1), and its identity
   is derivable server-side from `PetDefinition.SignatureSkillCardId`
   (`CARD_RULES.md` §1 item 4) and present in the loadout snapshot
   (`GAME_STATE.md` §2.3), so no dedicated confirmation member is defined here or
   on the wire (`SIGNALR_PROTOCOL.md` §3.2.21 item 2).
3. **`Power cost paid` is the effective cost actually deducted, and it is not a
   wire member.** The cost a cast pays is `EffectiveCardCost`, owned by
   `CARD_RULES.md` §3.6 — not the authored `CardDefinition.PowerCost` on its
   own, which remains the definition-side base value (§2/§4.1 there). The
   authoritative record of what a cast did to `PetState.Power` is the state
   value delivered by the push (`GAME_STATE.md` §2.3), and where a Power change
   must be reported as an event it is `PowerChanged` (below).
   `SIGNALR_PROTOCOL.md` §3.2.20 item 2 records that MVP `CardCast` therefore
   carries no cost member — a decision by the wire owner, which this document
   does not contradict. Where a Relic has reduced the cost, the reduction is
   already reflected in the paid amount and again in the resulting `Power`;
   neither is a second member here.
4. **`effect summary` is not populated yet**, on the same basis as
   `PassiveTriggered` item 3.
5. **Emission order: `CardCast` precedes `PetSkillCast`.** Both fire at
   `GAME_RULES.md` §17 step 14 (`CARD_RULES.md` §6), and
   `SIGNALR_PROTOCOL.md` §3.2.22 owns the ordering statement on the wire; a
   Basic Card cast emits `CardCast` alone (`CARD_RULES.md` §6).
6. **These are the Player's events, and a Boss cast is never either of them.**
   A Boss Card cast emits the dedicated `BossCardCast` below — at
   `GAME_RULES.md` §17 step 18c, inside the Boss response phase — and never a
   `CardCast` or a `PetSkillCast`. No actor/source member is added to either
   payload to distinguish them: the discriminator is the distinction
   (`CARD_RULES.md` §6, `ADR-029`).

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
Trigger:  The Boss's TRANSITIONAL Skill timing rule fires
          (BOSS_RULES.md §4, §4.4) — Charge ≥ Requirement AND Cooldown = 0
          (GAME_STATE.md §2.4.3)
Payload:  SkillId, SourceId (BossId), effect summary
```

`SourceId` carries the canonical technical Boss Identity (`BOSS_RULES.md`
§6.4, e.g. `boss-hoa-long`), never the display name — `source` is always
`"boss"` for this event.

The Skill's damage (if any) is reported by separate `DamageCalculated`/
`DamageDealt`/`DamageTaken` events in the same batch, with
`source = "boss"` and `target = "player"` (a fixed wire label meaning the
player's side / active Pet — ADR-011).

**`BossSkillCast` is the transitional path's event.** It is emitted by the
pre-migration Boss ability form only, and it is retired together with
`BossState.SkillCharge` when `ADR-029`'s Slice 5 removal gate is met
(`BOSS_RULES.md` §4.4). No new feature, consumer, or balance value may be built
on it.

*Slice 5 Removal Requirements (TASK-311):*
1. **Intended Future Removal:** The event discriminator `BossSkillCast = 9`, the `BossSkillCastEvent` payload struct, and `BattleEvent.ForBossSkillCast` factory are slated for deletion in Slice 5 once all consumers are reconciled.
2. **Affected Consumers:** Wire projection (`BattleEventWireProjection.cs`), wire contract (`SIGNALR_PROTOCOL.md` §3.2.18), client presentation (`BattleScene.ts`, `BattleEventPresenter.ts`), and legacy tests (`AtomicRoundOrchestrationTests.cs`).
3. **Current State:** Unchanged in TASK-311. All five MVP Bosses are migrated to Boss Cards at runtime, so `BossCardCast` is the only event emitted in production MVP battles; `BossSkillCast` remains available in the codebase solely as the transitional compatibility path until Slice 5 execution.

## BossCardCast
```text
Trigger:  The Boss resolves a Boss Card cast (BOSS_RULES.md §4.2–§4.3,
          GAME_RULES.md §17 step 18c) — ADR-029
Payload:  CardId, SourceId (BossId), effect summary
```

1. **It is the dedicated Boss action event.** A `BossCardCast` reports that the
   Boss selected a Boss Card cast as its one action for the response phase and
   that the cast resolved. It is emitted after the cost is deducted from
   `BossState.Power` and the effect applied, in the order `CARD_RULES.md` §3
   fixes (`ADR-029`). It is never emitted for a board match action — that action
   is reported by `SwapStarted`/`SwapResolved` and the board cycle's own events.
2. **`CardId` is the Boss Card's definition identity.** It is the ability
   identity `BOSS_RULES.md` §6.4 records for that Boss (e.g. `"flame-burst"`),
   reused as the Boss Card's `CardDefinitionId`; it is a reported identity, not a
   re-derived one (`GAME_STATE.md` §0 item 5, `CARD_RULES.md` §1.1 item 3).
3. **`SourceId` identifies the Boss.** It is the Boss's canonical technical
   Identity (`BossState.BossId`, `BOSS_RULES.md` §6.4), never a display name —
   the same convention `BossSkillCast.sourceId` and
   `PassiveCharged`/`PassiveTriggered.sourceId` already use with
   `source = "boss"`.
4. **The effect's consequences are carried by their own events and by state.**
   Damage (none in MVP — every MVP ability has `BaseDamage = 0`, `ADR-026` D3),
   applied Status Effects, and the resulting Boss Power and cooldown are reported
   by the events their own owners define and by the state push
   (`GAME_STATE.md` §2.4, `SIGNALR_PROTOCOL.md` §4). This event reports the cast
   and its identity, not a second copy of the effect.
5. **The action selection itself emits nothing.** There is no
   `BossActionSelected` event; which action the Boss took is reported by the
   events of that action (`BOSS_RULES.md` §7).
6. **`effect summary` is omitted**, on the same basis as `CardCast`'s
   (`SIGNALR_PROTOCOL.md` §3.2.25).

## BattleWon / BattleLost
```text
Trigger:  Boss HP or active Pet HP reaches 0 (GAME_RULES.md §1.4 —
          the Pet is the combat character; there is no Player HP pool,
          ADR-011)
Payload:  Outcome, final BattleState summary, reward summary (BattleWon
          only — this is the event-payload rule; the REST response's
          `rewards` field covers both outcomes — API_CONTRACTS.md §4;
          the single `RewardSummary` contract, whose Player-track member
          set applies to both outcomes and whose Pet-track member list
          remains deferred to the implementation task: DATABASE.md §1,
          "Reward semantics for `RewardSummary`")
```

1. **`Outcome` carries exactly one of two values: `"victory"` (with
   `BattleWon`) or `"defeat"` (with `BattleLost`).** This block owns the
   battle-outcome value set: the persisted `BattleResult.Outcome`
   (`DATABASE.md` §1), the REST `outcome` member (`API_CONTRACTS.md` §4),
   and the SignalR `outcome` wire member (`SIGNALR_PROTOCOL.md` §3.2.19)
   each use these same two values for the same battle.
2. **The reward summary in this payload is `RewardSummary`** — the one
   contract owned by `DATABASE.md` §1. This document does not define its
   members and does not contradict the REST response. Both tracks' reward
   semantics are now decided: the Player track by `COMBAT_RULES.md` §7
   (`BattleWon` grants `+100` Player XP, `BattleLost` `+0`), and the Pet
   track by `PET_RULES.md` §5.3 (`BattleWon` grants the active combat Pet
   `+100` Pet XP, `BattleLost` `+0`, inactive owned Pets `+0`). The
   `RewardSummary` Pet member list itself is deferred to the implementation
   task (`DATABASE.md` §1, "Reward semantics for `RewardSummary`"); no Pet
   reward member is frozen here (`AGENTS.md` §7, §9).

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
   gravity, spawn, Combo — are owned by `MATCH3_RULES.md` §2–§8. Special Gems
   are retired from technical contracts and event payloads under ADR-028.
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
8. **An operation that produces no Battle Event at all.** Some resolution steps
   are internal and are reported by the state they commit, not by an event. The
   shared board's **deadlock reshuffle** is the documented case: when the Boss
   Response phase's move selection finds no legal match-producing swap, it
   replaces the board with a deterministically generated valid one and selects
   on that board (`ADR-026` §8, `GAME_RULES.md` §17 step 18a). **The second
   documented case is `MATCH3_RULES.md` §1.6's commit-boundary recovery**: when
   the finished board of a non-terminal Round has no legal match-producing swap,
   it is replaced by the same deterministic generation on the battle's retained
   stream, before the Round's single write-back. Both are the same mechanism at
   two evaluation points, and neither emits anything — there is no
   `BoardReshuffled` event, discriminator, or payload member, and none is promised
   by any owning document (`GAME_RULES.md` §16,
   §2 above, `SIGNALR_PROTOCOL.md` §3.2.2). The reshuffle becomes authoritative
   only in the Round's single state write-back — the delivered committed board
   is the board that follows from it — and it reaches the client in the state
   push, exactly as §3 item 6 and
   `SIGNALR_PROTOCOL.md` §3.1 items 1–3 and §4 describe — the client therefore
   cannot identify the operation from the delivered stream, and that is an
   accepted limitation of this contract rather than a missing event.
   Whether such an operation needs an event is decided the ordinary way, by
   adding it to `GAME_RULES.md` §16 first (§3 item 5) — never by delivering
   state on a side channel, and never by an ADR asserting an event that this
   document does not define.
