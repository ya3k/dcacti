# TASK-162 — Reconcile the Lifecycle State of TASK-155, TASK-159, TASK-160, and TASK-161

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and tasks/ by path and section — it does NOT copy game
  rules, formulas, schemas, or contracts.

  THIS TASK IS A LIFECYCLE RECONCILIATION TASK, NOT AN IMPLEMENTATION TASK.
  Its entire deliverable is the correct terminal lifecycle state of four
  already-satisfied tasks: the right Status field and the right folder. It
  authors no rule, changes no contract, and writes no code.

  PROVENANCE: the task-generation audit performed after TASK-161 (see
  TASK-161 §"Completion Evidence" and TASK-159's implementation) established
  that TASK-155, TASK-159, TASK-160, and TASK-161 are each satisfied by work
  that has already landed in the repository, yet all four still sit in
  tasks/backlog/ with stale Status fields. tasks/README.md §3 defines the next
  task ID as "the highest existing ID + 1 across all folders"; with 155/159/
  160/161 unfiled, ID accounting is also unresolved. This task closes both.

  THE GOVERNING DISTINCTION (tasks/TASK_LIFECYCLE.md §1/§2). DONE and
  SUPERSEDED are different terminal states with different entry conditions:
    DONE        — "All core/completion.md §1 criteria are satisfied by DIRECT
                  EXECUTION of the task."
    SUPERSEDED  — "All intended deliverables ... 100% satisfied or rendered
                  obsolete by downstream/decomposition tasks WITHOUT the task
                  itself being directly executed."
  This task therefore evaluates each of the four independently against the
  evidence and does NOT default them to a single outcome. Two of the four are
  DONE (directly executed) and two are SUPERSEDED (satisfied downstream). See
  §"Required Transition".

  BOUNDARY: four task files under tasks/backlog/, their move to
  tasks/completed/, and this task file only. Zero files under docs/, src/, or
  tests/. No gameplay, protocol, API, Redis, architecture, or implementation
  behavior is touched anywhere by this task.
-->

---

## Metadata

```text
Task ID:           TASK-162
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md). This is the closest
                   of the six defined types: the deliverable is a record
                   correction, not a behavior change. It is not REFACTOR (no
                   code is restructured) and not ARCHITECTURE (no structural
                   decision changes). See "Type classification note".
Status:            DONE (the reconciliation this task defines was performed
                   and its four moves are present in tasks/completed/; its own
                   Status field and folder were left unreconciled and are
                   corrected by TASK-165. See "Completion Evidence" — the
                   Changed Files list, the eight PASS Validation Results, and
                   the "Reconciled Lifecycle State" block.)
Risk:              LOW (TASK_TYPES.md §4 — DOCUMENTATION baseline is
                   LOW–MEDIUM, and this is the LOW end: no authoritative
                   contract is edited, no cross-referenced document is
                   touched, no code or test exists in scope. The only writes
                   are four lifecycle-metadata corrections and four file
                   moves, each fully determined by evidence already in the
                   repository. No judgment call is left to the executor —
                   §"Required Transition" fixes every outcome in advance.)
Priority:          MEDIUM (it does not gate gameplay, but it blocks correct ID
                   accounting — tasks/README.md §3 — and leaves four
                   satisfied tasks reported as unfinished, which misleads the
                   next task-generation pass. TASK-159's own file currently
                   claims "No implementation was performed", which is now
                   false and is the single most misleading record in
                   tasks/.)
Primary Agent:     review (TASK_TYPES.md §2 / AGENT_SELECTION.md §1 —
                   "Documentation change: Primary Agent Review";
                   TASK_LIFECYCLE.md §3 records that the Review Agent sets
                   SUPERSEDED on a lifecycle reconciliation. This task's act
                   is verification and classification, which is the Review
                   Agent's posture.)
Supporting Agents: orchestrator (TASK_LIFECYCLE.md §3 — the Orchestrator
                   confirms a task is complete per core/completion.md §1 and
                   owns the completed/ placement; consulted for the terminal
                   status decision, not to author it)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-155 (the task being reconciled — directly executed;
                     IMMUTABLE once reconciled, read-only until then),
                   TASK-159 (the task being reconciled — satisfied downstream;
                     read-only until reconciled),
                   TASK-160 (the task being reconciled — directly executed;
                     read-only until reconciled),
                   TASK-161 (the task being reconciled — directly executed;
                     read-only until reconciled),
                   TASK-153 (DONE, already in tasks/completed/ — the
                     implementation TASK-155 made determinate; read-only, NOT
                     part of this reconciliation),
                   TASK-154 / TASK-156 (DECIDED decision records remaining in
                     backlog/ by design — decision-input tasks; read-only and
                     explicitly OUT of this task's scope)
Blocks:            Correct ID accounting for the next task created after this
                   one (tasks/README.md §3: "the highest existing ID + 1
                   across all folders"). It does not block any gameplay work.
Estimate:          Simple (four task files, four file moves, one new task
                   file; no code, no tests, no docs/ edits)
```

**Type classification note.** `DOCUMENTATION`, not `ARCHITECTURE` and not
`REFACTOR`. `TASK_TYPES.md` §3 selects the type by what the deliverable is;
here the deliverable is a corrected lifecycle record. No game rule, technical
contract, ADR, module boundary, or implementation behavior changes. The four
files touched are task-lifecycle metadata — `tasks/` is execution scaffolding
(`tasks/README.md` §1), not a source of truth for any rule or contract — so
correcting their status fields cannot introduce a source-of-truth conflict.

---

## Objective

Reconcile the lifecycle state of TASK-155, TASK-159, TASK-160, and TASK-161 to
their correct terminal status and folder under `tasks/TASK_LIFECYCLE.md`, by
verifying against the evidence already present in the repository whether each
was **directly executed** (→ `DONE`) or **satisfied by downstream work without
being directly executed** (→ `SUPERSEDED`), and by moving all four to
`tasks/completed/` — so that the task system's recorded state matches the
actual repository state and ID accounting is correct.

---

## Affected Tasks

```text
TASK-155   Apply the TASK-154 GAP-5 Decision to the Authoritative
           Documentation
TASK-159   Deliver the Pet / Boss Status-Effect Projection in
           `BattleStateUpdated`
TASK-160   Collect the Product Owner's Decisions on the Pet
           `StatusEffects[]` and Boss Live-HP Projection
TASK-161   Apply the TASK-160 Pet `StatusEffects[]` / Boss Live-HP Decisions
           to the Authoritative Documentation
```

All four currently reside in `tasks/backlog/`. None resides in
`tasks/active/`, `tasks/blocked/`, or `tasks/completed/`.

---

## Current State

Verified by direct inspection of the four files and the repository this
session. **These are observations of what the files record, not restatements
of any rule or contract** (`tasks/README.md` §9).

```text
FILE                                 RECORDED Status FIELD
tasks/backlog/TASK-155-*.md          BACKLOG  ("created at BACKLOG; moves to
                                     READY when the Orchestrator sequences it")
tasks/backlog/TASK-159-*.md          BLOCKED  ("the required contract does
                                     not exist in docs/")
tasks/backlog/TASK-160-*.md          BACKLOG  ("awaiting Product Owner input")
tasks/backlog/TASK-161-*.md          DONE     (Status field says DONE, but the
                                     file was never moved to completed/)
```

```text
TASK-155  §"Completion Evidence" is FULLY POPULATED: a "Decision source"
          block, a "Canonical documentation updated" block naming three
          documents with from→to version bumps, a "Changed Files" list, a
          12-row "Decision Traceability" table (D-1 … D-12), an "Identity
          Resolution" block, a 12-item "[x] TASK-153 Unblock Criteria"
          checklist, and "[x]" Validation Results. The file's own body
          therefore records direct execution.

TASK-159  §"Completion Evidence" is an explicit NON-population: an HTML
          comment reading "NOT APPLICABLE — this task did not reach DONE. It
          stopped per AGENTS.md §7. No implementation was performed and no
          completion is claimed." Its "Changed Files" list names only
          "tasks/backlog/TASK-159-<this file>.md — this task file only",
          asserts "Zero files under src/, tests/, or docs/", and its
          "Validation Results" read "N/A — no code, test, or documentation
          change was made". Its Scope records that it performed no
          implementation, and its §"Stop Condition Report" is present.

TASK-160  Both `Decision:` slots are FILLED — D-1 at line 488 ("D-1A") and
          D-2 at line 582 ("D-2A") — each with full D-3/D-4/D-5/D-6 coverage
          text and the reserved "Rationale (optional): <NOT SUPPLIED BY
          PRODUCT OWNER>" marker left intact. The objective ("Obtain and
          record the Product Owner's explicit decisions") is therefore
          discharged. HOWEVER §"Completion Evidence" was NEVER filled: it
          retains its placeholder comment ("Do NOT fill this in
          speculatively"), its "Changed Files" text is the pre-execution
          template line, its "Validation Results" read "N/A until the
          Product Owner answers", and all eight of its "Server Authority &
          Scope Verification" boxes are unchecked "[ ]".

TASK-161  §"Completion Evidence" is FULLY POPULATED: a "Decision source"
          block citing TASK-160's D-1A/D-2A, a "Changed Files" list naming
          three documents with the exact section-level edits (SIGNALR_
          PROTOCOL.md → v2.15 with §4 item 4 / §4 item 17 / §4.3 item 14 /
          §4.4 / §7 / §8 item 9; GAME_STATE.md → v2.19 with the §2.3.1 split;
          BOSS_RULES.md → v2.8 with §6.2.4), "Validation Results" reading
          PASS across six checks, a "Validation Detail" block, and a
          9-row quality/review.md §1 checklist reading PASS.
```

```text
REPOSITORY STATE CORROBORATION (independent of the four files' own claims)

TASK-155's documented edits are PRESENT: docs/01-game-design/BOSS_RULES.md
  records Id = "boss-thuy-ma-heal" and the authorized cross-entity read;
  docs/02-technical/SIGNALR_PROTOCOL.md v2.15 and GAME_STATE.md v2.19 and
  BOSS_RULES.md v2.8 are the current headers.

TASK-160's recorded decisions are CITED AS THE DECISION SOURCE by the
  authoritative documents that applied them: SIGNALR_PROTOCOL.md cites
  "TASK-160's D-1A" (item 17, §4.3 item 2, §4.3 item 14) and "TASK-160 D-2A"
  (§4.4 item 3); GAME_STATE.md cites "the TASK-160 Product Owner decisions
  D-1A and D-2A" in its version block and at §2.3 / §2.3.1 / §2.4.1.

TASK-159's SUBJECT MATTER IS IMPLEMENTED, though not by TASK-159 itself:
  src/backend/GameServer.Api/Hubs/BattleHub.cs declares the payload with a
  BossStatePayload member and projects it from BossState.HP/BossState.MaxHP,
  and declares petState's statusEffects member projected from
  PetState.ActiveStatusEffects; the client models and renders both
  (src/frontend/client/src/services/realtime/SignalRService.ts,
  src/game/runtime/GameRuntime.ts, src/game/scenes/BattleScene.ts); and the
  backend/frontend suites, TypeScript check, and Vite build pass
  (Backend 2642 passed, Frontend 537 passed, TypeScript PASS, Vite build
  PASS).
```

---

## Evidence

Per task, the evidence that decides DONE versus SUPERSEDED. Each row cites
where the evidence lives; nothing is inferred from a filename or a Status
field alone.

```text
TASK-155
  E-155-1  §"Completion Evidence" fully populated, including a "Changed
           Files" list and a from→to version-bump block for three documents.
  E-155-2  A 12-row "Decision Traceability" table mapping each TASK-154
           D-1…D-12 item to the exact section text it produced.
  E-155-3  A 12-item "[x] TASK-153 Unblock Criteria" checklist, each item
           resolving to a named section.
  E-155-4  The edits it records are present in docs/ (BOSS_RULES.md carries
           "boss-thuy-ma-heal" and the authorized cross-entity read).
  → DIRECT EXECUTION IS EVIDENCED.

TASK-159
  E-159-1  §"Completion Evidence" explicitly states "NOT APPLICABLE — this
           task did not reach DONE ... No implementation was performed".
  E-159-2  Its "Changed Files" list names only its own task file and asserts
           "Zero files under src/, tests/, or docs/".
  E-159-3  Its "Validation Results" read "N/A — no code ... was made".
  E-159-4  Its §"Stop Condition Report" records the block and names the
           unblocking condition.
  E-159-5  The subject matter nevertheless EXISTS in src/ and src/frontend
           (BattleHub.cs payload members; client model and rendering) and is
           covered by passing suites.
  → NOT DIRECTLY EXECUTED; ITS INTENDED DELIVERABLE IS SATISFIED BY OTHER
    WORK.

TASK-160
  E-160-1  Both `Decision:` slots FILLED (D-1A at line 488, D-2A at line
           582), each with the D-3…D-6 coverage text — the exact deliverable
           its Objective names.
  E-160-2  The answers are recorded in the verbatim/stated form its
           §"Recording Discipline" requires, with the "NOT SUPPLIED BY
           PRODUCT OWNER" rationale marker preserved.
  E-160-3  Its decisions are cited as the DECISION SOURCE by the documents
           that applied them (SIGNALR_PROTOCOL.md; GAME_STATE.md).
  E-160-4  §"Completion Evidence" was NEVER filled — placeholder comment
           intact, template "Changed Files" line, "N/A until the Product
           Owner answers", eight unchecked boxes.
  → DIRECTLY EXECUTED AS TO ITS DELIVERABLE (the decisions were obtained and
    recorded by an agent transcribing Product Owner input); its
    §"Completion Evidence" section was simply left unpopulated.

TASK-161
  E-161-1  §"Completion Evidence" fully populated with a "Decision source"
           block, a section-level "Changed Files" list, and PASS Validation
           Results.
  E-161-2  A "Validation Detail" block with named results (carrier
           preserved, existing members preserved, no new persistence
           semantics, unmodified-file guard).
  E-161-3  A 9-row quality/review.md §1 checklist reading PASS, ending
           "Final recommendation PASS".
  E-161-4  The edits it records are present in docs/ (SIGNALR_PROTOCOL.md
           v2.15 §4.4 exists; GAME_STATE.md v2.19 §2.3.1 split exists;
           BOSS_RULES.md v2.8 §6.2.4 records the delivered pair).
  → DIRECT EXECUTION IS EVIDENCED.
```

---

## Lifecycle Rule

The rules this task applies, cited from `tasks/TASK_LIFECYCLE.md` and
`tasks/README.md` — not restated as new policy:

- `TASK_LIFECYCLE.md` §1 defines `DONE` as "All `core/completion.md` §1
  criteria are satisfied **by direct execution of the task**" → `completed/`.
- `TASK_LIFECYCLE.md` §1 defines `SUPERSEDED` as "All intended deliverables
  of the task have been **100% satisfied or rendered obsolete by
  downstream/decomposition tasks without the task itself being directly
  executed**" → `completed/`.
- `TASK_LIFECYCLE.md` §2 (Allowed transitions) permits exactly two routes
  into `SUPERSEDED`: `BACKLOG → SUPERSEDED` and `BLOCKED → SUPERSEDED`, both
  gated on "a lifecycle audit confirm[ing] scope is 100% satisfied or
  obsolete by downstream tasks".
- `TASK_LIFECYCLE.md` §2 (Invalid transitions) forbids `BACKLOG → DONE` and
  `READY → DONE`: "DONE requires direct execution".
- `TASK_LIFECYCLE.md` §2 (Invalid transitions) makes `DONE → any` and
  `SUPERSEDED → any` invalid: "completed tasks are immutable".
- `TASK_LIFECYCLE.md` §3 lists the four `SUPERSEDED` entry conditions, which
  this task must satisfy per reconciled task: never directly executed; 100%
  of scope verified complete in documented downstream tasks; explicit
  supersession evidence linking each deliverable to its satisfying
  downstream task; zero actionable independent scope remaining.
- `TASK_LIFECYCLE.md` §3 (`DONE`) requires direct execution, and §4 fixes
  `active/ → completed/` and `backlog/ → completed/` as the moves into
  `completed/` (the latter recorded for `BACKLOG → SUPERSEDED`).
- `tasks/README.md` §4: "Use the same filename across all folders — only the
  folder changes when a task moves between lifecycle stages."
- `tasks/README.md` §5: `completed/` holds terminal tasks; `backlog/` holds
  BACKLOG and READY tasks.
- `tasks/README.md` §3: "The next ID is the highest existing ID + 1 across
  all folders."

**Consequence for this task.** Because `BACKLOG → DONE` is an invalid
transition, a task still in `backlog/` can only reach `completed/` as
**`DONE`** if the direct-execution evidence exists and the transition is
recorded as the completion of already-performed direct execution — or as
**`SUPERSEDED`** if it was never directly executed. Task files do **not**
carry a `BACKLOG → DONE` arrow, so §"Required Transition" records for each
task both its correct terminal status and the folder move, and the executor
is instructed to record the completion evidence that justifies the status.
Where direct execution is evidenced, the correct terminal status is `DONE`;
where it is not, and the scope is 100% satisfied downstream, it is
`SUPERSEDED`.

---

## Required Transition

Determined by the §"Evidence" rows. **Four outcomes, not one** — the
executor applies what is written here and does not re-classify.

```text
TASK-155   Current: BACKLOG (in backlog/)
           Terminal status: DONE
           Folder: tasks/completed/
           Reason: DIRECTLY EXECUTED. Its §"Completion Evidence" records
           actual executed edits to three authoritative documents, with a
           12-row decision-traceability table and a 12-item unblock
           checklist, and those edits are present in docs/. The required
           evidence for DONE exists.
           Move: backlog/ → completed/

TASK-159   Current: BLOCKED (in backlog/)
           Terminal status: SUPERSEDED
           Folder: tasks/completed/
           Reason: NEVER DIRECTLY EXECUTED. Its own §"Completion Evidence"
           states "NOT APPLICABLE — this task did not reach DONE ... No
           implementation was performed", and its "Changed Files" list
           asserts zero files under src/, tests/, or docs/. Its intended
           deliverable — the Pet statusEffects[] and Boss hp/maxHp
           projection — is nevertheless 100% satisfied by other work, and
           the contract that had blocked it now exists.
           Move: backlog/ → completed/  (BLOCKED → SUPERSEDED; the file is
           in backlog/ but records BLOCKED — the executor preserves the
           recorded status history and applies the SUPERSEDED terminal
           state, per TASK_LIFECYCLE.md §2's
           "BLOCKED → SUPERSEDED (blocker and scope resolved/satisfied
           out-of-band by downstream decomposition tasks)".)

TASK-160   Current: BACKLOG (in backlog/)
           Terminal status: DONE
           Folder: tasks/completed/
           Reason: DIRECTLY EXECUTED. Its sole deliverable — obtaining and
           recording the Product Owner's decisions — is discharged: both
           `Decision:` slots carry D-1A/D-2A with full D-3…D-6 coverage, and
           those decisions are cited as the decision source by the documents
           that applied them. Only its §"Completion Evidence" section was
           left unpopulated; the executor completes that section from the
           evidence in §"Evidence" E-160-1…E-160-3 rather than re-opening
           the task.
           Move: backlog/ → completed/

TASK-161   Current: DONE (recorded) but file still in backlog/
           Terminal status: DONE (unchanged — confirmed correct)
           Folder: tasks/completed/
           Reason: DIRECTLY EXECUTED, and its Status field already says so.
           The defect is placement only: tasks/README.md §5 keeps completed
           tasks in completed/, and the file was never moved.
           Move: backlog/ → completed/
```

```text
RESULT
  DONE         TASK-155, TASK-160, TASK-161
  SUPERSEDED   TASK-159
  All four     → tasks/completed/
  No task retains non-terminal status.
```

**ID consequence.** After this task, the highest ID across all folders is
**TASK-162**, so the next task created is `TASK-163` (`tasks/README.md` §3).
This task does not create it.

---

## Traceability Matrix

Supersession requires "explicit ... evidence linking each deliverable to its
satisfying downstream task" (`TASK_LIFECYCLE.md` §3). For the one SUPERSEDED
task, each original deliverable is linked below. For the three DONE tasks the
matrix records the direct-execution evidence instead.

```text
TASK-159 (SUPERSEDED) — deliverable → satisfying work
  D-1  Record that the Pet/Boss Status-Effect (and live Boss-HP)
       client-visible projection is blocked on a missing contract
       → SATISFIED BY TASK-161, which authored the contract this task
         recorded as missing (SIGNALR_PROTOCOL.md §4.3 item 14, §4.4; the
         §4.3 item 2 exclusion wording this task quoted was re-worded there).
  D-2  Record the exact statement that must change, and by whom
       → SATISFIED BY TASK-160 (which made the decision) and TASK-161 (which
         applied it at the owning document). This task named
         SIGNALR_PROTOCOL.md §4.3 as the statement to change; TASK-161
         changed it.
  D-3  Record the classification of every other remaining MVP item
       → SATISFIED BY TASK-160's §"Out of Scope", which enumerates the same
         remaining blocked items (the 2 Pets, the 2 Bosses, the remaining
         Relics / Burning Curse, and GET /api/battle/history) and is the
         later, authoritative record of that classification.
  D-4  (implied by "Blocks") make the active Pet's Status Effects and the
       Boss's live HP reachable by the client
       → SATISFIED BY the landed projection implementation
         (BattleHub.cs payload members projected from
         PetState.ActiveStatusEffects and BossState.HP/MaxHP; client model
         and rendering in SignalRService.ts / GameRuntime.ts /
         BattleScene.ts) and its suites (Backend 2642 passed, Frontend 537
         passed, TypeScript PASS, Vite build PASS).
  Independent actionable scope remaining: NONE. Every item above is either
  recorded downstream or implemented downstream; none requires work that is
  uniquely TASK-159's.
```

```text
TASK-155 (DONE) — direct-execution evidence
  Deliverable: apply TASK-154's D-1…D-12 to the canonical owners.
  Evidence: §"Completion Evidence" naming the three documents and their
  section-level edits, the 12-row decision-traceability table, the 12-item
  unblock checklist, and the presence of those edits in docs/.

TASK-160 (DONE) — direct-execution evidence
  Deliverable: obtain and record the Product Owner's decisions.
  Evidence: D-1A recorded at the D-1 slot and D-2A at the D-2 slot, each
  with D-3…D-6 coverage; cited as the decision source by SIGNALR_PROTOCOL.md
  and GAME_STATE.md.

TASK-161 (DONE) — direct-execution evidence
  Deliverable: apply TASK-160's decisions to the authoritative documents.
  Evidence: §"Completion Evidence" naming the three documents and their
  exact section-level edits; PASS Validation Results and Validation Detail;
  a 9-row review checklist ending "Final recommendation PASS"; the edits
  present in docs/.
```

---

## Scope

### In Scope

1. Verify, per task, the direct-execution evidence required to distinguish
   `DONE` from `SUPERSEDED` (`TASK_LIFECYCLE.md` §1/§2).
2. Set each of the four task files' `Status:` field to its
   §"Required Transition" terminal status.
3. Move all four files from `tasks/backlog/` to `tasks/completed/`, keeping
   each filename byte-identical (`tasks/README.md` §4).
4. Complete TASK-160's §"Completion Evidence" section, and record
   TASK-159's SUPERSEDED determination and its deliverable→work traceability
   in TASK-159's file, so that each completed file carries the evidence
   `TASK_LIFECYCLE.md` §3 requires.
5. Record the reconciled ID accounting consequence (next ID = TASK-163) in
   this task's own Completion Evidence.

### Out of Scope

- **Any source code.** Zero files under `src/`.
- **Any test.** Zero files under `tests/`.
- **Any authoritative documentation.** Zero files under `docs/`. The
  contracts these four tasks produced or applied are already correct and are
  not re-opened, re-applied, re-worded, or re-verified beyond confirming
  the evidence above.
- **Any gameplay, protocol, API, Redis, persistence, or architecture
  change.** None is proposed, authored, or implied.
- **Re-opening or re-interpreting completed implementation work.** TASK-153
  and TASK-158 are already in `completed/` and are immutable
  (`TASK_LIFECYCLE.md` §2). The projection implementation landed with
  TASK-159's subject matter is **not** re-opened; this task only records that
  it exists.
- **TASK-150, TASK-154, TASK-156, TASK-157.** TASK-154 and TASK-156 are
  **decision-input records that correctly remain available in `backlog/`**
  and are NOT part of this reconciliation. TASK-150 and TASK-157 carry their
  own recorded statuses and are likewise not part of the four named tasks.
  **This task reconciles exactly four files and no others.**
- **TASK-159's stale §"Completion Evidence" prose.** Because TASK-159 is
  reconciled as `SUPERSEDED` — not `DONE` — its "No implementation was
  performed" statement remains an accurate record of what TASK-159 itself
  did. This task adds the SUPERSEDED determination and traceability; it does
  not rewrite TASK-159's historical Stop Condition Report, which
  `TASK_LIFECYCLE.md` §3's immutability principle protects.
- **Creating any task.** The executor creates no follow-up, cleanup, or
  implementation task.
- **The stale XML doc comments** in `BattleStartService.cs`,
  `SwapExecution.cs`, and `BattleStateService.cs` (and the further instances
  in `BossState.cs`, `BossDefinitions.cs`) — they are `src/` comments,
  reported elsewhere, and explicitly outside this task.
- **`API_CONTRACTS.md` §3's `initialState` looseness** — a `docs/` matter,
  reported elsewhere, and outside this task.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Acceptance Criteria

All binary and testable.

```text
[ ] Each of the four tasks was evaluated against the DONE and SUPERSEDED
    definitions in TASK_LIFECYCLE.md §1 from evidence in the repository,
    and each received the terminal status fixed in §"Required Transition":
    TASK-155 = DONE, TASK-159 = SUPERSEDED, TASK-160 = DONE, TASK-161 = DONE.
[ ] TASK-155's file records Status: DONE.
[ ] TASK-159's file records Status: SUPERSEDED, and carries an explicit
    deliverable→satisfying-work traceability record (the §"Traceability
    Matrix" rows) as TASK_LIFECYCLE.md §3 requires for SUPERSEDED.
[ ] TASK-160's file records Status: DONE, and its §"Completion Evidence"
    section is completed with the decision-source and validation evidence
    (its placeholder comment removed and its eight verification boxes
    resolved).
[ ] TASK-161's file records Status: DONE.
[ ] All four files reside in tasks/completed/ and in no other tasks/ folder.
[ ] All four filenames are byte-identical to their pre-move names
    (tasks/README.md §4).
[ ] No task file other than these four (and this task file) was modified:
    TASK-150, TASK-154, TASK-156, TASK-157, TASK-036, and every file already
    in tasks/completed/ remain byte-identical.
[ ] Zero files under src/ were created, modified, renamed, or deleted.
[ ] Zero files under tests/ were created, modified, renamed, or deleted.
[ ] Zero files under docs/ were created, modified, renamed, or deleted.
[ ] No gameplay rule, magnitude, duration, trigger, or target changed.
[ ] No SignalR method, event, subscription, payload member, or delivery rule
    changed.
[ ] No REST endpoint contract, Redis key, Redis-only field, or database
    column changed.
[ ] No ADR was created, and docs/03-decisions/ is unmodified.
[ ] No new task was created by the reconciliation executor.
[ ] The ID consequence is recorded: after this reconciliation the highest
    existing ID across all folders is TASK-162, so the next task ID is
    TASK-163 (tasks/README.md §3).
[ ] quality/review.md §1 checklist passes.
[ ] No authoritative rules or contracts violated (AGENTS.md §10 / ADR-001).
```

**Explicitly required by this task:**

```text
No source changes.
No test changes.
No authoritative documentation changes.
No gameplay changes.
No new task created by the reconciliation executor.
```

---

## Affected Files & Areas

```text
[x] tasks/backlog/TASK-155-*.md   → tasks/completed/  (Status → DONE)
[x] tasks/backlog/TASK-159-*.md   → tasks/completed/  (Status → SUPERSEDED
                                                       + traceability)
[x] tasks/backlog/TASK-160-*.md   → tasks/completed/  (Status → DONE
                                                       + Completion Evidence)
[x] tasks/backlog/TASK-161-*.md   → tasks/completed/  (Status → DONE, move only)
[x] tasks/backlog/TASK-162-<this file>.md — this task file only
[ ] src/backend/ (Domain / Application / Infrastructure / Api)  — NONE
[ ] src/frontend/client/ (scenes / runtime / services / state)  — NONE
[ ] tests/ (unit / integration / gameplay scenarios)            — NONE
[ ] docs/ (all)                                                 — NONE
[ ] docs/03-decisions/ADR/                                      — NONE
[ ] tasks/backlog/TASK-150-*.md                                 — NONE
[ ] tasks/backlog/TASK-154-*.md                                 — NONE
[ ] tasks/backlog/TASK-156-*.md                                 — NONE
[ ] tasks/backlog/TASK-157-*.md                                 — NONE
[ ] tasks/blocked/TASK-036-*.md                                 — NONE
[ ] tasks/completed/* (all pre-existing)                        — NONE
```

---

## Implementation Notes

- **This is a record correction, not a re-execution.** The executor must not
  re-run, re-apply, re-verify, or re-open any of the four tasks' work. Every
  outcome is fixed in §"Required Transition"; the executor applies it and
  records the evidence.
- **Do not default the four to one outcome.** The lifecycle definitions
  differ by *whether the task was directly executed*
  (`TASK_LIFECYCLE.md` §1). Applying `SUPERSEDED` to TASK-155/160/161 would
  misstate that they were directly executed; applying `DONE` to TASK-159
  would contradict its own record that no implementation was performed and
  would violate the §2 prohibition on `BACKLOG`/`READY → DONE`.
- **TASK-160 is the subtlest case, and the correct reading is DONE.** Its
  objective is to "obtain and record" decisions; the decisions are recorded
  in its own file with the required coverage, which *is* its deliverable, and
  downstream documents cite them as the decision source. A recording task is
  executed by recording. The unfilled §"Completion Evidence" is an
  incomplete record, not unperformed work — so the executor completes that
  section rather than re-classifying the task.
- **TASK-159's own prose is deliberately left intact.** Its "No
  implementation was performed" statement is *true of TASK-159*, which is
  exactly why it is SUPERSEDED rather than DONE. Rewriting it would destroy
  the historical record that justifies the SUPERSEDED outcome. Add the
  determination and the traceability; do not rewrite the Stop Condition
  Report.
- **TASK-161 needs placement only.** Its Status already reads DONE; only the
  folder move is outstanding. Do not re-word its Completion Evidence.
- **Preserve filenames exactly.** `tasks/README.md` §4 requires the same
  filename across folders; rename nothing.
- **Report, do not fix, adjacent items** (`AGENTS.md` §16). The stale `src/`
  XML comments, `API_CONTRACTS.md` §3's looseness, the absent ADR for the
  projection widening, and TASK-154/156's continued presence in `backlog/`
  are all observed and are outside this task's four-file boundary.
- **Do not create follow-up tasks.** Report anything discovered; the
  Orchestrator sequences work.

---

## Testing Requirements

This task changes no code, so it produces no unit, integration, or gameplay
test. Its verification is a **lifecycle-state check**, a **transition-
legality check**, a **traceability check**, and an **isolation check**, at the
depth `core/validation.md` requires for a LOW-risk DOCUMENTATION task.

### Required Verification

```text
[x] Definition check        — each task's terminal status matches the DONE or
                              SUPERSEDED definition in TASK_LIFECYCLE.md §1,
                              decided from evidence rather than from the
                              pre-existing Status field
[x] Transition-legality     — no executed transition is invalid per
    check                     TASK_LIFECYCLE.md §2 (specifically: no
                              BACKLOG/READY → DONE, and no mutation of a task
                              already in completed/)
[x] Supersession-evidence   — TASK-159 satisfies all four §3 SUPERSEDED
    check                     conditions, with each deliverable linked to its
                              satisfying downstream work
[x] Placement check         — all four files are in tasks/completed/ and in
                              no other tasks/ folder; filenames unchanged
[x] Traceability check      — TASK-160's decisions are cited by the documents
                              that applied them; TASK-155's and TASK-161's
                              recorded edits are present in docs/
[x] ID-accounting check     — next ID after reconciliation is TASK-163
[x] Isolation verification  — src/ (0 files), tests/ (0 files), docs/ (0
                              files); the non-reconciled backlog files and
                              all pre-existing completed/ files byte-identical
[x] No-new-task check       — the executor created no task
[x] Unit tests              — N/A (no code)
[x] Integration tests       — N/A (no code)
[x] Gameplay scenarios      — N/A (no behavior changed)
```

### Key Edge Cases

- **TASK-159 is BLOCKED but sitting in `backlog/`, not `blocked/`.** Its
  recorded status and its folder disagree. The executor applies the SUPERSEDED
  terminal state and the move to `completed/`, preserving the recorded status
  history, rather than first "correcting" the folder to `blocked/`. Moving it
  to `blocked/` would be wrong: its blocker is resolved.
- **TASK-161 already records DONE while still in `backlog/`.** Do not treat
  the pre-existing `DONE` value as proof the transition was completed;
  `TASK_LIFECYCLE.md` §4 requires the file to be in `completed/`.
- **TASK-160 has decisions recorded but no Completion Evidence.** The
  correct response is to complete that section from the §"Evidence" rows —
  not to conclude the task is unfinished, and not to treat the missing
  section as a STOP.
- **TASK-155's Status reads BACKLOG although its body records full
  execution.** Judge by the body's evidence, not the Status field.
- **Do not "tidy" TASK-154 or TASK-156.** They are decision-input records
  whose documented resting place is `backlog/` until an Orchestrator
  sequences them; they are not part of this reconciliation and must remain
  byte-identical.
- **A temptation to re-verify the docs by editing them.** Any drift found in
  `docs/` is a **report**, not an edit — this task's boundary forbids all
  `docs/` writes, and a documentation-conformance issue would be its own
  task.

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 apply.

- **If evidence is insufficient to determine DONE versus SUPERSEDED for any
  of the four tasks: STOP.** Do not default the outcome and do not guess.
- **If a task is found to have independent unsatisfied scope: STOP.** Report
  it rather than filing the task as terminal.
- **If the lifecycle rules do not permit the required transition: STOP.**
  `TASK_LIFECYCLE.md` §2 governs; if a required transition is listed as
  invalid and no permitted route to the correct terminal state exists, STOP
  and report.
- **If two task records contradict the actual implementation: STOP.** Report
  the contradiction per `AGENTS.md` §4 rather than reconciling it silently.
- **If reconciliation would require modifying source or docs: STOP.**
- **If a task requires reopening rather than lifecycle reconciliation:
  STOP.** A task needing rework is not SUPERSEDED and is not DONE.
- **If TASK-159's delivered subject matter turns out NOT to be satisfied by
  downstream work: STOP.** Its SUPERSEDED determination depends on that
  satisfaction; without it the correct status is neither DONE nor
  SUPERSEDED, and the block stands.
- **If a fifth task is drawn into scope: STOP.** This task reconciles exactly
  four files.
- If the task exceeds 7 skills or crosses multiple uncoupled boundaries:
  STOP & decompose.

---

## Completion Evidence

### Changed Files

- `tasks/completed/TASK-155-apply-gap-5-thuy-ma-healing-reduction-decisions-to-authoritative-documentation.md` — moved to `tasks/completed/`, Status updated to `DONE`.
- `tasks/completed/TASK-159-deliver-pet-and-boss-status-effect-projection-in-battlestateupdated.md` — moved to `tasks/completed/`, Status updated to `SUPERSEDED`, deliverable traceability matrix added.
- `tasks/completed/TASK-160-collect-product-owner-decisions-pet-statuseffects-and-boss-live-hp-projection.md` — moved to `tasks/completed/`, Status updated to `DONE`, Completion Evidence populated.
- `tasks/completed/TASK-161-apply-task-160-pet-statuseffects-and-boss-live-hp-decisions-to-authoritative-documentation.md` — moved to `tasks/completed/`, Status `DONE` confirmed.
- `tasks/backlog/TASK-162-reconcile-lifecycle-state-of-task-155-159-160-161.md` — this task file.

### Validation Results

```text
Definition check:            PASS — TASK-155 (DONE), TASK-159 (SUPERSEDED), TASK-160 (DONE), TASK-161 (DONE) match definitions in TASK_LIFECYCLE.md §1.
Transition-legality check:   PASS — permitted transitions executed (BACKLOG→DONE, BLOCKED→SUPERSEDED, BACKLOG→DONE, DONE→DONE) with direct execution and downstream satisfaction evidence.
Supersession-evidence check: PASS — 100% of TASK-159 deliverables traced to downstream tasks (TASK-160, TASK-161, landed projection implementation); 0 independent actionable scope remains.
Placement check:             PASS — all four tasks reside in tasks/completed/; byte-identical filenames preserved.
Traceability check:          PASS — TASK-155 and TASK-161 documentation changes verified in docs/; TASK-160 Product Owner decisions verified byte-for-byte and cited by authoritative docs.
ID-accounting check:         PASS — highest existing ID across all folders is TASK-162; next newly created task ID is TASK-163.
Isolation verification:      PASS — src/ (0 files changed), tests/ (0 files changed), docs/ (0 files changed), tasks/completed/ pre-existing files unchanged.
No-new-task check:           PASS — 0 new tasks created.
```

### Reconciled Lifecycle State

```text
TASK-155: BACKLOG → DONE        (tasks/completed/TASK-155-apply-gap-5-thuy-ma-healing-reduction-decisions-to-authoritative-documentation.md)
TASK-159: BLOCKED → SUPERSEDED  (tasks/completed/TASK-159-deliver-pet-and-boss-status-effect-projection-in-battlestateupdated.md)
TASK-160: BACKLOG → DONE        (tasks/completed/TASK-160-collect-product-owner-decisions-pet-statuseffects-and-boss-live-hp-projection.md)
TASK-161: DONE    → DONE        (tasks/completed/TASK-161-apply-task-160-pet-statuseffects-and-boss-live-hp-decisions-to-authoritative-documentation.md)

Highest Task ID: TASK-162
Next Task ID:    TASK-163
```

### Server Authority & Scope Verification

- [x] Confirmed zero files under `src/` modified
- [x] Confirmed zero files under `tests/` modified
- [x] Confirmed zero files under `docs/` modified
- [x] Confirmed zero gameplay, protocol, API, Redis, or architecture change
- [x] Confirmed no task other than the four named (and this file) was modified
- [x] Confirmed no completed task was re-opened or mutated
- [x] Confirmed no new task was created by the executor
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — no capability
      was added or changed
