# TASK-169 â€” Reconcile the `DATABASE.md` `CardDefinition` Content-Set References

<!--
  GEN-TASK EXECUTION MANIFEST â€” DOCUMENTATION RECONCILIATION TASK
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and task files by path and section â€” it does NOT copy
  game rules, formulas, magnitudes, schemas, or contracts.

  THIS TASK DECIDES NOTHING. It corrects `DATABASE.md` statements that the
  completed TASK-168 provisioning made false. Every corrected count, key, and
  name is already owned by `CARD_RULES.md` Â§2/Â§4.1 and already provisioned by
  TASK-168 â€” this task transcribes that existing state into the technical
  document that currently describes the superseded six-row set.

  PROVENANCE: TASK-168 (DONE) provisioned `card-venomous-bloom` and
  `card-earthshaker`, and its Completion Evidence Â§"Reported Items" item 1
  explicitly named this reconciliation as the suggested follow-up. TASK-168 is
  IMMUTABLE and is read here only as historical evidence.

  BOUNDARY: documentation only â€” `docs/02-technical/DATABASE.md` and its version
  header. Zero files under src/. Zero files under tests/. No migration. No new
  task. No ADR. No gameplay decision.
-->

---

## Metadata

```text
Task ID:           TASK-169
Type:              DOCUMENTATION (TASK_TYPES.md Â§2 â€” "Change docs/ content â€”
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md. See "Type
                   classification note".)
Status:            DONE
Risk:              LOWâ€“MEDIUM (TASK_TYPES.md Â§4 â€” DOCUMENTATION baseline is
                   LOWâ€“MEDIUM. MEDIUM rather than LOW because the corrected
                   statements are cross-referenced by other documents' section
                   numbers (the Â§1 Card contract items, the Â§3 constraint lines,
                   and the Â§5 item 4 provisioning record all describe the Card
                   row set), so the wording must stay consistent with the
                   sections that cite them. No source, test, schema, migration,
                   or contract is touched.)
Priority:          MEDIUM (it removes an authoritative-document falsehood about
                   the current MVP content set. It gates no runtime work: the
                   rows already exist and already resolve. It is a consistency
                   debt whose cost grows with each subsequent reader.)
Primary Agent:     review (TASK_TYPES.md Â§2 / AGENT_SELECTION.md Â§1 â€”
                   "Documentation change: Primary Agent Review". This task
                   corrects counts and key references against already-owned
                   content and decides no value, which is the Review Agent's
                   posture. TASK-164 sets the DOCUMENTATION-agent precedent.)
Supporting Agents: persistence (DATABASE.md is the document being corrected and
                   it owns the CardDefinition storage shape and the Â§5 item 4
                   provisioning record â€” consulted to CONFIRM what the document
                   owns versus what the domain documents own),
                   gameplay (CARD_RULES.md Â§2/Â§4.1 is the canonical owner of the
                   defined Card set; consulted to CONFIRM the set and its names,
                   never to author a value. The Card/Pet domain owner)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   backend/persistence-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (5 skills â€” Normal budget, tasks/README.md Â§12)
Dependencies:      TASK-168 (DONE â€” provisioned `card-venomous-bloom`,
                     `card-earthshaker`, `pet-thanh-xa`, and `pet-son-hung`, and
                     its Completion Evidence Â§"Reported Items" item 1 named this
                     reconciliation as the suggested follow-up. IMMUTABLE;
                     read-only),
                   TASK-167 (DONE â€” authored both Signature Skills in
                     `CARD_RULES.md` Â§4.1 and `PET_RULES.md` Â§8 and corrected
                     three dependent stale references, but did NOT touch the
                     six-row Card-set wording this task corrects. IMMUTABLE;
                     read-only),
                   TASK-166 (DONE â€” the Product Owner decisions for both
                     Signature Skills. IMMUTABLE; read-only),
                   TASK-112 (DONE â€” encoded the then-six rows in the ARRAY shape
                     whose Â§1 item 8 wording is corrected here. IMMUTABLE;
                     read-only),
                   TASK-085 (DONE â€” the provisioning migration whose six-row Card
                     count the Â§5 item 4 record still states. IMMUTABLE;
                     read-only),
                   TASK-164 (DONE â€” the DOCUMENTATION-task precedent for type,
                     agent, skills, and evidence shape. IMMUTABLE; read-only)
Blocks:            Nothing at runtime. It gates only the clarity of the
                   authoritative `CardDefinition` record for future readers and
                   for the downstream TASK-115 runtime implementation, which
                   reads `CARD_RULES.md` Â§4.1 but may consult `DATABASE.md` Â§1
                   for the stored shape.
Estimate:          Simple (one docs/ file â€” the Â§1 CardDefinition block, the Â§1
                   Card contract items 7 and 8, the Â§3 constraint-line
                   parenthetical, the Â§5 item 4 row-count record, and the version
                   header. No code, no tests, no schema)
```

**Type classification note.** `DOCUMENTATION`, not `BUG` and not
`GAMEPLAY-CHANGE`. The deliverable is a correction to the *content of an
authoritative technical document*; nothing else changes. `TASK_TYPES.md` Â§3
selects the type by what the deliverable is: here it is a corrected document.
`GAMEPLAY-CHANGE` would be wrong because no rule, magnitude, cost, Element,
duration, or Card is authored, changed, or reinterpreted â€” the entire Card set
was already decided by TASK-166, already authored by TASK-167, and already
provisioned by TASK-168. `BUG` would be defensible (`TASK_TYPES.md` Â§2's BUG row
includes "documentation bugs (docs are stale)"), but `DOCUMENTATION` is the more
specific type: this task's *primary output* is a `docs/` change with no code
change required, which is exactly the DOCUMENTATION definition, and
`documentation/documentation-change.md` Â§1's flow applies end to end. TASK-164 and
TASK-167 set the precedent for choosing the workflow by deliverable.

**This task is NOT a provisioning task.** The two rows already exist
(TASK-168). This task changes no row, inserts no row, deletes no row, and does
not re-run or create a migration.

**This task is NOT a schema task.** TASK-167's Â§"Storage Decision" and TASK-168's
Completion Evidence Â§"Migration Verification" both established that the existing
storage model expresses every authored Card with no schema change. This task
inherits that verification unchanged.

---

## Objective

Reconcile the stale `CardDefinition` content-set references in
`docs/02-technical/DATABASE.md` with the currently authoritative MVP content, so
that the document no longer asserts the superseded six-row Card set
(**3 Basic + 3 Pet Skill**) anywhere it describes the complete defined or
provisioned `CardDefinition` set, and instead records the current set
(**3 Basic + 5 Pet Skill**, the two additional Pet Skill Cards being
**Venomous Bloom** (`card-venomous-bloom`) and **Earthshaker**
(`card-earthshaker`)) â€” using the document's own existing terminology, and
without restating any gameplay rule owned by `CARD_RULES.md` or `PET_RULES.md`.

This task decides nothing. Every corrected count, key, and name it writes is
already owned by `CARD_RULES.md` Â§2/Â§4.1 and already provisioned by TASK-168.

---

## Authoritative References

<!-- Cited by path and section. No rule, magnitude, cost, Element, duration, or schema is copied. -->

**The content set this task must reflect (sole owner â€” transcribe the set, never restate its content):**

- `docs/01-game-design/CARD_RULES.md` **Â§2** â€” the canonical owner of the Basic
  Card set (the three Basic Cards: Strike, Guard, Heal â€” the document's own
  display names) and **Â§4.1** â€” the canonical owner of the Pet Skill Card set.
  Â§4.1's closing statement records that **all five** MVP Pets' Signature Skills
  are authored in that section. Â§4.1 is the sole owner of each Pet Skill Card's
  cost, effect, magnitudes, and Element; **this task transcribes the SET and its
  names only, and copies no magnitude.**
- `docs/01-game-design/CARD_RULES.md` **Â§1** â€” the Card Category vocabulary
  (`Basic` | `PetSkill`) the `CardDefinition` block and Â§3 constraint already
  cite.

**The document being corrected (its ownership boundary must be preserved):**

- `docs/02-technical/DATABASE.md` **Â§1** â€” the `CardDefinition` entity block
  (the stale "3 Basic Cards â€¦ 3 Pet Skill Cards" set parenthetical) and the
  **"Card `EffectDefinition` contract"** note, whose **item 7** (the
  `CardDefinition`-scoped supersession of TASK-082 R2-7) and **item 8** (the
  provisioned-row encoding record) both state a six-row Card set.
- `docs/02-technical/DATABASE.md` **Â§3** â€” the `CardDefinition.Category` /
  `CardDefinition.EffectDefinition.*` constraint lines, one of whose
  parentheticals records the rows that named the effect vocabulary.
- `docs/02-technical/DATABASE.md` **Â§5 item 4** â€” the static-content
  provisioning record, which states the completed TASK-085 migration's
  `CardDefinition` row count as six.
- `docs/02-technical/DATABASE.md` **version header** â€” the version-line
  convention every prior edit to this document has followed.

**The already-provisioned state this task reflects (historical evidence, read-only):**

- `tasks/backlog/TASK-168-provision-thanh-xa-and-son-hung-signature-skill-content-rows.md`
  â€” **Status: DONE.** Its Completion Evidence Â§"Provisioned Rows Summary" records
  the 2 new `CardDefinition` rows and their canonical IDs; Â§"Changed Files"
  records the migration; Â§"Validation Results" records the applied-database
  result. **Â§"Reported Items" item 1 explicitly names this reconciliation as the
  suggested follow-up** and enumerates the stale locations. **IMMUTABLE â€” read
  only, never modified.**
- `tasks/completed/TASK-167-apply-task-166-signature-skill-decisions-to-authoritative-documentation.md`
  â€” **Status: DONE.** Â§"Completion Evidence" Â§"Canonical Content Updated" records
  that Â§4.1 gained both Pet Skill entries, and Â§"Storage Decision" records that
  **no `DATABASE.md` schema, vocabulary, or storage-shape change** was required.
  **IMMUTABLE â€” read only.**

**Governing contract and precedent:**

- `AGENTS.md` **Â§4** (never silently resolve a conflict), **Â§7** (invent
  nothing), **Â§8** (MVP protection), **Â§9** (anti-overengineering), **Â§16**
  (report, do not fix inline), **Â§17** (documentation change rule), **Â§18**
  (architecture change rule), **Â§20** (stop conditions), **Â§21** (output
  discipline), **Â§23** (implement documented intent â€” do not design on the
  project's behalf)
- `.ai/README.md` **Â§6** (source-of-truth rule; `docs/` wins), **Â§13** (stop
  conditions), **Â§14** (MVP protection)
- `.ai/workflow/documentation/documentation-change.md` **Â§1** (flow â€”
  identify the canonical owner, read related documents, check for conflicts,
  update the smallest authoritative source, update dependent references if
  required, validate consistency), **Â§2** (no duplication, ever â€” the governing
  rule for this task's shape), **Â§3** (determining the canonical owner), **Â§4**
  (composition)
- `tasks/TASK_LIFECYCLE.md` **Â§3** (completed tasks are immutable)
- `tasks/README.md` **Â§9** (no business-rule duplication), **Â§12** (skill budget)
- `tasks/completed/TASK-164-apply-battle-history-response-contract-to-api-contracts.md`
  â€” the DOCUMENTATION-task precedent (type, primary agent, skill set, evidence
  shape)

**Scope gate:**

- `docs/00-overview/MVP_SCOPE.md` **Â§1** â€” the "5 Pet Skill Cards (one per Pet)"
  line the corrected set must agree with; **Â§2** â€” nothing OUT is touched;
  **Â§4** â€” unlisted content is FUTURE. This task adds no content: it documents
  content that already exists.

---

## Ownership Determination

Per `.ai/workflow/documentation/documentation-change.md` Â§1/Â§3, the canonical
owner is the document whose stated question the information answers:

```text
CARD_RULES.md Â§2/Â§4.1  = canonical owner of WHICH Cards exist and what each
                         Card's content is (Basic Card set; Pet Skill Card set;
                         every cost, effect, magnitude, and Element). This task
                         reads it and transcribes the SET and its NAMES only.

DATABASE.md            = canonical owner of the STORAGE SHAPE and of the
                         provisioning RECORD. Its Â§1 Card block, its Â§1 Card
                         contract items 7/8, its Â§3 constraint lines, and its
                         Â§5 item 4 record are being corrected here because
                         they state a content-set count that CARD_RULES.md Â§4.1
                         has superseded and TASK-168 has provisioned.

NOT edited â€” and why:
  CARD_RULES.md      = already correct. TASK-167 authored all five Pet Skill
                       Cards there. Editing it would be a second edit to an
                       already-correct canonical owner, and TASK-167's
                       statements are not stale. NO CHANGE.
  PET_RULES.md       = already correct (Â§8 records all five Signature Skills).
                       NO CHANGE.
  GAME_STATE.md      = owns runtime state, not persisted content sets. The Card
                       set is not a battle-state member. NO CHANGE.
  EffectDefinition schema, tables, columns, indexes, constraints, migrations,
  seed code          = unchanged and not owned by this task. NO CHANGE.
```

**The corrected content set** (`CARD_RULES.md` Â§2/Â§4.1; provisioned by TASK-168):

```text
Basic Cards      (CARD_RULES.md Â§2)   : 3
Pet Skill Cards  (CARD_RULES.md Â§4.1) : 5
```

**Canonical key notation to preserve.** `DATABASE.md` Â§1 already fixes the key
value forms `card-<ascii-kebab-case-name>` and `pet-<ascii-kebab-case-name>`
(TASK-082 decision B / R2-9). This task uses that existing notation and the
canonical IDs TASK-168 already provisioned â€” `card-venomous-bloom` and
`card-earthshaker` â€” and invents no key, slug, ID, or field.

---

## Current State

```text
CARD_RULES.md Â§2/Â§4.1  â€” CORRECT (TASK-167)
  Three Basic Cards and five Pet Skill Cards are authored. Â§4.1's closing
  statement records that all five MVP Pets' Signature Skills are authored there.

TASK-168                â€” DONE
  `card-venomous-bloom` and `card-earthshaker` are provisioned, together with
  `pet-thanh-xa` and `pet-son-hung`. TASK-168's Completion Evidence records:
    CardDefinition  : +2 rows (8 total)
    PetDefinition   : +2 rows (5 total)
    RelicDefinition : unchanged (4 rows)
  and records that no docs/ file was changed by that task.

DATABASE.md             â€” STALE (the gap this task closes)
  Written before TASK-168 and never reconciled. Locations that describe the
  complete CardDefinition content set as six rows (3 Basic + 3 Pet Skill):

    Â§1  CardDefinition entity block â€” the set parenthetical
        "the defined rows are the 3 Basic Cards (CARD_RULES.md Â§2) and
         the 3 Pet Skill Cards (CARD_RULES.md Â§4.1)"

    Â§1  Card contract item 7 â€” the R2-7 supersession scope, whose Card half
        describes "the 3 Basic Cards â€¦ the 3 Pet Skill Cards"

    Â§1  Card contract item 8 â€” "The six provisioned rows are encoded in this
        shape â€¦ All six content-defined CardDefinition rows now hold the array
        shape above", with "the three Â§2 Basic Card rows" and "the three Pet
        Skill rows"

    Â§3  the effectType constraint-line parenthetical â€” "(Â§1; CARD_RULES.md Â§2
        for the first three, Â§4.1 for the last three â€” D-2 named all six â€¦)"

    Â§5  item 4 â€” the provisioning record's completed-migration row count:
        "provisions the PetDefinition (3), CardDefinition (6), and
        RelicDefinition (4) content-defined rows"

  This is exactly what TASK-168 Â§"Reported Items" item 1 reported and left
  uncorrected, as instructed.
```

**Why the six-row statements are now false.** Each describes the *complete*
content-defined `CardDefinition` set at the time it was written. `CARD_RULES.md`
Â§4.1 now defines five Pet Skill Cards, and TASK-168 provisioned them, so the
complete set is no longer six rows. Leaving the statements is an authoritative
document asserting a superseded content set (`AGENTS.md` Â§4).

---

## Scope

### In Scope

```text
1. docs/02-technical/DATABASE.md Â§1 â€” the CardDefinition entity block's
   content-set parenthetical: the defined row set becomes 3 Basic Cards
   (CARD_RULES.md Â§2) + 5 Pet Skill Cards (CARD_RULES.md Â§4.1), with the two
   additional Pet Skill Cards named in the document's existing notation, and
   the TASK-082 decision A / R1-1 attribution preserved.

2. docs/02-technical/DATABASE.md Â§1 â€” the Card EffectDefinition contract note,
   item 7 (the CardDefinition-scoped R2-7 supersession) and item 8 (the
   provisioned-row encoding record): the row counts and the Set-scoped wording
   are corrected from six/three-and-three to eight/five, and item 8 explicitly
   records that the two TASK-168 rows are encoded in the same ARRAY shape.

3. docs/02-technical/DATABASE.md Â§3 â€” the CardDefinition constraint-line
   parentheticals whose wording records which rows named the effect vocabulary,
   corrected so they no longer assert a closed six-row Card set.

4. docs/02-technical/DATABASE.md Â§5 item 4 â€” the provisioning record's
   CardDefinition row count, corrected to the currently provisioned set, with
   the TASK-168 migration named alongside the TASK-085 migration that record
   already cites.

5. docs/02-technical/DATABASE.md â€” the version header: one new version entry
   recording the change and its TASK-169 provenance, in the existing style.

6. A consistency re-read of the corrected document against CARD_RULES.md
   Â§2/Â§4.1, PET_RULES.md Â§8, TASK-167, and TASK-168, confirming no duplicate
   definition was introduced and no remaining complete-set statement is stale.
```

### Out of Scope

```text
- No gameplay rule change. No Card, Pet, cost, magnitude, Element, duration,
  Category, or LoadoutCopyLimit value is authored, changed, or reinterpreted.
- No schema change: no table, column, index, constraint, relationship, or
  vocabulary member; no EffectDefinition member added.
- No migration created, modified, re-run, or reverted. No seed code.
- No src/ change. No tests/ change.
- No change to the CardDefinition schema semantics, the PetDefinition schema, or
  the EffectDefinition schema â€” the corrections are to descriptive wording about
  the content set, not to the shape.
- No change to CARD_RULES.md â€” it is already correct (TASK-167).
- No change to PET_RULES.md â€” it is already correct (TASK-167).
- No change to GAME_STATE.md â€” the Card set is not a battle-state member.
- No change to COMBAT_RULES.md, PASSIVE_RULES.md, BOSS_RULES.md, GAME_RULES.md,
  RELIC_RULES.md, or ELEMENT_RULES.md.
- No change to docs/03-decisions/ADR/ â€” no architectural decision is introduced
  or affected.
- No modification of TASK-168, TASK-167, TASK-166, or any file under
  tasks/completed/ (TASK_LIFECYCLE.md Â§3 â€” immutable).
- No new task, and no TASK-115 implementation.
- No reconciliation of the other TASK-168 Â§"Reported Items" entries (fixture
  rows; the shared development database's unrelated fixture rows) â€” each stays
  reported, not fixed here.
- No "improvement" or refactor of unrelated `DATABASE.md` sections, and no
  renumbering of its existing contract items or Â§3 constraint lines.
```

---

## Acceptance Criteria

<!-- Binary and testable. Each is verifiable by reading the corrected document
     and re-reading the authoritative owners. -->

**Complete-set correctness**

- [x] `DATABASE.md` contains **no** statement describing the complete
      content-defined `CardDefinition` set as 3 Basic Cards + 3 Pet Skill Cards
      (or as six rows).
- [x] `DATABASE.md` Â§1's `CardDefinition` block states the defined row set as
      **3 Basic Cards** (`CARD_RULES.md` Â§2) and **5 Pet Skill Cards**
      (`CARD_RULES.md` Â§4.1).
- [x] The two additional Pet Skill Cards are represented by their canonical
      names and IDs, `Venomous Bloom` / `card-venomous-bloom` and `Earthshaker`
      / `card-earthshaker`, using `DATABASE.md`'s existing
      `card-<ascii-kebab-case-name>` notation. No new ID or key form is
      invented.
- [x] The corrected wording does not contradict `CARD_RULES.md` Â§2/Â§4.1,
      `PET_RULES.md` Â§8, TASK-167, or TASK-168.
- [x] The corrected wording agrees with `MVP_SCOPE.md` Â§1's
      "5 Pet Skill Cards (one per Pet)" line.

**Consistency of the dependent statements**

- [x] Â§1 Card contract item 8's provisioned-row record no longer states six
      rows / "three Basic â€¦ three Pet Skill", and records the current
      provisioned set, including that the two TASK-168 rows carry the same ARRAY
      shape as the earlier rows.
- [x] Â§1 Card contract item 7's `CardDefinition`-scoped R2-7 supersession
      statement no longer asserts a six-row Card set, and its statement of
      **which member** R2-7 is superseded for is unchanged.
- [x] Â§3's Card constraint-line parentheticals no longer assert a closed
      six-row Card set. **Every constraint itself is unchanged** â€” the closed
      `effectType` / `valueType` sets, the present-iff conditions, and the
      ordering statement are byte-identical in meaning.
- [x] Â§5 item 4's completed-provisioning record states the currently provisioned
      `CardDefinition` row count and, where it cites the migrations, cites both
      the TASK-085 migration it already names and the TASK-168 migration. The
      provisioning **rules (a) and (b) are unchanged**.
- [x] No contract item, Â§3 constraint line, or Â§1 entity member is renumbered,
      added, or removed.

**No schema and no duplication**

- [x] No table, column, index, constraint, migration, or seed change is stated
      or implied. Field lists, key value forms, member lists, and JSON shapes are
      unchanged.
- [x] No `Element`, `DamageElement`, `BurnElement`, or any other member is added
      to `CardDefinition` or `EffectDefinition`.
- [x] The edit restates no gameplay rule, cost, magnitude, Element, or duration
      owned by `CARD_RULES.md` Â§2/Â§4.1 (`documentation-change.md` Â§2 â€”
      `tasks/README.md` Â§9). The Pet Skill Cards' content is referenced, never
      copied.
- [x] `DATABASE.md`'s stated ownership split is preserved: `CARD_RULES.md`
      Â§2/Â§4.1 owns the effect values; `DATABASE.md` owns the storage shape.

**Isolation**

- [x] Zero files changed under `src/` (byte-identical).
- [x] Zero files changed under `tests/` (byte-identical).
- [x] Zero files changed under `migrations/`; `GameDbContextModelSnapshot.cs`
      byte-identical.
- [x] `CARD_RULES.md`, `PET_RULES.md`, `GAME_STATE.md`, and every other
      `docs/` file are byte-identical.
- [x] `docs/03-decisions/` is byte-identical â€” no ADR created or modified.
- [x] TASK-166, TASK-167, TASK-168, and every file under `tasks/completed/` are
      byte-identical.
- [x] The only changed files are `docs/02-technical/DATABASE.md` and this task
      file.
- [x] All relevant checks pass at the required validation depth
      (`core/validation.md` Â§2).
- [x] Quality review checklist passes (`quality/review.md` Â§1).
- [x] No authoritative rule or contract violated (`AGENTS.md` Â§10 / ADR-001).

---

## Affected Files & Areas

```text
[x] docs/02-technical/DATABASE.md â€” Â§1 CardDefinition block; Â§1 Card
      EffectDefinition contract items 7 and 8; Â§3 Card constraint-line
      parentheticals; Â§5 item 4 provisioning record; version header
[x] tasks/backlog/TASK-169-<this file>.md â€” this task file only
[ ] docs/01-game-design/CARD_RULES.md        â€” NONE (already correct â€” TASK-167)
[ ] docs/01-game-design/PET_RULES.md         â€” NONE (already correct â€” TASK-167)
[ ] docs/02-technical/GAME_STATE.md          â€” NONE
[ ] docs/02-technical/ARCHITECTURE.md        â€” NONE
[ ] docs/02-technical/GAME_EVENTS.md         â€” NONE
[ ] docs/02-technical/API_CONTRACTS.md       â€” NONE
[ ] docs/02-technical/SIGNALR_PROTOCOL.md    â€” NONE
[ ] docs/02-technical/REDIS_STATE.md         â€” NONE
[ ] docs/02-technical/TDD.md                 â€” NONE
[ ] docs/01-game-design/ (all other rule docs) â€” NONE
[ ] docs/00-overview/                        â€” NONE
[ ] docs/03-decisions/ADR/                   â€” NONE (no ADR)
[ ] src/ (backend / frontend)                â€” NONE
[ ] tests/                                   â€” NONE
[ ] migrations/ Â· GameDbContextModelSnapshot.cs â€” NONE
[ ] tasks/backlog/TASK-166-*.md              â€” NONE (immutable; read-only)
[ ] tasks/backlog/TASK-168-*.md              â€” NONE (immutable; read-only)
[ ] tasks/completed/                         â€” NONE (immutable)
```

---

## Implementation Notes

- **Correct wording; author nothing.** Every count, key, and name this task
  writes already exists in `CARD_RULES.md` Â§2/Â§4.1 and in TASK-168's Completion
  Evidence. If a value appears necessary that neither states, that is a STOP
  (`AGENTS.md` Â§7) â€” not a value to author.
- **Change counts and set membership only.** The deliverable is a set
  description, not a content restatement. Do not add a table of the Cards'
  costs, effects, magnitudes, Elements, or durations to `DATABASE.md`; the
  content owner is `CARD_RULES.md` Â§2/Â§4.1 (`documentation-change.md` Â§2).
- **Use the document's own terminology.** `DATABASE.md` already uses
  `CardDefinition`, `Basic`, `PetSkill`, and the
  `card-<ascii-kebab-case-name>` key form. Follow that notation; do not
  introduce a new vocabulary, a new ID form, or a `card-*` list format the
  document does not already use.
- **Follow the version-header convention.** `DATABASE.md`'s header records each
  edit's version, what changed, and which task changed it, and it preserves
  prior entries. Follow that exact style; do not renumber or rewrite prior
  entries.
- **Preserve historical attribution.** Statements attributed to TASK-082,
  TASK-085, TASK-109, TASK-111, TASK-112, and TASK-110 record what those tasks
  did at the time. Correct only the *currently-false current-state* assertions;
  do not rewrite a historical record into a false claim about the past, and do
  not re-status or re-open any completed task.
- **Item 8's array-shape statement is a present-tense claim.** "All six
  content-defined `CardDefinition` rows now hold the array shape above" is
  false today. Correct it to the current provisioned set and record that the two
  TASK-168 rows were written directly in that shape â€” no further encoding
  migration exists or is needed for them.
- **Keep the closed sets closed.** `effectType`'s and `valueType`'s member lists
  are unchanged by this task. If a correction appears to require adding,
  removing, or renaming a member, that is a schema change â€” STOP
  (`AGENTS.md` Â§7, Â§18).
- **Distinguish the rule from its illustration.** Â§5 item 4's rules (a) and (b)
  are binding and unchanged. Only the *current-state record* of the migration and
  its row count is corrected. TASK-167 set this exact precedent when it corrected
  the same item's stale example.
- **No ADR.** No architecture, database strategy, realtime strategy, module
  boundary, state model, schema, or infrastructure changes (`AGENTS.md` Â§18). A
  descriptive count in a technical document is not an architectural decision.
- **Report, do not fix, adjacent gaps** (`AGENTS.md` Â§16). The remaining
  TASK-168 Â§"Reported Items" entries â€” the five non-content fixture rows carrying
  the literal `"effect"` string (TASK-111 Reported Discrepancy 5), and the shared
  development database's unrelated fixture rows â€” are separate items with their
  own scope. Report them again if observed; do not fix them here.
- **Do not implement TASK-115.** This task reconciles documentation only. The
  runtime implementation of the two Skills is a separate, later task.
- **Do not modify TASK-166, TASK-167, TASK-168, or any file under
  `tasks/completed/`** (`TASK_LIFECYCLE.md` Â§3).

---

## Testing Requirements

This task changes no code, so it produces no unit, integration, or gameplay
test. Its verification is a **content-set fidelity check**, a **consistency
check**, a **duplication check**, a **no-schema-change check**, and a
**scope/isolation validation**, at the depth `core/validation.md` requires for a
LOWâ€“MEDIUM-risk DOCUMENTATION change.

### Required Verification

```text
[x] Stale-set sweep        â€” search DATABASE.md for every statement that
                             describes the COMPLETE CardDefinition set; confirm
                             none asserts 3 Basic + 3 Pet Skill or six rows
[x] Corrected-set check    â€” the stated set is 3 Basic Cards (CARD_RULES.md Â§2)
                             + 5 Pet Skill Cards (CARD_RULES.md Â§4.1)
[x] Key/name fidelity      â€” the two additional Cards appear as
                             `card-venomous-bloom` / Venomous Bloom and
                             `card-earthshaker` / Earthshaker, matching TASK-168's
                             provisioned canonical IDs and CARD_RULES.md Â§4.1
[x] Subset-not-complete check â€” confirm no remaining six-row statement describes
                             an intentional SUBSET (e.g. a historical encoded-row
                             record scoped to TASK-085's migration) rather than
                             the complete current set
[x] Item 7 scope check     â€” the R2-7 supersession still names exactly the
                             CardDefinition member, unchanged in scope
[x] Item 8 shape check     â€” the encoding record describes the current
                             provisioned rows and the same ARRAY shape
[x] Â§3 constraint check    â€” the effectType/valueType closed sets, the present-iff
                             conditions, and the ordering statement are
                             UNCHANGED; only wording about which rows named the
                             vocabulary is corrected
[x] Â§5 item 4 rule check   â€” rules (a) and (b) are UNCHANGED; only the
                             current-state record is corrected
[x] No-schema-change check â€” no table, column, index, constraint, vocabulary
                             member, migration, or seed state changed
[x] Cross-document check   â€” the corrected wording agrees with CARD_RULES.md
                             Â§2/Â§4.1, PET_RULES.md Â§8, MVP_SCOPE.md Â§1, TASK-167,
                             and TASK-168
[x] Duplication check      â€” no rule, cost, magnitude, Element, duration, or
                             schema text copied from its owner document
[x] Historical-integrity check â€” statements attributed to completed tasks still
                             record what those tasks did; no completed task is
                             re-statused or re-opened
[x] Consumer check         â€” a future reader can determine the complete
                             CardDefinition set from DATABASE.md without
                             consulting a superseded count
[x] Scope validation       â€” MVP_SCOPE.md Â§1/Â§2 (quality/scope-validation.md)
[x] Isolation verification  â€” src/ (0 files), tests/ (0 files), all other docs/
                             files unchanged, TASK-166/167/168 and
                             tasks/completed/ unchanged
[ ] Unit tests             â€” N/A (no code)
[ ] Integration tests      â€” N/A (no code)
[ ] Gameplay scenarios     â€” N/A (no code)
```

### Key Edge Cases

- **A statement that intentionally describes a subset.** A historical record
  scoped to the TASK-085 migration (e.g. "the six rows that migration inserted")
  may be textually true of that migration. **Correct the current-state
  assertions; keep a genuine historical subset record accurate and scoped** â€” do
  not convert it into a false claim about the past, and do not leave it reading
  as the current complete set. If it is genuinely ambiguous which it is, STOP
  (`AGENTS.md` Â§4) and report both readings rather than guessing.
- **A perceived need for a new key, ID, field, or schema member** â€” STOP. The
  existing model and the canonical IDs TASK-168 provisioned suffice
  (`AGENTS.md` Â§7).
- **A perceived need to change `CARD_RULES.md` to match `DATABASE.md`** â€” STOP.
  `CARD_RULES.md` Â§2/Â§4.1 is the canonical content owner and is already correct;
  `DATABASE.md` is the stale side (`AGENTS.md` Â§17).
- **A newly discovered disagreement between `CARD_RULES.md` and `DATABASE.md`
  about which Cards are defined** â€” STOP per `AGENTS.md` Â§4 and report both
  sources (`file + section`). Do not resolve it by choosing one.
- **A discovery that `DATABASE.md` describes a deliberately different subset**
  rather than the complete MVP set â€” STOP and report; do not "correct" an
  intentional boundary.
- **The Card review finds a further stale count or location this task did not
  enumerate** â€” correct it if it is the same class of stale complete-set
  reference and it is in `DATABASE.md`; otherwise report it as a named follow-up
  (`AGENTS.md` Â§16). Do not widen the change into another document.
- **A perceived need to re-run or create a migration** â€” STOP. TASK-168 already
  provisioned the rows; this task changes no data.

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` Â§20 and `.ai/README.md` Â§13 apply.

- **`DATABASE.md` contains a different authoritative `CardDefinition` set than
  this task records.** **STOP** â€” report both sources (`file + section`); do not
  assume.
- **`CARD_RULES.md` and `DATABASE.md` disagree about which Cards are defined.**
  **STOP** per `AGENTS.md` Â§4 â€” report both sides; do not silently reconcile.
- **The correct number of Cards cannot be determined from authoritative
  documentation.** **STOP** â€” do not infer, count from code, or estimate.
- **Correcting `DATABASE.md` appears to require a schema change.** **STOP** per
  `AGENTS.md` Â§7 and Â§18 â€” report the exact gap and require a separate
  decision/ADR task.
- **Correcting `DATABASE.md` appears to require a gameplay decision.** **STOP** â€”
  the content was decided by TASK-166 and applied by TASK-167; this task decides
  nothing.
- **`DATABASE.md` appears to intentionally describe a subset rather than the
  complete MVP set.** **STOP** â€” report; do not resolve by assumption.
- **A count or key needed for the correction is stated by no authoritative
  document.** **STOP** per `AGENTS.md` Â§7 â€” do not invent, borrow, or estimate.
- **The correction would require modifying TASK-166, TASK-167, TASK-168, or any
  file under `tasks/completed/`.** **STOP** â€” they are immutable
  (`TASK_LIFECYCLE.md` Â§3).
- **A statement attributed to a completed task would have to be falsified to make
  the correction.** **STOP** â€” correct the current-state assertion instead, or
  report the ambiguity.
- **The work exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries.** **STOP & decompose** (`tasks/README.md` Â§13).

---

## Dependencies

```text
TASK-166  (DONE â€” Product Owner decisions for both Signature Skills)
    â†“ applied to the canonical owners by
TASK-167  (DONE â€” CARD_RULES.md Â§4.1 and PET_RULES.md Â§8 author all five Skills)
    â†“ provisioned by
TASK-168  (DONE â€” inserts card-venomous-bloom, card-earthshaker,
                  pet-thanh-xa, pet-son-hung; changes no docs/ file)
    â†“ leaves stale six-row Card-set wording in DATABASE.md, reported as
    â†“ TASK-168 Â§"Reported Items" item 1
TASK-169  (THIS TASK â€” documentation reconciliation of DATABASE.md)
    â†“ after which
REVIEW    (quality/review.md â€” the reconciliation is verified)
    â†“ then
TASK-115  (the separate runtime implementation of the two Skills â€” NOT created
           here, NOT implemented here)
```

**ADR required: NO.** This task changes no architecture, database strategy,
realtime strategy, module boundary, state model, schema, vocabulary, or
infrastructure (`AGENTS.md` Â§18). It corrects descriptive wording about an
already-provisioned content set.

**This task does not create the downstream task, and does not implement it.** In
particular it does not implement or unblock TASK-115.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Documentation

```text
docs/02-technical/DATABASE.md
```

### Current CardDefinition content set

```text
3 Basic Cards + 5 Pet Skill Cards
```

### Added current references

```text
Venomous Bloom  / card-venomous-bloom
Earthshaker     / card-earthshaker
```

### Changed Files

- `docs/02-technical/DATABASE.md` (version 1.30 â†’ 1.31) â€” five corrected
  locations, all of them *complete-set* statements that the TASK-168
  provisioning made false:
  1. **Â§1 `CardDefinition` entity block** â€” the content-set parenthetical:
     "the 3 Basic Cards (`CARD_RULES.md` Â§2) and the **3** Pet Skill Cards
     (`CARD_RULES.md` Â§4.1)" â†’ "â€¦and the **5** Pet Skill Cards (`CARD_RULES.md`
     Â§4.1) â€” the three authored first (TASK-110) plus **Venomous Bloom /
     `card-venomous-bloom`** and **Earthshaker / `card-earthshaker`** (TASK-166
     decisions, authored by TASK-167, provisioned by TASK-168)". The TASK-082
     decision A / R1-1 attribution is preserved.
  2. **Â§1 Card contract item 7** â€” the R2-7 supersession note's third bullet:
     the present-tense "The three Pet Skill Cards carry the effect identity
     Â§4.1 names â€¦" is now past-tense and time-scoped ("The three Pet Skill Cards
     **authored at that time** carry the effect identity Â§4.1 named â€¦"), with the
     current five-Card set and the fact that the two later rows were provisioned
     already encoded added. The supersession's **scope** (that R2-7 is superseded
     for the `CardDefinition` member only) is unchanged, as is the Relic bullet
     and the immutability bullet.
  3. **Â§1 Card contract item 8** â€” "**The six** provisioned rows are encoded in
     this shape â€¦ **All six** content-defined `CardDefinition` rows now hold the
     array shape above â€¦ the three Pet Skill rows store one element per effect
     (two each)" â†’ "**The provisioned rows** are encoded in this shape â€¦
     **All eight** content-defined `CardDefinition` rows now hold the array shape
     above â€” the three Â§2 Basic Cards, **the three Â§4.1 Pet Skill rows authored at
     the time of the encoding**, and **the two Â§4.1 Pet Skill rows added since**
     (Venomous Bloom / `card-venomous-bloom` and Earthshaker /
     `card-earthshaker`)"; the per-effect element counts are now stated per Card
     (two each for Inferno, Tidal Barrier, and Venomous Bloom; one for Iron Fang
     and Earthshaker), and the record adds that the two TASK-168 rows were
     written **directly in that ARRAY shape** at provisioning time, so no further
     encoding migration exists or is needed for them. D-6/D-6b, the
     `card-iron-fang` identity correction, the `jsonb NOT NULL` statement, and
     the "no column or table added / `RelicDefinition` untouched" statements are
     preserved.
  4. **Â§3 `CardDefinition.EffectDefinition.effectType` constraint line** â€” the
     parenthetical "â€” D-2 named all six; `Damage` is distinct from `Power`â€¦" â†’
     "â€” D-2 named all six; **the Â§4.1 Pet Skill set is since five Cards, which
     adds no member** â€” `Damage` is distinct from `Power`â€¦". **The constraint
     itself is byte-identical**: the closed set
     `{Heal, Shield, Power, Damage, Burn, Crit}` is unchanged.
  5. **Â§5 item 4 provisioning record** â€” the completed-migration paragraph. The
     TASK-085 record is preserved verbatim as a historical fact and now explicitly
     scoped ("Those six `CardDefinition` rows were the complete content-defined
     Card set **at the time of that migration**"), followed by the currently
     provisioned set: **`PetDefinition` (5), `CardDefinition` (8),
     `RelicDefinition` (4)**, citing both the TASK-085 migration and **the TASK-168
     migration `20261004055006_ProvisionThanhXaAndSonHungSignatureSkills`**
     (`card-venomous-bloom`, `card-earthshaker`, `pet-thanh-xa`, `pet-son-hung`).
     **Rules (a) and (b) are byte-identical and remain binding.**
  6. **Version header** â€” one new 1.31 entry recording the correction, its
     TASK-169 provenance, and the no-schema-change statement, chaining into the
     preserved 1.30 â€¦ 1.1 entries.
- `tasks/completed/TASK-169-reconcile-database-carddefinition-content-set-references.md`
  â€” this task file only (moved from `tasks/backlog/` on DONE, per
  `TASK_LIFECYCLE.md` Â§4).

### Validation Results

- Stale-set sweep â€” **PASS**. Every statement in `DATABASE.md` describing the
  **complete** content-defined `CardDefinition` set was located and checked. The
  five current-state sites enumerated by this task are corrected. The only
  surviving "six"-row Card statements are **historically scoped** and textually
  true of the moment they describe:
  - the frozen **version-changelog** entries (v1.21 â€¦ v1.30), which record what
    earlier tasks did and are preserved by the header convention â€” including the
    v1.23 entry's "The six provisioned rows are **not yet re-encoded**", which is
    a past statement about the pre-TASK-112 state;
  - **Â§5 item 4**'s TASK-085 migration record, now explicitly scoped to that
    migration;
  - **Â§1 item 7/8**'s "the three Pet Skill rows authored at the time of the
    encoding", now explicitly time-scoped.
  No statement anywhere in the document still presents **3 Basic + 3 Pet Skill**
  (or six rows) as the *current* complete set. Verified by regex sweep for
  `the 3 Pet Skill`, `The six provisioned rows`, `All six content-defined`,
  `six rows`, `CardDefinition (6)`.
- Corrected-set check â€” **PASS**: `DATABASE.md` Â§1's `CardDefinition` block states
  the defined row set as **3 Basic Cards (`CARD_RULES.md` Â§2) + 5 Pet Skill Cards
  (`CARD_RULES.md` Â§4.1)**, and Â§1 item 8 states **All eight** content-defined
  `CardDefinition` rows hold the ARRAY shape.
- Key/name fidelity â€” **PASS**: `card-venomous-bloom` / Venomous Bloom and
  `card-earthshaker` / Earthshaker appear in the document's existing
  `card-<ascii-kebab-case-name>` notation, matching TASK-168's provisioned
  canonical IDs, `CARD_RULES.md` Â§4.1, and the migration source. A sweep of every
  `card-*` token in the document found only the eight canonical IDs (12 distinct
  matches including 4 prose compounds such as "Card-cost"); **no new ID, key
  form, or Card was invented.**
- Â§3 constraint / Â§5 item 4 rule checks â€” **PASS, unchanged**: the closed
  `effectType` / `valueType` sets, the present-iff conditions, and the element
  ordering statement are unaltered (Â§3's only change is descriptive wording in a
  parenthetical); Â§5 item 4's rules (a) and (b) are byte-identical. No contract
  item, Â§3 constraint line, or Â§1 entity member was renumbered, added, or
  removed.
- Historical-integrity check â€” **PASS**: the TASK-085 six-row Card record, the
  TASK-112 encoding record, the TASK-132 Relic re-encode record, and the TASK-082
  / TASK-111 / TASK-110 attributions all still record what those tasks did at the
  time. **TASK-085's historical migration scope was not rewritten** â€” its
  paragraph now names the six rows as the set as of that migration and
  separately records the current eight. No completed task was re-statused or
  re-opened; the version header's prior entries are preserved, not renumbered.
- No-schema-change check â€” **PASS**: no table, column, index, constraint,
  vocabulary member, migration, or seed state is stated or implied to change;
  `CardDefinition`'s field list, key value forms, and `jsonb` shape are
  byte-identical; no `Element` / `DamageElement` / `BurnElement` member was added
  to `CardDefinition` or `EffectDefinition`.
- Cross-document check â€” **PASS**: the corrected wording agrees with
  `CARD_RULES.md` Â§2/Â§4.1 and Â§1, `PET_RULES.md` Â§8, `MVP_SCOPE.md` Â§1
  ("3 Basic Cards (Heal, Shield, Power Charge)" / "5 Pet Skill Cards (one per
  Pet)"), TASK-167, and TASK-168. **No conflict was found** â€” `DATABASE.md` was
  the stale side in every case.
- Duplication check â€” **PASS**: no cost, magnitude, Element, duration, formula, or
  storage-shape text was copied from its owner document. The two new Pet Skill
  Cards are **referenced by name and ID only**; their content stays owned by
  `CARD_RULES.md` Â§4.1. The stated ownership split (`CARD_RULES.md` Â§2/Â§4.1 owns
  the effect values; `DATABASE.md` owns the storage shape) is preserved.
- Consumer check â€” **PASS**: a future reader can determine the complete
  `CardDefinition` set (3 Basic + 5 Pet Skill) and the currently provisioned row
  counts from `DATABASE.md` without consulting a superseded count.
- Scope validation â€” **PASS**: `MVP_SCOPE.md` Â§1/Â§2. This task documents content
  that already exists; it adds no content and touches nothing OUT.
- Isolation verification â€” **PASS**: `git status --porcelain` at completion
  reports exactly `M docs/02-technical/DATABASE.md` plus this task file. Zero
  files changed under `src/`, `tests/`, or `migrations/`;
  `GameDbContextModelSnapshot.cs` unchanged
  (SHA-256 `E7646742AD51BB1EFF4E75CE312FCD330901C11ADBBA2C9EDB55B45168BB6414`);
  no other `docs/` file changed; `docs/03-decisions/` unchanged (no ADR);
  TASK-166, TASK-167, TASK-168 and all of `tasks/completed/` byte-identical.
- Unit / integration / gameplay tests â€” N/A (no code; documentation only).

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic touched
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` Â§1)
- [x] Confirmed no schema change (no table, column, constraint, index,
      vocabulary member, migration, or seed)
- [x] Confirmed no gameplay rule, cost, magnitude, Element, or duration changed
- [x] Confirmed zero files changed under `src/` and `tests/`
- [x] Confirmed TASK-166, TASK-167, TASK-168, and `tasks/completed/` unmodified
- [x] Confirmed no ADR created or modified

### Reported Items (named, not created)

- **No new stale complete-set reference was found in `DATABASE.md`.** The
  document is internally consistent at version 1.31.
- **Pre-existing, NOT introduced here â€” five non-content fixture rows carrying
  the literal `"effect"` string** (TASK-111 Reported Discrepancy 5, re-reported by
  TASK-168 Â§"Reported Items" item 2). Out of this task's scope; requires its own
  task and, if it changes the stored contract, its own review.
- **Pre-existing, NOT introduced here â€” the shared development database carries
  unrelated fixture rows** written by the API integration suites (e.g.
  `smoke_result_pet_def_*`, `heal`, `shield`, `power_charge`, `thanh_xa_skill`,
  `relic_def_smoke`). Reported by TASK-168 Â§"Reported Items" item 3; unaffected by
  this documentation-only task.
- **TASK-168's status-versus-location inconsistency â€” NOT corrected here.**
  TASK-168's own Metadata reads `Status: DONE` and its Completion Evidence records
  DONE with a passed review, but its file still physically resides in
  `tasks/backlog/`; `TASK_LIFECYCLE.md` Â§4 places DONE tasks in `tasks/completed/`.
  It lives in `tasks/backlog/` (not `tasks/completed/`), so it is not protected by
  that section's immutability rule â€” but this task was instructed to leave it
  untouched, so it was read only and is **reported, not moved or edited**
  (`AGENTS.md` Â§16). Suggested follow-up: a lifecycle-placement reconciliation.

### Lifecycle

```text
Status set to:  DONE
File move:      tasks/backlog/ â†’ tasks/completed/ (per TASK_LIFECYCLE.md Â§4,
                IN REVIEW â†’ DONE). Completed tasks are immutable.
```
