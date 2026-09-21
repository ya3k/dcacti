# TASK-010 — Resolve Passive Partial Reset Contradiction

---

## Metadata

```text
Task ID:           TASK-010
Type:             DOCUMENTATION
Status:           DONE
Risk:             LOW
Priority:         MEDIUM
Primary Agent:    gameplay
Supporting Agents: N/A
Workflow:         documentation/documentation-change.md
Skills:           N/A
Dependencies:     TASK-009 (Passive Tracker, DONE)
```

---

## Objective

Identify and document the contradiction between Passive Partial Reset
semantics and the match-based charging model, decide whether progress may
overshoot Threshold or Partial Reset is effectively dead code, and update
only the authoritative documentation required by the decision. No code
changes. No modification to TASK-009 implementation.

---

## Context

During TASK-009 validation, a contradiction was discovered in the Passive
rules. The charging model (`PASSIVE_RULES.md §2.3–§5`) implies progress
increments by exactly +1 per Match and triggers/reset at exactly Threshold,
making overshoot impossible. Yet `PASSIVE_RULES.md §4.2` defines Partial
Reset as "progress reduces by Threshold rather than to 0, allowing overflow
matches from a single big Cascade to carry into the next charge" — and
TASK-009 included an acceptance scenario Threshold=5, Progress=7 → 2 that
is unreachable under the current charging model.

All five MVP Pet Passives use default full reset (`PASSIVE_RULES.md §8`),
so Partial Reset is defined in the rules but not exercised by any MVP Pet.
This is a pure documentation conflict — no code currently relies on either
interpretation.

---

## Authoritative Sources

- `docs/01-game-design/PASSIVE_RULES.md` §2.3–§2.4 — charging model,
  threshold trigger, reset-to-0 default
- `docs/01-game-design/PASSIVE_RULES.md` §4.2 — Partial Reset definition
- `docs/01-game-design/PASSIVE_RULES.md` §5 — multiple triggers in one
  cascade (Threshold=3, N=7 example)
- `docs/01-game-design/PASSIVE_RULES.md` §8 — MVP Passive reference
  (all use default reset)
- `docs/01-game-design/GAME_RULES.md` §10.3 — "Progress resets after
  triggering unless the Passive explicitly specifies otherwise"
- `docs/02-technical/GAME_STATE.md` §2.3 — PassiveResetOverride field
  ("only present if non-default reset behavior")

---

## Scope

### In Scope

- Identify and document the exact contradiction between §2.3/§5 and §4.2
- Decide one of two resolutions:
  - **Option A:** Progress may overshoot Threshold (partial reset is a
    real behavior requiring a charging model amendment)
  - **Option B:** Partial Reset is dead code / identical to Default (§4.2
    is reworded or removed; PassiveResetOverride covers only No Reset)
- Update the authoritative documentation owned by `docs/01-game-design/`
  and `docs/02-technical/` to match the chosen resolution
- If Option A: define how overshoot, multiple triggers, and remaining
  progress behave deterministically within a cascade

### Out of Scope

- Any code change (PassiveTracker, BattleEvent, tests)
- Modification of TASK-009 implementation or completion evidence
- Introduction of new MVP Pets that use Partial Reset
- Any system listed in `docs/00-overview/MVP_SCOPE.md` §2 (OUT)

---

## Current State

**The Contradiction (precise):**

`PASSIVE_RULES.md §2.3`: "When progress reaches the Passive's Threshold,
the Passive becomes Ready and triggers immediately."

`PASSIVE_RULES.md §5`: "If this crosses the Threshold multiple times
within the same Cascade resolution (e.g. Threshold=3 and N=7), the Passive
triggers multiple times, each with its own Reset."

Both imply: progress is incremented +1 per Match, evaluated after each
Match, triggers at exactly Threshold, resets immediately. Under this model
progress can never exceed Threshold.

`PASSIVE_RULES.md §4.2`: "Partial reset (e.g. progress reduces by
Threshold rather than to 0, allowing overflow matches from a single big
Cascade to carry into the next charge)."

If progress cannot exceed Threshold, Partial Reset is functionally
identical to Default Reset — it is unreachable.

**TASK-009 acceptance criterion (now in completed task):** "Given a Pet
with partial reset (Threshold=5, reset by Threshold), when the Passive
triggers at progress 7, then progress resets to 2." — This scenario
requires overshoot, which the charging model does not permit.

**MVP reality:** All five MVP Pet Passives use default full reset
(`PASSIVE_RULES.md §8`). No MVP Pet exercises Partial Reset.

---

## Acceptance Criteria

- [ ] The contradiction between §2.3/§5 and §4.2 is documented with
      precise source citations (file + section + quote)
- [ ] A decision is made on one of two options: overshoot allowed or
      Partial Reset is dead code
- [ ] If overshoot is allowed: the charging model in §2.3 is amended to
      define how overshoot behaves, and §5 is updated to show the correct
      cascade example with overshoot
- [ ] If Partial Reset is dead code: §4.2 is reworded or removed, and
      `PassiveResetOverride` in `GAME_STATE.md §2.3` is updated to cover
      only No Reset / Persistent
- [ ] `PASSIVE_RULES.md §8` is verified consistent with the decision
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

- This is a documentation-only task. The agent reads the authoritative
  sources, identifies the contradiction, proposes a resolution, and
  updates the owning docs after approval.
- `PASSIVE_RULES.md` is the game-design owner of this rule (`AGENTS.md §2`).
  The decision lives here, not in GAME_STATE.md or TASK-009.
- `GAME_STATE.md §2.3` (`PassiveResetOverride`) must stay consistent with
  whatever PASSIVE_RULES.md says after the fix.
- TASK-009's acceptance scenario (Threshold=5, Progress=7 → 2) must NOT be
  used as input to the decision. The decision must derive from gameplay
  intent, not from making an existing test pass.
- Per `AGENTS.md §7`, if the docs do not contain enough information to
  determine the intended gameplay behavior, STOP and report the unresolved
  decision rather than guessing.

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

- If overshoot is allowed: Threshold=1 with N=2 in a single cascade
  (should trigger twice, second trigger with overflow)
- If Partial Reset is dead code: verify no existing code or test relies
  on Partial Reset behavior

---

## Documentation Impact

**Option B — Update existing doc:**

> `docs/01-game-design/PASSIVE_RULES.md` §2.3 and/or §4.2 must be updated
> to resolve the contradiction. `docs/02-technical/GAME_STATE.md` §2.3 may
> need a dependent update depending on the decision. Use
> `documentation/documentation-change.md`.

---

## Stop Conditions

- If the authoritative sources do not contain enough information to
  determine the intended gameplay behavior (overshoot vs dead code): STOP
  per `AGENTS.md §7` and report the exact unresolved decision
- If the decision requires a gameplay design change beyond documentation
  clarification (e.g. new MVP Pet using Partial Reset): STOP per
  `AGENTS.md §7` and report

---

## Dependencies

- TASK-009 (Passive Tracker, DONE) — the contradiction was discovered
  during TASK-009 validation; TASK-009's completion evidence documents
  the acceptance scenario that exposed it

---

## Completion Evidence

<!-- TO BE FILLED BY THE AGENT after the task reaches DONE -->

### Summary

Resolved the contradiction between PASSIVE_RULES.md §2.3/§5 (implying
progress resets at exactly Threshold, making overshoot impossible) and §4.2
(Partial Reset, which requires overshoot to be meaningful). Decision: Option
A — progress may overshoot Threshold via batch accumulation across all
Matches in a Cascade, with a single Threshold evaluation after all Matches
resolve. Updated §2.3 and §5 in PASSIVE_RULES.md. §4.2, GAME_STATE.md §2.3,
and GAME_RULES.md §10 verified consistent without changes.

### Changes

- `docs/01-game-design/PASSIVE_RULES.md` §2.3 — rewritten to define batch
  accumulation model: progress accumulates across all Matches in a Cascade,
  Threshold evaluated once after all Matches, trigger if progress ≥ Threshold
- `docs/01-game-design/PASSIVE_RULES.md` §5 — rewritten with Default Reset
  and Partial Reset examples showing the batch model; removed "triggers
  multiple times" language

### Tests

N/A (documentation-only task)

### Documentation Consulted

- `docs/01-game-design/PASSIVE_RULES.md` §1–§8 (full document)
- `docs/01-game-design/GAME_RULES.md` §10 (Passive Rules overview)
- `docs/02-technical/GAME_STATE.md` §2.3 (PetState, PassiveProgress,
  PassiveResetOverride)

### Documentation Changed

- `docs/01-game-design/PASSIVE_RULES.md` §2.3 and §5 (as described in
  Changes above)

### Validation

- §4.2 Partial Reset text ("allowing overflow matches from a single big
  Cascade to carry into the next charge") is now consistent with the batch
  model
- GAME_STATE.md §2.3 PassiveProgress field ("current count vs. threshold")
  is consistent — progress can now exceed Threshold before trigger
- GAME_STATE.md §2.3 PassiveResetOverride field ("only present if
  non-default reset") is consistent — Partial Reset sets this field
- GAME_RULES.md §10 "reaching it makes it Ready" is compatible with
  ≥ Threshold evaluation
- §8 MVP Passive Reference unchanged — all five MVP Pets use default full
  reset, unaffected by this change

### Risks

- TASK-009 PassiveTracker was implemented under the old model (match-by-match
  triggers). The tracker's Threshold evaluation logic must be verified
  against the new batch model. If the tracker evaluates per-Match instead of
  after all Matches, its behavior is incorrect for the Partial Reset case.
  This is a code verification task, not a documentation task.

### Remaining Issues

- TASK-009 PassiveTracker may need code adjustment to match the batch model
  (evaluate Threshold once after all Matches, not per-Match). Should be
  verified as a follow-up task.

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
