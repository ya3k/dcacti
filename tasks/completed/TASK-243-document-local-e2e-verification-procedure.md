# TASK-243 — Document the Local E2E Verification Procedure and Prerequisites

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
-->

<!--
  Lifecycle: created BACKLOG 2026-10-10 upon authorization.
-->

<!--
  Lifecycle: BACKLOG → READY completed 2026-10-10 after validating tasks/TASK_LIFECYCLE.md §3 READY criteria:
    [x] Task type confirmed (DOCUMENTATION — tasks/TASK_TYPES.md §2, documentation/documentation-change.md)
    [x] Relevant documentation exists in docs/ (AGENTS.md §19, .ai/README.md, .ai/workflow/README.md)
    [x] MVP scope confirmed (MVP_SCOPE.md §1 — documentation of supported verification procedure)
    [x] Not blocked: Dependencies — None
    [x] Primary agent assigned (orchestrator), supporting agents assigned (testing, client, review)
    [x] Workflow assigned (documentation/documentation-change.md)
    [x] Acceptance criteria are binary and testable
    [x] Uniqueness: TASK-242 is highest completed ID; TASK-243 is next sequential ID
    [x] Ownership: tasks/active/ and tasks/blocked/ hold no open record
-->

<!--
  Lifecycle: READY → IN PROGRESS completed 2026-10-10: agent picked up task, moved manifest to tasks/active/.
-->

<!--
  Lifecycle: IN PROGRESS → IN REVIEW completed 2026-10-10: implementation of .ai/workflow/quality/local-e2e-verification.md
  complete, adversarial verification passed, all criteria verified against source.
-->

<!--
  Lifecycle: IN REVIEW → DONE completed 2026-10-10: Phase A implementation slice
  committed as 679df03415c42d26f72e63eeb0f0fc06eb85a3a1, and this completion record
  is filed under its own Phase B commit.
-->

---

## Metadata

```text
Task ID:           TASK-243
Type:              DOCUMENTATION
Status:            DONE
Risk:              LOW
Priority:          MEDIUM
Primary Agent:     orchestrator
Supporting Agents: testing, client, review
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery, quality/documentation-consistency, quality/scope-validation
Dependencies:      None
Declared Files:    .ai/workflow/quality/local-e2e-verification.md
```

---

## Objective

Make the repository's supported local end-to-end (E2E) verification procedure
discoverable, reproducible, and consistent with the current scripts, by adding
one focused procedure document under `.ai/workflow/quality/`. The document must
record only facts verifiable from the repository's own current sources
(`src/frontend/client/package.json`, the three smoke harness scripts,
`docker-compose.yml`, and the backend project/configuration), and must
distinguish plainly between a suite passing, its prerequisites being available,
and a pristine-machine reproducibility exercise being completed.

---

## Authoritative References

- `AGENTS.md` §19 — `.ai/workflow/` defines processes (e.g. how to run a specific check) and must not contradict §14/§15
- `.ai/README.md` §5, §6, §7 — workflow layer position, source-of-truth rule, document discovery
- `.ai/workflow/README.md` §1, §5, §6 — when a workflow is used; directory map; inherited stop conditions
- `.ai/workflow/documentation/documentation-change.md` §2, §3, §4 — no duplication; canonical owner; composition with review/completion
- `tasks/README.md` §6, §7, §11, §12 — task creation, pickup, manifest principle, skill budget
- `tasks/TASK_LIFECYCLE.md` §3, §4, §6 — status definitions, file movement, two-stage commit protocol
- `docs/03-decisions/ADR/ADR-015-application-session-authentication-contract.md` D10, D11 — the session signing secret's configuration channels (prerequisite, not a value)
- `docs/02-technical/DATABASE.md` §1/§5 — the `dotnet ef database update` apply workflow (referenced, not restated)

---

## Scope

### In Scope
- One focused local E2E procedure document at `.ai/workflow/quality/local-e2e-verification.md`.
- Verified working directory, package commands, required services/ports, startup commands and order, readiness checks, browser discovery and env overrides, run counts, pass criteria, debug variables, output locations, cleanup, and common preflight failures.
- Per-suite differences among `verify:e2e:smoke`, `verify:e2e:collection`, and `verify:e2e:history`.
- A brief disposition note for the orphaned `scripts/boss-selection-smoke.mjs`.
- Task metadata and completion records required by `tasks/TASK_LIFECYCLE.md`.

### Out of Scope
- Any modification to `tasks/artifacts/MVP-ACCEPTANCE-RELEASE-CLOSURE-AUDIT.md` (protected staged artifact).
- Production source, harness/script, test, database, or API-contract changes.
- Repairing, executing, deleting, or rewiring `boss-selection-smoke.mjs`.
- CI/CD or production infrastructure, hosting choice, or a production operational runbook.
- A pristine-machine reproducibility exercise.
- Modifying `AGENTS.md` or any `docs/` source-of-truth document.
- Any commit or push not authorized by the repository workflow and user.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State

The three E2E suites exist and are wired as package scripts in
`src/frontend/client/package.json`, and each harness script carries an inline
usage comment. However, no repository document states the procedure as a whole:
the required services and ports, the startup order, the readiness checks, the
browser discovery order and its environment overrides, the default run counts,
the per-suite pass criteria, the screenshot output directories, or the
difference between a passing suite and a completed pristine-machine
reproducibility exercise. `.ai/workflow/quality/` already exists and holds
`testing.md` and `review.md`; no E2E procedure document exists there.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-243:
```text
.ai/workflow/quality/local-e2e-verification.md
```

*(Note: The task manifest file itself is excluded from the implementation declared file set. The list above must strictly match the `Declared Files:` field in `## Metadata`.)*

---

## Acceptance Criteria

- [x] `.ai/workflow/quality/local-e2e-verification.md` exists and states the supported working directory (`src/frontend/client`).
- [x] The three commands `npm run verify:e2e:smoke`, `npm run verify:e2e:collection`, and `npm run verify:e2e:history` are documented exactly as `src/frontend/client/package.json` defines them.
- [x] Required services and ports are documented and match `docker-compose.yml`, `launchSettings.json`, `appsettings.Development.json`, and `vite.config.ts` (PostgreSQL 5433, Redis 6379, backend 5000, frontend 5173).
- [x] Backend and frontend startup commands are documented with their correct working directories, and the readiness checks match `/health` and the Vite dev server.
- [x] Browser discovery order and every supported environment override are documented against the scripts (`URL_UNDER_TEST`, `BACKEND_URL`, `DEBUG_PORT`, `EDGE_BIN`, `CHROME_BIN`, `HISTORY_RUNS`, `HISTORY_DEBUG`, `HISTORY_MAX_SWAPS`, `SMOKE_MAX_SWAPS`).
- [x] Default run counts and per-suite pass criteria are documented and match the scripts.
- [x] Screenshot output directories are documented and match the scripts' `OUT_DIR` values.
- [x] Cleanup instructions and common preflight failures are documented.
- [x] The document distinguishes a suite passing, prerequisites being available, and a pristine-machine reproducibility exercise being completed.
- [x] The document states only verified facts and does not claim the suites were freshly executed unless they were.
- [x] The orphaned `boss-selection-smoke.mjs` disposition is recorded without repairing, executing, or rewiring the script.
- [x] No staged, uncommitted, or protected file is modified beyond the declared file set.
- [x] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [x] Quality review checklist passes (`quality/review.md` §1)
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[ ] tests/ (unit / integration / gameplay scenarios)
[x] docs/ (documentation updates — .ai/workflow/quality/local-e2e-verification.md)
```

---

## Implementation Notes

- The document is a **process** document under `.ai/`, so per `.ai/README.md` §6 it must not become a source of truth for any game rule, technical contract, or architectural decision. It references owning documents by path instead of restating them.
- Verified starting facts (re-checked at task pickup; do not assume unchanged):
  - `HEAD` `0a20ab2e86e7c287a19bd80b57d04ee9138a4a4b`, branch `master`, `origin/master` `b89a047331591c868ef504b7906a8dcc5562a6aa`, 8 ahead / 0 behind.
  - Exactly one staged addition: `tasks/artifacts/MVP-ACCEPTANCE-RELEASE-CLOSURE-AUDIT.md`, blob `38e4b26ce077d1538036f2d1b411840062de6558` — protected, never staged, edited, or committed by this task.
  - No CI configuration exists in this repository (no `.github/workflows` outside `node_modules`), so no discovered CI path runs any smoke script.
  - `src/frontend/client/scripts/boss-selection-smoke.mjs` is not referenced by any package script.
- `scripts/standalone-web-smoke.mjs` at the repository root is a 5-line convenience wrapper that imports the client script; it is not a separate harness.
- The harnesses register a **unique new account per run**; this is the repository's isolation mechanism and no database rows are deleted and no test-only endpoint exists. Do not alter database contents.
- Do not copy claims from `tasks/artifacts/MVP-ACCEPTANCE-RELEASE-CLOSURE-AUDIT.md`; several of its E2E outcomes predate TASK-241's harness fix.

---

## Testing Requirements

### Required Verification
```text
[x] Documentation validation — every documented command, port, variable, run count, and output path checked against its owning source file
[x] Integration tests      — N/A (documentation-only change; no boundary crossed)
[x] Gameplay scenarios     — N/A (no gameplay behavior touched)
```

### Key Edge Cases
- See `.ai/workflow/documentation/documentation-change.md` §2 — no duplicated source of truth; reference by path and section.
- See `.ai/workflow/quality/review.md` §1 — "Documentation" and "Correctness" checklist items apply even though the change is docs-only.
- Confirm the three suites' prerequisites and behaviour genuinely differ before describing them as uniform.

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If task requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose
- If any required workflow file is not in the declared file set: STOP and reconcile the manifest first, per the task's own instruction not to edit undeclared files.
- If a documented fact cannot be verified against a current repository source: STOP and report rather than documenting an assumption.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Commit
- Implementation slice (Phase A): `679df03415c42d26f72e63eeb0f0fc06eb85a3a1`
- Subject: `docs: document local e2e verification procedure and prerequisites`
- Owns: `TASK-243`
- Files: `.ai/workflow/quality/local-e2e-verification.md`
- Shared with: `none`
- Unowned / pre-existing: `none`
- Phase B: this completion record alone, filed for the first time under its own bookkeeping commit (`tasks/completed/TASK-243-document-local-e2e-verification-procedure.md`).

### Changed Files
- `.ai/workflow/quality/local-e2e-verification.md` — new local E2E verification procedure document.

### Validation Results
- `git diff --check` — PASS (exit 0)
- Documentation cross-check against `package.json`, the three harness scripts, `docker-compose.yml`, `launchSettings.json`, `appsettings.Development.json`, and `vite.config.ts` — PASS (all verified against source)
- Environmental note: Local backend service (port 5000) and frontend Vite server (port 5173) are currently offline. Per task instructions and repository policy, no E2E suite was executed during this documentation task; no passing suite was fabricated.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed P-1…P-6 commit policy compliance
