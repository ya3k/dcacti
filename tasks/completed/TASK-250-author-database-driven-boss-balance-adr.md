# TASK-250 — Author and Review Architecture Decision Record for Database-Driven Boss Balance Configuration

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — does NOT duplicate game rules or schemas.
-->

<!--
  Lifecycle: BACKLOG → READY validation completed by the orchestrator role
  after validating tasks/TASK_LIFECYCLE.md §3 READY criteria:
    [x] Task type confirmed (ARCHITECTURE — tasks/TASK_TYPES.md §2 / architecture/architecture-change.md)
    [x] Relevant documentation exists in docs/ (MVP_SCOPE.md §1, BOSS_RULES.md §6.1/§6.4,
        DATABASE.md §1/§3/§5, GAME_STATE.md §2.4, ARCHITECTURE.md §2.1/§3/§5,
        docs/03-decisions/README.md, ADR-005, ADR-006)
    [x] MVP scope confirmed (MVP_SCOPE.md §1 — Match-3 battle engine, five MVP bosses,
        PostgreSQL persistence; operational balance via migrations is within scope;
        dynamic GM/admin UI is explicitly excluded)
    [x] Not blocked: Dependencies: TASK-249 (architecture audit and options evaluation complete)
    [x] Primary agent assigned (backend) and workflow assigned
        (architecture/architecture-change.md + architecture/adr-change.md)
    [x] Acceptance criteria are binary and testable
    [x] Uniqueness: highest assigned ID across repository is TASK-249; next available is TASK-250
    [x] Ownership: tasks/active/, tasks/blocked/, and tasks/backlog/ hold no open record
        naming the declared file set
-->

<!--
  Lifecycle: READY → IN PROGRESS completed 2026-10-11 by the backend agent under
  architecture/architecture-change.md: operator explicitly approved sequence
  TASK-250 → TASK-251 and selected Option 1 (Domain Overlay) for ADR-024.
  Executing ADR authoring and architecture documentation synchronization.

  Lifecycle: IN PROGRESS → IN REVIEW completed 2026-10-11 by the backend agent upon
  completing ADR-024 authoring (Proposed), registering ADR-024 in docs/03-decisions/README.md,
  synchronizing docs/02-technical/DATABASE.md and docs/02-technical/ARCHITECTURE.md,
  and executing all test and git diff validation checks. Awaiting operator review.

  Lifecycle: IN REVIEW → DONE completed 2026-10-11 by the backend agent: operator
  authorized TASK-250 Phase B completion record filing following Phase A commit
  aaba7f72dcca679ea1d5b6c57ee21b6b7add9c10. Moved to tasks/completed/.
-->

---

## Metadata

```text
Task ID:           TASK-250
Type:              ARCHITECTURE
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     backend
Supporting Agents: persistence, review, testing
Workflow:          architecture/architecture-change.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis, backend/persistence-analysis, quality/architecture-conformance, quality/documentation-consistency, quality/scope-validation
Dependencies:      TASK-249
Declared Files:    docs/03-decisions/ADR/ADR-024-database-driven-boss-balance-configuration.md, docs/03-decisions/README.md, docs/02-technical/DATABASE.md, docs/02-technical/ARCHITECTURE.md
```

---

## Objective

Author and review Architecture Decision Record **ADR-024** (`Database-driven boss balance configuration`) and synchronize authoritative architecture documentation (`docs/02-technical/DATABASE.md`, `docs/02-technical/ARCHITECTURE.md`, `docs/03-decisions/README.md`). The ADR formalizes the shift from compile-time hardcoded Boss combat balance (`MaxHP`, `ATK`, `DEF`, `EnrageThreshold`) to PostgreSQL-authoritative runtime balance, establishes battle-start snapshot isolation for in-progress battles, defines strict fail-closed validation, guarantees offline domain test decoupling, preserves canonical MVP base values (including Hỏa Long `MaxHP = 5000`, `ATK = 100`, `DEF = 50`, `EnrageThreshold = 0.30` per TASK-248), and resolves the architectural seam between static Domain definitions (`BossDefinitions.All`) and persistent rows to prevent dual conflicting runtime sources of truth.

---

## Problem Statement & Context

Currently, `docs/02-technical/DATABASE.md` §1 note item 1 explicitly dictates:
> "The combat-definition values MaxHP, ATK, DEF, EnrageThreshold (BOSS_RULES.md §6.1) are NOT stored in this table — they remain sourced from the authoritative Domain BossDefinition content at battle creation."

Furthermore, `docs/02-technical/DATABASE.md` §1 note item 2 and `src/backend/GameServer.Infrastructure/Postgres/Repositories/BossDefinitionLookup.cs` state:
> "It returns a key, not a definition... Returning a full definition would invite reading values from a second source and would be the duplicate representation GAME_STATE.md §0 item 5 forbids."

This compile-time binding was audited in `TASK-249`, which identified that any balance adjustment requires editing C# source code in `GameServer.Domain/Bosses/BossDefinitions.cs`, rebuilding server artifacts, and redeploying. However, transitioning balance to PostgreSQL introduces architectural risks that must be decided and recorded in an ADR before implementation:
1. **Conflicting Runtime Sources of Truth:** If static Domain definitions (`BossDefinitions.All`) remain in C# alongside PostgreSQL `BossDefinition` rows, the system risks dual runtime truths if code falls back to static defaults or if different subsystems read different sources.
2. **Structural Mechanics vs Adjustable Balance:** The existing EF Core mapping (`BossDefinitionConfiguration.cs` lines 176–185) deliberately excludes `BossSkillDefinition.SecondaryEffect` from persistence. If `BossDefinition` is materialized directly from PostgreSQL without preserving `SecondaryEffect`, runtime secondary skill effects (Flame Burst Burn, Drain Power, Root) would become `null` and fail to trigger.
3. **Workflow Gate:** Per `AGENTS.md` §18, `.ai/workflow/architecture/architecture-change.md` §2, and `docs/03-decisions/README.md` §5, an architecture change cannot be implemented in code (`TASK-251`) until the decision is formally proposed, reviewed, approved by the Product Owner / Operator, and documented in authoritative technical design.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Match-3 battle engine, five MVP bosses, PostgreSQL persistence; no runtime GM tools.
- `docs/01-game-design/BOSS_RULES.md` §5 item 4 — Enrage state transition rule (`BossHP < MaxHP * EnrageThreshold`).
- `docs/01-game-design/BOSS_RULES.md` §6.1 — MVP Boss base stats canonical reference (Hỏa Long: 5000/100/50/30%; Thủy Ma, Mộc Yêu, Sơn Thạch Vệ, Kim Lôi Vương).
- `docs/01-game-design/BOSS_RULES.md` §6.4 — Technical Boss identity contract (`BossId`, `PassiveId`, `SkillId`).
- `docs/02-technical/DATABASE.md` §1, §1 note items 1–5, §3, §5 item 4 — Persistence contracts for `BossDefinition`.
- `docs/02-technical/GAME_STATE.md` §0 item 5, §2.4 — `BossState` authoritative runtime structure and single-representation rule.
- `docs/02-technical/ARCHITECTURE.md` §2.1, §3, §5 — Layering boundaries, persistence repository patterns, anti-overengineering.
- `docs/03-decisions/README.md` §§1, 2, 4, 5, 7 — ADR requirements, numbering convention, index table, lifecycle status rules.
- `docs/03-decisions/ADR/ADR-005-redis-active-battle-state.md` — Redis as exclusive store for active, transient battle state.
- `docs/03-decisions/ADR/ADR-006-postgresql-persistence.md` — PostgreSQL for durable entities and configuration outside the hot swap path.
- `tasks/active/TASK-248-restore-hoa-long-canonical-base-stats.md` — Restored canonical Hỏa Long baseline stats.
- `tasks/backlog/TASK-249-design-database-driven-boss-balance-configuration.md` — Preceding architecture audit and design proposal.

---

## Scope

### In Scope
- Authoring `docs/03-decisions/ADR/ADR-024-database-driven-boss-balance-configuration.md` following the exact template in `docs/03-decisions/README.md`.
- Defining the hybrid configuration model: PostgreSQL as the authoritative runtime source of truth for battle creation, with static Domain definitions serving as compile-time defaults for migration seeding and offline unit test fixtures.
- Resolving the single-source-of-truth guarantee: strictly prohibiting silent runtime fallbacks to static Domain defaults when database records are missing or invalid (enforcing fail-closed semantics).
- Specifying exact MVP balance fields in scope (`MaxHP`, `ATK`, `DEF`, `EnrageThreshold`) versus static structural mechanics (`BossId`, `Element`, `PassiveId`, `SkillId`, `SecondaryEffect`).
- Defining how `BossDefinition` hydration maintains compatibility with non-persisted structural Domain members (specifically `BossSkillDefinition.SecondaryEffect`).
- Defining battle-start snapshot isolation in `BattleStateService` (in-memory per MVP single-instance scope, with Redis snapshot persistence explicitly deferred to a separate future task), ensuring live battles are immune to mid-game database updates.
- Defining schema extension, EF Core migration conventions, and reversible rollback procedures (`Up()` / `Down()`).
- Establishing test isolation strategy: offline deterministic Domain unit tests vs. `PostgresFixture` integration tests.
- Synchronizing `docs/02-technical/DATABASE.md` (§1, §1 notes 1–2, §3, §5) and `docs/02-technical/ARCHITECTURE.md` to reflect the approved ADR.
- Updating `docs/03-decisions/README.md` §7 index table with ADR-024.

### Out of Scope
- Implementing C# code changes in `src/backend/` (deferred to downstream implementation task `TASK-251`).
- Generating or applying EF Core migrations (deferred to `TASK-251`).
- Altering canonical gameplay stats from `BOSS_RULES.md` §6.1 (canonical values must be preserved verbatim).
- Designing runtime GM endpoints, admin portals, or live polling reloaders (out of MVP scope).
- Modifying SignalR contracts, battle events, or client presentation code.

---

## Current State

- `BossDefinitions.cs` contains static compile-time values for all 5 MVP bosses (`HoaLong`, `ThuyMa`, `MocYeu`, `SonThachVe`, `KimLoiVuong`).
- `BossDefinitionConfiguration.cs` explicitly calls `builder.Ignore(...)` for `MaxHP`, `ATK`, `DEF`, and `EnrageThreshold`, and excludes `SecondaryEffect` from the `SkillDefinition` JSON converter.
- `BattleStartService.ResolveBoss` scans `BossDefinitions.All` in memory, bypassing PostgreSQL.
- `IBossDefinitionLookup` only exposes `FindBossDefinitionIdByIdentityAsync` on the battle-end path for FK attribution on `BattleResult`.
- `DATABASE.md` §1 note 1 and note 2 explicitly document combat stats as non-persisted Domain configuration and forbid returning full definitions.
- `TASK-249` completed the initial problem audit and recommended decomposition into ADR authoring (`TASK-250`) followed by implementation (`TASK-251`).

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-250:
```text
docs/03-decisions/ADR/ADR-024-database-driven-boss-balance-configuration.md
docs/03-decisions/README.md
docs/02-technical/DATABASE.md
docs/02-technical/ARCHITECTURE.md
```

*(Note: The task manifest file itself is excluded from the implementation declared file set. The list above strictly matches the `Declared Files:` field in `## Metadata`.)*

---

## Acceptance Criteria

### 1. ADR Authoring & Structure
- [x] `docs/03-decisions/ADR/ADR-024-database-driven-boss-balance-configuration.md` is authored using the exact structure required by `docs/03-decisions/README.md` (Context, Decision, Alternatives Considered, Why, Consequences, Related Documents).
- [x] Status is set in accordance with workflow rules (`Proposed` for review, moving to `Accepted` upon operator approval and documentation synchronization).
- [x] Numbering strictly conforms to sequential convention (`ADR-024`, following `ADR-023`).

### 2. Runtime Authority & Elimination of Conflicting Sources of Truth
- [x] The ADR establishes **PostgreSQL as the single authoritative runtime source of truth** for boss balance values used during battle creation.
- [x] The role of static `BossDefinitions.All` is explicitly bounded: canonical domain baseline defaults for migration seeding and offline unit test fixtures; strictly prohibited from acting as a silent runtime fallback during battle start.
- [x] A missing or unresolvable boss record in PostgreSQL fails closed: an unknown client `bossId` produces `BOSS_NOT_FOUND` / `BattleStartOutcome.BossNotFound` (HTTP 400), while PostgreSQL infrastructure outages or corrupted records violating range constraints fail closed with HTTP 500 exceptions, rather than falling back to hardcoded Domain values or masking persistence errors.
- [x] Database connection failures fail closed (HTTP 500 / exception) without masking persistence outages.

### 3. MVP Balance Field Scope & Structural Mechanics Compatibility
- [x] Exact balance fields in scope for dynamic database tuning are defined: `MaxHP`, `ATK`, `DEF`, and `EnrageThreshold`.
- [x] Existing JSON columns for skill parameters (`baseDamage`, `chargeRequirement`, `cooldownTurns`) and passive parameters (`threshold`, `resetBehavior`) are confirmed as persisted operational data.
- [x] The ADR explicitly resolves `BossDefinition` hydration compatibility with non-persisted Domain members, specifically specifying how `BossSkillDefinition.SecondaryEffect` (Burn, Drain Power, Root) is preserved so secondary effects remain fully functional.

### 4. Battle-Start Snapshot & In-Progress Isolation
- [x] The battle-start resolution pipeline guarantees snapshot isolation: once a battle is created, its `BossState` (in Redis) and `_bossConfiguration` (in `BattleStateService`) remain immutable for that battle instance.
- [x] Mid-battle database modifications or migrations are explicitly prevented from altering running battles.

### 5. Schema, Migration, Validation & Rollback
- [x] Schema additions to `BossDefinition` table are specified: `MaxHP` (INTEGER, NOT NULL), `ATK` (INTEGER, NOT NULL), `DEF` (INTEGER, NOT NULL), `EnrageThreshold` (DOUBLE PRECISION, NOT NULL).
- [x] Invalidation / range rules are specified: `MaxHP > 0`, `ATK >= 0`, `DEF >= 0`, `0.0 < EnrageThreshold < 1.0`; violations trigger fail-closed validation exceptions.
- [x] Version-controlled EF Core migration procedure is defined: `Up()` populates existing rows with canonical values; `Down()` drops columns cleanly.
- [x] Operational balance update procedure is documented: applied via version-controlled EF Core data migrations or managed DBA migration scripts; dynamic admin UI is excluded per MVP scope.

### 6. Test Decoupling & Isolation Strategy
- [x] Unit testing strategy preserves offline test execution: `GameServer.Domain.Tests` continue using static `BossDefinitions.All` without PostgreSQL dependencies.
- [x] Application tests use mock / scripted doubles of `IBossDefinitionLookup`.
- [x] Integration tests verify PostgreSQL entity mapping, seeding, and `BattleStartService` resolution via `PostgresFixture`.

### 7. Documentation Synchronization
- [x] `docs/02-technical/DATABASE.md` is synchronized: §1 entity diagram, §1 note 1, §1 note 2, §3 schema constraints, and §5 provisioning records are updated to align with ADR-024.
- [x] `docs/02-technical/ARCHITECTURE.md` is synchronized regarding persistence boundaries and lookup responsibilities.
- [x] `docs/03-decisions/README.md` §7 index table is updated with the ADR-024 entry.

### 8. Value Preservation & Quality Gates
- [x] Canonical Hỏa Long base stats from TASK-248 and `BOSS_RULES.md` §6.1 are preserved verbatim: `MaxHP = 5000`, `ATK = 100`, `DEF = 50`, `EnrageThreshold = 0.30` (1500 HP).
- [x] Quality review checklist passes (`quality/review.md` §1).
- [x] `git diff --check` passes with zero whitespace or conflict markers.

---

## Affected Files & Areas

```text
[x] docs/03-decisions/ADR/ (ADR-024 authoring)
[x] docs/03-decisions/README.md (ADR index update)
[x] docs/02-technical/ (DATABASE.md and ARCHITECTURE.md synchronization)
[x] tasks/active/ (TASK-250 task record)
```

---

## Implementation Notes

- ADR-024 must directly address the previous restriction in `DATABASE.md` §1 note 2 ("Returning a full definition would invite reading values from a second source and would be the duplicate representation GAME_STATE.md §0 item 5 forbids"). The ADR must supersede this restriction by designating PostgreSQL as the authoritative runtime source of truth while bounding `BossDefinitions.All` to offline fixtures and seed baselines.
- The ADR must address the `SecondaryEffect` hydration gap: `BossDefinitionConfiguration.cs` lines 176–185 omits `SecondaryEffect` from `SkillDefinition` JSON. When `IBossDefinitionLookup.GetDefinitionByIdentityAsync` is implemented in TASK-251, it must either hydrate `SecondaryEffect` from the Domain structural definition or persist it, preventing silent secondary effect loss.
- Battle configuration snapshot persistence scope decision: Deferred to a separate future task per MVP single-instance deployment assumptions (`MVP_SCOPE.md` §2, `TDD.md` §7, `REDIS_STATE.md` §1) and existing battle configuration architecture (`_petConfiguration`, `_relicConfiguration`, `_consumedBossPassiveFiring`). In-memory snapshotting (`_bossConfiguration[battleId]`) provides complete snapshot isolation for the process lifetime. Process restart and multi-instance resilience is explicitly documented as a supported deployment limitation and not implied by ADR-024.
- Migration transitional defaults confirmation: Migration defaults (1, 0, 0, 0.5) are strictly transitional to allow column addition; all five existing canonical boss rows are updated within the same migration step before runtime lookup can rely on the new columns.
- Downstream task sequence:
  1. `TASK-250` (ARCHITECTURE): Author ADR-024 and synchronize architecture documentation (this task).
  2. `TASK-251` (FEATURE): Implement EF Core migration, update `BossDefinitionConfiguration`, extend `IBossDefinitionLookup`, and wire into `BattleStartService`.

---

## Testing Requirements

### Required Verification
```text
[x] Architecture documentation consistency checks across DATABASE.md, ARCHITECTURE.md, and ADR-024
[x] ADR structure compliance with docs/03-decisions/README.md
[x] Full backend test suite pass (dotnet test src/backend/GameServer.sln --nologo)
[x] Client test suite pass (npm test -- --run)
[x] Diff cleanliness check via git diff --check
```

### Key Edge Cases
- Dual-source desynchronization: database balance differs from static Domain defaults -> runtime must use database values exclusively.
- Missing database row at battle start -> rejected with `BOSS_NOT_FOUND` (400) if requested boss is unknown to MVP roster; fails closed with HTTP 500 exception if provisioned record is missing from DB.
- Database offline at battle start -> fails closed with server error (HTTP 500 exception), never falling back to static C# defaults.
- Corrupted database values (`MaxHP = -100` or `EnrageThreshold = 1.5`) -> fails closed with validation exception (HTTP 500).
- Mid-battle database update -> ongoing battle in Redis is unaffected.

---

## Stop Conditions

- If operator rejects transitioning boss balance configuration to PostgreSQL: STOP per `AGENTS.md` §18.
- If proposed ADR introduces runtime client-authoritative balance calculations: STOP per `AGENTS.md` §10.
- If task scope expands to implementing C# code or database migrations in this task: STOP per task instructions (documentation and ADR only; implementation belongs to TASK-251).
- If commit or push authorization is withheld: STOP before creating Git commits per operator instructions.

---

## Completion Evidence

### Commit
- Implementation slice (Phase A): `aaba7f72dcca679ea1d5b6c57ee21b6b7add9c10`
- Subject: `docs(adr): record database-driven boss balance configuration decision in ADR-024`
- Owns: `TASK-250`
- Files: `docs/03-decisions/ADR/ADR-024-database-driven-boss-balance-configuration.md, docs/03-decisions/README.md, docs/02-technical/DATABASE.md, docs/02-technical/ARCHITECTURE.md`
- Shared with: `none`
- Unowned / pre-existing: `none`
- Completion record filing (Phase B): this completion record alone, filed under its own bookkeeping commit (`tasks/completed/TASK-250-author-database-driven-boss-balance-adr.md`).

### Changed Files
- `docs/03-decisions/ADR/ADR-024-database-driven-boss-balance-configuration.md` — Author ADR-024 (Status: Accepted).
- `docs/03-decisions/README.md` — Register ADR-024 in ADR index.
- `docs/02-technical/DATABASE.md` — Synchronize entity diagram, persistence contracts, constraints, and notes.
- `docs/02-technical/ARCHITECTURE.md` — Synchronize lookup boundaries and component responsibilities.

### Validation Results
- `dotnet test src/backend/GameServer.sln --nologo` — PASS (2974 tests passed, 0 failed across full backend suite during Phase A verification gate)
- `npm test -- --run` (src/frontend/client) — PASS (23 test files, 991 tests passed across full client suite during Phase A verification gate)
- `git diff --check` — PASS (zero whitespace issues or conflict markers)
- Staging verification — `git diff --cached --name-status` matches strictly `tasks/completed/TASK-250-author-database-driven-boss-balance-adr.md` (P-5 compliant)

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed P-1…P-6 commit policy compliance
