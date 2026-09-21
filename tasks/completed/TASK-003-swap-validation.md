# TASK-003 — Swap Validation

---

## Metadata

```text
Task ID:           TASK-003
Type:              FEATURE
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: backend, testing, review
Workflow:          development/feature.md
Skills:            gameplay-behavior-derivation, authority-determinism-audit,
                   test-scenario-generation, implementation-review
Dependencies:      None (Board Foundation and Special Gem Resolution are DONE
                   and verified)
```

---

## Objective

Implement the server-authoritative **Swap Validation** capability on top of the
existing Match-3 engine: given the current authoritative board and a requested
`SWAP(from, to)`, decide whether the swap is legal under the documented rules,
without mutating the board, consuming RNG, or emitting gameplay events.

The capability answers exactly one question — *"Is this requested swap legal on
the current authoritative board?"* It does not execute the swap, begin a Turn,
increment `Sequence`, or resolve the board.

---

## Context

Board Foundation (`MATCH3_RULES.md` §1) and Special Gem Resolution
(§3–§5) are implemented and verified: `BoardState.Cells[64]`, `MatchDetector`,
`SpecialGemPlanner`, `SpecialGemEffects`, `Gravity`, `Spawn`, `BoardResolver`,
and `CascadeResolver` all exist.

What is missing is the **player Swap action's validation stage** — the entry
point of the resolution lifecycle in `MATCH3_RULES.md` §1.A. §2 and §2.1 already
define that stage completely; this task implements it. It is the prerequisite
for a later Swap *execution*/resolution task, and this task deliberately stops
before that boundary.

The board is immutable by construction (`BoardState` exposes only derivations
such as `WithSwapped`), and `BoardState.AreOrthogonallyAdjacent` already
implements the documented coordinate-based adjacency. Match legality reuses the
single authoritative `MatchDetector.Detect` rather than duplicating detection.

---

## Authoritative Sources

Read these before writing any code. This task does not restate them.

- `docs/00-overview/MVP_SCOPE.md` §1 — confirm Match-3 Swap is IN scope
- `docs/01-game-design/MATCH3_RULES.md` — **the primary contract**:
  - §1.0 cell indexing / board size (the only coordinate convention)
  - §1.1 Gem types
  - §1.4.2 — the Board Generation Validator is **not** the gameplay Swap
    Validator (the two must stay separate)
  - §2 items 1–5 — the Swap rule: orthogonal adjacency, simulate on a copy,
    commit only if a Match is produced, otherwise revert with no progression
  - §2.1.1 — identity and parameters (`from`/`to` are `0..63` cell indices;
    no direction field; the pair is unordered)
  - §2.1.2 — **the validation order** (index range → distinctness → adjacency →
    match-producing) and the four rejection reasons
  - §2.1.3 — coordinate/boundary rules, incl. no wrap-around across a row edge
  - §2.1.4 — staleness / idempotency (`STALE_ACTION`)
  - §2.1.5 — rejection is a gameplay no-op (board, `Turn`, `Sequence`,
    `RngState` unchanged; no event)
  - §3 — Match Detection (the authoritative shape a swap must produce)
  - §7.2 — validation draws nothing
- `docs/01-game-design/GAME_RULES.md` §2 (Turn), §17 (resolution order),
  §18 (server authority)
- `docs/02-technical/GAME_STATE.md` §2, §2.0.5, §2.1, §3, §5.1 — authoritative
  state, and which fields exist at this layer
- `docs/02-technical/GAME_EVENTS.md` §1.2 — a rejected Swap emits no event
- `docs/02-technical/SIGNALR_PROTOCOL.md` §2, §2.1, §5 — transport ownership;
  no new message is introduced
- `docs/02-technical/ARCHITECTURE.md` §1, §2.1, §5 — validation belongs to
  Domain; anti-overengineering
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md`
- `docs/03-decisions/ADR/ADR-009-deterministic-prng.md` — the single PRNG

---

## Scope

### In Scope

- A pure `BoardState + SwapRequest → SwapValidationResult` operation in
  `GameServer.Domain/Match3`.
- The documented validation order of `MATCH3_RULES.md` §2.1.2:
  index range, distinctness, orthogonal adjacency, match-producing.
- Index-range and same-cell rejection (`INVALID_CELL_INDEX`).
- Adjacency rejection by §1.0 row/column coordinates, so `7 ↔ 8` and
  `15 ↔ 16` are **not** adjacent (`INVALID_SWAP`).
- Match-producing evaluation by simulating the exchange on a derived board and
  reusing the existing authoritative `MatchDetector` (`NO_MATCH_FROM_SWAP`).
- A result type carrying accepted/rejected plus the documented rejection
  reason, for the caller only.
- Focused tests: indices, same cell, horizontal/vertical adjacency, row-wrap,
  diagonal, non-adjacent, match legality, Special-Gem-carrying cells, state
  immutability, and determinism.

### Out of Scope

- Swap **execution**: committing the exchange, `SwapStarted`/`SwapResolved`,
  Turn increment, Combo reset, board resolution, `Sequence` update, RNG
  retention, state publication.
- `STALE_ACTION` / §2.1.4 idempotency — see *Implementation Notes*; the
  documented inputs at this layer cannot supply it.
- Special Gem rules, Gravity rules, Spawn rules, Cascade rules — untouched.
- Any new REST endpoint, SignalR method/payload, Redis field, or DB schema.
- Turn, Damage, Combat, Power, Passive, Relic, Boss, Cards, frontend gameplay.
- Refactoring any existing Match-3 system.

---

## Current State

Existing, and reused rather than rebuilt:

- `src/backend/GameServer.Domain/Match3/BoardState.cs` — `Cells[64]`,
  `ToRow`/`ToColumn`/`ToIndex`, `AreOrthogonallyAdjacent` (coordinate-based),
  `WithSwapped` (non-mutating derivation), `CellsEqual`.
- `src/backend/GameServer.Domain/Match3/MatchDetector.cs` — the single
  authoritative §3 detection pass (`Detect`, `ClearedCellUnion`).
- `src/backend/GameServer.Domain/Match3/BoardGenerationValidator.cs` — the
  §1.4.1 generation-time validator, which §1.4.2 item 2 states is explicitly
  **not** the gameplay Swap Validator.
- `src/backend/GameServer.Domain/Battle/BattleState.cs` — `Turn`, `Sequence`,
  `RngState`, `BoardState`.

Missing: the gameplay Swap Validator (`MATCH3_RULES.md` §2.1.2). No type in
`GameServer.Domain` currently models a Swap request or its validation result
(`INVALID_CELL_INDEX` / `INVALID_SWAP` / `NO_MATCH_FROM_SWAP` / `STALE_ACTION`
appear only in `docs/`).

---

## Acceptance Criteria

- [ ] Given a board and a `SWAP(from, to)`, validation returns accepted or
      rejected (`MATCH3_RULES.md` §2.1.2).
- [ ] `from`/`to` outside `0..63` are rejected as `INVALID_CELL_INDEX`
      regardless of board contents (§2.1.2 item 1).
- [ ] `from == to` is rejected as `INVALID_CELL_INDEX` (§2.1.2 item 2).
- [ ] Non-orthogonally-adjacent pairs — diagonal, distant, and row-wrap
      (`7 ↔ 8`, `15 ↔ 16`) — are rejected as `INVALID_SWAP` (§2.1.2 item 3,
      §2.1.3 item 1).
- [ ] An adjacent swap that produces at least one §3 Match is accepted
      (§2.1.2 item 4).
- [ ] An adjacent swap that produces no §3 Match is rejected as
      `NO_MATCH_FROM_SWAP` (§2.1.2 item 4).
- [ ] Match legality reuses `MatchDetector`; no second detection implementation
      exists (§1.4.2, `AGENTS.md` §9).
- [ ] Validation leaves `BoardState`, `RngState`, `Turn`, and `Sequence`
      unchanged for both accepted and rejected requests (§2.1.5, §7.2).
- [ ] Validation consumes zero RNG (§7.2).
- [ ] Validation emits no gameplay event and calls no transport/persistence
      (§2.1.5 item 6, `GAME_EVENTS.md` §1.2).
- [ ] A cell carrying a Special Gem is exchanged as a whole entry and its
      Special Gem state is neither destroyed, activated, consumed, nor altered
      (`GAME_STATE.md` §2.1.5 item 2).
- [ ] Repeated validation of the same board and request returns an identical
      result (§2.1.4 item 6, §7.1).
- [ ] No new API / SignalR / Redis contract is introduced.
- [ ] All relevant tests pass at the depth required by
      `core/validation.md` §2 for the task's Risk level (MEDIUM)
- [ ] `quality/review.md` §1 checklist passes
- [ ] Documentation impact addressed (§ Documentation Impact below)

---

## Affected Areas

```text
[x] Domain (GameServer.Domain/) — Match3 module only
[ ] Application (GameServer.Application/)
[ ] API (GameServer.Api/)
[ ] Client (client/)
[ ] SignalR / Redis (GameServer.Infrastructure/SignalR/, /Redis/)
[ ] PostgreSQL (GameServer.Infrastructure/Postgres/)
[x] Tests (tests/)
[ ] Documentation (docs/)
[ ] ADR (docs/03-decisions/ADR/)
```

---

## Implementation Notes

Task-specific notes. Reference docs by path+section, never copy them.

1. **Reuse, do not duplicate.** `BoardState.AreOrthogonallyAdjacent` already
   implements §2.1.3's coordinate-based adjacency, and `BoardState.WithSwapped`
   already implements §2 item 2's "simulate on a copy". `MatchDetector.Detect`
   is the one authoritative §3 implementation. The validator composes these; it
   must not re-implement any of them, and `BoardGenerationValidator` must not be
   borrowed (it is bounded to initialization by §1.4.2).

2. **`STALE_ACTION` (§2.1.4 item 2) is not implemented — documented input
   boundary, not an omission.** The rule rejects a swap whose unordered pair is
   exactly "the pair most recently committed to the board". No field recording
   that pair exists at this layer: `GAME_STATE.md` §2's `BattleState` has no
   such member, §2.0.5's Board Foundation State has none, and §3's
   `ResolutionContext`/pass-local bookkeeping does not list one. The task's own
   validation order (§8 item 5) likewise lists index, distinctness, adjacency,
   and match legality only. Providing it would require adding a `BattleState`
   field — a state-contract change owned by `GAME_STATE.md` and outside this
   task's scope — or a caller-supplied "already applied" value, which is not the
   documented input (`MATCH3_RULES.md` §2.1.1 item 3: no client-supplied
   gameplay field). §2.1.4 item 3 also establishes that a Swap is otherwise
   stateless against the current board, so the remaining checks are complete
   without it. This boundary is reported, not silently invented around.

3. **`from`/`to` are symmetric.** §2.1.1 item 2 and §2.1.3 item 3: the order has
   no gameplay meaning for validation, so the result must be identical for
   `(a, b)` and `(b, a)`.

4. **Result type placement.** The rejection reason is reported to the caller
   only (`SIGNALR_PROTOCOL.md` §5) and is not a wire contract; do not add it to
   an API/DTO type. Keep it a Domain type and do not leak private
   implementation detail through it.

5. **`RngState` is a value type** (`readonly record struct`), so immutability of
   RNG is structural — the validator must simply never take an RNG parameter.

---

## Testing Requirements

### Test Types Required

```text
[x] Unit tests         — SwapValidator: all four §2.1.2 checks, on
                         hand-built deterministic boards
[ ] Integration tests  — N/A: no boundary is crossed (Domain only, no
                         API/DB/SignalR)
[ ] Gameplay scenarios — N/A: this task implements a precondition decision,
                         not a state transition. The SWAP → Match → Cascade
                         → Turn chain belongs to Swap execution, which is out
                         of scope.
[ ] API tests          — N/A: no endpoint introduced
[ ] Realtime tests     — N/A: no Hub method introduced
[ ] Persistence tests  — N/A: no schema/state added
```

Depth: MEDIUM per `core/validation.md` §2 — build/compile + focused unit tests
+ documentation validation + review. No boundary (API/DB/SignalR) is crossed,
which is why no integration layer applies; the task changes no documented
behavior, which is why a full gameplay-scenario chain does not apply.

### Key Edge Cases

- `docs/01-game-design/MATCH3_RULES.md` §2.1.3 item 1 — index `7` / index `8`
  differ by 1 but are not adjacent; no wrap-around across a row edge
- `docs/01-game-design/MATCH3_RULES.md` §2.1.2 item 3 — diagonal and distant
  pairs rejected
- `docs/01-game-design/MATCH3_RULES.md` §2.1.5 items 1–5 — rejected action
  changes board, `Turn`, `Sequence`, `RngState`; emits nothing
- `docs/01-game-design/MATCH3_RULES.md` §7.2 — validation draws nothing
- `docs/02-technical/GAME_STATE.md` §2.1.5 item 2 — a Special Gem travels with
  its cell when the cell is exchanged
- `docs/01-game-design/MATCH3_RULES.md` §2.1.1 item 2 — the pair is unordered,
  so `(a, b)` and `(b, a)` validate identically

---

## Documentation Impact

**Option A — None:**

> This task implements already-documented behavior. No doc changes required.
> `MATCH3_RULES.md` §2/§2.1 fully define Swap validation, including the
> validation order and the rejection reasons. The §2.1.4 idempotency check is
> deliberately not implemented at this layer because the state it reads is not
> part of the documented state model at this stage (see Implementation Notes
> item 2); this is a staging boundary already implied by `GAME_STATE.md`
> §2.0.5/§2, not a documentation gap requiring correction.

---

## Stop Conditions

- If the required behavior cannot be fully derived from the
  Authoritative Sources listed above: STOP per `AGENTS.md` §7
- If `MATCH3_RULES.md` §2.1.2's legality rule cannot be implemented without
  changing the `BattleState` state contract or another subsystem: STOP per
  `AGENTS.md` §18 (`STOP — IMPLEMENTATION BOUNDARY ISSUE`)
- If §2.1.2 and any other authoritative document disagree on swap legality:
  STOP per `AGENTS.md` §4

---

## Dependencies

- None

---

## Completion Evidence

### Summary

Implemented server-authoritative Swap Validation (`MATCH3_RULES.md` §2, §2.1.2)
as a pure `BoardState + SwapRequest → SwapValidationResult` operation in
`GameServer.Domain/Match3`. The validator evaluates the documented checks in the
documented order — index range and distinctness (`INVALID_CELL_INDEX`),
orthogonal adjacency on the §1.0 coordinate mapping (`INVALID_SWAP`), and
match-producing (`NO_MATCH_FROM_SWAP`) — and answers only whether a requested
swap is legal. It commits nothing, begins no Turn, advances no `Sequence`,
resolves no board, and emits no event.

Match legality reuses the single authoritative `MatchDetector` and adjacency
reuses `BoardState.AreOrthogonallyAdjacent`; no Match Detection or coordinate
logic is duplicated, and `BoardGenerationValidator` (which §1.4.2 item 2
explicitly excludes from this role) is not borrowed.

### Changes

Created:

- `src/backend/GameServer.Domain/Match3/SwapRequest.cs` — the requested swap's
  two §1.0 cell indices, with no direction field (§2.1.1).
- `src/backend/GameServer.Domain/Match3/SwapRejectionReason.cs` — the closed set
  of documented rejection reasons (§2.1.2).
- `src/backend/GameServer.Domain/Match3/SwapValidationResult.cs` — the
  accepted/rejected decision plus its reason.
- `src/backend/GameServer.Domain/Match3/SwapValidator.cs` — the validator.
- `tests/backend/GameServer.Domain.Tests/SwapValidationTests.cs` — focused tests.
- `tasks/active/TASK-003-swap-validation.md` — this task file.

Modified: none. No existing Match-3 system, test, contract, or document was
changed or weakened.

Not introduced: no REST endpoint, no SignalR method or payload, no Redis field,
no database schema, no frontend code, no new `BattleState` field.

### Tests

Added 65 focused tests in `SwapValidationTests`, all passing. Coverage maps to
the task's §13–§15:

- indices: `from`/`to` below 0 and above 63 (theory, both arguments, both
  out of range; board edges 0 and 63 not misreported)
- same cell: `from == to` across seven indices
- horizontal adjacency accepted: `0↔1`, `12`, `8↔9`, `6↔7`, `62↔63`
- row wrap rejected: `7↔8`, `15↔16`, `23↔24`, `31↔32`, plus the reversed order
  and an explicit guard that `Math.Abs(from - to) == 1` holds while
  `AreOrthogonallyAdjacent` is false
- vertical adjacency accepted: `0↔8`, `1↔9`, `7↔15`, `8↔16`, `55↔63`
- diagonal rejected: `0↔9`, `1↔8`, `9↔18`, `16↔25`
- non-adjacent rejected: `0↔2`, `0↔16`, `10↔30`, `0↔63`
- match legality: horizontal-completing accepted, vertical-completing accepted,
  no-match adjacent rejected, wrong-pair rejected while the completing pair is
  accepted, multi-shape accepted, length-4 accepted, and an exhaustive
  agreement check over all 112 adjacent pairs that validator acceptance equals
  `MatchDetector.Detect(swapped).Count > 0`
- Special Gem cells: a swap involving Special Gem cells is accepted on the
  normal terms, and all 64 entries (Gem type, Special Gem type, orientation) are
  identical before and after
- immutability: rejected, accepted, and exhaustive-over-all-adjacent-pairs
  board comparisons; plus `Turn`, `Sequence`, and `RngState` unchanged
- RNG: zero consumption proven behaviorally against a `Pcg32` control
- determinism: 20 repeats on one request, and repeated validation over every
  adjacent pair, each returning an identical result and leaving the board
  identical
- boundary: the validator exposes only `Validate`; `ArgumentNullException` on a
  null board

### Documentation Consulted

- `docs/00-overview/MVP_SCOPE.md` §1
- `docs/01-game-design/MATCH3_RULES.md` — §1.0, §1.1, §1.4.2, §2 (items 1–5),
  §2.1.1, §2.1.2, §2.1.3, §2.1.4, §2.1.5, §3 (incl. §3.1 item 3, §3.2, §3.3),
  §4.6, §7.2
- `docs/01-game-design/GAME_RULES.md` §2, §17, §18
- `docs/02-technical/GAME_STATE.md` §2, §2.0.5, §2.1 (incl. §2.1.5 item 2,
  §2.1.7 item 5), §3, §5.1
- `docs/02-technical/GAME_EVENTS.md` §1.2
- `docs/02-technical/SIGNALR_PROTOCOL.md` §2, §2.1, §5
- `docs/02-technical/ARCHITECTURE.md` §1, §2.1, §5
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md`,
  `ADR-009-deterministic-prng.md`
- `AGENTS.md`, `docs/AGENTS.md`, `.ai/README.md`,
  `.ai/workflow/core/{task-intake,context-discovery,planning,implementation,validation,completion}.md`,
  `.ai/skills/gameplay/gameplay-behavior-derivation.md`,
  `.ai/skills/testing/test-scenario-generation.md`,
  `.ai/skills/backend/`, `tasks/TASK_TEMPLATE.md`, `tasks/TASK_LIFECYCLE.md`

### Documentation Changed

None. `MATCH3_RULES.md` §2/§2.1 already define swap validation completely —
including the validation order and all four rejection reasons — so this task
implemented documented behavior and no documentation decision was required.

### Validation

Depth: MEDIUM per `core/validation.md` §2 — build/compile validation, focused
unit tests, documentation validation, and review. No API/DB/SignalR boundary is
crossed, so no integration layer applies; the task implements a precondition
decision rather than a state transition, so no full gameplay-scenario chain
applies.

```text
Build:       Build succeeded. 0 Warning(s), 0 Error(s)
Focused:     Passed! - Failed: 0, Passed: 65, Total: 65
             (GameServer.Domain.Tests, --filter SwapValidationTests)
Regression:  Passed! - Failed: 0, Passed: 376, Total: 376 (Domain)
             Passed! - Failed: 0, Passed:  26, Total:  26 (Application)
             Passed! - Failed: 0, Passed:   1, Total:   1 (Infrastructure)
             Passed! - Failed: 0, Passed:  18, Total:  18 (Api)
             Total 421 passed, 0 failed
```

Baseline before this task was 356 passed / 0 failed; the regression suite grew
by exactly the 65 added tests and nothing existing changed state. All existing
Board Foundation and Special Gem tests continue to pass; no test was weakened,
skipped, or deleted.

### Risks

- `STALE_ACTION` (§2.1.4) is not implemented at this layer. Documented in the
  task file and in `SwapValidator`'s XML docs. The rule needs a record of "the
  pair most recently committed to the board", which no documented state field
  holds at this stage (`GAME_STATE.md` §2, §2.0.5, §3). Adding it would change
  the `BattleState` contract, which is out of this task's scope. The Swap
  execution task must supply it when it owns the commit.
- `ProducesMatch` runs a whole-board detection pass rather than the §1.4.3
  neighbourhood minimum. This is deliberate: it keeps exactly one authoritative
  Match Detection (`AGENTS.md` §9) and cannot disagree with the detection the
  resolution itself will run. The cost is a full 64-cell pass per validation,
  which is negligible for an 8×8 board and is not a correctness risk.

### Remaining Issues

- The task's §8 validation order lists exactly five checks, the fifth of which
  is match legality. `MATCH3_RULES.md` §2.1.2 additionally lists
  already-applied/idempotency as check 3. The task text and the domain rule are
  not in conflict — the task says "at minimum validate" and does not forbid the
  documented set — but idempotency is nonetheless unimplementable here for the
  state-contract reason above. Reported, not worked around. Suggested follow-up:
  the Swap execution/resolution task owns `STALE_ACTION`, since it is the stage
  that commits the pair.
- Pre-existing uncommitted work is present in the working tree from earlier
  tasks (documentation updates, frontend runtime files, Board Foundation and
  Special Gem sources and tests). It was not touched or reviewed by this task.
  Not an issue with this task; noted only so the diff is not misread.

### Agent

gameplay (primary), with backend, testing, and review responsibilities applied
in the same session.

### Workflow Used

`development/feature.md`, gated by `core/task-intake.md`,
`core/context-discovery.md`, `core/planning.md`, `core/implementation.md`,
`core/validation.md`, and `core/completion.md`.

### Skills Used

`gameplay-behavior-derivation`, `authority-determinism-audit`,
`test-scenario-generation`, `implementation-review`, `scope-validation`.

### Status

DONE

---

## Handoff

Swap Validation is complete and verified. `SwapValidator.Validate` is the entry
point; it is pure, non-mutating, consumes no RNG, and touches no transport or
persistence.

The next task — Swap execution and board resolution integration — starts from an
accepted `SwapValidationResult` and owns everything this task deliberately did
not: the `SWAP → Match → Cascade → Combo → Turn` chain of `MATCH3_RULES.md`
§2.1.6, `Turn` and `Sequence` updates, and the `STALE_ACTION` idempotency check
of §2.1.4, which requires the newly committed pair to be recorded in
`BattleState` and is therefore a state-contract change to be raised against
`GAME_STATE.md` there.