# TASK-085 — Provision MVP Pet, Card, and Relic Content Definitions

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  This task implements the deterministic EF Core migration-based provisioning
  of the authoritative MVP PetDefinition, CardDefinition, and RelicDefinition
  rows established by TASK-082, TASK-084, and DATABASE.md §1/§5 item 4.

  It provisions ONLY static content definition rows. It does NOT provision Player
  ownership, does NOT create tables/columns, does NOT touch frontend or API,
  and does NOT implement gameplay systems.

  Authority chain:
    TASK-082 decision A/B/C/D  → content-definition scope, key forms, value rules,
                                  and EF Core migration INSERT mechanism
    TASK-084                   → confirmed canonical IDs & starter contract
    DATABASE.md §1 / §5 item 4 → schema contract and provisioning rules
    PET_RULES.md §8            → provisioned (3) vs deferred (2) Pets
    CARD_RULES.md §1/§2/§4.1   → 3 Basic + 3 Pet Skill Cards defined; 2 deferred
    RELIC_RULES.md §6 note 3   → provisioned (4) vs deferred (1) Relics
    PASSIVE_RULES.md §8        → Pet PassiveId & threshold definitions
-->

---

## Metadata

```text
Task ID:           TASK-085
Type:              FEATURE
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH (P1 — the required prerequisite unblocking definition-FK
                   dependencies for TASK-083 and battle start)
Primary Agent:     persistence
Supporting Agents: backend, testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery,
                   backend/persistence-analysis,
                   testing/test-scenario-generation,
                   quality/architecture-conformance,
                   quality/scope-validation
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-082 (DONE — Pet/Card/Relic content provisioning contract),
                   TASK-084 (DONE — MVP starter ownership contract resolved),
                   TASK-024 (DONE — Pet persistence and PetDefinition schema),
                   TASK-028 (DONE — Card persistence, CardDefinition schema, and
                     PetDefinition.SignatureSkillCardId FK),
                   TASK-027 (DONE — Relic persistence and RelicDefinition schema),
                   TASK-053 (DONE — BossDefinition provisioning precedent)
Blocks:            TASK-083 (BACKLOG — Player creation starter ownership initialization;
                     requires these Definition rows for valid foreign keys)
```

---

## Objective

Provision the authoritative MVP `PetDefinition` (3 rows), `CardDefinition` (6 rows: 3 Basic + 3 Pet Skill), and `RelicDefinition` (4 rows) content rows required by the resolved database contract, using the repository's established EF Core migration-INSERT precedent (TASK-052 / TASK-053; `DATABASE.md` §5 item 4).

The task creates exactly one new EF Core migration whose `Up` executes migration-level `InsertData` for the content-defined rows and whose `Down` executes the mirror `DeleteData` calls, applied through `dotnet ef database update`. It introduces no new tables, no new columns, no runtime seeder, and no Player ownership data.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 (Technical — "Persistent storage (PostgreSQL)"), §4 — scope boundaries.
- `docs/02-technical/DATABASE.md` §1 — `PetDefinition`, `CardDefinition`, and `RelicDefinition` entity schemas, key value forms (`pet-{slug}`, `card-{slug}`, `relic-{slug}`), column types, and constraints.
- `docs/02-technical/DATABASE.md` §2 — Ownership model; starter loadout composition and definition references (`pet-xich-lang`, `card-heal`, `card-shield`, `card-power-charge`, `relic-berserker-core`, `relic-mana-crystal`, `relic-assassin-eye`).
- `docs/02-technical/DATABASE.md` §3 — Database constraints: `CardDefinition.Category` ∈ `{Basic, PetSkill}`, required non-null fields, documented enum conversions.
- `docs/02-technical/DATABASE.md` §5 item 4 — Static content provisioning contract: EF Core migration-INSERT via `dotnet ef database update`; only content-defined rows may be provisioned; no `HasData`, seed runners, JSON pipelines, or external content services; exact values transcribed from owning domain docs.
- `docs/01-game-design/PET_RULES.md` §1, §3, §8 — MVP Pets: Xích Lang (Hỏa), Bạch Hổ (Kim), Huyền Quy (Thủy); Thanh Xà and Sơn Hùng explicitly deferred.
- `docs/01-game-design/CARD_RULES.md` §1, §2, §4.1 — Card definitions: `LoadoutCopyLimit = 1` for all defined cards; 3 Basic Cards (Heal, Shield, Power Charge); 3 Pet Skill Cards (Inferno, Tidal Barrier, Iron Fang); Thanh Xà / Sơn Hùng skills deferred.
- `docs/01-game-design/RELIC_RULES.md` §1, §3, §6 (including note 3) — MVP Relics: Berserker Core, Mana Crystal, Assassin Eye, Emergency Core; Burning Curse explicitly deferred.
- `docs/01-game-design/PASSIVE_RULES.md` §8 — Pet `PassiveId` format (`passive-{slug}`) and match thresholds (`passive-xich-lang`: 5, `passive-bach-ho`: 4, `passive-huyen-quy`: 6).
- `docs/02-technical/ARCHITECTURE.md` §2.1, §3 — Infrastructure layer, PostgreSQL persistence repository.
- `docs/03-decisions/ADR/ADR-006-efcore-postgresql-persistence.md` — EF Core PostgreSQL migrations.
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md`, `ADR-012`, `ADR-016` — Definition vs instance ownership separation.

---

## Current State

- Persistence tables `PetDefinition`, `CardDefinition`, and `RelicDefinition` were created by migrations `20260924163011_AddPetPersistence`, `20260925150903_AddCardPersistence`, and `20260925134303_AddRelicPersistence`.
- Foreign key `PetDefinition.SignatureSkillCardId` → `CardDefinition.CardDefinitionId` is mapped and enforced with `OnDelete(DeleteBehavior.Restrict)`.
- `PetDefinition.PassiveId` and `PetDefinition.PassiveThreshold` are properties mapped directly on `PetDefinition` (`PASSIVE_RULES.md` §8; `DATABASE.md` §1); there is NO separate `PassiveDefinition` table or entity in the database.
- All three definition tables are currently empty in PostgreSQL: no data migration has been authored for them yet.
- Precedent: `20260926151112_ProvisionBossDefinitions.cs` (TASK-053) provisions 3 `BossDefinition` rows via migration-level `InsertData` / `DeleteData`.

---

## Definition Provisioning Scope

### In Scope

1. **One new EF Core Migration** in `src/backend/GameServer.Infrastructure/Postgres/Migrations/` provisioning the exact content-defined rows below.
2. **CardDefinition rows (6 rows):**
   - 3 Basic Cards: Heal, Shield, Power Charge.
   - 3 Pet Skill Cards: Inferno, Tidal Barrier, Iron Fang.
3. **PetDefinition rows (3 rows):**
   - 3 content-defined Pets: Xích Lang, Bạch Hổ, Huyền Quy.
   - Each correctly referencing its corresponding `SignatureSkillCardId` and carrying its documented `PassiveId` and `PassiveThreshold`.
4. **RelicDefinition rows (4 rows):**
   - 4 provisionable Relics: Berserker Core, Mana Crystal, Assassin Eye, Emergency Core.
5. **Database update verification:** Migration applies cleanly via `dotnet ef database update` on an isolated clean/test database containing all prior migrations.
6. **Integration / Definition Tests:** Verify that repositories (`IPetRepository`, `ICardRepository`, `IRelicRepository`) and `GameDbContext` can query all provisioned rows by canonical IDs with exact documented properties.

---

## Exact Content

### 1. CardDefinition Rows (Table: `CardDefinition`)

*Note on insertion order:* `CardDefinition` rows must be inserted before `PetDefinition` rows due to the `SignatureSkillCardId` foreign key constraint.

| CardDefinitionId | Name | Category (`int`) | PowerCost (`int`) | LoadoutCopyLimit (`int`) | EffectDefinition (`string`, ≤128 chars) |
|---|---|---|---|---|---|
| `card-heal` | `Heal` | `0` (`Basic`) | `20` | `1` | `Restore the active Pet's HP by 20% of its Max HP` |
| `card-shield` | `Shield` | `0` (`Basic`) | `20` | `1` | `Active Pet gains Shield equal to 20% of its Max HP` |
| `card-power-charge` | `Power Charge` | `0` (`Basic`) | `0` | `1` | `Active Pet gains 25 Power` |
| `card-inferno` | `Inferno` | `1` (`PetSkill`) | `100` | `1` | `Deal high Fire (Hỏa) damage; apply Burn` |
| `card-tidal-barrier` | `Tidal Barrier` | `1` (`PetSkill`) | `80` | `1` | `Heal; Gain Shield` |
| `card-iron-fang` | `Iron Fang` | `1` (`PetSkill`) | `100` | `1` | `High damage; increased Crit chance` |

*Deferred (DO NOT PROVISION):*
- Thanh Xà Signature Skill Card (content not defined in `CARD_RULES.md` §4.1).
- Sơn Hùng Signature Skill Card (content not defined in `CARD_RULES.md` §4.1).

### 2. PetDefinition Rows (Table: `PetDefinition`)

| PetDefinitionId | Identity | Element (`int`) | PassiveId (`string`) | PassiveThreshold (`int`) | SignatureSkillCardId (`string`) |
|---|---|---|---|---|---|
| `pet-xich-lang` | `Xích Lang` | `3` (`Hoa`) | `passive-xich-lang` | `5` | `card-inferno` |
| `pet-bach-ho` | `Bạch Hổ` | `4` (`Kim`) | `passive-bach-ho` | `4` | `card-iron-fang` |
| `pet-huyen-quy` | `Huyền Quy` | `2` (`Thuy`) | `passive-huyen-quy` | `6` | `card-tidal-barrier` |

*Deferred (DO NOT PROVISION):*
- `Thanh Xà` (deferred by `PET_RULES.md` §8; SignatureSkillCardId target does not exist).
- `Sơn Hùng` (deferred by `PET_RULES.md` §8; SignatureSkillCardId target does not exist).

### 3. RelicDefinition Rows (Table: `RelicDefinition`)

| RelicDefinitionId | Name | Trigger (`string`) | Condition (`string?`) | EffectDefinition (`string`, ≤128 chars) |
|---|---|---|---|---|
| `relic-berserker-core` | `Berserker Core` | `OnMatchCount` | `every 3 Matches` | `+5% ATK` |
| `relic-mana-crystal` | `Mana Crystal` | `OnMatchCount` | `every 4 Matches` | `+10 Power` |
| `relic-assassin-eye` | `Assassin Eye` | `OnCombo` | `Combo ≥ 3` | `Increased Crit chance` |
| `relic-emergency-core` | `Emergency Core` | `OnHpBelow` | `HP < 30%` | `Heal Card cost −50%` |

*Deferred (DO NOT PROVISION):*
- `Burning Curse` (deferred by `RELIC_RULES.md` §6 note 3; static modifier trigger tension).

---

## Provisioning Mechanism

- **EF Core Migration `InsertData` / `DeleteData`:** Migration executes `migrationBuilder.InsertData(...)` in `Up()` and `migrationBuilder.DeleteData(...)` in `Down()`.
- **No Runtime Seeders or Startup Hooks:** Forbidden by `DATABASE.md` §5 item 4. No `HasData` in `EntityTypeConfiguration`, no `IHostedService` seeder, no JSON loader pipeline, no API-based provisioning.
- **Deterministic Application:** The migration is deterministic and applied exactly once per database through standard EF Core migration history. Primary keys (`PetDefinitionId`, `CardDefinitionId`, `RelicDefinitionId`) guarantee database-level uniqueness.
- **No Runtime Idempotency Logic:** No `IF NOT EXISTS`, `UPSERT`, `ON CONFLICT`, or startup seed checks are introduced in migration or application code.

---

## Database Integrity Requirements

1. **Foreign Key Integrity:** `CardDefinition` rows must be inserted prior to `PetDefinition` rows so `SignatureSkillCardId` foreign keys resolve immediately.
2. **Column Constraints Satisfied:** All non-null columns (`Name`, `Category`, `PowerCost`, `LoadoutCopyLimit`, `EffectDefinition`, `Identity`, `Element`, `PassiveId`, `PassiveThreshold`, `SignatureSkillCardId`, `Trigger`) are populated with valid values.
3. **String Length Limits:** `PetDefinitionId` (≤64), `Identity` (≤64), `PassiveId` (≤64), `SignatureSkillCardId` (≤64), `CardDefinitionId` (≤64), `Name` (≤64), `EffectDefinition` (≤128), `RelicDefinitionId` (≤64), `Trigger` (≤64), `Condition` (≤128).
4. **Enum Encoding:** `Element` encoded as `int` (`Moc = 0`, `Tho = 1`, `Thuy = 2`, `Hoa = 3`, `Kim = 4`); `CardCategory` encoded as `int` (`Basic = 0`, `PetSkill = 1`).
5. **No Schema Changes:** No `CreateTable`, `AddColumn`, `DropColumn`, `AlterColumn`, or `CreateIndex` operations.

---

## Out of Scope

- **Player Ownership Creation:** Creating `Player`, `Pet`, `PlayerUnlockedCard`, or `Relic` instance rows (owned by TASK-083).
- **Deferred Content:** Authoring or provisioning Thanh Xà, Sơn Hùng, or Burning Curse rows.
- **Gameplay / Battle Logic:** Match-3 mechanics, damage calculations, relic triggers, card casting resolution, passive progression evaluation.
- **API Endpoints:** Any new REST endpoint or contract change.
- **Frontend Changes:** Any file under `src/frontend/`.
- **Schema Modification:** Adding tables, columns, or indexes to PostgreSQL.
- **Modifying Protected Tasks:** Modifying `TASK-082`, `TASK-083`, `TASK-084`, or completed tasks.

---

## Acceptance Criteria

- [x] All currently provisionable PetDefinitions (`pet-xich-lang`, `pet-bach-ho`, `pet-huyen-quy`) are inserted via EF Core migration.
- [x] Deferred PetDefinitions (`pet-thanh-xa`, `pet-son-hung`) are NOT inserted.
- [x] All currently provisionable CardDefinitions (3 Basic: `card-heal`, `card-shield`, `card-power-charge`; 3 PetSkill: `card-inferno`, `card-tidal-barrier`, `card-iron-fang`) are inserted via EF Core migration.
- [x] Deferred CardDefinitions (Thanh Xà / Sơn Hùng signature skills) are NOT inserted.
- [x] All currently provisionable RelicDefinitions (`relic-berserker-core`, `relic-mana-crystal`, `relic-assassin-eye`, `relic-emergency-core`) are inserted via EF Core migration.
- [x] `Burning Curse` is NOT inserted.
- [x] No PassiveDefinition table or PassiveDefinition rows are created.
- [x] PetDefinition.PassiveId and PetDefinition.PassiveThreshold are populated directly from PASSIVE_RULES.md §8.
- [x] Canonical Definition IDs exactly match authoritative documentation (`DATABASE.md` §1/§2).
- [x] All required Definition fields use documented values (`Element`, `Category`, `PowerCost`, `LoadoutCopyLimit`, `EffectDefinition`, `Trigger`, `Condition`).
- [x] No values are invented.
- [x] Provisioning uses the documented EF Core migration mechanism (`migrationBuilder.InsertData`).
- [x] Migration is deterministic and applied exactly once per database through standard EF Core migration history.
- [x] No Player ownership rows (`Player`, `Pet`, `PlayerUnlockedCard`, `Relic`) are created.
- [x] No new database tables are created.
- [x] No new schema columns are created.
- [x] No API contract changes are introduced.
- [x] No frontend changes are introduced.
- [x] No gameplay logic is introduced.
- [x] Clean-database migration succeeds (`dotnet ef database update` on an isolated clean/test database).
- [x] Existing backend tests remain green (`dotnet test`).
- [x] Definition-specific tests verify the provisioned rows and canonical IDs.
- [x] The missing Definition-row prerequisite for TASK-083 is removed.
- [x] TASK-083 remains unchanged and requires a separate readiness audit before entering READY.

---

## Affected Files & Areas

```text
[x] src/backend/GameServer.Infrastructure/Postgres/Migrations/
    ├── <Timestamp>_ProvisionPetCardRelicDefinitions.cs          — new EF Core data migration
    ├── <Timestamp>_ProvisionPetCardRelicDefinitions.Designer.cs — migration metadata
    └── GameDbContextModelSnapshot.cs                           — modify only if EF Core actually requires a snapshot change for the generated migration
[x] tests/GameServer.Infrastructure.Tests/                       — integration/definition verification tests
[x] tasks/backlog/TASK-085-provision-mvp-pet-card-relic-content-definitions.md (this manifest)
[ ] tasks/backlog/TASK-083-*.md                                  — NO CHANGES (remains BACKLOG)
[ ] tasks/completed/TASK-082-*.md                                — NO CHANGES
[ ] tasks/completed/TASK-084-*.md                                — NO CHANGES
[ ] src/frontend/                                                — NO CHANGES
```

---

## Implementation Notes

- **Precedence Hierarchy for Values:**
  ```text
  Domain Rules
      ↓
  DATABASE.md
      ↓
  TASK-085
  ```
- **Authoritative-Value Verification:** Before inserting any value, the implementation agent MUST verify the value against the authoritative owning domain documentation (`PET_RULES.md`, `CARD_RULES.md`, `RELIC_RULES.md`, `PASSIVE_RULES.md`, `DATABASE.md`). The task itself is NOT the authority for gameplay/content values. Specifically verify:
  - `CardDefinition.PowerCost`
  - `CardDefinition.EffectDefinition`
  - `PetDefinition.Element`
  - `PetDefinition.PassiveThreshold`
  - `RelicDefinition.Trigger`
  - `RelicDefinition.Condition`
  - `RelicDefinition.EffectDefinition`
- Use the established migration naming convention: `ProvisionPetCardRelicDefinitions` (or similar descriptive timestamped migration name).
- Mirror the structure of `20260926151112_ProvisionBossDefinitions.cs`.
- Ensure `Down(MigrationBuilder)` cleans up in the correct reverse dependency order: delete `PetDefinition` rows first, then `CardDefinition` and `RelicDefinition` rows.
- Ensure the migration is data-only: no `CreateTable`, `AddColumn`, `DropColumn`, `AlterColumn`, or `CreateIndex` operations.
- Use exact string constants matching `DATABASE.md` §1/§2, `PET_RULES.md` §8, `CARD_RULES.md` §2/§4.1, `RELIC_RULES.md` §6, and `PASSIVE_RULES.md` §8 verbatim.

---

## Testing Requirements

### Required Verification

```text
[ ] Migration applies cleanly: `dotnet ef database update` succeeds on an isolated
    clean/test database containing all prior migrations.
[ ] Migration rollback is verified on an isolated test database by reverting the
    TASK-085 migration and confirming all 13 provisioned definition rows are removed
    without FK violations.
[ ] PetDefinition queries: `IPetRepository.GetDefinitionAsync("pet-xich-lang")`, `GetDefinitionAsync("pet-bach-ho")`, `GetDefinitionAsync("pet-huyen-quy")` return correct non-null definitions with exact documented properties.
[ ] CardDefinition queries: `ICardRepository.GetDefinitionAsync` returns all 6 provisioned cards (3 Basic, 3 PetSkill) with documented Category, PowerCost, LoadoutCopyLimit, and EffectDefinition.
[ ] RelicDefinition queries: `IRelicRepository.GetDefinitionAsync` returns all 4 provisioned relics with documented Trigger, Condition, and EffectDefinition.
[ ] Deferred checks: Queries for "pet-thanh-xa", "pet-son-hung", "relic-burning-curse" return null.
[ ] FK validation: Every PetDefinition's `SignatureSkillCardId` resolves to an existing `CardDefinition` row in the database.
[ ] Regression: All existing backend tests pass (`dotnet test`).
```

---

## Stop Conditions

Universal stop conditions (`AGENTS.md` §20) always apply. Task-specific:

- **If TASK-085 conflicts with an authoritative Domain Rule or DATABASE.md: STOP** and report the conflict. Do not silently modify the task's values or invent replacement values.
- **If authoritative documents disagree on which definitions are provisionable: STOP** and report conflict.
- **If required Definition field values are ambiguous or missing: STOP** — do not invent values.
- **If canonical IDs conflict across authoritative sources: STOP.**
- **If provisioning requires a schema change (table, column, index): STOP.**
- **If the migration mechanism selected by TASK-082 is no longer compatible with the schema: STOP.**
- **If satisfying any criterion requires modifying TASK-082, TASK-083, TASK-084, or a completed task: STOP.**

---

## Dependency on TASK-083

```text
TASK-085 (this task)                     TASK-083
────────────────────────────────────     ─────────────────────────────────────
Provisions static definition rows:        Initializes Player starter ownership:
  • 3 PetDefinitions                       • 1 Pet instance (FK → pet-xich-lang)
  • 6 CardDefinitions (3 Basic + 3 Skill)  • 3 PlayerUnlockedCards (FK → card-*)
  • 4 RelicDefinitions                     • 3 Relic instances (FK → relic-*)
```

**TASK-083 depends on this Definition provisioning task.**

TASK-085 removes only the **missing Definition-row prerequisite** for TASK-083. It does NOT automatically make TASK-083 READY.

**TASK-083 must remain BACKLOG / not READY until:**
1. Definition rows exist in the database (completed by TASK-085).
2. TASK-083 readiness is re-audited against the provisioned schema and contracts.
3. TASK-083 implementation prerequisites (combined commit-scope surface & concurrency batch-discard) are confirmed.

**The intended lifecycle is:**
```text
TASK-085 DONE
      ↓
Definition-row prerequisite removed
      ↓
TASK-083 readiness audit
      ↓
READY only if all TASK-083 prerequisites pass
```

**Do NOT modify TASK-083 in this task.**
**Do NOT change TASK-083 lifecycle state.**
**Do NOT mark TASK-083 READY.**

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Provisioned Rows Summary
- `PetDefinition`: 3 rows (`pet-xich-lang`, `pet-bach-ho`, `pet-huyen-quy`)
- `CardDefinition`: 6 rows (`card-heal`, `card-shield`, `card-power-charge`, `card-inferno`, `card-tidal-barrier`, `card-iron-fang`)
- `RelicDefinition`: 4 rows (`relic-berserker-core`, `relic-mana-crystal`, `relic-assassin-eye`, `relic-emergency-core`)
- Total: 13 content-definition rows.

### Changed Files
- `src/backend/GameServer.Infrastructure/Postgres/Migrations/20260929152651_ProvisionPetCardRelicContentDefinitions.cs` — new data-only migration: 13 `InsertData` in `Up` (Cards → Pets → Relics, FK-safe), 13 mirror `DeleteData` in `Down` (Pets → Cards → Relics).
- `src/backend/GameServer.Infrastructure/Postgres/Migrations/20260929152651_ProvisionPetCardRelicContentDefinitions.Designer.cs` — migration metadata; target model identical to the previous migration's model.
- `tests/backend/GameServer.Infrastructure.Tests/PetCardRelicDefinitionProvisioningTests.cs` — new provider-independent tests over the migration source (row set, canonical IDs, transcribed values, FK-safe order, `Down` mirror, no schema operation, no `HasData`, no deferred content).
- `tests/backend/GameServer.Infrastructure.Tests/PetCardRelicDefinitionPostgresProvisioningTests.cs` — new applied-database tests against real PostgreSQL (row counts, per-row documented values, deferred-content absence, FK resolution, repository lookups, no `PassiveDefinition` table).
- `tasks/backlog/TASK-085-provision-mvp-pet-card-relic-content-definitions.md` → moved to `tasks/completed/` — status + completion evidence only.

`GameDbContextModelSnapshot.cs` was **not** modified: EF generated no schema difference, confirming the migration is pure data.

### Validation Results
- Migration execution (`dotnet ef database update`) — PASS. Verified twice: (a) applied to the existing local PostgreSQL containing all 10 prior migrations; (b) applied from scratch to a **dedicated clean database** (`task085_verify`), where all 11 migrations applied in order and the provisioning migration produced exactly 6 CardDefinition / 3 PetDefinition / 4 RelicDefinition rows — whole-table counts, not a filtered view.
- Migration rollback verification — PASS. On the clean database, `dotnet ef database update 20260927144250_AddPetXp` reverted only this migration (`Reverting migration '20260929152651_ProvisionPetCardRelicContentDefinitions'`), leaving all three definition tables at **0 rows** with no FK violations, while the TASK-053 `BossDefinition` rows (3) were preserved. On the shared dev database the same rollback removed only the 13 rows and preserved unrelated pre-existing rows.
- Applied row verification — PASS (exact documented values per row confirmed by SQL: card names/categories/PowerCosts/LoadoutCopyLimit/EffectDefinition; pet Identity/Element/PassiveId/PassiveThreshold/SignatureSkillCardId; relic Name/Trigger/Condition/EffectDefinition including the `≥` and `−` characters)
- Definition lookup tests — PASS (`IPetRepository`, `ICardRepository`, `IRelicRepository` resolve all 13 canonical IDs; the deferred IDs return null)
- Deferred content checks — PASS (`pet-thanh-xa`, `pet-son-hung`, both TBD Signature Skill Cards, and `relic-burning-curse` absent; no `PassiveDefinition` table exists)
- FK validation — PASS (0 dangling `PetDefinition.SignatureSkillCardId` references on the clean database; every reference resolves to a provisioned `Category = PetSkill` row)
- Test suite execution (`dotnet test`) — PASS. 1800 tests: Domain 951, Application 350, Infrastructure 257, Api 242. Baseline before this task was 1760, so **+40 new tests**, 0 failures. The full suite was run **three consecutive times** with identical green results to confirm stability.
- Scope of applied-DB assertions — the new PostgreSQL-backed tests resolve rows by the closed set of thirteen canonical definition keys rather than by a `card-`/`pet-`/`relic-` name prefix (or a display name), because the shared dev database is also written by the API integration suites' smoke fixtures (e.g. `smoke_result_pet_def*`). This keeps the assertions about *this migration's* rows rather than about test-run ordering.
- TASK-083 definition-row prerequisite verification — PASS (TASK-085 provisioned the Definition rows required by TASK-083; TASK-083 remains BACKLOG until its own readiness audit passes)

### Notes
- `dotnet build -warnaserror` reports pre-existing repository conditions unrelated to this task (verified identical with this task's files stashed): `NU1900` NuGet-audit timeouts under no network, `MSB3277` `Microsoft.EntityFrameworkCore.Relational` 10.0.4-vs-10.0.12 conflict (Npgsql 10.0.3 declares a `[10.0.4, 11.0.0)` floor while the Infrastructure project pins EF Core 10.0.12), and `CS0105` duplicate using in `DependencyInjection.cs:6`. `dotnet test` builds and runs all four suites cleanly.
- The rename from the initially scaffolded `ProvisionPetCardRelicDefinitions` to `ProvisionPetCardRelicContentDefinitions` aligned `__EFMigrationsHistory` with the assembly's migration id; the applied row set is unaffected (verified: no duplicated rows, rollback still removes exactly 13).

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed no Player ownership rows created
- [x] Confirmed no schema alterations (tables/columns)
- [x] Confirmed TASK-082, TASK-083, TASK-084, and completed tasks unmodified
