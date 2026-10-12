# core/task-selection.md — Task Selection & Incremental Project Audit

**Version:** 1.0

> Purpose: orchestrate project baseline collection, audit-depth selection (FAST / INCREMENTAL / DEEP), candidate task validation, and next-task recommendation while maintaining a token-efficient navigation cache.

---

## 1. Overview & Principles

Selecting the next eligible task must not require re-reading the entire repository or historical backlog on every invocation.

This workflow enforces token-efficient project inspection by separating concerns:
- **Baseline Evidence:** Cheap Git metadata and task folder counts (`scripts/collect-project-state.sh`).
- **Navigation Cache:** Compact, verified status cache (`.ai/PROJECT_STATE.md`).
- **Readiness Verification:** Targeted evaluation of candidate tasks using minimal sufficient evidence (`skills/discovery/task-readiness-assessment.md`).

Workflows compose around this runbook: when an agent or orchestrator asks "What should I work on next?" or needs to verify project readiness, this workflow executes.

---

## 2. Audit Depths & Selection Rules

The workflow dynamically selects one of three audit modes based on repository evidence:

```text
Check .ai/PROJECT_STATE.md vs. current Git HEAD and working tree
                             │
            ┌────────────────┼────────────────┐
            ▼                ▼                ▼
          FAST          INCREMENTAL          DEEP
      (HEAD matches;    (New commits/files;  (Snapshot absent/stale;
       snapshot fresh)   focused delta)       broad boundary shift)
```

### FAST Mode
- **When to Use:**
  - `.ai/PROJECT_STATE.md` exists, is marked `FRESH`, and its recorded `Audited HEAD SHA` matches the current `git rev-parse HEAD`.
  - Independently verified working tree is clean (`scripts/collect-project-state.sh` / `git status --porcelain` empty). A dirty tree rejects FAST even when HEAD matches.
- **What to Inspect:**
  - Repository baseline (`branch`, `HEAD SHA`, working-tree status).
  - `.ai/PROJECT_STATE.md` snapshot summary.
  - Active tasks in `tasks/active/` (if any, to respect WIP limit = 1).
  - Leading candidate's specification in `tasks/backlog/`.
  - Only the 1-hop dependencies and cited authoritative doc sections needed to verify that candidate.
- **What NOT to Inspect:**
  - Do not reread `tasks/completed/`, all ADRs, or unreferenced source code.

### INCREMENTAL Mode
- **When to Use:**
  - New commits exist since the snapshot's recorded SHA (`git diff --name-only <BASE_SHA> HEAD`).
  - Task files changed, or new decisions/docs were introduced that may affect candidate dependencies.
- **What to Inspect:**
  - Paths changed since the base commit (`scripts/collect-project-state.sh <BASE_SHA>`).
  - Affected candidate tasks and their direct upstream dependencies.
  - Only the authoritative documents touched by the commit delta.
  - Test evidence directly associated with the changed areas.
- **What NOT to Inspect:**
  - Do not re-audit components and domains unaffected by the commit delta.

### DEEP Mode
- **When to Use:** Triggered **only** when evidence requires escalation:
  1. `.ai/PROJECT_STATE.md` is absent, uninitialized, or materially inconsistent with git evidence.
  2. Architecture, gameplay/domain rules, or data models have undergone cross-cutting alterations.
  3. Authoritative documents conflict or required Product Owner decisions remain unresolved.
  4. Changes cross multiple architectural boundaries simultaneously (e.g., Domain + API + Protocol + Client).
  5. Test evidence contradicts prior conclusions.
  6. No candidate task in `tasks/backlog/` passes FAST or INCREMENTAL readiness validation.
- **What to Inspect:**
  - Scope audit across the affected domains.
  - Re-verify cross-document references and dependency graphs across affected modules.
  - *Constraint:* Deep audit is bounded to the affected domains—it never means scanning every source file in the repository.

---

## 3. Workflow Procedure

```text
Step 1: Baseline Collection  ──▶  Step 2: Mode Selection (FAST / INCR / DEEP)
                                            │
                                            ▼
Step 5: Recommendation       ◀──  Step 4: Candidate Readiness Assessment
         & Cache Update                    (task-readiness-assessment)
```

### Step 1: Baseline Collection
1. Run `scripts/collect-project-state.sh` (or inspect `git rev-parse HEAD` and `git status --porcelain`).
2. Record:
   - Current branch and full HEAD SHA.
   - Working tree state (clean vs dirty).
   - Task counts in `tasks/backlog/`, `tasks/active/`, `tasks/blocked/`, and `tasks/completed/`.
   - Latest completed task filename and ID.

### Step 2: Freshness & Audit Depth Selection
1. Inspect `.ai/PROJECT_STATE.md` **and** independently verify with `scripts/collect-project-state.sh` / `git status --porcelain` (never trust the snapshot alone):
   - If missing or unparseable → Select **DEEP**.
   - If recorded `Audited HEAD SHA` == current `HEAD SHA` **and** working tree is clean **and** the snapshot claims a clean baseline → Select **FAST**.
   - If recorded `Audited HEAD SHA` == current `HEAD SHA` **but** the working tree is dirty → Select **INCREMENTAL** (FAST is rejected; inspect uncommitted task/doc/code paths).
   - If recorded `Audited HEAD SHA` != current `HEAD SHA` → Select **INCREMENTAL** (pass recorded SHA as `BASE_COMMIT` to `scripts/collect-project-state.sh`).
   - If the recorded base SHA is invalid or missing in this clone (e.g. shallow history) → Select **DEEP** (do not infer an empty delta).
   - If material contradictions or multi-boundary shifts are detected → Elevate to **DEEP**.

### Step 3: Candidate Discovery & Active Task Gate
1. **Check WIP Limit (`tasks/active/`):**
   - If a task is currently in `tasks/active/`, evaluate it first.
   - Per `AGENTS.md` §16 and `tasks/TASK_LIFECYCLE.md`, WIP limit is 1. If an active task is in progress, the recommended action is to complete or unblock the active task, rather than picking up a new backlog task.
2. **Check Blocked Tasks (`tasks/blocked/`):**
   - Note any tasks in `tasks/blocked/` and their blocking reasons.
3. **Discover Backlog Candidates (`tasks/backlog/`):**
   - If `tasks/active/` is empty, list files in `tasks/backlog/`.
   - If `tasks/backlog/` is empty, halt and report `NEEDS_TASK_INTAKE` (§Step 5).
   - Do **not** sort merely by highest task ID. Identify candidate tasks that align with current critical path.

### Step 4: Candidate Readiness Assessment
For the candidate task(s), invoke the `skills/discovery/task-readiness-assessment.md` skill:
1. Verify task lifecycle status (`BACKLOG` or `READY`).
2. Verify all declared upstream dependencies exist in `tasks/completed/`.
3. Check for required Product Owner / ADR decisions.
4. Verify MVP scope compatibility against `docs/00-overview/MVP_SCOPE.md` §1.
5. Verify alignment with cited authoritative documentation sections.
6. Verify testability and unambiguous acceptance criteria.

### Step 5: Prioritization, Recommendation & Snapshot Update
1. **Determine Outcome:**
   - **Case A: Eligible Candidate Found.**
     Rank candidates using:
     1. Critical-path dependency unblocking.
     2. MVP value and risk reduction.
     3. Implementation readiness and verifiability.
     4. Scope size and impact.
     Recommend the top candidate for transition to `READY` / `IN PROGRESS`.
   - **Case B: Backlog Empty.**
     Return status `NEEDS_TASK_INTAKE`. Recommend intake of next priority item from roadmap or user requirements.
   - **Case C: Candidates Blocked or Ambiguous.**
     Return status `BLOCKED` or `NEEDS_DECISION` with specific stop condition report.
2. **Update `.ai/PROJECT_STATE.md`:**
   - Update `Audited HEAD SHA`, `Audit Timestamp`, `Freshness Status`, task counts, candidate verdicts, and next recommended action.
   - Do **not** copy complete task files, specifications, or code into the snapshot.

---

## 4. Stop Conditions

Inherits all stop conditions from `AGENTS.md` §20 and `.ai/README.md` §13:
- If authoritative documents conflict, stop and report immediately using the `.ai/README.md` §13 format.
- If a candidate task requires an architectural or gameplay rule decision not recorded in `docs/`, mark `NEEDS_DECISION` and halt.
- If a candidate exceeds MVP scope, halt with `SCOPE_VIOLATION`.

---

## 5. Traceability & Consumed Skills

- **Invoked by:** Orchestrator agent, developers, or session startup scripts.
- **Skills Consumed:**
  - `skills/discovery/task-readiness-assessment.md` (mandatory evaluation skill)
  - `skills/discovery/documentation-discovery.md` (for targeted doc location)
  - `skills/quality/scope-validation.md` (for MVP scope boundary checks)
- **Artifacts Maintained:**
  - `.ai/PROJECT_STATE.md` (navigation cache)
