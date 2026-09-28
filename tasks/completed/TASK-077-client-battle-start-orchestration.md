# TASK-077 — Implement Client Battle-Start Orchestration

---

## Metadata

```text
Task ID:           TASK-077
Type:              FEATURE
Status:            DONE
Risk:              LOW
Priority:          HIGH
Primary Agent:     client
Supporting Agents: N/A
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, client/client-state-authority, realtime/realtime-protocol-validation, testing/test-scenario-generation, quality/scope-validation
Dependencies:      TASK-069, TASK-076
```

---

## Objective

Implement the client half of the documented battle-start sequence as one runtime method: `GameRuntime.startBattle(request: BattleStartRequest)` submits the request through the existing `ApiService.startBattle` (TASK-076), connects the existing `SignalRService` connection to the response's `signalrHub` when it is not already connected, invokes `JoinBattle` with the response's `battleId`, and leaves establishment of the synchronized copy to the existing `BattleStateUpdated` push handler — i.e. `SIGNALR_PROTOCOL.md` §1 items 1–2 and the §2 `JoinBattle` contract, with no new hub methods, no new events, no wait/timeout invented for the push, and no client-side state derivation. This is the "battle creation and joining orchestration" TASK-069 explicitly deferred and TASK-071's Next Step named as the next client step.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Battle Loop (Single-Player vs. Boss) is IN scope
- `docs/02-technical/SIGNALR_PROTOCOL.md` §1 (items 1–3, §1.1, §1.2) — obtain `battleId` and hub URL from `POST /api/battle/start`, connect and join the group, authenticate with the application session
- `docs/02-technical/SIGNALR_PROTOCOL.md` §2 (`JoinBattle(battleId)` and its items 1–4) — the join is a transport step; its only observable effect is the §4 state push, not a state request
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4, §4.1 — `BattleStateUpdated` initial-state delivery is server-initiated on group join; §7, §8.3, §8.4 — no client fetch/snapshot/re-creation path
- `docs/02-technical/API_CONTRACTS.md` §3 — start response members (`battleId`, `signalrHub`, `initialState`)
- `docs/02-technical/API_CONTRACTS.md` §2.8, §6 — session bearer transport, `401 UNAUTHENTICATED`, `400` loadout errors, error envelope
- `docs/02-technical/TDD.md` §2.1 — client responsibility split (runtime coordinates transport; `LobbyScene` owns the match-start trigger) and the Discord/session-before-SignalR authentication architecture
- `docs/02-technical/ARCHITECTURE.md` §2.2.1 (rules 1–6 and the initial-state paragraph) — GameRuntime coordinates the initial state subscription; runtime coordinates, does not compute; one runtime, one connection
- `docs/02-technical/GAME_STATE.md` §2 — authoritative `BattleState` is server-owned; the client holds only a synchronized presentation copy
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — server authority
- `docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md` — scene/runtime boundary
- `docs/03-decisions/ADR/ADR-004-signalr-realtime.md` — realtime transport decision
- `docs/03-decisions/ADR/ADR-008-battle-recovery.md` — reconnect/snapshot path is separate from this task
- `docs/03-decisions/ADR/ADR-015-application-session-authentication-contract.md` — session token propagation to REST and hub

---

## Scope

### In Scope

- One public orchestration method `GameRuntime.startBattle(request: BattleStartRequest): Promise<void>` executing, in order:
  1. `ApiService.getInstance().startBattle(request)` → `POST /api/battle/start`;
  2. `SignalRService.connect(response.signalrHub)` — the documented hub URL from the response (§1 item 1); the service itself is the idempotency point, so an already-connected runtime opens no second connection;
  3. `SignalRService.joinBattle(response.battleId)` (§1.2, §2).
- Guaranteeing the transport subscriptions (`ReceiveEvents`, `BattleStateUpdated`) exist exactly once for any connection this orchestration opens — including the case where `initialize()` recorded a connection failure before it reached its subscription step — so the join-triggered push is never silently unobserved.
- Rejection semantics: failures (REST `401`/`400`/transport error, connect failure, join failure) reject to the caller with runtime state unchanged, mirroring `requestAction`'s treatment of presentation-level failures (`SIGNALR_PROTOCOL.md` §8.3).
- Unit tests covering the sequence, the failure paths, the exactly-once subscriptions, and the single synchronization path.

### Out of Scope

- **Client application-session establishment** — the Discord → `ApiService.authenticateDiscord` → `ApplicationSession` → `GameRuntime.setSessionStatus` production wiring. (Suggested follow-up task; not created here.) Tests establish a session directly (precedent: `tests/BattleService.test.ts`), so this task is testable without it.
- **`App.tsx` connect-at-mount timing** and its pre-existing divergence from `TDD.md` §2.1 item 4 (session before SignalR connect) — report-only per `AGENTS.md` §16; unchanged here.
- **Loadout selection UI, scene navigation, and scene creation** — `MainMenuScene` / `LobbyScene` / `ResultScene` do not exist yet; `TDD.md` §2.1 assigns the match-start trigger to `LobbyScene`, a separate task.
- Any production caller wiring (React screen or scene) of `startBattle`.
- Using the REST `initialState` member for rendering, as the synchronized copy, or as any second synchronization path.
- Reconnection/resync (`GetBattleState`, `SIGNALR_PROTOCOL.md` §7, ADR-008), `Ping`, `CardCast`, `PetSkillCast`.
- New SignalR client methods or events (`BattleStarted`, `SyncBattle`, `BattleReady` or similar), any push-wait timeout constant, any client-authoritative value, any gameplay logic.
- Backend changes, documentation changes, changes to TASK-036 or any completed task.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State

Every step exists in isolation but nothing connects them: `ApiService.startBattle` (`src/frontend/client/src/services/api/ApiService.ts:207`, TASK-076) and `SignalRService.joinBattle` (`src/frontend/client/src/services/realtime/SignalRService.ts:468`) have no production caller, and `GameRuntime` connects at app mount (`src/frontend/client/src/app/App.tsx:62`) but never joins a battle, so `sync` stops at `'awaiting_battle'` and `getBattleState()` remains `null` in production. The push side is already complete: `GameRuntime.receiveBattleState` (`src/frontend/client/src/game/runtime/GameRuntime.ts:494`) stores the payload and sets `sync: 'synchronized'` (line 505), and `requestAction` (line 359) then sources `battleId` from that pushed state (TASK-069). `initialize()` registers its `ReceiveEvents`/`BattleStateUpdated` subscriptions only after a successful connect (lines 201–212), so a previously failed initialization leaves a later connection without handlers. No completed task wires Discord → application session either, so a real call without a session receives the documented `401` until that separate follow-up task lands.

---

## Acceptance Criteria

- [x] `GameRuntime.startBattle(request: BattleStartRequest): Promise<void>` exists and executes the three documented steps in order (REST start → `connect(response.signalrHub)` → `joinBattle(response.battleId)`), invoking no other hub method (`Swap`, `Ping`, …).
- [x] The method resolves after the join; no timeout, poll, or await-for-`sync` constant is added, and no second synchronization path exists.
- [x] `sync` becomes `'synchronized'` and `getBattleState()` becomes non-null only when a valid `BattleStateUpdated` push arrives through the existing handler; `response.initialState` is never stored, merged, or exposed as battle state.
- [x] After a successful start, the existing TASK-069 swap path (`requestAction`) works unchanged against the pushed `battleId`.
- [x] On any failure (REST `401 UNAUTHENTICATED` / `400` loadout error / transport error; connect failure; join without a connection; disposed runtime), the promise rejects to the caller and `sync`, `battleState`, and `connection` are unchanged.
- [x] After `startBattle` succeeds, `ReceiveEvents` and `BattleStateUpdated` are subscribed exactly once — including when `initialize()` previously failed at its connect step, and with no duplicates when `initialize()` already subscribed.
- [x] No new SignalR client method, runtime event type, or wire contract is introduced; no gameplay or battle-state value is computed, defaulted, or derived client-side.
- [x] No file outside `src/frontend/client/src/game/runtime/` and `src/frontend/client/tests/` is changed (`App.tsx`, scenes, services, docs, backend all untouched).
- [x] All relevant tests pass at the required validation depth (`core/validation.md` §2).
- [x] Quality review checklist passes (`quality/review.md` §1).
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[x] src/frontend/client/src/game/runtime/GameRuntime.ts (add startBattle; extract exactly-once transport-subscription helper from initialize)
[x] src/frontend/client/src/game/runtime/GameRuntimeEvents.ts (NOT changed — GameRuntimePort is not extended, see Implementation Notes)
[x] src/frontend/client/tests/ (GameRuntime.test.ts; FakeSignalR mirrors joinBattle/isConnected)
[x] src/frontend/client/src/services/api/ (NOT changed — TASK-076 surface is sufficient)
[x] src/frontend/client/src/app/App.tsx (NOT changed)
[x] src/backend/ (none)
[x] docs/ (none)
[x] tests/ (backend suite not applicable — no backend file touched)
```

---

## Implementation Notes

- Existing pieces to build on: `ApiService.startBattle` (`ApiService.ts:207`), `SignalRService.connect` (`SignalRService.ts:337` — returns immediately when already `Connected` and shares an in-flight start promise), `joinBattle` (`:468` — throws when not connected), `GameRuntime` constructor's injectable transport default (`GameRuntime.ts:100`). Inject `ApiService` the same way (constructor default `ApiService.getInstance()`) so tests pass doubles; `game/` code importing `services/` already has precedent in the `SignalRService` import.
- Call `connect(response.signalrHub)` unconditionally and let the service be the idempotency point (`ARCHITECTURE.md` §2.2.1 rule 6 — one runtime, one connection); do not build a second connection policy in the runtime.
- Extract the subscription registration currently inlined at `GameRuntime.ts:203–212` into a private helper invoked by both `initialize()` and `startBattle()`, guarded to register exactly once (the idempotency precedent `initialize()` already establishes at lines 119–124).
- Resolve after `joinBattle`; the push arrives asynchronously (`SignalRService.ts:463–466`). Do not await `sync`, and do not invent a timeout — no timeout value is documented.
- Do **not** extend `GameRuntimePort` (`GameRuntimeEvents.ts:226`): no scene calls this method in this task. Precedent: `requestAction` sits on the port because `BattleScene` calls it; `TDD.md` §2.1 assigns the match-start trigger to `LobbyScene`, which a later task owns. `startBattle` is a public method on the concrete `GameRuntime` (React's shell already holds the concrete instance, `App.tsx:22–27`).
- Session handling needs no new code: `ApiService.post` already attaches `ApplicationSession`'s bearer (`ApiService.ts:246`) and `connect` already supplies `accessTokenFactory` (`SignalRService.ts:358`). A missing session yields the documented `401`, which is propagated, not handled.
- Mirror `requestAction`'s failure style (`GameRuntime.ts:355–390`): reject with the transport's error; do not add `lastError`/`runtime_error` recording for start failures unless an existing code path already requires it for the same condition.
- Guard a disposed runtime the way `initialize()` does (line 115–117). Tests follow the app lifecycle and call `initialize()` first (`App.tsx:62`).

---

## Testing Requirements

### Required Verification

```text
[ ] Unit tests         — GameRuntime.startBattle sequencing (REST → connect → join, with the response's battleId/hub URL and no other hub call); rejection paths (401, 400, transport error, connect failure, join without connection, disposed runtime) leaving state untouched; exactly-once subscriptions including the previously-failed-initialize case; sync flips only on push; response.initialState never observable via getBattleState(); already-connected connect does not open a second connection.
[ ] Integration tests  — REST+SignalR boundary exercised through injected ApiService/SignalRService doubles (no live server); FakeSignalR gains joinBattle/isConnected mirrors including the real service's throw-when-disconnected behaviour.
[ ] Gameplay scenarios — N/A: no gameplay rule is derived or exercised by this task (AGENTS.md §6 maps gameplay scenarios to rule docs; none are touched here).
```

### Key Edge Cases

- No application session → `401 UNAUTHENTICATED` propagated (`API_CONTRACTS.md` §2.8), `sync`/`battleState` unchanged; session established directly in the test (precedent `BattleService.test.ts`).
- `400 INVALID_LOADOUT` / `PET_NOT_OWNED` / `BOSS_NOT_FOUND` propagated with no `connect`/`joinBattle` call made.
- `initialize()` previously failed at connect → `startBattle`'s connect succeeds and the push is observed (subscriptions completed), with no duplicates if `initialize()` had already subscribed.
- Already connected at mount → `connect` called with the response hub URL but no second connection is created (ARCHITECTURE §2.2.1 rule 6).
- Server push arrives → `sync: 'synchronized'`, `onBattleState` listeners notified once; a malformed push is ignored by the existing handler (no fabricated state).
- `startBattle` called after `dispose()` → rejects, no connection opened.
- A repeated `startBattle` call has no documented rule — do not add a client-side guard; observe behaviour and report if it is indeterminate (see Stop Conditions).

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If task requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose
- If wiring the sequence requires a production Discord → application-session path (no documented non-iframe/local session flow exists): STOP per `AGENTS.md` §7/§20 — that is the recorded follow-up task, and no session mechanism may be invented here
- If satisfying any criterion requires changing `App.tsx` connect-at-mount timing or resolving its divergence from `TDD.md` §2.1 item 4: STOP per `AGENTS.md` §4/§20 — report the conflict, change nothing
- If any criterion requires a loadout-selection UI, a new scene, or scene navigation: STOP per `AGENTS.md` §8 — out of scope
- If implementing requires a push-wait timeout, a second sync mechanism, or a hub method/event no document defines: STOP per `AGENTS.md` §7/§11
- If a repeated/overlapping `startBattle` requires a rule that no document states: STOP and report rather than inventing a guard

---

## Completion Evidence

### Changed Files

- `src/frontend/client/src/game/runtime/GameRuntime.ts` — added the public
  `startBattle(request: BattleStartRequest): Promise<void>`, which executes the
  documented sequence only (`ApiService.startBattle` → `SignalRService.connect(response.signalrHub)`
  → `SignalRService.joinBattle(response.battleId)`) and resolves after the join.
  Extracted the `ReceiveEvents`/`BattleStateUpdated` registration previously
  inlined in `initialize()` into the private exactly-once helper
  `registerTransportSubscriptions()`, now invoked by both `initialize()` and
  `startBattle()`. Added constructor injection of `ApiService`
  (default `ApiService.getInstance()`), mirroring the existing `SignalRService`
  default. `response.initialState` is never read.
- `src/frontend/client/tests/GameRuntime.test.ts` — extended `FakeSignalR` with
  `joinBattle` (throwing when disconnected, as the real service does),
  `isConnected`, physical-connection and registration counters, and recorded hub
  invocations; added 23 focused `startBattle` tests (sequence order, exact
  `signalrHub`/`battleId` use, no other hub call, single synchronization path,
  `initialState` never promoted, exactly-once subscriptions for cases A/B/C,
  single physical connection, and every rejection path).

No other file was changed. `GameRuntimeEvents.ts` (`GameRuntimePort`),
`ApiService.ts`, `SignalRService.ts`, `App.tsx`, all scenes, `src/backend/`,
`docs/`, TASK-036, and all completed tasks are untouched.

### Validation Results

- `npm run test:run` (vitest) in `src/frontend/client` — **PASS**: 315 tests /
  15 files (baseline was 292 tests / 15 files at TASK-076 completion; +23 new
  `startBattle` tests, 0 regressions).
- `npm run build` (`tsc` + `vite build`) in `src/frontend/client` — **PASS**
  (built in 2.51s). The only warnings are the pre-existing upstream
  `@microsoft/signalr` `/*#__PURE__*/` rollup annotations and the chunk-size
  advisory; neither is introduced by this task.
- Backend suite — **not run**, because no `src/backend/` file was touched.

### Test-suite load-bearing verification (mutation checks)

Each acceptance-critical property was verified by mutating the implementation and
confirming the tests fail, so the suite is not tautological:

```text
Mutation                                          Result
------------------------------------------------  -------------------------------
Remove the exactly-once guard from                2 tests FAIL  (case A / case C)
  registerTransportSubscriptions()
Remove startBattle's subscription registration     4 tests FAIL  (incl. the
                                                     previously-failed-init case)
Use response.initialState as runtime state        rejected (compile + behaviour)
Call an extra hub method after the join           14 tests FAIL (incl. "invokes no
                                                     other hub method")
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic — `startBattle`
      coordinates transport only and computes no `BattleId`, `Turn`, `Sequence`,
      RNG, board, HP, ATK, DEF, damage, match, or combo value. A case-sensitive
      scan of the code (comments stripped) shows none of the runtime boundary's
      forbidden terms.
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — battle start and
      realtime communication are IN scope; no OUT item is introduced.
- [x] Confirmed no changes to `App.tsx`, scenes, docs, backend, TASK-036, or
      completed tasks.
- [x] Confirmed the follow-up session-wiring task was reported, not silently
      implemented (see Remaining Issues).
- [x] Confirmed the existing TASK-069 swap behavior is unchanged — its tests pass
      unmodified, and a new test asserts `requestAction` still works against the
      pushed `battleId` after a successful start.

### Remaining Issues

Report-only, per `AGENTS.md` §16 — **not** fixed here:

1. **Client application-session establishment is still unwired.** No production
   path connects Discord → `ApiService.authenticateDiscord` →
   `ApplicationSession` → `GameRuntime.setSessionStatus`, so a real
   `startBattle` call without a session receives the documented
   `401 UNAUTHENTICATED` until that separate follow-up task lands. This is
   TASK-077's declared Out of Scope and was not implemented.
2. **`App.tsx` connects at mount, before any session exists**, diverging from
   `TDD.md` §2.1 item 4 (session before SignalR connect). Unchanged here by
   scope; `startBattle` calls `connect` unconditionally and lets the service own
   idempotency, so it does not depend on that timing.
3. **Repeated/overlapping `startBattle` calls have no authoritative rule.** Per
   §14 no client-side guard was added. Observed behavior is indeterminate in the
   sense that two overlapping calls issue two REST starts and two joins; the
   server remains the arbiter. No document states a dedupe rule, so none was
   invented — flagging it as a requirement gap rather than resolving it.
4. `src/frontend/client/tests/GameRuntime.test.ts` still carries `element: 'Hoa'`
   inside a payload that its test asserts is dropped (pre-existing, recorded by
   TASK-074/TASK-076; on the SignalR payload path, not this task's contract).
