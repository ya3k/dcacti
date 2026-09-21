# TASK-005 — Match / Combo Accounting

---

## Metadata

```text
Task ID:           TASK-005
Type:              GAMEPLAY / STATE IMPLEMENTATION
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     backend
Supporting Agents: gameplay, realtime, client, review, testing
Workflow:          development/gameplay-change.md
Skills:            gameplay-behavior-derivation, authority-determinism-audit,
                   architecture-conformance, documentation-consistency,
                   realtime-protocol-validation, test-scenario-generation,
                   impact-analysis
Dependencies:      TASK-005A (Player Battle Progression State Contract, DONE)
                   — the authoritative contract this task implements
                   TASK-004  (Swap Execution, DONE) — the pipeline extended
```

---

## Objective

Implement the `PlayerState.MatchCount` + `PlayerState.Combo` accounting contract
that TASK-005A fixed, inside the existing committed-Swap pipeline, reusing the
existing Match-3 resolution output and modifying no resolver.

---

## Discovery — What Was Read

```text
docs/00-overview/     MVP_SCOPE.md
docs/01-game-design/  GAME_RULES.md (§3, §4, §5, §18)
                      MATCH3_RULES.md (§2, §2.1.1–§2.1.6, §3, §3.1–§3.4, §4,
                                      §4.1–§4.3, §6 in full, §8, §8.1–§8.4)
docs/02-technical/    GAME_STATE.md (§0, §1, §2, §2.0–§2.0.5.3, §2.1.7,
                                      §2.1.10, §2.2, §3, §5, §5.1–§5.3)
                      GAME_EVENTS.md (§1, §1.1, §2, §3)
                      SIGNALR_PROTOCOL.md (§0, §1.2, §2, §2.1, §3, §3.1, §4,
                                          §4.1, §5, §8)
                      REDIS_STATE.md (§1–§4, §7)
                      ARCHITECTURE.md (§1, §2.1, §4.1, §5)
docs/03-decisions/    ADR-001, ADR-004, ADR-005, ADR-008, ADR-009, ADR-010
tasks/completed/      TASK-005A (the contract), TASK-004, TASK-004A, TASK-003
Existing implementation:
                      src/backend/GameServer.Domain/Battle/BattleState.cs
                      src/backend/GameServer.Domain/Match3/SwapExecution.cs
                      src/backend/GameServer.Domain/Match3/CascadeResolver.cs
                      src/backend/GameServer.Domain/Match3/ResolutionEvents.cs
                      src/backend/GameServer.Domain/Match3/* (resolver types)
                      src/backend/GameServer.Application/Battle/BattleStateService.cs
                      src/backend/GameServer.Api/Hubs/BattleHub.cs
                      src/frontend/client/src/services/realtime/SignalRService.ts
                      src/frontend/client/src/game/runtime/GameRuntime*.ts
                      src/frontend/client/src/state/GameRuntimeState.ts
                      every existing backend and frontend test suite
```

---

## Understanding

`GAME_STATE.md` §2.2 already owns `MatchCount` and `Combo` and §2 already nests
`PlayerState` inside `BattleState`; TASK-005A fixed the semantics, the reset
rule, the rejection rule, and the resolution source. Nothing about the contract
was open, so this task added no decision of its own.

The work was therefore three mechanical pieces and one integration:

1. a `PlayerState` type carrying exactly the two contractual fields;
2. `BattleState.PlayerState`, present from creation at `0`/`0`;
3. one counted walk over the resolution's own `Passes`, performed inside the
   existing single post-resolution write-back;
4. delivering the new stage field through the existing state push, because
   `SIGNALR_PROTOCOL.md` §4 item 4 makes the payload a one-to-one projection of
   the implemented `GAME_STATE.md` §0 stage.

---

## Relevant Documentation

The contract (TASK-005A, cited rather than re-derived) rests on:

```text
Owner           GAME_STATE.md §2, §2.2              PlayerState owns both fields
Match semantics GAME_RULES.md §3, §4; MATCH3_RULES.md §3, §6.2–§6.3
Combo semantics GAME_RULES.md §5;    MATCH3_RULES.md §6.1–§6.7
Reset           MATCH3_RULES.md §6.1 item 2, §6.4, §6.5
Rejection       MATCH3_RULES.md §2.1.5 item 5, §6.1 item 3; GAME_STATE.md §5.1 item 6
Write-back      GAME_STATE.md §5.1; MATCH3_RULES.md §8.3
Resolution      CascadeResolver.CascadeResult.Passes → PassResult.Matches
SignalR         SIGNALR_PROTOCOL.md §4 item 4, §4.1 item 1, §4.2 (new)
Redis           REDIS_STATE.md §7 items 1–4, 7, 8, 12 (new)
Events          GAME_EVENTS.md §3 item 7 (new); §1.1, §2 unchanged
```

---

## Plan

1. Add `GameServer.Domain/Battle/PlayerState.cs` — a two-field record with the
   documented initial values and the full contract in its XML documentation.
2. Add `BattleState.PlayerState` and set it to `PlayerState.Initial` in
   `BattleState.Create`.
3. Extend `SwapExecutor.Execute` with the accounting traversal and include the
   result in the existing single `with` write-back.
4. Extend the `BattleStateUpdated` projection with `playerState`, and update the
   client's payload types, runtime shape reading, and boundary tests.
5. Add focused tests: Domain (30), Application (4), API wire (2), frontend (3).
6. Update the four documents the change makes stale.
7. Run the full backend suite, the frontend suite, and both builds.

---

## Changes

### 1. `PlayerState` — new type

`src/backend/GameServer.Domain/Battle/PlayerState.cs`

```csharp
public readonly record struct PlayerState(int Combo, int MatchCount)
{
    public const int InitialMatchCount = 0;
    public const int InitialCombo = 0;
    public static PlayerState Initial => new(InitialCombo, InitialMatchCount);
}
```

- Exactly the two contractual fields. No `PlayerId`, `HP`, `Power`, `Pet`,
  `Cards`, `Relics`, `Status`, rewards, turn-local counters, queues, timestamps,
  or client fields — those are later stages (`GAME_STATE.md` §0 item 4).
- `int` for both, as the contract requires.
- It is a value type, so a state record cannot hold a `null` `PlayerState`.
- The two documented starting values are named constants rather than magic
  numbers, so the initializer and the accounting reset read as the rule.

### 2. `BattleState.PlayerState`

`src/backend/GameServer.Domain/Battle/BattleState.cs`

```csharp
public sealed record BattleState(
    string BattleId,
    int Turn,
    int Sequence,
    ulong RngSeed,
    RngState RngState,
    BoardState BoardState,
    PlayerState PlayerState,               // ← added
    CommittedSwapPair? LastCommittedSwapPair = null)
```

- Placed after `BoardState` and before `LastCommittedSwapPair`, so the existing
  fields keep their positions and the record stays source-compatible with the
  positional construction already in the tree.
- `BattleState.Create` passes `PlayerState.Initial`, so a new battle has
  `MatchCount = 0` and `Combo = 0` — no `null`, no sentinel, and no lazy
  creation during Swap execution.
- **No flat `BattleState.MatchCount` / `BattleState.Combo` exists.**
  `GAME_STATE.md` §2.2 is the owner and §0 item 5 forbids a second
  representation.

### 3. Accounting in the committed-Swap pipeline

`src/backend/GameServer.Domain/Match3/SwapExecution.cs`

```csharp
// after CascadeResolver.Resolve(...)
var playerState = AccountMatches(state.PlayerState, resolution);

var resolved = state with
{
    BoardState = resolution.Board,
    RngState = resolution.RngState,
    Turn = state.Turn + 1,
    Sequence = state.Sequence + 1,
    PlayerState = playerState,                    // ← added, same write-back
    LastCommittedSwapPair = CommittedSwapPair.FromCells(request.From, request.To),
};
```

```csharp
private static PlayerState AccountMatches(
    PlayerState previous,
    CascadeResolver.CascadeResult resolution)
{
    var combo = PlayerState.InitialCombo;   // §6.1 item 2 — reset on this Swap
    var matchCount = previous.MatchCount;   // §3 — cumulative, never reset

    foreach (var pass in resolution.Passes)
    {
        foreach (var match in pass.Matches)
        {
            _ = match;                      // one MatchResolution is one Match
            matchCount++;
            combo++;
        }
    }

    return new PlayerState(combo, matchCount);
}
```

This is the task's own pseudocode, kept literal. It is a `private static`
helper, so `SwapExecutor` still exposes exactly one public operation
(`Execute`) and no second entry point can run accounting on its own.

- **Reset placement.** The reset is the loop's starting value, reached only
  after `SwapValidator` accepted the request and after the board resolved.
  A rejected Swap returns before it (`MATCH3_RULES.md` §2.1.5 item 5).
- **Order.** `Passes` in order, `Matches` within each — the resolver's own
  deterministic order (§3.2 within a pass, §4.2 across passes). Nothing is
  re-sorted and no order is derived from a cell index, a Special Gem type, a
  cascade depth, or an animation.
- **Exclusions by construction.** Only `PassResult.Matches` is read.
  `ClearedCellUnion` (N cells ≠ N Matches), `ActivatedSpecialGems` (an
  activation, chain, or its cleared cells are not Matches — §5.5.5 item 8,
  §6.3.1), and `CreatedSpecialGems` (a consequence of an already-counted Match
  — §6.3 item 3) are never consulted. The terminating no-match pass is absent
  from `Passes` (§4.3 item 4), so it neither counts nor resets.
- **No wrong derivation.** `Combo` is not `Passes.Count`, not `CascadeDepth`,
  and not `CascadeDepth + 1`; `MatchCount` is not a cleared-cell total. Both
  were asserted against the resolution's own report in tests that specifically
  exercise several shapes in one pass.

### 4. SignalR projection and the client

`src/backend/GameServer.Api/Hubs/BattleHub.cs`

```csharp
public record BattleStateUpdated(
    string BattleId, int Turn, int Sequence, ulong RngSeed,
    RngStatePayload RngState, BoardPayload Board,
    PlayerStatePayload PlayerState);              // ← added

public record PlayerStatePayload(int Combo, int MatchCount);   // ← new
```

- Delivered because `SIGNALR_PROTOCOL.md` §4 item 4 admits exactly the
  implemented stage's fields, and `PlayerState` is now one of them. No new
  message, method, or subscription was introduced.
- `ToPayload` is still a pure one-to-one field mapping; the hub computes
  nothing.
- `LastCommittedSwapPair` remains **not** delivered (§4 item 12).
- The client mirrors the shape: `SignalRService.BattleStateUpdatedPayload` /
  `PlayerStatePayload`, `GameRuntimeEvents.RuntimeBattleState` /
  `RuntimePlayerState`, and `GameRuntime.readPlayerState` (which rejects a
  payload missing a value rather than defaulting one, since zero is a value
  here).

### 5. Files created and modified

**Created**

```text
src/backend/GameServer.Domain/Battle/PlayerState.cs
tests/backend/GameServer.Domain.Tests/MatchComboAccountingTests.cs
tasks/active/TASK-005-match-combo-accounting.md   → tasks/completed/
```

**Modified — implementation**

```text
src/backend/GameServer.Domain/Battle/BattleState.cs
src/backend/GameServer.Domain/Match3/SwapExecution.cs
src/backend/GameServer.Domain/Match3/ResolutionEvents.cs   (stale claim only)
src/backend/GameServer.Application/Battle/BattleStateService.cs
                                                           (stale claim only)
src/backend/GameServer.Api/Hubs/BattleHub.cs
src/frontend/client/src/services/realtime/SignalRService.ts
src/frontend/client/src/game/runtime/GameRuntimeEvents.ts
src/frontend/client/src/game/runtime/GameRuntime.ts
src/frontend/client/src/state/GameRuntimeState.ts
```

**Modified — tests**

```text
tests/backend/GameServer.Domain.Tests/BattleStateTests.cs
tests/backend/GameServer.Domain.Tests/CascadeAndDeterminismTests.cs
tests/backend/GameServer.Application.Tests/BattleStateServiceTests.cs
tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs
src/frontend/client/tests/GameRuntime.test.ts
src/frontend/client/tests/RuntimeBoundaries.test.ts
src/frontend/client/tests/SceneLifecycle.test.ts
```

**Modified — documentation**

```text
docs/02-technical/SIGNALR_PROTOCOL.md   §0 (stage list), §4 (payload + stage
                                        table), §4.2 (new)
docs/02-technical/GAME_STATE.md         §2.0.5.3, §2.2 (implemented-so-far note)
docs/02-technical/REDIS_STATE.md        §7 item 12 (new)
docs/02-technical/GAME_EVENTS.md        §3 item 7 (new)
```

**Not modified — the resolver pipeline, as required**

```text
MatchDetector          SpecialGemPlanner      SpecialGemEffects
BoardResolver          CascadeResolver        Gravity / Spawn
SwapValidator          CommittedSwapPair      BoardState
```

`CascadeResolver` already reports every Match boundary in order through
`Passes`, so no resolver change was needed — the same conclusion TASK-005A §6
reached.

---

## MatchCount

```text
Type:        int, on PlayerState             GAME_STATE.md §2.2
Initial:     0                               GAME_RULES.md §3
Cumulative:  YES — for the whole battle
Resets:      NEVER (not per Turn, Swap, or Cascade)
```

```text
MatchCount += resolution.Passes.Sum(pass => pass.Matches.Count);
```

One `MatchResolution` is one Match, so the per-Match walk and this sum are the
same value — asserted in tests both ways.

| Case | Behaviour | Verified by |
|---|---|---|
| Single Match | +1 | `CommittedSwap_ShouldCountOneMatchIntoMatchCountAndCombo` |
| Several Matches in one Swap | +N, each counted | `CascadeMatches_ShouldEachIncrementMatchCountAndCombo` |
| Cascade Matches | each counted independently | same, over a 3-pass/3-Match fixture |
| Existing value | carried forward, never reset | `CommittedSwap_ShouldAddToAnExistingMatchCountAndResetCombo` (7 → 7+N) |
| Repeated committed Swaps | accumulates | `MatchCount_ShouldNeverResetAcrossAChainOfCommittedSwaps` |
| Special Gem activation/chain/creation | no increment | `SpecialGemActivationAndChain_ShouldNotIncrementMatchCountOrCombo`, `SpecialGemActivationOnTheBoard_ShouldStillCountOnlyMatches` |
| Cleared cells | not counted | `SeveralMatchesInOnePass_ShouldNotBeDerivedFromPassCountOrCascadeDepth` |
| Rejected Swap | unchanged, for every reason | `RejectedSwap_ShouldLeaveMatchCountAndComboUnchangedForEveryReason` |

---

## Combo

```text
Type:        int, on PlayerState             GAME_STATE.md §2.2
Initial:     0                               MATCH3_RULES.md §6.1 item 1
Scope:       one committed Swap; not cumulative across Turns
Reset:       on a committed Swap, before its first Match — nothing else
```

```text
committed Swap → Combo = 0
    then, in resolution order: Combo += 1 per Match
```

| Case | Behaviour | Verified by |
|---|---|---|
| Single Match | 1 (never 0, never 2) | `CommittedSwap_ShouldCountOneMatchIntoMatchCountAndCombo` |
| 3 Matches across cascades | 3 | `CascadeMatches_ShouldEachIncrementMatchCountAndCombo` |
| Next committed Swap | reset, then counts only its own | `RepeatedCommittedSwaps_ShouldAccumulateMatchCountAndResetComboEachTime` (3 → 1, MatchCount 4) |
| Terminating no-match pass | does not reset (it is not in `Passes`) | `CascadeMatches_ShouldEachIncrementMatchCountAndCombo` |
| Special Gem activation | no increment, no reset, no delay | `SpecialGemActivationAndChain_ShouldNotIncrementMatchCountOrCombo` |
| Committed Swap floor | never published as 0 | `CommittedSwap_ShouldNeverPublishComboZero` (exhaustive over all 112 pairs) |
| Rejected Swap | neither reset nor changed | `RejectedSwap_ShouldLeaveMatchCountAndComboUnchangedForEveryReason`, `RejectedSwap_ShouldNotClearTheComboThePreviousSwapLeft` |

`Combo = 0` remains a valid **published** value only before the battle's first
committed Swap, which is what the wire delivers at creation.

---

## Resolution Integration

**The existing `Resolution.Passes` is reused verbatim. No resolver was
modified and no second Match-3 pipeline exists.**

```text
CascadeResolver.CascadeResult
└── Passes[]                  ← the authoritative traversal order
    └── PassResult
        ├── Matches[]                    ← counted (one MatchResolution = one Match)
        ├── CreatedSpecialGems[]         ← not counted
        ├── ActivatedSpecialGems[]       ← not counted
        ├── ClearedCellUnion[]           ← not counted
        ── Board / RngState
```

- `MatchDetector.Detect` is **not** called a second time for accounting. The
  accounting consumes `SwapExecutionResult.Resolution`, which the executor
  already holds.
- No new result type, return structure, or parallel resolver was introduced —
  the accounting writes straight into the state the executor was already
  building.
- `CascadeResult.TotalMatches` (the resolver's own `passes.Sum(p => p.Matches.Count)`)
  and the accounted `MatchCount`/`Combo` are asserted equal, which is the
  cross-check that the traversal counts the resolver's own boundaries.

---

## Swap Integration

The documented order (`MATCH3_RULES.md` §2.1.6, §8.3; `GAME_STATE.md` §5.1) is
preserved exactly, with accounting inserted where §8.3 step 5 and §8.3 item 2
place it:

```text
Validate                          §2.1.2   – counters unchanged
Commit the exchange               §2.1.6 step 4
Begin the Turn                    §8.3 step 4
Reset Combo to 0                  §8.3 step 5   ← the loop's starting value
Resolve the board until stable    §8.3 step 7   ← Passes produced here
Count Matches in resolution order §6.2–§6.3    ← accounting
Increment Sequence by exactly 1   §8.3 step 8
Retain the resulting RngState     §8.3 step 9
One write-back of the whole state §5.1          ← PlayerState written here
Publish                           §4
```

- **One write-back.** `PlayerState` is set in the same `with` expression as
  `BoardState`, `RngState`, `Turn`, `Sequence`, and `LastCommittedSwapPair`,
  over the input state. No partial or progressive mutation exists.
- **Nothing mid-resolution is written.** The reset value and the running totals
  are local to the accounting call; only the finished Swap's values reach the
  state (`GAME_STATE.md` §5.1 item 2).
- **`Turn` and `Sequence` semantics are untouched** — still exactly +1 per
  committed Swap, independent of the Match count.

---

## Rejected Swap Behavior

For **every** rejection reason, both fields are unchanged, together with
everything else:

```text
PlayerState.MatchCount      unchanged
PlayerState.Combo           unchanged
Turn                        unchanged
Sequence                    unchanged
LastCommittedSwapPair       unchanged
BoardState                  unchanged
RngState / RngSeed          unchanged
```

The executor returns `SwapExecutionResult.Rejected`, which carries **no state at
all**, so the caller keeps the snapshot it passed in and the registry keeps the
object it already held. No accounting runs before validation succeeds — the
reset and the increments are unreachable on the rejection path.

Verified for all four documented reasons plus both spellings of the stale pair:

```text
INVALID_CELL_INDEX   (99, To) ; (From, From)
INVALID_SWAP         (13, 15) ; (0, 9)
STALE_ACTION         (0, 1) ; (1, 0)
NO_MATCH_FROM_SWAP   (5, 6)
```

`Execute_ShouldLeaveEveryAuthoritativeFieldUnchangedOnRejection` (existing,
unchanged) covers the rest of the state; the new
`RejectedSwap_ShouldLeaveMatchCountAndComboUnchangedForEveryReason` covers the
two new fields over the same reason set.

---

## SignalR

```text
Payload (implemented stage):  battleId, turn, sequence, board, rngSeed,
                              rngState, playerState
playerState:                  { combo, matchCount }
```

- **No new message.** `BattleStateUpdated` remains the only state-push method;
  no `ComboChanged`/`MatchCountChanged`/`ProgressionUpdated` message, method, or
  subscription was introduced.
- **The Swap request is unchanged** and still carries no `MatchCount`/`Combo`:
  `Swap(battleId, fromCell, toCell, clientSequence)` is untouched, and the
  client remains non-authoritative in both directions.
- `LastCommittedSwapPair` is still not delivered.
- Both members are **always present**, including `0` — zero is delivered as
  zero, never omitted (`MATCH3_RULES.md` §6.5 item 4).
- The projection stays a pure field mapping; the hub derives nothing.
- Documented in the new `SIGNALR_PROTOCOL.md` §4.2.

Client side: the payload type, the runtime's synchronized battle-state shape,
and the shape reader were extended; the runtime reads the values as sent and
computes neither.

---

## Redis

```text
Key:                battle:{battleId}:state      (existing — no new key)
Redis-only fields:  NONE
New keys:           NONE
Schema change:      NONE
Persistence:        STILL DEFERRED — unchanged by this task
```

- `PlayerState` is a member of the §2 shape, so `REDIS_STATE.md` §2 item 1
  covers it: the JSON matches `GAME_STATE.md` §2 exactly, with no Redis-only
  field. No `battle:{battleId}:progression` key, no per-player key, no hash
  field, and no second record exists.
- Persistence remains deferred because `PetState` and `BossState` still do not
  exist, so §2's full shape still cannot be produced and
  `POST /api/battle/start` still cannot create a real battle (§7 items 3, 4, 7).
  This stage neither requires nor authorizes persistence — the same position
  §7 item 8 recorded for the board-resolution stage.
- No persistence code was written; the process-local registry remains the same
  staged, safe-to-lose boundary. `BattleStateService` gained no field.
- Recorded in the new `REDIS_STATE.md` §7 item 12.

---

## Serialization

```text
MatchCount:   always present, default 0, non-nullable
Combo:        always present, default 0, non-nullable
Omission:     never used to represent zero
```

- Both are non-nullable integers with `0` as a real value, so no absence
  convention applies — unlike `LastCommittedSwapPair` (§2.1.10 item 3) and the
  optional cell `SpecialGem` (§2.1.7 item 3).
- Neither is derived from `Turn`, `Sequence`, the board, or the resolution at
  the serialization layer: the projection copies the authoritative values.
- The change is additive: two members inside one new nested object. No existing
  field was renamed, removed, reordered, or retyped.
- The client's runtime reader treats a missing member as **malformed** rather
  than substituting a zero, which is the client-side half of "zero is a value,
  not an absence".

---

## Tests

### Focused tests added

```text
tests/backend/GameServer.Domain.Tests/MatchComboAccountingTests.cs   30 tests
tests/backend/GameServer.Application.Tests/BattleStateServiceTests.cs  +4 tests
tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs              +2 tests
src/frontend/client/tests/GameRuntime.test.ts                          +3 tests
```

Required scenarios, and where each is proven:

| Required scenario | Test |
|---|---|
| New `BattleState` has `PlayerState` | `NewBattleState_ShouldCarryPlayerState` |
| `MatchCount` starts at 0 | `NewBattleState_ShouldStartMatchCountAtZero` |
| `Combo` starts at 0 | `NewBattleState_ShouldStartComboAtZero` |
| Single Match → 1 / 1 | `CommittedSwap_ShouldCountOneMatchIntoMatchCountAndCombo` |
| Multiple Matches in one Swap | `CascadeMatches_ShouldEachIncrementMatchCountAndCombo` (3 Matches, 3 passes) |
| Existing `MatchCount = 7`, `Combo = 4` | `CommittedSwap_ShouldAddToAnExistingMatchCountAndResetCombo` |
| Combo reset (3 → then 1; MatchCount 4) | `RepeatedCommittedSwaps_ShouldAccumulateMatchCountAndResetComboEachTime` |
| Cascade accounting, per pass | `CascadeMatches_ShouldEachIncrementMatchCountAndCombo` |
| Special Gem exclusion | `SpecialGemActivationAndChain_ShouldNotIncrementMatchCountOrCombo`, `SpecialGemActivationOnTheBoard_ShouldStillCountOnlyMatches` |
| Rejected Swap invariants | `RejectedSwap_ShouldLeaveMatchCountAndComboUnchangedForEveryReason`, `RejectedSwap_ShouldNotExposeAnyStateAtAll`, `RejectedSwap_ShouldNotClearTheComboThePreviousSwapLeft`, `ExecuteSwap_ShouldLeavePlayerStateUnchangedOnEveryRejection` (Application), `Swap_ForARejectedRequest_ShouldNotPushOrChangePlayerState` (wire) |
| Determinism | `Accounting_ShouldBeEntirelyDeterministic` (20 runs), `Accounting_ShouldNotDependOnTheRequestOrderOfThePair`, `Accounting_ShouldConsumeNoRngAndChangeNoSeed` |
| Ownership shape | `PlayerState_ShouldBeOwnedByBattleStateAndNotFlatOnIt`, `PlayerState_ShouldCarryExactlyTheTwoDocumentedFields` |
| Single write-back | `CommittedSwap_ShouldWritePlayerStateInTheSameWriteBackAsEverythingElse` |
| No second pipeline / no events | `Executor_ShouldExposeNoAccountingEntryPointOfItsOwn`, `Accounting_ShouldIntroduceNoEventOrMessageSurface` |
| Wire delivery | `Swap_ShouldPushTheResolutionMatchAndComboValues`, `BattleStateUpdated_ShouldCarryExactlyTheDocumentedBoardFoundationFields` |
| Client carries, never derives | `carries the delivered MatchCount and Combo unchanged`, `rejects a payload missing a PlayerState value rather than defaulting it`, `%s derives no Match or Combo value from anything` |

Fixtures are server-generated boards quoted as authoritative inputs, or boards
built through `TestBoard`; every expected Match count is read from the
resolution's own `Passes` rather than hard-coded, so the tests assert the
documented identity (`Combo` = the Swap's Match total, `MatchCount` = their
cumulative sum) and not a particular board's shape. The cascade fixture is
asserted to be a genuine multi-pass (3 passes) and single-Match-first-pass
resolution before its values are asserted.

### Existing tests changed — and why

Four existing tests assert an **exact member set**, which is now contractually
different because `GAME_STATE.md` §2.2 requires `PlayerState`. Each was updated
narrowly, with an explanatory comment:

```text
BattleStateTests.BoardFoundationState_ShouldCarryExactlyTheDocumentedFields
    + PlayerState in the expected set.
BattleStateTests.BoardFoundationState_ShouldStillDeclareNoLaterStageField
    - "PlayerState" moved out of the absent-field list (it now exists);
      + the rest of §2.2 (HP, MaxHP, ATK, DEF, Crit, Power, StatusEffects,
        EquippedRelics, EquippedCards) and PetState/BossState added to it,
        so the "no later-stage field" guarantee is strictly stronger than before.
CascadeAndDeterminismTests.BattleState_ShouldStillCarryTheBoardAndRngState
    + PlayerState in the expected set.
ApiIntegrationTests.BattleStateUpdated_ShouldCarryNoGameplaySystemField
    - playerState/combo/matchCount removed from the blocklist (all three are the
      implemented stage's documented fields);
      + the blocklist now scans the nested playerState object too, and asserts
        its exact member set is {combo, matchCount}.
```

Further narrow updates, all forced by the now-required stage field:

```text
ApiIntegrationTests.BattleStateUpdated_ShouldCarryExactlyTheDocumentedBoardFoundationFields
    expected field set gains "playerState"; asserts the nested object's members.
ApiIntegrationTests.Swap_ShouldDeliverNoNewFieldAndNoLastCommittedSwapPair
    expected field set gains "playerState" (LastCommittedSwapPair still absent).
RuntimeBoundaries.test.ts   the "no gameplay state" term list
    drops `combo` — it is a documented GAME_STATE.md §2.2 field the client now
    carries. The assertion that actually matters was strengthened in its place:
    a new test forbids the runtime from *deriving* either value
    (MatchCount++/combo++/combo = 0/Passes/ClearCells), plus the existing
    "declares no gameplay state or calculations" list keeps every un-implemented
    system (damage, cascade, passive, boss, relic, hp, power, …).
GameRuntime.test.ts / SceneLifecycle.test.ts
    test payload/state builders gain the new required member.
```

No test was weakened, skipped, or deleted. No assertion was relaxed to make an
implementation pass — in every case above the expected set moved because the
documented field set moved.

### Results

```text
Domain:          489 passed  (was 463; +26 net: 30 added, 4 pre-existing updated)
Application:      41 passed  (was  37; +4)
Infrastructure:    1 passed  (unchanged)
Api:              27 passed  (was  25; +2)
                  ─────────
Backend total:   558 passed, 0 failed

Frontend:        174 passed  (was 172; +2 net: 3 added, 1 replaced in place)
                 tsc --noEmit: clean
                 vite build: succeeded (pre-existing dependency warnings only)
```

---

## Build

```text
dotnet build src/backend/GameServer.sln            Debug   0 Warning(s), 0 Error(s)
dotnet build src/backend/GameServer.sln -c Release         0 Warning(s), 0 Error(s)
dotnet test  src/backend/GameServer.sln                    558 passed, 0 failed
cd src/frontend/client && npx tsc --noEmit                 clean
cd src/frontend/client && npm run test:run                 174 passed
cd src/frontend/client && npm run build                    built
```

---

## Documentation Changes

Four documents described the stage as it was before this task. The code was
correct and the documents were the stale side (AGENTS.md §17), so they were
updated to match — no rule was changed, reinterpreted, or invented.

```text
SIGNALR_PROTOCOL.md  v1.3 → v1.4
  §0     stage list gains the Match / Combo accounting stage
  §4     payload signature gains `playerState`; the stage table gains the row
  §4.2   NEW — delivering the Match / Combo stage: two members, always present,
         zero delivered as zero, no second spelling, no new message, client
         renders and never derives, event system not delivered here
  Nothing in §1–§3, §4.1, §5–§8 was redefined.

GAME_STATE.md  v1.4 → v1.5
  §2.0.5.3  "still absent" list corrected: PlayerState's Combo/MatchCount are
            implemented; PetState, BossState, and the rest of §2.2 remain absent
  §2.2      "Implemented so far: Combo and MatchCount" — the two members, their
            initial values, their write-back, and why no absence convention
            applies; the remaining §2.2 members are not-yet-implemented, not
            not-required; Redis deferral restated
  §2.0.3    the "remaining §2 fields" sentence corrected
  §2, §3, §5, §5.1–§5.3 already stated the contract correctly and are unchanged.

REDIS_STATE.md
  §7 item 12 NEW — PlayerState adds no key, no Redis-only field, no persistence
  work; round-trip losslessness covers both members; zero is a value and must
  round-trip as one; lifecycle/concurrency untouched; the deferral is NOT ended
  because PetState/BossState still do not exist. §1–§4 and §7 items 1–11
  unchanged.

GAME_EVENTS.md  v1.2 → v1.3
  §3 item 7 NEW — event emission is a later stage; the Match / Combo accounting
  stage computes and publishes PlayerState as state and emits no event. §1, §1.1,
  and §2 are unchanged: no event was redefined, added, or removed.
```

Also corrected, in code documentation only, two comments that had become false:

```text
ResolutionEvents.cs     MatchResolution's doc said the accounting "does not
                        implement" MatchCount; it now states the counting
                        relationship (one MatchResolution = one Match).
BattleStateService.cs   two comments said PlayerState "does not exist" and that
                        Combo/Match belong to it "which does not exist yet".
GameRuntimeState.ts     the absent-value list named "combo" as unimplemented;
                        it is delivered and held as a presentation copy.
```

**No ADR was created.** `docs/03-decisions/README.md` §2 makes state-management
changes ADR-worthy, and TASK-004A created ADR-010 for a genuinely new state
field. This task made no new decision: the owner, both field names, both
semantics, the lifecycle, and the boundaries are all fixed by `GAME_STATE.md`
§2.2 and `MATCH3_RULES.md` §6, and TASK-005A already recorded them. An ADR
restating them would duplicate content already owned by a technical document,
which `README.md` §2 explicitly excludes.

---

## Not Implemented

Everything the task forbids, plus everything deliberately deferred:

```text
Passive progression                     NOT IMPLEMENTED
Power / PowerChanged                    NOT IMPLEMENTED
Cards / CardCast                        NOT IMPLEMENTED
Relics / RelicTriggered                 NOT IMPLEMENTED
Combat / Damage / HP / StatusEffects    NOT IMPLEMENTED
Boss response / BossState               NOT IMPLEMENTED
Rewards                                 NOT IMPLEMENTED
Multiplayer / PlayerId / PlayerStates[] NOT INTRODUCED
Redis persistence implementation        NOT IMPLEMENTED (deferral unchanged)
Redis keys / fields / schema            NONE ADDED
Database schema changes                 NONE
Frontend gameplay / UI rendering of
  Match or Combo                        NOT IMPLEMENTED
Battle Event system (any)               NOT IMPLEMENTED
MatchCreated / MatchResolved            NOT EMITTED
CascadeCreated / ComboChanged           NOT EMITTED
Event bus / emitter / message type      NOT INTRODUCED
New SignalR gameplay messages           NONE INTRODUCED
Swap request changes                    NONE
Combo damage multiplier / Relic or
  Passive consumption of MatchCount     NOT IMPLEMENTED
Match-3 rules / Special Gem rules /
  Swap validation semantics             UNCHANGED
MatchDetector / SpecialGemPlanner /
  SpecialGemEffects / BoardResolver /
  CascadeResolver / Gravity / Spawn     UNMODIFIED
Second Match-3 detection pipeline       DOES NOT EXIST
```

The published `Combo` value is state only. Nothing consumes it yet —
`GAME_RULES.md` §5 item 4's multiplier table and Combo-threshold Relic
activation remain unimplemented, as do all Match-count-based Passive charges.

---

## STOP Conditions

**None fired.** Every condition the task named was checked against the contract
before implementing, and the implementation required no reinterpretation of any
of them:

```text
PlayerState owner           UNCHANGED — GAME_STATE.md §2.2, as TASK-005A fixed
MatchCount semantics        UNCHANGED — §3/GAME_RULES.md, cumulative per Match
Combo semantics             UNCHANGED — §6/MATCH3_RULES.md, swap-scoped per Match
Match boundary              UNCHANGED — one MatchResolution is one Match
Combo reset behavior        UNCHANGED — committed Swap only, before its first Match
Rejected Swap behavior      UNCHANGED — nothing written, for every reason
Resolution.Passes semantics UNCHANGED — the existing ordering was sufficient
Authoritative state model   UNCHANGED — one nested PlayerState on BattleState
```

No contradiction was found between `GAME_STATE.md` §2.2 / §5.1, `GAME_RULES.md`
§3 / §5, `MATCH3_RULES.md` §6 / §8.3, and the existing resolver: the resolution
already reported every Match boundary in the documented order, so nothing had to
be changed to accommodate the contract. Two observations were recorded in
TASK-005A and remain out of scope here — the `swapOriginIndex: null` placement
caveat (§5.5.3 items 1–2) and the absent Match/Combo Event system; neither
affects Match counting or Combo, and neither was changed.

**One implementation inconvenience, recorded, not escalated.** The stage's
`playerState` payload member was originally frozen by an Api test blocklist and
a client boundary-test term list. Both were test-level artifacts of a *previous*
stage's field set rather than contract statements — `SIGNALR_PROTOCOL.md` §4
item 4 and `GAME_STATE.md` §2.2 both require the field to be delivered — so the
tests were updated narrowly (see "Existing tests changed"). This is an
implementation inconvenience, not a documentation contradiction, and it changed
no contract.

---

## Acceptance Criteria

```text
[✓] PlayerState exists                               src/.../Battle/PlayerState.cs
[✓] PlayerState is owned by BattleState              BattleState.PlayerState
[✓] MatchCount exists as int                         PlayerState.MatchCount
[✓] Combo exists as int                              PlayerState.Combo
[✓] Both initialize to 0                             PlayerState.Initial, Create
[✓] MatchCount is cumulative across the battle       never reset; chain test
[✓] Combo resets once per committed Swap             §6.1 item 2; reset test
[✓] Combo increments once per Match                  per-Match walk
[✓] MatchCount increments once per Match             per-Match walk
[✓] Cascade Matches count independently              3-pass fixture
[✓] Special Gem activations do not count             exclusion tests
[✓] Rejected Swaps do not modify either field        every-reason test
[✓] Existing Resolution.Passes is reused             AccountMatches reads Passes
[✓] No second Match-3 detection pipeline exists      no second Detect call
[✓] Existing Swap execution remains authoritative    SwapExecutor is the one path
[✓] State is written back once, existing commit       one `with` expression
[✓] No new gameplay event system introduced          no event type/emitter
[✓] No new Redis key/schema introduced               REDIS_STATE.md §7 item 12
[✓] No undocumented gameplay rule invented           no rule added or changed
[✓] Focused tests pass                               30 + 4 + 2 + 3 new
[✓] Full backend regression passes                   558 passed, 0 failed
[✓] Build passes with zero warnings/errors           Debug and Release
[✓] No unrelated files are modified                  scope audit below
[✓] Task completion file is created and moved        tasks/completed/
```

**Scope audit.** `git status` shows many modified and untracked files that this
task did not produce: the working tree already carried TASK-001 through
TASK-005A's documentation, sources, tests, and task files (the same note
TASK-003, TASK-004, TASK-004A, and TASK-005A recorded). The files this task
actually wrote are enumerated in "Files created and modified" above; every other
path in `git status` predates it. Within this task's own set, the changes are
confined to: the two contract types, the one accounting function, its
documentation-only corrections, the existing SignalR projection and its client
mirror, the tests that assert those field sets, and the four documents the
change made stale.

---

## Final Report

```text
TASK-005 — Match / Combo Accounting

Status:
DONE

PlayerState:
Created (GameServer.Domain.Battle.PlayerState) as a two-field record —
(int Combo, int MatchCount) — with InitialMatchCount = 0, InitialCombo = 0, and
PlayerState.Initial. No player id, HP, Power, Pet, Card, Relic, Status, reward,
turn-local, queue, timestamp, or client field. Owned by BattleState as a nested
field; no flat BattleState.MatchCount/Combo exists. Present from creation at
0/0 — never null, never a sentinel, never lazily created.

MatchCount:
int, cumulative for the whole battle, never reset. Increments once per
MatchResolution across Resolution.Passes in order — cascades included, several
Matches per Swap each counted, Special Gem activations/chains/creations and
cleared cells excluded by construction.

Combo:
int, swap-scoped, published after the Swap completes. Reset to 0 on every
committed Swap before its first Match, then +1 per Match in the same order.
Equals the Swap's Match total, never 0 for a committed Swap. The terminating
no-match pass neither counts nor resets. A rejected Swap neither resets nor
changes it.

Resolution Integration:
Reuses Resolution.Passes / PassResult.Matches / MatchResolution verbatim. No
second Detect call, no re-sort, no new result type. MatchDetector,
SpecialGemPlanner, SpecialGemEffects, BoardResolver, CascadeResolver, Gravity,
and Spawn are unmodified — the resolver already reported every Match boundary
in order.

Swap Integration:
Accounting is inserted into the existing committed-Swap pipeline after the board
resolves and before the single write-back: validate → exchange → resolve →
account → one `with` write-back (BoardState, RngState, Turn, Sequence,
PlayerState, LastCommittedSwapPair) → publish. No partial or progressive
mutation; nothing mid-resolution is written.

Rejected Swap Behavior:
Both fields unchanged for every rejection reason (INVALID_CELL_INDEX,
INVALID_SWAP, STALE_ACTION, NO_MATCH_FROM_SWAP, both stale spellings), together
with Turn, Sequence, LastCommittedSwapPair, BoardState, RngState, and RngSeed.
The rejection carries no state at all, so no accounting can have run.

SignalR:
No new message, method, or subscription. BattleStateUpdated gains exactly one
payload member, playerState { combo, matchCount }, projected one-to-one —
because SIGNALR_PROTOCOL.md §4 item 4 admits the implemented stage's fields.
Both members are always present including 0. The Swap request is unchanged and
still carries no MatchCount/Combo. Documented in the new §4.2.

Redis:
No new key, no Redis-only field, no schema change, no persistence code. The
deferral is NOT ended — PetState and BossState still do not exist, so §2's full
shape still cannot be produced. Recorded as REDIS_STATE.md §7 item 12.

Serialization:
MatchCount and Combo are always present, default 0, non-nullable, never spelled
by omission. The projection copies authoritative values and derives nothing. The
client's reader rejects a payload missing either member rather than defaulting
one.

Tests:
Domain 489 (30 new), Application 41 (+4), Infrastructure 1, Api 27 (+2) —
558 backend, 0 failed. Frontend 174 (+3), tsc clean, vite build succeeded.
Focused coverage for initialization, single Match, several Matches in one pass,
three Matches across a real three-pass cascade, an existing MatchCount, Combo
reset across two committed Swaps, Special Gem exclusion (including a held Burst activation), every rejection reason, determinism over 20 runs, single
write-back, request-order independence, and the ownership shape. Four existing
exact-member-set tests were updated narrowly because GAME_STATE.md §2.2 now
requires PlayerState; no test was weakened, skipped, or deleted.

Build:
Debug and Release: 0 Warning(s), 0 Error(s).

Files Created:
src/backend/GameServer.Domain/Battle/PlayerState.cs
tests/backend/GameServer.Domain.Tests/MatchComboAccountingTests.cs
tasks/active/TASK-005-match-combo-accounting.md → tasks/completed/

Files Modified:
src/backend/GameServer.Domain/Battle/BattleState.cs
src/backend/GameServer.Domain/Match3/SwapExecution.cs
src/backend/GameServer.Domain/Match3/ResolutionEvents.cs  (doc correction)
src/backend/GameServer.Application/Battle/BattleStateService.cs (doc correction)
src/backend/GameServer.Api/Hubs/BattleHub.cs
src/frontend/client/src/services/realtime/SignalRService.ts
src/frontend/client/src/game/runtime/GameRuntimeEvents.ts
src/frontend/client/src/game/runtime/GameRuntime.ts
src/frontend/client/src/state/GameRuntimeState.ts
tests/backend/GameServer.Domain.Tests/BattleStateTests.cs
tests/backend/GameServer.Domain.Tests/CascadeAndDeterminismTests.cs
tests/backend/GameServer.Application.Tests/BattleStateServiceTests.cs
tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs
src/frontend/client/tests/GameRuntime.test.ts
src/frontend/client/tests/RuntimeBoundaries.test.ts
src/frontend/client/tests/SceneLifecycle.test.ts

Documentation Changes:
SIGNALR_PROTOCOL.md §0, §4, and new §4.2 (v1.3 → v1.4)
GAME_STATE.md §2.0.3, §2.0.5.3, §2.2 (v1.4 → v1.5)
REDIS_STATE.md new §7 item 12
GAME_EVENTS.md new §3 item 7 (v1.2 → v1.3)
No ADR created — no new decision was made; the contract was already fixed.
No game rule was changed, reinterpreted, or invented.

Not Implemented:
Passive, Power, Cards, Relics, Combat, Damage, HP, StatusEffects, Boss, Rewards,
Multiplayer, Redis persistence, database schema, frontend Match/Combo rendering,
the Battle Event system (MatchCreated, MatchResolved, CascadeCreated,
ComboChanged and every other event), any new SignalR message, and any Combo
damage multiplier or Relic/Passive consumer of MatchCount.

STOP Conditions:
None fired. No contract item was contradicted by the implementation. The
resolver already provided sufficient Match boundaries, so no resolver change was
required.

Next Task:
Not started, per this task's instruction. The next event-system task can consume
the now-authoritative PlayerState.Combo and PlayerState.MatchCount; the
remaining §2.2 members (HP, ATK/DEF/Crit, Power, StatusEffects, EquippedRelics,
EquippedCards) and PetState/BossState remain owned by their own later stages.
```

---

## Status

DONE