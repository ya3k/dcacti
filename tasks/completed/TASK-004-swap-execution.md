# TASK-004 — Swap Execution

---

## Metadata

```text
Task ID:           TASK-004
Type:              FEATURE (gameplay / vertical slice)
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: backend, realtime, testing, review
Workflow:          development/feature.md
Skills:            gameplay-behavior-derivation, authority-determinism-audit,
                   test-scenario-generation, implementation-review,
                   scope-validation, realtime-protocol-validation
Dependencies:      TASK-003 (Swap Validation, DONE), TASK-004A (Committed Swap
                   State Contract, DONE) — which unblocked this task by defining
                   the state the already-applied check reads
```

---

## Objective

Implement the server-authoritative execution of a validated Match-3 swap, using
the committed-swap state contract TASK-004A established:

```text
SwapRequest
    → SwapValidator
    → valid
    → exchange complete Cells
    → resolve the board through the existing documented resolution pipeline
    → update LastCommittedSwapPair / Turn / Sequence per the authoritative
      contracts
    → final authoritative BattleState
```

The task is a vertical slice and owns the swap transition only. It does not
become a general battle Turn system.

---

## Implementation

### Domain — `SwapExecutor`

`src/backend/GameServer.Domain/Match3/SwapExecution.cs` — **new**, containing
`SwapExecutor` and `SwapExecutionResult`.

`SwapExecutor.Execute(BattleState, SwapRequest)` is the single entry point. It
sequences four existing capabilities in the documented order and owns no rule of
its own:

```text
SwapValidator.Validate(board, LastCommittedSwapPair, request)   §2.1.2, 4 checks
    ├── rejected → SwapExecutionResult.Rejected(reason); state untouched §2.1.5
    └── accepted
            ├── BoardState.WithSwapped(from, to)                §2.1.6 step 4
            ├── CascadeResolver.Resolve(exchanged, rng, null)   §2.1.6 step 8, §4
            └── one `with` expression: BoardState, RngState, Turn,
                Sequence, LastCommittedSwapPair                  §8.3, §5.1
```

Reuse, with no duplication:

- **Validation** — `SwapValidator`, the existing gameplay validator.
- **Exchange** — `BoardState.WithSwapped`, the existing whole-entry primitive.
- **Resolution** — `CascadeResolver.Resolve` over the existing `MatchDetector`,
  `SpecialGemPlanner`, `SpecialGemEffects`, `Gravity`, and `Spawn`.
- **Committed pair** — `CommittedSwapPair.FromCells` for the canonical
  `(min, max)` form.

No second swap pipeline, resolver, detector, or validator was introduced. The
`Execute` surface is the only public member on the type.

### The single write-back

Every authoritative field is set in **one** `with` expression over the input
state, so no caller can observe a partially committed state:

```text
Board already swapped but LastCommittedSwapPair still old     → not representable
LastCommittedSwapPair updated but board unresolved            → not representable
```

`SwapExecutionResult` carries either the rejection (reason + no state) or the
commit (resolved state + the resolution's own `CascadeResult`). A rejection
carries **no** state — reading `State` on a rejection throws — so a rejection
cannot be mistaken for a commit that produced an empty board.

### Turn and Sequence

`MATCH3_RULES.md` §8.1 states a Turn **begins** when a Swap is committed; it does
not say the stored number advances. §8.3 places "begin the Turn" as step 4
*before* the resolution, §8.4 holds `Turn` "in effect" (not written) through every
resolution step, and §8.3's counter write is step 8–9 of the single write-back.
The stored values are therefore advanced exactly once, in the same `with`
expression, after the board is stable — `Turn + 1` and `Sequence + 1`. Neither
advances per Match, per pass, per Cascade, or per Special Gem. A rejected request
advances neither.

### Stale action

`SwapValidator` now performs all four `§2.1.2` checks in the documented order,
including check 3:

```text
1. index range and distinctness   → INVALID_CELL_INDEX
2. adjacency                      → INVALID_SWAP
3. already applied (§2.1.4)       → STALE_ACTION       (reads LastCommittedSwapPair)
4. match-producing                → NO_MATCH_FROM_SWAP
```

The validator takes the committed pair as an input (`CommittedSwapPair?`) and
**only compares** it — the record's write path is the executor's. The comparison
uses `CommittedSwapPair.Matches`, so it is between **unordered** pairs: with
`(12, 13)` committed, both `(12, 13)` and `(13, 12)` are stale, while `(13, 14)` is
not. A `null` record never matches, so check 3 can never fail on a fresh battle.

The check is fully server-authoritative: it reads only
`BattleState.LastCommittedSwapPair`. `clientSequence` is accepted by the hub and
deliberately ignored, and the request type still carries exactly the two cells.

### Application — `BattleStateService.ExecuteSwap`

`ExecuteSwap(battleId, SwapRequest)` delegates to `SwapExecutor.Execute` and
records the result:

- **Rejected** → the registry keeps the state it already held; the state is never
  replaced with a partial one.
- **Committed** → the resolved state replaces the previous one in a single
  assignment; the value already carries the consistent board, RNG, counters, and
  record.

It implements no game rule — sequencing and recording only
(`ARCHITECTURE.md` §2.1, §4.1). Unknown battle → `null`, consistent with
`GetBattle` and `ResolveBoard`.

### API — `BattleHub.Swap`

```text
Swap(battleId, fromCell, toCell, clientSequence)  →  SwapResponse(Accepted, Reason)
```

The hub translates and delegates; it decides nothing and inspects no board. On
acceptance the resolution has already completed and been written back, so it
pushes the resulting state to the battle group through the **existing**
`BattleStateUpdated` path before returning the acknowledgement. On rejection it
returns the §5 acknowledgement and pushes nothing.

`SwapResponse` is the `SIGNALR_PROTOCOL.md` §5 shape. The rejection code is
produced by `SwapRejectionCodes.ToContractCode`, so the wire carries the
documented `STALE_ACTION` / `INVALID_CELL_INDEX` spellings rather than a cased
C# identifier (a mechanical translation would yield `STALEACTION`, a code the
protocol never defines).

This is the minimal compatible change §13 of the task permits: no new SignalR
message, method, subscription, or payload member. `LastCommittedSwapPair` is
**not** projected onto the wire — it appears in `BattleHub.cs` only inside a
documentation comment explaining why it is absent.

### Correction to obsolete documentation

`SwapValidator`'s class documentation stated that the `STALE_ACTION` check "is
not part of this operation … no such value exists in the documented state at this
layer". TASK-004A made that justification obsolete by establishing
`BattleState.LastCommittedSwapPair`. The class, both overloads, and
`ProducesMatch` were corrected. `SwapValidator` was **not** redesigned: it gained
the committed-pair input its own documented check requires and nothing else.

---

## Validation

The swap resolves through the `SwapValidator` **first**, before any mutation: the
exchange is not performed until all four checks pass, so no partial execution is
possible.

For `INVALID_CELL_INDEX`, `INVALID_SWAP`, `STALE_ACTION`, and
`NO_MATCH_FROM_SWAP` the authoritative state is verified unchanged across:

```text
BoardState            identical, entry for entry (Gem type + Special Gem + orientation)
Turn                  unchanged
Sequence              unchanged
LastCommittedSwapPair unchanged — never cleared
RngState              unchanged
RngSeed               unchanged
```

Check ordering is verified: a non-adjacent pair that is *also* the committed pair
reports `INVALID_SWAP` (check 2 precedes check 4), and a stale pair that would
also produce no match reports `STALE_ACTION` (check 4 precedes check 5).

---

## State Transition

```text
LastCommittedSwapPair:
    absent (initial)         → canonical (min, max) of the exchanged pair
    a previous pair P        → replaced by the new pair (only the latest is kept)
    any rejection            → unchanged; never cleared
    (13, 12) and (12, 13)    → record the identical value (12, 13)
    written in the same single post-resolution write-back as Turn/Sequence

Turn:
    N                        → N + 1, exactly once per committed Swap
    any rejection            → unchanged

Sequence:
    N                        → N + 1, exactly once, after the board is stable
    any rejection            → unchanged
```

---

## Board Resolution

The existing pipeline is reused unchanged and is reached only after the exchange
is committed:

```text
CascadeResolver.Resolve(exchanged, new Pcg32(RngState...), swapOriginIndex: null)
    ├── MatchDetector             §3
    ├── SpecialGemPlanner          §5.5.1–§5.5.4
    ├── SpecialGemEffects          §5.5.5, §5.8
    ├── Gravity                    §4.4
    └── Spawn                      §4.5
        → stable board, retained RngState
```

The resolution terminates naturally when a detection pass finds no Match
(`§4.3 item 1`); the committed board is verified stable by a fresh detection
pass. No cascade cap, no parallel algorithm, and no change to Special Gem
semantics was introduced.

**RNG.** Validation draws nothing and the raw exchange draws nothing. Only the
resolution's Spawn step advances `RngState`, by exactly one selection per spawned
cell. No new consumption, no reseed, and no second mechanism was added; the
committed `RngState` is asserted equal to what an independent replay of the same
deterministic resolution reaches.

---

## Tests

```text
Focused (SwapExecutionTests):   Passed! - Failed: 0, Passed:  55, Total:  55
Domain:                          Passed! - Failed: 0, Passed: 463, Total: 463
Application:                     Passed! - Failed: 0, Passed:  37, Total:  37
Infrastructure:                  Passed! - Failed: 0, Passed:   1, Total:   1
Api:                             Passed! - Failed: 0, Passed:  25, Total:  25
Frontend (Vitest):               Passed! - Failed: 0, Passed: 169, Total: 169
Build:                           Build succeeded. 0 Warning(s), 0 Error(s)
                                 (GameServer.sln)
                                 Backend total: 526 passed, 0 failed
```

Baseline before this task was 453 backend tests passing; the suite grew by
exactly the 73 added tests. No existing test was weakened, skipped, or deleted —
the only edits to existing tests were the TASK-003 call sites updated for the
validator's new signature, and the two assertions that correctly listed `Swap`
as an unimplemented Hub method. All Special Gem tests continue to pass.

### Coverage added

- **`tests/backend/GameServer.Domain.Tests/SwapExecutionTests.cs`** — **new**, 55
  focused tests:
  - *Validation integration* — valid swap; invalid index (`-1`, `64`,
    `int.MaxValue`); same cell; non-adjacent (diagonal, row-wrap `7↔8`, distant);
    `NO_MATCH_FROM_SWAP`; `STALE_ACTION`; the documented check **order**; the
    request/indices overload equivalence; null-state guard.
  - *Successful swap* — the task's exact Cell exchange example
    (`12 = POWER + Burst`, `13 = ATK + null` → mirrored); exchange through the
    documented `WithSwapped` primitive (equality with `CascadeResolver` on the
    primitive's own output); a Special Gem moving with its cell; every
    `SpecialGem` type and both `LineClear` orientations preserved; all 62
    unrelated cells unchanged; no Special Gem activated merely by being swapped.
  - *State* — canonical record for `(33,25)`; identical record for either request
    order; absent → committed; replacement of a prior pair; `Turn` +1 exactly
    once; `Sequence` +1 exactly once; one increment per swap over six consecutive
    committed swaps; all fields written together; input state not mutated;
    rejection exposes no state.
  - *Invalid request* — every rejection reason leaves board, `Turn`, `Sequence`,
    `RngState`, `RngSeed`, and `LastCommittedSwapPair` unchanged; no clearing of
    the record; a different pair still valid after a commit.
  - *Stale action* — `(12,13)` and `(13,12)` both stale; whole state unchanged;
    a different pair not stale; never fires on a fresh battle (exhaustive over
    all 112 adjacent pairs); end-to-end replay after a real commit; determinism;
    server-authoritative (the request type exposes exactly `From`/`To`, and the
    executor's inputs are exactly `BattleState` + `SwapRequest`).
  - *Resolution integration* — reaches the existing pipeline; stable full board;
    stability over every accepted pair of a generated board; Special Gems created
    by the resolution reach the committed board; a Match 4 fixture creating a
    `LineClear` with an orientation.
  - *RNG* — zero consumption for rejected swaps; no reseed; the resolution's own
    `RngState`; full determinism over 20 runs.
  - *Boundary* — `Execute` is the type's only public method; the four §5 wire
    codes are the documented spellings and are not cased member names.
- **`tests/backend/GameServer.Application.Tests/BattleStateServiceTests.cs`** —
  +11 tests: rejection leaves the recorded state unchanged; commit replaces it;
  `Turn`/`Sequence` +1 once; canonical record; replay rejected as stale with the
  state untouched; unknown battle → `null`; empty id rejected; board stable and
  consistent with the resolution; no event surface; no persisted field.
- **`tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs`** — +8 tests: the
  §5 acceptance result; the resolved state pushed through the **existing**
  `BattleStateUpdated` path with `turn`/`sequence` = 1 and 64 board cells; no new
  payload field and no `lastCommittedSwapPair` anywhere in the payload;
  rejection returns the code, changes nothing, and pushes nothing;
  `STALE_ACTION` for both spellings of a replayed pair; `clientSequence` never
  decides acceptance or staleness; an unknown battle creates no state.

No resolution test from TASK-002 was rewritten; only the integration coverage
needed to prove `Swap → Resolution → Stable Board` is correctly connected was
added.

---

## Files Changed

Created:

- `src/backend/GameServer.Domain/Match3/SwapExecution.cs` — `SwapExecutor` +
  `SwapExecutionResult`.
- `tests/backend/GameServer.Domain.Tests/SwapExecutionTests.cs` — 55 focused tests.
- `tasks/completed/TASK-004-swap-execution.md` — this report.

Modified:

- `src/backend/GameServer.Domain/Match3/SwapValidator.cs` — corrected the
  obsolete class/method documentation; the two `Validate` overloads now take
  `CommittedSwapPair?` and evaluate the documented check 3 in its documented
  position. No redesign.
- `src/backend/GameServer.Domain/Match3/SwapRejectionReason.cs` — added
  `SwapRejectionCodes.ToContractCode`, the documented §5 wire spellings.
- `src/backend/GameServer.Application/Battle/BattleStateService.cs` — added
  `ExecuteSwap`; corrected the `ResolveBoard` scope note that claimed the Swap
  path was unimplemented.
- `src/backend/GameServer.Api/Hubs/BattleHub.cs` — added `Swap` and
  `SwapResponse`; corrected the class note that listed `Swap` as unimplemented.
- `tests/backend/GameServer.Domain.Tests/SwapValidationTests.cs` — call sites
  updated for the new validator signature via a local
  `Validate(board, from, to)` helper that passes `null` (no commit); one new
  assertion of the request/indices overload equivalence retained.
- `tests/backend/GameServer.Application.Tests/BattleStateServiceTests.cs` — +11 tests.
- `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs` — +8 tests; `Swap`
  removed from the not-registered gameplay methods list.

---

## Documentation

- **`SwapValidator`'s obsolete class documentation is corrected** (the task's
  explicit requirement): the claim that check 3 "is not part of this operation"
  because no state existed for it is replaced by the check's actual contract, and
  the class now records that it **reads** the committed pair while the executor
  **writes** it.
- **No gameplay rule was changed.** `MATCH3_RULES.md` §2.1.2, §2.1.4, §2.1.6, and
  §8 already specified everything implemented; TASK-004A had already named the
  state owner. No `docs/` file was modified.
- **No unnecessary documentation change** was made: no `docs/` edit, no ADR, and
  no `GAME_STATE.md` / `SIGNALR_PROTOCOL.md` / `REDIS_STATE.md` change. §14 of the
  task forbids new Redis persistence, and the deferred boundary in
  `REDIS_STATE.md` §7 item 8/11 already covers the Match-3 resolution stage
  without edit.
- **Documentation consulted:** `GAME_RULES.md` §1, §2, §17, §18; `MATCH3_RULES.md`
  §1.0, §1.4, §2, §2.1.1–§2.1.6, §3, §4.1–§4.6, §5.5.3, §5.5.5, §7.1–§7.2, §8.1–§8.4;
  `GAME_STATE.md` §2, §2.0.5, §2.1.1–§2.1.10, §2.6.2, §3, §5, §5.1; `GAME_EVENTS.md`
  §1, §1.1–§1.3; `SIGNALR_PROTOCOL.md` §0, §1–§5, §6, §8; `ARCHITECTURE.md` §1,
  §2.1, §4, §4.1, §5; `REDIS_STATE.md` §4, §7; `MVP_SCOPE.md` §1–§4;
  `ADR-001`, `ADR-009`, `ADR-010`; `AGENTS.md`, `docs/AGENTS.md`, the `.ai/workflow/`
  and `.ai/skills/` documents named in the metadata.

---

## Scope Verification

```text
Cards:                        NOT IMPLEMENTED
Passive:                      NOT IMPLEMENTED
Relics:                       NOT IMPLEMENTED
Combat:                       NOT IMPLEMENTED
Boss:                         NOT IMPLEMENTED
Damage:                       NOT IMPLEMENTED
Power:                        NOT IMPLEMENTED
Five Elements:                NOT IMPLEMENTED
Rewards:                      NOT IMPLEMENTED
Redis persistence:            NOT IMPLEMENTED — no key, field, or token added
New SignalR protocol:         NOT IMPLEMENTED — no new message/method/payload
Frontend gameplay:            NOT IMPLEMENTED
Phaser gameplay:              NOT IMPLEMENTED
PvP:                          NOT IMPLEMENTED
Map mechanics:                NOT IMPLEMENTED
General battle Turn system:   NOT IMPLEMENTED — the slice owns one Swap only
```

Also **not modified**: `BoardState`, `SpecialGem`, `MatchDetector`,
`SpecialGemPlanner`, `SpecialGemEffects`, `Gravity`, `Spawn`, `CascadeResolver`,
`Pcg32`, and every frontend file. Verified by search: the only new Domain type is
`SwapExecutor`; no gameplay system from the forbidden list appears in `src/`
outside documentation comments; the hub's only `SendAsync` targets remain
`RuntimeStatusChanged` and `BattleStateUpdated`.

`LastCommittedSwapPair` is **not exposed through SignalR**: the Api-side tests
assert the pushed payload's field set is exactly
`{battleId, board, rngSeed, rngState, sequence, turn}` and that the serialized
payload contains no `lastCommittedSwapPair`.

---

## Risks / Handoff

- **`REDIS_STATE.md` §7 item 8's "not yet persisted" boundary is unchanged.** The
  resolved state lives only in `BattleStateService`'s process-local registry, which
  is the same staged, safe-to-lose boundary the service already held and is
  explicitly not Redis persistence. This is a documented deferral, not an omission.
- **Combo and Match count are not produced.** They belong to `PlayerState`
  (`GAME_STATE.md` §2.2), which does not exist yet. `MATCH3_RULES.md` §6.6's Combo
  timing and §2.1.6 steps 6–7 therefore remain unimplemented, as does Battle Event
  emission (`SwapStarted`/`SwapResolved`). A later Combo/event task must add them;
  this task did not fabricate a home for them.
- **Special-resource generation hooks** (`MATCH3_RULES.md` §5.7) are likewise
  unimplemented — they require the Combat/resource model. `PassResult` already
  reports `ClearedCellUnion`, which is the documented input, so the hook has a
  place to land.
- **`ResolveBoard` and `ExecuteSwap` are two entry points.** `ResolveBoard`
  resolves a board with no Swap behind it (no validation, no exchange, no commit,
  counters unchanged); `ExecuteSwap` is the documented §2.1.6 Swap sequence. There
  is one resolution pipeline — both call `CascadeResolver` — so this is not a
  duplicated implementation. `ResolveBoard` is now exercised only by the existing
  tests and has no production caller after `ExecuteSwap` exists; whether it should
  be retired is a follow-up question, deliberately not decided here.
- **Pre-existing uncommitted work** from earlier tasks remains in the working
  tree. It was not touched beyond the files listed above. Noted, as TASK-003 and
  TASK-004A also noted, so the diff is not misread.
- **No contradiction was found** between the TASK-004A contract and the existing
  execution rules, so no STOP condition fired. `MATCH3_RULES.md` §8's Turn rule
  needed no reinterpretation: §8.1 states the Turn *begins* on commit, §8.4 keeps
  the stored value unwritten through the resolution, and §8.3 writes it once — so
  "Turn increments exactly once" and "§8.3" agree without amendment.

---

## Handoff

Swap execution is complete and verified. `SwapExecutor.Execute` is the Domain
entry point; `BattleStateService.ExecuteSwap` is the Application boundary; and
`BattleHub.Swap` is the transport path.

The next task — Battle Events (`SwapStarted`/`SwapResolved`, `MatchCreated`,
`MatchResolved`, `CascadeCreated`, `GemMatched`, `ComboChanged`) — starts from the
`SwapExecutionResult` this task produces: `Resolution.Passes` already carries each
pass's matches, created Special Gems, activations, and cleared cell unions in the
documented order, which is the event stream's own ordering contract
(`GAME_EVENTS.md` §1.1). Combo requires `PlayerState` (`GAME_STATE.md` §2.2) to
exist first.

---

## Status

DONE