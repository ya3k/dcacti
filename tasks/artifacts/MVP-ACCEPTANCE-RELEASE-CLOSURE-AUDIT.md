# MVP Acceptance & Release Closure — Verification and Decision Audit

**Date of original audit:** 2026-10-10
**Audited HEAD of the original audit:** `5a56ef1b96db5718d0b9bd286ae453c9df8c3f30`
**Reconciled:** TASK-244, against current `HEAD` `9139531127d004be35cf52cd9aedad390d25eeec`
**Task type:** Verification, release preparation, decision tracking only.
**Authority:** This record does not accept or release the MVP. The Product Owner retains that authority.

> **Scope of this reconciliation (TASK-244).**
> This artifact was written at `5a56ef1` and is reproduced below with its
> observations intact. Seven commits have landed since (TASK-241, TASK-242,
> TASK-243, and this artifact's own filing commit), and several of its findings
> and status claims are now superseded. TASK-244 reconciled the record with
> current repository evidence **without re-running any suite**:
> `S-1` … `S-9` in §11 record what changed, what stands, and what remains open.
> Statements describing the repository **as measured at `5a56ef1`** are
> preserved as historical evidence and are labelled as such where they could
> otherwise be mistaken for current state.

---

## Status

```text
ACCEPTANCE CHECKLIST READY — PO DECISION RECORDED (E-F1 OPTION A)
PRODUCTION RELEASE NOT SUPPORTABLE — RELEASE-PROCESS GAPS REMAIN OPEN
```

**Rationale.** The MVP functional surface was re-verified at the original audit's HEAD (`5a56ef1`):
backend 2,970/2,970, frontend 944/944, and the primary live end-to-end suite 159 checks × 2
runs with 0 failures. Those measurements are **historical** — they were taken at `5a56ef1`, not at
the current HEAD, and TASK-244 did not re-run them (§11 S-2, S-8).

The single outstanding Product Owner decision (E-F1) **has since been made and recorded** — Option A,
by TASK-242 (§3, §11 S-5). **Formal acceptance review** is supported by the evidence below.
**Production release is not currently supportable**, for reasons that are release-process gaps rather
than MVP scope failures (§4, §7, §11 S-9).

---

## 1. Repository baseline (Phase A)

> **Historical.** The table below is the baseline **as measured at `5a56ef1`** during the original
> audit. It is retained as the audit's evidentiary baseline and is **superseded** by §1.1. It is not
> a description of the current repository (TASK-244 measured that separately).

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
| Live E2E at the original HEAD | **Executed by the original audit** (§2) | Not rerun | Superseded |
| Deployment pipeline | **None** — no `Dockerfile`, no `.github/`, no CD pipeline | None | Confirmed (still none at `9139531`) |
| Outstanding PO decision | **1** (E-F1) | 1 | **Superseded** — resolved as Option A by TASK-242 (§3.6, §11 S-5) |

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

### 1.1 Current baseline (measured by TASK-244)

Measured directly, not carried forward from the original audit. **This is the operative baseline**
for every "current" statement in this artifact.

| Item | Measured value | Source |
|---|---|---|
| Branch | `master` | `git rev-parse --abbrev-ref HEAD` |
| HEAD | `9139531127d004be35cf52cd9aedad390d25eeec` | `git rev-parse HEAD` |
| HEAD commit message | `docs: add MVP acceptance and release closure verification and decision audit record` | `git log -1` |
| `origin/master` | `9139531127d004be35cf52cd9aedad390d25eeec` | `git rev-parse origin/master` |
| Divergence | **`0` ahead / `0` behind** — HEAD equals `origin/master` | `git rev-list --left-right --count origin/master...HEAD` |
| Working tree | Clean | `git status --porcelain=v1` |
| Staged entries | None | `git diff --cached --name-status` |
| Target artifact tracked | **Yes** — tracked at mode `100644` | `git ls-files --stage tasks/artifacts/MVP-ACCEPTANCE-RELEASE-CLOSURE-AUDIT.md` |
| Target artifact blob (HEAD / index / worktree) | `38e4b26ce077d1538036f2d1b411840062de6558` in all three | `git rev-parse HEAD:<path>`, `git ls-files --stage`, `git hash-object` |
| `tasks/backlog/`, `tasks/active/`, `tasks/blocked/` | `.gitkeep` only — empty | directory listing |
| Latest completed task records | TASK-241, TASK-242, TASK-243 (all `DONE`) | `tasks/completed/` |

Seven commits landed between the original audit's HEAD (`5a56ef1`) and the current HEAD:

```text
ce15e07   fix: resolve lobby start battle control by caption in history smoke       TASK-241 A
385acfb ﻿ docs: file the battle history e2e harness selector fix completion record  TASK-241 B
dc6f481   docs: record the e-f1 mvp content baseline product owner decision         TASK-242
0a20ab2   docs: record the phase b commit sha in the e-f1 decision record           TASK-242
679df03   docs: document local e2e verification procedure and prerequisites         TASK-243 A
1870eb3   docs: file the local e2e verification procedure completion record         TASK-243 B
9139531   docs: add MVP acceptance and release closure verification and decision audit record
```

**Documentation gap identified at baseline (reconciled).** At the original audit the repository had
**no documented run procedure for the live E2E suites**, and the prerequisites were recovered from
the suites' own preflight code because that was the only in-repository source. **That gap is now
closed as documentation** by TASK-243 — see §11 S-4 for the current classification. The procedures
below (§2.1) are retained as the record of **how the original audit was actually carried out**, which
is a different statement from what is now documented.

---

## 2. E2E verification (Phase B)

> **Historically scoped — read before citing any result in this section.**
> Every command, exit code, and check count in §2 was produced at HEAD `5a56ef1`, during the
> original audit. **They are not results at the current HEAD and TASK-244 did not re-run them.**
> Per `.ai/workflow/quality/local-e2e-verification.md` §9, a suite "passing" is a statement about an
> executed run only; this artifact therefore claims **no fresh current-HEAD E2E success**. Current
> procedure is documented at `.ai/workflow/quality/local-e2e-verification.md` (TASK-243); current
> harness status is recorded in §11 S-2, S-3, and S-8.

### 2.1 Prerequisites and how they were satisfied

The original audit derived these from `standalone-web-smoke.mjs`'s `checkPreflightHealth()` and its
own remediation text, because no procedure document existed at that time (§1, now superseded by
TASK-243):

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

Executed at `5a56ef1` during the original audit. **Not re-executed by TASK-244.**

| # | Command | Exit | Result |
|---|---|---|---|
| 1 | `npm run verify:e2e:smoke` | **0** | RUN 1: 159 checks, 0 failures · RUN 2: 159 checks, 0 failures — "ALL SMOKE TEST RUNS PASSED CLEANLY (NO FLAKINESS)" |
| 2 | `npm run verify:e2e:collection` | **0** | RUN 1: 42 checks, 0 failures · RUN 2: 42 checks, 0 failures — "ALL COLLECTION VIEWER RUNS PASSED CLEANLY (NO FLAKINESS)" |
| 3 | `npm run verify:e2e:history` | **1** | **FATAL, reproducible ×2** at Phase 4 — see §2.3 **(defect since fixed by TASK-241)** |
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

### 2.3 Suite 3 (`verify:e2e:history`) — reproducible failure, classified (**CLOSED by TASK-241**)

> **Status of this finding: CLOSED.** The harness defect described in this subsection was fixed by
> **TASK-241** in commit `ce15e07` (`fix: resolve lobby start battle control by caption in history
> smoke`), recorded in `tasks/completed/TASK-241-battle-history-e2e-harness-selector-fix.md`.
> The failure analysis, root cause, and blast-radius enumeration below are preserved **as historical
> evidence of the defect as it existed at `5a56ef1`** — they no longer describe the current
> `battle-history-smoke.mjs`. See §11 S-2 for the post-fix verification status and its limits.

```text
Command:   npm run verify:e2e:history
Exit code: 1
Output:    [BATTLE HISTORY SMOKE FATAL ERROR]
           Timeout waiting for condition: BattleScene active after START BATTLE (15000ms)
Reproduced: 2 of 2 consecutive attempts, identical failure point
Passing checks before the failure: phase1 (2) + phase2 (6) + phase3 (6) + phase4 partial (3)
```

**Classification (as recorded at `5a56ef1`): PRE-EXISTING HARNESS DEFECT — not a product
regression, not an environment issue, not flaky.** The classification was *attributed by evidence*,
not assumed:

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

**Blast radius — same defective lookup in a second, unaffected script (as measured at `5a56ef1`):**

```text
src/frontend/client/scripts/battle-history-smoke.mjs:1131   (first battle, fails)   ← FIXED by TASK-241
src/frontend/client/scripts/battle-history-smoke.mjs:1364   (second battle, unreachable) ← FIXED by TASK-241
src/frontend/client/scripts/boss-selection-smoke.mjs:516    (same lookup)            ← STILL PRESENT (§11 S-3)
src/frontend/client/scripts/boss-selection-smoke.mjs:625    (same lookup)            ← STILL PRESENT (§11 S-3)
```

Re-verified by TASK-244: `battle-history-smoke.mjs` no longer contains the first-rectangle
battle-start lookup — both phases now resolve the control by caption
(`lobbyControlOf(lobby, 'START BATTLE')`, `:1150` and `:1387`), with fail-fast `MainMenuScene`
guards at `:1157` and `:1394`. The two `boss-selection-smoke.mjs` sites are **unchanged**.

`boss-selection-smoke.mjs` is **not wired into `package.json`**, so it is not part of any documented
verification command; it is reported here for completeness, not as a separate failure.

**Per the task's instruction, this failure was NOT fixed.** No production source, no test, and no
harness file was modified *by the original audit*. The subsequent fix was performed under its own
authorized task (TASK-241), not by the audit.

### 2.4 Checks not executed

As recorded at `5a56ef1`, with current status appended in the final column.

| Check | Reason | Status at current HEAD |
|---|---|---|
| `verify:e2e:history` full flow (phases 4–9) | Blocked by the pre-existing harness defect above; the suite cannot leave the Lobby | **UNBLOCKED** — TASK-241 fixed the lookup (`ce15e07`). Not re-run; see §11 S-2 |
| `boss-selection-smoke.mjs` | Not exposed as a documented `package.json` script; carries the same defective lookup | **UNCHANGED** — still unwired, still defective, deliberately deferred (`.ai/workflow/quality/local-e2e-verification.md` §12.1); see §11 S-3 |
| `verify:runtime`, `verify:viewport*` | Frontend-only helper scripts, not part of the E2E suites named in the task | Unchanged — out of scope |
| Production Docker image / deployed-host verification | Does not exist (§4) | Unchanged — still does not exist; see §11 S-9 |

---

## 3. PO decision request (Phase C) — **RESOLVED: E-F1 = OPTION A**

> **Status of this section: RESOLVED.** The Product Owner decided **E-F1 — Option A**, recorded by
> **TASK-242** in `tasks/completed/TASK-242-record-e-f1-content-baseline-decision.md` (Phase B commit
> `dc6f481`). The decision is **not reopened or reinterpreted** here.
>
> §3.1–§3.5 below are preserved as the **historical decision request** — they are the record of what
> was asked and why, and they remain accurate as an analysis of the repository. §3.6 records the
> resolution. No backfill or top-up behaviour is implemented, authorized, or implied (§11 S-5).

### 3.1 Origin of the finding

**TASK-221A — post-implementation audit, finding E-F1** (MEDIUM), at
`tasks/completed/TASK-221A-post-implementation-audit.md:513-544`, restated as a P2 decision at
`:1104` and `:1156-1157`. Current code re-verified this audit at
`src/backend/GameServer.Infrastructure/Postgres/Repositories/PlayerRepository.cs:36-39`.

### 3.2 The question the PO was asked (historical)

> Retained verbatim as the question put to the Product Owner. It was **answered**: see §3.6.

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

> **Outcome.** The Product Owner chose **(a)**. Recorded as **E-F1 — Option A** by TASK-242; see §3.6.
> The agent did not choose then, and TASK-244 did not choose or reinterpret now.

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
RESOLVED — E-F1 = OPTION A.  Decision recorded by TASK-242 (commit dc6f481).
```

**Decision, as recorded verbatim by TASK-242:**

```text
E-F1 — OPTION A.

The MVP starter content baseline applies exclusively to newly created
accounts. Existing accounts retain their current ownership unchanged.

No backfill or top-up is required.
```

**Binding statements** (`TASK-242` §2): the baseline applies exclusively to newly created accounts
(E-F1-1); existing accounts retain their ownership unchanged (E-F1-2); no backfill, top-up, or
migration task is authorized or required (E-F1-3); the existing creation-time starter-grant rule
remains authoritative and unchanged — the decision **confirms** it (E-F1-4); it resolves no other
finding (E-F1-5); it selects no hosting provider and authorizes no deployment (E-F1-6).

**Consistency.** TASK-242 §5.1 verified that `MVP_SCOPE.md` §1, `DATABASE.md` §2 item 3, and
`TASK-213` R-2/R-3 **already expressed Option A**, so the decision required **zero** edits to any
authoritative document and **zero** code change. It is a confirmation of documented and implemented
behaviour, not a rule change — no `GAME_RULES.md` §20 Rule Change was required.

**No new task was created to repeat this request**, per the original task's instruction; the original
TASK-221A request remains the authoritative record of the question, and TASK-242 is the authoritative
record of the answer.

---

## 4. Release and deployment readiness (Phase D)

Investigated by searching every tracked `*.md`, `*.yml`, `*.json`, and `*.cs`, and by enumerating
the repository root, `docs/`, and `src/` for deployment artifacts.

> **Re-verified by TASK-244 and still accurate.** Each negative finding below was re-measured
> against the current HEAD (`9139531`) and remains true: no CI configuration, no `Dockerfile`, no
> deploy script, and no documented deployment target. See §11 S-9 for the current classification of
> each item.

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

Per the task's instruction, these are distinguished and **MVP scope is NOT expanded to resolve them**.
The "verified" marks below refer to the original audit's E2E runs at `5a56ef1` (§2).

```text
MVP SCOPE REQUIREMENTS (MVP_SCOPE.md §1 "Technical", IN scope, and satisfied in code):
  Server-authoritative battle resolution   ✅ implemented and verified (Suite 1, at 5a56ef1)
  Realtime communication (SignalR)         ✅ implemented and verified (Suite 1, at 5a56ef1)
  Active battle state store (Redis)        ✅ implemented; Redis reachable in dev
  Persistent storage (PostgreSQL)          ✅ implemented; PostgreSQL reachable in dev

RELEASE-PROCESS GAPS (NOT MVP_SCOPE.md requirements — no document requires them):
  deployment target selection, CI/CD pipeline, production Dockerfile,
  production config/secret provisioning procedure, production runbook.
```

The MVP scope items are implemented; what is absent is the **release process**. That absence is
recorded as a gap and an operator/PO matter — not converted into a new MVP scope requirement, and
not resolved by creating infrastructure in this task (§9).

### 4.2 Local database procedure vs production database posture (reconciled)

TASK-244 verified the migration evidence and found one claim below that was **too broad** and is
narrowed here. The distinction matters because local development is documented and reproducible,
while production data posture is not:

```text
LOCAL — DOCUMENTED AND VERIFIED
  Migration files            Present and tracked: 15 EF Core migrations under
                             src/backend/GameServer.Infrastructure/Postgres/Migrations/
                             (20260924130701_AddPlayerPersistence … 20261005120735_
                             AddAccountsTableAndDropDiscordUserId), plus
                             GameDbContextModelSnapshot.cs. Several are content-provisioning
                             migrations (Bosses, Pets, Cards, Relics).
  Apply procedure            Documented: `dotnet ef database update` — the repository's
                             established EF Core workflow, recorded in DATABASE.md
                             (e.g. :256, :999, :1079, :1647) and restated with the exact
                             command in .ai/workflow/quality/local-e2e-verification.md §3.4
                             (TASK-243).
  Automatic migration        NOT performed at startup: Program.cs contains no
                             Database.Migrate() call; applying the schema is an explicit step.
  Schema/applied-state proof The startup-order requirement and the content rows the
                             provisioning migrations insert are documented in
                             .ai/workflow/quality/local-e2e-verification.md §3.4.

PRODUCTION — NOT DOCUMENTED (unchanged by the above)
  Provisioning               No production PostgreSQL/Redis instance is provisioned or documented.
  Migration execution        No production migration-run procedure, ordering, or ownership.
  Backup / restore posture   None documented.
  Data provisioning          No documented production seeding or data-load procedure.

CLASSIFICATION
  Local database + migration documentation  → DOCUMENTED (and consistent with tracked files)
  Production data posture                   → NOT DOCUMENTED / REQUIRES OPERATOR ACTION
```

**Consequence for the original §1 note.** The statement that "no production instance, provisioning,
backup, or migration-run procedure [is] documented" remains correct. What is **not** correct, and is
narrowed above, is any reading that the repository lacks migration *instructions* at all: the local
apply procedure is documented and the 15 migration files are tracked. The gap is
**production-specific**. See §5 table 3.7 and §11 S-6.

No migration was run and no database state was modified by TASK-244.

---

## 5. Acceptance checklist (Phase F)

Legend — **Blocking:** `ACCEPT` = blocks formal acceptance · `RELEASE` = blocks production release ·
`INFO` = informational.
Owner: **agent** · **PO** · **operator**.

> **Scope of this checklist after reconciliation (TASK-244).** Rows describing executed suites are
> marked **(historical)** — they record results produced by the original audit at `5a56ef1`. Rows
> describing code, configuration, or repository state were re-measured at the current HEAD. Rows
> whose subject changed are annotated in place and cross-referenced to §11.

### 1. MVP functional scope

> Rows 1.1–1.8 and 1.10 cite Suite 1 / Suite 2 results **as produced at `5a56ef1`** by the original
> audit. They are historical evidence of the MVP surface, not fresh current-HEAD results (§2).

| # | Requirement / decision | Evidence | Status | Owner | Required action | Blocking |
|---|---|---|---|---|---|---|
| 1.1 | Playable battle start→finish (register → Lobby → Boss → board → cast → Result) | Suite 1 at `5a56ef1`: 159 checks × 2 runs, 0 failures, exit 0 | **VERIFIED (historical)** | agent | none | — |
| 1.2 | 8×8 board, Match-3 swap, cascade, Combo | Suite 1 `phase5.board64CellsRendered` (64 labels), `phase5.match3SwapExecuted`, `phase5c.comboCalloutUsesTheDeliveredValue` | **VERIFIED (historical)** | agent | none | — |
| 1.3 | 5 Pets / 3 Basic Cards / 10 Relics reachable on a new account | Suite 1 `phase4.starterGrantLoaded`; history `phase4.starterGrantLoaded` = `{pets:5, cards:3, relics:10}` | **VERIFIED (historical)** | agent | none | — |
| 1.4 | All 5 MVP Bosses selectable | Suite 1 `phase4.bossSelected`; `standalone-web-smoke.mjs:74-81` canonical list | **VERIFIED (historical)** | agent | none | — |
| 1.5 | Relic trigger presentation names the delivered Relic | Suite 1 `phase5cRelic.everyRelicCalloutNamesTheDeliveredRelic`, `.noRawRelicIdentityOrEventTypeIsShown` | **VERIFIED (historical)** | agent | none | — |
| 1.6 | Signature Skill identification / presentation | Suite 1 `phase6d.*` (5 checks) | **VERIFIED (historical)** | agent | none | — |
| 1.7 | One-cast-per-turn enforcement | Suite 1 `phase6b.*` — server rejects 2nd cast `CARD_CAST_ALREADY_USED_THIS_TURN` | **VERIFIED (historical)** | agent | none | — |
| 1.8 | No developer diagnostics / raw event log rendered | Suite 1 `phase5b.noDeveloperDiagnosticsInTheHud`, `.rawEventLogIsNotRendered` | **VERIFIED (historical)** | agent | none | — |
| 1.9 | Battle History surface (empty state + real battles) | Historical: `phase1`–`phase3` passed (empty state); phases 4–9 were blocked by the §2.3 harness defect. TASK-241 then fixed the selector lookup; its record reports `51/51` checks in each of two runs (historical evidence, **not** a fresh current-HEAD run) | **PARTIALLY VERIFIED (historical) — defect closed by TASK-241; not re-run at current HEAD** | agent | re-run `npm run verify:e2e:history` at the current HEAD to establish a current result | INFO (see 2.6) |
| 1.10 | Collection Viewer (Pets/Cards/Relics) | Suite 2 (at `5a56ef1`): 42 checks × 2 runs, 0 failures. `verify:e2e:collection` → `collection-viewer-smoke.mjs` is still wired at `package.json:14` | **VERIFIED (historical)** | agent | none | — |

### 2. Automated and live E2E verification

> **Read with §2's scope note.** Rows 2.1–2.5 and 2.4/3.1–3.6 below record measurements taken by the
> **original audit at `5a56ef1`**. "Measured this audit" means measured *then*; TASK-244 did not
> re-measure them. Rows carrying **(historical)** were amended to say so explicitly.

| # | Requirement / decision | Evidence | Status | Owner | Required action | Blocking |
|---|---|---|---|---|---|---|
| 2.1 | Backend automated suite green at HEAD | Measured by the original audit at `5a56ef1`: 2,970 passed / 0 failed / 0 skipped | **VERIFIED (historical)** | agent | none recorded; re-run to claim a current result | — |
| 2.2 | Frontend automated suite green at HEAD | Measured by the original audit at `5a56ef1`: 944 passed / 0 failed, 23 files | **VERIFIED (historical)** | agent | none recorded; re-run to claim a current result | — |
| 2.3 | Frontend production build succeeds | `npm run build` exit 0 at `5a56ef1`; 82 modules; only pre-existing `>500 kB chunk` warning | **VERIFIED (historical)** | agent | none recorded; re-run to claim a current result | — |
| 2.4 | Primary live E2E suite green at HEAD | Suite 1 at `5a56ef1`: 159 × 2 runs, 0 failures, "NO FLAKINESS" | **VERIFIED (historical)** | agent | none recorded; re-run to claim a current result | — |
| 2.5 | Collection E2E suite green at HEAD | Suite 2 at `5a56ef1`: 42 × 2 runs, 0 failures | **VERIFIED (historical)** | agent | none recorded; re-run to claim a current result | — |
| 2.6 | Battle-history E2E suite green at HEAD | At `5a56ef1`: **FAILED**, exit 1, reproducible ×2 — pre-existing harness defect R-1 (`battle-history-smoke.mjs:1131`). **Defect fixed by TASK-241** (`ce15e07`); its record reports `51/51` checks × 2 runs. That result is **historical evidence from TASK-241, not a fresh run at the current HEAD** | **DEFECT CLOSED — SUITE NOT RE-VERIFIED AT CURRENT HEAD** | agent | re-run the suite at the current HEAD to claim a current result | INFO for the defect's resolution · blocks claiming "all E2E suites green at HEAD" until re-run |
| 2.7 | Documented run procedure for the E2E suites | **CLOSED AS DOCUMENTATION** by TASK-243: `.ai/workflow/quality/local-e2e-verification.md` (`679df03`) documents the working directory, prerequisites (incl. the signing secret), services/ports, startup order, `dotnet ef database update`, readiness checks, browser discovery, run counts, pass criteria, env overrides, outputs, cleanup, and preflight failures. That document asserts **no** suite result (§1, §9) | **VERIFIED (documented)** — documentation gap closed; suite execution is a separate question | agent | none for the documentation; run the suites to obtain results | INFO for acceptance · procedure now exists for repeatable release verification |
| 2.8 | No pristine-machine E2E reproducibility check | Not performed at the original audit (verification used this workstation's pre-existing PostgreSQL/Redis). TASK-243 documented the steps but performed **no** exercise; its §10 declares this out of scope | **NOT VERIFIED — still outstanding** | operator | perform and record a fresh-clone → documented-steps → suites-pass exercise | RELEASE |

### 3. Data persistence and battle integrity

> Rows 3.1–3.4 cite Suite 1 / backend results produced at `5a56ef1` (historical). Rows 3.5–3.6 rest on
> code and documentation re-verified **at the current HEAD by TASK-244** and are current.

| # | Requirement / decision | Evidence | Status | Owner | Required action | Blocking |
|---|---|---|---|---|---|---|
| 3.1 | Server-authoritative resolution; client derives nothing | Suite 1 `phase5c.feedbackMutatesNoAuthoritativeState`; `RuntimeBoundaries.test.ts` (in the 944) | **VERIFIED (historical)** | agent | none | — |
| 3.2 | Persistent battle results + rewards | Suite 1 `phase10.realBattleReachedItsOwnResult`, `.deliveredRewardsAreRendered`, `.durationIsRenderedFromTheDeliveredResult` | **VERIFIED (historical)** | agent | none | — |
| 3.3 | Active battle state in Redis, recovered on reconnect | Suite 1 `phase6c.*` (6 checks), `phase10b.*` (3 checks); ADR-008 | **VERIFIED (historical)** | agent | none | — |
| 3.4 | Persistence suite green against real PostgreSQL | `GameServer.Infrastructure.Tests` 422 passed (at `5a56ef1`) | **VERIFIED (historical)** | agent | none recorded; re-run to claim a current result | — |
| 3.5 | Starter-grant creation-only rule holds | Code re-verified: `PlayerRepository.cs:36-39` early return; `PlayerStarterGrantFactory.cs:166-171`; reconfirmed by TASK-242 §4.1 | **VERIFIED** | agent | none | — |
| 3.6 | No post-creation content acquisition (MVP rule) | `MVP_SCOPE.md:116-120`; `DATABASE.md:1457-1459`; no acquisition path located in code; confirmed by TASK-242 §4.3 under E-F1 Option A | **VERIFIED (documented + code)** | agent | none | — |
| 3.7 | Production data provisioning / backup / migration-run procedure | **NOT DOCUMENTED** — no *production* database procedure exists. **Narrowed (§4.2):** the 15 tracked migrations and the **local** `dotnet ef database update` apply procedure *are* documented (`DATABASE.md`; `.ai/workflow/quality/local-e2e-verification.md` §3.4). The gap is production-specific: provisioning, migration execution, backup posture, and data load | **NOT DOCUMENTED (production-specific)** | operator + PO | define production data provisioning, migration execution, and backup posture | RELEASE |

### 4. Authentication and production secrets

| # | Requirement / decision | Evidence | Status | Owner | Required action | Blocking |
|---|---|---|---|---|---|---|
| 4.1 | Standalone register / login / session works end-to-end | Suite 1 phase 1–3 at `5a56ef1` (register, session token, re-login via UI); `ApplicationSession.test.ts` (12, in the 944) | **VERIFIED (historical)** | agent | none | — |
| 4.2 | Signing secret read from configuration, never tracked files | ADR-015 D10 (`:132-140`); `src/backend/.env.example:20-44`; verified: no secret in `appsettings.json` or `appsettings.Development.json` | **VERIFIED** | agent | none | — |
| 4.3 | Startup fails closed when the secret is absent / too short | Verified in code: `ApplicationSessionSigningKeys.cs` throws `ConfigurationException` (`:185`, `:195`, `:207`); `MinimumSecretBytes = 32` (`:220`); never echoes the value. Re-confirmed by TASK-244 | **VERIFIED (code-level)** | agent | none | — |
| 4.4 | Production supply channel for `ApplicationSession:CurrentKey:Secret` | Documented channel = host environment variable (ADR-015 D10); key name `ApplicationSession__CurrentKey__Secret`. No production host exists to verify against | **DOCUMENTED BUT NOT VERIFIED** | operator | supply the secret via host env vars on the production host once one exists; never via a tracked file | RELEASE |
| 4.5 | Key rotation procedure (manual, overlap, `kid`) | ADR-015 D11 (`:141-147`) — documented; not exercised | **DOCUMENTED BUT NOT VERIFIED** | operator | exercise at least once before/at first production rotation | RELEASE |
| 4.6 | Production CORS origin for the deployed web client | `appsettings.json:14-18` allows only localhost origins. `Program.cs:39-56` additionally allows localhost/`127.0.0.1` on any port and any `*.trycloudflare.com` host. The FE origin must be allowed by the backend CORS policy (`src/frontend/client/.env.example:23-28` documents the requirement) | **REQUIRES OPERATOR ACTION** | operator | add the production frontend origin to the production CORS configuration | RELEASE |

### 5. Deployment and operational readiness

> Re-verified by TASK-244 against the current HEAD. Rows 5.1, 5.2, 5.6, 5.7 remain **open**; 5.3–5.5
> remain accurate.

| # | Requirement / decision | Evidence | Status | Owner | Required action | Blocking |
|---|---|---|---|---|---|---|
| 5.1 | Deployment target selected | **NOT DOCUMENTED** — no host/platform named anywhere; `ROADMAP.md` §1–§3 has no deployment phase (re-measured by TASK-244) | **REQUIRES PO DECISION** | PO | select and document the deployment target | RELEASE |
| 5.2 | Approved build/deploy procedure | **NOT DOCUMENTED** — no CI workflow, deploy script, or release artifact definition (re-measured) | **REQUIRES OPERATOR ACTION** | operator + agent | author and approve a build/deploy procedure once 5.1 is decided | RELEASE |
| 5.3 | Production web bundle build | `npm run build` exit 0 at `5a56ef1`, emits `dist/` (gitignored) | **VERIFIED (historical)** | agent | none for the build; serving/hosting still 5.1–5.2 | — |
| 5.4 | Local dev infrastructure reproducible | `docker-compose.yml` (PostgreSQL 17-alpine + Redis 8-alpine, healthchecks, named volumes); local procedure documented by TASK-243 | **DOCUMENTED AND VERIFIED (local)** | agent | none | — |
| 5.5 | Frontend backend-URL configuration | `VITE_API_URL` / `VITE_SIGNALR_URL` honoured since TASK-240 (commit `e26ca11`, now published and in `origin/master`), with same-origin defaults; `src/frontend/client/.env.example` | **VERIFIED** | agent | none | — |
| 5.6 | Production operational runbook (start/stop/health/rollback) | **NOT DOCUMENTED** — ADR-019:226 forward-references runbooks that do not exist (re-measured) | **NOT DOCUMENTED** | operator + PO | author a production runbook | RELEASE |
| 5.7 | Production Redis / PostgreSQL instances | Not provisioned or documented for production. `docker-compose.yml` is local-dev only (Redis `--save 20 1` is a dev posture) | **NOT DOCUMENTED / REQUIRES OPERATOR ACTION** | operator | provision and document both, including Redis durability posture | RELEASE |

### 6. Outstanding PO decisions

| # | Decision | Evidence | Status | Owner | Required action | Blocking |
|---|---|---|---|---|---|---|
| 6.1 | **E-F1** — MVP content baseline defined over newly created accounts only? | **RESOLVED by TASK-242**: `tasks/completed/TASK-242-record-e-f1-content-baseline-decision.md` (commit `dc6f481`); decision Option A — newly created accounts only, no backfill | **RESOLVED — OPTION A** | PO (done) | none — decision recorded. No backfill/top-up implementation authorized (§3.6, §11 S-5) | — |
| 6.2 | Deployment target selection | See 5.1 — still no host/platform named anywhere in `docs/`, `ROADMAP.md`, or any ADR | **REQUIRES PO DECISION (open)** | PO | select and document the target | RELEASE |
| 6.3 | Pet Passive Option A decision | Closed; explicitly out of scope for this task. **Not reopened by TASK-242 or TASK-244** | **NOT REOPENED** | — | none | — |

### 7. Source-control and release-source readiness

> **Superseded in part.** The original audit measured `0` behind / `4` ahead of `origin/master` at
> `5a56ef1`. **TASK-244 measured `0` ahead / `0` behind**: HEAD *is* `origin/master`. The commits that
> were local-only at the time of the original audit have since been published. The historical
> publication record is preserved in §6 and labelled as such.

| # | Requirement / decision | Evidence | Status | Owner | Required action | Blocking |
|---|---|---|---|---|---|---|
| 7.1 | Working tree clean, no uncommitted work | `git status --porcelain=v1` empty at TASK-244 preflight | **VERIFIED** | agent | none | — |
| 7.2 | Local commits carry completed, verified task work | At `5a56ef1`, the 4 then-local commits mapped to completed TASK-239/TASK-240 records whose evidence matched. The 7 commits landed since map to TASK-241/TASK-242/TASK-243 records (all `DONE`) and this artifact | **VERIFIED (historical + current)** | agent | none | — |
| 7.3 | Divergence from `origin/master` | **`0` behind, `0` ahead** — HEAD `9139531` equals `origin/master` (`git rev-list --left-right --count origin/master...HEAD` → `0  0`) | **VERIFIED — no divergence** | agent | none | — |
| 7.4 | Release source includes the TASK-239/TASK-240 work | **NOT APPLICABLE** — those commits are already published; `5a56ef1` is an ancestor of `origin/master` (`git merge-base --is-ancestor` exit 0). Nothing remains to publish from that set | **NOT APPLICABLE** | — | none | — |
| 7.5 | No push / merge / rebase / history rewrite performed | Original audit: confirmed. **TASK-244: confirmed** — no push, amend, rebase, reset, or history rewrite; `git rev-list --left-right --count` returned `0  0` before and after | **VERIFIED** | agent | none | — |

---

## 6. Unpushed commits at the time of the original audit (Phase E) — **HISTORICAL PUBLICATION RECORD**

> **Historical — no longer current.** These four commits were local-only when the original audit
> measured `origin/master..HEAD` at `5a56ef1`. **They have since been published:** at the current
> HEAD, `git rev-list --left-right --count origin/master...HEAD` returns `0  0`, and `5a56ef1` is an
> ancestor of `origin/master`. This section is retained only as the **historical publication record**
> of what was awaiting authorization at that time. It must not be read as describing currently
> unpublished work.

`origin/master..HEAD` **at `5a56ef1`** — four commits, re-fetched during the original audit:

| SHA | Subject | Task | Files | Verification status |
|---|---|---|---|---|
| `8f12b10` | `refactor: retire residual discord configuration and cors origin` | TASK-239 | 5 files: `appsettings.json`, `Program.cs`, `src/backend/.env.example`, `src/frontend/client/.env.example`, `vite-env.d.ts` (+9 / −84) | **COMPLETED & VERIFIED** |
| `daf4f6a` | `docs: file the residual discord configuration retirement completion record` | TASK-239 | 1 file: the TASK-239 completion record (+409) | **COMPLETED record** |
| `e26ca11` | `fix: resolve the client api and battle hub urls from the environment` | TASK-240 | 6 files: `ApiService.ts`, `App.tsx`, `vite-env.d.ts`, `tests/setup.ts`, `BattleService.test.ts`, `AppLifecycle.test.tsx` (+262 / −24) | **COMPLETED & VERIFIED** |
| `5a56ef1` | `docs: file the frontend environment endpoint wiring completion record` | TASK-240 | 1 file: the TASK-240 completion record (+303) | **COMPLETED record** |

**Assessment.**

1. **Completed and verified work.** Both commits use the repository's two-stage protocol (Phase A =
   implementation, Phase B = completion record) required by `TASK_LIFECYCLE.md` §6. Their task
   records claim specific evidence which the original audit **independently reproduced at `5a56ef1`**:
   TASK-239 recorded `dotnet test` 2,970 passed / 0 failed and `npm run test:run` 933 passed — the
   audit measured 2,970/0 and 944/0 (the +11 delta is exactly TASK-240's own additions, recorded at
   `TASK-240-*.md:286`). TASK-240 recorded `npm run build` 0 errors and 944 passed / 0 failed — the
   audit measured both. The claims are accurate, not merely asserted.
2. **The release source must include them (satisfied).** `e26ca11` is the change that makes
   `VITE_API_URL` / `VITE_SIGNALR_URL` effective in the runtime — i.e. the mechanism a deployed
   frontend needs to address a non-same-origin backend. `8f12b10` removed inert Discord
   configuration, including the `*.discordsays.com` CORS allowance. **Both are now published**, so
   this requirement is met by the current `origin/master`.
3. **Dependencies and risks.** The four commits are a clean, self-contained pair of task
   deliverables with no cross-dependency on other unpushed work. The working tree is clean, so no
   partially-staged work is entangled. Two low risks: (a) the retirements in `8f12b10` remove
   configuration a developer might still have locally — inert, but it changes their local
   `.env` expectations; (b) publishing makes these the baseline, so the E-F1 behaviour (§3) becomes
   the published baseline — which is precisely why the PO confirmation should accompany or precede
   the push. Neither risk is a blocker.

**Recommendation (historical):** publish all four commits as a unit. **Not applicable now** — they
have been published. **No push, merge, rebase, or history rewrite was performed by the original
audit**, confirmed at the time by `git rev-list --left-right --count origin/master...HEAD` returning
`0  4`; **nor by TASK-244**, which measured `0  0` before and after its edits. Authorization for any
*future* publication belongs to the Product Owner.

---

## 7. Final recommendation

### 7.1 Does the evidence support formal PO acceptance review?

```text
YES — acceptance review is supported.
```

The MVP functional surface was implemented and independently re-verified **at the original audit's
HEAD (`5a56ef1`)**: 2,970 backend tests, 944 frontend tests, a clean production build, and the primary
live E2E suite passing 159 checks twice with zero failures and zero flakiness. **Those measurements
are historical** — TASK-244 did not re-run them, and this artifact claims **no fresh current-HEAD
result** (§2, §11 S-2, S-8).

No acceptance-blocking defect was found. The one failing suite was an attributed, pre-existing
harness defect that does not touch product code, with the equivalent journey proven green by another
suite; that defect was subsequently fixed by TASK-241 (§2.3, §11 S-2).

The single outstanding PO decision (E-F1) **has been made and recorded: Option A**, by TASK-242
(§3.6, §11 S-5). It required no code change and no authoritative-document change, because the rule it
confirms was already documented and implemented consistently.

**Proposed disposition:** `ACCEPTANCE CHECKLIST READY — PO DECISION RECORDED (E-F1 OPTION A)`.
Acceptance itself remains the Product Owner's act.

### 7.2 Is production release currently supportable?

```text
NO — production release is not currently supportable.
```

Not because the MVP is incomplete, but because the **release process does not exist**. Re-verified by
TASK-244 against the current HEAD; each item carries its current classification (§11 S-9):

```text
1. No deployment target has been selected or documented        REQUIRES PO DECISION      (open)
2. No approved build/deploy procedure or CI pipeline exists     REQUIRES OPERATOR ACTION  (open)
3. No production configuration requirements are documented      NOT DOCUMENTED            (open)
4. No production runbook (start/stop/health/rollback) exists    NOT DOCUMENTED            (open)
5. Production data provisioning/backup posture undefined        NOT DOCUMENTED            (open)
   (local migration apply procedure IS documented — §4.2)
6. Operator must supply ApplicationSession:CurrentKey:Secret via host environment
   variables, and add the production frontend origin to CORS   REQUIRES OPERATOR ACTION  (open)
7. Publication of verified commits                             NOT APPLICABLE            (closed —
                                                               HEAD equals origin/master)
```

Items 1–6 are release-process gaps, **not** `MVP_SCOPE.md` §1 requirements. Per the original task's
instruction, MVP scope was **not** expanded to absorb them. Closing them requires PO decisions and
operator actions, not gameplay or scope changes. Item 7 is no longer a gap: the commits the original
audit listed as unpushed are published (§6, §11 S-7).

---

## 8. Scope confirmation

Confirmed for the **original audit**:

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

**Files modified by the original audit: none.** The audit was read-only apart from starting and
stopping the two documented local application services for Phase B, both of which were shut down and
their ports re-confirmed closed.

**Final state of the original audit (measured after verification):**

```text
Branch:            master
HEAD:              5a56ef1b96db5718d0b9bd286ae453c9df8c3f30   (unchanged)
Working tree:      clean
Remote divergence: 0 behind, 4 ahead of origin/master        (unchanged)
Push performed:    NO
```

### 8.1 TASK-244 reconciliation scope

Confirmed for the reconciliation that produced §11:

```text
✔ Documentation-only change — one artifact, no production code or configuration
✔ No E2E suite executed; no fresh current-HEAD result claimed
✔ No migration run and no database state modified
✔ No repair, execution, or rewiring of the legacy boss-selection smoke script
✔ AGENTS.md not modified (the stale .ai/workflow statement is reported, not fixed)
✔ CORS configuration not modified (the *.trycloudflare.com allowance is reported, not fixed)
✔ No hosting provider selected and no deployment architecture defined
✔ E-F1 Option A and Pet Passive Option A not reopened
✔ No follow-up task created
✔ Historical observations preserved and labelled, not rewritten as current measurements
```

**Files modified by TASK-244:** `tasks/artifacts/MVP-ACCEPTANCE-RELEASE-CLOSURE-AUDIT.md` only,
plus this task's own record under `tasks/completed/`.

---

## 9. Recommended follow-up (as recorded at `5a56ef1`; disposition updated)

Reported per `AGENTS.md` §16 — discovered, not fixed inline by the original audit. Current
disposition appended to each; see §11 for the full reconciliation.

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

     ► DISPOSITION (TASK-244): CLOSED IN PART.
       battle-history-smoke.mjs: FIXED by TASK-241 (ce15e07) — both phases now
       resolve by caption (lobbyControlOf, :1150 / :1387) with fail-fast
       MainMenuScene guards. The suite was NOT re-run at the current HEAD, so no
       current passing result is claimed (TASK-241's 51/51 × 2 is historical).
       boss-selection-smoke.mjs:516, :625 — UNCHANGED and deliberately deferred;
       see F-3 below and .ai/workflow/quality/local-e2e-verification.md §12.1.

F-2  E2E run procedure undocumented
     Issue:    No documented command, prerequisite list, or start/stop procedure
               for verify:e2e:smoke / :collection / :history.
     Location: .ai/workflow/quality/testing.md; AGENTS.md §6
     Impact:   Suite execution depends on tribal knowledge; blocks repeatable
               release verification and pristine-machine reproduction.
     Fix:      add an .ai/workflow entry documenting prerequisites and procedure.

     ► DISPOSITION (TASK-244): CLOSED AS DOCUMENTATION by TASK-243 (679df03).
       .ai/workflow/quality/local-e2e-verification.md now documents prerequisites,
       ports, startup order, readiness checks, browser discovery, run counts,
       pass criteria, env overrides, outputs, cleanup, and preflight failures.
       It asserts no suite result (§9 of that document), and TASK-243 executed no
       suite. Procedure documented ≠ procedure executed, and ≠ suite passing.

F-3  Legacy boss-selection smoke script (reported by TASK-244, not fixed)
     Issue:    boss-selection-smoke.mjs retains the stale first-enabled-Rectangle
               Lobby selector at :516 and :625, and is not referenced by any
               package script in src/frontend/client/package.json.
     Status:   Deliberate deferral, documented at
               .ai/workflow/quality/local-e2e-verification.md §12.1 (TASK-243),
               which explicitly directs: do not repair, execute, delete, or
               rewire it as part of running local E2E verification.
     Impact:   None on any documented verification command; it is not wired.
     Action:   NONE — out of scope for TASK-244 and for the deferred posture.
```

---

## 10. References

- `AGENTS.md`, `docs/AGENTS.md`, `.ai/README.md`, `.ai/workflow/quality/testing.md`
- `.ai/workflow/quality/local-e2e-verification.md` (added by TASK-243)
- `tasks/README.md`, `tasks/TASK_LIFECYCLE.md`
- `docs/00-overview/MVP_SCOPE.md` §1 · `docs/00-overview/ROADMAP.md` §1–§3
- `docs/02-technical/DATABASE.md` §2 item 3
- `docs/03-decisions/ADR/ADR-002`, `ADR-005`, `ADR-006`, `ADR-008`, `ADR-015` (D7, D10, D11), `ADR-019`, `ADR-020`
- `tasks/completed/TASK-221A-post-implementation-audit.md` §5.3 (E-F1), §10, §11
- `tasks/completed/TASK-221-content-ownership-and-relic-loadout.md` §10 (R-1)
- `tasks/completed/TASK-241-battle-history-e2e-harness-selector-fix.md` — harness selector fix
- `tasks/completed/TASK-242-record-e-f1-content-baseline-decision.md` — E-F1 Option A decision
- `tasks/completed/TASK-243-document-local-e2e-verification-procedure.md` — E2E procedure
- `tasks/completed/TASK-239-…md`, `tasks/completed/TASK-240-…md`
- `src/frontend/client/package.json`, `src/frontend/client/scripts/standalone-web-smoke.mjs`, `battle-history-smoke.mjs`, `collection-viewer-smoke.mjs`, `boss-selection-smoke.mjs`
- `src/backend/GameServer.Api/Authentication/ApplicationSessionSigningKeys.cs`, `ApplicationSessionOptions.cs`
- `src/backend/GameServer.Infrastructure/Postgres/Repositories/PlayerRepository.cs`
- `src/backend/GameServer.Infrastructure/Postgres/Migrations/` (15 migrations + model snapshot)
- `src/backend/GameServer.Api/appsettings.json`, `appsettings.Development.json`, `Program.cs`
- `src/backend/.env.example`, `src/frontend/client/.env.example`, `docker-compose.yml`

---

## 11. TASK-244 reconciliation of findings S-1 … S-9

**Reconciliation date:** 2026-10-10 · **Baseline:** HEAD `9139531`, `origin/master` `9139531`,
`0` ahead / `0` behind, working tree clean, artifact blob `38e4b26` (§1.1).

**Method.** Each finding below was re-verified against current repository evidence before any change
was made to this artifact. **No E2E suite was executed during this reconciliation**, no migration was
run, no production code or configuration was changed, and no Git history was rewritten. Where a
statement below rests on a task record rather than a fresh measurement, that is stated explicitly.

**Evidence taxonomy** used throughout: `VERIFIED` · `DOCUMENTED BUT NOT VERIFIED` · `NOT DOCUMENTED`
· `REQUIRES OPERATOR ACTION` · `REQUIRES PRODUCT OWNER DECISION` · `NOT APPLICABLE`.

---

### S-1 — Historical baseline `5a56ef1`

**Disposition: RECONCILED.**

The original `5a56ef1` snapshot is preserved as the audit's historical baseline and is **not**
rewritten. It is now explicitly labelled historical and superseded:

| Location | Change |
|---|---|
| Header | Records both the original audited HEAD (`5a56ef1`) and the current HEAD (`9139531`), with a scope note on the reconciliation |
| §1 | Table labelled **Historical** and marked superseded by §1.1 |
| §1.1 | **New** — the operative baseline measured by TASK-244 (branch, HEAD, `origin/master`, divergence, tree/index state, artifact tracking and blob identity, tasks folders, the 7 intervening commits) |
| §2, §6 | Section-level notes scoping their results and commit list to the original HEAD |
| §8 | Original-audit final state separated from TASK-244's reconciliation scope (§8.1) |

No historical observation was restated as a measurement taken at the current HEAD.

---

### S-2 — Battle History harness defect

**Disposition: CLOSED by TASK-241 — historical evidence preserved; no current-HEAD result claimed.**

**Verified.** `src/frontend/client/scripts/battle-history-smoke.mjs` no longer contains the defective
first-enabled-Rectangle battle-start lookup. Both battle-start phases now resolve the control by
caption — `lobbyControlOf(lobby, 'START BATTLE')` at `:1150` and `:1387` — with fail-fast guards that
throw when the active scene is `MainMenuScene` (`:1157`, `:1394`). Commit `ce15e07`
(`fix: resolve lobby start battle control by caption in history smoke`) owns `TASK-241`, declares
exactly that one file, and its completion record is filed as
`tasks/completed/TASK-241-battle-history-e2e-harness-selector-fix.md`.

**Historical evidence preserved.** TASK-241's record reports the suite completing **`51/51` checks in
each of two runs** (102/102 total, 0 failures), with 14 timestamped screenshots under
`battle-history-shots/`. This is retained as TASK-241's verification evidence.

**Explicit limitation — stated, not blurred.** That result is a **historical** verification recorded
by TASK-241. It **does not establish a fresh passing run at the current HEAD**, and TASK-244 did not
execute the suite. TASK-241's own record additionally notes that when it attempted a closure-time
re-test, the local backend (port 5000) and frontend (port 5173) were **offline**, producing the
preflight message `Backend service not reachable at http://localhost:5000/health (fetch failed)`; it
recorded that environmental limitation rather than fabricating output.

**Consequence for this artifact.** §2.3's obsolete defect claims are retained only as historical
evidence, headed by an explicit CLOSED status and cross-referenced here; §2.2's failing row and
§2.4's blocker row are annotated; §5 rows 1.9 and 2.6 no longer describe the defect as current.

**Evidence standard applied:** `.ai/workflow/quality/local-e2e-verification.md` §9 — a suite passing
is a statement about an executed run only, and "if you did not execute a suite, say so explicitly."
**No claim of current-HEAD E2E success is made anywhere in this artifact.**

---

### S-3 — Legacy boss-selection smoke script

**Disposition: PRESERVED — evidence remains accurate.**

**Verified.**

```text
src/frontend/client/scripts/boss-selection-smoke.mjs:516   lobby.interactive.find((o) => o.type === 'Rectangle' && o.enabled)
src/frontend/client/scripts/boss-selection-smoke.mjs:625   same lookup
src/frontend/client/package.json                           no reference to boss-selection
```

The stale lookup is **still present** at both sites, and the script is **still unwired** — the
`package.json` scripts are exactly `verify:runtime`, `verify:e2e:smoke`, `verify:e2e:collection`,
`verify:e2e:history`, `verify:viewport`, `verify:viewport:overflow`, `verify:viewport:shots`.

**Documented deferral cited.** `.ai/workflow/quality/local-e2e-verification.md` **§12.1** records the
script as legacy/orphaned, notes it is not wired and that no CI path runs it, records the stale
selector as a known low-priority defect, and directs: *"Do not repair, execute, delete, or rewire it
as part of running local E2E verification."*

**Action taken: none.** The script was not repaired, executed, deleted, or rewired. This artifact now
carries the finding explicitly as F-3 (§9) rather than as an implied follow-up.

---

### S-4 — Local E2E procedure

**Disposition: CLOSED AS DOCUMENTATION by TASK-243 — documentation is not execution.**

**Verified.** `.ai/workflow/quality/local-e2e-verification.md` exists (`679df03`, owning `TASK-243`),
and TASK-243's completion record is filed under its own Phase B commit (`1870eb3`). Its content
matches the repository's current sources on the points checked: the three package scripts
(`package.json:13-15`), the supported working directory (§2.1), ports 5433 / 6379 / 5000 / 5173
(§3.1), the `dotnet ef database update` apply step (§3.4), and the `boss-selection-smoke.mjs`
deferral (§12.1).

**Documented procedure ≠ executed procedure.** §4.3 of that document declares, and TASK-243's record
confirms, that **TASK-243 did not execute the E2E suites** — no suite was run, and the document
asserts no suite result (its §1: *"Nothing in this document asserts that any suite has passed"*).
This artifact correspondingly upgrades §5 row 2.7 only to **VERIFIED (documented)**, and leaves suite
execution and pristine-machine reproducibility (row 2.8) outstanding.

**Separate unresolved documentation inconsistency — reported, not fixed.** `AGENTS.md` **§19**
(:461) still states that `.ai/agents/`, `.ai/skills/`, and `.ai/workflow/` *"do not exist yet in this
repository as of this document's creation."* That is stale: `.ai/workflow/` now contains
`README.md` plus `architecture/`, `core/`, `development/`, `documentation/`, and `quality/`
subdirectories (including the TASK-243 document), and `.ai/skills/` and `.ai/agents/` are populated.
**`AGENTS.md` was not edited** — it is outside this task's authorized scope. Recorded here as a
distinct open documentation inconsistency, owned by no task in this reconciliation.

---

### S-5 — E-F1 Product Owner decision

**Disposition: RESOLVED by OPTION A (TASK-242) — not reopened.**

**Verified.** `tasks/completed/TASK-242-record-e-f1-content-baseline-decision.md` records the Product
Owner's decision **E-F1 — Option A**, filed under Phase B commit `dc6f481`. The record states that the
MVP starter content baseline applies **exclusively to newly created accounts**, that existing accounts
retain their ownership unchanged, and that **no backfill or top-up is required** (E-F1-1…E-F1-6).

TASK-242 §5.1 further verified that `MVP_SCOPE.md` §1, `DATABASE.md` §2 item 3, and `TASK-213` R-2/R-3
**already expressed Option A**, so the decision required zero edits to authoritative documents and
zero code change — a confirmation, not a rule change.

**Action taken.** §3 is retitled and headed **RESOLVED**, with §3.1–§3.5 preserved as the historical
decision request (the question, the alternatives, and the repository support for each) and §3.6
carrying the recorded decision and its binding statements. §5 row 6.1 now reads **RESOLVED — OPTION
A**. The decision was not reopened or reinterpreted, and **no backfill or top-up behaviour was
implemented or authorized**.

---

### S-6 — Database and migrations

**Disposition: RECONCILED — an over-broad claim is narrowed to the production-specific gap.**

**Verified.** 15 EF Core migration files plus `GameDbContextModelSnapshot.cs` are **tracked** under
`src/backend/GameServer.Infrastructure/Postgres/Migrations/`
(`20260924130701_AddPlayerPersistence` … `20261005120735_AddAccountsTableAndDropDiscordUserId`),
several of them content-provisioning migrations for Bosses, Pets, Cards, and Relics. The **local**
apply procedure is documented as the project's established `dotnet ef database update` workflow
(`DATABASE.md` :256, :999, :1079, :1647) and restated with the exact command in
`.ai/workflow/quality/local-e2e-verification.md` §3.4 (TASK-243). `Program.cs` contains no
`Database.Migrate()` call, so applying the schema is an explicit step.

**Narrowing applied.** The original artifact's blanket implication that migration instructions are
absent is replaced by the §4.2 split: **local database and migration documentation →
`DOCUMENTED`**; **production data posture (provisioning, migration execution, backup/restore,
data load) → `NOT DOCUMENTED` / `REQUIRES OPERATOR ACTION`**. §5 row 3.7 was rewritten to match and
its blocking rationale preserved.

**Action taken: none on the database.** No migration was run and no database state was modified.

---

### S-7 — Publication and commit history

**Disposition: HISTORICAL PUBLICATION RECORD — current publication authorization NOT APPLICABLE.**

**Verified.** HEAD `9139531127d004be35cf52cd9aedad390d25eeec` **equals** `origin/master`;
`git rev-list --left-right --count origin/master...HEAD` returns `0  0`; and
`git merge-base --is-ancestor 5a56ef1 origin/master` exits `0`, so the commits the original audit
listed as local-only are **published**.

**Action taken.** §6 is retitled **"Unpushed commits at the time of the original audit — HISTORICAL
PUBLICATION RECORD"**, headed by a note that they have since been published and must not be read as
currently unpublished; the four-commit table is preserved as the historical publication record, and
its "must include them" recommendation is marked satisfied. §7 is headed **"Superseded in part"**;
rows 7.3 and 7.5 now record `0  0`, and row 7.4 — authorization to publish the TASK-239/TASK-240
work — is **NOT APPLICABLE**. §7.2 item 7 is likewise **NOT APPLICABLE**.

Historical commits are **not** described as currently unpublished anywhere, and **no push, amend,
rebase, reset, or history rewrite was performed**.

---

### S-8 — Collection Viewer E2E

**Disposition: HISTORICAL RESULTS PRESERVED — no fresh current-HEAD run claimed.**

**Verified.** `src/frontend/client/package.json:14` defines
`"verify:e2e:collection": "node scripts/collection-viewer-smoke.mjs"`, and that script path exists.
The wiring is unchanged from the original audit.

**Preserved.** The historical result — Suite 2, `42` checks × 2 runs, 0 failures, exit 0, "ALL
COLLECTION VIEWER RUNS PASSED CLEANLY (NO FLAKINESS)" — and the command that produced it are retained
in §2.1–§2.2 and §5 row 1.10, now marked **VERIFIED (historical)**. No fresh run at the current HEAD
is claimed, because none was performed.

---

### S-9 — Production readiness and outstanding dependencies

**Disposition: RE-VERIFIED — all gaps remain open; hosting remains a PO decision.**

Each item was re-measured against the current HEAD. Local development readiness is classified
separately from production readiness throughout.

| # | Item | Current classification | Evidence measured by TASK-244 |
|---|---|---|---|
| 1 | Hosting target | **REQUIRES PRODUCT OWNER DECISION** | No provider, platform, or host is named in `docs/`, `ROADMAP.md`, or any ADR; `ROADMAP.md` §1–§3 contains no deployment phase. **No provider was chosen by this task.** |
| 2 | Build/deploy procedure | **REQUIRES OPERATOR ACTION** | No `.github/` or CI workflow, no deployment/release script, no defined release artifact. `npm run build` / `dotnet build` exist but are not documented as a release procedure |
| 3 | CI/CD | **NOT DOCUMENTED** | No CI configuration anywhere in the repository |
| 4 | Production runbook | **NOT DOCUMENTED** | ADR-019:226 forward-references "deployment runbooks" that do not exist |
| 5 | PostgreSQL / Redis provisioning (production) | **DOCUMENTED BUT NOT VERIFIED** / **REQUIRES OPERATOR ACTION** | Required by ADR-006/`DATABASE.md` and ADR-005/`REDIS_STATE.md`; consumed via `ConnectionStrings`. Only `docker-compose.yml` exists (PostgreSQL 17-alpine on 5433, Redis 8-alpine on 6379, `command: redis-server --save 20 1`) and it is explicitly local-development infrastructure — a **dev** durability posture, not a production decision. No production instance exists |
| 6 | Production migrations / backups / data provisioning | **NOT DOCUMENTED** (local documentation **DOCUMENTED** — see S-6/§4.2) | 15 migrations tracked; local `dotnet ef database update` documented. No production provisioning, migration-run, backup/restore, or data-load procedure |
| 7 | Session signing-secret supply | **DOCUMENTED BUT NOT VERIFIED** → **REQUIRES OPERATOR ACTION** | Channel documented (ADR-015 D10; key names in `src/backend/.env.example`). Fail-closed behaviour **verified in code**: `ApplicationSessionSigningKeys.cs` throws `ConfigurationException` (`:185`, `:195`, `:207`) and enforces `MinimumSecretBytes = 32` (`:220`). No production host exists to verify supply against |
| 8 | Key rotation | **DOCUMENTED BUT NOT VERIFIED** | ADR-015 D11 documents manual rotation with `kid` overlap; never exercised |
| 9 | Production CORS origin | **REQUIRES OPERATOR ACTION** | `appsettings.json:14-18` lists only localhost origins; `Program.cs:39-56` adds localhost/`127.0.0.1` any-port and `*.trycloudflare.com`. The deployed FE origin must be allowed. **Not modified by this task** — the `trycloudflare.com` allowance is outside this task's remediation scope |
| 10 | Pristine-machine E2E reproducibility | **NOT VERIFIED** | No exercise performed at the original audit or by TASK-243 (that document's §10 declares it out of scope) |

**Local vs production.** Local development readiness is `DOCUMENTED` and internally consistent
(`docker-compose.yml`, the TASK-243 procedure, tracked migrations). **Production readiness is not
claimed anywhere in this artifact**; items 1–9 remain operator/PO actions, and no evidence exists that
a production environment was ever provisioned or verified.

---

### Reconciliation summary

| Finding | Prior status | Reconciled status | Basis |
|---|---|---|---|
| S-1 | `5a56ef1` presented as current | Historical + superseded; §1.1 adds current baseline | Measured Git state |
| S-2 | Harness defect open | **Closed by TASK-241**; 51/51 × 2 preserved as historical, no current-HEAD claim | `ce15e07`; harness source; TASK-241 record |
| S-3 | Legacy script defective, unwired | **Preserved** — both facts re-verified; deliberate deferral cited | Harness source; `package.json`; procedure §12.1 |
| S-4 | Procedure not documented | **Closed as documentation** by TASK-243; not executed; `AGENTS.md` §19 drift reported | `.ai/workflow/quality/local-e2e-verification.md`; TASK-243 record |
| S-5 | PO decision required | **Resolved — Option A** (TASK-242); not reopened | `dc6f481`; TASK-242 record |
| S-6 | Migration instructions implied absent | **Narrowed** to a production-specific gap; local path documented | Migrations directory; `DATABASE.md`; procedure §3.4 |
| S-7 | 4 commits unpushed | **Historical record**; `0  0` divergence; authorization **NOT APPLICABLE** | `git rev-list --left-right --count`; `git merge-base --is-ancestor` |
| S-8 | Collection E2E passing | **Historical** — wiring re-verified; no fresh run claimed | `package.json:14`; script path |
| S-9 | Release-process gaps open | **Re-verified open**; hosting remains a PO decision; local ≠ production | Repository enumeration; ADR-015; `appsettings.json`; `Program.cs` |

```text
No E2E suite was executed during TASK-244.
No fresh current-HEAD E2E success is claimed anywhere in this artifact.
No production code or configuration was changed.
No migration was run and no database state was modified.
No hosting provider or deployment architecture was chosen.
No push and no history rewrite occurred.
```
