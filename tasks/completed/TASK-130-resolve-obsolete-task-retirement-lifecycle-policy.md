# TASK-130 — Resolve Obsolete and Redundant Task Retirement Lifecycle Policy

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and tasks/ by path and section.

  THIS TASK OBTAINS AND RECORDS A WORKFLOW / LIFECYCLE DECISION ONLY.
  It does not implement game code, does not modify gameplay rules, and does
  not directly alter existing tasks (TASK-079, TASK-099, TASK-102) prior to
  the policy decision being finalized.
-->

---

## Metadata

```text
Task ID:           TASK-130
Type:              DOCUMENTATION
Status:            DONE (Lifecycle policy decided, recorded, and applied to
                   tasks/TASK_LIFECYCLE.md. All 14 decision items resolved.
                   Zero source code or gameplay rules modified. Quality review
                   passed per quality/review.md §1. File moved to tasks/completed/
                   per tasks/TASK_LIFECYCLE.md §3.)
Risk:              MEDIUM (TASK_TYPES.md §4 — DOCUMENTATION baseline LOW–MEDIUM;
                   classified MEDIUM because the resulting policy governs the
                   terminal lifecycle rules and transition diagram across the
                   entire tasks/ directory and defines how historical and future
                   redundant tasks are reconciled.)
Priority:          HIGH (Multiple unexecuted tasks — TASK-079, TASK-099, TASK-102
                   — are blocked from valid lifecycle progression because current
                   TASK_LIFECYCLE.md rules forbid BACKLOG/READY -> DONE and provide
                   no legal retirement state.)
Primary Agent:     review (TASK_TYPES.md §2 / AGENT_SELECTION.md §1 —
                   "Documentation change: Primary Agent Review")
Supporting Agents: orchestrator (lifecycle workflow governance),
                   testing (traceability and audit verification)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-079 (BLOCKED — context example),
                   TASK-099 (BACKLOG — context example),
                   TASK-102 (READY — context example)
Blocks:            Lifecycle reconciliation of TASK-079, TASK-099, TASK-102,
                   and future decomposed/redundant tasks
Estimate:          Simple (policy decision and lifecycle specification only;
                   zero code, zero gameplay rules)
```

---

## Problem Statement

The repository has identified a recurring lifecycle gap: several tasks have become obsolete or fully redundant because their intended deliverables were completed through downstream decomposition tasks without the original task itself ever being independently executed.

Confirmed historical examples:

```text
TASK-079 (BLOCKED)
  → Original blockers resolved out-of-band by TASK-080..085
  → Entire scope satisfied; task itself never resumed
  → Current lifecycle provides no legal exit from BLOCKED without resumption

TASK-099 (BACKLOG)
  → Documentation deliverables completed by TASK-080, TASK-081, TASK-100
  → Task never independently executed
  → TASK_LIFECYCLE.md §2 explicitly defines BACKLOG → DONE as an invalid transition

TASK-102 (READY)
  → Implementation fully decomposed and completed by TASK-107, TASK-115, TASK-120, TASK-122
  → Task never independently executed
  → TASK_LIFECYCLE.md §2 explicitly defines READY → DONE as an invalid transition
```

`tasks/TASK_LIFECYCLE.md` defines only six states (`BACKLOG`, `READY`, `IN PROGRESS`, `IN REVIEW`, `BLOCKED`, `DONE`), explicitly rejects `BACKLOG → DONE` and `READY → DONE`, and provides no formal mechanism or state (such as `SUPERSEDED`, `CANCELLED`, `OBSOLETE`, or `RETIRED`) to legally close or retire redundant tasks. As a result, obsolete tasks remain indefinitely in `tasks/backlog/` or `tasks/blocked/`, creating ambiguity for agents and the orchestrator.

---

## Objective

Obtain and record an authoritative product/workflow lifecycle decision determining how the repository legally retires and reconciles unexecuted tasks whose scope has already been fully satisfied by downstream or decomposition tasks, preserving historical auditability without falsifying execution records or violating lifecycle state machine constraints.

---

## Authoritative References

- `AGENTS.md` §14 (Code Change Process), §16 (Task Discipline), §20 (Stop Conditions), §22 (Definition of Done), §23 (Final Principle)
- `tasks/TASK_LIFECYCLE.md` §1 (States), §2 (Transition Diagram and Invalid Transitions), §3 (State Definitions and Transition Criteria)
- `tasks/TASK_TYPES.md` §2 (DOCUMENTATION type definition and workflow mapping)
- `tasks/README.md` §5 (Folder Structure), §7 (How an Agent Picks Up a Task), §13 (Task Decomposition Guidelines)
- `.ai/README.md` §1 (Position in Hierarchy), §5 (Workflow Boundaries), §18 (Documentation Update Policy)
- `.ai/workflow/core/completion.md` §1 (Definition of Done), §3 (If Completion Cannot Be Reached)
- `.ai/workflow/quality/review.md` §1 (Review Checklist)
- `.ai/workflow/documentation/documentation-change.md` §1–§4 (Documentation Change Workflow)

---

## Scope

### In Scope

1. **Policy Decision Formulation:** Presenting the lifecycle retirement problem and recording an explicit decision on the canonical lifecycle model for obsolete/redundant tasks.
2. **Lifecycle State Machine Evaluation:** Introducing a formal terminal retirement state (`SUPERSEDED`) to `tasks/TASK_LIFECYCLE.md` and defining its state machine properties.
3. **Transition Rules Definition:** Defining valid transitions and criteria from `BACKLOG`, `READY`, and `BLOCKED` for redundant tasks.
4. **Evidence & Audit Standards:** Defining mandatory reconciliation evidence required to prove a task's deliverables are 100% satisfied elsewhere before retirement is permitted.
5. **Partial Decomposition Rules:** Defining how partially satisfied tasks must be handled (re-scoping vs. keeping active) versus completely superseded tasks.
6. **Documentation Update:** Applying the approved policy directly to `tasks/TASK_LIFECYCLE.md` per `documentation/documentation-change.md`.

### Out of Scope

- Implementing source code, test changes, or gameplay mechanics.
- Modifying `docs/01-game-design/` or `docs/02-technical/` documents.
- Modifying ADRs unrelated to task lifecycle management.
- Modifying `tasks/blocked/TASK-079-*.md`, `tasks/backlog/TASK-099-*.md`, or `tasks/backlog/TASK-102-*.md` during this task (their reconciliation is a separate follow-up action).
- Modifying completed tasks (`tasks/completed/*`).
- Deleting any task file.
- Inventing completion evidence or fabricating implementation claims.

---

## Decision Record (Items 1–14)

```text
1. Obsolete Task Retirement:
   Permitted. Unexecuted tasks whose entire deliverables are satisfied downstream
   may be formally retired to prevent indefinite backlog stalling.

2. Lifecycle State:
   Dedicated terminal state `SUPERSEDED` (distinct from `DONE`). `DONE` remains
   strictly reserved for tasks directly executed, tested, and reviewed.

3. Legal Transitions:
   - `BACKLOG → SUPERSEDED` (when lifecycle audit confirms 100% downstream coverage before pickup)
   - `READY → SUPERSEDED` (when lifecycle audit confirms 100% downstream coverage before execution)
   - `BLOCKED → SUPERSEDED` (when blocker and deliverables were resolved/satisfied out-of-band by decomposition tasks)

4. Approval Authority:
   Orchestrator Agent or Review Agent performs the audit; human or orchestrator
   authorizes the supersession transition.

5. Required Evidence:
   Must include a structured "Supersession / Downstream Satisfaction Evidence" section in the manifest:
   (a) Exact reason for obsolescence/supersession;
   (b) Requirement-by-requirement mapping from original scope to specific completed downstream tasks;
   (c) Verification that zero independent actionable scope remains;
   (d) Explicit affirmation that the task was NOT directly executed.

6. File Disposition:
   Moved from `tasks/backlog/` or `tasks/blocked/` to `tasks/completed/`.

7. Immutability:
   Superseded tasks in `tasks/completed/` are immutable historical records.

8. Partial Decomposition:
   If downstream tasks satisfy only a subset of deliverables, the task is NOT superseded.
   It remains active (`BACKLOG`, `READY`, or `BLOCKED`) with its scope narrowed and annotated.

9. Duplicate/Overlapping Tasks:
   Audited for unique scope. If one task's scope is completely subsumed by another, it is
   superseded; if overlapping, each task is narrowed to its unique scope.

10. Auditability:
    Historical manifest is preserved in full; no deletion, no rewriting of past blockers.

11. Validation Targets:
    TASK-079, TASK-099, and TASK-102 are recognized as primary historical targets
    to be reconciled in subsequent follow-up steps.

12. Documentation Update:
    `tasks/TASK_LIFECYCLE.md` is updated in this task to codify `SUPERSEDED`, its transitions,
    and its evidence rules.

13. Retroactive Application:
    Applies retroactively to existing unexecuted redundant tasks in the repository.

14. Scope Verification Bar:
    Requires 100% verification that every acceptance criterion of the obsolete task
    is satisfied in docs, code, or tests by completed tasks.
```

---

## Current State

- `tasks/TASK_LIFECYCLE.md` has been updated to include `SUPERSEDED`, allowing `BACKLOG → SUPERSEDED`, `READY → SUPERSEDED`, and `BLOCKED → SUPERSEDED`.
- `tasks/blocked/TASK-079-*.md`, `tasks/backlog/TASK-099-*.md`, and `tasks/backlog/TASK-102-*.md` remain unchanged, pending subsequent reconciliation.

---

## Acceptance Criteria

- [x] An explicit decision on the retirement/reconciliation lifecycle model is obtained and recorded in this task.
- [x] All 14 items in "Required Decision Coverage" are answered unambiguously.
- [x] The decision specifies exact valid state transitions from `BACKLOG`, `READY`, and `BLOCKED` for redundant tasks.
- [x] The decision specifies the required completion/reconciliation evidence format for superseded tasks.
- [x] The decision identifies necessary downstream documentation updates to `tasks/TASK_LIFECYCLE.md` and applies them.
- [x] The decision specifies the follow-up reconciliation actions for `TASK-079`, `TASK-099`, and `TASK-102`.
- [x] Zero source code (`src/`) and zero test files (`tests/`) are modified.
- [x] Zero gameplay documentation files (`docs/01-game-design/`, `docs/02-technical/`) are modified.
- [x] `TASK-079`, `TASK-099`, `TASK-102`, and all completed tasks remain unmodified during this task.
- [x] Quality review checklist passes (`quality/review.md` §1).
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[ ] src/ (none)
[ ] tests/ (none)
[ ] docs/ (none)
[x] tasks/completed/TASK-130-resolve-obsolete-task-retirement-lifecycle-policy.md (this file)
[x] tasks/TASK_LIFECYCLE.md (lifecycle states, transition diagram, definitions, and file movement summary updated)
```

---

## Implementation Notes

- **Policy Codification:** `tasks/TASK_LIFECYCLE.md` was updated with the canonical `SUPERSEDED` state and transitions.
- **Strict Scope Isolation:** `TASK-079`, `TASK-099`, and `TASK-102` were untouched during this task.

---

## Testing Requirements

### Required Verification

```text
[x] Documentation consistency — verified that tasks/TASK_LIFECYCLE.md is internally consistent and free of dead-ends.
[x] Completeness audit       — verified all 14 decision coverage questions are answered.
[x] Unmodified-file guards   — verified zero diff in src/, tests/, docs/01-game-design/, docs/02-technical/, and other tasks.
```

---

## Completion Evidence

### Decision Recorded

The authoritative lifecycle policy establishes the `SUPERSEDED` terminal state for tasks whose scope is 100% satisfied by downstream decomposition tasks without direct execution. Legal transitions `BACKLOG → SUPERSEDED`, `READY → SUPERSEDED`, and `BLOCKED → SUPERSEDED` are formally enabled and documented in `tasks/TASK_LIFECYCLE.md`.

### Changed Files

- `tasks/TASK_LIFECYCLE.md` — added `SUPERSEDED` state to Section 1, Section 2 (diagram & transitions), Section 3 (definition), Section 4 (file movements), and Section 5 (workflow relations).
- `tasks/completed/TASK-130-resolve-obsolete-task-retirement-lifecycle-policy.md` — recorded policy and completion evidence.

### Downstream Follow-Up Actions Identified

- Reconcile `TASK-079` (`tasks/blocked/` → `tasks/completed/` as `SUPERSEDED (by TASK-080..085)`).
- Reconcile `TASK-099` (`tasks/backlog/` → `tasks/completed/` as `SUPERSEDED (by TASK-080, TASK-081, TASK-100)`).
- Reconcile `TASK-102` (`tasks/backlog/` → `tasks/completed/` as `SUPERSEDED (by TASK-107, TASK-115, TASK-120, TASK-122)`).
