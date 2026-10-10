# TASK-239 — Retire Residual Configuration from the Retired Discord Integration

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section; does not copy rules or schemas.
  Created from ADR-020 (Status: Accepted, 2026-10-06), ADR-023 (Status:
  Accepted, 2026-10-09), and the completed TASK-237 and TASK-238 records.
-->

<!--
  Lifecycle: created BACKLOG 2026-10-10 by the orchestrator role at the
  requester's direction. Intake only — no production source, configuration,
  test, or documentation file is modified by this task record's creation.
-->

<!--
  Lifecycle: BACKLOG → READY completed 2026-10-10 by the orchestrator role at the
  requester's direction, after validating the tasks/TASK_LIFECYCLE.md §3 READY
  criteria:
    [x] Task type confirmed (REFACTOR — tasks/TASK_TYPES.md §2 / development/refactor.md)
    [x] Relevant documentation exists in docs/ (ADR-020 D1/D4/D5, ADR-023 §1/§2,
        ADR-015, ARCHITECTURE.md §2.2/§2.2.2/§2.3, MVP_SCOPE.md §1/§4)
    [x] MVP scope confirmed (MVP_SCOPE.md §1 — standalone web account authentication
        is the identity path; Discord is unlisted, hence FUTURE per §4; this task
        adds no scope — it removes residual configuration from the retired integration)
    [x] Not blocked: Dependencies: TASK-237 (DONE, immutable) and TASK-238 (DONE, immutable)
    [x] Primary agent assigned (backend), supporting agents assigned (client, testing, review)
    [x] Workflow assigned (development/refactor.md)
    [x] Acceptance criteria are binary and testable
    [x] Uniqueness: TASK-238 is the highest completed ID and no other record names TASK-239
    [x] Ownership: tasks/active/ and tasks/blocked/ hold no open record, and no other
        backlog record names any declared file
-->

<!--
  Lifecycle: READY → IN PROGRESS completed 2026-10-10 by the backend agent under
  development/refactor.md: preflight completed (AGENTS.md, docs/AGENTS.md,
  .ai/README.md, tasks/README.md, TASK_LIFECYCLE.md, TASK_TYPES.md,
  development/refactor.md, ADR-020, ADR-023, ADR-015); dependencies re-confirmed
  DONE in tasks/completed/ and therefore immutable (TASK-237, TASK-238); the
  declared 5-file set and the two-stage commit protocol re-confirmed against the
  manifest and the then-current HEAD (b89a047); the working tree re-checked
  (no unrelated user change present — only this record, untracked, plus the five
  declared files once edited). Fresh pre-change baselines recorded on the
  untouched tree: `dotnet build src/backend/GameServer.sln` 0 errors / 2 warnings,
  `dotnet test src/backend/GameServer.sln` 2,970 passed / 0 failed / 0 skipped
  (Domain 1,558 · Infrastructure 422 · Api 337 · Application 653),
  `npm run build` 0 errors, `npm run test:run` 933 passed across 23 test files —
  i.e. the manifest's intake baselines still hold.

  Lifecycle: IN PROGRESS → IN REVIEW completed 2026-10-10 by the backend agent
  upon completing the five declared edits: `appsettings.json` JSON-validated with
  the inert `"Discord"` section and its now-dangling comma removed, the
  `*.discordsays.com` CORS allowance and its stale comment removed from
  `Program.cs` with every other origin condition, header, method, credential, and
  policy name preserved, both `.env.example` templates reduced to live
  standalone-web settings, and `VITE_DISCORD_CLIENT_ID` dropped from
  `ImportMetaEnv`. Post-change verification: `dotnet build --no-incremental`
  0 errors with a warning set identical to a pristine HEAD-tree build (52
  MSBuild Warning(s) on both, 62 normalized warning lines), `dotnet test`
  2,970 passed / 0 failed / 0 skipped, `npm run build` 0 errors,
  `npm run test:run` 933 passed across 23 test files. No file outside the
  declared set is modified and nothing was staged.

  Lifecycle: IN REVIEW → DONE completed 2026-10-10 by the review role
  (independent read-only review of the working tree against this manifest,
  ADR-020 D1/D4, ADR-023 §2, `quality/review.md` §1, and
  `quality/scope-validation`): PASS WITH FINDINGS, no blocking finding, no stop
  condition fired — the staged set was exactly the 5 declared paths, the CORS
  delta is a pure narrowing of the allow-list with every other origin condition
  byte-preserved, the residue and consumer scans found no live reader of any
  removed key, and no whitespace, line-ending, or unrelated churn is present.
  The review's non-blocking findings (inert `VITE_API_URL`/`VITE_SIGNALR_URL`
  consumers, gitignored local residue, and the authorized doc-comment and
  `VITE_DEV_AUTH` retentions) are recorded in `## Completion Evidence` and were
  not fixed here — every one of them lies outside the declared file set. The
  declared implementation slice was committed as Phase A commit
  `8f12b106e8804965cfd0825227b316175b142d4b`, and this completion record is
  filed under its own Phase B commit.
-->

---

## Metadata

```text
Task ID:           TASK-239
Type:              REFACTOR
Status:            DONE
Risk:              LOW
Priority:          LOW
Primary Agent:     backend
Supporting Agents: client (frontend template and type declarations), testing (zero-regression suite validation), review (scope validation and security boundary checks)
Workflow:          development/refactor.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis, quality/architecture-conformance, quality/scope-validation
Dependencies:      TASK-237, TASK-238
Declared Files:    src/backend/GameServer.Api/appsettings.json, src/backend/GameServer.Api/Program.cs, src/backend/.env.example, src/frontend/client/.env.example, src/frontend/client/src/vite-env.d.ts
```

---

## Objective

Retire obsolete configuration keys, environment-template documentation, CORS origin conditions, and TypeScript environment declarations leftover from the retired Discord integration across five specific configuration targets (`src/backend/GameServer.Api/appsettings.json`, `src/backend/GameServer.Api/Program.cs`, `src/backend/.env.example`, `src/frontend/client/.env.example`, and `src/frontend/client/src/vite-env.d.ts`), strictly preserving all valid standalone-web authentication, CORS, SignalR, API, database, and local development configurations with zero behavior changes.

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-020-standalone-web-account-authentication.md` D1, D4, D5 — Retirement of Discord components, configuration keys (`Discord:ClientId`, `Discord:ClientSecret`), SDK, and iframe hosting; standalone web browser platform.
- `docs/03-decisions/ADR/ADR-023-backend-discord-identity-seam-resolution.md` §1, §2 — Retirement of backend Discord identity seam, options classes, and DI registrations.
- `docs/03-decisions/ADR/ADR-015-application-session-authentication-contract.md` D6, D10, D11 — Application session signing key and configuration requirements.
- `docs/02-technical/ARCHITECTURE.md` §2.2, §2.2.2, §2.3 — Standalone web client and backend authentication boundaries (reconciled by TASK-238).
- `docs/00-overview/MVP_SCOPE.md` §1, §4 — Standalone web account model is in scope; Discord integration is unlisted/FUTURE.
- `tasks/completed/TASK-237-retire-dormant-backend-discord-identity-seam.md` — Physical removal of dormant backend identity seam and identification of out-of-scope residue.
- `tasks/completed/TASK-238-reconcile-architecture-documentation-and-skills-with-standalone-web.md` — Reconciled architecture docs and confirmed out-of-scope configuration residue.
- `AGENTS.md` §§2, 9, 10, 13, 14, 16, 20; `docs/AGENTS.md` §§2, 4, 16; `.ai/workflow/development/refactor.md` §§1, 2, 4 — Refactoring without behavior change, smallest correct change, scope discipline.

---

## Scope

### In Scope

1. **`src/backend/GameServer.Api/appsettings.json`**:
   - Remove the inert `"Discord"` configuration section (`lines 20-23`) containing `"ClientId"` and `"ClientSecret"`.
   - Remove the trailing comma after the `"Cors"` block (`line 18`) to preserve valid JSON formatting.

2. **`src/backend/GameServer.Api/Program.cs`**:
   - Remove the obsolete `*.discordsays.com` CORS origin condition (`lines 57-59`).
   - Update the line 31 comment to describe CORS for local development and Cloudflare tunnels, dropping the reference to Discord iframe hosting.
   - Preserve all remaining CORS origin rules (`allowedOrigins`, `localhost`/`127.0.0.1` on any port, and `*.trycloudflare.com`) and policy headers/methods/credentials.

3. **`src/backend/.env.example`**:
   - Remove the retired `Development authentication (TASK-181)` section (`lines 46-60`) documenting `DevelopmentAuthentication__Enabled` and `POST /api/auth/discord`.
   - Remove the retired `Discord OAuth (ADR-019 D1)` section (`lines 62-81`) documenting `Discord__ClientId` and `Discord__ClientSecret`.
   - Preserve all valid sections: Database (`ConnectionStrings__DefaultConnection`), Redis (`ConnectionStrings__Redis`), Application session signing key (`ApplicationSession__*`), CORS (`Cors__AllowedOrigins__*`), and ASP.NET Core environment/URLs.

4. **`src/frontend/client/.env.example`**:
   - Remove the retired `VITE_DISCORD_CLIENT_ID` setting (`lines 7-9`).
   - Remove the retired `Development authentication (TASK-181)` section (`lines 10-39`) documenting `VITE_DEV_AUTH`.
   - Update the Cloudflare tunnel section (`lines 40-59`) to describe standalone web tunnel testing without Discord Activity iframe or Discord Developer Portal references.
   - Clean the header (`line 4`) of the Discord Client Secret reference.
   - Preserve valid standalone-web variables `VITE_API_URL` and `VITE_SIGNALR_URL`.

5. **`src/frontend/client/src/vite-env.d.ts`**:
   - Remove the obsolete `readonly VITE_DISCORD_CLIENT_ID?: string;` property declaration (`line 4`).
   - Preserve `VITE_API_URL`, `VITE_SIGNALR_URL`, and `VITE_DEV_AUTH` declarations.

### Out of Scope

- `PlayerRepository.GetOrCreateByDiscordUserIdAsync` or any callers/tests in `GameServer.Infrastructure` or `GameServer.Infrastructure.Tests`.
- `src/frontend/client/package-lock.json` (stale `@discord/embedded-app-sdk` lockfile entry).
- Historical ADRs (`docs/03-decisions/ADR/`) or the ADR index (`docs/03-decisions/README.md`).
- Backend authentication XML documentation (e.g. in `BattleHub.cs:792`, `ApplicationSessionClaims.cs:23-25`, `BattleResultResponse.cs:23`, `Program.cs:167-168`).
- `AGENTS.md` or `docs/AGENTS.md`.
- Product decisions concerning battle abandonment (`TASK-212` §A-16).
- Post-login initialization failure recovery (`NG-13`).
- `src/frontend/client/tests/setup.ts` (`vi.stubEnv('VITE_DEV_AUTH', 'false')`).
- `tests/backend/GameServer.Api.Tests/BattleStartEndpointTests.cs:51` (`private const string DiscordUserId`).
- EF Core database migrations (`src/backend/GameServer.Infrastructure/Postgres/Migrations/`).
- Any changes to standalone-web authentication logic, endpoint contracts, SignalR protocol, game rules, or runtime logic.
- Any other cleanup discovered incidentally.

---

## Current State

The repository has fully migrated to the standalone web architecture (`ADR-020`, `ADR-023`, `TASK-234`, `TASK-235`, `TASK-236`, `TASK-237`, `TASK-238`), but residual configuration files still contain dead keys, obsolete origin allowances, and stale template instructions:

1. `src/backend/GameServer.Api/appsettings.json:20-23`: Contains `"Discord": { "ClientId": "1113385100894277644", "ClientSecret": "" }`. Its consumer `DiscordCredentialOptions` was physically deleted in TASK-237; zero consumers remain.
2. `src/backend/GameServer.Api/Program.cs:31, 57-59`: Line 31 comments "and Discord iframe hosting", and lines 57-59 check `uri.Host.EndsWith(".discordsays.com", ...)`. No Discord iframe hosting exists in standalone web.
3. `src/backend/.env.example:46-81`: Documents `DevelopmentAuthentication__Enabled=true` (referencing deleted `AddDevelopmentDiscordIdentityResolver` and retired `POST /api/auth/discord`) and `Discord__ClientId`/`Discord__ClientSecret` (referencing deleted `DiscordCredentialOptions`).
4. `src/frontend/client/.env.example:7-59`: Contains `VITE_DISCORD_CLIENT_ID`, `VITE_DEV_AUTH` (referencing retired Discord iframe bypass), and tunnel instructions referencing Discord Activity iframe loading and Discord Developer Portal redirects.
5. `src/frontend/client/src/vite-env.d.ts:4`: Declares `readonly VITE_DISCORD_CLIENT_ID?: string;`, which is referenced by zero TypeScript files or runtime code in the repository.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-239 (5 files — all edits):

```text
src/backend/GameServer.Api/appsettings.json
src/backend/GameServer.Api/Program.cs
src/backend/.env.example
src/frontend/client/.env.example
src/frontend/client/src/vite-env.d.ts
```

Evidence per declared path:

| Path | Action | Evidence |
|---|---|---|
| `src/backend/GameServer.Api/appsettings.json` | Edit | Lines 20-23 declare `"Discord"` section. `DiscordCredentialOptions` deleted in TASK-237; zero code consumers repository-wide. Remove section and adjust line 18 comma. |
| `src/backend/GameServer.Api/Program.cs` | Edit | Line 31 comment and lines 57-59 `*.discordsays.com` origin allowance are Discord Activity residue. Surviving CORS policy continues to permit configured origins, localhost on any port, and Cloudflare quick tunnels. |
| `src/backend/.env.example` | Edit | Lines 46-60 (`DevelopmentAuthentication__Enabled`) and lines 62-81 (`Discord__ClientId`/`Secret`) document deleted/retired keys. All database, Redis, JWT session, CORS, and ASP.NET Core variables are preserved. |
| `src/frontend/client/.env.example` | Edit | Lines 7-9 declare `VITE_DISCORD_CLIENT_ID`, lines 10-39 declare `VITE_DEV_AUTH`, and lines 40-59 contain Discord Activity tunnel instructions. Standalone web variables `VITE_API_URL` and `VITE_SIGNALR_URL` are preserved. |
| `src/frontend/client/src/vite-env.d.ts` | Edit | Line 4 declares `readonly VITE_DISCORD_CLIENT_ID?: string;`. Zero TypeScript references exist across `src/` and `tests/`. Remove line 4; preserve `VITE_API_URL`, `VITE_SIGNALR_URL`, and `VITE_DEV_AUTH`. |

*(Note: The task manifest file itself is excluded from the implementation declared file set. The list above strictly matches the `Declared Files:` field in `## Metadata`.)*

---

## Acceptance Criteria

- [x] `src/backend/GameServer.Api/appsettings.json` contains no `"Discord"` configuration section, `"ClientId"`, or `"ClientSecret"`, and remains syntactically valid JSON.
- [x] `src/backend/GameServer.Api/Program.cs` contains no `*.discordsays.com` origin check and no Discord comment, while preserving `allowedOrigins`, `localhost`/`127.0.0.1` on any port, `*.trycloudflare.com`, headers, methods, and credentials. *(Verified: the CORS comment is now `// Configure CORS for local development and Cloudflare tunnels` and the allowance is gone. The file's one remaining "Discord" mention is the ADR-015 D4 authentication doc-comment at `:163`, which this record's Out of Scope list explicitly preserves as authentication XML documentation — recorded in `## Completion Evidence` for transparency.)*
- [x] `src/backend/.env.example` contains no `DevelopmentAuthentication__Enabled`, `Discord__ClientId`, `Discord__ClientSecret`, or `POST /api/auth/discord` documentation, while preserving all Database, Redis, Application session signing key, CORS, and ASP.NET Core variables.
- [x] `src/frontend/client/.env.example` contains no `VITE_DISCORD_CLIENT_ID`, `VITE_DEV_AUTH`, Discord Activity iframe instructions, or Discord Developer Portal references, while preserving `VITE_API_URL` and `VITE_SIGNALR_URL` with accurate standalone web tunnel documentation. *(The CORS/tunnel wording is accurate against `Program.cs`; the independent review additionally found the retained "paste it into `VITE_API_URL`/`VITE_SIGNALR_URL`" step inert because no client code reads those variables — a pre-existing gap outside this file set, recorded in `## Completion Evidence`.)*
- [x] `src/frontend/client/src/vite-env.d.ts` contains no `VITE_DISCORD_CLIENT_ID` property declaration, while preserving `VITE_API_URL`, `VITE_SIGNALR_URL`, and `VITE_DEV_AUTH`.
- [x] Backend solution compiles with zero errors: `dotnet build src/backend/GameServer.sln`.
- [x] Full backend test suite passes with zero newly introduced failures: `dotnet test src/backend/GameServer.sln` (baseline: 2,970 passed, 0 failed across Domain, Application, Infrastructure, Api).
- [x] Frontend builds cleanly with zero errors: `npm run build` in `src/frontend/client`.
- [x] Full frontend test suite passes with zero newly introduced failures: `npm run test:run` in `src/frontend/client` (baseline: 933 passed across 23 test files).
- [x] Zero behavior changes in standalone-web authentication, CORS, SignalR, API contracts, database, or gameplay. *(The single intentional CORS change is the removal of the retired `*.discordsays.com` allowance, required by this task and authorized by ADR-020 D1.)*
- [x] Exactly the 5 declared files are modified; no file outside the declared set is staged (P-5).
- [x] Two-stage commit protocol followed (`TASK_LIFECYCLE.md` §6): Phase A commits exactly the 5 declared files with `owns: TASK-239` in the body and no task ID in the subject; Phase B files this record under its own commit.
- [x] Completion Evidence records the Phase A SHA, the actual commands run, the observed results, and the changed files.

---

## Affected Files & Areas

```text
[x] src/backend/GameServer.Api/ (appsettings.json, Program.cs)
[x] src/backend/ (.env.example)
[x] src/frontend/client/ (.env.example, src/vite-env.d.ts)
[ ] tests/ (no test files declared)
[ ] docs/ (no documentation files declared)
```

---

## Implementation Notes

### Critical CORS analysis (`Program.cs`)

The current `FrontendPolicy` in `Program.cs` implements:
```csharp
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? ["http://localhost:5173", "https://localhost:5173", "http://127.0.0.1:5173"];

// ...
policy.SetIsOriginAllowed(origin =>
{
    if (string.IsNullOrEmpty(origin)) return false;
    if (allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)) return true;

    var uri = new Uri(origin);
    if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
        return true;

    if (uri.Host.EndsWith(".trycloudflare.com", StringComparison.OrdinalIgnoreCase))
        return true;

    if (uri.Host.EndsWith(".discordsays.com", StringComparison.OrdinalIgnoreCase))
        return true;

    return false;
})
```
- Removing `if (uri.Host.EndsWith(".discordsays.com", StringComparison.OrdinalIgnoreCase)) return true;` does NOT weaken or broaden CORS policy. It eliminates an obsolete origin condition permitting third-party iframe proxy origins that the application no longer uses under `ADR-020`.
- The standalone web client requires:
  1. Configured origins (`Cors:AllowedOrigins`)
  2. Local development (`localhost`, `127.0.0.1` on any port)
  3. Remote testing tunnels (`*.trycloudflare.com`)
  All three remain active and intact.
- Update the line 31 comment to:
  `// Configure CORS for local development and Cloudflare tunnels`

### Critical environment-template analysis

- **`src/backend/.env.example`**:
  - Preserve: Lines 1-12 (Header), Lines 14-16 (PostgreSQL), Lines 17-19 (Redis), Lines 20-45 (Application session signing key), Lines 82-85 (CORS `Cors__AllowedOrigins__*`), Lines 86-88 (ASP.NET Core `ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_URLS`).
  - Remove: Lines 46-60 (`Development authentication (TASK-181)`), Lines 62-81 (`Discord OAuth (ADR-019 D1)`).

- **`src/frontend/client/.env.example`**:
  - Preserve: Header (lines 1-5, cleaned of Discord Client Secret wording), `VITE_API_URL=` (line 60), `VITE_SIGNALR_URL=` (line 61).
  - Remove: Lines 7-9 (`VITE_DISCORD_CLIENT_ID`), Lines 10-39 (`Development authentication (TASK-181)`).
  - Update: Lines 40-59 (Cloudflare Tunnel section) to explain configuring backend tunnel URLs for standalone web development/testing, without referencing Discord Activity iframe loading or Discord Developer Portal OAuth2 redirects.

### Critical TypeScript declaration analysis (`vite-env.d.ts`)

- `VITE_DISCORD_CLIENT_ID` has zero references in `src/` or `tests/`. Removing `readonly VITE_DISCORD_CLIENT_ID?: string;` from `ImportMetaEnv` is safe and leaves no broken consumers.
- Preserve `VITE_API_URL`, `VITE_SIGNALR_URL`, and `VITE_DEV_AUTH`. Note: `tests/setup.ts` stubs `vi.stubEnv('VITE_DEV_AUTH', 'false')`; keeping `VITE_DEV_AUTH` in `vite-env.d.ts` avoids touching test code, which is out of scope.

### Explicit preserve list (do not touch)

```text
src/backend/GameServer.Infrastructure/Postgres/Repositories/PlayerRepository.cs:102
    GetOrCreateByDiscordUserIdAsync is [Obsolete] but maintained for legacy tests. Out of scope.
src/frontend/client/package-lock.json:11
    Stale @discord/embedded-app-sdk lockfile entry. Out of scope.
src/frontend/client/tests/setup.ts:34-40
    vi.stubEnv('VITE_DEV_AUTH', 'false') in test setup. Out of scope.
tests/backend/GameServer.Api.Tests/BattleStartEndpointTests.cs:51
    private const string DiscordUserId const. Bare string literal. Out of scope.
src/backend/GameServer.Api/Hubs/BattleHub.cs:792
src/backend/GameServer.Api/Authentication/ApplicationSessionClaims.cs:23-25
src/backend/GameServer.Api/Controllers/BattleResultResponse.cs:23
src/backend/GameServer.Api/Program.cs:167-168
    Doc-comments explaining security boundary and that Discord tokens are rejected. Out of scope.
```

---

## Testing Requirements

### Required Verification

```text
[x] Configuration validation — appsettings.json remains valid JSON and parses correctly
[x] CORS policy verification  — Program.cs compiles and correctly permits localhost, trycloudflare.com, and configured origins
[x] Template consistency      — backend and frontend .env.example accurately describe current standalone web variables
[x] TypeScript compilation    — npm run build succeeds with zero type errors in src/frontend/client
[x] Zero-regression suites   — backend and frontend test suites pass with no newly introduced failures
```

### Commands

```text
dotnet build src/backend/GameServer.sln
dotnet test src/backend/GameServer.sln
npm run build         # in src/frontend/client
npm run test:run      # in src/frontend/client
```

### Baseline Recorded at Intake (2026-10-10)

- Backend build: `dotnet build src/backend/GameServer.sln` — 0 errors, 2 warnings (EF Core relational conflicts in tests)
- Backend tests: `dotnet test src/backend/GameServer.sln` — 2,970 passed / 0 failed / 0 skipped (Domain 1,558, Infrastructure 422, Api 337, Application 653)
- Frontend build: `npm run build` in `src/frontend/client` — 0 errors
- Frontend tests: `npm run test:run` in `src/frontend/client` — 933 passed / 0 failed across 23 test files

### Key Edge Cases

- **JSON syntax corruption:** Removing `"Discord"` from `appsettings.json` requires removing the trailing comma on the preceding `"Cors"` property (`line 18`), or the host fails to parse configuration on boot.
- **Accidental CORS lockdown:** Do not remove the `trycloudflare.com` check or `localhost`/`127.0.0.1` checks; only remove the `discordsays.com` check.
- **Accidental deletion of essential environment settings:** Ensure `ConnectionStrings__*`, `ApplicationSession__*`, `Cors__AllowedOrigins__*`, and `ASPNETCORE_*` are preserved byte-for-byte in `src/backend/.env.example`.

---

## Stop Conditions

- If a file outside `## Declared File Set (P-2 / P-5)` requires a change to make the build or tests pass: STOP per `TASK_LIFECYCLE.md` §6.1 step 2 / P-5 — report the divergence; do not silently expand the declared set.
- If removing `*.discordsays.com` breaks any active development mode, test, or deployment: STOP and report.
- If any production code or test is found to depend on `VITE_DISCORD_CLIENT_ID` or `Discord:ClientId`: STOP and report the consumer.
- If removing configuration requires a new architectural or security decision: STOP per `AGENTS.md` §18 / §20.
- If task execution splits into more than one independently verifiable slice: STOP and report rather than expanding scope.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Commit
- Implementation slice (Phase A): `8f12b106e8804965cfd0825227b316175b142d4b`
- Subject: `refactor: retire residual discord configuration and cors origin`
- Owns: `TASK-239`
- Files: `src/backend/GameServer.Api/appsettings.json, src/backend/GameServer.Api/Program.cs, src/backend/.env.example, src/frontend/client/.env.example, src/frontend/client/src/vite-env.d.ts`
- Shared with: `none`
- Unowned / pre-existing: `none`
- Staging verification (P-5): `git diff --cached --name-status` matched this record's canonical `Declared Files` exactly — 5 modifications, zero extraneous files, task record excluded (`8f12b10`, `5 files changed, 9 insertions(+), 84 deletions(-)`).

### Changed Files
- `src/backend/GameServer.Api/appsettings.json` — removed the inert top-level `"Discord"` section (`ClientId`, `ClientSecret`; its only configuration contract, `DiscordCredentialOptions`, was deleted under TASK-237 and no consumer replaced it) and the comma left dangling after the `"Cors"` object, so the file stays valid JSON. `Logging`, `AllowedHosts`, `ConnectionStrings`, and `Cors.AllowedOrigins` (3 origins) are byte-preserved; the pre-existing no-trailing-newline state is preserved.
- `src/backend/GameServer.Api/Program.cs` — deleted the `*.discordsays.com` branch of `FrontendPolicy.SetIsOriginAllowed` and the comment above it (Discord Activity iframe hosting residue, retired by ADR-020 D1), and corrected the policy comment to `// Configure CORS for local development and Cloudflare tunnels`. Nothing else in the policy moved: the `Cors:AllowedOrigins` read and its 3-origin default array, the configured-origin check, the `localhost`/`127.0.0.1` any-port rule, the `*.trycloudflare.com` rule, `AllowAnyHeader`, `AllowAnyMethod`, `AllowCredentials`, and the policy name `FrontendPolicy` are all unchanged. The remaining Discord-named doc-comment in the same file is the ADR-015 D4 authentication boundary note, which this manifest declares out of scope.
- `src/backend/.env.example` — deleted the `Development authentication (TASK-181)` section (`DevelopmentAuthentication__Enabled=true`, whose options class and unreachable resolver were deleted under TASK-237, together with its retired `POST /api/auth/discord` prose) and the `Discord OAuth (ADR-019 D1)` section (`Discord__ClientId`, `Discord__ClientSecret`; ADR-019 is superseded by ADR-020 D1 item 4). All valid sections are preserved with their comments and boundaries: `ConnectionStrings__DefaultConnection`, `ConnectionStrings__Redis`, the ADR-015 D10 signing-key commentary and all four `ApplicationSession__*` key names, `Cors__AllowedOrigins__0/1`, `ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_URLS`.
- `src/frontend/client/.env.example` — deleted the `VITE_DISCORD_CLIENT_ID` setting and the `Development authentication (TASK-181)` `VITE_DEV_AUTH` section; removed the Discord Client Secret wording from the header (replaced with the accurate Vite inlining warning); rewrote the Cloudflare tunnel section for standalone-web testing — no Discord Activity iframe, no Discord Developer Portal OAuth2 redirect step, and the CORS step now states what the backend actually allows (any `localhost`/`127.0.0.1` port and any `*.trycloudflare.com` origin, per `Program.cs`). `VITE_API_URL` and `VITE_SIGNALR_URL` are preserved.
- `src/frontend/client/src/vite-env.d.ts` — deleted `readonly VITE_DISCORD_CLIENT_ID?: string;` (zero references in `src/` and `tests/`); `VITE_API_URL`, `VITE_SIGNALR_URL`, and `VITE_DEV_AUTH` are preserved, the last of these because `tests/setup.ts:40` still stubs it and this manifest declares that file out of scope.

### Validation Results
| Command | Pre-change baseline (recorded on the untouched tree at `b89a047`) | Post-change |
|---|---|---|
| `dotnet build src/backend/GameServer.sln` | 0 errors / 2 warnings (incremental; up-to-date projects emit nothing) | 0 errors |
| `dotnet build src/backend/GameServer.sln --no-incremental` | 0 errors / 52 Warning(s) — measured on a pristine `git archive HEAD` tree in a temp directory | 0 errors / 52 Warning(s); normalized warning sets compared with `Compare-Object` — **identical** (62 unique normalized warning lines each) |
| `dotnet test src/backend/GameServer.sln` | PASS 2,970 passed / 0 failed / 0 skipped (Domain 1,558 · Infrastructure 422 · Api 337 · Application 653) | PASS 2,970 passed / 0 failed / 0 skipped (identical split) |
| `npm run build` (src/frontend/client) | 0 errors (pre-existing Rollup `PURE`-comment advisories and the >500 kB chunk-size advisory only) | 0 errors, same advisories only |
| `npm run test:run` (src/frontend/client) | 933 passed across 23 test files | 933 passed across 23 test files |

Additional verification:
- **JSON validity** — `Get-Content ... -Raw | ConvertFrom-Json` on `appsettings.json`: valid; top-level keys `Logging`, `AllowedHosts`, `ConnectionStrings`, `Cors`; `Cors.AllowedOrigins` count 3.
- **CORS before/after** — `git show HEAD:src/backend/GameServer.Api/Program.cs` vs the working tree, comments and blank lines stripped and compared with `Compare-Object`: the only difference is the removed `*.discordsays.com` condition. `git diff -U0` shows exactly two hunks (`@@ -31 +31 @@` comment, `@@ -57,4 +56,0 @@` allowance). The change narrows the allow-list; it adds no origin and refactors nothing.
- **Retired-token scan** of the 5 files for `Discord`, `discordsays`, `ClientSecret`, `VITE_DEV_AUTH`, `DevelopmentAuthentication` — two matches remain, both explicitly authorized by this manifest: `Program.cs:163` (authentication XML documentation; the manifest's Out of Scope list names it) and `vite-env.d.ts:12` (`VITE_DEV_AUTH`, required to be preserved).
- **Preserved-token scan** — `ConnectionStrings__*`, `ApplicationSession__*`, `Cors__AllowedOrigins__*`, `ASPNETCORE_*`, `VITE_API_URL`, `VITE_SIGNALR_URL`, `allowedOrigins`, `trycloudflare`, `127.0.0.1`, `AllowedOrigins` all present in their files.
- **Scope** — `git diff --name-only` = exactly the 5 declared paths; `git diff --cached --name-only` was empty before Phase A staging; `git diff --summary` empty (no add/delete/mode changes); `git diff -w --stat` equals the plain `--stat` and `git diff --numstat` equals `git diff --ignore-cr-at-eol --numstat` (no whitespace-only or line-ending-only churn).
- **Consumer check (stop conditions)** — no source, test, script, or tracked configuration reads `VITE_DISCORD_CLIENT_ID`, `Discord:ClientId`, `Discord:ClientSecret`, `DevelopmentAuthentication:Enabled`, or `*.discordsays.com`. `Controllers/AuthController.cs` declares only `POST api/auth/register` and `POST api/auth/login`, so no `POST /api/auth/discord` route exists. No test asserts the retired CORS origin.
- **Quality review** (`quality/review.md` §1, independent read-only review) — **PASS WITH FINDINGS**, no blocking finding, no stop condition fired; both checklist items 1–5 and 7–8 PASS, item 6 PASS with the pre-existing accuracy finding recorded below.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic introduced
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed P-1…P-6 commit policy compliance
- [x] Confirmed standalone-web authentication, session behavior, CORS, SignalR, API contracts, database, and gameplay unchanged

### Reported, Not Fixed Here — Out-of-Scope Residue and Findings (`AGENTS.md` §16)
- `VITE_API_URL` and `VITE_SIGNALR_URL` are declared in `vite-env.d.ts` and documented in `src/frontend/client/.env.example`, but the reviewer's repository search found no reader in `src/frontend/client/src` (only `import.meta.env.DEV` is read; the API base URL defaults to a relative path). The retained step "Paste it into `VITE_API_URL` and `VITE_SIGNALR_URL` below" is therefore inherited behaviour this manifest explicitly requires preserving, not a statement this task introduced. Making the tunnel variables effective — or removing them — is a runtime/client-contract matter outside this task's 5-file configuration scope; follow-up task required.
- `src/backend/GameServer.Api/appsettings.Development.json:20-22` still holds `"Discord": { "ClientId": "1113385100894277644" }`. Confirmed **untracked and gitignored** (`.gitignore:6`, `git ls-files --error-unmatch` fails), i.e. a local developer file rather than repository content, and outside the declared set. Inert for the same reason as the section removed here (no consumer survives TASK-237). Report only.
- `src/frontend/client/.env.local:6-7` (and `:12`) still carries the local Discord block and `VITE_DISCORD_CLIENT_ID`. Confirmed gitignored (`.gitignore:7`) and untracked; a developer's local file, explicitly outside the declared set. Report only.
- `src/frontend/client/src/vite-env.d.ts:6-12` keeps the TASK-181 doc-comment above the preserved `VITE_DEV_AUTH` declaration while the matching `.env.example` section was deleted. The manifest authorizes preserving the declaration and says nothing about the comment; deleting it is not authorized, so it was left byte-identical. Report only.
- `docs/03-decisions/README.md:41` still says `src/backend/.env.example` "keeps a Discord section" as part of the ADR-019 entry. That text sits inside the ADR index's explicitly historical "Prior 1.12" version narrative, and ADRs plus the ADR index are declared out of scope here (and immutable); no action taken.

