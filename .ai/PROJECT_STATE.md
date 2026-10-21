# .ai/PROJECT_STATE.md — Project State Snapshot

**Version:** 1.9

**Notice:** This document is an AI navigation cache, not a source of truth. It records compact, verifiable project status to prevent repetitive whole-repository audits. Authoritative sources remain `docs/` (rules/architecture), `AGENTS.md` (contract), and `tasks/` (lifecycle state).

---

## 1. Audit Metadata

| Property | Value |
| :--- | :--- |
| **Audited Branch** | `master` |
| **Audited HEAD SHA** | `c6e2c918ee0851ecab58c56ef2ebcf3ae9097746` (`c6e2c91`) — TASK-313 Phase A (`a5e90a3`) / Phase B (`3a74edd`) and TASK-314 Phase A (`1f110bb`) / Phase B (`c6e2c91`) commits are filed. |
| **Upstream** | `origin/master` is at `3a74edd`; local `master` is ahead by TASK-314 Phase A and Phase B commits. Nothing is pushed. |
| **Audit Timestamp** | 2026-10-21 |
| **Audit Depth** | INCREMENTAL — base commit is `c6e2c91`; delta is the TASK-315 documentation-only declared set (`BOSS_RULES.md`, `COMBAT_RULES.md`, `GAME_STATE.md`, `.ai/PROJECT_STATE.md`). |
| **Freshness Status** | `FRESH` for baseline `c6e2c91`; updated inline under TASK-315. |
| **Working Tree Baseline** | Clean repository baseline with TASK-315 active in `tasks/active/`. |

---

## 2. Task Lifecycle Status

| Category | Count | Task IDs & Summary |
| :--- | :--- | :--- |
| **Last Verified Completed Task** | 1 | [TASK-314](../tasks/completed/TASK-314-preserve-balance-baseline-and-measure-modern-boss-cadence.md) (`preserve-balance-baseline-and-measure-modern-boss-cadence`; implementation `1f110bb`, completion record `c6e2c91`) |
| **Active Tasks (`tasks/active/`)** | 1 | [TASK-315](../tasks/active/TASK-315-reconcile-stale-boss-skill-specifications.md) (`reconcile-stale-boss-skill-specifications`) — documentation-only reconciliation in progress |
| **Blocked Tasks (`tasks/blocked/`)** | 0 | None (TASK-316 to be registered as preparation-only / blocked on production authorization) |
| **Backlog Candidates (`tasks/backlog/`)** | 0 | None |
| **Total Completed Tasks (`tasks/completed/`)** | 315 | Records through TASK-314 |
| **Highest Completed Task ID** | 314 | TASK-314 |
| **Highest Existing Task ID** | 315 | TASK-315 active |

---

## 3. Decisions & Governance Context

### Approved Key Architectural Decisions
- [ADR-001](../docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md): Server-authoritative gameplay resolution.
- [ADR-002](../docs/03-decisions/ADR/ADR-002-modular-monolith.md): Modular monolith structure (.NET backend).
- [ADR-003](../docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md): Phaser 4 client battle runtime with React UI shell.
- [ADR-004](../docs/03-decisions/ADR/ADR-004-signalr-realtime.md) / [ADR-005](../docs/03-decisions/ADR/ADR-005-redis-active-battle-state.md): SignalR event dispatch and Redis active battle state.
- [ADR-006](../docs/03-decisions/ADR/ADR-006-postgresql-persistence.md): PostgreSQL persistent state.
- [ADR-024](../docs/03-decisions/ADR/ADR-024-database-driven-boss-balance-configuration.md): Database-driven Boss balance configuration (Domain Overlay nullable `CastPriorityScore`).
- [ADR-026](../docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md): Shared-board gameplay and actor-relative match resolution.
- [ADR-029](../docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md): Boss as a genuine opponent — Boss Power, Boss Cards, one-action response policy. Slices 1–5 delivered (Slice 5 delivered under TASK-313).

### Resolved Operator Decisions
- **OD-311-1 = B** — `chargeRequirement` and `baseDamage` are retained inside persisted JSONB as storage compatibility only; runtime responsibility removed; no cleanup migration.
- **OD-311-2 = A** — `BossSkillCast` removed from wire contract as a coordinated breaking release; active-battle drain is a required pending operational prerequisite.
- **OD-314-1 = C** — Historical balance baseline preserved untouched without added Boss Cards; historical test failures resolved in `BalanceEvidenceMeasurementCorrectionTests`; separate modern Boss Card cadence measurement suite established in `ModernBossCardCadenceMeasurementTests` (delivered under TASK-314).
- **OD-315-1 = AUTHORIZED** — Documentation-only reconciliation of stale active specifications executed under TASK-315.
- **OD-316-1 = PREPARATION ONLY** — Breaking wire-contract release runbook and checklist preparation authorized; no battle drain, deployment, or remote push.

### Unresolved / Deferred Items
- **The fifth Gem / Power-drain Gem:** Remains FUTURE (`MVP_SCOPE.md` §3).
- **Breaking Wire Release Deployment Execution:** Pending operator authorization, maintenance window scheduling, and production execution.

---

## 4. Candidate Task Readiness

| Candidate Task ID | Lifecycle State | Dependencies Satisfied | Scope / Docs Aligned | Readiness Verdict |
| :--- | :--- | :--- | :--- | :--- |
| TASK-314 | DONE | TASK-313 DONE; OD-314-1 = C | Satisfied; verified at HEAD `c6e2c91` | COMPLETED (`1f110bb` / `c6e2c91`) |
| TASK-315 | IN PROGRESS | TASK-313, TASK-314 DONE; OD-315-1 = AUTHORIZED | Documentation-only declared set | **IN PROGRESS** |
| TASK-316 | PREPARATION ONLY | TASK-313 DONE; OD-316-1 = PREPARATION ONLY | Runbook/checklist preparation | READY FOR REGISTRATION (blocked on production authorization) |
| Fifth Gem / Power-drain Gem | **FUTURE** | — | `MVP_SCOPE.md` §3 | Out of scope until separate authorization |

---

## 5. Changed Areas & Recent Trajectory

- **Balance Evidence & Cadence (TASK-314):**
  - Historical balance baseline matrix (`BalanceBaselineMatrix`) and pinned artifacts preserved untouched.
  - Test contracts in `BalanceEvidenceMeasurementCorrectionTests` reconciled: `TASK195_PerTurnSeries_ReconcilesTerminalCardCastWithoutPhantomTurn` retargeted to seed `20261001UL` (turn 27 card-terminated victory under post-Slice-5 engine); `TASK296_ReviewD_Canonical75RunCandidate_Verification` distinguishes historical Candidate D artifact verification (8/41/26) from current-runtime offline matrix execution (10/39/26 with Sơn Thạch Vệ seeds 20261003/20261005 shifting to victory due to absent legacy skill damage).
  - Modern Boss Card cadence suite (`ModernBossCardCadenceMeasurementTests.cs`) added covering all five MVP Bosses with provisioned Boss Cards (`ProvisionedBossCardContent.All`), verifying canonical `BossCardCast` event emission, exact single counting in telemetry (M-13 `BossCardCastCount`), in-battle execution, one-action response policy, and contrasting with offline structural unavailability.
  - All 3363 backend tests passing with 0 failures, 0 skipped.
- **Active Specification Reconciliation (TASK-315):**
  - `BOSS_RULES.md` §6.2.1 reconciled to remove obsolete Boss Skill Base Damage references; Rage modifies `EffectiveBossATK` derived from confirmed ATK-gem matches.
  - `COMBAT_RULES.md` §3.4, §4 item 1, §5.3.3, §5.3.4, §5.4.3, §5.5.2, §5.5.3 reconciled: Boss abilities deal zero direct damage; Root lifecycle worked example updated to step 18c cast with canonical 3-Turn duration; `SkillCooldown` explicitly described as Boss ability cooldown counter.
  - `GAME_STATE.md` §2.4.5 Stun description reconciled to match the one-action response policy (prevents Boss Card casting and board match actions); §5.1.1 status effect lifecycle updated to refer to Boss response step 18 and `SkillCooldown`.

---

## 6. Known Blockers & Deferred Issues

- **Release prerequisite — breaking wire contract (PENDING OPERATIONS):** removing `BossSkillCast` makes server and client mutually incompatible. The coordinated client/server deployment and the `REDIS_STATE.md` active-battle drain are **required and have not been executed or verified**. No drain, deployment, or push occurred. TASK-316 establishes the release runbook and checklist as preparation only.
- **Pre-existing balance-evidence failures:** **RESOLVED** under TASK-314. All 3363 backend tests pass.
- **Residual stale documentation prose:** **RESOLVED** under TASK-315.
- **4 pre-existing Infrastructure failures:** **RESOLVED** under TASK-313.

---

## 7. Next Recommended Action

- **Status:** `TASK_315_EXECUTION`
- **Action:**
  1. Complete TASK-315 Phase A commit (documentation slice: `BOSS_RULES.md`, `COMBAT_RULES.md`, `GAME_STATE.md`, `.ai/PROJECT_STATE.md`).
  2. File TASK-315 Phase B completion record in `tasks/completed/`.
  3. Register TASK-316 for breaking wire-contract runbook and checklist preparation, recording operational prerequisites and leaving the task in the appropriate lifecycle state (`BLOCKED` on production deployment authorization).
  4. Perform final verification and compile audit report; do not push.

- **Standing constraints:** the Fifth Gem remains FUTURE; no database cleanup migration exists for the retained JSONB members; no tolerant-reader/no-op compatibility path was added for the removed wire event; do not claim deployment readiness while the drain/deploy prerequisite is unexecuted.
