# quality/local-e2e-verification.md — Local E2E Verification Procedure

**Version:** 1.0
**Status:** Binding
**Scope:** Running the repository's supported local end-to-end (E2E) browser
suites on a developer machine.

> This document answers: **"How do I run this repository's local E2E
> verification, and what must be running first?"**
>
> It is a **process** document (`.ai/README.md` §5) and is not a source of
> truth for anything. Every game rule, technical contract, and architectural
> decision it touches is owned by `docs/` — this file references those
> documents by path and never restates them (`.ai/README.md` §6,
> `.ai/workflow/documentation/documentation-change.md` §2).
>
> The facts below were verified against the repository's own current sources
> at the time of writing: `src/frontend/client/package.json`, the three
> harness scripts under `src/frontend/client/scripts/`, `docker-compose.yml`,
> `src/backend/GameServer.Api/appsettings.Development.json`,
> `src/backend/GameServer.Api/Properties/launchSettings.json`, and
> `src/frontend/client/vite.config.ts`. **Do not treat this document as
> authority over those files.** If they disagree, the source file is correct
> and this document must be corrected
> (`.ai/workflow/core/context-discovery.md` §3).

---

# 1. What This Document Is, and Is Not

```text
This document IS                This document IS NOT
─────────────────────────────   ─────────────────────────────────────────────
The local procedure for the     A statement that the suites currently pass.
three wired E2E suites.         See §9 — run them to know.

A record of the prerequisites   A substitute for the owning docs. Rules live
those suites require.           in docs/01-game-design/, contracts in
                                docs/02-technical/.

A description of the scripts'   A claim that the stack is reproducible on a
own overrides and outputs.      pristine machine. See §10.
```

**Nothing in this document asserts that any suite has passed.** A suite's
result is established only by executing it and reading its own output (§9).

---

# 2. Prerequisites

## 2.1 Supported working directory

All three suites are npm scripts of the client package:

```text
src/frontend/client
```

Run every command in this document from that directory unless a step says
otherwise. Invoking them from the repository root does not work: the root
`package.json` does not exist, and `/health`-proxying dev server, `vite.config.ts`,
and `node_modules/` all live under `src/frontend/client`.

```text
Repository root          E:\dcacti
Supported working dir    E:\dcacti\src\frontend\client
```

## 2.2 Host tooling

```text
Required                                    Why
──────────────────────────────────────────  ──────────────────────────────────
Node.js (script runner)                     Each suite is a `.mjs` script run
                                            by `node`. The harnesses use the
                                            global `fetch` and `WebSocket`.
npm                                         The suites are invoked as npm
                                            scripts.
.NET SDK (net10.0)                          To build and run GameServer.Api.
Docker (or an equivalent on ports 5433 /    `docker-compose.yml` starts
6379)                                       PostgreSQL and Redis.
A Chromium-based browser                    See §6 — Edge or Chrome. The
                                            suites do not download one.
```

Install client dependencies once, from `src/frontend/client`:

```text
npm install
```

## 2.3 The session signing secret (backend will not start without it)

`GameServer.Api` reads its application-session signing secret from
configuration during composition and **refuses to start** when it is absent.
`ADR-015` D10 forbids that secret from `appsettings.json`,
`appsettings.Development.json`, `.env.example`, source, tests, logs, and every
other committed artifact — so it is **not** in this repository and a fresh
clone cannot start the backend until it is supplied locally.

`src/backend/.env.example` is a **key-name template, not a config source**: a
`.env` file is not one of the host's configuration providers, so copying it
does not configure anything. Supply the values through a provider the host
actually reads. For local development that is the `dotnet user-secrets` store
(the project declares a `UserSecretsId` for exactly this):

```text
dotnet user-secrets --project src/backend/GameServer.Api set "ApplicationSession:CurrentKey:Secret" "<at least 32 random bytes>"
dotnet user-secrets --project src/backend/GameServer.Api set "ApplicationSession:CurrentKey:KeyId" "dev-1"
```

Run these from the repository root. `ApplicationSession:PreviousKey:*` exists
only during a rotation overlap (`ADR-015` D11) and is not needed locally.

Check the store without printing a secret into a tracked file:

```text
dotnet user-secrets --project src/backend/GameServer.Api list
```

> The secret is a credential. Never paste a real value into a tracked file, a
> task record, a commit message, or a report.

---

# 3. Services, Ports, and Startup Order

## 3.1 Required services and their ports

```text
Service       Port   Source of truth
────────────  ─────  ─────────────────────────────────────────────────────────
PostgreSQL    5433   docker-compose.yml (published as "5433:5432"); consumed by
                     ConnectionStrings:DefaultConnection
                     (appsettings.Development.json)
Redis         6379   docker-compose.yml (published as "6379:6379"); consumed by
                     ConnectionStrings:Redis (appsettings.Development.json)
Backend       5000   Properties/launchSettings.json ("http" profile
                     applicationUrl; the profile also sets
                     ASPNETCORE_ENVIRONMENT=Development). appsettings.
                     Development.json sets the same URL. The suites' default
                     BACKEND_URL, and vite.config.ts's proxy target
Frontend      5173   vite.config.ts (server.port); the suites' default
                     FRONTEND_URL
```

PostgreSQL and Redis are required by the **backend**, not directly by the
suites. The suites require PostgreSQL and Redis only in the sense that the
backend they talk to depends on them.

```text
All three suites              PostgreSQL + Redis + backend + frontend
verify:e2e:smoke              additionally requires a Chromium browser (§6)
verify:e2e:collection         additionally requires a Chromium browser (§6)
verify:e2e:history            additionally requires a Chromium browser (§6), and
                              drives real battles that persist BattleResult rows
```

## 3.2 Startup order

```text
1. PostgreSQL + Redis      (docker compose up -d)
        ↓
2. Apply database schema   (dotnet ef database update — §3.4)
        ↓
3. Backend                 (dotnet run — §3.3)
        ↓
4. Frontend dev server     (npm run dev — §3.3)
        ↓
5. E2E suite               (npm run verify:e2e:* — §5)
```

## 3.3 Startup commands

**PostgreSQL and Redis** — from the repository root:

```text
docker compose up -d
```
**Backend** — from the repository root:

```text
dotnet run --project src/backend/GameServer.Api/GameServer.Api.csproj
```

This is the command the suites' own remediation text prints. It binds
`http://localhost:5000` (the `http` launch profile, which also sets
`ASPNETCORE_ENVIRONMENT=Development` and so enables the user-secrets store).

**Frontend** — from `src/frontend/client`:

```text
npm run dev
```

It serves `http://localhost:5173/` and proxies `/api`, `/hubs`, and `/health`
to `http://localhost:5000` (`vite.config.ts`). Because of that proxy, no
frontend environment variable is needed for ordinary local runs — but see
§6.2 and §7.

## 3.4 Apply the database schema

No step in this repository applies migrations automatically at application
startup: `Program.cs` contains no `Database.Migrate()` call. The schema is
applied explicitly through the project's established EF Core workflow
(`dotnet ef`; the apply-path convention is recorded in
`docs/02-technical/DATABASE.md`):

```text
dotnet ef database update --project src/backend/GameServer.Infrastructure --startup-project src/backend/GameServer.Api
```

That single command also inserts the content-defined rows (Bosses, Pets,
Cards, Relics) that the domain rule documents define, because those rows are
provisioned by migrations. **Never** edit database contents by hand to make a
suite pass; if a suite fails, diagnose it — do not manufacture a green result.

## 3.5 Readiness checks

Wait until each dependency answers before running a suite. The suites do their
own preflight (§5.2) and will refuse to start if the backend or frontend is
unreachable; these commands tell you *why* it failed.

```text
Backend      GET http://localhost:5000/health          → "Healthy"
PostgreSQL   pg_isready -U dcacti -d dcacti_db         (healthcheck in
                                                       docker-compose.yml)
Redis        redis-cli ping                            → PONG (healthcheck in
                                                       docker-compose.yml)
Frontend     GET http://localhost:5173/                → serves the app
```

`/health` reports `Healthy` / `Degraded` / `Unhealthy`. PostgreSQL and Redis
are registered with `failureStatus: Degraded`
(`GameServer.Infrastructure/DependencyInjection.cs`), so **`Degraded` is the
signal that a data store is unreachable** — treat it as not ready, not as
ready.

A more detailed JSON view of each individual check is available at:

```text
GET http://localhost:5000/health/detail
```

---

# 4. What the Three Suites Cover

Each suite is a self-contained Node script driving a **real headless browser
over the Chrome DevTools Protocol**. There is no Playwright or Puppeteer
dependency: each script declares its own small CDP client class over a raw
`WebSocket`, and the three share that convention rather than a framework. Each
script records named checks and fails on any `FAIL`.

```text
Package script            Harness file
────────────────────────  ──────────────────────────────────────────────────
verify:e2e:smoke          scripts/standalone-web-smoke.mjs
verify:e2e:collection     scripts/collection-viewer-smoke.mjs
verify:e2e:history        scripts/battle-history-smoke.mjs
```

They are **complementary, not nested**: `verify:e2e:collection` deliberately
does not repeat the battle journey, and states that `standalone-web-smoke.mjs`
owns it. Run the one whose surface you changed; run all three when you want
whole-application confidence.

```text
Suite                  Journey it drives
─────────────────────  ──────────────────────────────────────────────────────
verify:e2e:smoke       The full standalone-web player journey: clean context →
                       register a unique account → starter-grant checks →
                       re-login → MainMenu → Lobby (loadout, every owned
                       Relic reachable, the canonical MVP Bosses, the
                       unselected-Boss guard) → Boss selection →
                       POST /api/battle/start → BattleScene → real pointer
                       swap and cast → terminal outcome → ResultScene →
                       post-result lifecycle (PLAY AGAIN with preserved
                       loadout) → scene-lifecycle teardown checks → zero
                       fatal errors.

verify:e2e:collection  The read-only collection viewer journey: register →
                       MainMenu → COLLECTION → PETS/CARDS/RELICS tabs → item
                       detail → < BACK → reopen. Asserts the viewer performs
                       no battle call, and that its three collection reads
                       total exactly six across the two viewer entries (three
                       reads per entry, none added by switching tabs).

verify:e2e:history     Battle History and account progression: empty history on
                       a fresh account → a real battle played to its own
                       server-resolved terminal outcome by real cell taps →
                       ResultScene → the completed battle visible in history →
                       a second real battle → both visible in delivered order.
```

`verify:e2e:history` negotiates two complete battles turn by turn through real
cell taps, so it is the slowest suite to run. Prefer the suite that owns the
surface you changed; reserve it for changes that touch battle resolution, the
result surface, or the history surface.

---

# 5. Running the Suites

## 5.1 Commands

From `src/frontend/client`, with §3's stack up:

```text
npm run verify:e2e:smoke
npm run verify:e2e:collection
npm run verify:e2e:history
```

Each script also accepts a URL as its first argument, which overrides the
frontend target for that run:

```text
node scripts/standalone-web-smoke.mjs <url>
node scripts/collection-viewer-smoke.mjs <url>
node scripts/battle-history-smoke.mjs <url>
```

## 5.2 Preflight health checks (the most common failure)

Every suite calls `checkPreflightHealth()` **before** launching a browser. It
makes two requests, each with a 3-second timeout:

```text
1. GET  ${BACKEND_URL}/health      default http://localhost:5000/health
2. GET  ${FRONTEND_URL}            default http://localhost:5173/
```

If either fails, the suite aborts with a `FATAL ERROR` naming the service and
the command that starts it. This is a prerequisite failure, **not** a failing
test — see §9.

## 5.3 Default run counts

All three suites run **twice** by default, so that a pass is a statement about
stability across two clean browser contexts rather than about one lucky run:

```text
Suite                  Default runs   Mechanism
─────────────────────  ────────────   ──────────────────────────────────────
verify:e2e:smoke       2              runSmokeTest(1) then runSmokeTest(2)
verify:e2e:collection  2              runCollectionViewerSmokeTest(1) then (2)
verify:e2e:history     2              HISTORY_RUNS, default 2
```

Each run gets a **fresh temporary browser profile** (a new directory under the
OS temp directory), which is what makes each run a clean browser context. Each
run also registers a **brand-new account** — this is the repository's isolation
mechanism: no database rows are deleted, and no test-only backend endpoint
exists. Expect registration-created rows to accumulate locally across runs.

## 5.4 Pass criteria

```text
A suite run PASSES when it records zero failures. On success the script prints
a RUN <n> SUMMARY with "Total checks: <N>, Failures: 0".

The suite as a whole PASSES when every run passes, and the script announces it
in its own words ("ALL SMOKE TEST RUNS PASSED CLEANLY (NO FLAKINESS)", and the
per-suite equivalents for the collection and history scripts) before exiting 0.

The suite FAILS when any check fails: the script throws, prints the fatal
error, and exits with code 1.
```

The number of checks is **not** a fixed target. It grows as checks are added
by other tasks, and the three suites report different totals from one another.
A specific expected count is therefore not a pass criterion — `Failures: 0` is.

## 5.5 Environment overrides

These are read by the harnesses. They are **harness/iteration** controls, not
gameplay configuration.

```text
Variable             Read by                Default                  Effect
───────────────────  ─────────────────────  ───────────────────────  ─────────────────────────────────
URL_UNDER_TEST       all three              http://localhost:5173/   Frontend target when no URL argument
                                                                            is given (argument wins over it).
BACKEND_URL          all three              http://localhost:5000    Backend for the /health preflight.
DEBUG_PORT           smoke 9289,            9291 (collection),       DevTools port the headless browser
                     collection 9291,       9292 (history)           listens on. Give the suites distinct
                     history 9292                                    values if they must run concurrently;
                                                                            a clash means the wrong browser is
                                                                            driven.
EDGE_BIN             all three              (unset)                  Explicit Edge executable path; first
                                                                            entry in the browser candidate list.
CHROME_BIN           all three              (unset)                  Explicit Chrome executable path;
                                                                            second entry in that list.

HISTORY_RUNS         history only           2                        Number of clean-context runs.
HISTORY_DEBUG=1      history only           0 (off)                  Logs every Swap attempt, the
                                                                            committed board, and the page's
                                                                            focus/loop state.
HISTORY_MAX_SWAPS    history only           120                      Caps the Swaps one battle may take
                                                                            before the run reports a failure.
SMOKE_MAX_SWAPS      smoke only             120                      Same cap for the smoke suite's battle.
```

`URL_UNDER_TEST`, `BACKEND_URL`, `DEBUG_PORT`, `EDGE_BIN`, and `CHROME_BIN` are
read by all three suites. The `HISTORY_*` variables exist only in
`battle-history-smoke.mjs` and `SMOKE_MAX_SWAPS` only in
`standalone-web-smoke.mjs` — setting another suite's variable in the
environment has no effect on that suite.

> **`DEBUG_PORT` clashes are a real hazard.** All three default to *different*
> ports, so running them concurrently is possible; if you override
> `DEBUG_PORT`, give each concurrently-running suite its own value.

---

# 6. Browser Discovery

## 6.1 The candidate list

Each suite resolves a headless browser by testing paths **in this order** and
using the first that exists:

```text
1.  $EDGE_BIN
2.  $CHROME_BIN
3.  C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe
4.  C:\Program Files\Microsoft\Edge\Application\msedge.exe
5.  C:\Program Files\Google\Chrome\Application\chrome.exe
6.  C:\Program Files (x86)\Google\Chrome\Application\chrome.exe
7.  /usr/bin/google-chrome
8.  /usr/bin/microsoft-edge
9.  /usr/bin/chromium-browser
10. /usr/bin/chromium
```

Because `EDGE_BIN` is checked first, it takes precedence over **every**
discovered path — including a discovered Edge. Likewise `CHROME_BIN` takes
precedence over all discovered paths below it. If no candidate exists the suite
aborts with:

```text
No supported Chromium browser found. Checked: ... Set EDGE_BIN or CHROME_BIN.
```

The suites do **not** install or download a browser; one must already be
present. Only Edge and Chrome/Chromium are supported (the harness drives
Chromium's DevTools Protocol).

## 6.2 Launch flags

The browser is spawned with `--headless=new`, `--remote-debugging-port=<DEBUG_PORT>`,
`--remote-allow-origins=*`, `--disable-gpu`, `--hide-scrollbars`,
`--window-size=1280,720`, and a fresh `--user-data-dir`. Each suite also
emulates a 1280×720 viewport over CDP.

> The suites emulate a 1280×720 viewport and dispatch clicks as **real pointer
> input** mapped through the live canvas bounds, so the browser window size is
> not a test parameter — do not "fix" a click failure by changing it.

---

# 7. Frontend Configuration and the Proxy

For ordinary local runs the frontend needs **no** environment variables: the
Vite dev server proxies `/api`, `/hubs` (with WebSocket upgrade), and `/health`
to `http://localhost:5000`, so the browser talks to the same origin it was
served from.

`VITE_API_URL` and `VITE_SIGNALR_URL` (template: `src/frontend/client/.env.example`,
copied to the uncommitted `.env.local`) point the client at a backend on a
different origin. They are for non-local frontends — for example a tunneled
backend. Consequences worth knowing before you use them:

```text
- The suites' preflight reads BACKEND_URL, NOT VITE_*; a tunnel-only setup must
  still have a backend reachable at BACKEND_URL for the preflight to pass.
- A frontend served from a non-localhost origin must be allowed by the backend
  CORS policy (Program.cs reads Cors:AllowedOrigins; localhost and 127.0.0.1 on
  any port, and *.trycloudflare.com, are allowed already).
```

Vite inlines every `VITE_*` value into the built bundle, so never put a
credential in one (`src/frontend/client/.env.example`).

---

# 8. Outputs and Artifacts

## 8.1 Screenshots

Each run writes PNG screenshots into a directory **relative to the working
directory** (`src/frontend/client`), named `run<N>-<step>`:

```text
Suite                  Directory                 Package script that produces it
─────────────────────  ────────────────────────  ───────────────────────────────
verify:e2e:smoke       smoke-shots/              verify:e2e:smoke
verify:e2e:collection  collection-viewer-shots/  verify:e2e:collection
verify:e2e:history     battle-history-shots/     verify:e2e:history
```

These directories are ignored by Git (`**/*-shots/` in `.gitignore`), so they
are never committed. Screenshot capture is non-fatal by design: a failure to
write one does not fail the suite.

## 8.2 Console output

The suites print their whole record to stdout — the phase headers, one
`PASS`/`FAIL` line per check, the `RUN <n> SUMMARY` with the check and failure
counts, and finally the all-runs-passed banner. **The console output is the
primary evidence**: a screenshot alone does not show which checks ran.

The browser's own profile directory is a fresh temp directory per run
(`dcacti-smoke-*`, `dcacti-collection-*`, `dcacti-history-*`), not an artifact
you keep.

## 8.3 Common preflight failures

```text
Symptom                                     Cause and fix
──────────────────────────────────────────  ─────────────────────────────────────────
Backend service not reachable at ...        Backend not running, or the wrong port.
/health                                     Start it (§3.3); check GET /health (§3.5).
Frontend dev server not reachable at ...    Vite not running, or the wrong port.
                                            Start it from src/frontend/client (§3.3).
No supported Chromium browser found         No Edge/Chrome at a candidate path. Install
                                            one or set EDGE_BIN / CHROME_BIN (§6.1).
DevTools endpoint ... did not become        A stale browser is holding DEBUG_PORT, or
available                                   the port is taken. Free it or set a
                                            different DEBUG_PORT (§5.5).
Backend exits at startup mentioning the     The session signing secret is missing
session signing key                         (§2.3). Set it in user-secrets.
/health reports Degraded                    PostgreSQL or Redis unreachable (§3.5).
                                            Start them and check docker compose ps.
Timeout waiting for condition: AuthScreen   The page loaded but the app did not mount —
mounted / MainMenuScene active              often a stale build, a frontend error, or a
                                            wrong FRONTEND_URL. Read the console output.
Schema/table errors on registration or      Migrations not applied (§3.4).
battle start
Battle History shows an old battle or a     Expected: a fresh account per run has empty
non-empty history on a "fresh" run          history. A failure here means the run did
                                            not get a new account.
```

---

# 9. Passing a Suite vs. Having Prerequisites

These are different statements, and a report must not blur them.

```text
Prerequisites available
    PostgreSQL, Redis, backend, frontend, browser and the signing secret are
    up and reachable. This is verified by §3.5 and by the suites' own preflight
    (§5.2). It says NOTHING about whether any behavior is correct.

A suite passing
    That suite was executed, it recorded zero failures in every run, and it
    exited 0 (§5.4). This is a statement about the checks the suite actually
    makes — not about untested behavior, and not about the other two suites.

A pristine-machine reproducibility exercise completed
    A different, larger claim: a clean environment (fresh clone, clean
    database, no pre-existing account rows) is prepared from documented steps
    alone and the suites pass there. See §10.
```

**A preflight failure is not a failing test, and a passing suite is not proof
that its prerequisites were correctly configured for someone else.** Never
record "E2E passed" on the strength of a stack that was already running, and
never record a suite as passing without capturing its own output (§8.2).

If you did not execute a suite, say so explicitly. A documentation change must
never imply that unperformed tests passed.

---

# 10. Out of Scope for This Document

This procedure covers **local developer verification on a prepared machine**.
The following are deliberately owned elsewhere and are **not** satisfied by
running the suites:

```text
- A pristine-machine reproducibility exercise (fresh clone → documented steps →
  suites pass). This document describes the steps and the prerequisites, but no
  such exercise has been performed or recorded here.
- CI/CD, continuous verification, or a pipeline that runs these suites. No CI
  configuration exists in this repository.
- Production deployment, hosting, infrastructure, or a production operational
  runbook.
- Acceptance or release sign-off. Suite results inform that work; they are not
  that work.
```

---

# 11. Cleanup

```text
Stop the E2E stack            From the repository root:
                              docker compose down

Stop the dev servers          Ctrl+C in each terminal (backend, frontend).

Screenshots                   Delete smoke-shots/, collection-viewer-shots/,
                              battle-history-shots/ under src/frontend/client.
                              They are Git-ignored, so they never enter a commit.

Browser profiles              Removed automatically at process exit; they live
                              under the OS temp directory (dcacti-*-*).

Stale headless browsers       If a run is killed, a browser may survive and hold
                              DEBUG_PORT. Find and stop it before re-running.

Local accounts                E2E runs create real accounts that persist in the
                              local database and accumulate. Removing them is a
                              database change and is NOT part of this procedure:
                              leave them alone unless you have an explicit,
                              authorized reason to clean the local database.
```

**Never** stop or restart unrelated services to make a suite run, and never
alter database contents to manufacture a passing result. If a suite fails,
report the failure with its output.

---

# 12. Not Part of the Supported Procedure

## 12.1 `boss-selection-smoke.mjs` — legacy/orphaned

`src/frontend/client/scripts/boss-selection-smoke.mjs` is **not** part of the
supported E2E procedure:

```text
- It is not wired into src/frontend/client/package.json (no package script
  references it), unlike the three suites in §4.
- No CI path runs it: this repository contains no CI configuration.
- It is treated here as legacy/orphaned, and its stale Lobby selector lookup
  (it picks the first enabled interactive rectangle rather than resolving the
  START BATTLE control by caption) is a known low-priority defect.
```

It is recorded here only so that a reader does not mistake it for a fourth
supported suite. **Do not repair, execute, delete, or rewire it as part of
running local E2E verification.** If it is ever revived, note that
`battle-history-smoke.mjs` already resolves the same Lobby control correctly by
caption, and that is the pattern to follow.

## 12.2 Other scripts in the same directory

`src/frontend/client/scripts/` contains additional scripts that are **not** E2E
suites and are not described here. Some are other npm scripts
(`verify:runtime`, `verify:viewport`, `verify:viewport:overflow`,
`verify:viewport:shots`); others are not referenced by any package script. Read
`src/frontend/client/package.json` for what is actually wired.

## 12.3 The root wrapper

`scripts/standalone-web-smoke.mjs` at the repository root is a five-line
convenience wrapper that imports the client script. It is not a separate
harness and adds nothing to the procedure; the supported entry point is the
package script (§5.1).

---

# 13. Stop Conditions

Inherited per `.ai/workflow/README.md` §6 and `.ai/README.md` §13. Additionally:

```text
- If a suite fails, report it. Do not edit a harness or a source file to make it
  pass, and do not change database contents to manufacture a green result.
- If a documented step here no longer matches its source file (package.json, a
  harness script, docker-compose.yml, or the backend configuration), the source
  file is authoritative: STOP and correct this document rather than following it.
- If running a suite would require stopping or reconfiguring an unrelated
  service, STOP and report.
```
