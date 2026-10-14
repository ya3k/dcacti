# TASK-276 — Authoritative Cascade Snapshot Protocol Planning

---

## Metadata

```text
Task ID:           TASK-276
Type:              ARCHITECTURE / PROTOCOL PLANNING
Status:            BACKLOG
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     architect
Supporting Agents: realtime, client, testing, review
Workflow:          architecture/adr.md
Skills:            realtime/realtime-protocol-validation, client/client-state-authority,
                   client/phaser-battle-presentation, dotnet/dotnet-backend-patterns,
                   testing/test-scenario-generation, quality/implementation-review
Dependencies:      TASK-254 (DONE), TASK-272 (IN REVIEW), TASK-273 (DONE),
                   TASK-274 (DONE), TASK-275 (IN REVIEW — CONDITIONAL PASS)
Declared Files:    tasks/backlog/TASK-276-authoritative-cascade-snapshot-protocol-planning.md
```

---

## 1. Executive Summary & Mission

### 1.1 Mission
Investigate and design an implementation-ready protocol specification for **TASK-254 DR-1 option B**, enabling authoritative per-pass gravity/drop and refill/spawn presentation in the Phaser 4.1.0 battle scene without client-side match-3 simulation, without weakening TASK-273/TASK-274 sequence and recovery boundaries, and without altering the single-writer Redis persistence architecture.

### 1.2 The Core Problem
Under the current protocol:
1. `SIGNALR_PROTOCOL.md` §3.1 item 2 specifies that intermediate states of a board resolution are never sent.
2. `GAME_STATE.md` §3, §5.1 item 2, and §5.3 item 4 specify that intermediate cascade states are transient resolution state, never stored in Redis, and never included in snapshots.
3. `MATCH3_RULES.md` §4.4 item 7 specifies that gravity produces no events.
4. Consequently, as proven by TASK-275:
   - Match detection and cleared cells are delivered (`MatchCreated.cells`, `GemMatched.cellIndex`).
   - Cascade pass sequencing is delivered (`CascadeCreated.cascadeDepth`).
   - **Source-to-destination movements of falling gems are NOT delivered.**
   - **Spawned gem types and their destination cells are NOT delivered.**
   - **Intermediate board configurations between cascade passes are NOT delivered.**
5. In TASK-275, confirmed gem destruction could only be validated against the rendered board for Pass 1 (depth 1). For Pass 2+ (depth $\ge 2$), because no intermediate gravity/spawn data reached the client, the client's rendered board did not match the server's post-gravity/spawn state. Per `AGENTS.md` §10 (zero client fabrication), `clearConfirmedCell` refused to clear mismatched cells, and refill entrance was forced to collapse into a single transition at the end of the round (`settle` phase).

### 1.3 Recommended Protocol Solution: `CascadeStepResolved` Event
We recommend adding **one dedicated event discriminator** to `ReceiveEvents.events[]`: `CascadeStepResolved` (or `BoardStepResolved`).
- It carries:
  1. `passDepth` (int): 1-based index of the detection pass ($1, 2, 3, \dots$).
  2. `actor` (string): `"player"` or `"boss"`, matching `SwapStarted.actor`.
  3. `dropMoves` (array): Explicit list of `{ fromCell, toCell }` for all surviving gems that fell.
  4. `spawnedGems` (array): Explicit list of `{ cellIndex, gemType, specialGem? }` for all newly spawned gems.
  5. `board` (object): The authoritative 64-cell board configuration resulting from this pass (using the existing `BoardPayload` schema).
- This solution requires **zero changes to the `ReceiveEvents` 3-member envelope**, **zero mid-resolution network pushes**, **zero changes to Redis CAS persistence**, and **zero changes to snapshot recovery**.

---

## 2. Confirmed Current Behavior & Baseline Evidence

### 2.1 Authoritative Contract References
- **`SIGNALR_PROTOCOL.md` §3**:
  `ReceiveEvents(battleId, serverSequence, events[])`. Envelope consists strictly of 3 properties.
- **`SIGNALR_PROTOCOL.md` §3.1 item 1**:
  State write-back precedes event delivery.
- **`SIGNALR_PROTOCOL.md` §3.1 item 2**:
  "No per-step delivery. The intermediate states of a resolution (`MATCH3_RULES.md` §4.1) are Transient Resolution State (`GAME_STATE.md` §3) and are never sent. There is no 'gravity' message, no per-pass message, and no partial batch."
- **`SIGNALR_PROTOCOL.md` §3.1 item 5**:
  "The client stays non-authoritative... it does not compute matches, Cascades, Combo, Special Gems, gravity results, or spawned Gems, and never draws from `rngSeed`/`rngState`."
- **`SIGNALR_PROTOCOL.md` §3.2.12 item 1**:
  Closed discriminator set with no board-per-event payload.
- **`SIGNALR_PROTOCOL.md` §6 item 3 & §7**:
  Sequence desync detection, withholding batches ahead of held sequence, snapshot supersession without event replay.
- **`GAME_STATE.md` §3**:
  `ResolutionContext` and pass-local board bookkeeping exist only during execution and are discarded.
- **`GAME_STATE.md` §5.1 item 2**:
  "Nothing is written mid-resolution."
- **`GAME_STATE.md` §5.3 item 4**:
  "A snapshot carries the board and not the cascade history."
- **`MATCH3_RULES.md` §4.4 item 7**:
  "Gravity produces no Match, no Combo, and no events. It only relocates Gems; the board is re-examined by the next detection pass."
- **`ADR-026` Decision 2 & 4**:
  Atomic round model where Player swap and Boss response resolve sequentially within one round, persisting via a single Redis CAS with `Sequence++`.

### 2.2 Backend Cascade Pipeline Evidence
Inspection of the backend codebase at HEAD reveals:
1. `src/backend/GameServer.Domain/Match3/BoardResolver.cs` (`ResolvePass`, lines 164–275):
   - Step 1: Clears matched cells union (`matchedUnion`).
   - Step 2: Gathers consumed special gems, resolves activations, plans and commits special gem creations (`afterStep2`).
   - Step 3: Calls `Gravity.Apply(afterStep2, empties)`.
   - Step 4: Calls `Spawn.Fill(gravity.Board, rng, vacatedCells)`.
   - Returns `PassResult(Board: spawn.Board, ...)`.
2. `src/backend/GameServer.Domain/Match3/GravityAndSpawn.cs`:
   - `Gravity.Apply` (lines 49–152): Iterates columns $0 \dots 7$, bottom-to-top (rows $7 \dots 0$). It reads from `readIndex` and writes surviving gems down to `writeIndex`. It computes `vacated` cell indices. **It calculates the exact source-to-destination moves (`readIndex -> writeIndex`), but discards the move pairings from `GravityResult`.**
   - `Spawn.Fill` (lines 224–273): Fills vacated cells top-to-bottom with newly drawn gems from `rng`. It creates `CreatedCells` (`IReadOnlyList<(int Index, Cell Cell)>`), and returns `SpawnResult(Board, CreatedCells, SpawnedCount)`. **`PassResult` discards `CreatedCells` and only retains `spawn.Board`.**
3. `src/backend/GameServer.Domain/Match3/CascadeResolver.cs` (`Resolve`, lines 83–125):
   - Loops `BoardResolver.ResolvePass` until `MatchDetector.Detect(currentBoard).Count == 0`.
   - Collects each `PassResult` into `CascadeResult.Passes`.
4. `src/backend/GameServer.Domain/Match3/BattleEventBuilder.cs` (`Build`, lines 84–152):
   - Translates `CascadeResult.Passes` into `BattleEvent` records.
   - Emits `BattleEvent.ForCascade(depth - 1)` (for depth $\ge 2$).
   - Emits `BattleEvent.ForMatch(match)`.
   - Emits `BattleEvent.ForGem(gem)` for matched cells and activation cells.
   - Emits `BattleEvent.ForCombo(combo)`.
   - **Emits NO event for gravity moves, spawned gems, or intermediate boards.**
5. `src/backend/GameServer.Application/Battle/BattleStateService.cs` (`ResolveSwapAsync`, lines 1382–1830):
   - Resolves Player swap cascades: `SwapExecutor.Execute` -> `CascadeResolver.Resolve`.
   - Resolves Boss board action cascades: `bossResolution = CascadeResolver.Resolve(...)`.
   - Concatenates events into one atomic round list.
6. `src/backend/GameServer.Api/Hubs/BattleHub.cs` (`Swap`, lines 1144–1146):
   - Broadcasts `BattleStateUpdated(state)` (containing the final committed board).
   - Broadcasts `ReceiveEvents(battleId, serverSequence, events)`.

### 2.3 Frontend Presentation Evidence
Inspection of the client codebase at HEAD reveals:
1. `src/frontend/client/src/game/runtime/GameRuntime.ts` (`forwardBattleEvents`, lines 1691–1741):
   - Verifies `envelope.battleId === held.battleId`.
   - Compares `envelope.serverSequence` with `held.sequence`:
     - If equal or lower: forwards batch to `battleListeners`.
     - If higher: withholds batch as a sequence desync and calls `requestBattleStateRecovery()`.
2. `src/frontend/client/src/game/scenes/BattleScene.ts`:
   - `renderBattleState` (lines 2083–2120): If presentation is active or pending, holds incoming authoritative state in `heldResolutionState` / `heldSequence`, preventing early reveal of the final board.
   - `buildPresentationPhases` (lines 4412–4645): Maps delivered events to phases (`banner`, `swap`, `match`, `cascade`, `damage`, `retaliation`, `boss-skill`, `settle`, `feedback`, `outcome`). Slices in exactly one `'settle'` phase at the end of the round's board cycles.
   - `playPresentationPhase` (lines 4874–4938):
     - For `'match'` / `'cascade'`: highlights matches and calls `presentMatchDestruction(phase.events)`.
     - `clearConfirmedCell` (lines 5585–5630): Only clears a cell if the delivered event's `gemType` matches the rendered gem view's current content. For Pass 1 this succeeds; for Pass 2+ this fails because the rendered board was never updated with Pass 1's gravity and refills!
     - For `'settle'`: calls `renderBoard(currentBattleState.board, true)`, performing bulk refill from the final board.
   - `settleHeldPresentation` (lines 5218–5250): Converges HUD and board to `currentBattleState` upon timeline completion or abort.

---

## 3. Approved TASK-254 DR-1 Option B Decisions vs Unresolved Assumptions

### 3.1 Approved Decisions (Non-Negotiable Constraints)
1. **Server Authority (`AGENTS.md` §10, ADR-001)**:
   The client must never simulate or calculate match detection, gravity drop destinations, spawned gem types, or intermediate board configurations. All presentation data must be directly grounded in authoritative server payloads.
2. **Single Atomic Round & Persistence Boundary (`GAME_STATE.md` §5.1, ADR-026)**:
   A battle round (Player action + cascades + Boss response + cascades + status ticks) commits to Redis as **one single atomic write-back** under `Sequence = S + 1`. No intermediate cascade state is ever written to Redis.
3. **No Mid-Resolution Interactive Network Pushes (`SIGNALR_PROTOCOL.md` §3.1 item 2)**:
   The server does not send step-by-step messages mid-resolution awaiting client animations or ACKs. All resolution data for the round arrives in the single atomic `ReceiveEvents` message.
4. **Snapshot Recovery Invariant (`ADR-008`, `GAME_STATE.md` §5.3 item 4, `SIGNALR_PROTOCOL.md` §7)**:
   Reconnect / resynchronization via `GetBattleState` delivers only the current settled authoritative state (`BoardState`, `Sequence`, `PetState`, `BossState`). It does NOT carry cascade history and NEVER replays missed intermediate cascade animations.

### 3.2 Unresolved Assumptions (Explicitly Preserved for Operator/PO Decision)
1. **Packaging Location**:
   - *Option A (Recommended)*: Dedicated event discriminator (`CascadeStepResolved`) inside `ReceiveEvents.events[]`.
   - *Option B*: Top-level property array on `ReceiveEventsPayload` (e.g. `cascadeSteps[]`).
   - *Option C*: Sub-property attached only to `CascadeCreated` (Note: Infeasible because Pass 1 has no `CascadeCreated`).
2. **Payload Content (B1 vs B2 vs Hybrid)**:
   - *B1 (Board Snapshot only)*: Delivers full 64-cell board for each pass. Client must derive which gem moved where (conflicts with PD-8 / client-derivation restrictions).
   - *B2 (Transition Delta only)*: Delivers `dropMoves` and `spawnedGems` only. Lacks a post-pass full board verification anchor.
   - *Hybrid (Recommended)*: Delivers `dropMoves` + `spawnedGems` + `board` (full 64 cells). Delivers explicit animation instructions while providing an authoritative board state anchor.
3. **Pass Depth Naming & Base**:
   - Whether to name the property `passDepth` (unambiguous 1-based index: 1 for swap pass, 2 for cascade 1, etc.) or `cascadeDepth` (which historically differed between `MatchCreated` and `CascadeCreated` per `SIGNALR_PROTOCOL.md` §3.2.7 item 2).
4. **Board Serialization Format**:
   - Reusing existing `BoardPayload` (`CellPayload[64]`) vs defining a compact 64-character string representation.

---

## 4. Proposed Protocol Schema Specification

### 4.1 Schema Definition: `CascadeStepResolved` Wire DTO
A new event discriminator is added to `SIGNALR_PROTOCOL.md` §3.2:

```json
{
  "type": "CascadeStepResolved",
  "passDepth": 1,
  "actor": "player",
  "clearedCells": [18, 19, 20],
  "dropMoves": [
    { "fromCell": 2, "toCell": 18 },
    { "fromCell": 10, "toCell": 19 },
    { "fromCell": 11, "toCell": 20 }
  ],
  "spawnedGems": [
    { "cellIndex": 2, "gemType": "fire", "specialGem": null },
    { "cellIndex": 10, "gemType": "water", "specialGem": null },
    { "cellIndex": 11, "gemType": "wood", "specialGem": null }
  ],
  "board": {
    "cells": [
      { "gemType": "fire", "specialGem": null },
      { "gemType": "water", "specialGem": null }
    ]
  }
}
```

### 4.2 Member Specification Table

| Member | Type | Presence | Description |
| :--- | :--- | :--- | :--- |
| `type` | string | Always | Fixed string `"CascadeStepResolved"`. Discriminator member. |
| `passDepth` | integer | Always | 1-based detection pass index: `1` for the initial Swap match pass; $\ge 2$ for subsequent Cascade passes. |
| `actor` | string | Always | Acting entity: `"player"` or `"boss"`. Correlates with the enclosing `SwapStarted.actor`. |
| `clearedCells` | int[] | Always | Ascending cell indices ($0 \dots 63$) emptied during this pass by match resolution and special gem activations. |
| `dropMoves` | object[] | Always | Array of `DropMoveDto`. Empty if no surviving gems moved. Ordered column $0 \dots 7$, bottom-to-top. |
| `dropMoves[].fromCell`| integer | Always | Origin cell index ($0 \dots 63$) before gravity. |
| `dropMoves[].toCell` | integer | Always | Destination cell index ($0 \dots 63$) after gravity. `fromCell != toCell`. |
| `spawnedGems` | object[] | Always | Array of `SpawnedGemDto`. Empty if no gems spawned. Ordered column $0 \dots 7$, top-to-bottom. |
| `spawnedGems[].cellIndex`| integer | Always | Destination cell index ($0 \dots 63$) where the spawned gem is placed. |
| `spawnedGems[].gemType` | string | Always | Contract gem type: `"atk"`, `"def"`, `"hp"`, or `"power"`. |
| `spawnedGems[].specialGem`| object? | Conditional | Special gem metadata if spawned (omitted / null for ordinary gems). |
| `board` | object | Always | Complete authoritative 64-cell board state at the conclusion of this pass (post-gravity, post-spawn). Reuses `BoardPayload` schema. |

### 4.3 Sequence Semantics & Ordering in `ReceiveEvents.events[]`
`CascadeStepResolved` is emitted **at the conclusion of each detection pass** (after that pass's match destruction and special gem activations, but before the next pass's `CascadeCreated`).

Example event batch order for a 2-pass player resolution:
```text
1.  SwapStarted         (actor: "player", fromCell: 18, toCell: 19)
2.  SwapResolved        (outcome: "committed")
3.  MatchCreated        (shape: "Straight", cells: [18, 19, 20], gemType: "atk", cascadeDepth: 1)
4.  GemMatched          (cellIndex: 18, gemType: "atk")
5.  GemMatched          (cellIndex: 19, gemType: "atk")
6.  GemMatched          (cellIndex: 20, gemType: "atk")
7.  ComboChanged        (combo: 1)
8.  CascadeStepResolved (passDepth: 1, actor: "player", dropMoves: [...], spawnedGems: [...], board: {...})
9.  CascadeCreated      (cascadeDepth: 1)
10. MatchCreated        (shape: "Straight", cells: [34, 35, 36], gemType: "def", cascadeDepth: 2)
11. GemMatched          (cellIndex: 34, gemType: "def")
12. GemMatched          (cellIndex: 35, gemType: "def")
13. GemMatched          (cellIndex: 36, gemType: "def")
14. ComboChanged        (combo: 2)
15. CascadeStepResolved (passDepth: 2, actor: "player", dropMoves: [...], spawnedGems: [...], board: {...})
16. DamageCalculated    (...)
17. DamageDealt         (...)
...
```

---

## 5. Backend Capture & Emission Architecture

### 5.1 Domain Layer (`GameServer.Domain.Match3`)
1. **Extend `GravityResult`** (`GravityAndSpawn.cs`):
   ```csharp
   public readonly record struct DropMove(int FromCell, int ToCell);
   public readonly record struct GravityResult(
       BoardState Board,
       IReadOnlyList<int> VacatedCells,
       IReadOnlyList<DropMove> DropMoves);
   ```
   In `Gravity.Apply`: When `readIndex != writeIndex`, record `new DropMove(readIndex, writeIndex)`.
2. **Extend `PassResult`** (`BoardResolver.cs`):
   ```csharp
   public readonly record struct SpawnedGem(int CellIndex, GemType GemType, SpecialGem? SpecialGem);
   ```
   Add to `PassResult`:
   - `IReadOnlyList<DropMove> DropMoves`
   - `IReadOnlyList<SpawnedGem> SpawnedGems`
3. **Capture in `BoardResolver.ResolvePass`**:
   - `gravity = Gravity.Apply(...)` provides `gravity.DropMoves`.
   - `spawn = Spawn.Fill(...)` provides `spawn.CreatedCells`. Map each `(index, cell)` to `new SpawnedGem(index, cell.GemType, cell.SpecialGem)`.
   - `PassResult` stores `DropMoves`, `SpawnedGems`, and `Board` (`spawn.Board`).
4. **Event Generation in `BattleEventBuilder.cs`**:
   In `BattleEventBuilder.Build`:
   At the end of each pass loop (after emitting `MatchCreated`, `GemMatched`, `ComboChanged`), append:
   ```csharp
   events.Add(BattleEvent.ForCascadeStepResolved(
       passDepth: pass.CascadeDepth,
       actor: actor,
       clearedCells: pass.ClearedCellUnion,
       dropMoves: pass.DropMoves,
       spawnedGems: pass.SpawnedGems,
       board: pass.Board));
   ```
   Note: `BattleEventBuilder.Build` accepts an `actor` parameter (`DamageParty.Player` or `DamageParty.Boss`).

### 5.2 Application Layer (`GameServer.Application.Battle`)
In `BattleStateService.ResolveSwapAsync`:
- For Player board resolution: passes `DamageParty.Player` to `BattleEventBuilder.Build(result.Resolution, combo, DamageParty.Player)`.
- For Boss board resolution: passes `DamageParty.Boss` to `BattleEventBuilder.Build(bossResolution, bossResolution.TotalMatches, DamageParty.Boss)`.

### 5.3 API Layer (`GameServer.Api.Hubs`)
1. In `BattleEventWireProjection.cs`:
   - Add discriminator case `BattleEventType.CascadeStepResolved` in `Project()`.
   - Project domain `DropMove`, `SpawnedGem`, and `BoardState` onto `BattleEventWireDto`.
2. In `BattleEventWireDto.cs`:
   - Add wire properties with `[JsonPropertyName]` and `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`:
     - `PassDepth`
     - `Actor`
     - `ClearedCells`
     - `DropMoves`
     - `SpawnedGems`
     - `Board`
   - Add static factory method `BattleEventWireDto.CascadeStepResolved(...)`.

---

## 6. Frontend Consumption & Presentation Lifecycle

### 6.1 Phase Expansion in `BattleEventPresenter.ts`
1. Add new phase kinds to `PresentationPhaseKind`:
   - `'gravity-drop'`: Surviving gems animating downward.
   - `'refill-spawn'`: New gems entering from top of columns.
2. Update `adaptPresentedEvent`:
   - For `event.type === 'CascadeStepResolved'`:
     Map to two sequential phase descriptors or a single compound `'cascade-step'` phase.
     *Recommendation*: Use two distinct presentation phases:
     1. Phase `gravity-drop`: hold time = `DROP_TWEEN_MS` (~150–200 ms).
     2. Phase `refill-spawn`: hold time = `REFILL_TWEEN_MS` (~150–200 ms).

### 6.2 Timeline Construction in `BattleScene.ts` (`buildPresentationPhases`)
When iterating through `ReceiveEvents.events[]`:
1. `MatchCreated` / `GemMatched`: Builds `'match'` or `'cascade'` phase (Highlights + gem destruction).
2. `CascadeStepResolved`:
   - Closes open match phase.
   - Pushes Phase `'gravity-drop'` carrying `event.dropMoves`.
   - Pushes Phase `'refill-spawn'` carrying `event.spawnedGems` and `event.board`.
3. If next pass arrives (`CascadeCreated` + matches):
   - Because Phase `'refill-spawn'` synchronized `gemViews` to `event.board`, the board rendered on screen **now exactly matches the server's pre-match board for Pass 2**!
   - `clearConfirmedCell` succeeds for Pass 2, Pass 3, etc., with 100% authoritative confirmation!
4. Final `'settle'` phase:
   - Placed at the end of all board cycles as before.
   - Because all per-pass drops and refills have already played, `'settle'` acts purely as a final reconciliation check against `currentBattleState.board`.

### 6.3 Animation Implementation in `BattleScene.ts`
1. **`playGravityDrop(phase)`**:
   - For each move in `phase.dropMoves`:
     - Looks up `gemViews[fromCell]`.
     - Tweens sprite position `(x, y)` from `fromCell` grid coordinate to `toCell` grid coordinate using `tweens.add`.
     - Re-indexes `gemViews[toCell] = gemViews[fromCell]`.
     - Registers tween in `timelineEffects`.
2. **`playRefillSpawn(phase)`**:
   - For each spawned gem in `phase.spawnedGems`:
     - Instantiates/activates gem view above column (e.g. `row = -1`).
     - Tweens sprite downward to `cellIndex` grid coordinate.
     - Sets `gemViews[cellIndex].content = gemContentKeyFor(...)`.
     - Registers tween in `timelineEffects`.
   - Upon tween completion: asserts and synchronizes with `phase.board`.

---

## 7. Compatibility, Recovery & Safety Analysis

### 7.1 Payload Size & Network Performance
- Overhead per pass:
  - `dropMoves`: ~5–15 items $\approx$ 150–350 bytes.
  - `spawnedGems`: ~3–10 items $\approx$ 100–250 bytes.
  - `board` (64 cells using `BoardPayload`): $\approx$ 2.2 KB.
  - Total added JSON per pass: $\approx$ 2.5–2.8 KB.
- Total added JSON for typical 2-cascade round: $\approx$ 5 KB.
- Over SignalR WebSocket:
  - 5 KB fits in 3–4 TCP packets or a single unfragmented WebSocket frame.
  - WebSocket compression (`permessage-deflate`) compresses repetitive cell keys by >75%, resulting in $< 1.2$ KB wire delta per cascade pass.
  - Network latency impact is negligible ($< 1$ ms on standard broadband).

### 7.2 Backward Compatibility
- **Server with Old Client**:
  - An old client ignores unknown event types in `adaptPresentedEvent` or skips them.
  - The old client continues executing the single-settle transition at the end of the round.
  - No crash, no desync.
- **New Client with Old Server**:
  - If `CascadeStepResolved` is absent from the batch, the new client generates no `'gravity-drop'` or `'refill-spawn'` phases.
  - The client gracefully falls back to the existing TASK-275 behavior (single settle phase at the end).
  - No assertion failure, no desync.

### 7.3 Recovery Behavior & Desync Invariant Protection
- **TASK-273 Batch Admission Invariant**:
  - `GameRuntime.ts` strictly gates admission on `envelope.serverSequence` vs `held.sequence`.
  - Because `CascadeStepResolved` travels *inside* `ReceiveEvents.events[]`, the batch sequence number is unchanged.
  - Gaps in `serverSequence` continue to trigger snapshot recovery via `GetBattleState`.
- **TASK-274 Recovery Boundary Invariant**:
  - Reconnect snapshot recovery (`GetBattleState`) delivers only the final committed `BattleState`.
  - A snapshot supersedes all in-flight and withheld batches.
  - When recovery occurs, `BattleScene.abortPresentation()` aborts active timelines, cancels tweens, and calls `settleHeldPresentation()`.
  - The scene converges instantly to the authoritative snapshot board without attempting to replay missed cascade passes (`ADR-008`).
- **Duplicate Batch Protection**:
  - `acceptedResolutionSequence >= envelope.serverSequence` drops duplicate batches before timeline creation.
  - Duplicate drops or spawns are impossible.

---

## 8. Failure Convergence & Degraded-Mode Semantics

| Anomaly / Edge Case | System Behavior | Convergence Path |
| :--- | :--- | :--- |
| **Malformed `dropMoves` (index $<0$ or $>63$)** | `BattleScene` catches bounds error; logs diagnostic; skips drop tween for that element. | Timeline continues; phase completes; board synchronizes to authoritative `board` at step end. |
| **Missing `CascadeStepResolved` event** | Client builds timeline without per-pass drop/spawn phases. | Falls back to single `'settle'` phase at end of board cycles; converges to `currentBattleState.board`. |
| **Network disconnect mid-cascade** | Socket drops; `GameRuntime` detects disconnect; cancels active timeline via `abortPresentation()`. | On reconnect, fetches `GetBattleState` snapshot; renders settled board; releases input lock. |
| **Sequence desync (`serverSequence > held.sequence`)** | `GameRuntime` withholds batch whole; does not emit to listeners. | Requests `GetBattleState` snapshot once; snapshot supersedes withheld batch; renders final state. |
| **Scene destroyed during drop tween** | `BattleScene.shutdown()` invokes `tweens.killAll()` and clears layers. | Zero orphan tweens, zero memory leaks, subscriptions cleanly released. |

---

## 9. Implementation Work Breakdown Structure (WBS)

```text
TASK-276 Implementation Subtasks:
├── Subtask 1: ADR & Contract Documentation
│   ├── Author ADR-027 (Authoritative Cascade Snapshot Protocol)
│   ├── Update docs/02-technical/SIGNALR_PROTOCOL.md (§3.2.2, §3.2.28)
│   ├── Update docs/02-technical/GAME_EVENTS.md (§1.1, §2)
│   └── Update docs/02-technical/GAME_STATE.md (§3, §5.1)
│
├── Subtask 2: Backend Domain & Application Pipeline
│   ├── Extend GravityResult with DropMoves in GravityAndSpawn.cs
│   ├── Extend PassResult with DropMoves and SpawnedGems in BoardResolver.cs
│   ├── Update BattleEventBuilder.cs to emit CascadeStepResolved
│   └── Update BattleStateService.cs to pass actor identity to builder
│
├── Subtask 3: Backend API & Wire Projection
│   ├── Add CascadeStepResolved DTO & projection in BattleEventWireProjection.cs
│   └── Add unit tests in GameServer.Api.Tests
│
├── Subtask 4: Frontend Presenter & Scene Pipeline
│   ├── Extend PresentationPhaseKind in BattleEventPresenter.ts
│   ├── Map CascadeStepResolved in adaptPresentedEvent
│   ├── Implement playGravityDrop and playRefillSpawn in BattleScene.ts
│   └── Update clearConfirmedCell to operate across multi-pass cascades
│
└── Subtask 5: Verification & Regression Testing
    ├── Backend unit & integration test suites
    ├── Frontend unit test suites (SceneLifecycle, BattleEventPresentation)
    └── Standalone web smoke test verification
```

---

## 10. Declared File Set for Implementation

When implementation is authorized, the declared files will be:

### Backend:
```text
src/backend/GameServer.Domain/Match3/GravityAndSpawn.cs
src/backend/GameServer.Domain/Match3/BoardResolver.cs
src/backend/GameServer.Domain/Match3/BattleEvent.cs
src/backend/GameServer.Domain/Match3/BattleEventBuilder.cs
src/backend/GameServer.Application/Battle/BattleStateService.cs
src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs
tests/backend/GameServer.Domain.Tests/Match3/CascadeResolverTests.cs
tests/backend/GameServer.Domain.Tests/Match3/BattleEventBuilderTests.cs
tests/backend/GameServer.Api.Tests/Hubs/BattleEventWireProjectionTests.cs
```

### Frontend:
```text
src/frontend/client/src/game/scenes/BattleEventPresenter.ts
src/frontend/client/src/game/scenes/BattleScene.ts
src/frontend/client/tests/BattleEventPresentation.test.ts
src/frontend/client/tests/SceneLifecycle.test.ts
```

### Documentation & ADRs:
```text
docs/02-technical/SIGNALR_PROTOCOL.md
docs/02-technical/GAME_EVENTS.md
docs/02-technical/GAME_STATE.md
docs/03-decisions/ADR/ADR-027-authoritative-cascade-snapshot-protocol.md
```

---

## 11. Test Matrix & Acceptance Criteria

### 11.1 Backend Unit & Integration Tests
- [ ] `Gravity_ComputesExactDropMoves_WhenGemsFall`: Asserts that `GravityResult.DropMoves` correctly pairs `(fromCell, toCell)` for each falling gem and excludes non-moving survivors.
- [ ] `Spawn_RecordsCreatedGems_WithTypesAndIndices`: Asserts that `SpawnResult` records all spawned gems in column order top-to-bottom.
- [ ] `BattleEventBuilder_EmitsCascadeStepResolved_PerPass`: Asserts that `CascadeStepResolved` is emitted at the end of Pass 1 and each subsequent Cascade pass.
- [ ] `BattleEventWireProjection_SerializesCascadeStepResolved_ExactContract`: Asserts that wire DTO contains camelCase properties, omits nulls, and projects `BoardPayload` accurately.
- [ ] `BossResolution_EmitsCascadeStepResolved_WithBossActor`: Asserts that Boss cascade steps carry `actor: "boss"`.

### 11.2 Frontend Unit & Integration Tests
- [ ] `Presenter_AdaptsCascadeStepResolved_ToDropAndRefillPhases`: Asserts that `adaptPresentedEvent` maps `CascadeStepResolved` to `'gravity-drop'` and `'refill-spawn'` phases.
- [ ] `Scene_AnimatesGravityDrop_FromAuthoritativeMoves`: Asserts that gem views tween from `fromCell` to `toCell` and re-index correctly.
- [ ] `Scene_AnimatesRefillSpawn_FromDeliveredGems`: Asserts that new gem views appear above columns and tween down into destination cells.
- [ ] `Scene_MultiPassDestruction_ConfirmsGemsAcrossAllPasses`: Asserts that `clearConfirmedCell` successfully clears gems at depth 2 and depth 3 because the board was updated by Pass 1's `CascadeStepResolved`.
- [ ] `Scene_RecoverySnapshot_InterruptsCascadeAndConverges`: Asserts that a recovery snapshot arriving mid-gravity aborts tweens, clears timeline, and converges to snapshot board immediately.
- [ ] `Scene_MissingCascadeStepResolved_FallsBackToSettlePhase`: Asserts graceful degradation when `CascadeStepResolved` is absent.

### 11.3 Acceptance Criteria
1. **AC-1 (Zero Client Simulation)**: Gem movements, spawns, and pass boards are 100% server-delivered. Zero client-side gravity or refill calculations.
2. **AC-2 (Per-Pass Visual Fidelity)**: Every cascade pass visually animates match highlights, confirmed destruction, gravity drop, and gem refill.
3. **AC-3 (Multi-Pass Confirmation)**: Match destruction gates (`clearConfirmedCell`) successfully confirm and destroy matched gems for passes at depth $\ge 2$.
4. **AC-4 (Wire Contract Integrity)**: The 3-member `ReceiveEvents` envelope is preserved; `BattleStateUpdated` push is preserved; single Redis CAS persistence is preserved.
5. **AC-5 (Recovery & Desync Invariant)**: Reconnect recovery snapshot behavior is unaltered; no cascade replay occurs during recovery; sequence gaps trigger clean snapshot convergence.
6. **AC-6 (Multi-Actor Support)**: Both Player and Boss cascades animate through the same pipeline, correctly distinguished by `actor`.

---

## 12. Rollout & Execution Plan

1. **Phase 1: Decision & Documentation Approval**:
   - File ADR-027 and update `SIGNALR_PROTOCOL.md`, `GAME_EVENTS.md`, `GAME_STATE.md`.
2. **Phase 2: Backend Implementation**:
   - Update `GravityAndSpawn.cs`, `BoardResolver.cs`, `BattleEventBuilder.cs`, `BattleStateService.cs`, and `BattleEventWireProjection.cs`.
   - Verify all backend unit and integration tests pass (`dotnet test src/backend/GameServer.sln`).
3. **Phase 3: Frontend Implementation**:
   - Update `BattleEventPresenter.ts` and `BattleScene.ts`.
   - Verify frontend unit and lifecycle suites pass (`npx vitest run`).
4. **Phase 4: Full E2E & Smoke Verification**:
   - Verify standalone web smoke test and manual browser inspection.
