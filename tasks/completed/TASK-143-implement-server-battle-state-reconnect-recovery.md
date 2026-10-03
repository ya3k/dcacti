# TASK-143 — Implement Server Battle State Reconnect Recovery

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and task files by path and section.
-->

---

## Metadata

```text
Task ID:           TASK-143
Type:              FEATURE (TASK_TYPES.md §2 — "Implement a documented mechanic, capability, or system that already has a home in docs/ but has not yet been built.")
Status:            DONE
Risk:              MEDIUM (TASK_TYPES.md §4 — FEATURE baseline MEDIUM; touches SignalR Hub method, session player authorization, and Redis state retrieval)
Priority:          HIGH (ROADMAP.md Phase 3 "Reconnect / resync behavior"; SIGNALR_PROTOCOL.md §7, ADR-008)
Primary Agent:     realtime (TASK_TYPES.md §5 — SignalR/Redis domain → Realtime. Supporting: backend, testing, review)
Supporting Agents: backend, testing, review
Workflow:          development/feature.md
Skills:            realtime/realtime-protocol-validation,
                   backend/api-contract-validation,
                   testing/test-scenario-generation,
                   quality/architecture-conformance,
                   quality/implementation-review
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-040 (DONE — Redis active battle state persistence),
                   TASK-034 (DONE — JWT session authentication on BattleHub),
                   TASK-043 (DONE — BattleState player identity carriage),
                   TASK-133 (DONE — complete BattleState wire projection shape),
                   ADR-008 (Accepted — Snapshot-Based Battle Reconnection),
                   ADR-014 (Accepted — Battle State Player Identity),
                   ADR-015 (Accepted — Application Session Authentication)
Blocks:            Client-side reconnect and resync recovery orchestration
Estimate:          Normal
```

---

## Objective

Implement the server-authoritative `GetBattleState(string battleId)` Hub method in `BattleHub` per `SIGNALR_PROTOCOL.md` §7 and `ADR-008`. The method validates caller authentication, ensures caller ownership of the battle session, retrieves the active battle snapshot from Redis (`IBattleStateRepository`), and returns the wire-projected `BattleStateUpdatedPayload` (with `BattleState.PlayerId` excluded) along with `serverSequence`, or returns a rejection (`BATTLE_NOT_FOUND` / unauthorized) if the battle does not exist, has expired, or belongs to another player.

---

## Authoritative References

- `docs/00-overview/ROADMAP.md` Phase 3 — Reconnect / resync behavior (`SIGNALR_PROTOCOL.md`)
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§7 (Reconnect & Resync)** — `GetBattleState(battleId)` method contract, snapshot recovery over replay, `BATTLE_NOT_FOUND` error handling, and fallback boundary
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§1 (Connection & Authentication)** — JWT session authentication, caller player identity binding
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§4 (Initial State Delivery / Projection)** — `BattleStateUpdatedPayload` schema, exclusion of `PlayerId` (`GAME_STATE.md` §2.8), inclusion of `equippedCards`, `relicState`, and `statuseffects`
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§5 (Request/Response Acknowledgement)** — `accepted: true/false` response envelope and error reasons
- `docs/03-decisions/ADR/ADR-008-battle-recovery.md` — Snapshot-Based Battle Reconnection architectural decision
- `docs/03-decisions/ADR/ADR-014-battle-state-player-identity.md` — `BattleState.PlayerId` carriage and wire exclusion
- `docs/03-decisions/ADR/ADR-015-application-session-authentication-contract.md` — Authenticated player session authorization
- `docs/02-technical/REDIS_STATE.md` §2 (Active battle state format), §3 (TTL), §5 (Session Recovery)
- `docs/02-technical/GAME_STATE.md` §2.8 (`PlayerId` server-only carriage), §5.1 (Authoritative Snapshot)
- `docs/02-technical/ARCHITECTURE.md` §2.1, §4.1 (Hub delegation and backend boundaries)

---

## Scope

### In Scope

1. **Implement `GetBattleState(string battleId)` Hub method in `BattleHub` (`src/backend/GameServer.Api/Hubs/BattleHub.cs`):**
   - Validate input parameter `battleId` (non-null, non-whitespace).
   - Authenticate caller session via JWT claims (`Context.User`), extracting caller `PlayerId`.
   - Fetch the current active `BattleState` from `BattleStateService` / `IBattleStateRepository`.
   - Return `BATTLE_NOT_FOUND` if state does not exist in Redis or TTL expired (`SIGNALR_PROTOCOL.md` §7.3, `REDIS_STATE.md` §3/§5).
   - Verify caller ownership: if caller `PlayerId` != `BattleState.PlayerId`, return rejection / unauthorized response without leaking state (`ADR-014`, `ADR-015`).
   - Project the snapshot using the existing `ToPayload(state)` converter (`SIGNALR_PROTOCOL.md` §4, `BattleStateUpdatedPayload`), excluding server-only `PlayerId`.
   - Return structured response containing `accepted: true`, `serverSequence: state.Sequence`, and `state: BattleStateUpdatedPayload`.
2. **Add Application / Api Layer support if needed:**
   - Expose retrieval method on `BattleStateService` (e.g. `GetBattleStateAsync(battleId, playerId)`) delegating to `IBattleStateRepository`.
3. **Synchronize Documentation & XML Comments:**
   - Update `BattleHub.cs` class comments indicating `GetBattleState` (§7) is implemented.
   - Synchronize any `SIGNALR_PROTOCOL.md` implementation status notes if applicable.
4. **Comprehensive Automated Tests:**
   - Unit tests in `GameServer.Application.Tests` and `GameServer.Api.Tests` for `GetBattleState`.
   - Test cases for successful recovery with matching caller identity, rejection on mismatched player identity, `BATTLE_NOT_FOUND` on missing/expired key, and wire projection field completeness.

### Out of Scope

- Client-side reconnection handlers and Phaser scene resync orchestration in `SignalRService.ts` / `GameRuntime.ts` (follow-up client task).
- Event replay log, event sourcing, or message queue infrastructure (`ADR-008`, `ARCHITECTURE.md` §5.2).
- Modifying `POST /api/battle/start` or `GET /api/battle/{battleId}/result` REST endpoints.
- Modifying any game rule, combat calculation, or board resolution mechanic.
- Discord credentials secret hygiene (`TASK-036`).

---

## Current State

```text
BattleHub.cs:
  - JoinBattle(battleId) pushes BattleStateUpdated on connection join.
  - Swap, CardCast, PetSkillCast in-battle action methods are implemented.
  - GetBattleState(battleId) is documented in SIGNALR_PROTOCOL.md §7 and ADR-008, but is explicitly marked not implemented in BattleHub comments.
```

---

## Acceptance Criteria

- [x] `BattleHub` defines `public async Task<GetBattleStateResponse> GetBattleState(string battleId)` (or equivalent structured response) matching `SIGNALR_PROTOCOL.md` §7 / §5.
- [x] An unauthenticated connection or a request for a battle owned by a different `PlayerId` is rejected without returning battle state (`ADR-014`, `ADR-015`).
- [x] A request for a non-existent or expired battle returns `accepted: false` with reason `"BATTLE_NOT_FOUND"` (`SIGNALR_PROTOCOL.md` §7.3).
- [x] A valid request by the owning player returns `accepted: true`, the authoritative `serverSequence`, and the complete `BattleStateUpdatedPayload` snapshot (`SIGNALR_PROTOCOL.md` §4 / §7.1).
- [x] The returned payload excludes server-only `PlayerId` (`GAME_STATE.md` §2.8, `ADR-014`).
- [x] No server-authoritative gameplay logic is placed on the client (`AGENTS.md` §10 / `ADR-001`).
- [x] All new and existing unit and integration tests pass across `GameServer.sln`.
- [x] Quality review checklist passes (`quality/review.md` §1).

---

## Affected Files & Areas

```text
[x] src/backend/GameServer.Api/Hubs/BattleHub.cs (GetBattleState implementation and response DTOs)
[x] src/backend/GameServer.Application/Battle/BattleStateService.cs (GetBattleState query if needed)
[x] tests/backend/GameServer.Api.Tests/ (BattleHub GetBattleState integration tests)
[x] tests/backend/GameServer.Application.Tests/ (Service query tests)
[ ] docs/02-technical/SIGNALR_PROTOCOL.md (status sync if applicable)
```

---

## Implementation Notes

- Use existing `ToPayload(BattleState state)` in `BattleHub.cs` to guarantee identical wire projection between `BattleStateUpdated` push and `GetBattleState` response.
- Extract caller `PlayerId` from `Context.User` using the existing JWT authentication helpers (`ClaimsPrincipal` / `PlayerId`).
- `REDIS_STATE.md` §3/§5: `IBattleStateRepository.GetAsync(battleId)` returns `null` when the key has expired or does not exist.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — BattleStateService / BattleHub GetBattleState query validation
[x] Integration tests  — BattleHub GetBattleState over test SignalR hub connection:
                         1. Successful snapshot retrieval matching player
                         2. BATTLE_NOT_FOUND when battleId does not exist in Redis
                         3. Unauthorized / rejection when caller PlayerId does not match BattleState.PlayerId
                         4. Correct wire schema (serverSequence + payload without PlayerId)
[ ] Gameplay scenarios — N/A: transport/recovery mechanism only
```

### Key Edge Cases

- Reconnecting after a completed Swap (Sequence advanced, HP/Power/StatusEffects/EquippedCards modified) returns exact current Redis snapshot.
- Expired Redis TTL returns `BATTLE_NOT_FOUND` rather than throwing an unhandled exception.

---

## Stop Conditions

- If `SIGNALR_PROTOCOL.md` §7 or `ADR-008` conflicts with existing hub protocol: STOP per `AGENTS.md` §4.
- If caller authentication or ownership model is ambiguous: STOP per `AGENTS.md` §20.
- If task attempts to introduce event replay / event sourcing: STOP per `ADR-008` and `ARCHITECTURE.md` §5.2.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Changed Files
- `src/backend/GameServer.Api/Hubs/BattleHub.cs` — added `GetBattleStateResponse` (the §5
  acknowledgement carrying `accepted`, `serverSequence`, `state`, `reason`) and the
  `GetBattleState(string battleId)` Hub method. The method reads the caller's `PlayerId`
  from the already-validated principal (`AuthenticatedPlayer.GetPlayerIdFromPrincipal`),
  delegates the store read and the ownership comparison to the Application layer, and
  returns the snapshot through the existing `ToPayload(BattleState)` §4 projection. The
  class-level comment that recorded `GetBattleState` as not implemented is corrected.
- `src/backend/GameServer.Application/Battle/BattleStateService.cs` — added
  `GetOwnedBattleStateAsync(battleId, callerPlayerId)`: reads the authoritative record
  through `IBattleStateRepository.GetAsync` and returns it only when
  `BattleState.PlayerId` equals the authenticated caller's identity (ordinal). Unknown,
  expired, and foreign all report `null` — one indistinguishable answer. It performs no
  write.
- `tests/backend/GameServer.Application.Tests/BattleStateServiceReconnectRecoveryTests.cs`
  — new: 10 tests for the ownership read, absence, no-write/no-`Sequence`-advance, and
  the foreign≡unknown indistinguishability.
- `tests/backend/GameServer.Api.Tests/Hubs/BattleHubReconnectRecoveryTests.cs` — new:
  15 tests over a real SignalR hub connection — successful recovery, post-resolution
  snapshot, caller-only delivery with no event/replay broadcast, §4 projection
  compatibility, `PlayerId` exclusion, foreign rejection, unknown battle, unavailable
  state, authentication, and the no-event-replay surface.
- `tests/backend/GameServer.Api.Tests/RedisBattleRecoverySmokeTest.cs` — new: the
  end-to-end run against a REAL Redis, asserting the documented
  `battle:{battleId}:state` key directly (and that it is the only key), `PlayerId`
  absent from the wire while present in the record, and a genuinely elapsed TTL
  yielding `BATTLE_NOT_FOUND`.
- `tests/backend/GameServer.Api.Tests/SignalRWireJson.cs` — new test helper that
  serializes a hub payload as the SignalR JSON protocol does, so wire member-name
  assertions test the wire contract rather than CLR property spelling.
- `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs` — `GetBattleState`
  removed from `BattleHub_ShouldNotRegisterGameplayMethods`'s absent-method list,
  because §7's method is now a real invokable hub method; the remaining entries and the
  rationale are unchanged.

### Validation Results
- `dotnet test src/backend/GameServer.sln` — PASS (2,606 passed / 0 failed / 0 skipped)
  - `GameServer.Application.Tests` — PASS (489)
  - `GameServer.Domain.Tests` — PASS (1,463)
  - `GameServer.Infrastructure.Tests` — PASS (363; a local Redis was reachable, so the
    real `REDIS_STATE.md` §1–§4 key/TTL/compare-and-set tests actually ran and none was
    skipped)
  - `GameServer.Api.Tests` — PASS (291; includes the 15 new hub recovery tests and the
    real-Redis `RedisBattleRecoverySmokeTest`)
- `npm run test:run` (`src/frontend/client`) — PASS (477 passed)
- No production TTL was modified. The expiry case is exercised by collapsing the TTL of
  the smoke test's own key through a raw Redis client and waiting for it to elapse.

### Acceptance Criteria
- [x] `BattleHub.GetBattleState(string battleId)` returns the documented structured
  response (`accepted` / `serverSequence` / `state` / `reason`) per §7 and §5.
- [x] An unauthenticated connection, or a request for a battle owned by a different
  `PlayerId`, is rejected without returning battle state.
- [x] A non-existent or expired battle returns `accepted: false` with
  `"BATTLE_NOT_FOUND"` (§7.3).
- [x] A valid request by the owning player returns `accepted: true`, the authoritative
  `serverSequence`, and the complete §4 snapshot.
- [x] The returned payload excludes the server-only `PlayerId` (§2.8, ADR-014).
- [x] No server-authoritative gameplay logic was placed on the client.
- [x] All new and existing unit and integration tests pass across `GameServer.sln`.
- [x] Quality review checklist (`quality/review.md` §1): serialization of the new DTO
  names every member explicitly, the hub stays a delegation boundary, and no new
  abstraction, Redis contract, or gameplay rule was introduced.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (no client file was modified)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] No new Redis key (`battle:{battleId}:state` remains the only one; the real-Redis
  smoke test asserts the key set is exactly `[battle:{battleId}:state]`)
- [x] No PostgreSQL BattleState persistence
- [x] No event replay or event sourcing (ADR-008's snapshot model; a test asserts no
  replay method exists)
- [x] No gameplay rule changed (no `Domain/` gameplay file was modified by this task)
