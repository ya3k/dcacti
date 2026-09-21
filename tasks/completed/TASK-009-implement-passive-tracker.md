# TASK-009 — Implement Passive Tracker

---

## Metadata

```text
Task ID:           TASK-009
Type:              FEATURE
Status:            DONE
Risk:              LOW
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: N/A
Workflow:          development/feature.md
Skills:            gameplay/gameplay-behavior-derivation.md
Dependencies:      TASK-008 (Element System, DONE)
```

---

## Objective

Implement the Passive Tracker Domain module that tracks match-based passive
progress for the active Pet, detects threshold crossing, emits
`PassiveCharged` and `PassiveTriggered` events, and applies reset behavior
after trigger.

---

## Context

Phase 1 requires one Pet fully implemented including its Passive
(`ROADMAP.md`). The Passive system is step 10 in the Event Resolution
pipeline (`GAME_RULES.md §17`). The Element System (TASK-008) is DONE;
the Passive Tracker is the next unbuilt dependency in the Pet→Passive→Combat
chain. The tracker mechanism is self-contained — Pet-specific effect
resolution (Burn, Shield, Crit) belongs to the Combat/Pet system, not this
task.

---

## Authoritative Sources

- `docs/00-overview/MVP_SCOPE.md` §1 — Pets IN scope
- `docs/01-game-design/GAME_RULES.md` §10 — Passive Rules overview
- `docs/01-game-design/GAME_RULES.md` §16 — PassiveCharged/PassiveTriggered events
- `docs/01-game-design/GAME_RULES.md` §17 step 10 — Charge Passive in resolution order
- `docs/01-game-design/PASSIVE_RULES.md` §1–§8 — full passive rules (structure,
  charging, thresholds, reset, events, MVP reference)
- `docs/02-technical/GAME_STATE.md` §2.3 — PassiveProgress, PassiveResetOverride
- `docs/02-technical/GAME_EVENTS.md` — PassiveCharged, PassiveTriggered definitions
- `docs/02-technical/ARCHITECTURE.md` §1 — PassiveTracker module in Domain

---

## Scope

### In Scope

- PassiveTracker Domain module with:
  - Progress tracking (increment by 1 per Match, PASSIVE_RULES.md §2)
  - Threshold detection (progress reaches threshold → Ready → Trigger)
  - Reset behavior (default: full reset to 0, PASSIVE_RULES.md §4)
  - Support for non-default reset (partial reset, PASSIVE_RULES.md §4.2)
  - Multiple-trigger handling within one cascade (PASSIVE_RULES.md §5)
- PassiveCharged and PassiveTriggered event types added to the BattleEvent
  system

### Out of Scope

- Pet-specific effect resolution (applying Burn, granting Shield, Crit
  modification — these are Combat/Pet system concerns)
- Pet data model, stats, identity, Element, Tier, Star, Level
- Pet Signature Skills
- Relics (Phase 2, MVP_SCOPE.md §2)
- Boss Passives (separate Boss task — Boss Passives use different trigger
  semantics per BOSS_RULES.md §3 and cannot share a match-based tracker)
- BattleResolution pipeline integration (Application layer — separate
  integration task)
- PassiveTriggered effect summary population (depends on Combat System)
- Client-side passive progress UI rendering
- Any system listed in `docs/00-overview/MVP_SCOPE.md` §2 (OUT)

---

## Current State

- Passive-related references exist in code comments only (no implementation):
  - `PlayerState.cs:27` — comment referencing Passive stage
  - `BattleEvent.cs:11` — comment deferring PassiveCharged to later task
  - `MatchDetector.cs:9` — comment referencing Passive ordering
  - `SwapValidator.cs:15` — comment referencing Passive invariant
- `PetState` (GAME_STATE.md §2.3) defines `PassiveProgress` and
  `PassiveResetOverride` fields — state contract exists
- No PassiveTracker class or module exists yet
- No PassiveCharged/PassiveTriggered event types exist yet

---

## Acceptance Criteria

- [x] Given a Pet with Threshold=N, when N Matches occur (including cascade
      Matches), then PassiveTriggered is emitted after the Nth Match
- [x] Given a Pet with Threshold=5, when 3 Matches occur, then
      PassiveCharged is emitted 3 times with progress values 1, 2, 3
- [x] Given a Pet with Threshold=5 and default reset, when the Passive
      triggers, then progress resets to 0
- [x] Given a Pet with Threshold=3 and a cascade producing 7 Matches, then
      the Passive triggers twice (at Match 3 and Match 6), each with its
      own reset
- [ ] ~~Given a Pet with partial reset (Threshold=5, reset by Threshold),
      when the Passive triggers at progress 7, then progress resets to 2~~
      — **NOT SATISFIABLE as written.** See Remaining Issues: the scenario
      presumes progress can exceed the Threshold, which `PASSIVE_RULES.md`
      §2 item 1 (+1 per Match) and §2 item 3 (trigger on reaching it) make
      unreachable. Partial reset is deliberately not implemented; the
      documented conflict is reported rather than guessed.
- [x] Special Gem detonations do NOT increment passive progress
      (PASSIVE_RULES.md §2 item 2)
- [x] PassiveCharged events are emitted in Match detection order
      (PASSIVE_RULES.md §2)
- [x] PassiveTriggered events are emitted with the Pet's Passive identity
      (GAME_EVENTS.md — effect summary population is a follow-up task)
- [x] All relevant tests pass at the depth required by
      `core/validation.md §2` for the task's Risk level
- [x] `quality/review.md §1` checklist passes
- [x] Documentation impact addressed (§ Documentation Impact below) —
      Option A did **not** hold: `GAME_EVENTS.md` §2 and `GAME_STATE.md`
      §2.3 required amendment. See Documentation Changed.

---

## Affected Areas

```text
[x] Domain (GameServer.Domain/) — PassiveTracker module, BattleEvent types
[ ] Application (GameServer.Application/)
[ ] API (GameServer.Api/)
[ ] Client (client/)
[ ] SignalR / Redis (GameServer.Infrastructure/SignalR/, /Redis/)
[ ] PostgreSQL (GameServer.Infrastructure/Postgres/)
[x] Tests (tests/)
[ ] Documentation (docs/)
[ ] ADR (docs/03-decisions/ADR/)
```

---

## Implementation Notes

- The existing `BattleEvent.cs` explicitly defers PassiveCharged and
  PassiveTriggered to a later task (line 11–12). This is that task.
- PassiveTracker should be a pure Domain module (no framework dependencies),
  consistent with the ARCHITECTURE.md §1 Domain layer contract.
- The tracker receives Match count information from the existing
  Match-3 resolution pipeline (steps 1–9 are DONE).
- Effect resolution is NOT part of this task. The tracker emits events
  describing what happened; downstream systems (Combat, Pet) resolve effects.
- The `PassiveResetOverride` field in PetState (GAME_STATE.md §2.3) is only
  present when non-default reset behavior applies. The tracker must handle
  both cases.

---

## Testing Requirements

### Test Types Required

```text
[x] Unit tests         — PassiveTracker: charge, threshold, reset, multiple triggers
[ ] Integration tests  — N/A (Domain-only module, no cross-boundary integration)
[ ] Gameplay scenarios — cascade with multiple passive triggers (PASSIVE_RULES.md §5)
[ ] API tests          — N/A
[ ] Realtime tests     — N/A
[ ] Persistence tests  — N/A
```

### Key Edge Cases

- `PASSIVE_RULES.md §2.2` — Special Gem detonations do NOT count as Match
- `PASSIVE_RULES.md §4.2` — Partial reset (progress reduces by Threshold)
- `PASSIVE_RULES.md §4.3` — No reset / persistent (rare, one-time Battle Start)
- `PASSIVE_RULES.md §5` — Multiple triggers within one cascade resolution
- Threshold of 1 (triggers on first Match)

---

## Documentation Impact

**Option A — None:**
> This task implements already-documented behavior. No doc changes required.

**Actual outcome — Option A did not hold.** Two authoritative documents were
amended, because the `PassiveTriggered` payload the task requires is not
derivable from them as written:

1. `GAME_EVENTS.md` §2 defined `PassiveTriggered`'s payload as exactly one
   member — `effect summary` — which this task must not populate (it depends
   on Combat), and defined no Passive identity for the event, unlike its
   sibling trigger events (`RelicTriggered`'s `RelicId`, `CardCast`'s
   `CardId`, `BossSkillCast`'s `SkillId`). The payload is now
   `PassiveId, new progress value, threshold`, with `effect summary` recorded
   as **deferred** to the Combat stage.
2. `GAME_STATE.md` §2.3 had no field to supply that identity, so `PetState`
   gains `PassiveId`.

Both amendments were approved by the requester before implementation
(`AGENTS.md` §17 steps 1–2). See Documentation Changed for the exact edits.

---

## Stop Conditions

- If the required behavior cannot be fully derived from the
  Authoritative Sources listed above: STOP per `AGENTS.md §7`
- If a Pet Passive effect requires a Combat System feature not yet
  implemented: emit the event without effect summary, report as follow-up
- BattleResolution pipeline integration at step 10 is a separate task

---

## Dependencies

- TASK-008 (Element System, DONE) — existing dependency

---

## Completion Evidence

<!-- TO BE FILLED BY THE AGENT after the task reaches DONE -->

### Summary

Implemented the Passive Tracker as a pure Domain module under
`src/backend/GameServer.Domain/Passives/`: match-based Passive charging (+1 per
Match), threshold detection, reset behavior, and the domain-side
`PassiveCharged` / `PassiveTriggered` reports. Added the two event types to the
existing `BattleEventType` / `BattleEvent` architecture.

The tracker is a pure function of the Matches it is given, the Passive's
Threshold, its reset behavior, and its identity. It detects nothing, reads no
board, consumes no RNG, resolves no effect, and references no Pet/Boss entity,
no Combat type, and no framework type (`ARCHITECTURE.md` §2.1 item 1). The
integration that calls it from `BattleResolution` at `GAME_RULES.md` §17 step 10
is a separate task, as TASK-009 requires.

Two stop conditions fired during discovery, both resolved by requester decision
before any code was written:

1. **`PassiveTriggered` payload/identity was not derivable** (`AGENTS.md` §7).
   `GAME_EVENTS.md` §2 gave the event a single payload member — `effect
   summary` — which this task explicitly must not populate, and defined no
   Passive identity, unlike its sibling trigger events. Resolved by amending
   `GAME_EVENTS.md` §2 and `GAME_STATE.md` §2.3 (docs first, then code, per
   `AGENTS.md` §17).
2. **`PASSIVE_RULES.md` §4 item 2's partial reset is underivable, and one
   reading of it is provably unreachable** (`AGENTS.md` §4/§20). §2 item 1
   raises progress by exactly 1 per Match and §2 item 3 triggers on reaching
   the Threshold, so progress never exceeds the Threshold; "reduce by
   Threshold" is therefore always a reduction to 0, making partial reset
   arithmetically identical to the default. The overflow the rule describes
   cannot occur. Per the requester's decision, partial reset is **left
   unimplemented and reported** rather than guessed (see Remaining Issues).

---

### Changes

**New — Domain module (`src/backend/GameServer.Domain/Passives/`)**

```text
PassiveResetBehavior.cs   new  the documented reset behaviors (PASSIVE_RULES.md §4)
PassiveProgress.cs        new  PassiveId + PassiveProgress (GAME_STATE.md §2.3)
PassiveEvents.cs          new  PassiveChargedEvent, PassiveTriggeredEvent, result
PassiveTracker.cs         new  the tracker — Charge (PASSIVE_RULES.md §2, §4, §5)
```

| Type | What it is | Source |
| --- | --- | --- |
| `PassiveId` | The Passive's identity — the active Pet's `PetState.PassiveId`. | §1, `GAME_STATE.md` §2.3 |
| `PassiveProgress` | `Threshold` + `Current`, with `AtStart` and `IsReady`. | §1, §2, §6 item 1 |
| `PassiveResetBehavior` | `Default` / `NoReset`. **No `Partial`** — see Remaining Issues. | §4 |
| `PassiveChargedEvent` | `PassiveId`, `Progress`, `Threshold`. | `GAME_EVENTS.md` §2 |
| `PassiveTriggeredEvent` | `PassiveId`, `Progress` (pre-reset), `Threshold`. No effect summary. | `GAME_EVENTS.md` §2 |
| `PassiveTracker.Charge(...)` | Charges over N Matches, emits per Match, resets after each crossing. | §2 item 1, §2 item 3, §4, §5 |

**Modified — Domain (`src/backend/GameServer.Domain/Match3/BattleEvent.cs`)**

`BattleEventType` gains `PassiveCharged = 4` and `PassiveTriggered = 5`;
`BattleEvent` gains the two payload members, their throwing accessors (existing
convention), and internal `ForPassiveCharged` / `ForPassiveTriggered` factories.
The existing architecture is preserved: same closed-enum discipline, same
"only the members the event's own definition carries are populated" rule, same
ordering convention (`GAME_EVENTS.md` §1.1), no new infrastructure.

**Design decisions inside documented intent**

1. **One Match crosses the Threshold at most once, so the crossing is an `if`
   and not a `while`.** §2 item 1 adds exactly 1 per Match and §2 item 3
   triggers on reaching the Threshold, so a single increment cannot cross
   twice. §5's repeated trigger is explicitly "processed match-by-match", so
   extra crossings come from later Matches. A loop would fire several triggers
   for one Match, which §2 item 1 and §5 do not define. Asserted by
   `MultipleTriggers_ShouldTriggerTwiceForSevenMatchesAtThresholdThree`.

2. **The tracker takes a Match *count*, not Match values.** §2 item 1 is "+1 per
   Match" and depends on nothing about the Match — not its shape, Gem type,
   cell, size, tier, depth, or created Special Gems (`MATCH3_RULES.md` §3
   item 5). Accepting `MatchResolution` values would add an input the rule never
   consults and would overstate the dependency. The board's Match count is the
   caller's to supply (`CascadeResolver.CascadeResult.TotalMatches`).

3. **Special Gem exclusion is enforced upstream, and the tracker is structurally
   incapable of counting one.** §2 item 2 already has an owner:
   `MATCH3_RULES.md` §5.5.5 item 8 gives an activation-cleared Gem a
   `GemMatched` and no Match, and the resolver's `TotalMatches` counts Matches
   only. The tracker has no activation, cell, or cleared-cell input, so no
   filter or flag was invented. Asserted from both sides by
   `SpecialGemDetonation_ShouldNotChargeBecauseItIsNotAMatch` and
   `SpecialGemDetonation_ShouldBeExcludedByTheBoardPipelinesOwnMatchCount`.

4. **Undocumented values throw rather than resolve.** A Threshold below 1, a
   negative progress or Match count, and an undefined reset behavior are all
   rejected — a Threshold of 0 would be Ready before any Match, and no document
   defines such a value.

**Not implemented (explicitly out of scope, per the task)**

```text
BattleResolution / Application integration   — separate integration task (§17 step 10)
Pet data model, stats, identity, Element     — separate task
Boss passives                                — BOSS_RULES.md §3, different trigger semantics
Combat effect resolution (Burn/Shield/Crit)  — COMBAT_RULES.md
PassiveTriggered effect-summary population   — depends on Combat (GAME_EVENTS.md §2 item 3)
SignalR / API / client / persistence         — no wire contract defined
Partial reset                                — unimplemented; documented conflict (see below)
```

---

### Tests

**New — `tests/backend/GameServer.Domain.Tests/PassiveTrackerTests.cs` — 39 tests**

| Required case (TASK-009) | Test |
| --- | --- |
| Threshold 1 | `ThresholdOne_ShouldTriggerOnTheFirstMatch`, `..._ShouldTriggerOnEveryMatch`, `..._ShouldBeRejectedWhenNotAUsableMatchCount` |
| Threshold N charging | `Charging_ShouldEmitOneChargePerMatch_WithProgressOneTwoThree`, `..._ShouldReportTheThresholdOnEveryCharge`, `Charging_ShouldContinueFromProgressSuppliedByTheCaller`, `ZeroMatches_ShouldChargeNothingAndLeaveProgressUnchanged` |
| Exact threshold trigger | `ExactThreshold_ShouldTriggerOnceAndResetToZero`, `..._ShouldStillChargeForTheTriggeringMatch`, `..._ShouldReportPreResetProgressOnTheTrigger`, `ThresholdN_ShouldTriggerExactlyAtTheNthMatch` (7 thresholds) |
| Default reset to 0 | `DefaultReset_ShouldResetToZeroAfterTheTrigger`, `..._ShouldBeTheBehaviorWhenNoOverrideIsSupplied`, `..._ShouldCarryNoProgressIntoTheNextCharge` |
| Partial reset | `PartialReset_ShouldNotBeRepresentableBecauseTheRuleIsUnderivable`, `..._ShouldRejectTheRemovedValueRatherThanSilentlyDefaulting`, `..._ShouldBeAContradictionTheDocumentsThemselvesShow` |
| Multiple triggers in one cascade | `MultipleTriggers_ShouldTriggerTwiceForSevenMatchesAtThresholdThree` (§5's own example), `..._ShouldProcessThemInMatchOrder`, `..._ShouldTriggerManyTimesOverALongCascade`, `..._ShouldSettleOnTheRemainderOfTheRun`, `..._ShouldChargeOnceForEveryMatchOfTheRun` |
| Progress/order of PassiveCharged | `PassiveCharged_ShouldBeEmittedInMatchDetectionOrder`, `..._ProgressShouldIncreaseByExactlyOneBetweenResets`, `..._ShouldNeverExceedTheThresholdUnderTheDefaultReset`, `..._ShouldReportThePassiveIdentityOnEveryEvent` |
| Special Gem not a Match | `SpecialGemDetonation_ShouldNotChargeBecauseItIsNotAMatch`, `..._ShouldBeExcludedByTheBoardPipelinesOwnMatchCount` |
| Documented edge cases | `NoReset_ShouldNotReduceProgress`, `..._ShouldNotReTriggerOnLaterMatchesWithoutAnotherCrossing`, `NoReset_ShouldRejectAnUndefinedResetBehavior`, `Charging_ShouldNotTriggerAtOneBelowTheThreshold`, `Charging_ShouldRejectANegativeMatchCountOrNegativeProgress` |
| PassiveTriggered payload | `PassiveTriggered_ShouldCarryThePassiveIdentityProgressAndThreshold`, `..._ShouldCarryNoEffectSummaryMember`, `..._ShouldNotBeEmittedWhenTheThresholdIsNotReached` |
| Purity / determinism | `Tracker_ShouldBeDeterministicAndFreeOfSharedState`, `Tracker_ShouldNotMutateTheProgressItWasGiven`, `Tracker_ShouldReachTheSameSettledProgressOnEitherResetPath` |

**Modified — `BattleEventEmissionTests.cs`** (TASK-006 contract tests that
assert the closed event set and the `BattleEvent` member shape; updated to admit
the two newly documented names):
`EventTypes_ShouldBeExactlyTheDocumentedOnes`,
`EventValue_ShouldCarryNoTimeGuidOrGeneratedIdentifier`.

**Results**

```text
GameServer.Domain.Tests          618 passed, 0 failed  (39 new)
GameServer.Application.Tests      41 passed, 0 failed
GameServer.Infrastructure.Tests    1 passed, 0 failed
GameServer.Api.Tests              46 passed, 0 failed
                                 ─────────────────────
                                 706 passed, 0 failed
```

---

### Documentation Consulted

```text
docs/00-overview/MVP_SCOPE.md            §1 (Pets IN), §2 (OUT), §4
docs/01-game-design/GAME_RULES.md        §3, §9, §10, §16, §17 (step 10), §18
docs/01-game-design/PASSIVE_RULES.md     §1–§8 (full)
docs/01-game-design/PET_RULES.md         §1, §2, §8
docs/02-technical/ARCHITECTURE.md        §1, §2.1, §3 (PassiveTracker), §4.1
docs/02-technical/GAME_STATE.md          §2, §2.2, §2.3, §5.1
docs/02-technical/GAME_EVENTS.md         §1, §1.1, §2, §3
docs/02-technical/SIGNALR_PROTOCOL.md    §4.2 (checked — unchanged by this task)
AGENTS.md                                §2, §4, §6, §7, §9, §11, §12, §14, §15,
                                          §16, §17, §20, §21, §22
.ai/workflow/development/feature.md      full
.ai/workflow/core/context-discovery.md   §1, §3
.ai/workflow/core/planning.md, implementation.md, validation.md   full
.ai/workflow/quality/review.md           §1
.ai/skills/gameplay/gameplay-behavior-derivation.md   full
tasks/TASK_LIFECYCLE.md                  §1–§4
```

---

### Documentation Changed

**`docs/02-technical/GAME_EVENTS.md`** — version 1.4 → 1.5 (header note
updated). §2 `PassiveCharged` / `PassiveTriggered`:

- Payload changed from `PassiveCharged: new progress value, threshold` /
  `PassiveTriggered: effect summary` to include `PassiveId` on both.
- Added item 1 defining `PassiveId` as the identity `GAME_STATE.md` §2.3 names,
  reported and never re-derived, and matching the sibling trigger events'
  identity members.
- Added item 2 defining the progress/threshold members, including that a
  `PassiveTriggered`'s progress is **pre-reset**.
- Added item 3 recording `effect summary` as **deferred** to the Combat stage,
  and stating that its absence means "not yet reported", not "no effect".

**`docs/02-technical/GAME_STATE.md`** — version 1.5 → 1.6 (header note updated).
§2.3 `PetState`:

- Added `PassiveId` to the field list.
- Added four numbered notes defining it as the Pet's one Passive's identity,
  present from battle creation, set once and never changed, an identity and not
  a definition, introducing no gameplay behavior.

**`docs/01-game-design/PASSIVE_RULES.md` — NOT changed.** Its §7 already
described both events correctly, and the partial-reset conflict is reported for
a human decision rather than silently edited (see Remaining Issues).

**No ADR was created.** `ARCHITECTURE.md` §3 already assigns `PassiveTracker` to
the Domain layer, no architecture, storage, realtime, or module boundary
changed, and the `PassiveId` addition is a state-field detail owned by
`GAME_STATE.md` — not an architectural decision (`AGENTS.md` §18).

---

### Validation

Depth per `core/validation.md` §2 for Risk: **LOW** — build/compile validation
+ focused unit tests + review.

```text
[✓] Build/compile validation
    dotnet build src/backend/GameServer.sln        → succeeded, 0 warnings, 0 errors
[✓] Focused unit tests
    dotnet test --filter PassiveTracker            → 39 passed, 0 failed
[✓] Full Domain suite (regression)
    GameServer.Domain.Tests                        → 618 passed, 0 failed
[✓] Cross-project regression
    Application 41 + Infrastructure 1 + Api 46     → 88 passed, 0 failed
[✓] Security        No auth, credential, or data-exposure surface touched.
[✓] Performance     Pure in-memory value work; no I/O, no query, no
                    allocation of note on the resolution path. O(1) per Match.
[✓] Maintainability No new interface, factory, event bus, DI registration, or
                    module infrastructure (AGENTS.md §9). Four small types.
[✓] Determinism     Server-authoritative; no RNG, clock, GUID, or unordered
                    collection participates (AGENTS.md §11). No client value
                    is authored — the tracker is Domain-only and the client
                    never calls it (AGENTS.md §10).
```

**`quality/review.md` §1**

```text
Correctness      Pass — every behavior cites PASSIVE_RULES.md / GAME_EVENTS.md;
                 the two non-derivable points are reported, not guessed.
Architecture     Pass — Domain layer only; no framework/Infra/Api reference;
                 passive charge/trigger/reset stays in its own module
                 (AGENTS.md §12).
Scope            Pass — no Application/Combat/Pet/Boss/Relic/SignalR/persistence
                 code, no unrelated refactor. The two files touched outside the
                 new module are the event contract and its contract tests.
Tests            Pass — all required cases covered at LOW depth, including the
                 §5 multiple-trigger example and the §2 item 2 exclusion.
Documentation    Pass — two owning docs amended first, then implemented (§17).
Security         Pass — no concern raised.
Performance      Pass — no regression; no new hot-path query.
Maintainability  Pass — no unnecessary abstraction.
```

---

### Risks

1. **`PassiveId` is a new field in the state contract.** `GAME_STATE.md` §2.3
   now declares it. Its type on the wire is the serializer's concern
   (`GAME_EVENTS.md` §3 item 1, `SIGNALR_PROTOCOL.md` §8 item 1); this task
   defines it as a Domain value only and introduces no wire shape. Impact is
   low while `PetState` itself is not yet implemented or delivered.
2. **The two `BattleEventEmissionTests` assertions were relaxed** from an exact
   4-name enum to an exact 6-name enum. This is intentional and still an exact,
   closed-set assertion — it was not loosened to a subset check — so an
   undocumented seventh name would still fail.
3. **Partial reset is absent from `PassiveResetBehavior`.** Any future caller
   passing the removed value now gets an `ArgumentOutOfRangeException` rather
   than a silent wrong behavior. This is deliberate, but it means a Pet
   designed with partial reset cannot be implemented until the documentation
   conflict is resolved.
4. **The tracker trusts its caller's Match count.** If a caller supplies a
   cleared-cell or activation count, progress over-charges. This is the
   documented division of responsibility (§2 item 2 is enforced where Matches
   are counted) and is asserted from both sides, but it is a real integration
   risk for the separate BattleResolution task.

---

### Remaining Issues

1. **BLOCKING (documentation) — `PASSIVE_RULES.md` partial reset is
   underivable, and TASK-009's partial-reset acceptance criterion is
   unsatisfiable as written.**

   ```text
   Problem:
   Partial reset cannot be implemented, because the documents describe it in
   terms that §2 makes unreachable.

   Sources:
   - docs/01-game-design/PASSIVE_RULES.md §2 item 1
       "Every Match increases the active Pet's Passive progress by 1"
   - docs/01-game-design/PASSIVE_RULES.md §2 item 3
       "When progress reaches the Passive's Threshold, the Passive becomes
        Ready and triggers immediately"
   - docs/01-game-design/PASSIVE_RULES.md §4 item 2
       "Partial reset (e.g. progress reduces by Threshold rather than to 0,
        allowing 'overflow' matches from a single big Cascade to carry into
        the next charge)"
   - docs/01-game-design/PASSIVE_RULES.md §5
       "...processed match-by-match in the order matches were detected"
   - tasks/backlog/TASK-009-implement-passive-tracker.md, Acceptance Criteria
       "Threshold=5, reset by Threshold … triggers at progress 7 → resets to 2"

   Conflict:
   §4 item 2 and the task's criterion both presume progress can sit ABOVE the
   Threshold at the moment of a trigger (7 with Threshold 5). §2 makes that
   impossible: progress rises by exactly 1 per Match, and it is compared
   against the Threshold after every Match (§5), so it is reset the instant it
   equals the Threshold and never exceeds it. "Reduce by Threshold" is
   therefore always Threshold − Threshold = 0, which makes partial reset
   behaviorally identical to the default full reset — the "overflow" §4 item 2
   exists to preserve can never exist.

   Impact:
   One of TASK-009's acceptance criteria is unreachable. All five MVP Pet
   Passives use the default reset (PASSIVE_RULES.md §8), so no MVP content is
   affected — but a future Pet designed with partial reset cannot be built.

   Suggested resolution (smallest possible, human decision required):
   State explicitly in PASSIVE_RULES.md §2 whether progress may overshoot the
   Threshold, and if so, when the trigger fires and what a trigger consumes.
   Two candidates:
     (a) Progress may overshoot only if a single Match can add more than 1
         (e.g. a future Relic/modifier), and a trigger consumes exactly
         Threshold — then §2 item 1's "+1 per Match" needs a stated exception.
     (b) Partial reset is removed from §4 item 2 as unreachable under the
         current charging rule, and TASK-009's criterion is corrected.

   Per AGENTS.md §4/§20 and this task's Stop Conditions, the tracker
   implements Default and NoReset and does NOT implement Partial; the behavior
   was left unguessed pending this decision.
   ```

2. **`PassiveTriggered`'s `effect summary` remains unpopulated** — by design.
   `GAME_EVENTS.md` §2 item 3 now records this as deferred to the Combat stage,
   which is the same follow-up TASK-009 anticipated.

3. **BattleResolution integration is not done** — out of scope by the task
   ("The integration task that calls the tracker from BattleResolution is
   separate"). `PassiveBattleEventBuilder`-style assembly of the two events into
   one ordered stream at `GAME_EVENTS.md` §1.1's position (step 10), the
   `PetState.PassiveProgress` write-back, and the `PlayerState`/`BattleState`
   plumbing are all still to be written. Suggested follow-up task: TASK-010 —
   integrate the Passive Tracker into BattleResolution step 10.

4. **No `GAME_STATE.md` §2.3 implementation exists yet.** `PetState` is still
   unimplemented, so `PassiveId` / `PassiveProgress` are contract fields only;
   the tracker takes them as parameters rather than reading state.

5. **`threshold` is an unused parameter in `PassiveTracker.Reset`.** It is
   retained as the documented amount a Partial reset would remove, so the
   unimplemented behavior's shape stays visible. It becomes used if the
   conflict above is resolved toward partial reset.

---

### Agent

gameplay

### Workflow Used

`development/feature.md` — MVP scope check → `core/task-intake.md` (LOW) →
`core/context-discovery.md` (two stop conditions fired and were resolved by
requester decision) → `core/planning.md` → `core/implementation.md` →
`quality/testing.md` → `quality/review.md` → `documentation/documentation-
change.md` (applied **before** implementation, per `AGENTS.md` §17) →
`core/completion.md`.

### Skills Used

`gameplay/gameplay-behavior-derivation.md` (expected-behavior record for
Passive charge/trigger/reset, and the source of the two reported gaps);
`discovery/documentation-discovery.md` and `discovery/impact-analysis.md`
(document routing and the affected-area trace).

### Status

DONE (with one reported documentation conflict — see Remaining Issues 1; the
conflicting behavior is deliberately not implemented rather than guessed).

---

## Handoff

No mid-execution handoff. Two decisions were escalated to the requester during
discovery rather than guessed:

1. `PassiveTriggered` payload / Passive identity — requester chose to amend
   `GAME_EVENTS.md` §2 + `GAME_STATE.md` §2.3 first, then implement.
2. Partial reset — requester chose to leave it unimplemented pending a
   documentation fix.

Follow-up work is listed under Remaining Issues. The next task in the
Pet → Passive → Combat chain is the BattleResolution step-10 integration
(suggested TASK-010), which is where Matches will actually be fed to
`PassiveTracker.Charge` and where `PetState.PassiveProgress` gets written back.
