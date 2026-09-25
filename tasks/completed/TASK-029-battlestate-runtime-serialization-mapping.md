# TASK-029 — BattleState Runtime Serialization Mapping

---

## Metadata

```text
Task ID:           TASK-029
Type:              FEATURE
Status:            DONE
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

- [x] A serialization mapping exists whose member set matches GAME_STATE §2 (root Combo/MatchCount; PetState combat/passive/loadout; no PlayerState node)
- [x] Round-trip tests pass for representative BattleStates including post-TASK-027/028 loadout arrays
- [x] No code writes any Redis key; REDIS_STATE §7 items remain in force and are cited in Completion Evidence
- [x] No BattleState persistence to PostgreSQL is introduced
- [x] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [x] Quality review checklist passes (`quality/review.md` §1)
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

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

### Outcome — DONE

The runtime JSON serialization mapping for the authoritative `BattleState` exists
and round-trips losslessly, verified member by member. **No Redis key is written,
no PostgreSQL persistence is introduced, no SignalR protocol changed, no API
endpoint added, and no gameplay logic was touched.** `REDIS_STATE.md` §7's
deferral remains in force and was not weakened.

### Changed Files

**Created — Domain**

- `src/backend/GameServer.Domain/Battle/Serialization/BattleStateSerializer.cs`
  — the mapping: `Serialize(BattleState) → string` and
  `Deserialize(string) → BattleState`. A pure field mapping: it computes no
  gameplay value, draws no RNG, advances no counter, generates/normalizes no
  board, and re-reads no loadout from inventory. It references no Redis,
  PostgreSQL, EF Core, ASP.NET Core, SignalR, HTTP, or transport type.
- `src/backend/GameServer.Domain/Battle/Serialization/BattleStateJson.cs` — the
  explicit DTO graph plus `BattleStateJsonNames`, which declares every JSON member
  name once. Explicit `[JsonPropertyName]` on every member means no naming policy
  can silently rename a persisted member (`SIGNALR_PROTOCOL.md` §3.2.3).

**Created — Tests**

- `tests/backend/GameServer.Domain.Tests/BattleStateSerializationTests.cs`
  (43 tests) — round-trip fidelity, member-set exactness, exclusions, optional
  members, enum representation, determinism, and rejection of malformed records.
- `tests/backend/GameServer.Application.Tests/BattleStateSerializationLifecycleTests.cs`
  (6 tests) — the Application-level integration seam: state created through the
  production `BattleStateService.CreateBattle` path round-trips, including after a
  real committed Swap.

**Not changed (deliberate)**

- `src/backend/GameServer.Domain/GameServer.Domain.csproj` — **no package
  reference added.** `System.Text.Json` is part of the .NET base class library, so
  Domain gained no framework dependency (`ARCHITECTURE.md` §2.1 item 1,
  `ADR-002`).
- `BattleHub`, `ReceiveEvents`, `GetBattleState`, `BattleEventWireProjection`,
  `BattleController` — untouched. The SignalR wire projection is a different,
  protocol-fixed subset (`SIGNALR_PROTOCOL.md` §4) and is not this mapping.
- `GameDbContext`, migrations, EF configurations, repositories — untouched.
- `src/frontend/client/` — untouched.
- `docs/` — **no change required** (see Documentation below).
- `tasks/completed/*` — untouched.

### Design decisions (both confirmed by the requester before implementation)

1. **Placement: Domain** (`GameServer.Domain/Battle/Serialization`). Verified
   against the actual dependency rules before implementing: `ARCHITECTURE.md` §2.1
   item 1 excludes *ASP.NET Core, Redis, PostgreSQL, and SignalR* from Domain —
   `System.Text.Json` is none of these. `ADR-002`'s "no framework dependency"
   concerns testability, which a BCL serializer satisfies. Infrastructure/Redis
   was rejected because TASK-029 forbids Redis writes, so it would be dead
   production code.
2. **Casing: explicit camelCase**, matching the existing
   `BoardStateSerializationTests` convention. No global naming policy is relied on.

### Serialization Shape (from the implementation, not invented)

```json
{
  "battleId": "battle-9f3c1d7e",
  "turn": 14,
  "sequence": 19,
  "rngSeed": 18364758544493064720,
  "rngState": { "state": 81985529216486895, "increment": 1 },
  "boardState": {
    "cells": [
      { "gemType": "ATK", "specialGem": { "type": "LineClear", "orientation": "Horizontal" } },
      { "gemType": "DEF" }
    ]
  },
  "combo": 6,
  "matchCount": 137,
  "petState": {
    "hp": 723, "maxHp": 1042, "atk": 61, "def": 17, "crit": 12, "power": 88,
    "element": "Thuy",
    "passiveId": "thanh-xa-poison",
    "passiveProgress": { "threshold": 7, "current": 3 },
    "passiveResetOverride": "Partial",
    "equippedRelics": ["relic-instance-c", "relic-instance-a", "relic-instance-b"],
    "equippedCards": ["card-heal", "card-heal", "card-shield", "card-thanh-xa-skill"]
  },
  "bossState": {
    "bossId": "Thủy Ma", "element": "Thuy",
    "hp": 3117, "maxHp": 5000, "atk": 100, "def": 50,
    "state": "Enraged",
    "passiveId": "boss-thuy-ma-heal",
    "passiveProgress": { "threshold": 5, "current": 4 },
    "skillCharge": 3, "skillCooldown": 2
  },
  "lastCommittedSwapPair": { "minCellIndex": 33, "maxCellIndex": 41 }
}
```

`cells` is exactly 64 entries in ascending index order. `lastCommittedSwapPair`,
`passiveResetOverride`, `specialGem`/`orientation`, and both loadout arrays are
**omitted** when absent — absence is the documented representation
(`GAME_STATE.md` §2.1.10 item 3, §2.1.7 item 3, §2.3).

### Mapping

```text
BattleState
    ↓  BattleStateSerializer.Serialize    (BattleState → BattleStateJson)
JSON
    ↓  BattleStateSerializer.Deserialize  (BattleStateJson → BattleState)
BattleState
```

The mapping lives entirely in
`GameServer.Domain/Battle/Serialization/`. Serialization projects the state
one-to-one onto the DTO graph; deserialization rebuilds through the Domain
constructors (`BoardState.FromCellEntries`, `CommittedSwapPair`, `PetState`,
`BossState`) so each type's documented invariants are enforced rather than
trusted. It deliberately does **not** use `BattleState.Create(...)`, which
generates a board from a seed and resets counters — that would discard the state
being recovered.

### Tests

```text
Focused (Domain round-trip)      — PASS (43)
Focused (Application lifecycle)  — PASS (6)
Application                     — PASS (199)
Domain                          — PASS (905)
Infrastructure                  — PASS (88)
Api                             — PASS (109)
Full backend                    — PASS (1301), 0 failed
Frontend                        — PASS (174, 12 files), unchanged
```

Baseline before this task was 1252 passing; 49 added, no test regressed.

### Redis Verification

```text
Redis keys written: 0
```

No Redis client, key, TTL, lock, or `StringSet` exists anywhere in the change. The
serializer is a pure function whose only output is a `string`, so it has no
storage side effect. `REDIS_STATE.md` §7's deferral remains active and unchanged:
§7 item 3's status note still records that the record is not written and that
§7 item 7's requirement is due rather than discharged by this task. §7 items 9–11
(round-trip losslessness for the counters, the commit record, and — via §7
item 10 — Special Gems) are now **satisfied by an implemented mapping**, but the
storage step itself remains deferred.

### PostgreSQL Verification

```text
No BattleState table
No migration
No EF persistence
```

Verified: `git status` shows no new migration, no new `DbSet`, and no change under
`GameServer.Infrastructure/Postgres/` attributable to this task
(`GameDbContext.cs` and the snapshot appear modified only from the earlier
TASK-023/024/027/028 work already present in the working tree).

### SignalR Verification

```text
No SignalR protocol changes
```

`BattleHub`, `ReceiveEvents`, `GetBattleState`, and `BattleEventWireProjection`
are untouched. The mapping is not a wire projection: `SIGNALR_PROTOCOL.md` §4
delivers a protocol-fixed subset, and `LastCommittedSwapPair` is deliberately
never delivered (§4 item 12) even though it is part of this persistence mapping.

### Mutation / Test-Teeth

Performed. Two mutants were introduced, both caught, then reverted:

```text
Mutant 1  MatchCount written as InitialMatchCount instead of state.MatchCount
          → CAUGHT: 2 Domain + 1 Application failures
Mutant 2  SpecialGem always written as null (drop every Special Gem)
          → CAUGHT: 6 Domain + 1 Application failures

Reverted; bin/obj purged and rebuilt before re-running.
Restored state: 43/43 Domain and 6/6 Application focused tests PASS.
```

The stale-artifact caveat TASK-027 and TASK-030 recorded applied here too, so the
artifacts were purged before the confirming run. No mutation remains in the code.

### Documentation

```text
No documentation changes required.
```

Cross-checked the implemented mapping against `GAME_STATE.md` §2 (every
documented member present; nothing invented), §2.0.3 (no `Status` or lifecycle
value; asserted absent from the JSON), §2.1.7 (cell order, absence conventions),
§2.3 (PetState member set; no `PlayerState`; both loadouts), §2.4 (BossState),
`REDIS_STATE.md` §2 and §7, `ARCHITECTURE.md`, and `TDD.md` §4. The documents
already describe this contract, so this task brought code to the documentation
rather than the reverse (`AGENTS.md` §17). `REDIS_STATE.md` §7 was **not** edited
to claim persistence.

### Scope Verification

```text
No Redis persistence.
No PostgreSQL BattleState persistence.
No new API.
No SignalR protocol changes.
No gameplay.
No Match-3 changes.
No combat changes.
No Card gameplay.
No Relic gameplay.
No Boss gameplay.
No client-authoritative state.
```

Also confirmed: TASK-025/027/028/030 were not reopened; no new abstraction
(`UniversalStateSerializer`, `GameStateManager`, `BattleStateManager`,
`StatePersistenceEngine`) was introduced; the serializer is the smallest
architecture-compatible implementation.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed serialization computes no gameplay value and draws no RNG
- [x] Confirmed `RngSeed`/`RngState` round-trip without reseeding or advancing
- [x] Confirmed no Redis key, no PostgreSQL persistence, no SignalR/API change
- [x] Confirmed no `Status`/lifecycle member and no `StatusEffects[]` placeholder
- [x] Confirmed `tasks/completed/*` untouched

