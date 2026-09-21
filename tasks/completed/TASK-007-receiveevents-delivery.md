# TASK-007 — ReceiveEvents Delivery

---

## Metadata

```text
Task ID:           TASK-007
Type:              IMPLEMENTATION
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Dependencies:      TASK-006 (Battle Event Emission, DONE) — the produced batch
                   TASK-007A (ReceiveEvents Delivery Contract, DONE)
                   TASK-007A3 (Fix ReceiveEvents Wire Contract, DONE) — the §3.2 schema
Blocks:            Frontend event consumption (client receives a batch again)
```

---

## Discovery — What Was Read

```text
Root                  AGENTS.md (§2, §4, §6, §9, §10, §13, §14, §15, §16, §17,
                                 §18, §20, §22, §23)
docs/                 docs/AGENTS.md (§2, §4)
docs/02-technical/    SIGNALR_PROTOCOL.md (§3 in full: §3, §3.1, §3.2,
                                           §3.2.1–§3.2.12; §4 envelope context)
                      GAME_EVENTS.md (§1, §1.1, §1.2, §1.3, §2, §3 — in full)
                      GAME_STATE.md (§2.1.3, §2.1.4, §2.1.7, §2.2, §3, §5, §5.1)
                      ARCHITECTURE.md (§1, §2.1, §2.2.1, §4.1, §5)
                      TDD.md (§2.1)
docs/03-decisions/    README.md, ADR-001, ADR-004, ADR-008
tasks/                active/TASK-007A3-fix-receiveevents-wire-contract.md
                      active/TASK-007A-resolve-receiveevents-delivery-contract.md
                      completed/TASK-006-battle-event-emission.md
Existing implementation / tests:
                      src/backend/GameServer.Domain/Match3/BattleEvent.cs
                      src/backend/GameServer.Domain/Match3/BattleEventBuilder.cs
                      src/backend/GameServer.Domain/Match3/ResolutionEvents.cs
                      src/backend/GameServer.Domain/Match3/MatchShape.cs
                      src/backend/GameServer.Domain/Match3/SpecialGem.cs
                      src/backend/GameServer.Domain/Match3/SpecialGemClaim.cs
                      src/backend/GameServer.Domain/Match3/GemType.cs
                      src/backend/GameServer.Domain/Match3/SwapExecution.cs
                      src/backend/GameServer.Application/Battle/BattleStateService.cs
                      src/backend/GameServer.Api/Hubs/BattleHub.cs
                      src/backend/GameServer.Api/Program.cs
                      tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs
                      src/frontend/client/src/game/runtime/GameRuntime.ts
                      src/frontend/client/src/game/runtime/GameRuntimeEvents.ts
                      src/frontend/client/src/services/realtime/SignalRService.ts
```

---

## Reproduction of the Defect (before the change)

The task's premise was verified by running the existing suite, not assumed:

```text
dotnet test tests/backend/GameServer.Api.Tests --filter "FullyQualifiedName~ReceiveEvents"

  Failed: 7, Passed: 4, Total: 11

  Swap_ShouldSendExactlyOneReceiveEventsBatch_WithTheDocumentedThreeMembers
    Microsoft.AspNetCore.SignalR.HubException : The server closed the
    connection with the following error: Connection closed with an error.
```

Exactly the failure `TASK-007A3` recorded. The cause was confirmed at the source:
`BattleHub.ReceiveEventsPayload` declared `IReadOnlyList<BattleEvent>`, so the
SignalR JSON serializer reflected over `BattleEvent`'s four public accessors and
aborted the send on the three that throw for the event's own kind.

The same run after the projection landed:

```text
  Failed: 0, Passed: 18, Total: 18
```

---

## Understanding

`BattleEvent` is a Domain value whose non-applicable payload accessors
(`Match`, `CascadeDepth`, `Combo`, `Gem`) throw rather than return a default.
`SIGNALR_PROTOCOL.md` §3.2.1 item 1 states that direct `System.Text.Json`
serialization of it is therefore **invalid by construction**. The delivery path
had to gain a transport representation between Domain and SignalR, without
changing `BattleEvent`, the gameplay semantics, or the frontend.

The documented boundary is:

```text
BattleEvent (Domain)
        ↓  transport projection          GameServer.Api.Hubs — pure field mapping
BattleEventWireDto
        ↓  ReceiveEventsPayload          { battleId, serverSequence, events[] }
        ↓  SignalR JSON
Frontend                                 opaque (ARCHITECTURE.md §2.2.1 rule 4)
```

---

## Relevant Documentation

| Fact implemented | Owner |
| --- | --- |
| `BattleEvent` is not a wire DTO; transport owns serialization | `SIGNALR_PROTOCOL.md` §3.2.1 items 1–2 |
| Projection is pure, one-to-one, order-preserving | §3.2.1 item 3 |
| Discriminator `type`, string, closed set of four | §3.2.2 |
| camelCase, fixed explicitly (not a serializer default) | §3.2.3 |
| Enums are strings, never numbers | §3.2.4 |
| Not-applicable members omitted, never `null` | §3.2.5 |
| `MatchCreated` members and `MatchShape` non-exposure | §3.2.6 |
| `CascadeCreated` depth index (≠ MatchCreated's pass depth) | §3.2.7 |
| `ComboChanged` carries the new, already-accounted value | §3.2.8 |
| `GemMatched` members and specialGem presence rule | §3.2.9 |
| Special Gem transport shape; `SpecialGemClaim` never serialized | §3.2.10 |
| `createdSpecialGems` entry shape | §3.2.11 |
| No member outside the schema; ordering unchanged | §3.2.12 |
| Envelope: three members, one batch, post-write-back, no batch on rejection | §3, §3.1 |
| `BattleStateUpdated` responsibilities unchanged | §3.1 item 3, §4 |

---

## Plan

1. Add a transport projection at the Api/hub boundary (not Domain).
2. Model the four documented flat objects as one DTO with four one-of slots, so
   no member outside §3.2 can be constructed.
3. Fix camelCase per member and omission per member.
4. Change `ReceiveEventsPayload.Events` to the wire DTO list and project the
   executor's list one-to-one.
5. Rewrite the `ToString()`-derived expectation to assert the wire
   representation member-for-member.
6. Add per-event schema tests and a real serialization-boundary regression test.
7. Run focused, backend, and frontend verification.

---

## Changes

### New — `src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs`

The transport projection and its wire DTOs:

```text
BattleEventWireDto           the flat wire item — one discriminator + the
                             event's own members, four one-of slots
CreatedSpecialGemWireDto     §3.2.11 entry { cellIndex, specialGem }
SpecialGemWireDto            §3.2.10 { type, orientation? }
BattleEventWireProjection    the pure field mapping
```

- The discriminator is read first and only the matching event's own payload is
  then read, so no throwing accessor is ever touched.
- `shape` is the identity `Straight` | `Lt`. `MatchShape` arms, arm lengths,
  `StartIndex`, `IntersectionIndex`, `Step`, and `SortKey` are **not** projected.
- `cascadeDepth` is taken from the payload that owns it: `MatchResolution.CascadeDepth`
  (pass depth) for `MatchCreated`, `BattleEvent.CascadeDepth` (depth within the
  Swap) for `CascadeCreated`. Neither is normalized into the other.
- `combo` is read as the already-accounted value and never recomputed.
- Only `CellIndex` and `SpecialGem` of each `SpecialGemClaim` are projected; its
  `ShapeIndex`, `IntraShapeOrder`, `Source`, and `GemType` never reach the wire.
- `createdSpecialGems` normalizes an empty list to omission, so a present member
  is never an empty array.
- Unknown `BattleEventType` throws rather than being dropped, preserving the
  one-to-one count the batch's atomicity depends on.

### Modified — `src/backend/GameServer.Api/Hubs/BattleHub.cs`

```csharp
public record ReceiveEventsPayload(
    [property: JsonPropertyName("battleId")] string BattleId,
    [property: JsonPropertyName("serverSequence")] int ServerSequence,
    [property: JsonPropertyName("events")] IReadOnlyList<BattleEventWireDto> Events);
```

`events[]` now carries wire DTOs, and `ToPayload(SwapExecutionResult)` projects
the executor's list through `BattleEventWireProjection.Project`. Doc comments
were updated to state the projection. The envelope gained no fourth member, the
`SendAsync` call site and its position are unchanged, and `BattleStateUpdated`
is untouched.

### Modified — `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs`

Test expectations moved from `BattleEvent.ToString()` to the documented wire
representation, and new coverage was added (see Tests).

---

## A Defect the New Tests Caught

The first implementation pass produced **valid JSON but the wrong schema**:
SignalR's server-side default writes `null` for absent members, so a
`ComboChanged` serialized as

```json
{"type":"ComboChanged","combo":1,"shape":null,"cells":null,"gemType":null,
 "cascadeDepth":null,"createdSpecialGems":null,"cellIndex":null,"specialGem":null}
```

That satisfies "the payload can be serialized" but violates `§3.2.5`, which
requires absence to be **omission** — "A producer must not emit `null` for any
optional member of this contract" — and `§3.2.12` item 1, which allows no
member outside the four tabulated sets.

The fix is per-member `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`
plus explicit `[JsonPropertyName]` on every member of all three DTOs, so both
the omission rule and the camelCase rule travel with the contract instead of
depending on a host-wide serializer option (`§3.2.3` item 2 warns explicitly
against relying on a serializer default).

**This is why the tests assert the serialized bytes rather than in-memory DTO
construction** — an in-memory-only test would have passed this defective
implementation.

---

## Serialization

`BattleEvent` is never serialized. It is read by `BattleEventWireProjection`
and discarded; the only type reaching `SendAsync` is `ReceiveEventsPayload`
carrying `BattleEventWireDto` values.

The regression test `Swap_ReceiveEvents_ShouldSerializeAcrossTheSignalRJsonBoundary`
commits a real Swap through the hub, awaits the batch across the SignalR client's
own JSON decoding, and asserts every item is a flat object with a string
discriminator and no `null` member. Before the projection existed this exact
call threw `HubException("The server closed the connection with the following
error")`.

---

## Tests

### Focused (`FullyQualifiedName~ReceiveEvents`)

```text
Failed: 0, Passed: 18, Total: 18
```

Coverage, by the task's required list:

```text
Envelope
  exactly one ReceiveEvents for one accepted swap        Swap_ShouldSendExactlyOne...ThreeMembers
  correct battleId / serverSequence / events array       same
  serverSequence == post-writeback authoritative Sequence
                                                        ...CarryTheCommittedPostResolutionSequence
  event count matches the Domain result count            ...ShouldPreserveTheExecutorEventOrderExactly

Event ordering
  CascadeCreated | MatchCreated | GemMatched… | ComboChanged preserved
                                                        ...ShouldPreserveTheExecutorEventOrderExactly
    — compared member-for-member against the §3.2 wire representation, plus
      positive assertions that CascadeCreated precedes its pass's Matches and
      that ComboChanged follows the Match it reports (GAME_EVENTS.md §1.1)

MatchCreated        type, shape, cells, gemType, cascadeDepth, createdSpecialGems,
                    geometry non-exposure                     ...MatchCreated_ShouldCarryTheDocumentedWireSchema
                    empty createdSpecialGems is omitted        ...ShouldOmitCreatedSpecialGems_WhenTheMatchCreatedNone
CascadeCreated      depth semantics independent of MatchCreated
                                                              ...CascadeCreated_ShouldCarryItsOwnDocumentedDepthSemantics
ComboChanged        type, exact new combo value               ...ComboChanged_ShouldCarryTheExactAccountedComboValue
GemMatched          type, cellIndex, gemType; specialGem omitted when absent and
                    represented correctly when consumed       ...GemMatched_ShouldCarryTheDocumentedWireSchema
Serialization       the payload crosses the real SignalR/JSON boundary
                                                              ...ShouldSerializeAcrossTheSignalRJsonBoundary
No extra members    no member outside the four tabulated sets  ...ShouldCarryNoMemberOutsideTheDocumentedSchema
Rejection           no batch, state unchanged (INVALID_CELL_INDEX, INVALID_SWAP,
                    NO_MATCH_FROM_SWAP, STALE_ACTION in either argument order)
                                                              Swap_ForARejectedRequest_ShouldSendNoReceiveEvents
                                                              Swap_ForANonMatchProducingSwap_ShouldSendNoReceiveEvents
                                                              Swap_ForAStaleReplayInEitherArgumentOrder_ShouldSendNoReceiveEvents
Unknown battle      no batch, existing behavior preserved      Swap_ForUnknownBattle_ShouldSendNoReceiveEvents
Atomicity           one accepted swap → exactly one batch; no event-specific
                    SignalR message introduced
                                                              ...ShouldNotReplaceTheExistingBattleStateUpdatedBehavior
                                                              Swap_ShouldSendNoAdditionalGameplayMessage
```

### Full suites

```text
Backend   Failed: 0, Passed: 623, Total: 623
          (Domain 535 · Application 41 · Infrastructure 1 · Api 46)
Frontend  Failed: 0, Passed: 174, Total: 174 (12 files)
Build     0 warnings / 0 errors (dotnet build GameServer.sln)
          Frontend: tsc + vite build succeeded
```

No assertion was weakened, removed, skipped, or deleted. The one expectation
whose *mechanism* changed (`Swap_ReceiveEvents_ShouldPreserveTheExecutorEventOrderExactly`)
is strictly stronger: it previously compared derived type strings and now
compares every member of every item against the §3.2 wire representation.

---

## Domain Invariants

```text
BattleEvent            UNMODIFIED
BattleEventBuilder     UNMODIFIED
MatchDetector          UNMODIFIED
SpecialGemPlanner      UNMODIFIED
BoardResolver          UNMODIFIED
CascadeResolver        UNMODIFIED
SwapValidator          UNMODIFIED
SwapExecutor           UNMODIFIED
PlayerState            UNMODIFIED
```

Verified by modification time: no file under `src/backend/GameServer.Domain/`
was touched by this task. No minimal compatibility change was needed — the
projection reads the existing public surface only.

---

## Documentation

UNCHANGED. `SIGNALR_PROTOCOL.md` §3.2 already owned the exact wire schema
(`TASK-007A3`), and `GAME_EVENTS.md` §3 item 1 already delegates to it. The
implementation matched the contract; the one place it initially did not
(omission vs `null`) was a code defect against §3.2.5, corrected in code rather
than by editing the document (`AGENTS.md` §17).

No contradiction between the contract and code was found, so no STOP condition
fired. No ADR was created, consistent with `TASK-007A`/`TASK-007A3` and
`docs/03-decisions/README.md` §2: this task adds no architecture, no new
message, no state-model change, and no module boundary — it fills in the
documented `BattleHub → ReceiveEvents` arrow.

---

## Frontend

UNCHANGED — zero files under `src/frontend/**` were modified. The existing
`GameRuntime.readBattleEventsEnvelope` shape check (`battleId` string,
`serverSequence` number, `events` array) continues to receive the same
three-member envelope, and `events` remains `readonly unknown[]`. No event
parsing, event type, union, or gameplay interpretation was added to the client.
`RuntimeBoundaries.test.ts`'s no-event-names rule and `GameRuntime.test.ts`'s
forwarding contracts pass unmodified.

---

## Scope Compliance

```text
GAME_RULES.md                 UNCHANGED
Gameplay semantics            UNCHANGED
Event names                   UNCHANGED (4)
Event ordering                UNCHANGED
Match counting                UNCHANGED
Combo counting                UNCHANGED
Cascade semantics             UNCHANGED
Special Gem semantics         UNCHANGED
RNG                           UNCHANGED
Turn                          UNCHANGED
Sequence                      UNCHANGED
Frontend                      UNCHANGED
BattleEvent direct serialize  ELIMINATED
Throwing accessors            INTACT (all four retained)
New gameplay events           NONE
Second SignalR message        NONE
Redis / database / auth work  NONE
Unrelated gameplay            NONE
Second swap pipeline          NONE
```

Files changed: **3** — one new production file, one modified production file,
one modified test file.

---

## Files Changed

```text
+ src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs
~ src/backend/GameServer.Api/Hubs/BattleHub.cs
~ tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs
```

---

## Out of Scope

- Redocumenting the wire schema — already finalized by `TASK-007A3`.
- `MatchResolved` and the action-boundary events (`TurnStarted`, `TurnEnded`,
  `SwapStarted`, `SwapResolved`) remain unemitted (`TASK-006` risks, unchanged).
- Client-side event interpretation — deliberately not implemented anywhere
  (`ARCHITECTURE.md` §2.2.1 rule 4, `GAME_EVENTS.md` §3 item 3).
- Redis/PostgreSQL persistence, authentication, and battle-creation endpoints.

---

## Risks / Follow-up

```text
1. The batch still carries only the four events TASK-006 emits. When
   MatchResolved or the action-boundary events are added, §3.2.2's closed
   discriminator set grows and this projection must gain the matching case
   (it throws rather than silently dropping an unknown kind, so the gap would
   surface immediately rather than corrupt the batch).
   Impact:  none today; forward-compatibility note only.
   Follow-up: extend with the event that defines it.

2. BattleStateService's doc comments still say ReceiveEvents "remains
   unimplemented" (a pre-TASK-007 statement made when the send did not exist).
   It is a comment with no behavioral effect, in a file this task did not need
   to touch.
   Impact:  comment accuracy only.
   Suggested follow-up: correct the comment wording in a documentation-only
   change.

3. The wire DTOs are public because the public ReceiveEventsPayload record
   exposes them as a constructor parameter (a less-accessible type is a
   compile error). They are transport records, not Domain types, and Domain
   takes no dependency on them.
   Impact:  none.
   Follow-up: none.
```

---

## Final Report

```text
TASK-007 — DONE

Implementation:
BattleEvent stays a Domain type and is never serialized. A transport projection
was added at the Api/hub boundary (GameServer.Api.Hubs) — the layer
ARCHITECTURE.md §2.1 item 4 makes responsible for translating wire messages —
so JSON concerns never entered Domain. ReceiveEventsPayload.Events changed from
IReadOnlyList<BattleEvent> to IReadOnlyList<BattleEventWireDto>, and
ToPayload(SwapExecutionResult) projects the executor's own list one-to-one.
The envelope kept exactly its three members, the SendAsync site and its
post-write-back position are unchanged, and BattleStateUpdated is untouched.

Transport projection:
BattleEventWireProjection — a pure, side-effect-free field mapping that reads
the discriminator first and then only that event's own payload, so no throwing
accessor is ever touched. One DTO with four one-of slots models the four flat
documented objects; construction normalizes empty createdSpecialGems to
omission and rejects an undocumented kind rather than dropping it, preserving
the N-to-N count and order. MatchShape geometry (arms, arm lengths, StartIndex,
IntersectionIndex, Step, SortKey) is not exposed; only CellIndex and SpecialGem
of each SpecialGemClaim are projected. The two documented cascadeDepth values
are kept apart: MatchCreated takes the pass depth, CascadeCreated takes the
depth index within the Swap.

ReceiveEvents:
Server → Client only, one atomic batch per accepted/resolved swap, sent after
the single authoritative write-back, carrying battleId, serverSequence read from
the committed BattleState.Sequence, and the projected ordered events. Rejected
swaps and unknown battles send nothing.

Serialization:
The batch crosses the real SignalR/JSON boundary. BattleEvent is replaced by
wire DTOs before SendAsync, and every member is explicitly named camelCase with
a per-member null-omission condition, so the schema does not depend on a
serializer default (§3.2.3 item 2). A defect found by the new tests — valid JSON
that wrote explicit nulls instead of omitting inapplicable members, violating
§3.2.5 — was corrected in code.

Tests:
Focused:  18/18
Backend:  623/623
Frontend: 174/174
Build: PASS (0 warnings / 0 errors; frontend tsc + vite build clean)

Files changed:
+ src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs
~ src/backend/GameServer.Api/Hubs/BattleHub.cs
~ tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs

Docs:
UNCHANGED (wire contract already finalized by TASK-007A3)

Frontend:
UNCHANGED

Domain:
UNCHANGED

Out of scope:
Redocumenting the wire schema; implementing the unemitted events
(MatchResolved, action-boundary events); client-side event interpretation;
Redis/PostgreSQL/auth work.

Risks / follow-up:
The closed discriminator set grows when MatchResolved and the action-boundary
events are emitted — the projection throws on an unknown kind so that gap
surfaces immediately. Two stale code comments note ReceiveEvents as
unimplemented and could be corrected in a documentation-only pass.
```

---

## Status

DONE