# TASK-257 — Fix Swap Action Identity, Preconditions Parsing, and Concurrency Coverage

<!--
  GEN-TASK EXECUTION MANIFEST TEMPLATE
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
-->

---

## Metadata

```text
Task ID:           TASK-257
Type:              BUG
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     backend
Supporting Agents: client, realtime, gameplay, testing, review
Workflow:          development/bug-fix.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis,
                   quality/documentation-consistency,
                   gameplay/authority-determinism-audit, testing/test-scenario-generation
Dependencies:      TASK-256 (DONE), TASK-255 (SUPERSEDED)
Declared Files:    docs/03-decisions/ADR/ADR-025-swap-action-preconditions-and-idempotency.md,
                   docs/01-game-design/MATCH3_RULES.md,
                   docs/02-technical/SIGNALR_PROTOCOL.md,
                   docs/02-technical/REDIS_STATE.md,
                   src/backend/GameServer.Application/Battle/BattleStateService.cs,
                   src/backend/GameServer.Api/Hubs/BattleHub.cs,
                   src/frontend/client/src/services/realtime/SignalRService.ts,
                   src/frontend/client/src/game/runtime/GameRuntime.ts,
                   src/frontend/client/src/game/runtime/ActionIdentity.ts,
                   tests/backend/GameServer.Application.Tests/BattleStateServiceTests.cs,
                   tests/backend/GameServer.Application.Tests/BattleStatePersistenceContractTests.cs,
                   tests/backend/GameServer.Application.Tests/RelicStageResolutionTests.cs,
                   tests/backend/GameServer.Application.Tests/Task173SonThachVeOncePerBattleFiringTests.cs,
                   tests/backend/GameServer.Application.Tests/BossSkillSecondaryEffectTests.cs,
                   tests/backend/GameServer.Application.Tests/InMemoryBattleStateRepository.cs,
                   tests/backend/GameServer.Application.Tests/ContendedBattleStateRepository.cs,
                   tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs,
                   src/frontend/client/tests/ActionIdentity.test.ts,
                   src/frontend/client/tests/GameRuntime.test.ts,
                   src/frontend/client/tests/SignalRService.test.ts
```

---

## Objective

Close the confirmed post-TASK-256 correctness gaps in the Swap action contract
without changing the ADR-025 architecture: give every logical action an identity
that is unique across runtime resets (a page reload must not reuse a previous
action's id), keep a retry of one logical action on its original identity, make
the idempotency recognition window and the legacy no-precondition form explicit,
parse the SignalR action token strictly so a stated sequence is never discarded
or guessed at, and prove the concurrency and reconnect properties with
deterministic regression tests rather than with reasoning alone.

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-025-swap-action-preconditions-and-idempotency.md` — approved precondition/idempotency architecture (§1–§4)
- `docs/01-game-design/MATCH3_RULES.md` §2.1.1–§2.1.6 — swap identity, validation order, staleness and idempotency, rejection no-op
- `docs/02-technical/SIGNALR_PROTOCOL.md` §2, §2.1, §5, §6 — hub methods, action token, acknowledgement codes, sequencing
- `docs/02-technical/REDIS_STATE.md` §4 — `Sequence` compare-and-set and the refused-write path
- `docs/02-technical/GAME_STATE.md` §2.1.10 — the single committed-swap record
- `docs/01-game-design/GAME_RULES.md` §18, `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — server authority
- `AGENTS.md` §10–§11, §14–§18, §20; `tasks/TASK_LIFECYCLE.md` §6

---

## Acceptance Criteria

- [x] A fresh client runtime cannot reuse an action id a previous logical action used.
- [x] A retry of one logical action carries its original id; a settled action's next submission does not.
- [x] `expectedSequence` and `actionId` are separate, explicit contract values end to end.
- [x] A reused `ActionId` with a different payload is rejected, never silently accepted.
- [x] The retry-recognition window is documented and tested (identity is compared with the most recently committed Swap only).
- [x] The action token's accepted forms are explicit and a bare integer is never read as an action id.
- [x] A stated sequence is never silently discarded because of a missing or malformed delimiter.
- [x] A request without a precondition can no longer be re-executed against a state version its caller never observed.
- [x] Deterministic concurrency regressions cover same-identity, different-identity, and CAS-conflict reload cases.
- [x] Frontend reconnect/reload coverage proves a fresh runtime submits a legitimate swap with a new id.
- [x] The broadcast-ordering finding is assessed and reported.
- [x] Backend and frontend unit suites pass; frontend build passes; E2E not run.

---

## Scope

### In Scope
- Action-identity generation and retention in the client runtime.
- Idempotency/precondition evaluation in `BattleStateService`.
- `Swap` action-token parsing in `BattleHub` and token formatting in `SignalRService`.
- Deterministic concurrency test doubles and regressions.
- Documentation of the behaviors above in their owning documents.

### Out of Scope
- Any change to the ADR-025 architecture (no new state members, no protocol fields, no per-battle locking).
- Widening the idempotency window (a committed-action history) — a separate state-shape decision.
- E2E suites.
- The stale `SwapRejectionReason` XML documentation discovered while working (reported, not fixed).

### Test Alignment
Four existing Application tests drove the compare-and-set retry path with a
request that stated no precondition (a bare `SwapRequest(from, to)`). That path is
now closed for such a request by `MATCH3_RULES.md` §2.1.4 item 1 /
`SIGNALR_PROTOCOL.md` §2 item 3, so those requests now state the state version
they observed. Their subjects — the refused write's retry, one write-back per
resolution, exactly-once once-per-battle activation, and deterministic
re-resolution — are unchanged; only the request shape is a conforming one.

- `BattleStatePersistenceContractTests.cs` — `StaleResolution_ShouldBeAbortedAndRetriedAgainstTheFreshState`, `RepeatedStaleResolution_ShouldNeverOverwriteTheNewerRecord`
- `RelicStageResolutionTests.cs` — `CommittedSwap_ShouldRetryAgainstFreshState_AndStillResolveStepEleven`
- `Task173SonThachVeOncePerBattleFiringTests.cs` — `RefusedWriteAndRetry_ShouldStillActivateExactlyOnce`
- `BossSkillSecondaryEffectTests.cs` — `RetriedCompareAndSet_ShouldCommitTheEffectsExactlyOnce`

---

## Confirmed Root Causes and Changes

### A. Action identity reused after a reload (confirmed)
`GameRuntime.nextClientSequence` built the Swap's identity from a module-scoped
counter (`swap_1`, `swap_2`, …). A page reload is a fresh module evaluation, so
the counter restarted and the first swap of a pair after a reload reproduced an
identity an earlier logical action had used. The server's identity check then
answered the new swap as a retry of the old one — accepted, no events, no
execution — or rejected it as `INVALID_ACTION_PAYLOAD` when the payload differed.

**Change:** `src/frontend/client/src/game/runtime/ActionIdentity.ts` generates
the identity from `crypto.randomUUID()` where available, then from a random
`crypto` value, then from time plus a random component — never from a counter
alone. `GameRuntime` retains the identity of an unresolved logical action keyed by
battle, kind, and the unordered pair, so a retry keeps its identity and a settled
or different action never does. `expectedSequence` and `actionId` are now
separate parameters through `SignalRService.swap`, which can no longer emit the
bare-sequence token the hub refuses.

### B. Idempotency recognition (verified, then made explicit)
The identity check already ran before the precondition check, which is what makes
a retry recognizable when it arrives carrying the committed sequence; a reused
identity with a different payload was already rejected. What was missing was the
defined relationship between the original precondition, the committed sequence,
and the current sequence, and the statement that recognition is one committed
Swap wide (`GAME_STATE.md` §2.1.10 item 5 keeps one record). That window is now
documented in `MATCH3_RULES.md` §2.1.4 item 2, `SIGNALR_PROTOCOL.md` §2 item 1,
and ADR-025 §2, and covered by tests: a retry carrying the original precondition
and one carrying the committed sequence are both replays; a retry after a later
action committed is `STALE_ACTION`, never a replay and never a second write.

### C. Token parsing dropped a stated sequence (confirmed)
`BattleHub.Swap` split on the first `:` only when it was at a positive index and
the prefix parsed as an integer; anything else fell through with
`actionId = clientSequence` and **no** precondition. So `":act-1"`, `"abc:act-1"`,
and `"0:"` silently became precondition-less actions, and `"0"` became an action
identity, discarding the sequence the caller stated. A request in that shape
executed without the one check that detects an obsolete state.

**Change:** the token is now parsed by an exact, total grammar — absent, `"{expectedSequence}:{actionId}"`, or the legacy `"{actionId}"` — with the sequence restricted to an
unsign-prefixed, unpadded, non-negative decimal integer. Anything else is refused
with the new transport-level code `INVALID_ACTION_TOKEN` before the Application
layer sees it, so a malformed token can never be downgraded into a
precondition-less action.

### D/E. The legacy form could be re-executed against an unseen state (confirmed)
`BattleStateService.ExecuteSwapAsync` retried a refused compare-and-set by
re-resolving against the freshly read state. For a request that stated no
precondition there was no check to stop that, so an obsolete request executed
against a board version its caller never observed — the bypass
`MATCH3_RULES.md` §2.1.4 item 1 exists to prevent. **Change:** a request that
states no precondition is rejected as `STALE_ACTION` when its write is refused;
a request that states one is still rejected once its precondition no longer holds
against the fresh record. Documented in `MATCH3_RULES.md` §2.1.4 item 2,
`SIGNALR_PROTOCOL.md` §2 item 3 / §6 item 5, `REDIS_STATE.md` §4 item 2, and
ADR-025 §4.

### F. Broadcast ordering (assessed, not a reproducible defect)
See Remaining Issues.

---

## Regression Tests Added

| Test | Proves |
| --- | --- |
| `ExecuteSwap_RetryCarryingTheCommittedSequence_ShouldStillBeRecognisedAsAReplay` | Identity is evaluated before the precondition, so a retry carrying either the original or the committed sequence is answered as a replay with no write, no event, and no state change. |
| `ExecuteSwap_RetryAfterALaterActionCommitted_ShouldRejectAsStaleNotAsAReplay` | The documented one-committed-Swap recognition window: a retry outside it is `STALE_ACTION`, not a replay, and writes nothing. |
| `ExecuteSwap_TwoConcurrentRequestsWithOneActionIdentity_ShouldCommitOnceAndAnswerTheOtherAsARetry` | Two deliveries of one logical action racing from one read version commit exactly once (one write-back), and the other is answered as a replay with no events. |
| `ExecuteSwap_TwoConcurrentRequestsWithDifferentActionIdentities_ShouldCommitAtMostOne` | Two different actions from the same observed version cannot both commit; the loser is `STALE_ACTION` and only one write-back happens. |
| `ExecuteSwap_WhenAConcurrentWinnerMovesTheState_ShouldRejectAnObsoletePreconditionAndWriteNothing` | A refused compare-and-set followed by a reload rejects an obsolete precondition instead of executing against the newer state, and leaves the competitor's record intact. |
| `ExecuteSwap_WithoutAPrecondition_ShouldNotBeReExecutedAgainstAStateThatMovedOn` | The confirmed bypass: the legacy form is rejected as `STALE_ACTION` on a refused write. Verified sensitive — it fails against the pre-fix code path. |
| `ExecuteSwap_WithoutAPrecondition_ShouldStillCommitAgainstTheStateItRead` | The legacy form still commits when no other writer moved the state. |
| `Swap_WithSequenceAndActionIdToken_ShouldApplyThePreconditionAndTheIdentity` (Api) | The two-value token's identity is parsed and retained: an identical retry emits no second event batch, and the same identity on another pair is `INVALID_ACTION_PAYLOAD`. |
| `Swap_WithAnObsoleteSequenceInTheToken_ShouldRejectAsStaleAction` (Api) | The token's sequence is parsed and enforced — the case that fails if the sequence part is dropped. |
| `Swap_WithALegacyActionIdToken_ShouldKeepTheDocumentedPreconditionlessForm` (Api) | The documented legacy form is preserved, with its identity still deduplicating. |
| `Swap_WithAnAbsentToken_ShouldResolveWithoutAPrecondition` (Api) | The absent form is accepted and invents no value. |
| `Swap_WithAMalformedOrAmbiguousToken_ShouldRejectAsInvalidActionToken` (Api, 8 cases) | `"0"`, `"7"`, `":act-1"`, `"abc:act-1"`, `"0:"`, `"0:act:1"`, `" 0:act-1"`, `"-1:act-1"` are refused with `INVALID_ACTION_TOKEN` and resolve nothing. |
| `Swap_ShouldPushTheCommittedStateBeforeItsEvents` (Api) | `SIGNALR_PROTOCOL.md` §6 item 4: the committed state push precedes its event batch. |
| `createActionId` suite (`ActionIdentity.test.ts`) | Uniqueness per call, token-grammar safety, all three entropy paths, and no identity reproduction after a fresh module evaluation. |
| `never reuses an action identity from an earlier client runtime` (`GameRuntime.test.ts`) | A reloaded runtime submitting the same swap does not reuse the earlier runtime's identity. |
| `preserves one logical action's identity for a retry after a transport failure` | A retry keeps its identity (including in reversed cell order) and a settled action's next submission does not. |
| `never emits a bare sequence as a Swap action token`, `refuses an action identity the token grammar cannot express unambiguously`, `refuses a precondition that is not a state version` (`SignalRService.test.ts`) | The client cannot put a token on the wire that the server would have to guess at. |

---

## Validation Results

```text
dotnet test src/backend/GameServer.sln --nologo
  PASS — 3,073 passed / 0 failed
          Domain 1,565 · Infrastructure 476 · Api 359 · Application 673

dotnet test tests/backend/GameServer.Application.Tests (targeted, mid-task)
  PASS — 673/673 after the four alignment edits

npm run test:run            (src/frontend/client)
  PASS — 24 files, 1,074 passed / 0 failed

npm run build               (src/frontend/client, tsc && vite build)
  PASS — built in 2.44s

E2E: NOT RUN (excluded by the task; not reported as passing).
```

---

## Completion Evidence

### Commit
- Implementation slice (Phase A): `047fcb33bff5e8130bee5d726dfb373d0ab3efdb`
- Subject: `fix(swap): give each logical swap a unique identity and honour its precondition`
- Owns: `TASK-257`
- Files: exactly the 20 declared files (see `## Metadata`)
- Shared with: `none`
- Unowned / pre-existing: `none`
- Completion record filing (Phase B): this completion record alone, filed under its own bookkeeping commit (`tasks/completed/TASK-257-fix-swap-action-identity-preconditions-and-concurrency-coverage.md`).

### Changed Files
- `docs/03-decisions/ADR/ADR-025-swap-action-preconditions-and-idempotency.md`: §2 states the exact token form and the one-Swap recognition window; §4 states that a precondition-less request is rejected on a refused compare-and-set.
- `docs/01-game-design/MATCH3_RULES.md`: §2.1.4 item 2 completed with the recognition window and the no-precondition limitation; version header updated. No rule changed.
- `docs/02-technical/SIGNALR_PROTOCOL.md`: §2 items 1–3 restate the token grammar and add `INVALID_ACTION_TOKEN`; §2.1 table row and §5 item 3 updated; §6 item 5 states the no-precondition consequence; version header updated.
- `docs/02-technical/REDIS_STATE.md`: §4 item 2 states that the retry never executes an action against a state version the request did not name; version header updated.
- `src/backend/GameServer.Application/Battle/BattleStateService.cs`: identity-first ordering documented; the precondition-less request is rejected on a refused write; method documentation states the precondition/identity contract.
- `src/backend/GameServer.Api/Hubs/BattleHub.cs`: `TryParseSwapActionToken` (exact grammar), `INVALID_ACTION_TOKEN`, corrected method documentation, `BATTLE_NOT_FOUND` constant reused.
- `src/frontend/client/src/services/realtime/SignalRService.ts`: `swap` takes `expectedSequence` and `actionId` explicitly and formats the token through `formatSwapActionToken`, which refuses a missing, colon-bearing, or all-digit identity and a non-version precondition.
- `src/frontend/client/src/game/runtime/GameRuntime.ts`: Swap identities come from `createActionId`, are retained per unresolved logical action, and are cleared on acknowledgement, battle change, recovery failure, and session invalidation.
- `src/frontend/client/src/game/runtime/ActionIdentity.ts` (new): the identity generator with its documented entropy paths.
- `tests/backend/GameServer.Application.Tests/BattleStateServiceTests.cs`: the seven new regressions and the widened `NewService` factory.
- `tests/backend/GameServer.Application.Tests/ContendedBattleStateRepository.cs` (new): thread-safe store double with a read rendezvous and an injectable competitor.
- `tests/backend/GameServer.Application.Tests/InMemoryBattleStateRepository.cs`: `ApplyConcurrentWrite`, so a competitor's commit is injectable without being counted as the request's write.
- `tests/backend/GameServer.Application.Tests/BattleStatePersistenceContractTests.cs`, `RelicStageResolutionTests.cs`, `Task173SonThachVeOncePerBattleFiringTests.cs`, `BossSkillSecondaryEffectTests.cs`: the compare-and-set retry tests now state the precondition they observed (see Test Alignment).
- `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs`: the token-grammar and push-order regressions.
- `src/frontend/client/tests/ActionIdentity.test.ts` (new), `GameRuntime.test.ts`, `SignalRService.test.ts`: identity uniqueness/reload/retry and token-format regressions.

### Server Authority & Scope Verification
- [x] No gameplay value is computed on the client: the changes carry a request token and an opaque identity, and compute no state.
- [x] No new state member, wire member, hub method, event, or protocol field: a rejection code and a documentation-exact token grammar are the whole protocol delta.
- [x] No per-battle locking or new concurrency mechanism was introduced.
- [x] MVP scope unchanged (`MVP_SCOPE.md` §1).
- [x] P-1…P-6 commit policy compliance; Phase A staged set verified as exactly the 20 declared files.

---

## Remaining Issues

1. **Broadcast ordering (audit item F) — not a reproducible correctness defect.**
   `BattleHub.Swap` sends `BattleStateUpdated` and then `ReceiveEvents` in one
   continuation, so the documented order (`SIGNALR_PROTOCOL.md` §6 item 4) holds
   for a commit; the new `Swap_ShouldPushTheCommittedStateBeforeItsEvents` test
   asserts it. Two overlapping commits could only interleave if a second action
   could commit from the same observed version — which `MATCH3_RULES.md` §2.1.4
   item 4 forbids and the new concurrency regressions now prove at the service
   level, and which the legacy re-resolution path (fixed here) was the only way to
   reach. The residual risk is a scheduling delay between the two sends of one
   invocation, which is not a protocol violation and which `SIGNALR_PROTOCOL.md`
   §6 item 3 already handles on the client (a skipped `serverSequence` is a
   desync, resolved by the §7 snapshot). No per-battle outbound sequencer was
   added, because no evidence shows it is needed; if multi-client or
   fire-and-forget action submission is ever introduced, that guarantee needs its
   own ADR.
2. **Stale XML documentation on `SwapRejectionReason.StaleAction`**
   (`src/backend/GameServer.Domain/Match3/SwapRejectionReason.cs`, lines ~58–80):
   the member's `<summary>` still describes the ADR-010 coordinate lock
   ("the pair most recently committed to the board", "exchanged once per Turn at
   most") and was left unaddressed by TASK-256. It is inaccurate documentation of
   the implemented rule. Reported, not fixed here: it is outside this task's
   declared file set, and correcting it should be its own (documentation) change.
   Suggested follow-up: a small documentation task updating that member's
   summary to the ADR-025 meaning.
3. **CardCast/PetSkillCast correlation ids still use the per-process counter.**
   ADR-025 defines no precondition or identity for those actions and the server
   deduplicates neither, so the counter is sufficient and unchanged. Recorded so
   the asymmetry is deliberate rather than an oversight.
