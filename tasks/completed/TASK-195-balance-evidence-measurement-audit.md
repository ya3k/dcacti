# TASK-195 — Balance Evidence Measurement Audit & Correction

<!--
  GEN-TASK EXECUTION MANIFEST — TEST INFRASTRUCTURE / MEASUREMENT CORRECTION SPECIFICATION ONLY
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  References docs/ and src/ by path and section — it does NOT copy game
  rules, formulas, magnitudes, schemas, or API payload shapes.

  SCOPE OF THIS TASK: Audit and correct measurement and harness issues
  discovered during TASK-194, then rerun the affected baseline simulation
  evidence matrix. This is MEASUREMENT CORRECTION ONLY, not a balance task.
  Zero production code changes, zero gameplay rule changes, zero balance
  changes, zero database changes, zero frontend changes, zero ADRs.
  No balance proposals approved or modified; no balance verdicts
  (BALANCED / UNBALANCED / PASS / FAIL) introduced.

  STATUS: DONE — audited harness, implemented measurement corrections,
  added regression test suite, verified determinism, reran 75-run baseline
  matrix, updated TASK-194 JSON artifact and evidence report.
-->

---

## Metadata

```text
Task ID:           TASK-195
Type:              Test Infrastructure / Measurement Audit & Correction
Status:            DONE
Executed:          2026-10-05
Execution Result:  All 8 measurement audit items addressed; 13/13 dedicated TASK-195
                   regression tests passing; 75/75 baseline simulations rerun and
                   bit-identically deterministic; full solution 2903 tests passing.
Production Changes: NONE
Balance Changes:   NONE
Database Changes:  NONE
Frontend Changes:  NONE
Balance Verdicts:  NONE (zero BALANCED / UNBALANCED / PASS / FAIL produced)
Risk:              LOW (test-only harness and measurement corrections; zero
                   production, gameplay, or balance changes)
Priority:          HIGH (corrects empirical measurement fidelity of baseline
                   evidence for TASK-191 balance decision support)
Primary Agent:     testing
Supporting Agents: gameplay, review
Workflow:          documentation/documentation-change.md
Skills:            testing/test-scenario-generation,
                   discovery/impact-analysis,
                   quality/scope-validation
Dependencies:      TASK-191 (balance pass specification; owner of decisions Q-1..Q-12),
                   TASK-193 (DONE — deterministic balance simulation harness),
                   TASK-194 (DONE — baseline simulation evidence and findings D-194-1..D-194-8)
Model:             Gemini 3.8
Reasoning:         High
```

---

## 1. Objective

Define the specification to audit and correct **measurement and harness issues discovered during TASK-194**, then rerun the affected baseline evidence matrix to ensure empirical data fidelity for open TASK-191 decisions.

Explicit boundaries:

```text
Type: Test Infrastructure / Measurement Audit & Correction
Production Changes: NONE
Gameplay Rule Changes: NONE
Balance Changes: NONE
Database Changes: NONE
Frontend Changes: NONE
ADR Creation: NONE
Balance Verdicts: NONE
```

TASK-195 is strictly a **measurement correction and test harness maintenance task**, not a balance task. It ensures that the test-only simulation harness (`BalanceSimulator`, `MetricAccumulator`, `BalanceSimulationConfiguration`) accurately measures authoritative combat execution without data loss, truncation, or schema ambiguity.

Once the test-only harness corrections pass regression testing, TASK-195 requires rerunning the affected TASK-194 baseline simulations (the canonical 75-run matrix: 5 Bosses × 3 policies × 5 deterministic seeds) and updating the archived raw JSON artifact and completion report tables with the corrected measurements.

---

## 2. Scope & Non-Scope

### 2.1 In Scope (Allowed)

- **Test-only harness corrections:** Modifications restricted to `tests/backend/GameServer.Application.Tests/Balance/`:
  - `MetricAccumulator.cs`: Fix pending damage clearing across rejected Swap retries; reconcile per-Turn series flush boundaries.
  - `BalanceSimulator.cs`: Correct Turn accumulation lifecycle on Swap rejection and cast-terminal battle resolutions; wire `Mode` property.
  - `BalanceSimulationConfiguration.cs`: Expose `Mode` (`BalanceSimulationMode`) as an immutable configuration property with default `Baseline`.
  - `BalanceSimulationMetrics.cs`: Align XML documentation comments with canonical specification numbering M-01 through M-15.
  - `BalanceBaselineMatrix.cs`: Ensure serialized evidence reflects corrected metrics and configuration schema.
- **Focused regression tests:** Dedicated unit tests asserting resolution of each identified measurement defect in `tests/backend/GameServer.Application.Tests/Balance/Tests/`.
- **Rerun of affected simulations:** Deterministic execution of the TASK-194 baseline matrix (75 runs) using corrected measurements.
- **Evidence artifact & report updates:** Updating `tasks/artifacts/TASK-194-baseline-simulation-results.json` and amending affected descriptive statistics and evidence tables in `tasks/completed/TASK-194-balance-baseline-simulation-evidence.md`.

### 2.2 Non-Scope & Critical Constraints (Forbidden)

- **Production code changes: NONE.** No file under `src/` may be created, edited, or deleted.
- **Gameplay rule changes: NONE.** `GAME_RULES.md` §17/§18, `MATCH3_RULES.md`, `COMBAT_RULES.md`, `CARD_RULES.md`, and all other authoritative rules remain intact.
- **Balance changes: NONE.** No Card, Relic, Boss, or Pet stat is tuned, normalized, or altered.
- **Power Charge changes: NONE.** Card `card-power-charge` retains `PowerCost = 0` per `CARD_RULES.md` §2 item 3; proposal B-01 remains undecided.
- **Mộc Yêu regeneration changes: NONE.** Passive `boss-moc-yeu-regen` retains authored 5% MaxHP regen per 5 player matches per `BOSS_RULES.md` §6.2.3; proposal B-07 remains undecided.
- **Pet Passive implementation: NONE.** Unapplied Pet Passive effects (`BattleStateService` / finding D-2) must NOT be implemented in this task.
- **Database changes: NONE.** No EF Core migration, entity modification, or schema script.
- **Frontend changes: NONE.** No Phaser, React, or UI presentation code is touched.
- **ADR creation: NONE.** Test harness and measurement corrections do not alter repository architecture (`docs/03-decisions/README.md` §2).
- **No balance verdicts:** The harness and report must NOT emit `BALANCED`, `UNBALANCED`, `PASS`, or `FAIL`. Technical outcomes remain strictly `Victory`, `Defeat`, `Stalemate`, `InvalidSimulation`.
- **Q-1 remains QUALITATIVE:** No numeric fight-duration target exists in authoritative docs; none is invented. Duration remains an observational metric.

---

## 3. Source-of-Truth Hierarchy & References

Governing documents per `AGENTS.md` §2 precedence:

### 3.1 Game Design Rules
- `docs/00-overview/GDD.md` §11 (damage flow), §12 (boss difficulty from mechanics).
- `docs/00-overview/MVP_SCOPE.md` §1 (in-scope gameplay), §2 (explicit non-goals).
- `docs/01-game-design/GAME_RULES.md` §2 (Turn concept), §5 (Combo table), §12 (Power clamp 0–100), §17 (turn resolution order), §18 (server authority).
- `docs/01-game-design/MATCH3_RULES.md` §2.1.5 (rejected swap begins no Turn), §7 (deterministic PRNG), §8.1 (Turn & sequence lifecycle: one committed Swap begins exactly one Turn; rejected Swap, board generation, and Card cast begin none).
- `docs/01-game-design/COMBAT_RULES.md` §1 (pet stat defaults), §2 (gem resource rates), §3 (damage pipeline steps 1–6), §4 (shield pool), §5 (crit composition).
- `docs/01-game-design/ELEMENT_RULES.md` §2 (matchup factors), §3 (elementless attacks resolve neutral), §5 (where element applies: damage instances with attacking element from Pet, Boss, Skill, or Effect dealing damage).
- `docs/01-game-design/CARD_RULES.md` §2 (card inventory), §3 (cast lifecycle; item 5 Card cast does not begin or advance Turn; item 6 one-card-cast-per-turn limit per ADR-021).
- `docs/01-game-design/RELIC_RULES.md` §2.3 (unequipped baseline), §8.5 (relic inventory).
- `docs/01-game-design/BOSS_RULES.md` §4 (skill resolution), §5 (enrage trigger), §6.1–§6.4 (Boss 1–5 definitions, passives, skills).
- `docs/01-game-design/PASSIVE_RULES.md` §4, §7 (passive charge and trigger lifecycle).

### 3.2 Technical Design & Architecture
- `docs/02-technical/ARCHITECTURE.md` §1, §3 (Domain and Application boundaries; test harness placement).
- `docs/02-technical/GAME_STATE.md` §2.0.2 (Turn counter), §2.2.2 (CardCastsUsedThisTurn), §2.6 (RngState determinism).
- `docs/02-technical/GAME_EVENTS.md` §1 (resolution order), §2 (DamageDealt, DamageCalculated, PowerChanged, BattleWon, BattleLost).
- `docs/02-technical/DATABASE.md` §1 (DurationTurns is `BattleState.Turn` at terminal resolution, equal to committed Swaps).
- `docs/03-decisions/ADR/ADR-009-deterministic-prng.md` (PRNG determinism).
- `docs/03-decisions/ADR/ADR-021-one-card-cast-per-committed-turn.md` (one-card-cast-per-turn limit).

### 3.3 Task Records
- `tasks/backlog/TASK-191-balance-pass.md` (balance audit, open decision queue Q-1..Q-12).
- `tasks/completed/TASK-193-deterministic-balance-simulation-harness.md` (harness specification and acceptance criteria H-01..H-10).
- `tasks/completed/TASK-194-balance-baseline-simulation-evidence.md` (baseline simulation execution, evidence, and findings D-194-1..D-194-8 in §29).

---

## 4. Issues Addressed

TASK-195 audits and corrects the specific measurement, harness, and fidelity issues identified during TASK-194 execution:

### 4.1 Issue 1: M-03 Player Damage Under-Count on Rejected Swap (Finding D-194-3)
- **Defect:** In 19 of 75 baseline simulation runs (100% of the `skilled` policy runs that executed damage card casts), `M-03 PlayerDamageTotal` recorded less player damage than the Boss's authoritative HP loss (`Boss.MaxHP - BossHpMin`).
- **Root Cause:** In `BalanceSimulator.RunAsync`, a Turn begins with `accumulator.BeginTurn(turnNumber)`, clearing pending damage. When `_policy.ProposeCast` resolves successfully, `accumulator.RecordResolution(cast.Value.Events)` accumulates cast damage into `_pendingPlayerDamage`. If the subsequent `_policy.ProposeSwap` is rejected by the production validator, the loop executes `continue;` to search for an alternate swap. The loop re-executes `accumulator.BeginTurn(turnNumber)`, which resets `_pendingPlayerDamage = 0`, erasing the committed Card cast's damage before `accumulator.EndTurn` is ever invoked. The authoritative state retains the damage, but the metric is lost.
- **Correction Applied:** In `BalanceSimulator.RunAsync`, `BeginTurn` is invoked only on new Turn entry (`attemptedSwaps.Count == 0`). In `MetricAccumulator.BeginTurn`, re-entry with the same turn number preserves pending damage. In `MetricAccumulator.EndTurn`, pending damage is flushed and zeroed. Across all non-healing runs, `PlayerDamageTotal >= Boss.MaxHP - BossHpMin` is fully restored.

### 4.2 Issue 2: Per-Turn Series Off-By-One on Cast-Terminated Runs (Finding D-194-4)
- **Defect:** In 3 of 75 baseline runs (Kim Lôi Vương × `skilled`, seeds 20261002, 20261003, 20261005), the per-Turn series (`PlayerDamagePerTurn`, `BossDamagePerTurn`, `BossHpPerTurn`, `PlayerHpPerTurn`, `ComboPerTurn`, `ResourceTrace`) contained 8 records while `M-01 Turns` and `M-02 DurationTurns` reported 7 (`Turns + 1`).
- **Root Cause:** When a Card cast delivers the terminal blow, `BalanceSimulator.RunAsync` previously called `accumulator.EndTurn(state)`. However, under authoritative rules (`CARD_RULES.md` §3 item 5, `MATCH3_RULES.md` §8.1 item 5, `DATABASE.md` §1 item 1), a Card cast does not begin, advance, or increment `BattleState.Turn`. Consequently, `state.Turn` remains at the count of committed Swaps ($T = 7$), but `EndTurn` appended an uncommitted phantom 8th entry.
- **Correction Applied:** Implemented `MetricAccumulator.ReconcileTerminalCardCast(state)`: reconciles terminal cast damage and authoritative state directly into the $T$-th turn record without appending an uncommitted Turn $T+1$. Enforces `series.Count == Turns` strictly across all collections.

### 4.3 Issue 3: M-12 Metric Completeness & Lower-Bound Audit (Finding D-194-5)
- **Defect / Observation:**
  - In the TASK-194 specification, **M-12** is defined as Boss Regeneration (`BossRegenerationTotal`, `BossRegenerationTurns`).
  - In the 5 Mộc Yêu × `average` stalemates, M-12 recorded 28 213–29 529 HP, whereas the derived balance `M-03 − net Boss HP loss` demonstrated that 48 537–52 728 HP was actually applied (against an authored trigger ceiling of 77 500–79 250 HP).
  - M-12 is currently measured as positive inter-turn Boss HP deltas (`state.BossState.HP > previous`). Because regeneration resolves inside the same Turn as player damage (`BOSS_RULES.md` §6.2.3, `GAME_RULES.md` §17 step 18a), any regeneration smaller than that Turn's damage is masked, making M-12 a lower bound.
- **Audit & Boundary:**
  - Audit of production event stream confirmed that `PassiveTracker` emits `PassiveTriggered` for `boss-moc-yeu-regen`, but the production domain pipeline emits no `BossHealed` event.
  - **Zero Combat Invention Rule:** The harness strictly adheres to production events and does not synthesize simulated calculations. M-12 is formally documented and preserved as an authoritative empirical lower bound.

### 4.4 Issue 4: Metric Numbering & Specification Schema Mismatch (Finding D-194-1)
- **Defect:** Code comments in `BalanceSimulationMetrics.cs` and `MetricAccumulator.cs` were off-by-one relative to `TASK-193` §3.1 and `TASK-194` §8.
- **Correction Applied:** XML documentation and comments in `BalanceSimulationMetrics.cs` and `MetricAccumulator.cs` aligned with canonical M-01…M-15 specification numbering without altering JSON schema keys or property semantics.

### 4.5 Issue 5: Simulation Mode Member & Configuration Identity (Finding D-194-2)
- **Defect:** In `BalanceSimulationConfiguration.cs`, `Mode` was absent from the configuration record. In `BalanceSimulator.RunAsync`, `var mode = BalanceSimulationMode.Baseline;` was hardcoded.
- **Correction Applied:** Added `public BalanceSimulationMode Mode { get; init; } = BalanceSimulationMode.Baseline;` to `BalanceSimulationConfiguration.cs`. Threaded `_configuration.Mode` through `BalanceSimulator.RunAsync` into `BalanceSimulationResult.Mode`.

### 4.6 Issue 6: PowerSpentTotal & Drain Power Semantics (Finding D-194-7)
- **Observation:** In `passive` runs against Thủy Ma (where player card casts were 0), `PowerSpentTotal` recorded 20–60 Power due to Thủy Ma's Drain Power skill.
- **Action Taken:** Documented that `PowerSpentTotal` measures total Power consumed/drained across all sinks (Card cast costs + Boss Drain Power) per `GAME_RULES.md` §12. Zero production code changed.

### 4.7 Issue 7: Card Damage Attacker Element Verification (Finding D-194-8)
- **Observation:** In TASK-194, Card damage resolved with the Pet's Element (Hỏa).
- **Rule Verification:** Verified against `ELEMENT_RULES.md` §5 and `CARD_RULES.md` §2/§4 that Basic and Pet Skill cards are cast by the player's active Pet and inherit `state.PetState.Element`. Documented as authoritative behavior; zero production code changed.

### 4.8 Issue 8: M-13 / Pet Passive Measurement Boundaries (Finding D-194-6 & Q-11)
- **Observation:** In TASK-194, M-13 recorded Boss passive triggers, but 0 Pet passive triggers.
- **Boundary Enforced:** Verified that Pet passive effects remain unapplied in combat (`BattleStateService` / finding D-2). Documented as an intentional measurement boundary; Pet passives are NOT implemented in TASK-195.

---

## 5. Specification for Corrections

### 5.1 Harness Modifications (`tests/backend/GameServer.Application.Tests/Balance/`)

#### 5.1.1 `BalanceSimulationConfiguration.cs`
- Added:
  ```csharp
  public BalanceSimulationMode Mode { get; init; } = BalanceSimulationMode.Baseline;
  ```

#### 5.1.2 `BalanceSimulator.cs`
- Replaced hardcoded `var mode = BalanceSimulationMode.Baseline;` with `var mode = _configuration.Mode;`.
- Called `accumulator.BeginTurn(turnNumber)` only when entering a new turn (`attemptedSwaps.Count == 0`).
- Reconciled terminal Card cast resolution via `accumulator.ReconcileTerminalCardCast(state)` instead of `accumulator.EndTurn(state)`.

#### 5.1.3 `MetricAccumulator.cs`
- Guarded `BeginTurn` against re-entry with identical turn number.
- In `EndTurn`, zeroed pending damage after flushing.
- Added `ReconcileTerminalCardCast(state)` to attribute terminal card cast damage to totals and last resolved turn record, reconciling `_castTurns[^1]` if greater than `state.Turn`.
- Re-aligned XML doc comments with canonical specification numbering M-01…M-15.

### 5.2 Authoritative Turn-Boundary Invariant Definition

For every simulation run:
```text
metrics.Turns == state.Turn
metrics.DurationTurns == metrics.Turns
metrics.PlayerDamagePerTurn.Count == metrics.Turns
metrics.BossDamagePerTurn.Count == metrics.Turns
metrics.BossHpPerTurn.Count == metrics.Turns
metrics.PlayerHpPerTurn.Count == metrics.Turns
metrics.ComboPerTurn.Count == metrics.Turns
metrics.ResourceTrace.Count == metrics.Turns
metrics.PlayerDamageTotal == metrics.PlayerDamagePerTurn.Sum()
metrics.BossDamageTotal == metrics.BossDamagePerTurn.Sum()
```

---

## 6. Regression Testing Suite

Added `tests/backend/GameServer.Application.Tests/Balance/Tests/BalanceEvidenceMeasurementCorrectionTests.cs` covering:

1. `TASK195_M03_RetainsCardCastDamageAcrossRejectedSwapProposals`:
   Asserts 100% damage retention across rejected Swaps for all 19 affected runs.
2. `TASK195_PerTurnSeries_SatisfiesInvariantOnCastTerminatedRuns`:
   Asserts `series.Count == Turns` strictly on cast-terminated seeds 20261002, 20261003, 20261005.
3. `TASK195_ConfigurationMode_AcceptsControlledComparisonAndThreadsToResult`:
   Asserts `Mode = ControlledComparison` configuration flows through to result.
4. `TASK195_PowerSpentTotal_RecordsDrainPowerWithoutPlayerCardCasts`:
   Asserts Thủy Ma Drain Power deltas in `PowerSpentTotal` on passive runs.
5. `TASK195_CardDamage_CalculatesModifierUsingPetElement`:
   Asserts Advantage (×1.50) against Kim Lôi Vương and Disadvantage (×0.75) against Thủy Ma.
6. `TASK195_PetPassives_DoNotPolluteBossPassiveTriggerCount`:
   Asserts BossPassiveTriggerCount isolates Boss passives and ignores Pet passives.

---

## 7. Acceptance Criteria Verification

- [x] Each identified measurement issue (M-03 damage loss, per-Turn series off-by-one, configuration Mode, metric numbering alignment) has a dedicated regression test.
- [x] Corrected metrics remain 100% deterministic: re-running any configuration produces bit-identical results and identical terminal `BattleState`.
- [x] TASK-193 acceptance criteria H-01 through H-10 remain valid and passing.
- [x] The Turn-boundary invariant is strictly satisfied: `series.Count == metrics.Turns` across all per-Turn collections.
- [x] All 75 baseline simulations are re-executed under corrected harness code.
- [x] Zero runs report `BalanceSimulationOutcome.InvalidSimulation`.
- [x] Corrected evidence replaces and updates the affected TASK-194 JSON artifact and report documentation.
- [x] No production code under `src/` is modified.
- [x] No gameplay rules, balance values, or content definitions are changed.
- [x] Power Charge retains `PowerCost = 0` and Mộc Yêu retains authored regeneration.
- [x] Pet Passives are NOT implemented in production.
- [x] No balance proposal (B-01..B-14) becomes approved.
- [x] Q-1 remains qualitative; no duration pass/fail target is introduced.
- [x] No `BALANCED`, `UNBALANCED`, `PASS`, or `FAIL` verdict is emitted or recorded.

---

## 8. Scope Audit Checklist

```text
Production code changed:           NO  (verified: 0 files under src/ modified)
Gameplay rules changed:            NO
Balance values changed:            NO
Boss stats changed:                NO
Pet / Card / Relic stats changed:  NO
Power Charge cost modified:        NO  (retains PowerCost = 0)
Mộc Yêu regeneration modified:     NO  (retains 5% MaxHP regen)
Pet Passives implemented:          NO  (effects remain unapplied in combat)
Database schema / migrations:      NO
Frontend / client changed:         NO
ADR created or modified:           NO
Balance verdicts introduced:       NO  (zero BALANCED / UNBALANCED / PASS / FAIL)
Q-1 treated as qualitative:        YES (no duration target invented or applied)
Harness code modified:             YES (test-only files under tests/.../Balance/ only)
Evidence artifact updated:         YES (tasks/artifacts/TASK-194-baseline-simulation-results.json)
TASK-194 report updated:           YES (tasks/completed/TASK-194-balance-baseline-simulation-evidence.md)
```

---

## 9. Execution Evidence & Verification

```text
Executed:           2026-10-05
Repository HEAD:    962a1416925ae67ac13a3077c43bf0caf3649c6
Harness:            tests/backend/GameServer.Application.Tests/Balance/
Full-suite command: dotnet test src/backend/GameServer.sln
Full-suite result:  2903 tests passed, 0 failed, 0 skipped
Regression command: dotnet test tests/backend/GameServer.Application.Tests/GameServer.Application.Tests.csproj --filter "FullyQualifiedName~TASK195"
Regression result:  13 tests passed, 0 failed, 0 skipped
Matrix command:     dotnet test tests/backend/GameServer.Application.Tests/GameServer.Application.Tests.csproj --filter "FullyQualifiedName~BalanceBaselineMatrixTests"
Matrix result:      5 tests passed, 0 failed, 0 skipped
Harness command:    dotnet test tests/backend/GameServer.Application.Tests/GameServer.Application.Tests.csproj --filter "FullyQualifiedName~BalanceSimulatorTests"
Harness result:     32 tests passed, 0 failed, 0 skipped

Raw Artifact:       tasks/artifacts/TASK-194-baseline-simulation-results.json
Size:               2 671 329 bytes
SHA-256:            4F9B5077B9320B09BD83C9144FF9B1234566D067B5FE0EF9BCD85FCB1CE6059F
Determinism:        Byte-for-byte identical across runs
```
