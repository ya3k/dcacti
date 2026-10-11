# ADR-025: Swap Action Preconditions and Idempotency

**Status:** Accepted
**Date:** 2026-10-18
**Supersedes:** ADR-010

## Context

`MATCH3_RULES.md` §2.1.2 defines a four-check validation order for a player Swap, where Check 3 was designated as the already-applied / staleness check. Under `ADR-010`, this was implemented via `BattleState.LastCommittedSwapPair`, which recorded the unordered coordinate pair `{from, to}` of the most recently committed Swap.

However, `LastCommittedSwapPair` suffered from a fundamental semantic defect:
1. It persisted across Turn boundaries without clearing or state versioning. Consequently, if a player swapped cells `(12, 13)` on Turn N, they were permanently blocked from legitimately swapping `(12, 13)` on any subsequent Turn (Turn N+1, N+2, etc.) until a different swap committed, even when the new board layout produced a valid match at those coordinates.
2. It failed to protect against delayed/stale requests that named any coordinate pair other than the single recorded pair.
3. It could not distinguish legitimate consecutive-turn coordinate reuse from duplicate deliveries or retried requests whose acknowledgements were lost.

## Decision

Supersede `ADR-010` and replace the coordinate-based lock with an explicit **dual state-precondition and action-idempotency contract**:

1. **State Version Precondition (`expectedSequence`):**
   - The authoritative state version is `BattleState.Sequence` (integer), which increments by exactly 1 on every committed battle action (`Swap`, `CardCast`, `PetSkillCast`).
   - The client observes `Sequence` through state pushes (`BattleStateUpdated`), event batches (`ReceiveEvents`), or reconnect state snapshots (`GetBattleState`).
   - Every Swap action submitted by the client carries `expectedSequence`.
   - The server verifies `expectedSequence == state.Sequence`. If `expectedSequence != state.Sequence`, the action is evaluated against an obsolete state and is rejected as `STALE_ACTION`, performing zero state mutations and emitting zero events.

2. **Action Identity and Idempotency (`actionId` / `clientSequence`):**
   - Every logical action carries a unique client-generated action identifier (`actionId`, carried via the SignalR correlation parameter). The correlation parameter carries the precondition and the identity as an exact token — `"{expectedSequence}:{actionId}"`, or `"{actionId}"` alone for a caller that states no precondition — and a token outside those forms is refused as a transport contract violation rather than reinterpreted (`SIGNALR_PROTOCOL.md` §2 items 1–3, `TASK-257`).
   - `BattleState` records the most recent committed swap action (`LastCommittedSwap`, storing `MinCellIndex`, `MaxCellIndex`, `Sequence`, and `ActionId`).
   - When a request arrives with an `actionId` matching `LastCommittedSwap.ActionId`:
     - If the payload coordinates match the committed swap, the server recognizes an idempotent retry (e.g. following a lost SignalR acknowledgement) and returns `{ Accepted: true, Reason: null }` without re-executing, re-incrementing counters, or emitting duplicate events.
     - If the payload coordinates differ, the request is rejected with `INVALID_ACTION_PAYLOAD`.
   - Recognition is against that one recorded action, so the retry window is one committed Swap wide: a retry arriving after a later action committed no longer matches and is rejected as `STALE_ACTION` by decision 1. That window is a property of the state shape this decision keeps (one committed-action record), and widening it would require a separate decision.

3. **Retirement of Global Coordinate Restriction:**
   - Swapping the same unordered cell pair on consecutive turns (e.g., `(12, 13)` on Turn N and again on Turn N+1) is fully legal whenever the board at Turn N+1 permits it. Coordinate history is no longer used to reject swaps.

4. **Concurrency Safety:**
   - Optimistic concurrency control in `BattleStateService` uses Redis compare-and-set on `Sequence`.
   - If CAS fails due to concurrent writer contention, the service re-reads the fresh state:
     - If the fresh state's committed action matches `request.ActionId`, the action succeeded in the race and returns success.
     - If another action committed and advanced `Sequence > request.ExpectedSequence`, the precondition is consumed and the request fails immediately with `STALE_ACTION` rather than re-executing against fresh board state.
   - A request that states no precondition (the legacy `"{actionId}"` token form) cannot show that it observed any state version, so a refused compare-and-set is terminal for it as well: it is rejected as `STALE_ACTION` instead of being re-resolved against the newer state (`SIGNALR_PROTOCOL.md` §2 item 3, `REDIS_STATE.md` §4 item 2, `TASK-257`).

## Consequences

### Positive
- Standard Match-3 gameplay is restored: players can legitimately swap the same coordinates on consecutive turns.
- Stale requests from earlier board states are deterministically rejected.
- Retries and duplicate deliveries are safely deduplicated.
- Lost SignalR acknowledgements can be retried idempotently without double-execution.
- The server remains 100% authoritative for all gameplay decisions.

### Negative
- Swap request contracts on SignalR and Domain require `expectedSequence` and `actionId`.
- `BattleState` serialization includes the committed action record.
