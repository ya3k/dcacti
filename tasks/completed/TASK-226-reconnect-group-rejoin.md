# TASK-226 — Reconnect Group Rejoin

**Type:** COMPLETION RECORD — record creation only. No production code, test,
contract document, smoke script, migration, or historical task record is
modified by this task.
**Subject:** P-1 — reconnect group re-join is unstated and unimplemented.
**Decision authority for the implemented flow:**
`OPTION A — Client Rejoin` (Product Owner approved decision; see §3).
**Status:** DONE

---

## 1. Status

```text
DONE
```

P-1 is implemented and verified. This record is the immutable completion record
for that work.

---

## 2. Objective

P-1 restores SignalR battle-group membership after a reconnect.

SignalR group membership is **connection-scoped**: the connection a reconnect
produces is a member of no group, so the server-initiated group broadcasts the
client depends on after a resolution (`BattleStateUpdated`, `ReceiveEvents`)
would not reach it. A reconnect that re-established only the authoritative
snapshot would leave the player receiving no further battle events.

P-1 closes that gap by having the **client re-invoke the existing
`JoinBattle(battleId)` operation on the new connection**, before authoritative
recovery resumes.

Implemented flow:

```text
successful reconnect
        ↓
await JoinBattle(battleId)      (on the new connection)
        ↓
await recoverBattleState()      (existing §7.1 GetBattleState snapshot)
        ↓
normal battle event delivery
```

No new hub method, event, wire member, or state model was introduced: the
re-join reuses the `JoinBattle` operation `startBattle` already performs.

---

## 3. Decision

```text
OPTION A — Client Rejoin
```

The approved decision is the authority for this work and is **not** recreated or
re-analyzed here. It selected the client as the owner of the re-join, over the
alternative of the server restoring group membership on connect.

Consequences fixed by that decision, as implemented:

- The client performs the re-join; the server restores nothing on its own.
- The re-join uses the **existing** `JoinBattle` operation.
- The re-join completes **before** the §7 item 1 snapshot request is issued.
- A runtime holding no current battle knows no group to re-join and issues no
  `JoinBattle`.

Documented rule added under this decision (`SIGNALR_PROTOCOL.md` §7 **item 4**):
on a successful reconnect the client re-adds its new connection to the battle's
group by invoking `JoinBattle(battleId)`; membership is connection-scoped and is
never restored by the server.

---

## 4. Implementation

Four files were changed:

```text
docs/02-technical/SIGNALR_PROTOCOL.md
src/frontend/client/src/game/runtime/GameRuntime.ts
src/frontend/client/tests/GameRuntime.test.ts
src/frontend/client/scripts/standalone-web-smoke.mjs
```

### 4.1 What actually changed

- **`docs/02-technical/SIGNALR_PROTOCOL.md`** — §7 item 4 documents
  **client-side rejoin**: the client re-adds its new connection to the battle's
  group via `JoinBattle(battleId)` on a successful reconnect; group membership is
  connection-scoped and never restored by the server; the re-join completes
  before §7 item 1's snapshot request, so the new connection is a group member
  before normal post-reconnect delivery is expected; a runtime with no current
  battle issues no `JoinBattle`. The existing `JoinBattle` push and existing
  `GetBattleState` snapshot remain the authoritative delivery paths.
- **`src/frontend/client/src/game/runtime/GameRuntime.ts`** — the `onReconnected`
  handler now calls `rejoinAndRecoverAfterReconnect()`, which **awaits
  `JoinBattle` before recovery**: it reads the battle id the runtime already
  holds, awaits `this.signalR.joinBattle(battleId)` when a battle is known, and
  only then awaits `recoverBattleState()`. A failed re-join is reported on the
  existing `runtime_error` channel and recovery still runs — the snapshot is a
  direct request/response and does not depend on group membership.
- **`src/frontend/client/tests/GameRuntime.test.ts`** — reconnect coverage
  asserts the re-join: `JoinBattle` is invoked on reconnect when a battle is
  known, it is awaited before the snapshot request, no join is issued when no
  battle is held, and a join failure does not suppress recovery.
- **`src/frontend/client/scripts/standalone-web-smoke.mjs`** — the reconnect E2E
  performs a real reconnect and then proves delivery **on the new connection**
  (see §5), rather than reconnecting without observing subsequent delivery.

### 4.2 What did not change

The existing `recoverBattleState` / `receiveBattleState` path **remains in use**
and unmodified in substance: the re-join is inserted ahead of it, and the
snapshot is still ingested through the same `receiveBattleState` path the §4
push uses.

Explicitly **not modified** by P-1:

```text
BattleHub.cs
RuntimeService
SignalRService.ts
```

**No server-side group restoration is claimed.** `BattleHub.OnConnectedAsync`
still re-adds no group, and `JoinBattle` remains the only `AddToGroupAsync`
site. The server restores nothing on reconnect; the client performs the re-join
using the pre-existing operation.

---

## 5. Acceptance criteria

The critical acceptance behavior:

```text
real reconnect
        ↓
new connection id
        ↓
JoinBattle on new connection
        ↓
committed battle action
        ↓
ReceiveEvents on new connection
        ↓
BattleStateUpdated on new connection
```

The reconnected client must not merely re-read state; it must be reachable by
subsequent server-generated group traffic.

> **Snapshot recovery alone is not considered sufficient; acceptance requires
> subsequent server-generated group broadcasts to reach the reconnected
> connection.**

This is why the E2E reconnect phase commits a battle action after the reconnect
and asserts that the group broadcasts arrive **on the new connection**.

---

## 6. Verification

Verified results from the implementation report:

```text
GameRuntime.test.ts                 134 passed
full frontend Vitest suite          21 files / 891 passed
backend API tests                   337 passed
BattleHub-focused tests              30 passed
TypeScript --noEmit                 passed
frontend build                      passed
reconnect E2E                       2 runs × 159 reported checks, 0 failures
post-implementation audit           PASS
```

No additional tests are claimed beyond those listed here.

---

## 7. Evidence caveats

Two caveats are recorded honestly. Both are classified:

```text
NON-BLOCKING EVIDENCE CAVEAT
```

Neither blocks the P-1 verdict, and **no follow-up engineering task is created
for either**.

### F-1 — Negative control reproducibility

```text
NON-BLOCKING EVIDENCE CAVEAT
```

The negative control (reconnect with the re-join **disabled**, demonstrating
that delivery does not reach the new connection without it) was executed as a
**temporary local modification**. It is **not reproducible from the
committed/current tree**, because no disable switch exists — there is no
supported way to turn the re-join off in the shipped configuration.

The positive evidence (delivery observed on the new connection after a real
reconnect) stands on its own and is unaffected.

No follow-up engineering task is created for F-1.

### F-2 — Aggregate E2E count

```text
NON-BLOCKING EVIDENCE CAVEAT
```

The reported `2 × 159` aggregate check count is **not independently derivable**
from the current script's `record()` sites, because some branches are
unreachable in normal execution (e.g. failure paths that only fire on error).

The aggregate is a run-time tally, not an auditable assertion inventory. The
**named acceptance checks remain the authoritative evidence** for P-1 — in
particular the post-reconnect action and the assertion that `ReceiveEvents` and
`BattleStateUpdated` arrive on the new connection.

No follow-up task is created for F-2.

---

## 8. Scope exclusions

P-1 did **not** authorize and did **not** modify any of the following:

```text
BattleHub.OnConnectedAsync
RuntimeService
Redis state
database / schema
ownership / authorization
new SignalR methods, events, or wire members
Relic configuration recovery
Boss configuration recovery
phase-6d caption race
TASK-212B
TASK-215B
TASK-223 / TASK-224
unrelated SignalR refactoring
```

P-1 is confined to the client reconnect flow, its protocol sentence, and the
tests that prove it. Everything above remains owned elsewhere, unchanged.

---

## 9. TASK-219A relationship

> **This completion record satisfies the forward-looking P-1 follow-up
> identified in TASK-219A §10.**

TASK-219A §10 reported P-1 as a **pre-existing reconnect defect, not
attributable to TASK-219A**, and proposed the smallest follow-up: a
decision-then-implementation task on who rejoins the battle group after a
reconnect, with one protocol sentence added to §7. P-1 was subsequently decided
(`OPTION A — Client Rejoin`), implemented, and audited; this record closes that
follow-up.

`TASK-219A-post-implementation-audit.md` was **not modified** by this task and
must not be modified.

TASK-219A remains an **immutable historical record**. Its original P-1 finding
must **not** be rewritten, restated as resolved inside that document, or
retro-edited to point at this record. At the time TASK-219A was written, P-1 was
genuinely unstated and unimplemented, and the audit correctly reported it that
way; that historical statement stays exactly as it is. This record is the
separate, later artifact that closes the gap — it does not amend the audit's
account of what was true then.

---

## 10. Ownership / commit note

The post-implementation audit's ownership findings are preserved:

- `GameRuntime.ts` and `GameRuntime.test.ts` now have a **recordable owner
  through TASK-226** — the P-1 change to each is attributable to this task.
- `standalone-web-smoke.mjs` is **shared work** and must **not** be treated as a
  P-1-exclusive commit slice. It carries changes from other tasks, and P-1's
  edit to it cannot be separated out as a whole-file P-1 slice.
- **TASK-224 remains the authority for deferred integration/commit handling.**
  This record does not alter, pre-empt, or extend TASK-224's integration plan.

Creating TASK-226 does **not** authorize staging or committing anything.

No Git operation was performed by this task.

---

## 11. Task id

This record uses **TASK-226**.

**TASK-225 is not claimed, not created, and remains reserved** by TASK-223 §10
F-3 ("reconcile the duplicated TASK-211 / TASK-213 ids and add an id-collision
check to the task intake rules").
