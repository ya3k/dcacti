# TASK-025 — PlayerState → PetState Combat State Refactor

---

## Metadata

```text
Task ID:           TASK-025
Type:              REFACTOR
Status:            BACKLOG
Risk:              HIGH
Priority:          CRITICAL
Primary Agent:     gameplay
Supporting Agents: backend, realtime, testing, review
Workflow:          development/refactor.md
Skills:            discovery/impact-analysis, gameplay/authority-determinism-audit, quality/architecture-conformance, testing/test-scenario-generation, quality/implementation-review
Dependencies:      None
```

---

## Objective

Mechanically re-home battle state to match ADR-011: remove the nested `PlayerState` node from `BattleState`, move Match/Combo accounting (`Combo`, `MatchCount`) to the `BattleState` root, and move combat fields (HP, MaxHP, ATK, DEF, Crit, Power) onto `PetState` — with zero externally observable behavior change and all existing tests still passing.

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` items 1–5 — PlayerState removal, root Combo/MatchCount, PetState combat home
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` item 12 — BattleState shape has no PlayerState node
- `docs/02-technical/GAME_STATE.md` §2, §2.2, §2.3 — BattleState structure, root accounting, PetState fields
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4.2, §4.3 — fixed wire labels (not renamed by this task)
- `docs/00-overview/MVP_SCOPE.md` §1 — server-authoritative battle resolution IN

---

## Scope

### In Scope
- Remove `PlayerState` nested parameter/property from the `BattleState` record
- Add `Combo` and `MatchCount` to `BattleState` root; re-home HP/MaxHP/ATK/DEF/Crit/Power onto `PetState`
- Update every Domain/Application/Api reader and writer to the new locations (mechanical rename of access paths only)
- Update existing tests' state setup/assertions to the new shape; full suite green
- Preserve wire member names, payload shapes, and event payloads exactly (`SIGNALR_PROTOCOL.md` §3.2, §4.2, §4.3)

### Out of Scope
- Semantic changes to damage/heal/passive/reward logic (TASK-026)
- Renaming wire labels `playerState`, `finalPlayerHp`, `target="player"` (ADR-011 item 6 — forbidden)
- Adding `EquippedRelics`/`EquippedCards`/combat-collection fields to PetState beyond the existing documented set (TASK-027, TASK-028, GAME_STATE §2.3)
- Redis key writes (REDIS_STATE §7 deferral unchanged)
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

`BattleState` (`src/backend/GameServer.Domain/Battle/BattleState.cs`) nests `PlayerState PlayerState` and `PetState PetState`. `PlayerState` (`src/backend/GameServer.Domain/Battle/PlayerState.cs`) holds HP/MaxHP/ATK/DEF/Crit/Power/Combo/MatchCount. `PetState` holds only Element/PassiveId/PassiveProgress/PassiveResetOverride. Consumers include `DamagePipeline.cs`, `BattleStateService.cs`, `BattleHub.cs`, `BattleEventWireProjection.cs`.

---

## Acceptance Criteria

- [ ] `BattleState` has no `PlayerState` member; `Combo` and `MatchCount` are root members per GAME_STATE §2.2
- [ ] Combat fields (HP, MaxHP, ATK, DEF, Crit, Power) exist on `PetState` and are the only Player-side combat home
- [ ] Domain, Application, and Api code compile with all access paths updated; no stale `PlayerState` combat readers remain outside wire-projection label mapping
- [ ] Wire payloads unchanged: `playerState` still carries exactly `combo`+`matchCount`; `petState` still carries exactly the Passive trio; `finalPlayerHp`/`target="player"` labels unchanged
- [ ] Full existing test suite passes without weakening assertions (behavior-preserving refactor per `development/refactor.md` §2)
- [ ] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[x] tests/ (unit / integration / gameplay scenarios)
[ ] docs/ (documentation already describes target shape — GAME_STATE.md §2.2/§2.3, ADR-011)
```

---

## Implementation Notes

- Key files: `GameServer.Domain/Battle/BattleState.cs`, `GameServer.Domain/Battle/PlayerState.cs` (delete after migration of fields), `GameServer.Domain/Combat/DamagePipeline.cs`, `GameServer.Application/Battle/BattleStateService.cs`, `GameServer.Api/Hubs/BattleHub.cs` (projection from root/PetState into unchanged `PlayerStatePayload`/`PetStatePayload` records), `GameServer.Api/Hubs/BattleEventWireProjection.cs`.
- `PlayerStatePayload(int Combo, int MatchCount)` and `PetStatePayload` record shapes stay as-is; only their source members move (`BattleHub.cs` ~L630–656).
- `BattleEvent.ForBattleWon/ForBattleLost` final-player-HP arguments must read Pet HP after the move (`BattleStateService.cs` L651, L816) — same value, new location; wire `finalPlayerHp` unchanged.
- This is a structure-only change: no damage formula, ordering, or event-payload edits. If any semantic correction seems needed, stop and defer to TASK-026.
- Completed tasks affected in spirit only (do not edit `tasks/completed/`): TASK-015, TASK-016, TASK-017, TASK-018, TASK-019, TASK-021, TASK-022.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — BattleState construction/factory methods with root Combo/MatchCount and PetState combat fields
[x] Integration tests  — existing battle resolution suites recompiled against new shape; full suite green
[x] Gameplay scenarios — unchanged outcomes: same swap → same damage/events as before the refactor (regression equality)
```

### Key Edge Cases
- See `docs/02-technical/GAME_STATE.md` §2.0.3 — fields explicitly not present (no reintroduction of forbidden nodes)
- See `docs/02-technical/SIGNALR_PROTOCOL.md` §4.2 item 2, §4.3 — payload member-set must not widen

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If the refactor cannot preserve external behavior without a protocol change: STOP per `development/refactor.md` §1 and split
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
