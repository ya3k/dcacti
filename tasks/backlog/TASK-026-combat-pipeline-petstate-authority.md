# TASK-026 — Combat Pipeline PetState Authority

---

## Metadata

```text
Task ID:           TASK-026
Type:              REFACTOR
Status:            BACKLOG
Risk:              HIGH
Priority:          CRITICAL
Primary Agent:     gameplay
Supporting Agents: backend, testing, review
Workflow:          development/refactor.md
Skills:            gameplay/authority-determinism-audit, gameplay/gameplay-behavior-derivation, quality/architecture-conformance, testing/test-scenario-generation, quality/implementation-review
Dependencies:      TASK-025
```

---

## Objective

Audit and correct the combat pipeline so every Player-side combat read/write (damage, heal, shield, Power, Crit, passive charge, skill resolution, Boss response, victory/defeat, turn resolution) sources `PetState` exclusively, the Player is never a combat target or HP subject, and invariant tests enforce single-combat-stat-home per ADR-011 items 3 and 5.

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` items 3, 5 — PetState as sole combat home; no Player combat pool
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` item 12 — no Player combat pool introduced
- `docs/01-game-design/COMBAT_RULES.md` §1.1, §3 — combat stats are the active Pet's; Damage Pipeline inputs
- `docs/01-game-design/GAME_RULES.md` §1, §14, §17 — Pet as combat character; fixed resolution order; server authority
- `docs/01-game-design/PET_RULES.md` §2 — Pet as combat identity
- `docs/02-technical/GAME_STATE.md` §2.3, §3 — PetState fields; Damage Pipeline state references

---

## Scope

### In Scope
- Semantic audit of `DamagePipeline`, `BattleStateService` resolution steps, Boss response, victory/defeat checks, and Power/Crit paths after TASK-025
- Correct any residual code that still treats a Player combat value as authoritative (including parameter names, doc comments implying a Player pool, and healing/shield targets)
- Invariant tests: no Player combat-stat reads; Player never the HP subject of DamageDealt/DamageTaken; Boss→player damage lands on PetState.HP
- Preserve numeric outcomes and event ordering exactly (behavior-preserving per `development/refactor.md`)

### Out of Scope
- Changing damage formulas, Crit rules, element multipliers, or resolution order (COMBAT/ELEMENT/MATCH3 rules unchanged)
- Wire label renames (ADR-011 item 6)
- Relic/Card loadout mechanics (TASK-027, TASK-028)
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

After TASK-025, combat fields live on `PetState`, but pipeline code and comments were written against `PlayerState` (e.g. `DamagePipeline.cs` references to `PlayerState.ATK`/`PlayerState.DEF`; `BattleStateService.cs` boss-response path writing `resolved.PlayerState` HP/DEF). These access paths and their semantic intent must be verified against PetState authority, not merely recompiled.

---

## Acceptance Criteria

- [ ] Every damage/heal/shield/Power/Crit/passive/skill/Boss-response/victory/defeat/turn-resolution read of Player-side combat state sources `PetState` (or BattleState-root Combo/MatchCount for accounting only)
- [ ] No production code path treats Player as an HP/ATK/DEF/Power/Crit subject or combat target
- [ ] Boss→player DamageTaken/DamageDealt wire events continue to carry `target="player"` as the fixed label while the HP value comes from PetState
- [ ] Invariant tests exist that fail if a Player combat-stat read is reintroduced (e.g. compile-time absence of a Player combat type plus behavioral assertions)
- [ ] Numeric outcomes and event ordering of existing scenarios are unchanged from TASK-025 baseline
- [ ] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[x] tests/ (unit / integration / gameplay scenarios)
[ ] docs/ (documentation already correct — ADR-011/GAME_STATE describe PetState authority)
```

---

## Implementation Notes

- Audit checklist (file → concern): `GameServer.Domain/Combat/DamagePipeline.cs` (ATK/DEF/HP inputs and Crit comment), `GameServer.Application/Battle/BattleStateService.cs` (attack resolution L571–587, boss response L737–773, defeat check L814–816, victory L651), `GameServer.Domain/Battle/BossState.cs` (cross-references), any healing/shield write-backs.
- Doc comments that still describe a `PlayerState` combat pool are defects under ADR-011 — correct them as part of this task's code comments (docs/ prose is already correct).
- Wire projection: `finalPlayerHp` maps PetState.HP (already correct in `BattleEventWireProjection.cs` factories); verify end-to-end after pipeline fixes.
- Completed tasks affected in spirit only (do not edit `tasks/completed/`): TASK-015, TASK-016, TASK-017, TASK-019, TASK-021, TASK-022.

---

## Testing Requirements

### Required Verification
```text
[ ] Unit tests         — invariant suite: no Player combat reads; PetState is damage/heal target; Crit/Power source assertions
[ ] Integration tests  — full battle resolution regression: identical events/values vs TASK-025 baseline
[x] Gameplay scenarios — Given boss→player damage, When resolved, Then PetState.HP changes and wire target="player" label is preserved (COMBAT_RULES §1.1, GAME_STATE §3)
```

### Key Edge Cases
- See `docs/01-game-design/COMBAT_RULES.md` §1.1 — active Pet as damage subject; no Player HP pool
- See `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.15 — `target` values are fixed labels, not state locations

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If a combat rule appears to require a Player combat pool: STOP — conflicts with ADR-011 item 5; report per `AGENTS.md` §4/§18
- If task requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose

---

## Completion Evidence

### Changed Files
- `<file path>` — <summary of change>

### Validation Results
- `<test command or suite>` — PASS (<N> tests)

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
