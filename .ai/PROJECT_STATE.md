# .ai/PROJECT_STATE.md — Project State Snapshot

**Version:** 1.3

**Notice:** This document is an AI navigation cache, not a source of truth. It records compact, verifiable project status to prevent repetitive whole-repository audits. Authoritative sources remain `docs/` (rules/architecture), `AGENTS.md` (contract), and `tasks/` (lifecycle state).

---

## 1. Audit Metadata

| Property | Value |
| :--- | :--- |
| **Audited Branch** | `master` |
| **Audited HEAD SHA** | `f6ddbd90c042e57c50288ed732a7deb63a419089` (`f6ddbd9`) |
| **Upstream** | `origin/master`, branch ahead 4 (the four TASK-306/307 commits; nothing pushed) |
| **Audit Timestamp** | 2026-10-19 |
| **Audit Depth** | INCREMENTAL — the previous cache recorded `94d40d0`; the delta was the TASK-307 artifact, completion record and cache. TASK-308's own Phase A/Phase B commits advance HEAD after this audit. Multi-boundary work (docs + schema + Domain + Application + Api + client) was bounded to the TASK-308 declared file set. |
| **Freshness Status** | `STALE` — the audit baseline is `f6ddbd9`; TASK-308's Phase A/Phase B commits advance HEAD. Re-run baseline collection before the next selection. |
| **Working Tree Baseline** | Clean at audit start; TASK-308's declared implementation files, its task record, and this cache are the in-flight set. |

---

## 2. Task Lifecycle Status

| Category | Count | Task IDs & Summary |
| :--- | :--- | :--- |
| **Last Verified Completed Task** | 1 | [TASK-307](../tasks/completed/TASK-307-adr-029-reconciliation-and-slice-3-readiness.md) (`adr-029-reconciliation-and-slice-3-readiness`, filed at `f6ddbd9`) |
| **Active Tasks (`tasks/active/`)** | 1 | [TASK-308](../tasks/active/TASK-308-adr-029-slice-3-boss-card-end-to-end.md) (`adr-029-slice-3-boss-card-end-to-end`) — implementation + validation complete; two-stage commit in progress |
| **Blocked Tasks (`tasks/blocked/`)** | 0 | None |
| **Backlog Candidates (`tasks/backlog/`)** | 0 | None |
| **Total Completed Tasks (`tasks/completed/`)** | 309 | Records through TASK-307 |
| **Highest Existing Task ID** | 308 | TASK-308 allocated (307 + 1); next sequential ID = TASK-309 |

---

## 3. Decisions & Governance Context

### Approved Key Architectural Decisions
- [ADR-001](../docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md): Server-authoritative gameplay resolution.
- [ADR-002](../docs/03-decisions/ADR/ADR-002-modular-monolith.md): Modular monolith structure (.NET backend).
- [ADR-003](../docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md): Phaser 4 client battle runtime with React UI shell.
- [ADR-004](../docs/03-decisions/ADR/ADR-004-signalr-realtime.md) / [ADR-005](../docs/03-decisions/ADR/ADR-005-redis-active-battle-state.md): SignalR event dispatch and Redis active battle state.
- [ADR-006](../docs/03-decisions/ADR/ADR-006-postgresql-persistence.md): PostgreSQL persistent state.
- [ADR-024](../docs/03-decisions/ADR/ADR-024-database-driven-boss-balance-configuration.md): Database-driven Boss balance configuration (**Accepted, amended in place by TASK-308**: Domain Overlay now five values, the fifth nullable `CastPriorityScore`).
- [ADR-026](../docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md): Shared-board gameplay and actor-relative match resolution (Accepted; POWER bullet partially superseded by ADR-029).
- [ADR-029](../docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md): Boss as a genuine opponent — Boss Power, Boss Cards, one-action response policy (**Accepted, amended in place by TASK-308**: Slice 3 definition/provisioning folded in; §4 exit criteria reconciled; `earthquake` initial values recorded). Slices 1–3 implemented.

### Operator-Approved TASK-308 Content (recorded in the task record §1)
- `earthquake.PowerCost = 30`; `earthquake` self `Shield` `150 Flat`; `earthquake.CastPriorityScore = 16`.
- Nullable `BossDefinition.CastPriorityScore` overlay; active form = provisioned Boss Card presence, resolved once at creation, fail closed, only Sơn Thạch Vệ enabled.
- These are initial implementation/testing values, not evidence of final balance optimization.

### Unresolved / Deferred Items
- **Slice 4**: the other four abilities; each needs its own approved effect-vocabulary extension (flame-burst → root → drain-power → thunder-strike last; U-2/U-4/U-5 remain open).
- **Slice 5**: retire `SkillCharge`/`BossSkillCast`/legacy handler only after ADR-029 §2.9's four-part parity gate.
- **U-3 cadence observation**: with the approved score `16`, the board action wins whenever the best candidate scores above `15`; a deterministic sample of post-player boards had a minimum best score of `40`, so the cast is technically selectable but practically rare in ordinary play. The value is operator-approved and was not tuned by the implementation; any re-tuning is future balance work.
- The fifth Gem / Power-drain Gem remains FUTURE (`MVP_SCOPE.md` §3).

---

## 4. Candidate Task Readiness

| Candidate Task ID | Lifecycle State | Dependencies Satisfied | Scope / Docs Aligned | Readiness Verdict |
| :--- | :--- | :--- | :--- | :--- |
| TASK-308 | Active (`IN PROGRESS` → `IN REVIEW` → `DONE`) | TASK-303/304/305/306/307 completed; OD-1c/1d/2a/2b/3a/3b approved | Authoritative docs/ADR amended; provisioned card + score; policy/round/event/client implemented; gates run | Implementation and validation complete; two-stage commit pending |
| Slice 4 — remaining four Boss abilities | **NOT REGISTERED** | Needs TASK-308 DONE plus a separately approved effect-vocabulary extension | Not yet specified | Register only after that approval |
| Slice 5 — retirement | **NOT REGISTERED** | Needs Slice 4 plus ADR-029 §2.9's parity gate | — | Gated |

---

## 5. Changed Areas & Recent Trajectory

- **Recent Focus Area:** ADR-029 migration — Boss Power (Slice 1, TASK-304), shared actor cast contract (Slice 2, TASK-305), slice/definition reconciliation (TASK-306/307), and the end-to-end Boss Card slice (TASK-308: docs/ADR, provisioned `earthquake` row, nullable `CastPriorityScore`, `BossActionPolicy`, atomic round integration, `BossCardCast` wire + client presentation).
- **Core Affected Components (recent code):**
  - `src/backend/GameServer.Domain/Bosses/BossActionPolicy.cs`, `BossDefinition.cs`, `BossDefinitions.cs`
  - `src/backend/GameServer.Domain/Match3/BattleEvent.cs`
  - `src/backend/GameServer.Application/Battle/BattleStateService.cs`
  - `src/backend/GameServer.Infrastructure/Postgres/Configurations/BossDefinitionConfiguration.cs`, `Repositories/BossDefinitionLookup.cs`, two TASK-308 migrations
  - `src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs`
  - `src/frontend/client/src/game/scenes/BattleEventPresenter.ts`, `BattleScene.ts`
- **Now verified present (was absent before TASK-308):** `BossActionPolicy` implementation; the provisioned `earthquake` `CardDefinition` (9th content row) with `PowerCost 30` / `Shield 150 Flat`; the nullable `CastPriorityScore` overlay (`earthquake = 16`); `BossCardCast` enum member `20`, factory, wire projection and client presentation.
- **Uncommitted (this task):** the TASK-308 declared file set, its record, and this cache refresh.

---

## 6. Known Blockers

- **None for TASK-308.** All operator decisions required by the slice are recorded.
- **Pre-existing test failures (confirmed unchanged at HEAD; not introduced by TASK-308):**
  - `GameServer.Infrastructure.Tests`: 4 failures — `BossPersistenceTests.ProvisioningMigration_ShouldWriteTheDocumentedPassiveAndSkillDocuments`, `BossPersistenceTests.Task172ProvisioningMigration_ShouldWriteTheDocumentedPassiveAndSkillDocuments`, `BossDefinitionPostgresProvisioningTests.Postgres_ShouldRoundTripBothJsonDocumentsToTheDomainDefinitions`, `BossDefinitionBalanceLookupTests.GetDefinitionByIdentityAsync_ShouldPreserveTheNonPersistedSecondaryEffects` (historical `BaseDamage` 150/180 vs Domain `0` drift; flame-burst `Burn` vs `AtkBuff` expectation).
  - `GameServer.Application.Tests`: 2 balance-evidence failures — `BalanceEvidenceMeasurementCorrectionTests.TASK195_PerTurnSeries_ReconcilesTerminalCardCastWithoutPhantomTurn`, `TASK296_ReviewD_Canonical75RunCandidate_Verification` (pre-existing historical-evidence mismatch; reproduced identically at HEAD `f6ddbd9`).
- **Reported, not fixed:** the historical Boss provisioning `BaseDamage` drift, the stale `BattleHub` comment, and the Signature-Skill category guard (P-8) remain outside this slice.

---

## 7. Next Recommended Action

- **Status:** `TASK-308_PHASE_B_PENDING`
- **Action:** finish TASK-308's two-stage commit (Phase A implementation slice, then Phase B completion record and cache), then select the next eligible task.
- **After TASK-308:** obtain the operator's approved effect-vocabulary extension for **Slice 4** (migrate the other four abilities, `thunder-strike` last) before registering that task. Do not start Slice 5 until ADR-029 §2.9's parity gate is met. Do not push without explicit authorization.
