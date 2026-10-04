# TASK-168 — Provision the Thanh Xà and Sơn Hùng Signature Skill Content Rows

<!--
  GEN-TASK EXECUTION MANIFEST — CONTENT-PROVISIONING TASK
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and task files by path and section — it does NOT copy
  game rules, formulas, magnitudes, schemas, or contracts.

  PROVENANCE: TASK-166 recorded the Product Owner's decisions for both
  Signature Skills; TASK-167 applied them to the canonical owners
  (CARD_RULES.md §4.1, PET_RULES.md §8). Both are DONE and immutable. The
  remaining gap is CONTENT PROVISIONING — the rows do not exist yet.

  BOUNDARY: this file plus one EF Core data migration (and its Designer),
  the provisioning test updates, and nothing else. Zero gameplay. Zero
  runtime. Zero schema change. Zero SignalR/Redis/frontend change.

  THIS TASK DECIDES NOTHING. Every value it writes is transcribed from
  CARD_RULES.md §4.1, PET_RULES.md §8, and PASSIVE_RULES.md §8. If a value
  is needed that those documents do not state, that is a STOP (AGENTS.md §7).
-->

---

## Metadata

```text
Task ID:           TASK-168
Type:              FEATURE (TASK_TYPES.md §2 — "Implement a documented
                   mechanic, capability, or system that already has a home in
                   docs/ but has not yet been built." Both Signature Skills are
                   now fully documented in CARD_RULES.md §4.1 and their Pet rows
                   in PET_RULES.md §8; the provisioning mechanism
                   (DATABASE.md §5 item 4) is decided and already implemented
                   twice. Nothing new is designed. This mirrors TASK-085's own
                   classification for the identical work on the six existing
                   rows.)
                   See "Type classification note".
Status:            DONE
Risk:              LOW–MEDIUM (TASK_TYPES.md §4 — FEATURE baseline MEDIUM. Here
                   it is LOW-MEDIUM: the task inserts rows into an existing
                   table through an already-decided, already-precedented
                   mechanism, changes no schema, and changes no runtime code
                   path. The only non-trivial consequence is that two existing
                   tests assert these rows are ABSENT and must be updated to
                   assert their documented values instead — a test-content
                   change, not a contract change.)
Priority:          HIGH (the sole remaining unblocking input for the MVP "5 Pets"
                   and "5 Pet Skill Cards (one per Pet)" lines in
                   MVP_SCOPE.md §1. ROADMAP.md §1 Phase 2 requires "All 5 Pets".
                   TASK-167 §"Downstream Dependency" names this task explicitly.)
Primary Agent:     persistence (TASK_TYPES.md §5 / AGENT_SELECTOR convention —
                   "Database change: Primary Agent Persistence". The deliverable
                   is a persistence-content migration over an existing table.
                   This is exactly TASK-085's agent assignment for the same
                   mechanism.)
Supporting Agents: backend (the migration lives in
                   GameServer.Infrastructure/Postgres/Migrations and the rows
                   are read by the repositories the Application layer consumes),
                   testing (the two existing tests that assert these rows are
                   absent must be updated to assert their documented values),
                   review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery,
                   backend/persistence-analysis,
                   testing/test-scenario-generation,
                   quality/scope-validation
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-167 (DONE — applied the TASK-166 decisions to
                     CARD_RULES.md §4.1 and PET_RULES.md §8, named this
                     provisioning follow-up, and recorded the intended row keys
                     card-venomous-bloom / card-earthshaker. IMMUTABLE;
                     read-only),
                   TASK-166 (DONE — the Product Owner decisions D-1/D-2 these
                     rows transcribe. IMMUTABLE; read-only),
                   TASK-112 (DONE — encoded the six existing rows in the ARRAY
                     shape these two rows must also use, and the strict reader
                     that will read them. IMMUTABLE; read-only),
                   TASK-085 (DONE — the provisioning migration
                     `20260929152651_ProvisionPetCardRelicContentDefinitions`,
                     the exact `InsertData`/`DeleteData` form this task follows,
                     and the tests this task must update. IMMUTABLE; read-only),
                   TASK-109 / TASK-111 (DONE — the stored contract whose Row
                     Content Migration table names the six existing rows.
                     IMMUTABLE; read-only),
                   TASK-082 (DONE — decision A: the provisioned/deferred row set
                     rule. IMMUTABLE; read-only),
                   TASK-052 / TASK-053 (DONE — the BossDefinition
                     migration-INSERT precedent. IMMUTABLE; read-only)
Blocks:            ROADMAP.md §1 Phase 2's "All 5 Pets"; the MVP_SCOPE.md §1
                   "5 Pets" and "5 Pet Skill Cards (one per Pet)" lines.
                   NOT blocking any runtime work: this task provisions content
                   only and implements no gameplay.
Estimate:          Simple (4 skills; 1 data-only migration; 4 rows; no schema,
                   no runtime, no contract)
```

**Type classification note.** `FEATURE`, not `GAMEPLAY-CHANGE`. The gameplay
decision half of this work is already complete (TASK-166) and already applied to
the authoritative documentation (TASK-167). What remains is building something
`docs/` already fully specifies — `TASK_TYPES.md` §2's exact `FEATURE`
definition. `GAMEPLAY-CHANGE` would be wrong because no rule, magnitude, cost,
Element, or duration is authored, changed, or reinterpreted by this task.
`DOCUMENTATION` would be wrong because the deliverable is database rows, not
documentation. TASK-085 set this classification precedent for the identical work.

**This task is NOT a runtime task.** It makes the two Skills *available* to the
existing runtime as content. It does not cast them, resolve them, or change any
code path that reads them. `DATABASE.md` §1 item 5 states the boundary: the
stored value is "read, never executed, by this contract".

---

## Objective

Provision the two newly content-defined MVP Pet Signature Skill Cards and their
two owning Pets as `CardDefinition` and `PetDefinition` rows, so that
`card-venomous-bloom`, `card-earthshaker`, `pet-thanh-xa`, and `pet-son-hung`
exist in PostgreSQL with exactly the values `CARD_RULES.md` §4.1, `PET_RULES.md`
§8, and `PASSIVE_RULES.md` §8 document — using the **existing**, already-decided
EF Core migration-INSERT mechanism and the **existing** storage model, with
**no** schema change, **no** new vocabulary, and **no** runtime implementation.

This task decides nothing. Every value it writes is transcribed from an owning
domain document, and it updates the two existing tests that currently assert
these rows are absent so they instead assert the documented values.

---

## Authoritative References

<!-- Cited by path and section. No rule, magnitude, formula, or schema is copied. -->

**The content this task provisions (sole owners — transcribe, never restate or
recompute):**

- `docs/01-game-design/CARD_RULES.md` **§4.1** — the canonical owner of both
  Pet Skill Cards' content: Thanh Xà — **Venomous Bloom** (cost, damage value,
  damage Element, Burn value, Burn Element, Burn duration) and Sơn Hùng —
  **Earthshaker** (cost, damage value, damage Element). Also §4.1's
  "Each effect carries its own Element" statement, which is what makes the
  two-Element Venomous Bloom shape representable, and §4.1's immediate-resolution
  statement. **§1** and **§1 item 5** — `Category` and the `LoadoutCopyLimit`
  value for every CardDefinition this document defines.
- `docs/01-game-design/PET_RULES.md` **§1** (the Pet identity model —
  `PetDefinitionId` is technical identity, `Identity` is display text), **§8**
  (the MVP Pet table: each Pet's Element and Signature Skill, and the
  "Provisioned vs. deferred row set" paragraph that records **all five rows as
  provisionable**).
- `docs/01-game-design/PASSIVE_RULES.md` **§8** — the two Pets' Passive match
  thresholds and the `PassiveId` derivation rule
  (`passive-<ascii-kebab-case-name>` of the owning Pet's documented name).
- `docs/01-game-design/ELEMENT_RULES.md` **§6** — the Pet→Element assignment
  (Thanh Xà = Mộc, Sơn Hùng = Thổ), and **§1.1** / **§5** — that a Skill carries
  an Element and that an Effect (Burn) carries its own, which is why the Element
  needs no storage member.

**The storage contract this task must satisfy (do not restate — satisfy):**

- `docs/02-technical/DATABASE.md` **§1** — the `PetDefinition` and
  `CardDefinition` entity blocks (member lists, key value forms
  `pet-<slug>` / `card-<slug>`, the `SignatureSkillCardId` FK), and the
  **"Card `EffectDefinition` contract" items 1–9** — the ARRAY shape, the
  closed `effectType` / `valueType` sets, the per-effect extra members
  (`duration` on `Burn`), the present-iff rules, and the loud-rejection rule.
- `docs/02-technical/DATABASE.md` **§3** — the `CardDefinition.EffectDefinition.*`
  constraint lines (closed sets, present-iff conditions) and
  `CardDefinition.Category ∈ {Basic, PetSkill}`.
- `docs/02-technical/DATABASE.md` **§5 item 4** — the static-content
  provisioning contract: the mechanism is an EF Core migration INSERT applied
  through `dotnet ef database update`; **rule (a)** only content-defined rows may
  be inserted; **rule (b)** row values are copied from the owning domain
  documents, never invented. Its paragraph already records that the Thanh Xà and
  Sơn Hùng rows are "**provisioned-later**, not content-blocked" and are
  "inserted by the separate provisioning task, not by this rule" — that separate
  task is this one.
- `docs/02-technical/ARCHITECTURE.md` **§2.1**, **§3** — the Infrastructure
  layer's PostgreSQL persistence responsibility.
- `docs/03-decisions/ADR/ADR-006-efcore-postgresql-persistence.md` — EF Core
  PostgreSQL migrations. **ADR-011 / ADR-012 / ADR-016** — the
  Definition-vs-instance ownership separation: `PetDefinition` and
  `CardDefinition` are **static content**, and this task provisions no ownership
  row.
- `docs/03-decisions/ADR/ADR-001` — server authority (unaffected: no runtime
  value is computed here).

**Scope gate:**

- `docs/00-overview/MVP_SCOPE.md` **§1** — "5 Pets (Thanh Xà, Xích Lang,
  Sơn Hùng, Bạch Hổ, Huyền Quy)" and "5 Pet Skill Cards (one per Pet)" are MVP
  **IN**; **§2** — nothing OUT is touched; **§4** — unlisted content is FUTURE.

**Governing contract and precedent:**

- `AGENTS.md` **§4** (never silently resolve a conflict), **§7** (invent no
  value), **§8** (MVP protection), **§9** (anti-overengineering), **§10**
  (server authority), **§12** (domain boundaries), **§16** (report, do not fix
  inline), **§17** (documentation change rule), **§18** (architecture change
  rule), **§20** (stop conditions), **§22** (Definition of Done)
- `.ai/README.md` **§6** (source-of-truth rule), **§13** (stop conditions)
- `tasks/README.md` **§9** (no business-rule duplication), **§12** (skill budget)
- `tasks/completed/TASK-085-provision-mvp-pet-card-relic-content-definitions.md`
  — the **direct precedent**: the same mechanism, the same table set, the same
  test shape, and the same DONE evidence standard
- `tasks/completed/TASK-053-provision-bossdefinition-rows.md` and
  `src/backend/GameServer.Infrastructure/Postgres/Migrations/20260926151112_ProvisionBossDefinitions.cs`
  — the original migration-INSERT precedent
- `tasks/completed/TASK-112-encode-effectdefinition-domain-types-and-rows.md`
  — the ARRAY shape these rows must be written in, and the strict reader that
  will read them
- `tasks/completed/TASK-167-apply-task-166-signature-skill-decisions-to-authoritative-documentation.md`
  — names this provisioning follow-up in its §"Downstream Dependency" and its
  Completion Evidence
- `tasks/completed/TASK-166-collect-product-owner-decisions-thanh-xa-and-son-hung-signature-skills.md`
  — the decision source (D-1 Venomous Bloom, D-2 Earthshaker). **Read-only.**

---

## Current State

```text
PROVISIONED TODAY (DATABASE.md §1; migration
20260929152651_ProvisionPetCardRelicContentDefinitions, TASK-085)
  CardDefinition   6 rows: card-heal, card-shield, card-power-charge,
                           card-inferno, card-tidal-barrier, card-iron-fang
  PetDefinition    3 rows: pet-xich-lang, pet-bach-ho, pet-huyen-quy
  RelicDefinition  4 rows: relic-berserker-core, relic-mana-crystal,
                           relic-assassin-eye, relic-emergency-core

  All six Card rows were subsequently re-encoded into the ARRAY shape by
  migration 20261002090000_EncodeCardEffectDefinitionArrayRows (TASK-112).

ABSENT TODAY — the gap this task closes
  card-venomous-bloom   (Thanh Xà's Signature Skill)   — does not exist
  card-earthshaker      (Sơn Hùng's Signature Skill)   — does not exist
  pet-thanh-xa          (Thanh Xà)                     — does not exist
  pet-son-hung          (Sơn Hùng)                     — does not exist

  Their ABSENCE is the documented state: TASK-085's migration comment records
  them deferred because their SignatureSkillCardId targets did not exist and
  the FK is required. TASK-167 has since authored both Skills, so the FK target
  now exists in docs/ and the rows are provisionable
  (PET_RULES.md §8; DATABASE.md §5 item 4's own paragraph).

WHY THEY WERE BLOCKED, AND WHY THEY NO LONGER ARE
  Before TASK-167: PetDefinition.SignatureSkillCardId is a required FK to
  CardDefinition (DATABASE.md §1/§2), and CARD_RULES.md §4.1 stated no content
  for either Skill, so no valid, non-invented CardDefinition row could be
  created and therefore no PetDefinition row either
  (DATABASE.md §5 item 4 rule (a)).
  After TASK-167: both Skills are content-defined, so both Card rows are
  insertable and both Pet rows' FK targets exist.
```

**Two existing tests currently assert these rows are ABSENT.** They encode the
pre-TASK-167 state and become false once this task lands:

```text
tests/backend/GameServer.Infrastructure.Tests/
    PetCardRelicDefinitionPostgresProvisioningTests.cs
  - Postgres_ShouldHoldNoDeferredContent   — asserts 0 rows for
      'pet-thanh-xa', 'pet-son-hung' and for the guessed Card keys
      'card-thanh-xa-skill', 'card-son-hung-skill'
  - Repositories_ShouldReturnNullForEveryDeferredDefinition — asserts
      GetDefinitionAsync returns null for those same four keys

tests/backend/GameServer.Infrastructure.Tests/
    PetCardRelicDefinitionProvisioningTests.cs
  - MigrationSource_ShouldInsertTheDeferredPetsNowhere — asserts "Thanh Xà",
      "Sơn Hùng", "pet-thanh-xa", "pet-son-hung", "passive-thanh-xa", and
      "passive-son-hung" appear NOWHERE in TASK-085's migration source
```

Both files assert against **TASK-085's migration source**. This task adds a
**separate** migration, so those assertions remain **textually true** for
TASK-085's file — but their recorded *rationale* ("deferred until their
Signature Skills are content-defined") is now false, and two of the assertions
are scoped to the database rather than to one migration's source, so they will
fail against a database carrying this task's migration. Resolving that correctly
is part of this task's scope (see Scope §4 and Testing Requirements).

---

## Scope

### In Scope

```text
1. ONE new EF Core data migration in
   src/backend/GameServer.Infrastructure/Postgres/Migrations/ that:
     - inserts exactly 2 CardDefinition rows (Venomous Bloom, Earthshaker)
       BEFORE the 2 PetDefinition rows that reference them (FK order), and
       exactly 2 PetDefinition rows (Thanh Xà, Sơn Hùng);
     - mirrors each insert with a DeleteData in Down(), removing the Pet rows
       before the Card rows they reference;
     - contains NO schema operation (no CreateTable, AddColumn, DropColumn,
       AlterColumn, CreateIndex, AddForeignKey, etc.);
     - uses migration-level InsertData/DeleteData only — no HasData, no seed
       runner, no startup loader, no JSON pipeline, no upsert.

2. The two CardDefinition rows, in the ARRAY EffectDefinition shape
   DATABASE.md §1 items 1–9 / TASK-112 defines, with every value transcribed
   from CARD_RULES.md §4.1/§1 and every member drawn from the EXISTING closed
   sets.

3. The two PetDefinition rows, with every value transcribed from PET_RULES.md
   §8 and PASSIVE_RULES.md §8.

4. Updating the existing tests whose assertions the new rows falsify, so they
   assert the documented post-provisioning state instead of the pre-TASK-167
   absence — and so no test encodes a false statement about the contract.

5. Verification that the migration applies cleanly, is reversible, satisfies
   every column constraint, resolves the SignatureSkillCardId FK, and reads
   back through the existing repositories and the existing strict
   EffectDefinition reader.
```

### Out of Scope

```text
- No gameplay implementation.
- No runtime implementation.
- No schema change (no table, column, constraint, index, or migration-type
  operation).
- No SignalR changes.
- No Redis changes.
- No frontend changes.
- No new EffectType. No new ValueType. No new Element storage member.
- No new ownership model, table, or foreign-key structure.
- No Player ownership rows (Player, Pet, PlayerUnlockedCard, Relic) — static
  content definitions only (ADR-011/ADR-012/ADR-016).
- No change to the six existing CardDefinition rows or the three existing
  PetDefinition rows (DATABASE.md §5 item 4; AGENTS.md §16).
- No RelicDefinition change. Burning Curse stays deferred
  (RELIC_RULES.md §6 note 3).
- No change to any file under docs/ — TASK-167 already applied the content.
- No modification of TASK-166 or TASK-167, and no modification of any file
  under tasks/completed/ (TASK_LIFECYCLE.md §3 — completed tasks are immutable).
- No widening of the change into the runtime implementation of
  PetSkillCast, BattleState mutation, Crit, Burn execution, the Damage
  Pipeline, GameRuntime, or BattleScene — those belong to a later task.
```

---

## Provisioning Requirements

### 1. CardDefinition rows (exactly 2 new rows)

The `CardDefinition` rows must be inserted before the `PetDefinition` rows that
reference them (`SignatureSkillCardId` FK, `OnDelete(Restrict)`).

```text
CARD ROW 1 — Thanh Xà's Signature Skill
  CardDefinitionId     card-venomous-bloom   (value form card-<ascii-kebab-case
                                              of the documented name> —
                                              DATABASE.md §1)
  Name                 Venomous Bloom        (CARD_RULES.md §4.1)
  Category             PetSkill              (CARD_RULES.md §1)
  PowerCost            from CARD_RULES.md §4.1
  LoadoutCopyLimit     from CARD_RULES.md §1 item 5
  EffectDefinition     a 2-element ARRAY, one element per effect §4.1 states:
                         element 1 — the damage effect:
                           effectType  Damage     (DATABASE.md §1 closed set)
                           valueType   Flat
                           value       from CARD_RULES.md §4.1
                         element 2 — the Burn effect:
                           effectType  Burn
                           valueType   Flat
                           value       from CARD_RULES.md §4.1
                           duration    from CARD_RULES.md §4.1 (Turns — the
                                       Burn element's required extra member,
                                       DATABASE.md §1 item 1 / §3)

CARD ROW 2 — Sơn Hùng's Signature Skill
  CardDefinitionId     card-earthshaker
  Name                 Earthshaker           (CARD_RULES.md §4.1)
  Category             PetSkill              (CARD_RULES.md §1)
  PowerCost            from CARD_RULES.md §4.1
  LoadoutCopyLimit     from CARD_RULES.md §1 item 5
  EffectDefinition     a 1-element ARRAY (§4.1 states exactly one effect):
                         element 1:
                           effectType  Damage
                           valueType   Flat
                           value       from CARD_RULES.md §4.1
                         (no Burn element; §4.1 states no Burn for this Skill)
```

**Element is NOT a member of either row and must not become one.** The damage
Element (Mộc / Thổ) and the Burn Element (Hỏa) are content owned by
`CARD_RULES.md` §4.1 and stated there in prose; `ELEMENT_RULES.md` §1.1/§5
establish that a Skill carries an Element and that an Effect (Burn) carries its
own. This is exactly how `card-inferno`'s Fire Element already reaches the
Damage Pipeline today. **TASK-167 §"Storage Decision" verified this and this task
inherits that verification unchanged** — do not add an `Element`, `DamageElement`,
or `BurnElement` member to `EffectDefinition`, and do not add an Element column
to `CardDefinition`.

### 2. PetDefinition rows (exactly 2 new rows)

```text
PET ROW 1 — Thanh Xà
  PetDefinitionId       pet-thanh-xa         (value form pet-<ascii-kebab-case>;
                                              this key IS the Pet's technical
                                              identity — PET_RULES.md §1,
                                              DATABASE.md §1)
  Identity              Thanh Xà             (display text — PET_RULES.md §1)
  Element               from PET_RULES.md §8 / ELEMENT_RULES.md §6, encoded as
                        the Domain Element enum's numeric value — the same
                        conversion PetDefinitionConfiguration already applies
  PassiveId             from PASSIVE_RULES.md §8's derivation rule
  PassiveThreshold      from PASSIVE_RULES.md §8's table
  SignatureSkillCardId  card-venomous-bloom  (the Card row inserted above)

PET ROW 2 — Sơn Hùng
  PetDefinitionId       pet-son-hung
  Identity              Sơn Hùng
  Element               from PET_RULES.md §8 / ELEMENT_RULES.md §6
  PassiveId             from PASSIVE_RULES.md §8's derivation rule
  PassiveThreshold      from PASSIVE_RULES.md §8's table
  SignatureSkillCardId  card-earthshaker
```

`PetDefinition.PassiveId` and `PassiveThreshold` are properties of
`PetDefinition` (`DATABASE.md` §1). **No `PassiveDefinition` table or row is
created.**

### 3. Provisioning mechanism (unchanged from the decided contract)

```text
Mechanism:   EF Core migration-level InsertData in Up() and mirror DeleteData
             in Down(), applied through `dotnet ef database update`
             (DATABASE.md §5 item 4; TASK-085 precedent).

FORBIDDEN:   HasData / model seed data, a startup seeder, an IHostedService or
             BackgroundService, a JSON content pipeline, an external content
             service, an API-based provisioning path, and
             `INSERT ... ON CONFLICT` / UPSERT.

Idempotency: EF's own migration history (__EFMigrationsHistory) — the migration
             runs once per database. Each table's primary key is the
             database-level guarantee against duplicates. No runtime
             idempotency logic is added.

Determinism: every statement is a fixed insert of a fixed value into a fixed
             primary key. No RNG, no clock, no environment-derived value, no
             data read from elsewhere.
```

### 4. Existing tests to reconcile

Two test files encode the pre-TASK-167 "these rows are absent" state. The
executing agent must determine, per assertion, whether it is now false and
update it to assert the documented post-provisioning state:

```text
tests/backend/GameServer.Infrastructure.Tests/
    PetCardRelicDefinitionPostgresProvisioningTests.cs
  - Postgres_ShouldHoldNoDeferredContent — its Card-key half currently probes
      'card-thanh-xa-skill' / 'card-son-hung-skill', which were GUESSED before
      TASK-167 named the real keys. Update so it no longer asserts these two
      Skills (and their two Pets) are absent, and so it stops encoding a
      non-existent key form. The Relic half (`relic-burning-curse` stays
      deferred) must remain asserted.
  - Repositories_ShouldReturnNullForEveryDeferredDefinition — same correction;
      the repository lookups for the two new keys must now resolve non-null.

tests/backend/GameServer.Infrastructure.Tests/
    PetCardRelicDefinitionProvisioningTests.cs
  - MigrationSource_ShouldInsertTheDeferredPetsNowhere — asserts TASK-085's
      migration source contains no Thanh Xà / Sơn Hùng token. That remains
      TEXTUALLY true of that file, but its recorded rationale ("deferred until
      their Signature Skills are content-defined") is now false. Correct the
      rationale and re-scope the assertion so it states what it actually
      guarantees (TASK-085's row set is unchanged), rather than a deferral
      that no longer holds.
  - Any other assertion in this file whose stated reason is the deferral must
      be corrected the same way.
```

**Do not delete these tests to make them pass** (`AGENTS.md` §15). Each must be
corrected to assert the true contract, and each corrected assertion must be
justified by a doc reference.

### 5. Explicit exclusions

```text
No gameplay implementation.
No runtime implementation.
No schema change.
  No database schema migration beyond the data-only one above.
  No new column.
  No new table.
  No new EffectType.
  No new ValueType.
  No new Element storage member.
No SignalR changes.
No Redis changes.
No frontend changes.
```

---

## Acceptance Criteria

<!-- Binary and testable. Each is verifiable by reading the migration and
     running the existing suites. -->

**Row existence and uniqueness**

- [ ] `card-venomous-bloom` exists in `CardDefinition` exactly once.
- [ ] `card-earthshaker` exists in `CardDefinition` exactly once.
- [ ] `pet-thanh-xa` exists in `PetDefinition` exactly once.
- [ ] `pet-son-hung` exists in `PetDefinition` exactly once.
- [ ] No duplicate provisioning exists: each of the four new canonical IDs
      appears exactly once as an inserted value and exactly once as a `Down`
      delete key, and no other row is inserted or deleted.

**Venomous Bloom content**

- [ ] `card-venomous-bloom` has the `PowerCost` `CARD_RULES.md` §4.1 states.
- [ ] `card-venomous-bloom` has `Category` = `PetSkill`.
- [ ] `card-venomous-bloom` has `LoadoutCopyLimit` = the value `CARD_RULES.md`
      §1 item 5 states for every CardDefinition that document defines.
- [ ] `card-venomous-bloom`'s `EffectDefinition` is a 2-element array whose
      damage element carries `effectType` `Damage`, `valueType` `Flat`, and the
      damage value `CARD_RULES.md` §4.1 states.
- [ ] `card-venomous-bloom`'s damage element's Element is Mộc, as
      `CARD_RULES.md` §4.1 and `ELEMENT_RULES.md` §6 state, and is **not**
      stored as an `EffectDefinition` member.
- [ ] `card-venomous-bloom`'s Burn element carries `effectType` `Burn`,
      `valueType` `Flat`, the Burn value `CARD_RULES.md` §4.1 states, and a
      `duration` equal to the Burn duration §4.1 states.
- [ ] `card-venomous-bloom`'s Burn element's Element is Hỏa, as
      `CARD_RULES.md` §4.1 and `COMBAT_RULES.md` §5.1 state, and is **not**
      stored as an `EffectDefinition` member.
- [ ] The damage Element (Mộc) is NOT applied to the Burn element: the two
      effects are recorded as carrying distinct Elements, per
      `CARD_RULES.md` §4.1's "Each effect carries its own Element" statement.

**Earthshaker content**

- [ ] `card-earthshaker` has the `PowerCost` `CARD_RULES.md` §4.1 states.
- [ ] `card-earthshaker` has `Category` = `PetSkill`.
- [ ] `card-earthshaker` has `LoadoutCopyLimit` = the value `CARD_RULES.md`
      §1 item 5 states.
- [ ] `card-earthshaker`'s `EffectDefinition` is a 1-element array whose single
      element carries `effectType` `Damage`, `valueType` `Flat`, and the damage
      value `CARD_RULES.md` §4.1 states.
- [ ] `card-earthshaker`'s damage Element is Thổ, as `CARD_RULES.md` §4.1 and
      `ELEMENT_RULES.md` §6 state, and is **not** stored as an
      `EffectDefinition` member.
- [ ] `card-earthshaker` has **no** Burn element (`CARD_RULES.md` §4.1 states
      no Burn for this Skill), and therefore no `duration` member.

**Pet rows**

- [ ] `pet-thanh-xa`'s `Identity` is the display text `PET_RULES.md` §8 states,
      and its `Element` equals the Element that section assigns, encoded as the
      Domain `Element` enum's numeric value.
- [ ] `pet-son-hung`'s `Identity` is the display text `PET_RULES.md` §8 states,
      and its `Element` equals the Element that section assigns, encoded the
      same way.
- [ ] `pet-thanh-xa`'s `PassiveId` and `PassiveThreshold` match
      `PASSIVE_RULES.md` §8's derivation rule and table.
- [ ] `pet-son-hung`'s `PassiveId` and `PassiveThreshold` match
      `PASSIVE_RULES.md` §8's derivation rule and table.
- [ ] `pet-thanh-xa.SignatureSkillCardId` = `card-venomous-bloom` and
      `pet-son-hung.SignatureSkillCardId` = `card-earthshaker`, and both resolve
      to an existing `CardDefinition` row with `Category` = `PetSkill` (the FK
      contract, `DATABASE.md` §2).

**Non-regression — 100% of existing content preserved**

- [ ] The three existing Pet Skill Cards (`card-inferno`, `card-tidal-barrier`,
      `card-iron-fang`) are byte-identical in every value.
- [ ] The three existing Basic Cards (`card-heal`, `card-shield`,
      `card-power-charge`) are byte-identical in every value.
- [ ] The three existing Pet rows (`pet-xich-lang`, `pet-bach-ho`,
      `pet-huyen-quy`) are byte-identical in every value.
- [ ] The four existing Relic rows are byte-identical in every value, and
      Burning Curse remains unprovisioned (`RELIC_RULES.md` §6 note 3).
- [ ] No existing `CardDefinition`/`PetDefinition`/`RelicDefinition` row is
      updated, deleted, or re-encoded by this task's migration.

**Mechanism and isolation**

- [ ] The migration is data-only: it contains no `CreateTable`, `DropTable`,
      `AddColumn`, `DropColumn`, `AlterColumn`, `CreateIndex`, `DropIndex`,
      `AddForeignKey`, `DropForeignKey`, `AddPrimaryKey`, `DropPrimaryKey`,
      `AddCheckConstraint`, `DropCheckConstraint`, `RenameColumn`, or
      `RenameTable` operation.
- [ ] **No schema migration is introduced.** No new column, table, constraint,
      index, `EffectType`, `ValueType`, or Element storage member.
- [ ] The migration contains no `HasData`, `UseSeeding`, `UseAsyncSeeding`,
      `IHostedService`, `BackgroundService`, `Seeder`, `SeedAsync`, or JSON
      pipeline. `GameDbContextModelSnapshot.cs` is unchanged (EF generates no
      schema difference for a pure data migration).
- [ ] The `CardDefinition` rows are inserted before the `PetDefinition` rows in
      `Up`; the `PetDefinition` rows are deleted before the `CardDefinition`
      rows in `Down`.
- [ ] Both new Card rows' `EffectDefinition` payloads are well-formed under
      `DATABASE.md` §1 items 1–9 and deserialize through the existing strict
      `CardEffectDefinitions` reader without rejection (no default, no fallback,
      no silent no-op — §1 item 6).
- [ ] Zero files changed under `src/frontend/`; zero client-authoritative
      gameplay logic introduced (`AGENTS.md` §10).
- [ ] Zero files changed under `docs/`. **TASK-166 and TASK-167 are byte-identical**,
      and no file under `tasks/completed/` is modified.
- [ ] The two existing test files named in Scope §4 no longer assert these
      rows are absent, and each corrected assertion cites the document that
      justifies it. No test was deleted to make the suite pass.
- [ ] All relevant tests pass at the required validation depth
      (`core/validation.md` §2).
- [ ] Quality review checklist passes (`quality/review.md` §1).
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[x] src/backend/GameServer.Infrastructure/Postgres/Migrations/
      ├── <Timestamp>_ProvisionThanhXaAndSonHungSignatureSkills.cs
      │     — new data-only migration: 2 CardDefinition inserts then 2
      │       PetDefinition inserts in Up; the mirror 4 DeleteData calls
      │       (Pets before Cards) in Down
      └── <Timestamp>_ProvisionThanhXaAndSonHungSignatureSkills.Designer.cs
            — migration metadata; target model identical to the previous
              migration's model
    GameDbContextModelSnapshot.cs — modify ONLY if EF Core genuinely requires
      a snapshot change for the generated migration. A pure data migration
      must not change it; if EF generates a diff, STOP and report.
[x] tests/backend/GameServer.Infrastructure.Tests/
      — the two files named in Scope §4 (corrected assertions), plus
        value-level assertions for the four new rows following the existing
        provisioning-test shape
[ ] src/backend/GameServer.Domain/            — NONE
[ ] src/backend/GameServer.Application/       — NONE
[ ] src/backend/GameServer.Api/               — NONE
[ ] src/frontend/                             — NONE
[ ] docs/                                     — NONE (TASK-167 already applied
      the content; this task writes rows, not documentation)
[ ] docs/03-decisions/ADR/                    — NONE (no ADR required)
[x] tasks/backlog/TASK-168-<this file>.md     — this task file only
[ ] tasks/backlog/TASK-166-*.md               — NONE (immutable; read-only)
[ ] tasks/completed/                          — NONE (immutable)
```

---

## Implementation Notes

- **Transcribe, never decide.** Every number this task writes comes from
  `CARD_RULES.md` §4.1, `PET_RULES.md` §8, or `PASSIVE_RULES.md` §8. If the
  migration appears to need a value none of those documents states, that is a
  STOP (`AGENTS.md` §7) — not a value to author.
- **Follow the TASK-085 migration form.** Mirror
  `20260929152651_ProvisionPetCardRelicContentDefinitions.cs`: one
  `migrationBuilder.InsertData(...)` per row naming the table, the explicit
  column array, and the value array; one `migrationBuilder.DeleteData(...)` per
  row keyed by the canonical primary key. Do not introduce a second migration
  style and do not rewrite the existing migration.
- **Insert Cards before Pets; delete Pets before Cards.** The
  `SignatureSkillCardId` FK is `OnDelete(Restrict)` (`DATABASE.md` §2), so the
  reverse order in either direction is rejected by the database.
- **Write the ARRAY shape** `DATABASE.md` §1 items 1–9 defines. A one-effect
  Card stores a one-element array (`Earthshaker`); a two-effect Card stores a
  two-element array (`Venomous Bloom`). Element order within the array is not
  semantic (§1 item 3); write each row's elements in the order `CARD_RULES.md`
  §4.1 states its effects.
- **The `Burn` element requires its `duration` member** (present-iff,
  `DATABASE.md` §1 item 1 / §3). Omitting it is a contract violation and the
  strict reader rejects the row loudly (§1 item 6).
- **Do not add an Element member.** See Provisioning Requirements §1's closing
  paragraph. `CARD_RULES.md` §4.1 owns the Elements in prose, exactly as it
  already does for Inferno's Fire Element; `ELEMENT_RULES.md` §1.1/§5 make the
  per-effect distinction meaningful without any storage member.
- **Match the existing enum encodings.** `Element` is stored as the Domain
  `Element` enum's numeric value; `Category` as the Domain `CardCategory` enum's
  numeric value. Read the existing migration and
  `PetDefinitionConfiguration.cs` / `CardDefinitionConfiguration.cs` for the
  conversions actually applied — do not hand-guess a number.
- **Use the canonical key forms.** `card-<ascii-kebab-case-name>` and
  `pet-<ascii-kebab-case-name>` (`DATABASE.md` §1; TASK-082 decision B).
  TASK-167's Completion Evidence already names `card-venomous-bloom` and
  `card-earthshaker`; use those. The `PassiveId` values derive from
  `PASSIVE_RULES.md` §8's rule.
- **Preserve the non-ASCII characters verbatim.** `Identity` carries "Thanh Xà"
  and "Sơn Hùng" with their diacritics, and `card-shield`'s existing neighbours
  already carry "Hỏa", "Thổ", and "≥"/"−". A transliterated value is an invented
  value.
- **Do not touch the existing rows.** This migration inserts and deletes only
  its own four rows. It is not a re-encode and must not be merged into
  `20261002090000_EncodeCardEffectDefinitionArrayRows` or into TASK-085's
  migration — completed migrations are historical records.
- **Reconcile the two existing test files honestly.** Their assertions encode
  the pre-TASK-167 deferral. Correct each to assert the documented
  post-provisioning state and cite the document that justifies it. Do not
  delete an assertion to make the suite green (`AGENTS.md` §15).
- **No ADR.** Persistence strategy, storage technology, the authoritative model,
  module boundaries, and the schema are all unchanged (`ADR-006` governs; the
  tables and columns already exist; no vocabulary is extended). If implementation
  appears to require an architectural decision, STOP per `AGENTS.md` §18.
- **Report, do not fix, adjacent gaps** (`AGENTS.md` §16). `DATABASE.md` §1's
  `CardDefinition` block still describes "the 3 Basic Cards … and the 3 Pet Skill
  Cards", and its §1 item 8, §3, and §5 item 4 paragraphs describe a six-row
  Card set — all written before TASK-168. This task changes no `docs/` file, so
  that staleness is **reported** as a named follow-up, not corrected here.
  Likewise, the five non-content fixture rows carrying the literal `"effect"`
  string (TASK-111 Reported Discrepancy 5) remain out of scope.
- **Do not modify TASK-166, TASK-167, or any file under `tasks/completed/`.**

---

## Testing / Validation

This task provisions static content and implements no gameplay behavior, so its
verification is a **row-content fidelity check**, a **migration-shape check**, an
**applied-database check**, and a **non-regression check** — using the existing
provisioning test mechanism (`Database.md` §5 item 4's mechanism leaves the
migration source as the authoritative record; the InMemory provider cannot apply
a migration).

```text
[ ] Migration source — exactly 4 InsertData in Up and 4 mirror DeleteData in
    Down; each of the four new canonical IDs appears exactly once as an
    inserted value and once as a DeleteData key.
[ ] Migration source — no schema operation of any kind (the TASK-085
    forbidden-token list).
[ ] Migration source — no HasData / seeder / hosted service / JSON pipeline
    (the TASK-085 forbidden-token list).
[ ] Migration source — the two CardDefinition inserts precede the two
    PetDefinition inserts in Up; the two PetDefinition deletes precede the two
    CardDefinition deletes in Down.
[ ] Migration source — the two new Card rows' values match CARD_RULES.md §4.1
    and §1 exactly, in the array shape; the two new Pet rows' values match
    PET_RULES.md §8 and PASSIVE_RULES.md §8 exactly.
[ ] Migration source — the enum-encoded integers equal the Domain enum members
    the rule documents name (never the enum NAME, which would not convert).
[ ] Migration source — no other row is inserted or deleted; the existing six
    Cards, three Pets, and four Relics are not named.
[ ] Applied database (where reachable) — the four rows exist with their exact
    documented values; the SignatureSkillCardId FK resolves to a
    Category = PetSkill row; whole-table counts reflect the new rows.
[ ] Reader round-trip — both new Card rows deserialize through the existing
    strict CardEffectDefinitions reader with no rejection, and the
    Burn element's required duration member is present.
[ ] Repository lookups — IPetRepository.GetDefinitionAsync resolves
    pet-thanh-xa and pet-son-hung; ICardRepository.GetDefinitionAsync resolves
    card-venomous-bloom and card-earthshaker.
[ ] Migration applies cleanly (`dotnet ef database update`) on an isolated
    clean/test database carrying all prior migrations.
[ ] Migration rollback — reverting this migration removes exactly the four new
    rows with no FK violation, and leaves every other row untouched.
[ ] Non-regression — the existing 13 content rows and the four Relic rows are
    unchanged; `relic-burning-curse` and `Burning Curse` remain absent.
[ ] Corrected assertions — the two test files named in Scope §4 no longer
    assert these rows are absent, and no test was deleted.
[ ] Full backend suite (`dotnet test`) — green, with any pre-existing
    environmental failure identified as such and proven independent of this
    task.
```

**Key edge cases**

- **A value none of the referenced documents states** — STOP; do not author it
  (`AGENTS.md` §7).
- **A `Burn` element without `duration`** — a contract violation; the strict
  reader rejects it loudly (`DATABASE.md` §1 item 6). Do not omit it, and do not
  default it.
- **A perceived need for an Element / DamageElement / BurnElement member** —
  STOP. TASK-167's Storage Decision already established that the existing model
  expresses both Skills; if repository inspection now disproves that, report the
  storage-contract conflict rather than inventing a schema change.
- **A perceived need for a new `EffectType` or `ValueType`** — STOP. Both Skills
  use only `Damage`, `Burn`, and `Flat` (`DATABASE.md` §1's closed sets).
- **EF generating a schema diff for the new migration** — STOP and report; a
  pure data migration must produce none.
- **An existing test that fails because it asserts the old deferral** — correct
  the assertion to the documented post-provisioning state and cite the document;
  never delete it and never weaken the suite.
- **A migration-order conflict with an environment's already-applied
  migrations** — report the environment state; do not renumber or rewrite a
  completed migration.

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 apply.

- **Existing provisioning ownership is ambiguous.** **STOP** — do not guess.
- **The `CardDefinition`/`PetDefinition` relationship is ambiguous.** **STOP** —
  `DATABASE.md` §1/§2 defines it; if it does not, report rather than invent.
- **The Element cannot be represented by the existing provisioning model.**
  **STOP.** TASK-167 §"Storage Decision" recorded it can; if repository
  inspection now proves otherwise, **report the storage-contract conflict** —
  do not add an Element member, column, or table.
- **A schema change appears necessary.** **STOP** per `AGENTS.md` §7 and §18 —
  report the exact gap and require a separate decision/ADR task.
- **The authoritative docs conflict with the completed TASK-167 contract.**
  **STOP** per `AGENTS.md` §4 — report both sources (`file + section`); do not
  silently reconcile.
- **Provisioning requires a new gameplay decision.** **STOP** — this task
  transcribes decided content; it decides nothing.
- **Provisioning requires changing TASK-166 or TASK-167.** **STOP** — both are
  immutable (`TASK_LIFECYCLE.md` §3).
- **A required value is not stated by any authoritative document.** **STOP** per
  `AGENTS.md` §7 — do not invent, borrow, estimate, default, or reuse another
  Card's value.
- **Existing provisioning conventions are insufficient to specify the task
  deterministically.** **STOP** and report the gap.
- **Making a corrected test pass would require weakening its assertion to
  "whatever the code does".** **STOP** — classify per `AGENTS.md` §15 instead.
- **The work exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries.** **STOP & decompose** (`tasks/README.md` §13).

---

## Dependencies

```text
TASK-166  (DONE — Product Owner decisions D-1/D-2)
    ↓ applied by
TASK-167  (DONE — CARD_RULES.md §4.1 and PET_RULES.md §8 author both Skills)
    ↓ provisioned by
TASK-168  (THIS TASK)
    inserts card-venomous-bloom, card-earthshaker, pet-thanh-xa, pet-son-hung
        ↓ makes the content available to
EXISTING RUNTIME  (unchanged by this task — no code path is modified)
        ↓ a later task may
IMPLEMENT / VERIFY the two Skills' gameplay behavior
    (PetSkillCast runtime, BattleState mutation, Crit, Burn execution, Damage
     Pipeline changes, Redis, SignalR, GameRuntime, BattleScene — ALL out of
     scope here)
```

**ADR required: NO.** This task changes no architecture, no database strategy, no
realtime strategy, no module boundary, no state model, no schema, and no
infrastructure (`AGENTS.md` §18). It inserts content rows into existing tables
through an already-decided mechanism.

**This task does not create the downstream implementation task and does not
implement it.**

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Provisioned Rows Summary

- `CardDefinition`: 2 rows
  - `card-venomous-bloom` — "Venomous Bloom", `Category = PetSkill`, `PowerCost`
    80, `LoadoutCopyLimit` 1, `EffectDefinition` =
    `[{"effectType":"Damage","valueType":"Flat","value":80},{"effectType":"Burn","valueType":"Flat","value":25,"duration":2}]`
  - `card-earthshaker` — "Earthshaker", `Category = PetSkill`, `PowerCost` 100,
    `LoadoutCopyLimit` 1, `EffectDefinition` =
    `[{"effectType":"Damage","valueType":"Flat","value":150}]`
- `PetDefinition`: 2 rows
  - `pet-thanh-xa` — "Thanh Xà", `Element` Mộc (0), `passive-thanh-xa`,
    threshold 7, `SignatureSkillCardId` = `card-venomous-bloom`
  - `pet-son-hung` — "Sơn Hùng", `Element` Thổ (1), `passive-son-hung`,
    threshold 5, `SignatureSkillCardId` = `card-earthshaker`
- Total: 4 new content-definition rows. `RelicDefinition` unchanged (4 rows);
  `Burning Curse` remains deferred.

### Changed Files

- `src/backend/GameServer.Infrastructure/Postgres/Migrations/20261004055006_ProvisionThanhXaAndSonHungSignatureSkills.cs` — new data-only migration: 2 `CardDefinition` + 2 `PetDefinition` `InsertData` in `Up` (Cards before Pets, FK-safe); the mirror 4 `DeleteData` in `Down` (Pets before Cards).
- `src/backend/GameServer.Infrastructure/Postgres/Migrations/20261004055006_ProvisionThanhXaAndSonHungSignatureSkills.Designer.cs` — migration metadata. Its target model is byte-identical to the previous migration's Designer; only the `[Migration(...)]` id and the class name differ.
- `tests/backend/GameServer.Infrastructure.Tests/ThanhXaAndSonHungSignatureSkillProvisioningTests.cs` — new provider-independent tests over this migration's source (row set, canonical IDs, transcribed values, ARRAY shape, no Element member, FK-safe order, `Down` mirror, no schema operation, no seed mechanism, untouched pre-existing row set).
- `tests/backend/GameServer.Infrastructure.Tests/PetCardRelicDefinitionProvisioningTests.cs` — corrected the two assertions whose stated rationale was the pre-TASK-167 deferral (`MigrationSource_ShouldInsertTheDeferredPetsNowhere` → `MigrationSource_ShouldInsertOnlyItsOwnDocumentedThanhXaAndSonHungRows`; `MigrationSource_ShouldInsertNoAdditionalOrDeferredCard` → `...NoAdditionalOrInventedCard`). Both remain scoped to TASK-085's own file.
- `tests/backend/GameServer.Infrastructure.Tests/PetCardRelicDefinitionPostgresProvisioningTests.cs` — corrected the applied-database assertions that encoded the deferral, and extended the canonical row set to the now-provisioned state (8 Cards / 5 Pets / 4 Relics).

`GameDbContextModelSnapshot.cs` was **not** modified: EF scaffolded the migration with **empty** `Up`/`Down` bodies, generating no schema difference and confirming the migration is pure data.

### Migration Verification

- **Data-only.** Zero occurrences of `CreateTable`, `DropTable`, `AddColumn`, `DropColumn`, `AlterColumn`, `CreateIndex`, `DropIndex`, `AddForeignKey`, `DropForeignKey`, `AddPrimaryKey`, `DropPrimaryKey`, `CreateSequence`, `DropSequence`, `RenameColumn`, `RenameTable`, `AddCheckConstraint`, `DropCheckConstraint`, or `migrationBuilder.Sql(`. No `UpdateData`.
- **No schema diff.** `dotnet ef migrations add` produced empty `Up`/`Down`; `GameDbContextModelSnapshot.cs` unchanged; the new Designer's model is byte-identical to the previous migration's Designer.
- **No model seed / runtime seeder.** No executable `HasData`, `UseSeeding`, `UseAsyncSeeding`, `IHostedService`, `BackgroundService`, `Seeder`, `SeedAsync`, `JsonSerializer`, or `.json`. (The one `HasData` token is inside an XML-doc comment.)
- **Up/Down ordering verified positionally:** `Up` = Card, Card, Pet, Pet; `Down` = Pet, Pet, Card, Card.
- **Applied cleanly** (`dotnet ef database update`) onto the local PostgreSQL carrying all prior migrations.
- **Rollback verified** (`dotnet ef database update 20261003074309_StructureRelicDefinitionStructuredColumns`): reverting removed exactly the four new rows (CardDefinition 13→11, PetDefinition 79→77, RelicDefinition 5→5) with **no FK violation** and every other row untouched. Re-applying reproduced the identical state.
- **Reader round-trip:** both new Card rows read back through the existing strict `CardEffectDefinitions` reader with no rejection, including the Burn element's required `duration` member.

### Validation Results

- Migration source assertions — PASS (20 new tests in `ThanhXaAndSonHungSignatureSkillProvisioningTests`).
- Applied-database assertions — PASS (11 tests, all executed against live PostgreSQL, none skipped).
- Corrected assertions — PASS: the two files named in Scope §4 no longer assert these rows are absent, and **no test was deleted** (7 rewritten → 9, net +2).
- Repository lookups — PASS (`IPetRepository.GetDefinitionAsync` resolves `pet-thanh-xa` / `pet-son-hung`; `ICardRepository.GetDefinitionAsync` resolves `card-venomous-bloom` / `card-earthshaker`; `relic-burning-curse` and the pre-TASK-167 guessed keys `card-thanh-xa-skill` / `card-son-hung-skill` still resolve to null).
- Non-regression — PASS: all six existing Card rows, three Pet rows, and four Relic rows read back byte-identical; `relic-burning-curse` remains absent.
- Full backend suite (`dotnet test src/backend/GameServer.sln`) — PASS, run twice with identical results: **2699 tests, 0 failures** (Domain 1469, Application 517, Infrastructure 393, Api 320). Baseline before this task was 2678 (Domain 1469, Application 517, Infrastructure 372, Api 320), so **+21 tests**, 0 failures, and no pre-existing test changed outcome.
- Pre-existing environmental warnings (not caused by this task, reproduced on the untouched baseline): `MSB3277` `Microsoft.EntityFrameworkCore.Relational` 10.0.4-vs-10.0.12 conflict, plus pre-existing `xUnit20xx`/`CS8602` analyzer warnings in unrelated test files. `dotnet test` builds and runs all four suites cleanly.

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed no schema change (no table, column, constraint, index, or
      migration-type operation)
- [x] Confirmed no gameplay or runtime implementation
- [x] Confirmed no SignalR, Redis, or frontend change
- [x] Confirmed TASK-166, TASK-167, and `tasks/completed/` unmodified
- [x] Confirmed the existing 13 content rows unchanged

SHA-256 of the two immutable inputs, verified identical before and after this
task: TASK-166 = `2DF66892ACB825D114CFFF58F41713A56BE48082A1E95D00FC47D0E1111C68E3`;
TASK-167 = `52C319939965651E1E6644B3308BCF6673C83FFBD08EE7E11B3FFC6415BDAD0D`.

### Reported Items (named, not created)

1. **`DATABASE.md` stale card-row-count wording — NOT corrected here, as
   instructed.** `§1`'s `CardDefinition` block still describes "the 3 Basic Cards
   … and the 3 Pet Skill Cards", and `§1` item 8, `§3`, and `§5` item 4 still
   describe a six-row Card set. All were written before TASK-168 and are now
   stale. This task changed no `docs/` file, so the staleness is reported rather
   than fixed. Suggested follow-up: a documentation-synchronization task.
2. **Five non-content fixture rows carrying the literal `"effect"` string**
   (TASK-111 Reported Discrepancy 5) remain out of scope and untouched.
3. **Shared development database carries unrelated fixture rows** written by the
   API integration suites (e.g. `smoke_result_pet_def_*`, `heal`, `shield`,
   `power_charge`, `thanh_xa_skill`, `relic_def_smoke`). This is why the applied
   assertions are scoped to a closed set of canonical definition keys rather than
   to a `card-`/`pet-`/`relic-` prefix. Pre-existing condition, not introduced
   here.
