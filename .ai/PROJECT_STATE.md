# .ai/PROJECT_STATE.md — Project State Snapshot

**Version:** 1.1

**Notice:** This document is an AI navigation cache, not a source of truth. It records compact, verifiable project status to prevent repetitive whole-repository audits. Authoritative sources remain `docs/` (rules/architecture), `AGENTS.md` (contract), and `tasks/` (lifecycle state).

---

## 1. Audit Metadata

| Property | Value |
| :--- | :--- |
| **Audited Branch** | `master` |
| **Audited HEAD SHA** | `0551386dcd17438df8daf95ddbef38e84c32a071` (`0551386`) |
| **Audit Timestamp** | 2026-10-19 |
| **Freshness Status** | `STALE` — the audit baseline is `0551386`; HEAD will advance with TASK-306's Phase A/Phase B commits. Re-run baseline collection before the next selection. |
| **Working Tree Baseline** | Clean at audit start (verified via `git status --porcelain`). In-flight declared files: the TASK-306 record, its design artifact, and this cache. |

---

## 2. Task Lifecycle Status

| Category | Count | Task IDs & Summary |
| :--- | :--- | :--- |
| **Last Verified Completed Task** | 1 | [TASK-305](../tasks/completed/TASK-305-adr-029-slice-2-shared-cast-contract-and-actor-relative-effect-resolution.md) (`adr-029-slice-2-shared-cast-contract-and-actor-relative-effect-resolution`, filed at `0551386`) |
| **Active Tasks (`tasks/active/`)** | 1 | [TASK-306](../tasks/active/TASK-306-adr-029-slice-boundary-reconciliation-and-boss-card-definition-design.md) (`adr-029-slice-boundary-reconciliation-and-boss-card-definition-design`) |
| **Blocked Tasks (`tasks/blocked/`)** | 0 | None |
| **Backlog Candidates (`tasks/backlog/`)** | 0 | None |
| **Total Completed Tasks (`tasks/completed/`)** | 307 | Records through TASK-305 (including sub-task sequences) |
| **Highest Existing Task ID** | 305 | Next sequential ID = TASK-306 (assigned in this session) |

---

## 3. Decisions & Governance Context

### Approved Key Architectural Decisions
- [ADR-001](../docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md): Server-authoritative gameplay resolution.
- [ADR-002](../docs/03-decisions/ADR/ADR-002-modular-monolith.md): Modular monolith structure (.NET backend).
- [ADR-003](../docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md): Phaser 4 client battle runtime with React UI shell.
- [ADR-004](../docs/03-decisions/ADR/ADR-004-signalr-realtime.md) / [ADR-005](../docs/03-decisions/ADR/ADR-005-redis-active-battle-state.md): SignalR event dispatch and Redis active battle state.
- [ADR-006](../docs/03-decisions/ADR/ADR-006-postgresql-persistence.md): PostgreSQL persistent state.
- [ADR-024](../docs/03-decisions/ADR/ADR-024-database-driven-boss-balance-configuration.md): Database-driven Boss balance configuration (static Domain baseline + PostgreSQL overlay).
- [ADR-026](../docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md): Shared-board gameplay and actor-relative match resolution.
- [ADR-029](../docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md): Boss as a genuine opponent — Boss Power, Boss Cards, one-action response policy (Accepted; Slice 1 and Slice 2 implemented).

### Unresolved Decisions & Open Items
- `ADR-029` §3 remains the owner of the unresolved Boss balance values: **U-2** (per-ability Power costs), **U-3** (cast-priority score and cadence), **U-4** (ability magnitudes — Earthquake Shield magnitude is implemented as 150 but is not recorded as authored), **U-5** (cooldown re-tuning), **U-6** (SkillCharge retirement questions).
- TASK-306 proposes additional operator decisions **U-7…U-12** (Boss Card provisioning authorization, identity value-form conflict, score carrier, first-card authored values, active-form migration signal, cooldown turn-count reading) in its design artifact. They are **not approved** and not recorded in `docs/`.
- No task is in `tasks/blocked/`; the blocker is the operator decision gate, recorded in the TASK-306 artifact.

---

## 4. Candidate Task Readiness

| Candidate Task ID | Lifecycle State | Dependencies Satisfied | Scope / Docs Aligned | Readiness Verdict |
| :--- | :--- | :--- | :--- | :--- |
| TASK-306 | Active WIP (`IN PROGRESS` → `IN REVIEW`) | TASK-303 / TASK-304 / TASK-305 completed | Authoritative docs and source inspected; a documentation conflict (Boss Card identity value form) is reported, not resolved | `DESIGN READY FOR OPERATOR APPROVAL`; runtime implementation blocked on U-2/U-3/U-4/U-7…U-12 |

---

## 5. Changed Areas & Recent Trajectory

- **Recent Focus Area:** ADR-029 migration — Boss Power foundation (Slice 1, TASK-304), shared actor-aware cast contract (Slice 2, TASK-305), now slice-boundary reconciliation and Boss Card definition design (TASK-306).
- **Core Affected Components (recent commits):**
  - `src/backend/GameServer.Domain/Battle/BossState.cs`, `src/backend/GameServer.Domain/Match3/ResourceGenerator.cs`
  - `src/backend/GameServer.Domain/Cards/CardCastExecutor.cs`, `CardCastActor.cs`, `CardCategory.cs`
  - `src/backend/GameServer.Application/Battle/BattleStateService.cs` (Boss response phase)
  - `src/frontend/client/src/game/scenes/BattleScene.ts`, `BattleEventPresenter.ts`
- **Not yet implemented:** Boss action policy, Boss Card definitions/provisioning, `BossCardCast`, client presentation of a Boss Card cast.
- **Uncommitted (this task):** the TASK-306 record, its design artifact, and this cache refresh.

---

## 6. Known Blockers

- **Operator decision gate (design-level):** `ADR-029` U-2/U-3/U-4 plus TASK-306's U-7…U-12. No Boss Card can be provisioned and Slice 3 cannot be implemented until they are decided.
- **Reported documentation conflict:** `DATABASE.md` §1/§3 fixes `CardDefinitionId` as `card-<kebab>`, while `CARD_RULES.md` §1.1 item 3 requires a Boss Card's id to be the ability `SkillId`. Requires an operator-authorized correction (`AGENTS.md` §4).
- **Reported, not fixed (pre-existing):** Boss `BaseDamage` doc/provisioning drift (two known failing Infrastructure tests), stale `BattleHub` projection comment, stale cooldown comment, and the missing category guard on Signature-Skill derivation.

---

## 7. Next Recommended Action

- **Status:** `DESIGN AWAITING OPERATOR DECISION`
- **Action:** operator review of the TASK-306 design report
  ([artifact](../tasks/artifacts/TASK-306-adr-029-slice-boundary-reconciliation-and-boss-card-definition-design.md)),
  then TASK-306's completion filing. After the decision set is granted, intake the
  documentation/ADR amendment task (proposal P-1), then the Boss Card
  provisioning prerequisite (P-2), in that order.
- **After TASK-306:** if the backlog remains empty and no operator decision is
  granted, the correct status is `NEEDS_TASK_INTAKE` / `BLOCKED` at the decision
  gate — not a silent work item.
