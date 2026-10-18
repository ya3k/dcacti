# TASK-299 — DEF Gem as a One-Turn Defense Buff

---

## Metadata

```text
Task ID:           TASK-299
Type:              GAMEPLAY-CHANGE
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: backend, client, testing, review
Workflow:          development/gameplay-change.md
Skills:            gameplay/gameplay-behavior-derivation, gameplay/authority-determinism-audit, quality/documentation-consistency, testing/test-scenario-generation, quality/scope-validation
Dependencies:      None
Declared Files:    docs/01-game-design/COMBAT_RULES.md, docs/01-game-design/GAME_RULES.md, docs/02-technical/GAME_STATE.md, docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md, docs/03-decisions/README.md, src/backend/GameServer.Domain/Match3/ResourceGenerator.cs, src/backend/GameServer.Domain/Match3/SwapExecution.cs, src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs, src/backend/GameServer.Domain/Battle/StatusEffect.cs, src/backend/GameServer.Domain/Combat/AttackResolution.cs, src/backend/GameServer.Domain/Combat/HealResolution.cs, src/backend/GameServer.Domain/Combat/DamagePipeline.cs, src/backend/GameServer.Domain/Cards/CardCastExecutor.cs, src/backend/GameServer.Application/Battle/BattleStateService.cs, tests/backend/GameServer.Domain.Tests/ActorRelativeMatchEffectsTests.cs, tests/backend/GameServer.Domain.Tests/DefenseBuffTests.cs, tests/backend/GameServer.Domain.Tests/PlayerEffectHealingTests.cs, tests/backend/GameServer.Application.Tests/BossPassiveEffectsTests.cs, tests/backend/GameServer.Application.Tests/BossResponseTests.cs, tests/backend/GameServer.Application.Tests/DefenseBuffLifecycleTests.cs
```

---

## Objective

Change DEF-Gem behavior from a persistent Shield absorption pool to a temporary
DEF stat buff granted to the acting actor, effective through the opponent's next
attack and expiring at the documented Turn boundary, without changing HP, POW,
or ATK behavior and without disturbing card- or Boss-granted Shields. Also guard
`HealResolution` so a dead Pet or dead Boss cannot be revived by ordinary
healing.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — no new system/content; buff uses the
  existing Status Effect model
- `docs/01-game-design/COMBAT_RULES.md` §1.1, §2, §3.2, §4, §5.1, §5.2, §5.3,
  §5.4.6, §5.5.6 — DEF-Gem output, effective DEF, Shield boundary, duration
- `docs/01-game-design/GAME_RULES.md` §1.4, §17 — Turn boundary and step order
- `docs/02-technical/GAME_STATE.md` §2.3, §2.3.1, §5.1.1 — instance schema and
  lifecycle
- `docs/03-decisions/ADR/ADR-026-*` — shared board / actor-relative ownership
  (Decision item 4 DEF bullet, amended in place by this task)

---

## Scope

### In Scope

- A confirmed DEF-Gem match grants one Turn-based `BuffDebuff` instance
  (`TargetStat = "DEF"`, identity `"DefenseBuff"`) to the acting actor.
- The buff raises the **effective DEF** the Damage Pipeline's mitigation step
  reads, for every damage instance against that actor while active.
- Duration/expiry: active through the opponent's next attack; removed at the
  step 19a boundary that ends the covered Turn (Pet: 1 Turn; Boss: 2 Turns,
  because the Player acts before the Boss in one atomic Round).
- One explicit stacking/refresh rule: refresh-not-stack (§5.2 item 2); a newer
  application sets the magnitude, never sums it.
- DEF Gems create no Shield; existing card/boss-skill Shields are untouched.
- Actor-relative ownership (Player buff on the Pet, Boss buff on the Boss).
- Terminal/duplicate-safety: the buff is applied inside the committed action
  resolution, so an idempotent duplicate action cannot apply it twice.
- `HealResolution` dead-target guard + regression tests.
- Documentation synchronization and the ADR-026 amendment.

### Out of Scope

- Balance tuning of HP, POW, ATK, `DefGemBaseDefense`, or any other value.
- Re-baselining pinned balance-evidence artifacts (protected; separate task).
- Reintroducing Special Gems or match-tier multipliers.
- Any new Status Effect `Type`, state member, wire member, Redis key, or
  database column.
- Historical migrations and unrelated ADRs.

---

## Current State

`ResourceGenerator.ApplyShield` converts the generated `DefensePool` into a
`StatusEffectType.Shield` instance; `SwapExecution` (Player) and
`ResourceGenerator.ApplyActorMatchEffects` (Boss) call it. The Damage Pipeline
reads `PetState.DEF` / `BossState.DEF` directly at `AttackResolution`,
`CardCastExecutor`, and the `BattleStateService` DoT ticks.

---

## Declared File Set (P-2 / P-5)

```text
docs/01-game-design/COMBAT_RULES.md
docs/01-game-design/GAME_RULES.md
docs/02-technical/GAME_STATE.md
docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md
docs/03-decisions/README.md
src/backend/GameServer.Domain/Match3/ResourceGenerator.cs
src/backend/GameServer.Domain/Match3/SwapExecution.cs
src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs
src/backend/GameServer.Domain/Battle/StatusEffect.cs
src/backend/GameServer.Domain/Combat/AttackResolution.cs
src/backend/GameServer.Domain/Combat/HealResolution.cs
src/backend/GameServer.Domain/Combat/DamagePipeline.cs
src/backend/GameServer.Domain/Cards/CardCastExecutor.cs
src/backend/GameServer.Application/Battle/BattleStateService.cs
tests/backend/GameServer.Domain.Tests/ActorRelativeMatchEffectsTests.cs
tests/backend/GameServer.Domain.Tests/DefenseBuffTests.cs
tests/backend/GameServer.Domain.Tests/PlayerEffectHealingTests.cs
tests/backend/GameServer.Application.Tests/BossPassiveEffectsTests.cs
tests/backend/GameServer.Application.Tests/BossResponseTests.cs
tests/backend/GameServer.Application.Tests/DefenseBuffLifecycleTests.cs
```

---

## Acceptance Criteria

- [x] A confirmed DEF-Gem match grants the acting actor one `"DefenseBuff"`
      instance (`BuffDebuff`, `TargetStat = "DEF"`) whose magnitude is that
      action's `DefensePool`; no Shield instance is created.
- [x] Effective DEF = stored base DEF + applicable DEF-buff magnitudes; the base
      stat is unchanged, and the mitigation step uses the effective value.
- [x] Incoming damage against a buffed defender is reduced through the Damage
      Pipeline by the buff.
- [x] The Pet's buff survives the same-Round Boss attack and is gone at that
      Turn's step 19a; the Boss's buff survives into the next Turn's Player
      attack and is gone at that Turn's step 19a; neither leaks further.
- [x] A second DEF match refreshes (sets) the buff magnitude and duration; it
      never accumulates.
- [x] Player and Boss buffs are actor-relative and never cross entities.
- [x] Duplicate/idempotent action submission cannot apply the buff twice.
- [x] Existing card/relic/Boss Shields keep their behavior.
- [x] `HealResolution.ApplyPetHeal`/`ApplyBossHeal` leave a target at HP <= 0
      unchanged.
- [x] Documentation (COMBAT_RULES, GAME_RULES, GAME_STATE, ADR-026, ADR index)
      is consistent with the implementation.
- [x] Relevant tests pass at the required validation depth, with any
      pre-existing/environmental failures disclosed.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — effective DEF composition, instance application,
                         refresh, no-Shield, dead-target heal guard
[x] Integration tests  — full Round: Pet buff covers the Boss attack and
                         expires; Boss buff covers the next Player attack and
                         expires; idempotent duplicate action
[x] Gameplay scenarios — COMBAT_RULES.md §5.4.6 / §5.5.6 given/when/then
```

---

## Stop Conditions

- Required behavior cannot be derived from the Authoritative References → STOP
  per `AGENTS.md` §7.
- Client-authoritative calculation required → STOP per `AGENTS.md` §10.
- Balance evidence re-baselining proves necessary → report as a separate task
  (protected artifacts must not be overwritten).

---

## Completion Evidence

**Completed.** `Status: DONE`. Phase A implementation slice committed; Phase B
completion record filed under operator authorization.

### Commit

```text
Phase A implementation slice   18999f11a528066e4444775ca90c6bcf67b19ba3
Subject                        feat(combat): implement def gem as one-turn defense buff
Owns                           TASK-299
Files                          20 declared paths (see ## Declared File Set (P-2 / P-5))
Diff                           20 files changed, 1820 insertions(+), 124 deletions(-)
Shared with                    none
Unowned / pre-existing         none
Phase B completion record      tasks/completed/TASK-299-def-gem-as-one-turn-defense-buff.md
```

### Changed Files

```text
docs/01-game-design/COMBAT_RULES.md                  §2 DEF row + actor-relative bullet, §3.2 effective DEF,
                                                     §4 items 1/3/7, §5.1 list, new §5.4.6 / §5.5.6, version
docs/01-game-design/GAME_RULES.md                    §17 step 18a DEF resource wording, version
docs/02-technical/GAME_STATE.md                      §2.3 note, §2.3.1 items 1–2, version
docs/03-decisions/ADR/ADR-026-…md                    in-place amendment: DEF-Gem bullet corrected
docs/03-decisions/README.md                          version history records the ADR-026 amendment
src/backend/GameServer.Domain/Battle/StatusEffect.cs          Id example list
src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs EffectivePetDefense/EffectiveBossDefense,
                                                              DefenseBuffId/DefenseStat, selector
src/backend/GameServer.Domain/Match3/ResourceGenerator.cs     ApplyDefenseBuff (Pet/Boss), durations,
                                                              ApplyActorMatchEffects, docs
src/backend/GameServer.Domain/Match3/SwapExecution.cs         Player DEF buff application
src/backend/GameServer.Domain/Combat/AttackResolution.cs      effective DEF at both attack sites
src/backend/GameServer.Domain/Combat/DamagePipeline.cs        comment: DEF input is the effective value
src/backend/GameServer.Domain/Combat/HealResolution.cs        dead-target heal guard (§4 items 1/7)
src/backend/GameServer.Domain/Cards/CardCastExecutor.cs       card damage uses effective Boss DEF
src/backend/GameServer.Application/Battle/BattleStateService.cs DoT ticks use effective DEF
tests/backend/GameServer.Domain.Tests/DefenseBuffTests.cs                       (new, 17 tests)
tests/backend/GameServer.Domain.Tests/ActorRelativeMatchEffectsTests.cs         DEF-buff rewrite
tests/backend/GameServer.Domain.Tests/PlayerEffectHealingTests.cs               dead-target guard tests (7 new tests)
tests/backend/GameServer.Application.Tests/DefenseBuffLifecycleTests.cs         (new, 7 tests)
tests/backend/GameServer.Application.Tests/BossPassiveEffectsTests.cs           DEF-buff expectations
tests/backend/GameServer.Application.Tests/BossResponseTests.cs                 effective-DEF expectation
```

### Validation Results

```text
Domain          dotnet test -c Release   1524 passed / 0 failed   (baseline 1500/0, delta +24)
Application     dotnet test -c Release    775 passed / 2 failed   (baseline 758/0, 777 total)
Api             dotnet test -c Release    377 passed / 0 failed   (baseline 377/0, 377 total)
Infrastructure  dotnet test -c Release    472 passed / 4 failed   (baseline 472/4, 476 total — identical, pre-existing)
Frontend        npm test -- --run        1244 passed / 0 failed   (24 test files)
E2E smoke       npm run verify:e2e:smoke   2 runs, 162 checks each, 0 failures (new backend build)
E2E history     npm run verify:e2e:history 2 runs,  51 checks each, 0 failures (new backend build)
```

#### Known Application Failures (Protected Balance Artifacts)

The two Application test failures are pinned balance-evidence measurement tests:
1. `GameServer.Application.Tests.Balance.Tests.BalanceEvidenceMeasurementCorrectionTests.TASK195_PerTurnSeries_ReconcilesTerminalCardCastWithoutPhantomTurn`
   - *Failure:* `Assert.StartsWith() Failure: String start does not match. String: "the committed Swap resolved the battle (V...", Expected start: "Card cast resolved"`
2. `GameServer.Application.Tests.Balance.Tests.BalanceEvidenceMeasurementCorrectionTests.TASK296_ReviewD_Canonical75RunCandidate_Verification`
   - *Failure:* `Assert.Equal() Failure: Values differ. Expected: 8, Actual: 5`

*Root Cause & Disposition:* Both tests execute deterministic fixed-seed battle simulations asserting against pinned balance-evidence artifacts created under the retired persistent Shield mechanics. Replacing persistent Shield accumulation with temporary 1-turn DEF mitigation alters damage pacing in these scenarios (e.g. earlier battle resolution via Swap; turn count shifting from 8 to 5). Per TASK-299 constraints, protected balance artifacts (`TASK-194`, `TASK-296`) must not be modified or re-baselined in this task; they require a dedicated balance re-baselining task following operator sign-off.

#### Known Infrastructure Failures (Verified Baseline Status)

The four Infrastructure test failures are:
1. `GameServer.Infrastructure.Tests.BossDefinitionBalanceLookupTests.GetDefinitionByIdentityAsync_ShouldPreserveTheNonPersistedSecondaryEffects`
2. `GameServer.Infrastructure.Tests.BossPersistenceTests.Task172ProvisioningMigration_ShouldWriteTheDocumentedPassiveAndSkillDocuments`
3. `GameServer.Infrastructure.Tests.BossPersistenceTests.ProvisioningMigration_ShouldWriteTheDocumentedPassiveAndSkillDocuments`
4. `GameServer.Infrastructure.Tests.BossDefinitionPostgresProvisioningTests.Postgres_ShouldRoundTripBothJsonDocumentsToTheDomainDefinitions`

*Baseline Verification:* These four failures are pre-existing, verified identical across prior completed tasks including `TASK-267`, `TASK-269`, `TASK-270`, `TASK-279`, `TASK-290`, `TASK-292`, and `TASK-298`. They stem from legacy JSON document formatting and EF Core migration schema fixtures in Postgres test harnesses, completely independent of combat mechanics, DEF buffing, or the application layer.

#### DEF Buff Semantics & Behavior

1. **Duration & Window:**
   - Active through the opponent's next attack; expires and is removed at the step 19a boundary of the covered Turn.
   - *Pet DEF buff:* 1 Turn duration (`PlayerDefBuffDurationTurns = 1`). Applied at step 12/14; mitigates Boss attack at step 18; decremented and removed at step 19a of the same Turn.
   - *Boss DEF buff:* 2 Turns duration (`BossDefBuffDurationTurns = 2`). Applied at step 18a; survives step 19a of Turn N (2 → 1); mitigates Player attack at step 15 of Turn N+1; decremented and removed at step 19a of Turn N+1 (1 → 0).
   - The duration asymmetry is structural (Player acts first in an atomic Round), expressing the identical gameplay rule: coverage across exactly one opponent attack phase without leaking.
2. **Refresh-Not-Stack:**
   - In accordance with `COMBAT_RULES.md` §5.2 item 2 and `GAME_STATE.md` §2.3.1 item 6 / §5.1.1 item 1: refresh, do not stack.
   - Subsequent DEF matches set `Magnitude` to the new action's generated `DefensePool` and reset `RemainingTurns` to base duration. Magnitudes are never summed across actions.
   - Multiple DEF matches within a single Swap resolution accumulate into that action's single `DefensePool` prior to application; at most one `"DefenseBuff"` instance exists per actor.
3. **Effective-DEF Semantics:**
   - Stored stats (`PetState.DEF` and `BossState.DEF`) are immutable base values and are never overwritten.
   - Damage Pipeline step 5 (Defense Mitigation) dynamically calculates `EffectiveDEF = StoredBaseDEF + Σ Magnitude` for all active `BuffDebuff` instances with `TargetStat = "DEF"`.
   - The contribution is flat, integral, additive, and evaluated on committed status effects at damage evaluation time.
   - Applied uniformly across all incoming damage instances: Boss attacks, Card damage, and DoT ticks.
   - DEF Gems produce NO Shield absorption pool (`StatusEffectType.Shield`); existing card/relic/Boss Shields are untouched and operate independently.

---

## Lifecycle Transition — DONE (Phase B Filing)

<!--
  LIFECYCLE TRANSITION RECORD: IN REVIEW -> DONE
  Governed by tasks/TASK_LIFECYCLE.md §1, §3, §4, §6.1, §6.4 and Operator Authorization.
-->

* **Transition:** `IN REVIEW` → `DONE`
* **Authorizing Basis:** Operator Authorization (TASK-299 Phase A/B commit execution).
* **Repository Policy:** `tasks/TASK_LIFECYCLE.md` §1 (DONE state definition), §3 (completed/ location), §4 (file move: `active/` → `completed/`), §6.1 (Phase B completion record filing commit).
* **Destination Path:** `tasks/completed/TASK-299-def-gem-as-one-turn-defense-buff.md`
* **Phase A Commit SHA:** `18999f11a528066e4444775ca90c6bcf67b19ba3`
