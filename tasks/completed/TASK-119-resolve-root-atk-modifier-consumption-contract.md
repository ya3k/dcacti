# TASK-119 — Resolve the Root ATK Modifier Contract (BuffDebuff `TargetStat` Consumption Point, Application Semantics, and Rounding)

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, schemas, or contracts.

  THIS TASK DECIDES NOTHING BY ITSELF AND IMPLEMENTS NO BEHAVIOR. It presents
  the documented evidence of a genuine contract gap and requires the
  appropriate human/Product-Owner decision, then records it in the canonical
  owner document(s). Choosing the application point of a BuffDebuff's
  magnitude, inventing an "effective ATK" identity, defining the percentage
  application or its rounding, or ruling on whether the modifier lands at
  Damage Pipeline step 1 or step 4 is the single prohibited action of this
  task (AGENTS.md §7, §20).

  PROVENANCE: identified while executing TASK-118. TASK-118
  (BACKLOG, BLOCKED by this gap) is instructed to implement Root's
  secondary effect per BOSS_RULES.md §6.3.1 item 3. TASK-118 §8 requires a
  STOP-and-verify on exactly three points before its ATK consumer may be
  written, and its "STOP Conditions" state: "STOP if the Root ATK reduction
  cannot be applied without changing COMBAT_RULES.md §3's six pipeline steps,
  their order (§3.1), or the Boss side's pinned step-4 value (§3.4)". The
  verification fired. This task is the smallest unit that resolves it.

  THIS IS A STOP-DRIVEN TASK, NOT A FEATURE. Per AGENTS.md §20, "Missing
  rule" and "Ambiguous requirement" are stop conditions. BOSS_RULES.md
  §6.3.1 item 3 states the magnitude (-30%) and duration (2 Turns) and hands
  the representation to "COMBAT_RULES.md §5.1 Buff/Debuff"; COMBAT_RULES.md
  §5.1 defines Buff/Debuff as "temporary stat modification (ATK/DEF/Crit/etc.)"
  and authors no consumer. GAME_STATE.md §2.3.1 item 2 states `Magnitude` is
  "typed but NOT interpreted here". No implementation task may proceed
  against it.

  BOUNDARY: documentation only. Zero files under src/ or tests/. This task
  creates no ADR unless the recorded answer requires one (reported, not
  authored — AGENTS.md §18), authors no balance value, adds no Battle Event,
  adds no SignalR method, adds no Redis key, adds no StatusEffect type, adds
  no TargetStat value, and does not fix TASK-118.
-->

---

## Metadata

```text
Task ID:           TASK-119
Type:              GAMEPLAY-CHANGE (TASK_TYPES.md §3 — "A game rule needs to
                   change" / a missing rule must be authored; §2 line 38 —
                   "not a FEATURE — it requires a GAMEPLAY-CHANGE or
                   ARCHITECTURE task". The deliverable is a recorded gameplay
                   contract decision plus its entry in the canonical owner
                   document(s). TASK_TYPES.md §2 GAMEPLAY-CHANGE line 82:
                   "A GAMEPLAY-CHANGE task must always update authoritative"
                   documentation. Workflow: documentation/documentation-change.md,
                   consistent with the TASK-116 precedent for an
                   author-the-missing-rule task. If the decision requires a
                   code, schema, or ADR change, that change is a SEPARATE
                   follow-up task — not this task's act.)
Status:            DONE
Risk:              HIGH (TASK_TYPES.md §4 — GAMEPLAY-CHANGE baseline is HIGH
                   ("Always HIGH — game rule changes are..."). HIGH also because
                   the decision governs how a Turn-based Buff/Debuff magnitude
                   reaches a stat, and is cross-referenced by BOSS_RULES.md
                   §6.3.1, COMBAT_RULES.md §3.1/§3.3/§3.4/§5.1/§5.2/§5.3,
                   GAME_STATE.md §2.3/§2.3.1/§5.1.1, and ADR-017. The decision
                   determines whether an already-authored state collection
                   (`StatusEffects[]` with Type = BuffDebuff) acquires a
                   consumer it has never had.)
Priority:          HIGH (the sole contract blocker on TASK-118's Root effect.
                   TASK-118 completes the last unimplemented half of
                   GAME_RULES.md §17 step 18b, a ROADMAP.md Phase 1 item.
                   Without this decision, Mộc Yêu's Skill remains
                   behaviorally identical to a Boss Basic Attack apart from
                   damage magnitude.)
Primary Agent:     review (TASK_TYPES.md §2 DOCUMENTATION / GAMEPLAY-CHANGE —
                   "Only the canonical owner of a concept may define it", and
                   the TASK-116 precedent: "No domain agent may author the
                   answer; the Product Owner supplies it.")
Supporting Agents: gameplay (BOSS_RULES.md §6.3.1 and COMBAT_RULES.md §5.1/
                   §5.2/§5.3 are the owning domain documents for Root and the
                   Buff/Debuff definition — consulted to CONFIRM the semantics
                   the contract must be able to express, not to author the
                   rule),
                   backend (GAME_STATE.md §2.3.1/§5.1.1 and COMBAT_RULES.md §3
                   own the state and pipeline contract — consulted to state
                   accurately what the current representation does and does not
                   carry),
                   testing (any recorded boundary must yield a Given/When/Then
                   scenario per AGENTS.md §15; REPORTED, not authored here)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   gameplay/gameplay-behavior-derivation,
                   gameplay/authority-determinism-audit,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (6 skills — Complex budget, tasks/README.md §12)
Dependencies:      TASK-118 (BACKLOG — the blocked implementation task this
                     contract unblocks. IMMUTABLE; read-only. NOT modified by
                     this task),
                   TASK-022 (DONE — implemented Boss Response step 18b: Skill
                     eligibility, Skill damage, Basic Attack fallback,
                     charge/cooldown reset, and deliberately applied no Skill
                     effect beyond damage. IMMUTABLE; read-only),
                   TASK-095 / TASK-096 (DONE — StatusEffect domain state
                     including the `TurnBased`/`TriggerBased` factories, the
                     targetStat-iff-BuffDebuff pairing, and round-trip
                     serialization. IMMUTABLE; read-only),
                   TASK-094 (DONE — the step 19a duration-consumption rule,
                     COMBAT_RULES.md §5.3 DR1–DR6, that Root's duration relies
                     on. IMMUTABLE; read-only),
                   TASK-105 (DONE — Shield refresh/depletion; the Boss→Pet
                     damage path Root attaches to. IMMUTABLE; read-only),
                   TASK-116 (IN REVIEW — the direct precedent: the analogous
                     stop-driven contract-resolution task for the NextAttack
                     Crit modifier. Its recorded finding that the state model
                     has no attack-consumption path is the decisive evidence
                     for the gap below. IMMUTABLE; read-only),
                   TASK-117 (DONE — ADR-017 and the
                     `NextAttackCritModifiers[]` state model. Its Option A
                     analysis records the missing consumption path. IMMUTABLE;
                     read-only)
Blocks:            TASK-118 (its Root effect cannot be implemented until this
                   contract is recorded). It does NOT block TASK-118's Burn or
                   Drain Power effects, which are fully determined — see
                   "Objective" and "Scope".
Estimate:          Complex (one decision set across two owner documents, one
                   pipeline-position ruling that must not disturb
                   COMBAT_RULES.md §3.1's fixed order, one rounding/
                   application ruling, and the coverage reconciliation across
                   the four documents that reference the concept)
```

**This task decided no gameplay. It recorded a decision the Product Owner
supplied, and authored the resulting rule in its canonical owner document.**

---

## Objective

Record the missing gameplay contract for how a Turn-based `BuffDebuff`
Status Effect's `Magnitude` reaches the stat its `TargetStat` names — so
that `BOSS_RULES.md` §6.3.1 item 3's Root ("-30% Pet ATK debuff for 2
Turns") becomes deterministically implementable — by (a) fixing the exact
point in the Pet's damage path where the modifier is consumed, (b) fixing
whether the reduction applies to `PetState.ATK` alone or to Damage Pipeline
step 1's sum, (c) fixing the percentage application and any rounding, and
(d) fixing how the active/expired state of the instance is determined for a
Pet attack within one grounded Turn, all without changing
`COMBAT_RULES.md` §3.1's six-step order, §3.4's pinned Boss-side step-4
value, `GAME_STATE.md` §2.3.1's item 3 duration dichotomy, item 6's
one-instance-per-identity rule, or `StatusEffect`'s existing schema.

This task does **not** author Root's magnitude or duration: those are
already `BOSS_RULES.md` §6.3.1 item 3's and are not restated here as
values to be chosen.

---

## Authoritative References

### The gap and its owners (READ ONLY — this task records, it does not redefine)

- `docs/01-game-design/BOSS_RULES.md` **§6.3.1 item 3** — the canonical
  statement of Root: "-30% Pet ATK debuff", "Percentage-based ATK reduction
  (-30% active Pet ATK)", Duration 2 Turns. It delegates the representation
  to `COMBAT_RULES.md` §5.1 and delegates the consumption timing to
  `COMBAT_RULES.md` §5.3. It names **no application site and no consumer.**
- `docs/01-game-design/BOSS_RULES.md` §6.3 (the `Secondary Effect &
  Magnitude` table), §6.1 (base stats), §6.4 (`SkillId` = `"root"`).
- `docs/01-game-design/COMBAT_RULES.md` **§5.1** — the `Buff/Debuff` entry:
  "temporary stat modification (ATK/DEF/Crit/etc.), with duration measured in
  Turns unless stated otherwise". This is the **complete** authored statement
  of what a Buff/Debuff is. It defines the category and authors **no
  consumer**.
- `docs/01-game-design/COMBAT_RULES.md` §5.2 item 2 (refresh duration, do not
  stack magnitude — the rule the Root instance's refresh uses), §5.3 DR1–DR6
  and §5.3.2 (Root is Turn-based; its one-Turn-of-duration consumption is
  governed by §5.3), §5.3.4 (no change to DoT or cooldown rules).
- `docs/01-game-design/COMBAT_RULES.md` **§3 step 4** — "× Other Modifiers
  (Relic bonuses, Passive bonuses, Buffs/Debuffs, Crit multiplier)". The
  pipeline stage that names Buffs/Debuffs among its inputs.
- `docs/01-game-design/COMBAT_RULES.md` **§3.1** — "Steps 1–6 must execute in
  this order for every damage instance" (the order the answer must not
  disturb).
- `docs/01-game-design/COMBAT_RULES.md` **§3.4** — "Step 4 — Other Modifiers
  = 1.0 (MVP: no Relic/Passive/Buff modifiers on Boss side)" (the Boss-side
  pin the answer must not change).
- `docs/01-game-design/COMBAT_RULES.md` §1.1 — `PetState` combat stats:
  `ATK` "attack power, MVP default: 50"; `Power` range 0–100; the statement
  that these are configuration and "not permanent invariants".
- `docs/01-game-design/GAME_RULES.md` **§17 step 18b** (the resolution site
  that applies the effect), step 19a (the duration-consumption point),
  step 19/18 ordering, §16 (canonical event list — no effect event exists),
  §20 (rule change policy: Detect → Report → Propose → Human Approval →
  Update Rules → Implement).
- `docs/02-technical/GAME_STATE.md` **§2.3.1 item 2** — "`Magnitude` is typed
  but not interpreted here. What the number means (flat damage, a percentage
  reduction, an absorption pool) is owned by the effect's rule document."
  The explicit hand-off this task must satisfy.
- `docs/02-technical/GAME_STATE.md` §2.3.1 item 1 (Id is an identity, not a
  definition), item 3 (the exclusive duration dichotomy), item 6 (at most one
  instance per identity), item 7 (targetStat-iff-BuffDebuff), item 8 (zero is
  never a stored state), §2.3.2 (round-trip and member set — the answer must
  add no JSON member), §5.1.1 (apply/consume/expire lifecycle).
- `docs/02-technical/GAME_STATE.md` **§2.3** — the `PetState` tree, whose
  committed line records `ATK / DEF / Crit (base + active modifiers —
  implemented in the Domain model)`. This phrase has never been given an
  application rule; whether it is the intended consumer, and what "active
  modifiers" means, is **part of the decision**, not evidence for it.
- `docs/03-decisions/ADR/ADR-017-nextattack-crit-modifiers-as-petstate-battle-state.md`
  — **decisive recorded evidence.** Its "Alternatives Considered → Option A"
  states verbatim: "Item 3 is also not the only obstacle: §2.3.1's model has
  **no attack-consumption path at all**, so the representation would still be
  missing its whole lifecycle." Its Decisions 7–11 record the analogous
  *Crit* resolution (base stat never overwritten; a composed value computed
  per pipeline execution; the `DefaultCrit` reset rejected as source-blind) —
  **the precedent the ATK answer should be reconciled with.**
- `docs/03-decisions/README.md` §8 (known documentation gaps) — to be checked
  and, if the answer requires it, updated by this task; currently it lists the
  NextAttack representation gap resolved by TASK-116/ADR-017.

### The state model the answer must reconcile with

```text
StatusEffect (GAME_STATE.md §2.3.1)   Id / Type / Source / Magnitude /
                                      TargetStat? / RemainingTurns? /
                                      ExpiryCondition?
Root instance (BOSS_RULES.md §6.3.1)  Id = "Root", Type = BuffDebuff,
                                      Source = Boss, TargetStat = "ATK",
                                      Magnitude = 30, RemainingTurns = 2
```

`StatusEffect.TurnBased(...)` already enforces the `TargetStat`-iff-
`BuffDebuff` pairing at construction, so the instance is well-formed and
serializable **today**. What does not exist is any rule that reads
`Magnitude` into a stat.

### The implementation evidence (READ ONLY — states what exists, not what is correct)

The verification below was performed against the workspace while executing
TASK-118 and is recorded here as the gap's evidence. It states what the
current code does; it does **not** state what is correct.

```text
1. The ONLY readers of StatusEffect.Magnitude in src/backend are:
     - StatusEffectLifecycle.ShieldPool   (src/backend/GameServer.Domain/
                                            Battle/StatusEffectLifecycle.cs)
       guards on Type == Shield and Id == "Shield"
     - the step 19a DoT tick loops in BattleStateService.cs, guarded on
       Type == DoT
     - BattleStateSerializer (pass-through read/write only)
   No code path reads a BuffDebuff magnitude into any stat.

2. BattleStateService.cs contains no mutation of PetState.Power and no
   occurrence of "Root" — the step 18b block resolves Skill damage and
   resets SkillCharge/SkillCooldown only.

3. The one place PetState.ATK is consumed for the Pet's own attack is the
   Player→Boss DamagePipeline.Calculate call, whose step-1 `Attack` argument
   is `resolved.PetState.ATK` and whose `BaseDamagePool` is the transient
   ATK-Gem pool from the same resolution.

4. TargetStat appears in the Domain only as a schema member and a
   construction guard (StatusEffect.cs); it is never read by a calculation.

5. No document in docs/ contains a rule for applying or rounding an ATK
   reduction. A targeted search for "effective ATK", "ATK reduction", and
   equivalent phrasing returns no authored rule.
```

### Technical and governance contracts the answer must not contradict

- `AGENTS.md` §2 (source-of-truth hierarchy and conflict precedence), §4
  (conflict resolution), §7 (game-rule protection), §9 (anti-overengineering),
  §11 (determinism/RNG), §17 (documentation change rule), §18 (architecture
  change rule), §20 (stop conditions).
- `docs/02-technical/TDD.md` §6 (single server-seeded PRNG — the answer must
  introduce no second draw or stream).
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4 (payload member sets — the answer
  must add no wire member).
- `docs/02-technical/REDIS_STATE.md` §2, §4, §7 (no new key, single
  write-back, no second representation).
- `docs/00-overview/MVP_SCOPE.md` §1 (Status Effects and Bosses are IN).

---

## Current State

`GAME_RULES.md` §17 step 18b resolves the Boss Skill fully except for its
secondary effect; TASK-118 is adding that. For **Drain Power** (instant
`PetState.Power` mutation) and **Flame Burst** (a Burn instance whose tick
path already exists at step 19a) the contracts are complete. For **Root** the
instance can already be built, stored, refreshed, expired, and serialized —
`StatusEffect.TurnBased("Root", BuffDebuff, Boss, 30, 2, "ATK")` is
well-formed by construction — but nothing applies it to anything.

`COMBAT_RULES.md` §5.1 defines Buff/Debuff and stops. `GAME_STATE.md`
§2.3.1 item 2 explicitly declines to interpret `Magnitude` and hands the
meaning to the effect's rule document — and that document never states one.
`ADR-017` records that the state model has "no attack-consumption path at
all". `COMBAT_RULES.md` §3 step 4 lists "Buffs/Debuffs" among step-4's
inputs, but §3.4 pins the **Boss** side to `1.0`, and TASK-118 §10 requires
Root to modify the **Pet's** step-1 attack value rather than the Boss's
step-4 factor — so step 4's mention is not, by itself, a usable answer.

The result is a genuine, already-recorded contract gap: an authored Status
Effect type with an authored instance identity, an authored magnitude, an
authored duration, an authored refresh rule, and an authored consumption
point — and no authored consumer.

---

## Product Owner Decisions

<!--
  ANSWERED — Product Owner. These decisions are the deliverable of this task.
  An agent authored none of them (AGENTS.md §7).
-->

**All decisions in this section were supplied by the Product Owner. The
"Resulting Contract" below is the bindable statement of them.**

**Attribution and date.** Supplied by the Product Owner as the authoritative
decision set for TASK-119. Recorded verbatim on execution of TASK-119. No
option in "Decision Inputs" was selected, ranked, or defaulted by an agent.

### D-1 — BuffDebuff ATK consumption point

**Decision: A — Damage Pipeline Step 1.**

A Turn-based `BuffDebuff` whose:

```text
TargetStat = "ATK"
```

is consumed when the Player → Boss Damage Pipeline is constructed, by modifying
the `Attack` argument supplied to Damage Pipeline Step 1.

The contract must not change the six-step order defined by:

```text
COMBAT_RULES.md §3.1
```

The implementation may use an internal helper/accessor if useful, but the
gameplay contract is that the modifier affects the Step-1 `Attack` input.

Do NOT move the modifier to Step 4.

`COMBAT_RULES.md` §3.4 remains unchanged:

```text
Boss-side Step 4 = 1.0
```

### D-2 — What the −30% applies to

**Decision: A — `PetState.ATK` only.**

The Root percentage applies only to the active Pet's ATK stat.

It does NOT reduce:

* Skill/Card base value
* ATK-Gem-generated damage pool
* the entire Step-1 sum
* Step-4 modifiers
* final damage

Therefore:

```text
EffectiveATK
    = reduced PetState.ATK

Step 1
    = EffectiveATK
    + Skill/Card base value
    + ATK-Gem-generated damage pool
```

For example:

```text
PetState.ATK = 100
ATK-Gem pool = 40
Root = -30%

EffectiveATK = 70

Step 1 base damage = 70 + 40 = 110
```

It must NOT become:

```text
(100 + 40) × 70% = 98
```

The canonical wording must remain consistent with:

```text
BOSS_RULES.md §6.3.1 item 3
```

which defines Root as:

```text
-30% active Pet ATK
```

### D-3 — Percentage application and rounding

**Decision: A — truncate toward zero.**

The reduction is calculated as:

```text
EffectiveATK =
    truncate(
        ATK × (100 - Magnitude) / 100
    )
```

For Root:

```text
Magnitude = 30

EffectiveATK =
    truncate(
        ATK × 70 / 100
    )
```

Examples:

```text
ATK 50  → 35
ATK 51  → 35
ATK 99  → 69
ATK 100 → 70
ATK 101 → 70
```

The resulting integer `EffectiveATK` is supplied to Damage Pipeline Step 1.

Do not introduce floating-point behavior where the result could vary by
representation.

Do not change the existing Step-6 final-damage rounding rule.

### D-4 — Active/expired timing

**Decision: use the existing StatusEffect duration lifecycle.**

The modifier is active according to the committed `StatusEffects[]` state at
attack resolution.

Existing ordering remains:

```text
Step 15 — Pet attack
        ↓
Step 18 — Boss response / secondary effect
        ↓
Step 19a — duration consumption
```

Therefore:

#### Root applied during Turn N

If Root is applied at Step 18b of Turn N:

```text
Turn N
  Step 15
    → Root does NOT retroactively modify this attack

  Step 18b
    → Root applied with RemainingTurns = 2

  Step 19a
    → 2 → 1
```

Then:

```text
Turn N+1
  Step 15
    → Root IS active
    → Pet ATK reduced by 30%

  Step 19a
    → 1 → 0
    → Root expires
```

Then:

```text
Turn N+2
  → Root is inactive
  → Pet ATK returns to its normal derived value
```

This follows the existing:

```text
COMBAT_RULES.md §5.3 DR2
COMBAT_RULES.md §5.3 DR5
GAME_STATE.md §2.3.1 item 8
```

Do not invent a new duration model.

### D-5 — Base stat preservation

**Decision: follow the ADR-017 non-destructive temporary-modifier precedent.**

`PetState.ATK` is never overwritten by Root.

Do NOT do:

```text
PetState.ATK = PetState.ATK * 0.7
```

and do not restore it later.

Do NOT use:

```text
PetState.ATK = DefaultATK
```

as an expiry/reset mechanism.

Instead:

```text
PetState.ATK
      ↓
temporary Root modifier
      ↓
EffectiveATK
      ↓
Damage Pipeline Step 1
```

`EffectiveATK` is derived at attack resolution and is not persisted as a second
state value.

This follows the temporary-modifier precedent established by:

```text
ADR-017
GAME_STATE.md §2.3.4
```

without modifying the `NextAttackCritModifiers[]` model.

### D-6 — Preservation Constraints (recorded, unchanged)

The decision set preserves, and this task records that it preserves:

```text
COMBAT_RULES.md §3.1 six-step order                     UNCHANGED
COMBAT_RULES.md §3.4 Boss-side Step 4 = 1.0             UNCHANGED
GAME_STATE.md §2.3.1 item 3 exclusive duration
  dichotomy (no third model)                            UNCHANGED
GAME_STATE.md §2.3.1 item 6 one-instance-per-identity   UNCHANGED
GAME_STATE.md §2.3.1 item 7 TargetStat-iff-BuffDebuff   UNCHANGED
StatusEffect member set (§2.3.2 item 3)                 UNCHANGED
GAME_RULES.md §16 canonical Battle Event list           UNCHANGED
StatusEffect Type vocabulary (§5.1 MVP list + Stun)     UNCHANGED
TargetStat value vocabulary ("ATK")                     UNCHANGED
SIGNALR_PROTOCOL.md §4 payload member sets              UNCHANGED
REDIS_STATE.md keys and single write-back                UNCHANGED
DATABASE.md columns / migrations                        UNCHANGED
TDD.md §6 / ADR-009 single RNG stream                   UNCHANGED
GAME_RULES.md §18 / ADR-001 server authority            UNCHANGED
PetState.Crit and NextAttackCritModifiers[] (ADR-017)   UNTOUCHED
```

---

## Resulting Contract (Deterministic, Implementation-Ready)

<!--
  The bindable statement of the Product Owner's decisions above. Each rule
  traces to a decision; none is authored here.
-->

### C-1. Consumer Site

A Turn-based `BuffDebuff` Status Effect instance whose `TargetStat` is `"ATK"`
is consumed at the **Player → Boss Damage Pipeline Step 1 `Attack` input**.
(D-1)

- The modifier is read when the pipeline call for the Pet's own attack is
  constructed, and it modifies the `Attack` argument that call receives.
- It does **not** become a Step-4 "Other Modifiers" factor. The Boss side's
  `Step 4 = 1.0` (`COMBAT_RULES.md` §3.4) is untouched. (D-1)
- The six-step order (`COMBAT_RULES.md` §3.1) is unchanged: this is a change to
  a Step-1 input value, not a new step, not a reordering, and not a new
  pipeline stage. (D-1)
- An internal helper or accessor that computes the Step-1 `Attack` argument is
  permitted; the **gameplay contract** is the Step-1 input, not the helper's
  name or shape. (D-1)

### C-2. Base the Percentage Applies To

The reduction applies to **`PetState.ATK` alone**. (D-2)

```text
EffectiveATK = reduced PetState.ATK

Step 1 = EffectiveATK
       + Skill/Card base value
       + ATK-Gem-generated damage pool
```

- It does **not** reduce the Skill/Card base value, the ATK-Gem-generated
  damage pool, the entire Step-1 sum, any Step-4 modifier, or final damage.
  (D-2)
- Concretely, `PetState.ATK = 100`, ATK-Gem pool `= 40`, Root `= -30%` yields
  `EffectiveATK = 70` and Step 1 base damage `= 70 + 40 = 110` — **not**
  `(100 + 40) × 70% = 98`. (D-2)
- This is consistent with `BOSS_RULES.md` §6.3.1 item 3's canonical wording,
  "-30% active Pet ATK": the object reduced is the Pet's ATK, not the Pet's
  damage. (D-2)

### C-3. Percentage Application and Rounding

```text
EffectiveATK = truncate(ATK × (100 - Magnitude) / 100)
```

For Root (`Magnitude = 30`):

```text
EffectiveATK = truncate(ATK × 70 / 100)
```

- `truncate` is toward zero, matching the integer convention
  `COMBAT_RULES.md` §3 step 6 already uses for Final Damage. (D-3)
- The **integer** `EffectiveATK` is what enters Step 1. (D-3)
- The calculation must not depend on floating-point representation: the same
  input yields the same integer on every platform and in every evaluation
  order. (D-3)
- Worked values: `ATK 50 → 35`, `51 → 35`, `99 → 69`, `100 → 70`,
  `101 → 70`. (D-3)
- **Step 6's final-damage rounding rule is unchanged.** The only rounding this
  contract authors is the one above, and it happens before the pipeline runs.
  (D-3)

### C-4. Activity and Lifetime

- The modifier is **active according to the committed `StatusEffects[]` state
  at attack resolution**. (D-4)
- It follows the **existing** Turn-based duration lifecycle; no new duration
  model is introduced. (D-4)

```text
Step 15  — Pet attack        (reads committed StatusEffects[])
Step 18  — Boss response / secondary effect (Root applied at 18b)
Step 19a — duration consumption (DR2/DR5)
```

- **Root applied at Step 18b of Turn N does not retroactively modify Turn N's
  Step 15 attack** — Step 15 already resolved before the application. (D-4)
- Turn N Step 18b applies Root with `RemainingTurns = 2`; Turn N Step 19a
  decrements `2 → 1`. (D-4)
- Turn N+1 Step 15 reads Root as active and reduces Pet ATK; Turn N+1 Step 19a
  decrements `1 → 0` and Root expires. (D-4)
- Turn N+2 Root is inactive and Pet ATK is back to its normal derived value.
  (D-4)
- This is `COMBAT_RULES.md` §5.3 DR2, §5.3 DR5, and `GAME_STATE.md` §2.3.1
  item 8 applied, not reinterpreted. (D-4, D-6)

### C-5. Base Stat Preservation

- **`PetState.ATK` is never overwritten by the modifier.**
  `PetState.ATK = PetState.ATK * 0.7` is forbidden, as is restoring it later.
  (D-5)
- **`PetState.ATK = DefaultATK` is never an expiry or reset mechanism.** (D-5)
- `EffectiveATK` is **derived at attack resolution** and is **not persisted** as
  a second state value — there is no `EffectiveATK` member in any state
  representation. (D-5)

```text
PetState.ATK
      ↓
temporary Root modifier
      ↓
EffectiveATK   (transient, per pipeline execution)
      ↓
Damage Pipeline Step 1
```

- This follows `ADR-017` and `GAME_STATE.md` §2.3.4's precedent for a
  non-destructive temporary modifier, **without** modifying the
  `NextAttackCritModifiers[]` model or extending it to ATK. (D-5)

### C-6. Preserved Constraints

All of D-6. In particular: no new Battle Event, no new `StatusEffect` Type, no
new `TargetStat` value, no new SignalR member, no new Redis key, no new database
column or migration, no second RNG stream, no client-authoritative computation,
and no second stat-modifier system.

---

## Required Authoritative Result — Coverage

```text
 1. consumption point (pipeline position)   → C-1  (Player→Boss Step 1 Attack)
 2. what the percentage applies to          → C-2  (PetState.ATK only)
 3. percentage application / rounding       → C-3  (truncate toward zero)
 4. active/expired determination            → C-4  (committed StatusEffects[],
                                                   existing §5.3 lifecycle)
 5. base stat preservation                  → C-5  (never overwritten; no
                                                   persisted EffectiveATK)
 6. preserved constraints                   → C-6  (all of D-6)
```

The contract statement required by "Required Authoritative Result" is
**SATISFIED** by C-1–C-6:

```text
A Turn-based BuffDebuff StatusEffect instance whose TargetStat is "ATK"
modifies the active Pet's ATK used by its own attack by exactly its Magnitude
percentage. The modifier is consumed at the Player→Boss Damage Pipeline
Step-1 Attack input. The reduction applies only to PetState.ATK and does not
modify the ATK-Gem-generated damage pool or other Step-1 contributions. The
resulting EffectiveATK is truncate(ATK × (100 - Magnitude) / 100). The stored
PetState.ATK is never overwritten. The modifier is active according to the
committed StatusEffects[] state at attack resolution and follows the existing
Turn-based duration lifecycle. No additional state representation, event, wire
member, Redis key, database column, StatusEffect type, TargetStat value, or RNG
stream is introduced.
```

---

## Required Documentation Changes

<!--
  Per .ai/workflow/documentation/documentation-change.md §3, each concept has
  ONE canonical owner. This task's own scope item 3 authorizes recording the
  contract in the canonical owner document(s) directly — unlike TASK-116, the
  decision requires no new battle-state concept and therefore no ADR, so the
  owning edit is not deferred to a separate task (see "Classification
  Outcome").
-->

```text
CONCEPT                                CANONICAL OWNER               ACTION
-------------------------------------  ----------------------------  ------------------------------
BuffDebuff TargetStat="ATK" magnitude  COMBAT_RULES.md §5 (new        AUTHORED by this task
  → Step-1 Attack consumption point      §5.4)
  + base + rounding + activity
Root's -30% Pet ATK "applied outside    COMBAT_RULES.md §3.4           NOT edited — see below
  the pipeline" wording
```

**`COMBAT_RULES.md` §3.4's "applied outside the pipeline" sentence — reported,
not edited.** §3.4's Boss Skill paragraph says a Boss Skill's non-damage effects
"are applied outside the pipeline". That sentence describes the **application**
of the effect (Root is applied to `StatusEffects[]`, not computed by the
pipeline) and remains accurate: the new §5.4 rule consumes the already-applied
instance at the Pet's **own** later attack, which is a different instance in a
different direction. No edit is required, and editing it would be the
duplication `documentation-change.md` §2 forbids. Recorded here so a later
reader does not read the two sentences as contradictory.

**No second owner.** `BOSS_RULES.md` §6.3.1 item 3 remains the owner of Root's
magnitude and duration and continues to delegate the representation and the
consumption timing to `COMBAT_RULES.md` §5.1/§5.3 — now also §5.4. No
`BOSS_RULES.md` edit is required and none was made.

```text
NOT required to change:
  docs/01-game-design/BOSS_RULES.md      Root's magnitude (-30%), duration (2
                                         Turns), identity ("Root"), and
                                         TargetStat ("ATK") are already
                                         authoritative in §6.3.1 item 3 and
                                         are not restated or changed.
  docs/01-game-design/GAME_RULES.md      §17's step order and §16's canonical
                                         event list are unchanged; §17 step
                                         19a already owns the consumption
                                         point this contract reuses.
  docs/02-technical/SIGNALR_PROTOCOL.md  no wire member (D-6). The Step-1
                                         `Attack` value already reaches the
                                         client as `DamageCalculated.base`
                                         (§3.2.13) — a VALUE change, not a
                                         member-set change.
  docs/02-technical/REDIS_STATE.md       no new key; no new representation
                                         (D-6).
  docs/02-technical/DATABASE.md          no column, no migration (D-6).
```

---

## Decision Inputs (RETAINED AS EVIDENCE)

<!--
  RETAINED AS EVIDENCE — the pre-decision analysis the Product Owner decided
  against or beyond. Retained per the TASK-113 / TASK-116 precedent so the
  reasoning is auditable. The "Product Owner Decisions" section above
  supersedes every open question framed here, and the option lists below were
  NOT ranked or preselected.

  A PRODUCT OWNER / HUMAN answered these. An agent must NOT answer them.
  Choosing, recommending, ranking, or defaulting any answer was the single
  prohibited action of this task (AGENTS.md §7, §20).
-->

Each question below is now answered in "Product Owner Decisions"; the evidence
is retained. The options were recorded to bound the decision, **not** to
select one.

### D-1 — Where is a `BuffDebuff`'s `TargetStat = "ATK"` magnitude consumed?

```text
(a) Damage Pipeline step 1 — the modifier reduces the Attack argument the
    Player→Boss Calculate call receives (the "effective ATK" reading),
    as TASK-118 §9/TASK-118 §10 currently require.

(b) Damage Pipeline step 4 — the modifier becomes a step-4 "Other Modifiers"
    factor, which COMBAT_RULES.md §3 step 4 already names Buffs/Debuffs
    among. This would require reconciling §3.4's Boss-side pin (the pin
    applies to the Boss's own attacks, so a Pet-side factor may be
    unaffected) and would make the modifier multiplicatively independent of
    the ATK term.

(c) A separately-named effective-ATK accessor that the step-1 caller reads,
    leaving the pipeline's arguments and steps untouched.
```

The answer must state which of these (or another) is canonical, and must not
require changing §3.1's six-step order or §3.4's pinned value.

### D-2 — What exactly does the −30% apply to?

```text
Step 1 reads THREE contributions (COMBAT_RULES.md §3 step 1): the ATK stat,
a Skill/Card base value, and the ATK-Gem-generated damage pool for the
action. For the Pet's Swap attack specifically, the caller supplies ATK and
the pool, so the candidate bases are:

(a) PetState.ATK alone
(b) PetState.ATK + BaseDamagePool (step 1's full sum)
```

`BOSS_RULES.md` §6.3.1 item 3 says "-30% active Pet **ATK**", which points at
(a) but does not exclude (b) — step 1's pool is also described as
ATK-generated. The answer must fix one.

### D-3 — Percentage application and rounding

```text
(a) The reduced ATK is truncated toward zero to an integer before entering
    the pipeline (mirroring COMBAT_RULES.md §3 step 6's truncate-toward-zero
    convention for Final Damage).
(b) The reduction is applied as an exact rational/fractional factor
    (mirroring §3 step 2's exact numerator-over-denominator multiplication,
    which exists specifically so no floating-point error enters before step 5).
(c) Some other defined rounding.
```

The answer must state the rounding, or that none is needed. The MVP default
ATK is `50`, so `50 × 0.70` (and `50 × 0.70 + pool`) are the first
observable cases.

### D-4 — How is the instance's active state determined for a Pet attack?

```text
Step 19a is the single duration-consumption point (COMBAT_RULES.md §5.3 DR2)
and it runs AFTER step 18 (GAME_RULES.md §17). A Pet attack (step 15) also
runs before step 19a within the same Turn. The answer must state:

(a) whether a Root instance applied at step 18b of Turn N reduces the Pet's
    step-15 attack of Turn N+1 (i.e. the modifier is read from the committed
    state at the start of the resolution), and
(b) whether an instance whose RemainingTurns reaches 0 at step 19a of Turn N
    is inactive for the Pet attack of Turn N+1, consistent with
    COMBAT_RULES.md §5.3 DR5 ("not active during the following Turn") and
    GAME_STATE.md §2.3.1 item 8.
```

### D-5 — Base stat identity and non-destructiveness

```text
COMBAT_RULES.md §1.1 and GAME_STATE.md §2.3 state the combat-stat defaults
are configuration and "not permanent invariants". ADR-017 Decisions 7 and 11
and GAME_STATE.md §2.3.4 items 9–10 resolved the analogous Crit question by
requiring that:

  - the base stat is NEVER overwritten by a temporary modifier;
  - the composed value is computed per pipeline execution, not stored; and
  - a reset to a configuration default is never used as a consumption or
    expiry mechanism.

The answer must state whether the ATK modifier follows this same precedent
(as TASK-118 §9's "do not create a second stat-modifier system" implies), or
whether a different, explicitly-reasoned representation is required.
```

### D-6 — What must NOT change

```text
The answer must preserve, and state that it preserves:

  - COMBAT_RULES.md §3.1's six-step order and §3.4's Boss-side step-4 value
  - GAME_STATE.md §2.3.1 item 3's exclusive duration dichotomy (no third model)
  - GAME_STATE.md §2.3.1 item 6's one-instance-per-identity rule
  - the StatusEffect member set (no new JSON member; §2.3.2 item 3)
  - the absence of any new Battle Event (GAME_RULES.md §16)
  - the absence of any new StatusEffect Type or TargetStat value
  - the single RNG stream (TDD.md §6, ADR-009)
  - the server-authoritative model (GAME_RULES.md §18, ADR-001)
  - PetState.Crit and NextAttackCritModifiers[] (ADR-017) — untouched
```

---

## Classification Outcome

**No ADR is required, and this task authored the owning edit itself.**

The task type is `GAMEPLAY-CHANGE` (`TASK_TYPES.md` §3), whose workflow
(`development/gameplay-change.md`) is "update the authoritative documentation"
— not "create a new concept". The Product Owner's decision set resolves the gap
**inside the existing model**, so none of the re-classification triggers an
ADR would answer fires:

```text
D-1  Step 1 is a VALUE the pipeline already takes as an argument. It adds no
     pipeline step, reorders nothing (§3.1 untouched), and does not make the
     Boss's step 4 read anything (§3.4's 1.0 untouched). No new concept.

D-2  No new contribution to Step 1 is authored; the rule states what is NOT
     reduced. No new concept.

D-3  A rounding rule for a Step-1 input, in the integer convention §3 step 6
     already uses. No new concept.

D-4  Explicitly reuses the existing §5.3 duration lifecycle and the existing
     step-19a consumption point. No new duration model, no new step.

D-5  Explicitly follows ADR-017's non-destructive precedent and states that
     no second representation is introduced. The modifier's only
     representation is the pre-existing StatusEffects[] instance
     (GAME_STATE.md §2.3.1 item 3/7). No new battle-state concept.
```

`AGENTS.md` §18 lists "the battle-state model" among the things requiring an
ADR. This decision **does not change the battle-state model**: it adds no
collection, no member, no type, no TargetStat value, and no lifecycle rule, and
it neither widens `GAME_STATE.md` §2.3.1 item 3's duration dichotomy nor
relaxes item 6. It supplies the consumer that `GAME_STATE.md` §2.3.1 item 2
explicitly delegated away ("`Magnitude` is typed but not interpreted here …
owned by the effect's rule document"). That is a missing **gameplay rule**, not
an architecture change, so `development/gameplay-change.md` applies and the
edit lands here rather than in a follow-up task.

This is recorded explicitly because it is the one place TASK-119 and the
TASK-116/117 precedent **diverge**: TASK-116's answer introduced
`NextAttackCritModifiers[]` and therefore required ADR-017 plus a separate
ARCHITECTURE task. TASK-119's answer introduces nothing, so no ADR is
authored, and `docs/03-decisions/README.md` §7's index is unchanged (still
ending at ADR-017) — see the §8 note added there.

No ADR is silently authored. If the Product Owner later decides that the
`BuffDebuff` consumption rule should generalize beyond `"ATK"` — e.g. a
DEF/Crit case whose application site is genuinely different — that
generalization would be a separate decision and, if it changes the model,
would carry its own ADR. This task authors the `"ATK"` case only, and §5.4.5
states that a `BuffDebuff` naming another stat is not silently treated as an
ATK modifier.

---

## Scope

### In Scope

1. The complete decision set D-1–D-5, answered by the Product Owner and
   recorded verbatim in this task.
2. The resulting deterministic, implementation-ready contract (the "C-"
   items), sufficient that a single consumer can be written with no further
   interpretation.
3. Recording the contract in its **canonical owner document(s)** with no
   duplication (`documentation/documentation-change.md` §2): the gameplay
   rule belongs to `COMBAT_RULES.md` §5 (most likely a new item under §5.1
   or §5.2, or a new §5.4), and any state-mutation consequence belongs to
   `GAME_STATE.md`.
4. A coverage reconciliation so no document restates the rule it does not
   own, and so `REFERENCE`-style delegation stays accurate.
5. Checking `docs/03-decisions/README.md` §8 and recording whether the gap
   was listed; an ADR is **reported** as required only if `AGENTS.md` §18
   says so (e.g. if the answer changes the battle-state model).
6. A `TASK-118` handoff stating the exact consumer site, inputs, and
   expectations the implementation task must satisfy.

### Out of Scope

- **Implementing Root.** TASK-118's act, not this one. Zero files under
  `src/` or `tests/`.
- **Authoring Root's magnitude, duration, or the `"ATK"` `TargetStat`.** All
  are `BOSS_RULES.md` §6.3.1 item 3's and `GAME_STATE.md` §2.3.1's.
- **Changing `COMBAT_RULES.md` §3.1's order or §3.4's Boss-side pin.**
  TASK-118 §10 and its stop conditions forbid it; this task may not authorize
  it.
- **Any new Battle Event, SignalR method/member, Redis key, PostgreSQL
  column, StatusEffect Type, or TargetStat value.**
- **Boss Passive effects (step 18a)** — Hỏa Long's Rage, Thủy Ma's healing
  reduction, Mộc Yêu's regeneration. Not fully determined by current
  documentation and requiring their own decision task.
- **Relic trigger evaluation (step 11)** — `ROADMAP.md` Phase 2.
- **A general "stat modifier framework".** `AGENTS.md` §9 forbids it. If the
  decision generalizes beyond ATK it must be explicitly authored as such.
- **TASK-118's Burn and Drain Power effects.** Fully determined; not blocked
  by this gap and not this task's concern.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Affected Files & Areas

```text
[x] docs/01-game-design/COMBAT_RULES.md   (AUTHORED — §5.4 added as the
                                           canonical owner of the BuffDebuff
                                           consumer rule; §5.1's Buff/Debuff
                                           entry now points at it. §3, §3.1,
                                           §3.4, §5.1's existing text, §5.2,
                                           and §5.3 are unchanged.)
[x] docs/02-technical/GAME_STATE.md       (CLARIFIED — §2.3.1 item 2's
                                           `Magnitude` hand-off now names
                                           §5.4; §2.3.1 item 12 records the
                                           state-mutation consequence (derived,
                                           not stored; stat not overwritten);
                                           §2.3's `ATK/DEF/Crit` tree note
                                           records the same. NO member,
                                           type, or lifecycle semantic
                                           changed.)
[x] docs/03-decisions/README.md           (§8 checked and recorded; no ADR)
[x] tasks/backlog/TASK-119-*.md           (this file — decisions + contract +
                                           handoff + completion evidence)
[ ] src/                                  (NONE)
[ ] tests/                                (NONE)
```

---

## Implementation Notes

- The precedent to reconcile with is TASK-116 / TASK-117 / `ADR-017`: the
  same representation question was asked for Crit, and the answer composed
  the value per pipeline execution without overwriting the base stat. Whether
  ATK follows that shape is D-5's question — reconcile, do not assume.
- `COMBAT_RULES.md` §3 step 4 does name "Buffs/Debuffs". That mention is the
  strongest existing hook for D-1(b). It is **not** self-executing: §3.4 pins
  the Boss side to `1.0`, and TASK-118 §10 requires the Pet's step-1 attack
  value to be the thing reduced. The decision must resolve the tension
  explicitly rather than leaving it to an implementer.
- `COMBAT_RULES.md` §5.3.1 (DR6) already establishes that every documented
  Buff/Debuff application site precedes step 19a, which is what makes D-4's
  first question answerable in principle. Quote it rather than re-deriving it.
- `StatusEffectLifecycle` is the only type that writes `StatusEffects[]`.
  Whatever D-1 selects, the consumer must not become a second apply/refresh
  path; TASK-118 §5/§7 require reuse of `Apply`.

---

## Acceptance Criteria

- [x] All of D-1–D-5 are answered, and the answers are recorded verbatim
      with their date/attribution.
- [x] The resulting contract is deterministic and implementation-ready: a
      single consumer can be written from it with no further interpretation
      (application point, base, rounding, and activity determination all
      fixed). → C-1–C-5
- [x] The contract names its canonical owner document and section, and adds
      no duplicate statement of the rule elsewhere.
      → `COMBAT_RULES.md` §5.4
- [x] The contract explicitly preserves every item in D-6. → C-6
- [x] `COMBAT_RULES.md` §3.1's six-step order and §3.4's Boss-side value are
      unchanged. → VERIFIED (byte-identical)
- [x] `GAME_STATE.md` §2.3.1's items 3, 6, and 7 are unchanged; the
      `StatusEffect` member set is unchanged. → VERIFIED (byte-identical)
- [x] No new Battle Event, StatusEffect Type, TargetStat value, SignalR
      member, Redis key, or database column is introduced. → VERIFIED
- [x] `docs/03-decisions/README.md` §8 was checked and its state recorded.
      → not previously listed; recorded as checked-and-closed
- [x] Any required ADR is **reported** as a separate follow-up, not authored
      here (`AGENTS.md` §18). → **No ADR required**; see "Classification
      Outcome"
- [x] A TASK-118 handoff records the consumer site, its inputs, and its
      expected behavior. → see "TASK-118 Handoff"
- [x] Zero files under `src/` or `tests/` changed. → VERIFIED
- [x] Quality review checklist passes (`quality/review.md` §1). → **PASS** on
      every documentation-applicable item (Correctness, Scope, Documentation,
      Maintainability, Determinism); code-only items (Tests, Security,
      Performance) are N/A for a documentation-only task per
      `documentation-change.md` §4. Re-verified independently — see
      "Independent Review Verification".
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).
      → VERIFIED

**Lifecycle note.** Per `TASK_LIFECYCLE.md` §4, `IN REVIEW → DONE` is the
transition that moves `active/` → `completed/`, and `READY → IN PROGRESS` is
the one that would have moved `backlog/` → `active/`. This file has remained in
`tasks/backlog/` throughout, matching the TASK-113 / TASK-116 / TASK-117
precedent for a decision-capture task. The executing agent does not move files
between lifecycle directories manually; landing in `completed/` is the
completion transition and is left to the workflow that performs it.

---

## Testing Requirements

### Required Verification

```text
[x] Documentation consistency — no restatement of the rule in a
                               non-owner document
                               (quality/documentation-consistency)
                               PASS — §5.4 states the rule once;
                               GAME_STATE.md §2.3.1 item 2/item 12 and §2.3
                               POINT at it and state only the state-mutation
                               consequence; BOSS_RULES.md §6.3.1 item 3
                               already delegates and is unchanged;
                               COMBAT_RULES.md §3.4's "applied outside the
                               pipeline" sentence is about application, not
                               consumption, and required no edit.
[x] Coverage       — every referenced section still resolves, and the
                     contract is reachable from BOSS_RULES.md §6.3.1
                     item 3 and GAME_STATE.md §2.3.1 item 2
                     PASS — §5.1's Buff/Debuff entry → §5.4;
                     GAME_STATE.md §2.3.1 item 2 → §5.4;
                     BOSS_RULES.md §6.3.1 item 3 → COMBAT_RULES.md §5.1/§5.3
                     (→ §5.4); §5.4 → §3 step 1, §5.3, §3.3 item 10,
                     GAME_STATE.md §2.3.1/§2.3.4. No dangling reference; no
                     changed section number.
[x] Gameplay scenarios — REPORTED as Given/When/Then for TASK-118 to
                     implement (see "TASK-118 Handoff")
[x] Scope          — no new event, wire member, Redis key, schema column,
                     StatusEffect type, or TargetStat value
                     PASS (quality/scope-validation)
```

### Key Edge Cases

- **Rounding** — `PetState.ATK = 50` with `−30%` (exactly `35`), and a value
  that does not divide evenly.
- **Root at the exact boundary** — an instance at `RemainingTurns = 1` at the
  step 19a of the Turn whose Pet attack it modifies.
- **Expiry Turn** — the resolution in which Root reaches 0 at step 19a; the
  following Turn's Pet attack must use the unreduced value
  (`COMBAT_RULES.md` §5.3 DR5, `GAME_STATE.md` §2.3.1 item 8).
- **Reapplication** — Root refreshed before expiry keeps one instance at
  `RemainingTurns = 2` (`COMBAT_RULES.md` §5.3.3).
- **Non-`ATK` `TargetStat`** — a `BuffDebuff` naming any other stat must not
  modify ATK.
- **Boss side untouched** — the Boss's own attack resolves with step 4 at
  `1.0` (`COMBAT_RULES.md` §3.4).

---

## Stop Conditions

<!--
  Universal stop conditions in AGENTS.md §20 and .ai/README.md §13 always apply.
-->

- **STOP** if the decision set requires changing `COMBAT_RULES.md` §3.1's
  six-step order or §3.4's Boss-side value. The task must be re-scoped and
  the change authorized explicitly, not absorbed here.
- **STOP** if the answer requires a new Battle Event, StatusEffect Type,
  TargetStat value, SignalR member, Redis key, or database column.
- **STOP** if the answer requires a second RNG draw or stream
  (`AGENTS.md` §11, `ADR-009`).
- **STOP** if the answer contradicts an `Accepted` ADR — including ADR-017's
  recorded boundary that `StatusEffects[]` and `NextAttackCritModifiers[]`
  do not overlap (`.ai/workflow/architecture/architecture-change.md` §3).
- **STOP** if the answer would overwrite `PetState.ATK` as stored state
  without an explicit decision authorizing that departure from the ADR-017
  precedent.
- **STOP** if this task's scope drifts into step 18a (Boss Passive effects)
  or step 11 (Relic triggers).
- **STOP** if the work would exceed the 6 listed skills or cross an uncoupled
  architectural boundary — decompose instead (`tasks/README.md` §13).

---

## Required Authoritative Result

The output of this task is a recorded contract — now authored at its canonical
owner, `COMBAT_RULES.md` §5.4 — that makes the following statement true and
traceable:

```text
A Turn-based BuffDebuff StatusEffect instance whose TargetStat is "ATK"
modifies the active Pet's ATK used by its own attack by exactly its
Magnitude percent, at the Player→Boss Damage Pipeline Step-1 Attack input,
with the base PetState.ATK alone and truncate-toward-zero rounding, for as
long as the instance is active as determined by the committed
StatusEffects[] state at attack resolution under the existing §5.3 lifecycle;
the stored base stat is never overwritten, and no new state, event, wire
member, Redis key, schema column, status type, target stat, or RNG stream is
introduced.
```

---

## TASK-118 Handoff

```text
Consumer site:
Player → Boss DamagePipeline Step 1 Attack input

Inputs:
PetState.ATK + active TargetStat="ATK" BuffDebuff Magnitude

Base:
PetState.ATK only

Rounding:
truncate toward zero

Activity:
committed StatusEffects[] state at attack resolution;
existing Step 19a duration lifecycle

Must preserve:
COMBAT_RULES §3.1
COMBAT_RULES §3.4
GAME_STATE §2.3.1 items 3/6/7
StatusEffect member set
single RNG stream
ADR-017 collections

Must not:
overwrite PetState.ATK
persist EffectiveATK
modify ATK-Gem pool
create a second stat-modifier system
add events/wire members/Redis keys/schema columns
```

### Consumer-site detail for the implementer

- The site is the existing Player → Boss `DamagePipeline.Calculate` call in
  `BattleStateService.cs`'s step-15 block, whose step-1 `Attack` argument is
  `resolved.PetState.ATK` today. The change is to that **argument's value**,
  not to the pipeline, its six steps, or their order (`COMBAT_RULES.md` §3.1).
- `BaseDamagePool` (the transient ATK-Gem pool from the same resolution) is
  **passed through unchanged**. The reduction is applied to the ATK term
  **before** the two are summed, so a `PetState.ATK = 100` + pool `= 40` case
  yields Step 1 `= 110`, never `98`.
- The modifier's **only** representation is the pre-existing `StatusEffects[]`
  instance. No `EffectiveATK` field is added to `PetState`, `BattleState`, the
  serializer, or any DTO — the value is computed locally for the one pipeline
  call and discarded.
- Reuse the existing `StatusEffectLifecycle` apply/refresh path; the consumer
  must **not** become a second apply/refresh path (TASK-118 §5/§7).
- The consumer must read `TargetStat` explicitly. A `BuffDebuff` whose
  `TargetStat` is not `"ATK"` modifies no ATK. The consumer must **not**
  dispatch on `StatusEffect.Id` (TASK-118 §9) — `Root` is the MVP instance of
  the *rule*, not the rule's identity.
- If several active `TargetStat = "ATK"` instances exist, each contributes its
  own percentage; `GAME_STATE.md` §2.3.1 item 6 bounds the set to one instance
  per `Id` per entity. Composing several simultaneously is not exercised by MVP
  content, and the implementer must not invent a stacking rule beyond
  applying each active instance's magnitude — if MVP content cannot produce
  that state, do not build for it (`AGENTS.md` §9).
- The Boss's own damage is untouched: `COMBAT_RULES.md` §3.4's
  `Step 4 — Other Modifiers = 1.0` and the Boss's Step-1 `Base Damage =
  Boss.ATK` are unchanged, and this rule authors no Boss-side factor.

### Gameplay scenarios to implement (REPORTED, not authored here)

```text
Given  the active Pet has PetState.ATK = 50
And    an active Turn-based BuffDebuff instance
         (Id "Root", TargetStat "ATK", Magnitude 30, RemainingTurns >= 1)
When   the Pet's own attack resolves at GAME_RULES.md §17 step 15
Then   the Damage Pipeline Step 1 Attack input is 35
And    the DamagePipeline's BaseDamagePool contribution is unchanged
And    the committed PetState.ATK is still 50

Given  the active Pet has PetState.ATK = 100
And    the same resolution's ATK-Gem-generated pool is 40
And    an active Root instance (Magnitude 30)
When   the Pet's own attack resolves
Then   Step 1 Base Damage is 110 (70 + 40)
And    it is NOT 98

Given  no BuffDebuff instance names TargetStat "ATK"
When   the Pet's own attack resolves
Then   the Step 1 Attack input is the unreduced PetState.ATK
And    PetState.ATK is unchanged

Given  a Root instance is applied at step 18b of Turn N
When   Turn N's step 15 attack (already resolved) is examined
Then   it was unaffected by Root
And    at Turn N's step 19a RemainingTurns becomes 1
And    at Turn N+1's step 15 the Pet's Step-1 Attack input is reduced
And    at Turn N+1's step 19a RemainingTurns becomes 0 and the instance is
       removed
And    at Turn N+2's step 15 the Step-1 Attack input is unreduced again
And    PetState.ATK was never overwritten nor reset to a configuration default

Given  an active BuffDebuff instance whose TargetStat is any value other
         than "ATK"
When   the Pet's own attack resolves
Then   the Step 1 Attack input is unmodified

Given  the active Pet is attacking (Player → Boss)
When   the damage instance is constructed
Then   COMBAT_RULES.md §3.1's six-step order is unchanged
And    the Boss side's Step 4 remains 1.0 (COMBAT_RULES.md §3.4)
And    no second RNG draw occurs (TDD.md §6, ADR-009)
```

---

## Completion Evidence

### Product Owner Decisions

Recorded verbatim in "Product Owner Decisions" above. Summary:

- **D-1** — consumption point: **A**, the Player → Boss Damage Pipeline **Step 1
  `Attack` input**. Not Step 4; `§3.1`'s order and `§3.4`'s Boss-side `1.0`
  unchanged.
- **D-2** — the percentage applies to **`PetState.ATK` alone**. Not the Skill/
  Card base value, not the ATK-Gem pool, not the Step-1 sum, not Step 4, not
  final damage. `100 + 40` pool at `-30%` → `110`, not `98`.
- **D-3** — **truncate toward zero**:
  `EffectiveATK = truncate(ATK × (100 − Magnitude) / 100)`; integer result
  enters Step 1; no floating-point dependence; Step 6's final-damage rounding
  unchanged. `50→35, 51→35, 99→69, 100→70, 101→70`.
- **D-4** — **existing duration lifecycle**; active per the committed
  `StatusEffects[]` state at attack resolution; step 15 → step 18 → step 19a
  ordering preserved. Root applied at Turn N step 18b affects Turn N+1's
  step 15, expires at Turn N+1's step 19a, and is gone by Turn N+2.
- **D-5** — **ADR-017 non-destructive precedent**: `PetState.ATK` never
  overwritten; no `PetState.ATK = DefaultATK` reset; `EffectiveATK` derived at
  attack resolution and never persisted; `NextAttackCritModifiers[]` untouched.
- **D-6** — all preservation constraints recorded and verified.

### Changed Files

Documentation only — zero files under `src/` or `tests/`, and
`tasks/backlog/TASK-118-implement-boss-skill-secondary-effects.md` is
**byte-identical** (`git diff` empty for it).

- `docs/01-game-design/COMBAT_RULES.md` — **version 1.7 → 1.8**, header
  updated. **§5.4 added** ("Stat Modifiers (`BuffDebuff` Consumption)") as the
  canonical owner of the rule: §5.4.1 the `TargetStat = "ATK"` consumption
  rule (consumption point, base, worked example), §5.4.2 rounding, §5.4.3
  activity and duration with the Turn-N/N+1/N+2 ordering, §5.4.4
  non-destructiveness, §5.4.5 scope and boundaries. §5.1's Buff/Debuff entry
  gained a pointer to §5.4. **§3, §3.1, §3.4, §5.1's existing text, §5.2, and
  §5.3 are byte-identical** — the six-step order and the Boss-side `1.0` are
  unchanged.
- `docs/02-technical/GAME_STATE.md` — **version 2.11 → 2.12**, header updated.
  §2.3.1 item 2's `Magnitude` hand-off now names `COMBAT_RULES.md` §5.4 (it
  previously named no owner, which was the delegation the gap consisted of);
  §2.3.1 item 12 records the state-mutation consequence (the rule derives an
  effective value, stores nothing, and overwrites no stat); §2.3's
  `ATK / DEF / Crit` tree note records the same. **No member was added,
  renamed, removed, or retyped; §2.3.1's tree, §2.3.2's serialized member set,
  and items 3/6/7 are byte-identical in meaning; §2.3.4 and §5.1.2 are
  untouched.**
- `docs/03-decisions/README.md` — **version 1.8 → 1.9**, header updated. §8:
  recorded that the `BuffDebuff` `TargetStat = "ATK"` consumption gap was
  checked, was **not** previously listed, is **not** an ADR-level open item,
  and is closed by `COMBAT_RULES.md` §5.4. §7's ADR index is unchanged (still
  ends at ADR-017) — **no ADR was added or edited.**
- `tasks/backlog/TASK-119-resolve-root-atk-modifier-consumption-contract.md` —
  this file: decisions recorded verbatim, "Resulting Contract" (C-1–C-6) and
  coverage map added, "Required Documentation Changes" register added,
  "Classification Outcome" added (no ADR), the pre-decision analysis retained
  as evidence, handoff completed, acceptance criteria and verification
  results recorded.

**Documents deliberately NOT changed:** `BOSS_RULES.md` (Root's magnitude,
duration, identity, and `TargetStat` are already authoritative in §6.3.1
item 3 and remain the owner), `GAME_RULES.md` (§17's step order and §16's event
list unchanged), `SIGNALR_PROTOCOL.md`, `REDIS_STATE.md`, `DATABASE.md`,
`TDD.md`, `MVP_SCOPE.md`, `ROADMAP.md`, and every ADR.

### Validation Results

```text
[PASS] COMBAT_RULES.md §3.1 six-step order          — byte-identical
[PASS] COMBAT_RULES.md §3.4 Boss-side Step 4 = 1.0  — byte-identical
[PASS] GAME_STATE.md §2.3.1 items 3 / 6 / 7         — byte-identical
[PASS] StatusEffect member set (§2.3.1 tree,
       §2.3.2 serialized shape)                     — byte-identical
[PASS] NextAttackCritModifiers[] (§2.3.4, §5.1.2)   — byte-identical
[PASS] No new StatusEffect Type                     — 4 members unchanged
[PASS] No new TargetStat value                      — "ATK" only
[PASS] No new Battle Event (GAME_RULES.md §16)      — unchanged
[PASS] No new SignalR member                        — §4 member sets unchanged
[PASS] No new Redis key                             — REDIS_STATE.md unchanged
[PASS] No database column or migration              — DATABASE.md unchanged
[PASS] Single RNG stream                            — TDD.md §6, ADR-009; the
                                                      rule draws no RNG at all
[PASS] Documentation consistency                    — one owner (§5.4); no
                                                      duplicate rule introduced
[PASS] Coverage                                     — §5.1 → §5.4,
                                                      BOSS_RULES §6.3.1 → §5.1,
                                                      GAME_STATE §2.3.1 item 2
                                                      → §5.4 all resolve
[PASS] Scope validation                             — MVP_SCOPE.md §1 Combat /
                                                      Bosses / Status Effects;
                                                      no OUT item engaged
[PASS] Zero files under src/ changed
[PASS] Zero files under tests/ changed
[PASS] TASK-118 byte-identical (not modified)
```

### Independent Review Verification

Re-verified against the current working tree by the Review Agent, using diff
inspection rather than assertion. Every check below was re-run independently of
the authoring pass; no finding required rework.

```text
Protected-section isolation (hunk-overlap, not eyeball)

  Every modified NEW-side line range in COMBAT_RULES.md was computed from the
  diff and intersected with each protected section's line span:

    3.1 Order Is Fixed          lines 194-200   overlap: NO
    3.4 Boss Damage             lines 384-410   overlap: NO
    5.2 Status Rules            lines 492-508   overlap: NO
    5.3 DR1-DR5                 lines 509-537   overlap: NO
    5.3.1-5.3.4                 lines 538-598   overlap: NO

  Result: the four protected COMBAT_RULES sections are outside every diff
  hunk — byte-identical, not merely "unchanged in meaning".

GAME_STATE.md protected regions

  2.3.1 spans lines 1136-1251. Two hunks fall inside it (new-side 1171-1175
  and 1230-1238). Inspected line-by-line: both are pure APPENDED clarifying
  sentences to item 2 and item 12. The pre-existing wording is preserved
  verbatim; no member, type, enum value, or rule statement was edited.

  Member set re-counted from the files, not from prose:
    StatusEffect serialized members (§2.3.2)     7  (id, type, source,
                                                    magnitude, targetStat,
                                                    remainingTurns,
                                                    expiryCondition)
    StatusEffectType members (source)            4  (DoT, BuffDebuff, Shield,
                                                    State)
    "EffectiveATK"/"AtkModifier" in GAME_STATE   0
    PetState members added by this task          0

ADR check

  docs/03-decisions/ADR/ ends at ADR-017. No ADR-018 file exists. The resolved
  decision introduces no new battle-state concept, collection, member, or
  persistence/transport concept, so AGENTS.md §18 is not engaged. Confirmed
  the divergence from TASK-116/ADR-017 is justified and not assumed.

TASK-118 handoff completeness

  All required fields present with concrete values (consumer site, inputs,
  base, rounding, activity, duration, Must preserve, Must not). The Turn-N
  step-18b → Turn-N+1 step-15 → Turn-N+1 step-19a → Turn-N+2 inactive ordering
  is present in the handoff, in the contract (C-4), and in the canonical rule
  (COMBAT_RULES.md §5.4.3).

Task boundary

  Type remains GAMEPLAY-CHANGE; the deliverable is a recorded decision plus its
  canonical-owner entry. No code, no tests, no task created.
  File location: tasks/backlog/ with Status IN REVIEW — matching the
  TASK_LIFECYCLE.md §4 transition table (IN REVIEW → DONE moves active/ →
  completed/) and the TASK-116 / TASK-117 / TASK-113 precedent. The file is
  deliberately NOT moved here: TASK_LIFECYCLE.md §4 gives the move to the
  completion transition, and the executing agent is not to relocate files
  manually.

Files modified by this task (filesystem-verified, not asserted)

  docs/01-game-design/COMBAT_RULES.md        (version 1.7 → 1.8; §5.4 added;
                                              §5.1 pointer added)
  docs/02-technical/GAME_STATE.md            (version 2.11 → 2.12; §2.3.1
                                              items 2 and 12 appended;
                                              §2.3 tree note)
  docs/03-decisions/README.md                (version 1.8 → 1.9; §8 closed)
  tasks/backlog/TASK-119-...md               (this file)

  Exactly four files carry a modification time from this task. Zero files
  under src/ or tests/. TASK-118's own modification time is unchanged, and its
  content is byte-identical.
```

### Documentation Impact

- Canonical owner: **`docs/01-game-design/COMBAT_RULES.md` §5.4** (gameplay
  rule). State-mutation consequence:
  **`docs/02-technical/GAME_STATE.md` §2.3.1 items 2 and 12** (pointer and
  consequence only — no member, type, or lifecycle change).
- ADR required: **NO.** The decision introduces no new battle-state concept —
  the modifier's only representation is the pre-existing `StatusEffects[]`
  instance, no member is added, `§2.3.1` item 3's duration dichotomy is not
  widened and item 6 is not relaxed — so `AGENTS.md` §18 is not engaged and the
  owning edit is not deferred to a separate task. Contrast TASK-116 → TASK-117
  → ADR-017, where the answer *did* introduce
  `PetState.NextAttackCritModifiers[]`. See "Classification Outcome".
- Follow-up tasks required: **none** for this contract. `docs/03-decisions/
  README.md` §7's index is unchanged and no new task was created.
