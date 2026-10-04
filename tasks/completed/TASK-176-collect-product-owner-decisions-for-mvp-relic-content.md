# TASK-176 — Collect Product Owner Decisions for MVP Relic Content

<!--
  GEN-TASK EXECUTION MANIFEST — PRODUCT-OWNER DECISION-INPUT TASK
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and task files by path and section — it does NOT copy
  game rules, formulas, magnitudes, schemas, or contracts.

  STATUS: DONE. Product Owner approved decisions recorded verbatim and applied
  to canonical documentation (RELIC_RULES.md v1.13, DATABASE.md).
-->

---

## Metadata

```text
Task ID:           TASK-176
Type:              DOCUMENTATION
Status:            DONE
Risk:              LOW (input capture and canonical documentation synchronization only;
                   no runtime code, schema changes, or database migrations in scope)
Priority:          HIGH (unblocking input for complete MVP Relic content)
Primary Agent:     orchestrator
Supporting Agents: N/A
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   gameplay/gameplay-behavior-derivation,
                   quality/scope-validation
Dependencies:      TASK-082 (DONE — initial provisioning contract; Burning Curse deferred),
                   TASK-085 (DONE — initial Relic definition provisioning),
                   TASK-131 (DONE — structured trigger/condition/effect contract; Burning Curse deferred),
                   TASK-132 (DONE — jsonb structured column migration),
                   TASK-133 (DONE — server-authoritative Relic trigger and effect resolution),
                   TASK-137 (DONE — Relic ATK modifier composition contract)
Blocks:            TASK-177 (Implement newly-required domain/combat vocabulary for Relics),
                   TASK-178 (Provision approved MVP Relic definitions),
                   TASK-179 (Relic verification and regression test suite)
Estimate:          Normal (documentation updates applied; ready for downstream implementation)
```

---

## Objective

Collect explicit Product Owner decisions required to complete the MVP Relic content contract:
1. Resolve the documented Burning Curse static-modifier vs event-trigger conflict (`BC-01`) and define its complete canonical effect contract (`BC-02`).
2. Collect complete canonical gameplay contracts for the 5 unauthored MVP Relics (`RD-06` through `RD-10`) to bring the total MVP Relic count to 10.
3. Apply approved decisions to authoritative documentation (`RELIC_RULES.md`, `DATABASE.md`) without implementing runtime code or database migrations.
4. Classify technical and architectural impact for each decision for downstream tasks.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Scope target: "~10 Relics"
- `docs/00-overview/ROADMAP.md` §1 Phase 2 — MVP Relic content requirements
- `docs/01-game-design/RELIC_RULES.md` §1 — Relic structure (`Trigger`, `Condition`, `Effect`, `Reset/Cooldown`)
- `docs/01-game-design/RELIC_RULES.md` §2 — Equip rules (loadout selection, snapshot into `PetState.EquippedRelics[]`)
- `docs/01-game-design/RELIC_RULES.md` §3 — Closed MVP Trigger vocabulary (`OnBattleStart`, `OnMatch`, `OnMatchCount`, `OnCombo`, `OnCascade`, `OnPowerGain`, `OnDamageDealt`, `OnDamageTaken`, `OnHpBelow`, `OnCardCast`, `OnTurnStart`, `OnTurnEnd`)
- `docs/01-game-design/RELIC_RULES.md` §4 — Deterministic trigger ordering (equip slot index 1–5, breadth-first chaining)
- `docs/01-game-design/RELIC_RULES.md` §5 — Anti-infinite-chain rule
- `docs/01-game-design/RELIC_RULES.md` §6 — MVP Relic Reference table
- `docs/01-game-design/RELIC_RULES.md` §8.1 — Structured `Condition` vocabulary (`MatchCountAtLeast(N)`, `ComboAtLeast(N)`, `HpPercentageBelow(N)`)
- `docs/01-game-design/RELIC_RULES.md` §8.2 — Structured `EffectDefinition[]` vocabulary (`effectType`, `valueType`, `value`, `target`, `lifetime`)
- `docs/01-game-design/RELIC_RULES.md` §8.3 — Allowed effect target and scope combinations
- `docs/01-game-design/COMBAT_RULES.md` §5 — Status Effects (Burn damage-over-time rules and stacking)
- `docs/02-technical/DATABASE.md` §1 — `RelicDefinition` schema and provisioning rules
- `AGENTS.md` §7, §8, §10, §12, §16, §20, §23 — Non-negotiable agent behavioral boundaries

---

## Canonical MVP Relic Inventory (10 Relics)

| Relic | ID | Trigger | Condition | EffectDefinition | Status |
| --- | --- | --- | --- | --- | --- |
| Berserker Core | `relic-berserker-core` | `OnMatchCount` | `MatchCountAtLeast(3)` | `[{ "effectType": "ATK", "valueType": "Percentage", "value": 5, "target": "Pet", "lifetime": "Battle" }]` | DEFINED & PROVISIONED |
| Mana Crystal | `relic-mana-crystal` | `OnMatchCount` | `MatchCountAtLeast(4)` | `[{ "effectType": "Power", "valueType": "Flat", "value": 10, "target": "Pet", "lifetime": "Immediate" }]` | DEFINED & PROVISIONED |
| Assassin Eye | `relic-assassin-eye` | `OnCombo` | `ComboAtLeast(3)` | `[{ "effectType": "Crit", "valueType": "PercentagePoints", "value": 10, "target": "Pet", "lifetime": "NextAttack" }]` | DEFINED & PROVISIONED |
| Emergency Core | `relic-emergency-core` | `OnHpBelow` | `HpPercentageBelow(30)` | `[{ "effectType": "CardCost", "valueType": "Percentage", "value": 50, "target": "Pet", "lifetime": "Battle" }]` | DEFINED & PROVISIONED |
| Burning Curse | `relic-burning-curse` | `OnBattleStart` | `null` | `[{ "effectType": "BurnDamage", "valueType": "Percentage", "value": 30, "target": "Pet", "lifetime": "Battle" }]` | DEFINED (TASK-176) |
| Combo Fang | `relic-combo-fang` | `OnCombo` | `ComboAtLeast(5)` | `[{ "effectType": "Crit", "valueType": "PercentagePoints", "value": 20, "target": "Pet", "lifetime": "NextAttack" }]` | DEFINED (TASK-176) |
| Arcane Battery | `relic-arcane-battery` | `OnPowerGain` | `null` | `[{ "effectType": "Power", "valueType": "Flat", "value": 5, "target": "Pet", "lifetime": "Immediate" }]` | DEFINED (TASK-176) |
| Execution Mark | `relic-execution-mark` | `OnHpBelow` | `HpPercentageBelow(30)` | `[{ "effectType": "Crit", "valueType": "PercentagePoints", "value": 15, "target": "Pet", "lifetime": "NextAttack" }]` | DEFINED (TASK-176) |
| Cascade Core | `relic-cascade-core` | `OnCascade` | `null` | `[{ "effectType": "Power", "valueType": "Flat", "value": 5, "target": "Pet", "lifetime": "Immediate" }]` | DEFINED (TASK-176) |
| Battle Instinct | `relic-battle-instinct` | `OnDamageTaken` | `null` | `[{ "effectType": "ATK", "valueType": "Percentage", "value": 10, "target": "Pet", "lifetime": "NextAttack" }]` | DEFINED (TASK-176) |

---

## Approved Product Owner Decisions

### 1. Burning Curse Trigger Model (`BC-01`) & Effect Contract (`BC-02`)

- **Decision:** Burning Curse is retained and reclassified as an event-driven Relic declaring `OnBattleStart`.
- **RelicId:** `relic-burning-curse`
- **Name:** Burning Curse
- **Description:** At Battle Start, Burning Curse grants the Pet +30% Burn damage for the Battle.
- **Trigger:** `OnBattleStart` (fires once per battle at battle start)
- **Condition:** `NONE` (`null`)
- **Effect:** `BurnDamage`
- **Magnitude:** `30`
- **Value type:** `Percentage`
- **Target:** `Pet`
- **Lifetime:** `Battle`
- **Stacking:** Non-stacking
- **Reset/re-fire behavior:** Once per battle via `OnBattleStart`

### 2. Combo Fang (`RD-06`)

- **RelicId:** `relic-combo-fang`
- **Name:** Combo Fang
- **Description:** A stronger combo threshold than Assassin Eye, granting increased Crit for the next attack.
- **Trigger:** `OnCombo`
- **Condition:** `ComboAtLeast`
- **Threshold:** `5`
- **Effect:** `Crit`
- **Magnitude:** `20`
- **Value type:** `PercentagePoints`
- **Target:** `Pet`
- **Lifetime:** `NextAttack`
- **Stacking:** Non-stacking
- **Reset/re-fire behavior:** Re-fires when the trigger condition is met after the effect is consumed.

### 3. Arcane Battery (`RD-07`)

- **RelicId:** `relic-arcane-battery`
- **Name:** Arcane Battery
- **Description:** Rewards Power generation by granting additional Power.
- **Trigger:** `OnPowerGain`
- **Condition:** `NONE` (`null`)
- **Threshold:** `NONE`
- **Effect:** `Power`
- **Magnitude:** `5`
- **Value type:** `Flat`
- **Target:** `Pet`
- **Lifetime:** `Immediate`
- **Stacking:** Each qualifying Power gain resolves independently
- **Reset/re-fire behavior:** Re-fires on each qualifying Power gain

### 4. Execution Mark (`RD-08`)

- **RelicId:** `relic-execution-mark`
- **Name:** Execution Mark
- **Description:** When the Pet's HP is below 30%, Execution Mark grants +15 Crit to the Pet's next attack.
- **Trigger:** `OnHpBelow`
- **Condition:** `HpPercentageBelow`
- **Threshold:** `30`
- **Effect:** `Crit`
- **Magnitude:** `15`
- **Value type:** `PercentagePoints`
- **Target:** `Pet`
- **Lifetime:** `NextAttack`
- **Stacking:** Non-stacking
- **Reset/re-fire behavior:** Conforms to existing canonical `OnHpBelow` semantics. Once consumed, re-fires when the condition is met at evaluation.

### 5. Cascade Core (`RD-09`)

- **RelicId:** `relic-cascade-core`
- **Name:** Cascade Core
- **Description:** Rewards cascade events with additional Power.
- **Trigger:** `OnCascade`
- **Condition:** `NONE` (`null`)
- **Threshold:** `NONE`
- **Effect:** `Power`
- **Magnitude:** `5`
- **Value type:** `Flat`
- **Target:** `Pet`
- **Lifetime:** `Immediate`
- **Stacking:** Each qualifying Cascade resolves independently
- **Reset/re-fire behavior:** Re-fires for each qualifying Cascade iteration.

### 6. Battle Instinct (`RD-10`)

- **RelicId:** `relic-battle-instinct`
- **Name:** Battle Instinct
- **Description:** Taking damage empowers the Pet's next attack.
- **Trigger:** `OnDamageTaken`
- **Condition:** `NONE` (`null`)
- **Threshold:** `NONE`
- **Effect:** `ATK`
- **Magnitude:** `10`
- **Value type:** `Percentage`
- **Target:** `Pet`
- **Lifetime:** `NextAttack`
- **Stacking:** Non-stacking
- **Reset/re-fire behavior:** Re-fires after each qualifying damage event once previous effect is consumed.

---

## Technical Impact Classification Matrix

| Relic / Decision | Trigger Classification | Effect Classification | Condition Classification | Technical Impact |
| --- | --- | --- | --- | --- |
| BC-01 / BC-02 (`Burning Curse`) | EXISTING CONTRACT (`OnBattleStart`) | DOMAIN CHANGE (`BurnDamage`) | EXISTING CONTRACT (`null`) | `DOCUMENTATION CHANGE`, `DOMAIN CHANGE`, `COMBAT RULE CHANGE`, `DATABASE/PROVISIONING CHANGE` |
| RD-06 (`Combo Fang`) | EXISTING CONTRACT (`OnCombo`) | EXISTING CONTRACT (`Crit`) | EXISTING CONTRACT (`ComboAtLeast`) | `DOCUMENTATION CHANGE`, `DATABASE/PROVISIONING CHANGE` |
| RD-07 (`Arcane Battery`) | EXISTING CONTRACT (`OnPowerGain`) | EXISTING CONTRACT (`Power`) | EXISTING CONTRACT (`null`) | `DOCUMENTATION CHANGE`, `DOMAIN CHANGE` (runtime trigger hook), `DATABASE/PROVISIONING CHANGE` |
| RD-08 (`Execution Mark`) | EXISTING CONTRACT (`OnHpBelow`) | EXISTING CONTRACT (`Crit`) | EXISTING CONTRACT (`HpPercentageBelow`) | `DOCUMENTATION CHANGE`, `DATABASE/PROVISIONING CHANGE` |
| RD-09 (`Cascade Core`) | EXISTING CONTRACT (`OnCascade`) | EXISTING CONTRACT (`Power`) | EXISTING CONTRACT (`null`) | `DOCUMENTATION CHANGE`, `DOMAIN CHANGE` (runtime trigger hook), `DATABASE/PROVISIONING CHANGE` |
| RD-10 (`Battle Instinct`) | EXISTING CONTRACT (`OnDamageTaken`) | EXISTING CONTRACT (`ATK`) | EXISTING CONTRACT (`null`) | `DOCUMENTATION CHANGE`, `DOMAIN CHANGE` (runtime trigger hook & `NextAttack` ATK carrier), `DATABASE/PROVISIONING CHANGE` |

---

## Completion Evidence

### Changed Files
- `docs/01-game-design/RELIC_RULES.md` — Bumped version to 1.13; updated §6 MVP Relic Reference table and notes with 10 canonical Relics; added `BurnDamage` to §8.2 structured effect types; updated §8.3 allowed scope combinations (`BurnDamage` + `Battle`, `ATK` + `NextAttack`); updated §8.4 canonical lifetimes; updated §8.5 canonical contracts table and items 1–10 (Burning Curse resolved to `OnBattleStart`, unblocking deferral); updated §8.7 status narrative.
- `docs/02-technical/DATABASE.md` — Synchronized static content definition references in §1 and §3 note item 4 with landed 10-Relic canonical contract.
- `tasks/completed/TASK-176-collect-product-owner-decisions-for-mvp-relic-content.md` — Task manifest finalized with full PO decision record and moved to completed.

### Validation Results
- Exactly 10 MVP Relic definitions are canonically defined in game design documents.
- Burning Curse static-modifier conflict is fully resolved; no unprovisioned placeholder or deferred tension remains in active rules.
- No new Trigger was added to §3.
- `BurnDamage` is the sole new effect vocabulary item introduced.
- Existing 4 Relics (`relic-berserker-core`, `relic-mana-crystal`, `relic-assassin-eye`, `relic-emergency-core`) remain unchanged in their gameplay contracts.
- No runtime code (`src/`), database migrations, seed data, or tests were created or modified in this task.
