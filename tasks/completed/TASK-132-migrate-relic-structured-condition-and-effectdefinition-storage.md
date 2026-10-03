# TASK-132 — Migrate Relic Structured Condition and EffectDefinition Storage

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and tasks/ by path and section.

  PROVENANCE: created by TASK-131 per its decision D9 — "varchar(128) is
  insufficient for structured Relic effects; a separate schema/storage
  migration task is required." TASK-131 recorded the contract decision and
  deliberately performed no schema change (AGENTS.md §18). This task is that
  separate migration.
-->

---

## Metadata

```text
Task ID:           TASK-132
Type:              ARCHITECTURE (TASK_TYPES.md §2 — "Change the project's
                   structure, layering, technology choice, persistence
                   strategy, realtime strategy, or authoritative-state
                   model." This changes the persistence strategy for two
                   RelicDefinition columns. TASK_TYPES.md §3: "The architecture
                   / technology / ADR needs to change" → ARCHITECTURE.
                   Workflow: architecture/architecture-change.md.)
Status:            DONE (direct execution verified — completion evidence
                   below matches the repository artifacts; recorded DONE per
                   TASK_LIFECYCLE.md §3, which reserves DONE for tasks
                   "satisfied by direct execution". File moved from
                   tasks/backlog/ to tasks/completed/.)
Risk:              MEDIUM (TASK_TYPES.md §4 — ARCHITECTURE baseline HIGH; the
                   database-migration trigger for HIGH "database migration" is
                   met. Classified MEDIUM-to-HIGH and treated as HIGH for
                   validation depth per TASK_TYPES.md §4's tie rule, because
                   the migration is additive at the column level (varchar →
                   jsonb) over 4 content rows with no player data and no
                   destructive operation. See "Risk classification note".)
Priority:          HIGH (D9's finding blocks the Relic resolution stage:
                   RELIC_RULES.md §8.6 and §8.7 record that structured Relic
                   storage is NOT IMPLEMENTED and the resolver cannot read a
                   Relic effect until it lands. GAME_RULES.md §17 step 11 has
                   no implementation on either side.)
Primary Agent:     persistence (TASK_TYPES.md §5 — PostgreSQL domain →
                   Persistence. Supporting: backend for the Domain content
                   types the columns deserialize into.)
Supporting Agents: backend (GameServer.Domain RelicDefinition content type and
                   the EF configuration), testing (migration up/down and
                   round-trip verification), review
Workflow:          architecture/architecture-change.md
Skills:            discovery/impact-analysis,
                   backend/persistence-analysis,
                   backend/api-contract-validation,
                   quality/architecture-conformance,
                   quality/documentation-consistency
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-131 (DONE — the contract is decided and recorded;
                   ADR-018 records the architectural consequence. This task
                   may not begin before TASK-131, which was its gate.)
Blocks:            The Relic trigger/effect implementation task (not yet
                   created) and the Relic content-encoding task
Estimate:          Normal (one migration, two columns, 4 content rows, the
                   EF configuration, and the Domain content type)
```

---

## Objective

Migrate `RelicDefinition.Condition` and `RelicDefinition.EffectDefinition` from
`character varying(128)` prose columns to the structured representation
`RELIC_RULES.md` §8 defines and `ADR-018` records, so that the Relic resolution
stage can read a Relic's Trigger condition and effect from its definition row
rather than from prose.

This task changes storage and the Domain content representation. It implements
**no** trigger evaluation, **no** effect application, and emits **no**
`RelicTriggered` event — those remain the Relic stage's own task
(`RELIC_RULES.md` §8.7).

---

## Authoritative References

- `docs/01-game-design/RELIC_RULES.md` **§8** — the canonical owner of the Relic Trigger/Condition/Effect contract (this task implements its **§8.6 Storage Consequence**; it restates no rule from §8.1–§8.5): §8.1 (structured `Condition` forms), §8.2 (structured `EffectDefinition[]`), §8.3 (`target`/`lifetime` vocabulary and allowed combinations), §8.4 (effect lifetime vs trigger re-evaluation), **§8.5 (the four provisioned rows as encoded values)**, §8.7 (startup status)
- `docs/01-game-design/RELIC_RULES.md` §3 (closed Trigger list — unchanged), §6 and notes 1–3 (the MVP Relic Reference and the deferred Burning Curse row), §7 (Events)
- `docs/02-technical/ADR/ADR-018` — the decision this task implements; items 1–3 (the structured shape), item 5 (no Relic counter and therefore no `GAME_STATE.md` change), item 9 (`varchar(128)` insufficient; the migration is this separate task)
- `docs/02-technical/DATABASE.md` §1 — the `RelicDefinition` block (now recording the structured shape and the migration requirement), the **"Relic `EffectDefinition` and `Condition` contract"** note, the **Card `EffectDefinition` contract** note (the `jsonb` precedent, items 1–9, including item 6's loud-rejection rule and item 9's `Undetermined` representation), the R2-7 supersession scope record, §2 (ownership), §3 (constraints), §5 item 4 (provisioned content rows)
- `docs/02-technical/ARCHITECTURE.md` §2.3 (persistence layer boundaries), §5 (anti-overengineering)
- `docs/02-technical/TDD.md` §2 (technical direction), §6 (determinism — this task introduces no RNG)
- `docs/00-overview/MVP_SCOPE.md` §1 (Relics IN), §4 (classification authority)
- `AGENTS.md` §5 (documentation-first), §9 (anti-overengineering), §14 (code change process), §15 (testing), §17 (documentation change), **§18 (architecture change rule — docs and ADR precede implementation)**, §20 (stop conditions), §22 (Definition of Done)
- `Reference implementation (Card precedent):` `src/backend/GameServer.Infrastructure/Postgres/Migrations/20261001112446_StructureCardDefinitionEffectDefinition.cs` and `20261002090000_EncodeCardEffectDefinitionArrayRows.cs`; `src/backend/GameServer.Domain/Cards/CardEffectDefinition.cs`, `CardEffectDefinitions.cs`, `CardEffectType.cs`, `CardEffectValueType.cs`; `src/backend/GameServer.Infrastructure/Postgres/Configurations/CardDefinitionConfiguration.cs`
- `tasks/completed/TASK-131-resolve-relic-trigger-and-effect-resolution-contract.md` — the decision set D1–D11 this task implements item D9 of
- `tasks/completed/TASK-112-encode-effectdefinition-domain-types-and-rows.md` — the exact Card-side precedent for encoding rows into the structured shape
- `tasks/completed/TASK-082-resolve-pet-card-relic-content-provisioning-contract.md` — decision A / R2-8 (the provisioned row set)

---

## Scope

### In Scope

1. **Domain content representation:** give `GameServer.Domain/Relics/RelicDefinition` the structured representation its two members require — a structured condition representation and a structured `EffectDefinition[]` — following the shape `RELIC_RULES.md` §8 defines. Reuse the Card-side domain types where the contract is identical rather than duplicating them (`AGENTS.md` §9).
2. **EF configuration:** update `RelicDefinitionConfiguration` so `Condition` and `EffectDefinition` map to the structured column shape, and remove the now-incorrect `HasMaxLength(128)` constraints on those two members.
3. **Migration (up and down):** a single EF Core migration that moves both columns from `character varying(128)` to the structured representation, with a correct and tested `Down` that restores the prior shape.
4. **Row encoding:** transcribe the four provisioned Relic rows (`RELIC_RULES.md` §8.5) into the structured shape — the values are already documented and are **transcribed, never computed or invented** (`DATABASE.md` §5 item 4 rule (b)).
5. **Round-trip verification:** the stored structured value deserializes into the Domain representation and back without loss, and a malformed stored value fails loudly rather than silently defaulting (`DATABASE.md` §1 Card contract item 6's standard, carried to Relics by its Relic note item 6).
6. **Documentation synchronization:** update `DATABASE.md` §1 to record the landed schema shape (and §5 item 4's provisioning status), and update `RELIC_RULES.md` §8.6/§8.7 to record that structured storage is now implemented. Do **not** restate the contract in `DATABASE.md`.
7. **ADR reconciliation:** `ADR-018` item 9 records the requirement; if its now-satisfied status requires a note, record it per `docs/03-decisions/README.md` conventions — without editing `ADR-018`'s decision content.

### Out of Scope

- **Trigger evaluation, condition evaluation, and effect application** — `RELIC_RULES.md` §8.7; the Relic stage's own task
- **`RelicTriggered` emission** — the Relic stage's task (`SIGNALR_PROTOCOL.md` §3.2.23)
- **Adding a member to `GAME_STATE.md` or `PetState`** — `ADR-018` item 5 introduces no Relic counter and no battle-state concept; this task adds none either
- **Any `BattleState`, Redis, or SignalR change** — nothing in this task touches active battle state or the wire
- **Any gameplay rule, magnitude, effect, or condition value** — every value is transcribed from `RELIC_RULES.md` §8.5; none is authored or adjusted here
- **Provisioning `Burning Curse`** — `RELIC_RULES.md` §6 note 3 / TASK-131 D7 keep it deferred; its `Trigger` conflict is unresolved and no row may be inserted
- **Adding, removing, or re-scoping a provisioned Relic definition** — TASK-082 A / R2-8
- **Changing `RelicDefinition.Trigger`** — §3's closed list is unchanged (TASK-131 D8); `Trigger` stays a prose value from that list
- **The `CardDefinition` structured contract** — landed by TASK-109/112; unchanged here
- **Relic stacking** — `RELIC_RULES.md` §2.4 item 6
- **Modifying any completed or superseded task** — `tasks/TASK_LIFECYCLE.md` §3
- **Reopening TASK-079, TASK-099, or TASK-102** — terminal `SUPERSEDED`
- **TASK-036's Discord credential decisions** — separate, independent blocker

---

## Current State

```text
Domain:
  src/backend/GameServer.Domain/Relics/RelicDefinition.cs
    Trigger           string   (required, from RELIC_RULES.md §3's closed list)
    Condition         string?  (free text — "every 3 Matches")
    EffectDefinition  string   (required, free text — "+5% ATK")

Infrastructure:
  src/backend/GameServer.Infrastructure/Postgres/Configurations/
    RelicDefinitionConfiguration.cs
      Trigger           .HasMaxLength(64)
      Condition         .HasMaxLength(128)     ← insufficient (D9)
      EffectDefinition  .HasMaxLength(128)     ← insufficient (D9)

Rows (migration 20260929152651_ProvisionPetCardRelicContentDefinitions):
  relic-berserker-core  OnMatchCount  "every 3 Matches"    "+5% ATK"
  relic-mana-crystal    OnMatchCount  "every 4 Matches"    "+10 Power"
  relic-assassin-eye    OnCombo       "Combo >= 3"         "Increased Crit chance"
  relic-emergency-core  OnHpBelow     "HP < 30%"           "Heal Card cost -50%"

Card precedent (already landed — the shape to follow):
  CardDefinitionConfiguration maps EffectDefinition to jsonb
  Migrations 20261001112446_StructureCardDefinitionEffectDefinition
             20261002090000_EncodeCardEffectDefinitionArrayRows
  Domain types CardEffectDefinition / CardEffectDefinitions /
               CardEffectType / CardEffectValueType
```

---

## Acceptance Criteria

- [ ] `RelicDefinition.Condition` and `RelicDefinition.EffectDefinition` are stored in the structured representation `RELIC_RULES.md` §8 defines
- [ ] The four provisioned Relic rows hold exactly the values `RELIC_RULES.md` §8.5 records — each value transcribed, none computed or invented
- [ ] `RelicDefinitionConfiguration` no longer constrains those two members to `HasMaxLength(128)`
- [ ] The migration's `Down` restores the prior `character varying(128)` shape, and the reversal is exercised in a test
- [ ] A stored structured value round-trips: persist → read → Domain representation → persist, with no loss
- [ ] A malformed or unrecognized stored value fails loudly — no fallback magnitude, no default, no silent no-op (`DATABASE.md` §1 Relic note item 6)
- [ ] An `Undetermined` effect element is representable and carries no `value` member (`RELIC_RULES.md` §8.2 item 3); no provisioned row requires it, and none is introduced
- [ ] `RelicDefinition.Trigger` remains its documented prose value from §3's closed list, unchanged
- [ ] No `GAME_STATE.md`, `PetState`, Redis, or SignalR contract changes (ADR-018 items 5 and 10)
- [ ] `Burning Curse` remains unprovisioned; no placeholder row, Trigger, or value is inserted
- [ ] `DATABASE.md` §1 records the landed shape and §5 item 4 its status; `RELIC_RULES.md` §8.6/§8.7 record the change — with no rule restated in `DATABASE.md` and no second source of truth created
- [ ] Zero gameplay values, magnitudes, or thresholds are authored or altered; every one is transcribed from `RELIC_RULES.md` §6/§8.5
- [ ] `tasks/completed/` is unmodified; TASK-131, TASK-079, TASK-099, TASK-102, and all completed tasks are byte-identical
- [ ] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[x] src/backend/GameServer.Domain/Relics/ (RelicDefinition content type; any
                                            new structured condition/effect
                                            representation it requires)
[x] src/backend/GameServer.Infrastructure/Postgres/Configurations/
                                            (RelicDefinitionConfiguration.cs)
[x] src/backend/GameServer.Infrastructure/Postgres/Migrations/
                                            (the new migration + designer +
                                             model snapshot)
[ ] src/backend/GameServer.Application/ (no change expected — no resolver is
                                          implemented here; confirm the
                                          RelicLoadoutService still reads only
                                          identity)
[ ] src/frontend/client/ (none)
[x] tests/ (migration round-trip, configuration mapping, encoding verification)
[x] docs/ (DATABASE.md §1/§5 status; RELIC_RULES.md §8.6/§8.7 status)
[ ] tasks/completed/ (MUST remain unmodified)
```

---

## Implementation Notes

- **Follow the Card precedent rather than inventing a second shape.** `CardDefinition` already made this exact `varchar(128)` → `jsonb` move (TASK-109) and already encoded its rows (TASK-112). `RELIC_RULES.md` §8.2 states the Relic representation "follow[s] the same representation contract `DATABASE.md` §1 records for `CardDefinition.EffectDefinition`". Reuse the existing Domain effect types where the contract is identical; add a Relic-specific type only where §8 genuinely differs.
- **`Condition` has no Card counterpart.** The Card contract has no structured condition member, so the `Condition` representation is new work here. Its forms are exactly `RELIC_RULES.md` §8.1's three, and its thresholds are transcribed from §8.5. Do not extend the form set.
- **This is an additive, non-destructive migration over 4 content rows.** There is no player data in `RelicDefinition` and no `Relic` ownership row is touched (ownership is the separate `Relic` table). Confirm that before and after the migration; if any operation would be destructive or lossy, STOP per `AGENTS.md` §20.
- **Do not implement the resolver.** It is tempting to "finish the job" by reading the newly structured column. That is the Relic stage's task (`RELIC_RULES.md` §8.7, `AGENTS.md` §16). This task ends at storage, configuration, encoding, and verification.
- **`Undetermined` handling follows the Card contract.** `DATABASE.md` §1 Card contract item 9 defines it: the element stores the effect identity with `valueType` `Undetermined` and **no** `value` member. No provisioned Relic needs it (`RELIC_RULES.md` §8.5 authors every magnitude), but the representation must accept it because §8.2 item 2 keeps it valid.
- **Keep `DATABASE.md` non-duplicative.** It records the storage shape and the migration status only; `RELIC_RULES.md` §8 owns the contract (`.ai/README.md` §6, `documentation/documentation-change.md` §2).
- **Migration ordering and the model snapshot:** the EF model snapshot must reflect the change, and the migration timestamp must sort after the existing migrations. The highest existing migration is `20261002090000_EncodeCardEffectDefinitionArrayRows`.
- Do not modify `tasks/completed/`.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — the structured Condition and EffectDefinition
                         representations serialize/deserialize correctly;
                         a malformed value is rejected loudly
[x] Integration tests  — migration Up produces the structured columns;
                         Down restores the prior shape; the four provisioned
                         rows hold exactly RELIC_RULES.md §8.5's values;
                         round-trip persist → read → persist is lossless
[ ] Gameplay scenarios — N/A: this task implements no gameplay behavior and
                         evaluates no trigger or effect
```

### Key Edge Cases

- Each of the three `Condition` forms (`MatchCountAtLeast`, `ComboAtLeast`, `HpPercentageBelow`) stores and reads back with its threshold intact
- An effect element carrying all of `effectType`/`valueType`/`value`/`target`/`lifetime` round-trips; an `Undetermined` element stores with no `value`
- A stored value with an unrecognized `effectType`, an unrecognized `valueType`, a missing `value`, or a missing required `target`/`lifetime` fails loudly rather than defaulting
- The `Down` migration produces a valid prior-shape state that the previous model snapshot can read
- No `Relic` ownership row and no `BattleState`/Redis value is affected

---

## Stop Conditions

- If `RELIC_RULES.md` §8 or `ADR-018` does not fully determine a stored value or representation detail this task must encode: STOP per `AGENTS.md` §7 — do not invent a member, a form, or a magnitude
- If the migration would be destructive to existing data, or would require a history rewrite: STOP per `AGENTS.md` §20 (destructive change)
- If the task drifts into implementing trigger evaluation, effect application, or `RelicTriggered` emission: STOP — the Relic stage's task
- If the task would add a member to `GAME_STATE.md`, `PetState`, Redis, or the SignalR wire: STOP — `ADR-018` items 5 and 10 forbid it
- If the task is asked to provision `Burning Curse`: STOP — `RELIC_RULES.md` §6 note 3 / TASK-131 D7
- If a documentation conflict is found between `RELIC_RULES.md` §8 and `DATABASE.md` §1: STOP and report per `AGENTS.md` §4 — do not silently choose one
- If the task is asked to modify a completed or superseded task: STOP — `TASK_LIFECYCLE.md` §3
- If the task is asked to reopen TASK-079, TASK-099, or TASK-102: STOP — terminal `SUPERSEDED`
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Changed Files

Domain (`GameServer.Domain/Relics/`):

- `RelicConditionType.cs` — **new**. The closed §8.1 condition-form set
  (`MatchCountAtLeast` | `ComboAtLeast` | `HpPercentageBelow`).
- `RelicCondition.cs` — **new**. The structured condition (form + integer
  threshold), with its persisted JSON object, its reader, and its loud
  rejection. No Card counterpart — the Card contract has no structured
  condition member.
- `RelicEffectType.cs` — **new**. The closed §8.2 item 1 effect-identity set
  (`ATK` | `Power` | `Crit` | `CardCost`). Relic-specific; deliberately not
  `CardEffectType`.
- `RelicEffectValueType.cs` — **new**. The closed §8.2 item 2 set (`Flat` |
  `Percentage` | `PercentagePoints` | `Undetermined`). Relic-specific;
  `Percentage` is not the Card set's `PercentMaxHp`.
- `RelicEffectTarget.cs` — **new**. §8.3 item 1's target vocabulary (`Pet`).
- `RelicEffectLifetime.cs` — **new**. §8.3 item 2's lifetime vocabulary
  (`Immediate` | `Battle` | `NextAttack`).
- `RelicEffectDefinition.cs` — **new**. One effect element: the
  `effectType`/`valueType`/`value` triple plus `target`/`lifetime`, enforcing
  §8.3's fixed per-`effectType` allowed combination, with its persisted JSON
  object, its reader, its `Undetermined` factory, and its loud rejection.
- `RelicEffectDefinitions.cs` — **new**. The structured `EffectDefinition[]`
  container: non-empty array, stored-order preservation, lossless round trip,
  loud rejection of a non-array/empty array/prose value.
- `RelicDefinition.cs` — **modified**. `Condition` is now
  `RelicCondition?` (structured, optional) and `EffectDefinition` is now
  `RelicEffectDefinitions` (structured array, required). `Trigger` is
  deliberately unchanged as the §3 prose identity (TASK-131 D8).

Infrastructure:

- `Postgres/Configurations/RelicDefinitionConfiguration.cs` — **modified**.
  Both members map through value converters to `jsonb`; `Condition` stays
  nullable and `EffectDefinition` stays NOT NULL; both `HasMaxLength(128)`
  constraints are removed; `Trigger` keeps its bounded prose mapping.
- `Postgres/Migrations/20261003074309_StructureRelicDefinitionStructuredColumns.cs`
  — **new**. The migration: encodes the four provisioned rows from prose to the
  structured shapes (before the type change, so the cast cannot meet prose),
  alters both columns to `jsonb` with explicit `USING` casts, and preserves
  every other row's text verbatim. `Down` restores the
  `character varying(128)` shape and the four rows' exact §6 prose.
- `Postgres/Migrations/20261003074309_StructureRelicDefinitionStructuredColumns.Designer.cs`
  — **new** (EF-scaffolded target model).
- `Postgres/Migrations/GameDbContextModelSnapshot.cs` — **modified**
  (EF-scaffolded; both members now `jsonb`).

Tests:

- `tests/backend/GameServer.Domain.Tests/RelicStructuredContractTests.cs` —
  **new**. The Domain contract: closed vocabularies, §8.3's fixed combinations,
  §8.5's four rows, lossless/deterministic serialization, `Undetermined` with no
  value member, and loud rejection (including the superseded prose form and the
  Card-only `duration`/`scope` members).
- `tests/backend/GameServer.Infrastructure.Tests/RelicStructuredStorageTests.cs`
  — **new**. The persistence boundary: the converter mapping, the dropped prose
  length limit, round trips through the provider, the applied PostgreSQL rows
  and thresholds, the absence of prose/`Undetermined`/Card-only members, the
  deferred `Burning Curse`, and the migration source's scope, reversibility, and
  determinism.
- `tests/backend/GameServer.Infrastructure.Tests/RelicProvisionedContent.cs` —
  **new**. The four §8.5 rows as structured values, held once for both suites.
- `TestRelicEffects.cs` — **new** in the Infrastructure, Application, and Api
  test projects. A valid, non-content structured fixture value, following the
  existing `TestCardEffects` convention.
- Fixture updates for the new member types (no behavioural change):
  `RelicPersistenceTests.cs`, `RelicLoadoutSnapshotTests.cs`,
  `CollectionRepositoryTests.cs`, `PetCardRelicDefinitionPostgresProvisioningTests.cs`,
  `CardEffectDefinitionPersistenceTests.cs` (Infrastructure);
  `PlayerStarterGrantFactoryTests.cs`, `CollectionQueryServiceTests.cs`
  (Application); `TestProvisionedContent.cs`, `BattleResultSmokeTest.cs`,
  `CollectionEndpointTests.cs` (Api).

Docs:

- `docs/02-technical/DATABASE.md` — §1's `RelicDefinition` block, the Relic
  `EffectDefinition`/`Condition` contract note (items 3–4 now record the landed
  migration; a new item 7 records the two stored member shapes and the encoded
  rows), §3's Relic constraint entries, and §5 item 4's status; version → 1.27.
  No rule is restated — `RELIC_RULES.md` §8 remains the canonical owner.
- `docs/01-game-design/RELIC_RULES.md` — §8.6 records that its required separate
  migration has landed and §8.7's `Structured Relic storage` line now reads
  IMPLEMENTED (every other line stays NOT IMPLEMENTED); version → 1.8. No
  trigger, condition, effect, magnitude, threshold, target, or lifetime changed.

### Validation Results

```text
dotnet build GameServer.sln                    PASS (0 warnings, 0 errors)
dotnet test GameServer.sln                     PASS (2403 tests, 0 failed)
  GameServer.Domain.Tests                      PASS (1342 — 1306 baseline + 36 new)
  GameServer.Infrastructure.Tests              PASS (350 — 327 baseline + 23 new)
  GameServer.Application.Tests                 PASS (446 — unchanged)
  GameServer.Api.Tests                         PASS (265 — unchanged)

dotnet ef database update                      PASS (migration applied)
dotnet ef database update <prior migration>    PASS (Down reverses cleanly)
dotnet ef database update                      PASS (Up re-applies identically)
```

**Up → Down → Up was exercised against real PostgreSQL.** After `Down` both
columns are `character varying(128)` again and the four rows hold their exact
§6 prose (`"every 3 Matches"` / `"+5% ATK"`, `"every 4 Matches"` / `"+10 Power"`,
`"Combo ≥ 3"` / `"Increased Crit chance"`, `"HP < 30%"` / `"Heal Card cost −50%"`);
after re-applying `Up` they hold the §8.5 structured values. A non-content
fixture row (`relic_def_smoke`) kept its exact bytes, converted to a JSON string
rather than given an invented structured value — reading it still fails loudly,
which is the documented behaviour.

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed `RelicDefinition.Trigger` unchanged and `Burning Curse` still deferred
- [x] Confirmed no `GAME_STATE.md` / Redis / SignalR contract change
- [x] Confirmed `tasks/completed/` unmodified

Additional scope confirmations:

- [x] No Relic runtime resolver implemented — no `RelicEngine`, `RelicManager`,
      `EffectEngine`, or `ConditionEngine` exists; no trigger is evaluated, no
      condition compared, no effect applied, and no `RelicTriggered` emitted
- [x] No Berserker Core ATK runtime carrier implemented — the `ATK | Pet |
      Battle | Percentage` declaration is stored, and the runtime ATK ambiguity
      this task was told not to resolve remains unresolved
- [x] No Emergency Core CardCost runtime implemented — no `CardCostModifiers[]`,
      no `EffectiveCardCost`, no HP-condition evaluation, no cost calculation
- [x] No Assassin Eye runtime Crit implementation — no
      `NextAttackCritModifiers[]` creation or consumption
- [x] No new table, column, index, constraint, or foreign key; the `Relic`
      ownership table is untouched
- [x] `TASK-131`, `TASK-133`, `TASK-134`, and `ADR-018` unmodified (verified by
      last-write timestamps, all predating this task's edits); no TASK-135 exists
      in the repository
- [x] No gameplay value, magnitude, threshold, target, or lifetime authored or
      altered — every one is transcribed from `RELIC_RULES.md` §8.5
