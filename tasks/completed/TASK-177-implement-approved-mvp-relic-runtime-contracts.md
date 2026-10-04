# TASK-177 — Implement Approved MVP Relic Runtime Contracts

<!--
  GEN-TASK EXECUTION MANIFEST — IMPLEMENTATION TASK
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and src/ by path and section — it does NOT copy
  game rules, formulas, magnitudes, schemas, or contracts as new authority.
  Every gameplay value below is TRANSCRIBED from its canonical owner and is
  cited; the task authors no gameplay rule of its own.

  SCOPE: runtime/domain implementation only.
  It carries NO migration, NO seed, NO HasData, NO INSERT, NO schema change,
  NO database provisioning, and NO frontend change.
-->

---

## Metadata

```text
Task ID:           TASK-177
Type:              Implementation
Status:            DONE
Risk:              MEDIUM (extends the landed Relic resolution stage across four
                   additional Trigger firing points and one new effect identity;
                   the four provisioned Relics must remain regression-safe)
Priority:          High
Primary Agent:     backend
Supporting Agents: gameplay (RELIC_RULES.md / COMBAT_RULES.md reading only —
                   no gameplay value is authored or altered by this task),
                   review (review posture on the regression surface)
Workflow:          core/implementation.md
Skills:            discovery/impact-analysis,
                   discovery/documentation-discovery,
                   gameplay/gameplay-behavior-derivation,
                   quality/scope-validation,
                   quality/review
Dependencies:      TASK-176 (DONE — the source of every approved decision this
                     task implements; it is the authority for all six new Relic
                     contracts and for the BurnDamage / ATK+NextAttack
                     vocabulary extensions),
                   TASK-178 (DONE — canonicalized the Product Owner decisions
                     Q-1…Q-4 that this task reported as runtime blockers; see
                     "Open Questions / Blockers — RESOLVED". It supersedes
                     nothing here: TASK-176 remains the gameplay-content
                     authority and this task remains the implementation task),
                   TASK-131 (DONE — structured Trigger/Condition/Effect contract),
                   TASK-132 (DONE — jsonb structured column migration),
                   TASK-133 (DONE — landed server-authoritative Relic trigger and
                     effect resolution; GAME_RULES.md §17 step 11),
                   TASK-137 (DONE — Relic ATK modifier composition contract),
                   TASK-138 (DONE — unified Pet ATK modifier composition),
                   TASK-139 (DONE — TASK-138 contract application),
                   TASK-140 (DONE — Relic ATK runtime carrier),
                   TASK-142 (DONE — Relic ATK modifier carrier runtime)
Blocks:            the downstream database-provisioning task that provisions the
                   six new RelicDefinition rows (NOT this task — see "Database
                   Boundary"),
                   the downstream Relic verification/regression suite task
                   (TASK-179 per TASK-176's Blocks list)
Estimate:          Large (six Relic runtime contracts, one new Effect identity,
                   one lifetime extension, four new Trigger firing points,
                   recursion safety, focused tests + regression tests)
```

---

## Objective

Implement runtime/domain support for the six Relics approved by **TASK-176**,
through the repository's **existing** Relic architecture — no second engine, no
per-Relic branch, no hard-coded gameplay value.

The six newly approved Relics:

```text
relic-burning-curse
relic-combo-fang
relic-arcane-battery
relic-execution-mark
relic-cascade-core
relic-battle-instinct
```

The four already provisioned Relics must remain **regression-safe**:

```text
relic-berserker-core
relic-mana-crystal
relic-assassin-eye
relic-emergency-core
```

**TASK-176 is the source of approved decisions for this task.** Every Trigger,
Condition, effect, magnitude, threshold, target, lifetime, and stacking value
this task implements is transcribed from TASK-176's approved decision record and
its canonical application in `docs/01-game-design/RELIC_RULES.md` §6 and §8. This
task **authors no gameplay rule**. `RELIC_RULES.md` remains the canonical owner.

---

## Authoritative References

Read before implementing (`AGENTS.md` §6 — Card/Relic row, plus the combat row
for the Burn and Damage-Taken surfaces):

- `AGENTS.md` §6, §7, §8, §9, §10, §11, §12, §14, §15, §16, §17, §18, §20, §22
- `docs/01-game-design/RELIC_RULES.md` §1 — Relic structure
- `docs/01-game-design/RELIC_RULES.md` §2.2, §2.3 — equipped-instance identity and
  equip-slot order (the resolution order this task must preserve)
- `docs/01-game-design/RELIC_RULES.md` §3 — **closed** Trigger vocabulary (12 values;
  this task adds no value to it)
- `docs/01-game-design/RELIC_RULES.md` §4 — deterministic trigger order and the
  breadth-first chain queue
- `docs/01-game-design/RELIC_RULES.md` §5 — **anti-infinite-chain rule** (the
  ownership of every recursion-safety criterion in this task)
- `docs/01-game-design/RELIC_RULES.md` §6 — MVP Relic Reference (the 10 rows)
  and notes 1–4
- `docs/01-game-design/RELIC_RULES.md` §7 — `RelicTriggered` event contract
- `docs/01-game-design/RELIC_RULES.md` §8.1 — structured `Condition` vocabulary
  and the step-11 observation point (item 8)
- `docs/01-game-design/RELIC_RULES.md` §8.2 — structured `EffectDefinition[]`
  vocabulary (`effectType` | `valueType` | `value` | `target` | `lifetime`)
- `docs/01-game-design/RELIC_RULES.md` §8.3 — **allowed target/lifetime/valueType
  combinations table** (the table this task must widen to the two rows TASK-176
  added — it does not invent rows)
- `docs/01-game-design/RELIC_RULES.md` §8.4 — effect lifetime vs. trigger
  re-evaluation independence
- `docs/01-game-design/RELIC_RULES.md` §8.5 — canonical Relic contract table,
  items 1–10
- `docs/01-game-design/RELIC_RULES.md` §8.6 — storage consequence (a
  *downstream* task's act, not this one)
- `docs/01-game-design/GAME_RULES.md` §12 — Power Rules (0–100 range)
- `docs/01-game-design/GAME_RULES.md` §16 — event list
- `docs/01-game-design/GAME_RULES.md` §17 — **fixed event resolution order**;
  step 11 ("Trigger Relics"), step 18c (`DamageTaken`, the `OnDamageTaken`
  firing point), step 19a (the Burn/DoT tick), and the battle-start point
- `docs/01-game-design/COMBAT_RULES.md` §2 — resource generation (the Power-gain
  surface)
- `docs/01-game-design/COMBAT_RULES.md` §3, §3.1 — Damage Pipeline and its fixed
  order
- `docs/01-game-design/COMBAT_RULES.md` §3.3 items 7–11 — **`EffectiveCrit`
  composition, its cap, the `NextAttack` lifetime, the qualifying-attack
  consumption boundary, and source-specific removal** (canonical owner; this task
  references it and must not restate or alter it)
- `docs/01-game-design/COMBAT_RULES.md` §5.1, §5.2 — Burn as a `DoT` Status
  Effect and the refresh-not-stack default (note §5.2 item 2's explicit mention
  of Burning Curse)
- `docs/01-game-design/COMBAT_RULES.md` §5.3 — duration consumption timing
- `docs/01-game-design/COMBAT_RULES.md` §5.6, §5.6.6 — **Pet ATK modifier
  composition** (`TotalATKModifierPercentage`, `EffectivePetATK`, single
  truncation; canonical owner)
- `docs/02-technical/GAME_STATE.md` §0 item 5 — no parallel representation
- `docs/02-technical/GAME_STATE.md` §2.3.4 and §5.1.2 — `NextAttackCritModifiers[]`
  carrier and its mutation lifecycle
- `docs/02-technical/GAME_STATE.md` §2.3.5 and §5.1.3 — `CardCostModifiers[]`
  carrier and its lifecycle
- `docs/02-technical/GAME_STATE.md` §2.3.7 and §5.1.4 — `ATKModifiers[]` carrier
  and its lifecycle
- `docs/02-technical/GAME_STATE.md` §5.1 — the single post-resolution write-back
- `docs/02-technical/GAME_EVENTS.md` §2 — `RelicTriggered`, `PowerChanged`
- `tasks/completed/TASK-176-collect-product-owner-decisions-for-mvp-relic-content.md`
  — **the approved decision record this task implements** (Decisions 1–6 and the
  Technical Impact Classification Matrix)
- `tasks/completed/TASK-131`, `TASK-132`, `TASK-133`, `TASK-137`, `TASK-138`,
  `TASK-139`, `TASK-140`, `TASK-142` — the historical implementation chain of the
  existing Relic architecture this task extends

Do not broadly inspect unrelated systems (`AGENTS.md` §6, §16).

---

## Existing Implementation — Components TASK-177 Must Modify

Identified by inspection. These are the **only** runtime components in scope.

| Component | Path | Current state (relevant to this task) |
| --- | --- | --- |
| Relic resolution stage | `src/backend/GameServer.Domain/Relics/RelicResolver.cs` | Implements `GAME_RULES.md` §17 step 11. Handles **three** Triggers — `OnMatchCount`, `OnCombo`, `OnHpBelow` (`IsEvaluatedByStep11`). Applies **four** `effectType` values — `ATK`, `Power`, `Crit`, `CardCost`. Has a per-instance `handledSources` safeguard. |
| Effect identity set | `src/backend/GameServer.Domain/Relics/RelicEffectType.cs` | Closed set `ATK = 0`, `Power = 1`, `Crit = 2`, `CardCost = 3`. |
| Effect element (combination validation) | `src/backend/GameServer.Domain/Relics/RelicEffectDefinition.cs` | `RequiredValueTypeFor` / `RequiredLifetimeFor` encode §8.3's table one lifetime per `effectType`. `ATK` is **hard-mapped to `Battle`**; no `BurnDamage` member exists. |
| Effect array wrapper | `src/backend/GameServer.Domain/Relics/RelicEffectDefinitions.cs` | Array read/write, validates all four members against the closed sets. |
| Structured contract DTO | `src/backend/GameServer.Domain/Relics/RelicDefinition.cs` | `Trigger` (string), `Condition` (`RelicCondition?`), `EffectDefinition` (`RelicEffectDefinitions`). |
| Condition | `src/backend/GameServer.Domain/Relics/RelicCondition.cs`, `RelicConditionType.cs` | Three forms: `MatchCountAtLeast`, `ComboAtLeast`, `HpPercentageBelow`. |
| Resolution result | `src/backend/GameServer.Domain/Relics/RelicResolutionResult.cs` | Resolved `PetState` + ordered `RelicTriggeredEvent`s + `PowerChangedEvent`s. |
| Relic events | `src/backend/GameServer.Domain/Relics/RelicEvents.cs` | `RelicTriggeredEvent` (identity only). |
| Equipped content | `src/backend/GameServer.Domain/Relics/EquippedRelicContent.cs`, `EquippedRelicIdentity.cs` | Per-instance identity + resolved definition. |
| Application orchestration (step 11 call site) | `src/backend/GameServer.Application/Battle/BattleStateService.cs` (~line 1218–1261) | Builds `EquippedRelicContent[]` from `PetState.EquippedRelics[]` + `_relicConfiguration`, calls `RelicResolver.Resolve`, merges the result, emits `RelicTriggered` + `PowerChanged`. |
| Power write site | `src/backend/GameServer.Domain/Match3/ResourceGenerator.cs` (`ApplyPower`) | The single clamp point for `GAME_RULES.md` §12's 0–100 range; called from the step-13 site in `BattleStateService.cs` (~line 1267). |
| Burn / DoT model | `src/backend/GameServer.Domain/Battle/StatusEffect.cs`, `StatusEffectType.cs` | `DoT` type, `Id = "Burn"`, `Id = "Root"`; magnitude is the per-tick damage. |
| Burn tick site | `src/backend/GameServer.Application/Battle/BattleStateService.cs` (~line 2016–2170) | Step 19a — ticks Boss-side and Pet-side `DoT` instances through `DamagePipeline.Calculate`. No Relic modifier participates today. |
| Crit composition | `src/backend/GameServer.Domain/Combat/DamagePipeline.cs` | Reads `AttackerCrit`; `PetState.NextAttackCritModifiers[]` consumption is Application-side (~line 1384). |
| Damage-taken site | `src/backend/GameServer.Application/Battle/BattleStateService.cs` (~lines 1399, 1812, 2090, 2162) | `BattleEvent.ForDamageTaken` emission sites. |
| Existing tests | `tests/backend/GameServer.Domain.Tests/RelicResolverTests.cs`, `RelicStructuredContractTests.cs`, `Application.Tests/RelicStageResolutionTests.cs`, `Infrastructure.Tests/RelicStructuredStorageTests.cs`, `Api.Tests/RelicWireProjectionTests.cs`, `Application.Tests/Battle/CardCostRelicIntegrationTests.cs` | The regression surface. |

**The identified gaps this task must close:**

```text
1. RelicResolver.IsEvaluatedByStep11 handles only OnMatchCount | OnCombo | OnHpBelow.
   → OnBattleStart, OnCascade, OnPowerGain, OnDamageTaken are not evaluated anywhere.
2. RelicEffectType has no BurnDamage member (§8.2 item 1 now defines five).
3. RelicEffectDefinition.RequiredLifetimeFor maps ATK → Battle only, so the
   §8.3 row "ATK | Pet | NextAttack | Percentage" that TASK-176 added is not
   representable at read time.
4. No runtime carrier or integration exists for a NextAttack-lifetime ATK
   modifier (Battle Instinct).
5. No Relic modifier participates in the Burn damage calculation (Burning Curse).
```

---

## Approved Gameplay Contract (transcribed from TASK-176 / `RELIC_RULES.md`)

> Every value below is transcribed from TASK-176's approved decisions and
> `RELIC_RULES.md` §6 / §8.5. **This task authors none of them.** If any value
> here disagrees with `RELIC_RULES.md`, `RELIC_RULES.md` wins (`AGENTS.md` §2) —
> stop and report per `AGENTS.md` §4.

### Burning Curse

```text
RelicId: relic-burning-curse
Trigger: OnBattleStart
Condition: NONE
Effect: BurnDamage
Magnitude: 30
ValueType: Percentage
Target: Pet
Lifetime: Battle
Stacking: Non-stacking
Reset/Re-fire: Once per battle
```

### Combo Fang

```text
RelicId: relic-combo-fang
Trigger: OnCombo
Condition: ComboAtLeast(5)
Effect: Crit
Magnitude: 20
ValueType: PercentagePoints
Target: Pet
Lifetime: NextAttack
Stacking: Non-stacking
```

### Arcane Battery

```text
RelicId: relic-arcane-battery
Trigger: OnPowerGain
Condition: NONE
Effect: Power
Magnitude: 5
ValueType: Flat
Target: Pet
Lifetime: Immediate
```

Each qualifying Power gain resolves independently.

**The task MUST explicitly require protection against recursive
self-triggering:**

```text
OnPowerGain
→ Arcane Battery +5 Power
→ must NOT trigger Arcane Battery again
```

### Execution Mark

```text
RelicId: relic-execution-mark
Trigger: OnHpBelow
Condition: HpPercentageBelow(30)
Effect: Crit
Magnitude: 15
ValueType: PercentagePoints
Target: Pet
Lifetime: NextAttack
```

**Use the existing canonical/runtime `OnHpBelow` semantics. Do not redefine
`OnHpBelow` globally.**

**Do not alter Emergency Core semantics.**

### Cascade Core

```text
RelicId: relic-cascade-core
Trigger: OnCascade
Condition: NONE
Effect: Power
Magnitude: 5
ValueType: Flat
Target: Pet
Lifetime: Immediate
```

Each qualifying cascade resolves independently.

Generated Power must not recursively create another Cascade Core activation.

### Battle Instinct

```text
RelicId: relic-battle-instinct
Trigger: OnDamageTaken
Condition: NONE
Effect: ATK
Magnitude: 10
ValueType: Percentage
Target: Pet
Lifetime: NextAttack
Stacking: Non-stacking
```

---

## New Effect Vocabulary — The Only New Effect

The only new Effect approved by TASK-176 is:

```text
BurnDamage
```

Runtime support is required for exactly this combination:

```text
BurnDamage + Percentage + Pet + Battle
```

**Do NOT introduce additional Effect types.** The `effectType` set after this
task is exactly the five `RELIC_RULES.md` §8.2 item 1 now defines:

```text
ATK | Power | Crit | CardCost | BurnDamage
```

No sixth member, no alias, no alias-of-an-alias for an existing Card effect.

---

## ATK `NextAttack` Lifetime Extension

TASK-176 explicitly expanded the canonical Relic vocabulary so that

```text
ATK + Percentage + NextAttack
```

is valid (`RELIC_RULES.md` §8.3's table now carries both the `Battle` and the
`NextAttack` row for `ATK`).

TASK-177 must therefore **inspect and, if necessary, extend the existing generic
modifier/lifetime pipeline** to support this combination. Concretely, the
implementer must:

1. Widen `RelicEffectDefinition.RequiredLifetimeFor` (and its counterpart
   validation) so `ATK` accepts **both** lifetimes §8.3's table lists —
   `Battle` and `NextAttack` — while `Power`, `Crit`, and `BurnDamage` keep the
   single lifetime their own row fixes, and while `CardCost` keeps `Battle`.
2. Determine where a `NextAttack`-lifetime ATK modifier is carried in battle
   state. The `NextAttack` lifetime boundary is
   `COMBAT_RULES.md` §3.3 item 8's; the composition is §5.6/§5.6.6's. The
   carrier decision must follow the existing `NextAttackCritModifiers[]`
   precedent (`GAME_STATE.md` §2.3.4 / §5.1.2, `ADR-017`) and must not create a
   third temporary-modifier representation for the same lifetime concept.
3. Reach the lifetime's consumption at the **same** qualifying-attack boundary
   the Crit modifier uses — one shared consumption rule, not a second one.

**Do NOT create a Battle Instinct-specific special-case attack calculation.**
Battle Instinct must flow through the same generic modifier pipeline Berserker
Core uses, differing only in its declared `lifetime` and its Trigger.

---

## Runtime Scope

The task covers, in the existing architecture:

- Relic trigger resolution — including the four Trigger firing points that do
  not exist yet (`OnBattleStart`, `OnCascade`, `OnPowerGain`, `OnDamageTaken`),
  each at the firing point `RELIC_RULES.md` §3 assigns it.
- condition resolution where necessary (`ComboAtLeast(5)`, `HpPercentageBelow(30)`
  — both existing forms; no new form).
- effect resolution (the five-member `effectType` set).
- modifier/lifetime handling (`Battle` and `NextAttack` on the existing carriers).
- Burn damage integration (Burning Curse's `+30%` reaching the Burn/DoT tick).
- ATK `NextAttack` integration (Battle Instinct).
- Power gain integration (Arcane Battery's `OnPowerGain` and Cascade Core's
  `OnCascade`).
- Cascade integration (Cascade Core, per Cascade iteration).
- Damage Taken integration (Battle Instinct).
- Battle Start integration (Burning Curse).
- recursion/re-entry safety (see "Recursion Safety").
- focused automated tests for all six Relics.
- regression tests for the four existing Relics.

**Implementation MUST go through the existing Relic architecture.** The
`RelicDefinition` / `RelicResolver` / structured-`EffectDefinition` pipeline that
TASK-131, TASK-132, TASK-133, and TASK-140/142 landed is the only path. The
runtime stays **data-driven**: every decision is read from the Relic's stored
`Trigger`, `Condition`, and `EffectDefinition[]`.

---

## Architecture Constraints

This task **MUST NOT**:

```text
hard-code Relic IDs in unrelated combat logic
hard-code gameplay values (magnitudes, thresholds, percentages, costs)
create a second/parallel Relic engine
create a new static/no-event Trigger
globally reinterpret OnHpBelow
globally reinterpret NoReset / Persistent
add a new Redis schema
add new database columns
make frontend changes
create migrations
perform database provisioning
```

Additionally:

- Do not add a Trigger value to `RELIC_RULES.md` §3's closed list. The four new
  firing points are **existing** §3 values, not new ones.
- Do not restate or modify `COMBAT_RULES.md` §3.3 items 7–11 or §5.6/§5.6.6;
  reference them.
- Do not add a `BattleState`/`PetState` member merely because it is convenient —
  if a carrier is genuinely required, follow the existing two-member
  `SourceIdentity` + value element schema (§2.3.4/§2.3.5/§2.3.7) and record the
  decision; if the carrier question is not answerable from the documents, that is
  the blocker described in "Open Questions / Blockers".

Runtime must remain **data-driven through the existing `RelicDefinition` /
resolver architecture.**

---

## Recursion Safety

`RELIC_RULES.md` §5 (anti-infinite-chain) owns this. The task must contain
explicit, tested acceptance criteria for:

### Arcane Battery

```text
Power gain
→ Arcane Battery
→ +5 Power
→ no recursive Arcane Battery trigger
```

The `+5` Power that Arcane Battery itself grants must not itself be a qualifying
`OnPowerGain` event for the same Relic instance within the same root event.

### Cascade Core

```text
Cascade
→ Cascade Core
→ +5 Power
→ no fake/recursive Cascade event
```

Granting Power must not synthesize a Cascade iteration, and must not make
Cascade Core eligible again within the same root event.

### Burning Curse

```text
Burn calculation
→ BurnDamage modifier
→ modified damage only
→ no recursive Burn event
```

The `BurnDamage` modifier changes the damage the Burn tick produces; it must not
emit, create, or re-enter a Burn event or a second Burn instance.

---

## Testing Requirements

TASK-177 requires **focused tests for all six Relics**, plus regression coverage
for the four existing MVP Relics. Tests derive from the owning rule
(`AGENTS.md` §15), use the existing scenario style (Given/When/Then), and live
alongside the existing Relic test suites listed above.

### Burning Curse

- Battle Start trigger fires.
- `+30%` Burn damage is applied to the Burn tick.
- `Battle` lifetime — the modifier persists for the remainder of the battle.
- Non-stacking — repeated/duplicate evaluation does not accumulate a second entry.
- Existing Burn behavior **without** the Relic is unchanged.

### Combo Fang

- `Combo < 5` → does not trigger.
- `Combo >= 5` → triggers `+20` Crit (percentage points).
- `NextAttack` consumption at the qualifying attack.
- Non-stacking / re-trigger behavior after consumption.

### Arcane Battery

- A Power gain triggers `+5` Power.
- Multiple independent gains each resolve.
- **Recursion prevention** — the Power it grants does not re-trigger it.

### Execution Mark

- Exercises the **existing** `OnHpBelow` semantics (not a new reading).
- Threshold boundary below/at `30%`.
- `+15` Crit (percentage points).
- `NextAttack` consumption.
- **Emergency Core regression** — the same `OnHpBelow` /
  `HpPercentageBelow(30)` surface behaves exactly as before.

### Cascade Core

- Each cascade iteration triggers `+5` Power.
- Multiple cascades resolve independently.
- Recursion safety — no fake/recursive Cascade event.

### Battle Instinct

- Damage taken triggers `+10%` ATK.
- Reaches the damage calculation through the **generic** ATK modifier pipeline
  (no Battle Instinct-specific calculation).
- `NextAttack` consumption at the qualifying attack.
- Non-stacking / re-trigger behavior.

### Regression Coverage — Existing Four Relics

- `relic-berserker-core` — `OnMatchCount` / `MatchCountAtLeast(3)` / `+5%` ATK
  (`Battle`).
- `relic-mana-crystal` — `OnMatchCount` / `MatchCountAtLeast(4)` / `+10` Power
  (`Immediate`).
- `relic-assassin-eye` — `OnCombo` / `ComboAtLeast(3)` / `+10pp` Crit
  (`NextAttack`), including consumption.
- `relic-emergency-core` — `OnHpBelow` / `HpPercentageBelow(30)` / `-50%` Card
  cost (`Battle`), including continuous re-evaluation and reversion.

The existing suites (`RelicResolverTests`, `RelicStructuredContractTests`,
`RelicStageResolutionTests`, `RelicStructuredStorageTests`,
`RelicWireProjectionTests`, `CardCostRelicIntegrationTests`) must be kept green.
Do not modify a test merely to make an incorrect implementation pass
(`AGENTS.md` §15).

---

## Database Boundary

> **TASK-177 does NOT provision the six new `RelicDefinition` rows.**

Database provisioning belongs to a downstream task (TASK-176's Blocks list names
it as TASK-178). TASK-177 contains:

```text
NO migration
NO seed
NO HasData
NO INSERT
NO database schema change
```

The six Relics are **content-defined** (`RELIC_RULES.md` §6 note 4) and become
reachable at runtime only once the downstream provisioning task lands. TASK-177's
tests must therefore exercise the runtime contracts through in-memory
`RelicDefinition` construction (the pattern the existing
`RelicProvisionedDefinitions.cs` / `TestRelicEffects.cs` helpers already use), not
through provisioned rows.

---

## Acceptance Criteria

Each AC is objectively verifiable.

### AC-1 — Burning Curse runtime

`OnBattleStart` is evaluated at the §3-assigned battle-start firing point; a
Burning Curse definition with `Condition = null` and
`{ "effectType": "BurnDamage", "valueType": "Percentage", "value": 30, "target": "Pet", "lifetime": "Battle" }`
applies a standing `+30%` Burn damage modifier with `Battle` lifetime, fires once
per battle, and does not stack a second entry on re-evaluation.

### AC-2 — Combo Fang runtime

`OnCombo` with `ComboAtLeast(5)` applies `+20` percentage points of Crit with
`NextAttack` lifetime. Combo `< 5` applies nothing. The modifier is consumed by
the next qualifying attack per the existing boundary, and re-triggers when the
condition is met again.

### AC-3 — Arcane Battery runtime

`OnPowerGain` with `Condition = null` applies `+5` flat Power with `Immediate`
lifetime. Each qualifying Power gain resolves independently.

### AC-4 — Execution Mark runtime

`OnHpBelow` with `HpPercentageBelow(30)` applies `+15` percentage points of Crit
with `NextAttack` lifetime, using the **existing** runtime `OnHpBelow` semantics
with no global redefinition. Emergency Core's behavior on the same trigger and
the same condition is unchanged.

### AC-5 — Cascade Core runtime

`OnCascade` with `Condition = null` applies `+5` flat Power with `Immediate`
lifetime on each qualifying cascade iteration; multiple cascades resolve
independently.

### AC-6 — Battle Instinct runtime

`OnDamageTaken` with `Condition = null` applies a `+10%` ATK modifier with
`NextAttack` lifetime, reaching the damage calculation through the generic ATK
modifier composition (`COMBAT_RULES.md` §5.6/§5.6.6) with no Battle
Instinct-specific calculation path, and is consumed at the next qualifying owner
attack.

### AC-7 — `BurnDamage` effect support

`BurnDamage` exists as a Relic `effectType` accepting exactly
`valueType: Percentage`, `target: Pet`, `lifetime: Battle`, and the
`BurnDamage + 30%` modifier measurably changes the Burn/DoT tick damage. No
additional `effectType` is introduced: the set is exactly
`ATK | Power | Crit | CardCost | BurnDamage`.

### AC-8 — `ATK` + `NextAttack` support

A Relic effect declaring `ATK` with `lifetime: NextAttack` is accepted by the
structured effect reader/writer (validation no longer forces `Battle`), and the
resulting modifier is carried and consumed through the **existing generic**
modifier/lifetime pipeline. `ATK` + `Battle` (Berserker Core) still works
unchanged, and `Power` / `Crit` / `CardCost` / `BurnDamage` keep the single
lifetime their §8.3 row fixes.

### AC-9 — Recursion / re-entry safety

Confirmed by test for each of the three documented loops:

```text
Arcane Battery: Power gain → +5 Power → no recursive Arcane Battery trigger
Cascade Core:   Cascade → +5 Power → no fake/recursive Cascade event
Burning Curse:  Burn calculation → BurnDamage modifier → modified damage only,
                no recursive Burn event
```

Each Relic fires at most once per root event for its own output
(`RELIC_RULES.md` §5 item 2), and the §4.3 breadth-first queue semantics are
preserved.

### AC-10 — Existing four Relics regression safety

`relic-berserker-core`, `relic-mana-crystal`, `relic-assassin-eye`, and
`relic-emergency-core` produce byte-identical behavior and unchanged
`RelicTriggered` / `PowerChanged` output. Their existing tests pass unmodified.

### AC-11 — No gameplay hard-coding

No Relic ID, `RelicDefinition.Name`, display string, magnitude, threshold, or
percentage appears in unrelated combat logic; the resolver reads only declared
content. Verified by inspection and by the code-review pass (`RELIC_RULES.md`
§8.2 item 1).

### AC-12 — No DB changes

The changeset contains no migration, no seed, no `HasData`, no `INSERT`, no
schema change, and no database provisioning. `git status`/diff shows only
`src/backend/GameServer.Domain/**` (and the minimum necessary
`src/backend/GameServer.Application/**` call-site wiring) plus `tests/**`.

### AC-13 — Focused tests

Every coverage bullet in "Testing Requirements" for the six new Relics exists as
an automated test and passes.

### AC-14 — Required backend test suite

The full backend suite passes:

```text
tests/backend/GameServer.Domain.Tests
tests/backend/GameServer.Application.Tests
tests/backend/GameServer.Infrastructure.Tests
tests/backend/GameServer.Api.Tests
```

with zero pre-existing tests modified to accommodate the new behavior.

### AC-15 — Task lifecycle evidence

Completion evidence is recorded in the task file per repository convention:
changed files, the exact test command run, the observed result, and the
`AGENTS.md` §22 Definition-of-Done checklist. Documentation impact is recorded:
if any implementation detail diverges from `RELIC_RULES.md` §8 or
`COMBAT_RULES.md`, that is an `AGENTS.md` §4 conflict to report and **stop**, not
to resolve silently.

---

## Prohibited Actions

```text
Do NOT modify canonical gameplay documentation in this task.
Do NOT create additional gameplay content.
Do NOT create migrations, seeds, or database rows.
Do NOT change Redis schema or database columns.
Do NOT change the frontend.
Do NOT add a new Trigger to RELIC_RULES.md §3.
Do NOT globally reinterpret OnHpBelow.
Do NOT globally reinterpret NoReset / Persistent.
Do NOT create a second Relic engine.
Do NOT decide an ambiguity TASK-176 did not resolve.
```

---

## Open Questions / Blockers — RESOLVED by TASK-178

> **Status: no open blockers.** The four runtime ambiguities Q-1…Q-4 this task
> reported were escalated to the Product Owner and **answered**; **TASK-178**
> (DONE) canonicalized the answers into the owning documents. The decision record
> below is a **pointer**, not new authority: the canonical rule is the cited
> document section, and if anything below disagrees with it, the document wins
> (`AGENTS.md` §2) — stop and report per `AGENTS.md` §4.
>
> The implementer must **not** re-decide any of these, and must **not** read the
> summaries below as a substitute for the cited sections.

```text
Q-1 = A   RESOLVED — docs/02-technical/GAME_STATE.md §2.3.7 item 11, §2.3.8
          item 3, §5.1.4 (items 1–4); docs/01-game-design/RELIC_RULES.md
          §8.3 item 4, §8.5 item 10; docs/01-game-design/COMBAT_RULES.md
          §3.3 item 8.

Q-2 = B   RESOLVED — docs/01-game-design/RELIC_RULES.md §3.1 and §8.5 item 7;
          §5 (the operative-boundaries paragraph).

Q-3 = A   RESOLVED — docs/01-game-design/RELIC_RULES.md §3.2 and §8.5 item 9;
          §5 (the operative-boundaries paragraph); iteration identity remains
          docs/01-game-design/MATCH3_RULES.md §4.2/§4.3.

Q-4 = C   RESOLVED — docs/01-game-design/RELIC_RULES.md §6 note 1 and §8.5
          item 5; docs/01-game-design/COMBAT_RULES.md §5.2 item 4.
```

### Q-1 — RESOLVED (Option A): the `NextAttack` ATK carrier

**Answered A — extend `ATKModifiers[]`.** A `NextAttack`-lifetime ATK modifier
is carried by the **existing** `PetState.ATKModifiers[]`, whose elements carry
their declared `lifetime` (`Battle` | `NextAttack`). **`NextAttackATKModifiers[]`
and a generic `NextAttackModifiers[]` are both explicitly NOT introduced.**

Consequences this task must implement as **AC-6/AC-8**:

- widen the structured effect reader/writer so `ATK` accepts **both** lifetimes
  `RELIC_RULES.md` §8.3's table lists (`Battle` and `NextAttack`), while `Power`,
  `Crit`, and `BurnDamage` keep the single lifetime their own row fixes and
  `CardCost` keeps `Battle`;
- carry the modifier on `PetState.ATKModifiers[]` per `GAME_STATE.md` §2.3.7
  (element: `SourceIdentity` + `ATKModifierPercentage` + `Lifetime`), whose
  serialized round trip must preserve `lifetime` (§2.3.8 item 3/5);
- **consume it at the same qualifying-attack boundary the Crit modifier uses** —
  `COMBAT_RULES.md` §3.3 items 7–11 is the single owner; `GAME_STATE.md` §5.1.4
  item 4 records the mutation. **One shared consumption rule, not a second one.**
- a `NextAttack` ATK modifier applies to the next qualifying Pet attack, is
  consumed when that attack resolves, is **never** added to `PetState.ATK`, does
  **not** remain active for later attacks, and is non-stacking (a re-trigger by
  the same source refreshes its one element rather than accumulating);
- Battle Instinct flows through the **generic** pipeline Berserker Core uses,
  differing only in its declared `lifetime` and its Trigger — no
  Battle-Instinct-specific attack calculation.

### Q-2 — RESOLVED (Option B): the qualifying `OnPowerGain` surface

**Answered B — non-Relic-generated Power gains only.** `OnPowerGain` reacts to
qualifying Power gains originating **outside** Relic effect resolution. Power
granted by a Relic effect — Arcane Battery's `+5`, Cascade Core's `+5`, Mana
Crystal's `+10` — is **not** a qualifying gain, so the recursion
`OnPowerGain → Arcane Battery → +5 Power → OnPowerGain → …` is forbidden.

`RELIC_RULES.md` §3.1 owns the rule; §8.5 item 7 records it for Arcane Battery.
**No new Trigger, Condition, Power source, or event type is introduced.** The
exclusion is a property of the gain's origin, so it applies to the observing
Relic and to any other `OnPowerGain` Relic instance alike.

### Q-3 — RESOLVED (Option A): the `OnCascade` event boundary

**Answered A — each cascade iteration is an independent `OnCascade` event.**
A Swap's cascades are **not** collapsed into one aggregated event, so Cascade
Core resolves once per qualifying iteration (`Cascade #1 → +5`, `Cascade #2 →
+5`, `Cascade #3 → +5`). `MATCH3_RULES.md` §4.2/§4.3 continue to own iteration
identity (depth 1 is not a Cascade; `d ≥ 2` is; a pass detecting no match
produces no Cascade). This is §5 item 2's naturally-repeating-Trigger case, not a
self-loop. **No new root-event Trigger is introduced, and Match/Combo semantics
are unmodified.**

### Q-4 — RESOLVED (Option C): Burning Curse Burn ownership

**Answered C — Pet-owned/source Burn only.** Burning Curse's `+30%`
`BurnDamage` modifies Burn damage the **Pet** applied/owns. Burn damage the
**Boss** applied/owns receives no bonus. The distinction is **Burn
source/ownership**, not the entity receiving the damage:

```text
Pet applies Burn to Boss        → Burning Curse applies
Pet-owned Burn tick resolves    → +30% applies
Boss applies Burn to Pet        → Burning Curse does NOT apply
Boss-owned Burn tick resolves   → unmodified
```

`target: Pet` identifies the Pet as the **owner/source context** of this
modifier; it does **not** mean "increase every Burn tick received by the Pet".
Burning Curse's TASK-176 contract is **unchanged** (`OnBattleStart`, no
Condition, `BurnDamage` `+30%`, `Percentage`, `target: Pet`, `Battle`,
non-stacking, once per battle). No second Burn effect system is introduced, and
the `BurnDamage` modifier changes damage only — it emits no Burn event and
creates no second Burn instance.

**None of Q-1…Q-4 remains an open question.** They are no longer blockers; the
implementer proceeds against the cited canonical sections and stops
(`AGENTS.md` §7, §20) only if a *new*, genuinely undecided ambiguity appears.

---

## Definition of Done

```text
[ ] Requirement understood (TASK-176 read as the decision source)
[ ] Relevant docs checked (AGENTS.md §6 Card/Relic + combat rows)
[ ] Scope checked (AGENTS.md §8; MVP_SCOPE.md ~10 Relics)
[ ] Existing implementation checked (the component table above)
[ ] Plan created
[ ] Code implemented through the existing Relic architecture only
[ ] Focused tests added for all six new Relics
[ ] Regression tests for the four existing Relics still pass unmodified
[ ] Tests pass (full backend suite)
[ ] No unrelated behavior changed (AGENTS.md §16)
[ ] No migration / seed / INSERT / schema change / provisioning present
[ ] No new Trigger introduced; BurnDamage is the only new Effect
[ ] Open Questions Q-1…Q-4 resolved by decision or reported as blockers
    (RESOLVED by TASK-178 — Q-1 = A, Q-2 = B, Q-3 = A, Q-4 = C; canonical
     references are recorded in "Open Questions / Blockers — RESOLVED")
[ ] No source-of-truth conflict introduced (AGENTS.md §4)
```

---

## Completion Evidence

### Contract implemented

```text
Burning Curse    OnBattleStart  —                      BurnDamage +30%  Pet  Battle       once per battle, non-stacking
Combo Fang       OnCombo        ComboAtLeast(5)        Crit +20pp       Pet  NextAttack
Arcane Battery   OnPowerGain    —                      Power +5         Pet  Immediate    qualifying non-Relic gains only
Execution Mark   OnHpBelow      HpPercentageBelow(30)  Crit +15pp       Pet  NextAttack
Cascade Core     OnCascade      —                      Power +5         Pet  Immediate    once per cascade iteration
Battle Instinct  OnDamageTaken  —                      ATK +10%         Pet  NextAttack   consumed by the qualifying attack
```

All six were implemented through the existing `RelicDefinition` /
`RelicResolver` / structured-`EffectDefinition` pipeline; the runtime stays
data-driven and no Relic is recognised by `RelicDefinitionId`, `Name`, or a
display string (verified by inspection: no provisioned Relic id appears anywhere
under `src/**`).

### Changed files

```text
src/backend/GameServer.Domain/Relics/RelicEffectType.cs          BurnDamage member (§8.2 item 1)
src/backend/GameServer.Domain/Relics/RelicEffectDefinition.cs    BurnDamage row; ATK's two-lifetime validation (§8.3)
src/backend/GameServer.Domain/Relics/RelicFiringPoint.cs         NEW — the four new firing points (+ §17 step 11's)
src/backend/GameServer.Domain/Relics/RelicResolver.cs            firing-point evaluation, BurnDamage application,
                                                                 ATK element lifetime, lifetime-scoped reversion,
                                                                 §3.1 Power-gain qualification
src/backend/GameServer.Domain/Battle/ATKModifier.cs              element's declared `Lifetime` (§2.3.7 item 11)
src/backend/GameServer.Domain/Battle/ATKModifiers.cs             carrier-lifetime validation, RemoveLifetime,
                                                                 ConsumeForQualifyingAttack (§5.1.4 item 4)
src/backend/GameServer.Domain/Battle/BurnDamageModifier.cs       NEW — the applied BurnDamage element
src/backend/GameServer.Domain/Battle/BurnDamageModifiers.cs      NEW — its lifecycle, ownership predicate, applied %
src/backend/GameServer.Domain/Battle/PetState.cs                 BurnDamageModifiers[] member + equality
src/backend/GameServer.Domain/Battle/EffectivePetATK.cs          both carrier lifetimes participate while active
src/backend/GameServer.Domain/Battle/Serialization/BattleStateJson.cs      new members/nodes
src/backend/GameServer.Domain/Battle/Serialization/BattleStateSerializer.cs write/read of the new members
src/backend/GameServer.Application/Battle/BattleStateService.cs  the five firing points, the shared NextAttack
                                                                 consumption, the Burn tick's step-4 factor

tests/backend/GameServer.Domain.Tests/Task177RelicFiringPointContractTests.cs          NEW
tests/backend/GameServer.Domain.Tests/Battle/Task177ModifierSerializationTests.cs      NEW
tests/backend/GameServer.Application.Tests/Task177RelicFiringPointRuntimeTests.cs      NEW
```

Four **pre-existing** tests were updated, each because the canonical contract it
transcribes was widened by TASK-176/TASK-178 (AGENTS.md §15: "the test is
outdated" — not a test edited to fit an implementation):

```text
RelicStructuredContractTests.EffectType_ShouldBeExactlyTheFourDocumentedIdentities
    → five identities; BurnDamage added by TASK-176 (§8.2 item 1)
RelicStructuredContractTests.Create_ShouldRejectALifetimeOtherThanTheEffectTypesOwn
    → ATK's second row (`NextAttack`, §8.3 / TASK-178 Q-1 = A) is now a defined
      combination; `Immediate` and the single-lifetime effect types still reject
BattleStateModifierSerializationTests.AnElement_SerializesExactlyTheTwoDocumentedMembers
    → three members; `lifetime` is required on every ATK element (§2.3.8 item 3,
      TASK-178 Q-1 = A)
BattleStateTests.PetState_ShouldCarryExactlyTheDocumentedFields
    → PetState gained BurnDamageModifiers[] (see "Documentation impact")
```

No other pre-existing test was changed; the four provisioned Relics' suites
(`RelicResolverTests`, `RelicStructuredContractTests`, `RelicStageResolutionTests`,
`RelicStructuredStorageTests`, `RelicWireProjectionTests`,
`CardCostRelicIntegrationTests`) pass with their existing scenarios.

### Commands run and observed results

```text
dotnet build src/backend/GameServer.sln -v q --nologo
  → Build succeeded. 0 Error(s)

dotnet test src/backend/GameServer.sln --nologo -v q
  → GameServer.Application.Tests    Passed!  Failed: 0, Passed: 557,  Total: 557
  → GameServer.Domain.Tests         Passed!  Failed: 0, Passed: 1543, Total: 1543
  → GameServer.Infrastructure.Tests Passed!  Failed: 0, Passed: 400,  Total: 400
  → GameServer.Api.Tests            Passed!  Failed: 0, Passed: 320,  Total: 320
```

Focused runs: `--filter "FullyQualifiedName~Task177"` → Domain 51, Application 12,
all passing (63 new tests).

### Acceptance criteria

```text
AC-1   PASS  OnBattleStart evaluates at battle start (creation) and applies one
             standing BurnDamage +30% / Pet / Battle element; a re-evaluation
             refreshes that element and a later Swap leaves it in place.
AC-2   PASS  ComboAtLeast(5): every match-producing Swap on the fixture board is
             asserted against its own Combo — the Relic resolves at Combo >= 5
             (and the +20 reaches, then is consumed by, that Swap's attack) and
             resolves for none below it.
AC-3   PASS  OnPowerGain applies +5 flat Power / Immediate per qualifying gain,
             and each gain resolves independently.
AC-4   PASS  OnHpBelow / HpPercentageBelow(30) applies +15pp Crit / NextAttack
             through the existing semantics; Emergency Core on the same Trigger
             and Condition is unchanged (same suite, same carriers).
AC-5   PASS  OnCascade resolves once per cascade iteration, asserted against the
             resolution's own pass count (`Passes.Count - 1`), never collapsed.
AC-6   PASS  OnDamageTaken applies ATK +10% / NextAttack on the existing
             ATKModifiers[] carrier; the +10% reaches the next attack through the
             generic EffectivePetATK composition (55 + pool observed) and is
             consumed by it, with no Battle-Instinct-specific calculation.
AC-7   PASS  BurnDamage exists with exactly Percentage / Pet / Battle; the +30%
             changes the Pet-owned Burn tick's damage and leaves the Boss-owned
             tick — and both instances and their durations — untouched.
AC-8   PASS  ATK + NextAttack is accepted by the reader/writer, carried on
             ATKModifiers[] with the element's own lifetime, and consumed at the
             same boundary as the Crit modifier; ATK + Battle is unchanged and
             Power / Crit / CardCost / BurnDamage keep their single lifetimes.
AC-9   PASS  Arcane Battery: exactly one activation per qualifying gain (the +5 it
             grants resolves nothing when re-dispatched with source Relic).
             Cascade Core: activations == cascade iterations, no synthesized
             cascade. Burning Curse: damage only, no Burn event, no second
             instance; §4.3's breadth-first queue is unchanged (one call = one
             root event, per-instance safeguard per call).
AC-10  PASS  The four provisioned Relics resolve identically: their existing
             suites pass unmodified, plus focused Domain and Application
             regression tests (all four in one resolution, in slot order).
AC-11  PASS  No Relic id, name, magnitude, threshold, or percentage appears in
             unrelated logic; the resolver reads only declared content.
AC-12  PASS  No migration, seed, HasData, INSERT, schema change, or provisioning.
             The changeset is src/backend/GameServer.Domain/**, the
             BattleStateService call-site wiring, and tests/** — plus the existing
             working-tree changes from the Boss work, which were not touched.
AC-13  PASS  Focused tests exist for every coverage bullet (63 new tests).
AC-14  PASS  The full backend suite passes (four projects, 2820 tests) with no
             pre-existing test changed except the four contract transcriptions
             listed above, each justified by the canonical widening.
AC-15  PASS  Completion evidence recorded here; documentation impact below.
```

### Documentation impact

```text
RELIC_RULES.md §8   NOT diverged from. BurnDamage, §8.3's BurnDamage/ATK rows,
                    §6 note 1's ownership scoping, §8.5 items 5–10, and §3.1/§3.2
                    are implemented exactly as written.
COMBAT_RULES.md     NOT diverged from. §3.3 items 7–11 remain the single
                    NextAttack consumption owner, §5.2 item 4's ownership scoping
                    and damage-only rule are implemented, §5.6/§5.6.6's
                    composition is reused unchanged.
GAME_STATE.md       FOLLOW-UP REQUIRED (not edited here — TASK-177 forbids
                    canonical documentation changes and AC-12 restricts the
                    changeset to src/** + tests/**).
                    §8.5 item 5 declares Burning Curse's effect and scopes it by
                    ownership, but no state section names a carrier for an APPLIED
                    `BurnDamage` modifier — unlike §8.5 items 2 and 4, which name
                    §2.3.5/§2.3.7. TASK-177's Architecture Constraints authorize
                    the smallest such carrier ("follow the existing two-member
                    SourceIdentity + value element schema"), so this task applied
                    the §2.3.5 CardCostModifier precedent exactly:
                      PetState.BurnDamageModifiers[]  (serialized `burnDamageModifiers`)
                      element = SourceIdentity + BurnDamagePercentage, written
                      order, always present/never null, Battle lifetime, source-
                      scoped removal.
                    A follow-up documentation task must add that member's section
                    to GAME_STATE.md (§2.3 tree, a §2.3.x instance schema, its
                    lifecycle, and the serialization note) and its line to
                    REDIS_STATE.md §7, so §17/§23 of AGENTS.md are satisfied.
                    Nothing else about the state contract changed: no key, no
                    Redis-only field, no wire member, no PostgreSQL column.
```

### Definition of Done

```text
[x] Requirement understood (TASK-176 read as the decision source)
[x] Relevant docs checked (AGENTS.md §6 Card/Relic + combat rows)
[x] Scope checked (AGENTS.md §8; MVP_SCOPE.md ~10 Relics)
[x] Existing implementation checked (the component table above)
[x] Plan created
[x] Code implemented through the existing Relic architecture only
[x] Focused tests added for all six new Relics
[x] Regression tests for the four existing Relics still pass (four test files
    updated only where the canonical contract they transcribe was widened)
[x] Tests pass (full backend suite)
[x] No unrelated behavior changed (AGENTS.md §16)
[x] No migration / seed / INSERT / schema change / provisioning present
[x] No new Trigger introduced; BurnDamage is the only new Effect
[x] Open Questions Q-1…Q-4 resolved by decision (TASK-178; none re-opened)
[x] No source-of-truth conflict introduced (AGENTS.md §4); one state-contract
    carrier is recorded above as a documentation follow-up
```

