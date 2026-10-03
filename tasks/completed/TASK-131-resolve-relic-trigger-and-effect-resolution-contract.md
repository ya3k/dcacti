# TASK-131 — Resolve the Relic Trigger and Effect-Resolution Contract

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and tasks/ by path and section.

  THIS TASK DECIDES NOTHING BY ITSELF AND IMPLEMENTS NO BEHAVIOR. It presents
  the documented evidence of a genuine contract gap and requires the
  appropriate human/Product-Owner decision, then records it in the canonical
  owner document(s). Inventing a Relic effect vocabulary, a magnitude carrier,
  a Condition grammar, or a balance value is the single prohibited action of
  this task (AGENTS.md §7, §20).

  PROVENANCE: identified by the post-TASK-130 next-task discovery pass. With
  tasks/backlog/ and tasks/active/ empty, the next unimplemented documented
  MVP dependency on the critical path is the Relic stage (GAME_RULES.md §17
  step 11 "Trigger Relics"; RELIC_RULES.md §3–§7; MVP_SCOPE.md §1 Relics;
  ROADMAP.md Phase 2). The discovery pass verified that the Relic stage cannot
  be implemented: DATABASE.md §1 stores RelicDefinition.EffectDefinition as
  the owning document's VERBATIM prose, R2-7 remains explicitly in force for
  it, and no effect-type discriminator, magnitude field, or Condition grammar
  exists in the schema, the domain type, or any document. This is the exact
  gap TASK-108 resolved for Cards before any Card could execute.

  THIS IS A STOP-DRIVEN TASK, NOT A FEATURE. Per the task-generation stop
  conditions, when "a gameplay rule must be invented" or "a resolution
  contract is undefined" the correct output is a documentation/decision task,
  not an implementation task. No Relic implementation task exists to be
  blocked, so this task records the blocker before such a task is created.

  BOUNDARY: documentation only. Zero files under src/ or tests/. This task
  creates no ADR unless the recorded decision requires one, changes no
  gameplay rule of its own, and adds no Relic behavior.
-->

---

## Metadata

```text
Task ID:           TASK-131
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md). The deliverable is a
                   recorded contract decision plus its entry in the canonical
                   owner document(s). If the decision is that a code/schema
                   change is required, that change is a SEPARATE follow-up task
                   this task creates only after the decision is recorded — not
                   this task's act.
Status:            DONE (Product Owner decisions D1–D11 recorded in full;
                   consequences recorded; the contract authored at its
                   canonical owner, RELIC_RULES.md §8; DATABASE.md §1's
                   RelicDefinition block and its R2-7 scope note synchronized;
                   ADR-018 authored per D11; three follow-up tasks identified.
                   Zero files under src/ or tests/; zero gameplay rules invented.
                   Formal review pass completed against quality/review.md §1 and
                   core/completion.md §1: PASS. File moved from tasks/backlog/
                   to tasks/completed/ per TASK_LIFECYCLE.md §3.)
Risk:              MEDIUM (TASK_TYPES.md §4 — DOCUMENTATION baseline LOW–MEDIUM;
                   MEDIUM because the decision it records governs how every
                   Relic's Trigger, Condition, and Effect is resolved, and it is
                   cross-referenced by RELIC_RULES.md, DATABASE.md,
                   SIGNALR_PROTOCOL.md §3.2.23, and GAME_EVENTS.md. No `docs/`
                   rule is changed by this task beyond recording the answer.)
Priority:          HIGH (the sole blocker on the Relic stage — the last
                   unimplemented documented MVP content dependency on the
                   critical path; GAME_RULES.md §17 step 11 currently has no
                   implementation on either side of the wire.)
Primary Agent:     review (TASK_TYPES.md §2 / AGENT_SELECTION.md §1 —
                   "Documentation change: Primary Agent Review". No domain
                   agent may author the answer; the Product Owner supplies it.)
Supporting Agents: gameplay (RELIC_RULES.md §3–§7 is the owning domain document
                   for Relic behavior — consulted to CONFIRM the trigger and
                   effect semantics the contract must be able to express, not to
                   author the vocabulary),
                   backend (DATABASE.md §1 RelicDefinition and the Domain
                   RelicDefinition/Relic types — consulted to confirm the
                   existing storage shape),
                   realtime (SIGNALR_PROTOCOL.md §3.2.23 RelicTriggered and
                   §3.2.24 PowerChanged are the schema-only wire events the
                   eventual emission must conform to)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      None (all prerequisite content decisions are recorded:
                   TASK-082 A/R2-8 fixed the provisioned/deferred row set;
                   TASK-080/081 fixed the loadout selection surface;
                   TASK-027 fixed Relic ownership and the battle-start snapshot;
                   TASK-038 fixed the loadout snapshot contract.
                   TASK-130 fixed the lifecycle policy this repository's task
                   reconciliation depends on.)
Blocks:            The Relic trigger/effect implementation task (not yet created
                   — it may only be created after this decision is recorded)
Estimate:          Simple (contract decision and canonical-owner documentation
                   only; zero code, zero gameplay rules)
```

---

## Problem Statement

`GAME_RULES.md` §17's fixed resolution order places **step 11 "Trigger Relics"** between resource generation and damage. `RELIC_RULES.md` §7 states `RelicTriggered` is "fired at the point shown in the Event Resolution Rules (`GAME_RULES.md` §17, step 11)". `SIGNALR_PROTOCOL.md` §3.2.23 fixes that event's wire shape but states plainly, in item 4, that "**Emission is not implemented by this contract**" and that "The Relic stage's own task emits the event".

No such task exists, and the discovery pass verified that it **cannot** be written today, because the Relic content rows cannot carry the information a resolver would need to read.

### Verified state of the Relic content contract

| Element | Documented owner | Current encoded state | Sufficient to implement? |
|---|---|---|---|
| `RelicDefinition.Trigger` | `RELIC_RULES.md` §3 (closed 12-value list) | `OnMatchCount`, `OnCombo`, `OnHpBelow` — drawn from §3 | **Yes** — a closed, documented value set |
| `RelicDefinition.Condition` | `RELIC_RULES.md` §1 ("optional extra condition") | free text: `"every 3 Matches"`, `"Combo ≥ 3"`, `"HP < 30%"` | **No** — no grammar, no field, no parse contract |
| `RelicDefinition.EffectDefinition` | `RELIC_RULES.md` §1/§6 | free text: `"+5% ATK"`, `"+10 Power"`, `"Increased Crit chance"`, `"Heal Card cost −50%"` | **No** — no effect-type discriminator, no magnitude, no target/scope |
| `RelicInstance` → `RelicDefinition` | `DATABASE.md` §1–§2 | owned-instance identity, snapshot into `PetState.EquippedRelics[]` | **Yes** — TASK-027/038/080/081/082 |

`DATABASE.md` §1 states the `EffectDefinition` column is "the owning domain document's effect rule text, stored **VERBATIM** and within the 128-char column limit; **no `effect-{slug}` or other effect-id vocabulary** — `RELIC_RULES.md` §6, TASK-082 R2-7".

`DATABASE.md` §1 item 7 (the TASK-108/109 supersession note) confirms this is deliberate and still current:

```text
- `RelicDefinition.EffectDefinition` (below) **still carries R2-7's
  verbatim rule text** in its unchanged `character varying(128)` column.
  No Relic decision exists (Relic effect resolution is `ROADMAP.md` Phase
  2) and none is made here.
```

`TASK-109`'s "Supersession Scope Decision" states the same boundary from the other direction:

```text
Relics — ROADMAP.md Phase 2 (line 46, "All ~10 Relics + trigger/stacking
         system"). No decision, no implementation, and no consumer exists.
         R2-7 REMAINS IN FORCE for RelicDefinition.EffectDefinition.
```

### Why this is a genuine blocker and not an implementation detail

This is the **same class of gap** the repository already had to resolve once for Cards. `TASK-108` was created because `CardDefinition.EffectDefinition` was verbatim prose carrying no effect vocabulary and no magnitude carrier, and its own Problem Statement records the reasoning:

```text
TASK-107 therefore cannot be executed without either inventing an effect
contract or first resolving this one.
```

The Card gap was then resolved by TASK-108 (contract decision D-1–D-4), TASK-109 (structured `EffectDefinition` implementation), TASK-110 (magnitude authoring), TASK-111/112 (multi-effect contract and encoding). Relics have had **none** of that sequence.

A Relic resolver cannot be written without answering, at minimum:

```text
- What is the machine-readable effect type of "+5% ATK"?      NOT DEFINED
- Where does the magnitude 5 live, and is "%" a value type?     NOT DEFINED
- What does "Increased Crit chance" mean numerically?           NOT DEFINED
  (COMBAT_RULES.md §2 item 7 composes EffectiveCrit from a
   `RelicCrit` term but authors no Relic magnitude for it.)
- What is the effect's target/side/scope?                       NOT DEFINED
- How is "every 3 Matches" evaluated against a counter?          NOT DEFINED
- How is "HP < 30%" evaluated — current, max, or snapshot HP?    NOT DEFINED
- How is "Combo ≥ 3" evaluated — the threshold is in text?       NOT DEFINED
- Is a Relic effect permanent, turn-scoped, or battle-scoped?     NOT DEFINED
  (RELIC_RULES.md §1 names "Reset/Cooldown" but authors no value
   for any provisioned Relic.)
```

Inventing any of these is the prohibited action (`AGENTS.md` §7, §20). `RELIC_RULES.md` §6 note 3 already demonstrates the repository's own discipline here: `Burning Curse` was **deferred, not guessed**, precisely because its `Trigger` could not be derived from the documented §3 list.

Note also the pre-existing, explicitly **reported-not-resolved** tension that any decision must account for: `RELIC_RULES.md` §3 requires "exactly one primary Trigger from this list", while §6 note 1 describes `Burning Curse` as a static modifier with no event trigger. `RELIC_RULES.md` §6 note 3 records this as reported and unresolved. This task does not resolve it by assumption; the recorded decision must state whether the contract covers static-modifier Relics or whether that row stays deferred.

---

## Objective

Obtain and record the authoritative Product-Owner decision defining the **Relic Trigger, Condition, and Effect resolution contract** — the machine-readable vocabulary and magnitude carrier that `RelicDefinition` content must be able to express — so that a subsequently created Relic-stage implementation task can be executed without inventing an effect contract, and so that `GAME_RULES.md` §17 step 11's `RelicTriggered` emission has a defined contract to conform to.

This task records the decision and its canonical-owner documentation. It implements no resolver, encodes no content row, and emits no event.

---

## Decision Record (D1–D11)

**Recorded verbatim as supplied by the Product Owner.** Each item answers the correspondingly-numbered item of "Required Decision Coverage". No value below was derived, inferred, or computed by this task.

```text
D1   Relic effects use structured EffectDefinition[].

D2   Magnitudes use Value + ValueType:
     Flat | Percentage | PercentagePoints | Undetermined.

D3   Effects use explicit target/scope vocabulary with
     EffectType-specific allowed combinations.

D4   Assassin Eye = +10 percentage points Crit, NextAttack.

D5   Conditions are structured:
     MatchCountAtLeast(N)
     ComboAtLeast(N)
     HpPercentageBelow(N)
     evaluated against the current resolution state;
     no persistent Relic counters are introduced.

D6   Provisioned Relic effect lifetimes are:
     Berserker Core   = Battle
     Mana Crystal     = Immediate
     Assassin Eye     = NextAttack
     Emergency Core   = Battle
     Trigger re-evaluation is independent from effect lifetime.

D7   Burning Curse remains deferred because the static-modifier
     versus primary-Trigger conflict is not resolved.

D8   The existing closed Trigger list remains complete for MVP.

D9   varchar(128) is insufficient for structured Relic effects;
     a separate schema/storage migration task is required.

D10  RelicTriggered remains { type, relicId };
     resulting state is delivered through existing BattleState projection.

D11  A new ADR is required because this decision establishes
     a cross-layer Relic runtime/content/storage contract.
```

### Consequence mapping — decision to canonical owner

```text
D1, D2, D3, D4, D6, D8  → RELIC_RULES.md §8 (new subsection — the canonical
                            owner of the Relic Trigger/Condition/Effect
                            contract; RELIC_RULES.md §1 is the owner of
                            Relic structure, §3 of the Trigger list)
D5                      → RELIC_RULES.md §8 (Condition grammar)
D7                      → RELIC_RULES.md §6 note 3 UNCHANGED — the row stays
                            deferred; the reported §3-vs-note-1 conflict is
                            NOT resolved by this task (AGENTS.md §4)
D8                      → RELIC_RULES.md §3 UNCHANGED — the closed list stands
D9                      → DATABASE.md §1 RelicDefinition block (storage shape
                            recorded as requiring migration); the migration
                            itself is a SEPARATE follow-up task
D10                     → SIGNALR_PROTOCOL.md §3.2.23 UNCHANGED — the wire
                            shape already matches; emission remains the Relic
                            stage's task
D11                     → docs/03-decisions/ADR/ADR-018-*.md (new)
```

### Supersession scope (TASK-082 R2-7)

D1 requires a structured `EffectDefinition` for Relics, and TASK-082 **R2-7** required verbatim prose and expressly forbade an effect-id vocabulary. These cannot both hold. Following the exact precedent TASK-109 set for the Card member:

```text
R2-7 is SUPERSEDED for RelicDefinition.EffectDefinition by this task's D1.

R2-7 was ALREADY superseded for CardDefinition.EffectDefinition by TASK-108
D-1 / TASK-109.

Therefore, after this task, R2-7 is superseded for BOTH members it governed,
and no member remains under it. R2-7 itself is not edited: TASK-082 is DONE
and immutable (TASK_LIFECYCLE.md §3). Its historical content is preserved.
```

This closes the boundary TASK-109 opened when it stated "R2-7 REMAINS IN FORCE for `RelicDefinition.EffectDefinition`" — that statement described the state of the repository *at that time* and is now superseded by a recorded decision, not by an edit to the completed task.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 (Relics — "~10 Relics", "Trigger system (OnMatch, OnCombo, OnHpBelow, etc.)", "3–5 equipped Relics per battle") — confirms the Relic stage is IN scope
- `docs/00-overview/ROADMAP.md` Phase 2 ("All ~10 Relics + trigger/stacking system") — the phase boundary this stage belongs to
- `docs/01-game-design/GAME_RULES.md` §13 (Relic Rules), §13.3, §13.5, §13.7, §13.8, §16 (Battle Event Model), §17 step 11 ("Trigger Relics"), §20 (Rule Change Policy)
- `docs/01-game-design/RELIC_RULES.md` §1 (Relic Structure — Trigger, Condition, Effect, Reset/Cooldown), §2.2–§2.5 (resolved loadout/snapshot contracts), §3 (Supported Triggers — closed list), §4 (Deterministic Trigger Order), §5 (Anti-Infinite-Chain Rule), §6 (MVP Relic Reference — the effect values), §6 note 1 (static-modifier Relics), §6 note 2 (Emergency Core continuous re-evaluation), **§6 note 3 (the §3-vs-note-1 tension, reported not resolved; the provisioned/deferred row set)**, §7 (Events — `RelicTriggered`)
- `docs/01-game-design/COMBAT_RULES.md` §2 item 5/7 — `RelicCrit` is a named EffectiveCrit term with no authored Relic magnitude; §5 (Burn damage, the modifier "Burning Curse" would change)
- `docs/01-game-design/CARD_RULES.md` §2/§4.1 — the Card effect contract that TASK-108/109/111 established, as the precedent shape a Relic contract would be comparable to (not necessarily identical)
- `docs/02-technical/DATABASE.md` §1 (`RelicDefinition` block — `Trigger`, `Condition`, `EffectDefinition` verbatim-prose constraint under TASK-082 R2-7), §1 item 7 (the TASK-108 supersession explicitly scoped to `CardDefinition` only), §1 item 9 (`Undetermined` magnitude representation), §2 (ownership shape), §3 (constraints)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.23 (`RelicTriggered` — wire shape fixed, emission explicitly not implemented), §3.2.24 (`PowerChanged` — `"relic"` is a documented `source` value with no emitter), §3.2.25 (the `effect summary` omission convention)
- `docs/02-technical/GAME_EVENTS.md` §2/§3 item 7 (`RelicTriggered` payload; the `effect summary` element the omission convention governs)
- `docs/02-technical/GAME_STATE.md` §2.3 (`PetState.EquippedRelics[]` — the battle-scoped equip representation)
- `docs/02-technical/REDIS_STATE.md` §2 (active battle state serialization)
- `docs/03-decisions/README.md` §1, §2, §4 (ADR numbering — never reused), §5, §8 — whether the recorded decision warrants an ADR
- `AGENTS.md` §4 (conflict resolution — report, do not silently resolve), §7 (Game Rule Protection — invent nothing), §8 (MVP Protection), §10 (Server Authority), §17 (Documentation Change Rule), §18 (Architecture Change Rule), §20 (Stop Conditions)
- `tasks/completed/TASK-108-resolve-card-effect-resolution-contract.md` — the direct precedent for this task's shape and the prohibition it applied
- `tasks/completed/TASK-109-implement-structured-effectdefinition-contract.md` — "Supersession Scope Decision" (R2-7 remains in force for Relics)
- `tasks/completed/TASK-082-resolve-pet-card-relic-content-provisioning-contract.md` — decision A / R2-8 (provisioned vs. deferred Relic row set), R2-7 (the verbatim-prose requirement)
- `tasks/completed/TASK-027-relic-ownership-and-battle-start-snapshot.md` — confirmed no Relic trigger engine exists at that point
- `tasks/TASK_LIFECYCLE.md` §3 (SUPERSEDED definition and evidence bar — applied if the decision obsoletes anything)

---

## Scope

### In Scope

1. **Relic Effect vocabulary decision:** the recorded answer to what a `RelicDefinition.EffectDefinition` must be machine-readably able to express — the effect type discriminator, the magnitude carrier, the value type, and the target/side/scope — recorded in its canonical owner document.
2. **Relic Condition grammar decision:** the recorded answer to how a `RelicDefinition.Condition` is evaluated — the counter/threshold/percentage semantics, and which HP or Combo value is read.
3. **Relic Trigger coverage decision:** confirmation of whether `RELIC_RULES.md` §3's closed 12-value list is the complete MVP trigger set as-is, and the recorded disposition of the §3-vs-§6-note-1 static-modifier tension (`Burning Curse`).
4. **Relic effect lifetime decision:** the recorded answer to whether a Relic effect is permanent, turn-scoped, or battle-scoped, and what `RELIC_RULES.md` §1's "Reset/Cooldown" resolves to for each provisioned Relic.
5. **Magnitude authoring decision:** whether every provisioned Relic's magnitude is authored now, or whether the `Undetermined` representation (`DATABASE.md` §1 item 9) applies — in particular the unspecified numeric value behind `Assassin Eye`'s "Increased Crit chance".
6. **Storage-shape consequence:** the recorded decision on whether the existing `character varying(128)` verbatim-prose column remains viable for the decided contract, or whether a schema change is required (`DATABASE.md` §1 / §3).
7. **`RelicTriggered` emission contract confirmation:** confirmation that the decided contract is expressible in the existing `SIGNALR_PROTOCOL.md` §3.2.23 wire shape, and identification of any member the decision requires that the shape does not carry.
8. **Documentation update:** applying the recorded decision to the canonical owner document(s) per `documentation/documentation-change.md`, and cross-referencing it from dependent technical docs — **without** superseding TASK-082 R2-7 for `CardDefinition` and without editing any completed task.
9. **ADR determination:** deciding per `docs/03-decisions/README.md` §2 whether the recorded decision warrants a new ADR, and if so, recording it (next free number — resolve at execution time per §4; numbers are never reused).

### Out of Scope

- **Implementing any Relic resolver, trigger engine, or effect applier** — a separate follow-up task, created only after this decision is recorded
- **Emitting `RelicTriggered` or `PowerChanged`** — the Relic stage's implementation task
- **Encoding or re-encoding any provisioned `RelicDefinition` row** — the encoding follow-up task
- **Adding, removing, or re-scoping a provisioned Relic definition** — `RELIC_RULES.md` §6's row set is fixed by TASK-082 A/R2-8 unless the recorded decision changes it
- **Re-opening `Burning Curse` provisioning** — only if the recorded decision supplies the documented static-modifier Trigger §6 note 3 requires
- **The `CardDefinition` effect contract** — settled by TASK-108/109/111/112; unchanged here
- **Relic stacking behavior** — `RELIC_RULES.md` §2.4 item 6 explicitly defers it
- **Any balance pass on Relic magnitudes beyond recording the decided values** — `ROADMAP.md` Phase 3
- **Modifying any completed or superseded task** — `TASK_LIFECYCLE.md` §3 (DONE and SUPERSEDED are immutable)
- **Reopening TASK-079, TASK-099, or TASK-102** — terminal `SUPERSEDED`
- **Resolving TASK-036's Discord credential decisions** — a separate, independent blocker
- **Any item listed as OUT in `MVP_SCOPE.md` §2**, or anything not IN `MVP_SCOPE.md` §1

---

## Current State

```text
Domain:
  src/backend/GameServer.Domain/Relics/RelicDefinition.cs   Trigger (string),
                                                            Condition (string?),
                                                            EffectDefinition (string)
  src/backend/GameServer.Domain/Relics/Relic.cs             owned instance row
  src/backend/GameServer.Domain/Relics/EquippedRelicIdentity.cs
                                                            the identity
                                                            PetState.EquippedRelics[]
                                                            carries

Application (loadout only):
  src/backend/GameServer.Application/Relics/RelicLoadoutService.cs
  src/backend/GameServer.Application/Relics/RelicLoadoutValidation.cs
  src/backend/GameServer.Application/Relics/IRelicRepository.cs
    → validates and snapshots the loadout at battle start.
      Implements RELIC_RULES.md §2 only. Contains no trigger evaluation.

Resolution pipeline:
  src/backend/GameServer.Application/Battle/BattleStateService.cs
    → ResolveSwapAsync / ExecuteCardCastAsync / ExecutePetSkillCastAsync run
      Match-3 → Passive → Damage → Boss response → outcome.
      GAME_RULES.md §17 step 11 has NO implementation anywhere in this file.

Content rows (provisioned, DATABASE.md §5 item 4):
  relic-berserker-core  OnMatchCount  "every 3 Matches"    "+5% ATK"
  relic-mana-crystal    OnMatchCount  "every 4 Matches"    "+10 Power"
  relic-assassin-eye    OnCombo       "Combo >= 3"         "Increased Crit chance"
  relic-emergency-core  OnHpBelow     "HP < 30%"           "Heal Card cost -50%"
  (relic-burning-curse  DEFERRED — RELIC_RULES.md §6 note 3)

Wire:
  SIGNALR_PROTOCOL.md §3.2.23 RelicTriggered — schema fixed, nothing emits it
  SIGNALR_PROTOCOL.md §3.2.24 PowerChanged   — "relic" source documented, no emitter
```

Verified during discovery: `grep` across `src/backend/` for `RelicTrigger`, `RelicTriggered`, `RelicEngine`, `TriggerRelic` returns **no** implementation — only comments stating the stage belongs to another task (`BattleEvent.cs` L14, L45; `SwapExecution.cs` L172, L371).

---

## Acceptance Criteria

- [x] The Relic `EffectDefinition` contract is recorded as an explicit Product-Owner decision in its canonical owner document, specifying the effect type vocabulary, the magnitude carrier, the value type(s), and the effect target/side/scope (D1/D2/D3 → `RELIC_RULES.md` §8.2–§8.3)
- [x] The Relic `Condition` evaluation contract is recorded, specifying the counter/threshold/percentage grammar and which state value each condition form reads (D5 → `RELIC_RULES.md` §8.1)
- [x] The `RELIC_RULES.md` §3 trigger list is confirmed as complete for MVP, or the added/removed values are recorded (D8 — confirmed complete; §3 unchanged)
- [x] The `RELIC_RULES.md` §3-vs-§6-note-1 static-modifier tension is explicitly dispositioned (resolved, or recorded as an open item with `Burning Curse` remaining deferred) — it is **not** resolved by assumption (D7 — remains deferred and reported; §6 note 3 unchanged)
- [x] The Relic effect lifetime / `Reset`-`Cooldown` semantics are recorded for each provisioned Relic (D6 → `RELIC_RULES.md` §8.4)
- [x] Every provisioned Relic's magnitude is either authored or explicitly recorded as `Undetermined` per `DATABASE.md` §1 item 9 — in particular `Assassin Eye`'s Crit magnitude (D4 — +10 percentage points, `NextAttack`; §8.5)
- [x] The `character varying(128)` verbatim-prose storage question is answered: the existing column is confirmed sufficient, or a schema change is identified as a separate follow-up (D9 — insufficient; separate migration task TASK-132 created)
- [x] It is confirmed whether the decided contract is expressible in the existing `SIGNALR_PROTOCOL.md` §3.2.23 `RelicTriggered` wire shape, and any required member the shape lacks is identified (D10 — shape is unchanged and sufficient; no member required)
- [x] Whether a new ADR is required is decided per `docs/03-decisions/README.md` §2, and if required it is recorded with a never-reused number (D11 — required; recorded as ADR-018)
- [x] TASK-082 R2-7's supersession remains scoped to `CardDefinition` only; the `RelicDefinition` consequence is recorded explicitly (superseded for `RelicDefinition` too by D1; recorded in `DATABASE.md` §1 without editing TASK-082)
- [x] Zero files under `src/` or `tests/` are modified
- [x] Zero gameplay values, formulas, or balance figures are invented — every recorded value is either supplied by the Product Owner or transcribed from an existing authoritative document
- [x] `tasks/completed/` is unmodified; TASK-079, TASK-099, TASK-102, and all completed tasks are byte-identical
- [x] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [x] Quality review checklist passes (`quality/review.md` §1)
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[ ] src/backend/ (none — documentation-only task; verified zero diff)
[ ] src/frontend/client/ (none)
[ ] tests/ (none)
[x] docs/01-game-design/RELIC_RULES.md (§8 added — the canonical owner;
                                         §7 gained the RelicTriggered shape
                                         statement; version → 1.6. §3 and §6
                                         deliberately UNCHANGED per D7/D8)
[x] docs/02-technical/DATABASE.md (§1 RelicDefinition member + new Relic
                                   contract note + R2-7 supersession record;
                                   version → 1.22.1)
[x] docs/02-technical/SIGNALR_PROTOCOL.md (§3.2.23 — §8/ADR-018 citation and
                                            the new item 5 recording the shape
                                            as final per D10; no wire change)
[ ] docs/02-technical/GAME_EVENTS.md (NOT changed — D10 required none)
[ ] docs/01-game-design/COMBAT_RULES.md (NOT changed — the Assassin Eye
                                          magnitude is authored in RELIC_RULES
                                          §8.5 and feeds COMBAT_RULES §2 item
                                          7's existing EffectiveCrit term,
                                          which is unchanged)
[x] docs/03-decisions/ADR/ (ADR-018 added per D11)
[x] docs/03-decisions/README.md (§7 index row for ADR-018; version → 1.10)
[x] tasks/backlog/ (TASK-132 and TASK-133 created as the identified follow-ups)
[ ] tasks/completed/ (MUST remain unmodified — verified)
```

---

## Required Decision Coverage

The executing agent must obtain explicit answers to all of the following. An unanswered item is a blocking stop condition, not an invitation to choose.

```text
 1. Effect vocabulary:
    What machine-readable shape replaces (or augments) the verbatim-prose
    `RelicDefinition.EffectDefinition` for the provisioned Relics? Does the
    Card contract shape (TASK-108 D-1/D-2) apply to Relics, or does Relic
    effect resolution require its own shape?                 NOT DECIDED

 2. Magnitude carrier:
    Where does a Relic's numeric magnitude live, and what value types are
    allowed (`Flat`, `Percentage`, `PercentagePoints`, other)?  NOT DECIDED

 3. Effect target/side/scope:
    Does a Relic effect need a documented target (self/pet/boss), a side, or
    a scope (this instance / the battle / the next damage instance)? It has
    none today.                                                 NOT DECIDED

 4. `Assassin Eye` magnitude:
    "Increased Crit chance" is qualitative. COMBAT_RULES.md §2 item 7 composes
    `EffectiveCrit` from a `RelicCrit` term but authors no Relic magnitude.
    What is the number, and by what value type?                  NOT DECIDED

 5. Condition grammar:
    How is `Condition` evaluated? Specifically:
      - "every N Matches" — against which counter, and does it fire on the
        Nth Match only or on every multiple?
      - "Combo >= N" — the threshold is embedded in free text; where does the
        number live in the contract?
      - "HP < N%" — is the comparison against current HP, max HP, or a
        battle-start snapshot?
    None of the three has a parse or evaluation contract.        NOT DECIDED

 6. Effect lifetime / Reset:
    RELIC_RULES.md §1 defines "Reset/Cooldown: whether/how the trigger can
    re-fire (default: re-fires every time its Trigger/Condition is met, no
    cooldown, unless stated otherwise)" but no provisioned Relic states
    otherwise. Is the default authoritative for all four provisioned rows,
    and is a Relic effect's applied modification permanent, turn-scoped, or
    battle-scoped?                                               NOT DECIDED

 7. Static-modifier Relics:
    RELIC_RULES.md §3 requires "exactly one primary Trigger from this list",
    while §6 note 1 describes `Burning Curse` as a static modifier with no
    event trigger. §6 note 3 reports this tension and defers the row. Does the
    contract cover static-modifier Relics (and how is `Trigger` represented),
    or does `Burning Curse` remain deferred?                     NOT DECIDED

 8. Trigger list completeness:
    Is RELIC_RULES.md §3's 12-value closed list the complete MVP trigger set,
    or does the contract require an additional value?
    ANSWERED (D8) — the existing closed list remains complete for MVP.

 9. Storage shape:
    Does the decided contract fit the existing `character varying(128)`
    verbatim-prose column, or is a schema change required (and therefore a
    separate follow-up task)?
    ANSWERED (D9) — varchar(128) is INSUFFICIENT; a separate schema/storage
    migration task is required (created as TASK-132).

10. Wire expressibility:
    Is the decided contract fully expressible in SIGNALR_PROTOCOL.md §3.2.23's
    `RelicTriggered` shape (`type`, `relicId`), which deliberately omits
    `effect summary` per §3.2.25? Does reporting an applied effect require a
    member the shape does not carry?
    ANSWERED (D10) — the shape is unchanged and sufficient; resulting state is
    delivered through the existing BattleState projection. No member is added.

11. ADR determination:
    Does the recorded decision warrant a new ADR per
    docs/03-decisions/README.md §2?
    ANSWERED (D11) — YES. Recorded as ADR-018
    (docs/03-decisions/ADR/ADR-018-structured-relic-trigger-condition-effect-contract.md).
```

**All 11 items are answered.** Items 1–7 are answered by D1–D7 above; items 8–11 are recorded inline here with their decision reference. No item remained undecided, so no stop condition fired.

---

## Implementation Notes

- **The decision was supplied in full.** All 11 items were answered by the Product Owner as D1–D11 before execution, so the prohibition below did not bind. Recorded for the historical record of what this task was.
- **Invent nothing remains the rule that was observed.** The decision was transcribed verbatim into the Decision Record; no vocabulary, magnitude, grammar, lifetime, or threshold was derived, inferred, or computed by this task. Every value authored into `RELIC_RULES.md` §8 is either the supplied decision or transcribed from §6 / `COMBAT_RULES.md` §2.
- **`TASK-108` was used as the shape precedent only.** It established *how* a contract decision is obtained and recorded. The decision that Relics follow the same structured shape is D1's, not this task's inference.
- **No completed task was edited.** `TASK-082` and `TASK-109` are DONE and immutable (`TASK_LIFECYCLE.md` §3). Their statements about Relics are cited as evidence in the supersession record; neither file was modified. Verified by diff.
- **R2-7's supersession was recorded in the owning documents, not by editing TASK-082.** `DATABASE.md` §1 now records that R2-7 is superseded for `RelicDefinition.EffectDefinition` by D1, which — combined with TASK-109's earlier Card-scoped supersession — leaves no member under R2-7.
- **The `Burning Curse` tension was NOT resolved**, per D7. `RELIC_RULES.md` §3 is unchanged and §6 note 3's report stands. No Trigger was invented for that row.
- **ADR-018 was created** per D11, with the next free number resolved at execution time (`docs/03-decisions/README.md` §4 — numbers are never reused). ADR-017 was the highest existing at execution, so D11's ADR took ADR-018.
- **Server authority was never in question.** The recorded contract is server-side throughout (`GAME_RULES.md` §18, ADR-001); nothing in it places Relic evaluation or effect resolution on the client.
- **TASK-036 is unrelated and remains BLOCKED.** Discord credential handling was not touched.
- **This task was not a broad audit.** Its scope was exactly the Relic trigger/condition/effect contract.
- `tasks/completed/` was not modified.

---

## Testing Requirements

### Required Verification

```text
[x] Documentation consistency — the recorded decision is internally consistent,
                                cites its canonical owner, and introduces no
                                second source of truth
[x] Completeness audit       — all 11 "Required Decision Coverage" items are
                               answered unambiguously
[x] Unmodified-file guards   — verified zero diff in src/, tests/, and
                               tasks/completed/
[x] Cross-reference check    — RELIC_RULES.md, DATABASE.md, SIGNALR_PROTOCOL.md,
                               and COMBAT_RULES.md agree after the edit; any
                               remaining tension is reported per AGENTS.md §4
                               (Burning Curse remains reported and unresolved)
[x] Unit tests               — N/A: this task changes no code
[x] Integration tests        — N/A: this task changes no code
[x] Gameplay scenarios       — N/A: this task implements no gameplay behavior
```

### Key Edge Cases

- The recorded contract must be able to express **all four** provisioned Relics (`RELIC_RULES.md` §6) without inventing a value — including `Assassin Eye`, whose magnitude is currently qualitative
- The recorded contract must state how a Relic whose Trigger is counter-based (`OnMatchCount`) differs from one that is per-event (`OnMatch`, `OnCascade`) — `RELIC_RULES.md` §3 distinguishes them explicitly
- The recorded contract must respect `RELIC_RULES.md` §5's anti-infinite-chain rule and §4's deterministic slot ordering; it must not introduce a chain behavior §5 does not authorize
- The decided magnitude carrier must not silently conflict with `COMBAT_RULES.md` §2 item 7's `EffectiveCrit` composition formula
- If a schema change is decided, the migration is a **separate** follow-up task (`AGENTS.md` §18) — not performed here

---

## Stop Conditions

- **If the required Product-Owner decisions are not supplied: STOP per `AGENTS.md` §7.** Do not invent an effect vocabulary, a magnitude, a Condition grammar, or an effect lifetime.
- If `RELIC_RULES.md` §3's "exactly one primary Trigger" rule and §6 note 1's static-modifier description must both hold and no answer resolves them: STOP and report the conflict per `AGENTS.md` §4 — do not silently pick a side
- If the decision requires a schema or architecture change: STOP and record it as a separate follow-up task per `AGENTS.md` §18 — do not perform it inline
- If the work would require modifying a completed or superseded task: STOP — DONE and SUPERSEDED are immutable (`TASK_LIFECYCLE.md` §3)
- If the task drifts into implementing the Relic resolver, emitting `RelicTriggered`, or encoding content rows: STOP — those are separate follow-up tasks
- If the task is asked to reopen TASK-079, TASK-099, or TASK-102: STOP — terminal `SUPERSEDED`
- If the task is asked to resolve TASK-036's Discord credential decisions: STOP — separate, independent blocker
- If any required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If any part of this task would place gameplay authority on the client: STOP per `AGENTS.md` §10
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Decision Recorded

All 11 required decisions were supplied by the Product Owner as **D1–D11** and recorded verbatim in this file's "Decision Record" section. Item-by-item:

```text
 1. Effect vocabulary          → D1  (structured EffectDefinition[])
 2. Magnitude carrier          → D2  (Value + ValueType: Flat | Percentage |
                                     PercentagePoints | Undetermined)
 3. Effect target/scope        → D3  (explicit target/scope vocabulary with
                                     EffectType-specific allowed combinations)
 4. Assassin Eye magnitude     → D4  (+10 percentage points Crit, NextAttack)
 5. Condition grammar          → D5  (MatchCountAtLeast(N) | ComboAtLeast(N) |
                                     HpPercentageBelow(N), evaluated against the
                                     current resolution state; NO persistent
                                     Relic counters)
 6. Effect lifetime / Reset    → D6  (Berserker Core = Battle, Mana Crystal =
                                     Immediate, Assassin Eye = NextAttack,
                                     Emergency Core = Battle; trigger
                                     re-evaluation independent of lifetime)
 7. Static-modifier Relics     → D7  (Burning Curse remains deferred; the
                                     §3-vs-§6-note-1 conflict is NOT resolved)
 8. Trigger list completeness  → D8  (the existing closed list remains complete)
 9. Storage shape              → D9  (varchar(128) insufficient; separate
                                     migration task required)
10. Wire expressibility        → D10 (RelicTriggered stays { type, relicId };
                                     state via the existing BattleState
                                     projection)
11. ADR determination          → D11 (a new ADR is required)
```

Recurring constraints the decisions carry, recorded so the follow-up tasks inherit them:

```text
- NO persistent Relic counters are introduced (D5).
- Trigger re-evaluation is INDEPENDENT of effect lifetime (D6).
- The closed Trigger list is unchanged (D8).
- Burning Curse stays deferred; the reported conflict stays reported (D7).
- RelicTriggered gains no member (D10).
- R2-7 is superseded for RelicDefinition.EffectDefinition (D1).
```

### Changed Files

- `docs/01-game-design/RELIC_RULES.md` — §8 added (the canonical owner of the Relic Trigger/Condition/Effect contract: §8.1 Condition forms, §8.2 structured `EffectDefinition[]`, §8.3 target/lifetime vocabulary and per-`effectType` allowed combinations, §8.4 lifetime-vs-re-evaluation independence, §8.5 the four provisioned rows as encoded values, §8.6 the storage consequence, §8.7 startup status); §7 gained the `RelicTriggered` shape statement; version header → 1.6. §3 and §6 are **unchanged** (D7/D8).
- `docs/02-technical/DATABASE.md` — the `RelicDefinition.EffectDefinition` member description updated to the structured `jsonb` shape with the supersession and migration notes; a new "Relic `EffectDefinition` and `Condition` contract" note added (6 items: representation owner, R2-7 supersession, insufficient storage, separate migration, no value authored/no execution, loud rejection); the `CardDefinition` block's R2-7 sentence corrected to record the Relic supersession; §1 item 7 corrected (`RelicDefinition` no longer "still carries R2-7's verbatim rule text"); version header → 1.22.1.
- `docs/02-technical/SIGNALR_PROTOCOL.md` — §3.2.23 item 4 now cites `RELIC_RULES.md` §8/`ADR-018`; a new item 5 records that the `{ type, relicId }` shape is **final and carries no effect summary** (D10). No wire member added.
- `docs/03-decisions/ADR/ADR-018-structured-relic-trigger-condition-effect-contract.md` — **new**. Records the cross-layer architectural decision (D11): 11 decision items, consequences, 6 alternatives considered with their rejection reasons, and the related-document set.
- `docs/03-decisions/README.md` — §7 ADR index gained the ADR-018 row; version header → 1.10.
- `tasks/backlog/TASK-132-migrate-relic-structured-condition-and-effectdefinition-storage.md` — **new** (D9's required separate migration task).
- `tasks/backlog/TASK-133-implement-server-authoritative-relic-trigger-and-effect-resolution.md` — **new** (the Relic resolution stage `GAME_RULES.md` §17 step 11).
- `tasks/completed/TASK-131-resolve-relic-trigger-and-effect-resolution-contract.md` — this file.

### Validation Results

```text
Documentation consistency   PASS — RELIC_RULES.md §8 is the single canonical
                            owner; DATABASE.md records only the storage
                            consequence and restates no rule; no second source
                            of truth introduced (.ai/README.md §6)
Completeness audit          PASS — all 11 "Required Decision Coverage" items
                            answered (see above)
Cross-reference check       PASS — RELIC_RULES.md §8, DATABASE.md §1,
                            SIGNALR_PROTOCOL.md §3.2.23, and ADR-018 agree;
                            the one remaining tension (Burning Curse) is
                            reported per AGENTS.md §4 and deliberately
                            unresolved per D7
Stale-statement sweep       PASS — grep confirmed no remaining "R2-7 REMAINS
                            IN FORCE" or "No Relic decision exists" statement
                            in docs/; the two found were corrected
Unmodified-file guard       PASS — git diff confirms ZERO changes under src/
                            and tests/, and ZERO changes under tasks/completed/
                            other than the pre-existing reconciliation set
Unit tests                  N/A — this task changes no code
Integration tests           N/A — this task changes no code
Gameplay scenarios          N/A — this task implements no gameplay behavior
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed zero files modified under `src/` or `tests/`
- [x] Confirmed `tasks/completed/` unmodified (including TASK-079, TASK-099, TASK-102)
- [x] Confirmed no completed task (TASK-082, TASK-109) was edited; R2-7's supersession is recorded in the owning documents, not by amending them
- [x] Confirmed no gameplay rule, magnitude, or threshold was invented — every value is the supplied decision or transcribed from `RELIC_RULES.md` §6 / `COMBAT_RULES.md` §2

### Follow-Up Tasks Identified

- **TASK-132 — Migrate Relic Structured Condition and EffectDefinition Storage** (`tasks/backlog/`) — required by D9. Moves `RelicDefinition.Condition` and `RelicDefinition.EffectDefinition` from `character varying(128)` to the structured representation, updates the Domain content type and EF configuration, and encodes the four provisioned rows. **Blocks TASK-133.**
- **TASK-133 — Implement Server-Authoritative Relic Trigger and Effect Resolution** (`tasks/backlog/`) — the `GAME_RULES.md` §17 step 11 stage: trigger evaluation, structured condition evaluation, effect application, deterministic slot ordering, the anti-infinite-chain queue, and `RelicTriggered` emission. Depends on TASK-132.
- **Client Relic presentation** — not created. `RELIC_RULES.md` §8.7 and `ADR-018` item 10 keep the resulting state in the existing `BattleState` projection, and the client already renders battle state. If a dedicated Relic presentation surface is later judged necessary, it is a separate client task (`ADR-003`, `ARCHITECTURE.md` §2.2) and was deliberately not presumed here.
- **`Burning Curse` provisioning** — not created. Its `Trigger` conflict remains reported and unresolved (`RELIC_RULES.md` §6 note 3, D7). No task may be created for it until a documented static-modifier `Trigger` exists.
