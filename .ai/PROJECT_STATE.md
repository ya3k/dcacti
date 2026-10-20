# .ai/PROJECT_STATE.md — Project State Snapshot

**Version:** 1.5

**Notice:** This document is an AI navigation cache, not a source of truth. It records compact, verifiable project status to prevent repetitive whole-repository audits. Authoritative sources remain `docs/` (rules/architecture), `AGENTS.md` (contract), and `tasks/` (lifecycle state).

---

## 1. Audit Metadata

| Property | Value |
| :--- | :--- |
| **Audited Branch** | `master` |
| **Audited HEAD SHA** | `857aa985a67caabb2f7aa85e973482e79237e970` (`857aa98`) — TASK-309's Phase A (`4d97c74`, 46 files) and Phase B (`857aa98`, completion record) commits are filed |
| **Upstream** | `origin/master` is at `081f9d7`; the branch is ahead by the two TASK-309 commits. Nothing is pushed. |
| **Audit Timestamp** | 2026-10-20 |
| **Audit Depth** | INCREMENTAL — the previous cache recorded `081f9d7`; the delta is TASK-309's two commits and TASK-310's uncommitted review-finding reconciliation. Bounded to the TASK-309/TASK-310 declared file sets. |
| **Freshness Status** | `STALE` — the audit baseline is `857aa98`; TASK-310's Phase A/Phase B commits will advance HEAD. Re-run baseline collection before the next selection. |
| **Working Tree Baseline** | HEAD `857aa98` tree; TASK-310's declared reconciliation files (5 docs, one comment-only Domain file, one Application test addition, this cache) and its task record are the uncommitted/in-flight set. No client file is changed. |

---

## 2. Task Lifecycle Status

| Category | Count | Task IDs & Summary |
| :--- | :--- | :--- |
| **Last Verified Completed Task** | 1 | [TASK-309](../tasks/completed/TASK-309-adr-029-slice-4-remaining-boss-card-abilities.md) (`adr-029-slice-4-remaining-boss-card-abilities`; implementation `4d97c74`, completion record `857aa98`) |
| **Active Tasks (`tasks/active/`)** | 1 | [TASK-310](../tasks/active/TASK-310-reconcile-task-309-review-findings.md) (`reconcile-task-309-review-findings`) — **IN PROGRESS**: closing the independent review's documentation-consistency findings; two-stage commit pending |
| **Blocked Tasks (`tasks/blocked/`)** | 0 | None |
| **Backlog Candidates (`tasks/backlog/`)** | 0 | None |
| **Total Completed Tasks (`tasks/completed/`)** | 311 | Records through TASK-309 |
| **Highest Completed Task ID** | 309 | TASK-309 (implementation `4d97c74`, completion record `857aa98`) |
| **Highest Existing Task ID** | 310 | TASK-310 allocated (309 + 1, active); next sequential ID = 311 |

### TASK-309 approved values and delivered scope (task record §1)

- Four remaining abilities migrated to `BossCard` form: `flame-burst` (`PowerCost 40`, cooldown `2`, Boss self `+50%` ATK for `3` Turns), `root` (`35`, cooldown `2`, active-Pet `−30%` ATK for `3` Turns), `drain-power` (`25`, cooldown `3`, active-Pet `−20` flat Power instant, no transfer to the Boss), `thunder-strike` (`40`, cooldown `0`, `+50%` Boss ATK on the next qualifying attack). All four keep `BaseDamage = 0`.
- `CastPriorityScore = 85` on Hỏa Long, Thủy Ma, Mộc Yêu and Kim Lôi Vương; Sơn Thạch Vệ keeps `16`. `85` is a starting value to monitor, not globally optimal, and guarantees no in-battle cast rate (in particular no `38%`).
- New Boss-only `CardEffectType` identities `AtkBuff` / `PowerDrain` / `AtkDebuff` / `NextAtkBuff` (D-1), extending the `duration` member to `AtkBuff`/`AtkDebuff` and the `scope` member to `NextAtkBuff` (`NextAttack` only) with no new member name. Player Card casts may not use the four; `PowerDrain` transfers no Power.
- The Boss Card forms of `flame-burst`/`root` carry the current-approved duration `3`; the transitional legacy `BossSkill` form keeps its historical persisted `2` until Slice 5 retirement (explicit documented divergence).
- B-4 preserves multiplicative ATK stacking unchanged (`1.20 × 1.50 = 1.80`); additive stacking is not authorized.

### TASK-310 review-finding reconciliation (in flight)

- The independent TASK-309 review passed implementation, migrations, tests, scope, security, determinism and performance; its only blocking finding was documentation consistency: `CARD_RULES.md` §3 item 9, `COMBAT_RULES.md` §2 and `GAME_STATE.md` §2.4.3 item 8 still called ADR-029 §3 U-2 (per-ability Boss Card `PowerCost`) unresolved after TASK-309 resolved it.
- TASK-310 (BUG, LOW risk) corrects those three statements to the approved values (`earthquake 30`; `flame-burst 40`, `drain-power 25`, `root 35`, `thunder-strike 40`), fixes `BOSS_RULES.md` §4.4 item 3's stale Slice 4 migration order and `GAME_EVENTS.md` §2's pre-ADR-029 Boss-phase step labels, corrects a comment-only stale enum example in `CardEffectDefinition.cs`, and adds the migrated-card `thunder-strike` no-ATK-round survival/consumption test the review found missing.
- TASK-309's completed record is immutable and is not edited.

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
- [ADR-029](../docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md): Boss as a genuine opponent — Boss Power, Boss Cards, one-action response policy (**Accepted, amended in place by TASK-308 and TASK-309**). §3's U-2/U-3/U-4/U-5 are resolved; U-6 remains gated. Slices 1–4 delivered; Slice 5 not started.

### Unresolved / Deferred Items
- **ADR-029 §3 U-6** — retirement of `SkillCharge`'s own unit-metric and clamp questions: gated by §2.9's four-part parity gate (Slice 5).
- **Slice 5** — retire `SkillCharge` / `BossSkillCast` / legacy handler only after ADR-029 §2.9's parity gate is met. Not started.
- The fifth Gem / Power-drain Gem remains FUTURE (`MVP_SCOPE.md` §3).

### TASK-309 Verification Gate Evidence (confirmed by the Lead)

- **V-1 baseline (reproducible 10,000-board simulation):** Pcg32 stream, master seed `0x5EED030900000001`, increment `0x0309`; `P(boardScore < 85) = 36.97%`, `P(< 25) = 0.00%`, `P(< 100) = 66.43%`; ordered-score SHA-256 digest `7b477ada5ed81d5f064a9ff752a3d62996a42531f7a465e64bdffab35488f940`; artifact [TASK-309-boss-cast-priority-baseline.json](../tasks/artifacts/TASK-309-boss-cast-priority-baseline.json). **Board-only measurement — not an in-battle cast rate.**
- **V-1 in-battle analysis (real pipeline):** 40 battles/boss with seeds `0x5EED030900000001 + b`; 9,665 Boss response phases / 2,903 casts; cast frequency flame-burst `30.28%`, drain-power `26.85%`, root `31.66%`, thunder-strike `31.20%`, aggregate `30.04%`; Power eligible `71.30%`, cooldown eligible `68.47%`, both `48.49%`; zero contractual violations; artifact [TASK-309-in-battle-cast-frequency.json](../tasks/artifacts/TASK-309-in-battle-cast-frequency.json). **No ~38% in-battle guarantee; the board-only and in-battle measurements are distinct.**
- **V-2 (status-effect lifecycle):** Application tests prove `flame-burst` duration 3 affects at most two qualifying Boss attacks and expires through a no-attack round; `root` mirrors it for Player attacks; `thunder-strike` is consumed exactly once; multiplicative stacking `1.20 × 1.50 = 180` is unchanged.
- **V-3 (migration reversibility):** portable PostgreSQL 18.6.1 on a disposable cluster/port `5544`; `dotnet ef database update` applied the four migrations in order (13 `CardDefinition` rows; scores `85/85/85/16/85`; `earthquake` and Player Cards unchanged; schema unchanged); revert restored the four rows' absence and `NULL` scores byte-identically; re-apply was identical; cluster deleted; no production database touched.
- **Test suites (final gate, Lead-run):** Domain `1691/1691`; Application `827 passed / 2 failed / 829 total` (includes the two V-1 in-battle analysis tests); Infrastructure `502 passed / 4 failed / 506 total`; Api `405/405`. The six failures are exactly the documented pre-existing ones (§6) and are unchanged by TASK-309.
- **Frontend suite/build (confirmed by the Lead):** `npm run test:run` — 24 files, `1271/1271` passed (exit 0); `npm run build` — `tsc` + Vite success (exit 0). No client file changed (TASK-309 declares no client change).

---

## 4. Candidate Task Readiness

| Candidate Task ID | Lifecycle State | Dependencies Satisfied | Scope / Docs Aligned | Readiness Verdict |
| :--- | :--- | :--- | :--- | :--- |
| TASK-309 | IN REVIEW (`IN PROGRESS` → `IN REVIEW` → `DONE`) | TASK-303…TASK-308 completed; operator B-1/B-2/B-3/B-4/D-1 and the four Power costs approved | Authoritative docs/ADR amended; four provisioned Boss Cards + scores; vocabulary/executor; round/event ordering; gates V-1/V-2/V-3 complete | Implementation and validation complete; two-stage commit pending |
| Slice 5 — retirement | **NOT REGISTERED** | Needs Slice 4 (TASK-309) DONE plus ADR-029 §2.9's four-part parity gate | — | Gated |
| Fifth Gem / Power-drain Gem | **FUTURE** | — | `MVP_SCOPE.md` §3 | Out of scope until its own approval |

---

## 5. Changed Areas & Recent Trajectory

- **Recent Focus Area:** ADR-029 Slice 4 (TASK-309) — migrating the four remaining Boss abilities to Boss Cards: approved costs/durations/scores recorded in the owning docs/ADR, the four Boss-only `CardEffectType` identities and shared-executor resolution, the four data-only content migrations, the `PowerDrain` → `PowerChanged` ordering in the round's single ordered batch, and the V-1/V-2/V-3 verification gates.
- **Core Affected Components (recent code):**
  - `src/backend/GameServer.Domain/Cards/CardEffectType.cs`, `CardEffectDefinition.cs`, `CardEffectDefinitions.cs`, `CardCastExecutor.cs`
  - `src/backend/GameServer.Domain/Bosses/BossDefinitions.cs` (`CastPriorityScore = 85` on the four Bosses; legacy `SecondaryEffect` values/durations untouched)
  - `src/backend/GameServer.Application/Battle/BattleStateService.cs` (cast-effect events carried before `BossCardCast`; no Boss `PowerChanged`)
  - `src/backend/GameServer.Infrastructure/Postgres/Migrations/20261020111406_ProvisionFlameBurstBossCard`, `20261020111411_ProvisionRootBossCard`, `20261020111415_ProvisionDrainPowerBossCard`, `20261020111419_ProvisionThunderStrikeBossCard` (data-only, ordered; mirrored `Down`)
  - Tests: `tests/backend/GameServer.Domain.Tests/TASK309BossCastPriorityBoardSimulationTests.cs`, `tests/backend/GameServer.Application.Tests/TASK309BossCardEffectRuntimeTests.cs`, `TASK309StatusEffectLifecycleTests.cs`, `tests/backend/GameServer.Application.Tests/Balance/TASK309/BossCardCastFrequencyAnalysisTests.cs`, `tests/backend/GameServer.Infrastructure.Tests/TASK309BossCardProvisioningMigrationTests.cs`, `tests/backend/GameServer.Api.Tests/TASK309BossCardWireTests.cs`, plus updated existing Domain/Application/Infrastructure/Api tests.
- **Authoritative documentation amended:** `docs/01-game-design/BOSS_RULES.md` v2.16, `docs/02-technical/DATABASE.md` v1.41, `docs/02-technical/GAME_EVENTS.md` v2.19, `docs/03-decisions/ADR/ADR-029-…md` (amended 2026-10-20), `docs/03-decisions/README.md` v1.24.
- **Now verified present (was absent before TASK-309):** four provisioned `BossCard` rows `flame-burst` / `root` / `drain-power` / `thunder-strike` (13 `CardDefinition` rows total); `CastPriorityScore = 85` on the four Bosses; the four Boss-only `CardEffectType` identities with `duration`/`scope` validation; `PowerDrain` emitting the existing `PowerChanged` before `BossCardCast`; the two V-1 engineering artifacts.
- **Uncommitted (this task):** the TASK-309 declared implementation set, its two artifacts, its task record, and this cache refresh.

---

## 6. Known Blockers

- **None for TASK-310.** Its input findings are recorded in its task record; the only remaining step is the two-stage commit.
- **Pre-existing test failures (confirmed unchanged at HEAD; not introduced by TASK-309):**
  - `GameServer.Infrastructure.Tests`: 4 failures — `BossPersistenceTests.ProvisioningMigration_ShouldWriteTheDocumentedPassiveAndSkillDocuments`, `BossPersistenceTests.Task172ProvisioningMigration_ShouldWriteTheDocumentedPassiveAndSkillDocuments`, `BossDefinitionPostgresProvisioningTests.Postgres_ShouldRoundTripBothJsonDocumentsToTheDomainDefinitions`, `BossDefinitionBalanceLookupTests.GetDefinitionByIdentityAsync_ShouldPreserveTheNonPersistedSecondaryEffects` (historical `BaseDamage` 150/180 vs Domain `0` drift; flame-burst `Burn` vs `AtkBuff` expectation).
  - `GameServer.Application.Tests`: 2 balance-evidence failures — `BalanceEvidenceMeasurementCorrectionTests.TASK195_PerTurnSeries_ReconcilesTerminalCardCastWithoutPhantomTurn`, `TASK296_ReviewD_Canonical75RunCandidate_Verification` (pre-existing historical-evidence mismatch; unchanged by TASK-309).
- **Reported, not fixed:** the historical Boss provisioning `BaseDamage` drift, the stale `BattleHub` comment, and the Signature-Skill category guard (P-8); plus documentation drift observed during TASK-309 — `BOSS_RULES.md` §6's MVP reference table still records the retired direct-damage/Burn values, and `GAME_EVENTS.md` retains a few pre-ADR-029 step-number references outside the amended clauses. These are outside TASK-309's approved content and need their own reconciliation task.

---

## 7. Next Recommended Action

- **Status:** `TASK-310_COMMIT_PENDING`
- **Action:** complete TASK-310's two-stage commit — Phase A with the exact declared reconciliation set (5 docs + comment-only Domain file + one Application test addition + this cache refresh), then Phase B with its completion record.
- **After TASK-310:** Slice 5 remains gated by ADR-029 §2.9's four-part parity gate; the fifth Gem remains FUTURE. Do not push without explicit authorization.
