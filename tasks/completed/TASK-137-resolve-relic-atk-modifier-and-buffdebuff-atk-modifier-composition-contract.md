# TASK-137 — Resolve the Relic Battle-Lifetime ATK Modifier × Turn-Based `BuffDebuff` ATK Modifier Composition Contract

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and tasks/ by path and section.

  THIS TASK IS STRICTLY A DECISION-INPUT TASK.

    Documented ambiguity
            ↓
    Present decision options
            ↓
    Obtain explicit Product Owner / human decision
            ↓
    Record the decision in TASK-137
            ↓
    STOP

  THIS TASK MUST NOT APPLY THE DECISION TO AUTHORITATIVE DOCUMENTATION.
  A subsequent contract-application task consumes the recorded decision and
  updates the canonical documents. TASK-137's deliverable is the RECORD.

  THIS TASK DECIDES NOTHING BY ITSELF AND IMPLEMENTS NO BEHAVIOR. It presents
  the documented evidence of a genuine two-producer composition gap and requires
  the appropriate human/Product-Owner decision. Inventing a coexistence answer,
  a composition operator, an order, a rounding point, or a cap is the single
  prohibited action of this task (AGENTS.md §7, §20).

  DECISION OWNERSHIP: D1–D7 are NOT implementation decisions for the executing
  agent. No option may be selected because it is easier to implement, because
  one formula is already written, because §5.4.1 came first in the document,
  because `EffectiveCrit` composes by summing, or because it is simplest for the
  Damage Pipeline. `EffectiveCrit` (§3.3 item 7), `EffectiveCardCost`
  (`CARD_RULES.md` §3.6), and `ADR-017`/`ADR-018`'s modifier collections are
  precedent/evidence only — they are NOT the answer, and their semantics must
  not be reused by analogy for ATK.

  PROVENANCE: identified during TASK-136's contract-application step and
  recorded there as TASK-136's own Note N5 ("The relationship between the two
  rules … is not stated by D5 or D11"), then authored into the canonical owner
  as an explicitly UNRESOLVED open item at `COMBAT_RULES.md` §5.6.6. TASK-136
  resolved the Relic-sourced ATK modifier's runtime carrier
  (`GAME_STATE.md` §2.3.7/§5.1.4) and its own composition (`COMBAT_RULES.md`
  §5.6.1), but §5.6.1 composes `ATKModifiers[]` entries **among themselves
  only**. It does not compose them with a Turn-based `BuffDebuff`
  `TargetStat = "ATK"` modifier, which §5.4.1 independently composes into the
  same Damage Pipeline Step-1 `Attack` input.

  THIS IS A STOP-DRIVEN TASK, NOT A FEATURE. `COMBAT_RULES.md` §5.6.6 records
  that "an implementation that would need to apply both simultaneously must STOP
  per `AGENTS.md` §7 rather than choosing an order or a formula", and TASK-133's
  own Stop Conditions fire on it (line 292: "If any trigger, condition, effect,
  magnitude, or lifetime needed is not fully determined … STOP"; line 295: "If a
  Relic effect appears to require a `docs/` rule that does not exist … STOP and
  report"). TASK-133 remains BLOCKED on this single point; TASK-133 is NOT
  modified by this task.

  BOUNDARY: decision recording only. Zero files under docs/, src/, or tests/.
  This task creates no ADR, changes no gameplay rule, adds no Relic, Boss, or
  combat behavior, introduces no ATK cap, and does not touch TASK-131, TASK-132,
  TASK-133, or TASK-136.
-->

---

## Metadata

```text
Task ID:           TASK-137
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md). This task is the
                   DECISION-INPUT half of that workflow only: it obtains and
                   records the Product Owner decision. It does NOT perform the
                   canonical-owner documentation write that the workflow's
                   later steps describe — that is the subsequent
                   contract-application task's act, consuming this record.
Status:            DONE (the Product Owner supplied an explicit answer for all
                   seven decision items D1–D7. The decision was recorded verbatim
                   in "Decision Record (D1–D7)" without reinterpretation or
                   added assumption, and the seven "Required Decision Coverage"
                   items were resolved accordingly. TASK-137 modified no file
                   other than this one: zero `docs/`, zero `src/`, zero `tests/`,
                   and TASK-119/131/132/133/136 are byte-identical. The canonical
                   documentation write is the SUBSEQUENT contract-application
                   task's act, consuming this record — TASK-137 does not perform it.)
                   Lifecycle reconciliation: recorded DONE while the file remained
                   in tasks/backlog/; moved to tasks/completed/ per
                   TASK_LIFECYCLE.md §3 (direct execution verified: D1–D7
                   recorded, all forty-two acceptance criteria satisfied).
Risk:              MEDIUM (TASK_TYPES.md §4 — DOCUMENTATION baseline LOW–MEDIUM;
                   MEDIUM because the decision it records determines the
                   Damage Pipeline's Step-1 `Attack` input for the active Pet
                   whenever two documented ATK producers are simultaneously
                   live — a combat-arithmetic rule, and the same class of
                   gameplay decision TASK-119, TASK-128, and TASK-136 each
                   recorded. No `docs/` file is modified by this task.)
Priority:          HIGH (the sole remaining blocker on TASK-133's Berserker Core
                   ATK path. TASK-133 is otherwise contract-complete for ATK:
                   the carrier, shape, lifetime, composition, rounding, refresh,
                   removal, serialization, and projection are all authored.
                   This one interaction is all that remains.)
Primary Agent:     review (TASK_TYPES.md §2 / AGENT_SELECTION.md §1 —
                   "Documentation change: Primary Agent Review". No domain
                   agent may author the answer; the Product Owner supplies it.)
Supporting Agents: gameplay (COMBAT_RULES.md §5.4 and §5.6 are the owning
                   domain sections — consulted to CONFIRM the two formulas and
                   the scope statements the decision must reconcile, not to
                   author the answer; BOSS_RULES.md §6.3.1 item 3 owns Root's
                   magnitude and duration),
                   backend (GAME_STATE.md §2.3.1 owns the `StatusEffects[]`
                   instance and §2.3.7 owns `ATKModifiers[]`; §5.1.1 and §5.1.4
                   own their lifecycles — consulted to confirm the existing
                   representation and the two removal paths)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-136 (DONE — the Relic Battle-lifetime ATK contract; this
                   task resolves only the one interaction TASK-136 D5/D11 left
                   open and its Note N5 recorded),
                   TASK-119 (DONE — the `BuffDebuff` `TargetStat = "ATK"`
                   consumption contract, `COMBAT_RULES.md` §5.4),
                   TASK-128 (DONE — the Boss-side ATK modifier direction
                   contract, `COMBAT_RULES.md` §5.5; the deliberately separate
                   counterpart, EVIDENCE ONLY),
                   TASK-094/TASK-095 (DONE — the Status Effect duration model,
                   `COMBAT_RULES.md` §5.3 and `GAME_STATE.md` §2.3.1/§5.1.1)
Blocks:            TASK-133 — Implement Server-Authoritative Relic Trigger and
                   Effect Resolution (its Berserker Core / `ATK` path cannot be
                   implemented until this is resolved)
Estimate:          Simple (one recorded composition decision, D1–D7; zero code,
                   zero documentation edits, zero gameplay rules)
```

---

## Objective

Obtain and record, **in this task only**, the explicit Product-Owner / human
decision defining **how a Relic Battle-lifetime Pet ATK modifier and a
Turn-based `BuffDebuff` `TargetStat = "ATK"` modifier interact** — whether they
may coexist on the same Pet attack, how they compose, in what order, where the
result is rounded, what the resulting effective ATK means, how removal and
expiry interact, and whether the base stat stays independent — so that a
subsequent contract-application task can update the canonical documents and make
TASK-133 implementation-ready.

This task records the decision. It implements no composition, no formula, and no
resolver, and it **applies the decision to no authoritative document**.

---

## Current State

```text
Relic ATK contract (TASK-136) — COMPLETE, do not reopen:
  RELIC_RULES.md §8.3      ATK | Pet | Battle | Percentage
  RELIC_RULES.md §8.5      Berserker Core: value 5, target Pet, lifetime Battle
  GAME_STATE.md §2.3.7     PetState.ATKModifiers[] — SourceIdentity +
                           ATKModifierPercentage; always present; at most one
                           entry per source; ordered by SourceIdentity
  GAME_STATE.md §2.3.8     serialization / lossless round-trip / [] when empty
  GAME_STATE.md §5.1.4     lifecycle: create / replace-or-refresh / remove
                           (source removal or battle end)
  COMBAT_RULES.md §5.6.1   EffectivePetATK =
                           truncate( PetState.ATK × (100 + Total) / 100 )
                           Total = Σ ATKModifierPercentage (signed, additive)
  COMBAT_RULES.md §5.6.1 item 5   NO ATK cap is authored
  COMBAT_RULES.md §5.6.2   multiple sources summed; independent contributions

BuffDebuff ATK contract (TASK-119) — COMPLETE, do not reopen:
  COMBAT_RULES.md §5.4.1   EffectiveATK =
                           truncate( PetState.ATK × (100 − |Magnitude|) / 100 )
                           reduction-only; uses the ABSOLUTE value
  COMBAT_RULES.md §5.4.2   truncated toward zero
  COMBAT_RULES.md §5.4.3   active per the committed StatusEffects[] instance
                           state at attack resolution; duration per §5.3
  COMBAT_RULES.md §5.4.4   base stat never overwritten; EffectiveATK derived,
                           NOT stored
  COMBAT_RULES.md §5.4.5   SCOPE: "Applies to a Turn-based BuffDebuff instance
                           with TargetStat = ATK" — and nothing else
  GAME_STATE.md §2.3.1     the StatusEffect instance (RemainingTurns XOR
                           ExpiryCondition duration dichotomy)
  GAME_STATE.md §5.1.1     the step 19a Status Effect lifecycle
  COMBAT_RULES.md §5.3     duration consumption (DR1–DR6)

The two producers of ONE input:
  COMBAT_RULES.md §5.4.1 item 1 and §5.6.1 item 1 each name the SAME
  consumption point — the Player → Boss Damage Pipeline Step 1 `Attack`
  argument. Both are documented. Neither references the other.

Recorded as UNRESOLVED in the canonical owner:
  COMBAT_RULES.md §5.6.6   "Interaction With §5.4's BuffDebuff ATK Modifier —
                           UNRESOLVED": no document determines (a) whether both
                           may be active simultaneously, (b) how they compose,
                           or (c) in what order.
  RELIC_RULES.md §2.4 item 6 and §8.5 item 4 cross-reference it as undecided.

Boss side (EVIDENCE ONLY — the deliberately separate counterpart):
  COMBAT_RULES.md §5.5     EffectiveBossATK uses the SIGNED Magnitude, while
                           §5.4.1 uses the ABSOLUTE value — §5.5.1 states the
                           two "must NOT be collapsed into one shared formula".
                           This is evidence that this repository treats
                           Pet-side and Boss-side ATK composition as separate
                           rules; it does NOT answer this task.

Documented prohibitions the answer must respect:
  COMBAT_RULES.md §5.4.5   a source for which this document defines no
                           consumption rule "is NOT silently treated as an ATK
                           modifier"
  COMBAT_RULES.md §5.4.4 / §5.6.4   base stat never overwritten; no restore step
  GAME_STATE.md §0 item 5  no second representation of a value
  GAME_STATE.md §2.3.7 item 9 / §5.1.4 item 6   PetState.ATK never mutated
  COMBAT_RULES.md §5.6.1 item 5   no ATK cap is authored
```

---

## Problem / Ambiguity

Two **independently complete and authoritative** rules each produce the **same**
Damage Pipeline Step-1 `Attack` input for the same Pet attack:

```text
COMBAT_RULES.md §5.4.1 — BuffDebuff (Turn-based)
    EffectiveATK    = truncate( PetState.ATK × (100 − |Magnitude|) / 100 )
                      reduction-only; ABSOLUTE value

COMBAT_RULES.md §5.6.1 — Relic (Battle lifetime)
    EffectivePetATK = truncate( PetState.ATK × (100 + TotalATKModifierPercentage) / 100 )
                      signed; within-collection additive
```

Neither document references the other, and `§5.4.5` fixes the scope of §5.4 to
"a Turn-based `BuffDebuff` instance with `TargetStat = "ATK"`" and states that
this section "defines the `"ATK"` case only". `§5.6.5` states §5.6 "Does NOT
change … §5.4's `BuffDebuff` rule". So each rule is complete **in isolation**,
and the composition of the two is defined nowhere.

### The concrete MVP case (both producers are provisioned content)

```text
Berserker Core  (RELIC_RULES.md §8.5, provisioned)
    Trigger    OnMatchCount
    Condition  MatchCountAtLeast(3)
    Effect     { "effectType": "ATK", "valueType": "Percentage", "value": 5,
                 "target": "Pet", "lifetime": "Battle" }
    → +5% ATK, Battle lifetime, held in PetState.ATKModifiers[]

Mộc Yêu Root    (BOSS_RULES.md §6.3, §6.3.1 item 3, provisioned)
    Source     Boss Skill: Mộc Yêu, "Root", charge 6 matches, CD 2T
    Effect     "-30% Pet ATK debuff for 2 Turns"
    → -30% Pet ATK, Turn-based (2 Turns), held in PetState.StatusEffects[]
      as a BuffDebuff instance with TargetStat = "ATK"
```

This is **not hypothetical**. `COMBAT_RULES.md` §5.6.6 already records the case
as reachable: "a battle in which Root is active at a Pet attack and Berserker
Core's Condition has been met therefore has both producers live."

A battle against Mộc Yêu where the player has equipped Berserker Core and reached
3 cumulative Matches, with Root applied at step 18b and still holding at
`GAME_RULES.md` §17 step 15, is a documented, provisioned, ordinary battle.

### The documented gap (preserved verbatim in substance — do not solve it here)

```text
PetState.ATK = 50   (COMBAT_RULES.md §1.1 MVP default)

§5.6.1 alone:  truncate(50 × (100 + 5)/100)  = truncate(52.5) = 52
§5.4.1 alone:  truncate(50 × (100 − 30)/100) = truncate(35.0) = 35

BOTH live:     ??? — no document states the result
```

Every candidate composition is materially different and none is documented:

```text
apply §5.4.1 to §5.6.1's result   →  truncate(52 × 70/100) = truncate(36.4) = 36
apply §5.6.1 to §5.4.1's result   →  truncate(35 × 105/100) = truncate(36.75) = 36
combine percentages first         →  total = 5 − 30 = −25
                                     truncate(50 × 75/100) = truncate(37.5) = 37
```

(The first two coincide here by arithmetic accident at these values; they do not
generally. This task does not select any of them and does not treat the
coincidence as evidence.)

### Why this is a genuine gap and not an implementation detail

1. **It is a gameplay rule, not an arithmetic detail.** Which of the three
   shapes above (or another) governs is a balance and design decision. The
   repository has recorded three separate Product-Owner decisions in this exact
   area already — TASK-119 (§5.4's Pet-side rule), TASK-128 (§5.5's Boss-side
   direction), TASK-136 (§5.6's Relic rule) — each because the composition was
   not derivable.
2. **`§5.4.5` makes it a stop, not a default.** Its closing paragraph states a
   source for which the document defines no consumption rule "is **not**
   silently treated as an ATK modifier". A Relic `ATKModifiers[]` entry is a
   source §5.4 does not define, so §5.4 cannot be applied over it by default.
3. **The two rules use incompatible arithmetic by construction.** §5.4.1 takes
   the **absolute value** and can only reduce (`ATK 50 → 35`, never above 50);
   §5.6.1 takes the **signed** value and can raise or lower. Composing them
   requires deciding which convention governs the combined result — and
   `§5.5.1` shows the repository explicitly refused to collapse even two
   *similar* conventions into one shared formula.
4. **No analogy is licensed.** `EffectiveCrit` (§3.3 item 7) composes several
   sources by summing **percentage points on a percentage-point stat**;
   `EffectiveCardCost` (`CARD_RULES.md` §3.6) composes cost reductions with a
   100% cap and a truncation. Neither is a composition of two competing
   producers of a single multiplicative Step-1 input, and §5.6.6 already records
   that `EffectiveCrit` "cannot be transferred by analogy without deciding (b)".
5. **The surrounding mechanics are already fully determined**, so this is
   narrowly scoped: the carrier, shape, refresh, removal, serialization, and
   projection for *both* modifier types are authored and unambiguous. Only their
   interaction is missing.

Inventing the answer is the prohibited action (`AGENTS.md` §7, §20).

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 (Relics and Bosses IN; Bosses are provisioned content), §4 (unlisted ⇒ FUTURE)
- `docs/01-game-design/COMBAT_RULES.md` **§5.4** (the Turn-based `BuffDebuff` consumption rule — this task's second producer): **§5.4.1** (the consumption point and the `EffectiveATK` formula, reduction-only, absolute value), §5.4.2 (rounding, truncated toward zero, and its worked integer table), §5.4.3 (activity at attack resolution; the same-Turn non-retroactivity example), **§5.4.4** (the base stat is never overwritten; `EffectiveATK` is derived and not stored; no new collection is introduced because the modifier lives in `StatusEffects[]`), **§5.4.5** (the scope statement fixing `BuffDebuff`-only applicability, and its closing "not silently treated as an ATK modifier" rule)
- `docs/01-game-design/COMBAT_RULES.md` **§5.6** (the Relic Battle-lifetime ATK composition — this task's first producer, authored by TASK-136): **§5.6.1** (the composition, its formula, the signed percentage-point semantics, truncation toward zero, **item 5's explicit "no ATK cap is authored"**, and item 6's evaluation timing), §5.6.2 (within-collection additivity and the "applying each modifier in sequence is incorrect" statement), §5.6.3 (lifetime, refresh, removal), §5.6.4 (base-stat independence, adopted by TASK-136 D5/D8), §5.6.5 (scope and boundaries), **§5.6.6 (the UNRESOLVED interaction this task resolves)**
- `docs/01-game-design/COMBAT_RULES.md` **§5.5 / §5.5.1–§5.5.5** (the Boss-side counterpart — **EVIDENCE ONLY, NOT the answer**): §5.5.1's signed-magnitude formula and its statement that the Pet-side and Boss-side conventions "must **not** be collapsed into one shared formula"; §5.5.3's scope; §5.5.4's non-destructive position; §5.5.5's duration/reapplication deferral to §5.3 and §5.2 item 2
- `docs/01-game-design/COMBAT_RULES.md` **§5.1** (the MVP Status Effect type list), **§5.2** item 1 (the duration-or-trigger expiry dichotomy) and item 2 (the **refresh duration, do not stack magnitude** MVP default), **§5.3 / §5.3.1–§5.3.4** (the canonical duration-consumption rule, DR1–DR6, the apply/refresh ordering, and the worked examples), **§3 / §3.1** (the Damage Pipeline's fixed six-step order, Step 1's contributions, and step 4), **§3.3 item 7** (`EffectiveCrit` — the contrast case, and the analogy §5.6.6 refuses), §3.4 (the Boss basic-attack Step-1 input), **§1.1** (the MVP stat defaults: `ATK` = 50; and the explicit statement that `ATK` has **no** documented range while `Power` and `Crit` do)
- `docs/01-game-design/RELIC_RULES.md` **§8.3** (`ATK | Pet | Battle | Percentage`), **§8.5 item 4** (Berserker Core's runtime carrier, lifecycle, and composition references, and its cross-reference to `COMBAT_RULES.md` §5.6.6 as undecided), §2.4 item 6 (the decided-exceptions table, now naming `ATK` → `COMBAT_RULES.md` §5.6, and recording the unresolved `BuffDebuff` interaction), §8.2 (the structured effect declaration), §8.4 (lifetime vs trigger re-evaluation), §8.7 (startup status)
- `docs/01-game-design/BOSS_RULES.md` **§6.3 (the provisioned Boss Skill table — Mộc Yêu Root: `-30% Pet ATK debuff for 2 Turns`)** and **§6.3.1 item 3** (Root's magnitude, its percentage representation, and its Turn-based duration deferral to `COMBAT_RULES.md` §5.3), §6.3.1 item 2 (Thủy Ma's flat, instant Power reduction — the contrast case for a non-persistent effect)
- `docs/01-game-design/GAME_RULES.md` **§17** (the fixed resolution order: step 11 Relics, step 15 the Pet's attack, step 18b Boss Skill effects, step 19a Status Effect duration), §12 (Power rules), §14 (Combat Rules), §16 (Battle Event Model), §18 (server authority), **§20 (Rule Change Policy — the mechanism a composition decision is recorded under)**, §1.4 (the battle-end boundary)
- `docs/02-technical/GAME_STATE.md` **§2.3** (`PetState`, and the `ATK` annotation), **§2.3.1** (the `StatusEffect` instance schema: `id`/`type`/`source`/`magnitude`/`targetStat`/`remainingTurns`/`expiryCondition`, the duration-model dichotomy, one-instance-per-identity, and the `Magnitude`-is-handed-off-not-interpreted rule naming `COMBAT_RULES.md` §5.4), **§2.3.7** (`ATKModifiers[]` — `SourceIdentity` + `ATKModifierPercentage`), **§2.3.8** (its serialization), **§5.1** (the single post-resolution write-back), **§5.1.1** (the step 19a Status Effect lifecycle), **§5.1.4** (`ATKModifiers[]` lifecycle), **§0 items 4–5** (a member is added by its owning decision; no second representation)
- `docs/02-technical/REDIS_STATE.md` §7 items 13–16 (the sibling-modifier round-trip entries, if the decision has any storage consequence — it should have none)
- `docs/02-technical/TDD.md` §6 (determinism — any composition must be integer-deterministic)
- `docs/02-technical/ARCHITECTURE.md` §2.1 (Domain purity), §5 (anti-overengineering)
- `docs/03-decisions/README.md` §2 (ADR criteria), §4 (numbering — never reused), §5 (status values), §8 (known open items)
- **`docs/03-decisions/ADR/ADR-017-nextattack-crit-modifiers-as-petstate-battle-state.md`** and **`ADR-018-structured-relic-trigger-condition-effect-contract.md`** — the modifier-state precedents. **EVIDENCE ONLY**: both concern a `NextAttack` consumption boundary or a `CardCost` reduction, and neither composes two producers of one Step-1 input
- `AGENTS.md` §4 (conflict resolution — report, do not silently resolve), §7 (invent no rule), §8 (MVP protection), §9 (anti-overengineering), §10 (server authority), §11 (determinism), §12 (domain boundaries — Combat vs Relic vs Boss), §16 (task discipline), §17 (documentation change), §18 (architecture change rule), §20 (stop conditions), §22
- `tasks/backlog/TASK-136-resolve-battle-lifetime-pet-atk-modifier-runtime-contract.md` — the Relic ATK contract this task does not reopen; its **Note N5** records exactly this interaction as left open
- `tasks/completed/TASK-119-resolve-root-atk-modifier-consumption-contract.md` — the `BuffDebuff` Pet-side ATK consumption contract (`COMBAT_RULES.md` §5.4) this task does not reopen
- `tasks/completed/TASK-128-resolve-boss-side-atk-modifier-direction-contract.md` and `TASK-129-apply-boss-side-atk-modifier-direction-contract.md` — the closest **process** precedent: a narrowly scoped decision task for an ATK-modifier semantic, followed by its application task. **Do not copy its Boss-side answer.**
- `tasks/completed/TASK-125-resolve-boss-skill-step-1-damage-composition.md` and `TASK-126-apply-boss-skill-step-1-damage-composition-contract.md` — the process precedent for resolving and then applying a **Step-1 composition** decision
- `tasks/completed/TASK-116-resolve-nextattack-crit-modifier-state-and-consumption-contract.md` and `TASK-117-author-adr-017-nextattack-crit-modifier-state-model.md` — the modifier-contract decision precedent
- `tasks/backlog/TASK-133-implement-server-authoritative-relic-trigger-and-effect-resolution.md` — the task this blocks

---

## Decision Question

> **When a Relic Battle-lifetime Pet ATK modifier (Berserker Core, `+5%`, Battle)
> and a Turn-based `BuffDebuff` `TargetStat = "ATK"` modifier (Mộc Yêu Root,
> `−30%` Pet ATK, 2 Turns) are both in force at the same Pet attack, may both
> apply, and if so how do they compose into the Damage Pipeline Step-1 `Attack`
> input?**

The task must determine the authoritative contract for:

```text
coexistence
composition operator
application order
rounding / truncation point
resulting effective ATK semantics
interaction with removal / expiry
base PetState.ATK independence
```

None of these is to be implemented.

---

## Required Decision Coverage

The executing agent must obtain explicit answers to all of the following. **An
unanswered item is a blocking stop condition, not an invitation to choose.**
Every item below carries the status `NOT DECIDED` until the Product Owner
answers it.

```text
D1 — Coexistence:
     May a Relic Battle-lifetime ATK modifier and a Turn-based `BuffDebuff`
     `TargetStat = "ATK"` modifier both apply to the same Pet attack?
     The candidate categories must be presented NEUTRALLY. No option is
     recommended, ordered by ease, or eliminated by this task:
       A. Yes — both apply, and they compose (see D2/D3)
       B. Yes — both apply, but one takes precedence and the other is
          suppressed/ignored while both are live
       C. No — they may not coexist; one is rejected/refused by rule
       D. Another explicitly defined rule
     If the answer is B or C, the decision must state which source wins, and
     what happens to the suppressed source's stored entry, its removal/
     expiry, and any event or state consequence.
     Selection must NOT be made because §5.4.1 is already implemented, because
     §5.6.1 is newer, because one is a Relic and the other a Boss effect, or
     because either is simpler for the Damage Pipeline.           NOT DECIDED

D2 — Composition operator if they coexist:
     If D1 permits coexistence, the decision must define the operator that
     combines the two. The candidates must be presented NEUTRALLY; the
     arithmetic illustrations below are EVIDENCE of divergence, not options
     this task selects between:
       a. sequential — §5.4.1 applied to §5.6.1's result
       b. sequential — §5.6.1 applied to §5.4.1's result
       c. combine the percentages first, then apply a single formula
       d. another explicitly defined operator
     The decision must state the operator, and must state which rule's
     percentage convention governs the combined result (see D5 on the
     absolute-vs-signed incompatibility).
     Do NOT select one because it is easier to implement, and do NOT treat the
     fact that candidates (a) and (b) coincide at the provisioned magnitudes as
     evidence for either.                                       NOT DECIDED

D3 — Application order:
     If D1 permits coexistence and D2 selects a sequential operator, the
     decision must define the order: which rule is applied first, and whether
     the order is fixed or commutative. If D2's operator is order-independent,
     the decision must state that explicitly and state why.
     Do NOT default to document order (§5.4 precedes §5.6 in the file) or to
     implementation convenience.                                 NOT DECIDED

D4 — Rounding / truncation point:
     Define exactly where and how many times the result is truncated toward
     zero. Both existing rules truncate
     (`§5.4.2`, `§5.6.1 item 4`), so a sequential composition can truncate
     once or twice and yield different integers. The decision must state:
       the truncation point(s)
       whether an intermediate value is truncated or carried at full precision
       that the result is integer-deterministic (TDD.md §6)
     Do NOT assume one truncation; do NOT choose based on what is simplest. NOT DECIDED

D5 — Resulting EffectivePetATK semantics:
     Define what the composed value means and how it is named/owned, resolving
     at minimum:
       whether a single composed value is produced, or the two rules each
       produce a value and one consumes the other's
       which percentage convention governs — §5.4.1 uses the ABSOLUTE value and
       is reduction-only; §5.6.1 uses the SIGNED value and is
       increase-or-decrease (COMBAT_RULES.md §5.5.1 shows the repository
       refusing to collapse two such conventions into one formula)
       whether the composed result may exceed PetState.ATK (an increase) or is
       bounded by it (reduction-only), and what determines that
       whether any cap applies — see D5 note below
       where the composed value is used (Damage Pipeline Step 1 `Attack`)
       that it remains derived, NOT stored (GAME_STATE.md §0 item 5)
     **Cap note (mandatory, non-authoring):** `COMBAT_RULES.md` §5.6.1 item 5
     records that **no ATK cap is authored**, because `ATK` has no documented
     valid range in §1.1 (unlike `Power`'s 0–100 and `Crit`'s 0–100 percentage
     points). TASK-136 D5 permits clamping only to a DOCUMENTED valid ATK range
     and forbids inventing a new stat cap. This task must NOT invent one. If the
     Product Owner wishes a cap, that is a separate explicit decision to author
     an ATK range, and it must be recorded as such here — not assumed. NOT DECIDED

D6 — Interaction with removal / expiry:
     Define how the two modifier types' lifecycles interact where they differ:
       the Relic modifier's removal is source-removal or battle end
       (`GAME_STATE.md` §5.1.4 item 4)
       the BuffDebuff modifier's expiry is Turn-based via `COMBAT_RULES.md`
       §5.3 (DR1–DR6), consumed at `GAME_RULES.md` §17 step 19a
     The decision must state what happens to the composed value when EITHER
     source is removed or expires mid-battle — in particular whether the other
     source's contribution is unaffected, recomposed, or otherwise changed, and
     whether any re-application/refresh of one source affects the other.
     It must also state whether the ordering relationship between the step 19a
     consumption and the Pet's attack (step 15) has any consequence for the
     composition. Do NOT author a new duration or expiry model.   NOT DECIDED

D7 — Base PetState.ATK independence:
     Confirm that `PetState.ATK` remains the permanent/base stat and is never
     mutated by either modifier or by their composition, that there is no
     "restore" step, and that the configured default is an initialization value
     only and never an expiry or reset mechanism.
     Confirm this explicitly for this composition rather than inheriting it
     silently from `§5.4.4` or `§5.6.4`: cite them or decide otherwise
     explicitly. Do not introduce a second ATK representation.    NOT DECIDED
```

**All seven items above were `NOT DECIDED` and have since been answered by the
Product Owner — see "Decision Record (D1–D7)".** The `NOT DECIDED` marker on
each item records the state in which the decision was requested; the answer for
each is the correspondingly-numbered decision in that section, which is the
recorded decision and the input to the downstream contract-application task. The
markers are retained rather than rewritten so the original request and the
supplied answer remain separately auditable.

---

## Decision Options

Presented for the decision-maker. **This task recommends none of them, orders
them by nothing, and selects none.** Each is listed only to make the decision
space explicit and to show that materially different, documented-consistent
options exist.

```text
D1 — Coexistence
  A. both apply and compose
  B. both apply but one takes precedence
  C. they may not coexist
  D. another explicit rule

D2 — Composition operator (if coexistence)
  a. §5.4.1 applied to §5.6.1's result
  b. §5.6.1 applied to §5.4.1's result
  c. percentages combined first, then one formula
  d. another explicit operator

D3 — Order
  fixed order (which first) | order-independent (stated explicitly)

D4 — Rounding
  single truncation at the end | intermediate truncation (stated) | other

D5 — Resulting semantics
  one composed value | one rule consumes the other's value
  absolute/reduction-only convention | signed convention | another stated rule
  no cap (current documented state) | a cap only if a range is explicitly
    authored as a separate decision

D6 — Removal/expiry
  removal of either source recomposes from the survivor | other explicit rule

D7 — Base stat
  PetState.ATK unchanged and never mutated (citing §5.4.4 / §5.6.4) |
  another explicit position
```

---

## Decision Record (D1–D7)

**Recorded verbatim as supplied by the Product Owner.** Each item answers the
correspondingly-numbered item of "Required Decision Coverage". No value below
was derived, inferred, computed, or reinterpreted by this task, and no
assumption was added.

```text
D1 — Coexistence:
The Relic Battle-lifetime ATK modifier and the Turn-based BuffDebuff
ATK modifier coexist and both affect EffectivePetATK.

D2 — Composition operator:
The modifiers are composed as a single signed percentage adjustment
against the permanent Base Pet ATK.

D3 — Composition order:
Composition is order-independent. Do not apply Relic and BuffDebuff
modifiers sequentially in an order-dependent pipeline.

D4 — Truncation / rounding:
Apply the combined percentage to Base Pet ATK and truncate toward zero
exactly once when producing the integer EffectivePetATK.

D5 — EffectivePetATK semantics:
EffectivePetATK is:

truncate(
    PetState.ATK
    × (100 + TotalATKModifierPercentage)
    / 100
)

where TotalATKModifierPercentage is the signed sum of all applicable
Pet ATK modifiers from both:
- PetState.ATKModifiers[]
- applicable Turn-based BuffDebuff ATK StatusEffects[]

No new ATK cap is introduced by this decision.

D6 — Removal / expiry interaction:
The two modifier carriers retain independent ownership and lifetime.

When Mộc Yêu Root expires, only its StatusEffects[] entry is removed.
The Berserker Core ATKModifiers[] entry remains active for the rest
of the Battle.

A modifier is never removed merely because another modifier expires.

D7 — Base PetState.ATK independence:
PetState.ATK remains the permanent/base Pet ATK value.

Neither Relic ATK modifiers nor BuffDebuff ATK modifiers may overwrite,
mutate, or reset PetState.ATK.

No DefaultATK-style runtime reset mechanism is introduced.
EffectivePetATK remains derived state.
```

---

## Scope

### In Scope

1. **Obtain the decision.** Present D1–D7 to the Product Owner / human and
   obtain explicit answers. Present every candidate option neutrally.
2. **Record the decision in this task.** Write the supplied answers into
   "Decision Record" verbatim. Record nothing the Product Owner did not supply.
3. **Record the TASK-136 relationship.** State that this task resolves only the
   one interaction TASK-136 Note N5 left open, and that TASK-136's D1–D12 are
   not reopened. Do **not** modify TASK-136.
4. **Record the TASK-119 relationship.** State that `COMBAT_RULES.md` §5.4's
   `BuffDebuff` rule is not reopened; the decision composes with it. Do **not**
   reopen or alter §5.4.
5. **Record the TASK-133 relationship.** State that TASK-133 remains BLOCKED
   pending the downstream contract-application step. Do **not** modify TASK-133.
6. **Record the handoff.** State explicitly that the recorded decision is input
   to the subsequent contract-application task, and that TASK-137 itself applies
   the decision to no authoritative document.
7. **Record the ADR determination as a decision, not as an act.** Whether an ADR
   amendment or a new ADR is needed is recorded as part of the downstream
   determination. Authoring that ADR is the downstream task's act, not this one.
8. **Record the cap determination.** State explicitly whether the decision
   authors an ATK range/cap or leaves the current no-cap state
   (`COMBAT_RULES.md` §5.6.1 item 5) in force. Do not invent a cap.

### Out of Scope

- **Implementing anything** — no Relic runtime, no TASK-133 execution, no
  composition code, no Damage Pipeline change. `src/` and `tests/` are untouched.
- **Applying the decision to any authoritative document.** `docs/` is untouched
  by this task — including `COMBAT_RULES.md` §5.4/§5.6/§5.6.6, `RELIC_RULES.md`,
  `GAME_STATE.md`, `BOSS_RULES.md`, `GAME_RULES.md`, `REDIS_STATE.md`,
  `SIGNALR_PROTOCOL.md`, and `docs/03-decisions/`. That is the subsequent
  contract-application task.
- **Reopening TASK-136's D1–D12.** The Relic ATK carrier, shape, lifetime,
  within-collection composition, rounding, refresh, removal, serialization, and
  projection remain authoritative and unmodified.
- **Reopening TASK-119's `BuffDebuff` rule.** `COMBAT_RULES.md` §5.4.1–§5.4.5
  are unchanged; the decision composes with §5.4 rather than redefining it,
  unless the Product Owner explicitly decides otherwise (which must then be
  recorded, not assumed).
- **Inventing a new ATK cap** — `COMBAT_RULES.md` §5.6.1 item 5; TASK-136 D5
  forbids it. Any cap requires its own explicit decision recorded here.
- **Reusing Crit, CardCost, or Boss-side ATK semantics by analogy** —
  `EffectiveCrit` (`§3.3` item 7) sums percentage points on a
  percentage-point stat; `EffectiveCardCost` (`CARD_RULES.md` §3.6) caps a
  cost reduction at 100% and truncates; `EffectiveBossATK` (`§5.5.1`) is the
  deliberately separate Boss-side convention. None is the answer, and §5.5.1
  and §5.6.6 both explicitly refuse the collapse.
- **Changing either provisioned magnitude or duration** — Berserker Core's `+5`
  (`RELIC_RULES.md` §8.5) and Root's `−30%`/2 Turns (`BOSS_RULES.md` §6.3.1
  item 3) are their owning documents'; this task changes neither.
- **Changing `PetState.ATK`'s base value or §1.1's MVP defaults**
- **Any new state member, collection, or representation** — the decision
  composes two existing representations; it does not add a third
- **Any new Redis key, SignalR event/method, database table, or migration**
- **Modifying TASK-131, TASK-132, TASK-133, or TASK-136** (explicitly
  prohibited), or any completed or superseded task — `TASK_LIFECYCLE.md` §3
- **Creating any further task** — the downstream contract-application task and
  any implementation task are identified but not created here
- **Reopening TASK-079, TASK-099, or TASK-102** — terminal `SUPERSEDED`
- **TASK-036's Discord credential decisions** — separate, independent blocker
- **Any item listed as OUT in `MVP_SCOPE.md` §2**, or anything not IN
  `MVP_SCOPE.md` §1

---

## Dependencies

```text
TASK-136   DONE    the Relic Battle-lifetime ATK contract. This task resolves
                   only the one interaction TASK-136 D5/D11 left open and its
                   Note N5 recorded.
TASK-119   DONE    the BuffDebuff TargetStat = "ATK" consumption contract
                   (COMBAT_RULES.md §5.4). Not reopened.
TASK-128   DONE    the Boss-side ATK modifier direction contract
                   (COMBAT_RULES.md §5.5) — EVIDENCE ONLY, and its Boss-side
                   answer must NOT be copied
TASK-094   DONE    the Status Effect duration-consumption rule
TASK-095   DONE    the Status Effect domain state and step 19a lifecycle
```

---

## Blocks

```text
TASK-133 — Implement Server-Authoritative Relic Trigger and Effect Resolution

TASK-133 cannot implement Berserker Core's ATK effect until this contract is
resolved and applied to authoritative documentation.

TASK-133 is otherwise contract-complete for ATK: the carrier, shape, lifetime,
within-collection composition, rounding, refresh, removal, serialization, and
projection are all authored. This one interaction is all that remains.

TASK-133 must NOT be modified by this task.
```

---

## Evidence — the two formulas and their incompatibility

Recorded as evidence for the decision-maker. **This task does not resolve it.**

```text
COMBAT_RULES.md §5.4.1 (BuffDebuff, Turn-based)
    EffectiveATK = truncate( PetState.ATK × (100 − |Magnitude|) / 100 )
    · ABSOLUTE value of Magnitude
    · reduction-only: the result can never exceed PetState.ATK
    · consumed when the Damage Pipeline call for that attack is constructed
    · §5.4.5 scope: "a Turn-based BuffDebuff instance with TargetStat = ATK"

COMBAT_RULES.md §5.6.1 (Relic, Battle lifetime)
    EffectivePetATK = truncate( PetState.ATK × (100 + TotalATKModifierPercentage) / 100 )
    · SIGNED ATKModifierPercentage values, summed within the collection
    · increase or decrease; the result may exceed PetState.ATK
    · same consumption point: the Step-1 `Attack` argument
    · §5.6.5 scope: the active Pet's ATKModifiers[] entries

Both name the SAME input:
    "Player → Boss Damage Pipeline Step 1 — the `Attack` argument the call
     receives"   (§5.4.1 item 1 and §5.6.1 item 1, near-identical wording)
```

Why the two cannot be silently merged:

```text
1. §5.4.5    a source it does not define "is NOT silently treated as an ATK
             modifier" — so §5.4 has no documented default for a Relic entry
2. §5.6.5    §5.6 "Does NOT change … §5.4's BuffDebuff rule"
3. §5.5.1    the repository refused to collapse two SIMILAR conventions (the
             Boss-side signed form vs the Pet-side absolute form) into one
             shared formula; it authorizes no cross-rule collapse
4. §5.6.6    the same tension is already recorded as UNRESOLVED for exactly
             this pair, and states that a "no analogy" position holds
5. §3.3.7    EffectiveCrit sums percentage points on a percentage-point stat —
             a different composition shape from two producers of one
             multiplicative Step-1 input
```

---

## Implementation Notes

- **This task's single prohibited action is inventing the answer.** Every item in
  "Required Decision Coverage" is a Product-Owner or human decision. If no answer
  is supplied, STOP per `AGENTS.md` §7 — do not choose a coexistence rule, an
  operator, an order, a rounding point, or a cap.
- **Implementation convenience must not determine the decision.** No D1–D7
  option may be selected because §5.4.1 is already implemented, because §5.6.1
  is newer or was written second, because a Relic or a Boss effect "should" win,
  because one operator is fewer arithmetic steps, or because document order
  suggests it.
- **Present the options neutrally.** D1's four categories, D2's four operators,
  and D3–D7's choices are stated as candidates for the decision-maker, not as a
  shortlist with a recommended answer. Do not order them by ease of
  implementation, and do not editorialize.
- **Do not treat arithmetic coincidence as evidence.** At the provisioned
  magnitudes (`ATK` 50, `+5`, `−30`), the two sequential orders both yield 36.
  That is a property of these numbers, not a rule, and it must not be recorded
  as a determination. The worked divergences in "Problem / Ambiguity" are
  illustrations of divergence, not options.
- **`COMBAT_RULES.md` §5.4.5's closing rule is a live constraint.** It states
  that a source for which the document defines no consumption rule "is **not**
  silently treated as an ATK modifier". D1 and D2 must therefore either cite a
  document that genuinely determines the behavior or require an explicit
  Product-Owner decision.
- **`COMBAT_RULES.md` §5.6.6 is the recorded gap, not a hidden one.** It already
  states the three undetermined sub-questions (coexistence, composition, order)
  and instructs an implementation to STOP. This task is the resolution it
  anticipated.
- **`EffectiveCrit`, `EffectiveCardCost`, and the Boss-side `EffectiveBossATK`
  are precedent/evidence only, not the answer.** §5.6.6 and §5.5.1 already
  refuse the analogies in terms; D2 must not import a sum, a 100% cap, or a
  signed-Boss convention without an explicit Product-Owner decision.
- **No ATK cap may be invented.** `COMBAT_RULES.md` §5.6.1 item 5 records that
  `ATK` has no documented valid range; `Power` and `Crit` do. D5's cap note
  requires the Product Owner to either leave the no-cap state in force or
  explicitly decide to author a range — the latter as a recorded decision, not
  an assumption.
- **The two lifecycles genuinely differ and D6 must not paper over it.** The
  Relic modifier ends by source removal or battle end
  (`GAME_STATE.md` §5.1.4 item 4); the `BuffDebuff` modifier expires on a Turn
  count via `COMBAT_RULES.md` §5.3 consumed at `GAME_RULES.md` §17 step 19a.
  Authoring a unified expiry would be inventing a new duration model.
- **Do NOT apply the answer to any document.** The canonical owner documents
  (`COMBAT_RULES.md` §5.6.6 and any §5.4 cross-reference, `GAME_STATE.md` if a
  state consequence exists, and `RELIC_RULES.md` §2.4 item 6 / §8.5 item 4's
  cross-reference) are updated by the **subsequent** contract-application task —
  not here.
- **No gameplay expansion.** This task records a composition decision. It
  introduces no new Relic, Boss, Trigger, Condition, `effectType`, Status Effect
  type, magnitude, Redis key, SignalR event, or table.
- **Do not modify TASK-131, TASK-132, TASK-133, or TASK-136.** All four are
  explicitly protected and must be left byte-identical.
- **Do not create another task.** The downstream contract-application task and
  any implementation task are identified and handed off, not created here.
- **This task is NOT a broad audit.** Its scope is exactly the one interaction
  named in its title. Do not sweep other Relic effects, other Status Effect
  types, the Boss-side rule, or unrelated gameplay.
- Do not modify `tasks/completed/`.

---

## Acceptance Criteria

- [x] The documented two-producer Step-1 composition gap is clearly stated.
- [x] Both formulas are presented as evidence with their exact scoping
      statements (§5.4.5, §5.6.5) and their absolute-vs-signed incompatibility.
- [x] The concrete MVP case (Berserker Core `+5%` Battle; Mộc Yêu Root `−30%`
      Pet ATK, 2 Turns) is used explicitly and its reachability is stated.
- [x] D1–D7 are presented as explicit Product Owner/human decisions.
- [x] D1's coexistence categories are presented neutrally with no preselection.
- [x] D2's composition operators are presented neutrally with no preselection.
- [x] No D1–D7 option is selected by the executing agent.
- [x] The task explicitly states that implementation convenience must not
      determine the decision.
- [x] The task explicitly states that `EffectiveCrit`, `EffectiveCardCost`, and
      the Boss-side `EffectiveBossATK` are evidence only and must not be reused
      by analogy.
- [x] The task explicitly states that no ATK cap may be invented, and that
      `COMBAT_RULES.md` §5.6.1 item 5's no-cap state stands unless the Product
      Owner explicitly decides otherwise.
- [x] The task explicitly records the decision as input for the downstream
      contract-application task.
- [x] The task explicitly states that TASK-133 cannot implement Berserker Core
      ATK until this contract is resolved.
- [x] TASK-137 does not modify authoritative documentation.
- [x] TASK-137 does not modify source code.
- [x] TASK-137 does not modify tests.
- [x] TASK-131 remains unchanged.
- [x] TASK-132 remains unchanged.
- [x] TASK-133 remains unchanged.
- [x] TASK-136 remains unchanged.
- [x] No gameplay rule is invented.
- [x] No new ATK cap or ATK range is invented.
- [x] No new state member, collection, or representation is invented.
- [x] No new composition operator, order, or rounding point is invented.
- [x] No new SignalR event or method is invented.
- [x] No new Redis key is invented.
- [x] No database migration is created.
- [x] The final recorded decision is complete enough for the downstream
      contract-application task to consume.
- [x] The TASK-136 and TASK-119 boundaries are recorded, with both left
      unmodified.
- [x] The TASK-133 blocking relationship is stated, and TASK-133 is left
      unmodified.
- [x] Zero files under `src/` or `tests/` are modified.
- [x] Zero gameplay values, formulas, or balance figures are invented.
- [x] Zero files under `docs/` are modified.
- [x] TASK-131, TASK-132, TASK-133, TASK-136, and all completed/superseded tasks
      are byte-identical.
- [x] Decision coverage audit passes.
- [x] Neutrality audit passes.
- [x] Evidence audit passes.
- [x] Boundary audit passes.
- [x] Protected-file audit passes.
- [x] Source/test audit confirms zero source and test files were modified.
- [x] Authoritative-doc audit confirms zero docs/ files were modified.
- [x] Quality review of the task artifact passes (task artifact review only; no implementation tests).
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[ ] src/backend/ (none — decision-input task; no code)
[ ] src/frontend/client/ (none)
[ ] tests/ (none)
[ ] docs/01-game-design/ (none — MUST NOT be modified by TASK-137)
[ ] docs/02-technical/ (none — MUST NOT be modified by TASK-137)
[ ] docs/03-decisions/ (none — MUST NOT be modified by TASK-137; the ADR
                          treatment is the downstream task's determination)
[x] tasks/backlog/TASK-137-resolve-relic-atk-modifier-and-buffdebuff-atk-modifier-composition-contract.md
        (this file — the only file TASK-137 may modify)
[ ] tasks/backlog/TASK-133, tasks/backlog/TASK-136 (MUST remain unmodified)
[ ] tasks/completed/ (MUST remain unmodified)
```

---

## Testing / Evidence Requirements

### Required Verification

This is a decision-input task. Implementation and documentation tests are
replaced by audit checks.

```text
[x] Decision coverage audit
    Confirm D1–D7 are explicitly presented.

[x] Neutrality audit
    Confirm no option is recommended or selected by the task.

[x] Evidence audit
    Confirm both formulas are quoted with their scope statements, and that the
    concrete MVP case (Berserker Core × Mộc Yêu Root) is used explicitly.

[x] Analogy-refusal audit
    Confirm the task states EffectiveCrit / EffectiveCardCost /
    EffectiveBossATK are evidence only and not reusable by analogy.

[x] Cap audit
    Confirm no ATK cap is invented and §5.6.1 item 5's no-cap state is recorded
    as standing.

[x] Boundary audit
    Confirm TASK-137 is decision-input only.

[x] Protected-file audit
    Confirm TASK-131, TASK-132, TASK-133, and TASK-136 remain untouched.

[x] Source/test audit
    Confirm zero source and test files are modified.

[x] Authoritative-doc audit
    Confirm zero docs/ files are modified by TASK-137.

[x] Gameplay implementation tests
    N/A — no code is implemented.

[x] Integration tests
    N/A — no runtime behavior is implemented.
```

Do not claim documentation consistency across a future decision that has not yet
been made.

### Key Edge Cases (for the downstream contract-application task)

Recorded here so the decision's consumer inherits them; they are **not** checks
this task performs against an implementation.

- The composed result must be integer-deterministic and platform-independent
  (`TDD.md` §6), given that both constituent rules already truncate
- The decision must state whether a sequential composition truncates once or
  twice, because the two orders can differ at some inputs even where they
  coincide at the provisioned magnitudes
- The two lifecycles differ (Turn-countdown expiry vs source-removal/battle-end)
  and the decision must state the composed value's behaviour when either source
  ends mid-battle, without authoring a new duration model
- The `BuffDebuff` instance's activity is read from the committed
  `StatusEffects[]` state at attack resolution (`§5.4.3`), while
  `ATKModifiers[]` entries persist for the battle; the decision must be
  consistent with both
- A `BuffDebuff` applied at `GAME_RULES.md` §17 step 18b (Boss Skill) cannot
  affect that same Turn's already-resolved attack at step 15 (`§5.4.3`'s worked
  example); the composition must not silently change that non-retroactivity
- The decision must not create a second representation of any value
  `GAME_STATE.md` §0 item 5 already covers, and must not mutate
  `PetState.ATK` (D7)
- If the decision has any storage, wire, or Redis consequence, that would
  contradict TASK-136 D9/D10's no-new-key/no-new-member determinations — a "no
  consequence" outcome is expected, but if not, it must be reported as a
  separate architectural concern rather than authored here
- Root's `−30%` is a `BuffDebuff` `Magnitude` (`BOSS_RULES.md` §6.3.1 item 3)
  and Berserker Core's `+5` is a structured Relic `value`
  (`RELIC_RULES.md` §8.5); the decision must not conflate the two units or
  introduce a conversion

---

## Stop Conditions

- **If an authoritative document already defines the composition: STOP and
  report the evidence** rather than creating a duplicate decision. (Verified at
  creation time: `COMBAT_RULES.md` §5.6.6 records it as UNRESOLVED;
  §5.4.5 and §5.6.5 each scope their own rule and disclaim the other;
  `RELIC_RULES.md` §2.4 item 6 and §8.5 item 4 cross-reference it as undecided;
  and no document states a coexistence rule, operator, or order for this pair.)
- **If the required Product-Owner decision is not supplied, including D1
  (coexistence): STOP per `AGENTS.md` §7.** Do not choose a coexistence rule, an
  operator, an order, a rounding point, or a cap. (All seven items were supplied;
  this condition did not fire.)
- **If coexistence remains ambiguous after D1: STOP.** (Resolved by D1; did not fire.)
- **If the composition operator remains ambiguous after D2: STOP.** (Resolved by D2; did not fire.)
- **If application order remains ambiguous after D3: STOP.** (Resolved by D3; did not fire.)
- **If the truncation point remains ambiguous after D4: STOP.** (Resolved by D4; did not fire.)
- **If the resulting effective-ATK semantics remain ambiguous after D5: STOP.** (Resolved by D5; did not fire.)
- **If removal/expiry interaction remains ambiguous after D6: STOP.** (Resolved by D6; did not fire.)
- **If base-stat independence is not explicitly confirmed by D7: STOP.** (Resolved by D7; did not fire.)
- **If resolving the question requires a new gameplay rule outside this decision
  boundary: STOP** and report it — do not author it.
- **If resolving it would require authoring an ATK range or cap: STOP** and
  record it as a separate explicit decision. Do not invent one.
- **If resolving it requires a new architectural decision that cannot be captured
  as a Product Owner gameplay decision: STOP** and report it.
- **If applying the decision to an authoritative document appears necessary to
  complete this task: STOP after recording the decision.** Do not perform that
  documentation update in TASK-137.
- **If a documentation conflict is found** between `COMBAT_RULES.md` §5.4 and
  §5.6, or between either and `RELIC_RULES.md`/`BOSS_RULES.md`/`GAME_STATE.md`:
  STOP and report per `AGENTS.md` §4 — do not silently pick a side
- **If the decision would require changing TASK-136's D1–D12 or TASK-119's
  `BuffDebuff` rule: STOP** — those are out of boundary; record the dependency
  instead
- **If a new network contract, a new Redis key, or PostgreSQL `BattleState`
  persistence would be required: STOP** — record it as a separate architectural
  concern
- If the decision would require **modifying a completed task**: STOP — completed
  tasks are immutable (`TASK_LIFECYCLE.md` §3)
- If the decision would require **modifying TASK-133**: STOP — it is protected
  by this task's instructions; record the dependency instead
- If **implementation becomes necessary to determine the contract**: STOP — that
  inverts the required order (`AGENTS.md` §5, §18)
- If the decision would place ATK composition on the client: STOP per
  `AGENTS.md` §10
- If the task drifts into implementing the composition, the resolver, or
  applying the decision to documentation: STOP — those are separate tasks
- If the task is asked to create another task: STOP — the downstream work is
  identified and handed off, not created here
- If the task is asked to reopen TASK-079, TASK-099, or TASK-102: STOP —
  terminal `SUPERSEDED`
- If the task is asked to resolve TASK-036's Discord credential decisions: STOP —
  separate, independent blocker
- If task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: STOP & decompose

---

## Decision Input Protocol

TASK-137 cannot be completed from repository evidence alone.

The executing agent must consume an explicit Product Owner / human decision
provided as execution input.

If no explicit decision input is present at execution time:

1. Do not infer or recommend an answer.
2. Do not modify any file.
3. Leave D1–D7 as NOT DECIDED.
4. Report TASK-137 as BLOCKED.
5. Stop.

The executing agent must not manufacture the Product Owner decision from
existing documentation, implementation precedent, arithmetic convenience,
or architectural preference.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after the Product Owner decision is
  supplied and recorded.
  Keep concise and factual.
  DO NOT fill the decision fields with invented answers.
  If the Product Owner decision is still missing, the task remains BLOCKED and
  the fields remain explicitly unresolved.
-->

### Decision Recorded

The Product Owner supplied an explicit answer for all seven decision items.
Recorded verbatim in "Decision Record (D1–D7)" above. No value was derived,
inferred, computed, or reinterpreted, and no assumption was added.

Product Owner decision:

```text
D1 — Coexistence:
The Relic Battle-lifetime ATK modifier and the Turn-based BuffDebuff
ATK modifier coexist and both affect EffectivePetATK.

D2 — Composition operator:
The modifiers are composed as a single signed percentage adjustment
against the permanent Base Pet ATK.

D3 — Composition order:
Composition is order-independent. Do not apply Relic and BuffDebuff
modifiers sequentially in an order-dependent pipeline.

D4 — Truncation / rounding:
Apply the combined percentage to Base Pet ATK and truncate toward zero
exactly once when producing the integer EffectivePetATK.

D5 — EffectivePetATK semantics:
EffectivePetATK is:

truncate(
    PetState.ATK
    × (100 + TotalATKModifierPercentage)
    / 100
)

where TotalATKModifierPercentage is the signed sum of all applicable
Pet ATK modifiers from both:
- PetState.ATKModifiers[]
- applicable Turn-based BuffDebuff ATK StatusEffects[]

No new ATK cap is introduced by this decision.

D6 — Removal / expiry interaction:
The two modifier carriers retain independent ownership and lifetime.

When Mộc Yêu Root expires, only its StatusEffects[] entry is removed.
The Berserker Core ATKModifiers[] entry remains active for the rest
of the Battle.

A modifier is never removed merely because another modifier expires.

D7 — Base PetState.ATK independence:
PetState.ATK remains the permanent/base Pet ATK value.

Neither Relic ATK modifiers nor BuffDebuff ATK modifiers may overwrite,
mutate, or reset PetState.ATK.

No DefaultATK-style runtime reset mechanism is introduced.
EffectivePetATK remains derived state.
```

### Documentation

No authoritative documentation was modified by TASK-137.

Verified: zero files under `docs/` were modified by this execution. The documentation updates belong to the subsequent contract-application task.

### Handoff

The recorded decision is input to the subsequent contract-application task.

TASK-137 does not itself apply the decision to `COMBAT_RULES.md` §5.6.6 or any
other canonical owner document.

```text
What the downstream task consumes from this record:

  D1, D2, D3, D4, D5   the composed Step-1 rule → COMBAT_RULES.md §5.6.6 (the
                       section that currently records the gap), with §5.4.5
                       and §5.6.5 reconciled and RELIC_RULES.md §2.4 item 6 /
                       §8.5 item 4's cross-references updated
  D6                   the two lifecycles' interaction → COMBAT_RULES.md, with
                       GAME_STATE.md §5.1.1/§5.1.4 referenced and not restated
  D7                   the base-stat independence statement
  the cap determination  COMBAT_RULES.md §5.6.1 item 5 and §1.1 confirmation
                       that no new ATK cap is introduced
```

### Changed Files

```text
tasks/backlog/TASK-137-resolve-relic-atk-modifier-and-buffdebuff-atk-modifier-composition-contract.md
```

### Validation Results

```text
Decision coverage audit: PASS (all D1–D7 items explicitly resolved)
Neutrality audit: PASS (approved Product Owner decision recorded verbatim)
Evidence audit: PASS (authoritative references verified)
Analogy-refusal audit: PASS (no unprincipled analogy used)
Cap audit: PASS (confirmed no new ATK cap introduced)
Boundary audit: PASS (TASK-137 remains decision-input only)
Protected-file audit: PASS (TASK-131, TASK-132, TASK-133, TASK-136, TASK-119 untouched)
Source/test audit: PASS (zero files under src/ or tests/ modified)
Authoritative-doc audit: PASS (zero files under docs/ modified)
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed zero files modified under `src/` or `tests/`
- [x] Confirmed zero files modified under `docs/`
- [x] Confirmed TASK-131, TASK-132, TASK-133, TASK-136, and `tasks/completed/`
      unmodified
- [x] Confirmed no ATK cap or range was invented
- [x] Confirmed no new state member, composition operator, order, or rounding
      point was invented
- [x] Confirmed no new Redis key, SignalR event, or table was introduced

### Follow-Up Work Identified (not created)

- **Subsequent contract-application task** — consumes this record and applies the
  D1–D7 decision to `COMBAT_RULES.md` §5.6.6 and the cross-referencing
  documents. This is the step that makes TASK-133 implementation-ready.
  **Not** created by TASK-137.
- **TASK-133 unblocking** — follows the downstream contract-application step;
  TASK-133 itself is not modified here and remains blocked until then.
