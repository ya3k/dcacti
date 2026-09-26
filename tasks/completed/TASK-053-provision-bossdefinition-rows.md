# TASK-053 — Provision BossDefinition Rows

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section; does not copy rules or schemas.
-->

---

## Metadata

```text
Task ID:           TASK-053
Type:              FEATURE
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH (the sole implementation link between the TASK-052
                   contract and TASK-041's FK prerequisite)
Primary Agent:     persistence
Supporting Agents: testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, backend/persistence-analysis,
                   testing/test-scenario-generation, quality/architecture-conformance
Dependencies:      TASK-052 (DONE — provisioning contract P1–P8 resolved);
                   TASK-045, TASK-049, TASK-051 (DONE — persistence/identity
                   contract chain). No open blocker.
```

---

## 1. Objective

Provision the three canonical `BossDefinition` rows required by the
resolved database contract, using exactly the mechanism TASK-052
decided: **one new EF Core migration whose `Up` performs migration-level
`InsertData` for the three content-defined rows, applied through the
repository's normal `dotnet ef database update` workflow** — nothing
else. No schema change, no runtime code, no alternative seeding path.
After completion the FK target of `BattleResult.BossDefinitionId`
(`DATABASE.md` §1, §2) exists in every battle-capable environment, so a
later TASK-041 readiness re-audit can find this dependency satisfied.

---

## 2. Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 (Technical — "Persistent storage
  (PostgreSQL)"), §4 — scope rule
- `docs/02-technical/DATABASE.md` §1 `BossDefinition` persistence
  contract, **note item 5** — the resolved provisioning contract
  (Mechanism / Row set / Row content / Idempotency and uniqueness /
  Availability guarantee / Missing provisioning fails closed)
- `docs/02-technical/DATABASE.md` §5 **item 4** — `BossDefinition`
  mechanism defined; migration script = implementation detail (item 1);
  other content tables remain open
- `docs/02-technical/DATABASE.md` §1 note items 1–4 (column set, key
  sourcing, exact JSON member lists, JSON/storage names are the
  contract), §3 (PK / `Identity` NOT NULL+UNIQUE, JSON NOT NULL,
  threshold-null ⇔ always-active), §4 (no documented index beyond
  `IX_BossDefinition_Identity`)
- `tasks/completed/TASK-052-bossdefinition-provisioning-contract.md`
  §5, §9, §13 — decisions P1–P8, implementation notes for this task
- `docs/01-game-design/BOSS_RULES.md` §6, §6.1–§6.4 — the three
  content-defined Bosses and their canonical Identity / PassiveId /
  SkillId / display-name values (owner of all row content)
- `docs/01-game-design/PASSIVE_RULES.md` §4 — reset behavior (default
  unless a rule documents an override)
- `docs/02-technical/ARCHITECTURE.md` §2 (Infrastructure owns
  persistence), §5 (anti-overengineering)
- `docs/03-decisions/ADR/ADR-006-postgresql-persistence.md` — the
  existing strategy this task stays inside (no new ADR)
- `AGENTS.md` §7, §9, §16, §17, §18, §20

---

## 3. Resolved Contract (do not redesign)

TASK-052 resolved the full provisioning contract; this task **implements
it verbatim** and may not reinterpret any part of it:

```text
Mechanism        One new EF Core migration; migration-level InsertData;
                 applied via the existing `dotnet ef database update`
                 workflow in every battle-capable environment before that
                 environment's first BattleResult write
                 (DATABASE.md §1 note item 5, §5 item 4).
FORBIDDEN        HasData / model seed data
                 startup loader or upsert
                 separate manual-SQL deployment path
                 runtime provisioning (service, repository, background job)
                 new provisioning architecture component of any kind
Idempotency      EF migration lifecycle only — the migration runs once
                 when applied; migration history prevents re-execution.
                 No `INSERT ... ON CONFLICT`, no runtime re-check,
                 no second mechanism invented to "achieve" idempotency.
Row content      Transcribed from the authoritative sources below; never
                 computed, never invented, never a placeholder row.
Failure mode     Absent rows remain governed solely by the fail-closed
                 rule (DATABASE.md §1 "Identity and reward sourcing"
                 item 3). This task does NOT implement that behavior —
                 it belongs to TASK-041.
```

---

## 4. Exact Three Rows to Provision

Exactly these three rows — no fourth row, no placeholder, no future Boss:

| # | `BossDefinitionId` (PK) | `Identity` (UNIQUE) |
|---|---|---|
| 1 | `boss-def-hoa-long` | `boss-hoa-long` |
| 2 | `boss-def-thuy-ma` | `boss-thuy-ma` |
| 3 | `boss-def-moc-yeu` | `boss-moc-yeu` |

**Row content sourcing (per column, `DATABASE.md` §1 note item 5 "Row
content"):**

- `BossDefinitionId`, `Identity` — the table above (already canonical in
  `DATABASE.md` §1 note item 2; Identities per `BOSS_RULES.md` §6.4).
- `Element` — `BOSS_RULES.md` §6 per Boss; stored as the Domain `Element`
  enum's integer representation (`BossDefinitionConfiguration`'s
  `HasConversion<int>()`) — derive the inserted integer from the enum,
  never hand-guess it.
- `PassiveDefinition` (jsonb) — members exactly `passiveId`,
  `threshold`, `resetBehavior`; values from `BOSS_RULES.md`
  §6.2/§6.4; `resetBehavior` = `Default` (no Boss documents an
  override); `threshold` = `null` for Thủy Ma's always-active Passive
  (never a `0` sentinel — `DATABASE.md` §3, §1 note item 3).
- `SkillDefinition` (jsonb) — members exactly `skillId`, `baseDamage`,
  `chargeRequirement`, `cooldownTurns`; values from `BOSS_RULES.md`
  §6.3/§6.4.
- Combat stats (`MaxHP`/`ATK`/`DEF`/`EnrageThreshold`) and display
  names are **not columns** (`DATABASE.md` §1 note item 1) and are
  never written.

**Three-way identity contract — must be preserved and verified:**

```text
BossDefinitionId  ≠  Identity  ≠  DisplayName
(persistence PK)    (technical     (presentation-only,
                     BossId,       BOSS_RULES.md §6/§6.4,
                     §6.4)         never a column — never written)
```

The migration writes only the first two, values as given; nothing may
derive one from another, and no display-name string may appear in any
persisted column.

---

## 5. Current State

- The `BossDefinition` model is complete and stable (TASK-044/045/049):
  five columns, PK + unique Identity index, no seed data of any kind.
- **No rows exist anywhere**, and no provisioning mechanism exists:
  `20260926124429_AddBossPersistence.cs` is schema-only, and
  `BossPersistenceTests` pins that (no `InsertData`/`HasData`/boss IDs
  in it) plus `BossDefinition_ShouldNeverBeProvisionedByTheModel`
  (fresh context empty, `GetSeedData()` empty).
- Tests run on the InMemory provider plus design-time model inspection;
  migration contracts are asserted against **migration source files**
  (string assertions). An opt-in live-PostgreSQL precedent exists:
  `PlayerPostgresConstraintTests` (hard-coded local connection string,
  skipped when unreachable).
- `BossDefinitions.cs` (Domain) already holds all three definitions
  verbatim; `BossDefinitionJson` is public and writes exactly the
  documented JSON documents.

---

## 6. Scope

### In Scope

1. One new EF migration (`InsertData` × 3 in `Up`, matching `DeleteData`
   in `Down`) generated from the **current, unchanged** model.
2. Tests proving the provisioning per §8, following existing conventions.
3. Updating now-stale test comments only (assertions untouched) —
   `BossPersistenceTests.cs` lines ~277 and ~658–660 claim "no
   provisioning mechanism is documented" (TASK-052 §13 issue 1).
4. Syncing the two present-tense "implementation remains / follow-up
   task" phrases in `DATABASE.md` §1 note item 5 and §5 item 4 to record
   that implementation landed (version bump; content otherwise
   untouched — no contract redesign).

### Out of Scope — this task MUST NOT

- implement `BattleResult` persistence, its lookup, its Redis-state
  delete, or `GET /api/battle/{battleId}/result` (TASK-041)
- modify `tasks/backlog/TASK-041-*` or move it to READY (it stays
  untouched and BACKLOG; a later readiness re-audit is a separate step)
- modify TASK-034 / authentication / SignalR / `BattleState` (and never
  add `BossDefinitionId` to `BattleState`)
- change gameplay, Boss stats, or any `docs/01-game-design/` rule
- add columns, constraints, indexes, or any other schema change
- edit any historical migration (incl. `AddBossPersistence`)
- add `HasData`, model seed, startup loader, `Database.Migrate()` in
  runtime code, `ON CONFLICT` SQL, a provisioning service/repository, or
  any new architecture component
- environment-specific row values (one contract, same rows everywhere)
- any item OUT in `MVP_SCOPE.md` §2

---

## 7. Migration Requirements

1. **Generate from the current model:** `dotnet ef migrations add`
   (repository's existing workflow) → new migration, e.g.
   `<timestamp>_ProvisionBossDefinitions` + `.Designer.cs`. Because the
   model is intentionally unchanged (adding `HasData` to force a diff is
   forbidden), the scaffolded migration is expected to be **empty** —
   then hand-add `InsertData` to `Up()` and the mirror `DeleteData` to
   `Down()`. Confirm with `dotnet ef migrations has-pending-model-changes`
   (expect: none) or by the empty schema scaffold.
2. **InsertData only:** exactly three rows, all five columns each,
   literal values derived per §4 — JSON strings produced from
   `BossDefinitionJson.WritePassive/WriteSkill(BossDefinitions.*)` so the
   migration and the converters cannot drift; `Element` from the enum's
   integer value.
3. **No schema operations:** the migration must contain no
   `CreateTable`/`AlterColumn`/`AddColumn`/`CreateIndex`/`Drop*` of any
   kind (source-asserted). PK, unique Identity index, and all NOT NULL
   constraints are preserved untouched; no new columns.
4. **No historical edits:** every existing file under
   `Postgres/Migrations/` stays byte-identical — in particular
   `20260926124429_AddBossPersistence.cs` remains schema-only (it is
   pinned by an existing test).
5. **Apply through the normal workflow:** `dotnet ef database update`
   against the local PostgreSQL (precedent: TASK-023/027/028), then
   verify. Script check (precedent: TASK-044):
   `dotnet ef migrations script AddBossPersistence <NewMigration>` must
   render only the three `INSERT`s (no DDL).
6. **Availability:** because this is a normal migration, it provisions
   every battle-capable environment through each environment's ordinary
   migration application before that environment's first battle-end
   write (`DATABASE.md` §1 note item 5, "Availability guarantee"). No
   separate deployment step may be introduced; record which environments
   were actually applied in the final report.
7. **Idempotency:** rely solely on EF migration history (runs once per
   database). Do not add conflict-handling, re-run guards, or a second
   mechanism.
8. **No runtime code:** zero changes under `src/` outside the
   `Migrations/` folder.

---

## 8. Test Requirements

Follow existing conventions (migration-source string assertions; opt-in
PostgreSQL patterned on `PlayerPostgresConstraintTests`); **no new
testing framework, no weakened assertions.**

```text
[ ] Migration source — new migration's Up contains InsertData for
    exactly the three canonical BossDefinitionId values; contains NO
    fourth boss-def- literal; contains no schema operation; Down
    contains the mirror DeleteData.
[ ] Existing guards stay green with ZERO assertion edits:
    BossDefinition_ShouldNeverBeProvisionedByTheModel (fresh context
    empty + GetSeedData() empty — a migration INSERT is not model seed
    data), Migration_ShouldCreateTheFiveDocumentedColumnsAsNotNullJsonb
    (AddBossPersistence still schema-only),
    Model_ShouldStoreExactlyTheFiveDocumentedColumns,
    BossDefinition_ShouldExposeNoDisplayNameColumn.
    If the migration makes any of these fail, that is a REGRESSION:
    fix the implementation, never the guard.
[ ] Applied-row verification (minimum):
      exactly 3 rows after the migration is applied
      BossDefinitionId set == { the three canonical values } (via
        Domain BossDefinitions.All)
      Identity set == { the three canonical BossIds }
      no duplicate canonical rows (count == 1 per id and per Identity)
      Element / PassiveDefinition / SkillDefinition round-trip equal to
        the Domain definitions (proves the inserted JSON satisfies the
        converters and DATABASE.md §1 note items 3–4)
      DisplayName: verified as the three-way contract — no display-name
        column exists (five documented columns only) and stored
        Identity values are the canonical technical IDs, never display
        names (display names remain BOSS_RULES.md §6/§6.4
        presentation content and are never written).
      Attempt this against live PostgreSQL following the existing
      PlayerPostgresConstraintTests skip-when-unreachable convention
      (and/or after `dotnet ef database update`).
[ ] Full test project passes: dotnet test tests/backend/GameServer.Infrastructure.Tests
[ ] Comment-only updates for the stale comments listed in §6 item 3 —
    assertions themselves are never edited to accommodate the migration.
```

**Documented limitation escape (if needed):** if the existing harness
makes the applied-row verification genuinely impractical, do **not**
invent infrastructure — document the exact limitation (what cannot be
asserted hermetically and why) in the test file and the completion
report, and cover everything reachable via migration-source assertions
plus the `migrations script` check.

---

## 9. TASK-041 Dependency

```text
TASK-052 contract resolved                    (DONE)
        ↓
BossDefinition provisioning implementation    (this task — TASK-053)
        ↓
TASK-041 readiness re-audit                   (separate step; TASK-051 §17)
        ↓
TASK-041 implementation                       (untouched, Status: BACKLOG)
```

This task changes nothing about TASK-041 itself: its file is read-only
here, it is not moved to READY, and its second dependency (TASK-034,
per TASK-051 decision B1) is untouched. Only the readiness re-audit —
performed after this task is DONE — may judge whether TASK-041's
provisioning dependency is now satisfied.

---

## 10. Acceptance Criteria

- [x] New migration exists, generated from the current model; `Up`
      holds exactly three `InsertData` rows (all five columns), `Down`
      the mirror `DeleteData`; no schema operations (source-asserted).
- [x] Inserted values match §4: correct `BossDefinitionId`, correct
      `Identity`, correct `Element`/JSON content round-tripping to the
      Domain definitions; no display-name value written anywhere.
- [x] Exactly three rows exist after application; no duplicate canonical
      rows; PK and `IX_BossDefinition_Identity` uniqueness preserved.
- [x] All historical migrations byte-identical; `AddBossPersistence`
      still schema-only (existing assertions unmodified and green).
- [x] `dotnet ef database update` applied via the normal workflow;
      `migrations script` check shows INSERTs only; environments applied
      recorded in the report.
- [x] The model-seed guard and every other existing test pass with zero
      assertion edits; stale comments updated (§6 item 3).
- [x] No `HasData`, seed, loader, runtime provisioning, service,
      repository, or architecture component added; no `src/` change
      outside `Migrations/`.
- [x] No out-of-scope item (§6) touched; `MVP_SCOPE.md` §1 adhered to.
- [x] `DATABASE.md` present-tense implementation-status phrases synced
      (version bump; no contract content changed).
- [x] Final report delivered per §12.

---

## 11. Affected Files & Areas

```text
[ ] src/backend/GameServer.Infrastructure/Postgres/Migrations/
      <ts>_ProvisionBossDefinitions.cs (+ .Designer.cs)   NEW (generated + InsertData)
[ ] tests/backend/GameServer.Infrastructure.Tests/BossPersistenceTests.cs
      comment-only updates; new source-assertion tests
[ ] tests/backend/GameServer.Infrastructure.Tests/
      optional new opt-in PostgreSQL provisioning test (existing pattern)
[ ] docs/02-technical/DATABASE.md   status-phrase sync + version bump ONLY
[ ] src/ anything else              FORBIDDEN
[ ] tasks/backlog/TASK-041-*        FORBIDDEN (read-only)
```

---

## 12. Implementation Notes

- Migration name suggestion: `ProvisionBossDefinitions` ("seed" is a
  forbidden mechanism word — do not use it in names/comments as if it
  were the mechanism).
- `BossDefinitionJson` is public precisely so tests and the migration
  can produce/verify the exact stored documents.
- A design-time failure of `dotef ef`/`dotnet ef` that cannot be
  resolved without changing runtime code or configuration → STOP (§13).
- Report-only carryovers already listed in TASK-052 §13 (stale test
  comments) are in scope here as comment fixes; the duplicate "item 3"
  numbering in `DATABASE.md` §1 and the `"black-hoa-long"` literal in
  the test's forbidden-strings list are NOT touched (pre-existing,
  out of scope).

---

## 13. Stop Conditions

Universal: `AGENTS.md` §20 (rule conflict, missing rule, architecture
conflict, scope violation, ambiguity, contract conflict, destructive
change) — on fire: Status → BLOCKED, file moves `active/` → `blocked/`,
write the STOP report, do not implement.

Task-specific — STOP instead of guessing if:

- exact canonical row content is ambiguous (sources disagree)
- `BossDefinitionId` / `Identity` / `DisplayName` values conflict
- the migration would require changing the documented schema (non-empty
  model diff)
- an existing constraint prevents the three rows from being inserted
- the existing model-seed guard contradicts TASK-052 (fixing it would
  require weakening an assertion)
- migration generation would require modifying a historical migration
- a new architecture decision (ADR) would be required
- provisioning would require runtime code, or `dotnet ef migrations
  add` cannot run without a runtime/config change
- any TASK-052 contract is found inconsistent with current docs
- the task would exceed 7 skills or require decomposing across
  uncoupled boundaries

---

## 14. Final Report Requirements

Report per `AGENTS.md` §21, including at minimum:

1. **Status** (DONE / BLOCKED with STOP report)
2. **Migration** — name, generation method, confirmation the model
   diff was empty, `Up`/`Down` operation summary
3. **Application** — `dotnet ef database update` result; script-check
   result; which battle-capable environments were applied
4. **Row verification** — counts, id/Identity sets, round-trip results,
   or the documented limitation if live verification was unavailable
5. **Tests** — commands run, pass counts; explicit confirmation the
   model-seed guard and schema-pin tests are unmodified and green
6. **Scope attestation** — confirmation that none of the §6 exclusions
   was touched; `src/` diff limited to `Migrations/`
7. **Documentation** — `DATABASE.md` sync summary (version, phrases)
8. **TASK-041 dependency** — state the chain above; TASK-041 remains
   BACKLOG pending the readiness re-audit
9. **Deviations / report-only issues** — anything discovered and not
   fixed (`AGENTS.md` §16)

---

## Completion Evidence

<!-- TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE. -->

### Changed Files
- `src/backend/GameServer.Infrastructure/Postgres/Migrations/20260926151112_ProvisionBossDefinitions.cs` (+ `.Designer.cs`) — new data-only migration: `Up` = 3× `InsertData` (all five columns), `Down` = 3× mirror `DeleteData`; no schema operation.
- `tests/backend/GameServer.Infrastructure.Tests/BossPersistenceTests.cs` — stale "no provisioning mechanism is documented" comments updated (§6 item 3, assertions untouched), private migration-reader helper generalized, 9 migration-source tests added.
- `tests/backend/GameServer.Infrastructure.Tests/BossDefinitionPostgresProvisioningTests.cs` — NEW opt-in live-PostgreSQL applied-row verification (patterned on `PlayerPostgresConstraintTests`).
- `docs/02-technical/DATABASE.md` — v1.12 → v1.13: §1 note item 5 final bullet and §5 item 4 status wording synced (contract resolved / implementation complete / three rows provisioned); no contract content changed.

### Validation Results
- `dotnet test tests/backend/GameServer.Infrastructure.Tests` — PASS (139 tests, 0 failed, 0 skipped).
- Full backend suite (`Api`/`Application`/`Domain`/`Infrastructure`) — PASS (115 + 210 + 911 + 139 = 1375 tests, 0 failed).
- `dotnet ef database update` — Done; idempotent re-apply (no change), normal workflow.
- `dotnet ef migrations list` — 6 migrations, all applied, none pending.
- `dotnet ef migrations has-pending-model-changes` — "No changes have been made to the model since the last migration."
- `dotnet ef migrations script 20260926124429_AddBossPersistence 20260926151112_ProvisionBossDefinitions` — 3× `INSERT INTO "BossDefinition"` + `__EFMigrationsHistory` row only; no DDL.
- Live row verification (`localhost:5433` / `dcacti_db`): `COUNT=3`; ids = `boss-def-hoa-long`, `boss-def-thuy-ma`, `boss-def-moc-yeu`; identities = `boss-hoa-long`, `boss-thuy-ma`, `boss-moc-yeu`; 0 rows with NULL columns; 2 indexes (PK + `IX_BossDefinition_Identity`); `__EFMigrationsHistory` contains `20260926151112_ProvisionBossDefinitions`; `BattleResult` table not yet present (expected — TASK-041 not implemented).
- Historical migrations, `AddBossPersistence`, and `GameDbContextModelSnapshot.cs` — zero `git diff`; new migration SHA256 `110A5492…` (`.cs`) / `C68614BE…` (`.Designer.cs`), snapshot SHA256 `76482BE4…`.
- Environments applied: local development PostgreSQL (`localhost:5433`) — the only battle-capable environment present.

### Server Authority & Scope Verification
- [x] Zero client-authoritative logic; zero runtime provisioning code (no `HasData`, seed, startup loader, service, repository, `ON CONFLICT`, or new architecture component; `src/` diff limited to `Migrations/`)
- [x] `MVP_SCOPE.md` §1 adhered to; §6 exclusions untouched

### Lifecycle Note
- The executing agent performed the implementation, tests, and `DATABASE.md` sync but never updated this file; this entry retroactively records that completed work (status, AC, evidence) rather than re-doing it. `Status: BACKLOG` → `DONE` and the move to `tasks/completed/` reflect workflow progress per `tasks/TASK_LIFECYCLE.md` §5.
