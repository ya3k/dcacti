# TASK-029 — BattleState Runtime Serialization Mapping

---

## Metadata

```text
Task ID:           TASK-029
Type:              FEATURE
Status:            BACKLOG
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     realtime
Supporting Agents: backend, testing, review
Workflow:          development/feature.md
Skills:            backend/persistence-analysis, realtime/realtime-protocol-validation, discovery/impact-analysis, testing/test-scenario-generation, quality/architecture-conformance
Dependencies:      TASK-025, TASK-027, TASK-028
```

---

## Objective

Define and implement the JSON serialization mapping for the post-refactor `BattleState` runtime shape (root Combo/MatchCount, PetState as combat authority with EquippedRelics/EquippedCards) with round-trip fidelity tests, while explicitly performing no Redis key writes — REDIS_STATE §7 deferral remains unchanged.

---

## Authoritative References

- `docs/02-technical/REDIS_STATE.md` §2 — serialization boundary; §7 — foundation runtime state not stored yet (deferral)
- `docs/02-technical/GAME_STATE.md` §2, §2.2, §2.3 — canonical BattleState/member shape the mapping must represent
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` — target BattleState structure (no PlayerState node)
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` item 8 — EquippedRelics travels inside BattleState tree
- `docs/02-technical/TDD.md` §4 — persistence strategy context
- `docs/00-overview/MVP_SCOPE.md` §1 — active battle state store IN (implementation of store itself remains deferred per REDIS_STATE §7)

---

## Scope

### In Scope
- Serialization mapping (e.g. JSON converter/DTO graph) covering the full BattleState shape after TASK-025/027/028
- Round-trip tests: serialize → deserialize → value-equal BattleState, including root Combo/MatchCount, PetState combat+passive+loadout members, BossState, BoardState, RNG members
- Mapping documentation cross-check against GAME_STATE §2 (no silent member drift)

### Out of Scope
- Redis key creation, TTL, concurrency, recovery — REDIS_STATE §7 deferral unchanged (no `battle:{id}:state` writes)
- Wire/SignalR payload shapes (SIGNALR_PROTOCOL owns those — TASK-031 verifies)
- PostgreSQL persistence of BattleState (forbidden — active battle state is Redis-scoped, ADR-005/AGENTS §13)
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

No BattleState serializer exists; Redis is not written anywhere. `GameDbContext` is PostgreSQL-only. BattleState lives only as an in-memory Domain record (`GameServer.Domain/Battle/BattleState.cs`).

---

## Acceptance Criteria

- [ ] A serialization mapping exists whose member set matches GAME_STATE §2 (root Combo/MatchCount; PetState combat/passive/loadout; no PlayerState node)
- [ ] Round-trip tests pass for representative BattleStates including post-TASK-027/028 loadout arrays
- [ ] No code writes any Redis key; REDIS_STATE §7 items remain in force and are cited in Completion Evidence
- [ ] No BattleState persistence to PostgreSQL is introduced
- [ ] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[x] tests/ (unit / integration / gameplay scenarios)
[ ] docs/ (REDIS_STATE.md §7 already documents the deferral — do not weaken it)
```

---

## Implementation Notes

- Place mapping with Domain/Infrastructure serialization concerns per ARCHITECTURE §2.1 — do not embed it in Api hub code (hubs own wire projection, not state serialization).
- Explicitly exclude any API surface that exposes the serialized BattleState to clients (server-authoritative; client receives SIGNALR projections only).
- If implementation seems to require a Redis connection or key schema now: STOP — REDIS_STATE §7 governs; report instead of implementing.
- Do not edit `tasks/completed/`.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — round-trip equality for BattleState including combat fields, root accounting, and loadout arrays
[x] Integration tests  — serializer invoked from Application-layer battle lifecycle without Redis (in-memory only)
[x] Gameplay scenarios — N/A (serialization fidelity, not a gameplay rule); covered by round-trip value equality
```

### Key Edge Cases
- See `docs/02-technical/GAME_STATE.md` §2.0.3 — fields explicitly not present must not appear in the mapping
- See `docs/02-technical/REDIS_STATE.md` §7 — deferral conditions must remain true after this task

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If task requires writing active battle state to Redis or PostgreSQL before their documented gates: STOP per `AGENTS.md` §13 / REDIS_STATE §7
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
