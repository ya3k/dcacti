# .ai/PROJECT_STATE.md — Project State Snapshot

**Version:** 1.8

**Notice:** This document is an AI navigation cache, not a source of truth. It records compact, verifiable project status to prevent repetitive whole-repository audits. Authoritative sources remain `docs/` (rules/architecture), `AGENTS.md` (contract), and `tasks/` (lifecycle state).

---

## 1. Audit Metadata

| Property | Value |
| :--- | :--- |
| **Audited Branch** | `master` |
| **Audited HEAD SHA** | `8589f7e8909f743e3461d77494588728da3a766b` (`8589f7e`) — TASK-311's Phase A (`0207c42`) and Phase B (`8589f7e`) commits are filed. TASK-313's own Phase A implementation commit and Phase B filing commit are created after this cache snapshot; their SHAs are recorded in TASK-313's completion record, not here. |
| **Upstream** | `origin/master` is at `081f9d7`; the branch is ahead by six commits through TASK-311 plus TASK-313's Phase A/Phase B commits. Nothing is pushed. |
| **Audit Timestamp** | 2026-10-20 |
| **Audit Depth** | INCREMENTAL — base commit is `8589f7e`; delta is the TASK-313 declared implementation set (ADR-029 Slice 5 removal across Domain, Application, Infrastructure, Api, Redis serialization, frontend client, backend/frontend tests, and 11 authoritative documents). |
| **Freshness Status** | `FRESH` for the pre-work baseline `8589f7e`; **`STALE`** once TASK-313's Phase A/Phase B commits land, because they advance HEAD. |
| **Working Tree Baseline** | `8589f7e` tree; TASK-313 active in `tasks/active/`, implementation and documentation files modified per its declared file set (manifest revision R3). |

---

## 2. Task Lifecycle Status

| Category | Count | Task IDs & Summary |
| :--- | :--- | :--- |
| **Last Verified Completed Task** | 1 | [TASK-311](../tasks/completed/TASK-311-adr-029-slice-5-removal-readiness-reconciliation.md) (`adr-029-slice-5-removal-readiness-reconciliation`; implementation `0207c42`, completion record `8589f7e`) |
| **Active Tasks (`tasks/active/`)** | 1 | [TASK-313](../tasks/active/TASK-313-adr-029-slice-5-skillcharge-and-legacy-boss-skill-removal.md) (`adr-029-slice-5-skillcharge-and-legacy-boss-skill-removal`) — implementation done, two-stage commit protocol in progress |
| **Blocked Tasks (`tasks/blocked/`)** | 0 | None |
| **Backlog Candidates (`tasks/backlog/`)** | 0 | None |
| **Total Completed Tasks (`tasks/completed/`)** | 313 | Records through TASK-311 (TASK-313 moves here on Phase B) |
| **Highest Completed Task ID** | 311 | TASK-311 (implementation `0207c42`, completion record `8589f7e`) |
| **Highest Existing Task ID** | 313 | TASK-312 preflight report (out-of-band); TASK-313 active |

---

## 3. Decisions & Governance Context

### Approved Key Architectural Decisions
- [ADR-001](../docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md): Server-authoritative gameplay resolution.
- [ADR-002](../docs/03-decisions/ADR/ADR-002-modular-monolith.md): Modular monolith structure (.NET backend).
- [ADR-003](../docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md): Phaser 4 client battle runtime with React UI shell.
- [ADR-004](../docs/03-decisions/ADR/ADR-004-signalr-realtime.md) / [ADR-005](../docs/03-decisions/ADR/ADR-005-redis-active-battle-state.md): SignalR event dispatch and Redis active battle state.
- [ADR-006](../docs/03-decisions/ADR/ADR-006-postgresql-persistence.md): PostgreSQL persistent state.
- [ADR-024](../docs/03-decisions/ADR/ADR-024-database-driven-boss-balance-configuration.md): Database-driven Boss balance configuration (**Accepted, amended by TASK-308**: Domain Overlay is five values, the fifth the nullable `CastPriorityScore`).
- [ADR-026](../docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md): Shared-board gameplay and actor-relative match resolution (Accepted; POWER bullet and `SkillCharge` balance questions partially superseded/closed by ADR-029).
- [ADR-029](../docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md): Boss as a genuine opponent — Boss Power, Boss Cards, one-action response policy (**Accepted, amended by TASK-308, TASK-309, TASK-311, and TASK-313**). Slices 1–4 delivered; **Slice 5 delivered under TASK-313**.

### Resolved Operator Decisions (TASK-313)
- **OD-311-1 = B** — `chargeRequirement` and `baseDamage` are **retained** inside the persisted `BossDefinition.SkillDefinition` JSONB as storage compatibility only; their runtime/Domain responsibility is removed; no cleanup migration was added and no historical migration was rewritten.
- **OD-311-2 = A** — `BossSkillCast` is **removed** from the wire contract as a coordinated breaking release; the active-battle drain is a required, **not yet executed**, deployment prerequisite.
- **Implementation authorization:** granted; TASK-313 executed the removal subject to the repository lifecycle/verification/commit gates.

### Unresolved / Deferred Items
- **The fifth Gem / Power-drain Gem:** Remains FUTURE (`MVP_SCOPE.md` §3). Nothing in TASK-313 touched it.
- **Balance-evidence re-baseline (operator decision needed):** see §6.

---

## 4. Candidate Task Readiness

| Candidate Task ID | Lifecycle State | Dependencies Satisfied | Scope / Docs Aligned | Readiness Verdict |
| :--- | :--- | :--- | :--- | :--- |
| TASK-311 | DONE | TASK-309, TASK-310 completed | Satisfied; verified at HEAD `8589f7e` | COMPLETED (`0207c42` / `8589f7e`) |
| TASK-313 | IN PROGRESS (completion pending) | TASK-311 DONE; OD-311-1 = B; OD-311-2 = A; implementation authorized | Slice 5 declaration revision R3 covering every consumer; ADR-029 and 10 further authoritative documents synchronized | **IMPLEMENTED AND VERIFIED; two-stage commit pending** |
| Fifth Gem / Power-drain Gem | **FUTURE** | — | `MVP_SCOPE.md` §3 | Out of scope until separate authorization |

---

## 5. Changed Areas & Recent Trajectory

- **Recent Focus Area:** ADR-029 Slice 5 — physical removal of the legacy Boss Skill mechanism (TASK-313), after the removal-readiness reconciliation under TASK-311 and the TASK-312 preflight consumer audit.
- **Removed from runtime/Domain:** `BossState.SkillCharge` and its battle-state `skillCharge` JSON member; `ResourceGenerator.ApplyBossSkillCharge` and `CountPowerMatchGroups`; the legacy `bossCard is null` Boss Skill execution branch with the `BossSkillSecondaryEffect`/`BossSkillSecondaryEffectKind` declarations and the `BossSkillDefinition.BaseDamage`/`ChargeRequirement` + `BossDefinition.SkillBaseDamage`/`SkillChargeRequirement` members; `BattleEventType.BossSkillCast` (value 9 retired, not reused) with `BossSkillCastEvent`/`ForBossSkillCast`/`BattleEvent.BossSkillCast`; the `BattleEventWireDto` `skillId` member and `BossSkillCast` factory; the client's `BossSkillCast` parsing/presentation and the smoke-script callout allow-list entry.
- **Preserved unchanged (explicit invariants):** `BossState.Power` (0–100) and the `bossState.power` projection; `SkillCooldown`; the canonical `BossCardCast` contract and event; the one-action response policy with `CastPriorityScore` (`16` for Sơn Thạch Vệ, `85` for the other four) and the board-action tie-break; the shared cast contract; every combat balance value; the Fifth Gem's FUTURE status.
- **Telemetry:** M-13's cast count is renamed `BossCardCastCount` and sourced from the canonical `BossCardCast` event (JSON key `M-13_bossCardCastCount`).
- **Tests:** backend suite retargeted to the canonical contract — 3 obsolete test files retired (`BossSkillContractTests`, `BossSkillSecondaryEffectTests`, `RootAttackConsumptionTests`) with their coverage mapped to the Boss Card equivalents; 6 tests added, including a legacy Redis snapshot (`skillCharge`) tolerance test, a retained-storage `SkillDefinition` read-tolerance test, a canonical Boss Card cooldown-blocking test, and a metric-contract test for M-13. Frontend suite retargeted with no `BossSkillCast` handling remaining.
- **Documentation synchronized:** ADR-029 and `docs/03-decisions/README.md` (Lead), plus `BOSS_RULES` 2.18, `GAME_RULES` 3.12, `COMBAT_RULES` 3.3, `CARD_RULES` 1.13, `GAME_STATE` 2.35, `GAME_EVENTS` 2.21, `SIGNALR_PROTOCOL` 2.30 (§3.2.18 retired in place, no renumbering), `REDIS_STATE` 1.18, `DATABASE` 1.43.

---

## 6. Known Blockers & Deferred Issues

- **Release prerequisite — breaking wire contract (PENDING OPERATIONS):** removing `BossSkillCast` makes server and client mutually incompatible. The coordinated client/server deployment and the `REDIS_STATE.md` active-battle drain are **required and have not been executed or verified**. No drain, deployment, or push occurred in TASK-313.
- **Pre-existing balance-evidence failures (unchanged by TASK-313, operator decision needed):**
  - `BalanceEvidenceMeasurementCorrectionTests.TASK195_PerTurnSeries_ReconcilesTerminalCardCastWithoutPhantomTurn` — expects the run's terminal outcome detail to begin `"Card cast resolved"`.
  - `BalanceEvidenceMeasurementCorrectionTests.TASK296_ReviewD_Canonical75RunCandidate_Verification` — the pinned TASK-194 75-run distribution `8/41/26` no longer holds (first mismatch `Expected: 8, Actual: 10`).
  - Root cause for both: the offline balance harness (`BalanceBaselineMatrix.Cards`) supplies only the four Player Cards and no provisioned Boss Card rows, so no Boss ability can fire there after the legacy path was removed. Resolving this requires either provisioning the five Boss Card definitions in the harness and operator-approved re-baselining of the TASK-194/TASK-296 evidence, or an explicit decision to accept the shifted baseline. Adding that content changes simulated balance and was deliberately **not** done under TASK-313.
- **New measurement gap (consequence, not regression):** the offline balance harness can no longer measure Boss ability cadence at all (M-13 is structurally 0 there) until the Boss Card content decision above is made. Canonical cast telemetry remains available from `BossCardCast` at runtime and in the TASK-309 cast-frequency harness.
- **Residual stale documentation prose (pre-existing, outside TASK-313's declared sections):** a few passages still speak of a "Boss Skill"/"authored Base Damage" value in illustrative examples (`BOSS_RULES.md` §6.2.1, `COMBAT_RULES.md` §3.4 code block/§5.5.2/§5.5.3/§5.3.3, `GAME_STATE.md` §5.1.1). Suggest a small follow-up documentation task.
- **4 pre-existing Infrastructure failures: RESOLVED** under the OD-311-1 = B retained-storage contract (read tolerance + documented 0 placeholders), verified against the live PostgreSQL provisioning schema.

---

## 7. Next Recommended Action

- **Status:** `TASK_313_TWO_STAGE_COMMIT_IN_PROGRESS`
- **Action:**
  1. Complete TASK-313's Phase A implementation-slice commit (staged set = declared manifest revision R3, excluding the task record) and record its SHA/parent in the task record.
  2. File the Phase B completion record into `tasks/completed/` with the Phase A SHA and validation evidence.
  3. After filing, the next task should be an operator decision on the balance-evidence re-baseline described in §6, and only then a re-baseline task; do not push.

- **Standing constraints:** the Fifth Gem remains FUTURE; no database cleanup migration exists for the retained JSONB members; no tolerant-reader/no-op compatibility path was added for the removed wire event; do not claim deployment readiness while the drain/deploy prerequisite is unexecuted.
