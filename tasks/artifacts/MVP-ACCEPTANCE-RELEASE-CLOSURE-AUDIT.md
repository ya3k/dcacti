# MVP Acceptance & Release Closure — Verification and Decision Audit

**Date:** 2026-10-10
**Branch audited:** `master`
**HEAD audited:** `5a56ef1b96db5718d0b9bd286ae453c9df8c3f30`
**Task type:** Verification, release preparation, decision tracking only.
**Authority:** This record does not accept or release the MVP. The Product Owner retains that authority.

---

## Status

```text
ACCEPTANCE CHECKLIST READY — PO DECISION PENDING
```

**Rationale.** The MVP functional surface is intact and independently re-verified at the current
HEAD: backend 2,970/2,970, frontend 944/944, and the primary live end-to-end suite 159 checks × 2
runs with 0 failures. The one outstanding Product Owner decision (E-F1) is a one-line product
record that does not block acceptance review of the existing behaviour. Formal **acceptance review**
is supported by the evidence below. **Production release is not currently supportable**, for reasons
that are release-process gaps rather than MVP scope failures (§4, §7).

---

## 1. Repository baseline (Phase A)

Re-verified, not carried forward from the prior audit.

| Item | Measured value | Prior audit claim | Verdict |
|---|---|---|---|
| Branch | `master` | `master` | Confirmed |
| HEAD | `5a56ef1b96db5718d0b9bd286ae453c9df8c3f30` | `5a56ef1` | Confirmed |
| HEAD commit message | `docs: file the frontend environment endpoint wiring completion record` | — | — |
| Working tree | Clean (`git status --porcelain` empty) | Clean | Confirmed |
| Remote divergence | `0` behind, `4` ahead of `origin/master` | 4 ahead | Confirmed (re-fetched) |
| Latest completed task | `TASK-240-wire-frontend-api-and-signalr-environment-endpoints.md` | TASK-240 | Confirmed |
| `tasks/backlog/` | `.gitkeep` only — empty | Empty | Confirmed |
| `tasks/active/` | `.gitkeep` only — empty | Empty | Confirmed |
| `tasks/blocked/` | `.gitkeep` only — empty | Empty | Confirmed |
| Backend tests | **2,970 passed / 0 failed / 0 skipped** | 2,970 passing | Confirmed |
| Frontend tests | **944 passed / 0 failed**, 23 files | 944 passing | Confirmed |
| Live E2E at current HEAD | **Executed this audit** (§2) | Not rerun | Superseded |
| Deployment pipeline | **None** — no `Dockerfile`, no `.github/`, no CD pipeline | None | Confirmed |
| Outstanding PO decision | **1** (E-F1) | 1 | Confirmed |

Backend suite breakdown (measured):

```text
GameServer.Domain.Tests          1,558 passed / 0 failed
GameServer.Infrastructure.Tests    422 passed / 0 failed
GameServer.Api.Tests               337 passed / 0 failed
GameServer.Application.Tests       653 passed / 0 failed
                                 -----
                                 2,970 passed / 0 failed / 0 skipped
```

No reset, rebase, amend, checkout, or discard was performed. No commit was created. No push was
performed. `HEAD` and the working tree are byte-identical to the values recorded above at the end
of this audit (§10).

**Documentation gap identified at baseline.** The repository has **no documented run procedure for
the live E2E suites**. `verify:e2e:smoke` / `:collection` / `:history` exist as npm scripts
(`src/frontend/client/package.json`) and `standalone-web-smoke.mjs` carries a usage comment, but
`AGENTS.md`, `.ai/workflow/quality/testing.md`, `.ai/README.md`, and `docs/` contain no command,
prerequisite list, or start/stop procedure for them. The prerequisites below were therefore derived
from the suites' own preflight code and error messages, which is the only in-repository source.

---

## 2. E2E verification (Phase B)

### 2.1 Prerequisites and how they were satisfied

Derived from `standalone-web-smoke.mjs`'s `checkPreflightHealth()` and its own remediation text:

```text
PostgreSQL       localhost:5433         already running (pre-existing dev container)
Redis            localhost:6379         already running (pre-existing dev container)
GameServer.Api   http://localhost:5000  started via: dotnet run --project
                                        src/backend/GameServer.Api/GameServer.Api.csproj
Vite dev server  http://localhost:5173  started via: npm run dev
                                        (in src/frontend/client)
Headless browser Edge at
                 C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe
Toolchain        dotnet 10.0.400 · node v25.6.1 · npm 11.9.0
```

Both application services were started **only after** verifying PostgreSQL and Redis were already
up. Development configuration and the pre-existing approved local secret store were used unchanged.
No secret value was printed, read, or exposed at any point. No application configuration was
changed to influence a result. Both services were shut down at the end of verification and their
ports re-confirmed closed.

### 2.2 Commands executed and outcomes

| # | Command | Exit | Result |
|---|---|---|---|
| 1 | `npm run verify:e2e:smoke` | **0** | RUN 1: 159 checks, 0 failures · RUN 2: 159 checks, 0 failures — "ALL SMOKE TEST RUNS PASSED CLEANLY (NO FLAKINESS)" |
| 2 | `npm run verify:e2e:collection` | **0** | RUN 1: 42 checks, 0 failures · RUN 2: 42 checks, 0 failures — "ALL COLLECTION VIEWER RUNS PASSED CLEANLY (NO FLAKINESS)" |
| 3 | `npm run verify:e2e:history` | **1** | **FATAL, reproducible ×2** at Phase 4 — see §2.3 |
| 4 | `dotnet test src/backend/GameServer.sln` | 0 | 2,970 passed / 0 failed |
| 5 | `npm run test:run` | 0 | 944 passed / 0 failed (23 files) |
| 6 | `npm run build` | 0 | Built clean; only the pre-existing `>500 kB chunk` warning |

**Suite 1 (`verify:e2e:smoke`) — PASS.** The full standalone-web player journey, twice against the
current HEAD, from a clean profile and a uniquely registered account each run. 159 checks per run,
zero failures, no flakiness across the two runs. Covered the registration → session/starter-grant
(5 Pets / 3 Cards / 10 Relics) → re-login → MainMenu → Lobby → Boss selection → battle start →
8×8 board → Match-3 swap → cast controls → one-cast-per-turn → Signature Skill →
reconnect/resync → ResultScene → reward failure & RETRY → PLAY AGAIN preserved-loadout lifecycle →
a real completed battle with delivered rewards → group delivery after reconnect → error/health
safety gates (zero Discord dependencies, zero uncaught exceptions, zero fatal console errors, zero
unexpected API responses).

**Suite 2 (`verify:e2e:collection`) — PASS.** 42 checks × 2 runs, 0 failures.

### 2.3 Suite 3 (`verify:e2e:history`) — reproducible failure, classified

```text
Command:   npm run verify:e2e:history
Exit code: 1
Output:    [BATTLE HISTORY SMOKE FATAL ERROR]
           Timeout waiting for condition: BattleScene active after START BATTLE (15000ms)
Reproduced: 2 of 2 consecutive attempts, identical failure point
Passing checks before the failure: phase1 (2) + phase2 (6) + phase3 (6) + phase4 partial (3)
```

**Classification: PRE-EXISTING HARNESS DEFECT — not a product regression, not an environment
issue, not flaky.** The classification is *attributed by evidence*, not assumed:

1. **Root cause, located.** `battle-history-smoke.mjs:1131` selects the battle-start control as
   ```js
   const startButton = lobby.interactive.find((o) => o.type === 'Rectangle' && o.enabled);
   ```
   This returns the **first** interactive rectangle in the Lobby's display order. `TASK-211` added
   a `< BACK` control to the Lobby *before* the `START BATTLE` trigger, so the first enabled
   rectangle is `< BACK`. The click returns to the main menu and the run dies waiting for
   `BattleScene`.
2. **The passing suite does it correctly.** `standalone-web-smoke.mjs` resolves the same control
   **by its caption** — `lobbyControlOf(lobby, 'START BATTLE')` (`:1952`, `:3830`, `:4155`) — and
   its 159 checks pass twice. Same application, same HEAD, same build: only the lookup differs.
3. **Already recorded upstream.** `TASK-221`'s completion record documents this exact defect as
   **R-1 [PRE-EXISTING, reported not fixed]**
   (`tasks/completed/TASK-221-content-ownership-and-relic-loadout.md:510-527`), including the same
   failing string and the same diagnosis. TASK-221 additionally proved attribution by temporarily
   disabling its own pager change and observing the identical failure, establishing the defect
   predates TASK-221.
4. **Not caused by the four unpushed commits.** TASK-239 touched `src/backend/` + `.env.example`
   only; TASK-240 touched `ApiService.ts`, `App.tsx`, `vite-env.d.ts`, and three test files. Neither
   touched `battle-history-smoke.mjs` or any Lobby scene code — confirmed by inspecting each
   commit's file set (§6).
5. **Not an environment issue.** The two sibling suites passed in the same session against the same
   running stack, including the identical Lobby → START BATTLE transition executed by
   `standalone-web-smoke.mjs` (its `phase4b.retryStartedTheBattle` passed).

**Blast radius — same defective lookup in a second, unaffected script:**

```text
src/frontend/client/scripts/battle-history-smoke.mjs:1131   (first battle, fails)
src/frontend/client/scripts/battle-history-smoke.mjs:1364   (second battle, unreachable)
src/frontend/client/scripts/boss-selection-smoke.mjs:516    (same lookup)
src/frontend/client/scripts/boss-selection-smoke.mjs:625    (same lookup)
```

`boss-selection-smoke.mjs` is **not wired into `package.json`**, so it is not part of any documented
verification command; it is reported here for completeness, not as a separate failure.

**Per the task's instruction, this failure was NOT fixed.** No production source, no test, and no
harness file was modified.

### 2.4 Checks not executed

| Check | Reason |
|---|---|
| `verify:e2e:history` full flow (phases 4–9) | Blocked by the pre-existing harness defect above; the suite cannot leave the Lobby |
| `boss-selection-smoke.mjs` | Not exposed as a documented `package.json` script; carries the same defective lookup |
| `verify:runtime`, `verify:viewport*` | Frontend-only helper scripts, not part of the E2E suites named in the task |
| Production Docker image / deployed-host verification | Does not exist (§4) |

---

## 3. PO decision request (Phase C)

### 3.1 Origin of the finding

**TASK-221A — post-implementation audit, finding E-F1** (MEDIUM), at
`tasks/completed/TASK-221A-post-implementation-audit.md:513-544`, restated as a P2 decision at
`:1104` and `:1156-1157`. Current code re-verified this audit at
`src/backend/GameServer.Infrastructure/Postgres/Repositories/PlayerRepository.cs:36-39`.

### 3.2 The question the PO must answer

> **Is the MVP content baseline defined over newly created accounts only?**

Stated so it is answerable in one line: *does the MVP's "5 Pets / 3 Basic Cards / 10 Relics" claim
apply (a) only to accounts created under the TASK-221 baseline, or (b) to all accounts, including
those created before it?*

### 3.3 Current documented and implemented behaviour

```text
IMPLEMENTED (verified this audit, not assumed):
  PlayerRepository.GetOrCreateForAccountAsync returns the existing Player row
  unchanged for any account that already has one (PlayerRepository.cs:36-39).
  The starter grant is composed only on the new-Player branch,
  and the factory deliberately addresses no PlayerId
  (PlayerStarterGrantFactory.cs:166-171), making "creation time only" structural.

DOCUMENTED:
  MVP_SCOPE.md §1  "The whole MVP content set is owned by a newly created Player …
                    it is not a minimum bootstrap" (:105-107);
                    "an account's owned content set is fixed at creation" (:116-120).
  DATABASE.md §2 item 3  starter initialization is reachable only when a new
                    Player row is inserted (per TASK-221A:1357-1359).
  TASK-213 R-3      "An account's owned content set does not change after
                    creation in MVP" (per TASK-221A:524-527).

NET EFFECT: an account whose Player row predates TASK-221 permanently owns
  1 Pet / 3 Basic Cards / 3 Relics and is never topped up.
```

The implementation, `MVP_SCOPE.md`, `DATABASE.md`, and `TASK-213 R-3` are **already mutually
consistent** under reading (a). The decision is therefore a *confirmation*, not a change.

### 3.4 Alternatives explicitly supported by repository evidence

| Alt | Meaning | Repository support | Cost / downstream impact |
|---|---|---|---|
| **(a)** Newly created accounts only *(current behaviour)* | MVP baseline binds accounts created under the TASK-221 grant | `MVP_SCOPE.md:105-107, :116-120`; `DATABASE.md:1352-1359`; `TASK-213 R-3`. Explicitly **recommended by TASK-221A:535-542** | **Zero code change.** One-line product record. Manual QA on a stale account must register a new account or drop the local `postgres_data` volume (a docker-compose named volume) — operational, not product |
| **(b)** All accounts | Every account is topped up to 5/3/10 | No supporting evidence found in `docs/` | Requires a **`GAME_RULES.md` §20 Rule Change**, then a one-off migration/backfill task. Directly contradicts `MVP_SCOPE.md:116-120` ("no acquisition path adds content after creation") and `TASK-213 R-3`. Expands MVP scope |

No third alternative is supported by repository evidence. **The agent does not choose.**

### 3.5 Affected scope and acceptance criteria

```text
Affected surface:  the starter-grant contract only — MVP_SCOPE.md §1
                   ("Content ownership & reachability"), DATABASE.md §2 item 3,
                   PlayerRepository.cs, PlayerStarterGrantFactory.cs.
Unaffected:        all E2E suites register a unique new account every run
                   (standalone-web-smoke.mjs phase 1; collection-viewer-smoke.mjs:546-548;
                   battle-history-smoke.mjs:969), so no automated test depends on
                   either answer. No test is blocked by this decision.
Manual-QA risk:    a human using a pre-TASK-221 account sees 3 Relics (one page,
                   no pager) and 1 selectable Pet, and could misread it as a
                   TASK-221 regression. It is the documented no-backfill rule.
```

### 3.6 Status

```text
PO DECISION REQUIRED  — E-F1 (P2, XS — a one-line product record)
```

**No new task was created to repeat this request**, per the task's instruction; the original TASK-221A
request remains the authoritative record.

---

## 4. Release and deployment readiness (Phase D)

Investigated by searching every tracked `*.md`, `*.yml`, `*.json`, and `*.cs`, and by enumerating
the repository root, `docs/`, and `src/` for deployment artifacts.

**Decisive negative findings:**

```text
Dockerfile (any)                      NOT FOUND  (only docker-compose.yml, infra-only:
                                                 PostgreSQL 17 + Redis 8 for local dev)
.github/ or any CI workflow           NOT FOUND
Deployment/release script             NOT FOUND
Deployment target selection           NOT DOCUMENTED anywhere
Approved build/deploy procedure       NOT DOCUMENTED anywhere
Production runbook                    NOT DOCUMENTED (ADR-019:226 forward-references
                                                 "deployment runbooks" that do not exist)
Production config requirements        NOT DOCUMENTED
ROADMAP §1–§3                         contains NO deployment or release phase
appsettings.json                      no ApplicationSession section, empty
                                      ConnectionStrings, no production CORS origins
```

| Item | Classification | Evidence |
|---|---|---|
| Deployment target selected | **NOT DOCUMENTED** | No provider/platform/host named in `docs/`, `ROADMAP.md`, or any ADR. `docker-compose.yml` is explicitly local-development infrastructure ("Used for local development via WSL Docker"). ADR-002 §"modular monolith" describes one deployable service but names no host |
| Approved build/deploy procedure | **NOT DOCUMENTED** | No CI config, no deploy script, no documented build-for-release command. `npm run build` and `dotnet build` exist but are not documented as a release procedure and no release artifact is defined |
| Production configuration requirements | **NOT DOCUMENTED** | `appsettings.json` ships empty `ConnectionStrings`, no `ApplicationSession` section, and CORS limited to localhost origins. No document states what a production host must set |
| `ApplicationSession:CurrentKey:Secret` secure supply | **DOCUMENTED BUT NOT VERIFIED** in production; **REQUIRES OPERATOR ACTION** | *Channel documented:* ADR-015 **D10** (`:132-140`) — "In production it is read from a host environment variable"; key names `ApplicationSession__CurrentKey__Secret` / `__KeyId` fixed in `src/backend/.env.example:20-44`. *Fail-closed behaviour verified in code:* `ApplicationSessionSigningKeys.CreateHmacKey` throws `ConfigurationException` when absent/blank, refuses < 32 bytes (RFC 7518 §3.2 / ADR-015 D7), and **never echoes the value** (`ApplicationSessionSigningKeys.cs:180-211`). *Not verified:* that any production host supplies it — no host exists |
| PostgreSQL production dependency | **DOCUMENTED BUT NOT VERIFIED** | Required for persistence (ADR-006, `DATABASE.md`); consumed via `ConnectionStrings:DefaultConnection`. No production instance, provisioning, backup, or migration-run procedure documented |
| Redis production dependency | **DOCUMENTED BUT NOT VERIFIED** | Required for active battle state (ADR-005, `REDIS_STATE.md`); consumed via `ConnectionStrings:Redis`. No production instance or durability posture documented. Note `docker-compose.yml` runs Redis with `--save 20 1` — a dev posture, not a documented production decision |
| Existing hosting/release mechanism outside repository evidence | **NONE FOUND** | No external mechanism is referenced by any in-repository document |

### 4.1 MVP scope requirement vs release-process gap

Per the task's instruction, these are distinguished and **MVP scope is NOT expanded to resolve them**:

```text
MVP SCOPE REQUIREMENTS (MVP_SCOPE.md §1 "Technical", IN scope, and satisfied in code):
  Server-authoritative battle resolution   ✅ implemented and verified (Suite 1)
  Realtime communication (SignalR)         ✅ implemented and verified (Suite 1)
  Active battle state store (Redis)        ✅ implemented; Redis reachable in dev
  Persistent storage (PostgreSQL)          ✅ implemented; PostgreSQL reachable in dev

RELEASE-PROCESS GAPS (NOT MVP_SCOPE.md requirements — no document requires them):
  deployment target selection, CI/CD pipeline, production Dockerfile,
  production config/secret provisioning procedure, production runbook.
```

The MVP scope items are implemented; what is absent is the **release process**. That absence is
recorded as a gap and an operator/PO matter — not converted into a new MVP scope requirement, and
not resolved by creating infrastructure in this task (§9).

---

## 5. Acceptance checklist (Phase F)

Legend — **Blocking:** `ACCEPT` = blocks formal acceptance · `RELEASE` = blocks production release ·
`INFO` = informational.
Owner: **agent** · **PO** · **operator**.

### 1. MVP functional scope

| # | Requirement / decision | Evidence | Status | Owner | Required action | Blocking |
|---|---|---|---|---|---|---|
| 1.1 | Playable battle start→finish (register → Lobby → Boss → board → cast → Result) | Suite 1: 159 checks × 2 runs, 0 failures, exit 0 | **VERIFIED** | agent | none | — |
| 1.2 | 8×8 board, Match-3 swap, cascade, Combo | Suite 1 `phase5.board64CellsRendered` (64 labels), `phase5.match3SwapExecuted`, `phase5c.comboCalloutUsesTheDeliveredValue` | **VERIFIED** | agent | none | — |
| 1.3 | 5 Pets / 3 Basic Cards / 10 Relics reachable on a new account | Suite 1 `phase4.starterGrantLoaded`; history `phase4.starterGrantLoaded` = `{pets:5, cards:3, relics:10}` | **VERIFIED** | agent | none | — |
| 1.4 | All 5 MVP Bosses selectable | Suite 1 `phase4.bossSelected`; `standalone-web-smoke.mjs:74-81` canonical list | **VERIFIED** | agent | none | — |
| 1.5 | Relic trigger presentation names the delivered Relic | Suite 1 `phase5cRelic.everyRelicCalloutNamesTheDeliveredRelic`, `.noRawRelicIdentityOrEventTypeIsShown` | **VERIFIED** | agent | none | — |
| 1.6 | Signature Skill identification / presentation | Suite 1 `phase6d.*` (5 checks) | **VERIFIED** | agent | none | — |
| 1.7 | One-cast-per-turn enforcement | Suite 1 `phase6b.*` — server rejects 2nd cast `CARD_CAST_ALREADY_USED_THIS_TURN` | **VERIFIED** | agent | none | — |
| 1.8 | No developer diagnostics / raw event log rendered | Suite 1 `phase5b.noDeveloperDiagnosticsInTheHud`, `.rawEventLogIsNotRendered` | **VERIFIED** | agent | none | — |
| 1.9 | Battle History surface (empty state + real battles) | Partially verified: `phase1`–`phase3` pass (empty state); phases 4–9 blocked by the §2.3 harness defect | **PARTIALLY VERIFIED** | agent | fix harness lookup, then rerun | INFO (see 2.4) |
| 1.10 | Collection Viewer (Pets/Cards/Relics) | Suite 2: 42 checks × 2 runs, 0 failures | **VERIFIED** | agent | none | — |

### 2. Automated and live E2E verification

| # | Requirement / decision | Evidence | Status | Owner | Required action | Blocking |
|---|---|---|---|---|---|---|
| 2.1 | Backend automated suite green at HEAD | Measured this audit: 2,970 passed / 0 failed / 0 skipped | **VERIFIED** | agent | none | — |
| 2.2 | Frontend automated suite green at HEAD | Measured this audit: 944 passed / 0 failed, 23 files | **VERIFIED** | agent | none | — |
| 2.3 | Frontend production build succeeds | `npm run build` exit 0; 82 modules; only pre-existing `>500 kB chunk` warning | **VERIFIED** | agent | none | — |
| 2.4 | Primary live E2E suite green at HEAD | Suite 1: 159 × 2 runs, 0 failures, "NO FLAKINESS" | **VERIFIED** | agent | none | — |
| 2.5 | Collection E2E suite green at HEAD | Suite 2: 42 × 2 runs, 0 failures | **VERIFIED** | agent | none | — |
| 2.6 | Battle-history E2E suite green at HEAD | Suite 3: **FAILS**, exit 1, reproducible ×2 — pre-existing harness defect R-1 (`battle-history-smoke.mjs:1131`) | **FAILED — pre-existing harness defect** | agent | small harness task: address `START BATTLE` by caption (pattern at `standalone-web-smoke.mjs:1952`), also fix `boss-selection-smoke.mjs:516,625` | INFO — does not block acceptance (the same journey passes in Suite 1); **does block** claiming "all E2E suites green" |
| 2.7 | Documented run procedure for the E2E suites | **NOT DOCUMENTED** — no command/prerequisite/start-stop procedure in `AGENTS.md`, `.ai/workflow/quality/testing.md`, or `docs/`; prerequisites recovered from suite preflight code | **NOT DOCUMENTED** | agent + PO | document the E2E procedure and prerequisites in the workflow layer | INFO for acceptance · RELEASE for repeatable release verification |
| 2.8 | No pristine-machine E2E reproducibility check | Not performed; verification used this workstation's pre-existing running PostgreSQL/Redis | **NOT VERIFIED** | operator | confirm suites run from a clean checkout with documented setup | RELEASE |

### 3. Data persistence and battle integrity

| # | Requirement / decision | Evidence | Status | Owner | Required action | Blocking |
|---|---|---|---|---|---|---|
| 3.1 | Server-authoritative resolution; client derives nothing | Suite 1 `phase5c.feedbackMutatesNoAuthoritativeState`; `RuntimeBoundaries.test.ts` (in the 944) | **VERIFIED** | agent | none | — |
| 3.2 | Persistent battle results + rewards | Suite 1 `phase10.realBattleReachedItsOwnResult`, `.deliveredRewardsAreRendered`, `.durationIsRenderedFromTheDeliveredResult` | **VERIFIED** | agent | none | — |
| 3.3 | Active battle state in Redis, recovered on reconnect | Suite 1 `phase6c.*` (6 checks), `phase10b.*` (3 checks); ADR-008 | **VERIFIED** | agent | none | — |
| 3.4 | Persistence suite green against real PostgreSQL | `GameServer.Infrastructure.Tests` 422 passed | **VERIFIED** | agent | none | — |
| 3.5 | Starter-grant creation-only rule holds | Code re-verified: `PlayerRepository.cs:36-39` early return; `PlayerStarterGrantFactory.cs:166-171` | **VERIFIED** | agent | none | — |
| 3.6 | No post-creation content acquisition (MVP rule) | `MVP_SCOPE.md:116-120`; no acquisition path located in code | **VERIFIED (documented + code)** | agent | none | — |
| 3.7 | Production data provisioning / backup / migration-run procedure | **NOT DOCUMENTED** — no production database procedure exists | **NOT DOCUMENTED** | operator + PO | define production data provisioning, migration execution, and backup posture | RELEASE |

### 4. Authentication and production secrets

| # | Requirement / decision | Evidence | Status | Owner | Required action | Blocking |
|---|---|---|---|---|---|---|
| 4.1 | Standalone register / login / session works end-to-end | Suite 1 phase 1–3 (register, session token, re-login via UI); `ApplicationSession.test.ts` (12, in the 944) | **VERIFIED** | agent | none | — |
| 4.2 | Signing secret read from configuration, never tracked files | ADR-015 D10 (`:132-140`); `src/backend/.env.example:20-44`; verified: no secret in `appsettings.json` or `appsettings.Development.json` | **VERIFIED** | agent | none | — |
| 4.3 | Startup fails closed when the secret is absent / too short | Verified in code: `ApplicationSessionSigningKeys.cs:180-211` throws `ConfigurationException`; refuses < 32 bytes; never echoes the value | **VERIFIED (code-level)** | agent | none | — |
| 4.4 | Production supply channel for `ApplicationSession:CurrentKey:Secret` | Documented channel = host environment variable (ADR-015 D10); key name `ApplicationSession__CurrentKey__Secret`. No production host exists to verify against | **DOCUMENTED BUT NOT VERIFIED** | operator | supply the secret via host env vars on the production host once one exists; never via a tracked file | RELEASE |
| 4.5 | Key rotation procedure (manual, overlap, `kid`) | ADR-015 D11 (`:141-147`) — documented; not exercised | **DOCUMENTED BUT NOT VERIFIED** | operator | exercise at least once before/at first production rotation | RELEASE |
| 4.6 | Production CORS origin for the deployed web client | `appsettings.json:13-19` allows only localhost origins. The FE origin must be allowed by the backend CORS policy (`src/frontend/client/.env.example:23-28` documents the requirement) | **REQUIRES OPERATOR ACTION** | operator | add the production frontend origin to the production CORS configuration | RELEASE |

### 5. Deployment and operational readiness

| # | Requirement / decision | Evidence | Status | Owner | Required action | Blocking |
|---|---|---|---|---|---|---|
| 5.1 | Deployment target selected | **NOT DOCUMENTED** — no host/platform named anywhere; `ROADMAP.md` §1–§3 has no deployment phase | **REQUIRES PO DECISION** | PO | select and document the deployment target | RELEASE |
| 5.2 | Approved build/deploy procedure | **NOT DOCUMENTED** — no CI workflow, deploy script, or release artifact definition | **REQUIRES OPERATOR ACTION** | operator + agent | author and approve a build/deploy procedure once 5.1 is decided | RELEASE |
| 5.3 | Production web bundle build | `npm run build` exit 0, emits `dist/` (gitignored) | **VERIFIED (artifact builds)** | agent | none for the build; serving/hosting still 5.1–5.2 | — |
| 5.4 | Local dev infrastructure reproducible | `docker-compose.yml` (PostgreSQL 17 + Redis 8, healthchecks, named volumes) | **DOCUMENTED AND VERIFIED** | agent | none | — |
| 5.5 | Frontend backend-URL configuration | `VITE_API_URL` / `VITE_SIGNALR_URL` honoured since TASK-240 (commit `e26ca11`), with same-origin defaults; `src/frontend/client/.env.example` | **VERIFIED** | agent | none | — |
| 5.6 | Production operational runbook (start/stop/health/rollback) | **NOT DOCUMENTED** — ADR-019:226 forward-references runbooks that do not exist | **NOT DOCUMENTED** | operator + PO | author a production runbook | RELEASE |
| 5.7 | Production Redis / PostgreSQL instances | Not provisioned or documented for production | **NOT DOCUMENTED** | operator | provision and document both, including Redis durability posture | RELEASE |

### 6. Outstanding PO decisions

| # | Decision | Evidence | Status | Owner | Required action | Blocking |
|---|---|---|---|---|---|---|
| 6.1 | **E-F1** — MVP content baseline defined over newly created accounts only? | `TASK-221A:513-544, :1104, :1156-1157`; code `PlayerRepository.cs:36-39`; `MVP_SCOPE.md:105-120`; `DATABASE.md:1352-1359`; `TASK-213 R-3` | **PO DECISION REQUIRED** | PO | record one line confirming alternative (a); see §3 | INFO for acceptance · RELEASE if the PO chooses (b) instead |
| 6.2 | Deployment target selection | See 5.1 | **REQUIRES PO DECISION** | PO | select and document the target | RELEASE |
| 6.3 | Pet Passive Option A decision | Closed; explicitly out of scope for this task | **NOT REOPENED** | — | none | — |

### 7. Source-control and release-source readiness

| # | Requirement / decision | Evidence | Status | Owner | Required action | Blocking |
|---|---|---|---|---|---|---|
| 7.1 | Working tree clean, no uncommitted work | `git status --porcelain` empty at start and end of audit | **VERIFIED** | agent | none | — |
| 7.2 | Local commits carry completed, verified task work | All 4 commits map to completed TASK-239/TASK-240 records; both task records state evidence matching this audit exactly | **VERIFIED** | agent | none | — |
| 7.3 | Divergence from `origin/master` | `0` behind, `4` ahead (re-fetched during this audit) | **VERIFIED** | agent | PO to authorize a push before the release source can include them (§6) | RELEASE |
| 7.4 | Release source includes the TASK-239/TASK-240 work | Recommended: **yes** (see §6) | **RECOMMENDED — awaiting authorization** | PO | authorize the push | RELEASE |
| 7.5 | No push / merge / rebase / history rewrite performed | Confirmed: `origin/master` still `4` behind HEAD | **VERIFIED** | agent | none | — |

---

## 6. Unpushed commits (Phase E)

`origin/master..HEAD` — four commits, re-fetched this audit:

| SHA | Subject | Task | Files | Verification status |
|---|---|---|---|---|
| `8f12b10` | `refactor: retire residual discord configuration and cors origin` | TASK-239 | 5 files: `appsettings.json`, `Program.cs`, `src/backend/.env.example`, `src/frontend/client/.env.example`, `vite-env.d.ts` (+9 / −84) | **COMPLETED & VERIFIED** |
| `daf4f6a` | `docs: file the residual discord configuration retirement completion record` | TASK-239 | 1 file: the TASK-239 completion record (+409) | **COMPLETED record** |
| `e26ca11` | `fix: resolve the client api and battle hub urls from the environment` | TASK-240 | 6 files: `ApiService.ts`, `App.tsx`, `vite-env.d.ts`, `tests/setup.ts`, `BattleService.test.ts`, `AppLifecycle.test.tsx` (+262 / −24) | **COMPLETED & VERIFIED** |
| `5a56ef1` | `docs: file the frontend environment endpoint wiring completion record` | TASK-240 | 1 file: the TASK-240 completion record (+303) | **COMPLETED record** |

**Assessment.**

1. **Completed and verified work.** Both commits use the repository's two-stage protocol (Phase A =
   implementation, Phase B = completion record) required by `TASK_LIFECYCLE.md` §6. Their task
   records claim specific evidence which I **independently reproduced at this HEAD**: TASK-239
   recorded `dotnet test` 2,970 passed / 0 failed and `npm run test:run` 933 passed — I measured
   2,970/0 and 944/0 (the +11 delta is exactly TASK-240's own additions, recorded at
   `TASK-240-*.md:286`). TASK-240 recorded `npm run build` 0 errors and 944 passed / 0 failed — I
   measured both. The claims are accurate, not merely asserted.
2. **The release source must include them.** `e26ca11` is the change that makes `VITE_API_URL` /
   `VITE_SIGNALR_URL` effective in the runtime — i.e. the mechanism a deployed frontend needs to
   address a non-same-origin backend. A release built without it would lose that capability.
   `8f12b10` removes inert Discord configuration, including the `*.discordsays.com` CORS allowance,
   which must not ship.
3. **Dependencies and risks.** The four commits are a clean, self-contained pair of task
   deliverables with no cross-dependency on other unpushed work. The working tree is clean, so no
   partially-staged work is entangled. Two low risks: (a) the retirements in `8f12b10` remove
   configuration a developer might still have locally — inert, but it changes their local
   `.env` expectations; (b) publishing makes these the baseline, so the E-F1 behaviour (§3) becomes
   the published baseline — which is precisely why the PO confirmation should accompany or precede
   the push. Neither risk is a blocker.

**Recommendation:** publish all four commits as a unit. **No push, merge, rebase, or history rewrite
was performed by this task** — confirmed by `git rev-list --left-right --count origin/master...HEAD`
returning `0  4` at the end of the audit. Authorization belongs to the Product Owner.

---

## 7. Final recommendation

### 7.1 Does the evidence support formal PO acceptance review?

```text
YES — acceptance review is supported.
```

The MVP functional surface is implemented and independently re-verified at the current HEAD:
2,970 backend tests, 944 frontend tests, a clean production build, and the primary live E2E suite
passing 159 checks twice with zero failures and zero flakiness. No acceptance-blocking defect was
found. The single outstanding PO decision (E-F1) is a one-line confirmation of behaviour that is
already documented and implemented consistently — it does not gate review, because no automated
test and no documented acceptance criterion depends on either answer. The one failing suite is an
attributed, pre-existing harness defect that does not touch product code, with the equivalent
journey proven green by another suite.

**Proposed disposition:** `ACCEPTANCE CHECKLIST READY — PO DECISION PENDING`. Acceptance itself
remains the Product Owner's act.

### 7.2 Is production release currently supportable?

```text
NO — production release is not currently supportable.
```

Not because the MVP is incomplete, but because the **release process does not exist**:

```text
1. No deployment target has been selected or documented        (REQUIRES PO DECISION)
2. No approved build/deploy procedure or CI pipeline exists     (REQUIRES OPERATOR ACTION)
3. No production configuration requirements are documented      (NOT DOCUMENTED)
4. No production runbook (start/stop/health/rollback) exists    (NOT DOCUMENTED)
5. Production data provisioning/backup posture undefined        (NOT DOCUMENTED)
6. Operator must supply ApplicationSession:CurrentKey:Secret via host environment
   variables, and add the production frontend origin to CORS   (REQUIRES OPERATOR ACTION)
7. Four verified commits are unpushed                            (REQUIRES PO AUTHORIZATION)
```

Items 1–7 are release-process gaps, **not** `MVP_SCOPE.md` §1 requirements. Per the task's
instruction, MVP scope was **not** expanded to absorb them. Closing them requires PO decisions and
operator actions, not gameplay or scope changes.

---

## 8. Scope confirmation

Confirmed for this task:

```text
✔ No production implementation changes
✔ No gameplay rule or scope changes
✔ Pet Passive Option A decision not reopened
✔ No TASK-215B
✔ No speculative feature tasks
✔ No modification of completed task records
✔ No commits, pushes, deployments, or destructive operations
✔ No new task created (no repository workflow required one here,
  and no authorization existed)
✔ Pre-existing user changes preserved — working tree clean before and after
✔ No application configuration changed to force a pass
✔ No secret printed, read, or exposed
✔ No external infrastructure started; no deployment action taken
✔ Failing E2E suite reported, classified, and NOT fixed
```

**Files modified by this task: none.** The audit was read-only apart from starting and stopping the
two documented local application services for Phase B, both of which were shut down and their ports
re-confirmed closed.

**Final state (measured after verification):**

```text
Branch:            master
HEAD:              5a56ef1b96db5718d0b9bd286ae453c9df8c3f30   (unchanged)
Working tree:      clean
Remote divergence: 0 behind, 4 ahead of origin/master        (unchanged)
Push performed:    NO
```

---

## 9. Recommended follow-up (not executed; for PO/agent authorization)

Reported per `AGENTS.md` §16 — discovered, not fixed inline:

```text
F-1  E2E harness defect (pre-existing, R-1 in TASK-221)
     Issue:    Lobby "START BATTLE" resolved as the first enabled Rectangle,
               which is "< BACK"; battle-history-smoke.mjs cannot leave the Lobby.
     Location: src/frontend/client/scripts/battle-history-smoke.mjs:1131, :1364
               src/frontend/client/scripts/boss-selection-smoke.mjs:516, :625
     Impact:   verify:e2e:history fails at Phase 4 (reproducible, exit 1);
               the history flow is not verifiable end-to-end.
     Fix:      resolve the control by its caption, as
               standalone-web-smoke.mjs:1952 already does (lobbyControlOf).
     Suggested follow-up task: a small E2E-harness task covering all four sites.
     Note:     TASK-221:522-527 already recommends exactly this follow-up.

F-2  E2E run procedure undocumented
     Issue:    No documented command, prerequisite list, or start/stop procedure
               for verify:e2e:smoke / :collection / :history.
     Location: .ai/workflow/quality/testing.md; AGENTS.md §6
     Impact:   Suite execution depends on tribal knowledge; blocks repeatable
               release verification and pristine-machine reproduction.
     Fix:      add an .ai/workflow entry documenting prerequisites and procedure.
```

---

## 10. References

- `AGENTS.md`, `docs/AGENTS.md`, `.ai/README.md`, `.ai/workflow/quality/testing.md`
- `tasks/README.md`, `tasks/TASK_LIFECYCLE.md`
- `docs/00-overview/MVP_SCOPE.md` §1 · `docs/00-overview/ROADMAP.md` §1–§3
- `docs/02-technical/DATABASE.md` §2 item 3
- `docs/03-decisions/ADR/ADR-002`, `ADR-005`, `ADR-006`, `ADR-008`, `ADR-015` (D7, D10, D11), `ADR-019`, `ADR-020`
- `tasks/completed/TASK-221A-post-implementation-audit.md` §5.3 (E-F1), §10, §11
- `tasks/completed/TASK-221-content-ownership-and-relic-loadout.md` §10 (R-1)
- `tasks/completed/TASK-239-…md`, `tasks/completed/TASK-240-…md`
- `src/frontend/client/package.json`, `src/frontend/client/scripts/standalone-web-smoke.mjs`, `battle-history-smoke.mjs`, `collection-viewer-smoke.mjs`, `boss-selection-smoke.mjs`
- `src/backend/GameServer.Api/Authentication/ApplicationSessionSigningKeys.cs`, `ApplicationSessionOptions.cs`
- `src/backend/GameServer.Infrastructure/Postgres/Repositories/PlayerRepository.cs`
- `src/backend/GameServer.Api/appsettings.json`, `appsettings.Development.json`
- `src/backend/.env.example`, `src/frontend/client/.env.example`, `docker-compose.yml`
