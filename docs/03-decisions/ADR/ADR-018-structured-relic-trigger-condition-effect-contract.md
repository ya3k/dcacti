# ADR-018: Structured Relic Trigger, Condition, and Effect Contract

**Status:** Accepted
**Date:** 2026-10-02
**Amended:** 2026-10-02 (item 12 added — the runtime state carrier for the
`CardCost` effect, per TASK-134 D1–D11. Items 1–11 and the original Context,
Consequences, Alternatives Considered, and Related Documents records are
preserved unmodified below; the amendment is appended, not substituted.)

## Context

`GAME_RULES.md` §17's fixed resolution order places **step 11 "Trigger Relics"**
between resource generation and damage, and `RELIC_RULES.md` §7 defines
`RelicTriggered` as the event fired at that point. `SIGNALR_PROTOCOL.md`
§3.2.23 fixes that event's wire shape but states plainly in item 4 that
"**Emission is not implemented by this contract**".

No Relic resolution stage exists. `TASK-027` confirmed at the time that no
Relic trigger engine, effect resolution, or stacking existed; a repository-wide
search during TASK-131's discovery confirmed that remains true — the only
occurrences of `RelicTriggered` in `src/backend/` are comments deferring the
stage to another task.

The stage could not be built, because the Relic content rows could not carry
the information a resolver must read. `DATABASE.md` §1 stored
`RelicDefinition.EffectDefinition` as the owning document's **verbatim rule
text** in a `character varying(128)` column, under TASK-082 decision **R2-7**,
which expressly forbade "an `effect-{slug}` vocabulary or any new
effect-reference identifier system":

```text
relic-berserker-core  OnMatchCount  "every 3 Matches"   "+5% ATK"
relic-mana-crystal    OnMatchCount  "every 4 Matches"   "+10 Power"
relic-assassin-eye    OnCombo       "Combo >= 3"        "Increased Crit chance"
relic-emergency-core  OnHpBelow     "HP < 30%"          "Heal Card cost -50%"
```

The `Condition` values embedded their thresholds in prose, and the
`EffectDefinition` values carried no effect-type discriminator, no magnitude
carrier, and no target or lifetime. `Assassin Eye`'s magnitude was purely
qualitative ("Increased Crit chance"), while `COMBAT_RULES.md` §2 item 7 already
composed `EffectiveCrit` from a `RelicCrit` term — a term with no authored Relic
magnitude anywhere.

This was the **same class of gap** the repository had already resolved once for
Cards. `TASK-108` was created as a stop-driven decision task because
`CardDefinition.EffectDefinition` was verbatim prose carrying no effect
vocabulary or magnitude carrier, and recorded that the Card implementation task
"cannot be executed without either inventing an effect contract or first
resolving this one". The Card gap was then resolved across TASK-108 (contract
decision) → TASK-109 (structured `jsonb` implementation) → TASK-110 (magnitude
authoring) → TASK-111/112 (multi-effect contract and encoding). TASK-109
explicitly **scoped its supersession of R2-7 to `CardDefinition` only** and left
`RelicDefinition` under R2-7, stating: "No decision, no implementation, and no
consumer exists."

Relics had had none of that sequence. Inventing the vocabulary, a magnitude, or
a Condition grammar is precisely what `AGENTS.md` §7 forbids.

The Product Owner resolved the contract in TASK-131 (D1–D11). D11 records that
the decision requires an ADR because it establishes a cross-layer contract
spanning the game-design rule document, the persistence schema, the Domain
content model, and the realtime event surface. `docs/03-decisions/README.md` §8
was checked: this gap was **not** listed there. This ADR records the
architectural decision and the cross-layer contract it requires. It authors no
gameplay rule and changes no gameplay decision.

## Decision

```text
1.  Relic effects use a structured `EffectDefinition[]`, not verbatim prose.
    The representation follows the contract shape already established for
    `CardDefinition.EffectDefinition` (TASK-108 D-1/D-2, TASK-111 D-1/D-5),
    because it is the same problem class and the repository already has one
    contract for it.

2.  Each element carries its own `effectType` / `valueType` / `value` triple.
    The `valueType` set is `Flat` | `Percentage` | `PercentagePoints` |
    `Undetermined`. `Undetermined` is not an interpretation: it records that
    the owning document states no magnitude yet, and it remains valid, so no
    value has to be invented to make such an effect representable. An
    `Undetermined` element carries no `value` member at all.

3.  Effects carry an explicit `target` and `lifetime` vocabulary, and the
    allowed combinations are fixed per `effectType`. A combination not listed
    is not defined and may not be inferred. `lifetime` values are `Immediate`,
    `Battle`, and `NextAttack`. `NextAttack` REUSES the existing Card `Crit`
    consumption boundary (ADR-017) rather than introducing a second one.

4.  Relic conditions are structured, not prose. The defined forms are
    `MatchCountAtLeast(N)`, `ComboAtLeast(N)`, and `HpPercentageBelow(N)`,
    each carrying its own threshold as an integer and evaluated against the
    current resolution state at the point GAME_RULES.md §17 step 11 executes.

5.  NO PERSISTENT RELIC COUNTERS ARE INTRODUCED. A Relic owns no counter of
    its own, and no Relic-scoped counter is added to active battle state.
    `MatchCountAtLeast` reads the battle's existing cumulative Match count and
    `ComboAtLeast` reads the existing Combo value. This decision therefore adds
    no member to GAME_STATE.md and no battle-state concept.

6.  Effect lifetime and trigger re-evaluation are INDEPENDENT. A Relic whose
    effect lifetime has ended is not disabled: an `Immediate` or `NextAttack`
    effect ending does not stop the Trigger from being re-evaluated on later
    events. Re-firing remains RELIC_RULES.md §1's `Reset/Cooldown` default, and
    §5's anti-infinite-chain rule remains the only constraint on repeated
    firing. No cooldown, no charge, and no per-Relic reset state is added.

7.  `RelicDefinition.Trigger` remains RELIC_RULES.md §3's existing closed list,
    unchanged: one primary Trigger per Relic. No trigger value is added,
    removed, or reinterpreted.

8.  `Burning Curse` remains deferred. The reported conflict between §3's
    "exactly one primary Trigger from this list" and §6 note 1's description of
    Burning Curse as a static modifier with no event trigger is NOT resolved by
    this decision, and no Trigger is invented for it. This is a deliberate
    AGENTS.md §4 report-not-resolve outcome, not an oversight.

9.  The existing `character varying(128)` columns are INSUFFICIENT for the
    structured representation. `RelicDefinition.EffectDefinition` and
    `RelicDefinition.Condition` require the same `jsonb` storage the Card
    member required. The migration is a SEPARATE task (AGENTS.md §18); this
    decision records the requirement and performs no schema change.

10. `RelicTriggered` remains `{ type, relicId }`. The resulting state is
    delivered through the existing `BattleState` projection, exactly as
    SIGNALR_PROTOCOL.md §3.2.23 and §3.2.25 already dispose of `effect
    summary`. No wire member is added, and the realtime contract is unchanged.

11. TASK-082 decision R2-7 is SUPERSEDED for `RelicDefinition.EffectDefinition`
    by this decision. Combined with TASK-109's earlier Card-scoped
    supersession, R2-7 is now superseded for both members it governed, and no
    member remains under it. TASK-082 is not edited: completed tasks are
    immutable (tasks/TASK_LIFECYCLE.md §3).
```

### Amendment — Decision 12: the `CardCost` Runtime State Carrier

*(Added per TASK-134 D11. Item 5 above states that this decision introduces no
battle-state member; that remains true of items 1–11 and of the structured
content contract. The `CardCost` effect's **runtime** carrier was left open by
item 5 and the Consequences note below, and is decided here.)*

The Product Owner resolved it in TASK-134 (D1–D11), together with the explicit
`EffectiveCardCost` fractional-value decision. This amendment records the
architectural decision and the state-model contract it requires. It authors no
gameplay rule and changes no gameplay decision recorded elsewhere.

```text
12. `PetState.CardCostModifiers[]` is a dedicated, source-specific collection
    on the authoritative `PetState` — the carrier for an applied,
    Battle-scoped Relic `CardCost` modifier (TASK-134 D1/D2). It is a member
    of `BattleState` like every other `PetState` member.

    a. Each element carries exactly TWO members:

           SourceIdentity           the source-scoped, stable identity of the
                                    source that applied the modifier — the
                                    replace/refresh and removal key
           CostReductionPercentage  the cost reduction in percentage points

       Two members is the whole schema. No `Duration`, no `RemainingTurns`,
       no `ExpiresAt`, no `ExpiryCondition`, no `StackCount`, no Turn counter,
       no "consumed" flag, no priority, and no timestamp is stored.

    b. It is SEPARATE from `StatusEffects[]`. A modifier here is not a
       `StatusEffect` instance; it carries no `RemainingTurns` and no
       `ExpiryCondition`. It therefore does not engage GAME_STATE.md §2.3.1
       item 3's duration-model dichotomy and does not engage item 6's
       one-instance-per-identity rule — neither is widened or relaxed. The
       effect was NOT turned into a Status Effect type, and COMBAT_RULES.md
       §5.1's closed MVP type list is untouched.

    c. GAME_STATE.md §2.3.3's prohibition is honored: no
       `PendingCardCostModifiers[]`, no queue, no pending collection, and no
       second representation of an in-flight application is introduced. A
       modifier present in the collection has already been applied, exactly as
       Decision 3 requires for this ADR's own collection.

    d. `SourceIdentity` is per SOURCE, stable, and deterministic — never a
       per-application unique key, a GUID, a clock, an allocation order, or an
       array position. This is the same identity discipline Decision 4 and
       ADR-017 require: two different sources must coexist (their identities
       differ), and a source re-applying must not accumulate (its own identity
       collides). A source whose condition is re-evaluated continuously
       therefore REPLACES/REFRESHES its one entry rather than appending a
       second (TASK-134 D6) — Emergency Core holds exactly one entry however
       often RELIC_RULES.md §6 note 2 re-evaluates.

    e. Lifetime is `Battle` (TASK-134 D3/D7), matching RELIC_RULES.md §8.3's
       allowed combination for `CardCost`. The collection is NOT Turn-based:
       there is NO Turn countdown, NO step 19a participation, NO timeout, and
       NO cleanup pass. It is not carried into a later battle — a new battle
       is a new `BattleState`. Removal is source-specific and is driven by the
       source's own documented condition (Emergency Core: the §6 note 2
       reversion when HP rises back above 30%). GAME_STATE.md §5.1.3 item 2's
       independence rule is preserved: continuous re-evaluation of the Trigger
       refreshes the applied modifier; it does not accumulate it.

    f. Multiple simultaneously-active `CardCost` modifiers compose by ADDING
       their `CostReductionPercentage` values, with the total reduction CAPPED
       AT 100% (TASK-134 D5). This composition is `CardCost`-SPECIFIC. It
       defines no stacking, scaling, or interaction behavior for `ATK`,
       `Power`, `Crit`, or any other `effectType`, all of which remain
       undefined under RELIC_RULES.md §2.4 item 6 and a future rule change
       (GAME_RULES.md §20). This amendment does not broaden D5.

    g. The cost calculation itself is NOT owned here. CARD_RULES.md §3.6 owns
       it: `TotalReduction = min(sum(CostReductionPercentage), 100)`,
       `RawEffectiveCardCost = CardDefinition.PowerCost × (100 −
       TotalReduction) / 100`, and `EffectiveCardCost =
       truncate(RawEffectiveCardCost)` — truncated TOWARD ZERO, an integer,
       with NO minimum-cost-1 rule. `CardDefinition.PowerCost` remains the
       authored/base value and is never mutated; `EffectiveCardCost` is a
       derived runtime value, not stored state and not a wire member.
       `EffectiveCardCost` is calculated once, before validation, deduction,
       and reporting, and the same value is used by all three.

    h. No PostgreSQL persistence is introduced for it: no table, no column, no
       migration. The Relic CONTENT declaration remains RELIC_RULES.md §8.2's
       structured `EffectDefinition[]`; this collection adds no member to that
       content shape, so TASK-132's content-side scope is unaffected.

    i. No Redis key is added. The collection serializes with `BattleState`
       under the existing round-trip obligation, and rides the existing
       `battle:{battleId}:state` record with its unchanged sliding TTL and
       unchanged `Sequence` compare-and-set (TASK-134 D8).

    j. No new SignalR event or method is introduced (TASK-134 D9).
       `CardCostModifiers[]` is NOT a member of the `BattleStateUpdated`
       payload, following the same "state added is not wire exposure added"
       convention as item 10 and this ADR's sibling collection. In particular
       no `CardCostModifierApplied`, `CardCostModifierExpired`, or
       `CardCostChanged` event exists. `RelicTriggered` keeps its
       `{ type, relicId }` shape (item 10).
```

**Why this warrant an amendment rather than a new ADR.** The change is
architecturally the same class as `ADR-017` — a purpose-built `PetState`
modifier collection with a source-scoped removal key — but it is the
*runtime half of this ADR's own contract*: item 5 deliberately introduced no
battle-state member because the content shape was the subject, and item 12
closes the runtime gap that decision left open for exactly one `effectType`.
Because items 1–11 (the representation contract) are unchanged and only the
previously-open runtime carrier is now decided, amending the existing record
preserves the decision history rather than fragmenting one contract across two
ADRs. This follows `docs/03-decisions/README.md` §8's guidance to add a new ADR
only when a *new* decision is being recorded; here the decision completes this
one.

## Consequences

```text
- RELIC_RULES.md §8 becomes the CANONICAL OWNER of the Relic Trigger/
  Condition/Effect representation. DATABASE.md §1 records only the storage
  consequence and does not restate the contract. This avoids a second source
  of truth (.ai/README.md §6, documentation/documentation-change.md §2).

- The Relic stage (GAME_RULES.md §17 step 11) is now BUILDABLE, but remains
  UNIMPLEMENTED. This ADR unblocks it; it does not build it.

- A schema migration is required before any structured Relic row can be
  stored, and before any resolver can read one. That migration is a separate
  task, and until it lands no provisioned Relic row carries the structured
  shape.

- The `RelicCrit` term COMBAT_RULES.md §2 item 7 names in its EffectiveCrit
  composition now has an authored magnitude for Assassin Eye. The composition
  rule, its cap, and its consumption boundary remain that document's and
  ADR-017's; this decision does not restate or alter them.

- No battle-state member is added by items 1–11. Because no Relic counter is
  introduced (item 5) and no wire member is added (item 10), the *representation*
  decision does not engage GAME_STATE.md's state model or REDIS_STATE.md's
  round-trip contract. **Amendment (Decision 12):** the runtime carrier for the
  `CardCost` effect is now decided and **does** engage both — it adds one
  `PetState` collection, which serializes with `BattleState` under the existing
  round-trip obligation and rides the existing `battle:{battleId}:state` record
  with no new key. This is the only battle-state member this ADR now
  introduces, and it is confined to the `CardCost` effect type.

- The Card contract's *content* is untouched. CardDefinition.EffectDefinition
  and its already-encoded rows are not modified, re-encoded, or re-scoped.
  **Amendment (Decision 12):** the runtime cost calculation this ADR's
  `CardCost` effect feeds is owned by CARD_RULES.md §3.6, which introduces no
  Card content change, no `CardDefinition.PowerCost` mutation, and no new
  column.
```

## Alternatives Considered

- **Reuse `RelicDefinition.EffectDefinition` prose and parse it at runtime.**
  Rejected: parsing prose into an effect type and magnitude is exactly the
  "derive the effect from parsed prose" anti-pattern the Card contract already
  forbids, and it cannot represent a threshold such as "every 3 Matches"
  unambiguously.

- **Give each Relic dedicated hardcoded resolver logic keyed by
  `RelicDefinitionId`.** Rejected: this is the "hardcoded card-specific logic"
  the Card contract explicitly forbids, it makes content authoring a code
  change, and it does not scale to the ~10 Relics MVP_SCOPE.md §1 requires.

- **Add a Relic-owned counter to battle state for `MatchCountAtLeast` /
  `ComboAtLeast`.** Rejected: it is unnecessary. The cumulative Match count
  and the Combo value already exist in the resolution state, so a second
  representation would duplicate authoritative state and require its own
  serialization and round-trip contract. This is why item 5 introduces none.

- **Treat effect lifetime as equivalent to trigger re-evaluation.** Rejected:
  it would silently make an `Immediate` or `NextAttack` Relic fire once per
  battle, contradicting RELIC_RULES.md §1's no-cooldown re-fire default.
  Item 6 separates the two concepts explicitly.

- **Author a `Trigger` for Burning Curse so the row can be provisioned.**
  Rejected: it is the invented-rule action AGENTS.md §7 prohibits, and
  RELIC_RULES.md §6 note 3 already recorded the conflict as reported, not
  resolved. Item 8 preserves that.

- **Resolve the Relic contract by editing R2-7 inside the completed TASK-082.**
  Rejected: completed tasks are immutable (tasks/TASK_LIFECYCLE.md §3). The
  supersession is recorded in the owning documents instead, as TASK-109 did
  for the Card member.

## Related Documents

- `docs/01-game-design/RELIC_RULES.md` §1 (Relic structure, `Reset/Cooldown`
  re-fire default), §2 (equip rules), §3 (closed Trigger list, unchanged), §4
  (deterministic equip-slot trigger order), §5 (anti-infinite-chain rule), §6
  and notes 1–3 (the MVP Relic Reference, the provisioned/deferred row set, and
  the Burning Curse conflict this decision leaves reported), §7 (Events —
  `RelicTriggered`), **§8 (the canonical owner of the contract this ADR
  records)**
- `docs/01-game-design/GAME_RULES.md` §13 (Relic Rules), §16 (Battle Event
  Model), §17 step 11 ("Trigger Relics"), §18 (server authority), §20 (Rule
  Change Policy)
- `docs/01-game-design/COMBAT_RULES.md` §2 items 2/5/7 (Crit roll, modifier
  sources naming Relics, and the canonical `EffectiveCrit` composition that the
  Assassin Eye magnitude feeds), §5 (Burn damage, the modifier Burning Curse
  would affect)
- `docs/01-game-design/CARD_RULES.md` §2/§4.1 (the Card effects and magnitudes
  whose contract shape this decision follows), **§3 and §3.6 (the Card casting
  sequence and the canonical owner of `EffectiveCardCost` — the value Decision
  12's `CardCost` modifier feeds; the composition, the 100% cap, and the
  truncate-toward-zero integer rule are owned there and not restated here)**, §5
  (the Cards-vs-Relic boundary a cost modifier does not blur)
- `docs/01-game-design/RELIC_RULES.md` §2.4 item 6 (the "no stacking behavior"
  disclaimer Decision 12f leaves intact for every `effectType` except
  `CardCost`), §6 note 2 (Emergency Core's continuous re-evaluation and its
  reversion, which Decision 12e's removal rule follows)
- `docs/02-technical/DATABASE.md` §1 (`RelicDefinition` block and the storage
  consequence, the `CardDefinition` contract precedent, the R2-7 supersession
  scope, the `Undetermined` representation), §3 (constraints), §5 item 4
  (content-defined rows)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.23 (`RelicTriggered` shape,
  unchanged), §3.2.24 (`PowerChanged` and its `"relic"` source), §3.2.25 (the
  `effect summary` omission convention), §4 (the `BattleState` projection)
- `docs/02-technical/GAME_EVENTS.md` §2/§3 item 7 (`RelicTriggered` payload and
  its sequencing position)
- `docs/02-technical/GAME_STATE.md` §2.3 (`PetState.EquippedRelics[]`), **§2.3.5
  (`CardCostModifiers[]` element schema, identity, always-present collection, and
  "not a wire member"), §2.3.6 (JSON serialization and round-trip), §5.1.3 (the
  create / replace-or-refresh / remove lifecycle Decision 12 requires)**, §0
  (implemented-state staging)
- `docs/02-technical/REDIS_STATE.md` §2 (active battle state serialization), §4
  (the single write-back and `Sequence` compare-and-set), **§7 item 15
  (`CardCostModifiers[]` adds no key, round-trips under the existing
  obligation, and introduces no new storage contract)**
- `docs/01-game-design/COMBAT_RULES.md` §1.1 (`Power` remains an
  integer-valued resource, which Decision 12g's truncation preserves)
- `docs/00-overview/MVP_SCOPE.md` §1 (Relics IN), `docs/00-overview/ROADMAP.md`
  Phase 2
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` (server
  authority), `ADR-005` (Redis active battle state), `ADR-006` (PostgreSQL
  persistence), `ADR-012` (Relic ownership vs battle-scoped equip),
  `ADR-017` (`NextAttack` Crit modifiers — the consumption boundary the
  `NextAttack` lifetime reuses)
- `docs/03-decisions/README.md` §2 (ADR criteria), §4 (numbering, never
  reused), §8 (known open items — this gap was not listed)
- `tasks/completed/TASK-131-resolve-relic-trigger-and-effect-resolution-contract.md`
  (the Product Owner decision set D1–D11 this ADR records the architectural
  consequence of)
- `tasks/backlog/TASK-134-resolve-cardcostmodifier-authoritative-state-carrier.md`
  (the Product Owner decision set D1–D11 — including the `CardCost` runtime
  state carrier Decision 12 records and the `EffectiveCardCost`
  fractional-value decision CARD_RULES.md §3.6 owns; TASK-134's own record is
  unchanged)
- `tasks/backlog/TASK-132-migrate-relic-structured-condition-and-effectdefinition-storage.md`
  (the content-side structured storage migration; Decision 12h leaves its scope
  unaffected) and `tasks/backlog/TASK-133-implement-server-authoritative-relic-trigger-and-effect-resolution.md`
  (the implementation this amendment unblocks)
- `tasks/completed/TASK-108-resolve-card-effect-resolution-contract.md` and
  `tasks/completed/TASK-109-implement-structured-effectdefinition-contract.md`
  (the precedent contract decision, the `jsonb` implementation, and the
  Relic-scoped boundary this decision closes)
- `tasks/completed/TASK-082-resolve-pet-card-relic-content-provisioning-contract.md`
  (decision A / R2-8 provisioned row set; R2-7, superseded here)
- `tasks/completed/TASK-027-relic-ownership-and-battle-start-snapshot.md`
  (confirmed no Relic trigger engine existed at that point)
