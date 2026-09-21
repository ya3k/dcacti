# TASK-005A — Define Player Battle Progression State Contract

---

## Metadata

```text
Task ID:           TASK-005A
Type:              ARCHITECTURE / STATE CONTRACT (documentation only)
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     backend
Supporting Agents: gameplay, review, testing
Workflow:          architecture/architecture-change.md, gated by
                   documentation/documentation-change.md
Skills:            documentation-consistency, architecture-conformance,
                   authority-determinism-audit, impact-analysis
Dependencies:      TASK-004 (Swap Execution, DONE) — which reported the gap
                   this task closes
```

---

## Objective

Define the authoritative state contract for **per-player battle progression**
— cumulative Match count and per-Swap Combo — so that the upcoming
Match / Combo Accounting implementation has a documented state owner before it
is written.

The gap, as reported by TASK-004:

```text
Match count, Combo state, and related per-player battle progression currently
have no authoritative state owner.
```

This task is **contract only**. It implements no Match counting, no Combo
calculation, no events, no persistence, and no gameplay behavior.

---

## Discovery — What Was Read

```text
docs/00-overview/     MVP_SCOPE.md (§1–§4)
docs/01-game-design/  GAME_RULES.md (§1–§5, §10, §16, §17, §18, §20)
                      MATCH3_RULES.md (§2, §3, §3.4, §4.2, §5.5.5, §6,
                                      §7.2, §8)
                      PASSIVE_RULES.md (§2–§8)
docs/02-technical/    GAME_STATE.md (§0, §1, §2, §2.0–§2.0.5, §2.1, §2.1.10,
                                     §2.2–§2.4, §3, §5, §5.1–§5.3)
                      GAME_EVENTS.md (§1, §1.1–§1.3, §2)
                      SIGNALR_PROTOCOL.md (§0, §3, §4, §4.1, §5, §6, §8)
                      REDIS_STATE.md (§1–§7)
                      ARCHITECTURE.md (§1, §2.1, §3, §4, §4.1, §5)
                      TDD.md, DATABASE.md (checked, not affected)
docs/03-decisions/    README.md (§2, §3, §5, §7), ADR-001, ADR-004, ADR-005,
                      ADR-008, ADR-009, ADR-010
Existing implementation:
                      src/backend/GameServer.Domain/Battle/BattleState.cs
                      src/backend/GameServer.Domain/Match3/ResolutionEvents.cs
                      src/backend/GameServer.Domain/Match3/MatchPrimitive.cs
                      src/backend/GameServer.Domain/Match3/MatchShape.cs
                      src/backend/GameServer.Domain/Match3/CascadeResolver.cs
                      src/backend/GameServer.Domain/Match3/BoardResolver.cs
                      src/backend/GameServer.Domain/Match3/SwapExecution.cs
                      src/backend/GameServer.Application/Battle/BattleStateService.cs
                      src/backend/GameServer.Api/Hubs/BattleHub.cs
```

---

## Contract Decision

### 1. Authoritative Owner

**`PlayerState`** — the `GAME_STATE.md` §2.2 domain object — is the owner of
both `MatchCount` and `Combo`.

This is **not** a new decision and **not** a choice of convenience. It is
already the documented contract, stated independently in five places:

```text
GAME_STATE.md §2             BattleState ├── PlayerState   (field tree)
GAME_STATE.md §2.2           PlayerState ├── Combo
                                         └── MatchCount  (cumulative Matches
                                             this battle, GAME_RULES.md §3)
GAME_STATE.md §3 item 2      "the authoritative cumulative total is
                              PlayerState.MatchCount (§2.2)"
GAME_STATE.md §5.3 item 1    "the resolution's values in PlayerState
                              (Combo, MatchCount)"
MATCH3_RULES.md §6.1 item 1  "Combo is a BattleState field (GAME_STATE.md
                              §2.2, PlayerState.Combo)"
MATCH3_RULES.md §6.7 item 2  "the cumulative battle total is
                              PlayerState.MatchCount (GAME_STATE.md §2.2)"
```

`ResolutionEvents.cs` and `BattleStateService.cs` already cite this owner in
their XML documentation while stating that the type does not yet exist.

**Rejected alternatives, and why:**

```text
BattleState directly (a flat MatchCount/Combo on the battle record)
    Rejected: §2 already nests them under PlayerState (§2.2), and §0 item 5
    forbids a stage from introducing a parallel representation of a concept
    another stage owns. A flat field would be a second owner.

BoardState
    Rejected: §2.1.2 item 1 fixes BoardState at exactly one field, Cells[64].
    Match count and Combo are battle progression, not board contents.

Transient Resolution State (ResolutionContext, §3)
    Rejected as the *authoritative* owner: §3 item 1 discards it entirely
    within one resolution and §3 item 3 makes it neither serialized nor
    recoverable. It holds the *in-progress* Combo (§3 item 1), which is
    exactly the transient half of the split defined in §5 below.

A new domain object
    Rejected: no document defines one, and §2.2 already owns both fields.
```

**Staging position.** `PlayerState` does not exist in code yet. That absence is
the §2.0.5.3 staging position, not a scope reduction: §0 item 4 states that a
field absent from a stage is **not yet implemented**, not **not required**. This
task defines the contract the type must satisfy; it does not create the type.

---

### 2. Player Identity

```text
GAME_STATE.md §2    BattleState has no player identifier field.
GAME_STATE.md §2.2  PlayerState has no player identifier field.
```

Both trees are closed lists, and neither contains a player id. **The contract
therefore defines no player identifier, and this task adds none.**

The player is implicit and singular:

1. **MVP is exactly one player per battle.** `GAME_RULES.md` §2.1 defines a
   Turn as *one player Swap/Action*; `GAME_STATE.md` §2.2 defines `MatchCount`
   as "cumulative Matches **this battle**", a single scalar with no per-player
   dimension; §2.3 defines `PetState` as "the **one** active Pet for this
   battle".
2. **The battle is the scope.** `BattleState` is per-battle
   (`REDIS_STATE.md` §1: `battle:{battleId}:state`), and `PlayerState` is a
   field **of** it. The player is identified by the battle, which is identified
   by `BattleId` — the existing `GAME_STATE.md` §2.0.1 field, whose documented
   job is to identify the battle session, not to name a participant.
3. **Player identity is supplied by surrounding context, not persisted in
   `PlayerState`.** The surrounding context is the battle/session: `BattleId`
   scopes the SignalR group (`SIGNALR_PROTOCOL.md` §1.2), and authentication is
   handled outside battle state (`ADR-007`, `ARCHITECTURE.md` §2.3). Adding a
   `PlayerId` to `PlayerState` would duplicate a value the session context
   already carries and that no documented rule reads.
4. **Future multiplayer compatibility is a compatibility statement, not an
   implementation.** The representation does not *block* multiplayer: a future
   per-player record could be reached through the existing `BattleState`
   container without any documented field becoming wrong. But this task does
   **not** introduce multiplayer architecture. `MVP_SCOPE.md` §3 lists
   "PvP / Multiplayer" as **FUTURE — direction only, not designed**, and §3
   forbids scaffolding it "for later". No collection of players, no
   `PlayerStates[]`, no participant index, and no per-player battle key is
   defined here.

---

### 3. Match Count

**Semantics** (`GAME_RULES.md` §3, `MATCH3_RULES.md` §6):

> Match = each individual Match created, including cascade Matches.

```text
Field:          MatchCount
Type:           integer (signed 32-bit; the resolution's own counter is int)
Owner:          PlayerState (GAME_STATE.md §2.2)
Initial value:  0
Cumulative:     YES — for the entire battle
Resets:         NEVER — not per Turn, not per Swap, not per Cascade
```

**What increments it.** Exactly one increment per **Match counted**, where a
Match is one detected shape:

```text
MATCH3_RULES.md §3 item 5      one shape is ONE Match however many cells it
                               spans and however many Special Gems it creates
MATCH3_RULES.md §6.2 item 3    a shape counted as one Match increments once,
                               whatever its size or tier
GAME_RULES.md §3 item 2        each individual Match is counted, including
                               Matches created by Cascades
GAME_RULES.md §3 item 3        match count is independent from Turn count
```

**The required answers:**

| Question | Contract answer | Source |
|---|---|---|
| Special-gem activations count as a Match? | **No.** | `MATCH3_RULES.md` §5.5.5 item 8, §6.3.1 item 1 (`GAME_RULES.md` §5 item 1); `PASSIVE_RULES.md` §2.2 |
| A cascade Match increments independently? | **Yes** — each cascade Match counts as its own Match. | `GAME_RULES.md` §3 item 2, §4 item 3; `MATCH3_RULES.md` §6.3 item 1 |
| Multiple Matches in one Swap each increment? | **Yes** — one increment per Match, in detection order. | `MATCH3_RULES.md` §3.2 order, §6.2 item 1 |
| Rejected swaps change it? | **No.** A rejected action writes nothing. | `MATCH3_RULES.md` §2.1.5 items 3, 5; `GAME_STATE.md` §5.1 item 6 |
| A Special Gem *effect* clearing N cells? | **No** — N cells are not N Matches. | `MATCH3_RULES.md` §6.3.1, §5.5.5 item 8 |

**Canonical example** (`GAME_RULES.md` §2, `MATCH3_RULES.md` §6.7):

```text
Turn = 1
  pass 1  → Match #1   MatchCount +1
  Cascade → Match #2   MatchCount +1
  Cascade → Match #3   MatchCount +1

Turn = 1
MatchCount (this Swap) = 3
```

**Turn ≠ Match is preserved.** A committed Swap increments `Turn` by exactly 1
regardless of how many Matches it produces (`MATCH3_RULES.md` §8.1 items 1–2,
§8.2 item 1). Match count is never derived from `Turn`, and `Turn` is never
derived from `MatchCount`.

---

### 4. Combo State

**Semantics** (`GAME_RULES.md` §5 item 1, `MATCH3_RULES.md` §6):

> Combo = consecutive Matches/Cascades created within one committed player Swap.

```text
Field:          Combo
Type:           integer (signed 32-bit)
Owner:          PlayerState (GAME_STATE.md §2.2)
Initial value:  0      (before the battle's first committed Swap)
Cumulative across Turns:  NO
```

**The required answers:**

| Question | Contract answer | Source |
|---|---|---|
| When Combo starts | On a **committed** Swap, reset to 0; the first Match makes it 1. | `MATCH3_RULES.md` §6.1 item 2, §6.2 item 1 |
| When Combo increments | By exactly 1 per Match, in detection order, across all passes. | `MATCH3_RULES.md` §6.2 items 1–3, §6.3 item 1 |
| When Combo resets | **Only** on a committed Swap (`§6.1 item 2`). Nothing else. | `MATCH3_RULES.md` §6.4 |
| Cumulative across turns | **No.** It does not persist between Swaps. | `GAME_RULES.md` §5 item 3; `MATCH3_RULES.md` §6.1 item 4 |
| Swap producing zero Matches | Unreachable as a committed Swap — such a Swap is **rejected** (`NO_MATCH_FROM_SWAP`) and neither resets nor changes Combo. | `MATCH3_RULES.md` §6.5 items 1–2, §2.1.2 item 4 |
| Swap producing exactly one Match | Combo = **1** — never 0, never 2. | `GAME_RULES.md` §5 item 2; `MATCH3_RULES.md` §6.2 item 1 |
| Multiple cascade Matches | Each counts; Combo equals the Swap's total Match count. | `MATCH3_RULES.md` §6.3 item 1 |
| After the Swap's resolution completes | Combo **retains** the Swap's final value — the terminating no-match pass does not reset it. | `MATCH3_RULES.md` §6.4, §6.6 item 1 |
| Rejected swaps | **No effect.** Neither reset nor changed. | `MATCH3_RULES.md` §6.1 item 3, §6.5 item 2 |
| Special Gem activation / chain / creation | **No effect** on Combo. | `MATCH3_RULES.md` §6.3.1 items 1–3 |

**The Combo floor** (`MATCH3_RULES.md` §6.5 item 3): no committed Swap can end
with `Combo = 0`, because a committed Swap always contains at least one Match.
The transient `Combo = 0` between the reset and the Swap's first Match is
internal to the resolution and is never written to the store or published
(`GAME_STATE.md` §5.1 item 2). Consequently the **published** value reads 0 only
before the battle's first committed Swap.

**Canonical example** (`GAME_RULES.md` §5, `MATCH3_RULES.md` §6.7):

```text
Swap A (committed)  → Combo 0, then Match → 1, Cascade Match → 2,
                      Cascade Match → 3        ⇒ Combo = 3

Next committed Swap → Combo 0, then Match → 1 ⇒ Combo = 1
```

**No Combo bonus is implemented.** `GAME_RULES.md` §5 item 4's multiplier table
(`1.00×`–`1.50×`) and Combo-threshold Relic activation are gameplay consumers of
this value, owned by `COMBAT_RULES.md` and `RELIC_RULES.md`. They are **out of
scope** here and remain unimplemented.

---

### 5. Turn-Local vs Battle-Cumulative Classification

| Value | Classification | Written to authoritative state? |
|---|---|---|
| `PlayerState.MatchCount` | **Battle-cumulative**, persistent authoritative state | **Yes** — one write per resolution |
| `PlayerState.Combo` | **Persistent authoritative state** (a value that is turn-*scoped* but must survive past the Turn) | **Yes** — one write per resolution |
| Running Match count *during* a resolution | **Transient / derived** — `ResolutionContext` | **No** |
| Running Combo *during* a resolution | **Transient / derived** — `ResolutionContext` | **No** |
| The swap's own Match total (for this Swap) | **Derived** — `CascadeResult.TotalMatches`, recomputable from `Resolution.Passes` | **No** |
| `Turn` | Battle-cumulative (existing) | Yes (already implemented) |
| `Sequence` | Battle-cumulative (existing) | Yes (already implemented) |
| Combo damage multiplier | **Presentation/derived** at the point of use | No |
| Client's displayed Match/Combo counter | **Presentation-only** | No |

**The split, stated explicitly.** Both `MatchCount` and `Combo` are
**persistent authoritative state** — they are `PlayerState` fields, so they are
part of `BattleState` and therefore part of the §1 "Active Battle State"
category. The *intermediate* values of both are **Transient Resolution State**
(`GAME_STATE.md` §3 item 1: `ResolutionContext` holds "the running Combo"; §3
item 2 holds `MatchesThisResolution[]`). The two are not duplicates: §3's copy
is the working set that never survives the resolution, and §2.2's copy is the
value the battle carries forward.

**Combo is not "turn-local" in the storage sense.** It is *scoped* to one Swap,
but it is an authoritative published value that must survive the resolution
(`MATCH3_RULES.md` §6.4: it "returns to 0 when the **next** Swap commits"), and
it is delivered to the client (`GAME_STATE.md` §2.2; `SIGNALR_PROTOCOL.md` §4).
It is therefore persisted, not turn-local scratch.

**Nothing transient is persisted.** Per the task's instruction, no value that is
only required during resolution is stored, because no document requires it —
`REDIS_STATE.md` §7 item 9 and `GAME_STATE.md` §5.1 item 2 both forbid
mid-resolution writes, and `GAME_STATE.md` §3 items 1 and 3 make
`ResolutionContext` non-serializable by construction.

---

### 6. Relationship With `Resolution.Passes`

**`Resolution.Passes` is sufficient. No change to the Match-3 resolver is
required, and none is made.**

`CascadeResolver.CascadeResult.Passes` (`IReadOnlyList<PassResult>`) already
carries everything Match/Combo accounting needs, in the documented order:

```text
CascadeResult.Passes[]
    └── PassResult
        ├── Matches : IReadOnlyList<MatchResolution>
        │       └── MatchResolution { Shape, CascadeDepth, CreatedSpecialGems }
        ├── CreatedSpecialGems   IReadOnlyList<SpecialGemClaim>
        ├── ActivatedSpecialGems IReadOnlyList<ActivatedSpecialGem>
        ├── ClearedCellUnion     IReadOnlyList<int>
        └── Board / RngState
CascadeResult.TotalMatches   == sum of Matches.Count over every pass
CascadeResult.CascadeDepth   == Passes.Count − 1
```

**Why it is sufficient, item by item:**

1. **Match boundaries are uniquely determined.** One `MatchResolution` is
   exactly one Match: `MatchResolution`'s own contract states "One shape is one
   Match however many cells it spans and however many Special Gems it creates:
   the Special Gems created are a consequence of the Match, never additional
   Matches" (`MATCH3_RULES.md` §5.4 item 4). So
   `MatchCount += Passes.Sum(p => p.Matches.Count)` and the counting rule of
   §3 above is satisfied with no ambiguity.
2. **The order is already the documented order.** `Passes` is "every pass that
   detected a Match, in order"; each `PassResult.Matches` is "the pass's match
   set, in §3.2 order". That is precisely `MATCH3_RULES.md` §6.2 item 1's "in
   detection order" / §6.6 item 1's "the order of §3.2 and §4.2".
3. **Cascade structure is already reported.** `MatchResolution.CascadeDepth`
   is the pass's depth (1 = the Swap's first pass, ≥ 2 = a Cascade), and
   `CascadeResult.CascadeDepth` is the pass count after the first. The
   `CascadeCreated` depth index of `MATCH3_RULES.md` §4.2 item 2 is therefore
   `CascadeDepth − 1`, already available.
4. **Special Gem activations are correctly excluded by construction.**
   `PassResult.Matches` contains only detected shapes. Activations live in the
   separate `ActivatedSpecialGems` and in `ClearedCellUnion`. A naive
   implementation that counted `ClearedCellUnion` would be wrong; the contract
   is to count `Matches`, which is what §6.3.1 requires.
5. **The terminating pass is already excluded.** `Passes` omits the
   no-match pass (§4.3 item 4), so an implementation never sees an empty pass to
   special-case.
6. **Combo needs no separate traversal.** `MATCH3_RULES.md` §6.3 item 1 makes
   Combo equal to the Swap's Match total across all passes. Counting Matches in
   `Passes` order produces both values in one traversal.

**What is missing, and it is not the resolver's job:** `Passes` reports the
resolution's *outcome*. It carries no `MatchCount` and no `Combo`, because
those are `PlayerState` values that do not exist yet — `MatchResolution`'s own
documentation says so ("this value is Domain-side and is **not** a Match
count"). The gap is the missing state owner (defined above), **not** missing
resolution output. The resolver is therefore not touched.

**One implementation caveat, recorded for TASK-005, not resolved here.**
`SwapExecutor.Execute` currently passes `swapOriginIndex: null`
(`SwapExecution.cs` line 235), while the speculative `Match 4 / Match 5`
placement rule of `MATCH3_RULES.md` §5.5.3 item 1 needs a non-null origin for
the first pass. That is a **Special Gem placement** concern, entirely
independent of Match counting and Combo: `Matches.Count` is unaffected by where
a created gem is placed. It is reported here as an observation for the owning
task and is deliberately **not** changed by this task (`AGENTS.md` §16).

---

### 7. Battle Events Boundary

`GAME_EVENTS.md` is the owner. This task defines **only whether** the future
event system reads or updates the new state, and invents **no** payload field.

| Event | Reads `MatchCount`? | Reads `Combo`? | Writes state? |
|---|---|---|---|
| `MatchCreated` | No — it *is* the count's cause, not a reader | No | No |
| `MatchResolved` | No | No | No |
| `CascadeCreated` | No — payload is the cascade depth index (`GAME_EVENTS.md` §2) | No | No |
| `ComboChanged` | No | **Yes** — reads the new value *after* the increment | No |
| `PassiveCharged` | No — it carries its own progress value (`PASSIVE_RULES.md` §6.1) | No | No |

**Required findings:**

1. **`ComboChanged` is the one event that reads a new-state field.**
   `GAME_EVENTS.md` §2 defines its payload as "New Combo value", and
   `MATCH3_RULES.md` §6.6 item 2 owns when the value changes. The event is
   emitted *after* the change, so it reads the post-increment `PlayerState.Combo`
   rather than the transient `ResolutionContext` copy.
2. **`MatchCreated` is the event that corresponds to the Match count.**
   `GAME_EVENTS.md` §2 states it explicitly: "A Match is counted when it is
   detected, which is why `MatchCreated` and not `MatchResolved` is the event
   that corresponds to the Match count (`GAME_RULES.md` §3)." No event carries a
   running Match total as a payload.
3. **`MatchCount` has no dedicated event and needs none.** `GAME_RULES.md` §16's
   canonical event list contains no `MatchCountChanged`. The value is published
   through state, not through an event. This task invents no event.
4. **No event writes state.** Events are outputs
   (`ARCHITECTURE.md` §4.1: "one action produces one state write-back and one
   event batch"). The ordering contract is
   `GAME_EVENTS.md` §1.1: `MatchCreated` → `GemMatched` → `ComboChanged` →
   `MatchResolved` per Match, with `CascadeCreated` preceding its pass.
5. **Ordering constraint on implementation.** `MATCH3_RULES.md` §6.6 item 1
   requires Combo to reach its final value "before the resolution completes",
   and §6.2 item 1 forbids a `ComboChanged` that jumps by more than 1
   (`GAME_EVENTS.md` §1.1 item 7 lists that as an ordering defect). Both follow
   automatically from counting `Passes` in order, but TASK-005 must not batch
   the increments.

**This task emits no events and implements no event system.**

---

### 8. SignalR Boundary

**Not delivered in this task. The existing protocol already defines the path
that will carry it, and no new message is introduced.**

```text
SIGNALR_PROTOCOL.md §4 item 4
    "the record carries exactly the implemented GAME_STATE.md §2.0 stage's
     fields ... No other field may be added to this record: additional state
     is introduced by extending GAME_STATE.md §2.0, not by the wire shape."

SIGNALR_PROTOCOL.md §4 (payload, as implemented)
    BattleStateUpdated(battleId, turn, sequence, board, rngSeed, rngState)

GAME_STATE.md §0 item 4 / §2.0.5.3
    PlayerState is absent from the current stage.
```

1. **The mechanism already exists.** `BattleStateUpdated` is "the only
   state-push method in this protocol" (`§4`, `§4 item 11`), and §4 item 4 makes
   the payload a one-to-one projection of the implemented `GAME_STATE.md` §0
   stage. When a `PlayerState` stage is implemented, `matchCount` and `combo`
   are added by **extending that stage**, exactly as the board stage added
   `board`, `rngSeed`, and `rngState` (`§4.1`). No second state-sync method, no
   `GameStateSync`, and no Match/Combo message is created.
2. **Nothing is delivered at this stage.** `PlayerState` does not exist, so the
   payload is unchanged: `{battleId, turn, sequence, board, rngSeed, rngState}`.
   The Api-side test from TASK-004 asserting that exact field set therefore
   remains correct and untouched.
3. **Combo is client-visible by design.** `GAME_RULES.md` §18 includes `Combo`
   in the list the client must never send authoritatively, and
   `SIGNALR_PROTOCOL.md` §2.1 repeats that the Swap request carries no "...
   result, Combo, Turn, or `Sequence` value". `MATCH3_RULES.md` §6.5 item 4 and
   §6.6 item 3 state the client "renders it from the events and from the
   published state". So the eventual exposure is by existing mechanism, on the
   server → client direction only.
4. **`MatchCount` visibility is likewise existing-mechanism.** `GAME_RULES.md`
   §3 item 3 makes Match count a Passive/Relic charging input, and
   `PASSIVE_RULES.md` §6.1 requires UI-facing progress for Match-based Passives.
   Both are satisfied by the same state push and the same events. **No UI is
   implemented here.**
5. **No new SignalR message, method, subscription, or payload member is
   introduced by this task.**

---

### 9. Redis Boundary

**The value belongs to the authoritative Redis battle snapshot. Persistence
remains deferred, untouched, and documented as such.**

`REDIS_STATE.md` §1 defines one key — `battle:{battleId}:state` — holding the
serialized `BattleState` of `GAME_STATE.md` §2. §2 item 1: "the JSON matches the
shape in `GAME_STATE.md` §2 exactly — no additional Redis-only fields". §1 also
states no other key is needed for MVP.

```text
Ownership:      battle:{battleId}:state  — the existing record, as a member of
                PlayerState within BattleState (GAME_STATE.md §2.2)
New keys:       NONE
New Redis-only fields: NONE
Schema change:  NONE
Implemented now: NO
```

1. **No new key.** `PlayerState` is a field of `BattleState` (§2), so it travels
   inside the existing record. No per-player key, no
   `battle:{battleId}:progression` key, no hash field, and no second record is
   defined.
2. **Persistence is intentionally deferred, and this is the documented
   position.** `REDIS_STATE.md` §7 item 8 states the Match-3 resolution stage is
   "still a staged subset" and "**neither requires nor authorizes** Redis
   persistence"; §7 item 4 gives the reason (no `PlayerState`/`PetState`/
   `BossState`, so §2's full shape cannot be produced); §7 item 7 states when
   persistence becomes required. Adding `MatchCount`/`Combo` does **not** end the
   deferral by itself, because `PetState` and `BossState` still do not exist.
   This task does **not** amend that boundary; it records that the new fields
   fall under the existing rule exactly as `LastCommittedSwapPair` did
   (§7 item 11).
3. **Where the value lives today.** The resolution's state is held in
   `BattleStateService`'s process-local registry — the same staged,
   safe-to-lose boundary TASK-004 documented, explicitly **not** Redis
   persistence.
4. **Write shape when persistence arrives.** One write-back per resolution,
   inside the existing record, under the existing `Sequence` compare-and-set
   (`§4 items 2, 5, 6`). No mid-resolution write: `§4 item 5` and
   `GAME_STATE.md` §5.1 item 2 forbid it, and the intermediate values are
   `ResolutionContext`, which is not serializable (`GAME_STATE.md` §3 item 3).
5. **Not a concurrency token.** `Sequence` remains the only one
   (`GAME_STATE.md` §5, `REDIS_STATE.md` §4 item 6).

---

### 10. Serialization

```text
Field presence:    MatchCount — always present once PlayerState exists
                   Combo      — always present once PlayerState exists
Nullability:       none. Both are non-nullable integers, not optional.
Default value:     MatchCount = 0 ; Combo = 0
Ordering:          PlayerState follows GAME_STATE.md §2.2's tree order
                   (Combo before MatchCount, as the tree lists them)
Backward compat:   additive; existing fields unchanged, none reordered,
                   none renamed
```

1. **No absence convention is needed.** Unlike `LastCommittedSwapPair`
   (`GAME_STATE.md` §2.1.10 item 3) and the optional cell `SpecialGem`
   (§2.1.7 item 3), both values are defined from battle creation: §2.2 says
   `MatchCount` is "cumulative Matches this battle" and §2.2/§6.1 item 1 give
   `Combo` a rule-level starting value of **0**. There is no "not yet occurred"
   state to represent — zero already means exactly that. So neither field is
   nullable and neither is omitted.
2. **`Combo = 0` is a real, publishable value** (`MATCH3_RULES.md` §6.5 item 4:
   it "reads 0 only before the battle's first committed Swap"). It must **not**
   be omitted on the wire as if absent; it is a value, not a gap.
3. **Deterministic ordering.** `Combo` and `MatchCount` are scalars, so no
   ordering rule applies *within* them. `PlayerState`'s own members are
   serialized in the §2.2 tree order, which is the same determinism rule
   `GAME_STATE.md` §2.1.7 applies to the board's cells.
4. **Round-trip losslessness** (`GAME_STATE.md` §2.1.7 item 5,
   `REDIS_STATE.md` §7 item 9): a record that drops, defaults, or recomputes
   either value does not round-trip. Both are **state, not derived** — `Combo`
   in particular cannot be re-derived from the board after the fact, and neither
   can be re-derived from `Turn` or `Sequence`
   (`GAME_STATE.md` §5.2 item 2: "`Sequence` is not a Match, Cascade, Combo, or
   Match-count value, and none of those is derived from it").
5. **Backward compatibility.** The change is **additive**: two new members
   inside a new nested object. No existing field is renamed, removed,
   reordered, or retyped. A reader that does not know `PlayerState` ignores it.
   The current wire payload is unchanged at this stage (§8 item 2).
6. **No serialization is implemented by this task**, because the existing
   contract only requires that the record match `GAME_STATE.md` §2 — and the
   §2 shape is not yet produced.

---

## Documentation Change Policy — Applied

**No document was rewritten, and no new ADR was created.**

Every contract item above is already derivable from existing documentation, so
the policy's first branch applies: *if a required decision is already derivable
from existing documentation, do not rewrite the documentation unnecessarily.*

```text
Owner (§1)              GAME_STATE.md §2, §2.2        already states PlayerState
                                                       owns Combo and MatchCount
Match semantics (§3)    GAME_RULES.md §3, §4;
                        MATCH3_RULES.md §3, §6        already complete
Combo semantics (§4)    GAME_RULES.md §5;
                        MATCH3_RULES.md §6            already complete
Categories (§5)         GAME_STATE.md §1, §3          already defines the five
                                                       categories and places both
                                                       values
Resolution (§6)         CascadeResolver / PassResult   code already sufficient
Events (§7)             GAME_EVENTS.md §1.1, §2       already complete
SignalR (§8)            SIGNALR_PROTOCOL.md §4 item 4 already states the
                                                       extension rule
Redis (§9)              REDIS_STATE.md §7 items 4–8   already states the deferral
Serialization (§10)     GAME_STATE.md §2.1.7;
                        REDIS_STATE.md §2, §7 item 9  already stated as a rule
```

**Why no ADR.** `docs/03-decisions/README.md` §2 lists "state-management-sensitive"
as ADR-worthy, and TASK-004A created ADR-010 for a genuinely new state field.
This case differs: **no new state decision is being made.** The owning object
(`PlayerState`), both field names, both semantics, the lifecycle, and the
boundaries are all already fixed by `GAME_STATE.md` §2.2 and
`MATCH3_RULES.md` §6. An ADR restating them would duplicate content "already
fully owned by a technical document", which `README.md` §2 explicitly says is
**not** a reason to create one. Creating one would also violate the task's
instruction not to rewrite documentation unnecessarily.

The only genuinely open item — that `BattleState` has no player identifier —
is resolved by the documentation's own structure (a closed field tree with no
such member, plus §2's one-player-per-battle framing), not by a new decision.
It is recorded here rather than invented into `docs/`.

---

## Conflict / STOP Conditions — Checked

All eight task STOP conditions were checked, and **none fired**:

```text
1. Two documents defining different owners     NOT PRESENT — §2.2 is the single
                                               owner, cited consistently by §3
                                               item 2, §5.3 item 1, MATCH3_RULES
                                               §6.1 item 1, §6.7 item 2
2. Match semantics vs MATCH3_RULES.md          NOT PRESENT — §3 above is §3 +
                                               §6.2/§6.3 restated, not changed
3. Combo semantics vs existing rules           NOT PRESENT — §4 above is §5 +
                                               §6 restated
4. Resolution cannot determine Match boundaries NOT PRESENT — one MatchResolution
                                               is exactly one Match (§5.4 item 4)
5. Player identity undeterminable              NOT PRESENT — see §2; no
                                               identifier is required, and none
                                               is defined
6. State lifecycle undeterminable              NOT PRESENT — §6.1/§6.4 own the
                                               reset, §6.5 the floor
7. Adding the state requires a rule change     NOT PRESENT — no rule is added,
                                               changed, or reinterpreted
8. Serialization/SignalR needs an undocumented
   gameplay contract                          NOT PRESENT — §8 uses the §4
                                               item 4 extension rule; §10 uses
                                               §2.1.7
```

**No documentation inconsistency was found.**

One phrasing was checked closely and confirmed to be consistent, recorded here
so the check is not repeated:

```text
Checked: MATCH3_RULES.md §2.1.5 item 5 — "Nothing else changes. No Match is
         counted, no Combo is set or reset ... and no battle state field is
         written."
         MATCH3_RULES.md §6.5 item 2 — after a rejected Swap, Combo "still
         holds the previous Swap's final value – it is not zeroed by a
         rejection."

Result:  CONSISTENT. "No Combo is set or reset" means the rejection performs no
         write, which is precisely why the value it held before the action is
         still the value afterwards. Precedent within the same item:
         LastCommittedSwapPair "neither records a pair nor clears the
         previously committed one" — a rejection changes nothing and therefore
         preserves everything. §6.1 item 3 states the same rule from the other
         direction ("neither resets nor changes Combo").

Note:    The similar-sounding clause at §2.1.1 item 3 ("The action carries no
         Gem type, no match result, no Combo value, no Turn, and no Sequence")
         is NOT about rejection output — it is the no-client-supplied-gameplay-
         field rule for the Swap request payload, and §7.2/§8 confirm it. It
         was initially misread as a rejection statement; it is not one, and no
         conflict exists.
```

**No gameplay rule was invented, changed, or reinterpreted.**

---

## Deliverables Checklist

```text
[✓]  1. Authoritative owner                     §1  PlayerState
[✓]  2. Exact state fields                      §3  MatchCount ; §4 Combo
[✓]  3. Initial values                          §3  0 ; §4 0
[✓]  4. Lifecycle / reset semantics             §4 (reset: committed Swap only)
[✓]  5. Turn-local vs battle-cumulative         §5
[✓]  6. Relationship to Resolution.Passes       §6
[✓]  7. Event-system boundary                   §7
[✓]  8. SignalR boundary                        §8
[✓]  9. Redis boundary                          §9
[✓] 10. Serialization contract                  §10
[✓] 11. Documentation changes                   none required — see above
[✓] 12. ADR                                     not required — see above
```

---

## Acceptance Criteria

```text
[✓] Authoritative owner explicitly defined
[✓] MatchCount semantics explicitly defined
[✓] Combo semantics explicitly defined
[✓] Initial values explicit
[✓] Reset behavior explicit
[✓] Rejected swap behavior explicit
[✓] Cascade behavior explicit
[✓] Multiple Matches in one Swap explicitly handled
[✓] Resolution.Passes relationship documented
[✓] Future Battle Event interaction documented
[✓] SignalR exposure documented
[✓] Redis ownership/persistence documented
[✓] Serialization behavior documented
[✓] No gameplay implementation added
[✓] No unrelated source files modified
[✓] No undocumented gameplay rule invented
[✓] No unnecessary documentation rewrite performed
[✓] Existing tests/build remain unaffected
[✓] Final report lists every changed file
[✓] Final report lists everything intentionally NOT implemented
[✓] No STOP condition fired — task is DONE, not BLOCKED
```

---

## Files Changed

**Created:**

- `tasks/completed/TASK-005A-player-battle-progression-state-contract.md` —
  this contract (`tasks/active/` → `tasks/completed/` at DONE).

**Modified:** none.

```text
src/                      UNTOUCHED — no source file created or modified
tests/                    UNTOUCHED — no test created or modified
docs/                     UNTOUCHED — no document rewritten
docs/03-decisions/ADR/    UNTOUCHED — no ADR created
```

**Pre-existing uncommitted work.** The working tree already carried
modifications and untracked files from earlier tasks (documentation updates,
Board Foundation / Special Gem / Swap execution sources and tests, frontend
runtime files, and the untracked `tasks/` files). `git status` therefore shows a
non-empty diff that this task did not produce. The only path this task added is
the task file itself. Noted so the diff is not misread — the same note
TASK-003, TASK-004, and TASK-004A also recorded.

**Verification that no implementation was added.** A repository-wide search for
`PlayerState`, `MatchCount`, and `Combo` across `src/backend` returns matches
**only inside XML documentation comments** — in `BattleState.cs`,
`BattleStateService.cs`, `ResolutionEvents.cs`, `BoardGenerator.cs`,
`BoardGenerationValidator.cs`, `GravityAndSpawn.cs`, `SwapValidator.cs`,
`SwapValidationResult.cs`, and `SwapRequest.cs`. Every one is a pre-existing
comment deferring the concept; none is an implementation. No `PlayerState` type
exists anywhere in `src/`.

---

## Tests

**No test was added, modified, or removed.** This task changes no behavior, so
there is nothing new to assert; adding a test for a contract with no
implementation would be a test of nothing.

Baseline was verified before and after the task:

```text
Domain:          Passed! - Failed: 0, Passed: 463, Total: 463
Application:     Passed! - Failed: 0, Passed:  37, Total:  37
Infrastructure:  Passed! - Failed: 0, Passed:   1, Total:   1
Api:             Passed! - Failed: 0, Passed:  25, Total:  25
                                                          ─────────────
Backend total:                                 526 passed, 0 failed
```

Identical to the TASK-004 baseline. No existing test was weakened, skipped, or
deleted.

---

## Build

```text
dotnet test src/backend/GameServer.sln
    Build succeeded. 0 Warning(s), 0 Error(s)
    526 passed, 0 failed
```

Unaffected — no compilable file was touched.

---

## Not Implemented

Everything the task forbids, plus everything deliberately deferred:

```text
Match counting                              NOT IMPLEMENTED
Combo counting / calculation                NOT IMPLEMENTED
MatchCreated / MatchResolved events         NOT IMPLEMENTED
CascadeCreated event                        NOT IMPLEMENTED
ComboChanged event                          NOT IMPLEMENTED
Battle Event system (any)                   NOT IMPLEMENTED
PlayerState type                            NOT CREATED — contract only
Passive progression                        NOT IMPLEMENTED
Power generation                            NOT IMPLEMENTED
Cards                                       NOT IMPLEMENTED
Relics                                      NOT IMPLEMENTED
Combat / Damage                             NOT IMPLEMENTED
Boss behavior                               NOT IMPLEMENTED
Rewards                                     NOT IMPLEMENTED
Frontend gameplay / UI                      NOT IMPLEMENTED
New SignalR gameplay messages               NOT INTRODUCED
Redis persistence implementation            NOT IMPLEMENTED
Redis keys / fields / schema                NOT ADDED
Database schema changes                     NONE
Multiplayer support                         NOT INTRODUCED
Match-3 resolver changes                    NONE
Serialization implementation                NONE
```

Also **not modified**: `BattleState.cs`, `CascadeResolver.cs`,
`BoardResolver.cs`, `SwapExecution.cs`, `MatchShape.cs`, `MatchPrimitive.cs`,
`ResolutionEvents.cs`, `BattleStateService.cs`, `BattleHub.cs`, and every
`docs/` file.

---

## Next Task

**TASK-005 — Match / Combo Accounting** is unblocked:

1. Create `PlayerState` per `GAME_STATE.md` §2.2, carrying `Combo` and
   `MatchCount` with the initial values and lifecycle defined above.
2. Add it as the `BattleState` field §2 already declares (§0 item 5), written in
   the same single post-write-back as `Turn`/`Sequence` (§5.1).
3. Count Matches and drive Combo by traversing `Resolution.Passes` in order —
   `Passes[i].Matches.Count` per pass, in the existing §3.2 order. Do not modify
   the resolver (§6) and do not count `ClearedCellUnion`.
4. Apply the §8.3 order: reset Combo at step 5, count during step 7, write at
   steps 8–9.
5. Emit nothing yet unless the task's scope includes `GAME_EVENTS.md` §1.1 —
   this contract only fixes *what the events would read* (§7).

Task 005 must implement the contract; it must not re-derive or alter it. If
implementation appears to require a different owner, field, or lifecycle, that
is a new contradiction to report per `AGENTS.md` §4 — not a licence to change
this contract.

**Do not begin TASK-005 as part of this task.**

---

## Status

DONE