# TASK-124 — Apply the TASK-123 Boss Passive Contract Decisions to Their Authoritative Documentation Owners

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, schemas, or contracts beyond the minimum needed to identify
  WHICH recorded decision is being applied and WHERE.

  THIS TASK DECIDES NOTHING. Every gameplay decision it applies was already
  made by the Product Owner and is recorded verbatim in
  tasks/blocked/TASK-123-resolve-boss-passive-effect-contract.md. This task
  is the repository's documentation CONTRACT-RESOLUTION step for that
  decision set: it carries the recorded decisions into their canonical
  owner documents so the contract becomes consistent and canonical.

  THIS TASK IMPLEMENTS NOTHING. Zero files under src/ or tests/. No Boss
  Passive runtime logic, no Rage logic, no healing-reduction logic, no
  regeneration logic, no Boss Skill change. It is DOCUMENTATION-only.

  PROVENANCE: TASK-123 records all fourteen decision groups (Contradiction
  A + Contradiction B + D-1/D-1a/D-1b/D-1c + D-2a/D-2b/D-2b-site/D-2c/
  D-2c-duration/D-2d/D-2e + D-3a–D-3f + D-4a–D-4g) as binding Product Owner
  decisions, names their canonical owners in its "Canonical Ownership
  Register", and then states explicitly that it is NOT DONE and remains
  BLOCKED: "the owning documentation edits have not been made ... they are
  a separate task's act", and "the repository's documentation-change
  workflow assigns the authoritative documentation edits to a separate
  contract-resolution task". This task is that task.

  BOUNDARY: documentation only. No source, no tests, no migration, no
  protocol change, no new state member, no new event, no new ADR unless
  this task's Stop Conditions prove one is genuinely required (TASK-123
  found none).
-->

---

## Metadata

```text
Task ID:           TASK-124
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md). The deliverable is
                   the applied contract in its canonical owner document(s).
                   NOT GAMEPLAY-CHANGE: no gameplay rule is changed — every
                   rule applied here was already decided by TASK-123 and is
                   only being written into its owner. NOT ARCHITECTURE: no
                   battle-state concept, persistence strategy, or realtime
                   strategy changes.
Status:            DONE — with ONE recorded, non-blocking STOP
                   (Boss Skill Step-1 composition, GAP-1). All documentation
                   reconciliation TASK-124 is responsible for has been applied
                   at its canonical owners; GAP-1 was assessed from
                   authoritative evidence and found NOT to be determined by
                   the existing documents, so per §6 it is recorded as an
                   unresolved PRE-EXISTING gameplay decision and was NOT
                   authored here. See "Completion Evidence" and "STOP
                   CONDITION — GAP-1" below. The task file is moved
                   tasks/backlog/ → tasks/completed/ per TASK_LIFECYCLE.md
                   §4 (IN REVIEW → DONE).
Risk:              MEDIUM (TASK_TYPES.md §4 — DOCUMENTATION baseline LOW–MEDIUM;
                   MEDIUM because the applied contract is cross-referenced by
                   GAME_RULES.md §17 step 18a, BOSS_RULES.md §3/§6.2,
                   COMBAT_RULES.md §4/§5, and GAME_STATE.md §2.4.1/§2.4.2, and
                   because one applied decision (D-2c) AUTHORS a genuinely new
                   combat-rule mechanism at its owner.)
Priority:          HIGH (this is the sole remaining precondition between the
                   completed TASK-123 decision set and any future step-18a
                   implementation. ROADMAP.md Phase 1 requires "3 MVP Bosses
                   (Hỏa Long, Thủy Ma, Mộc Yêu — Passive + Skill each)" and
                   "Boss Response (Passive → Skill → Attack →
                   Victory/Defeat)". The Skill half landed in TASK-118; the
                   Passive half is blocked behind this reconciliation.)
Primary Agent:     review (TASK_TYPES.md §2 / AGENT_SELECTION.md §1 —
                   "Documentation change: Primary Agent Review")
Supporting Agents: gameplay (BOSS_RULES.md §3/§6.2 and COMBAT_RULES.md §4/§5
                   are the owning domain documents for the three effects —
                   consulted to place each recorded decision at its owner
                   accurately, NOT to author or re-decide any rule),
                   backend (GAME_STATE.md §2.3.1/§2.4.1/§2.4.2 and
                   DATABASE.md §6 own the state and storage characterizations
                   being corrected — consulted to state accurately what the
                   existing representation does and does not carry)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-123 (BLOCKED — the decision-input task whose recorded
                     decisions are this task's entire input. IMMUTABLE;
                     READ-ONLY; must NOT be modified, re-statused, moved, or
                     rewritten),
                   TASK-119 (DONE — authored COMBAT_RULES.md §5.4, the
                     Pet-side precedent this task's Boss-side rule mirrors.
                     IMMUTABLE; read-only),
                   TASK-118 (DONE — implemented step 18b and fenced step 18a
                     out as requiring a separate decision task. IMMUTABLE;
                     read-only),
                   TASK-022 (DONE — implemented Boss Response
                     charging/events only. IMMUTABLE; read-only)
Blocks:            Any future Boss Passive effect implementation task (step
                   18a) — it cannot be created or executed until this
                   reconciliation lands and the contract is consistent.
Estimate:          Simple–Normal (apply a recorded decision set across at most
                   four owner documents; no code, no tests, no migration)
```

**Type classification note.** `DOCUMENTATION`. This task's entire content is
"take decisions that already exist and write them at their owners." It may not
reinterpret, re-rank, replace, soften, strengthen, or extend any TASK-123
decision, and it may not decide anything TASK-123 left open (see §6 below,
where one such open item is handled by STOP rather than by inference).

**Not a decision task.** If, while applying a decision, this task discovers
that the decision is genuinely ambiguous or that the documents cannot express
it, the correct action is the task's Stop Conditions — not to choose an
interpretation.

---

## Objective

Reconcile the authoritative documentation with the fourteen decision groups
already recorded in `tasks/blocked/TASK-123-resolve-boss-passive-effect-contract.md`,
so that the Boss Passive effect contract (`GAME_RULES.md` §17 step 18a — Hỏa
Long's Rage, Thủy Ma's healing reduction, Mộc Yêu's regeneration) exists as a
single consistent, deterministic, canonically-owned contract, with each rule
written once at its owner and referenced everywhere else.

The task is complete when:

```text
TASK-123 recorded decisions
        ↓
Authoritative documentation reconciliation
        ↓
Consistent canonical contract
        ↓
STOP
```

No source implementation is part of this task.

---

## Authoritative References

### The decision input (READ ONLY — this task applies these, it does not re-decide them)

- `tasks/blocked/TASK-123-resolve-boss-passive-effect-contract.md` — **the
  entire input.** Its "Product Owner Decisions" section holds the verbatim,
  binding decisions; its "Canonical Ownership Register" holds the owner
  mapping this task executes; its "Remaining Pre-Existing Documentation Gaps"
  (GAP-1 … GAP-5) holds the reported items this task must handle per §6 and
  §7 below. **IMMUTABLE — do not modify this file in any way.**

### Documents to be reconciled (the owners)

- `docs/01-game-design/COMBAT_RULES.md` **§3.4** — Boss Damage. The
  basic-attack vs per-Skill Step-1 distinction D-1a relies on. **NOT amended
  by D-1/D-1a.**
- `docs/01-game-design/COMBAT_RULES.md` **§4** — Healing and Shields. Owner
  of the Health/Heal rule; to gain the D-2c shared Heal Resolution step.
- `docs/01-game-design/COMBAT_RULES.md` **§5.2 item 2** — the MVP
  refresh-not-stack default D-1c and D-2d point at. **NOT amended.**
- `docs/01-game-design/COMBAT_RULES.md` **§5.3 / §5.3.1 / §5.3.2 / §5.3.3** —
  the canonical Turn-duration owner (DR1–DR6) D-1b and D-2c-duration ride.
  **DR1–DR6 NOT amended**; §5.3.3 gains worked examples.
- `docs/01-game-design/COMBAT_RULES.md` **§5.4 / §5.4.1 / §5.4.4 / §5.4.5** —
  the Pet-side ATK-modifier rule D-1 mirrors. **NOT amended**; §5.4.5's scope
  boundary remains true.
- `docs/01-game-design/BOSS_RULES.md` **§6.2** — the MVP Boss Passive rows
  and prose (`+20% ATK`/3 turns; `−50%`/`active Pet`; `5% MaxHP`). Owner of
  the per-Boss Passive behavior; carries the stale wording to correct.
- `docs/01-game-design/BOSS_RULES.md` **§3 / §3.3** and **§6.3.1** — Boss
  Passive timing and per-Skill effect magnitudes (context for §6 below).
- `docs/01-game-design/GAME_RULES.md` **§17 step 18a** — the step that
  obliges the effect application; owner of battle execution order.
- `docs/02-technical/GAME_STATE.md` **§2.3.1 / §2.4 / §2.4.1 / §2.4.2** —
  the `StatusEffect` schema and `BossState` representation. Owner of runtime
  state representation/lifecycle; carries the stale §2.4.1 sentence.
- `docs/02-technical/DATABASE.md` **§6** (and its `threshold` characterization
  around §1 item 3 / §6's constraint list) — owner of persistence/storage
  constraints only; carries the stale `always-active` characterization.

### Governance and process

- `.ai/workflow/documentation/documentation-change.md` §1 (flow), **§2 (no
  duplication, ever)**, §3 (determining the canonical owner), §4
  (composition — still goes through `quality/review.md`).
- `AGENTS.md` §2 (source-of-truth hierarchy), §4 (conflict resolution), §7
  (never invent a rule), §9 (anti-overengineering), §16 (task discipline),
  §17 (documentation change rule), §18 (ADR rule), §20 (stop conditions).
- `docs/AGENTS.md` §2 (owner hierarchy), §4 (conflict resolution), §7, §17,
  §20.
- `tasks/README.md` §9 (no business-rule duplication in task files), §12
  (skill budget), `tasks/TASK_TYPES.md` §2 (DOCUMENTATION), §4 (risk).
- `docs/00-overview/MVP_SCOPE.md` §1 (Bosses, Combat/Status Effects are IN),
  §2 (OUT), §4 (unlisted is not implicitly IN).

### Explicitly NOT amended (verify, do not edit)

- `docs/01-game-design/COMBAT_RULES.md` §3.4 and §5.4.5 — both retained by
  D-1. If an edit appears necessary here, that is a **Stop Condition**, not a
  reconcile.
- `docs/01-game-design/GAME_RULES.md` §16 — the closed canonical event list.
- `docs/02-technical/GAME_EVENTS.md` §2 — no payload member is added.
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4 — item 4 governs; `bossState` is
  explicitly refused by D-4b.
- `docs/02-technical/REDIS_STATE.md` — the existing key and write-back carry
  both carriers.
- `docs/03-decisions/README.md` §8 / `docs/03-decisions/ADR/` — **no ADR**
  (see §8 below).

---

## TASK-123 Decision Inputs (the complete decision set to apply)

Every item below is **already decided**. This task transcribes each into its
owner. Nothing here is open for reconsideration.

### Hỏa Long — Rage

```text
Contradiction A resolution   Rage modifies the Step-1 `Attack` input via a
                             derived EffectiveBossATK, BEFORE the Boss Damage
                             Pipeline; it is NOT a Step-4 modifier.
                             COMBAT_RULES.md §3.4 and §5.4.5 are retained
                             UNCHANGED.
D-1 representation           TurnBased BuffDebuff StatusEffect in
                             BossState.StatusEffects[], TargetStat = "ATK",
                             Magnitude = +20%, RemainingTurns = 3.
                             BossState.ATK remains the immutable/base value.
                             NOT a separate BossState field.
D-1a damage scope            Rage reaches ONLY Boss damage whose Step 1 Attack
                             input is derived from BossState.ATK — the basic
                             attack. An independently authored Boss Skill Base
                             Damage does NOT receive +20% merely for being a
                             Boss attack; Flame Burst's 150 remains 150 unless
                             its own contract declares ATK scaling.
D-1b duration                Existing TurnBased duration model. RemainingTurns
                             = 3 on application at step 18a; non-retroactive
                             for that Turn's already-resolved damage; active
                             for the next three counted Turns; decrement at
                             step 19a; expires after step 19a of the third
                             active Turn; no new duration mechanism.
D-1c reapplication           Re-trigger does NOT create a second instance and
                             does NOT add another +20%; it REFRESHES the
                             existing instance to RemainingTurns = 3;
                             magnitude stays +20%; same source identity; at
                             most one +20% Rage instance active at a time.
```

Canonical flow to record:

```text
BossState.ATK
    ↓
Hỏa Long Rage +20%
    ↓
EffectiveBossATK
    ↓
Boss Damage Pipeline
```

### Thủy Ma — healing reduction

```text
D-2a              "for 3 turns" is authoritative; the −50% is a triggered
                  temporary effect, NOT always-active; "Passive (always
                  active)" is retired as stale wording.
D-2b              Trigger = Battle Start (PASSIVE_RULES.md §3, an existing
                  one-time trigger). Represented with the existing TurnBased
                  Buff/Debuff StatusEffect model. No PassiveTracker.Charge;
                  no PassiveCharged/PassiveTriggered from match progress; no
                  new trigger mechanism.
D-2b-site         The −50% applies at the shared Heal resolution point BEFORE
                  the existing overheal clamp; it reaches Pet healing from
                  any existing source using that resolution (explicitly Card
                  Heal and HP-Gem healing); it does NOT modify MaxHP and does
                  NOT affect Shield.
D-2c              A canonical shared Heal Resolution step is AUTHORED at
                  COMBAT_RULES.md §4. All Pet-HP healing sources pass
                  through it before the existing overheal clamp. Canonical
                  order: Raw Heal → applicable Heal modifiers → final Heal
                  amount → existing MaxHP/overheal clamp → HP update.
                  Thủy Ma's −50% is ONE applicable Heal modifier.
                  Combat-rule mechanism only: no BattleState member, event,
                  SignalR payload, Redis key, or database field; no
                  speculative generic abstraction.
D-2c-duration     Applied at Battle Start with RemainingTurns = 3; active
                  throughout Turns 1, 2, and 3; the Battle Start application
                  is NOT a turn and consumes no duration unit; decrement at
                  the existing End Turn / step 19a boundary; after step 19a
                  of Turn 3, RemainingTurns reaches 0 and the effect expires
                  before Turn 4; no new duration mechanism or lifecycle
                  phase.
D-2d              Reapplication REFRESHES the existing instance to the full
                  3-turn duration; does NOT stack additively; at most one
                  active instance at a time; same Thủy Ma source identity.
D-2e              NO new Battle Event; emits no PassiveCharged/
                  PassiveTriggered; application, refresh, decrement, and
                  expiry are state changes only; no HealingReduced,
                  BossPassiveApplied, or BossPassiveExpired event.
```

Canonical flow to record:

```text
Battle Start
    ↓
BossState.StatusEffects[]
    ↓
BuffDebuff healing modifier
    ↓
RemainingTurns = 3
```

Healing modifier order to record:

```text
Raw Heal
    ↓
Applicable Heal Modifiers
    ↓
Final Heal Amount
    ↓
Existing MaxHP / overheal clamp
    ↓
HP update
```

**Scope boundary that must be preserved:** D-2c is **Pet-scoped**. Do not
silently widen it to Boss healing. D-4g explicitly refuses that widening.

### Mộc Yêu — regeneration

```text
D-3a  Timing        applied during Boss Response step 18a, at the point the
                    Boss Passive effect is applied.
D-3b  Base          heals exactly 5% of Mộc Yêu's MaxHP.
D-3c  Rounding      truncate toward zero to an integer HP amount.
D-3d  Clamping      Final HP = min(CurrentHP + RegenAmount, MaxHP); no
                    overheal retained.
D-3e  Observability a DIRECT authoritative BossState.HP update; no new Battle
                    Event; observable through existing authoritative state
                    synchronization.
D-3f  Repetition    each valid activation applies ONE 5% MaxHP regeneration;
                    it does NOT stack as a persistent modifier.
```

Canonical flow to record:

```text
GAME_RULES §17 step 18a
    ↓
Mộc Yêu Passive effect
    ↓
Boss HP += truncate(MaxHP × 5 / 100)
    ↓
clamp to MaxHP
```

Worked example at the documented MVP MaxHP:

```text
MaxHP = 5000            (BOSS_RULES.md §6.1)
5000 × 5 / 100 = 250
FinalHP = min(CurrentHP + 250, 5000)
```

**Required boundary:** no new persistent `StatusEffect` instance is created;
no new Battle Event; no new SignalR payload member. Do NOT widen D-2c's
Pet-scoped Heal Resolution to Boss healing (D-4g).

### Cross-cutting — D-4 (D-4a … D-4g)

```text
No new Battle Event.
No new SignalR method.
No new SignalR event.
No new BattleState member.
No new Redis key.
No PostgreSQL schema change.
No client-authoritative Boss state.
```

Existing runtime persistence remains:

```text
battle:{battleId}:state
```

`BossState` changes are persisted through the existing `BattleState`
serialization (`BossState.StatusEffects[]` and `BossState.HP` both ride it).
The current SignalR projection intentionally does NOT expose `BossState`; this
is a **recorded, intentional contract limitation**, not a defect.

If client-visible Boss HP/StatusEffects are ever required, that is a separate
future protocol task. **Do not create that future task here** — the
repository's documentation workflow does not require task creation as part of
reconciliation. Record the limitation in the authoritative contract instead
(see "Affected Documentation" item 5).

---

## Scope

### In Scope

1. **Apply the Hỏa Long decision set** (Contradiction A, D-1, D-1a, D-1b,
   D-1c) at its owners: author the Boss-side ATK modifier rule in
   `COMBAT_RULES.md` beside §5.4 (D-1's own consequence note proposes a
   §5.5-style subsection), and state the Rage behavior at `BOSS_RULES.md`
   §6.2 pointing at the owners rather than restating them.
2. **Apply the Thủy Ma decision set** (D-2a, D-2b, D-2b-site, D-2c,
   D-2c-duration, D-2d, D-2e): author the **shared Heal Resolution step** in
   `COMBAT_RULES.md` §4 (the one genuinely new authored mechanism from
   TASK-123, including routing the §2 HP-Gem "Heal pool" through it and
   widening §4's own self-description line to cover Heal resolution as well
   as Shield), and correct `BOSS_RULES.md` §6.2's stale trigger wording.
3. **Apply the Mộc Yêu decision set** (D-3a–D-3f) at `BOSS_RULES.md` §6.2
   and, where the timing/clamp belong to an existing owner, by reference to
   `GAME_RULES.md` §17 step 18a and `COMBAT_RULES.md` §4 item 1.
4. **Apply the D-4 cross-cutting contract**: record the no-new-event /
   no-new-wire-member / no-new-key / no-schema-change / server-authoritative
   position and the intentional client-visibility limitation in the
   authoritative contract(s) that own those statements — as statements of the
   existing contract, **not** as edits that widen any of them.
5. **Correct the stale wording** identified below in "Current State".
6. **Add the D-2c-duration and D-1b worked examples** to `COMBAT_RULES.md`
   §5.3.3 (a duration-3 apply before the first counted Turn, and the Boss-side
   step-18a apply case), since §5.3.3 currently illustrates only applications
   occurring at a Turn's own step. `§5.3`'s DR1–DR6 rules themselves are
   **not** amended.
7. **Validate** that no rule is duplicated across owners, that every
   cross-reference points at the actual canonical owner, and that no new
   event/wire member/state member/Redis key/schema change was introduced.
8. **Report** the disposition of the pre-existing gaps per §6 and §7.

### Out of Scope

- **Any source code change.** Zero files under `src/` or `tests/`. No Boss
  Passive runtime logic, no Rage logic, no healing-reduction logic, no
  regeneration logic, no Boss Skill runtime change, no Match-3, no combat
  code, no SignalR code, no Redis code, no database migration, no frontend,
  no Phaser.
- **Implementing step 18a**, or any part of it.
- **Re-deciding anything.** No reinterpretation, re-ranking, replacement,
  softening, or extension of any TASK-123 decision. No new gameplay rule.
- **Modifying `TASK-123`.** It is historical decision evidence; it is not
  rewritten, re-statused, edited, or moved.
- **Creating another implementation task**, or any other task.
- **Creating an ADR** unless a Stop Condition proves one is genuinely
  required (TASK-123 found none, and multiple documents changing is not by
  itself a reason — see §8).
- **Widening `D-2c` to Boss healing** (D-4g forbids it).
- **Adding `bossState` to the SignalR projection**, or any other protocol
  expansion.
- Editing `COMBAT_RULES.md` §3.4 or §5.4.5, `GAME_RULES.md` §16,
  `GAME_EVENTS.md` §2, `SIGNALR_PROTOCOL.md` §4, `REDIS_STATE.md`, or the
  database schema.
- **Designing option (i)/(ii) of the Boss Skill Step-1 question** — see §6.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State

The three §6.2 Boss Passive effect semantics are decided (TASK-123) but are
**not present in any `docs/` file**. TASK-123 changed no document and said so
explicitly. Verified in the working tree:

```text
COMBAT_RULES.md §4              Healing. Item 1 owns the overheal clamp
                                ("Heal effects restore HP up to Max HP;
                                overheal is discarded..."). Item 6 states Heal
                                amounts "ARE subject to their own explicit
                                modifiers (e.g. a Relic ...)" and defines NO
                                mechanism, NO site, and NO ordering. §4's own
                                self-description line declares it "the
                                canonical owner of the Shield rule" only.
                                → D-2c's shared Heal Resolution step does not
                                  exist. The string "Heal pool" occurs once in
                                  all of docs/ (COMBAT_RULES.md §2's generation
                                  table) and is never consumed or referenced.

COMBAT_RULES.md §3.4            "Step 4 — Other Modifiers = 1.0 (MVP: no
                                Relic/Passive/Buff modifiers on Boss side)".
                                Must stay as written (D-1 retains it).

COMBAT_RULES.md §5.4.5          "Does NOT apply to the Boss's damage — §3.4
                                pins the Boss side's Step 4 to 1.0, and this
                                rule authors no Boss-side factor".
                                Must stay as written (D-1 retains it).

COMBAT_RULES.md §5.3.3          Worked examples illustrate only applications
                                occurring at a Turn's own step. No Battle-Start
                                (pre-Turn-1) case and no Boss-side step-18a
                                case.

BOSS_RULES.md §6.2 (line 188)   "Thủy Ma  Active Pet healing reduced by 50%
                                for 3 turns  Passive (always active)" — the row
                                asserts BOTH a 3-turn duration AND an
                                always-active trigger. STALE per D-2a.

BOSS_RULES.md §6.2 (lines
192-200)                        The prose states Thủy Ma's trigger is
                                "Passive (always active)" — an alternate
                                trigger (PASSIVE_RULES.md §3)... and that "Its
                                always-on effect application is a separate
                                concern". The cited authority does not contain
                                the cited trigger (PASSIVE_RULES.md §3's closed
                                list is Combo | HP Threshold | Damage Dealt |
                                Battle Start | Card Cast). STALE per D-2a/D-2b.

GAME_STATE.md §2.4.1 (line
1475)                           "For MVP, no content-defined Boss applies a
                                Status Effect to itself; the collection is
                                defined because Stun (§2.4.5) and future
                                content are tracked through it." STALE once
                                Hỏa Long Rage (D-1) lives in exactly that
                                collection.

GAME_STATE.md §2.4.2            Defines `PassiveId` and `PassiveProgress` only.
                                No effect representation, no active-effect
                                collection, no effect duration. CORRECT and
                                unchanged by the recorded decisions (the
                                representation reuses §2.4.1's collection).

DATABASE.md §6 (line 1141)      Constraint list: "BossDefinition.
                                PassiveDefinition.threshold = null ⇔
                                always-active, no threshold  (BOSS_RULES.md
                                §6.2)". The pointer cites §6.2 for an
                                "always-active" semantic that D-2a RETIRED.
                                Also §1 item 3 (line 650): "`null` means the
                                Passive has no threshold and is always active
                                (`BOSS_RULES.md` §6.2)".

GAME_RULES.md §17 step 18a      "evaluate the Boss's Passive trigger condition
                                ... If the trigger is met, apply the Passive
                                effect and emit PassiveCharged/
                                PassiveTriggered". The obligation exists; the
                                contract it points to was absent. Unchanged by
                                the decisions — the step order is NOT amended.
```

**What this task does NOT change.** Boss Passive **timing** (step 18a, once per
player action, post-damage, before 18b/18c, not re-triggered by Boss Skill
damage) is frozen by `BOSS_RULES.md` §3.3 and `GAME_RULES.md` §17. Boss Passive
**charging, thresholds, reset behavior, and events** are frozen by TASK-022,
`PASSIVE_RULES.md` §2/§4, and `BOSS_RULES.md` §6.2. Enrage and Stun contracts
are frozen by `GAME_STATE.md` §2.4.4/§2.4.5. §5.4's Pet-side rule is frozen by
TASK-119. The §6.2 magnitudes (`+20% ATK`, `3 turns`, `−50%`, `5% MaxHP`,
MaxHP 5000) are **not** changed — they are already authored and this task
carries them, it does not choose them.

---

## Acceptance Criteria

All criteria are binary and testable.

- [ ] All TASK-123 decisions are represented consistently in authoritative
      documentation.
- [ ] Hỏa Long Rage representation is documented (TurnBased `BuffDebuff` in
      `BossState.StatusEffects[]`, `TargetStat = "ATK"`, `Magnitude = +20%`,
      `RemainingTurns = 3`; `BossState.ATK` remains the immutable/base value;
      not a separate `BossState` field).
- [ ] Hỏa Long Rage damage scope is deterministic (only Boss damage whose
      Step-1 `Attack` input derives from `BossState.ATK`; an independently
      authored Boss Skill Base Damage is not modified).
- [ ] Hỏa Long Rage duration is deterministic (apply at step 18a,
      `RemainingTurns = 3`, non-retroactive, three counted Turns, decrement at
      step 19a, expiry after the third active Turn's step 19a).
- [ ] Hỏa Long Rage reapplication is deterministic (refresh to
      `RemainingTurns = 3`, no stacking, same source identity, at most one
      instance).
- [ ] Thủy Ma trigger is deterministic (Battle Start; no
      `PassiveTracker.Charge`; no match-progress
      `PassiveCharged`/`PassiveTriggered`).
- [ ] Thủy Ma healing modifier application point is deterministic (the shared
      Heal resolution point, before the existing overheal clamp; does not
      modify MaxHP; does not affect Shield).
- [ ] Thủy Ma duration is deterministic (applied at Battle Start with
      `RemainingTurns = 3`; active Turns 1–3; the application is not a turn
      and consumes no unit; expires before Turn 4).
- [ ] Thủy Ma reapplication is deterministic (refresh to `RemainingTurns = 3`,
      no additive stacking, at most one instance, same source identity).
- [ ] Mộc Yêu regeneration timing is deterministic (Boss Response step 18a).
- [ ] Mộc Yêu regeneration amount/rounding/clamping is deterministic (5% of
      MaxHP, truncate toward zero, `min(CurrentHP + RegenAmount, MaxHP)`, no
      overheal; the 5000 → 250 worked example is recorded).
- [ ] Mộc Yêu repetition is deterministic (one 5% regeneration per valid
      activation; no persistent stacking).
- [ ] **Shared Heal Resolution is explicitly defined** as authored content at
      `COMBAT_RULES.md` §4, with the canonical order `Raw Heal → applicable
      Heal modifiers → final Heal amount → existing MaxHP/overheal clamp →
      HP update`, covering existing Pet-HP healing sources (Card Heal and
      HP-Gem healing), with Thủy Ma's −50% as one applicable Heal modifier.
- [ ] `COMBAT_RULES.md` §4's own self-description line no longer claims to own
      only the Shield rule.
- [ ] No new event is introduced (`GAME_RULES.md` §16 and `GAME_EVENTS.md` §2
      unchanged; no `BossPassiveApplied`, `BossPassiveExpired`,
      `HealingReduced`, `BossRageApplied`, `BossRegenerated`, or equivalent).
- [ ] No new SignalR method, event, or payload member is introduced
      (`bossState` is NOT added; `SIGNALR_PROTOCOL.md` §4 unchanged).
- [ ] No new `BattleState`/`BossState` member is introduced
      (`GAME_STATE.md` §2.3.1's member set unchanged).
- [ ] No new Redis key is introduced (`battle:{battleId}:state` remains sole
      persistence; `REDIS_STATE.md` unchanged).
- [ ] No PostgreSQL schema change is introduced (`DATABASE.md` gains no table,
      column, or entity — only the stale characterization is corrected).
- [ ] `BossState` remains server-authoritative; the client-visibility
      limitation is recorded as an intentional contract limitation.
- [ ] `GAME_STATE.md` §2.4.1's stale "no content-defined Boss applies a Status
      Effect to itself" wording is corrected minimally.
- [ ] `BOSS_RULES.md` §6.2's stale Thủy Ma wording (trigger column and the
      lines 192–200 prose, including the retired `PASSIVE_RULES.md` §3
      citation) is corrected, and no contradictory wording is preserved.
- [ ] `DATABASE.md`'s stale `threshold = null ⇔ always-active`
      characterization is corrected **if applicable**, without redesigning
      the schema, adding tables or columns, or changing the storage constraint
      itself.
- [ ] The Boss Skill Step-1 composition is either resolved from authoritative
      existing evidence or **explicitly left as an unresolved pre-existing
      gap** — per §6, not by inference.
- [ ] No source code is modified; zero files under `src/` or `tests/` change.
- [ ] `TASK-123` is byte-identical (unmodified, unmoved, not re-statused).
- [ ] No ADR is created.
- [ ] No rule is duplicated across documents; every cross-reference points at
      the actual canonical owner (`documentation-change.md` §2).
- [ ] Documentation validation passes at the depth `quality/review.md`
      requires for a documentation change (`core/validation.md` §2).
- [ ] Quality review checklist passes (`quality/review.md` §1, documentation
      items).

---

## Affected Documentation

```text
[x] docs/01-game-design/COMBAT_RULES.md
      §4            — AUTHOR the shared Heal Resolution step (D-2c); widen
                      §4's self-description line to cover Heal resolution;
                      route the §2 HP-Gem "Heal pool" through it; state
                      Thủy Ma's −50% as one applicable Heal modifier
                      (D-2b-site). §4 item 1's clamp is NOT re-ordered or
                      reworded.
      new §5.5-ish   — AUTHOR the Boss-side ATK modifier rule beside §5.4
                      (the shape D-1's own consequence note proposes),
                      stating the Step-1 consumption point, EffectiveBossATK
                      as derived/non-stored, base preservation, and the
                      D-1a damage scope; REFERENCE §3.4 and §5.4.5 rather
                      than restating them.
      §5.3.3        — ADD the worked examples (Battle-Start duration-3 apply;
                      Boss-side step-18a apply). DR1–DR6 unchanged.
      §3.4, §5.4.5  — NO EDIT (retained by D-1; verify only).

[x] docs/01-game-design/BOSS_RULES.md
      §6.2          — correct the Thủy Ma row's trigger column to Battle
                      Start; reconcile lines 192–200's prose so it no longer
                      describes an undefined "always-on effect application"
                      and no longer cites a trigger PASSIVE_RULES.md §3 does
                      not define; state the three effects' resolved behavior
                      (or point at their owners). Must NOT restate §3.4,
                      §5.2 item 2, §5.3, or §4 — reference them.

[ ] docs/01-game-design/GAME_RULES.md
      §17 step 18a  — NO CHANGE EXPECTED. Evaluate only whether a pointer is
                      genuinely useful; do NOT restate any rule here, and do
                      NOT reorder or reword the step.

[x] docs/02-technical/GAME_STATE.md
      §2.4.1        — correct the stale "no content-defined Boss applies a
                      Status Effect to itself" sentence minimally, so it is
                      consistent with Rage (D-1) living in that collection.
      §2.3.1/§2.4   — verify only. The collection, element schema, duration
                      model, and one-instance rule already exist and are NOT
                      widened or relaxed. §2.4.2 needs no structural change.

[?] docs/02-technical/DATABASE.md
      §6 (and §1 item 3's `threshold` note) — correct ONLY the stale
                      "always-active" characterization of `threshold = null`.
                      NO schema change, NO new table, NO new column, NO
                      redesign of the storage constraint itself.

[x] docs/03-decisions/README.md / ADR/ — NO EDIT. No ADR (see §8).
[ ] docs/02-technical/GAME_EVENTS.md, SIGNALR_PROTOCOL.md, REDIS_STATE.md
      — NO EDIT (D-4). Verify only.
[ ] src/**, tests/** — FORBIDDEN.
[x] tasks/ — this new task file only.
```

---

## Documentation Ownership

Each concept has exactly ONE canonical owner. This mapping is TASK-123's, and
this task executes it rather than re-deriving it.

```text
CONCEPT                          CANONICAL OWNER            WHAT THIS TASK DOES
-------------------------------  -------------------------  ------------------
Boss-specific passive behavior   BOSS_RULES.md §6.2         correct the stale
  (which Boss carries which                                   Thủy Ma row/prose;
  effect; its trigger)                                        state each effect's
                                                              behavior or point at
                                                              its owner

Combat resolution mechanics      COMBAT_RULES.md            AUTHOR the shared
  Heal Resolution (D-2c)           §4 (Healing)               Heal Resolution step;
                                                              widen §4's ownership
                                                              line; route the HP-Gem
                                                              heal pool through it

Combat resolution mechanics      COMBAT_RULES.md            AUTHOR the Boss-side ATK
  EffectiveBossATK / Rage          new subsection beside      modifier rule; state the
  Step-1 consumption point         §5.4                       Step-1 point, the
                                                              derived-not-stored
                                                              EffectiveBossATK, and
                                                              base preservation

Combat resolution mechanics      COMBAT_RULES.md            ADD worked examples only
  modifier application ordering    §5.2 item 2, §5.3 DR1–DR6  (both unchanged as rules)
  and Turn-duration consumption

Battle execution order           GAME_RULES.md §17          NO CHANGE — verify the
                                                              step order is intact

Runtime state representation     GAME_STATE.md              correct §2.4.1's stale
  /lifecycle                       §2.3.1, §2.4, §2.4.1,      sentence only; verify
                                   §2.4.2                     §2.3.1's member set is
                                                              unchanged

Persistence/storage constraints  DATABASE.md                correct the stale
  ONLY                                                       characterization only;
                                                              no schema change
```

**Duplication rule (`documentation-change.md` §2).** Each decided rule is
written **once** at its owner above. `BOSS_RULES.md` §6.2 **points at** the
owners and does not restate them. `GAME_STATE.md` types the state and points at
the owning gameplay rule. Technical documents must reference gameplay owners
rather than redefining gameplay values.

**Boundary rule.** `DATABASE.md` never defines gameplay; `GAME_STATE.md` never
defines what a value means, only what state exists.

---

## Validation Requirements

Documentation-only validation. **Do not run source implementation tests as a
substitute for documentation validation** — this task changes no source, so
a green suite is not evidence of correctness here.

```text
[ ] Search for contradictory Hỏa Long Rage definitions
      (representation, magnitude, duration, scope, reapplication) — exactly
      one definition, at its owner; §6.2 points at it.

[ ] Search for contradictory Thủy Ma duration/trigger definitions
      (trigger, duration, application site, reapplication, expiry) — exactly
      one definition; no "always active" / "always-on" survival anywhere in
      docs/ except an explicit historical note where a file's own version
      convention requires one.

[ ] Search for contradictory Mộc Yêu regeneration definitions
      (timing, base, rounding, clamping, repetition) — exactly one definition.

[ ] Verify all cross-references point to the actual canonical owner
      (no reference cites a section that does not carry the rule; no
      reference cites a retired wording).

[ ] Verify no new Battle Event names were introduced
      (GAME_RULES.md §16 unchanged; GAME_EVENTS.md §2 unchanged; search for
      BossPassiveApplied / BossPassiveExpired / HealingReduced /
      BossRageApplied / BossRegenerated → zero occurrences as event names).

[ ] Verify no SignalR payload expansion was introduced
      (SIGNALR_PROTOCOL.md §4 unchanged; no `bossState` member anywhere).

[ ] Verify no new BattleState/BossState field was introduced
      (GAME_STATE.md §2.3.1 member set and §2.4 tree unchanged).

[ ] Verify no Redis key / schema change was introduced
      (REDIS_STATE.md unchanged; DATABASE.md gains no table, column, or
      entity; the storage constraint set is otherwise unchanged).

[ ] Verify no source files were modified
      (aggregate hash or scoped diff over src/ and tests/ — must be identical
      before and after).

[ ] Verify TASK-123 remains historical decision evidence and is not rewritten
      (file byte-identical; still at tasks/blocked/; Status unchanged).

[ ] Re-read each changed owner together with every document that references
      it, confirming no duplicate definition was accidentally introduced
      (documentation-change.md §1's final step).

[ ] Version notes: where a changed file carries a header version-history
      convention (e.g. GAME_STATE.md, DATABASE.md, COMBAT_RULES.md), follow
      that convention and preserve the prior-version chain intact.
```

### Key Edge Cases

- `COMBAT_RULES.md` §3.4 and §5.4.5 must be **byte-identical** after this task
  (D-1 retains both). Any temptation to "fix" them is a Stop Condition.
- `DATABASE.md`'s `threshold = null` constraint is a **storage** statement.
  D-2a retires the *characterization* "always-active", not necessarily the
  constraint. Correct only the characterization; if the constraint itself
  appears to require a semantic change, that is a Stop Condition (§7).
- The retired phrase "Passive (always active)" cites `PASSIVE_RULES.md` §3,
  whose closed list (`Combo | HP Threshold | Damage Dealt | Battle Start |
  Card Cast`) does not contain it. Removing the phrase must also remove the
  defective citation.
- Do not conflate **Enrage** (`GAME_STATE.md` §2.4.4 — permanent, no duration
  field) with **Rage** (D-1 — Turn-based, `RemainingTurns = 3`). They are
  distinct; the reconciliation must not let one's wording leak into the other.
- D-2d's refresh rule is **unreachable in MVP play** (Thủy Ma's Battle Start
  trigger is one-time). It must still be authored — it is the safe behavior
  and §5.2 item 2's default. Do not omit it as "moot".
- D-1c's refresh rule **is** reachable (Hỏa Long's trigger is match-based and
  can recur while Rage is active). Do not annotate it as unreachable.
- `ELEMENT_RULES.md`'s boundary (the Element Modifier does not apply to
  non-damage effects such as Heal) must be preserved — the Heal Resolution
  step introduces no elemental interaction.
- The `"Heal pool"` string occurs exactly once in `docs/` and is never
  consumed today; routing it through the shared step is the intent, and the
  edit must not invent a second generation rule.

---

## Stop Conditions

Universal stop conditions (`AGENTS.md` §20, `.ai/README.md` §13) always apply.
Task-specific stops — **STOP and report instead of guessing** if:

1. **Boss Skill Step-1 composition cannot be determined from authoritative
   evidence.** Per §6 below: if the existing documents do not establish
   whether a Boss Skill's Step 1 is its authored Base Damage alone or
   `EffectiveBossATK + Base Damage`, **STOP and state that this remains a
   separate gameplay decision.** Do not choose by implementation convenience.
   Do not infer a new gameplay rule. Do not create a new decision task.
2. **An existing document contradicts a TASK-123 decision in a way the
   recorded decision cannot resolve.** Report both sources (file + section)
   per `AGENTS.md` §4.
3. **Applying a decision would require a new gameplay rule.**
4. **Applying a decision would require a new `BattleState`/`BossState`
   member.**
5. **Applying a decision would require a new SignalR contract.**
6. **Applying a decision would require a new Redis or database contract**
   (as opposed to merely correcting a stale *characterization*).
7. **A new ADR is genuinely required.** Multiple documents changing is not
   sufficient reason. `docs/03-decisions/README.md` §8 must be checked before
   asserting one is needed.
8. **The documentation workflow requires another decision before
   reconciliation** — including any attempt to widen `D-2c` to Boss healing,
   which `D-4g` explicitly refuses.
9. **`COMBAT_RULES.md` §3.4 / §5.4.5 would have to be amended** to apply a
   decision. D-1 retains both unchanged.
10. **Any work would touch `src/`, `tests/`, or `TASK-123`.**

Use `.ai/README.md` §13's exact report format. Unrelated issues discovered
while editing are **report-only** (`AGENTS.md` §16) — do not fix them inline.

---

## §6 — Pre-Existing Gap: Boss Skill Step-1 (`GAP-1`)

TASK-123 recorded this as a **pre-existing, unresolved** gap, surfaced by D-1a
but not created by it and not settled by any of its decisions:

```text
COMBAT_RULES.md §3.4          Boss Skill: "Same pipeline as above, but Step 1
                              Base Damage is defined per Skill
                              (BOSS_RULES.md §6)".
BOSS_RULES.md §6.3.1 item 1   Flame Burst "Base Damage: 150".

COMBAT_RULES.md §3 step 1     Base Damage is a SUM: "from ATK stat, Skill/Card
                              base value, and any ATK-Gem-generated damage pool
                              for this action".
COMBAT_RULES.md §5.4.1 item 2 spells the sum out for the Pet:
                              "Step 1 = EffectiveATK + Skill/Card base value +
                               ATK-Gem-generated damage pool".
```

**The question.** Is a Boss Skill's Step 1:

```text
(i)  Base Skill Damage ALONE                       (the Skill value REPLACES any
                                                    ATK term), or
(ii) EffectiveBossATK + Base Skill Damage          (both contribute)?
```

**Required determination.** Inspect the existing authoritative documents and
decide only this:

- If they **clearly establish** the intended composition, include the
  **smallest documentation correction** in this task.
- If they **do not determine** the answer, **STOP** and explicitly state that
  this remains a separate gameplay decision.

**Prohibitions.** Do not choose based on implementation convenience. Do not
infer a new gameplay rule. Do not invent the answer. Do not create a new
decision task unless the repository workflow explicitly requires one.

**Note for the executing agent.** Because Rage reaches only ATK-derived Boss
damage (D-1a), the Rage contract is deterministic under **both** readings —
this gap is not a blocker for the Rage reconciliation. It matters for Boss
Skill damage implementation. Assess the evidence honestly; the standing
expectation from TASK-123's own record is that the answer is **not**
determined by the current documents, in which case the correct outcome is an
explicit STOP on this item (recorded in the task's Stop Conditions section)
with the rest of the reconciliation proceeding.

---

## §7 — Pre-Existing Gaps: Required Disposition

TASK-123 reported five pre-existing gaps. Each has an explicit disposition
below; do not silently resolve any of them.

```text
GAP-1  Boss Skill Step-1 composition (§6 above).
       DISPOSITION: determine from evidence if possible; otherwise STOP and
       record as an unresolved pre-existing gameplay gap. NOT this task's to
       decide.

GAP-2  DATABASE.md §6's pointer to the retired BOSS_RULES.md §6.2
       "always-active" wording.
       DISPOSITION: IN SCOPE — correct the stale characterization only (§7 of
       the task brief; "Affected Documentation"). The storage constraint
       itself is not redesigned.

GAP-3  GAME_STATE.md §2.4.1's "no content-defined Boss applies a Status
       Effect to itself" sentence.
       DISPOSITION: IN SCOPE — correct minimally.

GAP-4  Client visibility of BossState.
       DISPOSITION: NOT a documentation inconsistency. It is a RECORDED,
       INTENTIONAL contract limitation (D-4f). Record it as the intentional
       limitation in the authoritative contract. Do NOT create a protocol
       task; do NOT add a wire member.

GAP-5  Boss-side heal vs D-2c's Pet-scoped Heal Resolution step.
       DISPOSITION: the TASK-123 boundary is DECIDED by D-4g — Mộc Yêu's
       regeneration uses its own direct Boss HP update and does NOT widen
       D-2c. The remaining design question (whether a shared Heal Resolution
       step should ever be target-agnostic) is a FUTURE task and is
       explicitly NOT to be resolved here.
```

---

## §8 — ADR Requirement

TASK-123 explicitly found **no ADR required**. This task must not create one
merely because multiple documents change.

```text
Re-verify at execution (do not assume):
  - No decision introduces a new battle-state concept, persistence change,
    realtime strategy change, or module-boundary change.
  - Rage reuses the EXISTING BossState.StatusEffects[] collection, element
    schema, and Turn-countdown model (the Stun precedent,
    GAME_STATE.md §2.4.5) — the TASK-119 precedent, NOT the
    TASK-116 → TASK-117 → ADR-017 precedent.
  - D-2c authors a COMBAT-RULE mechanism, not a state, persistence, or
    realtime concept.
  - Check docs/03-decisions/README.md §8 ("Known Open Items (Not ADRs)")
    before asserting an ADR is needed.
```

If, and only if, the evidence shows a recorded decision genuinely requires
architectural decision recording, that is **Stop Condition 7** — report it;
do not author it here.

---

## Required Statement (must appear verbatim in this task's execution report)

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

## Testing Requirements

### Required Verification

```text
[x] N/A — documentation task. No code, no tests authored, no test runner
        required as evidence of correctness.
```

This task creates no executable verification
(`.ai/workflow/documentation/documentation-change.md` §4). Its verification is
the "Validation Requirements" section above plus `quality/review.md`'s
documentation-applicable items. The resulting contract must nonetheless be
stated so that a **later** implementation task can derive Given/When/Then
scenarios from it per `AGENTS.md` §15 — do not author those tests here.

### Derived Scenarios (to be authored by the FUTURE implementation task, not here)

The reconciled contract must be expressible as at minimum:

- Hỏa Long's Passive triggering while Rage is already active (refresh, not
  +40%).
- Rage active while a Boss Skill fires — the Skill's authored Base Damage is
  unaffected.
- Rage expiring at exactly step 19a of the third active Turn.
- Thủy Ma active with a Card Heal cast, with HP-Gem healing, and with both in
  one Turn — all reduced by 50% at the shared Heal resolution point, before
  the overheal clamp.
- Thủy Ma's window expiring before Turn 4.
- Mộc Yêu regenerating at MaxHP (clamp) and its truncate rule at 5000 → 250.
- Mộc Yêu regenerating when the player's step 15–17 damage reduced Boss HP to
  0 in the same resolution (`BOSS_RULES.md` §5 item 4's terminal check order).
- Determinism: identical battle state + seed produces identical effect
  outcomes.

---

## STOP CONDITION — GAP-1 (`AGENTS.md` §4 / §7 / §20; `.ai/README.md` §13)

<!--
  Recorded by the executing agent. This STOP is the one §6 requires: the
  Boss Skill Step-1 composition could NOT be determined from authoritative
  evidence. It does NOT block the rest of TASK-124's reconciliation, which
  completed. It is NOT this task's to decide.
-->

```text
STOP CONDITION

Problem:
The Boss Skill Step-1 composition question (GAP-1) cannot be resolved from
the existing authoritative documentation. COMBAT_RULES.md §3.4 states only
that a Boss Skill's "Step 1 Base Damage is defined per Skill
(BOSS_RULES.md §6)"; BOSS_RULES.md §6.3.1 gives only flat per-Skill values
("Base Damage: 150"). Neither states whether the authored value REPLACES the
Boss's ATK term in Step 1 or is ADDED to it. Two materially different
implementations are both consistent with the documents — an AGENTS.md §20
"Ambiguous requirement" / "Missing rule" stop condition. Per TASK-124 §6 this
is recorded, not guessed, and NOT authored here.

Relevant sources:
  docs/01-game-design/COMBAT_RULES.md §3 step 1 (line 217):
    "1. Base Damage  (from ATK stat, Skill/Card base value, and any
     ATK-Gem-generated damage pool for this action)"
    → Step 1 is defined as a SUM of three contributions.
  docs/01-game-design/COMBAT_RULES.md §3.4 (lines 430-433):
    "Boss Skill: Same pipeline as above, but Step 1 Base Damage is defined
     per Skill (BOSS_RULES.md §6)."
    → names the source of the value, NOT its composition. Silent on whether
      the ATK term also contributes.
  docs/01-game-design/COMBAT_RULES.md §5.4.1 item 2 (lines 753-755) — the
    PET-side shape, for contrast:
    "Step 1 = EffectiveATK + Skill/Card base value + ATK-Gem-generated
     damage pool"
    → for the PET both terms contribute. §3.4 authors no equivalent sentence
      for the BOSS.
  docs/01-game-design/BOSS_RULES.md §6.3.1 items 1-3 (lines 352, 357, 362):
    "Base Damage: 150" / "120" / "100" — flat values, no composition
    statement, and no ATK-scaling declaration.

  IMPLEMENTATION EVIDENCE (established by inspection; NOT a design source,
  and NOT authoritative — recorded only to show the question is live, not
  to settle it):
    src/backend/GameServer.Domain/Bosses/BossDefinition.cs L143-150 states
      the Skill's Base Damage "is BossState.ATK + SkillBaseDamage — a
      separate additive component, not a replacement for ATK", citing
      COMBAT_RULES.md §3 step 1 / §3.4.
    src/backend/GameServer.Application/Battle/BattleStateService.cs L1390-1392
      computes bossAttack = bossState.ATK + bossDefinition.SkillBaseDamage.
    src/backend/GameServer.Domain/Combat/DamagePipeline.cs L359-367 restates
      the same reading.
    → the code implements reading (ii). COMBAT_RULES.md §3.4 does NOT contain
      the sentence the code cites it for. Whether the code or the docs are
      wrong is NOT resolvable from the documents, and TASK-124 must not pick
      a side (AGENTS.md §4).

Conflict / missing information:
    (i)  Boss Skill Step 1 = the Skill's authored Base Damage ALONE
         (the value REPLACES any ATK term) → Flame Burst deals 150.
    (ii) Boss Skill Step 1 = EffectiveBossATK + the Skill's Base Damage
         (both contribute) → Flame Burst deals EffectiveBossATK + 150.
  Neither §3.4 nor §6.3.1 selects (i) or (ii). This is PRE-EXISTING: it was
  surfaced by TASK-123's D-1a, not created by it, and is NOT settled by
  Contradiction A, D-1, D-1a, D-1b, D-1c, D-2*, D-3*, or D-4*.

  NOT a blocker for TASK-124: because ADR-free D-1a's rule ("Rage modifies
  only Boss damage whose Step 1 Attack input is derived from BossState.ATK")
  excludes an independently authored Skill Base Damage under BOTH readings,
  every Rage reconciliation in this task is deterministic either way. This
  STOP therefore does not block the applied contract; it blocks only the
  future Boss Skill damage implementation.

Proposed resolution:
  A separate Product-Owner gameplay decision selecting (i) or (ii), recorded
  against COMBAT_RULES.md §3.4 (the Step-1 composition owner) and
  BOSS_RULES.md §6.3.1. Once decided, the smallest documentation correction
  is one sentence at §3.4 stating the Boss Skill Step-1 composition, plus
  alignment of any contrary reading — a separate task, NOT created here
  (TASK-124 §6 forbids creating a decision task; the requester's instruction
  forbids creating any task).

Waiting for:
  Product Owner decision on the Boss Skill Step-1 composition. No behavior is
  changed until it exists (AGENTS.md §4 step 6). TASK-124 deliberately
  authored NO reading of this composition and left COMBAT_RULES.md §3.4's
  Step-1 composition statement exactly as it was.
```

**Status: DONE** for all reconciliation TASK-124 owns. **One STOP recorded**
(GAP-1 → "STOP CONDITION — GAP-1" above); it is a pre-existing gameplay
question this task was forbidden to decide, not a failure of the applied
contract.

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

### Changed Files

Documentation (4 — canonical owners only):

- `docs/01-game-design/COMBAT_RULES.md` — v1.8 → **v1.9**
  - **§4 self-description line** — widened from "canonical owner of the Shield
    rule" to cover the Heal Resolution step as well.
  - **§4 item 7 (NEW) — Heal Resolution (canonical).** AUTHORS the shared
    mechanism TASK-123 D-2c decided: `Raw Heal → Applicable Heal Modifiers →
    Final Heal Amount → existing MaxHP/overheal clamp (item 1, unchanged) →
    HP update`. States that every Pet-HP healing source passes through it;
    that Thủy Ma's −50% is ONE applicable Heal modifier and not a
    special-cased site (D-2b-site); that the step defines no modifier's own
    magnitude/source/duration (those belong to the creating rule); that the
    mechanism is **Pet-HP-scoped** and does not govern Boss-side HP
    restoration (D-4g); that it does not modify MaxHP or affect Shield
    (D-2b-site); and that it adds no state member, event, SignalR member,
    Redis key, or database column. §4 item 1's clamp is **not** re-ordered or
    reworded.
  - **§2 item 5 (NEW)** — routes the HP-Gem heal pool (the string that
    previously occurred once in all of `docs/` and was never consumed) through
    the §4 item 7 Heal Resolution step, by reference to the unchanged
    `GAME_RULES.md` §17 step 12/step 14 sites. States it is a healing source,
    not a second generation rule.
  - **§5.3.3** — three worked examples ADDED (Battle-Start duration-3 apply
    before the first counted Turn; Boss-side step-18a duration-3 apply; a
    duration-3 refresh), covering D-2c-duration and D-1b. **DR1–DR6 unchanged.**
  - **§5.5 (NEW) — Boss Stat Modifiers (`BuffDebuff` Consumption — Boss
    Side)**, §5.5.1–§5.5.5. AUTHORS the Boss-side counterpart of §5.4 for
    TASK-123 Contradiction A + D-1/D-1a/D-1b/D-1c: the consumption point is
    the Boss Damage Pipeline **Step 1 `Attack` input** as a derived, non-stored
    `EffectiveBossATK` (§5.5.1); the damage scope is only Boss damage whose
    Step-1 input derives from `BossState.ATK` — the basic attack — NOT an
    independently authored Boss Skill Base Damage (§5.5.2); Step 4 is NOT
    touched and §3.4 is referenced, not restated (§5.5.3); the Boss base stat
    is never overwritten and `EffectiveBossATK` is not a state member
    (§5.5.4); duration and reapplication are the **existing** §5.2 item 2 /
    §5.3 DR1–DR6 / `GAME_STATE.md` §2.3.1 item 6 rules, referenced not
    re-authored (§5.5.5). §5.5.2 also carries the explicit GAP-1 composition
    note: the Boss Skill Step-1 composition is **open** and deliberately not
    decided here.
  - **§3.4, §5.4, §5.4.5 — byte-identical, NO EDIT** (D-1 retains §3.4 and
    §5.4.5).

- `docs/01-game-design/BOSS_RULES.md` — v2.4 → **v2.5**
  - **§6 table** — Thủy Ma's trigger cell corrected from the bare
    "Healing received reduced" to "**Battle Start** → healing received
    reduced".
  - **§6.2** — the stale trigger wording **`Passive (always active)` is
    REMOVED** and replaced with **Battle Start** (`PASSIVE_RULES.md` §3's
    existing one-time trigger); the defective citation of an alternate trigger
    that `PASSIVE_RULES.md` §3's closed list does not contain is removed; the
    "always-on effect application is a separate concern" sentence is removed
    (TASK-123 D-2a / D-2b).
  - **§6.2.1–§6.2.4 (NEW)** — records the three decided effect behaviors at
    this document's owner: Hỏa Long Rage (representation, damage scope, apply
    point, duration, reapplication), Thủy Ma healing reduction (Battle Start
    trigger, representation, application site, duration window, reapplication,
    reachability note), Mộc Yêu regeneration (step-18a timing, 5% MaxHP base,
    the `5000 × 5 / 100 = 250` worked example, truncate-toward-zero, the
    `min(CurrentHP + 250, 5000)` clamp, direct authoritative `BossState.HP`
    update with no persistent StatusEffect instance, repetition), and §6.2.4's
    server-authority + **intentional client-invisibility** statement (D-4e /
    D-4f). Mechanics are **referenced** to their owners, not restated.

- `docs/02-technical/GAME_STATE.md` — v2.13 → **v2.14**
  - **§2.4.1** — corrected the stale sentence "For MVP, no content-defined
    Boss applies a Status Effect to itself". It now records that
    content-defined Bosses **do** apply Status Effects to themselves
    (Hỏa Long Rage and Thủy Ma healing reduction both held in
    `BossState.StatusEffects[]` per `BOSS_RULES.md` §6.2), and points at the
    owning gameplay rules (`COMBAT_RULES.md` §5.3, §5.5, §4 item 7). Minimal;
    no structural change. State representation/lifecycle only.

- `docs/02-technical/DATABASE.md` — v1.25 → **v1.26**
  - **§1 item 3's `threshold` note** — corrected the characterization that
    `null` "means the Passive has no threshold and **is always active**";
    `null` now reads as **no match-charging threshold**, with the activation
    trigger defined per Passive by `BOSS_RULES.md` §6.2 (Thủy Ma = Battle
    Start, one-time). The **storage constraint is preserved**: `null` still
    denotes a non-match-charged Passive and `0` is still never the sentinel.
  - **§3 provisioning row-content note** — the "(including `null` for Thủy
    Ma's always-active Passive — `§3`: `threshold = null` ⇔ always-active)"
    clause corrected to the same non-always-active characterization.
  - **§6 constraint list** — the `threshold = null ⇔ always-active` entry
    corrected to `no match-charging threshold (NOT always-active; the trigger
    is defined per Passive, not by null)`.
  - **prior 1.7 history entry** — annotated in place (the file's
    version-history convention preserves prior entries verbatim) to mark the
    superseded "always-active" wording as superseded in 1.26. **No table,
    column, storage member, schema shape, or vocabulary changed.**

Task file (1):

- `tasks/backlog/TASK-124-apply-boss-passive-contract-decisions.md` — Status
  BACKLOG → DONE; the GAP-1 STOP CONDITION and this Completion Evidence
  recorded; moved `backlog/` → `completed/`.

**Zero edits to:** `GAME_RULES.md` (§17 step 18a needed no change — verified
byte-identical), `GAME_EVENTS.md`, `SIGNALR_PROTOCOL.md`, `REDIS_STATE.md`,
`docs/03-decisions/README.md` and `ADR/`, and every `src/`/`tests/` path.

### TASK-123 Decisions Applied

```text
Hỏa Long   Contradiction A  APPLIED — §5.5.1/§5.5.3: Step-1 input via
                            EffectiveBossATK, NOT Step 4; §3.4 and §5.4.5
                            left byte-identical.
           D-1              APPLIED — §6.2.1 + §5.5.1/§5.5.4: TurnBased
                            BuffDebuff in BossState.StatusEffects[],
                            TargetStat "ATK", Magnitude +20%,
                            RemainingTurns 3; BossState.ATK immutable/base;
                            no new BossState field.
           D-1a             APPLIED — §5.5.2: reaches only ATK-derived Boss
                            damage; an independently authored Boss Skill
                            Base Damage is not modified.
           D-1b             APPLIED — §6.2.1 + §5.3.3: apply at step 18a,
                            RemainingTurns 3, non-retroactive, three counted
                            Turns, decrement at step 19a, expiry after the
                            third active Turn's step 19a.
           D-1c             APPLIED — §6.2.1 + §5.5.5: refresh to 3, no
                            stacking, same source identity, at most one
                            instance.
Thủy Ma    D-2a             APPLIED — §6.2 row/prose: "for 3 turns"
                            authoritative; "always active" RETIRED.
           D-2b             APPLIED — §6.2/§6.2.2: Battle Start trigger,
                            existing TurnBased representation, no
                            PassiveTracker.Charge, no match-progress
                            PassiveCharged/PassiveTriggered.
           D-2b-site        APPLIED — §6.2.2 + §4 item 7: applies at the
                            shared Heal resolution point before the overheal
                            clamp; reaches Card Heal and HP-Gem healing;
                            does not modify MaxHP; does not affect Shield.
           D-2c             APPLIED — §4 item 7 AUTHORED (the shared Heal
                            Resolution step) + §2 item 5 (HP-Gem pool
                            routed) + §4 self-description widened.
           D-2c-duration    APPLIED — §6.2.2 + §5.3.3: Battle Start with
                            RemainingTurns 3; active Turns 1-3; application
                            is not a turn; expires before Turn 4.
           D-2d             APPLIED — §6.2.2: refresh to 3, no additive
                            stacking, at most one instance, same source
                            identity; points at §5.2 item 2. Reachability
                            note recorded (unreachable in MVP).
           D-2e             APPLIED — §6.2.2 + §6.2.4: no new event, no
                            emissions, state changes only.
Mộc Yêu    D-3a             APPLIED — §6.2.3: applied during step 18a.
           D-3b             APPLIED — §6.2.3: exactly 5% of MaxHP.
           D-3c             APPLIED — §6.2.3: truncate toward zero.
           D-3d             APPLIED — §6.2.3: min(CurrentHP + RegenAmount,
                            MaxHP); no overheal.
           D-3e             APPLIED — §6.2.3: direct authoritative
                            BossState.HP update; no persistent instance; no
                            new event; §6.2.4 records the visibility
                            limitation as intentional.
           D-3f             APPLIED — §6.2.3: one 5% regeneration per valid
                            activation; no persistent stacking.
Cross-     D-4a             PRESERVED — no new Battle Event; §16 and
cutting                      GAME_EVENTS.md §2 untouched.
           D-4b             PRESERVED — no new SignalR member; bossState NOT
                            added (verified: payload is still battleId/turn/
                            sequence/board/rngSeed/rngState/playerState/
                            petState).
           D-4c             PRESERVED — no new Redis key; REDIS_STATE.md
                            untouched; battle:{battleId}:state remains sole
                            persistence.
           D-4d             PRESERVED — no PostgreSQL schema change; no
                            table, column, or entity added.
           D-4e             APPLIED — §6.2.4: all three effects
                            server-authoritative; no client calculation.
           D-4f             APPLIED — §6.2.4 + §6.2.3: client invisibility
                            recorded as an INTENTIONAL contract limitation,
                            not a defect; no protocol task created.
           D-4g             APPLIED — §4 item 7 and §6.2.3: the Heal
                            Resolution step stays Pet-scoped; Mộc Yêu uses
                            its own direct Boss HP update and does NOT widen
                            D-2c.
```

### Shared Heal Resolution — Result

**AUTHORED at its canonical owner**, `COMBAT_RULES.md` §4 item 7, with the
required order `Raw Heal → Applicable Heal Modifiers → Final Heal Amount →
existing MaxHP/overheal clamp → HP update`. All Pet-HP healing sources pass
through it; **Card Heal** (a Card's authored heal) and **HP-Gem healing** (the
§2 pool, now routed by §2 item 5) are both covered. **Thủy Ma's −50% is one
applicable Heal modifier**, not a special-cased site. §4 item 1's clamp is
unchanged and still runs last. The step is **Pet-scoped** — it explicitly does
not govern Boss-side HP restoration (D-4g), and Mộc Yêu's regeneration is
documented as its own direct `BossState.HP` update. It introduces **no**
`BattleState` member, event, SignalR member, Redis key, or database column,
and no speculative generic abstraction.

### GAP-1 — Boss Skill Step-1 Composition: REMAINS UNRESOLVED (STOP recorded)

I inspected the authoritative evidence directly rather than assuming.
**The existing documentation does NOT determine the answer.**

`COMBAT_RULES.md` §3 step 1 defines Base Damage as a **sum** of contributions
("ATK stat, Skill/Card base value, and any ATK-Gem-generated damage pool"), and
§5.4.1 item 2 spells that sum out **for the Pet**. But §3.4's Boss Skill clause
says only that "Step 1 Base Damage **is defined per Skill**" — it names the
value's *source*, never its *composition* — and `BOSS_RULES.md` §6.3.1 gives
only flat per-Skill values ("Base Damage: 150" / "120" / "100") with no
composition statement and no ATK-scaling declaration. Both readings therefore
remain consistent with the documents:

```text
(i)  Boss Skill Step 1 = the Skill's authored Base Damage ALONE     -> 150
(ii) Boss Skill Step 1 = EffectiveBossATK + the Skill's Base Damage
```

Recorded as context only (not as authority): the current source implements
reading (ii) (`BattleStateService.cs` L1390-1392, `BossDefinition.cs`
L143-150, `DamagePipeline.cs` L359-367) and cites `COMBAT_RULES.md` §3.4 for
it — but §3.4 does not contain the sentence it is cited for. Whether the code
or the docs are wrong is not resolvable from the documents, and per
`AGENTS.md` §4 TASK-124 did not pick a side.

**Disposition:** recorded as an unresolved PRE-EXISTING gameplay decision
(see "STOP CONDITION — GAP-1"). No interpretation was chosen, no behavior was
inferred, no new gameplay rule was authored, **no new decision task was
created**, and no unrelated documentation was modified. `COMBAT_RULES.md` §3.4
was left byte-identical. §5.5.2 carries an explicit composition note telling
the reader the question is open and deliberately not decided here.

**Not a blocker for this task:** because D-1a excludes an independently
authored Skill Base Damage under **both** readings, every applied Rage rule is
deterministic either way.

### GAP-2 … GAP-5 — Disposition

```text
GAP-2  DATABASE.md pointer to the retired "always-active" wording
       -> RESOLVED. Three stale characterizations corrected; the storage
          constraint itself preserved (no schema redesign).

GAP-3  GAME_STATE.md §2.4.1's "no content-defined Boss applies a Status
       Effect to itself"
       -> RESOLVED. Sentence corrected minimally.

GAP-4  Client visibility of BossState
       -> RECORDED, not "fixed" (by design). Recorded in §6.2.4 as an
          INTENTIONAL contract limitation (D-4f). No wire member added; no
          protocol task created.

GAP-5  Boss-side heal vs D-2c's Pet-scoped Heal Resolution
       -> RESOLVED FOR THIS TASK by D-4g. §4 item 7 is explicitly Pet-scoped
          and §6.2.3 states Mộc Yêu uses its own direct Boss HP update. The
          remaining design question (a target-agnostic Heal Resolution) is
          explicitly left to a FUTURE task and was NOT resolved here.
```

### ADR

**No ADR created — verified, not assumed.** Checked
`docs/03-decisions/README.md` §8 ("Known Open Items (Not ADRs)") and the
existing ADR set: no applied decision introduces a battle-state concept,
persistence-strategy change, realtime-strategy change, or module-boundary
change. Rage reuses the **existing** `BossState.StatusEffects[]` collection,
element schema, and Turn-countdown model (the `GAME_STATE.md` §2.4.5 Stun
precedent; the TASK-119 precedent), and D-2c authors a **combat-rule
mechanism**, not a state/persistence/realtime concept. Multiple documents
changing is not itself grounds for an ADR (`AGENTS.md` §18). No Stop
Condition 7 fired.

### Validation Results

```text
Contradictory Rage definitions        PASS — one definition, at its owners
                                      (COMBAT_RULES.md §5.5 for the combat
                                      rule; BOSS_RULES.md §6.2.1 for the
                                      per-Boss row). §6.2.1 points at §5.5
                                      and §5.3 rather than restating.
Contradictory Thủy Ma definitions     PASS — one trigger (Battle Start), one
                                      duration (3 turns, Turns 1-3), one
                                      application site (§4 item 7). The
                                      retired "Passive (always active)" text
                                      survives ONLY inside the BOSS_RULES
                                      version note and DATABASE.md's
                                      superseded-history annotation, both of
                                      which explicitly mark it as the
                                      corrected/retired wording — no current
                                      rule states it.
Contradictory Mộc Yêu definitions     PASS — one definition at §6.2.3
                                      (timing/base/rounding/clamp/
                                      observability/repetition).
Cross-references resolve to owners    PASS — every pointer cites a section
                                      that carries the rule; no pointer
                                      cites the retired wording. §5.5.2 and
                                      §6.2.1 reference §3.4/§5.4.5 rather
                                      than restating.
No new Battle Event names             PASS — zero occurrences of
                                      BossPassiveApplied/BossPassiveExpired/
                                      HealingReduced/BossRageApplied/
                                      BossRegenerated as event names; the
                                      only hits are §6.2.2's explicit
                                      prohibitions ("there is no ... event").
                                      GAME_RULES.md §16 and GAME_EVENTS.md §2
                                      untouched.
No SignalR payload expansion          PASS — BattleStateUpdated is still
                                      (battleId, turn, sequence, board,
                                      rngSeed, rngState, playerState,
                                      petState); no bossState member.
                                      SIGNALR_PROTOCOL.md not written.
No new BattleState/BossState member   PASS — GAME_STATE.md §2.3.1's
                                      StatusEffect member set and §2.4's
                                      BossState tree are unchanged; only
                                      §2.4.1's prose sentence changed.
No Redis key / schema change          PASS — REDIS_STATE.md untouched;
                                      battle:{battleId}:state still the sole
                                      key. DATABASE.md gained no table,
                                      column, member, or entity; only
                                      characterization prose changed.
No source files modified              PASS — aggregate src/ hash
                                      778AC89E...1A9D11 identical before and
                                      after; tests/ hash 52CEE2DB...D69916E
                                      identical before and after.
TASK-123 unmodified                   PASS — SHA256 913F4823...3A4255
                                      identical; still at tasks/blocked/;
                                      Status unchanged.
Version-note convention followed      PASS — COMBAT_RULES.md 1.8->1.9,
                                      BOSS_RULES.md 2.4->2.5, GAME_STATE.md
                                      2.13->2.14, DATABASE.md 1.25->1.26;
                                      each preserves its prior-version chain.
Duplicate-definition re-read          PASS — each changed owner re-read
                                      together with its referencing docs
                                      (documentation-change.md §1 final
                                      step); no rule written twice.
§3.4 / §5.4.5 byte-identical          PASS — D-1 retains both.
ELEMENT_RULES.md boundary preserved   PASS — §4 item 7 states the Element
                                      Modifier does not apply to non-damage
                                      effects, so no elemental interaction
                                      for healing is introduced.
Code tests                            N/A — documentation-only. No test
                                      reads docs/, so this change cannot
                                      affect the suite; no source was
                                      compiled or executed (AGENTS.md §15 /
                                      documentation-change.md §4).
```

### Scope Verification

- [x] Documentation only — zero files under `src/` or `tests/` modified
      (verified by pre/post aggregate hash)
- [x] `TASK-123` byte-identical (unmodified, unmoved, not re-statused)
- [x] `TASK-119`, `TASK-118`, `TASK-022` and all completed tasks unmodified
- [x] No new event, SignalR member, BattleState member, Redis key, or
      PostgreSQL column introduced
- [x] No ADR created
- [x] No rule duplicated across documents; every cross-reference points at
      the canonical owner
- [x] No balance value authored or changed (`+20%`, `3 turns`, `−50%`,
      `5% MaxHP`, MaxHP 5000 are carried, not chosen)
- [x] No implementation task created
- [x] Adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] GAP-1 recorded as an unresolved pre-existing gameplay decision; no
      reading invented and no task created for it
- [x] Boss Passive step 18a remains UNIMPLEMENTED (no source touched)
