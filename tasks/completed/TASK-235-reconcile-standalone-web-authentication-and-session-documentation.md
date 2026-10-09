# TASK-235 — Reconcile Standalone Web Authentication & Session Documentation Across Technical Specs, ADRs, and Client Source Comments

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  Lifecycle: BACKLOG → READY completed 2026-10-09 by the orchestrator role at
  the requester's direction, after validating the tasks/TASK_LIFECYCLE.md §3
  READY criteria:
    [x] Task type confirmed (DOCUMENTATION — tasks/TASK_TYPES.md / documentation/documentation-change.md)
    [x] Relevant documentation exists in docs/ (MVP_SCOPE.md §1, ADR-015, ADR-020, API_CONTRACTS.md §2.1–§2.3, ARCHITECTURE.md §2.2.1)
    [x] MVP scope confirmed (MVP_SCOPE.md §1 — standalone web account)
    [x] Not blocked: Dependencies: TASK-234 (DONE in tasks/completed/)
    [x] Primary agent assigned (review) and workflow assigned (documentation/documentation-change.md)
    [x] Acceptance criteria are binary and testable
    [x] Uniqueness: highest assigned ID across repository is TASK-234; no other record names TASK-235
    [x] Ownership: tasks/active/, tasks/backlog/ (other than this record), and
        tasks/blocked/ hold no open record naming the declared file set
-->

<!--
  Lifecycle: READY → IN PROGRESS → IN REVIEW → DONE completed 2026-10-09 by the
  review agent under documentation/documentation-change.md: the 41 declared files
  were reconciled (stale API_CONTRACTS.md §2.4–§2.8/ADR-007 citations, Discord-era
  descriptions, §2.3 standalone-web text, ARCHITECTURE.md §2.2.1
  invalidateSession(), .ai/ ADR-007 open-item guidance), the preserve list and
  executable test logic were verified untouched, both zero-regression suites
  passed, and quality/review.md §1 returned Pass, then the two-stage commit
  protocol (TASK_LIFECYCLE.md §6) — Phase A
  3aa39302119885595e76ade069247bed46050f7b and this Phase B filing.
-->

---

## Metadata

```text
Task ID:           TASK-235
Type:              DOCUMENTATION
Status:            DONE
Risk:              LOW
Priority:          MEDIUM
Primary Agent:     review
Supporting Agents: backend, client
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis, quality/documentation-consistency, quality/implementation-review
Dependencies:      TASK-234
Declared Files:    docs/02-technical/API_CONTRACTS.md, docs/02-technical/ARCHITECTURE.md, docs/02-technical/DATABASE.md, docs/02-technical/TDD.md, .ai/workflow/quality/review.md, .ai/skills/backend/api-contract-validation.md, .ai/skills/quality/implementation-review.md, src/frontend/client/src/state/GameRuntimeState.ts, src/frontend/client/src/game/GameConfig.ts, src/frontend/client/src/game/GameViewport.ts, src/frontend/client/src/app/App.css, src/frontend/client/src/game/runtime/GameRuntime.ts, src/frontend/client/src/services/api/ApplicationSession.ts, src/frontend/client/src/services/api/ApiService.ts, src/frontend/client/src/services/api/BattleModels.ts, src/frontend/client/src/game/scenes/LobbyScene.ts, src/backend/GameServer.Api/Program.cs, src/backend/GameServer.Api/Authentication/ApplicationSessionTokenService.cs, src/backend/GameServer.Api/Authentication/ApplicationSessionAuthentication.cs, src/backend/GameServer.Api/Controllers/BattleController.cs, src/backend/GameServer.Api/Controllers/CollectionController.cs, src/backend/GameServer.Api/Hubs/BattleHub.cs, src/backend/GameServer.Application/Battle/IBattleResultRepository.cs, src/backend/GameServer.Application/Battle/BattleResultQueryService.cs, src/backend/GameServer.Application/Collection/CollectionQueryService.cs, src/frontend/client/tests/CollectionService.test.ts, src/frontend/client/tests/BattleService.test.ts, src/frontend/client/tests/LobbyScene.test.ts, src/frontend/client/tests/GameRuntime.test.ts, tests/backend/GameServer.Api.Tests/ApplicationSessionConfigurationTests.cs, tests/backend/GameServer.Api.Tests/ApplicationSessionSignalRTests.cs, tests/backend/GameServer.Api.Tests/ApplicationSessionRESTTests.cs, tests/backend/GameServer.Api.Tests/BattleHistoryEndpointTests.cs, tests/backend/GameServer.Api.Tests/BattleResultEndpointTests.cs, tests/backend/GameServer.Api.Tests/BattleStartSmokeTest.cs, tests/backend/GameServer.Api.Tests/CollectionEndpointTests.cs, tests/backend/GameServer.Api.Tests/RedisBattleStateSmokeTest.cs, tests/backend/GameServer.Api.Tests/RedisBattleRecoverySmokeTest.cs, tests/backend/GameServer.Api.Tests/TestApplicationSession.cs, tests/backend/GameServer.Api.Tests/Hubs/BattleHubReconnectRecoveryTests.cs, tests/backend/GameServer.Application.Tests/CollectionQueryServiceTests.cs
```

---

## Objective

Reconcile verified stale references and descriptions introduced by the transition from Discord Activity authentication to standalone web authentication and the session invalidation lifecycle established in TASK-234. Update technical documentation (`API_CONTRACTS.md`, `ARCHITECTURE.md`, `DATABASE.md`, `TDD.md`), `.ai/` quality guidance and skills, and source/test comments across frontend and backend to align citations with `ADR-020`, `ADR-015`, and `API_CONTRACTS.md §2.3` with zero runtime behavior changes and zero test assertion modifications.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Confirm feature baseline and documentation alignment scope
- `docs/03-decisions/ADR/ADR-020-standalone-web-account-authentication.md` — Authoritative decision for standalone web authentication (username/password), retirement of Discord Activity dependencies, session token `localStorage` persistence, and client-side sign-out / session teardown (D4 item 2)
- `docs/03-decisions/ADR/ADR-015-application-session-authentication-contract.md` — Authoritative decision for JWT ApplicationSession token mechanism, claims, stateless validation, and security configuration
- `docs/02-technical/API_CONTRACTS.md` §2.1–§2.3 — Authoritative contracts for `/api/auth/register`, `/api/auth/login`, and Application Session Mechanism (including unchanged server-side `Lifecycle (MVP) — revocation: none` semantics)
- `docs/02-technical/ARCHITECTURE.md` §2.2.1 — Authoritative client game runtime coordination boundary (client-local coordination capabilities)

---

## Scope

### In Scope
- **API Contract Cross-References:** Update stale references pointing to former `API_CONTRACTS.md §2.8` and verified stale references pointing to former `API_CONTRACTS.md §2.7` (such as §2.7 item 4) across declared files in technical documentation (`API_CONTRACTS.md`, `DATABASE.md`), frontend source comments (`ApplicationSession.ts`, `ApiService.ts`, `BattleModels.ts`, `LobbyScene.ts`, `GameRuntime.ts`), backend source comments (`Program.cs`, `BattleController.cs`, `CollectionController.cs`, `BattleHub.cs`, `IBattleResultRepository.cs`, `BattleResultQueryService.cs`, `CollectionQueryService.cs`, `ApplicationSessionTokenService.cs`, `ApplicationSessionAuthentication.cs`), and automated test comments (including `BattleHubReconnectRecoveryTests.cs` around line 29 and line 587, `ApplicationSessionSignalRTests.cs` around line 147, and `ApplicationSessionRESTTests.cs` around lines 300, 306, 572) to point to current `API_CONTRACTS.md §2.3`.
- **API Contract Section 2.3 Reconciliation:** Reconcile `API_CONTRACTS.md §2.3` text to describe standalone web account identity exchange (`ADR-020`) and stateless JWT carriage (`ADR-015`) instead of obsolete Discord access token / DiscordUserId exchange narratives and stale §2.7 pointers. Server-side lifecycle semantics (`Lifecycle (MVP) — revocation: none`) remain strictly unchanged; do not claim §2.3 mandates client invalidation after HTTP 401.
- **Discord-Era Description Reconciliation:** Reconcile stale descriptions in declared files (`GameRuntimeState.ts`, `GameConfig.ts`, `GameViewport.ts`, `App.css`, and `TDD.md`) that describe the application as a Discord Activity iframe or embedded client to accurately state standalone web architecture.
- **Session Establishment Citations:** Update `GameRuntime.setSessionStatus` documentation in `GameRuntime.ts` to cite accepted decisions `ADR-020` and `ADR-015` instead of superseded `ADR-007`.
- **Architecture Documentation Addition:** Document `invalidateSession(): Promise<void>` in `ARCHITECTURE.md §2.2.1` as a client-local coordination capability alongside the existing `clearActiveBattleState(): void` runtime capability, citing `ADR-020` D4 item 2 as the authority for client-side session persistence and teardown/sign-out, and explaining that it coordinates local state cleanup on sign-out and on receiving an authenticated HTTP 401 UNAUTHENTICATED response without altering server-side revocation semantics.
- **`.ai/` Quality Review and Skill Guidance:** Update obsolete "ADR-007 open item" references in declared `.ai/` files (`.ai/workflow/quality/review.md`, `.ai/skills/backend/api-contract-validation.md`, and `.ai/skills/quality/implementation-review.md`) to reflect closed session authentication decisions under `ADR-020` and `ADR-015`.

### Out of Scope
- Any production code change or runtime behavior modification.
- Modifying authentication or session runtime behavior or server-side revocation semantics (`revocation: none` remains authoritative; no server-side revocation store or logout endpoint).
- API endpoint, schema, SignalR protocol, or database structure modifications.
- Product Owner decisions such as NG-13 post-login initialization recovery or in-battle forfeit/abandonment.
- Rewriting historical ADR files or altering historical decisions.
- Modifying test assertions, test fixtures, or executable test logic (comment/documentation updates only; executable test logic in `LobbyScene.test.ts` around line 1682 and the `forbidden` array around lines 1689–1700 must remain strictly untouched).
- Modifying any files outside the 41 declared files (see cautionary notes in Implementation Notes).
- Unrelated documentation or code cleanup.

---

## Current State

Following the adoption of `ADR-020` (standalone web account authentication) and `TASK-234` (sign-out and HTTP 401 session invalidation), the authentication endpoints reside at `API_CONTRACTS.md §2.1` (`/api/auth/register`), `§2.2` (`/api/auth/login`), and `§2.3` (Application Session Mechanism). However, multiple technical specifications, `.ai/` review guidelines, frontend and backend source comments, and test comments still cite the pre-ADR-020 section numbers `§2.8` and `§2.7`, reference superseded Discord Activity iframe concepts, or refer to session authentication as an "open item in ADR-007". Furthermore, client-local session coordination (`invalidateSession()`) needs concise documentation in `ARCHITECTURE.md §2.2.1` under `ADR-020` D4 item 2, maintaining a strict boundary with server-side revocation (`revocation: none`).

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-235 (exactly 41 files):
```text
docs/02-technical/API_CONTRACTS.md
docs/02-technical/ARCHITECTURE.md
docs/02-technical/DATABASE.md
docs/02-technical/TDD.md
.ai/workflow/quality/review.md
.ai/skills/backend/api-contract-validation.md
.ai/skills/quality/implementation-review.md
src/frontend/client/src/state/GameRuntimeState.ts
src/frontend/client/src/game/GameConfig.ts
src/frontend/client/src/game/GameViewport.ts
src/frontend/client/src/app/App.css
src/frontend/client/src/game/runtime/GameRuntime.ts
src/frontend/client/src/services/api/ApplicationSession.ts
src/frontend/client/src/services/api/ApiService.ts
src/frontend/client/src/services/api/BattleModels.ts
src/frontend/client/src/game/scenes/LobbyScene.ts
src/backend/GameServer.Api/Program.cs
src/backend/GameServer.Api/Authentication/ApplicationSessionTokenService.cs
src/backend/GameServer.Api/Authentication/ApplicationSessionAuthentication.cs
src/backend/GameServer.Api/Controllers/BattleController.cs
src/backend/GameServer.Api/Controllers/CollectionController.cs
src/backend/GameServer.Api/Hubs/BattleHub.cs
src/backend/GameServer.Application/Battle/IBattleResultRepository.cs
src/backend/GameServer.Application/Battle/BattleResultQueryService.cs
src/backend/GameServer.Application/Collection/CollectionQueryService.cs
src/frontend/client/tests/CollectionService.test.ts
src/frontend/client/tests/BattleService.test.ts
src/frontend/client/tests/LobbyScene.test.ts
src/frontend/client/tests/GameRuntime.test.ts
tests/backend/GameServer.Api.Tests/ApplicationSessionConfigurationTests.cs
tests/backend/GameServer.Api.Tests/ApplicationSessionSignalRTests.cs
tests/backend/GameServer.Api.Tests/ApplicationSessionRESTTests.cs
tests/backend/GameServer.Api.Tests/BattleHistoryEndpointTests.cs
tests/backend/GameServer.Api.Tests/BattleResultEndpointTests.cs
tests/backend/GameServer.Api.Tests/BattleStartSmokeTest.cs
tests/backend/GameServer.Api.Tests/CollectionEndpointTests.cs
tests/backend/GameServer.Api.Tests/RedisBattleStateSmokeTest.cs
tests/backend/GameServer.Api.Tests/RedisBattleRecoverySmokeTest.cs
tests/backend/GameServer.Api.Tests/TestApplicationSession.cs
tests/backend/GameServer.Api.Tests/Hubs/BattleHubReconnectRecoveryTests.cs
tests/backend/GameServer.Application.Tests/CollectionQueryServiceTests.cs
```

*(Note: The task manifest file itself is excluded from the implementation declared file set. The list above must strictly match the `Declared Files:` field in `## Metadata`.)*

---

## Acceptance Criteria

- [ ] Stale references pointing to former `API_CONTRACTS.md §2.8` and verified stale references pointing to former `API_CONTRACTS.md §2.7` (such as §2.7 item 4) across declared files in technical documentation (`API_CONTRACTS.md`, `DATABASE.md`), frontend source comments, backend source comments, and test comments (specifically including `BattleHubReconnectRecoveryTests.cs` around line 29) are updated to `API_CONTRACTS.md §2.3`.
- [ ] Valid, non-stale references within the declared file set (including historical version header changelog notes in `API_CONTRACTS.md`, `GAME_STATE.md §2.8` citations in `DATABASE.md`, `GameRuntime.ts`, `BattleModels.ts`, `BattleHub.cs`, `BattleService.test.ts`, `ApplicationSessionRESTTests.cs`, `RedisBattleStateSmokeTest.cs`, `RedisBattleRecoverySmokeTest.cs`, and `BattleHubReconnectRecoveryTests.cs`, `RELIC_RULES.md §2.3` citations in `LobbyScene.test.ts`, and `SIGNALR_PROTOCOL.md §3.2.8` citations in `BattleHub.cs`) remain strictly unmodified.
- [ ] `API_CONTRACTS.md §2.3` accurately describes standalone web account credential issuance (`ADR-020`) and stateless JWT carriage (`ADR-015`), removing obsolete Discord token exchange statements and stale §2.7 pointers, while keeping server-side `Lifecycle (MVP) — revocation: none` semantics unchanged (with no claim that §2.3 mandates client invalidation after HTTP 401).
- [ ] `ARCHITECTURE.md §2.2.1` documents `invalidateSession(): Promise<void>` as a client-local coordination capability alongside `clearActiveBattleState(): void`, citing `ADR-020` D4 item 2 as the authority for client-side session persistence and teardown/sign-out, and explaining that it coordinates local cleanup on sign-out and on receiving an authenticated HTTP 401 UNAUTHENTICATED response without altering server revocation semantics.
- [ ] Discord-era descriptions in declared files (`GameRuntimeState.ts`, `GameConfig.ts`, `GameViewport.ts`, `App.css`, and `TDD.md`) accurately describe the standalone web browser application.
- [ ] `GameRuntime.setSessionStatus` documentation in `GameRuntime.ts` cites accepted decisions `ADR-020` and `ADR-015` for session status management.
- [ ] Obsolete "ADR-007 open item" citations in declared `.ai/` files (`.ai/workflow/quality/review.md`, `.ai/skills/backend/api-contract-validation.md`, and `.ai/skills/quality/implementation-review.md`) are updated to reflect accepted decisions `ADR-020` and `ADR-015`.
- [ ] No production logic, runtime behavior, API schemas, SignalR protocols, or test assertions are modified. Executable test logic (including `LobbyScene.test.ts` around line 1682 and the `forbidden` array around lines 1689–1700) remains strictly untouched.
- [ ] All existing automated test suites pass with zero regressions (`npm run test:run` in `src/frontend/client/`, `dotnet test src/backend/GameServer.sln`). Test suites are run strictly to verify zero regressions, not to regenerate or alter tests, consistent with the documentation-change workflow.
- [ ] Staged Phase A implementation files match strictly the Declared File Set of exactly 41 files without extraneous files (`TASK_LIFECYCLE.md` §6 P-5).

---

## Affected Files & Areas

```text
[x] src/backend/ (Api / Application comments only)
[x] src/frontend/client/ (runtime / state / services / styles / scene comments only)
[x] tests/ (test comments only)
[x] docs/ (API_CONTRACTS.md, ARCHITECTURE.md, DATABASE.md, TDD.md)
[x] .ai/ (workflow / skills documentation only)
```

---

## Implementation Notes

### Authority Boundary: Client-Local Coordination vs. Server Revocation
- In `API_CONTRACTS.md §2.3`, update the narrative describing JWT issuance to reflect `POST /api/auth/register` and `POST /api/auth/login` (`ADR-020`), resolving `verified Account (username/password) → Player match/create → PlayerId → JWT player_id → authenticated request → PlayerId`.
- **Preserve Server Revocation Semantics:** Keep `API_CONTRACTS.md §2.3`'s `Lifecycle (MVP) — revocation: none (no logout, no server-side revocation state)` semantics completely unchanged. Do not claim §2.3 mandates client invalidation after HTTP 401.
- In `ARCHITECTURE.md §2.2.1`, document `invalidateSession(): Promise<void>` as a client-local coordination capability (consistent with the existing treatment of `clearActiveBattleState(): void`). Cite `ADR-020` D4 item 2 as the authority for client-side sign-out/session teardown. Document that `invalidateSession()` coordinates local state cleanup (dropping synchronized battle state, clearing stored credentials via `ApplicationSession.clear()`, detaching transport subscriptions, disconnecting SignalR, and resetting runtime status to unauthenticated) when the user signs out or when the client receives an authenticated HTTP 401 UNAUTHENTICATED response, introducing zero server-side revocation state and zero wire messages.

### Section Reference Reconciliation Scope
- Stale section numbers pointing to the pre-ADR-020 application session mechanism must be updated to `API_CONTRACTS.md §2.3`.
- **`§2.8 → §2.3`:** Reconcile stale citations across declared documentation, source comments, and test comments.
- **`§2.7 → §2.3`:** Reconcile verified stale citations to former security requirements / access token not being the session (e.g. `API_CONTRACTS.md:296`, `ApplicationSessionSignalRTests.cs:147`, `ApplicationSessionRESTTests.cs:300, 306, 572`) to `API_CONTRACTS.md §2.3`.
- **`BattleHubReconnectRecoveryTests.cs` around line 29:** Resolve `BattleStateService §2.8 — ownership, server-side` as a stale reference to `API_CONTRACTS.md §2.8` (session ownership verification) and update it to `§2.3` (or `API_CONTRACTS.md §2.3`), alongside line 587 (`API_CONTRACTS.md §2.8` → `§2.3`).

### Explicit Reference Preserve List (Do Not Modify)
Broad section-number find-and-replace is strictly prohibited. The following valid references within declared files must be preserved:
- `docs/02-technical/API_CONTRACTS.md`: lines 94 and 111 (historical version header changelog notes recording prior 1.9 additions).
- `docs/02-technical/DATABASE.md`: lines 306, 538, and 1094 (valid citations to `GAME_STATE.md §2.8` Battle Identity).
- `src/frontend/client/src/game/runtime/GameRuntime.ts`: line 71 (valid citation to `GAME_STATE.md §2.8`).
- `src/frontend/client/src/services/api/BattleModels.ts`: line 175 (valid citation to `GAME_STATE.md §2.8 item 3`).
- `src/backend/GameServer.Api/Hubs/BattleHub.cs`: lines 639, 925, and 943 (valid citations to `GAME_STATE.md §2.8 items 3 and 4`), and citations to `SIGNALR_PROTOCOL.md §3.2.8` (ComboChanged event contract).
- `src/frontend/client/tests/BattleService.test.ts`: line 94 (`§2.8 item 3`) and line 400 (`§2.8 item 3`) (valid citations to `GAME_STATE.md §2.8 item 3`).
- `src/frontend/client/tests/LobbyScene.test.ts`: lines 1069, 1543, and 2501 (valid citations to `RELIC_RULES.md §2.3`).
- `tests/backend/GameServer.Api.Tests/ApplicationSessionRESTTests.cs`: line 381 (valid citation to `GAME_STATE.md §2.8 item 1`).
- `tests/backend/GameServer.Api.Tests/RedisBattleStateSmokeTest.cs`: line 165 (`GAME_STATE.md §2.8 items 1–2`) and line 185 (`Battle Identity member §2.8`).
- `tests/backend/GameServer.Api.Tests/RedisBattleRecoverySmokeTest.cs`: line 183 (`GAME_STATE.md §2.8 item 3`) and line 191 (`§2.8 item 2`).
- `tests/backend/GameServer.Api.Tests/Hubs/BattleHubReconnectRecoveryTests.cs`: line 20 (`GAME_STATE.md §2.8`), line 58 (`PlayerId records (§2.8)`), line 399 (`GAME_STATE.md §2.8 item 3`), line 420 (`§2.8 item 3`), and line 442 (`GAME_STATE.md §2.8`).

### Prohibition Against Altering Executable Test Logic
- For all test files, changes are strictly limited to comments, XML doc summaries, and test descriptions.
- Never modify executable test assertions, test fixtures, or test logic.
- Specifically, in `src/frontend/client/tests/LobbyScene.test.ts` around line 1682 and the `forbidden` array around lines 1689–1700 (`it('imports no transport client and no Discord SDK', ...)`), the executable test logic and forbidden import list must remain completely unmodified. Comments naming these forbidden imports are stripped by the test runner before evaluation; do not edit the executable string literals.

### Cautionary Guidance on Undeclared Files (Out of Scope)
- Files outside the 41 declared paths must NOT be edited.
- Undeclared files containing valid `GAME_STATE.md §2.8` or `SIGNALR_PROTOCOL.md §3.2.8` citations (such as `PlayerId.cs`, `BattleState.cs`, `BattleResult.cs`, `BattleEventWireProjection.cs`, `BattleStateService.cs`, `BattleStartServiceTests.cs`, `BattleResultServiceTests.cs`, `AuthorityRegressionSuiteTests.cs`, `ApiIntegrationTests.cs`, `SIGNALR_PROTOCOL.md`, etc.) are out of scope.
- `tests/backend/GameServer.Application.Tests/BattleStateServiceReconnectRecoveryTests.cs` contains only valid `GAME_STATE.md §2.8` references (lines 23, 28) and was removed from the declared file set; it must NOT be touched.
- Undeclared files containing historical Discord references (such as `DiscordIdentityResolution.cs` or historical ADRs) are out of scope and must NOT be touched.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — Run existing frontend client Vitest suite (`npm run test:run` in src/frontend/client/)
[x] Unit tests         — Run existing backend .NET test suites (`dotnet test src/backend/GameServer.sln`)
[x] Documentation      — Verify cross-reference consistency across updated markdown and code comments
```

*Note on Testing:* Test suites are run strictly to verify zero regressions from comment and documentation updates, not to regenerate, modify, or assert new test behaviors, consistent with the documentation-change workflow (`documentation/documentation-change.md`).

### Key Edge Cases
- Ensure valid citations on the explicit preserve list are NOT altered.
- Cautionary boundary: verify zero edits occur in undeclared files.
- Verify executable assertions in `LobbyScene.test.ts` (lines 1682–1700) are unchanged.

---

## Stop Conditions

- If a proposed change requires modifying production runtime behavior, API schemas, SignalR protocols, or test assertions / executable test logic: STOP per `AGENTS.md` §16 / §20.
- If changes attempt to alter `API_CONTRACTS.md §2.3` server-side revocation semantics (`revocation: none`): STOP per contract restrictions.
- If an authoritative document conflict is discovered between `ADR-020`, `ADR-015`, and `docs/02-technical/`: STOP per `AGENTS.md` §4 / §20.
- If historical ADR files in `docs/03-decisions/ADR/` would need to be rewritten: STOP per repository immutability rules.
- If any file outside the 41 declared files would need to be modified: STOP per `AGENTS.md` §16.

---

## Completion Evidence

### Commit
- Implementation slice (Phase A): `3aa39302119885595e76ade069247bed46050f7b`
- Subject: `docs: reconcile standalone web authentication and session references`
- Owns: `TASK-235`
- Files: the record's declared file set — exactly 41 paths (`.git diff --cached --name-status` = 41 `M` entries, no extras, no missing, task record excluded)
- Shared with: `none`
- Unowned / pre-existing: `none`

### Changed Files
- `docs/02-technical/API_CONTRACTS.md` — §2.3 reconciled to standalone web account credential issuance (`ADR-020` D2/D3) and stateless JWT carriage (`ADR-015` D4): the retired Discord access-token / `DiscordUserId` exchange narrative and the stale `§2.5`/`§2.7 item 4`/`§2.4` pointers are replaced by the issued-JWT artifact, the `verified Account (username/password) → Player match/create → PlayerId → JWT player_id` chain, and the generic "no credential other than this JWT" transport rule. Live `§2.8` pointers in §4 note 6, §4 note 7, §4.5 note 7, §5 and §7 item 2 resolved to `§2.3` (or to the correct §2.1/§2.2); `ADR-007 item 4` in §4 note 7 resolved to `ADR-015` D3. **`Lifecycle (MVP) — revocation: none (no logout, no server-side revocation state)` is byte-identical (line 349); no client-side invalidation is claimed or mandated by §2.3.**
- `docs/02-technical/ARCHITECTURE.md` — §2.2.1 documents `invalidateSession(): Promise<void>` as a second client-local coordination-only capability alongside `clearActiveBattleState(): void`, citing `ADR-020` D4 item 2 as the authority, describing the credential clear / synchronized-copy drop / subscription detach / disconnect / `session: 'unauthenticated'` publication, and stating explicitly that it is client-local teardown and not server-side revocation (`ADR-015` D2/D5; `§2.3` does not mandate it).
- `docs/02-technical/DATABASE.md` — `API_CONTRACTS.md` `§2.8/§4 note 7` → `§2.3/§4 note 7`; the three `GAME_STATE.md §2.8` citations are untouched.
- `docs/02-technical/TDD.md` — four Discord-era descriptions reconciled to the standalone web client (`ADR-020`): the SPA statement, the §2.1 layer diagram, React's owned responsibilities, and the viewport paragraph.
- `.ai/workflow/quality/review.md` — the Security item's "ADR-007's open item" replaced by the accepted `ADR-020` / `ADR-015` session contract.
- `.ai/skills/backend/api-contract-validation.md` — the reads list, the session-derivation stop condition, the "authentication mechanism the ADR marks as undecided" failure mode and the traceability list now cite the accepted `ADR-020` / `ADR-015` (`ADR-015` D1–D11, `API_CONTRACTS.md` §2.3) instead of the superseded `ADR-007` open item.
- `.ai/skills/quality/implementation-review.md` — the Security/Performance row no longer cites "authentication remains an open item per ADR-007"; it cites `ADR-020` / `ADR-015`.
- `src/frontend/client/src/state/GameRuntimeState.ts` — the `SessionStatus` doc no longer describes the retired `POST /api/auth/discord` boundary; it names the standalone web account endpoints (`ADR-020`) and the `invalidateSession()` cleanup (`ARCHITECTURE.md` §2.2.1).
- `src/frontend/client/src/game/GameConfig.ts`, `.../game/GameViewport.ts`, `.../app/App.css` — Discord Activity viewport / iframe descriptions replaced by the standalone web game viewport (`ADR-020`).
- `src/frontend/client/src/game/runtime/GameRuntime.ts` — `setSessionStatus` doc comment now cites the accepted `ADR-020`/`ADR-015` (and the real `App`/`invalidateSession()` callers) instead of superseded `ADR-007` and the removed "not wired to real auth" claim; the `startBattle` failure citation `§2.8` → `§2.3`. Line 71's `GAME_STATE.md §2.8` citation is untouched.
- `src/frontend/client/src/services/api/ApplicationSession.ts`, `ApiService.ts`, `BattleModels.ts`, `game/scenes/LobbyScene.ts` — six `§2.8` session-contract citations in `ApiService.ts`/`ApplicationSession.ts`, one in `BattleModels.ts:65` and one in `LobbyScene.ts:1105` resolved to `§2.3`; `BattleModels.ts:175` untouched.
- `src/backend/GameServer.Api/Program.cs` — the two `§2.8` citations (hub transport, identity publication) resolved to `§2.3`.
- `src/backend/GameServer.Api/Authentication/ApplicationSessionTokenService.cs` — `§2.8` → `§2.3`; the `verified DiscordUserId` chain replaced by `verified Account (username/password)`; "called by exactly one place — `POST /api/auth/discord`" corrected to `AuthController` via §2.1/§2.2 (`ADR-020` D3/D4); the stale `§2.5` `playerId`/`sessionToken` pointers resolved to `§2.1`/`§2.2`; the `DiscordUserId` clause in the `player_id` claim comment removed.
- `src/backend/GameServer.Api/Authentication/ApplicationSessionAuthentication.cs` — the "one exception — `POST /api/auth/discord`" comment corrected to the two §2.1/§2.2 auth endpoints; five `§2.8` citations resolved to `§2.3`.
- `src/backend/GameServer.Api/Controllers/BattleController.cs` (9), `Controllers/CollectionController.cs` (6), `Hubs/BattleHub.cs` (2), `Application/Battle/IBattleResultRepository.cs` (1), `Application/Battle/BattleResultQueryService.cs` (3), `Application/Collection/CollectionQueryService.cs` (5) — `API_CONTRACTS.md §2.8` (and bare `§2.8` meaning it) resolved to `§2.3`; every `GAME_STATE.md §2.8` / `SIGNALR_PROTOCOL.md §3.2.8` citation is untouched.
- Test files (comments, XML docs and test descriptions only): `src/frontend/client/tests/CollectionService.test.ts` (3), `BattleService.test.ts` (4), `GameRuntime.test.ts` (2), `LobbyScene.test.ts` (1); `tests/backend/GameServer.Api.Tests/ApplicationSessionConfigurationTests.cs` (3), `ApplicationSessionSignalRTests.cs` (2), `ApplicationSessionRESTTests.cs` (7, incl. the class-diagram Discord identity-exchange narrative), `BattleHistoryEndpointTests.cs` (3), `BattleResultEndpointTests.cs` (3), `BattleStartSmokeTest.cs` (1), `CollectionEndpointTests.cs` (5, incl. the stale §1 `POST /api/auth/discord` quotation), `RedisBattleStateSmokeTest.cs` (1), `RedisBattleRecoverySmokeTest.cs` (2), `TestApplicationSession.cs` (2), `Hubs/BattleHubReconnectRecoveryTests.cs` (2), `tests/backend/GameServer.Application.Tests/CollectionQueryServiceTests.cs` (1).

### Acceptance Criteria Verification
1. **Stale `§2.8` / `§2.7` citations → `API_CONTRACTS.md §2.3`** — every declared site reconciled (technical docs, frontend/backend source comments, test comments), including `ApplicationSessionTokenService.cs`, `ApplicationSessionSignalRTests.cs:147` (`§2.7 item 4`), `ApplicationSessionRESTTests.cs:300/306/572` (`§2.7 item 4`), and the `BattleStateService §2.8 — ownership, server-side` diagrams at `RedisBattleRecoverySmokeTest.cs:34` and `BattleHubReconnectRecoveryTests.cs:29`. A repo-wide re-scan of the 41 declared files shows no surviving `API_CONTRACTS.md` `§2.4`–`§2.8` citation and no `§2.7`.
2. **Valid, non-stale references unmodified** — verified programmatically: no changed line in the commit is any line of the preserve list (`API_CONTRACTS.md:94,111`; `DATABASE.md:306,538,1094`; `GameRuntime.ts:71`; `BattleModels.ts:175`; `BattleHub.cs:639,925,943`; `BattleService.test.ts:94,400`; `LobbyScene.test.ts:1069,1543,2501`; `ApplicationSessionRESTTests.cs:381`; `RedisBattleStateSmokeTest.cs:165,185`; `RedisBattleRecoverySmokeTest.cs:183,191`; `BattleHubReconnectRecoveryTests.cs:20,58,399,420,442`).
3. **`API_CONTRACTS.md §2.3` accuracy** — standalone web credential issuance (`ADR-020` D2/D3) and stateless JWT carriage (`ADR-015` D4) described; obsolete Discord token-exchange statements and stale `§2.7`/`§2.5`/`§2.4` pointers removed; `Lifecycle (MVP) — revocation: none` unchanged; §2.3 makes no client-invalidation claim.
4. **`ARCHITECTURE.md §2.2.1`** — `invalidateSession(): Promise<void>` documented as a client-local coordination capability alongside `clearActiveBattleState(): void`, citing `ADR-020` D4 item 2, distinguishing client teardown from server revocation.
5. **Discord-era descriptions** — `GameRuntimeState.ts`, `GameConfig.ts`, `GameViewport.ts`, `App.css`, `TDD.md` now describe the standalone web browser application.
6. **`GameRuntime.setSessionStatus`** — cites accepted `ADR-020` and `ADR-015`.
7. **`.ai/` ADR-007 open-item citations** — reconciled in all three declared `.ai/` files.
8. **No production logic / API schema / SignalR protocol / test assertion change** — every source and test edit in the diff is inside a comment, XML doc or test description; no executable statement, assertion, expected value or string literal was modified (verified line-by-line from `git diff`). `LobbyScene.test.ts` around line 1682 and the `forbidden` array at 1689–1700 are byte-identical.
9. **Zero-regression suites** — both required commands run and pass; counts are unchanged from the pre-task baseline (frontend 933; backend 2970).
10. **Phase A staged set equals the declared file set** — verified: 41 `M` paths, exactly the declared set, no extraneous file, task record excluded (`TASK_LIFECYCLE.md` §6 P-5).

### Validation Results
- `npm run test:run` in `src/frontend/client` — **PASS** (23 files, 933 tests; 0 failed, 0 skipped; identical to the pre-task baseline)
- `dotnet test src/backend/GameServer.sln` — **PASS** (4 suites: Domain 1558, Infrastructure 422, Api 337, Application 653 = 2970 tests; 0 failed, 0 skipped)
- Preserve-list differential check (changed-line numbers from `git diff -U0` vs the record's protected lines) — **PASS**: zero protected lines modified
- Stale-reference re-scan over the 41 declared files — **PASS**: no `API_CONTRACTS.md` `§2.4`–`§2.8` or `§2.7` citation and no obsolete `ADR-007`-open-item citation survives; remaining `§2.x` matches are valid `GAME_STATE.md` / `RELIC_RULES.md` / `AGENTS.md` / `SIGNALR_PROTOCOL.md` citations, executable test strings, or historical version-header changelog notes
- Documentation cross-reference consistency (`documentation/documentation-change.md` §1 final step) — **PASS**: re-read `API_CONTRACTS.md` §2, `ARCHITECTURE.md` §2.2.1, `TDD.md` §2.1 and their referencing files together; no duplicate definition introduced

### Review (`quality/review.md` §1) — Disposition: Pass
- **Correctness** — matches `ADR-020` (D1 retirement, D2 `Accounts`/`AccountId`, D3 §2.1/§2.2 issuance, D4 item 2 persistence + sign-out), `ADR-015` (D1 artifact, D2 stateless, D3 `player_id`, D4 transport, D5 lifecycle), `API_CONTRACTS.md` §1/§2.1–§2.3/§6, and `ARCHITECTURE.md` §2.2.1.
- **Architecture** — no boundary moves: `invalidateSession()` is recorded as coordination-only exactly like `clearActiveBattleState()`; §2.2.1 rules 1/3/5/6 still hold; no module, layer, or port capability changed.
- **Scope** — only the 41 declared files, and within them only documentation and comments; nothing unrelated refactored or "improved".
- **Tests** — the documentation-change workflow treats suites as zero-regression verification, not new coverage (`documentation/documentation-change.md` §4): both suites ran, counts unchanged, and no test was regenerated or edited beyond comments.
- **Documentation** — this task *is* the documentation correction; the corrected documents were re-read against their referencing files and against `ADR-020`/`ADR-015`.
- **Security** — §2.3's obsolete Discord access-token narrative is replaced by the `ADR-015` D4 invariant (no credential other than the issued JWT is accepted as a session), no secret, key, or token material is described or exposed, and server-side revocation semantics are untouched.
- **Performance** — no code path changed.
- **Maintainability** — no abstraction, interface, or new concept; the §2.2.1 addition follows the existing capability-documentation pattern.
- **Determinism (gameplay/battle)** — not engaged: no gameplay, battle, RNG, or server-authoritative value is touched.

### Commit Policy Verification
- P-1 branch: single `master`, no per-task branch or worktree; P-2 Phase A = one commit for exactly this task's declared slice; P-3 authorized active manifest in `tasks/active/` before the commit; P-4 subject carries no task ID, the body carries `owns: TASK-235`; P-5 staged set verified equal to the declared file set; P-6 no amend, reset, rebase, force-push, `git commit -a`, or batched completion record.

### Out-of-Scope Observations (reported, not fixed — `AGENTS.md` §16)
- `docs/02-technical/ARCHITECTURE.md` §2.2 (lines 192, 201, 208, 216, 220) and §2.3's heading (line 656) plus the §3 component row (line 736) still carry Discord Activity / `services/discord/` / `DiscordService` descriptions. The manifest's Discord-era criterion names only `GameRuntimeState.ts`, `GameConfig.ts`, `GameViewport.ts`, `App.css` and `TDD.md`, and the only authorized ARCHITECTURE change is §2.2.1, so these were left in place rather than broadening the task.
- `src/frontend/client/src/game/scenes/LobbyScene.ts:339` still lists "the Discord SDK" among the imports the scene must not make. It is a boundary statement that mirrors the immutable `forbidden` array of `LobbyScene.test.ts:1689–1700`, not a description of the application as a Discord Activity, so it was preserved.
- `src/frontend/client/src/services/api/BattleModels.ts:66` still asserts "no `discordUserId`" as a deliberately absent request member. It is a still-true negative assertion about the request surface (ADR-020 D2 removed the member, so none can be sent) and is not one of the enumerated Discord-era corrections.
- Undeclared files keep drift that is outside this record's declared set: `src/backend/GameServer.Application/Identity/DiscordIdentityResolution.cs:31,37` (`API_CONTRACTS.md` `§2.6 rule 5` / `§2.7 item 5–6`), `ApplicationSessionClaims.cs`, `AuthenticatedPlayer.cs`, `PlayerIdClaimRequirement.cs`, `AuthenticatedPlayerMiddleware.cs`, `UnauthenticatedResponse.cs`, `ApplicationSessionOptions.cs`, `BattleResultResponse.cs` (`§2.8`), `docs/02-technical/SIGNALR_PROTOCOL.md:155` (`§2.8`, historical changelog note), `docs/01-game-design`/`docs/00-overview` Discord-era text, `.ai/skills/client/react-phaser-boundary/SKILL.md` ("Discord Activity SDK"), `src/backend/GameServer.Infrastructure/Discord/` and the inert `IDiscordIdentityResolver` registration (TASK-212 §A-20), and `src/backend/GameServer.Api/appsettings.json`'s `"Discord"` section. Suggested follow-up: a new task for the remaining backend/`.ai/`/`ARCHITECTURE.md` §2.2 Discord residue with the dependency decision TASK-212 §A-20 records as DECISION-REQUIRED.
- No `**Version:**` preamble entry was added to `API_CONTRACTS.md` / `ARCHITECTURE.md` / `TDD.md` / `DATABASE.md`: the manifest does not include version-header updates in scope, and acceptance criterion 2 explicitly protects `API_CONTRACTS.md`'s version-header changelog notes from modification. A version-note update is a legitimate follow-up if the repository wants the §2.3/§2.2.1 reconciliations recorded in those preambles.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed P-1…P-6 commit policy compliance
- [x] Confirmed product decisions NG-13 and A-16 are untouched (no file naming either decision was modified)
- [x] Confirmed `API_CONTRACTS.md §2.3` `revocation: none` semantics intact and no client-invalidation mandate attributed to §2.3
