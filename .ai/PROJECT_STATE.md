# .ai/PROJECT_STATE.md — Project State Snapshot

**Version:** 1.6

**Notice:** This document is an AI navigation cache, not a source of truth. It records compact, verifiable project status to prevent repetitive whole-repository audits. Authoritative sources remain `docs/` (rules/architecture), `AGENTS.md` (contract), and `tasks/` (lifecycle state).

---

## 1. Audit Metadata

| Property | Value |
| :--- | :--- |
| **Audited Branch** | `master` |
| **Audited HEAD SHA** | `36137e3dbc6a25675e6c68c9b00468a2812dc1f4` (`36137e3`) — TASK-310's Phase A (`2112723`) and Phase B (`36137e3`) commits are filed |
| **Upstream** | `origin/master` is at `081f9d7`; the branch is ahead by four commits (two from TASK-309, two from TASK-310). Nothing is pushed. |
| **Audit Timestamp** | 2026-10-20 |
| **Audit Depth** | INCREMENTAL — base commit is `36137e3`; delta bounded to the TASK-311 declared file set (documentation-first reconciliation; no runtime code or schema changes). |
| **Freshness Status** | `STALE` — the audit baseline is `36137e3`; TASK-311's Phase A/Phase B commits will advance HEAD. |
| **Working Tree Baseline** | HEAD `36137e3` tree; TASK-311's declared reconciliation set (artifact, ADR-029, README, BOSS_RULES, COMBAT_RULES, GAME_EVENTS, SIGNALR_PROTOCOL, GAME_STATE, DATABASE, REDIS_STATE, this cache) and its active task record in flight. |

---

## 2. Task Lifecycle Status

| Category | Count | Task IDs & Summary |
| :--- | :--- | :--- |
| **Last Verified Completed Task** | 1 | [TASK-310](../tasks/completed/TASK-310-reconcile-task-309-review-findings.md) (`reconcile-task-309-review-findings`; implementation `2112723`, completion record `36137e3`) |
| **Active Tasks (`tasks/active/`)** | 1 | [TASK-311](../tasks/active/TASK-311-adr-029-slice-5-removal-readiness-reconciliation.md) (`adr-029-slice-5-removal-readiness-reconciliation`) — **IN PROGRESS**: reconciling ADR-029 §2.9 Slice 5 readiness gaps |
| **Blocked Tasks (`tasks/blocked/`)** | 0 | None |
| **Backlog Candidates (`tasks/backlog/`)** | 0 | None |
| **Total Completed Tasks (`tasks/completed/`)** | 312 | Records through TASK-310 |
| **Highest Completed Task ID** | 310 | TASK-310 (implementation `2112723`, completion record `36137e3`) |
| **Highest Existing Task ID** | 311 | TASK-311 allocated (310 + 1, active); next sequential ID = 312 |

---

## 3. Decisions & Governance Context

### Approved Key Architectural Decisions
- [ADR-001](../docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md): Server-authoritative gameplay resolution.
- [ADR-002](../docs/03-decisions/ADR/ADR-002-modular-monolith.md): Modular monolith structure (.NET backend).
- [ADR-003](../docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md): Phaser 4 client battle runtime with React UI shell.
- [ADR-004](../docs/03-decisions/ADR/ADR-004-signalr-realtime.md) / [ADR-005](../docs/03-decisions/ADR/ADR-005-redis-active-battle-state.md): SignalR event dispatch and Redis active battle state.
- [ADR-006](../docs/03-decisions/ADR/ADR-006-postgresql-persistence.md): PostgreSQL persistent state.
- [ADR-024](../docs/03-decisions/ADR/ADR-024-database-driven-boss-balance-configuration.md): Database-driven Boss balance configuration (**Accepted, amended by TASK-308**: Domain Overlay now five values, fifth nullable `CastPriorityScore`).
- [ADR-026](../docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md): Shared-board gameplay and actor-relative match resolution (Accepted; POWER bullet partially superseded by ADR-029).
- [ADR-029](../docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md): Boss as a genuine opponent — Boss Power, Boss Cards, one-action response policy (**Accepted, amended by TASK-308, TASK-309, and TASK-311**). Slices 1–4 delivered; Slice 5 removal-readiness prerequisites reconciled under TASK-311.

### Unresolved / Deferred Items
- **ADR-029 §3 U-6 / Slice 5** — physical removal of `SkillCharge` / `BossSkillCast` / legacy handler: removal-readiness prerequisites reconciled under TASK-311; implementation remains gated and not started.
- **The fifth Gem / Power-drain Gem** — remains FUTURE (`MVP_SCOPE.md` §3).

---

## 4. Candidate Task Readiness

| Candidate Task ID | Lifecycle State | Dependencies Satisfied | Scope / Docs Aligned | Readiness Verdict |
| :--- | :--- | :--- | :--- | :--- |
| TASK-311 | IN PROGRESS | TASK-309, TASK-310 completed | Authoritative docs amended; five-Boss comparison artifact filed; gate terminology reconciled; consumer inventory audited | Implementation complete; two-stage commit pending |
| Slice 5 — removal | **NOT REGISTERED** | Needs TASK-311 DONE; §2.9 prerequisite gate reconciled | Technical documentation and consumer requirements prepared | **READY FOR SLICE 5 REGISTRATION REVIEW** (implementation gated) |
| Fifth Gem / Power-drain Gem | **FUTURE** | — | `MVP_SCOPE.md` §3 | Out of scope until separate authorization |

---

## 5. Changed Areas & Recent Trajectory

- **Recent Focus Area:** ADR-029 Slice 5 Removal-Readiness Reconciliation (TASK-311) — establishing documentation-first prerequisites for eventual `SkillCharge`/`BossSkill` removal without altering runtime behavior.
- **Artifacts & Documentation Amended:**
  - `tasks/artifacts/TASK-311-adr-029-slice-5-removal-readiness-reconciliation.md` — comprehensive 5-Boss legacy-vs-card behavior comparison (including `thunder-strike` same-round attack consumption divergence and `flame-burst`/`root` duration divergence), consumer inventory, and Slice 5 readiness assessment.
  - `docs/01-game-design/BOSS_RULES.md` (v2.17) — §6 overview table reconciled to authoritative zero-direct-damage and Boss Card rules; gate terminology aligned to Slice 5 removal gate.
  - `docs/01-game-design/COMBAT_RULES.md` (v3.2) — gate terminology in §2 POWER bullet and preamble aligned to Slice 5 removal gate.
  - `docs/02-technical/GAME_EVENTS.md` (v2.20) — `BossSkillCast` annotated with future Slice 5 removal expectations; gate terminology aligned.
  - `docs/02-technical/SIGNALR_PROTOCOL.md` (v2.29) — `BossSkillCast` §3.2.18 annotated with transitional status and Slice 5 expectations; §3.2.30 gate terminology aligned.
  - `docs/02-technical/GAME_STATE.md` (v2.34) — four instances of "Slice 4 parity gate" reconciled to "Slice 5 removal gate".
  - `docs/02-technical/DATABASE.md` (v1.42) — `BossDefinition.SkillDefinition` annotated with Slice 5 removal scope and persistence disposition note.
  - `docs/02-technical/REDIS_STATE.md` (v1.17) — `BossState.SkillCharge` future retirement, tolerant-reader deserialization, and battle drain expectations annotated.
  - `docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md` — §2.9 item 25, §4 (Slice 5), and §7 amended in place.
  - `docs/03-decisions/README.md` (v1.25) — index updated for ADR-029 in-place amendment.

---

## 6. Known Blockers & Deferred Issues

- **None for TASK-311.**
- **Pre-existing test failures (confirmed unchanged at HEAD; not introduced by TASK-309, TASK-310, or TASK-311):**
  - `GameServer.Infrastructure.Tests`: 4 failures — `BossPersistenceTests.ProvisioningMigration_ShouldWriteTheDocumentedPassiveAndSkillDocuments`, `BossPersistenceTests.Task172ProvisioningMigration_ShouldWriteTheDocumentedPassiveAndSkillDocuments`, `BossDefinitionPostgresProvisioningTests.Postgres_ShouldRoundTripBothJsonDocumentsToTheDomainDefinitions`, `BossDefinitionBalanceLookupTests.GetDefinitionByIdentityAsync_ShouldPreserveTheNonPersistedSecondaryEffects` (historical `BaseDamage` 150/180 vs Domain `0` drift; flame-burst `Burn` vs `AtkBuff` expectation). Classified: must be resolved during future Slice 5 implementation; does not block ADR-029 or TASK-311.
  - `GameServer.Application.Tests`: 2 balance-evidence failures — `BalanceEvidenceMeasurementCorrectionTests.TASK195_PerTurnSeries_ReconcilesTerminalCardCastWithoutPhantomTurn`, `TASK296_ReviewD_Canonical75RunCandidate_Verification` (pre-existing historical-evidence mismatch; unchanged). Classified: non-blocker.
- **P-8 Signature-Skill category guard:** Advisory category guard noted in TASK-309; non-blocker.
- **Unresolved Operator Decisions (Carried to Slice 5):**
  - `OD-311-1`: PostgreSQL JSONB storage disposition for `chargeRequirement` (physical data migration vs deprecated JSON property).
  - `OD-311-2`: Wire event deprecation window for `BossSkillCast` (single breaking release with battle drain vs temporary tolerant-reader mapping).

---

## 7. Next Recommended Action

- **Status:** `TASK-311_PHASE_A_COMMIT_PENDING`
- **Action:** Execute two-stage commit protocol for TASK-311:
  1. Phase A: commit the exact 11 declared implementation files (`tasks/artifacts/TASK-311-...`, 9 docs, this cache).
  2. Phase B: file completion record in `tasks/completed/TASK-311-adr-029-slice-5-removal-readiness-reconciliation.md` with Phase A SHA.
- **After TASK-311:** Slice 5 is reviewable and registrable, but remains unauthorized and unimplemented. The Fifth Gem remains FUTURE. Do not push without authorization.
