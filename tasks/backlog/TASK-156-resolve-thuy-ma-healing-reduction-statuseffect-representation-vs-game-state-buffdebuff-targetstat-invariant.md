# TASK-156 — Resolve the Thủy Ma Healing-Reduction Representation Against the `GAME_STATE.md` §2.3.1 `BuffDebuff` ↔ `TargetStat` Invariant

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, schemas, or contracts.

  THIS TASK DECIDES NOTHING BY ITSELF AND IMPLEMENTS NO BEHAVIOR. It presents
  the documented evidence of a genuine authoritative contract contradiction and
  requires the appropriate human/Product-Owner decision, then records it.
  Choosing a representation, authorizing a TargetStat value, relaxing the
  §2.3.1 invariant, or re-typing the instance is the single prohibited action of
  this task (AGENTS.md §4, §7, §20).

  PROVENANCE: TASK-153 was READY, transitioned READY → IN PROGRESS, and began
  implementing the three MVP Boss Passive effects at GAME_RULES.md §17 step 18a.
  GAP-5 had been discharged by TASK-154 (DECIDED) and TASK-155 (DONE), and all
  twelve TASK-153 Unblock Criteria were satisfied. Execution then STOPPED on a
  SECOND, INDEPENDENT contradiction — see tasks/blocked/TASK-153-*.md "Second
  Stop Condition Report". No source, test, or docs change was made; the
  attempted edits were reverted and the tree is clean.

  THIS IS NOT GAP-5. GAP-5 was the carrier/consumption split (where does the
  instance live, and how does a Pet-scoped step read it). That is decided and
  applied: BossState.StatusEffects[] carrier, Id = "boss-thuy-ma-heal",
  Pet HP healing only, Battle Start trigger, RemainingTurns = 3, Turns 1–3,
  expiry before Turn 4, authorized cross-entity read, −50% Raw Heal modifier,
  no consumption by healing, refresh-not-stack. NONE of that is reopened here.

  THE NEW PROBLEM IS ONE STEP FURTHER DOWN. BOSS_RULES.md §6.2.2 now requires
  the instance to be Type = BuffDebuff carrying NO TargetStat, while
  GAME_STATE.md §2.3.1 defines TargetStat as exactly the BuffDebuff member and
  pairs the two as an invariant (§2.3.1 item 7), enforced by
  StatusEffect.TurnBased. So the documented instance is UNREPRESENTABLE in the
  documented state model — observed at runtime as
  ArgumentNullException: Value cannot be null. (Parameter 'targetStat').

  THIS IS A STOP-DRIVEN TASK, NOT A FEATURE. Per AGENTS.md §20, "Rule conflict",
  "Missing rule", and "Ambiguous requirement" are stop conditions.

  BOUNDARY: documentation only. Zero files under src/ or tests/. This task
  authors NO rule and modifies NO authoritative document — it records the
  Product Owner's decision in this file. The subsequent documentation task
  (see "Blocks") determines which authoritative documents must change and
  applies them. No ADR is created here unless the recorded answer requires one
  (reported, not authored — AGENTS.md §18).
-->

---

## Metadata

```text
Task ID:           TASK-156
Type:              GAMEPLAY-CHANGE (TASK_TYPES.md §2 — "A gameplay mechanic
                   needs to behave differently than the documentation
                   currently says, OR a new undocumented mechanic is being
                   authorized." The second case applies: the already-decided
                   Thủy Ma effect cannot be expressed by the documented state
                   model, so a representation rule that docs/ does not
                   currently contain must be authored by the Product Owner
                   before any implementation may proceed.)
                   See "Type classification note" below — this task is the
                   DECISION-RECORDING half of GAMEPLAY-CHANGE per
                   development/gameplay-change.md §3; the documentation-apply
                   half is a separate task.
Status:            DECIDED — the Product Owner supplied a complete decision
                   covering all 14 required coverage items; it is recorded in
                   the "Decision Record" section below. The decision is
                   Option B (keep Type = BuffDebuff with TargetStat absent,
                   and explicitly relax the GAME_STATE.md §2.3.1 item 7
                   invariant for rule-consumed BuffDebuffs), with every
                   TASK-154 GAP-5 decision otherwise preserved and
                   TASK-154 D-10 explicitly changed (no new TargetStat value
                   is introduced).
                   The recorded decision is the DECISION-INPUT half only. No
                   authoritative document was modified by this task and no
                   implementation was performed. The documentation-apply task
                   (see "Blocks") applies it at its canonical owners and must
                   be created by the Orchestrator. TASK-153 remains BLOCKED
                   until that documentation update lands.
Risk:              HIGH (TASK_TYPES.md §4 — GAMEPLAY-CHANGE baseline HIGH,
                   "Always HIGH — game rule changes are the riskiest
                   category". This touches the Status Effect instance
                   invariant in GAME_STATE.md §2.3.1, the Boss Status Effect
                   carrier, the Thủy Ma effect contract, and the enforced
                   domain model in StatusEffect.cs.)
Priority:          HIGH (ROADMAP.md §1 Phase 1 requires "3 MVP Bosses
                   (Hỏa Long, Thủy Ma, Mộc Yêu — Passive + Skill each)" and
                   "Boss Response (Passive → Skill → Attack →
                   Victory/Defeat)". The Skill half landed in TASK-118. The
                   Passive half is blocked: TASK-153 cannot leave BLOCKED and
                   the Thủy Ma effect cannot be implemented until this
                   representation contradiction is resolved and the
                   authoritative documentation is updated.)
Primary Agent:     gameplay (TASK_TYPES.md §5 — Boss domain → Gameplay;
                   AGENT_SELECTION.md §1 — gameplay rule change → Gameplay
                   Agent)
Supporting Agents: backend (GAME_STATE.md §2.3.1 owns the Status Effect
                   instance schema and the TargetStat-iff-BuffDebuff
                   invariant; §2.4/§2.4.1 own the BossState carrier;
                   src/backend/GameServer.Domain/Battle/StatusEffect.cs
                   enforces the pairing — consulted to state accurately what
                   the existing representation does and does not permit, NOT
                   to choose an option),
                   review (documentation consistency and cross-document
                   conflict verification across BOSS_RULES.md,
                   COMBAT_RULES.md, and GAME_STATE.md)
Workflow:          development/gameplay-change.md
Skills:            gameplay/gameplay-behavior-derivation,
                   discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/scope-validation,
                   quality/documentation-consistency
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-153 (BLOCKED — the implementation attempt whose SECOND
                     STOP produced this task. IMMUTABLE; READ-ONLY; must NOT
                     be modified, re-statused, moved, or rewritten),
                   TASK-154 (DECIDED — the GAP-5 Product Owner decision record.
                     IMMUTABLE; read-only; must NOT be reopened or modified.
                     Its D-10 is the item this task may cause to change, and
                     only the Product Owner may change it),
                   TASK-155 (DONE — applied the GAP-5 decision and authored the
                     Id-based selector. IMMUTABLE; read-only; must NOT be
                     modified),
                   TASK-124 (DONE — recorded GAP-5 as deferred; the precedent
                     for a stop-driven decision task. IMMUTABLE; read-only),
                   TASK-123 (DONE — the step-18a decision set. IMMUTABLE;
                     read-only)
Blocks:            (1) TASK-153 leaving BLOCKED and being implemented;
                   (2) the authoritative-documentation task that applies the
                     recorded decision — it cannot be created until this
                     decision exists;
                   (3) the Thủy Ma half of the MVP Boss Passive set;
                   (4) ROADMAP.md §1 Phase 1 "Boss Response" completeness for
                     the 3 MVP Bosses.
Estimate:          Simple–Normal (present the contradiction, obtain one
                   decision covering 14 coverage items, record it; no code, no
                   tests, no documentation edit)
```

**Type classification note.** `GAMEPLAY-CHANGE`. Per
`development/gameplay-change.md` §2, the first branch is "does the mechanic
already exist as requested?" — it does not, because `BOSS_RULES.md` §6.2.2
requires a `BuffDebuff`-typed instance with no `TargetStat`, which
`GAME_STATE.md` §2.3.1's invariant forbids. Per §3, a task that "explicitly
authorizes a design change" then proceeds
`Design change → update authoritative documentation → …`. The repository's
precedent (TASK-123 → TASK-124, TASK-150 → TASK-151, TASK-154 → TASK-155)
splits that sequence into a **decision-recording** task and a
**documentation-apply** task, because the decision input is a human Product
Owner answer, not an agent act. This task is the first half. It is **not**
`DOCUMENTATION`: no document is edited here.

**Not an implementation task.** If, while recording the decision, an agent
finds the decision genuinely incomplete or internally inconsistent, the correct
action is this task's Stop Conditions — not to fill the gap by inference.

---

## Objective

Obtain and record an explicit Product Owner decision that determines how Thủy
Ma's healing-reduction effect (`BOSS_RULES.md` §6.2.2) is represented in
authoritative battle state such that the already-decided GAP-5 contract remains
intact while the authoritative `GAME_STATE.md` §2.3.1 status-effect invariants
remain internally consistent — resolving the contradiction that stopped
TASK-153 and unblocking the authoritative-documentation update that must precede
any implementation.

---

## Context

The decision chain is complete everywhere except this one representation
question. Nothing upstream of it is in doubt.

```text
TASK-123  DONE   decided the step-18a contract (D-1 … D-n)
    ↓
TASK-124  DONE   applied it to BOSS_RULES.md §6.2.1–§6.2.4 and the canonical
                 owners; recorded GAP-5 as explicitly deferred
    ↓
TASK-127  DONE   closed GAP-1 (step 18b Step-1 composition)
    ↓
TASK-154  DECIDED the GAP-5 Product Owner decision (D-1 … D-12, Option B):
                 BossState.StatusEffects[] carrier + explicitly authorized
                 cross-entity read by the Pet-side Heal Resolution step
    ↓
TASK-155  DONE   applied it at its canonical owners; authored the
                 applicable-instance selector Id = "boss-thuy-ma-heal"
    ↓
TASK-153  READY → IN PROGRESS   all twelve Unblock Criteria satisfied
    ↓
TASK-153  BLOCKED  ← execution STOPPED on the contradiction this task exists
                     to resolve
    ↓
TASK-156  this task — decision input only
```

The three MVP Boss Passive effects are Hỏa Long's Rage (`BOSS_RULES.md`
§6.2.1), Mộc Yêu's regeneration (§6.2.3), and Thủy Ma's healing reduction
(§6.2.2). (1) and (3) were independently assessed by TASK-153 as fully
specified and implementable. (2) is the one this contradiction blocks.

---

## Discovered Contradiction

Two authoritative documents require mutually incompatible representations of
the same documented instance.

### Side 1 — `docs/01-game-design/BOSS_RULES.md` §6.2.2

As applied by TASK-155, §6.2.2 requires the instance to be a
**`BuffDebuff`**-typed Status Effect carrying **no `TargetStat`**:

```text
§6.2.2 "Representation"
  "the existing Turn-based Buff/Debuff Status Effect model
   (GAME_STATE.md §2.3.1), held in BossState.StatusEffects[]"

§6.2.2 "Applicable-instance selector"
  "This is a `BuffDebuff`-typed instance in the documented element model, and
   it is **not** a `TargetStat`-consumed `BuffDebuff`: it is not consumed by a
   stat rule, and COMBAT_RULES.md §5.4.5 / §5.5.3 consequently open no new
   non-`"ATK"` `TargetStat` case for it."
```

Reinforced at `docs/01-game-design/COMBAT_RULES.md` §5.4.5 and §5.5.3, which
each record that Thủy Ma's healing reduction is **not** a `TargetStat`-consumed
`BuffDebuff` and that **no `TargetStat` value is added for it**.

### Side 2 — `docs/02-technical/GAME_STATE.md` §2.3.1

§2.3.1's schema defines `TargetStat` as **exactly** the `BuffDebuff` member, and
item 7 fixes the pairing as an invariant:

```text
§2.3.1 schema
  "├── TargetStat  (string, optional — the modified stat for Type =
   │               "BuffDebuff", e.g. "ATK"; absent otherwise)"
  "└── ExpiryCondition (string, optional — ... present iff RemainingTurns is
                       absent)"

§2.3.1 item 7 — "Absence conventions."
  "`TargetStat` and `ExpiryCondition` are absent when they do not apply
   (never `null`, never a sentinel string)"
```

`docs/01-game-design/COMBAT_RULES.md` §5.1 defines the whole type by that
member: Buff/Debuff is a "temporary stat modification (ATK/DEF/Crit/etc.)", and
"how a `Magnitude` reaches the stat its `TargetStat` names is owned by §5.4".

### The two sides, as one statement

```text
BOSS_RULES.md §6.2.2   →   Type = BuffDebuff   AND   TargetStat = absent
GAME_STATE.md §2.3.1   →   Type = BuffDebuff   ⇒     TargetStat = present
                                                      ("the modified stat for
                                                        Type = BuffDebuff")
```

**The documented instance cannot be constructed.** The conflict is not merely
documentary — the state model **enforces** the pairing, so the contract's own
choice is unrepresentable:

```text
src/backend/GameServer.Domain/Battle/StatusEffect.cs, TurnBased(), lines 251-257
  // §2.3.1 item 7: TargetStat is "present iff Type = BuffDebuff". Both
  // halves of the pairing are enforced, so neither a BuffDebuff without its
  // stat nor another type carrying one can be represented.
  if (type == StatusEffectType.BuffDebuff)
  {
      ArgumentException.ThrowIfNullOrWhiteSpace(targetStat);
  }
```

Observed at runtime during the attempted implementation:

```text
System.ArgumentNullException : Value cannot be null. (Parameter 'targetStat')
   at GameServer.Domain.Battle.StatusEffect.TurnBased(...)
   at GameServer.Domain.Battle.BossPassiveEffects.ApplyBattleStartEffects(...)
```

### Why this is not an implementation detail

Every escape route available under the **current** contract is closed:

```text
(a) Give the instance a TargetStat value (e.g. "HEAL")
    → FORBIDDEN. TASK-154 D-10 forbids a new TargetStat value;
      COMBAT_RULES.md §5.4.5 / §5.5.3 define the "ATK" case ONLY and state any
      other stat "would require its own recorded decision before it could be
      implemented"; and BOSS_RULES.md §6.2.2 itself states this effect "is
      **not** a `TargetStat`-consumed `BuffDebuff`" and "opens no new
      non-`"ATK"` `TargetStat` case". Choosing one = making a new gameplay
      decision (AGENTS.md §7, §20).

(b) Omit TargetStat and construct the instance anyway
    → IMPOSSIBLE. StatusEffect.TurnBased rejects it by construction
      (§2.3.1 item 7's pairing, enforced). This is what actually happened.

(c) Change the instance's Type to one that permits an absent TargetStat
    → NO SUCH TYPE IS CURRENTLY DEMONSTRATED. §2.3.1 item 3 fixes the
      vocabulary to DoT | BuffDebuff | Shield | State, and each other member
      is already claimed by another meaning:
        DoT     — COMBAT_RULES.md §5.1/§5.2 item 3: a damage-over-time tick.
                  This effect deals no damage and ticks nothing.
        State   — GAME_STATE.md §2.4.5: Stun, tracked with BossState.State.
                  This effect is not a state transition.
        Shield  — COMBAT_RULES.md §4 items 2-5: a trigger-based absorption
                  pool. This effect is Turn-based and absorbs nothing.
      Whether any existing Type can *semantically* represent this modifier is
      exactly what Option C below asks the Product Owner to determine; an agent
      must not assume one is valid.

(d) Relax the pairing so a BuffDebuff may omit TargetStat
    → FORBIDDEN HERE. That changes GAME_STATE.md §2.3.1 item 7, which TASK-154
      D-10 and TASK-155 BOTH explicitly record as UNCHANGED. It is a
      state-contract change requiring its own decision, not an implementation
      choice.
```

This is a **rule conflict between two authoritative documents** — `AGENTS.md`
§20's "Rule conflict" and "Architecture conflict" — not an implementation
detail. Per `AGENTS.md` §4, an agent must not pick a side.

### What is NOT in dispute

This task does **not** reopen GAP-5. The following decisions are settled, are
recorded in `BOSS_RULES.md` §6.2.2 as applied by TASK-155, and are preserved
unless the Product Owner explicitly changes them (see "Preserved Existing
Decisions"):

```text
BossState.StatusEffects[] carrier
Id = "boss-thuy-ma-heal" identity
Pet HP healing only
Battle Start trigger
RemainingTurns = 3 / Turns 1–3 / expiry before Turn 4
authorized cross-entity read by the Pet-side Heal Resolution step
−50% as one applicable Heal modifier on the Raw Heal
not consumed by healing
refresh to full duration / no stacking
no new event, wire member, Redis key, or database column
```

The new question is **only**: given those decisions, how may the instance be
represented while `GAME_STATE.md` §2.3.1's invariants hold?

---

## Decision Question

> **How should the Thủy Ma healing-reduction effect be represented so that the
> existing GAP-5 decisions remain intact while the authoritative
> `GAME_STATE.md` §2.3.1 status-effect invariants remain internally
> consistent?**

---

## Decision Options

Presented as concrete alternatives for the Product Owner to select from, reject,
or replace. Per `AGENTS.md` §4 and the TASK-123/TASK-154 precedent, **these are
evidence only — they are NOT recommendations and they are NOT ranked. An agent
must not preselect, rank, or argue for any of them.**

```text
Option A — Authorize a TargetStat for this effect

    Allow      Type = BuffDebuff
               TargetStat = <explicit healing-related value>

    This requires the Product Owner to decide whether a new TargetStat
    vocabulary is permitted at all, and if so its EXACT value and its EXACT
    meaning (what the value names, and how a Magnitude reaches it).

    THIS OPTION WOULD CHANGE THE PREVIOUS TASK-154 D-10 DECISION, which
    currently reads "No new `TargetStat` value is introduced" and confirms
    §2.3.1 item 7's pairing unchanged. It would also exercise the boundary
    COMBAT_RULES.md §5.4.5 / §5.5.3 currently record as "neither weakened nor
    exercised". The Product Owner must state explicitly that D-10 is being
    changed, and to what.

Option B — Relax the BuffDebuff / TargetStat invariant

    Keep       Type = BuffDebuff
               TargetStat = absent

    for this documented class of healing modifiers, and EXPLICITLY CHANGE the
    invariant stated in GAME_STATE.md §2.3.1 item 7 (and its schema line).

    The Product Owner must decide all three of:
      - whether the exception is GENERIC (any BuffDebuff whose Magnitude is
        consumed by a non-stat rule may omit TargetStat) or
        THỦY-MA-SPECIFIC (this single documented effect only);
      - the EXACT invariant wording that replaces item 7's pairing statement,
        including whether the schema line's "the modified stat for Type =
        `BuffDebuff`" wording changes;
      - whether OTHER existing BuffDebuff effects (e.g. Root's
        `TargetStat = "ATK"`, Hỏa Long's Rage's `TargetStat = "ATK"`) remain
        governed by the pairing unchanged.

    This option changes a technical state-contract invariant and the enforced
    domain model (StatusEffect.TurnBased). It is a decision about
    GAME_STATE.md, not only about Thủy Ma.

Option C — Use another existing StatusEffect type

    Determine whether an existing type — DoT | Shield | State — can
    semantically represent the Thủy Ma modifier without violating its
    documented meaning.

    DO NOT ASSUME ONE IS VALID. Each is currently claimed by a different
    documented meaning (see the closed-route list above). If the Product Owner
    selects this option, the Product Owner must EXPLICITLY APPROVE the semantic
    mapping: which Type, why its documented meaning admits a non-stat,
    non-damage, non-absorption, non-state Turn-based healing modifier, and what
    (if anything) changes in §2.3.1 item 3's "Type selects exactly one duration
    model" statement and in COMBAT_RULES.md §5.1's Type definitions.

Option D — Introduce another representation

    Only if the Product Owner wants a representation OUTSIDE the existing
    StatusEffect type vocabulary.

    This must be treated as a NEW architectural/gameplay decision, with its
    own consequences for the state model, the lifecycle, the cross-entity read
    the GAP-5 decision authorized, and possibly an ADR (AGENTS.md §18).

    DO NOT DEFINE THE REPRESENTATION IN THIS TASK. If this option is selected,
    the Product Owner states the representation's requirements and this task
    records them; the representation itself is authored by the subsequent
    documentation task or by the ADR task it requires — not here.

Option E — An answer this list does not anticipate.
```

---

## Required Decision Coverage

The recorded decision must explicitly determine **all** of the following. An
answer that leaves any item unresolved is incomplete and triggers this task's
Stop Conditions. An agent must not fill any item by inference.

```text
D-1  Representation
     Which representation does the Product Owner select (Option A, B, C, D, or
     a stated alternative)? State it explicitly. An answer that describes the
     effect without naming a representation does not answer D-1.

D-2  Type
     What is the instance's `Type`? One of the §2.3.1 item 3 vocabulary
     (DoT | BuffDebuff | Shield | State), or a changed/new vocabulary. If the
     vocabulary changes, state the exact new set and where it is owned.

D-3  Target / affected domain
     Which entity owns the instance and which entity's domain it affects.
     The GAP-5 decision fixes Boss-owned / Pet HP healing only; confirm, or
     state explicitly what changes.

D-4  TargetStat semantics, if applicable
     If the selected representation carries a `TargetStat`: its EXACT value and
     its EXACT meaning — what it names, and how a `Magnitude` reaches it. If the
     selected representation carries NO `TargetStat`: state that explicitly and
     resolve §2.3.1 item 7's absence convention for it ("absent when it does not
     apply — never `null`, never a sentinel string"). An answer that is silent
     on `TargetStat` leaves D-4 unanswered.

D-5  Identity
     The instance's identity / selector. The GAP-5 decision fixed
     `Id = "boss-thuy-ma-heal"` (TASK-155's authored selector). Confirm it
     remains the selector, or state the replacement. Note §2.3.1 item 6 makes
     `Id` the collection's uniqueness key and therefore the thing a refresh
     targets.

D-6  Carrier
     Where the instance lives (`BossState.StatusEffects[]` under GAP-5 D-1).
     Confirm, or state explicitly what changes. A carrier change re-opens the
     GAP-5 read boundary and must be stated as such.

D-7  Duration
     `RemainingTurns = 3`; Battle Start application consumes no duration unit;
     active Turns 1–3. Confirm, or state explicitly what changes.

D-8  Lifecycle / expiry
     Decrement at the existing step 19a boundary; expires before Turn 4; a
     stored zero is never active (§2.3.1 item 8); removed only by the existing
     turn-duration lifecycle. Confirm, or state explicitly what changes.

D-9  Heal Resolution integration
     How the −50% reaches Pet healing: the authorized cross-entity read at
     §4 item 7's Applicable Heal Modifiers stage, applied to the Raw Heal,
     before item 1's clamp, one-directional and non-mutating, not consumed by
     healing. Confirm, or state explicitly what changes — INCLUDING whether the
     selected representation invalidates the GAP-5 D-5 read authorization
     (e.g. if the effect is no longer stat-consumed, does the read still select
     as documented?).

D-10 Reapplication
     Refresh the existing instance to the full 3-turn duration; no additive
     stack; at most one instance; same source identity. Confirm, or state
     explicitly what changes.

D-11 Interaction with existing GAME_STATE invariants
     Item-by-item: which of §2.3.1's items (1, 2, 3, 6, 7, 8, 12), §2.3.3's
     PendingStatusEffects[] prohibition, §2.4/§2.4.1's Boss carrier contract,
     §5.1.1's lifecycle, and §0 item 5's one-representation-per-fact rule
     remain in force UNCHANGED, and which — if any — are CHANGED. This item
     must be answered explicitly for each; a blanket "unchanged" with a
     selected representation that cannot satisfy §2.3.1 item 7 is
     self-contradictory and is a Stop Condition.

D-12 Whether TASK-154 D-10 is superseded/changed
     TASK-154 D-10 currently reads "No new `TargetStat` value is introduced"
     and confirms §2.3.1's invariants unchanged. State explicitly whether D-10
     is (i) unchanged, (ii) changed — and to exactly what, or (iii) superseded.
     This item must be answered even if the answer is "unchanged"; silence is
     not an answer, because the selected representation may imply a change the
     Product Owner did not intend.

D-13 Whether any other existing BuffDebuff semantics are affected
     Does the decision affect any other Status Effect — Root's and Hỏa Long's
     Rage's `TargetStat = "ATK"` instances, Burn, Shield, Stun, or the Pet-side
     §5.4 / Boss-side §5.5 consumption rules? State explicitly which are
     affected and how, or state explicitly that none are. Consider the effect
     of a generic (rather than Thủy-Ma-specific) exception under Option B.

D-14 Documentation consequences
     Which authoritative documents/sections must change, and does any existing
     invariant cease to hold? At minimum evaluate: GAME_STATE.md §2.3.1 (and
     whether §2.4.1 changes), BOSS_RULES.md §6.2.2, COMBAT_RULES.md §4 item 7 /
     §5.1 / §5.4.5 / §5.5.3, GAME_RULES.md §16/§17/§18, GAME_EVENTS.md,
     SIGNALR_PROTOCOL.md, REDIS_STATE.md, DATABASE.md. State whether an ADR is
     required (reported, not authored — AGENTS.md §18).
```

### Values that must NOT be invented

```text
Thủy Ma Healing Reduction = −50%
```

is the only documented magnitude. It must not be changed, re-derived, or
rounded by this task unless the Product Owner explicitly changes it. The 3-turn
duration, the Battle Start trigger, and the "no new event or protocol" boundary
(`BOSS_RULES.md` §6.2.2) are likewise existing documented values. A
`TargetStat` value for healing must **not** be invented, and the §2.3.1 item 7
invariant must **not** be relaxed, by any agent acting on this task.

---

## Preserved Existing Decisions

Unless the Product Owner **explicitly** changes them, the following GAP-5
decisions remain in force. This task does not reopen them and does not restate
their owners' content:

```text
Carrier             BossState.StatusEffects[]                  (TASK-154 D-1)
Identity            Id = "boss-thuy-ma-heal"                    (TASK-155)
Target              Pet HP healing only                         (TASK-154 D-3)
Trigger             Battle Start, one-time, before Turn 1       (unchanged)
Duration            RemainingTurns = 3; Turns 1–3               (TASK-154 D-4)
Expiry              step 19a decrement; expires before Turn 4   (TASK-154 D-4)
Heal integration    −50% on Raw Heal as ONE applicable modifier,
                    before item 1's clamp, clamp still last     (TASK-154 D-6)
Cross-entity read   Pet-side step → Boss-held instance,
                    one-directional, non-mutating               (TASK-154 D-5)
Consumption         not consumed by healing; persistent /
                    Turn-based; removed only by the lifecycle   (TASK-154 D-7)
Reapplication       refresh to full duration; no stack;
                    one instance; same identity                 (TASK-154 D-8)
Scope               COMBAT_RULES.md §4 item 7 remains
                    "Scope — Pet HP only" (not widened)         (TASK-154 D-11)
No new event        no Battle Event, SignalR member, Redis key,
                    or database column                          (BOSS_RULES.md §6.2.2)
```

The single item this task may cause to change is **TASK-154 D-10** (and, through
it, `GAME_STATE.md` §2.3.1 item 7 if Option B is selected) — and only by an
explicit Product Owner statement recorded at coverage item D-12.

Do not reopen unrelated TASK-154 decisions. Do not re-litigate GAP-5.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — confirm Thủy Ma, the Boss Passive
  system, and healing remain IN scope; §2/§4 — the decision must not introduce
  an OUT-of-scope system
- `docs/01-game-design/BOSS_RULES.md` **§6.2.2** — the contested contract:
  magnitude (`50%`), duration (`3 turns`), the **Battle Start** trigger, the
  "Turn-based Buff/Debuff Status Effect model … held in
  `BossState.StatusEffects[]`" representation, the `Id`-based
  applicable-instance selector and its "**not** a `TargetStat`-consumed
  `BuffDebuff`" statement, the authorized cross-entity read, the
  "Where the −50% is applied" ordering, the persistent/Turn-based
  non-consumption statement, the reapplication rule, and the "no new event or
  protocol" boundary. **This is one side of the contradiction.**
- `docs/01-game-design/BOSS_RULES.md` **§6.2** (trigger table), **§6.2.1**
  (Hỏa Long Rage — the contrasting `TargetStat = "ATK"` instance),
  **§6.2.3** (Mộc Yêu regeneration — deliberately outside Heal Resolution),
  **§6.2.4** (server authority and client-invisibility), **§6.3.1** (the
  per-Boss declaration pattern), **§6.4** (Identity Contract — Thủy Ma's
  recorded `PassiveId`), **§3.3** (step-18a timing)
- `docs/02-technical/GAME_STATE.md` **§2.3.1** — the Status Effect instance
  schema; its `TargetStat` line ("the modified stat for Type = `BuffDebuff`,
  e.g. `"ATK"`; absent otherwise"); **item 1** (`Id` is an identity, not a
  definition), **item 2** (`Magnitude` typed but not interpreted; the
  `BuffDebuff` interpretation is by entity, via `COMBAT_RULES.md` §5.4/§5.5.1),
  **item 3** (the exclusive duration models and the `DoT | BuffDebuff |
  Shield | State` vocabulary), **item 6** (at most one instance per identity;
  `Id` is the uniqueness key), **item 7** (the **`TargetStat`-iff-`BuffDebuff`
  pairing** and the absence convention), **item 8** (a stored zero is never
  active), **item 12** (the section adds no gameplay rule). **This is the
  other side of the contradiction.**
- `docs/02-technical/GAME_STATE.md` **§2.3** (`PetState.StatusEffects[]`),
  **§2.3.3** (the `PendingStatusEffects[]` / queued-collection prohibition),
  **§2.4** / **§2.4.1** (the `BossState` tree and the Boss Status Effect
  carrier contract), **§2.4.5** (Stun and `BossState.State`), **§5.1** (the
  single write-back), **§5.1.1** (the Status Effect mutation lifecycle),
  **§0 item 5** (one representation per fact)
- `docs/01-game-design/COMBAT_RULES.md` **§5.1** — the Type definitions
  (Buff/Debuff as a "temporary stat modification … the stat its `TargetStat`
  names"); **§5.2 item 1/2/3** (duration models, refresh-not-stack default,
  DoT/Shield exclusions); **§5.3 / §5.3.1–§5.3.4** (DR1–DR6 duration
  consumption and the single step-19a decrement)
- `docs/01-game-design/COMBAT_RULES.md` **§4 item 7** — the **Heal Resolution**
  step, its `Raw Heal → Applicable Heal Modifiers → Final Heal Amount → item 1
  clamp → HP update` order, its "Scope — Pet HP only" clause, and its
  authorized-cross-entity-read bullets; **§4 item 1** (the overheal clamp,
  unchanged and still last)
- `docs/01-game-design/COMBAT_RULES.md` **§5.4 / §5.4.1 / §5.4.5** and
  **§5.5 / §5.5.1 / §5.5.3** — the `TargetStat` consumption rules: the `"ATK"`
  case only, and the boundary that any other stat "would require its own
  recorded decision before it could be implemented"; each section's explicit
  statement that Thủy Ma's healing reduction is not a `TargetStat`-consumed
  `BuffDebuff` and adds no `TargetStat` value
- `docs/01-game-design/GAME_RULES.md` **§17** — the fixed resolution order
  (step 18a Boss Passive, step 19a duration consumption); **§16** — the
  canonical event list; **§18** — server authority
- `docs/01-game-design/PASSIVE_RULES.md` **§3** — the Battle Start one-time
  trigger form
- `docs/02-technical/ARCHITECTURE.md` **§2.1** (Application sequences; Domain
  owns the rules), **§5** (anti-overengineering); `docs/02-technical/TDD.md`
  **§6** (determinism)
- `docs/00-overview/ROADMAP.md` §1 Phase 1 — "3 MVP Bosses (… Passive + Skill
  each)" and "Boss Response (Passive → Skill → Attack → Victory/Defeat)"
- `AGENTS.md` §4 (never silently resolve a conflict), §6 (required reading),
  §7 (invent no rule), §8 (MVP protection), §9 (anti-overengineering),
  §11 (determinism), §12 (domain boundaries), §15 (testing),
  §17 (documentation change rule), §18 (architecture change rule),
  §20 (when AI must stop), §23 (final principle)
- `.ai/README.md` §6 (source-of-truth rule), §13 (stop conditions), §18
  (documentation-update policy)
- `tasks/blocked/TASK-153-implement-mvp-boss-passive-effects-at-step-18a.md` —
  **the STOP report that identified this contradiction**, including the
  observed `ArgumentNullException`, the four closed routes, and the revert
  record. Read-only; must NOT be modified.
- `tasks/backlog/TASK-154-resolve-gap-5-thuy-ma-healing-reduction-representation-and-heal-resolution-boundary.md`
  — the GAP-5 decision record (D-1 … D-12). **D-10 is the item this task may
  cause to change.** Immutable; read-only.
- `tasks/backlog/TASK-155-apply-gap-5-thuy-ma-healing-reduction-decisions-to-authoritative-documentation.md`
  — the documentation apply that authored `Id = "boss-thuy-ma-heal"` and the
  §6.2.2 "not a `TargetStat`-consumed `BuffDebuff`" statements. Immutable;
  read-only.
- `tasks/completed/TASK-124-apply-boss-passive-contract-decisions.md` — the
  documentation-apply precedent and the GAP-5 deferral record
- `tasks/completed/TASK-123-resolve-boss-passive-effect-contract.md` — D-2 and
  its candidate list; the original split
- `tasks/backlog/TASK-150-resolve-powerchanged-source-semantics-for-boss-drain-and-card-power-charge.md`
  — the most recent decision-input task shape and its "Decision Question",
  "Required Decision Coverage", "Decision Options", and "Decision Record"
  sections
- `src/backend/GameServer.Domain/Battle/StatusEffect.cs` (`TurnBased()`, lines
  251-264 — the enforced pairing; `TriggerBased()` — the other duration model),
  `src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs` (the
  Turn-based lifecycle and `Apply` refresh),
  `src/backend/GameServer.Domain/Battle/BossPassiveEffects.cs` (the file
  TASK-153 created and then deleted at the point of failure — it does **not**
  exist in the tree)

---

## Current State

`BossState.StatusEffects[]` exists and is implemented (TASK-095/TASK-096,
`GAME_STATE.md` §2.4.1), and `StatusEffectLifecycle` implements apply / refresh /
consume / expire plus the `EffectiveBossAttack` reader. TASK-155 authored the
Thủy Ma contract at `BOSS_RULES.md` §6.2.2, `COMBAT_RULES.md` §4 item 7 /
§5.4.5 / §5.5.3, and `GAME_STATE.md` §2.4.1.

TASK-153 then began implementation and reached the point of constructing the
Battle Start instance. It failed on the enforced pairing. The following were
**created and then fully reverted**; no source, test, or documentation change
remains:

```text
CREATED then DELETED
  src/backend/GameServer.Domain/Battle/BossPassiveEffects.cs
  src/backend/GameServer.Domain/Battle/HealResolution.cs

MODIFIED then REVERTED (git checkout --)
  src/backend/GameServer.Domain/Bosses/BossDefinition.cs
  src/backend/GameServer.Domain/Bosses/BossDefinitions.cs
  src/backend/GameServer.Domain/Cards/CardCastExecutor.cs
  src/backend/GameServer.Domain/Match3/ResourceGenerator.cs
  src/backend/GameServer.Application/Battle/BattleStateService.cs
  tests/backend/GameServer.Domain.Tests/PlayerEffectHealingTests.cs
```

Today the contract is internally unsatisfiable: `BOSS_RULES.md` §6.2.2 requires
`Type = BuffDebuff` with `TargetStat` absent, `GAME_STATE.md` §2.3.1 requires
`TargetStat` present for every `BuffDebuff`, and `StatusEffect.TurnBased`
enforces the latter. Hỏa Long's Rage and Mộc Yêu's regeneration are unaffected
by this conflict and were assessed as implementable.

---

## Scope

### In Scope

1. Present the contradiction from the authoritative documents
   (§"Discovered Contradiction" above), with both sides cited by path and
   section and the observed runtime failure recorded.
2. Request the Product Owner decision covering all 14 required coverage items.
3. Record that decision verbatim in this task file's "Decision Record" section.
4. Report which authoritative documents the decision will require changing, so
   the follow-up documentation task can be created with an accurate scope.
5. Report whether the decision requires an ADR (`AGENTS.md` §18,
   `architecture/adr-change.md` §2) — reported, not authored.

### Out of Scope

- **Any change under `docs/`.** The decision is recorded in this task file. The
  authoritative-documentation update is a separate, subsequent task (this task
  "Blocks" it). Per `AGENTS.md` §17 and
  `documentation/documentation-change.md` §2, only the canonical owner of a
  concept may define it, and that edit is that task's act. In particular this
  task does **not** modify `GAME_STATE.md`, `BOSS_RULES.md`, `COMBAT_RULES.md`,
  `GAME_RULES.md`, `GAME_EVENTS.md`, `SIGNALR_PROTOCOL.md`, `REDIS_STATE.md`,
  or `DATABASE.md`.
- **Any source code or test.** Zero files under `src/` or `tests/`.
- **Any implementation workaround.** No partial instance construction, no
  placeholder `TargetStat`, no reflection or bypass of `StatusEffect.TurnBased`,
  no locally relaxed validator, no test-only constructor. The contradiction is
  resolved by a decision, not by code.
- **Implementing Hỏa Long's Rage** (`BOSS_RULES.md` §6.2.1), **Mộc Yêu's
  regeneration** (§6.2.3), or **Thủy Ma's healing reduction** (§6.2.2).
- **Modifying `TASK-153`** beyond the lifecycle metadata reconciliation this
  repository's workflow requires. Its `Status` remains `BLOCKED`; it is not
  re-statused to `READY` here, its implementation scope is not changed, and its
  Objective, Scope, Acceptance Criteria, Testing Requirements, Affected Files,
  and Implementation Notes are untouched.
- **Reopening or modifying `TASK-154` or `TASK-155`.** Both are immutable
  read-only sources. This task may report that TASK-154 **D-10** would be
  changed by the Product Owner's answer; it does not edit TASK-154.
- **Reopening GAP-5.** The carrier, the read boundary, the selector, the
  target, the duration, the ordering, the reapplication rule, and the
  no-new-protocol boundary are settled (see "Preserved Existing Decisions").
- **Changing the `−50%` magnitude, the 3-turn duration, or the Battle Start
  trigger** — unless the Product Owner explicitly changes them, in which case
  that change is recorded, not derived.
- **Introducing a new gameplay mechanic, Boss, Boss Skill, Statuseffect system,
  or StatusEffect Type** as part of executing this task. Option D (another
  representation) requires the Product Owner to state it; the task does not
  define it. Any resulting system work belongs to a separate task.
- **Introducing any new Battle Event, SignalR member, Redis key, or database
  column** (`BOSS_RULES.md` §6.2.2's "no new event or protocol").
- **`BossState` client exposure** — `BOSS_RULES.md` §6.2.4 records this as an
  intentional protocol limitation requiring its own separate decision.
- **Creating the follow-up documentation task or any implementation task.**
  This task records the decision and reports the resulting scope; sequencing
  the follow-up work is the Orchestrator's act, not this task's.
- **Redis implementation, SignalR changes, and frontend changes** — all
  excluded.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Dependencies

```text
TASK-153  BLOCKED   the implementation attempt whose SECOND STOP produced this
                    task. Immutable; read-only; stays BLOCKED.
TASK-154  DECIDED   the GAP-5 decision record. D-10 is the item in question.
                    Immutable; read-only; NOT reopened.
TASK-155  DONE      applied the GAP-5 decision and authored the Id selector.
                    Immutable; read-only.
TASK-124  DONE      the documentation-apply precedent; recorded GAP-5 deferral.
TASK-123  DONE      the step-18a decision set.
TASK-127  DONE      GAP-1 closed; EffectiveBossATK resolves.
TASK-118  DONE      step 18b implemented; step 18a fenced out.
TASK-022  DONE      the Boss Response stage and the step-18a charge point.
TASK-013  DONE      PassiveTracker integration and the shared Passive events.
```

All dependencies are satisfied in the sense required by this task: the
authoritative documents exist, MVP scope is confirmed, the primary agent and
workflow are assigned, and the acceptance criteria are testable. The one
outstanding input is the Product Owner's decision, which is this task's purpose.

---

## Acceptance Criteria

All binary and testable.

```text
[ ] The contradiction is explicitly identified, with BOTH sides cited by
    document path and section: BOSS_RULES.md §6.2.2 (Type = BuffDebuff,
    TargetStat absent) against GAME_STATE.md §2.3.1 (Type = BuffDebuff ⇒
    TargetStat present, item 7), plus COMBAT_RULES.md §5.4.5/§5.5.3.
[ ] The observed runtime failure (ArgumentNullException on 'targetStat' from
    StatusEffect.TurnBased) is recorded, establishing that the documented
    instance is unrepresentable in the documented state model.
[ ] It is stated explicitly that this is NOT GAP-5 and that GAP-5 is not
    reopened.
[ ] The Decision Question is stated exactly as required, and the recorded
    decision answers it rather than restating GAP-5.
[ ] Options A, B, C, and D are each presented, unranked and unendorsed.
[ ] Option A explicitly states that selecting it would CHANGE the previous
    TASK-154 D-10 decision, and that the exact new TargetStat value and its
    meaning must be decided.
[ ] Option B explicitly requires the Product Owner to decide (i) whether the
    exception is generic or Thủy-Ma-specific, (ii) the exact invariant wording,
    and (iii) whether other BuffDebuff effects remain unchanged.
[ ] Option C names DoT, Shield, and State, does NOT assume any is valid, and
    requires the Product Owner to explicitly approve any semantic mapping.
[ ] Option D requires the Product Owner to state the representation and is
    treated as a new architectural/gameplay decision, with the representation
    itself NOT defined in this task.
[ ] All 14 coverage items (D-1 … D-14) are explicitly answered in the recorded
    decision, or the task STOPS.
[ ] D-4 (TargetStat semantics) is answered whether or not a TargetStat is used.
[ ] D-11 explicitly addresses each existing GAME_STATE invariant in turn.
[ ] D-12 explicitly states whether TASK-154 D-10 is unchanged, changed (and to
    what), or superseded.
[ ] D-13 explicitly states whether any other BuffDebuff semantics are affected.
[ ] D-14 lists the authoritative documents/sections requiring update and
    reports whether an ADR is required.
[ ] The "Preserved Existing Decisions" list is recorded, and the recorded
    decision changes none of them except as explicitly stated at D-12/D-11.
[ ] Out-of-scope items are explicitly recorded: source implementation, tests,
    Redis implementation, SignalR changes, frontend changes, new gameplay
    mechanics, new Bosses, new Boss Skills, and new StatusEffect systems.
[ ] Stop Conditions cover: no explicit representation chosen; a representation
    remaining incompatible with GAME_STATE.md; an unapproved architectural
    change; multiple authoritative interpretations remaining possible; and a
    new gameplay rule required beyond this representation decision.
[ ] Source code was NOT modified.
[ ] Test code was NOT modified.
[ ] No authoritative gameplay documentation was modified — specifically
    GAME_STATE.md, BOSS_RULES.md, COMBAT_RULES.md, GAME_RULES.md,
    GAME_EVENTS.md, SIGNALR_PROTOCOL.md, REDIS_STATE.md, and DATABASE.md are
    all unmodified.
[ ] No implementation workaround was created.
[ ] TASK-153's implementation scope is unmodified, and its Status is still
    BLOCKED (not re-statused to READY, not re-opened).
[ ] TASK-154 and TASK-155 are unmodified.
[ ] No other task was created.
[ ] No gameplay value was invented: the −50% magnitude, the 3-turn duration,
    and the Battle Start trigger are unchanged unless explicitly decided
    otherwise; no TargetStat value was invented; no §2.3.1 invariant was
    relaxed.
[ ] The task explicitly states: "Decision-input only. No source code. No
    authoritative documentation changes. No implementation workaround."
[ ] All acceptance criteria are binary and testable.
[ ] Quality review checklist passes (quality/review.md §1)
[ ] No authoritative rules or contracts violated (AGENTS.md §10 / ADR-001)
```

---

## Affected Files & Areas

```text
[ ] src/backend/ — NONE
[ ] src/frontend/client/ — NONE
[ ] tests/ — NONE
[x] tasks/backlog/TASK-156-<this file>.md
      — the decision request and the recorded decision
[ ] docs/ — NONE (the follow-up documentation task applies the decision at its
      canonical owners; the sections that will need updating are listed at
      coverage item D-14)
[~] tasks/blocked/TASK-153-*.md
      — lifecycle metadata reconciliation ONLY, if the workflow requires it:
        Status remains BLOCKED, the dependency/reason reference is updated to
        name TASK-156 as the blocking decision task, and a reconciliation note
        is appended. Objective, Scope, Acceptance Criteria, Testing
        Requirements, Affected Files, and Implementation Notes are NOT touched.
[ ] tasks/backlog/TASK-154-*.md — NONE (immutable; read-only)
[ ] tasks/backlog/TASK-155-*.md — NONE (immutable; read-only)
[ ] docs/03-decisions/ADR/ — NONE (no ADR; reported at D-14 if required)
```

---

## Implementation Notes

- **This task's deliverable is a recorded decision, not a change.** The
  executing agent's job is to present the contradiction accurately, obtain the
  Product Owner's answer, and transcribe it faithfully. It is not to evaluate,
  rank, improve, select, or implement the options.
- **The precedent to follow is TASK-154** (and, one step earlier, TASK-150).
  Both recorded a Product Owner decision verbatim, stated explicitly that they
  were NOT the owner of the documentation edits, and enumerated the resulting
  document scope for the follow-up task. Match that shape and that Completion
  Evidence form.
- **The evidence is already gathered.** TASK-153's "Second Stop Condition
  Report" contains the four closed routes, the exact line references for both
  sides, and the observed exception. TASK-154's D-10 is the item potentially in
  question. Nothing needs to be re-derived; it needs to be resolved by the
  Product Owner.
- **The two sides, precisely.** `BOSS_RULES.md` §6.2.2's "Representation" bullet
  and "Applicable-instance selector" bullet require `BuffDebuff` with no
  `TargetStat`. `GAME_STATE.md` §2.3.1's schema line defines `TargetStat` as
  "the modified stat for Type = `BuffDebuff`" and item 7 fixes the absence
  convention. `COMBAT_RULES.md` §5.1 defines the type by that member, and
  §5.4.5 / §5.5.3 record the no-new-`TargetStat` position.
  `StatusEffect.TurnBased` enforces the pairing at construction.
- **Do not treat this as a documentation bug to fix.** The contradiction cannot
  be resolved by choosing the more convenient document. `AGENTS.md` §4 and §20
  require a human decision, and §23 forbids designing on the project's behalf.
- **Do not modify TASK-153 beyond lifecycle metadata.** Its STOP report is the
  evidence for this task. It stays BLOCKED. Its "Second Stop Condition Report"
  is a preserved historical record.
- **Do not create the follow-up tasks.** Report the scope; the Orchestrator
  sequences them.
- **No ADR is created here.** If the recorded answer requires one (likely only
  under Option D), report that at D-14 — authoring it is a separate task
  (`AGENTS.md` §18).

---

## Testing / Evidence Requirements

### Required Verification

```text
[ ] Unit tests         — N/A (no code produced by this task)
[ ] Integration tests  — N/A (no code produced by this task)
[ ] Gameplay scenarios — N/A (no behavior implemented by this task)
```

No test is authored or run: this task changes no behavior and produces no code.
The decision it records becomes testable only after the subsequent
documentation task lands and an implementation task exists. Deriving those
scenarios is that implementation task's obligation (`core/validation.md` §2,
`AGENTS.md` §15).

### Verification this task DOES perform

```text
[ ] Both sides of the contradiction are cited from the authoritative documents
    by path AND section, with no paraphrase that could soften either side.
[ ] The runtime failure is recorded from TASK-153's stop report, establishing
    that the conflict is enforced and not merely documentary.
[ ] The decision's 14 coverage items are each explicitly answered or explicitly
    flagged as unanswered (a flagged item = the task's Stop Condition).
[ ] No authoritative document was modified: GAME_STATE.md, BOSS_RULES.md,
    COMBAT_RULES.md, GAME_RULES.md, GAME_EVENTS.md, SIGNALR_PROTOCOL.md,
    REDIS_STATE.md, and DATABASE.md are byte-identical.
[ ] Zero files under src/ or tests/ modified.
[ ] TASK-153's implementation scope is byte-identical; its Status is BLOCKED.
[ ] TASK-154 and TASK-155 are byte-identical.
[ ] No value was invented: −50%, 3 turns, and the Battle Start trigger are
    unchanged; no TargetStat value was added; no invariant was relaxed.
[ ] No implementation workaround exists anywhere in the tree.
```

### Key Edge Cases

- **The decision chooses a representation but not its `Type`.** D-2 unanswered
  — STOP.
- **The decision chooses Option A without giving the exact `TargetStat` value
  and its meaning.** D-4 unanswered — STOP.
- **The decision chooses Option B without saying whether the exception is
  generic or Thủy-Ma-specific, without the exact invariant wording, or without
  addressing other BuffDebuff effects.** D-11 and/or D-13 unanswered — STOP.
- **The decision chooses Option C without explicitly approving the semantic
  mapping onto the named Type.** D-2/D-4 unanswered — STOP. An implicit
  re-typing is not an approval.
- **The decision chooses Option D but leaves the representation to be defined
  later.** That is permitted ONLY if the Product Owner separately states that a
  new decision task owns the representation; otherwise multiple authoritative
  interpretations remain possible — STOP.
- **The decision states "§2.3.1 unchanged" while selecting a representation
  that cannot satisfy item 7.** Self-contradictory — STOP and report rather
  than recording it as consistent.
- **The decision changes the `−50%` magnitude, the 3-turn duration, or the
  Battle Start trigger.** That is permitted only as an explicit Product Owner
  change; record it as a value change, never derive it.
- **The decision requires a new Battle Event, SignalR member, Redis key, or
  database column.** `BOSS_RULES.md` §6.2.2 states "no new event or protocol".
  A decision that contradicts its own section is an unresolved coupling — STOP
  and report it rather than reconciling it silently.
- **The decision requires `BossState` client exposure.** §6.2.4 records that as
  requiring its own separate protocol decision — STOP.
- **The decision is partial.** Any of the 14 items left unanswered = STOP.
- **An agent is tempted to "just add `TargetStat = "HEAL"`" or to relax
  `StatusEffect.TurnBased` to make TASK-153 proceed.** Both are the prohibited
  action of this task — STOP.

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 apply.

- **If the Product Owner does not explicitly choose a representation:** STOP. Do
  not fill any coverage item by inference, and do not select an Option A–E on
  the Product Owner's behalf.
- **If the selected representation remains incompatible with
  `GAME_STATE.md`:** STOP per `AGENTS.md` §4 and report the conflict. In
  particular, an answer that keeps `Type = BuffDebuff` and no `TargetStat`
  while asserting §2.3.1 item 7 unchanged is self-contradictory and must not be
  recorded as resolved.
- **If the selected representation requires an unapproved architectural
  change:** STOP per `AGENTS.md` §18. An architecture change needs its own
  ADR-first task, not this one.
- **If multiple authoritative interpretations remain possible after the
  decision:** STOP. The representation, the Type, the `TargetStat` question, and
  the invariant question are coupled; a decision that resolves one while
  leaving another open does not resolve the contradiction.
- **If a new gameplay rule is required beyond this representation decision:**
  STOP per `AGENTS.md` §7 — a new rule is a new decision, not a detail of this
  one.
- **If the decision requires inventing a `TargetStat` value without the Product
  Owner stating it:** STOP. `COMBAT_RULES.md` §5.4.5/§5.5.3 and TASK-154 D-10
  both close that route absent a decision.
- **If the decision requires relaxing `GAME_STATE.md` §2.3.1 item 7 without the
  Product Owner stating the new invariant wording:** STOP.
- **If the decision conflicts with `BOSS_RULES.md` §6.2.2's own "no new event or
  protocol" boundary:** STOP and report it; do not silently prefer one side
  (`AGENTS.md` §4).
- **If recording the decision would require modifying an authoritative document
  within this task:** STOP. That is the follow-up task's act (`AGENTS.md` §17).
- **If the decision requires reopening `TASK-154` or `TASK-155`:** STOP. They
  are immutable; a change to a decided contract is a new decision recorded here,
  not an edit to them.
- **If the decision would require re-statusing `TASK-153` to READY:** STOP. This
  task does not mark TASK-153 READY; it stays BLOCKED until the decision is
  resolved AND the authoritative documentation is updated.
- **If resolving the contradiction would require a source-code workaround:**
  STOP. This task is decision-input only.
- **If the task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries:** STOP & decompose (`tasks/README.md` §12).

---

## Decision Record

<!--
  TO BE COMPLETED BY THE PRODUCT OWNER (or recorded verbatim from the Product
  Owner's supplied answer by the executing agent — the agent transcribes, it
  does NOT author).

  Until this section is filled, Status MUST remain BACKLOG and the task MUST
  NOT proceed. Filling this section by inference is the single prohibited
  action of this task (AGENTS.md §4, §7, §20).

  Record each of the 14 required coverage items from "Required Decision
  Coverage" above. Where the supplied answer addresses an item implicitly,
  state the item explicitly and cite the supplied wording rather than
  paraphrasing it.
-->

```text
Status of decision:  SUPPLIED — decided by the Product Owner in this session.
                     Recorded by the executing agent; the agent transcribed
                     the supplied answers and authored no rule.

Selected candidate:  OPTION B — Relax the BuffDebuff / TargetStat invariant.
                     Keep Type = BuffDebuff with TargetStat absent for this
                     documented class of healing modifier, and explicitly
                     change the invariant stated in GAME_STATE.md §2.3.1
                     item 7 (and its schema line).

Decision source:     Product Owner, this session. The agent transcribed the
                     supplied selections and the supplied replacement wording
                     verbatim; it authored neither.

D-1  Representation:
     OPTION B — Relax the BuffDebuff / TargetStat invariant.
     Recorded verbatim (selected): "Option B — Relax the BuffDebuff/TargetStat
     invariant".
     The instance keeps `Type = BuffDebuff` and an ABSENT `TargetStat`, and
     the authoritative invariant at GAME_STATE.md §2.3.1 item 7 is explicitly
     CHANGED to admit it. Option A (authorize a TargetStat), Option C (re-type
     onto DoT | Shield | State), and Option D (a representation outside the
     StatusEffect vocabulary) were NOT selected.

D-2  Type:
     `BuffDebuff` — UNCHANGED. This is one of the existing GAME_STATE.md
     §2.3.1 item 3 vocabulary (DoT | BuffDebuff | Shield | State); the
     vocabulary itself is NOT changed, no type is added, and no item 3
     duration-model assignment changes. The instance remains Turn-based
     (`RemainingTurns`), which item 3 already assigns to `BuffDebuff`.

D-3  Target / affected domain:
     CONFIRMED UNCHANGED. Source/owner is the BOSS (Thủy Ma); the affected
     domain is PET HP healing ONLY. Boss HP healing is NOT affected.
     Recorded verbatim (selected): "Thủy-Ma-specific — no other BuffDebuff
     semantics affected". The Boss-owned / Pet-HP-only target of TASK-154 D-3
     is preserved. No target-domain change is made by this decision.

D-4  TargetStat semantics:
     NO `TargetStat` is carried. `TargetStat` remains ABSENT on this instance
     — never `null`, never a sentinel string, per §2.3.1 item 7's absence
     convention, which this decision does not change.
     This is the item that makes the contradiction: an absent `TargetStat` on
     a `BuffDebuff` is precisely what item 7's pairing forbade, and D-11
     resolves it by changing item 7 rather than by supplying a value.
     NO new `TargetStat` value is introduced (see D-12). The applicable-
     instance selector remains the existing Status Effect `Id`
     (D-5), NOT a `TargetStat` value.

D-5  Identity:
     CONFIRMED UNCHANGED. `Id = "boss-thuy-ma-heal"` remains the
     applicable-instance selector (TASK-155's authored selector), under
     GAME_STATE.md §2.3.1 item 1 (`Id` is an identity, not a definition) and
     item 6 (`Id` is the collection's uniqueness key and the thing a refresh
     targets). This decision does not change the identity, the identity
     model, or the selector.
     This is now load-bearing for the representation: because the instance
     carries no `TargetStat`, the `Id` is the ONLY thing that selects it.
     The `Id` is deliberately and explicitly NOT a `TargetStat` value.

D-6  Carrier:
     CONFIRMED UNCHANGED. `BossState.StatusEffects[]` remains the carrier
     (TASK-154 D-1, Option B). No new carrier, no second collection, and no
     `PendingStatusEffects[]` or queued/in-flight representation is
     introduced (GAME_STATE.md §2.3.3's prohibition is intact). The GAP-5
     read boundary is NOT re-opened: the carrier is unchanged, so the
     cross-entity read authorized by TASK-154 D-5 is preserved as-is.

D-7  Duration:
     CONFIRMED UNCHANGED. `RemainingTurns = 3`; the Battle Start application
     consumes no duration unit; the effect is active Turns 1–3.
     No value was re-derived, rounded, or invented.

D-8  Lifecycle / expiry:
     CONFIRMED UNCHANGED. Decrement at the existing step 19a boundary;
     expires before Turn 4; a stored zero is never active
     (GAME_STATE.md §2.3.1 item 8); removed only by the existing Turn-based
     turn-duration lifecycle (COMBAT_RULES.md §5.3; GAME_STATE.md §5.1.1
     item 5). Because the instance keeps `Type = BuffDebuff` and
     `RemainingTurns`, item 3's duration-model assignment and §5.3's
     consumption rules continue to govern it unchanged.

D-9  Heal Resolution integration:
     CONFIRMED UNCHANGED, and explicitly NOT invalidated by the selected
     representation. The −50% still reaches Pet healing through the
     authorized cross-entity read at COMBAT_RULES.md §4 item 7's Applicable
     Heal Modifiers stage, applied to the Raw Heal, before item 1's clamp,
     one-directional and non-mutating, and not consumed by healing.
     The read is NOT invalidated: the read has always selected the instance
     by its Status Effect `Id` (BOSS_RULES.md §6.2.2; COMBAT_RULES.md §4
     item 7; GAME_STATE.md §2.4.1) and never by `Type` + `TargetStat`.
     §6.2.2's own statements on this point remain consistent under the
     relaxed item 7 and require no change (see D-14).

D-10 Reapplication:
     CONFIRMED UNCHANGED. Refresh the existing instance to the full 3-turn
     duration; no additive stack; at most one instance; same source identity
     (GAME_STATE.md §2.3.1 item 6's uniqueness key, which is the `Id`).
     The refresh mechanism is unaffected because the identity rule is
     unaffected.

D-11 Interaction with existing GAME_STATE invariants — ITEM BY ITEM:
     EXACTLY ONE ITEM CHANGES: §2.3.1 item 7.
     Recorded verbatim (selected): "Only item 7 changes — I will supply the
     new wording".

     CHANGED — §2.3.1 item 7 (the `TargetStat`-iff-`BuffDebuff` pairing and
     the schema line's "the modified stat for Type = `BuffDebuff`" wording).
     The Product Owner supplied the exact replacement wording as its rule,
     recorded verbatim:

       "a BuffDebuff carries TargetStat iff its Magnitude is consumed as a
        stat modifier; a BuffDebuff consumed by a non-stat rule selects by
        Id and omits TargetStat"

     The schema line's "the modified stat for Type = `BuffDebuff`, e.g.
     `"ATK"`; absent otherwise" wording must be brought into line with that
     rule by the documentation-apply task, since the pairing is no longer
     unconditionally "iff Type = BuffDebuff".

     UNCHANGED — every other item, each answered explicitly:
       item 1  (`Id` is an identity, not a definition) — UNCHANGED. The
               selector is still the `Id` (D-5).
       item 2  (`Magnitude` typed but not interpreted here; the `BuffDebuff`
               interpretation is by entity via COMBAT_RULES.md §5.4/§5.5.1)
               — UNCHANGED. This decision authors no interpretation of
               `Magnitude` here; the −50% remains owned by BOSS_RULES.md
               §6.2.2 and consumed by §4 item 7.
       item 3  (the exclusive duration models and the
               `DoT | BuffDebuff | Shield | State` vocabulary) — UNCHANGED.
               The vocabulary is not changed, and `BuffDebuff` keeps the
               Turn countdown.
       item 6  (at most one instance per identity; `Id` is the uniqueness
               key) — UNCHANGED, and now the sole selection mechanism (D-5).
       item 8  (a stored zero is never active) — UNCHANGED.
       item 12 (the section adds no gameplay rule) — UNCHANGED. The change
               to item 7 is a state-model representability rule, not a
               gameplay rule; the effect's gameplay remains owned by
               BOSS_RULES.md §6.2.2 and COMBAT_RULES.md §4 item 7.
     §2.3.3's `PendingStatusEffects[]` prohibition — UNCHANGED and not
     relaxed (D-6).
     §2.4 / §2.4.1's Boss carrier contract — UNCHANGED. The carrier, the
     element schema, and the cross-entity read reference stand as applied by
     TASK-155; no member, value, type, or collection is added.
     §5.1.1's lifecycle — UNCHANGED (D-8).
     §0 item 5's one-representation-per-fact rule — UNCHANGED. This decision
     introduces no second representation: the instance is still the single
     `BossState.StatusEffects[]` element, and `TargetStat`'s absence is the
     absence of a member, not a second representation of one.

     NOT SELF-CONTRADICTORY: the selected representation is representable
     under item 7 AS CHANGED. The decision does not assert "§2.3.1 unchanged"
     — it explicitly changes item 7 — so the Stop Condition for a
     self-contradictory answer does not fire.

D-12 Whether TASK-154 D-10 is superseded/changed:
     CHANGED — D-10 is changed, and the Product Owner supplied the exact
     replacement text. Recorded verbatim (selected): "No new TargetStat
     value is introduced; §2.3.1 item 7's TargetStat-iff-BuffDebuff pairing
     is relaxed for rule-consumed BuffDebuffs. All other §2.3.1 invariants
     unchanged".

     Precisely:
       - D-10's first half is PRESERVED: NO new `TargetStat` value is
         introduced. No `"HEAL"` (or any other) `TargetStat` value is
         created, and the §5.4.5 / §5.5.3 boundary is not exercised.
       - D-10's second half is CHANGED: D-10 previously confirmed §2.3.1's
         invariants unchanged, including item 7's pairing. Item 7 is now
         explicitly RELAXED for rule-consumed `BuffDebuff` instances
         (D-11).
       - Every OTHER §2.3.1 invariant remains unchanged (D-11), which is why
         this is recorded as "changed" rather than "superseded": D-10's
         no-new-`TargetStat`-value core survives; only its invariant-
         confirmation clause changes.

     TASK-154 itself is NOT modified by this task. It is immutable and
     read-only; this record supersedes D-10's second half for downstream
     purposes only.

D-13 Whether any other existing BuffDebuff semantics are affected:
     NONE. Recorded verbatim (selected): "Thủy-Ma-specific — no other
     BuffDebuff semantics affected".
     The exception is THỦY-MA-SPECIFIC in effect, expressed GENERICALLY as a
     rule: item 7's relaxed form keys on whether the instance's `Magnitude`
     is consumed as a stat modifier, which is a property of the instance.
     Applying that rule:
       - Root's `TargetStat = "ATK"` instance (Pet-side) — UNAFFECTED. Its
         `Magnitude` IS consumed as a stat modifier (COMBAT_RULES.md §5.4),
         so it still carries `TargetStat` and the pairing still applies to
         it.
       - Hỏa Long's Rage `TargetStat = "ATK"` instance (Boss-side) —
         UNAFFECTED, for the same reason (COMBAT_RULES.md §5.5.1).
       - Burn (`DoT`), Shield (`Shield`), Stun (`State`) — UNAFFECTED. None
         is a `BuffDebuff`, so item 7 never applied to them; their
         `TargetStat`-absence was already governed by the unchanged
         "absent otherwise" clause and by item 3.
       - COMBAT_RULES.md §5.4 and §5.5 consumption rules — UNAFFECTED. This
         decision adds no non-`"ATK"` `TargetStat` case, so §5.4.5 / §5.5.3
         are neither weakened nor exercised, and their "any other stat would
         require its own recorded decision" boundary stands verbatim.
     No other Status Effect instance, and no other consumption rule, changes
     behavior as a result of this decision.

D-14 Documentation consequences:
     REQUIRED — GAME_STATE.md §2.3.1:
       item 7 — MUST change. Replace the unconditional
                `TargetStat`-iff-`BuffDebuff` pairing statement with the
                Product Owner's supplied rule (D-11), and bring the schema
                line's "the modified stat for Type = `BuffDebuff`, e.g.
                `"ATK"`; absent otherwise" wording into line with it.
                This is the ONE authoritative invariant that ceases to hold
                as currently written.
       §2.3.1 item 3 and §2.3.2 item 3 — need a consistency pass. §2.3.2
                item 3 restates the pairing as "present iff type =
                `"BuffDebuff"`" and must be reconciled with the new item 7
                rule; item 3's type/duration-model assignment is unchanged.
       §2.4.1  — already records the authorized cross-entity read and the
                `Id` selector; needs no substantive change beyond any
                reference consistent with item 7. No member, value, type, or
                collection is added.

     REQUIRED — BOSS_RULES.md §6.2.2:
       NO change required. Its "Representation" and "Applicable-instance
       selector" statements (the instance is a `BuffDebuff` that is not
       `TargetStat`-consumed, selected by `Id`) remain consistent under the
       relaxed item 7. Recorded verbatim (selected): "Keep §6.2.2 as-is —
       the relaxed item 7 makes its statement consistent". Its magnitude
       (50%), duration (3 turns), trigger, target, reapplication rule, and
       "no new event or protocol" boundary are all unchanged.

     REQUIRED — COMBAT_RULES.md:
       §5.4.5 / §5.5.3 — NO substantive change. Their statements that Thủy
                Ma's healing reduction is not a `TargetStat`-consumed
                `BuffDebuff` and opens no new non-`"ATK"` case remain
                correct; their boundary is neither weakened nor exercised.
       §5.1 / §4 item 7 — NO change. §5.1's Buff/Debuff definition and
                §4 item 7's mechanism, scope, ordering, and clamp position
                are unchanged. §5.1's phrase "the stat its `TargetStat`
                names" now needs reading under the relaxed item 7, but its
                ATK-consumption rule is unaffected (D-13).

     EVALUATED — NO change required: GAME_RULES.md (§16 event list, §17
       step 18a/19a, §18 server authority — no event, no step, no authority
       changed); GAME_EVENTS.md (no new event); SIGNALR_PROTOCOL.md (no new
       wire member; the §6.2.4 `BossState` client-invisibility limitation
       stands); REDIS_STATE.md (no new key; the serialized member set is
       unchanged, since `TargetStat`'s absence is already a representable
       serialized case per §2.3.1 item 7); DATABASE.md (no column);
       ARCHITECTURE.md (no boundary moved); TDD.md (no determinism or
       hot-path contract change); PASSIVE_RULES.md (trigger form unchanged);
       MVP_SCOPE.md (no scope change; Thủy Ma, the Boss Passive system, and
       healing remain IN scope — nothing OUT is introduced).

     ADR required:  NO. Recorded verbatim (selected): "No ADR — the change
       is a state-contract wording change, not architectural".
       Rationale recorded by the Product Owner: this decision changes no
       architecture, no database strategy, no realtime strategy, no module
       boundary, and no infrastructure (AGENTS.md §18). The battle-state
       model is unchanged: the carrier is the existing
       `BossState.StatusEffects[]`, the member set is unchanged, the
       lifecycle is the existing §5.1.1/§5.3 one, and no collection or
       second representation is added. What changes is a representability
       rule within an existing member's presence convention. No ADR task is
       required.

Values changed:      NONE. Thủy Ma Healing Reduction remains −50%; the
                     duration remains 3 turns; the trigger remains Battle
                     Start; the target remains Pet HP healing only; the
                     carrier and identity are unchanged. The magnitude,
                     duration, and trigger are unchanged unless explicitly
                     decided otherwise — and they were not changed here. No
                     value was re-derived, rounded, or invented, and no
                     `TargetStat` value was created (D-12).

### Implementation consequence (recorded, NOT performed)

The selected representation is unrepresentable under the CURRENT enforced
domain model, and this task does not change it:

```text
src/backend/GameServer.Domain/Battle/StatusEffect.cs, TurnBased(),
lines 251-257
  if (type == StatusEffectType.BuffDebuff)
  {
      ArgumentException.ThrowIfNullOrWhiteSpace(targetStat);
  }
```

Under the D-11 rule this validation no longer matches §2.3.1 item 7: a
`BuffDebuff` whose `Magnitude` is consumed by a non-stat rule must be
constructible with `TargetStat` absent. Relaxing that validation — and any
test that asserts the current pairing — belongs to a LATER implementation
task, not to TASK-156. It is recorded here only so the follow-up work is
scoped accurately and the recorded decision is not mistaken for a change
already in the tree.

Per this task's boundary, NO implementation workaround was introduced: no
partial instance construction, no placeholder `TargetStat`, no reflection or
bypass, and no locally relaxed validator.

### Decision completeness check (per this task's Stop Conditions)

```text
[x] D-1 through D-14 are each explicitly answered above.
[x] An explicit representation was chosen by the Product Owner (Option B) —
    not by inference, and not by an agent.
[x] The selected representation IS compatible with GAME_STATE.md once item 7
    is changed as D-11 specifies; it does not assert "§2.3.1 unchanged"
    while requiring an absent TargetStat on a BuffDebuff.
[x] D-4 is answered although no TargetStat is carried (it is explicitly
    absent, and explicitly not a new value).
[x] D-11 addresses each existing GAME_STATE invariant in turn.
[x] D-12 states explicitly that TASK-154 D-10 is CHANGED, and to exactly
    what.
[x] D-13 states explicitly which other BuffDebuff semantics are affected
    (none) and why the exception is Thủy-Ma-specific in effect.
[x] D-14 lists the authoritative documents/sections requiring update and
    reports that no ADR is required.
[x] No Stop Condition is met.
[x] No coverage item was filled by inference: the two items that required
    the Product Owner's own wording (D-11's replacement invariant text,
    D-12's replacement D-10 text) were obtained verbatim from the Product
    Owner rather than authored by the agent.
[x] The decision introduces no new gameplay mechanic, no new event, no new
    wire member, no new Redis key, and no new database column.
[x] No architectural change is required and no ADR is needed.
[x] TASK-153 is NOT re-statused to READY by this task.
```

**Decision COMPLETE. The decision-input half of TASK-156 is satisfied.** It
does not make the implementation usable by itself: the authoritative
documentation must be updated first (TASK-153 stays BLOCKED until then).

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after the decision is recorded.
  Keep concise and factual.
-->

### Changed Files
- `tasks/backlog/TASK-156-resolve-thuy-ma-healing-reduction-statuseffect-representation-vs-game-state-buffdebuff-targetstat-invariant.md`
  — this task file. Recorded the Product Owner's decision verbatim in the
  "Decision Record" section; resolved every "Required Decision Coverage" item
  (D-1 … D-14) against it; advanced `Status` from `BACKLOG` to `DECIDED`; and
  recorded this Completion Evidence section.

  **No other file was created, modified, or deleted by this task.** Zero files
  under `docs/`, zero under `src/`, zero under `tests/`, and zero other task
  files.

### Validation Results
This is a decision-input task. It modifies no code and no contract, so there is
no test suite to run and none was run. Its verification is an
**evidence-accuracy review**, performed against the authoritative documents at
their current revisions:

```text
[x] Both sides of the contradiction cited by path AND section, with no
    paraphrase that softens either side:
      BOSS_RULES.md §6.2.2 L293-294 (carrier = the existing Turn-based
        Buff/Debuff model, held in BossState.StatusEffects[]) and L308-312
        ("it is **not** a `TargetStat`-consumed `BuffDebuff`")
      GAME_STATE.md §2.3.1 L1229-1230 (schema: "the modified stat for Type =
        `BuffDebuff` …; absent otherwise"), L1283-1284 (item 7 absence
        convention), and §2.3.2 L1358 ("present iff type = `BuffDebuff`")
      COMBAT_RULES.md §5.4.5 L1090-1092 and §5.5.3 L1266-1268 (no
        non-`"ATK"` case; "would require its own recorded decision")
[x] The enforced-pairing evidence is recorded from the tree, establishing that
    the conflict is enforced and not merely documentary:
      src/backend/GameServer.Domain/Battle/StatusEffect.cs L251-257
      (ArgumentException.ThrowIfNullOrWhiteSpace(targetStat) for BuffDebuff)
[x] The decision's 14 coverage items are each explicitly answered. The two
    items requiring the Product Owner's own wording — D-11's replacement
    invariant text and D-12's replacement D-10 text — were obtained verbatim
    from the Product Owner, not authored by the agent.
[x] The §2.3.1 invariants were verified item by item (D-11): exactly item 7
    changes; items 1, 2, 3, 6, 8, 12, §2.3.3, §2.4/§2.4.1, §5.1.1, and §0
    item 5 are unchanged.
[x] No authoritative document was modified. Verified byte-identical by
    SHA-256 before and after this task:
      docs/01-game-design/BOSS_RULES.md
        1EE72E371CBFDE60E60BC9BF9755F35C54F9EC1525FAE61BF3F9D7B7D76D3215
      docs/01-game-design/COMBAT_RULES.md
        C0C5E5A12B43A70077337548570B7717C450067BB5E6EF2B84BE09D58647BE16
      docs/02-technical/GAME_STATE.md
        9ECC6F5E1673C39C00E0887CAF57DFE64098CA34EC856FDF53F86CB1C54A39C0
[x] Zero files under src/ or tests/ modified. Verified byte-identical:
      src/backend/GameServer.Domain/Battle/StatusEffect.cs
        15B7EDE9C6E65EFA103555969F5CBB14E39C3186C897A326427CA01FC1C64484
[x] TASK-153 is byte-identical and its Status is still BLOCKED:
      55BB5A48718B8B2134C52FE8081E659767F1C769C9C8214877B7AA6F7CDBFDCB
[x] TASK-154 and TASK-155 are byte-identical (immutable; read-only):
      TASK-154
        236A126ACC07949E9BCEB96946EB65559CC7B13C5E138FA3D2D55E93F48243EB
      TASK-155
        5103564157E7DC202879F7B0437F0492ACACAD74970EDA686BA704BA99D11679
[x] No value was invented: −50%, 3 turns, and the Battle Start trigger are
    unchanged; no TargetStat value was added; the changed invariant wording is
    the Product Owner's, quoted verbatim.
[x] No implementation workaround exists anywhere in the tree.
[x] No additional task was created.
[x] No authoritative rules or contracts violated (AGENTS.md §10 / ADR-001).
```

### Scope Verification
- [x] Confirmed zero files under `docs/` modified
- [x] Confirmed zero files under `src/` or `tests/` modified
- [x] Confirmed no implementation workaround created
- [x] Confirmed TASK-153 still BLOCKED with unmodified implementation scope
- [x] Confirmed TASK-154 and TASK-155 unmodified
- [x] Confirmed no new task created
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)

### Decision Application Status
The decision is **RECORDED, not APPLIED**. This task deliberately does not:

- edit `GAME_STATE.md` §2.3.1 item 7 or its schema line, which the decision
  makes wrong;
- edit `GAME_STATE.md` §2.3.2 item 3, which restates the pairing;
- relax `StatusEffect.TurnBased`'s enforced pairing, or any test asserting it;
- change `BOSS_RULES.md` §6.2.2, which the decision confirms is correct as
  written;
- create the downstream documentation-application task or any implementation
  task;
- re-status TASK-153.

### Reported downstream scope (for the Orchestrator; not created here)
```text
Documentation-application task — canonical owners to update:
  GAME_STATE.md §2.3.1 item 7 + the TargetStat schema line   (REQUIRED)
  GAME_STATE.md §2.3.2 item 3 (pairing restatement)          (REQUIRED)
  BOSS_RULES.md §6.2.2                                       (no change)
  COMBAT_RULES.md §4 item 7 / §5.4.5 / §5.5.3                (no change)
  ADR                                                        (NOT required)

Later implementation task — the enforced domain model:
  StatusEffect.TurnBased's BuffDebuff/TargetStat validation, and any test
  asserting the current pairing.

Still BLOCKED pending the documentation application:
  TASK-153 (Boss Passive effects at step 18a)
```

---

**Decision-input only. No source code. No authoritative documentation changes.
No implementation workaround.**
