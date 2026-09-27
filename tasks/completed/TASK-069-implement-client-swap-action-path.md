# TASK-069 — Implement the Client Swap Action Path

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and src/ by path and section; copies only what an
  implementer needs to know where to look.

  The Swap contract is already documented and already implemented on the
  server (BattleHub.Swap). This task builds the missing CLIENT half of that
  contract: transport method, runtime request boundary, and scene input.

  It does NOT change any game rule, API, event, Redis, or SignalR contract,
  and it does NOT touch the backend.
-->

---

## Metadata

```text
Task ID:           TASK-069
Type:              FEATURE (the contract exists in docs/; it has no client
                   implementation — tasks/TASK_TYPES.md §2 FEATURE)
Status:            DONE
Risk:              MEDIUM (client-only change; the risk is that three
                   foundation-stage guard tests assert the ABSENCE of exactly
                   what this task adds, so each test change must be a cited
                   stage advance, never a weakened property — §6, §10)
Priority:          HIGH (ROADMAP.md §1 Phase 1 "One playable battle, start to
                   finish"; Swap is the only gameplay action whose server half
                   exists, and no client code can invoke it today)
Primary Agent:     client
Supporting Agents: realtime (protocol conformance of the §2.1 request and §5
                   acknowledgement), testing (authority + guard-test coverage),
                   review (scope / no-client-authority verification)
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery,
                   client/client-state-authority,
                   client/phaser-match3,
                   realtime/realtime-protocol-validation,
                   testing/test-scenario-generation,
                   quality/implementation-review
Dependencies:      None (BattleHub.Swap, JoinBattle, BattleStateUpdated and
                   ReceiveEvents are all implemented — TASK-003/004, TASK-030,
                   TASK-031)
```

---

## Objective

Implement the documented client → server Swap action path so a player's board
selection reaches the authoritative server and the client renders the result:
expose `Swap` on `SignalRService` exactly as `SIGNALR_PROTOCOL.md` §2.1/§5
defines it, replace `GameRuntime.requestAction`'s deliberate
`RuntimeActionNotImplementedError` placeholder with a working swap request
(transport only, zero gameplay logic), and give `BattleScene` a two-cell
selection input that submits that request through the runtime port and reacts
to the acknowledgement (accepted → render the next authoritative push;
rejected → change nothing).

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — server-authoritative battle / client
  presentation is IN scope; §2 for exclusions
- `docs/00-overview/ROADMAP.md` §1 — Phase 1 goal (one playable battle,
  server-authoritative resolution over SignalR)
- `docs/01-game-design/MATCH3_RULES.md` §1.0 (cell index), §2.1.1 (pair
  semantics), §2.1.2 (validation order), §2.1.5 (rejection semantics — board,
  Turn, Sequence, RngState unchanged, no Battle Event)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §2 (hub method list + `clientSequence`
  meaning), §2.1 (Swap parameters; adjacency feedback is feedback only),
  §5 (acknowledgement shape and its non-authoritative meaning), §3.1 (delivery
  order and "rejected actions deliver nothing"), §4 (state push), §6 (ordering
  — no client prediction or rollback), §8.3 (no battle Status/lifecycle value)
- `docs/02-technical/ARCHITECTURE.md` §2.2 (client layering; scenes depend on
  the runtime port, never on SignalR/transport types)
- `docs/02-technical/GAME_STATE.md` §5.1 (`Sequence` is server-owned)
- `docs/01-game-design/GAME_RULES.md` §18 (server authority; client sends only
  a request)
- `docs/03-decisions/ADR/ADR-001`, `ADR-003`, `ADR-004` — authority model,
  Phaser presentation, SignalR transport

---

## Scope

### In Scope
- `SignalRService.swap(battleId, fromCell, toCell, clientSequence)` invoking the
  hub method `Swap` with exactly those four arguments and returning the §5
  acknowledgement (`accepted`, optional machine-readable `reason`) — no
  interpretation, no defaulting, no state mutation in the service.
- `GameRuntime.requestAction` implementing the **swap** action: it takes the
  cells from the caller, sources `battleId` from the runtime's received battle
  state, generates an opaque per-request `clientSequence`, submits through the
  existing `SignalRService`, and resolves with the acknowledgement.
- `BattleScene` two-tap cell selection on the drawn 8×8 board using the
  scene's existing index → screen mapping, submission through the runtime port
  only, and acknowledgement-driven presentation (selection feedback +
  rejection feedback).
- Client tests for transport invocation, runtime request boundary, scene
  selection → request flow, rejection-with-no-mutation, and authority.
- Cited updates to the three foundation-stage guard tests listed in §6.

### Out of Scope
- **Battle creation and joining orchestration**: no client call to
  `POST /api/battle/start`, no loadout UI, no automatic `JoinBattle`. Those
  depend on the unimplemented collection endpoints
  (`API_CONTRACTS.md` §5 `GET /api/pets|/api/cards|/api/relics`) and belong to
  a separate task. `SignalRService.joinBattle` already exists and is not this
  task's subject.
- **`CardCast`, `PetSkillCast`, `GetBattleState`** — the server does not
  implement them (`tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs`,
  `BattleHub_ShouldNotRegisterGameplayMethods`). They must stay unimplemented
  and must stay absent from `SignalRService.ts`.
- **Reconnect/resync** (`SIGNALR_PROTOCOL.md` §7 — Phase 3).
- **Any backend or documentation change.** If a contract gap is found that
  docs do not answer, STOP (§10) rather than extending the contract here.
- **Resolution presentation**: match, cascade, combo, damage, boss readouts;
  animations; prediction; optimistic board mutation; local re-derivation of any
  board, match, combo, or `Sequence` value.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State

Server: `src/backend/GameServer.Api/Hubs/BattleHub.cs` implements
`Swap(string battleId, int fromCell, int toCell, string? clientSequence)` and
returns `SwapResponse(Accepted, Reason)`; an accepted swap pushes
`BattleStateUpdated` and then `ReceiveEvents`, a rejected one returns only the
ack (`SIGNALR_PROTOCOL.md` §3.1, §5). No backend change is required.

Client: `SignalRService.ts` implements only `JoinBattle` and `Ping`
(`SignalRService.ts:434`, `:442`) and its header comment still lists `Swap` as
not implemented. `GameRuntime.requestAction`
(`src/frontend/client/src/game/runtime/GameRuntime.ts:315`) throws
`RuntimeActionNotImplementedError` for every action.
`BattleScene.ts` renders the pushed board and registers no input at all (its
class comment states there is no swap interaction).

---

## Acceptance Criteria

- [ ] `SignalRService` exposes `swap(...)` that invokes `'Swap'` with
      `(battleId, fromCell, toCell, clientSequence)` and returns the §5
      acknowledgement; the only invokable client → server methods in that file
      are `JoinBattle`, `Ping`, and `Swap`.
- [ ] The `Swap` request carries exactly four arguments — no Gem type, match,
      Combo, Turn, or `Sequence` value (§2.1 item 4).
- [ ] `clientSequence` is generated client-side, is opaque, and is never
      compared with, derived from, or required to equal `BattleState.Sequence`
      (§2 item 1; `GAME_STATE.md` §5).
- [ ] `GameRuntime.requestAction` resolves with the acknowledgement for the
      swap action and still rejects with `RuntimeActionNotImplementedError`
      for every other action kind (no `CardCast`/`PetSkillCast`/`GetBattleState`
      path exists in client code).
- [ ] With no known battle, or with no established connection, no request is
      sent and the call fails as a transport/runtime error (presentation
      state, not a game state — `SIGNALR_PROTOCOL.md` §8.3).
- [ ] `BattleScene` submits exclusively through the runtime port: no `SignalR`,
      `HubConnection`, or `SignalRService` import in scene files
      (`ARCHITECTURE.md` §2.2 rule 3 — existing boundary test still passes).
- [ ] On `accepted: true`, the rendered board/turn/sequence change only from
      the subsequent `BattleStateUpdated` push; the scene and runtime compute
      no board, match, cascade, combo, or `Sequence` value.
- [ ] On `accepted: false`, nothing is mutated locally, nothing is retried
      automatically, and the machine-readable `reason` is surfaced as
      presentation only (`MATCH3_RULES.md` §2.1.5; §5 item 2).
- [ ] Local selection feedback (highlight, optional adjacency pre-filter) is
      feedback only and never alters the request shape or substitutes for the
      server's validation (`SIGNALR_PROTOCOL.md` §2.1 item 1).
- [ ] All three guard-test updates in §6 are present, each justified by a
      citation to the stage it advances, with every still-valid property
      preserved (`AGENTS.md` §15).
- [ ] All client test suites pass (`src/frontend/client`), and the backend
      suite still passes (no backend file was touched).
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[ ] src/frontend/client/src/services/realtime/SignalRService.ts  (add swap(), update header comment)
[ ] src/frontend/client/src/game/runtime/GameRuntime.ts          (implement requestAction for swap)
[ ] src/frontend/client/src/game/runtime/GameRuntimeEvents.ts    (request/acknowledge types)
[ ] src/frontend/client/src/game/scenes/BattleScene.ts           (two-cell selection + ack presentation)
[ ] src/frontend/client/tests/SignalRService.test.ts
[ ] src/frontend/client/tests/GameRuntime.test.ts
[ ] src/frontend/client/tests/SceneLifecycle.test.ts
[ ] src/frontend/client/tests/RuntimeBoundaries.test.ts
[ ] src/backend/  — NOT TOUCHED
[ ] docs/         — NOT TOUCHED (contracts are already written)
```

---

## Implementation Notes

- **Transport signature must match the server verbatim.** The hub method is
  `Swap(battleId, fromCell, toCell, clientSequence = null)` in
  `BattleHub.cs`; cell indices are `int` `0..63`
  (`MATCH3_RULES.md` §1.0). Follow the existing `joinBattle`/`ping` pattern in
  `SignalRService.ts:434-448` (same connection guard, same direct `invoke`).
- **Boundary placeholder to replace.** `GameRuntime.ts:307-317` documents
  `requestAction` as the client → server request boundary whose sent requests
  "would be `Swap`/`CardCast`/`PetSkillCast`". Implement the swap action;
  leave the other kinds throwing `RuntimeActionNotImplementedError`, and keep
  `RuntimeActionRequest` from growing invented members.
- **battleId source.** Take it from the battle state the runtime already holds
  (`GameRuntime.getBattleState()`), i.e. the client must already have received
  a `BattleStateUpdated` push. Do not invent a battle id, do not accept one
  from the scene beyond a value the runtime itself delivered.
- **Scene input.** Register pointer input on the existing board layer using
  the documented index → screen mapping already used by `renderBoard`
  (`BattleScene.ts`). The scene keeps presentation-local state (selected cell,
  pending request) — do not add a battle `Status`/lifecycle value
  (`SIGNALR_PROTOCOL.md` §8.3, `GAME_STATE.md` §2.0.3).
- **In-flight handling** (ignoring further taps while a request is outstanding,
  visual feedback) is presentation freedom; server ordering and non-prediction
  remain governed by `SIGNALR_PROTOCOL.md` §6 items 2–5.
- **No interpretation of events.** `ReceiveEvents` batches continue to be
  forwarded unchanged; this task adds no event handling.

---

## Testing Requirements

### Required Verification
```text
[ ] Unit tests         — SignalRService.swap argument order/types + ack return;
                         GameRuntime.requestAction (swap accepted, swap
                         rejected, no-battle error, unimplemented kinds still
                         throw, no local mutation on rejection)
[ ] Integration tests  — scene selection → runtime request → transport invoke
                         (mocked transport, as the existing client suites do);
                         runtime port boundary (no transport types in scenes)
[ ] Gameplay scenarios — Given a joined battle and a player-selected adjacent
                         pair (MATCH3_RULES.md §2.1.1)
                         When the client sends Swap (SIGNALR_PROTOCOL.md §2.1)
                         Then the server validates it (§2.1.2)
                         And an accepted swap is re-rendered only from the next
                         BattleStateUpdated push (§3.1, §4)
                         Given a non-adjacent or stale pair
                         When the swap is rejected (§2.1.5)
                         Then the ack carries accepted:false + reason (§5)
                         And no local board change and no event occur.
```

### Key Edge Cases
- `docs/01-game-design/MATCH3_RULES.md` §2.1.2 (validation order),
  §2.1.4 (staleness/idempotency — client never tracks a server number),
  §2.1.5 (rejection leaves board, Turn, Sequence, RngState unchanged, no event)
- No connection / connection lost while a request is in flight
- Taps outside the board, second tap on the same cell, second tap on a
  non-adjacent cell (local feedback only)
- `accepted: false` with `reason: "BATTLE_NOT_FOUND"` (unknown battle —
  `BattleHub.cs` return path)

---

## Guard-Test Changes (required, and how they must be written)

Three tests assert the FOUNDATION stage — that these things do not exist yet.
This task is the stage advance those comments anticipate. Each change must
preserve every property that still holds (`AGENTS.md` §15):

1. `src/frontend/client/tests/RuntimeBoundaries.test.ts` —
   `the client exposes no gameplay Hub methods` (`:233-244`). Rewrite so it
   asserts the new boundary precisely: `'Swap'` **is** invoked;
   `'CardCast'`, `'PetSkillCast'`, `'GetBattleState'` are **still** absent
   (server does not implement them — cite `BattleHub_ShouldNotRegisterGameplayMethods`);
   `'JoinBattle'` is still present. Do not delete the file's other assertions.
2. `src/frontend/client/tests/SceneLifecycle.test.ts` —
   `contains no gameplay interaction or resolution presentation` (`:386-395`).
   Keep forbidding resolution-presentation readouts (match, cascade, combo,
   damage, boss). Swap *interaction* is now implemented, so the rendered-text
   check must no longer treat a swap-request/rejection status line as a
   violation — narrow the forbidden set explicitly and cite
   `SIGNALR_PROTOCOL.md` §2.1/§5 for why the request and its acknowledgement
   are transport feedback, not resolution presentation.
3. `src/frontend/client/tests/GameRuntime.test.ts` —
   `exposes the boundary without implementing gameplay actions` (`:914-920`).
   Replace with: the swap action resolves with the acknowledgement; every
   other action kind still rejects with `RuntimeActionNotImplementedError`.

If any of these cannot be updated without weakening a property that still
holds: STOP (§10) and report rather than editing the test.

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References:
  STOP per `AGENTS.md` §7
- If the task would require client-authoritative game logic calculation
  (computing matches, validating swaps as authority, mutating the board on
  acceptance, predicting `Sequence`): STOP per `AGENTS.md` §10
- If a contract gap is found (e.g. an acknowledgement field or rejection code
  not owned by `SIGNALR_PROTOCOL.md` §5 item 3 / the domain rule documents):
  STOP per `AGENTS.md` §20 rather than extending the contract in this task
- If the work starts requiring `POST /api/battle/start`, loadout selection, or
  `JoinBattle` orchestration: STOP — those are the separate, currently blocked
  battle-start task (§2 Out of Scope)
- If task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: STOP & decompose

---

## Completion Evidence

<!-- TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE. -->

### Changed Files

**Client source**
- `src/frontend/client/src/services/realtime/SignalRService.ts` — added
  `SwapResult` (the §5 acknowledgement) and `swap(battleId, fromCell, toCell,
  clientSequence)`, invoking `'Swap'` with exactly those four arguments and
  returning the result verbatim; refreshed the class header (the implemented
  client → server surface is now `JoinBattle`, `Ping`, `Swap`).
- `src/frontend/client/src/game/runtime/GameRuntimeEvents.ts` — replaced the
  placeholder `RuntimeActionRequest` (`kind: string` + index signature) with the
  documented `RuntimeSwapActionRequest` (`kind`/`fromCell`/`toCell`),
  added `RUNTIME_ACTION_SWAP` and `RuntimeActionAcknowledgement`, and narrowed
  `GameRuntimePort.requestAction` to
  `Promise<RuntimeActionAcknowledgement>`; refreshed the
  `RuntimeActionNotImplementedError` message.
- `src/frontend/client/src/game/runtime/GameRuntime.ts` — implemented
  `requestAction` for the Swap action (battle id sourced from the runtime's own
  `battleState`, opaque per-request `clientSequence`, transport submission, §5
  acknowledgement resolved to the caller); other kinds still throw
  `RuntimeActionNotImplementedError`. Added the action-boundary imports, the
  `clientSequenceCounter` module counter, `nextClientSequence()`, and updated the
  class doc.
- `src/frontend/client/src/game/scenes/BattleScene.ts` — added the two-tap cell
  selection on the existing board layer, `cellIndexAt` (the inverse of the
  existing `drawCell` index → screen mapping), `submitSwap` through the runtime
  port only, and `renderSwapStatus` (selection / in-flight / accepted / rejected
  / transport-failure feedback).

**Client tests**
- `src/frontend/client/tests/SignalRService.test.ts` — new
  "Swap request transport" block (7 cases) and a configurable `invoke` result on
  the existing fake hub.
- `src/frontend/client/tests/GameRuntime.test.ts` — `FakeSignalR` gained
  `swap()`; the `client → server request boundary` block was rewritten from the
  foundation guard into 8 Swap-path cases (see §Guard-Test Changes).
- `src/frontend/client/tests/SceneLifecycle.test.ts` — the scene harness gained
  board-input capture and `requestAction` capture, and the `phaser` mock gained
  `Input.Events`; new "BattleScene — Swap input" block (12 cases); the
  resolution-presentation guard was narrowed (see §Guard-Test Changes).
- `src/frontend/client/tests/RuntimeBoundaries.test.ts` — the "no gameplay Hub
  methods" guard was rewritten into the precise stage-advanced boundary (see
  §Guard-Test Changes).

**Task lifecycle**
- `tasks/backlog/TASK-069-…md` → `tasks/active/…md` → `tasks/completed/…md`
  (BACKLOG → READY → IN PROGRESS → IN REVIEW → DONE).

**NOT touched:** `src/backend/**`, `tests/backend/**`, `docs/**`, any ADR, any
API, TASK-067, TASK-068. No TASK-070 was created.

### Guard-Test Changes (each a cited stage advance)

1. `RuntimeBoundaries.test.ts` — `the client exposes no gameplay Hub methods`
   → `the client invokes exactly the documented gameplay Hub methods`.
   Stage advance: TASK-069 implements `SIGNALR_PROTOCOL.md` §2.1's Swap, whose
   server half already existed (`BattleHub.Swap`). Preserved properties: the
   invokable set is asserted *exactly* (`['JoinBattle', 'Ping', 'Swap']`),
   `'CardCast'`, `'PetSkillCast'`, and `'GetBattleState'` are still asserted
   absent with the `BattleHub_ShouldNotRegisterGameplayMethods` citation, and
   `'JoinBattle'` is still asserted present. The file's other 25 assertions are
   unchanged.
2. `SceneLifecycle.test.ts` — `contains no gameplay interaction or resolution
   presentation` → `contains no resolution presentation`.
   Stage advance: `MATCH3_RULES.md` §2 item 1's interaction is now implemented,
   so a `Swap` *request* line is no longer a violation. Narrowed explicitly to
   the still-forbidden resolution readouts (`Boss`, `Combo`, `Damage`,
   `Cascade`, `Match`) with the `SIGNALR_PROTOCOL.md` §2.1/§5 citation for why
   the request and its acknowledgement are transport feedback. No other
   assertion in the file was weakened; all 35 pre-existing cases still pass.
3. `GameRuntime.test.ts` — `exposes the boundary without implementing gameplay
   actions` → the 8-case Swap-path block.
   Stage advance: the same Swap implementation. Preserved properties: an
   unmodelled kind, `CardCast`, `PetSkillCast`, and `GetBattleState` are all
   still asserted to reject with `RuntimeActionNotImplementedError`, and no
   request is sent in those cases. The file's other 49 assertions are unchanged.

### Validation Results
- Frontend unit tests (`npm run test:run`, complete `src/frontend/client`
  suite): **PASS** — 13 files, **228 passed, 0 failed**
  (baseline before this task: 202 passed; +26 net new/updated cases)
- Frontend type-check + build (`npm run build` → `tsc && vite build`):
  **PASS** (exit 0; only the pre-existing `@microsoft/signalr` Rollup
  comment/chunk-size warnings)
- Backend regression (`dotnet test src/backend/GameServer.sln`): **PASS** —
  **1634 passed, 0 failed, 0 skipped**
  (Api 187 · Application 294 · Domain 951 · Infrastructure 202).
  No backend file was touched by this task.

### Swap Contract As Implemented

```text
hub method      Swap                                          (SIGNALR_PROTOCOL.md §2)

request         connection.invoke('Swap', battleId, fromCell, toCell, clientSequence)
                exactly four arguments — no Gem type, match, Combo, Turn, or
                Sequence value (§2.1 item 4)

battleId        sourced by GameRuntime from its own synchronized battle state
                (the server's BattleStateUpdated push), never from the scene
fromCell/toCell §1.0 row-major cell indices 0..63, and nothing else
                (MATCH3_RULES.md §2.1.1, §2.1.3); the pair is unordered
clientSequence  opaque, generated per request by GameRuntime
                (`swap_<n>`), never BattleState.Sequence, never compared with
                or derived from it, never used to reject a stale action
                (SIGNALR_PROTOCOL.md §2 item 1, MATCH3_RULES.md §2.1.4 item 1)

acknowledgement { accepted: true } | { accepted: false, reason: "<CODE>" }
                returned to the caller unchanged (§5); delivered to the caller
                only, not broadcast, not sequenced, not a Battle Event

failure paths   no known battle        → Error thrown, nothing sent
                no connection          → Error thrown, nothing sent
                rejected swap          → nothing mutated, nothing retried,
                                         reason surfaced as presentation only
                accepted swap          → no local change; the next
                                         BattleStateUpdated push re-renders
```

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (request only; all
      validation, resolution, and state mutation remain server-side) —
      `SignalRService.swap` only invokes and returns; `GameRuntime.requestAction`
      only sources `battleId`, generates the correlation id and forwards;
      `BattleScene` only maps a pointer to a cell and renders the ack. No range,
      adjacency, match, cascade, combo, damage, or `Sequence` computation exists
      in any of the three.
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1 — server-authoritative
      battle / client presentation is IN; no §2 OUT item introduced)
- [x] Confirmed no backend, docs, API, event, Redis, or SignalR contract change
      (`BattleHub.Swap` byte-identical; no `docs/**` or ADR modified;
      `POST /api/battle/start`, `GET /api/pets|/api/cards|/api/relics` untouched)
- [x] Confirmed `CardCast` / `PetSkillCast` / `GetBattleState` remain
      unimplemented on the client — zero occurrences anywhere in `src/`, and all
      three still reject with `RuntimeActionNotImplementedError`
- [x] Confirmed no local board mutation on rejection and no prediction on
      acceptance — the scene's `accepted: true` path rewrites no cell and the
      rendered board changes only on the next authoritative push; the
      `accepted: false` path mutates nothing and retries nothing
- [x] Confirmed every guard-test change is a cited stage advance, not a weakened
      property (`AGENTS.md` §15) — all three are documented in
      §Guard-Test Changes above, each preserving every property that still holds

### Review (quality/review.md §1)

```text
Correctness       PASS — matches SIGNALR_PROTOCOL.md §2/§2.1/§5, MATCH3_RULES.md
                  §1.0/§2/§2.1.1/§2.1.5, ARCHITECTURE.md §2.2, ADR-001/003/004
Architecture      PASS — BattleScene → GameRuntime → SignalRService → hub;
                  scenes import no transport type (boundary test still passes);
                  the runtime still imports no Phaser and no @microsoft/signalr
Scope             PASS — client-only; nothing unrelated refactored; no TASK-070
                  created; TASK-067/068 unmodified
Tests             PASS — MEDIUM depth: unit + boundary-crossing integration
                  (mocked transport) + documentation validation
Documentation     PASS — no documentation change required (see below)
Security          PASS — no auth surface touched; the access-token factory and
                  ApplicationSession handling are unchanged
Performance       PASS — one transport invoke per selection; no hot-path query,
                  no board scan, no resolution work added to the client
Maintainability   PASS — no new abstraction; one method on the existing
                  transport service, one branch in the existing request
                  boundary, presentation-local state in the existing scene
Determinism       PASS — server remains the sole owner/calculator/validator/
                  broadcaster of Board, Turn, Sequence, HP, Damage, Match,
                  Cascade, Combo; no client RNG was introduced (the correlation
                  id is a monotonic counter, not randomness)
```

### Documentation
No documentation changes. The end-to-end comparison against
`SIGNALR_PROTOCOL.md` (§2, §2.1, §3.1, §4, §5, §6), `GAME_STATE.md` (§2.0,
§2.1.1, §2.2, §2.3, §5), `GAME_EVENTS.md` (§1.2), `MATCH3_RULES.md`
(§1.0, §2, §2.1.1–§2.1.6), `ARCHITECTURE.md` (§2.2), and `TDD.md` (§2.1) found
**no contradiction**: the documented Swap request/acknowledgement contract is
exactly what the client now sends and consumes. No contract was extended and no
document was edited.

