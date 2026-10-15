# TASK-280 — Investigation: BattleHub State-Push-Before-Projection Batch-Loss Path

---

## Metadata

```text
Task ID:           TASK-280
Type:              BUG / INVESTIGATION
Status:            CLOSED — DISPROVED AS STATED (§6.1); reconciled and closed under TASK-282 (§7)
Verdict:           DISPROVED AS STATED — projection failure is not reachable from producible
                   pipeline values; a faulted second broadcast can lose the batch after a
                   state push
Risk:              MEDIUM
Priority:          HIGH
Mode:              Investigation & Defect Isolation
Classification:    VERIFIED — the projection-failure trigger is unreachable; the ordering
                   window is confirmed and pinned by a deterministic test; the transport
                   failure is reproduced at the hub boundary with unmeasured production
                   frequency
Production code:   NONE CHANGED (two tests added — §6.7); NONE REQUIRED (closed without a fix)
Target Component:  src/backend/GameServer.Api/Hubs/BattleHub.cs
Contract Refs:     docs/02-technical/SIGNALR_PROTOCOL.md §3, §3.1, §4, §6, §7, §8;
                   docs/02-technical/GAME_STATE.md §5.0
Dependencies:      None (Independent of protocol authorization gate TASK-278)
Declared Files:    tasks/completed/TASK-280-investigate-battlehub-state-push-projection-batch-loss.md,
                   tests/backend/GameServer.Api.Tests/Hubs/BattleHubPartialBroadcastTests.cs (new),
                   src/frontend/client/tests/GameRuntime.test.ts (one test added)
Verification:      §6.8 (as recorded) and §7.2 (re-executed under TASK-282) — Api.Tests
                   376/376, Domain.Tests 1626/1626, client vitest 1189/1189, focused
                   BattleHubPartialBroadcastTests 2/2, git diff --check clean
Follow-up:         §7.6 — a separate client recovery-trigger investigation (recommended;
                   not opened here, and not part of TASK-280)
```

> **How to read this record.** §1–§5 are the record exactly as filed: the suspected
> projection-failure defect and the remediation proposed against it. **§6 is the executed
> investigation and supersedes §1–§5 wherever they differ** — in particular §4's
> pre-projection reordering is *not* approved for implementation (§6.4, §6.9 item 1). §7 is
> the TASK-282 reconciliation: the evidence was re-verified against the working tree, the
> outstanding discrepancies were dispositioned, and the investigation is closed with no
> production change.

---

## 1. Problem Statement

In `src/backend/GameServer.Api/Hubs/BattleHub.cs`, three action broadcast paths (`Swap` lines 1143–1156, `CardCast` lines 1295–1308, and `PetSkillCast` lines 1341–1350) structure post-commit notifications as follows:

```csharp
try
{
    await Clients.Group(battleId).SendAsync("BattleStateUpdated", ToPayload(result.Value.State));
    await Clients.Group(battleId).SendAsync("ReceiveEvents", ToPayload(result.Value));
}
catch (Exception ex)
{
    _logger?.LogError(
        ex,
        "Failed to broadcast post-commit notifications for battle {BattleId}, sequence {Sequence}, action {ActionId}",
        battleId,
        result.Value.State.Sequence,
        request.ActionId);
}
```

### The Suspected Defect
Notice the order of execution:
1. `ToPayload(result.Value.State)` is evaluated and `SendAsync("BattleStateUpdated", ...)` is awaited over the network.
2. The client receives `BattleStateUpdated`, and advances its local `held.sequence` to $N$.
3. Next, on the second line, `ToPayload(result.Value)` is invoked inline as an argument to `SendAsync("ReceiveEvents", ...)`.
4. Inside `ToPayload(SwapExecutionResult result)` (lines 1387–1402):
   ```csharp
   BattleEventWireProjection.Project(result.Events)
   ```
   executes.
5. If `BattleEventWireProjection.Project(result.Events)` throws an unhandled exception (for example, if a new domain event type is unmapped, an invalid enum value is encountered, or a null reference occurs):
   - The exception is thrown **after** `BattleStateUpdated` has already been transmitted and received by the client!
   - The exception is caught by the `catch (Exception ex)` block and logged.
   - `ReceiveEvents` for sequence $N$ is **never sent**.

### The Undetected Desync Impact
Under `GAME_STATE.md` §5.0 and `GameRuntime.ts` (lines 1691–1741):
- The client detects sequence desynchronization when a `ReceiveEvents` batch arrives with `serverSequence > held.sequence`.
- In this failure scenario, `BattleStateUpdated` has **already advanced** `held.sequence` to $N$.
- Because no `ReceiveEvents` message arrives for sequence $N$, no comparison occurs.
- When the next action resolves at sequence $N+1$, its `ReceiveEvents` arrives with `serverSequence = N+1`.
- The client compares `serverSequence (N+1)` with `held.sequence (N+1)` (since state push $N+1$ also preceded it), finds them equal, and applies the batch normally.
- Result: **The entire event batch for sequence $N$ was silently dropped with zero client-side desync detection or recovery trigger!**

---

## 2. Contract Violations & Architectural Principles

1. **`SIGNALR_PROTOCOL.md` §3.1 item 1:**
   > "Events are produced by the resolution, but they describe a state that is already committed: the resolution completes and the new BattleState is written first, then the batch is sent..."
2. **Atomicity of Notification Projection:**
   While Redis CAS guarantees persistence atomicity, in-memory projection failure must not create a partial SignalR broadcast where state advances but events are lost.
3. **Recovery Gating (`ADR-008`, `SIGNALR_PROTOCOL.md` §7):**
   If events cannot be projected or sent, the client has no means of rendering animations, damage numbers, or banners for that turn, and is unaware that an event batch was lost.

---

## 3. Why Existing Tests Did Not Detect It

Existing integration tests (`tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs`) test successful end-to-end resolutions where `BattleEventWireProjection.Project()` succeeds for all 18 existing domain event types. No test injects an unhandled projection exception between `BattleStateUpdated` and `ReceiveEvents`.

---

## 4. Investigation & Verification Plan

### Phase 1: Verification (Simulate Projection Exception)
1. In a test harness, simulate a projection failure during `SwapExecutionResult` mapping.
2. Confirm that `BattleStateUpdated` is received by the client test mock, but `ReceiveEvents` is not.
3. Verify that subsequent sequence actions do not trigger `requestBattleStateRecovery()`.

### Phase 2: Remediation
1. **Pre-Project Payloads Before Any Network Transmission:**
   Evaluate and build both DTO payloads before awaiting any network call:
   ```csharp
   // 1. Project all DTOs first (pure memory operations; will throw before network send)
   var statePayload = ToPayload(result.Value.State);
   var eventsPayload = ToPayload(result.Value);

   // 2. Transmit notifications only after projection succeeds
   await Clients.Group(battleId).SendAsync("BattleStateUpdated", statePayload);
   await Clients.Group(battleId).SendAsync("ReceiveEvents", eventsPayload);
   ```
2. By projecting `eventsPayload` first:
   - If projection throws, **zero network messages are broadcast**.
   - Neither `BattleStateUpdated` nor `ReceiveEvents` reaches the client.
   - The caller receives an error response, and the client's held sequence does not advance ahead of its event timeline, preserving clean snapshot recovery.

---

## 5. Risk Assessment & Scope

- **Risk:** MEDIUM. Affects SignalR notification broadcast order in `BattleHub.cs`.
- **Target Files:** `src/backend/GameServer.Api/Hubs/BattleHub.cs`.
- **Independence:** Can be verified and resolved independently of the cascade protocol authorization gate (TASK-278).

---

## 6. Investigation Record — Executed

### 6.1 Verdict

**DISPROVED AS STATED.** The suspected failure — `BattleEventWireProjection.Project()`
throwing after `BattleStateUpdated` has been published — is **not reachable from any
value the committed pipeline can produce**, so no production defect was established and
**no production code was changed** (AGENTS.md §14, §16, §20: no speculative fix).

The *ordering* the record describes is **CONFIRMED** as code structure, and the
reachable way a batch can be lost behind its delivered state push is a **faulted second
broadcast** (transport), which §4's proposed pre-projection fix does not address.

| Claim (from §1 of this record) | Result |
| :--- | :--- |
| `BattleStateUpdated` is published before `ReceiveEventsPayload` is projected | **CONFIRMED** — argument evaluation order; §6.2 |
| `Project()` can throw after the state push, for a producible event list | **DISPROVED** — §6.3 |
| A committed batch can be lost after its state push was delivered | **CONFIRMED** — delivery fault; deterministic repro, §6.4/§6.6 |
| The client detects that loss as a gap and recovers | **DISPROVED** — no gap is observable, so no recovery is requested; §6.5 |
| The loss leaves the client's *state* diverged | **DISPROVED** — the state push is the reconstruction-safe carrier; §6.5 |
| The §4 remediation (project both payloads before sending either) prevents the loss | **DISPROVED** — the batch payload was already projected before the failing send; §6.4/§6.6 |

### 6.2 Exact execution ordering and failure path

Server path for one accepted action (line numbers verified against the working tree):

```text
1. resolve          SwapExecution.Execute(...) → SwapExecution.cs:415–555
2. sequence         SwapExecution.cs:538   Sequence = state.Sequence + 1
                    (and :552 records LastCommittedSwapPair at the same value)
3. write-back       BattleStateService.cs:953  TryUpdateAsync(committed.State, expectedSequence)
                    (CardCast :1183, PetSkillCast :1304)
                    — the CAS commit completes BEFORE anything is published
4. return           BattleStateService.cs:952–977  → BattleHub.cs:1107
5. state projection BattleHub.cs:1145  ToPayload(result.Value.State)   ← evaluated first
6. state publish    BattleHub.cs:1145  await Clients.Group(battleId).SendAsync("BattleStateUpdated", …)
7. batch projection BattleHub.cs:1146  ToPayload(result.Value)         ← evaluated only AFTER step 6 awaited
                    → BattleHub.cs:1387–1402 → BattleEventWireProjection.Project(result.Events)
8. batch publish    BattleHub.cs:1146  await Clients.Group(battleId).SendAsync("ReceiveEvents", …)
9. failure handling BattleHub.cs:1148–1156  catch (Exception) → LogError, swallow
10. acknowledgement BattleHub.cs:1158  return new SwapResponse(Accepted: true, Reason: null)
```

The same pair, with the same order and the same single `try`, exists at
`CardCast` **1297–1298** (catch 1300–1308) and `PetSkillCast` **1343–1344**
(catch 1346–1353); the record's §1 line numbers (1143–1156, 1295–1308, 1341–1350,
`ToPayload(SwapExecutionResult)` 1387–1402) are accurate. Because a `SendAsync`
argument is evaluated before the call, the batch payload is not even constructed until
the state frame has been published — the window exists exactly as described.
(`Swap` also has a separate, single-message idempotent-replay path at 1121–1141 that
deliberately sends no batch; it is not part of this window.)

Two distinct ways the window can produce a partial broadcast:

```text
(a) projection fault  ToPayload(result.Value) throws at step 7 → nothing at step 8.
                      REACHABILITY DISPROVED (§6.3): the projection is total for every
                      value the committed pipeline can hand it.
(b) delivery fault    step 8 throws after step 6 completed → the state frame is on the
                      wire, the batch is not. REACHABLE; reproduced deterministically (§6.6).
```

### 6.3 Why the projection cannot throw after the state push

The whole throw surface of `BattleEventWireProjection.Project` is four things:

1. `ArgumentNullException.ThrowIfNull(events)` (`BattleEventWireProjection.cs:746`) —
   unreachable: `SwapExecutionResult.Events` is `_events ?? []` (`SwapExecution.cs:135`)
   and the hub never passes `null`.
2. `_ => throw new ArgumentOutOfRangeException(...)` for an unmapped discriminator
   (`BattleEventWireProjection.cs:803–806`) — **unreachable**: the enum is closed at 18
   members (`BattleEvent.cs:61–293`) and the switch maps all 18 (arms at
   `BattleEventWireProjection.cs:777–802`); the only way to reach the default is a cast
   from an out-of-range integer, which nothing in Domain or Application performs.
3. The payload accessors that throw when the discriminator and payload disagree
   (`BattleEvent.cs:625–847`) — **unreachable**: every factory sets the discriminator
   together with its own payload (`BattleEvent.cs:849–1180`), and the projection reads
   `Type` first and then only that event's own payload. The only value in the struct's
   value space that violates this is `default(BattleEvent)` (Type = `MatchCreated` with
   no payload), which no pipeline path constructs — the test-only assertion in §6.6
   pins that boundary.
4. `GemTypes.ToContractName` (`GemType.cs:67–74`, called at
   `BattleEventWireProjection.cs:998` and `:1021`) — the only *value-dependent* throw, and
   **unreachable**: it fires only for an out-of-range `GemType`, and no `BoardState` can
   hold one. `BoardState.FromCells` (`BoardState.cs:135–140`), `FromValues` (`:170–176`)
   and `FromCellEntries` (`:204–210`) all reject an undefined Gem type; the persisted-state
   load path uses the guarded constructor
   (`BattleStateSerializer.cs:502  BoardState.FromCellEntries(...)`), so a corrupted Redis
   record fails loudly at load instead of producing an invalid board. The event payloads
   that reach `ToContractName` (`GemMatchedEvent.GemType`,
   `MatchShape.GemType`) are read from those same validated cells.

Consequently the DTO factories themselves contain no validation and no throw
(`BattleEventWireProjection.cs:281–626`), and no serialization step can fail after a
successful projection: the payload is a fully-materialized DTO graph.

**Why a hub-level injection of (a) is not constructible either** — the same reason, from
the test side: the hub's only producer of the events list is
`BattleStateService.ExecuteSwapAsync` (a **sealed** class; `BattleStateService.cs:104`,
methods non-virtual), so the list cannot be substituted for a fixture, and the only
test-constructible violating value (`default(BattleEvent)`) cannot come out of it.

### 6.4 The reachable partial-broadcast path

A faulted second `SendAsync` after a completed first one. Both payloads are already
projected at that point, so **§4's proposed remediation cannot prevent it**: whether the
batch is projected at step 5 or at step 7, the failure is at step 8. Reordering the two
messages is not an option either — `SIGNALR_PROTOCOL.md` §6 item 4 ("`BattleStateUpdated`
is delivered before any `ReceiveEvents` for that battle") makes the current order the
contract, and §3.1 item 6 already records this class of loss:

> "Redis CAS guarantees single-record state persistence atomicity, but does not provide
> distributed two-phase commit with SignalR delivery or guarantee exactly-once event
> broadcast. … If a connection drops between the Redis write and the WebSocket send,
> events may be lost in transit. Client recovery is strictly governed by §7 snapshot
> recovery (`GetBattleState`), not event stream guarantees."

The server has no second attempt anywhere: no retry on this path, and §8 item 8
("An event log or replay channel. Events are delivered once per resolution and are not
re-delivered") means the §7 snapshot — state only — is the only convergence mechanism.

### 6.5 Client-side behaviour when the ordering occurs

Verified in the client runtime and battle scene (behaviour, not intent):
`src/frontend/client/src/game/runtime/GameRuntime.ts` and
`src/frontend/client/src/game/scenes/BattleScene.ts`.

```text
BattleStateUpdated → GameRuntime.receiveBattleState (1884) → ingestBattleState
                     (:1913, called at :1900)
                   → this.battleState = state;   ← GameRuntime.ts:1929   the held sequence
ReceiveEvents      → GameRuntime.forwardBattleEvents (1691)
                   → if (envelope.serverSequence > held.sequence) withhold + recover (1730–1733)
                   → otherwise forward whole, no recovery (1735–1740)
```

* **The held sequence is advanced by the state push, with no gating on the batch**
  (`GameRuntime.ts:1929`); the batch's own comparison is against that copy (`:1701`,
  `:1730`). This is the §6 item 3 / `GAME_STATE.md` §5.0 rule as amended by TASK-273, so
  the code is conformant.
* **A batch lost behind its own delivered state push is invisible.** With push *N*
  delivered and batch *N* lost, the copy is already at *N*; push *N+1* moves it to *N+1*;
  batch *N+1* then satisfies `serverSequence > held.sequence` **false** and is forwarded
  as the documented delivery — no error, no `unsynchronized`, no snapshot request. There
  is no per-sequence bookkeeping that could notice (the runtime keeps one
  `withheldGapSequence` scalar, `:238`, set only by a batch *ahead* of the held
  sequence), and the client's recovery entry point is reachable only from an unanswered
  action, a reconnect, or a withheld gap (`:877`, `:1098`, `:1790`).
* **No state divergence.** The push carries the complete post-resolution state
  (`GAME_STATE.md` §5.1 item 3; `SIGNALR_PROTOCOL.md` §4), and §7 item 2 converges by
  discarding events rather than replaying them — the loss is a lost *presentation*
  (timeline/animation/readouts for that resolution), not a lost state.
* **The one non-presentational consequence is client-local.** `BattleScene` holds input
  and the board from submission until the resolution's timeline ends
  (`resolutionPending` set at `:3677`/`:3876`/`:3958`; the push is held at `:2341–2342`
  via `shouldHoldResolution` `:2453–2454`; `isInputLocked()` is true for every
  orchestration state except `idle`/`terminated`, `:5974–5976`; a settled
  acknowledgement does not release it, `:6104–6110`). With the batch lost and no further
  push in a single-player-vs-Boss battle, the view stays on the pre-resolution board
  with input locked until a delivered state releases it — in practice a reconnect, whose
  §7 recovery is ingested with `source='recovery'` and releases every lock
  (`BattleScene.ts:2272–2282`, `GameRuntime.ts:1085–1099`). **This is a client-side idle
  boundary, not a divergence, and it is not caused by the broadcast order**: a lost state
  push and a lost batch end in the same place. Existing coverage:
  `SceneLifecycle.test.ts:12256` ("converges a replay-style state push without a batch and
  releases the appropriate locks") covers the scene outcome once a later push arrives; no
  runtime-level test covered the exact push(*N*) delivered / batch(*N*) lost /
  push(*N+1*) + batch(*N+1*) sequence — now added (§6.6).

### 6.6 Reproduction and regression evidence

* **Reproduction (reachable path, deterministic).**
  `tests/backend/GameServer.Api.Tests/Hubs/BattleHubPartialBroadcastTests.cs`
  `Swap_WhenTheBatchBroadcastFails_LeavesTheCommittedStatePushedAndTheBatchLost` drives
  the real `BattleHub` + real `BattleStateService` + in-memory
  `IBattleStateRepository` with an `IHubCallerClients` double whose group proxy fails
  only `ReceiveEvents` (no timing, no retry). It asserts, in order: both broadcasts were
  attempted as `["BattleStateUpdated", "ReceiveEvents"]` while only the state frame was
  delivered; the delivered state frame carries sequence 1; the **batch payload was
  projected successfully** (`ReceiveEventsPayload.ServerSequence == 1`, non-empty
  `Events`) before the failing send; the failure was logged and never retried (exactly
  two attempts); the response is `Accepted: true`; and the committed record is the
  resolution's complete result at sequence 1.
* **Boundary guard (the unreachable trigger).** Same file,
  `EventProjection_RejectsOnlyADiscriminatorWithoutItsPayload_WhichNoResolutionProduces`
  pins the assertion that the projection's only value-dependent failure needs
  `default(BattleEvent)` — a discriminator with no payload that no factory builds. If a
  future change ever made such a value producible, this premise (and §6.3) must be
  re-derived.
* **Client characterisation.** `src/frontend/client/tests/GameRuntime.test.ts`
  test `13. cannot detect a batch lost after its own state push advanced the sequence
  (TASK-280)`: push(1) delivered, batch(1) omitted, push(2) + batch(2) delivered →
  `seen == [2]`, `getBattleStateCalls == []`, `lastError == null`,
  `sync == 'synchronized'`, held sequence `2`.

### 6.7 Files changed

```text
tests/backend/GameServer.Api.Tests/Hubs/BattleHubPartialBroadcastTests.cs   NEW  (2 tests)
src/frontend/client/tests/GameRuntime.test.ts                              +1 test (test 13)
tasks/backlog/TASK-280-investigate-battlehub-state-push-projection-batch-loss.md  record
```

No production file, protocol document, ADR, or game-rule document was modified, and no
TASK-273/TASK-274 safeguard was weakened. Pre-existing working-tree changes
(`BattleEventBuilder.cs`, `BattleEventEmissionTests.cs`) and all untracked task records
were left untouched. No commit or push was performed.

### 6.8 Test results

```text
dotnet test tests/backend/GameServer.Api.Tests        → Passed 376, Failed 0
dotnet test tests/backend/GameServer.Domain.Tests     → Passed 1626, Failed 0
npx vitest run (src/frontend/client, 24 files)        → Passed 1189, Failed 0
git diff --check                                      → clean (exit 0)
```

Re-executed under TASK-282 — §7.2: the three suite totals above were reproduced exactly.

Baseline before the added tests: the focused `BattleHubStatePushOrderTests` (4/4) and
`GameRuntime.test.ts` (191/191) were green on the unchanged tree; the Api and Domain
suites are green with the two additions included. The new tests pass on the unmodified
production code — that is the finding: the reachable behaviour is what the code already
does, and the suspected projection fault could not be produced against it.

### 6.9 Remaining risks, uncertainty, and recommended next action

1. **No fix is warranted by this investigation.** The projection trigger is unreachable
   and the proposed §4 remediation does not address the reachable (delivery) path, so
   implementing it would be a speculative production change (`AGENTS.md` §16, §20).
2. **Optional hardening, explicitly not a defect fix.** Projecting both payloads before
   the first `await` would be contract-neutral (no wire member, no message, no ordering
   change) and would remove window (a) if a future invariant violation ever made
   `Project` throw. If the operator wants it, it belongs in its own hardening task and
   must not be recorded as closing the batch-loss path.
3. **Residual uncertainty (affects frequency, not existence).** This investigation did not
   verify at transport level how often ASP.NET Core SignalR's group `SendAsync` faults for
   a connection that breaks between the two writes (group membership is removed on
   disconnect, so the practical window is narrow, and a per-connection failure may not
   surface to the hub's `catch` at all). Window (b) is real and reproducible at the hub
   boundary; its production frequency is unmeasured.
4. **Recommended follow-up (separate task, operator decision).** The client-side idle
   boundary of §6.5 — an accepted action whose batch never arrives leaves input locked and
   the board held until some later delivered state — is a genuine robustness gap, but it
   is a *client recovery-trigger* question, not a `BattleHub` ordering question: the
   snapshot path (`GetBattleState`, §7.1) already exists and carries no events, so a
   documented trigger for it would need a decision plus a `SIGNALR_PROTOCOL.md` §7 note.
   It must not be smuggled into TASK-280, and it must not weaken the §6 item 3 gap rule.
5. **TASK-278 dependency.** This verdict is independent of the pending A-1/A-4 decisions;
   no cascade-protocol proposal (TASK-276/TASK-277) was implemented or pre-empted.

---

## 7. TASK-282 Reconciliation and Closure

### 7.1 Scope and method

TASK-282 reconciled this record with its own completed evidence and closed it. It is
documentation-only: **no production code, protocol, schema, game-rule, or ADR was touched;
no TASK-273/TASK-274 sequencing, admission, or recovery safeguard was weakened; TASK-276
was not implemented; and the TASK-277/TASK-278 approval decisions were not altered.**

Reconciliation re-read this record, the two test files it declares
(`tests/backend/GameServer.Api.Tests/Hubs/BattleHubPartialBroadcastTests.cs`,
`src/frontend/client/tests/GameRuntime.test.ts`), and the working-tree diff for
`src/frontend/client/tests/GameRuntime.test.ts`; re-derived the record's line-level anchors
against the working tree (§7.2 second block); and re-executed the tests (§7.2 first block).
**The verdict of §6.1 is unchanged.**

### 7.2 Fresh evidence (executed for TASK-282)

Executed against the working tree as reconciled. `git status` in that tree shows the
pre-existing TASK-279 domain changes, the TASK-280 test additions, and the untracked task
records; `src/backend/GameServer.Api/Hubs/BattleHub.cs` is **unmodified** against `HEAD`, so
the ordering the record describes is the current code, not a historical revision.

```text
dotnet test tests/backend/GameServer.Api.Tests
  --filter "FullyQualifiedName~BattleHubPartialBroadcastTests"     → Passed 2, Failed 0, Skipped 0
dotnet test tests/backend/GameServer.Api.Tests (full)              → Passed 376, Failed 0, Skipped 0
dotnet test tests/backend/GameServer.Domain.Tests (full)           → Passed 1626, Failed 0, Skipped 0
npx vitest run tests/GameRuntime.test.ts   (cwd src/frontend/client) → 1 file passed, 192 tests passed
npx vitest run                             (cwd src/frontend/client) → 24 files passed, 1189 tests passed
git diff --check                                                   → clean (exit 0)
```

All three suite totals recorded in §6.8 are reproduced exactly. Two limits on this evidence
are stated rather than glossed:

* `GameRuntime.test.ts` now reports **192**, which is consistent with §6.8's "191/191 on the
  unchanged tree" plus the one added test — but that 191 baseline was **not** re-executed
  here (removing the added test is out of scope for a documentation-only task). D-4.
* The totals describe the suites **as the working tree stands**, which includes the
  pre-existing TASK-279 domain changes. They are not a statement about `HEAD` alone.

Anchors re-derived by inspection, all matching the record: `BattleHub.cs:1145–1146` /
`:1297–1298` / `:1343–1344` (push then batch inside one `try`; catches `:1148`, `:1300`,
`:1346`), the replay path `:1121–1141`, `ToPayload(SwapExecutionResult)` `:1387–1402` with
`BattleEventWireProjection.Project` at `:1402`, and `ToPayload(BattleState)` `:1414–1459`;
`BattleEventWireProjection.cs:746` (null guard), `:777–802` (all 18 kinds), `:803–806`
(default throw), `:998`/`:1021` (`GemTypes.ToContractName`), no `throw` anywhere in the DTO
factory range `:281–626`; `BattleEvent.cs` 18 enum members, the `Match` accessor's "carries
no Match" throw at `:625–627`, no `throw` in the factory range `:849–1180`; `GemType.cs:67`;
`SwapExecution.cs:135`, `:415`, `:538`, `:552`; `BattleStateService.cs:104` (sealed),
`:953`, `:1183`, `:1304`; `BattleStateSerializer.cs:502`; `GameRuntime.ts:238`, `:877`,
`:1098`, `:1691`, `:1701`, `:1730–1740`, `:1790`, `:1884`, `:1913`, `:1929` (and **exactly
three** `recoverBattleState` call sites — `:877`, `:1098`, `:1790` — from the declaration at
`:1184`); `BattleScene.ts:2272–2282`, `:2341–2342`, `:2453–2454`, `:3677`/`:3876`/`:3958`,
`:5974–5976`, `:6104–6110`; `SceneLifecycle.test.ts:12256`; and
`BattleHubStatePushOrderTests` = 4 `[Fact]`s, matching §6.8's recorded 4/4 baseline.

### 7.3 The five findings, each with its evidence and its limit

1. **DISPROVED — the projection-failure hypothesis (the record's §1 defect).**
   `BattleEventWireProjection.Project` cannot throw after the state push for any value the
   committed pipeline produces: its whole throw surface is the `null` guard (the hub never
   passes `null`), the unmapped-discriminator default (the enum is closed at 18 members and
   all 18 are mapped), the discriminator/payload accessor throws (every factory sets the two
   together; no pipeline path builds `default(BattleEvent)`), and
   `GemTypes.ToContractName` (needs an out-of-range `GemType`, which no `BoardState`
   constructor admits, and the Redis load path uses the guarded constructor). §6.3; pinned by
   `EventProjection_RejectsOnlyADiscriminatorWithoutItsPayload_WhichNoResolutionProduces`
   (§6.6). *Limit:* a reviewed reachability argument over a closed set, not a proof about
   future code — if the invariant ever changes, §6.3 and the guard test must be re-derived.
2. **CONFIRMED — the transport-failure path.**
   A faulted second `SendAsync` after a completed first one leaves the state frame delivered,
   the batch undelivered, the caller acknowledged, and the failure logged and never retried.
   Reproduced deterministically by
   `Swap_WhenTheBatchBroadcastFails_LeavesTheCommittedStatePushedAndTheBatchLost` (§6.6),
   which also asserts the batch payload **had already been projected** — the reason §4's
   proposed reordering does not address this path (§6.4). *Limit:* injected at the
   `IClientProxy` boundary, below the real transport; production frequency is unmeasured
   (§7.4 item 1).
3. **CONFIRMED — the client does not detect the missing batch in the reproduced scenario.**
   With push(1) delivered, batch(1) lost, then push(2)+batch(2) delivered, the runtime
   forwards only batch 2, requests no snapshot, reports no error, and stays `synchronized`
   (`GameRuntime.test.ts` test 13, §6.6). *Limit:* an envelope-level characterisation through
   a synthetic transport double for that one ordering; it does not generalise to arbitrary
   loss patterns (§7.4 item 5).
4. **CONFIRMED — no state divergence in that scenario.**
   The state push is a one-to-one field mapping of the committed record (`BattleHub.cs`
   `:1414–1459`: board cells entry-for-entry, Combo/MatchCount, PetState, Boss id/hp/maxHp,
   seed, RNG pair, turn, sequence) and the client stores it unconditionally as its
   authoritative copy (`GameRuntime.ts:1929`), with §7.1 recovery entering through the *same*
   ingestion path (`:1913`, `:1277`). The hub test asserts the pushed sequence equals the
   committed record's sequence (§6.6 step 6) and the client test asserts the held sequence.
   *Limit:* the conclusion rests on the mapping being total and derived only from `state`
   (inspection) plus sequence-level equality — no field-by-field assertion exists between the
   pushed payload and the committed record (§7.4 item 4).
5. **OPEN, client-local — the presentation / idle boundary (not a divergence, not caused by
   the ordering).** `BattleScene` holds the board and the input from submission until the
   resolution's timeline ends (`:3677`/`:3876`/`:3958` set `resolutionPending`; the push is
   held at `:2341–2342` via `shouldHoldResolution` `:2453–2454`; `isInputLocked()` is true in
   every orchestration state except `idle`/`terminated`, `:5974–5976`; a settled
   acknowledgement does not release it, `:6104–6110`). With the batch lost and no further
   push, the view stays on the pre-resolution board with input locked until a delivered state
   releases it — in practice a reconnect, whose snapshot is ingested with `source='recovery'`
   and releases every lock (`BattleScene.ts:2272–2282`; `GameRuntime.ts:1098`).
   *Limit:* observed from code plus the existing `SceneLifecycle.test.ts:12256` coverage, not
   from a measured user-visible event; a lost state push ends in the same place, so this is
   not attributable to the broadcast order, and no frequency is claimed.

### 7.4 Limitations of the evidence (none of which changes the verdict)

1. **The fault is injected at the hub boundary, not the transport.** The test asserts what
   the hub does *given* a faulted second send, not how often an ASP.NET Core SignalR group
   send faults for a breaking connection. Production frequency is unmeasured and is not
   claimed anywhere in this record (§6.9 item 3).
2. **One path was driven.** The reproduction is the `Swap` path with a single faulted
   `ReceiveEvents` send. `CardCast` (`BattleHub.cs:1297–1298`) and `PetSkillCast`
   (`:1343–1344`) share the structure, but the equivalence is structural, not executed.
3. **The projection argument is a reachability review**, pinned by one boundary test (§6.3,
   §6.6); it is not a machine-checked proof.
4. **No payload-level equality assertion** exists between the pushed state payload and the
   committed record (§7.3 finding 4 step 6) — only sequence equality.
5. **The client characterisation is one ordering.** Test 13 covers exactly one lost batch
   immediately behind its own delivered push, followed by one normal delivery. It does not
   cover a lost state push, or a lost batch with no later delivery (that case is §7.3
   finding 5, characterised from code).

### 7.5 Discrepancies found during reconciliation

| # | Item | Disposition |
| :--- | :--- | :--- |
| D-1 | §1–§5 present the suspect defect and §4's remediation as live, with no signpost that §6 supersedes them — a reader could act on §4 | Corrected: "How to read this record" note above; §6.4/§7.6 |
| D-2 | §6.5 cited `GameRuntime.ts` / `BattleScene.ts` without their owning directories under `src/frontend/client/src/` | Corrected in §6.5 (`src/game/runtime/`, `src/game/scenes/`) |
| D-3 | §6.5 cited `ingestBattleState (1900)`, which is the call site inside `receiveBattleState`; the definition is `GameRuntime.ts:1913` | Corrected in §6.5; no behavioural claim affected |
| D-4 | §6.8's baseline "`GameRuntime.test.ts` (191/191) on the unchanged tree" cannot be re-executed now | Recorded as not independently re-verified (§7.2); the current 192 is consistent with it |
| D-5 | Metadata `Status` read `INVESTIGATION COMPLETE`, which is not a closed state, and carried no verdict line | Corrected: `Status: CLOSED` + `Verdict:` (§7.6) |

**No discrepancy was found in any load-bearing claim**, and the record's declared file set
matches the working tree: the hub test file exists with 2 tests, and the entire diff of
`src/frontend/client/tests/GameRuntime.test.ts` is the one TASK-280 test (test 13).

### 7.6 Closure

```text
Status:  CLOSED
Verdict: DISPROVED AS STATED — projection failure is not reachable from producible
         pipeline values; a faulted second broadcast can lose the batch after a state push.
Fix:     NONE — no production change is warranted by this investigation (§6.9 item 1).
         §4's pre-projection reordering does not address the reachable path (§6.4) and is
         NOT authorized here. If the operator ever wants it, it is a separate
         contract-neutral hardening task (§6.9 item 2) and must not be recorded as closing
         the batch-loss path.
```

The record is closed **in place** in `tasks/backlog/`. `TASK_LIFECYCLE.md` §4/§6 files a DONE
record by moving it to `tasks/completed/` inside the two-stage commit protocol, which
requires a Phase A implementation commit SHA; TASK-282 prohibits commit and push, so that
transition cannot be completed truthfully by this task. Every substantive part of the
investigation (§6) and its reconciliation (§7) is final; only the filing commit is
outstanding.

**Recommendation — separate client recovery-trigger investigation (operator decision).**
Open a new task for the §7.3 finding 5 boundary: an accepted action whose batch never
arrives leaves the board held and the input locked until some later delivered state. It is a
client recovery-*trigger* question, not a `BattleHub` ordering question — the §7.1 snapshot
(`GetBattleState`) already exists and carries no events, and any trigger would need an
explicit decision plus a `SIGNALR_PROTOCOL.md` §7 note. It must not weaken the §6 item 3 gap
rule (a batch *ahead* of the held sequence is the only client-observable desync), and it must
not be folded back into TASK-280.

### 7.7 Preservation

TASK-282 changed **this record only**: §7 plus the metadata fields
`Status` / `Verdict` / `Classification` / `Production code` / `Verification` / `Follow-up`,
the "How to read this record" note, two anchor corrections in §6.5, and the §6.8
re-execution pointer. The pre-existing working-tree changes
(`src/backend/GameServer.Domain/Match3/BattleEventBuilder.cs`,
`tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs`, the
`GameRuntime.test.ts` TASK-280 addition,
`tests/backend/GameServer.Api.Tests/Hubs/BattleHubPartialBroadcastTests.cs`) and every other
untracked task record are intact. No file was deleted or moved, and no Git operation other
than read-only inspection (`status`, `diff`, `diff --check`) was performed.

