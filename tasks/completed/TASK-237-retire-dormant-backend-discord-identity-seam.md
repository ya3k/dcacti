# TASK-237 — Retire the Dormant Backend Discord Identity Seam (ADR-023 `REMOVE`)

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section; does not copy rules or schemas.
  Created from ADR-023 (Status: Accepted, 2026-10-09) and the completed
  TASK-236 decision record.
-->

<!--
  Lifecycle: created BACKLOG 2026-10-09 by the orchestrator role at the
  requester's direction. Intake only — no runtime source, test, configuration,
  or ADR file is modified by this task record's creation.
-->

<!--
  Lifecycle: BACKLOG → READY completed 2026-10-09 by the orchestrator role at the
  requester's direction, after validating the tasks/TASK_LIFECYCLE.md §3 READY
  criteria:
    [x] Task type confirmed (REFACTOR — tasks/TASK_TYPES.md §2 / development/refactor.md)
    [x] Relevant documentation exists in docs/ (ADR-023 §§1–2, ADR-020 D1/D4,
        ADR-015, ARCHITECTURE.md §2.3/§3, docs/03-decisions/README.md §7, MVP_SCOPE.md
        §1/§4)
    [x] MVP scope confirmed (MVP_SCOPE.md §1 — the standalone web account is the identity
        path; Discord is unlisted, hence FUTURE per §4; this task adds no scope — it
        retires already-decided residue)
    [x] Not blocked: Dependencies: TASK-236 (DONE, immutable; read-only decision record)
    [x] Primary agent assigned (backend) and workflow assigned (development/refactor.md)
    [x] Acceptance criteria are binary and testable
    [x] Uniqueness: TASK-237 is the highest assigned ID and no record other than this
        one names it
    [x] Ownership: tasks/active/ and tasks/blocked/ hold no open record, and no other
        backlog record names any declared file
-->

<!--
  Lifecycle: READY → IN PROGRESS completed 2026-10-09 by the backend agent under
  development/refactor.md: preflight completed (AGENTS.md, .ai/README.md,
  tasks/README.md, TASK_LIFECYCLE.md, TASK_TEMPLATE.md, development/refactor.md,
  ADR-023, ADR-020, ADR-015); core/task-intake.md classification produced (REFACTOR /
  BACKEND + TESTING; output = code + test compile-compatibility edits; risk MEDIUM);
  core/context-discovery.md ran with no unresolved conflict — ADR-023 §2 authorizes the
  REMOVE, no authoritative document asserts the seam is registered, and the
  repository-wide retired-type search found no production consumer outside the declared
  set; the baseline solution build reproduced 0 errors / 52 warnings.

  Lifecycle: IN PROGRESS → IN REVIEW completed 2026-10-09 by the backend agent upon
  completing the declared deletions and edits, `dotnet build src/backend/GameServer.sln`
  (0 errors / 52 warnings — identical warning set to the pre-change baseline), and the
  full `dotnet test src/backend/GameServer.sln` suite (2,970 passed / 0 failed /
  0 skipped — identical to the pre-change baseline), with no file outside the declared
  set changed.

  Lifecycle: IN REVIEW → DONE completed 2026-10-09 by the review agent (independent
  read-only review of the working tree against the manifest, ADR-023 §2, ADR-015 D4,
  quality/review.md §1, and quality/scope-validation): PASS with no blocking finding —
  the change set is exactly the 8 declared paths, zero retired-type references remain
  under src/ and tests/, both ADR-015 D4 assertions are byte-preserved and verified
  passing, and no manifest stop condition is triggered. The review's residual findings
  are recorded under ## Completion Evidence and were not fixed here (they are outside
  the declared file set). The declared implementation slice was committed as the Phase A
  commit recorded in ## Completion Evidence, and this completion record is filed under
  its own Phase B commit.
-->

---

## Metadata

```text
Task ID:           TASK-237
Type:              REFACTOR
Status:            DONE
Risk:              MEDIUM
Priority:          LOW
Primary Agent:     backend
Supporting Agents: testing (affected test-project compilation), review (scope and security-assertion preservation)
Workflow:          development/refactor.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis, quality/architecture-conformance, quality/scope-validation
Dependencies:      TASK-236 (DONE — the `REMOVE` decision this task implements; read-only, immutable, never edited)
Declared Files:    src/backend/GameServer.Application/Identity/DiscordIdentityResolution.cs, src/backend/GameServer.Infrastructure/Discord/UnconfiguredDiscordIdentityResolver.cs, src/backend/GameServer.Infrastructure/Discord/DevelopmentDiscordIdentityResolver.cs, src/backend/GameServer.Infrastructure/Discord/DevelopmentAuthenticationOptions.cs, src/backend/GameServer.Infrastructure/Discord/DiscordCredentialOptions.cs, src/backend/GameServer.Infrastructure/DependencyInjection.cs, tests/backend/GameServer.Api.Tests/ApplicationSessionRESTTests.cs, tests/backend/GameServer.Api.Tests/TestDiscordCredentials.cs
```

---

## Objective

Execute `ADR-023`'s accepted `REMOVE` decision by physically deleting the dormant backend Discord identity dependency-injection seam — the `IDiscordIdentityResolver` contract (`DiscordIdentity`, `DiscordIdentityResolution`), its two implementations, its two configuration option classes, and its DI registrations — and by removing or refactoring the two `GameServer.Api.Tests` compatibility paths that reference those types, with zero change to any observable behavior, wire contract, session contract, or game rule.

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-023-backend-discord-identity-seam-resolution.md` §2 — The authorized `REMOVE` decision, its named retirement set, and its explicit statement that physical deletion is deferred to downstream implementation work
- `docs/03-decisions/ADR/ADR-020-standalone-web-account-authentication.md` §2 D1 — Discord retirement: endpoint, configuration keys, and validation startup gates removed; standalone web account model is the sole identity path
- `docs/03-decisions/ADR/ADR-015-application-session-authentication-contract.md` — The application session contract that must remain intact (stateless signed JWT, `player_id` claim)
- `docs/02-technical/ARCHITECTURE.md` §2.3, §3 — The synchronized authentication boundary and component catalog (already reconciled to ADR-023 by TASK-236)
- `docs/00-overview/MVP_SCOPE.md` §1, §4 — Standalone web account model is the identity path; Discord is unlisted
- `tasks/completed/TASK-236-resolve-backend-discord-identity-seam-architecture-decision.md` §Stop Conditions — Verified `REMOVE` blast radius and the recorded evidence base (read-only; immutable)
- `tasks/completed/TASK-234-standalone-web-sign-out-and-http-401-session-invalidation.md` — Current standalone-web authentication/session behavior that must not regress
- `tasks/completed/TASK-057-remove-unsupported-player-combat-readiness-battle-result-gate.md` — Precedent for a bounded backend removal slice with test counter-edits
- `AGENTS.md` §§2, 14, 15, 16, 17, 18, 20; `.ai/README.md` §18 — Hierarchy, smallest correct change, testing, task discipline, doc-change rule, architecture-change rule, stop conditions

---

## Scope

### In Scope

1. Delete the five retired runtime source files named by `ADR-023` §2 item 1 and enumerated in `## Declared File Set (P-2 / P-5)`.
2. Remove the two DI registrations and their associated comment block in `DependencyInjection.cs`: the `AddSingleton<IDiscordIdentityResolver, UnconfiguredDiscordIdentityResolver>()` registration and the entire `AddDevelopmentDiscordIdentityResolver` extension method, plus the now-unused `using GameServer.Infrastructure.Discord;` import.
3. Remove the two `GameServer.Api.Tests` compatibility paths that reference the deleted types, so the `GameServer.Api.Tests` assembly compiles: the `StubIdentityResolver` substitution and its `RemoveAll`/`AddSingleton` pair in `ApplicationSessionRESTTests.cs`, and the `TestDiscordCredentials.cs` fixture file.
4. Preserve, unchanged, every existing test assertion in `GameServer.Api.Tests` — including the negative security assertions that mention Discord by name but assert the session/authorization boundary rather than the seam.
5. Verify no production consumer or active DI registration references the retired seam after the change.

### Out of Scope

- `IPlayerRepository.GetOrCreateByDiscordUserIdAsync` and its callers — it references no seam type and is explicitly outside the verified blast radius (`TASK-236` §Stop Conditions note D).
- The inert `"Discord"` section in `src/backend/GameServer.Api/appsettings.json` — not proven exclusively associated with the retired seam; report only.
- The `Program.cs` CORS `*.discordsays.com` allowance and Discord comment residue — out of the seam's blast radius; report only.
- Historical EF migrations and `docs/03-decisions/ADR/` historical records — never edited to erase history (`AGENTS.md` §16, `TASK_LIFECYCLE.md` §3).
- `docs/02-technical/ARCHITECTURE.md` §2.2 frontend Discord-Activity text (`:200`, `:209`, `:216`, `:224`, `:228`) — that residue is owned by `ADR-020`'s frontend retirement, not by `ADR-023`'s backend seam decision; report only.
- `tests/backend/GameServer.Api.Tests/BattleStartEndpointTests.cs:51`'s unused `private const string DiscordUserId` — a bare string literal referencing no retired type; it compiles unchanged; report only.
- Any new authentication design, identity provider abstraction, generic adapter, Discord SDK/OAuth/token dependency reintroduction, or schema/API/SignalR/rewards/progression/frontend change.
- Any player ownership or starter ownership behavior change.
- Any `docs/01-game-design/` change — this task changes no game rule.
- Unrelated cleanup discovered while here (report only).
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State

`ADR-023` (Status: Accepted) formally retired the dormant backend Discord identity seam and explicitly deferred physical deletion to downstream implementation work. The seam is still physically present and verified:

- `src/backend/GameServer.Application/Identity/DiscordIdentityResolution.cs` declares `IDiscordIdentityResolver`, `DiscordIdentity`, and `DiscordIdentityResolution`.
- `src/backend/GameServer.Infrastructure/Discord/` holds four files: `UnconfiguredDiscordIdentityResolver.cs`, `DevelopmentDiscordIdentityResolver.cs`, `DiscordCredentialOptions.cs`, `DevelopmentAuthenticationOptions.cs`.
- `DependencyInjection.cs:114` registers `AddSingleton<IDiscordIdentityResolver, UnconfiguredDiscordIdentityResolver>()`; `:139-182` defines `AddDevelopmentDiscordIdentityResolver`, which has **zero callers repository-wide** (only its own definition at `:165` and a comment at `:112`).
- `AuthController.cs` declares only `[HttpPost("register")]` and `[HttpPost("login")]` on `[Route("api/auth")]` — **no `POST /api/auth/discord` endpoint exists**, so no route can reach `ResolveAsync`.
- `API_CONTRACTS.md` §2 has been fully rewritten under ADR-020: §2.2 is now `POST /api/auth/login`, and the seam's cited §2.6/§2.7 no longer exist.
- `DevelopmentAuthentication:Enabled` is set in no tracked configuration file.

The seam is therefore dead wiring on the live DI container, exactly as `ADR-023` §1 records.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-237 (8 files — 5 whole-file deletions, 3 edits):

```text
src/backend/GameServer.Application/Identity/DiscordIdentityResolution.cs
src/backend/GameServer.Infrastructure/Discord/UnconfiguredDiscordIdentityResolver.cs
src/backend/GameServer.Infrastructure/Discord/DevelopmentDiscordIdentityResolver.cs
src/backend/GameServer.Infrastructure/Discord/DevelopmentAuthenticationOptions.cs
src/backend/GameServer.Infrastructure/Discord/DiscordCredentialOptions.cs
src/backend/GameServer.Infrastructure/DependencyInjection.cs
tests/backend/GameServer.Api.Tests/ApplicationSessionRESTTests.cs
tests/backend/GameServer.Api.Tests/TestDiscordCredentials.cs
```

Evidence per declared path:

| Path | Action | Evidence |
|---|---|---|
| `GameServer.Application/Identity/DiscordIdentityResolution.cs` | Delete | Declares only the 3 retired types; `ADR-023` §2 item 1 names `DiscordIdentity` and `DiscordIdentityResolution`. All other references to its types under `src/` are within the other declared files. |
| `Infrastructure/Discord/UnconfiguredDiscordIdentityResolver.cs` | Delete | The retired implementation; named by `ADR-023` §2 item 1 and §1 bullet 2. |
| `Infrastructure/Discord/DevelopmentDiscordIdentityResolver.cs` | Delete | The retired development implementation; named by `ADR-023` §2 item 1 and §1 bullet 2. |
| `Infrastructure/Discord/DevelopmentAuthenticationOptions.cs` | Delete | Named by `ADR-023` §2 item 1 and §1 bullet 3. Its `IsEnabled` is called from `DependencyInjection.cs:170` only. |
| `Infrastructure/Discord/DiscordCredentialOptions.cs` | Delete | Named by `ADR-023` §2 item 1 and §1 bullet 3. `ValidateForEnvironment` has zero composition-root callers; sole external reference is the declared `TestDiscordCredentials.cs`. |
| `Infrastructure/DependencyInjection.cs` | Edit | Holds both DI registrations named by `ADR-023` §1 bullet 4. Edits: drop `using …Discord;` (`:10`), the `:102-114` comment + registration, and the `:139-182` method. |
| `tests/backend/GameServer.Api.Tests/ApplicationSessionRESTTests.cs` | Edit | Hard compile break on deletion: `using GameServer.Application.Identity;` (`:7`), the substitution pair (`:609-610`), the `StubIdentityResolver` class (`:703-718`), and the now-unused `DiscordUserId` const (`:567`). Its assertions test the JWT/authorization pipeline, not Discord. |
| `tests/backend/GameServer.Api.Tests/TestDiscordCredentials.cs` | Edit/Delete | Whole-file reference to `DiscordCredentialOptions` (`:1`, `:40-41`) — hard compile break. Verified to have zero consumers, so nothing outside this file depends on it. |

*(Note: The task manifest file itself is excluded from the implementation declared file set. The list above strictly matches the `Declared Files:` field in `## Metadata`.)*

---

## Acceptance Criteria

- [x] The five runtime files enumerated in `ADR-023` §2 item 1 are deleted from `src/backend/`.
- [x] `DependencyInjection.cs` contains no `IDiscordIdentityResolver` registration, no `AddDevelopmentDiscordIdentityResolver` method, and no `using …Infrastructure.Discord;` import.
- [x] Zero production consumers or active DI registrations reference the retired seam: a repository-wide search for `IDiscordIdentityResolver`, `DiscordIdentityResolution`, `UnconfiguredDiscordIdentityResolver`, `DevelopmentDiscordIdentityResolver`, `DevelopmentAuthenticationOptions`, `DiscordCredentialOptions`, `AddDevelopmentDiscordIdentityResolver`, and `DiscordIdentity` returns no match under `src/`.
- [x] `dotnet build src/backend/GameServer.sln` succeeds with 0 errors; every backend test project compiles.
- [x] The obsolete `GameServer.Api.Tests` compatibility paths are removed or refactored as evidenced: the `StubIdentityResolver` substitution no longer exists, and `TestDiscordCredentials.cs` is deleted (zero consumers verified).
- [x] Every surviving assertion in `GameServer.Api.Tests` is preserved verbatim in intent — in particular `ProtectedEndpoint_WithADiscordAccessToken_ShouldReturn401Unauthenticated` (`ApplicationSessionRESTTests.cs:305`) and `HubConnection_WithADiscordAccessToken_ShouldBeRejected` (`ApplicationSessionSignalRTests.cs:145`) still assert that a raw external access token never authenticates, and no test is deleted or weakened merely because it mentions Discord.
- [x] Standalone-web authentication remains intact: `POST /api/auth/register` and `POST /api/auth/login` (`API_CONTRACTS.md` §2.1, §2.2) and the `ApplicationSessionRESTTests` suite pass unchanged.
- [x] ApplicationSession behavior remains intact: the `ApplicationSessionSignalRTests` and `ApplicationSessionRESTTests` suites pass unchanged (`ADR-015`).
- [x] Existing player ownership and starter ownership behavior is unchanged: `GameServer.Infrastructure.Tests` (including `PlayerStarterOwnershipTests`, `PlayerStarterOwnershipPostgresTests`) passes with zero test edits — this task declares no file in that project.
- [x] Full backend suite passes: `dotnet test src/backend/GameServer.sln`.
- [x] No file outside the declared set is staged (P-5), and no `docs/`, `src/frontend/`, `.ai/`, or ADR file is modified.
- [x] Quality review checklist passes (`quality/review.md` §1).
- [x] Two-stage commit protocol followed (`TASK_LIFECYCLE.md` §6): Phase A commits exactly the declared file set with `owns: TASK-237` in the body and no task ID in the subject; Phase B files this record under its own commit.
- [x] Completion Evidence records the Phase A SHA, the actual commands run, the observed results, and the changed files.

---

## Affected Files & Areas

```text
[x] src/backend/ (Application / Infrastructure)
[ ] src/backend/ (Domain / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[x] tests/ (GameServer.Api.Tests — compile-compatibility edits only)
[ ] docs/ (no documentation change is required; see Implementation Notes)
```

---

## Implementation Notes

### Verified dependency findings

- `AddDevelopmentDiscordIdentityResolver` has **zero external callers**; it is referenced only by its own definition and one comment. `Program.cs` composes only `AddApplicationServices`, `AddInfrastructureServices`, and `AddApplicationSessionAuthentication`.
- No controller or hub takes a Discord dependency. `AuthController` declares only `register` and `login`. No route invokes `ResolveAsync`.
- `DiscordCredentialOptions.ValidateForEnvironment` has no composition-root caller.
- `TestDiscordCredentials` has **zero consumers** — confirmed repository-wide — so deleting the file breaks nothing that references it; it must still be deleted or emptied because it references `DiscordCredentialOptions` and would otherwise break assembly compilation.
- `BattleStartEndpointTests.cs:51` declares an unused `private const string DiscordUserId`. It is a bare string literal that references no retired type, so it compiles unchanged. **Leave it; report it.**
- `ApplicationSessionSignalRTests.cs` uses only a string literal `DiscordAccessToken`; it references no retired type and must not be declared.

### Ordering guidance

Delete the contract file's dependents first (`DevelopmentDiscordIdentityResolver` references `UnconfiguredDiscordIdentityResolver`), then the option classes, then the contract file, then clean `DependencyInjection.cs`, then the two test paths. Confirm with a solution build before running tests.

### Minimize the test edit

Prefer the smallest change that restores compilation without touching assertions: remove the `StubIdentityResolver` class and its two registration lines, then drop the `using GameServer.Application.Identity;` import **only if** no surviving member in the file needs it (verify — the file may use other `GameServer.Application.*` namespaces). Do not reorder, rename, or re-comment unrelated test code.

### Documentation impact

No documentation change is required. `ADR-023` already records the decision and states that physical deletion is deferred; `docs/03-decisions/README.md` §7 already indexes it as `Accepted`; and `ARCHITECTURE.md` §2.3/§3 were already reconciled to ADR-023 by TASK-236. **If** implementation reveals an authoritative document that still asserts the seam is registered, STOP and report per `AGENTS.md` §4 / §17 rather than editing an undeclared document.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — none added: this task changes no behavior and introduces no predicate
[x] Integration tests  — GameServer.Api.Tests must still compile and pass unchanged in intent
                       (ApplicationSessionRESTTests, ApplicationSessionSignalRTests)
[x] Regression suite   — GameServer.Infrastructure.Tests must pass with zero edits, proving
                       player/starter ownership is untouched
[ ] Gameplay scenarios — N/A: no game rule, event, state, or wire contract changes
```

### Commands (repository's actual commands — do not invent new scripts)

```text
dotnet build src/backend/GameServer.sln
dotnet test  src/backend/GameServer.sln
```

### Key Edge Cases

- **Assembly-wide compile suppression:** both declared test paths are hard compile breaks. If either is missed, the entire `GameServer.Api.Tests` assembly fails to build and every test in it is silently suppressed. The acceptance criteria require the full suite to be observed passing, not merely a successful build.
- **Security assertions must survive:** the two `*DiscordAccessToken*` tests protect the `ADR-015` D4 boundary (a raw external access token is never an application session). They reference only string literals, so they must remain byte-identical in behavior.
- **Player-creation path:** `GetOrCreateByDiscordUserIdAsync` is `[Obsolete]` but live for other callers. Do not touch it or its tests.

---

## Stop Conditions

- If a file outside `## Declared File Set (P-2 / P-5)` requires a change to make the build or tests pass: STOP per `TASK_LIFECYCLE.md` §6.1 step 2 / P-5 — report the divergence; do not silently expand the declared set.
- If removing the seam is found to require a change in observable authentication, session, ownership, API, SignalR, database, or game behavior: STOP — that is a behavior change, not a refactor (`development/refactor.md` §1, §2), and it must be re-classified rather than executed here.
- If any authoritative document is found to still assert the seam is registered or reachable: STOP and report per `AGENTS.md` §4 / §17.
- If `ADR-023` is not `Accepted`, or TASK-236 is not `DONE`: STOP — the implementation is unauthorized.
- If the retired-type search returns a production reference outside the declared set: STOP and report the reference.
- If a required security assertion appears to require deletion or weakening to compile: STOP — determine whether it still protects the `ADR-015` boundary and report.
- If task execution splits into more than one independently verifiable slice: STOP and report rather than expanding scope.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Commit
- Implementation slice (Phase A): `1cdcd398ad4dc9d4ebc2854d9aecf0b1d5b48c4a`
- Subject: `refactor: retire the dormant backend discord identity seam`
- Owns: `TASK-237`
- Files: `src/backend/GameServer.Application/Identity/DiscordIdentityResolution.cs, src/backend/GameServer.Infrastructure/Discord/UnconfiguredDiscordIdentityResolver.cs, src/backend/GameServer.Infrastructure/Discord/DevelopmentDiscordIdentityResolver.cs, src/backend/GameServer.Infrastructure/Discord/DevelopmentAuthenticationOptions.cs, src/backend/GameServer.Infrastructure/Discord/DiscordCredentialOptions.cs, src/backend/GameServer.Infrastructure/DependencyInjection.cs, tests/backend/GameServer.Api.Tests/ApplicationSessionRESTTests.cs, tests/backend/GameServer.Api.Tests/TestDiscordCredentials.cs`
- Shared with: `none`
- Unowned / pre-existing: `none`
- Staging verification (P-5): `git diff --cached --name-status` matched the record's canonical `Declared Files` exactly — 6 deletions + 2 modifications, zero extraneous files, task record excluded (`1cdcd39`).

### Changed Files
- `src/backend/GameServer.Application/Identity/DiscordIdentityResolution.cs` — deleted: the retired `IDiscordIdentityResolver` contract, `DiscordIdentity`, and `DiscordIdentityResolution` (the `GameServer.Application.Identity` namespace existed only in this file).
- `src/backend/GameServer.Infrastructure/Discord/UnconfiguredDiscordIdentityResolver.cs` — deleted: the retired registration-point implementation.
- `src/backend/GameServer.Infrastructure/Discord/DevelopmentDiscordIdentityResolver.cs` — deleted: the retired development-only implementation.
- `src/backend/GameServer.Infrastructure/Discord/DevelopmentAuthenticationOptions.cs` — deleted: the retired development opt-in configuration contract (`IsEnabled` was called only by the removed extension method).
- `src/backend/GameServer.Infrastructure/Discord/DiscordCredentialOptions.cs` — deleted: the retired credential configuration contract (`ValidateForEnvironment` had no composition-root caller).
- `src/backend/GameServer.Infrastructure/DependencyInjection.cs` — removed the `AddSingleton<IDiscordIdentityResolver, UnconfiguredDiscordIdentityResolver>()` registration and its comment block, the whole `AddDevelopmentDiscordIdentityResolver` extension method, and the three imports it leaves unused: `GameServer.Infrastructure.Discord` (manifest-named), plus `GameServer.Application.Identity` (compile-required — its only namespace declaration was the deleted file) and `Microsoft.Extensions.DependencyInjection.Extensions` (`.Replace(...)` was used only by the removed method). 62 lines removed, 0 added.
- `tests/backend/GameServer.Api.Tests/ApplicationSessionRESTTests.cs` — removed the `StubIdentityResolver` substitution (`RemoveAll`/`AddSingleton` pair, the nested stub class, and the now-unused `DiscordUserId` const it fed), and dropped the `using GameServer.Application.Identity;` import; two doc-comment phrases that described the removed substitution were updated. No assertion, `[Fact]`, or test method was touched.
- `tests/backend/GameServer.Api.Tests/TestDiscordCredentials.cs` — deleted: the whole-file `DiscordCredentialOptions` reference, verified to have zero consumers.

### Validation Results
- `dotnet build src/backend/GameServer.sln` (baseline, pre-change) — PASS (0 errors, 52 warnings)
- `dotnet build src/backend/GameServer.sln --no-incremental` (post-change, final tree) — PASS (0 errors, 52 warnings); normalised warning sets are identical to baseline, so no warning was added or removed
- `dotnet test src/backend/GameServer.sln` (baseline, pre-change) — PASS (2,970 passed / 0 failed / 0 skipped: Domain 1,558, Application 653, Infrastructure 422, Api 337)
- `dotnet test src/backend/GameServer.sln` (post-change, final tree) — PASS (2,970 passed / 0 failed / 0 skipped: Domain 1,558, Application 653, Infrastructure 422, Api 337)
- `dotnet test tests/backend/GameServer.Api.Tests/GameServer.Api.Tests.csproj --filter "FullyQualifiedName~DiscordAccessToken"` — PASS (2/2): both ADR-015 D4 assertions (`ProtectedEndpoint_WithADiscordAccessToken_ShouldReturn401Unauthenticated`, `HubConnection_WithADiscordAccessToken_ShouldBeRejected`) still pass
- `dotnet test ... --filter "FullyQualifiedName~ApplicationSession"` (Api.Tests) — PASS (53/53)
- `dotnet test ... --filter "FullyQualifiedName~PlayerStarterOwnership|FullyQualifiedName~InfrastructureRegistration"` (Infrastructure.Tests) — PASS (49/49), with zero test edits in that project
- Retired-type repository search under `src/` and `tests/` (`IDiscordIdentityResolver`, `DiscordIdentityResolution`, `DiscordIdentity`, `UnconfiguredDiscordIdentityResolver`, `DevelopmentDiscordIdentityResolver`, `DevelopmentAuthenticationOptions`, `DiscordCredentialOptions`, `AddDevelopmentDiscordIdentityResolver`, `TestDiscordCredentials`, `StubIdentityResolver`) — 0 matches each
- Quality review (`quality/review.md` §1, independent read-only review) — PASS, no blocking finding

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic introduced
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed P-1…P-6 commit policy compliance
- [x] Confirmed standalone-web authentication, session behavior, and player ownership unchanged

### Reported, Not Fixed Here — Out-of-Scope Residue (`AGENTS.md` §16)
- `docs/02-technical/ARCHITECTURE.md:3-6` and `:683` still word the seam's physical deletion as "deferred to downstream implementation work"; that wording became historical once this slice landed. The file is undeclared, so it was not edited — follow-up documentation reconciliation required (`AGENTS.md` §17).
- `src/backend/.env.example:46-60` (tracked) sets `DevelopmentAuthentication__Enabled=true` and documents the retired development path and `POST /api/auth/discord`. The manifest §Current State statement that the switch "is set in no tracked configuration file", and the same over-broad evidence phrasing in `ADR-023` §1 item 2 and `TASK-236`, are therefore inaccurate as written. The key was inert regardless — `.env.example` is not a configuration provider and the extension method that read it had zero callers — so the removal stands; the evidence phrasing should be corrected in a follow-up.
- Remaining provably-dead Discord residue, all declared out of scope: `src/backend/GameServer.Api/appsettings.json:20-23`, `src/backend/.env.example:62-80`, `src/frontend/client/.env.example`, `Program.cs` `*.discordsays.com` CORS allowance and Discord comment residue, `PlayerRepository.GetOrCreateByDiscordUserIdAsync` (`:102`), `BattleStartEndpointTests.cs:51`'s unused `DiscordUserId`, `ARCHITECTURE.md` §2.2 frontend Discord-Activity text (`:200`, `:216`, `:224`, `:228`), and `docs/03-decisions/ADR/ADR-019` (superseded) historical references.
- Pre-existing documentation drift reported by the independent review, not caused by this task: `ADR-015` D1 (`:41`) and D6 (`:94-95`) still name `POST /api/auth/discord` as the session issuance endpoint; `ADR-020` D3 replaced it. ADR-015 never mentions the retired DI seam, so the manifest stop condition is not triggered, and ADR records are immutable — a `§4` reconciliation decision is required instead.
- Manifest evidence precision note: the `## Declared File Set` prose says "5 whole-file deletions, 3 edits", while the declared path set resolves to 6 deletions + 2 edits because `TestDiscordCredentials.cs` is deleted outright; the path set itself is exact and unchanged, and the file's own evidence table already read "Edit/Delete" with AC bullet 5 requiring deletion.
