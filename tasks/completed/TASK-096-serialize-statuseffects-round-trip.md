# TASK-096 — Serialize `StatusEffects[]` in the BattleState Runtime Round Trip

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, or schemas.

  THIS TASK IMPLEMENTS AN ALREADY-DEFINED CONTRACT. It authors no rule.
  The authoritative contract is:
    GAME_STATE.md §2.3.2   the JSON shape and the round-trip obligation
    GAME_STATE.md §2.3.1   the instance schema being mapped
    GAME_STATE.md §5.1.1   the lifecycle those instances carry
    REDIS_STATE.md §7 item 9   the round-trip obligation it discharges
    §2 item 1                  "matches GAME_STATE.md §2 exactly"
  All of it is settled by TASK-093 (contract) and TASK-095 (Domain state).
  Neither is modified by this task.

  BOUNDARY: the mapping layer only — BattleState → JSON → BattleState.
  TASK-095 deferred serialization to its own task and left the serializer
  untouched. This is that task, and it is the smallest complete one:
  GAME_STATE.md §2.3.2 item 7 states the collection is written in the SAME
  single post-resolution write-back as the rest of BattleState through the
  existing `battle:{battleId}:state` record, introducing no Redis key, no
  Redis-only field, and no second record — so no Redis task is required, and
  REDIS_STATE.md needs no edit. SignalR is excluded because §2.3.1's wire note
  and SIGNALR_PROTOCOL.md §4.2 item 2 state StatusEffects[] is NOT a wire
  member; adding one would be a protocol change owned by its own task.
-->

---

## Metadata

```text
Task ID:           TASK-096
Type:              FEATURE
Status:            DONE
Risk:              MEDIUM (TASK_TYPES.md §4 — FEATURE baseline MEDIUM; raised toward
                   HIGH because it touches the battle-state persistence record. No
                   gameplay value is computed and no rule changes: it is a pure
                   field-copy mapping of an already-implemented state model.)
Priority:          HIGH (TASK-095's Domain state is authoritative but is silently
                   dropped by every round trip today — a persisted battle that
                   carries Burn/Root/Shield/Stun loses them on reload. This is the
                   last link TASK-095 left open.)
Primary Agent:     realtime (owns the Redis/serialization boundary —
                   TASK_TYPES.md §5: SignalR/Redis → Realtime; TASK-029, the
                   existing serializer task, was also Realtime-led)
Supporting Agents: backend, testing, review
Workflow:          development/feature.md
Skills:            backend/persistence-analysis,
                   discovery/impact-analysis,
                   testing/test-scenario-generation,
                   quality/architecture-conformance,
                   gameplay/authority-determinism-audit
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-095 (DONE — delivered StatusEffect, PetState/
                   BossState.ActiveStatusEffects, StatusEffectLifecycle;
                   consumed as settled input, not modified),
                   TASK-093 (DONE — authored the §2.3.2 serialization contract),
                   TASK-029 (DONE — the serializer this task extends),
                   TASK-040 (DONE — the Redis record that carries it)
Blocks:            Any future StatusEffects[] wire-delivery (SignalR) task;
                   Boss Skill secondary-effect producers (they rely on the
                   collection surviving a persisted reload)
Estimate:          Normal (one JSON DTO member pair, two mapping calls, two
                   factory branches, round-trip tests; no new rule, no new field)
```

**Type classification note.** `FEATURE`, not `GAMEPLAY-CHANGE` and not `REFACTOR`:
the contract already exists in `docs/` (`GAME_STATE.md` §2.3.1/§2.3.2) and is
already implemented in Domain (TASK-095) — the task is to build the documented
mapping for it. `TASK_TYPES.md` §2 requires a GAMEPLAY-CHANGE only when the rule
itself must change or a new undocumented mechanic is authorized, neither of which
applies (`AGENTS.md` §7 does not fire). It is not `REFACTOR` because the mapping
gains documented members and a new round-trip obligation — observable behavior,
not a behavior-preserving restructure.

**Boundary note.** This task deliberately stops at the runtime JSON mapping. It
adds no gameplay rule, no producer that *creates* an effect, no wire member, no
Redis key/TTL/concurrency change, no PostgreSQL schema, and no client code.

---

## Objective

Map the already-implemented `PetState.ActiveStatusEffects` and
`BossState.ActiveStatusEffects` collections (`GAME_STATE.md` §2.3.1, implemented
by TASK-095) through the existing `BattleStateSerializer` / `BattleStateJson`
round trip as the `statusEffects` member, so that a serialized `BattleState`
restores the same instances, member values, optional-member presence/absence, and
element order — exactly as `GAME_STATE.md` §2.3.2 items 1–7 require and
`REDIS_STATE.md` §7 item 9 obliges — without changing any other `BattleState`
member, without adding a Redis structure, and without adding a wire member.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Status Effects are IN scope (Combat block:
  Burn, Shield, Buff/Debuff; `MVP_SCOPE.md` line 80)
- `docs/02-technical/GAME_STATE.md` §2.3.2 — **the serialization contract this
  task implements**: item 1 (`statusEffects` member on both `PetState` and
  `BossState`, empty array when no effect is active, never omitted, never
  `null`), item 3 (the exact per-element member set and types), item 4
  (`remainingTurns` is a plain integer; no `duration`/`elapsedTurns`/
  `appliedTurn`/`refreshedAt` member), item 5 (lossless and order-preserving
  round trip; a dropped instance, a renumbered `remainingTurns`, an absent
  optional member materialized as `null`, or a reordering is a **defect**),
  item 6 (order is preserved for fidelity, not semantics), item 7 (no Redis-only
  field; written in the same single post-resolution write-back)
- `docs/02-technical/GAME_STATE.md` §2.3.1 — the element schema being mapped
  (items 1–12), including item 6 (at most one instance per `Id`), item 7 (absence,
  never `null`, never a sentinel), item 8 (zero is never observable), item 10
  (ordering is not semantic), item 11 (step 19a order is not array order)
- `docs/02-technical/GAME_STATE.md` §2.3 / §2.4 — the `statusEffects` tree entry
  on both entities; §2.4.1 (same element schema, identical lifecycle)
- `docs/02-technical/GAME_STATE.md` §2.3.1 "Not a wire member" / "Not a
  Redis-only concern" notes — the two boundary statements this task must not
  cross
- `docs/02-technical/GAME_STATE.md` §2.1.7 items 4 and 5 — exact JSON member
  names and casing are the serializer's implementation detail; the round-trip
  obligation's form
- `docs/02-technical/GAME_STATE.md` §0 item 4, item 5 — a field is "not yet
  implemented", never "not required"; no parallel representation
- `docs/02-technical/GAME_STATE.md` §5.1 — the single post-resolution write-back
  the collection travels in
- `docs/02-technical/GAME_STATE.md` §5.1.1 — the lifecycle whose results this
  mapping must carry unchanged (items 1–12)
- `docs/02-technical/REDIS_STATE.md` §2 item 1 — the record "matches the shape in
  `GAME_STATE.md` §2 exactly — no additional Redis-only fields"; §2 item 2;
  §7 item 9 (the round-trip obligation for the counters, and the precedent this
  collection extends), §7 item 10 (Special Gem content change: "a **content**
  change to the record, not a **structure** change to the store")
- `docs/02-technical/REDIS_STATE.md` §1, §3, §4 — key structure, TTL, and
  compare-and-set, all **unchanged** by this task
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4 item 4, §4.2 item 2 — the
  `playerState`/`petState` payload member set is fixed and **does not include**
  `StatusEffects`; §8 item 1 — member spelling on a surface is that surface's
  contract, not a shared schema
- `docs/02-technical/ARCHITECTURE.md` §2.1 — Domain has zero dependencies on
  Redis/PostgreSQL/SignalR/ASP.NET Core; the serializer lives in Domain and
  references none of them
- `docs/03-decisions/ADR/ADR-005` — Redis is the active battle state store;
  §2.3.2 item 7 confirms no new key is introduced
- `docs/03-decisions/ADR/ADR-001` — server authority
- `tasks/completed/TASK-095-implement-statuseffect-domain-state-and-step-19a-lifecycle.md`
  — the Domain state this mapping carries (read-only; not modified). Its
  "Explicitly Not Tested Here" section names this task's obligation.
- `tasks/completed/TASK-093-resolve-status-effects-battle-state-contract.md` §12
  — the authored contract (read-only; not modified)
- `tasks/completed/TASK-029-battlestate-runtime-serialization-mapping.md` — the
  serializer this task extends, and the precedent that the mapping is delivered
  with **no Redis key writes** (read-only; not modified)

**ADR check:** no ADR addresses Status Effect serialization. ADR-001 (server
authority) is the governing one and is satisfied — this is a server-side Domain
mapping. ADR-005 (Redis as the active-state store) is satisfied unchanged: §2.3.2
item 7 introduces no new key.

---

## Current State

The `StatusEffects[]` collection is **implemented in Domain but silently dropped
by every round trip**:

```text
GAME_STATE.md §2.3.1/§2.3.2/§2.3.3/§5.1.1   contract authored by TASK-093
COMBAT_RULES.md §5.3                        canonical duration rule (DR1–DR6)
GAME_RULES.md §17 step 19a                  lifecycle position; implemented

src/backend/GameServer.Domain/Battle/
  StatusEffect.cs            IMPLEMENTED (TASK-095) — the 7-member instance
                             schema; TurnBased / TriggerBased factories make
                             RemainingTurns xor ExpiryCondition unrepresentable
                             if violated
  StatusEffectType.cs        IMPLEMENTED — StatusEffectType, StatusEffectSource
  StatusEffectLifecycle.cs   IMPLEMENTED — Apply / ConsumeAtStep19a / EffectsEqual
  PetState.cs                IMPLEMENTED — ActiveStatusEffects (never null, [] default)
  BossState.cs               IMPLEMENTED — ActiveStatusEffects (never null, [] default)

src/backend/GameServer.Domain/Battle/Serialization/
  BattleStateJson.cs         PetStateJson and BossStateJson have NO statusEffects
                             member; both XML docs still say StatusEffects[]
                             "is not yet implemented and is therefore not a member"
  BattleStateSerializer.cs   ToPetStateJson / ToBossStateJson / FromPetStateJson /
                             FromBossStateJson map exactly the implemented members
                             and carry no StatusEffects element mapping

tests/backend/GameServer.Domain.Tests/
  BattleStateSerializationTests.cs   Serialization_ShouldNotWriteStatusEffects
                                     ThatAreNotYetImplemented asserts the member
                                     is ABSENT on both petState and bossState
```

`StatusEffectLifecycle` states its own boundary explicitly: "It does not
serialize. `StatusEffects[]` is not a wire member … and the runtime record's round
trip is a separate task's obligation (§2.3.2)." That separate task is this one.

Consequence today: `BattleStateSerializer.Deserialize(BattleStateSerializer.
Serialize(state))` rebuilds both collections as **empty**, so a persisted battle
loses every active Burn / Root / Shield / Stun instance on reload. TASK-095 had to
work around exactly this in `BattleStateSerializationLifecycleTests`, where whole
`BossState` comparisons were changed to a structural `StatusEffectsEqual` comparison
because the record's array member compared by reference against a rebuilt `[]`.
This is the documented staging position being closed, not a regression introduced
here.

---

## Scope

### In Scope

1. **A `statusEffects` member on both `PetStateJson` and `BossStateJson`**
   (`GAME_STATE.md` §2.3.2 item 1) — the same element shape on both, no
   boss-specific DTO variant (§2.3.1 preamble, §2.4.1).
2. **A per-element JSON projection matching §2.3.2 item 3 exactly**, with the
   member set and presence rules of §2.3.1 items 3, 5, and 7:
   - `id`, `type`, `source`, `magnitude` — always written
   - `targetStat` — written iff `type` is `BuffDebuff`
   - `remainingTurns` — written iff the instance uses the Turn countdown
   - `expiryCondition` — written iff it does not
   - an inapplicable optional member is **omitted**, never written as JSON `null`
     and never as a sentinel (§2.3.1 item 7, the §2.1.7 item 3 convention the
     existing `SpecialGem` and `PassiveResetOverride` members already follow)
3. **An empty collection is written as an empty array** (`[]`) on both entities,
   on every record — the member is never omitted and never `null` (§2.3.2 item 1).
4. **Enum members written by name, not ordinal** (`StatusEffectType`,
   `StatusEffectSource`), matching the existing Element / GemType / BossState
   precedent, so a stored record cannot change meaning if a member is added ahead
   of it in the enum. The written names are the explicit camelCase/PascalCase
   convention the mapping already declares in `BattleStateJsonNames` — **not** a
   ruling on the contract's string values, per §2.3.2 item 2.
5. **Deserialization that rebuilds each instance through the Domain factories**
   (`StatusEffect.TurnBased` / `StatusEffect.TriggerBased`) so the documented
   invariants are enforced rather than trusted — the same "deserialization
   validates rather than trusts" rule the existing mapping states. A malformed
   element (both duration models, neither, `RemainingTurns` of `0` or less, a
   `BuffDebuff` without `TargetStat`, a `TargetStat` on a type that modifies no
   stat, an unknown `type`/`source` name) is **rejected**, not repaired into a
   state the battle never had.
6. **Order preserved element for element**, so the round trip is a no-op
   (§2.3.2 items 5–6). Nothing is sorted, filtered, or de-duplicated by the
   mapping — in particular the mapping must **not** impose the §5.1.1 item 6 `Id`
   ordering on the stored array, because that order belongs to the step 19a pass
   and §2.3.1 item 10 makes array order non-semantic.
7. **The absence of the collection is not representable.** No `null` DTO member,
   no `null` domain array, and no "no StatusEffects yet" spelling is introduced
   (§2.3.2 item 1).
8. **Updating the two superseded staging XML doc comments** on `PetStateJson` and
   `BossStateJson` only to the extent they would otherwise become false (they
   currently assert `StatusEffects[]` "is not yet implemented"). `BattleStateJson`
   keeps documenting the mapping's non-wire, non-Redis-schema nature — that
   remains true.
9. **Updating the superseded assertion** in
   `BattleStateSerializationTests.Serialization_ShouldNotWriteStatusEffectsThat
   AreNotYetImplemented` to assert the member's **presence and contract** instead
   of its absence, and extending
   `Serialization_ShouldWriteExactlyTheDocumentedRootMemberSet` /
   `Serialization_ShouldWriteEveryMemberUnderItsExplicitCamelCaseName` only where
   the new members require it. No assertion is weakened or deleted
   (`AGENTS.md` §15).
10. **Round-trip tests** at the depth the risk requires — see Testing Requirements.
11. **Reporting, not changing, anything the implementation reveals as a
    documentation gap** (`AGENTS.md` §17): if the contract cannot be implemented
    as written, STOP and report rather than adjusting `docs/`.

### Out of Scope

- **Any new gameplay rule or StatusEffect semantics.** No new type, no new
  magnitude or duration constant, no new tick or expiry timing, no stacking model
  (`GAME_STATE.md` §2.3.3; `COMBAT_RULES.md` §5.3 is the owner and is not
  restated, reinterpreted, or extended).
- **No new fields on the element.** No `StatusEffectId`, no `StackCount`, no
  `Duration`, no `TicksRemaining`, no `ApplicationTurn`, no `ElapsedTurns`, no
  `RefreshedAt`, and no other member — §2.3.2 item 4 forbids a second counter for
  the quantity `remainingTurns` already represents, and §2.3.3 forbids the rest.
- **Producing instances.** No Boss Skill secondary-effect application (Root), no
  Burn application from Flame Burst, no Shield cast, no Stun application.
  Deserialization reconstructs instances a producer supplied; it never creates a
  gameplay effect. The tests seed instances directly, exactly as TASK-095's do.
- **Burn's damage tick.** §17 step 19a's damage-over-time tick runs through the
  Damage Pipeline (`COMBAT_RULES.md` §5.2 item 3) and remains unimplemented.
- **Changing the lifecycle.** `StatusEffectLifecycle` is consumed as-is; its
  apply / refresh / consume / expire behavior and its `Id`-ascending pass order
  are not touched (`GAME_STATE.md` §5.1.1).
- **Any other `BattleState` member's serialization.** No change to the root
  members, `BoardState`, `RngState`, `Combo`/`MatchCount`, `LastCommittedSwapPair`,
  `EquippedRelics`/`EquippedCards`, or the Boss's other members. The board's
  `Cells[64]` mapping, its ordering, and its absence conventions are untouched.
- **Redis.** No key, no TTL, no Redis-only field, no concurrency change, no second
  storage representation, no change to `BattleStateRepository`, and no change to
  the write-back or compare-and-set (`GAME_STATE.md` §2.3.2 item 7;
  `REDIS_STATE.md` §1–§4, §7 item 9 already cover the record this collection
  travels in).
- **SignalR / wire.** No event, no payload member, no Hub method, no client
  projection. `StatusEffects[]` is **not** a wire member (`GAME_STATE.md` §2.3.1
  wire note; `SIGNALR_PROTOCOL.md` §4 item 4, §4.2 item 2) and adding one is a
  protocol change owned by its own task.
- **PostgreSQL.** No schema, no migration, no `BattleState` persistence
  (`AGENTS.md` §13: active battle state is Redis-scoped; `TDD.md` §4).
- **Client.** No client runtime, no Phaser presentation, no client-side
  computation of any status value (`AGENTS.md` §10, ADR-001).
- **Match-3, board, swap, match detection, cascade, gravity, combo, damage
  calculation, Boss skills, Pet skills, Passives, rewards, progression.**
- **`BossState.SkillCooldown`.** Its separate "decrements by 1 at each Turn
  increment" rule is not adopted, generalized, or touched (`GAME_STATE.md`
  §2.4.3, §5.1.1 item 2).
- **`docs/`.** No documentation change is expected — the contract is already
  authored by TASK-093. If implementation reveals a gap, report it per
  `AGENTS.md` §17 instead of editing the document.
- **TASK-095, TASK-093, TASK-094, TASK-091, TASK-092, TASK-029, TASK-040.** Not
  modified. Completed tasks are immutable (`TASK_LIFECYCLE.md` §3).
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Acceptance Criteria

- [ ] `PetStateJson` and `BossStateJson` each declare a `statusEffects` member
      (`GAME_STATE.md` §2.3.2 item 1).
- [ ] The member is written as a JSON **array** on both entities, and an entity
      with no active effect is written as an **empty array** — never omitted,
      never `null` (§2.3.2 item 1).
- [ ] Each element is written with exactly the members and types of §2.3.2 item 3
      — `id`/`type`/`source`/`magnitude` always present; `targetStat`,
      `remainingTurns`, `expiryCondition` per their documented conditions.
- [ ] An optional member that does not apply is **absent** from the serialized
      element — not JSON `null` and not a sentinel string (§2.3.1 item 7).
- [ ] `targetStat` is present in the round-tripped instance iff the instance's
      `Type` is `BuffDebuff` (§2.3.1 item 7).
- [ ] `remainingTurns` round-trips as a plain integer with its exact value; a
      Turn-based instance never gains `expiryCondition` and a trigger-based
      instance never gains `remainingTurns` (§2.3.1 item 3, §2.3.2 item 4).
- [ ] An instance carrying `RemainingTurns` survives `Domain → JSON → Domain`
      with the same value (§2.3.2 item 5).
- [ ] A trigger-based instance's `ExpiryCondition` survives the round trip with
      the same value (§2.3.2 item 5).
- [ ] `type` and `source` survive the round trip by their documented names, and
      are written by name rather than by enum ordinal (§2.3.2 item 3).
- [ ] Multiple active instances round-trip with the same elements and the **same
      element order** as the state held (§2.3.2 items 5–6).
- [ ] The mapping does **not** sort or otherwise reorder the collection; in
      particular it does not impose the §5.1.1 item 6 `Id` order on it
      (§2.3.1 item 10, §5.1.1 item 6).
- [ ] `magnitude` round-trips exactly for both an integral magnitude (flat
      damage) and a fractional one (`-30` percent), with no rounding, scaling, or
      unit conversion (§2.3.1 item 2).
- [ ] A round trip is stable across repeated cycles: serialize → deserialize →
      serialize produces a byte-identical document (§2.3.2 item 5).
- [ ] Both the Pet's and the Boss's collections survive a **full `BattleState`**
      round trip, and no unrelated `BattleState` member changes value
      (§2.3.2 item 5).
- [ ] A deserialized state holding no effect compares equal to the state that was
      written — "empty" is one value, not a second spelling of "unset"
      (§2.3.2 item 1, §2.3.1 item 8).
- [ ] A malformed element (both duration models, neither, `remainingTurns` of `0`
      or less, `BuffDebuff` without `targetStat`, `targetStat` on a type that
      modifies no stat, an unknown `type` or `source` name) is **rejected** rather
      than repaired, consistent with the mapping's existing rejection behavior
      (§2.3.1 items 3 and 8).
- [ ] `StatusEffects[]` is **not** added to any SignalR payload, event, or Hub
      method (`SIGNALR_PROTOCOL.md` §4.2 item 2; `GAME_STATE.md` §2.3.1 wire
      note).
- [ ] No Redis key, Redis-only field, TTL, record, or concurrency semantic is
      added or changed (`GAME_STATE.md` §2.3.2 item 7; `REDIS_STATE.md` §1–§4).
- [ ] No PostgreSQL schema or migration is added.
- [ ] `BattleStateSerializer` remains a pure, deterministic, total mapping: no
      RNG, no clock, no I/O, no Redis/HTTP/SignalR type, and no gameplay value
      computed, clamped, or derived (`ARCHITECTURE.md` §2.1, `TDD.md` §6,
      `GAME_STATE.md` §2.1.7).
- [ ] The serializer is reachable from **no** new endpoint and no new wire surface
      (`AGENTS.md` §10).
- [ ] `StatusEffectLifecycle`, `StatusEffect`, `StatusEffectType`, `PetState`, and
      `BossState` are unchanged in behavior; `ActiveStatusEffects` remains
      non-nullable and defaults to `[]` (`GAME_STATE.md` §2.3.2 item 1).
- [ ] The superseded "not yet implemented" assertions are updated to assert the
      new contract; **no** existing assertion is weakened or deleted
      (`AGENTS.md` §15).
- [ ] All existing serializer tests remain green, and
      `GameServer.Application.Tests` / `GameServer.Infrastructure.Tests` /
      `GameServer.Api.Tests` show no new failures.
- [ ] No documentation under `docs/` is modified, or — if a gap is found — it is
      reported per `AGENTS.md` §17 rather than edited silently.
- [ ] `GameServer.Domain` gains no reference to ASP.NET Core, SignalR, EF Core,
      Redis, HTTP, Phaser, or Discord (`ARCHITECTURE.md` §2.1).
- [ ] All relevant tests pass at the required validation depth
      (`core/validation.md` §2).
- [ ] Quality review checklist passes (`quality/review.md` §1).
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

### Explicit Constraints

```text
No gameplay expansion.
No new StatusEffect semantics.
No PostgreSQL BattleState persistence.
No undocumented Redis behavior.
No undocumented SignalR methods.
No frontend changes.
No client-authoritative state.
No modification to TASK-095.
```

---

## Affected Files & Areas

```text
[x] src/backend/GameServer.Domain/Battle/Serialization/BattleStateJson.cs
      (statusEffects member + element DTO on PetStateJson/BossStateJson;
       superseded staging comments corrected)
[x] src/backend/GameServer.Domain/Battle/Serialization/BattleStateSerializer.cs
      (element projection both directions, on both entities)
[ ] src/backend/GameServer.Domain/Battle/ (StatusEffect.cs, StatusEffectType.cs,
      StatusEffectLifecycle.cs, PetState.cs, BossState.cs — read-only; the
      factories are consumed, not changed)
[ ] src/backend/GameServer.Application/ (none)
[ ] src/backend/GameServer.Infrastructure/ (none — no Redis, no PostgreSQL)
[ ] src/backend/GameServer.Api/ (none — no endpoint, no Hub method, no wire member)
[ ] src/frontend/client/ (none)
[x] tests/backend/GameServer.Domain.Tests/ (BattleStateSerializationTests.cs —
      superseded absence assertion updated; new round-trip tests. A new
      StatusEffectSerializationTests.cs is acceptable if it keeps the existing
      suite readable rather than growing it past its current purpose)
[ ] tests/backend/GameServer.Application.Tests/ (none expected — but the
      round-trip tests that assert whole-record equality must stay green; report
      if the mapping's change requires one to be adjusted, per AGENTS.md §15)
[ ] tests/backend/GameServer.Infrastructure.Tests/ (none expected — Redis record
      content only; verify no new failure)
[ ] tests/backend/GameServer.Api.Tests/ (none expected — the wire membership
      assertions must keep passing unchanged)
[ ] docs/ (none — the contract is already authored by TASK-093; report instead if
      implementation reveals a documentation gap, per AGENTS.md §17)
```

---

## Implementation Notes

- **The contract is settled; do not re-derive it.** Every member, type, presence
  rule, and ordering statement comes from `GAME_STATE.md` §2.3.1/§2.3.2. Cite
  them by item number in code comments, matching how the existing mapping cites
  §2.1.7, §2.1.10, and §2.6.2 — do not restate them as new prose.
- **Extend the existing mapping; do not redesign it.** `BattleStateSerializer`
  is a one-to-one field-copy projection. The new members follow the same shape:
  a `ToStatusEffectJson` / `FromStatusEffectJson` pair called from the existing
  `ToPetStateJson` / `ToBossStateJson` / `FromPetStateJson` / `FromBossStateJson`
  methods. No new abstraction, interface, converter, or registry is needed
  (`AGENTS.md` §9, `ARCHITECTURE.md` §5).
- **One element DTO for both entities.** §2.3.1's preamble and §2.4.1 state the
  schema is identical for `PetState` and `BossState` — one DTO type, no
  boss-specific variant.
- **Omission is the absent-member convention.** The existing `SpecialGem` and
  `PassiveResetOverride` members spell absence with
  `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` over a nullable
  member. Use the same idiom for `targetStat`, `remainingTurns`, and
  `expiryCondition`; do **not** invent a sentinel value, and do **not** write
  `remainingTurns: 0` for a trigger-based instance (§2.3.1 items 7–8).
- **`remainingTurns` is nullable in the DTO but never zero-valued.** The Domain
  factory rejects a duration below `1` (§2.3.1 item 8: zero is never an observable
  committed value), so a stored `0` is a contract violation and must be rejected
  on the way in rather than normalized.
- **Rebuild through the Domain factories.** `StatusEffect.TurnBased` and
  `StatusEffect.TriggerBased` already enforce §2.3.1's two exclusivity rules, the
  `TargetStat`-iff-`BuffDebuff` pairing, and the positive-duration rule. Routing
  deserialization through them makes those invariants validated rather than
  trusted — the same reason the existing mapping goes through
  `BoardState.FromCellEntries` and `CommittedSwapPair` rather than setting fields.
- **Which factory to call is decided by presence, not by guessing.** An element
  with `remainingTurns` present is Turn-based; an element with `expiryCondition`
  present is trigger-based; both present or neither present is a contract
  violation (§2.3.1 item 3). `StatusEffect.UsesTurnCountdown` already expresses
  the reading the lifecycle uses.
- **Do not impose the step 19a order on the stored array.** §5.1.1 item 6 fixes the
  *pass* order to `Id` ascending, and §2.3.2 item 6 states explicitly that the
  *stored* order is preserved for round-trip fidelity and means nothing. Sorting
  here would silently reorder a record that is required to be a round-trip no-op.
- **`double` magnitudes must round-trip without loss.** `Magnitude` is a `double`
  (§2.3.1 item 2: typed but not interpreted). Write it as a JSON number and read
  it back as the same value — no rounding, no formatting to an integer, and no
  unit conversion. `System.Text.Json`'s default number handling satisfies this;
  do not introduce a custom number policy.
- **Enum names, not ordinals.** `StatusEffectType` and `StatusEffectSource` follow
  the existing `Element`, `GemType`, `BossStateKind`, and `SpecialGemType`
  precedent — written by name and parsed with `ignoreCase: true`. The exact
  spelling is the serializer's implementation detail (§2.3.2 item 2,
  `SIGNALR_PROTOCOL.md` §8 item 1), declared once in `BattleStateJsonNames`.
- **The staging comment is now false and must be corrected.** `PetStateJson` and
  `BossStateJson` currently say `StatusEffects[]` "is not yet implemented and is
  therefore not a member". After this task that is wrong; update it to state the
  member's contract. `BattleStateJson`'s own summary keeps documenting that this
  mapping is a runtime persistence representation and not a wire DTO — still
  true.
- **Existing tests will need updating, and that is expected.**
  `Serialization_ShouldNotWriteStatusEffectsThatAreNotYetImplemented` asserts the
  member is *absent*. That assertion encodes the staging position this task
  removes — TASK-095 §"Explicitly Not Tested Here" anticipated exactly this.
  Replace it with an assertion of the new contract; do not weaken or delete a
  contract assertion to make the change compile (`AGENTS.md` §15).
- **A `null` loadout array is a different case and must not be copied.** The
  existing `EquippedRelics`/`EquippedCards` members are nullable-and-omitted
  because their stage has an "unset" state (`RELIC_RULES.md` §2.1 item 1 defines
  no zero-Relic battle). `StatusEffects[]` has no such state: §2.3.2 item 1 makes
  an empty array the only representation of "none". Do not carry the nullable
  pattern across by analogy.
- **Do not pass `StatusEffects` through the wire "while you are here".** It is a
  separate protocol task; `SIGNALR_PROTOCOL.md` §4.2 item 2 fixes the payload
  member sets, and the Api tests assert those sets.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — the element projection's member set and presence rules;
                         empty-array writing on both entities; enum-name writing;
                         optional-member omission; malformed-element rejection
[x] Integration tests  — a full BattleState round trip carrying effects on both
                         PetState and BossState through the existing serializer,
                         and — where a live record is available — through the
                         existing Redis write-back path unchanged
[x] Gameplay scenarios — Given/When/Then round trips over the states
                         GAME_STATE.md §5.1.1 and COMBAT_RULES.md §5.3 produce
                         (a Turn-based pair, a trigger-based instance, a
                         multi-effect state), asserted at the state-transition
                         level, not only at the final value
```

### Required Scenarios (derived from `GAME_STATE.md` §2.3.2, §5.1.1)

```text
Given a BattleState whose PetState and BossState both hold an EMPTY
      StatusEffects collection
When  it is serialized and deserialized
Then  both collections are still present
And   both are empty
And   neither has become null
And   the member was written, not omitted

Given a Turn-based instance (Burn, Type = DoT) with RemainingTurns = 2
When  the state is serialized and deserialized
Then  the same instance is present with RemainingTurns = 2
And   it carries no ExpiryCondition member
And   its Magnitude and Source are unchanged

Given a trigger-based instance (Shield, Type = Shield) carrying
      ExpiryCondition = "ShieldDepleted"
When  the state is serialized and deserialized
Then  the instance is present with the same ExpiryCondition
And   it carries no RemainingTurns member
And   no duration was invented for it

Given a BuffDebuff instance (Root) with TargetStat = "ATK" and a negative
      Magnitude
When  the state is serialized and deserialized
Then  TargetStat is present with the same value
And   the Magnitude round-trips exactly, sign and fraction included

Given several active instances held in a non-Id order
When  the state is serialized and deserialized
Then  the same elements are present in the SAME order
And   the collection was not sorted into Id order
```

### Key Edge Cases

- **Empty collection round-trips as empty, not `null`.** The single most
  failure-prone case, and the one TASK-095 had to work around structurally
  (`GAME_STATE.md` §2.3.2 item 1). Assert the member exists in the JSON and is an
  array of length 0 — not merely that the restored collection is empty.
- **An absent optional member must not become `null`.** §2.3.2 item 5 names
  materializing an absent optional member as `null` an explicit **defect**, so the
  assertion is on the raw JSON (member present or absent), not only on the
  deserialized value.
- **`RemainingTurns` is not renumbered, and `0` never appears.** §2.3.2 items 4–5
  and §2.3.1 item 8.
- **Multiple simultaneous effects** (`Burn` and `Root`, per TASK-093's edge-case
  list) each survive with their own values, and the element order is preserved
  (`GAME_STATE.md` §2.3.2 items 5–6).
- **Both entities at once.** A `BattleState` carrying effects on the Pet *and* the
  Boss round-trips both, with no cross-contamination between the two collections.
- **Fractional vs. integral magnitude** round-trip without conversion — Root's
  percentage and Burn's flat damage are both real `Magnitude` values
  (§2.3.1 item 2).
- **A malformed element is rejected, not repaired**: both duration models
  present; neither present; `remainingTurns` of `0` or negative; a `BuffDebuff`
  with no `targetStat`; a `targetStat` on a `DoT`; an unknown `type` or `source`
  name (§2.3.1 items 3, 7, 8).
- **Repeated-cycle stability.** serialize → deserialize → serialize is
  byte-identical (§2.3.2 item 5).
- **Unrelated members unchanged.** A full `BattleState` round trip that carries
  effects still preserves every other member by value — the board's 64 cells in
  order, `Turn`, `Sequence`, the RNG pair, `Combo`/`MatchCount`, the commit
  record, both loadout arrays, and every Boss member (§2.3.2 item 5, §2.1.7
  item 5).
- **Determinism.** The same input state produces the same document across runs
  (`TDD.md` §6).
- **No wire member appears.** Serializing a state that carries effects must not
  place `statusEffects` on any SignalR payload; the existing Api wire-membership
  assertions must keep passing (`SIGNALR_PROTOCOL.md` §4.2 item 2).

### Explicitly Not Tested Here

- That a gameplay path *applies* an effect — no producer exists yet; tests seed
  instances directly, exactly as TASK-095's tests do.
- Burn's damage-per-tick value — the Damage Pipeline tick remains unimplemented.
- The §5.1.1 lifecycle itself (apply/refresh/consume/expire) — TASK-095 delivered
  and tested it; this task only carries its results through a round trip.
- `StatusEffects[]` on any wire surface — it is not a wire member.

---

## Stop Conditions

Universal `AGENTS.md` §20 stops always apply. Task-specific:

- If the required JSON shape or round-trip obligation cannot be fully derived from
  the Authoritative References: STOP per `AGENTS.md` §7.
- If `GAME_STATE.md` §2.3.2's shape conflicts with §2.3.1's instance schema, or
  with `REDIS_STATE.md` §2 item 1 / §7 item 9: STOP per `AGENTS.md` §4 — report
  both sections rather than choosing one.
- If a `null`-vs-`[]` semantic conflict appears across authoritative documents:
  STOP per `AGENTS.md` §4. (§2.3.2 item 1 is unambiguous today: the collection is
  never omitted and never `null`.)
- If implementing the mapping requires adding a new gameplay field or member to
  the element (a `StatusEffectId`, `StackCount`, `Duration`, `TicksRemaining`,
  `ApplicationTurn`, or similar): STOP — §2.3.2 item 4 and §2.3.3 forbid it, and
  it is a design change, not an implementation detail.
- If implementing the mapping requires changing `StatusEffect` semantics, its
  factories, or the `StatusEffectLifecycle` behavior: STOP — that is TASK-095's
  settled contract, not this task's.
- If the mapping cannot be made lossless without a Redis key, field, or
  concurrency change: STOP — §2.3.2 item 7 states none is introduced.
- If the mapping appears to require a SignalR payload member or Hub method: STOP —
  `SIGNALR_PROTOCOL.md` §4.2 item 2 fixes the payload sets and
  `StatusEffects[]` is not a member; that is a separate protocol task.
- If the existing "not yet implemented" assertions cannot be reconciled with the
  authored contract without deleting or weakening a contract assertion: STOP and
  report the conflict rather than weakening the test (`AGENTS.md` §15).
- If implementation reveals that `docs/` is wrong or incomplete rather than the
  code: STOP and report per `AGENTS.md` §17 — do not edit the document to match
  the code.
- If any `src/frontend/` change appears necessary: STOP per `AGENTS.md` §10.
- If the task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: STOP & decompose.

---

## Completion Evidence

### Changed Files

```text
src/backend/GameServer.Domain/Battle/Serialization/BattleStateJson.cs
    + statusEffects member on PetStateJson AND BossStateJson (one member name,
      one shared element shape — §2.3.2 item 1, §2.4.1)
    + StatusEffectJson element DTO: id/type/source/magnitude required;
      targetStat/remainingTurns/expiryCondition optional and omitted when absent
    + 10 new BattleStateJsonNames constants (collection + 7 element members)
    ~ superseded staging comments on PetStateJson/BossStateJson corrected — they
      asserted StatusEffects[] "is not yet implemented"; now they state the
      member's contract (affected-files item 8 of the task)

src/backend/GameServer.Domain/Battle/Serialization/BattleStateSerializer.cs
    + ToStatusEffectJson      — pure field copy, enums by name (§2.3.2 item 3)
    + FromStatusEffectsJson   — collection rebuild; rejects a null member
    + FromStatusEffectJson    — rebuilds through StatusEffect.TurnBased /
                                StatusEffect.TriggerBased so §2.3.1's invariants
                                are validated rather than trusted
    ~ ToPetStateJson / ToBossStateJson / FromPetStateJson / FromBossStateJson
      carry the collection; no other member touched

tests/backend/GameServer.Domain.Tests/BattleStateSerializationTests.cs
    ~ Serialization_ShouldNotWriteStatusEffectsThatAreNotYetImplemented (which
      asserted ABSENCE) replaced by
      Serialization_ShouldWriteStatusEffectsAsAnAlwaysPresentArray, asserting
      presence, JSON array kind, zero length, and a restored non-null empty
      collection — no assertion weakened or deleted
    + 14 new tests: Turn-based round trip; trigger-based round trip; TargetStat
      iff BuffDebuff; multi-instance collection order; Pet+Boss simultaneously;
      unrelated-members regression; repeated-cycle stability; fractional vs
      integral magnitude; Stun+Burn together; enum-by-name; empty-collection
      single value; null-member rejection; and 8 malformed-element rejections
```

No other file was changed by this task. **No documentation, Redis, SignalR,
PostgreSQL, frontend, Application, or gameplay file was modified.**

### Validation Results

```text
GameServer.Domain.Tests          1012 passed / 0 failed   (baseline 991; +21)
GameServer.Application.Tests      377 passed / 0 failed   (baseline 377;  +0)
GameServer.Infrastructure.Tests   297 passed / 0 failed   (baseline 297;  +0)
GameServer.Api.Tests              253 passed / 1 failed   (baseline 253 / 1 — identical)
```

Focused serializer suite: `BattleStateSerializationTests` **69 passed / 0 failed**
(baseline 55; +14). Wire-membership subset `ApiIntegrationTests` 59/59 passed;
`RedisBattleStateRepositoryTests` 13/13 passed.

The single Api failure is **pre-existing and environmental**, not caused by this
task. It was proven against the unchanged baseline by stashing this task's three
files and re-running:

```text
BattleResultSmokeTest.SmokeTest_AuthoritativeBattleActionToResultRead_
  ShouldWalkTheWholeDocumentedPath                       FAIL (baseline AND with TASK-096)

  System.AggregateException : An error occurred while writing to logger(s).
  (Cannot open log for source '.NET Runtime'. You may not have write access.)
  ---- System.InvalidOperationException : Cannot open log for source '.NET Runtime'.
  -------- System.ComponentModel.Win32Exception : Access is denied.
```

It fails identically with and without this task's changes — a Windows Event Log
permission condition in the test environment, and the same failure TASK-095
recorded.

### Acceptance Criteria Verification

| Criterion | Result | Evidence |
|---|---|---|
| `statusEffects` declared on both DTOs | PASS | `BattleStateJson.cs` (2 members) |
| Written as an array; empty is `[]`, never omitted/`null` | PASS | `Serialization_ShouldWriteStatusEffectsAsAnAlwaysPresentArray` |
| Element member set is §2.3.2 item 3's | PASS | `StatusEffectJson`; element assertions on raw JSON |
| Inapplicable optional member absent, not `null`/sentinel | PASS | `TryGetProperty` assertions in the Turn-based + trigger-based tests |
| `targetStat` present iff `BuffDebuff` | PASS | `RoundTrip_ShouldWriteTargetStatExactlyForABuffDebuff` |
| `remainingTurns` integer; models never cross | PASS | Turn-based + trigger-based round-trip tests |
| Turn-based `RemainingTurns` survives | PASS | `RoundTrip_ShouldPreserveATurnBasedInstance` |
| Trigger-based `ExpiryCondition` survives | PASS | `RoundTrip_ShouldPreserveATriggerBasedInstance` |
| `type`/`source` by name, not ordinal | PASS | `RoundTrip_ShouldPreserveEnumsByNameRatherThanOrdinal` |
| Multiple instances keep element order | PASS | `RoundTrip_ShouldPreserveMultipleInstancesInCollectionOrder` (held non-`Id` order) |
| Not sorted into §5.1.1 item 6 `Id` order | PASS | same test asserts `["Stun","Burn","Root"]` on the document |
| `magnitude` exact, integral and fractional | PASS | `RoundTrip_ShouldPreserveAFractionalAndAnIntegralMagnitude...` |
| Repeated-cycle stability | PASS | `RoundTrip_ShouldBeStableAcrossRepeatedCyclesWithEffects` |
| Pet and Boss both survive a full round trip | PASS | `RoundTrip_ShouldPreserveBothPetAndBossCollections` |
| No unrelated member changes | PASS | `RoundTrip_ShouldNotChangeAnyUnrelatedMemberWhenEffectsAreCarried` |
| Empty state compares equal (not a third value) | PASS | `RoundTrip_ShouldRestoreAnEmptyCollectionAsOneValueNotTwo` |
| Malformed element rejected, not repaired | PASS | 8 rejection tests (both/neither model, `0`, negative, `targetStat` both halves, unknown `type`/`source`, missing required member, explicit `null` member) |
| Not added to any wire payload/event/Hub method | PASS | `ApiIntegrationTests` 59/59; `SIGNALR_PROTOCOL.md` untouched |
| No Redis key/field/TTL/concurrency change | PASS | `Infrastructure` diff empty; 297/297 pass |
| No PostgreSQL schema/migration | PASS | no `Infrastructure` or migration change |
| Mapping stays pure/deterministic/no I/O | PASS | field copies only; no RNG, clock, or transport type |
| No new endpoint or wire surface | PASS | `Api/` diff empty |
| `StatusEffect`/`Lifecycle`/`PetState`/`BossState` unchanged | PASS | `git diff` on those files is empty; 1012 Domain tests green |
| Superseded assertions updated, none weakened | PASS | old absence test replaced by a stronger presence+contract test |
| No `docs/` change | PASS | `GAME_STATE.md` diff contains no TASK-096 text (it is TASK-093's v2.8 contract) |
| Domain gains no forbidden reference | PASS | `System.Text.Json` only; no ASP.NET/Redis/SignalR/EF type |
| All relevant tests pass at required depth | PASS | see Validation Results |
| Quality review checklist | PASS | see Review Checklist below |
| No authoritative rule/contract violated | PASS | ADR-001 satisfied; §2.3.1/§2.3.2 implemented as written |

### Review Checklist (`quality/review.md` §1)

```text
Correctness     PASS — implements GAME_STATE.md §2.3.2 items 1–7 as written;
                       no rule reinterpreted or extended
Architecture    PASS — mapping stays in Domain beside the type it maps
                       (ARCHITECTURE.md §2.1); no dependency added
Scope           PASS — 3 files changed, all listed in Affected Files; no
                       unrelated refactor, no "while I was here" change
Tests           PASS — MEDIUM risk: unit + integration + state-transition
                       round trips, plus a malformed-input rejection matrix
Documentation   PASS — no doc change required; contract already authored by
                       TASK-093 and implemented as written
Security        PASS — no auth surface touched; no data exposure added
Performance     PASS — no query, no I/O, no hot-path change; the mapping is
                       already on the existing write-back path
Maintainability PASS — no new abstraction: one DTO, three methods, reusing the
                       existing absence idiom and the Domain factories
Determinism     PASS — no RNG, no clock; server-authoritative state carried
                       verbatim; no client computation
```

### Implementation Deviation (reported, not silent)

One decision was **not** anticipated by the task text and is reported here per
`AGENTS.md` §16 / §21 rather than being applied silently.

**What.** The DTO's `StatusEffects` member is declared `IReadOnlyList<StatusEffectJson>?`
(nullable) and deserialization routes through a `FromStatusEffectsJson` guard that
throws a `JsonException` naming `GAME_STATE.md` §2.3.2 item 1 when the member is
`null`.

**Why.** `System.Text.Json` assigns `null` to a `required` reference member when a
stored document literally spells `"statusEffects": null`. A test written for the
contract's "never `null`" rule then failed — not with a contract rejection, but
with an **incidental** `ArgumentNullException` from LINQ inside `Select`. The
record was correctly refused, but for the wrong reason and with a message that
named no rule. §2.3.2 item 1 makes `null` an unrepresentable state, so the failure
is a contract violation and should be reported as one. The nullable DTO type plus
the guard is the smallest change that makes the rejection deliberate; the mapping
still always **writes** an array, so the `null` branch is unreachable except from a
document that already broke the contract.

**Consequence.** This widens the DTO's *type* while leaving the *serialized shape*
exactly §2.3.2's. It adds no member, no rule, and no accepted state — it turns an
incidental exception into a documented one. Recorded rather than silently absorbed
because the task's Implementation Notes described the member as non-nullable.

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic (no `src/frontend/` file changed by this task)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1 — Status Effects are IN)
- [x] Confirmed no new StatusEffect semantics (the Domain factories were consumed, not changed)
- [x] Confirmed no serializer, wire, Redis, or PostgreSQL change beyond the three listed files
- [x] Confirmed TASK-093/094/095/091/092/029/040 were not modified
- [x] Confirmed `StatusEffects[]` was not added to any SignalR payload
- [x] Confirmed `BattleStateRepository` and all Redis behavior untouched
- [x] Confirmed no gameplay effect is produced (tests seed instances directly)
- [x] Confirmed no documentation was modified
- [x] Confirmed unrelated pre-existing uncommitted changes from earlier tasks were left untouched

