# TASK-126 — Apply Boss Skill Step-1 Damage Composition Contract

<!--
  GEN-TASK EXECUTION MANIFEST — DOCUMENTATION CONTRACT-RESOLUTION TASK
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, schemas, or contracts beyond the minimum needed to identify
  WHICH recorded decision is being applied and WHERE.

  THIS TASK DECIDES NOTHING. The single gameplay decision it applies was
  already made by the Product Owner and is recorded verbatim in
  tasks/backlog/TASK-125-resolve-boss-skill-step-1-damage-composition.md §6
  (D-1 = Option B, Coverage 6 of 6). This task is the repository's
  documentation CONTRACT-RESOLUTION step for that decision: it carries the
  recorded decision into its canonical owner documents so the Boss Skill
  Step-1 contract becomes consistent and canonical.

  THIS TASK IMPLEMENTS NOTHING. Zero files under src/ or tests/. No
  BattleStateService change, no DamagePipeline change, no BossDefinition
  change, no BossState change, no SignalR change, no Redis change, no
  database change, no frontend change. It is DOCUMENTATION-only.

  PROVENANCE: TASK-125 recorded the Product-Owner decision (Option B) and
  stated in its §12 "Recording/Handoff" that the applying task is a
  "SUBSEQUENT DOCUMENTATION-RESOLUTION TASK (not created here), which applies
  the recorded decision to its canonical owners per
  documentation/documentation-change.md §3", naming COMBAT_RULES.md §3.4 as
  the Step-1 composition owner and BOSS_RULES.md §6.3.1 as the per-Skill
  magnitude owner. This task is that task.

  The gap itself was surfaced by TASK-123's D-1a, recorded by TASK-124 as its
  one non-blocking STOP ("STOP CONDITION — GAP-1"), and resolved by TASK-125.
  TASK-124 deliberately left COMBAT_RULES.md §5.5.2 carrying an OPEN
  "Composition note" because the composition was then undecided. That wording
  is now STALE and §6 of this task requires it to be resolved — not merely
  appended to.

  BOUNDARY: documentation only. No source, no tests, no migration, no
  protocol change, no new state member, no new event, no new ADR unless this
  task's Stop Conditions prove one is genuinely required. This task does NOT
  create the implementation task that consumes the resolved contract.
-->

---

## Metadata

```text
Task ID:           TASK-126
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md). The deliverable is
                   the applied contract in its canonical owner document(s).
                   NOT GAMEPLAY-CHANGE: no gameplay rule is authored or
                   changed here — the composition was already decided by the
                   Product Owner (TASK-125 §6, Option B) and is only being
                   written into its owners. NOT ARCHITECTURE: no battle-state
                   concept, persistence strategy, realtime strategy, or module
                   boundary changes. See "Type classification note".
Status:            DONE — the TASK-125 Option B decision is applied at its
                   canonical owner (COMBAT_RULES.md §3.4), §5.5.2's stale GAP-1
                   open-composition wording is RESOLVED, and BOSS_RULES.md
                   §6.2.1's contradicting damage-scope bullet is reconciled.
                   See "Completion Evidence" below. The task file is moved
                   tasks/backlog/ → tasks/completed/ per TASK_LIFECYCLE.md §4
                   (IN REVIEW → DONE).
Risk:              MEDIUM (TASK_TYPES.md §4 — DOCUMENTATION baseline LOW–MEDIUM;
                   MEDIUM because the applied contract is cross-referenced by
                   COMBAT_RULES.md §3 step 1, §3.4, §5.4.1, §5.5.1, §5.5.2, and
                   BOSS_RULES.md §6.2.1/§6.3/§6.3.1, and because it resolves a
                   composition that the Boss Skill damage contract becomes
                   load-bearing on. LOW as an act: this task edits documentation
                   only and authors no value, formula, or rule.)
Priority:          HIGH (this is the sole remaining precondition between the
                   completed TASK-125 Product-Owner decision and any future
                   deterministic Boss Skill damage implementation. ROADMAP.md
                   Phase 1 requires "3 MVP Bosses (Hỏa Long, Thủy Ma, Mộc Yêu —
                   Passive + Skill each)" and "Boss Response (Passive → Skill →
                   Attack → Victory/Defeat)". The Skill half landed in TASK-118;
                   its Step-1 composition has now been decided and must be
                   authored before that half can be implemented or tested.)
Primary Agent:     review (TASK_TYPES.md §2 / AGENT_SELECTION.md §1 —
                   "Documentation change: Primary Agent Review")
Supporting Agents: gameplay (COMBAT_RULES.md §3/§5 and BOSS_RULES.md §6.2/§6.3
                   are the owning domain documents; consulted to place the
                   recorded decision at its owner accurately — NOT to author,
                   extend, or re-decide any rule)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-125 (DECISION RECORDED — Option B, the decision-input
                     source and this task's entire input. IMMUTABLE; READ-ONLY;
                     must NOT be modified, re-statused, moved, or rewritten),
                   TASK-124 (DONE — applied TASK-123's decision set and
                     recorded GAP-1 as its one non-blocking STOP, deliberately
                     leaving COMBAT_RULES.md §5.5.2's composition note OPEN.
                     Historical/preceding context; IMMUTABLE; read-only; its
                     decisions are NOT reopened by this task),
                   TASK-123 (BLOCKED — D-1a's damage-scope decision, which the
                     composition below must remain consistent with. Historical/
                     preceding context; IMMUTABLE; read-only; its decisions are
                     NOT reopened by this task)
Blocks:            The future Boss Skill Step-1 damage implementation task (not
                   created here), and through it ROADMAP.md Phase 1's complete
                   Boss Response sequence.
Estimate:          Simple (4 skills; apply one recorded decision across at most
                   two owner documents plus their stale cross-references; no
                   code, no tests, no migration)
```

**Type classification note.** `DOCUMENTATION`, not `GAMEPLAY-CHANGE` and not
`ARCHITECTURE`. `TASK_TYPES.md` §2 types a `GAMEPLAY-CHANGE` as a rule change
"propagat[ed] … through implementation and tests" — this task changes no rule
and touches no implementation. The gameplay decision was already made and
recorded by TASK-125; `TASK_TYPES.md` §3 selects the type by "*what the
deliverable is*", and here the deliverable is the applied contract in its
canonical owner documents. This mirrors the TASK-124 precedent, which applied
TASK-123's already-recorded decision set as `DOCUMENTATION`.

**Not a decision task.** The composition decision is already made and is
authoritative input (`TASK-125` §6, Option B; §6A Coverage 6 of 6). This task
must NOT reopen it, re-derive it, choose between the options again, or create
another decision task. If the recorded decision is found missing or internally
inconsistent, that is a Stop Condition (§10), not license to decide.

**Not a substitute for the implementation task, and it is not that task.**
Carrying the resolved contract into `BattleStateService`, `DamagePipeline`,
`BossDefinition`, or `BossState` is a SEPARATE, subsequently created
implementation task. **Do not create it here** (§9, §11).

**This task creates no ADR.** The composition question is a combat-rule
question inside the existing Damage Pipeline. It introduces no battle-state
concept, no persistence change, no realtime strategy change, and no
module-boundary change. Check `docs/03-decisions/README.md` §8 ("Known Open
Items (Not ADRs)") before asserting one is needed; if an ADR is genuinely
required, that is reported, not authored here (`AGENTS.md` §18).

---

## Objective

Apply the Product-Owner decision recorded in
`tasks/backlog/TASK-125-resolve-boss-skill-step-1-damage-composition.md` §6
(**Option B**) to its canonical owner documents, so that the Boss Skill Step-1
Base Damage composition exists as a single, consistent, deterministic,
canonically-owned contract — with the stale "open question" wording removed and
every dependent reference made consistent.

The task is complete when:

```text
TASK-125 recorded decision (Option B)
        ↓
Authoritative documentation resolution
        ↓
Deterministic Boss Skill Step-1 contract
        ↓
STOP
```

No source implementation is part of this task. The implementation task is
created later and is **not** created here.

---

## Authoritative References

### The decision input (READ ONLY — this task applies it, it does not re-decide it)

- `tasks/backlog/TASK-125-resolve-boss-skill-step-1-damage-composition.md` —
  **the entire input.** Its §6 `Decision:` field holds the verbatim, binding
  Product-Owner decision (D-1 = **Option B**); its §6A "Decision Record" and
  "Coverage mapping" hold the 6-of-6 coverage confirmation; its §8 holds the
  worked example. **IMMUTABLE — do not modify this file in any way.**
- `tasks/completed/TASK-124-apply-boss-passive-contract-decisions.md` —
  **historical/preceding context.** Its "GAP-1" / "STOP CONDITION — GAP-1"
  sections are the verbatim record of why the composition was left open, and
  its §5.5.2 composition-note edit is the stale wording this task resolves.
  **IMMUTABLE — do not modify this file in any way.**
- `tasks/blocked/TASK-123-resolve-boss-passive-effect-contract.md` — D-1a
  (Rage's damage scope) and the "Remaining Pre-Existing Documentation Gaps"
  entry GAP-1. **IMMUTABLE — do not modify this file in any way.**

### The canonical owners to be resolved (the owners)

- `docs/01-game-design/COMBAT_RULES.md` **§3.4** — Boss Damage, and the
  **canonical owner** of the Boss Skill Step-1 composition. Its Boss Skill
  clause currently states only that Step 1 Base Damage "is defined per Skill"
  — naming the value's **source**, never its **composition**. This is where the
  composition must become explicit.
- `docs/01-game-design/COMBAT_RULES.md` **§3 step 1** (line 217) — the
  Damage Pipeline's Step-1 definition: Base Damage is a **sum** of applicable
  contributions. The decision is consistent with this rule and **does not
  alter it**; any edit here is limited to a consistency reference.
- `docs/01-game-design/COMBAT_RULES.md` **§5.4.1 item 2** — the **Pet-side**
  shape (`Step 1 = EffectiveATK + Skill/Card base value + ATK-Gem-generated
  damage pool`), referenced for the mirrored shape only. **NOT restated.**
- `docs/01-game-design/COMBAT_RULES.md` **§5.5.1 / §5.5.2 / §5.5.3 / §5.5.4**
  — the **Boss-side** ATK modifier rule (TASK-124). §5.5.1 fixes the
  consumption point as the Step-1 `Attack` input via a derived, non-stored
  `EffectiveBossATK`. §5.5.2's **"Composition note"** currently records this
  exact question as **open** and "deliberately not resolved here" — **this is
  the stale GAP-1 wording this task must resolve (§6)**.
- `docs/01-game-design/BOSS_RULES.md` **§6.3** and **§6.3.1** — the MVP Boss
  Skill base configuration and per-Skill magnitudes (including Flame Burst's
  authored `150`). §6.3 states the values "are **MVP base configuration** …
  configuration defaults used at battle creation; they do not represent
  formulas or scaling rules". **Consistency verification owner.**
- `docs/01-game-design/BOSS_RULES.md` **§6.2.1** — Hỏa Long's Rage
  (`+20% ATK`, 3 turns) and its damage-scope statement referencing
  `COMBAT_RULES.md` §5.5. **Consistency verification.**

### Governance and process

- `.ai/workflow/documentation/documentation-change.md` §1 (flow), **§2 (no
  duplication, ever)**, §3 (determining the canonical owner), §4
  (composition — still goes through `quality/review.md`).
- `AGENTS.md` §2 (source-of-truth hierarchy), §4 (conflict resolution), §7
  (never invent a rule), §16 (task discipline), §17 (documentation change
  rule), §18 (ADR rule), §20 (stop conditions).
- `docs/AGENTS.md` §2 (owner hierarchy), §4, §7, §17, §20.
- `tasks/README.md` §6 (how to create a task), §9 (no business-rule duplication
  in task files), §12 (skill budget), `tasks/TASK_TYPES.md` §2 (DOCUMENTATION),
  §3 (type selection), §4 (risk), `tasks/TASK_LIFECYCLE.md` §3 (BACKLOG / READY
  semantics).
- `docs/00-overview/MVP_SCOPE.md` §1 (Bosses and Combat/Damage are IN), §2
  (OUT), §4 (unlisted is not implicitly IN).
- `docs/01-game-design/GAME_RULES.md` §14 (the conceptual damage pipeline) and
  §17 (the fixed resolution order — step 18b Boss Skill, step 18c Boss Attack).
- `docs/03-decisions/README.md` §8 — "Known Open Items (Not ADRs)". Check it
  before asserting an ADR is required.

### Explicitly NOT amended (verify, do not edit)

- `docs/01-game-design/COMBAT_RULES.md` §3.4's **Boss Basic Attack** clause
  (`Step 1 — Base Damage = Boss.ATK`) and its retained **Boss-side Step 4 =
  1.0** — both fixed by TASK-123 and **not** in question.
- `docs/01-game-design/COMBAT_RULES.md` §5.4 and §5.4.5 — the Pet-side rule.
- `docs/01-game-design/COMBAT_RULES.md` §5.3 (DR1–DR6) — duration consumption.
- `docs/01-game-design/GAME_RULES.md` §16 — the closed canonical event list.
- `docs/02-technical/GAME_STATE.md` — no new state member (verified: no
  `EffectiveBossATK` member exists or is added).
- `docs/02-technical/SIGNALR_PROTOCOL.md`, `API_CONTRACTS.md`,
  `REDIS_STATE.md`, `DATABASE.md`, `ARCHITECTURE.md` — **no edit** (verified:
  no technical document references the composition; the Step-1 value already
  reaches the client as `DamageCalculated.base`).
- `docs/03-decisions/README.md` and `docs/03-decisions/ADR/` — **no edit; no
  ADR.**
- `src/**`, `tests/**` — **FORBIDDEN.**
- `tasks/backlog/TASK-125-*`, `tasks/completed/TASK-124-*`,
  `tasks/blocked/TASK-123-*`, and every other existing task file —
  **IMMUTABLE.**

---

## The Decision Being Applied (already decided — not open for reconsideration)

Every item below is the Product Owner's recorded decision
(`TASK-125` §6, transcribed verbatim there). This task writes it at its owners.
Nothing here is open for reconsideration.

```text
Decision: Option B — Boss Skill Step 1 includes EffectiveBossATK.

Boss Skill Step 1 includes EffectiveBossATK.

The authored Skill Base Damage is additive to EffectiveBossATK.

Therefore Boss ATK modifiers such as Hỏa Long Rage affect Boss Skill Step-1
damage through EffectiveBossATK.

The authored Skill Base Damage remains unchanged by Boss ATK modifiers.

This follows the existing Damage Pipeline Step 1 rule that Base Damage is the
sum of applicable base-damage contributions.

Worked example:

Boss ATK = 100
Hỏa Long Rage = +20%
EffectiveBossATK = 120
Flame Burst authored Base Damage = 150

Therefore:

Boss Skill Step-1 Base Damage = 120 + 150 = 270.
```

Canonical contract to make deterministic in documentation:

```text
BossState.ATK
    ↓
Boss ATK modifiers (e.g. Hỏa Long Rage +20%)
    ↓
EffectiveBossATK            derived at attack resolution — NOT stored
    ↓
Boss Skill Step 1
    ↓
+ authored Skill Base Damage
    ↓
Damage Pipeline Step 1 (the existing sum of applicable contributions)
```

**Not reopened by this decision (unchanged; recorded so they are not read as
in question).** All four were fixed by TASK-123 / TASK-124 and stand:

```text
BossState.ATK remains immutable/base.
EffectiveBossATK remains derived, non-stored, and NOT a new state field.
Hỏa Long Rage remains a modifier to EffectiveBossATK.
Boss-side Step 4 remains 1.0.
```

---

## Scope

### In Scope

1. **Author the Boss Skill Step-1 composition at its canonical owner**
   (`COMBAT_RULES.md` §3.4), stating that Boss Skill Step 1 includes
   `EffectiveBossATK` and that the authored Skill Base Damage is **additive**
   to it as applicable Step-1 Base Damage contributions under the existing
   §3 step 1 sum rule.
2. **Resolve the stale GAP-1 wording** at `COMBAT_RULES.md` §5.5.2's
   "Composition note", so the document no longer describes this composition as
   an open, unresolved question. Remove **or** rewrite it (per §6) — do not
   merely append the decision while leaving contradictory "open question"
   wording active.
3. **State the modifier reach consistently**: Boss ATK modifiers such as Hỏa
   Long Rage affect Boss Skill Step-1 damage **through `EffectiveBossATK`**,
   while the authored Skill Base Damage remains unchanged by Boss ATK
   modifiers.
4. **Add the worked example** so the documented outcome is deterministic
   (`Boss ATK = 100`, `Hỏa Long Rage = +20%`, `EffectiveBossATK = 120`,
   `Flame Burst` authored Base Damage `= 150`, Step-1 Base Damage `= 270`).
   The numbers are the authoritative documents' existing values
   (`COMBAT_RULES.md` §1.1's MVP ATK default; `BOSS_RULES.md` §6.2's `+20%`;
   `BOSS_RULES.md` §6.3/§6.3.1's `150`) — **no new damage value is
   introduced**.
5. **Verify `BOSS_RULES.md` §6.3/§6.3.1 consistency** (`§7` below):
   `Flame Burst = 150` remains the Skill's **authored Base Damage** and must
   **not** be rewritten to `270`.
6. **Update dependent references only where wording is now stale** — e.g. the
   `COMBAT_RULES.md` header version-history's "Reported open" line and any
   §5.5.2/§5.5.3 cross-reference whose wording asserts the question is
   undecided. Do not duplicate the rule into referencing sections
   (`documentation-change.md` §2).
7. **Validate** that no rule is duplicated across owners, that every
   cross-reference points at the actual canonical owner, that the §3 step 1
   sum rule is intact, and that no new event/wire member/state member/Redis
   key/schema change was introduced.
8. **Report** the disposition per §9 and §11.

### Out of Scope

- **Answering or re-deciding the composition.** It is already decided
  (Option B). Do not choose between Option A and Option B again, and do not
  create another decision task.
- **Any source code change.** Zero files under `src/` or `tests/`. No
  `BattleStateService`, `DamagePipeline`, `BossDefinition`, or `BossState`
  change; no SignalR code, no Redis code, no database migration, no API, no
  frontend, no Phaser.
- **Implementing the resolved contract.** The implementation task is created
  later and is **not** created here (§11).
- **Reopening `TASK-123`, `TASK-124`, or `TASK-125` decisions.** In
  particular preserve `BossState.ATK = immutable/base`,
  `EffectiveBossATK = derived`, `Hỏa Long Rage = modifier to
  EffectiveBossATK`, and Boss-side `Step 4 = 1.0`.
- **Introducing a new persisted state field.** Explicitly forbidden:
  `BossEffectiveATK`, `EffectiveBossSkillATK`, `SkillAttackStat`,
  `BossSkillATK`, or any equivalent.
- **Introducing a new `StatusEffect` representation**, a new Battle Event, a
  new SignalR method/event/payload member, a new Redis key, or a database
  field/table/column.
- **Any gameplay redesign.** No Boss combat redesign, Boss balance redesign,
  Boss Skill redesign, damage formula redesign, Crit redesign, Burn redesign,
  or Boss passive redesign. The only gameplay contract being resolved is
  **Boss Skill Step-1 Base Damage composition**.
- **Altering authored balance values** — `Flame Burst = 150`, `Drain Power =
  120`, `Root = 100`, Hỏa Long's `+20%` / `3 turns`, or any `BOSS_RULES.md`
  §6.3/§6.3.1 magnitude. Those are authored values; this task concerns only
  their composition.
- **Creating an ADR** unless a Stop Condition proves one is genuinely
  required (see Metadata).
- Match-3; board; gems; swap; cascade; critical hit; burn; Boss passive
  behavior; Boss Response ordering.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State

The composition is **decided but not present in any `docs/` file**. TASK-125
changed no document and said so explicitly; it recorded the decision in its own
task file only. Verified in the working tree:

```text
COMBAT_RULES.md §3.4 (lines 430-433)
    The Boss Skill clause states only: "Same pipeline as above, but Step 1
    Base Damage is defined per Skill (BOSS_RULES.md §6)." It names the value's
    SOURCE. It is silent on COMPOSITION and contains no equivalent of §5.4.1
    item 2's Pet-side sentence.
    → The decided composition is NOT authored here.

COMBAT_RULES.md §5.5.2 "Composition note" (lines 966-976)
    States verbatim that §3.4 and §3 step 1 "neither state[] whether the
    Skill's authored value REPLACES the ATK term or is ADDED to it", that this
    composition "is an open, pre-existing question owned by §3.4 /
    BOSS_RULES.md §6.3.1", and that it is "deliberately not resolved here."
    → STALE. This is the GAP-1 wording TASK-124 deliberately left open because
      TASK-125 had not yet been decided. It must be resolved by this task.

COMBAT_RULES.md §5.5.2 "Does NOT reach" rows (lines 953-958)
    State a Boss SKILL "whose Step 1 Base Damage is independently authored"
    does NOT receive the modifier, and does not apply "unless the Skill's own
    contract explicitly declares that its Base Damage scales from Boss ATK".
    → Requires consistency review against the resolved composition: the
      modifier now reaches Boss Skill Step-1 damage through the
      EffectiveBossATK contribution, while the authored Skill Base Damage
      itself remains unmodified. Reword only as needed; do not restate §3.4.

COMBAT_RULES.md header version history (lines 26-27)
    Records "Reported open: the Boss Skill Step-1 composition question
    (§3.4 × BOSS_RULES.md §6.3.1) remains unresolved and is not authored
    here." → STALE once the composition is authored. Follow the file's own
    version-history convention; do not rewrite prior-version entries.

BOSS_RULES.md §6.3 / §6.3.1 (lines 337-352)
    Flat per-Skill values and "Base Damage: 150" for Flame Burst, with §6.3
    stating they "do not represent formulas or scaling rules".
    → CONSISTENT with the resolved composition. The authored value remains
      the Skill's authored Base Damage; it is NOT rewritten to 270. Verify and
      reference the owner; do not restate the composition here.

BOSS_RULES.md §6.2.1 (lines 229-234)
    States Rage's damage scope by reference to COMBAT_RULES.md §5.5.
    → Verify only; the referenced owner is what changes.

GAME_RULES.md §17 step 18b / 18c and §14
    Conceptual pipeline and resolution order only — no Step-1 composition
    statement. → NO CHANGE EXPECTED.

docs/02-technical/** (GAME_STATE.md, SIGNALR_PROTOCOL.md, API_CONTRACTS.md,
REDIS_STATE.md, DATABASE.md, ARCHITECTURE.md)
    Verified: no document references a Boss Skill Step-1 composition and no
    document contains `EffectiveBossATK`. The Step-1 value already reaches the
    client as `DamageCalculated.base` (SIGNALR_PROTOCOL.md §3.2.13).
    → NO CHANGE EXPECTED (value change, not a member-set change).
```

**What this task does NOT change.** `§3.4`'s Boss Basic Attack clause, its
retained Boss-side `Step 4 = 1.0`, `§3.1`'s six-step order, `§5.3`'s DR1–DR6,
`§5.4`/§5.4.5`'s Pet-side rule, `§5.5`'s modifier consumption point and its
derived/non-stored `EffectiveBossATK`, `GAME_RULES.md` §16's closed event list
and §17's resolution order, the `BOSS_RULES.md` §6.2/§6.3/§6.3.1 balance
values, and every TASK-123/TASK-124/TASK-125 decision.

---

## Acceptance Criteria

All criteria are binary and testable.

- [ ] TASK-125's Option B decision is treated as authoritative input and is
      **not** reopened, re-derived, re-ranked, or replaced; no new decision
      task is created.
- [ ] `COMBAT_RULES.md` explicitly states that Boss Skill Step 1 includes
      `EffectiveBossATK`.
- [ ] `COMBAT_RULES.md` explicitly states that the authored Skill Base Damage
      is **additive** to `EffectiveBossATK`.
- [ ] `COMBAT_RULES.md` explicitly states that Boss ATK modifiers such as Hỏa
      Long Rage affect Boss Skill Step-1 damage **through `EffectiveBossATK`**.
- [ ] `COMBAT_RULES.md` explicitly states that the authored Skill Base Damage
      remains **unchanged** by Boss ATK modifiers.
- [ ] The existing Damage Pipeline Step-1 sum rule (`COMBAT_RULES.md` §3 step
      1) remains intact and unaltered; the decision is recorded as consistent
      with it, adding `EffectiveBossATK` and the authored Skill Base Damage as
      applicable contributions to that existing sum.
- [ ] The previous GAP-1 / open composition wording at `COMBAT_RULES.md`
      §5.5.2's "Composition note" is **removed or rewritten** so it no longer
      describes this composition as an open, unresolved question.
- [ ] No contradictory "open question" / "deliberately not resolved" wording
      for this composition survives anywhere in `docs/`.
- [ ] The `COMBAT_RULES.md` header version history's stale "Reported open"
      line for this question is corrected per that file's own convention, with
      prior-version entries preserved intact.
- [ ] `BOSS_RULES.md` §6.3 / §6.3.1 remains consistent with the resolved
      composition (verified, and referenced rather than restated).
- [ ] Flame Burst's **authored** Base Damage remains `150` in `BOSS_RULES.md`
      §6.3/§6.3.1; it is **not** rewritten to `270`.
- [ ] The distinction is explicit and preserved: authored Skill Base Damage
      `= 150`; runtime `EffectiveBossATK = 120`; runtime Step-1 Base Damage
      `= 270`.
- [ ] The worked example deterministically produces `EffectiveBossATK = 120`
      and Boss Skill Step-1 Base Damage `= 270` (from `Boss ATK = 100`,
      Hỏa Long Rage `= +20%`, Flame Burst authored Base Damage `= 150`), using
      **only** values the authoritative documents already carry.
- [ ] `BossState.ATK` remains documented as an immutable/base value.
- [ ] `EffectiveBossATK` remains documented as a **derived** value and is
      **not** introduced as a persisted `BattleState`/`BossState` field.
- [ ] No new persisted state field is introduced (`BossEffectiveATK`,
      `EffectiveBossSkillATK`, `SkillAttackStat`, `BossSkillATK`, or
      equivalent — zero occurrences).
- [ ] No new `StatusEffect` representation is introduced.
- [ ] No new Battle Event is introduced (`GAME_RULES.md` §16 and
      `GAME_EVENTS.md` §2 unchanged).
- [ ] No new SignalR method, event, or payload member is introduced
      (`SIGNALR_PROTOCOL.md` unchanged).
- [ ] No new Redis key is introduced (`REDIS_STATE.md` unchanged).
- [ ] No database table, column, or schema change is introduced
      (`DATABASE.md` unchanged).
- [ ] No new REST API endpoint or contract is introduced (`API_CONTRACTS.md`
      unchanged).
- [ ] No source code is modified; zero files under `src/` or `tests/` change.
- [ ] No gameplay system outside Boss Skill Step-1 composition is changed —
      in particular `BossState.ATK` semantics, `EffectiveBossATK` derivation,
      Hỏa Long Rage's scope, and Boss-side `Step 4 = 1.0` are preserved.
- [ ] `TASK-123`, `TASK-124`, and `TASK-125` are **byte-identical**
      (unmodified, unmoved, not re-statused) and no other existing task file is
      modified.
- [ ] The Boss Skill Step-1 **implementation** task is **not** created here.
- [ ] No ADR is created or modified.
- [ ] No rule is duplicated across documents; every cross-reference points at
      the actual canonical owner (`documentation-change.md` §2).
- [ ] Documentation validation passes at the depth `quality/review.md`
      requires for a documentation change (`core/validation.md` §2).
- [ ] Quality review checklist passes (`quality/review.md` §1, documentation
      items).

---

## Affected Files & Areas

```text
[ ] src/backend/  — FORBIDDEN. No Domain / Application / Infrastructure / Api
                    change. Explicitly: no BattleStateService, no
                    DamagePipeline, no BossDefinition, no BossState change.
[ ] src/frontend/client/ — FORBIDDEN. No scenes / runtime / services / state /
                    ui change.
[ ] tests/        — FORBIDDEN. No unit / integration / gameplay scenario
                    change. The resolved contract must be stated so a LATER
                    implementation task can derive Given/When/Then scenarios
                    from it (AGENTS.md §15) — do not author them here.
[ ] docs/03-decisions/ADR/ — FORBIDDEN. No ADR created or modified.
[x] docs/         — DOCUMENTATION CHANGE — this task's primary output.
      docs/01-game-design/COMBAT_RULES.md
        §3.4          — AUTHOR the Boss Skill Step-1 composition (the decided
                        contract): Step 1 includes EffectiveBossATK; the
                        authored Skill Base Damage is additive to it; the
                        modifier reaches it through EffectiveBossATK; the
                        authored value is unchanged by the modifier; the
                        worked example. Reference §3 step 1's sum rule and
                        §5.4.1 item 2's Pet-side shape rather than restating
                        them.
        §5.5.2        — RESOLVE the stale "Composition note" (GAP-1) so the
                        composition is no longer described as open; reconcile
                        the "Does NOT reach" wording with the resolved
                        composition without restating §3.4.
        header        — correct the stale "Reported open" version-history line
                        per the file's own convention; preserve prior entries.
        §3 step 1, §3.1, §5.3, §5.4, §5.4.5, §5.5.1, §5.5.3, §5.5.4
                      — VERIFY ONLY. §3 step 1's sum rule must remain intact;
                        §3.4's Boss Basic Attack clause and Boss-side
                        Step 4 = 1.0 must remain unchanged.
      docs/01-game-design/BOSS_RULES.md
        §6.3 / §6.3.1 — VERIFY consistency (Flame Burst's authored Base Damage
                        remains 150; the composition is not restated here).
                        Edit only if a stale or contradictory statement is
                        actually found — report it if so.
        §6.2.1        — VERIFY ONLY (Rage scope references COMBAT_RULES.md
                        §5.5).
      docs/01-game-design/GAME_RULES.md — NO CHANGE EXPECTED. Verify only.
[ ] docs/02-technical/** — NO CHANGE EXPECTED. Verify only: no composition
                    reference and no `EffectiveBossATK` member exists; the
                    Step-1 value already reaches the client as
                    `DamageCalculated.base`.
[x] tasks/        — THIS NEW TASK FILE ONLY
                    (tasks/backlog/TASK-126-apply-boss-skill-step-1-damage-
                    composition-contract.md). No existing task file modified.
```

---

## Documentation Ownership

Each concept has exactly ONE canonical owner. This mapping follows TASK-125's
own §12 handoff and this task executes it rather than re-deriving it.

```text
CONCEPT                            CANONICAL OWNER          WHAT THIS TASK DOES
---------------------------------  -----------------------  ------------------
Combat resolution mechanics        COMBAT_RULES.md §3.4     AUTHOR the Boss Skill
  Boss Skill Step-1 composition                              Step-1 composition:
  (EffectiveBossATK + authored                               both contributions,
  Skill Base Damage)                                         additive, with the
                                                             worked example

Combat resolution mechanics        COMBAT_RULES.md §3       REFERENCE the existing
  Base Damage is a sum of            step 1                   sum rule; do not alter
  applicable contributions                                    or restate it

Combat resolution mechanics        COMBAT_RULES.md §5.5     RESOLVE the stale
  which pipeline invocations         (§5.5.1, §5.5.2,        composition note; keep
  the Boss ATK modifier feeds         §5.5.3)                 the modifier's
  (derived, non-stored                                        consumption point and
  EffectiveBossATK)                                           derived status intact

Boss-specific Skill magnitudes     BOSS_RULES.md            VERIFY consistency and
  and per-Skill authored values      §6.3 / §6.3.1            reference the owner;
  (Flame Burst = 150)                                        do NOT restate the
                                                             composition and do NOT
                                                             rewrite 150 to 270

Battle execution order             GAME_RULES.md §17        NO CHANGE — verify the
                                                             step order is intact

Runtime state representation       GAME_STATE.md            NO CHANGE — verify no
                                                             new member is
                                                             introduced and
                                                             EffectiveBossATK stays
                                                             derived/non-stored
```

**Duplication rule (`documentation-change.md` §2).** The composition is written
**once**, at `COMBAT_RULES.md` §3.4. Referencing sections **point at** the owner
and do not restate it. `BOSS_RULES.md` owns per-Skill magnitudes and does not
restate combat composition; technical documents reference gameplay owners rather
than redefining gameplay values.

---

## Implementation Notes

Documentation pointers only — this task implements nothing.

- The authoritative record that the question **was** open already exists
  in-file at `docs/01-game-design/COMBAT_RULES.md` §5.5.2's **"Composition
  note"** (lines 966–976). Read it first; resolving it is §6's requirement.
- The verbatim recorded decision is
  `tasks/backlog/TASK-125-resolve-boss-skill-step-1-damage-composition.md` §6,
  with its §6A coverage mapping and §8 worked example. **Do not modify that
  file.**
- The complete historical evidence set is in
  `tasks/completed/TASK-124-apply-boss-passive-contract-decisions.md`
  ("STOP CONDITION — GAP-1", "GAP-1 — Boss Skill Step-1 Composition: REMAINS
  UNRESOLVED"). **Do not modify that file.**
- The Pet-side shape the composition mirrors is `COMBAT_RULES.md` §5.4.1
  item 2 — reference it; do not restate it.
- `BOSS_RULES.md` §6.3.1 item 1 carries the authored `150`. It does **not**
  become `270`; `270` is the runtime Step-1 result under the worked example's
  inputs.
- Follow each changed file's own header version-history convention and
  preserve the prior-version chain intact.
- Current source behavior is **not** the gameplay authority
  (`AGENTS.md` §2: code is last). This task does not need to read `src/`, and
  must not modify it.

---

## Testing Requirements

### Required Verification

```text
[x] N/A — documentation task. No code, no tests authored, and no test runner
        is evidence of correctness here (.ai/workflow/documentation/
        documentation-change.md §4).
```

This task creates no executable verification. Its verification is the
Acceptance Criteria above plus the documentation validation below.

```text
[ ] Search for the stale open-question wording for this composition across
    docs/ — expect ZERO surviving occurrences that describe it as open,
    unresolved, deliberately undecided, or "not authored here".

[ ] Search for contradictory Boss Skill Step-1 composition definitions —
    exactly ONE definition, at COMBAT_RULES.md §3.4; referencing sections
    point at it.

[ ] Verify §3 step 1's sum rule is intact (unchanged wording for the Base
    Damage sum).

[ ] Verify all cross-references point to the actual canonical owner (no
    reference cites a section that does not carry the rule; no reference cites
    the retired open-question wording).

[ ] Verify no new Battle Event names were introduced (GAME_RULES.md §16
    unchanged; GAME_EVENTS.md §2 unchanged).

[ ] Verify no SignalR payload expansion was introduced (SIGNALR_PROTOCOL.md
    unchanged; no `EffectiveBossATK` or equivalent member anywhere in docs/).

[ ] Verify no new BattleState/BossState field was introduced
    (GAME_STATE.md §2.3.1 member set and §2.4 tree unchanged).

[ ] Verify no Redis key / database / API contract change was introduced
    (REDIS_STATE.md, DATABASE.md, API_CONTRACTS.md unchanged).

[ ] Verify no source files were modified (scoped diff or aggregate hash over
    src/ and tests/ — identical before and after).

[ ] Verify TASK-123, TASK-124, and TASK-125 remain byte-identical and in their
    original folders with unchanged Status.

[ ] Re-read each changed owner together with every document that references
    it, confirming no duplicate definition was accidentally introduced
    (documentation-change.md §1's final step).

[ ] Version notes: where a changed file carries a header version-history
    convention (COMBAT_RULES.md does), follow that convention and preserve the
    prior-version chain intact.
```

The resolved contract must nonetheless be stated so that a **later**
implementation task can derive Given/When/Then scenarios from it per
`AGENTS.md` §15 — **do not** author those tests or scenarios here.

### Key Edge Cases

- `BOSS_RULES.md` §6.3.1's `150` is the **authored** Skill Base Damage. The
  worked example's `270` is a **runtime** result under stated inputs. The two
  must never be conflated, and `150` must not be rewritten.
- The composition must **not** be read as making the authored Skill Base
  Damage itself scalable: coverage item 4 requires the authored value to
  remain unchanged by Boss ATK modifiers.
- Boss-side `Step 4 = 1.0` (`§3.4`, retained by TASK-123) is **not** reopened;
  the modifier is a Step-1 input, not a Step-4 factor.
- `EffectiveBossATK` must remain derived and non-stored. It is **not** a
  `BattleState`/`BossState` field and must not become one.
- Boss Skills carry no ATK-Gem-generated damage pool (`§3.4`: Bosses match no
  Gems). The documentation must not imply one.
- The Rage-inactive contrast case (`100 + 150 = 250`) should remain derivable
  from the documented composition and need not be authored as a second
  example.
- `COMBAT_RULES.md` §5.5.2's "Does NOT reach" rows must stay true under the
  resolved composition: the **authored** Skill Base Damage still does not
  receive the modifier; what changes is that the modifier now reaches the
  Skill's Step-1 damage through the `EffectiveBossATK` contribution.

---

## Stop Conditions

Universal stop conditions (`AGENTS.md` §20, `.ai/README.md` §13) always apply.
Task-specific stops — **STOP and report instead of guessing** if:

1. **TASK-125's recorded decision is missing or inconsistent.** If the
   recorded Option B decision, its coverage mapping, or its worked example are
   absent, contradictory, or incomplete, **STOP** and report — do not supply,
   complete, or infer the decision.
2. **`COMBAT_RULES.md` and `BOSS_RULES.md` contain another unresolved conflict
   affecting this exact composition.** Report both sources (file + section)
   per `AGENTS.md` §4. Do not resolve it silently.
3. **Applying Option B requires a new gameplay decision.** **STOP** and report
   the additional rule — this task resolves exactly one composition question.
4. **Applying Option B requires a new architecture decision.** **STOP** per
   `AGENTS.md` §18 and report; a possible ADR/ARCHITECTURE task is reported,
   not created.
5. **The required authoritative owner cannot be determined.** If
   `documentation-change.md` §3 does not yield a single unambiguous canonical
   owner for the composition, **STOP** and report the structural ambiguity.
6. **A proposed documentation change would alter a TASK-123/TASK-124
   decision.** In particular anything that would reopen `BossState.ATK =
   immutable/base`, `EffectiveBossATK = derived`, `Hỏa Long Rage = modifier to
   EffectiveBossATK`, or Boss-side `Step 4 = 1.0`. **STOP** and report.
7. **The resulting contract would require a new state field, event, API,
   SignalR method, Redis key, or database field.** **STOP** and report.
8. **`BOSS_RULES.md` §6.3/§6.3.1 would have to change a value** (the `150` /
   `120` / `100` authored base damages, or Hỏa Long's `+20%` / `3 turns`).
   Those are authored balance values; this task concerns only their
   composition. **STOP** — do not authorize a balance change.
9. **An ADR is genuinely required.** Check `docs/03-decisions/README.md` §8
   first; multiple documents changing is not by itself sufficient reason.
   Report it; do not author it.
10. **Any work would touch `src/`, `tests/`, an ADR, `TASK-125`, `TASK-124`,
    `TASK-123`, or any existing task file.** **STOP.**
11. **The task drifts into creating the implementation task.** **STOP** — the
    implementation task is a later, separate step (§11).

Use `.ai/README.md` §13's exact report format. Unrelated issues discovered
while editing are **report-only** (`AGENTS.md` §16) — do not fix them inline.

---

## Required Statement

This statement must appear **verbatim** in this task's execution report:

```text
Documentation only.
No source code.
No tests requiring source changes.
No gameplay implementation.
No new event.
No new SignalR member.
No new BattleState member.
No new Redis key.
No PostgreSQL schema change.
```

---

## §9 — Completion Evidence Requirements

The execution report must show:

```text
1. TASK-125 was read and Option B was used, verbatim as recorded.
2. The exact authoritative documentation sections changed.
3. The old GAP-1 open-composition wording was resolved (removed or rewritten),
   with no contradictory wording surviving.
4. The Boss Skill Step-1 formula is deterministic as documented.
5. The Flame Burst worked example:
       Boss ATK 100
       +20% Rage
       = EffectiveBossATK 120
       + 150 authored Skill Base Damage
       = 270 Step-1 Base Damage
6. No source code changed.
7. No SignalR / API / Redis / database contract changed.
8. No unrelated gameplay rules changed.
```

### Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Changed Files

- `docs/01-game-design/COMBAT_RULES.md` — §3.4 AUTHORED the **Boss Skill
  Step-1 composition** (new subsection directly after §3.4's pipeline block):
  Step 1 Base Damage is the sum of its applicable Step-1 contributions under
  §3 step 1 — `EffectiveBossATK` **and** the Skill's authored Base Damage —
  with the flow diagram, the consequences (modifier reaches Step-1 damage
  through `EffectiveBossATK`; the authored value is unchanged; Boss-side
  counterpart of §5.4.1 item 2; no ATK-Gem pool; Step 4 unaffected), and the
  worked example (`120 + 150 = 270`, and `100 + 150 = 250` with Rage
  inactive).
- `docs/01-game-design/COMBAT_RULES.md` — §5.5.2: **GAP-1 RESOLVED.** The
  "Composition note" that described the composition as "an **open,
  pre-existing question**" and "**deliberately not resolved here**" was
  rewritten to state the resolved composition by reference to §3.4. The
  "Does NOT reach" rows were reconciled: the modifier now reaches **both** the
  Boss basic attack and a Boss Skill (through the `EffectiveBossATK`
  contribution), and never reaches the Skill's **authored** Base Damage value.
- `docs/01-game-design/COMBAT_RULES.md` — §5.5.3: the now-false row
  "Does NOT apply to a Boss Skill whose Base Damage is independently authored"
  was replaced with "Does NOT apply to a Boss Skill's independently authored
  Base Damage value …", and the "Applies to" row now cites the Step-1
  `EffectiveBossATK` contribution per §5.5.1/§5.5.2.
- `docs/01-game-design/COMBAT_RULES.md` — header: Version 1.9 → **2.0**,
  per the file's own convention; prior 1.9 text preserved verbatim under
  "Prior 1.9:".
- `docs/01-game-design/BOSS_RULES.md` — §6.2.1's Rage **Damage scope** bullet
  reconciled (it previously asserted Rage reaches "only … the Boss basic
  attack" and that a Boss Skill does not receive the modifier — contradicted
  by the resolved composition). It now states the modifier reaches Boss damage
  through the Step-1 `EffectiveBossATK` contribution (basic attack and Boss
  Skill alike) while never reaching a Skill's authored Base Damage value.
  Magnitudes unchanged; composition and consumption referenced to owners.
- `docs/01-game-design/BOSS_RULES.md` — header: Version 2.5 → **2.6**, per the
  file's own convention; prior 2.5 text preserved verbatim under "Prior 2.5:".
- `tasks/backlog/TASK-126-…md` — Status → DONE; Completion Evidence completed.
  (File moved to `tasks/completed/`.)

**No other file changed.** `src/**` and `tests/**` are byte-identical
(aggregate over 23,301 files unchanged). `GAME_RULES.md`, `GAME_STATE.md`,
`SIGNALR_PROTOCOL.md`, `API_CONTRACTS.md`, `REDIS_STATE.md`, `DATABASE.md`,
`ARCHITECTURE.md`, `GAME_EVENTS.md`, and `docs/03-decisions/**` are all
byte-identical.

### Validation Results

```text
TASK-125 byte-identical       PASS — SHA256
                              E9374FBE033CF4D65FE4E2262EA2DA9CE71AD194C8A10E90
                              B87A3E85C7DEC5CD, unchanged; still at
                              tasks/backlog/; Status unchanged.
TASK-124 byte-identical       PASS — SHA256
                              4EEE2C82635572D7D169FEDD91D8014343024DDE3D81ABC
                              4875250EA1263724A, unchanged; still at
                              tasks/completed/; Status unchanged.
TASK-123 byte-identical       PASS — SHA256
                              913F4823A958069C08AC1FF494379E43CFF1B466E4C5631F
                              C2114F57623A4255, unchanged; still at
                              tasks/blocked/; Status unchanged.
src/ + tests/ byte-identical  PASS — aggregate over 23,301 files unchanged.
Protected docs unchanged      PASS — GAME_RULES.md, GAME_STATE.md,
                              SIGNALR_PROTOCOL.md, API_CONTRACTS.md,
                              REDIS_STATE.md, DATABASE.md, ARCHITECTURE.md,
                              GAME_EVENTS.md, docs/03-decisions/README.md all
                              byte-identical before and after.
Old GAP-1 wording removed     PASS — searches for "open, pre-existing",
                              "deliberately not resolved", "either reading",
                              "REPLACES the ATK term", "remains unresolved",
                              "not authored here" return no ACTIVE claim about
                              this composition. The only surviving occurrence
                              is inside COMBAT_RULES.md's preserved historical
                              "Prior 1.9:" version entry, which is a correctly
                              labelled record of the superseded state.
Single canonical definition   PASS — exactly one composition definition, at
                              COMBAT_RULES.md §3.4; §5.5.2/§5.5.3 and
                              BOSS_RULES.md §6.2.1 reference it without
                              restating it.
§3 step 1 sum rule intact     PASS — the Base Damage sum wording is unchanged.
Required statements present   PASS — all five: Step 1 includes EffectiveBossATK;
                              authored value additive; modifier reaches Skill
                              Step-1 damage through EffectiveBossATK; authored
                              value unchanged by the modifier; worked example
                              120 + 150 = 270.
Preserved invariants          PASS — §3.4 Boss Basic Attack clause and Boss-side
                              Step 4 = 1.0 intact; BossState.ATK documented as
                              immutable/base; EffectiveBossATK documented as
                              derived/non-stored; Hỏa Long Rage remains a
                              modifier to EffectiveBossATK; §5.3 DR1–DR6, §5.4,
                              §5.4.5, §5.5.1, §5.5.4, §5.5.5 unchanged.
Authored values unchanged     PASS — BOSS_RULES.md §6.3/§6.3.1 still carry
                              150 / 120 / 100 and Rage +20% / 3 turns. The
                              string "270" occurs nowhere in BOSS_RULES.md and
                              appears in COMBAT_RULES.md only as the runtime
                              worked-example result.
Forbidden identifiers absent  PASS — zero occurrences of BossEffectiveATK,
                              EffectiveBossSkillATK, SkillAttackStat, or
                              BossSkillATK anywhere in docs/.
No new ADR                    PASS — no ADR created; docs/03-decisions/
                              unchanged.
No implementation task        PASS — no TASK-127 and no Boss Skill
                              implementation task created.
```

### Decision Applied

```text
TASK-125 D-1: Option B — applied at its canonical owners.
Boss Skill Step 1 includes EffectiveBossATK; the authored Skill Base Damage is
additive to it; Boss ATK modifiers such as Hỏa Long Rage reach Boss Skill
Step-1 damage through EffectiveBossATK; the authored Skill Base Damage remains
unchanged.
Worked example: EffectiveBossATK = 120; 120 + 150 = 270.
GAP-1 open-composition wording: RESOLVED.
```

### Required Statement

```text
Documentation only.
No source code.
No tests requiring source changes.
No gameplay implementation.
No new event.
No new SignalR member.
No new BattleState member.
No new Redis key.
No PostgreSQL schema change.
```

### Scope Verification

- [x] No source code modified — zero files under `src/` or `tests/`
- [x] No new `BattleState`/`BossState` member introduced
- [x] No new Battle Event introduced
- [x] No SignalR, API, Redis, or database contract change introduced
- [x] `TASK-123`, `TASK-124`, and `TASK-125` byte-identical, unmoved, not
      re-statused
- [x] No boss stat or balance value authored or changed
- [x] No ADR created or modified
- [x] The Boss Skill Step-1 implementation task was **not** created
- [x] No gameplay system outside Boss Skill Step-1 composition was changed
- [x] Adherence to MVP Scope (`docs/00-overview/MVP_SCOPE.md` §1)

### Reported (not fixed — `AGENTS.md` §16)

`BOSS_RULES.md` §6.2.1's "Damage scope" bullet was the only statement outside
`COMBAT_RULES.md` found to contradict the resolved composition, and it was in
this task's declared consistency-verification scope, so it was reconciled. No
other contradiction was found in `docs/`. No unrelated issue was fixed inline.

---

## §10 — Implementation Task Boundary

TASK-126 ends at:

```text
Authoritative documentation resolved
        ↓
Implementation task can be created later
```

It must **NOT** create the implementation task. If the repository's workflow
normally requires a separate implementation task, that is a later step and is
**not** performed here (§11).

---

## §11 — Out of Scope (Explicit)

```text
Source-code implementation
BattleStateService changes
DamagePipeline implementation
BossDefinition implementation
BossState implementation
SignalR changes
Redis changes
Database changes
Frontend changes
Match-3
Board
Gems
Swap
Cascade
Combat redesign
Critical hit redesign
Burn redesign
Boss passive redesign
Boss balance redesign
New gameplay mechanics
New state fields
New events
New API endpoints
New SignalR methods
New Redis keys
New ADR unless the workflow explicitly requires one
Creating the Boss Skill Step-1 implementation task
```

No implementation instruction is disguised as documentation work: this task
edits prose in `docs/` only, and states the contract that a later
implementation task will consume.
