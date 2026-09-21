# TASK-004A — Define Committed Swap State Contract

---

## Metadata

```text
Task ID:           TASK-004A
Type:              ARCHITECTURE / STATE CONTRACT (documentation + minimal code)
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     backend
Supporting Agents: gameplay, review, testing
Workflow:          architecture/adr-change.md, gated by
                   development/feature.md and documentation/documentation-change.md
Skills:            documentation-consistency, architecture-conformance,
                   authority-determinism-audit, test-scenario-generation
Dependencies:      TASK-003 (Swap Validation, DONE) — which deferred this
                   boundary and reported it
```

---

## Objective

Resolve the `STALE_ACTION` state-contract contradiction identified before
TASK-004: `MATCH3_RULES.md` §2.1.2 mandates an already-applied / idempotency
check as check 3 of the Swap validation order, and §2.1.4 item 2 defines its
input as "the pair most recently committed to the board", but no documented
`BattleState` field could represent that value.

This task defines the owning state, its representation, its lifecycle, its
serialization, and its protocol/persistence position — and nothing else. It does
not implement swap execution.

---

## The Contradiction Being Resolved

```text
MATCH3_RULES.md §2.1.2   "all checks pass before the swap is committed"
                          check 3 = already applied / idempotency
        ↓
MATCH3_RULES.md §2.1.4 item 2
                          "the pair most recently committed to the board"
        ↓
GAME_STATE.md §2         BattleState had no such member
GAME_STATE.md §2.0.5     Board Foundation State had none
GAME_STATE.md §2.1       BoardState is Cells[64] and only Cells[64]
                         (§2.1.2 item 1), and a swap leaves no trace in it
GAME_STATE.md §3         ResolutionContext is transient and discarded
```

Both escape routes were already closed by existing documentation, which is what
made this a genuine contradiction rather than a gap to fill by inference:

- `MATCH3_RULES.md` §2.1.1 item 3 forbids the request carrying a gameplay field.
- `MATCH3_RULES.md` §2.1.4 items 1 and 5 and `SIGNALR_PROTOCOL.md` §2 item 1
  all state `clientSequence` is **not** the staleness source.

---

## Contract Decision

All ten required items were derivable from existing documentation. No
interpretation was invented and no STOP condition fired.

### Authoritative owner

`BattleState` (`GAME_STATE.md` §2). Not `BoardState` — §2.1.2 item 1 fixes
`BoardState` at exactly one field, and more fundamentally a swap is not
observable in the board at all: after an exchange the board is precisely what it
would be had the cells always been in those positions, so `Cells[64]` cannot
distinguish "exchanged once" from "exchanged back". Not the request — §2.1.1
item 3. The field is declared in §2 because §0 item 5 requires a field added by
a later stage to be a field §2 already declares.

### Representation

```text
BattleState.LastCommittedSwapPair?
    ── (MinCellIndex, MaxCellIndex)   both §1.0 indices, MinCellIndex < MaxCellIndex
```

No `actionId`, `requestId`, `clientSequence`, `timestamp`, `retryCount`,
`generation`, `Turn`, history list, or queue — §2.1.10 item 11 records that
§2.1.4 item 3 defines no per-Turn queue, so the one pair is the whole of it.

### Canonical ordering

`min`/`max`, because `MATCH3_RULES.md` §2.1.1 item 2 makes the **unordered**
pair `{from, to}` the swap's identity and §2.1.3 item 3 makes the two inputs
symmetric. `(13, 12)` and `(12, 13)` record the same value. Request order is
never the stored identity.

### Initial value

Absent. No sentinel — not `(0, 0)`, not `(-1, -1)`, not a defaulted `(0, 1)`.
`(0, 0)` is not representable at all, since a committed pair always names two
distinct cells. This matches the absent-`SpecialGem` convention of §2.1.7
item 3. Battle creation leaves it absent because board generation commits no
Swap (§2.0.5.2 item 1).

### Successful swap update

Set to the action's canonical pair, written in the same single post-resolution
write-back as `Turn` and `Sequence` (§5.1, §2.1.10 item 5). Applying a new pair
replaces the stored value.

### Invalid request

Unchanged, for every rejection reason. A rejected action writes nothing
(§2.1.5). It neither records a pair nor **clears** one, so an unrelated
rejection can never make an earlier commit forgettable (§2.1.10 items 6–7).

### STALE_ACTION

Reject when the action's canonical pair equals the stored pair and the record is
present. While the record is absent, check 3 can never fail (§2.1.10 item 4).
The state is not changed by the rejected request. Derived purely from
authoritative state — never from `clientSequence`.

### Serialization

Part of serialized `BattleState` (§2.1.7 item 1). Written with
`MinCellIndex < MaxCellIndex` so the serialized order is itself canonical;
**omitted** when absent, matching §2.1.7 item 3. Round-trip losslessness
(§2.1.7 item 5) extends to it, including round-tripping absence as absence.

### SignalR

**Not delivered.** `SIGNALR_PROTOCOL.md` §4 item 4 admits no field beyond the
implemented `GAME_STATE.md` §2.0 stage's, and §4 item 12 now states this
explicitly. No new message, method, subscription, or payload member. This is not
a missing field: §2.1.4 makes staleness a server-side decision the client cannot
participate in, so the client has no use for the value and must never author,
adjust, or recompute it.

### Redis

No new key, no Redis-only field, no schema change, and no persistence
implemented. `REDIS_STATE.md` §7 item 11 records that the member travels in the
existing `battle:{battleId}:state` record when persistence becomes required
(§7 item 7), and that the staged deferral of §7 items 1–4 is unchanged. It is
not a concurrency token — `Sequence` remains the only one.

---

## ADR

**ADR-010 — Committed-Swap State for Idempotent Swap Rejection** (`Accepted`),
created per `AGENTS.md` §18 and `.ai/workflow/architecture/adr-change.md`. This
is an authoritative-state-model change, which `docs/03-decisions/README.md` §2
lists as ADR-worthy. Status is `Accepted` because the decision is genuinely
established in `docs/02-technical/GAME_STATE.md` §2.1.10 by this task
(`README.md` §5). Five rejected alternatives are recorded: storing it in
`BoardState`, using `clientSequence`, comparing against `Turn`, storing action
history or an action id, and re-simulating the swap.

---

## Changes

### Documentation

- `docs/02-technical/GAME_STATE.md` — **the owning document.** Version 1.4.
  Added `LastCommittedSwapPair` to the §2 field tree, to the §2.0.5 stage tree
  and its §2.0.5.1 field list; added **§2.1.10** (13 items) defining the owner,
  canonical ordering, absent initial value, write timing, rejection behaviour,
  non-delivery, serialization, and what the model does not add; extended the
  §5.1 write-back diagram and added §5.1 item 7; corrected the now-stale
  "unchanged in count" claim in §2.1.9 item 2.
- `docs/01-game-design/MATCH3_RULES.md` — Version 1.4. §2.1.2 check 3 promoted
  to an explicit item naming the state it reads; §2.1.4 item 2 names
  `BattleState.LastCommittedSwapPair` as the record and states the comparison is
  between unordered pairs; §2.1.5 item 5 and §2.1.6 step 4 name the field;
  §8.3 notes the record travels in the same write-back. **No gameplay rule
  changed** — the rule already existed; only its state owner is now named.
- `docs/02-technical/SIGNALR_PROTOCOL.md` — §4 item 12 added, stating the record
  is authoritative state that is deliberately not delivered, and why that needs
  no protocol change.
- `docs/02-technical/REDIS_STATE.md` — Version 1.3. Corrected §7 item 8's
  now-false "introduces no new `BattleState` field"; added §7 item 11 for the
  field's persistence boundary.
- `docs/03-decisions/ADR/ADR-010-committed-swap-state.md` — new.
- `docs/03-decisions/README.md` — ADR-010 added to the §7 index.

### Code

- `src/backend/GameServer.Domain/Match3/CommittedSwapPair.cs` — **new.** The
  canonical unordered pair. `FromCells` establishes `min`/`max` order; the
  constructor enforces the invariant for deserialization; `Matches` expresses
  the §2.1.4 item 2 comparison as a named operation.
- `src/backend/GameServer.Domain/Battle/BattleState.cs` — one optional field,
  `CommittedSwapPair? LastCommittedSwapPair = null`, appended so every existing
  positional construction site keeps working. `Create` leaves it absent.
- `src/backend/GameServer.Domain/Match3/SwapRejectionReason.cs` — added
  `StaleAction = 4`.

### Tests

- `tests/backend/GameServer.Domain.Tests/CommittedSwapPairContractTests.cs` —
  **new**, 32 focused tests.
- `tests/backend/GameServer.Domain.Tests/BattleStateTests.cs` and
  `CascadeAndDeterminismTests.cs` — updated the three field-set assertions that
  enumerate `BattleState`'s members, with comments recording why the new field
  is expected. No assertion was weakened: each still enumerates the exact set,
  and the Special Gem test still asserts that *that* stage added no field.

---

## Tests

```text
Focused:      Passed! - Failed: 0, Passed:  32, Total:  32
              (CommittedSwapPairContractTests)
Domain:       Passed! - Failed: 0, Passed: 408, Total: 408
Application:  Passed! - Failed: 0, Passed:  26, Total:  26
Infrastructure: Passed! - Failed: 0, Passed: 1, Total:   1
Api:          Passed! - Failed: 0, Passed:  18, Total:  18
Build:        Build succeeded. 0 Warning(s), 0 Error(s) (GameServer.sln)
              Total 453 passed, 0 failed
```

Coverage added, mapped to the task's minimum:

- **initial state has no committed pair** — `Create` leaves it absent; no
  sentinel is representable; every adjacent pair on a fresh board is not
  already-applied; absence round-trips as absence.
- **`(12,13)` canonicalizes to `(12,13)`** and **`(13,12)` canonicalizes to
  `(12,13)`** — plus identity for both orders over all 112 adjacent pairs, and a
  guard that a non-canonical construction is rejected.
- **successful commit records the canonical pair** — including regardless of
  request order, replacing the previous pair, and dragging no board/counter/RNG
  change along with it.
- **invalid action does not replace the pair** — for all three non-stale
  rejection reasons; also that a rejection does not *clear* it, and that a
  rejection on a fresh battle fabricates no commit.
- **same unordered pair identifies `STALE_ACTION`** — both orders match, a
  different pair does not, a stale rejection leaves the state unchanged,
  `clientSequence` is not a source (the request type carries only the two
  cells), and the outcome is deterministic.
- **serialization** — canonical ascending order on the wire, omission when
  absent, lossless round trip, absence preserved.
- **ownership** — the field is on `BattleState`, is not on `BoardState`, and the
  pair type declares no version/history/queue member.

No swap-execution test was added. `RecordCommit`/`ApplyRejection` in the test
file move only the commit record and are documented as such; they do not
simulate move application, `Turn`, `Sequence`, or board resolution.

---

## Scope Verification

- Swap execution (`SwapExecutor`, move application, `Turn++`, `Sequence++`):
  **NOT IMPLEMENTED**
- Board resolution / `MatchDetector` integration: **NOT IMPLEMENTED**
- Cascade: **NOT IMPLEMENTED**
- Special Gem activation: **NOT IMPLEMENTED**
- Gravity / Spawn: **NOT IMPLEMENTED**
- Combat / Power / Passive / Relic / Boss: **NOT IMPLEMENTED**
- Frontend gameplay: **NOT IMPLEMENTED**
- Redis runtime integration: **NOT IMPLEMENTED**
- New SignalR message/method/payload member: **NOT INTRODUCED**

Verified by search: no `SwapExecutor`, `ExecuteSwap`, or counter-increment code
exists in `src/`. The only implementation change to existing behavior is the
added optional field and enum member; no existing gameplay behavior was changed.

---

## Documentation Impact

Required, and performed. This task *is* a state-contract change, so the
owning document (`GAME_STATE.md` §2.1.10) defines the contract and the
referencing documents cite it rather than restating it
(`.ai/skills/quality/documentation-consistency.md` step 3):

```text
GAME_STATE.md §2.1.10          owns the state contract
MATCH3_RULES.md §2.1.2/.4/.5   owns the rule; now names its state owner
SIGNALR_PROTOCOL.md §4         records the deliberate non-delivery
REDIS_STATE.md §7              records the persistence boundary
ADR-010                        records why this placement was chosen
```

No gameplay rule was changed: `MATCH3_RULES.md` §2.1.4 item 2 already required
this check; TASK-004A supplies the state it reads. The `GAME_RULES.md` §20 Rule
Change Policy therefore does not apply, and no game-design decision was made.

---

## Remaining Issues

- **Pre-existing uncommitted work** is present in the working tree from earlier
  tasks (documentation updates, frontend runtime files, Board Foundation and
  Special Gem sources and tests, and the untracked `tasks/` files). It was not
  touched by this task beyond the three test files named above. Noted so the
  diff is not misread — same note as TASK-003.
- **The Api wire type cannot be asserted from the Domain test project.**
  `SIGNALR_PROTOCOL.md` §4 item 12 requires `BattleStateUpdated` to omit the
  field, but `BattleStateUpdated` lives in `GameServer.Api` and is not referenced
  by `GameServer.Domain.Tests`. The test asserts the Domain-side half of the
  contract (the record is state; the request carries only the two cells). A
  future task touching the Hub should add the Api-side assertion.
- **`grep` confirms only one consumer of the value exists so far** — the
  `SwapValidator` XML doc that deferred it. TASK-004 must now implement check 3
  in its documented position (third, before match-producing) and set the record
  at §8.3 step 3–4.

---

## Handoff

`STALE_ACTION` now has an explicit authoritative owner. TASK-004 is unblocked
and can implement the documented four-check order exactly as written:

```text
1. index range and distinctness   → INVALID_CELL_INDEX
2. adjacency                      → INVALID_SWAP
3. already applied                → STALE_ACTION   (reads LastCommittedSwapPair)
4. match-producing                → NO_MATCH_FROM_SWAP
        ↓
commit: board exchanged, record written, Turn begins (§8.3 step 4)
        ↓
Sequence +1 after the board is stable (§8.3 step 8)
```

TASK-004 must also update `SwapValidator`'s class documentation, which
currently states that check 3 "is not part of this operation" because no state
existed for it — that justification is now obsolete.

---

## Status

DONE