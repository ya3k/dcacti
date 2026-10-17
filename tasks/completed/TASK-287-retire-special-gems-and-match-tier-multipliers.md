# TASK-287 — Retire Special Gems and Match-Tier Multipliers from the Game-Design Documents

---

## Metadata

```text
Task ID:           TASK-287 (highest existing ID on disk is TASK-286)
Type:              DOCUMENTATION
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     review
Supporting Agents: gameplay, testing
Workflow:          documentation/documentation-change.md
Skills:            quality/documentation-consistency, gameplay/gameplay-behavior-derivation,
                   discovery/documentation-discovery, quality/implementation-review,
                   testing/test-scenario-generation
Dependencies:      OD-1…OD-7 (operator-approved, recorded in TASK-286);
                   OD-3/OD-4 owner of the retired rates; TASK-286 (READY) — JOIN
                   GROUP COMMIT PARTNER, not a blocker to authoring
Declared Files:    docs/01-game-design/GAME_RULES.md,
                   docs/01-game-design/MATCH3_RULES.md,
                   docs/01-game-design/COMBAT_RULES.md,
                   docs/01-game-design/PASSIVE_RULES.md
```

<!--
  RISK DEVIATION (disclosed). tasks/TASK_TYPES.md §4 gives DOCUMENTATION a
  LOW–MEDIUM baseline. HIGH is selected because this task retires a game rule
  statement owned by GAME_RULES.md (the highest-precedence game-design document),
  retires MATCH3_RULES.md §5 wholesale, and removes the match-tier rate table that
  COMBAT_RULES.md calls its sole source of truth for match-tier output. Per
  tasks/TASK_TYPES.md §4 and .ai/workflow/core/task-intake.md §3, HIGH is correct
  when in doubt for a game-rule change.

  CREATED UNDER B-3 (approved). The operator approved re-amending TASK-286 under
  its existing ownership "with COMBAT_RULES.md and MATCH3_RULES.md handled via the
  required join group commit with a new documentation task". This is that task.
-->

---

## Objective

Retire every remaining game-design statement that asserts the Special Gem model
(creation, activation, chain activation, AoE clearing) and every match-tier
resource multiplier, so that `docs/01-game-design/` states exactly one contract:
the OD-1…OD-7 model whose combat-side half is already written in
`COMBAT_RULES.md` §2 item 6 and `MATCH3_RULES.md` §5.7 item 8 by TASK-286.

This task is documentation only. It changes no `src/`, no `tests/`, and no
`docs/02-technical/` file.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — confirm the retirement is a reduction and
  that `Match 3 / Match 4 / Match 5` and `L/T matches` stay IN as **matches**;
  `MVP_SCOPE.md` itself must not be edited
- `docs/01-game-design/GAME_RULES.md` §3 item 4, §6, §7, §17 step 15 —
  this task's slice
- `docs/01-game-design/MATCH3_RULES.md` §1.A, §4.1, §4.5 item 7, §5 (whole),
  §5.7 items 1–7, §6.3.1, §7.2 — this task's slice; §5.7 item 8 is
  **TASK-286's** and must not be re-edited here
- `docs/01-game-design/COMBAT_RULES.md` §2 table, the "scope of the tier
  multiplier" paragraph, §2 items 1–4, §2 preamble gate wording — this task's
  slice; §2 item 6 is **TASK-286's** and must not be re-edited here
- `docs/01-game-design/PASSIVE_RULES.md` §2 item 2 — this task's slice
- `docs/03-decisions/ADR/ADR-026` D1, item 4 — the gating and ownership rules that
  survive unchanged
- `tasks/TASK_LIFECYCLE.md` §6.1 item 5, §6.2 (P-2, P-5) — the join group commit
- `tasks/backlog/TASK-286-…md` — the OD-1…OD-7 record and this task's join partner

---

## Scope

### In Scope

- `GAME_RULES.md` §3 item 4: delete the "may generate Special Gems" clause; state
  that Match 4 / Match 5 / L-T are detected and counted as single Matches carrying
  no additional board object and no additional rate.
- `GAME_RULES.md` §6: drop the "match-tier multipliers" pointer.
- `GAME_RULES.md` §7 "Special Gem Rules": retire the section.
- `GAME_RULES.md` §17 step 15: gate wording becomes the confirmed ATK-Match
  predicate rather than the cleared-gem count.
- `MATCH3_RULES.md` §1.A and §4.1: remove the Special Gems stage and its
  reservation language; the pass is Match Resolution → Gravity → Spawn → Re-detect.
- `MATCH3_RULES.md` §4.5 item 7: simplify the "Spawn never creates a Special Gem"
  clause.
- `MATCH3_RULES.md` §5 (§5.1–§5.9 inclusive): retire in full behind a single
  retirement statement, including §5.1.1, §5.2, §5.3, §5.4, §5.5, §5.5.1–§5.5.5,
  §5.6, §5.8, §5.9.
- `MATCH3_RULES.md` §5.7 items 1–7: reshape to "matches only, no tier".
- `MATCH3_RULES.md` §6.3.1 and the activation item of §7.2: retire.
- `COMBAT_RULES.md` §2: delete the match-tier multiplier table, the "scope of the
  tier multiplier" paragraph, and §2 items 1–4; correct the §2 preamble gate
  wording to the confirmed ATK-Match predicate.
- `PASSIVE_RULES.md` §2 item 2: retire the detonation clause.
- Version headers of all four documents to describe the content actually present.

### Out of Scope

- Any `docs/02-technical/` document, any ADR, `docs/03-decisions/README.md`
  (owned by a not-yet-created technical-documentation task).
- `MVP_SCOPE.md`.
- `COMBAT_RULES.md` §2 item 6 and `MATCH3_RULES.md` §5.7 item 8 — **TASK-286 owns
  them**; re-editing them here would duplicate ownership.
- Any `src/` or `tests/` file.
- Any task-record lifecycle transition.
- Staging, committing, pushing, deploying, or touching Redis.

---

## Current State

TASK-286 has already rewritten `COMBAT_RULES.md` §2 item 6 (v2.8) and
`MATCH3_RULES.md` §5.7 item 8 (v1.11) to the OD-1…OD-7 contract, and both documents
now disclose that they are **not self-consistent** until this task's slice lands.
Every other Special Gem and match-tier statement listed under Scope is still
present and still asserts the retired model.

---

## Declared File Set (P-2 / P-5)

```text
docs/01-game-design/GAME_RULES.md
docs/01-game-design/MATCH3_RULES.md
docs/01-game-design/COMBAT_RULES.md
docs/01-game-design/PASSIVE_RULES.md
```

*(The task record itself is excluded from the declared set — `tasks/TASK_TEMPLATE.md`.)*

---

## Shared-File Join Commit Requirement

`MATCH3_RULES.md` and `COMBAT_RULES.md` are **shared** with TASK-286, which is
currently `READY` in `tasks/backlog/`. Under `tasks/TASK_LIFECYCLE.md` §6.1 item 5
and P-2:

```text
Neither task's slice may be committed separately. The two slices are committed
together in ONE join group commit whose body carries:

    owns: TASK-286, TASK-287
    files: docs/01-game-design/COMBAT_RULES.md,
           docs/01-game-design/MATCH3_RULES.md,
           docs/01-game-design/GAME_RULES.md,
           docs/01-game-design/PASSIVE_RULES.md,
           tests/backend/GameServer.Domain.Tests/ResourceGenerationTests.cs
    shared with: TASK-286 (COMBAT_RULES.md, MATCH3_RULES.md)

Precondition (P-2/P-3): BOTH records must hold an authorized active manifest in
tasks/active/ and have passed quality/review.md. TASK-286 is currently READY in
tasks/backlog/, so this precondition is NOT met yet.
```

---

## Acceptance Criteria

- [x] AC-1 No live statement in `docs/01-game-design/` asserts a Special Gem, its
      creation, its activation, its chain activation, or an area clear.
- [x] AC-2 No live statement in `docs/01-game-design/` asserts a match-tier
      resource multiplier (Match 3 / Match 4 / Match 5 / L-T).
- [x] AC-3 `MATCH3_RULES.md` §5 is retired in one explicit statement and contains
      no orphaned sub-rules or dangling cross-references.
- [x] AC-4 `COMBAT_RULES.md` §2 keeps its base-output table (rates) and no longer
      contains the multiplier table, the tier-scope paragraph, or §2 items 1–4.
- [x] AC-5 `GAME_RULES.md` §3 item 4, §6, §7, §17 step 15 reflect OD-1…OD-7, and
      `GAME_RULES.md` §16's event list is unchanged (no Special Gem event ever
      existed, so none is removed).
- [x] AC-6 `PASSIVE_RULES.md` §2 item 2 no longer references a detonation.
- [x] AC-7 Every version header describes the content actually present.
- [x] AC-8 `MVP_SCOPE.md` is attested **unchanged**, with the reduction reasoning
      recorded in this record.
- [x] AC-9 No `src/`, `tests/`, or `docs/02-technical/` file is modified by this task slice.
- [x] AC-10 The join precondition is verified before staging, committed in joint group commit with TASK-286.
- [x] AC-11 LF-only, zero CRLF, in all four documents.
- [x] AC-12 Quality review checklist passes (`.ai/workflow/quality/review.md` §1).
- [x] AC-13 No authoritative rules or contracts violated; no new source-of-truth
      conflict introduced (`AGENTS.md` §4).

---

## Affected Files & Areas

```text
[ ] src/backend/            NOT TOUCHED
[ ] src/frontend/client/    NOT TOUCHED
[ ] tests/                  NOT TOUCHED
[x] docs/01-game-design/    GAME_RULES.md, MATCH3_RULES.md, COMBAT_RULES.md,
                            PASSIVE_RULES.md
```

---

## Implementation Notes

- `MATCH3_RULES.md` §5 is 364 reference-dense lines and carries the document's
  "NEW DESIGN DECISION" recovery history. Retire it with a single, explicit
  retirement statement rather than deleting silently: the history that those rules
  were newly authored (not recovered) is itself a record the document keeps.
- Do not renumber `MATCH3_RULES.md` sections; cite-by-section references exist
  across `docs/` and the task records.
- `MATCH3_RULES.md` §5.7 item 8 and `COMBAT_RULES.md` §2 item 6 must be left
  exactly as TASK-286 wrote them; this task supplies the sections around them.
- `GAME_RULES.md` §16's event list is unchanged: no `SpecialGemActivated` or
  `SpecialGemCreated` event ever existed.

---

## Testing Requirements

### Required Verification

```text
[x] git diff --check                  clean on docs/
[x] Reference sweep                   zero live Special Gem / tier-multiplier
                                      assertions in docs/01-game-design/
[x] Line endings                      LF only, zero CRLF
[x] Unit / integration / E2E tests    all backend and frontend test suites pass
```

### Key Edge Cases

- A cross-reference pointing at a retired section must be re-pointed or removed;
  a dangling `§5.x` reference is a defect (`AGENTS.md` §4).
- `GAME_RULES.md` is the highest-precedence game-design document; its §7
  retirement is the gate that makes the rest coherent.

---

## Stop Conditions

- If `TASK-286`'s active-manifest precondition is unmet at staging time: STOP
  rather than committing alone (`tasks/TASK_LIFECYCLE.md` §6.2 P-2/P-3).
- If a retirement requires touching a `docs/02-technical/` document: STOP — that is
  a different task.
- If a reviewer finds an authoritative rule conflict: STOP (`AGENTS.md` §4).
- If any command would stage, commit, stash, reset, restore, or clean: STOP.

---

## Completion Evidence

**Completed.** `Status: DONE`. Phase A implementation slice committed and Phase B completion record filed.

```text
Phase A implementation slice   9fd779a3ca90b3e53cb6e488031304464f448a8e
Phase B completion record      tasks/completed/TASK-287-retire-special-gems-and-match-tier-multipliers.md
Owns                           TASK-287 (joint group commit with TASK-286)
Files                          docs/01-game-design/GAME_RULES.md
                               docs/01-game-design/MATCH3_RULES.md
                               docs/01-game-design/COMBAT_RULES.md
                               docs/01-game-design/PASSIVE_RULES.md
Shared with                    TASK-286 (COMBAT_RULES.md, MATCH3_RULES.md)
                               → join group commit
Unowned / pre-existing         none
```
