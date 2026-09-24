# TASK-024 — Pet Persistence & Pet Level Derivation

---

## Metadata

```text
Task ID:           TASK-024
Type:              FEATURE
Status:            BACKLOG
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     persistence
Supporting Agents: gameplay, backend, testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, backend/persistence-analysis, gameplay/gameplay-behavior-derivation, testing/test-scenario-generation, quality/documentation-consistency
Dependencies:      TASK-023
```

---

## Objective

Persist Pet and PetDefinition entities per DATABASE.md §1–§2, store the per-Pet Level Multiplier as configuration on PetDefinition, and derive Pet Level from Player Level using the PET_RULES §5 formula with the documented clamp — keeping Pet.Level a denormalized snapshot that is recomputed when its inputs change, with no Pet XP system.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Pets, Tier/Star/Level progression IN; no Pet XP
- `docs/01-game-design/PET_RULES.md` §5, §5.1 — Pet Level formula and resolved open items
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` items 3–6 — formula ownership, clamp semantics, Tier/Star independence, no Pet XP/Evolution
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` item 7 — Pet Level as design formula
- `docs/02-technical/DATABASE.md` §1 — Pet and PetDefinition entities; §2 relationships; §3 constraints

---

## Scope

### In Scope
- Pet and PetDefinition entities on `GameDbContext` with EF migration
- `PetLevelMultiplier` stored on PetDefinition as configuration (never hard-coded)
- Pet Level derivation function implementing PET_RULES §5, reading Player.Level and PetDefinition multiplier
- Denormalized `Pet.Level` recompute hook when Player Level or multiplier changes (ADR-012 Consequences)
- `SignatureSkillCardId` column deferred — CardDefinition arrives in TASK-028; leave FK out until then (note in Completion Evidence)

### Out of Scope
- Pet XP, Evolution, Tier/Star changes (ADR-012 items 5–6; MVP_SCOPE §4)
- Combat stats on Player (ADR-011 item 5)
- Card/Relic ownership (TASK-027, TASK-028)
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

No Pet/PetDefinition entities exist; `GameDbContext` has no sets. `BattleStateService.PetConfiguration` (`src/backend/GameServer.Application/Battle/BattleStateService.cs`) initializes battle-time `PetState` without a persisted Pet identity or Level.

---

## Acceptance Criteria

- [ ] Pet and PetDefinition entities match DATABASE.md §1 and are registered with an applying EF migration
- [ ] Pet Level is derived exclusively via the PET_RULES §5 formula reading Player Level and PetDefinition's stored multiplier, clamped per ADR-012 item 4
- [ ] Stored Pet.Level is recomputed whenever Player Level or PetDefinition multiplier changes outside battle
- [ ] No Pet XP column, no Evolution field, no Tier/Star derivation from Player Level exists
- [ ] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[x] tests/ (unit / integration / gameplay scenarios)
[ ] docs/ (documentation updates if applicable — formula already documented)
```

---

## Implementation Notes

- Formula must be implemented from PET_RULES §5 — do not restate it here; do not hard-code the multiplier (`DATABASE.md` §1, ADR-012 item 3).
- Recompute hook: ADR-012 Consequences flags Pet.Level as a denormalized snapshot; wire the recompute to TASK-033's Level-up path (moved from TASK-023 by the TASK-023/033 decomposition revision — TASK-023 now provides only `Player.Level` persistence, with no Level-up path).
- SignatureSkillCardId FK is intentionally omitted until TASK-028 creates CardDefinition; do not create a stub table.
- Completed tasks affected in spirit only (do not edit `tasks/completed/`): TASK-014, TASK-015.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — Pet Level derivation at boundary Player Levels and multipliers; clamp behavior; recompute hook
[x] Integration tests  — migration round-trip; Player Level-up recomputes owned Pets' Level
[x] Gameplay scenarios — Given Player Level L and a Pet with multiplier M, When Pet Level is derived, Then the result equals PET_RULES §5 and stays within the documented range
```

### Key Edge Cases
- See `docs/01-game-design/PET_RULES.md` §5.1 — resolved open items (clamp, formula inputs)
- See `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` item 4 — both ranges respected independently

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If task requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10
- If Persistence of SignatureSkillCardId appears required before CardDefinition exists: STOP and report — do not invent a table
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
