# TASK-002 — Special Gem Resolution

---

## Metadata

```text
Task ID:           TASK-002
Type:              FEATURE
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: backend, testing, review
Workflow:          development/feature.md
Skills:            gameplay-behavior-derivation, authority-determinism-audit,
                   test-scenario-generation, implementation-review
Dependencies:      None (Board Foundation is DONE and verified)
```

---

## Objective

Implement deterministic Special Gem resolution on top of the existing Board
Foundation: the Match detection a Special Gem requires, Special Gem creation,
creation collision resolution, placement, activation, breadth-first chain
resolution, cleared-cell union semantics, `GemMatched` integration, gravity,
spawn, and cascade re-resolution — all as documented. Every rule is already
authored; this task implements documented behavior and invents nothing.

The Special Gem state representation is `BoardState.Cells[64]` with one optional
`SpecialGem` per cell entry (`GAME_STATE.md` §2.1). No second board collection is
introduced.

---

## Context

The Board Foundation stage is implemented and verified end-to-end
(`GAME_STATE.md` §2.0.5): deterministic row-major constrained random fill
(`MATCH3_RULES.md` §1.2.1), the Board Generation Validator (§1.3/§1.4), and the
initial-state SignalR push.

The Special Gem rules were authored in `MATCH3_RULES.md` §5.2–§5.9 and the state
representation was closed in `GAME_STATE.md` §2.1.2/§2.1.4. A design and
implementation-readiness audit has already been completed and returned
`READY — SPECIAL GEM IMPLEMENTATION`. No further design or audit cycle is
required or authorized by this task.

`BoardState` currently holds `Cells[64]` as Gem types only. This task adds the
Special Gem metadata **inside the cell entry** (`GAME_STATE.md` §2.1.1), which is
the representation the cell-shape contract already defines — no `BoardState`
field is added, and no parallel Special Gem collection is created.

---

## Authoritative Sources

Read these before writing any code. This task does not restate them.

- `docs/00-overview/MVP_SCOPE.md` §1 — confirm Match 4 / Match 5 / L-T matches,
  Cascade, Combo, and Special Gems are IN scope
- `docs/01-game-design/MATCH3_RULES.md` — **the primary contract**:
  - §1.0 indexing, §1.1 Gem types, §1.2.1.5 (constrained fill is initial-board
    only)
  - §3 Match Detection (shape, set semantics, §3.2 deterministic order,
    §3.3 duplicate/shared cells)
  - §4 Cascade (§4.1 per-pass step order, §4.2 loop, §4.3 termination, §4.4
    gravity, §4.5 spawn)
  - §5.2 Match 4 → Line Clear Gem (+ §5.2 items 2, 3: whole line, orientation)
  - §5.3 Match 5+ → Burst Gem (+ §5.3 item 2: 3×3, item 3: Match 6+ is Match 5)
  - §5.4 L/T → Area Gem (+ §5.4 item 4: per-arm tier ladder)
  - §5.5.1 creation order, incl. item 4 intra-L/T order
  - §5.5.2 shape → tier ladder
  - §5.5.3 creation location, incl. item 3 line centre and item 7 arm centre
  - §5.5.4 replacement, reservation, collision (item 2 reservation order)
  - §5.5.5 activation, affected sets, union, chains
  - §5.6 ordering index, §5.7 resource-generation hooks
  - §5.8.1 clipping, §5.8.2 union semantics, §5.8.3 deterministic ordering,
    §5.8.4 multiple creation, §5.8.5 worked examples, §5.8.6 Special+Special
  - §6 Combo (unchanged by Special Gems), §7 RNG, §8 Turn/Sequence
- `docs/01-game-design/GAME_RULES.md` §7 (Special Gems), §13 (chains), §17
  (resolution order), §18 (server authority)
- `docs/01-game-design/COMBAT_RULES.md` §2 — tier multipliers; Special Gem
  activation clears generate at the base rate (`MATCH3_RULES.md` §5.7 item 6)
- `docs/01-game-design/PASSIVE_RULES.md` §2.2, §5 — downstream consumer of Match
  events; do not implement Passive behavior, do not break its inputs
- `docs/01-game-design/RELIC_RULES.md` §4 item 3 — the breadth-first chain
  convention `MATCH3_RULES.md` §5.5.5 item 7 mirrors
- `docs/02-technical/GAME_STATE.md` §2.1 (the `Cells[64]` cell-entry model,
  §2.1.1 occupancy, §2.1.2 no `PendingSpecialGems[]`, §2.1.3 cell occupancy,
  §2.1.4 SpecialGem metadata + orientation, §2.1.5 gravity/spawn movement,
  §2.1.6 deterministic ordering, §2.1.7 serialization, §2.1.8 activation and
  removal, §2.1.9 what the model does not add), §2.2, §2.6, §3 Transient
  Resolution State, §5.1
- `docs/02-technical/GAME_EVENTS.md` §1, §1.1 (board cycle ordering), §1.3
  (`GemMatched` enumeration order), §2 (`MatchCreated` / `GemMatched` payloads)
- `docs/02-technical/ARCHITECTURE.md` §1, §2.1, §4.1, §5 — the cascade loop
  belongs to Domain
- `docs/02-technical/TDD.md` §3, §6
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4, §4.1, §8 item 7 — no new message
  (`SpecialGemActivated` must not exist)
- `docs/02-technical/REDIS_STATE.md` §2, §7 — no new Redis field
- `docs/03-decisions/ADR/ADR-009-deterministic-prng.md` — the single PRNG
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md`

---

## Scope

### In Scope

- Match detection required for Special Gem creation (`MATCH3_RULES.md` §3),
  producing the pass's match set as maximal horizontal/vertical primitives with
  L/T grouping, in the §3.2 deterministic order.
- Special Gem creation: Match 4 → `LineClear`, Match 5+ → `Burst`, L/T →
  `Area`, plus the §5.4 item 4 per-arm tier ladder.
- Creation collision resolution (§5.5.4 items 2–3, §5.8.4 item 4): reservation
  of creation cells before activation, and first-source-in-§5.5.1-order wins.
- Special Gem placement: swap origin, cascade line centre, L/T intersection,
  L/T arm centre (§5.5.3).
- Special Gem activation and affected-set geometry (§5.2 item 2, §5.3 item 2,
  §5.4 item 3) with §5.8.1 clipping.
- Special Gem chain resolution: breadth-first, finite by consumption, no depth
  cap (§5.5.5 item 7, §5.8.3 items 2, 7).
- Cleared-cell union semantics: each cell clears once, generates once, emits
  `GemMatched` once (§5.8.2, §5.5.5 item 6).
- `GemMatched` integration: once per cleared cell, ascending §1.0 cell index
  within the sub-step, pre-removal Special Gem metadata
  (`GAME_EVENTS.md` §1.3, §2).
- Gravity (§4.4) and Spawn (§4.5), including the §2.1.5 item 1 whole-entry
  movement and §4.5 item 7 spawn-never-creates-a-Special-Gem rule.
- Cascade re-resolution (§4.2, §4.3): the loop, depth indexing, termination.
- Authoritative state updates: the resulting `BoardState` and `RngState`
  (`GAME_STATE.md` §2.1.5, §2.6.2).
- Tests at the depth Risk HIGH requires.
- Correcting stale `PendingSpecialGems[]` comments where they misdescribe the
  current model.

### Out of Scope

- Cards triggering Special Gems.
- Relics triggering Special Gems.
- Bosses triggering Special Gems.
- Swap validation, Turn/Sequence counter behavior, and Combo lifecycle beyond
  what Special Gem resolution consumes — those belong to TASK-001
  (`MATCH3_RULES.md` §2, §6, §8). This task does not extend them.
- Resource/damage/combat calculation and multipliers (`COMBAT_RULES.md` §2) —
  this task only exposes the cleared-cell union those systems consume.
- Passive / Relic / Combat / Boss domain behavior.
- PvP, Map, Terrain, Weather, Microservices, Kubernetes, Kafka, new currencies.
- New Special Gem types, new gameplay mechanics, any Special Gem combination
  system (`MATCH3_RULES.md` §5.8.6 item 7).
- Redis persistence and reconnect/resync (`REDIS_STATE.md` §7).
- Full Special Gem client visuals. Only the minimum contract/types update
  needed to keep the presentation layer consistent with the serialized cell
  shape, and it stays presentation-only.
- Any system listed in `docs/00-overview/MVP_SCOPE.md` §2 (OUT).

---

## Current State

```text
src/backend/GameServer.Domain/Match3/
  GemType.cs                    four types + contract names
  BoardState.cs                 Cells[64] as GemType[]; indexing, adjacency,
                                WithSwapped, ToArray. Indexer is get-only.
  Pcg32.cs                      RngState pair + PCG32 (NextUInt32, NextBounded)
  BoardGenerator.cs             initial constrained fill (§1.2.1)
  BoardGenerationValidator.cs   §1.3/§1.4 generation-time checks only
src/backend/GameServer.Domain/Battle/BattleState.cs
                                record: BattleId, Turn, Sequence, RngSeed,
                                RngState, BoardState
src/backend/GameServer.Application/Battle/BattleStateService.cs
                                holds board-foundation state; no resolution API
src/backend/GameServer.Api/Hubs/BattleHub.cs
                                JoinBattle + BattleStateUpdated push only;
                                BoardPayload.Cells is IReadOnlyList<string>
src/frontend/client/src/services/realtime/SignalRService.ts
                                BoardPayload.cells: readonly string[]
```

Match detection, cascade, gravity, spawn, Special Gem model, creation,
activation, and chains do not exist. `BoardState` holds Gem types only; the
Special Gem metadata of `GAME_STATE.md` §2.1.4 is added by this task inside the
cell entry.

Existing verified baseline (run before any change): backend 130 tests pass
(94 Domain + 19 Application + 1 Infrastructure + 16 Api); frontend 167 tests
pass across 12 files.

---

## Acceptance Criteria

- [ ] `BoardState.Cells[64]` carries, per cell, a Gem type plus an optional
      Special Gem with type and — for `LineClear` only — orientation
      (`GAME_STATE.md` §2.1.1, §2.1.4).
- [ ] `BoardState` still has exactly one board collection; no
      `PendingSpecialGems[]`, no `SpecialGems[]`, no `CellIndex`,
      `SpecialGemId`, `CreationId`, `Depth`, `Age`, `Armed`, or
      `ActivationCount` field exists (`GAME_STATE.md` §2.1.2 item 1, §2.1.3
      item 4).
- [ ] A straight Match of exactly 4 creates exactly one `LineClear` Special Gem
      (`MATCH3_RULES.md` §5.2 item 1).
- [ ] A straight Match of 5 or more creates exactly one `Burst` Special Gem,
      with no additional tier for length ≥ 6 (§5.3 items 1, 3).
- [ ] An L/T shape creates exactly one `Area` Special Gem at the intersection,
      plus one gem per arm that independently qualifies at length 4 (`LineClear`)
      or ≥ 5 (`Burst`); a length-3 arm creates nothing (§5.4 items 2, 4).
- [ ] `LineClear` orientation is fixed at creation from the creating line's
      orientation and survives gravity unchanged (§5.2 item 3, §4.4 item 8).
- [ ] Creation placement is: swap origin for a Swap-produced Match 4/5; line
      centre `s + floor((L − 1) / 2) × step` for a Cascade Match 4/5; the
      intersection for an L/T `Area`; the arm's own line centre for an L/T arm
      gem (§5.5.3 items 1, 3, 4, 7).
- [ ] When two creations claim one cell, the first in the §5.5.1 order wins —
      and within one L/T, the §5.5.1 item 4 sequence (`Area`, then horizontal
      arm, then vertical arm). The later claim is not created, stored,
      activated, or reported (§5.5.4 item 3, §5.8.4 item 4).
- [ ] Creation cells are reserved before any activation of the same step runs,
      and an activation's effective set is
      `affected − reserved`, except where the reserved cell is the activated
      gem's own cell (§5.5.4 item 2).
- [ ] Activation geometry is exact: `LineClear` clears its whole row or column
      including its own cell; `Burst` clears the 3×3 square including diagonals;
      `Area` clears its own cell plus the four orthogonal neighbours
      (§5.2 item 2, §5.3 item 2, §5.4 item 3).
- [ ] Affected sets are computed in board coordinates and clipped by discard —
      never wrapped, folded, or clamped onto another cell (§5.8.1).
- [ ] Overlapping effects use union semantics: each cleared cell clears once,
      generates once, and emits `GemMatched` once (§5.8.2, §5.5.5 item 6).
- [ ] Each Special Gem activates at most once per step; chains are breadth-first
      and terminate through consumption with no invented depth cap
      (§5.5.5 item 7, §5.8.3 items 2, 7).
- [ ] A Special Gem created during a resolution pass does not activate during
      that same pass (§4.1 item 4, §5.8.3 item 6).
- [ ] Gravity moves the whole cell entry — Gem type and Special Gem metadata
      together — keeps each gem in its own column, and preserves relative order
      (§4.4 items 1, 3, 8; `GAME_STATE.md` §2.1.5 item 1).
- [ ] Spawn fills the top empty cells of each column in §4.5 item 2 order,
      consumes exactly one RNG selection per spawned cell, and never creates a
      Special Gem (§4.5 items 1, 4, 7).
- [ ] No Special Gem operation consumes RNG — not match detection, creation,
      geometry, collision resolution, activation, chain ordering, or
      `GemMatched` enumeration. Spawn is the only gameplay RNG consumer
      (§7.2).
- [ ] `GemMatched` is emitted once per cleared cell, in ascending §1.0 cell
      index within each §4.1 sub-step, carrying pre-removal Special Gem
      metadata where the cleared cell held one (`GAME_EVENTS.md` §1.3, §2).
- [ ] No `SpecialGemActivated`, `SpecialGemCreated`, or `SpecialGemDestroyed`
      event, message, method, or Redis field is introduced
      (`MATCH3_RULES.md` §5.9.4 item 7, `SIGNALR_PROTOCOL.md` §8 item 7).
- [ ] The same board plus the same state yields the same resolution — same final
      board, same `RngState`, same cleared union, same `GemMatched` order
      (§7.1, §4.6).
- [ ] Match / Combo / Cascade semantics are unchanged: a Special Gem activation
      is not a Match, does not increment Combo or the Match count, and no
      cascade or Special Gem multiplier is introduced (§5.5.5 item 8,
      §6.3.1, §5.7 item 7).
- [ ] Existing Board Foundation behavior and tests remain intact.
- [ ] All relevant tests pass at the depth required by `core/validation.md §2`
      for Risk HIGH.
- [ ] `quality/review.md §1` checklist passes.
- [ ] Documentation impact addressed (see below).

---

## Affected Areas

```text
[x] Domain (GameServer.Domain/) — Match3: Special Gem model, detector,
                                  resolver, gravity, spawn
[ ] Application (GameServer.Application/)
[ ] API (GameServer.Api/)
[x] Client (client/) — types only, if the serialized cell shape requires it;
                       presentation-only, no gameplay logic
[ ] SignalR / Redis (GameServer.Infrastructure/SignalR/, /Redis/) — no new
                       message or record
[ ] PostgreSQL (GameServer.Infrastructure/Postgres/)
[x] Tests (tests/)
[ ] Documentation (docs/) — see Documentation Impact
[ ] ADR (docs/03-decisions/ADR/)
```

---

## Implementation Notes

1. **The cascade loop is Domain.** `MATCH3_RULES.md` §4 is board behavior;
   `ARCHITECTURE.md` §4.1 places it in the Match-3 domain module. No gameplay
   calculation belongs in `BattleHub`, the client, or Redis infrastructure.
2. **Do not reuse `BoardGenerator` for spawn.** `MATCH3_RULES.md` §1.2.1.5 and
   §4.5 item 3 are explicit: the constrained fill is initial-board only. A
   cascade spawn is an independent uniform draw from all four types, one
   selection per cell.
3. **Do not reuse `BoardGenerationValidator` as the gameplay detector.** Its
   scope is bounded by §1.4.1/§1.4.2 to the two initialization-time constraints.
   The gameplay detector implements §3 in full — match set, ordering, shapes.
4. **One draw site.** `RngState` advances in exactly one gameplay place (spawn).
   If a second draw site appears, it contradicts §7.2.
5. **Existing primitives to reuse:** `BoardState.WithSwapped` (the documented
   "simulate the swap on a copy" primitive), `BoardState.ToIndex/ToRow/ToColumn`,
   `BoardState.AreOrthogonallyAdjacent`, and `Pcg32.NextBounded` (the canonical
   `pcg32_boundedrand_r` the §7.3 reduction requires). Do not add a second
   generator or a second coordinate convention.
6. **Transient bookkeeping stays transient.** Reservation sets, required
   creations, the cleared union, and the consumed-Special-Gem queue are pass-local
   (`GAME_STATE.md` §3). They must not become `BattleState` fields and must not
   become a second authoritative board.
7. **`BoardState.Cells` is currently a live `IReadOnlyList<GemType>` over the
   internal array and the indexer is get-only.** Extending the cell entry must
   preserve the documented accessors the existing tests rely on
   (`board[i]`, `Cells.Count`, `ToArray()`, `FromCells`, `FromValues`,
   `WithSwapped`), or update them deliberately.
8. **`BoardResponse`/`BoardPayload` currently sends `Cells` as
   `IReadOnlyList<string>`** (`BattleHub`). If the cell shape must carry Special
   Gem state on the wire, update that projection and the matching client type,
   keeping it a one-to-one projection with no client-side gameplay
   (`SIGNALR_PROTOCOL.md` §4 item 4).
9. **`ApiIntegrationTests.cs` uses three different top-level field-name
   assertions:** the §4.1 item 5 `pendingSpecialGems` absence check (line ~288),
   the board-is-64-cells check (line ~274), and the top-level field blocklist at
   lines 389–399 (`power`, `hp`, `status`, …) plus the exact top-level field-set
   assertion at lines 402–404. Those last two assert on the **top-level**
   envelope fields, not on cell contents — confirm that remains what they test
   after any board wire-shape change, and do not weaken them.
10. **Stale comments to correct:** `BoardState.cs`, `BattleState.cs`,
    `BattleHub.cs`, and `SignalRService.ts` still describe
    `PendingSpecialGems[]` as a deferred-but-eventual `BoardState` field. It no
    longer exists in any form (`GAME_STATE.md` §2.1.2 item 1). Correct the
    wording; do not touch the name-absence assertions themselves
    (`BoardStateTests.cs`, `BattleStateTests.cs`), which remain valid.

---

## Testing Requirements

### Test Types Required

```text
[x] Unit tests         — detection and match-set ordering; tier classification
                         per shape and per arm; creation placement (swap origin,
                         line centre, L/T intersection, arm centre, even/odd
                         length); collision resolution; all three activation
                         geometries incl. corner/edge/center clipping; union
                         accounting; gravity column/row order and whole-entry
                         movement; spawn position, draw count, and no Special Gem
[x] Integration tests  — full pass pipeline: detect → create → activate →
                         gravity → spawn → re-detect; Special→Special chains;
                         newly created gem inert in its creating pass
[x] Gameplay scenarios — Given/When/Then state-transition chains per
                         GAME_RULES.md §17 for the Special Gem portion, plus the
                         MATCH3_RULES.md §5.8.5 worked examples
[ ] API tests          — n/a (no new endpoint; API_CONTRACTS.md unchanged)
[ ] Realtime tests     — n/a (no new message; existing push shape only)
[ ] Persistence tests  — n/a (no persistence at this stage, REDIS_STATE.md §7)
```

### Key Edge Cases

- `MATCH3_RULES.md` §5.3 item 3 — a run of 6+ is one Burst Gem, not a new tier,
  and still counts as exactly one Match.
- `MATCH3_RULES.md` §5.4 item 4 item 2 — both arms ≥ 4 ⇒ three Special Gems.
- `MATCH3_RULES.md` §5.5.1 item 4 item 5 / §5.8.5 — the canonical L/T collision:
  horizontal arm row 2 columns 0..4 (length 5) + vertical arm column 2 rows 2..4
  (length 3), intersection index 18 ⇒ the `Area` gem wins at 18 and the arm's
  `Burst` is discarded.
- `MATCH3_RULES.md` §5.5.3 item 7 item 3 — an even-length arm selects the
  lower-indexed of the two middle cells; a length-4 arm selects the cell one
  along, not two.
- `MATCH3_RULES.md` §5.5.3 item 7 item 5 — placement uses the arm's **actual**
  length, never a Match-5-truncated length.
- `MATCH3_RULES.md` §5.8.1 item 5 — Burst at a corner clears 4 cells, on an edge
  6, interior 9; Area at a corner 3, edge 4, interior 5 — as consequences of
  clipping, not special cases.
- `MATCH3_RULES.md` §5.8.2 item 3 — two effects naming one Special Gem's cell
  ⇒ it activates once.
- `MATCH3_RULES.md` §5.8.3 item 6 — a newly created Special Gem never activates
  in its creating pass, but may in a later pass of the same Swap.
- `MATCH3_RULES.md` §4.1 item 4 / §3.3 item 2 — an L/T counts as one Match and
  the shared cell is removed once.
- `MATCH3_RULES.md` §5.2 item 2 — `LineClear` clears all 8 cells of the line
  including its own cell.
- `GAME_EVENTS.md` §1.3 item 7 — a descending or repeating `GemMatched`
  sequence is an ordering defect.
- `MATCH3_RULES.md` §4.5 item 7 — a spawned cell never carries a Special Gem,
  including a cell whose Special Gem was just consumed.

---

## Documentation Impact

**Option A — None:**

> This task implements already-documented behavior. `MATCH3_RULES.md` §3–§5,
> `GAME_STATE.md` §2.1, and `GAME_EVENTS.md` §1.3/§2 define the full contract,
> and the design/readiness audit already returned
> `READY — SPECIAL GEM IMPLEMENTATION`. No doc changes are required.
>
> Exception: stale comments *in code* that describe the removed
> `PendingSpecialGems[]` field are corrected as part of this task (they are
> implementation comments, not documentation). If implementation reveals a
> genuine documentation discrepancy, STOP per `AGENTS.md §7` /
> `.ai/README.md §13` and report it — do not edit docs to match whatever the code
> happens to do (`.ai/README.md §18`).

---

## Stop Conditions

- If the required behavior cannot be fully derived from the Authoritative
  Sources above: STOP per `AGENTS.md §7`.
- If a genuine gameplay contradiction is found between authoritative documents
  (e.g. one says a gem activates immediately and another says it waits a pass;
  or two documents require different state representations): STOP per
  `AGENTS.md §4` and report with the `STOP — DOCUMENTATION CONTRADICTION`
  format. Do not silently choose one.
- If a Special Gem placement case arises that `MATCH3_RULES.md` §5.5.3 does not
  cover: STOP.
- If implementation appears to need a cascade cap, a chain depth cap, or any
  other safety bound not already documented: STOP
  (`MATCH3_RULES.md` §4 item 5 and §5.8.3 item 7 forbid inventing one).
- If implementation appears to need a `BattleState` field the canonical model
  does not have: STOP (`GAME_STATE.md` §2.1.2 item 1).
- If a second RNG draw site appears necessary: STOP (`MATCH3_RULES.md` §7.2).

---

## Dependencies

- None. The Board Foundation stage is DONE and verified.
- TASK-001 (Swap validation, Turn/Sequence, Combo) is a separate task and is not
  a prerequisite for the Special Gem resolution contract implemented here.

---

## Completion Evidence

### Summary

Implemented deterministic Special Gem Resolution on top of the existing Board
Foundation, as documented and without inventing gameplay behavior.

The Special Gem state model was added **inside** `BoardState.Cells[64]`: each entry
is now a `Cell` carrying a `GemType` plus an optional `SpecialGem` (type, and
orientation for a Line Clear Gem). `BoardState` still has exactly one field
(`Cell[] _cells`) and exactly one board collection property (`Cells`) — no
`PendingSpecialGems[]`, no `SpecialGems[]`, and none of the forbidden per-cell
fields.

Implemented components:

- **`MatchDetector`** — §3 Match Detection: maximal horizontal/vertical primitives,
  L/T grouping (two same-type perpendicular primitives sharing exactly one cell),
  the §3.2 order (horizontal before vertical, then ascending start index; L/T at its
  intersection), and the cleared-cell union.
- **`SpecialGemPlanner`** — §5.5.1–§5.5.4: the tier ladder (4 → `LineClear`,
  5+ → `Burst`, L/T → `Area` plus one gem per qualifying arm), placement (swap
  origin, line centre, arm centre, intersection), reservation of creation cells
  before activation, and first-in-order collision resolution.
- **`SpecialGemEffects`** — §5.2 item 2 / §5.3 item 2 / §5.4 item 3 geometry with
  §5.8.1 clipping by discard. One offset table per type; no corner/edge/interior
  branching.
- **`BoardResolver`** — the §4.1 step order: Match Resolution → activation (with
  breadth-first chains) → creation → Gravity → Spawn. Union semantics, reservation,
  and `GemMatched` reports.
- **`Gravity` / `Spawn`** — §4.4 and §4.5, moving whole cell entries and consuming
  exactly one RNG selection per spawned cell.
- **`CascadeResolver`** — the §4.2 loop with §4.3 termination and no depth cap.
- **`BattleStateService.ResolveBoard`** — the Application-layer boundary ordering the
  documented calls; it implements no game rule and writes no Battle Event.
- **`BattleHub` / `SignalRService`** — the minimal presentation contract update so
  the existing §4 push carries each cell entry with its optional Special Gem, with no
  new endpoint, message, method, or Redis record.

### Changes

**Backend — Domain (`src/backend/GameServer.Domain/Match3/`)**
- `SpecialGem.cs` (new) — `SpecialGemType`, `SpecialGemOrientation`, `SpecialGem`.
- `Cell.cs` (new) — the `Cells[64]` entry shape.
- `BoardState.cs` (rewritten) — cell entries, `SpecialGemAt`, `FromCellEntries`,
  `WithCells`, `CellsEqual`, `ToCellArray`, `IsOnBoard`; all prior accessors
  preserved.
- `MatchPrimitive.cs`, `MatchShape.cs`, `MatchDetector.cs` (new) — detection.
- `SpecialGemClaim.cs`, `SpecialGemPlanner.cs` (new) — creation, placement, collision.
- `SpecialGemEffects.cs` (new) — activation geometry.
- `ResolutionEvents.cs` (new) — `GemMatchedEvent`, `MatchResolution`.
- `BoardResolver.cs` (new) — the pass pipeline and BFS chain resolution.
- `GravityAndSpawn.cs` (new) — `Gravity`, `GravityResult`, `Spawn`, `SpawnResult`.
- `CascadeResolver.cs` (new) — the cascade loop.

**Backend — other**
- `GameServer.Domain/Battle/BattleState.cs` — corrected a stale comment; no field
  change (the record's six documented fields are unchanged).
- `GameServer.Application/Battle/BattleStateService.cs` — added `ResolveBoard`.
- `GameServer.Api/Hubs/BattleHub.cs` — `BoardPayload.Cells` is now
  `IReadOnlyList<CellPayload>` with `CellPayload` / `SpecialGemPayload`.

**Frontend (presentation-only)**
- `src/frontend/client/src/services/realtime/SignalRService.ts` — `BoardPayload.cells`
  is now `readonly CellPayload[]`, plus the `CellPayload` / `SpecialGemPayload` types.
  No gameplay logic, no new method, no client authority.

**Tests**
- `BoardStateSerializationTests.cs`, `CascadeAndDeterminismTests.cs`,
  `GravityAndSpawnTests.cs`, `MatchDetectorTests.cs`, `SpecialGemActivationTests.cs`,
  `SpecialGemCreationTests.cs`, `SpecialGemEdgeCaseTests.cs`,
  `SpecialGemGeometryTests.cs`, `SpecialGemScenarioTests.cs`, `TestBoard.cs` (all new).
- `BoardStateTests.cs`, `BattleStateTests.cs`, `BoardGeneratorTests.cs`,
  `ApiIntegrationTests.cs`, `BattleStateServiceTests.cs`,
  `tests/SignalRService.test.ts` (updated).

**Task**
- `tasks/active/TASK-002-special-gem-resolution.md` (this file).

### Tests

Added 217 new backend tests and 2 new frontend tests:

- `MatchDetectorTests` (16) — runs, maximality, L/T grouping, §3.2 order, determinism,
  cleared union.
- `SpecialGemCreationTests` (48) — Match 4, Match 5+, the documented ladder,
  L/T arms (none / length 4 / 5+ / both), arm tier precedence, placement
  (swap origin, line centre, even/odd, arm centre, step 8, actual length),
  collision (including the canonical §5.5.1 item 4 item 5 case), reservation.
- `SpecialGemGeometryTests` (29) — all three geometries at corner/edge/interior,
  clipping (no wrap/fold/clamp), own-cell inclusion, malformed-gem rejection.
- `SpecialGemActivationTests` (31) — trigger, affected sets, reservation protection,
  newly-created gems inert in their creating pass, chains (BFS levels, sibling order,
  once-per-step, termination), union semantics, `GemMatched` ordering and payload.
- `GravityAndSpawnTests` (25) — column/row order, order preservation, whole-entry
  movement, orientation survival, no RNG, vacated-cell reporting, spawn position and
  draw count, spawn never creates a Special Gem.
- `CascadeAndDeterminismTests` (26) — loop and termination, depth indexing, no cap,
  reproducibility, RNG-discipline reflection checks, the single-board-collection and
  forbidden-field gates, round-trip.
- `SpecialGemScenarioTests` (11) — the §5.8.5 worked examples and the §5.8.6
  interaction matrix as full state-transition chains.
- `SpecialGemEdgeCaseTests` (12) — the seams between rules (match-set + blast
  consumption, chain levels, reservation under chains, union composition).
- `BoardStateSerializationTests` (12) — the documented serialization shape and
  lossless round-trip.
- Application (7) and integration (2) tests for the resolution boundary and the §4.1
  item 5 wire contract.
- Frontend (2) — cell entries with Special Gems delivered unchanged; no Special Gem
  gameplay method on the client.

### Documentation Consulted

- `docs/00-overview/MVP_SCOPE.md` §1, §2
- `docs/01-game-design/MATCH3_RULES.md` §1.0, §1.1, §1.2.1.5, §2.1, §3–§8 (in full,
  with §5.2–§5.9 read line by line)
- `docs/01-game-design/GAME_RULES.md` §7, §13, §17, §18
- `docs/01-game-design/COMBAT_RULES.md` §2
- `docs/01-game-design/PASSIVE_RULES.md` §2.2, §5
- `docs/01-game-design/RELIC_RULES.md` §4 item 3
- `docs/02-technical/GAME_STATE.md` §2.0.5, §2.1 (in full), §2.2, §2.6, §3, §5.1
- `docs/02-technical/GAME_EVENTS.md` §1, §1.1, §1.2, §1.3, §2
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4, §4.1 items 5–6, §8 item 7
- `docs/02-technical/REDIS_STATE.md` §2, §7
- `docs/02-technical/ARCHITECTURE.md` §1, §2.1, §4, §5
- `docs/02-technical/TDD.md` §3, §6
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md`
- `docs/03-decisions/ADR/ADR-009-deterministic-prng.md`
- `AGENTS.md`, `docs/AGENTS.md`, `.ai/README.md`, `.ai/workflow/**`, `.ai/agents/**`,
  `.ai/skills/**`, `tasks/TASK_LIFECYCLE.md`, `tasks/TASK_TEMPLATE.md`,
  `tasks/TASK_TYPES.md`

### Documentation Changed

**None.** No file under `docs/` was modified by this task. The documentation already
owned the complete Special Gem contract (`MATCH3_RULES.md` §5.9.5 declares "zero
unresolved Special Gem gameplay rules"), and implementation confirmed it. Stale
comments referring to the removed `PendingSpecialGems[]` field were corrected **in
code and tests**, which are implementation comments rather than documentation.

No new ADR was required: this task implements existing documented behavior within
the existing architecture and adds no architectural decision.

### Validation

```text
Backend build          dotnet build src/backend/GameServer.sln
                       → Build succeeded, 0 Warning(s), 0 Error(s)

Backend tests          dotnet test src/backend/GameServer.sln
                       → GameServer.Domain.Tests          311 passed, 0 failed
                       → GameServer.Application.Tests      26 passed, 0 failed
                       → GameServer.Infrastructure.Tests    1 passed, 0 failed
                       → GameServer.Api.Tests              18 passed, 0 failed
                       → total 356 passed, 0 failed

Frontend tests         npm run test:run  (client/)
                       → 12 files, 169 passed, 0 failed

TypeScript             npx tsc --noEmit  (client/)
                       → clean, no errors

Frontend build         npm run build  (client/)
                       → built successfully
```

Baseline before this task: 130 backend tests (94 Domain + 19 Application +
1 Infrastructure + 16 Api) and 167 frontend tests, all passing. Every baseline test
still passes; the count grew to 356 / 169.

Validation depth: Risk HIGH per `core/validation.md` §2 — unit tests, integration
tests, gameplay scenarios (Given/When/Then full state transitions), determinism
assertions, architecture-boundary checks, RNG-discipline checks, and a serialization
round-trip. An independent adversarial review was performed (see Risks).

No commit, push, or `git` state change was made: the working tree is left as it was
found plus these edits.

### Risks

1. **An independent adversarial review found and this task fixed two ordering
   defects** (both in activation reporting, none affecting the board):
   - Level-0 activations were ordered by ascending cell index, but
     `MATCH3_RULES.md` §5.5.5 item 5 and §5.8.3 level 3 require §3.2 **match-set**
     order. These differ when a vertical shape has a lower start index than a
     horizontal one. The final board was unaffected because clearing is
     union-based; the reported activation sequence was wrong.
   - Level-1 chain discoveries were collected in a `SortedSet`, discarding the
     consumer order §5.8.3 level 3a requires.
   Both are fixed and covered by two new regression tests
   (`LevelZeroActivations_ShouldFollowMatchSetOrder_NotCellIndexOrder`,
   `ChainLevelOne_ShouldFollowTheActivationOrderOfItsConsumers`).

2. **One documented ambiguity was resolved as an implementation decision and is
   reported, not hidden.** `MATCH3_RULES.md` §3.2 defines only two ordering keys
   (horizontal, vertical) and an L/T is neither, so §3.2 does not state where an L/T
   sorts relative to straight shapes. This implementation places an L/T in the
   **horizontal** group at its intersection cell (the alternative reading sorts the
   L/T purely by intersection index among all shapes). The choice is documented
   explicitly at `MatchShape.SortKey`. It affects only the reported order, never the
   cleared set or the board. If a rule is ever authored for it, that rule replaces
   the choice.

3. **`GravityResult.Board` carries placeholder Gem types in its vacated cells.** This
   is required — `MATCH3_RULES.md` §4.4 item 5 forbids a sentinel entering the
   four-type domain — and is sound because `Spawn.Fill` overwrites exactly
   `GravityResult.VacatedCells` in the same pass. The pairing is now documented on the
   record and guarded by two `Debug.Assert`s, and `Spawn.Fill` has no
   "fill everything" overload, so the stale state cannot be published by accident.

4. **`Turn`, `Sequence`, and Combo are not implemented by this task.** Board
   resolution is not a Swap (`MATCH3_RULES.md` §8.1 item 4), so `ResolveBoard` leaves
   both counters unchanged and writes no Battle Event. The Swap action, its
   validation, the counter update order (§8.3), and the Combo lifecycle (§6) remain
   TASK-001's scope. Any event-batch delivery for resolved boards is likewise
   TASK-001's (`SIGNALR_PROTOCOL.md` §3.1).

5. **Resource generation is not computed here.** `MATCH3_RULES.md` §5.7 item 5 places
   generation in `COMBAT_RULES.md`; this task exposes the cleared-cell union it must
   consume and applies no rate, tier multiplier, or cascade bonus.
   `ClearedCellUnion` is the documented interface for it.

6. **The board wire shape changed** from `string[]` to cell objects. This is what
   `SIGNALR_PROTOCOL.md` §4.1 item 5 requires ("the board is delivered as the state
   holds it, entry for entry"), and it adds no payload member and no message. The
   client type was updated to match, presentation-only. The three pre-existing
   top-level field assertions in `ApiIntegrationTests` were confirmed still to test
   what they intended (they guard the state envelope, not the board's interior) and
   were strengthened, not weakened.

### Remaining Issues

Discovered during this task but **out of scope**, reported per `AGENTS.md` §16:

1. **`GameServer.slnx` is empty** (`<Solution></Solution>`, two lines). `dotnet`
   commands against it report "no projects found" rather than an error, which is an
   easy way to believe tests ran when none did. `GameServer.sln` is the real solution.
   *Suggested follow-up:* remove the file or populate it.

2. **`ArchitectureTests.cs` enforces no architecture rules.** It is 18 lines and only
   asserts that two assembly-marker types resolve. The Domain→Application/Api/
   Infrastructure layering is enforced by project references alone, so a layering
   violation would not be caught. I verified the boundaries manually (Domain
   references no Application/Api/Infrastructure type; Api contains no gameplay call).
   *Suggested follow-up:* a task to add real boundary assertions.

3. **`src/frontend/client/.env.local` exists and is gitignored**, and
   `appsettings.Development.json` is gitignored. Neither was inspected or changed.

4. **The pre-existing Vite chunk-size warning** (>500 kB) is unrelated to this task
   and was present before it.

### Agent

Gameplay (primary), with Backend, Testing, and Review responsibilities discharged in
the same session. All work was performed by the orchestrating agent; a separate
subagent performed the independent adversarial review, and two subagents performed
read-only codebase and build-system discovery.

### Workflow Used

`development/feature.md`, following the repository lifecycle
`CONTEXT → PLAN → IMPLEMENT → TEST → REVIEW → DOCUMENTATION CHECK → COMPLETION`.

### Skills Used

`gameplay-behavior-derivation`, `authority-determinism-audit`,
`test-scenario-generation`, `implementation-review`, `scope-validation`,
`architecture-conformance`, `documentation-consistency`.

### Status

DONE

---

## Handoff

No handoff required — the task is complete.

For the next agent picking up the Match-3 work: **TASK-001** (`tasks/backlog/`,
Swap validation, Turn/Sequence, Combo, event batches) remains READY and
unimplemented. The board-resolution contract it needs now exists and is tested:
`CascadeResolver.Resolve(board, rng, swapOriginIndex)`,
`BattleStateService.ResolveBoard(battleId, swapOriginIndex)`, and the
`PassResult` reports (`Matches`, `MatchedCellGemMatched`,
`ActivationCellGemMatched`, `ActivatedSpecialGems`, `CreatedSpecialGems`,
`ClearedCellUnion`). The swap origin for the first pass of a Swap is what
`ResolveBoard`'s `swapOriginIndex` parameter is for (`MATCH3_RULES.md` §5.5.3
item 1); passing `null` places every Match 4 / Match 5 at its line centre.