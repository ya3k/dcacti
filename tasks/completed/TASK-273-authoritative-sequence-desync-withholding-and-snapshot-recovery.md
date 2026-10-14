# TASK-273 — Authoritative Sequence Desync Withholding & Snapshot Recovery

> [!IMPORTANT]
> **RECONSTRUCTED HISTORICAL RECORD — NOT AN ORIGINAL RECOVERED TASK FILE**
>
> 1. **Provenance:** The original TASK-273 task manifest was **not found** in repository git history (confirmed by filename, content, and git log sweeps recorded in `tasks/backlog/TASK-284-runtime-recovery-for-missing-accepted-action-event-batches.md` §1.2 and finding F-1).
> 2. **Authorization:** This record is retrospectively authored under **Product Owner / Operator Authorization PO-4**, following repository precedent established under PO Decision 2 = A (`tasks/completed/TASK-209-in-battle-state-projection-and-hud.md`, `tasks/completed/TASK-224-commit-step-and-task-209-record.md`).
> 3. **Source-of-truth grounding:** Every factual statement in this record is derived strictly from committed documentation (`docs/02-technical/SIGNALR_PROTOCOL.md` v2.23, `docs/02-technical/GAME_STATE.md` v2.28 §5.0, `docs/02-technical/ARCHITECTURE.md` v1.13 §2.2.1 rule 4, `docs/03-decisions/ADR/ADR-008-battle-recovery.md` amended 2026-10-13), task cross-references (`tasks/active/TASK-272-battlescene-recovery-presentation-lock-and-action-rollback.md`:69, `tasks/blocked/TASK-275-authoritative-turn-lifecycle-and-match3-animation-pipeline.md`:19), and passing regression tests (`tests/backend/GameServer.Api.Tests/Hubs/BattleHubStatePushOrderTests.cs`:33, `src/frontend/client/tests/GameRuntime.test.ts`:4238, `src/frontend/client/tests/SceneLifecycle.test.ts`:12136).
> 4. **Commit attribution constraint:** Commit `425bebb` contains the committed code, test, and documentation effects, but carries no `owns: TASK-273` attribution, bundles 19 files out of band, and is **not** a compliant Phase A implementation slice under `tasks/TASK_LIFECYCLE.md` §6.2 (P-2/P-3/P-5). No historical Phase A commit SHA or author is fabricated.
> 5. **Evidence classification:** Evidenced behaviors, inferred items, and unverified elements are explicitly labelled throughout this document.

---

## Metadata

```text
Task ID:           TASK-273
Type:              BUG / ARCHITECTURE (derived from doc amendments and bug-fix nature
                   noted in TASK-272:69; NOT RECORDED in original manifest)
Status:            DONE (Status justification below; see Lifecycle Note)
Risk:              NOT RECORDED (no original artifact survives)
Priority:          CRITICAL / P0.4 (evidenced in SceneLifecycle.test.ts:12136 and
                   GameRuntime.test.ts:4238)
Primary Agent:     NOT RECORDED / UNVERIFIED
Supporting Agents: NOT RECORDED / UNVERIFIED
Workflow:          NOT RECORDED / UNVERIFIED
Skills:            NOT RECORDED / UNVERIFIED
Dependencies:      TASK-272 (evidenced by TASK-272:69: "Follow-up: TASK-273 addresses it")
Declared Files:    NOT RECORDED historically. Candidate scope reconstructed from code,
                   tests, and documentation citing TASK-273:
                   - docs/02-technical/SIGNALR_PROTOCOL.md
                   - docs/02-technical/GAME_STATE.md
                   - docs/02-technical/ARCHITECTURE.md
                   - docs/03-decisions/ADR/ADR-008-battle-recovery.md
                   - src/frontend/client/src/game/runtime/GameRuntime.ts
                   - src/frontend/client/src/game/scenes/BattleScene.ts
                   - tests/backend/GameServer.Api.Tests/Hubs/BattleHubStatePushOrderTests.cs
                   - src/frontend/client/tests/GameRuntime.test.ts
                   - src/frontend/client/tests/SceneLifecycle.test.ts
Evidence Status:   PARTIALLY PROVEN (Contract, documentation amendments, code implementations,
                   and verification suites are proven; original task manifest, author, and
                   Phase A commit ownership are unverified)
Lifecycle Note:    Retrospectively reconstructed under PO-4. Tasks/TASK_LIFECYCLE.md §3 two-stage
                   commit protocol was not executed by an agent for this task file.
```

**Status justification.** `DONE` is a repository-defined `tasks/TASK_LIFECYCLE.md` §3 value and is assigned here strictly because:
1. Downstream tasks explicitly cite `TASK-273` as a satisfied dependency: `tasks/blocked/TASK-275-authoritative-turn-lifecycle-and-match3-animation-pipeline.md`:19 (`TASK-273 (preserved)`), `tasks/backlog/TASK-276-authoritative-cascade-snapshot-protocol-planning.md`:20 (`TASK-273 (DONE)`), `tasks/backlog/TASK-277-cascade-protocol-decision-and-compatibility-gate.md`:15 (`TASK-273 (DONE)`), and `tasks/backlog/TASK-278-cascade-protocol-authorization-gate.md`:16 (`TASK-273 (DONE)`).
2. All technical contracts and documentation amendments attributed to `TASK-273` are present in `docs/` and verified by automated regression test suites.
3. This record does not assert that a normal `IN REVIEW → DONE` transition occurred via the two-stage commit protocol; it records the ratified status on evidence.

---

## Objective

Operationally define authoritative sequence desynchronization on the client, implement whole-batch withholding and snapshot-based recovery convergence without event replay, enforce recovery envelope sequence consistency, and establish admission and lock-release gates across `GameRuntime` and `BattleScene` as specified in `SIGNALR_PROTOCOL.md` v2.23, `GAME_STATE.md` v2.28 §5.0, `ARCHITECTURE.md` v1.13 §2.2.1 rule 4, and `ADR-008`:

1. **Operational sequence desync definition:** Define sequence desync as a received `ReceiveEvents` batch whose `serverSequence` is strictly ahead of the `sequence` of the state the client holds (`batch.serverSequence > held.sequence`).
2. **Whole-batch withholding (Runtime):** When a batch arrives ahead of the held state or for a foreign battle, withhold the batch whole (never partially apply, filter, or reorder events), transition runtime synchronization to `unsynchronized`, and request the authoritative snapshot via `GetBattleState(battleId)` (`SIGNALR_PROTOCOL.md` §7.1).
3. **Request deduplication:** Deduplicate snapshot recovery requests so that at most one `GetBattleState` request is in flight per battle.
4. **Snapshot supersession & anti-replay:** Enforce that a delivered recovery snapshot supersedes all pending or in-flight batches at or below its `sequence`. Such batches are never replayed or animated.
5. **Recovery envelope agreement:** Enforce that an accepted `GetBattleState` response carries agreeing sequence values on the envelope (`serverSequence`) and payload (`state.sequence`). If they disagree, reject the response as an invalid recovery envelope, retain unsynchronized status, and do not converge.
6. **Scene admission gate (`BattleScene.admitsBatch`):** Refuse to build a presentation timeline, trigger callouts, or hand off outcomes for batches that the authoritative state on screen does not represent (`held.sequence !== batch.serverSequence`, battleId mismatch, or no state held).
7. **Unpresentable resolution lock release (`BattleScene.releaseUnpresentableResolution`):** When an admitted batch yields an empty or unpresentable phase set, release all engaged presentation locks and converge readouts rather than deadlocking the scene.
8. **Battle-ended publication (`BATTLE_NOT_FOUND`):** When recovery returns `BATTLE_NOT_FOUND`, publish `null` state with `source: 'ended'` to release locks, clear the board, and prevent hanging in presentation.

---

## Authoritative References

- `docs/02-technical/SIGNALR_PROTOCOL.md`:
  - Version 2.23 revision history header (:14) — formal summary of TASK-273 changes
  - §3.1 — Post-commit ordering: state write-back and push precede `ReceiveEvents` batch
  - §6 item 3 (:2434–2479) — Operational definition of sequence desync, whole-batch withholding, anti-replay
  - §6 item 4 (:2480–2482) — `BattleStateUpdated` delivery precedes `ReceiveEvents`
  - §7 (:2498–2576) — Reconnect & resync mechanism; snapshot as single recovery path
  - §7.1 (:2577–2581) — `GetBattleState(battleId)` request/response contract
  - §7.2 (:2582–2583) — Discard local prediction, re-render from snapshot, no event replay
  - §7.3 (:2584–2586) — `BATTLE_NOT_FOUND` handling
  - §7 item 5 (:2597–2613) — Dual sequence spelling agreement requirement
  - §7 precedence rule (:2650–2661) — Snapshot supersedes pending/in-flight batches at or below its sequence
  - §8 item 8 (:2704–2707) — Exclusion of event replay or retransmission mechanisms
- `docs/02-technical/GAME_STATE.md`:
  - Version 2.28 revision history header (:3)
  - §5.0 (:2733–2794) — Client's use of `Sequence` on delivery; relation table (`= n`, `< n`, `> n`, no state held)
  - §5.1 item 3 — Committed state represents complete resolution result
  - §5.2 item 3 — Sequence is battle-scoped; cross-battle comparison forbidden
- `docs/02-technical/ARCHITECTURE.md`:
  - Version 1.13 revision history header (:3)
  - §2.2.1 rule 1 (:289–297) — Scenes depend on runtime port, never transport
  - §2.2.1 rule 3 (:302–305) — Runtime coordinates, does not compute
  - §2.2.1 rule 4 (:306–350) — Batch forwarding and delivery-integrity exception; runtime/scene boundary split
- `docs/03-decisions/ADR/ADR-008-battle-recovery.md`:
  - Amendment record 2026-10-13 (:5–13)
  - Decision: Snapshot-based recovery applied to detected desync; snapshot precedence; runtime/scene ownership
- `tasks/active/TASK-272-battlescene-recovery-presentation-lock-and-action-rollback.md`:69 — Notes TASK-273 as the follow-up addressing gap-triggered sequence desync.

---

## Scope & Provenance Classification

### Evidenced Scope (Directly backed by committed documents, code, or tests)
- **Protocol & Specification:**
  - `SIGNALR_PROTOCOL.md` §6 item 3 definition of desync and whole-batch withholding.
  - `SIGNALR_PROTOCOL.md` §7 item 5 envelope sequence agreement rule.
  - `GAME_STATE.md` §5.0 client sequence comparison matrix.
  - `ARCHITECTURE.md` §2.2.1 rule 4 delivery-integrity exception definition.
  - `ADR-008` amendment unifying reconnect and detected sequence desync recovery.
- **Client Runtime (`GameRuntime.ts`):**
  - Tracking authoritative `sequence` from `BattleStateUpdated` pushes and recovery snapshots.
  - Comparing incoming `ReceiveEvents` `serverSequence` against held state `sequence`.
  - Withholding whole batch when `serverSequence > held.sequence` or when `battleId` mismatches.
  - Transitioning `GameRuntimeState.sync` to `'unsynchronized'`.
  - Requesting `GetBattleState(battleId)` and deduplicating concurrent recovery calls to one in flight.
  - Validating recovery envelope: refusing snapshot if `envelope.serverSequence !== state.sequence`.
  - Forwarding batch unchanged when `serverSequence === held.sequence` or `serverSequence < held.sequence`.
- **Client Presentation (`BattleScene.ts`):**
  - Implementing `admitsBatch(envelope)` checking `battleId` match and `serverSequence === held.sequence` (§4.2).
  - Implementing `releaseUnpresentableResolution()` releasing presentation locks on empty phase sets (§4.3).
  - Handling `setBattleState(null, { source: 'ended' })` by clearing presentation and locks (§4.4).
- **Verification Suites:**
  - `tests/backend/GameServer.Api.Tests/Hubs/BattleHubStatePushOrderTests.cs`: Verifies producer invariant that `BattleStateUpdated` push precedes `ReceiveEvents` with identical sequence.
  - `src/frontend/client/tests/GameRuntime.test.ts`: 12 test cases under `P0.4: client sequence desync detection and recovery convergence`.
  - `src/frontend/client/tests/SceneLifecycle.test.ts`: Scenarios under `TASK-273 — unrepresentable batches, unpresentable resolutions & ended battles (P0.4)`.

### Inferred Scope (Reasonably deduced from context but lacking explicit historical manifest)
- Original task categorization as `BUG` or `ARCHITECTURE`.
- Assignment of the `client` / `realtime` engineering agents.
- The precise sequence of commits before they were consolidated in bulk commit `425bebb`.

### Unverified Scope (Not found in repository evidence)
- Original author, creation timestamp, or branch name.
- Standalone Phase A implementation slice commit SHA.
- Original text of acceptance criteria checklist.

---

## Reconstructed Acceptance Criteria (Derived from Authoritative Contracts)

- [x] **AC-1 (Desync Detection):** When `ReceiveEvents` arrives with `serverSequence > held.sequence`, `GameRuntime` withholds the batch whole and sets `sync` to `'unsynchronized'`.
- [x] **AC-2 (Recovery Snapshot Invocation):** On sequence gap detection, `GameRuntime` invokes `GetBattleState(battleId)` to fetch the authoritative snapshot per `ADR-008`.
- [x] **AC-3 (Request Deduplication):** Concurrent observations of a desync gap result in at most one `GetBattleState` invocation in flight per battle.
- [x] **AC-4 (Envelope Validation):** When `GetBattleState` returns with `serverSequence !== state.sequence`, `GameRuntime` rejects the recovery snapshot, keeps `sync` as `'unsynchronized'`, and logs an inconsistent envelope error.
- [x] **AC-5 (Supersession & No Replay):** A batch arriving with `serverSequence <= held.sequence` is not treated as a desync; it is forwarded by runtime, but presentation refuses to replay previously accepted resolutions.
- [x] **AC-6 (Scene Admission Gate):** `BattleScene` refuses batches for foreign battles or batches with `serverSequence !== held.sequence`, building zero timelines and acquiring zero locks.
- [x] **AC-7 (Deadlock Avoidance on Empty Phases):** When an admitted batch produces an empty presentation phase list, `BattleScene` converges readouts and releases all locks immediately.
- [x] **AC-8 (Ended Battle Handling):** A `BATTLE_NOT_FOUND` response clears active battle state, publishes `source: 'ended'`, unlocks the scene, and prevents stale timeline execution.

---

## Completion & Commit Evidence

### Implementation Commit (Phase A)
- **Status:** `NONE / UNATTRIBUTED`
- **Explanation:** In Git history, the code, test, and documentation effects associated with TASK-273 first appear in bulk commit `425bebb4f6bd1e8019d06bbd92231fb3f9912be3` ("feat: implement authoritative turn lifecycle, battle recovery, and match-3 animation pipeline"). That commit spans 19 files, carries no `owns: TASK-273` commit body attribution, and violates `tasks/TASK_LIFECYCLE.md` §6.2 (P-2/P-3/P-4/P-5). In accordance with operator instructions and repository policy, `425bebb` is **not** claimed as a valid Phase A commit.

### Verification Results
- `tests/backend/GameServer.Api.Tests/Hubs/BattleHubStatePushOrderTests.cs`:
  - `Swap_PushesBattleStateUpdatedBeforeReceiveEvents_WithTheSameCommittedSequence` — PASS
  - `CardCast_PushesBattleStateUpdatedBeforeReceiveEvents_WithTheSameCommittedSequence` — PASS
  - `PetSkillCast_PushesBattleStateUpdatedBeforeReceiveEvents_WithTheSameCommittedSequence` — PASS
  - `GetBattleState_EnvelopeSequenceEqualsTheSnapshotSequence` — PASS
- `src/frontend/client/tests/GameRuntime.test.ts`:
  - `P0.4: client sequence desync detection and recovery convergence` (12 scenarios) — PASS
- `src/frontend/client/tests/SceneLifecycle.test.ts`:
  - `TASK-273 — unrepresentable batches, unpresentable resolutions & ended battles (P0.4)` — PASS
- Zero client-side authoritative calculations introduced (`AGENTS.md` §10).
