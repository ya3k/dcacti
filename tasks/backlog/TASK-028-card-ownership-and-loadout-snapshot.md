# TASK-028 — Card Ownership & Loadout Snapshot

---

## Metadata

```text
Task ID:           TASK-028
Type:              FEATURE
Status:            BACKLOG
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     persistence
Supporting Agents: gameplay, backend, testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, backend/persistence-analysis, gameplay/gameplay-behavior-derivation, testing/test-scenario-generation, quality/documentation-consistency
Dependencies:      TASK-023, TASK-024, TASK-025
```

---

## Objective

Persist CardDefinition and the `PlayerUnlockedCard` unlock-flag join per DATABASE.md §1–§2 (ADR-012 item 9), validate the documented battle loadout (exactly 3 Basic + 1 Pet Skill Card for the active Pet), and snapshot it into `PetState.EquippedCards[]` at battle start — with no `Pet.CardInventory` and no instance table.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — 3 Basic Cards + 5 Pet Skill Cards IN
- `docs/01-game-design/CARD_RULES.md` §1 — loadout composition; §3 — equip for active Pet
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` item 4 — battle-scoped Card loadout at POST /api/battle/start
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` items 9–10 — PlayerUnlockedCard join; PetState.EquippedCards snapshot; no Pet.CardInventory
- `docs/02-technical/DATABASE.md` §1 — CardDefinition; §2 — ownership join (ADR-012 resolves ASSUMPTION); §3 constraints
- `docs/02-technical/GAME_STATE.md` §2.3 — PetState.EquippedCards
- `docs/02-technical/API_CONTRACTS.md` §3 — POST /api/battle/start; §6 error convention

---

## Scope

### In Scope
- CardDefinition and `PlayerUnlockedCard(PlayerId, CardDefinitionId)` entities on `GameDbContext` with EF migration
- Completes TASK-024's deferred `SignatureSkillCardId` FK on PetDefinition now that CardDefinition exists
- Battle-start loadout validation (exactly 3 Basic + 1 Pet Skill Card, all unlocked by the Player, valid for the active Pet per CARD_RULES §1)
- Snapshot into `PetState.EquippedCards[]` at battle start
- Tests for unlock validation, loadout cardinality, snapshot immutability

### Out of Scope
- Card Tier/Star/Level instance tables (ADR-012 item 9 — MVP Cards have none)
- `Pet.CardInventory` (ADR-012 item 9 forbids it)
- Card cast/Power-spend resolution logic changes (existing CARD_RULES behavior; only ownership/snapshot plumbing)
- Persistent equip table (ADR-012 items 7–8 pattern)
- Relic loadout (TASK-027)
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

No Card entities exist; `GameDbContext` has no sets. `PetState` has no `EquippedCards` member. `BattleStateService.PetConfiguration` does not carry a Card loadout. `SignatureSkillCardId` was intentionally omitted in TASK-024.

---

## Acceptance Criteria

- [ ] CardDefinition and PlayerUnlockedCard entities match DATABASE.md §1–§2, registered with an applying EF migration
- [ ] `PetDefinition.SignatureSkillCardId` FK added and references CardDefinition (completes TASK-024 deferral)
- [ ] Battle-start path validates exactly the documented Basic + Pet Skill Card composition against the Player's unlocks and the active Pet
- [ ] `PetState.EquippedCards[]` exists per GAME_STATE §2.3, is populated at battle start, and does not change from live DB reads mid-battle
- [ ] No `Pet.CardInventory` field and no Card instance/Tier/Star/Level table exists
- [ ] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[x] tests/ (unit / integration / gameplay scenarios)
[ ] docs/ (documentation already specifies ownership/equip — ADR-011/012, CARD_RULES)
```

---

## Implementation Notes

- Same Application-service pattern as TASK-027: expose validation+snapshot for tests and wire into TASK-030's `POST /api/battle/start`.
- Extend `PetState` in `GameServer.Domain/Battle/BattleState.cs` with `EquippedCards` only as specified by GAME_STATE §2.3 — do not fold Relic members into this task.
- Invalid-loadout errors follow API_CONTRACTS §6.
- Do not edit `tasks/completed/`.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — unlock-flag check; Basic/Pet Skill cardinality; Pet↔Signature card binding; snapshot immutability
[x] Integration tests  — unlocked Cards load from DB at battle start; SignatureSkillCardId FK migration applies
[x] Gameplay scenarios — Given a Player with the required unlocks, When battle starts, Then PetState.EquippedCards contains exactly the documented loadout for the active Pet (CARD_RULES §1)
```

### Key Edge Cases
- See `docs/01-game-design/CARD_RULES.md` §1, §3 — loadout composition; active-Pet binding
- See `docs/02-technical/API_CONTRACTS.md` §6 — error convention for invalid loadout

---

## Stop Conditions

- If required behavior cannot be derived from Authoritative References: STOP per `AGENTS.md` §7
- If a Card instance/Tier/Star/Level table or `Pet.CardInventory` seems required: STOP — conflicts with ADR-012 item 9; report per `AGENTS.md` §4/§18
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
