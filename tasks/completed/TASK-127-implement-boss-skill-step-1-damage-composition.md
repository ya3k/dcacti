# TASK-127 — Implement the Boss Skill Step-1 Damage Composition (EffectiveBossATK + Authored Skill Base Damage)

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, schemas, or contracts.

  IMPLEMENTATION TASK. NO DOCUMENTATION DECISION. NO GAMEPLAY REDESIGN.
  CONSUMES THE EXISTING COMBAT_RULES.md §3.4 CONTRACT.

  PROVENANCE: GAP-1 was surfaced by TASK-123's D-1a, recorded by TASK-124 as
  its one non-blocking STOP ("STOP CONDITION — GAP-1"), decided by the Product
  Owner in TASK-125 §6 (D-1 = Option B), and authored at its canonical owner by
  TASK-126 (DONE) at COMBAT_RULES.md §3.4 ("Boss Skill Step-1 composition").
  TASK-126 §10/§11 states explicitly that it must NOT create the implementation
  task and that the implementation task "is a later, separate step". This is
  that step. GAP-1 is CLOSED; this task does not reopen it.

  CONTRACT BEING CONSUMED (authoritative, already authored — not decided here):
    BossState.ATK
          ↓
    Boss ATK modifiers (e.g. Hỏa Long Rage +20%, BOSS_RULES.md §6.2)
          ↓
    EffectiveBossATK            derived at attack resolution — NOT stored
          ↓
    Boss Skill Step 1
          ↓
    + the Skill's authored Base Damage
          ↓
    Damage Pipeline Step 1
  Owner: COMBAT_RULES.md §3.4 ("Boss Skill Step-1 composition"); consumption
  rule: COMBAT_RULES.md §5.5.1–§5.5.4.

  BOUNDARY: this task implements an ALREADY-DOCUMENTED contract. It decides NO
  gameplay, authors NO value or formula, adds NO state field, NO Battle Event,
  NO SignalR member, NO Redis key, and NO PostgreSQL change. Inventing a
  composition, a rounding rule, a new modifier representation, or a second Boss
  ATK representation is the prohibited action of this task (AGENTS.md §7, §20).
-->

---

## Metadata

```text
Task ID:           TASK-127
Type:              FEATURE (TASK_TYPES.md §2 — "Implement a documented
                   mechanic, capability, or system that already has a home in
                   docs/ but has not yet been built." The composition is fully
                   specified by COMBAT_RULES.md §3.4 and §5.5; the
                   EffectiveBossATK Step-1 contribution is not built.)
Status:            DONE
Risk:              HIGH (TASK_TYPES.md §4 — FEATURE baseline MEDIUM; HIGH
                   because it touches the Combat Damage Pipeline's Step-1
                   input and the Boss Skill damage path in
                   BattleStateService step 18b.)
Priority:          HIGH (ROADMAP.md Phase 1 — "Boss Response (Passive → Skill →
                   Attack → Victory/Defeat)". TASK-118 landed the Skill's
                   secondary effects; this task lands the Skill's Step-1 damage
                   composition, which TASK-125/TASK-126 resolved and which the
                   Boss Skill damage path cannot be correct without.)
Primary Agent:     gameplay (the composition is a COMBAT_RULES.md §3/§5 combat
                   rule; the domain is Combat/Boss)
Supporting Agents: backend (the step 18b resolution site in
                   BattleStateService.cs and its single write-back),
                   testing,
                   review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   gameplay/gameplay-behavior-derivation,
                   gameplay/authority-determinism-audit,
                   testing/test-scenario-generation,
                   quality/scope-validation
                   (6 skills — Complex budget, tasks/README.md §12)
Dependencies:      TASK-126 (DONE — authored the consumed contract at
                     COMBAT_RULES.md §3.4 and resolved §5.5.2's stale GAP-1
                     wording. IMMUTABLE; READ-ONLY; must NOT be modified,
                     re-statused, moved, or rewritten),
                   TASK-125 (DECISION RECORDED — the Product-Owner Option B
                     decision this contract applies. IMMUTABLE; read-only),
                   TASK-118 (BACKLOG — implemented the Boss Skill SECONDARY
                     effects at step 18b (Burn, Drain Power, Root). This task
                     extends the SAME step 18b block's damage composition and
                     must not disturb its effects. Read-only),
                   TASK-022 (DONE — implemented Boss Response: Skill
                     eligibility, Skill damage, Basic Attack fallback,
                     charge/cooldown reset. IMMUTABLE; read-only),
                   TASK-021 (DONE — implemented the Damage Pipeline whose §3
                     step 1 sum this composition feeds. IMMUTABLE; read-only)
Blocks:            Nothing directly. It completes the Boss Skill damage half of
                   ROADMAP.md Phase 1's Boss Response sequence. It does NOT
                   unblock TASK-036, TASK-079, or TASK-099.
Estimate:          Complex (one Application resolution site, one derived-value
                   producer, the interaction with the Boss-side modifier
                   lifecycle, and the tests for each)
```

**This task implements an already-documented contract.**

It does not create or modify gameplay rules. Every value, factor, and
composition it implements is already authored and owned by
`COMBAT_RULES.md` §3.4 / §5.5 and `BOSS_RULES.md` §6.2 / §6.3.1.

**Not a decision task.** The composition is decided (TASK-125 §6, Option B) and
authored (TASK-126). This task must NOT reopen it, re-derive it, choose between
Option A and Option B, or create another decision task. If the authored contract
is found missing or internally inconsistent, that is a Stop Condition (§8), not
license to decide.

**Not a documentation task.** `docs/` is **READ-ONLY** for this task
(`COMBAT_RULES.md` §3.4 and §5.5 are byte-identical after it).

---

## Objective

Implement the authoritative Boss Skill Step-1 Base Damage composition at the
existing `GAME_RULES.md` §17 step 18b resolution site, so that a fired Boss
Skill's Step-1 Base Damage is the sum of its two documented Step-1 contributions
— `EffectiveBossATK` and the Skill's authored Base Damage — with
`EffectiveBossATK` derived at attack resolution from `BossState.ATK` and any
applicable Boss ATK modifier, and with `BossState.ATK` never overwritten and the
authored Skill Base Damage never modified.

The task is complete when:

```text
BossState.ATK
      ↓
Boss ATK modifiers (Hỏa Long Rage, BOSS_RULES.md §6.2)
      ↓
EffectiveBossATK                derived — NOT stored
      ↓
Boss Skill Step 1
      ↓
+ the Skill's authored Base Damage
      ↓
Damage Pipeline Step 1 (the existing §3 step 1 sum)
```

is what the server computes at step 18b, with no other pipeline step, no other
event, and no other state affected.

---

## Authoritative References

### The contract being implemented (READ ONLY — this task consumes it, it does not author it)

- `docs/01-game-design/COMBAT_RULES.md` **§3.4** — Boss Damage, and the
  **canonical owner** of the composition. Its **"Boss Skill Step-1
  composition"** subsection states that a Boss Skill's Step 1 Base Damage is
  the sum of its applicable Step-1 contributions — `EffectiveBossATK` **and**
  the Skill's authored Base Damage — with the flow diagram, the consequences
  (a Boss ATK modifier reaches the Skill's Step-1 damage **through
  `EffectiveBossATK`**; the authored value is unchanged), the statement that
  this is the Boss-side counterpart of §5.4.1 item 2, the statement that a Boss
  Skill contributes **no ATK-Gem-generated damage pool**, and the worked
  example (`120 + 150 = 270`; `100 + 150 = 250` with Rage inactive).
  **This is the specification this task implements. Do not amend it.**
- `docs/01-game-design/COMBAT_RULES.md` **§3 step 1** — the Damage Pipeline's
  Step-1 definition: Base Damage is the **sum of applicable contributions**.
  The composition feeds this existing sum; this task changes no step.
- `docs/01-game-design/COMBAT_RULES.md` **§3.4 Boss Basic Attack clause** —
  `Step 1 — Base Damage = Boss.ATK`, and the retained Boss-side
  `Step 4 = 1.0`. **Unchanged by this task**; the Basic Attack path must keep
  its documented behavior.
- `docs/01-game-design/COMBAT_RULES.md` **§5.5 / §5.5.1 / §5.5.2 / §5.5.3 /
  §5.5.4 / §5.5.5** — the **Boss-side** ATK modifier rule and the consumption
  rule this composition depends on. §5.5.1 fixes the consumption point as the
  Boss Damage Pipeline's **Step 1 `Attack` input**, producing a derived,
  non-stored `EffectiveBossATK` (integer, truncated toward zero). §5.5.2 fixes
  the damage scope: the modifier reaches the Basic Attack **and** a Boss Skill
  (through the Skill's `EffectiveBossATK` contribution only), and **never** a
  Skill's authored Base Damage value. §5.5.3 fixes the boundaries (no Step-4
  factor; no non-`"ATK"` `TargetStat`). §5.5.4 requires `BossState.ATK` to be
  **never overwritten** with no "restore" step and `EffectiveBossATK` never
  persisted. §5.5.5 defers duration/reapplication to §5.3/§5.2 item 2.
- `docs/01-game-design/COMBAT_RULES.md` **§5.4 / §5.4.1 / §5.4.4 / §5.4.5** —
  the **Pet-side** counterpart, for the mirrored shape and for the
  non-destructive/derived-value discipline this task must match on the Boss
  side. **Referenced, not restated, and not modified.**
- `docs/01-game-design/BOSS_RULES.md` **§6.2 / §6.2.1** — Hỏa Long's Rage
  (`+20% ATK`, 3 turns), its representation (a Turn-based `BuffDebuff`
  StatusEffect in `BossState.StatusEffects[]` with `TargetStat = "ATK"`), and
  its **Damage scope** bullet, which states the modifier reaches Boss damage
  through the Step-1 `EffectiveBossATK` contribution (basic attack and Boss
  Skill alike) while never reaching a Skill's authored Base Damage value.
- `docs/01-game-design/BOSS_RULES.md` **§6.3 / §6.3.1** — the per-Skill
  authored Base Damage values (`150` / `120` / `100`) and the statement that
  they are MVP base configuration. **These values are authored and must not be
  changed.** `§6.3.1 item 1`'s `150` remains the Skill's **authored** Base
  Damage; it never becomes `270`.

### Resolution order, state, and events

- `docs/01-game-design/GAME_RULES.md` **§17 step 18b** (the resolution site),
  §17 step 18a (the Boss Passive, whose Rage instance this task's derivation
  consumes), §17 step 18c (the Basic Attack fallback), §17 step 19a (the
  duration-consumption pass), §16 (the canonical event list — no event is added
  by this task), §18 (server authority).
- `docs/02-technical/GAME_STATE.md` **§2.4 / §2.4.1** (`BossState`, and
  `StatusEffects[]` as the collection Rage is held in — **no new member is
  added**; `EffectiveBossATK` is **not** a member), **§2.3.1** (the
  `StatusEffect` instance schema and the one-instance-per-identity rule),
  **§5.1.1** (the lifecycle pass order), §0 item 5 (no second representation of
  a stat).
- `docs/02-technical/GAME_EVENTS.md` **§2** — the existing event vocabulary.
  The composed Step-1 value reaches the client through the existing
  `DamageCalculated.base`; **no event is added**.
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3.2.13** (`DamageCalculated`) and
  **§4** — the payload member sets. **No member is added**; the composition is
  a value change, not a member-set change.
- `docs/02-technical/REDIS_STATE.md` §2/§4 and `docs/02-technical/DATABASE.md` —
  **no change**: the composition commits in the existing single write-back and
  is not persisted as a new field.
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` (server
  authority), `ADR-009-deterministic-prng.md` (single generator — this task
  draws no RNG).

### Governance and process

- `tasks/completed/TASK-126-apply-boss-skill-step-1-damage-composition-contract.md`
  — the contract-resolution task whose §10/§11 explicitly defer this
  implementation task to a later step. **IMMUTABLE; read-only.**
- `tasks/backlog/TASK-125-resolve-boss-skill-step-1-damage-composition.md` §6 —
  the verbatim Product-Owner decision (Option B). **IMMUTABLE; read-only.**
- `AGENTS.md` §2 (code is last in the precedence chain), §7, §9
  (anti-overengineering), §10 (server authority), §12/§13 (domain and technical
  boundaries), §14, §15, §16, §17, §18, §20, §22.
- `.ai/workflow/development/feature.md`, `.ai/workflow/quality/testing.md`,
  `.ai/workflow/core/validation.md` §2 (HIGH depth), `.ai/README.md` §13.
- `tasks/README.md` §6, §9, §12; `tasks/TASK_TYPES.md` §2/§3/§4;
  `tasks/TASK_LIFECYCLE.md` §3.
- `docs/00-overview/MVP_SCOPE.md` §1 (Bosses and Combat/Damage are IN), §2.

### Explicitly NOT modified (verify, do not edit)

- `docs/**` — **READ-ONLY. Zero edits.** In particular `COMBAT_RULES.md`
  (including §3.4 and §5.5) and `BOSS_RULES.md` are **byte-identical** after
  this task.
- `docs/03-decisions/ADR/**` and `docs/03-decisions/README.md` — **no ADR.**
- `tasks/completed/TASK-126-*`, `tasks/backlog/TASK-125-*`, and every other
  existing task file — **IMMUTABLE.**

---

## Current State

The contract is **authored in `docs/` but not implemented**. Verified in the
working tree:

```text
COMBAT_RULES.md §3.4 "Boss Skill Step-1 composition"
    Authored by TASK-126: Step 1 = EffectiveBossATK + authored Skill Base
    Damage, with the worked example (120 + 150 = 270).
    → The authoritative contract this task implements. No source file
      implements EffectiveBossATK.

Combat/DamagePipeline.cs (Calculate)
    §3 step 1 computes `inputs.Attack + inputs.BaseDamagePool`. The caller
    supplies `Attack`, so the pipeline itself needs NO change — only the
    caller's Step-1 `Attack` argument for a Boss Skill changes. The file's own
    §3-step-1 comment currently says "for a Boss Skill it is Boss.ATK +
    SkillBaseDamage".

BattleStateService.cs step 18b (~L1390-1392)
    var bossAttack = skillFires
        ? bossState.ATK + bossDefinition.SkillBaseDamage
        : bossState.ATK;
    → Uses the RAW `bossState.ATK`. No Boss ATK modifier is applied, so a
      fired Boss Skill's Step-1 damage is 250 under the documented example
      instead of the contract's 270.

BattleStateService.cs step 18a (Boss Passive, ~L1305-1347)
    Charges and emits PassiveCharged/PassiveTriggered for Hỏa Long and Mộc
    Yêu, and skips the charge for Thủy Ma. It applies NO Boss Passive effect
    → Rage is never applied to `BossState.StatusEffects[]` by any code path.
      Nothing in src/ reads a Boss-side `TargetStat = "ATK"` instance.

StatusEffectLifecycle.cs
    `EffectiveAttack(int, IReadOnlyList<StatusEffect>)` exists and is the
    documented PET-side §5.4 consumer. It is currently called only at step 15
    (Player → Boss). There is NO Boss-side counterpart, and no
    `EffectiveBossATK` identifier exists anywhere in src/ or tests/.

BossDefinition.cs / BossDefinitions.cs
    `SkillBaseDamage` is the authored per-Skill value (150/120/100, §6.3).
    `BossSkillDefinition.SecondaryEffect` carries the §6.3.1 effect
    declaration (TASK-118). `ATK` is Domain configuration (MVP default 100,
    §6.1). None of these is changed by this task.

Existing tests
    BossResponseTests.cs `BossSkill_ShouldUseBossAttackPlusSkillBaseDamage`
    asserts `BOSS.ATK + SkillBaseDamage` (250) for a fired Skill.
    DamagePipelineTests.cs `BossSkill_BaseDamage_ShouldBeBossAtkPlusSkillBaseDamage`
    asserts the same at the pipeline boundary.
    → These assert the RAW-ATK reading. Under the resolved contract they are
      correct only in the Rage-INACTIVE case; they must be reconciled per
      §4's Rage-coupled expectation, not deleted.
```

**What this task does NOT change.** `§3.1`'s six-step order, `§3.4`'s Boss
Basic Attack clause and its Boss-side `Step 4 = 1.0`, `§3 step 1`'s sum rule,
`§5.4`'s Pet-side rule, `§5.5`'s modifier consumption point, `GAME_RULES.md`
§16's closed event list and §17's order, the `BOSS_RULES.md` §6.1/§6.2/§6.3/
§6.3.1 balance values, `NextAttackCritModifiers[]` (ADR-017), and TASK-118's
three Skill secondary effects.

---

## Scope

### In Scope

1. **Derive `EffectiveBossATK` at Boss attack resolution.** When a Boss attack's
   Step-1 input is derived from `BossState.ATK`, the value passed to the Damage
   Pipeline is the Boss's ATK after its applicable Boss ATK modifier — the
   `COMBAT_RULES.md` §5.5.1 consumption point and integer convention
   (truncated toward zero). It is a **derived** value used within one pipeline
   execution: never stored, never written back to `BossState.ATK`, and never a
   new `BossState`/`BattleState` member.
2. **Compose the Boss Skill's Step-1 Base Damage.** For a fired Boss Skill
   (step 18b), the Step-1 `Attack` argument becomes the sum of the two
   documented Step-1 contributions — `EffectiveBossATK` **and** the Skill's
   authored Base Damage — per `COMBAT_RULES.md` §3.4's composition and §3
   step 1's existing sum rule. The authored value is passed through
   **unchanged**.
3. **Keep the Boss Basic Attack's Step-1 input correct.** The step 18c Basic
   Attack continues to derive its Step-1 input from `BossState.ATK` alone
   (`§3.4`'s `Step 1 — Base Damage = Boss.ATK`), now through the same derived
   `EffectiveBossATK`, and contributes **no** authored Skill Base Damage.
4. **Consume the existing Boss ATK modifier representation.** The modifier is
   read from the Boss's own `BossState.StatusEffects[]` — a Turn-based
   `BuffDebuff` whose `TargetStat` is `"ATK"` (`BOSS_RULES.md` §6.2.1,
   `GAME_STATE.md` §2.4.1). Selection is by `Type` and `TargetStat`, **never by
   `Id`** (`§5.4.5`'s discipline applied to the Boss side per `§5.5.3`).
5. **Preserve non-destructiveness.** `BossState.ATK` is read and never
   written; there is no "×1.2 then restore" step and no reset to a configured
   default (`§5.5.4`). `EffectiveBossATK` is never persisted.
6. **Preserve the single write-back.** The composed value enters the existing
   `DamagePipeline.Calculate` call in the existing step 18b block, committed by
   the same single post-resolution write-back (`GAME_STATE.md` §5.1). No extra
   commit, no second CAS attempt, no extra pipeline call.
7. **Reconcile the two existing tests** that assert the raw-ATK reading, so
   they assert the contract's Rage-inactive case explicitly rather than
   incidentally (§4).
8. **Implement the smallest thing that makes the above executable**, reusing
   the existing Domain helper shape beside `StatusEffectLifecycle.EffectiveAttack`
   (`AGENTS.md` §9: no `UniversalBossEngine`, `BossDamageManager`, or
   `GameCombatManager`; no registry, factory, or effect framework).

### Out of Scope

- **Re-deciding or re-authoring the composition.** It is decided (TASK-125) and
  authored (TASK-126). Zero `docs/` edits.
- **Implementing the Boss Passive effects (step 18a).** Hỏa Long's Rage
  *application*, Thủy Ma's healing reduction, and Mộc Yêu's regeneration are
  the separate ROADMAP.md Phase 1 Passive half and remain out of scope. This
  task **consumes** a Rage instance if one is present in
  `BossState.StatusEffects[]`; it does not create one. See §8's stop condition
  on this boundary.
- **Applying the Boss-side modifier to any damage other than the Boss's own
  attack's Step-1 input** — in particular not to Step 4 (`§5.5.3`) and not to a
  Skill's authored Base Damage (`§5.5.2`).
- **Changing any value**: the `150` / `120` / `100` authored Base Damages, Hỏa
  Long's `+20%` / `3 turns`, `BossState.ATK`, or any `BOSS_RULES.md` §6.1/§6.3
  magnitude.
- **Adding a new `BossState`/`BattleState` member**, especially any persisted
  `EffectiveBossATK` / `BossEffectiveATK` / `EffectiveBossSkillATK` /
  `SkillAttackStat` / `BossSkillATK` equivalent (zero occurrences).
- **Adding a new Battle Event, SignalR member, API endpoint, Redis key, or
  PostgreSQL column/table/migration** — including adding `bossState` to the
  wire projection.
- **New Boss Skills, new Boss mechanics, Stun application, Boss AI, Boss
  Phases, or the 2 not-yet-content-defined Bosses.**
- **Burn / Drain Power / Root behavior changes** (TASK-118) — the step 18b
  secondary-effect block must be undisturbed.
- Match-3; board; gems; swap; cascade; Combo; Crit; Relic triggers (step 11);
  Cards; Pets; passives for Pets; frontend/Phaser; rewards; progression.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Contract Being Implemented (already decided and authored — not open for reconsideration)

Every item below is `COMBAT_RULES.md` §3.4 / §5.5's authored text. This task
writes it into code. Nothing here is open for reconsideration.

```text
Boss Skill Step 1 Base Damage = EffectiveBossATK + authored Skill Base Damage
```

```text
BossState.ATK
      ↓
Boss ATK modifiers (e.g. Hỏa Long Rage +20%)
      ↓
EffectiveBossATK            derived at attack resolution — NOT stored
      ↓
Boss Skill Step 1
      ↓
+ the Skill's authored Base Damage
      ↓
Damage Pipeline Step 1 (the existing sum of applicable contributions)
```

```text
Worked example (COMBAT_RULES.md §3.4):
  BossState.ATK                    = 100
  Hỏa Long Rage                    = +20%
  EffectiveBossATK                 = truncate(100 × (100 + 20) / 100) = 120
  Flame Burst authored Base Damage = 150        (unchanged by the modifier)
  Step 1 Base Damage               = 120 + 150 = 270

  With Rage inactive: EffectiveBossATK = 100 → Step 1 = 100 + 150 = 250
```

```text
Basic Attack (COMBAT_RULES.md §3.4, unchanged):
  Step 1 Base Damage = EffectiveBossATK          (no authored Skill term)
```

**Not reopened by this task (unchanged; recorded so they are not read as in
question).** All four were fixed by TASK-123 / TASK-124 / TASK-125 and stand:

```text
BossState.ATK remains immutable/base.
EffectiveBossATK remains derived, non-stored, and NOT a new state field.
Hỏa Long Rage remains a modifier to EffectiveBossATK.
Boss-side Step 4 remains 1.0.
```

**The one boundary this task must report rather than cross.** The contract's
*documented* worked example is the Rage-active case, and Rage's **application**
(step 18a) is not implemented. This task therefore implements the composition
deterministically against whatever committed `BossState.StatusEffects[]` state
the resolution receives, and must make the Rage-active case testable **without
inventing a step-18a Rage application** — see §4. If the composition cannot be
made deterministic without first implementing step 18a, that is §8's stop
condition, not a license to implement the Passive half here.

---

## Acceptance Criteria

- [x] A fired Boss Skill's Step-1 Base Damage equals
      `EffectiveBossATK + the Skill's authored Base Damage` at the existing
      step 18b `DamagePipeline.Calculate` call (`COMBAT_RULES.md` §3.4).
- [x] `EffectiveBossATK` is derived from `BossState.ATK` and the Boss's active
      `TargetStat = "ATK"` modifier instances, using `COMBAT_RULES.md` §5.5.1's
      integer convention (truncated toward zero). For `ATK 100` and `+20%` it
      is exactly `120`.
- [x] With no active Boss ATK modifier, `EffectiveBossATK` equals
      `BossState.ATK` and a fired Flame Burst's Step-1 Base Damage is exactly
      `100 + 150 = 250`.
- [x] With an active `+20%` modifier, the same Skill's Step-1 Base Damage is
      exactly `120 + 150 = 270`.
- [x] The Skill's authored Base Damage value is **unchanged** by the modifier:
      with Rage active the Skill's authored Base Damage read from the Boss
      definition is still `150`, and the composed Step-1 increase comes from the
      `EffectiveBossATK` contribution alone.
- [x] `BossState.ATK` is **unchanged** after deriving `EffectiveBossATK` — it
      still reads `100` on the committed state before and after the resolution,
      and no "restore" step exists (`§5.5.4`).
- [x] The composed Step-1 Base Damage enters the existing Damage Pipeline
      **exactly once** — exactly one `DamageCalculated` event for the Skill's
      damage instance, whose `base` is the composed value, with no second
      pipeline invocation and no double-applied modifier.
- [x] `EffectiveBossATK` is **derived and non-stored**: it appears as no
      member of `BossState`, `BattleState`, the serializer, any DTO, or any wire
      payload; the strings `EffectiveBossATK`, `BossEffectiveATK`,
      `EffectiveBossSkillATK`, `SkillAttackStat`, and `BossSkillATK` occur
      nowhere under `src/` or `tests/` as identifiers, and no such member is
      added.
- [x] A Boss **Basic Attack** (step 18c) still derives its Step-1 input from
      `BossState.ATK` alone — it receives no authored Skill Base Damage and its
      Step-1 Base Damage equals `EffectiveBossATK`.
- [x] Boss-side **Step 4 `OtherModifiers` remains `1.0`** for both the Skill and
      the Basic Attack; the modifier is a Step-1 input, never a Step-4 factor
      (`§3.4`, `§5.5.3`).
- [x] A Boss Skill contributes **no ATK-Gem-generated damage pool** — its
      pipeline `BaseDamagePool` remains `0` (`§3.4`).
- [x] Modifier selection is by `Type` and `TargetStat`, **never by `Id`**: a
      differently-named Turn-based `BuffDebuff` naming `"ATK"` applies, while a
      `DoT`, a `Shield`, a `State`, and a `BuffDebuff` naming any other stat do
      not (`§5.5.3`, `§5.4.5`).
- [x] `COMBAT_RULES.md` §3.1's six-step order is unchanged; no pipeline step is
      added, removed, or reordered.
- [x] `COMBAT_RULES.md` §5.4's **Pet-side** rule is unaffected: the step 15
      Player → Boss `Attack` argument still comes from
      `StatusEffectLifecycle.EffectiveAttack`, and the Boss-side derivation
      introduces no second ATK-consumption path for the Pet.
- [x] TASK-118's three Boss Skill secondary effects are undisturbed: a fired
      Flame Burst still applies Burn (50 / 2 Turns), Drain Power still reduces
      Power by 20 floored at 0, and Root still applies the `"ATK"` `BuffDebuff`.
- [x] No new Battle Event is emitted: step 18b's emitted set is unchanged
      (`BossSkillCast`, `DamageCalculated`, `DamageDealt`, `DamageTaken`)
      (`GAME_RULES.md` §16, `GAME_EVENTS.md` §2).
- [x] No new SignalR method, event, or payload member is introduced
      (`SIGNALR_PROTOCOL.md` §4's member sets are unchanged; `bossState` is not
      added); the composed value reaches the client only through the existing
      `DamageCalculated.base`.
- [x] No new Redis key and no second write: the composition commits in the
      existing single post-resolution write-back under the unchanged `Sequence`
      compare-and-set (`REDIS_STATE.md` §4).
- [x] No PostgreSQL table, column, or schema change (`DATABASE.md` unchanged).
- [x] No new REST endpoint or API contract (`API_CONTRACTS.md` unchanged).
- [x] The two existing tests that assert the raw-ATK reading are reconciled to
      assert the contract's Rage-inactive case (`100 + 150 = 250`) explicitly;
      no test is deleted, weakened, or made vacuous to accommodate the change.
- [x] Zero files under `docs/` are modified; `COMBAT_RULES.md` (including §3.4
      and §5.5) and `BOSS_RULES.md` are byte-identical before and after.
- [x] No ADR is created or modified.
- [x] `TASK-125`, `TASK-126`, and every other existing task file are
      **byte-identical** (unmodified, unmoved, not re-statused).
- [x] All existing test suites pass at the depth `.ai/workflow/core/validation.md`
      §2 requires for HIGH risk (build + unit + integration + gameplay
      scenarios + architecture validation).
- [x] Quality review checklist passes (`.ai/workflow/quality/review.md` §1).
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[x] src/backend/GameServer.Domain/Battle/    (the Boss-side derived-ATK helper,
                                              beside StatusEffectLifecycle)
[x] src/backend/GameServer.Application/Battle/BattleStateService.cs
                                              (step 18b's Step-1 composition;
                                               the value passed to the
                                               existing DamagePipeline call)
[x] tests/backend/ (Domain unit tests + Application gameplay scenarios)
[ ] src/frontend/client/  — NONE. No client change; the composition is a value
                            change visible only through the existing
                            DamageCalculated event (AGENTS.md §10).
[ ] docs/                 — NONE. READ-ONLY.
[ ] docs/03-decisions/ADR/ — NONE. No ADR.
[ ] src/backend/GameServer.Domain/Combat/DamagePipeline.cs
                          — VERIFY ONLY. §3 step 1 sums what the caller
                            supplies, so no pipeline change is expected. If a
                            pipeline change appears necessary, that is a Stop
                            Condition (§8).
[ ] src/backend/GameServer.Domain/Bosses/ — VERIFY ONLY. `SkillBaseDamage`
                            remains the authored value read unchanged; no
                            declaration, value, or effect binding changes.
[ ] tasks/                — THIS NEW TASK FILE ONLY
                            (tasks/backlog/TASK-127-implement-boss-skill-step-1-
                            damage-composition.md). No existing task file
                            modified.
```

---

## Implementation Notes

Task-specific pointers only. Every rule and value below belongs to the
referenced document, not to this task.

- **The resolution site.** `BattleStateService.cs`'s step 18b block — the block
  that evaluates `skillFires`, emits `BossSkillCast`, computes `bossAttack`, and
  calls `DamagePipeline.Calculate`. The composition changes **only** the
  `Attack:` argument of that one existing call. Nothing else in the block moves:
  the damage instance, the shield handling, the secondary-effect block, and the
  charge/cooldown reset keep their current order (TASK-118 §6's ordering
  requirement).
- **Reuse the existing helper shape, not a new subsystem.** The Pet-side
  consumer is `StatusEffectLifecycle.EffectiveAttack(int, IReadOnlyList<StatusEffect>)`
  in `GameServer.Domain/Battle/StatusEffectLifecycle.cs`. The Boss-side
  derivation belongs beside it, as the same kind of pure Domain function
  (`AGENTS.md` §9, `ARCHITECTURE.md` §5). Do **not** create a `UniversalBossEngine`,
  `BossDamageManager`, `GameCombatManager`, manager, registry, or factory.
- **Which collection to read.** The Boss's own
  `BossState.StatusEffects` (`GAME_STATE.md` §2.4.1) — **not** the Pet's. The
  step 18b block already holds the local `bossState` at the point the attack is
  composed, so the committed instances are in hand; no extra read or write is
  needed.
- **Selection.** Follow `§5.4.5`'s "read `TargetStat` explicitly" discipline:
  select on `Type == BuffDebuff` **and** `TargetStat == "ATK"`, and never on
  `Id`. A `DoT` (the Boss's own Burn tick), a `Shield`, a `State` (Stun), and a
  non-`"ATK"` `BuffDebuff` must all be excluded.
- **Integer-only arithmetic.** `§5.5.1` uses the same truncate-toward-zero,
  integer-domain convention `§5.4.2` states for the Pet side. A `double`
  multiply-then-cast invites platform-dependent results; integer division on a
  non-negative numerator gives the documented values exactly. The `+20%`
  magnitude is stored as the positive `20` (`BOSS_RULES.md` §6.2.1), while
  `EffectiveAttack` reads `Math.Abs(Magnitude)` because a debuff is written
  negatively — match whichever reading the composition requires and keep it
  consistent with the instance the definition actually declares.
- **Rage-active testability without implementing step 18a.** The step 18a block
  applies no Passive effect, so no code path currently creates a Rage instance.
  Seed the Boss's `StatusEffects[]` with a documented `"ATK"` `BuffDebuff`
  instance (`TargetStat = "ATK"`, magnitude `20`, some `RemainingTurns`) in the
  **test fixture** and drive a real Turn through `BattleStateService` — the same
  technique `RootAttackConsumptionTests.cs` uses for the Pet side. Construct the
  instance through the existing `StatusEffect.TurnBased(...)` factory so the
  `TargetStat`-iff-`BuffDebuff` pairing holds by construction. **Do not** add a
  Rage application, a step-18a effect, or a test-only production code path.
- **The existing tests to reconcile.** `BossResponseTests.cs`'s
  `BossSkill_ShouldUseBossAttackPlusSkillBaseDamage` and
  `DamagePipelineTests.cs`'s `BossSkill_BaseDamage_ShouldBeBossAtkPlusSkillBaseDamage`
  both assert `boss.ATK + boss.SkillBaseDamage`. Under the contract that value
  is the **Rage-inactive** case and remains correct — make the
  modifier-absence explicit in each test's premise and expectation rather than
  changing the number. Also check `RootAttackConsumptionTests.cs` (~L347-349)
  and `ApiIntegrationTests.cs` (~L2528), which model the same Step-1 value.
- **Do not touch the Boss Basic Attack's number.** For `skillFires == false`,
  the Step-1 input is the derived ATK alone. If the derivation is applied to
  both branches uniformly, the Basic Attack's behavior is unchanged whenever no
  modifier is active, which is the documented MVP-default case.
- **`DamagePipeline.cs`'s own §3-step-1 comment** currently describes the Boss
  Skill input as `Boss.ATK + SkillBaseDamage`. If the composition makes that
  comment stale, correct the comment to reference the owner — do not change
  pipeline behavior. A comment correction inside `src/` is not a documentation
  change to `docs/`.
- **Current source behavior is not the authority.** `AGENTS.md` §2 puts code
  last; the composition is `COMBAT_RULES.md` §3.4's. Where the existing
  `BossDefinition`/`DamagePipeline` comments restate an older reading, the
  document governs.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — the Boss-side derived-ATK function: no modifier
                         (100 → 100); +20% (100 → 120); the integer/truncation
                         convention; selection by Type + TargetStat and never
                         by Id; DoT/Shield/State/non-"ATK" excluded; the base
                         stat and the instance collection both unmodified
                         (non-destructiveness, §5.5.4).
[x] Integration tests  — the step 18b resolution driven through the real
                         BattleStateService with a fired Boss Skill, asserting
                         the composed `DamageCalculated.base` and the unchanged
                         committed `BossState.ATK`.
[x] Gameplay scenarios — Given/When/Then derived from COMBAT_RULES.md §3.4's
                         composition and worked example (see below).
[ ] Full suite         — Domain + Application + Infrastructure + Api suites
                         passing, with no pre-existing test weakened.
```

### Required Scenarios (derived from the contract, not from the implementation)

```text
1. No Boss ATK modifier
   Given a Boss with BossState.ATK = 100
   And no active Boss "ATK" modifier instance
   And a Boss Skill whose authored Base Damage is 150
   When the Skill fires at step 18b
   Then EffectiveBossATK = 100
   And the Skill's Step-1 Base Damage = 100 + 150 = 250

2. Hỏa Long Rage active
   Given a Boss with BossState.ATK = 100
   And an active Boss "ATK" Turn-based BuffDebuff instance of magnitude +20%
   And a Boss Skill whose authored Base Damage is 150
   When the Skill fires at step 18b
   Then EffectiveBossATK = truncate(100 × 120 / 100) = 120
   And the Skill's Step-1 Base Damage = 120 + 150 = 270

3. Authored Skill Base Damage is not modified
   Given the same inputs as scenario 2
   When the Skill fires
   Then the Skill's authored Base Damage read from its definition is still 150
   And the entire +20 increase comes from the EffectiveBossATK contribution
   And the modifier reaches the Skill's Step-1 damage, not the authored value

4. Base stat immutability
   Given the same inputs as scenario 2
   When EffectiveBossATK is derived and the Skill resolves
   Then BossState.ATK on the committed state is still 100
   And no "restore"/reset-to-default step exists anywhere in the path

5. Damage Pipeline integration — exactly once
   Given a fired Boss Skill
   When the resolution completes
   Then exactly one DamageCalculated event carries the Skill instance
   And its `base` is the composed Step-1 value
   And no second pipeline invocation or double-applied modifier exists
   And Step 4's OtherModifiers is 1.0
   And the pipeline BaseDamagePool is 0 (Bosses match no Gems)

6. Basic Attack is unchanged
   Given a Boss whose Skill does not fire at step 18b
   When the step 18c Basic Attack resolves
   Then its Step-1 Base Damage = EffectiveBossATK alone
   And no authored Skill Base Damage contributes

7. Modifier selection (negative guards)
   Given a Boss holding a DoT, a Shield, a State, and a BuffDebuff naming a
   stat other than "ATK"
   When EffectiveBossATK is derived
   Then none of them reduces or increases it
   And a differently-named BuffDebuff naming "ATK" does apply
```

### Key Edge Cases

- **Same-Turn non-retroactivity** — `COMBAT_RULES.md` §5.5.5 references §5.4.3:
  a modifier applied at a later step of the same Turn cannot affect that Turn's
  already-resolved attack. The composition reads the committed instance state at
  attack resolution; assert the applying Turn's own attack is unaffected.
- **Modifier expiring at step 19a** — §5.3 DR5 / §2.3.1 item 8 remove an
  instance at `0` in the same pass, so the following Turn's derivation reads an
  absent instance and `EffectiveBossATK` returns to `BossState.ATK` with no
  residual state.
- **The Boss's own Burn DoT** — Flame Burst applies a Boss-sourced Burn to the
  **Pet**; the Boss's own `StatusEffects[]` DoT tick (step 19a) must not be
  mistaken for an ATK modifier.
- **A Shield on the Pet** — `COMBAT_RULES.md` §4 absorbs the Skill's damage; the
  composed Step-1 value is unchanged by absorption (absorption is downstream of
  Step 1).
- **`BossState.ATK` values other than 100** — the derivation is a function of
  the stored stat, not a constant; assert at least one non-100 value so a
  hard-coded `120` cannot pass.
- **Zero-length collection** — no active instances yields the stored stat
  unchanged.

---

## Stop Conditions

Universal stop conditions (`AGENTS.md` §20, `.ai/README.md` §13) always apply.
Task-specific stops — **STOP and report instead of guessing** if:

1. **The authored contract is missing or inconsistent.** If
   `COMBAT_RULES.md` §3.4's composition, §5.5's consumption rule, or the worked
   example are absent or contradictory, **STOP** and report both sources
   (file + section) per `AGENTS.md` §4. Do not supply, complete, or infer the
   composition.
2. **The composition cannot be implemented without implementing step 18a
   (Boss Passive effects).** This task consumes a modifier instance; it does not
   create one. If Rage-active behavior cannot be made deterministic and testable
   without also implementing Hỏa Long's Rage *application*, **STOP** and report
   — do not widen this task into the Passive half.
3. **The change requires a new state field.** Any persisted/derived
   `EffectiveBossATK` member, a second Boss ATK representation, or a new
   collection is forbidden. **STOP** and report.
4. **The change requires a new Battle Event, SignalR method/payload member, API
   endpoint, or Redis key.** **STOP** and report.
5. **The change requires a `DamagePipeline` behavioral change** — e.g. a new
   step, a reordered step, or a new input member. §3 step 1 already sums what
   the caller supplies. **STOP** and report; a possible ARCHITECTURE task is
   reported, not created.
6. **The change would alter Boss-side `Step 4 = 1.0`**, `BossState.ATK`
   semantics, §5.4's Pet-side rule, or §3.1's six-step order. **STOP** and
   report.
7. **A required value is unavailable.** If the derivation needs a magnitude,
   percentage, or rounding rule that `COMBAT_RULES.md` §5.5 /
   `BOSS_RULES.md` §6.2 do not state, that is a missing rule
   (`AGENTS.md` §7/§20), not a value to choose. **STOP** and report.
8. **TASK-118's secondary effects would be disturbed**, or the change would
   require modifying its step 18b ordering. **STOP** and report.
9. **An existing `Accepted` ADR would be contradicted**
   (`.ai/workflow/architecture/architecture-change.md` §3). **STOP** and report.
10. **Any work would touch `docs/`, an ADR, `TASK-125`, `TASK-126`, `TASK-118`,
    or any existing task file.** **STOP.**
11. **The work would exceed the 6 listed skills or cross an uncoupled
    architectural boundary.** Decompose instead (`tasks/README.md` §13).
12. **The task drifts into creating a documentation or decision task.**
    **STOP** — the contract is already authored.

Use `.ai/README.md` §13's exact report format. Unrelated issues discovered while
working are **report-only** (`AGENTS.md` §16) — do not fix them inline.

---

## Required Statement

This statement must appear **verbatim** in this task's execution report:

```text
Implementation task.
No documentation decision.
No gameplay redesign.
Consumes existing COMBAT_RULES.md §3.4 contract.
No new Boss Skill damage formula.
No new Boss ATK state field.
No new SignalR event.
No new API endpoint.
No new Redis key.
No PostgreSQL BattleState persistence.
```

Also required, verbatim:

```text
Do not change COMBAT_RULES.md §3.4.
Do not reinterpret TASK-125.
Do not reopen GAP-1.
```

---

## Required Statement (as executed)

```text
Implementation task.
No documentation decision.
No gameplay redesign.
Consumes existing COMBAT_RULES.md §3.4 contract.
No new Boss Skill damage formula.
No new Boss ATK state field.
No new SignalR event.
No new API endpoint.
No new Redis key.
No PostgreSQL BattleState persistence.
```

```text
Do not change COMBAT_RULES.md §3.4.
Do not reinterpret TASK-125.
Do not reopen GAP-1.
```

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Changed Files

Source (4 files):

- `src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs` — **added
  `EffectiveBossAttack(int, IReadOnlyList<StatusEffect>)`** (L742), the
  `COMBAT_RULES.md` §5.5.1 Step-1 `Attack` value for a Boss attack: the Boss's
  stored ATK modified by every active `TargetStat = "ATK"` Turn-based
  `BuffDebuff` instance. This is the **EffectiveBossATK implementation
  location**, placed beside the existing Pet-side `EffectiveAttack` as the same
  kind of pure Domain function — no new type, manager, registry, or factory.
  Selection reuses the existing `IsAtkBuffDebuff` selector (`Type` +
  `TargetStat`, never `Id`); arithmetic is integer division on a non-negative
  numerator (truncate toward zero, §5.5.1 / §5.4.2); the magnitude's sign
  selects buff (`100 + m`) vs debuff (`100 − m`) so no flag is introduced;
  magnitudes outside the documented 0–100 percentage range are rejected rather
  than reinterpreted. The method reads and writes nothing — the stored stat and
  the instance collection are both untouched (§5.5.4).
- `src/backend/GameServer.Application/Battle/BattleStateService.cs` — **the
  Boss Skill Step-1 composition location** (step 18b, L1389–L1417). Added
  `var effectiveBossAtk = StatusEffectLifecycle.EffectiveBossAttack(
  bossState.ATK, bossState.ActiveStatusEffects);` and changed `bossAttack` to
  `skillFires ? effectiveBossAtk + bossDefinition.SkillBaseDamage :
  effectiveBossAtk`. This changes **only** the `Attack:` argument of the one
  pre-existing `DamagePipeline.Calculate` call; the damage instance, shield
  handling, TASK-118 secondary-effect block, and charge/cooldown reset keep
  their existing order and the same single post-resolution write-back. The
  `bossAttack` comment was rewritten to state the consumed contract and to
  record that the modifier reaches Step 1 and never Step 4.
- `src/backend/GameServer.Domain/Combat/DamagePipeline.cs` — **comment only.**
  The §3-step-1 comment described the Boss Skill input as
  `Boss.ATK + SkillBaseDamage`; it now names
  `EffectiveBossATK + the Skill's authored Base Damage` and cites §3.4 / §5.5.
  **No pipeline behavior changed** — `Calculate` and its six steps are
  byte-identical apart from that comment.
- `src/backend/GameServer.Domain/Bosses/BossDefinition.cs` — **comment only.**
  `SkillBaseDamage`'s XML doc previously stated the Skill's Base Damage "is
  `BossState.ATK + SkillBaseDamage`". It now states the composition is
  `EffectiveBossATK + SkillBaseDamage`, that the authored value is a separate
  additive Step-1 contribution, and that a Boss ATK modifier reaches the
  Skill's damage only through the `EffectiveBossATK` contribution. **No
  declaration, value, or effect binding changed.**

Tests (5 files):

- `tests/backend/GameServer.Domain.Tests/EffectiveBossAttackTests.cs` —
  **new; 21 tests** for the derived-ATK consumer itself: the unreduced case;
  §3.4's worked example (`100 → 120`); the percentage applied to several stored
  stats (so a hard-coded `120` cannot pass); truncation toward zero
  (`50 → 60`, `51 → 61`, `7 → 8`, `3 → 3` — the `3.6 → 3` case is the one a
  rounding implementation would fail); selection by `Type` + `TargetStat` and
  **not** by `Id` (a differently-named `"ATK"` instance applies); `BuffDebuff`
  naming another stat ignored; `DoT` / `Shield` / `State` ignored; a negative
  magnitude reducing (`100 → 70`); order-independence across two simultaneous
  instances (`100 → 120 → 84`); base and collection preservation
  (§5.5.4); the expired case read as the documented step-19a removal; and the
  composition itself (`100 + 150 = 250`; `120 + 150 = 270`, with the entire +20
  attributed to the `EffectiveBossATK` contribution).
- `tests/backend/GameServer.Application.Tests/BossSkillStep1CompositionTests.cs`
  — **new; 13 tests** driving real Turns through `BattleStateService`:
  scenario 1 (no modifier → `250`), scenario 2 (`+20%` → `270`), scenario 3
  (the recovered authored contribution is still the declared `150`), scenario 4
  (committed `BossState.ATK` still `100` before and after, and the modifier
  instance read rather than consumed), scenario 5 (the composed value enters
  the existing pipeline exactly once, `OtherModifiers` `1.0`, `ComboModifier`
  `1.0`), scenario 6 (Basic Attack is `100` with no modifier and `120` with
  one — never `270`), scenario 7 (a differently-named `"ATK"` instance applies;
  a `"DEF"` `BuffDebuff`, a `DoT`, and a `State` do not), a non-default
  `ATK 200` deriving `240 + 150 = 390`, TASK-118's Burn still applied at
  50 / 2 Turns alongside the composition, and the Boss's **own** Burn `DoT`
  not being mistaken for an ATK modifier.
- `tests/backend/GameServer.Application.Tests/BossResponseTests.cs` —
  `BossSkill_ShouldUseBossAttackPlusSkillBaseDamage` reconciled: the raw-ATK
  reading it asserted is §3.4's **Rage-inactive** case, so the
  modifier-absence is now an explicit premise (`Assert.Empty(
  created.BossState.ActiveStatusEffects)`) and the assert documents which case
  it covers. **The expected number is unchanged (250) and nothing was
  weakened.**
- `tests/backend/GameServer.Domain.Tests/DamagePipelineTests.cs` —
  `BossSkill_BaseDamage_ShouldBeBossAtkPlusSkillBaseDamage` reconciled the same
  way: it now derives its ATK term through
  `StatusEffectLifecycle.EffectiveBossAttack(boss.ATK, [])` and asserts that
  equals the stored stat, making the Rage-inactive premise explicit. **250
  unchanged.**
- `tests/backend/GameServer.Application.Tests/RootAttackConsumptionTests.cs` —
  comment only: the Basic Attack Step-1 comment now cites §3.4's composition
  and §5.5.1 rather than the superseded "defined per Skill" wording. The
  asserted `100` is unchanged.
- `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs` — the hand-rolled
  expectation's `bossAttack` now reads the same documented
  `EffectiveBossAttack` consumer rather than assuming the stored stat, so the
  expectation tracks the contract instead of the previous implementation. No
  expectation value changed (no MVP path applies a Boss ATK modifier).

**No other file changed.** `docs/**` (39 files) is **byte-identical**,
`src/frontend/**` is untouched, and `tasks/backlog/TASK-125-*`,
`tasks/completed/TASK-126-*`, and `tasks/backlog/TASK-118-*` are byte-identical
at their baseline SHA256 values.

### Validation Results

```text
dotnet build src/backend/GameServer.sln   — PASS (0 errors; 19 warnings, all
                                            pre-existing)

dotnet test  src/backend/GameServer.sln   — PASS (2330), 0 failed, 0 skipped
  GameServer.Application.Tests     — PASS (442)
  GameServer.Domain.Tests          — PASS (1296)
  GameServer.Infrastructure.Tests  — PASS (327)
  GameServer.Api.Tests             — PASS (265)
  (run 3× consecutively: identically green each time)

Targeted, before the full run:
  EffectiveBossAttackTests                 — PASS (21)
  BossSkillStep1CompositionTests           — PASS (13)
  BossResponseTests + DamagePipelineTests
    + RootAttackConsumptionTests
    + BossSkillSecondaryEffectTests
    + EffectiveAttackTests                 — PASS (100 Domain, 57 Application)

Frontend (unchanged by this task, run to prove no regression):
  npx vitest run — 18 files, PASS (477), 0 failed

Baseline, before this task:
  Application 429 → 442 (+13)   Domain 1275 → 1296 (+21)
  Infrastructure 327 (unchanged)  Api 262 → 265 (+3, from the pre-existing
  working-tree changes already present when this task began)

The suites grew by 34 tests, all of them this task's. No existing test was
deleted or weakened; the two reconciled tests assert the same value.
```

### Contract Evidence

```text
Boss Skill Step-1 composition implemented at:
  src/backend/GameServer.Application/Battle/BattleStateService.cs L1389-L1417
    effectiveBossAtk = StatusEffectLifecycle.EffectiveBossAttack(
                           bossState.ATK, bossState.ActiveStatusEffects);
    bossAttack       = skillFires
                           ? effectiveBossAtk + bossDefinition.SkillBaseDamage
                           : effectiveBossAtk;

EffectiveBossATK implementation location:
  src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs L742
    public static int EffectiveBossAttack(int attack,
                                          IReadOnlyList<StatusEffect> effects)

Worked example, asserted end-to-end through a real Turn:
  BossState.ATK = 100, Rage +20%  → EffectiveBossATK = 120
  + Flame Burst authored 150      → Step-1 Base Damage = 270
  Rage inactive                   → 100 + 150 = 250
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed the composed Step-1 value reaches the client only through the
      existing `DamageCalculated.base`
- [x] Confirmed `BossState.ATK` never overwritten and `EffectiveBossATK` never
      persisted (no `Effective*` member exists on `BossState`, `BattleState`,
      the serializer, or any DTO; `BossEffectiveATK`, `EffectiveBossSkillATK`,
      `SkillAttackStat`, and `BossSkillATK` have **zero** occurrences in
      `src/` and `tests/`)
- [x] Confirmed no new state field, Battle Event, wire member, Redis key, or
      database column
- [x] Confirmed `COMBAT_RULES.md` §3.1's six-step order and §3.4's Boss-side
      `Step 4 = 1.0` are unchanged
- [x] Confirmed §5.4's Pet-side rule is unaffected (step 15 still calls
      `StatusEffectLifecycle.EffectiveAttack`; no second Pet-side path added)
- [x] Confirmed TASK-118's three secondary effects are undisturbed (Burn
      50 / 2 Turns, Drain Power −20 floored at 0, Root `"ATK"` `BuffDebuff`
      all still asserted by their own suites, which pass unchanged)
- [x] Confirmed zero files under `docs/` modified (39 files byte-identical)
- [x] Confirmed adherence to MVP Scope (`docs/00-overview/MVP_SCOPE.md` §1)

### Reported (not fixed — `AGENTS.md` §16)

Hỏa Long's Rage **application** remains unimplemented: it is step 18a's Boss
Passive effect and is explicitly out of this task's scope. No production code
path therefore creates a Rage instance, so the Rage-active scenarios arrange
one through the documented `StatusEffectLifecycle.Apply` on the Boss's own
collection — the same technique `RootAttackConsumptionTests` already uses for
the Pet side. The composition itself is fully deterministic and correct
against whatever committed `BossState.StatusEffects[]` state a resolution
receives; without this task's implementation it would have produced `250`
instead of the contract's `270` the moment Rage landed. This is a scope
observation, not a defect, and it is the separate Passive half of
`ROADMAP.md` Phase 1.

---

## Review Evidence

<!--
  RECORDED BY THE REVIEW AGENT per .ai/workflow/quality/review.md §1/§3 and
  .ai/agents/review.md's Output Contract. Review-only: no implementation,
  test, or `docs/` file was modified to produce this record.
-->

```text
Review Status:        CHANGES REQUIRED (1 Major finding — see Findings)
Reviewed Commit:      0cbcdc1a15a78ca33b9a7dc0df0461c79bd63d6c (HEAD)
Reviewed Working Tree: uncommitted TASK-127 changes present; reviewed directly
Reviewer:             review agent (review-only; no fixes applied)
Risk / Depth:         HIGH (TASK_TYPES.md §4) — full quality/review.md §1
                      checklist, core/validation.md §2 HIGH depth
```

### Contract Verified

```text
COMBAT_RULES.md §3.4 ("Boss Skill Step-1 composition")  VERIFIED as the
  specification implemented. Step 1 = EffectiveBossATK + authored Skill Base
  Damage; worked example 120 + 150 = 270; Rage-inactive 100 + 150 = 250.
COMBAT_RULES.md §5.5.1–§5.5.4   VERIFIED as the consumption rule and
  boundaries (Step-1 input only; Step 4 = 1.0; base stat never overwritten;
  EffectiveBossATK derived and non-stored).
COMBAT_RULES.md §5.4.1 item 3   READ as the Pet-side analogue.
BOSS_RULES.md §6.2.1 / §6.3.1   VERIFIED: Rage stored Magnitude = +20%; the
  authored 150 / 120 / 100 values unchanged.
```

### Implementation Verified

```text
EffectiveBossATK:   IMPLEMENTED at
                    StatusEffectLifecycle.EffectiveBossAttack (L742).
                    Derives from BossState.ATK; reads the BOSS's own
                    BossState.StatusEffects; selects by Type + TargetStat via
                    the shared IsAtkBuffDebuff; no Id / CardId / SkillId /
                    hard-coded Boss-identity lookup; no Math.Round / Floor /
                    Ceiling (integer division on a non-negative numerator);
                    does not mutate the stat or the collection; no persisted
                    member (zero Effective* members anywhere).
Boss Skill Step-1:  IMPLEMENTED at BattleStateService.cs L1389–L1417.
                    effectiveBossAtk + bossDefinition.SkillBaseDamage, passed
                    to the ONE pre-existing DamagePipeline.Calculate call. The
                    authored value is passed through unchanged. No second
                    damage path, no duplicate addition.
Basic Attack:       VERIFIED unchanged — receives effectiveBossAtk alone and
                    no authored Skill term (BattleStateService.cs L1415–L1417).
DamagePipeline:     VERIFIED behaviorally unmodified. Calculate and steps 1–6
                    are unchanged; the only edit in DamagePipeline.cs is a
                    §3-step-1 comment. No Crit / Burn / Shield / Defense /
                    Combo / RNG change.
TASK-118 effects:   VERIFIED undisturbed — Burn 50/2, Drain Power −20 floored
                    at 0, Root "ATK" BuffDebuff all still pass their own
                    suites unchanged.
Boss Passive:       VERIFIED NOT implemented — step 18a applies no effect; no
                    Rage application, no PassiveTracker change, no new event.
                    Tests seed an existing instance (consuming, not creating).
```

### Tests Verified

```text
Executed independently by the review agent:

  dotnet build src/backend/GameServer.sln   PASS (0 errors)
  dotnet test  src/backend/GameServer.sln   PASS (2330), 0 failed
    Application 442 | Domain 1296 | Infrastructure 327 | Api 265

Test-validity review: expected values trace to §3.4 / §5.5 / §6.3.1 rather
than to the implementation; the two reconciled tests keep their original
expected value (250) and only make the modifier-absence premise explicit; no
test was deleted or weakened. HOWEVER, the new suites encode the
implementer's sign convention rather than the contract's (Finding 1): the
Rage fixture seeds magnitude +20, so the tests pass while the multi-instance
and negative-magnitude expectations are the implementation's own.
```

### Scope Verified

```text
No new BattleState / BossState field ......... yes (zero Effective* members)
No new StatusEffect type ..................... yes
No new event ................................. yes
No new SignalR member / API / Redis / DB ..... yes
No PostgreSQL BattleState persistence ........ yes
No frontend change ........................... yes (all frontend mtimes predate)
No new gameplay rule / Boss Skill / mechanic . yes
No speculative abstraction ................... yes (no BossDamageManager,
                                                BossCombatManager,
                                                UniversalBossEngine,
                                                BossAttackCalculator)
```

### Documentation Integrity Verified

```text
docs/ — NOT modified. 39 files byte-identical across the review (SHA256
  aggregate re-checked before and after). No ADR created or modified.
TASK-125 (E9374FBE…), TASK-126 (7A6CC1D3…), TASK-118 (F975FF00…) —
  byte-identical at their pre-review hashes; lifecycle states unchanged.
```

### Findings

```text
| # | Severity | File / Section                        | Finding |
|---|----------|---------------------------------------|---------|
| 1 | Major    | StatusEffectLifecycle.cs L780-L788    | The Boss-side percentage |
   |          | (EffectiveBossAttack)                 | direction is inferred |
   |          |                                       | from the SIGN of |
   |          |                                       | Magnitude. No |
   |          |                                       | authoritative document |
   |          |                                       | makes Magnitude's sign |
   |          |                                       | meaningful, and §5.4.1 |
   |          |                                       | item 3 uses |Magnitude| |
   |          |                                       | for the Pet. |
```

**Finding 1 — detail (Major).**

```text
Where:  src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs
        L780-L788, inside EffectiveBossAttack.

What:   `var isDebuff = effect.Magnitude < 0;` selects
        `effective * (100 - m) / 100` (negative) or
        `effective * (100 + m) / 100` (positive).

Why it is a finding:  Magnitude's sign is NOT documented as semantic.
  - GAME_STATE.md §2.3.1 item 2: "Magnitude is typed but not interpreted
    here. What the number means ... is owned by the effect's rule document."
    No sign convention is stated.
  - COMBAT_RULES.md §5.4.1 item 3 (the only authored percentage rule) is
    `EffectiveATK = truncate(ATK x (100 - |Magnitude|) / 100)` — it takes the
    ABSOLUTE value and always reduces. The Pet-side consumer
    (StatusEffectLifecycle.EffectiveAttack L654) matches it exactly.
  - BOSS_RULES.md §6.2.1 fixes Rage's stored `Magnitude = +20%` (positive),
    while §6.3.1 item 3 fixes Root as a "-30% ATK debuff" that production
    stores as the POSITIVE 30 (BossDefinitions.cs L219) — i.e. a debuff is
    NOT "written negatively".
  - COMBAT_RULES.md §5.5.1 item 3 says "EffectiveBossATK = the MODIFIED
    BossState.ATK" — direction-neutral — whereas §5.4.1 item 2 deliberately
    says "the REDUCED PetState.ATK". §5.5.1 authors no formula and no sign
    rule; it says only "the active instance's Magnitude supplies it".

Observed consequence (independently probed against the built Domain assembly,
using the documented production magnitudes):
  ATK 100 + Rage    magnitude +20  -> 120   correct (matches §3.4)
  ATK 100 + a boss  magnitude +30  -> 130   an ATK DEBUFF is applied as a BUFF
  ATK 100 + a boss  magnitude -20  ->  80    a +20% BUFF would be applied as a DEBUFF

  The first row is the only case TASK-127's tests exercise, which is why the
  suite is green.

Source-of-truth reference: COMBAT_RULES.md §5.4.1 item 3 (the |Magnitude|
reading); GAME_STATE.md §2.3.1 item 2 (sign not interpreted); BOSS_RULES.md
§6.2.1 (Magnitude = +20%) and §6.3.1 item 3 (Root stored as positive 30).

Smallest recommended correction (for the implementation agent to make, NOT
made here):  drop the sign branch and follow §5.4.1 item 3 —
`effective = effective * (100 - (int)Math.Abs(effect.Magnitude)) / 100` —
which for a +20 buff would yield 80, NOT the contract's 120. **This does not
by itself produce §3.4's 120**, which is exactly why this is a contract
question and not a mechanical fix (see "Why this is not a one-line fix").
```

**Why this is not a one-line fix — and why it does not block on a new decision.**

```text
§3.4's worked example requires Magnitude +20 to INCREASE the stat to 120,
but §5.4.1 item 3's only authored formula (ATK x (100 - |Magnitude|) / 100)
can only REDUCE. §5.5.1 item 3 supplies no formula and no direction, and
§5.5.2/§5.5.3 add none. So the two authoritative sections do not jointly
determine how a positive Rage magnitude raises the stat.

This is NOT a Blocker in the sense of a missing gameplay decision: §5.5.1's
own flow diagram and §3.4's worked example BOTH state the +20% increases
100 -> 120 unambiguously, and §5.5.2 states the modifier reaches the Skill's
Step-1 damage through that contribution. The required BEHAVIOR is decided;
only its PERCENTAGE-APPLICATION FORMULA is unauthored. The implementation is
therefore under-specified against, not in conflict with, a decided contract.

Two candidate readings, both consistent with §3.4's outcome:
  (a) a Boss-side buff/debuff distinction derived from a documented signal
      (e.g. the instance identity or an authored direction member), or
  (b) a Boss-side formula authored at §5.5.1 mirroring §5.4.1 item 3.

Selecting one is a documentation/contract act, which this review is not
authorized to perform (.ai/agents/review.md → Decision Authority: "Changing
game rules" is Not allowed). It is therefore reported for the owning task,
not decided here.
```

### Regression Status

```text
No regression introduced. Build clean; all 2330 backend tests pass; TASK-118's
Burn / Drain Power / Root suites pass unchanged; §5.4's Pet-side path and its
21 Domain tests pass unchanged. The single defect is confined to the new
Boss-side branch and is unreachable in MVP play today (no code path applies a
Rage instance yet), so it is latent rather than actively miscomputing a live
battle.

Failure attribution: the 0 failing tests are attributable to no test — the
suites pass because they exercise only the magnitude-+20 case. Finding 1 is a
behavior/contract defect the suite does not cover, not a failing test.
```

### Reviewer Decision

```text
CHANGES REQUIRED
```

`core/completion.md` §1 ("Implementation completed AND relevant validation
passed") is not satisfied while Finding 1 stands: the implementation is not
demonstrably correct against §5.5.1 for any ATK-modifier magnitude other than
the single +20 case its tests seed. Returning to the implementation agent per
`.ai/agents/review.md` Handoff ("Review → Specialist Agent: the finding, its
severity, the authoritative source it violates, what needs to change").

**Not applied by this review** (`.ai/agents/review.md`: the Review Agent "Must
NOT fix implementation defects"): no source, test, or `docs/` change was made.

### Lifecycle

```text
TASK-127 remains IN REVIEW at
  tasks/backlog/TASK-127-implement-boss-skill-step-1-damage-composition.md

The IN REVIEW → DONE transition is NOT taken: quality/review.md §2 makes
review non-optional and core/completion.md §1 is unmet. Per
TASK_LIFECYCLE.md §2 the correct transition for a review finding is
IN REVIEW → IN PROGRESS (rework), not DONE, and §4 keeps the file in
backlog/ until DONE. This review did not move the file and did not perform
the rework transition itself; the task file is left IN REVIEW for the
implementation agent to pick up.
```

### Unrelated Issues (`AGENTS.md` §16 format — reported only, not blocking)

```text
Issue:    Hỏa Long's Rage application (step 18a Boss Passive effect) is
          unimplemented, so no production path creates the Rage instance
          TASK-127's composition consumes.
Location: BattleStateService.cs step 18a (L1305–L1347) — charges and emits
          PassiveCharged/PassiveTriggered, applies no effect.
Impact:   The composed 270 path is unreachable in live play; the Boss Skill
          still deals 250. TASK-127 is correct-by-construction against a
          future step-18a implementation.
Follow-up: the separate Boss Passive implementation task (TASK-123's
          remaining scope / ROADMAP.md Phase 1 Passive half). Not created
          here.
```

---

## Rework Session — Review Finding 1 (BLOCKED, no source change)

<!--
  RECORDED BY THE IMPLEMENTING AGENT on resuming after the review's
  CHANGES REQUIRED. The instructed first action was to VERIFY THE CONTRACT
  before changing source. The verification was performed and it establishes
  that the direction semantics are NOT authoritatively determined. Per the
  rework instructions' critical stop rule, no source, test, or docs change
  was made.
-->

```text
Status:            BLOCKED — awaiting the owning contract decision.
Source changes:    NONE.
Test changes:      NONE.
docs/ changes:     NONE (39 files still byte-identical).
```

### Blocker

```text
TASK-127 cannot safely continue.

Reason:
Boss-side BuffDebuff ATK percentage DIRECTION is not defined by any
authoritative document. The implementation therefore has to invent a
direction rule, which is exactly the review's Major finding.
```

**The exact unanswered question.**

```text
Given:  Type = BuffDebuff
        TargetStat = "ATK"
        Magnitude = 30

How does the Boss-side consumer know whether this means +30% or -30%?
```

No authoritative document answers it, and no representation field carries it.

### Evidence (each item re-verified in this session)

```text
1. COMBAT_RULES.md §5.5.1 item 3 (L1022-L1026) — DIRECTION-NEUTRAL.
     "EffectiveBossATK = the modified BossState.ATK"
   §5.5.1 authors NO formula at all, unlike §5.4.1 item 3 which authors one
   for the Pet. Its "Percentage application and rounding" bullet (L1034-1038)
   supplies only the truncation convention and says "This rule authors no
   percentage of its own; the active instance's Magnitude supplies it".
   It does not say whether Magnitude is added or subtracted.

2. COMBAT_RULES.md §5.4.1 item 3 (L858) — THE ONLY AUTHORED FORMULA, and it
   is PET-SCOPED and UNIDIRECTIONAL:
     "EffectiveATK = truncate( ATK × (100 − |Magnitude|) / 100 )"
   It takes the ABSOLUTE value and only ever REDUCES. §5.4.1 item 2 (L850)
   says "the reduced PetState.ATK". So the Pet-side rule cannot express a
   buff at all, and cannot be the Boss-side buff rule.
   §5.4's own scope is the Pet: §5.4.1 L837-838 "modifies the active Pet's
   ATK"; §5.4.5 L965 "Does NOT apply to the Boss's damage".

3. §5.5.1's flow diagram and §3.4's worked example REQUIRE AN INCREASE
   (L1016 "+20% Rage modifier"; §3.4 L517 "truncate(100 × (100 + 20) / 100)
   = 120"). So the Boss side must be able to raise ATK, while the only
   authored formula can only lower it. The two sections do not jointly
   determine the arithmetic.

4. BOSS_RULES.md §6.2.1 L238 — "Magnitude = +20%" (positive) for a BUFF.
   BOSS_RULES.md §6.3.1 item 3 L381 / BossDefinitions.cs L219 — Root is a
   "-30% ATK debuff" whose stored Magnitude is the POSITIVE 30.
   => Sign does NOT distinguish buff from debuff in existing content; a buff
   and a debuff both carry positive magnitudes.

5. GAME_STATE.md §2.3.1 item 2 (L1179-1187) — "Magnitude is typed but not
   interpreted here. What the number means ... is owned by the effect's rule
   document." No sign semantics are assigned, and the schema tree (L1155-1171)
   lists only Id / Type / Source / Magnitude / TargetStat / RemainingTurns /
   ExpiryCondition.

6. NO SEMANTIC DISCRIMINATOR EXISTS. Searched docs/ for
   Operation | ModifierDirection | EffectType | Direction | buff-vs-debuff
   and src/backend for IsBuff | IsDebuff | Operation | Direction | EffectKind:
   the StatusEffect model has NO buff/debuff direction member. Type's four
   members (DoT | BuffDebuff | Shield | State, StatusEffectType.cs L30-76)
   select the duration model and do NOT distinguish buff from debuff —
   "BuffDebuff" is a single combined category.
   Source (Player | Boss) is documented as "a label, not a lookup ... no rule
   reads it to decide behavior" (StatusEffectType.cs L94-96), so it cannot
   carry direction either.

7. ADR-017 Option E (L390-399) confirms by precedent that adding a
   StatusEffect representation for a missing semantic is "a gameplay decision
   owned by COMBAT_RULES.md, not by this state", and may not be done from the
   implementation.
```

### Why This Is a Contract Gap, Not an Implementation Detail

```text
The implementation's current `Magnitude < 0` branch is an INVENTED rule. It
is not merely inaccurate: it applies an ATK DEBUFF as a BUFF (magnitude +30 →
130 on ATK 100), which is a wrong game outcome for any future Boss-side debuff.
But removing the branch does not fix it either — following §5.4.1 item 3's
absolute-value formula would turn the documented +20 buff into a reduction
(100 → 80), contradicting §3.4's worked example of 120.

Every available option requires a gameplay decision:
  (a) authorize a Boss-side buff formula at §5.5.1 (mirroring §5.4.1 item 3
      but bidirectional), or
  (b) state a documented direction signal for the instance (e.g. that a
      Boss-side ATK modifier's direction is carried by a field that does not
      yet exist), or
  (c) state that Boss-side ATK modifiers are always reductions, and reconcile
      §3.4's 120 example.

(a) and (b) are game-rule/contract acts; (c) would contradict TASK-125's
recorded decision and §3.4, which this task must NOT reopen. None is the
implementing agent's to choose (AGENTS.md §7, §20; TASK-127 §8 stop
condition 7: "A required value is unavailable ... that is a missing rule, not
a value to choose").
```

### Required Decision

```text
Define how a Boss-side BuffDebuff ATK modifier's DIRECTION is represented
and interpreted — i.e. author the Boss-side counterpart of
COMBAT_RULES.md §5.4.1 item 3's formula at §5.5.1, and state whether it is
bidirectional (buff vs debuff) and how the instance's direction is carried.

Owner:           COMBAT_RULES.md §5.5.1 (the canonical owner of how a
                 Boss-side BuffDebuff Magnitude reaches its TargetStat).
                 Decided by the Product Owner per GAME_RULES.md §20.
Smallest scope:  one formula/statement at COMBAT_RULES.md §5.5.1. No new
                 BattleState member, no new StatusEffect Type, no new event.
                 If a direction signal is chosen that the instance must
                 carry, that is a GAME_STATE.md §2.3.1 representation change
                 and needs its own authorization.
Not authorized:  TASK-127 to invent the formula, add a state field, alter
                 §3.4, or reopen TASK-125 / GAP-1.
```

### Scope Confirmation

```text
[ ] No source file modified in this session.
[ ] No test added, changed, or removed in this session.
[ ] docs/ byte-identical (39 files) — no authoritative documentation change.
[ ] TASK-118 / TASK-123 / TASK-124 / TASK-125 / TASK-126 untouched.
[ ] No new contract, state field, event, SignalR/API/Redis/DB change.
[ ] No frontend change.
[ ] No Damage Pipeline change.
[ ] No Boss Passive application implemented.
[ ] TASK-127 was NOT expanded to invent the missing rule.
[ ] No new task created (not required by the task-generation workflow here);
    the decision is reported to the owning contract instead.
```

**TASK-127 is not DONE.** Review Finding 1 (Major) remains unresolved and
unresolvable from the current authoritative documents.

---

## Rework Session 2 — Review Finding 1 RESOLVED (implementation)

<!--
  RECORDED BY THE IMPLEMENTING AGENT on resuming after TASK-128 recorded the
  Product Owner decision and TASK-129 applied it at its canonical owner.
  The contract is no longer ambiguous: COMBAT_RULES.md §5.5.1 now authors the
  Boss-side signed-Magnitude formula. This session removes the rejected
  sign-inference branch and derives the value from that formula.
-->

```text
Status:            IN REVIEW — rework implemented; the previous CHANGES REQUIRED
                   (Finding 1, Major) is resolved. `quality/review.md` is the
                   remaining gate.
Source changes:    2 files (StatusEffectLifecycle.cs, DamagePipeline.cs comment).
Test changes:      2 files.
docs/ changes:     NONE. Authored by TASK-129, not here.
```

### The defect that was removed

```text
Before (rejected):  var isDebuff = effect.Magnitude < 0;
                    effective = isDebuff
                        ? effective * (100 - magnitude) / 100
                        : effective * (100 + magnitude) / 100;
                    with magnitude = (int)Math.Abs(effect.Magnitude);

After (authoritative):
                    var magnitude = (int)effect.Magnitude;
                    effective = effective * (100 + magnitude) / 100;
```

The direction is no longer inferred from the sign as a separate semantic lookup.
The sign is the addend's sign inside §5.5.1's single documented expression, so a
negative Magnitude decreases through the arithmetic itself and no buff/debuff
branch, flag, field, or member exists. `Math.Abs` and the identifier `isDebuff`
now have **zero** occurrences in `EffectiveBossAttack` — the only remaining
`Math.Abs` in the file (L654) is the untouched Pet-side `EffectiveAttack`, whose
§5.4.1 `|Magnitude|` reading is deliberately different and unchanged.

### Implementation

- `EffectiveBossATK` now follows the signed-Magnitude Boss-side formula.
- Positive, negative, and zero modifiers verified: `+20 → 120`, `-30 → 70`,
  `0 → 100`, no instance → `100`.
- Truncation toward zero verified in **both** directions (`51, +20 → 61`;
  `51, -30 → 35`; `7, -30 → 4`).
- Boss Skill Step-1 composition uses
  `EffectiveBossATK + authored Skill Base Damage` (`120 + 150 = 270`;
  `70 + 150 = 220`; `100 + 150 = 250`).
- Basic Boss Attack uses `EffectiveBossATK` alone (`100`, `120`, or `70` —
  never a Skill term).

### Tests

```text
dotnet build src/backend/GameServer.sln
  PASS — 0 errors, 2 warnings (both pre-existing package-conflict warnings)

dotnet test src/backend/GameServer.sln
  PASS (2344), 0 failed, 0 skipped
    GameServer.Application.Tests     — PASS (446)
    GameServer.Domain.Tests          — PASS (1306)
    GameServer.Infrastructure.Tests  — PASS (327)
    GameServer.Api.Tests             — PASS (265)

Targeted before the full run:
  EffectiveBossAttackTests                 — PASS (31)
  BossSkillStep1CompositionTests           — PASS (17)

Frontend (unchanged by this task, run to prove no regression):
  from src/frontend/client:  npx vitest run — 18 files, PASS (477), 0 failed

Baseline before this rework session:
  Domain 1296 → 1306 (+10)   Application 442 → 446 (+4)
  Infrastructure 327 (unchanged)   Api 265 (unchanged)
```

The new cases are the ones the previous suite lacked: the **negative** modifier
(the defect the rework exists to fix), the **zero** modifier, decrease-direction
truncation, the Boss/Pet convention-separation guard, and the
decrease-composed Skill and Basic Attack values. No existing test was deleted,
weakened, or made vacuous; the two reconciled tests keep their original expected
value (250).

### Files Changed

Source (2):

- `src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs` —
  `EffectiveBossAttack` reworked to §5.5.1's authored formula
  `truncate( BossState.ATK × (100 + Magnitude) / 100 )` with the signed
  `Magnitude` as the single addend; the `isDebuff` branch, the `Math.Abs`
  reading, and the 0–100 magnitude range rejection were removed (they existed
  only to serve the absolute-value fold, which §5.5.1 does not author and which
  cannot express a decrease). XML docs rewritten to cite §5.5.1 items 3/4 and to
  state the deliberate non-normalization against §5.4.1.
- `src/backend/GameServer.Domain/Combat/DamagePipeline.cs` — **comment only.**
  Extended the §3-step-1 comment with §5.5.1's signed formula and a statement
  that the derivation belongs to the Domain consumer, so the pipeline stays
  unaware of `BossState`/`StatusEffect`. **No behavior changed.**

Tests (2):

- `tests/backend/GameServer.Domain.Tests/EffectiveBossAttackTests.cs` — the
  `-30` decrease case made load-bearing (it now asserts the value the rejected
  branch inverted: `70`, not `130`); added a three-direction theory
  (`+20/-0/-30`), decrease-direction truncation cases (`51→35`, `7→4`, `13→9`),
  a net-negative multi-instance case, a decrease-composed Skill case
  (`70 + 150 = 220`), and a Boss/Pet convention-separation guard (the same
  `Magnitude = 30` instance reads `130` on the Boss side and `70` on the Pet
  side).
- `tests/backend/GameServer.Application.Tests/BossSkillStep1CompositionTests.cs`
  — added the end-to-end decrease scenarios: a real Turn with a `-30` modifier
  composing `220` (and explicitly not the old inverted `280`), committed
  `BossState.ATK` still `100`, a zero modifier composing `250`, and a Basic
  Attack of `70`.

No other file changed. `docs/**`, `src/frontend/**`, every other `src/` and
`tests/` file, and every other task file are byte-identical.

### Scope

```text
[x] No documentation changes.              docs/ untouched by this session;
                                           §5.5.1 was authored by TASK-129.
[x] No new state representation.           zero new members; the sign of the
                                           existing Magnitude field carries
                                           direction.
[x] No new transport/persistence contract. zero new events, SignalR/API
                                           members, Redis keys, or DB columns.
[x] No gameplay expansion.                 no new Boss Skill, passive, Root,
                                           Rage application, or content.
[x] No Pet-side formula change.            EffectiveAttack and §5.4.1 untouched.
[x] No Root semantics change.              Root remains Pet-side, Magnitude 30.
[x] No DamagePipeline semantics change.    steps 1-6 byte-identical; comment only.
[x] No new direction field/flag/member.    IsBuff / IsDebuff / Direction / etc.
                                           have zero occurrences in src/ tests/.
[x] No ADR created or modified.
[x] No new task created.
```

### Required Statement (this session)

```text
Implementation task.
No documentation decision.
No gameplay redesign.
Consumes existing COMBAT_RULES.md §3.4 contract.
No new Boss Skill damage formula.
No new Boss ATK state field.
No new SignalR event.
No new API endpoint.
No new Redis key.
No PostgreSQL BattleState persistence.
```

```text
Do not change COMBAT_RULES.md §3.4.
Do not reinterpret TASK-125.
Do not reopen GAP-1.
```

**TASK-127 rework is implemented and validated.** Review Finding 1 (Major) is
resolved: the implementation is now derived directly from `COMBAT_RULES.md`
§5.5.1's canonical Boss-side formula rather than from an inferred `isDebuff`
branch. `quality/review.md` is the remaining gate.

---

## Review Evidence — Rework (PASS)

<!--
  RECORDED BY THE REVIEW AGENT per .ai/workflow/quality/review.md §1/§3 and
  .ai/agents/review.md's Output Contract. Review-only: no source, test, or
  `docs/` file was modified to produce this record. The only file written is
  this task file.
-->

```text
Review Status:        PASS
Reviewed Working Tree: TASK-127 rework present (uncommitted); reviewed directly
Reviewer:             review agent (review-only; no fixes applied)
Risk / Depth:         HIGH (TASK_TYPES.md §4) — full quality/review.md §1
                      checklist, core/validation.md §2 HIGH depth
Previous Finding 1:   RESOLVED (Major — Magnitude-sign direction inference)
```

### Contract Verified

```text
COMBAT_RULES.md §5.5.1 item 3  VERIFIED as the implemented formula:
  EffectiveBossATK = truncate( BossState.ATK × (100 + Magnitude) / 100 )
COMBAT_RULES.md §5.5.1 item 4  VERIFIED as the implemented direction semantics:
  Magnitude > 0 → increase; < 0 → decrease; = 0 → unchanged, with the
  Magnitude used WITH ITS OWN SIGN and no separate direction field/flag.
COMBAT_RULES.md §5.5.1 rounding bullet  VERIFIED: truncate toward zero, integer
  domain, no floating-point dependence (the implementation uses integer
  division, not a double multiply-then-cast).
COMBAT_RULES.md §3.4  VERIFIED as the composition
  (EffectiveBossATK + authored Skill Base Damage); NOT amended.
COMBAT_RULES.md §5.4.1 / §5.4.5  VERIFIED byte-identical and still Pet-scoped;
  the Boss-side rule is a separate convention and was NOT normalized onto it.
BOSS_RULES.md §6.2.1 / §6.3.1  VERIFIED: Rage +20%, Root 30, authored
  150/120/100 all unchanged.
GAME_STATE.md §2.3.1  VERIFIED: no member added/removed/retyped; the direction
  signal is the sign of the EXISTING Magnitude field.
```

### Implementation Verified

```text
EffectiveBossAttack  StatusEffectLifecycle.cs L759–L796.
  - Uses signed `Magnitude`: `var magnitude = (int)effect.Magnitude;` (L789) —
    no Math.Abs on the Boss path.
  - Single expression `effective * (100 + magnitude) / 100` (L791) — the
    canonical formula; the sign enters as the addend's sign.
  - NO isDebuff / isBuff / Direction / ModifierDirection / EffectDirection
    branch or identifier: an assembly-wide scan of src/ and tests/ returns
    ZERO occurrences.
  - `Math.Abs` occurs exactly once in the file, at L654, inside the untouched
    PET-side EffectiveAttack — the documented §5.4.1 |Magnitude| reading.
  - Truncates toward zero via integer division (no Math.Round/Floor/Ceiling).
  - Does not mutate BossState.ATK; no write-back, no "restore" step (§5.5.4).
  - EffectiveBossATK is not persisted: no Effective* member exists on
    BossState, BattleState, PetState, DamageInputs, or DamageResult (verified
    by reflection over the built Domain assembly). Only StatusEffectLifecycle
    declares Effective* members (the two static consumers).
  - Signature is (int, IReadOnlyList<StatusEffect>) → int: no BossState,
    BattleState, Rage, or Root identity is taken or looked up. Selection is by
    Type + TargetStat through the shared IsAtkBuffDebuff predicate — never by
    Id, source name, or hard-coded content identity.

Boss Skill Step-1  BattleStateService.cs L1389–L1417.
  - `effectiveBossAtk + bossDefinition.SkillBaseDamage` is passed as the
    `Attack:` argument of the ONE pre-existing DamagePipeline.Calculate call.
  - The authored value is passed through unchanged; no second damage path and
    no double application.
  - Verified end-to-end: +20 → 120 + 150 = 270; −30 → 70 + 150 = 220;
    0 → 100 + 150 = 250; no modifier → 100 + 150 = 250.

Basic Boss Attack  VERIFIED: receives `effectiveBossAtk` alone (no authored
  Skill term) — 120 with +20, 70 with −30, 100 unmodified.

DamagePipeline  VERIFIED behaviorally unmodified.
  - No pipeline step assignment changed; steps 1–6 are unchanged (a diff of the
    step computations returns nothing).
  - The only TASK-127 edit in the file is the §3-step-1 comment, extended to
    name §5.5.1's signed formula and to record that the derivation belongs to
    the Domain consumer. Every BossState/StatusEffect mention in the file is
    inside a comment; the pipeline remains unaware of BossState, StatusEffect,
    Rage, and Root, and takes the already-derived integer.

TASK-118 effects  VERIFIED undisturbed: Burn (50 / 2 Turns), Drain Power
  (20, floored at 0), and Root ("ATK" BuffDebuff, 30) declarations unchanged;
  their suites pass unchanged (25 tests).

Boss Passive  VERIFIED NOT implemented and NOT claimed by TASK-127 — step 18a
  applies no effect, which is out of this task's scope (§7 of the review brief).
```

### Coverage Mapping (Pet vs Boss — the critical separation)

```text
The same `Magnitude = +30` instance yields different values on each side,
which is the evidence that the two conventions were NOT collapsed:

  Boss side  (EffectiveBossAttack): +30 → 130   signed Magnitude
  Pet  side  (EffectiveAttack):     +30 →  70   |Magnitude|, §5.4.1

  Pet ATK 50, Magnitude 30 → 35   (§5.4.2's documented table, unchanged)
```

### Independent Verification (executed by the review agent)

```text
A standalone probe compiled against the built Domain assembly — not the test
suite or its fixtures — asserted every §5.5.1 / §3.4 value directly. All 34
checks passed, including: the four TASK-128 §6.8 required outcomes; the
truncation cases (51/+20 → 61; 7/−30 → 4; 13/−30 → 9; 3/+20 → 3); all four
composition values; Basic Attack in both directions; the five negative
selection guards (differently-named "ATK" applies; DEF BuffDebuff, DoT,
Shield, and State ignored); non-destructiveness of both the stored stat and
the instance collection; the Pet/Boss separation rows above; and the
reflection checks for absent Effective* state members.
```

### Test-validity review (does the suite actually catch the old defect?)

```text
The previous suite passed while the `isDebuff` defect shipped, so "the tests
are green" was not accepted as evidence. The rejected implementation was
re-introduced into a scratch copy of the source and the TASK-127 suites were
re-run against it:

  pre-rework body (|Magnitude| + buff-only reading):
    EffectiveBossAttackTests          — FAILED 10 of 31
    BossSkillStep1CompositionTests    — FAILED  3 of 17

The failure set is exactly the negative/truncation/composition cases the
rework added, so the new tests are load-bearing rather than superficially
adjusted. The scratch copy was then restored and byte-verified; the final
clean rebuild returns all 2344 tests green on the restored source.

Note recorded for accuracy: a first mutation attempt (|Magnitude| PLUS an
isDebuff branch) passed, because that form is algebraically identical to the
correct signed formula (|m| with a sign branch == signed m). It therefore
proves nothing and was discarded in favour of the true pre-rework body above.
```

### Tests Verified

```text
Executed independently by the review agent on a clean rebuild:

  dotnet build src/backend/GameServer.sln -t:Rebuild   PASS (0 errors)
  dotnet test  src/backend/GameServer.sln --no-build   PASS (2344), 0 failed
    Domain 1306 | Application 446 | Infrastructure 327 | Api 265

  Focused suites:
    EffectiveBossAttackTests            PASS (31)
    BossSkillStep1CompositionTests      PASS (17)
    BossSkillSecondaryEffect*           PASS (25)   TASK-118 intact
    RootAttackConsumption*              PASS  (8)   Pet side intact
    EffectiveAttackTests                PASS (19)   Pet side intact

  Frontend (unchanged; run from src/frontend/client to prove no regression):
    npx vitest run — 18 files, PASS (477), 0 failed

Timing artifact recorded: two intermediate full-suite runs reported failures
(Domain 10 / Application 3) because they executed while the mutation-test's
scratch body was on disk. Those results were the mutation, not the rework;
they are superseded by the clean rebuild above.
```

### Scope Verified

```text
No docs changes ................ yes — docs/ untouched by the rework and review
                                 (§5.5.1 was authored by TASK-129, separately).
No new state fields ............ yes — zero Effective* members on any state type.
No new StatusEffect members .... yes.
No new StatusEffect types ...... yes.
No new TargetStat .............. yes.
No SignalR / API / Redis / DB .. yes — zero new events, members, keys, columns.
No ADR ......................... yes — none created or modified.
No new task .................... yes.
No gameplay expansion .......... yes — no new Skill, passive, Rage application,
                                 or content.
No Pet-side rule change ........ yes — §5.4.1 byte-identical; EffectiveAttack
                                 unchanged; its 19 tests and Root's 8 pass.
No Root semantics change ....... yes — Root remains Pet-side, Magnitude 30.
No Boss Skill composition change yes — §3.4 unchanged.
No DamagePipeline redesign ..... yes — steps 1–6 unchanged; comment only.
TASK-118 secondary effects ..... yes — Burn / Drain Power / Root unchanged.
No speculative abstraction ..... yes — no manager, registry, factory, or engine.
```

### Findings

```text
NONE. No defect found against the authoritative contract.
```

The previous Major finding is closed. `core/completion.md` §1 is satisfied:
the implementation is complete, HIGH-depth validation passed, no documentation
conflict remains (§5.5.1 now owns the rule that the code implements), no scope
expansion occurred, documentation impact was checked and requires no change,
and no architectural impact exists.

### Lifecycle

```text
IN REVIEW → DONE is now permitted (quality/review.md §2's review gate passes
and core/completion.md §1 is met). This review did not relocate the task file:
per TASK_LIFECYCLE.md §4 the file stays in backlog/ until that transition is
taken, and the review agent does not move task files (the TASK-118 / TASK-119 /
TASK-129 precedent).
```

### Unrelated Issues (`AGENTS.md` §16 format — reported only, not blocking)

```text
Issue:    Hỏa Long's Rage application (step 18a Boss Passive effect) remains
          unimplemented, so no production path creates the Rage instance
          TASK-127's composition consumes.
Location: BattleStateService.cs step 18a — charges and emits PassiveCharged /
          PassiveTriggered, applies no effect.
Impact:   The 270 / 220 composition paths are unreachable in live play today;
          the Boss Skill still deals 250. TASK-127 does not own Rage
          application and is correct-by-construction against whichever
          committed BossState.StatusEffects[] a resolution receives.
Follow-up: the separate Boss Passive implementation task (ROADMAP.md Phase 1
          Passive half). Not created here.
```


