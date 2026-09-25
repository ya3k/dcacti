# TASK-030 — POST /api/battle/start Endpoint

---

## Metadata

```text
Task ID:           TASK-030
Type:              FEATURE
Status:            BACKLOG
Risk:              HIGH
Priority:          CRITICAL
Primary Agent:     backend
Supporting Agents: persistence, realtime, testing, review
Workflow:          development/feature.md
Skills:            backend/api-contract-validation, discovery/documentation-discovery, backend/persistence-analysis, testing/test-scenario-generation, quality/scope-validation
Dependencies:      TASK-024, TASK-027, TASK-028
```

---

## Objective

Implement the documented `POST /api/battle/start` endpoint per API_CONTRACTS §3: authenticate the session, validate petId ownership and bossId, run Card/Relic loadout validation and snapshot (from TASK-027/028 services), create the BattleState via the existing battle bootstrap, and return the contract response — with documented error codes and no client-supplied combat values.

---

## Authoritative References

- `docs/02-technical/API_CONTRACTS.md` §3 — request/response contract; §6 — error convention; §1 endpoint summary
- `docs/00-overview/MVP_SCOPE.md` §1 — server-authoritative battle resolution IN
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` item 4 — loadout selected at POST /api/battle/start for the one active Pet
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` items 7–10 — equip counts and snapshot timing at this endpoint
- `docs/02-technical/GAME_STATE.md` §2 — BattleState creation contract; §2.3 PetState from Pet configuration
- `docs/02-technical/SIGNALR_PROTOCOL.md` §1 — client obtains battleId/hub URL from this endpoint
- `docs/02-technical/REDIS_STATE.md` §3, §7 — TTL lifecycle begins when endpoint lands; persistence deferral conditions

---

## Scope

### In Scope
- New controller/endpoint implementing API_CONTRACTS §3 request validation (session, petId owned by Player, bossId valid, loadout via TASK-027/028 services)
- Battle creation through `BattleStateService` with PetState built from persisted Pet (TASK-024) and loadout snapshots applied
- Documented error responses per API_CONTRACTS §6 (including INVALID_LOADOUT / PET_NOT_OWNED as specified)
- Integration tests covering success path and each documented error code

### Out of Scope
- Redis persistence writes — REDIS_STATE §7 deferral gate is separate; endpoint may bootstrap in-memory state per current runtime model (cite §7 in Completion Evidence if still deferred)
- Client-side battle-start logic (client only calls the endpoint)
- Changing the endpoint contract (API_CONTRACTS §3 owns it)
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

`POST /api/battle/start` does not exist. BossDefinition/BossId already reference the contract (`src/backend/GameServer.Domain/Bosses/BossId.cs`, `BossDefinition.cs`). `BattleStateService` (Application) can create BattleState from a `PetConfiguration` but has no HTTP entry point or persistence-backed Pet/loadout validation.

---

## Acceptance Criteria

- [ ] `POST /api/battle/start` exists and returns exactly the API_CONTRACTS §3 response shape
- [ ] Invalid session, unowned petId, invalid bossId, and invalid Card/Relic loadout each return the documented error code (API_CONTRACTS §6)
- [ ] On success, BattleState contains PetState derived from the persisted Pet (TASK-024) plus EquippedRelics (TASK-027) and EquippedCards (TASK-028) snapshots
- [ ] No request field accepts Damage/HP/Power/Match/Combo values (API_CONTRACTS §1 server-authoritative note)
- [ ] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[x] tests/ (unit / integration / gameplay scenarios)
[ ] docs/ (contract already documented — API_CONTRACTS §3)
```

---

## Implementation Notes

- New controller alongside `AuthController` under `src/backend/GameServer.Api/Controllers/`.
- Reuse TASK-027/TASK-028 Application services for loadout validation+snapshot; do not duplicate that logic in the controller.
- PetState construction: persist-derived path should mirror `BattleStateService.PetConfiguration.ToPetState()` but source fields from the persisted Pet row.
- Redis: if the endpoint is implemented while REDIS_STATE §7 still defers storage, document that no key is written yet and §7 remains in force (do not invent a partial store).
- Do not edit `tasks/completed/`.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — request validation branches (session, pet, boss, each loadout failure mode)
[x] Integration tests  — full endpoint success path returns contract response and creates BattleState with snapshots
[x] Gameplay scenarios — Given an authenticated Player with a valid pet/boss/loadout, When POST /api/battle/start succeeds, Then battleId is returned and PetState carries the snapshotted loadout (API_CONTRACTS §3, ADR-012 items 7–10)
```

### Key Edge Cases
- See `docs/02-technical/API_CONTRACTS.md` §6 — every documented error code has a test
- See `docs/02-technical/SIGNALR_PROTOCOL.md` §1 — returned battleId/hub URL feed the connection flow

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If the contract and another doc disagree on request/response fields: STOP per `AGENTS.md` §4 (data contract conflict)
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
