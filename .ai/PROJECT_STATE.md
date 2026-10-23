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
| **Active Tasks (`tasks/active/`)** | 1 | [TASK-324](../tasks/active/TASK-324-cycle-less-combat-trace-heal-and-boss-passive-regen-record.md) (`cycle-less-combat-trace-heal-and-boss-passive-regen-record`) — **IN REVIEW**; Phase A/Phase B unexecuted (Git prohibited in its session), pending operator lifecycle finalization |
| **Blocked Tasks (`tasks/blocked/`)** | 2 | [TASK-316](../tasks/blocked/TASK-316-breaking-wire-contract-release-preparation.md) (`breaking-wire-contract-release-preparation`) — runbook authored (`docs/02-technical/RUNBOOK_BREAKING_WIRE_RELEASE.md`); live deployment blocked on production authorization. TASK-320 (governance resolution of the local commit chain) is also in `tasks/blocked/` |
| **Backlog Candidates (`tasks/backlog/`)** | 0 | None |
| **Total Completed Tasks (`tasks/completed/`)** | 321 | Record files present in `tasks/completed/` number **321** — far above the 316 this row previously stated, because the snapshot drifted before TASK-324. The row is corrected from an actual directory count only; the per-task history between TASK-315 and TASK-321 is **not** re-verified here and is not claimed. TASK-324 adds nothing to this folder |
| **Highest Completed Task ID** | 315 | TASK-315 |
| **Highest Existing Task ID** | 324 | TASK-324 in `tasks/active/` |

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
- **OD-322-1 = A (operator decision, made in the TASK-324 session, 2026-10-23)** — the `combatTrace` heal's `cycleOrdinal` becomes optional end to end: `CombatTraceHeal.CycleOrdinal` and `CombatTraceHealWireDto.CycleOrdinal` are `int?`; an inapplicable cycle ordinal is **omitted** from the serialized JSON and is never written as `null` or `0`; the frontend reader accepts an absent member without inventing a value; valid numeric ordinals are unchanged. OD-322-1 was **open (unanswered) at its introduction in TASK-322's record**; this is its first ruling, not an earlier approval.
- **TASK-324 phase-limited delivery (operator decision, made in the TASK-324 session, 2026-10-23)** — Git operations remain prohibited, so TASK-324's permitted terminal state is `IN REVIEW`. Its Phase A implementation commit and Phase B completion record are **not executed**; the operator performs the authorized lifecycle finalization when Git operations are permitted. TASK-324 is **not** `DONE` and no commit SHA is reported for it.

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
| TASK-324 | **IN REVIEW** | TASK-321, TASK-322 DONE; OD-322-1 = A | Implementation + tests complete and verified; Phase A/Phase B unexecuted (Git prohibited) | **IN REVIEW** in `tasks/active/` — pending operator lifecycle finalization |
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
- **Cycle-less combat-trace heal and Boss Passive regeneration (TASK-324, IN REVIEW):**
  - `CombatTraceHeal.CycleOrdinal` and `CombatTraceHealWireDto.CycleOrdinal` are `int?` (OD-322-1 = A); `CombatTrace.NoCycle` and its `0` sentinel are gone. A heal belonging to no actor cycle omits the member from the serialized JSON — never `null`, never `0`.
  - `BattleStateService`'s `boss-moc-yeu-regen` branch now records exactly one `CombatTraceHeal` (Target Boss, `Amount` = the clamped HP restored, `RawAmount` = the authored 5%, `CycleOrdinal: null`), so `trace.Boss` reconciles with the trace's own damage and heals.
  - The frontend `CombatTraceHealing.cycleOrdinal` is optional and the reader never invents it; `SIGNALR_PROTOCOL.md` is Version 2.34 with a §8 item 10 interoperability clause.
  - Two defective TASK-324 regression tests were repaired: their premise ("progress one Match below the threshold") was wrong because `PASSIVE_RULES.md` §2 item 3 evaluates the threshold once at `P + N`; the repair pins progress from the Round's own Match batch, as `BossPassiveEffectsTests` does.
  - **Backend aggregate now 3418** (Domain 1700, Application 803, Api 408, Infrastructure 507), 0 failed, 0 skipped. Frontend: 24 files, 1304 tests. Both builds pass.
  - Phase A implementation commit and Phase B completion record are **unexecuted** (Git prohibited); the task remains `IN REVIEW`.

---

## 6. Known Blockers & Deferred Issues

- **Release prerequisite — breaking wire contract (PENDING OPERATIONS):** removing `BossSkillCast` makes server and client mutually incompatible. The coordinated client/server deployment and the `REDIS_STATE.md` active-battle drain are **required and have not been executed or verified**. No drain, deployment, or push occurred. TASK-316 establishes the release runbook and checklist as preparation only.
- **TASK-324 lifecycle finalization (PENDING OPERATOR, not a defect):** TASK-324 sits in `tasks/active/` at `Status: IN REVIEW`. Its Phase A implementation commit and Phase B completion record cannot be executed while Git operations are prohibited. The operator must perform the two-stage commit protocol (`tasks/TASK_LIFECYCLE.md` §6) when Git operations are permitted. Nothing in the delivered work is withheld.
- **This snapshot's audit metadata is stale relative to the local commit chain:** §1 still records `1a159a3` as the audited HEAD and §2 still counts records through TASK-315, while `tasks/completed/` already contains TASK-321 and TASK-322. That discrepancy predates TASK-324 and is **not repaired here** (no Git operation may be run to establish the current HEAD, and TASK-324 is not a reconciliation task).
- **Pre-existing balance-evidence failures:** **RESOLVED** under TASK-314.
- **Residual stale documentation prose:** **RESOLVED** under TASK-315.
- **4 pre-existing Infrastructure failures:** **RESOLVED** under TASK-313.
- **Residual evidence discrepancy (OPEN, disclosure only):** TASK-313's record states a pre-TASK-314 Application count of `785`, but a discovery listing of the TASK-313 tree is required to confirm it; TASK-314's record discloses that the base plus its twelve new cases does not reconcile to the executed 788. The reproducible figures are those recorded above.
- **Test-count drift between TASK-322 and TASK-324 (disclosed):** TASK-322 recorded Domain 1700 / Application 801 / Api 408 / Infrastructure 507 = 3416. The TASK-324 start-of-session baseline executed Domain 1700 / Application **803** / Api 408 / Infrastructure 507 = 3418, with exactly the two defective TASK-324 regen tests failing. The two extra Application cases (801 → 803) are the broken TASK-324 tests, which were already present in the tree when this session began; their provenance is not established here. After TASK-324 the aggregate is **3418**, 0 failed, 0 skipped, with those two cases repaired rather than replaced.
- **Commit-message defects in two remediation Phase A commits (OPEN, not repaired):** `c4a5823` (TASK-318) begins its message with a UTF-8 BOM before `test:`; `1a159a3` (TASK-317) has no conventional-commit subject (its first body paragraph became the subject). `TASK_LIFECYCLE.md` §6.2 P-6 and §6.4 item 6 forbid amend/reset/rebase, so both were left as created and disclosed in their task records. Both commits' file sets and content are correct and verified.

---

## 7. Next Recommended Action

- **Status:** `RELEASE_PREREQUISITES_BLOCKED`
- **Action:**
  0. **Operator, first:** finalize TASK-324's lifecycle when Git operations are permitted — execute `tasks/TASK_LIFECYCLE.md` §6 Phase A over its declared file set, capture the SHA, then Phase B to file the record in `tasks/completed/` as `DONE`. No agent may do this while Git operations are prohibited.
  1. TASK-314 and TASK-315 are completed and verified (commits `1f110bb`/`c6e2c91` and `590c7a6`/`09f2bf8`).
  2. TASK-316 breaking wire-contract runbook and operational checklist authored at `docs/02-technical/RUNBOOK_BREAKING_WIRE_RELEASE.md` (commit `8424a9e2d0a432281b3d67cc528af901fdae4497`), with its drain guidance corrected under TASK-317 (`1a159a3`).
  3. Production deployment remains strictly prohibited within automated sessions (OD-316-1 = PREPARATION ONLY). Task is held BLOCKED in `tasks/blocked/` pending operator scheduling of production maintenance window and deployment execution. Do not push.
  4. Operator follow-up for the two disclosed commit-message defects (`c4a5823`, `1a159a3`) requires an explicit history-rewrite authorization; no agent may perform it under P-6.
- **Standing constraints:** the Fifth Gem remains FUTURE; no database cleanup migration exists for the retained JSONB members; no tolerant-reader/no-op compatibility path was added for the removed wire event; do not claim deployment readiness while the drain/deploy prerequisite is unexecuted.
