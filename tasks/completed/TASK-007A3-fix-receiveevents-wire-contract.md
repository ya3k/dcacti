# TASK-007A3 — Fix ReceiveEvents Battle Event Wire Contract

---

## Metadata

```text
Task ID:           TASK-007A3
Type:              CONTRACT RESOLUTION (documentation only)
Status:            DONE
Risk:              LOW
Priority:          HIGH
Dependencies:      TASK-006 (Battle Event Emission, DONE)  — the produced batch
                   TASK-007A (ReceiveEvents Delivery Contract, DONE)
                   TASK-007A2 (wire-contract gap report, NOT FOUND ON DISK)
Blocks:            TASK-007 (ReceiveEvents Delivery) — must be re-run after this
```

---

## Discovery — What Was Read

```text
Root                  AGENTS.md (§2, §4, §7, §10, §11, §13, §16, §17, §18, §20)
docs/                 docs/AGENTS.md (§2, §4)
docs/00-overview/     MVP_SCOPE.md
docs/01-game-design/  GAME_RULES.md (§16, §17, §18)
                      MATCH3_RULES.md (§1.0, §1.1, §3, §3.1–§3.4, §4.2,
                                       §5.1–§5.5.4, §6.6, §8)
docs/02-technical/    SIGNALR_PROTOCOL.md (§0–§8, in full)
                      GAME_EVENTS.md (§1, §1.1, §1.2, §1.3, §2, §3 — in full)
                      GAME_STATE.md (§2.1.1–§2.1.10, §2.2, §2.6, §3, §5, §5.1)
                      ARCHITECTURE.md (§1, §2.1, §2.2.1, §4.1, §5)
                      TDD.md (§0, §2.1)
docs/03-decisions/    README.md (§1–§8), ADR-001, ADR-004, ADR-008
tasks/                active/TASK-007A-resolve-receiveevents-delivery-contract.md
                      completed/TASK-006-battle-event-emission.md
Existing implementation / tests:
                      src/backend/GameServer.Domain/Match3/BattleEvent.cs
                      src/backend/GameServer.Domain/Match3/BattleEventBuilder.cs
                      src/backend/GameServer.Domain/Match3/ResolutionEvents.cs
                      src/backend/GameServer.Domain/Match3/MatchShape.cs
                      src/backend/GameServer.Domain/Match3/MatchPrimitive.cs
                      src/backend/GameServer.Domain/Match3/SpecialGem.cs
                      src/backend/GameServer.Domain/Match3/SpecialGemClaim.cs
                      src/backend/GameServer.Domain/Match3/GemType.cs
                      src/backend/GameServer.Domain/Match3/SwapExecution.cs
                      src/backend/GameServer.Api/Hubs/BattleHub.cs
                      src/frontend/client/src/services/realtime/SignalRService.ts
                      src/frontend/client/src/game/runtime/GameRuntimeEvents.ts
                      tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs
```

**TASK-007A2's report was not found.** No file named `TASK-007A2` exists under
`tasks/` (or anywhere in the repository), and `git log` shows only two commits.
TASK-007A2's findings were therefore reproduced independently rather than read
— see "Independent Reproduction" below. The gap it identified is real and was
confirmed by direct execution.

---

## Independent Reproduction of the Gap

The premise of the task ("direct `System.Text.Json` serialization is invalid
because non-applicable payload accessors throw") was **verified by running it**,
not assumed:

```text
Probe: System.Text.Json.JsonSerializer.Serialize(BattleEvent) over a real
       committed Swap's event list (BoardGenerator + SwapExecutor).

Result — every event failed:

  [MatchCreated]  InvalidOperationException: A MatchCreated event carries no
                  Cascade depth. Check Type before reading CascadeDepth.
  [GemMatched]    InvalidOperationException: A GemMatched event carries no
                  Match. Check Type before reading Match.
  [ComboChanged]  InvalidOperationException: A ComboChanged event carries no
                  Match. Check Type before reading Match.
```

The same failure occurs end-to-end through the hub:

```text
dotnet test ... --filter "FullyQualifiedName~ReceiveEvents"
  Failed: 7, Passed: 4, Total: 11

  Swap_ShouldSendExactlyOneReceiveEventsBatch_WithTheDocumentedThreeMembers
    Microsoft.AspNetCore.SignalR.HubException : The server closed the
    connection with the following error: Connection closed with an error.
```

The current `ReceiveEvents` implementation sends `BattleEvent` values directly
(`ReceiveEventsPayload(string, int, IReadOnlyList<BattleEvent>)`), so the
serializer reflects over all four throwing accessors and aborts the send,
closing the connection. **The wire contract gap is a live, reproducible defect
— not a theoretical one.** This is exactly why the contract had to be fixed
before TASK-007 can deliver.

*(The probe was a temporary project outside `src/` and `tests/`; it was deleted
after the run. No production file was created or modified.)*

---

## Primary Decision — Wire Schema Ownership

```text
SIGNALR_PROTOCOL.md §3.2   OWNS the exact wire schema of events[]
GAME_EVENTS.md §2          OWNS what each event means / its payload content
```

### The circular deferral, and its resolution

```text
BEFORE

GAME_EVENTS.md §3 item 1  →  "wire-level envelope ... see SIGNALR_PROTOCOL.md"
SIGNALR_PROTOCOL.md §8.1  →  "exact wire schema is an implementation detail"
                              (i.e. owned by nobody)
```

Both documents deferred, so the schema was owned by **no** document. This task
assigns it to exactly one:

```text
AFTER

SIGNALR_PROTOCOL.md §3.2     owns the exact wire schema (authoritative)
GAME_EVENTS.md §3 item 1     delegates to §3.2, does not restate it
SIGNALR_PROTOCOL.md §8.1     no longer calls the schema an implementation detail
```

The split is **semantics vs. wire schema** — a real division of ownership, not
a mutual deferral. No schema is duplicated: `GAME_EVENTS.md` gained a pointer
only and contains no JSON.

---

## The Contract

### Serialization boundary

```text
BattleEvent                 Domain — throwing accessors, NOT a wire DTO
        ↓ transport projection   Application/API — pure field mapping
ReceiveEvents payload       { battleId, serverSequence, events[] }
        ↓ SignalR JSON
Frontend                    opaque (ARCHITECTURE.md §2.2.1 rule 4)
```

`BattleEvent` stays a Domain type and stays non-wire. Its accessors were not
modified, weakened, or removed, and Domain was not made JSON-friendly. The
transport layer owns serialization.

### Discriminator

| Property | Encoding | Values |
| --- | --- | --- |
| `type` | **string** (never numeric) | `MatchCreated`, `CascadeCreated`, `ComboChanged`, `GemMatched` |

Flat objects: the discriminator and the event's own payload members are
siblings. The set is closed; the Domain enum ordinal is not on the wire.

### Property casing

**`camelCase`, fixed explicitly** — not delegated to `System.Text.Json`'s
PascalCase default. Case-sensitive.

### Enum representation

All enums are **strings**, never numbers:

| Enum | Wire member | Values |
| --- | --- | --- |
| `GemType` | `gemType` | `ATK`, `DEF`, `HP`, `POWER` |
| `SpecialGemType` | `specialGem.type` | `LineClear`, `Burst`, `Area` |
| `SpecialGemOrientation` | `specialGem.orientation` | `Horizontal`, `Vertical` |

`GemType` uses the uppercase contract names `MATCH3_RULES.md` §1.1 already
fixes and the board projection already carries; Special Gem values use their
documented `GAME_STATE.md` §2.1.4 spellings.

### Optionality — **omitted**, never explicit `null`

Not applicable ⇒ **member omitted entirely**. No member of any event is ever
sent as `null`.

This is derived from the *existing documented convention*, not from the
throwing accessors: `GAME_STATE.md` §2.1.7 item 3 already fixes the board's
optional `SpecialGem` as "**absent**, not a null element, not a sentinel type,
and not a default value". The new contract reuses that convention so an
optional member means one thing across the whole protocol, rather than
inventing a second convention.

### The four events

```json
{ "type": "MatchCreated", "shape": "Straight", "cells": [8, 9, 10],
  "gemType": "ATK", "cascadeDepth": 1 }

{ "type": "MatchCreated", "shape": "Lt", "cells": [9, 10, 11, 17, 25],
  "gemType": "ATK", "cascadeDepth": 1,
  "createdSpecialGems": [ { "cellIndex": 9,
                            "specialGem": { "type": "LineClear",
                                            "orientation": "Horizontal" } } ] }

{ "type": "CascadeCreated", "cascadeDepth": 1 }

{ "type": "ComboChanged", "combo": 3 }

{ "type": "GemMatched", "cellIndex": 27, "gemType": "HP" }

{ "type": "GemMatched", "cellIndex": 27, "gemType": "HP",
  "specialGem": { "type": "LineClear", "orientation": "Horizontal" } }
```

### Match representation, and `MatchShape`

`MatchShape` is **not serialized mechanically.** Exposed:

```text
shape                "Straight" | "Lt"   — the documented shape identity
cells                ascending §1.0 indices, each cell once
gemType              the Match's Gem type
cascadeDepth         the detection pass's own depth (1 = not a Cascade)
createdSpecialGems   the committed creations, in §5.5.1 order
```

**Deliberately not exposed:** `HorizontalArm`, `VerticalArm`, arm lengths,
`StartIndex`, `IntersectionIndex`, `Step`, and `SortKey`. These are Domain
implementation geometry. Everything `GAME_EVENTS.md` §2 requires is already
carried by the five members above, and exposing arms would leak Domain detail
and invite client-side geometry derivation, which `ARCHITECTURE.md` §2.2.1
rule 3 and `GAME_RULES.md` §18 forbid.

`shape` uses the two categories the game rules actually own
(`MATCH3_RULES.md` §3 item 1, §3 item 3, §5.4 item 1) — `Straight` and `Lt`.
No separate `L`/`T` tier is invented (`MATCH3_RULES.md` §5.4 items 1–3).

**Note on `cascadeDepth`.** `MatchCreated.cascadeDepth` is the **pass depth**
(1 = the post-Swap pass); `CascadeCreated.cascadeDepth` is the **depth index
within the Swap** (1 = the Swap's *second* pass — `GAME_EVENTS.md` §2). The
member name is shared because both answer "which pass", but the two carry
different documented values on the same pass and the contract says so
explicitly, so neither is mistaken for the other.

### Special Gems — `SpecialGemClaim` is NOT serialized

`SpecialGemClaim` is documented as **Transient Resolution State**
(`GAME_STATE.md` §3): pass-local bookkeeping that "is never a `BattleState`
field, is never serialized or delivered, and does not survive the pass that
built it". Its `ShapeIndex` / `IntraShapeOrder` / `Source` members are the
§5.5.1/§5.5.4 collision keys, which `GAME_STATE.md` §2.1.6 item 4 states are
never serialized.

A **transport-level** representation is therefore defined instead, carrying
only the two facts any consumer needs — exactly the `SpecialGem` metadata shape
`GAME_STATE.md` §2.1.4 owns and the board already carries:

```text
specialGem
├── type            "LineClear" | "Burst" | "Area"   always
└── orientation     "Horizontal" | "Vertical"        present iff type is LineClear
```

It carries **no** `cellIndex` (the sibling `cellIndex` already states it), **no**
`GemType` (the event's own `gemType` already states it, and both arms of a shape
share one type), and no identity/id/order/depth/age. Orientation is present
**iff** the type is `LineClear`, matching `GAME_STATE.md` §2.1.4 item 2.

The `GemMatched` consumed Special Gem and the `MatchCreated` created Special
Gems both use this one representation. No `SpecialGemActivated` /
`SpecialGemCreated` event was introduced (`GAME_EVENTS.md` §2 item 3).

---

## Documentation Changes

| File | Change |
| --- | --- |
| `docs/02-technical/SIGNALR_PROTOCOL.md` | **+ §3.2** "The `events[]` Wire Schema" (§3.2.1–§3.2.12): boundary, discriminator, casing, enums, optionality, all four events, Special Gems. **§8 item 1 rewritten** so the schema is no longer an implementation detail. Version → 1.5. |
| `docs/02-technical/GAME_EVENTS.md` | **§3 item 1 rewritten** to delegate to the single owner (`SIGNALR_PROTOCOL.md` §3.2) without restating any schema. Version → 1.4. |

No other document was edited. No schema is duplicated across the two files:
`GAME_EVENTS.md` contains a pointer and no JSON.

---

## ADR

```text
NOT REQUIRED — NOT CREATED
```

Per `docs/03-decisions/README.md`:

- **§2** — do NOT create an ADR for a decision "that duplicates content already
  fully owned by a technical document." The wire schema is exactly such
  content: `SIGNALR_PROTOCOL.md` is the technical document that owns how
  realtime communication works, and §3.2 now owns the schema.
- **§6** — the ADR layer answers *why*; `SIGNALR_PROTOCOL.md` answers *how*. An
  ADR restating §3.2 would be a *how* in the *why* layer.
- **§3** — an ADR must never override a technical document, so it could add no
  authority here.
- **`AGENTS.md` §18** requires an ADR when architecture, the battle-state model,
  the authoritative model, module boundaries, or infrastructure change. **None
  changed.** The contract records a representation; it moves no responsibility
  across a boundary. ADR-004 already records SignalR as the accepted transport
  and defers the wire to `SIGNALR_PROTOCOL.md` (its Related Documents list the
  whole document), so no new transport decision exists to record.

---

## Constraint Compliance

```text
Match counting             UNCHANGED        Combo counting          UNCHANGED
Cascade semantics          UNCHANGED        Special Gem semantics   UNCHANGED
Board resolution           UNCHANGED        RNG                     UNCHANGED
Turn                       UNCHANGED        Sequence                UNCHANGED
Battle state               UNCHANGED        Event ordering          UNCHANGED
Event names                UNCHANGED        Event count (4)         UNCHANGED

BattleEvent                UNMODIFIED       BattleEventBuilder.cs   UNMODIFIED
MatchShape / MatchPrimitiveUNMODIFIED       SpecialGemClaim.cs      UNMODIFIED
ReceiveEvents envelope     UNCHANGED (3 members)
ReceiveEvents impl.        NOT IMPLEMENTED BY THIS TASK
Transport projection       NOT IMPLEMENTED BY THIS TASK
Frontend code              UNCHANGED (0 files)
Production code            UNCHANGED (0 files)
New event types            NONE
New gameplay fields        NONE
ADR                        NOT CREATED (policy excludes it)
```

**Files modified by this task: 2 documents.** `git status` confirms no file
under `src/` or `tests/` was touched by this task; `BattleEvent.cs`,
`GameRuntime.ts`, and `GameRuntimeEvents.ts` retain their pre-task mtimes.

---

## Required Verification

```text
[✓] One document clearly owns the exact wire schema        SIGNALR_PROTOCOL.md §3.2
[✓] ReceiveEvents envelope remains unchanged               3 members, §3 untouched
[✓] Exactly four existing event types represented          closed discriminator set
[✓] Event ordering remains unchanged                       §3.2.12 item 4; schema only
[✓] Every documented event payload field has a wire repr.  §3.2.6–§3.2.11
[✓] No undocumented gameplay field is invented             §3.2.12 item 3
[✓] Discriminator is explicitly defined                    §3.2.2 — `type`, string
[✓] Property casing is explicitly defined                  §3.2.3 — camelCase, fixed
[✓] Enum representation is explicitly defined              §3.2.4 — strings, never numeric
[✓] Nullable/omitted behavior is explicitly defined        §3.2.5 — omitted, never null
[✓] Match representation is explicitly defined             §3.2.6
[✓] Special Gem representation is explicitly defined       §3.2.10, §3.2.11
[✓] BattleEvent remains a Domain type                      unmodified
[✓] BattleEvent remains non-wire                           §3.2.1 item 1
[✓] Frontend remains opaque                                §3.2.1 item 4
[✓] No production code changed                             0 files under src/
[✓] No frontend code changed                               0 files
[✓] No gameplay rules changed                              semantics untouched
[✓] No new event types introduced                          4 only
[✓] No ADR created (policy excludes it)                    README.md §2/§3/§6
```

---

## STOP Conditions

**None fired.**

```text
1  Contradictory requirements that cannot be reconciled
   NO. The two documents did not conflict — they mutually deferred, leaving the
   schema unowned. Ownership is assignable per AGENTS.md §2 (SIGNALR_PROTOCOL.md
   owns how realtime communication works), so the deferral is resolved rather
   than adjudicated.

2  Would require changing gameplay semantics
   NO. Zero gameplay change; the contract is a representation of existing
   semantics only.

3  Required payload cannot be represented without inventing gameplay info
   NO. Every member maps to a documented value: shape identity and cells
   (MATCH3_RULES.md §3.1/§3.3), Gem type (§3.3 item 2), pass depth (§4.2),
   created Special Gems (§5.5.1/§5.5.4), Cascade depth index (§4.2 item 2),
   new Combo value (GAME_EVENTS.md §2), cell index and consumed Special Gem
   (§2 items 1–2). Nothing was invented.

4  Existing event semantics ambiguous in a way that materially affects the wire
   NO. Two subtleties were found and resolved by explicit statement rather than
   guesswork, both already determined by existing docs:
     (a) MatchShape geometry — §2 requires shape (cells), Gem type, tier,
         created Special Gems; arm geometry is Domain detail, so the wire
         carries the documented shape identity plus cells.
     (b) The two `cascadeDepth` values — MatchCreated's pass depth vs
         CascadeCreated's depth index, both fixed by GAME_EVENTS.md §2 and
         MATCH3_RULES.md §4.2. The contract names the distinction explicitly.

5  Representation would require frontend gameplay interpretation
   NO. ARCHITECTURE.md §2.2.1 rule 4 keeps the runtime opaque; the schema is
   defined for the server's projection, and §3.2.1 item 4 states that defining
   it does not require the client to model it.

6  An existing ADR conflicts with the proposed contract
   NO. ADR-004 records SignalR as the transport and defers the wire to
   SIGNALR_PROTOCOL.md. ADR-001 (server-authoritative) and ADR-008 (snapshot
   recovery) are unaffected. No conflict.

7  Would require changing BattleEvent Domain semantics
   NO. BattleEvent was not modified, and its throwing accessors were neither
   removed nor weakened — the contract is built on top of them.

8  Schema cannot be uniquely fixed without another human gameplay/product decision
   NO. Every decision above is determined by existing authoritative documents;
   see the derivation table in each §3.2 subsection.
```

---

## Remaining Issues

Recorded, not fixed (`AGENTS.md` §16):

```text
1. The current ReceiveEvents implementation serializes BattleEvent directly and
   therefore fails (7 of 11 ReceiveEvents integration tests red). This task
   defined the contract; implementing the transport projection is TASK-007's
   work and was explicitly out of scope here. TASK-007 must now project onto
   the §3.2 schema instead of sending Domain values.
   Impact:  the client receives no event batch until TASK-007 lands.
   Follow-up: run TASK-007 against this contract.

2. ApiIntegrationTests.Swap_ReceiveEvents_ShouldPreserveTheExecutorEventOrderExactly
   derives its expectation from e.ToString() and reads only e.type from the
   wire, so it will need its expectation reshaped to the §3.2 schema when
   TASK-007 implements the projection. Its assertion intent (order preserved,
   batch atomic) stays valid and must not be weakened.
   Impact:  one test's expectation form, owned by TASK-007.
   Follow-up: TASK-007.

3. MatchResolved and the action-boundary events remain unemitted (TASK-006
   Risks). Unchanged by this task; the contract covers exactly the four events
   that exist, and §3.2.2 item 2 explicitly names MatchResolved as not a valid
   wire type.
   Impact:  none on this contract.
   Follow-up: none here.

4. TASK-007A2's report is not present on disk, so its findings were reproduced
   independently (see "Independent Reproduction"). Both the deserialization
   failure and the test failures were confirmed by direct execution.
   Impact:  none — the gap is confirmed either way.
   Follow-up: none required.
```

---

## Final Report

```text
DONE

Wire schema owner:
docs/02-technical/SIGNALR_PROTOCOL.md §3.2 ("The `events[]` Wire Schema",
§3.2.1–§3.2.12) — the single owner of the exact wire schema of
ReceiveEvents.events[]. GAME_EVENTS.md keeps event semantics and payload
content (§2) and now delegates the wire shape without restating it. The former
mutual deferral is resolved: SIGNALR_PROTOCOL.md §8 item 1 no longer calls the
schema an implementation detail.

Transport representation:
A flat camelCase JSON object per event, discriminated by a string `type` whose
closed value set is exactly the four existing names — MatchCreated,
CascadeCreated, ComboChanged, GemMatched. MatchCreated carries shape
("Straight" | "Lt"), cells (ascending §1.0 indices), gemType, cascadeDepth (the
detection pass's own depth) and, when non-empty, createdSpecialGems. CascadeCreated
carries cascadeDepth as the depth index within the Swap (1 = the Swap's second
pass) — deliberately a different documented value from MatchCreated's, and the
contract says so. ComboChanged carries the integer `combo` (the new value).
GemMatched carries cellIndex, gemType, and `specialGem` only when a Special Gem
was consumed at that cell. Enums are always strings, never numbers: GemType as
ATK/DEF/HP/POWER, Special Gem type as LineClear/Burst/Area, orientation as
Horizontal/Vertical. Not-applicable members are OMITTED — never explicit null —
reusing the board's documented absence convention (GAME_STATE.md §2.1.7 item 3)
rather than inventing a second one. MatchShape is not serialized mechanically:
its arms, arm lengths, StartIndex, IntersectionIndex and Step stay Domain-side,
because the five documented facts already cover GAME_EVENTS.md §2. SpecialGemClaim
is never serialized — it is Transient Resolution State (GAME_STATE.md §3) whose
collision keys §2.1.6 item 4 states are never serialized — so a transport-level
{type, orientation?} representation is defined instead for both created and
consumed Special Gems.

Domain boundary:
PRESERVED. BattleEvent remains a Domain type and remains non-wire; its throwing
accessors were not removed, weakened, or modified, and Domain was not made
JSON-friendly. The documented direction Domain → Application/API transport
projection → SignalR → Frontend is stated as the boundary, with serialization
owned by the transport layer. The frontend stays opaque per ARCHITECTURE.md
§2.2.1 rule 4.

Documentation changes:
docs/02-technical/SIGNALR_PROTOCOL.md — new §3.2 (§3.2.1–§3.2.12) making this
document authoritative for the exact event wire schema; §8 item 1 rewritten to
end the "implementation detail" deferral; version → 1.5.
docs/02-technical/GAME_EVENTS.md — §3 item 1 rewritten to delegate to the single
owner, with a pointer and no duplicated schema; version → 1.4.
No other document edited.

ADR:
NOT REQUIRED — NOT CREATED. docs/03-decisions/README.md §2 excludes an ADR that
duplicates content a technical document fully owns; §6 keeps the *how* out of
the *why* layer; §3 means an ADR could add no authority. AGENTS.md §18 requires
an ADR only for architecture, state-model, authority, boundary, or
infrastructure changes — this task made none. ADR-004 already records SignalR as
the transport and defers the wire to SIGNALR_PROTOCOL.md.

Production code:
UNCHANGED

Frontend:
UNCHANGED
```

---

## Status

DONE

---

## Handoff

TASK-007 may now be run. It must implement a **transport projection** from
`BattleEvent` onto the §3.2 schema rather than serializing Domain values: read
`Type` first, then only that event's own payload, and emit the flat camelCase
object §3.2 defines. The seven currently red `ReceiveEvents` integration tests
are the acceptance surface, and existing `BattleEventEmissionTests.cs` must stay
untouched since TASK-006's emission contract is its input, not its subject.