# TASK-120 — Implement Client Card and Skill Cast Action Paths (CardCast / PetSkillCast)

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and src/ by path and section; copies only what an
  implementer needs to know where to look.

  PROVENANCE: Sequenced Phase-1 implementation task following completed
  server-authoritative action paths: TASK-107 (server CardCast for Basic Cards)
  and TASK-115 (server PetSkillCast, Crit, Burn).
  Both hub methods (CardCast, PetSkillCast) and event projections are live on the
  backend, but the client runtime and BattleScene currently only implement Swap
  (TASK-069), leaving all Card and Skill casting inoperable on the client.
-->

---

## Metadata

```text
Task ID:           TASK-120
Type:              FEATURE
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     client
Supporting Agents: realtime, testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery,
                   client/client-state-authority,
                   client/phaser-battle-presentation,
                   client/client-event-projection,
                   realtime/realtime-protocol-validation,
                   testing/test-scenario-generation,
                   quality/scope-validation
                   (7 skills — Complex budget, tasks/README.md §12 hard limit)
Dependencies:      TASK-069 (DONE — implemented client Swap action path; established
                     SignalRService transport, GameRuntime.requestAction, and BattleScene input pattern),
                   TASK-088 (DONE — implemented in-battle event presentation in BattleEventPresenter),
                   TASK-107 (DONE — implemented server Basic CardCast path, BattleHub.CardCast, and wire event),
                   TASK-115 (DONE — implemented server PetSkillCast path, BattleHub.PetSkillCast, and wire event)
Blocks:            None
```

---

## Objective

Implement the documented client → server action paths and event presentation for CardCast and PetSkillCast so that a player in BattleScene can cast equipped Cards and the active Pet's Signature Skill against the server-authoritative battle: expose `cardCast` and `petSkillCast` on `SignalRService` matching `SIGNALR_PROTOCOL.md` §2/§5, extend `GameRuntimePort` / `GameRuntime.requestAction` to route CardCast and PetSkillCast requests, format `CardCast` and `PetSkillCast` events in `BattleEventPresenter` (`SIGNALR_PROTOCOL.md` §3.2.20–§3.2.22), and add interactive casting controls with transport acknowledgement feedback in `BattleScene`.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — 3 Basic Cards (Heal, Shield, Power Charge), Pet Signature Skill, server-authoritative battle resolution, realtime communication (SignalR), and client presentation are IN scope
- `docs/00-overview/ROADMAP.md` §1 — Phase 1 Core Loop Vertical Slice (one playable battle start to finish against Bosses; 3 Basic Cards; one Pet fully implemented; server-authoritative resolution over SignalR)
- `docs/01-game-design/CARD_RULES.md` §1–§4 — Basic Cards, Pet Skill Cards, Power Costs, casting validation and rejection
- `docs/01-game-design/GAME_RULES.md` §11 (Cards), §12 (Power range), §18 (Server-authoritative rule: client only requests actions)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §2 (`CardCast` and `PetSkillCast` client → server hub methods, parameters, correlation id `clientSequence`), §3.2.20 (`CardCast` event wire payload), §3.2.21 (`PetSkillCast` event wire payload), §3.2.22 (emission ordering: `CardCast` first, then `PetSkillCast`), §4 (authoritative state push), §5 (request/response acknowledgement shape: `{ accepted: boolean, reason?: string | null }`)
- `docs/02-technical/GAME_EVENTS.md` §2 — `CardCast`, `PetSkillCast` event definitions and payloads
- `docs/02-technical/GAME_STATE.md` §2.3 — `PetState.EquippedCards[]`, `PetState.Power`, `PetState.HP`
- `docs/02-technical/ARCHITECTURE.md` §2.2 — Client layering: Phaser scenes depend on `GameRuntimePort`, never directly on transport (`SignalRService`) or HTTP (`ApiService`); capabilities, not models
- `docs/03-decisions/ADR/ADR-001-server-authoritative-architecture.md` — Server authority; client requests actions, server validates and resolves
- `docs/03-decisions/ADR/ADR-003-phaser-battle-presentation.md` — Phaser scene lifecycle and battle presentation
- `docs/03-decisions/ADR/ADR-004-realtime-signalr-transport.md` — SignalR realtime transport

---

## Scope

### In Scope
- **SignalR transport methods** (`SignalRService.ts`):
  - Expose `cardCast(battleId: string, cardId: string, clientSequence?: string): Promise<CardCastAcknowledgement>` invoking hub method `CardCast` (`SIGNALR_PROTOCOL.md` §2, §5).
  - Expose `petSkillCast(battleId: string, clientSequence?: string): Promise<PetSkillCastAcknowledgement>` invoking hub method `PetSkillCast` (`SIGNALR_PROTOCOL.md` §2, §5).
  - Return typed acknowledgement shapes conforming to `SIGNALR_PROTOCOL.md` §5 (`{ accepted: boolean, reason?: string | null }`).
- **Runtime port and action routing** (`GameRuntimeEvents.ts`, `GameRuntime.ts`):
  - Define `RuntimeCardCastActionRequest { kind: 'CardCast', cardId: string }`.
  - Define `RuntimePetSkillCastActionRequest { kind: 'PetSkillCast' }`.
  - Extend `RuntimeActionRequest` discriminated union to include `RuntimeCardCastActionRequest` and `RuntimePetSkillCastActionRequest`.
  - Implement `CardCast` and `PetSkillCast` handling in `GameRuntime.requestAction`, passing the runtime's synchronized `battleId` and generating opaque correlation sequences via `nextClientSequence()`.
  - Preserve `RuntimeActionNotImplementedError` for unhandled action kinds and `GetBattleState` (`SIGNALR_PROTOCOL.md` §7).
- **Event presentation** (`BattleEventPresenter.ts`):
  - Add formatting for `CardCast` (`SIGNALR_PROTOCOL.md` §3.2.20) and `PetSkillCast` (`SIGNALR_PROTOCOL.md` §3.2.21) events.
  - Extend the non-outcome closed wire event discriminator from 10 to 12 supported event types.
- **BattleScene presentation and input** (`BattleScene.ts`):
  - Render interactive cast triggers for the active Pet's equipped cards (`state.petState.equippedCards`) and Signature Skill using authoritative state.
  - Submit cast actions exclusively through `GameRuntimePort.requestAction`.
  - Lock cast input while any action request (swap or cast) is in flight to prevent concurrent submissions.
  - Display transport acknowledgement feedback (in flight, accepted, or rejected with server reason code).
  - Render event presentation readout for received `CardCast` and `PetSkillCast` events.
- **Automated test coverage**:
  - Update `RuntimeBoundaries.test.ts` to assert `'CardCast'` and `'PetSkillCast'` are now permitted transport invocations while `'GetBattleState'` remains unimplemented.
  - Unit tests in `SignalRService.test.ts` for `cardCast` and `petSkillCast`.
  - Unit tests in `GameRuntime.test.ts` for `requestAction(CardCast)` and `requestAction(PetSkillCast)`.
  - Presentation tests in `BattleEventPresentation.test.ts` for `CardCast` and `PetSkillCast`.
  - Scene input tests in `BattleScene.test.ts` / `SceneLifecycle.test.ts`.

### Out of Scope
- Server-side modifications: zero files in `src/backend/` or `tests/backend/` (CardCast and PetSkillCast server paths are fully implemented and verified).
- Documentation changes: zero files in `docs/` or `docs/03-decisions/ADR/` (all contracts and schemas are authoritative).
- Client-side gameplay logic: zero client-side calculation of damage, power subtraction, HP modification, cooldown decrement, or validation (server remains authoritative per ADR-001).
- Reconnect/resync action path (`GetBattleState` — Phase 3, `SIGNALR_PROTOCOL.md` §7).
- Rich card hand art, spine animations, or drag-and-drop physics (Phaser interactive controls conform to current project scene style).
- Multiple ATK modifier composition (TASK-118 note; deferred because unreachable).
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State

The backend fully implements server-authoritative `BattleHub.CardCast` and `BattleHub.PetSkillCast` (`TASK-107`, `TASK-115`), along with `CardCast` and `PetSkillCast` wire event projections. On the frontend, `SignalRService` implements only `JoinBattle`, `Ping`, and `Swap` (`RuntimeBoundaries.test.ts:337`), `GameRuntime.requestAction` throws `RuntimeActionNotImplementedError` for any action other than `Swap` (`GameRuntime.ts:363`), `BattleEventPresenter` only handles 10 event types up to `BossSkillCast`, and `BattleScene` offers no UI or input for casting Cards or Pet Skills.

---

## Acceptance Criteria

- [x] `SignalRService` exposes `cardCast(battleId, cardId, clientSequence)` invoking hub method `CardCast` and returning `{ accepted, reason }` (`SIGNALR_PROTOCOL.md` §2, §5).
- [x] `SignalRService` exposes `petSkillCast(battleId, clientSequence)` invoking hub method `PetSkillCast` and returning `{ accepted, reason }` (`SIGNALR_PROTOCOL.md` §2, §5).
- [x] `GameRuntimeEvents.ts` defines `RuntimeCardCastActionRequest` (`kind: 'CardCast'`, `cardId: string`) and `RuntimePetSkillCastActionRequest` (`kind: 'PetSkillCast'`), including both in `RuntimeActionRequest`.
- [x] `GameRuntime.requestAction` routes `CardCast` requests to `SignalRService.cardCast` with the runtime's synchronized `battleId` and an opaque client sequence.
- [x] `GameRuntime.requestAction` routes `PetSkillCast` requests to `SignalRService.petSkillCast` with the runtime's synchronized `battleId` and an opaque client sequence.
- [x] `GameRuntime.requestAction` continues to reject unknown action kinds and `GetBattleState` with `RuntimeActionNotImplementedError`.
- [x] `GameRuntime.requestAction` throws an error when no battle state is known (`battleState === null`) before submitting a cast.
- [x] `BattleEventPresenter` formats `CardCast` (`SIGNALR_PROTOCOL.md` §3.2.20) and `PetSkillCast` (`SIGNALR_PROTOCOL.md` §3.2.21) events verbatim without modifying event data.
- [x] `BattleScene` renders interactive cast triggers for equipped cards (`state.petState.equippedCards`) and the active Pet's Signature Skill from authoritative `PetState`.
- [x] Tapping an equipped card or signature skill in `BattleScene` submits the corresponding `CardCast` or `PetSkillCast` request via `GameRuntimePort.requestAction`.
- [x] Cast input in `BattleScene` is locked while any action request (swap or cast) is in flight to prevent concurrent submissions.
- [x] `BattleScene` presents transport feedback for cast requests (in flight, accepted, or rejected with server reason code).
- [x] `RuntimeBoundaries.test.ts` is updated to assert `'CardCast'` and `'PetSkillCast'` are permitted transport invocations, while `'GetBattleState'` remains unimplemented.
- [x] All existing and new client tests pass (`npm test` in `src/frontend/client`).
- [x] Quality review checklist passes (`quality/review.md` §1).
- [x] Server authority is preserved with zero client-side calculation of gameplay state (`GAME_RULES.md` §18, ADR-001).

---

## Affected Files & Areas

```text
[ ] src/backend/ (None — server path is complete)
[x] src/frontend/client/src/services/realtime/SignalRService.ts
[x] src/frontend/client/src/game/runtime/GameRuntimeEvents.ts
[x] src/frontend/client/src/game/runtime/GameRuntime.ts
[x] src/frontend/client/src/game/scenes/BattleEventPresenter.ts
[x] src/frontend/client/src/game/scenes/BattleScene.ts
[x] src/frontend/client/tests/SignalRService.test.ts
[x] src/frontend/client/tests/GameRuntime.test.ts
[x] src/frontend/client/tests/RuntimeBoundaries.test.ts
[x] src/frontend/client/tests/BattleEventPresentation.test.ts
[x] src/frontend/client/tests/SceneLifecycle.test.ts
[ ] docs/ (None — contracts are authoritative)
```

---

## Implementation Notes

- **Transport boundary (`SignalRService.ts`):** Follow the exact pattern established by `swap(...)`. Hub method names must match `SIGNALR_PROTOCOL.md` §2 (`'CardCast'`, `'PetSkillCast'`). Arguments: `(battleId, cardId, clientSequence)` for CardCast, `(battleId, clientSequence)` for PetSkillCast.
- **Runtime Port (`GameRuntimeEvents.ts`):** Define constants `RUNTIME_ACTION_CARD_CAST = 'CardCast'` and `RUNTIME_ACTION_PET_SKILL_CAST = 'PetSkillCast'`. Define request interfaces and expand `RuntimeActionRequest`.
- **Runtime Coordinator (`GameRuntime.ts`):** In `requestAction`, switch or branch on `action.kind`. Verify `battleState !== null` before dispatching. Do not validate Power or Card ownership on the client — let the server validate and return the §5 acknowledgement (`CARD_RULES.md` §3).
- **Event Presenter (`BattleEventPresenter.ts`):** Add `PresentedCardCast` (`cardId`) and `PresentedPetSkillCast` (`cardId`), and format them cleanly in `formatEvent` and `describeEvent`.
- **Scene Input & Presentation (`BattleScene.ts`):** Render cast controls within the existing layout geometry (e.g. below or adjacent to the board in `SAFE_AREA`). Use `setInteractive({ useHandCursor: true })` on controls. Share the `isActionInFlight` lock so the player cannot submit a swap while a cast is in flight or vice versa. Render acknowledgement reason on rejection (e.g. `INSUFFICIENT_POWER`, `CARD_NOT_IN_LOADOUT`).
- **Boundary Test (`RuntimeBoundaries.test.ts`):** Update the assertion at lines 336–350 to expect `['CardCast', 'JoinBattle', 'PetSkillCast', 'Ping', 'Swap']`, while ensuring `'GetBattleState'` remains absent.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — SignalRService cardCast & petSkillCast method invocations and ack parsing
[x] Integration tests  — GameRuntime requestAction dispatching, error guards, clientSequence propagation
[x] Boundary tests     — RuntimeBoundaries asserting permitted SignalR method invocations and port boundaries
[x] Presentation tests — BattleEventPresenter CardCast & PetSkillCast formatting
[x] Scene tests        — BattleScene cast input, in-flight action locking, and ack feedback display
```

### Key Edge Cases
- See `SIGNALR_PROTOCOL.md` §2 item 3 & §5 item 2: Rejected casts return `{ accepted: false, reason }` and mutate zero state; client must display rejection without desynchronizing.
- See `SIGNALR_PROTOCOL.md` §2 item 1: `clientSequence` is an opaque request correlation id, never server sequence.
- See `SIGNALR_PROTOCOL.md` §3.2.22: For Pet Skill Card cast, both `CardCast` and `PetSkillCast` arrive in the event batch.
- Action in-flight guard: Rapid taps while a cast is pending must not dispatch duplicate requests.

---

## Stop Conditions

- If client-authoritative gameplay logic (e.g. Power deduction, cooldown simulation, damage calculation) is requested: STOP per `AGENTS.md` §10.
- If server API or SignalR protocol contract requires modification: STOP per `AGENTS.md` §20.
- If task exceeds 7 skills or crosses backend boundaries: STOP and decompose per `tasks/README.md` §13.

---

## Completion Evidence

### Changed Files
- `src/frontend/client/src/services/realtime/SignalRService.ts` — Added `CardCastAcknowledgement`, `PetSkillCastAcknowledgement`, `equippedCards` in `PetStatePayload`, and `cardCast` & `petSkillCast` methods invoking SignalR hub methods.
- `src/frontend/client/src/game/runtime/GameRuntimeEvents.ts` — Added `RUNTIME_ACTION_CARD_CAST`, `RUNTIME_ACTION_PET_SKILL_CAST`, `RuntimeCardCastActionRequest`, `RuntimePetSkillCastActionRequest`, expanded `RuntimeActionRequest`, and added `equippedCards` in `RuntimePetState`.
- `src/frontend/client/src/game/runtime/GameRuntime.ts` — Implemented routing for `CardCast` and `PetSkillCast` in `requestAction`, opaque sequence generation with action prefixes, and validated `equippedCards` 4-entry string array in `readPetState`.
- `src/frontend/client/src/game/scenes/BattleEventPresenter.ts` — Added `PresentedCardCast`, `PresentedPetSkillCast`, parsing and formatting for `CardCast` and `PetSkillCast` events, extending the non-outcome closed wire discriminator to 12 event types.
- `src/frontend/client/src/game/scenes/BattleScene.ts` — Rendered interactive cast triggers from `equippedCards` and `GameRuntimePort.getCards()`, implemented `submitCardCast`, `submitPetSkillCast`, unified action locking (`isActionInFlight` / `isInputLocked`), and added transport feedback readout.
- `src/frontend/client/tests/SignalRService.test.ts` — Added unit tests for `cardCast` and `petSkillCast` transport invocations, argument matching, acknowledgement return, rejection propagation, connection error handling, and `equippedCards` payload presence.
- `src/frontend/client/tests/GameRuntime.test.ts` — Added unit tests for `requestAction` with `CardCast` and `PetSkillCast`, client sequence correlation, state preservation on rejection, `equippedCards` payload extraction and validation.
- `src/frontend/client/tests/RuntimeBoundaries.test.ts` — Updated hub invocation whitelist to `['CardCast', 'JoinBattle', 'PetSkillCast', 'Ping', 'Swap']`, verifying `GetBattleState` remains unimplemented.
- `src/frontend/client/tests/BattleEventPresentation.test.ts` — Added tests for parsing, formatting, malformed event rejection, and in-order presentation of `CardCast` and `PetSkillCast` events.
- `src/frontend/client/tests/SceneLifecycle.test.ts` — Updated harness and boundary tests to verify cast controls rendering, `CardCast` and `PetSkillCast` action dispatching, transport feedback presentation, and action locking across concurrent taps.

### Validation Results
- `npm test -- --run` in `src/frontend/client` — PASS (18 test files, 477 tests passed)
- `npm run build` (`tsc && vite build`) in `src/frontend/client` — PASS (clean build, 0 errors)
- `dotnet test src/backend/GameServer.sln` — PASS (4 test projects, 2,296 tests passed)

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed all action validations are performed by the server
