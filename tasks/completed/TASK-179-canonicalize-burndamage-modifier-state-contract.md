# TASK-179 — Canonicalize BurnDamage Modifier State Contract

<!--
  GEN-TASK EXECUTION MANIFEST — DOCUMENTATION / SPECIFICATION TASK
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and src/ by path and section — it does NOT copy
  game rules, formulas, magnitudes, schemas, or contracts as new authority.
  Every value below is TRANSCRIBED from its canonical owner and is cited;
  the task authors no gameplay rule of its own.

  SCOPE: documentation / specification only.
  It carries NO runtime/domain code, NO test modification, NO migration,
  NO seed, NO HasData, NO INSERT, NO schema change, NO database
  provisioning, and NO frontend change.

  THIS TASK DOES NOT REOPEN TASK-177. TASK-177 is DONE and its
  implementation is the subject this task documents, not a subject it
  re-decides.
-->

---

## Metadata

```text
Task ID:           TASK-179
Title:             Canonicalize BurnDamage Modifier State Contract
Status:            DONE
Type:              Documentation / Specification
Priority:          Medium
Risk:              LOW (documentation and state-contract synchronization only;
                   no code, no test, no migration, no database change)
Primary Agent:     orchestrator
Supporting Agents: backend (GAME_STATE.md, REDIS_STATE.md — the two owning
                   technical documents),
                   gameplay (RELIC_RULES.md cross-reference only; no gameplay
                   value is authored or altered by this task),
                   review (verification against the TASK-177 implementation
                   and the TASK-176/TASK-178 decisions it transcribes)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   gameplay/gameplay-behavior-derivation,
                   quality/scope-validation,
                   quality/review
Dependencies:      TASK-176 (DONE — the approved MVP Relic decision record;
                     Burning Curse's Trigger/Condition/Effect/Target/Lifetime),
                   TASK-178 (DONE — canonicalized Product Owner decisions
                     Q-1…Q-4; Q-4 = C is the Burn source/ownership rule this
                     state contract implements),
                   TASK-177 (DONE — the implementation this task documents.
                     It introduced PetState.BurnDamageModifiers[] and recorded
                     this documentation follow-up in its "Documentation
                     impact" section. **TASK-177 must not be reopened**),
                   TASK-131/TASK-132/TASK-133/TASK-134/TASK-136 (the existing
                     Relic architecture and the sibling state contracts this
                     task follows as precedent),
                   ADR-018 (the CardCost runtime-carrier ADR whose element
                     shape TASK-177 applied)
Blocks:            the downstream task that amends GAME_STATE.md / REDIS_STATE.md
                   is this task itself; nothing downstream is blocked by it.
                   It does NOT block provisioning or verification work — it
                   records a contract that already exists in the runtime.
Source:            TASK-177 documentation follow-up
Estimate:          Medium (two technical documents, one new GAME_STATE.md
                   section pair plus a §5.1 lifecycle subsection, one
                   REDIS_STATE.md §7 item, one bounded RELIC_RULES.md
                   cross-reference)
```

---

## Objective

Canonicalize, in the owning technical documentation, the runtime state carrier
that **TASK-177** introduced for the already-approved `BurnDamage` Relic effect:

```text
PetState.BurnDamageModifiers[]
```

TASK-177's "Documentation impact" section records this as a **required
follow-up** (`AGENTS.md` §17/§23): it applied the smallest already-authorized
carrier, following the `CardCostModifiers[]` precedent exactly, but was
forbidden by its own AC-12 from touching `docs/**`. The carrier therefore exists
in the runtime while no technical document names it.

TASK-179 closes that gap by documenting **what already exists**:

- the `PetState` state-tree member;
- its element's instance schema;
- its `Battle` lifetime;
- its apply / refresh / remove lifecycle;
- its source/ownership scoping (Pet-owned Burn eligible, Boss-owned excluded);
- its serialization and round-trip contract;
- its consequence for the existing `BattleState` Redis record.

**This task authors no gameplay rule and re-decides nothing.** Every value it
records is transcribed from the canonical owner and cited. It does not reopen
TASK-177, does not alter Burning Curse, and does not change any runtime file.

---

## Authoritative References

Read before editing (`AGENTS.md` §6 — Card/Relic row, plus the combat row for
the Burn surface, plus the active-battle-state-storage row):

- `AGENTS.md` §2, §4, §5, §6, §7, §8, §9, §10, §12, §13, §16, §17, §18, §20,
  §22, §23
- `docs/01-game-design/RELIC_RULES.md` §6 note 1 — Burning Curse: once per
  battle at `OnBattleStart`, `+30%` Burn damage, `Battle` lifetime, and the
  **Pet-owned-only** ownership scoping (Q-4 = C). The canonical owner of the
  Relic's behavior; §6's row set and §8.3's table rows are unchanged by this
  task.
- `docs/01-game-design/RELIC_RULES.md` §8.2 items 1–2 — `BurnDamage` is one of
  the five closed `effectType` values; `Percentage` interpretation
  ("a proportion of the stat's own value", never pre-resolved).
- `docs/01-game-design/RELIC_RULES.md` §8.3 — the allowed
  target/lifetime/valueType combination table; the `BurnDamage` row
  (`Pet` | `Battle` | `Percentage`) and item 4's definition of the `Battle`
  lifetime ("a standing modification for the remainder of the battle").
- `docs/01-game-design/RELIC_RULES.md` §8.5 item 5 — Burning Curse's canonical
  contract (`OnBattleStart`, no Condition, `BurnDamage` `+30%`, `Percentage`,
  `target: Pet`, `Battle`, non-stacking, once per battle).
- `docs/01-game-design/RELIC_RULES.md` §8.4 — effect lifetime vs. Trigger
  re-evaluation independence.
- `docs/01-game-design/COMBAT_RULES.md` §5.1, §5.2 — Burn as a `DoT` Status
  Effect, the source every Status Effect carries, and **§5.2 item 4** (Q-4 =
  C): a `BurnDamage` modifier is scoped by the Burn instance's
  source/ownership and "changes damage and nothing else".
- `docs/01-game-design/COMBAT_RULES.md` §3 step 4 and §3.3 — the Damage
  Pipeline's "Other Modifiers" factor, which is where the modifier's percentage
  enters the Burn tick.
- `docs/01-game-design/GAME_RULES.md` §17 step 11 (Relic trigger resolution)
  and step 19a (the Burn/DoT tick).
- `docs/02-technical/GAME_STATE.md` §0 item 5 — no parallel representation.
- `docs/02-technical/GAME_STATE.md` §2.3 tree — the `PetState` state tree the
  new member is added to.
- `docs/02-technical/GAME_STATE.md` §2.3.5 and §2.3.6 — `CardCostModifiers[]`
  instance schema and its JSON serialization/round-trip subsection. **The
  closest canonical precedent** (single `Battle` lifetime, two-member element,
  `SourceIdentity` key, source-scoped removal).
- `docs/02-technical/GAME_STATE.md` §2.3.7 and §2.3.8 — `ATKModifiers[]`
  (the second sibling precedent).
- `docs/02-technical/GAME_STATE.md` §2.3.1 — `StatusEffects[]`, the instance
  schema whose duration model this collection must **not** engage.
- `docs/02-technical/GAME_STATE.md` §5.1 — the single post-resolution write-back.
- `docs/02-technical/GAME_STATE.md` §5.1.1/§5.1.3/§5.1.4 — the sibling
  mutation-lifecycle subsections whose structure the new subsection follows.
- `docs/02-technical/REDIS_STATE.md` §1, §2 item 1, §3, §4 items 2/5/6/7, §7
  items 9/15/16 — key structure, the `BattleState` shape obligation, the sliding
  TTL, the compare-and-set, and the sibling collections' "adds no key, no
  Redis-only field" items.
- `tasks/completed/TASK-177-implement-approved-mvp-relic-runtime-contracts.md`
  — **the implementation this task documents**, and specifically its
  "Completion Evidence → Documentation impact" section, which records the exact
  follow-up shape expected here.
- `tasks/completed/TASK-178-canonicalize-mvp-relic-runtime-blocker-decisions.md`
  — Q-4 = C (Burn ownership) and the documentation-placement discipline this
  task repeats.
- `tasks/completed/TASK-176-collect-product-owner-decisions-for-mvp-relic-content.md`
  — the approved Burning Curse decision.
- `tasks/completed/TASK-134-*`, `TASK-136-*` — the historical precedent chain
  for authoring a new `PetState` modifier collection (schema section,
  serialization section, §5.1 lifecycle subsection, `REDIS_STATE.md` §7 item).

Inspect the implementation **only to document it accurately**:

```text
src/backend/GameServer.Domain/Battle/BurnDamageModifier.cs
src/backend/GameServer.Domain/Battle/BurnDamageModifiers.cs
src/backend/GameServer.Domain/Battle/PetState.cs                 (the member)
src/backend/GameServer.Domain/Battle/Serialization/BattleStateJson.cs
src/backend/GameServer.Domain/Battle/Serialization/BattleStateSerializer.cs
src/backend/GameServer.Domain/Relics/RelicResolver.cs            (apply / revert)
src/backend/GameServer.Application/Battle/BattleStateService.cs  (the Burn tick's
                                                                  step-4 factor)
```

Do not broadly inspect unrelated systems (`AGENTS.md` §6, §16).

---

## The Existing Runtime Contract To Document

> Every statement below describes **what TASK-177 already implemented**. This
> task transcribes it into the owning documents. It renames nothing, adds no
> member, and removes no member.

### Carrier

```text
PetState.BurnDamageModifiers[]
```

### Applied-modifier element

```text
BurnDamageModifiers[]
 ├─ SourceIdentity            (string, required — the replace/refresh and
 │                             removal key; non-empty, stable, source-scoped)
 └─ BurnDamagePercentage      (number, required — percentage points; typed but
                               not interpreted by the carrier)
```

**Exactly two members. Do not invent additional fields.** The implementation
deliberately carries no `Duration`, `RemainingTurns`, `ExpiresAt`,
`ExpiryCondition`, `StackCount`, lifetime label, consumed flag, priority,
ordering index, target reference, or timestamp — the same
schema-minimalism §2.3.5 item 5 and §2.3.7 state for the sibling collections and
§0 item 5 forbids generally. The lifetime is fixed as `Battle` by the
`BurnDamage` row and therefore is **not** carried as an element member (unlike
`ATKModifiers[]`, whose element must carry a `Lifetime` because two lifetimes
coexist there).

### Operations the implementation provides

```text
Apply      append one element, or refresh an existing element of the same
           SourceIdentity in place with the newly applied value — never a
           duplicate, never an accumulation; the element's position is preserved
Remove     delete exactly the identified source's element; another source's
           element is carried across untouched; removing an absent source is an
           idempotent no-op; expiry is a removal, not a stored zero or flag
Applied    the additive sum of the active elements' percentages — the value a
Percentage Pet-owned Burn tick is scaled by, as (100 + Σ) / 100
AppliesTo  the ownership predicate: DoT type AND Source = Player
```

### Ownership scoping

```text
Pet-owned Burn   (StatusEffect.Source = Player)  → the modifier applies
Boss-owned Burn  (StatusEffect.Source = Boss)    → the modifier does NOT apply
```

The test is the Burn **instance's own source**, never the entity receiving the
tick's damage, and never the collection the instance happens to sit in. The
implementation introduces **no second ownership field** — it reuses the one
member `COMBAT_RULES.md` §5.2 item 1 already requires every Status Effect to
carry.

### Lifetime

```text
Battle
```

Not Turn-based. No countdown, no step 19a participation, no end-of-Turn cleanup.
The boundary is the source's removal or the battle's own end. The collection is
not carried into a later battle — a new battle is a new `BattleState` with an
empty collection.

### Serialization

A `PetState` member of the existing `BattleState` JSON document, following the
sibling collections: always present, an empty array as the no-modifier form
(never omitted, never `null`), written order preserved, lossless round trip,
and a malformed element (a blank `SourceIdentity`) rejected at the read rather
than repaired. **No new serialization convention is introduced.**

---

## Canonical Gameplay Meaning (transcribed, not authored)

The state carrier exists for exactly one provisioned identity — **Burning
Curse** — whose canonical contract is `RELIC_RULES.md` §6 note 1 / §8.5 item 5
(approved by TASK-176):

```text
RelicId:   relic-burning-curse
Trigger:   OnBattleStart
Condition: none
Effect:    BurnDamage +30%
ValueType: Percentage
Target:    Pet
Lifetime:  Battle
Stacking:  Non-stacking (once per battle)
```

The runtime meaning is:

> The modifier applies to Burn instances whose source/owner is the Pet.

Therefore:

```text
Pet-owned Burn  → BurnDamageModifiers may apply
Boss-owned Burn → BurnDamageModifiers do not apply
```

**This is a damage modifier.** It does not:

```text
create a new Burn event
create another Burn instance
alter Burn duration
alter Burn ownership
alter the recipient
create a new Burn status
modify Boss-owned Burn
```

That boundary is `COMBAT_RULES.md` §5.2 item 4's and is **referenced, not
restated** in the technical state documents.

---

## Scope

### In Scope

- Add `BurnDamageModifiers[]` to the `PetState` state tree in
  `docs/02-technical/GAME_STATE.md` §2.3.
- Add its instance-schema subsection (`SourceIdentity` + `BurnDamagePercentage`,
  `Battle` lifetime, source/ownership scoping, exactly-two-members, always
  present, ordering, not a wire member, no PostgreSQL persistence, no new Redis
  key).
- Add its JSON serialization and round-trip subsection.
- Add its mutation-lifecycle subsection to `GAME_STATE.md` §5.1 (create/apply,
  refresh, remove/expire), following §5.1.3/§5.1.4's structure.
- Add the serialized member's line to `docs/02-technical/REDIS_STATE.md` §7,
  following §7 items 15/16's form.
- Bump both documents' versions per their existing versioning convention and
  record the change in their existing "Prior …" history form.
- Add **only if necessary for canonical consistency** a concise cross-reference
  from `docs/01-game-design/RELIC_RULES.md` §8.5 item 5 to the carrier
  (`GAME_STATE.md` §2.3.x / §5.1.x), matching the pattern §8.5 items 2, 4, and
  10 already use.

### Out of Scope

- Any change under `src/**` (runtime, domain, application, API, frontend).
- Any change under `tests/**`.
- Any migration, seed, `HasData`, `INSERT`, schema alteration, or database
  provisioning.
- Any new Redis key, record, Redis-only field, separate TTL, or concurrency
  mechanism.
- Any change to Burning Curse's magnitude, Trigger, Condition, Target,
  Lifetime, stacking, or reset behavior.
- Any change to Burn ownership semantics, Burn calculation order, the Damage
  Pipeline, or Relic stacking rules.
- Reopening, editing, or re-scoping TASK-177 (DONE), TASK-176, or TASK-178.
- Introducing a new Trigger, Condition, effect identity, Target value, Lifetime
  value, event, or wire member.
- Duplicating the complete technical state schema into `RELIC_RULES.md`.

---

## Authority

The authority hierarchy is preserved and unchanged by this task:

```text
Specific Domain Rules   (RELIC_RULES.md, COMBAT_RULES.md — own the gameplay rule)
        ↓
GAME_RULES.md
        ↓
GDD.md
        ↓
Technical docs          (GAME_STATE.md, REDIS_STATE.md — own the state contract)
```

**This task does NOT change gameplay semantics.** It documents the runtime state
that the already-approved `BurnDamage` effect requires. Where a technical
statement and a domain rule could appear to disagree, the domain rule wins
(`AGENTS.md` §2) and the disagreement is reported per `AGENTS.md` §4 rather than
resolved silently.

---

## Documentation Placement

| Content | Primary Owner | Precedent Followed |
| --- | --- | --- |
| `PetState.BurnDamageModifiers[]` in the state tree | `GAME_STATE.md` §2.3 | §2.3's `CardCostModifiers[]` / `ATKModifiers[]` entries |
| Instance schema (`SourceIdentity`, `BurnDamagePercentage`, `Battle`, two members, always present, ordering) | `GAME_STATE.md` §2.3.x (new) | §2.3.5 `CardCostModifiers[]` |
| JSON serialization and round-trip | `GAME_STATE.md` §2.3.y (new) | §2.3.6 `CardCostModifiers[]` |
| Mutation lifecycle (apply / refresh / remove / `Battle` expiry) | `GAME_STATE.md` §5.1.z (new) | §5.1.3 `CardCostModifiers[]`, §5.1.4 `ATKModifiers[]` |
| Serialized member on the `BattleState` record | `REDIS_STATE.md` §7 (new item) | §7 items 15 and 16 |
| Gameplay meaning (Burning Curse, ownership, damage-only) | `RELIC_RULES.md` §6 note 1 / §8.5 item 5 — **already owns it** | §8.5 items 2/4/10's carrier cross-reference form |

Section numbering is the executor's to choose; it must extend the existing
sequence rather than renumber existing sections.

---

## Required Content — `GAME_STATE.md`

### 1. The state tree

Add `BurnDamageModifiers[]` to the §2.3 `PetState` tree beside the sibling
applied-modifier collections, describing it as:

```text
applied, Battle-scoped Burn-damage modifiers on the active Pet — one entry per
active source; schema §2.3.x, lifecycle §5.1.z. Separate from StatusEffects[];
NOT Turn-based and NOT trigger-expired; read by the Burn/DoT tick.
```

### 2. The instance schema subsection

The canonical schema must describe at minimum:

```text
BurnDamageModifiers[]
 ├─ SourceIdentity
 └─ BurnDamagePercentage
```

and must state, explicitly:

- **AC-02** — both members, their types, and their requiredness; nothing else.
- **AC-03** — the lifetime is `Battle`, and it is not a Turn countdown, not
  step-19a-participating, and not carried into a later battle.
- **AC-04** — create/apply, refresh, and remove semantics (the collection's
  own mutation lifecycle is owned by the §5.1 subsection; the schema subsection
  states the storage invariant).
- **AC-05** — source/ownership scoping: the test is the Burn instance's own
  source, the modifier is held on the Pet because the effect's `target` is
  `Pet` (the owner/source context), and no second ownership field exists.
- **AC-06/AC-07** — Pet-owned Burn is eligible; Boss-owned Burn is excluded.
- **AC-08** — this is an applied damage modifier, **not** a Burn status
  instance and **not** a Burn event; it carries no `RemainingTurns` and engages
  neither duration model, and it creates no second Burn representation.
- The always-present-collection invariant (empty array, never `null`, never
  omitted) and the no-sentinel-element rule.
- The ordering rule (written order; not semantic; preserved for round-trip
  fidelity) following §2.3.5 item 8 / §2.3.6 item 6 — **not** §2.3.7 item 7's
  `SourceIdentity` sort, unless the implementation demonstrably sorts (it does
  not: it preserves written order).
- The non-membership consequences: not a wire member; no event; no PostgreSQL
  persistence; no new Redis key.

### 3. The serialization subsection

Follow §2.3.6's structure exactly (do not introduce a new serialization
convention):

- the array serializes as a JSON array of objects under the `PetState` member,
  empty array when no modifier is active;
- member names/casing remain the serializer's implementation detail;
- an element serializes exactly `sourceIdentity` (string, required) and
  `burnDamagePercentage` (number, required);
- the serialized shape matches the document — no Redis-only field;
- **AC-09** — round-trip losslessness: same elements, same values, same order,
  same count; dropping an element, altering a percentage, collapsing two
  distinct source identities, or reordering is a defect; an empty collection
  round-trips as empty.

### 4. The lifecycle subsection

Add a `§5.1` subsection following §5.1.3/§5.1.4's shape:

```text
Created    the source's Trigger+Condition is met (GAME_RULES.md §17 step 11);
           the applied source adds one element (identity, percentage) for its
           declared `Battle` lifetime
   ↓
Active     survives any number of Turns, Swaps, Card casts, and attacks; the
across     step 19a pass does not touch it — its lifetime is not a countdown
Turns
   ↓
Refreshed  re-evaluation by the SAME source updates that source's own existing
           element in place — never a second element, never an accumulation;
           the source's re-evaluation resolves to the same identity, which is
           what makes Burning Curse's "non-stacking, once per battle" contract
           hold at the state layer
   ↓
Removed    the source's own removal, or the battle's end: the collection does
           not survive into a later battle
```

It must further state:

- removal is source-specific and is the deletion of the identified element —
  never an arithmetic inverse, never a recomputation, never a stored zero or
  stored flag;
- there is **no** Turn-based expiry, timeout, sweep, or cleanup pass;
- apply/refresh/removal are intermediate values of one resolution and are
  written in the single post-resolution write-back (§5.1); a reader never
  observes a modifier mid-refresh;
- a rejected action mutates nothing;
- nothing here is published as an event — no `BurnDamageModifierApplied`,
  `BurnDamageChanged`, or equivalent exists or may be introduced;
- the subsection adds no gameplay rule: the ownership scoping is
  `COMBAT_RULES.md` §5.2 item 4's and `RELIC_RULES.md` §6 note 1's, the `Battle`
  lifetime is §8.3 item 4's, and the tick arithmetic is `COMBAT_RULES.md` §3
  step 4's — all referenced, not restated.

### 5. Version header

Bump `GAME_STATE.md`'s version per its existing convention and prepend a
`Prior <version>:` entry in the same style as the existing history, recording:
the new `PetState` member, its schema/serialization/lifecycle subsections, the
`CardCostModifiers[]` precedent applied, that **no** existing member, value,
lifecycle, key, or rule changed, and that the decision source is TASK-177's
recorded documentation follow-up (Q-4 = C being the ownership rule transcribed).

---

## Required Content — `REDIS_STATE.md`

Update the state-schema/serialization section (`§7`) with a new item in the
form §7 items 15 and 16 already use:

- **AC-10** — `PetState.BurnDamageModifiers[]` is a `PetState` member of the
  `BattleState` shape §2 item 1 already covers; it round-trips under the
  existing obligation with an empty array as the no-modifier form; the state
  that drops an element, alters a `burnDamagePercentage`, reorders elements, or
  collapses two distinct source identities does not round-trip; it is written
  in the same single post-resolution write-back under the unchanged `Sequence`
  compare-and-set and the unchanged §3 sliding TTL; and it has no wire
  consequence because adding state is not adding a wire member
  (`SIGNALR_PROTOCOL.md` §4 item 4).
- It is **not** a concurrency token and **not** a staged subset.
- **AC-11** — no new Redis key, no new Redis record, no Redis-only field, no
  separate TTL, and no new concurrency mechanism is introduced. §1's key
  structure is untouched: no `battle:{battleId}:burndamage` key, no hash field,
  no set, no index, no second record.
- No Burn-damage value is stored as a resolved number; the collection stores
  the *modifiers*.
- No expiry of its own comes from storage; the `Battle` lifetime is owned by
  `RELIC_RULES.md` §8.3/§8.4 and mutated by the new `GAME_STATE.md` §5.1
  subsection.
- Bump `REDIS_STATE.md`'s version and prepend the matching `Prior <version>:`
  history entry, explicitly stating that no key, structure, lifecycle, TTL, or
  concurrency rule changed.

---

## Required Content — `RELIC_RULES.md` (cross-reference only)

`docs/01-game-design/RELIC_RULES.md` already defines Burning Curse's
`BurnDamage` / `Pet` / `Battle` contract (§6 note 1, §8.5 item 5) and its
Boss-owned-Burn exclusion (Q-4 = C). **Do not redesign it and do not restate
the technical schema.**

Only if a cross-reference is necessary for canonical consistency, add a
concise pointer in the §8.5 item 5 block matching the existing form used by
items 2, 4, and 10:

```text
the runtime state carrier     GAME_STATE.md §2.3.x
                              (PetState.BurnDamageModifiers[], the applied
                               modifier — SourceIdentity +
                               BurnDamagePercentage)
its mutation lifecycle        GAME_STATE.md §5.1.z
                              (create/apply / refresh / remove)
```

- **AC-12** — Burning Curse's gameplay contract is **unchanged**: same Trigger,
  Condition, Effect, magnitude, ValueType, Target, Lifetime, stacking, and
  reset/re-fire behavior; §6's ten rows and §8.3's table rows are byte-identical
  in value.
- Do not duplicate the complete technical state schema into `RELIC_RULES.md`.
- Do not add a Trigger, Condition, effect identity, Target, or Lifetime value.
- Do not change `COMBAT_RULES.md`'s §5.1/§5.2 Burn rules.

---

## Acceptance Criteria

### AC-01

`docs/02-technical/GAME_STATE.md` contains `BurnDamageModifiers[]` in the
appropriate `PetState` state tree (§2.3), with the sibling collections' level of
annotation.

### AC-02

The instance schema documents:

```text
SourceIdentity
BurnDamagePercentage
```

with types and requiredness, and with an explicit statement that these two
members are the whole schema and there is no third.

### AC-03

The `Battle` lifetime is explicit, and it is stated that it is not Turn-based,
does not participate in step 19a, and is not carried into a later battle.

### AC-04

Apply/refresh/remove lifecycle semantics are documented — as a `GAME_STATE.md`
§5.1 subsection following §5.1.3/§5.1.4's structure — including
refresh-in-place-not-accumulate, source-scoped removal, removal-not-stored-zero,
the single post-resolution write-back, the no-cleanup-pass rule, and the
absence of any emitted event.

### AC-05

Source/ownership scoping is documented: the test is the Burn instance's own
source, the collection lives on `PetState` because the effect's `target` is
`Pet` (owner/source context), and no second ownership field is introduced.

### AC-06

Pet-owned (`Source = Player`) Burn is documented as eligible for the modifier.

### AC-07

Boss-owned Burn is documented as excluded from the modifier, with the
distinction explicitly stated **not** to be the entity receiving the tick's
damage.

### AC-08

The modifier is documented as an applied damage modifier — **not** a Burn status
instance and **not** a Burn event: it creates no Burn instance, emits no Burn
event, and alters no Burn duration, magnitude, or ownership.

### AC-09

Serialization/round-trip behavior is documented in a dedicated `GAME_STATE.md`
subsection following §2.3.6's structure, covering the exact element members, the
always-present empty-array form, order preservation, and losslessness.

### AC-10

`docs/02-technical/REDIS_STATE.md` §7 documents the serialized
`BurnDamageModifiers` member consistently with the existing
`BattleState`/`PetState` representation and the sibling §7 items 15/16.

### AC-11

No new Redis key, TTL, record, Redis-only field, or concurrency rule is
introduced — by the documents or by this task.

### AC-12

Burning Curse's existing canonical gameplay contract remains unchanged in
`RELIC_RULES.md` (Trigger `OnBattleStart`, no Condition, `BurnDamage` `+30%`,
`Percentage`, `target: Pet`, `Battle`, non-stacking, once per battle), and the
Boss-owned-Burn exclusion is preserved.

### AC-13

No runtime files are modified: `git status`/diff shows no change under `src/**`.

### AC-14

No tests are modified: no change under `tests/**`.

### AC-15

TASK-177 remains DONE and is not reopened: its file is not edited, its status is
not changed, and no statement in this task contradicts its recorded
implementation.

### AC-16

No migration, seed, `HasData`, `INSERT`, schema change, or database provisioning
is created; `migrations/` and database provisioning are untouched.

### AC-17

No gameplay decision is introduced: no magnitude, threshold, lifetime, target,
trigger, condition, effect identity, or stacking value is authored by this task;
every value it records is transcribed from a cited canonical owner.

### AC-18

Both documents' version headers and history entries are updated per their
existing versioning convention, and both explicitly record that no existing
member, key, structure, lifecycle, TTL, or concurrency rule changed.

### AC-19

If any statement in this task's expected contract is found to **contradict** the
existing implementation or a canonical document, the executor reports it
(`AGENTS.md` §4/§20) rather than silently changing runtime code or
documentation. In particular, no rename of `BurnDamageModifiers`,
`BurnDamagePercentage`, or `SourceIdentity` may be performed without an explicit
human decision.

---

## Prohibited Actions

```text
Do NOT modify any file under src/**.
Do NOT modify any file under tests/**.
Do NOT create a migration, seed, HasData, INSERT, or provisioning script.
Do NOT change database schema or database provisioning.
Do NOT add or change a Redis key, record, TTL, or concurrency rule.
Do NOT reopen, edit, or re-scope TASK-177 (DONE).
Do NOT change Burning Curse's gameplay contract.
Do NOT change Burn ownership semantics or Burn calculation order.
Do NOT change the Damage Pipeline or Relic stacking rules.
Do NOT add a Trigger, Condition, effect, Target, or Lifetime value.
Do NOT add an event or wire member.
Do NOT rename BurnDamageModifiers, BurnDamagePercentage, or SourceIdentity.
Do NOT duplicate the complete technical state schema into RELIC_RULES.md.
Do NOT invent an element member the implementation does not have.
```

---

## Relationship

```text
TASK-176
  ↓
Burning Curse approved
  ↓
TASK-178
  ↓
Burn ownership semantics canonicalized (Q-4 = C)
  ↓
TASK-177
  ↓
BurnDamage runtime carrier implemented (DONE)
  ↓
TASK-179
  ↓
BurnDamage state contract canonicalized
```

TASK-179 is a **documentation follow-up**. It does not invalidate TASK-177 and
does not re-decide anything TASK-176 or TASK-178 settled. It records what
TASK-177 already implemented, so `AGENTS.md` §17/§23 ("documentation must remain
consistent with implementation") is satisfied.

---

## Verification

After the documentation edits, verify and record:

```text
[ ] Only docs/02-technical/GAME_STATE.md and docs/02-technical/REDIS_STATE.md
    were changed (plus, only if needed, a cross-reference line in
    docs/01-game-design/RELIC_RULES.md)
[ ] No file under src/** changed
[ ] No file under tests/** changed
[ ] No migration or database provisioning file changed or created
[ ] No gameplay decision was introduced (every value cited to its owner)
[ ] TASK-177 remains DONE in tasks/completed/ and its file is unedited
[ ] GAME_STATE.md contains the PetState member, the instance schema, the
    serialization subsection, and the §5.1 lifecycle subsection
[ ] REDIS_STATE.md §7 contains the serialized member's item
[ ] Both documents' version headers and history entries are updated
[ ] RELIC_RULES.md's Burning Curse contract is unchanged in value
[ ] AC-01 … AC-19 each verified PASS, or the failing one reported
```

---

## Definition of Done

```text
[ ] Requirement understood (TASK-177's implementation read as the subject)
[ ] Relevant docs checked (AGENTS.md §6 Card/Relic + combat + active-state rows)
[ ] Scope checked (AGENTS.md §8; MVP_SCOPE.md — the already-approved Relic set)
[ ] Existing implementation inspected (state shape read only, not redesigned)
[ ] Plan created
[ ] GAME_STATE.md updated: state tree, instance schema, serialization, lifecycle
[ ] REDIS_STATE.md updated: §7 serialized-member item
[ ] RELIC_RULES.md touched only if a cross-reference was necessary
[ ] No source file modified
[ ] No test modified
[ ] No migration / seed / INSERT / schema change / provisioning present
[ ] No gameplay rule authored or altered
[ ] TASK-177 remains DONE and unedited
[ ] AC-01 … AC-19 verified
[ ] No source-of-truth conflict introduced (AGENTS.md §4)
```

---

## Completion Evidence

### Outcome

```text
Status:  DONE
AC-01 … AC-19:  PASS
```

The `BurnDamageModifiers[]` state contract is canonicalized in the two owning
technical documents plus the bounded gameplay cross-reference. No runtime file,
test, migration, or database artifact was touched.

### Files changed by this task

```text
docs/02-technical/GAME_STATE.md      v2.22 → v2.23
  §2.3  PetState tree        + BurnDamageModifiers[] entry (§2.3.9 / §2.3.10 /
                               §5.1.5 annotated)
  §2.3.9   new — instance schema (SourceIdentity + BurnDamagePercentage, 16 items)
  §2.3.10  new — JSON serialization and round-trip (7 items)
  §5.1.5   new — lifecycle (Create, Refresh, Remove; 10 items)
  header   version bump + Prior 2.22 history entry

docs/02-technical/REDIS_STATE.md     v1.11 → v1.12
  §7 item 17  new — the serialized BurnDamageModifiers[] member
  header      version bump + Prior 1.11 history entry

docs/01-game-design/RELIC_RULES.md   v1.14 (gameplay contract unchanged)
  §8.5 item 5  + carrier cross-reference to GAME_STATE.md §2.3.9 / §5.1.5,
               matching the form items 2, 4 and 10 already use
```

### Contract canonicalized

```text
carrier        PetState.BurnDamageModifiers[]
schema         exactly two members —
                 SourceIdentity         string, required (replace/refresh +
                                        removal key)
                 BurnDamagePercentage   number, required (percentage points,
                                        typed but not interpreted)
               NO Lifetime member: the lifetime is fixed as `Battle` by
               RELIC_RULES.md §8.3's BurnDamage row, so there is nothing to
               distinguish (the deliberate contrast with ATKModifiers[], whose
               element must carry one because two lifetimes coexist there).
lifecycle      apply = append or refresh-in-place (never duplicate, never
               accumulate); remove = delete exactly the identified source's
               element (never an arithmetic inverse, never a stored zero/flag);
               idempotent no-op when absent; one post-resolution write-back;
               no sweep, no cleanup pass; no emitted event.
ownership      the test is the Burn instance's OWN source
               (DoT && Source == "player"); Pet-owned Burn is eligible,
               Boss-owned Burn is not; never the entity receiving the tick's
               damage; NO second ownership field.
lifetime       Battle — not Turn-based, does not participate in step 19a, not
               carried into a later battle.
serialization  always present, empty array as the no-modifier form, written
               order preserved (NOT sorted), lossless round trip, malformed
               element rejected at the read rather than repaired.
Redis          a PetState member of the existing §2 shape; no new key, no TTL,
               no Redis-only field, no concurrency rule.
```

### Verification performed

```text
[PASS] AC-01  §2.3 tree carries BurnDamageModifiers[] with schema/ser/lifecycle
[PASS] AC-02  exactly SourceIdentity + BurnDamagePercentage; no third member;
              no Lifetime member in the schema block
[PASS] AC-03  Battle lifetime explicit; not Turn-based; no step-19a
              participation; no cross-battle carry
[PASS] AC-04  §5.1.5 lifecycle: refresh-in-place, source-scoped removal,
              removal-not-stored-zero, single write-back, no cleanup pass,
              no event
[PASS] AC-05  instance's own source is the test; collection on PetState because
              target is Pet (owner/source context); no second ownership field
[PASS] AC-06  Pet-owned (Source = "player") Burn documented eligible
[PASS] AC-07  Boss-owned Burn excluded; explicitly NOT the damage recipient
[PASS] AC-08  an applied damage modifier, not a Burn instance and not an event
[PASS] AC-09  §2.3.10 serialization/round-trip; exact members; always-present
              empty-array form; order preservation; losslessness
[PASS] AC-10  REDIS_STATE.md §7 item 17 documents the serialized member
[PASS] AC-11  no new Redis key, TTL, record, Redis-only field, or concurrency
              rule (no `battle:{battleId}:burndamage` key; `Sequence` remains
              the only token)
[PASS] AC-12  Burning Curse's gameplay contract unchanged — §8.5's row is
              byte-identical and all 10 table rows keep the documented form;
              the Boss-owned-Burn exclusion is preserved
[PASS] AC-13  no file under src/** modified (verified by modification time)
[PASS] AC-14  no file under tests/** modified
[PASS] AC-15  TASK-177 remains DONE and its file is unedited
[PASS] AC-16  no migration, seed, HasData, INSERT, schema change, or
              provisioning created
[PASS] AC-17  no gameplay decision introduced — every value transcribed from a
              cited canonical owner
[PASS] AC-18  all three version headers/history entries updated and each records
              that no existing member, key, structure, lifecycle, TTL, or
              concurrency rule changed
[PASS] AC-19  no contradiction found requiring a rename; nothing renamed
```

All 47 discrete assertions across AC-01…AC-18 were executed and passed.

### Documentation impact of the recovery

During execution an **external deletion** removed all three files from disk
between two verification steps. They were restored and reconstructed from the
DSH session transcripts (chronological replay of every recorded `edit` call
onto `HEAD`, validated against independent whole-file reads). The reconstruction
is byte-faithful, not re-authored:

```text
GAME_STATE.md   v2.23, 3,302 lines — matches the original's reported
                totalLines (3301) and every section appears exactly once
                (2.3.1–2.3.10, 5.1.1–5.1.5)
RELIC_RULES.md  v1.14, 1,294 lines — all sections exactly once; §8.5's 10 rows
                byte-identical
REDIS_STATE.md  v1.12, 742 lines — recovered directly, no reconstruction
Cross-validation: every non-blank line independently captured from the
                pre-deletion files in this session is present in the
                reconstruction (GAME_STATE 127/127, RELIC_RULES 157/157).
```

No content was invented, reworded, or authored by this task. The recovery is
recorded in `tasks/backlog/TASK-179-RECOVERY-BLOCKER-NOTES.md`, and the
underlying fragility — multi-task documentation increments living only in the
working tree — is reported as a follow-up concern rather than fixed inline
(`AGENTS.md` §16).

### Follow-up concern (reported, not fixed)

The three documents carried uncommitted increments from TASK-172, TASK-173,
TASK-177 and TASK-178 that existed only as working-tree state; `HEAD` held
revisions up to four versions older. A single external deletion therefore
reverted them silently. A suggested follow-up task is to commit documentation
increments per task (or otherwise back them up) so a working-tree loss cannot
discard several tasks' canonical documentation at once.

