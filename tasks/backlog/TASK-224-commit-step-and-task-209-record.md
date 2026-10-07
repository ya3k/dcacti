# TASK-224 — Establish the Commit Step and Reconstruct the TASK-209 Record

<!--
  Follow-up created by TASK-223 under its Option C
  (tasks/completed/TASK-223-commit-worktree-ownership.md §10 F-1).
  Reserved id: highest existing task id (TASK-223) + 1, per tasks/README.md §3.
  Status is BACKLOG: it is not READY because it is blocked on the Product Owner
  decision F-2 (TASK-213 §10's self-contradiction). See Stop Conditions.

  TYPE CLASSIFICATION NOTE. `DOCUMENTATION` is the type because all four
  deliverables are documents: the workflow-layer commit step, the recorded commit
  policy, TASK-209's reconstructed record, and the TASK-223 record's own §6.3
  commit plan executed as its mechanical application. This mirrors TASK-213's own
  Kind taxonomy ("P + a small decision" for TASK-223) rather than inventing a
  seventh task type: TASK_TYPES.md §1 fixes six types and this task adds none.
-->

---

## Metadata

```text
Task ID:           TASK-224
Type:              DOCUMENTATION (workflow-layer process documentation + the
                   missing task record; the integration commit is the mechanical
                   application of the policy this task records)
Status:            BACKLOG
Risk:              MEDIUM (it writes the process that governs every future commit
                   and it authors a retrospective task record; it changes no
                   gameplay, API, schema, or contract)
Priority:          HIGH (F-1 of TASK-223: blocks TASK-221 from starting on an
                   attributable tree)
Primary Agent:     review
Supporting Agents: orchestrator, testing
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (3 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-223 (DONE — the ownership map and the deferred commit plan
                     this task applies), TASK-213 (DONE — the decision record whose
                     §10 commissions the commit/integration step and whose A-4
                     self-contradiction must be resolved first), and a Product
                     Owner decision on TASK-223 §10 F-2
Blocks:            TASK-221 (and every other implementation task that must not
                   absorb the 51 uncommitted entries)
```

---

## Objective

Close the process gap TASK-213 §10 (N-11) identified and TASK-223 documented: give
the repository an explicit, documented commit step and granularity, author the
TASK-209 record that does not exist, and then integrate the verified
`TASK-207 → TASK-213` chain into history in slices whose ownership TASK-223 §2 has
already established — without rewriting history, without amending an existing
commit, and without committing a file under a task that has no record.

---

## Authoritative References

- `tasks/completed/TASK-223-commit-worktree-ownership.md` — §2 (the per-file
  ownership map, the authority for every file assignment below), §3 (the
  ambiguities A-1…A-6), §4 (the absence of any commit rule), §6.2 (the P-1…P-6
  policy this task adopts) and §6.3 (the exact deferred commit plan)
- `tasks/completed/TASK-213-content-reachability-decision.md` §9/§10 — the decision
  that created TASK-223, the N-11 finding, and the self-contradictory
  "TASK-223 minimal scope" block at lines 677–690
- `tasks/completed/TASK-211-non-battle-screen-robustness-and-recovery.md`
  §Remaining 5 — "The TASK-209 record still does not exist under `tasks/`
  (audit NG-23)"
- `tasks/completed/TASK-208A-…md` §Remaining 1–3 and §TASK-209 Unblock Check — what
  TASK-209 was required to implement
- `tasks/completed/TASK-210-…md` §Metadata/§Diff audit/§Tests — TASK-209's
  co-located work and its verification continuity
- `tasks/README.md` §3 (ids are never reused), §4 (file naming), §5 (folders), §6
  (how a task is created), §7 (how an agent picks up a task)
- `tasks/TASK_LIFECYCLE.md` §3 (DONE and SUPERSEDED definitions, immutability)
- `.ai/workflow/core/completion.md` §1/§2 (Definition of Done, final report shape)
- `AGENTS.md` §4 (conflict resolution), §16 (task discipline), §17 (documentation
  change rule), §20 (stop conditions), §22 (Definition of Done)
- `.ai/README.md` §6 (source-of-truth rule), §18 (documentation update policy)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4.3 item 15 and §4.4 item 10, and
  `docs/02-technical/GAME_STATE.md` §2.3/§2.4 — the contract TASK-209 implemented;
  cite them, do not restate them

---

## Scope

### In Scope

1. **Record the commit step where process lives.** Add the commit/integration step
   and the granularity rule to the workflow layer — `tasks/TASK_LIFECYCLE.md` §3/§4
   (the transition and file-movement tables) and `.ai/workflow/core/completion.md`
   §1/§2 (the final-report shape gains the commit field) — and, if the Product Owner
   requires it, the corresponding line in `AGENTS.md` §22. The rule must state, in
   the repository's own voice, the policy TASK-223 §6.2 records as P-1…P-6:
   `master`-only; one commit per task slice only when the record's declared file set
   equals the changed set; group commits with an explicit member list when a file is
   shared; no commit under a task with no record; task ids in the commit body, never
   the subject; a commit must not contradict a record's declared file list.
2. **Author TASK-209's record.** Reconstruct it from the code, tests, migrations
   and the citing records: its scope (the `BattleStateUpdated` projection and the
   battle HUD), its declared file set, its acceptance criteria, and — only where an
   artifact proves it — its validation. Where verification evidence does not exist,
   say so explicitly rather than asserting a result. File it in
   `tasks/completed/` with a Status that reflects the evidence actually found.
3. **Disclose what cannot be attributed.** If TASK-209's work cannot be faithfully
   attributed to a reconstructed scope, record that disposition explicitly and apply
   the "unowned/pre-existing: `<paths>`" disclosure rule to those paths instead of
   inventing an owner.
4. **Execute TASK-223 §6.3's integration plan** — in its recorded order, with the
   group assignments its §6.3 identifies, after items 1–3 land. Report every commit
   hash and every task/file subset each commit owns.
5. **Verify the result** with `git log --oneline`, `git status`, `git diff --stat`
   and `git diff --check`, and confirm that no record's declared file list is
   contradicted by a commit.

### Out of Scope

- Rewriting, amending, rebasing, cherry-picking, force-pushing, tagging, or
  branching anything. The plan is additive-only.
- Any change to `docs/` contracts, any gameplay rule, any `src/` or `tests/` file,
  any migration, and any ADR — the integration commit stages files exactly as they
  are; it does not edit them.
- Pushing to `origin`. Nothing in the repository requires or forbids it; it is not
  part of this task.
- The documentation drift TASK-213 §11 assigned (TASK-217A/217B/217C), the
  duplicated TASK-211/TASK-213 ids (reserved TASK-225 per TASK-223 §10 F-3), and
  TASK-221/218/219/216/220 — all separate tasks.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State

```text
HEAD                       afe5b14 ("feat: implement battle system, game scenes,
                           runtime, and API integration")
Branch                     master only (no other branch, no tag, no worktree)
Modified tracked files     38
Untracked entries          15 (13 measured by TASK-223 + TASK-223's record +
                           this manifest)
Task owning a commit step  NONE, in any document at any layer
TASK-209 record            ABSENT — the file does not exist anywhere under tasks/
Task-id collisions         TASK-211 and TASK-213 are each declared by two tasks
Files with no recordable   16 (10 owned only by TASK-209, 6 shared with it)
  owner
```

---

## Acceptance Criteria

- [ ] The commit/integration step and the granularity rule exist in the workflow
      layer, in the owning document, with one owner per fact and no duplication
- [ ] A `tasks/completed/` record exists for TASK-209, or its disposition is
      explicitly recorded and every affected path is disclosed as unowned
- [ ] No commit was created before the TASK-213 §10 conflict was resolved in
      writing by the Product Owner
- [ ] Every integration commit's body names each task and its file subset, and no
      commit contradicts any record's declared file list
- [ ] No file is committed under a task id that has no record in
      `tasks/completed/`, unless the commit body discloses it as unowned
- [ ] No amend, rebase, cherry-pick, force-push, or history rewrite occurred
- [ ] `git status` after integration is explainable: every remaining entry is
      explicitly named
- [ ] No production code, test, contract, product rule, or ADR was modified by this
      task
- [ ] Quality review checklist passes (`quality/review.md` §1)

---

## Affected Files & Areas

```text
[ ] src/backend/ · src/frontend/client/       — NONE (staged by the commits, never
                                                edited by this task)
[ ] tests/                                     — NONE (same)
[ ] docs/                                      — NONE (unless the Product Owner
                                                places the commit rule in a docs/
                                                technical document; the current
                                                owner is the workflow layer)
[x] .ai/workflow/core/completion.md            — the commit step + report field
[x] tasks/TASK_LIFECYCLE.md                    — the commit transition + rule
[?] AGENTS.md §22                              — only if the Product Owner
                                                requires it at the contract level
[x] tasks/completed/TASK-209-<title>.md        — reconstructed (or its disposition
                                                recorded)
[x] tasks/completed/TASK-224-<this file>.md    — this record
```

---

## Implementation Notes

- The ownership authority is TASK-223 §2's table. Do not re-derive attribution from
  filenames or chronology; read the diff content as TASK-223 did.
- `SceneLifecycle.test.ts` carries three tasks' work (TASK-209 + TASK-210 +
  TASK-211) and `standalone-web-smoke.mjs` carries four (TASK-209 + TASK-210 +
  TASK-211 + TASK-212A-1). Under the granularity rule these must be group commits
  with an explicit member list, not per-task slices.
- TASK-208A's record declares exactly three documentation files and no other task
  touched them. Whether to commit that contract before its implementation
  (`.ai/workflow` order: documentation precedes code) or fold it into the
  implementation commit is a decision this task must record, not assume.
- `git diff --check` currently passes (exit 0); keep it passing.
- Do not stage `tasks/completed/TASK-223-commit-worktree-ownership.md` and this
  manifest into a commit that claims another task's ownership; they belong to
  TASK-223/TASK-224's own slice.

---

## Testing Requirements

### Required Verification

```text
[ ] Unit tests         — N/A (no executable file is edited by this task)
[ ] Integration tests  — N/A (same)
[ ] Gameplay scenarios — N/A (same)
[x] Content preservation — git diff --stat, --name-status, --check and
                           `git rev-parse HEAD` before/after each commit; the only
                           permitted difference is the set of paths the commit
                           itself names
[x] History integrity    — git log --oneline shows linear additive commits only;
                           no commit's parent set changed
```

No test suite need be run for the documentary half. If any commit is created, run
`git diff --cached --name-status` before committing and compare it to that commit's
declared file subset; a mismatch blocks the commit.

### Key Edge Cases

- A file claimed by two records (the 10 shared files in TASK-223 §2, 6 of which also
  involve TASK-209).
- A file whose only owner has no record (the 10 category-6 files).
- A record whose declared file list does not equal the changed set (TASK-212A-1 and
  TASK-211 each declare shared files they do not solely own).
- A commit created while the TASK-213 §10 conflict is unresolved.

---

## Stop Conditions

Universal stop conditions (`AGENTS.md` §20, `.ai/README.md` §13) always apply. Stop,
set the task BLOCKED, and report — do not guess — if any of these fires:

1. **The Product Owner has not resolved TASK-213 §10's self-contradiction**
   (TASK-223 §3 A-4: items (a)/(d) commission the commit/integration step, the
   closing list forbids "commit" and "stage" while naming TASK-223). Two
   authoritative readings disagree; `AGENTS.md` §4 requires a human decision. **No
   commit may be created while this holds.**
2. **TASK-209's scope cannot be reconstructed faithfully** from code, tests and the
   citing records, and the Product Owner has not chosen the "commit unowned and
   disclose" disposition. Do not author a record that asserts a scope or a
   verification result no artifact supports.
3. **A commit would have to contradict a record's declared file list** to be
   created. Report the divergence instead; the record is the authority.
4. **An integration step would require editing a file** to make the tree match a
   record. Editing is out of scope: report the mismatch.
5. **Any destructive or history-rewriting operation appears necessary** (reset,
   rebase, amend, filter, force-push). Stop: the plan is additive-only by design.
6. **A record or a file turns out to be missing that TASK-223 §1 did not measure**
   (e.g. a changed file not in TASK-223 §2, or a second missing task record).
   Report it; do not silently fold it into a group commit.
7. If this task exceeds the Simple skill budget (4) or must edit `docs/` contracts
   to proceed: **STOP & decompose**.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual. Report the ACTUAL observed result.
-->

### Changed Files
- `<file path>` — <summary of change>

### Commits Created
```text
<hash>  <subject>
        owns: <task id(s)>  ·  files: <exact paths>
        disclosed unowned: <paths or none>
```

### Validation Results
- `git diff --check` — PASS / FAIL
- `git diff --stat` (before/after each commit) — <observed>
- `git log --oneline` — <observed range>
- `git status` — <every remaining entry named>

### Ownership Verification
- [ ] Confirmed every commit's file subset matches its declared task(s)
- [ ] Confirmed no record's declared file list is contradicted by a commit
- [ ] Confirmed no file was committed under a task id with no record (or was
      disclosed as unowned)
- [ ] Confirmed zero amend / rebase / cherry-pick / force-push / history rewrite
- [ ] Confirmed no production code, test, contract, product rule, or ADR was edited
- [ ] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)

---

## Revision History

**Revision 1 — task created (BACKLOG) by TASK-223.** Created under TASK-223's
Option C ("If ownership cannot be established safely, document why and create a
follow-up task"), from TASK-223 §10 F-1. Status is BACKLOG rather than READY
because it is blocked on the Product Owner decision in Stop Condition 1. It owns
TASK-223 §10 F-1 (the commit step and TASK-209's record) and the execution of
TASK-223 §6.3's deferred integration plan. TASK-223 §10 F-3 (the duplicated
TASK-211/TASK-213 ids) is reserved as TASK-225 and is not part of this task unless
the Product Owner folds it in.
