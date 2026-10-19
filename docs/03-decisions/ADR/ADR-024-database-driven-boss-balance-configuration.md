# ADR-024: Database-Driven Boss Balance Configuration (Domain Overlay Model)

**Status:** Accepted
**Date:** 2026-10-11
**Amended:** 2026-10-19 (TASK-308 / ADR-029 Slice 3) — the Domain Overlay is
extended from four to five values by the approved nullable
`CastPriorityScore` column (`INTEGER NULL`,
`CHECK (CastPriorityScore IS NULL OR CastPriorityScore >= 0)`). The
PostgreSQL-authority, structural-preservation, fail-closed and no-fallback
semantics of this decision are unchanged; §2.2, §2.4 and §2.5 record the
extension.

---

## 1. Context

In the DCacti battle engine, Boss identity, mechanics, and combat balance are defined across two layers:
1. **Domain Definitions:** `src/backend/GameServer.Domain/Bosses/BossDefinitions.cs` hardcodes static definitions for the five MVP bosses (`HoaLong`, `ThuyMa`, `MocYeu`, `SonThachVe`, `KimLoiVuong`). Base combat stats (`MaxHP`, `ATK`, `DEF`, `EnrageThreshold`) are defined directly on `BossDefinition` records.
2. **PostgreSQL Persistence:** Table `BossDefinition` persists `BossDefinitionId` (PK), `Identity` (unique), `Element`, `PassiveDefinition` (JSONB), and `SkillDefinition` (JSONB). However, `src/backend/GameServer.Infrastructure/Postgres/Configurations/BossDefinitionConfiguration.cs` lines 114–117 explicitly invoke:
   ```csharp
   builder.Ignore(definition => definition.MaxHP);
   builder.Ignore(definition => definition.ATK);
   builder.Ignore(definition => definition.DEF);
   builder.Ignore(definition => definition.EnrageThreshold);
   ```
   Furthermore, `docs/02-technical/DATABASE.md` §1 note item 1 specifically dictates that combat stats are non-persisted Domain configuration, and §1 note item 2 states that `IBossDefinitionLookup` returns only a key on the battle-end path rather than a full definition to prevent duplicate representation (`GAME_STATE.md` §0 item 5).

### Problem Statement

This compile-time binding was audited under `TASK-249`. Because base stats are hardcoded in C# and ignored in PostgreSQL:
1. **Source-Coupled Balance Pacing:** Any balance adjustment (such as the TASK-248 canonical restoration of Hỏa Long) requires modifying C# source code, rebuilding backend server containers, and deploying a new application release.
2. **Persistence Boundary Asymmetry:** While `PetDefinition`, `CardDefinition`, and `RelicDefinition` store their operational balance parameters in PostgreSQL, `BossDefinition` cannot be adjusted via migrations or database tooling.

However, moving boss balance to PostgreSQL introduces two major architectural hazards:
- **Hazard 1: Conflicting Runtime Sources of Truth.** If static Domain definitions (`BossDefinitions.All`) remain in C# alongside database columns, any runtime fallback on missing or corrupted database records would create silent split-brain configuration drift.
- **Hazard 2: Secondary Effect Destruction.** `BossSkillDefinition.SecondaryEffect` (carrying Flame Burst's Burn DoT, Drain Power's Pet Power reduction, and Root's Pet ATK debuff per `BOSS_RULES.md` §6.3.1) is excluded from PostgreSQL persistence in `BossDefinitionConfiguration.cs` (lines 176–185). If `BossDefinition` is materialized directly from PostgreSQL as a plain EF Core entity, `bossDefinition.SkillDefinition.SecondaryEffect` becomes `null`, silently breaking all boss secondary skill effects at runtime (`BattleStateService.cs` line 1960).

---

## 2. Decision

The architecture adopts **Option 1 — Domain Overlay**:

### 2.1 Single Runtime Source of Truth (PostgreSQL Authority)
- **PostgreSQL is authoritative at runtime** for active battle creation. When `POST /api/battle/start` resolves a boss, combat balance values are sourced from PostgreSQL.
- **Static Domain Definitions (`BossDefinitions.All`)** remain in the codebase with specific, bounded responsibilities:
  1. Canonical compile-time baseline defaults for database migration seeding (`BOSS_RULES.md` §6.1).
  2. Test fixtures for fast, deterministic, offline unit testing (`GameServer.Domain.Tests`).
  3. Authoritative structural definitions of boss mechanics (Element affinity, Skill/Passive IDs, and Skill Secondary Effects).
- **No Silent Runtime Fallback:** Static `BossDefinitions.All` is **strictly forbidden** from serving as an automatic runtime fallback if the database row is absent or invalid.
- **Error Semantics & Failure Mapping:** Failures are distinguished according to their source rather than conflating all errors into HTTP 400:
  1. *Unknown or invalid Boss requested by client:* If the client provides a `bossId` that does not match a known MVP Boss identity, `BattleStartService` returns `BattleStartOutcome.BossNotFound`, producing HTTP 400 Bad Request with `{ "error": "BOSS_NOT_FOUND", "message": "The selected Boss is not a valid MVP Boss." }` per `API_CONTRACTS.md` §3 and §6. (No invented error codes such as `INVALID_BOSS` are used).
  2. *PostgreSQL infrastructure or connectivity failure:* If PostgreSQL is unreachable, connection times out, or a database exception occurs (`NpgsqlException`, `DbException`), the exception bubbles up unhandled to ASP.NET Core's pipeline and returns HTTP 500 Internal Server Error. The exception is never caught, masked, or translated into a client 400 Bad Request.
  3. *Missing database provisioned row or corrupt balance configuration:* If the requested Boss is a valid canonical MVP Boss but its corresponding PostgreSQL row is missing, or the row contains values that violate domain range invariants (`MaxHP <= 0`, `ATK < 0`, `DEF < 0`, `EnrageThreshold <= 0.0 || >= 1.0`), this indicates a server data integrity or provisioning failure. It fails closed by throwing an `InvalidOperationException`, bubbling up as HTTP 500 Internal Server Error (never returned as client 400 Bad Request).

### 2.2 Domain Overlay Hydration Mechanism
To decouple combat balance while preserving complex Domain mechanics without risk of nullifying secondary effects:
1. The PostgreSQL `BossDefinition` table is extended with the four balance
   columns below and, per TASK-308, the fifth overlay value — the nullable
   cast-priority score:
   - `MaxHP` (`INTEGER NOT NULL`)
   - `ATK` (`INTEGER NOT NULL`)
   - `DEF` (`INTEGER NOT NULL`)
   - `EnrageThreshold` (`DOUBLE PRECISION NOT NULL`)
   - `CastPriorityScore` (`INTEGER NULL`,
     `CHECK (CastPriorityScore IS NULL OR CastPriorityScore >= 0)`) — `NULL`
     means "no cast-priority score authored" and is legal only while the Boss
     has no Boss Card; a Boss Card form requires a non-null value
     (`BOSS_RULES.md` §4.3 item 5, §4.4 item 6)
2. `IBossDefinitionLookup` (Application layer) is extended with runtime lookup:
   ```csharp
   Task<BossDefinition?> GetDefinitionByIdentityAsync(
       string bossIdentity,
       CancellationToken cancellationToken = default);
   ```
3. The Infrastructure implementation queries PostgreSQL for the persisted row by `Identity`. It then matches the technical identity against the structural Domain baseline (`BossDefinitions.All`) and applies the runtime balance values via record mutation:
   ```csharp
   var domainBaseline = BossDefinitions.All.FirstOrDefault(b => b.BossId == identity);
   if (domainBaseline == null) return null;

   return domainBaseline with
   {
       MaxHP = dbRow.MaxHP,
       ATK = dbRow.ATK,
       DEF = dbRow.DEF,
       EnrageThreshold = dbRow.EnrageThreshold,
       CastPriorityScore = dbRow.CastPriorityScore
   };
   ```
4. **Structural Preservation:** `SecondaryEffect` (Burn, Drain Power, Root), `Element`, `PassiveDefinition`, and `SkillDefinition` mechanical typings remain intact from the Domain definition, completely eliminating the nullification hazard without requiring complex polymorphic JSON storage in PostgreSQL for MVP.

### 2.3 Battle-Start Snapshot, Lifecycle, & Supported Deployment Limitation
- During `BattleStartService.StartBattleAsync`, the resolved, database-overlaid `BossDefinition` is supplied to `BattleStateService.CreateBattleAsync`.
- Active battle state in Redis stores `BossState` initialized from the overlaid definition (`HP = MaxHP`, `MaxHP`, `ATK`, `DEF`, `State = Idle`).
- `BattleStateService` registers the definition in its battle-scoped configuration (`_bossConfiguration[battleId]`).
- All subsequent turn resolutions (`ExecuteSwapAsync`, `CastCardAsync`) consume the battle-scoped configuration attached at creation.
- **In-Progress Isolation Guarantee:** Subsequent database updates or migrations never alter running battles during the lifetime of the server process.
- **Scope Decision for Redis Snapshot Persistence (Deferred to Future Task):**
  - *MVP Deployment Assumptions:* The authoritative deployment architecture for MVP assumes a single ASP.NET Core server process instance (`MVP_SCOPE.md` §2, `TDD.md` §7). `REDIS_STATE.md` §1 strictly restricts Redis keys to `battle:{battleId}:state` and `battle:{battleId}:lock`, explicitly stating: *"No other Redis keys are needed for MVP (no leaderboard, no pub/sub channel beyond what SignalR's own backplane may require if the server is scaled to multiple instances — out of scope for MVP, single-instance assumed per TDD.md §7)"*.
  - *Architectural Parity Across Battle Configurations:* In the current system, all battle-scoped static inputs—including `_petConfiguration` (Pet reset behavior), `_relicConfiguration` (equipped relic definitions), `_consumedBossPassiveFiring` (one-time boss passive tracking), and `_bossConfiguration`—reside in process memory within `BattleStateService` (`REDIS_STATE.md` §1, `BattleStateService.cs` lines 299–362). Persisting only `BossConfiguration` to Redis in TASK-251 would not achieve process-restart resilience because turn resolution would still fail due to lost `_relicConfiguration` (relic cascade/turn triggers silent drop) and lost `_consumedBossPassiveFiring`.
  - *Decision:* Redis configuration snapshot persistence is **deferred to a separate future task** (holistic battle-session configuration durability and cross-process recovery). TASK-251 remains strictly focused on PostgreSQL schema migration, entity mapping, and runtime lookup wiring.
  - *Explicit Supported Deployment Limitation:* ADR-024 and TASK-251 do **not** provide or imply process restart or multi-instance horizontal resilience for turn resolution. While the active `BattleState` in Redis survives server restarts and supports reconnect inspection via `GetBattleState` (`SIGNALR_PROTOCOL.md` §7), subsequent turn actions (swaps/cards) require unbroken process memory. If the server process restarts or requests are routed across multiple instances, in-memory battle configurations are lost and turn execution cannot proceed. Cross-process battle-session configuration durability is a separate future concern.

### 2.4 Fail-Closed Validation
When overlaying values from PostgreSQL, strict domain range invariants are enforced:
- `MaxHP > 0`
- `ATK >= 0`
- `DEF >= 0`
- `0.0 < EnrageThreshold < 1.0`
- `CastPriorityScore IS NULL OR CastPriorityScore >= 0` (TASK-308). `NULL` is
  the explicit "not authored" marker and is valid only for a Boss with no Boss
  Card; a Boss whose provisioned Boss Card is present must carry a non-null,
  non-negative score, otherwise battle creation fails closed. No default, no
  fallback weight and no board-only degradation is substituted.
Any row violating these ranges throws an `InvalidOperationException` and bubbles up through ASP.NET Core's pipeline as HTTP 500 Internal Server Error, failing battle creation closed without masking database corruption as a client request error.

### 2.5 Migration, Versioning, and Rollback
- Balance changes are managed exclusively through version-controlled EF Core migrations (`migrationBuilder.UpdateData`) or managed DBA migration scripts.
- **Transitional Defaults vs. Canonical Population:**
  Column default values (`defaultValue: 1`, `defaultValue: 0`, `defaultValue: 0`, `defaultValue: 0.5`) in `AddColumn` are **strictly transitional** to permit adding non-nullable columns to the existing PostgreSQL table without violating schema constraints. They are never relied upon by runtime lookups.
  In the exact same migration step (`Up()`), every canonical existing boss row is explicitly updated with its verified canonical values from `BOSS_RULES.md` §6.1 via `migrationBuilder.UpdateData` before check constraints are activated:
  - Hỏa Long: `MaxHP = 5000`, `ATK = 100`, `DEF = 50`, `EnrageThreshold = 0.30`
  - Thủy Ma: `MaxHP = 5000`, `ATK = 100`, `DEF = 50`, `EnrageThreshold = 0.30`
  - Mộc Yêu: `MaxHP = 5000`, `ATK = 100`, `DEF = 50`, `EnrageThreshold = 0.30`
  - Sơn Thạch Vệ: `MaxHP = 3000`, `ATK = 120`, `DEF = 0`, `EnrageThreshold = 0.50`
  - Kim Lôi Vương: `MaxHP = 2800`, `ATK = 140`, `DEF = 0`, `EnrageThreshold = 0.75`
  Check constraints (`CK_BossDefinition_MaxHP_Positive`, `CK_BossDefinition_ATK_NonNegative`, `CK_BossDefinition_DEF_NonNegative`, `CK_BossDefinition_EnrageThreshold_Range`) are applied immediately following this update. The EF Core entity mapping (`BossDefinitionConfiguration.cs`) does not define default values (`HasDefaultValue`), ensuring any unpopulated insert fails database constraints.
- **Initial Schema & Seed Migration (`Up`):**
  1. Adds the four balance columns to table `BossDefinition` with the transitional default values:
     - `MaxHP` (`integer NOT NULL`, `defaultValue: 1`)
     - `ATK` (`integer NOT NULL`, `defaultValue: 0`)
     - `DEF` (`integer NOT NULL`, `defaultValue: 0`)
     - `EnrageThreshold` (`double precision NOT NULL`, `defaultValue: 0.5`)
  2. Updates all five existing provisioned rows (`boss-def-hoa-long`, `boss-def-thuy-ma`, `boss-def-moc-yeu`, `boss-def-son-thach-ve`, `boss-def-kim-loi-vuong`) with their exact canonical baseline values.
  3. Adds check constraints enforcing schema invariants:
     - `CK_BossDefinition_MaxHP_Positive` (`"MaxHP" > 0`)
     - `CK_BossDefinition_ATK_NonNegative` (`"ATK" >= 0`)
     - `CK_BossDefinition_DEF_NonNegative` (`"DEF" >= 0`)
     - `CK_BossDefinition_EnrageThreshold_Range` (`"EnrageThreshold" > 0.0 AND "EnrageThreshold" < 1.0`)
   4. Drops the four transitional defaults (`ALTER COLUMN ... DROP DEFAULT`) via `AlterColumn`, ensuring direct SQL inserts and entity inserts cannot inherit transitional values and fail closed with `NOT NULL` violations if omitted.
- **Safe Rollback Behavior (`Down`):**
  1. Drops the four check constraints.
  2. Drops the four balance columns (`EnrageThreshold`, `DEF`, `ATK`, `MaxHP`).
  3. **Row Preservation:** Existing `BossDefinition` rows must NOT be deleted. Existing `BattleResult` records hold foreign key references (`BattleResult.BossDefinitionId FK`), so dropping rows would violate FK constraints. Reversion cleanly restores `BossDefinition` to its pre-migration 5-column structure without row loss.
- **Subsequent Balance Adjustments:**
  Future balance modifications are executed via dedicated data-only EF Core migrations using `migrationBuilder.UpdateData` targeting exact canonical primary keys, preserving auditability and Git history.
- **Fifth overlay value (TASK-308, ADR-029 Slice 3):** the nullable
  `CastPriorityScore` column is added by its own migration with
  `nullable: true` (no transitional default is needed because `NULL` is the
  meaningful "not authored" value), followed by
  `CHECK (CastPriorityScore IS NULL OR CastPriorityScore >= 0)`, and a data-only
  `UpdateData` setting `earthquake`'s approved score (`16`). The rollback drops
  the check constraint and then the column; no row is deleted and no
  `BattleResult` foreign key is touched. The same fail-closed overlay and
  no-fallback discipline applies to the fifth value.
- Dynamic runtime admin dashboards or live in-memory configuration reloaders are explicitly excluded from MVP scope (`MVP_SCOPE.md` §1).

---

## 3. Alternatives Considered

### Option A: Source-Only Configuration (Status Quo)
- **Description:** Retain combat stats hardcoded in C# `BossDefinitions.cs` and ignored in EF Core.
- **Why Rejected:** Violates the core objective. Any pacing adjustment requires C# source edits, rebuilds, and container redeployments.

### Option B: Full Dynamic Entity Persistence (Database-Only)
- **Description:** Remove static `BossDefinitions.All` entirely; store all boss metadata in PostgreSQL and load entities purely via EF Core.
- **Why Rejected:**
  - Severely impairs domain testability by forcing over 380 existing unit test references to mock EF Core or depend on live PostgreSQL instances.
  - Requires migrating `BossSkillSecondaryEffect` into polymorphic JSON columns, introducing unnecessary risk for MVP when only scalar combat numbers need tuning.

### Option C: Full Entity Persistence with SecondaryEffect JSON Expansion
- **Description:** Store the four balance columns in `BossDefinition` and also extend `SkillDefinition` JSON column to store `SecondaryEffect` (`Kind`, `Type`, `TargetStat`, `Magnitude`, `DurationTurns`).
- **Why Deferred:** Adds schema and serialization complexity for effects that are static gameplay mechanics rather than tuning knobs. Option 1 achieves the tuning objective with zero risk of secondary effect disruption.

---

## 4. Consequences

### Positive
- **Operational Pacing Decoupling:** Boss balance can be tuned via version-controlled database migrations without C# code recompilation or application redeployment.
- **Zero Loss of Secondary Mechanics:** Flame Burst Burn, Drain Power, and Root remain 100% functional because `SecondaryEffect` is preserved through Domain overlay.
- **Test Suite Decoupling:** Over 2,900 unit and domain tests continue running offline, fast, and deterministically without database dependencies.
- **Clean In-Progress Isolation:** Live battles remain unaffected by runtime balance modifications.

### Negative / Trade-offs
- Adding an entirely new Boss in the future requires both a Domain static entry and a database provisioning migration. (Acceptable trade-off: MVP scope fixes the boss roster to exactly five content-defined bosses).
- Requires updating `docs/02-technical/DATABASE.md` §1 notes 1 and 2 to supersede the previous prohibition on runtime boss definition lookup.
- **Supported Deployment Limitation:** In-memory battle-scoped configuration (`_bossConfiguration`, along with `_petConfiguration`, `_relicConfiguration`, and `_consumedBossPassiveFiring`) is tied to the server process lifetime. While active `BattleState` in Redis survives restarts for reconnect inspection (`GetBattleState`), subsequent turn resolution does not survive application process restarts or multi-instance load balancing. Redis configuration snapshot persistence is deferred to a future cross-process session recovery task per MVP single-instance assumptions (`MVP_SCOPE.md` §2, `TDD.md` §7, `REDIS_STATE.md` §1).

---

## 5. Related Documents

- `docs/00-overview/MVP_SCOPE.md` §1 — Core Match-3 Battle System, 5 MVP Bosses, PostgreSQL persistence.
- `docs/01-game-design/BOSS_RULES.md` §6.1 — MVP Boss base stats canonical reference (Hỏa Long HP 5000, ATK 100, DEF 50, Enrage 1500 / 30%).
- `docs/01-game-design/BOSS_RULES.md` §6.3.1 — Boss Skill secondary effect mechanics.
- `docs/02-technical/DATABASE.md` §1, §3, §5 — Database entities, constraints, and provisioning rules.
- `docs/02-technical/GAME_STATE.md` §0 item 5, §2.4 — Battle state architecture and single-representation principle.
- `docs/02-technical/ARCHITECTURE.md` §2.1, §3 — Layer boundaries and persistence lookup contracts.
- `docs/03-decisions/ADR/ADR-005-redis-active-battle-state.md` — Active battle state storage in Redis.
- `docs/03-decisions/ADR/ADR-006-postgresql-persistence.md` — Persistent entities in PostgreSQL.
- `tasks/active/TASK-248-restore-hoa-long-canonical-base-stats.md` — Restoration of canonical Hỏa Long base stats.
- `tasks/backlog/TASK-249-design-database-driven-boss-balance-configuration.md` — Architecture audit and design options.
- `tasks/active/TASK-250-author-database-driven-boss-balance-adr.md` — Task record for ADR-024 authoring and synchronization.
