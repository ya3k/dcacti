# TASK-178 — Canonicalize MVP Relic Runtime Blocker Decisions

<!--
  GEN-TASK EXECUTION MANIFEST — DOCUMENTATION / SPECIFICATION TASK
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and task files by path and section — it does NOT copy
  game rules, formulas, magnitudes, schemas, or contracts as new authority.

  SCOPE: Documentation / specification only.
  No runtime/domain code, no migrations, no seed/HasData/INSERT,
  no database provisioning, and no test modifications are in scope.
-->

---

## Metadata

```text
Task ID:           TASK-178
Title:             Canonicalize MVP Relic Runtime Blocker Decisions
Status:            DONE
Type:              Documentation / Specification
Priority:          High
Risk:              LOW (documentation updates and state contract synchronization only;
                   no code, migrations, or database changes)
Primary Agent:     orchestrator
Supporting Agents: gameplay (RELIC_RULES.md, COMBAT_RULES.md),
                   backend (GAME_STATE.md, REDIS_STATE.md),
                   review (verification against approved PO decisions)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   gameplay/gameplay-behavior-derivation,
                   quality/scope-validation
Dependencies:      TASK-176 (DONE — MVP Relic PO decision collection),
                   TASK-177 (BACKLOG — Relic runtime implementation where blockers Q-1..Q-4 were identified)
Blocks:            TASK-177 (Implement Approved MVP Relic Runtime Contracts)
Source:            TASK-177 implementation-readiness blockers
```

---

## Objective

Synchronize the canonical game design and technical state documentation (`GAME_STATE.md`, `RELIC_RULES.md`, `COMBAT_RULES.md`) with the four authoritative Product Owner-approved runtime decisions (**Q-1 = A**, **Q-2 = B**, **Q-3 = A**, **Q-4 = C**) discovered as blockers during the preparation of **TASK-177**.

By formalizing these four decisions into canonical documentation owners, TASK-178 resolves all architectural and gameplay ambiguities so that **TASK-177** can proceed with implementation without guessing.

---

## Authoritative References

- `AGENTS.md` §2, §4, §6, §7, §8, §9, §10, §12, §13, §16, §17, §18, §20, §23
- `docs/00-overview/MVP_SCOPE.md` §1 — MVP Relic scope (~10 Relics)
- `docs/01-game-design/RELIC_RULES.md` §3 — Supported Triggers (`OnBattleStart`, `OnCascade`, `OnPowerGain`, `OnDamageTaken`)
- `docs/01-game-design/RELIC_RULES.md` §4 — Deterministic trigger order and queue
- `docs/01-game-design/RELIC_RULES.md` §5 — Anti-infinite-chain rule
- `docs/01-game-design/RELIC_RULES.md` §6 — MVP Relic Reference table and notes
- `docs/01-game-design/RELIC_RULES.md` §8.2 — Structured `EffectDefinition[]` vocabulary
- `docs/01-game-design/RELIC_RULES.md` §8.3 — Allowed effect target, lifetime, and valueType combinations
- `docs/01-game-design/RELIC_RULES.md` §8.4 — Effect lifetime vs trigger re-evaluation independence
- `docs/01-game-design/RELIC_RULES.md` §8.5 — Canonical Relic contracts (specifically Burning Curse, Arcane Battery, Cascade Core, Battle Instinct)
- `docs/01-game-design/GAME_RULES.md` §12 — Power Rules (0–100 range)
- `docs/01-game-design/GAME_RULES.md` §16 — Event List
- `docs/01-game-design/GAME_RULES.md` §17 — Fixed event resolution order (steps 11, 13, 18c, 19a)
- `docs/01-game-design/MATCH3_RULES.md` §4, §4.2, §4.3 — Cascade loop and cascade depth iterations
- `docs/01-game-design/COMBAT_RULES.md` §2 — Resource generation (Power-gain surface)
- `docs/01-game-design/COMBAT_RULES.md` §3.3 items 7–11 — `NextAttack` lifetime boundary, consumption on qualifying attack, and source-specific removal
- `docs/01-game-design/COMBAT_RULES.md` §5.1, §5.2 — Burn Status Effect, source tracking, and DoT damage-pipeline traversal
- `docs/01-game-design/COMBAT_RULES.md` §5.6, §5.6.6 — Pet ATK modifier composition
- `docs/02-technical/GAME_STATE.md` §2.3.4, §5.1.2 — `NextAttackCritModifiers[]` carrier and lifecycle
- `docs/02-technical/GAME_STATE.md` §2.3.7, §5.1.4 — `ATKModifiers[]` carrier and lifecycle
- `docs/02-technical/REDIS_STATE.md` §7 item 16 — `PetState.ATKModifiers[]` serialization and round-trip invariant
- `tasks/completed/TASK-176-collect-product-owner-decisions-for-mvp-relic-content.md` — Approved MVP Relic decisions
- `tasks/backlog/TASK-177-implement-approved-mvp-relic-runtime-contracts.md` — Downstream implementation task documenting blockers Q-1 through Q-4

---

## Relationship to TASK-177 Pipeline

```text
TASK-176 (DONE)
  ↓
Approved MVP Relic gameplay contracts
  ↓
TASK-177 (BACKLOG)
  ↓
Identified runtime blockers Q-1, Q-2, Q-3, Q-4
  ↓
TASK-178 (THIS TASK — BACKLOG)
  ↓
Canonicalize PO-approved runtime/state decisions in docs/
  ↓
TASK-177 Implementation unblocked (NO GUESSING)
```

TASK-178 is a strict prerequisite for TASK-177 implementation. It does **not** supersede TASK-177. TASK-177 remains the implementation task for runtime/domain code.

---

## Scope

### In Scope
- Canonicalize Product Owner decisions Q-1, Q-2, Q-3, and Q-4 across authoritative documents.
- Update `docs/02-technical/GAME_STATE.md` (§2.3.7 and §5.1.4) to reconcile `ATKModifiers[]` lifetime support for both `Battle` and `NextAttack`, documenting `NextAttack` consumption semantics without introducing extra collections.
- Update `docs/01-game-design/RELIC_RULES.md` (§3, §5, §8.5 note 7) to canonically define the qualifying surface for `OnPowerGain` (non-Relic-generated Power gains only, preventing Relic chain recursion).
- Update `docs/01-game-design/RELIC_RULES.md` (§3, §8.5 note 9) to formalize that each actual cascade iteration is an independent qualifying `OnCascade` event boundary.
- Update `docs/01-game-design/RELIC_RULES.md` (§6 note 1, §8.5 note 5) and cross-reference `docs/01-game-design/COMBAT_RULES.md` (§5.2) to specify that Burning Curse's `+30%` `BurnDamage` applies only to Pet-owned/source Burn damage, not Boss-owned Burn damage.
- Maintain single-source-of-truth ownership per `AGENTS.md` §2 with concise cross-references.

### Out of Scope
- Modifying runtime or domain code (`src/backend/**`, `src/frontend/**`).
- Modifying `RelicResolver`, `BattleStateService`, `DamagePipeline`, or combat handlers.
- Creating migrations, seed data, `HasData`, `INSERT` statements, or schema alterations.
- Provisioning `RelicDefinition` database rows (remains with the content provisioning task).
- Adding or modifying automated tests.
- Inventing new Triggers, Conditions, Effects, Targets, or Lifetimes.
- Re-opening or altering approved decisions from TASK-176.

---

## Authoritative Product Owner Decisions

The following four decisions are binding and authoritative:

```text
Q-1 = A
Q-2 = B
Q-3 = A
Q-4 = C
```

### 1. Q-1 — ATK + NextAttack State Carrier (Option A)

**Approved Decision:** Option A.
The existing `ATKModifiers[]` carrier on `PetState` is extended to support both `Battle` and `NextAttack` lifetimes.

**Constraints:**
- The project must **NOT** introduce `NextAttackATKModifiers[]`.
- The project must **NOT** introduce a generic `NextAttackModifiers[]`.

**Canonical Semantics:**
- `ATKModifiers[]` elements carry an ATK modifier whose lifetime is either `Battle` or `NextAttack`.
- Example structured effect representation:
  ```json
  {
    "effectType": "ATK",
    "valueType": "Percentage",
    "value": 10,
    "target": "Pet",
    "lifetime": "NextAttack"
  }
  ```
- Battle Instinct uses this contract.
- A `NextAttack` ATK modifier:
  1. is applied to the next qualifying Pet attack (aligned with `COMBAT_RULES.md` §3.3 qualifying attack consumption boundary);
  2. is consumed when that qualifying attack resolves;
  3. is never permanently added to base `PetState.ATK`;
  4. is non-stacking per Battle Instinct's approved Relic contract;
  5. is refreshed/replaced on a qualifying re-trigger rather than accumulating duplicate modifiers from the same source.
- Existing `Battle` lifetime ATK modifiers (e.g. Berserker Core) retain their existing semantics unchanged.

**Documentation Owner & Action:**
- Primary owner: `docs/02-technical/GAME_STATE.md` §2.3.7 and §5.1.4.
- Reconcile the previous statement that `ATKModifiers[]` is "Battle-only" with `Battle | NextAttack`. Document the `NextAttack` lifecycle, consumption at the qualifying attack boundary, and source-specific removal.
- Cross-reference in `docs/01-game-design/RELIC_RULES.md` §8.3 and §8.5 item 10.

---

### 2. Q-2 — OnPowerGain Qualifying Surface (Option B)

**Approved Decision:** Option B.
`OnPowerGain` triggers exclusively for qualifying **non-Relic-generated Power gains**. Power granted by a Relic effect does **not** qualify as another `OnPowerGain` trigger in that Relic chain.

**Canonical Rule:**
> `OnPowerGain` reacts to qualifying Power gains originating outside Relic effect resolution. Power granted by a Relic effect is not itself a qualifying `OnPowerGain` event.

**Examples & Boundary:**
- Match resolution producing Power (e.g. POWER Gem match) → **Qualifies**.
- Card / Skill / gameplay system granting Power → **Qualifies** (if recognized as a Power-gain event).
- Relic effect granting Power (e.g. Arcane Battery granting +5 Power, Cascade Core granting +5 Power, Mana Crystal granting +10 Power) → **Does NOT qualify** as an `OnPowerGain` trigger.

**Purpose:**
Directly enforces `RELIC_RULES.md` §5 anti-infinite-chain safety by eliminating self-referential or cross-Relic recursion:
```text
OnPowerGain
→ Arcane Battery (+5 Power)
→ [BLOCKED from triggering OnPowerGain]
```

**Constraints:**
- Do NOT create a new Trigger type.
- Do NOT create a new Condition type.
- Do NOT define arbitrary future Power sources.

**Documentation Owner & Action:**
- Primary owner: `docs/01-game-design/RELIC_RULES.md` §3 (Trigger definitions), §5 (Anti-Infinite-Chain Rule), and §8.5 note 7 (Arcane Battery contract).
- Cross-reference in `docs/01-game-design/GAME_RULES.md` §16 / §17 step 13 if necessary.

---

### 3. Q-3 — OnCascade Event Boundary (Option A)

**Approved Decision:** Option A.
Each actual cascade iteration resolved by the Match-3 engine constitutes an independent qualifying `OnCascade` event boundary.

**Canonical Semantics:**
- Resolution sequence:
  ```text
  Player Swap
   └─ Initial detection pass (depth 1 — Swap match, not a cascade)
   └─ Cascade iteration #1 (depth 2) → fires OnCascade
   └─ Cascade iteration #2 (depth 3) → fires OnCascade
   └─ Cascade iteration #3 (depth 4) → fires OnCascade
  ```
- Cascade Core resolves independently for each qualifying cascade iteration (e.g. Cascade 1: +5 Power, Cascade 2: +5 Power, Cascade 3: +5 Power).
- Cascades from a single Swap must **NOT** be collapsed into a single aggregated `OnCascade` event.
- Do NOT invent a new root-event Trigger.
- Do NOT change existing Match-3 cascade mechanics or Combo accounting (`MATCH3_RULES.md` §4).

**Documentation Owner & Action:**
- Primary owner: `docs/01-game-design/RELIC_RULES.md` §3 and §8.5 note 9.
- Explicitly cite `docs/01-game-design/MATCH3_RULES.md` §4.2 / §4.3 regarding cascade pass depth and iteration boundaries.

---

### 4. Q-4 — Burning Curse Burn Ownership (Option C)

**Approved Decision:** Option C.
Burning Curse modifies Burn damage caused by the **Pet** (as the owning/applying source). It does **NOT** modify Burn damage caused by the Boss.

**Canonical Semantics:**
- Burn source/ownership governs modifier eligibility:
  - Pet applies Burn to Boss → Pet is source/owner → Burning Curse `+30%` applies to that Burn's damage ticks.
  - Pet-owned Burn tick resolves → receives `+30%` damage bonus.
  - Boss applies Burn to Pet → Boss is source/owner → Burning Curse does **NOT** apply.
  - Boss-owned Burn tick resolves → deals normal un-modified Burn damage.
- The distinction is **Burn source/owner**, not damage recipient.
- Burning Curse contract remains:
  ```text
  RelicId: relic-burning-curse
  Trigger: OnBattleStart
  Condition: null
  Effect: BurnDamage
  ValueType: Percentage
  Value: 30
  Target: Pet
  Lifetime: Battle
  ```
- The target `Pet` designates the Pet as the owner whose outgoing Burn damage is modified.
- Do NOT alter `OnBattleStart`, `+30% BurnDamage`, or `Battle` lifetime approved in TASK-176.
- Do NOT introduce a second Burn effect subsystem.

**Documentation Owner & Action:**
- Primary owner: `docs/01-game-design/RELIC_RULES.md` §6 note 1 and §8.5 note 5.
- Cross-reference in `docs/01-game-design/COMBAT_RULES.md` §5.1 / §5.2 (Burn Status Effect rules).

---

## Canonical Documentation Placement Matrix

| Decision | Primary Canonical Owner | Supporting Cross-References | Rationale |
| --- | --- | --- | --- |
| **Q-1 (ATKModifiers carrier & NextAttack lifetime)** | `docs/02-technical/GAME_STATE.md` §2.3.7, §5.1.4 | `docs/01-game-design/RELIC_RULES.md` §8.3, §8.5 item 10 | `GAME_STATE.md` owns Battle State schema, carrier invariants, and lifecycle transitions. |
| **Q-2 (OnPowerGain non-Relic source boundary)** | `docs/01-game-design/RELIC_RULES.md` §3, §5, §8.5 note 7 | `docs/01-game-design/GAME_RULES.md` §16, §17 step 13 | `RELIC_RULES.md` owns Relic trigger conditions and anti-infinite-chain resolution rules. |
| **Q-3 (OnCascade per-iteration event boundary)** | `docs/01-game-design/RELIC_RULES.md` §3, §8.5 note 9 | `docs/01-game-design/MATCH3_RULES.md` §4.2, §4.3 | `RELIC_RULES.md` owns trigger event mappings; `MATCH3_RULES.md` defines cascade iteration identity. |
| **Q-4 (Burning Curse Pet-ownership Burn damage)** | `docs/01-game-design/RELIC_RULES.md` §6 note 1, §8.5 note 5 | `docs/01-game-design/COMBAT_RULES.md` §5.1, §5.2 | `RELIC_RULES.md` owns Relic effect behavior; `COMBAT_RULES.md` owns Status Effect calculation. |

---

## Acceptance Criteria

### AC-01 — Q-1: Carrier Lifetime Extension
`docs/02-technical/GAME_STATE.md` §2.3.7 and §5.1.4 explicitly support both `Battle` and `NextAttack` lifetimes for `ATKModifiers[]`, and document consumption semantics upon the next qualifying Pet attack.

### AC-02 — Q-1: Battle Instinct Alignment
Battle Instinct's approved `ATK + NextAttack` contract no longer contradicts `GAME_STATE.md`. No `NextAttackATKModifiers[]` or generic carrier is introduced.

### AC-03 — Q-2: Non-Relic Power Gain Definition
`docs/01-game-design/RELIC_RULES.md` §3 explicitly defines `OnPowerGain` as triggering on qualifying Power gains originating outside Relic effect resolution.

### AC-04 — Q-2: Relic Power Anti-Recursion
`docs/01-game-design/RELIC_RULES.md` §5 and §8.5 note 7 explicitly specify that Relic-generated Power does not qualify as an `OnPowerGain` trigger, preventing recursive self-looping.

### AC-05 — Q-3: Independent Cascade Iterations
`docs/01-game-design/RELIC_RULES.md` §3 and §8.5 note 9 establish that each cascade iteration resolved by the Match-3 engine is an independent `OnCascade` event boundary.

### AC-06 — Q-3: Multi-Cascade Triggering
`RELIC_RULES.md` clarifies that multiple cascades occurring within a single Swap independently trigger Cascade Core.

### AC-07 — Q-4: Pet Burn Ownership
`docs/01-game-design/RELIC_RULES.md` §6 note 1 and §8.5 note 5 explicitly specify that Burning Curse applies only to Pet-owned/source Burn damage.

### AC-08 — Q-4: Boss Burn Exclusion
`docs/01-game-design/RELIC_RULES.md` and `docs/01-game-design/COMBAT_RULES.md` confirm that Boss-owned Burn ticks do not receive Burning Curse's `+30%` bonus.

### AC-09 — Existing Semantics Preserved
Pre-existing semantics for `OnHpBelow`, `OnCombo`, `OnMatchCount`, and the four existing MVP Relics (`relic-berserker-core`, `relic-mana-crystal`, `relic-assassin-eye`, `relic-emergency-core`) remain completely unaltered.

### AC-10 — No Architecture Invention
No new Trigger, Condition, Effect, Target, or Lifetime is introduced beyond those already established in TASK-176.

### AC-11 — Canonical Ownership Discipline
Each decision is assigned to exactly one primary canonical documentation owner with concise cross-references, avoiding redundant rule duplication.

### AC-12 — TASK-177 Unblocked
All four blockers Q-1 through Q-4 in `tasks/backlog/TASK-177-implement-approved-mvp-relic-runtime-contracts.md` have unambiguous canonical references, unblocking implementation.

### AC-13 — No Runtime Code Changes
No changes are made to `src/**`, domain classes, application services, or runtime engines.

### AC-14 — No Database Changes
No migrations, schema changes, seeds, or database rows are created.

### AC-15 — Task Lifecycle Discipline
TASK-178 can be marked DONE and moved to `tasks/completed/` only after all documentation updates are executed, verified, and reviewed.

---

## Affected Documentation Files

```text
docs/02-technical/GAME_STATE.md
docs/01-game-design/RELIC_RULES.md
docs/01-game-design/COMBAT_RULES.md
tasks/backlog/TASK-177-implement-approved-mvp-relic-runtime-contracts.md
```

---

## Definition of Done

```text
[x] Product Owner decisions Q-1 through Q-4 understood and recorded verbatim
[x] Relevant documentation read and existing carriers/rules inspected
[x] Documentation updates applied to primary canonical owners:
    [x] GAME_STATE.md updated for Q-1 (ATKModifiers[] Battle | NextAttack)
    [x] RELIC_RULES.md updated for Q-2 (OnPowerGain qualifying surface)
    [x] RELIC_RULES.md updated for Q-3 (OnCascade event boundary)
    [x] RELIC_RULES.md & COMBAT_RULES.md updated for Q-4 (Burning Curse source ownership)
[x] TASK-177 "Open Questions / Blockers" section updated to reference TASK-178 resolution
[x] No code files modified
[x] No database or migration files created
[x] No gameplay content or vocabulary invented
[x] All AC-01 through AC-15 verified
```

---

## Completion Evidence

### Decisions Applied

```text
Q-1 = A   ATKModifiers[] supports Battle + NextAttack (single carrier)
Q-2 = B   OnPowerGain accepts qualifying non-Relic-generated Power gains
Q-3 = A   each cascade iteration is an independent OnCascade event
Q-4 = C   Burning Curse applies to Pet-owned/source Burn only
```

### Changed Files

- `docs/02-technical/GAME_STATE.md` — v2.21 → **v2.22**. §2.3 tree's
  `ATKModifiers[]` entry restated as the single ATK carrier with a declared
  `lifetime`. §2.3.7: no longer `Battle`-only; the element schema gained the
  required `Lifetime` member (`Battle` | `NextAttack`); new **item 11** records
  the two-lifetime contract, the per-effect carrier map, and that
  `NextAttackATKModifiers[]` / generic `NextAttackModifiers[]` are **not**
  introduced. Item 8 (neither lifetime is Turn-based) and item 4
  (one-entry-per-source ⇒ non-stacking) reconciled. §2.3.8: the serialized
  member list gained `lifetime`; the round-trip obligation extended to cover it.
  §5.1.4: retitled **Create, Refresh, Consume, Expire**; the state diagram,
  item 4 (the `NextAttack` qualifying-attack consumption boundary, referencing
  `COMBAT_RULES.md` §3.3 items 7–11 rather than restating it), the
  lifetime-scoped-consumption rule, and item 7 (`PetState.ATK` never mutated by
  a `NextAttack` modifier) added; items renumbered accordingly.
- `docs/01-game-design/RELIC_RULES.md` — v1.13 → **v1.14**. §3's `OnPowerGain`
  line qualified. New **§3.1** (Q-2: non-Relic-generated Power gains qualify;
  Relic-granted Power does not; the recursive shape is blocked; no new Trigger,
  Condition, Power source, or event) and new **§3.2** (Q-3: each cascade
  iteration is an independent `OnCascade` event; `MATCH3_RULES.md` §4.2 owns
  iteration identity; this is §5 item 2's naturally-repeating-Trigger case).
  §5 gained the paragraph naming the two operative boundaries without widening
  §5's own rules. §6 note 1 (Q-4: Pet-owned/source Burn only, Boss-owned
  excluded, `target: Pet` is the owner/source context) and §8.3 item 4 (Q-1:
  `ATK`'s two lifetimes share one carrier; consumption at the existing
  qualifying-attack boundary; refresh-not-accumulate) extended. §8.5 items 5, 7,
  9, and 10 extended with the Q-4, Q-2, Q-3, and Q-1 contract references.
  §3's closed Trigger list, §8.1's Condition forms, §8.2's `effectType` set,
  §8.3's table rows, and §6's 10 rows are unchanged.
- `docs/01-game-design/COMBAT_RULES.md` — v2.4 → **v2.5**. §3.3 item 8 (the
  canonical `NextAttack` owner) now states that the **same** boundary governs a
  Relic `ATK` modifier declaring `lifetime: NextAttack` — one consumption rule,
  no second carrier. §5.2 item 3 notes a DoT tick consumes no `NextAttack`
  modifier. New §5.2 item 4 (Q-4): a `BurnDamage` modifier is scoped by the Burn
  instance's **source/ownership** — Pet-owned modified, Boss-owned not — and it
  changes damage only (no Burn event, no second instance). Existing §5.2
  numbering is preserved.
- `docs/02-technical/REDIS_STATE.md` — v1.10 → **v1.11**. §7 item 16 reconciled:
  the element now carries its declared `lifetime`, and the round-trip obligation
  covers that member. No key, structure, lifecycle, TTL, or concurrency rule
  changed.
- `tasks/backlog/TASK-177-implement-approved-mvp-relic-runtime-contracts.md` —
  "Open Questions / Blockers" replaced with "Open Questions / Blockers —
  RESOLVED by TASK-178" (pointer-plus-summary; the canonical sections remain the
  authority). Its DoD checkbox and metadata `Dependencies` line updated.
  **TASK-177's implementation scope was not rewritten, and TASK-177 was not
  executed or marked complete.**
- `tasks/completed/TASK-178-canonicalize-mvp-relic-runtime-blocker-decisions.md`
  — status set to DONE and the task moved here.

### Acceptance Criteria Verification

```text
AC-01  PASS  GAME_STATE.md §2.3.7 (item 11, schema) and §5.1.4 (item 4) state
             both lifetimes and the next-qualifying-attack consumption.
AC-02  PASS  Battle Instinct no longer contradicts GAME_STATE.md; no
             NextAttackATKModifiers[] and no generic NextAttackModifiers[]
             exists anywhere (asserted in §2.3.7 item 11 and §8.5 item 10).
AC-03  PASS  RELIC_RULES.md §3's OnPowerGain line and §3.1 define the
             qualifying surface as Power gains from outside Relic resolution.
AC-04  PASS  RELIC_RULES.md §5's boundaries paragraph and §8.5 item 7 state
             that Relic-generated Power does not qualify, blocking the loop.
AC-05  PASS  RELIC_RULES.md §3.2 and §8.5 item 9 establish per-iteration
             OnCascade boundaries, citing MATCH3_RULES.md §4.2.
AC-06  PASS  §3.2 item 2/3 and §8.5 item 9 state that several cascades in one
             Swap independently resolve Cascade Core and are not collapsed.
AC-07  PASS  RELIC_RULES.md §6 note 1 and §8.5 item 5 state Pet-owned/source
             Burn only.
AC-08  PASS  RELIC_RULES.md §6 note 1/§8.5 item 5 and COMBAT_RULES.md §5.2
             item 4 confirm Boss-owned Burn receives no bonus.
AC-09  PASS  OnHpBelow, OnCombo, OnMatchCount and the four provisioned Relics
             are untouched; §6's rows and §8.5's contracts are byte-identical
             in value.
AC-10  PASS  No Trigger, Condition, effectType, Target, or Lifetime value was
             added. §3's 12-value Trigger list, §8.1's three Condition forms,
             §8.2's five effectType values, and §8.3's six table rows are
             unchanged.
AC-11  PASS  One primary owner per decision (Q-1 → GAME_STATE.md §2.3.7/§5.1.4;
             Q-2 → RELIC_RULES.md §3.1; Q-3 → RELIC_RULES.md §3.2;
             Q-4 → RELIC_RULES.md §6 note 1/§8.5 item 5), with cross-references
             from COMBAT_RULES.md, REDIS_STATE.md, and RELIC_RULES.md §8.3/§8.5
             instead of duplicated rules.
AC-12  PASS  TASK-177's blockers now carry unambiguous canonical references and
             the section is marked RESOLVED; TASK-177 is READY.
AC-13  PASS  No file under src/** was modified.
AC-14  PASS  No migration, schema change, seed, HasData, or INSERT was created.
AC-15  PASS  All documentation updates are applied and verified; status set to
             DONE and the task moved to tasks/completed/.
```

### Scope Confirmation

```text
Runtime (src/**)          UNCHANGED
Tests (tests/**)          UNCHANGED
Migrations / provisioning UNCHANGED
Database schema           UNCHANGED
Redis schema              UNCHANGED (REDIS_STATE.md text only)
Frontend                  UNCHANGED
Unrelated documentation   UNCHANGED
```

### Note for TASK-177

`docs/02-technical/REDIS_STATE.md` §7 item 16 now records that an
`ATKModifiers[]` element carries `lifetime`, so TASK-177's read/write path must
round-trip that member. That is a consequence of Q-1 = A's chosen carrier, not a
new decision.
