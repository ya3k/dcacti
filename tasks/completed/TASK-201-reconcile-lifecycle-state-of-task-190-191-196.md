# TASK-201 — Reconcile Lifecycle State of Stale Backlog Tasks (TASK-190, TASK-191, and TASK-196)

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and tasks/ by path and section.

  THIS TASK IS A LIFECYCLE RECONCILIATION TASK, NOT AN IMPLEMENTATION TASK.
  Its entire deliverable is the correct terminal lifecycle state of three
  already-satisfied tasks: the right Status field and the right folder. It
  authors no rule, changes no contract, and writes no production code.

  PROVENANCE: The repository audit conducted under TASK-201 established that
  TASK-190, TASK-191, and TASK-196 are each fully satisfied by work that has
  already landed in the repository, yet all three still sit in tasks/backlog/
  with stale Status fields (TASK-190 as BACKLOG, TASK-191 as READY, TASK-196
  as READY). tasks/README.md §3 defines the next task ID as "the highest
  existing ID + 1 across all folders"; with 190/191/196 unfiled, backlog
  accounting is distorted. This task closes that gap following the exact
  precedent of TASK-162 and TASK-165.

  THE GOVERNING DISTINCTION (tasks/TASK_LIFECYCLE.md §1/§2):
    DONE        — "All core/completion.md §1 criteria are satisfied by DIRECT
                  EXECUTION of the task."
    SUPERSEDED  — "All intended deliverables ... 100% satisfied or rendered
                  obsolete by downstream/decomposition tasks WITHOUT the task
                  itself being directly executed."

  Evaluation against repository evidence:
    - TASK-190: DIRECTLY EXECUTED in commit e767a9e (CollectionViewerScene.ts,
      51 unit tests in CollectionViewerScene.test.ts, CDP smoke test in
      collection-viewer-smoke.mjs, and completion evidence authored).
      Transition: BACKLOG → DONE, move to tasks/completed/.
    - TASK-191: SUPERSEDED by downstream balance implementation chain
      (TASK-192, TASK-193, TASK-194, TASK-195, TASK-197, TASK-198, and TASK-200).
      All 12 Product Owner questions (Q-1 through Q-12) were resolved and
      integrated under TASK-200.
      Transition: READY → SUPERSEDED, move to tasks/completed/.
    - TASK-196: SUPERSEDED by downstream balance experiment matrix and decision
      integration (TASK-197, TASK-198, and TASK-200).
      Transition: READY → SUPERSEDED, move to tasks/completed/.

  BOUNDARY: Three task files under tasks/backlog/, their move to tasks/completed/,
  and this task file only. Zero files under docs/, src/, or tests/.
-->

---

## Metadata

```text
Task ID:           TASK-201
Type:              DOCUMENTATION (TASK_TYPES.md §2; lifecycle reconciliation)
Status:            DONE
Risk:              LOW (record correction and file moves only; zero production code)
Priority:          HIGH (unblocks clean backlog accounting and prevents duplicate work)
Primary Agent:     review (TASK_TYPES.md §2 / AGENT_SELECTION.md §1)
Supporting Agents: orchestrator (lifecycle workflow governance)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
Dependencies:      TASK-190 (implemented in commit e767a9e),
                   TASK-200 (completed balance pass lifecycle for TASK-191 & TASK-196)
```

---

## Objective

Reconcile the stale lifecycle states of `TASK-190`, `TASK-191`, and `TASK-196` by updating their lifecycle metadata to their proper terminal states (`DONE` for `TASK-190`; `SUPERSEDED` for `TASK-191` and `TASK-196`) and moving them from `tasks/backlog/` to `tasks/completed/`.

---

## Authoritative References

- `tasks/README.md` §3 — Task ID convention and backlog sequencing
- `tasks/TASK_LIFECYCLE.md` §1, §2 — Terminal states (`DONE` vs `SUPERSEDED`) and valid transitions
- `tasks/completed/TASK-162-reconcile-lifecycle-state-of-task-155-159-160-161.md` — Established reconciliation precedent
- `tasks/completed/TASK-165-reconcile-lifecycle-state-of-task-150-157-162-163-164.md` — Established reconciliation precedent
- `tasks/completed/TASK-200-mvp-balance-decision-implementation.md` — Closure evidence for balance chain (Q-1 through Q-12)

---

## Scope

### In Scope

1. Update `tasks/backlog/TASK-190-collection-viewer-ui-scene.md`:
   - Transition status to `DONE`.
   - Move file to `tasks/completed/TASK-190-collection-viewer-ui-scene.md`.
2. Update `tasks/backlog/TASK-191-balance-pass.md`:
   - Transition status to `SUPERSEDED` (downstream satisfaction by TASK-192..200).
   - Move file to `tasks/completed/TASK-191-balance-pass.md`.
3. Update `tasks/backlog/TASK-196-balance-decision-evidence-analysis.md`:
   - Transition status to `SUPERSEDED` (downstream satisfaction by TASK-197..200).
   - Move file to `tasks/completed/TASK-196-balance-decision-evidence-analysis.md`.
4. Transition `TASK-201` itself to `DONE` upon verification.

### Out of Scope

- Zero changes to production code in `src/`
- Zero changes to test code in `tests/`
- Zero changes to authoritative documentation in `docs/`
- No new features, gameplay mechanics, or UI screens

---

## Current State

- `tasks/backlog/TASK-190-collection-viewer-ui-scene.md` has status `BACKLOG`, but its implementation (`CollectionViewerScene.ts`, 51 unit tests, smoke script) is 100% complete in the repository.
- `tasks/backlog/TASK-191-balance-pass.md` has status `READY`, but all 12 questions were resolved and implemented under TASK-200.
- `tasks/backlog/TASK-196-balance-decision-evidence-analysis.md` has status `READY`, but its analysis was consumed and satisfied by TASK-197, TASK-198, and TASK-200.

---

## Acceptance Criteria

- [x] `tasks/backlog/TASK-190-collection-viewer-ui-scene.md` moved to `tasks/completed/` with `Status: DONE`.
- [x] `tasks/backlog/TASK-191-balance-pass.md` moved to `tasks/completed/` with `Status: SUPERSEDED`.
- [x] `tasks/backlog/TASK-196-balance-decision-evidence-analysis.md` moved to `tasks/completed/` with `Status: SUPERSEDED`.
- [x] No files remain in `tasks/backlog/` except newly sequenced work.
- [x] Zero production files or tests modified.
- [x] Test baselines (2,916 backend, 615 frontend) remain 100% green.

---

## Affected Files & Areas

```text
[x] tasks/backlog/TASK-190-collection-viewer-ui-scene.md -> tasks/completed/
[x] tasks/backlog/TASK-191-balance-pass.md -> tasks/completed/
[x] tasks/backlog/TASK-196-balance-decision-evidence-analysis.md -> tasks/completed/
[x] tasks/backlog/TASK-201-reconcile-lifecycle-state-of-task-190-191-196.md -> tasks/completed/
```

---

## Completion Evidence

### Changed Files

- `tasks/completed/TASK-190-collection-viewer-ui-scene.md` — moved from `tasks/backlog/`, Status updated to `DONE`.
- `tasks/completed/TASK-191-balance-pass.md` — moved from `tasks/backlog/`, Status updated to `SUPERSEDED`.
- `tasks/completed/TASK-196-balance-decision-evidence-analysis.md` — moved from `tasks/backlog/`, Status updated to `SUPERSEDED`.
- `tasks/completed/TASK-201-reconcile-lifecycle-state-of-task-190-191-196.md` — this task file, moved from `tasks/backlog/`, Status updated to `DONE`.

### Validation Results

```text
Definition check:            PASS — TASK-190 (DONE), TASK-191 (SUPERSEDED), TASK-196 (SUPERSEDED), TASK-201 (DONE) match definitions in TASK_LIFECYCLE.md §1.
Transition-legality check:   PASS — permitted transitions executed (BACKLOG→DONE, READY→SUPERSEDED, READY→SUPERSEDED, BACKLOG→DONE) per TASK_LIFECYCLE.md §2.
Placement check:             PASS — all four tasks reside in tasks/completed/; no stale TASK files remain in tasks/backlog/.
Backend test suite:          PASS — 2,916 passed, 0 failed, 0 skipped (GameServer.sln).
Frontend test suite:         PASS — 615 passed, 0 failed, 0 skipped.
Frontend build:              PASS — exit code 0 (tsc clean + vite build).
Isolation verification:      PASS — src/ (0 files changed), tests/ (0 files changed), docs/ (0 files changed).
No-new-task check:           PASS — 0 new tasks created.
```

### Reconciled Lifecycle State

```text
TASK-190: BACKLOG → DONE        (tasks/completed/TASK-190-collection-viewer-ui-scene.md)
TASK-191: READY   → SUPERSEDED  (tasks/completed/TASK-191-balance-pass.md)
TASK-196: READY   → SUPERSEDED  (tasks/completed/TASK-196-balance-decision-evidence-analysis.md)
TASK-201: BACKLOG → DONE        (tasks/completed/TASK-201-reconcile-lifecycle-state-of-task-190-191-196.md)

Highest Task ID: TASK-201
Next Task ID:    TASK-202
```

### Server Authority & Scope Verification

- [x] Confirmed zero files under `src/` modified
- [x] Confirmed zero files under `tests/` modified
- [x] Confirmed zero files under `docs/` modified
- [x] Confirmed zero gameplay, protocol, API, Redis, or architecture change
- [x] Confirmed no task other than the four named was modified
- [x] Confirmed no new task was created by the executor
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
