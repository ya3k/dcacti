# TASK-251 — Implement Database-Driven Boss Balance Configuration

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — does NOT duplicate game rules or schemas.
-->

<!--
  Lifecycle: BACKLOG → READY validation completed by the orchestrator role
  after validating tasks/TASK_LIFECYCLE.md §3 READY criteria:
    [x] Task type confirmed (FEATURE — tasks/TASK_TYPES.md §2 / development/feature.md)
    [x] Relevant documentation exists in docs/ (MVP_SCOPE.md §1, BOSS_RULES.md §5/§6.1/§6.3.1/§6.4,
        DATABASE.md §1/§1 note items 1–5/§3/§5 item 4, GAME_STATE.md §2.4, ARCHITECTURE.md §2.1/§3/§5,
        TDD.md §4 item 3, API_CONTRACTS.md §3/§6, ADR-006, ADR-024)
    [x] MVP scope confirmed (MVP_SCOPE.md §1 — Match-3 battle engine, five MVP bosses,
        PostgreSQL persistence; operational balance via migrations is within scope;
        dynamic GM/admin UI, Redis snapshot keys, and multi-instance recovery are excluded)
    [x] Not blocked: Dependencies: TASK-250 (ADR-024 authored and approved by the operator),
        TASK-248 (canonical Hỏa Long baseline restored)
    [x] Primary agent assigned (backend) and workflow assigned (development/feature.md
        over the ADR-024 architecture decision)
    [x] Acceptance criteria are binary and testable
    [x] Uniqueness: highest assigned ID across repository is TASK-250; next available is TASK-251
    [x] Ownership: tasks/active/, tasks/blocked/, and tasks/backlog/ hold no open record
        naming the declared file set

  Lifecycle: READY → IN PROGRESS completed by the backend agent under
  development/feature.md: the operator explicitly approved ADR-024 (Option 1 —
  Domain Overlay) and authorized TASK-251 to implement it.

  Lifecycle: IN PROGRESS → IN REVIEW completed by the backend agent upon
  implementing the Domain Overlay lookup, the EF Core mapping and migration, and
  the wiring into BattleStartService; adding the unit, PostgreSQL integration,
  and API tests; and running every validation check. Awaiting operator
  authorization for the Phase A implementation slice commit.

  Lifecycle: IN REVIEW → DONE completed 2026-10-11 by the backend agent: operator
  authorized TASK-251 Phase B completion record filing following Phase A commit
  5a1c5be75e15fca3436cc42e520a7b27f5f76efd. Moved to tasks/completed/.
-->

---

## Metadata

```text
Task ID:           TASK-251
Type:              FEATURE
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     backend
Supporting Agents: persistence, testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis, backend/persistence-analysis, backend/api-contract-validation, quality/architecture-conformance, quality/implementation-review, quality/scope-validation
Dependencies:      TASK-250 (ADR-024 approval), TASK-248 (canonical Hỏa Long baseline)
Declared Files:    src/backend/GameServer.Domain/Bosses/BossDefinition.cs, src/backend/GameServer.Application/Battle/IBossDefinitionLookup.cs, src/backend/GameServer.Application/Battle/BattleStartService.cs, src/backend/GameServer.Infrastructure/Postgres/Configurations/BossDefinitionConfiguration.cs, src/backend/GameServer.Infrastructure/Postgres/Repositories/BossDefinitionLookup.cs, src/backend/GameServer.Infrastructure/Postgres/GameDbContext.cs, src/backend/GameServer.Infrastructure/Postgres/Migrations/20261011042819_AddBossBalanceConfiguration.cs, src/backend/GameServer.Infrastructure/Postgres/Migrations/20261011042819_AddBossBalanceConfiguration.Designer.cs, src/backend/GameServer.Infrastructure/Postgres/Migrations/GameDbContextModelSnapshot.cs, tests/backend/GameServer.Api.Tests/ApplicationSessionRESTTests.cs, tests/backend/GameServer.Api.Tests/BattleStartEndpointTests.cs, tests/backend/GameServer.Api.Tests/BattleStartSmokeTest.cs, tests/backend/GameServer.Api.Tests/RedisBattleStateSmokeTest.cs, tests/backend/GameServer.Api.Tests/TestProvisionedContent.cs, tests/backend/GameServer.Application.Tests/BattleResultTestDoubles.cs, tests/backend/GameServer.Application.Tests/BattleStartServiceTests.cs, tests/backend/GameServer.Infrastructure.Tests/BossDefinitionBalanceLookupTests.cs, tests/backend/GameServer.Infrastructure.Tests/BossDefinitionPostgresProvisioningTests.cs, tests/backend/GameServer.Infrastructure.Tests/BossPersistenceTests.cs
```

---

## Objective

Implement the approved **ADR-024** design (Option 1 — Domain Overlay) so that
PostgreSQL is authoritative for a Boss's four runtime combat balance values
(`MaxHP`, `ATK`, `DEF`, `EnrageThreshold`) at battle creation: add the four
columns to the `BossDefinition` table with database check constraints and a
migration that populates the five canonical rows from `BOSS_RULES.md` §6.1,
implement `IBossDefinitionLookup.GetDefinitionByIdentityAsync` as the Domain
Overlay that keeps the structural definition (`Element`, Passive, Skill, and the
Skill's `SecondaryEffect`) from `BossDefinitions.All`, and wire it into
`BattleStartService` — failing closed on an absent or out-of-range row, keeping
the `BOSS_NOT_FOUND` HTTP 400 contract for an invalid client identity, and never
querying PostgreSQL on the swap/turn-resolution path.

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-024-database-driven-boss-balance-configuration.md` §2.1–§2.5 — the approved design: runtime authority, Domain Overlay, snapshot isolation, fail-closed validation, migration/rollback.
- `docs/00-overview/MVP_SCOPE.md` §1, §2 — five MVP bosses and PostgreSQL persistence are IN; dynamic admin tooling and multi-instance recovery are not.
- `docs/01-game-design/BOSS_RULES.md` §5 item 4 — the Enrage transition (`BossHP < MaxHP × EnrageThreshold`).
- `docs/01-game-design/BOSS_RULES.md` §6.1 — the canonical base-stat table the migration transcribes (Hỏa Long `5000 / 100 / 50 / 1500 (30%)`).
- `docs/01-game-design/BOSS_RULES.md` §6.3.1 — the Skill secondary effects the overlay must preserve (Burn, Drain Power, Root).
- `docs/01-game-design/BOSS_RULES.md` §6.4 — the canonical technical Boss Identity contract.
- `docs/02-technical/DATABASE.md` §1, §1 note items 1–5, §3, §5 item 4 — the `BossDefinition` persistence contract, constraint set, and provisioning/migration rules.
- `docs/02-technical/GAME_STATE.md` §0 item 5, §2.4 — single-representation rule and the `BossState` a battle is created with.
- `docs/02-technical/ARCHITECTURE.md` §2.1, §3, §5 — layer boundaries, the existing persistence lookup boundary, and anti-overengineering.
- `docs/02-technical/API_CONTRACTS.md` §3, §6 — `BOSS_NOT_FOUND` / HTTP 400 for an invalid `bossId`.
- `docs/02-technical/TDD.md` §4 item 3 — PostgreSQL is never queried on the hot path that resolves a single Swap.
- `docs/03-decisions/ADR/ADR-006-postgresql-persistence.md` — PostgreSQL for durable entities and configuration outside the hot resolution path.

---

## Scope

### In Scope
- The four runtime balance columns on PostgreSQL `BossDefinition`, their EF Core mapping, and the four database check constraints.
- One EF Core migration that adds the columns with transitional defaults, updates all five canonical rows to their exact `BOSS_RULES.md` §6.1 values before activating the constraints, and rolls back without deleting rows or touching foreign keys.
- `IBossDefinitionLookup.GetDefinitionByIdentityAsync` implemented as the Domain Overlay in `BossDefinitionLookup`.
- Wiring that lookup into `BattleStartService`'s Boss-resolution step.
- Focused unit, PostgreSQL integration, and API tests for the overlay, the structural preservation, the fail-closed paths, and the 400-versus-500 error semantics.
- Test-host provisioning of the canonical `BossDefinition` rows for isolated in-memory stores, which apply no migration.

### Out of Scope
- Redis configuration-snapshot persistence, restart recovery, and multi-instance turn resolution (`ADR-024` §2.3 defers them to a separate future task).
- Changing any boss balance value, gameplay rule, skill/passive magnitude, or the canonical baselines themselves.
- Persisting `BossSkillDefinition.SecondaryEffect` (ADR-024 Option C is deferred).
- Schema changes to any other table, SignalR/wire contracts, client presentation, or frontend behavior.
- Runtime GM/admin endpoints, live configuration reloaders, and `HasData`/model seeding (`MVP_SCOPE.md` §1; `DATABASE.md` §1 note item 5).
- Editing `ADR-024`, `docs/02-technical/DATABASE.md`, `docs/02-technical/ARCHITECTURE.md`, or `docs/03-decisions/README.md` — those paths are TASK-250's declared file set and are still uncommitted working-tree changes owned by that task.

---

## Current State

- `BossDefinitionConfiguration` explicitly ignored the four balance properties; the applied table held five columns.
- `BattleStartService.ResolveBoss` scanned `BossDefinitions.All` in memory, so balance was compile-time only.
- `IBossDefinitionLookup` exposed only the battle-end `FindBossDefinitionIdByIdentityAsync`.
- Two Infrastructure tests pinned the superseded "no combat-stat column" contract (`DATABASE.md` §1 note item 1 as it stood before TASK-250 synchronized it).

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-251:
```text
src/backend/GameServer.Domain/Bosses/BossDefinition.cs
src/backend/GameServer.Application/Battle/IBossDefinitionLookup.cs
src/backend/GameServer.Application/Battle/BattleStartService.cs
src/backend/GameServer.Infrastructure/Postgres/Configurations/BossDefinitionConfiguration.cs
src/backend/GameServer.Infrastructure/Postgres/Repositories/BossDefinitionLookup.cs
src/backend/GameServer.Infrastructure/Postgres/GameDbContext.cs
src/backend/GameServer.Infrastructure/Postgres/Migrations/20261011042819_AddBossBalanceConfiguration.cs
src/backend/GameServer.Infrastructure/Postgres/Migrations/20261011042819_AddBossBalanceConfiguration.Designer.cs
src/backend/GameServer.Infrastructure/Postgres/Migrations/GameDbContextModelSnapshot.cs
tests/backend/GameServer.Api.Tests/ApplicationSessionRESTTests.cs
tests/backend/GameServer.Api.Tests/BattleStartEndpointTests.cs
tests/backend/GameServer.Api.Tests/BattleStartSmokeTest.cs
tests/backend/GameServer.Api.Tests/RedisBattleStateSmokeTest.cs
tests/backend/GameServer.Api.Tests/TestProvisionedContent.cs
tests/backend/GameServer.Application.Tests/BattleResultTestDoubles.cs
tests/backend/GameServer.Application.Tests/BattleStartServiceTests.cs
tests/backend/GameServer.Infrastructure.Tests/BossDefinitionBalanceLookupTests.cs
tests/backend/GameServer.Infrastructure.Tests/BossDefinitionPostgresProvisioningTests.cs
tests/backend/GameServer.Infrastructure.Tests/BossPersistenceTests.cs
```

*(Note: The task manifest file itself is excluded from the implementation declared file set. The list above strictly matches the `Declared Files:` field in `## Metadata`.)*

---

## Acceptance Criteria

### 1. Schema, Mapping, and Constraints
- [x] `BossDefinition` gains exactly four columns: `MaxHP` (integer, NOT NULL), `ATK` (integer, NOT NULL), `DEF` (integer, NOT NULL), `EnrageThreshold` (double precision, NOT NULL).
- [x] The EF Core mapping declares no default value (`HasDefaultValue`) for any of the four (ADR-024 §2.5).
- [x] The four check constraints exist with the ADR's names: `CK_BossDefinition_MaxHP_Positive`, `CK_BossDefinition_ATK_NonNegative`, `CK_BossDefinition_DEF_NonNegative`, `CK_BossDefinition_EnrageThreshold_Range`.
- [x] No structural column, index, key, or foreign key is altered.

### 2. Migration and Rollback
- [x] Migration `20261011042819_AddBossBalanceConfiguration` adds the four columns with the transitional defaults `1 / 0 / 0 / 0.5`.
- [x] All five canonical rows are updated to `BOSS_RULES.md` §6.1 values in the same migration, before the constraints are activated.
- [x] The four transitional defaults are dropped via `AlterColumn` (`DROP DEFAULT`) after activating constraints, ensuring unpopulated inserts fail closed with `NOT NULL` violations.
- [x] `Down()` drops the four constraints then the four columns, deletes no row, and touches no foreign key.
- [x] The migration inserts no row (`InsertData`/`HasData` absent) and `dotnet ef migrations has-pending-model-changes` reports none.

### 3. Domain Overlay Lookup
- [x] `IBossDefinitionLookup.GetDefinitionByIdentityAsync` returns the structural Domain baseline with the row's four balance values overlaid.
- [x] `Element`, `BossDefinitionId`, `BossId`, `PassiveDefinition`, `SkillDefinition`, and `SkillDefinition.SecondaryEffect` survive the overlay unchanged.
- [x] An identity outside the content-defined MVP set returns `null`.
- [x] A content-defined identity whose row is absent throws `InvalidOperationException`, and the static baseline's balance values are never returned.
- [x] A row violating `MaxHP > 0`, `ATK >= 0`, `DEF >= 0`, or `0.0 < EnrageThreshold < 1.0` throws `InvalidOperationException`.
- [x] The battle-end `FindBossDefinitionIdByIdentityAsync` contract is unchanged.

### 4. Battle-Start Wiring and Error Semantics
- [x] `BattleStartService` resolves the Boss through the lookup and creates the battle from the overlaid balance.
- [x] The definition is resolved once per battle creation; no turn-resolution path depends on the lookup.
- [x] A PostgreSQL connectivity failure (`DbException` family) is neither caught nor translated.
- [x] An invalid client `bossId` still produces `BOSS_NOT_FOUND` / HTTP 400; an absent, out-of-range, or unreadable row does **not** produce HTTP 400.

### 5. Quality Gates
- [x] All relevant tests pass at the required validation depth (`core/validation.md` §2, HIGH risk).
- [x] The full backend solution suite passes and the frontend suite is unaffected.
- [x] `git diff --check` passes with zero whitespace or conflict markers.
- [x] Unrelated working-tree changes are preserved and uncommitted.

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain / Application / Infrastructure)
[x] tests/ (unit / PostgreSQL integration / API)
[ ] src/frontend/client/ (unchanged)
[ ] docs/ (owned by TASK-250; not modified by this task)
```

---

## Implementation Notes

- **Migration identifier:** `20261011042819_AddBossBalanceConfiguration`.
- **Enrage representation:** the column stores the documented fraction (`0.30`, `0.50`, `0.75`) because `BOSS_RULES.md` §5 item 4's comparison is `BossHP < MaxHP × EnrageThreshold`; `BOSS_RULES.md` §6.1 states the absolute figures (`1500`, `2100`), and the migration's values are the baseline's own.
- **Fail-closed direction:** the identity set is decided from `BossDefinitions.All` *before* persistence is read, so an invalid `bossId` never queries the database and stays a client 400 even when PostgreSQL is unreachable — while a canonical identity with an unusable row fails closed server-side.
- **`EnrageThreshold` validation is written as the documented invariant in its positive form** (`!(x > 0.0 && x < 1.0)`), which also rejects `NaN`; PostgreSQL's own ordering does the same for the check constraint.
- **Test-host seeding:** the isolated in-memory API stores apply no migration, so `TestProvisionedContent` and the battle-start/smoke hosts now also supply the five canonical `BossDefinition` rows — the same provisioned content a migrated database holds.
- **Two superseded tests updated:** `BossPersistenceTests.Model_ShouldStoreExactlyTheFiveDocumentedColumns` (now `…NineDocumentedColumns`) and `Model_ShouldNotPersistTheCombatDefinitionValues` (now the mapping/constraint assertions). The old assertions encoded `DATABASE.md` §1 note item 1 as it stood before TASK-250 synchronized it to the approved ADR-024 contract (`AGENTS.md` §15 — the test was outdated, not the implementation).

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — GameServer.Infrastructure.Tests (BossDefinitionBalanceLookupTests: overlay, structural
                         preservation, SecondaryEffect survival, missing row, invalid ranges, unknown identity)
[x] Integration tests  — GameServer.Infrastructure.Tests (PostgreSQL: applied columns/values/constraints, constraint
                         rejection, live Npgsql overlay, stored-value authority); migration-source contract assertions
[x] API tests          — GameServer.Api.Tests (battle start from the stored balance; missing/out-of-range/unreachable
                         row is not HTTP 400; unknown bossId remains BOSS_NOT_FOUND / HTTP 400)
[x] Solution suite     — dotnet test src/backend/GameServer.sln --nologo
[x] Client suite       — npm test -- --run (from src/frontend/client)
```

### Key Edge Cases
- `BOSS_RULES.md` §5 item 4 — the Enrage comparison is strict, so exactly `MaxHP × EnrageThreshold` is not Enraged, and Hỏa Long's absolute threshold is `1500`.
- `ADR-024` §2.4 — both `EnrageThreshold` boundaries (`0.0`, `1.0`) are exclusive, `MaxHP = 0` violates the invariant while `MaxHP = 1` satisfies it, and `NaN` is invalid.
- `ADR-024` §2.1 item 3 — a canonical Boss with no provisioned row must never be answered from the static baseline.
- `DATABASE.md` §1 note item 2 — the persistence key and the Identity are never substituted for each other.

---

## Stop Conditions

- If ADR-024 is not approved by the operator: STOP per `AGENTS.md` §18.
- If the implementation requires a balance value, gameplay rule, or schema change outside ADR-024: STOP per `AGENTS.md` §7 / §20.
- If the change expands to Redis snapshot keys, restart/multi-instance recovery, or frontend behavior: STOP per task boundaries.
- If commit or push authorization is withheld: STOP before creating Git commits per operator instructions.

---

## Completion Evidence

### Commit
- Implementation slice (Phase A): `5a1c5be75e15fca3436cc42e520a7b27f5f76efd`
- Subject: `feat(bosses): source runtime boss balance from PostgreSQL via domain overlay`
- Owns: `TASK-251`
- Files: see `## Declared File Set (P-2 / P-5)`
- Shared with: `none`
- Unowned / pre-existing: `none`
- Completion record filing (Phase B): this completion record alone, filed under its own bookkeeping commit (`tasks/completed/TASK-251-implement-database-driven-boss-balance-configuration.md`).

### Changed Files
- `src/backend/GameServer.Domain/Bosses/BossDefinition.cs` — Documentation contract updated for the four runtime balance columns and the Domain Overlay; no behavior change.
- `src/backend/GameServer.Application/Battle/IBossDefinitionLookup.cs` — Added `GetDefinitionByIdentityAsync` with its documented failure split.
- `src/backend/GameServer.Application/Battle/BattleStartService.cs` — Boss resolution now delegates to the lookup; the static in-memory resolver removed.
- `src/backend/GameServer.Infrastructure/Postgres/Configurations/BossDefinitionConfiguration.cs` — Mapped the four columns (required, no default) and added the four check constraints.
- `src/backend/GameServer.Infrastructure/Postgres/Repositories/BossDefinitionLookup.cs` — Implemented the Domain Overlay with fail-closed range validation.
- `src/backend/GameServer.Infrastructure/Postgres/GameDbContext.cs` — Documentation contract updated; no behavior change.
- `src/backend/GameServer.Infrastructure/Postgres/Migrations/20261011042819_AddBossBalanceConfiguration.cs` — New migration: columns with transitional defaults, canonical row population, constraints, drop transitional defaults for fail-closed inserts, safe rollback.
- `…/20261011042819_AddBossBalanceConfiguration.Designer.cs`, `…/GameDbContextModelSnapshot.cs` — Generated model state for the new migration.
- `tests/backend/GameServer.Infrastructure.Tests/BossDefinitionBalanceLookupTests.cs` — New: the overlay, structural preservation, and every fail-closed path.
- `tests/backend/GameServer.Infrastructure.Tests/BossDefinitionPostgresProvisioningTests.cs` — Applied columns/values, default drop verification, direct insert NOT NULL rejection, constraint rejection, live overlay, stored-value authority, rollback-safe probe rows.
- `tests/backend/GameServer.Infrastructure.Tests/BossPersistenceTests.cs` — Nine-column model contract, no model defaults, constraint expressions, default drop operation verification, and the migration-source contract.
- `tests/backend/GameServer.Application.Tests/BattleResultTestDoubles.cs` — `ScriptedBossDefinitionLookup` implements both lookups.
- `tests/backend/GameServer.Application.Tests/BattleStartServiceTests.cs` — Overlaid-balance creation, one lookup per battle, no lookup on the turn path, and propagated failures.
- `tests/backend/GameServer.Api.Tests/BattleStartEndpointTests.cs` — Stored-balance creation and the 400-versus-server-failure semantics.
- `tests/backend/GameServer.Api.Tests/{TestProvisionedContent,BattleStartSmokeTest,RedisBattleStateSmokeTest,ApplicationSessionRESTTests}.cs` — Canonical `BossDefinition` rows supplied to isolated in-memory hosts.

### Validation Results
- `dotnet test src/backend/GameServer.sln --nologo` — PASS (3,039 tests: Domain 1,558; Infrastructure 476; Api 342; Application 663)
- `dotnet test tests/backend/GameServer.Infrastructure.Tests/GameServer.Infrastructure.Tests.csproj --nologo` — PASS (476/476, PostgreSQL integration included and executed against a live server)
- `npm test -- --run` (src/frontend/client) — PASS (23 files, 991/991)
- `git diff --check` — PASS (exit 0)
- `dotnet ef migrations has-pending-model-changes` — "No changes have been made to the model since the last migration."
- `dotnet ef database update` → `dotnet ef database update 20261005120735_AddAccountsTableAndDropDiscordUserId` → re-apply — PASS; applied schema verified by direct catalog query (9 columns, canonical values, 4 constraints); rollback verified to restore 5 columns and 0 constraints while preserving 5 `BossDefinition` rows, 93 `BattleResult` rows, and all 93 foreign-key joins.
- Phase A commit verification — `git show --name-status 5a1c5be75e15fca3436cc42e520a7b27f5f76efd` matches the 19 declared implementation files exactly.
- Staging verification — `git diff --cached --name-status` matches strictly `tasks/completed/TASK-251-implement-database-driven-boss-balance-configuration.md` (P-5 compliant).

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed P-1…P-6 commit policy compliance
