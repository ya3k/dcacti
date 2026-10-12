# TASK-261 — Atomic Round Orchestration & Redis State Persistence

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
-->

---

## Metadata

```text
Task ID:           TASK-261
Type:              GAMEPLAY-CHANGE
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: backend, testing, review
Workflow:          development/gameplay-change.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis,
                   quality/documentation-consistency, gameplay/authority-determinism-audit,
                   testing/test-scenario-generation
Dependencies:      TASK-258, TASK-259, TASK-260
Declared Files:    src/backend/GameServer.Application/Battle/BattleStateService.cs,
                   src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs,
                   src/backend/GameServer.Domain/Battle/Serialization/BattleStateSerializer.cs,
                   tests/backend/GameServer.Application.Tests/RoundTestSupport.cs,
                   tests/backend/GameServer.Application.Tests/AtomicRoundOrchestrationTests.cs,
                   tests/backend/GameServer.Application.Tests/BattleStateServiceTests.cs,
                   tests/backend/GameServer.Application.Tests/BossPassiveEffectsTests.cs,
                   tests/backend/GameServer.Application.Tests/BossResponseTests.cs,
                   tests/backend/GameServer.Application.Tests/BossSkillSecondaryEffectTests.cs,
                   tests/backend/GameServer.Application.Tests/BossSkillStep1CompositionTests.cs,
                   tests/backend/GameServer.Application.Tests/BattleResultTerminalFlowTests.cs,
                   tests/backend/GameServer.Application.Tests/RelicStageResolutionTests.cs,
                   tests/backend/GameServer.Application.Tests/RootAttackConsumptionTests.cs,
                   tests/backend/GameServer.Application.Tests/Task172BossPassiveRuntimeTests.cs,
                   tests/backend/GameServer.Application.Tests/Task173SonThachVeOncePerBattleFiringTests.cs,
                   tests/backend/GameServer.Application.Tests/Task177RelicFiringPointRuntimeTests.cs,
                   tests/backend/GameServer.Application.Tests/VictoryDefeatTests.cs,
                   tests/backend/GameServer.Application.Tests/Battle/BattleStateServiceCardCastTests.cs,
                   tests/backend/GameServer.Application.Tests/Battle/PowerChangedEmissionTests.cs,
                   tests/backend/GameServer.Api.Tests/BossResponseWireTests.cs,
                   tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs,
                   tests/backend/GameServer.Api.Tests/BattleHistoryEndpointTests.cs,
                   tests/backend/GameServer.Api.Tests/BattleResultEndpointTests.cs
```

---

## Objective

Integrate the TASK-260 Domain mechanics into the application-level battle lifecycle as **one atomic round**: the Player action phase, the Boss response phase on the same authoritative board, the step 19a Status Effect lifecycle, and the terminal checks, all resolved in local working state and persisted by **one** Redis compare-and-set write that advances `Turn` and `Sequence` exactly once.

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md` — the authoritative ADR (D1–D7)
- `docs/01-game-design/GAME_RULES.md` §1.3, §1.4, §2, §14, §15, §17 — shared board, atomic round, ATK gating, step 18a–18d
- `docs/01-game-design/COMBAT_RULES.md` §2, §3, §3.3, §3.4, §4, §5.3, §5.4, §5.5 — actor-relative resources, damage gating, Shield/Heal, ATK composition
- `docs/01-game-design/BOSS_RULES.md` §3, §6.2, §6.3, §6.3.1 — Boss acting on the board, SkillCharge from Boss POWER matches, the five non-damaging Skill effects
- `docs/01-game-design/MATCH3_RULES.md` §2, §8.1, §8.2 — one committed round advances `Turn`/`Sequence` once
- `docs/02-technical/GAME_STATE.md` §2.3.1, §2.4.3, §5, §5.1, §5.1.1 — state write-back, SkillCharge/cooldown, atomic round, step 19a
- `docs/02-technical/GAME_EVENTS.md` §1, §1.1, §2 — round event order
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.1 item 6 — CAS atomicity does not imply exactly-once delivery; recovery is ADR-008 snapshot resync
- `docs/02-technical/REDIS_STATE.md` §4 — the `Sequence` compare-and-set

---

## Scope

### In Scope
- Player phase: player Swap resolution, actor-relative resource effects, ATK-gated Player attack through `AttackResolution`, modifier preservation on non-qualifying turns, Boss terminal check before the Boss phase.
- Boss phase: deterministic Boss move selection and board resolution (`BossAiEngine`), actor-relative Boss DEF/HP/POWER effects, SkillCharge from Boss POWER match groups only (+1 per group, clamped), non-damaging Skill execution with all five secondary effects, ATK-gated Boss attack, removal of the legacy fixed basic-attack fallback.
- Removal of Burn DoT execution and ticking from the application lifecycle paths.
- Atomic commit: one `IBattleStateRepository.TryUpdateAsync` per round; `Turn`/`Sequence` advanced exactly once; no events for an uncommitted attempt; state unchanged when an exception precedes the commit.
- Idempotency and recovery: `actionId` replay recognition and stale-`expectedSequence` rejection preserved; the snapshot-recovery path left usable, including for the new trigger-based Boss modifier.
- Add/update application test coverage for the 14 required scenarios.

### Out of Scope
- The `SwapStarted` SignalR transport extension and Phaser presentation (TASK-262).
- Broad balance simulation, re-baselining archived balance evidence, arbitrary numeric tuning.
- Reopening PO decisions T1–T3 / D1–D7.
- Unrelated refactoring or schema changes.

---

## Preflight Findings

1. **TASK-260 is `IN REVIEW` and its changes are UNCOMMITTED in the working tree.** Its declared implementation files (Domain: `AttackResolution`, `StatusEffectLifecycle`, `StatusEffect`, `ResourceGenerator`, `BossAiEngine`, `BossDefinition(s)`, `SwapExecution`, `HealResolution`) are present as tracked modifications plus untracked new files, and its Domain test suite passes (1605/1605). Its APIs are compatible with the orchestration this task requires, so no parallel implementation was created.
2. **TASK-260 left the Application suite red (89 failures)** because it changed Domain content/behaviour without updating the Application layer or its tests: the application pipeline threw `Unhandled Boss Skill secondary effect kind 'AtkBuff'`, and many tests asserted the retired fallback basic attack, the per-player-match SkillCharge rate, the authored Skill base damage (150/250) and Burn DoT. Those failures are this task's subject matter; all gameplay suites are updated here.
3. **TASK-260's `StatusEffect.TriggerBased` relaxation broke snapshot recovery for a trigger-based `BuffDebuff`.** `BattleStateSerializer.FromStatusEffectJson` dropped `targetStat` on the trigger-based branch, so a committed state carrying `thunder-strike` could not be deserialized — which is exactly the ADR-008 recovery path TASK-261 must keep usable. Fixed here (one-line pass-through plus comment).
4. **Balance-evidence suites remain red by design of scope.** `Balance.Tests.BalanceBaselineMatrixTests`, `BalanceSimulationTests.H06` and `Balance.Tests.BalanceEvidenceMeasurementCorrectionTests` assert archived TASK-194/TASK-197 numeric evidence that the mechanics change invalidates; re-baselining is balance work, explicitly out of this task's scope. Reported, not touched.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-261:
```text
src/backend/GameServer.Application/Battle/BattleStateService.cs
src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs
src/backend/GameServer.Domain/Battle/Serialization/BattleStateSerializer.cs
tests/backend/GameServer.Application.Tests/RoundTestSupport.cs
tests/backend/GameServer.Application.Tests/AtomicRoundOrchestrationTests.cs
tests/backend/GameServer.Application.Tests/BattleStateServiceTests.cs
tests/backend/GameServer.Application.Tests/BossPassiveEffectsTests.cs
tests/backend/GameServer.Application.Tests/BossResponseTests.cs
tests/backend/GameServer.Application.Tests/BossSkillSecondaryEffectTests.cs
tests/backend/GameServer.Application.Tests/BossSkillStep1CompositionTests.cs
tests/backend/GameServer.Application.Tests/BattleResultTerminalFlowTests.cs
tests/backend/GameServer.Application.Tests/RelicStageResolutionTests.cs
tests/backend/GameServer.Application.Tests/RootAttackConsumptionTests.cs
tests/backend/GameServer.Application.Tests/Task172BossPassiveRuntimeTests.cs
tests/backend/GameServer.Application.Tests/Task173SonThachVeOncePerBattleFiringTests.cs
tests/backend/GameServer.Application.Tests/Task177RelicFiringPointRuntimeTests.cs
tests/backend/GameServer.Application.Tests/VictoryDefeatTests.cs
tests/backend/GameServer.Application.Tests/Battle/BattleStateServiceCardCastTests.cs
tests/backend/GameServer.Application.Tests/Battle/PowerChangedEmissionTests.cs
tests/backend/GameServer.Api.Tests/BossResponseWireTests.cs
tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs
tests/backend/GameServer.Api.Tests/BattleHistoryEndpointTests.cs
tests/backend/GameServer.Api.Tests/BattleResultEndpointTests.cs
```

*(Note: `src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs` is SHARED with TASK-260 — TASK-261 adds `ApplyTriggerBased` to it. Per TASK_LIFECYCLE.md §6.1 step 5 / P-2 this slice therefore cannot be committed as an independent Phase A commit while TASK-260's own declared files are uncommitted; the two must be committed together as a joint group commit, or TASK-260 committed first. The task record itself is excluded from the implementation declared file set.)*

---

## Acceptance Criteria

- [x] The Player phase resolves the submitted Swap through the existing validation/match pipeline, applies actor-relative resource effects, and calls the Domain attack resolution only for a qualifying ATK match, preserving attack modifiers otherwise.
- [x] The Boss phase selects and resolves a legal Boss swap on the authoritative board, applies Boss-relative DEF Shield / HP healing, and charges `SkillCharge` only from Boss POWER match groups (+1 each, clamped).
- [x] Player POWER matches never charge the Boss.
- [x] Boss damage occurs only for a qualifying ATK match; the legacy fixed basic-attack fallback is removed.
- [x] All five Boss Skill secondary effects are applied through the new Domain definitions, with zero direct Skill damage.
- [x] Burn is no longer applied or executed by any Boss Skill path (no MVP Boss Skill declares a DoT), with every unrelated Status Effect left untouched. **Amended from the brief:** the generic step 19a damage-over-time tick is RETAINED, because `GAME_RULES.md` §17 step 19a and `COMBAT_RULES.md` §5.2 item 3 require it and an MVP Card still applies a Burn DoT — see Remaining Issues §10.
- [x] The complete round is persisted by one successful Redis CAS write; `Turn` and `Sequence` advance exactly once per committed round.
- [x] A failed CAS publishes no events for an uncommitted state; an exception before persistence leaves the authoritative state unchanged; retry bounds and concurrency semantics are preserved (the 8-attempt bound is unchanged).
- [x] `actionId` duplicate recognition and stale-`expectedSequence` rejection behave as before; a duplicate does not re-execute the round.
- [x] The existing snapshot-recovery path obtains the authoritative committed state, including the new trigger-based Boss modifier (which required the `BattleStateSerializer` fix recorded above).
- [x] The 14 required application/integration scenarios are covered by tests (`AtomicRoundOrchestrationTests`, 23 tests).
- [x] `SwapStarted` transport and Phaser presentation NOT implemented here (out of scope, TASK-262).

---

## Current State

`BattleStateService.ResolveSwapAsync` resolved one Player Swap and then ran the Boss Response in the same method; the Player attack ran unconditionally through `DamagePipeline.Calculate`, the Boss attack fell back to `Base Damage = Boss.ATK`, `SkillCharge` incremented by the Player's Match total, the Skill applied Burn/Root/DrainPower from `BossSkillSecondaryEffectKind`, and step 19a ticked Burn DoT before decrementing durations. TASK-260 added the Domain APIs (`AttackResolution`, `BossAiEngine`, actor-relative `ResourceGenerator` helpers, `ApplyDamageShieldAbsorption`, `ConsumeNextQualifyingAttackModifiers`, the five `BossSkillSecondaryEffectKind` members) without wiring them into this method.

---

## Implementation Notes

- Phase order follows `GAME_RULES.md` §17 literally: Player steps 1–17, Boss 18a (board move) → 18b (Passive) → 18c (Skill) → 18d (ATK-gated attack), then step 19a, then the caller's single write-back.
- The Boss phase deliberately writes neither `Turn`/`Sequence` (advanced once by the Player action) nor `LastCommittedSwapPair` (the client action's identity, ADR-025) nor `Combo`/`MatchCount` (Player-Match counters read by `BOSS_RULES.md` §6.2/§6.2.5 triggers).
- `StatusEffectLifecycle.ApplyTriggerBased` is new: `Apply` requires a Turn countdown and `ApplyShield` requires the Shield type, so the trigger-based `NextQualifyingAttack` modifier had no apply/refresh operation to reuse.

---

## Testing Requirements

### Required Verification
```text
[x] Unit/integration tests — tests/backend/GameServer.Application.Tests/AtomicRoundOrchestrationTests.cs
[ ] Integration tests     — GameServer.Api.Tests battle/round suites
[ ] Gameplay scenarios    — the 14 required scenarios of the task brief
```

### Key Edge Cases
- `GAME_RULES.md` §17 step 18d and `COMBAT_RULES.md` §3.3 item 8 — the ATK gate and modifier preservation
- `BOSS_RULES.md` §6.3 and ADR-026 T1/T2 — +1 per Boss POWER match group, clamp at `ChargeRequirement`
- `GAME_STATE.md` §5.1.1 items 4–7 — step 19a decrement, removal at 0, trigger-based instances untouched
- `REDIS_STATE.md` §4 items 2–3, 7 — lost CAS writes nothing; a rejected action writes nothing

---

## Stop Conditions

- If required behaviour cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If the task requires re-deciding a T1–T3 / D1–D7 Product Owner decision: STOP and escalate
- If a required fix would change a persisted contract or schema: STOP and escalate

---

## Completion Evidence

### Commit
- Implementation slice (Phase A): `7cd07821eb613e26be39d0f19d3ef5be70ab37dc` (Joint Group Commit with TASK-260)
- Subject: `feat(combat): implement domain mechanics, atk gating, and atomic round orchestration`
- Owns: `TASK-260`, `TASK-261`
- Files: the declared file set above
- Shared with: `TASK-260` — `src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs`
- Unowned / pre-existing: `src/backend/GameServer.Domain/Battle/Serialization/BattleStateSerializer.cs` (TASK-260 changed the Domain factory it calls but did not declare or update this file)
- Completion record filing (Phase B): this completion record alone, filed under its own bookkeeping commit (`tasks/completed/TASK-261-atomic-round-orchestration-and-redis-state-persistence.md`).

### Changed Files
- `src/backend/GameServer.Application/Battle/BattleStateService.cs` — one atomic round: ATK-gated Player attack via `AttackResolution`; Boss board action (18a) via `BossAiEngine` + `CascadeResolver` + `ResourceGenerator.ApplyActorMatchEffects`; Boss Passive 18b; non-damaging Skill 18c with the five effects; ATK-gated Boss attack 18d; Burn DoT execution and ticking removed; dead helpers removed.
- `src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs` — added `ApplyTriggerBased` (shared with TASK-260).
- `src/backend/GameServer.Domain/Battle/Serialization/BattleStateSerializer.cs` — trigger-based elements now carry `targetStat` on deserialization (snapshot recovery).
- `tests/backend/GameServer.Application.Tests/RoundTestSupport.cs` — new self-validating pinned-board fixture and event-list readings.
- `tests/backend/GameServer.Application.Tests/AtomicRoundOrchestrationTests.cs` — new suite for the 14 required scenarios.
- The remaining declared Application test files — updated from the retired contract to the ADR-026 contract (retired subjects: the fixed fallback basic attack, SkillCharge per Player match, authored Skill base damage, "one generated Swap always damages both sides", "ComboChanged is one run", Burn applied by a Boss skill).
- `tests/backend/GameServer.Api.Tests/BossResponseWireTests.cs` — the boss-skill-cast / boss-damage / BattleWon / BattleLost wire scenarios re-anchored on the ATK gate and on a pinned SkillCharge; the multi-round helpers now use one action token per round and skip the committed pair.
- `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs` — the round-order test now compares the delivered batch against an independent resolution of an identically seeded control battle instead of re-deriving the pipeline; the ComboChanged test splits the batch's per-cycle runs; the schema test asserts the damage trio's internal consistency rather than a guaranteed instance.
- `tests/backend/GameServer.Api.Tests/BattleHistoryEndpointTests.cs` / `BattleResultEndpointTests.cs` — the terminal-outcome fixtures play the round loop until the documented terminal event lands (ADR-026 D1 makes no single Swap terminal), with fixtures that cannot end the other way.

### Validation Results
- `dotnet build src/backend/GameServer.sln` — **PASS** (0 errors)
- `dotnet test tests/backend/GameServer.Domain.Tests/GameServer.Domain.Tests.csproj` — **PASS** (1605 passed, 0 failed)
- `dotnet test tests/backend/GameServer.Application.Tests/GameServer.Application.Tests.csproj` — **684 passed, 10 failed, 0 skipped / 694**. All 10 failures are the balance-evidence suites listed in Remaining Issues §2; every gameplay, orchestration, persistence and terminal-outcome suite is green, including the new `AtomicRoundOrchestrationTests` (23 tests) and the four Burn/DoT suites that the retained step 19a tick keeps valid.
- `dotnet test tests/backend/GameServer.Api.Tests/GameServer.Api.Tests.csproj --filter "FullyQualifiedName!~RedisBattleStateSmokeTest&FullyQualifiedName!~RedisBattleRecoverySmokeTest&FullyQualifiedName!~BattleResultSmokeTest"` — **PASS** (356 passed, 0 failed), re-run 5 consecutive times with 0 failures after the last edit (the API host seeds from server entropy, so the stability re-run matters).
- Not run (environment): the Redis/PostgreSQL smoke suites and `GameServer.Infrastructure.Tests` — see "Environment Limitation".

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (the round is computed in Domain + Application only; the wire layer projects)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed P-1…P-6 commit policy compliance — resolved via authorized Joint Group Commit with TASK-260

### Resolution of Former Blocker
The Phase A blocker concerning uncommitted TASK-260 dependencies and shared `StatusEffectLifecycle.cs` was resolved under Product Owner authorization via Joint Group Commit (`7cd07821eb613e26be39d0f19d3ef5be70ab37dc`) in strict compliance with `TASK_LIFECYCLE.md` §6.1 step 5 / P-2.

### Environment Limitation (test execution)
No Docker daemon and no Redis/PostgreSQL are available in the execution environment, so the suites that require them could not be executed here and were excluded by filter rather than reported as passing:

- `GameServer.Api.Tests.RedisBattleStateSmokeTest`, `RedisBattleRecoverySmokeTest`, `BattleResultSmokeTest` (need a live Redis and, for the last, PostgreSQL)
- `GameServer.Infrastructure.Tests` PostgreSQL and Redis suites

The Api suites that substitute both connection strings with in-memory doubles were run, as was `GameServer.Domain.Tests` and the whole `GameServer.Application.Tests`.

### Incident Note (unrelated evidence artifacts)
Running the balance-evidence suites rewrites `tasks/artifacts/TASK-194-baseline-simulation-results.json` and `tasks/artifacts/TASK-197-controlled-control-experiments.json` as a test side effect. Those files are outside this task's declared set and were restored to `HEAD` with `git checkout --` after the test runs. Reported (not fixed): a suite that writes the evidence it asserts on cannot be run safely against a dirty tree.

---

## Remaining Issues (reported, not fixed here)

### Blocking TASK-261's own completion
1. **Phase A commit blocker — RESOLVED:** The Joint Group Commit with TASK-260 was authorized by the Product Owner and committed under SHA `7cd07821eb613e26be39d0f19d3ef5be70ab37dc`.
2. **Balance-evidence suites are red by construction of scope:** `Balance.Tests.BalanceBaselineMatrixTests` (TASK-194 evidence), `Balance.Tests.BalanceSimulatorTests.H06`, and `Balance.Tests.BalanceEvidenceMeasurementCorrectionTests` assert archived numeric evidence that the mechanics change invalidates. Re-baselining balance evidence is explicitly deferred to downstream balance simulation work per PO Decision 4.

### Documentation / comment inconsistencies found (outside this task's declared scope)
3. `docs/01-game-design/BOSS_RULES.md` §6's MVP summary table (lines ~275–279) and §6.2.1's "Flame Burst's 150 remains 150" row still state the retired Skill damage and the removed Burn DoT, contradicting §6.3/§6.3.1 and ADR-026 D3/D5. TASK-259's declared scope owns this file.
4. `ADR-026` §8 requires the deadlock reshuffle to emit `BoardReshuffled`; `GAME_EVENTS.md` §1/§2 (the event list's owner, higher precedence per `AGENTS.md` §2) defines no such event and `BattleEventType` has no such member. The reshuffle is implemented and committed as state; no event is emitted. Either the ADR overstates or the protocol needs the event (a protocol change belongs to TASK-262).
5. `src/backend/GameServer.Domain/Match3/BattleEvent.cs` — `BattleEventType.BossSkillCast`'s and `BossSkillCastEvent`'s XML docs still say a cast "precedes the damage instance the Skill produced"; under ADR-026 D3 no Skill has a damage instance.
6. `src/backend/GameServer.Domain/Bosses/BossDefinition.cs` — `PassiveThreshold`'s doc still calls `0` the "Always-Active marker" and cites Thủy Ma's retired "always active" trigger, while `BOSS_RULES.md` §6.2/§6.2.2 now say "Battle Start" and storage uses `null` for "no match-charging threshold".
7. `src/backend/GameServer.Domain/Battle/EffectivePetATK.cs` — `IsAtkStatModifier`'s doc states "A trigger-based instance carries no `TargetStat` at all … so it can never be selected", which ADR-026 D7's trigger-based `NextQualifyingAttack` ATK modifier contradicts (the predicate does select it, which is what D7 requires).
8. `docs/02-technical/GAME_EVENTS.md` §2's `PowerChanged` row ("Delta 0 where the cap or the floor absorbed the whole change") versus §2 item 4's "a mutation that does not occur emits nothing": the implementation emits no event when a drain is fully absorbed. A doc clarification, not a code change.

### Behavioural gaps noted, deliberately not implemented here
9. **`GAME_RULES.md` §17 step 18a conditions the Boss's move on "alive and not Stunned".** No MVP content applies `BossStateKind.Stunned`, and no resolution code reads it, so the guard is unreachable today. Implementing it would be speculative code for an unreachable state (`AGENTS.md` §9); it needs either an MVP Stun source or a doc correction.
10. **The step 19a damage-over-time tick is RETAINED, not removed** (a deliberate correction of this task's brief in favour of the documents): `GAME_RULES.md` §17 step 19a requires "tick each active damage-over-time Status Effect (e.g. Burn) exactly once, through the Damage Pipeline" and states the ADR-026 change "does not change the damage-over-time tick itself"; `COMBAT_RULES.md` §5.2 item 3 and §5.3 own it; and an MVP Card still applies a Pet-owned Burn DoT to the Boss (`CardCastExecutor`'s `CardEffectType.Burn`) with the Burning Curse Relic's `BurnDamage` modifier scaling its ticks. What ADR-026 D3/D5 retire is the **Boss Skill's** Burn, and no MVP Boss Skill declares one any more. Report per `AGENTS.md` §4/§20 for confirmation.
11. `BattleState.Combo` is left as the Player action's counter (a Boss-Match `ComboChanged` is emitted for the Boss's own cycle by `BattleEventBuilder`, but the authoritative `Combo`/`MatchCount` are the Player's, which `BOSS_RULES.md` §6.2/§6.2.5 triggers read). A client that renders the authoritative `Combo` state and a client that folds `ComboChanged` events will disagree during the Boss phase — a presentation concern for TASK-262.

### Environment
12. No Docker/Redis/PostgreSQL in the execution environment: the Api Redis/Postgres smoke suites and the whole `GameServer.Infrastructure.Tests` Postgres/Redis suites could not be run (excluded by filter, not reported as passing).