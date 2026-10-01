# TASK-111 — Resolve the Multi-Effect and Vocabulary Contract for Structured Card `EffectDefinition`

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and tasks/ by path and section — it copies no rule,
  formula, schema, or effect value.

  THIS TASK DECIDES NOTHING BY ITSELF AND IMPLEMENTS NO BEHAVIOR. It
  presents the documented evidence of a genuine contract gap that
  TASK-110 was required to REPORT rather than solve (TASK-110 "Reported
  Consequences" item 2), obtains the Product Owner's explicit answers,
  records them verbatim (TASK-104 / TASK-108 precedent), and corrects
  the canonical contract statements those answers make true or false.

  THE GAP, STATED WITHOUT SOLVING IT: TASK-109's structured contract
  gives one `CardDefinition` row exactly one `effectType`/`valueType`/
  `value` triple from closed sets (DATABASE.md §1 items 1–2, §3), while
  every Pet Skill Card in CARD_RULES.md §4.1 states TWO effects, two of
  which (damage-dealing, Burn, Crit) have no member in the closed sets
  at all, and whose authored magnitudes (per-tick rate + Turn duration;
  percentage-point scope) have no carrier in the `valueType` set
  (Flat | PercentMaxHp | Undetermined). DATABASE.md §1 also still
  states that §4.1 authors no magnitude for these Cards — a statement
  TASK-110 made stale.

  PROVENANCE: TASK-110 "Reported Consequences" item 2 prescribes "a
  contract decision task" and states the vocabulary question and the
  multi-effect question "must be decided together". TASK-109 Stop
  Condition 2 and TASK-108 D-4 recorded the same class of gap.

  THIS IS A STOP-DRIVEN DECISION-INPUT TASK (AGENTS.md §7/§20), not a
  feature. Choosing an effectType string, a valueType carrier, a
  multi-effect shape, or an ordering rule without a recorded owner
  answer is the single prohibited action of this task.

  BOUNDARY (explicit, binding):
    No source code.
    No database schema change and no database migration.
    No CardCast implementation.
    No PetSkillCast implementation.
    No gameplay behavior changes.
    No undocumented effect vocabulary (every member name in the
      decided contract must trace to a recorded owner answer).
    No client-authoritative state (ADR-001 / GAME_RULES.md §18).
    No new task files — this is the only task this task may create.
-->

---

## Metadata

```text
Task ID:           TASK-111
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; the
                   deliverable is a recorded contract decision plus its
                   correction in the canonical owner document. See "Type
                   classification note". Code/schema/row consequences are
                   REPORTED as named follow-up tasks, never applied here.)
Status:            DONE (all eight decisions D-1…D-8 now answered by the Product
                   Owner and recorded verbatim, with date and author; coverage
                   **8 of 8** — the two items that held the previous BLOCKED
                   state, D-2 and D-3, were supplied in a later session and are
                   recorded below; the `DATABASE.md` contract statements those
                   answers determine are corrected, and the representability
                   walk-through now passes for all six provisioned Cards.
                   Lifecycle, in order: `backlog/` → `active/` → `blocked/`
                   (0 of 8) → `active/` → `blocked/` (6 of 8) → `active/`
                   (8 of 8) → this file is the DONE record. Prior blocked
                   states and the STOP CONDITION REPORT they carried are
                   RETAINED below, marked RESOLVED, so the decision trail is
                   intact (`TASK_LIFECYCLE.md` §3 — completed tasks are
                   immutable; this file records its own progression, and no
                   other task was edited.)
                   Zero files under `src/` or `tests/` changed; no schema, no
                   migration, no database row changed; every game-design
                   document and every wire/API/state contract byte-identical.
                   See "Product Owner Decisions", "Decision Register",
                   "Representability Walk-Through", "Changed Files" and
                   "Validation Results" below.)
Risk:              MEDIUM (TASK_TYPES.md §4 — DOCUMENTATION baseline LOW–MEDIUM;
                   MEDIUM because the decided contract governs how every future
                   multi-effect Card is stored and resolved, it corrects
                   statements TASK-109 just wrote, and an incautious answer
                   could collide with the frozen Damage Pipeline, Burn, Crit,
                   and Shield contracts. No gameplay rule is changed here.)
Priority:          HIGH (the reported blocker on (a) TASK-110 "Reported
                   Consequences" item 1 — the CardDefinition row-content
                   encoding follow-up, and (b) PetSkillCast effect resolution /
                   TASK-102's three Pet Skill Card acceptance criteria —
                   TASK-108 Follow-Up item 2. It does NOT block TASK-107 or the
                   Phase 1 "3 Basic Cards" slice: §2's three Basic Cards are
                   fully representable under the current contract.)
Primary Agent:     review (TASK_TYPES.md §2 / AGENT_SELECTION.md §1 —
                   "Documentation change: Primary Agent Review". No domain
                   agent may author the answer; the Product Owner supplies it.)
Supporting Agents: gameplay (CARD_RULES.md §4.1 is the owning domain document
                   for the effect content the contract must be able to express
                   — consulted to CONFIRM expressibility requirements, never to
                   author vocabulary or alter a value),
                   backend (the Domain CardEffectType / CardEffectValueType /
                   CardEffectDefinition types and the strict deserializer —
                   consulted to state accurately what the current contract does
                   and does not carry),
                   persistence (DATABASE.md §1/§3 storage shape and the
                   provisioned rows — consulted on what a decided shape implies
                   for the jsonb value and the six rows; any migration is a
                   REPORTED follow-up, never applied here),
                   testing (scenario derivation for whichever follow-up
                   implements the decided contract)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   backend/persistence-analysis,
                   quality/scope-validation
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-109 (DONE — the structured CardEffectDefinition contract
                      and its Row Content Migration table this gap lives in;
                      IMMUTABLE, read/cite only),
                   TASK-110 (DONE — the §4.1 magnitudes the contract must now
                      carry, and the Reported Consequences item 2 that
                      prescribes this task; IMMUTABLE, read/cite only),
                   TASK-108 (IN REVIEW per this task's recorded metadata. NOTE —
                      the status VALUE is accurate but the FOLDER is not: at
                      pickup the file is `tasks/backlog/TASK-108-…md` while its
                      own `Status:` field reads `IN REVIEW`, whereas
                      `TASK_LIFECYCLE.md` §3 places an IN REVIEW task in
                      `tasks/active/`. NOT A BLOCKER for TASK-111 — this task
                      reads TASK-108 as a read-only precedent, never writes to
                      it, and the decision register it holds is unaffected by
                      which folder the file sits in. RECORDED, NOT MODIFIED:
                      moving or re-statusing another task is the
                      orchestrator's/reviewer's act (`TASK_LIFECYCLE.md` §3/§4;
                      this task's own Boundary — "no status transition on any
                      task but this task's own"). Separately reported in the
                      Completion Evidence.),
                   TASK-082 (DONE — decision R2-7; still governs
                      RelicDefinition.EffectDefinition and is immutable),
                   TASK-104 (READY — the Product Owner decision-register
                      precedent for verbatim answers with date and author;
                      read-only),
                   TASK-102 (READY — CardCast server path whose three Pet Skill
                      Card acceptance criteria stay unassertable until the
                      decided contract is encoded; read-only),
                   TASK-107 (BACKLOG — Basic CardCast implementation; UNAFFECTED
                      by this task — read-only, NOT re-scoped, NOT re-statused)
Blocks:            The CardDefinition row-content encoding follow-up named by
                   TASK-110 "Reported Consequences" item 1 (it cannot encode
                   damage/Burn/Crit or two-effect rows under the current
                   contract), and PetSkillCast effect resolution /
                   TASK-102's three Pet Skill Card acceptance criteria
                   (TASK-108 Follow-Up item 2). It does NOT block TASK-107,
                   TASK-036, TASK-079, or TASK-099. Follow-ups are NAMED in
                   Completion Evidence, not created.
Estimate:          Normal (8 decision questions across 3 authoritative docs plus
                   their registers; one bounded doc correction; zero code,
                   zero tests — tasks/README.md §12)
```

**Type classification note.** `DOCUMENTATION`, not `GAMEPLAY-CHANGE` and not
`ARCHITECTURE`. No gameplay rule is being authored: the Burn amount, duration,
Crit magnitude, damage magnitudes, Shield semantics, and Damage Pipeline already
exist (`CARD_RULES.md` §4.1, `COMBAT_RULES.md` §3/§3.3/§4/§5, `GAME_RULES.md`
§17) — this task decides only how the already-decided content is
**represented in storage**, which is `DATABASE.md`'s ownership. It is not
`ARCHITECTURE`: no layer, storage technology, realtime strategy, or
authoritative model changes (the jsonb column, the reader paths, and ADR-006's
role are unchanged by the decision itself); if the recorded answer nonetheless
implies an ADR, that is Stop Condition 5. It is not `FEATURE`: nothing is
built. `TASK-108` — the immediately preceding task for the same contract — set
this exact precedent (`Type: DOCUMENTATION`, `documentation/documentation-change.md`,
`Primary Agent: review`). If a decision requires a gameplay rule or a value
change, Stop Condition 3 fires and a separate GAMEPLAY-CHANGE task is reported,
never executed here.

---

## Objective

Obtain the Product Owner's explicit decisions for every open question of the
structured `CardDefinition.EffectDefinition` contract raised by TASK-110's
Reported Consequences item 2 — multi-effect representation, the `effectType`
closed set, the `valueType` carrier vocabulary, the damage carrier, effect
ordering, row-compatibility, canonical ownership, and follow-up authorization —
record each answer verbatim with date and author in this task's decision
register, then correct the affected contract statements in
`docs/02-technical/DATABASE.md` (§1 Card `EffectDefinition` contract, §3
constraints, and the stale §4.1 references TASK-110 made) so documentation
states the decided contract, **without** changing any source code, schema,
migration, provisioned row, game-rule document, event or wire contract, or
creating any additional task file.

> **Final rule for the executing agent:** present each question neutrally with
> its citations, obtain the owner's explicit answer, record it verbatim with
> date and author (TASK-104 / TASK-108 precedent), then write only what the
> answers decide. Do not recommend an option, do not rank candidates, do not
> invent an `effectType` string, `valueType` carrier, member name, shape, or
> ordering rule, and do not fill an unanswered item by inference
> (`AGENTS.md` §7, §20). If any item in §"Required Decision Coverage" is left
> unanswered, record which items remain unanswered and keep this task BLOCKED.

---

## Current State

- `docs/02-technical/DATABASE.md` **§1 "Card `EffectDefinition` contract"
  (items 1–7)** defines the landed contract: one `jsonb` value holding
  **exactly three members** (`effectType`, `valueType`, `value`); closed sets
  `effectType ∈ {Heal, Shield, Power}` and `valueType ∈ {Flat, PercentMaxHp,
  Undetermined}`; rejection without default for anything unrecognized
  (items 1, 2, 5); and the statement that "a fourth [effect identity] is a
  gameplay rule this document does not author". **§3** restates both closed
  sets as constraints. Six `CardDefinition` rows were migrated to it
  (TASK-109 Row Content Migration table): the three §2 Basic Cards carry full
  triples; the three §4.1 Pet Skill Cards each carry **one** `effectType` with
  `valueType: "Undetermined"` and no `value`.
- `docs/01-game-design/CARD_RULES.md` **§4.1 (v1.5)** now authors every Pet
  Skill magnitude (TASK-110 D-1…D-6): Inferno and Iron Fang each state a
  damage effect **plus** a second effect (Burn; Crit), and Tidal Barrier states
  Heal **plus** Shield. `DATABASE.md` §1 items 6–7 still say "§4.1 authors no
  magnitude for Inferno or Iron Fang and **no Shield magnitude at all** for
  Tidal Barrier" and describe a still-open content gap — **stale statements**
  TASK-110 created; they must be corrected to reference the authored §4.1.
- **The gap (reported, not solved):** a row carries a single effect, so no §4.1
  Card is representable today (two effects each); `Damage`, `Burn`, and `Crit`
  are absent from the `effectType` set; the authored Burn magnitude
  (per-tick rate + Turn duration) and Crit magnitude (percentage points +
  next-attack scope) have no carrier in `valueType`, per TASK-110 Reported
  Consequences item 2. The three rows remain `Undetermined` and "not
  resolvable" (`DATABASE.md` §1 items 5, 7) — no value was or may be invented.
- **The frozen runtime the decided contract must feed, not change:**
  `COMBAT_RULES.md` §3 (Damage Pipeline, fixed order) / §3.3 (Crit) / §4
  (Shield refresh-not-stack) / §5.1–§5.2 (Burn ticks); `GAME_RULES.md` §17
  (fixed resolution order; step 19a End-Turn Burn tick); `GAME_STATE.md` §2.3.1
  (`StatusEffects[]` instance schema — `Type`, `Magnitude`, `RemainingTurns`);
  `PASSIVE_RULES.md` §8 (Iron Fang's Crit value is independent of Bạch Hổ's
  Passive config, `CARD_RULES.md` §4.1 closing note).
- Code already implements the landed contract (TASK-109): Domain
  `CardEffectType` / `CardEffectValueType` / `CardEffectDefinition` with strict
  deserialization and loud rejection; `CardDefinition.EffectDefinition` is a
  jsonb-mapped value. Nothing here may change it.
- Blocked work: TASK-110 Reported Consequences item 1 (row encoding) and
  PetSkillCast effect resolution (TASK-108 Follow-Up item 2). TASK-107
  (Basic Cards) is **not** blocked.

---

## Authoritative References

### The gap this task resolves (READ — the prescribed input)

- `tasks/completed/TASK-110-author-pet-skill-card-effect-magnitudes.md` —
  **"Reported Consequences" items 1 and 2** (the contract gap, the
  must-decide-together note, the named encoding follow-up) and the **D-1…D-6
  decisions** (what the contract must carry; IMMUTABLE).
- `tasks/completed/TASK-109-implement-structured-effectdefinition-contract.md` —
  **"Row Content Migration" table** and **Stop Condition 2** (the `Undetermined`
  rows and why they exist; IMMUTABLE).

### The contract being extended (the doc this task corrects)

- `docs/02-technical/DATABASE.md` **§1 "Card `EffectDefinition` contract"
  items 1, 2, 5, 6, 7** — single triple, closed sets, loud rejection,
  supersession scope, `Undetermined` rows, and the stale §4.1 references.
  **§3 lines for `CardDefinition.EffectDefinition.*`** — constraint restatements.
  **Version header (item 1 summary)** — restates the structured contract.
  This document owns the storage shape (`tasks/README.md` §9); it is the
  expected write target. Any part of the answer that belongs to a game-design
  document is out of scope for an edit here (Stop Condition 3).

### The content the contract must express (READ ONLY — values are frozen)

- `docs/01-game-design/CARD_RULES.md` **§4.1** — the authored Pet Skill effects
  and magnitudes (structure and values owned by TASK-110; **byte-identical, not
  editable by this task**), **§4 item 4** (Card damage enters the Damage
  Pipeline), **§2** (the three Basic Cards already representable), **§3** (Card
  cast — unimplemented concern, unchanged).
- `docs/01-game-design/COMBAT_RULES.md` **§3 / §3.1 / §3.3** (Damage Pipeline
  step 1 = Card base value; Crit), **§4** (Shield), **§5.1 / §5.2 / §5.3**
  (Burn ticks, status rules, duration consumption timing).
- `docs/01-game-design/GAME_RULES.md` **§17** (fixed resolution order, step
  19a), **§18** (server authority), **§12** (Power).
- `docs/02-technical/GAME_STATE.md` **§2.3.1** — `StatusEffects[]` runtime
  instance schema the stored effect must eventually produce.

### Precedents and constraints (READ / CITE — none may be modified)

- `tasks/backlog/TASK-108-resolve-card-effect-resolution-contract.md` —
  decision-input precedent: **"Product Owner Decisions" register format**,
  D-1/D-2 (single triple, data-driven magnitude), D-6 (balance-value boundary).
- `tasks/backlog/TASK-104-collect-cardcast-wire-and-shield-contract-decisions.md`
  — the verbatim-answer-with-date-and-author register precedent (READY).
- `tasks/completed/TASK-082-…md` decision **R2-7** — immutable; still governs
  `RelicDefinition.EffectDefinition` only (DATABASE.md §1 item 6).
- `docs/00-overview/MVP_SCOPE.md` **§1** — Cards and Combat/Status Effects are
  IN; no new system is introduced.
- `docs/03-decisions/README.md` **§8** and **ADR-001 / ADR-006** — checked by
  the executor before concluding no ADR is required (Stop Condition 5).

---

## Decision Questions

All eight questions are **contract/product decisions**. None can be answered
from `docs/` today — that is the gap. An agent presents each question with the
citations below and records the owner's answer; an agent never supplies one.

```text
D-1  MULTI-EFFECT REPRESENTATION
     Every §4.1 Pet Skill Card states two effects, and CARD_RULES.md §4/§4.1
     does not restrict future Cards to one. DATABASE.md §1 item 1 holds
     exactly one triple per row (one jsonb value, one property). How is a
     Card with more than one effect represented in
     CardDefinition.EffectDefinition — what is the stored shape (e.g. a
     single object whose members are per the decision, a sequence of effect
     objects, or another structure), and does each effect keep its own
     effectType/valueType/value triple inside that shape? State the shape
     precisely enough that a migration and a strict reader could implement
     it without further guessing.

D-2  effectType CLOSED SET
     DATABASE.md §1 item 2 fixes {Heal, Shield, Power} and states "a fourth
     is a gameplay rule this document does not author"; §4.1 now documents
     damage-dealing, Burn, and Crit effects (TASK-110). Does the closed set
     extend, and what are the EXACT member strings for the effects §4.1
     names (damage base value; Burn; Crit increase; and confirmation that
     Heal/Shield/Power remain)? Which document owns the set — DATABASE.md §1
     as storage vocabulary, or CARD_RULES.md as effect identity, or both at
     their respective layers? Any string not spoken here may not be used.

D-3  valueType CARRIERS FOR THE AUTHORED MAGNITUDES
     The set {Flat, PercentMaxHp} interprets §2's two forms. The authored
     §4.1 magnitudes additionally express: a per-Tick rate with a Turn
     duration (Burn), and a percentage-POINT adjustment scoped to the next
     attack (Crit). What represents each — new valueType member(s), extra
     member(s) alongside value, or another carrier — and what exactly is
     carried (unit, duration, scope)? Does `Undetermined` remain the
     open-content-gap marker unchanged? Name every new member verbatim.

D-4  DAMAGE CARRIER AND THE "Power" MEMBER
     Inferno/Iron Fang deal damage whose authored magnitude is the Card base
     value entering COMBAT_RULES.md §3 step 1. How is a damage effect stored
     so a resolver reads the base value from content (not from Card name or
     CardDefinitionId mapping, TASK-108 D-1), and how does the vocabulary
     distinguish a damage-dealing effect from the existing `Power` member,
     which denotes Power Charge (CARD_RULES.md §2)? Give the exact member
     name(s) for the damage effect.

D-5  EFFECT ORDERING
     With two effects per Card, is the stored sequence meaningful — does the
     order in which a Card's effects resolve matter, and where is that order
     fixed: the stored shape (DATABASE.md), the resolution order
     (GAME_RULES.md §17 / CARD_RULES.md §4), or nowhere (all orders
     equivalent)? If fixing an order requires a NEW gameplay rule, say so
     explicitly — that answer is reported as a separate GAMEPLAY-CHANGE
     follow-up, not written into a game-design doc here (Stop Condition 3).

D-6  ROW COMPATIBILITY AND THE THREE BASIC CARDS
     The six migrated rows exist today as single triples (TASK-109). Under
     the decided shape, do the three §2 Basic Card rows stay as they are
     (compatible) or require re-encoding, and does that decision also retire
     the three rows' `Undetermined` markers (their magnitudes are now
     authored), or does that stay with the separate encoding follow-up
     (TASK-110 Reported Consequences item 1)? State which rows, if any, this
     task's decision makes restatable and which the follow-up owns.

D-7  CANONICAL OWNERSHIP
     For each part of the decided contract — stored shape, effect vocabulary,
     value-interpretation vocabulary, effect magnitudes (already
     CARD_RULES.md §4.1), and runtime effect application (already
     COMBAT_RULES.md / GAME_STATE.md §2.3.1) — which document is the single
     owner, and which statements in DATABASE.md §1/§3 (and its version
     header) must change to match? This determines exactly what this task
     may edit and what it must report instead.

D-8  FOLLOW-UP AUTHORIZATION
     Which implementation consequences follow from the recorded decisions and
     to what kind of task are they reported: (a) Domain enum/type +
     deserializer/validation change, (b) EF mapping + migration + row
     re-encode of affected CardDefinition rows, (c) resolver application of
     damage/Burn/Crit effects and PetSkillCast, (d) any GAME_EVENTS /
     SIGNALR_PROTOCOL / API_CONTRACTS consequence (report only), (e) ADR if
     one is required? Confirm none of these is executed by this task, and
     confirm this task creates no additional task file.
```

---

## Required Decision Coverage

Every item below must be satisfied before this task can leave BLOCKED. Coverage
is binary; partial coverage is recorded and the task stays BLOCKED
(`AGENTS.md` §7/§20; TASK-110 precedent).

```text
[ ] D-1 answered verbatim with date and author — stored shape for 2+ effects
[ ] D-2 answered verbatim — complete effectType member list + owning document
[ ] D-3 answered verbatim — carrier for Burn (rate/duration) and Crit
      (percentage points/scope); Undetermined disposition confirmed
[ ] D-4 answered verbatim — damage effect member name(s) + how `Power` is
      distinguished
[ ] D-5 answered verbatim — ordering meaningful or not + where fixed; any new
      gameplay rule explicitly flagged for a GAMEPLAY-CHANGE follow-up
[ ] D-6 answered verbatim — Basic Card row compatibility + Undetermined
      disposition (this task vs encoding follow-up)
[ ] D-7 answered verbatim — owner document per contract part + the exact
      DATABASE.md statement set to correct
[ ] D-8 answered verbatim — follow-up task kinds (a)–(e) authorized/rejected;
      none executed here; no additional task file created
[ ] Representability walk-through completed for all six provisioned Cards
      (§2 × 3, §4.1 × 3): each effect maps to the decided representation
      using only recorded member names — gaps reported, never filled
[ ] A question left unanswered ⇒ listed by ID with Status BLOCKED and a
      recorded reason — not inferred, not defaulted, not "assumed yes"
```

---

## Scope

### In Scope

- Present D-1…D-8 neutrally with citations; obtain the Product Owner's explicit
  answers; record each verbatim with date and author in a decision register in
  this file (TASK-104 / TASK-108 precedent).
- Correct `docs/02-technical/DATABASE.md` — §1 Card `EffectDefinition` contract
  (items 1, 2, 5, 6, 7 as the answers affect them), the §3 constraint lines for
  `CardDefinition.EffectDefinition.*`, and the version-header summary — so the
  document states the decided contract, and refresh the now-stale statements
  that "§4.1 authors no magnitude" / names an open content gap, replacing them
  with references to `CARD_RULES.md` §4.1 as authored by TASK-110 (a factual
  correction; no value is restated as a second source).
- Record, per D-7, which contract parts are owned by other documents — as
  references, without editing them.
- Report (never implement, never create) the follow-up tasks authorized by D-8,
  each with type, inputs, and owning area, in Completion Evidence.
- Perform and record change-isolation verification (SHA-256 baseline for the
  authoritative docs touched or guarded) and the representability walk-through.

### Out of Scope

- **No source code** — no `src/` file, no Domain type, no enum, no
  `CardEffectDefinition`, no deserializer/validator, no EF configuration.
- **No database schema change, no migration, no seed/row change** — the six
  provisioned rows (including the three `Undetermined`) are untouched in
  PostgreSQL.
- **No CardCast implementation and no PetSkillCast implementation** — no
  resolver, no `ApplyHeal`/`ApplyShield`/`ApplyPower`, no damage/Burn/Crit
  application, no `StatusEffect` runtime change.
- **No gameplay behavior changes** — `CARD_RULES.md` (§4.1 values included),
  `COMBAT_RULES.md`, `GAME_RULES.md`, `PASSIVE_RULES.md`, `ELEMENT_RULES.md`,
  `PET_RULES.md`, `BOSS_RULES.md` are byte-identical; if an answer needs one of
  them edited, Stop Condition 3 fires.
- **No undocumented effect vocabulary** — no `effectType`, `valueType`, member,
  shape, or ordering rule originates anywhere but a recorded answer.
- **No client-authoritative state** and no event/wire/state contract change —
  `GAME_EVENTS.md`, `SIGNALR_PROTOCOL.md`, `API_CONTRACTS.md`,
  `GAME_STATE.md`, `REDIS_STATE.md` byte-identical; consequences are reported
  under D-8(d).
- **No additional task file created** (exactly one new task: this one), no
  status transition on any task but this task's own, no modification of
  TASK-082 / TASK-104 / TASK-108 / TASK-109 / TASK-110 / TASK-102 / TASK-107.
- **No ADR authored** here; if one is required, Stop Condition 5 fires.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Acceptance Criteria

- [ ] All eight decision items D-1…D-8 are recorded verbatim with date and
      author, each under a labeled register entry; any unanswered item is
      listed by ID with Status BLOCKED and its reason (no inference, no
      default, no "unanimous assumption").
- [ ] Vocabulary traceability: a table maps every `effectType`/`valueType`/
      member/shape element in the decided contract to the exact recorded
      answer that introduced it; zero entries lack a source (no agent-invented
      vocabulary — `AGENTS.md` §7).
- [ ] Representability walk-through: each of the six provisioned Cards (§2 × 3,
      §4.1 × 3) has a row showing Card → effects → decided representation →
      owning document section, with any non-representable part reported as an
      open item rather than filled.
- [ ] `docs/02-technical/DATABASE.md` §1 items 1/2/5/6/7, the §3
      `CardDefinition.EffectDefinition.*` constraint lines, and the version
      header state the decided contract; every changed line cites a recorded
      decision ID; the stale "§4.1 authors no magnitude" / open-gap statements
      are corrected to reference `CARD_RULES.md` §4.1 as authored by TASK-110.
- [ ] Change isolation: SHA-256 recorded at pickup and re-verified at
      completion — changed-file set is exactly {this task file,
      `docs/02-technical/DATABASE.md`}; `CARD_RULES.md`, `COMBAT_RULES.md`,
      `GAME_RULES.md`, `GAME_STATE.md`, `GAME_EVENTS.md`,
      `SIGNALR_PROTOCOL.md`, `API_CONTRACTS.md`, `REDIS_STATE.md`, and every
      other authoritative doc are byte-identical.
- [ ] Zero files under `src/` and `tests/` changed; no migration file added;
      the provisioned `CardDefinition` rows are unchanged (the three Pet Skill
      rows still read `valueType: "Undetermined"` in PostgreSQL).
- [ ] TASK-082, TASK-102, TASK-104, TASK-107, TASK-108, TASK-109, TASK-110 are
      byte-identical (hash-checked); this task made no status transition on
      any task but its own.
- [ ] Follow-up report complete: consequences (a)–(e) from D-8 are each stated
      as reported items with type and owning area, named but not created;
      exactly one new task file exists in the repository (this one).
- [ ] No frozen contract contradicted: Damage Pipeline (COMBAT_RULES.md §3),
      Crit (§3.3), Shield refresh-not-stack (§4), Burn timing (§5.1–§5.2 /
      GAME_RULES.md §17 step 19a), `StatusEffects[]` (GAME_STATE.md §2.3.1),
      and Iron Fang's independence from `PASSIVE_RULES.md` §8 are cited as
      unchanged constraints and each was re-verified after the edit.
- [ ] All relevant tests pass at the required validation depth
      (`core/validation.md` §2) — or the N/A justification in Testing
      Requirements is recorded (zero code/test changes).
- [ ] Quality review checklist passes (`quality/review.md` §1).
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001);
      `MVP_SCOPE.md` §1 confirmed — no new system, mechanic, Card, or currency
      introduced.

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)   — NONE
[ ] src/frontend/client/ (scenes / runtime / services / state / ui) — NONE
[ ] tests/ (unit / integration / gameplay scenarios)             — NONE
[x] docs/02-technical/DATABASE.md — §1 Card EffectDefinition contract,
    §3 EffectDefinition constraint lines, version-header summary (decided
    contract + staleness correction)
[x] tasks/backlog/TASK-111-resolve-multi-effect-card-effectdefinition-contract.md
    — decision register (verbatim answers) + Completion Evidence
```

---

## Implementation Notes

- Register format: one block per decision, `ID — question — ANSWER (verbatim) —
  date — author`, exactly as TASK-108's "Product Owner Decisions" and
  TASK-104's register. Date and author are mandatory; an answer without them
  does not count as coverage.
- `DATABASE.md` edit sites (verify line numbers at execution): §1 items 1–7
  (the "exactly three members" statement, the closed-set bullet including the
  "a fourth is a gameplay rule this document does not author" clause, the
  `valueType` bullet, the loud-rejection item, the supersession item's §4.1
  sentences, and the `Undetermined` item), the §3 constraint lines for
  `effectType`/`valueType`/`value`, and the version-header summary. Mark
  changed statements with their decision ID so a reader can trace them; state
  explicitly that implementation is deferred to the D-8 follow-ups (do not
  imply the code already complies).
- Do not restate effect magnitudes in `DATABASE.md` or this file
  (`tasks/README.md` §9) — cite `CARD_RULES.md` §4.1.
- The `RelicDefinition.EffectDefinition` block keeps R2-7 verbatim-prose
  wording untouched (`DATABASE.md` §1 item 6; no Relic decision exists —
  ROADMAP.md Phase 2).
- If D-7 names a game-design document as owner of a statement this task would
  otherwise edit, record it and report the edit as a follow-up — do not edit
  that document (Stop Condition 3).
- Report items for Completion Evidence (named, not created): encoding follow-up
  (TASK-110 RC-1: likely FEATURE + migration), PetSkillCast/resolver
  application (TASK-108 Follow-Up item 2), gameplay-ordering task only if D-5
  requires one, wire/state consequence note only if D-8(d) fires, ADR proposal
  only if Stop Condition 5 fires.

---

## Testing Requirements

### Required Verification

```text
[ ] Unit tests         — N/A justification recorded (zero src/ changes; no
                         behavior to test). If any test file changed, STOP:
                         that is Out of Scope.
[ ] Integration tests  — N/A (no schema, no rows, no API touched). Postgres
                         verification is a read-only assertion that the three
                         Pet Skill rows still read "Undetermined".
[ ] Documentation      — consistency re-verification: DATABASE.md §1/§3 vs the
   scenarios (via         recorded register vs CARD_RULES.md §4.1 vs
   documentation-         COMBAT_RULES.md §3/§3.3/§4/§5 vs GAME_STATE.md
   consistency)           §2.3.1 — no contradiction introduced or left by the
                         edit; staleness corrected.
[ ] Representability    — walk-through table (all six Cards) executed and
                         recorded; unresolved entries reported, not filled.
[ ] Change isolation     — SHA-256 baseline before/after for all guarded docs.
```

### Key Edge Cases

- Two-effect Cards where only one effect was previously stored (Tidal Barrier's
  Heal half has no stored effect at all today) — the walk-through must account
  for **every** effect §4.1 states, not one per Card.
- `Undetermined` semantics: if D-6 leaves any row `Undetermined`, confirm the
  decided contract still forbids a value substitution (`DATABASE.md` §1 items
  5, 7).
- `Power` member collision (D-4): the decided vocabulary must not make the
  Power Charge Basic Card unrepresentable or ambiguous.
- `RelicDefinition.EffectDefinition` must remain byte-identical (R2-7 still
  governs it).
- See `docs/01-game-design/CARD_RULES.md` §4.1 closing note (Iron Fang Crit
  independence) and `COMBAT_RULES.md` §3.3 — the decided Crit carrier must not
  imply editing the Crit formula or the Passive config.

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 always
apply. On any fire: Status → BLOCKED, file moves `active/` → `blocked/`, and
the report is written in this section using `.ai/README.md` §13's exact format
(`tasks/README.md` §10).

- If any D-1…D-8 item cannot be answered by the Product Owner, or a required
  member/shape/ordering value would have to be invented, recommended, ranked,
  or inferred by an agent: STOP per `AGENTS.md` §7 — record the unanswered
  item(s) and stay BLOCKED; never write a guessed vocabulary.
- If any answer requires editing a game-design document
  (`CARD_RULES.md`, `COMBAT_RULES.md`, `GAME_RULES.md`, `PASSIVE_RULES.md`,
  `ELEMENT_RULES.md`, `PET_RULES.md`, `BOSS_RULES.md`) or authoring a new
  gameplay rule (including an effect-resolution order): STOP per `AGENTS.md`
  §7/§17 — report a separate GAMEPLAY-CHANGE task; do not edit those documents
  here.
- If any answer requires code, a schema change, a migration, a row re-encode,
  or an event/wire/state contract change: STOP applying it — record it as the
  corresponding D-8 follow-up report; this task records and corrects docs only.
- If the recorded decisions conflict with an existing ADR, or the executing
  agent concludes an ADR is required for the decided shape: STOP per
  `AGENTS.md` §18 / `docs/03-decisions/README.md` §8 — propose the ADR first;
  author none here.
- If a conflict is detected between `DATABASE.md` and another authoritative
  document that the recorded answers do not resolve: STOP per `AGENTS.md` §4 —
  report both sources; do not silently pick the convenient reading.
- If the task exceeds 7 skills or the scope grows beyond {register,
  `DATABASE.md`, report}: STOP & decompose (`tasks/README.md` §13).
- If task requires client-authoritative game logic calculation: STOP per
  `AGENTS.md` §10 (none is permitted by this task's boundary).

---

## STOP CONDITION REPORT (fired at pickup 2026-10-01; re-issued on the partial answer set; **CLEARED 2026-10-01** — retained as the block's audit trail)

Format per `.ai/README.md` §13 / `core/context-discovery.md` §3
(`tasks/README.md` §10).

```text
*** STATUS OF THIS REPORT: RESOLVED — the stop condition it records has been
cleared. It is retained in full, unaltered below, as the audit trail of the two
blocked states this task passed through (0 of 8 answered, then 6 of 8). The
Product Owner subsequently supplied D-2 and D-3, and the task completed.
Do not read this report as an active block. ***

REVISION HISTORY
  version 1 — written at the initial halt: 0 of 8 answered.
  version 2 — re-issued after session 1: 6 of 8 answered, D-2/D-3 outstanding.
  version 3 — the report's condition CLEARED after session 2 supplied D-2 and
              D-3; coverage reached 8 of 8 and the `DATABASE.md` corrections
              were applied.

Problem (as it stood when the block was live — SUPERSEDED):
  TASK-111 could not complete. Six of its eight required decisions were answered
  and recorded verbatim (D-1, D-4, D-5, D-6, D-7, D-8), but **D-2 and D-3
  remained UNANSWERED**, and those two owned the vocabulary the task's only
  `docs/` write target is made of. The `effectType` member list (D-2) is the
  discriminator every stored row and every future reader dispatches on; the
  `valueType` carriers (D-3) are what make the authored Burn and Crit magnitudes
  expressible at all. Without them, `DATABASE.md` §1 items 1/2/5/6/7 and the §3
  constraint lines could not be rewritten, because their content IS the missing
  vocabulary. Supplying those names — as the executing agent — is the single act
  this task forbids (`AGENTS.md` §7, §20; `GAME_RULES.md` §20; `.ai/README.md`
  §8 "An agent is not a Product Owner and does not decide game design"). Per
  TASK-111 §"Required Decision Coverage", "Coverage is binary; partial coverage
  is recorded and the task stays BLOCKED". The task therefore halted with zero
  `docs/` edits at that time.

  D-4 was answered in PRINCIPLE only at that time: the owner confirmed a
  distinct damage member exists and that `Power` keeps denoting Power Charge,
  but expressly deferred the member's name to D-2. D-2's absence therefore
  propagated into D-4 and the damage effect could not be written into the
  contract either.

  RESOLUTION: session 2 supplied D-2 ("Damage, Burn, Crit" + Heal/Shield/Power
  remain) and D-3 (the `PercentagePoints` valueType plus `duration` and `scope`
  per-effect members; `Undetermined` remains valid). D-4's name was thereby
  resolved as `Damage`. Coverage reached 8 of 8, the `DATABASE.md` statements
  those answers determine were corrected, and the representability walk-through
  passed for all six Cards.

Relevant sources:
  docs/02-technical/DATABASE.md §1 items 1–7 — the LANDED contract being
                          extended: one jsonb value holding exactly three
                          members; effectType ∈ {Heal, Shield, Power} with
                          "a fourth is a gameplay rule this document does not
                          author"; valueType ∈ {Flat, PercentMaxHp,
                          Undetermined}; loud rejection without default. §3
                          restates both closed sets as constraints. §1 items
                          6–7 still state that "§4.1 authors no magnitude for
                          Inferno or Iron Fang and no Shield magnitude at all
                          for Tidal Barrier" — STALE since TASK-110.
  docs/01-game-design/CARD_RULES.md §4.1 — the content the contract must carry
                          (TASK-110 v1.5): Inferno = damage + Burn; Tidal
                          Barrier = Heal + Shield; Iron Fang = damage + Crit.
                          Two effects per Card.
  docs/01-game-design/CARD_RULES.md §2 — the three Basic Cards whose single
                          effect the landed contract represents.
  docs/01-game-design/COMBAT_RULES.md §3 step 1 (Card base value enters the
                          Damage Pipeline), §3.3 (Crit roll — does not exist in
                          MVP), §4 (Shield), §5.1/§5.2 (Burn magnitudes,
                          stacking default), §5.3 (duration consumption).
  docs/01-game-design/GAME_RULES.md §17 step 14 / step 19a (resolution
                          position and the End-Turn Burn tick).
  docs/02-technical/GAME_STATE.md §2.3.1 — the StatusEffect instance schema the
                          stored effect must eventually produce.
  tasks/completed/TASK-109-…md  "Row Content Migration" table — the six rows
                          as landed (three Pet Skill rows carry ONE effectType
                          with valueType "Undetermined" and no value).
  tasks/completed/TASK-110-…md  "Reported Consequences" item 2 — the gap, and
                          the statement that the vocabulary question and the
                          multi-effect question "must be decided together".
  tasks/backlog/TASK-108-…md    D-1/D-2 (structured identity + data-driven
                          magnitude) and "Product Owner Decisions" register
                          format; IN REVIEW, read-only.
  tasks/backlog/TASK-104-…md    §5A "Decision Record" — the verbatim-answer
                          register precedent; READY, read-only.
  docs/03-decisions/README.md §8 — "Known Open Items (Not ADRs)" lists only the
                          backend-runtime assumption and the PRNG algorithm;
                          nothing here is recorded there.

Conflict / missing information:
  *** RESOLVED — coverage is now 8 of 8. The missing information this report
  recorded no longer exists; the report is retained as the audit trail of the
  two blocked states. ***
  Status per item, as of the Product Owner's answers across both sessions
  (see "Product Owner Decisions"):
    D-1 Multi-effect representation        — ANSWERED (array of effect objects;
                                             always an array; each element keeps
                                             its own triple)
    D-2 effectType closed set              — ANSWERED session 2 (Damage, Burn,
                                             Crit; Heal/Shield/Power remain)
    D-3 valueType carriers (Burn, Crit)    — ANSWERED session 2
                                             (PercentagePoints added; `duration`
                                             and `scope` as per-effect members;
                                             Undetermined remains valid)
    D-4 Damage carrier vs. `Power`         — ANSWERED (distinct member, `Power`
                                             keeps meaning Power Charge) and the
                                             name RESOLVED by D-2 as `Damage`
    D-5 Effect ordering                    — ANSWERED (order carries no meaning)
    D-6 Basic Card compatibility           — ANSWERED (compatible; re-encoded by
                                             the downstream task; Undetermined
                                             markers retired there, not here)
    D-7 Canonical ownership                — ANSWERED (DATABASE.md owns stored
                                             shape, vocabulary, value
                                             interpretation "only")
    D-8 Follow-up authorization            — ANSWERED (all of (a)–(e)
                                             authorized; none executed here; no
                                             task file created)
  Verified exhaustive (checked, not assumed):
    - Before the answers arrived, no answer for D-1 … D-8 existed anywhere in
      `docs/` (all 22 documents and all ADRs), `tasks/` (backlog, active,
      blocked, completed), or `.ai/`. TASK-108's register answers only D-1/D-2
      of ITS OWN question set (structured identity; data-driven magnitude) and
      explicitly leaves the multi-effect shape, the fourth `effectType`, the
      Burn/Crit carrier, and the ordering unaddressed — it is the input to this
      task, not its answer. TASK-104's register answers the WIRE and SHIELD
      questions only.
    - The closed sets were closed in the working tree and in the live database
      at pickup (`effectType ∈ {Heal, Shield, Power}`, `valueType ∈ {Flat,
      PercentMaxHp, Undetermined}`), which is what made the gap real rather than
      assumed. The decided extension is authored into `DATABASE.md` by this task
      and is NOT yet encoded in any row, in any domain type, or in any
      migration — the runtime is still frozen at TASK-109's state (no resolver,
      no CardCast, no PetSkillCast, no Burn/Crit execution).
    - No element of the gap was filled by inference at any point
      (`AGENTS.md` §7). While D-2 and D-3 were unanswered the task was BLOCKED
      and no vocabulary was authored; the names and carriers that now appear in
      `DATABASE.md` come verbatim from the Product Owner's session-2 message.
  TASK-111 §"Product Owner Decision Rule" and §"Hard Stop Rule").

Impact:
  *** RESOLVED for this task's own deliverable. *** The `DATABASE.md` contract
  statements the eight answers determine ARE now corrected — the `effectType`
  set, the `valueType` set, the stored array shape, the per-effect extra
  members, the ordering statement, and the stale §4.1 references — and the
  representability walk-through passes for all six provisioned Cards.
  What REMAINS blocked is only the downstream work, exactly as D-8 authorized
  it and this task did not perform it: the CardDefinition row-content encoding
  (TASK-110 Reported Consequences item 1) and PetSkillCast effect resolution /
  TASK-102's three Pet Skill Card acceptance criteria (TASK-108 Follow-Up
  item 2). The three Pet Skill rows still read `valueType: "Undetermined"`, and
  `card-iron-fang` still carries an `effectType` that disagrees with
  `CARD_RULES.md` §4.1 — both are the encoding follow-up's act (D-6/D-6b), not
  this task's. No row, no migration, and no source file was changed here.
  The stale §1 items 6–7 statements WERE corrected, because every answer they
  needed had arrived.

Proposed resolution:
  *** SUPERSEDED — no longer applicable. *** It is retained because it records
  precisely what was asked for and received. The Product Owner supplied both
  items in session 2: D-2 named the three new members (Damage, Burn, Crit) and
  confirmed Heal/Shield/Power remain; D-3 named `PercentagePoints` and the
  `duration` / `scope` per-effect members, and confirmed `Undetermined` remains
  valid. D-4's member name followed from D-2 as `Damage`. TASK-111 then applied
  only the `DATABASE.md` statements those answers determine and completed the
  representability walk-through for all six Cards.

Waiting for:
  *** NOTHING — this task is complete. *** The two items this section named
  (D-2, D-3) were supplied, dated 2026-10-01, attributed to the Product Owner,
  and recorded verbatim. No answer require a NEW gameplay rule: `scope:
  "NextAttack"` stores a rule `CARD_RULES.md` §4.1 and `PASSIVE_RULES.md` §7
  already author, and D-5's "order carries no meaning" authors no resolution
  step. Stop Condition 2 therefore did not fire.
  The downstream work authorized by D-8 remains for separate tasks, named in
  §"Reported Follow-Ups" and created by nobody here.
```

### Unanswered Items Register

Every item below is recorded as **UNANSWERED**, by its own decision ID, with
the coverage it requires. None was inferred, defaulted, assumed, or ranked.

```text
Decision ID:  D-1 — Multi-effect representation
Status:       ANSWERED (2026-10-01, Product Owner)
Answer:       "A sequence/array of effect objects"
              + D-1a: "Yes — each element keeps its own effectType/valueType/
                value triple"
              + D-1b: "Yes — always an array, even with one effect"
Coverage met: the stored shape, stated precisely enough for a migration and a
              strict reader: the `jsonb` value is an ARRAY of effect objects;
              every element carries its own effectType/valueType/value triple;
              the shape is uniform for one-effect and multi-effect Cards alike.
              Recorded verbatim in "Product Owner Decisions".

Decision ID:  D-2 — effectType closed set
Status:       *** RESOLVED — ANSWERED (2026-10-01, session 2) ***
Answer:       "D-2 = Damage, Burn, Crit. Heal, Shield, and Power remain in the
              effectType set."
Coverage met: the complete, exact effectType member list —
              **{Heal, Shield, Power, Damage, Burn, Crit}** — with the three
              landed members explicitly confirmed to remain and the three new
              members named verbatim by the owner. The owning document for the
              set is `DATABASE.md` per D-7. D-4's deferred member name is
              resolved by this answer as `Damage`.
              This unblocks the `DATABASE.md` §1 item 2 and §3 `effectType`
              constraint rewrites — see "Changed Files".
PRIOR STATE (retained for audit): the owner first confirmed the set extends
              ("Yes — the set extends, and I will give the exact member names")
              and then deferred the names ("I need to think — leave D-2
              UNANSWERED for now"). No member string was invented by any agent
              while it was deferred.

Decision ID:  D-3 — valueType carriers for the authored magnitudes
Status:       *** RESOLVED — ANSWERED (2026-10-01, session 2) ***
Answer:       "D-3 = Use the existing `valueType` as the type of `value`, with
              structured extra members where an effect requires additional
              parameters. The valueType set becomes: Flat, PercentMaxHp,
              PercentagePoints, Undetermined. Burn uses { effectType: Burn,
              valueType: Flat, value: 50, duration: 2 } where `value` is damage
              per tick and `duration` is the number of Turns. Crit uses
              { effectType: Crit, valueType: PercentagePoints, value: 10,
              scope: NextAttack } where `value` is the Crit increase in
              percentage points and `scope` is `NextAttack`. `Undetermined`
              remains valid for effects whose authored magnitude is not yet
              determined."
Coverage met: the carrier for Burn (its `value` is the per-tick rate; its
              `duration` carries the duration in Turns — the authoritative
              Turn/End-Turn-tick unit of GAME_RULES.md §17 step 19a and
              COMBAT_RULES.md §5.2); the carrier for Crit (its `value` is the
              percentage-point increase; its `scope` carries the next-attack
              scope already authored in CARD_RULES.md §4.1); every new member
              named verbatim (`PercentagePoints` as a valueType, `duration` and
              `scope` as per-effect parameters beside `value`); and the explicit
              statement that `Undetermined` REMAINS VALID.
              This unblocks representability for card-inferno and
              card-iron-fang — see the walk-through.
PRIOR STATE (retained for audit): the owner first deferred it ("I don't know
              yet — leave D-3 UNANSWERED"). No carrier was invented while it
              was deferred.

Decision ID:  D-4 — Damage carrier and the "Power" member
Status:       *** RESOLVED — FULLY ANSWERED (see below) ***
              ANSWERED IN PRINCIPLE (2026-10-01, session 1): "Yes — a distinct
              damage member, which I will name once D-2 is settled"
              RESOLVED IN VOCABULARY (2026-10-01, session 2) by D-2, which names
              the member: **`Damage`**.
Answer:       "Yes — a distinct damage member, which I will name once D-2 is
              settled"  +  D-2's "Damage" as the member name.
Coverage met: BOTH halves of D-4's coverage are now satisfied.
              1. The vocabulary distinction: a damage-dealing effect is a member
                 DISTINCT from `Power`, and `Power` continues to denote Power
                 Charge (`CARD_RULES.md` §2). The D-4 collision — the risk that
                 the decided vocabulary makes the Power Charge Basic Card
                 unrepresentable or ambiguous — is resolved: `Power` keeps its
                 meaning, and damage is carried by `Damage`.
              2. The exact member name: **`Damage`**, supplied by D-2 in
                 session 2. It is the same member that enters
                 `COMBAT_RULES.md` §3 step 1 as the Card base value
                 (`CARD_RULES.md` §4.1 states both Inferno's and Iron Fang's
                 damage as "flat base value, entering the Damage Pipeline as the
                 Card/Skill base value"). The value is read from content, never
                 from `CardId` or Card name (TASK-108 D-1).
              Nothing in D-4's answer authors the Damage Pipeline, a Crit roll,
              or a Burn tick — those remain owned by `COMBAT_RULES.md` and
              unchanged.

Decision ID:  D-5 — Effect ordering
Status:       ANSWERED (2026-10-01, Product Owner)
Answer:       "No — effect order carries no meaning"
Coverage met: the ruling on whether ordering is meaningful; and, since ordering
              is NOT meaningful, the "where is that order defined" branch does
              not arise. Critically, the answer does NOT require a new gameplay
              rule, so this task's Stop Condition 2 does not fire and no
              GAMEPLAY-CHANGE decision task is required for ordering. The
              recorded consequence is a STORAGE statement (the array is a
              storage sequence only, non-semantic) which D-7 places in
              DATABASE.md; it authors no resolution step, no priority, and no
              change to GAME_RULES.md §17.

Decision ID:  D-6 — Row compatibility and the three Basic Cards
Status:       ANSWERED (2026-10-01, Product Owner)
Answer:       "They stay compatible, but need re-encoding into the array shape
              — left to the downstream task"
              + D-6b: "The Undetermined markers are retired by the downstream
                encoding task, not by TASK-111"
Coverage met: which rows this decision makes restatable (none — no row is
              restated by this task) and which remain with the encoding
              follow-up (all six). The three §2 Basic Card rows are declared
              CONTRACT-COMPATIBLE with the array shape; re-encoding them and
              retiring the three Pet Skill rows' `Undetermined` markers are the
              downstream encoding task's act. Confirms this task changes no
              database row — see §10 "D-6 Row Boundary".

Decision ID:  D-7 — Canonical ownership
Status:       ANSWERED (2026-10-01, Product Owner)
Answer:       "DATABASE.md owns the stored shape, vocabulary and value
              interpretation only"
Coverage met: the owner per contract part, with no duplication —
                stored shape          → docs/02-technical/DATABASE.md
                effect vocabulary     → docs/02-technical/DATABASE.md
                value interpretation  → docs/02-technical/DATABASE.md
                effect magnitudes     → docs/01-game-design/CARD_RULES.md
                                        §2/§4.1 (unchanged owner)
                runtime application   → docs/01-game-design/COMBAT_RULES.md
                                        §4/§5 + docs/02-technical/GAME_STATE.md
                                        §2.3.1 (unchanged owners)
              The "only" is recorded as given: DATABASE.md does not own a
              domain effect's behaviour, its magnitude, or its resolution
              position, so this task may not author any of those.
              The statement set this task may correct is therefore DATABASE.md
              §1 items 1/2/5/6/7, the §3 constraint lines, and the version
              header — but their CONTENT is D-2/D-3's vocabulary, so none is
              written until those two land.

Decision ID:  D-8 — Follow-up authorization
Status:       ANSWERED (2026-10-01, Product Owner)
Answer:       all five selected —
                "(a) enum/type + deserializer change"
                "(b) EF mapping + migration + row re-encode"
                "(c) resolver / PetSkillCast effect application"
                "(d) wire/API consequence"
                "(e) ADR"
              + D-8b: "Yes — confirm: TASK-111 executes none of them and creates
                no task file"
Coverage met: authorization for each of (a)–(e) with its task kind, and both
              required confirmations: none of (a)–(e) is executed by TASK-111,
              and TASK-111 creates no additional task file — the follow-ups are
              named in §"Reported Follow-Ups" only. Exactly zero task files
              were created by this task.

UNANSWERED:    none
Answered:      8 of 8  (D-1, D-2, D-3, D-4, D-5, D-6, D-7, D-8)
Nothing holds the task: D-2 and D-3 arrived in session 2, and D-4's deferred
              member name was resolved by D-2 as `Damage`.
```

### Blocking Preconditions Discovered Beyond D-1…D-8

Recorded per `AGENTS.md` §16 ("if unrelated problems are discovered while
working, report them") so that resolving the eight decisions does not appear,
to a later reader, to unblock this task by itself. **Two preconditions stand
outside the decision register and are NOT satisfied.** Neither authorizes any
action by this task.

```text
P-1  *** RESOLVED — the Product Owner answered D-5 "No — effect order carries
     no meaning". The (b) branch (a new gameplay resolution rule) was NOT
     taken, so TASK-111 Stop Condition 2 did NOT fire and no GAMEPLAY-CHANGE
     decision task is required for effect ordering. Retained for audit. ***
     D-5 required deciding whether a NEW GAMEPLAY RULE was required.
     Evidence: GAME_RULES.md §17 step 14 ("Resolve Player Effects") is a single
     step with NO documented sub-step expansion, and CARD_RULES.md §3 item 4
     groups a Pet Skill Card's two effects into one unordered step ("Apply
     Effect"). No document fixes an order between the effects of ONE Card.
     Therefore ANY answer to D-5 that makes a two-effect Card's stored sequence
     meaningful either (a) declines to make ordering meaningful, or (b) asserts
     a NEW gameplay resolution rule. Under (b) this task's own Stop Condition 2
     would fire: the rule must be authored by a separate GAMEPLAY-CHANGE task,
     and this task must NOT write it into DATABASE.md.
     RESOLUTION: the owner selected (a).

P-2  *** RESOLVED — the Product Owner supplied D-2 and D-3 in session 2, so
     every DATABASE.md §1/§3 statement this task may rewrite under D-7 is now
     writable and the corrections are applied in full. Retained for audit. ***
     The DATABASE.md correction this task's Acceptance Criteria require was NOT
     licensed while D-2 and D-3 were unanswered.
     Evidence: every DATABASE.md §1/§3 statement this task may rewrite under
     D-7 is a statement ABOUT the effectType vocabulary, the valueType
     vocabulary, the stored shape, or the rows' Undetermined markers. D-1
     supplied the shape and D-6 the rows' disposition, but the vocabulary
     statements could not be written without D-2's exact member names and D-3's
     carriers. The stale §1 items 6–7 are FACTUALLY stale irrespective of the
     shape decision, but they sit inside the same item block whose other items
     are vocabulary statements, and §"Scope" forbids partial application
     ("a half-authored contract block reads as decided").
     RESOLUTION: with all eight answers in hand, the block is written whole —
     no partial application was ever performed.
```

Additionally recorded as a factual correction to a §3 statement in this task's
"Current Contract Gap" framing, without changing that framing's conclusion:

```text
The three Pet Skill rows do NOT all carry the effect §4.1 names. Verified in
the live database at pickup:
  card-inferno   valueType "Undetermined", effectType "Power"   (correct)
  card-tidal-barrier valueType "Undetermined", effectType "Shield" (correct)
  card-iron-fang valueType "Undetermined", effectType "Power"   — but
                 CARD_RULES.md §4.1 states Iron Fang is "High damage; increased
                 Crit chance", NOT a Power Charge effect.
So the landed row data already disagrees with CARD_RULES.md §4.1 for
card-iron-fang: TASK-109 transcribed a placeholder effectType for a Card whose
authored effects (§4.1) are damage + Crit. This is a DATA-vs-DOCUMENT
inconsistency, reported NOT repaired — the row is not this task's to change
(no migration, no re-encode, no DML), and which effectType is correct is exactly
what D-2/D-4 own. It strengthens, and does not weaken, the case that D-1…D-4
are prerequisites to the encoding follow-up.
```

### D-6 Row Boundary — verified, not changed

Recorded as a fact of this execution, independently of D-6's answer, because
D-6's answer (whenever it is supplied) authorizes no data change here:

```text
Read-only verification against the live PostgreSQL instance at pickup
(dcacti-postgres, database dcacti_db, `SELECT` only — no DDL, no DML):

  card-heal          {"value": 20, "valueType": "PercentMaxHp", "effectType": "Heal"}
  card-shield        {"value": 20, "valueType": "PercentMaxHp", "effectType": "Shield"}
  card-power-charge  {"value": 25, "valueType": "Flat",          "effectType": "Power"}
  card-inferno       {               "valueType": "Undetermined", "effectType": "Power"}
  card-tidal-barrier {               "valueType": "Undetermined", "effectType": "Shield"}
  card-iron-fang     {               "valueType": "Undetermined", "effectType": "Power"}

Four further rows present in this database (`heal`, `shield`, `power_charge`,
`thanh_xa_skill`) carry the literal JSON string "effect". They are the test
suites' own smoke fixtures — not this migration's content, not among the six
content-defined rows, and not touched by any provisioning task. They are
recorded here only so the "rows unchanged" claim is exact and auditable.

No row was read as a decision input, none was modified, no migration was run,
no seed or UPDATE was issued, and no valueType was changed. The three Pet
Skill rows still read `Undetermined` and still carry no `value`.
```

---

## Product Owner Decisions (recorded verbatim, per the TASK-104 / TASK-108 precedent)

**Register format:** one block per decision — `ID — question — ANSWER (verbatim)
— date — author` (`tasks/README.md` §8 TASK-108 precedent; TASK-104 §5A). Date
and author are mandatory; an answer without them does not count as coverage.

Answers below were supplied **by selection from neutral option lists** — the
option list is reproduced as presented, and the selected option's own text is
the verbatim answer. **No option was marked recommended, preferred, ranked, or
best** (TASK-104 §9 criterion 4), and no candidate member name, shape, ordering
rule, or carrier was proposed by the agent. The D-2 and D-3 answers were typed
by the Product Owner directly, because those two required vocabulary the agent
was forbidden to supply — which is exactly why they were deferred and the task
was blocked until they arrived.

```text
Recorded by:  Product Owner (direct, in-session)   Date: 2026-10-01
Answers supplied across two sessions on the same date:
  session 1 — D-1, D-4, D-5, D-6, D-7, D-8  (six, by option selection)
  session 2 — D-2, D-3                      (two, typed verbatim)
Answered:     8 of 8  (D-1, D-2, D-3, D-4, D-5, D-6, D-7, D-8)
Unanswered:   none
```
D-1 — Multi-effect representation
  ANSWER (verbatim):
    "A sequence/array of effect objects"
  Follow-up D-1a: "Does each element of the array keep its own
    effectType / valueType / value triple (the same three members a row holds
    today)?"
    ANSWER (verbatim): "Yes — each element keeps its own
    effectType/valueType/value triple"
  Follow-up D-1b: "Does the array shape apply to EVERY Card, including the
    three Basic Cards that have only one effect?"
    ANSWER (verbatim): "Yes — always an array, even with one effect"

D-4 — Damage carrier and the "Power" member
  QUESTION (verbatim): "Should the damage effect be a member distinct from
    `Power` — where `Power` keeps meaning Power Charge, as on the Basic Power
    Charge card?"
  ANSWER (verbatim):
    "Yes — a distinct damage member, which I will name once D-2 is settled"
  NOTE: this answer is PRINCIPLED BUT NOT COMPLETE. It establishes that the
    damage effect is NOT the existing `Power` member and that `Power` continues
    to denote Power Charge — i.e. the D-4 collision question is resolved in
    principle. It does NOT supply the exact member name, because the Product
    Owner deferred that to D-2 and D-2 is unanswered. The D-4 coverage item
    "the damage effect's exact member name(s)" is therefore NOT MET, and no name
    was supplied by the agent to fill it.

D-5 — Effect ordering
  ANSWER (verbatim):
    "No — effect order carries no meaning"
  Recorded consequence: the array is a STORAGE sequence only. It fixes no
    gameplay priority, so no NEW gameplay rule is required and this task's
    Stop Condition 2 does NOT fire. Under this answer the stored sequence is
    non-semantic — consistent in kind with the existing non-semantic orderings
    the repository already records (GAME_STATE.md §2.3.1 item 10 for
    StatusEffects[]; GAME_STATE.md §2.3 for EquippedCards[]). Stating that the
    new array's order is non-semantic is a storage statement, which D-7 places
    in DATABASE.md, and it authors no resolution step, no priority, and no
    change to GAME_RULES.md §17.

D-6 — Row compatibility and the three Basic Cards
  ANSWER (verbatim):
    "They stay compatible, but need re-encoding into the array shape — left to
    the downstream task"
  Follow-up D-6b: "Do the rows' Undetermined markers get retired as part of
    this decision, or does that stay with the downstream encoding task?"
    ANSWER (verbatim): "The Undetermined markers are retired by the downstream
    encoding task, not by TASK-111"
  Recorded consequence: the three §2 Basic Card rows are CONTRACT-COMPATIBLE
    with the decided array shape (their single effect is the array's single
    element) but are NOT yet encoded in it. Re-encoding all six rows, and
    retiring the three `Undetermined` markers, belong to the downstream
    encoding task. This task changes no row (see §10 "D-6 Row Boundary").

D-7 — Canonical ownership
  ANSWER (verbatim):
    "DATABASE.md owns the stored shape, vocabulary and value interpretation
    only"
  Recorded consequence: ownership resolves as follows, with no duplication —
    stored shape, effect vocabulary (`effectType` member names) and value
    interpretation (`valueType` member names) → `docs/02-technical/DATABASE.md`;
    effect magnitudes → `docs/01-game-design/CARD_RULES.md` §2/§4.1 (unchanged
    owner, as TASK-108 D-2 / TASK-110 settled it); runtime effect application →
    `docs/01-game-design/COMBAT_RULES.md` §4/§5 and
    `docs/02-technical/GAME_STATE.md` §2.3.1 (unchanged owners). The wording
    "only" is recorded as given: DATABASE.md does NOT own a domain effect's
    behaviour, its magnitude, or its resolution position, and this task
    therefore may not author any of those.

D-8 — Follow-up authorization
  QUESTION (verbatim): "Which of these downstream consequences do you authorize
    as separate follow-up tasks?"
  ANSWER (verbatim, all five selected):
    "(a) enum/type + deserializer change"
    "(b) EF mapping + migration + row re-encode"
    "(c) resolver / PetSkillCast effect application"
    "(d) wire/API consequence"
    "(e) ADR"
  Follow-up D-8b: "Please confirm two things: 1. TASK-111 itself executes NONE
    of (a)–(e). 2. TASK-111 creates NO additional task file — the follow-ups
    are named in its report only."
    ANSWER (verbatim): "Yes — confirm: TASK-111 executes none of them and
    creates no task file"
  Recorded consequence: all five consequence classes are AUTHORIZED for
    separate follow-up tasks and NONE is executed here. Exactly zero task files
    are created by this task; they are named in §"Reported Follow-Ups".

D-2 — effectType closed set
  STATUS: ANSWERED (2026-10-01, Product Owner — supplied in a later session,
    superseding the earlier deferral recorded below)
  ANSWER (verbatim):
    "D-2 = Damage, Burn, Crit.

     Heal, Shield, and Power remain in the effectType set."
  Recorded consequence: the `effectType` closed set becomes **Heal | Shield |
    Power | Damage | Burn | Crit** — the three landed members plus the three
    the Product Owner names here. `Damage` is the damage-dealing effect D-4
    answered in principle and whose name D-4 explicitly deferred to D-2, so
    **D-4's outstanding name is also resolved by this answer**: the member is
    `Damage`. Burn is the damage-over-time effect; Crit is the Crit-chance
    increase. The owning document for the set is `DATABASE.md` per D-7.
  PRIOR STATE (retained for audit): the owner first confirmed the set extends
    ("Yes — the set extends, and I will give the exact member names") and then
    deferred the names ("I need to think — leave D-2 UNANSWERED for now"). No
    name was invented by any agent while it was deferred.

D-3 — valueType carriers for the authored magnitudes
  STATUS: ANSWERED (2026-10-01, Product Owner — supplied in a later session,
    superseding the earlier deferral recorded below)
  ANSWER (verbatim):
    "D-3 = Use the existing `valueType` as the type of `value`, with structured
     extra members where an effect requires additional parameters.

     The valueType set becomes:

     * Flat
     * PercentMaxHp
     * PercentagePoints
     * Undetermined

     Burn uses:

     {
       \"effectType\": \"Burn\",
       \"valueType\": \"Flat\",
       \"value\": 50,
       \"duration\": 2
     }

     where `value` is damage per tick and `duration` is the number of Turns.

     Crit uses:

     {
       \"effectType\": \"Crit\",
       \"valueType\": \"PercentagePoints\",
       \"value\": 10,
       \"scope\": \"NextAttack\"
     }

     where `value` is the Crit increase in percentage points and `scope` is
     `NextAttack`.

     `Undetermined` remains valid for effects whose authored magnitude is not
     yet determined."
  Recorded consequence — the decided carrier contract:
    * `valueType` set extends from {Flat, PercentMaxHp, Undetermined} to
      **{Flat, PercentMaxHp, PercentagePoints, Undetermined}**: one new member,
      `PercentagePoints`.
    * `valueType` remains the TYPE OF `value` — its role is unchanged. Additional
      per-effect parameters travel as **extra members beside `value` in the same
      effect object**, not as new valueType interpretations.
    * `Burn` carries `value` (damage per tick) + `duration` (number of Turns,
      the authoritative Turn/End-Turn-tick unit).
    * `Crit` carries `value` (increase in percentage points) + `scope`
      (`NextAttack`).
    * `Undetermined` remains valid and unchanged for effects whose authored
      magnitude is not yet determined.
    * `scope: "NextAttack"` records an ALREADY-AUTHORED gameplay rule, not a new
      one: `CARD_RULES.md` §4.1 states Crit applies "for the next attack only"
      (and §1.6 of its version header repeats it), and `PASSIVE_RULES.md` §7
      already uses the same "next attack" scope for Bạch Hổ's Passive. D-3
      decides only how that existing rule is STORED, which is `DATABASE.md`'s
      ownership under D-7. No resolution step, priority, or ordering is
      authored — consistent with D-5.
  PRIOR STATE (retained for audit): the owner first deferred it ("I don't know
    yet — leave D-3 UNANSWERED"). No carrier was invented while it was deferred.

UNANSWERED items: none
Coverage:         8 of 8 answered
```

### Partial-Coverage Consequence — *** CLEARED (retained for audit) ***

```text
This section explained why the task could not leave BLOCKED while D-2 and D-3
were outstanding. Both have since been answered, so every obstacle it lists is
resolved. It is retained because it states precisely what the two missing
answers were load-bearing for.

1. The `effectType` set is the discriminator every stored row and every future
   reader dispatches on. D-2's answer is the list of legal values; DATABASE.md
   §1 item 2 and §3 currently publish a set the owner has now said extends, so
   those statements cannot be written until the replacement list exists.
   RESOLVED — D-2 supplied {Heal, Shield, Power, Damage, Burn, Crit}; both
   statements are rewritten (see §"Changed Files").

2. Burn and Crit have authored magnitudes with no carrier (D-3). Without a
   carrier, card-inferno and card-iron-fang are NOT representable under the
   decided shape.
   RESOLVED — D-3 supplied `PercentagePoints` plus the `duration` and `scope`
   per-effect members; both Cards are now representable (see the walk-through).

3. D-4 is answered only in principle. Its own answer defers the member name to
   D-2, so the damage effect cannot be written into the contract either.
   RESOLVED — D-2 named the member `Damage`.

4. The stale DATABASE.md statements (§1 items 6–7) are in scope to correct but
   sit in the same item block as the vocabulary statements, and partial
   application was forbidden.
   RESOLVED — the whole block is now writable, so the correction is applied in
   full rather than partially.

Therefore: the register records 8 of 8, `docs/02-technical/DATABASE.md` is
corrected, and the walk-through passes for all six Cards.
```

---

## Completion Evidence

<!-- TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE. -->

**Outcome of this execution: DONE.** All eight decisions are answered and
recorded verbatim, the `DATABASE.md` contract statements they determine are
corrected, and the representability walk-through passes for all six provisioned
Cards. Completed per `core/completion.md` §1 and `TASK_LIFECYCLE.md` §3.

### Decision Register

```text
D-1 "A sequence/array of effect objects" (+ D-1a "Yes — each element keeps its
    own effectType/valueType/value triple"; D-1b "Yes — always an array, even
    with one effect") | 2026-10-01 | Product Owner
D-2 "D-2 = Damage, Burn, Crit. Heal, Shield, and Power remain in the effectType
    set." | 2026-10-01 | Product Owner
D-3 "D-3 = Use the existing `valueType` as the type of `value`, with structured
    extra members where an effect requires additional parameters. The valueType
    set becomes: Flat, PercentMaxHp, PercentagePoints, Undetermined. Burn uses
    { effectType: Burn, valueType: Flat, value: 50, duration: 2 } where `value`
    is damage per tick and `duration` is the number of Turns. Crit uses
    { effectType: Crit, valueType: PercentagePoints, value: 10, scope:
    NextAttack } where `value` is the Crit increase in percentage points and
    `scope` is `NextAttack`. `Undetermined` remains valid for effects whose
    authored magnitude is not yet determined." | 2026-10-01 | Product Owner
D-4 "Yes — a distinct damage member, which I will name once D-2 is settled"
    | 2026-10-01 | Product Owner   [name RESOLVED by D-2 as `Damage`]
D-5 "No — effect order carries no meaning" | 2026-10-01 | Product Owner
D-6 "They stay compatible, but need re-encoding into the array shape — left to
    the downstream task" (+ D-6b "The Undetermined markers are retired by the
    downstream encoding task, not by TASK-111") | 2026-10-01 | Product Owner
D-7 "DATABASE.md owns the stored shape, vocabulary and value interpretation
    only" | 2026-10-01 | Product Owner
D-8 all five of (a) enum/type + deserializer change, (b) EF mapping +
    migration + row re-encode, (c) resolver / PetSkillCast effect application,
    (d) wire/API consequence, (e) ADR (+ D-8b "Yes — confirm: TASK-111 executes
    none of them and creates no task file") | 2026-10-01 | Product Owner
(unanswered: none — 8 of 8)
```

No answer was paraphrased, "cleaned up", ranked, defaulted, or inferred. The
answers arrived as selections from neutral option lists that contained **no
invented vocabulary** — which is precisely why D-2 could not be completed: it
required the owner to type member names, the agent offered none, and the owner
deferred. No answer was manufactured to satisfy the date-and-author rule.

### Representability Walk-Through

**PERFORMED — 6 of 6 Cards representable; zero open items.** With D-1 (shape),
D-2 (vocabulary), and D-3 (carriers) all answered, every authored effect of
every provisioned Card maps onto the decided representation using only recorded
member names. The magnitudes shown are transcribed from `CARD_RULES.md` §2/§4.1
— cited, not authored here; encoding them into rows remains the downstream
task's act under D-6/D-6b.

```text
Card                authored effects     decided representation              source          representable
card-heal           Heal                 [ { "effectType":"Heal",            CARD_RULES.md   YES
                                           "valueType":"PercentMaxHp",        §2
                                           "value": 20 } ]
                                         — one-element array per D-1b; the
                                           element is the landed triple.
card-shield         Shield               [ { "effectType":"Shield",          CARD_RULES.md   YES
                                           "valueType":"PercentMaxHp",        §2
                                           "value": 20 } ]
card-power-charge   Power Charge         [ { "effectType":"Power",           CARD_RULES.md   YES
                                           "valueType":"Flat",                §2
                                           "value": 25 } ]
                                         — D-4 confirms `Power` KEEPS
                                           denoting Power Charge, distinct
                                           from `Damage`.
card-tidal-barrier  Heal + Shield        [ { "effectType":"Heal",            CARD_RULES.md   YES
                                           "valueType":"PercentMaxHp",        §4.1
                                           "value": 20 },
                                         { "effectType":"Shield",
                                           "valueType":"PercentMaxHp",
                                           "value": 20 } ]
                                         — the two-element array D-1 enables;
                                           nothing new was needed, both
                                           effects use landed vocabulary.
card-inferno        damage + Burn        [ { "effectType":"Damage",          CARD_RULES.md   YES
                                           "valueType":"Flat",                §4.1
                                           "value": 100 },
                                         { "effectType":"Burn",
                                           "valueType":"Flat",
                                           "value": 50,
                                           "duration": 2 } ]
                                         — `Damage` per D-2/D-4 (flat base
                                           value entering COMBAT_RULES.md §3
                                           step 1); `Burn` per D-2 with D-3's
                                           `value` = damage per tick and
                                           `duration` = Turns.
card-iron-fang      damage + Crit        [ { "effectType":"Damage",          CARD_RULES.md   YES
                                           "valueType":"Flat",                §4.1
                                           "value": 120 },
                                         { "effectType":"Crit",
                                           "valueType":"PercentagePoints",
                                           "value": 10,
                                           "scope": "NextAttack" } ]
                                         — `Damage` per D-2/D-4; `Crit` per
                                           D-2 with D-3's `value` = percentage
                                           points and `scope` = NextAttack
                                           (the scope CARD_RULES.md §4.1
                                           already authors).

open items: NONE. Every effect of every provisioned Card is representable using
            only recorded Product Owner vocabulary. The prior open items O-1
            … O-6 are each closed by D-2 and D-3:
              O-1/O-3  damage member name        → `Damage`  (D-2, D-4)
              O-2      Burn member + carrier      → `Burn` + `duration` (D-2, D-3)
              O-4      Crit member + carrier      → `Crit` + `scope`/`PercentagePoints`
                                                    (D-2, D-3)
              O-5      Heal/Shield/Power remain   → confirmed (D-2)
              O-6      `Undetermined` remains     → confirmed valid (D-3)

Card-ownership note: the magnitudes above are CARD_RULES.md §2/§4.1's values,
cited here to prove representability. They are NOT encoded by this task and are
NOT a second source for those values (D-7 places magnitudes with CARD_RULES.md).
```

### Vocabulary Traceability

**PARTIALLY SATISFIED — and the only new element traces to a recorded answer.**
Exactly one contract element was introduced by this task's register, and it is
traceable; every other element remains whatever TASK-109 landed.

```text
contract element introduced            traced to
shape: `EffectDefinition` is an ARRAY  D-1 "A sequence/array of effect objects"
shape: every element keeps its own     D-1a "Yes — each element keeps its own
  effectType/valueType/value triple      effectType/valueType/value triple"
shape: uniform arity (1 or N effects)  D-1b "Yes — always an array, even with
                                         one effect"
ordering: sequence is non-semantic     D-5 "No — effect order carries no
                                         meaning"
row disposition: Basic rows compatible D-6 "They stay compatible, but need
  but re-encoded downstream              re-encoding into the array shape —
                                         left to the downstream task"
ownership: DATABASE.md owns shape +    D-7 "DATABASE.md owns the stored shape,
  vocabulary + value interpretation      vocabulary and value interpretation
                                         only"
effectType: `Damage` added             D-2 "D-2 = Damage, Burn, Crit."
effectType: `Burn` added               D-2 (same answer)
effectType: `Crit` added               D-2 (same answer)
effectType: Heal/Shield/Power retained D-2 "Heal, Shield, and Power remain in
                                         the effectType set."
effectType: `Damage` ≠ `Power`         D-4 "Yes — a distinct damage member…"
valueType: `PercentagePoints` added    D-3 "The valueType set becomes: Flat,
                                         PercentMaxHp, PercentagePoints,
                                         Undetermined"
valueType: `Undetermined` retained     D-3 "`Undetermined` remains valid for
                                         effects whose authored magnitude is
                                         not yet determined."
extra member: `duration` (Burn)        D-3 "…\"duration\": 2 … where `value` is
                                         damage per tick and `duration` is the
                                         number of Turns."
extra member: `scope` (Crit)           D-3 "…\"scope\": \"NextAttack\" … where
                                         `value` is the Crit increase in
                                         percentage points and `scope` is
                                         `NextAttack`."
`valueType` stays the TYPE of `value`  D-3 "Use the existing `valueType` as the
                                         type of `value`, with structured extra
                                         members where an effect requires
                                         additional parameters."

zero entries lack a source. Zero entries were invented. Every member name,
valueType, shape element, and ordering statement in the corrected DATABASE.md
contract traces to one of the eight recorded Product Owner answers above.
```

### Representability / Contract Statements

```text
docs/02-technical/DATABASE.md §1 items 1–9        CHANGED — array shape (D-1),
  (rewritten as the extended contract)              effectType set (D-2),
                                                    valueType set + extra members
                                                    (D-3), Damage vs Power (D-4),
                                                    non-semantic ordering (D-5),
                                                    rows not yet re-encoded (D-6),
                                                    Undetermined retained (D-3)
docs/02-technical/DATABASE.md §3 constraint lines CHANGED — effectType/valueType
                                                    sets, `duration` and `scope`
                                                    constraints, the rejection
                                                    rule extended to a missing
                                                    required extra member, and an
                                                    explicit non-semantic-ordering
                                                    line
docs/02-technical/DATABASE.md version header       CHANGED — 1.22 → 1.23, with the
                                                    extension recorded in the
                                                    document's existing style and
                                                    the "no gameplay value changed"
                                                    statement preserved
docs/02-technical/DATABASE.md §1 entity block      CHANGED — the
  (`EffectDefinition` description)                  `EffectDefinition` member is
                                                    described as a JSON ARRAY and
                                                    records the TASK-111 extension
Stale "§4.1 authors no…magnitude" statements       CORRECTED — both removed; §1
                                                    now references CARD_RULES.md
                                                    §4.1 as the authored owner and
                                                    states that the rows are
                                                    simply not yet re-encoded (a
                                                    D-6/D-6b fact), not that the
                                                    content is unauthored
RelicDefinition block + rows                       NOT CHANGED — R2-7 remains in
                                                    force for Relics; verified
                                                    byte-identical
CARD_RULES.md §2/§4.1 magnitudes                   NOT RESTATED as a second
                                                    source — cited by reference
                                                    only, per D-7 and
                                                    tasks/README.md §9
```

### Reported Follow-Ups (named, not created)

All five classes are now **AUTHORIZED by D-8** and **executed by nobody**:

```text
(a) Domain enum/type + deserializer change          AUTHORIZED by D-8 — type:
                                                    expected FEATURE (Domain
                                                    `CardEffectType` /
                                                    `CardEffectDefinition`
                                                    shape + strict reader).
                                                    Also carries D-2's member
                                                    names and D-3's carriers.
(b) EF mapping + migration + row re-encode          AUTHORIZED by D-8 — type:
                                                    expected FEATURE +
                                                    migration. Owns the six-row
                                                    re-encode into the array
                                                    shape (D-6) and retiring the
                                                    three `Undetermined` markers
                                                    (D-6b), and must also
                                                    correct card-iron-fang's
                                                    wrong effectType (see
                                                    Reported Discrepancy 3).
(c) Resolver / PetSkillCast effect application      AUTHORIZED by D-8 — type:
                                                    FEATURE (TASK-108
                                                    Follow-Up item 2).
(d) GAME_EVENTS / SIGNALR / API consequence         AUTHORIZED by D-8 — type:
                                                    report only; no wire
                                                    member, event, or endpoint
                                                    is authored here. The known
                                                    consequence is that
                                                    `effect summary`
                                                    (GAME_EVENTS.md §2,
                                                    SIGNALR_PROTOCOL.md
                                                    §3.2.25) is the natural
                                                    carrier for a resolved
                                                    multi-effect Card, and that
                                                    adding it is a member
                                                    addition owned by that
                                                    stage's task.
(e) ADR proposal                                    AUTHORIZED by D-8 — type:
                                                    ARCHITECTURE /
                                                    adr-change. Not authored
                                                    here (AGENTS.md §18;
                                                    docs/03-decisions/README.md
                                                    §8).
Additional task files created by this task:         0  — confirmed by D-8b
```

Known consequences that remain for the downstream tasks (recorded as
observations; none executed here):

```text
- The CardDefinition row-content encoding follow-up named by TASK-110
  "Reported Consequences" item 1 is now UNBLOCKED at the contract level: the
  vocabulary (D-2), the carriers (D-3), and the shape (D-1) all exist, so (b)
  can encode damage, Burn, Crit, and two-effect rows. It must also re-encode
  the three Basic rows into the array shape and retire all three `Undetermined`
  markers (D-6/D-6b), and correct card-iron-fang's wrong effectType (Reported
  Discrepancy 3).
- PetSkillCast effect resolution and TASK-102's three Pet Skill Card
  acceptance criteria remain blocked on capabilities the frozen runtime lacks
  (TASK-108 Follow-Up item 2). See Reported Discrepancy 4 — TASK-102
  additionally needs a Crit roll and Burn damage-over-time ticking, neither of
  which exists today.
- DATABASE.md §1's stale statements are now CORRECTED (see §"Representability /
  Contract Statements"). No stale statement remains.
```

### Changed Files

**Documentation (the task's deliverable):**

- `docs/02-technical/DATABASE.md` — the canonical owner of the stored shape,
  vocabulary, and value interpretation (D-7). **Version 1.22 → 1.23.** Changes,
  each traceable to a recorded decision:
  - **§1 entity block** — the `CardDefinition.EffectDefinition` member is now
    described as a JSON **ARRAY** of effect objects, and the block records the
    TASK-111 extension (D-1/D-2/D-3/D-4/D-5).
  - **§1 "Card `EffectDefinition` contract"** — rewritten and renumbered 1–9:
    item 1 states the array shape, each element's own triple (D-1/D-1a/D-1b),
    the extended `effectType` set with `Damage` distinct from `Power`
    (D-2/D-4), the extended `valueType` set with `Undetermined` retained (D-3),
    and the `duration` / `scope` per-effect extra members with worked Burn and
    Crit examples (D-3); item 3 is NEW and records that element order is not
    semantic (D-5); item 6 extends the loud-rejection rule to a missing required
    extra member; item 7 corrects the stale "§4.1 authors no magnitude" wording
    to the authored §4.1; item 8 is NEW and records that the six provisioned
    rows are not yet re-encoded and that correcting `card-iron-fang`'s
    `effectType` belongs to the encoding task (D-6/D-6b); item 9 carries the
    `Undetermined` rule with D-3's confirmation that it remains valid.
  - **§3 Constraints** — the `effectType` and `valueType` sets updated, two new
    constraints for `duration` (present iff `Burn`) and `scope` (present iff
    `Crit`, value `NextAttack`), the rejection line extended, and an explicit
    non-semantic-ordering constraint added.
  - **Version header** — 1.23 entry recording the extension, naming every
    decision ID, and preserving the "no gameplay value, rule, or balance figure
    changed" statement.
  - **`RelicDefinition` block and rows — NOT changed.** R2-7 remains in force
    for Relics; verified byte-identical to pickup.

- `tasks/active/TASK-111-resolve-multi-effect-card-effectdefinition-contract.md`
  — **this file**: `Status:` `BLOCKED` (6 of 8) → `DONE` (8 of 8); D-2, D-3, and
  the resolution of D-4's deferred name recorded verbatim; the
  Partial-Coverage Consequence and both preconditions marked CLEARED; the STOP
  CONDITION REPORT marked RESOLVED and retained as the audit trail; the
  Decision Register, Representability Walk-Through, Vocabulary Traceability,
  and Representability/Contract Statements sections updated to the completed
  state; validation results refreshed. No acceptance criterion was weakened,
  removed, or re-scoped.
- File movement: `tasks/backlog/TASK-111-…md` → `active/` → `blocked/` →
  `active/` → `blocked/` → `active/` → (on completion) `tasks/completed/`
  (`TASK_LIFECYCLE.md` §2/§4).
- No other task file was created, modified, moved, or deleted — in particular
  TASK-108, TASK-109, and TASK-110 are byte-identical, and **zero** downstream
  task files were created (confirmed by D-8b).

**No file under `src/` or `tests/` was changed. No migration was added or run.
No database row was changed.**

### Validation Results

- **Decision completeness — PASS (8 of 8).** All eight items are answered and
  recorded verbatim with date and author. Nothing was inferred, defaulted, or
  "assumed yes" at any point; while D-2/D-3 were outstanding the task was
  BLOCKED and no vocabulary was authored (`AGENTS.md` §7/§20).
- **Register-fidelity check — PASS.** Every answer is in the Product Owner's own
  words, dated 2026-10-01 and attributed to the Product Owner. No wording or
  value was altered, and no answer was "cleaned up" into a more precise
  technical decision. The two interim deferrals are retained as deferrals.
- **No-option-ranked check — PASS.** No option shown to the Product Owner was
  marked recommended, preferred, ranked, or best, and no option list contained
  an invented member name, carrier, or shape (`AGENTS.md` §4; TASK-104 §9
  criterion 4). D-2 and D-3 required the owner to type vocabulary and the agent
  supplied none.
- **Gap re-verification — PASS (the premise was live when the decisions were
  taken).** Before the answers arrived, `DATABASE.md` §1 fixed three members and
  the closed set `Heal | Shield | Power`, `CARD_RULES.md` §4.1 stated two effects
  per Pet Skill Card, the three Pet Skill rows read
  `valueType: "Undetermined"` in live PostgreSQL, and no resolver, CardCast, or
  PetSkillCast existed in `src/`. The decisions were not requested against a
  stale premise.
- **Vocabulary traceability — PASS.** Every member, set, shape element, and
  ordering statement in the corrected contract traces to a specific recorded
  decision ID (table above). Zero elements lack a source; zero were invented.
- **New-gameplay-rule check — PASS.** No answer introduced a gameplay rule.
  `scope: "NextAttack"` stores a scope `CARD_RULES.md` §4.1 already authors (and
  `PASSIVE_RULES.md` §7 already uses); `duration` stores Burn's duration in the
  unit `GAME_RULES.md` §17 step 19a and `COMBAT_RULES.md` §5.2 already own; D-5
  explicitly makes ordering non-semantic, so no resolution step or priority was
  created. Stop Condition 2 did **not** fire and no GAMEPLAY-CHANGE task is
  required for this contract.
- **Documentation consistency — PASS.** `DATABASE.md` §1/§3/header now state the
  decided contract. The two pre-existing inconsistencies are resolved: the stale
  "§4.1 authors no magnitude" statements are corrected, and the `effectType` set
  no longer contradicts the effects `CARD_RULES.md` §4.1 authors. No magnitude
  was restated into `DATABASE.md` as a second source — `CARD_RULES.md` §2/§4.1
  remains the sole value owner (D-7), and it is `byte-identical`.
- **Representability walk-through — PASS, 6 of 6.** Every authored effect of all
  six provisioned Cards maps onto the decided representation using only recorded
  member names. Zero open items; O-1 … O-6 are all closed by D-2/D-3.
- **Freeze check on the frozen runtime — PASS.** `COMBAT_RULES.md`,
  `GAME_RULES.md`, `GAME_STATE.md`, `GAME_EVENTS.md`, `SIGNALR_PROTOCOL.md`,
  `API_CONTRACTS.md`, `REDIS_STATE.md`, `CARD_RULES.md` and the Damage Pipeline,
  Crit, Shield, Burn-timing and `StatusEffects[]` contracts are byte-identical.
  In particular D-5's answer authors no resolution order, so
  `GAME_RULES.md` §17 step 14 is untouched.
- **Change isolation — PASS.** The changed-file set is exactly
  {`docs/02-technical/DATABASE.md`, `tasks/…/TASK-111-…md`} — the Task's Scope
  §"In Scope" and its Acceptance Criterion 5 name precisely this pair. All other
  guarded documents, source files, test files, and migration files are
  byte-identical to pickup (hashes below).
- **Read-only database verification — PASS.** A `SELECT`-only query confirmed
  the six provisioned rows are unchanged and the three Pet Skill rows still
  read `valueType: "Undetermined"` with no `value` member. No DDL, no DML, no
  migration, no seed. The echo of this task's own decisions into row data is the
  downstream encoding task's act (D-6/D-6b), not this one's.
- **`dotnet build` / `dotnet test` / `npm test` — N/A and not run.** This task
  changes no code, and its own §"Testing Requirements" records the N/A
  justification (zero `src/`/`tests/` changes; no behavior to test).
- **Scope validation (`MVP_SCOPE.md` §1/§2) — PASS.** Cards and Combat/Status
  Effects are IN; this task adds no system, mechanic, Card, Pet, or currency,
  and reaches no OUT or unlisted item.
- **ADR check — PASS, none required.** No ADR addresses the `EffectDefinition`
  storage shape, and `docs/03-decisions/README.md` §8's "Known Open Items (Not
  ADRs)" lists only the backend-runtime assumption and the PRNG algorithm. The
  decided shape is a content-storage detail on an existing table: no layer,
  storage technology, realtime strategy, or authoritative model changes.
  `ADR-006` (PostgreSQL, queried only outside the hot resolution path) still
  governs and is preserved. D-8(e) authorizes an ADR **proposal** downstream
  (named in §"Reported Follow-Ups"), which this task does not author
  (`AGENTS.md` §18).

#### Change-isolation baseline (SHA-256, recorded at pickup, re-verified at reporting)

```text
CHANGED — this task's deliverable (expected, and the ONLY doc change):
7365CED86160EFE9FEFF3E819F490A28F911F3FEC18AF20462D8C4BFC333A90A  docs/02-technical/DATABASE.md   (pickup value)
                                                                  → CHANGED by
                                                                    TASK-111
                                                                    (v1.22 → v1.23;
                                                                    §1 contract,
                                                                    §1 entity block,
                                                                    §3 constraints,
                                                                    header)

UNCHANGED — every other guarded authoritative document:
E94E63F0D7D7288871371ED1D7078021C03DE904665BA1C5D4CCFA03674F8309  docs/01-game-design/CARD_RULES.md
258B12A3E53962AD3983DFE6E9895FE4AD07762EF1997FFFF5FDA70802EF0EF6  docs/01-game-design/COMBAT_RULES.md
7514786E52AFE7774CA64451275C66A3CA7BEE8AF2B9AC17D4C0CA96CD143C60  docs/01-game-design/GAME_RULES.md
D8F9ACFA698E8769EFFC67A2ECD47FDE7DE97021D2AB89546930F276B059031C  docs/01-game-design/PASSIVE_RULES.md
0FA64FD5CE9CFB6280B1093E4052F3C17795CE0A79D2C33EDA590D1ED1769255  docs/01-game-design/PET_RULES.md
3784E6EAC2D355B26435B3D6906894AF822572B870C1858D73B8CCD6AA74E39A  docs/01-game-design/BOSS_RULES.md
03C43D44FCA163D0C65B63EFFE876D3203AA2039F0E7EB97C33E1C7B6D35E556  docs/01-game-design/ELEMENT_RULES.md
A7155BF78547F5162397023493ABC9B141D1E04FE2F626E77455E039D511ED1A  docs/01-game-design/RELIC_RULES.md
CB3D567651C439D0B2B7848EF9AD2CFB0E4C1E4ECD6A1891A4C7CF31924B0CD4  docs/01-game-design/MATCH3_RULES.md
31FDB733BF587235D8BADF07161EAB3E73B1A8B2B7D5FBAFB82231475A9329BA  docs/00-overview/MVP_SCOPE.md
7004B262B7B4CFDDA7FE479038D9C281299040FF983CDFF828AABE6C6481609B  docs/02-technical/GAME_STATE.md
454A6F82611B927F0C4987A9014ECBE75690AC93147225A490B5C43CF27E75CE  docs/02-technical/GAME_EVENTS.md
AE4FD980C6D5444148093A1BA76831586D45ABA9263A50DDAB447177B4CB2AA1  docs/02-technical/SIGNALR_PROTOCOL.md
AEC7E56D0D0048FCEF940DCA5BF218FC2A83080417A96BCE49A6BBE0FF53AE1F  docs/02-technical/API_CONTRACTS.md
6655EAD97C8139257E03F1637775AB420D5351EF466ADE4759C1F30AAF7BF475  docs/02-technical/ARCHITECTURE.md
DF68851D129F9CAB7F46237AF31B36129C802FF68C534FAF6E3ECFE05B0DA447  docs/02-technical/TDD.md
4A3100777534E1DFFF6E6336C55D45DDA9D2CD85E126CF03BA3CEFF08E043541  docs/02-technical/REDIS_STATE.md
513F121B64FDC2C857D3B884CDC5A45DA8754999629A9B56A250A40303F686E6  docs/03-decisions/README.md
DB3839DE55FF271A1B2112F7FD6F049DD180EAAFF4D8BAE2BFC0F69F41303684  AGENTS.md

UNCHANGED — the tasks this task must not modify:
F0628870A3E3178233177993104C4E9B6F463AE25BB746EC2809A72CDA1AFBCB  tasks/completed/TASK-109-implement-structured-effectdefinition-contract.md
BFC283E55C0B6DA5C060A08B414AB86B7262F7A296F63272B8A6807E9B3A592C  tasks/completed/TASK-110-author-pet-skill-card-effect-magnitudes.md
A5C64F8A98037338E78A033F3DD618A981520CC4717BB977B742D43B48C7F5A5  tasks/backlog/TASK-108-resolve-card-effect-resolution-contract.md
15DA088ABE0291E12B90BF9DF2AD8D7082CEAD9704AA464496415CC298C804B8  tasks/backlog/TASK-102-implement-card-cast-server-path.md
EF35D7B1B940B1B989EBC6E87734EFA9CEB0367CFC1F5402B7EEA095C1FDBD96  tasks/backlog/TASK-103-resolve-cardcast-wire-schema-and-shield-stacking-contract.md
F830DBC580E21BE4F9EF819FED899AD6FA3472187A6C0DE2EFBDBB37763857D6  tasks/backlog/TASK-104-collect-cardcast-wire-and-shield-contract-decisions.md
DADAEE3F5AD577C9884D4395A3AFEB1AE7436D064C094F175803A2BFDFA63B7F  tasks/backlog/TASK-106-close-task-103-blocker-a-bookkeeping.md
6A8987077D5FC07B97AC39110BB53930C9B996A508D0CE3DA9D5F00023F9F2C4  tasks/backlog/TASK-107-implement-basic-cardcast-server-path.md
111A56BCCE0A63C9A56CE6CD85AD77CD27A6D1F2D84A29CB4EBFEDA7A74E8381  tasks/completed/TASK-082-resolve-pet-card-relic-content-provisioning-contract.md

UNCHANGED — src/ and tests/ (357 tracked non-build files, one tree digest):
EE3640D1CAB21B839B893FAF2D8A96A1208082EDACA996F94D50711E883ECC87  src/ + tests/ tree digest
ECACEB8A798456FBCFBFDAE36CA9E0AA6671CC1F50651CB3F21789D36AA6B1DB  src/backend/GameServer.Domain/Cards/CardEffectType.cs
92C5B2E4A8CD407C80D1132FF2221B1464C6B1F3F283024A6A452405609BFD67  src/backend/GameServer.Domain/Cards/CardEffectValueType.cs
78DDC9292FB2BF225F85FC9402B4FF94EB52837B86C59726125AF7F85F4FFF0A  src/backend/GameServer.Domain/Cards/CardEffectDefinition.cs
E42364DAFE08C67FA3C4E4DF1457B970FEA4689CDEC71E0EC9D5165DD3D1CC40  src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs
15B7EDE9C6E65EFA103555969F5CBB14E39C3186C897A326427CA01FC1C64484  src/backend/GameServer.Domain/Battle/StatusEffect.cs
8CF3F226F74611EB787327663528D2693E49E742BA95EBC1C35EB2B453AF7306  src/backend/GameServer.Domain/Battle/StatusEffectType.cs
9C26B4F2DE86F54FDB75ACFFAE390EE9D55BE0A05F9525629732C4E8E951ADC3  src/backend/GameServer.Domain/Combat/DamagePipeline.cs
F543C18C3F9E1F859313EA5BF411CD3CB91CA8D931AB615FA2EF0B6A002F695F  src/backend/GameServer.Domain/Match3/BattleEvent.cs
E92AAA05119125140CB7CDD5569F5EDDB7DD80F10F4C3DC7B9AA4BD7F51F3397  src/backend/GameServer.Domain/Match3/ResourceGenerator.cs
569385EBC339F92FB77D16BC8B88482BEBA2141EF69A008D8384D0A1F4AF0969  src/backend/GameServer.Infrastructure/Postgres/Configurations/CardDefinitionConfiguration.cs
A9403F77B1E3CC202BD7E2FB3F6A30F42CEA8F2A6EDB0D824A843AC4B619DA1B  src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs
8D6171519D7FED385264A216776E6165D3CF128114F1D8B18C42D49FE28D083A  src/backend/GameServer.Api/Hubs/BattleHub.cs

UNCHANGED — migrations (25 files under
src/backend/GameServer.Infrastructure/Postgres/Migrations/), including:
D3BA917F4B823202786D913853922DD3714B0EF16529DCB79BCC190DA45FF165  20261001112446_StructureCardDefinitionEffectDefinition.cs
D12D22F8A6BAAA87C8DF5B02B69E190AD69353B83E358EE59066D916D5AA3390  20261001112446_StructureCardDefinitionEffectDefinition.Designer.cs
2670F716374D78A19E69B1C0CC1A0C39F37697B4A65E91F472CA30AE9897227A  20260929152651_ProvisionPetCardRelicContentDefinitions.cs
B4A1339221A2396922CB4597D1855E0E9D4B5BD45174234BFC426ADC89FD9BD0  GameDbContextModelSnapshot.cs

MOVED — this task's own file, and (on completion) its final location:
tasks/backlog/TASK-111-…md → tasks/active/… → tasks/blocked/… → tasks/active/…
  → tasks/blocked/… → tasks/active/… → tasks/completed/TASK-111-…md
```

### Discrepancies and Stale Statements Reported

Recorded per `AGENTS.md` §16 / `TASK_LIFECYCLE.md` §3. Items 2 and 3 were
**repaired by this task** once the decisions they needed arrived (item 2 in
`docs/`, item 3 now scoped to the downstream encoding task because it is a ROW
change this task may not make). The rest remain reported-only.

```text
2. DATABASE.md §1 ITEMS 6–7 WERE STALE AGAINST CARD_RULES.md §4.1.
   *** RESOLVED BY THIS TASK. ***
   Location:    docs/02-technical/DATABASE.md §1 (the "§4.1 authors no
                magnitude for Inferno or Iron Fang and no Shield magnitude at
                all for Tidal Barrier" wording, plus the open-content-gap
                framing).
   Observation: `CARD_RULES.md` §4.1 (v1.5, TASK-110) authors every one of
                those magnitudes; the DATABASE.md statements were true when
                TASK-109 wrote them and TASK-110 made them false.
   Action:      CORRECTED under D-7 (which places the storage statement in
                DATABASE.md) and D-6/D-6b (which place the ROW change
                downstream). §1 now references CARD_RULES.md §4.1 as the
                authored owner and states that the rows are simply not yet
                re-encoded — a D-6 fact — rather than that the content is
                unauthored. No magnitude was restated as a second source.
   Follow-up:   none — closed.

```text
1. TASK-108 STATUS / FOLDER DISCREPANCY.
   Location:    tasks/backlog/TASK-108-resolve-card-effect-resolution-contract.md
   Observation: its `Status:` field reads `IN REVIEW` while the file sits in
                `tasks/backlog/`. `TASK_LIFECYCLE.md` §3/§4 place an IN REVIEW
                task in `tasks/active/`, and §2 lists `IN REVIEW → DONE` as
                landing in `tasks/completed/`. TASK-108's own "Status note"
                explains that it began BACKLOG because
                `TASK_LIFECYCLE.md` §2 forbids `BACKLOG → BLOCKED` — which is
                consistent with its field, not with its folder.
   Impact:      None on TASK-111, which reads TASK-108 as an immutable-in-
                practice precedent and never writes to it. The recorded
                dependency's status VALUE is accurate; only its location is
                inconsistent. A reader resolving this task's register will find
                TASK-108 by name, not by folder.
   Not done:    No move and no re-status — that is the orchestrator's/reviewer's
                act, and this task's Boundary forbids a status transition on any
                task but its own.
   Follow-up:   An orchestrator/reviewer lifecycle correction
                (`tasks/backlog/` → `tasks/active/` for TASK-108). Named, not
                created.

2. DATABASE.md §1's STALE "§4.1 authors no magnitude" STATEMENTS.
   *** MOVED — see item 2 above (RESOLVED BY THIS TASK). *** Duplicate entry
   removed on completion; the resolution record is the one above.

3. card-iron-fang's LANDED effectType DISAGREES WITH CARD_RULES.md §4.1.
   *** NOW SCOPED — the correct member is DECIDED (`Crit`/`Damage`, D-2), so
   this is purely a ROW correction owned by the downstream encoding task. ***
   Location:    the `CardDefinition` row `card-iron-fang` in PostgreSQL
                (effectType "Power") versus docs/01-game-design/CARD_RULES.md
                §4.1 (Iron Fang = damage + increased Crit chance).
   Observation: TASK-109 transcribed a placeholder effectType for a Card whose
                authored effects are not a Power Charge. TASK-109's own Row
                Content Migration table records the same shape for card-inferno.
   Impact:      The stored content is not merely incomplete (Undetermined
                magnitude) — for card-iron-fang the effect IDENTITY is wrong
                against the owning domain document. Any resolver reading the
                row today would select the wrong effect.
   Not done:    NOT repaired. Row content is not this task's to change (no
                migration, no re-encode, no DML — task Boundary and §10 D-6 Row
                Boundary). The correct identity IS now decided: D-2/D-4 give
                `Damage` + `Crit`, so the encoding task has no ambiguity left.
                This fact is recorded in DATABASE.md §1 item 8 so the encoding
                task cannot miss it.
   Follow-up:   The CardDefinition row-content encoding follow-up (TASK-110
                "Reported Consequences" item 1) must correct the identity as
                well as supply the magnitudes. Named, not created.

4. TASK-102's PET SKILL CARD CRITERIA ARE HARD-BLOCKED, NOT ONLY UNBLOCKED-BY-
   MIGRATION.
   Location:    tasks/backlog/TASK-102-implement-card-cast-server-path.md,
                Acceptance Criteria (Inferno damage + Burn; Tidal Barrier Heal
                and Shield; Iron Fang damage + increased Crit chance) and the
                underlying capability.
   Observation: TASK-102 is `READY` and its dependency reasoning treats Tidal
                Barrier's Shield MAGNITUDE as the only open content gap. Two
                further capabilities those criteria require are absent from the
                frozen runtime: (a) the Damage Pipeline performs NO Crit roll by
                design — `DamagePipeline`'s own documentation records that
                `COMBAT_RULES.md` §3.3's roll and its RNG infrastructure do not
                exist in MVP; and (b) `StatusEffectLifecycle` applies and
                expires Status Effects but does NOT tick damage-over-time, so a
                Burn instance produces no damage. Burn's tick schedule is owned
                by `GAME_RULES.md` §17 step 19a / `BOSS_RULES.md` §6.3.1 item 1.
   Impact:      TASK-102's three Pet Skill Card criteria cannot be satisfied by
                reading magnitudes from content alone.
   Not done:    NOT analysed further and NOT repaired — implementing a Crit
                roll would introduce a randomization mechanism the documents
                say does not exist (`AGENTS.md` §11 would require a documented
                RNG rule first), and TASK-102 is read-only here.
   Follow-up:   The capability consequences D-8(c) authorizes, plus possibly a
                GAMEPLAY-CHANGE decision for Crit-roll and Burn-tick behavior
                inside the Damage Pipeline. Whether that is in D-8's scope is
                for the Product Owner to say; recorded here as an observation
                only. Named, not created.

5. FIVE ADDITIONAL CardDefinition ROWS EXIST THAT ARE NOT CONTENT-DEFINED.
   Location:    the same table, rows `heal`, `shield`, `power_charge`,
                `thanh_xa_skill`, each carrying the literal JSON string
                "effect".
   Observation: They are the API/Infrastructure test suites' own smoke fixtures
                and were already recorded by TASK-109, which preserved them
                through the column type change. They are not among the six
                content-defined rows and are not this task's content.
   Impact:      None for this task. Recorded so the "rows unchanged" claim and
                the encoding follow-up's row scope are both exact.
   Not done:    Nothing — no row touched.
   Follow-up:   Test-fixture hygiene, if anyone wants it. Named, not created.
```

### Contract Preservation Verification

```text
Card rules              UNCHANGED (CARD_RULES.md byte-identical; no Cost,
                          effect, or magnitude authored, altered, or restated)
Card EffectDefinition   CHANGED (DATABASE.md §1 — the EXTENDED contract: array
   contract               shape, effectType/valueType sets, extra members,
                          non-semantic ordering, rows-not-yet-encoded, and the
                          corrected §4.1 references. v1.22 → v1.23.)
CardEffectType (code)   UNCHANGED (still the closed enum Heal | Shield | Power —
                          the DECIDED set is broader, but implementing it is
                          D-8(a)'s follow-up, not this task's)
CardEffectValueType     UNCHANGED (code still Flat | PercentMaxHp | Undetermined;
   (code)                 `PercentagePoints` is decided, not implemented — D-8(a))
CardEffectDefinition    UNCHANGED (deserializer and validator untouched; they do
   (code)                 not yet accept an array or the new members — D-8(a))
EF mapping              UNCHANGED (CardDefinitionConfiguration untouched — D-8(b))
Damage Pipeline         UNCHANGED (COMBAT_RULES.md §3; DamagePipeline.cs
                          byte-identical; still performs no Crit roll)
Crit                    UNCHANGED (COMBAT_RULES.md §3.3 unimplemented; no
                          second randomization mechanism introduced. D-3 stored
                          the scope; it did not implement the roll.)
Shield contract         UNCHANGED (COMBAT_RULES.md §4; refresh-not-stack,
                          one instance, depletion at exactly 0)
Burn                    UNCHANGED (GAME_RULES.md §17 step 19a owns the tick;
                          StatusEffectLifecycle.ConsumeAtStep19a still does not
                          tick Burn damage. D-3 stored duration; no tick added.)
StatusEffects[] schema  UNCHANGED (GAME_STATE.md §2.3.1; no state member)
BattleState / PetState  UNCHANGED (no member added)
Redis                   UNCHANGED (no key, field, or TTL)
SignalR                 UNCHANGED (hub method set unchanged; no event added)
API                     UNCHANGED (no endpoint)
PostgreSQL              UNCHANGED (no schema, no migration, no row change —
                          read-only SELECT verification only)
src/ and tests/         UNCHANGED (tree digest re-verified)
docs/                   CHANGED (DATABASE.md ONLY — the task's deliverable)
docs/03-decisions/      UNCHANGED (0 ADRs authored; D-8(e)'s proposal is reported,
                          not written)
TASK-102/103/104/106/107/108  UNCHANGED (byte-identical)
TASK-109 / TASK-110           UNCHANGED (byte-identical)
TASK-082, tasks/completed/    UNCHANGED (byte-identical)
PetSkillCast            NOT IMPLEMENTED (no method, no event, no projection)
Tidal Barrier magnitude STILL "Undetermined" IN THE ROW (the magnitude IS
                          authored in CARD_RULES.md §4.1 by TASK-110; encoding it
                          into the row is D-6/D-6b's downstream act. No value was
                          invented here.)
Additional task files   0 created by this task
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic (no code touched;
      the server remains the sole authority for Card effect resolution)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — no new system,
      mechanic, Card, Pet, Relic, Element, Status Effect, resource, or
      progression axis introduced
- [x] Confirmed **every** recorded element traces to a verbatim Product Owner
      answer — `Damage`/`Burn`/`Crit` plus the retained Heal/Shield/Power (D-2),
      `PercentagePoints`/`duration`/`scope` plus retained `Undetermined` (D-3),
      the array shape (D-1), non-semantic ordering (D-5), row disposition (D-6),
      and ownership (D-7). **No** name, carrier, shape, or ordering rule was
      invented by the agent at any point, including while the two items were
      deferred and the task was BLOCKED
- [x] Confirmed no source code, test, schema, migration, or provisioned row
      changed; the three Pet Skill rows still read `valueType: "Undetermined"`
      (read-only verification). The decided vocabulary is NOT implemented in any
      domain type — that is D-8(a)'s follow-up
- [x] Confirmed `CARD_RULES.md` §4.1 and every other game-design document are
      byte-identical; no frozen combat contract was changed, and no magnitude
      was restated into `DATABASE.md` as a second source (D-7)
- [x] Confirmed the task did not answer a decision question, did not rank or
      recommend an option, did not supply a default, and did not "clean up" an
      answer into a more precise technical decision
- [x] Confirmed no new gameplay rule was authored: `scope: "NextAttack"` stores
      a scope `CARD_RULES.md` §4.1 already states, and `duration` stores Burn's
      duration in the unit `GAME_RULES.md` §17 step 19a already owns
- [x] Confirmed TASK-082/102/103/104/106/107/108/109/110 are byte-identical and
      no task status transition was made other than this task's own
- [x] Confirmed exactly **one** task file was touched (this one); zero
      additional task files were created (confirmed by D-8b)
- [x] Confirmed no ADR was authored and `docs/03-decisions/` is unmodified

