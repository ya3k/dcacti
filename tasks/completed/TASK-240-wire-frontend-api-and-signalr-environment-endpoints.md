# TASK-240 — Wire Frontend API and SignalR Environment Endpoints

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section; does not copy rules or schemas.
  Created following the post-TASK-239 repository audit.
-->

<!--
  Lifecycle: created BACKLOG 2026-10-10 by the orchestrator role during post-TASK-239 audit.
-->

<!--
  Lifecycle: BACKLOG → READY completed 2026-10-10 at the requester's direction,
  after validating the tasks/TASK_LIFECYCLE.md §3 READY criteria:
    [x] Task type confirmed (BUG — tasks/TASK_TYPES.md §2, development/bug-fix.md;
        the class is a Configuration/environment bug, §1: the documented
        `VITE_API_URL` / `VITE_SIGNALR_URL` settings in
        `src/frontend/client/.env.example` have no client reader)
    [x] Relevant documentation exists in docs/ (ADR-020 D1/D4/D5,
        ARCHITECTURE.md §2.2/§2.2.1/§2.2.2, MVP_SCOPE.md §1)
    [x] MVP scope confirmed (MVP_SCOPE.md §1 — standalone web client is IN; this
        task adds no system or content, it makes an existing documented
        environment setting effective)
    [x] Not blocked: Dependencies — TASK-239 (DONE, immutable)
    [x] Primary agent assigned (client), supporting agents assigned (testing, review)
    [x] Workflow assigned (development/bug-fix.md)
    [x] Acceptance criteria are binary and testable
    [x] Uniqueness: TASK-239 is the highest completed ID and no other record names TASK-240
    [x] Ownership: tasks/active/ and tasks/blocked/ hold no open record, and no other
        backlog record names any declared file
-->

<!--
  Lifecycle: READY → IN PROGRESS completed 2026-10-10 by the client agent under
  development/bug-fix.md: preflight completed (AGENTS.md, docs/AGENTS.md,
  .ai/README.md, tasks/README.md, tasks/TASK_LIFECYCLE.md, tasks/TASK_TYPES.md,
  development/bug-fix.md, core/completion.md, quality/testing.md, quality/review.md,
  discovery/documentation-discovery, discovery/impact-analysis,
  client/react-phaser-boundary, quality/architecture-conformance,
  quality/scope-validation, ADR-020, ARCHITECTURE.md §2.2/§2.2.1, MVP_SCOPE.md §1,
  `src/frontend/client/.env.example`, the TASK-239 record); TASK-239 re-confirmed
  DONE and immutable in tasks/completed/ (Phase A 8f12b10, Phase B daf4f6a); the
  declared 6-file set and the two-stage commit protocol re-confirmed against the
  manifest and the then-current HEAD (daf4f6a); the working tree re-checked
  (clean index, no stash, no unrelated user change — only this record, untracked,
  plus the six declared files once edited); fresh pre-change baselines recorded on
  the untouched tree: `npm run build` 0 errors (pre-existing @microsoft/signalr
  PURE-annotation and >500 kB chunk warnings only), `npm run test:run`
  933 passed / 0 failed across 23 test files.
-->

<!--
  Lifecycle: IN PROGRESS → IN REVIEW completed 2026-10-10 by the client agent
  upon completing the six declared edits: `ApiService` resolves its `baseUrl`
  once per instance from `import.meta.env.VITE_API_URL` (trailing slashes
  stripped, `''` when unset or empty) with a documented test-only
  `resetInstance()` seam, `App.tsx` builds the hub URL from
  `import.meta.env.VITE_SIGNALR_URL` on both connection paths while
  `BATTLE_HUB_URL` stays the unchanged relative default, the `VITE_DEV_AUTH`
  declaration and its stub plus both stale TASK-181 doc-comments were removed,
  and 11 regression tests were added. Post-change verification: `npm run build`
  0 errors, `npm run test:run` 944 passed / 0 failed across 23 test files; the
  seven new configured/normalized cases were shown to FAIL on the pre-change
  behaviour (temporary revert of the two defaults, 7 failed | 66 passed) and to
  pass with it; a configured production bundle inlines the configured base for
  both resolvers while a default bundle contains no absolute URL. No file
  outside the declared set is modified.

  Lifecycle: IN REVIEW → DONE completed 2026-10-10 by the review role
  (independent read-only review of the working tree against this manifest,
  `src/frontend/client/.env.example`, ADR-020 D1/D4/D5, ARCHITECTURE.md
  §2.2/§2.2.1, MVP_SCOPE.md §1, `quality/review.md` §1 and
  `quality/scope-validation`): PASS, no blocking finding, no stop condition
  fired — the staged set was exactly the 6 declared paths, the two removal sites
  are byte-identical to the manifest's authorization, no other file's behaviour
  changed, no `SignalRService` / `GameRuntime` signature moved, and no
  whitespace, line-ending or unrelated churn is present (`git diff --numstat`
  equals `git diff --ignore-cr-at-eol --numstat`). The declared implementation
  slice was committed as Phase A commit
  `e26ca114adb77c853fd1052cdcf27ed42c9f6461`, and this completion record is
  filed under its own Phase B commit.
-->

---

## Metadata

```text
Task ID:           TASK-240
Type:              BUG
Status:            DONE
Risk:              LOW
Priority:          LOW
Primary Agent:     client
Supporting Agents: testing, review
Workflow:          development/bug-fix.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis, client/react-phaser-boundary, quality/architecture-conformance, quality/scope-validation
Dependencies:      TASK-239
Declared Files:    src/frontend/client/src/services/api/ApiService.ts, src/frontend/client/src/app/App.tsx, src/frontend/client/src/vite-env.d.ts, src/frontend/client/tests/setup.ts, src/frontend/client/tests/BattleService.test.ts, src/frontend/client/tests/AppLifecycle.test.tsx
```

---

## Objective

Connect the frontend client runtime to the documented `VITE_API_URL` and `VITE_SIGNALR_URL` environment variables so that remote/tunnel endpoints configured in `.env.local` are respected by `ApiService` and `App.tsx` while preserving the default relative same-origin/proxy behavior when unset, and retire the obsolete `VITE_DEV_AUTH` declaration and test stub residue leftover from the retired pre-ADR-020 dev authentication harness.

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-020-standalone-web-account-authentication.md` D1, D4, D5 — Standalone web client architecture and session management.
- `docs/02-technical/ARCHITECTURE.md` §2.2, §2.2.2 — Standalone web client structure, `ApiService` REST transport, and `SignalRService` realtime connection.
- `docs/00-overview/MVP_SCOPE.md` §1 — Standalone web client in MVP scope.
- `src/frontend/client/.env.example` — Authoritative frontend environment configuration template documenting `VITE_API_URL` and `VITE_SIGNALR_URL` for tunnel testing.
- `tasks/completed/TASK-239-retire-residual-configuration-from-retired-discord-integration.md` — Completed configuration audit identifying the inert environment variable gap and stale `VITE_DEV_AUTH` residue.
- `AGENTS.md` §§9, 10, 13, 14, 16; `.ai/workflow/development/bug-fix.md` §§1–4 — Bug classification, minimal fix discipline, server authority, scope discipline.

---

## Scope

### In Scope

1. **`src/frontend/client/src/services/api/ApiService.ts`**:
   - Resolve `baseUrl` using `import.meta.env.VITE_API_URL` (trimmed of trailing slashes) when defined and non-empty; fallback to empty string `''` for default same-origin / Vite dev proxy requests.
   - Support test reset / dynamic instance configuration where necessary for unit testing.

2. **`src/frontend/client/src/app/App.tsx`**:
   - Construct the battle hub connection URL by prefixing `BATTLE_HUB_URL` (`'/hubs/battle'`) with `import.meta.env.VITE_SIGNALR_URL` (trimmed of trailing slashes) when defined and non-empty.
   - Preserve default relative path `'/hubs/battle'` when `VITE_SIGNALR_URL` is unset or empty.

3. **`src/frontend/client/src/vite-env.d.ts`**:
   - Remove the obsolete `VITE_DEV_AUTH` property declaration (`lines 6-12`) and its TASK-181 doc-comment.
   - Preserve `VITE_API_URL` and `VITE_SIGNALR_URL` type definitions in `ImportMetaEnv`.

4. **`src/frontend/client/tests/setup.ts`**:
   - Remove the stale `vi.stubEnv('VITE_DEV_AUTH', 'false')` call (`lines 29-40`) and its accompanying TASK-181 doc-comment.

5. **`src/frontend/client/tests/BattleService.test.ts`**:
   - Add unit tests verifying `ApiService` endpoint URL generation with and without `VITE_API_URL`.

6. **`src/frontend/client/tests/AppLifecycle.test.tsx`**:
   - Add unit test verifying that `bootstrapApplication` / `handleAuthenticated` resolves the SignalR hub URL with and without `VITE_SIGNALR_URL`.

### Out of Scope

- Modifying backend CORS policy, routes, or controllers (`src/backend/`).
- Changing backend environment templates (`src/backend/.env.example`).
- Changing frontend template instructions (`src/frontend/client/.env.example`).
- Product decisions regarding battle forfeit/abandonment (TASK-212 §A-16) or post-login initialization error recovery (NG-13).
- Modifying `SignalRService.ts` or `GameRuntime.ts` method signatures.
- Any changes to gameplay, board, combat, or presentation logic.

---

## Current State

In `src/frontend/client/.env.example`, developers are instructed to supply `VITE_API_URL` and `VITE_SIGNALR_URL` when testing with remote backend tunnels (e.g., `https://xxxx.trycloudflare.com`). However:
1. `ApiService.ts` hardcodes `baseUrl = ''`, ignoring `import.meta.env.VITE_API_URL`.
2. `App.tsx` passes a hardcoded relative `BATTLE_HUB_URL = '/hubs/battle'`, ignoring `import.meta.env.VITE_SIGNALR_URL`.
3. `vite-env.d.ts` and `tests/setup.ts` still declare and stub `VITE_DEV_AUTH`, an obsolete development mock switch from TASK-181 that has no remaining consumers in production or test code.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-240 (6 files):
```text
src/frontend/client/src/services/api/ApiService.ts
src/frontend/client/src/app/App.tsx
src/frontend/client/src/vite-env.d.ts
src/frontend/client/tests/setup.ts
src/frontend/client/tests/BattleService.test.ts
src/frontend/client/tests/AppLifecycle.test.tsx
```

Evidence per declared path:

| Path | Action | Evidence |
|---|---|---|
| `src/frontend/client/src/services/api/ApiService.ts` | Edit | Consume `import.meta.env.VITE_API_URL` as default `baseUrl` when present; fallback to `''`. |
| `src/frontend/client/src/app/App.tsx` | Edit | Prefix `BATTLE_HUB_URL` with `import.meta.env.VITE_SIGNALR_URL` when present. |
| `src/frontend/client/src/vite-env.d.ts` | Edit | Remove obsolete `VITE_DEV_AUTH` property and comment; preserve `VITE_API_URL` and `VITE_SIGNALR_URL`. |
| `src/frontend/client/tests/setup.ts` | Edit | Remove stale `vi.stubEnv('VITE_DEV_AUTH', 'false')` and its doc-comment. |
| `src/frontend/client/tests/BattleService.test.ts` | Edit | Add tests verifying `ApiService` URL resolution against configured vs unset `VITE_API_URL`. |
| `src/frontend/client/tests/AppLifecycle.test.tsx` | Edit | Add tests verifying SignalR hub URL resolution against configured vs unset `VITE_SIGNALR_URL`. |

*(Note: The task manifest file itself is excluded from the implementation declared file set. The list above strictly matches the `Declared Files:` field in `## Metadata`.)*

---

## Acceptance Criteria

- [x] When `import.meta.env.VITE_API_URL` is set to a base URL (e.g. `https://be.example.com`), `ApiService` issues requests to `${VITE_API_URL}/api/...` and `${VITE_API_URL}/health` with redundant trailing slashes normalized. *(Config is read once per instance by `resolveApiBaseUrl`, which strips every trailing slash; `BattleService.test.ts` asserts `…/api/battle/history`, `…/api/battle/start` and `…/health` for bases ending in `/` and `///`.)*
- [x] When `import.meta.env.VITE_API_URL` is unset or empty, `ApiService` preserves existing relative paths (e.g. `/api/...` and `/health`). *(Unset — `vi.stubEnv(..., undefined)`, which deletes the variable — and empty both resolve to `''`, and the existing relative-path assertions across `BattleService.test.ts`, `CollectionService.test.ts` and `ApplicationSession.test.ts` still pass unchanged.)*
- [x] When `import.meta.env.VITE_SIGNALR_URL` is set (e.g. `https://be.example.com`), `App.tsx` initializes the SignalR connection to `${VITE_SIGNALR_URL}/hubs/battle` with redundant trailing slashes normalized. *(Both connection paths — `bootstrapApplication` and `handleAuthenticated` — pass `resolveBattleHubUrl()` to `runtime.initialize`, and `AppLifecycle.test.tsx` asserts the URL `SignalRService.connect` is actually opened with.)*
- [x] When `import.meta.env.VITE_SIGNALR_URL` is unset or empty, `App.tsx` preserves existing relative path `/hubs/battle`. *(Unset and empty both return the unchanged exported `BATTLE_HUB_URL`.)*
- [x] `src/frontend/client/src/vite-env.d.ts` contains no `VITE_DEV_AUTH` declaration or comment. *(Repository-wide search of `src/` and `tests/` returns no `VITE_DEV_AUTH` occurrence; remaining matches are only in task records, which no code reads.)*
- [x] `src/frontend/client/tests/setup.ts` contains no `VITE_DEV_AUTH` stub or comment. *(Stub and doc-comment removed; every other test-environment initialisation — the `localStorage` double and the Phaser 4 mock — is untouched.)*
- [x] Frontend solution builds with zero errors: `npm run build` in `src/frontend/client`. *(0 errors; only the pre-existing `@microsoft/signalr` PURE-annotation and >500 kB chunk warnings, identical to the pre-change baseline.)*
- [x] Frontend test suite passes with zero failures: `npm run test:run` in `src/frontend/client`. *(944 passed / 0 failed across 23 test files — the 933-test baseline plus 11 new cases.)*
- [x] Backend solution and test suite remain unaffected (zero regressions). *(No `src/backend/` or `tests/backend/` path appears in the change set; `dotnet build src/backend/GameServer.sln` 0 errors / 2 warnings, the same warning set as the recorded baseline.)*
- [x] Exactly the 6 declared files are modified; no file outside the declared set is staged (P-5). *(`git diff --cached --name-status` before Phase A listed exactly the six declared paths and no seventh.)*
- [x] Two-stage commit protocol followed (`TASK_LIFECYCLE.md` §6). *(Phase A `e26ca11` carries the six declared files with `owns: TASK-240` and no task ID in its subject; Phase B files this record alone.)*

---

## Affected Files & Areas

```text
[ ] src/backend/ (no backend changes)
[x] src/frontend/client/ (src/services/api/ApiService.ts, src/app/App.tsx, src/vite-env.d.ts)
[x] tests/ (src/frontend/client/tests/setup.ts, BattleService.test.ts, AppLifecycle.test.tsx)
[ ] docs/ (no documentation changes)
```

---

## Implementation Notes

### URL Normalization Guidance
- Use a helper function or inline `.replace(/\/+$/, '')` on the environment variables so that both `https://tunnel.trycloudflare.com` and `https://tunnel.trycloudflare.com/` result in clean URLs like `https://tunnel.trycloudflare.com/api/...` without double slashes.

### Environment Stubbing in Tests
- Vitest provides `vi.stubEnv('VITE_API_URL', '...')` and `vi.unstubAllEnvs()` to safely verify configured and default environments within test cases without polluting subsequent tests.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — ApiService base URL resolution with empty, relative, and absolute VITE_API_URL values
[x] Lifecycle tests    — App.tsx SignalR hub URL resolution with empty and absolute VITE_SIGNALR_URL values
[x] Zero-regression    — npm run test:run across all 23 frontend test files
[x] Type check & build — npm run build in src/frontend/client
```

### Commands
```text
npm run build      # in src/frontend/client
npm run test:run   # in src/frontend/client
```

---

## Stop Conditions

- If modifying `ApiService` or `App.tsx` requires changing backend CORS rules or API endpoint routes: STOP and report.
- If removing `VITE_DEV_AUTH` breaks any active tests or runtime paths: STOP and report the unexpected consumer.
- If task requires widening beyond the 6 declared files: STOP per `TASK_LIFECYCLE.md` §6.1 / P-5.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Commit
- Implementation slice (Phase A): `e26ca114adb77c853fd1052cdcf27ed42c9f6461`
- Subject: `fix: resolve the client api and battle hub urls from the environment`
- Owns: `TASK-240`
- Files: `src/frontend/client/src/services/api/ApiService.ts, src/frontend/client/src/app/App.tsx, src/frontend/client/src/vite-env.d.ts, src/frontend/client/tests/setup.ts, src/frontend/client/tests/BattleService.test.ts, src/frontend/client/tests/AppLifecycle.test.tsx`
- Shared with: `none`
- Unowned / pre-existing: `none`
- Phase B: this completion record alone, filed for the first time under its own
  bookkeeping commit (the record was never tracked, so Git represents it as an
  addition `A` in `tasks/completed/`).

### Changed Files
- `src/frontend/client/src/services/api/ApiService.ts` — added `resolveApiBaseUrl()`, which reads `import.meta.env.VITE_API_URL`, returns `''` when it is unset or not a string, and strips every trailing slash otherwise; the existing private constructor now defaults its `baseUrl` parameter to that value (the explicit-argument path and the `baseUrl` field are unchanged, so no consumer or interface changed); added a documented test-only `ApiService.resetInstance()` so a test can rebuild the singleton under a stubbed environment, mirroring `App.tsx`'s existing `resetSharedRuntime()` seam.
- `src/frontend/client/src/app/App.tsx` — added `resolveBattleHubUrl()`, which returns the unchanged exported `BATTLE_HUB_URL` (`/hubs/battle`) when `import.meta.env.VITE_SIGNALR_URL` is unset or empty and otherwise `${base without trailing slashes}/hubs/battle`; both connection paths (`bootstrapApplication` and `handleAuthenticated`) now pass it to `runtime.initialize(...)`. `GameRuntime.initialize` / `SignalRService.connect` signatures and the authentication, session-recovery, reconnect and lifecycle code around them are untouched.
- `src/frontend/client/src/vite-env.d.ts` — removed the obsolete `VITE_DEV_AUTH` property declaration and its TASK-181 doc-comment; `VITE_API_URL` and `VITE_SIGNALR_URL` are preserved.
- `src/frontend/client/tests/setup.ts` — removed the obsolete `vi.stubEnv('VITE_DEV_AUTH', 'false')` and its TASK-181 doc-comment; the `localStorage` double and the Phaser 4 mock are preserved.
- `src/frontend/client/tests/BattleService.test.ts` — added the `ApiService base URL (VITE_API_URL)` describe (6 cases): configured base used for a `GET`, single and multiple trailing slashes normalized (including the `/health` path), a configured `POST` keeping its documented path/method/body, and unset (`vi.stubEnv(..., undefined)`) and empty both keeping relative paths. The describe resets the singleton around each case and calls `vi.unstubAllEnvs()`, so no stubbed environment leaks into the other 49 cases.
- `src/frontend/client/tests/AppLifecycle.test.tsx` — added the `battle hub URL (VITE_SIGNALR_URL)` describe (5 cases): configured base on the startup session-restore path, trailing-slash normalization, unset and empty keeping `/hubs/battle`, and the configured base on the interactive login path. Each case asserts the URL `SignalRService.connect` is actually opened with, so it proves resolved runtime behaviour rather than a helper's return value; `vi.unstubAllEnvs()` was added to the file's existing `afterEach`.

### Validation Results
- `npm run build` (in `src/frontend/client`) — PASS, 0 errors. Output identical to the pre-change baseline apart from the bundle hash (`dist/assets/index-CckSn1rs.js`, 1,981.63 kB); the only diagnostics are the pre-existing `@microsoft/signalr` `/*#__PURE__*/` rollup notes and the >500 kB chunk-size warning, both present before this change.
- `npm run test:run` (in `src/frontend/client`) — PASS, 944 passed / 0 failed / 0 skipped across 23 test files (baseline 933 passed / 0 failed across the same 23 files; +11 new cases, no existing case modified in intent).
- Targeted runs — `npx vitest run tests/BattleService.test.ts tests/AppLifecycle.test.tsx` PASS (55 + 18 tests); the same two files with the two pre-change defaults temporarily restored FAIL 7 / pass 66, i.e. exactly the configured and trailing-slash cases, proving the new tests are regression tests for the fixed behaviour.
- Targeted end-to-end probe — `npm run build` with `VITE_API_URL` and `VITE_SIGNALR_URL` set to `https://be.example.com` emits both resolvers with that base inlined (two occurrences, each followed by `.replace(/\/+$/,"")`, with `/hubs/battle` appended by the hub resolver); the default build emits no occurrence of the absolute URL and keeps `/hubs/battle`. `dist/` is gitignored, so no build artifact is part of the change set.
- Backend unaffected — no `src/backend/` or `tests/backend/` path is touched; `dotnet build src/backend/GameServer.sln` → 0 errors / 2 warnings, matching the recorded pre-change baseline warning set (the two NuGet `MSB3277` EF-Core version-conflict warnings).
- Residue scan — `VITE_DEV_AUTH` has zero occurrences under `src/frontend/client/src` and `src/frontend/client/tests`; the only remaining occurrences in the repository are in task records, which no code reads.
- Scope — `git diff --cached --name-status` before Phase A listed exactly the six declared paths; the record itself stayed unstaged for Phase B; `git diff --summary` is empty (no mode/add/delete changes) and `git diff --numstat` equals `git diff --ignore-cr-at-eol --numstat` (no whitespace- or line-ending-only churn).

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (the change resolves transport addresses only; no battle, board, combat, reward or progression value is read, derived or stored, per `AGENTS.md` §10 / ADR-001)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1 — standalone web client; no system, content or technology added)
- [x] Confirmed no documentation change was required: no document under `docs/` mentions `VITE_API_URL` / `VITE_SIGNALR_URL`, and the authoritative frontend template `src/frontend/client/.env.example` already documents both variables and is explicitly out of scope, so it is byte-unchanged
- [x] Confirmed `SignalRService.ts` and `GameRuntime.ts` are unmodified and their public method signatures are unchanged
- [x] Confirmed same-origin/proxy workflows are preserved by construction and by test (relative `/api/...`, `/health` and `/hubs/battle` remain the behaviour when the variables are unset or empty)
- [x] Confirmed P-1…P-6 commit policy compliance

### Remaining Issues / Findings (report only — outside the declared file set)
- `src/frontend/client/.env.local` is a developer-local, gitignored file that still contains the retired `VITE_DISCORD_CLIENT_ID=…` and `VITE_DEV_AUTH=true` settings plus their stale section comments. It is untracked, absent from this manifest's declared set, and not read by any code anymore; the residual stub removal here makes it inert. A follow-up cleanup task (or a developer deleting the file) would remove the last trace.
- `src/frontend/client/.env.example` and `vite.config.ts` remain the only places that describe the client's own-origin/tunnel configuration; they are explicitly out of scope and were not touched.
