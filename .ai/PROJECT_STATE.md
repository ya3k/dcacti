# .ai/PROJECT_STATE.md — Project State Snapshot

**Version:** 1.0  
**Notice:** This document is an AI navigation cache, not a source of truth. It records compact, verifiable project status to prevent repetitive whole-repository audits. Authoritative sources remain `docs/` (rules/architecture), `AGENTS.md` (contract), and `tasks/` (lifecycle state).

---

## 1. Audit Metadata

| Property | Value |
| :--- | :--- |
| **Audited Branch** | `master` |
| **Audited HEAD SHA** | `1d62313a14edf315029e147ff72a957eb4cab2d4` (`1d62313`) |
| **Audit Timestamp** | 2026-10-12 |
| **Freshness Status** | `STALE` (working tree dirty — FAST is rejected; re-run baseline collection before selection) |
| **Working Tree Baseline** | Dirty (uncommitted `.ai/` workflow/harness/snapshot files, technical docs, task artifacts, and an active task file) |

---

## 2. Task Lifecycle Status

| Category | Count | Task IDs & Summary |
| :--- | :--- | :--- |
| **Last Verified Completed Task** | 1 | [TASK-262](../tasks/completed/TASK-262-shared-board-battle-presentation-and-event-handling.md) (`shared-board-battle-presentation-and-event-handling`) |
| **Active Tasks (`tasks/active/`)** | 1 | [TASK-263](../tasks/active/TASK-263-shared-board-actor-aware-swap-event-contract.md) (`shared-board-actor-aware-swap-event-contract`, uncommitted file) |
| **Blocked Tasks (`tasks/blocked/`)** | 0 | None |
| **Backlog Candidates (`tasks/backlog/`)** | 0 | None |
| **Total Completed Tasks (`tasks/completed/`)** | 269 | Through TASK-262 (including sub-task sequences) |

---

## 3. Decisions & Governance Context

### Approved Key Architectural Decisions
- [ADR-001](../docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md): Server-authoritative gameplay resolution.
- [ADR-002](../docs/03-decisions/ADR/ADR-002-modular-monolith.md): Modular monolith structure (.NET backend).
- [ADR-003](../docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md): Phaser 4 client battle runtime with React UI shell.
- [ADR-004](../docs/03-decisions/ADR/ADR-004-signalr-realtime.md) / [ADR-005](../docs/03-decisions/ADR/ADR-005-redis-active-battle-state.md): SignalR event dispatch and Redis active battle state.
- [ADR-006](../docs/03-decisions/ADR/ADR-006-postgresql-persistence.md): PostgreSQL persistent state.
- [ADR-025](../docs/03-decisions/ADR/ADR-025-swap-action-preconditions-and-idempotency.md): Swap action preconditions and idempotency.
- [ADR-026](../docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md): Shared-board gameplay and actor-relative match resolution.

### Unresolved Decisions & Open Items
- None currently recorded in `tasks/blocked/`. Active WIP is TASK-263 (not re-readiness-audited in this tooling check).

---

## 4. Candidate Task Readiness

| Candidate Task ID | Lifecycle State | Dependencies Satisfied | Scope / Docs Aligned | Readiness Verdict |
| :--- | :--- | :--- | :--- | :--- |
| TASK-263 | Active WIP (uncommitted file) | Not re-validated in this tooling check | Not re-validated in this tooling check | `ACTIVE_WIP` — complete or unblock; do not pick a new backlog task (WIP limit = 1) |

---

## 5. Changed Areas & Recent Trajectory

- **Recent Focus Area:** Shared-board backend round orchestration (through TASK-261), client presentation (TASK-262), now actor-aware swap event contract (TASK-263).
- **Core Affected Components (recent commits):**
  - `src/backend/GameServer.Domain/Entities/BattleState.cs`
  - `src/backend/GameServer.Application/Services/BattleService.cs`
  - `src/frontend/client/src/game/scenes/BattleScene.ts`
  - `docs/01-game-design/*`, `docs/02-technical/GAME_STATE.md`, `REDIS_STATE.md`
- **Uncommitted (observed at snapshot refresh):** `.ai/` harness/workflow/snapshot files, `.claude/` + `.agents/` adapters, `docs/02-technical/GAME_EVENTS.md`, `docs/02-technical/SIGNALR_PROTOCOL.md`, task artifacts, and TASK-263.

---

## 6. Known Blockers

- No tasks currently in `tasks/blocked/`.
- Tooling limitations: OpenCode and DSH have no runtime adapters; harness support there is reference-only. Snapshot is not auto-updated and is marked `STALE` while the working tree is dirty.

---

## 7. Next Recommended Action

- **Status:** `ACTIVE_WIP`
- **Action:** Per WIP limit = 1, complete or resolve active [TASK-263](../tasks/active/TASK-263-shared-board-actor-aware-swap-event-contract.md) before any new intake. Empty backlog is not a stop while an active task exists.
- **After TASK-263:** If backlog remains empty, intake the next prioritized item per [tasks/README.md](../tasks/README.md) §6 and [tasks/TASK_TEMPLATE.md](../tasks/TASK_TEMPLATE.md).
