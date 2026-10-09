# tasks/TASK_LIFECYCLE.md — Task Lifecycle

**Version:** 1.2 (Formalized two-stage commit protocol — Phase A implementation slice commit and Phase B individual completion record filing — adopted under TASK-230)

> This document answers: **"What states can a task be in, how does it
> move between them, and who is responsible for each transition?"**

---

# 1. States

```text
BACKLOG       Defined but not yet validated for readiness.
READY         Validated: docs exist, scope confirmed, agent assigned,
              no known blockers. Ready for an agent to pick up.
IN PROGRESS   Agent is actively executing the workflow.
IN REVIEW     Implementation and tests are complete; quality/review.md
              is running.
BLOCKED       A stop condition has fired. Work cannot proceed until a
              human resolves the blocking condition.
DONE          All core/completion.md §1 criteria are satisfied by direct
              execution of the task. Task is moved to completed/.
SUPERSEDED    All intended deliverables of the task have been 100% satisfied
              or rendered obsolete by downstream/decomposition tasks without
              the task itself being directly executed. Task is moved to
              completed/.
```

---

# 2. Transition Diagram

```text
BACKLOG ──────────────────────────────────→ READY
   │                                          │
   │ ┌────────────────────────────────────────┘
   │ │
   │ ├────────────────────────────────────→ SUPERSEDED ◄────────┐
   │ │                                           ▲              │
   ↓ ↓                                           │              │
IN PROGRESS ◄────────────────────────────┐       │              │
   │                                     │       │              │
   ├────────────┬────────────┐           │       │              │
   ↓            ↓            ↓           │       │              │
BLOCKED    IN REVIEW      BLOCKED        │       │              │
   │            │            │           │       │              │
   │     ┌──────┴──────┐     └───────────┼───────┤              │
   │     ↓             ↓                 │       │              │
   │   DONE      IN PROGRESS ────────────┘       │              │
   │                (rework)                     │              │
   │                                             │              │
   ├─────────────────────────────────────────────┘              │
   │ (human resolves)                                           │
   ↓                                                            │
IN PROGRESS                                                     │
   │                                                            │
   └────────────────────────────────────────────────────────────┘
     (if decomposed out-of-band: BLOCKED → SUPERSEDED)
```

### Allowed transitions

```text
BACKLOG     → READY           Orchestrator or requester confirms task is
                               ready (docs exist, scope confirmed)
BACKLOG     → SUPERSEDED      Lifecycle audit confirms scope is 100% satisfied
                               or obsolete by downstream tasks before pickup
READY       → IN PROGRESS     Agent picks up the task
READY       → SUPERSEDED      Lifecycle audit confirms scope is 100% satisfied
                               or obsolete by downstream tasks before execution
IN PROGRESS → IN REVIEW       Implementation + tests complete
IN PROGRESS → BLOCKED         Stop condition fires during execution
IN REVIEW   → DONE            Review passes all quality/review.md §1 items,
                              the task's Phase A implementation slice is
                              committed, and its Phase B completion record
                              is filed (§6)
IN REVIEW   → IN PROGRESS     Review identifies a defect requiring rework
BLOCKED     → IN PROGRESS     Blocking condition resolved by human decision
BLOCKED     → SUPERSEDED      Blocker and scope resolved/satisfied out-of-band
                               by downstream decomposition tasks
```

### Invalid transitions

```text
BACKLOG     → IN PROGRESS     (must pass through READY)
BACKLOG     → DONE            (DONE requires direct execution; use SUPERSEDED if satisfied downstream)
READY       → DONE            (DONE requires direct execution; use SUPERSEDED if satisfied downstream)
READY       → BLOCKED         (if a blocker is found during READY
                               validation, return to BACKLOG)
IN REVIEW   → BLOCKED         (review findings are rework, not blocks;
                               use IN REVIEW → IN PROGRESS for rework)
DONE        → any             (completed tasks are immutable;
                               create a new task for follow-up work)
SUPERSEDED  → any             (superseded tasks are immutable;
                               create a new task for follow-up work)
```

---

# 3. State Definitions

## BACKLOG

A task exists but has not been validated for execution readiness.

**What it means:**
- Task may be incompletely specified
- Relevant docs may not yet be confirmed to exist
- Agent may not yet be assigned
- Dependencies may not be resolved

**Who sets it:** Task creator.

**What must happen before → READY:**
```text
[ ] Task type confirmed (TASK_TYPES.md)
[ ] Relevant documentation exists in docs/
[ ] MVP scope confirmed (docs/00-overview/MVP_SCOPE.md §1)
[ ] Task is not blocked by an unresolved dependency
[ ] Primary agent assigned
[ ] Workflow assigned
[ ] Acceptance criteria are testable
```

---

## READY

The task has been validated and is waiting for an agent.

**What it means:**
- All BACKLOG → READY criteria are met
- An agent can pick this up without preliminary validation failures

**Who sets it:** Orchestrator Agent or task requester after validation.

**File location:** `tasks/backlog/`

---

## IN PROGRESS

An agent is actively executing the assigned workflow.

**What it means:**
- Agent has read the task file and confirmed context
- core/task-intake.md classification is complete
- core/context-discovery.md has run (no unresolved conflicts)
- Implementation is underway

**Who sets it:** Agent (when picking up from READY).

**File location:** `tasks/active/`
**File move:** `backlog/` → `active/`

---

## IN REVIEW

Implementation and tests are complete. quality/review.md is running.

**What it means:**
- core/implementation.md is complete
- quality/testing.md is complete at the required depth
- quality/review.md checklist is in progress or complete
- No remaining implementation work (only review findings may create
  rework)

**Who sets it:** Agent (after testing is complete).

**File location:** `tasks/active/` (no file move — status field only)

---

## BLOCKED

A stop condition has fired and the task cannot proceed.

**What it means:**
- One of the AGENTS.md §20 / .ai/README.md §13 stop conditions has
  fired, OR a task-specific stop condition was hit
- Human decision or documentation update is required before work
  can resume
- The STOP CONDITION report is written in the task's Stop Conditions
  section

**Who sets it:** Agent (immediately when a stop condition fires).

**File location:** `tasks/blocked/`
**File move:** `active/` → `blocked/`

**Resolution:**
- A human reads the STOP CONDITION report
- Provides the required decision, documentation update, or
  clarification
- Agent updates the task file with the resolution
- Agent moves the file back to `active/` and sets Status: IN PROGRESS

---

## DONE

All core/completion.md §1 criteria are satisfied by direct execution.

**What it means:**
```text
[ ] Implementation completed
[ ] Relevant validation passed (per core/validation.md depth)
[ ] No unresolved documentation conflict
[ ] No unintended scope expansion
[ ] Documentation impact checked and addressed if required
[ ] Architecture impact checked and addressed if required
[ ] Phase A implementation slice committed per §6 (staged set equals
    the record's declared file set; group commit when a file is shared;
    unowned/pre-existing paths disclosed per P-3)
[ ] Phase B completion record filed in tasks/completed/ documenting
    the Phase A commit SHA
```

*(Note: This checklist must remain strictly consistent with `.ai/workflow/core/completion.md` §1.)*

**Who sets it:** Agent (after quality/review.md passes and two-stage commit protocol completes).

**File location:** `tasks/completed/`
**File move:** `active/` → `completed/`

**Completed tasks are immutable.** Do not edit a completed task's
implementation decisions or acceptance criteria. Create a new task
for follow-up work.

---

## SUPERSEDED

All intended deliverables of the task have been verified 100% satisfied
or rendered obsolete by downstream/decomposition tasks without the
task itself being directly executed.

**What it means:**
```text
[ ] Task was never directly executed
[ ] 100% of the task's original scope/deliverables are verified complete
    in documented downstream tasks
[ ] Supersession / downstream satisfaction evidence is explicitly recorded
    linking each deliverable to its satisfying downstream task
[ ] Zero actionable independent scope remains in the task
```

**Who sets it:** Orchestrator Agent or Review Agent during lifecycle reconciliation.

**File location:** `tasks/completed/`
**File move:** `backlog/` → `completed/` or `blocked/` → `completed/`

**Superseded tasks are immutable.** Once reconciled and moved to `completed/`,
they stand as historical records and are not edited.

---

# 4. File Movement Summary

```text
Transition                 From         To
─────────────────────────  ──────────   ──────────
BACKLOG → READY            backlog/     backlog/     (no move; status field only)
BACKLOG → SUPERSEDED      backlog/     completed/
READY → IN PROGRESS        backlog/     active/
READY → SUPERSEDED         backlog/     completed/
IN PROGRESS → IN REVIEW    active/      active/      (no move; status field only)
IN REVIEW → IN PROGRESS    active/      active/      (no move; status field only)
IN PROGRESS → BLOCKED      active/      blocked/
BLOCKED → IN PROGRESS      blocked/     active/
BLOCKED → SUPERSEDED       blocked/     completed/
IN REVIEW → DONE           active/      completed/
```

The two-stage commit protocol (§6) runs inside the IN REVIEW → DONE transition:

```text
IN REVIEW → DONE    the task's declared implementation files are staged and
                    committed in Phase A, followed by moving/filing the task
                    record to tasks/completed/ via Phase B (§6)
```

---

# 5. Relationship to Workflows

Each lifecycle transition maps to a specific workflow gate:

```text
BACKLOG → READY         Manual validation (READY criteria in §3)
BACKLOG → SUPERSEDED    Lifecycle reconciliation audit confirming 100% downstream coverage
READY → IN PROGRESS     core/task-intake.md + core/context-discovery.md
READY → SUPERSEDED      Lifecycle reconciliation audit confirming 100% downstream coverage
IN PROGRESS workflow     core/planning.md → core/implementation.md
IN PROGRESS → BLOCKED   Any stop condition (AGENTS.md §20)
IN PROGRESS → IN REVIEW quality/testing.md complete
IN REVIEW workflow       quality/review.md
IN REVIEW → DONE        core/completion.md §1 satisfied + Phase A implementation commit + Phase B completion record filing (§6)
IN REVIEW → IN PROGRESS quality/review.md found defect requiring rework
BLOCKED → SUPERSEDED    Lifecycle reconciliation audit confirming blocker & scope resolved downstream
```

The workflow is the authoritative process — the lifecycle state in the
task file is a reflection of workflow progress, not a substitute for it.

---

# 6. Two-Stage Commit Protocol and Commit Ownership

**Adopted:** Product Owner Decision 1 = A, recorded under TASK-224, adopting
TASK-223 §6.2's P-1…P-6 as repository policy, and formalized under TASK-230
as the Two-Stage Commit Protocol (Design A) with dedicated completion record
filing (Option 1A). This section is the single owner of the commit rule;
`core/completion.md` §1/§2 reference it and do not restate it.

---

## 6.1 The Two-Stage Commit Protocol

A task's changes enter history through a discrete two-stage commit sequence
executed inside the IN REVIEW → DONE transition (§4, §5) — after
`quality/review.md` §1 passes, before the task is marked DONE.

### Phase A: Implementation Slice Commit
1. **Stage Declared Implementation Files:** Stage strictly the file set
   declared in the task's canonical `Declared Files` manifest field
   (`tasks/TASK_TEMPLATE.md`). The task record itself is NOT staged in
   Phase A.
2. **Exact-Set Staging Verification (P-5):** Compare
   `git diff --cached --name-status` against the record's canonical
   `Declared Files`. The staged set must match the declared file set exactly
   with zero extraneous files and excluding the task record. A mismatch
   BLOCKS the commit (P-5). Report the divergence; never edit a record's
   declared file list post-hoc to make an invalid commit legal.
3. **Commit Implementation Slice:** Commit with conventional commit subject
   style carrying NO task IDs in the subject line (P-4). The commit body
   records `owns: <TASK-ID>`, exact changed files, shared files, and
   unowned/pre-existing disclosures.
4. **Capture Commit SHA:** Capture the generated Phase A commit SHA for
   inclusion in the task record.
5. **Shared Implementation Files (Group Commit):** If an implementation file
   is shared with other tasks, the slice is NOT committed separately. The
   sharing tasks are committed together in a single joint group commit
   where each participating task has an authorized active manifest in
   `tasks/active/` and has passed applicable review gates.

### Phase B: Completion Record Filing Commit
1. **Update Completion Record:** Update the task record in `tasks/active/` to
   `Status: DONE` and populate its `## Completion Evidence` / `### Commit`
   section with the Phase A commit SHA, changed files, and validation
   evidence.
2. **Move Task Record:** Move the completed record from `tasks/active/` to
   `tasks/completed/`.
3. **Staging Verification (P-5):** Stage strictly the single completion
   record being filed. Verify `git diff --cached --name-status` matches
   strictly the completion record (accounting for Git's file representation:
   rename `R<score>`, deletion `D` + addition `A`, or direct addition `A`).
   No implementation changes and no other files may be staged.
4. **Commit Completion Record (Option 1A):** Commit the completion record
   under its own dedicated bookkeeping commit. The commit subject carries
   NO task IDs (P-4); the body carries `owns: <TASK-ID>`, the filed record path,
   and discloses `none` for unowned/pre-existing paths.
5. **Prohibition of Batching:** Each completion record receives its own
   dedicated Phase B commit (Option 1A). Batching multiple completion
   records into a single commit is prohibited.

---

## 6.2 Policy (P-1…P-6)

```text
P-1  Branch model:       Single `master`, as today. No per-task branch, no
                         worktree per task, no tag per task. A task's changes
                         are never pushed to a new branch to "isolate" them.
P-2  Commit granularity: Applied separately across both phases:
                         - Phase A (Implementation Slice): ONE COMMIT PER TASK
                           SLICE, created only when the staged set matches
                           EXACTLY the task's canonical declared implementation
                           file set. If a file is shared with another task,
                           a single joint group commit is used, naming each
                           participating task (all of which must have authorized
                           active manifests and passed review).
                         - Phase B (Completion Record Filing): ONE COMMIT PER
                           COMPLETION RECORD (Option 1A). Batching multiple
                           completion records into a single commit is prohibited.
P-3  Ownership           - Phase A Ownership Precondition: Requires an
     precondition:         authorized active task manifest in `tasks/active/`
                           that has passed applicable review gates
                           (`quality/review.md`). No implementation file may
                           be committed under a task without an authorized
                           active manifest.
                         - Phase B Ownership Precondition: Files strictly the
                           relevant completion record into `tasks/completed/`
                           (Option 1A), requiring an existing valid Phase A
                           commit SHA documented within that record.
                         - Unowned / Pre-Existing Files: A path with no
                           recorded owner is committed only under a commit whose
                           body explicitly discloses it ("unowned/pre-existing:
                           <paths>") and never under an invented task ID. No
                           exceptions may be created that conflict with
                           higher-priority instructions.
P-4  Mapping:            Task IDs go in the COMMIT BODY, never in the subject
                         (preserving the existing conventional-commit subject
                         style, which carries no task IDs, for both Phase A
                         and Phase B commits). The body names each task under
                         `owns: <TASK-ID>`, its file subset, shared files, and
                         disclosures.
P-5  Truth rule &        A commit must not contradict any record's declared
     staging             file list. Verification is performed separately:
     verification:       - Phase A: Staged set (`git diff --cached --name-status`)
                           must match the record's canonical `Declared Files`
                           exactly with zero extraneous files, excluding the
                           task record itself. Mismatch blocks the commit.
                         - Phase B: Staged set must match strictly the single
                           completion record being filed in `tasks/completed/`
                           (as rename `R`, delete `D` + add `A`, or direct
                           addition `A`).
P-6  Prohibitions:       No amend, no rebase, no force push, no history
                         rewrite, no `git commit -a`, no staging of a file the
                         commit does not own, no commit while a file's owner is
                         unresolved without the P-3 disclosure, no batching of
                         completion records into a single commit (Option 1B
                         prohibited), and no duplicate implementation commits.
```

---

## 6.3 Commit Body Shapes

### Phase A: Implementation Slice Commit Body Shape
```text
<type>: <conventional subject — carries no task ids>

<task id> — <what that task contributed>
<task id> — <...>

owns: <task id(s)>
files: <exact paths, or "see the record's declared file set">
shared with: <other task ids, and which paths, or "none">
unowned/pre-existing: <paths, or "none">
```

### Phase B: Completion Record Filing Commit Body Shape
```text
docs: <conventional subject — carries no task ids, e.g. "file the foo completion record">

<task id> — its own record only; it declares no other path.

owns: <task id>
files: tasks/completed/<task-file>.md
shared with: none
unowned/pre-existing: none
```

---

## 6.4 Interruption Recovery Protocol

When an agent or process is interrupted between Phase A and Phase B (or when
recovering unfiled tasks), follow this deterministic protocol:

1. **Inspect Git History:** Search `git log` for matching `owns: <TASK-ID>` or
   explicitly authorized group ownership in the commit body (not merely a loose
   task-ID string anywhere in a commit message).
2. **Verify Changed Paths:** Inspect the candidate commit
   (`git show --name-status <SHA>`) and verify that its actual changed paths
   match the task's applicable declared file set.
3. **Inspect Subsequent History and Working Tree:** Inspect subsequent commits
   (`git log <SHA>..HEAD`) and the current working tree (`git status`).
   - Do not assume the candidate commit must be `HEAD`.
   - Do not automatically reject every intervening commit; evaluate whether
     subsequent commits affect the declared files, whether they are already
     integrated, and whether the current repository state can be safely
     established.
4. **Verify Review and Validation Evidence:** Verify that the implementation
   passed applicable review gates (`quality/review.md`) and validation before
   accepting an existing Phase A commit.
5. **Mandatory STOP on Ambiguity:** If ownership, provenance, file scope,
   validation evidence, or integration status remains ambiguous: STOP per
   `AGENTS.md` §20 and escalate to the human / Product Owner.
6. **No History Rewriting or Duplicate Commits:** Never create duplicate
   implementation commits or rewrite Git history (`git reset`,
   `git commit --amend`, `git rebase`, `git push --force`) to repair an
   ambiguous state.
7. **Resume Phase B:** If and only if the Phase A commit is verified, valid,
   and attributable, update the task record with the Phase A commit SHA and
   file it under Phase B.

---

## 6.5 Pre-Adoption History (Historical Exception — Not a Precedent)

This rule entered the repository **after** commit `9b5c5fe` ("feat: add backend
card and relic content projection with client presentation tests") had already
been created out of band. That commit complied with none of P-2…P-4:

```text
9b5c5fe   73 files staged in one batch
          body carries no task id and no unowned/pre-existing disclosure
          it commits all 16 of the 16 paths TASK-223 §2 attributes to TASK-209,
          a task with no record in tasks/completed/
```

It is recorded here as a **pre-adoption batch commit** — a historical
exception. It is not a precedent, the practice must not be repeated, and it is
not to be remedied by rewriting history (P-6). What it already placed in
history can be recorded prospectively by a task record or a disclosure; it
cannot be retroactively attributed inside the commit itself.
