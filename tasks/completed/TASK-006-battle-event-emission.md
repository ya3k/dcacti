# TASK-006 — Battle Event Emission

---

## Metadata

```text
Task ID:           TASK-006
Type:              FEATURE (gameplay / event emission)
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: backend, testing, review
Workflow:          development/gameplay-change.md
Skills:            gameplay-behavior-derivation, authority-determinism-audit,
                   architecture-conformance, documentation-consistency,
                   test-scenario-generation, scope-validation
Dependencies:      TASK-004 (Swap Execution, DONE)      — the pipeline extended
                   TASK-005 (Match / Combo Accounting, DONE) — the Combo values
                   the ComboChanged payload reports
```

---

## Objective

Implement the Battle Event emission that `GAME_EVENTS.md` §1, §1.1, and §2
already define, for the existing Match-3 resolution and the existing
`PlayerState` accounting — producing events only, from the resolution the
pipeline already ran, without a second pipeline and without mutating any
authoritative state.

---

## Discovery — What Was Read

```text
docs/00-overview/     MVP_SCOPE.md
docs/01-game-design/  GAME_RULES.md (§2, §3, §4, §5, §16, §17, §18)
                      MATCH3_RULES.md (§1.0, §2.1.1–§2.1.6, §3, §3.1–§3.4, §4,
                                       §4.1–§4.6, §5.4, §5.5.1, §5.5.3, §5.5.4,
                                       §5.5.5, §5.6, §5.8.1–§5.8.3, §6 in full,
                                       §6.7, §7, §7.1–§7.2, §8, §8.1–§8.4)
docs/02-technical/    GAME_EVENTS.md (§1, §1.1, §1.2, §1.3, §2, §3)
                      GAME_STATE.md (§0, §2.1.1, §2.1.3, §2.1.4, §2.1.7, §2.1.8,
                                      §2.2, §2.6, §3, §5, §5.1–§5.3)
                      SIGNALR_PROTOCOL.md (§0, §2, §2.1, §3, §3.1, §4, §4.2, §5,
                                          §6, §8)
                      ARCHITECTURE.md (§1, §2.1, §4.1, §5)
                      REDIS_STATE.md (§7)
                      TDD.md
docs/03-decisions/    ADR-001, ADR-004, ADR-009, ADR-010
tasks/completed/      TASK-005 (the accounting this consumes), TASK-004, TASK-004A,
                      TASK-003, TASK-002
tasks/backlog/        TASK-001 (the resolution contract)
Existing implementation:
                      src/backend/GameServer.Domain/Match3/SwapExecution.cs
                      src/backend/GameServer.Domain/Match3/CascadeResolver.cs
                      src/backend/GameServer.Domain/Match3/BoardResolver.cs
                      src/backend/GameServer.Domain/Match3/ResolutionEvents.cs
                      src/backend/GameServer.Domain/Match3/MatchShape.cs
                      src/backend/GameServer.Domain/Match3/SpecialGemClaim.cs
                      src/backend/GameServer.Domain/Battle/{BattleState,PlayerState}.cs
                      src/backend/GameServer.Application/Battle/BattleStateService.cs
                      src/backend/GameServer.Api/Hubs/BattleHub.cs
                      every existing backend test suite
```

---

## Executive Summary

`GAME_EVENTS.md` already defined the events, their payloads, and their order.
`Resolution.Passes` already carried every Match boundary and every
`GemMatched` report in the documented order. `PlayerState.Combo` was already
authoritative. So this task added the smallest thing that was actually missing:
a **Domain representation of one event** and a **builder that renders the
resolution into the documented event sequence**, wired into the existing
committed-Swap pipeline as an output alongside the single state write-back.

No resolver, no validator, no board rule, no accounting rule, and no
documentation contract was changed. One test's expected-value blocklist was
updated because the events it forbade now legitimately exist.

---

## Relevant Documentation

```text
Owner              GAME_EVENTS.md §1, §1.1, §1.2, §1.3, §2   (the event contract)
Event names        GAME_RULES.md §16                          (canonical name list)
Ordering source    MATCH3_RULES.md §3.2 (per pass), §4.2 (across passes)
Cascade depth      MATCH3_RULES.md §4.2 items 1–2             (depth d − 1)
Termination        MATCH3_RULES.md §4.3 item 4; GAME_EVENTS.md §1.1 item 6
Special Gems       MATCH3_RULES.md §5.5.5 item 8, §6.3.1; GAME_EVENTS.md §2 item 3
Combo value        GAME_EVENTS.md §2; MATCH3_RULES.md §6.2, §6.3, §6.5, §6.6
Rejection          GAME_EVENTS.md §1.2; MATCH3_RULES.md §2.1.5 item 6
Not state          GAME_EVENTS.md §3 item 6; SIGNALR_PROTOCOL.md §4 item 6
Write-back         GAME_STATE.md §5.1; MATCH3_RULES.md §8.3
Determinism        MATCH3_RULES.md §7.1, §7.2 item 4; AGENTS.md §11
Delivery (later)   SIGNALR_PROTOCOL.md §3, §3.1 (ReceiveEvents — not this task)
```

---

## Plan

1. Inspect the repository for an existing event abstraction.
2. Add the smallest Domain representation: `BattleEventType` + `BattleEvent`.
3. Add `BattleEventBuilder.Build(resolution, combo)` reading `Resolution.Passes`.
4. Carry the events on the existing `SwapExecutionResult`; produce them inside
   the existing `SwapExecutor.Execute` before the single write-back.
5. Update the code comments TASK-005 left claiming no event surface exists.
6. Add focused tests covering every scenario the task names.
7. Run the full backend regression and both builds.

---

## Event Infrastructure — What Already Existed

**An abstraction already existed, in part, and was reused.**

```text
GemMatchedEvent          src/.../Match3/ResolutionEvents.cs   TASK-002 — REUSED
                         the Domain description of one consumed Gem, already
                         produced by BoardResolver in the §1.3 enumeration order.
                         GemMatched's payload is this value unchanged.

MatchResolution          src/.../Match3/ResolutionEvents.cs   TASK-002 — REUSED
                         shape + cascade depth + created Special Gems — exactly
                         GAME_EVENTS.md §2's MatchCreated payload.

ActivatedSpecialGem      src/.../Match3/BoardResolver.cs     TASK-002 — READ ONLY
SpecialGemClaim          src/.../Match3/SpecialGemClaim.cs   TASK-002 — READ ONLY
```

**What did not exist** was a value describing *one event as an event* — a
discriminated value naming which documented event it is and carrying that
event's payload. That gap is exactly what `GAME_EVENTS.md` §2 defines and
`GAME_RULES.md` §16 names, so it is the smallest representation required, and
it is all this task added.

**Deliberately not introduced:**

```text
event bus / broker / queue / dispatcher     NOT INTRODUCED
emitter / publisher / subscriber interfaces NOT INTRODUCED
persistence or event log                    NOT INTRODUCED
ReceiveEvents SignalR message               NOT INTRODUCED
new SignalR method or subscription          NOT INTRODUCED
ADR                                         NOT REQUIRED (no architectural change)
```

`BattleEventBuilder` is a pure static function returning an
`IReadOnlyList<BattleEvent>`; it holds no registry, no buffer, and no state, and
no caller can subscribe to it. Verified by test
(`EventEmission_ShouldIntroduceNoEventInfrastructure`,
`EventBuilder_ShouldRunNoSecondDetectionPass`).

---

## Changes

### 1. `BattleEvent` / `BattleEventType` — new types

`src/backend/GameServer.Domain/Match3/BattleEvent.cs`

```csharp
public enum BattleEventType
{
    MatchCreated = 0,
    CascadeCreated = 1,
    ComboChanged = 2,
    GemMatched = 3,
}

public readonly record struct BattleEvent
{
    public BattleEventType Type { get; }
    public MatchResolution Match { get; }      // MatchCreated only
    public int CascadeDepth { get; }           // CascadeCreated only
    public int Combo { get; }                  // ComboChanged only — the NEW value
    public GemMatchedEvent Gem { get; }        // GemMatched only
}
```

- **Exactly the four names the Match-3 resolution produces**, taken from
  `GAME_RULES.md` §16's canonical list and `GAME_EVENTS.md` §2's definitions.
- **Payload members are the existing Domain values**, not re-derived ones:
  `MatchCreated` carries the resolver's own `MatchResolution`, so shape, cells,
  Gem type, tier, and created Special Gems are the values already produced;
  `GemMatched` carries the existing `GemMatchedEvent`. Nothing is recomputed.
- **A payload accessor for the wrong kind throws**, rather than returning a
  plausible default: a `CascadeCreated` has no Match, and reading one as if it
  did is a defect. Verified by `EventPayloadAccessors_ShouldRejectTheWrongEventKind`.
- The value carries **no id, timestamp, or generated identifier** — verified
  reflectively by `EventValue_ShouldCarryNoTimeGuidOrGeneratedIdentifier`.

**Undocumented event types are absent by construction**, so none had to be
"not invented": there is no `MatchCountChanged`, `SpecialGemActivated`,
`SpecialGemCreated`, `SpecialGemDestroyed`, `TurnChanged`, `SequenceChanged`,
`BoardChanged`, `BoardResolved`, or `CascadeUpdated` member, and none exists
anywhere in Domain (asserted by test). Each is excluded for a documented reason:

```text
MatchCountChanged      the Match count is PlayerState.MatchCount — STATE, and
                       GAME_EVENTS.md §3 item 7 says the accounting stage emits
                       no event at all. No such event is defined in §2.
SpecialGemActivated    GAME_EVENTS.md §2 item 3: "no SpecialGemActivated event
                       or message exists". Activation is reported through
                       GemMatched + the state push (SIGNALR_PROTOCOL.md §8 item 7).
SpecialGemCreated      creation is reported inside the MatchCreated payload
                       ("Special Gem created (if any)"), not as its own event.
TurnChanged /          Turn and Sequence are delivered as STATE
SequenceChanged        (SIGNALR_PROTOCOL.md §3.1 item 3, §4). No wrapper event is
                       defined; TurnStarted/TurnEnded are separate names this
                       task's scope does not reach.
BoardChanged           the board is a state field delivered by the §4 push
                       (SIGNALR_PROTOCOL.md §8 items 5–7).
```

### 2. `BattleEventBuilder` — new builder

`src/backend/GameServer.Domain/Match3/BattleEventBuilder.cs`

```csharp
public static IReadOnlyList<BattleEvent> Build(
    CascadeResolver.CascadeResult resolution,
    int combo)
```

One walk of `Resolution.Passes`, in the resolver's own order, emitting:

```text
for each pass in Resolution.Passes order:            (the outer order, §4.2)
    if pass is a Cascade (first Match's depth ≥ 2):
        CascadeCreated(depth − 1)                    §1.1 item 1 — FIRST
    for each Match in the pass's §3.2 order:
        MatchCreated(match)                          §1.1 item 2
        GemMatched…  from pass.MatchedCellGemMatched §1.3 sub-step 1
        ComboChanged(++comboSoFar)                   §1.1 item 5
    GemMatched…  from pass.ActivationCellGemMatched  §1.3 sub-step 2
```

- **No second detection pass.** `MatchDetector.Detect` is never called. The
  builder reads only `PassResult.Matches`, `PassResult.MatchedCellGemMatched`,
  and `PassResult.ActivationCellGemMatched` — the reports the existing
  `BoardResolver` already made.
- **No new ordering rule.** Nothing is sorted. The pass order, the match-set
  order, and the two `GemMatched` orders are the resolver's; the builder
  reproduces the `GAME_EVENTS.md` §1.1 cycle on them. Verified structurally by
  `Events_ShouldFollowTheDocumentedCycleOrder`, which reconstructs the expected
  sequence from the resolution's own report.
- **Not derived from cleared cells or animation order.** `ClearedCellUnion` is
  never read; only the two per-sub-step reports are, which is what §1.3 defines.
- **`ComboChanged` uses the documented new value.** The run starts at 1 for the
  Swap's first Match and increments once per Match, so the last value equals the
  accounted `PlayerState.Combo` — asserted directly
  (`CascadeResolution_ShouldReachTheAccountedComboForItsLastMatch`,
  `ComboChanged_ShouldUseTheDocumentedNewComboValueAcrossASwapChain`).
- **A termination pass contributes nothing**, because it is absent from
  `Passes` by construction.

### 3. Pipeline integration — one pipeline, one write-back

`src/backend/GameServer.Domain/Match3/SwapExecution.cs`

```csharp
var resolution   = CascadeResolver.Resolve(exchanged, rng, swapOriginIndex: null);
var playerState  = AccountMatches(state.PlayerState, resolution);
var events       = BattleEventBuilder.Build(resolution, playerState.Combo);   // ← added

var resolved = state with { BoardState = …, RngState = …, Turn = …, Sequence = …,
                            PlayerState = playerState, LastCommittedSwapPair = … };

return SwapExecutionResult.Committed(resolved, resolution, events);           // ← added
```

- The flow remains the documented one and no second pipeline was created:
  `Validate → Swap → Resolve Board → Account Match/Combo → Produce Events →
  single BattleState write-back → (delivery is a later task)`.
- **Events are built after the board is stable and the accounting is complete**,
  so every value they report is a finished resolution's
  (`GAME_STATE.md` §5.1 item 2, `SIGNALR_PROTOCOL.md` §3.1 item 1).
- **They travel on the existing result** as `SwapExecutionResult.Events`, so
  there is no separate event-producing entry point a caller could run instead of
  resolving. `SwapExecutor` still exposes exactly one public operation
  (`Execute`) — asserted by `Executor_ShouldStillExposeOnlyItsSingleEntryPoint`.
- `ResolveBoard` is untouched: it is not a Swap and emits nothing.

### 4. Files created and modified

**Created**

```text
src/backend/GameServer.Domain/Match3/BattleEvent.cs
src/backend/GameServer.Domain/Match3/BattleEventBuilder.cs
tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs
tasks/completed/TASK-006-battle-event-emission.md
```

**Modified**

```text
src/backend/GameServer.Domain/Match3/SwapExecution.cs
    SwapExecutionResult: + Events (empty list for a rejection), Committed gains
    the events parameter, Rejected passes an empty list.
    SwapExecutor.Execute: builds the events before the write-back.
    Class + method documentation: records that the events describe the same
    resolution, and how.
src/backend/GameServer.Application/Battle/BattleStateService.cs
    Two comments claiming "Battle Events are not emitted" corrected: the
    boundary hands back what Domain produced and delivers nothing.
tests/backend/GameServer.Domain.Tests/MatchComboAccountingTests.cs
    Accounting_ShouldIntroduceNoEventOrMessageSurface — see "Existing tests
    changed" below.
```

**Not modified — the resolution pipeline, as required**

```text
MatchDetector        SpecialGemPlanner    SpecialGemEffects   SpecialGemClaim
BoardResolver        CascadeResolver      Gravity / Spawn     MatchShape
SwapValidator        CommittedSwapPair    BoardState          BattleState
PlayerState          MatchResolution      GemMatchedEvent     ActivatedSpecialGem
```

---

## Events Implemented

```text
MatchCreated      EMITTED   one per MatchResolution, in the pass's §3.2 order,
                            across every pass. Payload: the resolution's own
                            MatchResolution (shape/cells, Gem type, tier,
                            created Special Gems) — GAME_EVENTS.md §2.
CascadeCreated    EMITTED   once per Cascade pass, before that pass's Matches,
                            at depth d − 1 (1 for the Swap's second pass).
                            Not emitted for the first pass or the terminating
                            pass — GAME_EVENTS.md §2, MATCH3_RULES.md §4.2–§4.3.
ComboChanged      EMITTED   one per Match, after that Match's MatchCreated and
                            before the next Match's. Payload: the NEW Combo
                            value — GAME_EVENTS.md §2, MATCH3_RULES.md §6.6.
GemMatched        EMITTED   once per cleared cell, per sub-step, ascending §1.0
                            index: the pass's matched cells first, then the
                            activations of that pass — GAME_EVENTS.md §1.3, §2.
```

`GemMatched` was not named in the task's "at minimum" list, but §1.1 places it on
the Match-3 cycle and §2 defines its payload, and the pieced
`GemMatchedEvent` value already existed from TASK-002 with no emitter. Leaving it
unemitted would have made this task's own output incomplete against §1.1, so it
is emitted. Nothing was invented to do so: the resolver already reported exactly
those cells in exactly that order.

`MatchResolved` is **not** emitted, and that is not an omission:
`GAME_EVENTS.md` §2 states that `MatchCreated` — not `MatchResolved` — is the
event corresponding to the Match count, and the resolution reports detection, not
removal. Its ordering position (§1.1 item 2) is honoured by `MatchCreated`. No
documented payload for `MatchResolved` is produced by the resolution, so
emitting it would require inventing one — a STOP condition, not a feature.

```text
BattleStarted / TurnStarted / TurnEnded / SwapStarted / SwapResolved
    NOT IMPLEMENTED — §1 places them outside the Match-3 cycle's scope here.
    TurnStarted/TurnEnded and SwapStarted/SwapResolved sit at the action
    boundary, which this task's pipeline stage does not own; BattleStarted
    requires a Pet and a Boss, which do not exist (GAME_STATE.md §2.3–§2.4).
PowerChanged, PassiveCharged/Triggered, RelicTriggered, CardCast,
PetSkillCast, DamageCalculated/Dealt/Taken, BossSkillCast,
BattleWon/BattleLost
    NOT IMPLEMENTED — owned by the Combat, Passive, Relic, Card, and Boss
    stages (GAME_EVENTS.md §3 item 7).
```

---

## Event Ordering

```text
per pass, in Resolution.Passes order — the outer order (MATCH3_RULES.md §4.2)
  ├── CascadeCreated     once, when the pass is a Cascade — FIRST (GAME_EVENTS.md
  │                      §1.1 item 1: "it reports the pass itself")
  └── per Match, in the pass's §3.2 order (orientation, then start cell)
        ├── MatchCreated                     §1.1 item 2
        ├── GemMatched  (matched cells, ascending §1.0 index)   §1.3 sub-step 1
        ── ComboChanged (the NEW Combo value)                  §1.1 item 5
  └── GemMatched        (activation cells, ascending §1.0 index) §1.3 sub-step 2
```

Every position is the resolution's own; none is a new ordering rule.

```text
Ordering source          Resolution.Passes → PassResult.Matches (not a new rule)
Pass order               MATCH3_RULES.md §4.2, via the resolver's own Passes list
Within a pass            MATCH3_RULES.md §3.2, via the resolver's own match set
Cascade depth            MATCH3_RULES.md §4.2 item 2 (d − 1)
GemMatched enumeration   GAME_EVENTS.md §1.3, via the resolver's own reports
Second detection pass    NOT RUN — MatchDetector.Detect is never called
New ordering rule        NONE
Derived from cleared
  cells / animation      NO — ClearedCellUnion is never read
```

---

## State Interaction

```text
BattleState                 NOT MUTATED — the input state is a pure read
PlayerState                 NOT MUTATED — Combo/MatchCount remain TASK-005's
BoardState                  NOT MODIFIED — no board is created or altered
RNG                         NOT CONSUMED — no draw, no reseed
Turn                        NOT CHANGED by events — still +1 per committed Swap
Sequence                    NOT CHANGED by events — still +1 per committed Swap
LastCommittedSwapPair       NOT MODIFIED by events — written by the Swap only
```

- **Events are outputs only** (`GAME_EVENTS.md` §3 item 6). `BattleEventBuilder`
  is a pure static function over the resolution and the accounted Combo; it has
  no mutable state and no side effect.
- **`PlayerState.MatchCount` and `PlayerState.Combo` remain authoritative state
  owned by TASK-005.** The builder consumes the accounted `Combo` as its
  endpoint; it never writes to `PlayerState`, and it recomputes nothing — the
  per-Match run it emits is the same walk the accounting performed, so the last
  value equals the accounted one.
- **The events are not a second account of the resolution.** They carry the
  resolver's own objects (`MatchResolution`, `GemMatchedEvent`), so an event
  cannot disagree with the resolution it describes.
- **One write-back is preserved.** Events are built before it and carried
  alongside it; the state is still written once, whole.
- Verified by `EventGeneration_ShouldNotMutateBattleState`,
  `EventGeneration_ShouldNotConsumeRngOrChangeTheSeed`,
  `EventGeneration_ShouldNotChangeTurnOrSequenceBeyondTheResolution`,
  `EventGeneration_ShouldLeaveLastCommittedSwapPairToTheSwap`, and
  `EventGeneration_ShouldNotWriteAnythingIntoThePassResult`.

---

## Rejected Swap

**No gameplay Battle Event of any kind is produced.**

```text
INVALID_CELL_INDEX   (99, To) ; (From, From)   → no event
INVALID_SWAP         (13, 15) ; (0, 9)         → no event
STALE_ACTION         (From, To) after commit   → no event
                     (To, From) after commit   → no event  (same pair)
NO_MATCH_FROM_SWAP   (5, 6)                    → no event
```

- `SwapExecutor.Execute` returns before the resolution when validation rejects, so
  `BattleEventBuilder.Build` is unreachable on the rejection path — the guarantee
  is structural, not a filter.
- `SwapExecutionResult.Rejected` carries **no state and an empty event list**
  (`GAME_EVENTS.md` §1.2: "emits no Battle Event at all — not `SwapStarted`, not
  `SwapResolved`, not any Match-3 event").
- The list is **empty, not null**, so a caller never has to distinguish "no
  events" from "not applicable".
- **The rejection is invisible on the event path**: §1.2 item 3 — "the event
  stream of a battle is therefore identical whether or not a rejected action was
  ever sent" — is asserted directly by sending all five rejections between two
  identical commits and comparing the resulting sequences and states
  (`RejectedSwap_ShouldNotDisturbTheEventStreamOfTheNextCommittedSwap`).
- Verified for every reason by `RejectedSwap_ShouldProduceNoGameplayEvents`
  (a `[Theory]` over five requests), `StaleAction_ShouldProduceNoGameplayEvents`,
  and `Events_ShouldBeAnEmptyListForARejectionNeverNull`.

---

## Determinism

```text
given identical BattleState + SwapRequest
    → identical event sequence, byte for byte, run after run
```

- The sequence depends only on `Resolution.Passes`, the pass's match sets, and the
  Cell indices the resolver already ordered — all functions of the board and the
  request (`MATCH3_RULES.md` §7.1).
- **No RNG is consumed for event generation** (`§7.2 item 4`). The only draw site
  remains Spawn; the seed is never rewritten.
- **No timestamp, GUID, random id, or hash** participates — asserted reflectively
  over `BattleEvent` and every nested payload type.
- **No unordered collection participates.** The walk is over `IReadOnlyList`s in
  their own order; the only set-like step is none at all — nothing is sorted,
  deduplicated, or grouped.
- Asserted by `EventSequence_ShouldBeDeterministicAcrossRepeatedIdenticalExecutions`
  (20 runs over a multi-pass cascade),
  `EventSequence_ShouldNotDependOnTheRequestArgumentOrder`, and
  `EventSequence_ShouldBeDeterministicOverEveryCommittingPair` (all 112 adjacent
  pairs of a generated board).
- Sequences are compared as whole value-level descriptions, never by reference
  (`AGENTS.md` §11).

---

## Integration Boundary

```text
Validate                    SwapValidator.Validate          §2.1.2
Commit the exchange         BoardState.WithSwapped          §2.1.6 step 4
Resolve the board           CascadeResolver.Resolve         §4, §8.3 step 7
Account Match / Combo       SwapExecutor.AccountMatches     §6, §8.3 step 5
Produce Events              BattleEventBuilder.Build        GAME_EVENTS.md §1.1
One BattleState write-back  state with { … }                GAME_STATE.md §5.1
Publish                     (SIGNALR_PROTOCOL.md §3)        LATER TASK
```

- **No second resolution pipeline.** One `Execute`, one `CascadeResolver.Resolve`
  call, one accounting walk, one write-back. The builder runs no detection and
  the executor still exposes one public operation.
- **No new SignalR message.** `SIGNALR_PROTOCOL.md` §8 item 7 forbids a
  `MatchCreated`-style method, `BoardResolved`, `CascadeUpdated`, or
  `SpecialGemActivated` message, and this task added none. The Hub is untouched.
  Delivering the batch is `ReceiveEvents` (§3), which remains unimplemented and
  out of scope.
- **No Redis or database change.** Persistence remains deferred
  (`REDIS_STATE.md` §7 items 8 and 12).

---

## Tests

### Focused tests added

```text
tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs   46 tests
```

Every scenario the task names, and where each is proven:

| Required scenario | Test |
|---|---|
| One Match | `SingleMatch_ShouldEmitMatchCreatedAndComboChanged` |
| Multiple Matches in one pass | `MultipleMatchesInOnePass_ShouldEmitOneMatchCreatedEachInMatchSetOrder`, `…_ShouldIncrementComboByExactlyOnePerMatch`, `…_ShouldCountMatches_NotClearedCells` |
| Multiple cascade passes | `CascadeResolution_ShouldEmitOneCascadeCreatedPerCascadePass`, `CascadeResolution_ShouldEmitOneMatchCreatedPerMatchAcrossEveryPass` |
| Match event ordering | `Events_ShouldFollowTheDocumentedCycleOrder`, `SingleMatch_ShouldEmitMatchCreatedBeforeComboChanged`, `Events_ShouldReportTheResolutionTheyDescribe` |
| `ComboChanged` uses the documented new value | `MultipleMatchesInOnePass_ShouldIncrementComboByExactlyOnePerMatch`, `CascadeResolution_ShouldReachTheAccountedComboForItsLastMatch`, `ComboChanged_ShouldUseTheDocumentedNewComboValueAcrossASwapChain` |
| `CascadeCreated` ordering / depth | `CascadeCreated_ShouldPrecedeTheMatchesOfItsPass`, `CascadeResolution_ShouldEmitOneCascadeCreatedPerCascadePass` |
| Special Gem activation → no undocumented Match event | `SpecialGemActivation_ShouldNotGenerateAnUndocumentedMatchEvent`, `SpecialGemCreation_ShouldNotEmitAnAdditionalMatchCreated` |
| GemMatched payload on activation | `GemMatched_ShouldReportTheSpecialGemConsumedAtThatCell`, `SingleMatch_ShouldEmitOneGemMatchedPerMatchedCell` |
| Rejected Swap → no gameplay events | `RejectedSwap_ShouldProduceNoGameplayEvents` (5 cases), `StaleAction_ShouldProduceNoGameplayEvents`, `RejectedSwap_ShouldNotDisturbTheEventStreamOfTheNextCommittedSwap`, `Events_ShouldBeAnEmptyListForARejectionNeverNull` |
| Event generation does not mutate `BattleState` | `EventGeneration_ShouldNotMutateBattleState`, `…_ShouldNotConsumeRngOrChangeTheSeed`, `…_ShouldNotChangeTurnOrSequenceBeyondTheResolution`, `…_ShouldLeaveLastCommittedSwapPairToTheSwap`, `…_ShouldNotWriteAnythingIntoThePassResult` |
| Deterministic sequence across repeated executions | `EventSequence_ShouldBeDeterministicAcrossRepeatedIdenticalExecutions` (20 runs), `…_ShouldNotDependOnTheRequestArgumentOrder`, `…_ShouldBeDeterministicOverEveryCommittingPair` (112 pairs) |
| Event count matches `Resolution.Passes` | `MatchEventCount_ShouldMatchTheResolutionPasses`, `GemMatchedCount_ShouldMatchTheResolutionsOwnReports`, `CascadeEventCount_ShouldMatchTheCascadePasses` |
| No duplicate events | `Events_ShouldContainNoDuplicates` |
| No event for the terminating pass | `TerminatingPass_ShouldEmitNothing`, `CommittedSwap_ShouldAlwaysEmitAtLeastOneMatchEvent` |
| No undocumented event type | `EventTypes_ShouldBeExactlyTheDocumentedOnes` |
| No event infrastructure | `EventEmission_ShouldIntroduceNoEventInfrastructure`, `EventBuilder_ShouldRunNoSecondDetectionPass`, `Events_ShouldBeCarriedOnTheExistingResultNotASecondPipeline` |
| No second pipeline | `Executor_ShouldStillExposeOnlyItsSingleEntryPoint`, `EventGeneration_ShouldNotWriteAnythingIntoThePassResult` |
| Wrong-payload access is a defect | `EventPayloadAccessors_ShouldRejectTheWrongEventKind` |
| No timestamp / GUID / id | `EventValue_ShouldCarryNoTimeGuidOrGeneratedIdentifier` |

**Fixtures are not hard-coded board shapes where the shape is the point.** The
"several Matches in one pass" tests search the pair space for a pair whose
resolution *really* has a multi-Match first pass and assert that property before
using it (`FindMultiMatchInOnePass`), so the fixture cannot silently degrade into
a test of something else. The cascade fixture is asserted to be a genuine
multi-pass resolution first. The Special Gem activation fixture holds a Line
Clear Gem at a cell the match set consumes, and the test asserts both that an
activation occurred and that it cleared extra cells before asserting the absence
of extra Match events. Expected Combo runs are derived from the resolution's own
Match sequence, never hard-coded.

### Existing tests changed — and why

**One test changed**, narrowly and with an explanatory comment:

```text
MatchComboAccountingTests.Accounting_ShouldIntroduceNoEventOrMessageSurface
    TASK-005's version forbade the four event names and "BattleEvent" outright,
    because at that stage the accounting emitted nothing and no event type
    existed anywhere in Domain. GAME_EVENTS.md §3 item 7 recorded that as a
    SEQUENCING position, not a scope reduction — and this task is the stage it
    deferred to, so the four names now legitimately exist.
    The test was rewritten to assert what the documentation actually forbids and
    keeps forbidding: no event INFRASTRUCTURE (bus, broker, emitter, publisher,
    store, message broker, handler, subscriber) and no UNDOCUMENTED event name
    (MatchCountChanged, SpecialGemActivated, SpecialGemCreated,
    SpecialGemDestroyed, TurnChanged, SequenceChanged, BoardChanged,
    BoardUpdated). Strictly stronger than the blocklist it replaced, in the
    direction the contract owns.
```

No other test was touched. No test was weakened, skipped, or deleted; no
assertion was relaxed to make the implementation pass. `BattleEventEmissionTests`
independently re-asserts both halves of the rewritten test.

### Results

```text
Domain:          535 passed  (was 489; +46)
Application:      41 passed  (unchanged)
Infrastructure:    1 passed  (unchanged)
Api:              27 passed  (unchanged)
                  ─────────
Backend total:   604 passed, 0 failed
```

---

## Build

```text
dotnet build src/backend/GameServer.sln -c Debug     0 Warning(s), 0 Error(s)
dotnet build src/backend/GameServer.sln -c Release   0 Warning(s), 0 Error(s)
dotnet test  src/backend/GameServer.sln             604 passed, 0 failed
```

Frontend was not built: no frontend file was touched, and the frontend does not
consume battle events (`ReceiveEvents` is unimplemented).

---

## Documentation Changes

**None.** `GAME_EVENTS.md` §1, §1.1, §1.2, §1.3, and §2 already define every
event, payload, and ordering this task implements, and §3 item 7 already recorded
that emission was a later stage — this task is that stage, so §3 item 7's
sequencing note is now historical rather than stale, and §1.1/§2 were never
weakened by it (the document says so itself: "This item records a sequencing
position, not a scope reduction of §1—§2").

The document was read as the authority and implemented; no rule was changed,
reinterpreted, or invented, and no doc was edited to match the code
(`AGENTS.md` §17). The only "documentation" updates are two code comments in
`BattleStateService.cs` that had become false, and they were corrected rather
than the contract.

**No ADR was created.** `docs/03-decisions/README.md` §2 makes state-model and
architecture changes ADR-worthy. This task changed neither: no `BattleState`
field, no persistence strategy, no module boundary, and no authoritative model.
The event contract is owned by `GAME_EVENTS.md`, and an ADR restating it would
duplicate content a technical document already owns — which `README.md` §2
explicitly excludes.

---

## Not Implemented

```text
Passive progression / PassiveCharged / PassiveTriggered   NOT IMPLEMENTED
Power / PowerChanged                                      NOT IMPLEMENTED
Cards / CardCast                                          NOT IMPLEMENTED
Relics / RelicTriggered                                   NOT IMPLEMENTED
Combat / Damage / HP / DamageCalculated/Dealt/Taken       NOT IMPLEMENTED
Boss / BossSkillCast / BossState                          NOT IMPLEMENTED
Rewards                                                   NOT IMPLEMENTED
BattleWon / BattleLost                                    NOT IMPLEMENTED
BattleStarted / PetState                                  NOT IMPLEMENTED
TurnStarted / TurnEnded / SwapStarted / SwapResolved      NOT IMPLEMENTED
MatchResolved                                             NOT EMITTED (see above)
Multiplayer                                               NOT INTRODUCED
Redis persistence implementation                          NOT IMPLEMENTED
Redis keys / fields / schema                              NONE ADDED
Database schema changes                                   NONE
Frontend gameplay / event rendering                       NOT IMPLEMENTED
ReceiveEvents / any new SignalR message or method         NOT INTRODUCED
Event bus / broker / emitter / publisher / subscriber     NOT INTRODUCED
Event persistence / event log                             NOT INTRODUCED
Match-3 resolution semantics                              UNCHANGED
Special Gem semantics                                     UNCHANGED
Swap validation / execution semantics                     UNCHANGED
MatchDetector / SpecialGemPlanner / SpecialGemEffects /
  BoardResolver / CascadeResolver / Gravity / Spawn       UNMODIFIED
Second Match-3 detection pipeline                         DOES NOT EXIST
New ordering rule for Matches                             NONE INTRODUCED
```

---

## STOP Conditions

**None fired.** Each condition the task named was checked against the contract
before implementing:

```text
Event with no sufficiently defined payload
    NO — §2 defines a payload for MatchCreated, CascadeCreated, ComboChanged,
    and GemMatched, and every member is either an existing Domain value or a
    documented scalar (cascade depth, new Combo value).
    MatchResolved HAS a defined payload ("Match shape (cells), Gem type, tier,
    Special Gem created") but the resolution reports detection, not removal, and
    §2 states MatchCreated — not MatchResolved — is the event corresponding to
    the Match count. It was therefore left unimplemented rather than invented;
    see "Events Implemented".

Event ordering contradicts MATCH3_RULES.md
    NO — §1.1 states explicitly that it reproduces §4's cycle only to place the
    events on it, and the two agree: CascadeCreated opens its pass, MatchCreated
    precedes ComboChanged per Match, and the terminating pass emits nothing.
    Both are the resolver's own order.

Existing event infrastructure conflicts with the documented model
    NO — the existing values (GemMatchedEvent, MatchResolution,
    ActivatedSpecialGem, SpecialGemClaim) are exactly the documented payloads
    and were reused unchanged. There was no bus, emitter, or publisher to
    conflict with.

An event would need to mutate authoritative state
    NO — every event is a pure read of the resolution. The builder has no
    mutable state and no side effect.

Implementing an event requires inventing a new gameplay rule
    NO — no rule was added. The cycle, the payloads, the depths, and the
    enumeration order are all owned by GAME_EVENTS.md and MATCH3_RULES.md.

The event requires a field that does not exist and is not contractually defined
    NO — every payload member is either an existing Domain value or a value the
    documentation names (Cascade depth index; New Combo value). No field was
    invented, and PlayerState's two members were read, not extended.
```

No contradiction was found between `GAME_EVENTS.md` §1–§2, `GAME_RULES.md`
§16–§17, `MATCH3_RULES.md` §3.2/§4/§5.5.5/§6, `GAME_STATE.md` §2.2/§5.1, and
`SIGNALR_PROTOCOL.md` §3.1/§8. No documentation was changed.

---

## Acceptance Criteria

```text
[✓] Documented Match events are emitted              MatchCreated, per MatchResolution
[✓] Documented Combo events are emitted              ComboChanged, the NEW value
[✓] Documented Cascade events are emitted            CascadeCreated, depth d − 1
[✓] GemMatched emitted per §1.1/§2                   once per cleared cell, §1.3 order
[✓] Ordering is deterministic                        §1.1 cycle; §7.2 item 4 respected
[✓] Ordering follows Resolution.Passes → Matches     no second pass, no new order
[✓] Rejected Swaps produce no resolution events      structural; all 5 reasons proven
[✓] Events do not mutate state                       pure read; 5 tests
[✓] No duplicate resolution pipeline exists          one Execute, one Resolve, one walk
[✓] No undocumented event types introduced           enum is exactly the 4 names
[✓] No event infrastructure introduced               asserted reflectively
[✓] No new SignalR message or method                 Hub untouched
[✓] Focused tests pass                               46 new
[✓] Full backend regression passes                   604 passed, 0 failed
[✓] Build passes with zero warnings/errors           Debug and Release
[✓] Unrelated files remain untouched                 scope audit below
[✓] Task file is moved to tasks/completed/           this file
```

**Scope audit.** `git status` shows many modified and untracked files this task
did not produce: the working tree already carried TASK-001 through TASK-005's
documentation, sources, tests, and task files (the same note TASK-003, TASK-004,
TASK-004A, and TASK-005 recorded). The files this task actually wrote are
enumerated in "Files created and modified" above; every other path in `git status`
predates it. Within this task's own set the changes are confined to: two new
Domain types, one new test file, the existing executor's result and call
sequence, two corrected comments in the Application boundary, and the one test
blocklist the new event types legitimately forced. No resolver, validator, board
model, accounting rule, Hub, client, or document was modified.

---

## Risks

```text
MatchResolved is not emitted.
    §2 defines its payload and §1.1 gives it a position, but the resolution
    reports detection and not removal, and §2 states MatchCreated is the event
    that corresponds to the Match count. Emitting it would require inventing a
    removal report the pipeline does not produce. Low risk: nothing consumes it
    yet. The next stage that needs it (Passive, which reads Match events) will
    need a contract decision on whether resolution must also report removal.

GemMatched is emitted although the task's "at minimum" list did not name it.
    It is defined by §2, placed on the §1.1 cycle, and its payload value already
    existed unimplemented. Emitting it completes §1.1 rather than exceeding it.
    If the intent was to defer it, removing one foreach pair from the builder is
    the whole change.

Delivery is not implemented.
    The events are produced and carried on the result but nothing sends them:
    ReceiveEvents (SIGNALR_PROTOCOL.md §3) is a separate stage. The client
    therefore sees no event stream yet, which matches the staged contract in
    SIGNALR_PROTOCOL.md §0.
```

---

## Remaining Issues

Recorded, not fixed (`AGENTS.md` §16):

```text
1. MatchShape.SortKey's L/T placement is an implementation reading of a
   documented ambiguity (MATCH3_RULES.md §3.2 names only two orientation keys
   and an L/T is neither). Recorded in TASK-001 and TASK-002; unchanged here.
   Impact: the position of an L/T among straight shapes in one pass. The events
   reproduce whatever order the match set has, so they inherit the reading
   rather than introducing one. Suggested follow-up: give §3.2 an explicit L/T
   ordering rule.

2. swapOriginIndex is passed as null from SwapExecutor.Execute
   (MATCH3_RULES.md §5.5.3 items 1–2). Recorded in TASK-004 and TASK-005;
   unchanged here. Impact: a Match 4/5 Special Gem's placement on the Swap's
   first pass. Suggested follow-up: thread the accepted swap's moved-into cell.

3. MatchResolved and the action-boundary events (TurnStarted/TurnEnded,
   SwapStarted/SwapResolved) remain unimplemented; see Risks and
   "Not Implemented".
```

---

## Final Report

```text
TASK-006 — Battle Event Emission

Status:
DONE

Events Implemented:
MatchCreated, CascadeCreated, ComboChanged, GemMatched — the four names
GAME_RULES.md §16 lists and GAME_EVENTS.md §2 defines that the Match-3
resolution produces. Each is a Domain BattleEvent value carrying that event's
own documented payload: MatchCreated carries the resolution's own
MatchResolution (shape/cells, Gem type, tier, created Special Gems);
CascadeCreated carries the Cascade depth index (d − 1); ComboChanged carries the
NEW Combo value; GemMatched carries the existing GemMatchedEvent (cell position,
Gem type, Special Gem consumed there). No MatchCountChanged, SpecialGemActivated,
SpecialGemCreated, TurnChanged, SequenceChanged, or BoardChanged exists —
MatchCountChanged and the Special Gem events are excluded by explicit
documentation, and Turn/Sequence/the board are delivered as state.

Event Ordering:
Per pass in Resolution.Passes order; CascadeCreated first when the pass is a
Cascade; then per Match in the pass's §3.2 order — MatchCreated, the pass's
matched-cell GemMatched reports in ascending §1.0 index, ComboChanged; then the
pass's activation GemMatched reports in ascending §1.0 index. Every position is
the resolver's own: no second MatchDetector pass, no new sort, no derivation
from cleared cells or animation order. The terminating pass contributes nothing
because it is absent from Passes.

State Interaction:
None. Events are outputs only. The builder is a pure function of the resolution
and the accounted Combo: BattleState, PlayerState, and BoardState are read never
written, no RNG is consumed, Turn and Sequence keep their +1-per-committed-Swap
rule, and LastCommittedSwapPair is left to the Swap. PlayerState.MatchCount and
PlayerState.Combo remain TASK-005's authoritative state; the builder consumes the
accounted Combo as its endpoint and recomputes nothing. One write-back preserved.

Rejected Swap:
No gameplay Battle Event of any kind. Execute returns before the resolution, so
the guarantee is structural. SwapExecutionResult.Rejected carries an empty event
list (never null) and no state. Verified for INVALID_CELL_INDEX, INVALID_SWAP,
STALE_ACTION (both argument orders), and NO_MATCH_FROM_SWAP, and verified that the
event stream of a later committed Swap is identical whether or not those
rejections were sent.

Determinism:
Identical BattleState + SwapRequest produce a byte-identical event sequence. The
sequence depends only on Resolution.Passes, its match sets, and the Cell indices
the resolver already ordered. No RNG, timestamp, GUID, random id, hash, or
unordered collection participates. Asserted over 20 repeated runs, both request
argument orders, and all 112 adjacent pairs of a generated board.

Tests:
46 new tests in BattleEventEmissionTests.cs (Domain 535 = 489 + 46). One existing
test updated narrowly: TASK-005's Accounting_ShouldIntroduceNoEventOrMessageSurface
forbade the four event names outright, which GAME_EVENTS.md §3 item 7 recorded as
a sequencing position rather than a scope reduction; it now forbids event
infrastructure and undocumented event names instead — strictly stronger, and
re-asserted independently by the new file. No test weakened, skipped, or deleted.

Build:
Debug and Release: 0 Warning(s), 0 Error(s). Backend 604 passed, 0 failed
(Domain 535, Application 41, Infrastructure 1, Api 27).

Files Created:
src/backend/GameServer.Domain/Match3/BattleEvent.cs
src/backend/GameServer.Domain/Match3/BattleEventBuilder.cs
tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs
tasks/completed/TASK-006-battle-event-emission.md

Files Modified:
src/backend/GameServer.Domain/Match3/SwapExecution.cs
src/backend/GameServer.Application/Battle/BattleStateService.cs  (comment correction)
tests/backend/GameServer.Domain.Tests/MatchComboAccountingTests.cs  (one test)

Documentation Changes:
None. GAME_EVENTS.md §1–§3, GAME_RULES.md §16–§17, MATCH3_RULES.md §3.2/§4/§6,
and GAME_STATE.md §2.2/§5.1 already define the whole contract and were
implemented, not edited. No ADR — no state-model or architectural decision was
made.

Not Implemented:
Passive, Power, Cards, Relics, Combat, Damage, HP, Boss, Rewards, BattleWon/
BattleLost, BattleStarted, TurnStarted/TurnEnded, SwapStarted/SwapResolved,
MatchResolved, multiplayer, Redis persistence, database schema, frontend event
rendering, ReceiveEvents or any new SignalR message, and any event bus, broker,
emitter, publisher, subscriber, or event log. No resolver, validator, board rule,
Special Gem rule, swap semantic, or accounting rule was changed.

STOP Conditions:
None fired. Every payload is documented; the ordering agrees with
MATCH3_RULES.md §4; the existing event values are exactly the documented
payloads and were reused; no event needs to mutate state; no rule was invented;
and no field outside the documented contract was required.

Next Task:
Not started, per this task's instruction. Delivering the produced batch over
ReceiveEvents (SIGNALR_PROTOCOL.md §3, §3.1) is the natural next stage, as is the
Passive stage that consumes Match events (PASSIVE_RULES.md §2, §5) — the latter
will need the MatchResolved question in Risks resolved first.
```

---

## Status

DONE

---

## Handoff

The event stream is produced, ordered, deterministic, and carried on
`SwapExecutionResult.Events`, but nothing delivers it yet: `ReceiveEvents`
(`SIGNALR_PROTOCOL.md` §3) is unimplemented, so the client sees the state push
only. A delivery task needs no contract decision — §3 and §3.1 define the batch,
its atomicity, and the rule that it is sent after the write-back, and
`GAME_EVENTS.md` §3 item 1 leaves the envelope and JSON schema to that stage.
A Passive task additionally needs the `MatchResolved` question in Risks settled,
since `GAME_EVENTS.md` §2 defines its payload while the resolution reports
detection and not removal.