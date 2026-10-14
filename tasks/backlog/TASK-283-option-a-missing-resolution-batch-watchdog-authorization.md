# TASK-283 — Option A Authorization Record: the Missing Resolution-Batch Watchdog

---

## Metadata

```text
Task ID:           TASK-283 (RE-FILED — read §1 before citing it as history)
Type:              GOVERNANCE / AUTHORIZATION RECORD — the approval artifact TASK-284's
                   prerequisite names
Status:            APPROVED by the Operator (D-1 … D-7); IMPLEMENTED under TASK-284A
Risk:              LOW
Priority:          HIGH
Mode:              Controlled Repository Workflow — authorization record and evidence only
Production code:   NONE CHANGED by this record (it records the approval the implementation acts on)
Decision owner:    Operator (D-1 … D-7, §3)
Scope authority:   tasks/backlog/TASK-284-runtime-recovery-for-missing-accepted-action-event-batches.md
                   §3.3 (constraints), §4 (Option A), §5 (decisions), §6 (§7 amendment draft),
                   §7 (test plan), §8 (documentation and verification plan)
Implemented by:    TASK-284A — src/frontend/client/src/game/runtime/GameRuntime.ts,
                   src/frontend/client/src/game/runtime/GameRuntimeEvents.ts,
                   src/frontend/client/tests/GameRuntime.test.ts,
                   docs/02-technical/SIGNALR_PROTOCOL.md §7 (and its version header)
Related records:   TASK-276, TASK-277, TASK-278, TASK-279, TASK-280, TASK-284, TASK-284A
Recorded:          2026-10-14 — the approval and this record are one act (§1)
Verification:      §4 (the absence evidence) and §5 (what this does and does not authorize)
```

---

## 1. Why this record exists, and what it is not

`TASK-284`'s prerequisite reads: implement **Option A from TASK-283**, but only after
the operator explicitly approves (a) the bounded `GameRuntime` watchdog, (b) the
`SIGNALR_PROTOCOL.md` §7 amendment, and (c) the initial watchdog duration and retry
policy — and produce a readiness report instead if that authorization is not present in
repository evidence.

The readiness report (TASK-284 §1) established that **no TASK-283 artifact existed** in
this repository: not as a file, not in the index, not in any task folder, and not in git
history. Its finding **F-2** recorded that, and its decision item **D-7** asked whether
TASK-283's Option A text should be re-filed so the approval artifact can be cited by
later work.

The operator then:

1. explicitly approved **D-1 … D-7** with the readiness report as the **scope authority**,
   and
2. authorized this implementation slice.

**D-7 is therefore answered "yes", and this file is that re-filing.**

```text
WHAT THIS RECORD IS
  the approval artifact TASK-284's prerequisite names, created on 2026-10-14 from the
  operator's explicit approval of D-1 … D-7, and pointing by section at the approved
  Option A design that already exists in the TASK-284 readiness report.

WHAT THIS RECORD IS NOT
  historical evidence. It is not a reconstruction, quotation, or ratification of any
  earlier TASK-283 document, because none was found (§4). No earlier wording, date,
  author, or acceptance criterion is invented or implied. Nothing here may be cited as
  proof that a pre-2026-10-14 TASK-283 record existed.
```

---

## 2. The approved decision (Option A), as authorized

The approved change is exactly the readiness report's §4 proposal, under its §3.3
constraints:

```text
one bounded, client-local "resolution batch expectation" in GameRuntime — not a second
recovery mechanism:
  armed     by a delivered authoritative state push (§4) that ADVANCES the held
            `sequence`, for a battle the runtime has an action submitted against that
            is not yet settled
  carries   the battle id, the submitted action's identity, and the sequence the
            DELIVERED push carries (never `expectedSequence + 1` — GAME_STATE.md §5.0)
  cleared   by the matching batch's admission, by a recovery snapshot that covers it
            (§7 precedence), by the battle's replacement/clear/end, and on disposal
  expires   publish `sync = 'unsynchronized'` with a technical detail, then invoke the
            EXISTING `recoverBattleState()` (§7 item 1, `MAX_RECOVERY_ATTEMPTS`)
```

---

## 3. D-1 … D-7, as decided

```text
D-1  Authorize Option A at all?
     APPROVED. Implemented as the one bounded expectation above (TASK-284A).

D-2  §7 amendment scope: (i) §7 only, or (ii) §7 + an ADR-008 §Decision amendment?
     APPROVED: (i) §7 only. No ADR-008 edit was made. The §7 amendment adds a third
     desynchronization moment closed by the existing item 1 request, so ADR-008's
     mechanism ("a snapshot, never an event replay") is unchanged and does not need a
     new decision. `SIGNALR_PROTOCOL.md`'s version header records the amendment, as that
     document's own convention requires.

D-3  Approve the initial watchdog duration.
     APPROVED: 2,500 ms — configuration, not protocol. Implemented as
     `DEFAULT_RESOLUTION_WATCHDOG_MS = 2500` with a construction-time override
     (`GameRuntimeOptions.resolutionWatchdogMs`).

D-4  Approve the retry policy.
     APPROVED: R1 — the expectation fires at most once per observation and delegates
     every retry to the existing bounded recovery (`MAX_RECOVERY_ATTEMPTS = 3`, the
     existing 25/50/100 ms backoff). No second timer family, backoff loop, or attempt
     counter was added.

D-5  Approve the exclusion of the documented no-broadcast acceptance
     (SIGNALR_PROTOCOL.md §3.1 item 6; ADR-025 §2).
     APPROVED, and implemented structurally rather than by a special case: only a
     delivered push that ADVANCES the held `sequence` arms anything, and an idempotent
     retry re-commits and re-broadcasts nothing, so it produces no advance and cannot
     arm. A delivered push that does not advance (a duplicate) arms nothing either. Both
     are pinned by tests.

D-6  Confirm that NO BattleScene or GameRuntimePort change is in scope.
     CONFIRMED. No scene file changed and the port's member set is unchanged. Expiry
     releases no input lock; the lock is released where it is released today — by the
     delivered state, including the `'recovery'` snapshot the expiry requests.

D-7  Confirm whether TASK-283's Option A text should be re-filed first, so the approval
     artifact can be cited.
     CONFIRMED "yes". This record is that re-filing (§1).
```

---

## 4. Absence evidence (why a re-filing was needed)

Read-only sweeps run against the working tree at baseline
(`HEAD = 425bebb`, branch `master`):

```text
1  Filename sweep     Get-ChildItem -Recurse -File -Filter "*TASK-283*"   → no results
2  Content sweep      git grep -n "TASK-283" -- .                          → no matches
                                                                             (exit 1)
3  History sweep      git log --all -S "TASK-283"                          → no commits
4  History sweep      git log --all --diff-filter=A --name-only | grep TASK-283
                                                                           → no adds
5  Task folders       tasks/backlog, tasks/active, tasks/blocked, tasks/completed
                      → no TASK-283 file in any of them
```

The only repository text that named TASK-283 before this record was TASK-284's own
readiness report (its prerequisite and finding F-2) — i.e. the reference, not the
artifact. That is why §1 marks this file as created, not recovered.

---

## 5. What this authorizes, and what it still does not

```text
AUTHORIZED
  · the Option A expectation in GameRuntime, exactly as §2 defines it
  · the SIGNALR_PROTOCOL.md §7 amendment (and its version-header note)
  · 2,500 ms initial duration, configurable at construction
  · R1 retry policy (fire once; the existing bounded recovery retries)
  · the exclusion of the documented no-broadcast acceptance
  · no scene change and no new port member
  · deterministic fake-timer tests for the nine required scenarios (TASK-284 §7)

NOT AUTHORIZED — unchanged by this record
  · any new hub method, argument, subscription, message, or state-push member
    (SIGNALR_PROTOCOL.md §8 items 3–9)
  · an event log, replay channel, or retransmission request (§8 item 8)
  · any BattleScene presentation-admission change, or a port member for recovery
  · timeout-based input unlocking, scene/port changes, or a new recovery protocol
  · any ADR amendment (D-2 chose §7 only)
  · any further game rule, mechanic, content, or scope beyond the readiness report
```

If a later slice needs any of the second list, that is a new decision — not a reading of
this one.
