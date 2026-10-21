# TASK-319 — Reconcile Test-Count Evidence and the Runbook Commit Identifier

---

## Metadata

```text
Task ID:           TASK-319
Type:              BUG
Status:            DONE
Risk:              LOW
Priority:          HIGH
Primary Agent:     testing
Supporting Agents: review
Workflow:          development/bug-fix.md
Skills:            testing/test-scenario-generation, documentation/spec-reconciliation, quality/scope-validation
Dependencies:      TASK-317, TASK-318
Declared Files:    tasks/completed/TASK-314-preserve-balance-baseline-and-measure-modern-boss-cadence.md, tasks/blocked/TASK-316-breaking-wire-contract-release-preparation.md, .ai/PROJECT_STATE.md
```

---

## Objective

Make the repository's recorded test-count evidence and its recorded runbook
commit identifier reproducible.

1. The Application test count reported by
   `tasks/completed/TASK-314-preserve-balance-baseline-and-measure-modern-boss-cadence.md`
   does not match what the recorded command produces on the current tree.
   Reconcile the reported counts with fresh, reproducible test output and explain
   the difference by discovery, configuration, or environment rather than
   asserting a number.
2. The runbook commit identifier recorded in
   `tasks/blocked/TASK-316-breaking-wire-contract-release-preparation.md` does not
   resolve to any commit in this repository. Replace it with the exact full SHA
   obtained from Git.

TASK-316 must remain **BLOCKED** pending explicit production release
authorization; this task corrects recorded evidence only and authorizes no
production operation.

---

## Authoritative References

- `tasks/TASK_LIFECYCLE.md` §3, §6 — Completed and BLOCKED task states, two-stage commit protocol, P-5 truth rule
- `.ai/workflow/core/completion.md` §1, §2 — Definition of Done and the report shape that must carry reproducible `## Tests` evidence
- `AGENTS.md` §21, §22 — Output discipline and definition of done
- `docs/02-technical/REDIS_STATE.md` §1, §3 — The contract TASK-317 aligns the runbook with
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.18, §8 item 10 — Breaking wire-contract removal and its coordinated deployment requirement

---

## Scope

### In Scope
- Running the recorded test commands and recording their exact, reproducible results.
- Correcting the Application and aggregate backend test counts (and any other
  count directly derived from them) in the TASK-314 completion record and in
  `.ai/PROJECT_STATE.md`, with an explicit explanation of the difference.
- Replacing the invalid runbook commit identifier in the TASK-316 blocked record
  with the full SHA Git reports for that commit.
- Re-affirming TASK-316's `Status: BLOCKED` and its pending-authorization
  condition while allowing the blocker's *description* of the drain
  confirmation to be consistent with TASK-317's corrected runbook.

### Out of Scope
- Changing any test, source file, or build configuration to make a reported
  count true.
- Editing TASK-313 or any other DONE/SUPERSEDED record except TASK-314's
  evidence correction.
- Transitioning TASK-316 to any other lifecycle state.
- Any production operation, deployment, migration, or push.

---

## Current State

- `tasks/completed/TASK-314-...md` reports `GameServer.Application.Tests` as
  `783/783 passed` and states the backend aggregate as 3363.
- `.ai/PROJECT_STATE.md` §5 repeats "All 3363 backend tests passing".
- `tasks/blocked/TASK-316-...md` records the runbook implementation commit as
  `8424a9e5251648a474fe5e0cb2aa55c28591a27e`.
- The actual commit subject `docs: author breaking wire-contract release runbook
  and deployment checklist` resolves to a different full SHA.
- The documented backend aggregate in this repository (`tasks/completed/TASK-313-...md`,
  §Validation Results) is 3356 at the pre-TASK-314 head, i.e. 785 was the
  pre-TASK-314 Application count and TASK-314 added its suites on top of it.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-319:
```text
tasks/completed/TASK-314-preserve-balance-baseline-and-measure-modern-boss-cadence.md
tasks/blocked/TASK-316-breaking-wire-contract-release-preparation.md
.ai/PROJECT_STATE.md
```

*(Note: The task manifest file itself is excluded from the implementation declared file set. The list above must strictly match the `Declared Files:` field in `## Metadata`.)*

---

## Acceptance Criteria

- [ ] 1. The exact commands used to produce each reported count are recorded, and each reported count equals the number that command actually produced on the recorded tree.
- [ ] 2. Every per-project count reported for TASK-314 is consistent with the aggregate the same run reports, so no arithmetic inconsistency remains.
- [ ] 3. The difference between the previously reported and the reproduced count is explained in the record (discovery, configuration, or environment), not merely overwritten.
- [ ] 4. The TASK-316 blocked record carries the exact full SHA Git reports for the runbook authoring commit, verified with `git rev-parse`/`git show`, and no manually approximated or truncated identifier.
- [ ] 5. TASK-316's lifecycle state remains BLOCKED and it is not marked complete, authorized, or deployment-ready.
- [ ] 6. TASK-313 and every other DONE/SUPERSEDED record is unchanged.
- [ ] 7. All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [ ] 8. Quality review checklist passes (`quality/review.md` §1)
- [ ] 9. No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[ ] tests/ (unit / integration / gameplay scenarios)
[x] docs/ (task records and the .ai navigation cache only)
```

---

## Implementation Notes

- The authoritative per-project totals for comparison: Domain, Infrastructure,
  Api, Application (see `tasks/completed/TASK-313-...md` §Validation Results for
  the pre-TASK-314 baseline).
- Record counts from a single full-solution run rather than by summing
  separately-invoked per-project runs, so the aggregate and the per-project
  counts cannot drift apart.
- Correcting TASK-314's evidence is an evidence correction to its `## Completion
  Evidence` section, not a change to its implementation decisions or acceptance
  criteria (`TASK_LIFECYCLE.md` §3 "Completed tasks are immutable" is respected
  for everything except the demonstrably wrong recorded count).
- TASK-316 is in `tasks/blocked/`; its correction is an edit in place — the
  record must not move.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — N/A (evidence reconciliation; no runtime change)
[x] Integration tests  — N/A
[x] Gameplay scenarios — N/A
```

### Key Edge Cases
- A full-solution `dotnet test` run can report a different total than the sum of
  four separately-invoked per-project runs if any project fails to build or is
  filtered in one invocation; the record must state which invocation the numbers
  come from.
- `git rev-parse` must be used for the SHA; a subject-based lookup must be
  cross-checked with `git show --stat` to prove the commit is the runbook
  authoring commit.

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If reconciling the counts would require changing tests or configuration: STOP and report the discrepancy instead (`AGENTS.md` §15)
- If correcting TASK-316 would require executing a production operation: STOP (OD-316-1 = PREPARATION ONLY)

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Commit
- Implementation slice (Phase A): `b7409fe0edbfaf541b32d0f87d352eb6a8146615`
- Subject: `docs: reconcile test-count evidence and the runbook commit identifier` (conventional subject, no task ID — P-4; verified free of a BOM)
- Owns: `TASK-319`
- Files:
  - `tasks/completed/TASK-314-preserve-balance-baseline-and-measure-modern-boss-cadence.md`
  - `tasks/blocked/TASK-316-breaking-wire-contract-release-preparation.md`
  - `.ai/PROJECT_STATE.md`
- Shared with: `none`
- Unowned / pre-existing: `none`

### Changed Files
- `tasks/completed/TASK-314-preserve-balance-baseline-and-measure-modern-boss-cadence.md` — Application count corrected `783` → `788` and the aggregate `3363` → `3368`, with the reproduction method, the derivation of the wrong figure, and the disclosed residual discrepancy against TASK-313's stated base.
- `tasks/blocked/TASK-316-breaking-wire-contract-release-preparation.md` — invalid commit identifier replaced with the exact full SHA `8424a9e2d0a432281b3d67cc528af901fdae4497`; the drain description updated to the TASK-317 correction; a TASK-319 evidence note added. Status remains `BLOCKED`.
- `.ai/PROJECT_STATE.md` — corrected counts, corrected runbook SHA, remediation deltas, and the open evidence/commit-message defects recorded.

### Validation Results
- `dotnet test src/backend/GameServer.sln` — **PASS** (Domain 1674, Infrastructure 507, Api 399, Application 785 → **3365** total; 0 failed, 0 skipped) — the current tree, which is 788 − 3 because TASK-318 replaced a five-case Theory with two Facts.
- `dotnet test …GameServer.Application.Tests.csproj` — **PASS** (`Passed: 788`) on the TASK-314 tree; reproduced independently by `--list-tests` enumeration (788 cases, 12 in the TASK-314 cadence suite).
- `npm run test:run` (`src/frontend/client`) — **PASS** (24 files, 1267 passed)
- `git cat-file -t 8424a9e5251648a474fe5e0cb2aa55c28591a27e` — **FAILS** ("could not get object info"), proving the previously recorded identifier was invalid.
- `git rev-parse 8424a9e` → `8424a9e2d0a432281b3d67cc528af901fdae4497`; `git show --stat` → `docs: author breaking wire-contract release runbook and deployment checklist`, `docs/02-technical/RUNBOOK_BREAKING_WIRE_RELEASE.md | 143 ++++++++++`, confirming it is the runbook authoring commit.

### Reconciliation Summary
```text
Application (TASK-314 tree, recorded command)   788   (was reported 783)
Application (current tree, post-TASK-318)       785   (12 - 5 + 2 cadence cases)
Backend aggregate (TASK-314 tree)              3368   (was reported 3363)
Backend aggregate (current tree)               3365
Difference cause                                transcription slip in TASK-314's
                                                record, not discovery/configuration/
                                                environment (both figures reproduced)
Runbook implementation commit (corrected)       8424a9e2d0a432281b3d67cc528af901fdae4497
Runbook implementation commit (invalid, removed)8424a9e5251648a474fe5e0cb2aa55c28591a27e
TASK-316 lifecycle                               BLOCKED (unchanged)
```

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed P-1…P-6 commit policy compliance (conventional subject, no task ID in subject, exact declared file set, no amend/reset/push)
- [x] TASK-316 **not** marked complete, authorized, or deployment-ready
- [x] No production operation performed
