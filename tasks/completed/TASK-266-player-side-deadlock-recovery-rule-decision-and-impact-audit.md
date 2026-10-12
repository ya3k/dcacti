# TASK-266 — Player-Side Deadlock Recovery: Rule Decision and Impact Audit

<!--
  GEN-TASK EXECUTION MANIFEST / AUDIT & DECISION PREPARATION
  Principle: TASK = EXECUTION MANIFEST & DECISION PREPARATION.
  This task is audit and decision preparation only. No production or gameplay code changes.
-->

---

## Metadata

```text
Task ID:           TASK-266
Type:              DOCUMENTATION
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     orchestrator
Supporting Agents: gameplay, backend, review
Workflow:          documentation/documentation-change.md
Skills:            quality/documentation-consistency,
                   gameplay/authority-determinism-audit,
                   testing/test-scenario-generation
Dependencies:      TASK-258 (DONE), TASK-259 (DONE), TASK-260 (DONE), TASK-261 (DONE),
                   TASK-262 (DONE), TASK-263 (DONE), TASK-264 (DONE), TASK-265 (DONE)
Declared Files:    none
```

---

## Objective

Investigate the player-side deadlock risk reported in TASK-265 and establish the smallest correct gameplay decision before any implementation.

Reported findings to verify:
1. Deadlock recovery currently occurs during the Boss phase.
2. If the Boss-HP-zero terminal path short-circuits the round before Boss AI executes, that recovery path is skipped.
3. A committed board may therefore have no legal Player move.
4. The reshuffle mechanic lacks a dedicated authoritative game-design or technical owner outside `ADR-026`.

**Scope constraint:** This task is audit and decision preparation only. Do not change gameplay code or implement deadlock recovery.

---

## Authoritative References

- `AGENTS.md` §2 (hierarchy of sources of truth), §7 (game rule protection), §10 (server authority), §11 (determinism & RNG), §20 (stop conditions)
- `docs/00-overview/MVP_SCOPE.md` §1 (scope boundary)
- `tasks/TASK_LIFECYCLE.md` (two-stage commit protocol, task states)
- Completed task records `TASK-258` through `TASK-265`
- `docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md` §2, §7, §8, Amendment
- `docs/03-decisions/ADR/ADR-008-battle-recovery.md` (snapshot-based battle recovery)
- `docs/01-game-design/GAME_RULES.md` §1.4, §16, §17 (steps 18a, 19a), §18, §21
- `docs/01-game-design/MATCH3_RULES.md` §1.3, §1.4, §1.4.1, §1.4.2, §1.5, §2, §2.1.2, §2.1.4, §4, §8.1–§8.3
- `docs/01-game-design/COMBAT_RULES.md` §3, §5.1–§5.3
- `docs/01-game-design/BOSS_RULES.md` §5 item 4, §6, §7
- `docs/02-technical/GAME_STATE.md` §2.0.3, §2.1, §2.6, §3, §5.1
- `docs/02-technical/GAME_EVENTS.md` §1, §2, §3 item 8
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3, §3.1, §3.2.2, §3.2.19, §4, §7
- Implementation in `src/backend`:
  - `GameServer.Application.Battle.BattleStateService`
  - `GameServer.Application.Battle.BattleResultService`
  - `GameServer.Domain.Bosses.BossAiEngine`
  - `GameServer.Domain.Match3.BoardGenerator`
  - `GameServer.Domain.Match3.BoardGenerationValidator`
  - `GameServer.Domain.Match3.SwapValidator`
  - `GameServer.Domain.Match3.SwapExecution`
  - `GameServer.Domain.Match3.CascadeResolver`
  - `GameServer.Domain.Match3.Pcg32`
  - `GameServer.Api.Hubs.BattleHub`

---

## Investigation Findings & Evidence

### Q1 — Is the Reported Deadlock Reachable?

#### 1. Definition of a Legal Player Move under Authoritative Rules
Under `docs/01-game-design/MATCH3_RULES.md` §2 and §2.1.2, a Player Swap Action is validated by four sequential checks:
1. **Index Range & Distinctness (§2.1.2 items 1–2):** `from` and `to` must each be in `0..63` and distinct (`INVALID_CELL_INDEX`).
2. **Adjacency (§2.1.2 item 3, §2 item 1):** `|row(from) - row(to)| + |col(from) - col(to)| == 1` (`INVALID_SWAP`).
3. **State Precondition / Staleness (§2.1.2 item 4, ADR-025):** `expectedSequence == state.Sequence` or unrepeated action (`STALE_ACTION`).
4. **Match-Producing (§2.1.2 item 5, §2 item 3):** The swap is simulated on an in-memory copy of the board and must produce **at least one** §3 Match (a straight run of 3+ identical gems or L/T shape). A swap producing 0 matches is **rejected** (`SwapRejectionReason.NoMatchFromSwap`), and the board reverts with zero state mutation.

**Distinction of Board States:**
- **Any adjacent swap:** If it does not create a 3+ match, it is rejected (`NO_MATCH_FROM_SWAP`).
- **Any swap producing a valid match:** This is the *only* legal move accepted by `SwapValidator.Validate` (`SwapValidator.cs:159-165`).
- **A board accepted by initial generation:** Under `MATCH3_RULES.md` §1.3, §1.4 and `BoardGenerator.cs:136`, `BoardGenerationValidator.IsValidInitialBoard` guarantees `!HasMatch(board) && HasValidSwap(board)`. It is guaranteed to have $\ge 1$ legal match-producing swap.
- **A board produced by cascades:** `CascadeResolver.Resolve(...)` (`CascadeResolver.cs:83-300`) loops passes of gravity and unconstrained PRNG gem spawns until no matches exist. It performs **zero validation** of whether the resulting stable board contains any legal swap. It can and does produce boards with 0 legal swaps.
- **A board committed after Boss AI recovery:** In `BossAiEngine.SelectMove(...)` (`BossAiEngine.cs:199-217`), deadlock reshuffle occurs *before* the Boss selects its move. Once selected, the Boss's move and cascades resolve via `CascadeResolver.Resolve(...)` (`BattleStateService.cs:1632-1635`). The board committed to Redis (`resolved.BoardState = bossResolution.Board` at `:1680`) is the post-Boss-cascade board, which is **not** checked for legal swaps!
- **A board committed after a terminal short-circuit:** When player damage reduces Boss HP to 0 (`bossState.HP == 0` at `BattleStateService.cs:1559`), Step 8 short-circuits. Step 9 (`BossAiEngine.SelectMove`) is skipped entirely. The board committed to Redis is `resolved.BoardState` (produced by Player's cascades), unverified for legal swaps.

#### 2. Exact Control Flow When Boss HP Reaches Zero
Trace in `BattleStateService.cs`:
1. Player submits `Swap(from, to, expectedSequence, actionId)`.
2. `ExecuteSwapAsync` reads `BattleState` from Redis (`:844`), validates idempotency and sequence (`:870-888`).
3. `ResolveSwapAsync` executes `SwapExecutor.Execute` (`:1252`), resolving player swap and player cascades.
4. Player attack calculates damage and updates `bossState.HP` (`:1505-1515`).
5. `bossState.HP == 0` triggers Step 8 (`:1559`):
   - Appends `BattleEvent.ForBattleWon(0, petHp)` to `events` (`:1576`).
   - Ticks status effects at Step 19a (`:1590`).
   - Returns `SwapResolution` (`:1587`), **skipping Steps 9–12** (`BossAiEngine.SelectMove`, Boss cascades, Boss skill, Boss attack, Player terminal check).
6. In `ExecuteSwapAsync`, `_repository.TryUpdateAsync(committed.State, expectedSequence)` commits the state to Redis (`:921-923`). The committed state has `Turn = state.Turn + 1`, `Sequence = state.Sequence + 1`, and `BoardState = resolved.BoardState` (unverified for valid moves).
7. Line 943 calls `await PersistTerminalResultAsync(committed.State, committed.Events, cancellationToken)`:
   - `TerminalOutcome(events)` returns `BattleOutcome.Victory` (`:2955`).
   - In `BattleResultService.cs:560-663`, PostgreSQL writes the `BattleResult` row, Player XP, Pet XP in a single transaction.
   - At line 703:
     ```csharp
     await _battles.DeleteAsync(battleId, cancellationToken);
     ```
     The active battle state in Redis is **deleted**.
8. `ExecuteSwapAsync` returns `committed` (`:946`).
9. `BattleHub.Swap` broadcasts `BattleStateUpdated` and `ReceiveEvents` (with `BattleWon`) to the group (`BattleHub.cs:1120-1121`).

#### 3. Is the Player Allowed to Act Afterward?
- **Authoritative Rules:** Under `GAME_RULES.md` §1.4 ("a battle ends when either the Boss or Player reaches 0 HP"), `BOSS_RULES.md` §7 ("the terminal check then ends the battle with no Boss Response"), and `GAME_EVENTS.md` §2 (`BattleWon` terminates the battle). The battle has **ended**. No further player action is permitted or defined. There is no "Player's next turn".
- **Realized System Behavior:** In normal operation, Redis state is deleted (`BattleResultService.cs:703`). Any subsequent call to `BattleHub.Swap` finds `GetAsync == null` (`BattleStateService.cs:844`), returning `SwapResponse(Accepted: false, Reason: "BATTLE_NOT_FOUND")` (`BattleHub.cs:1105`). The player cannot act.
- **Persistence Failure Edge Case:** If PostgreSQL persistence fails or is unconfigured (`_battleResults == null`), `PersistTerminalResultAsync` catches/ignores the failure (`BattleStateService.cs:2858-2892`), and the state remains in Redis with `BossState.HP == 0`. `ExecuteSwapAsync` currently lacks a guard checking `BossState.HP == 0` at entry. If the board has no valid swaps, `SwapValidator.Validate` rejects with `NO_MATCH_FROM_SWAP`. If it has a valid swap, it executes and emits `BattleWon` again. However, this is an infrastructure resilience anomaly, not a gameplay deadlock.

#### 4. The Real, Reachable Gameplay Deadlock (Non-Terminal Execution Path)
The critical discovery of this audit is that **the real, reachable deadlock occurs on the NON-TERMINAL path**:
1. Round $N$ begins. Player makes a swap. Cascades resolve. Boss HP is $> 0$.
2. Step 9: Boss AI executes `BossAiEngine.SelectMove(resolved.BoardState, rng)`.
   - If the pre-move board is deadlocked, it reshuffles.
   - Boss selects a move.
3. Boss's move and cascades resolve: `bossResolution = CascadeResolver.Resolve(...)` (`BattleStateService.cs:1632-1635`).
4. Step 9 assigns `resolved.BoardState = bossResolution.Board` (`:1680`).
5. Step 12: `PetState.HP > 0` (non-terminal). Step 13 ticks DoTs (`BossState.HP > 0`).
6. Round $N$ commits to Redis with `resolved.BoardState`.
7. **No code verifies `bossResolution.Board` for valid swaps before committing!**
8. Round $N+1$ begins. It is the **Player's turn to act** on an active, non-terminal battle.
9. If `bossResolution.Board` settled with 0 legal swaps (verified reproducible with alternating striped patterns, e.g. `BossAiEngineTests.cs:190-202`):
   - The Player attempts any swap.
   - `SwapValidator.Validate` tests all adjacent pairs and rejects all 112 with `SwapRejectionReason.NoMatchFromSwap`.
   - `ExecuteSwapAsync` returns `IsRejected = true` (`:914-917`), writing nothing to Redis.
   - `ResolveSwapAsync` steps 3–14 (including `BossAiEngine.SelectMove`) **never run**.
   - The battle is **permanently, irrecoverably deadlocked**.

---

### Q2 — Gameplay Invariant and Scope

#### 1. Proposed Invariant
> **"Every non-terminal committed board on which the Player may act has at least one legal match-producing swap."**

#### 2. Interaction Analysis
- **Terminal Wins & Losses:**
  Terminal boards (`BattleWon`, `BattleLost`) are **explicitly exempt**. When a battle ends, the player never acts again. Enforcing reshuffles on terminal boards would waste server entropy and risk aborting a valid victory or defeat if the 64-attempt retry bound were exhausted during battle termination.
- **Boss-HP-Zero Short-Circuit:**
  Because Boss-HP-zero is terminal (`BattleWon`), it falls under the terminal exemption. The short-circuit at Step 8 is gameplay-sound and does not require recovery.
- **Round Atomicity (`ADR-026` §2):**
  Deadlock recovery on the non-terminal committed board must occur *inside* the atomic round transaction, before `TryUpdateAsync` commits to Redis. The reshuffled board and advanced `RngState` are committed in the same single write-back.
- **Cascade Resolution:**
  `CascadeResolver` remains pure, deterministic, and actor-agnostic. It stops when the board is stable. Post-cascade recovery is a lifecycle concern evaluated at the round commit boundary, not inside `CascadeResolver`.
- **Reshuffle Generation Failure (Bounded Retry):**
  `BoardGenerator.Generate` enforces `MaxAttempts = 64`. If 64 attempts fail, it throws `BoardGenerationFailedException`. The round fails closed: nothing is written to Redis, leaving stored state and RNG uncorrupted.
- **Existing Match-3 Rules and Input Validation:**
  `SwapValidator.Validate` continues to reject non-match-producing swaps (`NO_MATCH_FROM_SWAP`). With the invariant enforced, a valid swap is guaranteed to exist for the player.

---

### Q3 — Comparison of Recovery Boundaries (Option A, B, C)

| Option | Location | Control Flow & Impact | Verdict |
|---|---|---|---|
| **Option A** | Validate & recover before every non-terminal board commit | At the conclusion of `ResolveSwapAsync` (after Step 19a, before returning `SwapResolution`), check if the board has legal moves (`BossAiEngine.EnumerateLegalMoves(board).Count > 0` or `BoardGenerationValidator.HasValidSwap(board)`). If 0 legal moves AND non-terminal (`PetState.HP > 0 && BossState.HP > 0`), reshuffle via `BoardGenerator.Generate(rng)`. Board and updated `RngState` are committed atomically via Redis CAS. | **RECOMMENDED.** Enforces the invariant at the authoritative commit boundary. Client always receives a playable board via `BattleStateUpdated` and `GetBattleState`. Preserves round atomicity and offline determinism. |
| **Option B** | Recover only at Player-action entry | Check board in `ExecuteSwapAsync` when a player sends a swap. If deadlocked, reshuffle and push updated state. | **REJECTED.** Client visually renders an unplayable board and only discovers deadlock upon action attempt. Requires out-of-band state mutation and extra CAS commit outside round resolution. Violates clean snapshot recovery (`ADR-008`). |
| **Option C** | Keep existing Boss-phase-only recovery | Only recover inside `BossAiEngine.SelectMove`. | **REJECTED.** Demonstrably violates the invariant. Does not check post-Boss cascades (`bossResolution.Board`), allowing deadlocked boards to be committed to Redis for the Player with zero recovery path. |

**Structural Refinement to Option A:**
`BossAiEngine.SelectMove` must **retain** its existing pre-move reshuffle check. Why? Because if the Player's move leaves a deadlocked board, the Boss needs a valid swap to make its own move during Step 9.
Option A adds the **commit-boundary recovery** after all round cascades, guaranteeing the invariant for the Player's subsequent turn.

---

### Q4 — Deterministic State, RNG, CAS, and Snapshots

1. **RNG Stream Ownership:**
   Deadlock recovery strictly consumes the active battle's server-seeded `Pcg32` stream (`BattleState.RngState`). No unseeded or system randomness may be used (`TDD.md` §6, `ADR-009`, `ADR-026` §8).
2. **Inclusion in Committed State:**
   `BoardGenerator.Generate(Pcg32 rng)` draws 64 cell selections per attempt. The resulting `rng.CurrentState` is assigned to `committed.State.RngState`. Both `BoardState` and `RngState` are written back together in the single Redis CAS commit (`TryUpdateAsync`).
3. **Retry Exhaustion:**
   If `BoardGenerator.Generate` exhausts 64 attempts, it throws `BoardGenerationFailedException` before `TryUpdateAsync`. Redis CAS is never called; stored state, sequence, and RNG are preserved with zero corruption.
4. **CAS Conflicts:**
   In `ExecuteSwapAsync`, if `TryUpdateAsync` fails due to a sequence mismatch, the loop re-reads fresh state from Redis and re-computes the entire deterministic resolution against the fresh state, including any reshuffle.
5. **Idempotency & Snapshots:**
   - Idempotent retries (`ActionId` match) return `SwapExecutionResult.IdempotentReplay` without calling `ResolveSwapAsync`, consuming zero RNG (`BattleStateService.cs:870-877`).
   - Snapshot recovery (`GetBattleState`) reads the committed state from Redis (`ADR-008`). Because the committed state already includes any reshuffle, reconnecting clients receive the stable, valid snapshot with zero secondary reshuffle.

---

### Q5 — Authoritative Rule Ownership & Documentation Locations

Per the source-of-truth hierarchy (`AGENTS.md` §2):
```text
MATCH3_RULES.md > GAME_RULES.md > Technical Docs (GAME_STATE.md) > ADRs (ADR-026)
```

1. **Primary Domain Rule Owner: `docs/01-game-design/MATCH3_RULES.md`**
   - **Recommended location:** New §1.6 "Runtime Board Deadlock Recovery" (immediately following §1.5 "Retry on Invalid Candidate").
   - **Exact specifications to author in future task:**
     - *Deadlock Definition:* A stable board containing zero legal match-producing adjacent swaps under §2.1.2.
     - *Invariant:* Every non-terminal committed board on which the Player may act has at least one legal match-producing swap.
     - *Trigger & Boundary:* Evaluated at the round commit boundary after all cascade resolutions and status effect ticks.
     - *Recovery Algorithm:* Deterministic reshuffle via `BoardGenerator.Generate(rng)` consuming the active `Pcg32` stream, bounded by `MaxAttempts = 64`.
     - *Exemption:* Terminal boards (`BattleWon`, `BattleLost`) are exempt.
     - *Visibility:* Server-internal; emits no Battle Events; committed board travels in the round's single state push (`ADR-026` / `TASK-265` Option A).
2. **Game Rules Document: `docs/01-game-design/GAME_RULES.md`**
   - **Recommended location:** §17 (Event Resolution Rules), Step 19 / Round Commit subsection.
   - Reference `MATCH3_RULES.md` §1.6 for deadlock recovery before atomic round write-back.
3. **Technical Specification: `docs/02-technical/GAME_STATE.md`**
   - **Recommended location:** §5.1 (Atomic Round Persistence).
   - Clarify that `BattleState.BoardState` and `RngState` reflect any commit-boundary deadlock recovery performed prior to the Redis CAS write-back.
4. **Architectural Decision Record: `docs/03-decisions/ADR/ADR-026-...md`**
   - **Recommended location:** Decision 8 (Deadlock Reshuffling).
   - Update Decision 8 to cite `MATCH3_RULES.md` §1.6 as the authoritative domain owner and explicitly note the commit-boundary invariant for the Player.

---

## Required Test Coverage for Future Implementation Task

1. **Domain Unit Tests (`GameServer.Domain.Tests`):**
   - `Match3/BoardDeadlockDetectorTests.cs`: Verify detection of deadlocked boards (e.g. 8x8 striped pattern from `BossAiEngineTests.cs:190`) vs. boards with $\ge 1$ legal swap.
   - `Match3/BoardGeneratorTests.cs`: Verify deterministic reshuffle from deadlocked boards produces boards with `HasValidSwap == true` and advances `Pcg32` deterministically.
2. **Application Round Orchestration Tests (`GameServer.Application.Tests`):**
   - `BattleStateServiceDeadlockRecoveryTests.cs`:
     - Test A: Non-terminal round where Boss cascades settle into a deadlocked board $\rightarrow$ round commits a reshuffled board with $\ge 1$ legal swap and advanced `RngState`.
     - Test B: Non-terminal round where Player cascades settle into a deadlocked board $\rightarrow$ Boss AI reshuffles, Boss moves, and final board is valid.
     - Test C: Terminal win round (`BossState.HP == 0`) where Player cascades settle into a deadlocked board $\rightarrow$ round commits without reshuffling (exempt), `BattleWon` emitted.
     - Test D: Terminal loss round (`PetState.HP <= 0`) $\rightarrow$ round commits without reshuffling (exempt), `BattleLost` emitted.
     - Test E: Idempotent replay of an action that triggered reshuffle $\rightarrow$ returns committed state without re-drawing RNG.
     - Test F: CAS concurrency conflict $\rightarrow$ retried resolution produces identical reshuffle deterministically against fresh state.
3. **Entry Guard Test (`BattleStateServiceTests.cs`):**
   - Test that submitting a swap action to an already-terminal battle (`BossState.HP == 0` or `PetState.HP <= 0`) is rejected at entry, preventing zombie execution.

---

## Explicit Product Owner Decisions & Recommendations

1. **Decision 1: Board Validity Invariant Scope**
   - *Recommendation:* Approve the proposed invariant: "Every non-terminal committed board on which the Player may act has at least one legal match-producing swap." Terminal boards are exempt.
2. **Decision 2: Authoritative Rule Home**
   - *Recommendation:* Place the formal domain rule in `MATCH3_RULES.md` §1.6, with cross-references in `GAME_RULES.md` §17 and `ADR-026` §8.
3. **Decision 3: Recovery Architecture (Option A)**
   - *Recommendation:* Implement commit-boundary recovery in `BattleStateService` before Redis CAS, while retaining pre-move recovery in `BossAiEngine.SelectMove`.
4. **Decision 4: Terminal Action Guard**
   - *Recommendation:* Add an entry guard in `ExecuteSwapAsync` rejecting swap actions on terminal battles (`BossState.HP <= 0 || PetState.HP <= 0`), hardening against zombie state persistence.

---

## Scope Compliance & Verification

- [x] No production code was modified (`src/` is untouched).
- [x] No gameplay rules were modified (`docs/` is untouched).
- [x] No new events, wire discriminators, or DTOs were introduced.
- [x] No RNG algorithms or retry bounds were modified.
- [x] No G-2 (`Combo`/`MatchCount`) work was performed.
- [x] All unowned and pre-existing files are preserved unstaged.
- [x] No follow-up task was automatically created.

---

## Completion Evidence

### Tests Executed
- `dotnet test tests/backend/GameServer.Domain.Tests/GameServer.Domain.Tests.csproj --filter "FullyQualifiedName~BossAiEngineTests"`: 6 passed, 0 failed.
- `dotnet test tests/backend/GameServer.Application.Tests/GameServer.Application.Tests.csproj --filter "FullyQualifiedName~BattleStateServiceTests"`: 66 passed, 0 failed.

### Checks Not Run
- Full backend suite, client unit suite (`npm run test:run`), frontend build (`npm run build`), and browser E2E test suites were deliberately not run because this task modifies no production or test code.

### Commit
- Implementation slice (Phase A): none (design review and contract audit artifact; declares no implementation files outside its own record)
- Completion record filing (Phase B): this completion record alone (`tasks/completed/TASK-266-player-side-deadlock-recovery-rule-decision-and-impact-audit.md`)
- Owns: `TASK-266`
- Files: `tasks/completed/TASK-266-player-side-deadlock-recovery-rule-decision-and-impact-audit.md`
- Shared with: `none`
- Unowned / pre-existing: `none`

### Final Status

**`DONE — AUDIT COMPLETE & DECISION PREPARATION RECORDED`**

