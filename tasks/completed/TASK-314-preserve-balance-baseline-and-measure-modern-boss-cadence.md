# TASK-314 — Historical Baseline Preservation and Modern Boss Card Cadence

---

## Metadata

```text
Task ID:           TASK-314
Type:              BUG
Status:            DONE
Risk:              LOW
Priority:          HIGH
Primary Agent:     testing
Supporting Agents: gameplay, backend, review
Workflow:          development/bug-fix.md
Skills:            testing/test-scenario-generation, discovery/impact-analysis, quality/scope-validation
Dependencies:      TASK-313
Declared Files:    tests/backend/GameServer.Application.Tests/Balance/Tests/BalanceEvidenceMeasurementCorrectionTests.cs, tests/backend/GameServer.Application.Tests/Balance/Tests/ModernBossCardCadenceMeasurementTests.cs
```

---

## Objective

Preserve historical balance baseline evidence and build a separate, explicitly named suite that measures modern canonical Boss Card casting and cadence behavior across all five MVP Bosses under operator decision OD-314-1 = C:
1. Preserve historical baseline files, matrix definitions, seeds, and historical claims unchanged; do not add Boss Cards to `BalanceBaselineMatrix` merely to mimic current runtime behavior.
2. Resolve historical balance test failures in `BalanceEvidenceMeasurementCorrectionTests`:
   - Retarget `TASK195_PerTurnSeries_ReconcilesTerminalCardCastWithoutPhantomTurn` to seed `20261001UL`, which genuinely terminates through a player Card cast under current runtime without legacy Boss Skill, verifying that terminal card casts reconcile without phantom turns.
   - Reconcile `TASK296_ReviewD_Canonical75RunCandidate_Verification` to distinguish historical reproducibility verification of Candidate D (8/41/26 distribution, hash, 9 shifted runs, 66 unshifted runs against TASK-194) from post-Slice-5 live runtime execution (10/39/26 distribution without Boss Cards, with Sơn Thạch Vệ seeds 20261003/20261005 surviving without legacy skill damage).
3. Establish a separate, modern Boss Card cadence measurement suite (`ModernBossCardCadenceMeasurementTests.cs`) that provisions all five MVP Boss Cards (`ProvisionedBossCardContent.All`), measures canonical `BossCardCast` events through the supported event contract, verifies exact single counting in telemetry (M-13 `BossCardCastCount`), asserts casting conditions and the one-action policy, and contrasts with the structurally unavailable 0 casts in offline scenarios.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — MVP Boss abilities are IN; Fifth Gem is OUT/FUTURE
- `docs/01-game-design/BOSS_RULES.md` §4.1, §4.3, §6.1, §6.3 — Canonical Boss Cards, costs, cooldowns, cast priority scores, one-action response policy
- `docs/01-game-design/COMBAT_RULES.md` §3.4 — Boss abilities deal zero direct damage (D3)
- `docs/02-technical/GAME_EVENTS.md` §1, §2 — Canonical `BossCardCast` event
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.30 — `BossCardCast` wire schema
- `docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md` — Operator decisions OD-311-1 = B, OD-311-2 = A, Slice 5 delivered

---

## Scope

### In Scope
- Correcting test contract in `tests/backend/GameServer.Application.Tests/Balance/Tests/BalanceEvidenceMeasurementCorrectionTests.cs`.
- Building `tests/backend/GameServer.Application.Tests/Balance/Tests/ModernBossCardCadenceMeasurementTests.cs`.
- Running focused and full test suites to verify determinism and zero regressions.

### Out of Scope
- Adding Boss Cards to `BalanceBaselineMatrix.Cards` or modifying historical baseline matrix definitions.
- Overwriting or mutating pinned historical artifacts (`TASK-194-baseline-simulation-results.json`, `TASK-197-controlled-balance-experiments.json`, `TASK-296-canonical-75-candidate.json`).
- Modifying production combat rules, damage values, boss stats, or action policy.
- Reintroducing `BossState.SkillCharge`, legacy Boss Skill execution/fallback, or `BossSkillCast`.
- Fifth Gem / Power-drain Gem work (`MVP_SCOPE.md` §3).

---

## Current State

Post-TASK-313, legacy Boss Skill fallback is completely removed. In the offline historical baseline matrix (`BalanceBaselineMatrix`), which provisions only the four Player cards, Boss abilities cannot fire (M-13 is structurally 0). Two historical tests in `BalanceEvidenceMeasurementCorrectionTests.cs` failed because seed 20261002 shifted terminal reason from Card cast to Swap, and live matrix execution yields 10/39/26 instead of the pre-Slice-5 Candidate D 8/41/26 distribution.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-314:
```text
tests/backend/GameServer.Application.Tests/Balance/Tests/BalanceEvidenceMeasurementCorrectionTests.cs
tests/backend/GameServer.Application.Tests/Balance/Tests/ModernBossCardCadenceMeasurementTests.cs
```

---

## Acceptance Criteria

- [x] 1. Historical baseline artifacts and `BalanceBaselineMatrix` definition are preserved untouched without added Boss Cards.
- [x] 2. `TASK195_PerTurnSeries_ReconcilesTerminalCardCastWithoutPhantomTurn` passes on a card-terminated run (`20261001UL`), verifying dimensional series invariants without phantom turns.
- [x] 3. `TASK296_ReviewD_Canonical75RunCandidate_Verification` distinguishes historical Candidate D artifact verification from post-Slice-5 live runtime execution without falsifying historical evidence.
- [x] 4. A separate modern Boss Card cadence suite (`ModernBossCardCadenceMeasurementTests.cs`) covers all five MVP Bosses with provisioned Boss Cards, asserting canonical `BossCardCast` event emission, exact single counting in telemetry (M-13 `BossCardCastCount`), and the one-action response policy.
- [x] 5. Structurally unavailable 0 casts in offline scenarios lacking Boss Cards are explicitly tested and documented as unmeasured, not zero live cadence.
- [x] 6. All backend tests pass (`dotnet test src/backend/GameServer.sln`).
- [x] 7. Two-stage commit protocol (Phase A implementation commit, Phase B completion record) is strictly followed without pushing.

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[x] tests/ (unit / integration / gameplay scenarios / balance telemetry)
[ ] docs/ (documentation updates if applicable)
```

---

## Implementation Notes

- `BalanceEvidenceMeasurementCorrectionTests.cs`: Retargeted `TASK195_PerTurnSeries_ReconcilesTerminalCardCastWithoutPhantomTurn` to seed `20261001UL`. Reconciled `TASK296_ReviewD_Canonical75RunCandidate_Verification` to verify historical Candidate D artifact and assert current runtime behavior without overwriting `TASK-296-canonical-75-candidate.json`.
- `ModernBossCardCadenceMeasurementTests.cs`: Used `ProvisionedBossCardContent.All` behind `TestCardDefinitionLookup` to measure in-battle cast frequency across all five Bosses.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — MetricAccumulator M-13 BossCardCastCount exact counting
[x] Integration tests  — BattleStateService with ProvisionedBossCardContent for all 5 Bosses
[x] Gameplay scenarios — One-action policy: either/or BossCardCast vs Swap
```

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If task requires modifying production gameplay logic or balance values: STOP per `AGENTS.md` §16
- If task touches Fifth Gem / Power-drain Gem: STOP (`MVP_SCOPE.md` §3)

---

## Completion Evidence

### Commit
- Implementation slice (Phase A): `1f110bb1a21ea1123af8bb3c06d1cc7006336b29`
- Subject: `test: preserve historical balance baseline and verify modern boss card cadence`
- Owns: `TASK-314`
- Files:
  - `tests/backend/GameServer.Application.Tests/Balance/Tests/BalanceEvidenceMeasurementCorrectionTests.cs`
  - `tests/backend/GameServer.Application.Tests/Balance/Tests/ModernBossCardCadenceMeasurementTests.cs`
- Shared with: `none`
- Unowned / pre-existing: `none`

### Changed Files
- `tests/backend/GameServer.Application.Tests/Balance/Tests/BalanceEvidenceMeasurementCorrectionTests.cs` — retargeted `TASK195_PerTurnSeries_ReconcilesTerminalCardCastWithoutPhantomTurn` to seed `20261001UL` (verifying card-terminated resolution without phantom turns under post-Slice-5 runtime); reconciled `TASK296_ReviewD_Canonical75RunCandidate_Verification` to distinguish historical Candidate D artifact verification (8/41/26) from current-runtime offline matrix execution (10/39/26 with Sơn Thạch Vệ seeds 20261003/20261005 shifting to victory due to absent legacy skill damage), asserting M-13 is structurally unavailable without overwriting protected artifacts.
- `tests/backend/GameServer.Application.Tests/Balance/Tests/ModernBossCardCadenceMeasurementTests.cs` — added dedicated modern Boss Card cadence suite covering all five MVP Bosses with provisioned Boss Cards, testing canonical `BossCardCast` event emission, exact single counting in telemetry (M-13 `BossCardCastCount`), in-battle execution, one-action response policy, and contrasting with offline structural unavailability.

### Validation Results
- `dotnet test tests/backend/GameServer.Domain.Tests/GameServer.Domain.Tests.csproj` — **PASS** (1674/1674 passed)
- `dotnet test tests/backend/GameServer.Infrastructure.Tests/GameServer.Infrastructure.Tests.csproj` — **PASS** (507/507 passed)
- `dotnet test tests/backend/GameServer.Api.Tests/GameServer.Api.Tests.csproj` — **PASS** (399/399 passed)
- `dotnet test tests/backend/GameServer.Application.Tests/GameServer.Application.Tests.csproj` — **PASS** (788/788 passed)
- `npm run test:run` (`src/frontend/client`) — **PASS** (24 files, 1267 passed)
- `npm run build` (`src/frontend/client`) — **PASS** (`tsc && vite build`, exit 0)
- `git diff --check` — **PASS** (0 errors)

> **Evidence correction (TASK-319).** This section previously reported the
> Application suite as `783/783 passed`, which made the four per-project counts
> sum to 3363. Re-measured at TASK-319, the Application suite executes **788**
> tests, and the backend aggregate is **3368**, not 3363.
>
> **Reproduced counts (TASK-319).** Each recorded project command was invoked in
> turn on this tree. The Application figure was reproduced twice independently:
> once by a full run of this exact command (`Passed: 788`), and once by
> discovery over the TASK-314 tree (`--list-tests`, with TASK-318's later edits
> removed, listed exactly 788 cases of which 12 belong to this task's cadence
> suite):
>
> ```text
> Domain           1674 passed
> Infrastructure    507 passed
> Api               399 passed
> Application       788 passed   (12 of them from ModernBossCardCadenceMeasurementTests)
> ------------------------------
> aggregate        3368 passed, 0 failed, 0 skipped
> ```
>
> **The cause is a transcription error in the record, not discovery,
> configuration, or environment.** The reproduction above is decisive on that
> point: the same tree, the same project configuration and the same command
> produce 788, and the cadence suite this task added accounts for 12 of them.
> The executed-test count of a test file equals its `[Fact]` methods plus its
> `[InlineData]` rows; that rule was validated against a project this
> correction did not touch (Infrastructure: 457 `[Fact]` + 50 `[InlineData]` =
> 507, exactly the number the suite executes).
>
> ```text
> TASK-314 (Phase A 1f110bb) — the two files this task declares
>   ModernBossCardCadenceMeasurementTests   2 [Fact] + 10 [InlineData] = 12 cases (new)
>   BalanceEvidenceMeasurementCorrectionTests   methods retargeted in place,
>                                              0 cases added or removed,
>                                              suite still executes 15 cases
>   ----------------------------------------------------------------------
>   cases newly executable by this task                                   = 12
>
> observed Application total on the TASK-314 tree                          = 788
> observed Application total with TASK-318's edits applied (current tree)   = 785
>   (TASK-318 replaced the five-range TASK314_InBattleExecution Theory with
>    two Facts: 12 - 5 + 2 = 9 cadence cases, so 788 - 3 = 785)
> ```
>
> The figure previously recorded, `783`, is 788 − 5: it equals the current tree's
> cadence count substituted into a total that belongs to the earlier tree. It is
> therefore a transcription slip rather than a count produced by any
> configuration of this repository, and `788` is the reproducible figure now
> recorded above.
>
> **Residual discrepancy, disclosed rather than absorbed.** TASK-313's record
> states a pre-TASK-314 Application count of 785. That base plus this task's
> twelve new cases predicts 797 for the TASK-314 tree, while the tree actually
> executes 788. This correction cannot attribute the 9-case gap without
> re-measuring the historical commit, and that earlier figure belongs to a
> completed task this correction does not own. It is reported here as an open
> evidence discrepancy for follow-up; it does not change the reproducible
> figures above, and it is not silently reconciled.
>
> **Scope of the correction.** Only the recorded numeric evidence changed. This
> task's implementation decisions, declared file set, acceptance criteria, and
> the Phase A commit `1f110bb` are unchanged (`TASK_LIFECYCLE.md` §3 — completed
> tasks are otherwise immutable). The tree after TASK-318 measures **3365**
> backend tests (Domain 1674, Infrastructure 507, Api 399, Application 785);
> those additions are recorded in TASK-318, not retroactively here.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed P-1…P-6 commit policy compliance
