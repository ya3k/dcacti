# TASK-279 — Investigation & Fix: Multi-Match GemMatched Event Duplication in BattleEventBuilder

---

## Metadata

```text
Task ID:           TASK-279
Type:              BUG / INVESTIGATION & FIX
Status:            DONE
Risk:              LOW
Priority:          MEDIUM
Mode:              Defect Isolation, Minimal Fix & Verification
Classification:    VERIFIED DEFECT & MINIMAL FIX — Runtime reproduction confirmed; minimal domain fix and regression tests verified
Production code:   src/backend/GameServer.Domain/Match3/BattleEventBuilder.cs
Test code:         tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs
Contract Refs:     docs/02-technical/GAME_EVENTS.md §1.1, §1.3, §2; docs/01-game-design/GAME_RULES.md §16
Dependencies:      None (Independent of protocol authorization gate TASK-278)
Declared Files:    src/backend/GameServer.Domain/Match3/BattleEventBuilder.cs,
                   tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs
```

---

## 1. Problem Statement & Confirmed Runtime Reproduction

### The Defect
In `src/backend/GameServer.Domain/Match3/BattleEventBuilder.cs` (lines 114–136 prior to fix), the loop emitting `GemMatched` events for matched cells was lexically nested **inside** the loop iterating over `pass.Matches`:

```csharp
foreach (var match in pass.Matches)
{
    events.Add(BattleEvent.ForMatch(match));

    // §2 GemMatched / §1.3: the matched sub-step's consumed Gems, once
    // per cell, ascending §1.0 index — the enumeration the resolver
    // already applied...
    foreach (var gem in pass.MatchedCellGemMatched)
    {
        events.Add(BattleEvent.ForGem(gem));
    }

    comboSoFar++;
    events.Add(BattleEvent.ForCombo(comboSoFar));
}
```

Because `pass.MatchedCellGemMatched` is a pass-wide union containing all matched cells across the detection pass, whenever a pass resolved with $M \ge 2$ simultaneous matches (e.g. two disjoint 3-gem matches), the entire collection of matched cells was emitted $M$ times:
- For Match 1: `MatchCreated` was emitted, followed by all cells in `pass.MatchedCellGemMatched`.
- For Match 2: `MatchCreated` was emitted, followed by all cells in `pass.MatchedCellGemMatched` **again**.
- As a result, each matched cell was duplicated $M$ times in the emitted battle event stream.

### Confirmed Runtime Reproduction
The defect was verified through a failing reproduction test using a deterministic two-match fixture (`TwoDisjointMatchesBoard()`):
- An 8x8 board was configured such that an adjacent swap of `(3,1) <-> (4,1)` completed two disjoint horizontal Match 3s simultaneously in Pass 1:
  - Row 3, Cols 0–2: ATK (3 cells)
  - Row 4, Cols 0–2: HP (3 cells)
  - Total unique matched cells: 6 cells.
- **Unfixed Behavior:**
  - `pass.Matches.Count` was 2 ($M = 2$).
  - `pass.MatchedCellGemMatched.Count` was 6.
  - `BattleEventBuilder.Build()` emitted **12** `GemMatched` events for Pass 1 instead of 6 (6 cells emitted under Match 1, then the same 6 cells emitted under Match 2).
  - This confirmed runtime reproduction of the duplication defect and proved the lexical nesting finding.

---

## 2. Contract Violations & Ordering Specification Gap

1. **`GAME_EVENTS.md` §1.3:**
   - Item 1: *"the pass's matched cells first"* — the matched sub-step is one union per pass, ascending §1.0 index.
   - Item 2: *"no two cells share an index and a cell named by several sources is reported once."*
   - Item 7: A `GemMatched` sequence that repeats a cell is explicitly defined as an ordering defect.
2. **`GAME_EVENTS.md` §2 (`GemMatched` payload):**
   - Each consumed gem cell must be reported exactly once. Emitting duplicate cell events bloats SignalR payloads and sends redundant destruction/clearing commands to client presentation layers.
3. **Coupling to Protocol Decision R-5 (TASK-277 §4.2):**
   - Recommendation R-5 (omitting a redundant `clearedCells` array from `CascadeStepResolved`) requires the `GemMatched` stream to accurately represent the unique set of cleared cells per pass. The duplication bug prevented `GemMatched` from serving as an authoritative per-pass clear statement.

### Ordering Convention & Documentation Specification Gap
In `GAME_EVENTS.md` §1.1 and §1.3:
- §1.1 specifies the pass cycle as `[CascadeCreated] -> (per match: MatchCreated, GemMatched..., ComboChanged...) -> GemMatched (activation)`.
- §1.3 establishes that matched cells form **one union for the pass**, not a per-match list.
- **Specification Gap:** The documentation specifies the sequence when a pass contains a single match ($M = 1$), but does **not** explicitly specify the event placement order when multiple matches occur simultaneously in one pass ($M \ge 2$). Specifically, it does not state whether the single pass-wide `GemMatched` run should appear after the first `MatchCreated`, after the last `MatchCreated`, or be partitioned per-match.
- **Chosen Ordering Convention:** The minimal fix places the pass's single unified `MatchedCellGemMatched` sequence immediately following the **first** `MatchCreated` event in the pass:
  ```text
  Pass (M >= 2):
    [CascadeCreated] (if depth >= 2)
    MatchCreated (Match 1)
      GemMatched (all pass matched cells, ascending index, exactly once)
      ComboChanged (Combo for Match 1)
    MatchCreated (Match 2)
      ComboChanged (Combo for Match 2)
    ...
    GemMatched (activation sub-step, if any Special Gems triggered)
  ```
- This ordering ensures each match gets its corresponding `MatchCreated` and `ComboChanged` in §3.2 match-set order, while the single pass-wide cell clear union is delivered with the initial match event that opens the pass's resolution. While structurally consistent, formalizing this multi-Match placement in `GAME_EVENTS.md` remains a documentation follow-up item.

---

## 3. Minimal Production Fix

### Implementation in `BattleEventBuilder.cs`
File: `src/backend/GameServer.Domain/Match3/BattleEventBuilder.cs`

A minimal boolean flag `matchedSubStepReported` was introduced to guard the `pass.MatchedCellGemMatched` loop, ensuring it executes only once per pass:

```csharp
// §1.3 items 1–2: the matched sub-step is ONE union for the pass — the
// pass's matched cells, ascending §1.0 index, each cell once — and it is
// not a per-Match list. It is therefore reported once per pass: with the
// MatchCreated that opens the pass's match sequence, which is the position
// §1.1's cycle fixes for it. Emitting it once per Match would report every
// matched cell of a pass with M ≥ 2 Matches M times, which §1.3 item 2's
// "reported once" and item 7's "repeats a cell" forbid.
var matchedSubStepReported = false;

foreach (var match in pass.Matches)
{
    // §1.1 items 2 and 5: MatchCreated precedes this Match's
    // ComboChanged, and both sit inside the pass's match-set order.
    events.Add(BattleEvent.ForMatch(match));

    // §2 GemMatched / §1.3: the matched sub-step's consumed Gems, once
    // per cell, ascending §1.0 index — the enumeration the resolver
    // already applied. Reported with the first MatchCreated of the pass
    // it belongs to, so the GemMatched run reads as the pass's own
    // matched sub-step and then, for this pass, the activation
    // sub-step's.
    if (!matchedSubStepReported)
    {
        foreach (var gem in pass.MatchedCellGemMatched)
        {
            events.Add(BattleEvent.ForGem(gem));
        }

        matchedSubStepReported = true;
    }

    comboSoFar++;
    events.Add(BattleEvent.ForCombo(comboSoFar));
}
```

### Architectural Properties Preserved
- **Pure Function:** `BattleEventBuilder.Build()` remains a pure static mapping with zero state mutations, side effects, or allocations beyond event construction.
- **No Protocol or Schema Changes:** No changes were made to DTOs, wire contracts, SignalR protocol envelopes, or database schemas.
- **No Game Rule Changes:** Gameplay mechanics, cascade depth resolution, combo counting, and damage pipelines remain completely unaltered.

---

## 4. Regression & Multi-Match Tests Added

File: `tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs`

### 1. Deterministic Multi-Match Fixtures
- `TwoDisjointMatchesBoard()`: Generates an 8x8 match-free board where swapping `(3,1) <-> (4,1)` forms two disjoint 3-gem matches (row 3 ATK, row 4 HP) in Pass 1, cascading into subsequent single-match passes.
- `TwoDisjointMatchesWithALineClearGemBoard()`: Configures the same two-match board with a horizontal Line Clear Gem at `(3,0)` consumed during the swap's first match, exercising both the matched sub-step and activation sub-step simultaneously in a multi-match pass.

### 2. Unit & Regression Tests
- **`SeveralMatchesInOnePass_ShouldEmitEachMatchedCellExactlyOnce`:**
  Asserts that across a two-match pass, all 6 matched cells are emitted exactly once in ascending index order immediately after the first `MatchCreated`. Asserts that the second `MatchCreated` is followed by `ComboChanged` rather than duplicate `GemMatched` events.
- **`SeveralMatchesInOnePass_ShouldFollowTheDocumentedCycleOrder`:**
  Validates the full structural sequence of events across both initial multi-match and subsequent cascade passes, confirming `CascadeCreated`, `MatchCreated`, deduplicated `GemMatched`, and per-match `ComboChanged` ordering.
- **`SeveralMatchesInOnePass_ShouldEmitTheActivationSubStepOnceAfterTheMatchedSubStep`:**
  Verifies that when a Special Gem is consumed in a multi-match pass, the matched sub-step and activation sub-step each emit their respective cells once without overlap or repetition.
- **Updated `Events_ShouldFollowTheDocumentedCycleOrder`:**
  Refactored existing test fixture verification to align with the pass-level single emission pattern for `MatchedCellGemMatched`.

---

## 5. Verification Evidence & Test Execution

### Verification Summary
- `git diff --check`: Clean (no whitespace defects or merge marker conflicts).
- Domain test suite:
  ```bash
  dotnet test tests/backend/GameServer.Domain.Tests/
  ```
  **Result:** 1,626 passed, 0 failed, 0 skipped (Duration: ~1s). All new and existing event emission tests passed.
- API test suite:
  ```bash
  dotnet test tests/backend/GameServer.Api.Tests/
  ```
  **Result:** 374 passed, 0 failed, 0 skipped (Duration: ~47s).
- Infrastructure test suite:
  ```bash
  dotnet test tests/backend/GameServer.Infrastructure.Tests/
  ```
  **Result:** 472 passed, 4 failed (4 pre-existing failures in boss definition / migration JSON tests, completely unrelated to domain event building).

### Independently Reproduced Application Failures
Running the full backend test suite (`dotnet test src/backend/GameServer.sln`) reproduced exactly **10** pre-existing test failures in `GameServer.Application.Tests`:
1. `BalanceEvidenceMeasurementCorrectionTests.TASK195_PowerSpentTotal_RecordsDrainPowerWithoutPlayerCardCasts` (seed: 20261005 — expected 2 boss skill casts, actual 3)
2. `BalanceEvidenceMeasurementCorrectionTests.TASK195_PowerSpentTotal_RecordsDrainPowerWithoutPlayerCardCasts` (seed: 20261003 — expected 2 boss skill casts, actual 3)
3. `BalanceEvidenceMeasurementCorrectionTests.TASK195_PowerSpentTotal_RecordsDrainPowerWithoutPlayerCardCasts` (seed: 20261001 — expected 2 boss skill casts, actual 1)
4. `BalanceEvidenceMeasurementCorrectionTests.TASK195_PowerSpentTotal_RecordsDrainPowerWithoutPlayerCardCasts` (seed: 20261002 — expected 2 boss skill casts, actual 1)
5. `BalanceEvidenceMeasurementCorrectionTests.TASK195_PowerSpentTotal_RecordsDrainPowerWithoutPlayerCardCasts` (seed: 20261004 — expected 3 boss skill casts, actual 4)
6. `BalanceEvidenceMeasurementCorrectionTests.TASK195_PerTurnSeries_SatisfiesInvariantOnCastTerminatedRuns` (seed: 20261002 — string start mismatch: committed Swap vs Card cast)
7. `BalanceEvidenceMeasurementCorrectionTests.TASK195_PerTurnSeries_SatisfiesInvariantOnCastTerminatedRuns` (seed: 20261005 — string start mismatch: committed Swap vs Card cast)
8. `BalanceEvidenceMeasurementCorrectionTests.TASK195_PerTurnSeries_SatisfiesInvariantOnCastTerminatedRuns` (seed: 20261003 — string start mismatch: committed Swap vs Card cast)
9. `BalanceSimulatorTests.H06_EveryMetric_IsPopulatedPerItsDefinition` (expected 0, actual 5630)
10. `BalanceBaselineMatrixTests.TASK194_AllSeventyFiveRuns_CompleteWithCompleteMetrics` (expected 0, actual 64)

These 10 failures stem from earlier balance simulation tuning (TASK-194 / TASK-195) and have zero dependency on or interaction with `BattleEventBuilder.cs`.

---

## Completion Evidence

### Historical Implementation Commit
- **Commit:** `e33254e8057159f50012746ebf17a7d01acd6d13`
- **Subject:** `feat: implement GameRuntime and battle protocol recovery architecture`
- **Lifecycle Ratification:** Historical composite commit exception under `tasks/TASK_LIFECYCLE.md` §6.4. Git history preserved without rewriting.
- **Implemented Files:**
  - `src/backend/GameServer.Domain/Match3/BattleEventBuilder.cs`
  - `tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs`
- **Validation Evidence:**
  - `dotnet test tests/backend/GameServer.Domain.Tests` — PASS (1629 passed, 0 failed)
  - Regression tests: `SeveralMatchesInOnePass_ShouldEmitEachMatchedCellExactlyOnce`, `SeveralMatchesInOnePass_ShouldFollowTheDocumentedCycleOrder`, `SeveralMatchesInOnePass_ShouldEmitTheActivationSubStepOnceAfterTheMatchedSubStep`.
- **Phase B Filing:**
  - Filed to `tasks/completed/TASK-279-investigate-multi-match-gemmatched-duplication.md` under Operator Authorization (Backlog Reconciliation Execution).
  - Status finalized as `DONE`.

---

## 6. Scope, Remaining Limitations & Controlled Workflow Compliance

### Remaining Limitations & Open Items
1. **Frontend / Browser Verification:**
   No frontend test suite (`npm run test:run`) or browser Playwright E2E suite was executed in this domain verification pass. Frontend presentation handling of multi-match events relies on existing client event handlers.
2. **Dedicated Multi-Match Boss Fixture:**
   The multi-match verification tests exercise player swap resolutions (`SwapExecutor`). While boss moves reuse the shared match-3 resolution engine, there is currently no dedicated multi-match boss turn test fixture in the suite.
3. **Documentation Alignment:**
   `GAME_EVENTS.md` §1.1 should be updated in a follow-up documentation task to explicitly specify the event placement convention for multi-match passes ($M \ge 2$).

### Controlled Workflow Compliance
- **Scope Restriction:** Strictly documentation-only for this reconciliation task. No production code, tests, ADRs, or technical documents were modified.
- **Worktree Integrity:** All pre-existing modified files (`BattleEventBuilder.cs`, `BattleEventEmissionTests.cs`) and untracked backlog records were preserved without reset or stash.
- **Commit/Push Prohibited:** In accordance with the controlled repository workflow, no git commit or push was performed.
