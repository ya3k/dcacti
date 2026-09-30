# TASK-086 — Establish the Client Application Session Before SignalR Connect

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and src/ by path and section; copies only what an
  implementer needs to know where to look.

  This is the "client application-session establishment" follow-up recorded by
  TASK-077 (Out of Scope + Remaining Issues) and TASK-078 (Remaining Issues):
  the production wiring of Discord authorization code ->
  ApiService.authenticateDiscord -> ApplicationSession -> GameRuntime session
  status -> SignalR connect, in the documented order.
-->

---

## Metadata

```text
Task ID:           TASK-086
Type:              FEATURE (the behavior is specified by TDD.md §2.1 item 4 /
                   API_CONTRACTS.md §2; only the production client wiring is
                   missing — tasks/TASK_TYPES.md §2)
Status:            DONE
Risk:              HIGH (authentication/security change —
                   .ai/workflow/core/task-intake.md §3; validation depth per
                   .ai/workflow/core/validation.md §2)
Priority:          CRITICAL (until this lands, every production REST call and
                   the hub connection run without the documented session —
                   API_CONTRACTS.md §2.8 failure behavior `401 UNAUTHENTICATED`;
                   recorded by TASK-077 and TASK-078 Remaining Issues 1)
Primary Agent:     client
Supporting Agents: realtime (hub access-token connection authentication),
                   testing (HIGH-depth scenario coverage),
                   review (authority / scope verification)
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, client/react-phaser-boundary,
                   client/client-state-authority, backend/api-contract-validation,
                   realtime/realtime-protocol-validation,
                   quality/architecture-conformance,
                   testing/test-scenario-generation
Dependencies:      TASK-034, TASK-035, TASK-054, TASK-055, TASK-069, TASK-075,
                   TASK-076, TASK-077, TASK-078
```

---

## Objective

Wire the documented production application-session path in the React shell so
that the client acquires a Discord authorization code
(`DiscordService.getAuthorizationCode`), exchanges it for the application
session (`ApiService.authenticateDiscord`, which records it in
`ApplicationSession`), records the session state on the runtime
(`GameRuntime.setSessionStatus`), and only **then** opens the SignalR
connection — i.e. `TDD.md` §2.1 item 4 ("SignalR Hub connections are
established only *after* the application session is authenticated"),
`ARCHITECTURE.md` §2.3 items 3–4, `SIGNALR_PROTOCOL.md` §1 item 3, and
`API_CONTRACTS.md` §2 / §2.8. If authentication fails at any step, no
connection is opened and the failure is surfaced as runtime state, with no
new auth mechanism, no backend change, and no client-held secret.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Discord Activity + authenticated
  client are part of the IN-scope MVP client shell
- `docs/02-technical/TDD.md` §2.1 (Discord Embedded App SDK & Authentication
  Architecture, items 1–5) — SDK ownership, Client Secret boundary, the
  session-before-SignalR sequence (item 4), separation of concerns (item 5)
- `docs/02-technical/TDD.md` §2.1 (React Responsibilities & Boundaries) —
  React owns the application shell and the Discord Activity integration
  boundary; §2.1 (Service Boundaries) — `services/discord/`,
  `services/realtime/`, `services/api/` isolation
- `docs/02-technical/API_CONTRACTS.md` §2 (items §2.1–§2.8) — the exchange
  endpoint contract; §2.8 — session transport, coverage, failure behavior,
  MVP lifecycle (no refresh / no revocation)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §1 (items 3–5) — connection
  authenticated with the application session via SignalR's standard
  access-token mechanism; rejection before the hub
- `docs/02-technical/ARCHITECTURE.md` §2.3 (items 1–5) — SDK location,
  Client Secret security, authentication boundary, SignalR authentication,
  architectural isolation; §2.2.1 — one runtime, one connection
- `docs/03-decisions/ADR/ADR-007-discord-activity-authentication.md` — why
  the Discord Activity authentication architecture exists
- `docs/03-decisions/ADR/ADR-013-discord-identity-exchange-contract.md` —
  identity exchange contract behind `POST /api/auth/discord`
- `docs/03-decisions/ADR/ADR-015-application-session-authentication-contract.md`
  — session decisions (D3/D4 holder + transport, D5 lifecycle, D10 secret
  boundary) and their rationale
- `AGENTS.md` §10 (server-authoritative), §13 (frontend/transport boundary),
  §16 (task discipline), §20 (stop conditions)

---

## Scope

### In Scope

- Production orchestration in the React shell (`src/frontend/client/src/app/App.tsx`,
  which documents itself as owner of the `GameRuntime` lifecycle and the
  Discord Activity SDK integration boundary), executing in order:
  1. `DiscordService.getInstance().initialize()` — SDK ready
     (`DiscordService.ts:26`);
  2. `DiscordService.getAuthorizationCode()` (`DiscordService.ts:56`) →
     authorization code or `null`;
  3. `ApiService.getInstance().authenticateDiscord(code)`
     (`ApiService.ts:75`) — `POST /api/auth/discord`, which already records
     the session via `ApplicationSession.establish` (`ApiService.ts:90`);
  4. `GameRuntime.setSessionStatus('authenticated')`
     (`GameRuntime.ts:272`);
  5. only then `GameRuntime.initialize(BATTLE_HUB_URL)` (`GameRuntime.ts:122`,
     connect at `:192`) — the existing mount call at `App.tsx:62` moves behind
     this sequence.
- Session status transitions using only the existing `SessionStatus` values
  (`src/frontend/client/src/state/GameRuntimeState.ts:40-44`:
  `unauthenticated` → `authenticating` → `authenticated`, or `error`), recorded
  exclusively through `setSessionStatus`.
- Failure semantics: SDK unavailable, `authorize()` returning `null`
  (`DiscordService.ts:70-72`), non-2xx/network failure from
  `authenticateDiscord` (`ApiService.ts:84-87`) → session status ends
  non-authenticated (`error`), **no** SignalR connect is attempted, the shell
  still renders, and the failure is surfaced as runtime state (existing
  graceful-failure pattern, `App.tsx:58-61`).
- Idempotency across StrictMode effect re-invocation: exactly one
  authorization/exchange sequence and at most one transport connection
  (existing guarantee `App.tsx:44-49`, `ARCHITECTURE.md` §2.2.1 rule 6).
- Test updates required by this ordering: `tests/AppLifecycle.test.tsx`
  currently asserts connect-at-mount (`:75`, `:97`); update it to the
  documented ordering as a cited stage advance (`AGENTS.md` §15) — the
  "no duplicate connection" property itself must not be weakened — plus new
  unit/integration coverage for ordering, status transitions, and every
  failure path.
- Session-bearing test setup for the app-level tests, using the existing
  precedent of establishing a session directly (tests `BattleService.test.ts`,
  `ApplicationSession.test.ts:109`).

### Out of Scope

- Any backend change (`AuthController.cs`, `POST /api/auth/discord` behavior,
  token issuance) and any change to `API_CONTRACTS.md`, `SIGNALR_PROTOCOL.md`,
  `TDD.md`, `ARCHITECTURE.md`, or any ADR — docs describe this behavior
  already; only code is missing.
- Any SignalR protocol change and any new token mechanism:
  `SignalRService.ts:358` already supplies the session via
  `accessTokenFactory`; it is reused as-is.
- **A local / non-iframe development session flow** — none is documented
  (`tasks/completed/TASK-077…md:150`); this task invents no bypass, mock
  session, or alternate credential (report-only, see Stop Conditions).
- Client Secret handling of any kind; persisting `sessionToken` to storage;
  refresh, revocation, or logout flows (`ADR-015` D5, `API_CONTRACTS.md`
  §2.8 Lifecycle).
- `tasks/completed/*` (incl. TASK-077, TASK-078) and `tasks/backlog/TASK-079*`
  modifications — other than the test assertions in `tests/AppLifecycle.test.tsx`
  that this task's own ordering changes, each edit must be a cited stage
  advance, never a weakened property (`AGENTS.md` §15).
- Phaser scenes, loadout/lobby selection, gameplay logic, new hub
  methods/events, reconnection/resync semantics (`ADR-008`),
  session-expiry re-authentication (no documented flow).
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State

Every building block exists in isolation and none of them is wired:
`DiscordService.getAuthorizationCode` (`DiscordService.ts:56`) has no
production caller (only `ui/components/StatusOverlay.tsx:102` reads its
context), `ApiService.authenticateDiscord` (`ApiService.ts:75`) has no
production caller, `ApplicationSession.establish`/`isAuthenticated`
(`ApplicationSession.ts:60`/`:79`) is populated only by tests, and
`GameRuntime.setSessionStatus` (`GameRuntime.ts:272`) is never called, so
`session` stays at its initial `'unauthenticated'`
(`GameRuntimeState.ts:91`). Meanwhile `App.tsx:62` calls
`activeRuntime.initialize(BATTLE_HUB_URL)` at mount, connecting the transport
before any session exists — the pre-existing divergence from `TDD.md` §2.1
item 4 recorded by TASK-077 (`:61`, `:233-238`) and TASK-078 (`:436-440`).
REST calls already attach the bearer (`ApiService.ts:108`, `:246`) and the
transport already has the access-token factory (`SignalRService.ts:358`), so
the only missing link is the ordering above.

---

## Acceptance Criteria

- [x] In an iframe with successful SDK init, `authorize()` and
      `POST /api/auth/discord`, `ApplicationSession.isAuthenticated()` is true
      **before** the first hub-connection attempt; the ordering is asserted by
      a test, satisfying `TDD.md` §2.1 item 4.
- [x] `GameRuntime` session state transitions
      `unauthenticated → authenticating → authenticated` on success, and ends
      `error` on any failure, using only existing `SessionStatus` values and
      only the existing `setSessionStatus` setter.
- [x] When authentication fails at any step (SDK unavailable, `authorize()`
      returns `null`, non-2xx or network failure), no `SignalRService.connect`
      is invoked, `connection` remains `disconnected`, and the shell renders
      without crashing.
- [x] StrictMode double-invocation still results in exactly one
      authorization/exchange sequence and at most one connection
      (`tests/AppLifecycle.test.tsx` existing properties preserved).
- [x] The connection still authenticates with the same session JWT through the
      existing `accessTokenFactory` (`SignalRService.ts:358`) — no second
      credential path, no Discord access token on the hub
      (`API_CONTRACTS.md` §2.7 item 4, `SIGNALR_PROTOCOL.md` §1 item 4).
- [x] No Client Secret and no `sessionToken` persistence: the token stays
      in-memory in `ApplicationSession` (`ADR-015` D5/D10,
      `ApplicationSession.ts:14-18`).
- [x] The Phaser-side guard still passes: no scene references `DiscordService`,
      `services/discord`, or `ApiService` (`tests/LobbyScene.test.ts:875`).
- [x] Zero backend, docs, ADR, API-contract, and SignalR-protocol changes;
      zero client-authoritative gameplay logic (`AGENTS.md` §10).
- [x] All relevant tests pass at HIGH validation depth
      (`.ai/workflow/core/validation.md` §2: build/unit/integration +
      architecture validation + full `quality/review.md` checklist).
- [x] Quality review checklist passes (`quality/review.md` §1); no
      authoritative rule or contract violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[ ] src/backend/                                      — NOT touched (endpoint already exists)
[x] src/frontend/client/src/app/App.tsx              — auth orchestration + connect ordering
[ ] src/frontend/client/src/game/runtime/GameRuntime.ts — only if a guard is added; setSessionStatus exists
[ ] src/frontend/client/src/services/(discord|api)   — reused as-is; no contract change
[x] src/frontend/client/tests/AppLifecycle.test.tsx  — updated ordering assertions + new auth-path coverage
[ ] src/frontend/client/tests/ (new/extended)        — failure-path and status-transition tests
[ ] tests/                                           — covered by the client test suite above
[ ] docs/                                            — NOT touched (docs already specify this behavior)
```

---

## Implementation Notes

- Entry point is the `useEffect` at `App.tsx:53-71`; the current unconditional
  call is `App.tsx:62`. React owns this boundary (`TDD.md` §2.1 React
  Responsibilities; `App.tsx` header comment), and `TDD.md` §2.1 item 5
  requires the chain Discord SDK → `DiscordService` → application session →
  REST/SignalR, so the orchestration belongs here, not in Phaser and not in
  `GameRuntime` (which only records status — `GameRuntime.ts:268-272`).
- Reuse, do not extend: `DiscordService.initialize/getAuthorizationCode`,
  `ApiService.authenticateDiscord` (already calls
  `ApplicationSession.establish`), `ApplicationSession.getAuthorizationHeader`,
  `SignalRService` `accessTokenFactory`.
- Enforce the ordering at the orchestration point; if an explicit guard inside
  `GameRuntime.initialize` is chosen instead/in addition, keep it minimal and
  do not alter `startBattle`'s connect path (`GameRuntime.ts:474`, TASK-077),
  which is only reachable after a session-gated REST call.
- Keep the existing idempotency story intact: module-scoped runtime +
  idempotent `initialize` (`App.tsx:44-49`); the auth sequence needs the same
  once-only treatment under StrictMode.
- Test precedent for establishing a session directly:
  `tests/BattleService.test.ts`, `tests/ApplicationSession.test.ts:109`
  (`vi` mocks of `DiscordService`/`fetch` for app-level tests; phaser is
  already mocked in `tests/setup.ts`).
- `StatusOverlay` needs no change: it reads `DiscordService` context and
  connection state only (`ui/components/StatusOverlay.tsx:24`, `:102`).

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — orchestration order (auth before connect), SessionStatus
                         transitions, each failure path returning "no connect",
                         StrictMode idempotency of the auth sequence
[x] Integration tests  — App render with mocked DiscordService + auth fetch:
                         success opens exactly one connection after
                         isAuthenticated(); 401/non-2xx/network failure and
                         SDK-unavailable open none
[x] Gameplay scenarios — N/A, justified: this task changes only technical
                         session/connection state, no gameplay rule or
                         GameplayState (GameRuntimeState.ts:4-5 scope note);
                         HIGH depth is met by integration + architecture
                         validation + full review instead
```

### Key Edge Cases
- SDK unavailable outside the Discord iframe (`DiscordService.ts:30-36`,
  existing "Local Development Mode" context) → failure path, no connect; see
  Stop Conditions for the undocumented local session question.
- `authorize()` throws → `null` (`DiscordService.ts:70-72`) → failure path.
- `authenticateDiscord` non-2xx/network throw (`ApiService.ts:84-87`) →
  failure path; no partial session (`ApplicationSession` stays cleared).
- Re-render/hot-reload with an already-established session → no second
  authorize/exchange storm; ordering still holds.
- Session reaches absolute expiry (24h, `API_CONTRACTS.md` §2.8 Lifecycle)
  mid-session → no documented re-auth flow exists; observe and report, do not
  invent one.

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References:
  STOP per `AGENTS.md` §7
- If task requires client-authoritative game logic calculation: STOP per
  `AGENTS.md` §10
- If task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: STOP & decompose
- If implementation requires a **local/non-iframe session flow** (a way for
  non-Discord-iframe clients to obtain a session): STOP per `AGENTS.md`
  §7/§20 — no such flow is documented (`tasks/completed/TASK-077…md:150`);
  report the requirement instead of inventing a bypass or mock credential
- If any criterion requires token persistence, refresh, revocation, a new
  credential type, or exposing the Client Secret to the frontend: STOP per
  `ADR-015` D5/D10, `TDD.md` §2.1 item 3, `AGENTS.md` §18
- If any criterion requires a new hub method/event, a protocol change, or a
  second token mechanism: STOP per `AGENTS.md` §7/§18
- If satisfying any criterion requires editing `tasks/completed/*`,
  `tasks/backlog/TASK-079*`, or the backend/docs/ADRs: STOP per `AGENTS.md`
  §16/§17/§18 — report instead

---

## Completion Evidence

### Changed Files
- `src/frontend/client/src/app/App.tsx` (auth orchestration + connect ordering)
- `src/frontend/client/tests/AppLifecycle.test.tsx` (updated ordering assertions + new auth-path coverage)

### Validation Results
- Targeted: `npx vitest run tests/AppLifecycle.test.tsx` (13 passed)
- Frontend Suite: `npx vitest run` (17 test files, 407 passed)
- Type Check: `npx tsc --noEmit` (0 errors)
- Production Build: `npx vite build` (clean build)
- Architecture & Boundaries: `tests/RuntimeBoundaries.test.ts` (37 tests passed)

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
