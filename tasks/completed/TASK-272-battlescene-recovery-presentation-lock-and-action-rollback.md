# TASK-272 — BattleScene Recovery Convergence, Presentation Lock Release & Action Rollback

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
-->

---

## Metadata

```text
Task ID:           TASK-272
Type:              BUG
Status:            DONE
Risk:              MEDIUM
Priority:          CRITICAL
Primary Agent:     client
Supporting Agents: realtime, testing, review
Workflow:          development/bug-fix.md
Skills:            client/phaser-battle-presentation, client/client-state-authority, realtime/realtime-protocol-validation, testing/test-scenario-generation, quality/implementation-review
Dependencies:      None
Declared Files:    src/frontend/client/src/game/runtime/GameRuntime.ts, src/frontend/client/src/game/runtime/GameRuntimeEvents.ts, src/frontend/client/src/game/scenes/BattleScene.ts, src/frontend/client/tests/GameRuntime.test.ts, src/frontend/client/tests/SceneLifecycle.test.ts, src/frontend/client/tests/BattleEventPresentation.test.ts, src/frontend/client/tests/ResultScene.test.ts
```

---

## Objective

Close the remaining scene-level correctness gaps left by the P0.1/P0.2 work: an authoritative recovery snapshot (`onBattleState(state, { source: 'recovery' })`) must converge the scene directly — board, HUD, health, turn-owned readouts and Status Effects — without requiring a matching `ReceiveEvents` batch, cancelling stale presentation work and releasing every presentation lock through one explicit, idempotent lifecycle; a definitive rejection must roll the board back onto the newest authoritative state while a transport-uncertain outcome must never be treated as "not sent"; stale callbacks and late cross-battle responses must not write into the active scene; and `GameRuntimeState.sync` must drive one non-blocking synchronization indicator.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Confirm feature is in MVP scope (Match-3 resolution, server authority, combat flow)
- `docs/01-game-design/GAME_RULES.md` §18 — Server authority; the client renders, never computes
- `docs/01-game-design/MATCH3_RULES.md` §2.1.5 — A rejected Swap is a gameplay no-op
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.1 — The resolution's state write-back precedes its `ReceiveEvents` batch
- `docs/02-technical/SIGNALR_PROTOCOL.md` §5 — The action acknowledgement; `accepted: false` is a definitive rejection
- `docs/02-technical/SIGNALR_PROTOCOL.md` §6 item 3 — `serverSequence` order and desync
- `docs/02-technical/SIGNALR_PROTOCOL.md` §7 items 1–2 — Reconnect/resync: discard local prediction, re-render from the snapshot, never replay missed events
- `docs/02-technical/SIGNALR_PROTOCOL.md` §8.3 — No battle lifecycle/status message exists
- `docs/02-technical/GAME_STATE.md` §5 / §5.1 item 3 — `Sequence` is the post-resolution version; a duplicate is the same complete resolution
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle-resolution.md` — The server is authoritative
- `docs/03-decisions/ADR/ADR-008-reconnect-recovery-strategy.md` — Recovery is a snapshot, never an event replay
- `docs/03-decisions/ADR/ADR-025-swap-action-preconditions-and-idempotency.md` §2 — The logical action identity a retry reuses
- `docs/02-technical/ARCHITECTURE.md` §2.2.1 rules 3–5 — The runtime coordinates; the scene renders; runtime state is technical

---

## Scope

### In Scope
- **Recovery convergence (scene).** `metadata.source === 'recovery'` is applied directly and immediately (no hold, no batch awaited), and converges/cancels every in-flight presentation first.
- **Explicit, idempotent lock release.** One lifecycle — `BattleScene.invalidatePendingPresentation()` — bumps a presentation generation and a submission serial, cancels the timeline (pacing, gem travels, transient effects, callout), converges held values, and releases the resolution/submission locks. Used by the recovery path and by the delivered-battle-changed/ended path.
- **Presentation generation.** `advancePresentation`'s pacing callback is bound to the generation it was scheduled under, so an abandoned timeline's callback cannot advance a newer timeline or release a newer action's lock.
- **Accepted-resolution seeding.** A recovered snapshot's `sequence` becomes the newest resolution the scene has accepted, so a late batch for a resolution the snapshot already contains is recorded but never replayed.
- **Definitive rejection rollback.** `accepted: false` drops the gesture and converges the board and every timeline-owned readout onto the **current** authoritative state (never a snapshot captured at submission).
- **Transport-uncertain outcomes.** `GameRuntime.requestAction` raises the new `RuntimeActionOutcomeUnknownError` when a submitted action is never acknowledged, publishes `sync: 'unsynchronized'`, and requests the authoritative snapshot (`recoverBattleState`); the scene keeps the committed pair, keeps the resolution pending, and never reports "not sent".
- **Stale-continuation guard.** Every action continuation is gated on `isCurrentSubmission` (serial + battle id), so a late answer cannot report feedback, clear a newer pair, or re-lock the scene.
- **Synchronization indicator.** One non-interactive line in the boss band driven by `GameRuntimeState.sync` (`unsynchronized` visible, `synchronized`/`awaiting_battle` hidden), disposed with the shell.
- **Tests.** 11 scene-level scenarios (`SceneLifecycle.test.ts`, new P0.3 suite) and 4 runtime-level scenarios (`GameRuntime.test.ts`, new P0.3 suite).

### Out of Scope
- No server-side change: no hub method, wire member, event, endpoint, Redis or PostgreSQL change.
- No new `GameRuntimePort` member (the port's member set is pinned by `RuntimeBoundaries.test.ts`); recovery is initiated inside `GameRuntime.requestAction`, which is the only place that knows an acknowledgement never arrived.
- No gameplay rule, balance, RNG, or combat-resolution change (`AGENTS.md` §7, §10).
- No gap-triggered recovery on `serverSequence` skips (`SIGNALR_PROTOCOL.md` §6 item 3): the runtime is documented not to act on `serverSequence` beyond reporting, and the scene has no recovery capability on the port. Reported as a finding, not implemented here — this task's scope is unchanged by that finding. **Follow-up: TASK-273** addresses it (withholding a batch ahead of the synchronized state and requesting the §7.1 snapshot; refusing to present a batch the state the scene renders does not represent).
- No commit or push (operator instruction).

---

## Current State

Before this task, `BattleScene` subscribed with `onBattleState((state) => …)` and dropped the delivery metadata, so a §7.1 recovery snapshot was handled as an ordinary §4 push: a snapshot level with the resolution being presented left the timeline (and its lock) running, a snapshot that matched the awaited `settledBoardSequence + 1` was *held* for a batch that a recovery may never deliver, and no late batch suppression existed for the sequence a snapshot already contained. `submitSwap` treated every thrown error as "Swap not sent" — asserting an outcome the client does not have — and reported a rejected swap without converging the board. Nothing rendered `GameRuntimeState.sync`.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-272:
```text
src/frontend/client/src/game/runtime/GameRuntime.ts
src/frontend/client/src/game/runtime/GameRuntimeEvents.ts
src/frontend/client/src/game/scenes/BattleScene.ts
src/frontend/client/tests/GameRuntime.test.ts
src/frontend/client/tests/SceneLifecycle.test.ts
src/frontend/client/tests/BattleEventPresentation.test.ts
src/frontend/client/tests/ResultScene.test.ts
```

*(Note: The task manifest file itself is excluded from the implementation declared file set. The list above must strictly match the `Declared Files:` field in `## Metadata`.)*

**Shared / pre-existing disclosure (P-3).** `GameRuntime.ts`, `GameRuntimeEvents.ts` and `tests/GameRuntime.test.ts` already carried uncommitted P0.1/P0.2 work before this task started; this task extends them. The two harness files `tests/BattleEventPresentation.test.ts` and `tests/ResultScene.test.ts` changed only in their text-object mocks (a `setVisible`/`visible` member), which the scene's new indicator requires. No other uncommitted path is touched: `src/backend/GameServer.Api/Hubs/BattleHub.cs`, `tests/backend/GameServer.Api.Tests/Hubs/BattleHubActionIdempotencyTests.cs`, `tasks/artifacts/TASK-194-baseline-simulation-results.json`, `tasks/artifacts/TASK-197-controlled-balance-experiments.json` and `run-dev.bat` are left exactly as found.

---

## Acceptance Criteria

- [x] **AC-1 (Recovery convergence):** A `source: 'recovery'` state is applied directly — board, Boss/Pet HP, Power, Passive pair, Status Effects, Combo/Match — with no `ReceiveEvents` batch required, and it releases every presentation lock.
- [x] **AC-2 (Cascade/combat interruption):** A recovery arriving during a cascade timeline and during a boss/player combat animation abandons the timeline exactly where it stood and converges the scene on the snapshot.
- [x] **AC-3 (Stale callbacks):** A pacing callback of an abandoned timeline cannot advance a newer timeline, restore a stale board, or release a newer lock.
- [x] **AC-4 (Idempotent release):** Repeated (including equal-sequence) recoveries release the pending presentation exactly once and leave the machine `idle`.
- [x] **AC-5 (Push unchanged):** Ordinary `source: 'push'` deliveries keep the event-driven animation and the hold/convergence contract.
- [x] **AC-6 (Rejection rollback):** A definitive rejection converges the board and the timeline-owned readouts onto the newest authoritative state and presents lightweight feedback; the refused pair is never presented later.
- [x] **AC-7 (Transport uncertainty):** An unanswered action raises `RuntimeActionOutcomeUnknownError`, preserves the logical identity, publishes `sync: 'unsynchronized'`, requests the snapshot, and is reconciled by it — never reported as "not sent".
- [x] **AC-8 (Cross-battle safety):** A late response for a battle that is no longer active cannot settle, report, or lock the active scene.
- [x] **AC-9 (Sync indicator):** The indicator is visible while `unsynchronized`, hidden for `synchronized`/`awaiting_battle`, non-interactive, inside the boss band, and released by teardown.
- [x] **AC-10 (No leaks):** Repeated recoveries and scene shutdown leak no timer, tween, listener, or Phaser object.
- [x] **AC-11 (Explicit lifecycle):** Lock release is one named, idempotent lifecycle — not force-unlock calls scattered through unrelated methods.
- [x] **AC-12 (Verification):** Frontend typecheck/build and the full frontend suite pass; unrelated pre-existing changes and task artifacts remain untouched.

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)
[x] src/frontend/client/ (scenes / runtime / services / state / ui)
[x] tests/ (unit / integration / gameplay scenarios)
[ ] docs/ (documentation updates if applicable)
```

---

## Implementation Notes

- `GameRuntimeEvents.ts` — `RuntimeActionOutcomeUnknownError` (`kind`, `cause`; the transport's own message is preserved inside this error's message). Declared after `RuntimeActionNotImplementedError` so the `GameRuntimePort` member-set guard in `RuntimeBoundaries.test.ts` is unaffected.
- `GameRuntime.ts` — `requestAction`'s Swap/CardCast/PetSkillCast arms wrap the transport call and, on failure, call `markActionOutcomeUnknown` (`sync: 'unsynchronized'` + `void recoverBattleState()`) and throw the new error. The unresolved Swap identity is untouched.
- `BattleScene.ts` — `renderBattleState(state, metadata?)` branches on `metadata?.source === 'recovery'`; `invalidatePendingPresentation()` is the single release lifecycle; `presentationGeneration` guards the pacing callback; `seedAcceptedResolution` seeds `acceptedResolutionSequence`; `beginSubmission`/`isCurrentSubmission` gate every continuation; `rollbackToAuthoritativeState()` converges a rejection; `syncText` + `renderSyncIndicator` present `GameRuntimeState.sync`.
- `SceneLifecycle.test.ts` — harness gains `setBattleState(state, metadata?)`, a `visible` member on text objects, and `pendingTweenRecords().completion`; new P0.3 suite of 11 tests.
- `GameRuntime.test.ts` — new P0.3 suite of 4 tests.
- `BattleEventPresentation.test.ts` / `ResultScene.test.ts` — text-object mocks gain `setVisible`/`visible` only.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — src/frontend/client/tests/SceneLifecycle.test.ts (P0.3 suite, 11 tests)
[x] Unit tests         — src/frontend/client/tests/GameRuntime.test.ts (P0.3 suite, 4 tests)
[x] Regression         — src/frontend/client/tests/BattleEventPresentation.test.ts, ResultScene.test.ts, CastFeedbackWaitPredicate.test.ts, RuntimeBoundaries.test.ts
[x] Full suite         — npm run test:run (frontend)
[x] Typecheck/build    — npx tsc --noEmit / npm run build
```

### Key Edge Cases
- Recovery while a cascade timeline is mid-phase, and while a boss/player combat animation is playing.
- Recovery level with the resolution being presented (`GameState.md` §5.1 item 3) and recovery ahead of it.
- Late pacing callback landing after a newer timeline started.
- Rejection whose state push arrived while the request was in flight (rollback must not apply an obsolete snapshot).
- Transport failure with an unknown commit outcome vs a definitive rejection vs a pre-submission refusal.
- Late acknowledgement for a battle that has been replaced.

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7.
- If a change would require client-authoritative gameplay computation: STOP per `AGENTS.md` §10.
- If a wire/port/state contract would have to change: STOP and propose it separately (`AGENTS.md` §18).

---

## Completion Evidence

### Commit
- **Historical Composite Commit:** `425bebb4f6bd1e8019d06bbd92231fb3f9912be3`
- **Subject:** `feat: implement authoritative turn lifecycle, battle recovery, and match-3 animation pipeline`
- **Lifecycle Ratification:** Ratified by Operator Decision PO-2 as a historical composite commit under `tasks/TASK_LIFECYCLE.md` §6.4. Git history is preserved; no amend, reset, rebase, or rewrite.
- **§6.4 Historical Exception Disclosure:**
  - Commit `425bebb` did NOT follow the canonical two-stage commit protocol (Phase A implementation slice commit and Phase B completion record filing commit).
  - It was a broad composite batch commit combining multiple tasks (TASK-272, TASK-275, etc.) and task manifests into history in a single commit.
  - The commit body lacked the prescribed ownership metadata (`owns:`, `files:`, `shared with:`, `unowned/pre-existing:`).
  - No dedicated TASK-272 Phase A commit exists; commit `425bebb` is treated strictly as an exceptional historical composite commit under §6.4.
- **Status & Phase B Filing Record:**
  - All 12 acceptance criteria (AC-1 through AC-12) are verified and covered by the committed code and tests in `425bebb`.
  - Under Operator Authorization (Backlog Reconciliation Execution), Phase B completion record is filed to `tasks/completed/TASK-272-battlescene-recovery-presentation-lock-and-action-rollback.md` under historical composite commit exception §6.4.
  - Status is finalized as `DONE`.

### Changed Files
- `src/frontend/client/src/game/runtime/GameRuntimeEvents.ts` — added `RuntimeActionOutcomeUnknownError` (the definitive-rejection vs unknown-outcome distinction).
- `src/frontend/client/src/game/runtime/GameRuntime.ts` — action submissions wrap the transport call: on no acknowledgement, publish `sync: 'unsynchronized'`, request the authoritative snapshot, preserve the Swap identity, and raise the unknown-outcome error (Swap, CardCast, PetSkillCast).
- `src/frontend/client/src/game/scenes/BattleScene.ts` — recovery-source convergence (`invalidatePendingPresentation`, `seedAcceptedResolution`), presentation-generation guard on pacing, submission serial/battle-id continuation guard, rejection rollback, uncertain-outcome handling, and the `GameRuntimeState.sync` indicator.
- `src/frontend/client/tests/SceneLifecycle.test.ts` — harness support (battle-state metadata, text visibility, tween completion) and the P0.3 suite (11 tests).
- `src/frontend/client/tests/GameRuntime.test.ts` — P0.3 runtime suite (4 tests).
- `src/frontend/client/tests/BattleEventPresentation.test.ts`, `src/frontend/client/tests/ResultScene.test.ts` — text-object mocks gain `setVisible`/`visible`.

### Validation Results
- `npx tsc --noEmit` (cwd `src/frontend/client`) — PASS (exit 0).
- `npx vitest run` (cwd `src/frontend/client`) — PASS (24 files, 1206 tests, 0 failures).
- Focused: `npx vitest run tests/SceneLifecycle.test.ts -t "P0.3"` — PASS (11/11); `npx vitest run tests/GameRuntime.test.ts -t "P0.3"` — PASS (4/4).

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed no wire, port, state-model, or backend change
- [x] Confirmed no commit/push performed (operator instruction)

---

## Section 6.4 Lifecycle Reconciliation Audit

<!--
  RECONCILIATION AUDIT UNDER tasks/TASK_LIFECYCLE.md §6.4
  Ratified by Operator Decision PO-2.
-->

### 1. Verification of Acceptance Criteria in Commit `425bebb`

| AC | Status | Implementation Evidence | Test Verification |
| --- | --- | --- | --- |
| **AC-1** (Recovery convergence) | **VERIFIED** | `BattleScene.ts`: `renderBattleState` branches on `metadata?.source === 'recovery'`, invoking `invalidatePendingPresentation()`, resetting locks, and directly converging board, HUD, HP, Power, and status readouts without awaiting events | `SceneLifecycle.test.ts` (P0.3 suite): Test 1 (cascade recovery), Test 2 (combat recovery), Test 5 (direct recovery without batch) |
| **AC-2** (Cascade/combat interruption) | **VERIFIED** | `BattleScene.ts`: `invalidatePendingPresentation()` stops `presentationTimeline`, kills active tweens and effects, and cleanly abandons in-flight cascade or combat animations | `SceneLifecycle.test.ts` (P0.3 suite): Test 1 (abandons mid-cascade timeline), Test 2 (abandons mid-combat timeline) |
| **AC-3** (Stale callbacks) | **VERIFIED** | `BattleScene.ts`: `presentationGeneration` incremented on invalidation; pacing callbacks check `generation === this.presentationGeneration` before advancing or touching the board | `SceneLifecycle.test.ts` (P0.3 suite): Test 3 (abandoned pacing callback cannot advance newer timeline or alter board) |
| **AC-4** (Idempotent release) | **VERIFIED** | `BattleScene.ts`: `invalidatePendingPresentation()` is fully idempotent; repeated or equal-sequence recoveries clean up once and settle to `idle` | `SceneLifecycle.test.ts` (P0.3 suite): Test 4 (releases presentation exactly once across repeated and equal-sequence recoveries) |
| **AC-5** (Push unchanged) | **VERIFIED** | `BattleScene.ts`: `metadata?.source === 'push'` retains standard event-driven hold and timeline animation pipeline | `SceneLifecycle.test.ts` (P0.3 suite): Test 5 (held state awaiting batch), Test 6 (push batch drives full timeline: match → cascade → settle → damage → retaliation) |
| **AC-6** (Rejection rollback) | **VERIFIED** | `BattleScene.ts`: `rollbackToAuthoritativeState()` converges board and timeline readouts onto current authoritative state, clears `committedSwapCells`, and displays rejection reason toast | `SceneLifecycle.test.ts` (P0.3 suite): Test 7 (rollback converges on newest authoritative state; refused pair never presented); `GameRuntime.test.ts`: P0.3 Test 3 |
| **AC-7** (Transport uncertainty) | **VERIFIED** | `GameRuntimeEvents.ts`: `RuntimeActionOutcomeUnknownError`; `GameRuntime.ts`: `markActionOutcomeUnknown` sets `sync: 'unsynchronized'`, calls `recoverBattleState()`, and throws error without asserting "not sent" | `SceneLifecycle.test.ts` (P0.3 suite): Test 8 (unanswered action unresolved, displays 'unconfirmed', reconciled by snapshot); `GameRuntime.test.ts`: P0.3 Tests 1, 2, 4 |
| **AC-8** (Cross-battle safety) | **VERIFIED** | `BattleScene.ts`: `beginSubmission` and `isCurrentSubmission` validate `submissionSerial` and `submissionBattleId`; late responses for superseded battles are ignored | `SceneLifecycle.test.ts` (P0.3 suite): Test 9 (late response for replaced battle cannot settle, report, or lock active scene) |
| **AC-9** (Sync indicator) | **VERIFIED** | `BattleScene.ts`: `syncText` + `renderSyncIndicator()` render non-blocking line in boss band driven by `GameRuntimeState.sync` and connection state | `SceneLifecycle.test.ts` (P0.3 suite): Test 10 (visible while `unsynchronized`, hidden when `synchronized`/`awaiting_battle`, non-interactive) |
| **AC-10** (No leaks) | **VERIFIED** | `BattleScene.ts`: explicit cleanup of listeners, tweens, text objects, and sync indicator during repeated recoveries and scene shutdown | `SceneLifecycle.test.ts` (P0.3 suite): Test 11 (0 leaked tweens, listeners, or Phaser objects across repeated recoveries and shutdown) |
| **AC-11** (Explicit lifecycle) | **VERIFIED** | `BattleScene.ts`: single named, idempotent `invalidatePendingPresentation()` lifecycle handles all lock releases and presentation aborts | Verified in `BattleScene.ts` source and `SceneLifecycle.test.ts` (P0.3 Tests 1, 2, 4, 11) |
| **AC-12** (Verification) | **VERIFIED** | Full frontend build and test suites pass; no pre-existing tracked changes or artifacts overwritten | `npm run test:run` (1206 tests pass), `npm run build` (tsc + vite pass), `git diff --check` clean |

### 2. Historical Exception & Provenance Disclosure (§6.4)

* **Commit Identification:** Commit `425bebb4f6bd1e8019d06bbd92231fb3f9912be3` (`master`).
* **Protocol Deviation:** The canonical two-stage commit protocol (Phase A implementation slice commit and Phase B completion record filing commit per `tasks/TASK_LIFECYCLE.md` §6.1) was NOT followed for commit `425bebb`.
* **Composite Scope:** Commit `425bebb` batched implementation files from multiple tasks (including TASK-272, TASK-275, and backend battle hub updates) along with task manifest drafts in a single git commit.
* **Prescribed Metadata Omission:** The commit body lacked the required `owns:`, `files:`, `shared with:`, and `unowned/pre-existing:` ownership disclosures mandated by §6.2 (P-3, P-4).
* **Ratification Basis:** Operator Decision PO-2 ratified commit `425bebb` as a historical composite commit under `tasks/TASK_LIFECYCLE.md` §6.4. Git history is preserved without rewriting, amending, or resetting.

### 3. Lifecycle Status & Final Filing

* **Final Status:** `DONE` (filed in `tasks/completed/`).
* **Completion Gate Resolution:**
  1. Operator authorized backlog reconciliation and Phase B filing commit.
  2. Under `tasks/TASK_LIFECYCLE.md` §6.1 (Phase B) and §6.4, task record is moved to `tasks/completed/` documenting historical implementation composite commit `425bebb4f6bd1e8019d06bbd92231fb3f9912be3`.
* **Resolution:** All 12 acceptance criteria verified complete; task lifecycle formally closed as `DONE`.
