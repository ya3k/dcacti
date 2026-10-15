# TASK-284A — Missing Resolution-Batch Watchdog: Implementation Record

---

## Metadata

```text
Task ID:           TASK-284A (the authorized implementation slice of TASK-284)
Type:              IMPLEMENTATION — client runtime recovery trigger + protocol amendment + tests
Status:            DONE
Risk:              MEDIUM (delivery-timing behaviour on the existing realtime path)
Priority:          HIGH
Mode:              Controlled Repository Workflow — authorized implementation (§2 of the task brief)
Authorization:     tasks/backlog/TASK-283-option-a-missing-resolution-batch-watchdog-authorization.md
                   (D-1 … D-7 approved by the operator; the readiness report is the scope authority)
Scope authority:   tasks/backlog/TASK-284-runtime-recovery-for-missing-accepted-action-event-batches.md
                   §3.3, §4, §5, §6, §7, §8
Historical commit: e33254e8057159f50012746ebf17a7d01acd6d13 (under tasks/TASK_LIFECYCLE.md §6.4)
Production code:   src/frontend/client/src/game/runtime/GameRuntime.ts,
                   src/frontend/client/src/game/runtime/GameRuntimeEvents.ts
Protocol:          docs/02-technical/SIGNALR_PROTOCOL.md §7 (+ version header, v2.24)
Tests:             src/frontend/client/tests/GameRuntime.test.ts (§5)
Verification:      §5 — focused 209/209, three-file 592/592, full suite 1206/1206,
                   `npx tsc --noEmit` exit 0, `git diff --check` clean
Declared Files:    src/frontend/client/src/game/runtime/GameRuntime.ts,
                   src/frontend/client/src/game/runtime/GameRuntimeEvents.ts,
                   docs/02-technical/SIGNALR_PROTOCOL.md,
                   src/frontend/client/tests/GameRuntime.test.ts
```

---

## 1. Outcome

The gap recorded as `TASK-280` §7.3 finding 5 is closed on the client without adding a
mechanism: a delivered authoritative state push whose `ReceiveEvents` batch never
arrives now expires one bounded, client-local expectation, which marks the runtime
`unsynchronized` and requests the **existing** `SIGNALR_PROTOCOL.md` §7 item 1
`GetBattleState(battleId)` snapshot.

```text
before   push(S) delivered, batch(S) lost → held.sequence = S, sync = 'synchronized',
         scene input-locked on the pre-resolution board, and nothing ever asks
after    the same delivery arms one expectation at S; if the batch at S is not admitted
         before 2,500 ms, the runtime publishes sync = 'unsynchronized' with a technical
         detail (delivered sequence + action identity) and requests the existing §7.1
         snapshot, whose existing bounded retry is the only retry there is
```

No new request, message, subscription, state member, wire argument, scene change, or
port member exists after this slice. `ADR-008` is unmodified (D-2 = §7 only).

---

## 2. Files changed

```text
docs/02-technical/SIGNALR_PROTOCOL.md                                MODIFIED (amendment only — §7 preamble + version header)
src/frontend/client/src/game/runtime/GameRuntime.ts                   MODIFIED (the expectation + its four hook points)
src/frontend/client/src/game/runtime/GameRuntimeEvents.ts             MODIFIED (DEFAULT_RESOLUTION_WATCHDOG_MS, GameRuntimeOptions)
src/frontend/client/tests/GameRuntime.test.ts                         MODIFIED (17 new deterministic tests; pre-existing content untouched)
tasks/backlog/TASK-283-option-a-missing-resolution-batch-watchdog-authorization.md   NEW (D-7 re-filing)
tasks/backlog/TASK-284A-missing-resolution-batch-watchdog-implementation.md          NEW (this record)
tasks/backlog/TASK-284-runtime-recovery-for-missing-accepted-action-event-batches.md  MODIFIED (post-authorization status addendum only)
```

Pre-existing uncommitted work was preserved byte-for-byte: the TASK-280 pin at
`GameRuntime.test.ts` test 13, `src/backend/GameServer.Domain/Match3/BattleEventBuilder.cs`,
`tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs`,
`tests/backend/GameServer.Api.Tests/Hubs/BattleHubPartialBroadcastTests.cs`, and the
TASK-276 … TASK-280 backlog records.

---

## 3. Watchdog lifecycle

```text
state added (GameRuntime.ts)
  ResolutionBatchExpectation      :91    the one armed expectation: battleId, actionIdentity,
                                         delivered sequence, timer — at most one exists
  pendingResolutionAction         :279   the submitted, unsettled action identity per battle
  resolutionWatchdogMs            :290   construction-time duration (default 2,500 ms)
  resolutionExpectation           :312   the armed expectation, or null
  GameRuntimeOptions                      :821 (GameRuntimeEvents.ts) — resolutionWatchdogMs?
  DEFAULT_RESOLUTION_WATCHDOG_MS           :812 (GameRuntimeEvents.ts) = 2500
```

```text
armed      armResolutionExpectation (:365), reached from
           reconcileResolutionExpectation (:467) on a §4 push (`source === 'push'`) that
           ADVANCES the held sequence of the SAME battle, when pendingResolutionAction
           names that battle. The sequence armed is the delivered state's own
           `state.sequence` — never `expectedSequence`, never `previous + 1`
           (GAME_STATE.md §5.0). Re-arming cancels the previous expectation.
           Recording point for the action identity: the three request paths in
           `requestAction` record it BEFORE the transport call, because §3.1 item 1
           publishes the state before the hub call returns.
cleared    settleResolutionExpectation (:507) when an admitted batch for that battle is
           at or beyond the expectation's sequence (the matching batch), and the
           submitted action once the batch at the held sequence is admitted;
           reconcileResolutionExpectation when a recovery snapshot is ingested or the
           held battle changes; clearRecoveryState (:331) on dispose,
           clearActiveBattleState, invalidateSession, and BATTLE_NOT_FOUND's
           handleBattleNotRecoverable — i.e. every existing battle boundary;
           forgetSubmittedAction (:540) on a definitive rejection (no commit, no batch).
expires    expireResolutionExpectation (:406): publishes sync = 'unsynchronized' plus the
           technical detail through the existing lastError / `runtime_error` channel, then
           `void this.recoverBattleState()` — the existing §7.1 request with its existing
           one-in-flight-per-battle rule and MAX_RECOVERY_ATTEMPTS bound.
inert      the expectation is not the armed one (replaced or cancelled); the runtime is
           disposed; no battle is held; the held battle is a different one; or the held
           sequence has moved past the expectation's — in which case the newer delivered
           state already covers that resolution (§7 precedence) and nothing is recovered.
releases   nothing. Expiry unlocks no input, publishes no battle state, presents nothing,
           and determines no outcome. The lock is released where it is released today —
           by a delivered state, including the `'recovery'` snapshot expiry requests
           (BattleScene.ts converges and releases on `source === 'recovery'`;
           SceneLifecycle.test.ts pins it).
retries    none of its own. R1: one fire per observation; every retry belongs to the
           existing bounded recovery (MAX_RECOVERY_ATTEMPTS = 3, 25/50/100 ms).
```

The exclusions the approved design requires are structural, not special cases:

```text
§3.1 item 6 idempotent retry   re-commits and re-broadcasts nothing → no advance → no arm
duplicate/level push           not an advance → no arm
push with no submitted action  no identity to associate → no arm
definitive rejection           the action's identity is forgotten → no arm in its name
```

---

## 4. Protocol amendment

`docs/02-technical/SIGNALR_PROTOCOL.md`:

```text
version header  v2.24 (was v2.23): records the §7 amendment and repeats that no hub
                method, argument, subscription, delivery path, state-push member, or
                gameplay rule is added or removed
§7 preamble     "every moment" (was "both"), a third `unbatched resolution` entry in the
                moment list, and "No moment is a special case of another" (:2498 …)
§7 new text      :2524 the third moment: one bounded client-local expectation, 2,500 ms
                 initially, armed by a push that advances the sequence for a battle with
                 an unsettled submitted action; the delivered sequence may not be spelled
                 as `expectedSequence + 1`; expiry marks the client unsynchronized and
                 requests the same item 1 snapshot; the expectation is delivery-timing
                 only (no method/argument/subscription/message/member, no replay, no lock
                 release, no outcome, one fire, cleared by the matching batch, by a
                 snapshot that covers it, by battle replacement/clear/end, and on disposal)
                 :2553 the exclusion: an accepted acknowledgement that produces no new
                 broadcast (the §3.1 item 6 idempotent retry, ADR-025 §2) arms nothing and
                 is never reported as a desync; a push, not an acknowledgement, arms
                 :2565 snapshot precedence over an armed expectation, end to end
```

`ADR-008` was **not** modified: D-2 chose §7 only, and the ADR's mechanism — a snapshot,
never an event replay — is unchanged.

---

## 5. Verification (exact commands and results)

All commands run from `src/frontend/client` unless noted.

```text
BASELINE (before any edit — the tree already carried the pre-existing changes of §2)
  npx vitest run tests/GameRuntime.test.ts        → 1 file passed, 192 tests passed
  npx tsc --noEmit                                → exit 0

AFTER THE IMPLEMENTATION
  npx vitest run tests/GameRuntime.test.ts        → 1 file passed, 209 tests passed
                                                    (192 pre-existing + 17 new)
  npx vitest run tests/GameRuntime.test.ts -t "TASK-284"
                                                   → 17 passed | 192 skipped (209)
  npx vitest run tests/GameRuntime.test.ts \
    tests/SceneLifecycle.test.ts tests/RuntimeBoundaries.test.ts
                                                   → 3 files passed, 592 tests passed
  npx vitest run                                   → 24 files passed, 1206 tests passed
  npx tsc --noEmit                                 → exit 0
  git diff --check                                 → exit 0 (clean)

MUTATION CHECKS (temporary, both reverted immediately; run to prove the new tests bite)
  arming disabled in reconcileResolutionExpectation  → 9 of the 17 new tests fail
                                                       (all arming-dependent ones), and the
                                                       exclusion/teardown tests still pass
  sequence taken as previousBattle.sequence + 1      → new test 2 fails: "expected [] to
                                                       deeply equal [ 'battle-1' ]" — the
                                                       delivered-sequence requirement (task
                                                       requirement 4) is pinned
  reverted state re-verified                         → 209/209 passed
```

Scenario coverage (task requirement 8) — all deterministic, `vi.useFakeTimers()` and
controlled promises, no sleeps:

```text
#  required scenario                       test
─  ─────────────────────────────────────  ───────────────────────────────────────────────
1  matching batch before expiry            1. a matching batch inside the window settles the
                                              expectation: no recovery at all
2  state push before RPC acknowledgement   2. a push delivered before the acknowledgement arms
                                              it, at the DELIVERED sequence (also proves the
                                              sequence is not `expectedSequence + 1`)
3  lost batch triggers recovery            3. a lost batch expires at the 2,500 ms default and
                                              requests the existing snapshot (also pins the
                                              default duration: nothing at 2,499 ms, fires at
                                              2,500 ms)
4  batch arrives during recovery           4. a batch arriving while the requested snapshot is
                                              in flight opens no second request
5  snapshot supersedes pending             5. the snapshot supersedes the pending presentation:
   presentation                               a late batch replays nothing
6  recovery failure bounded and locked     6. a failed recovery stays unsynchronized, publishes
                                              no state, and stays bounded (3 attempts, then stop)
7  terminal / expired battle               7. BATTLE_NOT_FOUND keeps its terminal path and
                                              leaves no watchdog behind
8  teardown / reconnect                    8a. dispose; 8b. post-result clear;
                                              8c. a reconnect recovery supersedes the watchdog
                                              instead of racing it
9  an old watchdog cannot recover a newer  9. a watchdog armed for one battle cannot recover the
   battle                                     battle that replaced it; 9b. a newer advance
                                              re-arms the ONE expectation, so only the newest
                                              resolution governs
+  §3.1 item 6 no-broadcast acceptance      excludes a documented no-broadcast acceptance
+  no submitted action                      arms nothing for a push that advances with no action
                                              outstanding (the TASK-280 push-only loss)
+  definitive rejection                     arms nothing for a definitively rejected action
+  configurability                          honours the configured duration instead of 2,500 ms
+  cast identity                            names a cast request's own correlation id
```

Not run, deliberately:

```text
dotnet test / dotnet build   NOT REQUIRED and not run. The amendment adds no hub method,
                             argument, subscription, delivery path, state-push member, or
                             gameplay rule, and touches no C# source, wire message, or
                             shared contract — the task's own scope authority says so
                             (TASK-284 §8: "backend: NOT required"), and no backend test
                             reads SIGNALR_PROTOCOL.md. No backend check is claimed as run.
```

---

## 6. Review against the baseline (requirement 10)

```text
[✓] the emitted wire contract is unchanged         — no new method/argument/subscription/
                                                     message/member; §8 items 3–9 hold
[✓] the client authors nothing                     — no sequence is derived, incremented, or
                                                     written; only delivered values are read
                                                     (GAME_STATE.md §5.0, AGENTS.md §10)
[✓] no second recovery mechanism                   — the same §7.1 request, the same retry bound
[✓] no scene or port change                        — D-6 honored; the boundary tests pass
[✓] input unlocking stays fact-driven              — expiry publishes the sync flip only, which
                                                     the scene reads for the indicator alone;
                                                     SceneLifecycle's "never from elapsed time"
                                                     pin (test at :7239) still passes
[✓] battle-sequence admission, duplicate
    suppression, reconnect, terminal states        — untouched code paths; the 192 pre-existing
                                                     focused tests and the full 1206-test suite
                                                     pass unchanged
[✓] supersession on both sides                     — runtime forwards an at-or-below batch whole
                                                     (scene memory refuses a second presentation);
                                                     a recovery snapshot clears the expectation
[✓] no unapproved scope expansion                  — no ADR edit, no new protocol trigger beyond
                                                     §7, no game rule, mechanic, or content
```

---

## 7. Remaining risks and findings reported, not fixed (`AGENTS.md` §16)

```text
R-1  The watchdog declares "the batch was lost" without ever seeing the batch. A delivery
     that is merely slower than 2,500 ms — a stalled device, a congested link — is
     recovered unnecessarily: the client flips to `unsynchronized`, fetches a snapshot it
     did not need, and the scene converges with 'recovery' semantics (no animation for a
     resolution that did arrive). It never fabricates an outcome and never diverges; the
     cost is presentation. The duration is configurable for exactly this reason, and the
     protocol records it as client policy, not a protocol value.

R-2  Recovery failure remains an honest dead end, unchanged from the pre-existing contract:
     after MAX_RECOVERY_ATTEMPTS the client stays `unsynchronized` with the last reported
     failure, and the scene keeps its lock until a delivered state arrives. That is the
     documented outcome (SIGNALR_PROTOCOL.md §7), not a new one, and it is pinned by new
     test 6 and by SceneLifecycle's held-lock pin.

R-3  A push that advances with no action outstanding is still undetectable (no expectation
     can be armed without an action identity). In that shape no input lock is held —
     locks are taken at submission — so there is no user-visible deadlock; the state the
     client holds is complete for those resolutions. New test "arms nothing for a push that
     advances with no action outstanding" pins it, and it is why the pre-existing TASK-280
     pin (GameRuntime.test.ts test 13, which submits no action) remains green and true for
     its own scenario. Widening the trigger to action-less pushes is a new decision, not a
     reading of D-1 … D-7.

R-4  GameRuntime.test.ts test 13's title ("cannot detect a batch lost after its own state
     push advanced the sequence") is now true only of its action-less shape; the runtime
     can detect that loss when an accepted action is outstanding. The pre-existing
     uncommitted test was preserved byte-for-byte rather than reworded (§2), so the
     narrower claim is recorded here instead. Suggested follow-up: re-title it in a
     documentation-only slice, if wanted.

R-5  TASK-273 / TASK-274 still have no record file in the repository (readiness report
     F-1), although their code and document effects are present. Unchanged by this slice,
     which relied on their effects only as pinned by the existing tests.

R-6  .ai/PROJECT_STATE.md remains stale (readiness report F-5). Unchanged; out of scope.
```

---

## Completion Evidence

### Historical Implementation Commit
- **Commit:** `e33254e8057159f50012746ebf17a7d01acd6d13`
- **Subject:** `feat: implement GameRuntime and battle protocol recovery architecture`
- **Lifecycle Ratification:** Historical composite commit exception under `tasks/TASK_LIFECYCLE.md` §6.4. Git history preserved without rewriting.
- **Implemented Files:**
  - `docs/02-technical/SIGNALR_PROTOCOL.md` (§7 + version header)
  - `src/frontend/client/src/game/runtime/GameRuntime.ts`
  - `src/frontend/client/src/game/runtime/GameRuntimeEvents.ts`
  - `src/frontend/client/tests/GameRuntime.test.ts`
- **Validation Evidence:**
  - `npx vitest run tests/GameRuntime.test.ts` — PASS (209/209 tests pass)
  - `npx tsc --noEmit` — PASS (exit 0)
- **Phase B Filing:**
  - Filed to `tasks/completed/TASK-284A-missing-resolution-batch-watchdog-implementation.md` under Operator Authorization (Backlog Reconciliation Execution).
  - Status finalized as `DONE`.

---

## 8. Git state (committed in e33254e, filed under Phase B)

```text
branch  master        HEAD  425bebb4f6bd1e8019d06bbd92231fb3f9912be3

 M docs/02-technical/SIGNALR_PROTOCOL.md
 M src/backend/GameServer.Domain/Match3/BattleEventBuilder.cs               (pre-existing)
 M src/frontend/client/src/game/runtime/GameRuntime.ts
 M src/frontend/client/src/game/runtime/GameRuntimeEvents.ts
 M src/frontend/client/tests/GameRuntime.test.ts
 M tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs        (pre-existing)
?? tasks/backlog/TASK-276-authoritative-cascade-snapshot-protocol-planning.md   (pre-existing)
?? tasks/backlog/TASK-277-cascade-protocol-decision-and-compatibility-gate.md   (pre-existing)
?? tasks/backlog/TASK-278-cascade-protocol-authorization-gate.md                 (pre-existing)
?? tasks/backlog/TASK-279-investigate-multi-match-gemmatched-duplication.md      (pre-existing)
?? tasks/backlog/TASK-280-investigate-battlehub-state-push-projection-batch-loss.md (pre-existing)
?? tasks/backlog/TASK-283-option-a-missing-resolution-batch-watchdog-authorization.md   (this slice)
?? tasks/backlog/TASK-284-runtime-recovery-for-missing-accepted-action-event-batches.md (pre-existing; status addendum added)
?? tasks/backlog/TASK-284A-missing-resolution-batch-watchdog-implementation.md          (this slice)
?? tests/backend/GameServer.Api.Tests/Hubs/BattleHubPartialBroadcastTests.cs      (pre-existing)

No commit, push, checkout, reset, stash, or untracked-file cleanup was performed.
```
