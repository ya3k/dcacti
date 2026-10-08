# TASK-228 — Tighten Phase 6 First-Cast E2E Wait (V-2)

**Type:** COMPLETION RECORD — record creation only. This task creates this
record and nothing else. No production code, test, contract document, smoke
script, migration, or historical task record is modified by it.
**Subject:** V-2 — the Phase 6 first-cast wait resolved on any non-empty
`castText`.
**Predecessor:** TASK-227 (V-1).
**Status:** DONE

---

## 1. Status

```text
DONE
```

V-2 is implemented and verified. This record is the immutable completion record
for that work.

---

## 2. Problem

The Phase 6 first-cast wait in `standalone-web-smoke.mjs` previously resolved on
**any non-empty `castText`**:

```js
if (snap.castText && snap.castText.length > 0) return snap.castText;
```

`castText` carries three kinds of text, not one. Alongside the server's settled
acknowledgement, the same line carries the scene's **transient** transport
captions:

```text
CardCast <cardId> in flight…                      request on the wire
CardCast <cardId> not sent: <error>               request never left the client
CardCast unavailable: <runtime message>           no runtime connected
CardCast <cardId>: accepted. Awaiting the server's state push.   settled
CardCast <cardId>: rejected (<reason>).                          settled
```

A wait described as capturing the **authoritative cast acknowledgement** was
therefore satisfied by a caption that is not an acknowledgement at all. The
consequences:

- a **false-positive Phase 6 result** — the phase reported success while the
  server had answered nothing;
- a **vacuous downstream check** — B-02's applicability test becomes meaningless
  when a genuinely accepted first cast is still represented by the `in flight…`
  caption.

This was an **E2E verification defect only**. No production behavior defect was
identified: the captions are correct, the scene is correct, only the test's
notion of "acknowledged" was wrong.

---

## 3. Root cause

The caption ladder contains both transient and settled states, and the Phase 6
wait never distinguished them.

The predecessor task had already introduced the helper that makes exactly that
distinction:

```js
function isSettledCastAcknowledgement(text) {
  return typeof text === 'string' && /: (?:accepted|rejected)\b/.test(text);
}
```

TASK-227 (V-1) routed the Phase 6b and Phase 6d waits through it. **Phase 6 had
not been routed through it**, so the one wait with no stale-caption comparison
was also the one wait with no settled-caption comparison — it accepted the first
caption of any kind.

---

## 4. Implementation

The Phase 6 first-cast wait predicate was changed to:

```js
const text = snap.castText || '';
return isSettledCastAcknowledgement(text) ? text : false;
```

Only the Phase 6 first-cast wait was changed. Everything the task was not
authorized to touch remained unchanged, and this was verified by diff inspection
against the pre-change working tree:

```text
click location and action
8000 ms timeout
polling mechanism
acknowledgement record             (phase6.castFeedbackReceived)
retry behavior
surrounding Phase 6 assertions
production caption behavior
Phase 6b and Phase 6d V-1 logic
```

No new acknowledgement predicate was introduced, and no new `record(...)` site
was introduced: the change reuses the existing helper and the existing record
site.

---

## 5. Test guard

Modified:

```text
src/frontend/client/tests/CastFeedbackWaitPredicate.test.ts
```

The guard is a **source guard** in the repository's established style
(`SceneLifecycle.test.ts`): the smoke script drives Chrome over CDP, so it can
be neither imported by the TypeScript suite nor executed in it. The guard reads
the script's own source, comment-strips it, and **compiles and executes the
predicates it finds** — so it verifies behavior, not a paraphrase of it.

It now verifies:

- exactly one helper declaration;
- three helper call sites;
- four total textual occurrences including the declaration (a fourth call site
  would mean the predicate was wired somewhere unaudited);
- Phase 6 explicitly calls `isSettledCastAcknowledgement`;
- the Phase 6 gated return has the expected shape
  (`isSettledCastAcknowledgement(text) ? text : false`);
- transient captions resolve to `false`;
- settled captions resolve to the **original caption**, unchanged;
- Phase 6b and Phase 6d retain their existing stale-caption protections
  (`text !== castFeedback`, `text !== secondCastText`);
- the production captions in `BattleScene.ts` are untouched.

---

## 6. Verification

### Targeted test

```text
npx vitest run tests/CastFeedbackWaitPredicate.test.ts
7 passed / 0 failed
1 file
```

### Full frontend suite

```text
npx vitest run
898 passed / 0 failed
22 test files
```

### Typecheck

```text
npx tsc --noEmit
PASS (exit 0)
```

### Build

```text
npm run build
PASS (exit 0)
82 modules transformed
```

Only pre-existing Rollup warnings were reported (SignalR `/*#__PURE__*/`
annotation placement, chunk size). Neither is attributable to this task.

### Behavioral before/after verification

The pre-TASK-228 HEAD predicate and the post-TASK-228 predicate were both
executed against the production caption ladder, through the same compiled
predicate path the guard uses. For the Phase 6 wait:

```text
                BEFORE (HEAD)   AFTER (TASK-228)
in flight       resolved        rejected
not sent        resolved        rejected
unavailable     resolved        rejected
accepted        resolved        resolved  → exact original caption returned
rejected        resolved        resolved  → exact original caption returned
```

For settled captions the exact original caption is returned, unmodified — the
wait reports *what the server said*, not a normalized form.

V-1 preservation was re-confirmed on the same run. With Phase 6b's marker set to
the settled `accepted` caption:

```text
in flight       rejected
not sent        rejected
unavailable     rejected
accepted        rejected   ← settled but stale: still not a *new* acknowledgement
rejected        resolved
```

### E2E

```text
NOT RUN — infrastructure unavailable
```

At verification time:

```text
backend   :5000   not listening
frontend  :5173   not listening
PostgreSQL :5432  not listening (an unrelated instance listens on :5433)
Docker            not installed
Redis     :6379   listening
```

**No live E2E result was fabricated or inferred.** The deterministic evidence
above stands on its own; it does not establish that the phase passes against
running infrastructure.

---

## 7. Scope and boundary verification

Confirmed **unchanged** by this task:

```text
BattleScene.ts
GameRuntime.ts
SignalRService.ts
BattleHub.cs
SIGNALR_PROTOCOL.md
GAME_RULES.md
GDD
MVP_SCOPE.md
TASK-189
TASK-219A
TASK-227
```

Also unchanged: the `isSettledCastAcknowledgement` implementation, production
captions, polling interval, timeout, retry behavior, Phase 6b, Phase 6d, and
R-1.

The boundary between the three waits is now explicit:

```text
Phase 6    settled acknowledgement only                        → TASK-228
Phase 6b   settled acknowledgement + stale-caption guard       → TASK-227
Phase 6d   settled acknowledgement + stale-caption guard       → TASK-227
```

**Phase 6 deliberately has no stale-caption comparison**, and this is correct
rather than an omission: Phase 6 is the *first* cast, so `castText` starts empty
and there is no earlier caption available to go stale. Adding a guard there
would compare against nothing.

---

## 8. Audit result

Post-implementation audit verdict:

```text
COMPLETE
```

The audit independently confirmed:

1. exact TASK-228 diff boundary;
2. correct helper occurrence semantics;
3. executable transient/settled predicate behavior;
4. exact settled-caption preservation;
5. V-1 preservation;
6. no production/protocol/gameplay changes;
7. no accidental stale-caption guard introduced into Phase 6;
8. unchanged timeout/polling/retry behavior;
9. deterministic tests passing;
10. typecheck passing;
11. build passing;
12. honest E2E skip;
13. R-1 untouched;
14. no Git staging or commit.

---

## 9. Informational findings

These are recorded for accuracy. None is a defect and none requires follow-up
engineering work.

### 9.1 Attribution caveat

```text
INFORMATIONAL
```

TASK-227 and TASK-228 remain uncommitted in the shared `standalone-web-smoke.mjs`
working tree. Attribution therefore rests on:

- task tags in individual hunks;
- modification-time isolation;
- direct diff inspection.

The evidence is consistent and unambiguous, but it is weaker than the baseline
separation a committed TASK-227 state would provide. This is an artifact of
TASK-224's deferred integration handling, not of TASK-228.

### 9.2 Test fixture completeness

```text
INFORMATIONAL
```

Some CardCast transient captions are supplied directly as predicate inputs
rather than all being sourced dynamically from `BattleScene.ts`. Independent
verification confirmed those fixture strings match the production caption
vocabulary; the settled/transient *grammar* under test (`: accepted` /
`: rejected` versus `in flight…` / `not sent:` / `unavailable:`) is what the
predicate is defined against.

### 9.3 Task-record debt

```text
INFORMATIONAL — NOT RETROACTIVELY CHANGED
```

TASK-227 and TASK-228 completion records were both absent during the audit.
**This record is TASK-228's required completion artifact.**

**TASK-227's missing record remains pre-existing record debt.** TASK-228 does
**not** create it, does not backfill it, and does not modify TASK-227 in any way.
Creating a record on TASK-227's behalf would misattribute authorship of a
completed task and would misstate what V-1 verified.

---

## 10. Explicit non-scope

### R-1

The Phase 6 first cast still has **no retry loop** when `isInputLocked` prevents
the click. This remains a separate robustness observation. It was **not fixed,
not reclassified, and not expanded into TASK-228**.

### TASK-227 / V-1

TASK-227 remains **historically immutable**. Its Phase 6b/6d fix was not
modified by TASK-228. V-1's stale-caption protections were re-verified as
intact (§6), not re-implemented.

### Production behavior

No production change was necessary or authorized. The defect was in the test's
definition of "acknowledged", not in the product.

---

## 11. Ownership / commit note

- `standalone-web-smoke.mjs` is **shared work**. It carries changes from
  TASK-226, TASK-227, and TASK-228, and TASK-228's edit to it cannot be
  separated out as a whole-file slice. The TASK-228 hunk is the Phase 6 wait
  predicate only.
- `CastFeedbackWaitPredicate.test.ts` is an **untracked new file** that now
  contains both TASK-227's and TASK-228's guard assertions.
- **TASK-224 remains the authority for deferred integration/commit handling.**
  This record does not alter, pre-empt, or extend TASK-224's integration plan.

Creating TASK-228 does **not** authorize staging or committing anything, and
**no Git operation was performed** by this task.

---

## 12. Final status

```text
TASK-228 — DONE
V-2 — RESOLVED
Production behavior — UNCHANGED
V-1 — PRESERVED
R-1 — SEPARATE / UNTOUCHED
Live E2E — NOT RUN (environment unavailable)
Deterministic verification — PASS
Git staging/commit — NOT PERFORMED
```

No further implementation is required for V-2 under TASK-228.
