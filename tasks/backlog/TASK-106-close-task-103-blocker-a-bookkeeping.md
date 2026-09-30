# TASK-106 — Close TASK-103's Blocker A Bookkeeping and Re-Point Its Shield Half

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, schemas, or contracts.

  WHY THIS TASK IS SMALL: the contract work it would otherwise be tasked
  with is ALREADY DONE. Blocker A was applied to docs/ by TASK-103 itself
  (SIGNALR_PROTOCOL.md v2.7, GAME_EVENTS.md v2.8), and Blocker B was
  resolved by TASK-105 (COMBAT_RULES.md §4, v1.6). What remains is
  BOOKKEEPING: TASK-103 still reads "Blocker A APPLIED; Blocker B BLOCKED"
  with 10 unchecked Blocker A criteria and a Blocker B section describing a
  conflict that no longer exists.

  THIS TASK CHANGES NO CONTRACT AND NO RULE. It edits ONE task file
  (TASK-103's) to make its recorded state match the repository's actual
  state. If the executing agent finds that ANY contract edit is genuinely
  still required, that is a STOP (§9 item 1) — the work belongs in a task
  with the correct type, not here.

  BOUNDARY: zero files under src/ and tests/. Zero files under docs/.
-->

---

## Metadata

```text
Task ID:           TASK-106
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"). Narrowed by
                   this task's own scope: the only file changed is a task file
                   under tasks/, because the docs/ contract text is already
                   correct. Workflow documentation/documentation-change.md §2
                   ("no duplication") is the governing principle — see the Type
                   classification note.
Status:            IN REVIEW (executed. All ten Blocker A criteria verified
                   satisfied against the applied contract text and ticked with
                   per-criterion subsection citations; TASK-103's Status field
                   corrected; the TASK-105 superseded-by block added with both
                   historical STOP records preserved unaltered. Zero files under
                   `docs/`, `src/`, or `tests/` changed — all four contract
                   documents re-hashed and byte-identical. No STOP condition
                   fired and no contract mismatch was found. Per
                   `TASK_LIFECYCLE.md` §4 the file stays in `backlog/`; landing
                   in `completed/` requires review to pass, and TASK-103's own
                   terminal lifecycle state is deliberately NOT asserted — see
                   "Verification Record".)
Risk:              LOW (TASK_TYPES.md §4 — DOCUMENTATION baseline LOW–MEDIUM;
                   this is a LOW correction. No contract, rule, schema, wire
                   member, or gameplay semantic is altered. The only file
                   written is TASK-103's task file.)
Priority:          MEDIUM (TASK-102 is READY and its implementation runs against
                   contracts that TASK-103's own status field still describes as
                   BLOCKED. The stale status is a false signal that misdirects
                   the next implementer; it does not itself block code.)
Primary Agent:     review (TASK_TYPES.md §2 / AGENT_SELECTION.md §1 —
                   "Documentation change: Primary Agent Review")
Supporting Agents: realtime (the A-series rulings' owner document is
                   SIGNALR_PROTOCOL.md — consulted to CONFIRM the applied state,
                   not to edit it),
                   gameplay (Blocker B's owner COMBAT_RULES.md §4 — consulted to
                   CONFIRM TASK-105 closed it, not to edit it)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-103 (the file this task edits — its Blocker A is
                     APPLIED and its Blocker B is superseded; read the
                     Completion Evidence, do not redo the work),
                   TASK-104 (READY — the decision register holding the A-1…A-5
                     and B-1…B-3 Product Owner answers this task cites as
                     already-applied; read-only, NOT modified),
                   TASK-105 (DONE — resolved Blocker B by changing
                     COMBAT_RULES.md §4 from additive stacking to refresh, and
                     authored the depletion rule; immutable, NOT modified),
                   TASK-102 (READY — the implementation task both blockers were
                     blocking; read-only, NOT modified)
Blocks:            Nothing. TASK-102 is READY and is not blocked by TASK-103's
                   stale status field — its contracts are already authored. This
                   task removes a misleading record, it does not unblock code.
Estimate:          Small (verify ten already-satisfied criteria against the
                   applied docs, tick them, correct one status field, and
                   re-point one superseded section; no docs/ edit, no code)
```

**Type classification note.** `DOCUMENTATION`, and deliberately the smallest
possible instance of it. `TASK_TYPES.md` §2 defines the type by its *output*
("Change `docs/` content — documentation is the primary output"), and the
repository's documentation-consistency principle (`AGENTS.md` §17,
`documentation-change.md` §2) is that a document which no longer matches
reality is corrected **where the error is** — here, in the task file that holds
the stale claim, not in the `docs/` files that are already correct.

**This task applies no decision and authors no contract.** The A-1…A-5 rulings
were applied by TASK-103 to `SIGNALR_PROTOCOL.md` §3.2 (v2.7) and
`GAME_EVENTS.md` §2 (v2.8) — verified live at TASK-106 authoring time; see
"Current State". The B-1/B-2/B-3 rulings were applied by TASK-105 to
`COMBAT_RULES.md` §4 (v1.6). **Every contract this repository's task chain was
waiting on already exists.** Re-applying them would be redundant churn and is
forbidden by this task's §3 Out of Scope.

**No ADR is required.** No layering, storage, transport, or authoritative-model
decision is touched (`AGENTS.md` §18).

---

## Objective

Make TASK-103's recorded state match the repository's actual state, so that its
stale status no longer misrepresents two already-resolved blockers: verify each
of TASK-103's ten Blocker A acceptance criteria against the rulings already
applied in `SIGNALR_PROTOCOL.md` §3.2 and `GAME_EVENTS.md` §2 and mark the
verified ones satisfied; then correct TASK-103's `Status:` field and re-point
its Blocker B section at the resolution TASK-105 already delivered in
`COMBAT_RULES.md` §4 — **editing nothing under `docs/`, `src/`, or `tests/`.**

---

## Authoritative References

### The state to be verified (the applied contract text — READ ONLY)

- `docs/02-technical/SIGNALR_PROTOCOL.md` **version header (v2.7)** and **§3.2.2**
  — the discriminator table extended to 16 names and its closed-set statement
  restated as closed against events `GAME_EVENTS.md` §2 does not define
  (TASK-104 **A-1A**). §3.2.2 item 5 records that admission adds no method and
  no state-push member.
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3.2.20** (`CardCast` — `type`,
  `cardId`; item 2 records MVP carries **no** Power cost member, TASK-104
  **A-2C**; item 3 applies the omission convention), **§3.2.21**
  (`PetSkillCast` — `type`, `cardId`, the same identity and spelling as
  §3.2.20; item 2 records the Signature Skill confirmation is carried by
  `cardId`, TASK-104 **A-4B**), **§3.2.22** (the emission order — `CardCast`
  then `PetSkillCast`, TASK-104 **A-5A**; item 4 states the §17 step list is
  unchanged), **§3.2.23** (`RelicTriggered`), **§3.2.24** (`PowerChanged`,
  item 4 distinguishing it from a Card cost member), **§3.2.25** (the
  `effect summary` omission convention, TASK-104 **A-3**).
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3.2.3** (camelCase), **§3.2.4**
  (enum projected to its name), **§3.2.5** (omitted never `null`),
  **§3.2.12** (member scope; the stale "seven events" count corrected to all
  16), **§3.2.17**/**§3.2.18**/**§3.2.19** (reconciled to §3.2.25).
- `docs/02-technical/GAME_EVENTS.md` **version header (v2.8)** and **§2** —
  the `CardCast`/`PetSkillCast`, `RelicTriggered`, and `PowerChanged` payload
  items authored; `effect summary` restated as "not populated yet" with a
  cross-reference to `SIGNALR_PROTOCOL.md` §3.2.25; §2 item 5 records the
  `CardCast` → `PetSkillCast` order; §3 item 1 continues to defer wire shape to
  the protocol owner.
- `docs/01-game-design/COMBAT_RULES.md` **version header (v1.6)** and **§4** —
  the Shield rule, already reconciled to refresh with the depletion rule
  authored (TASK-105). **Cited only to confirm Blocker B is closed; this task
  does not touch Shield semantics.**

### The record to be corrected (the file this task edits)

- `tasks/backlog/TASK-103-resolve-cardcast-wire-schema-and-shield-stacking-contract.md`
  — its `Status:` field, its **Acceptance Criteria** §"Blocker A" (criteria 1–9)
  and §"Blocker B", and its **Completion Evidence** sections "Blocker A —
  Resolution (APPLIED)" and "Blocker B — Resolution (STILL OPEN — RE-TYPING
  REQUIRED)". The Blocker B STOP report preserved there is an unaltered
  historical record and **must remain preserved** — it is superseded, not
  false; TASK-105 is what resolved it.

### The decision register (read-only)

- `tasks/backlog/TASK-104-collect-cardcast-wire-and-shield-contract-decisions.md`
  **§4 (A-1…A-5, B-1…B-3), §5A** — the Product Owner's answers, recorded
  verbatim. This task cites them; it does not re-open, re-rank, or re-apply
  them.

### Precedent and governance

- `tasks/completed/TASK-105-change-shield-application-semantics-to-refresh.md`
  — the GAMEPLAY-CHANGE task that resolved Blocker B. **Immutable; do not
  modify.**
- `tasks/README.md` §5 (folder/status semantics: within-folder status changes
  are a `Status:` field change, not a file move), §9 (no business-rule
  duplication in task files).
- `tasks/TASK_LIFECYCLE.md` §3 (`BACKLOG`/`DONE` definitions; completed tasks
  are immutable), §4 (file-movement summary).
- `AGENTS.md` §16 (report, do not fix, adjacent issues), §17 (a document that
  no longer matches reality is corrected where the error is).
- `docs/00-overview/MVP_SCOPE.md` §1/§2 — Cards and Combat are IN; no OUT item is
  reachable by a bookkeeping correction.

---

## Current State

**Both of TASK-103's blockers are already resolved in `docs/`.** The following
was verified directly against the working tree at TASK-106 authoring time.

```text
Blocker A — APPLIED (by TASK-103 itself).
  SIGNALR_PROTOCOL.md v2.7 header states the four events were added and the
  A-1A/A-2C/A-3/A-4B/A-5A rulings applied. §3.2.2's table lists 16 names;
  §3.2.20–§3.2.25 exist; the stale "seven events" / "four events" counts were
  corrected. GAME_EVENTS.md v2.8 states the same, with §2 payload items
  authored and the effect-summary wording moved to "not populated yet".
  TASK-103's own Status field already reads "Blocker A APPLIED".

Blocker B — RESOLVED (by TASK-105, a separate GAMEPLAY-CHANGE task).
  COMBAT_RULES.md v1.6 §4 now states refresh-not-stack, one Shield instance,
  set-to-new-magnitude, removal at exactly 0 in the same resolution, and
  overflow to HP by the remainder. The COMBAT_RULES.md §4 item 3 vs
  GAME_STATE.md §2.3.1 item 6 conflict TASK-103 recorded is therefore gone.
```

**What is stale is the record, not the repository.** TASK-103 still presents
itself as `PARTIALLY RESOLVED — Blocker A APPLIED; Blocker B BLOCKED`, carries
**10 unchecked** Blocker A acceptance criteria, and its Completion Evidence
still contains a Blocker B section headed "STILL OPEN — RE-TYPING REQUIRED"
directing a future agent to create a GAMEPLAY-CHANGE task for a rule change
that TASK-105 has already shipped. `TASK-102` is `READY` and its criteria are
written against contracts that already exist — so the stale status is a **false
blocked-signal**, not a real blocker.

**Consequence if not corrected:** the next implementer reading TASK-103 would
be pointed at two non-existent work items — re-applying already-applied protocol
text, and commissioning a Shield rule change that already landed. That is the
specific harm this task removes.

---

## Scope

### In Scope

1. **Verify the ten Blocker A criteria** in TASK-103 §"Acceptance Criteria"
   (criteria 1–9) against the already-applied contract text, and mark
   `[x]` exactly those that are genuinely satisfied — each with a one-line
   citation of the subsection that satisfies it (`§3.2.2`, `§3.2.20`–`§3.2.25`,
   `GAME_EVENTS.md` §2). Any criterion that is **not** satisfied stays `[ ]`
   and is reported per §9 item 1 rather than ticked.
2. **Correct TASK-103's `Status:` field** to state the true position: Blocker A
   applied and verified; Blocker B resolved by TASK-105 (not by this task). The
   field must not claim work this task did not do.
3. **Re-point the Blocker B record in TASK-103's Completion Evidence** — add a
   superseded-by note naming TASK-105 and the `COMBAT_RULES.md` §4 rule it
   delivered — **preserving the original STOP report and the "NOT APPLIED"
   record unaltered** as historical evidence, per the TASK-093 §12 precedent
   TASK-103 itself cites.
4. **Report** any residual inconsistency found between TASK-103's recorded
   claims and the actual `docs/` state, rather than silently reconciling it.

### Out of Scope

- **Any edit to `docs/`.** `SIGNALR_PROTOCOL.md`, `GAME_EVENTS.md`,
  `COMBAT_RULES.md`, `GAME_STATE.md`, and every other file under `docs/` are
  **byte-identical** after this task. The contract text is already correct; if
  the executing agent believes an edit is still required, that is §9 item 1.
- **Re-applying A-1…A-5, or any part of Blocker A.** Already applied; re-doing
  it is redundant churn (`documentation-change.md` §2).
- **Applying B-1…B-3, or modifying Shield semantics in any way.** Already
  resolved by TASK-105; Shield is explicitly out of this task's boundary.
- **Any source code or test change.** `src/` and `tests/` are byte-identical.
- **Modifying TASK-102.** It is `READY`; do not edit it, move it, re-status it,
  or rewrite its acceptance criteria.
- **Modifying TASK-105.** It is `DONE` (`TASK_LIFECYCLE.md` §3 — completed tasks
  are immutable).
- **Modifying TASK-104** or re-opening any of its nine decisions.
- **Any new event, wire member, payload member, state field, SignalR method,
  Redis key, API endpoint, or migration.**
- **Any new gameplay rule, Card, Pet, Boss, Relic, Element, Status Effect type,
  or balance value** — including Tidal Barrier's still-unauthored Shield
  magnitude (TASK-104 B-4), which remains a separate content decision.
- **Moving TASK-103 between folders.** `TASK_LIFECYCLE.md` §4 lists no
  `backlog/` → `backlog/` move for a status change; `tasks/README.md` §5 is
  explicit that within-folder status transitions are `Status:` field changes
  only. Whether TASK-103 should be closed as DONE is a human/orchestrator
  decision this task reports, not one it takes (§9 item 3).
- **TASK-036, TASK-079, TASK-099, and every `tasks/completed/*` file.**
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Acceptance Criteria

All binary. Criteria 1–5 concern the verification; 6–9 the record and scope.

- [ ] Each of TASK-103's ten Blocker A acceptance criteria is individually
      evaluated against the applied contract text, and each satisfied criterion
      is marked `[x]` with a citation to the subsection that satisfies it.
- [ ] No Blocker A criterion is marked `[x]` without that citation, and no
      unsatisfied criterion is marked `[x]` — verified by reading all ten
      against the references in "Authoritative References".
- [ ] TASK-103's `Status:` field no longer asserts that Blocker B is open or
      that a re-typing is still required; it records that Blocker B was resolved
      by TASK-105.
- [ ] TASK-103's Blocker B STOP report and its "NOT APPLIED" resolution record
      are **preserved unaltered** as historical evidence, with a clearly marked
      superseded-by note naming TASK-105 (`TASK_LIFECYCLE.md` §3 records why
      history is kept rather than rewritten).
- [ ] TASK-103's record contains no claim of work performed by TASK-106 beyond
      the bookkeeping actually done.
- [ ] `docs/` is **byte-identical**: `SIGNALR_PROTOCOL.md` remains `AE4FD980…`
      and `COMBAT_RULES.md` remains `258B12A3…` (the values TASK-105's
      Completion Evidence records), verified by SHA256 before and after.
- [ ] `src/` and `tests/` are byte-identical; no build or test run is required
      or performed (no code change).
- [ ] TASK-102, TASK-104, TASK-105, TASK-036, TASK-079, TASK-099, and every
      `tasks/completed/*` file are unmodified.
- [ ] Scope validated against `MVP_SCOPE.md` §1/§2
      (`quality/scope-validation`) and the documentation-consistency audit
      passes (`quality/documentation-consistency`), and the quality review
      checklist passes (`quality/review.md` §1) skipping only the code-only
      items per `documentation/documentation-change.md` §4.

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)   — NONE
[ ] src/frontend/client/ (scenes / runtime / services / state)   — NONE
[ ] tests/ (unit / integration / gameplay scenarios)             — NONE
[ ] docs/                                                        — NONE
      (SIGNALR_PROTOCOL.md, GAME_EVENTS.md, COMBAT_RULES.md, and GAME_STATE.md
       are already correct; editing them would be redundant churn and is a STOP)
[ ] docs/00-overview/                                            — NONE
[ ] docs/03-decisions/ADR/                                        — NONE
[x] tasks/backlog/TASK-103-resolve-cardcast-wire-schema-and-shield-stacking-contract.md
      (the ONLY substantive edit: ten criteria verified, Status corrected,
       Blocker B superseded-by note added; history preserved)
[x] tasks/backlog/TASK-106-close-task-103-blocker-a-bookkeeping.md
      (this file: its own Completion Evidence, when executed)
```

---

## Implementation Notes

- **The work is verification, not authoring.** Blocker A's ten criteria are
  checked against text that already exists. Read
  `SIGNALR_PROTOCOL.md` §3.2.2 and §3.2.20–§3.2.25 with
  `GAME_EVENTS.md` §2 side by side, and tick what is genuinely there.
- **Cite, do not restate.** When recording which subsection satisfies a
  criterion, cite it (`§3.2.21 item 2`). Never copy a member table, a payload
  shape, or a value into TASK-103 or this file (`AGENTS.md` §9,
  `documentation-change.md` §2, `tasks/README.md` §9).
- **Distinguish "applied" from "correct."** TASK-103 asserts Blocker A is
  applied; this task independently confirms it. If a criterion looks applied but
  the wording does not actually satisfy it, that is a genuine finding — report it
  under §9 item 1 rather than ticking the box.
- **Do not resolve TASK-103's status lifecycle.** Its Blocker B half was
  resolved by a *different* task (TASK-105, `GAMEPLAY-CHANGE`), not by TASK-103.
  Whether TASK-103 is therefore DONE, or should be closed as superseded, is a
  human/orchestrator call — `AGENTS.md` §20's "ambiguous requirement" applies.
  Record the position; do not compute the transition.
- **Preserve the STOP report.** TASK-103's Blocker B record was accurate when
  written. Rewriting it to look as though TASK-103 resolved it would falsify the
  history TASK-093 §12's precedent exists to protect.
- **Report, do not fix, adjacent gaps** (`AGENTS.md` §16). Known and previously
  reported, none belonging to this task: Tidal Barrier's unauthored Shield
  magnitude (TASK-104 B-4); Thanh Xà / Sơn Hùng Signature Skill content
  (`PET_RULES.md` §8); the `BattleStateJson.cs` items TASK-097/TASK-098 reported;
  and `BattleHub.cs`'s "intentionally NOT implemented" comment, which is still
  accurate and is TASK-102's to correct when it implements the methods.

---

## Testing Requirements

This task changes no code and no contract, so it produces no unit, integration,
or gameplay test. Its verification is a **documentation consistency audit**, a
**hash-based change-isolation check**, and a **scope validation**, at the depth
`core/validation.md` §2 requires for a LOW-risk DOCUMENTATION task.

### Required Verification

```text
[x] Documentation consistency audit — TASK-103's recorded state vs. the actual
                                      `docs/` state, for each of the ten
                                      Blocker A criteria and the Blocker B
                                      record; plus confirmation that
                                      SIGNALR_PROTOCOL.md §3.2 and
                                      GAME_EVENTS.md §2 still agree with each
                                      other and with COMBAT_RULES.md §4
[x] Change-isolation check          — SHA256 of SIGNALR_PROTOCOL.md,
                                      GAME_EVENTS.md, COMBAT_RULES.md, and
                                      GAME_STATE.md before and after; all four
                                      unchanged
[x] Scope validation                — MVP_SCOPE.md §1/§2;
                                      quality/scope-validation
[ ] Unit tests                      — N/A (no code)
[ ] Integration tests               — N/A (no code)
[ ] Gameplay scenarios              — N/A (no code, no rule change)
```

### Key Edge Cases

- **A Blocker A criterion that is applied but not actually satisfied** — e.g. a
  required-vs-optional statement that is implicit rather than explicit. Must be
  reported, not ticked.
- **The `effect summary` convention** — `SIGNALR_PROTOCOL.md` §3.2.25 and
  `GAME_EVENTS.md` §2 must not disagree about whether the element is "deferred"
  or "not populated yet"; confirm one convention governs
  (`documentation-change.md` §2).
- **The `CardCast` cost member** — confirm `SIGNALR_PROTOCOL.md` §3.2.20 and
  `GAME_EVENTS.md` §2 both record its absence consistently, and that neither
  implies a cost contract A-2C did not authorize.
- **TASK-103's Blocker B history** — confirm the superseded-by note does not
  overwrite the original STOP report or the "NOT APPLIED" record.

---

## Stop Conditions

Universal stop conditions (`AGENTS.md` §20, `.ai/README.md` §13) always apply.
Task-specific:

1. **If any of TASK-103's ten Blocker A criteria is NOT actually satisfied by
   the applied contract text** — **STOP** and report which criterion and what is
   missing. Do not tick it, and do not author the missing contract text here:
   that is a `DOCUMENTATION`/`GAMEPLAY-CHANGE` task with its own scope and
   approval path. **Do not invent a protocol member** (§9 item 4).
2. **If correcting TASK-103's record would require editing any file under
   `docs/`** — **STOP** and report. The contract text is the source of truth and
   is already correct; a needed `docs/` edit means a different task is required.
3. **If deciding TASK-103's correct lifecycle state requires a judgement this
   task cannot make from `TASK_LIFECYCLE.md`** — e.g. whether a task whose
   blockers were closed by *other* tasks is DONE or superseded — **STOP** and
   record the required human/orchestrator decision. Do not move the file and do
   not set a terminal status on someone else's authority.
4. **If the A-1…A-5 decisions are found to be in conflict with the authoritative
   `docs/` text** rather than already consistent with it — **STOP per
   `AGENTS.md` §4** and report both sources. Do not silently favor one.
5. **If any part of this task would require a new event, wire member, payload
   member, state field, SignalR method, Redis key, API endpoint, or migration** —
   **STOP**; report it for a separate task.
6. **If any part would change Shield semantics, or touch
   `COMBAT_RULES.md` §4** — **STOP**; Shield is resolved and out of scope here.
7. **If satisfying any criterion requires modifying TASK-102, TASK-104,
   TASK-105, TASK-036, TASK-079, TASK-099, or any `tasks/completed/*` file** —
   **STOP**; report the stale statement instead (`AGENTS.md` §16).
8. **If the change would cross into `MVP_SCOPE.md` §2's OUT list, or reach an
   unlisted (FUTURE) system** — **STOP** (`AGENTS.md` §8).
9. **If the task exceeds 7 skills or crosses an uncoupled architectural
   boundary** — **STOP and decompose** (`tasks/README.md` §13).

When stopped, report the exact condition and **do not invent a resolution**.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual. Record a §9 STOP report here instead if a contract
  gap, a docs/ edit, or a lifecycle judgement is required — do not claim a
  resolution that was not made.
-->

### Pickup Baseline (recorded)

```text
4156BB877BE3D6452897BF5C1834FB1E38178E9847818A53B52263187F36A067  tasks/backlog/TASK-103-resolve-cardcast-wire-schema-and-shield-stacking-contract.md
AE4FD980C6D5444148093A1BA76831586D45ABA9263A50DDAB447177B4CB2AA1  docs/02-technical/SIGNALR_PROTOCOL.md
454A6F82611B927F0C4987A9014ECBE75690AC93147225A490B5C43CF27E75CE  docs/02-technical/GAME_EVENTS.md
258B12A3E53962AD3983DFE6E9895FE4AD07762EF1997FFFF5FDA70802EF0EF6  docs/01-game-design/COMBAT_RULES.md
7004B262B7B4CFDDA7FE479038D9C281299040FF983CDFF828AABE6C6481609B  docs/02-technical/GAME_STATE.md
F08C2490306ED662A25E569EA5D9F1D25B6AA486BFDA4C568F4D5F02C690D82F  docs/01-game-design/CARD_RULES.md
7514786E52AFE7774CA64451275C66A3CA7BEE8AF2B9AC17D4C0CA96CD143C60  docs/01-game-design/GAME_RULES.md
15DA088ABE0291E12B90BF9DF2AD8D7082CEAD9704AA464496415CC298C804B8  tasks/backlog/TASK-102-implement-card-cast-server-path.md
F830DBC580E21BE4F9EF819FED899AD6FA3472187A6C0DE2EFBDBB37763857D6  tasks/backlog/TASK-104-collect-cardcast-wire-and-shield-contract-decisions.md
22356299F140318D96C5AD43B5D7655F0F91DF11EE87BEDFFB2BE316F224367F  tasks/completed/TASK-105-change-shield-application-semantics-to-refresh.md
```

`git status --short` at pickup recorded the same pre-existing modifications
TASK-103/TASK-105 recorded: `docs/01-game-design/{BOSS_RULES,COMBAT_RULES,GAME_RULES}.md`,
`docs/02-technical/{API_CONTRACTS,ARCHITECTURE,DATABASE,GAME_EVENTS,GAME_STATE,SIGNALR_PROTOCOL,TDD}.md`,
several `src/` and `tests/` files, plus untracked `StatusEffect*` and
`TASK-086…TASK-106` files. None was reverted, and none was folded into this task.
The baseline is the **working-tree content at pickup**, not `HEAD`.

### Verification Record

```text
Blocker A criteria verified:   10 of 10 SATISFIED and ticked. No criterion was
                               left unchecked and none failed. Each tick carries
                               a citation of the subsection that satisfies it:
                                 C1  discriminator CardCast    → §3.2.2 table, §3.2.20 `type` row
                                 C2  discriminator PetSkillCast→ §3.2.2 table, §3.2.21 `type` row
                                 C3  payload member names      → §3.2.20 / §3.2.21 member tables
                                                                 (`type`, `cardId`); §3.2.12 item 1
                                 C4  casing camelCase          → §3.2.20 item 1, §3.2.21 item 1, §3.2.3 item 1
                                 C5  required-vs-optional      → both tables' `Presence` column = `always`;
                                                                 §3.2.20 item 3, §3.2.21 item 4, §3.2.5
                                 C6  every member's type       → both tables' `Type` column (`string`)
                                 C7  enum representation       → VACUOUS: neither event carries an
                                                                 enum-valued member; §3.2.2 item 3 still
                                                                 fixes name-not-ordinal for `type`
                                 C8  relationship + order      → §3.2.22 items 1, 2, 4; §3.2.21 item 5
                                 C9  no §3.2 / §2 contradiction→ §3.2.2 item 2, §3.2.12 item 1,
                                                                 GAME_EVENTS.md §2 items 2–5
                                 C10 TASK-102 projectable      → §3.2.20–§3.2.22 complete; §3.2.25
                                                                 disposes of `effect summary`
                               C7 is ticked as satisfied **vacuously**: the criterion is
                               conditional ("for any enum-valued member"), and §3.2.20/
                               §3.2.21 declare no enum-valued member. This is recorded
                               explicitly rather than ticked silently.
Blocker B record:              SUPERSEDED-BY NOTE ADDED naming TASK-105 and the
                               COMBAT_RULES.md §4 v1.6 (258B12A3…) contract it
                               delivered. Both original historical records — the
                               "NOT APPLIED" resolution block and the "NO — STOP"
                               report — are PRESERVED UNALTERED, as is the
                               section heading. Verified by line-level search:
                               L1276 "NO — NOT APPLIED", L1289 "Multiple Shields:
                               RESOLVED BY RULING", L1325 "NO — STOP", L1328
                               "Ruling: NONE — requires the B-1/B-2 human gameplay
                               ruling", L1335 "UNRESOLVED — the exact conflict
                               under adjudication". One stale in-body forward
                               reference ("STILL OPEN") was left untouched
                               deliberately — it is inside the historical block.
TASK-103 Status corrected to:  "BOTH BLOCKERS RESOLVED — Blocker A APPLIED and
                               VERIFIED; Blocker B RESOLVED BY TASK-105 (not by
                               this task)." The field now states the true
                               position and claims no work TASK-106 did not do.
Lifecycle judgement:           NOT ASSERTED — recorded as an explicit NOTE in
                               TASK-103's Status field and in the superseded-by
                               block. Blocker B was closed by a DIFFERENT task
                               (TASK-105), so whether that completes TASK-103 is
                               a human/orchestrator decision. This task did not
                               set a terminal status and did not move the file
                               (§9 item 3; TASK_LIFECYCLE.md §4 lists no
                               backlog/ → backlog/ move, and tasks/README.md §5
                               makes within-folder transitions a Status-field
                               change only).
Contract mismatches found:     NONE. No STOP condition fired.
```

### Changed Files

- `tasks/backlog/TASK-103-resolve-cardcast-wire-schema-and-shield-stacking-contract.md`
  — the 10 Blocker A criteria verified and ticked with per-criterion subsection
  citations and an explanatory header comment; the `Status:` field corrected from
  "PARTIALLY RESOLVED — Blocker A APPLIED; Blocker B BLOCKED (RE-TYPING
  REQUIRED)" to "BOTH BLOCKERS RESOLVED" with a lifecycle note; and a
  superseded-by block added above the Blocker B section naming TASK-105, with
  both original historical records left byte-for-byte intact. No acceptance
  criterion was weakened, removed, or re-scoped; no contract text was authored.
- `tasks/backlog/TASK-106-close-task-103-blocker-a-bookkeeping.md` — this file:
  Status `BACKLOG` → `IN REVIEW`, and this Completion Evidence.

**No file under `docs/`, `src/`, or `tests/` was changed.**

### Validation Results

- Documentation consistency audit — **PASS.** Each of the ten Blocker A criteria
  was read against the applied text rather than taken from TASK-103's own claim.
  Cross-owner agreement confirmed on the three points where the two owners could
  disagree: the closed-set restatement (§3.2.2 item 2 ↔ `GAME_EVENTS.md` §3
  item 5), the `CardCast` cost member (absent in both — §3.2.20 item 2 ↔
  `GAME_EVENTS.md` §2 item 3), and `effect summary` (one convention governs —
  §3.2.25 ↔ `GAME_EVENTS.md` §2 item 4 and `PassiveTriggered` item 3). No
  duplicate definition and no residual contradiction found.
- Change-isolation check — **PASS.** All four contract documents re-hashed after
  the edits and are byte-identical to pickup: `SIGNALR_PROTOCOL.md` `AE4FD980…`,
  `GAME_EVENTS.md` `454A6F82…`, `COMBAT_RULES.md` `258B12A3…`,
  `GAME_STATE.md` `7004B262…`. The only files whose hashes moved are TASK-103's
  and this one.
- Scope validation (`MVP_SCOPE.md` §1/§2) — **PASS.** Cards and Combat/Status
  Effects are IN; this task adds no system and reaches no OUT or unlisted
  (FUTURE) item. The change is confined to two task files.
- `dotnet build` / `dotnet test` / `npm test` — **N/A and not run**: this task
  changes no code, and its own §Testing Requirements states no test is produced.

### Contract Preservation Verification

```text
SIGNALR_PROTOCOL.md    UNCHANGED (byte-identical; AE4FD980…)
GAME_EVENTS.md         UNCHANGED (byte-identical; 454A6F82…)
COMBAT_RULES.md        UNCHANGED (byte-identical; 258B12A3…)
GAME_STATE.md          UNCHANGED (byte-identical; 7004B262…)
docs/ (all)            UNCHANGED (0 files modified)
docs/03-decisions/     UNCHANGED (0 ADRs)
src/ and tests/        UNCHANGED (byte-identical)
Shield semantics       UNCHANGED (not touched; resolved by TASK-105)
TASK-102 / 104 / 105   UNCHANGED (hashes as recorded at pickup)
TASK-036 / 079 / 099 / tasks/completed/   UNCHANGED
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic introduced
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed no new event, wire member, payload member, or state field
- [x] Confirmed no gameplay semantic introduced and no rule changed

### Unrelated Stale Documentation Discovered (REPORTED, NOT CHANGED)

- **Stale forward reference inside TASK-103's historical Blocker B block.**
  `tasks/backlog/TASK-103-…md` line ~805 reads *"See 'Blocker B — STOP
  CONDITION (STILL OPEN)'"*. Impact: cosmetic only — it sits inside the
  preserved historical record, and the superseded-by block added by TASK-106
  immediately above the Blocker B section now governs a reader's
  interpretation. **Not changed**, deliberately: editing text inside a preserved
  STOP record would defeat the purpose of preserving it. Suggested follow-up:
  none required; if TASK-103 is later closed, its terminal state supersedes the
  wording.
- **TASK-103 does not carry its original pickup hashes for the docs it wrote.**
  Its Completion Evidence records changed files but not the pre-edit SHA256
  baseline TASK-105 and TASK-106 both recorded. Impact: LOW — the applied
  contract state was independently verified by this task. Reported only.
- **Tidal Barrier's Shield magnitude remains unauthored** (`CARD_RULES.md` §4.1)
  — deferred by the Product Owner to a separate gameplay-content decision
  (TASK-104 B-4). Unchanged and still deferred; **no value invented**.
- **Thanh Xà / Sơn Hùng Signature Skill content** remains TBD
  (`PET_RULES.md` §8, `CARD_RULES.md` §4.1) — unrelated, reported only.
- **`BattleHub.cs`'s "intentionally NOT implemented" comment** (L389–391) remains
  accurate and is TASK-102's to correct when it implements the methods —
  reported only, per this task's Implementation Notes.
