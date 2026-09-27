# TASK-061 — Record the Product Owner's Pet XP Progression Decisions

<!--
  GEN-TASK EXECUTION MANIFEST — PRODUCT-OWNER DECISION-INPUT TASK
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  THIS TASK DECIDES NOTHING AND IMPLEMENTS NOTHING. Its only purpose is to
  capture twelve explicit Product Owner gameplay decisions in §2 below, so
  that TASK-060 can resume and finish the Pet XP contract.

  AN AGENT MUST NOT ANSWER §2. If no Product Owner answer is present, the
  agent reports the task as awaiting input and stops. Choosing, recommending,
  ranking, or defaulting any of these values is the single prohibited action
  of this task.
-->

---

## Metadata

```text
Task ID:           TASK-061
Type:              DOCUMENTATION
Status:            BACKLOG
Risk:              LOW (input capture only — no document is edited, no rule is
                   changed, no code exists in scope; risk rises to MEDIUM only
                   if a recorded decision is later transcribed into docs/,
                   which TASK-060 owns, not this task)
Priority:          HIGH (it is the sole unblocking input for TASK-060, which in
                   turn unblocks the Pet XP implementation task)
Primary Agent:     orchestrator (task-lifecycle / requester coordination —
                   this task records requester input; no domain agent may
                   author the values)
Supporting Agents: N/A (no domain agent may supply or review the VALUES;
                   review is limited to checking that all twelve slots are
                   answered and recorded verbatim)
Workflow:          documentation/documentation-change.md
                   (no docs/ file is edited by this task; the workflow governs
                   the recording discipline — §2 no duplication, §3 canonical
                   owner — and §4 routes the final report through
                   quality/review.md + core/completion.md)
Skills:            discovery/documentation-discovery,
                   quality/scope-validation
                   (2 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-060 (BLOCKED — the downstream contract task these
                   decisions unblock; read as context, AGENTS.md §16, must
                   not be modified by this task),
                   TASK-059 (DONE — established the two-track structure and
                   the Player contract; read-only, must not be modified)
Blocks:            TASK-060 (cannot resume until §2 is answered)
Estimate:          Simple (2 skills; no code, no tests, no document edits;
                   the twelve answers are this task's INPUT, not its output)
```

**Type classification note.** `DOCUMENTATION`, not `GAMEPLAY-CHANGE`.
`tasks/TASK_TYPES.md` §2 types a `GAMEPLAY-CHANGE` as a rule change that is
"propagat[ed] … through implementation and tests"; this task changes no rule
and touches no implementation — it only records requester decisions into a
task file. `TASK_TYPES.md` §3 selects the type by "*what the deliverable is*";
here the deliverable is a recorded decision artifact, and the only file
written is a `tasks/` file. This mirrors the existing precedent for
human-decision capture: the `§2 Human Decisions — Authoritative Input`
sections of TASK-058 and TASK-059, and TASK-060's own §4 decision block.

**This task is NOT a substitute for TASK-060.** It records decisions; it does
not write them into `docs/`. Transcribing the recorded answers into the
authoritative documents (`PET_RULES.md`, `DATABASE.md`, `API_CONTRACTS.md`,
`GAME_EVENTS.md`) remains TASK-060's deliverable, per
`documentation/documentation-change.md` §3 (canonical owner).
**Do not bypass TASK-060.**

**Status note.** `BACKLOG`, not `BLOCKED`: per `tasks/README.md` §6 the file
lives in `backlog/`. An unanswered §2 is this task's normal starting state —
it is what the task exists to collect — so it is not a blocker under
`TASK_LIFECYCLE.md` §3. The task completes when §2 is answered and §5 records
the answers; it moves to `BLOCKED` only under §7 (e.g. the task-generation
workflow is found not to permit a decision-input task).

---

## 1. Objective

Obtain and record the **Product Owner's explicit decisions** for the twelve
unresolved Pet XP progression/reward questions that TASK-060 requires, so
that TASK-060 can resume and complete the Pet XP contract.

This task **collects and records only**. It does not choose, recommend, rank,
default, or implement any value, and it edits no authoritative document.

---

## 2. Product Owner Decisions Required

> **AWAITING PRODUCT OWNER INPUT — execution attempted, no decisions supplied.**
>
> Execution of this task was attempted and stopped at this section: none of
> the twelve `Decision:` fields carries a Product Owner answer. Per §7's
> first condition this is the task's **normal starting state, not a failure** —
> no stop condition has fired and no lifecycle transition applies. The task
> remains `BACKLOG` in `backlog/`.
>
> **What is needed:** the Product Owner fills in the twelve `Decision:` fields
> below. An agent must not fill them in (`AGENTS.md` §7, `GAME_RULES.md` §20).
>
> **Verification performed at execution time (see §11):** all twelve slots
> still read `<PENDING PRODUCT-OWNER DECISION>`; no authoritative document,
> ADR, task, or repository file supplies any of them; no Pet XP numeric value
> exists anywhere in `docs/` or `tasks/`; TASK-060 is still `BLOCKED`.

> **PRODUCT OWNER INPUT.** The twelve items below are gameplay/product
> decisions. Only the Product Owner may supply them. An agent executing this
> task must not fill in any `Decision:` field.
>
> Answer each item independently. Items 5–8 jointly define one coherent
> battle reward rule — read them together (§4).

### A — Persistence / Initial State

**1. Pet XP persistence semantics**

```text
Decision:
<PENDING PRODUCT-OWNER DECISION>

Rationale (optional):
<PENDING PRODUCT-OWNER DECISION>
```

**2. Initial Pet XP**

```text
Decision:
<PENDING PRODUCT-OWNER DECISION>

Rationale (optional):
<PENDING PRODUCT-OWNER DECISION>
```

**3. Initial Pet Level**

```text
Decision:
<PENDING PRODUCT-OWNER DECISION>

Rationale (optional):
<PENDING PRODUCT-OWNER DECISION>
```

**4. Pet Level range**

```text
Decision:
<PENDING PRODUCT-OWNER DECISION>

Rationale (optional):
<PENDING PRODUCT-OWNER DECISION>
```

### B — Battle Reward Targeting (items 5–8 are one coherent rule — see §4)

**5. Pet XP awarded per BattleWon**

```text
Decision:
<PENDING PRODUCT-OWNER DECISION>

Rationale (optional):
<PENDING PRODUCT-OWNER DECISION>
```

**6. Pet XP awarded on BattleLost**

```text
Decision:
<PENDING PRODUCT-OWNER DECISION>

Rationale (optional):
<PENDING PRODUCT-OWNER DECISION>
```

**7. Whether only the active combat Pet receives XP**

```text
Decision:
<PENDING PRODUCT-OWNER DECISION>

Rationale (optional):
<PENDING PRODUCT-OWNER DECISION>
```

**8. What happens to inactive owned Pets**

```text
Decision:
<PENDING PRODUCT-OWNER DECISION>

Rationale (optional):
<PENDING PRODUCT-OWNER DECISION>
```

### C — Progression

**9. Pet XP → Pet Level formula**

```text
Decision:
<PENDING PRODUCT-OWNER DECISION>

Rationale (optional):
<PENDING PRODUCT-OWNER DECISION>
```

### D — Relationship With the Player Track

**10. Whether Pet XP continues accumulating after Pet Level 50**

```text
Decision:
<PENDING PRODUCT-OWNER DECISION>

Rationale (optional):
<PENDING PRODUCT-OWNER DECISION>
```

**11. Whether Pet XP uses the same curve as Player XP**

```text
Decision:
<PENDING PRODUCT-OWNER DECISION>

Rationale (optional):
<PENDING PRODUCT-OWNER DECISION>
```

**12. Whether Pet XP reward amount equals Player XP reward amount**

```text
Decision:
<PENDING PRODUCT-OWNER DECISION>

Rationale (optional):
<PENDING PRODUCT-OWNER DECISION>
```

---

## 3. Product Owner Decision Boundary

The Product Owner supplies the gameplay decisions. The executing agent must
**NOT**:

```text
choose a value                    recommend a value
rank the options                  copy Player XP values
infer from TASK-024                infer from PetLevelMultiplier
infer from existing source code    use implementation convenience
use a "reasonable default"         convert an unresolved item into a
                                   configuration decision
```

Answers may be recorded **verbatim**, including a deliberate "not yet
decided" — but a deferred answer must be recorded explicitly as deferred. It
must not be left reading `<PENDING PRODUCT-OWNER DECISION>` while the task is
called complete (see §6).

### 3.1 The Player XP contract is context only — NOT a default

Provided solely so the Product Owner can see what already exists. It is
**not** a template, precedent, or default for Pet XP, and this task must not
modify it.

```text
Player XP (already decided — COMBAT_RULES.md §7):
BattleWon   → +100 Player XP
BattleLost  → +0 Player XP
Player.Level = min(floor(Player.XP / 100) + 1, 50)
Player XP uncapped; Player Level capped at 50
Initial Player XP = 0; initial Player Level = 1
Player Level range = [1, 50]
```

```text
These values are NOT defaults for Pet XP.
```

A Pet answer that happens to equal a Player value is only valid if the
Product Owner says so explicitly for the Pet track. The Pet reward amount and
the Pet curve constant are **two independent decisions** (items 5–6 vs item
9); the Player track's choice of `100` for both is not a reason for the Pet
track to do the same.

### 3.2 `PetLevelMultiplier` is RETIRED and is not an option

```text
PetLevelMultiplier = RETIRED
```

The Product Owner is deciding **Pet XP progression** — not whether to restore
the retired `Player.Level × PetLevelMultiplier` model. Do not present
`PetLevelMultiplier` as a candidate option, do not reinterpret it as an XP
multiplier, reward multiplier, curve multiplier, scaling factor, or balancing
parameter, and do not revive it in any form.

---

## 4. Reward Targeting Must Be Decided as One Coherent Rule

Items 5–8 are not four independent settings. Together they define **one**
battle reward-targeting model, and the Product Owner must be able to state it
end-to-end:

```text
BattleWon
    ↓
Pet XP reward            ← item 5
    ↓
recipient                ← item 7 (+ item 8 for everyone else)
```

```text
BattleLost
    ↓
Pet XP reward            ← item 6
    ↓
recipient                ← item 7 (+ item 8 for everyone else)
```

The answers must jointly establish:

```text
what a BattleWon grants
what a BattleLost grants
which Pet(s) receive it
what happens to owned Pets that are not targeted
whether one battle can grant Pet XP to more than one Pet
```

**Do not assume active-Pet-only behavior.** The existence of an active combat
Pet (`PET_RULES.md` §2 item 2) is not a reward-targeting rule. **Do not
assume all owned Pets receive XP.**

---

## 5. Recording Requirement

When the Product Owner has answered §2, record the closure here:

```text
Answered by:        <Product Owner>
Date recorded:      <date>
Answers recorded:   <1–12, verbatim from §2>
Any item deferred:  <item numbers + the Product Owner's stated reason, or
                     "none">
```

Answers must be recorded **verbatim**. The executing agent may fix only
formatting, never wording or values.

---

## 6. Acceptance Criteria

All binary; each cites the section that must contain it.

- [ ] All twelve decision slots exist and are individually listed (§2)
- [ ] Each slot has a clear Product Owner input field (`Decision:` +
      optional `Rationale:`) (§2)
- [ ] No Pet XP value is preselected anywhere in this task
- [ ] Player XP values are explicitly marked as **not** defaults for Pet XP
      (§3.1)
- [ ] `PetLevelMultiplier` is explicitly recorded as RETIRED and is not
      presented as an option (§3.2)
- [ ] Reward-targeting items 5–8 are presented as one coherent model (§4)
- [ ] Every one of the twelve items has a recorded Product Owner answer, or
      is explicitly recorded as deferred with the Product Owner's reason
- [ ] Answers are recorded verbatim; no value or wording was altered
- [ ] §5 closure record is completed
- [ ] TASK-060 is identified as the downstream contract task (§1, §7)
- [ ] No implementation work is included anywhere in this task
- [ ] No authoritative document (`docs/`) was modified by this task
- [ ] No completed task was modified; TASK-060 and TASK-059 are unmodified
- [ ] No gameplay decision was made by the agent (§3)
- [ ] No source code, test, or migration was modified
- [ ] Quality review checklist passes for the documentation items only
      (`quality/review.md` §1)

---

## 7. Stop Conditions

Universal stop conditions (`AGENTS.md` §20, `.ai/README.md` §13) always
apply. Task-specific conditions:

- **If §2 has no Product Owner answers** → this is the task's normal
  starting state, not a failure: report the task as **awaiting Product Owner
  input** and STOP. Do not answer it. Do not partially answer it.
- **If an agent is about to fill in any `Decision:` field** → STOP. That is
  the single prohibited action of this task (`AGENTS.md` §7,
  `GAME_RULES.md` §20).
- **If the answers would require choosing gameplay values to proceed** →
  STOP; report what is missing.
- **If the task-generation workflow is found not to permit a Product-Owner
  decision-input task** → STOP and report.
- **If the next task ID cannot be determined** → STOP.
- **If TASK-060 has been completed, materially changed, or is no longer
  BLOCKED on these decisions** → STOP; report the current state instead of
  proceeding.
- **If another authoritative source has already supplied the twelve
  decisions** → STOP; do not duplicate them. Report where they exist and
  whether TASK-060 should instead resume directly.
- **If recording the answers would require editing an authoritative
  document** → STOP; that is TASK-060's deliverable, not this task's.
- **If a Pet answer would reintroduce `Player.Level × PetLevelMultiplier`**
  → STOP; that model is retired (`PET_RULES.md` §5.3, `ADR-016` item 13).
- **If a recorded answer would change the Player XP contract** → STOP;
  §3.1 is frozen by TASK-059 and out of this task's authority.
- **If a recorded answer would require an `MVP_SCOPE.md` §2 OUT item** →
  STOP (`AGENTS.md` §8).
- **If completing this task would require source code, tests, migrations,
  or a schema change** → STOP; this task is input capture only.
- **If this task is asked to also transcribe the answers into `docs/`** →
  STOP; that is TASK-060's scope, and this task must not bypass it.
- **If the task exceeds 7 skills or crosses multiple uncoupled
  architectural boundaries** → STOP & decompose.

When stopped, report the exact condition and **do not invent a resolution**.

---

## 8. Relationship to TASK-060 and the Implementation Task

```text
TASK-059 (DONE)   two-track structure + full Player contract
        ↓
TASK-061 (this task)   records the Product Owner's twelve Pet XP decisions
        ↓
TASK-060 (BLOCKED)     resumes on those decisions; writes the Pet XP contract
                       into PET_RULES.md / DATABASE.md / API_CONTRACTS.md /
                       GAME_EVENTS.md
        ↓
Pet XP contract becomes deterministic
        ↓
Implementation-readiness audit
        ↓
Pet XP implementation task (NEW ID — NOT created here)
```

- **This task supplies the missing Product Owner input required by
  TASK-060.** It does not replace or bypass TASK-060.
- **TASK-060 is not modified by this task.** Its status change
  (`BLOCKED → IN PROGRESS`) is a TASK-060 lifecycle action, performed when
  TASK-060 resumes — not by this task (`TASK_LIFECYCLE.md` §3).
- **Do not create the Pet XP implementation task from this task**, and do not
  create it from TASK-060 either — TASK-060 §12 forbids it until its own
  contract is complete.

---

## 9. Scope

### In Scope

1. Collecting an explicit Product Owner answer for each of the twelve items
   in §2.
2. Recording those answers verbatim, with rationale preserved where the
   Product Owner supplies it.
3. Completing the §5 closure record.
4. Linking the recorded answers to TASK-060 as its unblocking input.

### Out of Scope

```text
source code · tests · migrations · database changes · schema changes ·
Redis · SignalR · Phaser · React · Match-3 · combat ·
RewardSummary implementation · Pet XP implementation · Pet XP calculation ·
Pet Level calculation · reward application · reward persistence ·
Player XP changes · Pet combat · Pet evolution · tier · star ·
passive · skill · relic · card · gacha · quests · energy · prestige ·
balance recommendations · ranking of options · choosing any value ·
architecture changes · creating an ADR (that is TASK-060's §9 decision,
after the decisions are supplied) · writing the decisions into docs/
(TASK-060's deliverable) · creating the Pet XP implementation task
```

Also out of scope: modifying TASK-060, TASK-059, TASK-024, or any completed
task (`AGENTS.md` §16).

---

## 10. Authoritative References

- `tasks/blocked/TASK-060-*` §3–§4, §9, §14 — the twelve decisions, their
  exact wording, and the STOP CONDITION this task resolves
- `docs/01-game-design/PET_RULES.md` §5.1 (ownership structure — not
  reopenable), §5.2 (the same twelve items, the single enumeration),
  §5.3 (`PetLevelMultiplier` RETIRED), §2 item 2 (active Pet selection)
- `docs/01-game-design/COMBAT_RULES.md` §7 — the Player contract (context
  only); §7.3 (reward amount vs. curve constant are independent concepts)
- `docs/01-game-design/GAME_RULES.md` §20 (Rule Change Policy — only a human
  decision makes a proposed mechanic authoritative), §21 (hierarchy)
- `docs/03-decisions/ADR/ADR-016-*` items 9–14 — the Pet structure decisions
  and the explicit deferral of these twelve
- `docs/02-technical/DATABASE.md` §1/§3 — where the decisions will land
- `docs/00-overview/MVP_SCOPE.md` §1/§2/§4 — scope boundary for any answer
- `AGENTS.md` §7, §8, §16, §20; `tasks/TASK_LIFECYCLE.md` §3;
  `tasks/TASK_TYPES.md` §2–§3; `tasks/README.md` §6, §9, §11

Historical context only — **read, never cite as a Pet XP source, never
modify**: `tasks/completed/TASK-024-*`.

---

## 11. Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after the Product Owner answers §2.
  Keep concise and factual.
-->

### Changed Files
- `tasks/backlog/TASK-061-record-product-owner-pet-xp-decisions.md` — this
  execution added the §2 "AWAITING PRODUCT OWNER INPUT" notice and this §11
  record. **No decision field was filled in.**

### Decisions Recorded
None. All twelve remain `<PENDING PRODUCT-OWNER DECISION>`.

- `item 1` — NOT SUPPLIED — awaiting Product Owner
- `item 2` — NOT SUPPLIED — awaiting Product Owner
- `item 3` — NOT SUPPLIED — awaiting Product Owner
- `item 4` — NOT SUPPLIED — awaiting Product Owner
- `item 5` — NOT SUPPLIED — awaiting Product Owner
- `item 6` — NOT SUPPLIED — awaiting Product Owner
- `item 7` — NOT SUPPLIED — awaiting Product Owner
- `item 8` — NOT SUPPLIED — awaiting Product Owner
- `item 9` — NOT SUPPLIED — awaiting Product Owner
- `item 10` — NOT SUPPLIED — awaiting Product Owner
- `item 11` — NOT SUPPLIED — awaiting Product Owner
- `item 12` — NOT SUPPLIED — awaiting Product Owner

**This task is NOT DONE.** §2 is unanswered, so §6's acceptance criteria are
not met and §15 completion criteria are not met. No value was chosen,
recommended, inferred, defaulted, or copied.

### Validation Results
- `twelve §2 Decision slots still PENDING` — PASS (24 PENDING placeholder
  lines = 12 `Decision:` + 12 `Rationale:` fields; 0 answered)
- `no supplied Product Owner answers anywhere in the repository` — PASS
  (searched `docs/`, `tasks/`, `.ai/`; the only pattern hits were unrelated
  pre-existing text in TASK-035 about `DATABASE.md`)
- `no Pet XP numeric value exists in docs/ or tasks/` — PASS (0 hits)
- `TASK-060 still BLOCKED` — PASS
- `no docs/ file modified` — PASS
- `no source / test / migration file modified` — PASS

### Final Report (required format)

```text
## Status
AWAITING PRODUCT OWNER INPUT
(not DONE — §2 unanswered; no stop condition fired, so not BLOCKED either)

## Summary
Execution of TASK-061 was attempted. The task's twelve Product Owner
decision slots are all still <PENDING PRODUCT-OWNER DECISION>, and no
authoritative source anywhere in the repository supplies any of them.
Per §7's first condition this is the task's normal starting state. The
agent recorded the awaiting-input notice and stopped without deciding
anything.

## Answers Recorded
None yet — awaiting Product Owner.

## Deferred Items
None. (Nothing was deferred by the Product Owner; the items are simply
not yet supplied.)

## What TASK-060 Can Now Do
Nothing yet. TASK-060 remains BLOCKED on all twelve of its §4 items
(§5.2 items 1–12 in PET_RULES.md): persistence, initial XP, initial Level,
Level range, BattleWon reward, BattleLost reward, reward recipient,
inactive-Pet behavior, XP → Level formula, post-Level-50 XP, curve
relationship, reward-amount relationship.

## Documentation Changed
None — this task edits no authoritative document.

## Untouched
TASK-060 · TASK-059 · TASK-024 · completed tasks · docs/ · src/ · tests/

## Source Code
No source code changed.

## Tests
No tests run; no implementation validation applies to input capture.

## Next Step
The Product Owner supplies the twelve decisions in TASK-061 §2; then
TASK-060 resumes. Do not create the implementation task.
```

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (no code written)
- [x] Confirmed no authoritative document was modified
- [x] Confirmed no `PlayerAttack`/`PlayerDefense`/`PlayerHP`/`PlayerCrit`/
      `PlayerPower` or equivalent introduced
- [x] Confirmed no Prestige, Paragon, Season XP, Evolution, or other extra
      progression system introduced
- [x] Confirmed no Player XP value was copied into a Pet answer (no Pet
      answer exists at all)
