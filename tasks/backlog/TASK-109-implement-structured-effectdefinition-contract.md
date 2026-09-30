# TASK-109 — Implement the Structured `EffectDefinition` Contract (`CardDefinition` Runtime Support)

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, schemas, or contracts.

  THIS TASK IMPLEMENTS TASK-108's APPROVED CONTRACT AND SUPERSEDES TASK-082
  R2-7 FOR CARD EffectDefinition. It implements NO gameplay execution.

  WHAT IT IS: the contract migration + runtime data support that TASK-108
  D-1/D-2 approved — a structured, runtime-readable Card `EffectDefinition`,
  its persistence, its serialization/deserialization, and its validation.
  WHAT IT IS NOT: CardCast, an effect resolver's gameplay behavior, or any
  call to ApplyHeal / ApplyShield / ApplyPower. Those are TASK-107's.

  WHY THIS IS A CONTRACT MIGRATION, NOT A FEATURE. TASK-082 R2-7 (DONE) ruled
  the opposite of TASK-108 D-1: it required `EffectDefinition` to be the owning
  document's VERBATIM PROSE and explicitly forbade "an `effect-{slug}`
  vocabulary or any new effect-reference identifier system". TASK-108's
  Product Owner decision requires exactly such a structured contract for Cards.
  The two cannot both hold. This task applies the supersession DELIBERATELY and
  IN THE OPEN — it does not silently edit DATABASE.md, and it does not touch
  TASK-082 (DONE tasks are immutable, TASK_LIFECYCLE.md §3).

  SUPERSESSION SCOPE — CARD ONLY. TASK-082 R2-7 governed BOTH CardDefinition
  and RelicDefinition `EffectDefinition` (DATABASE.md §1, both blocks). There is
  no Product Owner decision about Relic effects, and Relic trigger/effect
  resolution is Phase 2 ("No Relics yet", ROADMAP.md Phase 1 line 36, Phase 2
  line 46). This task therefore supersedes R2-7 FOR CARD EffectDefinition ONLY
  and leaves the RelicDefinition half of §1 exactly as R2-7 wrote it. See
  "Supersession Scope Decision".

  THE BALANCE BOUNDARY. TASK-108's illustrative JSON carried example numbers
  (Heal 30, Power 5) that DIFFER from the provisioned CARD_RULES.md §2 values
  (Heal 20, Power Charge 25). This task encodes ONLY the values CARD_RULES.md
  §2 states, and changes no balance value. The examples are shapes, not content.

  BOUNDARY: no CardCast, no resolver gameplay, no SignalR, no BattleEvent, no
  Shield/Heal/Power semantic change, no PetSkillCast, no Relics, no ADR.
-->

---

## Metadata

```text
Task ID:           TASK-109
Type:              FEATURE (TASK_TYPES.md §2 — "Implement a documented
                   mechanic, capability, or system that already has a home in
                   docs/ but has not yet been built." The structured contract
                   this task implements was decided by TASK-108 and is
                   recorded in the Product Owner answers that task holds; the
                   DATABASE.md entry the contract requires is a required
                   consequence of the supersession, not a blended type. See
                   "Type classification note".)
Status:            BACKLOG (per tasks/README.md §6 step 6. Ascends to READY
                   only through a lifecycle validation confirming
                   TASK_LIFECYCLE.md §3's criteria — see "Readiness
                   Pre-Check".)
Risk:              HIGH (TASK_TYPES.md §4 — FEATURE baseline MEDIUM, "can be
                   HIGH if it touches combat / battle state / auth"; raised
                   further because this task changes a PERSISTENCE CONTRACT
                   the whole Card system reads, supersedes a DONE decision,
                   and rewrites provisioned content rows. It touches
                   PostgreSQL schema, the Domain Card content type, the
                   provisioning migration, and DATABASE.md.)
Priority:          HIGH (the sole prerequisite for TASK-107 — the ROADMAP.md
                   Phase 1 "3 Basic Cards" vertical slice, and the only
                   remaining producer of the Shield TASK-102 implemented the
                   consumption side of. TASK-107 cannot be EXECUTED until this
                   lands; see Dependency / Sequencing.)
Primary Agent:     persistence (TASK_TYPES.md §5 / AGENT_SELECTION.md §1 —
                   "Database change: Primary Agent Persistence". The core
                   deliverable is a persistence-contract migration: the
                   column, the EF mapping, the migration, and the provisioned
                   row content.)
Supporting Agents: backend (the Domain CardDefinition content model and the
                   Application-layer Card definitions reader — the type that
                   will carry the structured value),
                   gameplay (CONSULTED ONLY — CARD_RULES.md §2 is the owner of
                   the values to be encoded; this task encodes them verbatim
                   and authors none),
                   testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   backend/persistence-analysis,
                   quality/architecture-conformance,
                   quality/documentation-consistency,
                   testing/test-scenario-generation,
                   quality/scope-validation
                   (7 skills — Complex budget ceiling, tasks/README.md §12)
Dependencies:      TASK-108 (IN REVIEW — the approved D-1/D-2 contract this
                     task implements, and the R2-7 supersession it records.
                     Read-only; NOT modified),
                   TASK-082 (DONE — the R2-7 decision this task SUPERSEDES for
                     Card EffectDefinition. IMMUTABLE: read, cited, NOT
                     modified, NOT re-opened, NOT re-statused),
                   TASK-085 (DONE — provisioned the 6 CardDefinition rows this
                     task's content migration rewrites; IMMUTABLE),
                   TASK-052/TASK-053 (DONE — the BossDefinition provisioning
                     migration precedent TASK-085 followed; cited, not
                     modified),
                   TASK-028 (DONE — Card ownership + the CardDefinition
                     content model and its EF configuration; read-only),
                   TASK-102 (Shield consumption, implemented — the behavior
                     the Shield Card's effect will eventually reach. This task
                     does NOT call it),
                   TASK-105 (DONE — the frozen Shield contract; IMMUTABLE)
Blocks:            TASK-107 (it cannot be EXECUTED, and its resolver has no
                     readable effect data, until this lands). Through TASK-107
                     it blocks the Phase 1 "3 Basic Cards" slice. It does NOT
                     block TASK-036, TASK-079, or TASK-099, and it does not
                     unblock PetSkillCast.
Estimate:          Complex (crosses Domain → Infrastructure → docs; one
                   Domain content type change, one EF mapping change, one
                   migration including a content rewrite, one DATABASE.md
                   contract edit, and their tests)
```

**Type classification note.** `FEATURE`. `TASK_TYPES.md` §2 defines `FEATURE` as
implementing "a documented mechanic, capability, or system that already has a
home in `docs/` but has not yet been built" — the structured contract has a home
(the TASK-108 decision) and is not built. It is **not** `GAMEPLAY-CHANGE`: no
game rule changes, and `CARD_RULES.md` §2's Costs and effect values are encoded
verbatim, not altered. It is **not** `ARCHITECTURE`: no layer, module boundary,
technology, or authoritative model changes; `EffectDefinition` stays a column on
the same table read by the same paths. It is **not** `DOCUMENTATION`: the
`DATABASE.md` edit is a required consequence of the contract change, and
`TASK_TYPES.md` §2 states "Code changes that also update docs as a side effect
do not use this type". `TASK_TYPES.md` §3 forbids *blending* types — it does not
forbid a code task from carrying its own doc update, which is what
`AGENTS.md` §17 requires.

**No ADR is required.** This is a content-storage shape change on an existing
table, not a change to layering, storage *technology*, realtime strategy, or the
authoritative model. `ADR-006` (PostgreSQL for persistent data, "queried only
outside the hot resolution path … never per-action during an active battle")
already governs and is preserved — the structured value is read from the
already-snapshotted content, not queried per cast. `docs/03-decisions/README.md`
§8's "Known Open Items (Not ADRs)" should still be checked by the executing
agent, and if it concludes an ADR *is* required that is a STOP (see Stop
Conditions).

**This task authors no game rule and no balance value.** Every encoded number
comes verbatim from `CARD_RULES.md` §2. TASK-108's illustrative JSON values are
**shape examples** and must NOT be transcribed (its Heal example says 30 where
`CARD_RULES.md` §2 says 20; its Power example says 5 where §2 says 25).

---

## Objective

Implement the runtime-readable, structured `CardDefinition.EffectDefinition`
contract that TASK-108's Product Owner decision (D-1 structured effect identity,
D-2 data-driven magnitude) approved: define its representation, carry it through
persistence (column type/size, EF mapping, a migration), provide its
serialization/deserialization and validation, migrate the six provisioned
`CardDefinition` rows from verbatim prose to the structured form using **only**
the values `CARD_RULES.md` §2/§4.1 states, and record the **Card-scoped**
supersession of TASK-082 R2-7 in `docs/02-technical/DATABASE.md` — **without**
implementing CardCast, any effect resolver gameplay behavior, any call to
`ApplyHeal` / `ApplyShield` / `ApplyPower`, or any change to a gameplay rule or
balance value.

---

## Authoritative References

### The decision this task implements (READ ONLY — it is the approved input)

- `tasks/backlog/TASK-108-resolve-card-effect-resolution-contract.md`
  **"Product Owner Decisions" (D-1, D-2)** and **"D-5 The Resolver Contract"** /
  **"D-6 Balance-Value Boundary"** — the approved contract. D-1: effect identity
  comes from structured effect data (`effectType`), and the runtime must not
  parse prose, infer from Card name, map from `CardDefinitionId`, or hardcode
  card logic. D-2: the magnitude is data-driven inside the same structure,
  carrying the value, its interpretation, and the calculation rule
  (`valueType` + `value`). **The D-6 boundary is binding: the concrete balances
  remain owned by `CARD_RULES.md`, and the illustrative values in D-2 are shapes,
  not content.**
- `tasks/backlog/TASK-108-…md` **"TASK-082 R2-7 Supersession"** and
  **"Follow-Up Tasks Required" item 1** — the supersession this task applies and
  the scope it was handed. Read-only; NOT modified by this task.

### The contract being superseded (READ, CITE, DO NOT MODIFY)

- `tasks/completed/TASK-082-resolve-pet-card-relic-content-provisioning-contract.md`
  **decision R2-7** and **"Decisions Recorded" item C** — the ruling this task
  supersedes for Cards: store "the authoritative effect rule text … in
  EffectDefinition", "Do not introduce an `effect-{slug}` vocabulary or any new
  effect-reference identifier system", "must be copied from the authoritative
  domain documentation verbatim and must satisfy the existing 128-character
  database limit". **IMMUTABLE** — `TASK_LIFECYCLE.md` §3. This task reads and
  cites it; it does not edit, re-open, re-status, or move it.
- `docs/02-technical/DATABASE.md` **§1** — the owner of the storage shape. The
  `CardDefinition` block's `EffectDefinition` member currently carries R2-7's
  verbatim-prose ruling and its "no `effect-{slug}` or other effect-id
  vocabulary" clause (**line ~231**), and the **version header** (~line 28)
  restates it. Both are the statements this task corrects. The
  **`RelicDefinition` block (~line 258)** carries the same R2-7 wording and is
  **NOT changed** (see Supersession Scope Decision). Also **§2** (ownership and
  the FK relationships), **§3** (constraints), **§5 item 4** (the provisioning
  mechanism and its "row content is transcribed, never computed or invented"
  rule (b)).
- `docs/02-technical/DATABASE.md` **§1** `LoadoutCopyLimit` and **§2** — the
  surrounding `CardDefinition` contract that must be preserved unchanged.

### The values to encode (the ONLY authorized source of numbers)

- `docs/01-game-design/CARD_RULES.md` **§2** — the three MVP Basic Cards with
  their exact Costs and Effects (Heal, Shield, Power Charge), and item 3
  (Power Charge's Cost 0 "must never be blocked by insufficient Power").
  **§4.1** — the three content-defined Pet Skill Cards (Inferno, Tidal Barrier,
  Iron Fang). **§1** — `Category` and the `LoadoutCopyLimit` value.
  **The values this task encodes must be transcribed from here and nowhere
  else.** No magnitude is authored, rounded, or reinterpreted.
- `docs/01-game-design/COMBAT_RULES.md` **§4** — the frozen Shield contract the
  encoded Shield value must eventually reach (context for what the value means;
  **no Shield semantic is changed here**); **§4 item 1** — the Heal clamp;
  **§6 / `GAME_RULES.md` §12** — the Power range/cap.

### The implementation surfaces (inspect before changing)

- `src/backend/GameServer.Domain/Cards/CardDefinition.cs` — the Domain content
  type. `EffectDefinition` is currently `required string`, documented as "an
  effect **reference** … carried as a reference, not as resolved effect content,
  and nothing executes it". This is the member whose representation changes.
- `src/backend/GameServer.Domain/Cards/PlayerUnlockedCard.cs` — the ownership
  row that references a `CardDefinition` (do not change its shape).
- `src/backend/GameServer.Infrastructure/Postgres/Configurations/CardDefinitionConfiguration.cs`
  — the EF mapping, including the `EffectDefinition` property configuration.
- `src/backend/GameServer.Infrastructure/Postgres/Migrations/20260925150903_AddCardPersistence.cs`
  — the column as created: `EffectDefinition = table.Column<string>(type:
  "character varying(128)", maxLength: 128, nullable: false)`. **A structured
  payload will very likely not fit 128 characters and the column type is not
  JSON-native — this is the concrete persistence question this task resolves.**
- `src/backend/GameServer.Infrastructure/Postgres/Migrations/20260929152651_ProvisionPetCardRelicContentDefinitions.cs`
  — the six provisioned `CardDefinition` rows whose `EffectDefinition` values
  are verbatim prose. **This task adds a NEW migration; it does not edit this
  one** (an applied migration is history).
- `src/backend/GameServer.Infrastructure/Postgres/Migrations/GameDbContextModelSnapshot.cs`
  — updated by the migration tooling.
- `src/backend/GameServer.Application/Cards/CardLoadoutService.cs` and
  `ICardRepository.cs` — the existing readers of Card content. The loadout
  snapshot path must keep working; it reads identity, not effect.
- `src/backend/GameServer.Domain/Battle/Serialization/` — the existing
  serializer conventions this repository already uses for runtime state (a
  reference for style; **the battle-state serializer is not the place for Card
  content, and no battle-state schema changes**).

### Boundaries the answer must respect

- `docs/02-technical/ARCHITECTURE.md` **§2.1** (Domain owns rules, Application
  orders calls, Infrastructure persists, Api transports), **§5** (the
  anti-overengineering standard — do not add an effect framework, a handler
  registry, or a plugin system), **§2.2.2** (the implemented `Swap` path).
- `docs/02-technical/TDD.md` **§4 item 3** (PostgreSQL is never read on the hot
  resolution path), **§6** (determinism).
- `docs/02-technical/GAME_STATE.md` **§0 item 5** (no parallel representation of
  a concept another stage owns), **§2.3** / **§2.3.1** (`PetState`,
  `ActiveStatusEffects[]`, and the Status Effect `Magnitude` member — the
  Shield the encoded value will eventually produce).
- `docs/03-decisions/ADR/ADR-006-postgresql-persistence.md` (persistence
  technology and the hot-path exclusion), `ADR-012` item 9 (Card ownership: the
  `PlayerUnlockedCard` unlock-flag join table; no Card instance).
- `docs/00-overview/MVP_SCOPE.md` **§1** (Cards and Combat/Status Effects are
  IN), **§2** (OUT), **§4** (unlisted is not implicitly IN); `ROADMAP.md`
  Phase 1 (Cards; "No Relics yet" — line 36) and Phase 2 (Relics — line 46).
- `AGENTS.md` §4 (conflict resolution), §7 (never invent a rule or value),
  §9 (anti-overengineering), §16 (report, do not fix, adjacent issues),
  §17 (documentation change rule), §18 (architecture change rule), §20 (stop
  conditions); `tasks/README.md` §9, §12, §13.

**ADR check:** no ADR is required (see the Metadata note). The executing agent
must confirm rather than assume, and a required ADR is a STOP.

---

## Current State

`CardDefinition.EffectDefinition` is a **prose string** at every layer, and
nothing reads it as data. Verified in the working tree:

```text
Domain    src/backend/GameServer.Domain/Cards/CardDefinition.cs
            public required string EffectDefinition { get; init; }
            documented: "an effect reference … not as resolved effect content,
                         and nothing executes it"

EF        .../Postgres/Configurations/CardDefinitionConfiguration.cs
            builder.Property(definition => definition.EffectDefinition) …

Schema    .../Migrations/20260925150903_AddCardPersistence.cs
            EffectDefinition = table.Column<string>(
                type: "character varying(128)", maxLength: 128, nullable: false)

Rows      .../Migrations/20260929152651_ProvisionPetCardRelicContentDefinitions.cs
            card-heal          "Restore the active Pet's HP by 20% of its Max HP"
            card-shield        "Active Pet gains Shield equal to 20% of its Max HP"
            card-power-charge  "Active Pet gains 25 Power"
            (+ 3 Pet Skill rows: card-inferno, card-tidal-barrier, card-iron-fang)

Readers   src/backend/GameServer.Application/Cards/CardLoadoutService.cs
            reads Card content for loadout validation; reads identity, not effect
```

**Nothing resolves `EffectDefinition`.** A repo-wide search for a Card-effect
vocabulary (`CardEffectType` / `CardEffectKind` / `EffectSlug`) returns zero
matches in `src/` and `tests/`; the only reference to `EffectDefinition` outside
the entity, its EF configuration, and the provisioning migration is a doc
comment. `StatusEffectLifecycle.ApplyShield` exists (TASK-102) and is called by
nothing.

**The contradicting rule.** `docs/02-technical/DATABASE.md` §1's `CardDefinition`
block currently states R2-7's opposite of TASK-108's decision, and its version
header restates it. Until this task lands, the document and the approved
contract disagree.

**Consequence.** TASK-107's resolver is specified to read a `CardDefinition` and
apply its documented effect, with the magnitude coming from Card content — which
this representation cannot provide. TASK-107 cannot be executed until this
lands.

---

## Supersession Scope Decision

TASK-082 R2-7 governed **both** `CardDefinition` and `RelicDefinition`
`EffectDefinition` (`DATABASE.md` §1 carries the identical wording in both
blocks). TASK-108's decision addresses **Card** effects only, and no Product
Owner decision exists about Relic effects. The scope is therefore determined —
not guessed — by the roadmap's phase boundary:

```text
Cards  — ROADMAP.md Phase 1 (line 36 names "No Relics yet" as a Phase 1
         boundary, so Cards ARE the Phase 1 content system). TASK-108 D-1/D-2
         apply. R2-7 is SUPERSEDED for CardDefinition.EffectDefinition.

Relics — ROADMAP.md Phase 2 (line 46, "All ~10 Relics + trigger/stacking
         system"). No decision, no implementation, and no consumer exists.
         R2-7 REMAINS IN FORCE for RelicDefinition.EffectDefinition.
```

**This task therefore:**
- supersedes R2-7 for the **`CardDefinition`** block of `DATABASE.md` §1 and for
  the `CardDefinition` rows in provisioning;
- **leaves the `RelicDefinition` block and its rows exactly as R2-7 wrote them**;
- must state this split explicitly in the `DATABASE.md` edit, so a later reader
  does not conclude the whole of R2-7 was withdrawn.

If the executing agent concludes the Relic half cannot be left intact (e.g.
because a shared type forces it), that is a **STOP** — the Relic scope is a
decision this task was not given.

---

## Scope

### In Scope

1. **Define the structured `EffectDefinition` representation** carrying D-1's
   `effectType` and D-2's `valueType` + `value`. The representation must be
   runtime-readable without prose parsing, name inference, or `CardDefinitionId`
   mapping. It must express **exactly** the effects `CARD_RULES.md` §2 and §4.1
   document — no more. Determining the concrete serialization form (JSON document
   text, or a set of typed columns) is this task's engineering decision **within**
   the approved contract; it must be the simplest form that satisfies D-1/D-2
   (`AGENTS.md` §9).
2. **Decide and implement the persistence representation**: the column
   type/size (the current `character varying(128)` was sized for prose and is
   not JSON-native), the EF mapping, and a **new migration**. The migration must:
   - change the column shape if required;
   - **migrate the six provisioned `CardDefinition` rows deterministically**
     from their prose values to the structured form;
   - preserve `RelicDefinition`'s `EffectDefinition` and every other column
     unchanged;
   - be reversible (`Down` restores the prose form) — the executing agent must
     state the reversibility approach rather than leaving `Down` empty.
3. **Migrate the provisioned row content**, transcribing values **only** from
   `CARD_RULES.md` §2/§4.1. The mapping from each existing prose string to its
   structured equivalent is a deterministic, auditable one-to-one
   transcription — **no value is invented, rounded, or inferred**. Where
   `CARD_RULES.md` does not fully define an effect (see below), the row must NOT
   be silently given a value.
4. **Runtime model + serialization/deserialization**: represent the structured
   value on the Domain content type and (de)serialize it losslessly and
   deterministically. Round-tripping a `CardDefinition`'s effect must preserve
   every member and its exact type.
5. **Validation**: reject malformed/incomplete effect data — an unknown
   `effectType`, a missing `value`, an unsupported `valueType`, and any
   combination the contract does not define. Validation must fail loudly rather
   than defaulting (`GAME_STATE.md` §2.3.1 item 8's "zero/absent is not a
   sentinel" spirit; `CardDefinition.LoadoutCopyLimit`'s "no default"
   precedent).
6. **Record the Card-scoped supersession of TASK-082 R2-7 in
   `docs/02-technical/DATABASE.md`**: correct the `CardDefinition` block's
   `EffectDefinition` member description and the version header, state the
   supersession explicitly (naming TASK-082 R2-7 and TASK-108 D-1/D-2), and
   state that the **Relic** half of R2-7 remains in force. Advance the version
   header per the document's own convention.
7. **Tests**: serialization round trip; validation rejection cases; the
   migration's determinism (the six rows migrate to the expected structured
   values); the existing Card loadout and collection paths still pass; and the
   negative assertions that no gameplay execution was added.

### Out of Scope

- **CardCast, in any form.** No hub method, no Application cast use case, no
  `BattleEventType` member, no wire-projection arm. This task makes the data
  readable; TASK-107 casts it. `BattleHub.cs`'s "intentionally NOT implemented"
  comment stays accurate for `CardCast`/`PetSkillCast` and must NOT be changed.
- **Any effect resolver gameplay behavior.** No dispatch that calls
  `ResourceGenerator.ApplyHeal`, `StatusEffectLifecycle.ApplyShield`, or
  `ResourceGenerator.ApplyPower`; no `EffectResolver` that executes. The
  structured value is produced, stored, read, and validated — **not applied**.
  A resolver whose only job is to *decode* the structure is in scope; one that
  *mutates battle state* is not.
- **PetSkillCast and the Pet Skill Cards** (`CARD_RULES.md` §4/§4.1). The three
  Pet Skill rows are migrated for **schema consistency only** — because the
  column now carries structured data — using only what `CARD_RULES.md` §4.1
  states. **Huyền Quy's Tidal Barrier has NO authored Shield magnitude** ("Heal;
  Gain Shield"), recorded as an unauthored content gap by TASK-104 §5 / B-4. Its
  row must NOT be given a Shield magnitude, and if the structured form cannot
  represent that row without one, that is a **STOP** (see Stop Conditions) — not
  a value to invent. Thanh Xà and Sơn Hùng have no Signature Skill rows at all
  (`PET_RULES.md` §8) and none may be created.
- **Any balance value change.** `CARD_RULES.md` §2/§4.1 is the sole source, and
  every encoded number is transcribed from it. **TASK-108's illustrative values
  (Heal 30, Power 5) must NOT be used** — they disagree with §2 (Heal 20, Power
  Charge 25). If any prose→structured transcription appears to require a value
  `CARD_RULES.md` does not state, that is a STOP.
- **Any Shield semantic.** One instance per entity, refresh replaces magnitude,
  no additive stacking, absorption before HP, overflow by remainder, and
  `ShieldDepleted` is not a Battle Event are frozen (`COMBAT_RULES.md` §4,
  TASK-105). `StatusEffectLifecycle` and `DamagePipeline` are READ-ONLY here.
- **Relic `EffectDefinition`** — unchanged; R2-7 remains in force for it (see
  Supersession Scope Decision).
- **Any Relic, Passive, Boss, Pet, Element, Status Effect type, resource, or
  progression change**; no Match-3, board, Gem, Swap, Cascade, or Combo change.
- **Any new SignalR method, event, wire member, Redis key, or API endpoint**;
  no `BattleState`/`PetState`/`BossState` member.
- **Any new gameplay rule, ADR, or architecture change** — a required ADR is a
  STOP.
- **Any client / Phaser / React work.**
- **Modifying TASK-082, TASK-102, TASK-103, TASK-104, TASK-105, TASK-106,
  TASK-107, TASK-108, TASK-036, TASK-079, TASK-099, or any `tasks/completed/*`
  file.**
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Affected Files & Areas

```text
[x] src/backend/GameServer.Domain/Cards/CardDefinition.cs (the EffectDefinition
      member's representation — the structured content model. The other members
      and the CardDefinitionId/ownership contract are unchanged.)
[x] src/backend/GameServer.Domain/Cards/ (any new small type the structured
      representation requires — e.g. a value object for the structured effect.
      NO resolver, NO handler registry, NO effect framework — AGENTS.md §9.)
[x] src/backend/GameServer.Infrastructure/Postgres/Configurations/
      CardDefinitionConfiguration.cs (the EF mapping)
[x] src/backend/GameServer.Infrastructure/Postgres/Migrations/ (ONE new
      migration: column shape + deterministic content migration of the six
      CardDefinition rows; plus the model snapshot the tooling updates)
[x] src/backend/GameServer.Infrastructure/Postgres/ (the provisioning path /
      repository that reads Card content, if its shape must follow)
[x] docs/02-technical/DATABASE.md (the CardDefinition EffectDefinition member
      description + version header — the Card-scoped R2-7 supersession. The
      RelicDefinition block is NOT changed.)
[?] src/backend/GameServer.Application/Cards/ (ONLY if a reader must follow the
      representation change; the loadout path reads identity, so this is
      expected to need at most a mechanical adjustment — report if more)
[ ] src/backend/GameServer.Domain/Battle/ (StatusEffect / StatusEffectLifecycle
      / PetState / BattleState — READ-ONLY; no state member)
[ ] src/backend/GameServer.Domain/Combat/ (DamagePipeline — READ-ONLY)
[ ] src/backend/GameServer.Api/ (NONE — no hub method, no projection arm)
[ ] src/frontend/client/ (NONE)
[x] tests/ (Domain unit tests for the representation + (de)serialization +
      validation; Infrastructure tests for the migration's deterministic
      content outcome and the EF round trip; regression coverage that the
      existing Card loadout/collection paths still pass)
[ ] docs/03-decisions/ADR/ (NONE — a required ADR is a STOP)
[x] tasks/ (this file only)
```

The executing agent must reduce each `[?]` to `[x]` or `[ ]` based on what the
change actually requires, and must not touch a document or file it does not
need to.

---

## Implementation Notes

- **Apply the supersession in the open.** This is the task's defining feature.
  The `DATABASE.md` edit must name TASK-082 R2-7 and TASK-108 D-1/D-2 and state
  that Cards now use a structured contract while Relics retain R2-7. Do **not**
  silently rewrite the sentence as though the contract had always been
  structured — that would erase the decision trail (`AGENTS.md` §17,
  `TASK-108`'s "TASK-082 R2-7 Supersession").
- **Do not edit TASK-082.** It is DONE and immutable (`TASK_LIFECYCLE.md` §3).
  Cite it; do not re-open it, re-status it, or add a superseded-by note to it.
  TASK-108 already records the supersession from the decision side.
- **Do not edit the applied provisioning migration.** `20260929152651_…` is
  history. Either add a new migration that updates the six rows, or (if the
  project's precedent permits) the new column-shape migration also carries the
  content `UPDATE`. Follow the TASK-052/TASK-053/TASK-085 provisioning
  precedent, and keep "row content is transcribed, never computed or invented"
  (`DATABASE.md` §5 item 4 rule (b)) intact.
- **Transcribe, never transcribe-from-the-wrong-document.** The only authorized
  numbers are `CARD_RULES.md` §2/§4.1. TASK-108's JSON examples are shapes whose
  numbers disagree with §2 — using them would change balance. Encode §2's
  values, and state in the migration comments which §2/§4.1 line each row's
  value came from (the existing provisioning migration's citation style).
- **The column decision is the real engineering question.** `character
  varying(128)` was sized for prose. A structured payload needs either a larger
  variable-length type or a JSON-native type. Choose the simplest representation
  that satisfies D-1/D-2 and the existing EF/Npgsql stack (`AGENTS.md` §9 — no
  JSON pipeline, no content service, no external loader unless the existing
  architecture already requires it). Whatever you choose, round-trip losslessness
  and determinism are required.
- **Tidal Barrier is the trap.** Its `CARD_RULES.md` §4.1 effect is "Heal; Gain
  Shield" with **no Shield magnitude**. The column now requires structured data
  for every row, so this row forces a decision. **Inventing the magnitude is
  forbidden** (TASK-104 B-4; `AGENTS.md` §7). If the contract genuinely cannot
  represent "Shield, magnitude TBD", either (a) represent exactly the parts
  §4.1 does define and leave the magnitude member explicitly absent/undetermined
  in a documented way, or (b) **STOP and report** the forced decision. Do not
  pick a number.
- **Keep the boundary between "decode" and "execute" visible in the code.** A
  structured type plus a decode/validate step belongs here; a switch that calls
  the Domain write sites belongs to TASK-107. If you find yourself adding
  `ApplyHeal`/`ApplyShield`/`ApplyPower` calls, you have crossed the scope line.
- **No new abstraction layer.** No `IEffectHandler`, no effect registry, no
  factory keyed by `effectType`, no plugin discovery. The contract needs a data
  shape and its validation — nothing more (`ARCHITECTURE.md` §5, `AGENTS.md` §9).
- **Preserve the existing Card contract.** `CardDefinitionId`, `Name`,
  `Category`, `PowerCost`, `LoadoutCopyLimit`, the `PlayerUnlockedCard` join,
  the `PetDefinition.SignatureSkillCardId` FK, and the loadout-snapshot path are
  all unchanged. The only member whose representation moves is
  `EffectDefinition`.
- **Cite, do not restate.** Keep the `<c>CARD_RULES.md</c> §2` citation idiom.
  Never copy a Cost, a magnitude, or a formula into a comment or into this task
  file (`AGENTS.md` §9, `tasks/README.md` §9).
- **Report, do not fix, adjacent gaps** (`AGENTS.md` §16). Known and previously
  reported, none belonging to this task: Tidal Barrier's unauthored Shield
  magnitude (TASK-104 B-4); Thanh Xà / Sơn Hùng Signature Skill content
  (`PET_RULES.md` §8); the "Burning Curse" Relic's `RELIC_RULES.md` §3-vs-note-1
  tension; the `BattleStateJson.cs` items TASK-097/TASK-098 reported; the
  `SIGNALR_PROTOCOL.md` "§3.3" and `BOSS_RULES.md` §4 citation nits; and the
  open Relic storage-shape question (`DATABASE.md` §2 note).

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — the structured representation on its own: construction
                         from valid effect data; rejection of malformed data
                         (unknown effectType, missing value, unsupported
                         valueType, absent required members); and lossless,
                         deterministic serialization/deserialization
                         (a round trip preserves every member and its exact
                         type — no coercion, no defaults, no reordering).
[x] Integration tests  — the persistence path: EF mapping of the structured
                         value; a round trip through the configured provider;
                         and the migration's deterministic content outcome
                         (each of the six CardDefinition rows migrates to the
                         structured value transcribed from CARD_RULES.md §2/
                         §4.1 — asserted per row, not in aggregate).
[x] Regression tests   — the existing Card content paths still pass unchanged:
                         loadout validation/snapshot (CardLoadoutService /
                         ICardRepository), the collection read endpoints, and
                         the battle-start smoke path that submits a basic card
                         loadout.
[x] Negative tests     — the scope boundary is enforced by assertion: no
                         CardCast hub method exists; no BattleEventType member
                         was added; no call site invokes ApplyHeal / ApplyShield
                         / ApplyPower from Card content; RelicDefinition's
                         EffectDefinition is unchanged.
```

### Key Edge Cases

- **A row whose documented effect cannot be fully structured** — Tidal Barrier
  ("Heal; Gain Shield", no magnitude; `CARD_RULES.md` §4.1, TASK-104 B-4). Must
  be handled without inventing a value, or the task STOPS.
- **The value-type distinction actually matters** — a percentage-of-MaxHP effect
  and a flat effect must not collapse into one representation; D-2 requires the
  interpretation to be carried (`valueType`). Assert that two effects differing
  only in `valueType` do not deserialize to the same thing.
- **Power Charge's Cost is 0** (`CARD_RULES.md` §2 item 3) while its *effect* is
  a positive Power gain — the structured `value` is the effect magnitude, not
  the Cost; assert the two are not conflated.
- **The Shield value's unit** — `CARD_RULES.md` §2 states it as a proportion of
  Max HP, so the encoded value must carry that interpretation and must NOT be
  pre-resolved to a number against some assumed MaxHP (the Pet's `MaxHP` is
  battle state, `GAME_STATE.md` §2.3, and is read at cast time by TASK-107).
- **A malformed or truncated payload in the column** — validation must reject
  it, not fall back to a default or to treating it as prose.
- **The migration is deterministic and idempotent under re-run** — the six rows
  end in exactly one documented structured state, and `Down` restores the prose
  form the provisioning migration wrote.
- **A RelicDefinition row** — must still carry R2-7's verbatim prose after the
  migration; asserted, since the column type change touches the same EF
  configuration shape.
- **An unknown/new `effectType` value** — rejected by validation rather than
  silently accepted, so a content typo cannot become a silent no-op.

### Explicitly Not Tested Here

- Card cast execution, Power deduction, Turn/Combo non-interaction, or the
  `CardCast` event — all TASK-107's.
- Shield absorption/refresh/depletion — TASK-102's, already covered.
- PetSkillCast and any Pet Skill effect execution — out of scope.
- Relic trigger/effect resolution — Phase 2.
- Client-side rendering of any effect — no client work.

---

## Stop Conditions

Universal `AGENTS.md` §20 / `.ai/README.md` §13 stops always apply.
Task-specific:

1. **If any prose→structured transcription would require a value that
   `CARD_RULES.md` §2/§4.1 does not state** — **STOP per `AGENTS.md` §7.**
   Include TASK-108's illustrative values: they disagree with §2 and must not be
   used. Report the row and the missing value.
2. **If Tidal Barrier's row cannot be represented without inventing its Shield
   magnitude** — **STOP** and report. The magnitude is an unauthored content
   gap (TASK-104 §5 / B-4). Do not pick a number, do not use a placeholder, and
   do not drop the row.
3. **If the structured contract cannot be determined within D-1/D-2's approved
   shape** — e.g. expressing `CARD_RULES.md` §2's effects requires a fourth
   semantic the Product Owner did not approve — **STOP**; that is a new
   decision, not an engineering choice.
4. **If multiple materially different contracts remain equally valid** after
   reading D-1/D-2 — **STOP** (`AGENTS.md` §20, ambiguous requirement) rather
   than choosing one on the Product Owner's behalf.
5. **If the change would require an ADR** — **STOP and report**
   (`AGENTS.md` §18). Check `docs/03-decisions/README.md` §8 first.
6. **If the migration cannot be made deterministic for the existing rows** —
   e.g. a prose value cannot be mapped to exactly one structured value, or
   `Down` cannot restore a faithful previous state — **STOP**; report the row.
7. **If the Relic half of R2-7 cannot be left intact** — e.g. a shared type or
   EF configuration forces the Relic column to change representation too —
   **STOP**; the Relic scope is an ungiven decision (see Supersession Scope
   Decision).
8. **If the implementation would require CardCast, a resolver that mutates
   battle state, a `BattleEvent`, a SignalR member, a Redis key, a new API
   endpoint, or any `BattleState`/`PetState` member** — **STOP**; that is
   TASK-107's or a separate task's scope.
9. **If any part would change a Shield, Heal, or Power semantic, or a balance
   value** — **STOP** (`COMBAT_RULES.md` §4 is frozen; `CARD_RULES.md` §2 is the
   value owner).
10. **If satisfying any criterion would require modifying TASK-082, TASK-102,
    TASK-103, TASK-104, TASK-105, TASK-106, TASK-107, TASK-108, TASK-036,
    TASK-079, TASK-099, or any `tasks/completed/*` file** — **STOP**; report the
    stale statement instead (`TASK_LIFECYCLE.md` §3 — completed tasks are
    immutable).
11. **If the change would cross into `MVP_SCOPE.md` §2's OUT list, or reach an
    unlisted (FUTURE) system** — **STOP** (`AGENTS.md` §8, `MVP_SCOPE.md` §4).
    Relics are Phase 2 and are explicitly not reached.
12. **If the task exceeds 7 skills or crosses an uncoupled architectural
    boundary** — **STOP and decompose** (`tasks/README.md` §13).

When stopped, report the exact condition and **do not invent a resolution**.

---

## Readiness Pre-Check (for the lifecycle validation that follows this task)

This task is `BACKLOG` per `tasks/README.md` §6. The discovery pass verified the
`TASK_LIFECYCLE.md` §3 criteria as follows:

```text
[x] Task type confirmed (TASK_TYPES.md)   FEATURE — a decided contract with a
                                          home in docs/ (TASK-108) that is not
                                          built. Not GAMEPLAY-CHANGE (no rule
                                          or balance changes), not ARCHITECTURE
                                          (no layer/technology/model change),
                                          not DOCUMENTATION (code is the
                                          primary output; the DATABASE.md edit
                                          is its required side effect).
[x] Relevant documentation exists in docs/ DATABASE.md §1/§2/§3/§5, CARD_RULES.md
                                          §1/§2/§4.1, COMBAT_RULES.md §4/§6,
                                          GAME_RULES.md §12, GAME_STATE.md
                                          §0/§2.3/§2.3.1, ARCHITECTURE.md §2.1/
                                          §5, TDD.md §4/§6, ADR-006, ADR-012,
                                          ROADMAP.md, MVP_SCOPE.md — all present.
[x] MVP scope confirmed (MVP_SCOPE.md §1) Cards are IN; Relics are Phase 2 and
                                          are explicitly not reached.
[x] Not blocked by an unresolved dep.     TASK-108's D-1/D-2 are answered. The
                                          one open content gap (Tidal Barrier's
                                          magnitude) is handled by an explicit
                                          STOP condition rather than by
                                          assuming a value.
[x] Primary agent assigned                persistence; supporting: backend,
                                          gameplay (consultative), testing,
                                          review.
[x] Workflow assigned                     development/feature.md
[x] Acceptance criteria are testable      All binary; each names the owning doc
                                          section or a proving assertion.
```

**Dependency / sequencing.** TASK-108 is `IN REVIEW`; the decision it records is
approved and is this task's input. TASK-107 remains `BACKLOG` and cannot be
*executed* until this task lands — it is not modified, re-scoped, or re-statused
here.

**Known open item carried forward, not resolved here:** Tidal Barrier's Shield
magnitude (`CARD_RULES.md` §4.1) remains unauthored per TASK-104 §5 / B-4. This
task migrates that row only as far as §4.1 allows; the magnitude remains a
separate content decision, and PetSkillCast remains blocked on it.

---

## Acceptance Criteria

### The contract

- [ ] A structured `EffectDefinition` representation exists carrying D-1's
      effect identity and D-2's value + value interpretation, and it is
      **runtime-readable without prose parsing, Card-name inference,
      `CardDefinitionId` mapping, or card-specific hardcoding**.
- [ ] The representation can express exactly the effects `CARD_RULES.md` §2 and
      §4.1 document, and **no effect type the documents do not define** is
      introduced.
- [ ] **`EffectDefinition` remains a single content concept** — no parallel
      representation of the effect is introduced elsewhere
      (`GAME_STATE.md` §0 item 5).
- [ ] Serialization is **lossless and deterministic**: a round trip preserves
      every member and its exact type, with no coercion, defaulting, or
      reordering (`TDD.md` §6).

### The supersession (TASK-082 R2-7)

- [ ] `docs/02-technical/DATABASE.md`'s **`CardDefinition`** block no longer
      states that `EffectDefinition` is verbatim prose and no longer forbids an
      effect-identity vocabulary; it describes the structured contract.
- [ ] The `DATABASE.md` edit **explicitly records the supersession**, naming
      TASK-082 R2-7 and TASK-108 D-1/D-2 — it is not a silent rewrite.
- [ ] The `DATABASE.md` edit **states that the `RelicDefinition` half of R2-7
      remains in force**, and the `RelicDefinition` block itself is **byte-
      identical** apart from any version-header advancement.
- [ ] The `DATABASE.md` **version header** is advanced and records the change in
      the document's existing style.
- [ ] **TASK-082 is not modified** — byte-identical, not re-opened, not
      re-statused, not moved (`TASK_LIFECYCLE.md` §3).

### Persistence

- [ ] `CardDefinition` can **store** the structured representation: the column
      type/size is sufficient (the former `character varying(128)` prose limit is
      resolved explicitly, with the chosen type recorded in `DATABASE.md`).
- [ ] The EF mapping carries the representation losslessly through the
      configured provider, proven by a round trip.
- [ ] **One new migration** exists and is the only migration added; the applied
      `20260929152651_ProvisionPetCardRelicContentDefinitions` migration is
      **unmodified**.
- [ ] The migration migrates the six provisioned `CardDefinition` rows from
      prose to the structured form **deterministically**, asserted per row.
- [ ] The migration is **reversible**: `Down` restores the prior representation,
      and the approach is stated rather than left empty.
- [ ] **`RelicDefinition.EffectDefinition` rows are unchanged** by the
      migration.
- [ ] The other `CardDefinition` members (`CardDefinitionId`, `Name`,
      `Category`, `PowerCost`, `LoadoutCopyLimit`), the `PlayerUnlockedCard`
      join, and the `PetDefinition.SignatureSkillCardId` FK are **unchanged**.

### Content fidelity (the balance boundary)

- [ ] Every value encoded in the six rows is **transcribed from
      `CARD_RULES.md` §2/§4.1**, and each row's migration comment cites the
      section it came from.
- [ ] **No balance value changed**: `CARD_RULES.md` is **byte-identical**, and
      no Cost or effect magnitude differs from what it states.
- [ ] **TASK-108's illustrative values (Heal 30, Power 5) do not appear**
      anywhere in the implementation, tests, or data.
- [ ] **No value was invented for Tidal Barrier's Shield magnitude**
      (`CARD_RULES.md` §4.1, TASK-104 B-4): the row encodes only what §4.1
      defines, or the task STOPs.
- [ ] Percentage-of-MaxHP and flat magnitudes remain **distinguishable** — the
      Shield/Heal proportion is not pre-resolved to a number, and Power
      Charge's effect magnitude is not conflated with its Cost.

### Validation

- [ ] Validation **rejects** an unknown effect identity, a missing value, an
      unsupported value interpretation, and any malformed/incomplete payload —
      each proven by a test.
- [ ] Validation **fails loudly rather than defaulting**: no fallback value, no
      prose fallback, and no silent no-op for unrecognized data.

### The scope boundary

- [ ] **No CardCast execution exists**: no hub method, no Application cast use
      case, no `BattleEventType` member, no wire-projection arm.
- [ ] **No effect resolver mutates battle state**: no call site invokes
      `ApplyHeal`, `ApplyShield`, or `ApplyPower` from Card content.
- [ ] **No PetSkillCast implementation exists** and no Pet Skill effect is
      executed.
- [ ] `BattleHub.cs`, `BattleEvent.cs`, `BattleEventWireProjection.cs`,
      `StatusEffectLifecycle.cs`, and `DamagePipeline.cs` are **byte-identical**.
- [ ] No `BattleState` / `PetState` / `BossState` member is added; no Redis key,
      SignalR method, event, wire member, or API endpoint is added.
- [ ] No new SignalR method exists; the hub's method set is unchanged
      (`JoinBattle`, `Swap`, `Ping`).

### General

- [ ] **All existing tests remain green unmodified**, and the new tests pass —
      Domain, Application, Infrastructure, and Api suites.
- [ ] Build is green with zero new errors and zero new warnings.
- [ ] **Zero ADRs** created or edited; no ADR was required.
- [ ] TASK-082, TASK-102, TASK-103, TASK-104, TASK-105, TASK-106, TASK-107,
      TASK-108, TASK-036, TASK-079, TASK-099, and every `tasks/completed/*` file
      are **byte-identical**.
- [ ] Quality review checklist passes (`quality/review.md` §1).
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / `ADR-001`);
      zero client-authoritative logic introduced.

### Explicit Constraints

```text
No CardCast, no PetSkillCast.
No effect execution, no resolver that mutates battle state.
No invented balance value; CARD_RULES.md §2/§4.1 is the only value source.
No use of TASK-108's illustrative example values.
No Shield/Heal/Power semantic change.
No Relic EffectDefinition change (R2-7 remains in force for Relics).
No new BattleEvent, SignalR member, Redis key, or API endpoint.
No BattleState/PetState member.
No effect framework, handler registry, or plugin system.
No docs/ change beyond the DATABASE.md supersession record.
No modification of any DONE task or TASK-107/TASK-108.
No ADR (a required ADR is a STOP).
```

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Record a STOP report here instead if a §"Stop Conditions" condition fires —
  do not claim a resolution that was not made, and do not invent a value.
-->

**Picker-up note.** Record before editing: (a) the SHA256 of
`docs/02-technical/DATABASE.md`, `docs/01-game-design/CARD_RULES.md`,
`docs/01-game-design/COMBAT_RULES.md`, `src/backend/GameServer.Domain/Cards/CardDefinition.cs`,
`src/backend/GameServer.Infrastructure/Postgres/Configurations/CardDefinitionConfiguration.cs`,
`src/backend/GameServer.Infrastructure/Postgres/Migrations/20260929152651_ProvisionPetCardRelicContentDefinitions.cs`,
and `tasks/backlog/TASK-107-implement-basic-cardcast-server-path.md` as the
change-isolation baseline; (b) the output of `git status`; (c) the pre-edit
`dotnet test` counts for the Domain, Application, Infrastructure, and Api
projects. The working tree carries uncommitted changes from earlier tasks, so
the baseline is the working-tree content at pickup, **not** `HEAD`.

### Structured Representation (as implemented)

```text
Effect identity (D-1):   <the member/vocabulary that carries it>
Value (D-2):             <the member(s) carrying value + interpretation>
Serialization form:      <the chosen form, and why it is the simplest that
                          satisfies D-1/D-2>
Validation rules:        <what is rejected and how it fails>
```

### Persistence Decision (as implemented)

```text
Column before:  effectDefinition  character varying(128)  NOT NULL  (prose)
Column after:   <type/size>  <nullability>  <why this type>
EF mapping:     <the configuration change>
Migration:      <name>  — <column change + content migration>
Reversibility:  <how Down restores the prior representation>
Relic column:   UNCHANGED (R2-7 remains in force for RelicDefinition)
```

### Supersession Record (TASK-082 R2-7 → TASK-108 D-1/D-2)

```text
Superseded decision:  TASK-082 R2-7 (DONE, immutable) — Card AND Relic
                      EffectDefinition = verbatim prose, no effect-id
                      vocabulary.
Superseded for:       CardDefinition.EffectDefinition ONLY.
Remains in force for: RelicDefinition.EffectDefinition (ROADMAP.md Phase 2;
                      no decision, no consumer, no change made).
Recorded in:          docs/02-technical/DATABASE.md §1 CardDefinition block
                      + version header (v… → v…).
Stated explicitly:    <quote the supersession sentence added to DATABASE.md>
TASK-082 modified:    NO — byte-identical (<hash>).
```

### Row Content Migration (per row, transcribed from CARD_RULES.md)

```text
CardDefinitionId     prose (before)                structured (after)     source
card-heal            <prose>                       <structured>           CARD_RULES.md §2
card-shield          <prose>                       <structured>           CARD_RULES.md §2
card-power-charge    <prose>                       <structured>           CARD_RULES.md §2
card-inferno         <prose>                       <structured>           CARD_RULES.md §4.1
card-tidal-barrier   <prose>                       <structured / partial> CARD_RULES.md §4.1
card-iron-fang       <prose>                       <structured>           CARD_RULES.md §4.1
Tidal Barrier Shield magnitude: <NOT AUTHORED — how the row was handled
                                  without inventing it, per Stop Condition 2>
```

### Changed Files
- `<file path>` — <summary of change>

### Validation Results
- `<test command or suite>` — PASS (<N> tests)  [Domain / Application /
  Infrastructure / Api]
- Migration determinism — <PASS: the six rows' asserted structured values>
- Serialization round trip — <PASS: lossless, deterministic>
- Validation rejection cases — <PASS: N cases>
- Regression (Card loadout / collection / battle-start smoke) — <PASS>
- Negative scope assertions — <PASS: no CardCast method, no new BattleEventType,
  no ApplyHeal/ApplyShield/ApplyPower call site from Card content,
  RelicDefinition unchanged>
- Scope validation (`MVP_SCOPE.md` §1/§2) — <PASS>
- Documentation consistency audit — <PASS: DATABASE.md vs CARD_RULES.md vs the
  implemented representation; no rule duplicated>

### Contract Preservation Verification

```text
Card rules             UNCHANGED (CARD_RULES.md byte-identical; every encoded
                         value transcribed from it verbatim)
Card balance values    UNCHANGED (no Cost or magnitude altered; TASK-108's
                         illustrative values absent)
Shield contract        UNCHANGED (COMBAT_RULES.md §4 byte-identical; TASK-105's
                         refresh/one-instance/no-accumulation/depletion intact)
Shield representation  UNCHANGED (StatusEffect untouched; ApplyShield untouched)
Damage Pipeline        UNCHANGED
BattleState schema     UNCHANGED (no new member; PetState/BossState untouched)
CardDefinition other   UNCHANGED (Id/Name/Category/PowerCost/LoadoutCopyLimit,
                         PlayerUnlockedCard join, SignatureSkillCardId FK)
RelicDefinition        UNCHANGED (EffectDefinition still R2-7 verbatim prose)
Redis / SignalR / API  UNCHANGED (no key, method, member, or endpoint)
PostgreSQL             CHANGED (this task: one new migration + the EF mapping;
                         DATABASE.md §1 Card block + version header updated)
src/ and tests/        CHANGED as scoped; Api/ and Domain/Battle/ and
                         Domain/Combat/ byte-identical
docs/                  CHANGED (DATABASE.md only — the Card-scoped R2-7
                         supersession)
docs/03-decisions/     UNCHANGED (0 ADRs)
TASK-082/102/103/104/105/106/107/108, TASK-036/079/099, tasks/completed/
                       UNCHANGED (byte-identical)
PetSkillCast           NOT IMPLEMENTED (no method, no event, no projection)
Tidal Barrier magnitude NOT AUTHORED (still deferred, TASK-104 B-4)
```

### Server Authority & Scope Verification
- [ ] Confirmed zero client-authoritative gameplay logic introduced
- [ ] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1); Relics (Phase 2) not
      reached
- [ ] Confirmed no effect execution: no call to ApplyHeal / ApplyShield /
      ApplyPower from Card content
- [ ] Confirmed no CardCast and no PetSkillCast implementation
- [ ] Confirmed no new event, wire member, state field, SignalR method, Redis
      key, or API endpoint
- [ ] Confirmed no gameplay balance changed and no value invented
- [ ] Confirmed TASK-082 R2-7 superseded for Cards only, recorded explicitly,
      with the Relic half left in force

### Unrelated Stale Documentation Discovered (REPORTED, NOT CHANGED)
- <item, location, impact, suggested follow-up task — per `AGENTS.md` §16>
