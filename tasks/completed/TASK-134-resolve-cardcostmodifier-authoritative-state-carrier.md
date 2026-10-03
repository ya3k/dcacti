# TASK-134 — Resolve the Authoritative Runtime State Carrier for the Relic `CardCost` Modifier

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
    Record the decision in TASK-134
            ↓
    STOP

  THIS TASK MUST NOT APPLY THE DECISION TO AUTHORITATIVE DOCUMENTATION.
  The subsequent documentation/contract task consumes the recorded decision
  and updates the canonical documents. TASK-134's deliverable is the RECORD,
  not the documentation change.

  THIS TASK DECIDES NOTHING BY ITSELF AND IMPLEMENTS NO BEHAVIOR. It presents
  the documented evidence of a genuine state-carrier gap and requires the
  appropriate human/Product-Owner decision. Inventing a state member, a shape,
  a field set, a composition rule, or an expiration rule is the single
  prohibited action of this task (AGENTS.md §7, §20).

  DECISION OWNERSHIP: D1–D11 are NOT implementation decisions for the
  executing agent. No option may be selected because it is easier to
  implement, consistent with an existing implementation, similar to
  `NextAttackCritModifiers[]`, simpler for Redis, simpler for SignalR, simpler
  for `CardCastExecutor`, or architecturally convenient. `ADR-017` is
  precedent/evidence only — it is NOT the answer.

  PROVENANCE: identified during TASK-131's execution and recorded there as a
  discovered ambiguity (TASK-131 Completion Evidence, follow-up section).
  TASK-131 resolved the Relic Trigger/Condition/Effect REPRESENTATION contract
  and authored `CardCost` as an allowed effect combination
  (RELIC_RULES.md §8.3: `CardCost` | `Pet` | `Battle` | `Percentage`), but no
  document defines where an APPLIED, battle-scoped Card-cost modifier LIVES in
  authoritative battle state, nor which calculation reads it. TASK-131
  deliberately did not invent one.

  THIS IS A STOP-DRIVEN TASK, NOT A FEATURE. Per the task-generation stop
  conditions, when "a gameplay rule must be invented" or "the state contract is
  undefined" the correct output is a decision task, not an implementation task.
  TASK-133 remains BLOCKED from implementing Emergency Core until this
  resolves; TASK-133 is NOT modified by this task.

  BOUNDARY: decision recording only. Zero files under docs/, src/, or tests/.
  This task creates no ADR, changes no gameplay rule, adds no Relic or Card
  behavior, and does not touch TASK-131, TASK-132, or TASK-133.
-->

---

## Metadata

```text
Task ID:           TASK-134
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md). This task is the
                   DECISION-INPUT half of that workflow only: it obtains and
                   records the Product Owner decision. It does NOT perform the
                   canonical-owner documentation write that the workflow's
                   later steps describe — that is the subsequent
                   contract/documentation task's act, consuming this record.
Status:            DONE (the Product Owner supplied an explicit answer for all
                   eleven decision items D1–D11. The decision was recorded
                   verbatim in "Decision Recorded" without reinterpretation or
                   added assumption, and the eleven "Required Decision
                   Coverage" items were resolved accordingly. TASK-134 modified
                   no file other than this one: zero `docs/`, zero `src/`, zero
                   `tests/`, no ADR, and TASK-131/132/133 are byte-identical.
                   The canonical documentation write and the ADR-018 amendment
                   are the SUBSEQUENT contract/documentation task's act,
                   consuming this record — TASK-134 does not perform them.)
                   Lifecycle reconciliation: recorded DONE while the file remained
                   in tasks/backlog/; moved to tasks/completed/ per
                   TASK_LIFECYCLE.md §3 (direct execution verified: D1–D11
                   recorded, all twenty-seven acceptance criteria satisfied).
Risk:              MEDIUM (TASK_TYPES.md §4 — DOCUMENTATION baseline LOW–MEDIUM;
                   MEDIUM because the decision it records will govern
                   authoritative battle state, its Redis serialization, and
                   potentially the SignalR projection — the same class of
                   state-model change ADR-017 required an ADR for. No `docs/`
                   file is modified by this task.)
Priority:          HIGH (the sole blocker on TASK-133's Emergency Core
                   implementation — the last unimplemented documented MVP Relic
                   effect, and a dependency of GAME_RULES.md §17 step 11.)
Primary Agent:     review (TASK_TYPES.md §2 / AGENT_SELECTION.md §1 —
                   "Documentation change: Primary Agent Review". No domain
                   agent may author the answer; the Product Owner supplies it.)
Supporting Agents: gameplay (CARD_RULES.md §3 owns Card casting and cost, and
                   RELIC_RULES.md §8 owns the Relic effect contract — consulted
                   to CONFIRM the semantics the carrier must express, not to
                   author the answer),
                   backend (GAME_STATE.md §2.3 owns the PetState member set;
                   the Domain PetState/CardCastExecutor are consulted to
                   confirm the existing representation),
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
Dependencies:      TASK-131 (DONE — the Relic contract; this task resolves only
                   its missing runtime state carrier),
                   ADR-017 (DONE — the precedent for a purpose-built PetState
                   modifier collection and its removal-key design; EVIDENCE
                   ONLY, not the answer),
                   ADR-018 (DONE — records that the Relic decision spans
                   runtime/content/storage; this task resolves the runtime part
                   it left open)
Blocks:            TASK-133 — Implement Server-Authoritative Relic Trigger and
                   Effect Resolution (its Emergency Core / `CardCost` path
                   cannot be implemented until this is resolved)
Estimate:          Simple (one recorded state-carrier decision, D1–D11; zero
                   code, zero documentation edits, zero gameplay rules)
```

---

## Problem Statement

TASK-131 resolved the Relic Trigger/Condition/Effect contract. `RELIC_RULES.md`
§8.3 fixes `CardCost` as a defined effect combination:

```text
| `effectType` | `target` | `lifetime` | `valueType` |
| `CardCost`   | `Pet`    | `Battle`   | `Percentage` |
```

and §8.5 transcribes Emergency Core as:

```text
Trigger:    OnHpBelow
Condition:  HpPercentageBelow(30)
Effect:     { "effectType": "CardCost", "valueType": "Percentage",
              "value": 50, "target": "Pet", "lifetime": "Battle" }
```

That is a complete **declaration**. What no document defines is where an
**applied** battle-scoped Card-cost modifier lives in authoritative battle
state, and which calculation reads it.

### The documented gap (preserved verbatim in substance — do not solve it here)

```text
RELIC_RULES.md §8.3
    CardCost | Pet | Battle | Percentage
              ↓
Emergency Core
              ↓
-50% Heal Card cost

BUT

PetState has no CardCost modifier carrier
CardCastExecutor reads CardDefinition.PowerCost directly
No authoritative calculation path consumes a CardCost modifier
```

### Verified state of the representation

| Question | Answer today |
|---|---|
| Is `CardCost` a defined `effectType`? | **Yes** — `RELIC_RULES.md` §8.2 item 1, §8.3 |
| Is Emergency Core's value authored? | **Yes** — `RELIC_RULES.md` §8.5 (from §6: "Heal Card cost −50%") |
| Does `PetState` carry a Card-cost modifier? | **No** — see the member list below |
| Does any other state type carry one? | **No** |
| Which calculation reads a cost modifier? | **None** — `CardCastExecutor` reads `cardDefinition.PowerCost` directly |

`GAME_STATE.md` §2.3's `PetState` member set, as implemented in
`src/backend/GameServer.Domain/Battle/PetState.cs`, is:

```text
PetId, HP, MaxHP, ATK, DEF, Crit, Power, Element,
PassiveId, PassiveProgress, PassiveResetOverride?,
EquippedRelics[], EquippedCards[],
ActiveStatusEffects[], NextAttackCritModifiers[]
```

None of these is a Card-cost modifier. `NextAttackCritModifiers[]` is
Crit-specific by construction (`GAME_STATE.md` §2.3.4: its only payload member
is `CritContribution`, and §2.3.4 item 1 scopes it to Crit), and
`StatusEffects[]` is governed by `COMBAT_RULES.md` §5.1's closed MVP type list
(Burn, Shield, Buff/Debuff) plus Stun (`GAME_STATE.md` §2.4.5) — §2.3.3
explicitly states that introducing another Status Effect type "is a gameplay
decision owned by `COMBAT_RULES.md`".

The Card cost consumption site confirms no modifier path exists:

```text
src/backend/GameServer.Domain/Cards/CardCastExecutor.cs
  L52  if (state.PetState.Power < cardDefinition.PowerCost)   → validate
  L58  var newPower = state.PetState.Power - cardDefinition.PowerCost;
  L79  BattleEvent.CreateCardCast(cardDefinition.CardDefinitionId,
                                  cardDefinition.PowerCost);   → report
```

All three read the **definition's** `PowerCost` directly. `CARD_RULES.md` §3
item 2 states the validation as "`current Power ≥ Card Cost`" and item 4's
sequence as "Deduct Cost from Power" — but neither document defines "Card Cost"
as a composed value that a Relic modifier participates in. `CARD_RULES.md` §5
("Cards vs. Relics") does not address cost modification either.

### Why this is a genuine gap and not an implementation detail

Three separate things are undefined, and each is a decision rather than a
detail:

1. **Where the applied modifier lives.** Adding a `PetState` member is a
   battle-state-model change — the exact class of change `AGENTS.md` §18 lists
   as requiring an ADR, and the class `ADR-017` was written for when
   `NextAttackCritModifiers[]` was introduced. `GAME_STATE.md` §0 item 4 forbids
   adding a field ahead of its owning decision, and §0 item 5 forbids a second
   representation of a value.
2. **Which calculation reads it.** Whether the modifier reduces the validated
   cost, the deducted cost, the reported `CardCast` cost, or some composition of
   these is a `CARD_RULES.md` §3 semantic that no document states. "Heal Card
   cost −50%" must attach to a specific documented step.
3. **Composition and re-trigger semantics.** `RELIC_RULES.md` §2.4 item 6
   explicitly states that it "defines no stacking behavior" and that any such
   behavior is a future rule change; §8.4 governs trigger re-evaluation but not
   what two simultaneously-active cost modifiers do. Emergency Core's Condition
   re-evaluates continuously while HP < 30% (`RELIC_RULES.md` §6 note 2), so
   "triggers again while already active" is a real case, not a hypothetical.

Inventing any of these is the prohibited action (`AGENTS.md` §7, §20). Note the
repository's own discipline here: `Burning Curse` was deferred rather than
guessed (`RELIC_RULES.md` §6 note 3), and `ADR-017` exists precisely because a
modifier with no defined state carrier could not be implemented safely.

---

## Objective

Obtain and record, **in this task only**, the explicit Product-Owner / human
decision defining the **authoritative runtime representation of an applied,
Battle-scoped Relic `CardCost` modifier** — its state carrier, shape, scope,
application semantics, composition behavior, re-trigger behavior, expiration,
serialization, and projection — so that a subsequent contract/documentation
task can update the canonical documents and make TASK-133 implementation-ready.

This task records the decision. It implements no state member, no cost
calculation, and no resolver, and it **applies the decision to no authoritative
document**.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 (Relics IN — "~10 Relics", trigger system), §4 (unlisted ⇒ FUTURE)
- `docs/00-overview/ROADMAP.md` Phase 2 (Relic trigger system)
- `docs/01-game-design/RELIC_RULES.md` **§8** (the Relic contract this task does not reopen): §8.2 item 1 (`effectType` set incl. `CardCost`), **§8.3** (the allowed-combination table: `CardCost` | `Pet` | `Battle` | `Percentage`), §8.4 (effect lifetime vs trigger re-evaluation), **§8.5 item 2 (Emergency Core's effect and its continuous re-evaluation)**, §8.6 (storage consequence), §8.7 (startup status)
- `docs/01-game-design/RELIC_RULES.md` §1 (Relic structure), §2.4 item 6 (**"This defines no stacking behavior"** — the explicit deferral), §3 (`OnHpBelow`), §4 (deterministic trigger order), §5 (anti-infinite-chain rule), §6 + note 2 (Emergency Core: "re-evaluates continuously … not a one-shot fire", and reverts when HP rises above 30%), §6 note 3 (provisioned/deferred row set), §7 (Events)
- `docs/01-game-design/CARD_RULES.md` **§3** (Casting Rules — §3 item 2 "`current Power ≥ Card Cost`" validation, §3 item 4's `Deduct Cost from Power` sequence, §3 item 5 Card/Turn independence), §5 (Cards vs. Relics — the cost-modification boundary), §2/§4.1 (Card costs and effects — not restated)
- `docs/01-game-design/COMBAT_RULES.md` §5.1 (the closed MVP Status Effect type list — Burn, Shield, Buff/Debuff), §6 (Power as the Card/Skill resource), §1.1 (Power's 0–100 range)
- `docs/01-game-design/GAME_RULES.md` §12 (Power and Card costs), §16 (Battle Event Model), §17 (the fixed resolution order — step 11 "Trigger Relics"; step 14 Card cast), §18 (server authority), §20 (Rule Change Policy)
- `docs/02-technical/GAME_STATE.md` **§2.3** (`PetState` member set), **§2.3.1** (`StatusEffects[]` element schema and its wire note), §2.3.1 item 3 (the `RemainingTurns` XOR `ExpiryCondition` duration dichotomy), §2.3.1 item 6 (one-instance-per-identity, and §5.2 item 2's refresh-in-place), **§2.3.2 item 1** ("the collection always exists … never omitted and never `null`"), §2.3.2 item 5 (the round-trip obligation), **§2.3.3** (what the Status Effect model does not add, including the `PendingStatusEffects[]`/queued/second-representation prohibition), **§2.3.4** (`NextAttackCritModifiers[]` — the direct precedent: a purpose-built collection with a `SourceIdentity` removal key, its own schema, its own round-trip obligation, and its own "not a wire member" ruling), §2.4/§2.4.1 (`BossState` parity), §3 (Transient Resolution State), **§5.1** (the single post-resolution write-back), §5.1.1 (the step 19a Status Effect pass), §5.1.2 (the NextAttack modifier lifecycle — the mutation-ownership precedent), §0 items 4–5 (a member is added by its owning decision; no second representation)
- `docs/02-technical/DATABASE.md` §1 (the `CardDefinition.PowerCost` column — the definition-side cost, and the `RelicDefinition` structured contract), §5 item 4 (content-defined rows)
- `docs/02-technical/GAME_EVENTS.md` §2 (`CardCast` payload), §3 item 7 (emission sequencing)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.20 (`CardCast` — which currently reports the cost paid), §3.2.24 (`PowerChanged` and its `"relic"` source), §3.2.25 (the `effect summary` omission convention), **§4.2/§4.3** (the current `BattleStateUpdated` payload member sets), §4 item 10/§4.1 item 2 (state added is not wire exposure added)
- `docs/02-technical/REDIS_STATE.md` §2 (active battle state serialization), §4 (the single write-back and `Sequence` compare-and-set), **§7 item 13** (the round-trip obligation a new state member inherits)
- `docs/02-technical/TDD.md` §6 (determinism — a serialized member must round-trip deterministically)
- `docs/02-technical/ARCHITECTURE.md` §2.1 (Domain purity), §2.3 (persistence boundaries), §5 (anti-overengineering)
- `docs/03-decisions/README.md` §2 (ADR criteria), §4 (numbering — never reused), §5 (status values), §8 (known open items)
- **`docs/03-decisions/ADR/ADR-018-structured-relic-trigger-condition-effect-contract.md`** — the decision this task extends. Its item 5 states this decision introduces **no** battle-state member; the state carrier was left open. Note its item 9 and the storage/runtime split.
- `docs/03-decisions/ADR/ADR-017-nextattack-crit-modifiers-as-petstate-battle-state.md` — **precedent/evidence only, NOT the answer**: a purpose-built `PetState` modifier collection introduced because no existing representation could carry the concept, including its rejection of the `StatusEffects[]` escape hatch and its `SourceIdentity` removal-key design
- `docs/03-decisions/ADR/ADR-001` (server authority), `ADR-005` (Redis active battle state), `ADR-011` (`PetState` as the combat runtime state), `ADR-012` (Card/Relic ownership vs battle equip)
- `AGENTS.md` §4 (conflict resolution), §7 (invent no rule), §8 (MVP protection), §9 (anti-overengineering), §10 (server authority), §11 (determinism), §12 (domain boundaries — Card vs Relic), §17 (documentation change), **§18 (architecture change rule)**, §20 (stop conditions), §22
- `tasks/completed/TASK-131-resolve-relic-trigger-and-effect-resolution-contract.md` — the contract this task does not reopen; its Completion Evidence records this gap
- `tasks/completed/TASK-116-resolve-nextattack-crit-modifier-state-and-consumption-contract.md` and `TASK-119-resolve-root-atk-modifier-consumption-contract.md` — the precedent decision-task shape for a modifier state/consumption contract
- `tasks/backlog/TASK-132-migrate-relic-structured-condition-and-effectdefinition-storage.md` — the **content-side** storage task; see "TASK-132 Relationship"
- `tasks/backlog/TASK-133-implement-server-authoritative-relic-trigger-and-effect-resolution.md` — the task this blocks

---

## Scope

### In Scope

1. **Obtain the decision.** Present D1–D11 to the Product Owner / human and
   obtain explicit answers. Present every candidate option neutrally.
2. **Record the decision in this task.** Write the supplied answers into
   "Decision Recorded" verbatim. Record nothing the Product Owner did not
   supply.
3. **Record the TASK-132 relationship.** State whether the decision affects
   TASK-132's content-side scope or only the runtime side. Do **not** modify
   TASK-132.
4. **Record the TASK-133 relationship.** State that TASK-133 remains blocked
   pending the downstream contract-resolution step. Do **not** modify TASK-133.
5. **Record the handoff.** State explicitly that the recorded decision is input
   to the subsequent contract/documentation task, and that TASK-134 itself
   applies the decision to no authoritative document.
6. **Record the ADR determination (D11) as a decision, not as an act.** Whether
   an ADR amendment or a new ADR is needed is recorded as the Product Owner's
   answer. Authoring that ADR is the downstream task's act, not this one.

### Out of Scope

- **Applying the decision to any authoritative document.** `docs/` is untouched
  by this task — including `GAME_STATE.md`, `CARD_RULES.md`, `RELIC_RULES.md`,
  `REDIS_STATE.md`, `SIGNALR_PROTOCOL.md`, `DATABASE.md`, `GAME_EVENTS.md`, and
  `docs/03-decisions/`. That is the subsequent contract/documentation task.
- **Authoring or amending an ADR** — recorded as D11's answer only
- **Reopening any TASK-131 decision.** D1 effect vocabulary, D2 magnitude
  vocabulary, D3 target/scope model, D4 Assassin Eye, D5 Condition grammar,
  D6 Relic lifetime model, D7 Burning Curse, D8 Trigger list, D9 Relic storage
  contract, D10 `RelicTriggered` wire shape. All remain authoritative and
  unmodified.
- **Implementing any state member, cost calculation, Relic resolver, or
  Card-cast change** — `src/` and `tests/` are untouched
- **Any SignalR, Redis, or EF implementation**
- **Redesigning Card rules or Card costs** — `CARD_RULES.md` §2/§3/§4.1 owns
  them; D4 only identifies which documented step the modifier participates in
- **Changing `CardDefinition.PowerCost` or the Relic content rows**
- **Provisioning or un-deferring `Burning Curse`** — `RELIC_RULES.md` §6 note 3
  / TASK-131 D7
- **Adding a Status Effect type** — `GAME_STATE.md` §2.3.3 defers that to
  `COMBAT_RULES.md`; not this task
- **Any other Relic effect's state carrier** (ATK, Power, Crit) — ATK and Crit
  already have documented representations and Power is an existing `PetState`
  member; this task covers **only** `CardCost`
- **Relic stacking in general** — `RELIC_RULES.md` §2.4 item 6; D5 covers only
  multiple *CardCost* modifiers
- **Modifying TASK-131, TASK-132, or TASK-133** (explicitly prohibited), or any
  completed or superseded task — `TASK_LIFECYCLE.md` §3
- **Creating any further task** — including the downstream
  contract/documentation task and any implementation task. Those are identified
  but not created here.
- **Reopening TASK-079, TASK-099, or TASK-102** — terminal `SUPERSEDED`
- **TASK-036's Discord credential decisions** — separate, independent blocker
- **Any item listed as OUT in `MVP_SCOPE.md` §2**, or anything not IN
  `MVP_SCOPE.md` §1

---

## Current State

```text
Relic contract (TASK-131, ADR-018) — COMPLETE, do not reopen:
  RELIC_RULES.md §8.2  effectType set: ATK | Power | Crit | CardCost
  RELIC_RULES.md §8.3  CardCost | Pet | Battle | Percentage
  RELIC_RULES.md §8.5  Emergency Core: value 50, target Pet, lifetime Battle
  RELIC_RULES.md §6 n2 Emergency Core re-evaluates continuously while HP < 30%

PetState (GAME_STATE.md §2.3; src/backend/GameServer.Domain/Battle/PetState.cs):
  PetId, HP, MaxHP, ATK, DEF, Crit, Power, Element,
  PassiveId, PassiveProgress, PassiveResetOverride?,
  EquippedRelics[], EquippedCards[],
  ActiveStatusEffects[], NextAttackCritModifiers[]
  → NO Card-cost modifier member. No cost-modifier concept at all.

Card cost consumption (src/backend/GameServer.Domain/Cards/CardCastExecutor.cs):
  L52  state.PetState.Power < cardDefinition.PowerCost   → validate
  L58  state.PetState.Power - cardDefinition.PowerCost   → deduct
  L79  BattleEvent.CreateCardCast(..., cardDefinition.PowerCost) → report
  → All three read the DEFINITION's cost directly. No modifier indirection
    exists, and no document defines "Card Cost" as a composed value.

Existing precedents for a purpose-built modifier (EVIDENCE, not the answer):
  NextAttackCritModifiers[]  PetState member + own schema (§2.3.4) +
                             SourceIdentity removal key + own lifecycle
                             (§5.1.2) + own round-trip obligation + own
                             "not a wire member" ruling    [ADR-017]
  StatusEffects[]            governed by COMBAT_RULES.md §5.1's CLOSED type
                             list; a new type is a COMBAT_RULES decision
                             (GAME_STATE.md §2.3.3)

Documented prohibitions the answer must respect:
  GAME_STATE.md §2.3.3   no PendingStatusEffects[], no queued/pending
                         collection, no second representation of an in-flight
                         application
  GAME_STATE.md §0 item 5  no second representation of a value
  GAME_STATE.md §0 item 4  a field is added by its owning decision, not ahead
                           of it
  RELIC_RULES.md §2.4 n6   "This defines no stacking behavior"
```

---

## Required Decision Coverage

The executing agent must obtain explicit answers to all of the following. **An
unanswered item is a blocking stop condition, not an invitation to choose.**
Every item below carries the status `NOT DECIDED` until the Product Owner
answers it.

```text
D1 — State carrier:
     Where does an applied Battle-scoped CardCost modifier live?
     The following candidate categories must be presented NEUTRALLY. No option
     is recommended, ordered by ease, or eliminated by this task:
       A. Existing PetState member
       B. New PetState member
       C. Existing StatusEffects[]
       D. Existing generic modifier representation
       E. Derived from authoritative state (no stored modifier)
       F. Another documented representation
     The answer must be justified against GAME_STATE.md §2.3.
     Selection must NOT be made because it is easiest to implement, most
     consistent with the existing implementation, most similar to
     NextAttackCritModifiers[], simplest for Redis, simplest for SignalR, or
     simplest for CardCastExecutor.                              NOT DECIDED

D2 — Representation:
     If D1 selects a stored representation, what is its exact shape? The
     decision must define:
       member name
       single value vs collection
       every field
       field types
       requiredness
       ordering
       removal/identity semantics (cf. GAME_STATE.md §2.3.4's SourceIdentity)
     The examples below are ILLUSTRATIONS ONLY — not options to pick between:
       CardCostModifier            (single value)
       CardCostModifiers[]         (collection)
     Do NOT select one. Do NOT assume a field shape beyond the examples given.
                                                                 NOT DECIDED

D3 — Scope confirmation:
     Confirm the already-authoritative TASK-131 contract:
       target   = Pet
       lifetime = Battle
     This is CONFIRMATION of RELIC_RULES.md §8.3, not a reopening of TASK-131.
                                                                 NOT DECIDED

D4 — Card-cost application semantics:
     The Product Owner must decide exactly how "-50% Heal Card cost"
     participates in Card casting. The decision must identify whether the
     modifier affects:
       validation                       (CARD_RULES.md §3 item 2)
       deduction                        (CARD_RULES.md §3 item 4)
       reported CardCast cost           (GAME_EVENTS.md §2 /
                                         SIGNALR_PROTOCOL.md §3.2.20)
       or another explicitly documented cost-reading point
     When the modifier is applied must also be stated.
     Do NOT choose based on the current implementation, and do not redesign
     Card rules.                                                 NOT DECIDED

D5 — Multiple CardCost modifiers:
     Decide what happens when multiple CardCost modifiers apply
     simultaneously:
       stack
       replace
       compose
       other explicit rule
     RELIC_RULES.md §2.4 item 6 currently defines no stacking behavior, so an
     existing determination must be cited precisely or the answer must be an
     explicit Product Owner decision. Do not infer the answer.   NOT DECIDED

D6 — Re-trigger behavior:
     Decide what happens when Emergency Core triggers again while its modifier
     is already active. RELIC_RULES.md §6 note 2 makes continuous
     re-evaluation relevant, so this is a real case. The candidate semantics
     must be presented NEUTRALLY and this task selects none:
       refresh | replace | stack | no-op | other explicit rule
     Do not infer the answer.                                    NOT DECIDED

D7 — Expiration:
     Given lifetime = Battle, decide the exact expiration boundary:
       battle end | victory/defeat resolution | new battle initialization |
       other explicit boundary
     Do NOT invent a turn-based or turn-count expiration.        NOT DECIDED

D8 — State serialization:
     Decide whether the selected carrier participates in:
       BattleState JSON serialization
       Redis active BattleState
     and what deterministic round-trip requirement applies (TDD.md §6,
     REDIS_STATE.md §7 item 13, GAME_STATE.md §2.3.2 item 5).
     Do NOT implement serialization.                             NOT DECIDED

D9 — SignalR projection:
     Decide whether the selected state is exposed through the existing
     BattleStateUpdated projection (SIGNALR_PROTOCOL.md §4.2/§4.3), or whether
     GAME_STATE.md §2.3.1's "state added is not wire exposure added"
     convention applies.
     Do NOT create a new event. Do NOT choose based on implementation
     convenience.                                                NOT DECIDED

D10 — Existing state constraints:
     The decision must explicitly address compatibility with:
       GAME_STATE.md §2.3
       GAME_STATE.md §2.3.3
       GAME_STATE.md §0 item 4
       GAME_STATE.md §0 item 5
     including the prohibition on:
       PendingStatusEffects[]
       queued/pending state
       a second in-flight representation
       a duplicate state representation
     Record the verification the Product Owner supplies.         NOT DECIDED

D11 — ADR treatment:
     Decide whether the eventual authoritative contract requires:
       amend ADR-018
       create a new ADR
       another explicitly justified treatment
     ADR-017's existence does NOT automatically require a new ADR, and this
     task must not assume that it does.                          NOT DECIDED
```

**All eleven items below were `NOT DECIDED` and have since been answered by the
Product Owner — see "Decision Record (D1–D11)".** The `NOT DECIDED` marker on
each item records the state in which the decision was requested; the answer for
each is the correspondingly-numbered decision in that section, which is the
recorded decision and the input to the downstream contract/documentation task.
The markers are retained rather than rewritten so the original request and the
supplied answer remain separately auditable.

---

## Decision Record (D1–D11)

**Recorded verbatim as supplied by the Product Owner.** Each item answers the
correspondingly-numbered item of "Required Decision Coverage". No value below
was derived, inferred, computed, or reinterpreted by this task, and no
assumption was added.

```text
D1   PetState.CardCostModifiers[]

D2   Collection with:
     - SourceIdentity
     - CostReductionPercentage

D3   Target = Pet; Lifetime = Battle

D4   EffectiveCardCost is calculated before validation, deduction,
     and reporting.

D5   Additive percentage reduction; total reduction capped at 100%.

D6   Same source replaces/refreshes its existing modifier.

D7   Modifier expires when the battle ends.

D8   Stored inside BattleState.PetState and serialized through
     existing BattleState JSON/Redis.

D9   Existing BattleStateUpdated projection; no new SignalR
     event/method.

D10  Preserve existing GAME_STATE constraints; no pending
     collection, no second representation, no duplicate state.

D11  Amend ADR-018.
```

### Status of the "Required Decision Coverage" items

Every item below was `NOT DECIDED`; each is now answered by the corresponding
decision above. The `NOT DECIDED` markers in "Required Decision Coverage" are
thereby resolved — they are superseded by this record, not restated here.

```text
D1  → ANSWERED by the D1 decision (state carrier selected)
D2  → ANSWERED by the D2 decision (exact shape supplied)
D3  → ANSWERED by the D3 decision (confirmation of RELIC_RULES.md §8.3:
     target = Pet, lifetime = Battle — confirmation only, TASK-131 not reopened)
D4  → ANSWERED by the D4 decision (the point at which the modifier
     participates, and when the effective cost is calculated)
D5  → ANSWERED by the D5 decision (composition of multiple modifiers)
D6  → ANSWERED by the D6 decision (re-trigger behavior)
D7  → ANSWERED by the D7 decision (expiration boundary under Battle lifetime)
D8  → ANSWERED by the D8 decision (serialization treatment)
D9  → ANSWERED by the D9 decision (projection treatment)
D10 → ANSWERED by the D10 decision (constraint compatibility confirmation)
D11 → ANSWERED by the D11 decision (ADR treatment)
```

### Boundary confirmation

```text
The decision was recorded. NOTHING was applied to authoritative documentation.

This task did NOT:
  - create PetState.CardCostModifiers[]        (source; D1/D2 are the contract,
                                                implementation is downstream)
  - add any member to GAME_STATE.md            (docs/ untouched)
  - define "EffectiveCardCost" in CARD_RULES.md (docs/ untouched)
  - implement any cost calculation             (src/ untouched)
  - amend ADR-018                              (D11 recorded; the amendment is
                                                the downstream task's act)
```

---

## Decision Recording

TASK-134 is a decision-input task.

The executing agent must NOT apply the Product Owner decision to authoritative
documentation.

The executing agent must record the explicit decision in this task only.

The recorded decision is input to the subsequent contract/documentation task.

TASK-134 must not modify:

```text
- docs/01-game-design/
- docs/02-technical/
- docs/03-decisions/
- src/
- tests/
- TASK-131
- TASK-132
- TASK-133
- any completed or superseded task
```

If the repository workflow requires the decision to be applied to an
authoritative document before the downstream implementation task can proceed:

```text
STOP after recording the decision in TASK-134.

Do not perform that documentation update in TASK-134.
```

---

## TASK-131 Boundary

```text
TASK-131 (DONE) and RELIC_RULES.md §8 / ADR-018 remain AUTHORITATIVE for the
Relic Trigger/Condition/Effect contract.

This task resolves ONLY the missing runtime state carrier for CardCost.

It does NOT reopen:
  TASK-131 D1   effect vocabulary
  TASK-131 D2   magnitude vocabulary
  TASK-131 D3   target/scope model
  TASK-131 D4   Assassin Eye
  TASK-131 D5   Condition grammar
  TASK-131 D6   Relic lifetime model
  TASK-131 D7   Burning Curse
  TASK-131 D8   Trigger list
  TASK-131 D9   storage contract
  TASK-131 D10  RelicTriggered wire shape
```

D3 above **confirms** §8.3's `target`/`lifetime` values as an input to this
decision; confirmation is not re-litigation. If the executing agent finds that
the state carrier cannot be chosen without changing any of the ten items above,
that is a stop condition — not a mandate to change them.

---

## TASK-132 Relationship

```text
TASK-132 = content-side storage
TASK-134 = runtime decision-input
TASK-133 = runtime implementation
```

TASK-132 (`tasks/backlog/TASK-132-migrate-relic-structured-condition-and-effectdefinition-storage.md`)
migrates the **content-side** storage: `RelicDefinition.Condition` and
`RelicDefinition.EffectDefinition` from `varchar(128)` prose to the structured
`jsonb` representation (`RELIC_RULES.md` §8.6, TASK-131 D9).

This task concerns the **runtime-side** carrier for an applied effect.

These are different layers. TASK-134 must only **record** whether the decision
affects TASK-132. It must NOT modify TASK-132. The executing agent must
determine and record which is true:

```text
(a) The decision affects ONLY BattleState runtime storage
    → TASK-132's scope is unaffected; record that finding explicitly and
      leave TASK-132 untouched.

(b) The decision also affects the STRUCTURED CONTENT SHAPE
    (e.g. it determines that a CardCost element must carry a member
     RELIC_RULES.md §8.2/§8.3 does not currently define)
    → that would be a change to the TASK-131 structured content contract.
      STOP.
      Do NOT alter TASK-132 to compensate. Record the finding only.
```

**TASK-132 must NOT be modified by this task under any circumstance.**

---

## TASK-133 Relationship

TASK-133 (`tasks/backlog/TASK-133-implement-server-authoritative-relic-trigger-and-effect-resolution.md`)
implements `GAME_RULES.md` §17 step 11 and includes Emergency Core among its
deliverables.

**TASK-133 remains BLOCKED for the Emergency Core / CardCost path until the
runtime contract is resolved.** TASK-134 must not modify TASK-133.

The recorded decision is the input that the later documentation task will use
to make TASK-133 implementation-ready. Until that downstream step lands, TASK-133
cannot implement Emergency Core without inventing a state representation.

---

## Implementation Notes

- **This task's single prohibited action is inventing the answer.** Every item
  in "Required Decision Coverage" is a Product-Owner or human decision. If no
  answer is supplied, STOP per `AGENTS.md` §7 — do not choose a carrier, a field
  shape, a composition rule, or an expiration rule.
- **Implementation convenience must not determine the decision.** No D1–D11
  option may be selected because it is easier to implement, consistent with an
  existing implementation, similar to `NextAttackCritModifiers[]`, simpler for
  Redis, simpler for SignalR, simpler for `CardCastExecutor`, or architecturally
  convenient.
- **Present the options neutrally.** D1's six categories and D6's five
  semantics are stated as candidates for the decision-maker, not as a shortlist
  with a recommended answer. Do not order them by ease of implementation, and
  do not editorialize.
- **`ADR-017` is precedent/evidence only, not the answer.** It records that a
  purpose-built `PetState` modifier collection was chosen for NextAttack Crit,
  that the `StatusEffects[]` escape hatch was explicitly rejected, and that a
  `SourceIdentity` removal key was required. It is evidence about how this
  repository solves this class of problem — but D1 and D2 are still open
  decisions, and `CardCost` differs from Crit (it modifies a *cost read at cast
  time*, not a stat, and it has no "next attack" consumption event).
- **`RELIC_RULES.md` §2.4 item 6 is a live constraint.** It states "This defines
  no stacking behavior… Any such behavior is a future rule change
  (`GAME_RULES.md` §20)". D5 must therefore either cite a document that *does*
  determine the behavior or require an explicit decision.
- **Emergency Core's continuous re-evaluation is not a one-shot fire.**
  `RELIC_RULES.md` §6 note 2 and §8.5 item 2 both state it re-evaluates while
  the condition holds and reverts when HP rises above 30%. D6 and D7 must be
  answered consistently with that — including what "reverts" means for an
  applied modifier.
- **Do NOT apply the answer to any document.** The canonical owner documents
  (`GAME_STATE.md` for D1/D2/D8/D9/D10, `CARD_RULES.md` §3 for D4 if that is
  where the reading belongs, `RELIC_RULES.md` for a cross-reference only, and
  the ADR set for D11) are updated by the **subsequent** contract/documentation
  task — not here. `AGENTS.md` §12's domain boundaries still apply to whoever
  performs that step: Card cost is a Card concern; the Relic side only declares
  the effect.
- **No gameplay expansion.** This task records a state-carrier decision. It
  introduces no new Relic, Trigger, Condition, `effectType`, combat mechanic,
  Card mechanic, Status Effect type, stacking system, or event.
- **Do not modify TASK-131, TASK-132, or TASK-133.** All three are explicitly
  protected. TASK-131 is DONE (immutable); TASK-132 and TASK-133 are
  BLOCKED-pending and must be left byte-identical.
- **Do not create another task.** The downstream contract/documentation task
  and any implementation task are identified and handed off, not created here.
- **This task is NOT a broad audit.** Its scope is exactly the `CardCost`
  modifier state carrier. Do not sweep other Relic effects, other modifiers, or
  unrelated gameplay.
- Do not modify `tasks/completed/`.

---

## Acceptance Criteria

- [ ] The documented CardCost runtime-state gap is clearly stated.
- [ ] D1–D11 are presented as explicit Product Owner/human decisions.
- [ ] No D1–D11 option is selected by the executing agent.
- [ ] The task explicitly states that implementation convenience must not
      determine the decision.
- [ ] The task explicitly records the decision as input for the downstream
      contract/documentation task.
- [ ] TASK-134 does not modify authoritative documentation.
- [ ] TASK-134 does not modify source code.
- [ ] TASK-134 does not modify tests.
- [ ] TASK-131 remains unchanged.
- [ ] TASK-132 remains unchanged.
- [ ] TASK-133 remains unchanged.
- [ ] No new gameplay rule is invented.
- [ ] No new state member is invented.
- [ ] No new state shape is invented.
- [ ] No new stacking rule is invented.
- [ ] No expiration rule is invented.
- [ ] No new SignalR event is invented.
- [ ] No Redis behavior is invented.
- [ ] The final recorded decision is complete enough for the downstream
      documentation/contract task to consume.
- [ ] The TASK-132 relationship is recorded as either "runtime only"
      (TASK-132 unaffected) or a reported dependency — with TASK-132 left
      unmodified either way.
- [ ] The TASK-133 blocking relationship is stated, and TASK-133 is left
      unmodified.
- [ ] All ten TASK-131 items listed under "TASK-131 Boundary" remain unmodified.
- [ ] Zero files under `src/` or `tests/` are modified.
- [ ] Zero gameplay values, formulas, or balance figures are invented.
- [ ] No new Relic, Trigger, Condition, `effectType`, combat mechanic, Card
      mechanic, Status Effect type, stacking system, or event is introduced.
- [ ] TASK-131, TASK-132, TASK-133, and all completed/superseded tasks are
      byte-identical.
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[ ] src/backend/ (none — decision-input task; no code)
[ ] src/frontend/client/ (none)
[ ] tests/ (none)
[ ] docs/01-game-design/ (none — MUST NOT be modified by TASK-134)
[ ] docs/02-technical/ (none — MUST NOT be modified by TASK-134)
[ ] docs/03-decisions/ (none — MUST NOT be modified by TASK-134; D11 is
                          recorded as a decision, and the ADR act belongs to
                          the downstream task)
[x] tasks/backlog/TASK-134-resolve-cardcostmodifier-authoritative-state-carrier.md
        (this file — the only file TASK-134 may modify)
[ ] tasks/backlog/TASK-132, tasks/backlog/TASK-133 (MUST remain unmodified)
[ ] tasks/completed/ (MUST remain unmodified)
```

---

## Testing Requirements

### Required Verification

This remains a decision-input task. Implementation and documentation tests are
replaced by audit checks.

```text
[x] Decision coverage audit
    Confirm D1–D11 are explicitly presented.

[x] Neutrality audit
    Confirm no option is recommended or selected by the task.

[x] Boundary audit
    Confirm TASK-134 is decision-input only.

[x] Protected-file audit
    Confirm TASK-131, TASK-132, TASK-133 remain untouched.

[x] Source/test audit
    Confirm zero source and test files are modified.

[x] Authoritative-doc audit
    Confirm zero docs/ files are modified by TASK-134.

[ ] Gameplay implementation tests
    N/A — no code is implemented.

[ ] Integration tests
    N/A — no runtime behavior is implemented.
```

Do not claim documentation consistency across a future decision that has not yet
been made.

### Key Edge Cases (for the downstream contract/documentation task)

Recorded here so the decision's consumer inherits them; they are **not** checks
this task performs against an implementation.

- The selected representation must be able to express Emergency Core's `-50%`
  `Percentage` value without a second magnitude convention
- The selected representation must handle the "already active, triggers again"
  case that `RELIC_RULES.md` §6 note 2 makes real
- The selected representation must have a defined `Battle`-lifetime end that
  does not conflict with `GAME_RULES.md` §17's resolution order or with
  battle-end processing
- If a collection is selected, its ordering must be deterministic and its
  removal key must be deterministic and reproducible (`TDD.md` §6) — cf.
  `GAME_STATE.md` §2.3.4 item 2's rejection of GUIDs, timestamps, and array
  positions as keys
- If the representation is exposed on the wire, the round-trip obligation of
  `GAME_STATE.md` §2.3.2 item 5 and `REDIS_STATE.md` §7 item 13 must hold
- The decision must not create a second representation of any value
  `GAME_STATE.md` §0 item 5 already covers

---

## Stop Conditions

- **If the required Product-Owner decision is not supplied: STOP per
  `AGENTS.md` §7.** Do not choose a state carrier, a field shape, a composition
  rule, an expiration rule, or a projection treatment.
- **If applying the decision to an authoritative document appears necessary to
  complete this task: STOP after recording the decision.** Do not perform that
  documentation update in TASK-134.
- **If the decision would require changing the TASK-131 structured content
  contract: STOP.** Do not alter TASK-132 to compensate.
- If existing authoritative documents already contain a **conflicting** Card-cost
  state model: STOP and report the conflict per `AGENTS.md` §4 — do not silently
  pick a side
- If choosing a state carrier requires a **broader gameplay decision** than this
  task's scope: STOP and report it
- If choosing a state carrier requires **changing the Relic contract from
  TASK-131** (any of the ten protected items, including §8.2/§8.3's element
  shape): STOP — that is out of boundary
- If **multiple `CardCost` modifier semantics remain unresolved** after D5 and
  no decision is supplied: STOP
- If **Card cost calculation ownership is ambiguous** — i.e. no document can be
  identified as owning "Card Cost" as a composed value: STOP and report which
  document should own it rather than assigning ownership
- If **serialization ownership is ambiguous**: STOP
- If **SignalR projection requirements conflict** with `GAME_STATE.md` §2.3.1's
  wire note or `SIGNALR_PROTOCOL.md` §4: STOP
- If a **new architectural decision is required but cannot be decided**: STOP
- If the decision would require **modifying a completed task** (TASK-131,
  TASK-082, TASK-109, TASK-116, ADR-017, ADR-018's decided content): STOP —
  completed tasks are immutable (`TASK_LIFECYCLE.md` §3)
- If the decision would require **modifying TASK-132 or TASK-133**: STOP — both
  are protected by this task's instructions; record the dependency instead
- If **implementation becomes necessary to determine the contract**: STOP — that
  inverts the required order (`AGENTS.md` §5, §18)
- If the decision would place Card-cost or Relic-effect computation on the
  client: STOP per `AGENTS.md` §10
- If the task drifts into implementing the state member, the cost calculation,
  the Relic resolver, or applying the decision to documentation: STOP — those
  are separate tasks
- If the task is asked to create another task: STOP — the downstream work is
  identified and handed off, not created here
- If the task is asked to reopen TASK-079, TASK-099, or TASK-102: STOP —
  terminal `SUPERSEDED`
- If the task is asked to resolve TASK-036's Discord credential decisions:
  STOP — separate, independent blocker
- If task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: STOP & decompose

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after the Product Owner decision is
  supplied and recorded.
  Keep concise and factual.
  DO NOT fill the decision fields with invented answers.
  If the Product Owner decision is still missing, the task remains BLOCKED and
  the fields remain explicitly unresolved.
-->

### Decision Recorded

The Product Owner supplied an explicit answer for all eleven decision items.
Recorded verbatim in "Decision Record (D1–D11)" above. No value was derived,
inferred, computed, or reinterpreted, and no assumption was added.

Product Owner decision:

```text
D1:  PetState.CardCostModifiers[]
D2:  Collection with:
     - SourceIdentity
     - CostReductionPercentage
D3:  Target = Pet; Lifetime = Battle
D4:  EffectiveCardCost is calculated before validation, deduction,
     and reporting.
D5:  Additive percentage reduction; total reduction capped at 100%.
D6:  Same source replaces/refreshes its existing modifier.
D7:  Modifier expires when the battle ends.
D8:  Stored inside BattleState.PetState and serialized through
     existing BattleState JSON/Redis.
D9:  Existing BattleStateUpdated projection; no new SignalR
     event/method.
D10: Preserve existing GAME_STATE constraints; no pending
     collection, no second representation, no duplicate state.
D11: Amend ADR-018.
```

**Decision completeness:** all eleven items were answered, so no stop condition
fired and the task reaches `DONE`. D9 records "no new SignalR event/method" and
D11 records "amend ADR-018" as decisions; neither act was performed by this
task.

### Documentation

No authoritative documentation was modified by TASK-134.

Verified: zero files under `docs/` were modified by this execution. The
`docs/` entries visible in `git status` (`RELIC_RULES.md`, `DATABASE.md`,
`SIGNALR_PROTOCOL.md`, `docs/03-decisions/README.md`, `ADR-018`) are the
pre-existing TASK-131 reconciliation set: their modification times are
13:56–13:58, predating this execution at 14:16, and their content hashes are
unchanged across it.

### Handoff

The recorded decision is input to the subsequent contract/documentation task.

TASK-134 does not itself apply the decision to `GAME_STATE.md`,
`CARD_RULES.md`, `RELIC_RULES.md`, `REDIS_STATE.md`, `SIGNALR_PROTOCOL.md`,
or `ADR-018`.

```text
What the downstream task consumes from this record:

  D1, D2   the state carrier and its exact shape → GAME_STATE.md §2.3
           (the canonical owner of the PetState member set)
  D4       the Card-cost reading point, and the "EffectiveCardCost"
           term → CARD_RULES.md §3 (the owner of Card casting and cost),
           with RELIC_RULES.md receiving a cross-reference only
  D5, D6   composition and re-trigger semantics → the owning rule document
           (D5 must be reconciled against RELIC_RULES.md §2.4 item 6, which
           currently defines no stacking behavior)
  D7       the Battle-lifetime expiration boundary
  D8, D9   serialization and projection treatment → REDIS_STATE.md §2/§7,
           SIGNALR_PROTOCOL.md §4.2/§4.3 (D9 reconfirms no new event)
  D10      the GAME_STATE.md §2.3 / §2.3.3 / §0 item 4 / §0 item 5
           compatibility statement
  D11      the ADR-018 amendment
```

This handoff is recorded, not performed. The downstream task is **not** created
by TASK-134.

### TASK-132

TASK-132 was not modified. Verified: its content hash is unchanged
(`b4b43e06…`) across this execution.

Relationship:

```text
runtime-only

The D1/D2 decision selects a BattleState runtime carrier
(PetState.CardCostModifiers[] with SourceIdentity and
CostReductionPercentage). That is a runtime-state representation, not a
change to the structured CONTENT shape: the CardCost element's declared
members remain RELIC_RULES.md §8.2/§8.3's effectType / valueType / value /
target / lifetime, and the decision adds no member to that element.

TASK-132 therefore remains scoped to the content-side storage migration and
requires no change. Recorded as a finding; TASK-132 was not altered.
```

This is branch (a) of this task's "TASK-132 Relationship": the decision affects
only BattleState runtime storage, so TASK-132's scope is unaffected. No
TASK-131 structured content-shape change is implied, so no stop condition fired
on this point.

### TASK-133

TASK-133 was not modified. Verified: its content hash is unchanged
(`d70e98f9…`) across this execution.

Status:

```text
BLOCKED pending authoritative runtime CardCost contract.
```

TASK-133 remains blocked for the Emergency Core / `CardCost` path. TASK-134
supplied the decision; it did not apply it to the authoritative documents, so
TASK-133 is not yet implementation-ready. Making it implementation-ready
requires the downstream contract/documentation step, which consumes this record
and updates the canonical owner documents.

### Changed Files

- `tasks/backlog/TASK-134-resolve-cardcostmodifier-authoritative-state-carrier.md`
  — the only file TASK-134 modified. Change: the D1–D11 decision recorded
  verbatim in "Decision Record (D1–D11)", the eleven "Required Decision
  Coverage" items resolved against it, `Status` set to `DONE`, and this
  Completion Evidence section completed.

### Validation Results

Decision/task-boundary validation only. No implementation tests were run —
this task implements no behavior.

```text
[x] D1–D11 explicitly answered           — PASS (all eleven supplied by the
                                               Product Owner and recorded)
[x] No executing-agent option selection  — PASS (no option chosen, inferred,
                                               defaulted, or recommended by the
                                               agent; every value is the
                                               Product Owner's)
[x] Decision recorded faithfully         — PASS (verbatim; no reinterpretation,
                                               no added assumption)
[x] TASK-131 unchanged                   — PASS (hash 8a704f71…)
[x] TASK-132 unchanged                   — PASS (hash b4b43e06…)
[x] TASK-133 unchanged                   — PASS (hash d70e98f9…)
[x] No docs/ modified                    — PASS (zero files; the visible
                                               docs/ diff predates execution)
[x] No src/ modified                     — PASS (zero files; read-only
                                               inspection of PetState.cs and
                                               CardCastExecutor.cs)
[x] No tests modified                    — PASS (zero files)
[x] No ADR created/modified              — PASS (D11 records "amend ADR-018" as
                                               a decision; ADR-018 is unmodified)
[x] No new gameplay rule invented        — PASS
[x] No new state member invented by the  — PASS (D1/D2's member is the Product
    agent                                    Owner's decision, not the agent's;
                                             nothing was added to any file)
[x] No stacking behavior invented by the — PASS (D5 is the Product Owner's
    agent                                    decision)
[x] No expiration behavior invented by   — PASS (D7 is the Product Owner's
    the agent                                decision)
[x] No SignalR behavior invented by the  — PASS (D9 reconfirms no new event)
    agent
[x] No Redis behavior invented by the    — PASS (D8 reuses existing BattleState
    agent                                    JSON/Redis)
[x] No new task created                  — PASS (backlog holds TASK-132/133/134
                                               only)
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed zero files modified under `src/` or `tests/`
- [x] Confirmed zero files modified under `docs/`
- [x] Confirmed TASK-131, TASK-132, TASK-133, and `tasks/completed/` unmodified
- [x] Confirmed the ten TASK-131 protected items are unmodified
- [x] Confirmed no new Relic, Trigger, Condition, `effectType`, Status Effect
      type, stacking system, or event was introduced

### Follow-Up Work Identified (not created)

- **Subsequent contract/documentation task** — consumes this record and applies
  the D1–D11 decision to the canonical owner documents, including the D11
  ADR-018 amendment. This is the step that makes TASK-133 implementation-ready.
  **Not** created by TASK-134.
- **TASK-133 unblocking** — follows the downstream documentation step; TASK-133
  itself is not modified here and remains blocked until then.
