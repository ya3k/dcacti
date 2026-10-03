# TASK-139 — Apply the Unified Pet ATK Composition Contract (TASK-138 Decision)

<!--
  GEN-TASK EXECUTION MANIFEST — DOCUMENTATION CONTRACT-APPLICATION TASK
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  THIS TASK APPLIES A DECISION THAT IS ALREADY MADE. It decides nothing.
  The decision was supplied by the Product Owner and recorded verbatim in
  TASK-138 ("Decision Record (D1–D6)"). This task transcribes that recorded
  decision into its canonical owner documents (COMBAT_RULES.md §5.4 and §5.6).

  PROVENANCE: Identified as a blocker during TASK-133 execution. Two authoritative
  formulas in COMBAT_RULES.md (§5.4.1 and §5.6.6) both addressed the Damage
  Pipeline Step-1 Attack input under incompatible arithmetic conventions.
  TASK-138 was created as the decision-input task and obtained the explicit
  Product Owner decision set D1–D6. This task applies that decision set to the
  authoritative documentation.

  BOUNDARY: This task modifies docs/01-game-design/COMBAT_RULES.md (§5.4.1,
  §5.4.2, §5.4.5, §5.6.1, §5.6.5, §5.6.6). It touches NO source code, NO tests,
  and creates NO ADR. It does not implement TASK-133 and does not resolve
  TASK-133's other independent prerequisites (PetState.ATKModifiers[],
  PetState.CardCostModifiers[], IRelicDefinitionLookup).
-->

---

## Metadata

```text
Task ID:           TASK-139
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md. The deliverable is
                   reconciled contract statements at their canonical owners.
                   NOT GAMEPLAY-CHANGE: this task changes no gameplay value —
                   the decisions were made by the Product Owner in TASK-138.)
Status:            DONE (direct execution recorded: TASK-138 D1–D6 applied to
                   COMBAT_RULES.md §5.4.1/§5.4.2/§5.4.5/§5.6.1/§5.6.5/§5.6.6
                   and bumped to v2.3; all fifteen acceptance criteria satisfied
                   — see Completion Evidence.)
                   Lifecycle reconciliation: recorded DONE while the file remained
                   in tasks/backlog/; moved to tasks/completed/ per
                   TASK_LIFECYCLE.md §3 (direct execution verified).
Risk:              LOW–MEDIUM (TASK_TYPES.md §4 — DOCUMENTATION: "LOW for
                   corrections, MEDIUM if it affects a cross-referenced contract".
                   COMBAT_RULES.md §5.4 and §5.6 are cross-referenced across
                   combat and relic domain documentation.)
Priority:          HIGH (unblocks the formula dependency for TASK-133.)
Primary Agent:     review (TASK_TYPES.md §2 / AGENT_SELECTION.md §1 —
                   Documentation change: Primary Agent Review.)
Supporting Agents: gameplay (content accuracy — Combat domain rules owner.)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   quality/documentation-consistency,
                   quality/scope-validation
Dependencies:      TASK-138 (DONE — the recorded Product Owner decision set D1–D6;
                   immutable input to this task),
                   TASK-137 (DONE — Relic × BuffDebuff coexistence contract; not reopened),
                   TASK-136 (DONE — Relic Battle-lifetime ATK contract; not reopened),
                   TASK-119 (DONE — Pet-side BuffDebuff ATK consumption contract; not reopened),
                   TASK-128 (DONE — Boss-side ATK modifier direction contract; not reopened)
Blocks:            TASK-133 (unblocks the ATK formula composition blocker; other
                   independent runtime prerequisites remain open)
Estimate:          Simple (documentation reconciliation across COMBAT_RULES.md §5.4 and §5.6;
                   zero code, zero tests)
```

---

## Objective

Apply the recorded Product Owner decision set (D1–D6) from `TASK-138` to authoritative
documentation (`docs/01-game-design/COMBAT_RULES.md`), establishing `§5.6 / §5.6.6`
as the canonical composition model for all applicable Pet ATK modifiers (including
the BuffDebuff-only case) while preserving `§5.4` as the canonical owner of the
Turn-based `BuffDebuff` StatusEffect consumption and lifetime contract.

---

## Authoritative References

- `tasks/backlog/TASK-138-resolve-buffdebuff-atk-formula-relationship-to-combined-atk-composition.md` — The authoritative Product Owner decision set (D1–D6)
- `docs/01-game-design/COMBAT_RULES.md` §5.4, §5.4.1, §5.4.2, §5.4.5 — Turn-based `BuffDebuff` consumption contract
- `docs/01-game-design/COMBAT_RULES.md` §5.6, §5.6.1, §5.6.5, §5.6.6 — Unified Pet ATK composition contract
- `docs/01-game-design/COMBAT_RULES.md` §1.1, §3.1, §3.4 — Combat stat defaults, pipeline steps, and damage rules
- `docs/01-game-design/BOSS_RULES.md` §6.3.1 item 3 — Root debuff definition (-30% Pet ATK, 2 Turns)
- `docs/01-game-design/RELIC_RULES.md` §8.3, §8.5 item 4 — Berserker Core definition (+5% Pet ATK, Battle lifetime)
- `docs/02-technical/GAME_STATE.md` §2.3.1, §2.3.7, §5.1.1, §5.1.4 — StatusEffect and ATKModifiers state contracts
- `docs/02-technical/TDD.md` §6 — Determinism and integer arithmetic

---

## Input Decisions (TASK-138 D1–D6)

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

---

## Scope

### In Scope
- Reconcile `docs/01-game-design/COMBAT_RULES.md` §5.4.1, §5.4.2, §5.4.5, §5.6.1, §5.6.5, §5.6.6 per D1–D6.
- Update `COMBAT_RULES.md` version header to 2.3.
- Verify consistency across referencing documents (`BOSS_RULES.md`, `RELIC_RULES.md`, `GAME_RULES.md`, `GAME_STATE.md`, `TDD.md`).
- Update this task manifest to DONE with completion evidence upon completion.

### Out of Scope
- Implementing source code or tests (StatusEffectLifecycle, BattleStateService, etc.).
- Modifying TASK-133 (remains blocked on other runtime prerequisites).
- Implementing or resolving TASK-133's other independent prerequisites (`PetState.ATKModifiers[]`, `PetState.CardCostModifiers[]`, `IRelicDefinitionLookup`).
- Reopening or modifying TASK-119, TASK-128, TASK-136, or TASK-137.
- Introducing any new ATK cap or valid range.
- Adding any new state member, Redis key, SignalR protocol member, or database schema.
- Creating an ADR.

---

## Acceptance Criteria

- [x] `COMBAT_RULES.md` version header bumped to 2.3 documenting the TASK-138 reconciliation.
- [x] `COMBAT_RULES.md` §5.4.1 delegates Pet ATK composition arithmetic to §5.6 / §5.6.6 and no longer authors an independent absolute-value formula.
- [x] `COMBAT_RULES.md` §5.4 retains ownership of Turn-based BuffDebuff StatusEffect consumption and lifetime semantics.
- [x] `COMBAT_RULES.md` §5.4.2 and §5.6.6 specify single truncation toward zero after combined percentage calculation, with intermediate per-modifier truncation prohibited.
- [x] `COMBAT_RULES.md` §5.4.5 clarifies that an active ATK BuffDebuff participates in §5.6.6 unified composition.
- [x] `COMBAT_RULES.md` §5.6.1 and §5.6.6 establish that §5.6.6 governs all applicable Pet ATK modifiers, including the BuffDebuff-only case (no Relic active).
- [x] `COMBAT_RULES.md` §5.6.5 reconciles the scope statement so §5.6 does not replace §5.4's lifecycle/consumption semantics while §5.6 owns ATK composition.
- [x] `COMBAT_RULES.md` §5.6.6 documents D3 sign mapping: reduction contributes `-|Magnitude|`, increase contributes `+|Magnitude|`, stored Magnitude does not encode sign.
- [x] `COMBAT_RULES.md` §5.6.6 documents D4 single unified calculation path; no parallel derived ATK representations.
- [x] `COMBAT_RULES.md` §5.6.4 and §5.6.6 preserve base `PetState.ATK` as permanent and never mutated/reset.
- [x] `COMBAT_RULES.md` §5.6.1 item 5 no-cap rule remains preserved.
- [x] Worked examples verify:
      - Relic + BuffDebuff: 50 + (+5%) + (-30%) = 37
      - BuffDebuff-only: 50 + (-30%) = 35
      - Relic-only: 50 + (+5%) = 52
      - Multi-modifier divergence: 50 + (-30%) + (-10%) = 30 (diverges from sequential truncation 31).
- [x] Cross-document consistency verified across `BOSS_RULES.md`, `RELIC_RULES.md`, `GAME_STATE.md`, `GAME_RULES.md`, and `TDD.md`.
- [x] No source code or tests modified.
- [x] No new state member, Redis key, SignalR protocol member, DB schema, or ADR created.

---

## Affected Files & Areas

```text
[ ] src/backend/ (none)
[ ] src/frontend/client/ (none)
[ ] tests/ (none)
[x] docs/01-game-design/COMBAT_RULES.md (reconciled §5.4 and §5.6; bumped to v2.3)
[x] tasks/backlog/TASK-139-apply-pet-atk-modifier-composition-contract.md (this file)
```

---

## Completion Evidence

### Decision Source
TASK-138 Product Owner D1–D6

### Canonical Owner
`docs/01-game-design/COMBAT_RULES.md` §5.6 / §5.6.6

### Lifecycle Owner Preserved
`docs/01-game-design/COMBAT_RULES.md` §5.4

### Resolved Relationship
`§5.6.6` is the canonical Pet ATK composition model for all applicable Pet ATK
modifiers, including BuffDebuff-only (TASK-138 D1, D2). `§5.4.1`'s historical
absolute-value ATK composition formula is superseded/narrowed and no longer
defines a separate calculation path (D2, D4).

### BuffDebuff Sign Mapping
Reduction contributes ` -|Magnitude| ` signed percentage points; increase contributes
` +|Magnitude| ` signed percentage points (TASK-138 D3). Stored `StatusEffect.Magnitude`
remains the applied magnitude and does not encode sign (`GAME_STATE.md` §2.3.1 item 2).

### Calculation
One unified signed composition calculation produces `EffectivePetATK` (TASK-138 D4).
The combined signed percentage is applied to permanent base `PetState.ATK` and
truncated toward zero exactly once after the combined calculation; no intermediate
per-modifier truncation occurs (TASK-138 D5).

### Base Stat
`PetState.ATK` remains permanent/base and is never overwritten, mutated, restored,
or reset by modifier resolution (TASK-138 D6). No `DefaultATK` runtime reset exists.
`EffectivePetATK` is derived at calculation time and is never stored as a second representation.

### ATK Cap
No new cap introduced; existing `COMBAT_RULES.md` §5.6.1 item 5 and §1.1 no-cap rule preserved.

### TASK-133 Dependency
Formula blocker resolved, but independent runtime-carrier (`PetState.ATKModifiers[]`,
`PetState.CardCostModifiers[]`) and Relic-definition-lookup (`IRelicDefinitionLookup`) blockers
remain open.

### Changed Files
- `docs/01-game-design/COMBAT_RULES.md` — Reconciled §5.4.1, §5.4.2, §5.4.4, §5.4.5, §5.5.1, §5.6, §5.6.1, §5.6.5, §5.6.6; bumped to Version 2.3.
- `tasks/backlog/TASK-139-apply-pet-atk-modifier-composition-contract.md` — Created and marked DONE.

### Validation Results
- Contract validation: PASS (unified model covers BuffDebuff-only, Relic-only, Relic + BuffDebuff, multi-modifier)
- Arithmetic validation: PASS (50-30%=35; 50+5%=52; 50+5%-30%=37; 50-30%-10%=30)
- Scope validation: PASS (§5.4 = lifecycle/consumption; §5.6 = Pet ATK composition; zero duplicate/conflicting formulas)
- Authority validation: PASS (zero code modified, zero tests modified, zero new state members, zero new ADRs)
- Cross-document validation: PASS (BOSS_RULES.md, RELIC_RULES.md, GAME_STATE.md, GAME_RULES.md, TDD.md verified)
