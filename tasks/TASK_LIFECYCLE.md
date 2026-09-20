# tasks/TASK_LIFECYCLE.md — Task Lifecycle

**Version:** 1.0

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
DONE          All core/completion.md §1 criteria are satisfied.
              Task is moved to completed/.
```

---

# 2. Transition Diagram

```text
BACKLOG ──────────────────────────────────→ READY
                                              │
                         ┌────────────────────┘
                         ↓
                     IN PROGRESS ◄────────────────────────────┐
                         │                                     │
            ┌────────────┼────────────┐                        │
            ↓            ↓            ↓                        │
         BLOCKED    IN REVIEW      BLOCKED                     │
            │            │                                     │
            │     ┌──────┴──────┐                              │
            │     ↓             ↓                              │
            │   DONE      IN PROGRESS ─────────────────────────┘
            │                (rework)
            │
    (human resolves)
            │
            └──────────────→ IN PROGRESS
```

### Allowed transitions

```text
BACKLOG     → READY           Orchestrator or requester confirms task is
                               ready (docs exist, scope confirmed)
READY       → IN PROGRESS     Agent picks up the task
IN PROGRESS → IN REVIEW       Implementation + tests complete
IN PROGRESS → BLOCKED         Stop condition fires during execution
IN REVIEW   → DONE            Review passes all quality/review.md §1 items
IN REVIEW   → IN PROGRESS     Review identifies a defect requiring rework
BLOCKED     → IN PROGRESS     Blocking condition resolved by human decision
```

### Invalid transitions

```text
BACKLOG     → IN PROGRESS     (must pass through READY)
BACKLOG     → DONE
READY       → DONE
READY       → BLOCKED         (if a blocker is found during READY
                               validation, return to BACKLOG)
IN REVIEW   → BLOCKED         (review findings are rework, not blocks;
                               use IN REVIEW → IN PROGRESS for rework)
DONE        → any             (completed tasks are immutable;
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

All core/completion.md §1 criteria are satisfied.

**What it means:**
```text
[ ] Implementation completed
[ ] Relevant validation passed (per core/validation.md depth)
[ ] No unresolved documentation conflict
[ ] No unintended scope expansion
[ ] Documentation impact checked and addressed if required
[ ] Architecture impact checked and addressed if required
```

**Who sets it:** Agent (after quality/review.md passes).

**File location:** `tasks/completed/`
**File move:** `active/` → `completed/`

**Completed tasks are immutable.** Do not edit a completed task's
implementation decisions or acceptance criteria. Create a new task
for follow-up work.

---

# 4. File Movement Summary

```text
Transition                 From         To
─────────────────────────  ──────────   ──────────
BACKLOG → READY            backlog/     backlog/     (no move; status field only)
READY → IN PROGRESS        backlog/     active/
IN PROGRESS → IN REVIEW    active/      active/      (no move; status field only)
IN REVIEW → IN PROGRESS    active/      active/      (no move; status field only)
IN PROGRESS → BLOCKED      active/      blocked/
BLOCKED → IN PROGRESS      blocked/     active/
IN REVIEW → DONE           active/      completed/
```

---

# 5. Relationship to Workflows

Each lifecycle transition maps to a specific workflow gate:

```text
BACKLOG → READY         Manual validation (READY criteria in §3)
READY → IN PROGRESS     core/task-intake.md + core/context-discovery.md
IN PROGRESS workflow     core/planning.md → core/implementation.md
IN PROGRESS → BLOCKED   Any stop condition (AGENTS.md §20)
IN PROGRESS → IN REVIEW quality/testing.md complete
IN REVIEW workflow       quality/review.md
IN REVIEW → DONE        core/completion.md §1 satisfied
IN REVIEW → IN PROGRESS quality/review.md found defect requiring rework
```

The workflow is the authoritative process — the lifecycle state in the
task file is a reflection of workflow progress, not a substitute for it.
