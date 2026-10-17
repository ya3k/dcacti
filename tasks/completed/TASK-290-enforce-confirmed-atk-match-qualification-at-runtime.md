# TASK-290 — Enforce Confirmed ATK-Match Qualification at Runtime

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  The requester labelled this task "IMP-290". The repository's highest existing
  Task ID on disk is TASK-289, so the next sequential ID is TASK-290; the ID and
  the requester's label therefore agree. This record is created under the
  repository's task-creation workflow (`tasks/TASK_TEMPLATE.md`) because no
  IMP-290 manifest existed in `tasks/`.

  LIFECYCLE NOTE (disclosed, not silent). The task was created directly in
  `tasks/active/` with `Status: IN PROGRESS` because the requester instructed the
  agent to "begin with preflight and execute the task; do not return only a
  plan". `TASK_LIFECYCLE.md` §4 makes READY → IN PROGRESS a status change plus a
  `backlog/` → `active/` move; creating the record already in `active/` records
  the transition that the execution instruction authorises, and avoids a
  created-then-immediately-moved record inside one slice.
-->

---

## Metadata

```text
Task ID:           TASK-290
Type:              GAMEPLAY-CHANGE
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: backend, testing, review
Workflow:          development/gameplay-change.md
Skills:            gameplay/gameplay-behavior-derivation, gameplay/authority-determinism-audit,
                   testing/test-scenario-generation, quality/implementation-review,
                   discovery/impact-analysis
Dependencies:      ADR-026 (Accepted); ADR-028 (Accepted, untracked — see D-3);
                   TASK-286 (completed); TASK-287 (completed); TASK-289 (completed)
Declared Files:    src/backend/GameServer.Domain/Combat/AttackResolution.cs,
                   src/backend/GameServer.Application/Battle/BattleStateService.cs,
                   tests/backend/GameServer.Domain.Tests/AttackResolutionTests.cs,
                   tests/backend/GameServer.Domain.Tests/BossSkillContractTests.cs,
                   tests/backend/GameServer.Application.Tests/AtomicRoundOrchestrationTests.cs
```

<!--
  RISK/PRECONDITION DISCLOSURE. `tasks/TASK_TYPES.md` §4 gives GAMEPLAY-CHANGE a
  HIGH baseline, and HIGH is selected because the invariant decides whether attack
  damage executes at all, on both the Player and the Boss path.

  Docs-first precondition (AGENTS.md §17 / development/gameplay-change.md §3): the
  governing rule is ALREADY authoritative and committed — `COMBAT_RULES.md` §2
  item 6 ("Attack qualification (OD-6)") and §3, `MATCH3_RULES.md` §5.7 item 8,
  `GAME_STATE.md` §2.1.8 item 4, `TDD.md` §8 item 6, and `ADR-026` D1 — landed by
  the completed TASK-286 / TASK-287 / TASK-289 slices. This task is the
  implementation half of that already-approved change; it changes no rule.
-->

---

## Objective

Make runtime attack qualification depend on the acting actor's **own confirmed ATK-gem match** — authoritative match-resolution evidence — instead of on `ClearedGemCounts.Atk > 0`, so that the documented invariant holds:

> An attack qualifies **if and only if** the acting actor's own resolution contains a confirmed valid ATK-gem match.

---

## Authoritative References

- `docs/01-game-design/COMBAT_RULES.md` §2 item 6 ("Attack qualification (OD-6)") and §2 item "Actor-relative resource handling" — clears counts are reporting only
- `docs/01-game-design/COMBAT_RULES.md` §3 (order of operations, "If no valid ATK-gem match is confirmed" list) and §3.3 item 8 — modifier preservation
- `docs/01-game-design/MATCH3_RULES.md` §5.7 item 8 — the board-side facts (Matches only; attack qualification is not owned there)
- `docs/01-game-design/GAME_RULES.md` §17 — the fixed resolution order
- `docs/02-technical/GAME_STATE.md` §2.1.8 item 4 — "Attack qualification" state-model statement
- `docs/02-technical/TDD.md` §8 item 6 — "Attack Qualification Invariant"
- `docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md` D1 and item 4 — confirmed-ATK gating and actor ownership
- `docs/03-decisions/ADR/ADR-028-retire-special-gems-from-battle-state-model.md` item 5 and item 7 — OD-6 and the "Independent ATK-match qualification gate" slice
- `docs/00-overview/MVP_SCOPE.md` §1 — Match-3 / Combat / Bosses are IN scope
- `tasks/TASK_LIFECYCLE.md` §3, §4, §6; `tasks/README.md`

---

## Scope

### In Scope

- Introduce a minimal qualification value derived from the authoritative resolution evidence (`CascadeResolver.CascadeResult.Passes[].Matches[].Shape.GemType`).
- Pass that value through the existing runtime boundary (`AttackResolution`), replacing the `ClearedGemCounts` parameter that currently carries the gate.
- Consume the qualification result in the damage-execution branch of both the Player and the Boss path `BattleStateService` already owns.
- Preserve cleared-gem accounting for reporting/resource generation.
- Regression tests for: positive ATK match, DEF-only, HP/POWER-only, stale positive ATK count, absent reporting count, actor isolation, resolution isolation, accounting separation, damage preservation.

### Out of Scope

- Removing `SpecialGem`, `SpecialGemType`, `SpecialGemOrientation`, `SpecialGemClaim`, `MatchCreated.createdSpecialGems`, or `CellPayload.specialGem` (ADR-028 item 7's compile-coupled Domain+Api slice; see F-2).
- Removing match-tier multipliers from `ResourceGenerator` / `MatchTier` (see F-1).
- Frontend code (owner tasks TASK-275 / TASK-285 hold those paths).
- Boss AI count-only scoring (B-1 follow-up; see F-2).
- Match detection, gravity, spawn, cascade semantics, damage balance, BaseOutput rates.
- Any ADR or design-document edit; any stage/commit of `docs/03-decisions/README.md` or the untracked ADR-028 file.

---

## Current State

At `HEAD` (`b634d00`, `master`) the runtime gates attack damage on a cleared-gem count while the committed design documents already forbid that:

```text
AttackResolution.ResolvePlayerAttack(..., ClearedGemCounts clearedGems, ...)
AttackResolution.ResolveBossAttack(...,  ClearedGemCounts clearedGems, ...)
        if (clearedGems.Atk == 0) return NonQualifying(...)
BattleStateService (Player path, step 15) → result.Resources.ClearedGemCounts
BattleStateService (Boss path, step 18d) → bossGeneration.ClearedGemCounts
```

`ClearedGemCounts` is produced by `ResourceGenerator.Generate` as cleared-cell **accounting**. Under the still-present (but design-retired) Special Gem activation path a resolution can clear an ATK cell without an ATK Match, so the count is not equivalent to the confirmed-match predicate; and under the post-retirement model the count carries no evidence at all beyond the Match that produced it.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-290:

```text
src/backend/GameServer.Domain/Combat/AttackResolution.cs
src/backend/GameServer.Application/Battle/BattleStateService.cs
tests/backend/GameServer.Domain.Tests/AttackResolutionTests.cs
tests/backend/GameServer.Domain.Tests/BossSkillContractTests.cs
tests/backend/GameServer.Application.Tests/AtomicRoundOrchestrationTests.cs
```

*(The task record itself is excluded from the implementation declared file set — `tasks/TASK_TEMPLATE.md`.)*

---

## Acceptance Criteria

- [x] AC-1 Attack authorization in `AttackResolution` depends on the acting actor's own confirmed valid ATK-gem match, derived from `CascadeResolver.CascadeResult`.
- [x] AC-2 `ClearedGemCounts` can no longer independently authorize damage: it is not a parameter of the attack-resolution API, so the cleared ATK count cannot reach the eligibility branch.
- [x] AC-3 Both call sites in `BattleStateService` supply the evidence of the resolution they actually resolved (Player: `result.Resolution`; Boss: `bossResolution`), so no other actor's or earlier resolution's evidence can qualify.
- [x] AC-4 Cleared-gem reporting and non-damage resource accounting are unchanged.
- [x] AC-5 Damage calculation, BaseOutput rates, downstream modifiers, passive effects, turn ordering and unrelated combat behavior are unchanged.
- [x] AC-6 No Special Gem behavior is introduced and no retired Special Gem data establishes qualification.
- [x] AC-7 Regression tests cover the nine required cases at the narrowest appropriate layer.
- [x] AC-8 Backend solution builds; relevant Domain / Application / Api suites run; no new failure versus the recorded baseline.
- [x] AC-9 `git diff --check` clean; the final diff contains only this task's declared files.
- [x] AC-10 Quality review checklist passes (`.ai/workflow/quality/review.md` §1).

---

## Affected Files & Areas

```text
[x] src/backend/GameServer.Domain/Combat/AttackResolution.cs        qualification value + gate
[x] src/backend/GameServer.Application/Battle/BattleStateService.cs the two call sites
[x] tests/backend/GameServer.Domain.Tests/                          domain regression tests
[x] tests/backend/GameServer.Application.Tests/                     round-level regression tests
[ ] src/frontend/client/                                            NOT TOUCHED
[ ] docs/                                                           NOT TOUCHED
```

---

## Implementation Notes

- The authoritative evidence already exists: `CascadeResolver.CascadeResult.Passes[].Matches[]` is the detected-match report the cascade loop produces (`MATCH3_RULES.md` §4.3), and `MatchResolution.Shape.GemType` is the Match's Gem type. `ResourceGenerator.CountPowerMatchGroups` reads the same structure for the Boss SkillCharge contract, so reading it for qualification duplicates no detection.
- Presentation events (`MatchCreated`, `GemMatched`) are **not** a source for qualification: they are outputs describing a finished resolution (`GAME_EVENTS.md` §3 item 6).
- The qualification is a value derived at the call site from the resolution being resolved. It is not stored, not serialized, and not carried in `BattleState` / `PetState` / `BossState`, which is what makes per-actor and per-resolution isolation structural rather than a convention.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — GameServer.Domain.Tests/AttackResolutionTests.cs (qualification + gate)
[x] Integration tests  — GameServer.Application.Tests/AtomicRoundOrchestrationTests.cs (one atomic round)
[x] Gameplay scenarios — Given a pinned board and a committed Swap, When the round resolves,
                        Then damage executes iff the acting actor's own resolution confirmed an ATK Match
[x] Build              — dotnet build src/backend/GameServer.sln
[x] Api suite          — dotnet test tests/backend/GameServer.Api.Tests
[x] git diff --check
```

### Key Edge Cases

- A stale positive `ClearedGemCounts.Atk` with no confirmed ATK Match (`COMBAT_RULES.md` §2 item 6).
- A confirmed ATK Match with the reporting count absent (§2 item 6: reporting is not the predicate).
- Another actor's ATK Match in the same atomic round (ADR-026 item 4).
- An earlier round's ATK Match (per-resolution evidence, never carried forward).

---

## Stop Conditions

- If the required behavior cannot be derived from the Authoritative References: STOP (`AGENTS.md` §7).
- If a change is needed outside the declared file set: STOP (`AGENTS.md` §16).
- If a documentation conflict is found: STOP and report (`AGENTS.md` §4).
- If any command would stage, commit, stash, reset, restore, or clean a path this task does not own: STOP (`tasks/TASK_LIFECYCLE.md` §6.2 P-6).

---

## Discovered Issues (reported, not fixed — `AGENTS.md` §16)

```text
F-1  docs/01-game-design/ documents and docs/02-technical/TDD.md §8 items 2–3
     state that match-tier multipliers are retired, but
     src/backend/GameServer.Domain/Match3/ResourceGenerator.cs still applies
     MatchTierMultipliers and GameServer.Domain.Tests/ResourceGenerationTests.cs
     still asserts Match 4 = 1.5x / Match 5 = 2.0x / L/T = 1.25x.
     Impact: documentation/implementation divergence outside this task's
     invariant; it does not affect attack qualification.
     Follow-up: a runtime slice under the TASK-286 follow-up graph
     (OD-3/OD-4 retirement of match tiers + B-1 Boss AI count-only scoring).
     Owner: follow-up task to be created; NOT this task.

F-2  ADR-028 item 7's compile-coupled Domain+Api Special Gem removal, the
     frontend presentation retirement, and B-1 Boss AI count-only scoring
     remain unstarted. They are separate slices with separate ownership.
     Owner: follow-up tasks; NOT this task.

F-3  Pre-existing failures: balance-suite tests fail at HEAD (10 failures in
     GameServer.Application.Tests Balance.Tests) — evidence recorded in
     Completion Evidence. Outside this task's scope; no correction attempted.
```

---

## Quality Review (`.ai/workflow/quality/review.md` §1)

```text
Correctness    PASS  The predicate is COMBAT_RULES.md §2 item 6's "at least one
                     confirmed valid ATK-gem match" read from the resolution's own
                     confirmed Matches (MATCH3_RULES.md §4.3); COMBAT_RULES.md §3's
                     "If no valid ATK-gem match is confirmed" list is implemented
                     item for item (no pipeline run, no ATK use, zero damage, no
                     damage events, modifiers preserved). GAME_STATE.md §2.1.8 item 4
                     and TDD.md §8 item 6 now describe the runtime.
Architecture   PASS  Rule lives in Domain/Combat (AGENTS.md §12); Application only
                     orders the call and supplies the resolution it owns
                     (ARCHITECTURE.md §2.1). No new layer, no new dependency edge:
                     Combat already referenced Match3 for ClearedGemCounts.
Scope          PASS  Five declared files, no other path touched; nothing renamed or
                     refactored; no Special Gem, match-tier, frontend, or Boss AI
                     change.
Tests          PASS  HIGH depth (core/validation.md §2): unit tests plus
                     state-transition-level gameplay scenarios over one atomic round.
Documentation  PASS  No documentation change is required: the documents already
                     state the invariant (AGENTS.md §17 — the code was the stale
                     side). No source-of-truth conflict introduced.
Security       PASS  No authentication, authorization, session, or credential path
                     touched.
Performance    PASS  One O(confirmed Matches) walk over a structure the round
                     already produced; replaces a struct parameter with a bool
                     payload. No new I/O, database access, or allocation on the
                     resolution path (TDD.md §4.3).
Maintainability PASS One record struct plus one factory; no interface, registry,
                     or speculative abstraction (AGENTS.md §9). The cleared-gem
                     parameter is REMOVED rather than ignored, so the regression
                     cannot be reintroduced by passing counts.
Determinism    PASS  Pure function of the resolution; no RNG, no clock, no
                     enumeration-order dependence. Server authority unchanged, client
                     untouched (AGENTS.md §10/§11, ADR-001, ADR-009).
```

---

## Validation Results (actual commands, actual outcomes)

Baseline for comparison is a pristine `HEAD` tree materialised with
`git archive HEAD` at `E:\dcacti-baseline-HEAD`; the working tree was never
stashed, reset, checked out, or restored.

```text
dotnet build src/backend/GameServer.sln
    → 0 Error(s), 49 Warning(s)   (warnings identical to the HEAD baseline)

dotnet test tests/backend/GameServer.Domain.Tests
    → Passed: 1643, Failed: 0, Total: 1643
      (HEAD baseline: 1633 — the +10 are this task's new Domain tests)

dotnet test tests/backend/GameServer.Application.Tests
    → Passed: 740, Failed: 9, Total: 749
      (HEAD baseline: Passed 737, Failed 10, Total 747 — +2 are this task's
       new round-level tests)

dotnet test tests/backend/GameServer.Api.Tests
    → Passed: 377, Failed: 0, Total: 377

dotnet test tests/backend/GameServer.Infrastructure.Tests
    → Passed: 472, Failed: 4, Total: 476
      (HEAD baseline: identical 4 failures, same four tests)

git diff --check
    → clean (exit 0)
```

### Pre-existing failures (not caused by this task — `core/validation.md` §3)

`GameServer.Application.Tests` — every failure is in `Balance.Tests` and every one
of them fails on the pristine `HEAD` tree too:

```text
BalanceEvidenceMeasurementCorrectionTests.TASK195_PowerSpentTotal_…   5 cases  both
BalanceEvidenceMeasurementCorrectionTests.TASK195_PerTurnSeries_…     3 @ HEAD, 2 now
BalanceSimulatorTests.H06_EveryMetric_IsPopulatedPerItsDefinition     1 case  both
BalanceBaselineMatrixTests.TASK194_AllSeventyFiveRuns_…               1 case  both
```

The failure set is a **subset** of the baseline set (9 vs 10): no new failure was
introduced. The one case that now passes
(`TASK195_PerTurnSeries_SatisfiesInvariantOnCastTerminatedRuns(seed: 20261003)`)
is a stale balance-evidence expectation in an already-failing class, and the class
still fails otherwise — the task does not claim to fix it.

`GameServer.Infrastructure.Tests` — the same four tests fail on the pristine
`HEAD` tree (`BossDefinitionBalanceLookupTests`, `BossPersistenceTests` ×2,
`BossDefinitionPostgresProvisioningTests`); they are database-provisioning
contracts unrelated to attack qualification.

---

## Completion Evidence

**Completed.** `Status: DONE`. Phase A implementation slice committed and Phase B completion record filed.

### Commit

```text
Phase A implementation slice   5429b222a94c9a272bf283bbbbbf3123f33d3a47   (see `git log`; subject below)
Subject                        fix(combat): gate attack damage on the actor's own confirmed atk match
Owns                           TASK-290
Files                          src/backend/GameServer.Domain/Combat/AttackResolution.cs
                               src/backend/GameServer.Application/Battle/BattleStateService.cs
                               tests/backend/GameServer.Domain.Tests/AttackResolutionTests.cs
                               tests/backend/GameServer.Domain.Tests/BossSkillContractTests.cs
                               tests/backend/GameServer.Application.Tests/AtomicRoundOrchestrationTests.cs
Shared with                    none
Unowned / pre-existing         none
```

The exact Phase A SHA is the `fix(combat): gate attack damage on the actor's own
confirmed atk match` commit on `master`; it is the direct parent of the Phase B
completion-record commit. Staged set verified with `git diff --cached
--name-status` as exactly the five declared files, zero extraneous paths.

### Changed Files

- `src/backend/GameServer.Domain/Combat/AttackResolution.cs` — adds
  `AttackQualification` (`bool HasConfirmedAtkMatch`, `None`,
  `FromResolution(CascadeResolver.CascadeResult)` reading
  `Passes[].Matches[].Shape.GemType`); replaces the `ClearedGemCounts` parameter of
  `ResolvePlayerAttack` / `ResolveBossAttack` /
  `ResolveAndApplyPlayerAttack` / `ResolveAndApplyBossAttack` with it; the gate is
  now `!qualification.HasConfirmedAtkMatch`. Documentation rewritten to the
  confirmed-ATK-match predicate. No damage arithmetic touched.
- `src/backend/GameServer.Application/Battle/BattleStateService.cs` — the Player
  call site (step 15) supplies `AttackQualification.FromResolution(result.Resolution)`;
  the Boss call site (step 18d) supplies
  `AttackQualification.FromResolution(bossResolution)`. Comments corrected from
  "cleared no ATK Gem" to the confirmed-match predicate. No ordering, modifier, or
  write-back change.
- `tests/backend/GameServer.Domain.Tests/AttackResolutionTests.cs` — rewritten to the
  qualification API; 19 cases covering the invariant (positive, DEF-only,
  HP/POWER-only, stale positive count, absent reporting count, actor isolation,
  resolution isolation, both gates' positive/negative branches, modifier
  preservation/consumption, Shield-absorbed qualifying attack, damage preservation).
- `tests/backend/GameServer.Domain.Tests/BossSkillContractTests.cs` — the two
  `ResolveBossAttack` calls pass `AttackQualification.None` / a qualifying value
  instead of `ClearedGemCounts`.
- `tests/backend/GameServer.Application.Tests/AtomicRoundOrchestrationTests.cs` —
  two new round-level scenarios: `NonAtkSwap_ShouldKeepClearedGemReportingAndItsResourceEffects`
  (accounting separation) and
  `PlayerDamageInstances_ShouldExistExactlyWhenThePlayersOwnResolutionConfirmedAnAtkMatch`
  (per-round equivalence, resolution isolation, actor isolation), plus comment
  corrections on the existing gate tests.

### Test Disposition (the nine required cases)

```text
1 positive            Domain: Qualification_WhenTheActorsResolutionHasAConfirmedAtkMatch_ShouldQualify
                      App:    PlayerAtkMatch_ShouldDamageTheBossThroughTheGatedPipeline
2 DEF-only negative   Domain: Qualification_WhenTheActorsResolutionHasOnlyANonAtkMatch_ShouldNotQualify(Def)
3 HP/POWER negative   Domain: same theory (Hp), (Power)
4 stale count         Domain: Attack_WhenOnlyTheClearedAtkCountIsPositive_ShouldNotExecuteDamage
5 missing count       Domain: Qualification_WhenTheAtkMatchExistsButTheReportIsAbsent_ShouldStillQualify
6 actor isolation     Domain: Qualification_WhenOnlyAnotherActorHasAnAtkMatch_ShouldNotQualifyTheActingActor
                      App:    PlayerDamageInstances_ShouldExistExactlyWhenThePlayersOwnResolutionConfirmedAnAtkMatch
7 resolution isolation Domain: Qualification_FromAnEarlierResolution_ShouldNotAuthorizeTheCurrentOne
                      App:    the same per-round test (successive rounds, alternating Match type)
8 accounting separation Domain: ResourceGenerator reporting still derives the counts;
                      App: NonAtkSwap_ShouldKeepClearedGemReportingAndItsResourceEffects,
                           PlayerPowerMatch_ShouldNotChargeTheBoss (existing)
9 damage preservation Domain: QualifyingAttack_ShouldProduceTheSameDamageWhateverTheQualificationCarriesAside
                      App:    PlayerAtkMatch_ShouldDamageTheBossThroughTheGatedPipeline (event triple)
```

### Validation Results

```text
dotnet build src/backend/GameServer.sln                        PASS  0 Error(s), 49 Warning(s) (baseline identical)
dotnet test tests/backend/GameServer.Domain.Tests              PASS  1643 passed / 0 failed  (baseline 1633)
dotnet test tests/backend/GameServer.Application.Tests         PASS* 740 passed / 9 failed / 749 total
                                                                    (baseline 737 / 10 / 747 — subset, no new failure)
dotnet test tests/backend/GameServer.Api.Tests                 PASS  377 passed / 0 failed
dotnet test tests/backend/GameServer.Infrastructure.Tests      PASS* 472 passed / 4 failed / 476 total
                                                                    (baseline identical: same four tests)
git diff --check                                               PASS  clean (exit 0)
```

`*` pre-existing failures, reproduced on a pristine `HEAD` tree materialised with
`git archive HEAD`; the failing sets and their assertion messages are recorded in
the Validation Results section above.

### Server Authority & Scope Verification

- [x] Zero client-authoritative gameplay logic added or changed (no client file touched)
- [x] Adhered to `MVP_SCOPE.md` §1 (reduction/alignment only; no new system or content)
- [x] Server authority and determinism preserved (`AGENTS.md` §10/§11, ADR-001, ADR-009)
- [x] Special Gems were neither removed nor used to establish qualification; no
      frontend change; no Boss AI change
- [x] Documentation required no update: the code was brought to the already-committed
      rule (`AGENTS.md` §17)
- [x] P-1…P-6 posture: Phase A staged set equals the declared file set; Phase B
      filed this record alone; no amend/rebase/history rewrite; nothing pushed
- [x] Pre-existing worktree changes (`docs/03-decisions/README.md`, untracked
      ADR-028) preserved untouched and unstaged
- [x] Test-regenerated balance artifacts
      (`tasks/artifacts/TASK-194-*.json`, `TASK-197-*.json`) were written by the
      validation runs and restored to `HEAD`; they are not part of this task's diff
