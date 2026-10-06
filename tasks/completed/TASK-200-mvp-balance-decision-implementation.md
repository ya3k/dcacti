# TASK-200 — MVP Balance Decision Implementation

```text
Task ID:           TASK-200
Type:              MVP Balance Decision Implementation
Status:            IMPLEMENTATION COMPLETE
Date:              2026-10-06
Governing Rules:   AGENTS.md, docs/01-game-design/BOSS_RULES.md v2.11
Governing Tasks:   TASK-191 (Balance Pass Specification),
                   TASK-198 (Balance Pass Decision Integration & Content Tuning Migration)
Decision Authority:Product Owner Explicit Approved Decisions (Q-1 through Q-12)
Production Change: Exactly 1 balance lever modified:
                   Mộc Yêu passive threshold: 5 matches -> 8 matches
Files Modified:    src/backend/GameServer.Domain/Bosses/BossDefinitions.cs
                   src/backend/GameServer.Infrastructure/Postgres/Migrations/20260926151112_ProvisionBossDefinitions.cs
                   docs/01-game-design/BOSS_RULES.md
                   tasks/backlog/TASK-191-balance-pass.md
                   tasks/completed/TASK-198-balance-pass-decision-integration.md
                   tests/backend/GameServer.Domain.Tests/BossStateTests.cs
                   tests/backend/GameServer.Infrastructure.Tests/BossPersistenceTests.cs
                   tests/backend/GameServer.Infrastructure.Tests/BossDefinitionPostgresProvisioningTests.cs
                   tests/backend/GameServer.Application.Tests/Balance/BalanceBaselineMatrix.cs
                   tests/backend/GameServer.Application.Tests/Balance/BalanceControlledExperimentDefinitions.cs
                   tests/backend/GameServer.Application.Tests/BossPassiveEffectsTests.cs
Test Coverage:     2,916 tests passing; 0 failed; 0 skipped (GameServer.sln)
```

---

## 1. Executive Summary

TASK-200 implements the explicit, authoritative Product Owner balance decisions across the MVP combat engine, completing the operational lifecycle that began with the TASK-191 Balance Pass specification.

Under strict adherence to `AGENTS.md` §7 ("Game Rule Protection"), §16 ("Task Discipline"), and §17 ("Documentation Change Rule"), **exactly one** production balance lever was authorized for modification:
- **Mộc Yêu Regeneration Threshold:** Every 5 player matches $\rightarrow$ **Every 8 player matches**.

All other 11 balance areas evaluated across TASK-191 through TASK-198 were formally approved by the Product Owner to **retain their current authored baseline values** without modification.

---

## 2. Product Owner Decision Ledger (Q-1 through Q-12)

Every question from `TASK-191` §6 has been answered with an explicit, approved decision:

| ID | Balance Area | Product Owner Approved Decision | Implementation Action | Production Delta |
|---|---|---|---|---|
| **Q-1** | Fight Duration Target | **QUALITATIVE / OBSERVATIONAL.** Qualitative and observational metric only. No artificial target or tolerance gate invented. | Preserved observational status in simulation harness. | None |
| **Q-2** | Power Charge Cost | **KEEP POWERCOST 0.** Retain authored `PowerCost = 0`. ADR-021 bounds abuse. | Preserved authored content in `CardDefinitions.cs`. | None |
| **Q-3** | Boss 1–5 Stats | **KEEP CURRENT BOSS 1–5 STATS.** Flat Boss 1–3 base stats (5000 HP, 100 ATK, 50 DEF) and mechanics-driven progression accepted. | Preserved `BossDefinitions.cs` stats. | None |
| **Q-4** | Card Cast Limit | **ONE CARD CAST PER COMMITTED TURN.** At most 1 successful card cast per committed Match-3 Turn (ADR-021). | Implemented in TASK-192 (`CardCastExecutor.cs`). Preserved. | None |
| **Q-5** | Heal / Shield Economy | **KEEP CURRENT VALUES, BOTH 20 POWER.** Heal & Shield pairing retained at 20 Power cost as authored. | Preserved `CardDefinitions.cs`. | None |
| **Q-6** | Pet Progression | **COSMETIC FOR MVP.** Pet Tier/Star/Level remain progression signals without combat stat scaling for MVP. | Preserved default pet combat stats. | None |
| **Q-7** | Player XP Pacing | **KEEP CURRENT 1 WIN -> 1 LEVEL.** Retain linear 1 win = 1 level progression without combat stat scaling. | Preserved `BattleResultService.cs`. | None |
| **Q-8** | Mộc Yêu Regeneration | **5% MAXHP EVERY 8 MATCHES.** Raise threshold from 5 to 8 matches. Retain 5% MaxHP magnitude (250 HP). Reject 2%/3% magnitude variants. | **UPDATED in production.** `BossDefinitions.MocYeu` threshold updated to 8. | **Threshold: 5 -> 8** |
| **Q-9** | Card Balance | **KEEP CURRENT AUTHORED VALUES.** Authored card damage, costs, and effects retained for MVP. | Preserved `CardDefinitions.cs`. | None |
| **Q-10**| Relic Economy | **KEEP CURRENT AUTHORED VALUES.** Authored relic values and triggers retained for MVP. | Preserved `RelicDefinitions.cs`. | None |
| **Q-11**| Pet Passives | **REMAIN UNIMPLEMENTED FOR MVP.** Xích Lang and Sơn Hùng passives remain unimplemented per MVP scope. | Preserved domain/application boundaries. | None |
| **Q-12**| Simulation Harness | **OPERATIONAL PREREQUISITE.** Approved test-only validation harness. | Maintained in `GameServer.Application.Tests/Balance`. | None |

---

## 3. Production Changes & Invariants Preserved

### 3.1 Domain Authority (`BossDefinitions.cs`)
In `src/backend/GameServer.Domain/Bosses/BossDefinitions.cs`:
```csharp
    public static readonly BossDefinition MocYeu = new(
        BossDefinitionId: "boss-def-moc-yeu",
        new BossId("boss-moc-yeu"),
        Element.Moc,
        // §6.2/§6.4: "Every 8 Player Matches" — a match-charged Passive (TASK-200).
        PassiveDefinition: new BossPassiveDefinition(
            new PassiveId("boss-moc-yeu-regen"), 8, "Default"),
```
- Passive threshold is updated from `5` to `8`.
- Passive ID remains `boss-moc-yeu-regen`.
- Reset behavior remains `Default` (clears to 0 on trigger).

### 3.2 Database Persistence Migration (`ProvisionBossDefinitions.cs`)
In `src/backend/GameServer.Infrastructure/Postgres/Migrations/20260926151112_ProvisionBossDefinitions.cs`:
The provisioned JSON payload for row `boss-def-moc-yeu` is updated:
```csharp
values: new object[] {
    "boss-def-moc-yeu",
    "boss-moc-yeu",
    0,
    "{\"passiveId\":\"boss-moc-yeu-regen\",\"threshold\":8,\"resetBehavior\":\"Default\"}",
    "{\"skillId\":\"root\",\"baseDamage\":100,\"chargeRequirement\":6,\"cooldownTurns\":2}"
}
```

### 3.3 Core Invariants Preserved
1. **Regeneration Magnitude:** Remains exactly `(bossState.MaxHP * 5) / 100` (`250 HP` at `MaxHP = 5000`). No configurable percentage was introduced.
2. **Integer Truncation & Overheal Clamping:** Truncated toward zero to an integer amount; clamped at `MaxHP` (`min(HP + 250, 5000)`).
3. **Execution Ordering:** Resolved authoritatively at Step 18a during Boss Response (`GAME_RULES.md` §17 step 18a).
4. **Terminal Resolution Precedence:** A lethal blow during player damage reduces Boss HP to 0 and ends the battle in `BattleWon` with no Boss Response; regeneration does not revive the Boss.
5. **Turn & Sequence Semantics:** Swaps advance Turn and Sequence by 1; rejected swaps advance neither.
6. **Server Authority & Determinism:** All state mutations occur server-side with zero client prediction reliance.

---

## 4. Authoritative Documentation Updates

### 4.1 `docs/01-game-design/BOSS_RULES.md`
- **Document Version:** Bumped to `2.11`.
- **MVP Boss Reference (§6):** Updated Mộc Yêu row to `Every 8 Player Matches → Regen HP`.
- **Boss Passive Details (§6.2):**
  - Updated Mộc Yêu row to `Every 8 Player Matches`.
  - Updated `PassiveThreshold` narrative: `Hỏa Long uses PassiveThreshold = 5 and Mộc Yêu uses PassiveThreshold = 8`.
- **Mộc Yêu Regeneration (§6.2.3):**
  - Documented trigger cadence: `PassiveThreshold = 8 (every 8 Player Matches, charged via PassiveTracker.Charge using default reset behavior; updated from 5 matches under Product Owner MVP balance decision TASK-200 / Q-8 in TASK-191)`.
  - Retained exact magnitude specification (`5% MaxHP`, `250 HP` at `MaxHP 5000`).

### 4.2 Task Decision Records
- **`tasks/backlog/TASK-191-balance-pass.md`:** Updated §6 Decisions Required table with the explicit resolutions for all questions Q-1 through Q-12, recording Q-8 as `APPROVED / IMPLEMENTED`.
- **`tasks/completed/TASK-198-balance-pass-decision-integration.md`:** Updated metadata and decision audit tables to transition status to `COMPLETE (Decisions Resolved & Implemented under TASK-200)` and reflect production integration.

---

## 5. Verification & Regression Testing

### 5.1 Unit & Integration Test Updates
1. `tests/backend/GameServer.Domain.Tests/BossStateTests.cs`:
   - Updated parameterized threshold test to assert threshold `8` for `boss-moc-yeu`.
2. `tests/backend/GameServer.Infrastructure.Tests/BossPersistenceTests.cs`:
   - Updated migration content assertions to check occurrence of `"threshold":5` (1 occurrence for Hỏa Long) and `"threshold":8` (1 occurrence for Mộc Yêu).
3. `tests/backend/GameServer.Infrastructure.Tests/BossDefinitionPostgresProvisioningTests.cs`:
   - Updated live PostgreSQL round-trip assertions to verify threshold `8` for Mộc Yêu.

### 5.2 Deterministic Mộc Yêu Regression Suite (`BossPassiveEffectsTests.cs`)
Seven comprehensive test scenarios were added to `tests/backend/GameServer.Application.Tests/BossPassiveEffectsTests.cs`:
1. `TASK200_MocYeuRegen_ShouldNotTriggerBeforeEightQualifyingMatches`: Proves that with 7 matches accumulated, no `PassiveTriggered` event is emitted and 0 HP regeneration occurs.
2. `TASK200_MocYeuRegen_ShouldTriggerAtExactThresholdOfEightMatches`: Proves that reaching exactly 8 matches emits `PassiveTriggered` and heals exactly 250 HP.
3. `TASK200_MocYeuRegen_Magnitude_RemainsExactlyFivePercentMaxHp`: Proves that regeneration magnitude strictly equals `(MaxHP * 5) / 100`.
4. `TASK200_MocYeuRegen_RepeatedThresholds_ShouldBehaveDeterministically`: Proves that across consecutive trigger cycles, progress resets to 0 and re-triggers deterministically.
5. `TASK200_MocYeuRegen_TurnAndSequenceSemantics_RemainUnchanged`: Proves that Turn and Sequence advance by exactly 1 on accepted swap.
6. `TASK200_MocYeuRegen_RejectedSwap_ShouldProduceZeroProgressAndZeroRegen`: Proves that invalid swaps produce `IsAccepted == false`, leaving Turn, Sequence, Boss HP, and passive progress unmodified.
7. `TASK200_MocYeuRegen_TerminalResolution_RemainsCorrect`: Proves that lethal player damage ending the battle immediately halts Boss Response and prevents regeneration from reviving the Boss.

### 5.3 Historical Simulation Reproducibility
- Historical baseline configuration for TASK-194 was preserved in `BalanceBaselineMatrix.cs` (configuring Mộc Yêu at threshold 5 for the baseline control run).
- Historical controlled experiment definitions for TASK-197 (`EXP-01`) were preserved in `BalanceControlledExperimentDefinitions.cs`, ensuring `baseline_5pct_5matches` (threshold 5) and `threshold_8matches` (threshold 8) remain fully reproducible.
- Historical simulation artifacts (`tasks/artifacts/TASK-194-baseline-simulation-results.json` and `tasks/artifacts/TASK-197-controlled-balance-experiments.json`) remain intact.

### 5.4 Test Suite Execution
Execution of `dotnet test src/backend/GameServer.sln`:
- `GameServer.Domain.Tests`: 1,558 passed, 0 failed, 0 skipped.
- `GameServer.Infrastructure.Tests`: 412 passed, 0 failed, 0 skipped.
- `GameServer.Api.Tests`: 323 passed, 0 failed, 0 skipped.
- `GameServer.Application.Tests`: 623 passed, 0 failed, 0 skipped.
- **Total: 2,916 passed, 0 failed, 0 skipped.**

---

## 6. Definition of Done Checklist

```text
[x] Product Owner explicit approved decisions recorded (Q-1 through Q-12)
[x] Exactly one production balance change implemented: Mộc Yêu threshold 5 -> 8
[x] 5% MaxHP magnitude hardcoded formula strictly preserved
[x] No configurable percentage introduced
[x] No other balance values modified in production
[x] Authoritative game rules documentation updated (docs/01-game-design/BOSS_RULES.md v2.11)
[x] Task decision records updated (TASK-191, TASK-198)
[x] Historical simulation harness artifacts preserved
[x] Deterministic unit and regression tests added/updated for threshold 8
[x] Complete solution test suite passing (2,916/2,916 green)
[x] TASK-200 completion record authored
```
