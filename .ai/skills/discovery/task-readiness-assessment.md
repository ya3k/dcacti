# skills/discovery/task-readiness-assessment.md — Skill: Task Readiness Assessment

**Version:** 1.0

> Assess candidate task readiness against authoritative documentation, dependencies, decisions, and acceptance criteria using minimal sufficient evidence.

## Purpose

Evaluate whether a candidate task in `tasks/backlog/` or `tasks/active/` is demonstrably ready for execution without performing an unconstrained full-repository audit. This skill bounds inspection to the candidate's declared specification, its direct 1-hop dependencies, and only the authoritative documents governing its domain.

## When to Use

- During task selection in `core/task-selection.md` to evaluate backlog candidates.
- Prior to transitioning any task from `BACKLOG` to `READY` or `READY` to `IN PROGRESS` (`tasks/TASK_LIFECYCLE.md` §3).
- When validating whether an active task is blocked by unmet prerequisites or unresolved design decisions.
- When an incremental audit detects changes in upstream dependencies or authoritative documentation that may impact candidate tasks.

## When Not to Use

- To classify an unformatted or un-intaken task request (use `core/task-intake.md` first).
- To author full technical plans or implementation code (use `core/planning.md` and `core/implementation.md`).
- To conduct deep code quality review of completed work (use `quality/review.md` and `quality/implementation-review.md`).
- To re-verify already completed and immutable tasks in `tasks/completed/`.

## Inputs

```text
Candidate task identifier or task file path (e.g., tasks/backlog/TASK-NNN-*.md)
Project state snapshot (.ai/PROJECT_STATE.md) or current Git HEAD baseline
Optional: Target domain filter or specific task priority directive
```

## Prerequisites & Required Context

- Candidate task file exists in `tasks/backlog/` or `tasks/active/`.
- Repository baseline (branch, HEAD SHA, working tree cleanliness).
- Current task lifecycle rules (`tasks/TASK_LIFECYCLE.md` §1–§3) and folder structure (`tasks/README.md` §5).
- MVP scope boundaries (`docs/00-overview/MVP_SCOPE.md` §1–§4).

## Authoritative Sources

```text
AGENTS.md §2, §6, §8, §16           Precedence, documentation routing, MVP protection, task discipline
docs/00-overview/MVP_SCOPE.md       Single source of truth for IN / OUT / FUTURE scope
tasks/README.md                     Task system rules and structure
tasks/TASK_LIFECYCLE.md             States, transitions, and commit protocol
docs/03-decisions/ADR/              Architectural decisions and recorded rationale
docs/01-game-design/*               Domain rules (when candidate touches gameplay)
docs/02-technical/*                 Technical contracts (when candidate touches backend/realtime/client)
.ai/PROJECT_STATE.md                Compact navigation cache (verified against Git HEAD)
```

## Procedure

1. **Verify Lifecycle State & File Integrity:**
   - Confirm candidate file exists and is located in `tasks/backlog/` or `tasks/active/`.
   - Read the task's frontmatter and `Status:` field. A candidate must have `Status: BACKLOG` (evaluating for `READY`) or `Status: READY` (evaluating for pickup). If marked `DONE` or `SUPERSEDED`, halt with invalid state.
   - Do not assume the highest task ID is the next task.

2. **Verify Dependency Satisfaction:**
   - Inspect the `Dependencies:` or `Prerequisites:` section in the candidate task file.
   - Cross-check each referenced upstream task against `tasks/completed/` and Git history.
   - If an upstream dependency is missing, uncompleted, or in `tasks/blocked/`, mark candidate as `DEPENDENCY_UNSATISFIED`.

3. **Check Product Owner & Architectural Decisions:**
   - Check if the task depends on an unresolved decision, open question, or draft ADR (`docs/03-decisions/README.md` §8).
   - If the task requires a design choice that is unrecorded in `docs/` or an approved ADR, classify as `NEEDS_DECISION`.

4. **Validate MVP Scope Compatibility:**
   - Cross-check candidate's declared systems and mechanics against `docs/00-overview/MVP_SCOPE.md`.
   - Hand scope classification to `quality/scope-validation` principles: confirm all items are listed in §1 `IN`. If any item is in §2 `OUT`, §3 `FUTURE`, or unlisted, mark candidate as `SCOPE_VIOLATION`.

5. **Verify Authoritative Documentation Alignment:**
   - Read only the specific sections of `docs/` cited in the candidate task.
   - Verify that cited files and section numbers exist.
   - Confirm there are no obvious contradictions between the task description and the authoritative doc sections. If a conflict exists, trigger stop condition (§Stop Conditions).

6. **Validate Acceptance Criteria & Testability:**
   - Verify that the task specifies binary, unambiguous acceptance criteria.
   - Confirm required validation commands (e.g., `dotnet test`, `npm run test:run`) and declared file sets are identified.
   - If acceptance criteria are vague or untestable, mark candidate as `INSUFFICIENT_CRITERIA`.

7. **Rank and Recommend:**
   - If candidate passes checks 1–6, evaluate candidate eligibility.
   - When multiple candidates are eligible, rank using repository prioritization:
     1. Dependency and critical-path readiness (unblocks subsequent work).
     2. MVP value or material risk reduction (core battle/gameplay over optional presentation).
     3. Implementation readiness and verifiability (concrete spec and clear test surface).
     4. Scope size and impact (compact, well-bounded units over sprawling changes).
   - If no candidate passes, return an explicit `BLOCKED` or `NEEDS_DECISION` outcome.

## Outputs

```text
Task Readiness Assessment Report
- Candidate Task: <TASK-ID> (<File Path>)
- Current Lifecycle Status: <BACKLOG | READY | BLOCKED>
- Dependency Status: <SATISFIED | UNMET (List unmet dependencies)>
- Scope Status: <IN_MVP | SCOPE_VIOLATION>
- Documentation Alignment: <ALIGNED | CONFLICT_DETECTED | MISSING_REF>
- Decision Status: <APPROVED | NEEDS_DECISION (List pending decisions)>
- Acceptance Criteria Quality: <SUFFICIENT | INSUFFICIENT>
- Final Verdict: <READY_FOR_PICKUP | READY_FOR_TRIAGE | BLOCKED | NEEDS_DECISION>
- Ranking Justification: <Brief explanation of priority rank>
```

## Validation

A readiness assessment is valid only if:
- [ ] Only the candidate task specification, direct 1-hop dependencies, and cited doc sections were read.
- [ ] The assessment verified actual repository state (file existence, completed tasks in `tasks/completed/`).
- [ ] No game balance numbers, formulas, or schemas were duplicated into the assessment output.
- [ ] The candidate task was not presumed ready solely because of its numerical ID.
- [ ] Stop conditions were triggered immediately if documentation conflicts or scope violations were detected.

## Stop Conditions

Inherits all stop conditions from `AGENTS.md` §20 and `.ai/README.md` §13. Restates conditions specific to task readiness:

```text
STOP CONDITION

Problem:
Skill task-readiness-assessment stopped during step <step-number> for candidate <TASK-ID>.

Relevant sources:
<Candidate task path and conflicting documentation paths with sections>

Conflict / missing information:
<Unresolved dependency, missing PO decision, doc contradiction, or scope exclusion>

Proposed resolution:
<Smallest resolution: author missing decision, correct doc citation, or supersede task>

Waiting for:
<Product Owner decision / upstream task completion / documentation reconciliation>
```

## Common Failure Modes

- **Full-Repo Audit Bleed:** Reading all historical task files in `tasks/completed/` instead of checking existence of specific direct dependencies.
- **Highest ID Bias:** Selecting the candidate with the highest number without verifying dependency graph readiness.
- **Silent Scope Creep:** Approving a candidate that introduces features from `MVP_SCOPE.md` §2 or §3 without checking `MVP_SCOPE.md` §1.
- **Unverified Assumptions:** Marking dependencies satisfied based on conversational context rather than verified files in `tasks/completed/`.

## Traceability

- **Used by:** `.ai/workflow/core/task-selection.md`, `core/task-intake.md`
- **Reads:** `tasks/backlog/*`, `tasks/active/*`, `tasks/completed/*`, `docs/00-overview/MVP_SCOPE.md`, `docs/03-decisions/ADR/*`, `.ai/PROJECT_STATE.md`
- **Produces:** Task Readiness Assessment Report
- **Depends on:** `discovery/documentation-discovery`, `quality/scope-validation`
