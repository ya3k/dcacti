# TASK-016 — Add Combat Stats to PlayerState

---

## Metadata

```text
Task ID:           TASK-016
Type:              FEATURE
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: N/A
Workflow:          development/feature.md
Skills:            N/A
Dependencies:      TASK-015 (DONE — resolved combat constants)
```

---

## Lifecycle Note — Invalid Start Transition (recorded for review)

This task was executed directly from `Status: BACKLOG`. Per
`tasks/TASK_LIFECYCLE.md` §2, `BACKLOG → IN PROGRESS` is an **invalid**
transition — it must pass through `READY`, and `tasks/README.md` §7 step 2
requires confirming `Status is READY` before pickup. The BACKLOG → READY
validation (`TASK_LIFECYCLE.md` §3) was never performed or recorded.

The implementation was executed and completed, and all
`core/completion.md` §1 criteria are satisfied, so the task is **DONE**. This
section records the process defect rather than concealing it.

**A second process defect, corrected:** this task was first closed out as
`IN REVIEW` in `active/` on the mistaken reasoning that a separate review pass
was owed because the start transition had been invalid. That was wrong on its
own terms — `TASK_LIFECYCLE.md` §3 makes the agent responsible for running
`quality/review.md` and setting `DONE` itself, so there is no external reviewer
to wait for, and skipping a step is not repaired by skipping a second one. The
review was run (`quality/review.md` §1, results in Validation below), it
passed, and the task was then set to `DONE` and moved to `completed/`
(`IN REVIEW → DONE`, `active/` → `completed/`). Fabricating a `READY` state to
retroactively legalize the start was still not done — the invalid transition is
recorded here as a defect, not erased.

---

## Objective

Add HP, MaxHP, ATK, DEF, Power, and Crit fields to `PlayerState` with documented MVP defaults, and initialize them at battle creation. This extends the Battle State Foundation to include the combat stats required by the Damage Pipeline.

---

## Context

TASK-015 resolved the combat constants in authoritative documentation. The combat stats (HP=1000, ATK=50, DEF=25, Crit=5%, Power range 0–100) are now defined in `COMBAT_RULES.md §1.1`. However, `PlayerState` currently only carries `Combo` and `MatchCount` — the combat fields are absent from the domain model.

The staged implementation plan (`GAME_STATE.md §2.0.5.3`) requires these fields to exist before the Damage Pipeline, Resource Generation, or any combat-related resolution can be implemented.

---

## Authoritative Sources

- `docs/01-game-design/COMBAT_RULES.md` §1.1 — Player stats (HP, MaxHP, ATK, DEF, Power, Crit) with MVP defaults
- `docs/02-technical/GAME_STATE.md` §2.2 — PlayerState field list (Combo, MatchCount, HP, MaxHP, ATK, DEF, Power, Crit, StatusEffects[], EquippedRelics[], EquippedCards[])
- `docs/02-technical/GAME_STATE.md` §2.0.5.3 — staged implementation: fields absent from a stage are "not yet implemented", not "not required"
- `docs/02-technical/GAME_STATE.md` §5.1 — post-resolution write-back rule
- `docs/02-technical/ARCHITECTURE.md` §3 — Domain layer module boundaries
- `docs/00-overview/MVP_SCOPE.md` §1 — Combat is IN scope

---

## Scope

### In Scope

- Add `HP`, `MaxHP`, `ATK`, `DEF`, `Power`, `Crit` fields to `PlayerState`
- Define documented MVP default constants (`COMBAT_RULES.md §1.1`)
- Initialize combat stats at battle creation via `PlayerState.Initial` or a new factory method
- Update `BattleState.Create` to pass initial combat values
- Update existing tests that construct `PlayerState` to use new signature

### Out of Scope

- Resource Generation from Gem matches (TASK-017)
- Damage Pipeline (`COMBAT_RULES.md §3`)
- Status Effects (`COMBAT_RULES.md §5`)
- BossState (Boss initial stats deferred per TASK-015)
- Cards, Relics, Pet Skills
- Victory/Defeat conditions
- Any system listed in `MVP_SCOPE.md §2` (OUT)

---

## Current State

```text
PlayerState(Combo, MatchCount)   — only two fields
BattleState.Create()             — creates PlayerState.Initial with Combo=0, MatchCount=0
```

`PlayerState` is a `readonly record struct` at `src/backend/GameServer.Domain/Battle/PlayerState.cs`. It has no combat fields. The `Initial` static property returns `new(0, 0)`.

---

## Acceptance Criteria

- [ ] `PlayerState` contains `HP`, `MaxHP`, `ATK`, `DEF`, `Power`, `Crit` fields with correct types (`int` for HP/MaxHP/ATK/DEF/Power, `decimal` or `float` for Crit percentage)
- [ ] MVP default constants are defined: HP=1000, MaxHP=1000, ATK=50, DEF=25, Power=0 (starting), Crit=5%
- [ ] `PlayerState.Initial` (or equivalent factory) returns a state with combat stats at documented defaults
- [ ] `BattleState.Create` produces a `PlayerState` with combat stats initialized
- [ ] `Power` starts at 0 (resource for casting Cards/Skills, per `COMBAT_RULES.md §1.1`)
- [ ] `Crit` is expressed as a percentage value consistent with `COMBAT_RULES.md §3.3` (5% = 0.05 or 5 depending on chosen representation)
- [ ] All existing tests pass after the signature change
- [ ] New unit tests verify combat stats initialization to documented defaults
- [ ] `quality/review.md §1` checklist passes

---

## Affected Areas

```text
[x] Domain (GameServer.Domain/) — Battle/PlayerState.cs
[x] Tests (tests/) — PlayerState construction sites, BattleStateTests
[ ] Application (GameServer.Application/)
[ ] API (GameServer.Api/)
[ ] Client (client/)
[ ] SignalR / Redis
[ ] PostgreSQL
[ ] Documentation (docs/) — none required (TASK-015 already updated docs)
```

---

## Implementation Notes

1. **Field types**: `HP`, `MaxHP`, `ATK`, `DEF`, `Power` are `int`. `Crit` is a percentage — choose `decimal` (0.05m) or `int` (5 = percent) based on how downstream consumers (`COMBAT_RULES.md §3.3` crit roll) will use it. Reference `COMBAT_RULES.md §1.1` for the exact default value.

2. **Record struct signature change**: Adding fields to `PlayerState` changes its constructor. Every call site that creates a `PlayerState` must be updated. Use the existing `Initial` factory to centralize defaults.

3. **Power range**: `COMBAT_RULES.md §1.1` defines Power as 0–100. Consider whether to enforce the range at the domain level (clamp on write) or leave it as a documented invariant. The simpler approach (no clamping, document the range) is preferred per `ARCHITECTURE.md §5` anti-overengineering.

4. **No BossState**: Boss initial stats are explicitly deferred. Do not add `BossState` to `BattleState` in this task.

5. **Existing test fixtures**: `BattleState.CreateWith()` uses `DefaultPassive(new PassiveId("fixture-passive"), 5)` — this does not need to change, but any direct `PlayerState` construction in tests must be updated.

---

## Testing Requirements

### Test Types Required

```text
[x] Unit tests         — PlayerState initialization, default values
[ ] Integration tests  — N/A (no new boundaries)
[ ] Gameplay scenarios — N/A (no resolution logic yet)
[ ] API tests          — N/A
[ ] Realtime tests     — N/A
[ ] Persistence tests  — N/A
```

### Key Edge Cases

- `Power` starts at 0 (not at max) — per `COMBAT_RULES.md §1.1`
- `HP` starts equal to `MaxHP` (full health at battle start)
- `Crit` default is 5% — verify consistent representation across codebase

---

## Documentation Impact

**Option A — None:**
> This task implements already-documented behavior (`COMBAT_RULES.md §1.1`, `GAME_STATE.md §2.2`). TASK-015 already resolved the constants. No doc changes required.

---

## Stop Conditions

- If `COMBAT_RULES.md §1.1` values conflict with `GAME_STATE.md §2.2` field list: STOP per `AGENTS.md §4`
- If adding a field requires a corresponding change to `BattleStateUpdated` wire format (`SIGNALR_PROTOCOL.md §4.2`): STOP and report — wire changes require explicit documentation

---

## Dependencies

- TASK-015 (DONE) — combat constants resolved in authoritative docs

---

## Completion Evidence

<!-- FILLED BY THE AGENT after implementation; task is IN REVIEW, not DONE. -->

### Summary

Added `HP`, `MaxHP`, `ATK`, `DEF`, `Power`, and `Crit` to `PlayerState` as
`int` members, initialized at battle creation to the documented MVP defaults
of `COMBAT_RULES.md` §1.1 (1000 / 1000 / 50 / 25 / 0 / 5), and made the §5.1
post-resolution write-back carry them forward unchanged. The wire contract was
deliberately NOT changed — see "Deviation from task metadata" below.

### Changes

```text
src/backend/GameServer.Domain/Battle/PlayerState.cs
  - Six new fields: HP, MaxHP, ATK, DEF, Power, Crit (all int), ahead of the
    existing Combo/MatchCount so the record reads in GAME_STATE.md §2.2 order.
  - Six named default constants: DefaultHP/MaxHP/ATK/DEF/Power/Crit. The
    balance values of COMBAT_RULES.md §1.1 therefore have exactly one spelling
    in the type; Initial reads them and restates none.
  - PlayerState.Initial now returns all eight fields at their documented
    starting point.
  - Documented the not-invariants status of the values (§1.1), why Crit is the
    percent 5 and not 0.05, why Power's 0-100 range is not clamped
    (ARCHITECTURE.md §5 anti-overengineering), and that these fields are state
    but not yet wire-delivered.

src/backend/GameServer.Domain/Match3/SwapExecution.cs
  - AccountMatches now copies the six combat stats from `previous` when it
    rebuilds PlayerState. Without this the whole-state replacement of the §5.1
    write-back would have silently reset them to their defaults on every
    committed Swap. Carried forward, not computed: no damage, mitigation,
    healing, or generation rule is implemented here.

src/backend/GameServer.Domain/Battle/BattleState.cs
  - Documentation/comments only. No signature change was required: Create
    already passed PlayerState.Initial, so initialization flowed through the
    documented factory unmodified. Field-list doc updated to show the new
    defaults, and the "rest of PlayerState" deferral list narrowed to
    StatusEffects/EquippedRelics/EquippedCards.

tests/backend/GameServer.Domain.Tests/MatchComboAccountingTests.cs
  - Renamed PlayerState_ShouldCarryExactlyTheTwoDocumentedFields to
    ..._ShouldCarryTheDocumentedFieldsOfTheImplementedStages; expects all 8.
  - PlayerState_ShouldBeOwnedByBattleStateAndNotFlatOnIt now asserts none of
    the 8 is spelled flat on BattleState.
  - Six new tests: documented MVP defaults; Power starts at 0 not the cap; HP
    starts equal to MaxHP; the six constants are exposed and Initial reads
    them; a committed Swap carries the combat stats forward unchanged; a
    rejected Swap leaves them unchanged.

tests/backend/GameServer.Domain.Tests/BattleStateTests.cs
  - Removed HP, MaxHP, ATK, DEF, Crit, Power from the later-stage absent-field
    list; comment updated to record that they are now implemented.

tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs
  - Two direct PlayerState construction sites migrated to
    `PlayerState.Initial with { Combo = ..., MatchCount = ... }` so the
    fixtures keep asserting Match/Combo accounting with combat stats present.
    All other diffs in this file are pre-existing from TASK-013, not this task.

docs/02-technical/GAME_STATE.md
  - §2.2 corrected: it still described HP/MaxHP, ATK/DEF/Crit, and Power as
    "not yet implemented". Now records them as implemented, with their §1.1
    defaults, the Crit percent note, the no-clamping note, and the explicit
    statement that being state is not being wire-delivered. §2.2 item 2 also
    corrected — PetState now exists (TASK-013), so only BossState remains
    absent.
  - NOTE: this edit was made during close-out (per the requesting human's
    decision to restore docs<->code consistency, AGENTS.md §17) and it
    PRE-EMPTS tasks/backlog/TASK-017-update-game-state-docs-combat-stats.md,
    which was discovered afterwards and which has this exact objective. All of
    TASK-017's Acceptance Criteria are satisfied by this edit, including its
    edge case requiring consistency with SIGNALR_PROTOCOL.md §4.2. TASK-017 is
    therefore redundant and should be closed as satisfied-by-TASK-016 rather
    than executed — that decision is left to the human, and TASK-017 was not
    modified.

tasks/active/TASK-016-add-combat-stats-to-playerstate.md
  - Moved from backlog/; Status set to IN REVIEW. Not moved to completed/.
```

### Deviation from task metadata — wire delivery deferred

The task shipped a **Stop Condition** that fired during execution:

> If adding a field requires a corresponding change to `BattleStateUpdated`
> wire format (`SIGNALR_PROTOCOL.md` §4.2): STOP and report — wire changes
> require explicit documentation.

`SIGNALR_PROTOCOL.md` §4.2 items 2-3 fix the `playerState` payload member to
**exactly** `combo` and `matchCount`, and state the rest of §2.2 is "not
delivered, because §4 item 4 admits only the implemented stage's own fields".
The frontend (`PlayerStatePayload`, `RuntimePlayerState`) and the API-payload
tests mirror that. This was reported to the requesting human rather than
resolved unilaterally (`AGENTS.md` §4, §20), and the human's decision was:

```text
Do not modify SIGNALR_PROTOCOL.md, BattleStateUpdated, PlayerStatePayload,
RuntimePlayerState, or any SignalR/API wire contract in TASK-016. Proceed at
the domain/application state level only.
```

That decision is implemented exactly: the fields exist in authoritative state
and are **not** delivered. The pre-existing API tests asserting `playerState`
carries exactly `combo`/`matchCount` still pass unmodified, which is the
evidence that no wire contract moved. This was NOT in the task's original
Acceptance Criteria and is the main item for review.

### Tests

```text
dotnet build src/backend/GameServer.sln          0 warnings, 0 errors
dotnet test  GameServer.Domain.Tests             645 passed, 0 failed
dotnet test  src/backend/GameServer.sln (full)   750 passed, 0 failed
                                                  (Domain 645, Application 53,
                                                   Api 51, Infrastructure 1)
npm run test:run  (frontend)                     174 passed, 0 failed
npm run build     (frontend, tsc && vite build)  succeeded

One transient Application-suite failure appeared in the first full-solution
run and passed on re-run and on every subsequent run; it is unrelated
flakiness, not caused by this change.
```

### Documentation Consulted

```text
docs/01-game-design/COMBAT_RULES.md      §1.1 (player stats + MVP defaults),
                                          §1.2, §2, §3 (pipeline, incl. §3.2,
                                          §3.3), §4, §5, §6, §7
docs/01-game-design/GAME_RULES.md        §3, §5, §12 (Power range), §14, §18
docs/01-game-design/MATCH3_RULES.md      §6 (Combo lifecycle), §8.1-§8.3
docs/01-game-design/PASSIVE_RULES.md     §1, §4, §8 (Crit modifiers)
docs/01-game-design/RELIC_RULES.md       §5 (Assassin Eye Crit modifier)
docs/02-technical/GAME_STATE.md          §0 (staged implementation, items 4-5),
                                          §2, §2.0.5.3, §2.1.10, §2.2, §2.3,
                                          §5.1, §5.2, §5.3
docs/02-technical/SIGNALR_PROTOCOL.md    §4 (items 4, 11, 12, 13), §4.2, §4.3
docs/02-technical/ARCHITECTURE.md        §5 (anti-overengineering)
docs/00-overview/MVP_SCOPE.md            §1 (Combat IN), §2, §3
```

### Documentation Changed

```text
docs/02-technical/GAME_STATE.md §2.2 — corrected (see Changes above).

The task's own metadata said "Documentation Impact: Option A — None", on the
grounds that TASK-015 had already resolved the constants. That was correct
about the constants and wrong about §2.2's implementation-status prose, which
this task made stale. Corrected per AGENTS.md §17 rather than left
inconsistent.

No other doc changed. COMBAT_RULES.md was not touched by this task (its
working-tree diff is TASK-015's, timestamped before this task began).
```

### Validation

```text
Depth: MEDIUM risk per TASK_LIFECYCLE.md §3 / core/validation.md —
gameplay implementation of an already-documented mechanic.

Verified:
- Every implemented value traces to COMBAT_RULES.md §1.1.
- Initialization reaches PlayerState.Initial via BattleState.Create with no
  other production construction site (grep-checked).
- The §5.1 write-back preserves the stats across a committed Swap (new test).
- A rejected Swap writes nothing (new test).
- No second representation: no combat field is flat on BattleState (asserted).
- Server authority preserved: all six are server-produced state; the client
  neither computes nor receives them.
quality/review.md §1 checklist — run, all items PASS:
  Correctness    PASS — every value traces to COMBAT_RULES.md §1.1; GAME_STATE.md
                 §2.2 field list matches the implemented type one-to-one.
  Architecture   PASS — fields live in Domain/Battle; no Infrastructure/Api
                 dependency added to Domain (ARCHITECTURE.md §2.1).
  Scope          PASS — no unrelated refactor. The GAME_STATE.md §2.2 correction
                 is a documentation-consistency requirement of AGENTS.md §17;
                 it pre-empts TASK-017 (recorded in Changes, left to the human).
  Tests          PASS — depth for MEDIUM risk: build validation, focused unit
                 tests, and the API integration tests that guard the boundary
                 (they pass unmodified, proving no wire change). No integration
                 boundary is crossed by this change itself.
  Documentation  PASS — GAME_STATE.md §2.2 corrected and consistent with code.
  Security       PASS — no auth surface touched.
  Performance    PASS — six int fields on a readonly record struct; no query
                 added to the TDD.md §4.3 hot resolution path.
  Maintainability PASS — no new abstraction, interface, or factory. Constants
                 replace restated literals; per ARCHITECTURE.md §5.
  Determinism    PASS — server authority preserved (all six are server-produced
                 state; client neither computes nor receives them), no RNG
                 introduced, write-back still one atomic post-resolution write.
```

### Risks

```text
1. The combat stats are invisible to the client until a wire-contract task
   lands. This is the human's explicit decision, but it means HP is not
   renderable yet.
2. Power is not clamped to 0-100. Chosen as documented-invariant per §1.1 /
   GAME_RULES.md §12 and ARCHITECTURE.md §5. If TASK-017's Resource Generation
   writes Power, the cap must be enforced there.
3. Crit is a percent int. If a later task models it as a fraction, every
   modifier boundary (§3.3, Assassin Eye, Bạch Hổ) needs conversion.
```

### Remaining Issues

```text
1. Wire delivery of the combat stats is unimplemented by design and needs its
   own task (SIGNALR_PROTOCOL.md §4 item 4: additional state is introduced by
   extending GAME_STATE.md, not by the wire shape). It would touch
   SIGNALR_PROTOCOL.md §4.2, PlayerStatePayload, RuntimePlayerState, and the
   API-payload field-set tests.
2. AGENTS.md §16 report — pre-existing stale client/API comments now made
   more stale by this task:
     - src/backend/GameServer.Api/Hubs/BattleHub.cs:173-178 and
       GameServer.Application/Battle/BattleStateService.cs:65,74 still say
       PlayerState carries "only" its Match/Combo members.
     - src/frontend/client/src/services/realtime/SignalRService.ts:57-67
       mirrors the same two-member statement.
   Impact: comment/doc staleness only; no behavior. Not fixed here (AGENTS.md
   §16 — out of this task's scope, and the wire task above will own them).
3. Invalid lifecycle start (BACKLOG -> IN PROGRESS). Recorded in the Lifecycle
   Note above. Process defect, not a code defect.
```

### Agent

gameplay (per task metadata `Primary Agent: gameplay`), executed by the
DeepSeek Harness coding agent.

### Workflow Used

development/feature.md — MVP scope check, context discovery, implementation,
quality/testing.md, quality/review.md (this IN REVIEW gate),
core/completion.md.

### Skills Used

None formally invoked. `determinism` and server-authority checks from
`.ai/workflows`' guidance were applied manually via core/implementation.md §4.

### Status

DONE

---

## Handoff

<!-- FILLED — task is DONE. Completed tasks are immutable
     (TASK_LIFECYCLE.md §3); create a new task for follow-up work. -->

Implementation, testing, and review are complete; `core/completion.md` §1 is
satisfied and the task is DONE. Follow-up work is captured in Remaining Issues
above, and must be raised as new task files rather than by editing this one.

The main follow-up is the **wire-delivery task**: the combat stats are
authoritative state but are not delivered in the `playerState` payload, by the
requesting human's explicit decision when this task's own Stop Condition fired.
That task would touch `SIGNALR_PROTOCOL.md` §4.2, `PlayerStatePayload`,
`RuntimePlayerState`, and the API-payload field-set tests.

TASK-017 (update GAME_STATE.md for combat stats) is **redundant** — this task's
close-out already performed it. See the note in Changes.

TASK-017 as originally scoped for Resource Generation was not started; the
backlog's `TASK-017` slot is currently the documentation task named above.
