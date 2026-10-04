# TASK-174 — Implement Sơn Thạch Vệ Once-Per-Battle Boss Passive Runtime

<!--
  GEN-TASK EXECUTION MANIFEST — HISTORICAL RUNTIME IMPLEMENTATION RECORD
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and task files by path and section — it does NOT copy game
  rules, formulas, magnitudes, schemas, or contracts.

  THIS TASK RECORDS COMPLETED WORK.
  The runtime implementation and verification tests for Sơn Thạch Vệ's once-per-battle
  Boss Passive contract have already been completed and verified.
  No new Product Owner decision was made by the runtime implementation.

  PROVENANCE: TASK-173 formalized the approved Product Owner Option B decision into
  authoritative documentation (PASSIVE_RULES.md §4, BOSS_RULES.md §6.2.4,
  DATABASE.md §1 note item 3, and GAME_STATE.md §2.4.2). The runtime defect in
  BattleStateService.cs was resolved by replacing the defective presence-based
  status-effect guard with a battle-session-scoped firing consumption record
  (_consumedBossPassiveFiring) evaluated on trigger and consumed after accepted commit.

  BOUNDARY: runtime execution in src/backend/GameServer.Application/Battle/BattleStateService.cs
  and verification test suite in tests/backend/GameServer.Application.Tests/Task173SonThachVeOncePerBattleFiringTests.cs.
  Zero schema changes. Zero migrations. Zero state members added. Zero API/SignalR changes.
-->

---

## Metadata

```text
Task ID:           TASK-174
Type:              FEATURE (TASK_TYPES.md §2 — implement runtime behavior for an authored contract; workflow development/feature.md)
Status:            DONE
Risk:              MEDIUM (touches battle-state resolution pipeline, step-18a Boss Passive trigger evaluation, and commit/retry path in BattleStateService.cs. Zero schema, migration, or BossState changes.)
Priority:          HIGH (closes the Sơn Thạch Vệ Passive retrigger defect and implements the approved once-per-battle contract without state schema pollution.)
Primary Agent:     gameplay (TASK_TYPES.md §5 — Boss domain runtime logic. Supporting: backend for Application service resolution wiring, review for contract conformance)
Supporting Agents: backend, review
Workflow:          development/feature.md
Skills:            gameplay/gameplay-behavior-derivation,
                   discovery/impact-analysis,
                   quality/architecture-conformance,
                   quality/scope-validation,
                   testing/test-scenario-generation
Dependencies:      TASK-173 (DONE — authoritative documentation amendment for Option B contract in PASSIVE_RULES.md §4, BOSS_RULES.md §6.2.4, DATABASE.md §1 note item 3, GAME_STATE.md §2.4.2)
Blocks:            None (runtime implementation is complete and verified)
Estimate:          Normal
```

---

## 1. Objective

Record the completed runtime implementation and verification of the approved Sơn Thạch Vệ once-per-battle Boss Passive contract in `BattleStateService.cs` and `Task173SonThachVeOncePerBattleFiringTests.cs`.

---

## 2. Authority

This implementation strictly implements the canonical contract authored in:
- `docs/01-game-design/PASSIVE_RULES.md` §4 ("Reset Behavior" — Boss Passives: `Persistent` governs firing eligibility)
- `docs/01-game-design/BOSS_RULES.md` §6.2.4 ("Sơn Thạch Vệ — Rage on an HP threshold")
- `docs/02-technical/DATABASE.md` §1 note item 3 (`Persistent` token mapping to `PassiveResetBehavior.NoReset`)
- `docs/02-technical/GAME_STATE.md` §2.4.2 ("Boss Passive" firing eligibility dispatch-level evaluation)
- `tasks/completed/TASK-173-apply-son-thach-ve-persistent-boss-passive-contract-to-authoritative-documentation.md`

**No new Product Owner decision was made by the runtime implementation.** All behavioral specifications and architectural constraints are derived directly from the approved canonical documentation.

---

## 3. Implementation Summary

The runtime implementation in `src/backend/GameServer.Application/Battle/BattleStateService.cs` introduces:

1. **Battle-Session Firing Tracking (`_consumedBossPassiveFiring`):**
   - Private field: `ConcurrentDictionary<(string BattleId, string PassiveId), bool> _consumedBossPassiveFiring`
   - Keyed strictly by `(BattleId, PassiveId)`, scoping consumption to the specific battle session.
   - Sits alongside existing per-battle session inputs (`_petConfiguration`, `_bossConfiguration`, `_relicConfiguration`).

2. **Eligibility Query (`IsBossPassiveFiringEligible`):**
   - Signature: `bool IsBossPassiveFiringEligible(string battleId, BossDefinition bossDefinition, PassiveId passiveId)`
   - Reads `bossDefinition.PassiveResetBehavior`: if not `PassiveResetBehavior.NoReset`, returns `true` (unaffected).
   - If `NoReset`, verifies `!_consumedBossPassiveFiring.ContainsKey((battleId, passiveId.Value))`.
   - Pure read query: does not mutate state or consume eligibility during evaluation.

3. **Post-Commit Consumption (`ConsumeOncePerBattleBossPassiveFiring`):**
   - Signature: `void ConsumeOncePerBattleBossPassiveFiring(string battleId, PassiveId? passiveId)`
   - Invoked only after the resolution write-back is accepted by `_repository.TryUpdateAsync`.
   - Records consumption via `_consumedBossPassiveFiring.TryAdd((battleId, activated.Value), true)`.

4. **Internal Resolution Carrier (`SwapResolution`):**
   - Internal record: `readonly record struct SwapResolution(SwapExecutionResult Result, PassiveId? ActivatedOncePerBattleBossPassive)`
   - Returned by `ResolveSwapAsync` to report if a once-per-battle Boss Passive was activated during that resolution attempt.
   - Ensures decoupling between deterministic state evaluation and side-effect consumption.

5. **Removal of Flawed Status-Effect Presence Guard:**
   - The former presence-based guard (`bossState.ActiveStatusEffects.Any(e => e.Id == "son-thach-ve-enrage")`) was completely removed.
   - Now guarded by `bossState.HP <= halfMaxHp && IsBossPassiveFiringEligible(battleId, bossDefinition, bossState.PassiveId)`.

---

## 4. Behavioral Contract

The implemented runtime behavior adheres to the exact specifications:

```text
Trigger:
  Boss HP <= 50% of MaxHP (evaluated against post-damage HP at Step 18a, inclusive ≤)

Effect:
  +20% ATK (Turn-based BuffDebuff with TargetStat = "ATK", Magnitude = 20)

Duration:
  3 Turns (decremented at step 19a boundary)

PassiveThreshold:
  null (non-match-charged; never accumulated via PassiveTracker.Charge)

Firing:
  At most once per battle

Expiry:
  When the 3-Turn effect expires at step 19a, firing eligibility remains consumed

Re-crossing:
  Leaving the threshold (> 50% HP) and re-entering (<= 50% HP) does not re-trigger

New Battle:
  Fresh battle ID starts with fresh eligibility; consumption in Battle A never affects Battle B
```

---

## 5. State Constraints

The implementation strictly maintains all repository state and schema boundaries:
- **Zero new `BossState` members:** No `HasActivated`, `EnrageFired`, or boolean flags added to `BossState`.
- **No `PassiveProgress.Current` repurposing:** `Current` remains 0 and is not overloaded as a firing marker.
- **Zero Redis schema changes:** No new Redis keys, hashes, or fields introduced.
- **Zero database schema changes:** No EF Core migrations, column additions, or table changes.
- **Zero new enum/token/vocabulary items:** Uses existing `PassiveResetBehavior.NoReset` and `"Persistent"` storage token.
- **Zero protocol changes:** No SignalR event or payload modifications.

---

## 6. Retry / Commit Ordering

Under `REDIS_STATE.md` §4 items 2–3 and 6, concurrency conflicts cause a refused sequence write followed by an automatic retry against fresh state.

The runtime implementation adheres strictly to the accepted-write ordering:
1. **Resolution Stage (`ResolveSwapAsync`):**
   - Reads `IsBossPassiveFiringEligible`.
   - If eligible and HP <= 50%, activates the effect and marks `ActivatedOncePerBattleBossPassive = "son-thach-ve-enrage"`.
   - Does NOT consume eligibility in the dictionary yet.
2. **Refused Write Path:**
   - If `_repository.TryUpdateAsync` returns `false`, the resolution is aborted.
   - Nothing was consumed in `_consumedBossPassiveFiring`.
   - The retry re-reads fresh state and re-evaluates the trigger; if still eligible, the activation can succeed.
3. **Accepted Write Path:**
   - Once `_repository.TryUpdateAsync` succeeds, `ConsumeOncePerBattleBossPassiveFiring` is executed.
   - The firing eligibility is permanently marked consumed for that `(BattleId, PassiveId)`.
   - Subsequent turns in that battle session cannot re-arm or re-trigger the Passive.

---

## 7. Acceptance Criteria

- [x] Approved TASK-173 contract is implemented.
- [x] `son-thach-ve-enrage` fires at HP <= 50%.
- [x] Exact 50% boundary works (`HP = 1500 / 3000`).
- [x] +20% ATK remains unchanged (`TargetStat = "ATK"`, `Magnitude = 20`).
- [x] 3-Turn duration remains unchanged (`RemainingTurns = 3` before step-19a decrement).
- [x] `PassiveThreshold = null` (non-match-charged).
- [x] At most one activation per battle.
- [x] Effect expiry does not restore eligibility.
- [x] Re-crossing threshold does not re-trigger.
- [x] New battle restores eligibility.
- [x] No new `BossState` member.
- [x] No `PassiveProgress.Current` repurposing.
- [x] Generic `NoReset` / match-charging semantics remain unchanged.
- [x] Kim Lôi Vương remains unchanged (`Player Combo >= 4`, default reset).
- [x] Existing Boss Passives remain unchanged (Hỏa Long, Thủy Ma, Mộc Yêu).
- [x] Tests pass (8/8 in dedicated suite, 110/110 targeted application tests, 146/146 targeted domain tests).
- [x] Build passes (0 errors).
- [x] No schema/migration/API/SignalR changes.

---

## 8. Verified Test Results

### Dedicated Once-Per-Battle Suite (`Task173SonThachVeOncePerBattleFiringTests.cs`)
All 8 facts pass:
1. `FirstActivation_WhenHpIsExactlyHalfMaxHp_ShouldApplyTheRageInstance` — verifies inclusive `≤` operator at exact 50% boundary (HP 1500 of 3000).
2. `FirstActivation_WhenHpIsBelowHalfMaxHp_ShouldApplyTheRageInstance` — verifies strictly below 50% activates independently of `State == Enraged`.
3. `WhileTheRageInstanceIsActive_ShouldNotApplyASecondInstance` — verifies sustained HP <= 50% does not refresh duration or stack magnitude while active.
4. `AfterTheEffectExpires_ShouldNotFireAgainWhileHpIsAtOrBelowHalf` — **CRITICAL REGRESSION TEST**: verifies Turn 4 (after Turn 3 expiry) with HP <= 50% does NOT re-fire.
5. `WhileHpRemainsAtOrBelowHalf_ShouldFireExactlyOnceAcrossManyTurns` — verifies sustained HP <= 50% across 10 Turns produces exactly 1 activation.
6. `AfterLeavingAndReEnteringTheThreshold_ShouldNotFireAgain` — verifies HP rising to 2600 and dropping back to 1500 does NOT re-fire.
7. `RefusedWriteAndRetry_ShouldStillActivateExactlyOnce` — verifies sequence conflict retry preserves eligibility until write commits, then consumes.
8. `NewBattle_ShouldStartWithFreshFiringEligibility` — verifies Battle A consumption does not consume Battle B eligibility on the same service.

### Targeted Suites
- `Task173SonThachVeOncePerBattleFiringTests`: 8 passed
- `Task172BossPassiveRuntimeTests`: 12 passed
- `BossPassiveEffectsTests`: 10 passed
- `BossResponseTests`: 24 passed
- `BattleStateServiceTests`: 56 passed
- `BossStateTests`: 56 passed
- `Task172BossDefinitionsContentTests`: 14 passed
- `PassiveTrackerTests`: 45 passed
- `EffectiveBossAttackTests`: 31 passed

### Full Backend Build
- `dotnet build src/backend/GameServer.sln`: 0 Errors.

---

## 9. Follow-Up Items (Out of Scope for TASK-174)

1. **Session Dictionary Cleanup:**
   - In-memory dictionaries (`_petConfiguration`, `_bossConfiguration`, `_relicConfiguration`, and `_consumedBossPassiveFiring`) in `BattleStateService.cs` are not evicted upon battle termination.
   - This is a pre-existing resource-management concern shared with all other per-battle configurations.
2. **Process Restart / Multi-Node Scalability:**
   - Firing eligibility is held in memory for the active battle session. Process restart or node failover during an active battle does not restore consumed state.
   - This matches existing `_bossConfiguration` architecture and respects the constraint forbidding persistent state additions.
