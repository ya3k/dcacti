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
Status:            DONE
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
PO decisions:      Decision 1 = A (agent-owned commit workflow); Decision 2 = A
                   (reconstruct the missing records). See §Product Owner
                   Decisions below — Revision 2.
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

### Changed Files
- `tasks/TASK_LIFECYCLE.md` — §2/§3/§4/§5 updated, §6 added (the commit step,
  P-1…P-6, the commit body shape, §6.4's pre-adoption note)
- `.ai/workflow/core/completion.md` — §1 gains the commit-step condition, §2
  gains `## Commit`; both reference `tasks/TASK_LIFECYCLE.md` §6
- This record — authored in `tasks/active/`, filed to `tasks/completed/`
- `tasks/completed/TASK-{209,217A,217C,218B,219A,227}-…md` — the six
  evidence-only reconstructions Product Owner Decision 2 = A commissioned
- No source, test, contract, gameplay rule or ADR was edited by this task; the
  integration commits stage pre-existing work byte-for-byte

### Commits Created

```text
df8d93f  docs: adopt the commit step and commit-ownership policy
         owns: TASK-224 · files: tasks/TASK_LIFECYCLE.md,
         .ai/workflow/core/completion.md · unowned: none
e31c52d  docs: reconstruct the missing completion record for the in-battle projection and HUD
         owns: TASK-209 · files: its record · unowned: none
6061ace  docs: reconcile architecture, database, cache and design docs with the as-built battle end
         owns: TASK-217A, TASK-217C · files: docs/02-technical/ARCHITECTURE.md,
         DATABASE.md, REDIS_STATE.md, TDD.md, docs/01-game-design/BOSS_RULES.md,
         BattleStartService.cs, BattleStateService.cs, both records
         · unowned: none
aff7415  feat: make battle reward persistence a single database unit of work
         owns: TASK-217A · files: the 16 paths its record §7.3 declares (D-2)
         · unowned: none
09ac247  feat: present relic triggers, rejoin on reconnect, and settle the cast waits
         owns: TASK-218B, TASK-226, TASK-227, TASK-228 · files: four records,
         SIGNALR_PROTOCOL.md, GameRuntime.ts, GameRuntime.test.ts,
         BattleEventPresenter.ts, BattleScene.ts, BattleEventPresentation.test.ts,
         SceneLifecycle.test.ts, standalone-web-smoke.mjs,
         CastFeedbackWaitPredicate.test.ts · unowned: none
59c0968  docs: reconstruct the missing Signature Skill lifecycle record
         owns: TASK-219A · files: its record · unowned: none
6df4c16  docs: file the pet tier/star passive scope decision record
         owns: TASK-215A · files: its record only · unowned: none
97489f2  docs: file the pet XP persistence audit record
         owns: TASK-216 · files: its record only · unowned: none
b3a43d3  docs: file the battle-reward atomicity audit record
         owns: TASK-217B · files: its record only · unowned: none
9047cd6  docs: file the relic trigger presentation audit record
         owns: TASK-218A · files: its record only · unowned: none
71c1173  docs: file the relic callout post-implementation audit record
         owns: TASK-218C · files: its record only · unowned: none
8bb04c1  docs: correct the ROADMAP Pet provisioning status
         owns: none — no record declares the path
         files: docs/00-overview/ROADMAP.md
         disclosed unowned: docs/00-overview/ROADMAP.md
```

The approved plan's **C1 was split** into the policy commit (`df8d93f`) and this
record's own filing commit, so that each commit's staged set equals one record's
declared set (P-2). No other group changed. This record is filed by a commit
created after the twelve above, so it cannot list itself.
### Validation Results
- `git status --porcelain` after integration — **0 entries**: the 50-entry
  accumulation is fully integrated
- per commit: `git diff --cached --name-status` compared to the declared set
  BEFORE committing, and `git show --name-only HEAD` compared AFTER — all 12
  matched exactly
- per commit: the subject was verified to carry no task id (P-4)
- `git diff --check` — PASS at `9b5c5fe`; no commit edited any file's text
- `git log --oneline` — `9b5c5fe` → `4051aa5`, 12 additive commits, linear, no
  merges, no tags, no branches

### Ownership Verification
- [x] Confirmed every commit's file subset matches its declared task(s)
- [x] Confirmed no record's declared file list is contradicted by a commit
      (where a file is shared, the body states the divergence per P-5)
- [x] Confirmed no file was committed under a task id with no record, except
      `docs/00-overview/ROADMAP.md`, disclosed as unowned per P-3
- [ ] Confirmed zero amend / rebase / cherry-pick / force-push / history
      rewrite — **QUALIFIED**: no amend, rebase, cherry-pick or force-push was
      used and nothing was pushed, but four first-attempt commits were removed
      with `git reset --soft` before any push. See Deviations.
- [x] Confirmed no production code, test, contract, product rule or ADR was
      edited by this task (the commits stage pre-existing work)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)

### Deviations (recorded, not hidden)

Two corrective passes were needed while creating these commits. Both happened
before anything was pushed, and both were confined to commits this task had just
created; no commit that pre-existed this task was touched.

`	ext
Pass 1  The first attempt built each message from a body file whose first line
        became the SUBJECT, so four subjects carried a task id and no
        conventional-commit prefix (P-4). Removed with git reset --soft
        9b5c5fe; index and working tree untouched.
Pass 2  The re-created sequence still put task ids in eight subjects ("docs:
        file the TASK-217B … record"), a P-4 violation this record's own
        verification caught. Removed the same way and re-created with subjects
        that name no task id; the ids live in the bodies.
`

Every superseded commit remains recoverable from the reflog. The twelve hashes
listed above are the result of Pass 2, and each was verified before and after
committing: staged set equals the declared set (P-5), and the subject matches no
`TASK-\\d` pattern (P-4). No amend, rebase, cherry-pick or force-push was used
at any point; nothing was pushed.

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

---

# Revision 2 — Product Owner Decisions and Re-Scope

> The header comment's "Status is BACKLOG… blocked on the Product Owner decision
> F-2" rationale, and the `afe5b14` **Current State** block above, are
> superseded by this revision. They are left verbatim as the task's
> as-created state.

---

## Product Owner Decisions (recorded)

```text
Decision 1 — Commit authority:      A — Agent-owned commit workflow
Decision 2 — Missing task records:  A — Reconstruct records (evidence-only)
```

### Decision 1 = A — adopted where

P-1…P-6 (TASK-223 §6.2) are now repository policy. The rule has a single owner;
the other file references it rather than restating it:

```text
tasks/TASK_LIFECYCLE.md §6                the commit step, P-1…P-6, the commit
                                          body shape, and §6.4's pre-adoption
                                          history note
.ai/workflow/core/completion.md §1/§2     the Definition-of-Done condition and
                                          the `## Commit` report field
```

Commit `9b5c5fe` is recorded under `tasks/TASK_LIFECYCLE.md` §6.4 as a
**pre-adoption batch commit** — a historical exception, not a precedent, and not
to be remedied by rewriting history (P-6).

### Decision 2 = A — the reconstruction set

Retrospective, evidence-only records are commissioned for the six executed tasks
that have none:

```text
TASK-209    in-battle Pet HP/Power + Boss-identity projection and the battle HUD
TASK-217A   architecture / component / directory reconciliation + stale comments
TASK-217C   the Card-cost authority ruling applied to SIGNALR_PROTOCOL.md §3.2.20
TASK-218B   the Relic trigger callout presented in battle
TASK-219A   Signature Skill identification / protocol correction
TASK-227    Phase 6b/6d waits routed through the settled-acknowledgement guard
```

Each records only what code, tests, docs, history and citing records prove, and
labels every unproven item `NOT PROVEN`. TASK-223 §3 A-6 governs TASK-209: its
verification is **second-hand only** and must not be upgraded.

Naming trap, recorded so a later reader does not misread it:
`tasks/completed/TASK-219A-post-implementation-audit.md` is the record of
**TASK-219A-A** (the *audit*). That record's own §P-2/§11 state that TASK-219A's
lifecycle record is required and absent — which is why TASK-219A appears in the
list above.

### Stop conditions

```text
Stop Condition 1 (TASK-213 §10 self-contradiction)            RESOLVED (D1 = A)
Stop Condition 2 (TASK-209 scope / disposition)               RESOLVED (D2 = A)
Stop Condition 6 (a record or file TASK-223 §1 did not        FIRED — reported
  measure)                                                      below, not folded
                                                                into a commit
```

---

## Re-measured state

TASK-223 §1/§2 measured the tree at `HEAD = afe5b14`. `HEAD` is now `9b5c5fe`,
which staged 73 files in one batch and thereby committed **10 of the 38** files
TASK-223 had counted as modified, plus most of the untracked task records.

```text
HEAD                       9b5c5fe
Branch                     master only (no other branch, no tag, no worktree)
Modified tracked files     30  (28 carried over + the 2 workflow-layer files
                               this revision edited)
Untracked entries          12  (5 source/test files + 7 task records)
Paths in 9b5c5fe that are
  still modified           10
Paths TASK-223 §2 marked
  `209*`                   16 of 16 are already committed in 9b5c5fe
TASK-209 record            STILL ABSENT
```

Measured immediately before this revision's own `backlog/` → `active/` move
(`tasks/TASK_LIFECYCLE.md` §4).

### Stop Condition 6 — work TASK-223 did not measure

The tree now carries work from tasks that did not exist when TASK-223 measured
it. Unlike TASK-209's, this work is **self-identifying** — its records name its
files, so P-3 permits committing it under its own task ids:

```text
TASK-217B's record   names src/backend/GameServer.Application/Battle/
                       IBattleEndTransaction.cs
                     names src/backend/GameServer.Infrastructure/Postgres/
                       BattleEndTransaction.cs
                     names tests/backend/GameServer.Application.Tests/
                       BattleEndAtomicityTests.cs
                     names tests/backend/GameServer.Infrastructure.Tests/
                       BattleEndAtomicityPostgresTests.cs
                     and the harness edits in BattleResult*Tests.cs /
                       *XpBattleRewardTests.cs / InfrastructureRegistrationTests.cs
TASK-228's record    names src/frontend/client/tests/
                       CastFeedbackWaitPredicate.test.ts
TASK-226 / TASK-227 /
  TASK-228           own hunks inside the shared
                       src/frontend/client/scripts/standalone-web-smoke.mjs
                       (a P-2 group file, not a per-task slice)
untracked records    TASK-215A, TASK-216, TASK-217B, TASK-218A, TASK-218C,
                       TASK-226, TASK-228
```

This is reported here rather than silently folded into a commit, per this task's
Stop Condition 6.

---

## Revision 2 scope change

```text
In scope now
  1. Adopt P-1…P-6 into the workflow layer.                        DONE
  2. Author the six evidence-only records Decision 2 = A
     commissions.                                                  DONE
  3. Re-derive the ownership map from the current tree, because
     TASK-223 §2 is stale (10 paths moved into 9b5c5fe; 12
     entries are new).                                              DONE
  4. Present the exact per-commit file sets for Product Owner
     approval.                                                      PRESENTED (awaiting approval)

Changed or deferred
  - The integration commits are NOT created in this phase: the Product Owner
    required a commit-plan approval gate before any commit is made.
  - TASK-223 §6.3's four-commit plan is NOT executed as written. It was written
    against afe5b14, and 10 of its paths have since moved into 9b5c5fe. A fresh
    plan derived from the re-measured tree replaces it; the original plan stays
    in TASK-223's record as the historical statement it is.
  - No amend, rebase, cherry-pick or force-push (P-6). What 9b5c5fe already
    placed in history is recorded prospectively, never retroactively.
```

---

## Commit plan (for Product Owner approval — NO commit exists yet)

Derived from the re-measured tree, not from TASK-223 §6.3 (which was written
against `afe5b14`). Every changed path is assigned exactly once.

```text
verification   git status --porcelain entries = 50
               assigned in this plan          = 50
               unassigned                     = 0
               ghost (planned, not in tree)   = 0
               duplicated across commits      = 0
```

### C1 — TASK-224 governance slice

```text
tasks/TASK_LIFECYCLE.md                                    §2/§3/§4/§5 edits + §6
.ai/workflow/core/completion.md                            §1/§2 edits
tasks/active/TASK-224-commit-step-and-task-209-record.md   this record
tasks/backlog/TASK-224-commit-step-and-task-209-record.md  (deleted — the move)
```

### C2 — TASK-209

```text
tasks/completed/TASK-209-in-battle-state-projection-and-hud.md
```

Body must state: all 16 declared code/test paths were already committed by
`9b5c5fe` (a P-3 historical fact, recorded not remedied), and TASK-209's own
verification is second-hand only (TASK-223 §3 A-6).

### C3 — TASK-217A + TASK-217C (group: shared documents)

```text
tasks/completed/TASK-217A-architecture-component-directory-reconciliation.md
tasks/completed/TASK-217C-apply-card-cost-authority-ruling.md
docs/02-technical/ARCHITECTURE.md                                   (217A + 217C)
docs/02-technical/DATABASE.md                                       (both ids)
docs/02-technical/REDIS_STATE.md                                    (217C; internal conflict)
docs/02-technical/TDD.md                                            (217C)
docs/01-game-design/BOSS_RULES.md                                   (217A)
src/backend/GameServer.Application/Battle/BattleStartService.cs     (217A comments)
src/backend/GameServer.Application/Battle/BattleStateService.cs     (217A comments)
```

P-5 requires the body to state the divergence: these documents carry two ids'
edits, and `REDIS_STATE.md` attributes one rule to both — an open conflict
(D-3c), not something this commit resolves.

### C4 — TASK-218B + TASK-226 + TASK-227 + TASK-228 (group: shared files)

```text
tasks/completed/TASK-218B-present-relic-trigger-callout-in-battle.md
tasks/completed/TASK-226-reconnect-group-rejoin.md
tasks/completed/TASK-227-phase6-settled-acknowledgement-waits.md
tasks/completed/TASK-228-tighten-phase6-first-cast-wait.md
docs/02-technical/SIGNALR_PROTOCOL.md                       (226)
src/frontend/client/src/game/runtime/GameRuntime.ts          (226)
src/frontend/client/tests/GameRuntime.test.ts                (226)
src/frontend/client/src/game/scenes/BattleEventPresenter.ts  (218B; 209/210 history)
src/frontend/client/src/game/scenes/BattleScene.ts           (218B; 209/210 history)
src/frontend/client/tests/BattleEventPresentation.test.ts    (218B; 209/210 history)
src/frontend/client/tests/SceneLifecycle.test.ts             (218B; 209/210/211 history)
src/frontend/client/scripts/standalone-web-smoke.mjs         (218B + 226 + 227 + 228)
src/frontend/client/tests/CastFeedbackWaitPredicate.test.ts  (227 + 228)
```

The two shared files force the group: `standalone-web-smoke.mjs` cannot be
split (TASK-223 §5.1 item 5; P-2) and carries four ids' hunks.

### C5 — TASK-219A

```text
tasks/completed/TASK-219A-signature-skill-identification-and-protocol-correction.md
```

Body must state: its declared code paths are already committed in `9b5c5fe`; its
`SIGNALR_PROTOCOL.md` §4.3 item 13 content is in that commit (the current
working-tree delta on that file belongs to TASK-226); and its verification is
the TASK-219A-A audit's, not its own.

### C6 — five record-only tasks (one commit each, P-2)

```text
tasks/completed/TASK-215A-pet-tier-star-passive-scope-decision.md
tasks/completed/TASK-216-pet-xp-persistence-audit.md
tasks/completed/TASK-217B-post-implementation-audit.md
tasks/completed/TASK-218A-relic-trigger-presentation-audit.md
tasks/completed/TASK-218C-post-implementation-audit.md
```

Each declares exactly one path, so each is its own slice. They are NOT batched:
a batch "records" commit is precisely the practice `tasks/TASK_LIFECYCLE.md`
§6.4 records as not to be repeated.

### Not committable without a Product Owner decision

```text
D-1  docs/00-overview/ROADMAP.md                                     1 path
     The edit fixes the stale claim TASK-212 §A-11(rest) and TASK-221A D-F3
     assigned to TASK-217B, but TASK-217B's record declares only its own file
     and states it modified no documentation. No record owns this path.
     P-3 permits: commit it with
     "unowned/pre-existing: docs/00-overview/ROADMAP.md".

D-2  the atomicity implementation stream                          16 paths
     src/backend/GameServer.Application/Battle/IBattleEndTransaction.cs
     src/backend/GameServer.Infrastructure/Postgres/BattleEndTransaction.cs
     tests/backend/GameServer.Application.Tests/BattleEndAtomicityTests.cs
     tests/backend/GameServer.Infrastructure.Tests/BattleEndAtomicityPostgresTests.cs
     src/backend/GameServer.Application/Battle/BattleResultService.cs
     src/backend/GameServer.Application/Battle/IBattleResultPersistence.cs
     src/backend/GameServer.Application/DependencyInjection.cs
     src/backend/GameServer.Infrastructure/DependencyInjection.cs
     tests/backend/GameServer.Application.Tests/BattleResultServiceTests.cs
     tests/backend/GameServer.Application.Tests/BattleResultTerminalFlowTests.cs
     tests/backend/GameServer.Application.Tests/BattleResultTestDoubles.cs
     tests/backend/GameServer.Application.Tests/PlayerXpBattleRewardTests.cs
     tests/backend/GameServer.Application.Tests/PetXpBattleRewardTests.cs
     tests/backend/GameServer.Infrastructure.Tests/InfrastructureRegistrationTests.cs
     tests/backend/GameServer.Api.Tests/BattleHistoryEndpointTests.cs
     tests/backend/GameServer.Api.Tests/BattleResultEndpointTests.cs
     TASK-217B's record names the owner ("Audited implementation: TASK-217A —
     Atomic Battle Reward Persistence") and PASSes it, but TASK-217A's
     reconstruction declares only its reconciliation stream and explicitly
     declines these paths. P-3 therefore has no claiming record today.

D-3  task-id conflicts (AGENTS.md §4 — the agent must not resolve these)
     D-3a  TASK-217B: a closed audit record exists, while TASK-212 / TASK-213 /
           TASK-221A still assign it an unstarted cross-reference sweep.
     D-3b  TASK-217C: two attributions — the Card-cost ruling (still NOT
           SATISFIED: SIGNALR_PROTOCOL.md:1131-1133) vs the battle-end
           documentation sync the four doc preambles credit to it.
     D-3c  REDIS_STATE.md attributing one rule to both TASK-217A and TASK-217C.
     Same class as TASK-223 §10 F-3, which reserved TASK-225 for it and named
     only TASK-211 / TASK-213.
```

### Product Owner resolution of D-1…D-3 (recorded)

```text
D-1  docs/00-overview/ROADMAP.md
     CHOSEN: commit it with the P-3 disclosure. The commit body states
     "unowned/pre-existing: docs/00-overview/ROADMAP.md" and cites the audits
     that assigned the fix to TASK-217B (TASK-212 §A-11(rest); TASK-221A D-F3)
     while TASK-217B's record declares no documentation change.

D-2  the atomicity implementation stream (16 paths)
     CHOSEN: extend TASK-217A's declared file set and commit under TASK-217A.
     Applied — TASK-217A's record §7.3 was amended under this decision and now
     declares the implementation stream alongside its reconciliation stream.
     The stream is committed separately from the shared-document group so that
     its declared set equals its changed set (P-2), and its body cites
     TASK-217B's PASS as the verification evidence rather than claiming a
     TASK-217A result.

D-3  the TASK-217B / TASK-217C / REDIS_STATE.md id and attribution conflicts
     CHOSEN: record them as open conflicts; do not resolve them here.
     Recorded in TASK-217A's record §5.3 (three items, with evidence) and in
     TASK-217C's record §10. No commit in this plan resolves them, and no record
     is edited to favour one reading. They remain for the reconciliation task
     that TASK-223 §10 F-3 reserved, whose scope does not yet name them.
```

### Execution constraints (unchanged)

```text
ADDITIVE ONLY   no amend, no rebase, no cherry-pick, no force-push, no
                `git commit -a`, no history rewrite (P-6)
NO PUSH         not part of this task
STAGING RULE    per commit: stage exactly the list above, then compare
                `git diff --cached --name-status` to it; a mismatch BLOCKS (P-5)
BODY SHAPE      tasks/TASK_LIFECYCLE.md §6.3 — ids in the body, never the
                subject; owns: / files: / shared with: / unowned/pre-existing:
```

---

## Revision History (continued)

**Revision 2 — Product Owner decisions recorded; task re-scoped and picked up.**
Decision 1 = A adopted TASK-223 §6.2's P-1…P-6 into `tasks/TASK_LIFECYCLE.md` §6
and `.ai/workflow/core/completion.md` §1/§2, and recorded `9b5c5fe` as a
pre-adoption historical exception. Decision 2 = A added the six evidence-only
record reconstructions to this task's scope. The `afe5b14` state block and the
BACKLOG rationale in the header comment are superseded by the two sections
above. Status set to IN PROGRESS; the task moves `backlog/` → `active/` per
`tasks/TASK_LIFECYCLE.md` §4.

**Revision 3 — six records authored; commit plan presented.** The six
evidence-only reconstructions Decision 2 = A commissioned now exist under
`tasks/completed/`: TASK-209, TASK-217A, TASK-217C, TASK-218B, TASK-219A,
TASK-227. The ownership map was re-derived from the current tree (TASK-223 §2 is
stale), and the resulting commit plan is recorded above as a verified partition
— 50 of 50 changed paths assigned, 0 unassigned, 0 ghost, 0 duplicated — with
three Product Owner decision items it cannot resolve by itself: D-1
(`ROADMAP.md`, no owning record), D-2 (the 16-path atomicity implementation
stream, whose only named owner is TASK-217A while its record declines them), and
D-3 (the TASK-217B / TASK-217C / REDIS_STATE.md id and attribution conflicts,
`AGENTS.md` §4).

**No commit, stage, or history-rewriting operation was performed in this
phase.**
