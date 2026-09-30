# TASK-093 — Resolve StatusEffects Battle State Contract

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and tasks/ by path and section.

  THIS TASK RESOLVES EXACTLY ONE ARCHITECTURAL/STATE CONTRACT:
  The state representation, lifecycle, and serialization schema for
  `StatusEffects[]` under `PetState` and `BossState` in `GAME_STATE.md` §2.3, §2.4, §2.5.

  IT IS A DOCUMENTATION CONTRACT TASK. It does NOT implement code.
-->

---

## Metadata

```text
Task ID:           TASK-093
Type:              DOCUMENTATION
Status:            DONE (the §11 blocking decision was delivered by TASK-094 as
                    DR1–DR6 and is now applied. `StatusEffects[]` is
                    contract-defined in `GAME_STATE.md` §2.3.1–§2.3.3 and §5.1.1;
                    the canonical gameplay rule is `COMBAT_RULES.md` §5.3. No
                    source code was implemented — that is a later task.)
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     review
Supporting Agents: gameplay, backend
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
Dependencies:      TASK-091, TASK-092
Blocks:            Status Effects and Boss Skill implementation tasks
```

---

## Objective

Define and record the authoritative data contract, instance schema, lifecycle rules (application, decrement, expiry), and serialization representation for `StatusEffects[]` in `PetState` and `BossState` within `docs/02-technical/GAME_STATE.md` (§2.3, §2.4; the serialization contract is at §2.3.2 because no §2.5 heading exists — see the Authoritative References note), fully aligning with the resolved Status Effect timing (`GAME_RULES.md` §17 step 19a, TASK-091), the canonical duration rule (`COMBAT_RULES.md` §5.3, TASK-094 DR1–DR6), and Boss Skill effect contracts (`BOSS_RULES.md` §6.3.1, TASK-092).

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Combat, Bosses, and Status Effects in MVP scope
- `docs/01-game-design/COMBAT_RULES.md` §5, §5.1, §5.2 — Status Effects rules (Burn, Buff/Debuff, Shield, stacking/refresh rule)
- `docs/01-game-design/GAME_RULES.md` §17 — Turn Resolution Order (step 18b Boss Skill, step 19a Status Effect tick)
- `docs/01-game-design/BOSS_RULES.md` §6.3.1 — Boss Skill secondary effect contracts (Flame Burst Burn, Root ATK debuff, Drain Power instant reduction)
- `docs/02-technical/GAME_STATE.md` §2.3, §2.4, §5.1 — Active battle state tree, `StatusEffects[]` contract, single write-back rules (see the §2.5 note below)
- `tasks/completed/TASK-091-resolve-status-effect-tick-timing.md` — Status Effect tick timing at End Turn step 19a
- `tasks/completed/TASK-092-resolve-boss-skill-effect-magnitudes.md` — Boss Skill effect magnitudes and duration definitions
- `tasks/backlog/TASK-094-resolve-buff-debuff-duration-consumption-timing.md` — **the §11 blocking decision, delivered as DR1–DR6 (Status: READY). Consumed as settled input; not modified by this task.**

**Note on `GAME_STATE.md` §2.5 (cited above).** Every reference to a "§2.5" location in this task's original text is stale: `GAME_STATE.md` has **no §2.5 heading** — §2.4 is followed by §2.6 as an intentional historical gap (see §11 "Explicitly resolved"). This task must not invent that heading. The serialization contract was therefore authored at **§2.3.2**, adjacent to the schema it describes. See §12 note 1.

---

## Scope

### In Scope

- Defining the `StatusEffect` instance member schema for `PetState.StatusEffects[]` and `BossState.StatusEffects[]` in `GAME_STATE.md` §2.3 and §2.4:
  - Instance fields: `id` / `type` (e.g., `"Burn"`, `"Debuff"`, `"Buff"`, `"Shield"`), `targetStat` (for Buff/Debuff, e.g. `"ATK"`), `magnitude` (numeric value), `remainingTurns` / `duration` (integer turn count), and `source` (`"player"` / `"boss"`).
- Defining the lifecycle mutation contract in `GAME_STATE.md` §5.1:
  - Application of status instances during combat resolution (e.g. step 18b Boss Skill application).
  - Decrement and expiration mechanics during End Turn resolution (step 19a / End Turn decrement).
  - Stacking and refresh mechanics in state: applying an existing effect refreshes duration and does not stack magnitude (`COMBAT_RULES.md` §5.2 item 2).
- Defining the JSON serialization and round-trip schema in `GAME_STATE.md` §2.5.
- Ensuring strict compatibility with TASK-091 (End Turn step 19a tick) and TASK-092 (Flame Burst Burn 50 dmg/tick for 2 Turns; Root -30% Pet ATK for 2 Turns; Drain Power instant -20 Power).

### Out of Scope

- Implementing backend domain classes, services, or status-effect processors in `src/backend/`.
- Implementing frontend / Phaser visual effect presentation in `src/frontend/client/`.
- Modifying SignalR broadcast schemas or designing new SignalR events (wire transport delivery remains a separate concern).
- Modifying REST API endpoints or database persistence schemas.
- Modifying Boss Passives or base stats.

---

## Current State

`GAME_STATE.md` §2.3, §2.4, and §2.5 currently list `StatusEffects[]` as deferred / not yet implemented. While game rules (`COMBAT_RULES.md` §5, `GAME_RULES.md` §17, `BOSS_RULES.md` §6.3.1) define the mechanics, timing, and magnitudes for Burn and Root, the technical state shape, serialization, and lifecycle update semantics in `GAME_STATE.md` are not yet authored.

---

## Acceptance Criteria

- [x] `StatusEffects[]` instance schema is explicitly documented in `GAME_STATE.md` §2.3 (`PetState`) and §2.4 (`BossState`). (§2.3.1; §2.4 references it — same schema)
- [x] Field names, types, and nullability for status effect instances are explicitly specified. (§2.3.1 items 1–7; §2.3.2 item 3)
- [x] Stacking/refresh state rules in `GAME_STATE.md` match `COMBAT_RULES.md` §5.2 item 2 (refresh duration, do not stack magnitude). (§2.3.1 item 6, §5.1.1 item 1)
- [x] Turn decrement and expiration lifecycle in `GAME_STATE.md` §5.1 matches `GAME_RULES.md` §17 step 19a and TASK-091/092. (§5.1.1; the rule itself is `COMBAT_RULES.md` §5.3)
- [x] JSON serialization schema for `StatusEffects[]` is explicitly defined. (§2.3.2 — placed there rather than an invented §2.5, see §12 note 1)
- [x] No source code is implemented (`src/` remains untouched).
- [x] No SignalR wire protocol or API contracts are modified.
- [x] No database schema changes are introduced.
- [x] Documentation is internally consistent with `COMBAT_RULES.md`, `GAME_RULES.md`, and `BOSS_RULES.md`.

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[ ] tests/ (unit / integration / gameplay scenarios)
[x] docs/ (docs/02-technical/GAME_STATE.md)
```

---

## Implementation Notes

- Canonical domain owner for battle state structure is `docs/02-technical/GAME_STATE.md`.
- `StatusEffects[]` lives under `PetState` for player-side active Pet status effects, and under `BossState` for boss-side status effects (`ADR-011`, `GAME_STATE.md` §2.3, §2.4).
- The state contract must remain deterministic and adhere to single write-back rules (`GAME_STATE.md` §5.1).

---

## Testing Requirements

### Required Verification

```text
[ ] Documentation consistency audit across GAME_STATE.md, COMBAT_RULES.md, BOSS_RULES.md, and GAME_RULES.md
[ ] Scope validation against MVP_SCOPE.md §1
```

### Key Edge Cases

- Status effect expiry: an effect with `remainingTurns = 1` decrements to 0 and is removed from `StatusEffects[]` upon End Turn resolution.
- Multiple active status effects on the same entity (e.g. Burn and Root active simultaneously).
- Instant non-duration effects (e.g. Drain Power) must not create a persistent entry in `StatusEffects[]`.

---

## Stop Conditions

- If required StatusEffect types exceed MVP scope (Burn, Shield, Buff/Debuff): STOP per `AGENTS.md` §8.
- If proposed serialization conflicts with existing `BattleStateJson` root member conventions: STOP per `AGENTS.md` §4 / §18.
- If duration decrement model conflicts with TASK-091 / `GAME_RULES.md` §17: STOP per `AGENTS.md` §4 / §20.

---

## 11. STOP CONDITION REPORT

> **RESOLVED.** The blocking decision was delivered by **TASK-094** as
> **DR1–DR6** and has been applied by this task. The original STOP report is
> retained below unaltered as the historical record of why the contract could
> not be authored earlier. See §12 for the resolution and the contract that was
> authored.

```text
STOP — TASK-093 cannot safely resolve the StatusEffects state contract.
```

**No documentation was modified.** `docs/02-technical/GAME_STATE.md` is
unchanged. The ambiguity below must be resolved by a gameplay/product
decision before the contract can be authored.

### Problem

`COMBAT_RULES.md` §5.2 item 1 requires every Status Effect to carry "a
duration (in Turns)", but no authoritative document states **when that
duration decrements relative to application** for a non-DoT Status Effect.
Burn's schedule is fully pinned by `BOSS_RULES.md` §6.3.1; Root's is not. A
`remainingTurns` field therefore cannot be defined without choosing a
decrement rule the docs do not state — which fixes both the field's meaning
and Root's real-world lifespan.

### Sources

```text
COMBAT_RULES.md §5.1        Burn ticks once per resolved Turn at End Turn
                            (GAME_RULES.md §17 step 19a); Buff/Debuff is
                            "temporary stat modification ..., with duration
                            measured in Turns unless stated otherwise"
COMBAT_RULES.md §5.2 item 1 "Every Status Effect has a source, a magnitude,
                            and a duration (in Turns) or a trigger-based
                            expiry"
COMBAT_RULES.md §5.2 item 2 stacking = "refresh duration, do not stack
                            magnitude" (decrement point still unstated)

BOSS_RULES.md §6.3.1 item 1 Burn: "2 Turns = exactly 2 End Turn ticks ...
                            tick #1 at Turn N step 19a ... tick #2 at Turn
                            N+1 step 19a, after which the Burn expires
                            before Turn N+2"
BOSS_RULES.md §6.3.1 item 3 Root: "-30% Pet ATK debuff ... Root Duration:
                            2 Turns using the authoritative Turn model"
                            (no tick, no decrement, no expiry point)

BOSS_RULES.md §6.3          "Decrements by 1 at each Turn increment" —
                            scoped to BossState.SkillCooldown only
GAME_STATE.md §2.4.3        same, scoped to SkillCooldown
GAME_STATE.md §2.3/§2.4     StatusEffects[] present, "not yet implemented"
BOSS_RULES.md §5 item 5     Stun duration "tracked by StatusEffects[]"
                            (no decrement rule either)
```

### Conflict / missing information

The same subsection (`BOSS_RULES.md` §6.3.1) specifies **two different
levels of detail** for the same "2 Turns" duration:

```text
Burn  → explicit tick schedule; 2 Turns = exactly 2 End Turn ticks,
        with named ticks and an explicit expiry point.
Root  → "2 Turns using the authoritative Turn model" and nothing more.
```

Two materially different `remainingTurns` semantics are both consistent with
that text, and they produce different gameplay:

```text
Reading 1 — "remainingTurns = remaining End Turn decrements"
   Applied at Turn N step 18b -> decremented at step 19a of Turn N and
   Turn N+1 -> expires before Turn N+2. Root affects Turns N and N+1.
   This is Burn's documented model, extended to Root by analogy.

Reading 2 — "2 Turns = the Turn of application plus one more"
   Root would instead expire after Turn N+1's End Turn with no decrement
   on Turn N, leaving it in force for part or all of Turn N+2.
```

`COMBAT_RULES.md` §5.2 item 1 states a duration exists but not the decrement
point; §5.2 item 2 fixes reapplication (refresh, not stack) without fixing
when the refreshed counter is consumed. `GAME_RULES.md` §17 step 19a defines
the tick for **damage-over-time only** ("tick each active damage-over-time
Status Effect"), so it does not by itself rule on a Buff/Debuff duration.

`TASK-091` §5.3 explicitly forbids the one precedent that would settle this:
"**The SkillCooldown precedent.** ... That is a specific counter rule.
Generalizing it to Status Effects invents a rule not in the docs." The same
paragraph bars deciding by implementation convenience.

### Impact

Blocked acceptance criteria (all depend on the decrement point):

```text
[ ] Root duration is deterministic
[ ] Burn duration is deterministic (tick schedule is known; the
    remainingTurns field semantics are not)
[ ] Reapplication/refresh behavior is explicitly defined
[ ] Expiration behavior is explicit
[ ] PetState.StatusEffects[] / BossState.StatusEffects[] schema
    (a remainingTurns field cannot be typed or defined)
[ ] Serialization / round-trip behavior
```

Not blocked, and verifiable today: Burn's tick cadence, Drain Power's
classification as an immediate mutation, and the server-authority /
no-wire-change boundaries.

### Explicitly resolved (verified, not blocked)

```text
Burn tick timing      GAME_RULES.md §17 step 19a, one tick per resolved
                      Turn (TASK-091). Not changed by this task.
Burn magnitude        50 fixed damage/tick, 2 Turns (BOSS_RULES.md §6.3.1
                      item 1). Not changed.
Root magnitude        -30% Pet ATK (BOSS_RULES.md §6.3.1 item 3).
Drain Power           Immediate mutation, NOT a StatusEffect —
                      BOSS_RULES.md §6.3.1 item 2: "Duration: None
                      (instant stat reduction, not a persistent status
                      effect)"; PetState.Power = max(0, Power - 20).
Reapplication         COMBAT_RULES.md §5.2 item 2 — refresh duration, do
                      not stack magnitude.
Serialization         GAME_STATE.md §2.1.7 item 5 + REDIS_STATE.md §7
                      item 9 already own the round-trip obligation.
Ownership             Server-authoritative (GAME_RULES.md §18, ADR-001).
No §2.5               GAME_STATE.md has no §2.5 heading; §2.4 is followed by
                      §2.6 as an intentional historical gap. No heading was
                      invented.
```

### Proposed resolution

The smallest change that would resolve it — **a gameplay decision, not a
documentation edit this task may make on its own**. Choose one:

```text
Option 1  All Status Effects decrement at GAME_RULES.md §17 step 19a,
          alongside the DoT tick. Burn and Root then share one model.
          This is Burn's documented behavior generalized to Root.

Option 2  Buff/Debuff durations decrement at a different named point
          (e.g. Turn increment, per the SkillCooldown precedent), which
          requires stating that rule explicitly for Status Effects.
```

On decision, the owning design document is `COMBAT_RULES.md` §5.2 (the
canonical owner of Status Effect rules); `BOSS_RULES.md` §6.3.1 item 3 would
then cross-reference it rather than restate it
(`.ai/workflow/documentation/documentation-change.md` §2). Only after that
can `GAME_STATE.md` §2.3/§2.4 define `StatusEffects[]` deterministically.

### Waiting for

A gameplay/product decision selecting Option 1 or Option 2 (or a third
ruling) for the Status Effect duration decrement point. No contract is
partially invented pending that decision.


---

## 12. RESOLUTION

### Blocking decision delivered

`tasks/backlog/TASK-094-resolve-buff-debuff-duration-consumption-timing.md`
(Status: READY) supplied the gameplay/product decision as **DR1–DR6**, and the
Product Owner selected `GAME_RULES.md` §17 step 19a as the consumption point
(§4 interaction item **i**). These were treated as **settled input** and were
neither reopened nor reinterpreted.

### Where the decision was applied

The decision required two distinct applications, because the *gameplay rule*
and the *state contract* have different canonical owners
(`.ai/workflow/documentation/documentation-change.md` §2–§3):

```text
COMBAT_RULES.md §5.3   canonical owner of the GAMEPLAY RULE
                       (when one Turn of duration is consumed)
        ↓
GAME_RULES.md §17 19a  the resolution position it runs at (scoping note only)
        ↓
GAME_STATE.md §2.3.1   the STATE CONTRACT (instance schema)
GAME_STATE.md §2.3.2   serialization / round-trip
GAME_STATE.md §2.3.3   what the model does not add
GAME_STATE.md §5.1.1   the lifecycle mutation contract
        ↓
BOSS_RULES.md §6.3.1 item 3   reference update (Root duration)
```

`COMBAT_RULES.md` §5.2 was **not** renumbered or rewritten; §5.3 was added as a
new subsection so the existing §5.2 items 1–3 and their numbering are
untouched. `COMBAT_RULES.md` §5.3 is now the single owner of the duration rule;
`GAME_STATE.md` §5.1.1 references it and does not restate it.

### Final `StatusEffects[]` contract

Defined in `GAME_STATE.md` §2.3.1 (schema), §2.3.2 (serialization), §2.3.3
(boundaries), and §5.1.1 (lifecycle). Element shape:

```text
StatusEffect
├── Id              string    required    ("Burn" | "Root" | "Shield" | "Stun")
├── Type            string    required    ("DoT"|"BuffDebuff"|"Shield"|"State")
├── Source          string    required    ("player" | "boss")
├── Magnitude       number    required
├── TargetStat      string    optional    (iff Type = "BuffDebuff")
├── RemainingTurns  integer   optional    (iff the instance uses the Turn countdown)
└── ExpiryCondition string    optional    (iff it does not)
```

The same schema serves `PetState.StatusEffects[]` and
`BossState.StatusEffects[]` — one element shape, one lifecycle, no
boss-specific variant. `RemainingTurns` and `ExpiryCondition` are mutually
exclusive: every instance uses exactly one duration model, which expresses
`COMBAT_RULES.md` §5.2 item 1's "a duration (in Turns) **or** a trigger-based
expiry" as state.

### §17 step 19a interaction

Resolved explicitly and consistently, with no new step and no change to Burn:

- Step 19a **remains one step in one fixed position**. No resolution step was
  added, removed, or reordered (`GAME_RULES.md` §17 is canonical).
- Step 19a now carries a scoping note stating it is also the single
  duration-consumption point for Turn-based Buff/Debuff effects, and pointing
  at `COMBAT_RULES.md` §5.3 as the owner. The note adds no rule of its own.
- Burn's meaning is **unchanged**: its tick schedule (2 Turns = exactly 2 End
  Turn ticks, expiry before Turn N+2 — `BOSS_RULES.md` §6.3.1 item 1) is
  preserved verbatim and is consistent with DR1–DR5; it was not rewritten,
  re-scoped, or extended to non-DoT effects.
- `BossState.SkillCooldown`'s "decrements by 1 at each Turn increment" rule is
  **not** generalized to Status Effects (`COMBAT_RULES.md` §5.3.4,
  `GAME_STATE.md` §5.1.1 item 2). TASK-091 §5.3's prohibition is respected.
- No undocumented application point was created. `COMBAT_RULES.md` §5.3.1
  records that all documented apply sites precede step 19a and that a future
  post-19a site would require its own explicit decision.

### Notes on scope decisions made during this task

1. **§2.5 does not exist and was not created.** The Objective and Acceptance
   Criteria named `GAME_STATE.md` §2.5 for serialization, but §2.4 is followed
   by §2.6 as an **intentional historical gap** (TASK-093 §11 "Explicitly
   resolved"). Inventing a §2.5 heading would have filled a gap the repository
   deliberately preserves. The serialization contract was therefore placed at
   **§2.3.2**, alongside the schema it describes, and the gap is untouched.
2. **`COMBAT_RULES.md` was modified, although "Affected Files" listed only
   `GAME_STATE.md`.** §11 of this task directed that "the owning design
   document is `COMBAT_RULES.md` §5.2 … Only after that can `GAME_STATE.md`
   §2.3/§2.4 define `StatusEffects[]` deterministically." Applying DR1–DR6
   therefore required it. `BOSS_RULES.md` §6.3.1 item 3 was updated only to
   cross-reference the new canonical section (per §11 and
   `documentation-change.md` §2, no duplication).
3. **No Redis/`DATABASE.md` change.** `GAME_STATE.md` §2.3.2 item 7 records
   that no Redis-only field, key, or record is introduced, so
   `REDIS_STATE.md` needs no edit; its §7 item 9 round-trip obligation already
   covers `BattleState` contents generally.
4. **Implementation remains deferred.** The contract is authored; no
   `StatusEffects[]` implementation, no wire member, and no Redis behavior was
   added. §2.4.1 and §2.3 now state "contract-defined, not yet implemented"
   so the staged status stays honest.

---

## Completion Evidence

The §11 blocking decision was received (TASK-094 DR1–DR6) and the contract has
been authored, so this section is now a completion record. The pre-resolution
blocked evidence is preserved in the collapsed note below.

<!--
  PRIOR STATE (retained for history) — TASK-093 was BLOCKED (see §11) and this
  section recorded the evidence of the blocked investigation, not a completion
  claim. That blocker is now resolved; see §12.
-->

### Status Before Resolution

BLOCKED — gameplay/product decision required.

### Changed Files Before Resolution

None. No authoritative documentation was modified at that time.

### Changed Files

```text
docs/02-technical/GAME_STATE.md    §2.3.1, §2.3.2, §2.3.3 added;
                                   §2.3 tree entry + narrative updated;
                                   §2.4 tree entry, §2.4.1, §2.4.5 updated;
                                   §5.1.1 added; version 2.7 → 2.8
docs/01-game-design/COMBAT_RULES.md §5.3 added (canonical owner of the
                                   duration rule); version 1.4 → 1.5
docs/01-game-design/GAME_RULES.md  §17 step 19a scoping note added
docs/01-game-design/BOSS_RULES.md  §6.3.1 item 3 cross-reference added
tasks (this file)                  status, §11 resolution note, §12, evidence
```

### Documentation

`StatusEffects[]` is now contract-defined. The contract comprises:

```text
GAME_STATE.md §2.3.1   instance schema (7 members, presence rules)
GAME_STATE.md §2.3.2   JSON serialization and round-trip
GAME_STATE.md §2.3.3   explicit non-additions (no new type/wire/storage)
GAME_STATE.md §5.1.1   lifecycle: apply, refresh, consume, expire
COMBAT_RULES.md §5.3   the canonical gameplay rule (DR1–DR6)
```

`GAME_STATE.md` **still has no §2.5 heading** — the intentional §2.4 → §2.6 gap
was preserved, and the serialization contract was placed at §2.3.2 instead of
inventing the missing heading (see §12 note 1).

No `<StatusEffects[]>` implementation was added: the field is documented as
"contract-defined, not yet implemented", consistent with §0 item 4.

### Validation

Documentation consistency audit: **PASS**.

The audit confirmed:

- **DR1–DR6 represented consistently.** `COMBAT_RULES.md` §5.3 states the rule
  canonically; `GAME_STATE.md` §5.1.1 implements it without restating it;
  `GAME_RULES.md` §17 step 19a scopes it without adding a step. No document
  contradicts another.
- **No stale duration wording remains.** No surviving statement claims the
  consumption point is undetermined. The only "not yet implemented" references
  are the correct implementation-status ones (§2.3.1/§2.4.1 "contract-defined,
  not yet implemented"), plus the unrelated `EquippedRelics[]`/
  `EquippedCards[]` entries.
- **Burn is unchanged.** `BOSS_RULES.md` §6.3.1 item 1's tick schedule
  (tick #1 Turn N step 19a, tick #2 Turn N+1 step 19a, expiry before Turn N+2)
  is byte-identical and consistent with DR1–DR5. Burn was not extended to
  non-DoT effects and its magnitude was not touched.
- **Root is consistent.** Magnitude (`-30% Pet ATK`) and duration (2 Turns)
  unchanged; only a cross-reference to `COMBAT_RULES.md` §5.3 was added.
- **`SkillCooldown` is not generalized.** `BOSS_RULES.md` §6.3 and
  `GAME_STATE.md` §2.4.3 are unchanged; new text explicitly excludes them.
- **No `READY`/status-field regression.** Only TASK-093's own status changed
  (BLOCKED → DONE). TASK-094 remains READY and was not modified.
- **The §2.4 → §2.6 numbering gap is intact** — no §2.5 heading exists.
- **No source code changed**; no `StatusEffects[]` implementation exists.
- **No unrelated task changed** (TASK-079/091/092/094 untouched).
- Scope validated against `MVP_SCOPE.md` §1: Burn, Shield, and Buff/Debuff are
  IN scope; Stun is documented as existing-for-future-content with no MVP
  content applying it (`GAME_STATE.md` §2.4.5). No out-of-scope type was added.

### Source Code

No source code was modified.

### Runtime / Protocol / Persistence

No changes were made to:

- SignalR
- API contracts
- Redis
- PostgreSQL
- client runtime
- gameplay logic

`BattleState` serialization **code** was not modified. The serialization
*contract* for `StatusEffects[]` is documented in `GAME_STATE.md` §2.3.2, and
that section states explicitly that no Redis key, Redis-only field, or second
storage representation is introduced, and that no wire member is added.

### Scope Verification

Confirmed:

- TASK-094 was not modified (it remains READY; its DR1–DR6 were consumed as
  settled input).
- TASK-091 was not modified.
- TASK-092 was not modified.
- TASK-079 was not modified.
- No `StatusEffects[]` implementation was added.
- No client-authoritative gameplay logic was added.
- No gameplay behavior was implemented.
- No new task was created.

### Blocking Decision

**Resolved.** The blocking decision was delivered by TASK-094 as DR1–DR6:

> For a non-DoT Buff/Debuff with duration N Turns, at what exact point is one
> Turn of duration consumed relative to application and `GAME_RULES.md` §17
> step 19a?

Answer: one Turn of duration is consumed at `GAME_RULES.md` §17 step 19a, with
the application Turn counting, Apply and Refresh sharing one mechanism, and
expiry on reaching 0 at that step (`COMBAT_RULES.md` §5.3, DR1–DR6).
