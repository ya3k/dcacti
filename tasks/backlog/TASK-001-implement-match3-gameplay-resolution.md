# TASK-001 — Implement Match-3 Gameplay Resolution

---

## Metadata

```text
Task ID:           TASK-001
Type:              FEATURE
Status:            READY
Risk:              HIGH
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: backend, realtime, testing, review
Workflow:          development/feature.md
Skills:            gameplay-behavior-derivation, authority-determinism-audit,
                   test-scenario-generation, documentation-consistency
Dependencies:      None (Board Foundation is DONE and verified)
```

---

## Objective

Implement the Match-3 board resolution lifecycle defined by
`docs/01-game-design/MATCH3_RULES.md` §2–§8: the player Swap action and its
validation, Swap resolution, Match Detection, Match Resolution, Special Gem
creation and activation, Cascade, Gravity, Spawn, Combo, and the `Turn` /
`Sequence` update — producing a resulting `BattleState` and the ordered Battle
Events for one resolved action. Every rule is already documented; this task
implements documented behaviour and invents nothing.

---

## Context

The Board Foundation stage is implemented and verified end-to-end
(`GAME_STATE.md` §2.0.5): deterministic row-major constrained random fill,
the Board Generation Validator, and the initial-state SignalR push. No gameplay
resolution exists yet.

A documentation-contract task has now resolved the complete resolution
contract. The previously missing semantics — when `Turn` increments, when
`Sequence` increments, swap rejection behaviour, RNG consumption during
gameplay, cascade ordering, gravity, spawn, Special Gem creation/activation,
Combo lifecycle, and event ordering — are now all defined and each has exactly
one owning section. This task implements them.

---

## Authoritative Sources

Read these, in this order, before writing any code:

- `docs/00-overview/MVP_SCOPE.md` §1 — confirm Match-3 swap/match/cascade/combo
  and Special Gems are IN scope
- `docs/01-game-design/GAME_RULES.md` §2 (Turn), §3 (Match), §4 (Cascade),
  §5 (Combo), §16 (event names), §17 (resolution order), §18 (server authority)
- `docs/01-game-design/MATCH3_RULES.md` — **the primary contract**:
  - §1.0 indexing, §1.1 Gem types, §1.2.1.4 generation RNG
  - §2.1 Swap action contract (validation order, staleness, rejection,
    accepted lifecycle)
  - §3 Match Detection (set semantics, deterministic order, duplicate cells)
  - §4 Cascade (per-pass steps, loop, termination, gravity, spawn)
  - §5 Special Gems (which shape creates which gem, placement, activation,
    chain ordering)
  - §5.7 resource generation hooks
  - §6 Combo (start, increment, reset, timing)
  - §7 RNG (what consumes it, what must not, conversion)
  - §8 Turn and Sequence (when each changes, update order, counter table)
- `docs/02-technical/GAME_STATE.md` §2.0.2 (initial values), §2.1 (`BoardState`
  as `Cells[64]`, the cell entry, and the Special Gem state representation),
  §2.2 (`PlayerState.Combo`, `MatchCount`),
  §2.6 (RNG), §5.1 (state write-back and counter order), §5.2, §5.3
- `docs/02-technical/GAME_EVENTS.md` §1, §1.1 (board cycle ordering), §1.2
  (rejected swap emits nothing), §2 (payloads)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §2, §2.1 (`Swap` parameters),
  §3, §3.1 (delivery on a resolution), §5 (acknowledgement), §6
- `docs/02-technical/ARCHITECTURE.md` §1, §2.1, §4.1 (one action is one
  resolution; the cascade loop belongs to Domain)
- `docs/02-technical/REDIS_STATE.md` §4 (one write-back, `Sequence`-gated),
  §7 (staging boundary — do not persist at this stage)
- `docs/02-technical/TDD.md` §3, §6
- `docs/03-decisions/ADR/ADR-009-deterministic-prng.md` (the PRNG)
- `docs/01-game-design/PASSIVE_RULES.md` §2, §5 — downstream consumer of Match
  events; do not implement Passive behaviour here, but do not break its inputs

---

## Scope

### In Scope

- Domain: Swap validation (`MATCH3_RULES.md` §2.1.2–§2.1.5), Match Detection
  (§3), the cascade loop with Match Resolution / Special Gem activation and
  creation / Gravity / Spawn (§4), Special Gem rules (§5), Combo (§6), RNG
  consumption (§7).
- Domain: the `Turn` / `Sequence` counter changes (§8) as part of producing the
  resulting board state.
- Application: the `ResolveSwap` use case sequencing the pipeline per
  `ARCHITECTURE.md` §4.1 and `GAME_RULES.md` §17.
- Api/Realtime: the `Swap` Hub method, thin, delegating to Application, plus
  the `accepted` / rejection result (`SIGNALR_PROTOCOL.md` §2, §5).
- Events: the Match-3 events of `GAME_EVENTS.md` §1 in the documented order.
- Tests: unit and gameplay-scenario tests per §Testing Requirements below.

### Out of Scope

- Combat, damage, Power, Passives, Relics, Cards, Bosses — later stages. The
  pipeline steps 10–19 of `GAME_RULES.md` §17 are not implemented here.
- Redis / PostgreSQL persistence (`REDIS_STATE.md` §7 item 8 — resolving a board
  does not authorize persistence; the staged subset still applies).
- Reconnect / resync (`SIGNALR_PROTOCOL.md` §7).
- `CardCast` / `PetSkillCast`.
- Any change to the board-generation contract (`MATCH3_RULES.md` §1.2–§1.5).
- Any new Special Gem type, event name, SignalR method, or game mechanic.
- Any system listed in `docs/00-overview/MVP_SCOPE.md` §2 (OUT).

---

## Current State

```text
src/backend/GameServer.Domain/Match3/
  GemType.cs                    four types + contract names
  BoardState.cs                 Cells[64], indexing, adjacency, WithSwapped
  Pcg32.cs                      RngState pair + PCG32
  BoardGenerator.cs             initial constrained fill
  BoardGenerationValidator.cs   §1.3/§1.4 generation-time checks only
src/backend/GameServer.Domain/Battle/BattleState.cs
                                BattleId, Turn, Sequence, RngSeed, RngState,
                                BoardState
src/backend/GameServer.Application/Battle/BattleStateService.cs
                                creates/holds board-foundation state
src/backend/GameServer.Api/Hubs/BattleHub.cs
                                JoinBattle + BattleStateUpdated push only
```

Match detection, cascade, gravity, spawn, Special Gems, Combo, and the `Swap`
method do not exist. `BoardState` currently holds `Cells[64]` as Gem types
only; the Special Gem metadata of `GAME_STATE.md` §2.1.3 is added by this task
inside the cell entry, with no second collection and no added `BoardState`
field. `BoardState.WithSwapped` already exists and is the
documented "simulate the swap on a copy" primitive — reuse it; do not
re-purpose `BoardGenerationValidator`, whose scope is bounded by
`MATCH3_RULES.md` §1.4.1 to generation only.

---

## Acceptance Criteria

- [ ] The `Swap` Hub method exists with the `SIGNALR_PROTOCOL.md` §2.1
      parameters, is thin, and returns the §5 `accepted` / rejection result.
- [ ] A Swap that fails any `MATCH3_RULES.md` §2.1.2 check is rejected: the
      board is state-for-state unchanged, `Turn`, `Sequence`, and `RngState`
      are unchanged, and no Battle Event is emitted (§2.1.5).
- [ ] A committed Swap begins exactly one Turn and increments `Sequence` by
      exactly 1, whatever number of Matches/Cascades it contains (§8).
- [ ] `Sequence` is written once, after the board is stable, and no
      intermediate board state is ever written or published (`§8.3`,
      `GAME_STATE.md` §5.1).
- [ ] Match Detection is one pass per board state, produces a set with no
      duplicate cells, and reports in the §3.2 order.
- [ ] The cascade loop follows §4.1 step order exactly, ends when a pass finds
      no Match (§4.3), and introduces no cascade cap.
- [ ] Gravity matches §4.4 (column 0→7, bottom-up, order-preserving, no RNG).
- [ ] Spawn fills the top empty cells of each column in §4.5 order and consumes
      exactly one RNG selection per spawned cell — and nothing else in the
      resolution consumes RNG (§7.2).
- [ ] Special Gems are created per §5.5.2, at the §5.5.3 positions, after
      activation of the pass's consumed Special Gems and before Gravity,
      in the §5.5.1 order.
- [ ] Special Gem activation clears its whole affected set as one step, chains
      breadth-first, removes each cell once, and creates no Match (§5.5.5).
- [ ] Combo resets to 0 on a committed Swap and increments by exactly 1 per
      Match; no committed Swap can publish `Combo = 0` (§6).
- [ ] The same initial state plus the same ordered accepted actions produces the
      same final board, the same `RngState`, the same Match count, and the same
      Combo (§7.1) — asserted by test.
- [ ] Events are emitted in the `GAME_EVENTS.md` §1 / §1.1 order.
- [ ] Domain has no dependency on Application/Infrastructure/Api types
      (`ARCHITECTURE.md` §2.1).
- [ ] All relevant tests pass at the depth required by `core/validation.md §2`
      for Risk HIGH.
- [ ] `quality/review.md §1` checklist passes.
- [ ] Documentation impact addressed (see below).

---

## Affected Areas

```text
[x] Domain (GameServer.Domain/) — Match3 (new resolution modules), Battle
[x] Application (GameServer.Application/) — ResolveSwap use case
[x] API (GameServer.Api/) — BattleHub.Swap
[x] Client (client/) — no (presentation of new events is a later task)
[x] SignalR / Redis (GameServer.Infrastructure/SignalR/, /Redis/) — Hub only;
                                no Redis writes (§7 item 8)
[ ] PostgreSQL (GameServer.Infrastructure/Postgres/)
[x] Tests (tests/)
[ ] Documentation (docs/) — see Documentation Impact
[ ] ADR (docs/03-decisions/ADR/)
```

---

## Implementation Notes

1. **The cascade loop is Domain, not Application.** `MATCH3_RULES.md` §4 is
   board behaviour; `ARCHITECTURE.md` §4.1 item 1 places it in the Match-3
   domain module. `BattleResolutionService` sequences steps and does not
   implement the loop.
2. **Do not reuse `BoardGenerator` for spawn.** `MATCH3_RULES.md` §1.2.1.5 and
   §4.5 item 3 are explicit: the constrained fill is initial-board only. A
   cascade spawn is an independent uniform draw from all four types, one
   selection per cell.
3. **Do not reuse `BoardGenerationValidator` as the gameplay detector.** Its
   scope is bounded by `MATCH3_RULES.md` §1.4.1/§1.4.2 to the two
   initialization-time constraints; the gameplay detector implements §3 in
   full (match set, order, tiers, shapes).
4. **One draw site.** `RngState` must be advanced in exactly one gameplay
   place (spawn). If a second draw site appears, it contradicts §7.2.
5. **`BoardState` is `Cells[64]` alone — there is no `PendingSpecialGems[]`.**
   `GAME_STATE.md` §2.1 now defines the Special Gem state representation:
   `BoardState` has exactly one field, `Cells[64]`, and each cell entry carries
   its Gem type plus an optional Special Gem (type, and orientation for a Line
   Clear Gem). The former `PendingSpecialGems[]` field-shape gap is closed; it
   was never a gameplay rule and does not block this task. Within this task the
   Special Gem store is a Domain-owned structure over `Cells[64]`; it must not
   introduce a second board collection, a `CellIndex` field, or any field the
   canonical model does not have (`GAME_STATE.md` §2.1.3 item 4, §2.1.2
   item 1). Redis is still not written at this stage (`REDIS_STATE.md` §7
   item 8),
   so this task neither serializes the board nor adds a wire field — the §4
   push continues to carry the state's own shape.
6. **`RngState` advancement must be threaded through the resolution.** Each
   spawn step takes the current `RngState`, advances it, and returns the new
   one so the final state retains it (`GAME_STATE.md` §2.6.2 item 2).

---

## Testing Requirements

### Test Types Required

```text
[x] Unit tests         — swap validation matrix (adjacent / diagonal /
                         same-cell / out-of-range / no-match / already-applied),
                         match detection and ordering, gravity column+row order,
                         spawn position and draw count, Special Gem placement
                         and precedence, Combo increments and reset
[x] Integration tests  — Hub Swap → Application → Domain → counter update →
                         event batch
[x] Gameplay scenarios — full state-transition chain per GAME_RULES.md §17 for
                         the Match-3 portion, including the §6.7 worked example
                         (1 Turn, 3 Matches, Combo 3)
[ ] API tests          — n/a (§API_CONTRACTS.md has no Swap endpoint)
[x] Realtime tests     — rejected Swap delivers no batch and emits no event;
                         accepted Swap delivers one batch with the new sequence
[ ] Persistence tests  — n/a (no persistence at this stage)
```

### Key Edge Cases

- `MATCH3_RULES.md` §2.1.3 item 1 — `index 7` / `index 8` are not adjacent.
- `MATCH3_RULES.md` §2.1.4 item 2 — the same pair swapped twice is rejected as
  already applied.
- `MATCH3_RULES.md` §3.3 — a shared cell forms one L/T match, not two; a cell
  named twice is removed once.
- `MATCH3_RULES.md` §4.2 item 2 / `GAME_EVENTS.md` §2 — depth 1 is not a
  Cascade; `CascadeCreated` is depth-indexed from the second pass.
- `MATCH3_RULES.md` §4.3 item 4 — the terminating pass emits nothing.
- `MATCH3_RULES.md` §4.4 item 3 — gravity never reorders Gems within a column.
- `MATCH3_RULES.md` §4.5 item 1 — a column with `k` survivors spawns exactly
  `k` Gems.
- `MATCH3_RULES.md` §5.3 item 3 — a run of 6+ is a Burst Gem, not a new tier.
- `MATCH3_RULES.md` §5.4 item 3 — an L/T whose arms qualify separately creates
  both Special Gems.
- `MATCH3_RULES.md` §5.5.3 item 3 — even-length line centre tie-break is the
  lower-indexed middle cell.
- `MATCH3_RULES.md` §5.5.5 item 7 — two Special Gems naming each other's cells
  still terminate.
- `MATCH3_RULES.md` §6.5 item 3 — no committed Swap publishes `Combo = 0`.
- `MATCH3_RULES.md` §7.2 — a rejected Swap leaves `RngState` untouched, so
  sending and retrying rejected actions does not shift the stream.
- `RELIC_RULES.md` §4 item 3 ordering convention — referenced by
  `MATCH3_RULES.md` §5.5.5 item 7 for chain breadth-first order.

---

## Documentation Impact

**Option A — None:**

> This task implements already-documented behavior. `MATCH3_RULES.md` §2–§8,
> `GAME_STATE.md` §5.1, `GAME_EVENTS.md` §1, and `SIGNALR_PROTOCOL.md` §2–§3.1
> define the full contract and were resolved before this task was created. No
> doc changes are required.
>
> Exception: if implementation reveals a genuine gap, STOP per
> `AGENTS.md §7` / `.ai/README.md §13` and report it — do not edit docs to
> match whatever the code happens to do (`.ai/README.md §18`).

---

## Stop Conditions

- If the required behavior cannot be fully derived from the Authoritative
  Sources listed above: STOP per `AGENTS.md §7`.
- If the `PendingSpecialGems[]` field shape is judged blocking: it is **not**
  — `GAME_STATE.md` §2.1 defines it (`BoardState` is `Cells[64]`; a Special Gem
  is part of its cell's entry). Do not invent a different representation; if
  the implementation appears to need a field the canonical model does not have,
  STOP per `AGENTS.md §7`.
- If a special-gem placement case arises that `MATCH3_RULES.md` §5.5.3 does not
  cover (e.g. an origin that cannot be identified, §5.5.3 item 2): STOP.
- If implementation needs a cascade cap or any other safety bound not already
  documented: STOP (`MATCH3_RULES.md` §4 item 5 forbids inventing one).
- If two authoritative documents are found to conflict: STOP per
  `AGENTS.md §4`.

---

## Dependencies

- None. The Board Foundation stage is DONE and verified.

---

## Completion Evidence

### Summary
TO BE FILLED BY THE AGENT.

### Changes
TO BE FILLED BY THE AGENT.

### Tests
TO BE FILLED BY THE AGENT.

### Documentation Consulted
TO BE FILLED BY THE AGENT.

### Documentation Changed
TO BE FILLED BY THE AGENT.

### Validation
TO BE FILLED BY THE AGENT.

### Risks
TO BE FILLED BY THE AGENT.

### Remaining Issues
TO BE FILLED BY THE AGENT.

### Agent
TO BE FILLED BY THE AGENT.

### Workflow Used
TO BE FILLED BY THE AGENT.

### Skills Used
TO BE FILLED BY THE AGENT.

### Status
TO BE FILLED BY THE AGENT.

---

## Handoff

TO BE FILLED BY THE AGENT.