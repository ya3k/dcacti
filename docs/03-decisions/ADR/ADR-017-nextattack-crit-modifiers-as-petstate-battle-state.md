# ADR-017: `NextAttack` Crit Modifiers as Authoritative `PetState` Battle State

**Status:** Accepted
**Date:** 2026-09-28

## Context

`DATABASE.md` §3 item 1 stores an Iron Fang `Crit` element as
`scope = "NextAttack"` and states explicitly that the member "author[s] no
gameplay": the *rule* the scope names is owned by `CARD_RULES.md` §4.1
("increase Crit chance by 10 percentage points for the next attack only"),
with `PASSIVE_RULES.md` §7/§8 carrying the same `NextAttack` scope for Bạch
Hổ's Passive. Neither document defined how such a modifier is represented in
authoritative battle state, when it stops applying, which attack consumes it,
or how it composes with the other Crit sources `COMBAT_RULES.md` §3.3 item 5
names (Relics, Pet Passives, Cards).

The gap was a hard blocker: TASK-115 (implement Iron Fang's Crit and Bạch
Hổ's Passive Crit) could not proceed, and the implementation that existed
`BattleStateService.cs` lines 1179–1186 — consumed the modifier by comparing
the composed stat to a configuration constant:

```text
if (resolved.PetState.Crit != PetState.DefaultCrit)
    → PetState.Crit = PetState.DefaultCrit
```

That is source-blind (it cannot distinguish Iron Fang from Bạch Hổ from a
Relic from a future base-value change), it couples "no modifier active" to
"the stat equals the MVP default" — which `GAME_STATE.md` §2.3 states is
configuration and "not a permanent invariant" — and it guesses a consumption
site. `AGENTS.md` §7 forbids shipping a guessed version of a missing rule.

The existing state model could not carry the concept as-is:

```text
GAME_STATE.md §2.3.1 item 3   `StatusEffect` duration is a strict dichotomy:
                              `RemainingTurns` XOR `ExpiryCondition` —
                              "never both and never neither". A NextAttack
                              modifier is neither: it is a stat modification
                              (so `BuffDebuff`) whose trigger is not Shield
                              depletion and whose lifetime is not a Turn
                              countdown.

GAME_STATE.md §2.3.1 item 6   At most one instance per effect identity per
                              entity, so two simultaneous NextAttack Crit
                              sources (Iron Fang and Bạch Hổ) could not both
                              be held under one `Id`.

GAME_STATE.md §2.3.3          Explicitly forbids the escape hatch: "No
                              `PendingStatusEffects[]`, no queued/pending
                              application collection, and no second
                              representation of an in-flight application".

GAME_STATE.md §5.1.1 item 7   The step 19a pass "must not invent a duration
                              for it"; no attack-consumption path is defined
                              for any instance.
```

The Product Owner resolved the gameplay contract in TASK-116 (D-1–D-8),
whose bindable statement is TASK-116's "Resulting Contract" C-1–C-10. D-1
introduces `PetState.NextAttackCritModifiers[]` — a **new authoritative
battle-state concept**, which `AGENTS.md` §18 lists as requiring an ADR
("changing … the battle-state model"). `docs/03-decisions/README.md` §8 was
checked: the NextAttack representation gap was **not** listed there. This ADR
records that architectural decision and the state-model contract it requires.
It authors no gameplay rule and changes no gameplay decision.

## Decision

```text
1.  `PetState.NextAttackCritModifiers[]` is a dedicated, source-specific
    collection on the authoritative `PetState`, member of `BattleState` like
    every other `PetState` member.

2.  It is SEPARATE from `StatusEffects[]`. It is not a `StatusEffect`
    instance, it does not use `RemainingTurns`, and it does not use
    `ExpiryCondition`/Shield depletion. It therefore does not engage
    `GAME_STATE.md` §2.3.1 item 3's duration-model dichotomy, does not widen
    that rule, and does not relax item 6's one-instance-per-identity rule.

3.  `GAME_STATE.md` §2.3.3's prohibition is honored: no
    `PendingStatusEffects[]`, no queue, and no second representation of an
    in-flight application is introduced. The collection holds modifiers that
    are already applied, not pending ones.

4.  Each element carries a stable source/instance identity and a Crit
    contribution in percentage points. No other field is required.

5.  Lifetime: a modifier remains active until the owner's next qualifying
    attack consumes it. It is NOT TurnBased, has NO Turn expiry, NO timeout,
    and NO automatic cleanup. An unconsumed modifier persists across Turns
    indefinitely by design.

6.  A qualifying attack is an explicit owner attack action that enters the
    Damage Pipeline. A raw damage instance is not itself an attack for this
    purpose; a non-damaging action does not consume; and if one qualifying
    attack produces multiple damage instances, the modifier is consumed once
    at the first qualifying instance and not again by later instances of the
    same attack.

7.  `PetState.Crit` remains the permanent/base Crit value and is never
    overwritten by a temporary modifier. Effective Crit is computed for the
    current Damage Pipeline execution.

8.  Crit contributions are additive. Effective Crit is capped at 100
    percentage points. The existing bounded Crit RNG procedure is unchanged:
    one bounded selection V ∈ [0,100), Crit succeeds iff V < EffectiveCrit.

9.  Consumption removes only the source-specific modifiers the qualifying
    attack consumed. It never resets `PetState.Crit`, never removes Passive
    Crit or Relic Crit, and never removes an unrelated temporary Crit source.

10. Multiple applicable NextAttack Crit modifiers stack additively, are not
    replaced by one another, and are consumed together by the qualifying
    attack.

11. `DefaultCrit` is initialization/default-state data only. It is never a
    runtime reset mechanism, and
    `PetState.Crit = PetState.DefaultCrit` must never be used to consume a
    modifier.

12. No PostgreSQL persistence, no new Redis key, no new SignalR method, no
    new Battle Event, and no second RNG stream is introduced. The collection
    serializes with `BattleState` under the existing round-trip obligation
    and is not a wire member.
```

## State Model

```text
PetState                                    (GAME_STATE.md §2.3)
├── Crit                                    permanent/base percentage points
└── NextAttackCritModifiers[]               (this ADR; empty when none
    └── element                             active, always present)
        ├── source identity
        └── Crit contribution (percentage points)
```

Exactly two members per element. The identity is what makes source-specific
removal (Decision 9) possible; the contribution is what the composition
(Decision 8) sums. No duration, no Turn counter, no expiry label, no
consumed flag, no priority, no timestamp, no ordering index, and no
magnitude-of-anything-else is stored — none is read by any rule, and adding
one would be the speculative field `AGENTS.md` §9 forbids.

The identity is **source-scoped stable identity**, not a per-cast unique
instance key. This is the minimum that satisfies both requirements at once:

```text
two different sources must coexist   → their identities differ
a source re-applying must not stack  → its own identity collides
```

So one element exists per distinct source identity. Iron Fang and Bạch Hổ
hold two elements with two different identities. Casting Iron Fang twice
before any qualifying attack holds one Iron Fang element, matching
`CARD_RULES.md` §4.1's "for the next attack only" reading and
`COMBAT_RULES.md` §5.2 item 2's MVP refresh-default principle ("refresh, do
not stack magnitude") rather than authoring a new stacking rule. The concrete
identity spelling is not fixed by this ADR: the contract requires
determinism and stability, and leaves the exact token to the owning rule and
its implementation. What the architecture decides is that the identity is
**per source**, that it is **stable**, and that it is **deterministic** —
never derived from a clock, a GUID, an allocation order, or an array
position.

The collection is always present and empty when no modifier is active — the
same always-present-collection convention `GAME_STATE.md` §2.3.2 item 1 fixes
for `StatusEffects[]`. Absence of the collection is not a representable
state.

## Ownership

```text
Authoritative state          BattleState.PetState.NextAttackCritModifiers[]
                             (server-owned — GAME_RULES.md §18, ADR-001)

State contract (shape,       GAME_STATE.md  (this ADR's implementation-half
lifetime, consumption,                       owner; see "Related Documents")
ordering, serialization)

Crit composition, cap,       COMBAT_RULES.md §3.3
and the qualifying-attack
boundary as a gameplay rule

Iron Fang's contribution     CARD_RULES.md §4.1
Bạch Hổ's contribution       PASSIVE_RULES.md §7/§8
Relic Crit contribution      RELIC_RULES.md §5

Storage (atomicity, CAS)     REDIS_STATE.md §4 — unchanged; the collection
                             rides the existing single write-back
```

Creating a modifier belongs to whichever resolution step owns the source:
a Card or Pet Skill at `GAME_RULES.md` §17 step 14 ("Resolve Player
Effects"), a Pet Passive at step 10 ("Charge Passive"). Both are existing
resolution sites; this decision adds no step to `GAME_RULES.md` §17. The
owner of the collection is the entity the modifier applies to — the active
`PetState`. The Boss carries no such collection: no documented Boss source
produces a NextAttack Crit modifier, and `BossState` (`GAME_STATE.md` §2.4)
is unchanged.

## Lifecycle

```text
Created              the source resolves (Card/Pet Skill at §17 step 14,
                     Pet Passive at §17 step 10) and adds one element
                     carrying its identity and contribution
        ↓
Active across Turns  the modifier survives any number of Turns, any number
                     of Swaps, and any number of non-damaging actions; the
                     step 19a pass does not touch it
        ↓
Qualifying attack    an explicit owner attack action enters the Damage
                     Pipeline; Effective Crit is composed for that
                     execution
        ↓
Consumed             at the first qualifying damage instance of that
                     attack, the consumed source-specific elements are
                     removed; later instances of the same attack do not
                     consume again
```

Two lifecycle properties are deliberate and must not be "fixed" by an
implementation:

1. **No Turn-based expiry.** `GAME_STATE.md` §5.1.1 item 2's single
   decrement-per-Turn rule governs `StatusEffects[]` instances that use the
   Turn countdown. This collection is not one of them, and §5.1.1 item 7
   already states that the step 19a pass "must not invent a duration for" a
   non-Turn-based instance. An unconsumed modifier legitimately persists
   across Turns.
2. **No cleanup of any kind.** There is no end-of-turn removal, no timeout,
   no expiry on battle state change, and no periodic sweep. The only event
   that removes an element is consumption by a qualifying attack.

Ordering within the collection is not semantic, matching
`GAME_STATE.md` §2.3.1 item 10's convention for `StatusEffects[]`: addition
and consumption operate on identities, and the resulting composed value does
not depend on array order. Consumption is a set operation over identities,
so it is order-independent by construction.

## Crit Composition

```text
EffectiveCrit =
      BaseCrit                                  PetState.Crit (permanent)
    + PassiveCrit                               applicable and active
    + RelicCrit                                 applicable and active
    + sum of applicable NextAttackCritModifiers[]
        ↓
    capped at 100 percentage points
        ↓
    existing bounded Crit roll, unchanged
```

1. **`PetState.Crit` is the permanent/base value and is never overwritten.**
   A temporary modifier is never written into it. This is the property that
   makes Decision 9's source-specific removal possible at all: nothing has to
   be "un-applied", because nothing was applied to the base.
2. **Effective Crit is computed for the current Damage Pipeline execution.**
   It is a value used within one pipeline run, not a new stored member. No
   `EffectiveCrit` field, and no second Crit representation, is introduced.
3. **Contributions are additive percentage points.** That unit is the one
   `CARD_RULES.md` §4.1 and `DATABASE.md` §3 item 1 already use for a Crit
   element's `value`.
4. **The cap is 100 percentage points** and it is a **new authored value**
   recorded by TASK-116 D-4.4. It is not a pre-existing documented cap: no
   Crit range existed in `docs/` before this decision (`COMBAT_RULES.md`
   §1.1 gave `Crit` a default and no range; the `100` in the decision derives
   from the roll bound, not from a documented stat ceiling). It is recorded
   here and owned by `COMBAT_RULES.md` §1.1/§3.3 so no later reader mistakes
   it for a restatement of existing documentation.
5. **The Crit stat range and the Crit RNG roll are separate values and must
   not be inferred from each other.**

   ```text
   Crit stat (composed)      0–100 percentage points   (a cap)
   Crit RNG roll             V ∈ [0, 100)              (a draw bound)
   ```

   `V < EffectiveCrit` is the unchanged success condition
   (`COMBAT_RULES.md` §3.3 item 2). No second roll, no second stream, and no
   second generator is introduced: the draw continues to consume the single
   server-seeded `RngState` (`ADR-009`, `GAME_STATE.md` §2.6.2, `TDD.md` §6).
6. **Crit result representation is unchanged.** The outcome continues to be
   carried inside step 4's combined `otherModifiers` multiplier
   (`COMBAT_RULES.md` §3.3 item 6, `SIGNALR_PROTOCOL.md` §3.2.13). No Crit
   event, no state property, and no wire member is added.

## Consumption Boundary

```text
qualifying attack        an explicit owner attack action that enters the
                         Damage Pipeline
non-damaging action      does NOT consume
raw damage instance      is NOT itself an attack
multiple instances from  belong to the SAME attack; consumed at the first
one qualifying attack    qualifying instance, exactly once
Burn/DoT tick            is not the owner's qualifying attack action — does
                         not consume
Boss attack / Boss Skill is not the owner's qualifying attack action — does
                         not consume
```

The two consequences that follow, and that implementation must not
"reinterpret":

1. Because a Burn tick and a Boss attack are not the owner's qualifying
   attack action, they do not consume a modifier — **even though**
   `COMBAT_RULES.md` §3.3 item 4 makes every damage instance Crit-eligible,
   so they *do* participate in Effective Crit composition while a modifier is
   active. Consumption and eligibility are different questions: eligibility
   decides whether the Crit roll reads Effective Crit; consumption decides
   whether the modifier survives the instance.
2. Consumption is expressed as a removal of element identities from the
   collection. It is never expressed as an arithmetic inverse, never as a
   reset to a default, and never as a recomputation from the base.

## Serialization Impact

| Surface | Impact |
|---|---|
| `BattleState` JSON | The collection serializes as part of `PetState`, under the same single write-back (`GAME_STATE.md` §5.1). No additional Redis-only field, matching `REDIS_STATE.md` §2 item 1. |
| Round trip | Lossless and order-preserving, under the existing obligation (`REDIS_STATE.md` §7 item 9). An empty collection round-trips as an empty collection, never as omission or `null`. |
| Redis | No new key, no new hash field, no second record. The collection rides `battle:{battleId}:state` and the existing `Sequence` compare-and-set (`REDIS_STATE.md` §1, §4). |
| SignalR projection | **No change.** The collection is authoritative state, not a wire member. State added is not wire exposure added (`SIGNALR_PROTOCOL.md` §4 item 4) — the same position `StatusEffects[]` holds (`GAME_STATE.md` §2.3.1's "Not a wire member" note). Delivering it would be a protocol change owned by its own task. |
| Client runtime state | **No change.** The client neither receives, computes, nor infers Crit modifiers (`AGENTS.md` §10, `GAME_RULES.md` §18, ADR-001). |
| PostgreSQL | **No change.** This is active battle runtime state; no table, column, or migration is introduced. |

Exact JSON member names and casing remain the serializer's implementation
detail (`GAME_STATE.md` §2.1.7 item 4, `SIGNALR_PROTOCOL.md` §8 item 1). What
is owned here is the existence, type, and meaning of the two members.

## Alternatives Considered

### Option A — Represent the modifier as a `StatusEffect`

Hold it as a `StatusEffect` instance with `Type = "BuffDebuff"` and
`TargetStat = "Crit"`.

Rejected. `GAME_STATE.md` §2.3.1 item 3 makes the duration model a strict
exclusive dichotomy, and a NextAttack modifier is neither branch: it is not
Turn-based and its trigger is not Shield depletion. Holding it there would
require either widening item 3's dichotomy with a third lifetime model or
relaxing item 6's one-instance-per-identity rule so that Iron Fang and Bạch
Hổ could both be held — both of which re-open an already-authored,
cross-referenced state contract (`COMBAT_RULES.md` §5.2 item 1, §5.3.2,
`GAME_STATE.md` §5.1.1 item 7) for a concept that is not a status effect.
Item 3 is also not the only obstacle: §2.3.1's model has no
attack-consumption path at all, so the representation would still be missing
its whole lifecycle.

### Option B — A pending/queued collection

Hold the modifier in a `PendingStatusEffects[]`-style queue until an attack
consumes it.

Rejected outright: `GAME_STATE.md` §2.3.3 forbids exactly this ("No
`PendingStatusEffects[]`, no queued/pending application collection, and no
second representation of an in-flight application"). Beyond being forbidden,
it is also the wrong shape: a modifier that has been granted is *applied
state*, not an in-flight application awaiting write-back.

### Option C — Keep mutating `PetState.Crit` directly

Continue the existing `newCrit += critAmount` / reset-to-`DefaultCrit`
approach.

Rejected. It is source-blind by construction: the stat cannot distinguish
Iron Fang's contribution from Bạch Hổ's, from a Relic's, or from the base
value, so source-specific removal (Decision 9) is unrepresentable. It also
makes consumption depend on a configuration constant — `GAME_STATE.md` §2.3
states the combat-stat defaults are configuration and "not permanent
invariants", so a balance retune would silently change the logic's meaning.
And it overwrites the base, which the Product Owner decision D-4.1 forbids.

### Option D — A parallel Crit-specific representation

Introduce a `CritQueue`, `CritEvents[]`, `NextAttackQueue`, or a per-source
Crit registry.

Rejected. Each is a second in-flight state representation of the same
concept, which `GAME_STATE.md` §0 item 5 and §2.3.3 forbid, and each is
larger than the problem: the concept needs one identity and one contribution
per active source. A separate event or queue representation would also
duplicate state the collection already holds.

### Option E — Extend `StatusEffects[]` with a NextAttack type

Add a fourth `StatusEffectType` for attack-consumed modifiers.

Rejected. `GAME_STATE.md` §2.3.3 states that introducing another Status
Effect type "is a gameplay decision owned by `COMBAT_RULES.md`, not by this
state", and no gameplay decision authorizes a new Status Effect type —
TASK-116 resolved the representation of a Crit modifier, not the addition of
a status effect. It would also put a non-status concept into the status
collection purely for convenience, which `AGENTS.md` §12 warns against.

## Why

The concept's lifetime is its whole identity, and that lifetime is the one
thing the existing status model structurally cannot express. A Status Effect
lasts a number of Turns or until a named trigger fires; a NextAttack Crit
modifier lasts until an *attack* consumes it, may outlive any number of
Turns, and is consumed by an event that is not the end of a Turn and not the
depletion of a pool. Forcing it into `StatusEffects[]` would either corrupt
the meaning of that collection's duration rules or require re-opening them —
and it would still leave consumption undefined.

A dedicated, non-Turn-based collection states the concept directly: the
permanent Crit value stays permanent, each temporary source is individually
identified and therefore individually removable, and composition is a pure
sum over the elements that are active. That is also what makes the additive
and cap rules of this ADR expressible without touching the base value, and
what lets an implementation stop comparing the stat against a default
constant to guess whether a modifier is active.

Separating the collection from `StatusEffects[]` is likewise the smaller
choice, not the larger one. `StatusEffects[]` is a model with a duration
dichotomy, a refresh-in-place uniqueness rule, a fixed step-19a consumption
pass, and a deterministic pass order — every one of which is irrelevant to
this concept. Reusing it would import four rules that do not apply and
require exempting each.

## Consequences

### Positive

- Iron Fang's Crit and Bạch Hổ's Passive Crit become implementable
  deterministically: creation, composition, and consumption each have one
  documented site and one documented shape.
- The base Crit value is structurally protected. No operation in this design
  can overwrite `PetState.Crit`, because composition reads the base and never
  writes it — which removes the class of defect the previous
  reset-to-`DefaultCrit` implementation contained.
- Source-specific removal is first-class: consumption is a set operation over
  identities, so removing one source cannot disturb another by construction,
  rather than by a comparison against a default.
- The existing Crit pipeline is untouched. Pipeline position (step 4), roll
  procedure, comparison operator, multiplier, result representation, and the
  single `RngState` stream are all unchanged, so no existing Crit behavior is
  re-decided.
- No boundary is crossed: no new Redis key, no new PostgreSQL schema, no new
  SignalR method, no new Battle Event, and no client-visible change.

### Negative

- `BattleState` gains one `PetState` member, so the Domain state type and its
  serializer must add it, and the existing implementation must be corrected:
  the `DefaultCrit` reset must be removed and `CardCastExecutor`'s direct
  `Crit` write replaced by an element addition. That alignment is a separate
  implementation task (TASK-115), not part of this decision — recorded here
  as an expected divergence until that task lands, per the ADR-016 precedent.
- An unconsumed modifier persists indefinitely across Turns. This is a
  deliberate consequence of the resolved gameplay decision, not an oversight,
  and it means a battle can legitimately carry a stale-looking element for
  many Turns. Implementation must not add Turn-based cleanup to "prevent"
  this; doing so would silently contradict the contract.
- The collection is a new named concept in `GAME_STATE.md`, so future
  Crit-modifying sources must participate in it rather than inventing their
  own path. That is the intended constraint, but it does mean a source that
  is not a Card, a Pet Skill, or a Pet Passive needs an explicit decision
  about how it creates an element.
- Crit's 100 percentage-point cap is newly authored by this decision set. It
  is recorded as new so no reader treats it as pre-existing documentation,
  and it must not be conflated with the Crit roll's `[0, 100)` bound.

### Trade-offs

- Storing an identity per element rather than a single aggregate Crit bonus
  costs one member per active modifier. The alternative — an aggregate — is
  exactly the source-blindness this decision exists to remove, so the member
  is the minimum that satisfies source-specific removal rather than a
  convenience field.
- Not delivering the collection on the wire keeps the protocol frozen, at the
  cost of the client being unable to display an active Crit modifier without
  a future, separately-decided protocol change. That is the same position
  `StatusEffects[]` already holds and is consistent with `GAME_RULES.md` §18.
- Source-scoped identity (rather than a per-cast unique key) keeps two sources
  independent while making a repeat application refresh rather than stack.
  This reuses the MVP refresh default instead of authoring a new stacking
  rule; the cost is that two casts of the same Card before a qualifying
  attack carry one modifier, not two.

## Related Documents

- `docs/02-technical/GAME_STATE.md` §2.3 (`PetState` member), §2.3.x
  (`NextAttackCritModifiers[]` schema, identity, always-present collection,
  and "not a wire member"), §5.1.1 (lifecycle: not a step-19a participant),
  §5.1 (single write-back), §2.3.1/§2.3.3 (`StatusEffects[]` boundary this
  collection is deliberately outside of)
- `docs/01-game-design/COMBAT_RULES.md` §1.1 (Crit row: range/cap), §3.3
  items 1–6 (pipeline position, roll procedure, scope, source composition,
  result representation), §3.1 (step 4 ordering), §5.2/§5.3 (the Status
  Effect duration model this collection does not use)
- `docs/01-game-design/CARD_RULES.md` §4.1 (Iron Fang's Crit contribution
  and its independence from Bạch Hổ's Passive)
- `docs/01-game-design/PASSIVE_RULES.md` §7, §8 (Bạch Hổ's Passive, the
  second `NextAttack` Crit source)
- `docs/01-game-design/RELIC_RULES.md` §5 (Assassin Eye — the Relic Crit
  source that composes with, and is untouched by, this collection)
- `docs/01-game-design/GAME_RULES.md` §17 (steps 10, 14, 19a — creation and
  non-consumption sites), §16 (canonical event list — no Crit event), §18
  (server authority)
- `docs/02-technical/REDIS_STATE.md` §2 (shape/round trip), §4 (single
  write-back and `Sequence` compare-and-set), §7 item 9 (round-trip
  obligation)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.13 (`otherModifiers` as the
  sole step-4 carrier), §4 items 4/12 (state added is not wire exposure
  added), §4.2/§4.3 (current payload members)
- `docs/02-technical/DATABASE.md` §1, §3 item 1 (`scope = "NextAttack"` as a
  storage token; no persistence is added by this decision)
- `docs/02-technical/TDD.md` §6 (single server-seeded PRNG)
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` (server
  authority), `ADR-005` (Redis active battle state), `ADR-009` (deterministic
  PRNG), `ADR-010` (precedent: a missing `BattleState` member resolved
  ADR-first), `ADR-011` (`PetState` as the combat runtime state),
  `ADR-014` (precedent: `BattleState` member added, not a wire member)
- `tasks/backlog/TASK-116-resolve-nextattack-crit-modifier-state-and-consumption-contract.md`
  (the Product Owner decision set this ADR records the architecture
  consequence of — D-1–D-8, C-1–C-10)
- `tasks/backlog/TASK-115-implement-server-authoritative-petskillcast-crit-and-burn.md`
  (the implementation task this decision unblocks)
