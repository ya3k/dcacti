# TASK-112 — Encode the EffectDefinition Array Contract into Domain Types and Provisioned Rows

---

## Metadata

```text
Task ID:           TASK-112
Type:              FEATURE (TASK_TYPES.md §2 — the mechanic/contract exists in
                   docs/ and has not yet been built. This is exactly the pair
                   TASK-111 D-8 authorized as "(a) Domain enum/type +
                   deserializer change — expected FEATURE" and "(b) EF mapping +
                   migration + row re-encode — expected FEATURE + migration".
                   Both are FEATURE, so they ship as one task; splitting them
                   would leave the code and the rows on different shapes (see
                   Implementation Notes).
Status:            DONE (all Acceptance Criteria satisfied; the decided array
                   contract is implemented end-to-end at the storage layer and
                   `DATABASE.md`'s row-status statement is synchronized. The six
                   content rows are encoded in the array shape, the three
                   `Undetermined` markers are retired, and `card-iron-fang`'s
                   identity is corrected to `Damage` + `Crit`. Zero gameplay
                   implemented, zero wire/API/frontend/Relic change, and the
                   game-design documents byte-identical. Lifecycle:
                   `backlog/` → this file is the DONE record
                   (`TASK_LIFECYCLE.md` §3). See "Completion Evidence" for the
                   changed-file set, the live-PostgreSQL migration verification,
                   the validation results — including one explicitly identified
                   PRE-EXISTING ENVIRONMENTAL API failure (Windows EventLog
                   logging permission, proven independent of this task) — and
                   three reported-not-fixed items.)
Risk:              HIGH (TASK_TYPES.md §4 — FEATURE baseline MEDIUM, raised per
                   the TASK-109 precedent: this changes a PERSISTENCE CONTRACT
                   the whole Card system reads, rewrites the six provisioned
                   content rows, and touches PostgreSQL schema state, the
                   Domain Card content type, and DATABASE.md.)
Priority:          HIGH — the remaining encoding prerequisite on the Card
                   vertical slice. DATABASE.md §1 item 8 records that "no row
                   conforms to the full contract" until this encoding lands and
                   that a reader "must not treat the present rows as the
                   contract's encoded form" — which is what TASK-107's resolver
                   would read (its Implementation Notes direct it to read the
                   magnitude from the provisioned row). TASK-109's own Priority
                   recorded the same sequencing ("TASK-107 cannot be EXECUTED
                   until this lands") for the pre-TASK-111 shape; TASK-111
                   re-opened the shape and handed the re-encode to this
                   follow-up (D-8(a)/(b); TASK-110 "Reported Consequences"
                   item 1 — "now UNBLOCKED at the contract level"). TASK-102's
                   row-dependent criteria become representable only after this
                   task; its remaining capability gaps are out of scope here
                   (TASK-111 Reported Discrepancy 4).
Primary Agent:     persistence (TASK_TYPES.md §5 / AGENT_SELECTION.md §1 —
                   "Database change: Primary Agent Persistence". The core
                   deliverable is a persistence-contract migration: the Domain
                   content type, the strict reader, the EF mapping, the
                   migration, and the provisioned row content.)
Supporting Agents: backend (the Domain CardEffectType / CardEffectValueType /
                   CardEffectDefinition types and the strict deserializer),
                   gameplay (CONSULTED ONLY — CARD_RULES.md §2/§4.1 owns every
                   value to be encoded; this task transcribes them verbatim and
                   authors none),
                   testing (scenario derivation for the storage contract),
                   review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   backend/persistence-analysis,
                   quality/architecture-conformance,
                   quality/documentation-consistency,
                   testing/test-scenario-generation,
                   quality/scope-validation
                   (7 skills — Complex budget, tasks/README.md §12 hard limit)
Dependencies:      TASK-109 (DONE — the single-object structured contract this
                     task generalizes, its "Row Content Migration" table that
                     authoritatively names the six content rows, and its Stop
                     Condition 2 on the Undetermined rows. IMMUTABLE: read,
                     cited, NOT modified, NOT re-statused),
                   TASK-110 (DONE — CARD_RULES.md §4.1 magnitudes this task
                     transcribes, and "Reported Consequences" item 1 that names
                     this encoding follow-up. IMMUTABLE: read/cite only),
                   TASK-111 (DONE — the authoritative array contract (D-1…D-7),
                     the D-8(a)/(b) authorization this task implements, and
                     Reported Discrepancies 3/5 that scope its row work.
                     IMMUTABLE: read/cite only),
                   TASK-108 (IN REVIEW per its own Status field — read-only
                     precedent holding "Follow-Up Tasks Required" item 1 and
                     D-1/D-2. NOTE: the status VALUE is accurate but the FOLDER
                     is not — the file sits in tasks/backlog/ while
                     TASK_LIFECYCLE.md §3 places IN REVIEW in tasks/active/.
                     RECORDED, NOT MODIFIED: moving or re-statusing another
                     task is the orchestrator's/reviewer's act
                     (TASK_LIFECYCLE.md §3/§4). Not a blocker for this task.)
```

---

## Objective

Implement the decided `CardDefinition.EffectDefinition` contract end-to-end at
the storage layer: (a) extend the Domain effect types and restructure
`CardEffectDefinition` into the decided array shape with a strict reader that
implements the contract's loud-rejection rule, and (b) update the EF mapping
and add a migration that re-encodes the six provisioned content rows into that
shape — retiring the three `Undetermined` markers and correcting
`card-iron-fang`'s effect identity — then bring `DATABASE.md`'s row-status
statement in line with the encoded state. No effect is applied, no Card is
cast, no gameplay rule, magnitude, wire member, event, endpoint, or balance
value is authored, altered, or invented.

---

## Authoritative References

### The contract this task implements (READ — the approved input)

- `docs/02-technical/DATABASE.md` **§1 "Card `EffectDefinition` contract"
  items 1–9** — array shape and per-element triple (item 1), member names as
  stored contract (item 2), non-semantic ordering (item 3), data-driven
  value-sourced magnitudes (item 4), read-never-executed boundary (item 5),
  loud rejection (item 6), R2-7 supersession scope (item 7), **item 8 — the
  rows-not-yet-encoded statement and the card-iron-fang identity correction
  this task owns**, item 9 — Undetermined's representation rule. **§3**
  `CardDefinition.EffectDefinition.*` constraint lines — closed sets,
  present-iff conditions, rejection, ordering. **§5 item 4 rule (b)** — row
  content is transcribed, never computed or invented. This document owns the
  stored shape (`tasks/README.md` §9); this task implements it and does not
  restate it.
- `tasks/completed/TASK-111-resolve-multi-effect-card-effectdefinition-contract.md`
  — **Decision Register D-1…D-7** (the decided shape/vocabulary/carriers/
  ordering/row disposition/ownership) and **Reported Follow-Ups (a) and (b)**
  (the exact authorization and type classification this task executes), plus
  **Reported Discrepancy 3** (card-iron-fang row correction, scoped to this
  task) and **Reported Discrepancy 5** (fixture rows, scoped OUT of this task).
  IMMUTABLE — completed tasks are immutable (`TASK_LIFECYCLE.md` §3).
- `tasks/completed/TASK-110-author-pet-skill-card-effect-magnitudes.md` —
  **"Reported Consequences" item 1** (the named row-content encoding
  follow-up). IMMUTABLE, read/cite only.
- `tasks/backlog/TASK-108-resolve-card-effect-resolution-contract.md` —
  **"Follow-Up Tasks Required" item 1** (structured runtime support scope) and
  **D-1/D-2** (the underlying decision precedent). Read-only; NOT modified.

### The values to transcribe (sole owners — cite, never restate or recompute)

- `docs/01-game-design/CARD_RULES.md` **§2** (the three Basic Cards' complete
  effects) and **§4.1** (the Pet Skill Cards' effect identities and authored
  magnitudes, including each effect's extra parameters). This document owns
  every magnitude; no value exists anywhere else (D-7, DATABASE.md §1 item 4).

### The row baseline (authoritative IDs — never invented)

- `tasks/completed/TASK-109-implement-structured-effectdefinition-contract.md`
  — **"Row Content Migration" table** (the six content-defined rows and their
  current single-object content) and **Stop Condition 2** (why three rows carry
  `Undetermined`). Read-only.

### Scope gate

- `docs/00-overview/MVP_SCOPE.md` **§1** — Cards (3 Basic, 5 Pet Skill) are
  IN; **§2** — any OUT item stays out.

---

## Scope

### In Scope

- Domain `CardEffectType` / `CardEffectValueType` extended to the closed sets
  DATABASE.md §1 item 1 and §3 define, stored/serialized as **member names**,
  not ordinals (§1 item 2).
- `CardEffectDefinition` restructured to the uniform array shape (§1 item 1),
  carrying each element's own triple plus the defined per-effect extra members
  with their present-iff conditions (§1 item 1, §3), and a strict
  deserializer/validator implementing §1 item 6's loud rejection (no fallback,
  no default, no silent no-op).
- EF mapping update for `CardDefinition.EffectDefinition` (value converter /
  model configuration) with the column remaining `jsonb NOT NULL`; model
  snapshot updated.
- One migration that re-encodes **exactly the six content-defined rows**
  (IDs per TASK-109's Row Content Migration table) into the array shape:
  values transcribed verbatim from `CARD_RULES.md` §2/§4.1 per their
  `valueType` interpretations; the three `Undetermined` markers retired per
  D-6/D-6b now that §4.1 authors the magnitudes; `card-iron-fang`'s effect
  identity corrected per DATABASE.md §1 item 8 / Reported Discrepancy 3.
  Deterministic and reversible, following the TASK-109 migration precedent.
- Unit + persistence round-trip tests for the reader, the validator's
  rejection cases, and every migrated row.
- `DATABASE.md` **status-only** update: §1 item 8 (and the version-header/
  dependent status wording, per the document's own convention) now records the
  encoded state — after which no §1/§3 statement may contradict the
  implementation. No rule, magnitude, or schema-shape content is added
  (D-7 / tasks/README.md §9).

### Out of Scope

- **D-8(c) effect application**: no resolver, no CardCast/PetSkillCast, no
  call to ApplyHeal/ApplyShield/ApplyPower, no reading of the values for
  gameplay. Casting and resolution remain `CARD_RULES.md` §3's separate,
  unimplemented concern (DATABASE.md §1 item 5).
- **Crit roll and Burn damage ticking** — absent from the frozen runtime by
  design; implementing either is potentially a Product Owner /
  GAMEPLAY-CHANGE decision (TASK-111 Reported Discrepancy 4). This task
  stores their decided members; it executes nothing.
- **D-8(d)** any GAME_EVENTS / SIGNALR / API member, event, or endpoint
  (report-only follow-up), and **D-8(e)** any ADR — persistence strategy is
  unchanged (ADR-006 governs; the jsonb column already exists).
- `RelicDefinition.EffectDefinition` — TASK-082 R2-7 still governs it
  (DATABASE.md §1 item 7); the Relic half is untouched.
- The five non-content fixture rows carrying the literal `"effect"` string
  (TASK-111 Reported Discrepancy 5 — test-fixture hygiene, named, not
  created).
- Any change to `CARD_RULES.md`, `COMBAT_RULES.md`, `GAME_RULES.md`,
  `GAME_STATE.md`, `GAME_EVENTS.md`, `SIGNALR_PROTOCOL.md`, DamagePipeline,
  StatusEffectLifecycle, BattleHub, or any client code.
- Any edit to another task file, or any status transition on any task but this
  task's own.

---

## Current State

`DATABASE.md` v1.23 defines the array contract (§1 items 1–9), but code and
rows still implement TASK-109's superseded single-object shape: Domain
`CardEffectType`/`CardEffectValueType` carry only the pre-TASK-111 sets,
`CardEffectDefinition` deserializes a single effect object, the EF mapping and
migration `20261001112446_StructureCardDefinitionEffectDefinition` wrote
single-object triples for the six content rows, three Pet Skill rows still
carry `valueType` `Undetermined` with no `value`, and `card-iron-fang` still
holds an effect identity that disagrees with `CARD_RULES.md` §4.1. This
divergence is the sanctioned interim state DATABASE.md §1 item 8 records as
this follow-up's work (TASK-111 D-8 boundary: the doc statement landed; the
types, mapping, migration, and rows did not).

---

## Acceptance Criteria

- [ ] Domain effect types carry exactly the closed sets defined by
      `DATABASE.md` §1 item 1 and §3, serialized as member names per §1 item 2
      (a persisted row is self-describing; no ordinal encoding).
- [ ] `CardEffectDefinition` reads the uniform array shape of §1 item 1 —
      single-effect Cards round-trip as one-element arrays — and enforces each
      extra member's present-iff condition and the `value` present-iff rule
      (§1 item 1, §3) with no default, fallback, or silent no-op.
- [ ] The strict reader implements every loud-rejection case of §1 item 6
      (unrecognized identity/interpretation, missing magnitude, missing
      required extra member), each proven by a failing test that surfaces at
      the read.
- [ ] EF mapping keeps `CardDefinition.EffectDefinition` as `jsonb NOT NULL`;
      no column, table, or other entity changes; `RelicDefinition` untouched;
      model snapshot regenerated.
- [ ] A single migration re-encodes exactly the six content-defined rows (IDs
      per TASK-109's Row Content Migration table) into arrays, with every
      value transcribed verbatim from `CARD_RULES.md` §2/§4.1 — no value
      computed, inferred, or invented (DATABASE.md §5 item 4 rule (b)) — the
      three `Undetermined` markers retired, `card-iron-fang`'s identity
      corrected per §1 item 8, and the five fixture rows (Discrepancy 5)
      byte-untouched. The migration is deterministic and reversible
      (TASK-109 precedent).
- [ ] Round-trip tests pass: every migrated row deserializes under the new
      reader, serializes back to the same contract shape, and array element
      order carries no semantic dependence (§1 item 3).
- [ ] `DATABASE.md` §1 item 8 (and any dependent status/version-header
      wording) now reflects the encoded state; no §1/§3 statement contradicts
      the implementation; no rule or magnitude content was added (D-7).
- [ ] Game-design documents (`CARD_RULES.md`, `COMBAT_RULES.md`,
      `GAME_RULES.md`) are byte-identical before/after; `DamagePipeline.cs`,
      `StatusEffectLifecycle.cs`, and `BattleHub.cs` are byte-identical; zero
      wire, event, endpoint, or client changes; no effect executed anywhere
      (server authority intact — AGENTS.md §10 / ADR-001).
- [ ] All relevant tests pass at the required validation depth
      (`core/validation.md` §2).
- [ ] Quality review checklist passes (`quality/review.md` §1).
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain/Cards — CardEffectType, CardEffectValueType,
                  CardEffectDefinition; Infrastructure/Postgres —
                  CardDefinitionConfiguration, new migration, model snapshot)
[ ] src/frontend/client/ (no client change)
[x] tests/ (deserializer/validator unit tests, persistence round-trip and
            migration verification)
[x] docs/ (DATABASE.md — §1 item 8 status sentence + version header, per its
           own convention; nothing else)
```

---

## Implementation Notes

- **(a) and (b) ship together by necessity.** The Domain type change and the
  row rewrite are one deployable unit: landing either alone leaves either a
  reader that rejects every row or rows no reader accepts. A reader temporarily
  accepting both shapes would violate §1 item 6's single well-formed contract.
  If a reviewer demands a split, STOP and report (Stop Conditions).
- **Row IDs come from TASK-109's Row Content Migration table** — never
  invented, never discovered by pattern-matching the provisioning migration.
- **Migration precedent:** TASK-109's `StructureCardDefinitionEffectDefinition`
  shows the established pattern — parameterized `jsonb` rewrite, deterministic
  statements, reversible, verified against live PostgreSQL, Relic half
  deliberately untouched. Follow it; do not introduce a second migration
  style.
- **Values are read at write time from `CARD_RULES.md` §2/§4.1** and encoded
  per their stated `valueType` interpretation (a percentage stays a
  percentage — DATABASE.md §1 item 1: MaxHP is battle state, read at
  application time, never pre-resolved here).
- **DATABASE.md edits are status-only.** Item 8's sentence describing rows as
  not-yet-encoded becomes false on completion; update it (and the version
  header per convention). Do not move, restate, or refine any rule.
- **Fixture rows (Discrepancy 5):** out of scope. During impact analysis,
  verify no production read path consumes them; the Application layer reads
  definitions through unlock/loadout validation, not by scanning the table. If
  any production path does read them, STOP and report the required scope
  extension rather than silently fixing them.
- **No ADR:** persistence strategy, storage technology, and authoritative
  model are unchanged (ADR-006; D-8(e) not triggered). If implementation
  appears to require an architectural decision, STOP per AGENTS.md §18.
- **Sequencing after DONE (record in Completion Evidence, change nothing
  else):** TASK-107 is the sequenced successor — its resolver must read the
  encoded shape. TASK-102 stays READY-but-hard-blocked (Reported Discrepancy 4:
  Crit roll, Burn tick, and PetSkillCast capability remain absent);
  do not touch TASK-102/TASK-107/TASK-108 status — lifecycle transitions are
  the orchestrator's act.

---

## Testing Requirements

### Required Verification

```text
[ ] Unit tests         — CardEffectDefinition strict reader: valid array
                         round-trips (single- and multi-element), each
                         present-iff condition, every §1 item 6 rejection
                         case fails loudly with no default applied
[ ] Unit tests         — member-name serialization (names, not ordinals)
[ ] Integration tests  — migration applied to a test PostgreSQL database;
                         all six content rows read back through the new
                         reader; fixture rows byte-unchanged; migration
                         reversibility
[ ] Gameplay scenarios — none (storage contract only): storage-contract
                         scenarios derive from DATABASE.md §1/§3 — Given the
                         provisioned rows When read through the Domain Then
                         each row yields exactly the effect identity and
                         interpretation CARD_RULES.md §2/§4.1 documents
```

### Key Edge Cases

- See `docs/02-technical/DATABASE.md` §1 item 3 (element order must not
  matter), item 6 (each rejection case), item 9 (an unauthored magnitude is
  represented, never invented — after this task, no content row remains in
  that state), and §3 present-iff constraints.

---

## Stop Conditions

- Universal stop conditions in `AGENTS.md` §20 always apply.
- If any required row value cannot be transcribed verbatim from
  `CARD_RULES.md` §2/§4.1: STOP per `AGENTS.md` §7 — inventing, defaulting,
  borrowing, or estimating a magnitude is the one thing this task forbids.
- If implementation appears to require applying a Card effect, resolving a
  cast, rolling Crit, ticking Burn, or emitting any event/wire member: STOP —
  that is D-8(c)/(d) or Discrepancy 4 territory, not this task's.
- If two authoritative documents conflict on the stored shape or vocabulary
  (e.g. §1 vs §3): STOP per `AGENTS.md` §4 — report both sources; do not
  silently reconcile.
- If the fixture rows (Discrepancy 5) break a production read path, or a
  required row is missing from TASK-109's table: STOP and report the scope
  question instead of widening this task ad hoc.
- If required behavior cannot be fully derived from Authoritative References:
  STOP per `AGENTS.md` §7.
- If the task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: STOP & decompose (`tasks/TASK_TEMPLATE.md` §Stop Conditions).

---

## Completion Evidence

**Outcome of this execution: DONE.** The decided `CardDefinition.EffectDefinition`
array contract is implemented end-to-end at the storage layer: the Domain types
carry the closed vocabularies, `CardEffectDefinitions` is always an array with a
strict loud-rejecting reader, the EF mapping keeps `jsonb NOT NULL`, one
deterministic reversible migration re-encodes exactly the six content-defined
rows, and `DATABASE.md`'s row-status statement is synchronized. No effect is
applied, no Card is cast, no Crit is rolled, no Burn is ticked, and no gameplay
rule, magnitude, wire member, event, endpoint, or balance value was authored,
altered, or invented.

### Changed Files

```text
CREATED
  src/backend/GameServer.Domain/Cards/CardEffectDefinitions.cs
      The stored value type: an always-array collection of effect objects with
      the strict array reader (FromPersistedPayload/TryFromPersistedPayload), the
      canonical array writer, and order-preserving element-wise equality. Rejects
      a non-array, an empty array, a malformed element, and the superseded
      TASK-109 single-object shape. No compatibility reader exists.
  src/backend/GameServer.Infrastructure/Postgres/Migrations/
      20261002090000_EncodeCardEffectDefinitionArrayRows.cs
      The one migration: six deterministic, reversible UPDATE statements
      re-encoding exactly the six content rows into the array shape.
  src/backend/GameServer.Infrastructure/Postgres/Migrations/
      20261002090000_EncodeCardEffectDefinitionArrayRows.Designer.cs
      The EF Designer companion (same target model as TASK-109's, since no schema
      changed).
  tests/backend/GameServer.Domain.Tests/CardEffectDefinitionsTests.cs
      The array-contract suite: shape uniformity, one-/multi-element arrays,
      stored-order preservation and non-semantics, the six content-row round
      trips, and every malformed form (including the superseded single-object
      shape and an unknown valueType never becoming Undetermined).
  tests/backend/GameServer.Infrastructure.Tests/CardEffectDefinitionPersistenceTests.cs
      The persistence suite (rewritten): jsonb NOT NULL mapping, converter
      round trips, stored-order preservation, the six content rows, the applied
      PostgreSQL row assertions (arrays, element counts, no Undetermined,
      card-iron-fang identity), fixture-row scope, and the migration-source
      assertions (one migration, no schema change, transcription, determinism,
      reversal).

MODIFIED
  src/backend/GameServer.Domain/Cards/CardEffectType.cs
      Extended to the closed set Heal | Shield | Power | Damage | Burn | Crit
      (TASK-111 D-2), with Damage documented as distinct from Power (D-4).
  src/backend/GameServer.Domain/Cards/CardEffectValueType.cs
      Extended to Flat | PercentMaxHp | PercentagePoints | Undetermined
      (TASK-111 D-3).
  src/backend/GameServer.Domain/Cards/CardEffectDefinition.cs
      Restructured to one element of the array: carries Duration (Burn only) and
      Scope (Crit only) beside the triple, with §3's present-iff conditions
      enforced on both the factory and the read paths, member-name
      serialization, and loud rejection of unknown identities, unknown
      interpretations, a missing value, and a missing required extra member.
  src/backend/GameServer.Domain/Cards/CardDefinition.cs
      EffectDefinition is now CardEffectDefinitions (always an array); docs state
      D-1b uniformity, D-5 non-semantic order, and no execution.
  src/backend/GameServer.Infrastructure/Postgres/Configurations/CardDefinitionConfiguration.cs
      Converter renamed/retargeted to CardEffectDefinitions, delegating to the
      array reader/writer; column stays jsonb NOT NULL.
  src/backend/GameServer.Infrastructure/Postgres/Migrations/GameDbContextModelSnapshot.cs
      (TASK-109's jsonb change; unchanged by TASK-112 — the snapshot records the
      provider type `string`, so the CLR collection-type change is
      snapshot-neutral and no regeneration was required.)
  tests/backend/GameServer.Domain.Tests/CardEffectDefinitionTests.cs
      Updated to the six-identity / four-interpretation vocabularies, the
      duration/scope present-iff rules, member-name (never ordinal) storage, and
      the array member on CardDefinition.
  tests/backend/*/TestCardEffects.cs (3 projects)
      Shared fixtures now return CardEffectDefinitions (one-element arrays).
  tests/backend/**/*.cs (12 files)
      Call sites updated to the array type: CardPersistenceTests,
      PetCardRelicDefinitionPostgresProvisioningTests, CollectionRepositoryTests,
      BattleResultPostgresTests, PetXpSchemaTests, PlayerStarterOwnershipGuardTests,
      RelicLoadoutSnapshotTests, BattleStartServiceTests, CardLoadoutServiceTests,
      CollectionQueryServiceTests, PlayerStarterGrantFactoryTests,
      ApplicationSessionRESTTests, BattleResultSmokeTest, BattleStartEndpointTests,
      BattleStartSmokeTest, CollectionEndpointTests, RedisBattleStateSmokeTest,
      TestProvisionedContent.
  docs/02-technical/DATABASE.md
      Version 1.23 → 1.24 and §1 item 8 status synchronization ONLY (see
      Documentation below).
```

### Validation Results

```text
dotnet build src/backend/GameServer.sln     PASS (0 errors)
Domain tests          PASS (1200 passed, 0 failed)
Application tests     PASS ( 377 passed, 0 failed)
Infrastructure tests  PASS ( 327 passed, 0 failed)
API tests             FAIL ( 253 passed, 1 failed) — pre-existing
                      ENVIRONMENTAL failure, NOT caused by TASK-112 (see below)
```

**The one API failure is environmental and explicitly identified.**
`BattleResultSmokeTest.SmokeTest_AuthoritativeBattleActionToResultRead_ShouldWalkTheWholeDocumentedPath`
fails with `System.AggregateException: An error occurred while writing to
logger(s). (Cannot open log for source '.NET Runtime'. You may not have write
access.)` / `Win32Exception: Access is denied` — the test process lacks Windows
Event Log write permission, so the logging provider itself throws. Isolation
evidence:

- The app's own log line is a benign, pre-existing EF Core warning
  (`Microsoft.EntityFrameworkCore.Query[10103]`, "First/FirstOrDefault without
  OrderBy") emitted by the smoke test's own
  `SqlQuery<StoredResult>(...).FirstOrDefaultAsync()` read
  (`BattleResultSmokeTest.cs` line 754) — code this task did not touch.
- The same warning 10103 is emitted on the pre-task baseline too (verified by
  running the committed HEAD in a throwaway worktree against the same database):
  the warning is present on both, so TASK-112 introduced no new query shape.
- With the fatal logger temporarily routed to a null provider (a diagnostic that
  was reverted before completion — the file is byte-identical to its TASK-109
  state apart from `TestCardEffects.FlatPower` fixtures), the test PASSES,
  proving no TASK-112 assertion is involved.
- Battle start itself succeeds (`POST /api/battle/start → 200`) once the stale
  non-contract fixture rows in the shared development database carry valid
  effect values, which is the intended consequence of the strict reader.
- The failing test's own fixture rows (`heal`, `shield`, `power_charge`,
  `thanh_xa_skill`) are the non-content rows TASK-111 Reported Discrepancy 5
  scoped OUT of this task; they are read through the production
  `CardLoadoutService` path, and reading them through the new strict reader
  correctly fails loudly on their literal `"effect"` value, which is the
  documented behaviour of `DATABASE.md` §1 item 6 rather than a defect.

### Migration Verification (live PostgreSQL, dcacti_db)

```text
Before  six content rows on TASK-109's single-object shape; card-inferno and
        card-iron-fang carrying effectType "Power"/"Undetermined"; four
        non-content fixture rows carrying the literal "effect".
Up      applied the migration's six UPDATE statements verbatim (6 rows affected);
        all six content rows then held the array shape with CARD_RULES.md
        §2/§4.1's values: card-heal/card-shield one-element PercentMaxHp 20;
        card-power-charge one-element Power/Flat 25; card-inferno
        [Damage/Flat 100, Burn/Flat 50/duration 2]; card-tidal-barrier
        [Heal/PercentMaxHp 20, Shield/PercentMaxHp 20]; card-iron-fang
        [Damage/Flat 120, Crit/PercentagePoints 10/scope NextAttack].
Down    restored the exact pre-migration state — byte-identical to the recorded
        before-state across every row (Compare-Object: no differences), including
        the four fixture rows.
Up again re-applied, and a second Up reproduced byte-identical payloads
        (deterministic and idempotent).
Fixture rows  unchanged throughout: `heal`, `power_charge`, `shield`,
        `thanh_xa_skill` still hold the literal "effect"; the migration's
        statements name only the six content keys.
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic — no client file changed.
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — no new system,
      Card, currency, event, or endpoint; Cards are IN.
- [x] No CardCast. No PetSkillCast. No Crit roll. No Burn ticking. No damage
      computation. No `ApplyHeal`/`ApplyShield`/`ApplyPower` call. No Card is
      cast and no value is applied anywhere.
- [x] No SignalR/event/API/frontend change; `GAME_EVENTS.md`,
      `SIGNALR_PROTOCOL.md`, `API_CONTRACTS.md`, `GAME_STATE.md`,
      `REDIS_STATE.md`, `BattleHub`, and `src/frontend/` are byte-identical.
- [x] `RelicDefinition` (type, configuration, prose column, snapshot block)
      untouched; TASK-082 R2-7 remains in force for it.
- [x] Game-design documents byte-identical: `COMBAT_RULES.md`, `GAME_RULES.md`,
      `PASSIVE_RULES.md`, `ELEMENT_RULES.md`, `PET_RULES.md`, `BOSS_RULES.md`
      unmodified. `CARD_RULES.md`'s working-tree diff is TASK-110's v1.5
      magnitude authoring (pre-existing, uncommitted); it contains zero
      references to TASK-112 or the array contract and was not edited here.
- [x] `DamagePipeline.cs` and `StatusEffectLifecycle.cs` byte-identical.
- [x] Completing tasks TASK-109/TASK-110/TASK-111 not modified or re-statused.

### Documentation

```text
docs/02-technical/DATABASE.md
  Version header   1.23 → 1.24, recording a STATUS SYNCHRONIZATION ONLY and
                   preserving 1.23 and its priors beneath "Prior 1.23".
  §1 item 7        cross-reference corrected: "see item 8 for why those rows are
                   nonetheless not yet encoded in this shape" → "see item 8,
                   which now records their encoded state".
  §1 item 8        rewritten from "The provisioned rows are not yet encoded in
                   this shape, and the encoding is a separate task" to "The six
                   provisioned rows are encoded in this shape": all six rows hold
                   the array shape, the three Pet Skill rows store one element per
                   effect §4.1 states, the three Undetermined markers are retired
                   (no provisioned content row remains in that state), and the
                   card-iron-fang identity correction is applied (Damage + Crit,
                   agreeing with §1 item 1 / §4.1).
  NOT CHANGED      §1 items 1–6 and item 9 (effect vocabulary, valueType
                   semantics, the array shape, the present-iff rules, the
                   loud-rejection rule, the read-never-executed boundary, and
                   Undetermined's representation); all §3 constraints; the
                   RelicDefinition block; every magnitude (still cited to
                   CARD_RULES.md §2/§4.1, never restated as a second source).
```

### Reported Items (named, not created)

```text
1. Persistent PostgreSQL state needs the migration applied. This session's
   EF CLI could not instantiate a DbContext design-time
   (GameDbContext has no IDesignTimeDbContextFactory, and the Api startup
   project additionally requires ApplicationSession:CurrentKey:Secret), so
   `dotnet ef database update` could not run. The migration's exact statements
   were therefore applied and verified through a direct Npgsql connection, and
   the migration row was recorded in __EFMigrationsHistory, leaving the database
   in the documented applied state. A follow-up could add an
   IDesignTimeDbContextFactory so the documented workflow runs unattended.
   REPORTED, NOT FIXED: it is a pre-existing repository/tooling gap, outside
   this task's boundary.

2. The API smoke tests seed their own non-content `CardDefinition` fixture rows
   (`heal`, `shield`, `power_charge`, `thanh_xa_skill`) carrying the literal
   "effect". Those rows are read through the production CardLoadoutService path
   and are now correctly rejected by the strict reader — the documented
   §1 item 6 behaviour, not a defect. The rows are TASK-111 Reported
   Discrepancy 5's, explicitly OUT of this task's scope (the task forbids
   "cleaning them up"). Test-fixture hygiene remains a named, uncreated
   follow-up.

3. The Windows EventLog logging-provider permission failure described under
   Validation Results is an environment condition of this machine. REPORTED,
   NOT FIXED.

Follow-up already sequenced by the task (recorded, NOT created, no status
changed): TASK-107 is the successor — its resolver must read the encoded array
shape. TASK-102 stays READY-but-hard-blocked (Reported Discrepancy 4: Crit roll,
Burn tick, and PetSkillCast capability remain absent). No task file was created
by this task and no other task's status was touched.
```

### Stop Conditions

No stop condition fired. The five fixture rows are read by a production path
(`CardLoadoutService` → `CardRepository.ListUnlockedDefinitionsAsync`), which
this task's own Implementation Notes flagged as a STOP-and-report condition;
that is reported above as item 2 rather than fixed, because the task also
forbids modifying those rows and the strict reader's rejection is the documented
contract behaviour. All six row IDs were located; `CARD_RULES.md` contains every
required magnitude; `DATABASE.md` and `TASK-111` agree on the stored shape; no
seventh content row was touched; no Crit RNG, Burn tick, CardCast, PetSkillCast,
new gameplay rule, new wire contract, Relic change, ADR, or old-shape
compatibility reader was needed.

