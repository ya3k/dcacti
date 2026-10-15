# TASK-284 — Runtime Recovery for Missing Accepted-Action Event Batches — Readiness Report

> **Post-authorization status (2026-10-14).** The operator has since explicitly approved
> **D-1 … D-7** and authorized implementation, with this report as the scope authority.
> The approval artifact is
> `tasks/backlog/TASK-283-option-a-missing-resolution-batch-watchdog-authorization.md`
> (re-filed per D-7: no earlier TASK-283 artifact was found in the repository — §1.2,
> finding F-2) and the implementation is recorded in
> `tasks/backlog/TASK-284A-missing-resolution-batch-watchdog-implementation.md`
> (2,500 ms initial duration per D-3, R1 retry policy per D-4, §7 only per D-2, no
> scene/port change per D-6). **§0–§11 below are preserved unchanged as the
> pre-authorization assessment; they are history, not current status.** §4–§7 are the
> approved proposal that was implemented; §0's "STOP — do not implement" is superseded by
> the authorization, not deleted.

---

## Metadata

```text
Task ID:           TASK-284
Type:              ARCHITECTURE / GOVERNANCE & DECISION PREPARATION (readiness gate)
Status:            SUPERSEDED
Superseded by:     TASK-283 (Option A authorized by Operator) and TASK-284A
                   (Missing resolution-batch watchdog implemented under commit e33254e)
Verdict:           SUPERSEDED — proposal Option A was formally authorized under TASK-283
                   and implemented under TASK-284A. Zero actionable independent scope remains.
Risk:              MEDIUM (the underlying gap is real; the blocked work is unstarted)
Priority:          HIGH
Mode:              Controlled Repository Workflow — readiness assessment and STOP only
Production code:   NONE CHANGED (no source, test, docs/, or ADR edit; this record only)
Target (if authorized):
                   src/frontend/client/src/game/runtime/GameRuntime.ts (+ its tests, + the
                   SIGNALR_PROTOCOL.md §7 amendment)
Contract Refs:     docs/02-technical/SIGNALR_PROTOCOL.md §3, §3.1 (item 6), §4, §5 item 2,
                   §6 items 3–5, §7, §8; docs/02-technical/GAME_STATE.md §5.0, §5.1;
                   docs/02-technical/ARCHITECTURE.md §2.2.1 (rules 1, 3, 4, 5);
                   docs/03-decisions/ADR/ADR-008; docs/03-decisions/ADR/ADR-025
Dependencies:      TASK-283 (APPROVED), TASK-284A (DONE), TASK-280 (CLOSED)
Declared Files:    tasks/completed/TASK-284-runtime-recovery-for-missing-accepted-action-event-batches.md
Verification:      §11 — focused GameRuntime suite 192/192, `git diff --check` clean,
                   no implementation run attempted (nothing to run)
```

> **How to read this record.** §0 is the verdict. §1 is the gate the task itself set and the
> evidence that it is unsatisfied. §2 is the canonical readiness assessment. §3 characterises
> the current implementation and the exact gap. **§4–§8 are a design *proposal* for the
> operator to approve or reject — nothing in them is applied, and no part of them may be
> treated as authorized by this record.** §9 lists findings this task is reporting rather
> than fixing (`AGENTS.md` §16). §10 is the stop-condition report required by
> `tasks/README.md` §10 and `.ai/README.md` §13.

---

## 0. Verdict

**STOP — do not implement.** The task's prerequisite is that Option A from TASK-283 be
implemented *only after the operator explicitly approves* (a) the bounded `GameRuntime`
watchdog, (b) the required `SIGNALR_PROTOCOL.md` §7 amendment, and (c) the initial watchdog
duration and retry policy — and that if that authorization is not present in repository
evidence, the agent must produce a readiness report instead.

None of the three is present:

```text
A. TASK-283 (the carrier of "Option A")   → the file does not exist anywhere in the
                                            repository, in any task folder, in git
                                            history, or in the index. There is nothing
                                            to read, quote, or verify.
B. SIGNALR_PROTOCOL.md §7 amendment       → §7 (v2.23) still enumerates exactly two
                                            recovery triggers (reconnect; a batch that
                                            ARRIVED ahead of the held sequence). No
                                            timeout, missing-batch, or unanswered-action
                                            trigger is documented, and the words
                                            "watchdog" and "client-side timeout" appear
                                            nowhere in docs/.
C. Watchdog duration / retry policy       → no value, bound, or interval is recorded in
                                            any task record, doc, or ADR. The only
                                            recovery-timing statement in the protocol is
                                            "after the client's bounded retry"
                                            (SIGNALR_PROTOCOL.md:2512), with no bound
                                            documented anywhere.
```

Under `AGENTS.md` §20 (ambiguous requirement / missing authorization) and §23 ("if documented
intent is missing or ambiguous, that is a stop condition — not license to design on the
project's behalf"), and under this task's own prerequisite, the correct output is this
readiness report. **No code, test, protocol, or ADR file was modified.**

The gap the task targets is nonetheless **real and confirmed** (§3.2): an accepted action
whose state push was delivered but whose `ReceiveEvents` batch never arrives leaves the
client `synchronized`, holding, and input-locked until some later delivered state releases
it — the boundary already recorded as TASK-280 §7.3 finding 5 and recommended there as a
separate, explicitly authorized follow-up.

---

## 1. The prerequisite and the authorization evidence

### 1.1 What the task requires before any implementation

```text
Implement Option A from TASK-283 ONLY AFTER the operator explicitly approves:
  - a bounded GameRuntime watchdog for an accepted action whose matching batch is missing,
  - the required SIGNALR_PROTOCOL.md §7 amendment,
  - the initial watchdog duration and retry policy.
If authorization is not present in repository evidence, do not implement.
Produce a readiness report instead.
```

The task also fixes the documentation order (§17 of `AGENTS.md`): the §7 amendment lands
**before** the new trigger may be treated as part of the documented contract.

### 1.2 Verification performed (read-only)

```text
1. Filename sweep       tasks/**/*TASK-273*, *TASK-274*, *TASK-283*        → no results
2. Content sweep        git grep -n "TASK-283" -- .                        → no matches
3. History sweep        git log --all --diff-filter=A for TASK-273/274/283 → no commits
                        git log --all -S "TASK-283"                        → no commits
                        git log --all --name-only | select TASK-27[34]|283  → never tracked
4. Doc sweep            grep -i over docs/ for "watchdog"                  → no matches
                        (§7 read in full, 2487–2598; §6 2416–2483)
5. Decision sweep       ADR index (docs/03-decisions/README.md, ADR/ list)  → no
                        watchdog/recovery-trigger ADR; ADR-008's amendment
                        (2026-10-13) covers only the TASK-273 detected-desync trigger
6. Working tree         git status --porcelain --untracked-files=all        → 3 modified
                        files + the pre-existing untracked task records and API
                        test file, all unrelated to TASK-284 (see §11)
```

Exact results are reproduced in §11.2.

### 1.3 What the sweep establishes

```text
A-1  TASK-283 does not exist in any form. The approval artifact cannot be located,
     quoted, or verified — the gate cannot be evaluated as "satisfied".
A-2  TASK-273 and TASK-274 are referenced as DONE by other records (TASK-275:19,
     TASK-276:19–20, TASK-277:15, TASK-278:15–16) and their effects ARE in the tree
     (SIGNALR_PROTOCOL.md:3 v2.23 amendment, ADR-008:5–13 amendment,
     ARCHITECTURE.md:3 v1.13, GAME_STATE.md:3 v2.28) — but no task record file for
     either was ever committed, so their acceptance criteria and safeguards cannot be
     re-read or re-verified from the repository. See finding F-1.
A-3  No document defines a client-side timeout, timer, or watchdog for battle recovery;
     §7's trigger list is exactly two, and neither is a timeout.
A-4  No duration, interval, attempt bound, or backoff for a watchdog is recorded
     anywhere. `MAX_RECOVERY_ATTEMPTS = 3` and the 25/50/100 ms backoff are the
     *existing recovery retry's* implementation values (GameRuntime.ts:204, :1328),
     not an approved watchdog policy.
A-5  §7:2511 names "a withheld batch and an unanswered action" — but inside the
     request-deduplication sentence, as an observation of one unresolved gap. It
     defines no detection rule and no trigger. Making an unanswered/unbatched accepted
     action a trigger is therefore a documentation change, not a silent implementation
     choice (§7's list is closed; AGENTS.md §17–§18).
```

**Conclusion.** The prerequisite is unsatisfied on evidence, not on interpretation. This
record does not implement, does not pre-authorize, and does not pre-empt the operator's
decision. §4–§8 exist so that the decision can be made on a concrete, anchored proposal.

---

## 2. Readiness assessment (canonical output, `.ai/skills/discovery/task-readiness-assessment.md`)

```text
Candidate Task:            TASK-284 (requested directly; no task file pre-existed)
Current Lifecycle Status:  BLOCKED — prerequisite approval artifact absent
Dependency Status:         UNMET — TASK-283 absent (required approval artifact);
                           TASK-273/TASK-274 records absent (code/doc effects present)
Scope Status:              IN_MVP — client reliability of the existing realtime
                           delivery path (MVP_SCOPE.md §1 "Realtime communication
                           (SignalR)"); it adds no game system, content, or mechanic.
                           No MVP_SCOPE.md §2 OUT item is involved.
Documentation Alignment:   ALIGNED for the mechanism (the §7.1 GetBattleState snapshot
                           is already the single resynchronization path, ADR-008);
                           CONFLICT_DETECTED for the trigger — the required §7
                           amendment does not exist, so the new trigger is not part of
                           the documented contract and may not be implemented.
Decision Status:           NEEDS_DECISION — D-1 … D-6 (§5), all operator-owned
Acceptance Criteria Quality:
                           SUFFICIENT — the task's 9 deterministic scenarios are
                           binary and testable; §7 below maps each to a concrete test
Final Verdict:             BLOCKED (NEEDS_DECISION) — readiness report produced;
                           implementation not started, by the task's own instruction
Ranking Justification:     Not ranked. The task is gated on an operator decision, not on
                           priority; the underlying gap is client-local robustness, not
                           a state-divergence defect (TASK-280 §7.3 findings 4–5).
```

---

## 3. Current implementation: what exists, and the exact gap

### 3.1 Mechanisms that already exist (all verified in the working tree)

```text
Recovery entry point       GameRuntime.recoverBattleState()              GameRuntime.ts:1184
                           — one request in flight per battle (:1193–1200), issues
                           SignalRService.getBattleState(battleId) (:1226), ingests the
                           snapshot through the SHARED §4 path ingestBattleState (:1277)
Recovery response checks   envelope serverSequence must equal state.sequence
                           (:1264–1273); malformed payload refused (:1257–1262)
Bounded retry              MAX_RECOVERY_ATTEMPTS = 3 (:204), retryRecoveryIfStillNeeded
                           (:1313), scheduleRecoveryRetry (:1321) with 25·2^(n−2) ms
                           (:1328), gated on battle identity + still-unsynchronized
                           (:1324, :1334)
Not-recoverable path       handleBattleNotRecoverable (:1376) — drops the copy, publishes
                           null with source 'ended' (:1401–1404), takes the existing
                           result fallback (:1407)
Existing trigger A         reconnect/resync: rejoinAndRecoverAfterReconnect (:1085)
Existing trigger B         withheld gap: forwardBattleEvents (:1691) withholds a batch
                           whose serverSequence > held.sequence (:1730) and requests the
                           snapshot once per observed gap (:1763–1790)
Existing trigger C         unanswered RPC: markActionOutcomeUnknown (:871) — no
                           acknowledgement received → unsynchronized + recovery
Superseded-batch rule      a batch at or below the held sequence is forwarded and NOT
                           re-presented: runtime side GameRuntime.ts:1735–1740
                           (ARCHITECTURE.md:321–323), presentation side
                           BattleScene.playEventBatch's acceptedResolutionSequence guard
                           (BattleScene.ts:4275–4282), seeded by a recovery snapshot at
                           BattleScene.ts:2280–2282 via seedAcceptedResolution (:2633)
Recovery releases locks    BattleScene.renderBattleState: a 'recovery' source is never
                           held (hold = !recovery && … , :2341), releases every lock
                           (invalidatePendingPresentation, :2276) and presents the
                           snapshot's board immediately
Input lock                 BattleScene's orchestration machine (:693–698, legal
                           transitions :727–735, locked set :738–742) with
                           isInputLocked (:5974–5976); set at submission (:3671/:3676/
                           :3677, and the same shape for CardCast :3874–3876 and
                           PetSkillCast :3956–3958), released only by delivered facts
                           (:5962–5968) — timeline end (:6073–6087, via releaseTimeline
                           :5293–5303), an acknowledgement with no pending resolution
                           (:6104–6117), a recovery publication (:2276 → :2580–2617),
                           a push that cannot be the awaited resolution
                           (:2307–2313 → :2483–2489), battle gone/ended (:2268–2269,
                           :2298), supersession (:2287–2290), an admitted batch with no
                           phases (:4315–4325), rejection/failure (:3709, :3900–3901),
                           and teardown (:1577, :1594)
sync flip is inert         BattleScene reads state.sync exactly once (:2165) and only
                           for the sync indicator (:2159–2178): no branch keys
                           orchestration, input, or the hold on it — so a timer that
                           only flips `sync` cannot unlock input, by construction
No scene-side timers       setTimeout / delayedCall / Phaser.Time / this.time.* /
                           deadline / Date.now → no matches in BattleScene.ts. The only
                           time-based mechanism is the presentation timeline's pacing
                           tween (:4828–4849), which exists only for a lock that already
                           has a timeline and therefore cannot release the lost-batch
                           hold (no timeline is ever built); pinned by
                           SceneLifecycle.test.ts:7239 ("releases a pending resolution
                           from delivered facts, never from elapsed time")
Port surface is closed     RuntimeBoundaries.test.ts:666–715 asserts the port's exact
                           14 member names; recoverBattleState() is deliberately not on it
                           (GameRuntime.ts:1184). A watchdog needs no port member — and
                           adding one would break that boundary test
```

### 3.2 The gap TASK-284 targets (confirmed, not fixed here)

```text
no watchdog              The runtime holds NO expectation that a delivered
                         BattleStateUpdated(S) will be followed by ReceiveEvents(S).
                         `withheldGapSequence` records only a batch that was AHEAD
                         (GameRuntime.ts:238, :1770–1771); `unresolvedActionIds` records
                         only an action with no acknowledgement (:197, :792).
                         There is exactly one timer in the runtime — the recovery retry
                         (:210) — and none is armed by a delivered state push.
observable consequence   push(S) delivered, batch(S) lost, no later push in a
                         single-player-vs-Boss battle → held.sequence = S, the scene is
                         synchronized and input-locked with the pre-resolution board on
                         screen, and nothing requests recovery (the gap is invisible:
                         no batch arrives to compare against, so §6 item 3's rule never
                         fires). Pinned at the runtime level by GameRuntime.test.ts:4638
                         ("13. cannot detect a batch lost after its own state push
                         advanced the sequence (TASK-280)": seen == [2],
                         getBattleStateCalls == [], lastError == null,
                         sync == 'synchronized'). Recorded independently as TASK-280 §7.3
                         finding 5; the existing scene coverage at
                         SceneLifecycle.test.ts:12256 releases the hold only once a LATER
                         delivered state arrives, so it does not cover "no further push".
                         `sync` stays 'synchronized' — which is the honesty problem the
                         task's completion criteria name.
not a state divergence   The state push is the reconstruction-safe carrier
                         (SIGNALR_PROTOCOL.md:1804–1812); the loss is presentation +
                         a client-local lock, not a diverged state (TASK-280 §7.3
                         finding 4).
```

### 3.3 Constraints any approved implementation must respect (from the documents)

```text
1   Reuse §7.1's existing GetBattleState request; add NO hub method, argument,
    subscription, message, or state-push member (SP:2501–2506, :2515, §4 item 11
    :1648–1653/:1821–1824, §8 items 3–7 and 9 :2618–2658, header :3).
2   Add NO server broadcast-order change and NO retransmission/replay channel
    (SP:2469–2471, :2466–2468, :2643–2645).
3   Never reorder, filter, split, partially apply, count, or interpret a batch
    (SP:402–404, :2462–2464; ARCHITECTURE.md:333–342).
4   Never author, merge, advance, or fabricate state; never infer HP/Power/board from
    events (SP:2512–2513, :2207–2219, :2301–2310; ARCHITECTURE.md:302–305, :351–356).
5   Never write, increment, decrement, or re-derive Sequence (GAME_STATE.md:2783–2785,
    §5.2 item 3 :3418–3429). This forbids computing the expected batch sequence as
    expectedSequence + 1 — see §4.2.
6   At most one GetBattleState in flight per battle (SP:2508–2510; ADR-008:71–72).
7   A failed recovery ends honestly desynchronized; never converge on a guess
    (SP:2512–2513, :2541–2546). BATTLE_NOT_FOUND keeps its own path (SP:2522–2524).
8   A snapshot supersedes every batch at or below its sequence; a superseded or
    withheld batch is never replayed (SP:2588–2598; ADR-008:42–57).
9   Recovery is requested by the runtime, never by a scene; a scene imports no
    transport (ADR-008:79–80; ARCHITECTURE.md:289–297).
10  A watchdog keyed on "accepted ⇒ a batch is expected" would fire on a
    documented-legal case: an idempotent retry returns Accepted: true WITHOUT
    re-broadcasting events (SP §3.1 item 6 :459–461; ADR-025 decision 2 :30). Any
    approved trigger must exclude it or it converts a documented no-broadcast case
    into a false desync. This is decision item D-5.
```

---

## 4. Proposed Option A design (PROPOSAL — NOT IMPLEMENTED, NOT AUTHORIZED)

Offered only so the operator can approve, amend, or reject a concrete shape. Every item
below is unapplied; none is a claim about what the code does today.

### 4.1 Shape

```text
GameRuntime gains ONE bounded "resolution batch expectation" — not a second recovery
mechanism:

  armed       on the delivery of an authoritative state push (source 'push') that
              ADVANCES the held sequence for the battle an accepted action was
              submitted against
  cleared     when the matching batch is admitted, when a recovery snapshot supersedes
              it, when the battle is cleared/ended/replaced, or on dispose
  expires     the expectation's timer fires → publish sync = 'unsynchronized' with a
              technical detail, then invoke the EXISTING recoverBattleState()
              (one request in flight per battle; the existing bounded retry applies)

It adds no request, no message, no event, and no ingestion path: the only new thing is
a client-local, self-cancelling timer plus the association keys that make it correct.
```

### 4.2 Association keys (battle id · action identity · authoritative sequence)

```text
battleId     the battle the action was submitted against — the same value
             requestAction resolves from the held state (GameRuntime.ts:723–744) and
             recoverBattleState addresses (SP §4.9, §7.1)
actionId /   the logical action's identity. For Swap: logicalActionKey +
logicalKey   actionIdFor (GameRuntime.ts:747–753, :903–931), i.e. exactly the identity
             ADR-025 §2 makes retry-stable and the value retained in
             unresolvedActionIds until settled (:792). CardCast / PetSkillCast carry the
             opaque per-request clientSequence (:808, :832) and are identified by
             request rather than by ADR-025 §2's swap identity.
sequence     the sequence the DELIVERED STATE PUSH carries. It is NOT computed. GAME_STATE
             §5.0 (:2783–2785) forbids the client to increment or re-derive Sequence, and
             ADR-025 decision 1 ties the committed resolution to the version the client
             OBSERVES through the push — so "expectedSequence + 1" is not an available
             spelling, even though ADR-025:21 says the counter increments by one.
             Consequence for the design: the expectation is armed by the push, not by
             the acknowledgement.
```

**Why push-armed, not acceptance-armed** (this is the load-bearing choice, and it is both a
correctness and a false-positive argument):

```text
(a) SP §3.1 item 1 (:429–434) writes the state, then publishes, then returns the hub
    result — so the push legitimately arrives BEFORE the RPC acknowledgement, which is
    already the behaviour pinned by GameRuntime.test.ts:3870 ("a newer authoritative state
    arriving before a delayed action response is preserved"). An acceptance-armed
    watchdog would need the acknowledgement first and would mis-key the resolution it
    belongs to.
(b) An accepted response does not imply a broadcast: SP §3.1 item 6 (:459–461) documents
    an idempotent retry returning Accepted: true with NO batch, and ADR-025:30 the same.
    Push-arming keeps that documented case out of the trigger entirely (§5, D-5).
(c) It preserves the existing distinction: an action with no acknowledgement at all is
    already markActionOutcomeUnknown's case (:871) and is not this trigger.
```

### 4.3 Expiry behaviour (honest, bounded, and lock-neutral)

```text
on expiry   1. publish sync = 'unsynchronized' with a technical detail naming the
               delivered sequence, the action identity, and the fact that its batch was
               not delivered (the existing lastError + runtime_error channel —
               no new state member, per ARCHITECTURE.md §2.2.1 rule 5)
            2. invoke the EXISTING recoverBattleState()
            3. DO NOT release input, DO NOT converge the board, DO NOT present anything,
               DO NOT fabricate a result, and DO NOT touch acceptedResolutionSequence
               (the scene's memory) from the runtime — a timer releases nothing
```

Input is released only where it is released today: by a delivered state (`push` or
`recovery`, the latter never held — `BattleScene.ts:2341`, `:2276`), by a batch timeline
completing, by a rejection/failure, by supersession, or by teardown (`:5962–5968`).
The `sync` flip above is provably inert for input: the scene reads `state.sync` exactly
once, for the sync indicator (`BattleScene.ts:2165`, `:2159–2178`), and no branch keys
the orchestration machine, the hold, or the submit guards on it. Expiry therefore cannot
unlock anything even by accident, and it also cannot lock anything: the release comes
only from the snapshot the recovery then delivers.
This is exactly the task's constraint 6 and it preserves `AGENTS.md` §10, and it keeps
`SceneLifecycle.test.ts:7239`'s "never from elapsed time" pin true.

### 4.4 Cancellation and supersession

```text
matching batch admitted      forwardBattleEvents admits serverSequence === expectation
                             .sequence for expectation.battleId → clear expectation
                             (including its timer)
recovery supersedes          ingestBattleState (the shared §4/§7.1 ingestion point,
                             :1913) clears the expectation for that battle with the
                             existing bookkeeping (:1943–1944), so a snapshot that
                             already contains the resolution ends the expectation
                             rather than racing it (SP:2588–2598, ADR-008:42–57)
battle boundary              clearActiveBattleState (:609), handleBattleNotRecoverable
                             (:1376), invalidateSession (:481), dispose (:390) — every
                             boundary that already calls clearRecoveryState (:255) also
                             drops the expectation, so no orphaned timer survives
                             teardown/recreation or a reconnect
stale/foreign push           a push that does not advance the expectation's battle, or
                             that arrives for a replaced battle, does not re-arm it
```

### 4.5 A delayed batch cannot replay over the superseding snapshot

The guarantee is already composed and does not need a new rule; the implementation must not
weaken it:

```text
runtime      a batch at or below the held sequence is forwarded whole and changes no
             synchronization (GameRuntime.ts:1735–1740; ARCHITECTURE.md:321–323)
scene        BattleScene.playEventBatch presents no second time a batch whose
             serverSequence ≤ acceptedResolutionSequence (:4275–4282), and a recovery
             snapshot seeds that memory from the snapshot's own sequence (:2280–2282,
             :2633–2646)
```

So a delayed batch for a resolution a watchdog-triggered recovery snapshot already covers
converges through the existing path and is presented no second time (SP:2588–2598).
`TASK-284`'s constraint 7 is therefore a **regression-prevention** requirement, not new
behaviour to invent — and the planned test in §7 asserts it end to end.

### 4.6 Retry policy (proposal; operator-owned — D-4)

```text
Option R1 (recommended for review)  the watchdog fires at most ONCE per unresolved
                                    expectation and delegates all retrying to the
                                    existing bounded recovery retry
                                    (MAX_RECOVERY_ATTEMPTS = 3, 25/50/100 ms;
                                    GameRuntime.ts:204, :1313, :1328). No second backoff
                                    loop, no second timer family, no new constant beyond
                                    the watchdog duration.
Option R2                          the expectation re-arms up to N times before
                                    delegating (a longer total wait, one more counter).
Option R3                          no independent retry at all: fire once; a failed
                                    recovery leaves the client 'unsynchronized' with its
                                    last reported failure (which is R1's outcome, stated
                                    without the delegation detail).
```

R1 is recommended because the document already assigns retry to the recovery request
(SP:2508–2513) and because a second timer family would be the first thing `AGENTS.md` §9
(anti-overengineering) would question.

### 4.7 Duration (proposal; operator-owned — D-3)

There is **no documented basis for any value**, so this record does not adopt one. The
tradeoff the operator is choosing between:

```text
too short   a slow-but-healthy round trip, or a device that is briefly stalled, is
            converted into a needless recovery: the client marks itself unsynchronized,
            fetches a snapshot it did not need, and the scene presents the snapshot with
            'recovery' semantics (no animation for a resolution that did arrive). It never
            fabricates an outcome and never diverges — the cost is presentation, not
            correctness.
too long    a genuinely lost batch leaves the player holding a locked, stale board for
            that long before the client even asks. Nothing else recovers it (no later
            push exists in a single-player battle).
```

Candidate values for the operator to choose or replace: **2000 ms**, **3000 ms**,
**5000 ms**. This record recommends **5000 ms as the initial value** on the ground that the
cheap failure mode is a needless-but-correct snapshot while the expensive failure mode is
player-visible deadlock — but that reasoning is this record's, not a document's, and the
value is not adopted here. It is decision D-3 and must be recorded by the operator (or in
the amended doc) before implementation.

---

## 5. Decision items requiring operator authorization

```text
D-1  Authorize Option A at all? (the bounded GameRuntime watchdog) — this is the gate.
     Absent evidence: A-1. Not implemented by this task.
D-2  Authorize the SIGNALR_PROTOCOL.md §7 amendment, and decide its scope:
     (i) §7 only (the task's stated requirement), or
     (ii) §7 + an ADR-008 §Decision amendment. ADR-008:33–40 currently frames the
     snapshot as the answer to "a detected desynchronization" and enumerates what can
     open a gap; adding a third opening route may fall under AGENTS.md §18
     ("realtime strategy"). The mechanism does not change either way — the same §7.1
     request, deduplicated and bounded — so this is a documentation-scope decision.
D-3  Approve the initial watchdog duration (§4.7; no documented basis).
D-4  Approve the retry policy (§4.6, R1/R2/R3).
D-5  Approve the trigger's exclusion of the documented no-broadcast acceptance
     (SP §3.1 item 6 :459–461; ADR-025:30). Without this, a legitimate idempotent retry
     would be reported as a desync (§3.3 constraint 10, §4.2).
D-6  Confirm that NO BattleScene or GameRuntimePort change is in scope. The proposal
     needs neither: input is still released only by a delivered state through the
     existing recovery path (ADR-008:74–80; BattleScene.ts:2272–2282), and no new port
     member is required — recoverBattleState() is already public on the class and
     deliberately not on the port (GameRuntime.ts:1184), and
     RuntimeBoundaries.test.ts:666–715 asserts the port's exact 14 member names, so
     adding one would break a boundary test rather than help. If a port member or scene
     change is wanted, that is a separate, additionally authorized scope and this record
     does not cover it.
D-7  Confirm whether TASK-283's Option A text should be re-filed into the repository
     first (finding F-2), so the approval artifact can be cited by later work.
```

---

## 6. Draft `SIGNALR_PROTOCOL.md` §7 amendment (DRAFT — NOT APPLIED)

Shown so D-2 can be decided on exact text. It adds **no** method, argument, subscription,
message, or state member, and it is deliberately written as a *third moment* that closes
through the same item 1 request. If approved, it lands **before** any implementation, per
`AGENTS.md` §17.

```text
--- proposed insertion, §7 preamble (after :2499's list, :2501–2506 paragraph) ---

An accepted action's resolution batch is a third way the client can be desynchronized,
and it is closed by the same item 1 request as the other two:

  unbatched resolution   the authoritative state push for a committed, accepted action
                         was delivered (so the client's `sequence` advanced to n), but
                         the `ReceiveEvents` batch at n did not arrive. §6 item 3's
                         comparison cannot detect this: it keys on a batch that
                         ARRIVES ahead of the held sequence, and here no batch arrives.

                         A client MUST NOT wait indefinitely for that batch. It MAY arm
                         one bounded, client-local expectation when a delivered state
                         push advances the sequence for a battle it has an accepted,
                         unsettled action against, and, if the matching batch is not
                         admitted before it expires, it marks itself unsynchronized and
                         requests the same §7.1 `GetBattleState(battleId)` snapshot.

                         The expectation is a delivery-timing fact only. It adds no hub
                         method, argument, subscription, message, or state-push member;
                         it requests nothing new and no event is ever replayed;
                         it never releases the client's input lock by itself; and it
                         never determines, infers, or fabricates an outcome —
                         `BattleStateUpdated` remains the reconstruction-safe carrier
                         (§4 item 18), and the expired expectation's answer is the
                         snapshot, exactly as for the other two moments.

                         A batch arriving after the snapshot that the snapshot covers
                         (`serverSequence` at or below the snapshot's `sequence`) is
                         superseded and is never replayed, per this section's precedence
                         rule below. An accepted acknowledgement whose resolution was
                         already committed and broadcast (for example an idempotent
                         retry, §3.1 item 6) is not an unbatched resolution and is not
                         reported as a desync.
```

```text
--- proposed version-header note (SIGNALR_PROTOCOL.md:3) ---
Version 2.24 (§7 **amended**: a third desynchronization moment — an accepted action whose
authoritative state push was delivered but whose `ReceiveEvents` batch never arrived — is
recorded, with the bounded client-local expectation that detects it, the requirement that
expiry mark the client unsynchronized and request the existing §7.1 snapshot, and the
explicit statements that the expectation releases no input and fabricates no outcome. **No
hub method, argument, subscription, delivery path, state-push member, or gameplay rule is
added or removed.**)
```

---

## 7. Proposed deterministic test plan (for the authorized implementation)

All nine required scenarios, in `src/frontend/client/tests/GameRuntime.test.ts` unless
noted, using `vi.useFakeTimers()` and controlled promises — no `sleep`, no wall-clock wait.
The existing file already supplies the pattern (a `FakeSignalR` double with
`swapBehaviour`, `getBattleStateBehaviour`, `getBattleStateCalls`, `emit(...)`).

```text
 #  Required scenario                              Test sketch
── ─────────────────────────────────────────────  ─────────────────────────────────────
 1  matching batch before timeout: normal          arm via push advanced for an accepted
    presentation, no recovery                      action; deliver batch(n); expect it
                                                   forwarded, sync stays 'synchronized',
                                                   no getBattleState call, timer cleared
                                                   (advance timers past the window →
                                                   still no call)
 2  state push before RPC acknowledgement          hold the swap promise unresolved; emit
                                                   push(n); assert the expectation is
                                                   associated with the DELIVERED sequence
                                                   and that resolving the delayed ack
                                                   afterwards neither re-arms nor
                                                   re-keys it (extends existing test 4,
                                                   GameRuntime.test.ts:3870)
 3  batch lost → authoritative recovery            push(n) with no batch; advance fake
                                                   timers by the duration → sync
                                                   'unsynchronized', exactly one
                                                   getBattleState call, snapshot ingested
                                                   through the existing §4 path
 4  batch arrives while recovery is in flight →    push(n) → expire → snapshot in flight
    no duplicate or stale presentation             → deliver batch(n): assert no second
                                                   getBattleState call (one in flight per
                                                   battle), no second 'recovery'
                                                   publication, and (scene-side, in
                                                   SceneLifecycle.test.ts) no second
                                                   presentation
 5  recovery snapshot supersedes the pending       push(n) → expire → snapshot at n →
    batch: scene converges, locks released         deliver the delayed batch(n): runtime
    through the existing recovery path             forwards it whole (it is at the held
                                                   sequence), the scene's
                                                   acceptedResolutionSequence guard
                                                   presents it no second time, and the
                                                   recovery publication already released
                                                   the lock (hold = !recovery)
 6  recovery fails → stays unsynchronized,         getBattleStateBehaviour throws: sync
    retries bounded                                remains 'unsynchronized', attempts are
                                                   bounded by MAX_RECOVERY_ATTEMPTS,
                                                   no fabricated state
 7  terminal or expired battle: existing            BATTLE_NOT_FOUND keeps its path —
    result/not-recoverable handling stays          copy dropped, null + 'ended' published,
    authoritative                                  result fallback taken; no watchdog
                                                   retry after the battle is gone
 8  teardown/recreation and reconnect leave no     expire/arm an expectation, then
    orphaned watchdogs                             dispose() / clearActiveBattleState() /
                                                   reconnect: advance timers past the
                                                   window and assert no recovery is
                                                   requested and no listener is notified
 9  a newer battle/sequence cannot be recovered    arm for battle A at n; replace the
    using an obsolete action's watchdog            battle (or advance to n+1 via a later
                                                   push); assert A's expectation cannot
                                                   request recovery for B, and that the
                                                   newer resolution's own expectation
                                                   (or its admitted batch) is what
                                                   governs
```

Regression assertions to keep green in the same run: the existing P0.4 suite
(`GameRuntime.test.ts:4256`+ — push-then-batch, gap withhold, duplicate/older batch,
cross-battle drop), the P0.2 suite (`:3778`+ — action identity, unanswered action,
push-before-ack at `:3870`), the TASK-280 blind-spot test (`:4638`), and the scene's
delivered-fact pins — `SceneLifecycle.test.ts:7239` ("releases a pending resolution from
delivered facts, never from elapsed time") and `:12256` (replay-style push without a
batch) — plus `RuntimeBoundaries.test.ts:666–715` (the port member list) and
`:653–664` (the scene stays transport-independent). The focused files are
`src/frontend/client/tests/GameRuntime.test.ts`,
`src/frontend/client/tests/SceneLifecycle.test.ts`, and
`src/frontend/client/tests/RuntimeBoundaries.test.ts`.

---

## 8. Documentation and verification plan (to run only after authorization)

```text
pre-code     1. apply the approved §7 amendment (and, per D-2, any ADR-008 amendment)
                in the same slice that introduces the trigger, per AGENTS.md §17
             2. record D-3/D-4's approved values in the amended section
implement    src/frontend/client/src/game/runtime/GameRuntime.ts (+ its doc comment,
                which currently enumerates the recovery entry points at :108–117)
tests        src/frontend/client/tests/GameRuntime.test.ts, and
                src/frontend/client/tests/SceneLifecycle.test.ts if a scene-side
                assertion is needed
verify       focused:   npx vitest run tests/GameRuntime.test.ts        (cwd src/frontend/client)
             full:      npx vitest run                                   (cwd src/frontend/client)
             types:     npx tsc --noEmit                                 (cwd src/frontend/client)
             hygiene:   git diff --check
             backend:   NOT required — no shared contract, backend source, or wire
                        message is touched by the proposal (and none may be)
```

---

## 9. Findings reported, not fixed (`AGENTS.md` §16)

```text
F-1  TASK-273 and TASK-274 have no record file in the repository — not in backlog/,
     active/, blocked/, or completed/, and never in git history — although their code
     and document effects are present and four other records cite them as DONE. Their
     safeguards cannot be re-verified from the repository. Impact: traceability of the
     safeguards TASK-284 must not weaken. Suggested follow-up: a documentation task that
     either files the two records or records explicitly that their ratification exists
     only as document version headers.
F-2  TASK-283 — the task this task's prerequisite names as the carrier of "Option A" —
     does not exist in any form. Impact: the approval artifact cannot be cited; the gate
     cannot be evaluated except as "not present". Suggested follow-up: re-file TASK-283
     (or its decision) before TASK-284 is taken up again (D-7).
F-3  `SIGNALR_PROTOCOL.md` §5 item 2 (:2395–2396) states that an accepted action's events
     "arrive in the §3 batch", while §3.1 item 6 (:459–461) and ADR-025:30 document an
     accepted action (an idempotent retry) that is never re-broadcast. These are not in
     conflict for the server, but they are the exact pair a watchdog must be written
     against, and the §7 amendment proposed in §6 states the exclusion explicitly (D-5).
F-4  §7:2511 names "a withheld batch and an unanswered action" in its deduplication
     sentence, and GameRuntime's own doc comment repeats it (:1171–1177), while §7's
     trigger list contains only two moments. Impact: a reader could mistake the phrase
     for an existing trigger. The proposed amendment (§6) is the smallest correction.
F-5  `.ai/PROJECT_STATE.md` is stale (it records TASK-262/263 as the frontier; the tree is
     at TASK-280+ records). The readiness skill's step 4 asks for a navigation-cache
     update, which this task did not perform: it is outside TASK-284's scope and the
     cache's own "verified against Git HEAD" claim would need a broader audit to restore
     honestly. Reported instead.
F-6  TASK-280 §7.6 already recommends exactly this follow-up as "a separate client
     recovery-trigger investigation (operator decision)". This record is that
     investigation's readiness half; it does not reopen TASK-280's verdict.
```

---

## 10. STOP CONDITION (`.ai/README.md` §13 / `tasks/README.md` §10 format)

```text
STOP CONDITION

Problem:
TASK-284's implementation is gated on an explicit operator authorization that is not
present in repository evidence: TASK-283 ("Option A") does not exist, SIGNALR_PROTOCOL.md
§7 has not been amended for a missing-batch trigger, and no watchdog duration or retry
policy is recorded.

Relevant sources:
- tasks/backlog/TASK-284-runtime-recovery-for-missing-accepted-action-event-batches.md §1
  (the prerequisite and the verification sweep)
- docs/02-technical/SIGNALR_PROTOCOL.md §7 (:2487–2598), §6 items 3–5 (:2416–2483),
  §3.1 item 6 (:453–461), §8 items 3–9 (:2618–2658), version header (:3)
- docs/03-decisions/ADR/ADR-008 (:33–57, :63–85), ADR-025 (:20–32)
- docs/02-technical/ARCHITECTURE.md §2.2.1 rules 1, 3, 4, 5 (:289–356)
- docs/02-technical/GAME_STATE.md §5.0 (:2733–2793)
- AGENTS.md §4, §7, §16, §17, §18, §20, §23; tasks/README.md §10

Conflict / missing information:
The task requires implementing "Option A from TASK-283" after explicit operator approval
of the watchdog, the §7 amendment, and the duration/retry policy. No approval artifact
exists, the documented §7 trigger list is closed at two non-timeout moments, and no
duration or retry value is documented anywhere. Implementing anyway would invent a
protocol trigger and two client policy values, which AGENTS.md §17/§18/§20/§23 forbid.

Proposed resolution:
Keep this readiness report and the §4–§7 proposal in tasks/backlog/; re-file TASK-283 (or
its decision) so the approval artifact exists; have the operator answer D-1 … D-7; then
apply the §7 amendment first and implement the watchdog in a follow-up slice limited to
GameRuntime.ts plus its tests.

Waiting for:
Operator / Product-Owner authorization: D-1 (Option A), D-2 (§7 amendment scope),
D-3 (watchdog duration), D-4 (retry policy), D-5 (exclusion of the documented
no-broadcast acceptance), D-6 (no scene/port change), D-7 (re-filing TASK-283).
```

---

## 11. Preservation and verification evidence

### 11.1 Files changed by this task

```text
tasks/backlog/TASK-284-runtime-recovery-for-missing-accepted-action-event-batches.md
  NEW — this readiness report. No other file was created, modified, deleted, or moved.
```

No source, test, `docs/`, ADR, schema, or game-rule file was edited. No TASK-273/TASK-274
safeguard was weakened (none was touched). TASK-276/TASK-277/TASK-278 decisions were not
altered. No commit, push, checkout, reset, stash, or other Git write operation was
performed.

### 11.2 Verification executed

```text
git status --porcelain --untracked-files=all
  M src/backend/GameServer.Domain/Match3/BattleEventBuilder.cs          (pre-existing)
  M src/frontend/client/tests/GameRuntime.test.ts                       (pre-existing)
  M tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs   (pre-existing)
  ?? tasks/backlog/TASK-276-…, TASK-277-…, TASK-278-…, TASK-279-…, TASK-280-…  (pre-existing)
  ?? tests/backend/GameServer.Api.Tests/Hubs/BattleHubPartialBroadcastTests.cs (pre-existing)
  ?? tasks/backlog/TASK-284-runtime-recovery-for-missing-accepted-action-event-batches.md
       — this report: the only addition made by this task
  — every pre-existing change and untracked file is preserved untouched.

git diff --check                                → clean (exit 0)
npx vitest run tests/GameRuntime.test.ts        → 1 file passed, 192 tests passed
  (cwd src/frontend/client; run for baseline evidence only — no implementation exists
   to test, and the task's full verification set belongs to the authorized slice)
git grep -n "TASK-283" / history / filename sweeps → no matches (see §1.2)

Not run, deliberately: dotnet test (no C# file is touched and no shared contract changes),
full frontend suite and tsc (they belong to the authorized implementation slice, and this
task changes TypeScript nowhere).
```

### 11.3 Definition of Done (`AGENTS.md` §22), as it applies to a blocked task

```text
[x] Requirement understood                  — gate read as the task's own prerequisite
[x] Relevant docs checked (§6)              — SP §3/§3.1/§4/§5/§6/§7/§8, GAME_STATE
                                              §5.0–§5.1, ARCHITECTURE §2.2.1, ADR-008,
                                              ADR-025, MVP_SCOPE §1
[x] Scope checked (§8)                      — existing realtime delivery path; no new
                                              system, content, or mechanic
[x] Existing implementation checked         — GameRuntime.ts, GameRuntimeEvents.ts,
                                              BattleScene.ts and their tests
[x] Plan created                            — §4–§7 (proposal, pending authorization)
[ ] Code implemented                        — NOT APPLICABLE: the task's prerequisite
                                              forbids it, so a readiness report is the
                                              required deliverable
[ ] Relevant tests added/updated            — NOT APPLICABLE: no behavior changed
[x] Tests pass                              — pre-existing suites unaffected; focused
                                              runtime suite green (§11.2)
[x] No unrelated behavior changed (§16)     — no behavior changed at all
[x] Documentation updated if required (§17)  — the required §7 amendment is DRAFTED (§6)
                                              and deliberately NOT applied
[x] No source-of-truth conflict introduced   — §3.3 and §9 report the tensions found
                                              rather than resolving them silently
```
