# TASK-154 — Resolve the GAP-5 Thủy Ma Healing-Reduction Representation and Heal-Resolution Read Boundary

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, schemas, or contracts.

  THIS TASK DECIDES NOTHING BY ITSELF AND IMPLEMENTS NO BEHAVIOR. It presents
  the documented evidence of a genuine contract gap and requires the
  appropriate human/Product-Owner decision, then records it. Choosing between
  the candidate representations, inventing a TargetStat value for healing, or
  widening the Heal Resolution scope is the single prohibited action of this
  task (AGENTS.md §4, §7, §20).

  PROVENANCE: TASK-153 attempted to implement the three MVP Boss Passive
  effects at GAME_RULES.md §17 step 18a and STOPPED. Hỏa Long's Rage
  (BOSS_RULES.md §6.2.1) and Mộc Yêu's regeneration (§6.2.3) were assessed as
  fully specified and implementable; Thủy Ma's healing reduction (§6.2.2) was
  NOT, because the contract requires two carriers that no documented mechanism
  connects. TASK-153's Stop Condition ("the existing state carrier cannot
  represent the documented effect") fired and no code was written.

  This is not a new discovery. TASK-124 recorded it explicitly as GAP-5:
  "The remaining design question (a target-agnostic Heal Resolution) is
  explicitly left to a FUTURE task and was NOT resolved here." TASK-123's D-2
  had already recorded the same split (candidate (i): "no TargetStat value for
  healing is documented"; candidate (iii): "requires a state member to carry
  its duration — BossState has no effect collection (§2.4.2)"). D-2b then chose
  the Boss-carried representation AND D-2c authored the Pet-scoped consumption
  step, without reconciling the two. TASK-153 assumed the chain was complete;
  it was not. This task is the missing decision step.

  THIS IS A STOP-DRIVEN TASK, NOT A FEATURE. Per AGENTS.md §20, "Missing rule",
  "Rule conflict", and "Ambiguous requirement" are stop conditions.

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
Task ID:           TASK-154
Type:              GAMEPLAY-CHANGE (TASK_TYPES.md §2 — "A gameplay mechanic
                   needs to behave differently than the documentation
                   currently says, OR a new undocumented mechanic is being
                   authorized." GAP-5 is precisely the second case: the
                   documented behavior at BOSS_RULES.md §6.2.2 cannot be
                   expressed by the documented state model, so a rule that
                   docs/ does not currently contain must be authored by the
                   Product Owner before any implementation may proceed.)
                   See "Type classification note" below — this task is the
                   DECISION-RECORDING half of GAMEPLAY-CHANGE per
                   development/gameplay-change.md §3; the documentation-apply
                   half is a separate task.
Status:            DECIDED — the Product Owner supplied a complete decision
                   covering all 12 required coverage items; it is recorded in
                   the "Decision Record" section below. The decision is
                   Option B (Boss-side carrier, `BossState.StatusEffects[]`),
                   with an explicitly authorized cross-entity read by the
                   Pet-side Heal Resolution step, §2.3.1's invariants
                   unchanged, and COMBAT_RULES.md §4 item 7's scope left
                   "Pet HP only".
                   The recorded decision is the DECISION-INPUT half only. No
                   authoritative document was modified by this task and no
                   implementation was performed. The documentation-apply task
                   (see "Blocks") applies it at its canonical owners and must
                   be created by the Orchestrator.
Risk:              HIGH (TASK_TYPES.md §4 — GAMEPLAY-CHANGE baseline HIGH,
                   "Always HIGH — game rule changes are the riskiest
                   category". This touches the Pet healing path, the Boss
                   Status Effect carrier, the Status Effect instance schema,
                   and the Heal Resolution step's documented scope.)
Priority:          HIGH (ROADMAP.md §1 Phase 1 requires "3 MVP Bosses
                   (Hỏa Long, Thủy Ma, Mộc Yêu — Passive + Skill each)" and
                   "Boss Response (Passive → Skill → Attack →
                   Victory/Defeat)". The Skill half landed in TASK-118. The
                   Passive half is blocked: TASK-153 cannot complete, and the
                   Hỏa Long/Mộc Yêu halves cannot be delivered as a coherent
                   documented set, until this decision lands.)
Primary Agent:     gameplay (TASK_TYPES.md §5 — Boss domain → Gameplay;
                   AGENT_SELECTION.md §1 — gameplay rule change → Gameplay
                   Agent)
Supporting Agents: backend (GAME_STATE.md §2.3.1/§2.4/§2.4.1 own the Status
                   Effect instance schema and the BossState carrier; the
                   COMBAT_RULES.md §4 item 7 Heal Resolution step is the
                   consumption site — consulted to state accurately what the
                   existing representation does and does not carry, NOT to
                   choose an option),
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
Dependencies:      TASK-153 (BLOCKED — the implementation attempt whose STOP
                     produced this task. IMMUTABLE; READ-ONLY; must NOT be
                     modified, re-statused, moved, or rewritten),
                   TASK-124 (DONE — recorded GAP-5 as explicitly deferred to a
                     future task; this task is that future task. IMMUTABLE;
                     read-only),
                   TASK-123 (DONE — the decision set whose D-2/D-2b/D-2c
                     created the split this task must reconcile. IMMUTABLE;
                     read-only),
                   TASK-127 (DONE — closed GAP-1; EffectiveBossATK resolves.
                     IMMUTABLE; read-only)
Blocks:            (1) The authoritative-documentation task that applies the
                   recorded decision at its canonical owners — it cannot be
                   created until this decision exists;
                   (2) the Thủy Ma half of any Boss Passive implementation,
                   and therefore the completion of TASK-153's objective;
                   (3) A complete ROADMAP.md §1 Phase 1 "Boss Response"
                   (Passive + Skill per Boss) for the 3 MVP Bosses.
Estimate:          Simple–Normal (present evidence, obtain one decision, record
                   it; no code, no tests, no documentation edit)
```

**Type classification note.** `GAMEPLAY-CHANGE`. Per
`development/gameplay-change.md` §2, the first branch is "does the mechanic
already exist as requested?" — it does not, because `BOSS_RULES.md` §6.2.2
requires a representation the state model cannot express. Per §3, a task that
"explicitly authorizes a design change" then proceeds
`Design change → update authoritative documentation → …`. The repository's
precedent (TASK-123 → TASK-124) splits that sequence into a
**decision-recording** task and a **documentation-apply** task, because the
decision input is a human Product Owner answer, not an agent act. This task is
the first half. It is **not** `DOCUMENTATION`: no document is edited here.

**Not an implementation task.** If, while recording the decision, an agent
finds the decision genuinely incomplete or internally inconsistent, the correct
action is this task's Stop Conditions — not to fill the gap by inference.

---

## Objective

Obtain and record an explicit Product Owner decision that determines how Thủy
Ma's healing reduction (`BOSS_RULES.md` §6.2.2) is represented in authoritative
battle state and consumed by the Heal Resolution step (`COMBAT_RULES.md` §4
item 7), such that the documented Boss-side ownership and the Pet-scoped Heal
Resolution can coexist deterministically — closing GAP-5 and unblocking the
authoritative-documentation update that must precede any implementation.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — confirm Thủy Ma, the Boss Passive
  system, and the healing mechanic remain IN scope; §2/§4 — the decision must
  not introduce an OUT-of-scope system
- `docs/01-game-design/BOSS_RULES.md` **§6.2.2** — the contested contract: the
  magnitude (`50%`), the duration (`3 turns`), the **Battle Start** trigger, the
  representation ("held in `BossState.StatusEffects[]`"), the application site
  (the shared Heal Resolution step, before the overheal clamp), the reapplication
  rule, and the "no new event or protocol" boundary. **This is the section the
  contradiction centres on.**
- `docs/01-game-design/BOSS_RULES.md` **§6.2**, **§6.2.4**, **§3.3** — the
  trigger table, the server-authority/client-invisibility statement, and the
  step-18a timing
- `docs/01-game-design/COMBAT_RULES.md` **§4 item 7** — the **Heal Resolution**
  step: `Raw Heal → Applicable Heal Modifiers → Final Heal Amount → item 1's
  clamp → HP update`; and its **"Scope — Pet HP only"** clause, which states a
  Boss-side HP change "does not route through this step". **This is the other
  half of the contradiction.**
- `docs/01-game-design/COMBAT_RULES.md` **§4 item 1** — the overheal clamp the
  modifier must precede (referenced, not restated); **§4 item 6** — Heal is not
  subject to the Damage Pipeline
- `docs/01-game-design/COMBAT_RULES.md` **§5.4.5** — the Pet-side `BuffDebuff`
  consumption scope: "Applies to a Turn-based BuffDebuff instance with
  `TargetStat = "ATK"`"; and the closing boundary that a `BuffDebuff` naming any
  other stat "would require its own recorded decision before it could be
  implemented"
- `docs/01-game-design/COMBAT_RULES.md` **§5.5 / §5.5.1 / §5.5.3** — the
  Boss-side counterpart: the `"ATK"`-only case and the same
  own-recorded-decision boundary. §5.5.1 fixes
  `EffectiveBossATK = truncate(BossState.ATK × (100 + Magnitude) / 100)`, which
  reads `Magnitude` as a **percentage of ATK** — not a healing modifier
- `docs/01-game-design/COMBAT_RULES.md` **§5.2 item 1/2**, **§5.3 DR1–DR6** —
  the Status Effect duration model and the refresh-not-stack MVP default the
  current (§6.2.2) reapplication rule cites
- `docs/01-game-design/GAME_RULES.md` **§17** — the fixed resolution order
  (step 18a Boss Passive, step 19a End Turn duration consumption); **§16** — the
  canonical event list (contains no Boss-Passive-effect event); **§18** — server
  authority
- `docs/02-technical/GAME_STATE.md` **§2.3.1** — the Status Effect instance
  schema; **item 2** (`Magnitude` "typed but not interpreted here"), **item 3**
  (the two exclusive duration models), **item 6** (at most one instance per
  identity), **item 7** (the **`TargetStat`-iff-`BuffDebuff` pairing**; absence
  conventions — "never `null`, never a sentinel string"), **item 12** (the
  section adds no gameplay rule)
- `docs/02-technical/GAME_STATE.md` **§2.4** / **§2.4.1** — the `BossState`
  tree and the Boss Status Effect carrier contract ("identical element schema,
  identical lifecycle", and the statement that Hỏa Long's Rage and Thủy Ma's
  healing reduction are **both** Turn-based instances held there)
- `docs/02-technical/GAME_STATE.md` **§2.3.3** — the prohibition on
  `PendingStatusEffects[]`/queued collections; **§5.1.1** — the Status Effect
  mutation lifecycle
- `docs/02-technical/ARCHITECTURE.md` **§2.1** — the Application layer
  sequences, Domain owns the rules; **§5** — anti-overengineering
- `docs/02-technical/TDD.md` **§4** — the hot-path constraint a cross-entity
  read would be measured against; **§6** — determinism
- `AGENTS.md` §4 (never silently resolve a conflict), §6 (required reading),
  §7 (invent no rule), §8 (MVP protection), §9 (anti-overengineering),
  §12 (domain boundaries), §15 (testing), §17 (documentation change rule),
  §20 (when AI must stop), §23 (final principle)
- `.ai/README.md` §6 (source-of-truth rule), §13 (stop conditions), §18
  (documentation-update policy)
- `tasks/backlog/TASK-153-implement-mvp-boss-passive-effects-at-step-18a.md` —
  the STOP report that identified this gap, with the three closed
  representations
- `tasks/completed/TASK-124-apply-boss-passive-contract-decisions.md` — GAP-5,
  recorded as "explicitly left to a FUTURE task and was NOT resolved here"
- `tasks/completed/TASK-123-resolve-boss-passive-effect-contract.md` — D-2 and
  its candidate list; D-2b (representation and trigger); D-2c (the Heal
  Resolution mechanism); D-2c-duration; D-2d (reapplication)
- `tasks/completed/TASK-127-implement-boss-skill-step-1-damage-composition.md` —
  GAP-1 closed; the `EffectiveBossATK` path, unaffected by this decision

---

## Current State

TASK-153 attempted to implement the three documented MVP Boss Passive effects at
`GAME_RULES.md` §17 step 18a and **STOPPED without writing code**. Hỏa Long's
Rage (`BOSS_RULES.md` §6.2.1) and Mộc Yêu's regeneration (§6.2.3) were assessed
as fully specified — each names its carrier, magnitude, timing, rounding, and
clamp unambiguously. Thủy Ma's healing reduction (§6.2.2) was not.

`BossState.StatusEffects[]` exists and is implemented (TASK-095/TASK-096,
`GAME_STATE.md` §2.4.1), and `StatusEffectLifecycle` implements apply/refresh/
consume/expire plus the `EffectiveBossAttack` reader. `ResourceGenerator.
ApplyHeal` is the existing Pet heal write site and holds the overheal clamp.
**Nothing yet connects a Boss-held instance to that Pet-side heal path**, and
`BattleStateService.ResolveSwapAsync` applies no Boss Passive effect.

### The exact ambiguity

`BOSS_RULES.md` §6.2.2 requires **both** of the following simultaneously:

```text
1. the instance is "held in BossState.StatusEffects[]"      (§6.2.2)
2. the −50% is applied at COMBAT_RULES.md §4 item 7's
   Heal Resolution step, before the overheal clamp          (§6.2.2)
```

But `COMBAT_RULES.md` §4 item 7 declares its own **"Scope — Pet HP only"** and
states that a Boss's own HP restoration "does not route through this step".
No documented mechanism connects a `BossState.StatusEffects[]` instance to the
Pet's Heal Resolution, and no rule states how a Pet-scoped step reads an effect
held on the Boss.

Every representation available under the **current** contract is closed:

```text
(i)   a BossStatusEffects[] entry naming a heal stat via TargetStat
      -> §5.4.5 and §5.5.3 define the "ATK" case ONLY and state any other
         stat "would require its own recorded decision before it could be
         implemented". No TargetStat value for healing exists in
         GAME_STATE.md §2.3.1 item 7.
(ii)  a BossStatusEffects[] entry with NO TargetStat
      -> §2.3.1 item 7 fixes the TargetStat-iff-BuffDebuff pairing as an
         invariant ("present iff ... never null, never a sentinel string"),
         enforced in code by StatusEffect.TurnBased. Unrepresentable.
(iii) a PetStatusEffects[] debuff
      -> contradicts §6.2.2's "held in BossState.StatusEffects[]".
```

A second, independent gap compounds the first: §4 item 7 states it "authors the
mechanism only" and defines **no** modifier's source, magnitude, or **activity
window** — so even given a carrier, the read boundary and the window remain
unauthored.

This is a **missing rule**, not an implementation detail. Resolving it requires
choosing between materially different representations, each of which changes or
extends a documented contract (`AGENTS.md` §20 — "Ambiguous requirement", "Rule
conflict", "Missing rule").

---

## Decision Request

> **How should Thủy Ma's Healing Reduction be represented and consumed so that
> the documented Boss-side ownership and Pet-side Heal Resolution can coexist
> deterministically?**

### Candidate approaches (evidence only — NOT recommendations, NOT ranked)

Per `AGENTS.md` §4 and TASK-123's D-2 precedent, these are presented as
candidates for the Product Owner to select from, reject, or replace. **An agent
must not preselect, rank, or argue for any of them.**

```text
Option A — Pet-side representation
    Represent the modifier in PetState.StatusEffects[] and define the required
    healing-related target/stat representation. This requires deciding whether
    the existing TargetStat contract (GAME_STATE.md §2.3.1 item 7;
    COMBAT_RULES.md §5.4.5/§5.5.3) can be extended to healing at all, and
    supersedes §6.2.2's "held in BossState.StatusEffects[]" statement.

Option B — Boss-carried modifier read by Heal Resolution
    Keep BossState.StatusEffects[] as the authoritative carrier and define an
    explicit cross-entity read: the Pet Heal Resolution step consults the
    applicable BossState modifier. This requires defining the read boundary,
    the targeting semantics, and the modifier's activity window, and requires
    deciding how §4 item 7's "Scope — Pet HP only" clause is reconciled with a
    Boss-side effect that reaches Pet healing.

Option C — Target-agnostic Heal Resolution
    Change COMBAT_RULES.md §4 item 7 so its scope is not limited to Pet HP,
    defining explicitly which entity is the heal target, which modifiers apply,
    and which state carriers are consulted. This is the option TASK-124 GAP-5
    named when it deferred "a target-agnostic Heal Resolution" to a future
    task. Do not assume it is acceptable: it widens a canonical owner section
    and interacts with §6.2.3's deliberate exclusion of Mộc Yêu's regeneration
    from that step.

Option D — An answer this list does not anticipate.
```

### Required decision coverage

The recorded decision must explicitly determine **all** of the following. An
answer that leaves any item unresolved is incomplete and triggers this task's
Stop Conditions.

```text
1.  State carrier
    Where does the Thủy Ma healing-reduction instance live? If the answer
    differs from BOSS_RULES.md §6.2.2's current "held in
    BossState.StatusEffects[]", say so explicitly, because §6.2.2 must then
    change.

2.  Identity
    How is the modifier identified (the StatusEffect.Id value)? Note
    GAME_STATE.md §2.3.1 item 6 makes Id the collection's uniqueness key and
    therefore the thing a refresh targets.

3.  Target ownership
    Which entity owns the effect, and which entity's healing does it affect?
    (Boss-owned / Pet-affected, or otherwise.)

4.  Representation and schema
    Is a TargetStat value required? If yes, its exact value and whether
    GAME_STATE.md §2.3.1 item 7's TargetStat-iff-BuffDebuff pairing is
    unchanged. If §4 item 7's modifier is not a StatusEffect at all, say what
    carries it instead. Explicitly confirm GAME_STATE.md §2.3.1's invariants
    are unchanged, and in particular that NO PendingStatusEffects[] or other
    second in-flight representation is introduced (§2.3.3's prohibition).

5.  Heal Resolution read boundary
    Exactly how does the Heal Resolution step discover the modifier? Which
    state carrier does it consult, for which recipient, and is the read
    cross-entity (Pet step reading a Boss-held effect)? State the boundary
    precisely enough that an implementer needs no further decision.

6.  Activity window
    When does the modifier become active, and when does it become inactive?
    (BOSS_RULES.md §6.2.2 currently says: applied at Battle Start, active
    throughout Turns 1–3, decremented at step 19a, expired before Turn 4.)
    Confirm or restate.

7.  Consumption/expiry behavior
    Is the modifier consumed per heal, per action, or per turn? Does the
    Heal Resolution read consume it, or is it purely a duration-based
    modifier read repeatedly while active? How does it expire?

8.  Reapplication
    What happens if Thủy Ma applies it again while an instance is active?
    (§6.2.2 currently says: refresh the existing instance to the full
    3-turn duration; it does NOT stack additively; at most one instance; same
    source identity.) Confirm or restate. Note §6.2.2's own reachability note:
    the one-time Battle Start trigger makes this unreachable in current MVP
    content.

9.  Ordering within Heal Resolution
    At exactly which point does the reduction apply? §6.2.2 says "before the
    existing overheal clamp"; §4 item 7's canonical order is
    Raw Heal → Applicable Heal Modifiers → Final Heal Amount → item 1's clamp
    → HP update. Confirm the modifier's position in that order, and confirm
    whether §4 item 1's clamp is still last and unchanged.

10. Base healing scope
    Which healing does the modifier affect — raw heal, percentage-based heal
    (Card Heal), flat heal (the HP-Gem pool), or all documented healing?
    §6.2.2 currently says it "reaches Pet healing from any existing source
    that uses that resolution — explicitly including Card Heal and HP-Gem
    healing", and that it does NOT modify MaxHP and does NOT affect Shield.
    Confirm or restate. Define this ONLY if it is part of the intended
    gameplay decision.

11. Interaction with COMBAT_RULES.md §4 item 7's scope
    Does §4 item 7's "Scope — Pet HP only" clause change? If the answer
    requires it to change, state the new scope. If it does not, state how a
    Boss-held modifier reaches a Pet-scoped step without widening it.

12. Cross-document consequences
    Which of BOSS_RULES.md §6.2.2, COMBAT_RULES.md §4 item 7 / §5.4.5 /
    §5.5.3, and GAME_STATE.md §2.3.1 / §2.4.1 must change, and does any
    existing StatusEffect invariant cease to hold?
```

### Values that must NOT be invented

```text
Thủy Ma Healing Reduction = −50%
```

is the only documented magnitude. It must not be changed, re-derived, or
rounded by this task unless the Product Owner explicitly changes it. The 3-turn
duration, the Battle Start trigger, and the "no new event or protocol" boundary
(`BOSS_RULES.md` §6.2.2) are likewise existing documented values.

---

## Decision Record

<!--
  TO BE COMPLETED BY THE PRODUCT OWNER (or recorded verbatim from the Product
  Owner's supplied answer by the executing agent — the agent transcribes, it
  does NOT author).

  Until this section is filled, Status MUST remain BACKLOG and the task MUST
  NOT proceed. Filling this section by inference is the single prohibited
  action of this task (AGENTS.md §4, §7, §20).

  Record each of the 12 required coverage items from "Decision Request" above.
  Where the supplied answer addresses an item implicitly, state the item
  explicitly and cite the supplied wording rather than paraphrasing it.
-->

```text
Status of decision:  SUPPLIED — decided by the Product Owner in this session.
                     Recorded by the executing agent; the agent transcribed
                     the supplied answers and authored no rule.

Selected candidate:  OPTION B — Boss-side carrier with an explicit,
                     authorized cross-entity read.

1.  State carrier:
    `BossState.StatusEffects[]` — UNCHANGED from BOSS_RULES.md §6.2.2's
    current statement. The Boss-side carrier is retained and remains the
    authoritative home of the instance. No new carrier is introduced, and
    the Pet-side representation (Option A) was NOT selected. This item is
    decided; it is not left implicit.

2.  Identity:
    A single effect-identity `Id` value, with at most one instance per
    identity — the existing GAME_STATE.md §2.3.1 item 6 rule
    ("There is never more than one instance per effect identity per entity",
    `Id` is the collection's uniqueness key) is CONFIRMED and applies
    unchanged. No per-application distinct identity, and therefore no
    independent-instance coexistence, is authorized.
    (The exact `Id` string is a content/naming detail for the
    documentation-apply task; this decision fixes the identity MODEL, which
    is what D-2 requires. Nothing in the decision depends on the spelling.)

3.  Target ownership:
    Source/owner: the BOSS (Thủy Ma). The instance is Boss-owned.
    Affected: PET HP healing ONLY. Boss HP healing is NOT affected.
    Recorded verbatim from the supplied answer: "Pet HP healing only".
    §6.2.2's "Active Pet healing reduced by 50%" is confirmed.

4.  Representation and schema:
    No new `TargetStat` value is introduced. All GAME_STATE.md §2.3.1
    invariants are UNCHANGED:
      - No `PendingStatusEffects[]` and no second in-flight representation
        (§2.3.3's prohibition is not relaxed).
      - No sentinel `TargetStat`, and item 7's `TargetStat`-iff-`BuffDebuff`
        pairing is preserved exactly ("present iff ... never null, never a
        sentinel string").
      - No undocumented `StatusEffect` shape; §2.3.1's member set is
        unchanged.
    Recorded verbatim from the supplied answer: "All §2.3.1 invariants
    unchanged — no new TargetStat value is introduced".
    The modifier is the existing Turn-based `BuffDebuff` Status Effect
    instance model (§2.3.1), consumed by the mechanism coverage item 5
    below authorizes — NOT by a `TargetStat`-driven stat consumer. This is
    the specific consequence of selecting Option B with this invariant
    answer, and it is why coverage item 11 is answered as it is.
    NOTE (reported, not resolved here): this leaves the question of WHICH
    field the selector reads (a dedicated `Id`-based identity selector vs.
    a `TargetStat` value) to the documentation-apply task, which must author
    the selector precisely. This decision authorizes the cross-entity read
    and fixes that no new `TargetStat` value is created; it does not author
    the selector's spelling.

5.  Heal Resolution read boundary:
    CROSS-ENTITY READ — EXPLICITLY AUTHORIZED by the Product Owner.
    Recorded verbatim: "Authorize an explicit cross-entity read of
    BossState by the Pet heal path".
    The Pet-side Heal Resolution step consults `BossState.StatusEffects[]`
    for instances applicable to Pet healing, at the point §4 item 7
    evaluates its Applicable Heal Modifiers (coverage item 9). The read is
    Pet-step → Boss-held instance; it is one-directional and
    non-mutating (it does not write `BossState`). Coverage item 7 fixes
    that the read does not consume the instance.

6.  Activity window:
    CONFIRMED AS DOCUMENTED — no change. Recorded verbatim: "Confirm as
    documented: Battle Start, active Turns 1–3, expires before Turn 4".
    Creation/activation: applied at Battle Start, evaluated once when the
    battle session is created, before the first Turn; the Battle Start
    application is not a Turn and consumes no duration unit.
    `RemainingTurns = 3` at application.
    Expiration: active throughout Turns 1, 2, and 3; decremented at the
    existing step 19a boundary (`COMBAT_RULES.md` §5.3 DR1–DR5, DR6);
    after step 19a of Turn 3, `RemainingTurns` reaches 0 and the instance
    expires before Turn 4.
    Survival (explicitly stated, as D-4 requires): the modifier SURVIVES
    Turn transitions, Swaps, non-healing actions, and multiple heals, for as
    long as it is active. Only the step-19a decrement reduces its duration.

7.  Consumption/expiry behavior:
    PERSISTENT / Turn-based. The modifier is NOT consumed per heal, NOT
    consumed per action, and NOT consumed once. Recorded verbatim:
    "Persistent Turn-based; not consumed by healing; expires only at
    step 19a".
    The Heal Resolution read is a pure read: reading the modifier does not
    decrement it, remove it, or otherwise alter it. The instance is removed
    only by the existing §5.1.1 item 5 lifecycle removal at the step 19a
    resolution that produces `RemainingTurns = 0` (GAME_STATE.md §2.3.1
    item 8: a stored zero is never an active state).

8.  Reapplication:
    REFRESH — no stack. Recorded verbatim: "Refresh to full duration — no
    stack, one instance". A further application while an instance is active
    refreshes that instance to the full 3-turn duration via the existing
    `COMBAT_RULES.md` §5.2 item 2 MVP default and §5.3 DR3/DR4 (apply and
    refresh are the same "set remaining" operation; same-Turn
    reapplication resets `remaining` and triggers no extra consumption that
    Turn). It does NOT stack additively (−50% + −50% = −100% is explicitly
    not the behavior), at most one instance exists at a time, and the
    source identity is the same (coverage item 2). This confirms
    §6.2.2's existing reapplication rule unchanged, including its
    reachability note: the one-time Battle Start trigger makes
    reapplication unreachable in current MVP content.

9.  Ordering within Heal Resolution:
    CONFIRMED AS DOCUMENTED. Recorded verbatim: "Confirm: applies to Raw
    Heal as one applicable Heal modifier; clamp stays last".
    Position in §4 item 7's canonical order:

      Raw Heal
        ↓
      Applicable Heal Modifiers   ← the −50% applies HERE, as ONE
                                    applicable Heal modifier
        ↓
      Final Heal Amount
        ↓
      item 1's MaxHP / overheal clamp   ← UNCHANGED, STILL LAST
        ↓
      HP update

    The reduction applies to the Raw Heal, through the Applicable Heal
    Modifiers stage, as one applicable Heal modifier and not a
    special-cased site (§4 item 7's own statement). It does NOT apply to
    the final heal after other modifiers, and it does NOT apply after the
    clamp or to the HP delta. §4 item 1's clamp remains last and unchanged.
    No arithmetic rule is invented by this decision: the signed-Magnitude
    percentage convention already owned by `COMBAT_RULES.md` §5.4/§5.5 is
    the existing mechanism; no new formula is authored here.

10. Base healing scope:
    ALL documented Pet-healing sources that use the §4 item 7 resolution,
    explicitly INCLUDING Card Heal (`CARD_RULES.md` §4.1) and HP-Gem
    healing (`GAME_RULES.md` §17 steps 12/14) — confirming §6.2.2's
    existing statement unchanged.
    NON-EFFECTS (confirmed): the reduction does NOT modify MaxHP, and does
    NOT affect Shield. §4 item 7's own closing statement ("It does not
    modify MaxHP, and it does not affect Shield") is unchanged.
    Mộc Yêu's regeneration (`BOSS_RULES.md` §6.2.3) remains OUTSIDE this
    step: it is a direct authoritative `BossState.HP` update, it is not Pet
    healing, and coverage item 3's Pet-only target leaves it unaffected.

11. Interaction with COMBAT_RULES.md §4 item 7's scope:
    UNCHANGED — the clause REMAINS "Scope — Pet HP only".
    Recorded verbatim: "Unchanged — §4 item 7 remains 'Scope — Pet HP
    only'".
    Option C (widening the step to be target-aware/target-agnostic) was NOT
    selected. §4 item 7 is not widened.
    How a Boss-held modifier reaches a Pet-scoped step WITHOUT widening it
    (the reconciliation D-11 requires): the step's SCOPE is unchanged —
    it still governs only healing that restores Pet HP, and it still does
    not govern any Boss-side HP change. What the decision adds is not a new
    scope but an authorized READ: coverage item 5 explicitly authorizes the
    Pet-side step to consult the applicable Boss-owned modifier while
    resolving that Pet healing. The effect's OWNER is the Boss; the
    RESOLUTION SITE is the Pet-scoped step; the cross-entity read is the
    authorized bridge. §4 item 7's sentence that a Boss's own HP
    restoration "does not route through this step" remains true and is
    unaffected, because that sentence concerns Boss HP restoration, not the
    Pet healing this instance reduces.

12. Cross-document consequences (which authoritative documents must change;
    does any existing StatusEffect invariant cease to hold):
    NO existing `StatusEffect` invariant ceases to hold. Every invariant in
    GAME_STATE.md §2.3.1 is confirmed unchanged (coverage item 4).

    Documents/sections requiring update by the subsequent
    documentation-apply task (recorded, NOT modified here):

    REQUIRED — BOSS_RULES.md (canonical owner of the effect's contract)
      §6.2.2 — must state the authorized read boundary: that the Boss-held
               instance is consumed by the Pet-scoped Heal Resolution step
               via an explicit cross-entity read of
               `BossState.StatusEffects[]`, and must state the applicable-
               instance selector precisely enough that an implementer needs
               no further decision. Its existing magnitude, duration,
               Battle Start trigger, application site, reapplication rule,
               and "no new event or protocol" boundary are all CONFIRMED
               UNCHANGED and need no revision.

    REQUIRED — COMBAT_RULES.md
      §4 item 7 — must record that an applicable Heal modifier may be
               Boss-owned and reach this Pet-scoped step through the
               authorized cross-entity read, so the "Scope — Pet HP only"
               clause and a Boss-held modifier are not readable as a
               contradiction. The clause's SCOPE wording itself is
               unchanged (coverage item 11). Its existing statements on
               ordering, the clamp, MaxHP, and Shield are unchanged.
      §5.4.5 / §5.5.3 — must record that this effect is NOT a
               `TargetStat`-consumed `BuffDebuff` and therefore does NOT
               open a new non-"ATK" `TargetStat` case; their "any other
               stat would require its own recorded decision" boundary is
               preserved and is not exercised by this decision.

    REQUIRED — GAME_STATE.md
      §2.4.1 — already states that Thủy Ma's healing reduction is a
               Turn-based instance held in `BossState.StatusEffects[]` and
               that the gameplay rule it consumes is "owned by
               `COMBAT_RULES.md` (§5.3 duration, §5.5 Boss ATK modifiers,
               §4 item 7 Heal resolution)". It must reference the now-
               authored cross-entity read for the §4 item 7 case. NO
               member is added, changed, or repurposed, and §2.3.1's
               member set and invariants are unchanged (coverage item 4,
               §2.3.1 item 12).
      §2.3.1 — NO substantive change. Confirmed unchanged
               (coverage item 4).

    EVALUATED — GAME_RULES.md
      NO change required. §17 step 18a is the match-charged Boss Passive
               application point; §6.2.2's trigger is Battle Start and is
               explicitly not match-charged and emits no
               `PassiveCharged`/`PassiveTriggered`, so §17's step 18a is
               not this effect's application point and needs no revision.
               Step 19a already owns the single duration-consumption point
               (§5.3), which coverage items 6 and 7 confirm. §16's event
               list is unchanged — no event is added or removed. §18
               (server authority) is unaffected.

      Also evaluated, NO change required: GAME_EVENTS.md §2/§3 item 7
               (no new event), SIGNALR_PROTOCOL.md §3/§4 (no new wire
               member; `BossState` client exposure remains the §6.2.4
               limitation requiring its own separate protocol decision),
               REDIS_STATE.md §4/§7 (no new key; rides the existing single
               write-back and round-trip obligation), DATABASE.md
               (no column), ARCHITECTURE.md (no boundary moved; the
               cross-entity read is a Domain-layer read within the existing
               Battle aggregate, not a new layer), TDD.md (no determinism
               or hot-path contract change), PASSIVE_RULES.md (the trigger
               form is unchanged).

ADR required:        NO. This decision changes no architecture, no database
                     strategy, no realtime strategy, no module boundary, and
                     no infrastructure (AGENTS.md §18). The battle-state
                     model is unchanged: the carrier is the already-
                     implemented `BossState.StatusEffects[]` (§2.4.1,
                     TASK-095/TASK-096), the lifecycle is the existing
                     §5.1.1/§5.3 one, and no member, collection, or second
                     representation is added. What the decision adds is a
                     gameplay consumption/read rule — exactly the category
                     `COMBAT_RULES.md` §4 item 7 and §5.4 routinely author.
                     The cross-entity read is a read within the single
                     authoritative `BattleState` aggregate, which the
                     resolution already holds; it crosses no documented
                     boundary. No ADR task is required.

Values changed:      NONE. Thủy Ma Healing Reduction remains −50%; the
                     duration remains 3 turns; the trigger remains Battle
                     Start. Recorded verbatim from the supplied answer:
                     "Unchanged: −50%, 3 turns, Battle Start trigger".
                     No value was re-derived, rounded, or invented.
```

### Decision completeness check (per this task's Stop Conditions)

```text
[x] D-1 through D-12 are each explicitly answered above.
[x] The state carrier is decided (BossState.StatusEffects[] — Option B).
[x] The target is decided and unambiguous (Pet HP healing only).
[x] Heal Resolution visibility is decided and explicit (cross-entity read
    of BossState.StatusEffects[], authorized by the Product Owner).
[x] The activity/lifetime is decided and unambiguous (Battle Start,
    Turns 1–3, step-19a decrement, persistent, expires before Turn 4).
[x] Reapplication is decided and unambiguous (refresh, no stack, one
    instance, same source identity).
[x] No coverage item was filled by inference.
[x] The decision does not depend on a gameplay question outside GAP-5.
[x] The representation violates no existing state invariant — §2.3.1's
    invariants are confirmed unchanged, so no invariant-change decision is
    needed.
[x] The decision introduces no new Battle Event, SignalR member, Redis key,
    or database column (§6.2.2's "no new event or protocol" is preserved).
[x] The decision does not require `BossState` client exposure (§6.2.4's
    separate-decision boundary is respected).
[x] No ADR is required, and that determination is reported above rather
    than authored.

No Stop Condition is met. The decision is COMPLETE.
```

### Reported open detail (not a Stop Condition, not a decision gap)

Coverage item 4 notes that the exact SELECTOR by which the Pet-side read
identifies an applicable Boss-held instance — an `Id`-based identity selector
versus some other documented form — is a spelling the documentation-apply
task must author, because D-10 forbids introducing a new `TargetStat` value.
This is a naming/authoring detail downstream of a decided contract, not an
unanswered D-item: the carrier (D-1), the target (D-3), the read boundary
(D-5), the ordering (D-6), and the lifetime (D-7) are all decided, so an
implementer is not left to choose the mechanic. It is recorded here so the
follow-up task scopes it explicitly rather than silently.


---

## Scope

### In Scope

1. Present the GAP-5 evidence from the authoritative documents (§"Current
   State" above), with both sides of the contradiction cited by path and
   section.
2. Request the Product Owner decision covering all 12 required coverage items.
3. Record that decision verbatim in this task file's "Decision Record" section.
4. Report which authoritative documents the decision will require changing, so
   the follow-up documentation task can be created with an accurate scope.
5. Report whether the decision requires an ADR (`AGENTS.md` §18,
   `architecture/adr-change.md` §2) — reported, not authored.

### Out of Scope

- **Any change under `docs/`.** The decision is recorded in this task file. The
  authoritative-documentation update is a separate, subsequent task (this task
  "Blocks" it). Per `AGENTS.md` §17 and `documentation/documentation-change.md`
  §2, only the canonical owner of a concept may define it, and that edit is that
  task's act.
- **Any source code or test.** Zero files under `src/` or `tests/`.
- **Implementing Hỏa Long's Rage** (`BOSS_RULES.md` §6.2.1), **Mộc Yêu's
  regeneration** (§6.2.3), or **Thủy Ma's healing reduction** (§6.2.2).
- **Modifying `TASK-153`** — it is part of this task's provenance and is
  immutable/read-only. It stays BLOCKED until this decision and the subsequent
  documentation update land.
- **Modifying `BOSS_RULES.md`, `COMBAT_RULES.md`, `GAME_RULES.md`, or
  `GAME_STATE.md`.** This task only records the decision.
- **Modifying the Heal Resolution step** (`COMBAT_RULES.md` §4 item 7).
- **Creating the follow-up documentation task or any implementation task.** This
  task records the decision and reports the resulting scope; sequencing the
  follow-up work is the Orchestrator's act, not this task's.
- **Changing the `−50%` magnitude, the 3-turn duration, or the Battle Start
  trigger** — unless the Product Owner explicitly changes them, in which case
  that change is recorded, not derived.
- **Introducing any new representation** — in particular `PendingStatusEffects[]`
  or any second in-flight collection (`GAME_STATE.md` §2.3.3's prohibition),
  a new Battle Event (`GAME_RULES.md` §16 has none for this effect), a new
  SignalR member, a new Redis key, or a new database column (`BOSS_RULES.md`
  §6.2.2's "no new event or protocol").
- **`BossState` client exposure** — `BOSS_RULES.md` §6.2.4 records this as an
  intentional protocol limitation requiring its own separate decision.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Acceptance Criteria

```text
[ ] GAP-5 is explicitly identified, with both sides of the contradiction
    cited by document path and section.
[ ] A complete Product Owner decision is recorded verbatim in the
    "Decision Record" section.
[ ] State carrier is explicitly defined.
[ ] Effect identity is explicitly defined.
[ ] Target ownership is explicitly defined.
[ ] Representation and schema are explicitly defined, including whether
    GAME_STATE.md §2.3.1's instance schema and its TargetStat-iff-BuffDebuff
    invariant remain unchanged.
[ ] Confirmed that no PendingStatusEffects[] or second in-flight
    representation is introduced (GAME_STATE.md §2.3.3).
[ ] Heal Resolution read boundary is explicitly defined.
[ ] Activity window is explicitly defined.
[ ] Consumption/expiry semantics are explicitly defined.
[ ] Reapplication semantics are explicitly defined.
[ ] Ordering within the Heal Resolution step is explicitly defined, including
    whether COMBAT_RULES.md §4 item 1's clamp remains last and unchanged.
[ ] Base healing scope is explicitly defined, including the MaxHP and Shield
    non-effects.
[ ] The interaction with COMBAT_RULES.md §4 item 7's "Scope — Pet HP only"
    clause is explicitly resolved (changed, or reconciled without widening).
[ ] All existing StatusEffect invariants are explicitly addressed.
[ ] The authoritative documents requiring update are listed, with the owning
    section for each.
[ ] Whether an ADR is required is reported.
[ ] No source code was modified.
[ ] No authoritative gameplay documentation was modified.
[ ] TASK-153 was not modified.
[ ] Zero files under docs/ were modified by this task.
[ ] No gameplay value was invented; the −50% magnitude, 3-turn duration, and
    Battle Start trigger are unchanged unless explicitly decided otherwise.
[ ] Quality review checklist passes (quality/review.md §1)
[ ] No authoritative rules or contracts violated (AGENTS.md §10 / ADR-001)
```

---

## Affected Files & Areas

```text
[ ] src/backend/ — NONE
[ ] src/frontend/client/ — NONE
[ ] tests/ — NONE
[x] tasks/backlog/TASK-154-<this file>.md
      — the decision request and the recorded decision
[ ] docs/ — NONE (the follow-up documentation task applies the decision at
      its canonical owners; the sections that will need updating are listed in
      the Decision Record's coverage item 12)
[ ] tasks/backlog/TASK-153-*.md — NONE (immutable; stays BLOCKED)
```

---

## Implementation Notes

- **This task's deliverable is a recorded decision, not a change.** The
  executing agent's job is to present the evidence accurately, obtain the
  Product Owner's answer, and transcribe it faithfully. It is not to evaluate,
  rank, improve, or implement the options.
- **The precedent to follow is TASK-123.** That task recorded fourteen decision
  groups verbatim ("Recorded verbatim as supplied"), then stated explicitly that
  it was NOT the owner of the documentation edits — TASK-124 was. That split is
  what this task reproduces for GAP-5.
- **The evidence is already gathered.** TASK-153's Stop Condition Report
  (in `tasks/backlog/TASK-153-*.md`) contains the three closed representations
  and the exact line references for both sides of the contradiction. TASK-123's
  D-2 records the same split as the original open question. TASK-124 GAP-5
  records it as deferred. Nothing needs to be re-derived; it needs to be
  resolved by the Product Owner.
- **The two sides, precisely.** `BOSS_RULES.md` §6.2.2 lines ~272–275 place the
  instance in `BossState.StatusEffects[]`; its lines ~276–281 apply the −50% at
  `COMBAT_RULES.md` §4 item 7. `COMBAT_RULES.md` §4 item 7's "Scope — Pet HP
  only" clause (lines ~708–713) states a Boss-side HP change does not route
  through that step. §5.4.5 (lines ~1040–1063) and §5.5.3 (lines ~1209–1234)
  each close the non-"ATK" `TargetStat` case with "would require its own
  recorded decision". `GAME_STATE.md` §2.3.1 item 7 (lines ~1267–1273) fixes the
  `TargetStat`-iff-`BuffDebuff` pairing and the absence convention.
- **Do not modify TASK-153.** Its STOP report is the evidence for this task and
  the reason it exists.
- **Do not create the follow-up tasks.** Report the scope; the Orchestrator
  sequences them.

---

## Testing Requirements

### Required Verification

```text
[ ] Unit tests         — N/A (no code produced by this task)
[ ] Integration tests  — N/A (no code produced by this task)
[ ] Gameplay scenarios — N/A (no behavior implemented by this task)
```

No test is authored or run: this task changes no behavior and produces no code.
The decision it records becomes testable only after the subsequent
documentation task lands and an implementation task exists. Deriving those
scenarios is that implementation task's obligation
(`core/validation.md` §2, `AGENTS.md` §15).

### Verification this task DOES perform

```text
[x] Both sides of the contradiction are cited from the authoritative documents
    by path AND section, with no paraphrase that could soften either side.
[x] The decision's 12 coverage items are each explicitly answered or explicitly
    flagged as unanswered (a flagged item = the task's Stop Condition).
[x] No authoritative document was modified (git status / diff shows zero docs/
    changes attributable to this task).
[x] TASK-153 is byte-identical to its BLOCKED state.
[x] No value was invented: the −50% magnitude, the 3-turn duration, and the
    Battle Start trigger are unchanged unless the decision changed them.
```

### Key Edge Cases

- **The decision addresses the mechanism but not the read boundary.** If the
  answer says "keep it on the Boss and read it during heal resolution" without
  stating *how* the Pet-scoped step discovers it, that is coverage item 5
  unanswered — STOP.
- **The decision selects an option without stating its `TargetStat`/
  representation consequence.** Coverage items 2 and 4 unanswered — STOP.
- **The decision implies a `PendingStatusEffects[]` or any queued/second
  in-flight carrier.** That contradicts `GAME_STATE.md` §2.3.3 and
  `GAME_STATE.md` §0 item 5 — report the conflict, do not record it as
  consistent. STOP.
- **The decision requires a new Battle Event, SignalR member, Redis key, or
  database column.** `BOSS_RULES.md` §6.2.2 states "No new event or protocol".
  A decision that contradicts its own section is an unresolved coupling — STOP
  and report it rather than reconciling it silently.
- **The decision changes the `−50%` magnitude or the 3-turn duration.** That is
  permitted only as an explicit Product Owner change; record it as a value
  change, never derive it.
- **The decision requires `BossState` client exposure.** §6.2.4 records that as
  requiring its own separate protocol decision — STOP, it is out of this task's
  authority.
- **The decision is partial.** Any of the 12 items left unanswered = STOP per
  "Stop Conditions" below.

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 apply.

- **If the Product Owner does not provide a complete decision:** STOP. Do not
  fill any of the 12 coverage items by inference, and do not select an Option A–D
  on the Product Owner's behalf.
- **If the decision requires a new gameplay mechanic not included in the
  question:** STOP per `AGENTS.md` §7 — a new mechanic is a new decision, not a
  detail of this one.
- **If the decision requires an unrelated architecture change:** STOP per
  `AGENTS.md` §18. An architecture change needs its own ADR-first task, not this
  one.
- **If multiple decisions remain coupled but unresolved:** STOP. The Thủy Ma
  effect's carrier, read boundary, and activity window are coupled; a decision
  that resolves one while leaving another open does not close GAP-5.
- **If the state representation cannot be made consistent with `GAME_STATE.md`
  §2.3.1's constraints:** STOP per `AGENTS.md` §4 and report the conflict. In
  particular, a resolution requiring a `TargetStat` value that §5.4.5/§5.5.3
  forbid, or one contradicting item 7's pairing, is a contract conflict — not
  something to reconcile in this task.
- **If Heal Resolution semantics remain ambiguous after the decision:** STOP.
  The read boundary and the ordering must be determinate enough that an
  implementer needs no further decision.
- **If the decision contradicts `BOSS_RULES.md` §6.2.2's own "no new event or
  protocol" boundary:** STOP and report it, do not silently prefer one side
  (`AGENTS.md` §4).
- **If recording the decision would require modifying an authoritative document
  within this task:** STOP. That is the follow-up task's act
  (`AGENTS.md` §17).
- **If the task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries:** STOP & decompose (`tasks/README.md` §12).

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after the decision is recorded.
  Keep concise and factual.
-->

### Changed Files
- `tasks/backlog/TASK-154-resolve-gap-5-thuy-ma-healing-reduction-representation-and-heal-resolution-boundary.md`
  — the "Decision Record" section filled from the Product Owner's supplied
  answer; Status updated to DECIDED; this "Completion Evidence" section
  completed. **No other file was created, modified, or deleted by this
  task.** In particular: zero files under `docs/`, zero under `src/`, zero
  under `tests/`, and `TASK-153` is unmodified.

### Decision Recorded
- See the "Decision Record" section of this file.
- **Decision Status: DECIDED** — all of D-1 through D-12 answered.
- Selected candidate: **Option B** — Boss-side carrier
  (`BossState.StatusEffects[]`) with an explicit, Product-Owner-authorized
  cross-entity read by the Pet-side Heal Resolution step.
- Target: **Pet HP healing only**. §4 item 7's "Scope — Pet HP only" is
  **unchanged** (Option C was not selected).
- `GAME_STATE.md` §2.3.1 invariants: **all unchanged**; no new `TargetStat`
  value is introduced; no `PendingStatusEffects[]` or second in-flight
  representation.
- Magnitude/duration/trigger: **unchanged** at −50% / 3 turns / Battle Start.
- **ADR required: NO.**

### Documents That Will Need Updating
Reported per coverage item 12; **not modified by this task**:

```text
REQUIRED
  docs/01-game-design/BOSS_RULES.md        §6.2.2 — state the authorized
                                           cross-entity read boundary and
                                           the applicable-instance selector
  docs/01-game-design/COMBAT_RULES.md      §4 item 7 — record that an
                                           applicable Heal modifier may be
                                           Boss-owned, reaching this Pet-scoped
                                           step via the authorized read
  docs/01-game-design/COMBAT_RULES.md      §5.4.5 / §5.5.3 — record that this
                                           effect is not a TargetStat-consumed
                                           BuffDebuff and opens no new
                                           non-"ATK" TargetStat case
  docs/02-technical/GAME_STATE.md          §2.4.1 — reference the authored
                                           cross-entity read for the §4 item 7
                                           case (no member changes)
  docs/02-technical/GAME_STATE.md          §2.3.1 — no substantive change;
                                           confirmed unchanged

EVALUATED — NO CHANGE REQUIRED
  docs/01-game-design/GAME_RULES.md        §17 (Battle Start is not step 18a;
                                           step 19a already owns the single
                                           decrement), §16, §18
  docs/02-technical/GAME_EVENTS.md         no new event
  docs/02-technical/SIGNALR_PROTOCOL.md    no new wire member; §6.2.4's
                                           BossState exposure limitation stands
  docs/02-technical/REDIS_STATE.md         no new key; existing write-back
  docs/02-technical/DATABASE.md            no column
  docs/02-technical/ARCHITECTURE.md        no boundary moved
  docs/02-technical/TDD.md                 no determinism/hot-path change
  docs/01-game-design/PASSIVE_RULES.md     trigger form unchanged
```

### Validation Results
```text
[x] GAP-5 identified with both sides cited by path and section — the
    §"Current State" section ("The exact ambiguity") and the "Decision
    Record" coverage items 5 and 11 cite BOSS_RULES.md §6.2.2 against
    COMBAT_RULES.md §4 item 7's "Scope — Pet HP only", plus §5.4.5/§5.5.3
    and GAME_STATE.md §2.3.1 item 7.
[x] A complete Product Owner decision is recorded in the "Decision Record"
    section, transcribed from the supplied answer.
[x] All 12 coverage items are explicitly answered — see the
    "Decision completeness check" subsection.
[x] No coverage item was filled by inference.
[x] The cross-entity read (the item D-5 requires be explicitly authorized
    if needed) WAS explicitly authorized by the Product Owner.
[x] Zero files under docs/ modified by this task (verified: git status shows
    no docs/ change attributable to this task; the docs/ modifications
    present in the worktree pre-date this task).
[x] Zero files under src/ or tests/ modified by this task.
[x] TASK-153 unmodified.
[x] No value was invented: −50%, 3 turns, and the Battle Start trigger are
    recorded as unchanged (recorded "Values changed: NONE").
[x] No new Battle Event, SignalR member, Redis key, or DB column is
    introduced by the decision.
```

### Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (no code produced)
- [x] Confirmed zero files under `docs/` modified
- [x] Confirmed zero files under `src/` or `tests/` modified
- [x] Confirmed TASK-153 unmodified (still BLOCKED)
- [x] Confirmed no new Battle Event, SignalR member, Redis key, or DB column
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed no new task was created (the follow-up documentation task is
      the Orchestrator's act, not this task's)
