# TASK-013 — Integrate PassiveTracker with Application-Layer Resolution

## Metadata

```text
Task ID:           TASK-013
Type:              FEATURE
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: N/A
Workflow:          development/feature.md
Skills:            N/A
Dependencies:      TASK-009, TASK-010, TASK-011, TASK-012, TASK-014
```

---

## Objective

Wire PassiveTracker.Charge() into the Application-layer Swap resolution pipeline: add PetState to BattleState, call PassiveTracker.Charge() after CascadeResolver.Resolve(), append PassiveCharged/PassiveTriggered events to the resolution's event list, and write the settled PassiveProgress back to PetState.

---

## Context

TASK-012 completed the Domain-layer PassiveTracker (batch-per-Cascade model, all three reset behaviors). The tracker is a pure function with no caller. The Application layer's BattleStateService.ExecuteSwap() delegates to SwapExecutor.Execute() which returns SwapExecutionResult (state + events), but never calls PassiveTracker.Charge() and has no PetState to read/write.

The documentation requires:
- PetState is part of BattleState (GAME_STATE.md §2.3)
- PassiveCharged is emitted per Match (GAME_EVENTS.md §2)
- PassiveTriggered is emitted when threshold is crossed (GAME_EVENTS.md §2)
- PassiveProgress is written back to PetState after resolution (GAME_EVENTS.md §2, PassiveChargeResult.Progress)
- Charge sits at step 10 of GAME_RULES.md §17, after Count Matches and before Trigger Relics

---

## Authoritative Sources

- `docs/02-technical/GAME_STATE.md` §2.3 — PetState (PassiveId, PassiveProgress, PassiveResetOverride)
- `docs/02-technical/GAME_STATE.md` §5.1 — post-resolution write-back
- `docs/02-technical/GAME_EVENTS.md` §1.1 — event order (PassiveCharged per Match, PassiveTriggered on crossing)
- `docs/02-technical/GAME_EVENTS.md` §2 — PassiveCharged/PassiveTriggered payloads
- `docs/01-game-design/GAME_RULES.md` §17 step 10 — "Charge Passive" in resolution order
- `docs/01-game-design/PASSIVE_RULES.md` §2, §4, §5 — charging, reset, cascade behavior
- `docs/02-technical/ARCHITECTURE.md` §3 — PassiveTracker (Domain), BattleStateService (Application)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4.3 — PetState wire delivery contract (petState nested object)
- `docs/AGENTS.md` §13 — Technical Boundaries (Backend = Domain + Application)

---

## Scope

### In Scope

- Create `PetState` type in Domain/Battle with Passive-related fields (PassiveId, PassiveProgress, PassiveResetOverride)
- Add `PetState` field to `BattleState`
- Update `BattleState.Create()` to accept Passive configuration and initialize PetState
- Call `PassiveTracker.Charge()` after CascadeResolver.Resolve() in the Application layer
- Append PassiveCharged/PassiveTriggered events to SwapExecutionResult's event list
- Write settled PassiveProgress back to PetState
- Unit tests for the integration

### Out of Scope

- Pet selection system (PetId, Element, Tier, Star, Level) — not yet implemented
- Pet definition configuration (thresholds, effects) — balance values, not implemented
- Combat integration (step 11–19 of §17) — separate tasks
- Relic/Card integration — separate tasks
- Redis persistence of PetState — deferred (REDIS_STATE.md §7)
- Pet identity/progression delivery (PetId, Element, Tier/Star/Level) — not yet implemented

---

## Current State

`BattleState` at `src/backend/GameServer.Domain/Battle/BattleState.cs`:
- Has: BattleId, Turn, Sequence, RngSeed, RngState, BoardState, PlayerState, LastCommittedSwapPair
- Missing: PetState (§2.3)

`BattleStateService` at `src/backend/GameServer.Application/Battle/BattleStateService.cs`:
- ExecuteSwap() calls SwapExecutor.Execute() and stores the result
- No PassiveTracker.Charge() call
- No PetState read/write

`SwapExecutionResult` at `src/backend/GameServer.Domain/Match3/SwapExecution.cs`:
- Returns: State (BattleState), Resolution (CascadeResult with TotalMatches), Events (IReadOnlyList<BattleEvent>)
- Events list is immutable after construction

`PassiveTracker` at `src/backend/GameServer.Domain/Passives/PassiveTracker.cs`:
- Charge(PassiveProgress, matchCount, PassiveId, reset) → PassiveChargeResult
- Returns: Progress (PassiveProgress), Charges (list), Triggers (list)
- Complete and tested (TASK-012)

---

## Acceptance Criteria

- [ ] `PetState` type exists in `GameServer.Domain.Battle` with PassiveId, PassiveProgress, PassiveResetOverride
- [ ] `BattleState` includes a `PetState` field
- [ ] `BattleState.Create()` requires PassiveId, PassiveThreshold, and optional PassiveResetOverride — PetState is not optional, not defaulted, and is initialized at battle creation consistent with GAME_STATE.md §2.3 item 3 ("present from battle creation")
- [ ] `BattleState.Create()` initializes PetState with progress at 0
- [ ] `SwapExecutionResult.WithEvents(IReadOnlyList<BattleEvent>)` exists and returns a new immutable result containing the supplied events, preserving all other SwapExecutionResult state (IsAccepted, Reason, State, Resolution) without mutating the original
- [ ] After a committed Swap resolution, PassiveTracker.Charge() is called with PetState's PassiveProgress and CascadeResult.TotalMatches
- [ ] PassiveCharged events are appended to the resolution's event list (one per Match)
- [ ] PassiveTriggered event is appended when threshold is crossed (0 or 1 per Cascade)
- [ ] Settled PassiveProgress is written back to PetState in BattleState
- [ ] Rejected Swaps do not call PassiveTracker.Charge()
- [ ] `BattleStateService` (or BattleHub projection) produces the §4.3 `petState` wire shape: `passiveId` (string), `passiveProgress` (`{ threshold, current }`), and `passiveResetOverride` (omitted when Default, `"Partial"` or `"NoReset"` when non-default) — following the §4.2 `playerState` projection precedent
- [ ] All existing tests pass
- [ ] New tests cover: charge integration, progress write-back, event appending, rejected-swap-no-charge

---

## Affected Areas

```text
[x] Domain (GameServer.Domain/) — Battle/PetState.cs (new), BattleState.cs (add field), Match3/SwapExecution.cs (add WithEvents)
[x] Application (GameServer.Application/) — BattleStateService.cs (integrate PassiveTracker)
[ ] API (GameServer.Api/)
[ ] Client (client/)
[x] SignalR / Redis — BattleHub projection: petState wire shape per §4.3
[ ] PostgreSQL
[x] Tests (tests/)
[ ] Documentation (docs/) — doc comments only
[ ] ADR
```

---

## Implementation Notes

### PetState Design

Follow the PlayerState staged pattern (§2.2): PetState exists with only the fields this stage requires. Other §2.3 fields (PetId, Element, Tier) are added by their owning tasks.

```csharp
public readonly record struct PetState(
    PassiveId PassiveId,
    PassiveProgress PassiveProgress,
    PassiveResetBehavior? PassiveResetOverride = null)
```

- PassiveId: set at battle creation, never changes (§2.3 item 2)
- PassiveProgress: starts at (Threshold, 0), written after each resolution
- PassiveResetOverride: null means Default (§4 item 3, §2.3: "only present if non-default")

### BattleState Change

Add `PetState PetState` parameter to BattleState record and Create() factory. The existing constructor call in Create() must pass PetState.

### Integration in BattleStateService

After SwapExecutor.Execute() returns an accepted result:
1. Read current PassiveProgress from state.PetState
2. Call PassiveTracker.Charge(progress, resolution.TotalMatches, passiveId, reset)
3. Create new BattleState with updated PassiveProgress
4. Append PassiveCharged + PassiveTriggered events to the event list
5. Return modified SwapExecutionResult (or a wrapper)

The event-appending challenge: SwapExecutionResult is immutable. Options:
- Add a `WithEvents(IReadOnlyList<BattleEvent>)` method to SwapExecutionResult (Domain change, cleanest)
- Have BattleStateService construct a new result type (Application concern, avoids Domain change)
- Append events before SwapExecutor returns (requires SwapExecutor to know about Passive — wrong boundary)

Recommendation: Add `WithEvents` to SwapExecutionResult in Domain. It is a pure factory that creates a new immutable value — it changes no behavior, only enables the documented event-assembly step.

### PassiveCharged Event Order

The documentation places PassiveCharged per-Match in detection order (§1.1). Since PassiveTracker.Charge() takes a total match count, the charge events it returns are sequential (P+1, P+2, ..., P+N). These should be appended in order after the board resolution's existing events. The exact position within the event list should follow §1.1: after ComboChanged and before the next cascade pass's CascadeCreated.

For the initial integration, appending all Passive events after the board resolution's events is acceptable. Fine-grained per-Match event positioning within the detection pass is a follow-up refinement.

### Battle Creation

`PetState` is required at battle creation — GAME_STATE.md §2.3 item 3: "It is present from battle creation." `BattleState.Create()` must accept PassiveId and PassiveThreshold as required parameters (PassiveResetOverride is optional, defaulting to Default). PetState is never absent, never lazily initialized, and never silently defaulted with invented values.

Since Pet selection is not implemented, BattleState.Create() needs PassiveId and Threshold from the caller. For tests: provide PassiveId and Threshold directly.

### Wire Projection (§4.3)

TASK-014 resolved that `PetState` is delivered through `BattleStateUpdated` as a nested `petState` object. The wire shape is defined by `SIGNALR_PROTOCOL.md` §4.3:

- `passiveId`: string, always present
- `passiveProgress`: `{ threshold: int, current: int }`, always present
- `passiveResetOverride`: `"Partial"` | `"NoReset"`, present only when non-default; omitted (never null) for Default reset

The projection follows the §4.2 `playerState` precedent: a pure field mapping from the Domain `PetState` record to the wire object, owned by the transport layer. `BattleStateService` or `BattleHub` must produce this shape from the `PetState` field that TASK-013 adds to `BattleState`.

---

## Testing Requirements

### Test Types Required

```text
[x] Unit tests         — PetState, BattleState (with PetState), BattleStateService (integration)
[ ] Integration tests
[ ] Gameplay scenarios
[ ] API tests
[ ] Realtime tests
[ ] Persistence tests
```

### Key Edge Cases

- Committed Swap with N Matches → N PassiveCharged events, PassiveProgress updated
- Committed Swap where threshold crossed → PassiveTriggered appended, progress reset per behavior
- Committed Swap where threshold not crossed → no PassiveTriggered, progress carries
- Rejected Swap → no PassiveTracker.Charge() call, no event changes
- PassiveProgress persists across Swaps (accumulated progress carries between turns)
- Partial Reset: overflow carries to next Swap's charge
- NoReset: progress persists and may re-trigger on next Swap

---

## Documentation Impact

**Option A — None:**
> This task implements already-documented behavior (GAME_STATE.md §2.3,
> GAME_EVENTS.md §2, GAME_RULES.md §17 step 10). Code doc comments will
> be updated to match, but no docs/ file changes are needed.

---

## Stop Conditions

- If PetState's fields in GAME_STATE.md §2.3 contradict another authoritative doc: STOP per AGENTS.md §4
- If PassiveCharged event ordering in GAME_EVENTS.md §1.1 contradicts another doc: STOP per AGENTS.md §4

---

## Blocker

**RESOLVED by TASK-014** — the PetState wire delivery contract is now decided
and documented. The original report is retained below for traceability.

**Problem:** `GAME_STATE.md` §2.3 defines `PetState` as a `BattleState` field
present from battle creation. `SIGNALR_PROTOCOL.md` §4 item 4 delivers
`BattleState` fields through `BattleStateUpdated`. There is no documented
exclusion for `PetState` (unlike `LastCommittedSwapPair`, §4 item 12).
`PASSIVE_RULES.md` §6 item 1 requires passive progress to be exposed via a
UI-facing value. TASK-013's Out of Scope claimed "Client-facing PetState
delivery — deferred" but this exclusion was not documented in
`SIGNALR_PROTOCOL.md`. Existing tests (`BattleStateTests.cs:133`,
`ApiIntegrationTests.cs:449`) asserted `PetState` is absent from
`BattleStateUpdated`.

**Relevant sources:**
- `docs/02-technical/GAME_STATE.md` §2.3 — PetState is a BattleState field
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4 items 4, 11 — BattleStateUpdated delivers stage fields
- `docs/01-game-design/PASSIVE_RULES.md` §6 item 1 — UI-facing visibility requirement
- `tests/backend/GameServer.Domain.Tests/BattleStateTests.cs:133` — PetState absent assertion
- `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs:449` — petState absent assertion

**Resolution (TASK-014 DONE):** `PetState` **is** delivered through
`BattleStateUpdated` as a nested `petState` object carrying `passiveId`,
`passiveProgress` (`{ threshold, current }`), and — only when non-default —
`passiveResetOverride` (`"Partial"` | `"NoReset"`). The contract is owned by
`SIGNALR_PROTOCOL.md` §4.3, and §4 item 13 records why item 12's exclusion
precedent does not apply to it. The two "PetState absent" assertions have been
removed from the absent-field lists and replaced with assertions of the §4.3
shape. TASK-013's Out of Scope line was replaced accordingly: delivery is now
in scope, and TASK-013's `BattleState` field addition makes it live.

**Waiting for:** nothing — this task is unblocked.

---

## Dependencies

- TASK-009 (DONE) — PassiveTracker
- TASK-010 (DONE) — Partial Reset resolved
- TASK-011 (DONE) — Single trigger per Cascade
- TASK-012 (DONE) — Batch model implementation
- TASK-014 (DONE) — PetState wire delivery contract resolved

---

## Completion Evidence

### Summary

`PassiveTracker.Charge()` is wired into the Application-layer Swap resolution
pipeline, and `PetState` now exists in Domain Battle state, is created at battle
creation, is written back after each committed Swap, and is delivered on the
`BattleStateUpdated` wire as the §4.3 `petState` object.

The board pipeline (`SwapExecutor`) remains the Match-3 owner and builds no
Passive event. The Passive stage sits at the pipeline step that owns
`GAME_RULES.md` §17 step 10 ("Charge Passive", after step 9 "Count Matches" and
before step 11 "Trigger Relics"), reads the progress the authoritative
`PetState` holds, and hands the assembled event list and the extended state back
through the two pure factories TASK-013 added.

### Changes

**Domain (`src/backend/GameServer.Domain/`)**

```text
Battle/PetState.cs           NEW — PassiveId, PassiveProgress,
                             PassiveResetOverride?; presence/absence semantics
                             per GAME_STATE.md §2.3 and PASSIVE_RULES.md §4.
                             AtBattleCreation(id, threshold, reset?) applies the
                             documented starting progress (Threshold, Current=0).
                             Derived readings ResetBehavior (absent ⇒ Default) and
                             HasResetOverride. No PetId/Element/Tier/Star/Level.

Battle/BattleState.cs        ADDED the required `PetState PetState` record field
                             (non-nullable, non-optional) and Create(...)
                             overloads that require the Passive configuration:
                               Create(battleId, rngSeed, PetState)
                               Create(battleId, rngSeed, PassiveId, int,
                                      PassiveResetBehavior? = null)
                             CreateWith(battleId, rngSeed) — test-only back-compat
                             helper carrying a fixture Passive, so the earlier
                             stages' suites stay readable; Production code never
                             calls it (CreateBattle takes the real configuration).
                             PetState is initialized at creation with progress 0
                             and is not defaulted, stubbed, or lazily created.

Match3/SwapExecution.cs      ADDED SwapExecutionResult.WithEvents(events) and
                             WithState(state) — pure factories over the value;
                             both throw on a rejection (a rejected action writes
                             nothing and emits nothing). Added a doc note that the
                             Passive stage's events are deliberately NOT built by
                             SwapExecutor, and updated the flow diagram.
                             Committed(...) stays internal; the Application step
                             composes the two factories over it.

Match3/BattleEvent.cs        ForPassiveCharged/ForPassiveTriggered made public —
                             the Passive stage's events are produced by the
                             Application step, not inside this assembly's executor
                             (the Match-3 factories stay internal).
```

**Application (`src/backend/GameServer.Application/Battle/BattleStateService.cs`)**

```text
PassiveConfiguration        NEW nested readonly record struct — the battle's
                            Passive loadout input (PassiveId, PassiveThreshold,
                            PassiveResetOverride?) with ToPetState(). Pet
                            selection is not implemented, so the battle's Passive
                            is supplied by its creator; the Threshold is
                            data-driven configuration, never invented here.

CreateBattle                NOW REQUIRES the configuration and initializes
                            PetState from it. No value is invented for
                            PassiveId or the Threshold.

ExecuteSwap                 After an accepted Swap: charges the Passive via
                            PassiveTracker.Charge(PetState.PassiveProgress,
                            Resolution.TotalMatches, PetState.PassiveId,
                            PetState.ResetBehavior); writes the settled
                            PassiveProgress back into BattleState.PetState in the
                            same write-back; appends PassiveCharged per Match and
                            the single PassiveTriggered; returns the result via
                            WithEvents(...).WithState(resolved).
                            A REJECTED Swap returns the executor's result
                            untouched — Charge is never called.
```

**Api (`src/backend/GameServer.Api/Hubs/`)**

```text
BattleHub.cs                BattleStateUpdated gained `PetState`; NEW
                            PetStatePayload(passiveId, passiveProgress
                            {threshold, current}, passiveResetOverride?) and
                            PassiveProgressPayload. ToPetStatePayload is a pure
                            field mapping. `passiveResetOverride` is omitted —
                            never JSON null — when Default, via a per-member
                            JsonIgnore(WhenWritingNull), so the rule travels with
                            the contract (§4.3 item 7) rather than a serializer
                            policy. Stale "§4.4" cross-references corrected.

BattleEventWireProjection   §3.3 schema added for the two Passive events:
                            passiveId, progress, threshold; PassiveTriggered
                            carries no `effect summary` (GAME_EVENTS.md §2 item 3
                            records it as deferred). Discriminator set extended;
                            the unknown-type guard still throws.
```

### Tests

```text
GameServer.Domain.Tests          639 passed, 0 failed  (baseline 624)
GameServer.Application.Tests      53 passed, 0 failed  (baseline 40)
GameServer.Infrastructure.Tests     1 passed, 0 failed
GameServer.Api.Tests               51 passed, 0 failed  (baseline 44 passed / 2 failed)
                                 ─────────────────────
backend total                    744 passed, 0 failed
src/frontend/client (vitest)     174 passed, 0 failed  (unchanged; consumes the
                                 payload by named member and tolerates extra ones)
```

New coverage (TASK-013's required cases):

```text
Domain   PetState contract: creation at progress 0 against the supplied
         Threshold; required configuration (no omitting overload); declared
         non-default override recorded; absent override ⇒ Default; the three
         documented data members and no PetId/Element/Tier/Star/Level.
         BattleState: PetState in the exact field set; no later-stage field.
         WithEvents/WithState: new value returned, all other members preserved,
         original not mutated, both reject a rejection, both reject null.

Application
         create initializes PetState from the configuration;
         charge integration (one PassiveCharged per Match, ascending, reporting
         the battle's PassiveId and Threshold) with the settle value derived from
         the documented batch comparison, not from the code;
         event appending order (board cycle first, then the charges, then the
         single trigger last);
         progress write-back, and that the returned result carries the SAME
         write-back the registry stores;
         committed Swap where the threshold is crossed ⇒ exactly one
         PassiveTriggered with pre-reset progress (Default/Partial/NoReset);
         threshold not crossed ⇒ no trigger, progress carries;
         progress carries across Swaps;
         Partial Reset overflow carries (current − Threshold);
         NoReset keeps the batch total;
         REJECTED swap ⇒ no Charge, no events, progress untouched;
         Rejected Swap ⇒ no Passive event and the state object is unchanged;
         ResolveBoard does not charge.

Api      §4.3 shape on join: passiveId, passiveProgress {threshold, current};
         reset override omitted for Default and never written as null or
         "Default"; override present as "Partial"/"NoReset" (contract name, not
         ordinal) when declared;
         settled progress delivered on every committed Swap's state push and
         equal to the authoritative state's value;
         no Pet/Passive member anywhere else (top level or board);
         no Passive-specific message or hub method;
         §3.3 event members in the ReceiveEvents batch, including that
         PassiveCharged count == MatchCreated count, at most one
         PassiveTriggered, and the trigger last;
         the batch still equals the resolution's own order member-for-member.
```

### Acceptance criteria

```text
[x] PetState type in GameServer.Domain.Battle with PassiveId, PassiveProgress,
    PassiveResetOverride
[x] BattleState includes a PetState field
[x] BattleState.Create() requires PassiveId, PassiveThreshold, and optional
    PassiveResetOverride; PetState is not optional, not defaulted, not lazily
    initialized (GAME_STATE.md §2.3 item 3)
[x] BattleState.Create() initializes PetState with progress at 0
[x] SwapExecutionResult.WithEvents(IReadOnlyList<BattleEvent>) exists, returns a
    new immutable result with the supplied events, preserves IsAccepted, Reason,
    State and Resolution, and does not mutate the original
    (+ WithState, required so the extended state and the assembled events are one
    write-back — see "Deviations" below)
[x] After a committed Swap resolution, PassiveTracker.Charge() is called with
    PetState's PassiveProgress and CascadeResult.TotalMatches
[x] PassiveCharged appended one per Match
[x] PassiveTriggered appended when the threshold is crossed (0 or 1 per Cascade)
[x] Settled PassiveProgress written back to PetState in BattleState
[x] Rejected Swaps do not call PassiveTracker.Charge()
[x] The §4.3 petState wire shape is produced (passiveId string, passiveProgress
    {threshold, current}, passiveResetOverride omitted when Default, "Partial" /
    "NoReset" when non-default), following the §4.2 playerState precedent
[x] All existing tests pass
[x] New tests cover charge integration, progress write-back, event appending,
    and rejected-swap-no-charge
```

### Deviations from the task's Implementation Notes (and why)

```text
1. `WithState` was added alongside `WithEvents`.
   The task (§ Implementation Notes) assumed the extended state could ride the
   result built by SwapExecutor, with WithEvents replacing only the event list.
   That is not sufficient: the settled PetState write-back happens in the
   Application step, so a result returned with the executor's pre-charge state
   would report events for a resolution whose state has not been written back —
   splitting the single post-resolution write-back GAME_STATE.md §5.1 requires
   and letting a caller observe a state where the Passive is uncharged while the
   batch already reports its charges. WithState is the same kind of pure factory
   and is what makes the returned result the stored write-back.

2. `BattleState.CreateWith` is a test-only back-compat helper.
   Making PetState required (acceptance criterion 3) breaks every earlier-stage
   creation site. Rather than let those suites settle on an arbitrary Passive, the
   one fixture value lives in one clearly-marked helper that no production caller
   uses — CreateBattle takes the battle's real configuration. The earlier suites'
   own contracts are unchanged.

3. The §3.3 wire schema for PassiveCharged/PassiveTriggered was implemented.
   Appending the two events to the resolution's event list puts them in the
   ReceiveEvents batch, which is serialized (§3.2.1 item 1: BattleEvent is not a
   wire DTO and cannot be serialized directly). Without a §3.3 item shape the
   send would throw, so the batch could not carry the events the acceptance
   criteria require. TASK-014's own ApiIntegrationTests comments already
   enumerated `passiveId`/`passiveProgress` for this stage.

4. PassiveCharged/PassiveTriggered ordering.
   The task's Implementation Notes record that "appending all Passive events
   after the board resolution's events is acceptable" and that "fine-grained
   per-Match event positioning within the detection pass is a follow-up
   refinement". That is what was implemented: the board cycle's events first
   (MatchCreated … ComboChanged, per pass), then the tracker's charges in Match
   order, then the single trigger. GAME_EVENTS.md §17's "Charge Passive" position
   after "Count Matches" is respected; intra-pass interleaving is left to the
   follow-up the task itself names.
```

### Source consistency check

```text
GAME_STATE.md §2.3 / §2.5    UNCHANGED and implemented — PetState is present
                             from creation, carries Progress at (Threshold, 0),
                             and is written in the single §5.1 write-back.
                             No §2.3 field beyond the three this stage owns was
                             added (no PetId/Element/Tier/Star/Level).
GAME_RULES.md §17 step 10    SATISFIED — Charge Passive runs after Count Matches
                             and before Trigger Relics; step 11+ is untouched.
PASSIVE_RULES.md §2, §4, §5  UNCHANGED and delegated — the Application step
                             calls PassiveTracker and re-implements no rule; the
                             §5 worked examples are the tests' expectations.
GAME_EVENTS.md §1.1, §2      SATISFIED — one PassiveCharged per Match, at most
                             one PassiveTriggered per Cascade, pre-reset progress
                             on the trigger, no `effect summary` invented.
SIGNALR_PROTOCOL.md §3.2/§3.3, §4.3
                             SATISFIED — petState has exactly the three §4.3
                             members, the override is omitted rather than null,
                             the events travel on the existing ReceiveEvents
                             batch, and no message/method/subscription was added.
ARCHITECTURE.md §3           RESPECTED — PassiveTracker stays Domain; the
                             sequencing sits in the Application boundary; the
                             projection sits in Api. No boundary moved.
REDIS_STATE.md §7            UNCHANGED — no persistence was added; §4.3 item 12
                             and §7 leave PetState storage exactly as deferred.
```

No documentation conflict was found: `GAME_STATE.md` §2.3, `GAME_EVENTS.md` §2,
`PASSIVE_RULES.md` §2/§4/§5, `GAME_RULES.md` §17 step 10, and
`SIGNALR_PROTOCOL.md` §4.3 agree, so none of the task's Stop Conditions fired
and no `docs/` file was changed.

### Validation (core/validation.md — MEDIUM)

```text
Documentation validation   done — every behaviour traced to its owning section
                           (§ Source consistency check).
Build/compile validation   done — `dotnet build src/backend/GameServer.sln`:
                           Build succeeded, 0 warnings, 0 errors.
Unit tests                 done — Domain 639, Application 53.
Integration tests          done — Api 51 (the change crosses the Hub/SignalR
                           boundary and the state write-back), Infrastructure 1.
Gameplay scenarios         done — the state-transition chain
                           Swap → Match → Cascade → Combo → Passive is asserted
                           end-to-end, including the three Reset Behaviors and
                           the overflow/carry cases from PASSIVE_RULES.md §5, and
                           that a rejected Swap transitions nothing.
Final review               done — see the checklist below.
```

### Review (quality/review.md)

```text
Correctness   PASS — matches GAME_STATE.md §2.3, GAME_EVENTS.md §2,
              GAME_RULES.md §17 step 10, PASSIVE_RULES.md §2/§4/§5,
              SIGNALR_PROTOCOL.md §3.3/§4.3.
Architecture  PASS — Domain computes (PassiveTracker), Application sequences
              (§17), Api projects. No rule implemented outside its owning layer;
              the writer of PetState progress is the tracker's output, not this
              layer's arithmetic.
Scope         PASS — no Pet selection/progression, no Pet identity beyond the
              Passive fields, no combat, relics, cards, Redis persistence, or
              unrelated SignalR change. The three additions beyond the task's
              literal notes are reported in "Deviations" above.
Tests         PASS — every acceptance criterion and every "Key Edge Case" in the
              task's Testing Requirements has a test; expectations come from the
              rule documents, not from the implementation.
Documentation PASS — no docs/ change needed; doc comments updated to match
              (TASK-014's three follow-up notes are addressed).
Security      N/A — no auth, credential, or data-exposure surface touched.
Performance   PASS — no I/O, query, or repository added on the resolution path;
              the charge is O(Matches) over the list already in hand.
Maintainability PASS — no new interface, factory, bus, or repository; one small
              configuration record and two pure value factories.
Determinism   PASS — server-authoritative and deterministic: the charge is a pure
              function of state + Match count, consumes no RNG, and adds no
              randomness. The client still authors no Passive value
              (GAME_RULES.md §18, ADR-001).
```

### Remaining issues (reported, not fixed — AGENTS.md §16)

```text
1. src/frontend/client/tests/GameRuntime.test.ts:612 lists the expected
   BattleStateUpdated top-level keys and does not include `petState`. It still
   passes (the client reads named members and ignores extras), and client-side
   rendering of Passive state is outside TASK-013's scope, but the list is now an
   incomplete description of the implemented stage.
   Impact: client test documentation only. Suggested follow-up: a client task
   that models `petState` in BattleStateUpdatedPayload/RuntimeBattleState and
   renders `current / threshold` (PASSIVE_RULES.md §6 item 1).

2. Per-Match positioning of PassiveCharged within the detection pass is not
   implemented; all Passive events follow the board resolution's events. This is
   the refinement TASK-013's Implementation Notes explicitly defer.
   Impact: GAME_EVENTS.md §1.1's finest-grained interleaving is not yet exact.
   Suggested follow-up: a task that interleaves PassiveCharged per Match inside
   the pass cycle.

3. BattleState.CreateWith (test-only fixture helper) lives in production Domain.
   It exists because the test project cannot see internals in the other
   direction. Impact: a test-shaped member on a Domain type. Suggested follow-up:
   if the suite grows more such helpers, move the fixture into the test project
   via InternalsVisibleTo configuration.
```

---
