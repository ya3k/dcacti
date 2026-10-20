# TASK-313 — ADR-029 Slice 5: SkillCharge and Legacy Boss Skill Removal

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
-->

---

## Metadata

```text
Task ID:           TASK-313
Type:              REFACTOR
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     backend
Supporting Agents: realtime, client, testing, review
Workflow:          development/refactor.md
Skills:            quality/scope-validation,
                   quality/architecture-conformance,
                   backend/api-contract-validation,
                   realtime/realtime-protocol-validation,
                   quality/testing
Dependencies:      TASK-311 (DONE), ADR-029 (Accepted, amended), Operator Decision OD-311-1 (RESOLVED = B), Operator Decision OD-311-2 (RESOLVED = A), Operator implementation authorization (GRANTED)
Declared Files:    src/backend/GameServer.Domain/Battle/BossState.cs,
                   src/backend/GameServer.Domain/Bosses/BossDefinition.cs,
                   src/backend/GameServer.Domain/Bosses/BossDefinitions.cs,
                   src/backend/GameServer.Domain/Match3/BattleEvent.cs,
                   src/backend/GameServer.Domain/Match3/ResourceGenerator.cs,
                   src/backend/GameServer.Domain/Battle/Serialization/BattleStateJson.cs,
                   src/backend/GameServer.Domain/Battle/Serialization/BattleStateSerializer.cs,
                   src/backend/GameServer.Domain/Cards/CardCastExecutor.cs,
                   src/backend/GameServer.Application/Battle/BattleStateService.cs,
                   src/backend/GameServer.Infrastructure/Postgres/Configurations/BossDefinitionConfiguration.cs,
                   src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs,
                   src/backend/GameServer.Api/Hubs/BattleHub.cs,
                   src/frontend/client/src/game/scenes/BattleScene.ts,
                   src/frontend/client/src/game/scenes/BattleEventPresenter.ts,
                   src/frontend/client/src/game/presentation/GemMatchSummaryBanner.ts,
                   src/frontend/client/src/services/api/BattleModels.ts,
                   src/frontend/client/src/services/realtime/SignalRService.ts,
                   src/frontend/client/scripts/standalone-web-smoke.mjs,
                   tests/backend/GameServer.Domain.Tests/BossStateTests.cs,
                   tests/backend/GameServer.Domain.Tests/ActorRelativeMatchEffectsTests.cs,
                   tests/backend/GameServer.Domain.Tests/BattleStateSerializationTests.cs,
                   tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs,
                   tests/backend/GameServer.Domain.Tests/BossSkillContractTests.cs,
                   tests/backend/GameServer.Domain.Tests/DamagePipelineTests.cs,
                   tests/backend/GameServer.Domain.Tests/EffectiveBossAttackTests.cs,
                   tests/backend/GameServer.Domain.Tests/Cards/CardCastExecutorTests.cs,
                   tests/backend/GameServer.Domain.Tests/Cards/BossCardCastExecutorTests.cs,
                   tests/backend/GameServer.Domain.Tests/StatusEffectLifecycleTests.cs,
                   tests/backend/GameServer.Domain.Tests/Task172BossDefinitionsContentTests.cs,
                   tests/backend/GameServer.Application.Tests/AtomicRoundOrchestrationTests.cs,
                   tests/backend/GameServer.Application.Tests/BossResponseTests.cs,
                   tests/backend/GameServer.Application.Tests/BossSkillSecondaryEffectTests.cs,
                   tests/backend/GameServer.Application.Tests/BossSkillStep1CompositionTests.cs,
                   tests/backend/GameServer.Application.Tests/BattleStateServiceTests.cs,
                   tests/backend/GameServer.Application.Tests/BattleStateSerializationLifecycleTests.cs,
                   tests/backend/GameServer.Application.Tests/BattleResultTerminalFlowTests.cs,
                   tests/backend/GameServer.Application.Tests/BattleStateServiceDeadlockRecoveryTests.cs,
                   tests/backend/GameServer.Application.Tests/BattleStateServiceTerminalActionGuardTests.cs,
                   tests/backend/GameServer.Application.Tests/HpGemMechanicsAuditTests.cs,
                   tests/backend/GameServer.Application.Tests/RootAttackConsumptionTests.cs,
                   tests/backend/GameServer.Application.Tests/RoundTestSupport.cs,
                   tests/backend/GameServer.Application.Tests/Task172BossPassiveRuntimeTests.cs,
                   tests/backend/GameServer.Application.Tests/VictoryDefeatTests.cs,
                   tests/backend/GameServer.Application.Tests/BossCardCastRuntimeTests.cs,
                   tests/backend/GameServer.Application.Tests/TASK309BossCardEffectRuntimeTests.cs,
                   tests/backend/GameServer.Application.Tests/Battle/PowerChangedEmissionTests.cs,
                   tests/backend/GameServer.Application.Tests/Balance/MetricAccumulator.cs,
                   tests/backend/GameServer.Application.Tests/Balance/BalanceSimulationMetrics.cs,
                   tests/backend/GameServer.Application.Tests/Balance/BalanceBaselineMatrix.cs,
                   tests/backend/GameServer.Application.Tests/Balance/BalanceControlledExperimentMatrix.cs,
                   tests/backend/GameServer.Application.Tests/Balance/TASK298ExperimentRunner.cs,
                   tests/backend/GameServer.Application.Tests/Balance/Tests/TASK298ExperimentTests.cs,
                   tests/backend/GameServer.Application.Tests/Balance/Tests/BalanceBaselineMatrixTests.cs,
                   tests/backend/GameServer.Application.Tests/Balance/Tests/BalanceSimulatorTests.cs,
                   tests/backend/GameServer.Application.Tests/Balance/Tests/BalanceEvidenceMeasurementCorrectionTests.cs,
                   tests/backend/GameServer.Application.Tests/Balance/TASK309/BossCardCastFrequencyAnalysisTests.cs,
                   tests/backend/GameServer.Infrastructure.Tests/BossPersistenceTests.cs,
                   tests/backend/GameServer.Infrastructure.Tests/BossDefinitionBalanceLookupTests.cs,
                   tests/backend/GameServer.Infrastructure.Tests/BossDefinitionPostgresProvisioningTests.cs,
                   tests/backend/GameServer.Api.Tests/BossResponseWireTests.cs,
                   tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs,
                   tests/backend/GameServer.Api.Tests/BossWireProjectionTests.cs,
                   tests/backend/GameServer.Api.Tests/BattleHistoryEndpointTests.cs,
                   tests/backend/GameServer.Api.Tests/BattleResultEndpointTests.cs,
                   src/frontend/client/tests/SceneLifecycle.test.ts,
                   src/frontend/client/tests/GameRuntime.test.ts,
                   src/frontend/client/tests/BattleEventPresentation.test.ts,
                   src/frontend/client/tests/RuntimeBoundaries.test.ts,
                   docs/01-game-design/BOSS_RULES.md,
                   docs/01-game-design/GAME_RULES.md,
                   docs/01-game-design/COMBAT_RULES.md,
                   docs/01-game-design/CARD_RULES.md,
                   docs/02-technical/GAME_STATE.md,
                   docs/02-technical/GAME_EVENTS.md,
                   docs/02-technical/SIGNALR_PROTOCOL.md,
                   docs/02-technical/REDIS_STATE.md,
                   docs/02-technical/DATABASE.md,
                   docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md,
                   docs/03-decisions/README.md,
                   .ai/PROJECT_STATE.md
```

**Lifecycle:** `BACKLOG → READY` (registration validated: docs exist, MVP scope confirmed, dependencies resolved, primary agent and workflow assigned, acceptance criteria testable) and `READY → IN PROGRESS` (operator decisions resolved and implementation authorized; record moved to `tasks/active/`).

**Manifest Revision R2 (Phase 0 inventory resolution).** The R1 declared set was evidence-audited and corrected before any implementation file was edited:
- **Removed (nonexistent path):** `src/backend/GameServer.Domain/Bosses/BossSkillDefinition.cs` does not exist in the repository. `BossSkillDefinition`/`BossSkillSecondaryEffect`/`BossSkillSecondaryEffectKind` are declared inside `src/backend/GameServer.Domain/Bosses/BossDefinition.cs`, which is declared.
- **Removed (no change required):** `src/backend/GameServer.Infrastructure/Redis/BattleStateRepository.cs` contains no `SkillCharge`/`BossSkillCast` reference; it delegates to `BattleStateSerializer`, whose tolerant read is covered by `BattleStateSerializationLifecycleTests` (declared). TASK-313's Redis obligation is a verification obligation, not a code change in that file.
- **Added — omission evidence (TASK-312's suspected telemetry consumers confirmed):** `tests/backend/GameServer.Application.Tests/Balance/MetricAccumulator.cs` (`case BattleEventType.BossSkillCast` at §185 and `BossSkillCastCount` at §385/§427) and `tests/backend/GameServer.Application.Tests/Balance/BalanceSimulationMetrics.cs` (`BossSkillCastCount` at §261) both consume the removed event and must change. They were absent from R1.
- **Added — further discovered consumers (all evidenced by direct references to removed symbols):** `BattleEventEmissionTests.cs`, `BossSkillContractTests.cs`, `DamagePipelineTests.cs`, `EffectiveBossAttackTests.cs` (Domain); `BattleResultTerminalFlowTests.cs`, `BattleStateServiceDeadlockRecoveryTests.cs`, `BattleStateServiceTerminalActionGuardTests.cs`, `HpGemMechanicsAuditTests.cs`, `MatchedGemSummaryEmissionTests.cs`, `RoundTestSupport.cs`, `Task172BossPassiveRuntimeTests.cs`, `VictoryDefeatTests.cs`, `TASK309BossCardEffectRuntimeTests.cs`, `Balance/BalanceBaselineMatrixTests.cs`, `Balance/Tests/BalanceSimulatorTests.cs` (Application); `BossWireProjectionTests.cs`, `TASK309BossCardWireTests.cs`, `BattleHistoryEndpointTests.cs`, `BattleResultEndpointTests.cs` (Api); `src/backend/GameServer.Domain/Cards/CardCastExecutor.cs`, `src/frontend/client/scripts/standalone-web-smoke.mjs`, `src/frontend/client/src/services/api/BattleModels.ts`, `src/frontend/client/src/services/realtime/SignalRService.ts`, `src/frontend/client/src/game/presentation/GemMatchSummaryBanner.ts` (live source comments enumerating the removed member/event).
- **Added — documentation consumers:** `docs/01-game-design/COMBAT_RULES.md` (§2 POWER bullet, §6 bounds) and `docs/01-game-design/CARD_RULES.md` (§3 item 9, §6) reference `SkillCharge`/`BossSkillCast` as live transitional text and were absent from R1.
- **Not in scope, evidence-based exclusion:** `docs/00-overview/MVP_SCOPE.md` — its only matches are historical version-note text; removing the mechanism changes no MVP IN/OUT/FUTURE classification, so no MVP-scope edit is authorized. Historical migrations under `src/backend/GameServer.Infrastructure/Postgres/Migrations/` are immutable and are deliberately absent. `tasks/artifacts/**` and `tasks/completed/**` are immutable historical records. `ResourceGenerator.CountPowerMatchGroups` tests live in already-declared files.
**Manifest Revision R3 (post-implementation reconciliation, applied before Phase A staging).** Two R2 entries required no edit and are removed from the declared set; one genuine consumer omitted from R2 is added; three declared test files are deleted because their entire subject was removed:
- **Removed — no change required:** `tests/backend/GameServer.Application.Tests/MatchedGemSummaryEmissionTests.cs` — its only remaining matches are prose about what `MatchedGemSummary` does not carry; no assertion referenced the removed member, so the file is unchanged. `tests/backend/GameServer.Api.Tests/TASK309BossCardWireTests.cs` — its only remaining references are negative wire assertions (`DoesNotContain(..., "BossSkillCast")`), which stay valid as regression guards against reintroducing the retired discriminator; no edit was needed.
- **Added — omission evidence:** `tests/backend/GameServer.Application.Tests/Balance/Tests/BalanceEvidenceMeasurementCorrectionTests.cs` consumes `BossSkillCastCount` (line 237) and its `TASK195_PowerSpentTotal` theory asserted that the offline harness produced legacy casts. The M-13 rename plus the removal required it to be retargeted to the canonical `BossCardCast` metric contract. It was absent from R2.
- **Declared deletions:** `tests/backend/GameServer.Domain.Tests/BossSkillContractTests.cs`, `tests/backend/GameServer.Application.Tests/BossSkillSecondaryEffectTests.cs`, and `tests/backend/GameServer.Application.Tests/RootAttackConsumptionTests.cs` are deleted because their entire subject (the legacy Boss Skill contract, its charge-driven secondary-effect application, and its Root consumption) no longer exists; the canonical Boss Card equivalents are mapped in the completion evidence.
- **Phase A rule:** the staged set must equal this R3 metadata list exactly. The task record itself is excluded and is filed by Phase B.


---

## Objective

Execute the physical removal of legacy `SkillCharge`, the obsolete `BossSkill` runtime execution fallback path, legacy secondary effect definitions, and the `BossSkillCast` wire contract and presentation, completing the retirement stage of ADR-029 Slice 5. All five MVP Bosses have been migrated to canonical Boss Cards and verified under Slices 3–4 (TASK-308, TASK-309, TASK-310, TASK-311). This task physically removes dead legacy code while preserving canonical Boss Card mechanics, preserving `SkillCooldown` unchanged, ensuring tolerant-reader compatibility across Redis and PostgreSQL per operator decisions, resolving legacy test and balance telemetry fixture references, and keeping historical migrations immutable.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 (IN), §3 (FUTURE), §4
- `docs/01-game-design/BOSS_RULES.md` §4.1–§4.4, §6, §6.1–§6.4
- `docs/01-game-design/GAME_RULES.md` §12, §15, §16, §17 step 18
- `docs/01-game-design/COMBAT_RULES.md` §1.2, §2 (POWER), §4, §5.1–§5.5
- `docs/01-game-design/CARD_RULES.md` §1.1, §3 item 9, §3.6, §6
- `docs/02-technical/GAME_STATE.md` §2.4, §2.4.3, §5.1.1
- `docs/02-technical/GAME_EVENTS.md` §1, §2 (`BossSkillCast`, `BossCardCast`, `PowerChanged`)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.2, §3.2.18, §3.2.30, §4.4
- `docs/02-technical/DATABASE.md` §1, §3, §5
- `docs/02-technical/REDIS_STATE.md` §2.1
- `docs/03-decisions/ADR/ADR-024-database-driven-boss-balance-configuration.md`
- `docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md`
- `docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md` (§2.9, §4 Slice 5, §7)
- `docs/03-decisions/README.md`
- `tasks/artifacts/TASK-311-adr-029-slice-5-removal-readiness-reconciliation.md`
- `tasks/completed/TASK-311-adr-029-slice-5-removal-readiness-reconciliation.md`

---

## Baseline Reconciliation & Prior Preflight Audit Record

1. **Git Baseline Verification:**
   - Preflight verified commit: `8589f7e8909f743e3461d77494588728da3a766b` (`8589f7e`).
   - Subject: `docs: file the adr-029 slice 5 removal-readiness completion record`.
   - Parent: `0207c4220984e7b3fe06256d3e661bd5262786a0` (`0207c42`).
   - Prior Reported SHA: `8589f7e7f6f600f135b5505db174cb813ee83177` was confirmed nonexistent in repository history.
   - Historical Git commits remain preserved and unmodified.

2. **TASK-312 Preflight Audit Context:**
   - The preflight consumer audit was executed as TASK-312 (out-of-band preflight report).
   - Expanded consumer discovery during TASK-312 identified serialization adapters (`BattleStateJson.cs`, `BattleStateSerializer.cs`) and balance telemetry fixtures (`BalanceControlledExperimentMatrix.cs`, `BalanceBaselineMatrix.cs`, `TASK298ExperimentRunner.cs`, `TASK298ExperimentTests.cs`, `BossCardCastFrequencyAnalysisTests.cs`).
   - TASK-313 is sequentially allocated as the canonical implementation task for ADR-029 Slice 5.

---

## Operator Decisions Status

### OD-311-1 — PostgreSQL JSON Storage Disposition
- **Status:** `RESOLVED = B` (operator-approved under TASK-313).
- **Approved Choice:** Option B. Retain legacy `chargeRequirement` and `baseDamage` properties in persisted JSONB while removing their runtime and Domain responsibility.
- **Approved Requirements:**
  - Deserializer / lookup does not require legacy fields to instantiate Domain definitions.
  - Remove obsolete Domain properties (`BossSkillDefinition.ChargeRequirement`, `BossSkillDefinition.BaseDamage`, `BossDefinition.SkillChargeRequirement`, `BossDefinition.SkillBaseDamage`) without reintroducing legacy execution.
  - Historical migrations remain immutable.
  - Zero database schema migrations added for JSONB cleanup.
  - Existing JSONB rows must still be readable: the persisted document keeps its four member names (`skillId`, `baseDamage`, `chargeRequirement`, `cooldownTurns`) as storage compatibility, and the two legacy members are ignored by the Domain.
  - Documentation distinguishes storage compatibility from runtime/Domain responsibility.

### OD-311-2 — Wire Event Deprecation Window
- **Status:** `RESOLVED = A` (operator-approved under TASK-313).
- **Approved Choice:** Option A. Remove `BossSkillCast` from the wire contract in a coordinated breaking release with active-battle drain.
- **Approved Requirements:**
  - Remove legacy wire projection and DTO (`BattleEventWireProjection.cs`).
  - Remove obsolete wire discriminator (`BossSkillCast`) and schema from `SIGNALR_PROTOCOL.md` §3.2.18.
  - Remove frontend handling of `BossSkillCast` (`BattleScene.ts`, `BattleEventPresenter.ts`).
  - Active-battle drain procedure documented as a deployment prerequisite (no claim of an executed drain or deployment).
  - Canonical `BossCardCast` contract, event emission, and animation presentation preserved.

### Implementation Authorization
- **Status:** `GRANTED` under TASK-313 by the operator. Implementation is authorized subject to all mandatory repository lifecycle, scope, verification, and commit gates. Fifth Gem work, unrelated balance changes, unrelated refactoring, history rewriting, and remote pushes remain unauthorized.


---

## Scope

### In Scope

- **Backend Domain Layer:**
  - Remove `SkillCharge` record member from `BossState.cs` (constructor, `ToString`, equality, initial factory).
  - Retire `SkillChargeRequirement` / `SkillBaseDamage` projections from `BossDefinition.cs`.
  - Remove the legacy `BossSkillSecondaryEffect` / `BossSkillSecondaryEffectKind` declarations and the `BossSkillDefinition.BaseDamage` / `ChargeRequirement` runtime members from `BossDefinition.cs` (OD-311-1 = B: the persisted JSONB document keeps those member names; the Domain stops carrying them).
  - Retire `BattleEventType.BossSkillCast = 9`, `BossSkillCastEvent` struct, `ForBossSkillCast` factory, and `BossSkillCast` property from `BattleEvent.cs`.
  - Remove `ApplyBossSkillCharge` / `CountPowerMatchGroups` from `ResourceGenerator.cs`; Boss match effects accumulate `BossState.Power` only.
  - Update `BattleStateJson.cs` and `BattleStateSerializer.cs` to remove the `skillCharge` mapping while tolerating legacy snapshots that still contain it.
- **Backend Application Layer:**
  - Remove the Step 18a `ApplyBossSkillCharge` write-back and the legacy `var skillFires = bossCard is null && ...` fallback branch (including the legacy secondary-effect application) in `BattleStateService.cs`.
  - Remove `BattleEvent.ForBossSkillCast` emissions and `SkillCharge = 0` state mutations.
- **Backend Infrastructure & Persistence:**
  - Align `BossDefinitionConfiguration.cs` with OD-311-1 (Option B: retain legacy properties in the DB JSONB document as ignored fields, tolerating their presence and never reintroducing them as runtime authority).
  - Preserve all historical migrations without back-editing.
  - Verify Redis tolerant-reader deserialization of existing battle snapshots through `BattleStateSerializer` (verification obligation; the repository file itself needs no edit).
- **Backend API & Realtime Wire:**
  - Remove the `BossSkillCast` mapping, DTO factory, and discriminator prose from `BattleEventWireProjection.cs` per OD-311-2.
  - Remove `BossSkillCast` from `SIGNALR_PROTOCOL.md` §3.2.2 and retire §3.2.18.
- **Frontend Client:**
  - Remove `BossSkillCast` handling in `BattleScene.ts` and `BattleEventPresenter.ts`, plus the smoke-script callout list.
  - Ensure client renders only canonical `BossCardCast` events.
- **Tests & Balance Telemetry:**
  - Update backend unit, integration, serialization, persistence, and wire-contract tests that reference the removed members/events.
  - Resolve the 4 pre-existing infrastructure test failures (`BossPersistenceTests`, `BossDefinitionBalanceLookupTests`, `BossDefinitionPostgresProvisioningTests`) by retargeting their obsolete legacy secondary-effect / historical `baseDamage` assertions to the retained-storage-only contract.
  - Repurpose M-13's cast-count telemetry from the removed `BossSkillCast` to the canonical `BossCardCast` (`MetricAccumulator`, `BalanceSimulationMetrics`, and the balance matrix/runner projections), preserving the documented M-13 cadence measurement.
  - Update frontend test suites (`SceneLifecycle.test.ts`, `GameRuntime.test.ts`, `BattleEventPresentation.test.ts`, `RuntimeBoundaries.test.ts`).
- **Documentation Synchronization:**
  - Update `BOSS_RULES.md`, `GAME_RULES.md`, `COMBAT_RULES.md`, `CARD_RULES.md`, `GAME_STATE.md`, `GAME_EVENTS.md`, `SIGNALR_PROTOCOL.md`, `REDIS_STATE.md`, `DATABASE.md`, `ADR-029`, and `docs/03-decisions/README.md`.

### Out of Scope

- Do NOT implement Slice 5 during task registration.
- Do NOT alter canonical `BossCardCast` wire schema, event emission, or client presentation.
- Do NOT alter `SkillCooldown` (retained unchanged per ADR-029 §2.9).
- Do NOT change Boss balance values, power costs, action priorities (`CastPriorityScore = 85`), or simulation baselines.
- Do NOT author database migrations modifying historical tables or columns unless separately authorized.
- The Fifth Gem / Power-drain Gem remains strictly FUTURE and OUT OF SCOPE (`MVP_SCOPE.md` §3).

---

## Current State

All five MVP Boss abilities (`earthquake`, `flame-burst`, `drain-power`, `root`, `thunder-strike`) execute as canonical Boss Cards at runtime. The legacy `SkillCharge` accumulator and `BossSkill` evaluation path remain in the codebase as inert fallback paths. Documentation prerequisites and the four-part parity gate under ADR-029 §2.9 were reconciled in TASK-311.

---

## Declared File Set (P-2 / P-5)

The canonical declared implementation file set is the `Declared Files:` field in `## Metadata` above (manifest revision **R3**, corrected by the Phase 0 inventory audit and reconciled against the implemented change set before Phase A staging). It is not restated here so the two copies cannot drift.

*(Note: the task record itself is excluded from the implementation declared file set and is filed by the Phase B commit. `tasks/artifacts/**`, `tasks/completed/**`, and `src/backend/GameServer.Infrastructure/Postgres/Migrations/**` are immutable and deliberately absent.)*

---

## Acceptance Criteria

- [x] 1. `BossState.SkillCharge`, `ApplyBossSkillCharge`, and legacy `BossSkillDefinition` secondary effect declarations are completely removed from `GameServer.Domain`.
- [x] 2. `SkillCooldown` is preserved unchanged as an active integer member on `BossState`.
- [x] 3. `BattleEvent.ForBossSkillCast` and `BattleEventType.BossSkillCast` are retired; canonical `BossCardCast` remains the sole Boss ability event.
- [x] 4. `BattleStateJson.cs` and `BattleStateSerializer.cs` serialize and deserialize `BossState` without `SkillCharge`, and a legacy snapshot carrying `skillCharge` still deserializes with the value discarded.
- [x] 5. `BattleStateService.cs` contains zero references to legacy `SkillCharge` or the obsolete fallback branch; all Boss ability executions route through `BossActionPolicy.Select` and `CardCastExecutor`.
- [x] 6. PostgreSQL JSON storage conforms to OD-311-1 (Option B: persisted JSONB retains legacy fields as ignored, without requiring database schema rewrites).
- [x] 7. Wire contracts and client event presenters conform to OD-311-2 (Option A: `BossSkillCast` retired from `BattleEventWireProjection.cs`, `SIGNALR_PROTOCOL.md`, `BattleScene.ts`, and `BattleEventPresenter.ts`).
- [x] 8. Historical EF Core migrations remain immutable in git history.
- [x] 9. Redis tolerant deserialization functions without errors for existing battle snapshots.
- [x] 10. Balance telemetry matrixes and runners (`BalanceControlledExperimentMatrix`, `BalanceBaselineMatrix`, `TASK298ExperimentRunner`, `TASK298ExperimentTests`, `BossCardCastFrequencyAnalysisTests`) compile and execute cleanly without legacy skill charge properties; M-13 now sources `BossCardCastCount` from the canonical event.
- [x] 11. Pre-existing infrastructure test failures (`BossPersistenceTests`, `BossDefinitionBalanceLookupTests`, `BossDefinitionPostgresProvisioningTests`) are resolved by retiring obsolete secondary effect expectations.
- [ ] 12. All backend tests pass (`dotnet test src/backend/GameServer.sln`). **NOT FULLY MET:** 3354 passed / 2 failed / 0 skipped; the 2 failures are the pre-existing `TASK195_PerTurnSeries_*` and `TASK296_ReviewD_*` balance-evidence methods, unchanged by this task and requiring an operator-approved re-baseline (see Assessment).
- [x] 13. All frontend tests pass (`npm run test:run`) and client builds cleanly (`npm run build`).
- [x] 14. Documentation files (`BOSS_RULES.md`, `GAME_RULES.md`, `COMBAT_RULES.md`, `CARD_RULES.md`, `GAME_STATE.md`, `GAME_EVENTS.md`, `SIGNALR_PROTOCOL.md`, `REDIS_STATE.md`, `DATABASE.md`, `ADR-029`, `docs/03-decisions/README.md`) are updated to reflect the completed retirement.
- [x] 15. Server authority and MVP scope constraints are strictly preserved; the Fifth Gem remains FUTURE.
- [x] 16. `git diff --check` passes with zero errors.

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain / Application / Infrastructure / Api)
[x] src/frontend/client/ (scenes / runtime / services / state / ui)
[x] tests/ (unit / integration / gameplay scenarios / balance telemetry)
[x] docs/ (documentation updates for completed retirement)
```

---

## Implementation Notes

- **Domain Model:** In `BossState.cs`, remove positional member `int SkillCharge` from the constructor, the equality/hash overrides, and `BossState.Initial`. `Power` stays positional member #10 and `SkillCooldown` becomes #11 — the invariants are unchanged, only the removed member is gone.
- **Domain Definitions:** `BossSkillDefinition` reduces to `(string SkillId, int CooldownTurns)`; `BossDefinition` drops the `SkillBaseDamage` and `SkillChargeRequirement` projections and its `BossSkillSecondaryEffect`/`BossSkillSecondaryEffectKind` declarations. `SkillId` and `SkillCooldownTurns` stay — the former resolves the provisioned Boss Card, the latter sets the cast cooldown.
- **Serialization Mapping:** In `BattleStateJson.cs` and `BattleStateSerializer.cs`, remove the `BossSkillCharge`/`skillCharge` DTO member and both mappings. Legacy Redis snapshots still containing `skillCharge` must deserialize (unknown members are ignored by the configured `JsonSerializerOptions`), and a lifecycle test must prove it.
- **Board Resolution:** In `ResourceGenerator.cs`, delete both `ApplyBossSkillCharge` overloads and `CountPowerMatchGroups`, and drop the charge-requirement parameter from `ApplyActorMatchEffects`.
- **Response Phase:** In `BattleStateService.cs`, delete the `skillFires` gate and its whole block (legacy announcement plus the five legacy secondary-effect applications), promote `else if (bossCastSelected)` to `if (bossCastSelected)`, and remove the charge-requirement argument at the step-18a call site. The canonical 18c Boss Card branch is preserved verbatim apart from surrounding prose.
- **Persistence (OD-311-1 = B):** `BossDefinitionJson.SkillDocument` keeps its four documented members as the storage contract. `ReadSkill` ignores `baseDamage`/`chargeRequirement`; `WriteSkill` emits the retained members as compatibility placeholders (`0`), because provisioning is migration-only and the application has no boss-definition write path. No migration is added or edited.
- **Wire Projection:** In `BattleEventWireProjection.cs`, delete `ProjectBossSkillCast` and `BattleEventWireDto.BossSkillCast` plus the `BattleEventType.BossSkillCast` arm of the switch.
- **Client Presentation:** In `BattleScene.ts` and `BattleEventPresenter.ts`, delete the `BossSkillCast` case, parser, interface, ordering/actor/display-name branches, and the Boss-band callout method.
- **Telemetry:** M-13's cast count is renamed `BossCardCastCount` and sourced from `BattleEventType.BossCardCast`; `bossPassiveTriggerCount`/`bossEnrageTurn` are unchanged.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — Domain BossStateTests, Serialization, ResourceGenerator, CardCastExecutor
[x] Integration tests  — AtomicRoundOrchestrationTests, BossResponseTests, BossCardWireTests
[x] Gameplay scenarios — 5-Boss ability execution, status effect lifecycles, action suppression
[x] Balance Telemetry  — Simulation runners and matrix consistency tests
[x] Frontend Tests     — SceneLifecycle, GameRuntime, BattleEventPresentation vitest suites
```

### Key Edge Cases
- See `tasks/artifacts/TASK-311-adr-029-slice-5-removal-readiness-reconciliation.md` §2 for per-ability comparisons:
  - `earthquake`: Shield absorption lifecycle and same-round attack suppression.
  - `flame-burst`: 3-turn buff duration and attack window preservation.
  - `drain-power`: Power deduction without Boss gain; `PowerChanged` ordering before `BossCardCast`.
  - `root`: 3-turn debuff duration and attack consumption timing.
  - `thunder-strike`: `NextAtkBuff` survival across non-qualifying rounds.

---

## Stop Conditions

- **Decision Gate:** `SATISFIED` — OD-311-1 = B and OD-311-2 = A were operator-approved and recorded in ADR-029 and in this record.
- **Authorization Gate:** `SATISFIED` — implementation was explicitly authorized by the operator under TASK-313.
- **Scope Violation Stop Condition (still live):** If implementation touches the Fifth Gem / Power-drain Gem, STOP immediately (`MVP_SCOPE.md` §3).
- **Rule Violation Stop Condition (still live):** If implementation attempts to alter `SkillCooldown`, `BossState.Power`, `BossCardCast`, or the one-action/cast-priority policy, STOP immediately (ADR-029 §2.7, §2.9).
- **Release Gate (still live):** The wire contract is breaking. Do not claim a deployment, an executed active-battle drain, or production readiness without evidence.

---

## Completion Evidence

### Commit
- Implementation slice (Phase A): `a5e90a399d2c2cc43eda138b999fbbe24d13e591`
- Parent: `8589f7e8909f743e3461d77494588728da3a766b`
- Subject: `refactor: retire the legacy boss skill charge and skillcast contract` (carries no task ID — P-4)
- Owns: `TASK-313`
- Files: the declared file set at manifest revision **R3** — 80 paths (77 modified, 3 deleted), verified equal to `git diff --cached --name-only` before the commit. No database migration path is included.
- Shared with: `none`
- Unowned / pre-existing: `none` (`.ai/PROJECT_STATE.md`'s pre-implementation TASK-313 registration delta is folded into this record's own declared update of that file)
- Deleted (subject removed): `tests/backend/GameServer.Domain.Tests/BossSkillContractTests.cs`, `tests/backend/GameServer.Application.Tests/BossSkillSecondaryEffectTests.cs`, `tests/backend/GameServer.Application.Tests/RootAttackConsumptionTests.cs`

### Changed Files
- **Domain:** `BossState` loses the `SkillCharge` member; `BossDefinition`/`BossDefinitions` lose the `BossSkillSecondaryEffect(Kind)` declarations and the `SkillBaseDamage`/`SkillChargeRequirement` projections and reduce `BossSkillDefinition` to `skillId` + `cooldownTurns`; `BattleEvent` loses `BossSkillCast` (value 9 retired, not reused) with its payload/factory/property; `ResourceGenerator` loses `ApplyBossSkillCharge`/`CountPowerMatchGroups` and the charge-requirement parameter; the battle-state JSON DTO/serializer lose the `skillCharge` mapping; one `CardCastExecutor` doc comment de-staled.
- **Application:** `BattleStateService` loses the `skillFires` gate and the whole legacy `bossCard is null` execution/secondary-effect branch; the canonical 18c `BossCardCast` branch is preserved verbatim apart from surrounding prose.
- **Infrastructure:** `BossDefinitionConfiguration` keeps the four-member persisted `SkillDefinition` document (OD-311-1 = B) but ignores `baseDamage`/`chargeRequirement` on read and writes them as documented 0 placeholders; no migration touched.
- **Api:** `BattleEventWireProjection` loses the `BossSkillCast` factory, projector, switch arm and `skillId` DTO member; `BattleHub` doc comment de-staled.
- **Frontend:** `BattleEventPresenter`/`BattleScene` lose `BossSkillCast` parsing, formatting, ordering, actor attribution, display-name resolution and presentation; `BossCardCast` untouched; smoke-script callout lists and three stale `SkillCharge` doc comments updated; frontend tests retargeted.
- **Tests:** backend suite retargeted to the canonical contract (Domain/Application/Infrastructure/Api), M-13 renamed to `BossCardCastCount` sourced from `BossCardCast`, 6 tests added (legacy `skillCharge` snapshot tolerance, retained-storage `SkillDefinition` read tolerance, canonical Boss Card cooldown blocking, M-13 metric contract, and retargeted live-intent cases).
- **Documentation:** ADR-029 + ADR index (Lead), and `BOSS_RULES` 2.18, `GAME_RULES` 3.12, `COMBAT_RULES` 3.3, `CARD_RULES` 1.13, `GAME_STATE` 2.35, `GAME_EVENTS` 2.21, `SIGNALR_PROTOCOL` 2.30 (§3.2.18 retired in place, no renumbering), `REDIS_STATE` 1.18, `DATABASE` 1.43, `.ai/PROJECT_STATE` 1.8.

### Validation Results
- `dotnet build src/backend/GameServer.sln` — **PASS**, 0 errors (63 pre-existing warnings on a clean build; 2 incremental; none from a changed line).
- `dotnet test src/backend/GameServer.sln` — **3356 total, 3354 passed, 2 failed, 0 skipped**: Domain 1674/1674, Infrastructure 507/507, Api 399/399, Application 774/776.
- PostgreSQL-backed Infrastructure suites executed against the live provisioned schema (not skipped): container up on :5433, schema applied, 5 `BossDefinition` rows, `pg_stat_database` deltas confirm real query activity.
- `npm run test:run` (`src/frontend/client`) — **PASS**: 24 files, 1267 tests.
- `npm run build` (`src/frontend/client`) — **PASS** (`tsc && vite build`, exit 0; only pre-existing chunk-size/PURE warnings).
- `git diff --check` — **PASS** (exit 0).
- Repository-wide symbol sweep: no live runtime, wire, or client reference to `BossSkillCast`, `ForBossSkillCast`, `BossSkillCastEvent`, `PresentedBossSkillCast`, `SkillCharge` or `ApplyBossSkillCharge` remains; the only surviving mentions are deliberate retirement notes, storage-compatibility statements, negative wire assertions kept as regression guards, immutable historical task/artifact records, and immutable migration literals.

### Assessment (pre-existing vs new; unverified)
- **New regressions: none.** One failure caused by this removal (`TASK195_PowerSpentTotal` legacy-cast expectation) was retargeted to a canonical `MetricAccumulator` contract test and now passes.
- **Pre-existing, unchanged:** `BalanceEvidenceMeasurementCorrectionTests.TASK195_PerTurnSeries_ReconcilesTerminalCardCastWithoutPhantomTurn` and `TASK296_ReviewD_Canonical75RunCandidate_Verification`. Both stem from the offline balance harness containing no provisioned Boss Card content; resolving them requires an operator-approved decision to provision Boss Cards in the harness and re-baseline the TASK-194/TASK-296 evidence, or to accept the shifted baseline. Adding harness content changes simulated balance and was deliberately not done here.
- **Consequence to record:** the offline balance harness can no longer measure Boss ability cadence at all (M-13 is structurally 0 there) until that content decision is made; runtime `BossCardCast` telemetry and the TASK-309 cast-frequency harness are unaffected.
- **Release requirement (UNVERIFIED / not executed):** the wire contract is breaking. Active-battle drain and coordinated client/server deployment are recorded as pending operational steps; no drain, deployment, or push occurred.
- Residual pre-existing stale prose remains in a few illustrative doc examples (`BOSS_RULES` §6.2.1, `COMBAT_RULES` §3.4/§5.5.2/§5.5.3/§5.3.3, `GAME_STATE` §5.1.1) — flagged for a small follow-up documentation task rather than expanded into this slice.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (the client diff only removes presentation of a retired event and stale doc comments)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — the removal changes no IN/OUT/FUTURE classification; the Fifth Gem remains FUTURE
- [x] Confirmed P-1…P-6 commit policy compliance (single `master`, one Phase A commit, staged set equals the declared R3 set, task ID only in the body, no amend/rebase/force-push, no push)
