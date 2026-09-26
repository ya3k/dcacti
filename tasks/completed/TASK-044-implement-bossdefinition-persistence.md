# TASK-044 — Implement BossDefinition Persistence

---

## Metadata

```text
Task ID:           TASK-044
Type:              FEATURE
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     persistence
Supporting Agents: gameplay, testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis,
                   backend/persistence-analysis, quality/architecture-conformance,
                   testing/test-scenario-generation
Dependencies:      TASK-020 (DONE); TASK-045 (DONE), TASK-046 (DONE),
                   TASK-047 (DONE), TASK-048 (DONE), TASK-049 (DONE) — the
                   Boss persistence/identity contract chain this task
                   implements; all resolved, no open blocker
```

---

## Objective

Create the `BossDefinition` table per `DATABASE.md` §1 — EF Core entity
mapping, `GameDbContext` DbSet, and a new migration — so the documented
`BattleResult.BossDefinitionId` foreign key (`DATABASE.md` §1, §2) has a
persisted target when TASK-041 implements `BattleResult`. Every column
representation is now fully determined by `DATABASE.md` §1 (persistence
contract note items 1–5, TASK-045/TASK-049), §3, and the `BOSS_RULES.md`
§6 / §6.4 identity and configuration contract; no Boss behavior, content
decisions, rewards, battle-end writes, or row-provisioning mechanism is
invented here. The one Domain addition required is
`required string BossDefinitionId` on the `BossDefinition` record, with the
three resolved values supplied in `BossDefinitions.cs`.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 (Technical — "Persistent storage
  (PostgreSQL)"; Bosses: Element / Passive / Skill per Boss), §4 — scope rule
- `docs/02-technical/DATABASE.md` §1 — `BossDefinition` entity sketch
  (columns `BossDefinitionId`, `Identity`, `Element`, `PassiveDefinition`,
  `SkillDefinition`), §1 note on `BattleResult.BossDefinitionId` FK
- `docs/02-technical/DATABASE.md` §1 persistence-contract note **item 2**
  (TASK-049) — `BossDefinitionId` is a **caller/content-supplied stable key**
  (`required string BossDefinitionId` on the Domain record, stored as the PK),
  **never database-generated** and never derived from `Identity` or the display
  name; value form `boss-def-<ascii-kebab-case-name>`; canonical values
  `boss-def-hoa-long` / `boss-def-thuy-ma` / `boss-def-moc-yeu`
- `docs/02-technical/DATABASE.md` §1 persistence-contract note **items 3–4**
  (TASK-045) — exact `PassiveDefinition` / `SkillDefinition` JSON member lists,
  nullability, and the reset-token set
- `docs/02-technical/DATABASE.md` §3 — the documented `BossDefinition`
  constraints (`BossDefinitionId` and `Identity` NOT NULL/UNIQUE, both JSON
  objects NOT NULL, reset-token set, threshold-null ⇔ always-active)
- `docs/02-technical/DATABASE.md` §4 — indexes (none documented for
  `BossDefinition`) and §5 item 1 (exact SQL types are an implementation
  detail) and §5 item 4 (no seed/provisioning mechanism)
- `docs/02-technical/DATABASE.md` §2 — `BattleResult` N─1 `BossDefinition`
- `docs/01-game-design/BOSS_RULES.md` §6 — MVP Boss configuration
- `docs/01-game-design/BOSS_RULES.md` §6.4 — BossId / PassiveId / SkillId
  identity contract (the three content-defined MVP Bosses and their values);
  `BossDefinitionId` is the persistence PK owned by `DATABASE.md` §1
- `docs/01-game-design/PASSIVE_RULES.md` §1–§4 — threshold / effect reference
  semantics (only if a persisted column requires them)
- `docs/02-technical/ARCHITECTURE.md` §2 (layer direction — Infrastructure
  owns persistence), §5 item 1 (no plugin system; concrete domain type with
  data-driven configuration)
- `docs/03-decisions/ADR/ADR-006-postgresql-persistence.md` — PostgreSQL
  persistence
- `AGENTS.md` §7 (no invented rules), §9 (no speculative abstractions),
  §12 (Boss domain boundary), §20 (stop conditions)

---

## Scope

### In Scope

- EF Core entity type + configuration for `BossDefinition` in
  `src/backend/GameServer.Infrastructure/Postgres/Configurations/`, with
  columns and PK exactly as `DATABASE.md` §1 defines
- `GameDbContext` DbSet registration
- A new migration creating the table, following the existing
  `AddPlayerPersistence` / `AddPetPersistence` / `AddRelicPersistence` /
  `AddCardPersistence` migration series
- Mapping the Domain `BossDefinition` record's documented fields
  (`src/backend/GameServer.Domain/Bosses/BossDefinition.cs`) onto those
  columns, using only representations derivable from the Authoritative
  References
- Persistence tests in the existing `GameServer.Infrastructure.Tests`
  pattern: documented-fields assertion (as `CardPersistenceTests`) and
  insert/read-back round-trip via `TestGameDbContextFactory`
- Documentation impact: align the `BossDefinition.cs` XML header claim
  "No registry, service, or persistence" once persistence exists
  (`AGENTS.md` §17)

### Out of Scope

- **`BattleResult` entity, configuration, migration, repository, and its
  battle-end write** — TASK-041 owns that (`DATABASE.md` §1; `GAME_STATE.md`
  §2.8 item 4)
- **Row provisioning / seeding / deciding which Boss rows exist at runtime**
  — no mechanism is documented (`DATABASE.md` §1 calls it static content);
  report the gap per `AGENTS.md` §21 — do not invent a seed system
- **Boss AI, Boss State, combat behavior, Passive/Skill resolution**
  — TASK-020 / TASK-022 / `BOSS_RULES.md` §3
- **Rewards / XP** — TASK-033 owns
- **Any repository / read path / BossRegistry / BossService abstraction**
  — no documented query exists; `ARCHITECTURE.md` §5 item 1 and
  `AGENTS.md` §9 forbid speculative infrastructure
- **Any index beyond the documentation** — `DATABASE.md` §4 documents no
  `BossDefinition` index; do not add one
- **Frontend changes**; any item listed as OUT in `MVP_SCOPE.md` §2

---

## Current State

**Re-synchronized after the TASK-045 → TASK-049 contract chain completed
(readiness audit 2026-09-26). The contract is now fully determined; read the
current `docs/` at execution time rather than relying on this summary.**

- Seven EF configurations + DbSets exist in
  `src/backend/GameServer.Infrastructure/Postgres/`: Player, Pet,
  PetDefinition, Relic, RelicDefinition, CardDefinition,
  PlayerUnlockedCard (`GameDbContext.cs`); four migrations exist
  (`AddPlayerPersistence`, `AddPetPersistence`, `AddRelicPersistence`,
  `AddCardPersistence`). Configurations are applied by
  `ApplyConfigurationsFromAssembly`, and `ForeignKeyIndexConvention` is
  removed by `GameDbContext`, so no implicit FK index is scaffolded.
- No `Boss*` persistence exists anywhere in Infrastructure (grep confirmed:
  zero matches for Boss in `src/backend/GameServer.Infrastructure/`,
  excluding `bin`/`obj`)
- Domain carries `BossDefinition` (`readonly record struct`, no
  `BossDefinitionId` member) and `BossDefinitions` (static in-code content for
  the three content-defined MVP Bosses) —
  `src/backend/GameServer.Domain/Bosses/`
- **The column mapping is now fully determined** (`DATABASE.md` §1
  persistence-contract note items 1–5, §3):
  `BossDefinitionId` (PK, required string, content-supplied — TASK-049),
  `Identity` (canonical technical Boss ID, NOT NULL + UNIQUE), `Element`
  (enum → numeric, the sibling convention; representation is an
  implementation detail under §5 item 1), `PassiveDefinition` and
  `SkillDefinition` (JSON objects, NOT NULL, with exact fixed member lists).
  `MaxHP` / `ATK` / `DEF` / `EnrageThreshold` are explicitly **not** persisted
  (§1 note item 1) — do not promote them to columns.
- **Domain change required:** the record must gain
  `required string BossDefinitionId` (TASK-049), which also requires supplying
  the resolved values in `BossDefinitions.cs`
  (`boss-def-hoa-long` / `boss-def-thuy-ma` / `boss-def-moc-yeu`). This is a
  mechanical consequence of the resolved contract, not a new decision.
- `BossDefinition.cs` L39–47 XML comment states "No registry, service, or
  persistence" — non-authoritative prose; becomes stale if this task lands
  (documentation impact, not a blocker). Keep the no-registry/no-service part
  intact (`ARCHITECTURE.md` §5 item 1 still applies).
- JSON columns are net-new: no existing configuration maps a JSON column, so
  the storage mechanism is a fresh EF implementation detail (the member
  contract is authoritative; the SQL type is not — `DATABASE.md` §5 item 1).
- Existing test seam: `TestGameDbContextFactory.cs`; precedent test:
  `CardPersistenceTests.CardDefinition_ShouldCarryExactlyTheDocumentedFields`
- Provisioning remains undocumented and must not be invented
  (`DATABASE.md` §1 note item 5, §5 item 4): no `HasData`, seed, startup
  loader, or migration-inserted production row. Only the three
  content-defined Bosses exist; do not invent the missing two.

---

## Acceptance Criteria

- [x] `BossDefinition` entity + EF configuration exist in
      `GameServer.Infrastructure/Postgres/` with a `GameDbContext` DbSet and
      a migration creating the table with `DATABASE.md` §1's documented
      columns and PK
- [x] The Domain `BossDefinition` record carries
      `required string BossDefinitionId`, and `BossDefinitions.cs` supplies
      `boss-def-hoa-long` / `boss-def-thuy-ma` / `boss-def-moc-yeu`
- [x] `BossDefinitionId` is the caller-supplied string PK — not
      database-generated, not derived from `Identity` or the display name
- [x] `Identity` is stored as the canonical technical Boss ID
      (`boss-hoa-long` / `boss-thuy-ma` / `boss-moc-yeu`), NOT NULL + UNIQUE,
      and is distinct from `BossDefinitionId`
- [x] `Element` is persisted using the established sibling enum convention
- [x] `PassiveDefinition` is a NOT NULL JSON object whose members are exactly
      `passiveId` / `threshold` / `resetBehavior`, with
      `threshold: null` (never `0`) for Thủy Ma's always-active Passive and
      `resetBehavior` restricted to `Default` | `Partial` | `Persistent`
- [x] `SkillDefinition` is a NOT NULL JSON object whose members are exactly
      `skillId` / `baseDamage` / `chargeRequirement` / `cooldownTurns`
- [x] `MaxHP` / `ATK` / `DEF` / `EnrageThreshold` are **not** persisted as
      columns (`DATABASE.md` §1 note item 1)
- [x] Insert + read-back round-trip through `TestGameDbContextFactory`
      preserves the row unchanged
- [x] A documented-fields test asserts the entity carries exactly the
      documented columns (pattern:
      `CardPersistenceTests.CardDefinition_ShouldCarryExactlyTheDocumentedFields`)
- [x] The migration applies cleanly alongside the four existing migrations
      and alters no existing table or index (`DATABASE.md` §4)
- [x] No `BattleResult`, reward, Boss behavior, or seed/provisioning code
      added; no new repository/registry/service abstraction; no undocumented
      index
- [x] All relevant tests pass at the required validation depth
      (`core/validation.md` §2)
- [x] Quality review checklist passes (`quality/review.md` §1)
- [x] No authoritative rules or contracts violated (`AGENTS.md` §7 / §9 / §12)

---

## Completion Criteria

```text
[x] Requirement understood; MVP scope checked (MVP_SCOPE.md §1, §4)
[x] Authoritative References read; no documentation conflict introduced (AGENTS.md §4)
[x] Existing implementation checked (see Current State)
[x] Plan created under development/feature.md
[x] Code implemented in Infrastructure/Postgres only (entity config, DbSet,
    migration) + Domain XML doc-comment alignment if persistence lands
[x] Relevant tests added/updated and passing
[x] Ambiguity stop conditions evaluated; if any fired, reported instead of
    invented (see Stop Conditions)
[x] No unrelated behavior changed (AGENTS.md §16); tasks/completed/ untouched
[x] Completion Evidence filled below
```

---

## Affected Files & Areas

```text
[x] src/backend/ (Infrastructure/Postgres — entity configuration, DbSet,
    migration; Domain — add `required string BossDefinitionId` to
    BossDefinition.cs, supply the three values in BossDefinitions.cs, and
    align the stale "No registry, service, or persistence" XML header)
[x] src/frontend/client/ (scenes / runtime / services / state / ui)
[x] tests/ (unit / integration persistence tests)
[x] docs/ (documentation updates if a gap/conflict is found — report per
    AGENTS.md §4; do not edit silently)
```

---

## Implementation Notes

- Follow the exact shape of the existing
  `Configurations/*DefinitionConfiguration.cs` files (closest precedents:
  `PetDefinitionConfiguration.cs`, `CardDefinitionConfiguration.cs`,
  `RelicDefinitionConfiguration.cs`) and the `Add*Persistence` migration
  series; `TestGameDbContextFactory` is the test seam.
- `DATABASE.md` §5 item 1: exact SQL types are an implementation detail —
  choose them in line with the sibling definition tables.
- **Column representations are now resolved — do not re-decide them.**
  `DATABASE.md` §1 persistence-contract note fixes them:
  - `BossDefinitionId` — caller/content-supplied string PK, value form
    `boss-def-<ascii-kebab-case-name>` (note item 2, TASK-049). Add
    `required string BossDefinitionId` to the Domain record and supply the
    three resolved values in `BossDefinitions.cs`.
  - `Identity` — the Domain `BossId.Value`, NOT NULL + UNIQUE (§3).
  - `Element` — enum → numeric, the established sibling convention (§1;
    `PetDefinitionConfiguration` / `CardDefinitionConfiguration`).
  - `PassiveDefinition` — JSON object, NOT NULL, members exactly
    `{passiveId, threshold, resetBehavior}` (note item 3). `threshold` is
    `int | null` and `null` means always-active; **`0` must never be persisted
    as the always-active sentinel**. The Domain carries
    `int PassiveThreshold` with `0` as its in-code always-active marker
    (`Thủy Ma`), so persistence maps that marker to `null` — the JSON names
    are the contract and the internal representation maps to them
    (`DATABASE.md` §1 note item 3). `resetBehavior` is exactly one of
    `Default` | `Partial` | `Persistent`; the internal
    `PassiveResetBehavior` enum maps `Persistent` → `NoReset`.
  - `SkillDefinition` — JSON object, NOT NULL, members exactly
    `{skillId, baseDamage, chargeRequirement, cooldownTurns}` (note item 4).
  - `MaxHP` / `ATK` / `DEF` / `EnrageThreshold` remain **sourced from the
    Domain content** and are **not** columns (§1 note item 1).
  - Do not mirror a sibling's mapping as authority; the sibling
    configurations are precedent, not documentation (`AGENTS.md` §7).
- Domain `BossDefinition.cs` XML header currently claims "No registry,
  service, or persistence" — if persistence lands, that claim is false;
  align it in the same change (`AGENTS.md` §17). Keep the no-registry /
  no-service part intact (`ARCHITECTURE.md` §5 item 1 still applies).
- The `5 MVP Bosses` vs `3 content-defined` question is **resolved, not a
  conflict**: `DATABASE.md` §1 header distinguishes the MVP scope target (5,
  `MVP_SCOPE.md` §1) from the content-defined count (3, `BOSS_RULES.md` §6),
  and §1 note item 5 states only content-defined rows may ever be provisioned.
  Implement three-row-capable persistence and provision nothing; do not invent
  the missing two Bosses.
- Agent routing: `.ai/agents/persistence.md` scopes the Persistence Agent to
  `src/GameServer.Infrastructure/Postgres/*`. The Domain edits
  (`BossDefinition.cs`, `BossDefinitions.cs`) are part of this same task and
  should be covered by the supporting-agent handoff (gameplay/Domain
  ownership) — do not split them into a second task, and do not widen the
  Persistence Agent's scope.
- Do not modify `tasks/completed/` or other tasks' files.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — configuration maps exactly the documented fields;
                         documented-fields assertion
                         (CardPersistenceTests pattern)
[x] Integration tests  — insert a BossDefinition row and read it back
                         unchanged via TestGameDbContextFactory; migration
                         applies cleanly with the existing migration set
[x] Gameplay scenarios — N/A: no gameplay behavior changes; Boss mechanics
                         are TASK-020 / TASK-022 (BOSS_RULES.md)
```

### Key Edge Cases

- **`threshold: null` ⇔ always-active.** Thủy Ma is the always-active Boss
  (`BOSS_RULES.md` §6.2) and its Domain `PassiveThreshold` is `0` — the
  in-code marker. Persistence must store `"threshold": null` for it and
  **never** `0` (`DATABASE.md` §1 note item 3, §3). The test must cover this
  mapping explicitly.
- **`BossDefinitionId` ≠ `Identity`.** Both are NOT NULL and UNIQUE and both
  are strings, so a wrong mapping would pass a naive round-trip. Assert the
  exact values (`boss-def-hoa-long` vs `boss-hoa-long`) and that neither is
  derived from the other and neither is the display name.
- **`resetBehavior` token set.** Exactly `Default` | `Partial` | `Persistent`
  (`DATABASE.md` §3); no other token exists, and the internal
  `PassiveResetBehavior` enum's `NoReset` must map to the storage token
  `Persistent`.
- Any enum/value-type conversions (e.g. `Element`) follow the established
  sibling convention — never a new encoding.
- Do not add `MaxHP` / `ATK` / `DEF` / `EnrageThreshold` columns: they are
  explicitly not persisted (`DATABASE.md` §1 note item 1).
- Round-trip preserves every field; no field silently defaulted or dropped
- Migration must not alter existing tables, columns, or indexes
  (`DATABASE.md` §4)
- No FK may target `BattleResult` (TASK-041 owns it); no speculative index
  (`AGENTS.md` §9)

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 apply.
The former contract-ambiguity stops (column representation, and the
"5 vs 3 Bosses" reconciliation) are **resolved** by TASK-045/TASK-049 and no
longer fire; they are recorded here only as history. Task-specific stops that
still apply:

- **If a `DATABASE.md` §1 column representation cannot in fact be derived from
  the current Authoritative References: STOP per `AGENTS.md` §7 / §20** —
  report the ambiguity precisely rather than inventing a shape. (At
  re-synchronization: **all five columns are fully determined** —
  `BossDefinitionId` per TASK-049, `Identity`, `Element`, and both JSON member
  lists per TASK-045.)
- **If the task requires deciding which Boss rows exist or how they are
  provisioned (seed / content load / static-content mechanism): STOP &
  report per `AGENTS.md` §7 / §21** — no provisioning mechanism is documented
  (`DATABASE.md` §1 note item 5, §5 item 4); an invented seed system is not an
  acceptable substitute. Content-defined = 3 (`BOSS_RULES.md` §6); do not
  invent the missing two.
- If implementing requires `BattleResult`, reward, or Boss behavior
  changes: out of scope — STOP & report per `AGENTS.md` §16.
- If a new abstraction (repository / registry / service for Boss
  definitions) appears required: STOP per `AGENTS.md` §9 /
  `ARCHITECTURE.md` §5 item 1.
- If the Domain change (`required string BossDefinitionId`) cannot be made
  without altering `BossState` or any gameplay behavior: STOP and report —
  this task adds a persistence identity, not a gameplay change.
- If MVP_SCOPE §1 read as not covering the change: STOP & report per
  `MVP_SCOPE.md` §4.
- If the skill budget (7) is exceeded: STOP and decompose per
  `TASK_TEMPLATE.md`.

---

## Completion Evidence

**Status: DONE** (executed 2026-09-26). `BossDefinition` persistence is
implemented, tested, and verified against `DATABASE.md` §1/§3. No STOP
condition fired. Every column representation came from the resolved contract
(TASK-045, TASK-049); nothing was invented.

### Changed Files

**Domain (2)** — the persistence identity and the two documented JSON groups:

- `src/backend/GameServer.Domain/Bosses/BossDefinition.cs` — added
  `required string BossDefinitionId` (the TASK-049 persistence key) and grouped
  the Passive/Skill configuration into the two documented persisted objects
  (`BossPassiveDefinition`, `BossSkillDefinition`) as constructor state, mapped
  as scalar `jsonb`. `MaxHP`/`ATK`/`DEF`/`EnrageThreshold` moved out of the
  constructor to `init` properties (they are Domain configuration, **not**
  columns — §1 note item 1) with the §6.1 base values as defaults
  (`BossRules`); `PassiveId`, `PassiveThreshold`, `PassiveResetBehavior`,
  `SkillId`, `SkillBaseDamage`, `SkillChargeRequirement`, and
  `SkillCooldownTurns` remain readable as projections so every existing call
  site is unchanged. The stale "No registry, service, or persistence" header
  was corrected; it now states the no-registry/no-repository position and the
  persisted-vs-combat-stat split (`AGENTS.md` §17).
- `src/backend/GameServer.Domain/Bosses/BossDefinitions.cs` — the three
  content-defined Bosses now carry their resolved persistence keys
  (`boss-def-hoa-long` / `boss-def-thuy-ma` / `boss-def-moc-yeu`) alongside
  their unchanged canonical Identities; Thủy Ma's Passive stores the documented
  `null` threshold. Class and per-Boss XML updated.

**Infrastructure (4)** — the mapping, registration, and schema:

- `src/backend/GameServer.Infrastructure/Postgres/Configurations/BossDefinitionConfiguration.cs`
  (**new**) — maps exactly the five documented columns: string PK
  (`BossDefinitionId`, required, no default, never store-generated), `Identity`
  (canonical Identity, NOT NULL + UNIQUE), `Element` (enum → numeric), and both
  JSON objects as `jsonb` NOT NULL via value converters over the documented
  member lists. `MaxHP`/`ATK`/`DEF`/`EnrageThreshold` are explicitly ignored.
  Contains `BossDefinitionJson`, the single definition of the two storage
  documents (camelCase member names per §1 note item 3).
- `src/backend/GameServer.Infrastructure/Postgres/GameDbContext.cs` — added
  `DbSet<BossDefinition> BossDefinitions`; `ApplyConfigurationsFromAssembly`
  unchanged, no second context.
- `src/backend/GameServer.Infrastructure/Postgres/Migrations/20260926124429_AddBossPersistence.cs`
  (**new**) — creates the table with the five documented columns, the PK, and
  the unique Identity index. **Schema only**: no `InsertData`, no seed, no
  provisioned row.
- `.../Migrations/20260926124429_AddBossPersistence.Designer.cs` and
  `.../Migrations/GameDbContextModelSnapshot.cs` — EF-generated model snapshot
  updates accompanying the migration.

**Tests (6)**:

- `tests/backend/GameServer.Infrastructure.Tests/BossPersistenceTests.cs`
  (**new**, 21 tests) — the documented-fields and schema assertions, the PK
  semantics, `Identity` uniqueness and its distinctness from the PK, `Element`
  round-trip across all five Elements, both JSON documents' member lists,
  nullability, the `threshold: null` always-active case (asserting JSON `null`
  and that `0` is never stored), all three reset tokens, round-trips, the
  absence of combat-stat columns, and the absence of seed data.
- `tests/backend/GameServer.Infrastructure.Tests/CardPersistenceTests.cs` and
  `RelicPersistenceTests.cs` — the exhaustive model table-list assertions now
  include `BossDefinition` (the new documented table). No assertion weakened.
- `tests/backend/GameServer.Api.Tests/BossResponseWireTests.cs`,
  `tests/backend/GameServer.Application.Tests/BossResponseTests.cs`, and
  `VictoryDefeatTests.cs` — the 23 Boss scenario overrides updated mechanically
  from the flat members to the persisted groups
  (`with { PassiveDefinition = … with { Threshold = … } }`), preserving each
  scenario's exact value and intent.

**Task file (1)**: this file — Status READY → IN PROGRESS → DONE; moved
`backlog/` → `active/` → `completed/`.

### Validation Results

| Depth | Command | Result |
|---|---|---|
| Build | `dotnet build GameServer.sln` | **succeeded, 0 errors** |
| Full backend | `dotnet test GameServer.sln` | **PASS — 1361 tests, 0 failed, 0 skipped** |
| Frontend | `npx vitest run` | **PASS — 188 tests, 12 files** |
| Migration SQL | `dotnet ef migrations script AddCardPersistence AddBossPersistence` | 5 columns, PK, unique Identity index, **no INSERT into `BossDefinition`** |

Per-project backend totals: Domain 911, Application 210, Api 115, Infrastructure
125 (was 104; +21 `BossPersistenceTests`).

**Generated schema** (verified against `DATABASE.md` §1):

```sql
CREATE TABLE "BossDefinition" (
    "BossDefinitionId" character varying(64) NOT NULL,
    "Identity" character varying(64) NOT NULL,
    "Element" integer NOT NULL,
    "PassiveDefinition" jsonb NOT NULL,
    "SkillDefinition" jsonb NOT NULL,
    CONSTRAINT "PK_BossDefinition" PRIMARY KEY ("BossDefinitionId")
);
CREATE UNIQUE INDEX "IX_BossDefinition_Identity" ON "BossDefinition" ("Identity");
```

### Implementation Notes (design decisions taken)

1. **The Domain record became a `record class` with the two JSON groups as
   constructor state.** `DATABASE.md` §1 fixes the *storage* shape (two JSON
   objects) while §1 note item 3 states "the JSON/storage names are the
   contract; internal representation maps to them". EF Core binds an immutable
   record's state through its constructor and cannot bind an owned navigation
   to a constructor parameter, so the groups are constructor state mapped as
   **scalar `jsonb` via value converters** — no `OwnsOne`, no navigation, no
   interceptor, and no second persistence model. The flat convenience members
   remain readable projections, so production call sites are untouched.
2. **The combat stats were moved out of the constructor.** EF maps any settable
   property by convention, and §1 note item 1 forbids these as columns. Keeping
   them as `init` properties (excluded from the model) preserves both the
   contract and the `with { MaxHP = … }` scenario overrides.
3. **`BossDefinitionJson` is public** so the persistence tests can assert the
   exact stored document without a relational provider; no `InternalsVisibleTo`
   convention exists in the repository and none was invented.

### Scope Verification

- [x] No Boss gameplay, AI, skill execution, or passive execution
- [x] No `BattleResult` changes (TASK-041 owns it; nothing added here)
- [x] No Redis changes and no Boss→Redis runtime persistence
- [x] No SignalR changes; no API changes; no auth changes; no rewards
- [x] No Match-3 or Combat changes
- [x] No production provisioning — no `HasData`, seed, startup loader, or
      migration-inserted Boss row; the missing two Bosses were not invented
- [x] No new repository/registry/service abstraction
- [x] No documentation edited (`DATABASE.md` hash unchanged); no contract
      reopened — TASK-046/047/048/049 decisions preserved

### Reviewer Note (documentation impact — reported, not silently changed)

The Domain grouping described in note 1 above is an **implementation** choice
within the resolved contract, not a contract change: `DATABASE.md` fixes the
five columns, the PK semantics, and the JSON member lists, and states that the
internal representation maps to them. No authoritative document specifies the
Domain record's constructor shape, so nothing contradicted required a
documentation edit. Two contract consequences worth recording for the reviewer:
`BossDefinitionId` is now carried on the Domain record as §1 note item 2
required, and the combat stats are Domain-only defaults rather than persisted
values, which is how §1 note item 1 already described them.

---

## Revision History

| Revision | Date       | Change                                                        |
|----------|------------|---------------------------------------------------------------|
| 1        | (created)  | Created from TASK-041 readiness re-audit: DATABASE.md §1      |
|          |            | documents a BossDefinition table required by the BattleResult |
|          |            | FK, but no entity/config/DbSet/migration exists and no task   |
|          |            | owned it. Assigned sequential ID TASK-044 (next free).        |
| 2        | 2026-09-26 | **Metadata re-synchronization after readiness audit.** Status |
|          |            | BACKLOG → READY. Dependencies now record the resolved         |
|          |            | contract chain (TASK-045/046/047/048/049 DONE). Authoritative |
|          |            | References now cite the §1 persistence-contract note items    |
|          |            | 2–5 and §3/§5. Current State, Implementation Notes, Key Edge  |
|          |            | Cases, Acceptance Criteria, and Stop Conditions updated to    |
|          |            | the now-determined contract (TASK-049 `BossDefinitionId`;     |
|          |            | TASK-045 JSON shapes; the 5-vs-3 question resolved as scope   |
|          |            | vs content-defined count, not a conflict). The former         |
|          |            | column-representation and 5-vs-3 STOP conditions are retained |
|          |            | as history only. Implementation scope unchanged; no contract  |
|          |            | change; no source or test change.                             |
