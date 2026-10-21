# .ai/PROJECT_STATE.md — Project State Snapshot

**Version:** 1.9

**Notice:** This document is an AI navigation cache, not a source of truth. It records compact, verifiable project status to prevent repetitive whole-repository audits. Authoritative sources remain `docs/` (rules/architecture), `AGENTS.md` (contract), and `tasks/` (lifecycle state).

---

## 1. Audit Metadata

| Property | Value |
| :--- | :--- |
| **Audited Branch** | `master` |
| **Audited HEAD SHA** | `1a159a3` — TASK-317/318/319 post-audit remediation Phase A slices; TASK-313/314/315/316 history unchanged. |
| **Upstream** | `origin/master` is at `3a74edd`; local `master` is ahead. Nothing is pushed. |
| **Audit Timestamp** | 2026-10-21 |
| **Audit Depth** | INCREMENTAL — base commit is `3a74edd`; deltas are TASK-314, TASK-315, TASK-316, and the post-audit remediation TASK-317, TASK-318, TASK-319. |
| **Freshness Status** | `FRESH`. |
| **Working Tree Baseline** | Clean repository baseline with TASK-316 recorded in `tasks/blocked/`. |

---

## 2. Task Lifecycle Status

| Category | Count | Task IDs & Summary |
| :--- | :--- | :--- |
| **Last Verified Completed Task** | 1 | [TASK-315](../tasks/completed/TASK-315-reconcile-stale-boss-skill-specifications.md) (`reconcile-stale-boss-skill-specifications`; implementation `590c7a6`, completion record `09f2bf8`) |
| **Active Tasks (`tasks/active/`)** | 0 | None |
| **Blocked Tasks (`tasks/blocked/`)** | 1 | [TASK-316](../tasks/blocked/TASK-316-breaking-wire-contract-release-preparation.md) (`breaking-wire-contract-release-preparation`) — runbook authored (`docs/02-technical/RUNBOOK_BREAKING_WIRE_RELEASE.md`); live deployment blocked on production authorization |
| **Backlog Candidates (`tasks/backlog/`)** | 0 | None |
| **Total Completed Tasks (`tasks/completed/`)** | 316 | Records through TASK-315 |
| **Highest Completed Task ID** | 315 | TASK-315 |
| **Highest Existing Task ID** | 316 | TASK-316 in `tasks/blocked/` |

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
| TASK-314 | DONE | TASK-313 DONE; OD-314-1 = C | Satisfied; verified at commit `c6e2c91` | COMPLETED (`1f110bb` / `c6e2c91`) |
| TASK-315 | DONE | TASK-313, TASK-314 completed | Satisfied; verified at commit `09f2bf8` | COMPLETED (`590c7a6` / `09f2bf8`) |
| TASK-316 | BLOCKED | TASK-313, TASK-315 DONE; OD-316-1 = PREPARATION ONLY | Runbook authored; live deployment blocked | **BLOCKED** in `tasks/blocked/` |
| Fifth Gem / Power-drain Gem | **FUTURE** | — | `MVP_SCOPE.md` §3 | Out of scope until separate authorization |

---

## 5. Changed Areas & Recent Trajectory

- **Balance Evidence & Cadence (TASK-314):**
  - Historical balance baseline matrix (`BalanceBaselineMatrix`) and pinned artifacts preserved untouched.
  - Test contracts in `BalanceEvidenceMeasurementCorrectionTests` reconciled: `TASK195_PerTurnSeries_ReconcilesTerminalCardCastWithoutPhantomTurn` retargeted to seed `20261001UL` (turn 27 card-terminated victory under post-Slice-5 engine); `TASK296_ReviewD_Canonical75RunCandidate_Verification` distinguishes historical Candidate D artifact verification (8/41/26) from current-runtime offline matrix execution (10/39/26 with Sơn Thạch Vệ seeds 20261003/20261005 shifting to victory due to absent legacy skill damage).
  - Modern Boss Card cadence suite (`ModernBossCardCadenceMeasurementTests.cs`) added covering all five MVP Bosses with provisioned Boss Cards (`ProvisionedBossCardContent.All`), verifying canonical `BossCardCast` event emission, exact single counting in telemetry (M-13 `BossCardCastCount`), in-battle execution, one-action response policy, and contrasting with offline structural unavailability.
  - Backend test counts **corrected under TASK-319**: the Application suite executes **788** and the backend aggregate is **3368** (TASK-314 tree), not the `783`/`3363` previously recorded here. The previous figure was a transcription error, not a discovery/configuration/environment effect — the recorded command reproduces 788 and a discovery listing of the TASK-314 tree enumerates exactly 788 cases. See the TASK-314 record's TASK-319 evidence note.
- **Active Specification Reconciliation (TASK-315):**
  - `BOSS_RULES.md` §6.2.1 reconciled to remove obsolete Boss Skill Base Damage references; Rage modifies `EffectiveBossATK` derived from confirmed ATK-gem matches.
  - `COMBAT_RULES.md` §3.4, §4 item 1, §5.3.3, §5.3.4, §5.4.3, §5.5.2, §5.5.3 reconciled: Boss abilities deal zero direct damage; Root lifecycle worked example updated to step 18c cast with canonical 3-Turn duration; `SkillCooldown` explicitly described as Boss ability cooldown counter.
  - `GAME_STATE.md` §2.4.5 Stun description reconciled to match the one-action response policy (prevents Boss Card casting and board match actions); §5.1.1 status effect lifecycle updated to refer to Boss response step 18 and `SkillCooldown`.
- **Post-Audit Remediation (TASK-317, TASK-318, TASK-319):**
  - **TASK-317 — runbook drain guidance corrected** (`1a159a3`): `RUNBOOK_BREAKING_WIRE_RELEASE.md` no longer relies on a 15-minute expiry; the drain is gated on an explicit verified zero-active-battle confirmation derived from `REDIS_STATE.md` §1/§3 (single `battle:{battleId}:state` key, 30-minute inactivity expiry refreshed only on successful resolution), with per-key classification and the required production tooling named. Preparation-only scope unchanged.
  - **TASK-318 — a real Boss Card cast is now proven** (`c4a5823`): the cadence suite asserts one canonical `Sơn Thạch Vệ` → `earthquake` cast from a committed round at the authored cast-priority score `16`, with M-13 = 1, M-08 = 0, the authored Power cost and cooldown, and the one-action invariant. Diagnosis recorded: because a first-pass Match clears ≥ 3 cells and the lightest weight is 5, a board action never scores below 15, and the observed minimum over 500 boards was 25 — so the cast is a near-exceptional case on generated boards and the fixture supplies a crafted 15-point board while the production policy still makes the selection.
  - **TASK-319 — evidence reconciled**: TASK-314's test counts corrected to the reproducible values and the TASK-316 blocked record's commit identifier replaced with the exact full SHA `8424a9e2d0a432281b3d67cc528af901fdae4497` (the previously recorded `8424a9e5251648a474fe5e0cb2aa55c28591a27e` did not resolve to any Git object). TASK-316 remains BLOCKED.
  - **Current backend aggregate after TASK-318: 3365** (Domain 1674, Infrastructure 507, Api 399, Application 785), 0 failed, 0 skipped. Frontend: 24 files, 1267 tests.

---

## 6. Known Blockers & Deferred Issues

- **Release prerequisite — breaking wire contract (PENDING OPERATIONS):** removing `BossSkillCast` makes server and client mutually incompatible. The coordinated client/server deployment and the `REDIS_STATE.md` active-battle drain are **required and have not been executed or verified**. No drain, deployment, or push occurred. TASK-316 establishes the release runbook and checklist as preparation only.
- **Pre-existing balance-evidence failures:** **RESOLVED** under TASK-314.
- **Residual stale documentation prose:** **RESOLVED** under TASK-315.
- **4 pre-existing Infrastructure failures:** **RESOLVED** under TASK-313.
- **Residual evidence discrepancy (OPEN, disclosure only):** TASK-313's record states a pre-TASK-314 Application count of `785`, but a discovery listing of the TASK-313 tree is required to confirm it; TASK-314's record discloses that the base plus its twelve new cases does not reconcile to the executed 788. The reproducible figures are those recorded above.
- **Commit-message defects in two remediation Phase A commits (OPEN, not repaired):** `c4a5823` (TASK-318) begins its message with a UTF-8 BOM before `test:`; `1a159a3` (TASK-317) has no conventional-commit subject (its first body paragraph became the subject). `TASK_LIFECYCLE.md` §6.2 P-6 and §6.4 item 6 forbid amend/reset/rebase, so both were left as created and disclosed in their task records. Both commits' file sets and content are correct and verified.

---

## 7. Next Recommended Action

- **Status:** `RELEASE_PREREQUISITES_BLOCKED`
- **Action:**
  1. TASK-314 and TASK-315 are completed and verified (commits `1f110bb`/`c6e2c91` and `590c7a6`/`09f2bf8`).
  2. TASK-316 breaking wire-contract runbook and operational checklist authored at `docs/02-technical/RUNBOOK_BREAKING_WIRE_RELEASE.md` (commit `8424a9e2d0a432281b3d67cc528af901fdae4497`), with its drain guidance corrected under TASK-317 (`1a159a3`).
  3. Production deployment remains strictly prohibited within automated sessions (OD-316-1 = PREPARATION ONLY). Task is held BLOCKED in `tasks/blocked/` pending operator scheduling of production maintenance window and deployment execution. Do not push.
  4. Operator follow-up for the two disclosed commit-message defects (`c4a5823`, `1a159a3`) requires an explicit history-rewrite authorization; no agent may perform it under P-6.
- **Standing constraints:** the Fifth Gem remains FUTURE; no database cleanup migration exists for the retained JSONB members; no tolerant-reader/no-op compatibility path was added for the removed wire event; do not claim deployment readiness while the drain/deploy prerequisite is unexecuted.
