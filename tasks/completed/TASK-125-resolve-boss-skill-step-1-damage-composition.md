# TASK-125 — Resolve the Boss Skill Step-1 Damage Composition (EffectiveBossATK Participation)

<!--
  GEN-TASK EXECUTION MANIFEST — PRODUCT-OWNER GAMEPLAY DECISION-INPUT TASK
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, schemas, or contracts beyond the minimum needed to state the
  question precisely.

  THIS IS A DECISION-INPUT TASK.
  No source code changes.
  No authoritative documentation changes.
  The recorded decision is input to a subsequent documentation-resolution task.

  THIS TASK ANSWERS NOTHING. Its only act is to present one pre-existing
  gameplay ambiguity with its exact evidence and obtain the Product Owner's
  explicit decision, then record it and stop.

  AN AGENT MUST NOT ANSWER §6. If no Product Owner answer is present, the
  agent reports the task as awaiting input and stops. Choosing, recommending,
  ranking, defaulting, or inferring either option is the single prohibited
  action of this task.

  PROVENANCE: recorded by TASK-124 (DONE) as its one non-blocking STOP —
  "STOP CONDITION — GAP-1" in
  tasks/completed/TASK-124-apply-boss-passive-contract-decisions.md. TASK-124
  assessed the authoritative evidence honestly, found the composition NOT
  determined by the existing documents, and per its §6 recorded it as an
  unresolved PRE-EXISTING gameplay decision rather than guessing or creating
  a decision task (its §6 forbade creating one). This task is that missing
  decision-input artifact.

  PRE-EXISTING, NOT CREATED BY TASK-123/TASK-124: the gap was surfaced by
  TASK-123's D-1a but is not settled by Contradiction A, D-1, D-1a, D-1b,
  D-1c, D-2*, D-3*, or D-4*.

  BOUNDARY: this task writes only this task file. It edits no docs/ file, no
  ADR, no src/ file, no tests/ file, and creates no ADR. It authors no damage
  value, no formula, and no rule.
-->

---

## Metadata

```text
Task ID:           TASK-125
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md, which governs
                   recording discipline: §2 no duplication, §3 canonical
                   owner, §4 report through quality/review.md +
                   core/completion.md). The deliverable is a RECORDED
                   Product-Owner decision artifact. See "Type classification
                   note". NOT GAMEPLAY-CHANGE: this task changes no rule and
                   touches no implementation — it records a decision that a
                   SEPARATE later task will apply.
Status:            DONE (Product Owner decision D-1 = Option B recorded verbatim
                   in §6; all 6 coverage items resolved; handoff to downstream
                   TASK-126 completed. Formal review pass completed against
                   quality/review.md §1 and core/completion.md §1: PASS. File
                   moved from tasks/backlog/ to tasks/completed/ per
                   TASK_LIFECYCLE.md §3.)
Risk:              MEDIUM (TASK_TYPES.md §4 — DOCUMENTATION baseline
                   LOW–MEDIUM; MEDIUM because the recorded answer is
                   cross-referenced by COMBAT_RULES.md §3 step 1, §3.4,
                   §5.4.1 item 2, §5.5.2 and BOSS_RULES.md §6.3/§6.3.1, and
                   because the answer determines whether a Boss ATK modifier
                   can reach Boss Skill damage — a combat-resolution
                   contract. LOW as an act: this task writes no docs/ file
                   and changes no rule. Risk rises downstream, when a
                   documentation task applies the answer.)
Priority:          HIGH (this is the sole remaining pre-existing gameplay
                   ambiguity blocking deterministic Boss Skill damage. It is
                   the last open item recorded against COMBAT_RULES.md §3.4
                   and BOSS_RULES.md §6.3.1: TASK-124 closed every other gap
                   it owned. ROADMAP.md Phase 1 requires "3 MVP Bosses
                   (Hỏa Long, Thủy Ma, Mộc Yêu — Passive + Skill each)" and
                   "Boss Response (Passive → Skill → Attack →
                   Victory/Defeat)". The Skill half landed in TASK-118; its
                   Step-1 composition remains undecided.)
Primary Agent:     orchestrator (task-lifecycle / requester coordination —
                   this task records requester input. TASK_TYPES.md §2
                   DOCUMENTATION names the Review Agent, and no domain agent
                   may author this answer. The orchestrator's own contract
                   permits "Task classification artifacts only" and forbids
                   it to "make game design decisions"
                   (.ai/agents/orchestrator.md §Scope / §Decision Authority),
                   which is exactly this task's posture. Same routing as the
                   TASK-104 decision-input precedent.)
Supporting Agents: N/A (no domain agent may supply or review the DECISION.
                   Review is limited to confirming that the decision slot
                   exists, that neither option is pre-judged or ranked, and
                   that any supplied answer is recorded verbatim.)
Workflow:          documentation/documentation-change.md
                   (no docs/ file is edited by this task. The workflow
                   governs recording discipline — §2 no duplication, §3
                   canonical owner — and §4 routes the final report through
                   quality/review.md + core/completion.md.)
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-124 (DONE — recorded this ambiguity as its GAP-1 STOP
                     CONDITION and explicitly left COMBAT_RULES.md §3.4
                     byte-identical rather than authoring a reading.
                     IMMUTABLE; READ-ONLY; must NOT be modified, re-statused,
                     moved, or rewritten),
                   TASK-123 (BLOCKED — D-1a's damage-scope decision, whose
                     determinism holds under BOTH options of this question.
                     IMMUTABLE; READ-ONLY; must NOT be modified),
                   TASK-118 (DONE — implemented step 18b Boss Skill secondary
                     effects and fenced step 18a out. IMMUTABLE; read-only),
                   TASK-119 (DONE — authored COMBAT_RULES.md §5.4, the
                     Pet-side ATK-modifier rule §5.5 mirrors. Context only:
                     its symmetry is explicitly NOT a basis for this decision.
                     IMMUTABLE; read-only),
                   TASK-021 (DONE — implemented the Damage Pipeline whose
                     §3 step 1 sum this question is about. IMMUTABLE;
                     read-only)
Blocks:            The subsequent documentation-resolution task that applies
                   the recorded decision to COMBAT_RULES.md and BOSS_RULES.md
                   (not created here), and through it deterministic Boss Skill
                   damage implementation and ROADMAP.md Phase 1's complete
                   Boss Response sequence.
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
TASK-094, and TASK-104.

**Type Re-Classification Condition (for the LATER applying task, not this
one).** This task records the answer; it does not type the task that applies it.
The applying task must determine its own type:

```text
- If Option A is chosen, COMBAT_RULES.md §3.4's existing wording ("Step 1 Base
  Damage is defined per Skill") stays correct and only a composition sentence is
  ADDED. That is likely DOCUMENTATION.

- If Option B is chosen, §3.4's existing wording remains literally true but the
  Step-1 sum in §3 step 1 becomes load-bearing for Boss Skills; whether that is
  an ADDED clarification or a CHANGE to documented behavior is for the applying
  task to determine and REPORT (TASK_TYPES.md §2 / §3; possibly
  GAMEPLAY-CHANGE).

Either determination is made DOWNSTREAM and is NOT made, pre-judged, or
recommended here.
```

**This task is NOT a substitute for the applying task, and it is not that task.**
Transcribing the recorded answer into its canonical owner documents
(`COMBAT_RULES.md` §3.4 — the Step-1 composition owner — and
`BOSS_RULES.md` §6.3.1) is a SEPARATE, subsequently created documentation task,
per `documentation/documentation-change.md` §3 (canonical owner).
**Do not bypass it.**

**This task creates no ADR.** The composition question is a combat-rule
question inside the existing Damage Pipeline. It introduces no battle-state
concept, no persistence change, no realtime strategy change, and no
module-boundary change. It is not listed in `docs/03-decisions/README.md` §8 and
this task does not add it there. If the later applying task finds an ADR is
genuinely required, that is reported there, not authored here.

**This task authors no damage value and no formula.** The only numbers appearing
below are the ones the authoritative documents already carry — `Boss ATK = 100`
(`COMBAT_RULES.md` §1.1's MVP default), `Hỏa Long Rage = +20%` and `3 turns`
(`BOSS_RULES.md` §6.2), and `Flame Burst Base Damage = 150`
(`BOSS_RULES.md` §6.3/§6.3.1) — carried here only to make the two readings
concretely distinguishable. **No new damage value is introduced.** None is this
task's to change.

**This task does not modify any implementation.** The Boss Response code path is
untouched. Current source behavior is recorded as EVIDENCE ONLY, in §7.

---

## Objective

Obtain and record the **Product Owner's explicit decision** on the one remaining
pre-existing gameplay ambiguity identified by TASK-124:

```text
When a Boss Skill resolves through the Damage Pipeline, what constitutes
its Step-1 Base Damage?
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

It presents the documented evidence, states the two readings precisely with a
worked example, obtains a human/Product-Owner answer, and records it — without
choosing, recommending, ranking, or defaulting either reading, and without
applying the answer to any authoritative document.

---

## Authoritative References

### The ambiguity and its owners (READ ONLY — this task records, it does not define)

- `docs/01-game-design/COMBAT_RULES.md` **§3 step 1** (line 217) — the Damage
  Pipeline's Step-1 definition. It establishes Base Damage as a **sum** of
  applicable contributions: "from ATK stat, Skill/Card base value, and any
  ATK-Gem-generated damage pool for this action". **This is the sum the Boss
  Skill question is about.**
- `docs/01-game-design/COMBAT_RULES.md` **§3.4** (lines 417–434) — **Boss
  Damage**, and the **canonical owner** of the Boss Skill Step-1 composition
  question. Its Boss Basic Attack clause fixes `Step 1 — Base Damage = Boss.ATK`
  (line 423). Its Boss Skill clause states only: *"Same pipeline as above, but
  Step 1 Base Damage is defined per Skill (`BOSS_RULES.md` §6)."* It names the
  value's **source**, never its **composition**. **It does not contain the
  sentence current source cites it for — see §7.**
- `docs/01-game-design/COMBAT_RULES.md` **§5.4.1 item 2** (lines 749–755) — the
  **Pet-side** shape, for contrast only: `Step 1 = EffectiveATK + Skill/Card
  base value + ATK-Gem-generated damage pool`. For the **Pet**, both the stat
  term and the Skill/Card base value contribute. **§3.4 authors no equivalent
  sentence for the Boss.** Per the FINAL RULE in §8, this asymmetry is
  documented evidence, **not** a basis for the decision.
- `docs/01-game-design/COMBAT_RULES.md` **§5.5 / §5.5.1 / §5.5.2 / §5.5.3**
  (TASK-124) — the **Boss-side** ATK modifier rule. §5.5.1 fixes the modifier's
  consumption point as the Boss Damage Pipeline **Step 1 `Attack` input**, as a
  derived, non-stored `EffectiveBossATK`. §5.5.2 limits its reach to Boss damage
  "whose Step 1 `Attack` input is derived from `BossState.ATK`" — the basic
  attack — and states it does **not** reach "a Boss SKILL whose Step 1 Base
  Damage is independently authored … §3.4 states a Boss Skill's 'Step 1 Base
  Damage is defined per Skill'; such a value does NOT receive the modifier
  merely because the attack is a Boss attack".
- `docs/01-game-design/COMBAT_RULES.md` **§5.5.2 "Composition note"** (lines
  966–976) — **TASK-124's own record of this exact ambiguity, in the
  authoritative file.** It states verbatim that `§3.4` and `§3 step 1`
  "**neither state[] whether the Skill's authored value REPLACES the ATK term or
  is ADDED to it**", that this "composition is an **open, pre-existing
  question** owned by `§3.4` / `BOSS_RULES.md` §6.3.1", and that it is
  "**deliberately not resolved here** — it is not this rule's to decide and no
  reading is inferred". **This is the authoritative record that the question is
  genuinely open.**
- `docs/01-game-design/BOSS_RULES.md` **§6.3** (lines 336–347) and **§6.3.1**
  (lines 349–365) — the MVP Boss Skill base configuration and per-Skill effect
  magnitudes. §6.3 gives flat per-Skill values ("150" / "120" / "100") and states
  they "are **MVP base configuration** … configuration defaults used at battle
  creation; they do not represent formulas or scaling rules". §6.3.1 item 1
  states `Base Damage: 150 (deals damage through Damage Pipeline to active Pet)`.
  **Neither states the Step-1 composition and neither declares ATK scaling.**
- `docs/01-game-design/BOSS_RULES.md` **§6.2** — the MVP Boss Passive rows,
  including Hỏa Long's `Gain +20% ATK (Rage) for 3 turns`. Context for decision
  coverage item 3. **Not amended.**
- `docs/01-game-design/COMBAT_RULES.md` **§1.1** — the `ATK` stat row (attribute
  "attack power"; MVP default `100`). The `100` in the §6 worked example is this
  documented default, not a value authored here.
- `docs/01-game-design/GAME_RULES.md` **§14** (the conceptual damage pipeline
  `COMBAT_RULES.md` §3 expands) and **§17** (the fixed resolution order — step
  18b Boss Skill, step 18c Boss Attack). Context for decision coverage item 5.
- `docs/01-game-design/GAME_RULES.md` **§20** — Rule Change Policy
  (`Detect Conflict → Report Conflict → Propose Change → Human Approval → Update
  Rules`), and `AGENTS.md` §7 / §20.

### Governance and process

- `tasks/completed/TASK-124-apply-boss-passive-contract-decisions.md` — **the
  provenance.** Its "STOP CONDITION — GAP-1" section is the complete, verbatim
  evidence record this task formalizes; its §6 forbade creating a decision task,
  and its Completion Evidence states GAP-1 "REMAINS UNRESOLVED". **IMMUTABLE —
  do not modify this file in any way.**
- `tasks/blocked/TASK-123-resolve-boss-passive-effect-contract.md` — D-1a
  (Rage's damage scope) and its "Remaining Pre-Existing Documentation Gaps"
  entry GAP-1. **IMMUTABLE — do not modify this file in any way.**
- `AGENTS.md` §4 (conflict resolution: detect → report → propose → STOP), §7
  (never invent a rule), §16 (task discipline), §20 ("Missing rule" / "Ambiguous
  requirement" stop conditions), §21 (output discipline).
- `docs/AGENTS.md` §2 (owner hierarchy), §4, §7, §20.
- `.ai/README.md` §13 (stop conditions and their exact report format), §18
  (default assumption: docs describe intended behavior; code is the possibly
  incorrect implementation).
- `.ai/workflow/documentation/documentation-change.md` §1 (flow), §2 (no
  duplication, ever), §3 (determining the canonical owner), §4 (composition —
  still goes through `quality/review.md`).
- `tasks/README.md` §6 (how to create a task), §9 (no business-rule duplication
  in task files), §12 (skill budget), `tasks/TASK_TYPES.md` §2 (DOCUMENTATION),
  §4 (risk), `tasks/TASK_LIFECYCLE.md` §3 (BACKLOG / READY / BLOCKED semantics).
- `docs/00-overview/MVP_SCOPE.md` §1 (Bosses and Combat/Damage are IN), §2 (OUT),
  §4 (unlisted is not implicitly IN).
- `docs/03-decisions/README.md` §8 — "Known Open Items (Not ADRs)". Checked: the
  composition question is **not** an ADR-level item.

### Decision-input precedent (format and discipline)

- `tasks/backlog/TASK-104-collect-cardcast-wire-and-shield-contract-decisions.md`
  — the Product-Owner decision-input shape this task follows (Decision Owner and
  Recording Discipline; per-item evidence, options, required coverage, and
  `Decision:` slot; the "an agent must not answer" gate; the Decision Record).
- `tasks/completed/TASK-061-record-product-owner-pet-xp-decisions.md` and
  `tasks/completed/TASK-094-resolve-buff-debuff-duration-consumption-timing.md`
  — the earlier precedent for the same shape.

### Explicitly NOT modified (verify, do not edit)

- `docs/01-game-design/COMBAT_RULES.md` — **byte-identical after this task**,
  §3.4 included.
- `docs/01-game-design/BOSS_RULES.md`, `docs/01-game-design/GAME_RULES.md`,
  `docs/02-technical/GAME_STATE.md`, `docs/02-technical/DATABASE.md`,
  `docs/02-technical/SIGNALR_PROTOCOL.md`, `docs/02-technical/API_CONTRACTS.md`,
  `docs/02-technical/REDIS_STATE.md` — **no edit**.
- `docs/03-decisions/README.md` and `docs/03-decisions/ADR/` — **no edit; no
  ADR.**
- `src/**`, `tests/**` — **FORBIDDEN.**
- `tasks/blocked/TASK-123-*`, `tasks/completed/TASK-124-*`, and every other
  existing task file — **IMMUTABLE.**

---

## 1. Decision Owner and Recording Discipline

```text
Decision owner:     The Product Owner (human). This is a GAMEPLAY decision —
                    the composition of a combat-rule Step-1 contribution. No
                    agent, and no domain agent, may supply it (AGENTS.md §7;
                    GAME_RULES.md §20; .ai/agents/orchestrator.md §Decision
                    Authority).

Recording agent:    An agent may ONLY transcribe a supplied answer verbatim.
                    It may fix formatting, never wording or values.

Answer verbatim:    The answer must be recorded exactly as the Product Owner
                    gives it. If an answer is ambiguous or incomplete relative to
                    its required coverage in §5, record it as given and note the
                    gap — do not resolve it.

Basis prohibited:   The answer must NOT be derived from the current source
                    implementation, implementation convenience, symmetry with
                    the Pet-side rule (§5.4.1 item 2), expected balance, or
                    assumed RPG conventions. See §8.
```

---

## 2. Current State

The composition is **undetermined by the existing authoritative documents** and
is recorded as such **inside `COMBAT_RULES.md` itself**:

```text
COMBAT_RULES.md §3 step 1   Base Damage is a SUM: "from ATK stat, Skill/Card
                            base value, and any ATK-Gem-generated damage pool
                            for this action" (line 217).

COMBAT_RULES.md §5.4.1      Spells the sum out for the PET (item 2):
  item 2                    "Step 1 = EffectiveATK + Skill/Card base value +
                             ATK-Gem-generated damage pool" (lines 753-755).
                            → For the PET, both the stat term and the authored
                              base value contribute.

COMBAT_RULES.md §3.4        BOSS SKILL clause states only: "Same pipeline as
                            above, but Step 1 Base Damage is defined per Skill
                            (BOSS_RULES.md §6)" (lines 430-433).
                            → Names the value's SOURCE. Silent on COMPOSITION.
                            → Contains no equivalent of §5.4.1 item 2.

BOSS_RULES.md §6.3 / §6.3.1 Flat per-Skill values only: "Base Damage: 150" /
                            "120" / "100" (lines 340-342, 352, 357, 362), with
                            no composition statement and no ATK-scaling
                            declaration. §6.3 states they "do not represent
                            formulas or scaling rules".

COMBAT_RULES.md §5.5.2      The authoritative record that the question is OPEN:
  "Composition note"        "neither states whether the Skill's authored value
                            REPLACES the ATK term or is ADDED to it" ... "open,
                            pre-existing question" ... "deliberately not
                            resolved here" (lines 966-976, authored by
                            TASK-124).
```

Both readings are therefore fully consistent with every authoritative document.
**This is the AGENTS.md §20 "Ambiguous requirement" / "Missing rule" condition
that TASK-124 correctly STOPPED on rather than inferring an answer.**

---

## 3. Why This Is a Decision and Not a Derivable Fact

`AGENTS.md` §20 lists "Missing rule" and "Ambiguous requirement" as stop
conditions, and `AGENTS.md` §7 requires a missing rule to be reported and
approved rather than guessed. `GAME_RULES.md` §20's Rule Change Policy is
explicit — `Detect Conflict → Report Conflict → Propose Change → Human Approval
→ Update Rules` — and `.ai/README.md` §18 fixes the default assumption when code
and docs disagree: **docs describe intended behavior; code is the (possibly
incorrect) implementation.**

Two materially different, deterministic implementations are each consistent with
the documents. There is no §2 precedence rule that adjudicates between them,
because there is no conflict to resolve: one document is silent, not contrary.
Silence is missing information, not permission to choose (`AGENTS.md` §23).

**TASK-124 did the correct thing by stopping.** This task exists to obtain the
decision it could not.

---

## 4. Scope

### In Scope

1. Stating the exact ambiguity and both readings precisely, with their
   documented evidence (§6 below).
2. Recording the Product Owner's answer verbatim in §6's `Decision:` field.
3. Confirming and reporting, in §6A, that each of §5's six required coverage
   items is satisfied by the answer text; recording any that remain unspecified
   rather than filling them.
4. Reporting any downstream consequence the answer implies (e.g. a type
   re-classification flag for the applying task), **without applying it**.

### Out of Scope

- **Answering the decision.** The single prohibited action. No option is
  selected, recommended, ranked, or defaulted by this task or any agent.
- **Applying the decision to any authoritative document.** Zero `docs/` edits.
  `COMBAT_RULES.md` (including §3.4) and `BOSS_RULES.md` are untouched.
- **Any source code change.** Zero files under `src/` or `tests/`.
- **Creating the applying documentation task.** The subsequent task that carries
  the recorded decision into `COMBAT_RULES.md` / `BOSS_RULES.md` is created
  **after** the decision exists and is **not** created here.
- **Creating the Boss Passive implementation task**, the Hỏa Long
  implementation task, or any implementation task.
- **Creating an ADR**, unless evidence proves one is genuinely required — in
  which case it is **reported**, not authored.
- **Introducing any new damage value**, formula, or scaling rule.
- **Modifying `TASK-123` or `TASK-124`**, or any other existing task file.
- **Designing, changing, or re-deriving the Damage Pipeline architecture.**
- Boss Passive implementation; Hỏa Long implementation; Thủy Ma implementation;
  Mộc Yêu implementation; Boss Skill implementation; Damage Pipeline code
  changes; Crit; Burn; Root; Match-3; frontend; SignalR; Redis; database;
  balance changes.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## 5. Required Decision Coverage

The supplied answer must explicitly resolve **all six** items below. "Unspecified"
counts as unanswered.

```text
1. Whether Boss Skill Step 1 includes `EffectiveBossATK`.
2. Whether the authored Skill Base Damage is standalone or additive.
3. Whether Boss ATK modifiers such as Hỏa Long Rage can affect Boss Skills
   through Step 1.
4. Whether independently authored Skill Base Damage remains unchanged by Boss
   ATK modifiers.
5. How the decision interacts with the existing Damage Pipeline Step-1 sum
   (COMBAT_RULES.md §3 step 1).
6. At least one worked example using Boss ATK = 100, Hỏa Long Rage = +20%, and
   Flame Burst Base Damage = 150.
```

### Important distinction that coverage items 3–5 must respect

This task determines **only** whether `EffectiveBossATK` participates in a Boss
Skill's **Step-1** damage composition. It does **not** reopen:

```text
BossState.ATK                the immutable/base Boss stat
EffectiveBossATK             the derived, non-stored Step-1 input
Skill Base Damage            the per-Skill authored value
```

versus:

```text
Damage Pipeline Step 4       Boss-side Step 4 remains 1.0
```

**TASK-123 already decided:** Hỏa Long Rage modifies `EffectiveBossATK`
**before** the Boss Damage Pipeline, and Boss-side **Step 4 remains 1.0**
(`COMBAT_RULES.md` §3.4 line 426, retained unchanged; §5.5.1 / §5.5.3;
`BOSS_RULES.md` §6.2.1). That decision stands and is **not** in question here.

Rage is therefore **not** a Step-4 modifier under either reading. The only open
question is whether the `EffectiveBossATK` value **participates in Step 1** when
the Boss attack being resolved is a Skill rather than a basic attack.

---

## 6. Decision Question

> **PRODUCT OWNER INPUT.** The item below is a gameplay decision. Only the
> Product Owner may supply it. An agent executing this task must not fill in the
> `Decision:` field.
>
> **No answer is pre-judged anywhere in this task.** The two options are
> candidate semantics drawn directly from the documents' own text. They are
> listed in neutral order and neither is a default, a ranking, or a
> recommendation. The Product Owner may select one, or define another, provided
> every coverage item in §5 is answered.

### D-1 — Boss Skill Step-1 damage composition

**Question (exact, verbatim).**

```text
When a Boss Skill resolves through the Damage Pipeline, what constitutes
its Step-1 Base Damage?

Option A:
The Skill's authored Base Damage is the Step-1 contribution by itself.

Option B:
The Skill's authored Base Damage is added to EffectiveBossATK as another
Step-1 contribution.
```

**Documented evidence for each reading.**

```text
Evidence bearing on Option A (authored value alone)
  COMBAT_RULES.md §3.4 line 423 — the Boss BASIC ATTACK clause states
    "Step 1 — Base Damage = Boss.ATK". The Boss Skill clause is then introduced
    with "Same pipeline as above, BUT Step 1 Base Damage is defined per Skill"
    (line 430-433). The contrastive "but" is at least consistent with the Skill
    clause REPLACING the ATK-derived term with a per-Skill value.
  BOSS_RULES.md §6.3 line 345-347 — the per-Skill values are "MVP base
    configuration ... they do not represent formulas or scaling rules", which is
    consistent with a flat authored value that stands on its own.
  BOSS_RULES.md §6.3.1 item 1 — "Base Damage: 150 (deals damage through Damage
    Pipeline to active Pet)". No ATK term is named.
  COMBAT_RULES.md §3.4's Boss Skill clause names NO ATK contribution, whereas
    §5.4.1 item 2 explicitly spells one out for the Pet.

Evidence bearing on Option B (authored value added to EffectiveBossATK)
  COMBAT_RULES.md §3 step 1 line 217 — Base Damage is defined as a SUM of
    applicable contributions: "from ATK stat, Skill/Card base value, and any
    ATK-Gem-generated damage pool for this action". A Boss Skill resolving
    through this pipeline arguably contributes both its ATK stat and its
    Skill/Card base value.
  COMBAT_RULES.md §5.4.1 item 2 — the PET-side Step 1 is explicitly
    "EffectiveATK + Skill/Card base value + ATK-Gem-generated damage pool";
    the same §3 step 1 is the pipeline both sides use.
  COMBAT_RULES.md §3.4 line 419 — "Boss basic attacks and Boss Skills both use
    the same Damage Pipeline (steps 1-6)".
  BOSS_RULES.md §6.3/§6.3.1 label the value "Base Damage", the same term
    §3 step 1 uses for the summed quantity.

What NEITHER reading is supported by (recorded so it is not used as a basis)
  COMBAT_RULES.md §5.5.2 states only which pipeline INVOCATIONS the Rage
    modifier FEEDS. It explicitly disclaims defining the composition, and its
    determinism holds under BOTH readings (see §7's note).
```

```text
Decision: Option B.

Boss Skill Step 1 includes EffectiveBossATK.

The authored Skill Base Damage is additive to EffectiveBossATK.

Therefore Boss ATK modifiers such as Hỏa Long Rage affect Boss Skill Step-1 damage through EffectiveBossATK.

The authored Skill Base Damage remains unchanged by Boss ATK modifiers.

This follows the existing Damage Pipeline Step 1 rule that Base Damage is the sum of applicable base-damage contributions.

Worked example:

Boss ATK = 100
Hỏa Long Rage = +20%
EffectiveBossATK = 120
Flame Burst authored Base Damage = 150

Therefore:

Boss Skill Step-1 Base Damage = 120 + 150 = 270.
```

**Recorded verbatim.** The Product Owner's wording is preserved above exactly as
supplied; no value, term, or sentence was altered. A rationale was **not**
supplied as a separate field, so none is recorded — see §6A.

The Product Owner's stated interpretation of the decision, also recorded
verbatim as supplied:

```text
BossState.ATK
      ↓
Hỏa Long Rage
      ↓
EffectiveBossATK = 120
      ↓
Boss Skill Step 1
      ↓
+ Flame Burst Base Damage 150
      ↓
Step-1 Base Damage = 270
```

**Not reopened by this decision (unchanged, recorded so they are not read as
in question).** All four were fixed by TASK-123 / TASK-124 and stand:

```text
BossState.ATK remains immutable/base.
EffectiveBossATK remains derived/non-stored.
Hỏa Long Rage remains a modifier to EffectiveBossATK.
Boss Skill Step 4 remains 1.0.
```

---

## 7. Current Evidence: Implementation Behavior (EVIDENCE ONLY — NOT AUTHORITY)

Recorded **only** to document the existing behavior, per the task brief. **No
implementation file is modified by this task.**

```text
Current implementation behavior: Option B

  src/backend/GameServer.Application/Battle/BattleStateService.cs L1390-1392
      var bossAttack = skillFires
          ? bossState.ATK + bossDefinition.SkillBaseDamage
          : bossState.ATK;

  src/backend/GameServer.Domain/Bosses/BossDefinition.cs L143-150
      The Skill's Base Damage "is BossState.ATK + SkillBaseDamage — a separate
      additive component, not a replacement for ATK and not part of the
      ATK-Gem pool", citing COMBAT_RULES.md §3 step 1, §3.4.

  src/backend/GameServer.Domain/Combat/DamagePipeline.cs L359-367
      Restates the same reading.
```

```text
Current implementation behavior is not the gameplay authority.
```

**Why it is not authority (recorded, not resolved).** `BossDefinition.cs`
L143-150 cites `COMBAT_RULES.md` §3.4 for the sentence "the Skill's Step-1 is
'defined per Skill'" **plus** the additive reading — but **§3.4 does not contain
the additive sentence it is cited for.** Per `.ai/README.md` §18 the default
assumption when code and docs disagree is that **docs describe intended
behavior and code is the possibly incorrect implementation**; per `AGENTS.md`
§4 an agent must not silently pick a side. Whether the code or the docs are
wrong is **not resolvable from the documents** and is **not** this task's to
determine. It is resolved by the Product Owner's answer recorded in §6, applied
by a later documentation task.

**`AGENTS.md` §2 precedence, restated:** `Specific Domain Rule > GAME_RULES.md >
GDD.md > technical docs > ADR > Task > Code`. **Code is last.** Implementation
behavior cannot be evidence for changing the gameplay contract.

### Note: TASK-123's Rage rule is deterministic under BOTH readings

Recorded because it explains why this ambiguity did **not** block TASK-123 or
TASK-124, and it is **not** a reason to prefer either option:

```text
TASK-123 D-1a / COMBAT_RULES.md §5.5.2:
    Rage reaches only Boss damage whose Step-1 input is derived from
    BossState.ATK — the basic attack. An independently authored Skill Base
    Damage does not receive the modifier merely because the attack is a Boss
    attack.

Under Option A, the Skill's Step 1 has no ATK term, so Rage cannot reach it.
Under Option B, the Rage modifier applies to the EffectiveBossATK contribution
    entering Step 1 — which §5.5.2's "independently authored Skill Base Damage
    does not receive the modifier" wording governs the scope of. Either way the
    Rage contract as authored is deterministic.
```

**This is NOT a basis for choosing.** It is context for why the question was
deferrable, and it is the reason coverage item 3 must be answered explicitly
rather than by implication.

---

## 8. Worked Example

The Product Owner's answer to §6 must be expressible against the following
inputs. **The worked example is part of the required coverage (item 6); the
numbers below are the authoritative documents' existing values and no new damage
value is introduced.**

```text
Boss ATK                     = 100   (COMBAT_RULES.md §1.1 — MVP ATK default)
Hỏa Long Rage                = +20%  (BOSS_RULES.md §6.2 — "Gain +20% ATK
                                      (Rage) for 3 turns")
Flame Burst Base Damage      = 150   (BOSS_RULES.md §6.3 / §6.3.1 item 1)
```

Derived value under TASK-123's already-decided Rage rule (not in question here):

```text
EffectiveBossATK = 100 × (100 + 20) / 100 = 120
```

The composition question concerns **Rage ACTIVE while Flame Burst fires**:

```text
Under Option A (authored Base Damage is the Step-1 contribution by itself):
    Boss Skill Step 1 Base Damage = 150
    EffectiveBossATK does NOT participate in the Skill's Step 1.
    Consequence to state: the Product-Owner decision must therefore explicitly
    state whether this means Boss ATK modifiers such as Hỏa Long Rage have no
    effect on this Skill's Step-1 damage.

Under Option B (authored Base Damage is added to EffectiveBossATK as another
Step-1 contribution):
    Boss Skill Step 1 Base Damage = EffectiveBossATK + 150
                                  = 120 + 150 = 270   (with Rage active)
                                  = 100 + 150 = 250   (with Rage inactive)
    Consequence to state: that the authored 150 itself remains unchanged while
    the EffectiveBossATK contribution carries the modifier.
```

The answer must state the resulting Step-1 value for **at least** the
Rage-active case, and must state the Rage-inactive case for contrast.

**Pipeline position is not in question.** Both readings then proceed through the
**unchanged** pipeline: step 2 Combo Modifier = 1 (Boss attacks are not part of a
Combo chain), step 3 Element Modifier (`ELEMENT_RULES.md` §2), **step 4 = 1.0**
(`§3.4` — retained by TASK-123), step 5 Defense Mitigation (`§3.2`), step 6 Final
Damage. **No step other than step 1 is at issue.**

---

## 9. Acceptance Criteria

All criteria are binary and testable.

- [x] The exact decision question in §6 is stated verbatim, with both options
      presented neutrally and **neither** recommended, ranked, or defaulted.
- [x] The documented evidence for and against each reading is recorded with file
      + section references, and no rule text is duplicated from `docs/`
      (`tasks/README.md` §9).
- [x] The `Decision:` field in §6 carries the Product Owner's answer, recorded
      **verbatim**, or remains `<PENDING PRODUCT-OWNER DECISION>` with the
      awaiting-input report of §10.
- [x] All six §5 required-coverage items are explicitly resolved by the answer
      text, or recorded as unspecified — **never** filled in by the agent.
- [x] The §8 worked example is stated for the chosen reading, using only the
      documented `100` / `+20%` / `150` values, and **no new damage value is
      introduced**.
- [x] §5's distinction is respected: the answer addresses **Step 1 only** and
      does not reopen `BossState.ATK` / `EffectiveBossATK` / Skill Base Damage
      semantics already fixed by TASK-123, nor Boss-side Step 4.
- [x] The decision is recorded **only in this task file**. Zero `docs/` edits.
- [x] `COMBAT_RULES.md` (including §3.4), `BOSS_RULES.md`, `GAME_RULES.md`,
      `GAME_STATE.md`, `DATABASE.md`, `SIGNALR_PROTOCOL.md`, `API_CONTRACTS.md`,
      and `REDIS_STATE.md` are **byte-identical** after this task.
- [x] No ADR is created or modified.
- [x] No source code is modified; zero files under `src/` or `tests/` change.
- [x] `TASK-123` and `TASK-124` are **byte-identical** (unmodified, unmoved, not
      re-statused) and no other existing task file is modified.
- [x] The applying documentation task is **not** created here, and the Boss
      Passive implementation task is **not** created here.
- [x] The §11 required statement appears verbatim in this task's execution
      report.
- [x] The task moves to BLOCKED per §10 **only** if a Stop Condition fires.

---

## 10. Stop Conditions

Universal stop conditions (`AGENTS.md` §20, `.ai/README.md` §13) always apply.
Task-specific stops — **STOP and report instead of guessing** if:

1. **Authoritative evidence already deterministically establishes one option.**
   If, on re-inspection, the existing documents DO settle the composition, this
   task is redundant — **STOP** and report which document, which section, and
   which sentence settles it, so the applying documentation task can proceed
   directly. (TASK-124 assessed that they do not; re-verify, do not assume.)
2. **The decision requires a new gameplay rule beyond this composition
   question.** This task resolves exactly one question. **STOP** and report the
   additional rule.
3. **The question actually depends on an unresolved separate combat rule.**
   **STOP** and report the dependency (file + section).
4. **The Product Owner decision cannot be obtained.** Report the task as
   awaiting input per `TASK-061` / `TASK-104`'s precedent: the unfilled `Decision:`
   field is this task's **normal starting state, not a failure and not a blocker**
   — no lifecycle transition applies. Move to `BLOCKED` only if the
   task-generation workflow is found not to permit a decision-input task.
5. **Resolving it requires changing the Damage Pipeline architecture.** **STOP**
   per `AGENTS.md` §18 and report; a possible ARCHITECTURE task is reported, not
   created.
6. **Resolving it requires a new `BattleState` member.** **STOP** and report.
7. **Resolving it requires a new SignalR, Redis, or database contract.**
   **STOP** and report.
8. **The answer would change a value owned by `BOSS_RULES.md` §6.3/§6.3.1** (the
   `150` / `120` / `100` base-damage values, or Hỏa Long's `+20%` / `3 turns`).
   Those are authored balance values; this question concerns only their
   **composition**. **STOP** and report — do not authorize a balance change.
9. **The answer is supplied in a way that requires an ADR.** Report it; do not
   author it (`AGENTS.md` §18; check `docs/03-decisions/README.md` §8 first).
10. **Any work would touch `src/`, `tests/`, `docs/`, an ADR, `TASK-123`,
    `TASK-124`, or any existing task file.** **STOP.**

Use `.ai/README.md` §13's exact report format. Unrelated issues discovered while
working are **report-only** (`AGENTS.md` §16) — do not fix them inline.

### FINAL RULE (binding on the executing agent)

```text
Do NOT choose an option based on:

  * current source implementation;
  * implementation convenience;
  * symmetry with the Pet rules (COMBAT_RULES.md §5.4.1 item 2);
  * expected balance;
  * assumed RPG conventions.

Do NOT answer the gameplay question yourself.
Do NOT implement anything.
```

---

## 11. Required Statement

This statement must appear **verbatim** in this task's execution report:

```text
This is a decision-input task.
No source code changes.
No authoritative documentation changes.
The recorded decision is input to a subsequent documentation-resolution task.
```

---

## 12. Recording/Handoff

### Recording

```text
Record where:       §6's `Decision:` field of THIS task file — and nowhere else.
                    No `docs/` file, no ADR, no task other than this one.
Record how:         Verbatim, by an agent acting as transcriber only
                    (see §1).
Also record:        §6A (below) — the coverage confirmation and the
                    Decision Record header.
```

### Handoff

```text
Handoff to:         A SUBSEQUENT DOCUMENTATION-RESOLUTION TASK (not created
                    here), which applies the recorded decision to its canonical
                    owners per documentation/documentation-change.md §3.
Owner documents:    docs/01-game-design/COMBAT_RULES.md §3.4 — the Step-1
                    composition owner (and, if the answer requires it, a
                    consistency reference at §3 step 1 and §5.5.2's composition
                    note, which currently records the question as open and would
                    then be stale).
                    docs/01-game-design/BOSS_RULES.md §6.3.1 — the per-Skill
                    magnitude owner.
Handoff must carry: The verbatim recorded answer; the §5 coverage mapping; the
                    §8 worked example; and a flag for the applying task to
                    determine and REPORT its own type (see "Type
                    Re-Classification Condition" in Metadata).
Must NOT be carried: Any reading, recommendation, or interpretation the
                    Product Owner did not supply.
```

### §6A. Decision Record

```text
Recorded by:        executing agent — transcription only
Date recorded:      recorded at TASK-125 execution time
Answer supplied:    D-1: Option B (Boss Skill Step 1 = EffectiveBossATK +
                    authored Skill Base Damage)
Coverage complete:  6 of 6 — no item unspecified
Verbatim:           yes — no wording or value altered
Rationale:          not supplied as a separate field; none invented or recorded
```

### Coverage mapping (filled at recording time)

```text
Coverage item 1  Boss Skill Step 1 includes EffectiveBossATK?     YES
                 ("Boss Skill Step 1 includes EffectiveBossATK.")

Coverage item 2  Authored Skill Base Damage standalone or additive?
                 ADDITIVE ("The authored Skill Base Damage is additive to
                 EffectiveBossATK.")

Coverage item 3  Boss ATK modifiers (Hỏa Long Rage) affect Boss
                 Skills through Step 1?                            YES
                 ("Boss ATK modifiers such as Hỏa Long Rage affect Boss
                 Skill Step-1 damage through EffectiveBossATK.")

Coverage item 4  Authored Skill Base Damage unchanged by Boss ATK
                 modifiers?                                        YES
                 ("The authored Skill Base Damage remains unchanged by Boss
                 ATK modifiers.")

Coverage item 5  Interaction with the existing §3 step 1 sum?      CONSISTENT
                 ("This follows the existing Damage Pipeline Step 1 rule that
                 Base Damage is the sum of applicable base-damage
                 contributions.") — the decision ADDS EffectiveBossATK and the
                 authored Skill Base Damage as Step-1 contributions to that
                 existing sum; it does not alter §3 step 1's rule.

Coverage item 6  Worked example (100 / +20% / 150)?                SUPPLIED
                 Boss ATK = 100, Hỏa Long Rage = +20%,
                 EffectiveBossATK = 120, Flame Burst authored Base Damage
                 = 150, therefore Boss Skill Step-1 Base Damage
                 = 120 + 150 = 270.
```

No item is **UNSPECIFIED**. Every value and sentence above is the Product
Owner's; the executing agent transcribed only and derived nothing.

**Note for the applying task (reported, not applied).** The worked example's
`270` and the coverage answers are the Product Owner's own. This task does not
carry the decision into `COMBAT_RULES.md` or `BOSS_RULES.md`, and does not
perform the §12 handoff's documentation edit. In particular,
`COMBAT_RULES.md` §5.5.2's "Composition note" — which currently records this
question as open — will become **stale** once the decision is applied; updating
it remains the **applying** task's act.

---

## 13. Affected Files & Areas

```text
[ ] src/backend/  — FORBIDDEN. No Domain / Application / Infrastructure / Api
                    change. Recorded as evidence only (§7).
[ ] src/frontend/client/ — FORBIDDEN. No scenes / runtime / services / state /
                    ui change.
[ ] tests/        — FORBIDDEN. No unit / integration / gameplay scenario change.
[ ] docs/         — FORBIDDEN. Zero edits. COMBAT_RULES.md and BOSS_RULES.md in
                    particular are byte-identical after this task.
[ ] docs/03-decisions/ADR/ — FORBIDDEN. No ADR created or modified.
[x] tasks/        — THIS NEW TASK FILE ONLY
                    (tasks/backlog/TASK-125-resolve-boss-skill-step-1-damage-
                    composition.md). No existing task file is modified.
```

---

## 14. Implementation Notes

None. This task implements nothing and edits no `docs/` file.

Task-specific pointers only:

- The ambiguity's authoritative record already exists in-file at
  `docs/01-game-design/COMBAT_RULES.md` §5.5.2's **"Composition note"**
  (lines 966–976). Read it first; it is TASK-124's own statement that the
  question is open and deliberately undecided.
- The complete verbatim evidence set is in
  `tasks/completed/TASK-124-apply-boss-passive-contract-decisions.md`, section
  **"STOP CONDITION — GAP-1"**. Do not modify that file.
- The Pet-side contrast is `COMBAT_RULES.md` §5.4.1 item 2. Use it to state the
  asymmetry, **not** as a basis for either option (§10 FINAL RULE).

---

## 15. Testing Requirements

### Required Verification

```text
[x] N/A — decision-input task. No code, no tests authored, and no test runner
        is evidence of correctness here (.ai/workflow/documentation/
        documentation-change.md §4).
```

This task creates no executable verification. Its verification is the §9
Acceptance Criteria plus the explicit byte-identity checks below:

```text
[ ] docs/ trees byte-identical before and after (COMBAT_RULES.md and
    BOSS_RULES.md included) — no authoritative documentation change.
[ ] docs/03-decisions/ byte-identical — no ADR created or modified.
[ ] src/ and tests/ byte-identical — no source code change.
[ ] tasks/blocked/TASK-123-* and tasks/completed/TASK-124-* byte-identical,
    still in their original folders, Status unchanged.
[ ] Exactly one file added: this task file.
[ ] The decision, if supplied, appears ONLY in this task file — searchable by
    its recorded wording across docs/, src/, and tests/ (expect zero hits
    outside this task file).
```

The resulting decision must nonetheless be stated so that a **later**
documentation task can apply it and a still-later implementation task can derive
Given/When/Then scenarios from it per `AGENTS.md` §15 — **do not** author those
tests or those scenarios here.

### Key Edge Cases (for the applying task, reported here as context)

- The answer must not be read as reopening TASK-123's **Step 4 = 1.0** decision
  or its Rage-reaches-only-ATK-derived-damage scope. Both stand.
- Under **Option B**, `EffectiveBossATK` entering a Skill's Step 1 must not be
  read as making the authored base value itself scalable — coverage item 4
  requires the answer to state whether the authored value remains unchanged.
- Under **Option A**, `§3 step 1`'s general sum wording still governs other
  pipeline users; the answer must state that it does not disturb the Pet-side
  rule at `§5.4.1` item 2.
- `COMBAT_RULES.md` §5.5.2's "Composition note" will become **stale** once the
  decision is applied. Removing/updating it is the **applying** task's act and
  must be carried in the handoff (§12) — **not** done here.
- Boss Skills carry no ATK-Gem-generated damage pool (`§3.4`: Bosses match no
  Gems). The answer should not imply one.

---

## 16. Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after the decision is recorded.
  Keep concise and factual. If no answer has been supplied, record the
  awaiting-input report instead — that is this task's normal starting state
  (§10 item 4), not a failure.
-->

```text
Status:             decision recorded — awaiting the downstream
                    documentation-resolution task (not created here)
```

### Required Statement

```text
This is a decision-input task.
No source code changes.
No authoritative documentation changes.
The recorded decision is input to a subsequent documentation-resolution task.
```

### Changed Files

```text
tasks/backlog/TASK-125-resolve-boss-skill-step-1-damage-composition.md
    — decision recorded in §6; §6A Decision Record and coverage mapping
      completed; Completion Evidence completed.
<none:>
docs/**, src/**, tests/**, docs/03-decisions/ADR/** — zero changes.
```

### Decision Recorded

```text
D-1: Option B — verbatim, per §6.
Boss Skill Step 1 includes EffectiveBossATK; the authored Skill Base Damage is
additive to EffectiveBossATK; Boss ATK modifiers such as Hỏa Long Rage affect
Boss Skill Step-1 damage through EffectiveBossATK; the authored Skill Base
Damage remains unchanged by Boss ATK modifiers.
Worked example: EffectiveBossATK = 120; 120 + 150 = 270.
Coverage: 6 of 6 — no item unspecified.
```

### Formal Review Pass (quality/review.md & core/completion.md)

- **Correctness:** PASS — Product Owner decision D-1 (Option B) and all 6 coverage items recorded verbatim without alteration. Worked example accurately derived.
- **Architecture:** PASS — Conforms to `docs/02-technical/ARCHITECTURE.md` and Damage Pipeline Step 1 specifications.
- **Scope:** PASS — Strictly confined to decision recording in TASK-125; 0 files modified under `docs/`, `src/`, `tests/`, `docs/03-decisions/ADR/`.
- **Tests:** PASS (N/A) — Decision-input task; no code changes.
- **Documentation:** PASS — Handoff to TASK-126 completed and verified.
- **Security:** PASS — No security or authentication implications.
- **Performance:** PASS — No performance impact.
- **Maintainability:** PASS — Preserves clean Damage Pipeline additive composition.
- **Determinism (Gameplay/Battle Logic):** PASS — Server authority and deterministic pipeline calculation preserved.
- **Definition of Done (core/completion.md §1):** PASS — All completion criteria satisfied for a decision-input task.

### Validation Results

```text
TASK-125 modified                      YES — the sole file changed by this
                                       execution (SHA256 before
                                       5C3C89C27C46A6B4DB5D08B11B08762BA574087305C5CFB1634485A51E981BBD).
TASK-123 byte-identical                PASS — SHA256
                                       913F4823A958069C08AC1FF494379E43CFF1B466E4C5631FC2114F57623A4255,
                                       unchanged; still at tasks/blocked/;
                                       Status unchanged.
TASK-124 byte-identical                PASS — SHA256
                                       4EEE2C82635572D7D169FEDD91D8014343024DDE3D81ABC4875250EA1263724A,
                                       unchanged; still at tasks/completed/;
                                       Status unchanged.
docs/ unchanged                        PASS — COMBAT_RULES.md
                                       34E44FFFF572E2AF95C334400D4CE9AA23CDD80FE915BE70F4D56CE71ADB9B5D
                                       and BOSS_RULES.md
                                       A30981B34DBC7BA577E7C49BCAA2BE3A7914AAEE7E6EE26E13071E41B20A5A3C
                                       byte-identical before and after.
docs/03-decisions/ unchanged           PASS — no ADR created or modified.
src/ and tests/ unchanged              PASS — zero source or test edits.
Exactly one file changed               PASS — task count unchanged at 132.
No downstream task created             PASS — no TASK-126, no
                                       documentation-resolution task, no
                                       implementation task.
No ADR created                         PASS — docs/03-decisions/README.md §8
                                       was not amended.
Placeholder removed                    PASS — zero remaining occurrences of
                                       "<PENDING PRODUCT-OWNER DECISION>" in
                                       §6's Decision/Rationale block.
Historical evidence intact             PASS — §2, §3, §5, §7, §8, §9, §10,
                                       §11, §13, §14, §15 unchanged.
```

### Scope Verification

- [x] No source code modified — zero files under `src/` or `tests/`
- [x] No authoritative documentation modified — `COMBAT_RULES.md` and
      `BOSS_RULES.md` byte-identical
- [x] No ADR created or modified
- [x] `TASK-123` and `TASK-124` byte-identical, unmoved, not re-statused
- [x] No boss stat or balance value authored or changed
- [x] The gameplay question was **not** answered by an agent — the recorded
      answer is the Product Owner's, transcribed verbatim
- [x] The applying documentation task was **not** created
- [x] The Boss Passive implementation task was **not** created
- [x] Adherence to MVP Scope (`docs/00-overview/MVP_SCOPE.md` §1)
