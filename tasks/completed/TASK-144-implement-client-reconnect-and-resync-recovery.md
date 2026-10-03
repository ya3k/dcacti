# TASK-144 — Implement Client Reconnect & Resync Recovery

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and task files by path and section.
-->

---

## Metadata

```text
Task ID:           TASK-144
Type:              FEATURE (TASK_TYPES.md §2 — "Implement a documented mechanic, capability, or system that already has a home in docs/ but has not yet been built.")
Status:            DONE
Risk:              MEDIUM (TASK_TYPES.md §4 — FEATURE baseline MEDIUM; touches the connection lifecycle, the runtime's synchronized-state ingestion path, and a historical boundary assertion that this task reverses)
Priority:          HIGH (ROADMAP.md Phase 3 "Reconnect / resync behavior"; SIGNALR_PROTOCOL.md §7, ADR-008)
Primary Agent:     client (TASK_TYPES.md §5 — client-side SignalR consumption and presentation state → Client. Supporting: realtime, testing, review)
Supporting Agents: realtime, testing, review
Workflow:          development/feature.md
Skills:            realtime/realtime-protocol-validation,
                   client/client-state-authority,
                   client/client-event-projection,
                   testing/test-scenario-generation,
                   quality/implementation-review
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-143 (DONE — server GetBattleState recovery endpoint),
                   TASK-120 (DONE — client CardCast/PetSkillCast action paths),
                   TASK-086 (DONE — client application session establishment),
                   TASK-077 (DONE — client battle-start orchestration),
                   TASK-087 (DONE — client battle outcome / result scene),
                   SIGNALR_PROTOCOL.md §7 (surface complete),
                   ADR-008 (Accepted — Snapshot-Based Battle Reconnection),
                   ADR-014 (Accepted — Battle State Player Identity),
                   ADR-015 (Accepted — Application Session Authentication)
Blocks:            None verified. ROADMAP.md Phase 3's remaining items
                   (Discord Activity SDK integration, meta progression UI,
                   balance pass) are independent of this task.
Estimate:          Normal
```

---

## Objective

Implement the client half of the documented reconnect/resync mechanism: add the transport operation for the already-implemented `GetBattleState(battleId)` Hub method to `SignalRService`, and have `GameRuntime` request that snapshot after a SignalR reconnect when a current battle is known, routing the response through the existing `receiveBattleState()` ingestion path so the recovered snapshot replaces the runtime's synchronized copy per `SIGNALR_PROTOCOL.md` §7 and `ADR-008`. `BattleScene` continues to observe the recovered state through its existing `runtime.onBattleState(...)` subscription and requires no change.

---

## Authoritative References

- `docs/00-overview/ROADMAP.md` Phase 3 ("Reconnect / resync behavior (`SIGNALR_PROTOCOL.md`)")
- `docs/00-overview/MVP_SCOPE.md` §1 — scope confirmation
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§7 (Reconnect & Resync)** — items 1–3: the client calls `GetBattleState(battleId)`; the client discards local prediction and re-renders from the snapshot; `BATTLE_NOT_FOUND` means the battle is treated as ended and the client falls back to `GET /api/battle/{battleId}/result`
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§0 (State Delivery Stages)** — the three delivery shapes; `GetBattleState` is the client-requested snapshot path and is not interchangeable with the §4 push
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§1 (Connection)** — session-authenticated connection; the hub defines no second authentication mechanism
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§5 (Request/Response Acknowledgement)** — the `accepted`/`reason` envelope and its caller-only, unsequenced, non-event status
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§8 items 7–8** — no new method or message, and no event log or replay channel; a desynchronized client resynchronizes from a snapshot only
- `docs/02-technical/GAME_STATE.md` **§5.1 (Authoritative Snapshot)**, **§5.2 (`Sequence` is not the client correlation id)**, **§5.3 (Reconnect and Snapshot Compatibility)**
- `docs/02-technical/GAME_STATE.md` **§2.8** — `PlayerId` is server-only and excluded from the wire
- `docs/02-technical/ARCHITECTURE.md` **§2.2 rule 3** (Phaser scenes never depend on transport), **§2.2.1** (one runtime, one connection), **§5.2** (no event-sourcing infrastructure)
- `docs/02-technical/TDD.md` **§6** (determinism; a recovered session behaves consistently)
- `docs/02-technical/REDIS_STATE.md` **§3 (TTL / Lifecycle)** and **§5 (Session Recovery)** — the expiry condition that produces `BATTLE_NOT_FOUND`
- `docs/02-technical/API_CONTRACTS.md` §4 — the battle-result fallback endpoint the §7.3 behavior names
- `docs/03-decisions/ADR/ADR-008-battle-recovery.md` — snapshot-over-replay; the client discards prediction and re-renders; no event log
- `docs/03-decisions/ADR/ADR-014-battle-state-player-identity.md` — `PlayerId` carriage and wire exclusion
- `docs/03-decisions/ADR/ADR-015-application-session-authentication-contract.md` — the session credential the hub connection presents

---

## Scope

### In Scope

1. **Transport operation in `SignalRService` (`src/frontend/client/src/services/realtime/SignalRService.ts`):**
   - Add the documented `getBattleState(battleId)` operation, invoking the existing `GetBattleState` Hub method on the existing connection.
   - Model the documented response envelope (`accepted`, `serverSequence`, `state`, `reason?`) as a transport-level type only, mirroring the existing `SwapResult` / `CardCastAcknowledgement` / `PetSkillCastAcknowledgement` treatment: returned verbatim, with no interpretation, no defaults, and no state mutation.
   - Reuse the existing `BattleStateUpdatedPayload` interface for the `state` member. `SIGNALR_PROTOCOL.md` §0 and TASK-143's implementation guarantee the §7 snapshot and the §4 push share one projection, so a second wire model would be a duplicate shape.
   - Correct the class comment that records reconnect recovery as out of scope, so the file's stated status matches the protocol it implements.

2. **Recovery orchestration in `GameRuntime` (`src/frontend/client/src/game/runtime/GameRuntime.ts`):**
   - In the `onReconnected` handler, when the runtime holds a current battle, request `GetBattleState(battleId)`.
   - Route an accepted response through the **existing** `receiveBattleState()` method so the recovered snapshot is ingested by the same path, and produces the same `battle_state_changed` notification and `battle_state_changed` listener dispatch, as the §4 push already does. Do not add a second ingestion path and do not add a second state store.
   - Handle the `BATTLE_NOT_FOUND` case per `SIGNALR_PROTOCOL.md` §7.3.
   - Keep the battle identity sourced from the runtime's own synchronized state, exactly as `requestAction` and `startBattle` already do — the caller never supplies a `battleId`.

3. **Runtime port / events (`src/frontend/client/src/game/runtime/GameRuntimeEvents.ts`):**
   - Add only what presentation genuinely requires to observe recovery. Where the existing `onBattleState` subscription already suffices, add nothing.
   - Do not introduce a recovery-specific authoritative state model, a second state store, or a new authoritative-state concept.

4. **Boundary test correction (`src/frontend/client/tests/RuntimeBoundaries.test.ts`):**
   - Update the permitted hub-invocation list so `GetBattleState` is recognized as a documented client invocation.
   - Leave the assertion that `BattleScene` has no direct SignalR dependency intact.

5. **Tests** for the transport operation, the recovery orchestration, and the boundary are as specified in Testing Requirements.

### Out of Scope

- **Any change under `src/backend/`.** The server contract is complete (`TASK-143`). If a documentation-supported defect in it is discovered, STOP per Stop Conditions rather than expanding this task.
- **Any new SignalR hub method or event**, including `BattleStateRecovered`, `ReconnectComplete`, `ResyncComplete`, `BattleRecovery`, or `BattleStateResynced`. The documented `GetBattleState` request and the existing snapshot contracts are the whole mechanism (`SIGNALR_PROTOCOL.md` §8 items 7–8).
- **Event replay, an event log, or event sourcing** (`ADR-008`, `ARCHITECTURE.md` §5.2). The client re-renders from the snapshot and must not attempt to replay missed events.
- **`BattleScene.ts`**, unless the authoritative documentation proves a required presentation change is missing. Its `runtime.onBattleState(...)` subscription is already the presentation path for `battle_state_changed`.
- Any new Redis key or Redis contract.
- Any modification to `GAME_STATE.md`, `SIGNALR_PROTOCOL.md`, or any other source-of-truth document.
- Any modification to a completed task file, including `TASK-120`. Completed tasks are immutable (`TASK_LIFECYCLE.md` §3).
- All gameplay: board generation, swap, match detection, cascade, gravity, combo, damage, Crit, Burn, Pet Skills, Cards, Relics, Passives, Boss behavior, XP, progression, rewards.
- All items `MVP_SCOPE.md` §2 lists as OUT.

---

## Current State

The server recovery path is complete and TASK-143 is `DONE`: `BattleHub.GetBattleState(string battleId)` returns the §5 envelope (`accepted` / `serverSequence` / `state` / `reason`) with the §4 `BattleStateUpdatedPayload` projection, `PlayerId` excluded, and `BATTLE_NOT_FOUND` for unknown, expired, and foreign battles. Its Implementation Notes record that `GetBattleState` reuses the same `ToPayload(BattleState)` converter as the §4 push, so both paths emit an identical wire shape.

The client half is absent. Verified in the current source:

```text
SignalRService.ts
  - connect() builds the connection with withAutomaticReconnect() and a
    session-token accessTokenFactory; onreconnecting / onreconnected / onclose
    are wired to SignalRConnectionHandlers.
  - swap, cardCast, petSkillCast, joinBattle, ping are the implemented
    invocations. getBattleState does not exist.
  - The class comment states: "Reconnect recovery (GetBattleState, §7) remains
    out of scope."

GameRuntime.ts
  - onReconnected currently sets connection: 'connected', sync: 'awaiting_battle',
    runtime: 'ready' — it does not request a snapshot, and by writing
    'awaiting_battle' it discards the fact that a synchronized battle exists.
  - receiveBattleState(payload) is the existing ingestion path: it validates
    the §4 shape, stores this.battleState, sets sync: 'synchronized', emits
    battle_state_changed, and notifies battleStateListeners. It is reached only
    from the 'BattleStateUpdated' subscription registered in
    registerTransportSubscriptions().
  - requestAction already resolves the battle id from this.battleState.battleId
    and documents that the caller cannot supply one the runtime never received.
  - The class comment states that every action kind "including GetBattleState"
    still rejects with RuntimeActionNotImplementedError.

BattleScene.ts
  - Reads the runtime through the GameRuntime port only; imports no transport
    type. Subscribes via runtime.onBattleState(...) at line 152 and re-renders.

RuntimeBoundaries.test.ts
  - Asserts the invoked hub-method set equals
    ['CardCast', 'JoinBattle', 'PetSkillCast', 'Ping', 'Swap'] and asserts
    expect(code).not.toMatch(/'GetBattleState'/) — a historical TASK-120
    assertion that the current authoritative protocol now supersedes.
```

---

## Acceptance Criteria

- [ ] TASK-144 implements client reconnect/resync according to `SIGNALR_PROTOCOL.md` §7.
- [ ] `SignalRService` exposes the documented `GetBattleState` operation as `getBattleState(battleId)`.
- [ ] `getBattleState` uses the existing SignalR connection and adds no second connection path.
- [ ] `getBattleState` performs no gameplay calculation: it invokes the hub method and returns the documented response verbatim, mutating nothing.
- [ ] The response envelope preserves the documented members (`accepted`, `serverSequence`, `state`, `reason?`) and the `state` member reuses the existing `BattleStateUpdatedPayload` type rather than a new duplicate model.
- [ ] `GameRuntime` requests recovery after reconnect when a current battle is known.
- [ ] Recovery uses the authoritative `GetBattleState` snapshot; no state is reconstructed from previously received events.
- [ ] Recovery reuses the existing `receiveBattleState()` ingestion path rather than adding a second ingestion path.
- [ ] Runtime state is replaced/synchronized from the authoritative snapshot, including `sync` reaching `'synchronized'` and the `battle_state_changed` notification being emitted.
- [ ] Local prediction is not treated as authoritative after recovery.
- [ ] `BattleScene` does not connect directly to SignalR.
- [ ] The existing `BattleScene` `runtime.onBattleState(...)` subscription remains the presentation path, and `BattleScene.ts` is unmodified (or a modification is justified by a cited authoritative requirement).
- [ ] `serverSequence` is preserved according to the documented contract.
- [ ] `clientSequence` is not confused with `serverSequence`: no client correlation id is seeded from, compared with, or required to equal it.
- [ ] `BATTLE_NOT_FOUND` follows the documented §7.3 behavior.
- [ ] No event replay is introduced.
- [ ] No new SignalR event is introduced.
- [ ] No new Redis contract is introduced.
- [ ] No client-authoritative state is introduced (`AGENTS.md` §10, ADR-001).
- [ ] `RuntimeBoundaries.test.ts` reflects the now-documented `GetBattleState` invocation while still asserting `BattleScene` has no direct SignalR dependency.
- [ ] `SignalRService` tests cover successful `GetBattleState`.
- [ ] `GameRuntime` tests cover reconnect recovery.
- [ ] `GameRuntime` tests cover authoritative snapshot replacement.
- [ ] `GameRuntime` tests cover `BATTLE_NOT_FOUND` recovery behavior.
- [ ] Existing frontend regression tests remain green.
- [ ] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)   — NOT EXPECTED: TASK-143 is complete
[x] src/frontend/client/src/services/realtime/SignalRService.ts (getBattleState + response type)
[x] src/frontend/client/src/game/runtime/GameRuntime.ts          (onReconnected recovery)
[x] src/frontend/client/src/game/runtime/GameRuntimeEvents.ts    (port addition only if required)
[x] src/frontend/client/src/game/scenes/BattleScene.ts           — EXPECTED UNCHANGED; change only if documented
[x] tests/ (unit / integration / gameplay scenarios)             — src/frontend/client/tests/
[ ] docs/ (documentation updates if applicable)                  — NOT EXPECTED: no contract change
```

---

## Implementation Notes

- **The wire shape already exists on the client.** `receiveBattleState()` and `readBattleState()` (`GameRuntime.ts`) already validate the §4 `BattleStateUpdatedPayload` shape, and `SignalRService` already declares that interface. Because TASK-143 routed `GetBattleState` through the same `ToPayload(BattleState)` converter as the §4 push, recovery can call `receiveBattleState()` with the response's `state` member unchanged. Introducing a second validator or a recovery-specific model would create exactly the second wire shape `SIGNALR_PROTOCOL.md` §8.1 rules out.
- **Ingestion path, not a new pathway.** `receiveBattleState()` is currently private and reached only from the `BattleStateUpdated` subscription. Routing recovery through it is what makes `sync: 'synchronized'`, the `battle_state_changed` event, and the `battleStateListeners` dispatch happen without new plumbing.
- **`onReconnected` currently discards synchronization.** It writes `sync: 'awaiting_battle'` unconditionally. A runtime that had a synchronized battle must not remain in that state after a successful recovery; a runtime that never had a battle must not fabricate one.
- **Battle identity comes from the runtime.** Use `this.battleState?.battleId`, as `requestAction` does. Do not add a caller-supplied `battleId`, a cached copy, or a separately tracked identity.
- **`sync` status values are already defined** in `src/frontend/client/src/state/GameRuntimeState.ts` (`'unsynchronized' | 'awaiting_battle' | 'synchronized'`). Do not add a recovery-specific sync value — the documented snapshot semantics are covered by `'synchronized'`.
- **`RuntimeBoundaries.test.ts` is the one intentional reversal.** It currently asserts `expect(code).not.toMatch(/'GetBattleState'/)` (line 346) and an invoked-method array at line 337. Both must reflect the current authoritative protocol. This supersedes a TASK-120 *assertion*, not TASK-120 itself: completed tasks are immutable (`TASK_LIFECYCLE.md` §3), so the history stands and only the live test moves.
- **Concurrency: introduce nothing.** Neither `SIGNALR_PROTOCOL.md` §7 nor any other authoritative document defines special semantics for overlapping `GetBattleState` requests, and §6's ordering guarantees are about action resolution. Follow the precedent `startBattle` already documents in this file for the same class of gap ("no document defines the behavior of overlapping calls, so none is invented here"): add no deduplication protocol, no concurrency mechanism, no request-cancellation semantics, and no duplicate-request guard. Where existing structure naturally prevents duplicates, preserve it. Record in Completion Evidence that no additional duplicate-request semantics were introduced.
- **Stale-snapshot comparison: introduce nothing.** `GAME_STATE.md` §5.2 item 3 and `SIGNALR_PROTOCOL.md` §2 item 1 establish that `Sequence` is not the client's correlation id, and the runtime already keeps `clientSequenceCounter` deliberately separate. The response's `serverSequence` is documented state metadata, not a comparison input: do not invent a "snapshot is stale, discard it" rule, do not increment the sequence client-side, and do not reject a snapshot on a sequence comparison.
- **§7.3's fallback already exists client-side.** `GET /api/battle/{battleId}/result` is implemented (`TASK-041`, `TASK-087`). §7.3 requires the client to *treat the battle as ended and fall back*, so reuse the existing result path rather than inventing a new error state or exposing any Redis detail.
- **Authentication is already handled.** The connection presents the application session through `accessTokenFactory`, and `ADR-015` D6 keeps authentication at the boundary rather than in the hub. No auth code belongs in this task.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — SignalRService.getBattleState: successful invocation,
                          verbatim documented response handling, transport-only
                          behavior (nothing mutated, nothing interpreted),
                          no second connection created, connection-state
                          precondition behavior consistent with the sibling
                          swap/cardCast/petSkillCast operations.
[x] Unit tests         — GameRuntime recovery: reconnect with a known battle
                          issues the recovery request; reconnect with no known
                          battle issues no request and fabricates no state;
                          successful recovery stores the authoritative snapshot;
                          runtime state replacement (prior synchronized state is
                          replaced, not merged); the sync transition reaches
                          'synchronized'; the battle_state_changed notification
                          and listener dispatch occur; BATTLE_NOT_FOUND follows
                          §7.3; no local authoritative calculation.
[x] Integration tests  — Runtime boundaries: 'GetBattleState' appears as a
                          permitted documented hub invocation, while BattleScene
                          retains no direct SignalR dependency.
[ ] Gameplay scenarios — N/A: transport/recovery mechanism only; no gameplay
                          rule is involved and none may be added.
```

### Key Edge Cases

- Reconnect while a battle is known and synchronized: the recovered snapshot replaces the runtime's copy and `sync` is `'synchronized'` afterwards, not `'awaiting_battle'`.
- Reconnect when no battle is known: no recovery request is issued and no battle state is invented.
- `accepted: false` with `reason: "BATTLE_NOT_FOUND"`: the documented §7.3 path is taken and no battle state is fabricated.
- A malformed or incomplete `state` member: handled exactly as the existing `BattleStateUpdated` ingestion already handles it, with no new fabricated state.
- The recovered snapshot carries a `serverSequence`; no client code seeds, compares, or increments it (`GAME_STATE.md` §5.2 item 3).
- `BattleScene` continues to receive recovered state through `runtime.onBattleState(...)` with no new subscription and no transport import.
- Expired-battle and foreign-battle responses are indistinguishable to the client, exactly as the server contract defines them; the client must not infer which occurred.

---

## Stop Conditions

- If reconnect semantics conflict across authoritative documents: STOP per `AGENTS.md` §4.
- If snapshot replacement semantics are ambiguous: STOP per `AGENTS.md` §20.
- If `serverSequence` handling is undefined where implementation requires a decision: STOP per `AGENTS.md` §20.
- If battle identity across reconnect is unavailable: STOP per `AGENTS.md` §20.
- If authentication/session behavior conflicts with existing contracts: STOP per `AGENTS.md` §20.
- If `BATTLE_NOT_FOUND` handling conflicts with `SIGNALR_PROTOCOL.md` §7.3: STOP per `AGENTS.md` §4.
- If a new SignalR method or event is required: STOP per `SIGNALR_PROTOCOL.md` §8 items 7–8.
- If backend changes become necessary: STOP. `TASK-143` is complete, and any defect in the server contract is a separate finding, not a silent expansion of this task.
- If a new Redis contract becomes necessary: STOP per `AGENTS.md` §20.
- If implementation requires gameplay changes: STOP per `AGENTS.md` §7 and §10.
- If implementation requires a new architecture decision: STOP per `AGENTS.md` §18.
- If task requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10.
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Implementation
- `src/frontend/client/src/services/realtime/SignalRService.ts` — added the
  `BattleStateSnapshotResponse` transport envelope (`accepted`,
  `serverSequence`, `state`, `reason`) reusing `BattleStateUpdatedPayload` for
  `state`, and the documented `getBattleState(battleId)` operation invoking
  `GetBattleState` on the existing connection. It returns the response verbatim;
  it calculates nothing, merges nothing, seeds/compares/increments no
  `serverSequence`, and creates no second connection. The class comment that
  recorded §7 as out of scope is corrected.
- `src/frontend/client/src/game/runtime/GameRuntime.ts` — `onReconnected` now
  requests recovery when a battle is known (and no longer demotes a runtime that
  holds a battle to `awaiting_battle`). Added `recoverBattleState()` (an accepted
  response is routed through the existing `receiveBattleState()`), the
  `BATTLE_NOT_FOUND_REASON` constant, and `handleBattleNotRecoverable()` for
  §7.3. The stale "including `GetBattleState`" claim in the `requestAction`
  docblock was corrected.
- `src/frontend/client/src/game/runtime/GameRuntimeEvents.ts` — comment/message
  corrections only: the port and `RuntimeActionNotImplementedError` no longer
  state that §7's snapshot is unimplemented; it is requested by the runtime's own
  reconnect handling and is still not a caller-submitted action kind. No new
  event type, port member, or state model.
- `src/frontend/client/tests/RuntimeBoundaries.test.ts` — `GetBattleState` is
  now an allowed documented invocation (invoked-method set updated); added the
  assertion that `BattleScene` names no hub method and keeps using
  `onBattleState`. The `BattleScene` transport-independence assertions are
  unchanged.
- `src/frontend/client/tests/SignalRService.test.ts` — new
  `GetBattleState reconnect snapshot transport` block (6 tests).
- `src/frontend/client/tests/GameRuntime.test.ts` — `FakeSignalR` gained the
  `getBattleState(battleId)` surface; new
  `reconnect & resync recovery (SIGNALR_PROTOCOL.md §7, ADR-008)` block
  (11 tests).
- `src/frontend/client/src/game/scenes/BattleScene.ts` — **unchanged**.

### Reconnect
```text
SignalR reconnect → GetBattleState
The handler is `GameRuntime`'s `onReconnected` (SIGNALR_PROTOCOL.md §7.1,
ADR-008). It records the connection transition first (`connection: 'connected'`,
new/retained `connectionId`, `runtime: 'ready'`) and then dispatches
`recoverBattleState()`. Recovery is requested if and only if the runtime holds a
current battle: the id is `this.battleState.battleId`, the same runtime-owned
source `requestAction` uses — no caller-supplied, URL, localStorage, React, or
scene-sourced id, and no second battle-id store. `sync` becomes
`'unsynchronized'` for a runtime that held a battle (the snapshot request is what
restores synchronization) and `'awaiting_battle'` for one that never had one.
With no current battle known, no request is issued at all and no battle is
fabricated.
```

### Recovery
```text
GetBattleState → authoritative snapshot → GameRuntime
The response members consumed are `accepted`, `state`, and `reason`;
`serverSequence` is carried by the transport type and is deliberately unused by
the runtime (no seeding from, comparison with, or increment of it —
GAME_STATE.md §5.2 item 3). An accepted response is passed to the existing
`receiveBattleState(payload)` method — the same private ingestion path the §4
`BattleStateUpdated` subscription uses — so the recovered snapshot is validated
by the existing §4 reader, `this.battleState` is replaced wholesale (never
merged with the client's prior copy, and no missed event is replayed), `sync`
reaches `'synchronized'`, the existing `battle_state_changed` runtime event is
emitted, and `battleStateListeners` are dispatched. `BattleScene` observes it
through its existing `runtime.onBattleState(...)` subscription. A malformed
`state` is handled exactly as the §4 push already handles it: a technical
`runtime_error` is reported and nothing is fabricated. A transport failure is
reported the same way and leaves the previous copy untouched. No state-sync
method, event, store, or validator was added, and no duplicate-request guard or
cancellation was introduced (none is documented).
```

### Presentation
```text
GameRuntime → BattleScene
`src/frontend/client/src/game/scenes/BattleScene.ts` is unmodified. No
authoritative requirement proved a presentation change necessary: the scene
already subscribes with `runtime.onBattleState(...)` (line 152) and re-renders on
every notification, which is exactly what `receiveBattleState()` triggers. It
imports no transport type and names no hub method, which the updated
`RuntimeBoundaries.test.ts` now asserts explicitly for `GetBattleState`.
```

### Tests
- `npm run test:run` (`src/frontend/client`) — PASS (495 tests, 18 files)
  - `tests/SignalRService.test.ts` — PASS (42; includes 6 new `GetBattleState` tests: invocation with exactly `['battleId']`, connection reuse with no second connection, verbatim envelope return, `BATTLE_NOT_FOUND` with no state, disconnected precondition, transport-only surface/sequence scan)
  - `tests/GameRuntime.test.ts` — PASS (103; includes 11 new reconnect/resync tests: request-after-reconnect with a known battle, no request with no battle, ingestion through `receiveBattleState` with `battle_state_changed` + listener dispatch + `sync: 'synchronized'`, stale-copy replacement with no merge, no invented stale-snapshot comparison, `BATTLE_NOT_FOUND` → ended + existing result fallback, no store/ownership detail exposed, malformed snapshot reported without fabrication, transport failure reported without fabrication, no event replay, no client-side calculation/`serverSequence` increment)
  - `tests/RuntimeBoundaries.test.ts` — PASS (39; the invoked-method set is now `['CardCast','GetBattleState','JoinBattle','PetSkillCast','Ping','Swap']`, plus the `BattleScene` transport-independence assertion)
  - `tests/SceneLifecycle.test.ts` — PASS (70; the pre-existing assertion that `BattleScene` contains no `'GetBattleState'` still holds, because the file is unchanged)
  - `tests/ResultScene.test.ts` — PASS (16); `tests/BattleService.test.ts` — PASS (37) (`ApiService.getBattleResult`, the route §7.3 reuses, is unchanged)
  - remaining suites — PASS (`CollectionService` 27, `LobbyScene` 44, `BattleEventPresentation` 37, `ApplicationSession` 11, `StatusOverlay` 9, `PhaserGame` 9, `AppLifecycle` 13, `ViewportCss` 8, `ViewportDebugOverlay` 3, `GameShell` 6, `GameViewport` 18, `DiscordService` 3)
- `npx tsc --noEmit -p tsconfig.json` — PASS (no errors)
- The backend suite was not run: no file under `src/backend/` (or `tests/backend/`) was modified by this task.

### Scope
- [x] No gameplay logic added or changed
- [x] No backend changes (`src/backend/` untouched)
- [x] No new SignalR method or event
- [x] No new Redis key or contract
- [x] No event replay or event log introduced
- [x] No client-authoritative state introduced
- [x] No additional duplicate-request semantics introduced (none is documented)
- [x] No source-of-truth document modified

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] `SIGNALR_PROTOCOL.md` §7.3 followed: `BATTLE_NOT_FOUND` clears the runtime's
  synchronized copy (the battle is treated as ended) and the fallback is the
  existing `GET /api/battle/{battleId}/result` route
  (`ApiService.getBattleResult`), not a new recovery mechanism; unknown, expired,
  and foreign remain indistinguishable and no Redis or ownership detail is
  exposed
- [x] `serverSequence` preserved as the server's value: never incremented,
  compared, or used as a client correlation id
- [x] `BattleScene` has no direct SignalR dependency and remains the presentation
  layer reached only through `GameRuntime`
