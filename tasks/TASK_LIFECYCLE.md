# tasks/TASK_LIFECYCLE.md — Task Lifecycle

**Version:** 1.1 (§6 added: the commit step and commit ownership policy,
adopted by Product Owner Decision 1 = A under TASK-224)

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
IN REVIEW   → DONE            Review passes all quality/review.md §1 items
                               and the task's slice is committed (§6)
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
[ ] The task's slice committed per §6 (staged set equals the record's
    declared file set; group commit when a file is shared; a path with
    no recorded owner disclosed, never committed under an invented id)
```

**Who sets it:** Agent (after quality/review.md passes).

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

The commit step (§6) runs inside the IN REVIEW → DONE transition:

```text
IN REVIEW → DONE    the task's declared files are staged and committed
                    (§6), then the task record is filed as completed
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
IN REVIEW → DONE        core/completion.md §1 satisfied + the commit step (§6)
IN REVIEW → IN PROGRESS quality/review.md found defect requiring rework
BLOCKED → SUPERSEDED    Lifecycle reconciliation audit confirming blocker & scope resolved downstream
```

The workflow is the authoritative process — the lifecycle state in the
task file is a reflection of workflow progress, not a substitute for it.

---

# 6. Commit Step and Commit Ownership

**Adopted:** Product Owner Decision 1 = A, recorded under TASK-224, adopting
TASK-223 §6.2's P-1…P-6 as repository policy. This section is the single owner
of the commit rule; `core/completion.md` §1/§2 reference it and do not restate
it.

---

## 6.1 The commit step

A task's changes enter history through exactly one commit step, performed
inside the IN REVIEW → DONE transition (§4, §5) — after `quality/review.md` §1
passes, and before the task is filed as completed.

```text
1. Stage EXACTLY the file set the task's record declares. Stage nothing else.
2. Compare `git diff --cached --name-status` to that declared set. A mismatch
   BLOCKS the commit (P-5). Report the divergence; never edit a record's
   declared file list to make a commit legal.
3. Commit with the task ids in the BODY, never the subject (P-4).
4. File the record as completed and report the hash in the final report's
   `## Commit` section (`core/completion.md` §2).
```

---

## 6.2 Policy (P-1…P-6)

```text
P-1  Branch model:       single `master`, as today. No per-task branch, no
                         worktree per task, no tag per task. A task's changes
                         are never pushed to a new branch to "isolate" them.
P-2  Commit granularity: ONE COMMIT PER TASK SLICE, created only when the set
                         of files the task's record declares is EXACTLY the set
                         of files the task changed. If a file is shared with
                         another task, the slice is NOT committed separately;
                         the sharing tasks are committed together, as an
                         explicitly named group (e.g. "TASK-209 + TASK-210"),
                         and the commit body names each member task and its
                         part.
P-3  Ownership           no file may be committed under a task that has no
     precondition:       record in tasks/completed/. A file with no recorded
                         owner is committed only under a commit whose body
                         explicitly says so ("unowned/pre-existing: <paths>")
                         and never under an invented task id.
P-4  Mapping:            task ids go in the COMMIT BODY, never in the subject
                         (preserving the existing conventional-commit subject
                         style, which carries no task ids). The body names each
                         task, its file subset, and the shared/uncertain files.
P-5  Truth rule:         a commit must not contradict any record's declared
                         file list. If it must (because of sharing), the commit
                         body states the divergence and the record is the
                         authority.
P-6  Prohibitions:       no amend, no rebase, no force push, no history
                         rewrite, no `git commit -a`, no staging of a file the
                         commit does not own, no commit while a file's owner is
                         unresolved without the P-3 disclosure.
```

---

## 6.3 Commit body shape

```text
<type>: <conventional subject — carries no task ids>

<task id> — <what that task contributed>
<task id> — <...>

owns: <task id(s)>
files: <exact paths, or "see the record's declared file set">
shared with: <other task ids, and which paths>
unowned/pre-existing: <paths, or "none">
```

---

## 6.4 Pre-adoption history (historical exception — not a precedent)

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
