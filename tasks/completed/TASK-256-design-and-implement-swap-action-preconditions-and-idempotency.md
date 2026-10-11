# TASK-256 — Design and Implement Swap Action Preconditions & Idempotency

<!--
  GEN-TASK EXECUTION MANIFEST TEMPLATE
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
-->

---

## Metadata

```text
Task ID:           TASK-256
Type:              ARCHITECTURE
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     backend
Supporting Agents: gameplay, client, testing, review
Workflow:          architecture/architecture-change.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis,
                   quality/documentation-consistency,
                   gameplay/authority-determinism-audit, testing/test-scenario-generation
Dependencies:      TASK-255 (SUPERSEDED)
Declared Files:    docs/03-decisions/ADR/ADR-025-swap-action-preconditions-and-idempotency.md,
                   docs/03-decisions/ADR/ADR-010-committed-swap-state.md,
                   docs/03-decisions/README.md,
                   docs/01-game-design/MATCH3_RULES.md,
                   docs/02-technical/GAME_STATE.md,
                   docs/02-technical/SIGNALR_PROTOCOL.md,
                   src/backend/GameServer.Domain/Match3/SwapRequest.cs,
                   src/backend/GameServer.Domain/Match3/CommittedSwapPair.cs,
                   src/backend/GameServer.Domain/Match3/SwapRejectionReason.cs,
                   src/backend/GameServer.Domain/Match3/SwapValidator.cs,
                   src/backend/GameServer.Domain/Match3/SwapExecution.cs,
                   src/backend/GameServer.Domain/Battle/BattleState.cs,
                   src/backend/GameServer.Domain/Battle/Serialization/BattleStateJson.cs,
                   src/backend/GameServer.Domain/Battle/Serialization/BattleStateSerializer.cs,
                   src/backend/GameServer.Application/Battle/BattleStateService.cs,
                   src/backend/GameServer.Api/Hubs/BattleHub.cs,
                   src/frontend/client/src/services/realtime/SignalRService.ts,
                   src/frontend/client/src/game/runtime/GameRuntime.ts,
                   src/frontend/client/src/game/scenes/BattleEventPresenter.ts,
                   tests/backend/GameServer.Domain.Tests/SwapExecutionTests.cs,
                   tests/backend/GameServer.Domain.Tests/SwapValidationTests.cs,
                   tests/backend/GameServer.Domain.Tests/CommittedSwapPairContractTests.cs,
                   tests/backend/GameServer.Domain.Tests/BattleStateSerializationTests.cs,
                   tests/backend/GameServer.Application.Tests/BattleStateServiceTests.cs,
                   tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs,
                   src/frontend/client/tests/SignalRService.test.ts,
                   src/frontend/client/tests/GameRuntime.test.ts,
                   src/frontend/client/tests/BattleEventPresentation.test.ts
```

---

## Objective

Resolve the `LastCommittedSwapPair` semantic defect so that DC Acti follows standard Match-3 gameplay:
- Players may legitimately swap the same unordered pair of cells on consecutive turns when the current board permits it.
- Delayed or stale requests from an earlier board state must not be executed against a newer state.
- Retrying the same logical action must not execute it twice.
- The server remains authoritative for all gameplay decisions.

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — Server authority
- `docs/03-decisions/ADR/ADR-004-signalr-realtime.md` — SignalR realtime transport
- `docs/03-decisions/ADR/ADR-008-battle-recovery.md` — Snapshot-based battle resync
- `docs/03-decisions/ADR/ADR-010-committed-swap-state.md` — Former committed-swap state (superseded by ADR-025)
- `docs/03-decisions/ADR/ADR-025-swap-action-preconditions-and-idempotency.md` — Approved protocol and architecture decision
- `docs/01-game-design/MATCH3_RULES.md` — Match-3 rules, validation order (§2.1.2), staleness & idempotency (§2.1.4)
- `docs/01-game-design/GAME_RULES.md` — Server authority (§18), Turn lifecycle (§2)
- `docs/02-technical/GAME_STATE.md` — Authoritative battle state, Sequence, commit records (§2.1.10, §5, §5.1)
- `docs/02-technical/SIGNALR_PROTOCOL.md` — Hub method signatures, acknowledgement and sequencing contracts (§2, §2.1, §5, §6)
- `tasks/TASK_LIFECYCLE.md` — Two-stage commit protocol (§6)

---

## Acceptance Criteria

- [x] Approved ADR-025 authored and ADR-010 marked superseded.
- [x] Documentation updated across `MATCH3_RULES.md`, `GAME_STATE.md`, and `SIGNALR_PROTOCOL.md`.
- [x] Backend Domain and Application models implement `expectedSequence` precondition and `actionId` idempotency.
- [x] `LastCommittedSwapPair` global coordinate lock removed so consecutive-turn coordinate reuse works legitimately.
- [x] SignalR transport and frontend client dispatch (`SignalRService`, `GameRuntime`) updated to supply preconditions and idempotency keys.
- [x] Safe optimistic CAS concurrency handling in `BattleStateService`.
- [x] All required regression scenarios verified:
  - Valid swap of pair A-B commits on turn N.
  - Valid swap of pair A-B commits on later turn when board permits.
  - Stale request with obsolete state version rejected without mutating state.
  - Reversing From and To does not bypass stale-action protection.
  - Repeating same logical action with same idempotency key does not execute twice.
  - Reusing idempotency key with different payload is rejected.
  - Two concurrent actions based on same state cannot both commit.
  - Retry after lost acknowledgement returns consistent result without duplicating gameplay effects.
  - Reconnect/resync followed by valid new swap works correctly.
  - Rejection does not consume turn or partially mutate state.
- [x] All backend and frontend unit tests pass.
- [x] Two-stage commit protocol (Phase A implementation slice, Phase B completion record) completed.

---

## Completion Evidence

### Commit
- Implementation slice (Phase A): `d97184e7a8869e0fa956dc811875b880c8104438`
- Subject: `feat(match3): implement swap action preconditions and idempotency`
- Owns: `TASK-256`
- Files: Exactly 28 declared files (see `## Metadata`)
- Shared with: `none`
- Unowned / pre-existing: `none`
- Completion record filing (Phase B): this completion record alone, filed under its own bookkeeping commit (`tasks/completed/TASK-256-design-and-implement-swap-action-preconditions-and-idempotency.md`).

### Changed Files
- `docs/03-decisions/ADR/ADR-025-swap-action-preconditions-and-idempotency.md`: Authored approved dual precondition and idempotency architecture (Accepted).
- `docs/03-decisions/ADR/ADR-010-committed-swap-state.md`: Marked superseded by ADR-025.
- `docs/03-decisions/README.md`: Indexed ADR-025 and marked ADR-010 superseded.
- `docs/01-game-design/MATCH3_RULES.md`: Updated §2.1.2 check 3 and §2.1.4 for expectedSequence precondition and actionId idempotency.
- `docs/02-technical/GAME_STATE.md`: Updated §2.1.10 documenting LastCommittedSwapPair sequence and actionId metadata.
- `docs/02-technical/SIGNALR_PROTOCOL.md`: Updated §2, §2.1, and §6.5 documenting Swap token format carrying expectedSequence and actionId.
- `src/backend/GameServer.Domain/Match3/SwapRequest.cs`: Added ExpectedSequence and ActionId properties.
- `src/backend/GameServer.Domain/Match3/CommittedSwapPair.cs`: Enriched with Sequence and ActionId metadata while preserving value equality.
- `src/backend/GameServer.Domain/Match3/SwapRejectionReason.cs`: Added InvalidActionPayload = 5 mapped to "INVALID_ACTION_PAYLOAD".
- `src/backend/GameServer.Domain/Match3/SwapValidator.cs`: Removed coordinate restriction, validates ExpectedSequence precondition.
- `src/backend/GameServer.Domain/Match3/SwapExecution.cs`: Exposed IdempotentReplay and Rejected factory methods, populated LastCommittedSwapPair with Sequence and ActionId.
- `src/backend/GameServer.Domain/Battle/BattleState.cs`: Updated documentation contract for LastCommittedSwapPair under ADR-025.
- `src/backend/GameServer.Domain/Battle/Serialization/BattleStateJson.cs`: Serializes Sequence and ActionId with WhenWritingDefault / WhenWritingNull.
- `src/backend/GameServer.Domain/Battle/Serialization/BattleStateSerializer.cs`: Deserializes Sequence and ActionId into CommittedSwapPair.
- `src/backend/GameServer.Application/Battle/BattleStateService.cs`: Deduplicates idempotency via ActionId and enforces ExpectedSequence precondition on initial read and CAS failure reload.
- `src/backend/GameServer.Api/Hubs/BattleHub.cs`: Extracts expectedSequence and actionId from client action token, dispatches to service.
- `src/frontend/client/src/services/realtime/SignalRService.ts`: Invokes Swap with formatted actionToken.
- `src/frontend/client/src/game/runtime/GameRuntime.ts`: Passes battleState.sequence as expectedSequence and nextClientSequence('swap') as clientSequence.
- `src/frontend/client/src/game/scenes/BattleEventPresenter.ts`: Added INVALID_ACTION_PAYLOAD friendly error message.
- `tests/backend/GameServer.Domain.Tests/SwapExecutionTests.cs`: Verified execution with sequence precondition and action idempotency.
- `tests/backend/GameServer.Domain.Tests/SwapValidationTests.cs`: Verified validation of ExpectedSequence and consecutive-turn coordinate reuse.
- `tests/backend/GameServer.Domain.Tests/CommittedSwapPairContractTests.cs`: Updated reflection tests to assert contract properties.
- `tests/backend/GameServer.Domain.Tests/BattleStateSerializationTests.cs`: Verified round-trip preservation of CommittedSwapPair metadata.
- `tests/backend/GameServer.Application.Tests/BattleStateServiceTests.cs`: Verified idempotent retries, payload conflicts, and sequence staleness.
- `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs`: Verified hub integration for precondition match/mismatch and idempotent event deduplication.
- `src/frontend/client/tests/SignalRService.test.ts`: Verified SignalR invocation signature and token formatting.
- `src/frontend/client/tests/GameRuntime.test.ts`: Verified runtime client sequencing and expectedSequence tracking.
- `src/frontend/client/tests/BattleEventPresentation.test.ts`: Verified presentation mapping for INVALID_ACTION_PAYLOAD.

### Validation Results
- `dotnet test src/backend/GameServer.sln --nologo`: PASS (3,053 tests across 4 projects: Domain 1,565; Infrastructure 476; Api 346; Application 666; 0 failed).
- `npm run test:run` (src/frontend/client): PASS (23 test files, 1,061/1,061 passed, 0 failed).
- `npm run build` (src/frontend/client): PASS (tsc && vite build).
- Phase A commit `d97184e7a8869e0fa956dc811875b880c8104438` verified to contain strictly the 28 declared files.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed P-1…P-6 commit policy compliance
