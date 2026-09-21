# TASK-012 — Align PassiveTracker with Batch-per-Cascade Model

## Metadata

```text
Task ID:           TASK-012
Type:              REFACTOR
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: N/A
Workflow:          development/refactor.md
Skills:            N/A
Dependencies:      TASK-009, TASK-010, TASK-011
```

---

## Objective

Rewrite `PassiveTracker.Charge` to implement the batch-per-Cascade model from `PASSIVE_RULES.md` §2.3 and §5: accumulate all Matches in a Cascade first, then evaluate the Threshold once, triggering at most once per Cascade. Add `Partial` reset behavior. Update all tests to match.

---

## Context

TASK-009 implemented PassiveTracker using a match-by-match model (checking Threshold after each increment). TASK-010 and TASK-011 established the batch model in `PASSIVE_RULES.md` §2.3 and §5, resolving the Partial Reset contradiction and multi-crossing ambiguity. The implementation now contradicts the authoritative docs:

- **Current code**: match-by-match loop, checks Threshold after each increment, can trigger multiple times per Cascade
- **Authoritative docs (§2.3, §5)**: accumulate all Matches, evaluate Threshold once, trigger at most once per Cascade

Additionally, `PassiveResetBehavior.cs` is missing the `Partial` member (only has `Default`/`NoReset`), which §4 defines and §5 gives examples for.

---

## Authoritative Sources

- `docs/01-game-design/PASSIVE_RULES.md` §2.3 — batch accumulation model, single trigger per Cascade
- `docs/01-game-design/PASSIVE_RULES.md` §4 — reset behaviors (Default, Partial, NoReset)
- `docs/01-game-design/PASSIVE_RULES.md` §5 — cascade examples with Default, Partial, and multi-crossing
- `docs/02-technical/GAME_STATE.md` §2.3 — PetState.PassiveProgress, PassiveResetOverride
- `docs/02-technical/GAME_EVENTS.md` §2 — PassiveCharged/PassiveTriggered event payloads
- `docs/02-technical/ARCHITECTURE.md` §3 — PassiveTracker = Domain layer
- `docs/AGENTS.md` §17 — documentation change rule

---

## Scope

### In Scope

- Add `Partial = 2` to `PassiveResetBehavior` enum
- Rewrite `PassiveTracker.Charge` to use batch model
- Update `Reset` method to handle Partial (`current - threshold`)
- Update `IsDefined` to accept Partial
- Update `PassiveResetBehavior` doc comments (remove "deliberately absent" language)
- Update `PassiveTracker` doc comments to match batch model
- Rewrite `PassiveTrackerTests` for batch model behavior
- Remove tests that assert old match-by-match behavior (multiple triggers per Cascade)

### Out of Scope

- Changing `PassiveProgress`, `PassiveChargedEvent`, `PassiveTriggeredEvent`, or `PassiveChargeResult` types
- Changing any Application-layer or upstream caller code
- Adding new Passives or Pet definitions
- Any system listed in `docs/00-overview/MVP_SCOPE.md` §2 (OUT)

---

## Current State

`PassiveTracker.Charge` at `src/backend/GameServer.Domain/Passives/PassiveTracker.cs`:
- Processes Matches in a for-loop, incrementing progress by 1 per Match
- Checks Threshold after each increment (`if (current >= threshold)`)
- Can trigger multiple times per Cascade (at every crossing)
- Resets to 0 (Default) or unchanged (NoReset) after each trigger
- Rejects Partial Reset (`IsDefined` only accepts Default/NoReset)

`PassiveResetBehavior` at `src/backend/GameServer.Domain/Passives/PassiveResetBehavior.cs`:
- Has only `Default = 0` and `NoReset = 1`
- Doc comments state Partial is "deliberately absent" due to old contradiction

`PassiveTrackerTests` at `tests/backend/GameServer.Domain.Tests/PassiveTrackerTests.cs`:
- Written for match-by-match model
- Asserts multiple triggers per Cascade (e.g. Threshold=3, N=7 → 2 triggers)
- Has tests asserting Partial is unrepresentable

---

## Acceptance Criteria

- [ ] `PassiveResetBehavior` includes `Partial = 2` with correct doc comments
- [ ] `PassiveTracker.Charge` accumulates all Matches before evaluating Threshold
- [ ] `PassiveTracker.Charge` triggers at most once per Cascade (per call)
- [ ] Default Reset: progress → 0 after trigger
- [ ] Partial Reset: progress → `current - threshold` after trigger (overflow preserved)
- [ ] NoReset: progress unchanged after trigger
- [ ] Overflow from Partial Reset is NOT re-evaluated within the same Cascade
- [ ] `PassiveCharged` events emitted once per Match (N charges for N Matches)
- [ ] `PassiveTriggered` event emitted 0 or 1 times per Cascade
- [ ] Trigger reports pre-reset progress (`PASSIVE_RULES.md` §4, `GAME_EVENTS.md` §2 item 2)
- [ ] Threshold ≤ 0 still rejected
- [ ] Negative matchCount still rejected
- [ ] Negative progress still rejected
- [ ] Undefined reset behaviors still rejected
- [ ] All tests pass
- [ ] No unrelated behavior changed

---

## Affected Areas

```text
[x] Domain (GameServer.Domain/) — Passives module
[ ] Application (GameServer.Application/)
[ ] API (GameServer.Api/)
[ ] Client (client/)
[ ] SignalR / Redis
[ ] PostgreSQL
[x] Tests (tests/)
[ ] Documentation (docs/) — doc comments only, no doc/ file changes
[ ] ADR
```

---

## Implementation Notes

The batch model is simple arithmetic. Given N Matches and current progress P:

```
finalProgress = P + N
if finalProgress >= Threshold:
    emit PassiveTriggered(finalProgress, Threshold)
    apply Reset: Default → 0, Partial → finalProgress - Threshold, NoReset → finalProgress
else:
    // no trigger
```

`PassiveCharged` events: emit N events, one per Match, with progress values P+1, P+2, ..., P+N. These are informational — the threshold check uses the final accumulated value, not the intermediate values.

Key difference from old model: the old code checked Threshold after each increment and could trigger multiple times. The new code accumulates all Matches first, checks once, triggers at most once. Under Default Reset with N=7, Threshold=3, the old model triggers at Match 3 (progress 3→0) and Match 6 (progress 3→0), settling at 1. The new model accumulates to 7, triggers once, resets to 0.

---

## Testing Requirements

### Test Types Required

```text
[x] Unit tests         — PassiveTracker, PassiveResetBehavior
[ ] Integration tests
[ ] Gameplay scenarios
[ ] API tests
[ ] Realtime tests
[ ] Persistence tests
```

### Key Edge Cases

- `PASSIVE_RULES.md` §5: Default Reset example (Threshold=3, N=7 → trigger once, progress→0)
- `PASSIVE_RULES.md` §5: Partial Reset example (Threshold=5, N=7 → trigger once, progress→2)
- `PASSIVE_RULES.md` §5: Multi-crossing Partial Reset (Threshold=3, N=7 → trigger once, progress→4)
- Threshold=1: every Cascade triggers
- Threshold > N: no trigger, progress carries
- NoReset: progress persists, re-triggers on next Cascade if still ≥ Threshold
- Zero Matches: no charges, no trigger, progress unchanged

---

## Documentation Impact

**Option A — None:**
> This task implements already-documented behavior (PASSIVE_RULES.md §2.3, §4, §5).
> Doc comments in code will be updated to match, but no docs/ file changes are needed.

---

## Stop Conditions

- If the batch model in PASSIVE_RULES.md §2.3/§5 contradicts another authoritative doc: STOP per AGENTS.md §4
- If Partial Reset behavior in §4/§5 contradicts another authoritative doc: STOP per AGENTS.md §4

---

## Dependencies

- TASK-009 (DONE) — original PassiveTracker implementation
- TASK-010 (DONE) — Partial Reset contradiction resolved, batch model established
- TASK-011 (DONE) — Multi-crossing ambiguity resolved, single trigger per Cascade

---

## Completion Evidence

### Summary

Rewrote `PassiveTracker.Charge` from the match-by-match model to the
batch-per-Cascade model of `PASSIVE_RULES.md` §2 item 3 and §5: the Cascade's
Matches are accumulated first (`P + N`), the Threshold is evaluated **once**
after the full batch, and the Passive triggers **at most once per `Charge()`
call** for every Reset Behavior. Added `PassiveResetBehavior.Partial = 2` with
its documented arithmetic (`current − Threshold`, overflow carried and not
re-evaluated within the same Cascade). `PassiveCharged` remains a pure
informational progress report — it triggers no threshold evaluation and no
reset.

### Changes

- `src/backend/GameServer.Domain/Passives/PassiveResetBehavior.cs` — added
  `Partial = 2`; rewrote the enum's doc comments, removing the
  "deliberately absent / documented conflict" reasoning (resolved by TASK-010
  and TASK-011) and documenting all three behaviors against §4 item 2 and §5's
  worked examples.
- `src/backend/GameServer.Domain/Passives/PassiveTracker.cs` — `Charge` now
  emits one `PassiveCharged` per Match in a per-Match loop (no evaluation, no
  reset inside the loop), then performs a single `if (current >= threshold)`
  evaluation after the batch, emitting zero or one `PassiveTriggered` and
  returning immediately so no remainder can be re-evaluated. `Reset` implements
  `Default → 0`, `Partial → current − threshold`, `NoReset → current`.
  `IsDefined` accepts all three; the rejection message and all class/method doc
  comments were rewritten for the batch model.
- `src/backend/GameServer.Domain/Passives/PassiveEvents.cs` — doc comments only:
  `PassiveCharged` is described as informational (no evaluation, no reset),
  `PassiveTriggered` reports the post-batch pre-reset total, and
  `PassiveChargeResult.Triggers` is documented as zero-or-one per Cascade.
- `src/backend/GameServer.Domain/Passives/PassiveProgress.cs` — doc comments
  only: corrected the note that progress cannot exceed the Threshold mid-Cascade
  (batch accumulation may overshoot it) and restated the settled-range note
  against §2 item 4/§5.
- `src/backend/GameServer.Domain/Match3/BattleEvent.cs` — doc comments only on
  `BattleEventType.PassiveCharged` / `PassiveTriggered`: charged is
  informational, triggered is at most once per Cascade.
- `tests/backend/GameServer.Domain.Tests/PassiveTrackerTests.cs` — rewritten for
  the batch model; removed every test asserting the old match-by-match behavior
  (multiple triggers per Cascade, Partial being unrepresentable, charges never
  exceeding the Threshold). Added §5's Default/Partial/multi-crossing examples
  verbatim, the exhaustive "at most one trigger per Cascade" invariants, the
  informational-charge boundary tests, and re-triggering across separate
  `Charge()` calls under NoReset.

No `docs/` file, Application/API/client/realtime/persistence layer, public type,
event payload, or `PassiveChargeResult` member was changed.

### Tests

- Focused: `dotnet test --filter FullyQualifiedName~PassiveTrackerTests` —
  **45 passed, 0 failed**.
- Full backend suite — **712 passed, 0 failed**:
  - GameServer.Domain.Tests — 624
  - GameServer.Application.Tests — 41
  - GameServer.Api.Tests — 46
  - GameServer.Infrastructure.Tests — 1

### Documentation Consulted

- `docs/01-game-design/PASSIVE_RULES.md` §1, §2 (items 1–4), §3, §4, §5, §6, §7,
  §8 — primary authority
- `docs/01-game-design/GAME_RULES.md` §10 (Passive Rules overview)
- `docs/02-technical/GAME_EVENTS.md` §1, §1.1, §2, §3 item 6
- `docs/02-technical/GAME_STATE.md` §2.3 (`PetState`, `PassiveProgress`,
  `PassiveResetOverride`)
- `docs/02-technical/ARCHITECTURE.md` §2.1 item 1, §3 (`PassiveTracker`, Domain)
- `docs/AGENTS.md` §4, §6, §14, §15, §16, §17, §20, §21, §22
- `tasks/completed/TASK-009/010/011` (dependency decisions)
- `.ai/workflow/development/refactor.md`, `.ai/workflow/core/*`,
  `.ai/workflow/quality/testing.md`

### Documentation Changed

None. TASK-012's Documentation Impact Option A holds: this implements behavior
`PASSIVE_RULES.md` §2 item 3, §4, and §5 already define (as established by
TASK-010 and TASK-011), and code doc comments were updated to match. No
authoritative document was edited.

### Conflict Check

No conflict found. `PASSIVE_RULES.md` §2 item 3, §4, and §5 are internally
consistent; `GAME_RULES.md` §10 (high-level "reaching it makes it Ready and
triggers it"), `GAME_STATE.md` §2.3 (`PassiveProgress` /
`PassiveResetOverride`, which defer detail to `PASSIVE_RULES.md`), and
`GAME_EVENTS.md` §2 (once-per-Match charge, threshold-crossing trigger,
pre-reset progress) all remain consistent with the batch model. Neither Stop
Condition fired.

### Validation

- Every acceptance criterion is covered by a passing test asserting the
  documented value, including all three §5 examples verbatim
  (Default 3/7→0, Partial 5/7→2, multi-crossing Partial 3/7→4 with a single
  trigger) and the rejected cases (Threshold ≤ 0, negative `matchCount`,
  negative progress, undefined reset values).
- Server authority unchanged: Passive progress is still computed only in the
  Domain layer; no client-side computation was added
  (`.ai/workflow/core/implementation.md` §4).
- Builds: GameServer.Domain, GameServer.Application, GameServer.Api, and
  GameServer.Infrastructure — **all succeeded with 0 warnings, 0 errors**.

### Risks

- One test authored during this task initially asserted that Partial Reset
  always settles below the Threshold; that is false when the overshoot exceeds
  one Threshold (e.g. Threshold 1, N=6 → 5). The test was corrected to assert
  §4's actual arithmetic — this was a test-authoring error, not a documentation
  conflict.
- `PassiveProgress.IsReady` is now true for the carried-over remainder under
  `NoReset` and for a large-overshoot `Partial` remainder. That is consistent
  with §4 item 2 being persistent and §2 item 4 deferring re-evaluation to the
  next Cascade, but no caller consumes `IsReady` yet, so the behavior is
  documented rather than exercised.

### Remaining Issues

- Application-layer plumbing that actually feeds Match batches from a Cascade
  into `Charge` and writes the result back to `PetState.PassiveProgress`
  (including how `PassiveResetOverride` is read) remains a separate
  integration task, as TASK-009 scoped it.
- `GAME_EVENTS.md` §1.1's ordered event-list assembly (where `PassiveTriggered`
  now always follows the Cascade's charges) is likewise owned by that
  Application-layer task.

