# TASK-249 — Design Database-Driven Boss Balance Configuration

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — does NOT duplicate game rules or schemas.

  Lifecycle: BACKLOG → READY validation completed by orchestrator.
  Lifecycle: READY → SUPERSEDED completed 2026-10-11 by the orchestrator/review role
  following tasks/TASK_LIFECYCLE.md §1/§2/§3: 100% of scope and deliverables were
  decomposed into and fully satisfied by downstream tasks TASK-250 (ADR-024) and
  TASK-251 (implementation). Moved to tasks/completed/.
-->

---

## Metadata

```text
Task ID:           TASK-249
Type:              ARCHITECTURE
Status:            SUPERSEDED (satisfied downstream by TASK-250 and TASK-251)
Risk:              HIGH
Priority:          HIGH
Primary Agent:     backend
Supporting Agents: persistence, review, testing
Workflow:          architecture/architecture-change.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis, backend/persistence-analysis, quality/architecture-conformance, quality/scope-validation
Dependencies:      TASK-248
Declared Files:    None
```

---

## Objective

Design the architectural schema, lifecycle contracts, and data migration strategy for storing configurable Boss balance values (`MaxHP`, `ATK`, `DEF`, `EnrageThreshold`, and related skill/passive parameters) in PostgreSQL, eliminating source-code recompilation for future balance adjustments. Ensure battle-start snapshots guarantee determinism and isolation for in-progress battles, preserve canonical MVP base values (including Hỏa Long's restored `MaxHP = 5000` and `EnrageThreshold = 0.30` per TASK-248), maintain domain unit test independence, and establish fail-closed validation.

---

## Problem Statement & Impact

Currently, boss combat balance values (`MaxHP`, `ATK`, `DEF`, `EnrageThreshold`) are hardcoded in C# within `src/backend/GameServer.Domain/Bosses/BossDefinitions.cs` and explicitly ignored by EF Core in `src/backend/GameServer.Infrastructure/Postgres/Configurations/BossDefinitionConfiguration.cs`. Furthermore, `docs/02-technical/DATABASE.md` §1 note item 1 specifically dictates that combat stats are non-persisted Domain configuration.

This hardcoded design presents several major operational and architectural risks:
1. **Source-Coupled Balance Updates:** Every balance tweak (such as the recent restoration of Hỏa Long in TASK-248) requires editing Domain C# source code, rebuilding the server container, and deploying a new software release.
2. **Persistence Boundary Asymmetry:** While `PetDefinition`, `CardDefinition`, and `RelicDefinition` store their operational definitions and balance values (e.g. `PowerCost`, `LoadoutCopyLimit`, `EffectDefinition` JSON) in PostgreSQL, `BossDefinition` stores only `Identity`, `Element`, `PassiveDefinition`, and `SkillDefinition`, omitting core base combat stats.
3. **Fragility During Content Tuning:** Live combat pacing adjustments cannot be managed via database migrations, configuration scripts, or operational tooling without code changes.

A clean, database-driven balance configuration architecture is required to decouple game balance from source code while preserving server authority, determinism, and domain testability.

---

## Repository Evidence & Current State

1. **Hardcoded Domain Stats:** `src/backend/GameServer.Domain/Bosses/BossDefinitions.cs` lines 142–148, 189–194, 240–245, 279–288, 319–327 hardcode base stats for all five MVP bosses (`HoaLong`, `ThuyMa`, `MocYeu`, `SonThachVe`, `KimLoiVuong`).
2. **Explicit EF Core Exclusion:** `src/backend/GameServer.Infrastructure/Postgres/Configurations/BossDefinitionConfiguration.cs` lines 114–117 explicitly invoke:
   - `builder.Ignore(definition => definition.MaxHP);`
   - `builder.Ignore(definition => definition.ATK);`
   - `builder.Ignore(definition => definition.DEF);`
   - `builder.Ignore(definition => definition.EnrageThreshold);`
3. **Battle Start Resolution:** `src/backend/GameServer.Application/Battle/BattleStartService.cs` lines 508–524 (`ResolveBoss`) resolves the boss by iterating `BossDefinitions.All` directly in memory without consulting PostgreSQL.
4. **Active Battle State Snapshot:** `src/backend/GameServer.Application/Battle/BattleStateService.cs` lines 508–557 instantiates `BossConfiguration(bossDefinition)`, initializes `BossState` via `bossDefinition.ToInitialState()`, saves `BattleState` in Redis, and holds `_bossConfiguration[battleId]` in an in-memory dictionary.
5. **Database-End-Only Boss Lookup:** `src/backend/GameServer.Infrastructure/Postgres/Repositories/BossDefinitionLookup.cs` only queries PostgreSQL on the battle-end path to resolve `Identity -> BossDefinitionId` for foreign-key carriage on `BattleResult`.
6. **Authoritative Technical Documentation:**
   - `docs/02-technical/DATABASE.md` §1 note item 1 explicitly documents combat stats as Domain-only non-persisted configuration.
   - `docs/02-technical/GAME_STATE.md` §2.4 documents `BossState` in active battle state.
   - `docs/01-game-design/BOSS_RULES.md` §6.1 documents canonical MVP boss base stats approved by the Product Owner.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — MVP scope: Match-3 battle engine, five MVP bosses, PostgreSQL persistence.
- `docs/01-game-design/BOSS_RULES.md` §5 item 4 — Permanent Enrage state transition rule (`BossHP < MaxHP * EnrageThreshold`).
- `docs/01-game-design/BOSS_RULES.md` §6.1 — MVP Boss base stats canonical reference (Hỏa Long: HP 5000, ATK 100, DEF 50, Enrage 1500 / 30%; Thủy Ma, Mộc Yêu, Sơn Thạch Vệ, Kim Lôi Vương).
- `docs/01-game-design/BOSS_RULES.md` §6.4 — Boss technical Identity contract (`BossId`, `PassiveId`, `SkillId`).
- `docs/02-technical/DATABASE.md` §1, §1 note items 1–5, §3, §5 item 4 — PostgreSQL schema, `BossDefinition` persistence contract, and migration-level seeding rules.
- `docs/02-technical/GAME_STATE.md` §2.4, §2.4.1–§2.4.3 — `BossState` authoritative structure and active battle state fields.
- `docs/02-technical/ARCHITECTURE.md` §2.1, §3, §5 — Layering boundaries, persistence decoupling, and anti-overengineering constraints.
- `docs/03-decisions/ADR/ADR-005-redis-active-battle-state.md` — Redis as exclusive store for active, transient battle state.
- `docs/03-decisions/ADR/ADR-006-postgresql-persistence.md` — PostgreSQL for durable entities and configuration outside the hot resolution path.
- `tasks/active/TASK-248-restore-hoa-long-canonical-base-stats.md` — Baseline canonical values restored.

---

## Scope

### In Scope
- Architectural design for storing boss balance values in PostgreSQL (`BossDefinition` table).
- Specification of static identity vs. adjustable balance fields across Domain, Application, and Persistence layers.
- Battle-start resolution pipeline design replacing static in-memory lookup with database-backed lookup while preserving battle isolation and snapshot consistency.
- Evaluation and specification of failure behaviors (missing, corrupted, unavailable, duplicated configuration) enforcing fail-closed rules.
- Design of schema migration, data seeding, and rollback mechanisms conforming to `DATABASE.md` §1 note 5 conventions.
- Strategy for maintaining fast, offline unit testing without requiring live PostgreSQL database instances.
- Preparation of downstream task decomposition (ADR authoring, documentation synchronization, EF Core migration & repository implementation).

### Out of Scope
- Code modifications in `src/backend/` or `src/frontend/` (this is a design task; implementation is deferred to downstream tasks).
- Changing canonical boss balance values, skill damage, or passive rules (canonical values in `BOSS_RULES.md` §6.1 must be preserved verbatim).
- Creating or applying database migrations in this task.
- Modifying SignalR wire contracts, event emission protocols, or client presentation.
- Designing dynamic runtime GM/admin dashboards or live in-memory configuration reloaders (out of MVP scope).

---

## Architecture Assessment & Options Considered

### 1. Boss Field Categorization
- **Static Identity / Domain Content (Unchanging across balance passes):**
  - `BossDefinitionId` (Persistence PK, e.g. `"boss-def-hoa-long"`)
  - `Identity` / `BossId` (Technical canonical ID, e.g. `"boss-hoa-long"`)
  - `DisplayName` (Content/presentation only, e.g. `"Hỏa Long"`)
  - `Element` (Core elemental affinity: `Hoa`, `Thuy`, `Moc`, `Tho`, `Kim`)
  - `PassiveId` & `SkillId` (Mechanical identifiers)
  - `SecondaryEffect.Kind`, `StatusEffectType`, `TargetStat` (Structural effect typings)
- **Adjustable Balance Values (Subject to tuning):**
  - Base stats: `MaxHP`, `ATK`, `DEF`, `EnrageThreshold` (combat attributes)
  - Skill parameters: `SkillBaseDamage`, `SkillChargeRequirement`, `SkillCooldownTurns`
  - Skill secondary effect parameters: `Magnitude`, `DurationTurns`
  - Passive parameters: `PassiveThreshold` (match count), passive effect magnitude and duration

### 2. Battle Start Lifecycle
Currently: `POST /api/battle/start` -> `BattleStartService.StartBattleAsync` -> `ResolveBoss` (scans `BossDefinitions.All`) -> `BattleStateService.CreateBattleAsync` -> `BossDefinition.ToInitialState()` -> `BossState.Initial()`.
Proposed: `POST /api/battle/start` -> `BattleStartService` queries `IBossDefinitionLookup.GetDefinitionByIdentityAsync(bossId)` -> receives populated `BossDefinition` from PostgreSQL -> passes to `BattleStateService.CreateBattleAsync` -> creates snapshot in Redis `BattleState.BossState`.

### 3. State & Snapshot Durability
- Active battle state in Redis already holds `BossState` (`HP`, `MaxHP`, `ATK`, `DEF`, `State`, etc.).
- However, turn resolution (`ExecuteSwapAsync`) currently reads `EnrageThreshold`, `SkillChargeRequirement`, `SkillBaseDamage`, etc., from `_bossConfiguration[battleId]`.
- To preserve battle consistency across server restarts, the battle-start snapshot must ensure that the exact `BossDefinition` resolved at battle start remains bound to the battle for its entire duration.

### 4. Options Evaluated

#### Option A: Source-Only Configuration (Status Quo)
- **Description:** Hardcode all stats in `BossDefinitions.cs`; ignore in PostgreSQL.
- **Pros:** Zero DB overhead at battle start; compile-time type safety; offline tests trivial.
- **Cons:** Any balance adjustment requires code edits, test updates, Docker build, and deployment. Violates TASK-249 objective.
- **Verdict:** Rejected.

#### Option B: Database-Only Configuration (Pure Dynamic)
- **Description:** Remove all static definitions from Domain; require PostgreSQL query for all domain instantiation; fail without DB.
- **Pros:** Complete centralization of balance in database.
- **Cons:** Breaks Domain purity; severely degrades unit tests by introducing DB dependencies or requiring excessive mock scaffolding; risks startup failure if DB is unseeded.
- **Verdict:** Rejected.

#### Option C: Hybrid Configuration — Domain Canonical Defaults with Database Authority at Runtime (Recommended)
- **Description:**
  1. The Domain retains `BossDefinition` with canonical baseline defaults (`BossDefinitions.HoaLong`, etc.) to serve as domain invariants, test fixtures, and fallback baselines.
  2. PostgreSQL `BossDefinition` table is extended to persist `MaxHP`, `ATK`, `DEF`, and `EnrageThreshold` as explicit integer/double columns.
  3. The Application layer extends `IBossDefinitionLookup` with `GetDefinitionByIdentityAsync(string identity)` (mirroring `IPetRepository.GetDefinitionAsync`, `ICardDefinitionLookup.GetDefinitionAsync`, and `IRelicDefinitionLookup.GetDefinitionAsync`).
  4. `BattleStartService` resolves `BossDefinition` from PostgreSQL at battle creation.
  5. The resolved configuration is snapshotted into the battle instance, guaranteeing that live in-progress battles are immune to mid-game database modifications.
  6. Unit tests remain decoupled from the database by utilizing the static Domain definitions or in-memory test doubles.
- **Verdict:** Recommended. Smallest architectural footprint consistent with existing patterns and anti-overengineering principles.

---

## Proposed Database Schema & Configuration Lifecycle (Proposal)

### Proposed Table Alteration (`BossDefinition`)
Extend table `BossDefinition` with columns for combat balance:
```text
Table: BossDefinition
├── BossDefinitionId (VARCHAR(64), PK)       [Existing]
├── Identity         (VARCHAR(64), UNIQUE)   [Existing]
├── Element          (INTEGER, NOT NULL)     [Existing]
├── MaxHP            (INTEGER, NOT NULL)     [PROPOSED NEW COLUMN]
├── ATK              (INTEGER, NOT NULL)     [PROPOSED NEW COLUMN]
├── DEF              (INTEGER, NOT NULL)     [PROPOSED NEW COLUMN]
├── EnrageThreshold  (DOUBLE PRECISION)      [PROPOSED NEW COLUMN]
├── PassiveDefinition(JSONB, NOT NULL)       [Existing - contains threshold, resetBehavior]
└── SkillDefinition  (JSONB, NOT NULL)       [Existing - contains baseDamage, chargeRequirement, cooldownTurns, secondaryEffect]
```

### Proposed EF Core Entity Mapping
In `BossDefinitionConfiguration.cs`:
- Remove `builder.Ignore(definition => definition.MaxHP);`
- Remove `builder.Ignore(definition => definition.ATK);`
- Remove `builder.Ignore(definition => definition.DEF);`
- Remove `builder.Ignore(definition => definition.EnrageThreshold);`
- Map properties as required columns with sensible constraints:
  - `MaxHP`: `IsRequired()`, `HasDefaultValue(5000)`
  - `ATK`: `IsRequired()`, `HasDefaultValue(100)`
  - `DEF`: `IsRequired()`, `HasDefaultValue(50)`
  - `EnrageThreshold`: `IsRequired()`, `HasDefaultValue(0.30)`

---

## Battle-Start Snapshot & Consistency Requirements

1. **Resolution at Battle Creation:**
   - In `POST /api/battle/start`, `BattleStartService` queries `IBossDefinitionLookup.GetDefinitionByIdentityAsync(request.BossId)`.
   - If not found or invalid, request fails closed with `400 Bad Request` (`INVALID_BOSS` / `BossNotFound`).
2. **Snapshot Carriage:**
   - The resolved `BossDefinition` is supplied to `BattleStateService.CreateBattleAsync`.
   - `BossState.Initial` initializes `HP = MaxHP`, `MaxHP = MaxHP`, `ATK = ATK`, `DEF = DEF`.
   - Active battle state in Redis holds this authoritative state.
3. **In-Progress Isolation:**
   - A subsequent database update or balance migration does NOT affect battles already created.
   - For turn resolutions (`ExecuteSwapAsync`, `CastCardAsync`), `BattleStateService` uses the battle-scoped configuration attached at creation.

---

## Migration, Fallback, Validation, Versioning & Rollback Strategy

1. **Migration Convention:**
   - Follow `DATABASE.md` §1 note 5: Schema and data changes must be managed via EF Core code-first migrations (`AddColumn` in `Up()`, `DropColumn` in `Down()`).
   - Populate existing rows in `Up()` with canonical baseline values matching `BOSS_RULES.md` §6.1:
     - Hỏa Long: MaxHP = 5000, ATK = 100, DEF = 50, EnrageThreshold = 0.30
     - Thủy Ma: MaxHP = 5000, ATK = 100, DEF = 50, EnrageThreshold = 0.30
     - Mộc Yêu: MaxHP = 5000, ATK = 100, DEF = 50, EnrageThreshold = 0.30
     - Sơn Thạch Vệ: MaxHP = 3000, ATK = 120, DEF = 0, EnrageThreshold = 0.50
     - Kim Lôi Vương: MaxHP = 2800, ATK = 140, DEF = 0, EnrageThreshold = 0.75
2. **Fail-Closed Validation:**
   - When loading `BossDefinition` from PostgreSQL:
     - Enforce `MaxHP > 0`, `ATK >= 0`, `DEF >= 0`, and `0.0 < EnrageThreshold < 1.0`.
     - Corrupted or out-of-range rows throw a domain validation exception rather than applying corrupted combat math.
3. **Rollback Strategy:**
   - Migrations provide automated rollback via EF Core migration downgrade (`Down()` drops added columns).
   - Balance adjustments are applied as explicit data migrations (`migrationBuilder.UpdateData`), which can be reversed cleanly with paired `UpdateData` in `Down()`.
4. **No Dynamic GM Tooling in MVP:**
   - To adhere to `AGENTS.md` §9 (Anti-Overengineering) and `MVP_SCOPE.md` §1, balance updates are applied via version-controlled database migrations, not runtime HTTP admin endpoints or unmanaged SQL scripts.

---

## Test Strategy & Dependency Management

1. **Domain Unit Tests (`GameServer.Domain.Tests`):**
   - Continue using `BossDefinitions.HoaLong` and `BossDefinitions.All` directly.
   - Zero dependency on database or EF Core; unit tests remain fast, offline, and deterministic.
2. **Application Unit Tests (`GameServer.Application.Tests`):**
   - Test `BattleStartService` using an in-memory double or mock of `IBossDefinitionLookup` (e.g. `ScriptedBossDefinitionLookup`).
3. **Integration Tests (`GameServer.Api.Tests`, `GameServer.Infrastructure.Tests`):**
   - `BattleStartSmokeTest` and `BattleResultSmokeTest` verify end-to-end database loading of `BossDefinition` through `PostgresFixture`.
4. **Assertion Preservation:**
   - All existing assertions asserting canonical values (e.g. `MaxHP = 5000`, `EnrageThreshold = 0.30`, absolute enrage 1500) will be maintained without weakening assertions.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-249:
```text
None
```

*(Note: The task manifest file itself is excluded from the implementation declared file set. The list above strictly matches the `Declared Files:` field in `## Metadata`.)*

---

## Acceptance Criteria

*(Note: All 10 acceptance criteria were evaluated and 100% satisfied downstream by TASK-250 and TASK-251 per tasks/TASK_LIFECYCLE.md §3; see Supersession Traceability Matrix below.)*

- [x] Architectural design record completely documented and ready for operator review (satisfied by TASK-250 ADR-024).
- [x] Static vs. adjustable boss attributes clearly categorized across Domain, Application, and Persistence layers (satisfied by TASK-250 ADR-024 §2.1 and TASK-251).
- [x] Hybrid configuration model recommended with clear justification against source-only and database-only models (satisfied by TASK-250 ADR-024 Option 1 — Domain Overlay).
- [x] Schema extension proposal for `BossDefinition` in PostgreSQL fully specified with types and default constraints (satisfied by TASK-250 DATABASE.md update and TASK-251 migration).
- [x] Battle-start resolution pipeline designed with fail-closed behavior for missing or invalid configuration (satisfied by TASK-250 ADR-024 §2.4 and TASK-251 BattleStartService).
- [x] Battle isolation verified: in-progress battles remain unaffected by subsequent database updates (satisfied by TASK-250 ADR-024 §2.3 and TASK-251).
- [x] Migration strategy with Up/Down rollback capability documented per `DATABASE.md` §1 note 5 conventions (satisfied by TASK-250 ADR-024 §2.5 and TASK-251 migration).
- [x] Domain test decoupling strategy verified, ensuring over 200 existing test references to `BossDefinitions` remain functional without PostgreSQL dependencies (satisfied by TASK-250 ADR-024 §2.1 and TASK-251 test suites).
- [x] Preserves canonical MVP base values (Hỏa Long `MaxHP = 5000`, `EnrageThreshold = 0.30` / 1500 HP) (satisfied by TASK-250 ADR-024 §2.5 and TASK-251 migration).
- [x] Downstream implementation tasks decomposed with clear scope boundaries (satisfied by authoring and completing TASK-250 and TASK-251).

---

## Affected Files & Areas

```text
[x] tasks/backlog/ (TASK-249 task manifest)
[ ] docs/02-technical/ (DATABASE.md, ARCHITECTURE.md updates in downstream task)
[ ] docs/03-decisions/ADR/ (ADR authoring in downstream task)
[ ] src/backend/ (EF Core entities, lookup repository, battle start in downstream task)
[ ] tests/ (Downstream integration verification)
```

---

## Implementation Notes

- Current `BossDefinitionConfiguration` explicitly ignores combat stats (`MaxHP`, `ATK`, `DEF`, `EnrageThreshold`).
- `DATABASE.md` §1 note 1 will need synchronization in a downstream DOCUMENTATION task once an ADR is approved.
- `IBossDefinitionLookup` is currently located at `src/backend/GameServer.Application/Battle/IBossDefinitionLookup.cs` and implemented in `src/backend/GameServer.Infrastructure/Postgres/Repositories/BossDefinitionLookup.cs`.
- Downstream task decomposition recommendation:
  1. `TASK-250` (ARCHITECTURE / ADR): Author ADR for database-driven boss balance configuration and update `DATABASE.md`.
  2. `TASK-251` (FEATURE): Add EF Core migration for `BossDefinition` balance columns, update `BossDefinitionConfiguration`, implement `IBossDefinitionLookup.GetDefinitionByIdentityAsync`, and wire into `BattleStartService`.

---

## Testing Requirements

### Required Verification
```text
[x] Verification of existing repository architecture and file citations
[x] Validation of test decoupling strategy against 2,970 backend tests
[x] Verification of git diff cleanliness via git diff --check
```

### Key Edge Cases
- Boss row missing in database -> Battle start must reject with `INVALID_BOSS` / `BossNotFound` (fail-closed).
- Balance update applied mid-battle -> In-progress battle in Redis continues with its initial creation snapshot.
- Server restart during active battle -> Active battle state reconstructed with immutable initial battle stats.

---

## Stop Conditions

- If operator requests immediate code implementation under this design task: STOP per task instructions (design only, no implementation).
- If proposed architecture violates server authority or deterministic battle rules: STOP per `AGENTS.md` §10.
- If proposed design contradicts `docs/00-overview/MVP_SCOPE.md` or introduces out-of-scope systems: STOP per `AGENTS.md` §8.

---

## Completion Evidence

<!--
  LIFECYCLE RECONCILIATION: This task was decomposed into downstream tasks TASK-250 and
  TASK-251 per its own implementation notes (lines 277-280). Per tasks/TASK_LIFECYCLE.md
  §1/§2/§3, it is reconciled as SUPERSEDED because 100% of its intended deliverables and
  scope were satisfied downstream without requiring direct execution of this task file.
-->

### Supersession Determination & Traceability Matrix

```text
Status: SUPERSEDED (tasks/TASK_LIFECYCLE.md §1/§3)
Reconciliation: READY → SUPERSEDED

TASK-249 DELIVERABLE TRACEABILITY:
  D-1  Architectural design & ADR authoring for PostgreSQL boss balance
       → SATISFIED BY TASK-250 (ADR-024 Accepted; commits aaba7f7 and 87158a4).
  D-2  Specification of static identity vs. adjustable balance fields (Domain Overlay)
       → SATISFIED BY TASK-250 (ADR-024 §2.1–§2.2) and TASK-251 (IBossDefinitionLookup,
         BossDefinitionLookup).
  D-3  Schema extension proposal & EF Core mapping for BossDefinition
       → SATISFIED BY TASK-250 (docs/02-technical/DATABASE.md) and TASK-251
         (BossDefinitionConfiguration, migration 20261011042819_AddBossBalanceConfiguration).
  D-4  Battle-start resolution pipeline with snapshot isolation & fail-closed validation
       → SATISFIED BY TASK-250 (ADR-024 §2.3–§2.4) and TASK-251 (BattleStartService,
         BossDefinitionLookup range checks and DbException propagation).
  D-5  Migration Up/Down rollback & canonical baseline transcription
       → SATISFIED BY TASK-250 (ADR-024 §2.5) and TASK-251 (migration Up/Down verified,
         transcribing BOSS_RULES.md §6.1 canonical base stats including Hỏa Long 5000/100/50/0.30).
  D-6  Domain test decoupling strategy preserving offline suite
       → SATISFIED BY TASK-250 (ADR-024 §2.1 item 1) and TASK-251 (3,039 backend tests passing
         with zero database coupling in Domain tests).
  D-7  Downstream task decomposition
       → SATISFIED BY executing and completing TASK-250 and TASK-251 to 100% DONE.

Independent actionable scope remaining: NONE (100% satisfied downstream).
```

### Commit
- Supersession record filing: this record alone, filed under its own dedicated bookkeeping commit (`tasks/completed/TASK-249-design-database-driven-boss-balance-configuration.md`).
- Subject: `docs: file the database-driven boss balance configuration design supersession record`
- Owns: `TASK-249`
- Files: `tasks/completed/TASK-249-design-database-driven-boss-balance-configuration.md`
- Shared with: `none`
- Unowned / pre-existing: `none`

### Changed Files
- `tasks/completed/TASK-249-design-database-driven-boss-balance-configuration.md` — moved from `tasks/backlog/` to `tasks/completed/` with Status: SUPERSEDED. Zero production code files.

### Validation Results
- `tasks/TASK_LIFECYCLE.md` §1/§2/§3 compliance verified — transition `READY → SUPERSEDED` is authorized and valid.
- Downstream delivery verified — TASK-250 (Phase A: `aaba7f7`, Phase B: `87158a4`) and TASK-251 (Phase A: `5a1c5be`, Phase B: `431c8ac`) both in `tasks/completed/` with `Status: DONE`.
- `git diff --check` — PASS.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed P-1…P-6 commit policy compliance
