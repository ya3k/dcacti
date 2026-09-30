# TASK-095 — Implement StatusEffect Domain State and §17 Step 19a Duration Lifecycle

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, or schemas.

  THIS TASK IMPLEMENTS AN ALREADY-DEFINED CONTRACT. It authors no rule.
  The authoritative contract is:
    GAME_STATE.md §2.3.1   instance schema
    GAME_STATE.md §2.3.3   explicit non-additions
    GAME_STATE.md §5.1.1   lifecycle: apply, refresh, consume, expire
    COMBAT_RULES.md §5.3   the canonical gameplay rule (DR1–DR6)
  All of it is settled by TASK-093 (contract) and TASK-094 (DR1–DR6).
  Neither is modified by this task.

  BOUNDARY: Domain state model + the §17 step 19a state mutation that
  gives it meaning. This is Candidate A + C of the task-generation brief,
  selected because GAME_STATE.md §0 item 5 forbids representing a field
  no rule yet reads — a StatusEffect type without §5.1.1's mutation would
  be exactly that. Serialization (Candidate B) is a separate mapping
  layer with its own round-trip obligation and is deliberately deferred
  to its own task. SignalR (Candidate E) is premature: §2.3.1's wire note
  records that StatusEffects[] is not a wire member, so no protocol task
  is meaningful yet.
-->

---

## Metadata

```text
Task ID:           TASK-095
Type:              FEATURE
Status:            DONE
Risk:              MEDIUM (TASK_TYPES.md §4 — FEATURE baseline MEDIUM; raised toward
                   HIGH because it touches the battle-state model. No gameplay
                   value is computed: the rule is already documented and the
                   change is a pure state transition.)
Priority:          HIGH (the StatusEffects[] contract is authored and unimplemented;
                   Boss Skill secondary effects — Root, Shield, Stun — cannot land
                   until this state model exists.)
Primary Agent:     gameplay (owns Domain battle state; TASK_TYPES.md §5 Domain ×
                   Type matrix: Combat → Gameplay)
Supporting Agents: backend, testing, review
Workflow:          development/feature.md
Skills:            gameplay/gameplay-behavior-derivation,
                   discovery/impact-analysis,
                   testing/test-scenario-generation,
                   quality/architecture-conformance
                   (4 skills — Simple/Normal budget, tasks/README.md §12)
Dependencies:      TASK-093 (DONE — authored the StatusEffects[] contract),
                   TASK-094 (READY — delivered DR1–DR6, consumed as settled input),
                   TASK-091 (DONE — fixed §17 step 19a as the tick timing),
                   TASK-092 (DONE — Boss Skill effect magnitudes and durations)
Blocks:            StatusEffects[] serialization/round-trip task;
                   Boss Skill secondary-effect implementation (Root, Shield);
                   any future StatusEffects[] wire-delivery task
Estimate:          Normal (one Domain type, one lifecycle function, state wiring,
                   unit + gameplay-scenario tests; no new gameplay rule)
```

**Type classification note.** `FEATURE`, not `GAMEPLAY-CHANGE`: the mechanic and
its contract already exist in `docs/` (`COMBAT_RULES.md` §5.3, `GAME_STATE.md`
§2.3.1/§5.1.1) — the task is to build them. Per `TASK_TYPES.md` §2, a
GAMEPLAY-CHANGE is required only when the rule itself must change or a new
undocumented mechanic is authorized, which is not the case here
(`AGENTS.md` §7 does not fire).

**Boundary note.** This task deliberately stops at the Domain state model and
its lifecycle mutation. It adds no serializer member, no wire member, no Redis
behavior, no event, and no Boss/Pet skill that *applies* an effect — producing
instances is the downstream gameplay task's act.

---

## Objective

Implement the `StatusEffect` Domain type and the `StatusEffects[]` state model on
`PetState` and `BossState`, together with the `GAME_RULES.md` §17 step 19a
duration-consumption/expiry pass, so that the already-authored
`GAME_STATE.md` §2.3.1 schema and §5.1.1 lifecycle are represented in
`GameServer.Domain` exactly as documented — with no new gameplay rule, no
serialization change, and no wire or storage change.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Status Effects are IN scope (Combat block:
  Burn, Shield, Buff/Debuff)
- `docs/01-game-design/COMBAT_RULES.md` §5.3 — **canonical owner** of duration
  consumption timing (DR1–DR6); §5.3.1 apply/refresh ordering, §5.3.2 scope,
  §5.3.3 worked examples, §5.3.4 exclusions. Do not restate; implement.
- `docs/01-game-design/COMBAT_RULES.md` §5.1, §5.2 — MVP Status Effect list and
  status rules (source/magnitude/duration, refresh-don't-stack)
- `docs/01-game-design/GAME_RULES.md` §17 step 19a — the resolution position the
  lifecycle runs at; already carries its scoping note. §17's fixed order is
  canonical and must not be reordered.
- `docs/02-technical/GAME_STATE.md` §2.3.1 — instance schema (7 members,
  presence/absence rules, item 6 at-most-one-per-`Id`, item 7 absence
  conventions, item 8 zero-is-not-expired, item 9 instant effects create no
  instance, item 10 ordering is not semantic, item 11 deterministic iteration
  order)
- `docs/02-technical/GAME_STATE.md` §2.3.3 — what this model must NOT add (no new
  type, no magnitude/duration constant, no event, no pending collection, no
  stacking model)
- `docs/02-technical/GAME_STATE.md` §5.1.1 — **the lifecycle mutation contract**
  (items 1–11: apply-as-set, exactly one decrement at 19a, apply timing does not
  change the count, expiry is removal, single pass, deterministic `Id`
  ascending order, trigger-based instances not decremented, Stun/`State`
  agreement, one write-back, nothing published, rejected action mutates nothing)
- `docs/02-technical/GAME_STATE.md` §2.3, §2.4 — `PetState` / `BossState` state
  trees; §2.4.5 — `Stunned` follows the Stun instance
- `docs/02-technical/GAME_STATE.md` §5.1 — single post-resolution write-back,
  `Sequence` gating; §0 item 4 (staging is "not yet implemented", never "not
  required"), §0 item 5 (no parallel representation)
- `docs/02-technical/ARCHITECTURE.md` §2.1 — Domain layering and framework
  independence; §5 — anti-overengineering
- `docs/03-decisions/ADR/ADR-001` — server authority
- `tasks/completed/TASK-093-resolve-status-effects-battle-state-contract.md` —
  the authored contract (read-only; not modified)
- `tasks/completed/TASK-091-resolve-status-effect-tick-timing.md` — §17 step 19a
  tick timing; §5.3 forbids generalizing the `SkillCooldown` precedent
- `tasks/completed/TASK-092-resolve-boss-skill-effect-magnitudes.md` — Burn 50
  dmg/tick for 2 Turns; Root −30% Pet ATK for 2 Turns; Drain Power instant

**ADR check:** no ADR addresses Status Effect state representation; ADR-001
(server authority) is the governing one and is satisfied — this is server-side
Domain state.

---

## Current State

The `StatusEffects[]` contract is **authored but entirely unimplemented**:

```text
GAME_STATE.md §2.3.1/§2.3.2/§2.3.3/§5.1.1   contract authored by TASK-093
COMBAT_RULES.md §5.3                        canonical rule (DR1–DR6)
GAME_RULES.md §17 step 19a                  scoping note added; position fixed

src/backend/GameServer.Domain/Battle/
  PetState.cs     record struct — no StatusEffects member; its XML doc still
                  says StatusEffects[] "belongs to the Status Effect stage and
                  is not yet implemented" (correct today)
  BossState.cs    record struct — no StatusEffects member; same staging note
  BattleState.cs  XML doc lists StatusEffects as a still-absent §2 field
  Serialization/BattleStateJson.cs
                  PetStateJson/BossStateJson explicitly document that
                  StatusEffects[] "is not yet implemented and is therefore not
                  a member"
  Serialization/BattleStateSerializer.cs   maps exactly the implemented members

tests/backend/GameServer.Domain.Tests/
  BattleStateTests.cs                 asserts StatusEffects is absent from the
                                      declared member set
  BossStateTests.cs                   asserts BossState declares no
                                      StatusEffects member
  BattleStateSerializationTests.cs    asserts StatusEffects is not written
```

No §17 step 19a pass exists anywhere in `src/` — there is no `EndTurn`,
`TickStatusEffects`, or duration-consumption call. `BattleStateService.
ResolveSwapAsync` implements the pipeline up to the Boss Skill cooldown and the
Passive charge; step 19a is absent. This is the documented staging position, not
a scope reduction (`GAME_STATE.md` §0 item 4).

---

## Scope

### In Scope

1. **`StatusEffect` Domain representation** matching `GAME_STATE.md` §2.3.1
   exactly — one element shape serving both `PetState` and `BossState`
   (§2.3.1: "identical element shape and identical lifecycle"):
   - `Id`, `Type`, `Source`, `Magnitude` — always present
   - `TargetStat` — present exactly for `Type = "BuffDebuff"`
   - exactly one of `RemainingTurns` / `ExpiryCondition`
   - the type/identity vocabularies the contract names (`"DoT"` / `"BuffDebuff"`
     / `"Shield"` / `"State"`; `"player"` / `"boss"`), typed so the two
     documented exclusivity rules are structurally unrepresentable if violated
2. **The at-most-one-instance-per-`Id` rule** (§2.3.1 item 6) expressed in the
   model and enforced by the apply/refresh operation.
3. **`StatusEffects` on `PetState` and `BossState`** as the documented
   collection — always present, empty array when no effect is active
   (never omitted, never `null`; §2.3.2 item 1, §0 item 4).
4. **The §5.1.1 lifecycle operation** as a pure Domain function, implementing
   every numbered item of `GAME_STATE.md` §5.1.1:
   - **Apply** — appends one element with `RemainingTurns = duration`
     (DR1, DR3; §5.1.1 item 1)
   - **Refresh** — re-sets `RemainingTurns = duration` on the existing instance;
     appends nothing, adds nothing to the current value (DR3; §5.1.1 item 1)
   - **Same-Turn reapplication** — resets only; no additional decrement
     (DR4; §5.1.1 item 3)
   - **Consume** — exactly one decrement per resolved Turn at §17 step 19a, for
     every active Turn-countdown instance, regardless of how many
     apply/refresh operations preceded it in that Turn (DR2)
   - **Expire** — removal in the same pass when `RemainingTurns` reaches `0`
     (DR5; §5.1.1 items 4–5); `0` is never observable in a committed state
   - **Trigger-based instances are not decremented** — an instance carrying
     `ExpiryCondition` is untouched by the pass (§5.1.1 item 7)
   - **Deterministic pass order** — by `Id` in ordinal ascending order
     (§5.1.1 item 6, §2.3.1 item 11)
5. **`BossState.State` agreement for Stun** — when a Stun instance expires,
   `State` reverts to `Idle` in the same resolution so `State` never disagrees
   with the instance's presence (§5.1.1 item 8, §2.4.5). `RemainingTurns` is
   authoritative; `State` reflects it.
6. **Step 19a wired into the resolution** at its documented position — after
   Boss Response (step 18) and as the last combat effect of the Turn, within the
   existing single post-resolution write-back and `Sequence` gate
   (`GAME_RULES.md` §17 step 19a; `GAME_STATE.md` §5.1, §5.1.1 items 9 and 11).
7. **Updating the superseded staging comments** on `PetState`, `BossState`,
   `BattleState`, and the JSON DTOs only to the extent they would otherwise
   become false. `BattleStateJson.cs` keeps documenting that `StatusEffects[]`
   is not a serialized member — that remains true after this task (see Out of
   Scope).
8. **Tests** at the depth the risk requires — see Testing Requirements.

### Out of Scope

- **Any new gameplay rule.** No new Status Effect type, no new duration
  semantics, no new tick timing, no new expiry timing, no new lifecycle state,
  no stacking model, no magnitude or duration constant
  (`GAME_STATE.md` §2.3.3; `COMBAT_RULES.md` §5.3 is the owner and is not
  restated, reinterpreted, or extended).
- **Producing instances.** No Boss Skill secondary-effect application (Root),
  no Burn application from Flame Burst, no Shield cast, no Stun application —
  no code path in this task *creates* an effect from gameplay. Only the state
  model and the operations that act on already-present instances.
- **Burn's damage tick.** Step 19a's damage-over-time tick runs through the
  Damage Pipeline (`COMBAT_RULES.md` §5.2 item 3) and is not implemented here:
  this task implements the **duration countdown and expiry**, not the tick
  damage. Burn's schedule and magnitude are unchanged
  (`BOSS_RULES.md` §6.3.1 item 1; `COMBAT_RULES.md` §5.3.4).
- **Serialization.** No `StatusEffects` member on `PetStateJson`/`BossStateJson`
  and no change to `BattleStateSerializer`. The round-trip obligation
  (`GAME_STATE.md` §2.3.2 item 5) is a separate mapping-layer task.
- **SignalR / wire.** No event, no payload member, no Hub method.
  `StatusEffects[]` is not a wire member (`GAME_STATE.md` §2.3.1 wire note,
  §2.3.3, `SIGNALR_PROTOCOL.md` §4.2/§4.3).
- **Redis.** No key, no TTL, no Redis-only field, no second storage
  representation, no concurrency change (`GAME_STATE.md` §2.3.2 item 7;
  `REDIS_STATE.md` §7 item 9 already covers the round trip in its own task).
- **PostgreSQL.** No schema, no migration, no BattleState persistence.
- **Client.** No client runtime, no Phaser presentation, no client-side
  computation of any status value (`AGENTS.md` §10, ADR-001).
- **Match-3, board, swap, match detection, cascade, gravity, combo, damage
  calculation, Boss skills, Pet skills, Passives, rewards, progression.**
- **`BossState.SkillCooldown`.** Its separate "decrements by 1 at each Turn
  increment" rule is not adopted, generalized, or touched
  (`BOSS_RULES.md` §6.3, `GAME_STATE.md` §2.4.3, `COMBAT_RULES.md` §5.3.4,
  `GAME_STATE.md` §5.1.1 item 2).
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Acceptance Criteria

- [ ] A `StatusEffect` type exists in `GameServer.Domain` whose members and
      presence rules match `GAME_STATE.md` §2.3.1 items 1–9 exactly (`Id`,
      `Type`, `Source`, `Magnitude` required; `TargetStat` present iff
      `BuffDebuff`; exactly one of `RemainingTurns` / `ExpiryCondition`).
- [ ] The mutual exclusivity of `RemainingTurns` and `ExpiryCondition` is
      structurally enforced: no constructible instance carries both or neither
      (`GAME_STATE.md` §2.3.1 item 3).
- [ ] `PetState` and `BossState` each carry a `StatusEffects` collection with the
      **same element type** — no boss-specific variant (`GAME_STATE.md` §2.3.1).
- [ ] An entity with no active effect holds an **empty** collection, never
      `null` and never an omitted member (`GAME_STATE.md` §2.3.2 item 1,
      §0 item 4).
- [ ] Apply with duration `N` sets `RemainingTurns = N` and appends exactly one
      element (`COMBAT_RULES.md` §5.3 DR1, DR3).
- [ ] Apply of an already-active `Id` refreshes that existing instance —
      `RemainingTurns` is re-set to the new duration and the collection length is
      unchanged (`GAME_STATE.md` §2.3.1 item 6, §5.1.1 item 1).
- [ ] Refresh does **not** add to the current `RemainingTurns` value, and does
      **not** change `Magnitude` (`COMBAT_RULES.md` §5.2 item 2).
- [ ] One resolved Turn consumes **exactly one** Turn of duration per active
      Turn-countdown instance (`COMBAT_RULES.md` §5.3 DR2).
- [ ] Apply and Refresh within the same Turn followed by one step 19a pass
      produces only **one** decrement (`COMBAT_RULES.md` §5.3 DR4,
      `GAME_STATE.md` §5.1.1 item 3).
- [ ] An instance at `RemainingTurns = 1` is removed by the step 19a pass, and
      `RemainingTurns = 0` is never observable in the returned state
      (`COMBAT_RULES.md` §5.3 DR5, `GAME_STATE.md` §2.3.1 item 8,
      §5.1.1 items 4–5).
- [ ] An instance carrying `ExpiryCondition` is **not** decremented and **not**
      removed by the step 19a pass (`GAME_STATE.md` §5.1.1 item 7,
      `COMBAT_RULES.md` §5.3.2).
- [ ] The consumption pass processes instances in `Id` ordinal ascending order,
      independent of array insertion order, producing identical results for
      identical input state (`GAME_STATE.md` §5.1.1 item 6, §2.3.1 item 11).
- [ ] When a Stun instance expires at step 19a, `BossState.State` is `Idle` in
      the same returned state (`GAME_STATE.md` §5.1.1 item 8, §2.4.5).
- [ ] Step 19a runs after Boss Response and is the last combat effect of the
      Turn; no resolution step is added, removed, or reordered
      (`GAME_RULES.md` §17).
- [ ] A rejected action applies, decrements, and removes nothing — step 19a does
      not run (`GAME_STATE.md` §5.1.1 item 11, §5.1 item 6).
- [ ] The lifecycle is a pure, deterministic Domain function: no RNG, no clock,
      no I/O, no framework dependency (`ARCHITECTURE.md` §2.1, `TDD.md` §6).
- [ ] The lifecycle is exercised through the existing single post-resolution
      write-back, with no second write path introduced (`GAME_STATE.md` §5.1,
      §5.1.1 item 9).
- [ ] No `StatusEffects` member is added to any JSON DTO and
      `BattleStateSerializer` is unchanged in behavior.
- [ ] No SignalR method, event, or payload member is added.
- [ ] No Redis key, TTL, field, or concurrency semantic is added or changed.
- [ ] No PostgreSQL schema or migration is added.
- [ ] `BossState.SkillCooldown`'s decrement behavior is unchanged
      (`GAME_STATE.md` §2.4.3, §5.1.1 item 2).
- [ ] `GameServer.Domain` gains no reference to ASP.NET Core, SignalR, EF Core,
      Redis, HTTP, Phaser, or Discord (`ARCHITECTURE.md` §2.1).
- [ ] All relevant tests pass at the required validation depth
      (`core/validation.md` §2).
- [ ] Quality review checklist passes (`quality/review.md` §1).
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

### Explicit Constraints

```text
No gameplay expansion.
No PostgreSQL BattleState persistence.
No undocumented Redis behavior.
No undocumented SignalR methods.
No client-authoritative state.
No new StatusEffect semantics.
```

---

## Affected Files & Areas

```text
[x] src/backend/GameServer.Domain/Battle/ (StatusEffect type; PetState; BossState;
                                            the §5.1.1 lifecycle operation)
[x] src/backend/GameServer.Application/ (step 19a call site within the existing
                                          Swap resolution sequence only —
                                          no game rule logic added)
[ ] src/backend/GameServer.Infrastructure/ (none — no Redis, no PostgreSQL)
[ ] src/backend/GameServer.Api/ (none — no endpoint, no Hub method, no wire member)
[ ] src/frontend/client/ (none)
[x] tests/backend/GameServer.Domain.Tests/ (StatusEffect + lifecycle unit tests
                                             and gameplay scenarios)
[x] tests/backend/GameServer.Application.Tests/ (step 19a position + write-back;
                                                  only if the call site changes
                                                  observable Application behavior)
[ ] docs/ (none required — the contract is already authored by TASK-093;
          report instead if implementation reveals a documentation gap, per
          AGENTS.md §17)
```

---

## Implementation Notes

- **Domain ownership is settled.** `GAME_STATE.md` §2.3/§2.4 own the state tree;
  `COMBAT_RULES.md` §5.3 owns the rule. The new type belongs beside
  `PetState`/`BossState` in `GameServer.Domain/Battle/` — the same module and
  the same "deliberately minimal, framework-independent" standard
  (`ARCHITECTURE.md` §2.1).
- **One element shape, two owners.** `GAME_STATE.md` §2.3.1 states the schema
  serves `PetState.StatusEffects[]` and `BossState.StatusEffects[]` identically.
  Do not branch on the owning entity inside the type.
- **The operation is a pure function.** The §5.1.1 lifecycle is an apply/refresh
  operation and one consumption pass — a static, side-effect-free Domain
  function over the collection, in the shape `PassiveTracker.Charge` and
  `DamagePipeline.Calculate` already use. `SwapExecutor.Execute` and
  `BattleStateService.ResolveSwapAsync` sequence such calls; the rule itself
  lives in Domain.
- **Do not re-derive the rule.** `COMBAT_RULES.md` §5.3's DR1–DR6 are cited by
  identifier in code comments, not restated as new prose, matching how
  `PetState` cites `COMBAT_RULES.md` §1.1 and how `BossState` cites
  `BOSS_RULES.md` §6.
- **`RemainingTurns` is an `int` and never nullable** (§2.3.1 item 4). Zero is a
  real value that never survives a commit (item 8).
- **`Magnitude` is typed but not interpreted** (§2.3.1 item 2): store the value,
  apply no meaning to it. The effect's rules stay in their owning document.
- **Instant effects create no instance** (§2.3.1 item 9): Drain Power is a
  `PetState.Power` mutation. Do not add a zero-duration representation for it.
- **Ordering is not semantic** (§2.3.1 item 10) — but the pass order is fixed by
  §5.1.1 item 6. Sort for the pass; do not make array position meaningful.
- **No pending collection** (§2.3.3): an application mid-resolution is Transient
  Resolution State (§3) until the write-back. Do not add a queue.
- **Existing tests will need updating, and that is expected.** `BattleStateTests`,
  `BossStateTests`, and `BattleStateSerializationTests` currently assert
  `StatusEffects` is *absent*. Those assertions encode the staging position this
  task removes. Update them to assert the new member's presence and contract —
  do not weaken or delete a contract assertion to make the change compile
  (`AGENTS.md` §15).
- **Step 19a is a position, not a new step.** `GAME_RULES.md` §17 step 19a
  already exists and already carries its scoping note. Place the call at that
  position; add no step and reorder nothing.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — StatusEffect presence/exclusivity rules; apply; refresh;
                         consume; expiry; trigger-based exclusion; pass order
[x] Integration tests  — step 19a at its documented position in the Swap
                         resolution, via the existing write-back path
[x] Gameplay scenarios — the worked examples of COMBAT_RULES.md §5.3.3 and the
                         GAME_STATE.md §5.1.1 lifecycle, as Given/When/Then
```

### Required Scenarios (derived from `COMBAT_RULES.md` §5.3.3)

```text
Given an effect with duration = 2 applied during Turn N
When  Turn N resolves
Then  RemainingTurns is 1
And   the instance is still present
When  Turn N+1 resolves
Then  the instance is removed (RemainingTurns reached 0)
And   it is not active during Turn N+2

Given an effect with duration = 2 applied during Turn N
When  it is refreshed during Turn N+1
Then  RemainingTurns is re-set to 2 at the refresh
And   that Turn's step 19a leaves it at 1
And   it expires at Turn N+2's step 19a

Given an effect with duration = 1 applied during Turn N
When  Turn N resolves
Then  the instance is removed in that same pass
And   it is inactive from Turn N+1

Given an effect with duration = 2 applied during Turn N
When  it is applied and refreshed again within that same Turn N
Then  RemainingTurns is 2 after the refresh (reset, not incremented)
And   that Turn's step 19a consumes exactly one unit, leaving 1
```

### Key Edge Cases

- `RemainingTurns = 1` decrements to `0` and is removed in the same pass
  (`GAME_STATE.md` §5.1.1 item 5) — the single most failure-prone case.
- Same-Turn apply + refresh + step 19a produces exactly one decrement
  (`COMBAT_RULES.md` §5.3 DR4) — asserted as a count, not only as a value.
- Multiple active instances (`Burn` and `Root` simultaneously) each decrement
  exactly once, in `Id` ordinal ascending order, independent of insertion order
  (`GAME_STATE.md` §5.1.1 item 6; TASK-093's edge-case list).
- An `ExpiryCondition` instance (Shield) survives every step 19a pass untouched
  (`GAME_STATE.md` §5.1.1 item 7).
- Stun expiry reverts `BossState.State` to `Idle` in the same resolution
  (`GAME_STATE.md` §5.1.1 item 8, §2.4.5).
- A rejected Swap leaves the collections, `Turn`, and `Sequence` untouched and
  runs no step 19a (`GAME_STATE.md` §5.1.1 item 11).
- An entity with no active effect holds an empty collection, not `null`
  (`GAME_STATE.md` §2.3.2 item 1).
- Determinism: the same input state produces the same output state across runs
  (`TDD.md` §6).

### Explicitly Not Tested Here

- Serialization round-trip of `StatusEffects[]` — no serializer member exists in
  this task (`GAME_STATE.md` §2.3.2 is a later task's obligation). The existing
  "not written" assertion remains valid and must keep passing.
- Burn's damage-per-tick value — the Damage Pipeline tick is not implemented
  here.
- Any gameplay path that *applies* an effect — no producer exists yet.

---

## Stop Conditions

Universal `AGENTS.md` §20 stops always apply. Task-specific:

- If the required behavior cannot be fully derived from the Authoritative
  References: STOP per `AGENTS.md` §7.
- If implementing the lifecycle requires choosing a duration, magnitude,
  tick point, expiry point, or scope rule that `COMBAT_RULES.md` §5.3 does not
  already state: STOP — that is a new gameplay decision, not an implementation
  detail.
- If implementing step 19a would require adding, removing, or reordering a
  `GAME_RULES.md` §17 step, or changing Burn's tick, or adopting
  `SkillCooldown`'s decrement rule for Status Effects: STOP per `AGENTS.md` §4.
- If the existing `StatusEffects`-absence assertions cannot be reconciled with
  the authored contract without deleting a contract assertion: STOP and report
  the conflict rather than weakening the test (`AGENTS.md` §15).
- If implementation requires a serializer member, a wire member, a Redis field,
  or a PostgreSQL change to be meaningful: STOP — those are separate tasks'
  acts (`GAME_STATE.md` §2.3.2 item 7, §2.3.3).
- If the state model cannot be implemented without representing a field no rule
  reads: STOP per `GAME_STATE.md` §0 item 5.
- If any `src/frontend/` change appears necessary: STOP per `AGENTS.md` §10.
- If the task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: STOP & decompose.

---

## Completion Evidence

### Changed Files

**Created (Domain):**

```text
src/backend/GameServer.Domain/Battle/StatusEffectType.cs        Type/Source enums
                                                                (§2.3.1 items 3, 7)
src/backend/GameServer.Domain/Battle/StatusEffect.cs            instance schema
                                                                (§2.3.1; two factories)
src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs   Apply/ConsumeAtStep19a
                                                                (§5.1.1; §5.3 DR1–DR6)
```

**Modified (Domain / Application):**

```text
src/backend/GameServer.Domain/Battle/PetState.cs        ActiveStatusEffects member,
                                                        AtBattleCreation parameter,
                                                        docs tree + staging note
src/backend/GameServer.Domain/Battle/BossState.cs       ActiveStatusEffects member,
                                                        docs tree + staging note
src/backend/GameServer.Application/Battle/BattleStateService.cs
                                                        §17 step 19a call site, in the
                                                        documented position, on both
                                                        terminal and non-terminal paths
```

**Created (tests):**

```text
tests/backend/GameServer.Domain.Tests/StatusEffectTests.cs           instance contract (20 tests)
tests/backend/GameServer.Domain.Tests/StatusEffectLifecycleTests.cs  lifecycle (21 tests)
```

**Modified (tests — staging assertions that the new contract supersedes):**

```text
tests/backend/GameServer.Domain.Tests/BattleStateTests.cs            PetState member list;
                                                                     StatusEffects removed
                                                                     from the "not yet
                                                                     implemented" list
tests/backend/GameServer.Domain.Tests/BossStateTests.cs              BossState member list;
                                                                     "no deferred field"
                                                                     now asserts PRESENT
tests/backend/GameServer.Application.Tests/BattleStateServiceTests.cs
                                                                     5 step-19a integration
                                                                     tests added
tests/backend/GameServer.Application.Tests/BattleStateSerializationLifecycleTests.cs
                                                                     BossState comparisons now
                                                                     assert the collection
                                                                     structurally
```

### Validation Results

```text
GameServer.Domain.Tests          991 passed / 0 failed  (was 950; +41 new)
GameServer.Application.Tests     377 passed / 0 failed  (was 372; +5 new)
GameServer.Infrastructure.Tests  297 passed / 0 failed  (unchanged)
GameServer.Api.Tests             253 passed / 1 failed
```

The single Api failure is **pre-existing and environmental**, not caused by
this task: `BattleResultSmokeTest.SmokeTest_AuthoritativeBattleActionToResultRead_
ShouldWalkTheWholeDocumentedPath` fails with

```text
System.InvalidOperationException : Cannot open log for source '.NET Runtime'.
You may not have write access.  ---- System.ComponentModel.Win32Exception :
Access is denied.
```

It fails identically on the unmodified baseline (verified by stashing this
task's changes and re-running the test), so it is a Windows Event Log
permission condition in the test environment.

### Acceptance Criteria Verification

| Criterion | Result | Evidence |
|---|---|---|
| `StatusEffect` matches §2.3.1 items 1–9 | PASS | `StatusEffect.cs` |
| `RemainingTurns`/`ExpiryCondition` exclusivity structural | PASS | two factories; `Instance_ShouldNeverCarryBothDurationModels` |
| Same element type on both entities | PASS | one type; `ConsumeAtStep19a_ShouldConsumeBothPetAndBossCollections` |
| Empty collection, never `null` | PASS | `ActiveStatusEffects`; `Lifecycle_ShouldRejectANullCollection` |
| Apply sets `RemainingTurns = duration` | PASS | `Apply_ShouldInitializeDurationOnAnEmptyCollection` |
| Apply of active `Id` refreshes in place | PASS | `Apply_ShouldRefreshInPlaceRatherThanAppendASecondInstance` |
| Refresh does not add to current value | PASS | `Refresh_ShouldNotAddToTheCurrentValue` |
| Exactly one decrement per resolved Turn | PASS | `ConsumeAtStep19a_ShouldDecrementEachActiveInstanceExactlyOnce` |
| Same-Turn apply+refresh = one decrement | PASS | `SameTurnApplyAndRefresh_ShouldCauseExactlyOneDecrement` |
| `duration=1` expires at 19a; 0 unobservable | PASS | `DurationOne_ShouldExpireAtTheApplicationTurnsStep19a` |
| `ExpiryCondition` not decremented | PASS | `ConsumeAtStep19a_ShouldNotDecrementATriggerBasedInstance` |
| `Id` ordinal ascending pass order | PASS | `ConsumeAtStep19a_ShouldProcessInstancesInIdAscendingOrder` |
| Stun expiry → `BossState.State = Idle` | PASS | `ConsumeAtStep19a_ShouldRevertBossStateToIdleWhenStunExpires` |
| Step 19a position; no step added/reordered | PASS | `BattleStateService.cs`; `ExecuteSwap_ShouldRunStep19aAtItsDocumentedPositionInTheResolution` |
| Rejected action runs no 19a | PASS | `ExecuteSwap_ShouldNotRunStep19aForARejectedAction` |
| Pure, deterministic Domain function | PASS | no RNG/clock/I/O in `StatusEffectLifecycle.cs` |
| Single write-back, no second write path | PASS | call site sits before the existing return |
| No DTO / serializer member added | PASS | `git diff` on `Serialization/` is empty |
| No SignalR / Redis / PostgreSQL change | PASS | `git diff` on `Api/`, `Infrastructure/` is empty |
| `SkillCooldown` decrement unchanged | PASS | `ConsumeAtStep19a_ShouldChangeNothingElseInTheState` |

### Implementation Deviation (reported, not silent)

One decision was **not** anticipated by the task text and is reported here per
`AGENTS.md` §16 / §21 rather than being applied silently.

**What changed.** `StatusEffects` is exposed as
`ActiveStatusEffects` — a non-nullable `StatusEffect[]` initialized to `[]` —
rather than as the nullable positional record parameter the task's
"Affected Files" implied. The nullable parameter was removed after it was
found to collide with the contract.

**Why.** `GAME_STATE.md` §2.3.2 item 1 states the collection "always exists
(§0 item 4), so it is never omitted and never `null`". A nullable positional
member contradicted that in two ways:

1. **Value equality.** A record's synthesized equality compares an array member
   by reference, so a state holding `[]` and a state holding `null` compared
   **unequal** even though item 1 makes them the same documented state. This
   was not hypothetical: `BattleStateSerializationLifecycleTests` failed on the
   round trip for exactly this reason, because the serializer does not carry
   `StatusEffects[]` (this task does not add it) and therefore rebuilt `null`.

2. **Representability.** Keeping `null` constructible would have preserved a
   third state — "unset" — that item 1 does not admit.

Making the member non-nullable and defaulting it to `[]` removes the third
state entirely, so `null` cannot enter and the two spellings of "no active
effect" are one value. This is the smallest change that satisfies item 1; it
adds no rule and no behavior.

**Consequential test edits.** Four existing tests compared whole `BossState`
structs. Because that comparison now includes a non-nullable array member, they
were changed to assert the collection structurally
(`StatusEffectsEqual`, mirroring the existing `BoardState.CellsEqual` idiom of
§2.1.7 item 5) plus the individual value members. **No assertion was weakened
or deleted** — the replacements are strictly more specific, and the aggregate
they previously relied on is preserved field by field.

**Note on `AGENTS.md` §15.** The five modified tests were staging assertions
("`StatusEffects` is not yet implemented") that this task's contract
supersedes, which TASK-095 §Implementation Notes anticipated. Each replacement
asserts presence and contract rather than absence; none was weakened to make
the implementation compile.

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic (no `src/frontend/` file changed)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1 — Burn/Shield/Buff-Debuff are IN)
- [x] Confirmed no new StatusEffect semantics were introduced
      (`COMBAT_RULES.md` §5.3 implemented as written, not reinterpreted)
- [x] Confirmed no serializer, wire, Redis, or PostgreSQL change
- [x] Confirmed TASK-093, TASK-091, TASK-092, TASK-094, TASK-079 were not modified
- [x] Confirmed `BossState.SkillCooldown` behavior unchanged
- [x] Confirmed `GAME_RULES.md` §17 order unchanged (no step added, removed, or reordered)
- [x] Confirmed no gameplay effect is produced (no Root/Burn/Shield/Stun application;
      the integration tests seed instances directly, because producing them is a
      downstream task's act)
- [x] Confirmed no documentation was modified (the contract was authored by TASK-093)

