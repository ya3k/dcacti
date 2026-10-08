# TASK-216 — Pet XP Persistence & Atomicity Audit Report

**Date:** 2026-10-05  
**Auditor:** Gemini 3.8 — High  
**Status:** Complete  
**Scope:** Pet XP persistence and atomicity path from battle completion to client-visible state (Pet Level MVP-IN; Pet Tier/Star progression and passive mechanics excluded).

---

## 1. Current Flow

The complete flow from battle resolution to client-visible Pet state was traced through Domain, Application, Infrastructure, and Api layers:

```text
Battle Resolution (BattleStateService.cs)
 ├── Terminal Action executed (ExecuteSwapAsync / ExecuteCardCastAsync / ExecutePetSkillCastAsync)
 ├── Domain Combat Resolution emits terminal event (BattleWon / BattleLost per GAME_EVENTS.md §2)
 ├── Active Battle State CAS write to Redis (TryUpdateAsync against Redis key `battle:{battleId}:state`)
 └── CAS success triggers terminal persistence hook:
      ↓
 Application Orchestration
 ├── BattleStateService.PersistTerminalResultAsync invokes IBattleResultPersistence.PersistTerminalResultAsync
 ├── ScopedBattleResultPersistence creates IServiceScope, resolving scoped BattleResultService
 ├── BattleResultService.PersistTerminalResultAsync executes:
 │    ├── 1. Pre-validation: asserts BattleId, PlayerId, PetInstanceId, BossIdentity are present.
 │    ├── 2. Boss lookup: IBossDefinitionLookup.FindBossDefinitionIdByIdentityAsync queries PostgreSQL `BossDefinition`.
 │    │       └── If unresolvable: fails closed (returns false, Redis key retained per DATABASE.md §1).
 │    ├── 3. Idempotency probe: IBattleResultRepository.GetByIdAsync(battleId) checks PostgreSQL `BattleResults`.
 │    │       └── If already durable: returns true immediately without re-awarding or rewriting.
 │    ├── 4. Entity reads:
 │    │       ├── IPlayerRepository.GetByIdAsync(playerId)
 │    │       └── IPetRepository.GetByIdAsync(petInstanceId)
 │    ├── 5. XP & Level calculation:
 │    │       ├── Player XP: Player.BattleWonXpReward (100) or Player.BattleLostXpReward (0)
 │    │       ├── Pet XP: Pet.BattleWonXpReward (100) or Pet.BattleLostXpReward (0)
 │    │       ├── Post-grant values projected: petXpAfterGrant = Math.Min(pet.XP + petXpGained, Pet.MaxXp)
 │    │       └── Pet Level: Pet.LevelForXp(petXpAfterGrant) = min(floor(XP/100) + 1, 50)
 │    ├── 6. Durable result construction: builds BattleResult with 8-member RewardSummary JSON.
 │    ├── 7. BattleResult write:
 │    │       └── IBattleResultRepository.AddAsync(result)
 │    │            └── BattleResultRepository calls `await _dbContext.SaveChangesAsync()` (Commit #1)
 │    ├── 8. Progression updates (gated on firstDurableWrite):
 │    │       ├── player.GrantBattleXp(playerXpGained)
 │    │       ├── IPlayerRepository.SaveProgressionAsync(player)
 │    │       │    └── PlayerRepository calls `await _dbContext.SaveChangesAsync()` (Commit #2)
 │    │       ├── pet.GrantBattleXp(petXpGained)
 │    │       └── IPetRepository.SaveProgressionAsync(pet)
 │    │            └── PetRepository calls `await _dbContext.SaveChangesAsync()` (Commit #3)
 │    └── 9. Active state cleanup:
 │            └── IBattleStateRepository.DeleteAsync(battleId) deletes Redis key `battle:{battleId}:state`.
 │                 (Exceptions caught and ignored; Redis TTL remains cleanup path).
 ↓
 Client-Visible Read Paths
 ├── Collection Read: GET /api/pets (CollectionController.cs)
 │    ├── IPetRepository.ListByPlayerIdAsync queries PostgreSQL `Pet` table where PlayerId == caller.
 │    └── Projects PetResponse with stored `level = pet.Level` (XP is deliberately omitted per API_CONTRACTS.md §5.1).
 └── Battle Result Read: GET /api/battle/{battleId}/result (BattleController.cs)
      ├── IBattleResultRepository.GetOwnedResultAsync queries PostgreSQL `BattleResults`.
      └── Projects BattleResultResponse with `rewards` containing `petXpGained`, `newPetXp`, `petLeveledUp`, `newPetLevel`.
```

---

## 2. Authoritative XP Source

| Dimension | Finding | Citations & Code Locations |
| :--- | :--- | :--- |
| **Where calculated** | Evaluated server-side in `BattleResultService` mapping over domain reward constants; mutated in `Pet.GrantBattleXp`. | `GameServer.Domain.Pets.Pet` lines 113, 124, 340–353; `BattleResultService.cs` lines 377–385, 413–424. |
| **Granting event/action** | Terminal battle events `BattleWon` or `BattleLost` produced by the authoritative Combat/Swap resolution pipeline when Boss HP reaches 0 or Player HP reaches 0. | `GAME_EVENTS.md` §2; `COMBAT_RULES.md` §7.2, §7.4; `PET_RULES.md` §5.3; `BattleStateService.cs` lines 883, 1002, 1083. |
| **Reward amounts** | `Victory` $\rightarrow$ `+100` Pet XP (`Pet.BattleWonXpReward`).<br>`Defeat` $\rightarrow$ `+0` Pet XP (`Pet.BattleLostXpReward`). | `PET_RULES.md` §5.3 items 1–2; `Pet.cs` lines 113, 124. |
| **Server authority** | 100% server-authoritative. The endpoint and hub accept no reward, outcome, or XP values from the client. | `GAME_RULES.md` §18; `ADR-001`; `AGENTS.md` §10. |
| **Client influence** | **None.** The client can only submit action requests (`Swap`, `CardCast`, `PetSkillCast`). | `BattleHub.cs` lines 1070, 1120, 1210; `API_CONTRACTS.md` §3. |

---

## 3. Ownership Correctness

| Verification Point | Result | Evidence |
| :--- | :---: | :--- |
| **Active combat Pet targeting** | **VERIFIED** | `state.PetState.PetId` is established at battle creation from the validated owned Pet (`POST /api/battle/start`), carried on `BattleState` through Redis serialization, and extracted directly via `var petInstanceId = state.PetState.PetId.Value;` in `BattleResultService.cs:257`. |
| **Owned Pet instance vs. PetDefinition** | **VERIFIED** | Mutations target `GameServer.Domain.Pets.Pet` (table `"Pet"`, PK `"PetInstanceId"`). `PetDefinition` represents static species content and possesses no `XP` or `Level` columns (`DATABASE.md` §1). `PetRepository.SaveProgressionAsync` queries `_dbContext.Pets` by `PetInstanceId`. |
| **Cross-account / cross-instance isolation** | **VERIFIED** | `PetInstanceId` is a globally unique primary key. Inactive owned Pets receive `+0` Pet XP (`PET_RULES.md` §5.3 item 3) and are never queried or mutated. Other players' Pets are completely untouched. |
| **Test verification** | **VERIFIED** | Explicitly covered in `PetXpBattleRewardTests.cs`: `BattleWon_ShouldLeaveEveryInactiveOwnedPetUntouched`, `BattleWon_ShouldCreditThePetTheBattleStateNames_NotAnotherOwnedPet`, and `PetGrant_ShouldTargetTheRowTheStateResolved_EvenWhenItIsNotTheOldest`. |

---

## 4. Level Calculation

| Progression Rule | Authoritative Contract | Domain Implementation | Status |
| :--- | :--- | :--- | :---: |
| **XP $\rightarrow$ Level formula** | $\text{Pet.Level} = \min(\lfloor \text{Pet.XP} / 100 \rfloor + 1, 50)$ (`PET_RULES.md` §5.4) | `Pet.LevelForXp(int xp)` in `Pet.cs:289-301`: `Math.Min((xp / XpPerLevelCurveConstant) + 1, MaxLevel)` | **PASS** |
| **Worked boundaries** | XP $0 \rightarrow$ Level 1<br>XP $100 \rightarrow$ Level 2<br>XP $4900 \rightarrow$ Level 50 | Verified identically in domain logic and unit tests (`PetXpProgressionTests.cs`). | **PASS** |
| **Hard cap** | Hard maximum at 4900 XP (`PET_RULES.md` §5.5). No overflow, no hidden XP, no post-cap accumulation. | Enforced in `Pet.GrantBattleXp`: `XP = Math.Min(XP + xpGained, MaxXp)` (`MaxXp = 4900`). | **PASS** |
| **Post-cap reward** | Grants crossing 4900 clamp to 4900; grants at 4900 store nothing. | Verified in `Pet.cs:348` and `PetXpBattleRewardTests.cs:320-333`. | **PASS** |
| **Tier / Star independence** | Tier and Star are independent axes; MVP progression involves Level only (`PET_RULES.md` §5.7 item 4; `ADR-012` item 5). | `Tier` and `Star` are immutable `{ get; init; }` properties on `Pet.cs` and are never read or altered during battle reward processing. | **PASS** |
| **Track independence** | Pet XP and Player XP are strictly separate pools (`PET_RULES.md` §5.1 item 5; `ADR-016` item 12). | Neither reads or modifies the other; Player XP is uncapped while Pet XP has a 4900 hard cap. | **PASS** |

---

## 5. Persistence Path

1. **Physical Storage:**
   - PostgreSQL table `"Pet"` (mapped by `PetConfiguration.cs` in `GameDbContext`).
   - Columns: `"XP"` (integer, check constraint $0 \le \text{XP} \le 4900$), `"Level"` (integer, check constraint $1 \le \text{Level} \le 50$).
   - JSON summary: PostgreSQL table `"BattleResult"` column `"RewardSummary"` (`jsonb`), holding `petXpGained`, `newPetXp`, `petLeveledUp`, `newPetLevel`.
2. **Execution in PostgreSQL:**
   - `PetRepository.SaveProgressionAsync` queries `_dbContext.Pets.FirstOrDefaultAsync(instance => instance.PetInstanceId == pet.PetInstanceId)`.
   - Modifies `stored.XP = pet.XP` and `stored.Level = pet.Level`.
   - Invokes `await _dbContext.SaveChangesAsync(cancellationToken)`.
   - Generates an `UPDATE "Pet" SET "XP" = @p0, "Level" = @p1 WHERE "PetInstanceId" = @p2` statement against PostgreSQL.
3. **Round-Trip Consistency:**
   - Reloading via a fresh `GameDbContext` returns the updated `XP` and `Level` (`PetProgressionPersistenceTests.SaveProgression_ShouldPersistXpAndLevel_AcrossAReload`).

---

## 6. Actual Transaction Boundary

Inspection of the production code reveals the following database transaction boundary:

```csharp
// BattleResultService.cs:491
var firstDurableWrite = await _results
    .AddAsync(result, cancellationToken)
    .ConfigureAwait(false); // ---> BattleResultRepository calls _dbContext.SaveChangesAsync() [COMMIT #1]

if (firstDurableWrite)
{
    if (player is not null)
    {
        player.GrantBattleXp(playerXpGained);
        await _players.SaveProgressionAsync(player, cancellationToken)
            .ConfigureAwait(false); // ---> PlayerRepository calls _dbContext.SaveChangesAsync() [COMMIT #2]
    }

    if (pet is not null)
    {
        pet.GrantBattleXp(petXpGained);
        await _pets.SaveProgressionAsync(pet, cancellationToken)
            .ConfigureAwait(false); // ---> PetRepository calls _dbContext.SaveChangesAsync() [COMMIT #3]
    }
}
```

### Critical Finding: Absence of Database Transaction
- **No outer transaction exists.** Neither `ScopedBattleResultPersistence`, `BattleResultService`, nor any repository calls `_dbContext.Database.BeginTransactionAsync()` or enlists in a `TransactionScope`.
- **Three independent database commits occur.** In EF Core, each call to `SaveChangesAsync()` opens and commits its own independent PostgreSQL transaction if no ambient transaction exists.
- The persistence operations are executed sequentially as **three separate database transactions**:
  1. Commit #1: Inserts `BattleResult` row.
  2. Commit #2: Updates `Player` row (`XP`, `Level`).
  3. Commit #3: Updates `Pet` row (`XP`, `Level`).

---

## 7. Failure / Partial-Commit Analysis

Because the operations are split across three separate database transactions, the sequence is vulnerable to partial failure:

### Scenario 7.1: BattleResult succeeds, Pet progression fails (Critical Defect)
- **Failure Trigger:** After Commit #1 succeeds, Commit #3 fails due to:
  - Transient network interruption to PostgreSQL;
  - Database lock timeout or deadlock;
  - Server process crash (SIGKILL, container restart, OOM);
  - Cancellation token cancellation during or immediately before `_pets.SaveProgressionAsync`.
- **Database State in PostgreSQL:**
  - `BattleResult` row is **durably committed**.
  - `BattleResult.RewardSummary` records: `"petXpGained": 100`, `"newPetXp": 100`, `"newPetLevel": 2`, `"petLeveledUp": true`.
  - `Pet` row in table `"Pet"` **remains un-updated** (e.g. `XP = 0`, `Level = 1`).
- **Consequence on Retry/Recovery:**
  - `BattleStateService.PersistTerminalResultAsync` catches the failure and logs it in `_unpersistedResults`. The active state in Redis is NOT deleted.
  - When the client reconnects, retries, or recovery re-runs terminal persistence, `BattleResultService.PersistTerminalResultAsync` executes:
    ```csharp
    var alreadyDurable = await _results
        .GetByIdAsync(battleId, cancellationToken)
        .ConfigureAwait(false) is not null;

    if (alreadyDurable)
    {
        return true; // <--- Returns true immediately!
    }
    ```
  - Because `BattleResult` was committed in Commit #1, `alreadyDurable` is `true`.
  - The method **immediately returns `true`** without ever executing `_pets.SaveProgressionAsync`!
  - **Result: The Pet's XP is permanently lost.** Furthermore, `BattleResult.RewardSummary` reports that the Pet was awarded XP, while the actual `Pet` record in the database permanently reflects the pre-battle values.

### Scenario 7.2: BattleResult succeeds, Player progression succeeds, Pet progression fails
- `Player` receives +100 XP (Commit #2 committed).
- `Pet` receives +0 XP (Commit #3 failed).
- On retry, `alreadyDurable` returns `true`. The Player has progressed, but the Pet has permanently lost XP.

### Scenario 7.3: Ignored Return Value
- `PetRepository.SaveProgressionAsync` returns `bool` (`false` if the Pet row was missing from PostgreSQL).
- In `BattleResultService.cs:529`, the return value `await _pets.SaveProgressionAsync(...)` is discarded. A silent failure to persist the Pet row is not detected, Redis state is deleted, and the method reports success (`true`).

---

## 8. Retry / Idempotency Analysis

1. **Under Normal (All-Success) Conditions:**
   - The primary-key guard `BattleResultId = BattleId` effectively prevents duplicate XP grants.
   - On a retry, `alreadyDurable` checks `_results.GetByIdAsync(battleId)`. If present, it returns `true` without granting XP again.
   - Tested and proven in `PetXpBattleRewardTests.RepeatedTerminalPersistence_ShouldGrantPetXpOnlyOnce` and `ManyRepeatedTerminalPersistences_ShouldStillGrantPetXpOnce`.
2. **Under Partial-Failure Conditions:**
   - The idempotency check is structurally coupled **only** to the existence of `BattleResultId`.
   - It assumes that if `BattleResult` exists, `Pet` and `Player` progression must have been saved.
   - Because the writes are not atomic, this assumption is invalid upon partial failure. The idempotency guard turns into a false-positive gate that suppresses recovery of the unpersisted Pet progression.

---

## 9. Reload / Reconnect Verification

1. **Collection Reads (`GET /api/pets` and `GET /api/pets/{petId}`):**
   - Read directly from PostgreSQL `Pet` table via `IPetRepository.ListByPlayerIdAsync` / `GetByIdAsync`.
   - Correctly exposes `level = pet.Level`.
   - Conforms to `API_CONTRACTS.md` §5.1: `xp` is persisted but explicitly NOT exposed on `/api/pets`.
2. **Result Read (`GET /api/battle/{battleId}/result`):**
   - Reads directly from PostgreSQL `BattleResult` table via `IBattleResultRepository.GetOwnedResultAsync`.
   - Exposes `rewards.petXpGained`, `rewards.newPetXp`, `rewards.newPetLevel`, `rewards.petLeveledUp`.
3. **Session Reconnect / Fresh Session:**
   - On clean completion: fresh session sees updated Pet Level in `/api/pets` and updated XP/Level in `/api/battle/{battleId}/result`.
   - On partial failure: fresh session sees stale Pet Level in `/api/pets`, but updated XP/Level in `/api/battle/{battleId}/result`, creating an inconsistent user experience.

---

## 10. Tests Currently Covering the Behavior

| Test Suite | File | What It Exercises | Gaps |
| :--- | :--- | :--- | :--- |
| **Domain Tests** | `PetXpProgressionTests.cs` | XP $\rightarrow$ Level formula boundaries, 4900 hard cap, input validation. | None for domain logic. |
| **Infrastructure Tests** | `PetProgressionPersistenceTests.cs` | `PetRepository.SaveProgressionAsync` writes to PostgreSQL and reloads via DbContext. | Exercises `PetRepository` in isolation, not within battle result flow. |
| **Infrastructure Tests** | `BattleResultPersistenceTests.cs` & `BattleResultPostgresTests.cs` | Schema constraints, foreign keys, uniqueness of `BattleResultId`. | Tests `BattleResult` table in isolation without progression updates. |
| **Application Tests** | `PetXpBattleRewardTests.cs` | `BattleResultService` with in-memory test doubles (`InMemoryBattleResultRepository`, `InMemoryPetRepository`, `InMemoryPlayerRepository`). | **Major gap:** All repositories are in-memory mocks. No tests simulate `SaveProgressionAsync` failure; `InMemoryPetRepository` does not even support failure injection. |
| **Terminal Flow Tests** | `BattleResultTerminalFlowTests.cs` | End-to-end battle resolution pipeline emitting terminal events and invoking persistence. | Uses in-memory test doubles. Does not verify database transaction boundaries or rollback. |

---

## 11. Findings Classification

### A. PASS
1. **Authoritative Pet XP Source:** Server-authoritative; derived from `Pet.BattleWonXpReward` (100) and `Pet.BattleLostXpReward` (0); zero client influence.
2. **Targeting & Ownership:** Resolves correct combat Pet instance via `BattleState.PetState.PetId`; updates owned `Pet` row rather than `PetDefinition`; inactive pets receive +0.
3. **Progression Formula & Cap:** `Pet.Level = min(floor(XP/100)+1, 50)` correctly implemented; 4900 hard cap strictly enforced on write; Tier and Star progression are untouched.
4. **Track Independence:** Pet XP and Player XP are fully decoupled.
5. **Read Path & Wire Projection:** `GET /api/pets` and `GET /api/battle/{battleId}/result` conform strictly to `API_CONTRACTS.md`.

### B. Implementation Defect
1. **Missing Atomic Transaction Boundary:**
   `BattleResultService.PersistTerminalResultAsync` executes three separate calls to `_dbContext.SaveChangesAsync()` without wrapping them in an explicit database transaction (`BeginTransactionAsync`).
2. **Vulnerability to Permanent XP Loss on Partial Commit:**
   If `BattleResult` is persisted but `PetRepository.SaveProgressionAsync` fails, subsequent retries are blocked by `alreadyDurable`, permanently losing Pet XP and creating state desynchronization between `BattleResult.RewardSummary` and `Pet.XP`.
3. **Ignored Return Value on Progression Save:**
   `BattleResultService` ignores the boolean return value of `_pets.SaveProgressionAsync(pet)`.

### C. Test Gap
1. **No Failure Injection for Progression Persistence:**
   `InMemoryPetRepository` and `InMemoryPlayerRepository` lack failure-injection capabilities (unlike `InMemoryBattleResultRepository.WriteFails`).
2. **No Tests for Partial Failure / Atomicity Rollback:**
   No existing test simulates a failure during Pet progression save after BattleResult has been staged/written.
3. **No Integration Tests over Shared Transaction Scope:**
   No test verifies that a failure to persist Pet XP rolls back the `BattleResult` insertion in PostgreSQL.

### D. Documentation Drift
1. **`DATABASE.md` §1 & `BattleResultService.cs` XML Documentation:**
   The documentation states: *"The two documented progression writes, bound to the first durable write — the exactly-once point"* and assumes that sequential invocation within a single C# method guarantees atomicity. In reality, under EF Core without an explicit transaction, each repository call commits independently.

---

## 12. Recommendation

**Classification: MODIFY — specific implementation task required**

### Proposed Follow-up Task: `TASK-217 — Pet & Player Battle Reward Atomicity Fix`

1. **Transaction Boundary Implementation:**
   - In `ScopedBattleResultPersistence` (or via an execution strategy / Unit of Work pattern on `GameDbContext`), wrap the execution of `BattleResultService.PersistTerminalResultAsync` (or the database writes within it) in an explicit EF Core database transaction:
     ```csharp
     await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
     // Add BattleResult
     // Save Player Progression
     // Save Pet Progression
     await transaction.CommitAsync(cancellationToken);
     ```
   - Alternatively, stage entity changes across repositories within the shared scoped `GameDbContext` and execute a single consolidated `SaveChangesAsync` call.
2. **Defensive Idempotency / Retry Handling:**
   - If an explicit transaction is used, failure in Pet progression will roll back the `BattleResult` row as well. The battle active state in Redis remains intact and recoverable. A subsequent retry will then cleanly re-attempt the entire atomic block.
   - Handle the boolean return value of `SaveProgressionAsync` by throwing an exception if a participating entity row cannot be found, triggering transaction rollback.
3. **Harness & Test Gap Remediation:**
   - Add `SaveProgressionFails` property to `InMemoryPetRepository` and `InMemoryPlayerRepository`.
   - Add unit tests verifying that when `SaveProgressionAsync` fails, the operation raises an exception and does not corrupt state.
   - Add an integration test using `GameDbContext` asserting that an exception during Pet progression update rolls back the inserted `BattleResult` row.

---

## Verification Summary
- Read and audited all relevant documentation (`docs/01-game-design/PET_RULES.md`, `docs/02-technical/DATABASE.md`, `docs/02-technical/API_CONTRACTS.md`, `docs/03-decisions/ADR/ADR-016*`).
- Audited production code in Domain (`Pet.cs`), Application (`BattleResultService.cs`, `BattleStateService.cs`, `ScopedBattleResultPersistence.cs`), and Infrastructure (`BattleResultRepository.cs`, `PetRepository.cs`, `PlayerRepository.cs`, `GameDbContext.cs`).
- Ran targeted test suites (`dotnet test` on Domain, Application, and Infrastructure) confirming all 860 existing tests pass.
- Verified that no production or test code was modified in this audit task.
- Only the audit artifact `tasks/completed/TASK-216-pet-xp-persistence-audit.md` was created.
- No Git commit, stage, stash, reset, or clean operations were performed.
