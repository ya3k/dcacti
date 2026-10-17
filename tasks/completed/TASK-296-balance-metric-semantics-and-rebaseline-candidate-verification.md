# TASK-296 — Balance Metric Semantics and Re-baseline Candidate Verification

- **Task ID:** TASK-296
- **Type:** Verification, Testing, and Metric Analysis
- **Status:** Completed (Assessment & Proposed Diffs; Awaiting Product Owner Balance Re-baseline Approval)
- **Dependencies:** TASK-194, TASK-195, TASK-197, TASK-290, TASK-291, TASK-292, ADR-026, ADR-028

---

## 1. Executive Summary

TASK-296 resolves the identified balance-test semantic issues and independently verifies the proposed TASK-194 baseline under the approved match-only gameplay rules (ADR-026, ADR-028, TASK-290..293).

Pinned historical artifacts (`tasks/artifacts/TASK-194-baseline-simulation-results.json` and `tasks/artifacts/TASK-197-controlled-balance-experiments.json`) were preserved and not overwritten. Operator-owned `docs/03-decisions/ADR/ADR-028-retire-special-gems-from-battle-state-model.md` and `docs/03-decisions/README.md` were preserved untouched.

---

## 2. Workstream A — Metric Semantics & Root Cause Analysis

### A1. PowerSpentTotal (`BalanceEvidenceMeasurementCorrectionTests.cs`)
- **Root Cause:** Test expectations (`expectedPowerSpent`, `expectedBossSkillCasts`) were hardcoded in TASK-195 based on the pre-TASK-290 simulation board state (with special gems and match-tier multipliers). Under approved match-only gameplay rules, board evolution and swap choices shifted skill charging and pet power timings.
- **Event-Level Evidence (Thủy Ma × Passive):**
  - **Seed 20261001:** Turn 4: BossSkillCast #1 (`drain-power`); Pet Power was 30 $\to$ drained 20 (powerAfter=10). Terminal at Turn 9, PetPower=70. Total skill casts: 1; total power drained: 20.
  - **Seed 20261002:** Turn 3: BossSkillCast #1 (Pet Power 0 $\to$ drained 0); Turn 6: BossSkillCast #2 (Pet Power 0 $\to$ drained 0); Turn 12: BossSkillCast #3 (Pet Power 30 $\to$ drained 20, powerAfter=10). Terminal at Turn 14. Total skill casts: 3; total power drained: 20.
  - **Seed 20261003:** Turn 8: BossSkillCast #1 (Pet Power 90 $\to$ drained 20); Turn 12: BossSkillCast #2 (Pet Power 100 $\to$ drained 20). Terminal at Turn 13. Total skill casts: 2; total power drained: 40.
  - **Seed 20261004:** Turn 3: BossSkillCast #1 (Pet Power 0 $\to$ drained 0); Turn 7: BossSkillCast #2 (Pet Power 0 $\to$ drained 0). Terminal at Turn 9. Total skill casts: 2; total power drained: 0.
  - **Seed 20261005:** Turn 3: BossSkillCast #1 (Pet Power 30 $\to$ drained 20); Turn 9: BossSkillCast #2 (Pet Power 100 $\to$ drained 20); Turn 14: BossSkillCast #3 (Pet Power 100 $\to$ drained 20). Terminal at Turn 14. Total skill casts: 3; total power drained: 60.
- **Zero-Power Casts:**
  When a Boss casts `drain-power` while Pet Power is 0, `BattleEvent.ForBossSkillCast` is emitted, incrementing `BossSkillCastCount`. However, `PetState.Power = Math.Max(0, 0 - 20) = 0`, resulting in a drain of 0. Per `BattleStateService.cs:2195`, `BattleEvent.ForPowerChanged` is only emitted when `drained > 0`. `PowerSpentTotal` correctly sums negative power changes. Thus, zero-power casts count toward skill casts but add 0 to `PowerSpentTotal`.
- **Metric Conformance:** Matches documented definitions in `TASK-194` §8 and `GAME_RULES.md` §12.

### A2. PerTurnSeries (`BalanceEvidenceMeasurementCorrectionTests.cs`)
- **Primary Contract:** Finding D-194-4 established that the primary contract is the dimensional invariant `series.Count == metrics.Turns` across all per-turn collections (`PlayerDamagePerTurn`, `BossDamagePerTurn`, `BossHpPerTurn`, `PlayerHpPerTurn`, `ComboPerTurn`, `ResourceTrace`), ensuring no phantom Turn $T+1$ record is emitted.
- **Outcome Shift:** Under match-only rules:
  - Kim Lôi Vương × skilled seed 20261002 still terminates via Card cast (`OutcomeDetail: Card cast resolved the battle (Victory)`).
  - Seeds 20261003 and 20261005 now terminate via a committed Swap (`OutcomeDetail: the committed Swap resolved the battle (Victory)`).
  - All three seeds strictly satisfy `series.Count == metrics.Turns`, `DurationTurns == Turns`, and damage sums.
- **Separation of Invariants:** Separated the dimensional series invariant test (`TASK195_PerTurnSeries_SatisfiesDimensionalInvariant` over seeds 20261002, 20261003, 20261005) from the card-cast termination reconciliation assertion (`TASK195_PerTurnSeries_ReconcilesTerminalCardCastWithoutPhantomTurn` over genuine card-terminated seed 20261002). Neither assertion is weakened; no artificial seeds are introduced.

### A3. BossRegenerationTotal (`BalanceBaselineMatrixTests.cs`, `BalanceSimulatorTests.cs`)
- **Sources of Boss HP Increase:**
  1. *Passive Regeneration:* Mộc Yêu's `boss-moc-yeu-regen` (5% MaxHP per passive trigger, clamped to MaxHP).
  2. *HP-Gem Resource Contribution:* Under ADR-026 shared-board rules, all Bosses act via swaps and match HP gems, gaining `generation.HealPool` (+20 HP per HP gem) via `ResourceGenerator.ApplyActorMatchEffects` / `ApplyBossHeal`.
  3. *Other Healing:* None in MVP (no cards or relics heal the boss).
  4. *Max-HP Clamping:* `Math.Min(bossState.HP + heal, bossState.MaxHP)` strictly caps Boss HP to MaxHP, discarding overheal.
- **Current Metric Semantics:** `MetricAccumulator.EndTurn` measures positive inter-turn Boss HP movement (`state.BossState.HP > previous`). Under ADR-026, non-Mộc Yêu bosses also match HP gems on their turns and heal (54 of 60 non-Mộc Yêu runs in the canonical baseline have `BossRegenerationTotal > 0`).
- **Test Semantic Issues:**
  - `BalanceBaselineMatrixTests.cs:236` and `BalanceSimulatorTests.cs:483` asserted `BossRegenerationTotal == 0` for non-Mộc Yêu bosses based on pre-ADR-026 assumptions. Under ADR-026 shared-board gameplay, the valid invariant is `BossRegenerationTotal >= 0`.
  - In `BalanceSimulatorTests.cs:510` (`H06_MocYeuRegeneration_IsMeasured`), Mộc Yêu was configured with `MaxHP = 20000, ATK = 0` for 40 turns with average policy. With ADR-026 shared-board DEF matches, Mộc Yêu generated Shields that absorbed all player damage, preventing HP from ever dropping below 20000. Configuring `policy: BalancePolicies.SkilledName` delivers offensive matches that pierce shields, allowing regeneration to be observed.
- **Authoritative Events Evaluation:** `PassiveTriggered` events exist for `boss-moc-yeu-regen`, but `GAME_EVENTS.md` defines no `Healed` event. A nominal calculation from passive triggers would omit clamping. Measuring applied regeneration from authoritative events would require a new domain event in `GAME_EVENTS.md`.

---

## 3. Workstream B — Canonical 75-Run Verification

The canonical 75-run baseline matrix was executed from current HEAD:
- **Run Count:** 75/75 completed.
- **Invalid Simulations:** 0.
- **Outcome Counts:**
  - Pinned (TASK-194): Victory: 5, Defeat: 48, Stalemate: 22, InvalidSimulation: 0
  - Current HEAD: Victory: 8, Defeat: 41, Stalemate: 26, InvalidSimulation: 0
  - Net Delta: Victory +3, Defeat -7, Stalemate +4
- **Shifted Runs (Exactly 9):**
  1. `boss-thuy-ma / average / 20261001`: Defeat (Turn 43) $\to$ Stalemate (Turn 1000)
  2. `boss-thuy-ma / average / 20261003`: Defeat (Turn 45) $\to$ Stalemate (Turn 1000)
  3. `boss-thuy-ma / average / 20261005`: Defeat (Turn 191) $\to$ Stalemate (Turn 1000)
  4. `boss-moc-yeu / skilled / 20261004`: Defeat (Turn 42) $\to$ Stalemate (Turn 1000)
  5. `boss-son-thach-ve / skilled / 20261001`: Defeat (Turn 46) $\to$ Victory (Turn 378, Card cast)
  6. `boss-son-thach-ve / skilled / 20261002`: Defeat (Turn 140) $\to$ Victory (Turn 233, Swap)
  7. `boss-son-thach-ve / skilled / 20261003`: Victory (Turn 100) $\to$ Defeat (Turn 151, Swap)
  8. `boss-son-thach-ve / skilled / 20261004`: Defeat (Turn 38) $\to$ Victory (Turn 295, Card cast)
  9. `boss-kim-loi-vuong / skilled / 20261004`: Defeat (Turn 42) $\to$ Victory (Turn 36, Card cast)
- **Unshifted Runs:** All other 66 runs retain their exact historical outcomes.
- **Determinism:** `TASK194_EveryRun_IsReproducedBitIdenticallyOnReRun` confirms 100% bit-identical determinism across runs for all outcomes, metrics, and final states.

---

## 4. Workstream C — Proposed Diffs & Owner Approvals

### Diff 1: Test Semantic Fixes
- `BalanceEvidenceMeasurementCorrectionTests.cs`:
  - Align `TASK195_PowerSpentTotal` expectations to verified event-level values.
  - Strengthen `TASK195_PerTurnSeries` with separate dimensional series invariant checks and terminal card-cast reconciliation without phantom turns.
  - Add targeted M-12 unit tests verifying known positive inter-turn HP delta measurement, zero delta on non-increases, non-Mộc Yêu boss HP-gem healing under ADR-026, and isolation of clamped passive triggers at MaxHP.
  - Add automated 75-run candidate verification test checking all 75 runs, 0 invalid simulations, 8/41/26 distribution, 9 shifted runs, and 66 unshifted runs against historical artifact.
- `BalanceBaselineMatrixTests.cs`:
  - Update M-12 assertion for all bosses to `Assert.True(metrics.BossRegenerationTotal >= 0)` to accommodate ADR-026 HP-gem healing.
- `BalanceSimulatorTests.cs`:
  - Update `H06_EveryMetric` to assert `Assert.True(m.BossRegenerationTotal >= 0)`.
  - Update `H06_MocYeuRegeneration_IsMeasured` to use `policy: BalancePolicies.SkilledName`.

### Diff 2: Metric Implementation Changes (Status: Held Pending Approval)
- Retain `MetricAccumulator.cs` M-12 as the empirical lower bound of all inter-turn Boss HP increases. Altering metric semantics or adding domain events requires Product Owner approval.

### Diff 3: TASK-194 Artifact Re-baseline (Status: Candidate Prepared; Not Overwritten)
- Pinned historical artifact `tasks/artifacts/TASK-194-baseline-simulation-results.json` preserved untouched. Candidate artifact verified (SHA-256 `A9E9ABBD9B342C9B8849E5CECB1580D3154915782F539F5A3547252E4EE4FC62`) and available for overwrite upon Product Owner approval.

### Diff 4: TASK-197 Artifact Preservation (Status: No-Change Recommendation)
- Pinned historical artifact `tasks/artifacts/TASK-197-controlled-balance-experiments.json` preserved untouched (SHA-256 `AA3D87ED523C44D835D6FBE722FD59A4DEA50835B5ABC8F4A81B9A404140E08A`).
- Explicit no-change recommendation: TASK-197 is a historical experimental evidence archive for TASK-198 decisions, not a continuous baseline. Any future match-only experiment matrix should be commissioned under a new task identifier.

---

## 5. M-12 Decision Note: Boss Regeneration Semantics

### Option A: Empirical Measure of Observed Inter-Turn HP Increases (Recommended for MVP)
- **Description:** Maintain the existing `MetricAccumulator` semantics where `BossRegenerationTotal` records positive inter-turn Boss HP deltas ($HP_{t} - HP_{t-1} > 0$).
- **Scope:** Test and documentation updates only; zero production code or event schema changes.
- **Trade-off:**
  - *Pros:* Zero production risk, zero protocol/event changes, fully deterministic, reflects net effective healing that actually improved the Boss's survivability.
  - *Cons:* Blends Mộc Yêu passive regeneration with ADR-026 HP-gem healing; represents an empirical lower bound rather than gross healing due to intra-turn damage offset and MaxHP clamping.

### Option B: Authoritative Separately Attributable Healing Metric
- **Description:** Introduce an authoritative `Healed` domain event in `GAME_EVENTS.md` and wire discriminator in `SIGNALR_PROTOCOL.md`, emitted by `ResourceGenerator.ApplyBossHeal` (source = `"gem"`) and `BattleStateService` Step 18a (source = `"passive"`).
- **Scope:** Cross-cutting production changes across `Domain`, `Application`, `Api`, SignalR protocol contracts, and frontend event consumers.
- **Trade-off:**
  - *Pros:* Fully isolates passive regeneration from board match healing; captures clamped and intra-turn healing amounts.
  - *Cons:* Substantial architectural scope expansion, requires Product Owner approval, protocol bump, and client event handler updates.

---

## 6. Required Approvals & Remaining Blockers

1. **Product Owner Approval Needed:**
   - Formal acceptance of the new baseline distribution under match-only gameplay rules (Victory: 8, Defeat: 41, Stalemate: 26).
   - Authorization to commit Diff 1 (test semantic fixes) and Diff 3 (overwrite TASK-194 pinned artifact).
   - Selection between Option A and Option B for M-12 metric semantics.
2. **Remaining Blockers:**
   - Formal sign-off on balance re-baseline and authorization of pinned artifact replacement.
