# TASK-128 — Resolve the Boss-Side `BuffDebuff` ATK Modifier Direction Contract

<!--
  GEN-TASK EXECUTION MANIFEST — PRODUCT-OWNER DECISION-INPUT TASK
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  THIS TASK DECIDES NOTHING AND IMPLEMENTS NOTHING. Its only purpose is to
  present one pre-existing, evidence-backed gameplay ambiguity and obtain the
  Product Owner's explicit decision, then record it and stop.

  AN AGENT MUST NOT ANSWER §6. If no Product Owner answer is present, the
  agent reports the task as awaiting input and stops. Choosing, recommending,
  ranking, defaulting, or inferring an answer is the single prohibited action
  of this task.

  PROVENANCE: surfaced by the TASK-127 review (CHANGES REQUIRED, one Major
  finding). TASK-127 §2's implementation of EffectiveBossATK branches on the
  SIGN of Magnitude (`Magnitude < 0` ⇒ reduce, else increase). The review
  established that the authoritative documents do not assign Magnitude sign
  semantics, and that the existing content contradicts the branch: Rage is a
  BUFF stored as the POSITIVE +20, while Root is a DEBUFF stored as the
  POSITIVE 30. TASK-127 correctly STOPPED per its own §8 stop condition 7 ("a
  required value is unavailable … that is a missing rule, not a value to
  choose") and made no source change. This task is the missing decision-input
  artifact that unblocks it.

  NOT CREATED BY TASK-123/TASK-124: this gap is NOT GAP-1 (Boss Skill Step-1
  composition — resolved by TASK-125/TASK-126) and is not GAP-2…GAP-5. It was
  surfaced later, by the TASK-127 review.

  BOUNDARY: this task writes only this task file. It edits no docs/ file, no
  ADR, no src/ file, and no tests/ file. It authors no formula, no value, and
  no rule.
-->

---

## Metadata

```text
Task ID:           TASK-128
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md, which governs
                   recording discipline: §2 no duplication, §3 canonical owner,
                   §4 report through quality/review.md + core/completion.md).
                   The deliverable is a RECORDED Product-Owner decision
                   artifact. See "Type classification note". NOT
                   GAMEPLAY-CHANGE: this task changes no rule and touches no
                   implementation — it records a decision that SEPARATE later
                   tasks apply.
Status:            BACKLOG (awaiting Product Owner input — §6 is the input this
                   task exists to collect. The unfilled `Decision:` field is
                   this task's NORMAL STARTING STATE, not a failure and not a
                   blocker; no lifecycle transition applies. Follows the
                   TASK-104 / TASK-116 / TASK-119 decision-input precedent.)
Risk:              LOW as an act (this task writes no docs/ file, authors no
                   rule, and touches no source). MEDIUM as a consequence — the
                   decision determines the Boss-side ATK modifier formula and
                   may require a GAME_STATE.md §2.3.1 representation change.
                   TASK_TYPES.md §4's DOCUMENTATION baseline is LOW–MEDIUM.
Priority:          HIGH (the sole unblocking input for TASK-127, which is
                   otherwise complete and is the Boss Skill damage half of
                   ROADMAP.md Phase 1's "Boss Response (Passive → Skill →
                   Attack → Victory/Defeat)".)
Primary Agent:     orchestrator (task-lifecycle / requester coordination — this
                   task records requester input. TASK_TYPES.md §2 DOCUMENTATION
                   names the Review Agent, and no domain agent may author this
                   answer. The orchestrator's own contract permits "Task
                   classification artifacts only" and forbids it to "make game
                   design decisions" (.ai/agents/orchestrator.md §Scope /
                   §Decision Authority), which is exactly this task's posture.)
Supporting Agents: N/A (no domain agent may supply or review the DECISION.
                   Review is limited to confirming that the decision slot
                   exists, that no option is pre-judged or ranked, and that any
                   supplied answer is recorded verbatim.)
Workflow:          documentation/documentation-change.md
                   (no docs/ file is edited by this task. The workflow governs
                   recording discipline — §2 no duplication, §3 canonical owner
                   — and §4 routes the final report through quality/review.md +
                   core/completion.md.)
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-127 (BLOCKED — the implementation task this decision
                     unblocks. IMMUTABLE; READ-ONLY; must NOT be modified,
                     re-statused, moved, or rewritten. Its §Rework Session
                     section records the blocker and evidence this task
                     formalizes),
                   TASK-125 (DECISION RECORDED — the Boss Skill Step-1
                     composition decision (Option B), whose 120 outcome this
                     decision MUST preserve and NOT reopen. IMMUTABLE;
                     read-only),
                   TASK-126 (DONE — authored that composition at
                     COMBAT_RULES.md §3.4. IMMUTABLE; read-only),
                   TASK-119 (IN REVIEW / REVIEW COMPLETE — authored the
                     PET-side §5.4 contract, incl. §5.4.1 item 3's
                     truncate(ATK × (100 − |Magnitude|) / 100). Its Pet-side
                     semantics must NOT be replaced or modified by this
                     decision. IMMUTABLE; read-only),
                   TASK-123 (BLOCKED — D-1a/D-1-representation recorded Rage's
                     representation and consumption. Historical context;
                     IMMUTABLE; read-only; its decisions are NOT reopened)
Blocks:            TASK-127 (BLOCKED), and through it the Boss Skill damage
                   half of ROADMAP.md Phase 1.
Estimate:          Simple (4 skills; no code, no tests, no document edits; the
                   answer is this task's INPUT, not its output)
```

**Type classification note.** `DOCUMENTATION`, not `GAMEPLAY-CHANGE` and not
`ARCHITECTURE`. `TASK_TYPES.md` §2 types a `GAMEPLAY-CHANGE` as a rule change
"propagat[ed] … through implementation and tests"; this task changes no rule and
touches no implementation — it only records a requester decision into a task
file. `TASK_TYPES.md` §3 selects the type by "*what the deliverable is*"; here
the deliverable is a recorded decision artifact and the only file written is a
`tasks/` file. This follows the existing decision-input precedent: TASK-061,
TASK-094, TASK-104, and TASK-125.

**This task is NOT a substitute for the applying task, and it is not that
task.** Transcribing the recorded answer into its canonical owner documents
(`COMBAT_RULES.md` §5.5.1 — the Boss ATK modifier rule — and, if the chosen
representation requires it, `GAME_STATE.md` §2.3.1) is a SEPARATE, subsequently
created documentation-resolution task. **Do not bypass it.**

**This task creates no ADR.** The question is a combat-rule/representation
question inside the existing Status Effect model. It introduces no persistence
change, no realtime strategy change, and no module-boundary change. It is not
listed in `docs/03-decisions/README.md` §8 and this task does not add it there.
If the later applying task finds an ADR is genuinely required, that is reported
there, not authored here.

**This task authors no formula and no value.** The only numbers appearing below
are the ones the authoritative documents already carry — `BossState.ATK = 100`
(`COMBAT_RULES.md` §1.1's MVP default), `Rage = +20%`
(`BOSS_RULES.md` §6.2.1), `Root Magnitude = 30` (`BOSS_RULES.md` §6.3.1 item 3
/ `BossDefinitions.cs`), and `Flame Burst = 150` (`BOSS_RULES.md` §6.3.1 item
1) — carried here only to make the missing semantics concretely
distinguishable. **No new value is introduced.**

**This task does not modify any implementation.** `StatusEffectLifecycle`,
`BattleStateService`, `DamagePipeline`, `BossDefinition`, `BossDefinitions`,
and every test file are untouched.

---

## Objective

Obtain and record the **Product Owner's explicit decision** on the one
pre-existing gameplay ambiguity that currently blocks TASK-127:

```text
How is a Boss-side `BuffDebuff` ATK modifier's percentage DIRECTION
represented and interpreted when EffectiveBossATK is calculated?
```

The task is complete when:

```text
Existing evidence
      ↓
Present exact ambiguity
      ↓
Obtain explicit Product-Owner gameplay decision
      ↓
Record decision
      ↓
Stop
```

It presents the documented evidence, states the ambiguity precisely with
worked example values, obtains a human/Product-Owner answer, and records it —
without choosing, recommending, ranking, or defaulting any reading, and without
applying the answer to any authoritative document.

---

## Problem Statement

TASK-127 implements `StatusEffectLifecycle.EffectiveBossAttack(int,
IReadOnlyList<StatusEffect>)`, the `COMBAT_RULES.md` §5.5.1 Step-1 `Attack`
value for a Boss attack. To decide whether an active `TargetStat = "ATK"`
instance **raises** or **lowers** `BossState.ATK`, the current implementation
branches on the **sign** of `Magnitude`:

```text
Magnitude < 0  →  effective × (100 − |Magnitude|) / 100     (reduce)
Magnitude >= 0 →  effective × (100 + |Magnitude|) / 100     (increase)
```

The TASK-127 review determined this branch is an **invented rule**: no
authoritative document assigns direction semantics to `Magnitude`, and the
existing content contradicts the branch outright — Rage is a **buff** stored as
the **positive** `+20`, while Root is a **debuff** stored as the **positive**
`30`. Under the current branch a Boss-side ATK debuff of magnitude `+30` would
be applied as a **buff** (`ATK 100 → 130`).

The gap is therefore not an implementation defect that a silent correction can
fix: **no** available reading is authorized. Following `§5.4.1` item 3's
absolute-value formula would turn the documented `+20` buff into a reduction
(`100 → 80`), contradicting `§3.4`'s worked example of `120`. Choosing a
direction representation, or authoring a Boss-side formula, is a gameplay
decision.

---

## Authoritative Evidence

### The ambiguity and its owners (READ ONLY — this task records, it does not define)

- `docs/01-game-design/COMBAT_RULES.md` **§5.5.1** (L998–L1041) — the **Boss
  ATK Modifier Rule**, and the **canonical owner** of the missing Boss-side
  percentage direction/formula. Its **item 3** (L1022–L1026) states only:

  ```text
  EffectiveBossATK = the modified BossState.ATK
  Step 1 = EffectiveBossATK
  ```

  Its "Percentage application and rounding" bullet (L1034–L1038) supplies the
  **truncation** convention and states verbatim: *"This rule authors **no
  percentage of its own**; the active instance's `Magnitude` supplies it
  (`BOSS_RULES.md` §6.2)."* **It does not state whether the supplied
  percentage is added or subtracted, and it authors no sign convention.** Its
  flow diagram (L1014–L1020) writes `+20% Rage modifier`, which presupposes an
  increase without defining how a magnitude of `20` yields one.

- `docs/01-game-design/COMBAT_RULES.md` **§5.4.1 item 3** (L858) — the
  **only authored percentage formula**, and it is **Pet-scoped** and
  **unidirectional**:

  ```text
  EffectiveATK = truncate( ATK × (100 − |Magnitude|) / 100 )
  ```

  It takes the **absolute value** and only ever **reduces**; §5.4.1 item 2
  (L850) says *"EffectiveATK = the **reduced** `PetState.ATK`"*. Its scope is
  the Pet: §5.4.1 (L837–L838) *"modifies the active **Pet's** ATK"*, and
  §5.4.5 (L965) states *"Does NOT apply to the **Boss's** damage"*. It
  therefore **cannot express the Boss-side Rage increase** by itself, and it is
  **not** the Boss formula.

- `docs/01-game-design/COMBAT_RULES.md` **§3.4** ("Boss Skill Step-1
  composition", L459–L533) — the **resolved** contract (TASK-125/TASK-126)
  whose worked example requires `BossState.ATK = 100` + `Rage = +20%` →
  `EffectiveBossATK = 120`, and `120 + 150 = 270`. **Its `120` outcome MUST be
  preserved by this decision and MUST NOT be reopened.**

- `docs/01-game-design/BOSS_RULES.md` **§6.2.1** (L232–L263) — Hỏa Long's
  Rage: `+20% ATK` for 3 turns, represented as a Turn-based `BuffDebuff` in
  `BossState.StatusEffects[]` with `TargetStat = "ATK"`,
  **`Magnitude = +20%`** (L238), `RemainingTurns = 3`. A **positive magnitude
  for a buff.**

- `docs/01-game-design/BOSS_RULES.md` **§6.3.1 item 3** (L378–L382) — Root:
  *"Applies a **-30%** Pet ATK debuff"*, *"Percentage-based ATK reduction
  (-30% active Pet ATK)"*. The existing declaration stores it as the
  **positive** `Magnitude = 30` (`src/backend/GameServer.Domain/Bosses/
  BossDefinitions.cs` L213–L221). A **positive magnitude for a debuff.**

- `docs/02-technical/GAME_STATE.md` **§2.3.1** (L1148–L1221) — the
  `StatusEffect` instance schema. Its tree (L1155–L1171) lists exactly
  `Id | Type | Source | Magnitude | TargetStat | RemainingTurns |
  ExpiryCondition` — **no direction/operation/buff-vs-debuff member**. **Item 2**
  (L1179–L1187) states verbatim: *"`Magnitude` is **typed but not interpreted
  here**. What the number means (flat damage, a percentage reduction, an
  absorption pool) is owned by the effect's rule document."* **No sign
  semantics are assigned anywhere in this document.**

- `docs/01-game-design/COMBAT_RULES.md` **§5.1** (L678) — the Buff/Debuff
  definition: *"temporary stat modification (ATK/DEF/Crit/etc.), with duration
  measured in Turns unless stated otherwise; how a `Magnitude` reaches the stat
  its `TargetStat` names is owned by §5.4"*. It names **one combined category**
  and authors no direction discriminator.

- **The representation carries no direction discriminator.** The four
  `StatusEffectType` members (`DoT | BuffDebuff | Shield | State`) select the
  **duration model**; `BuffDebuff` is a single combined *"Buff/Debuff"*
  category that does not distinguish buff from debuff. `Source`
  (`Player | Boss`) is documented as *"a label, not a lookup … no rule reads it
  to decide behavior"* (`src/backend/GameServer.Domain/Battle/
  StatusEffectType.cs` L94–L96), so it cannot carry direction either.

- `docs/03-decisions/ADR/ADR-017-*` **Option E** (L390–L399) — the precedent
  that adding a Status Effect representation for a missing semantic *"is a
  gameplay decision owned by `COMBAT_RULES.md`, not by this state"*, and that
  no gameplay decision authorizes a new Status Effect type. **Context for the
  representation question — not a basis for an answer.**

### Why this is a decision and not a derivable fact

`AGENTS.md` §20 lists "Missing rule" and "Ambiguous requirement" as stop
conditions, and `AGENTS.md` §7 requires a missing rule to be reported and
approved rather than guessed. `GAME_RULES.md` §20's Rule Change Policy is
explicit — `Detect Conflict → Report Conflict → Propose Change → Human Approval
→ Update Rules` — and `.ai/README.md` §18 fixes the default assumption when
code and docs disagree: **docs describe intended behavior; code is the
(possibly incorrect) implementation.**

The required **outcome** for Rage is decided (`100 → 120`); the **arithmetic
and its direction signal** are not authored. The repository contains no
authoritative answer to:

```text
Given:  Type = BuffDebuff
        TargetStat = "ATK"
        Magnitude = 30

How does the Boss-side consumer know whether this means
+30% or −30%?
```

Silence is missing information, not permission to choose (`AGENTS.md` §23).
**TASK-127 did the correct thing by stopping.** This task exists to obtain the
decision it could not.

### Confirmed: no existing task owns this decision

Checked `tasks/backlog/`, `tasks/active/`, `tasks/blocked/`, and
`tasks/completed/`:

```text
TASK-127  — the DOWNSTREAM CONSUMER, BLOCKED on this decision. Not its owner.
TASK-123  — owns the Boss PASSIVE effect contract; its decision IDs are all
            resolved, and its recorded gaps are GAP-1…GAP-5, none of which is
            this question.
TASK-125  — owns the Boss Skill Step-1 COMPOSITION (resolved, Option B).
TASK-126  — applied that composition. DONE.
TASK-119  — owns the PET-side §5.4 consumption contract. Its §5.4.1 response
            is the unidirectional formula this question must NOT reuse.
=> No task owns the Boss-side ATK modifier DIRECTION contract.
```

---

## Decision Question

> **PRODUCT OWNER INPUT — SUPPLIED.** The item below is a gameplay/
> representation decision. It was supplied by the Product Owner (see
> §"Decision — PRODUCT OWNER DECISION (RECORDED)" below) and is recorded
> here verbatim by an agent acting as transcriber only. The agent selected,
> recommended, ranked, and defaulted nothing.
>
> **No answer is pre-judged anywhere in this task.** The categories listed in
> §6 are candidate *shapes* the answer might take, drawn from the existing
> documents or named as open possibilities. They are listed in neutral order
> and none is a default, a ranking, or a recommendation. The Product Owner may
> select one, combine them, or define another, provided every coverage item in
> §5 is answered.

### D-1 — Boss-side `BuffDebuff` ATK modifier direction

**Question (exact, verbatim).**

```text
How should the Boss-side `BuffDebuff` ATK modifier communicate and apply
percentage direction when calculating EffectiveBossATK?
```

The decision must resolve **both halves**: the **semantic direction** (what
directions the modifier can represent) and the **representation** (how the
Boss-side consumer determines which one applies).

### Documented input values the answer must be expressible against

```text
BossState.ATK                = 100   (COMBAT_RULES.md §1.1 — MVP ATK default)
Hỏa Long Rage                = +20%  (BOSS_RULES.md §6.2.1 — "Magnitude = +20%")
Root (existing)              = 30    (BOSS_RULES.md §6.3.1 item 3;
                                      BossDefinitions.cs — an ATK DEBUFF
                                      stored as a POSITIVE magnitude)
Flame Burst authored Base    = 150   (BOSS_RULES.md §6.3.1 item 1)
```

The required, already-resolved outcomes:

```text
Rage active:    EffectiveBossATK = 120       (COMBAT_RULES.md §3.4;
                                               TASK-125/TASK-126 — NOT reopened)
Rage inactive:  EffectiveBossATK = 100
```

---

## Decision — PRODUCT OWNER DECISION (RECORDED)

<!--
  RECORDED BY THE EXECUTING AGENT — TRANSCRIPTION ONLY.
  The text below is the Product Owner's supplied decision, recorded verbatim.
  The agent chose no option, ranked none, recommended none, and defaulted
  none. Nothing below was authored by the agent.
-->

### Decision

```text
How should the Boss-side `BuffDebuff` ATK modifier communicate and apply
percentage direction when calculating EffectiveBossATK?
```

---

### §6.1 Direction

```text
BOTH increase and decrease.

A Boss-side ATK `BuffDebuff` supports:
  - increase
  - decrease

There is no "increase only" and no "decrease only" semantic.
```

---

### §6.2 Representation

```text
Direction is represented by the SIGN of `Magnitude`.

SIGN of Magnitude:
    Magnitude > 0   ->  increase
    Magnitude < 0   ->  decrease

This is a deliberate, explicit semantic assignment to an EXISTING field
(`Magnitude`, GAME_STATE.md §2.3.1). No new state field, no new StatusEffect
member, no new Type, and no new TargetStat value is introduced.
```

```text
Downstream state-contract consequence: NONE.
GAME_STATE.md §2.3.1 already stores a signed `Magnitude` and explicitly
declines to interpret it ("typed but not interpreted here"), leaving the
meaning to the effect's rule document. This decision supplies that meaning
for the Boss-side ATK case at its canonical owner; it adds no member.
```

---

### §6.3 Formula

```text
EffectiveBossATK = truncate( BossState.ATK × (100 + Magnitude) / 100 )

  base ATK      = BossState.ATK
  Magnitude     = the active instance's Magnitude, used with its OWN SIGN
                  (not its absolute value)
  direction     = carried by Magnitude's sign
                  Magnitude = +20  ->  (100 + 20)  ->  increase
                  Magnitude = -30  ->  (100 - 30)  ->  decrease
  rounding      = truncate toward zero, to an integer
                  (the integer convention COMBAT_RULES.md §5.5.1's
                   "Percentage application and rounding" bullet and §5.4.2
                   already state; authored arithmetic only — no new
                   rounding rule is introduced)
```

Equivalently, and stated per direction so there is exactly one reading:

```text
Magnitude > 0:  EffectiveBossATK = truncate( ATK × (100 + Magnitude) / 100 )
Magnitude < 0:  EffectiveBossATK = truncate( ATK × (100 + Magnitude) / 100 )
```

The two lines are the same expression; the sign of `Magnitude` supplies the
direction inside the single formula. There is no branch on a separate flag.

```text
Magnitude = 0  ->  EffectiveBossATK = BossState.ATK   (no change)
```

**Multiple active ATK modifiers.** `COMBAT_RULES.md` §5.5.5 and
`GAME_STATE.md` §2.3.1 item 6 already fix the existing Boss contract: at most
one instance per effect identity, refreshed rather than stacked, with no new
stacking model. This decision introduces **no** stacking rule. It only
defines the arithmetic for one instance; where more than one distinct active
ATK instance is present in the existing collection, each is applied in turn
by the same single formula, in the collection's existing committed order.
The refresh-not-stack default and the one-instance-per-identity rule are
unchanged and are referenced, not restated.

---

### §6.4 Hỏa Long Rage

```text
PRESERVED. Not reopened, not changed.

BossState.ATK = 100
Rage          = Magnitude +20  (BOSS_RULES.md §6.2.1 stores it POSITIVE)

EffectiveBossATK = truncate(100 × (100 + 20) / 100)
                 = truncate(100 × 120 / 100)
                 = 120

100 + 20%  ->  120                       (COMBAT_RULES.md §3.4 — unchanged)
120 + 150 (Flame Burst authored) = 270   (unchanged)
Rage inactive: EffectiveBossATK = 100 -> 100 + 150 = 250   (unchanged)
```

TASK-125, TASK-126, `COMBAT_RULES.md` §3.4, Rage's `+20%`, and Rage's
`3 turns` are **not** reopened and are **not** modified.

---

### §6.5 Root

```text
Root's authored value is NOT changed (Magnitude = 30) and Root is NOT
redesigned.

The existing Boss-side declaration stores Root's Pet-ATK debuff as the
POSITIVE `Magnitude = 30`. Under this decision the sign of `Magnitude`
carries direction, so a `TargetStat = "ATK"` instance whose `Magnitude` is
the positive `30` reads as an INCREASE (+30%), not as the authored -30%.

The reason the Boss-side consumer is not asked to read Root's instance is
that Root is not a Boss-side ATK modifier at all:

  - Root's `TargetStat = "ATK"` instance is applied to the ACTIVE PET
    (BOSS_RULES.md §6.3.1 item 3: "-30% Pet ATK debuff"), so it lives in
    PetState.StatusEffects[], not in the Boss's collection.
  - Root's consumption is owned by the PET-side rule
    COMBAT_RULES.md §5.4.1 item 3, whose formula is
    `EffectiveATK = truncate(ATK × (100 - |Magnitude|) / 100)` — it takes
    the ABSOLUTE value, so the positive `30` yields the authored -30%
    (100 -> 70).
  - §5.4.5 states the Pet-side rule "Does NOT apply to the Boss's damage".

So the authored "-30% Pet ATK debuff" is expressed by the **Pet-side**
consumer's absolute-value reading, not by the Boss-side sign reading, and
Root's authored value and design are untouched. This decision does not
change, reinterpret, or migrate Root.
```

```text
Recorded consequence (report-only, not acted on here):
The Boss-side sign convention and the Pet-side absolute-value convention are
DIFFERENT conventions for the same `Magnitude` field, selected by the
collection (Boss's StatusEffects[] vs Pet's StatusEffects[]). That is the
direct result of this decision and is stated so the later applying task
records it at the canonical owners rather than discovering it as a
contradiction.
```

---

### §6.6 Pet-side separation

```text
CONFIRMED. This Boss-side decision:

  does NOT replace   COMBAT_RULES.md §5.4.1, and
  does NOT modify    COMBAT_RULES.md §5.4.1, and
  does NOT redefine  COMBAT_RULES.md §5.4.1.

§5.4.1 remains Pet-scoped and unidirectional exactly as authored:

  EffectiveATK = truncate( ATK × (100 − |Magnitude|) / 100 )

The Boss-side rule is a separate, Boss-scoped rule at the Boss-side
canonical owner, COMBAT_RULES.md §5.5.1. The two rules are counterparts,
not one rule, and the Boss-side formula is not retro-applied to the Pet.
§5.4.1's own scope statements (§5.4.1 "modifies the active Pet's ATK";
§5.4.5 "Does NOT apply to the Boss's damage") are unchanged.
```

---

### §6.7 GAME_STATE impact

```text
NO.

Resolving direction does NOT require a change to GAME_STATE.md §2.3.1.

The chosen representation is the SIGN of the EXISTING `Magnitude` field.
§2.3.1's instance tree already lists `Magnitude`, and §2.3.1 item 2 already
states verbatim that `Magnitude` is "typed but not interpreted here" and
that its meaning "is owned by the effect's rule document". This decision
supplies that meaning for the Boss-side ATK case at its rule document
(COMBAT_RULES.md §5.5.1), which is exactly the arrangement §2.3.1 describes.

No member is added, removed, renamed, retyped, or made optional/required
differently. No new collection. No new representation.

Downstream consequence: NONE for GAME_STATE.md §2.3.1.
GAME_STATE.md is NOT modified by this task.
```

```text
Related (reported, not acted on here): §2.3.1 item 2's last sentences
currently cite COMBAT_RULES.md §5.4 as owning how a `Magnitude` reaches its
`TargetStat`, including "the `\"ATK\"` case". That is the Pet-side owner. The
Boss-side counterpart now exists at §5.5.1. Whether §2.3.1 item 2 should name
§5.5.1 alongside §5.4 is a downstream documentation-consistency question for
the separate documentation-resolution task, not a representation change and
not a change made here.
```

---

### §6.8 Runtime behavior

```text
Deterministic behavior TASK-127 must implement.

Input:
  BossState.ATK  — integer
  an active Boss-side instance: Type = BuffDebuff, TargetStat = "ATK",
  Magnitude = signed integer percentage

Computation (per active instance, in the collection's committed order):
  EffectiveBossATK = truncate( EffectiveBossATK × (100 + Magnitude) / 100 )

  with truncation toward zero and integer-domain arithmetic (no
  floating-point multiply-then-cast), so the result is platform- and
  evaluation-order-independent.
```

Required deterministic outcomes:

```text
ATK 100, Magnitude +20   ->  120
ATK 100, Magnitude -30   ->   70
ATK 100, Magnitude  +0   ->  100
ATK 100, no instance     ->  100
```

`Given/When/Then` scenarios derivable from this decision:

```text
1. Rage active
   Given BossState.ATK = 100
   And an active Boss "ATK" BuffDebuff instance with Magnitude = +20
   When EffectiveBossATK is derived
   Then EffectiveBossATK = 120

2. Boss-side decrease
   Given BossState.ATK = 100
   And an active Boss "ATK" BuffDebuff instance with Magnitude = -30
   When EffectiveBossATK is derived
   Then EffectiveBossATK = 70

3. No modifier
   Given BossState.ATK = 100
   And no active Boss "ATK" BuffDebuff instance
   When EffectiveBossATK is derived
   Then EffectiveBossATK = 100

4. Zero magnitude
   Given BossState.ATK = 100
   And an active instance with Magnitude = 0
   When EffectiveBossATK is derived
   Then EffectiveBossATK = 100

5. Truncation
   Given BossState.ATK = 51
   And an active instance with Magnitude = +20
   When EffectiveBossATK is derived
   Then EffectiveBossATK = truncate(61.2) = 61      (truncated toward zero)

6. Non-default base stat
   Given BossState.ATK = 200
   And an active instance with Magnitude = +20
   When EffectiveBossATK is derived
   Then EffectiveBossATK = 240
        (so a hard-coded 120 cannot pass)
```

Non-destructiveness is unchanged and is not re-decided here:
`BossState.ATK` is never overwritten, there is no "restore" step, and
`EffectiveBossATK` is never persisted (`COMBAT_RULES.md` §5.5.4).

---

### Decision rationale

```text
[Only the Product Owner's supplied rationale. None was supplied with this
decision. Recorded as: NOT SUPPLIED — no rationale was provided, and none is
invented here.]

The decision above was supplied as an answer without accompanying rationale.
No rationale is authored, inferred, or reconstructed by the executing agent.
```

---

### Recording metadata

```text
Recorded by:        executing agent — transcription only
Recorded in:        this task file only (tasks/backlog/TASK-128-*.md)
Agent selections:   NONE — no option chosen, ranked, recommended, or
                    defaulted by the agent
```

---

## Decision Options / Possible Categories

<!--
  NEUTRAL PRESENTATION ONLY. These are not recommendations. They are the
  shapes an answer could take, drawn from the evidence above. The Product
  Owner may select one, combine them, or define another — provided §5's
  coverage items are all answered.
-->

### For the semantic direction (§6.1)

```text
Possible direction 1:  the modifier can only INCREASE Boss ATK
Possible direction 2:  the modifier can only DECREASE Boss ATK
Possible direction 3:  the modifier can represent BOTH, with an explicit
                       direction signal
```

### For the representation (§6.2) — how the consumer determines direction

```text
Possible representation A:  an EXISTING StatusEffect field carries it
                            (candidate fields, per GAME_STATE.md §2.3.1:
                            Id | Type | Source | Magnitude | TargetStat |
                            RemainingTurns | ExpiryCondition)
Possible representation B:  the SIGN of Magnitude carries it
                            (⚠ the existing content contradicts a plain
                            "positive = buff" reading: Rage +20 is a buff and
                            Root +30 is a debuff)
Possible representation C:  another EXISTING semantic property carries it
Possible representation D:  a NEW direction representation is introduced
                            (⚠ GAME_STATE.md §2.3.1 would then need a change,
                            and ADR-017 Option E records that adding a Status
                            Effect representation is a gameplay decision)
Possible representation E:  another explicitly chosen representation,
                            defined by the Product Owner
```

### Note recorded so it is not used as a basis for an answer

```text
TASK-127's current `Magnitude < 0` branch is an implementation assumption
that the review found unsupported. It is NOT evidence for any option, and
its removal is NOT a resolution of this question: following §5.4.1 item 3's
absolute-value formula instead would make the documented Rage +20 REDUCE
ATK (100 → 80), contradicting §3.4's 120.
```

### Note recorded so it is not used as a basis for an answer

```text
Convenience, expected balance, symmetry with the Pet-side rule (§5.4.1), and
"what is easiest to implement" are NOT bases for this decision. See §8.
```

---

## Required Decision Coverage

The supplied answer must explicitly resolve **all eight** items below.
"Unspecified" counts as unanswered.

```text
§6.1  DIRECTION — For a Boss `BuffDebuff` targeting `ATK`, does the modifier
      represent an INCREASE, a DECREASE, or BOTH?

§6.2  REPRESENTATION — How does the Boss-side consumer DETERMINE which
      direction applies to a given instance?

§6.3  FORMULA — The exact Boss-side `EffectiveBossATK` formula, specifying:
        - the base ATK term
        - how `Magnitude` enters
        - how direction enters
        - rounding / truncation
        - multiple active modifiers (ONLY if the existing Boss contract
          already requires them; do NOT invent new stacking rules)

§6.4  HỎA LONG RAGE — The answer MUST preserve, explicitly:
        BossState.ATK = 100, Rage = +20%  →  EffectiveBossATK = 120
      (already resolved by TASK-125/TASK-126; NOT reopened, NOT changed)

§6.5  ROOT — How the EXISTING Root representation
        TargetStat = "ATK", Magnitude = 30
      is interpreted as an ATK reduction. Root's value is NOT changed and
      Root is NOT redesigned by this decision.

§6.6  PET-SIDE SEPARATION — Explicit confirmation that the Boss-side rule
      does NOT silently replace, restate, or modify COMBAT_RULES.md §5.4.1's
      Pet-side ATK reduction semantics.

§6.7  STATE REPRESENTATION IMPACT — Whether resolving direction requires a
      change to GAME_STATE.md §2.3.1. If yes, it is recorded as a downstream
      documentation/state-contract consequence (NOT made here).

§6.8  RUNTIME BEHAVIOR — Enough defined behavior for TASK-127 to compute
      `EffectiveBossATK` deterministically, such that executable tests
      (Given/When/Then, AGENTS.md §15) can be written after the decision is
      applied.
```

---

## Non-Decisions

The following are **already decided** and are recorded here so they are **not**
read as in question. This task must NOT reopen any of them.

```text
TASK-125 Option B — UNCHANGED:

    Boss Skill Step 1 Base Damage
        = EffectiveBossATK + authored Skill Base Damage

    Boss ATK = 100, Rage +20%  →  EffectiveBossATK = 120
    Skill Base = 150           →  Step-1 Base Damage = 270

GAP-1                        — RESOLVED by TASK-125/TASK-126. NOT reopened.
Boss Skill Step-1 composition — RESOLVED at COMBAT_RULES.md §3.4. NOT reopened.
TASK-125, TASK-126            — NOT reopened, NOT modified.
COMBAT_RULES.md §5.4.1        — the PET-side rule. NOT modified, NOT replaced.
Root's magnitude and design   — NOT changed.
```

**The only decision this task obtains is:**

```text
How is Boss-side ATK BuffDebuff DIRECTION represented and interpreted?
```

---

## Out of Scope

- **Answering the decision.** The single prohibited action. No option is
  selected, recommended, ranked, or defaulted by this task or any agent.
- **Applying the decision to any authoritative document.** Zero `docs/` edits.
  `COMBAT_RULES.md`, `BOSS_RULES.md`, `GAME_STATE.md`, and `GAME_RULES.md` are
  untouched.
- **Any source code change.** Zero files under `src/` or `tests/`. In
  particular: no `EffectiveBossAttack` change, no `StatusEffectLifecycle`
  change, no `BattleStateService` change, no `DamagePipeline` change, no
  `BossDefinition`/`BossDefinitions` change.
- **Implementing `EffectiveBossATK`, Boss Skill damage, Rage application, or
  Root.** All are excluded.
- **Modifying `TASK-127`** — including its Status, scope, content, or evidence.
- **Modifying `TASK-123`, `TASK-124`, `TASK-125`, `TASK-126`, or `TASK-119`.**
- **Creating the downstream documentation-resolution task** that applies the
  decision to `COMBAT_RULES.md` §5.5.1 (and `GAME_STATE.md` §2.3.1 if
  required). That task is created **after** the decision exists and is **not**
  created here.
- **Creating an ADR**, unless evidence proves one is genuinely required — in
  which case it is **reported**, not authored.
- **Introducing any new formula, value, or scaling rule.**
- **Creating a new Status Effect `Type`**, a new `TargetStat` value, a new
  state member, a new Battle Event, a new SignalR member, a new API endpoint,
  a new Redis key, or a database column.
- **Modifying `GAME_STATE.md`, `COMBAT_RULES.md`, `BOSS_RULES.md`,
  `GAME_RULES.md`, `DATABASE.md`, `SIGNALR_PROTOCOL.md`, `API_CONTRACTS.md`,
  or `REDIS_STATE.md`.**
- Frontend, Phaser, SignalR code, Redis, PostgreSQL, Match-3, Cards, Relics,
  Pet redesign, reward system, progression.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Downstream Impact

### Canonical owners (identified, NOT modified by this task)

```text
CONCEPT                              CANONICAL OWNER            WHAT HAPPENS LATER
-----------------------------------  -------------------------  ------------------
Boss-side ATK modifier percentage    COMBAT_RULES.md §5.5.1     The applying task
  direction and formula                                          authors the decided
                                                                 formula/direction
                                                                 statement here.
                                                                 NOTE: §5.5.1 is the
                                                                 Boss ATK Modifier
                                                                 Rule and currently
                                                                 authors NO formula.

Status Effect instance schema        GAME_STATE.md §2.3.1       IF §6.7 resolves that
  (`StatusEffect` members)                                       direction requires a
                                                                 representation change,
                                                                 the applying task
                                                                 records it here.
                                                                 This task does NOT
                                                                 change it and does
                                                                 NOT create any field.

Pet-side ATK reduction               COMBAT_RULES.md §5.4.1     UNCHANGED. The Boss
  (truncate(ATK × (100 − |M|)/100))                              rule is the Boss-side
                                                                 counterpart; it does
                                                                 not replace §5.4.1.

Boss Skill Step-1 composition        COMBAT_RULES.md §3.4       UNCHANGED (resolved).
```

**Duplication rule (`documentation-change.md` §2).** The decision is written
**once**, at `COMBAT_RULES.md` §5.5.1. Referencing sections **point at** the
owner and do not restate it.

### Downstream tasks (reported, NOT created here)

```text
1. A documentation-resolution task that applies the recorded decision to
   COMBAT_RULES.md §5.5.1 (and GAME_STATE.md §2.3.1 if §6.7 requires it).
   Owner: created after the decision exists. NOT created here.

2. TASK-127 — the implementation task that consumes the resolved contract.
   Currently BLOCKED; it remains the downstream implementation owner and is
   NOT modified by this task.
```

---

## Stop Conditions

Universal stop conditions (`AGENTS.md` §20, `.ai/README.md` §13) always apply.
Task-specific stops — **STOP and report instead of guessing** if:

1. **Authoritative evidence already deterministically establishes the
   direction.** If, on re-inspection, the existing documents DO settle it,
   this task is redundant — **STOP** and report which document, which section,
   and which sentence settles it. (TASK-127's review assessed that they do not;
   re-verify, do not assume.)
2. **The decision requires a new gameplay rule beyond this direction
   question.** **STOP** and report the additional rule.
3. **The question actually depends on an unresolved separate combat rule.**
   **STOP** and report the dependency (file + section).
4. **The Product Owner decision cannot be obtained.** Report the task as
   awaiting input per the TASK-104 / TASK-116 / TASK-119 precedent: the
   unfilled `Decision:` field is this task's **normal starting state, not a
   failure and not a blocker** — no lifecycle transition applies. Move to
   `BLOCKED` only if the task-generation workflow is found not to permit a
   decision-input task.
5. **Resolving it requires changing the Damage Pipeline architecture.** **STOP**
   per `AGENTS.md` §18 and report.
6. **Resolving it requires a new `BattleState`/`BossState` member or a new
   `StatusEffect` member.** **STOP** and report — that is a state-contract
   change requiring its own authorization, recorded as a §6.7 consequence, not
   made inline.
7. **Resolving it requires a new SignalR, Redis, database, or API contract.**
   **STOP** and report.
8. **The answer would change a value owned by `BOSS_RULES.md` §6.2.1/§6.3.1**
   (Rage's `+20%` / `3 turns`, or Root's `30`). Those are authored balance
   values; this question concerns only their **direction semantics**. **STOP**
   and report — do not authorize a balance change.
9. **The answer would reopen TASK-125/TASK-126 or `§3.4`'s `120`.** **STOP**
   and report.
10. **The answer would modify the Pet-side `§5.4.1`.** **STOP** and report.
11. **The answer is supplied in a way that requires an ADR.** Report it; do not
    author it (`AGENTS.md` §18; check `docs/03-decisions/README.md` §8 first).
12. **Any work would touch `src/`, `tests/`, `docs/`, an ADR, `TASK-127`,
    `TASK-125`, `TASK-126`, `TASK-123`, `TASK-124`, `TASK-119`, or any existing
    task file.** **STOP.**

Use `.ai/README.md` §13's exact report format. Unrelated issues discovered while
working are **report-only** (`AGENTS.md` §16) — do not fix them inline.

### FINAL RULE (binding on the executing agent)

```text
Do NOT choose an option based on:

  * the current source implementation (TASK-127's `Magnitude < 0` branch);
  * implementation convenience;
  * symmetry with the Pet rules (COMBAT_RULES.md §5.4.1);
  * expected balance;
  * assumed RPG conventions;
  * which option is "cleaner", "simpler", or "more consistent".

Do NOT answer the gameplay question yourself.
Do NOT implement anything.
Do NOT rank or recommend any option.
```

---

## Acceptance Criteria

All criteria are binary and testable.

- [ ] The exact decision question in §5 is stated verbatim, with the candidate
      categories presented neutrally and **none** recommended, ranked, or
      defaulted.
- [ ] The documented evidence is recorded with file + section references for
      every claim, and no rule text is duplicated from `docs/`
      (`tasks/README.md` §9).
- [ ] The `Decision:` field in §6 carries the Product Owner's answer, recorded
      **verbatim**, or remains `<PENDING PRODUCT-OWNER DECISION>` with the
      awaiting-input report of §7.
- [ ] All **eight** §5 required-coverage items are explicitly resolved by the
      answer text, or recorded as unspecified — **never** filled in by the
      agent.
- [ ] **§6.1 Direction** — the answer explicitly states whether the Boss-side
      ATK modifier represents an increase, a decrease, or both.
- [ ] **§6.2 Representation** — the answer explicitly states how the Boss-side
      consumer determines the direction.
- [ ] **§6.3 Formula** — the answer explicitly defines the exact
      `EffectiveBossATK` formula, including base ATK, magnitude, direction,
      and rounding/truncation.
- [ ] **§6.4 Hỏa Long Rage** — the answer preserves `100 + 20% → 120`
      explicitly; TASK-125/TASK-126 and `§3.4` are **not** reopened.
- [ ] **§6.5 Root** — the answer explicitly explains how the existing
      `TargetStat = "ATK"`, `Magnitude = 30` representation is interpreted as
      an ATK reduction; Root's value is **not** changed and Root is **not**
      redesigned.
- [ ] **§6.6 Pet-side separation** — the answer explicitly states that
      `COMBAT_RULES.md` §5.4.1's Pet-side semantics are **not** replaced or
      modified.
- [ ] **§6.7 State representation impact** — the answer explicitly identifies
      whether `GAME_STATE.md` §2.3.1 requires a downstream change; any such
      change is recorded as a downstream consequence and **not** made here.
- [ ] **§6.8 Runtime behavior** — the answer is complete enough that TASK-127
      can compute `EffectiveBossATK` deterministically and executable tests
      can be derived from it.
- [ ] The decision is recorded **only in this task file**. Zero `docs/` edits.
- [ ] `COMBAT_RULES.md`, `BOSS_RULES.md`, `GAME_STATE.md`, `GAME_RULES.md`,
      `DATABASE.md`, `SIGNALR_PROTOCOL.md`, `API_CONTRACTS.md`, and
      `REDIS_STATE.md` are **byte-identical** after this task.
- [ ] No ADR is created or modified.
- [ ] No source code is modified; zero files under `src/` or `tests/` change.
- [ ] `TASK-127` is **byte-identical** (unmodified, unmoved, not re-statused)
      and remains the downstream implementation task.
- [ ] `TASK-119`, `TASK-123`, `TASK-124`, `TASK-125`, and `TASK-126` are
      **byte-identical** and no other existing task file is modified.
- [ ] The downstream documentation-resolution task is **not** created here.
- [ ] The task moves to `BLOCKED` per §7 **only** if a Stop Condition fires.

---

## §7 — Recording / Handoff

### Recording

```text
Record where:       §6's `Decision:` field of THIS task file — and nowhere else.
                    No `docs/` file, no ADR, no task other than this one.
Record how:         Verbatim, by an agent acting as transcriber only.
Also record:        §6A (below) — the coverage confirmation and the Decision
                    Record header.
```

### Handoff

```text
Handoff to:         A SUBSEQUENT DOCUMENTATION-RESOLUTION TASK (not created
                    here), which applies the recorded decision to its canonical
                    owners per documentation/documentation-change.md §3.
Owner documents:    docs/01-game-design/COMBAT_RULES.md §5.5.1 — the Boss ATK
                    Modifier Rule, and the canonical owner of the missing
                    Boss-side percentage direction/formula.
                    docs/02-technical/GAME_STATE.md §2.3.1 — IF §6.7 resolves
                    that the chosen representation requires a schema change;
                    otherwise unchanged.
Handoff must carry: The verbatim recorded answer; the §5 coverage mapping; the
                    §6.4 Rage outcome; the §6.5 Root interpretation; the §6.6
                    Pet-side separation; and the §6.7 state-impact finding.
Must NOT be carried: Any reading, recommendation, or interpretation the
                    Product Owner did not supply.
Downstream consumer: TASK-127 (BLOCKED), which remains the implementation owner.
```

### §6A. Decision Record

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT when the Product Owner answers.
  NOT completed until the decision is actually recorded.
-->

```text
Recorded by:        executing agent — transcription only
Date recorded:      recorded in this session
Answer supplied:    SIGN of `Magnitude` carries direction; both increase and
                    decrease are supported;
                    EffectiveBossATK = truncate(ATK × (100 + Magnitude) / 100).
                    See "Decision — PRODUCT OWNER DECISION (RECORDED)" above.
Coverage complete:  8 of 8 resolved
Verbatim:           yes — recorded in the section above, unaltered
Rationale:          NOT SUPPLIED — no rationale accompanied the decision, and
                    none was invented
```

### Coverage mapping (filled at recording time)

```text
Coverage §6.1  Direction (increase / decrease / both)?           BOTH

Coverage §6.2  How direction is represented/interpreted?         SIGN of the
                                                                 EXISTING
                                                                 `Magnitude`
                                                                 field
                                                                 (+ = increase,
                                                                  − = decrease)

Coverage §6.3  Exact EffectiveBossATK formula?                   RESOLVED
                                                                 EffectiveBossATK
                                                                 = truncate(
                                                                   ATK ×
                                                                   (100 +
                                                                   Magnitude)
                                                                   / 100)
                                                                 sign-carrying,
                                                                 truncate
                                                                 toward zero;
                                                                 no new stacking
                                                                 rule

Coverage §6.4  Rage preserved at 100 + 20% → 120?                RESOLVED —
                                                                 preserved:
                                                                 truncate(100 ×
                                                                 120 / 100)
                                                                 = 120;
                                                                 §3.4 and
                                                                 TASK-125/126
                                                                 not reopened

Coverage §6.5  Root (TargetStat=ATK, Magnitude=30) as a
               reduction?                                        RESOLVED —
                                                                 Root's `"ATK"`
                                                                 instance is
                                                                 PET-side,
                                                                 consumed by
                                                                 §5.4.1's
                                                                 absolute-value
                                                                 formula
                                                                 (100 → 70);
                                                                 Root's value and
                                                                 design unchanged

Coverage §6.6  Pet-side §5.4.1 not replaced/modified?            RESOLVED —
                                                                 confirmed not
                                                                 replaced, not
                                                                 modified, not
                                                                 redefined

Coverage §6.7  GAME_STATE.md §2.3.1 impact identified?           RESOLVED — NO
                                                                 change
                                                                 required; sign
                                                                 of an existing
                                                                 field; zero
                                                                 representation
                                                                 consequence

Coverage §6.8  Deterministic + testable enough for TASK-127?     RESOLVED —
                                                                 deterministic
                                                                 outcomes
                                                                 (±/0/absent)
                                                                 and 6 derivable
                                                                 Given/When/Then
                                                                 scenarios
                                                                 recorded
```

---

## Affected Files & Areas

```text
[ ] src/backend/  — FORBIDDEN. No Domain / Application / Infrastructure / Api
                    change. Explicitly: no StatusEffectLifecycle, no
                    BattleStateService, no DamagePipeline, no BossDefinition,
                    no BossDefinitions change.
[ ] src/frontend/client/ — FORBIDDEN.
[ ] tests/        — FORBIDDEN. No unit / integration / gameplay scenario change.
[ ] docs/         — FORBIDDEN. Zero edits. COMBAT_RULES.md, BOSS_RULES.md,
                    GAME_STATE.md, and GAME_RULES.md in particular are
                    byte-identical after this task.
[ ] docs/03-decisions/ADR/ — FORBIDDEN. No ADR created or modified.
[x] tasks/        — THIS NEW TASK FILE ONLY
                    (tasks/backlog/TASK-128-resolve-boss-side-atk-modifier-
                    direction-contract.md). No existing task file is modified.
```

---

## Implementation Notes

None. This task implements nothing and edits no `docs/` file.

Task-specific pointers only:

- The ambiguity's implementation-site evidence is in
  `tasks/backlog/TASK-127-implement-boss-skill-step-1-damage-composition.md`
  §"Rework Session — Review Finding 1", which records the blocker, the exact
  evidence, and the required decision. **Do not modify that file.**
- The review's independent findings are in the same file's §"Review Evidence".
  **Do not modify that file.**
- The Pet-side contrast is `COMBAT_RULES.md` §5.4.1 item 3. Use it to state the
  asymmetry, **not** as a basis for an option (§8 FINAL RULE).
- The canonical owner for the eventual Boss formula is
  `COMBAT_RULES.md` §5.5.1. Read it first.

---

## Testing Requirements

### Required Verification

```text
[x] N/A — decision-input task. No code, no tests authored, and no test runner
        is evidence of correctness here (.ai/workflow/documentation/
        documentation-change.md §4).
```

This task creates no executable verification. Its verification is the
Acceptance Criteria above plus the explicit byte-identity checks below:

```text
[ ] docs/ trees byte-identical before and after (COMBAT_RULES.md, BOSS_RULES.md,
    GAME_STATE.md, and GAME_RULES.md included) — no authoritative change.
[ ] docs/03-decisions/ byte-identical — no ADR created or modified.
[ ] src/ and tests/ byte-identical — no source code change.
[ ] tasks/backlog/TASK-127-* byte-identical, still in backlog/, Status
    unchanged.
[ ] Exactly one file added: this task file.
[ ] The decision, if supplied, appears ONLY in this task file — searchable by
    its recorded wording across docs/, src/, and tests/ (expect zero hits
    outside this task file).
```

The resulting decision must nonetheless be stated so that a **later**
documentation task can apply it and TASK-127 can derive Given/When/Then
scenarios from it per `AGENTS.md` §15 — **do not** author those scenarios here.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after the Decision Owner's answer is
  recorded. Until then every item below stays UNCHECKED. Do NOT mark these as
  completed until the Product Owner decision is actually recorded.
-->

### Decision Recorded

```text
Decision:

Direction is carried by the SIGN of the EXISTING `Magnitude` field
(+ = increase, − = decrease). Both increase and decrease are supported:

    EffectiveBossATK = truncate( BossState.ATK × (100 + Magnitude) / 100 )

truncated toward zero on the integer domain. No new state field, no new
StatusEffect member, no new Type, no new TargetStat value.
```

#### Direction

```text
BOTH increase and decrease. See §6.1 above.
```

#### Representation

```text
The SIGN of `Magnitude` — an EXISTING field (GAME_STATE.md §2.3.1).
Magnitude > 0 ⇒ increase; Magnitude < 0 ⇒ decrease. No new representation.
See §6.2 above.
```

#### Formula

```text
EffectiveBossATK = truncate( BossState.ATK × (100 + Magnitude) / 100 )

  base ATK   = BossState.ATK
  Magnitude  = the active instance's Magnitude, with its own sign
  direction  = the sign of Magnitude
  rounding   = truncate toward zero, integer domain

No new stacking rule; §5.5.5 / §2.3.1 item 6's refresh-not-stack default is
unchanged. See §6.3 above.
```

#### Hỏa Long Rage

```text
PRESERVED: BossState.ATK = 100, Rage Magnitude = +20
→ EffectiveBossATK = truncate(100 × (100 + 20) / 100) = 120
→ Step-1 Base Damage = 120 + 150 = 270

COMBAT_RULES.md §3.4 and TASK-125/TASK-126 are NOT reopened.
See §6.4 above.
```

#### Root

```text
Root's authored value (Magnitude = 30) is unchanged and Root is not
redesigned. Root's `TargetStat = "ATK"` instance is applied to the ACTIVE
PET, so it is consumed by the PET-side rule COMBAT_RULES.md §5.4.1 item 3 —
whose formula takes the ABSOLUTE value,

    EffectiveATK = truncate( ATK × (100 − |Magnitude|) / 100 )

so the positive `30` yields the authored −30% (100 → 70) on the Pet side.
The Boss-side sign convention is a separate, Boss-scoped rule and is not
applied to Root. Root's authored value and design are untouched.
See §6.5 above.
```

#### Pet-side separation

```text
CONFIRMED: COMBAT_RULES.md §5.4.1 is NOT replaced, NOT modified, and NOT
redefined by this Boss-side decision. §5.4/§5.4.1/§5.4.5 remain Pet-scoped
and unidirectional exactly as authored. See §6.6 above.
```

#### GAME_STATE impact

```text
NO — GAME_STATE.md §2.3.1 requires no change. The chosen representation is
the sign of the EXISTING `Magnitude` field, whose interpretation §2.3.1 item
2 already delegates to the effect's rule document. Zero representation
consequence. GAME_STATE.md is NOT modified here.
See §6.7 above (including the report-only note on §2.3.1 item 2's §5.4
citation, which is a downstream documentation-consistency question).
```

#### TASK-127 handoff

```text
The decision is now complete enough for TASK-127 to consume.
TASK-127 remains the implementation owner.
```

### Documentation

```text
No authoritative documentation was modified by this decision-input task.
```

### Source

```text
No source code was modified.
```

### Server Authority & Scope Verification

- [ ] Confirmed zero client-authoritative gameplay logic
- [ ] Confirmed no new state field, Battle Event, wire member, Redis key, or
      database column
- [ ] Confirmed zero files under `docs/` modified
- [ ] Confirmed zero files under `src/` or `tests/` modified
- [ ] Confirmed no ADR created or modified
- [ ] Confirmed TASK-127 and all other task files byte-identical
- [ ] Confirmed adherence to MVP Scope (`docs/00-overview/MVP_SCOPE.md` §1)
