# TASK-234 — Implement Standalone Web Sign-Out & HTTP 401 Session Invalidation

<!--
  GEN-TASK EXECUTION MANIFEST

  Lifecycle: BACKLOG → READY completed 2026-10-09 by the orchestrator role at
  the requester's direction, after validating the tasks/TASK_LIFECYCLE.md §3
  READY criteria:
    [x] Task type confirmed (FEATURE — tasks/TASK_TYPES.md / development/feature.md)
    [x] Relevant documentation exists in docs/ (ADR-015, ADR-020, ADR-022,
        API_CONTRACTS.md §1/§2/§6, ARCHITECTURE.md §2.2.1/§2.2.2/§2.3, TDD.md §2.1)
    [x] MVP scope confirmed (MVP_SCOPE.md §1 — standalone web account)
    [x] Not blocked: Dependencies: None
    [x] Primary agent assigned (client) and workflow assigned (development/feature.md)
    [x] Acceptance criteria are binary and testable
    [x] Uniqueness: highest assigned ID is TASK-233; no other record names TASK-234
    [x] Ownership: tasks/active/, tasks/backlog/ (other than this record) and
        tasks/blocked/ hold no open record naming the declared file set

  Lifecycle: READY → IN PROGRESS → IN REVIEW → DONE completed 2026-10-09 by the
  client agent under development/feature.md: implementation, tests, HIGH-risk
  validation and quality/review.md §1 (disposition Pass) all completed, then the
  two-stage commit protocol (TASK_LIFECYCLE.md §6) — Phase A
  06eef7957b660c1647bd9ba36536ecda3885d4ad and this Phase B filing.
-->

---

## Metadata

```text
Task ID:           TASK-234
Type:              FEATURE
Status:            DONE
Risk:              HIGH
Priority:          MEDIUM
Primary Agent:     client
Supporting Agents: testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis, client/react-phaser-boundary, testing/test-scenario-generation, quality/implementation-review
Dependencies:      None
Declared Files:    src/frontend/client/src/game/runtime/GameRuntime.ts, src/frontend/client/src/app/App.tsx, src/frontend/client/tests/GameRuntime.test.ts, src/frontend/client/tests/AppLifecycle.test.tsx
```

---

## Objective

Make the standalone web client's session lifecycle complete on the two paths that are already decided but unimplemented: (1) an authenticated API request that receives the documented `401 UNAUTHENTICATED` must stop being an active session — the stale credentials are cleared through the existing `ApplicationSession.clear()`, the runtime publishes `session: 'unauthenticated'` through the runtime state it already owns, and `App` therefore renders `AuthScreen` instead of `GameShell`; and (2) the authenticated application shell must offer an explicit, operable sign-out control (`ADR-020` D4 item 2) that performs that same canonical cleanup. Both paths must share one idempotent cleanup (no duplicated session state, no new event system), must release the authenticated transport so no SignalR connection or synchronized battle copy outlives the session, and must leave the client able to authenticate again in the same document.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — "Player account (standalone web account with username/password registration/login, PostgreSQL Accounts, JWT/ApplicationSession, collection owner)": the IN-scope surface this task completes; the block is attributed to `ADR-020` by that section's own version note.
- `docs/03-decisions/ADR/ADR-020-standalone-web-account-authentication.md` D4 item 2 — the session token is persisted in `localStorage` so a refresh preserves the session, and "a sign-out/logout option clears the token" (the decided-but-unbuilt behavior).
- `docs/03-decisions/ADR/ADR-020-standalone-web-account-authentication.md` D5 items 1–2 — unauthenticated → React presents `AuthScreen`; authenticated → the existing `GameShell` mounts Phaser.
- `docs/02-technical/API_CONTRACTS.md` §1 preamble and §2.3 "Coverage" — every endpoint except `POST /api/auth/register` and `POST /api/auth/login` requires an authenticated session; `BattleHub` requires the same session.
- `docs/02-technical/API_CONTRACTS.md` §2.3 "Failure behavior" — missing, invalid/tampered, and expired sessions share one public response: `401` + `{ "error": "UNAUTHENTICATED" }` (§6 envelope).
- `docs/02-technical/API_CONTRACTS.md` §2.3 "Lifecycle (MVP)" — absolute expiry 24 hours, no idle timeout, no renewal/refresh, and revocation `none` (no logout, no server-side revocation state); a new successful §2 exchange issues a new JWT and does not invalidate previously issued tokens.
- `docs/02-technical/API_CONTRACTS.md` §2.2 rule 2 — `POST /api/auth/login` answers `401` with `INVALID_CREDENTIALS`: the one `401` in this contract that is **not** a session-invalidation signal and must not trigger this task's cleanup.
- `docs/02-technical/API_CONTRACTS.md` §6 — the error envelope the client already reads.
- `docs/03-decisions/ADR/ADR-015-application-session-authentication-contract.md` D5 and Consequences (Negative) — no server-side revocation exists in MVP; a token stays valid until its 24-hour absolute expiry even after the player authenticates again; early invalidation (logout) is unavailable without a future decision. This task introduces none.
- `docs/02-technical/ARCHITECTURE.md` §2.2.1 rules 1, 3, 5 and 6 — scenes reach the transport only through the runtime port; the runtime owns connection state, runtime lifecycle and runtime state; `state/GameRuntimeState.ts` carries connection/session/runtime/synchronization status only; one runtime, one connection.
- `docs/02-technical/ARCHITECTURE.md` §2.2.2 rule 4 — the React overlay layer is absolutely positioned and passes pointer events through "except where a child explicitly opts in".
- `docs/02-technical/ARCHITECTURE.md` §2.3 items 3–5 — the authentication boundary is React → `ApiService` → `ApplicationSession`, the hub connects with the application session token, and no coupling exists between authentication presentation and `BattleScene` or any game system.
- `docs/02-technical/TDD.md` §2.1 — the Phaser/React split: React owns the application/platform shell, HTML overlays, menus, connection status UI and the authentication boundary, while game-related interactive flows belong inside Phaser; `MainMenuScene` is "Main game menu presentation and navigation".
- `docs/03-decisions/ADR/ADR-022-post-result-preserved-loadout-carrier.md` D3, D5 and D6 — the preserved pre-battle loadout is client presentation state whose lifetime is the running game instance and which the runtime port does not carry; the runtime port gains only explicitly documented client-local capabilities (`clearActiveBattleState` precedent).
- `tasks/TASK_LIFECYCLE.md` §6 — the two-stage commit protocol (Phase A implementation slice commit, Phase B completion record filing commit).

Gap evidence (not authoritative for behavior; records the audit that opened this task): `tasks/completed/TASK-211-post-task-210-product-audit.md` §Part C — NG-12 ("no logout; a mid-session 401 never resets the session"), and `tasks/completed/TASK-233-fix-authscreen-duplicate-username-error-code-mapping.md` §Scope, which explicitly leaves sign-out, session invalidation, and the NG-13 initialization case out of its own scope.

---

## Scope

### In Scope

1. **HTTP 401 invalidation (included).** A `401` raised by the shared authenticated REST transport — `ApiService.get` / `ApiService.post`, which are used only for endpoints that `API_CONTRACTS.md` §2.3 "Coverage" places behind the session — invalidates the client's session through the canonical mechanisms: `ApplicationSession.clear()` for the stored credentials and the runtime's existing session-status publication for the state transition.
2. **Explicit sign-out (included).** A visible, operable sign-out control in the authenticated application shell. Activating it performs the same canonical cleanup and returns the application to `AuthScreen` in the same document.
3. **Re-authentication after either path (included).** After invalidation or sign-out, a subsequent successful login/registration must re-establish the session in the same document: the authenticated transport reconnects, the documented `ReceiveEvents` / `BattleStateUpdated` subscriptions are registered again, and `GameShell` mounts again.
4. **Shared, idempotent cleanup (included).** One cleanup routine serves both paths, is safe to run repeatedly and when no session/battle is held, drops the runtime's synchronized battle copy, releases the authenticated transport connection, and performs no HTTP request.
5. Unit and state-transition test coverage for the above, and the two-stage commit protocol.

### Out of Scope

- **Authentication initialization recovery (excluded — unresolved decision).** `App.tsx`'s post-login path currently sets session status `'error'` when `initialize()` rejects after a successful `POST /api/auth/login` / `/register` (`App.tsx:101-109` + `:119`), leaving the just-stored token in place while `AuthScreen` is rendered (audit NG-13). No authoritative document states what the client must do when the `BattleHub` connection or runtime initialization fails *after* a successful §2 exchange: retaining the valid token for a retry and clearing it are both consistent with `ADR-020` D4 item 2 / `TDD.md` §2.1, and the bootstrap path already deletes a valid credential on any transient hub-connect error (`App.tsx:41-49`). Per `AGENTS.md` §20 ("Ambiguous requirement") this is a separate, decision-gated item, not part of this task.
- Any server-side revocation, logout endpoint, session store, or token invalidation semantics (`API_CONTRACTS.md` §2.3 "Lifecycle (MVP)"; `ADR-015` Consequences — Negative). The signed-out token keeps its documented 24-hour server-side validity.
- Any backend, database, API-contract, SignalR-protocol, documentation, or ADR change.
- `MainMenuScene` (the menu is a game-related presentation flow, `TDD.md` §2.1, and needs no change for a session/platform action; adding a capability to `GameRuntimePort` would also break the member set pinned by `tests/RuntimeBoundaries.test.ts:666-715`), and any `BattleScene` / `LobbyScene` / `ResultScene` behavior.
- The DEV-only diagnostics (`StatusOverlay`, `ViewportDebugOverlay`) and any gameplay HUD.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State

Verified against the working tree at `74c1dda` (clean; `npm run test:run` in `src/frontend/client` → 23 files / 917 tests passing):

- `ApplicationSession` already owns the canonical credential store and clearing mechanism: three `localStorage` keys (`ApplicationSession.ts:25-29`), `establish()` (`:64`), `restoreFromStorage()` (`:84`), `clear()` (`:108-122`), `isAuthenticated()` (`:136`). `clear()` has exactly one application caller today — the failed-bootstrap path in `App.tsx:47`.
- The runtime already owns session status: `GameRuntime.setSessionStatus()` (`GameRuntime.ts:318-320`) writes the `session` member of `GameRuntimeState` (`GameRuntimeState.ts:40-44, 65-86`), and `App.tsx:77-79` subscribes with `onRuntimeEvent(...)` and re-renders on `event.state.session`. `App.tsx:119-125` already renders `AuthScreen` for every status other than `'authenticated'`. No new event system is needed.
- No 401 handling exists: `ApiService.get` / `post` read the §6 envelope into `ApiRequestError` and throw (`ApiService.ts:232-250, 424-444`); nothing clears the token or transitions the session. `describeApiFailure` already classifies a bare `401` on this transport as "Your session is no longer valid. Please sign in again." (`ApiService.ts:111-113`) — the transport already treats it as a session failure, it simply never acts on it.
- No sign-out control exists: `logout` / `signOut` / `sign out` appear nowhere in `src/`. `MainMenuScene.ts:83-111` holds the only player-facing menu entries (`START BATTLE`, `COLLECTION`, `BATTLE HISTORY`) and the authenticated shell's overlay children are the two DEV-only diagnostics (`App.tsx:139-140`).
- Lifecycle constraints a naive cleanup would break: `GameRuntime.initialize()` is idempotent and returns early once `initialized` is set (`GameRuntime.ts:161-164`) while `dispose()` is terminal (`:266-270`, and `initialize()` throws after it — `:155-157`); `registerTransportSubscriptions()` no-ops while subscription unsubscribers exist (`:968-971`); `SignalRService.on()` requires a live connection (`SignalRService.ts:704-706`); `SignalRService.connect()` returns early when the existing connection is already `Connected` (`:592-594`) and the connection authenticates with whatever token built it (`accessTokenFactory`, `:612`). `SignalRService.disconnect()` is re-connectable (`:663-678`). Unmounting `GameShell` destroys the Phaser game (`PhaserGame.tsx:51-56`), which ends the game-wide registry and therefore the `ADR-022` preserved-loadout carrier's lifetime.
- Ownership: `tasks/active/`, `tasks/backlog/`, and `tasks/blocked/` are all empty, and neither declared implementation file is named by any open record; TASK-233 (DONE) touched `AuthScreen.tsx` / `AuthScreen.test.tsx` only. The highest assigned ID across the repository is TASK-233.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-234:
```text
src/frontend/client/src/game/runtime/GameRuntime.ts
src/frontend/client/src/app/App.tsx
src/frontend/client/tests/GameRuntime.test.ts
src/frontend/client/tests/AppLifecycle.test.tsx
```

*(The task manifest file itself is excluded from the implementation declared file set. This list strictly matches the `Declared Files:` field in `## Metadata`.)*

Why this set, and why nothing else:

- `GameRuntime.ts` — the only client component that calls `ApiService` for covered endpoints (`ARCHITECTURE.md` §2.2.1 rule 1; scenes never import the transport), and the owner of connection state, runtime lifecycle and the `session` runtime state (§2.2.1 rules 3 and 5). Both the 401 observation point and the cleanup capability belong here, which is also what keeps the cleanup single and idempotent instead of duplicated per scene.
- `App.tsx` — the owner of the authenticated/unauthenticated presentation decision (`ADR-020` D5) and the only place the authenticated shell's overlay children are declared (`:127-142`), so it holds the sign-out control and needs no other change: the render condition at `:119-125` already returns to `AuthScreen`.
- `GameRuntime.test.ts` / `AppLifecycle.test.tsx` — the existing suites that already cover the runtime's REST boundary `401` propagation (`GameRuntime.test.ts:2703-2710, 2852-2858`) and the App's session/login/register/pagehide lifecycle (`AppLifecycle.test.tsx`). Extending them is the smallest honest coverage; no new test file is required, and no scene, transport, or collection suite is touched.
- Deliberately **not** declared: `ApiService.ts` (its `ApiRequestError` already carries `status` and `code`; `register`/`login` must keep the `INVALID_CREDENTIALS` behavior untouched and are a separate method pair from `get`/`post`); `ApplicationSession.ts` (`clear()` and the storage keys already exist and are sufficient); `GameRuntimeEvents.ts` (the capability is app-facing like the existing `setSessionStatus`/`initialize`/`dispose`, and the `GameRuntimePort` member set is pinned by `tests/RuntimeBoundaries.test.ts:666-715`, which must keep passing unmodified); `MainMenuScene.ts` and `SceneLifecycle.test.ts` (no scene change is in scope); `CollectionService.test.ts` / `BattleService.test.ts` (an earlier audit proposal listed `CollectionService.test.ts`; the 401 classification lives in the runtime boundary, so that suite needs no change); `App.css` (position the control with an inline style, following `PhaserGame.tsx`'s precedent, so the declared set stays minimal — if a stylesheet rule proves necessary, STOP and declare `App.css` before writing it).

---

## Acceptance Criteria

- [ ] A qualifying authenticated `401` (a `401` from `ApiService.get` / `ApiService.post`) clears the stored session through `ApplicationSession.clear()`: `getSessionToken()`, `getPlayerId()`, and `getUsername()` are `null`, and the three `dcacti_*` `localStorage` keys are removed.
- [ ] The same `401` publishes `session: 'unauthenticated'` (the existing `SessionStatus` value, not a new one), and `App` then renders `auth-screen` and no longer renders `game-shell` — in the same document, without a reload.
- [ ] The cleanup drops the runtime's synchronized battle copy (`getBattleState()` is `null`) and releases the authenticated transport (`SignalRService.disconnect()` is invoked), so no live authenticated hub connection or stale battle state outlives the session.
- [ ] The cleanup is idempotent: a second `401` (including an overlapping in-flight call) after the session is already invalidated performs no second credential clear and no second transport disconnect, and leaves the state `unauthenticated`.
- [ ] The cleanup performs no HTTP request of its own (no `fetch` call is attributable to it) and adds no endpoint, wire message, or server-side revocation.
- [ ] A `401 INVALID_CREDENTIALS` from `POST /api/auth/login` (`API_CONTRACTS.md` §2.2 rule 2) and a `409 USERNAME_ALREADY_EXISTS` from `/register` are unchanged: no credential clearing, no session transition, and `AuthScreen`'s existing banner mappings still behave exactly as `AuthScreen.test.tsx` asserts.
- [ ] Non-401 authenticated failures are unchanged: `400 INVALID_LOADOUT` / `PET_NOT_OWNED` / `BOSS_NOT_FOUND`, `404`, and transport rejections still propagate to the caller as the same `ApiRequestError`, and session state is untouched.
- [ ] A visible, operable sign-out control exists in the authenticated application shell (`game-shell-overlay`), is absent from `AuthScreen`, and activating it performs the identical cleanup and returns the application to `auth-screen` in the same document.
- [ ] Activating sign-out twice cannot produce a second destructive transition, and the control cannot be fired concurrently while its cleanup is in flight.
- [ ] A user can authenticate again after sign-out **and** after 401 invalidation: a subsequent successful login/registration re-establishes the session, reconnects the authenticated transport, re-registers `ReceiveEvents` and `BattleStateUpdated`, and mounts `game-shell` again in the same document — i.e. no stale `initialized` / `disposed` / subscription state blocks the second authentication.
- [ ] The invalidation clears the three session keys only: an unrelated `localStorage` key present before the cleanup is still present after it.
- [ ] No architectural boundary moves: `tests/RuntimeBoundaries.test.ts` passes **unmodified** (the pinned `GameRuntimePort` member set is unchanged; the runtime gains no preserved-loadout carrier capability), and scenes still import no transport client.
- [ ] No backend, database, `docs/`, or ADR file is changed, and the signed-out token's documented server-side behavior is unaltered (`API_CONTRACTS.md` §2.3 "Lifecycle (MVP)"; `ADR-015` D5).
- [ ] Frontend tests pass via `npm run test:run` and type/build verification passes via `npx tsc --noEmit` in `src/frontend/client` (23 files / 917 tests passing at intake; the new cases increase both counts).
- [ ] HIGH-risk validation depth is met (`core/validation.md` §2): state-transition-level scenarios for both paths, architecture validation against `ARCHITECTURE.md` §2.2.1/§2.3 and `ADR-015`/`ADR-020`/`ADR-022`, and every `quality/review.md` §1 checklist item.
- [ ] Quality review checklist passes (`quality/review.md` §1) and the two-stage commit protocol is followed (`tasks/TASK_LIFECYCLE.md` §6): the Phase A staged set equals this record's Declared File Set exactly, and the Phase B completion record is filed.

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)
[x] src/frontend/client/ (runtime + app shell)
[ ] tests/ (unit / integration / gameplay scenarios) — frontend tests live in src/frontend/client/tests/
[ ] docs/ (documentation updates if applicable)
```

---

## Implementation Notes

- **One cleanup, two triggers.** The 401 path and the sign-out path must run the same routine; duplicating it (or giving the app a second session-state value) would create exactly the duplicate session state `AGENTS.md` §9 and `ARCHITECTURE.md` §2.2.1 rules 3/5 forbid.
- **Reuse, do not invent.** `ApplicationSession.clear()` (`ApplicationSession.ts:108`) is the only credential-clearing mechanism; `GameRuntime`'s existing `updateState` / `setSessionStatus` publication plus `App`'s existing `onRuntimeEvent` subscription (`App.tsx:77-79`) is the only state-transition mechanism; the existing `sessionStatus !== 'authenticated'` render condition (`App.tsx:119`) is the only route back to `AuthScreen`. No event bus, no store, no new status value, no fourth `localStorage` key.
- **The 401 classification boundary.** `ApiService.get`/`post` serve only session-covered endpoints (`API_CONTRACTS.md` §2.3 "Coverage"), so a `401` on that transport is the §2.3 `UNAUTHENTICATED` outcome. `ApiService.login`/`register` are separate methods and must not be touched: their `401 INVALID_CREDENTIALS` / `409 USERNAME_ALREADY_EXISTS` rejections are not session signals (`API_CONTRACTS.md` §2.2 rule 2, §2.1 rule 3). A `401` is the trigger; no other status is.
- **Apply it at the runtime's single REST boundary**, i.e. for every runtime capability that reaches a covered endpoint (`getPets`, `getPet`, `getCards`, `getRelics`, `getBattleResult`, `getBattleHistory`, `startBattle`, and the §7.3 fallback read inside `handleBattleNotRecoverable`) rather than per scene or per call site.
- **Re-authentication is the real constraint, not the clearing.** `initialize()` returns early while `initialized` is true (`GameRuntime.ts:161-164`) and `dispose()` is terminal (`:155-157, 266-270`), so a cleanup that only clears and publishes leaves a client whose second login cannot reconnect or re-register its subscriptions. Whichever mechanism is chosen inside the declared files (a re-initializable reset of the runtime, or a fresh runtime instance owned by `App`), the observable outcome in the criteria above must hold and must be proved by test — the runtime instance must not be left permanently disposed, and `GameRuntimeEvents.ts` must stay unchanged.
- **`SignalRService.connect()` early-returns on an already-`Connected` connection** (`:592-594`) and the hub authenticates with the token that built it (`:612`). Releasing the connection on invalidation/sign-out is therefore what prevents a second account from inheriting the first session's authenticated connection.
- **AuthScreen is out of bounds.** `AuthScreen.tsx` currently calls `retryConnection`-style behavior through `App` and renders the `auth-error-banner` from `handleSubmit`'s mapping chain; that suite must keep passing unchanged.
- **Control placement.** Render the control as an overlay child of `GameShell` from `App.tsx` (the overlay spans the shell and children opt into pointer events — `App.css:76-88`, `ARCHITECTURE.md` §2.2.2 rule 4), position it clear of the DEV diagnostics (top-left viewport overlay, bottom-right status card), and give it a stable `data-testid` for `AppLifecycle.test.tsx`.
- **Code-comment staleness (report, do not fix here).** `ApiService.ts` and `GameRuntime.ts` reference `API_CONTRACTS.md` "§2.8" for the session mechanism; the current document numbers it §2.3. That is pre-existing drift outside this task's declared files (`AGENTS.md` §16) — record it, do not repair it in this slice. Likewise `GameRuntimeState.ts:36-38` still describes the retired Discord boundary.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — the cleanup routine and the 401 classification at the runtime's REST boundary
[x] Integration tests  — runtime → runtime state → App render (React 18 + @testing-library/react, jsdom)
[x] Gameplay scenarios — N/A for gameplay; HIGH-risk depth is met by session state-transition scenarios
                         (Given/When/Then, quality/testing.md §3) because this task touches no game rule
                         and no server-authoritative value (AGENTS.md §10 not engaged)
```

Scenarios to express as Given/When/Then in `GameRuntime.test.ts`:
1. Given an authenticated session and a runtime holding synchronized battle state, When a runtime read rejects with `401 UNAUTHENTICATED`, Then the stored session is cleared, `session` is `unauthenticated`, `getBattleState()` is `null`, and the transport is disconnected — once.
2. Given a session already invalidated, When a second `401` arrives (including one already in flight), Then no second clear and no second disconnect occur.
3. Given an authenticated session, When a read rejects with `400 INVALID_LOADOUT` / `404` / a transport error, Then the rejection propagates unchanged and the session state is unchanged.
4. Given a session invalidated by `401`, When the player authenticates again, Then `initialize()` reconnects the transport and re-registers `ReceiveEvents` and `BattleStateUpdated`.

Scenarios in `AppLifecycle.test.tsx`:
5. Given an authenticated shell, When the sign-out control is activated, Then `auth-screen` is rendered, `game-shell` is gone, the three session keys are cleared, and the transport is disconnected.
6. Given a signed-out client, When login succeeds again, Then `game-shell` is mounted again and the transport reconnects in the same document.
7. Given an authenticated client, When a runtime read rejects with `401 UNAUTHENTICATED`, Then the application returns to `auth-screen` without a reload.
8. The existing restore-from-storage, login, register, `INVALID_CREDENTIALS` banner, and `pagehide` dispose cases remain green and unmodified.

### Key Edge Cases
- Overlapping `401`s from two in-flight reads (`API_CONTRACTS.md` §2.3 — one public code, indistinguishable cause): must collapse to a single cleanup.
- Cleanup with no session and no battle held (already-unauthenticated or never-connected client): must be safe and silent.
- `401 INVALID_CREDENTIALS` on login vs `401 UNAUTHENTICATED` on a covered endpoint (`API_CONTRACTS.md` §2.2 rule 2 vs §2.3): only the latter invalidates.
- Re-login after invalidation in the same document: the `initialized` / `disposed` / subscription guards must not block the second authentication (`GameRuntime.ts:161-164, 266-270, 968-971`).
- An unrelated `localStorage` key must survive the cleanup.
- The control must be absent while unauthenticated and must not be activatable twice.

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7 / §20.
- If implementation appears to require a server-side revocation, a logout endpoint, or any `API_CONTRACTS.md` change: STOP — `API_CONTRACTS.md` §2.3 "Lifecycle (MVP)" and `ADR-015` D5 fix revocation as absent in MVP, and only a new decision may change that.
- If implementation requires a change to `GameRuntimeEvents.ts`'s `GameRuntimePort` member set, `ApiService.ts`'s `register`/`login` behavior, `ApplicationSession.ts`, `MainMenuScene.ts`, or `App.css`: STOP and declare the additional file (and, for a port capability, re-check `tests/RuntimeBoundaries.test.ts:666-715`) before writing it.
- If the resolution of the post-login initialization failure (the excluded NG-13 case) is needed to satisfy any criterion above: STOP — that is a separate decision-gated item, not a reason to widen this task.
- If task-specific scope cannot stay within 7 skills or the declared file set: STOP & decompose per `tasks/README.md` §13.

---

## Completion Evidence

### Commit
- Implementation slice (Phase A): `06eef7957b660c1647bd9ba36536ecda3885d4ad`
- Subject: `feat: end the client session on sign-out and authenticated 401`
- Owns: `TASK-234`
- Files: `src/frontend/client/src/game/runtime/GameRuntime.ts`, `src/frontend/client/src/app/App.tsx`, `src/frontend/client/tests/GameRuntime.test.ts`, `src/frontend/client/tests/AppLifecycle.test.tsx`
- Shared with: `none`
- Unowned / pre-existing: `none`

### Changed Files
- `src/frontend/client/src/game/runtime/GameRuntime.ts` — adds the single `invalidateSession()` cleanup (canonical `ApplicationSession.clear()`, synchronized-copy drop, transport unsubscribe + `disconnect()`, `initialized = false` re-arm, and the existing `session: 'unauthenticated'` publication) and the private `throughAuthenticatedTransport` boundary that classifies a `401` on `ApiService.get`/`post`. Every covered capability now reaches the boundary (`getPets`, `getPet`, `getCards`, `getRelics`, `getBattleResult`, `getBattleHistory`, `startBattle`, and the §7.3 fallback read in `handleBattleNotRecoverable`). `GameRuntimePort` and `GameRuntimeEvents.ts` are unchanged.
- `src/frontend/client/src/app/App.tsx` — renders the explicit sign-out control as an authenticated `game-shell-overlay` child and runs the identical cleanup through an in-flight-guarded handler. The existing render condition (`session !== 'authenticated'`) and `handleAuthenticated` are unchanged, so the excluded NG-13 initialization-recovery behavior is untouched.
- `src/frontend/client/tests/GameRuntime.test.ts` — new `authenticated session invalidation` suite (10 tests) covering the four required scenarios plus overlapping-401 collapse, the endpoint-independent classification boundary, the explicit sign-out trigger, the no-session no-op, three-key-only clearing and the unchanged technical state contract. Existing tests are unmodified (only the vitest import line was extended).
- `src/frontend/client/tests/AppLifecycle.test.tsx` — new `explicit sign-out` (4 tests) and `authenticated HTTP 401 invalidation` (2 tests) suites. Existing restore-session, login, register, `INVALID_CREDENTIALS` banner and `pagehide` cases are unmodified and green (only the `disconnectSpy` capture and the `ApiService` import line changed).

### Acceptance Criteria Verification
- `401` clears the session through `ApplicationSession.clear()`; the three `dcacti_*` keys are removed — `GameRuntime.test.ts` "a \`401\` clears the session…" and "clears the three session keys only" (an unrelated key survives); `AppLifecycle.test.tsx` 401 case.
- The same `401` publishes the existing `session: 'unauthenticated'` and `App` renders `auth-screen` with no `game-shell`, in the same document — `AppLifecycle.test.tsx` 401 case (`getSharedRuntime()` identity preserved, no re-bootstrap, no reload).
- The cleanup drops the synchronized copy and releases the transport — `GameRuntime.test.ts` 401 case (`getBattleState()` null, `disconnectCalls === 1`, subscriptions detached, `connection === 'disconnected'`).
- Idempotent for repeated/overlapping `401`s — "is idempotent…" (`clear` once, disconnect once) and "collapses two overlapping \`401\`s…".
- No HTTP request of its own, no endpoint/wire/revocation added — "runs the identical cleanup for the explicit sign-out path, with no request of its own" (no `ApiService` method called, no hub method invoked).
- `401 INVALID_CREDENTIALS` / `409 USERNAME_ALREADY_EXISTS` unchanged — `AuthScreen.test.tsx` (6 tests) passes unmodified; login/register are a separate `ApiService` method pair and are never routed through the boundary.
- Non-401 failures unchanged — "leaves the session and runtime state untouched for any non-\`401\` failure" (400/404/transport error) and `AppLifecycle.test.tsx` "does not invalidate the session for a non-401 failure".
- A visible, operable sign-out control exists in `game-shell-overlay`, is absent from `AuthScreen`, and returns the app to `auth-screen` — `AppLifecycle.test.tsx` "returns to AuthScreen…" (overlay containment asserted) and "is absent from the unauthenticated presentation".
- Sign-out cannot transition twice / cannot fire concurrently — "cannot be fired concurrently…" (`invalidateSession` dispatched once, one disconnect).
- Re-authentication after sign-out **and** after `401` — `AppLifecycle.test.tsx` "signs out and logs in again in the same document" and the 401 case (second login mounts `game-shell` again); `GameRuntime.test.ts` "re-initializes after invalidation…" (reconnect, `ReceiveEvents` / `BattleStateUpdated` re-registered and live).
- Only the three session keys are cleared — "clears the three session keys only".
- No architectural boundary moves — `tests/RuntimeBoundaries.test.ts` passes unmodified (54 tests); scenes still import no transport.
- No backend / database / `docs/` / ADR file changed — Phase A touched only the four declared files; `git status` shows no other path.
- `npm run test:run` and `npx tsc --noEmit` pass with increased counts — see below.
- HIGH-risk validation depth — see below (`core/validation.md` §2: state-transition scenarios, architecture validation, full `quality/review.md` §1 checklist).
- `quality/review.md` §1 passes and the two-stage protocol is followed — review disposition `Pass`; Phase A staged set equalled the Declared File Set exactly (4 `M` paths), and this Phase B record is filed.

### Validation Results
- `npx vitest run tests/GameRuntime.test.ts tests/AppLifecycle.test.tsx tests/RuntimeBoundaries.test.ts` in `src/frontend/client` — PASS (211 tests: 144 + 13 + 54)
- `npm run test:run` in `src/frontend/client` — PASS (23 files, 933 tests; 917 at intake + 16 new)
- `npx tsc --noEmit` in `src/frontend/client` — PASS (0 errors)
- `npm run build` in `src/frontend/client` — PASS (82 modules transformed, `dist/` is ignored)
- `tests/RuntimeBoundaries.test.ts` — PASS, run **unmodified**
- State-transition scenarios (Given/When/Then, `quality/testing.md` §3): the four `GameRuntime.test.ts` scenarios and the four `AppLifecycle.test.tsx` scenarios required by the manifest are implemented; no gameplay rule or server-authoritative value is involved, so `AGENTS.md` §10 is not engaged.

### Review (quality/review.md §1) — Disposition: Pass
- Correctness: matches `ADR-020` D4 item 2 / D5, `API_CONTRACTS.md` §1/§2.3/§6 and `ADR-015` D5 (client-side release only; the token keeps its documented server-side validity).
- Architecture: `ARCHITECTURE.md` §2.2.1 rules 1/3/5/6 hold (scenes still use the port; no new runtime state member; one runtime, one connection); §2.2.2 rule 4 holds (overlay child opts into pointer events); §2.3 item 5 holds (no coupling between authentication presentation and any game system).
- Scope: only the four declared files changed; no refactor, no unrelated "improvement", no doc/ADR/backend/contract change.
- Tests: high-risk depth met; test expectations trace to the cited documents, not to the implementation (see Regression Guard Evidence).
- Documentation: no update required — the task's Out of Scope excludes `docs/`/ADR change, and no implemented behavior contradicts a document. Reported observations below.
- Security: no token is logged, echoed, or persisted beyond the existing three keys; the authenticated connection is released, so a second account cannot inherit the first session's connection. No new exposure.
- Performance: the boundary adds a `try`/`catch` around existing awaits; no new I/O, query, or polling.
- Maintainability: one private helper plus one public method; no interface, factory, event bus, store, or new status value (`AGENTS.md` §9).
- Determinism: n/a — no gameplay/battle value is touched (`AGENTS.md` §11 not engaged).

### Regression Guard Evidence
- Removing the `401` cleanup call from the boundary (`GameRuntime.ts`) and short-circuiting the sign-out handler (`App.tsx`) made exactly 10 of the 16 new tests fail (6 in `GameRuntime.test.ts`, 4 in `AppLifecycle.test.tsx`) while all other tests stayed green.
- Removing only the control's in-flight guard in `App.tsx` made exactly 1 test fail (`cannot be fired concurrently…`), proving the control-level guard is exercised and not merely the runtime's idempotency.
- Both source files were restored byte-identically after each mutation (SHA-256 verified), and the full suite was re-run green.

### Out-of-Scope Observations (reported, not fixed — `AGENTS.md` §16)
- Pre-existing comment drift, as the manifest anticipated: `ApiService.ts` and `GameRuntime.ts` refer to `API_CONTRACTS.md` "§2.8" for the session mechanism (the document now numbers it §2.3); `GameRuntimeState.ts:36-38` still describes the retired Discord boundary; `GameRuntime.setSessionStatus`'s doc comment cites `ADR-007` (superseded by `ADR-020`). Left untouched per this manifest's instruction.
- `ARCHITECTURE.md` §2.2.1 does not name the session-cleanup capability that now exists alongside `clearActiveBattleState`; documenting it would be a `docs/` change this task explicitly excludes.
- `.ai/workflow/quality/review.md` §1 Security cites "ADR-007's open item"; ADR-007 is superseded by ADR-020. Pre-existing `.ai/`-layer drift, outside the declared set.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed P-1…P-6 commit policy compliance

