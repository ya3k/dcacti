# TASK-304 — Boss Power Slice 1: Implementation and Verification

<!--
  GEN-TASK EXECUTION MANIFEST — STATUS: DONE

  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  Resumed from BLOCKED following explicit operator authorization of U-1 Option A:
  use the existing Player Power per-Gem output unchanged for Boss Power generation
  (+10 Power per uniquely matched POWER cell, BaseOutput(POWER) × unique matched
  POWER count, clamped to [0, 100], actor-relative attribution).
  Slice 1 implementation, tests, and two-stage commit protocol are complete.
-->

---

## Metadata

```text
Task ID:           TASK-304
Type:              FEATURE
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: backend, realtime, persistence, client, testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis,
                   gameplay/gameplay-behavior-derivation,
                   gameplay/authority-determinism-audit,
                   realtime/realtime-protocol-validation,
                   quality/scope-validation, quality/implementation-review
Dependencies:      TASK-303 (COMPLETED — authorized ADR-029 and the documented
                   target state), ADR-029 (Accepted), TASK-258 §2 D2
                   (superseded by ADR-029)
Declared Files:    docs/01-game-design/BOSS_RULES.md
                   docs/01-game-design/COMBAT_RULES.md
                   docs/01-game-design/GAME_RULES.md
                   docs/02-technical/GAME_STATE.md
                   docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md
                   src/backend/GameServer.Api/Controllers/BattleStartResponse.cs
                   src/backend/GameServer.Api/Controllers/BattleStartStateSummary.cs
                   src/backend/GameServer.Api/Hubs/BattleHub.cs
                   src/backend/GameServer.Domain/Battle/BossState.cs
                   src/backend/GameServer.Domain/Battle/Serialization/BattleStateJson.cs
                   src/backend/GameServer.Domain/Battle/Serialization/BattleStateSerializer.cs
                   src/backend/GameServer.Domain/Match3/ResourceGenerator.cs
                   src/frontend/client/src/game/runtime/GameRuntime.ts
                   src/frontend/client/src/game/runtime/GameRuntimeEvents.ts
                   src/frontend/client/src/game/scenes/BattleScene.ts
                   src/frontend/client/src/services/api/BattleModels.ts
                   src/frontend/client/src/services/realtime/SignalRService.ts
                   src/frontend/client/tests/BattleService.test.ts
                   src/frontend/client/tests/GameRuntime.test.ts
                   src/frontend/client/tests/SceneLifecycle.test.ts
                   src/frontend/client/tests/SignalRService.test.ts
                   tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs
                   tests/backend/GameServer.Api.Tests/Hubs/BattleHubReconnectRecoveryTests.cs
                   tests/backend/GameServer.Domain.Tests/ActorRelativeMatchEffectsTests.cs
                   tests/backend/GameServer.Domain.Tests/BattleStateSerializationTests.cs
                   tests/backend/GameServer.Domain.Tests/BossStateTests.cs
```

**Lifecycle path executed:**

```text
BACKLOG -> READY -> IN PROGRESS -> BLOCKED -> IN PROGRESS -> IN REVIEW -> DONE
```

`BACKLOG -> READY` was claimed on the documented readiness basis (scope confirmed,
docs exist, task type and workflow assigned). The stop condition fired during
`core/context-discovery.md` §3's conflict check regarding unresolved decision U-1
in ADR-029 §3. Work was halted and filed under `tasks/blocked/`.
On operator resolution authorizing U-1 Option A, the task transitioned back to
`IN PROGRESS` per `tasks/TASK_LIFECYCLE.md` §3, moved to `tasks/active/`, and the
canonical declared file set was populated prior to modifying any implementation files.
Following implementation and passing validation suites, the task transitioned through
`IN REVIEW` and to `DONE` via the two-stage commit protocol (`TASK_LIFECYCLE.md` §6).

---

## Objective

Implement Slice 1 of the approved Boss-as-Player-Opponent architecture recorded in
ADR-029: introduce a real `BossState.Power` resource (integer 0–100), synchronize
it through the authoritative server-state lifecycle, expose it through the
existing Boss state projection as `bossState.power` (and REST `BattleStartBossState`),
render it in the HUD, and restore it correctly after reconnect. Boss Power only —
no Boss AI, no Boss Card execution, no Skill migration, no `SkillCharge` retirement.

---

## 1. Blocker Resolution (U-1 Decision)

The operator authorized **U-1 Option A: use the existing Player Power per-Gem output
unchanged for Boss Power generation**.

- **Grant basis:** `BaseOutput(POWER) × number of uniquely matched POWER cells`.
- **Current output:** `+10 Power per uniquely matched POWER Gem`, using the existing
  Player Power rate table (`COMBAT_RULES.md` §2).
- **Actor attribution:** Power is granted only to the actor whose eligible match
  generated it. Boss Power is not granted from Player matches, and Player Power is not
  granted from Boss matches.
- **Range:** Clamp the resulting Power to the inclusive interval `[0, 100]`.
- **State ownership:** Keep `PetState.Power` and `BossState.Power` separate.
- **Legacy behavior:** Preserve `SkillCharge` generation and existing Boss Skill
  behavior in this slice.
- **Unresolved balance choices U-2 through U-6 remain unresolved:** Per-ability Power
  costs (U-2), Boss Card cast-priority weights (U-3), outstanding ability magnitudes
  (U-4), migrated ability cooldown tuning (U-5), and `SkillCharge` retirement timing (U-6).
- **Explicitly out of scope:** Boss AI, Boss Card execution, ability migration,
  `BossCardCast` emission/presentation, `SkillCharge` retirement, and the fifth Gem.

---

## 2. Preflight Baseline (verified facts at HEAD)

```text
Repository:                E:\dcacti
Branch:                    master
HEAD:                      e8a1cb87e002eaa47e6f09006e20a3d950a7c485
HEAD subject:              docs: file the boss power and boss card decision reconciliation record
Working tree at pickup:    clean (prior untracked blocked record removed on resumption)
Task registry at pickup:   backlog/ 0, active/ 1 (this task), blocked/ 0,
                           completed/ 305 TASK-*.md records
Highest accepted ADR:      ADR-029 (29 ADR files)
Date:                      2026-10-18
```

---

## 3. Required Reading Completed

Read in full or in the cited sections before any edit was attempted:

```text
AGENTS.md (root)                                all sections
docs/AGENTS.md (nested)                         all sections
CLAUDE.md                                       orientation
tasks/TASK_LIFECYCLE.md                         §1–§6 (states, transitions,
                                                two-stage commit protocol)
tasks/README.md                                 §1–§13
tasks/TASK_TYPES.md                             §1–§5 (type selection, risk)
tasks/TASK_TEMPLATE.md                          all sections
.ai/README.md                                   §6, §13, §15, §16, §18
.ai/workflow/core/completion.md                 §1–§3 (DoD, final report format)
tasks/completed/TASK-303-…-reconciliation.md    §1–§13 (the authorizing record)
docs/03-decisions/ADR/ADR-029-…md               all sections
docs/03-decisions/ADR/ADR-026-…md               amended POWER bullet + unresolved note
docs/00-overview/MVP_SCOPE.md                   §1, §3
docs/01-game-design/GAME_RULES.md               §12, §15, §16, §17 step 18
docs/01-game-design/BOSS_RULES.md               §1, §4.1–§4.4, §6.3
docs/01-game-design/COMBAT_RULES.md             §1.2, §2, §5.4.6, §5.5.6, §6
docs/02-technical/GAME_STATE.md                 §2.3, §2.4, §2.4.1, §2.4.3, §5.1
docs/02-technical/GAME_EVENTS.md                §1, §2
docs/02-technical/SIGNALR_PROTOCOL.md           §3.2.2, §4.3 item 15, §4.4 item 12, §7
docs/02-technical/REDIS_STATE.md                §2.1 (Boss Power member)
docs/02-technical/ARCHITECTURE.md               §2.1, §2.2.2, §3
```

---

## 4. Implementation Delivered

1. **Documentation Reconciliation:**
   - `ADR-029`: Updated §3 U-1 row, §4 Slice 1, §6 Negative to record Option A approval (+10 Power per uniquely matched POWER cell, actor-relative attribution, clamped to [0, 100]). Preserved target-state/runtime-divergence warning.
   - `BOSS_RULES.md`: Updated §4.1 item 7 to resolve U-1 while noting future cadence and per-ability costs (U-2) remain open.
   - `COMBAT_RULES.md`: Updated §2 POWER bullet tail to record U-1 resolution under Option A.
   - `GAME_STATE.md`: Updated §2.4.3 item 8 to record U-1 resolution under Option A.
   - `GAME_RULES.md`: Updated version 3.11 note to record U-1 resolution under TASK-304.
2. **Boss State Domain Member:**
   - `BossState.cs`: Added `int Power` parameter, initialized to 0 in `BossState.Initial()`, added to `Equals` and `GetHashCode`.
   - `ResourceGenerator.cs`: Implemented `ApplyBossPower(BossState previous, int powerDelta)` with clamping to `[0, 100]`, and wired it into `ApplyActorMatchEffects` Boss branch alongside legacy `ApplyBossSkillCharge`.
3. **Serialization & Persistence Compatibility:**
   - `BattleStateJsonNames.cs`: Added `BossPower = "power"`.
   - `BattleStateJson.cs`: Added non-required `int Power` property to `BossStateJson` with default 0, guaranteeing backward compatibility for legacy persisted records without schema versioning or migration.
   - `BattleStateSerializer.cs`: Mapped `Power` in `ToBossStateJson` and `FromBossStateJson`.
4. **Authoritative Projections:**
   - `BattleHub.cs`: Added `power` to `BossStatePayload` and mapped from `BossState.Power` in `ToPayload`.
   - `BattleStartResponse.cs`: Added `Power` to `BattleStartBossState`.
   - `BattleStartStateSummary.cs`: Mapped `Power: bossState.Power` in `ToBossState`.
5. **Frontend Client & Presentation:**
   - `BattleModels.ts`: Added `readonly power: number` to `BattleStartBossState`.
   - `SignalRService.ts`: Added `readonly power: number` to `BossStatePayload`.
   - `GameRuntimeEvents.ts`: Added `readonly power: number` to `RuntimeBossState`.
   - `GameRuntime.ts`: Extended `readBossState` to validate and ingest `power`.
   - `BattleScene.ts`: Added Boss Power label, gauge (`POWER_GAUGE_COLOR`), and text readout (`<power> / 100`), presented in `renderBossHud` and `settleHeldPresentation`, surviving reconnect snapshots and initial join state.

---

## 5. Scope Verification

```text
Boss AI decision policy                       not implemented
Boss Card execution / cost validation         not implemented
Ability migration (5 abilities)                not implemented
SkillCharge retirement / legacy handler change not implemented
BossCardCast emission/presentation             not implemented
Fifth Gem / Power-drain Gem                    not implemented
Boss Card persistence / DATABASE.md change     not implemented
Encounter / Relic / balance changes            not implemented
Player Power behavior                          untouched
```

---

## 6. Declared File Set (P-2 / P-5)

```text
docs/01-game-design/BOSS_RULES.md
docs/01-game-design/COMBAT_RULES.md
docs/01-game-design/GAME_RULES.md
docs/02-technical/GAME_STATE.md
docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md
src/backend/GameServer.Api/Controllers/BattleStartResponse.cs
src/backend/GameServer.Api/Controllers/BattleStartStateSummary.cs
src/backend/GameServer.Api/Hubs/BattleHub.cs
src/backend/GameServer.Domain/Battle/BossState.cs
src/backend/GameServer.Domain/Battle/Serialization/BattleStateJson.cs
src/backend/GameServer.Domain/Battle/Serialization/BattleStateSerializer.cs
src/backend/GameServer.Domain/Match3/ResourceGenerator.cs
src/frontend/client/src/game/runtime/GameRuntime.ts
src/frontend/client/src/game/runtime/GameRuntimeEvents.ts
src/frontend/client/src/game/scenes/BattleScene.ts
src/frontend/client/src/services/api/BattleModels.ts
src/frontend/client/src/services/realtime/SignalRService.ts
src/frontend/client/tests/BattleService.test.ts
src/frontend/client/tests/GameRuntime.test.ts
src/frontend/client/tests/SceneLifecycle.test.ts
src/frontend/client/tests/SignalRService.test.ts
tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs
tests/backend/GameServer.Api.Tests/Hubs/BattleHubReconnectRecoveryTests.cs
tests/backend/GameServer.Domain.Tests/ActorRelativeMatchEffectsTests.cs
tests/backend/GameServer.Domain.Tests/BattleStateSerializationTests.cs
tests/backend/GameServer.Domain.Tests/BossStateTests.cs
```

---

## 7. Acceptance Criteria

- [x] ADR-029, GAME_RULES.md, COMBAT_RULES.md, BOSS_RULES.md, and GAME_STATE.md reconciled documentation-first for U-1 Option A.
- [x] `BossState.Power` initialized to 0 at battle creation, clamped to [0, 100].
- [x] Unique matched POWER cells attributed to the Boss grant +10 Power to the Boss.
- [x] Player matches grant Power only to Player; Boss matches grant Power only to Boss.
- [x] Non-POWER matches grant no Power.
- [x] Player Power and legacy `SkillCharge` generation remain unchanged.
- [x] `BossState.Power` survives cloning, copying, and `BattleStateSerializer` JSON serialization round trip.
- [x] Pre-deploy/legacy battle state without `power` deserializes with `Power = 0`.
- [x] Existing atomic CAS / Redis update lifecycle preserves `BossState.Power`.
- [x] SignalR `bossState.power` projection delivers the authoritative value on state update and reconnect snapshot.
- [x] REST `BattleStartBossState` includes `power`.
- [x] Frontend client models and `readBossState` parser ingest `bossState.power`.
- [x] HUD displays Boss Power gauge and text (`<power> / 100`) using established visual conventions.
- [x] All unit, integration, and regression tests pass for backend and frontend.

---

## 8. Completion Evidence

### 8.1 Commit (Phase A Implementation Slice)

```text
commit:  de7888743818bc48e84fc0af400ae40f6eca47dc
subject: feat: implement boss power state, actor-relative generation, and hud projection
owns:    TASK-304
files:   26 files declared and staged (exact match, verified via P-5)
shared:  none
unowned: none
```

### 8.2 Validation Executed

```text
Command                                                      Result
-----------------------------------------------------------  ----------------------
dotnet test ... --filter ActorRelativeMatchEffectsTests       18 passed (47 ms)
dotnet test ... --filter BossStateTests                      59 passed (60 ms)
dotnet test ... --filter BattleStateSerializationTests       85 passed (180 ms)
dotnet test ... --filter BossResponseTests|AtomicRound...    59 passed (173 ms)
dotnet test ... --filter ApiIntegrationTests|BattleHub...   135 passed (12 s)
dotnet test ... --filter RedisBattleStateRepositoryTests     19 passed (342 ms)
npm run test:run (vitest)                                    1265 passed, 24 test files
npm run build (tsc && vite build)                            Built successfully (2.49s)
git diff --check                                             Clean (0 errors)
```
