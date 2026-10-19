# .ai/PROJECT_STATE.md — Project State Snapshot

**Version:** 1.2

**Notice:** This document is an AI navigation cache, not a source of truth. It records compact, verifiable project status to prevent repetitive whole-repository audits. Authoritative sources remain `docs/` (rules/architecture), `AGENTS.md` (contract), and `tasks/` (lifecycle state).

---

## 1. Audit Metadata

| Property | Value |
| :--- | :--- |
| **Audited Branch** | `master` |
| **Audited HEAD SHA** | `94d40d0a0a15fa3da195ac3cf851d527f855acd3` (`94d40d0`) |
| **Upstream** | `origin/master`, branch ahead 2 (the two TASK-306 commits; nothing pushed) |
| **Audit Timestamp** | 2026-10-19 |
| **Audit Depth** | INCREMENTAL — `.ai/PROJECT_STATE.md` recorded `0551386`, which differs from HEAD; the delta is exactly the TASK-306 artifact, its completion record and this cache. Recorded lifecycle facts were re-derived from Git. |
| **Freshness Status** | `STALE` — the audit baseline is `94d40d0`; TASK-307's Phase A/Phase B commits advance HEAD. Re-run baseline collection before the next selection. |
| **Working Tree Baseline** | Clean at audit start (verified via `git status --porcelain`). In-flight declared files: the TASK-307 record, its reconciliation artifact, and this cache. |

---

## 2. Task Lifecycle Status

| Category | Count | Task IDs & Summary |
| :--- | :--- | :--- |
| **Last Verified Completed Task** | 1 | [TASK-306](../tasks/completed/TASK-306-adr-029-slice-boundary-reconciliation-and-boss-card-definition-design.md) (`adr-029-slice-boundary-reconciliation-and-boss-card-definition-design`, filed at `94d40d0`) |
| **Active Tasks (`tasks/active/`)** | 1 | [TASK-307](../tasks/active/TASK-307-adr-029-reconciliation-and-slice-3-readiness.md) (`adr-029-reconciliation-and-slice-3-readiness`) |
| **Blocked Tasks (`tasks/blocked/`)** | 0 | None |
| **Backlog Candidates (`tasks/backlog/`)** | 0 | None |
| **Total Completed Tasks (`tasks/completed/`)** | 308 | Records through TASK-306 (including sub-task sequences) |
| **Highest Existing Task ID** | 306 | Next sequential ID = TASK-307 (assigned in this session); 309 completed records after TASK-307 files |

---

## 3. Decisions & Governance Context

### Approved Key Architectural Decisions
- [ADR-001](../docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md): Server-authoritative gameplay resolution.
- [ADR-002](../docs/03-decisions/ADR/ADR-002-modular-monolith.md): Modular monolith structure (.NET backend).
- [ADR-003](../docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md): Phaser 4 client battle runtime with React UI shell.
- [ADR-004](../docs/03-decisions/ADR/ADR-004-signalr-realtime.md) / [ADR-005](../docs/03-decisions/ADR/ADR-005-redis-active-battle-state.md): SignalR event dispatch and Redis active battle state.
- [ADR-006](../docs/03-decisions/ADR/ADR-006-postgresql-persistence.md): PostgreSQL persistent state.
- [ADR-024](../docs/03-decisions/ADR/ADR-024-database-driven-boss-balance-configuration.md): Database-driven Boss balance configuration (Accepted; Domain Overlay over four balance columns).
- [ADR-026](../docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md): Shared-board gameplay and actor-relative match resolution (Accepted; POWER bullet partially superseded by ADR-029).
- [ADR-029](../docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md): Boss as a genuine opponent — Boss Power, Boss Cards, one-action response policy (Accepted; Slices 1 and 2 implemented).

### Unresolved Decisions & Open Items
- **TASK-307 reduced the operator decision set to three items** recorded in its artifact §4.2: **OD-1** (approve the consolidated Boss Card contract — `CardDefinition` reuse, the `DATABASE.md` identity exception, provisioned-content active form with only Sơn Thạch Vệ enabled, and the ADR-029 amendment), **OD-2** (`earthquake`'s `PowerCost` and Shield magnitude), **OD-3** (nullable `CastPriorityScore` overlay column + `earthquake`'s score). Nothing is approved; no value is invented.
- **Closed by authoritative evidence (not operator decisions):** U-1 (resolved TASK-304), U-12 (cooldown `N−1` ordering already fixed by `GAME_STATE.md` §2.4.3 and `BOSS_RULES.md` §4.3/§6.3; the stale inline comment is a code defect).
- **Narrowed:** U-2 → OD-2a; U-4 → OD-2b; U-3 → OD-3b (only `earthquake` needed for Slice 3). **Deferred, not blocking:** U-5 (Slice 4 cooldown retune), U-6 (closed by Slice 5 retirement).
- No task is in `tasks/blocked/`; the blocker is the operator decision gate recorded in the TASK-307 artifact §4.2/§10.

---

## 4. Candidate Task Readiness

| Candidate Task ID | Lifecycle State | Dependencies Satisfied | Scope / Docs Aligned | Readiness Verdict |
| :--- | :--- | :--- | :--- | :--- |
| TASK-307 | Active WIP (`IN PROGRESS` → `IN REVIEW` → `DONE`) | TASK-303 / TASK-304 / TASK-305 / TASK-306 completed | Authoritative docs and source re-verified; one consolidated contract prepared; two authoritative conflicts reported, not resolved | `READY FOR OPERATOR APPROVAL`; runtime implementation and `docs/` application blocked on OD-1/OD-2/OD-3 |
| **Slice 3 — End-to-end Boss Card action policy for Sơn Thạch Vệ** | **NOT REGISTERED (proposal only)** | Needs TASK-307 approval (OD-1/OD-2/OD-3) | Scope + acceptance checklist in TASK-307 artifact §7 | Register only after the three decisions are recorded |

---

## 5. Changed Areas & Recent Trajectory

- **Recent Focus Area:** ADR-029 migration — Boss Power foundation (Slice 1, TASK-304), shared actor-cast contract (Slice 2, TASK-305), slice-boundary reconciliation and Boss Card design (TASK-306), now contract reconciliation and Slice 3 readiness (TASK-307).
- **Core Affected Components (recent code):**
  - `src/backend/GameServer.Domain/Bosses/BossState.cs`, `BossAiEngine.cs`, `BossDefinitions.cs`, `BossDefinition.cs`
  - `src/backend/GameServer.Domain/Cards/CardCastExecutor.cs`, `CardCategory.cs`
  - `src/backend/GameServer.Application/Battle/BattleStateService.cs`
  - `src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs`
  - `src/frontend/client/src/game/scenes/BattleScene.ts`, `BattleEventPresenter.ts`
- **Verified absent (Slice 3 work):** `BossActionPolicy` implementation (already named in `ARCHITECTURE.md` §3), Boss Card provisioned row, `CastPriorityScore` carrier, `BossCardCast` enum/factory/projection, client Boss Card presentation.
- **Uncommitted (this task):** the TASK-307 record, its reconciliation artifact, and this cache refresh.

---

## 6. Known Blockers

- **Operator decision gate (design/product):** OD-1/OD-2/OD-3. No Boss Card may be provisioned, no `docs/`/ADR amendment may be applied, and Slice 3 cannot start until they are recorded.
- **Reported authoritative conflicts (not resolved):** `DATABASE.md` §1/§3 fixes `CardDefinitionId` as `card-<kebab>` while `CARD_RULES.md` §1.1 item 3 requires a Boss Card's id to be the ability `SkillId` (OD-1b is the resolution); `BOSS_RULES.md:914` ("no Boss-specific value remains to be authored") conflicts with §6.3/§6.3.1's unresolved magnitudes and Power costs.
- **Reported, not fixed (pre-existing):** Boss `BaseDamage` provisioning drift (migrations' JSON carries legacy 150/120/100/150/180 while the Domain baseline and Boss rules carry 0; two known failing Infrastructure tests), stale `BattleHub` projection comment, stale cooldown comment, missing category guard on Signature-Skill derivation.

---

## 7. Next Recommended Action

- **Status:** `NEEDS_OPERATOR_DECISION`
- **Action:** operator answers the three decision points in the TASK-307 artifact
  ([artifact](../tasks/artifacts/TASK-307-adr-029-reconciliation-and-slice-3-readiness.md) §4.2/§10).
  Then register exactly one Slice 3 `GAMEPLAY-CHANGE` task (artifact §7) covering
  docs-first amendments, the provisioned `earthquake` Boss Card and score carrier,
  the deterministic policy, the round integration, `BossCardCast`, the client
  presentation and the regression/balance evidence. Do not create the four
  TASK-306 follow-up tasks (P-1…P-4) — they are work items of that one task.
- **If no decision is granted:** the correct status stays `NEEDS_OPERATOR_DECISION`
  at the documented gate; do not edit `docs/`, do not provision content, do not
  register Slice 3.
