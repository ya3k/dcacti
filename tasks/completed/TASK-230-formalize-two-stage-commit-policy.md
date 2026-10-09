# TASK-230 — Formalize Two-Stage Commit Policy

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
-->

## Metadata

```text
Task ID:           TASK-230
Type:              DOCUMENTATION
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     review
Supporting Agents: orchestrator, testing
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery, quality/documentation-consistency, quality/scope-validation
Dependencies:      TASK-224, TASK-229
Declared Files:    tasks/TASK_LIFECYCLE.md, .ai/workflow/core/completion.md, tasks/TASK_TEMPLATE.md, tasks/README.md
```

---

## Objective

Formalize Design A (Formal Two-Stage Commit Protocol) and Option 1A (individual completion record bookkeeping commits) across the repository governance and workflow documentation. Reconcile task lifecycle transitions, commit ownership rules (P-1…P-6), staging verification, shared-file implementation commits, and interruption recovery across the four declared governance files, eliminating the structural tension between exact implementation slice staging and immutable completion record filing.

---

## Authoritative References

- `AGENTS.md` §§4, 16, 17, 20, 22 — Global engineering contract, conflict resolution, task discipline, stop conditions, and Definition of Done
- `docs/AGENTS.md` §§4, 16, 17, 20, 22 — Documentation-layer engineering contract
- `tasks/TASK_LIFECYCLE.md` §§2, 3, 4, 5, 6.1, 6.2 — Transition diagram, state definitions and DONE checklist, file movement summary, workflow gate alignment, commit step, and commit ownership policy (P-1…P-6)
- `.ai/workflow/core/completion.md` §§1–2 — Definition of Done and final report structure
- `tasks/TASK_TEMPLATE.md` — Canonical task manifest template
- `tasks/README.md` §§3–7 — Task system structure, ID conventions, file naming, folder transitions, and creation/pickup flow
- `tasks/completed/TASK-224-commit-step-and-task-209-record.md` — Historical adoption of the commit step and P-1…P-6
- `tasks/completed/TASK-229-fix-superseded-card-cost-rule-in-signalr-protocol.md` — Practical precedent for the two-stage commit protocol (Phase A commit `0f6beb7`, Phase B commit `8dc8810`)
- `tasks/completed/TASK-223-commit-worktree-ownership.md` — Origin of the commit ownership principles (P-1…P-6)
- `docs/00-overview/MVP_SCOPE.md` §1 — Scope confirmation

---

## Scope

### In Scope

1. **Formalize Two-Stage Commit Protocol (Design A) in `tasks/TASK_LIFECYCLE.md` across §§2, 3, 4, 5, 6.1, and 6.2:**
   - **§2 (Transition Diagram & Allowed Transitions):** Update allowed transitions for `IN REVIEW → DONE` to specify the two-stage commit sequence (Phase A implementation slice commit executed during the transition, followed by Phase B completion record filing commit).
   - **§3 (State Definitions — DONE):** Update the `DONE` checklist and state definition to require:
     - Phase A implementation slice committed per §6 (staged set equals the record's declared file set; group commit when a file is shared; unowned/pre-existing paths disclosed per P-3).
     - Phase B completion record filed in `tasks/completed/` documenting the Phase A commit SHA.
     - Require the §3 DONE checklist to remain strictly consistent with `.ai/workflow/core/completion.md` §1.
   - **§4 (File Movement Summary):** Update the `IN REVIEW → DONE` transition summary to specify that the task's declared implementation files are staged and committed in Phase A, followed by moving/filing the task record to `tasks/completed/` via Phase B.
   - **§5 (Relationship to Workflows):** Update the `IN REVIEW → DONE` workflow gate mapping to require satisfying `.ai/workflow/core/completion.md` §1, executing the Phase A implementation commit, and filing the completion record via Phase B.
   - **§6.1 (The Commit Step):** Define the two discrete phases:
     - **Phase A (Implementation Slice Commit):** Stages and commits strictly the task's declared implementation files (`Declared Files`). The task record is NOT staged in Phase A. The actual Phase A commit SHA is generated and captured. For shared implementation files, a single joint group commit is required where each participating task has an authorized active manifest and has passed applicable review gates.
     - **Phase B (Completion Record Filing Commit):** Each completion record receives its own dedicated bookkeeping commit (Option 1A). Moves/files the completion record in `tasks/completed/`. The record documents the Phase A commit SHA. Phase B contains no implementation changes and never batches unrelated completion records.
   - **§6.2 (Policy Rules P-1…P-6):** Reconcile policy rules P-2, P-3, P-4, and P-5 with the two-stage protocol:
     - **P-2 (Commit Granularity):** Apply granularity separately: Phase A implementation slices commit strictly declared files (or joint group commit for shared files); Phase B individual completion records receive dedicated 1:1 commits (batching prohibited).
     - **P-3 (Ownership Precondition):**
       - Phase A ownership precondition: requires an authorized active task manifest (in `tasks/active/`) that has passed applicable review gates (`quality/review.md`). No implementation file may be committed under a task without an authorized active manifest.
       - Phase B ownership precondition: files strictly the relevant completion record into `tasks/completed/` (Option 1A), requiring an existing valid Phase A commit SHA documented within that record.
       - Unowned / pre-existing files: preserve the existing rule that any path with no recorded owner is committed only under a commit whose body explicitly discloses it (`unowned/pre-existing: <paths>`) and never under an invented task ID.
       - Do not create exceptions that conflict with higher-priority instructions.
     - **P-4 (Mapping & Subject Discipline):** Explicitly mandate that both Phase A and Phase B commit subjects must NOT contain task IDs; task IDs belong exclusively in the commit body under `owns: <TASK-ID>` (preserving conventional commit subject style). The commit body carries `owns: <TASK-ID>`, file details, and disclosures.
     - **P-5 (Truth Rule & Staging Verification):** Define Phase A exact-set verification separately from Phase B completion-record filing verification:
       - Phase A exact-set verification: compare `git diff --cached --name-status` against the record's canonical `Declared Files`; must match exactly with zero extraneous files and excluding the task record itself. A mismatch blocks the commit; never edit declared files post-hoc to make an invalid commit legal.
       - Phase B completion-record filing verification: verify `git diff --cached --name-status` matches strictly the single completion record being filed. Accurately describe Phase B staged paths in Git's representations: rename (`R<score>  tasks/active/<file>  tasks/completed/<file>`), deletion + addition (`D  tasks/active/<file>` + `A  tasks/completed/<file>`), or direct addition (`A  tasks/completed/<file>`).
   - **§6 (Interruption Recovery Protocol):** Add explicit, safe interruption recovery guidelines:
     - Inspect Git history via `git log` requiring matching `owns: <TASK-ID>` or explicitly authorized group ownership in the commit body, not merely a loose task-ID string anywhere in a commit message.
     - Verify the candidate commit's actual changed paths (`git show --name-status <SHA>`) against the task's applicable declared file set.
     - Inspect subsequent commits and the current working tree (`git log <SHA>..HEAD`, `git status`).
     - Verify review and validation evidence before accepting an existing Phase A commit.
     - Do not assume the candidate commit must be `HEAD`.
     - Do not automatically reject every intervening commit; evaluate whether later changes affect the declared files, whether they are already integrated, and whether the current repository state can be established safely.
     - STOP (report to human / Product Owner per `AGENTS.md` §20) if ownership, provenance, file scope, validation, or integration status remains ambiguous.
     - Never create duplicate implementation commits or rewrite history (`git reset`, `git commit --amend`, `git rebase`, `git push --force`) to repair an ambiguous state.

2. **Align Completion Workflow in `.ai/workflow/core/completion.md`:**
   - Update §1 (Definition of Done) to reflect that the task slice was committed via Phase A and the completion record is filed via Phase B, keeping §1 strictly consistent with `tasks/TASK_LIFECYCLE.md` §3 DONE checklist.
   - Update §2 (Final Report) to clarify that the `## Commit` section reports the Phase A implementation commit SHA (along with subject, owns, files, etc.), and that Phase B commits the completion record itself.

3. **Update Manifest Template in `tasks/TASK_TEMPLATE.md`:**
   - Remove all “and/or” ambiguity.
   - Specify one canonical `Declared Files:` field in `## Metadata` (comma-separated list of relative repository paths).
   - Require a dedicated `## Declared File Set (P-2 / P-5)` section that provides the explicit block list of exact implementation files, defining exactly that the block list and metadata field must strictly match.
   - Confirm compatibility with the existing task template structure and README conventions.
   - Structure `## Completion Evidence` to include the standard `### Commit` subsection for Phase A commit details (SHA, Subject, Owns, Files, Shared with, Unowned/pre-existing).

4. **Update System Overview in `tasks/README.md`:**
   - Update §5 (Folder Structure) and §7 (How an Agent Picks Up a Task) to reflect the two-stage commit protocol during the completion and archiving sequence.
   - Confirm compatibility with the canonical `Declared Files:` metadata field and dedicated file set section.

### Out of Scope

- Modifying `AGENTS.md`, `docs/AGENTS.md`, or any completed task records under `tasks/completed/`. All historical records and Git history remain immutable.
- Modifying production code (`src/`), test suites (`tests/`), game rules (`docs/01-game-design/`), technical specs (`docs/02-technical/`), or architectural decisions (`docs/03-decisions/ADR/`).
- Staging, committing, pushing, or mutating Git history during the creation of this backlog manifest.
- Batching multiple completion records into a single Phase B commit (Option 1A requires one Phase B commit per completion record).
- Amending, rebasing, resetting, or force-pushing Git commits.

---

## Current State

Under TASK-224, the commit step and P-1…P-6 rules were incorporated into `tasks/TASK_LIFECYCLE.md` §6 and `.ai/workflow/core/completion.md`. However, §6.1 specified a single commit step where staging exactly the declared file set conflicted with updating the task record with the commit hash before filing.

TASK-229 demonstrated the resolution by separating implementation and record filing into two stages:
- Phase A commit `0f6beb7` (`docs: align CardCast cost statement with authoritative rule`) committed only `docs/02-technical/SIGNALR_PROTOCOL.md`.
- Phase B commit `8dc8810` (`docs: file the card-cost rule correction completion record`) filed the record referencing `0f6beb7`.

The four governance documents have not yet been formalized to reflect this two-stage protocol, Option 1A individual filing, Git path verification mechanics, and interruption recovery across lifecycle sections §§2–6.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-230:
```text
tasks/TASK_LIFECYCLE.md
.ai/workflow/core/completion.md
tasks/TASK_TEMPLATE.md
tasks/README.md
```

*(Note: The task manifest file `tasks/completed/TASK-230-formalize-two-stage-commit-policy.md` is the specification manifest, not an implementation file, and is excluded from the implementation declared file set.)*

---

## Acceptance Criteria

- [x] Two-stage commit protocol (Phase A implementation slice, Phase B completion record filing) is formally defined in `tasks/TASK_LIFECYCLE.md` and `.ai/workflow/core/completion.md`.
- [x] `tasks/TASK_LIFECYCLE.md` is updated across §§2, 3, 4, 5, 6.1, and 6.2, with the §3 DONE checklist explicitly required to remain consistent with `.ai/workflow/core/completion.md` §1.
- [x] Option 1A is explicitly adopted: each completion record receives its own dedicated Phase B bookkeeping commit; batching unrelated records into one Phase B commit is prohibited.
- [x] P-3 ownership preconditions are specified: Phase A requires an authorized active task manifest in `tasks/active/` that has passed applicable review gates; Phase B files strictly the relevant completion record with an existing Phase A SHA documented. Existing rule for disclosing unowned/pre-existing files is preserved; no conflicting exceptions are created.
- [x] Phase A rules explicitly require staging only declared implementation files, excluding the task record, capturing the Phase A SHA, and using joint group commits for shared files where each participating task has an authorized manifest and has passed review gates.
- [x] P-4 is explicitly specified: Phase B commit subjects (as well as Phase A commit subjects) must NOT contain task IDs; task IDs belong exclusively in the commit body under `owns: <TASK-ID>`.
- [x] P-5 defines Phase A exact-set verification separately from Phase B completion-record filing verification, accurately describing Phase B staged paths for Git's file representation (rename `R`, delete `D` + add `A`, or direct `A`), staging strictly the completion record.
- [x] Completion record stores the Phase A commit SHA in its `## Completion Evidence` / `## Commit` section, never its own Phase B SHA.
- [x] Interruption recovery protocol requires matching `owns: <TASK-ID>` or authorized group ownership in commit body (not merely a task-ID string in a commit message), verifying actual changed paths against declared files, inspecting subsequent commits and the current working tree, verifying review and validation evidence before accepting an existing Phase A, allowing non-`HEAD` candidate evaluation without automatically rejecting intervening commits, stopping on ambiguity, and never creating duplicate implementation commits or rewriting history.
- [x] `tasks/TASK_TEMPLATE.md` removes all “and/or” ambiguity, specifies one canonical `Declared Files:` field in `## Metadata`, defines the dedicated `## Declared File Set (P-2 / P-5)` section requiring both representations to strictly match, confirms compatibility with template/README conventions, and includes Phase A commit tracking under Completion Evidence.
- [x] `tasks/README.md` §§5, 7 are updated to align with the two-stage commit protocol and template conventions.
- [x] The four declared implementation files are the only files modified; `AGENTS.md`, `docs/AGENTS.md`, and all completed records remain untouched.
- [x] No Git history is rewritten, amended, reset, or force-pushed.

---

## Affected Files & Areas

```text
[ ] src/backend/
[ ] src/frontend/client/
[ ] tests/
[x] docs/ (governance and task execution documentation)
    - tasks/TASK_LIFECYCLE.md
    - .ai/workflow/core/completion.md
    - tasks/TASK_TEMPLATE.md
    - tasks/README.md
```

---

## Implementation Notes

When implementing TASK-230:
1. **Lifecycle Scope Coverage (`tasks/TASK_LIFECYCLE.md`):** Ensure updates cover §§2, 3, 4, 5, 6.1, and 6.2. In §3 (DONE), ensure the checklist mirrors `.ai/workflow/core/completion.md` §1 exactly, keeping both documents synchronized.
2. **P-3 Ownership Preconditions:** Clearly separate Phase A and Phase B ownership preconditions:
   - Phase A requires an authorized active task manifest in `tasks/active/` that has passed applicable review gates (`quality/review.md`). No implementation file may be committed without an authorized active manifest.
   - Phase B files strictly the relevant completion record into `tasks/completed/` (Option 1A), requiring an existing valid Phase A commit SHA documented within that record.
   - Preserve the rule that unowned/pre-existing files must be disclosed in the commit body (`unowned/pre-existing: <paths>`) without inventing task IDs or creating conflicting exceptions.
3. **P-4 Mapping & Commit Subjects:** Explicitly state that neither Phase A nor Phase B commit subjects may contain task IDs (preserving conventional commit subject style). Task IDs belong exclusively in the commit body under `owns: <TASK-ID>`.
4. **P-5 Separate Staging Verification & Phase B Path Representations:** Explicitly separate Phase A exact-set verification (matches canonical `Declared Files`, zero extra files, excludes task record) from Phase B completion-record filing verification. In Phase B, account for `git diff --cached --name-status` showing rename (`R<score>  tasks/active/...  tasks/completed/...`), deletion + addition (`D  tasks/active/...` + `A  tasks/completed/...`), or direct addition (`A  tasks/completed/...`), avoiding contradictions while ensuring strictly the single completion record is staged.
5. **Robust Interruption Recovery Protocol:** Formulate the step-by-step recovery procedure:
   - Search `git log` for matching `owns: <TASK-ID>` or explicitly authorized group ownership in the commit body, not merely loose task-ID strings anywhere in the message.
   - Verify the candidate commit's actual changed paths (`git show --name-status <SHA>`) against the task's applicable declared file set.
   - Inspect subsequent commits (`git log <SHA>..HEAD`) and the current working tree (`git status`).
   - Verify review and validation evidence before accepting an existing Phase A commit.
   - Do not assume the candidate must be `HEAD`. Do not automatically reject every intervening commit; evaluate whether subsequent commits touch the declared files, whether they are already integrated, and whether the current repository state can be safely established.
   - If ownership, provenance, file scope, validation, or integration status remains ambiguous: STOP per `AGENTS.md` §20 and escalate to the Product Owner / human.
   - Strictly prohibit creating duplicate implementation commits or rewriting Git history (`git reset`, `git commit --amend`, `git rebase`, `git push --force`).
6. **Template Canonical Representation (`tasks/TASK_TEMPLATE.md`):** Eliminate all "and/or" ambiguity. Specify one canonical `Declared Files:` field in `## Metadata` (comma-separated list). Require the dedicated `## Declared File Set (P-2 / P-5)` section to provide the explicit block list and require both representations to strictly match. Confirm compatibility with template and README conventions.
7. **Group Commits vs Individual Commits:** Phase A allows joint group commits when files are shared (all participating tasks must have authorized active manifests and passed review gates), but Phase B is strictly 1:1 per completion record (Option 1A).
8. **Consistent File Naming & Immutability of History:** Keep filenames consistent across folder transitions (`TASK-230-formalize-two-stage-commit-policy.md`). Reinforce that historical commits (including `9b5c5fe`, TASK-224 commits, and TASK-229 commits) must not be rewritten.

---

## Testing & Verification Requirements

### Required Verification
```text
[x] Cross-document consistency — Verify definitions of DONE, Phase A, Phase B, and P-1…P-6 match across all four files
[x] Template integrity — Verify TASK_TEMPLATE.md contains Declared Files without redundant fields
[x] Git diff check — Run git diff --check to verify whitespace and formatting hygiene
[x] Scope verification — Verify git status / git diff shows changes strictly within the four declared files
```

### Key Edge Cases
- Interruption recovery when commits from other tasks land on `master` after Phase A but before Phase B: evaluating non-`HEAD` candidate commits without automatic rejection, verifying changed paths, integration status, and working tree.
- Ambiguous recovery state where ownership, provenance, file scope, validation, or integration cannot be proved with certainty: mandatory STOP without duplicate commits or history rewriting.
- Shared implementation files in Phase A committing multiple tasks together while Phase B files each record individually.
- Handling of direct completion record creation (e.g. retrospective audits) versus standard `tasks/active/` to `tasks/completed/` moves in Phase B path verification (`A` vs `R` / `D`+`A`).

---

## Stop Conditions

- If any proposed policy contradicts `AGENTS.md` or higher-priority engineering directives: STOP per `AGENTS.md` §4.
- If scope expansion beyond the four declared files is requested: STOP per `AGENTS.md` §20.
- If batching of completion records (Option 1B) or single-stage commit (Design B) is reintroduced: STOP and enforce Product Owner approved Design A / Option 1A.
- If history rewriting (`git commit --amend`, `git rebase`, `git reset`, `git push --force`) or duplicate implementation commits are proposed: STOP per P-6.
- If commit ownership, provenance, file scope, validation evidence, or integration status remains ambiguous during interruption recovery: STOP per `AGENTS.md` §20.

---

## Completion Evidence

### Commit
- Implementation slice (Phase A): `4d2df9b80beb89e3469bdea9719c2ed6af86ab64`
- Subject: `docs: formalize two-stage commit protocol across governance files`
- Owns: `TASK-230`
- Files: `tasks/TASK_LIFECYCLE.md`, `.ai/workflow/core/completion.md`, `tasks/TASK_TEMPLATE.md`, `tasks/README.md`
- Shared with: `none`
- Unowned / pre-existing: `none`

### Changed Files
- `tasks/TASK_LIFECYCLE.md` — Formalized two-stage commit protocol across §§2, 3, 4, 5, 6.1, and 6.2; synchronized §3 DONE checklist with `core/completion.md` §1; defined P-3 ownership preconditions, P-4 subject rules, P-5 path verification, Option 1A individual filing, and safe interruption recovery protocol.
- `.ai/workflow/core/completion.md` — Aligned Definition of Done (§1) and final report Commit reporting (§2) with two-stage commit protocol.
- `tasks/TASK_TEMPLATE.md` — Added canonical `Declared Files` metadata field and matching `## Declared File Set (P-2 / P-5)` section without "and/or" ambiguity, and Phase A commit tracking under Completion Evidence.
- `tasks/README.md` — Updated folder transitions and agent pickup/completion flow (§§5, 7) to align with two-stage commit protocol and template conventions.

### Validation Results
- `git diff --check` — PASS (exit code 0, 0 whitespace/formatting issues)
- `git diff --name-status` / `git diff --cached --name-status` — PASS (strictly the 4 declared implementation files in Phase A)
- Cross-document consistency audit — PASS (DONE checklist, Phase A, Phase B, P-1…P-6, Option 1A, and recovery rules consistent across all 4 files)

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed P-1…P-6 commit policy compliance
