# TASK-011 — Resolve Multi-Crossing Cascade Behavior

---

## Metadata

```text
Task ID:           TASK-011
Type:             DOCUMENTATION
Status:           DONE
Risk:             LOW
Priority:         MEDIUM
Primary Agent:    gameplay
Supporting Agents: N/A
Workflow:         documentation/documentation-change.md
Skills:           N/A
Dependencies:     TASK-010 (Partial Reset Contradiction, DONE)
```

---

## Objective

Define deterministic behavior for what happens when a single Cascade
produces enough accumulated progress to cross the Passive Threshold more
than once. Determine PassiveTriggered event count, remaining progress
after the Cascade, and behavior for all three Reset variants (Default,
Partial, NoReset). Update only the authoritative documentation required by
the decision. No code changes. No modification to TASK-009.

---

## Context

TASK-010 established a batch-accumulation model: progress accumulates
across all Matches in a Cascade, Threshold evaluated once after all
Matches resolve. §5 examples show Default Reset (Threshold=3, N=7 →
progress=0) and Partial Reset (Threshold=5, N=7 → progress=2). Both
examples involve exactly one Threshold crossing.

When one Cascade produces enough progress to cross the Threshold more
than once (e.g. Threshold=3, N=7, starting progress=0), the current docs
do not specify whether the Passive triggers once or multiple times. §2.3
and §5 use singular language ("triggers", "evaluated once") but do not
address what happens after a Partial Reset leaves progress ≥ Threshold.

TASK-009's existing implementation uses the old match-by-match model
(multiple triggers per Cascade) and explicitly left Partial Reset
unimplemented. The task before this one (TASK-010) changed the charging
model but did not resolve the multi-crossing question.

---

## Authoritative Sources

- `docs/01-game-design/PASSIVE_RULES.md` §2.3 — batch accumulation,
  "evaluated once", "triggers" (singular)
- `docs/01-game-design/PASSIVE_RULES.md` §4 — Reset Behavior (Default,
  Partial, NoReset)
- `docs/01-game-design/PASSIVE_RULES.md` §5 — cascade examples, "triggers
  once per Cascade resolution"
- `docs/01-game-design/GAME_RULES.md` §10 — "reaching it makes it Ready
  and triggers it", "resets after triggering unless..."
- `docs/02-technical/GAME_STATE.md` §2.3 — PassiveProgress,
  PassiveResetOverride

---

## Scope

### In Scope

- Identify the exact ambiguity with source citations
- Inspect all authoritative Passive rules for evidence of intended
  semantics
- Decide one of two models:
  - **Option A (single trigger):** Passive triggers at most once per
    Cascade regardless of Reset Behavior. Partial Reset overflow carries
    into the *next* Cascade only.
  - **Option B (repeated triggers):** Passive triggers repeatedly within
    one Cascade until progress < Threshold. Each trigger emits a
    PassiveTriggered event and applies its own Reset.
- Define deterministic behavior for:
  - Multiple Threshold crossings within one Cascade
  - PassiveTriggered event count per Cascade
  - Remaining progress after the Cascade for each Reset variant
  - PassiveCharged event semantics under each model
- Update the authoritative documentation to match the decision

### Out of Scope

- Any code change (PassiveTracker, BattleEvent, tests)
- Modification of TASK-009 or TASK-010
- Introduction of new MVP Pets
- Any system listed in `docs/00-overview/MVP_SCOPE.md` §2 (OUT)

---

## Current State

**The Ambiguity (precise):**

§2.3 says: "After all Matches in the Cascade have been counted, the
Threshold is evaluated once. If progress ≥ Threshold, the Passive becomes
Ready and triggers."

§5 says: "In both cases the Passive triggers once per Cascade resolution."

§4 Partial Reset says: "progress reduces by Threshold rather than to 0,
allowing overflow matches from a single big Cascade to carry into the
next charge."

Consider: Threshold=3, starting progress=0, one Cascade produces 7
Matches.

- Progress accumulates: 0 + 7 = 7
- 7 ≥ 3 → Passive triggers
- **Now what?**
  - Default Reset: progress → 0. No further triggers. One event.
  - Partial Reset: progress → 7 − 3 = 4. **4 ≥ 3 still.** Does it
    trigger again? §2.3 says "evaluated once" but doesn't define
    post-reset re-evaluation. §5 says "triggers once" but §4 says
    overflow "carries into the next charge."
  - NoReset: progress stays at 7. **7 ≥ 3 still.** Same question.

**Evidence in the docs:**

| Source | Says | Supports |
|--------|------|----------|
| §2.3 step 3 | "Threshold is evaluated once" | Single trigger |
| §2.3 step 4 | "resets according to Reset Behavior (§4)" | Doesn't address re-evaluation |
| §5 | "triggers once per Cascade resolution" | Single trigger |
| §5 examples | Both show one trigger per Cascade | Single trigger (but only one crossing each) |
| §4.2 Partial Reset | "overflow...carry into the next charge" | "next charge" implies not re-triggering within current Cascade |
| §4.3 NoReset | "rare, must be explicitly justified" | Undefined behavior for multi-crossing |
| GAME_RULES §10.2 | "reaching it makes it Ready and triggers it" | Neutral |
| GAME_RULES §10.3 | "resets after triggering unless...otherwise" | Neutral |

The strongest evidence: §4.2 says overflow carries "into the **next**
charge," not "triggers again within this Cascade." Combined with §2.3
"evaluated once" and §5 "triggers once," this points to Option A.

However, this is an inference, not an explicit statement. The docs do not
directly say "the Passive triggers at most once per Cascade regardless of
Reset Behavior."

---

## Acceptance Criteria

- [ ] The ambiguity is documented with precise source citations
- [ ] A decision is made on Option A or Option B
- [ ] Deterministic behavior is defined for all three Reset variants under
      the chosen model
- [ ] PassiveTriggered event count per Cascade is defined
- [ ] Remaining progress after the Cascade is defined for each Reset
      variant
- [ ] `PASSIVE_RULES.md` §2.3 and/or §5 are updated to match the
      decision
- [ ] `GAME_RULES.md` §10 is verified consistent
- [ ] `GAME_STATE.md` §2.3 is verified consistent
- [ ] All relevant tests pass at the depth required by
      `core/validation.md §2` for the task's Risk level
- [ ] `quality/review.md §1` checklist passes
- [ ] Documentation impact addressed (§ Documentation Impact below)

---

## Affected Areas

```text
[ ] Domain (GameServer.Domain/)
[ ] Application (GameServer.Application/)
[ ] API (GameServer.Api/)
[ ] Client (client/)
[ ] SignalR / Redis (GameServer.Infrastructure/SignalR/, /Redis/)
[ ] PostgreSQL (GameServer.Infrastructure/Postgres/)
[ ] Tests (tests/)
[x] Documentation (docs/)
[ ] ADR (docs/03-decisions/ADR/)
```

---

## Implementation Notes

- This is a documentation-only task. Read the authoritative sources, make
  the decision, update the owning docs.
- `PASSIVE_RULES.md` is the game-design owner (`AGENTS.md §2`).
- The decision must not be based on TASK-009's existing implementation.
  TASK-009 used the old match-by-match model and left Partial Reset
  unimplemented — its behavior is not a source of truth.
- Per `AGENTS.md §7`, if the docs do not contain enough information to
  determine the intended behavior, STOP and report the unresolved decision
  rather than guessing.

---

## Testing Requirements

### Test Types Required

```text
[ ] Unit tests         — N/A (documentation only)
[ ] Integration tests  — N/A
[ ] Gameplay scenarios — N/A
[ ] API tests          — N/A
[ ] Realtime tests     — N/A
[ ] Persistence tests  — N/A
```

### Key Edge Cases

- Threshold=3, N=7, Default Reset → verify one trigger, progress=0
- Threshold=3, N=7, Partial Reset → verify trigger count and remaining
  progress under chosen model
- Threshold=3, N=7, NoReset → verify trigger count and remaining progress
  under chosen model
- Threshold=1, N=1 (minimum crossing)
- Threshold=1, N=5 (maximum overshoot ratio)

---

## Documentation Impact

**Option B — Update existing doc:**

> `docs/01-game-design/PASSIVE_RULES.md` §2.3 and/or §5 must be updated
> to define multi-crossing behavior. Use
> `documentation/documentation-change.md`.

---

## Stop Conditions

- If the authoritative sources do not contain enough information to
  determine the intended behavior (single vs repeated triggers): STOP
  per `AGENTS.md §7` and report the exact unresolved decision
- If the decision requires a gameplay design change beyond documentation
  clarification: STOP per `AGENTS.md §7` and report

---

## Dependencies

- TASK-010 (Partial Reset Contradiction, DONE) — established the batch
  accumulation model that created this ambiguity

---

## Completion Evidence

<!-- TO BE FILLED BY THE AGENT after the task reaches DONE -->

### Summary

Defined deterministic behavior for multi-crossing Cascades: the Passive
triggers at most once per Cascade regardless of Reset Behavior. Any
overflow remaining after reset (Partial Reset) is NOT re-evaluated within
the same Cascade — it waits for the next Cascade. Updated §2.3 (added
"at most once per Cascade" and overflow-waits language) and §5 (added
multi-crossing example with Partial Reset showing progress=4 after reset,
which carries into next charge). §4, GAME_RULES.md §10, and GAME_STATE.md
§2.3 verified consistent without changes.

### Changes

- `docs/01-game-design/PASSIVE_RULES.md` §2.3 — added "at most once per
  Cascade" to trigger language; added step 4 overflow-waits clause;
  updated flow chart to show "at most once" and "overflow waits for
  next Cascade"
- `docs/01-game-design/PASSIVE_RULES.md` §5 — added "at most once" to
  trigger language; added multi-crossing example (Threshold=3, N=7,
  Partial Reset → progress=4, carries into next charge); clarified
  "Overflow (if any) waits for next Cascade"

### Tests

N/A (documentation-only task)

### Documentation Consulted

- `docs/01-game-design/PASSIVE_RULES.md` §1–§8 (full document)
- `docs/01-game-design/GAME_RULES.md` §10 (Passive Rules overview)
- `docs/02-technical/GAME_STATE.md` §2.3 (PetState, PassiveProgress,
  PassiveResetOverride)
- `tasks/completed/TASK-010-resolve-passive-partial-reset-contradiction.md`
  (completion evidence, confirmation of batch model)

### Documentation Changed

- `docs/01-game-design/PASSIVE_RULES.md` §2.3 and §5 (as described in
  Changes above)

### Validation

- §4 Partial Reset text ("overflow...carry into the next charge") is
  consistent — "next charge" means next Cascade, not re-trigger within
  same Cascade
- §4.3 NoReset — progress stays at accumulated value after single trigger;
  no re-evaluation within same Cascade. Consistent.
- GAME_RULES.md §10.2 "reaching it makes it Ready and triggers it" —
  compatible with single trigger (reaching it once triggers it once)
- GAME_RULES.md §10.3 "resets after triggering unless..." — compatible
  (reset happens once after the single trigger)
- GAME_STATE.md §2.3 PassiveProgress — can exceed Threshold and carry
  overflow. Consistent.
- GAME_STATE.md §2.3 PassiveResetOverride — covers Partial Reset and
  NoReset. Consistent.

### Risks

- TASK-009 PassiveTracker was implemented under the old match-by-match
  model (multiple triggers per Cascade) and explicitly left PartialReset
  unimplemented. Its behavior is now incorrect under the batch model.
  A follow-up code task is needed to align the tracker with the
  documented contract.

### Remaining Issues

- TASK-009 PassiveTracker needs code adjustment to match batch model
  (single trigger per Cascade, Partial Reset support). Should be
  verified/rewritten as a follow-up task.

### Agent
AI (opencode)

### Workflow Used
documentation/documentation-change.md

### Skills Used
N/A

### Status
DONE

---

## Handoff

<!-- TO BE FILLED BY THE AGENT if handed off mid-execution -->

<TBD>
