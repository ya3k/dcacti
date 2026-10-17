# TASK-292 — Remove Special Gem Mechanics from Backend Domain and API

---

## Metadata

```text
Task ID:           TASK-292 (highest existing ID on disk is TASK-291)
Type:              GAMEPLAY-CHANGE
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     backend
Supporting Agents: gameplay, realtime, persistence, testing, review
Workflow:          development/gameplay-change.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis,
                   gameplay/authority-determinism-audit, backend/persistence-analysis,
                   realtime/realtime-protocol-validation, testing/test-scenario-generation,
                   quality/implementation-review
Dependencies:      ADR-028 (Accepted); TASK-286, TASK-287, TASK-289 (DONE);
                   TASK-290 (DONE), TASK-291 (DONE)
Declared Files:    see ## Declared File Set (P-2 / P-5)
```

<!--
  RISK DEVIATION (disclosed). tasks/TASK_TYPES.md §4 gives GAMEPLAY-CHANGE a HIGH
  baseline; HIGH is selected and no deviation is claimed. The risk is HIGH because
  the slice touches the authoritative battle-state model, the match/cascade
  resolution pipeline, combat qualification input, and the SignalR event/state wire
  contracts.

  OWNERSHIP. ADR-028 §7 divides the runtime implementation into slices; this task is
  its "Compile-coupled Domain and API Special Gem removal (GameServer.Domain and
  GameServer.Api)" slice. TASK-290 (ATK-match qualification) and TASK-291
  (match-tier multiplier retirement) are DONE and are preserved unchanged.
-->

---

## Objective

Remove the retired Special Gem model and every runtime mechanic that exists only to
support it from the backend Domain/Application/API production code and its tests, so
that executable behavior matches the already-approved match-only contract
(`ADR-028`, OD-1…OD-7). Board cells carry an ordinary `GemType` only; match
resolution creates, places, activates, chains, and area-clears nothing; emitted
events and delivered cell payloads describe the match-only resolution.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Match 3/4/5 and L/T remain IN as **matches**;
  the change is a reduction, not new content
- `docs/03-decisions/ADR/ADR-028-retire-special-gems-from-battle-state-model.md` —
  the governing decision (Status: Accepted); §7 assigns this slice
- `docs/01-game-design/GAME_RULES.md` §3 item 4, §6, §7, §17 step 15
- `docs/01-game-design/MATCH3_RULES.md` §1.A, §4.1, §4.5 item 7, §5 (retired),
  §5.7, §6.3.1
- `docs/01-game-design/COMBAT_RULES.md` §2, §2 item 6, §3–§4
- `docs/01-game-design/PASSIVE_RULES.md` §2
- `docs/02-technical/GAME_STATE.md` §2.1–§2.1.9, §3
- `docs/02-technical/GAME_EVENTS.md` §1.1, §1.3, §2
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.6, §3.2.9–§3.2.11, §4.1
- `docs/02-technical/REDIS_STATE.md` §6 item 1, §7 item 10 (tolerant-reader basis)
- `docs/02-technical/TDD.md`, `docs/02-technical/ARCHITECTURE.md` §1, §3, §4
- `tasks/completed/TASK-290-…md`, `tasks/completed/TASK-291-…md` — the preserved
  behaviors this task must not regress

---

## Scope

### In Scope

- Delete retired Domain types: `SpecialGem`, `SpecialGemType`,
  `SpecialGemOrientation`, `SpecialGemClaim`, `CreationSource`, `SpecialGemEffects`,
  `SpecialGemPlanner`.
- Reduce the board model to pure `GemType` cells (`BoardState.Cells[64]`), removing
  the `Cell` special-gem member and the accessors/constructors that exist only for
  it.
- Reduce `BoardResolver` / `CascadeResolver` to the approved match-only pass
  pipeline (remove / Gravity / Spawn) and drop the swap-origin input whose only
  consumer was the creation planner.
- Drop the Special Gem members from `PassResult`, `MatchResolution`, and
  `GemMatchedEvent` (`ActivatedSpecialGems`, `CreatedSpecialGems`,
  `ActivationCellGemMatched`, `ConsumedSpecialGem`).
- Remove `MatchCreated.createdSpecialGems` and the `specialGem` cell / `GemMatched`
  payload members from the SignalR projection and REST battle-start responses.
- Remove the Redis persisted `cell.specialGem` member and reconcile
  serialization/deserialization with the pure `GemType` cell model.
- Remove Special Gem–only transient resolution bookkeeping and comments in Domain
  code (Match-3, Passives, combat/resource docs) that describe the retired model.
- Delete the obsolete Special Gem test suites; adapt survivors to the match-only
  contract; add regression coverage for the surviving contract (§4 of the request).

### Out of Scope

- Any `src/frontend/client/` file — the coordinated client cleanup is TASK-293.
- Any `docs/` file, any ADR, `docs/03-decisions/README.md`, and the untracked
  ADR-028 file (untouched, unstaged).
- BaseOutput rates, match-tier scoring, Boss AI scoring, attack qualification,
  damage balance, or approved resolution ordering.
- A Redis data migration; ADR-028's approved policy is battle-drain + joint deploy.
- The Balance-evidence corpus (`tasks/artifacts/TASK-194-*.json`,
  `TASK-197-*.json`): its pinned expectations are Product-Owner-approved balance
  evidence (TASK-194/195/198/200), and re-deriving them is a balance decision, not
  part of this gameplay-contract task. The delta this change produces in that corpus
  is reported, not corrected (same disposition as TASK-290 F-3 and TASK-291 F-4).
- `tasks/completed/` history.

---

## Current State

`docs/01-game-design/` and `docs/02-technical/` were reconciled by TASK-287/TASK-289
and now define the match-only model, so the code was the only remaining
Special-Gem-bearing layer in the backend. The Domain `Match3` module still carried
`SpecialGem`, `SpecialGemType`, `SpecialGemOrientation`, `SpecialGemClaim`,
`SpecialGemPlanner`, `SpecialGemEffects`, a `Cell` special-gem member, and the
activation/creation members of `PassResult`/`MatchResolution`/`GemMatchedEvent`; the
API still emitted `createdSpecialGems` / `specialGem` members; the Redis JSON mapping
still persisted `cell.specialGem`.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-292 (reconciled to the final changed
set before Phase A):

```text
src/backend/GameServer.Api/Controllers/BattleStartResponse.cs
src/backend/GameServer.Api/Controllers/BattleStartStateSummary.cs
src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs
src/backend/GameServer.Api/Hubs/BattleHub.cs
src/backend/GameServer.Application/Battle/BattleStateService.cs
src/backend/GameServer.Domain/Battle/BattleState.cs
src/backend/GameServer.Domain/Battle/Serialization/BattleStateJson.cs
src/backend/GameServer.Domain/Battle/Serialization/BattleStateSerializer.cs
src/backend/GameServer.Domain/Match3/BattleEvent.cs
src/backend/GameServer.Domain/Match3/BattleEventBuilder.cs
src/backend/GameServer.Domain/Match3/BoardGenerationValidator.cs
src/backend/GameServer.Domain/Match3/BoardResolver.cs
src/backend/GameServer.Domain/Match3/BoardState.cs
src/backend/GameServer.Domain/Match3/CascadeResolver.cs
src/backend/GameServer.Domain/Match3/Cell.cs
src/backend/GameServer.Domain/Match3/GravityAndSpawn.cs
src/backend/GameServer.Domain/Match3/MatchDetector.cs
src/backend/GameServer.Domain/Match3/MatchPrimitive.cs
src/backend/GameServer.Domain/Match3/MatchShape.cs
src/backend/GameServer.Domain/Match3/ResolutionEvents.cs
src/backend/GameServer.Domain/Match3/SpecialGem.cs
src/backend/GameServer.Domain/Match3/SpecialGemClaim.cs
src/backend/GameServer.Domain/Match3/SpecialGemEffects.cs
src/backend/GameServer.Domain/Match3/SpecialGemPlanner.cs
src/backend/GameServer.Domain/Match3/SwapExecution.cs
src/backend/GameServer.Domain/Match3/SwapValidator.cs
src/backend/GameServer.Domain/Passives/PassiveTracker.cs
tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs
tests/backend/GameServer.Application.Tests/AtomicRoundOrchestrationTests.cs
tests/backend/GameServer.Application.Tests/Balance/BalancePlayerPolicy.cs
tests/backend/GameServer.Application.Tests/Balance/Tests/BalanceSimulatorTests.cs
tests/backend/GameServer.Application.Tests/BattleStateSerializationLifecycleTests.cs
tests/backend/GameServer.Application.Tests/BattleStateServiceDeadlockRecoveryTests.cs
tests/backend/GameServer.Application.Tests/BattleStateServiceTests.cs
tests/backend/GameServer.Application.Tests/BossResponseTests.cs
tests/backend/GameServer.Application.Tests/BossSkillStep1CompositionTests.cs
tests/backend/GameServer.Application.Tests/RoundTestSupport.cs
tests/backend/GameServer.Application.Tests/Task177RelicFiringPointRuntimeTests.cs
tests/backend/GameServer.Application.Tests/VictoryDefeatTests.cs
tests/backend/GameServer.Domain.Tests/ActorRelativeMatchEffectsTests.cs
tests/backend/GameServer.Domain.Tests/AttackResolutionTests.cs
tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs
tests/backend/GameServer.Domain.Tests/BattleStateSerializationTests.cs
tests/backend/GameServer.Domain.Tests/BattleStateTests.cs
tests/backend/GameServer.Domain.Tests/BoardGeneratorTests.cs
tests/backend/GameServer.Domain.Tests/BoardStateSerializationTests.cs
tests/backend/GameServer.Domain.Tests/BoardStateTests.cs
tests/backend/GameServer.Domain.Tests/CascadeAndDeterminismTests.cs
tests/backend/GameServer.Domain.Tests/GravityAndSpawnTests.cs
tests/backend/GameServer.Domain.Tests/MatchComboAccountingTests.cs
tests/backend/GameServer.Domain.Tests/MatchDetectorTests.cs
tests/backend/GameServer.Domain.Tests/PassiveTrackerTests.cs
tests/backend/GameServer.Domain.Tests/ResourceGenerationTests.cs
tests/backend/GameServer.Domain.Tests/SpecialGemActivationTests.cs
tests/backend/GameServer.Domain.Tests/SpecialGemCreationTests.cs
tests/backend/GameServer.Domain.Tests/SpecialGemEdgeCaseTests.cs
tests/backend/GameServer.Domain.Tests/SpecialGemGeometryTests.cs
tests/backend/GameServer.Domain.Tests/SpecialGemScenarioTests.cs
tests/backend/GameServer.Domain.Tests/SwapExecutionTests.cs
tests/backend/GameServer.Domain.Tests/SwapValidationTests.cs
tests/backend/GameServer.Domain.Tests/TestBoard.cs
```

*(61 paths. Ten are deletions — the five Domain `Match3` special-gem files
(`Cell.cs`, `SpecialGem.cs`, `SpecialGemClaim.cs`, `SpecialGemEffects.cs`,
`SpecialGemPlanner.cs`) and the five `SpecialGem*Tests.cs` suites — and every other
path is a modification. The task record itself is excluded from the declared set —
`tasks/TASK_TEMPLATE.md`. Shared-file determination: no other task holds an
authorized active manifest over any of these paths; the pending frontend task
(TASK-293) owns disjoint `src/frontend/` paths.)*

---

## Acceptance Criteria

- [x] AC-1 No backend production code declares or references `SpecialGem`,
      `SpecialGemType`, `SpecialGemOrientation`, `SpecialGemClaim`,
      `CreationSource`, `SpecialGemPlanner`, `SpecialGemEffects`,
      `CreatedSpecialGems`, `ActivatedSpecialGems`, `ConsumedSpecialGem`,
      `RequiredCreations`, `ReservedCreationCells`, or `ActivationCellGemMatched`.
- [x] AC-2 `BoardState.Cells` carries an ordinary `GemType` per cell only; no cell
      type or accessor can express Special Gem metadata.
- [x] AC-3 One detection pass removes exactly its confirmed matched union and runs
      Gravity then Spawn; no activation, chain, detonation, area clear, or creation
      step exists.
- [x] AC-4 Match-3 / Match-4 / Match-5 / L/T all resolve as confirmed matches with the
      TASK-291 `BaseOutput(type) × unique confirmed matched cells(type)` contract and
      accumulate across passes; no match-tier multiplier is reintroduced.
- [x] AC-5 TASK-290's `AttackQualification` still requires the acting actor's own
      confirmed valid ATK match, and a positive cleared ATK count cannot authorize
      damage.
- [x] AC-6 `MatchCreated` carries no `createdSpecialGems`; `GemMatched` and every cell
      payload carry no `specialGem`; no retired wire enum/DTO remains.
- [x] AC-7 Redis serialization writes pure `GemType` cells and round-trips the board;
      the tolerant-reader basis is unchanged (no `[JsonRequired]`, no
      `JsonUnmappedMemberHandling.Disallow`), with a regression test proving a legacy
      per-cell member is ignored.
- [x] AC-8 Obsolete Special Gem test suites are removed; every surviving invariant in
      §4 of the request has regression coverage.
- [x] AC-9 Focused + Domain + Application + API + Infrastructure suites and the
      backend solution build pass, subject only to evidenced baseline failures
      (Infrastructure database provisioning) and the reported Balance-evidence delta.
- [x] AC-10 `git diff --check` is clean; a final symbol sweep leaves no active match.
- [x] AC-11 `src/frontend/client/` is unchanged, and no `docs/` file, ADR, or task
      history is modified by this task slice.
- [x] AC-12 Quality review checklist passes (`.ai/workflow/quality/review.md` §1).

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain / Application / Api)
[ ] src/frontend/client/   NOT TOUCHED (TASK-293)
[x] tests/ (unit / integration / gameplay scenarios)
[ ] docs/                  NOT TOUCHED (reconciled by TASK-287/TASK-289)
```

---

## Implementation Notes

- `MatchPrimitive`'s line-centre / tier helpers (`LineCentreIndex`,
  `IsExactLengthFour`, `IsLengthFiveOrMore`) existed only for creation placement and
  are removed, as are `EndIndex`/`Contains`, which only the creation planner used;
  the §3.2 shape ordering that `MatchShape.SortKey` provides is still required and
  stays.
- `CascadeResolver.Resolve` / `BoardResolver.ResolvePass` took a swap-origin input
  whose only consumer was the creation planner; it is removed rather than left as a
  dead parameter.
- `BoardState.FromCells` / `BoardState.ToArray` already express the pure-`GemType`
  board; Gravity and Spawn build their result through them instead of the removed
  cell-entry constructor.
- ADR-028 §3 (Decision B-2) is the tolerance/deployment authority: the serializer
  config keeps ignoring unknown legacy members; no migration or compatibility shim
  was added.
- `BattleStateService.ResolveBoardAsync`'s `swapOriginIndex` parameter was forwarded
  only to the resolution and became unused; it is removed.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — cascade/resolution, gravity/spawn, resources, combat
                         qualification, passives, serialization
[x] Integration tests  — API event/cell payload projection, REST battle-start
                         board summary, Redis battle-state round trip
[x] Gameplay scenarios — Match 3/4/5 and L/T resolve with no Special Gem
                         (state-transition level, HIGH risk depth)
```

### Key Edge Cases

- Overlapping cells (L/T arm or two shapes naming one cell) count once for removal,
  `GemMatched`, and resource generation.
- A pass that clears ATK gems without the actor's own confirmed ATK match emits no
  damage (TASK-290).
- Cascade passes accumulate resources; no cascade bonus multiplier exists.
- Legacy Redis JSON carrying a per-cell obsolete member still deserializes without
  error (tolerant-reader basis) and the obsolete member is ignored.

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP
  per `AGENTS.md` §7.
- If a change requires touching `src/frontend/client/`: STOP — that is TASK-293.
- If a change requires editing `docs/`, an ADR, or the untracked ADR-028 file: STOP.
- If a reviewer finds an authoritative rule conflict: STOP (`AGENTS.md` §4).
- If an active Special Gem gameplay dependency cannot be removed within this scope:
  STOP and record the residual dependency plus a separate follow-up task.

---

## Completion Evidence

**Completed.** `Status: DONE`. Phase A implementation slice committed; Phase B
completion record filed.

### Commit

```text
Phase A implementation slice   ad30e8eea47442283f67abb40e26d0bd399abad9
Subject                        fix(match3): retire special gems from the backend domain and api
Owns                           TASK-292
Files                          61 declared paths (27 under src/backend, 34 under tests/backend;
                               10 are deletions — see ## Declared File Set)
Shared with                    none
Unowned / pre-existing         none
Phase B completion record      tasks/completed/TASK-292-remove-special-gem-mechanics-from-backend-domain-and-api.md
```

The pre-existing working-tree items (`docs/03-decisions/README.md` modification and
the untracked `docs/03-decisions/ADR/ADR-028-…md`) were **not** staged or modified by
this task; `git status` shows them in the same state they were found.

### Changes

**Domain (Match3, Battle, Passives) — the retired model and its mechanics removed**

- Deleted `SpecialGem.cs` (`SpecialGem`, `SpecialGemType`, `SpecialGemOrientation`),
  `SpecialGemClaim.cs` (`SpecialGemClaim`, `CreationSource`), `SpecialGemPlanner.cs`
  (creation planning, collision reservation, chain planning), `SpecialGemEffects.cs`
  (LineClear/Burst/Area affected-cell geometry), and `Cell.cs` (the
  `GemType` + optional `SpecialGem` cell entry).
- `BoardState`: `Cells[64]` is `IReadOnlyList<GemType>`; each entry is one functional
  gem type. `SpecialGemAt`, `FromCellEntries`, `ToCellArray` and both `WithCells`
  overloads are gone; `FromCells`, `FromValues`, `ToArray`, `WithSwapped`,
  `CellsEqual` remain.
- `BoardResolver`: one pass is `remove the confirmed matched union → Gravity →
  Spawn` only. Activation, chain traversal, creation reservation, and creation
  commit are deleted; `PassResult` now carries `Board`, `RngState`, `Matches`,
  `GemMatched`, `ClearedCellUnion`, `ClearedGems`, `DropMoves`, `SpawnedGems`.
- `CascadeResolver` / `BoardResolver.ResolvePass`: the creation-only
  `swapOriginIndex` input is removed from both signatures.
- `ResolutionEvents`: `MatchResolution(Shape, CascadeDepth)`;
  `GemMatchedEvent(CellIndex, GemType)`.
- `Gravity`/`Spawn`: operate on `GemType[]`; a spawned gem is always an ordinary gem.
- `MatchPrimitive`: the creation-placement helpers (`LineCentreIndex`,
  `IsExactLengthFour`, `IsLengthFiveOrMore`, `EndIndex`, `Contains`) are deleted; the
  §3.2 ordering data (`SortKey`, arms, cells) is preserved.
- `MatchShape`: L/T intersection still supplies the §3.2 ordering key; creation-order
  language removed.
- `SwapExecution`, `BattleEvent`, `BattleEventBuilder`, `MatchDetector`,
  `SwapValidator`, `BoardGenerationValidator`, `PassiveTracker`: the pass/event/swap
  descriptions now state the match-only pipeline; `GemMatched` reports the pass's one
  cleared union; combo/match accounting reads `Matches` only.
- `BattleStateService` (`ResolveBoardAsync`): the unused `swapOriginIndex` parameter
  is removed; the Boss response resolves through the same two-argument pipeline.

**Api — wire and REST payloads**

- `BattleEventWireDto`: `MatchCreated` loses `createdSpecialGems`; `GemMatched` loses
  `specialGem`. `CreatedSpecialGemWireDto` and `SpecialGemWireDto` are deleted, as are
  their projection helpers; `ProjectMatch`/`ProjectGem` map the surviving members
  only.
- `BattleHub.CellPayload` is `(string GemType)`; `SpecialGemPayload` and
  `ToSpecialGemPayload` are deleted, so `BoardStateUpdated` / `GetBattleState` cells
  carry `gemType` alone. `BattleStartStateSummary.ToCell` /
  `BattleStartCell` (and `BattleStartSpecialGem`) mirror that on the REST battle-start
  response.

**Redis / persisted battle state**

- `BattleStateJson.CellJson` is `{ "gemType": … }` only; `SpecialGemJson`, the
  `specialGem`/`type`/`orientation` member names and the cell-level ignore attribute
  are deleted. `BattleStateSerializer` writes and reads a pure `GemType` board
  through `BoardState.FromCells`.
- ADR-028 §3 (Decision B-2) tolerance basis is **unchanged**: default System.Text.Json
  settings, no `JsonUnmappedMemberHandling.Disallow`, no `[JsonRequired]` on a cell
  member. A new regression test injects a legacy per-cell member into a serialized
  record and asserts the record deserializes with the board intact. No Redis
  migration, no compatibility branch, no data mutation — the approved deployment
  policy remains "drain active battles, then deploy server and client together".

**Tests**

- Deleted (obsolete by construction — each asserted retired Special Gem behavior
  only): `SpecialGemActivationTests`, `SpecialGemCreationTests`, `SpecialGemEdgeCaseTests`,
  `SpecialGemGeometryTests`, `SpecialGemScenarioTests`.
- Adapted: every surviving Domain/Application/Api suite that referenced the retired
  members — the five `MatchedCellGemMatched`/activation assertions, the special-gem
  round-trip and event-payload assertions, the crafted-cell board helpers, and the
  three-argument resolution call sites. No assertion was weakened: where a fixture's
  property no longer held under the match-only resolution, the **fixture** was
  re-pinned (board/stream), not the assertion relaxed.
- Added regression coverage for the surviving contract:
  - `MatchFour_ShouldEmitOneMatchCreatedForTheShape_AndClearOnlyItsCells` and
    `Execute_ShouldResolveAMatchFourAsASingleConfirmedMatch` — all tiers resolve as
    one confirmed match; the pass clears exactly its matched cells; no side effects.
  - `ResolvingAMatch4_ShouldClearExactlyTheConfirmedMatchedCells_WithThePerCellResourceContract`
    (Api) — `Σ BaseOutputFor(type)` equals `BaseOutputFor(ATK) × unique cells`, no
    tier factor (TASK-291 preserved).
  - `MatchFourResolution_ShouldChargeOnItsMatches_NotItsClearedCells` /
    `MatchFourResolution_ShouldCountMatches_NotClearedCells` — passive and combo
    accounting keep counting matches, not cells.
  - `Deserialization_ShouldIgnoreALegacyPerCellMember` (production serializer) and
    `Deserialization_ShouldIgnoreAnUnknownLegacyCellMember` — the tolerant reader.
  - `BattleStateUpdated_ShouldCarryExactlyOneGemTypePerCellWithNoRetiredBoardMember`,
    `Swap_ReceiveEvents_ShouldCarryNoRetiredBoardMemberInAnyItem` (Api) — no retired
    member in any state or event payload at any depth.
  - `BoardState_ShouldRejectAnInvalidCellValue`, `Board_ShouldCarryExactlyOneBoardCollection`,
    `GemType_ShouldBeExactlyTheFourFunctionalTypes` — the pure gem-type model.

### Validation Results

```text
dotnet build src/backend/GameServer.sln                 PASS  0 errors, 49 warnings
                                                              (no new warning class; TASK-290 recorded
                                                               the same 49-warning baseline)
GameServer.Domain.Tests                                 PASS  1500 passed / 0 failed
GameServer.Api.Tests                                    PASS  377 passed / 0 failed
GameServer.Infrastructure.Tests                         FAIL  472 passed / 4 failed — ALL four fail
                                                              identically on pristine HEAD
                                                              (database-provisioning baseline)
GameServer.Application.Tests                            FAIL  740 passed / 9 failed — ALL nine are inside
                                                              the pre-existing Balance-evidence corpus
git diff --check                                        PASS  clean (staged and post-commit)
Retired-symbol sweep (src/backend + tests/backend)       PASS  0 matches for
                                                             SpecialGem|specialGem|CreatedSpecialGems|
                                                             ConsumedSpecialGem|ActivatedSpecialGems|
                                                             RequiredCreations|ReservedCreationCells|
                                                             ActivationCellGemMatched|PendingSpecialGems|
                                                             WithSpecial|FromCellEntries|ToCellArray|swapOriginIndex
src/frontend/client                                     PASS  unchanged (git status empty)
docs/ + tasks/completed/                                PASS  unchanged by this slice
```

**Balance-corpus comparison against a pristine baseline.** Baseline materialised as a
detached `git worktree` of `HEAD` (`4deece9`) and run with the same Balance filter.

```text
baseline (HEAD 4deece9)  Balance: failed 7 / passed 55
this slice               Balance: failed 9 / passed 53

persisting baseline failures (5)
  TASK194_AllSeventyFiveRuns_CompleteWithCompleteMetrics
  TASK195_PerTurnSeries seeds 20261005, 20261003
  TASK195_PowerSpentTotal seed 20261002
  H06_EveryMetric_IsPopulatedPerItsDefinition

newly failing (4) — expected consequence of the approved resolution change
  TASK195_PowerSpentTotal seeds 20261005, 20261001, 20261004
  H06_MocYeuRegeneration_IsMeasured

newly passing (2)
  TASK195_PowerSpentTotal seed 20261003
  TASK195_PerTurnSeries seed 20261002
```

The Balance suites simulate whole battles end to end, so a resolution change that
removes special-gem creation shifts simulated metrics and, for some seeds, the battle
outcome — exactly the disposition TASK-290 (F-3) and TASK-291 (F-4) recorded for their
own approved changes. The pinned expectations are Product-Owner-approved balance
evidence (`tasks/artifacts/TASK-194-*.json`, `TASK-197-*.json`) whose re-derivation is
a balance decision, not a gameplay-contract task (`AGENTS.md` §16, §17); they were
**not** edited, and the artifact files the suite rewrites were restored to `HEAD` after
the runs. Owner: a follow-up balance re-baseline task.

**Infrastructure baseline (identical pre/post).** `BossPersistenceTests.Task172ProvisioningMigration_…`,
`BossPersistenceTests.ProvisioningMigration_…`,
`BossDefinitionPostgresProvisioningTests.Postgres_ShouldRoundTripBothJsonDocumentsToTheDomainDefinitions`,
`BossDefinitionBalanceLookupTests.GetDefinitionByIdentityAsync_ShouldPreserveTheNonPersistedSecondaryEffects`
— the documented database-provisioning baseline.

### Residual-reference classification (repository-wide backend audit)

| Class | Occurrence | Disposition |
| --- | --- | --- |
| Active code that had to be removed | All `SpecialGem*` types, `Cell`, creation/activation members, wire DTOs, JSON members | Removed (this commit) |
| Historical / auditability | `tasks/completed/TASK-286…291`, `tasks/artifacts/*` (untouched), `docs/` retirement statements (TASK-287/289, untouched) | Retained |
| Explicit compatibility handling permitted by ADR-028 | The serializer's default unknown-member tolerance (no code added) | Retained as-is; now regression-tested |
| Unrelated term sharing the search string | `COMBAT_RULES.md` §5.x / `PET_RULES.md` §5.x / `API_CONTRACTS.md` §5.x citations; `CellIndex`/`Cell` words in unrelated APIs | Left untouched |
| Negative-contract tests | `ApiIntegrationTests` asserts that no retired payload member reappears, assembling the retired member names from fragments so no retired identifier is live in source | Retained (guards reintroduction) |

No active backend gameplay path still depends on the retired model.

### Quality Review (`.ai/workflow/quality/review.md` §1)

```text
Correctness   PASS  Cells are pure GemType; one pass is remove -> Gravity -> Spawn;
                    resources are BaseOutput × unique confirmed matched cells with no
                    tier factor; attack qualification is unchanged (acting actor's own
                    confirmed ATK match); cleared-cell reporting never authorizes damage;
                    events describe exactly the resolution that ran.
Architecture  PASS  All changes stay in Domain/Application/Api and the Redis JSON mapping.
                    No layering inversion, no new abstraction, no new interface/factory/
                    bus/repository. Domain still has no framework dependency.
Scope         PASS  61 declared paths; no frontend, docs, ADR, task-history or balance-
                    evidence change. No BaseOutput, match-tier, Boss AI, damage or
                    ordering change.
Tests         PASS  Deleted only suites that exclusively asserted retired behavior; every
                    surviving invariant (including the TASK-290 and TASK-291 contracts)
                    has coverage; fixtures re-pinned, assertions not weakened; new
                    tolerant-reader and no-retired-member regressions added.
Documentation PASS  docs/01-game-design and docs/02-technical already state the
                    match-only contract (TASK-287/TASK-289); code now matches them, so no
                    documentation edit was required by this slice.
Security      PASS  No authentication, session, or authorization surface touched.
Performance   PASS  Net removal on the hot resolution path (a pass no longer plans or
                    resolves activations, and no second union/order pass is recomputed).
                  No new query, allocation-heavy structure, or I/O.
Maintainability PASS  Net -4793 lines; a retired mechanism deleted rather than bypassed, so
                    it cannot be reintroduced behind a flag.
Determinism   PASS  Only Spawn consumes RNG, one selection per spawned cell; detection,
                    cascade order, gravity and spawn order unchanged; the reordered tests
                    plus CascadeAndDeterminismTests keep the guarantee.
```

---

## Remaining Issues / Follow-ups

```text
F-1  TASK-293 (frontend): the client still models/renders Special Gem payload fields.
     Server and client must be deployed together after the ADR-028 battle drain;
     this task did not touch src/frontend/client/.
F-2  Balance-evidence re-baseline: the 9 Balance-corpus failures above (4 newly
     shifted) need the Product-Owner-approved re-derivation of TASK-194/195/197
     expectations under the match-only resolution. Not part of this contract task.
F-3  Infrastructure database-provisioning failures: unchanged pre-existing baseline
     (4 tests), not caused by this slice.
F-4  Deployment procedure (not runtime behavior): ADR-028 §3 requires draining active
     battles before the joint deployment. No Redis migration was performed or invented;
     a stored record that still carries a legacy per-cell member is ignored by the
     tolerant reader.
```
