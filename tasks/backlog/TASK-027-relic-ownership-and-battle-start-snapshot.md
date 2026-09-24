# TASK-027 — Relic Ownership & Battle-Start Snapshot

---

## Metadata

```text
Task ID:           TASK-027
Type:              FEATURE
Status:            BACKLOG
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     persistence
Supporting Agents: gameplay, backend, testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, backend/persistence-analysis, gameplay/gameplay-behavior-derivation, backend/api-contract-validation, testing/test-scenario-generation
Dependencies:      TASK-023, TASK-025
```

---

## Objective

Persist RelicDefinition and Player-owned Relic instances per DATABASE.md §1–§2, validate the documented battle-start equip count (3–5 owned Relics) for the active Pet, and snapshot the selected loadout into `PetState.EquippedRelics[]` at battle start with fixed slot order — no persistent equip table and no live inventory queries during battle.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — ~10 Relics; 3–5 equipped per battle IN
- `docs/01-game-design/RELIC_RULES.md` §2 — ownership vs equip; §4 — runtime representation/slot order
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` item 4 — battle-scoped loadout at POST /api/battle/start
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` items 7–8 — persistent ownership, battle-scoped equipment, PetState.EquippedRelics snapshot, no persistent equip table
- `docs/02-technical/DATABASE.md` §1–§2 — Relic entities/relationships; §3 constraints
- `docs/02-technical/GAME_STATE.md` §2.3 — PetState.EquippedRelics
- `docs/02-technical/API_CONTRACTS.md` §3 — POST /api/battle/start; §6 error convention

---

## Scope

### In Scope
- RelicDefinition and Relic (instance owned by Player) entities on `GameDbContext` with EF migration
- Owned-Relic validation for battle start (count and ownership checks per RELIC_RULES §2)
- Snapshot of selected Relics into `PetState.EquippedRelics[]` with fixed slot order at battle start
- Tests for ownership validation, count bounds, snapshot immutability during battle

### Out of Scope
- Persistent equip/loadout table (ADR-012 item 7 forbids it)
- Live PostgreSQL inventory reads during an active battle (snapshot only — ADR-012 item 8)
- Relic trigger/stacking resolution logic changes (existing RELIC_RULES behavior; only ownership/snapshot plumbing)
- Card loadout (TASK-028); Player Level (TASK-023)
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

No Relic entities exist; `GameDbContext` has no sets. `PetState` has no `EquippedRelics` member. `POST /api/battle/start` is not implemented (API_CONTRACTS §3 documented only). Battle bootstrap path is `BattleStateService` with caller-supplied `PetConfiguration`.

---

## Acceptance Criteria

- [ ] RelicDefinition and owned-Relic entities match DATABASE.md §1–§2, registered with an applying EF migration
- [ ] Battle-start path validates the documented equip count and ownership of each selected Relic for the requesting Player
- [ ] `PetState.EquippedRelics[]` exists per GAME_STATE §2.3, is populated at battle start with the fixed slot order of RELIC_RULES §4, and does not change from live DB reads mid-battle
- [ ] No persistent equip table is created; no Relic ownership rows are written during battle
- [ ] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[x] tests/ (unit / integration / gameplay scenarios)
[ ] docs/ (documentation already specifies ownership/equip split — ADR-011/012, RELIC_RULES)
```

---

## Implementation Notes

- Snapshot population should hook the same battle-bootstrap entry used by TASK-030 (`POST /api/battle/start`); until TASK-030 lands, expose the validation+snapshot as an Application service callable from tests and later wired by TASK-030.
- Extend `PetState` in `GameServer.Domain/Battle/BattleState.cs` with `EquippedRelics` only as specified by GAME_STATE §2.3 — do not add unrelated members here (TASK-028 adds EquippedCards separately).
- Error codes for invalid counts/ownership follow API_CONTRACTS §6 (`INVALID_LOADOUT` semantics as documented).
- Do not edit `tasks/completed/`; related history: TASK-018 (resource generation may interact with Relic triggers — leave trigger logic untouched).

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — equip-count bounds, ownership check, snapshot order/immutability
[x] Integration tests  — owned Relics load from DB at battle start; snapshot survives subsequent ownership-row changes
[x] Gameplay scenarios — Given a Player owning N Relics, When battle starts with a valid 3–5 selection, Then PetState.EquippedRelics matches the selection in documented slot order (RELIC_RULES §2, §4)
```

### Key Edge Cases
- See `docs/01-game-design/RELIC_RULES.md` §2, §4 — count bounds; snapshot slot order; anti-infinite-chain §5 remains trigger-domain (untouched)
- See `docs/02-technical/API_CONTRACTS.md` §6 — error convention for invalid loadout

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If a persistent equip table seems required: STOP — conflicts with ADR-012 item 7; report per `AGENTS.md` §4/§18
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
