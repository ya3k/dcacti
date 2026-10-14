# TASK-274 — Authoritative Sequence Desync Supersession & Replay Prevention

> [!IMPORTANT]
> **RECONSTRUCTED HISTORICAL RECORD — NOT AN ORIGINAL RECOVERED TASK FILE**
>
> 1. **Provenance:** The original TASK-274 task manifest was **not found** in repository git history (confirmed by filename, content, and git log sweeps recorded in `tasks/backlog/TASK-284-runtime-recovery-for-missing-accepted-action-event-batches.md` §1.2 and finding F-1).
> 2. **Authorization:** This record is retrospectively authored under **Product Owner / Operator Authorization PO-4**, following repository precedent established under PO Decision 2 = A (`tasks/completed/TASK-209-in-battle-state-projection-and-hud.md`, `tasks/completed/TASK-224-commit-step-and-task-209-record.md`).
> 3. **Source-of-truth grounding:** Every factual statement in this record is derived strictly from explicit repository evidence: dedicated test suites in `src/frontend/client/tests/GameRuntime.test.ts` (lines 4670–4985: `TASK-274 — final recovery boundary & runtime invariant verification`), `src/frontend/client/tests/SceneLifecycle.test.ts` (lines 12505–12652: `TASK-274 — final recovery boundary review & scene admission gate verification`), explicit code comments in `src/frontend/client/src/game/scenes/BattleScene.ts` (lines 4155, 4179: citing `TASK-274 Review A`), and downstream task dependencies (`tasks/blocked/TASK-275-authoritative-turn-lifecycle-and-match3-animation-pipeline.md`:19, `tasks/backlog/TASK-276-authoritative-cascade-snapshot-protocol-planning.md`:20, `tasks/backlog/TASK-277-cascade-protocol-decision-and-compatibility-gate.md`:15, `tasks/backlog/TASK-278-cascade-protocol-authorization-gate.md`:16).
> 4. **Independence from TASK-273:** This record details the distinct hardening, review, and invariant verification scope identified as Review A, Review B, and Review C. It does not duplicate TASK-273's protocol definition claims without separate evidence.
> 5. **Commit attribution constraint:** Commit `425bebb` contains the committed test and implementation code, but carries no `owns: TASK-274` attribution, bundles 19 files out of band, and is **not** a compliant Phase A implementation slice under `tasks/TASK_LIFECYCLE.md` §6.2 (P-2/P-3/P-5). No historical Phase A commit SHA or author is fabricated.
> 6. **Evidence classification:** Evidenced behaviors, inferred items, and unverified elements are explicitly labelled throughout this document.

---

## Metadata

```text
Task ID:           TASK-274
Type:              REFACTOR / VERIFICATION / QUALITY-GATE (derived from explicit test suite
                   structure: "final recovery boundary review & scene admission gate verification";
                   NOT RECORDED in original manifest)
Status:            DONE (Status justification below; see Lifecycle Note)
Risk:              NOT RECORDED (no original artifact survives)
Priority:          HIGH / CRITICAL (evidenced by downstream gate dependencies in TASK-275,
                   TASK-276, TASK-277, and TASK-278)
Primary Agent:     NOT RECORDED / UNVERIFIED
Supporting Agents: NOT RECORDED / UNVERIFIED
Workflow:          NOT RECORDED / UNVERIFIED
Skills:            NOT RECORDED / UNVERIFIED
Dependencies:      TASK-273 (evidenced by review structure evaluating TASK-273 boundaries)
Declared Files:    NOT RECORDED historically. Candidate scope reconstructed from code,
                   tests, and documentation citing TASK-274:
                   - src/frontend/client/src/game/runtime/GameRuntime.ts
                   - src/frontend/client/src/game/scenes/BattleScene.ts
                   - src/frontend/client/tests/GameRuntime.test.ts
                   - src/frontend/client/tests/SceneLifecycle.test.ts
Evidence Status:   PARTIALLY PROVEN (Review A, B, and C test suites and BattleScene code citations
                   are proven in the tree; original task manifest, author, and Phase A commit
                   ownership are unverified)
Lifecycle Note:    Retrospectively reconstructed under PO-4. Tasks/TASK_LIFECYCLE.md §3 two-stage
                   commit protocol was not executed by an agent for this task file.
```

**Status justification.** `DONE` is a repository-defined `tasks/TASK_LIFECYCLE.md` §3 value and is assigned here strictly because:
1. Downstream task manifests explicitly cite `TASK-274` as a satisfied dependency: `tasks/blocked/TASK-275-authoritative-turn-lifecycle-and-match3-animation-pipeline.md`:19 (`TASK-274 (both preserved)`), `tasks/backlog/TASK-276-authoritative-cascade-snapshot-protocol-planning.md`:20 (`TASK-274 (DONE)`), `tasks/backlog/TASK-277-cascade-protocol-decision-and-compatibility-gate.md`:15 (`TASK-274 (DONE)`), and `tasks/backlog/TASK-278-cascade-protocol-authorization-gate.md`:16 (`TASK-274 (DONE)`).
2. The complete test suites dedicated to TASK-274 (`GameRuntime.test.ts`:4670–4985 and `SceneLifecycle.test.ts`:12505–12652) are committed, passing, and guard the recovery boundaries.
3. This record does not assert that a normal `IN REVIEW → DONE` transition occurred via the two-stage commit protocol; it records the ratified status on evidence.

---

## Objective

Execute a rigorous verification, hardening, and review pass over the client battle recovery boundaries and runtime invariants established under TASK-273, resolving subtle edge cases across `GameRuntime` and `BattleScene`. Specifically, verify and guard three distinct review domains (Review A, Review B, and Review C) to ensure that stale, cross-battle, malformed, or unheld events cannot corrupt active battle state, cause presentation deadlocks, or resurrect ended battles:

1. **Review A — Unheld, Stale & Cross-Battle Batch Rejection:**
   - Drop batches arriving before any authoritative state has been held (`held === null`) in `GameRuntime` and refuse them in `BattleScene` before reaching presentation listeners.
   - Drop batches for battle A when battle B is active, preventing cross-battle pollution.
   - Ensure a legitimate state push followed by a matching batch flows through normally.
   - Drop late batches arriving after a battle has ended (`source: 'ended'`), preventing resurrection of ended battles.
   - Drop late batches from a previous battle after a new battle has started without disrupting the new battle.
2. **Review B — Recovery Envelope Agreement & In-Flight State Hardening:**
   - On receiving an inconsistent recovery envelope (`envelope.serverSequence !== state.sequence`), reject the snapshot, retain `unsynchronized` status, clear the in-flight recovery flag, and verify no infinite automatic retry loop occurs.
   - Verify that synchronization is restored cleanly when a subsequent valid recovery response arrives.
   - Reject malformed recovery responses for an old battle and prevent them from mutating the active battle.
   - In `BattleScene`, safely retain presentation locks (`inputLocked`, `resolutionPending`) during failed recovery attempts until valid recovery convergence arrives, avoiding race conditions and indefinite deadlocks.
3. **Review C — Battle Lifecycle Convergence & Terminal Navigation:**
   - Ensure neither a late state push nor a late batch can resurrect an ended battle (`BATTLE_NOT_FOUND`).
   - Ensure delayed `getBattleResult` responses for an old battle cannot overwrite or displace the active battle.
   - Clear board cells and release locks on `BATTLE_NOT_FOUND` without fabricating unearned results (`ResultScene` is not started for expired battles).
   - Navigate to `ResultScene` strictly upon receiving an authoritative outcome event (`BattleWon` / `BattleLost`) within an admitted batch.

---

## Authoritative References

- `docs/02-technical/SIGNALR_PROTOCOL.md`:
  - §3.1 — Documented push-then-batch order
  - §6 items 3–4 — State sequence comparison and delivery ordering guarantees
  - §7.1 — `GetBattleState` snapshot contract
  - §7.3 — `BATTLE_NOT_FOUND` handling and result fallback
  - §7 item 5 — Envelope sequence agreement invariant
  - §8.3 — No battle status message; outcome determined strictly by authoritative events
- `docs/02-technical/GAME_STATE.md`:
  - §5.0 — Client's use of Sequence on delivery; handling of `no state held at all`
  - §5.1 item 3 — Authoritative state represents complete resolution result
- `docs/02-technical/ARCHITECTURE.md`:
  - §2.2.1 rules 1, 3, 4 — Separation of runtime delivery integrity and scene presentation admission
- `docs/03-decisions/ADR/ADR-008-battle-recovery.md` — Snapshot-based battle reconnection and anti-replay principles
- `src/frontend/client/src/game/scenes/BattleScene.ts`:
  - Line 4155: Explicit citation `no authoritative state held -> refuse no state to represent resolution (TASK-274 Review A)`
  - Line 4179: Explicit citation `unheld batches cannot reach presentation (TASK-274 Review A)`
- `src/frontend/client/tests/GameRuntime.test.ts`:
  - Lines 4670–4985: `TASK-274 — final recovery boundary & runtime invariant verification`
- `src/frontend/client/tests/SceneLifecycle.test.ts`:
  - Lines 12505–12652: `TASK-274 — final recovery boundary review & scene admission gate verification`

---

## Scope & Provenance Classification

### Evidenced Scope (Directly backed by committed code and tests)
- **Review A (Boundary Invariants):**
  - Verified in `GameRuntime.test.ts` (Review A.1–A.5):
    - A.1: Drops batch when no state held; emits `runtime_error` ("while no battle is synchronized").
    - A.2: Drops batch for battle A when battle B is synchronized ("while 'battle-B' is synchronized").
    - A.3: Push followed by matching batch passes through.
    - A.4: Drops late batch after battle-ended publication.
    - A.5: Drops late batch from prior battle after new battle started.
  - Verified in `SceneLifecycle.test.ts` (Review A.1, A.4, A.5):
    - A.1: `BattleScene.admitsBatch` returns `false` when `currentBattleState === null`; zero phases played.
    - A.4: Late batch after `source: 'ended'` cannot restart presentation or re-lock scene.
    - A.5: Late batch from old battle cannot affect newly active battle on scene.
  - Implemented in `BattleScene.ts`: Lines 4155 and 4179 cite TASK-274 Review A.
- **Review B (Recovery Hardening):**
  - Verified in `GameRuntime.test.ts` (Review B.1–B.7):
    - B.1–B.5: Mismatched recovery envelope (`serverSequence !== state.sequence`) does not mutate state, leaves `sync: 'unsynchronized'`, clears `recoveryInProgress` flag, and triggers no infinite retry loop.
    - B.6: Restores `synchronized` when a subsequent valid response arrives.
    - B.7: Rejects malformed recovery for old battle A while battle B is active.
  - Verified in `SceneLifecycle.test.ts` (Review B.8):
    - B.8: Presentation lock safely retained during malformed recovery; unlocked cleanly upon valid recovery snapshot.
- **Review C (Lifecycle Edge Cases & Teardown):**
  - Verified in `GameRuntime.test.ts` (Review C.2, C.3):
    - C.2: Late push or batch cannot resurrect ended battle.
    - C.3: Delayed `getBattleResult` promise cannot overwrite active new battle.
  - Verified in `SceneLifecycle.test.ts` (Review C.1, C.4, C.5):
    - C.1 & C.5: `BATTLE_NOT_FOUND` clears board cells and locks, shows fallback message, fabricates no `ResultScene`.
    - C.4: Authoritative outcome event (`BattleWon`) in an admitted batch navigates to `ResultScene`.

### Inferred Scope (Reasonably deduced from context but lacking explicit historical manifest)
- Original task categorization as `REFACTOR` or `VERIFICATION`.
- Agent workflow mapping.

### Unverified Scope (Not found in repository evidence)
- Original author, creation timestamp, or branch name.
- Standalone Phase A implementation slice commit SHA.
- Original text of task file.

---

## Reconstructed Acceptance Criteria (Derived from Verified Test Suites)

- [x] **AC-1 (Review A — Unheld Batch Rejection):** Batches arriving when `GameRuntime` holds no battle state are dropped, log a descriptive error, and never reach listeners. `BattleScene` refuses unheld batches and builds zero timelines.
- [x] **AC-2 (Review A — Cross-Battle Protection):** Batches whose `battleId` does not match the active battle are dropped without altering active battle state, sequence, or synchronization.
- [x] **AC-3 (Review A — Post-Termination Protection):** Late batches arriving after a battle has been marked ended (`source: 'ended'`) are dropped and cannot re-lock the scene or restart animations.
- [x] **AC-4 (Review B — Envelope Mismatch Handling):** A `GetBattleState` response with conflicting sequence numbers (`serverSequence !== state.sequence`) is rejected, leaves held state unchanged, keeps runtime `unsynchronized`, and leaves in-flight locks clear for future retries.
- [x] **AC-5 (Review B — Recovery Convergence):** Following a failed recovery attempt, the arrival of a valid, consistent recovery snapshot immediately restores synchronization and converges the scene.
- [x] **AC-6 (Review B — Lock Retention During Recovery):** When an action is pending and recovery fails, `BattleScene` safely holds its presentation lock until a valid snapshot arrives, avoiding mid-resolution input leakage.
- [x] **AC-7 (Review C — Anti-Resurrection):** Late pushes or batches for an ended battle cannot resurrect it or overwrite a newly active battle.
- [x] **AC-8 (Review C — Teardown & Navigation Fidelity):** When `BATTLE_NOT_FOUND` occurs, all board cells are cleared and locks released without starting `ResultScene`. `ResultScene` is started only when an admitted batch contains an authoritative `BattleWon` or `BattleLost` event.

---

## Completion & Commit Evidence

### Implementation Commit (Phase A)
- **Status:** `NONE / UNATTRIBUTED`
- **Explanation:** In Git history, the code and test suites associated with TASK-274 first appear in bulk commit `425bebb4f6bd1e8019d06bbd92231fb3f9912be3` ("feat: implement authoritative turn lifecycle, battle recovery, and match-3 animation pipeline"). That commit spans 19 files, carries no `owns: TASK-274` commit body attribution, and violates `tasks/TASK_LIFECYCLE.md` §6.2 (P-2/P-3/P-4/P-5). In accordance with operator instructions and repository policy, `425bebb` is **not** claimed as a valid Phase A commit.

### Verification Results
- `src/frontend/client/tests/GameRuntime.test.ts`:
  - `TASK-274 — final recovery boundary & runtime invariant verification`:
    - `Review A.1: drops a batch for a battle with no held state before reaching listeners` — PASS
    - `Review A.2: drops a batch for battle A when battle B is active` — PASS
    - `Review A.3: allows state push followed by matching batch to work normally through runtime` — PASS
    - `Review A.4: drops late batch arriving after battle-ended publication and cannot restart old presentation` — PASS
    - `Review A.5: drops late batch from previous battle after new battle is active without affecting new battle` — PASS
    - `Review B.1-B.5: rejects mismatched recovery envelope, remains unsynchronized, clears in-flight state without infinite retry loop` — PASS
    - `Review B.6: restores synchronization when a subsequent valid recovery response arrives` — PASS
    - `Review B.7: rejects malformed recovery response for old battle A and prevents it from mutating active battle B` — PASS
    - `Review C.2: late state push or batch cannot resurrect an ended battle` — PASS
    - `Review C.3: delayed getBattleResult response for old battle cannot replace active battle` — PASS
- `src/frontend/client/tests/SceneLifecycle.test.ts`:
  - `TASK-274 — final recovery boundary review & scene admission gate verification`:
    - `Review A.1: refuses an unversioned batch when BattleScene holds no authoritative state` — PASS
    - `Review A.4: late batch after battle-ended publication cannot restart presentation or re-lock scene` — PASS
    - `Review A.5: late batch from old battle cannot affect newly active battle on scene` — PASS
    - `Review B.8: presentation lock is safely retained until valid recovery convergence without indefinite deadlock` — PASS
    - `Review C.1 & C.5: BATTLE_NOT_FOUND clears board and locks, fabricates no result, and provides stable fallback` — PASS
    - `Review C.4: authoritative outcome event in admitted batch navigates to ResultScene` — PASS
- Zero client-side authoritative calculations introduced (`AGENTS.md` §10).
