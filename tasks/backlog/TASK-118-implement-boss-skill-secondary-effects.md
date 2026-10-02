# TASK-118 — Implement Boss Skill Secondary Effects (Flame Burst Burn, Drain Power, Root ATK Debuff)

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, schemas, or contracts.

  PROVENANCE: The GAME_RULES.md §17 step 18b completion task. TASK-022
  implemented Boss Response charging, Skill eligibility, Skill damage, the
  Basic Attack fallback, and the charge/cooldown reset — but deliberately
  applied no Skill *effect* other than damage. BOSS_RULES.md §6.3.1 fully
  specifies all three secondary effects, and every value, representation, and
  duration is already authoritative. This task lands exactly those three
  effects and nothing else.

  BOUNDARY: this task implements an already-documented contract. It decides NO
  gameplay. Every magnitude (Burn 50/tick × 2 Turns, −20 flat Power, −30% ATK
  × 2 Turns) is BOSS_RULES.md §6.3.1's, verbatim. Inventing a value,
  re-deriving a duration, adding a Status Effect type, or adding a Battle Event
  is the prohibited action of this task (AGENTS.md §7, §20).

  SCOPE FENCE: Boss Passive *effects* (step 18a — Hỏa Long's Rage, Thủy Ma's
  healing reduction, Mộc Yêu's regeneration) are NOT in this task. They are not
  fully determined by current documentation and require a separate decision
  task first (see "Dependencies" and "Stop Conditions"). This task is 18b only.
-->

---

## Metadata

```text
Task ID:           TASK-118
Type:              FEATURE (TASK_TYPES.md §2 — "Implement a documented
                   mechanic, capability, or system that already has a home in
                   docs/ but has not yet been built." All three effects have a
                   complete authoritative specification; none is built.)
Status:            COMPLETE — IN REVIEW. All three documented effects are
                   IMPLEMENTED and tested: Flame Burst's Burn (Scope item 1),
                   Drain Power (Scope item 2), Root's ATK debuff instance and
                   its ATK consumer (Scope items 3 and 4). Scope item 4 was
                   BLOCKED on TASK-119; TASK-119 recorded the contract in
                   COMBAT_RULES.md §5.4 (IN REVIEW — REVIEW COMPLETE, PASS)
                   and the consumer is now implemented against it. See
                   "Blocker" (resolved), "Completion Evidence".
Risk:              HIGH (TASK_TYPES.md §4 — FEATURE baseline MEDIUM, HIGH
                   because it touches the Combat Damage Pipeline, Boss State,
                   PetState combat stats, and the Status Effects collection.)
Priority:          HIGH (ROADMAP.md Phase 1 — "Boss Response (Passive →
                   Skill → Attack → Victory/Defeat)". Step 18b currently deals
                   damage only, so two of the three MVP Bosses' Skill identity
                   is absent from play: Thủy Ma and Mộc Yêu differ from a Basic
                   Attack only by damage magnitude.)
Primary Agent:     gameplay (the effects are Domain combat/status resolution;
                   BOSS_RULES.md §6.3.1 and COMBAT_RULES.md §5 own the rules)
Supporting Agents: backend (the step 18b resolution site in
                   BattleStateService.cs and its single write-back),
                   testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   gameplay/gameplay-behavior-derivation,
                   gameplay/authority-determinism-audit,
                   testing/test-scenario-generation,
                   quality/scope-validation
                   (6 skills — Complex budget, tasks/README.md §12)
Dependencies:      TASK-022 (DONE — implemented Boss Response: Passive
                     charge/trigger events, Skill eligibility, Skill damage,
                     Basic Attack fallback, charge/cooldown reset. It
                     deliberately applied no Skill effect beyond damage. The
                     boundary this task extends; immutable, read-only),
                   TASK-095 (DONE — StatusEffect domain state: `TurnBased`
                     and `TriggerBased` factories, the targetStat-iff-
                     BuffDebuff pairing, the §2.3.1 duration dichotomy),
                   TASK-096 (DONE — StatusEffects round-trip serialization),
                   TASK-094 (DONE — the step 19a duration-consumption rule this
                     task's two Turn-based effects rely on, COMBAT_RULES.md
                     §5.3 DR1–DR6),
                   TASK-105 (DONE — Shield refresh/depletion contract; the
                     Boss→Pet damage path this task attaches effects to),
                   TASK-112 (DONE — structured `EffectDefinition[]` content
                     encoding, the precedent this task does NOT reuse because
                     Boss effects are configuration, not Cards),
                   TASK-115 (DONE — server-authoritative PetSkillCast + Crit +
                     Burn. Its Burn step-19a tick is the shared consumer of the
                     Burn instance this task applies; this task adds no second
                     tick),
                   TASK-117 (DONE — ADR-017 and the `NextAttackCritModifiers[]`
                     state model. Read-only context; unaffected by this task)
                   TASK-119 (BACKLOG — the stop-driven contract task that
                     unblocks this task's Root effect; see "Blocker")
Blocks:            Nothing directly. It completes the last unimplemented half
                   of GAME_RULES.md §17 step 18b, which is a ROADMAP.md Phase 1
                   item. It does NOT unblock TASK-036, TASK-079, or TASK-099,
                   and it does not complete step 18a.
Estimate:          Complex (one Application resolution site plus its
                   post-resolution write-back, three effect applications
                   across two entities, one Debuff magnitude that must be read
                   by the Damage Pipeline, and the tests for each)
```

**This task implements an already-documented contract.**

It does not create or modify gameplay rules unless explicitly authorized
by an existing authoritative documentation task.

---

## Blocker (recorded while executing — Root only) — RESOLVED

> **RESOLVED.** TASK-119 (IN REVIEW — REVIEW COMPLETE, PASS) answered the
> decision set and authored the missing rule at its canonical owner,
> `COMBAT_RULES.md` §5.4 ("Stat Modifiers (`BuffDebuff` Consumption)"), with
> the state-mutation consequence recorded in `GAME_STATE.md` §2.3.1 items 2
> and 12. Checkpoint C now passes: a consumer exists, and Scope item 4 was
> implemented against §5.4 without changing §3.1's six-step order or §3.4's
> Boss-side `Step 4 = 1.0`. The original finding is retained below as evidence.

The three-point verification this task's §8 requires was performed against
the workspace. Checkpoints A and B pass; **checkpoint C fails**, and the
failure is a documented contract gap rather than an implementation detail.

```text
A  Damage boundary          PASS  PetState.ATK is consumed as Damage
                                  Pipeline step 1's `Attack` argument in the
                                  Player→Boss Calculate call
                                  (BattleStateService.cs, step 15 block).

B  Pipeline compatibility   PASS  A -30% factor at that point changes neither
                                  COMBAT_RULES.md §3.1's six-step order nor
                                  §3.4's pinned Boss-side step-4 value.

C  Duration ordering /
   consumer existence       FAIL  No rule states that a BuffDebuff's
                                  Magnitude modifies any stat, so there is no
                                  consumer to order against step 19a.
```

Evidence:

```text
1. The only readers of StatusEffect.Magnitude in src/backend are
   StatusEffectLifecycle.ShieldPool (guarded Type == Shield) and the step 19a
   DoT tick loops (guarded Type == DoT), plus the serializer's pass-through.
   No code path reads a BuffDebuff magnitude into a stat.

2. ADR-017 (Alternatives Considered → Option A) records verbatim:
   "§2.3.1's model has no attack-consumption path at all, so the
    representation would still be missing its whole lifecycle."

3. GAME_STATE.md §2.3.1 item 2 states Magnitude is "typed but NOT interpreted
   here" and hands the meaning to the effect's rule document — which never
   states one.

4. COMBAT_RULES.md §5.1 defines Buff/Debuff and authors no consumer.

5. No document in docs/ defines how an ATK reduction is applied or rounded.
   Open sub-questions: whether -30% applies to PetState.ATK alone or to
   step 1's sum (ATK + BaseDamagePool); the rounding; and how the instance's
   active/expired state is read for a Pet attack in a given Turn.
```

Resolving these requires choosing an application point and a rounding rule —
the exact speculation TASK-118 §9 and §21 prohibit. `COMBAT_RULES.md` §3
step 4 does name "Buffs/Debuffs", but §3.4 pins the Boss side to `1.0` and
TASK-118 §10 requires the **Pet's** step-1 value to be reduced, so the
mention does not resolve the tension by itself.

**Resolution path:** TASK-119 recorded the missing Root ATK modifier contract
(now `COMBAT_RULES.md` §5.4) and this task's Root scope item was implemented
against it — see "Completion Evidence → Scope item 4 — Root's ATK consumer
(IMPLEMENTED)". Its **Flame Burst Burn** and **Drain Power** effects were
fully determined and were never affected by this blocker.

The one sub-question the blocker raised that the resolution **closed** rather
than deferred: §5.4.1 item 2 fixes the base as `PetState.ATK` **alone**, so
the reduction applies to the ATK term and not to step 1's sum, and the ATK-Gem
pool is added afterwards (`ATK 100` + pool `40` at `−30%` → `70 + 40 = 110`,
never `98`).

---

## Objective

Apply the three documented Boss Skill secondary effects at `GAME_RULES.md` §17
step 18b — Flame Burst's Burn, Drain Power's instant Power reduction, and
Root's ATK debuff — so each of the three content-defined MVP Bosses' Skill is
behaviorally distinct from a Boss Basic Attack, using exactly the magnitudes,
representations, and durations `BOSS_RULES.md` §6.3.1 already fixes, without
adding a Battle Event, a Status Effect type, a wire member, or a second RNG
draw.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Bosses ("Element, Passive, Skill per
  Boss") and Combat ("Status Effects") are IN; §2 (OUT), §4 (unlisted = FUTURE)
- `docs/00-overview/ROADMAP.md` §1 Phase 1 — "Boss Response (Passive → Skill →
  Attack → Victory/Defeat)" is the milestone this step belongs to
- `docs/01-game-design/BOSS_RULES.md` **§6.3** (Skill timing and the
  `Secondary Effect & Magnitude` column for all three Skills), **§6.3.1**
  (**the canonical owner of all three effect magnitudes and duration
  semantics** — items 1, 2, 3), §6.1 (Boss base stats), §6.4 (BossId /
  PassiveId / SkillId identities), §4 (Boss Skill rules; item 4 "Boss Skills
  may apply non-damage effects"), §7 (events)
- `docs/01-game-design/COMBAT_RULES.md` **§3.4** (Boss Damage; the pipeline a
  Boss Skill traverses and the statement that non-damage effects are "applied
  outside the pipeline"), **§5.1** (MVP Status Effects: Burn, Shield,
  Buff/Debuff), **§5.2** (item 1 source/magnitude/duration; item 2 refresh
  default — "refresh duration, do not stack magnitude"), **§5.3** (DR1–DR6,
  the canonical duration-consumption timing both Turn-based effects use),
  §5.3.2 (scope — Turn-based Buff/Debuff), §3.3 (Crit, which a Burn tick
  participates in), §4 (Heal/Shield — not modified by this task), §1.1
  (`PetState` combat stats and the Power range 0–100)
- `docs/01-game-design/GAME_RULES.md` **§17 step 18b** (the resolution site),
  §17 step 18 (18b runs after 18a and before 18c), §17 step 19a (the DoT tick
  the Burn instance feeds), §16 (canonical event list — no Skill-effect event
  exists), §12 (Power range 0–100), §18 (server authority)
- `docs/02-technical/GAME_STATE.md` **§2.3.1** (the `StatusEffect` instance
  schema: `Id`/`Type`/`Source`/`Magnitude`/`TargetStat`/`RemainingTurns`/
  `ExpiryCondition`; item 3's exclusive duration dichotomy; item 6's
  one-instance-per-identity rule; item 7's targetStat-iff-BuffDebuff pairing;
  **item 9** — "Instant, non-duration effects create no instance. Drain Power
  is an immediate `PetState.Power` mutation, not a Status Effect"), §2.3.2
  (always-present collection), §2.3.4 (the separate `NextAttackCritModifiers[]`
  collection this task must not touch), §5.1.1 (step-19a lifecycle), §2.4.3
  (`BossState.SkillCharge` / `SkillCooldown`)
- `docs/02-technical/GAME_EVENTS.md` §2 (`BossSkillCast`, `DamageCalculated`,
  `DamageDealt`, `DamageTaken` — the only events step 18b emits), §3 (ordering)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.18 (`BossSkillCast` wire DTO),
  §3.2.13 (`otherModifiers` as the sole step-4 carrier), §4 (the payload
  member sets — no new member is added)
- `docs/02-technical/TDD.md` §6 (single server-seeded PRNG — the Burn tick's
  Crit draw is the existing stream's, not a new one)
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` (server
  authority), `ADR-009-deterministic-prng.md` (single PCG32 generator)
- `tasks/completed/TASK-022-implement-boss-response.md` — **IMMUTABLE;
  read-only.** Records the step 18b resolution this task extends and the
  explicit "effects not applied" boundary.
- `AGENTS.md` §4, §7, §9, §10, §11, §12, §13, §15, §16, §17, §18, §20, §21,
  §22; `.ai/workflow/development/feature.md`; `tasks/README.md` §9, §12

---

## Current State

`GAME_RULES.md` §17 step 18b resolves the Boss Skill fully except for its
secondary effect. The resolution site is implemented and correctly ordered;
the effect application is absent.

```text
BattleStateService.cs   step 18b: eligibility evaluated (SkillCharge >=
  ~1355–1457            ChargeRequirement AND SkillCooldown == 0)
                        → BossSkillCast emitted
                        → Skill damage through DamagePipeline.Calculate
                        → SkillCharge = 0, SkillCooldown = CooldownTurns
                        NO secondary effect is applied for any Boss.

BossDefinitions.cs       Each definition carries SkillId, BaseDamage,
  ~86–194                ChargeRequirement, CooldownTurns — configuration
                        only. Its own comment records the boundary:
                        "The Passive effects — Hỏa Long's Rage, Thủy Ma's
                        healing reduction, Mộc Yêu's regeneration — and the
                        Skill effects — Burn, Power drain, ATK reduction —
                        remain unimplemented and are owned by their own
                        stages."

StatusEffectType.cs      `DoT`, `BuffDebuff`, `Shield`, `State` all exist.
                         `BuffDebuff`'s doc comment already names Root as
                         "the documented MVP instance of this type."

StatusEffect.cs          `TurnBased(...)` enforces the targetStat-iff-
                         BuffDebuff pairing (§2.3.1 item 7), so a Root
                         instance cannot be constructed without its stat.

CardCastExecutor.cs      Burn application exists for a PLAYER-sourced Card
  ~175–194               (`StatusEffectSource.Player`, applied to BossState).
                         The Boss-sourced path this task needs does not exist.
```

`COMBAT_RULES.md` §5.1 defines Buff/Debuff as "temporary stat modification
(ATK/DEF/Crit/etc.), with duration measured in Turns unless stated otherwise" —
but this document records **no consumer** that reads a `BuffDebuff` magnitude
into a damage calculation. The `BuffDebuff` instance can be represented,
serialized, and duration-consumed today; nothing yet applies it to a stat.

---

## Scope

### In Scope

1. **Flame Burst — Burn application (Hỏa Long).** On step 18b Skill
   resolution, apply a Burn Status Effect instance to the active Pet per
   `BOSS_RULES.md` §6.3.1 item 1: `Id = "Burn"`, `Type = DoT`,
   `Source = Boss`, fixed `Magnitude = 50`, `RemainingTurns = 2`. Applied via
   the existing `StatusEffectLifecycle.Apply` refresh semantics
   (`COMBAT_RULES.md` §5.2 item 2, `GAME_STATE.md` §2.3.1 item 6) — a refresh
   replaces the existing instance's duration and magnitude, it does not append
   a second one. The existing step-19a DoT tick (TASK-115) consumes it; **no
   new tick path is added.**
2. **Drain Power — instant Power reduction (Thủy Ma).** On step 18b Skill
   resolution, reduce the active Pet's Power by 20, floored at 0:
   `PetState.Power = max(0, Power − 20)`, per `BOSS_RULES.md` §6.3.1 item 2.
   This creates **no Status Effect instance** (`GAME_STATE.md` §2.3.1 item 9)
   and has no duration.
3. **Root — ATK debuff application (Mộc Yêu).** On step 18b Skill resolution,
   apply a `BuffDebuff` Status Effect instance to the active Pet per
   `BOSS_RULES.md` §6.3.1 item 3: `Id = "Root"`, `Type = BuffDebuff`,
   `Source = Boss`, `TargetStat = "ATK"`, `Magnitude = 30`,
   `RemainingTurns = 2`, applied with the same refresh semantics as item 1.
   Its one-Turn-of-duration consumption is the existing step 19a pass
   (`COMBAT_RULES.md` §5.3 DR1–DR6, `GAME_STATE.md` §5.1.1) — **no new
   consumption path is added.**
4. **The ATK debuff must actually modify ATK where ATK is consumed.** Because
   `BOSS_RULES.md` §6.3.1 item 3 defines Root as a percentage reduction of the
   active Pet's ATK, and the active Pet's ATK reaches damage as the
   ATK-Gem-generated damage pool / attack value the Damage Pipeline's step 1
   consumes, the effective ATK used by the Pet's own damage must reflect the
   active `Root` instance's magnitude. This is the one non-local change in the
   task: it requires exactly one consumer at the existing Damage Pipeline
   entry, and it must not alter the pipeline's six steps or its ordering.
5. **Bind each effect to its own Boss.** Only the Skill that declares the
   effect applies it: Flame Burst → Burn, Drain Power → Power reduction,
   Root → ATK debuff. The binding is data-driven from the Boss's own
   definition, consistent with `BossDefinitions.cs`'s existing
   "configuration, not a registry" shape — **do not** dispatch on
   `SkillId` string comparison in the resolution body if the definition can
   carry the declaration instead.
6. Extend the existing step 18b resolution and its **single post-resolution
   write-back** (`GAME_STATE.md` §5.1) — no extra commit, no extra CAS
   attempt, no second write.

### Out of Scope

- **Boss Passive effects (step 18a)** — Hỏa Long's Rage (+20% ATK, 3 Turns),
  Thủy Ma's healing reduction (−50%, always active), and Mộc Yêu's
  regeneration (5% MaxHP). These are **not fully determined by current
  documentation**: no documented stat/representation is fixed for Rage, no
  heal-modifier application site exists in `COMBAT_RULES.md` §4, and the
  regeneration trigger has no documented application point or event
  (`BOSS_RULES.md` §6.2 states Thủy Ma emits no `PassiveCharged`/
  `PassiveTriggered`). They require a documentation/decision task first.
- **Relic trigger evaluation** (`GAME_RULES.md` §17 step 11). Absent, and
  `ROADMAP.md` Phase 2 places "All ~10 Relics + trigger/stacking system"
  there. Not this task.
- **The `RelicTriggered` Battle Event type and its wire DTO.**
- Any new Battle Event (e.g. `BurnApplied`, `DebuffApplied`, `PowerDrained`,
  `StatusTicked`). `GAME_RULES.md` §16's canonical list is closed and
  `BOSS_RULES.md` §7 enumerates step 18's events.
- Any new Status Effect `Type` (`StatusEffectType`'s four members are the
  complete documented vocabulary).
- Any new `TargetStat` value beyond `"ATK"`, and any second stat-modification
  mechanism.
- `COMBAT_RULES.md` §3.4's `Step 4 — Other Modifiers = 1.0 (MVP: no
  Relic/Passive/Buff modifiers on Boss side)`. This task modifies the
  **Pet's** step-1 attack, not the Boss's step 4.
- Stun application. `BOSS_RULES.md` §5 item 5: "no content-defined Boss
  applies Stun."
- `PetState.NextAttackCritModifiers[]` (`ADR-017`) — unaffected and untouched.
- Boss AI, Boss Phases, the 2 not-yet-content-defined Bosses
  (`BOSS_RULES.md` §6), Match-3, Cards, Passives for Pets, frontend work.
- Changing any magnitude, duration, or base stat. Every value is
  `BOSS_RULES.md` §6.1/§6.3.1's.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## State Ownership & Concurrency

```text
Authoritative owner      Battle server (BattleStateService step 18b).
                         AGENTS.md §10, GAME_RULES.md §18, ADR-001.
State contract owner     GAME_STATE.md §2.3.1 (`StatusEffect` schema and its
                         item 6/7/9 rules), §2.3 (PetState combat stats).
Gameplay rule owner      BOSS_RULES.md §6.3.1 (each effect's magnitude,
                         duration, and semantics) and COMBAT_RULES.md §5.1/
                         §5.2/§5.3 (Buff/Debuff definition, refresh default,
                         consumption timing).
Serialization owner      GAME_STATE.md §2.3.2 + BattleStateSerializer.cs —
                         UNCHANGED. Both new instances use the existing
                         schema and round-trip through the existing code; no
                         new member, no new JSON name.
Redis owner              REDIS_STATE.md §2/§4 — UNCHANGED. Both instances are
                         part of `BattleState` and serialize with it under the
                         existing single write-back and `Sequence` CAS.
Runtime mutation owner   BattleStateService step 18b, in the resolution's
                         single post-resolution write-back (GAME_STATE.md
                         §5.1). No mutation outside that commit.
SignalR projection       UNCHANGED. `StatusEffects[]` is not a wire member
                         (GAME_STATE.md §2.3.1's "Not a wire member";
                         SIGNALR_PROTOCOL.md §4). GAME_STATE.md §2.3.1 item 9
                         makes Drain Power equally invisible. The only wire
                         effects are the Skill's existing `BossSkillCast` and
                         damage events.
Client representation    None required. The client renders the resulting
                         events; it never computes, applies, or expires these
                         effects (AGENTS.md §10).
```

---

## Acceptance Criteria

- [ ] A Hỏa Long Skill resolution applies exactly one Burn instance to the
      active Pet with `Id = "Burn"`, `Type = DoT`, `Source = Boss`,
      `Magnitude = 50`, `RemainingTurns = 2` (`BOSS_RULES.md` §6.3.1 item 1).
- [ ] The applied Burn ticks at step 19a of the resolving Turn and the
      following Turn, then expires before the third — the TASK-115 tick path
      is reused and no second tick path exists.
- [ ] A Thủy Ma Skill resolution reduces the active Pet's Power by exactly 20
      and the result is floored at 0 (Power 10 → 0, not −10)
      (`BOSS_RULES.md` §6.3.1 item 2).
- [ ] Drain Power creates **no** Status Effect instance
      (`GAME_STATE.md` §2.3.1 item 9).
- [ ] A Mộc Yêu Skill resolution applies exactly one Root instance to the
      active Pet with `Id = "Root"`, `Type = BuffDebuff`, `Source = Boss`,
      `TargetStat = "ATK"`, `Magnitude = 30`, `RemainingTurns = 2`
      (`BOSS_RULES.md` §6.3.1 item 3).
- [x] While an active Root instance is present, the active Pet's effective ATK
      used by its own damage is reduced by 30%; when the instance expires at
      step 19a, the effective ATK returns to the unreduced value with no
      residual state. → `COMBAT_RULES.md` §5.4.1/§5.4.3/§5.4.4; consumer
      `StatusEffectLifecycle.EffectiveAttack`.
- [ ] Re-applying the same effect (Burn or Root) before it expires refreshes
      the existing instance — `RemainingTurns` is reset to 2 and no second
      element with the same `Id` appears (`COMBAT_RULES.md` §5.2 item 2,
      `GAME_STATE.md` §2.3.1 item 6).
- [ ] Each effect is applied only by the Skill that declares it: a Hỏa Long
      resolution applies no Root and no Power reduction, and so on for each
      Boss.
- [ ] No new Battle Event is emitted by any of the three effects. Step 18b's
      emitted event set is unchanged from TASK-022's
      (`BossSkillCast`, `DamageCalculated`, `DamageDealt`, `DamageTaken`).
- [ ] No new Status Effect `Type`, no new `TargetStat` value, and no new
      SignalR method, message, or payload member is introduced.
- [ ] No wire member is added: `SIGNALR_PROTOCOL.md` §4's payload member sets
      are byte-identical in meaning.
- [ ] No new Redis key and no second write: all three effects commit in the
      existing single post-resolution write-back under the unchanged
      `Sequence` compare-and-set (`REDIS_STATE.md` §4).
- [ ] A rejected or CAS-failed Swap applies none of the three effects and
      mutates nothing.
- [ ] The Burn tick's Crit evaluation consumes from the existing single
      `RngState`; no second draw, stream, or generator exists
      (`ADR-009`, `TDD.md` §6).
- [ ] `HitDamage`/status round-trip: a `BattleState` carrying the new Burn and
      Root instances serializes and deserializes losslessly with no new JSON
      member (`GAME_STATE.md` §2.3.2).
- [ ] Zero files under `docs/` modified; no new ADR required.
- [ ] All existing test suites pass; new Domain and Application tests cover
      every criterion above.
- [ ] Quality review checklist passes (`quality/review.md` §1).
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[ ] src/backend/GameServer.Domain/Combat/        (the Pet ATK consumer for Root)
[ ] src/backend/GameServer.Domain/Bosses/        (the per-Boss effect declaration)
[ ] src/backend/GameServer.Domain/Battle/        (StatusEffect factories if a
                                                  Boss-sourced helper is needed)
[ ] src/backend/GameServer.Application/Battle/BattleStateService.cs
                                                  (step 18b application)
[ ] tests/backend/ (Domain + Application tests, gameplay scenarios)
[ ] docs/ (NONE — authoritative docs already complete)
```

---

## Implementation Notes

- The step 18b site is `BattleStateService.cs`'s Boss Response resolution —
  the block that emits `BattleSkillCast`, calls `DamagePipeline.Calculate` for
  the Skill's damage, and then resets `SkillCharge`/`SkillCooldown`. The three
  effects attach there, **after** the Skill's damage instance and **before**
  the charge/cooldown reset, so the reset semantics TASK-022 recorded are
  untouched.
- `BOSS_RULES.md` §6.3.1 item 1 places Burn "during Turn N Boss Response (step
  18b)" and its first tick at "Turn N step 19a" — i.e. the **same** Turn. The
  step-19a pass already runs after step 18 in the same resolution, so no
  ordering change is needed; confirm the applied instance is visible to that
  pass.
- `StatusEffect.TurnBased(...)` enforces the `TargetStat`-iff-`BuffDebuff`
  pairing at construction, so the Root instance is well-formed by
  construction. Reuse `StatusEffectLifecycle.Apply` for both applications to
  inherit the refresh behavior rather than writing a second apply path.
- `COMBAT_RULES.md` §5.1's Buff/Debuff entry is the definition; there is no
  existing consumer that reads a `BuffDebuff` magnitude into a calculation.
  Adding that single consumer is the scope item 4 change. Keep it at the
  Damage Pipeline entry so the pipeline's six steps and their order
  (`COMBAT_RULES.md` §3.1) are unchanged, and do not make the *Boss's* step 4
  read it (`COMBAT_RULES.md` §3.4 pins the Boss side to `1.0`).
- `BossDefinitions.cs` is described by its own comment as "configuration, not a
  registry … no lookup, no indexing, and no discovery mechanism". Prefer
  carrying the effect declaration on the existing per-Boss definition type
  over string-dispatching on `SkillId` in the resolution body; do not introduce
  a registry, factory, or effect-resolution framework (`AGENTS.md` §9).
- Drain Power's floor at 0 is `BOSS_RULES.md` §6.3.1 item 2's own formula
  (`max(0, Power − 20)`); the Power range is `GAME_RULES.md` §12's 0–100.
- The same `PetState`-mutation and `StatusEffectLifecycle`-application
  patterns already exist on the Player→Boss and Boss→Pet paths in
  `BattleStateService.cs`; follow them rather than inventing a new style.

---

## Testing Requirements

### Required Verification

```text
[ ] Unit tests         — Burn application to the active Pet at Mag 50 /
                         2 Turns, Boss-sourced; refresh on re-application
                         resets duration and appends no second element.
[ ] Unit tests         — Power reduction: 30 → 10, 20 → 0, 10 → 0, 0 → 0;
                         and that no StatusEffect instance is created.
[ ] Unit tests         — Root application: BuffDebuff, TargetStat "ATK",
                         Mag 30, 2 Turns, Boss-sourced; refresh semantics.
[ ] Unit tests         — The Root ATK consumer: effective ATK reduced 30%
                         while active; exactly restored once the instance
                         expires; no effect when no Root is active.
[ ] Application tests  — Step 18b per-Boss dispatch: each Boss applies only
                         its own effect; Basic Attack fallback (18c) applies
                         none of them.
[ ] Application tests  — Single write-back / CAS: a rejected or CAS-failed
                         Swap applies no effect; the committed state carries
                         all effects from one resolution.
[ ] Application tests  — Event set unchanged: step 18b emits no event beyond
                         TASK-022's four.
[ ] Regression tests   — StatusEffects round-trip with the new instances;
                         full Domain + Application + Api suite passing.
```

### Key Edge Cases

- **Burn applied at Turn N, then a second Flame Burst at Turn N+1** — one Burn
  instance, duration refreshed to 2 (not 4 and not two instances).
- **Power at exactly the boundary** — 20 and 21 (→ 0 and → 1).
- **Root expiring at step 19a** — the recovery resolution is the one that
  reaches `RemainingTurns = 0`; the ATK reduction is gone from that point
  forward (`COMBAT_RULES.md` §5.3 DR5, `GAME_STATE.md` §2.3.1 item 8).
- **Burn tick Crit** — the tick participates in Effective Crit via the
  existing pipeline (`COMBAT_RULES.md` §3.3 item 4, §5.2 item 3) with a
  neutralized Combo; assert the existing RNG stream advanced by exactly the
  tick's one draw.
- **Skill fires while the Pet has an active Shield** — Shield absorbs the
  Skill damage per `COMBAT_RULES.md` §4; the secondary effect still applies
  (it is not damage).
- **Boss Skill that does not fire** — the 18c Basic Attack path applies no
  secondary effect for any Boss.
- **Non-`ATK` `TargetStat`** — a `BuffDebuff` naming any other stat must not
  silently modify ATK; the consumer reads `TargetStat` explicitly.

---

## Stop Conditions

<!--
  Universal stop conditions in AGENTS.md §20 and .ai/README.md §13 always apply.
-->

- **STOP** if any effect's magnitude, duration, or representation cannot be
  taken verbatim from `BOSS_RULES.md` §6.3.1 — a missing value is a
  documentation gap (`AGENTS.md` §7), not a value to choose.
- **STOP** if applying an effect appears to require a new Battle Event
  (`BOSS_RULES.md` §7, `GAME_RULES.md` §16), a new Status Effect `Type`, or a
  new `TargetStat`.
- **STOP** if applying an effect appears to require a second RNG draw, stream,
  or generator (`AGENTS.md` §11, `ADR-009`).
- **STOP** if the Root ATK reduction cannot be applied without changing
  `COMBAT_RULES.md` §3's six pipeline steps, their order (§3.1), or the Boss
  side's pinned step-4 value (§3.4).
- **STOP** if the change appears to require a new wire member, SignalR method,
  Redis key, or PostgreSQL column/schema.
- **STOP** if Burn's step-19a tick would need to be modified rather than
  reused (`COMBAT_RULES.md` §5.3 is the canonical owner of consumption
  timing).
- **STOP** if this task's scope drifts into **step 18a** (Boss Passive
  effects). Those are not fully determined by current documentation and
  require a decision task first — report it rather than deciding it here.
- **STOP** if this task's scope drifts into step 11 (Relic trigger evaluation)
  or the `RelicTriggered` event.
- **STOP** if an existing `Accepted` ADR would be contradicted
  (`.ai/workflow/architecture/architecture-change.md` §3).
- **STOP** if the work would exceed the 6 listed skills or cross an uncoupled
  architectural boundary — decompose instead (`tasks/README.md` §13).

---

## Completion Evidence

<!--
  COMPLETE: all four scope items are DONE and verified. Scope item 4 (Root's
  ATK consumer) was blocked on TASK-119; TASK-119 recorded the contract in
  COMBAT_RULES.md §5.4 (IN REVIEW — REVIEW COMPLETE, PASS) and the consumer is
  implemented against it. The blocker section above is retained as evidence and
  marked RESOLVED.
-->

### Changed Files

Source:
- `src/backend/GameServer.Domain/Bosses/BossDefinition.cs` — added
  `BossSkillDefinition.SecondaryEffect` (declared outside the persisted
  constructor, so `DATABASE.md` §1 note item 4's four-member
  `SkillDefinition` document is unchanged), plus the
  `BossSkillSecondaryEffect` record and `BossSkillSecondaryEffectKind` enum.
- `src/backend/GameServer.Domain/Bosses/BossDefinitions.cs` — declared each
  Skill's documented effect: Flame Burst → Burn (DoT, 50, 2 Turns), Drain
  Power → −20 flat Power, Root → ATK BuffDebuff (−30, 2 Turns).
- `src/backend/GameServer.Application/Battle/BattleStateService.cs` — step 18b
  now applies the fired Skill's declared secondary effect, after the Skill's
  damage instance and before the charge/cooldown reset, in the existing single
  write-back. Burn and Root go through `StatusEffectLifecycle.Apply` (refresh,
  not duplicate); Drain Power is an instant floored `PetState.Power` mutation
  creating no instance.
- `src/backend/GameServer.Infrastructure/Postgres/Configurations/BossDefinitionConfiguration.cs`
  — documentation only: records that `SecondaryEffect` is deliberately not
  written by the `SkillDefinition` converter. **No migration, no schema change.**

Root ATK consumer (Scope item 4) — added in the resumed session:
- `src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs` — added
  `EffectiveAttack(int, IReadOnlyList<StatusEffect>)`, the `COMBAT_RULES.md`
  §5.4.1 Step-1 `Attack` value, plus its `IsAtkBuffDebuff` selector and the
  `"ATK"` constant. Pure and non-destructive: it reads the stored stat and the
  active instances and writes neither.
- `src/backend/GameServer.Application/Battle/BattleStateService.cs` — the step-15
  Player → Boss call now passes
  `StatusEffectLifecycle.EffectiveAttack(resolved.PetState.ATK,
  resolved.PetState.ActiveStatusEffects)` as its `Attack` argument, with the
  ATK-Gem `BaseDamagePool` unchanged. The step-18b `AtkDebuff` comment was
  updated to name §5.4's consumer. **No pipeline change, no ordering change, no
  second write.**
- `src/backend/GameServer.Domain/Bosses/BossDefinitions.cs` and
  `src/backend/GameServer.Domain/Bosses/BossDefinition.cs` — comments only: the
  stale "Root's consumer is blocked on TASK-119" notes now record the resolved
  contract and the consumer. No value, magnitude, duration, or declaration
  changed.

Tests:
- `tests/backend/GameServer.Application.Tests/BossSkillSecondaryEffectTests.cs`
  — new; 25 tests covering Burn application/refresh/same-Turn tick/no event/RNG,
  Drain Power's 30→10, 21→1, 20→0, 10→0, 0→0 and no instance, Root
  application/refresh/consumption/no event, the per-Boss binding, the Basic
  Attack fallback, a rejected Swap, an exhausted compare-and-set (no partial
  commit, no write), a retried compare-and-set (committed exactly once), and
  the single write-back.
- `tests/backend/GameServer.Domain.Tests/BattleStateSerializationTests.cs` —
  added `RoundTrip_ShouldPreserveTheBossSkillSecondaryEffectInstances`.
- `tests/backend/GameServer.Application.Tests/BossResponseTests.cs` — two
  assertions scoped to the Skill's own damage instance (a Burn tick is
  Boss-sourced too, so "the last"/"exactly one" no longer identified it).
- `tests/backend/GameServer.Api.Tests/BossResponseWireTests.cs` — two
  assertions scoped to each direction's members rather than a fixed total.
- `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs` — the hand-rolled
  expectation now models the step 19a Burn tick AND resumes the RNG stream from
  the board resolution's output. **This repaired a pre-existing latent defect:**
  the expectation previously rolled Crit from a fresh stream, so the test was
  seed-dependent (~25% flaky) even before this task; it is now 20/20 stable.
- `tests/backend/GameServer.Infrastructure.Tests/BossDefinitionPostgresProvisioningTests.cs`
  — the persisted-document comparison is scoped to the four stored members.

Root ATK consumer (Scope item 4) — added in the resumed session:
- `tests/backend/GameServer.Domain.Tests/EffectiveAttackTests.cs` — **new**; 19
  tests for the consumer itself: the unreduced case; §5.4.1 item 3's `100 → 70`;
  §5.4.2's worked values (`50 → 35`, `51 → 35`, `99 → 69`, `100 → 70`,
  `101 → 70`); floating-point independence (`77 → 53`); §5.4.1's worked example
  with the pool (`70 + 40 = 110`, and the explicit **not `98`** guard); base
  preservation (the stat and the instance collection are both unmodified);
  non-`"ATK"` `TargetStat` ignored; `DoT`/`Shield`/`State` ignored; selection on
  `TargetStat` rather than `Id` (a differently-named `"ATK"` instance applies);
  the expired case asserted as the documented §5.3 DR5 removal (`1 → 0` →
  absent); §5.4.3's `2 → 1 → 0` timeline; refresh; and order-independence across
  two simultaneous instances.
- `tests/backend/GameServer.Application.Tests/RootAttackConsumptionTests.cs` —
  **new**; 8 end-to-end tests driving real Turns through `BattleStateService`:
  same-Turn non-retroactivity (§5.4.3 — the applying Turn's attack is unreduced
  at `50` while the instance *is* committed); the applied-2/decremented-to-1
  boundary; §5.4.3's full Turn N / N+1 / N+2 timeline (`50 → 35 → 50` with the
  instance absent from Turn N+1's committed state, since its last Turn is
  consumed in that same resolution); `PetState.ATK` unchanged before, during,
  and after (asserted `50` on every Turn including the reduced one); the pool not
  reduced (a `(ATK + pool) × 70%` negative guard, with the pooled-Turn coverage
  itself asserted so the guard cannot become vacuous); data-driven dispatch with
  a renamed `SkillId`; the Boss's own Step-1 base and `Step 4 = 1.0` unaffected;
  and a Boss-sourced Burn coexisting with Root contributing nothing to the ATK
  reduction (which would otherwise read `17`).

### Validation Results

```text
dotnet build GameServer.sln        — PASS (0 errors; warnings all pre-existing)

dotnet test  GameServer.sln        (final, after the Root ATK consumer)
  GameServer.Application.Tests     — PASS (429)
  GameServer.Domain.Tests          — PASS (1275)
  GameServer.Infrastructure.Tests  — PASS (327)
  GameServer.Api.Tests             — PASS (262)
  TOTAL                            — PASS (2293), 0 failed, 0 skipped

Before the Root ATK consumer (recorded earlier in this task):
  GameServer.Application.Tests     — PASS (421)
  GameServer.Domain.Tests          — PASS (1256)
  GameServer.Infrastructure.Tests  — PASS (327)
  GameServer.Api.Tests             — PASS (262)
  TOTAL                            — PASS (2266), 5/5 consecutive full-suite runs green
```

The suites grew by 27 tests (19 Domain + 8 Application), all of them this task's;
no existing test was modified, weakened, or deleted to accommodate the consumer.

The Api suite was additionally run 20× consecutively with 0 failures after the
RNG-origin fix recorded earlier, against ~25% flakiness before it.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed single RNG stream maintained with no secondary generator (the
      ATK consumer draws no RNG at all)
- [x] Confirmed no new Battle Event, Status Effect type, wire member, Redis
      key, or database column
- [x] Confirmed the effects commit in one write-back under the existing
      `Sequence` compare-and-set
- [x] Confirmed step 18a (Boss Passive effects) and step 11 (Relic triggers)
      remain unimplemented and were not entered
- [x] Confirmed no new gameplay decision was made: every value and rule the
      consumer implements is `COMBAT_RULES.md` §5.4's or `BOSS_RULES.md`
      §6.3.1's, and the one undefined case (multiple simultaneous `"ATK"`
      instances) was reported, not decided (§15)
- [x] Confirmed `PetState.ATK` is never overwritten, `EffectiveATK` is never
      persisted, and the ATK-Gem pool is never reduced (§5.4.1 item 2, §5.4.4)
- [x] Confirmed `COMBAT_RULES.md` §3.1's six-step order and §3.4's Boss-side
      `Step 4 = 1.0` are unchanged, and that `NextAttackCritModifiers[]`
      (ADR-017) is untouched
- [x] Confirmed zero files under `docs/` modified by this task

### Scope item 4 — Root's ATK consumer (IMPLEMENTED)

Implemented against TASK-119's resolved contract, whose canonical owner is
`COMBAT_RULES.md` **§5.4** ("Stat Modifiers (`BuffDebuff` Consumption)"). No
document was modified by this task to implement it.

**Implementation path**

```text
BossSkillDefinition.SecondaryEffect      (Domain configuration, per Boss —
                                          BOSS_RULES.md §6.3.1, transcribed)
        ↓  step 18b, when the Skill fires
Root StatusEffect instance               Id "Root", Type BuffDebuff,
                                          Source Boss, TargetStat "ATK",
                                          Magnitude 30, RemainingTurns 2
                                          applied via StatusEffectLifecycle.Apply
        ↓  committed StatusEffects[] at attack resolution
                                          COMBAT_RULES.md §5.4.3 activity
        ↓  step 15 of a later Turn
StatusEffectLifecycle.EffectiveAttack    truncate(ATK × (100 − |Magnitude|) / 100)
                                          §5.4.1 item 3, §5.4.2
        ↓  the value becomes the `Attack` argument only
Damage Pipeline Step 1 `Attack` input    §5.4.1 item 1
        ↓  `Base = Attack + BaseDamagePool`, unchanged §3 step 1
                                          the pool is NOT reduced (§5.4.1 item 2)
```

- **Where the consumer lives.**
  `src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs` —
  `EffectiveAttack(int attack, IReadOnlyList<StatusEffect> effects)`. It is the
  smallest abstraction that fits: a pure Domain function beside the collection's
  other operations, not a new system (`AGENTS.md` §9). No `UniversalStatSystem`,
  `BattleStatManager`, `BuffManager`, or `CombatModifierEngine` was created.
- **Where it is consumed.**
  `src/backend/GameServer.Application/Battle/BattleStateService.cs`, the step-15
  Player → Boss `DamagePipeline.Calculate` call — its `Attack` argument is now
  the helper's result instead of the raw `PetState.ATK`. **Only that argument's
  value changed**: no pipeline step was added, removed, or reordered
  (`COMBAT_RULES.md` §3.1), and the Boss side's `Step 4 = 1.0` (§3.4) is
  untouched.
- **Selection is by `Type` and `TargetStat`, never by `Id`** (§5.4.5). A `DoT`,
  a `Shield`, a `State`, and a `BuffDebuff` naming any other stat are all
  excluded, and a differently-named `BuffDebuff` naming `"ATK"` applies — so the
  behavior is the rule's, not Root's identity.
- **Data-driven.** The step 18b dispatch still reads
  `BossSkillDefinition.SecondaryEffect`; there is no `SkillId`/name/description
  comparison anywhere in the resolution body. A test drives the consumer with the
  `SkillId` renamed to a value matching nothing and asserts the reduction still
  happens.
- **Base preservation.** `PetState.ATK` is never written by this path, and
  `EffectiveATK` is never persisted — there is no `EffectiveATK` member on
  `PetState`, `BattleState`, the serializer, or any DTO.
- **Rounding is integer-only.** `effective * (100 - magnitude) / 100` on
  non-negative integers, so no floating-point representation can vary the result
  (§5.4.2). The magnitude is read as `(int)Math.Abs(...)` because
  `BOSS_RULES.md` §6.3.1 item 3 writes Root as `−30%` while the stored
  `Magnitude` is the positive `30`.

**Multiple simultaneous ATK modifiers — the §15/§8 stop condition.** Inspected
as required: `COMBAT_RULES.md` §5.4, §5.2, and §5.3 do **not** define how
several active `TargetStat = "ATK"` instances combine — §5.4 authors the
single-instance rule and no composition rule beyond it. Per TASK-118 §8 this
task therefore **invented no stacking semantics** (no additive, multiplicative,
or strongest-only choice) and built no aggregation abstraction. The helper folds
each active instance's own factor in `Id`-ordinal order — the order
`GAME_STATE.md` §5.1.1 item 6 already fixes for this collection — and that
ordering property is the only thing the test asserts. **No MVP content can
produce two simultaneous `TargetStat = "ATK"` instances**: `Root` is the only
documented producer (it is Mộc Yêu's Skill effect), and §2.3.1 item 6 bounds the
collection to one instance per `Id`. This is the §15 report:

```text
Root single-instance implementation is deterministic,
but multiple active ATK Buff/Debuff composition remains
undocumented.
```

No combination behavior is claimed, and none is reachable from MVP content.
