# TASK-138 — Resolve the Relationship Between §5.4.1's `BuffDebuff` ATK Formula and §5.6.6's Combined Pet ATK Composition

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
    Record the decision in TASK-138
            ↓
    STOP

  THIS TASK MUST NOT APPLY THE DECISION TO AUTHORITATIVE DOCUMENTATION.
  A subsequent contract-application task consumes the recorded decision and
  updates the canonical documents. TASK-138's deliverable is the RECORD.

  THIS TASK DECIDES NOTHING BY ITSELF AND IMPLEMENTS NO BEHAVIOR. It presents
  the documented evidence of a genuine two-formula relationship gap and requires
  the appropriate human/Product-Owner decision. Inventing an answer — which
  formula governs a BuffDebuff-only attack, whether §5.6.6 is the universal
  composition model, how a reduction magnitude maps to a signed contribution, or
  where truncation occurs — is the single prohibited action of this task
  (AGENTS.md §7, §20).

  DECISION OWNERSHIP: D1–D6 are NOT implementation decisions for the executing
  agent. No option may be selected because the current code already does it,
  because one formula is simpler for the Damage Pipeline, because one is already
  implemented in StatusEffectLifecycle.EffectiveAttack, because §5.6.6 was
  written later, because §5.4.1 came first in the document, because TASK-128's
  Boss-side rule uses the signed form, or because TASK-137 alone appears to
  settle it. The current implementation, document order, arithmetic convenience,
  and the Boss-side convention are EVIDENCE ONLY — they are NOT the answer.

  PROVENANCE: identified by the TASK-133 implementation agent, which STOPPED
  before writing any source code rather than choosing an answer. TASK-137
  resolved the Relic × BuffDebuff COEXISTENCE case and authored §5.6.6, but did
  NOT rewrite §5.4.1 — and §5.4.1 still independently authors a reduction-only
  absolute-value formula for the same Damage Pipeline Step-1 `Attack` input.
  Two authoritative statements now describe that one input under different
  arithmetic, and no document determines which governs when only a BuffDebuff is
  active, or whether §5.6.6 has superseded §5.4.1 as the composition model.

  THIS IS A STOP-DRIVEN TASK, NOT A FEATURE. TASK-133's own Stop Conditions fire
  on exactly this (its line 292: "If any trigger, condition, effect, magnitude,
  or lifetime needed is not fully determined by RELIC_RULES.md §3–§8 and
  ADR-018: STOP per AGENTS.md §7 — invent nothing"). TASK-133 remains BLOCKED on
  this point; TASK-133 is NOT modified by this task.

  BOUNDARY: decision recording only. Zero files under docs/, src/, or tests/.
  This task creates no ADR, changes no gameplay rule, adds no Relic, Boss, or
  combat behavior, introduces no ATK cap, and does not touch TASK-119, TASK-128,
  TASK-131, TASK-132, TASK-133, TASK-134, TASK-136, or TASK-137.
-->

---

## Metadata

```text
Task ID:           TASK-138
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md). This task is the
                   DECISION-INPUT half of that workflow only: it obtains and
                   records the Product Owner decision. It does NOT perform the
                   canonical-owner documentation write that the workflow's
                   later steps describe — that is the subsequent
                   contract-application task's act, consuming this record.
Status:            DONE (the Product Owner supplied an explicit answer for all
                   six decision items D1–D6. The decision was recorded verbatim
                   in "Decision Record (D1–D6)" without reinterpretation or
                   added assumption. TASK-138 modified no file other than this
                   one: zero `docs/`, zero `src/`, zero `tests/`, and
                   TASK-119/128/133/136/137 are byte-identical. The canonical
                   documentation reconciliation is the SUBSEQUENT
                   contract-application task's act, consuming this record —
                   TASK-138 does not perform it.)
                   Lifecycle reconciliation: recorded DONE while the file remained
                   in tasks/backlog/; moved to tasks/completed/ per
                   TASK_LIFECYCLE.md §3 (direct execution verified: D1–D6
                   recorded, all thirty-four acceptance criteria satisfied).
Risk:              HIGH (TASK_TYPES.md §4 — DOCUMENTATION baseline LOW–MEDIUM,
                   raised because the decision determines the Damage Pipeline's
                   Step-1 `Attack` input for the active Pet in the ordinary
                   single-debuff case, which is more reachable than TASK-137's
                   coexistence case. It is the same class of combat-arithmetic
                   gameplay decision TASK-119, TASK-128, TASK-136, and TASK-137
                   each recorded. No `docs/` file is modified by this task.)
Priority:          HIGH (a live blocker on TASK-133, which is itself CRITICAL and
                   the last unimplemented step of GAME_RULES.md §17.)
Primary Agent:     review (TASK_TYPES.md §2 / AGENT_SELECTION.md §1 —
                   "Documentation change: Primary Agent Review". No domain
                   agent may author the answer; the Product Owner supplies it.)
Supporting Agents: gameplay (COMBAT_RULES.md §5.4 and §5.6 are the owning domain
                   sections — consulted to CONFIRM the two formulas and the
                   scope statements the decision must reconcile, not to author
                   the answer; BOSS_RULES.md §6.3.1 item 3 owns Root's magnitude
                   and duration),
                   backend (GAME_STATE.md §2.3.1 owns the StatusEffects[]
                   instance and §5.1.1 its lifecycle; consulted to confirm that
                   the stored Magnitude is the positive value and that no state
                   change follows from either candidate)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-137 (DONE — the Relic × BuffDebuff coexistence decision
                   and the §5.6.6 composition contract; this task resolves only
                   the RELATIONSHIP between §5.6.6 and the earlier §5.4.1, and
                   does not reopen TASK-137's D1–D7),
                   TASK-119 (DONE — the §5.4 BuffDebuff TargetStat = "ATK"
                   consumption contract this task must reconcile with §5.6.6),
                   TASK-136 (DONE — the Relic Battle-lifetime ATK contract that
                   authored §5.6.1; not reopened),
                   TASK-133 (BLOCKED — the implementation task whose STOP
                   surfaced this gap)
Blocks:            TASK-133 — Implement Server-Authoritative Relic Trigger and
                   Effect Resolution (its ATK composition arithmetic cannot be
                   implemented deterministically until this is resolved)
Estimate:          Simple (one recorded formula-relationship decision, D1–D6;
                   zero code, zero documentation edits, zero gameplay rules)
```

---

## Objective

Obtain and record, **in this task only**, the explicit Product-Owner / human
decision defining **how `COMBAT_RULES.md` §5.4.1's `BuffDebuff` ATK formula
relates to §5.6.6's combined Relic + `BuffDebuff` composition contract** — whether
a `BuffDebuff`-only attack still uses §5.4.1's reduction-only absolute-value
arithmetic or is now represented as a signed modifier under the unified §5.6.6
model, whether §5.6.6 is scoped to the coexistence case or is the canonical
composition model for all applicable Pet ATK modifiers, how a documented
reduction magnitude maps into a signed total, whether one unified calculation
path or two paths remain, and where truncation occurs — so that a subsequent
contract-application task can reconcile the canonical documents and make
TASK-133 implementation-ready.

This task records the decision. It implements no composition, no formula, and no
resolver, and it **applies the decision to no authoritative document**.

---

## Current State

```text
§5.4 BuffDebuff ATK contract (TASK-119) — authored, NOT rewritten by TASK-137:
  COMBAT_RULES.md §5.4      the canonical owner of how a Turn-based Buff/Debuff
                            Status Effect's Magnitude reaches the stat its
                            TargetStat names
  COMBAT_RULES.md §5.4.1    EffectiveATK =
                            truncate( PetState.ATK × (100 − |Magnitude|) / 100 )
                            item 1  the SAME Player → Boss Damage Pipeline
                                    Step-1 `Attack` input §5.6.1 names
                            item 2  the percentage applies to PetState.ATK ALONE
                            item 3  reduction-only; the ABSOLUTE value
  COMBAT_RULES.md §5.4.2    truncated toward zero; worked integer table
                            (ATK 50 → 35, 51 → 35, 99 → 69, 100 → 70, 101 → 70)
  COMBAT_RULES.md §5.4.3    active per the committed StatusEffects[] instance
                            state at attack resolution; duration per §5.3
  COMBAT_RULES.md §5.4.4    base stat never overwritten; EffectiveATK derived,
                            NOT stored
  COMBAT_RULES.md §5.4.5    SCOPE: "Applies to a Turn-based BuffDebuff instance
                            with TargetStat = ATK" — and nothing else

§5.6 Relic ATK + combined composition contract (TASK-136, TASK-137):
  COMBAT_RULES.md §5.6.1    EffectivePetATK =
                            truncate( PetState.ATK × (100 + TotalATKModifierPercentage) / 100 )
                            Total = Σ ATKModifierPercentage (signed, additive)
                            item 4  signed: supports increase AND decrease
                            item 5  NO ATK cap is authored
  COMBAT_RULES.md §5.6.2    multiple sources summed; each contributes
                            independently; sequential application is INCORRECT
  COMBAT_RULES.md §5.6.4    base stat never overwritten; no restore step
  COMBAT_RULES.md §5.6.5    §5.6 is a SEPARATE RULE, not an extension of §5.4;
                            §5.4 remains scoped by §5.4.5
  COMBAT_RULES.md §5.6.6    RESOLVED (TASK-137 D1–D7) — the coexistence case:
                            Relic and BuffDebuff modifiers COEXIST, compose as a
                            SINGLE SIGNED percentage adjustment, are
                            ORDER-INDEPENDENT, apply the combined percentage to
                            Base Pet ATK, and truncate toward zero EXACTLY ONCE.
                            It states the |Magnitude| convention "is not used
                            for the combined modifier".

The two producers of ONE input, now under two arithmetics:
  COMBAT_RULES.md §5.4.1 item 1 and §5.6.1 item 1 each name the SAME
  Player → Boss Damage Pipeline Step-1 `Attack` argument. §5.6.6 additionally
  states that the absolute-value convention is not used for the COMBINED
  modifier. No document states which arithmetic governs when ONLY a BuffDebuff
  ATK modifier is active.

The implementation state (EVIDENCE ONLY — not the answer):
  src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs
      EffectiveAttack(attack, effects)  applies each active
      TargetStat = "ATK" BuffDebuff in Id-ordinal order as a SEPARATE
      sequential multiplier using (int)Math.Abs(effect.Magnitude) with an
      intermediate integer truncation per step. Its own XML documentation
      asserts "§5.4 authors no stacking rule beyond applying each active
      instance's magnitude in turn".
  src/backend/GameServer.Application/Battle/BattleStateService.cs
      calls EffectiveAttack(resolved.PetState.ATK,
      resolved.PetState.ActiveStatusEffects) as the Step-1 `Attack` argument.
  This is §5.4.1's arithmetic, applied literally. It cannot represent a Relic
  contribution because PetState.ATKModifiers[] does not exist in code at all
  (a separate, independent TASK-133 blocker, out of this task's scope).

Documented prohibitions the answer must respect:
  COMBAT_RULES.md §5.4.4 / §5.6.4   base stat never overwritten; no restore step
  COMBAT_RULES.md §5.6.1 item 5     no ATK cap is authored
  GAME_STATE.md §0 item 5           no second representation of a value
  GAME_STATE.md §2.3.1 item 2       the stored Magnitude is the applied value,
                                    handed off uninterpreted
```

---

## Problem / Ambiguity

`COMBAT_RULES.md` §5.4.1 and §5.6.6 **each author a complete formula that
produces the same Damage Pipeline Step-1 `Attack` input for the same Pet
attack**, using two incompatible arithmetic conventions:

```text
§5.4.1 — BuffDebuff (Turn-based), authored by TASK-119, unchanged by TASK-137
    EffectiveATK = truncate( PetState.ATK × (100 − |Magnitude|) / 100 )
                   ABSOLUTE value; reduction-only; the result can never exceed
                   PetState.ATK; truncation per applied instance
    §5.4.5 scope: "a Turn-based BuffDebuff instance with TargetStat = 'ATK'"

§5.6.6 — Relic + BuffDebuff combined (RESOLVED, TASK-137 D1–D7)
    TotalATKModifierPercentage = Σ (all applicable SIGNED ATK modifiers)
    EffectivePetATK = truncate( PetState.ATK × (100 + Total) / 100 )
                   SIGNED sum; order-independent; applied ONCE; truncated ONCE
                   §5.6.6 states |Magnitude| "is not used for the combined
                   modifier"
```

§5.6.6 explicitly resolved the **coexistence** case (both a Relic and a
`BuffDebuff` modifier live on the same attack). It did **not** state what happens
when only a `BuffDebuff` modifier is active, and it did **not** rewrite §5.4.1 —
whose formula remains on the books, unamended, describing that same input.

So for the ordinary single-debuff attack the repository now holds two
authoritative formulas and no rule choosing between them. Both readings are
consistent with the documents as written:

```text
Reading A — §5.4.1 still governs the BuffDebuff-only case
    §5.6.6 is scoped to the coexistence case it resolved; §5.4.1 is unchanged
    and continues to author the reduction-only absolute-value formula whenever
    no Relic ATK modifier is present. Two calculation paths exist.

Reading B — §5.6.6 is the canonical composition model for all Pet ATK modifiers
    §5.4.1's absolute-value convention is superseded for ATK composition
    generally; a BuffDebuff's reduction contributes a negative signed
    percentage point and one unified composition path serves every case.
```

This is a genuine gap, not an arithmetic detail:

1. **It is a gameplay rule.** Which arithmetic governs is a balance and design
   decision, and the repository has recorded four separate Product-Owner
   decisions in this exact area already — TASK-119 (§5.4's Pet-side rule),
   TASK-128 (§5.5's Boss-side direction), TASK-136 (§5.6's Relic rule),
   TASK-137 (§5.6.6's coexistence composition).
2. **The two conventions are incompatible by construction.** §5.4.1 takes the
   absolute value and can only reduce; §5.6.6 takes the signed value and can
   raise or lower. §5.5.1 shows the repository explicitly refusing to collapse
   even two *similar* conventions into one shared formula.
3. **The reachability is broader than TASK-137's case.** Root is a provisioned
   Boss Skill effect applied to the active Pet in an ordinary Mộc Yêu battle
   with **no** Relic equipped. A single-debuff attack does not depend on any
   Relic being present, so this ambiguity is reachable more often than the
   coexistence case TASK-137 resolved.
4. **TASK-133 cannot proceed deterministically without it.** The implementation
   must produce one integer for that Step-1 input, and the two readings produce
   different integers at the task's own acceptance values (see Evidence).
5. **The current implementation is not a tie-breaker.** It implements §5.4.1
   literally, but `AGENTS.md` §17 makes code-versus-documentation agreement a
   thing to establish by deciding which was correct — not by precedence. The
   code is evidence of what §5.4.1 says, not of which formula is intended.

Inventing the answer is the prohibited action (`AGENTS.md` §7, §20).

---

## Authoritative References

- `docs/01-game-design/COMBAT_RULES.md` **§5.4** (the Turn-based `BuffDebuff` consumption rule — this task's earlier formula): **§5.4.1** (the consumption point, the `EffectiveATK` formula, item 1 naming the SAME Step-1 `Attack` input, item 2's "PetState.ATK ALONE", item 3's reduction-only absolute value), **§5.4.2** (rounding, truncated toward zero, and its worked integer table), §5.4.3 (activity at attack resolution; the same-Turn non-retroactivity example), **§5.4.4** (the base stat is never overwritten; `EffectiveATK` is derived and not stored), **§5.4.5** (the scope statement fixing `BuffDebuff`-only applicability, and its closing "not silently treated as an ATK modifier" rule)
- `docs/01-game-design/COMBAT_RULES.md` **§5.6** (the Relic Battle-lifetime ATK composition and now the combined contract — this task's later formula): §5.6.1 (the composition, its formula, signed percentage-point semantics, truncation toward zero, **item 5's "no ATK cap is authored"**), §5.6.2 (additivity and the "applying each modifier in sequence is incorrect" statement), §5.6.3 (lifetime, refresh, removal), §5.6.4 (base-stat independence), **§5.6.5 (the "separate rule" scope statement that keeps §5.4 scoped by §5.4.5)**, **§5.6.6 (the resolved coexistence contract, and its statement that `|Magnitude|` "is not used for the combined modifier")**
- `docs/01-game-design/COMBAT_RULES.md` **§5.5 / §5.5.1–§5.5.5** (the Boss-side counterpart — **EVIDENCE ONLY, NOT the answer**): §5.5.1's signed-magnitude formula and its statement that the Pet-side and Boss-side conventions "must **not** be collapsed into one shared formula"; §5.5.3's scope. The Boss-side signed form must not be imported by analogy.
- `docs/01-game-design/COMBAT_RULES.md` **§5.1** (the MVP Status Effect type list), **§5.2** item 1 (the duration-or-trigger expiry dichotomy) and item 2 (refresh duration, do not stack magnitude), **§5.3 / §5.3.1–§5.3.4** (the canonical duration-consumption rule), **§3 / §3.1** (the Damage Pipeline's fixed six-step order and Step 1's contributions), **§3.3 item 7** (`EffectiveCrit` — the contrast case), **§1.1** (the MVP stat defaults: `ATK` = 50; and the explicit statement that `ATK` has **no** documented range while `Power` and `Crit` do)
- `docs/01-game-design/BOSS_RULES.md` **§6.3** (the provisioned Boss Skill table — Mộc Yêu Root: `-30% Pet ATK debuff for 2 Turns`) and **§6.3.1 item 3** (Root's magnitude, its percentage representation, and its Turn-based duration deferral to `COMBAT_RULES.md` §5.3), §6.3.1 item 2 (Thủy Ma's flat, instant Power reduction — the contrast case)
- `docs/01-game-design/GAME_RULES.md` **§17** (the fixed resolution order: step 11 Relics, step 15 the Pet's attack, step 18b Boss Skill effects, step 19a Status Effect duration), §14 (Combat Rules), §18 (server authority), **§20 (Rule Change Policy — the mechanism a composition decision is recorded under)**, §1.4 (the battle-end boundary)
- `docs/01-game-design/RELIC_RULES.md` §8.3 (`ATK | Pet | Battle | Percentage`) and §8.5 item 4 (Berserker Core's runtime carrier, lifecycle, and composition references) — evidence of the Relic side only, not the answer
- `docs/02-technical/GAME_STATE.md` **§2.3.1** (the `StatusEffect` instance schema — `id`/`type`/`source`/`magnitude`/`targetStat`/`remainingTurns`/`expiryCondition`, item 2's "`Magnitude`-is-handed-off-not-interpreted" rule naming `COMBAT_RULES.md` §5.4, item 6's one-instance-per-identity rule, item 7's `TargetStat`-iff-`BuffDebuff` pairing), **§2.3.2** (serialization), **§5.1.1** (the step 19a Status Effect lifecycle), **§0 items 4–5** (a member is added by its owning decision; no second representation)
- `docs/02-technical/GAME_STATE.md` §2.3 (the `PetState.ATK` annotation), §5.1 (the single post-resolution write-back)
- `docs/02-technical/TDD.md` §6 (determinism — any composition must be integer-deterministic)
- `docs/02-technical/ARCHITECTURE.md` §2.1 (Domain purity), §5 (anti-overengineering)
- `docs/03-decisions/README.md` §2 (ADR criteria), §5 (status values)
- `tasks/backlog/TASK-137-resolve-relic-atk-modifier-and-buffdebuff-atk-modifier-composition-contract.md` — the coexistence decision this task does **not** reopen; its D1–D7 are accepted and its §5.6.6 record is the later formula
- `tasks/completed/TASK-119-resolve-root-atk-modifier-consumption-contract.md` — the `BuffDebuff` Pet-side ATK consumption contract that authored §5.4; not reopened, but its formula's relationship to §5.6.6 is what this task must decide
- `tasks/backlog/TASK-136-resolve-battle-lifetime-pet-atk-modifier-runtime-contract.md` — the Relic ATK contract that authored §5.6.1; not reopened
- `tasks/completed/TASK-128-resolve-boss-side-atk-modifier-direction-contract.md` and `TASK-129-apply-boss-side-atk-modifier-direction-contract.md` — the closest **process** precedent: a narrowly scoped decision task for an ATK-modifier semantic, followed by its application task. **Do not copy its Boss-side answer.**
- `tasks/backlog/TASK-133-implement-server-authoritative-relic-trigger-and-effect-resolution.md` — the task this blocks; its Stop Conditions (line 292) are the STOP this task resolves
- `AGENTS.md` §4 (conflict resolution — report, do not silently resolve), §7 (invent no rule), §9 (anti-overengineering), §12 (domain boundaries), §16 (task discipline), §17 (documentation change — decide which of code or documentation was correct), §20 (stop conditions), §22
- `.ai/workflow/documentation/documentation-change.md` §2 (no duplication — one concept, one owner), §3 (determining the canonical owner)

---

## Decision Question

> **When a Turn-based `BuffDebuff` `TargetStat = "ATK"` modifier is the only Pet
> ATK modifier active (no Relic ATK modifier present), does the Damage Pipeline
> Step-1 `Attack` input still follow `COMBAT_RULES.md` §5.4.1's reduction-only
> absolute-value formula — or is it produced by `COMBAT_RULES.md` §5.6.6's
> unified signed composition, with §5.4.1's absolute-value convention no longer
> governing ATK composition?**

The task must determine the authoritative relationship between the two formulas:

```text
which formula governs the BuffDebuff-only case
the scope of §5.6.6 (coexistence-only, or the canonical composition model)
how a documented reduction magnitude maps into a signed total
whether one unified calculation path or two distinct paths remain
where truncation occurs in each case
base PetState.ATK independence under either reading
```

None of these is to be implemented.

---

## Required Decision Coverage

The executing agent must obtain explicit answers to all of the following. **An
unanswered item is a blocking stop condition, not an invitation to choose.**
Every item below carries the status `NOT DECIDED` until the Product Owner
answers it.

```text
D1 — BuffDebuff-only behavior:
     When only a Turn-based BuffDebuff TargetStat = "ATK" modifier is active —
     no Relic ATK modifier present — which formula governs the Step-1 `Attack`
     input?
     The candidate categories must be presented NEUTRALLY. No option is
     recommended, ordered by ease, or eliminated by this task:
       A. §5.4.1 continues to govern: the reduction-only absolute-value formula
          applies, unchanged for this case
       B. §5.6.6 governs: the BuffDebuff participates as a signed modifier in
          the unified composition
       C. Another explicitly defined rule
     Selection must NOT be made because the current implementation already does
     A, because A is simpler for the Damage Pipeline, because §5.4.1 appears
     first in the document, or because B seems implied by TASK-137.   NOT DECIDED

D2 — Unified composition boundary:
     Is §5.6.6 scoped to the coexistence case only — applying when a Relic ATK
     modifier AND a BuffDebuff ATK modifier are both live — or is it the
     canonical composition model for ALL applicable Pet ATK modifiers,
     including the BuffDebuff-only case?
     The decision must state the scope explicitly. If §5.6.6 is coexistence-only,
     the decision must state which rule governs each remaining case. If §5.6.6
     is universal, the decision must state whether §5.4.1 is superseded,
     narrowed, or retained as a special case, and how the two documents are to
     be reconciled so that only one authors the ATK composition.
     Do NOT infer the answer from §5.6.6's existence or from TASK-137's
     silence on the BuffDebuff-only case.                            NOT DECIDED

D3 — BuffDebuff sign semantics:
     If the BuffDebuff participates in the unified signed composition (D1 option
     B, or D2 universal), the decision must define how its DOCUMENTED reduction
     magnitude maps into the signed total.
     Root is documented as "-30% Pet ATK" (BOSS_RULES.md §6.3.1 item 3) while
     GAME_STATE.md §2.3.1 item 2 stores Magnitude as the applied value handed
     off uninterpreted (the provisioned stored value is the positive 30 — the
     sign is carried by the rule's prose, not by the stored member).
     The decision must state, explicitly and for the contract:
       whether the contribution is -30 signed percentage points
       or -|Magnitude| derived from the stored value
       or another explicitly stated mapping
       and whether the mapping is symmetric (a BuffDebuff that documents an
       INCREASE — none is provisioned — would contribute a positive value)
     This must be an explicit contract decision, NOT an implementation
     inference from the stored representation.                       NOT DECIDED

D4 — Calculation boundary:
     Does a single unified composition produce EffectivePetATK for every case,
     or does a distinct BuffDebuff-only calculation path remain?
     The decision must state the number and identity of the calculation paths
     that produce the Step-1 `Attack` input, and must satisfy GAME_STATE.md
     §0 item 5 (no second representation of a value) and TDD.md §6
     (integer-determinism — the same input state yields the same integer on
     every platform and in every evaluation order).
     Do NOT default to "two paths" merely because two sections exist today, and
     do NOT default to "one path" merely because it is tidier.        NOT DECIDED

D5 — Truncation:
     Does the unified case use a single truncation after the combined percentage
     is applied, and does the SAME truncation rule apply to the BuffDebuff-only
     case?
     The decision must state:
       the truncation point(s) for each case the decision defines
       whether an intermediate value is truncated or carried at full precision
       that the result is integer-deterministic (TDD.md §6)
     Do NOT infer this from arithmetic coincidence. In particular, at the
     provisioned magnitudes a single combined truncation and a per-instance
     sequential truncation can coincide for some inputs; that coincidence is a
     property of those numbers and is NOT evidence for either rule.  NOT DECIDED

D6 — Base PetState.ATK independence:
     Confirm explicitly, for the formula(s) D1–D5 select, that:
       PetState.ATK remains the permanent/base ATK value
       neither formula overwrites, mutates, or restores PetState.ATK
       no DefaultATK-style runtime reset mechanism is introduced
       EffectivePetATK (and any EffectiveATK) remain derived state, never stored
     This must be confirmed explicitly for this reconciliation rather than
     inherited silently from §5.4.4 or §5.6.4: cite them or decide otherwise.
     Do not introduce a second ATK representation.                   NOT DECIDED
```

**All six items above are `NOT DECIDED`.** Each answer, once supplied, is
recorded in "Decision Record (D1–D6)" and is the input to the downstream
contract-application task. The `NOT DECIDED` marker records the state in which
the decision was requested.

---

## Decision Options

Presented for the decision-maker. **This task recommends none of them, orders
them by nothing, and selects none.** Each is listed only to make the decision
space explicit and to show that materially different, documented-consistent
options exist.

```text
D1 — BuffDebuff-only behavior
  A. §5.4.1 continues to govern the BuffDebuff-only case
  B. §5.6.6 governs; the BuffDebuff is a signed modifier in the unified model
  C. another explicit rule

D2 — §5.6.6 scope
  coexistence-only (a separate rule governs each other case) | canonical model
  for all applicable Pet ATK modifiers

D3 — BuffDebuff sign mapping
  -|Magnitude| as signed percentage points | an explicitly stated alternative
  mapping | mapping not required (if D1 selects A)

D4 — Calculation boundary
  one unified composition path | two distinct paths (stated) | other

D5 — Truncation
  single truncation after combined application, same rule in every case |
  per-instance truncation retained for the BuffDebuff-only case | other

D6 — Base stat
  PetState.ATK unchanged and never mutated, EffectivePetATK derived
  (citing §5.4.4 / §5.6.4) | another explicit position
```

---

## Decision Record (D1–D6)

**Recorded verbatim as supplied by the Product Owner.** Each item answers the
correspondingly-numbered item of "Required Decision Coverage". No value below was
derived, inferred, computed, or reinterpreted by the executing agent, and no
assumption was added.

```text
D1 — BuffDebuff-only behavior:
B. §5.6.6 governs. A Turn-based BuffDebuff TargetStat = ATK participates
as a signed modifier in the unified Pet ATK composition, including when
no Relic ATK modifier is active.

D2 — Unified composition boundary:
§5.6.6 is the canonical composition model for all applicable Pet ATK
modifiers. §5.4.1's absolute-value ATK composition is superseded/narrowed
for ATK composition and must no longer define a separate calculation path.
The BuffDebuff StatusEffect consumption/lifetime contract remains governed
by §5.4; only its contribution to EffectivePetATK is governed by the
unified composition model.

D3 — BuffDebuff sign mapping:
A BuffDebuff ATK reduction contributes -|Magnitude| signed percentage
points to TotalATKModifierPercentage. A BuffDebuff ATK increase, if
introduced by an authoritative gameplay rule, contributes +|Magnitude|
signed percentage points. The stored StatusEffect Magnitude remains the
existing applied magnitude and does not itself encode the sign.

D4 — Calculation boundary:
Use one unified composition calculation to produce EffectivePetATK for
all applicable Pet ATK modifiers. Relic ATK modifiers and applicable
BuffDebuff ATK modifiers contribute to the same signed total. No second
stored or derived ATK representation is introduced.

D5 — Truncation:
Apply the combined signed percentage to PetState.ATK and truncate toward
zero exactly once after the combined calculation. No intermediate
per-modifier truncation occurs.

D6 — Base PetState.ATK independence:
PetState.ATK remains the permanent/base ATK value and is never overwritten,
mutated, restored, or reset by modifier resolution. No DefaultATK-style
runtime reset mechanism exists. EffectivePetATK is derived at calculation
time and is never stored as a second ATK representation.
```

The `NOT DECIDED` markers in "Required Decision Coverage" record the state in
which the decision was requested; the answer for each is the
correspondingly-numbered decision above, which is the recorded decision and the
input to the downstream contract-application task. The markers are retained
rather than rewritten so the original request and the supplied answer remain
separately auditable.

---

## Evidence — the two formulas and their divergence

Recorded as evidence for the decision-maker. **This task does not resolve it.**

```text
COMBAT_RULES.md §5.4.1 (BuffDebuff, Turn-based) — authored by TASK-119
    EffectiveATK = truncate( PetState.ATK × (100 − |Magnitude|) / 100 )
    · ABSOLUTE value of Magnitude
    · reduction-only: the result can never exceed PetState.ATK
    · consumed when the Damage Pipeline call for that attack is constructed
    · §5.4.5 scope: "a Turn-based BuffDebuff instance with TargetStat = ATK"

COMBAT_RULES.md §5.6.6 (Relic + BuffDebuff combined) — authored by TASK-137
    TotalATKModifierPercentage = Σ (all applicable SIGNED ATK modifiers)
    EffectivePetATK = truncate( PetState.ATK × (100 + Total) / 100 )
    · SIGNED; order-independent; the combined percentage is applied ONCE
    · truncate toward zero EXACTLY ONCE
    · states |Magnitude| "is not used for the combined modifier"

Both name the SAME input:
    "Player → Boss Damage Pipeline Step 1 — the `Attack` argument the call
     receives"   (§5.4.1 item 1 and §5.6.1 item 1, near-identical wording)
```

### The concrete MVP case at the provisioned magnitudes

```text
PetState.ATK = 50   (COMBAT_RULES.md §1.1 MVP default)

BOTH modifiers live — TASK-137 requires:
    Total = +5 + (−30) = −25
    EffectivePetATK = truncate(50 × 75 / 100) = truncate(37.5) = 37

Root ONLY (no Relic equipped) — which rule governs is NOT determined:
    §5.4.1 reading:   truncate(50 × (100 − 30) / 100) = truncate(35.0) = 35
    §5.6.6 reading:   Total = −30
                      truncate(50 × (100 − 30) / 100) = truncate(35.0) = 35
```

**Important.** In this particular single-instance example the two readings
**coincide** at 35, because one instance's sequential application and one
instance's combined application are the same operation. That coincidence is a
property of these numbers and **must not be treated as evidence for either
reading**. The readings diverge as soon as more than one `BuffDebuff` ATK
instance is simultaneously active, or as soon as any Relic contribution is
present:

```text
Two simultaneous BuffDebuff ATK instances, −30 and −10, PetState.ATK = 50:

    §5.4.1 sequential, per-instance truncation:
        truncate(50 × 70 / 100) = 35
        truncate(35 × 90 / 100) = truncate(31.5) = 31

    §5.6.6 combined, single truncation:
        Total = −30 + (−10) = −40
        truncate(50 × 60 / 100) = truncate(30.0) = 30

    31 ≠ 30 — the readings are materially different.
```

The example therefore demonstrates the **contract boundary** — that two
authoritative formulas address one input — but does **not** itself determine the
`BuffDebuff`-only rule. Determining it is the Product Owner's decision.

### Why the two cannot be silently merged

```text
1. §5.4.1     authors a complete reduction-only formula and was NOT rewritten
              by TASK-137; §5.6.6 authors a complete signed formula for the
              same input
2. §5.6.5     states §5.6 does not change §5.4's BuffDebuff rule — so §5.6.6's
              own section disclaims superseding §5.4.1
3. §5.5.1     the repository refused to collapse two SIMILAR conventions (the
              Boss-side signed form vs the Pet-side absolute form) into one
              shared formula; it authorizes no cross-rule collapse
4. TASK-137   resolved coexistence only; its scope record states §5.4.1–§5.4.5
              "are unchanged; the decision composes with §5.4 rather than
              redefining it"
5. AGENTS.md  §17 makes code-versus-documentation agreement something to
              establish by DECIDING which was correct — not by precedence, and
              not by reading the current implementation as the answer
```

---

## Scope

### In Scope

1. **Obtain the decision.** Present D1–D6 to the Product Owner / human and
   obtain explicit answers. Present every candidate option neutrally.
2. **Record the decision in this task.** Write the supplied answers into
   "Decision Record (D1–D6)" verbatim. Record nothing the Product Owner did not
   supply.
3. **Record the TASK-137 relationship.** State that TASK-137's D1–D7 are
   accepted decisions, that the Relic × BuffDebuff coexistence case is already
   resolved, and that this task resolves only the relationship between §5.6.6
   and the earlier §5.4.1. Do **not** reopen TASK-137.
4. **Record the TASK-119 relationship.** State that §5.4's `BuffDebuff`
   consumption contract is the subject of the reconciliation, and that this
   task decides its relationship to §5.6.6 rather than rewriting it. Do **not**
   modify TASK-119.
5. **Record the TASK-133 relationship and the blocker.** State precisely why
   TASK-133 STOPPED, at which Stop Condition, and what contract gap remains.
   Do **not** modify TASK-133.
6. **Record the handoff.** State explicitly that the recorded decision is input
   to the subsequent contract-application task, and that TASK-138 itself applies
   the decision to no authoritative document.
7. **Record the cap determination.** State explicitly whether the decision
   authors an ATK range/cap or leaves the current no-cap state
   (`COMBAT_RULES.md` §5.6.1 item 5) in force. Do not invent a cap.
8. **Record any state determination.** If the decision has a state consequence,
   record it as a determination for the downstream task. Do not add a state
   member here.

### Out of Scope

- **Implementing anything** — no composition code, no Damage Pipeline change, no
  `StatusEffectLifecycle` change, no TASK-133 execution. `src/` and `tests/` are
  untouched.
- **Applying the decision to any authoritative document.** `docs/` is untouched
  by this task — including `COMBAT_RULES.md` §5.4 and §5.6/§5.6.6,
  `RELIC_RULES.md`, `GAME_STATE.md`, `BOSS_RULES.md`, and `GAME_RULES.md`. That
  is the subsequent contract-application task.
- **Reopening TASK-137's D1–D7.** The Relic × BuffDebuff coexistence decision,
  the signed composition, order independence, single truncation, independent
  carrier lifetimes, and base-stat independence remain authoritative and
  unmodified. This task does not ask whether Berserker Core and Root may
  coexist — that is already resolved.
- **Reopening TASK-136's Relic ATK contract, TASK-119's `BuffDebuff` rule,
  TASK-128's Boss-side direction decision, or TASK-134's `CardCost` contract.**
- **Inventing a new ATK cap** — `COMBAT_RULES.md` §5.6.1 item 5; any cap
  requires its own explicit decision recorded here.
- **Reusing Crit, CardCost, or Boss-side ATK semantics by analogy** —
  `EffectiveCrit` (§3.3 item 7) sums percentage points on a percentage-point
  stat; `EffectiveCardCost` (`CARD_RULES.md` §3.6) caps a cost reduction at
  100% and truncates; `EffectiveBossATK` (§5.5.1) is the deliberately separate
  Boss-side convention that §5.5.1 and §5.6.6 both explicitly refuse to
  collapse. None is the answer.
- **Creating the TASK-133 prerequisite state carriers** — `PetState.ATKModifiers[]`
  and `PetState.CardCostModifiers[]` do not exist in code. That is a separate,
  independent blocker on TASK-133 and is **not** this task's subject. This task
  creates no state carrier, no lookup interface, and no other task.
- **Changing any provisioned magnitude or duration** — Root's `−30%`/2 Turns
  (`BOSS_RULES.md` §6.3.1 item 3) and Berserker Core's `+5`
  (`RELIC_RULES.md` §8.5) are their owning documents'.
- **Changing `PetState.ATK`'s base value or §1.1's MVP defaults**
- **Any new state member, collection, or representation** — the decision
  reconciles two existing representations; it does not add a third
- **Any new Redis key, SignalR event/method, database table, or migration**
- **Modifying TASK-131, TASK-132, TASK-133, TASK-134, TASK-136, or TASK-137**
  (explicitly prohibited), or any completed or superseded task —
  `TASK_LIFECYCLE.md` §3
- **Creating any further task** — the downstream contract-application task and
  the TASK-133 prerequisite tasks are identified but not created here
- **Any item listed as OUT in `MVP_SCOPE.md` §2**, or anything not IN
  `MVP_SCOPE.md` §1

---

## Dependencies

```text
TASK-137   DONE      the Relic × BuffDebuff coexistence decision and the
                     §5.6.6 composition contract. Not reopened.
TASK-119   DONE      the BuffDebuff TargetStat = "ATK" consumption contract
                     (COMBAT_RULES.md §5.4) whose relationship to §5.6.6 this
                     task determines. Not modified.
TASK-136   DONE      the Relic Battle-lifetime ATK contract (§5.6.1).
                     Not reopened.
TASK-128   DONE      the Boss-side ATK modifier direction contract
                     (§5.5) — EVIDENCE ONLY; its answer must NOT be copied.
TASK-094   DONE      the Status Effect duration-consumption rule
TASK-095   DONE      the Status Effect domain state and step 19a lifecycle
```

---

## Blocks

```text
TASK-133 — Implement Server-Authoritative Relic Trigger and Effect Resolution

TASK-133 cannot implement the Pet ATK Step-1 `Attack` input deterministically
until this contract is resolved and applied to authoritative documentation.

TASK-133 STOPPED at its own Stop Condition (line 292: "If any trigger,
condition, effect, magnitude, or lifetime needed is not fully determined by
RELIC_RULES.md §3–§8 and ADR-018: STOP per AGENTS.md §7 — invent nothing")
rather than choosing between §5.4.1's and §5.6.6's arithmetic.

TASK-133 must NOT be modified by this task.
```

---

## Decision Input Protocol

```text
1. A HUMAN / PRODUCT OWNER supplies an explicit answer to each of D1–D6.
2. The executing agent records each answer VERBATIM in "Decision Record
   (D1–D6)". No value is derived, inferred, computed, or reinterpreted.
3. If ANY of D1–D6 is unanswered:

       D1–D6 = NOT DECIDED
       Status = BLOCKED
       No files modified
       STOP

   — write the STOP CONDITION report in "Stop Conditions" using
   .ai/README.md §13's format, change Status to BLOCKED, and proceed no
   further.

4. The executing agent must NOT select an answer from any of:

       implementation convenience
       the current implementation (StatusEffectLifecycle.EffectiveAttack)
       arithmetic coincidence at particular magnitudes
       TASK-128 / the Boss-side signed convention
       Boss ATK semantics generally
       document order (§5.4 precedes §5.6 in the file)
       TASK-137 alone (it resolved coexistence, not this relationship)
       architectural preference, or the fact that one path is tidier

5. When the decision is recorded, the deliverable is the RECORD. This task
   applies the decision to no authoritative document and implements no
   behavior.
```

---

## Acceptance Criteria

- [ ] The documented two-formula relationship gap is clearly stated
- [ ] Both formulas are presented as evidence with their exact scoping
      statements (§5.4.5, §5.6.5) and their absolute-vs-signed incompatibility
- [ ] The concrete MVP case (Root alone; and Root with Berserker Core) is used
      explicitly and its reachability without any Relic equipped is stated
- [ ] The evidence section states explicitly that the single-instance numerical
      coincidence is a property of the numbers and is NOT evidence for either
      reading, and shows a case where the readings diverge
- [ ] D1–D6 are presented as explicit Product Owner / human decisions
- [ ] D1's candidate categories are presented neutrally with no preselection
- [ ] D2's scope choice is presented neutrally with no inferred answer
- [ ] No D1–D6 option is selected by the executing agent
- [ ] The task explicitly states that TASK-137 D1–D7 remain accepted decisions
      and are not reopened
- [ ] The task explicitly states that the coexistence question is already
      resolved and is not asked again
- [ ] The task explicitly states that implementation convenience, the current
      implementation, document order, arithmetic coincidence, the Boss-side
      convention, and TASK-137 alone must not determine the decision
- [ ] The task explicitly states that no ATK cap may be invented, and that
      `COMBAT_RULES.md` §5.6.1 item 5's no-cap state stands unless the Product
      Owner explicitly decides otherwise
- [ ] The task explicitly records the decision as input for the downstream
      contract-application task
- [ ] The task explicitly states why TASK-133 is blocked and which Stop
      Condition fired
- [ ] The Decision Input Protocol is present and unambiguous
- [ ] TASK-138 does not modify authoritative documentation
- [ ] TASK-138 does not modify source code
- [ ] TASK-138 does not modify tests
- [ ] TASK-119, TASK-128, TASK-133, TASK-136, and TASK-137 remain unchanged
- [ ] No gameplay rule is invented
- [ ] No new ATK cap or ATK range is invented
- [ ] No new state member, collection, or representation is invented
- [ ] No composition operator, order, sign mapping, or rounding point is
      invented
- [ ] No new SignalR event or method is invented
- [ ] No new Redis key is invented
- [ ] No database migration is created
- [ ] No additional task is created by TASK-138
- [ ] Decision coverage audit passes (all six items explicitly resolvable)
- [ ] Neutrality audit passes (no option preselected or recommended)
- [ ] Evidence audit passes (authoritative references verified)
- [ ] Boundary audit passes (TASK-138 remains decision-input only)
- [ ] Protected-file audit passes
- [ ] Source/test audit confirms zero source and test files were modified
- [ ] Authoritative-doc audit confirms zero `docs/` files were modified

---

## Affected Files & Areas

```text
[ ] src/backend/ (none — decision recording only)
[ ] src/frontend/client/ (none)
[ ] tests/ (none — this task has no implementation to test)
[x] docs/ (NONE MODIFIED — the contract-application task owns the docs write)
[x] tasks/backlog/TASK-138-... (this file, and no other task file)
[ ] tasks/completed/ (MUST remain unmodified)
```

---

## Implementation Notes

- **This task's single prohibited action is inventing the answer.** Every item in
  "Required Decision Coverage" is a Product-Owner or human decision. If no answer
  is supplied, STOP per `AGENTS.md` §7 — do not choose a formula, a scope, a
  sign mapping, a path count, or a truncation point.
- **Do not treat the current implementation as the answer.** `AGENTS.md` §17
  requires deciding which of code or documentation was correct rather than
  letting either silently define the other.
  `StatusEffectLifecycle.EffectiveAttack` implements §5.4.1 literally; that is
  evidence of what §5.4.1 says, not of which formula is intended.
- **Do not treat the numerical coincidence as evidence.** At Root alone the two
  readings both yield 35. That is arithmetic, not a determination. The evidence
  section shows a two-instance case where they diverge (31 vs 30) precisely so
  the coincidence cannot be mistaken for a resolution.
- **`§5.6.5` is a live constraint on Reading B.** It states §5.6 does not change
  §5.4's `BuffDebuff` rule. Any decision that makes §5.6.6 universal must say
  how that statement is reconciled — it cannot simply be ignored.
- **`§5.4.5`'s closing rule remains live.** It states that a source for which
  the document defines no consumption rule "is **not** silently treated as an
  ATK modifier". D1 and D2 must either cite a document that genuinely determines
  the behavior or require an explicit Product-Owner decision.
- **`EffectiveCrit`, `EffectiveCardCost`, and `EffectiveBossATK` are
  precedent/evidence only, not the answer.** §5.6.6 and §5.5.1 already refuse
  the analogies in terms; D3 must not import the Boss-side signed convention
  without an explicit Product-Owner decision.
- **No ATK cap may be invented.** `COMBAT_RULES.md` §5.6.1 item 5 records that
  `ATK` has no documented valid range.
- **The state member is shared by both readings.** `GAME_STATE.md` §2.3.1 item 2
  hands `Magnitude` off uninterpreted and the provisioned stored value for Root
  is the positive 30, with the sign carried by `BOSS_RULES.md` §6.3.1 item 3's
  prose. D3 therefore asks for an explicit mapping rather than assuming the
  stored representation already encodes it.
- **Do NOT apply the answer to any document.** The canonical owner documents
  (`COMBAT_RULES.md` §5.4 and §5.6/§5.6.6, and any cross-reference in
  `GAME_STATE.md` or `RELIC_RULES.md`) are updated by the **subsequent**
  contract-application task — not here.
- **Do not conflate this with the other TASK-133 blockers.** The absent
  `PetState.ATKModifiers[]` / `PetState.CardCostModifiers[]` carriers and the
  absent `IRelicDefinitionLookup` are separate, independent gaps. This task
  addresses only the formula relationship.
- **No gameplay expansion.** This task records a composition-relationship
  decision. It introduces no new Relic, Boss, Trigger, Condition, `effectType`,
  Status Effect type, magnitude, Redis key, SignalR event, or table.
- **Do not modify TASK-119, TASK-128, TASK-131, TASK-132, TASK-133, TASK-134,
  TASK-136, or TASK-137.** All are explicitly protected and must be left
  byte-identical.
- **Do not create another task.** The downstream contract-application task and
  the TASK-133 prerequisite tasks are identified and handed off, not created
  here.
- **This task is NOT a broad audit.** Its scope is exactly the one relationship
  named in its title. Do not sweep other Status Effect types, the Boss-side
  rule, the Crit composition, or unrelated gameplay.
- Do not modify `tasks/completed/`.

---

## Downstream Handoff

```text
Decision (this task's record)
        ↓
downstream documentation contract-application task
        ↓
authoritative COMBAT_RULES.md reconciliation
        ↓
implementation dependency becomes deterministic
        ↓
TASK-133 can resume (subject to its other, independent prerequisites)
```

The downstream contract-application task consumes this record and applies the
D1–D6 decision to `COMBAT_RULES.md` §5.4 and §5.6/§5.6.6 and any
cross-referencing document, making the ATK composition deterministic.

**That downstream task is NOT created here.** Neither are the TASK-133
prerequisite tasks identified during the same STOP. Creating them is a separate
operation requiring its own authorization.

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 always
apply. Task-specific:

- **If the Product Owner decision is not supplied when this task is executed:
  STOP.** Record `D1–D6 = NOT DECIDED`, set `Status: BLOCKED`, modify no files,
  and report. Do not select an answer from implementation convenience, the
  current implementation, arithmetic, TASK-128, Boss ATK semantics, TASK-137
  alone, document order, or architectural preference.
- **If TASK-137's recorded decision appears to conflict with this task's
  framing: STOP and report** — do not reinterpret TASK-137 (`AGENTS.md` §4).
- **If the answer would require reopening TASK-137, TASK-136, TASK-128, or
  TASK-119: STOP and report.**
- **If resolving D1–D6 appears to require a new state member, a new wire
  member, a new event, or a new persistence contract: STOP and report** — this
  task's subject is a formula relationship, and either reading composes from
  existing state.
- **If the answer would require a new ADR: STOP and report** — record the
  determination, do not author the ADR.
- **If applying the decision would require modifying any authoritative document
  in this task: STOP** — that is the downstream contract-application task's act.
- **If TASK-133's other independent blockers are folded into this task: STOP**
  — the absent state carriers and the absent Relic definition lookup are
  separate concerns.
- **If asked to create the downstream task or any prerequisite task: STOP** —
  this task creates exactly one task (itself).

---

## Completion Evidence

### Decision Source

- Product Owner / human decision supplied directly to the executing agent
  (conversation turn following the TASK-138 STOP report) — all six of D1–D6
  answered explicitly. No answer was derived, inferred, or selected by the
  executing agent.

### Decision Record

```text
D1: B. §5.6.6 governs. A Turn-based BuffDebuff TargetStat = ATK participates
    as a signed modifier in the unified Pet ATK composition, including when
    no Relic ATK modifier is active.

D2: §5.6.6 is the canonical composition model for all applicable Pet ATK
    modifiers. §5.4.1's absolute-value ATK composition is superseded/narrowed
    for ATK composition and must no longer define a separate calculation path.
    The BuffDebuff StatusEffect consumption/lifetime contract remains governed
    by §5.4; only its contribution to EffectivePetATK is governed by the
    unified composition model.

D3: A BuffDebuff ATK reduction contributes -|Magnitude| signed percentage
    points to TotalATKModifierPercentage. A BuffDebuff ATK increase, if
    introduced by an authoritative gameplay rule, contributes +|Magnitude|
    signed percentage points. The stored StatusEffect Magnitude remains the
    existing applied magnitude and does not itself encode the sign.

D4: Use one unified composition calculation to produce EffectivePetATK for
    all applicable Pet ATK modifiers. Relic ATK modifiers and applicable
    BuffDebuff ATK modifiers contribute to the same signed total. No second
    stored or derived ATK representation is introduced.

D5: Apply the combined signed percentage to PetState.ATK and truncate toward
    zero exactly once after the combined calculation. No intermediate
    per-modifier truncation occurs.

D6: PetState.ATK remains the permanent/base ATK value and is never overwritten,
    mutated, restored, or reset by modifier resolution. No DefaultATK-style
    runtime reset mechanism exists. EffectivePetATK is derived at calculation
    time and is never stored as a second ATK representation.
```

### Documentation

No authoritative documentation was modified.

Verified: zero files under `docs/` were modified by this execution. The
`COMBAT_RULES.md` §5.4.1 / §5.6.6 reconciliation is the subsequent
contract-application task's act, consuming this record.

### Source

No source code modified.

### Tests

No tests modified.

### Handoff

The recorded D1–D6 decision is input to the subsequent contract-application task.

TASK-138 does not itself apply the decision to `COMBAT_RULES.md` §5.4.1,
§5.6.5, or §5.6.6, nor to any other canonical owner document.

```text
What the downstream task consumes from this record:

  D1, D2        §5.6.6 becomes the canonical composition model for all
                applicable Pet ATK modifiers; §5.4.1's absolute-value ATK
                composition is superseded/narrowed for ATK composition and
                must no longer define a separate calculation path, while §5.4
                retains the BuffDebuff consumption/lifetime contract
                → COMBAT_RULES.md §5.4.1, §5.4.2, §5.6.5, §5.6.6, and any
                  cross-reference, with §1.1 and §5.6.1 item 5's no-cap state
                  confirmed unchanged
  D3            the signed contribution mapping for a BuffDebuff ATK
                modifier, including its symmetry for a hypothetical increase
                and the statement that the stored Magnitude does not encode
                the sign
                → COMBAT_RULES.md §5.6.6 (the composition) with
                  GAME_STATE.md §2.3.1 item 2 referenced and not restated
  D4            the single-path calculation boundary
                → COMBAT_RULES.md §5.6.1 / §5.6.6
  D5            the single truncation point
                → COMBAT_RULES.md §5.6.1 item 4 / §5.6.6, reconciled against
                  §5.4.2's existing truncation statement
  D6            the base-stat independence statement
                → COMBAT_RULES.md §5.6.4, confirmed for the unified case
  the cap       COMBAT_RULES.md §5.6.1 item 5 and §1.1 confirmation that no
  determination new ATK cap is introduced
```

### TASK-133

Still blocked on its other independent prerequisites unless those prerequisites
have separately been resolved.

```text
TASK-138 resolves ONLY the §5.4.1 ↔ §5.6.6 ATK formula relationship.

TASK-133's other independent blockers are NOT resolved by this task and remain
open:
  PetState.ATKModifiers[] runtime carrier      — no code representation exists
  PetState.CardCostModifiers[] runtime carrier — no code representation exists
  IRelicDefinitionLookup                       — no Application contract exists

TASK-133 is NOT claimed implementation-ready. Those concerns remain separate
and were deliberately not folded into TASK-138 (TASK-138 §8).
```

### Changed Files

```text
tasks/backlog/TASK-138-resolve-buffdebuff-atk-formula-relationship-to-combined-atk-composition.md
```

### Validation Results

```text
Decision coverage audit: PASS (all six D1–D6 items explicitly answered)
Neutrality audit: PASS (approved Product Owner decision recorded verbatim)
Evidence audit: PASS (authoritative references verified — §5.4.1, §5.4.5,
                       §5.6.1 item 5, §5.6.5, §5.6.6, and the shared Step-1
                       `Attack` input all confirmed in COMBAT_RULES.md)
Boundary audit: PASS (TASK-138 remains decision-input only; the decision is
                       applied to no authoritative document)
Protected-file audit: PASS (TASK-119, TASK-128, TASK-133, TASK-136, TASK-137,
                       and tasks/completed/ untouched)
Source/test audit: PASS (zero files under src/ or tests/ modified)
Authoritative-doc audit: PASS (zero files under docs/ modified)
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed zero files modified under `src/` or `tests/`
- [x] Confirmed zero files modified under `docs/`
- [x] Confirmed TASK-119, TASK-128, TASK-133, TASK-136, TASK-137, and
      `tasks/completed/` unmodified
- [x] Confirmed no ATK cap or range was invented
- [x] Confirmed no composition operator, scope, sign mapping, or rounding point
      was invented (each was supplied by the Product Owner and recorded verbatim)
- [x] Confirmed no new state member, Redis key, SignalR event, or table was
      introduced
- [x] Confirmed no additional task was created
- [x] Confirmed no ADR was created
- [x] Confirmed TASK-137 was not reopened and the coexistence question was not
      re-asked
