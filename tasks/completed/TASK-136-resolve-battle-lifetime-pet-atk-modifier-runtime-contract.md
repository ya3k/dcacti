# TASK-136 — Resolve the Battle-Lifetime Pet ATK Modifier Runtime Contract

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and tasks/ by path and section.

  THIS TASK IS STRICTLY A DECISION-INPUT TASK.

    Documented ambiguity
            ↓
    Present decision options
            ↓
    Obtain explicit Product Owner / human decision
            ↓
    Record the decision in TASK-136
            ↓
    STOP

  THIS TASK MUST NOT APPLY THE DECISION TO AUTHORITATIVE DOCUMENTATION.
  The subsequent contract-application task consumes the recorded decision and
  updates the canonical documents. TASK-136's deliverable is the RECORD, not the
  documentation change.

  THIS TASK DECIDES NOTHING BY ITSELF AND IMPLEMENTS NO BEHAVIOR. It presents
  the documented evidence of a genuine state-carrier and composition gap and
  requires the appropriate human/Product-Owner decision. Inventing a state
  member, a shape, a field set, a composition rule, a stacking rule, or an
  expiration rule is the single prohibited action of this task
  (AGENTS.md §7, §20).

  DECISION OWNERSHIP: D1–D12 are NOT implementation decisions for the executing
  agent. No option may be selected because it is easier to implement, consistent
  with an existing implementation, similar to `CardCostModifiers[]` or
  `NextAttackCritModifiers[]`, simpler for Redis, simpler for SignalR, simpler
  for the Damage Pipeline, or architecturally convenient. `ADR-017` and
  `ADR-018`'s TASK-134 amendment are precedent/evidence only — they are NOT the
  answer, and TASK-134's CardCost answer must NOT be copied.

  PROVENANCE: identified during TASK-132's execution and recorded there as a
  discovered, deliberately unresolved ambiguity (TASK-132 Completion Evidence:
  "the `ATK | Pet | Battle | Percentage` declaration is stored, and the runtime
  ATK ambiguity this task was told not to resolve remains unresolved"), and
  independently confirmed by TASK-133's correctly-stopped attempt. TASK-132
  landed the structured content storage (`RelicDefinition.Condition`,
  `RelicDefinition.EffectDefinition[]`); it did not implement runtime
  resolution. The authoritative content contract declares Berserker Core's
  effect, but no document defines where an APPLIED, Battle-lifetime Pet ATK
  modifier LIVES in authoritative battle state, nor how it composes into
  effective Pet ATK.

  THIS IS A STOP-DRIVEN TASK, NOT A FEATURE. Per the task-generation stop
  conditions, when "a gameplay rule must be invented" or "the state contract is
  undefined" the correct output is a decision task, not an implementation task.
  TASK-133 remains BLOCKED from implementing Berserker Core's ATK effect until
  this resolves; TASK-133 is NOT modified by this task.

  BOUNDARY: decision recording only. Zero files under docs/, src/, or tests/.
  This task creates no ADR, changes no gameplay rule, adds no Relic or combat
  behavior, and does not touch TASK-131, TASK-132, TASK-133, or TASK-134.
-->

---

## Metadata

```text
Task ID:           TASK-136
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md). This task is the
                   DECISION-INPUT half of that workflow only: it obtains and
                   records the Product Owner decision. It does NOT perform the
                   canonical-owner documentation write that the workflow's
                   later steps describe — that is the subsequent
                   contract-application task's act, consuming this record.
Status:            DONE (the Product Owner supplied an explicit answer for all
                   twelve decision items D1–D12. The decision was recorded
                   verbatim in "Decision Record" without reinterpretation or
                   added assumption, and the twelve "Required Decision
                   Coverage" items were resolved accordingly. TASK-136 modified
                   no file other than this one: zero `docs/`, zero `src/`, zero
                   `tests/`, no ADR, no runtime state carrier, and TASK-131/
                   132/133/134 are byte-identical. The canonical documentation
                   write is the SUBSEQUENT contract-application task's act,
                   consuming this record — TASK-136 does not perform it.)
                   Lifecycle reconciliation: recorded DONE while the file remained
                   in tasks/backlog/; moved to tasks/completed/ per
                   TASK_LIFECYCLE.md §3 (direct execution verified: D1–D12
                   recorded, all thirty-six acceptance criteria satisfied).
Risk:              MEDIUM (TASK_TYPES.md §4 — DOCUMENTATION baseline LOW–MEDIUM;
                   MEDIUM because the decision it records will govern
                   authoritative battle state, its Redis serialization, the
                   Damage Pipeline's Step-1 input, and potentially the SignalR
                   projection — the same class of state-model change ADR-017 and
                   ADR-018's TASK-134 amendment each required an ADR or an ADR
                   amendment for. No `docs/` file is modified by this task.)
Priority:          HIGH (the sole remaining blocker on TASK-133's Berserker Core
                   path — the last unimplemented documented MVP Relic effect
                   alongside Emergency Core, and a dependency of GAME_RULES.md
                   §17 step 11.)
Primary Agent:     review (TASK_TYPES.md §2 / AGENT_SELECTION.md §1 —
                   "Documentation change: Primary Agent Review". No domain
                   agent may author the answer; the Product Owner supplies it.)
Supporting Agents: gameplay (COMBAT_RULES.md §5.4 owns the Pet-side ATK
                   modifier rule for `BuffDebuff`; RELIC_RULES.md §8 owns the
                   Relic effect contract — consulted to CONFIRM the semantics
                   the carrier must express, not to author the answer),
                   backend (GAME_STATE.md §2.3 owns the PetState member set;
                   the Domain PetState/DamagePipeline are consulted to confirm
                   the existing representation),
                   realtime (GAME_STATE.md §2.3.1's wire note,
                   SIGNALR_PROTOCOL.md §4.2/§4.3, REDIS_STATE.md §2/§7 — the
                   serialization and projection consequences the decision must
                   address)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-131 (DONE — the Relic Trigger/Condition/Effect contract;
                   this task resolves only its missing runtime ATK
                   representation and composition),
                   TASK-132 (DONE — the structured content storage that carries
                   the `ATK | Pet | Battle | Percentage` declaration; it stored
                   the declaration and deliberately resolved no runtime),
                   ADR-017 (DONE — the precedent for a purpose-built PetState
                   modifier collection and its source-scoped removal key;
                   EVIDENCE ONLY, not the answer),
                   ADR-018 (DONE — amended by TASK-134 to record the CardCost
                   runtime carrier. It explicitly states it "defines no
                   stacking, scaling, or interaction behavior for `ATK`";
                   EVIDENCE ONLY, and its CardCost answer must NOT be copied)
Blocks:            TASK-133 — Implement Server-Authoritative Relic Trigger and
                   Effect Resolution (its Berserker Core / `ATK` path cannot be
                   implemented until this is resolved)
Estimate:          Simple (one recorded runtime contract decision, D1–D12; zero
                   code, zero documentation edits, zero gameplay rules)
```

---

## Objective

Obtain and record, **in this task only**, the explicit Product-Owner / human
decision defining the **authoritative runtime representation and composition of
an applied, Battle-lifetime Pet ATK percentage modifier** — such as Berserker
Core's `+5% ATK` — covering its state carrier, stored shape, target, lifetime,
ATK composition, multi-modifier semantics, same-source retrigger behavior,
removal/expiration, serialization, Redis persistence, SignalR projection, and
document ownership — so that a subsequent contract-application task can update
the canonical documents and make TASK-133 implementation-ready.

This task records the decision. It implements no state member, no ATK
calculation, and no resolver, and it **applies the decision to no authoritative
document**.

---

## Current State

```text
Relic content contract (TASK-131, ADR-018) — COMPLETE, do not reopen:
  RELIC_RULES.md §8.2  effectType set: ATK | Power | Crit | CardCost
  RELIC_RULES.md §8.3  ATK | Pet | Battle | Percentage
  RELIC_RULES.md §8.5  Berserker Core: value 5, target Pet, lifetime Battle
  RELIC_RULES.md §8.4  effect lifetime is INDEPENDENT of trigger re-evaluation

Relic structured content storage (TASK-132) — COMPLETE:
  RelicDefinition.Condition          structured (RelicCondition)
  RelicDefinition.EffectDefinition[] structured (RelicEffectDefinitions)
  The four provisioned rows hold RELIC_RULES.md §8.5's values.
  → The DECLARATION is stored. No runtime resolution exists.

PetState (GAME_STATE.md §2.3; src/backend/GameServer.Domain/Battle/PetState.cs):
  PetId, HP, MaxHP, ATK, DEF, Crit, Power, Element,
  PassiveId, PassiveProgress, PassiveResetOverride?,
  EquippedRelics[], EquippedCards[],
  StatusEffects[], NextAttackCritModifiers[], CardCostModifiers[]
  → ATK is the BASE Pet ATK only. No Battle-lifetime ATK modifier member.

Existing modifier precedents (EVIDENCE, not the answer):
  NextAttackCritModifiers[]  PetState member + own schema (§2.3.4) +
                             SourceIdentity removal key + own lifecycle
                             (§5.1.2) + own round-trip obligation + own
                             "not a wire member" ruling          [ADR-017]
  CardCostModifiers[]        PetState member + own schema (§2.3.5) +
                             SourceIdentity removal key + own lifecycle
                             (§5.1.3) + own round-trip obligation + own
                             "not a wire member" ruling          [TASK-134 /
                                                                 ADR-018 amend]
  StatusEffects[]            governed by COMBAT_RULES.md §5.1's CLOSED type
                             list; a new type is a COMBAT_RULES decision
                             (GAME_STATE.md §2.3.3)

Existing Pet ATK composition (COMBAT_RULES.md §5.4) — SCOPE-LIMITED:
  §5.4.1  EffectiveATK = truncate( ATK × (100 − |Magnitude|) / 100 )
          derived at attack resolution — NOT stored; changes the
          Damage Pipeline Step-1 `Attack` input
  §5.4.4  the base stat is never overwritten; no restore step
  §5.4.5  "Applies to  a Turn-based BuffDebuff instance with
                       TargetStat = ATK"
  → §5.4 is the canonical owner of the BuffDebuff consumption rule ONLY.
    It is NOT a Relic-effect rule, it is reduction-only (absolute value),
    and §5.4.5 does not extend it to any other source.

Documented prohibitions the answer must respect:
  GAME_STATE.md §2.3.3   no queued/pending collection, no second
                         representation of an in-flight application
  GAME_STATE.md §0 item 4  a field is added by its owning decision, not ahead
                           of it
  GAME_STATE.md §0 item 5  no second representation of a value
  RELIC_RULES.md §2.4 n6   "This section defines no stacking, scaling, or
                           interaction behavior for ATK, Power, Crit, or any
                           other effectType — those remain exactly where this
                           item left them: undefined, and a future rule change
                           (GAME_RULES.md §20)."
```

---

## Problem / Ambiguity

`RELIC_RULES.md` §8.3 fixes `ATK` as a defined effect combination:

```text
| `effectType` | `target` | `lifetime` | `valueType` |
| `ATK`        | `Pet`    | `Battle`   | `Percentage` |
```

and §8.5 transcribes Berserker Core as:

```text
Trigger:    OnMatchCount
Condition:  MatchCountAtLeast(3)
Effect:     { "effectType": "ATK", "valueType": "Percentage",
              "value": 5, "target": "Pet", "lifetime": "Battle" }
```

That is a complete **declaration**. What no document defines is where an
**applied** Battle-lifetime Pet ATK modifier lives in authoritative battle
state, and how it composes into the effective Pet ATK the Damage Pipeline
consumes.

### The documented gap (preserved verbatim in substance — do not solve it here)

```text
RELIC_RULES.md §8.3
    ATK | Pet | Battle | Percentage
              ↓
Berserker Core
              ↓
+5% ATK

BUT

PetState.ATK is the base Pet ATK
No PetState member carries a Battle-lifetime ATK modifier
COMBAT_RULES.md §5.4 is BuffDebuff-scoped, not Relic-scoped
No authoritative calculation path consumes a Relic-sourced ATK modifier
```

### Verified state of the representation

| Question | Answer today |
|---|---|
| Is `ATK` a defined `effectType`? | **Yes** — `RELIC_RULES.md` §8.2 item 1, §8.3 |
| Is Berserker Core's magnitude authored? | **Yes** — `RELIC_RULES.md` §8.5 (value 5) |
| Is the declaration stored? | **Yes** — TASK-132's structured `EffectDefinition[]` |
| Does `PetState` carry a Battle-lifetime ATK modifier? | **No** — see the member list above |
| Does any other state type carry one? | **No** |
| Does any document define Relic-sourced ATK composition? | **No** — `RELIC_RULES.md` §2.4 item 6 says so explicitly |
| Does `COMBAT_RULES.md` §5.4's `EffectiveATK` apply? | **No** — §5.4.5 scopes it to `BuffDebuff` instances |

`GAME_STATE.md` §2.3's `PetState` member set, as implemented in
`src/backend/GameServer.Domain/Battle/PetState.cs`, is:

```text
PetId, HP, MaxHP, ATK, DEF, Crit, Power, Element,
PassiveId, PassiveProgress, PassiveResetOverride?,
EquippedRelics[], EquippedCards[],
StatusEffects[], NextAttackCritModifiers[], CardCostModifiers[]
```

None of these is a Battle-lifetime ATK modifier. The two purpose-built modifier
collections are each explicitly and narrowly scoped to their own concept:
`NextAttackCritModifiers[]` is Crit- and `NextAttack`-specific by construction
(`GAME_STATE.md` §2.3.4; `ADR-017`), and `CardCostModifiers[]` is Card-cost
specific — `GAME_STATE.md` §2.3.5 states it exists "so a Relic can reduce a
Card's cost", its only payload member is `CostReductionPercentage`, and
`ADR-018`'s amending record states in terms that it "defines no stacking,
scaling, or interaction behavior for `ATK`, `Power`, `Crit`, or any other
`effectType`, all of which remain undefined under `RELIC_RULES.md` §2.4 item 6
and a future rule change (`GAME_RULES.md` §20)".

`StatusEffects[]` is governed by `COMBAT_RULES.md` §5.1's closed MVP type list
(Burn, Shield, Buff/Debuff) plus Stun (`GAME_STATE.md` §2.4.5) — §2.3.3
explicitly states that introducing another Status Effect type "is a gameplay
decision owned by `COMBAT_RULES.md`".

The existing Pet ATK composition rule does not close the gap either.
`COMBAT_RULES.md` §5.4 is the canonical owner of how a **Turn-based `BuffDebuff`
Status Effect's** `Magnitude` reaches `ATK`, and its own scope statement
§5.4.5 fixes that:

```text
Applies to      a Turn-based BuffDebuff instance with TargetStat = "ATK"
                consumed by the owning Pet's own attack (Player → Boss)
Does NOT apply  to a BuffDebuff naming any stat other than "ATK"
Does NOT apply  to a DoT tick or a Shield: those Types have their own rules
                and are not ATK modifiers
```

§5.4 is therefore a **`BuffDebuff`-scoped** rule whose formula is
reduction-only (`truncate( ATK × (100 − |Magnitude|) / 100 )`, using the
absolute value). Berserker Core is neither a `BuffDebuff` instance nor a
reduction: it is a Relic effect declared `+5%`, an increase, with `Battle`
lifetime and no `RemainingTurns`. Applying §5.4's formula unchanged would
invert the declared direction; `COMBAT_RULES.md` §5.5's Boss-side note makes the
same point from the other direction, stating the two conventions "must **not**
be collapsed into one shared formula". Whether §5.4 is extended, a sibling rule
is authored, or the composition is defined elsewhere is a decision, not a
detail — and §5.4.5 already states that a source for which no consumption rule
is defined "is **not** silently treated as an ATK modifier".

### Why this is a genuine gap and not an implementation detail

Three separate things are undefined, and each is a decision rather than a
detail:

1. **Where the applied modifier lives.** Adding a `PetState` member is a
   battle-state-model change — the exact class of change `AGENTS.md` §18 lists
   as requiring an ADR, and the class `ADR-017` was written for when
   `NextAttackCritModifiers[]` was introduced and `ADR-018` was amended for when
   `CardCostModifiers[]` was. `GAME_STATE.md` §0 item 4 forbids adding a field
   ahead of its owning decision, and §0 item 5 forbids a second representation
   of a value.
2. **How it composes into effective Pet ATK.** Whether the Relic modifier
   reaches the Damage Pipeline Step-1 `Attack` input, composes with §5.4's
   `EffectiveATK`, becomes a Step-4 factor, or something else is a composition
   decision no document states. The percentage's direction (increase vs
   decrease), its rounding, and any cap are equally unstated.
3. **Composition, retrigger, and removal semantics.** `RELIC_RULES.md` §2.4
   item 6 explicitly states it "defines no stacking, scaling, or interaction
   behavior for `ATK`", and names that as a future rule change under
   `GAME_RULES.md` §20. Berserker Core's Condition is cumulative
   (`MatchCountAtLeast(3)`, `RELIC_RULES.md` §8.1), so "triggers again while
   already active" is a real case, not a hypothetical — and `RELIC_RULES.md`
   §8.4 keeps trigger re-evaluation independent of effect lifetime.

Inventing any of these is the prohibited action (`AGENTS.md` §7, §20). Note the
repository's own discipline here: `Burning Curse` was deferred rather than
guessed (`RELIC_RULES.md` §6 note 3), `ADR-017` exists precisely because a
modifier with no defined state carrier could not be implemented safely, and
TASK-133 was correctly stopped rather than guessing this one.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 (Relics IN — "~10 Relics", trigger system), §4 (unlisted ⇒ FUTURE)
- `docs/00-overview/ROADMAP.md` Phase 2 (Relic trigger system)
- `docs/01-game-design/RELIC_RULES.md` **§8** (the Relic contract this task does not reopen): §8.2 item 1 (`effectType` set incl. `ATK`), item 2 (`valueType` set incl. `Percentage`), **§8.3** (the allowed-combination table: `ATK` | `Pet` | `Battle` | `Percentage`; item 4's "`Battle` denotes a standing modification for the remainder of the battle"), §8.4 (effect lifetime vs trigger re-evaluation), **§8.5 item 1 (Berserker Core's effect)**, §8.6 (storage consequence), §8.7 (startup status)
- `docs/01-game-design/RELIC_RULES.md` §1 (Relic structure), §2 item 1 (Relics are carried by the active Pet), **§2.4 item 6 ("This section defines no stacking, scaling, or interaction behavior for `ATK`, `Power`, `Crit`, or any other `effectType` — those remain … undefined, and a future rule change (`GAME_RULES.md` §20)")**, §3 (`OnMatchCount`), §4 (deterministic trigger order), §5 (anti-infinite-chain rule), §6 (the MVP Relic Reference; Berserker Core "+5% ATK"), §6 note 3 (the deferred row set), §7 (Events)
- `docs/01-game-design/COMBAT_RULES.md` **§5.4** (the canonical owner of how a Turn-based `BuffDebuff` `Magnitude` reaches `ATK`): **§5.4.1** (the consumption point and `EffectiveATK` formula), §5.4.2 (rounding, truncated toward zero), §5.4.3 (activity and duration), **§5.4.4** (the base stat is never overwritten; `EffectiveATK` is not persisted and is not a second representation), **§5.4.5** (the scope statement that fixes `BuffDebuff`-only applicability), §5.5/§5.5.1–§5.5.3 (the deliberately separate Boss-side convention), **§3/§3.1** (the Damage Pipeline's fixed six-step order and Step 1's contributions), §3.3 item 7 (`EffectiveCrit` — the contrast case where composition *is* authored), §3.4 (the Boss basic-attack Step-1 input), §1.1 (the MVP stat defaults and `ATK` = 50)
- `docs/01-game-design/GAME_RULES.md` §13 (Relic Rules), §14 (Combat Rules), §16 (Battle Event Model), **§17 (the fixed resolution order — step 11 "Trigger Relics"; step 15 the Pet's attack)**, §18 (server authority), §20 (Rule Change Policy)
- `docs/02-technical/GAME_STATE.md` **§2.3** (`PetState` member set and the `ATK` annotation stating the stat holds the stored base value and that the `BuffDebuff` modifier is composed at resolution time via `COMBAT_RULES.md` §5.4), **§2.3.1** (`StatusEffects[]` element schema, its `RemainingTurns` XOR `ExpiryCondition` duration dichotomy, one-instance-per-identity), §2.3.2 (serialization and the round-trip obligation), **§2.3.3** (what the Status Effect model does not add, including the queued/pending/second-representation prohibition), **§2.3.4** (`NextAttackCritModifiers[]` — the direct precedent: a purpose-built collection with a `SourceIdentity` removal key, its own schema, own lifecycle, own round-trip obligation, own "not a wire member" ruling), **§2.3.5** (`CardCostModifiers[]` — the `CardCost`-specific precedent, whose items 1 and 2 fix its separateness from `StatusEffects[]` and its `Pet` targeting), §2.3.6 (its serialization), §2.4/§2.4.1 (`BossState` parity), §3 (Transient Resolution State), **§5.1** (the single post-resolution write-back), §5.1.1 (the step 19a Status Effect pass), **§5.1.2** (the NextAttack modifier lifecycle — the mutation-ownership precedent), **§5.1.3** (the CardCostModifiers lifecycle), **§0 items 4–5** (a member is added by its owning decision; no second representation)
- `docs/02-technical/DATABASE.md` §1 (the `RelicDefinition` structured shape and the Card contract precedent), §5 item 4 (content-defined rows)
- `docs/02-technical/GAME_EVENTS.md` §2 (`RelicTriggered` payload), §3 item 7 (emission sequencing)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.13 (`DamageCalculated`, whose `base` already carries the Step-1 value), §3.2.23 (`RelicTriggered` = `{ type, relicId }`), §3.2.24 (`PowerChanged` and its source values), §3.2.25 (the `effect summary` omission convention), **§4.2/§4.3** (the current `BattleStateUpdated` payload member sets), §4 item 10/§4.1 item 2 (state added is not wire exposure added)
- `docs/02-technical/REDIS_STATE.md` §2 (active battle state serialization), §4 (the single write-back and `Sequence` compare-and-set), **§7 item 13** (the round-trip obligation a new state member inherits), §7 item 15 (the CardCostModifiers precedent)
- `docs/02-technical/TDD.md` §6 (determinism — a serialized member must round-trip deterministically)
- `docs/02-technical/ARCHITECTURE.md` §2.1 (Domain purity), §2.3 (persistence boundaries), §5 (anti-overengineering)
- `docs/03-decisions/README.md` §2 (ADR criteria), §4 (numbering — never reused), §5 (status values), §8 (known open items)
- **`docs/03-decisions/ADR/ADR-018-structured-relic-trigger-condition-effect-contract.md`** — the Relic contract record. Item 5 states the representation decision introduced **no** battle-state member; its TASK-134 amendment (item 12) resolves the runtime carrier for `CardCost` **only** and states in terms that it "defines no stacking, scaling, or interaction behavior for `ATK`, `Power`, `Crit`, or any other `effectType`". **Evidence only, and its CardCost answer must NOT be copied.**
- `docs/03-decisions/ADR/ADR-017-nextattack-crit-modifiers-as-petstate-battle-state.md` — **precedent/evidence only, NOT the answer**: a purpose-built `PetState` modifier collection introduced because no existing representation could carry the concept, including its rejection of the `StatusEffects[]` escape hatch and its `SourceIdentity` removal-key design
- `docs/03-decisions/ADR/ADR-001` (server authority), `ADR-005` (Redis active battle state), `ADR-011` (`PetState` as the combat runtime state), `ADR-012` (Card/Relic ownership vs battle equip)
- `AGENTS.md` §4 (conflict resolution), §7 (invent no rule), §8 (MVP protection), §9 (anti-overengineering), §10 (server authority), §11 (determinism), §12 (domain boundaries — Combat vs Relic), §17 (documentation change), **§18 (architecture change rule)**, §20 (stop conditions), §22
- `tasks/completed/TASK-131-resolve-relic-trigger-and-effect-resolution-contract.md` — the contract this task does not reopen
- `tasks/backlog/TASK-132-migrate-relic-structured-condition-and-effectdefinition-storage.md` — the content-side storage task (DONE); its Completion Evidence records this runtime ATK ambiguity as deliberately unresolved
- `tasks/backlog/TASK-134-resolve-cardcostmodifier-authoritative-state-carrier.md` — the **process** precedent for resolving a missing runtime state carrier. **Do not copy its CardCost answer.**
- `tasks/backlog/TASK-133-implement-server-authoritative-relic-trigger-and-effect-resolution.md` — the task this blocks
- `tasks/completed/TASK-116-resolve-nextattack-crit-modifier-state-and-consumption-contract.md` and `TASK-119-resolve-root-atk-modifier-consumption-contract.md` — the precedent decision-task shape for a modifier state/consumption contract

---

## Decision Question

> **Where and how is a Battle-lifetime Pet ATK percentage modifier, such as
> Berserker Core's +5% ATK, represented and applied at runtime?**

The task must determine the authoritative contract for:

```text
representation
lifetime
target
composition
stacking
refresh/retrigger
effective ATK calculation
removal/expiration
serialization
Redis persistence
SignalR projection
```

None of these is to be implemented.

---

## Required Decision Coverage

The executing agent must obtain explicit answers to all of the following. **An
unanswered item is a blocking stop condition, not an invitation to choose.**
Every item below carries the status `NOT DECIDED` until the Product Owner
answers it.

```text
D1 — State carrier:
     Where does an applied Battle-lifetime Pet ATK modifier live?
     The following candidate categories must be presented NEUTRALLY. No option
     is recommended, ordered by ease, or eliminated by this task:
       A. Existing PetState member
       B. New PetState collection/member
       C. Existing ActiveStatusEffects[]
       D. Existing generic modifier representation
       E. Derived-only representation (no stored modifier)
       F. Another documented representation
     These are examples only. The answer must be justified against
     GAME_STATE.md §2.3. Selection must NOT be made because it is easiest to
     implement, most consistent with the existing implementation, most similar
     to CardCostModifiers[] or NextAttackCritModifiers[], simplest for Redis,
     simplest for SignalR, or simplest for the Damage Pipeline.  NOT DECIDED

D2 — Exact stored shape:
     If D1 selects a stored representation, what is its exact shape? The
     decision must define:
       member name
       single value vs collection
       every field
       field types
       requiredness
       ordering
       source identity
       replacement/removal semantics
     The examples below are ILLUSTRATIONS ONLY — not options to pick between:
       ATKModifiers[]              (collection)
       BattleATKModifier           (single value)
       RelicATKModifiers[]         (collection)
     Do NOT select one. Do NOT assume a field shape beyond the examples given.
     If the decision is derived-only, explicitly state why no stored carrier
     exists.                                                    NOT DECIDED

D3 — Target:
     Confirm: target = Pet, unless the authoritative gameplay decision
     explicitly changes it. This CONFIRMS RELIC_RULES.md §8.3; it is not a
     reopening of TASK-131. Do not infer a different target.     NOT DECIDED

D4 — Lifetime:
     Confirm: lifetime = Battle (RELIC_RULES.md §8.3 item 4). Define the exact
     expiration boundary. At minimum resolve whether the modifier ends at:
       battle end | victory/defeat resolution | new battle initialization |
       other explicitly defined boundary
     Do NOT introduce turn expiry, a turn countdown, or a step-19a
     participation unless explicitly decided.                    NOT DECIDED

D5 — ATK composition:
     Define exactly how the modifier contributes to effective Pet ATK. The
     decision must establish:
       Base Pet ATK
       + Berserker Core modifier
       + other documented ATK modifiers
     or whatever composition the Product Owner explicitly chooses.
     The decision must also define:
       percentage interpretation (increase, decrease, or signed)
       composition operator
       rounding/truncation
       cap, if any
       evaluation timing
     Do NOT assume COMBAT_RULES.md §5.4's `EffectiveATK` formula applies: it is
     scoped by §5.4.5 to a Turn-based `BuffDebuff` instance and its formula is
     reduction-only using the absolute value. Do NOT assume the Boss-side §5.5
     convention applies. Whether §5.4 is extended, a sibling rule is authored,
     or the composition is defined elsewhere must be decided.   NOT DECIDED

D6 — Multiple ATK modifiers:
     Resolve what happens when multiple Battle-lifetime ATK modifiers apply
     simultaneously:
       stack | replace | refresh | compose | other explicit rule
     If additive, define the exact semantics and any cap. Do NOT generalize
     TASK-134's CardCost stacking decision to ATK; ADR-018's amendment states
     in terms that the CardCost composition is `CardCost`-specific and defines
     no interaction behavior for `ATK`. RELIC_RULES.md §2.4 item 6 currently
     defines no stacking behavior for `ATK`, so either an existing
     determination must be cited precisely or the answer must be an explicit
     Product Owner decision.                                     NOT DECIDED

D7 — Same-source retrigger:
     Resolve what happens when the same source applies again while its modifier
     is already active. Berserker Core's Condition is cumulative
     (`MatchCountAtLeast(3)`, RELIC_RULES.md §8.1) and its Trigger re-evaluates
     independently of effect lifetime (§8.4), so this is a real case. The
     candidate semantics must be presented NEUTRALLY and this task selects
     none:
       replace | refresh | stack | no-op | other explicit rule
     Do NOT choose one automatically.                            NOT DECIDED

D8 — Removal / expiration:
     Define when the modifier is removed and what state remains afterward, and
     whether base `PetState.ATK` is ever mutated. Explicitly address:
       PetState.ATK
     and whether it remains the permanent/base stat. Note COMBAT_RULES.md
     §5.4.4's existing non-destructive precedent (never overwritten, no restore
     step, the configured default is an initialization value only and never an
     expiry or reset mechanism) — cite it or decide otherwise explicitly; do
     not silently inherit it.                                    NOT DECIDED

D9 — Serialization:
     If a runtime carrier is selected, define its participation in:
       BattleState JSON
       Redis active state
       round-trip behavior
       deterministic ordering
       empty collection semantics
     (GAME_STATE.md §2.3.2 item 5, §2.3.6; REDIS_STATE.md §7 item 13; TDD.md
     §6.) Do NOT create a new Redis key as part of the decision unless
     explicitly required. Do NOT implement serialization.        NOT DECIDED

D10 — SignalR projection:
     Resolve whether the modifier is:
       not a wire member
       existing BattleStateUpdated projection
       another existing projection
       new network contract
     (GAME_STATE.md §2.3.1's "state added is not wire exposure added"
     convention; SIGNALR_PROTOCOL.md §4.2/§4.3; §3.2.13's `DamageCalculated`
     already carries the Step-1 value.) Do NOT create a new event or method.
     If a new network contract would be required, record that as a separate
     architectural concern and STOP rather than implementing it. NOT DECIDED

D11 — Effective ATK ownership:
     Determine which authoritative document owns:
       Effective Pet ATK composition
     Potential ownership may involve:
       COMBAT_RULES.md | GAME_RULES.md | RELIC_RULES.md | GAME_STATE.md
     Do not duplicate the same calculation across documents
     (documentation/documentation-change.md §2). State whether
     COMBAT_RULES.md §5.4 is extended, whether a sibling section is authored,
     and how §5.4.5's `BuffDebuff`-only scope statement is reconciled with the
     new rule.                                                   NOT DECIDED

D12 — Constraints:
     The decision must explicitly preserve:
       server authority
       no client-authoritative ATK
       no mutation of base ATK for temporary modifiers
       no undocumented pending state
       no second in-flight representation
       no speculative PostgreSQL BattleState persistence
       no undocumented Redis key
       no undocumented SignalR event/method
     Record the verification the Product Owner supplies.         NOT DECIDED
```

**All twelve items below were `NOT DECIDED` and have since been answered by the
Product Owner — see "Decision Record (D1–D12)".** The `NOT DECIDED` marker on
each item records the state in which the decision was requested; the answer for
each is the correspondingly-numbered decision in that section, which is the
recorded decision and the input to the downstream contract-application task. The
markers are retained rather than rewritten so the original request and the
supplied answer remain separately auditable.

---

## Decision Options

Presented for the decision-maker. **This task recommends none of them, orders
them by nothing, and selects none.** Each is listed only to make the decision
space explicit and to show that materially different, documented-consistent
options exist.

```text
D1 — State carrier
  A. an existing PetState member
  B. a new PetState collection/member
  C. the existing ActiveStatusEffects[] (StatusEffects[])
  D. an existing generic modifier representation
  E. a derived-only representation with no stored carrier
  F. another documented representation

D2 — Stored shape (if D1 selects a stored representation)
  a collection of source-identified entries, or a single value; with the field
  set, types, requiredness, ordering, and removal key the Product Owner defines
  — or an explicit statement of why no stored carrier exists

D5/D11 — Composition and its owner
  an extension of COMBAT_RULES.md §5.4 beyond its `BuffDebuff` scope, a sibling
  Pet-side rule, a rule owned by another document, or another explicit choice
  — with the percentage direction, operator, rounding, cap, and evaluation
  timing defined

D6/D7 — Multi-modifier and retrigger semantics
  an explicit rule, or a precise citation of a document that already determines
  the behavior. RELIC_RULES.md §2.4 item 6 currently determines neither.

D10 — Projection
  not a wire member, the existing BattleStateUpdated projection, another
  existing projection, or a newly required network contract (which would be a
  separate architectural concern and a STOP for implementation)
```

---

## Decision Record (D1–D12)

**Recorded verbatim as supplied by the Product Owner.** Each item answers the
correspondingly-numbered item of "Required Decision Coverage". No value below
was derived, inferred, computed, or reinterpreted by this task, and no
assumption was added.

```text
D1   New PetState collection/member
     (supplied as option "2B" — the D1 candidate category "B. New PetState
     collection/member").

D2   PetState.ATKModifiers[]
     Each entry contains exactly:
     - SourceIdentity: string
     - ATKModifierPercentage: number

     The collection is always present.
     Each source has at most one active entry.
     Ordering is deterministic by SourceIdentity.

D3   Target = Pet.

D4   Lifetime = Battle.
     The modifier expires when the current battle ends and is removed with
     the battle's final authoritative state transition. It does not expire on
     Turn, Swap, or non-damaging actions.

D5   ATK modifiers are signed percentage-point contributions.
     Multiple modifiers are additive.

     EffectivePetATK =
     truncate(PetState.ATK × (100 + TotalATKModifierPercentage) / 100)

     TotalATKModifierPercentage is the sum of all applicable Battle-lifetime
     ATK modifiers.

     Clamp the final EffectivePetATK to the documented valid ATK range, if such
     a range already exists. Do not invent a new stat cap.

     The calculation is performed from the permanent base PetState.ATK plus
     active modifiers when ATK is required; PetState.ATK itself is never
     mutated.

D6   Additive — multiple simultaneous Battle-lifetime ATK modifiers are
     summed.

D7   Same source replaces/refreshes its existing modifier; it does not create
     a duplicate. Refresh uses the newly applied value.

D8   A modifier is removed when its source is removed or when the Battle ends.
     Removing a modifier recalculates EffectivePetATK from the unchanged
     PetState.ATK plus the remaining modifiers.

     PetState.ATK remains the permanent/base ATK and is never mutated by the
     temporary modifier system.

D9   ATKModifiers[] is part of authoritative BattleState JSON serialization
     and Redis active-state persistence.

     Serialization must round-trip losslessly.
     Empty collection serializes as [].
     Ordering is deterministic.

D10  ATKModifiers[] is not a new SignalR event or method.
     It is included only through the existing BattleStateUpdated projection
     if the existing projection already exposes the relevant PetState fields.
     No new ATK-specific network contract is introduced.

D11  Effective Pet ATK composition is owned by COMBAT_RULES.md.
     The existing COMBAT_RULES.md §5.4 BuffDebuff rule remains scoped to its
     existing Turn-based BuffDebuff semantics; it is not silently reused for
     Relic Battle-lifetime modifiers.

D12  Preserve:
     - server-authoritative ATK
     - no client-authoritative calculation
     - no mutation of base PetState.ATK
     - no PendingStatusEffects[]
     - no second in-flight representation
     - no PostgreSQL BattleState persistence
     - no new Redis key
     - no undocumented SignalR event/method
```

### Status of the "Required Decision Coverage" items

Every item below was `NOT DECIDED`; each is now answered by the corresponding
decision above. The `NOT DECIDED` markers in "Required Decision Coverage" are
thereby resolved — they are superseded by this record, not restated here.

```text
D1  → ANSWERED by the D1 decision (state carrier selected: new PetState
      collection/member)
D2  → ANSWERED by the D2 decision (exact shape supplied: PetState.ATKModifiers[]
      with SourceIdentity and ATKModifierPercentage, always present, at most one
      active entry per source, deterministic SourceIdentity ordering)
D3  → ANSWERED by the D3 decision (confirmation of RELIC_RULES.md §8.3:
      target = Pet — confirmation only, TASK-131 not reopened)
D4  → ANSWERED by the D4 decision (lifetime = Battle, with the exact expiration
      boundary defined; no Turn/Swap/non-damaging expiry)
D5  → ANSWERED by the D5 decision (signed percentage-point contributions,
      additive, the EffectivePetATK formula, rounding by truncate, the
      existing-range-only clamp instruction, and evaluation timing)
D6  → ANSWERED by the D6 decision (multiple modifiers are additive/summed)
D7  → ANSWERED by the D7 decision (same source replaces/refreshes; refresh uses
      the newly applied value; no duplicate)
D8  → ANSWERED by the D8 decision (removal on source removal or Battle end;
      recalculation from unchanged base; base ATK independence)
D9  → ANSWERED by the D9 decision (BattleState JSON + Redis active-state
      participation, lossless round-trip, empty collection as [], deterministic
      ordering)
D10 → ANSWERED by the D10 decision (no new event or method; existing
      BattleStateUpdated projection only, conditionally; no new ATK-specific
      network contract)
D11 → ANSWERED by the D11 decision (COMBAT_RULES.md owns Effective Pet ATK
      composition; §5.4 remains scoped to its Turn-based BuffDebuff semantics
      and is not silently reused)
D12 → ANSWERED by the D12 decision (the eight constraints preserved)
```

### Boundary confirmation

```text
The decision was recorded. NOTHING was applied to authoritative documentation.

This task did NOT:
  - create PetState.ATKModifiers[]            (source; D1/D2 are the contract,
                                                implementation is downstream)
  - add any member to GAME_STATE.md            (docs/ untouched)
  - define "EffectivePetATK" in COMBAT_RULES.md (docs/ untouched)
  - implement any ATK calculation              (src/ untouched)
  - author or amend any ADR                    (docs/03-decisions/ untouched)
```

### Notes recorded for the downstream contract-application task

Recorded so the decision's consumer does not mistake an open detail for a
settled one. **These are observations about the supplied text, not decisions,
and this task resolves none of them.**

```text
N1  D5's clamp instruction is conditional: "Clamp the final EffectivePetATK to
    the documented valid ATK range, if such a range already exists. Do not
    invent a new stat cap." No ATK valid range was identified at the time of
    recording. COMBAT_RULES.md §1.1 owns the MVP stat defaults and documents an
    explicit 0–100 range for `Power` (GAME_RULES.md §12) and a 0–100
    percentage-point range for `Crit` (§1.1, TASK-116 D-4.4), but no
    corresponding documented valid range for `ATK` was located. The downstream
    task must determine whether such a range exists before applying or omitting
    the clamp; inventing one is prohibited by D5 itself and by AGENTS.md §7.

N2  D5 supplies the formula and `truncate` as the rounding, and D5/D6 supply
    additivity. D5 does not restate whether `ATKModifierPercentage`'s unit is
    percentage points matching RELIC_RULES.md §8.2's `valueType: "Percentage"`;
    D5's "signed percentage-point contributions" states the unit explicitly, so
    the downstream task should carry that wording rather than introduce a second
    magnitude convention.

N3  D8 states removal occurs "when its source is removed or when the Battle
    ends", and D4 states the modifier "is removed with the battle's final
    authoritative state transition". The downstream task must reconcile these
    with the existing single post-resolution write-back (GAME_STATE.md §5.1) and
    battle-end processing, and must record whether RRelic-sourced entries are
    removed only by Battle end in practice, since no Relic source is removable
    mid-battle by any documented mechanic.

N4  D10 is conditional: the collection is included "only through the existing
    BattleStateUpdated projection if the existing projection already exposes the
    relevant PetState fields". SIGNALR_PROTOCOL.md §4.3's `petState` projection
    is the delivery surface; whether it "already exposes the relevant PetState
    fields" for this member is a determination the downstream task must make and
    record, consistent with §4 item 10/§4.1 item 2's "state added is not wire
    exposure added" convention. D10 forbids a new ATK-specific network contract,
    so a "no" determination yields no wire member — not a new event.

N5  D11 assigns ownership of Effective Pet ATK composition to COMBAT_RULES.md
    and states §5.4 "remains scoped to its existing Turn-based BuffDebuff
    semantics; it is not silently reused for Relic Battle-lifetime modifiers".
    The downstream task must therefore author the Relic-sourced composition at
    its canonical owner without restating or widening §5.4, and must reconcile
    §5.4.5's existing `BuffDebuff`-only scope statement with the new rule. The
    relationship between the two rules (whether the Relic modifier composes with
    a simultaneous §5.4 `BuffDebuff` reduction, and in what order) is not stated
    by D5 or D11 and is left for the downstream task to raise if it is not
    otherwise determined.

N6  D2's ordering rule ("deterministic by SourceIdentity") is an ordering
    obligation the downstream task must carry into
    GAME_STATE.md §2.3.2/§2.3.6-style serialization language and REDIS_STATE.md
    §7-style round-trip language, matching the existing sibling collections'
    treatment. D2's "at most one active entry per source" is the D7 refresh
    semantics expressed as a storage invariant.
```

---

## Scope

### In Scope

1. **Obtain the decision.** Present D1–D12 to the Product Owner / human and
   obtain explicit answers. Present every candidate option neutrally.
2. **Record the decision in this task.** Write the supplied answers into
   "Decision Record (D1–D12)" verbatim. Record nothing the Product Owner did not
   supply.
3. **Record the TASK-132 relationship.** State whether the decision affects
   TASK-132's content-side scope or only the runtime side. Do **not** modify
   TASK-132.
4. **Record the TASK-133 relationship.** State that TASK-133 remains blocked
   pending the downstream contract-application step. Do **not** modify TASK-133.
5. **Record the TASK-134 relationship.** Record that TASK-134 is the process
   precedent only and that its CardCost answer was not copied. Do **not** modify
   TASK-134.
6. **Record the handoff.** State explicitly that the recorded decision is input
   to the subsequent contract-application task, and that TASK-136 itself applies
   the decision to no authoritative document.
7. **Record the ADR determination as a decision, not as an act.** Whether an ADR
   amendment or a new ADR is needed is recorded as part of the downstream
   contract-application determination. Authoring that ADR is the downstream
   task's act, not this one. (D1–D12 as supplied do not themselves state an ADR
   treatment; the downstream task determines it per `docs/03-decisions/README.md`
   §2, as TASK-134 D11 did for CardCost.)

### Out of Scope

- **Implementing anything** — no Relic runtime, no TASK-133 execution, no
  structured storage migration, no CardCost runtime, no NextAttack Crit
  implementation. `src/` and `tests/` are untouched by this task.
- **Applying the decision to any authoritative document.** `docs/` is untouched
  by this task — including `GAME_STATE.md`, `COMBAT_RULES.md`, `GAME_RULES.md`,
  `RELIC_RULES.md`, `REDIS_STATE.md`, `SIGNALR_PROTOCOL.md`, `DATABASE.md`,
  `GAME_EVENTS.md`, and `docs/03-decisions/`. That is the subsequent
  contract-application task.
- **Authoring or amending an ADR** — the ADR treatment is the downstream task's
  determination and act
- **Creating any runtime state carrier** — `PetState.ATKModifiers[]` is the
  recorded D1/D2 decision, not something this task creates. No state member,
  collection, or field may be added to a document or implemented by this task.
- **Reopening any TASK-131 decision.** The effect vocabulary, magnitude
  vocabulary, target/scope model, Assassin Eye, Condition grammar, Relic
  lifetime model, Burning Curse disposition, Trigger list, storage contract, and
  `RelicTriggered` wire shape all remain authoritative and unmodified.
- **Changing Berserker Core's declared content values.** Trigger
  (`OnMatchCount`), Condition (`MatchCountAtLeast(3)`), and Effect
  (`ATK`/`Percentage`/5/`Pet`/`Battle`) are fixed by `RELIC_RULES.md` §8.5. This
  task resolves only how the declared effect exists and behaves at runtime.
- **Reusing or extending `CardCostModifiers[]` or `NextAttackCritModifiers[]`**
  — each is specifically scoped to its own concept; neither may carry an ATK
  modifier, and TASK-134's CardCost answer must not be copied to ATK
- **Any new gameplay mechanics** — no new Relic, effect, trigger, condition,
  Status Effect type, stacking system, or combat mechanic
- **Any new Redis infrastructure** — D9 reuses the existing record; no new key;
  no speculative PostgreSQL `BattleState` persistence
- **Any new SignalR event or method** (D10); any new database table
- **Modifying TASK-131, TASK-132, TASK-133, or TASK-134** (explicitly
  prohibited), or any completed or superseded task — `TASK_LIFECYCLE.md` §3
- **Creating any further task** — the downstream contract-application task and
  any implementation task are identified but not created here
- **Reopening TASK-079, TASK-099, or TASK-102** — terminal `SUPERSEDED`
- **TASK-036's Discord credential decisions** — separate, independent blocker
- **Any item listed as OUT in `MVP_SCOPE.md` §2**, or anything not IN
  `MVP_SCOPE.md` §1

---

## Dependencies

```text
TASK-131   DONE    the Relic Trigger/Condition/Effect contract this task does
                   not reopen; it resolves only the missing runtime ATK
                   representation and composition
TASK-132   DONE    the structured content storage carrying the
                   `ATK | Pet | Battle | Percentage` declaration. It stored the
                   declaration and deliberately resolved no runtime; its
                   Completion Evidence records this ambiguity as open.
ADR-017    DONE    precedent/evidence for a purpose-built PetState modifier
                   collection and its source-scoped removal key — NOT the answer
ADR-018    DONE    the Relic contract record. Its TASK-134 amendment resolves
                   the CardCost runtime carrier only, and states that it defines
                   no interaction behavior for ATK — EVIDENCE ONLY, and its
                   CardCost answer must NOT be copied
```

---

## Blocks

```text
TASK-133 — Implement Server-Authoritative Relic Trigger and Effect Resolution

TASK-133 cannot implement Berserker Core's ATK effect until this contract is
resolved and applied to authoritative documentation.

TASK-133 remains BLOCKED pending the downstream contract-application step. This
task must NOT modify TASK-133.
```

---

## TASK-131 Boundary

```text
TASK-131 (DONE) and RELIC_RULES.md §8 / ADR-018 remain AUTHORITATIVE for the
Relic Trigger/Condition/Effect contract.

This task resolves ONLY the missing runtime representation and composition of a
Battle-lifetime Pet ATK modifier. It does NOT reopen TASK-131's decisions on the
effect vocabulary, magnitude vocabulary, target/scope model, Assassin Eye,
Condition grammar, Relic lifetime model, Burning Curse, Trigger list, storage
contract, or `RelicTriggered` wire shape.
```

D3 above **confirms** §8.3's `target`/`lifetime` values as an input to this
decision; confirmation is not re-litigation. If the executing agent finds that
the runtime contract cannot be chosen without changing any TASK-131 decision or
any `RELIC_RULES.md` §8.5 content value, that is a stop condition — not a
mandate to change them.

---

## TASK-132 Relationship

```text
TASK-132 = content-side storage   (DONE)
TASK-136 = runtime decision-input
TASK-133 = runtime implementation
```

TASK-132 migrated `RelicDefinition.Condition` and
`RelicDefinition.EffectDefinition` to the structured representation
(`RELIC_RULES.md` §8.6) and encoded the four provisioned rows. It explicitly did
not implement runtime resolution, and its Completion Evidence records that
"the runtime ATK ambiguity this task was told not to resolve remains
unresolved".

This task concerns the **runtime-side** representation and composition of an
applied ATK effect. These are different layers. TASK-136 must only **record**
whether the decision affects TASK-132. It must NOT modify TASK-132.

```text
Finding recorded (branch (a)):

The D1/D2 decision selects a BattleState runtime carrier
(PetState.ATKModifiers[] with SourceIdentity and ATKModifierPercentage). That is
a runtime-state representation, not a change to the structured CONTENT shape:
the ATK element's declared members remain RELIC_RULES.md §8.2/§8.3's
effectType / valueType / value / target / lifetime, and the decision adds no
member to that element. D5's composition rule likewise operates on runtime
state, not on the stored RelicDefinition content.

TASK-132 therefore remains scoped to the content-side storage migration and
requires no change. Recorded as a finding; TASK-132 was not altered.
```

**TASK-132 must NOT be modified by this task under any circumstance.**

---

## TASK-133 Relationship

TASK-133 (`tasks/backlog/TASK-133-implement-server-authoritative-relic-trigger-and-effect-resolution.md`)
implements `GAME_RULES.md` §17 step 11 and includes Berserker Core among its
deliverables.

**TASK-133 cannot implement Berserker Core ATK until this contract is
resolved.** TASK-133 must not be modified by this task.

TASK-133 remains BLOCKED for the Berserker Core / `ATK` path. TASK-136 supplied
the decision; it did not apply it to the authoritative documents, so TASK-133 is
not yet implementation-ready. Making it implementation-ready requires the
downstream contract-application step, which consumes this record and updates the
canonical owner documents.

---

## TASK-134 Relationship

TASK-134 resolved `CardCostModifiers[]` for the Emergency Core `CardCost` effect
and is used here **only as a process precedent** for how this repository
resolves a missing runtime state carrier: present the ambiguity, obtain an
explicit Product Owner decision, record it, and apply nothing.

```text
TASK-134's CardCost answer was NOT copied.

CardCostModifiers[]          was NOT reused for ATK.
The CardCost rule (additive percentage reduction, cap at 100%, truncate toward
zero) was NOT copied to ATK. The D5/D6 ATK semantics (signed percentage-point
contributions, additive, `truncate(PetState.ATK × (100 +
TotalATKModifierPercentage) / 100)`, no invented cap) are the Product Owner's
ATK-specific decision and differ from the CardCost rule in direction, in
formula, and in the cap question.

ADR-018's amendment states in terms that the CardCost composition "defines no
stacking, scaling, or interaction behavior for `ATK`, `Power`, `Crit`, or any
other `effectType`" — so the ATK decision required its own answer, which D5/D6
now supply.

TASK-134 was NOT modified by this task.
```

The structural resemblance between the two answers (`SourceIdentity`-keyed
`PetState` collection, always present, deterministic ordering, no new wire
member, no new Redis key) is a consequence of both decisions resolving the same
class of gap — it is not a reuse of the CardCost answer, and `ATK` semantics
were not inherited from it.

---

## Related But Distinct: `NextAttackCritModifiers[]`

```text
The existing NextAttackCritModifiers[] contract is specifically for NextAttack
Crit modifiers (GAME_STATE.md §2.3.4; ADR-017).

It was NOT reused for Battle-lifetime ATK.
ATK was NOT added to that collection.
```

---

## Implementation Notes

- **The decision was supplied in full.** All twelve items were answered by the
  Product Owner as D1–D12, so the prohibition below did not bind. Recorded for
  the historical record of what this task was.
- **Invent nothing remains the rule that was observed.** The decision was
  transcribed verbatim into the Decision Record; no carrier, shape, field,
  composition, rounding, cap, stacking, refresh, or expiration semantic was
  derived, inferred, or computed by this task.
- **Implementation convenience did not determine the decision.** D1 selected
  option B (new PetState collection/member) with the Product Owner's own
  justification path; the task presented all six categories neutrally and
  selected none.
- **`COMBAT_RULES.md` §5.4 was not silently applied.** D11 explicitly keeps
  §5.4 scoped to its existing Turn-based `BuffDebuff` semantics and states it is
  not silently reused for Relic Battle-lifetime modifiers. D5 supplies the
  Relic-sourced composition instead, as a signed additive percentage-point rule.
- **`RELIC_RULES.md` §2.4 item 6 was honored.** It states the section defines no
  stacking, scaling, or interaction behavior for `ATK`. D6 supplies that
  behavior explicitly as a Product Owner decision, which is the "future rule
  change (`GAME_RULES.md` §20)" item 6 anticipated — not an inference from it.
- **`ADR-017` and `ADR-018` are precedent/evidence only.** The structural
  resemblance of the recorded answer to the sibling collections is not a
  citation of their authority, and their Crit- and CardCost-specific semantics
  were not inherited.
- **Do NOT apply the answer to any document.** The canonical owner documents
  (`GAME_STATE.md` for the member set, `COMBAT_RULES.md` for the composition per
  D11, `RELIC_RULES.md` for a cross-reference only, `REDIS_STATE.md` for the
  round-trip statement, and the ADR set) are updated by the **subsequent**
  contract-application task — not here.
- **No gameplay expansion.** This task records a runtime-contract decision. It
  introduces no new Relic, Trigger, Condition, `effectType`, combat mechanic,
  Status Effect type, stacking system, Redis key, SignalR event, or table.
- **Do not modify TASK-131, TASK-132, TASK-133, or TASK-134.** All four are
  explicitly protected and must be left byte-identical.
- **Do not create another task.** The downstream contract-application task and
  any implementation task are identified and handed off, not created here.
- **This task is NOT a broad audit.** Its scope is exactly the Battle-lifetime
  Pet ATK modifier runtime contract. Do not sweep other Relic effects, other
  modifiers, or unrelated gameplay.
- Do not modify `tasks/completed/`.

---

## Acceptance Criteria

- [x] The documented Battle-lifetime Pet ATK runtime gap is clearly stated.
- [x] D1–D12 are presented as explicit Product Owner/human decisions.
- [x] D1's candidate categories are presented neutrally with no preselection.
- [x] No D1–D12 option is selected by the executing agent.
- [x] The task explicitly states that implementation convenience must not
      determine the decision.
- [x] The task explicitly states that `COMBAT_RULES.md` §5.4's `EffectiveATK`
      formula is `BuffDebuff`-scoped per §5.4.5 and must not be assumed to apply.
- [x] The task explicitly states that `CardCostModifiers[]` must not be reused
      for ATK and that TASK-134's CardCost answer must not be copied.
- [x] The task explicitly states that `NextAttackCritModifiers[]` must not be
      reused for ATK and that ATK must not be added to that collection.
- [x] The task explicitly states that Berserker Core's declared content values
      (trigger, condition, effect) must not be changed.
- [x] The task explicitly records the decision as input for the downstream
      contract-application task.
- [x] The task explicitly states that TASK-133 cannot implement Berserker Core
      ATK until this contract is resolved.
- [x] TASK-136 does not modify authoritative documentation.
- [x] TASK-136 does not modify source code.
- [x] TASK-136 does not modify tests.
- [x] TASK-131 remains unchanged.
- [x] TASK-132 remains unchanged.
- [x] TASK-133 remains unchanged.
- [x] TASK-134 remains unchanged.
- [x] No new gameplay rule is invented.
- [x] No runtime state carrier is created by this task.
- [x] No new state member is invented by the executing agent.
- [x] No new state shape is invented by the executing agent.
- [x] No stacking rule is invented by the executing agent.
- [x] No composition rule is invented by the executing agent.
- [x] No expiration rule is invented by the executing agent.
- [x] No new SignalR event or method is invented.
- [x] No Redis behavior or key is invented.
- [x] No database migration is created.
- [x] The final recorded decision is complete enough for the downstream
      contract-application task to consume.
- [x] The TASK-132 relationship is recorded as "runtime only"
      (TASK-132 unaffected) — with TASK-132 left unmodified.
- [x] The TASK-133 blocking relationship is stated, and TASK-133 is left
      unmodified.
- [x] Zero files under `src/` or `tests/` are modified.
- [x] Zero gameplay values, formulas, or balance figures are invented.
- [x] Zero files under `docs/` are modified.
- [x] TASK-131, TASK-132, TASK-133, TASK-134, and all completed/superseded tasks
      are byte-identical.
- [x] All relevant tests pass at the required validation depth (`core/validation.md` §2)
      — N/A: this task implements no behavior; boundary audits recorded below.
- [x] Quality review checklist passes (`quality/review.md` §1)
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[ ] src/backend/ (none — decision-input task; no code)
[ ] src/frontend/client/ (none)
[ ] tests/ (none)
[ ] docs/01-game-design/ (none — MUST NOT be modified by TASK-136)
[ ] docs/02-technical/ (none — MUST NOT be modified by TASK-136)
[ ] docs/03-decisions/ (none — MUST NOT be modified by TASK-136; the ADR
                          treatment is the downstream task's determination and
                          act)
[x] tasks/backlog/TASK-136-resolve-battle-lifetime-pet-atk-modifier-runtime-contract.md
        (this file — the only file TASK-136 may modify)
[ ] tasks/backlog/TASK-132, tasks/backlog/TASK-133, tasks/backlog/TASK-134
        (MUST remain unmodified)
[ ] tasks/completed/ (MUST remain unmodified)
```

---

## Testing / Evidence Requirements

### Required Verification

This is a decision-input task. Implementation and documentation tests are
replaced by audit checks.

```text
[x] Decision coverage audit
    Confirm D1–D12 are explicitly answered. — PASS (all twelve supplied)

[x] Neutrality audit
    Confirm no option is recommended or selected by the task. — PASS (no option
    chosen, inferred, defaulted, or recommended by the executing agent; every
    value is the Product Owner's)

[x] Boundary audit
    Confirm TASK-136 is decision-input only. — PASS

[x] Protected-file audit
    Confirm TASK-131, TASK-132, TASK-133, and TASK-134 remain untouched.
    — PASS (hashes unchanged)

[x] Source/test audit
    Confirm zero source and test files are modified. — PASS (zero files)

[x] Authoritative-doc audit
    Confirm zero docs/ files are modified by TASK-136. — PASS (zero files)

[ ] Gameplay implementation tests
    N/A — no code is implemented.

[ ] Integration tests
    N/A — no runtime behavior is implemented.
```

Do not claim documentation consistency across a decision that has not yet been
applied to its canonical owner documents.

### Key Edge Cases (for the downstream contract-application task)

Recorded here so the decision's consumer inherits them; they are **not** checks
this task performs against an implementation.

- The recorded representation must express Berserker Core's `+5%`
  `Percentage` value, as an **increase**, without a second magnitude convention
  and without being inverted by `COMBAT_RULES.md` §5.4's reduction-only formula
  (D5's signed `+ TotalATKModifierPercentage` form and D11's explicit
  non-reuse of §5.4 address this; the downstream task must author it without
  restating or widening §5.4)
- The recorded representation must handle the "already active, triggers again"
  case that Berserker Core's cumulative `MatchCountAtLeast(3)` Condition and
  `RELIC_RULES.md` §8.4's lifetime independence make real — D7's
  replace/refresh-with-newly-applied-value semantics apply, and note N2/N6
  above records what the answer does and does not fix
- The `Battle`-lifetime end must be reconciled with `GAME_RULES.md` §17's
  resolution order and with battle-end processing (see note N3)
- A collection is selected, so its ordering must be deterministic and its
  removal key must be deterministic and reproducible (`TDD.md` §6) — D2 fixes
  ordering by `SourceIdentity` and at most one active entry per source,
  matching `GAME_STATE.md` §2.3.4 item 2's rejection of GUIDs, timestamps, and
  array positions as keys
- The round-trip obligation of `GAME_STATE.md` §2.3.2 item 5 / §2.3.6 and
  `REDIS_STATE.md` §7 item 13 must hold; D9 requires lossless round-trip, `[]`
  for the empty collection, and deterministic ordering
- D10 is conditional on the existing projection already exposing the relevant
  `PetState` fields — see note N4; a "no" determination yields no wire member,
  never a new event
- D5's clamp is conditional on a documented ATK range existing — see note N1;
  inventing a cap is prohibited
- The decision must not create a second representation of any value
  `GAME_STATE.md` §0 item 5 already covers, and must not mutate base
  `PetState.ATK` (D5/D8 both address this)

---

## Stop Conditions

- **If an authoritative document already defines the carrier: STOP and report the
  evidence** rather than creating a duplicate decision. (Verified at creation
  time: no such document exists — `COMBAT_RULES.md` §5.4 is `BuffDebuff`-scoped
  by §5.4.5, `RELIC_RULES.md` §2.4 item 6 states `ATK` composition is undefined,
  `ADR-018` states it defines no `ATK` interaction behavior, and no
  `ATKModifiers[]`, `BattleATKModifier`, `RelicATKModifiers[]`, or
  `EffectivePetATK` appears anywhere in `docs/`.)
- **If the required Product-Owner decision is not supplied, including D1 (the
  carrier): STOP per `AGENTS.md` §7.** Do not choose a state carrier, a field
  shape, a composition rule, a stacking rule, an expiration rule, or a
  projection treatment. (All twelve items were supplied; this condition did not
  fire.)
- **If ATK composition remains ambiguous after the decision: STOP.**
- **If multiple ATK modifier semantics remain ambiguous after D6: STOP.**
- **If lifetime/removal remains ambiguous after D4/D8: STOP.**
- **If serialization semantics remain ambiguous after D9: STOP.**
- **If SignalR ownership remains ambiguous after D10: STOP.**
- **If resolving the question requires a new gameplay rule outside this decision
  boundary: STOP** and report it — do not author it.
- **If resolving it requires a new architectural decision that cannot be captured
  as a Product Owner gameplay decision: STOP** and report it.
- **If applying the decision to an authoritative document appears necessary to
  complete this task: STOP after recording the decision.** Do not perform that
  documentation update in TASK-136.
- **If the decision would require changing the TASK-131 structured content
  contract: STOP.** Do not alter TASK-132 to compensate.
- If existing authoritative documents already contain a **conflicting** Pet ATK
  modifier model: STOP and report the conflict per `AGENTS.md` §4 — do not
  silently pick a side
- If **Effective Pet ATK composition ownership is ambiguous** — i.e. no document
  can be identified as owning it: STOP and report which document should own it
  rather than assigning ownership. (D11 assigns it to `COMBAT_RULES.md`.)
- If a **new network contract is required** (D10): record it as a separate
  architectural concern and STOP — do not implement it
- If a **new Redis key** would be required: STOP — record it as a separate
  concern. (D9 reuses the existing record and introduces no key.)
- If **PostgreSQL `BattleState` persistence** would be required: STOP —
  `ADR-005`/`REDIS_STATE.md` keep active battle state in Redis. (D12 forbids it.)
- If the decision would require **modifying a completed task** (TASK-131,
  TASK-132, ADR-017, ADR-018's decided content): STOP — completed tasks are
  immutable (`TASK_LIFECYCLE.md` §3)
- If the decision would require **modifying TASK-132, TASK-133, or TASK-134**:
  STOP — all are protected by this task's instructions; record the dependency
  instead
- If **implementation becomes necessary to determine the contract**: STOP — that
  inverts the required order (`AGENTS.md` §5, §18)
- If the decision would place ATK or Relic-effect computation on the client:
  STOP per `AGENTS.md` §10. (D12 preserves server-authoritative ATK.)
- If the task drifts into implementing the state member, the ATK calculation, the
  Relic resolver, or applying the decision to documentation: STOP — those are
  separate tasks
- If the task is asked to create another task: STOP — the downstream work is
  identified and handed off, not created here
- If the task is asked to reopen TASK-079, TASK-099, or TASK-102: STOP —
  terminal `SUPERSEDED`
- If the task is asked to resolve TASK-036's Discord credential decisions: STOP —
  separate, independent blocker
- If task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: STOP & decompose

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after the Product Owner decision is
  supplied and recorded.
  Keep concise and factual.
  DO NOT fill the decision fields with invented answers.
-->

### Decision Recorded

The Product Owner supplied an explicit answer for all twelve decision items.
Recorded verbatim in "Decision Record (D1–D12)" above. No value was derived,
inferred, computed, or reinterpreted, and no assumption was added.

Product Owner decision:

```text
D1:  New PetState collection/member (option B)

D2:  PetState.ATKModifiers[]
     Each entry contains exactly:
     - SourceIdentity: string
     - ATKModifierPercentage: number

     The collection is always present.
     Each source has at most one active entry.
     Ordering is deterministic by SourceIdentity.

D3:  Target = Pet.

D4:  Lifetime = Battle.
     The modifier expires when the current battle ends and is removed with
     the battle's final authoritative state transition. It does not expire on
     Turn, Swap, or non-damaging actions.

D5:  ATK modifiers are signed percentage-point contributions.
     Multiple modifiers are additive.

     EffectivePetATK =
     truncate(PetState.ATK × (100 + TotalATKModifierPercentage) / 100)

     TotalATKModifierPercentage is the sum of all applicable Battle-lifetime
     ATK modifiers.

     Clamp the final EffectivePetATK to the documented valid ATK range, if
     such a range already exists. Do not invent a new stat cap.

     The calculation is performed from the permanent base PetState.ATK plus
     active modifiers when ATK is required; PetState.ATK itself is never
     mutated.

D6:  Additive — multiple simultaneous Battle-lifetime ATK modifiers are
     summed.

D7:  Same source replaces/refreshes its existing modifier; it does not create
     a duplicate. Refresh uses the newly applied value.

D8:  A modifier is removed when its source is removed or when the Battle ends.
     Removing a modifier recalculates EffectivePetATK from the unchanged
     PetState.ATK plus the remaining modifiers.

     PetState.ATK remains the permanent/base ATK and is never mutated by the
     temporary modifier system.

D9:  ATKModifiers[] is part of authoritative BattleState JSON serialization
     and Redis active-state persistence.

     Serialization must round-trip losslessly.
     Empty collection serializes as [].
     Ordering is deterministic.

D10: ATKModifiers[] is not a new SignalR event or method.
     It is included only through the existing BattleStateUpdated projection
     if the existing projection already exposes the relevant PetState fields.
     No new ATK-specific network contract is introduced.

D11: Effective Pet ATK composition is owned by COMBAT_RULES.md.
     The existing COMBAT_RULES.md §5.4 BuffDebuff rule remains scoped to its
     existing Turn-based BuffDebuff semantics; it is not silently reused for
     Relic Battle-lifetime modifiers.

D12: Preserve:
     - server-authoritative ATK
     - no client-authoritative calculation
     - no mutation of base PetState.ATK
     - no PendingStatusEffects[]
     - no second in-flight representation
     - no PostgreSQL BattleState persistence
     - no new Redis key
     - no undocumented SignalR event/method
```

**Decision completeness:** all twelve items were answered, so no stop condition
fired and the task reaches `DONE`. D10 records "no new ATK-specific network
contract" and D11 records `COMBAT_RULES.md` ownership as decisions; neither
documentation act was performed by this task.

### Documentation

No authoritative documentation was modified by TASK-136.

Verified: zero files under `docs/` were modified by this execution. The `docs/`
entries visible in `git status` (`RELIC_RULES.md`, `DATABASE.md`,
`SIGNALR_PROTOCOL.md`, `GAME_STATE.md`, `docs/03-decisions/README.md`,
`ADR-018`, and others) are the pre-existing TASK-131/TASK-132 reconciliation set
and predate this execution.

### Handoff

The recorded decision is input to the subsequent contract-application task.

TASK-136 does not itself apply the decision to `GAME_STATE.md`,
`COMBAT_RULES.md`, `RELIC_RULES.md`, `REDIS_STATE.md`, `SIGNALR_PROTOCOL.md`,
or the ADR set.

```text
What the downstream task consumes from this record:

  D1, D2   the state carrier and its exact shape → GAME_STATE.md §2.3
           (the canonical owner of the PetState member set), following the
           §2.3.4/§2.3.5 sibling-collection precedent for the schema subsection,
           the lifecycle subsection, and the serialization subsection
  D3       target = Pet (confirmation of RELIC_RULES.md §8.3)
  D4       the Battle-lifetime expiration boundary
  D5, D6, D7   the EffectivePetATK composition, its rounding, the additive
           multi-source semantics, and the same-source replace/refresh
           semantics → COMBAT_RULES.md (D11's assigned owner), with
           RELIC_RULES.md §2.4 item 6 receiving the interaction-behavior
           determination it anticipated and RELIC_RULES.md §8 receiving a
           cross-reference only
  D8       removal/expiration and the base-ATK-independence statement
  D9       serialization and round-trip treatment → GAME_STATE.md §2.3.x and
           REDIS_STATE.md §7 (D9 introduces no new Redis key)
  D10      the projection determination → SIGNALR_PROTOCOL.md §4.2/§4.3
           (D10 forbids a new event or method; see note N4)
  D11      the Effective Pet ATK composition ownership → COMBAT_RULES.md, with
           §5.4 left scoped to its Turn-based BuffDebuff semantics
  D12      the eight preserved constraints
  N1–N6    the open details recorded above, which the downstream task must
           determine and record rather than assume

The ADR treatment is the downstream task's determination per
docs/03-decisions/README.md §2 — D1–D12 do not state one.
```

This handoff is recorded, not performed. The downstream task is **not** created
by TASK-136.

### TASK-132

TASK-132 was not modified. Verified: its content hash is unchanged
(`59f988d3…`) across this execution.

Relationship:

```text
runtime-only

The D1/D2 decision selects a BattleState runtime carrier
(PetState.ATKModifiers[] with SourceIdentity and ATKModifierPercentage). That is
a runtime-state representation, not a change to the structured CONTENT shape:
the ATK element's declared members remain RELIC_RULES.md §8.2/§8.3's
effectType / valueType / value / target / lifetime, and the decision adds no
member to that element. D5's composition rule likewise operates on runtime
state, not on the stored RelicDefinition content.

TASK-132 therefore remains scoped to the content-side storage migration and
requires no change. Recorded as a finding; TASK-132 was not altered.
```

This is branch (a) of this task's "TASK-132 Relationship": the decision affects
only BattleState runtime storage, so TASK-132's scope is unaffected. No
TASK-131 structured content-shape change is implied, so no stop condition fired
on this point.

### TASK-133

TASK-133 was not modified. Verified: its content hash is unchanged
(`810aec5a…`) across this execution.

Status:

```text
BLOCKED pending authoritative runtime ATK contract application.
```

TASK-133 remains blocked for the Berserker Core / `ATK` path. TASK-136 supplied
the decision; it did not apply it to the authoritative documents, so TASK-133 is
not yet implementation-ready. Making it implementation-ready requires the
downstream contract-application step, which consumes this record and updates the
canonical owner documents.

### TASK-134

TASK-134 was not modified. Verified: its content hash is unchanged
(`6e894efe…`) across this execution.

```text
Process precedent only. Its CardCost answer was NOT copied, CardCostModifiers[]
was NOT reused for ATK, and the CardCost composition rule (additive percentage
reduction, cap at 100%, truncate toward zero) was NOT copied to ATK.
```

### Changed Files

- `tasks/backlog/TASK-136-resolve-battle-lifetime-pet-atk-modifier-runtime-contract.md`
  — the only file TASK-136 modified. Change: the D1–D12 decision recorded
  verbatim in "Decision Record (D1–D12)", the twelve "Required Decision
  Coverage" items resolved against it, six notes (N1–N6) recording open details
  for the downstream task, `Status` set to `DONE`, and this Completion Evidence
  section completed.

### Validation Results

Decision/task-boundary validation only. No implementation tests were run — this
task implements no behavior.

```text
[x] D1–D12 explicitly answered           — PASS (all twelve supplied by the
                                                Product Owner and recorded)
[x] No executing-agent option selection  — PASS (no option chosen, inferred,
                                                defaulted, or recommended by the
                                                agent; every value is the
                                                Product Owner's)
[x] Decision recorded faithfully         — PASS (verbatim; no reinterpretation,
                                                no added assumption)
[x] TASK-131 unchanged                   — PASS (hash 5bd627b7…)
[x] TASK-132 unchanged                   — PASS (hash 59f988d3…)
[x] TASK-133 unchanged                   — PASS (hash 810aec5a…)
[x] TASK-134 unchanged                   — PASS (hash 6e894efe…)
[x] No docs/ modified                    — PASS (zero files)
[x] No src/ modified                     — PASS (zero files)
[x] No tests modified                    — PASS (zero files)
[x] No ADR created/modified              — PASS (no ADR act performed; the ADR
                                                treatment is the downstream
                                                task's determination)
[x] No runtime state carrier created     — PASS (PetState.ATKModifiers[] is the
                                                Product Owner's decision; no
                                                member was added to any file)
[x] No new gameplay rule invented        — PASS
[x] No stacking rule invented by the     — PASS (D6 is the Product Owner's
    agent                                    decision)
[x] No composition/rounding/cap          — PASS (D5 is the Product Owner's
    invented by the agent                    decision; the clamp remains
                                                conditional per note N1)
[x] No expiration behavior invented by   — PASS (D4/D8 are the Product Owner's
    the agent                                decisions)
[x] No SignalR behavior invented by the  — PASS (D10 reconfirms no new event or
    agent                                    method, conditionally per note N4)
[x] No Redis behavior invented by the    — PASS (D9 reuses the existing
    agent                                    BattleState record; no new key)
[x] CardCostModifiers[] not reused       — PASS (not reused for ATK)
[x] NextAttackCritModifiers[] not        — PASS (not reused for ATK)
    reused
[x] TASK-134's CardCost answer not       — PASS (ATK semantics are the Product
    copied                                   Owner's D5/D6/D7 decision and
                                                differ from the CardCost rule)
[x] No new task created                  — PASS
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed zero files modified under `src/` or `tests/`
- [x] Confirmed zero files modified under `docs/`
- [x] Confirmed TASK-131, TASK-132, TASK-133, TASK-134, and
      `tasks/completed/` unmodified
- [x] Confirmed no runtime state carrier was created by this task
- [x] Confirmed no new Relic, Trigger, Condition, `effectType`, Status Effect
      type, stacking system, Redis key, SignalR event, or table was introduced
- [x] Confirmed `CardCostModifiers[]` and `NextAttackCritModifiers[]` were not
      reused for ATK
- [x] Confirmed D12's eight constraints are recorded as preserved

### Follow-Up Work Identified (not created)

- **Subsequent contract-application task** — consumes this record and applies the
  D1–D12 decision to the canonical owner documents (including notes N1–N6 and
  the ADR determination). This is the step that makes TASK-133
  implementation-ready. **Not** created by TASK-136.
- **TASK-133 unblocking** — follows the downstream contract-application step;
  TASK-133 itself is not modified here and remains blocked until then.
