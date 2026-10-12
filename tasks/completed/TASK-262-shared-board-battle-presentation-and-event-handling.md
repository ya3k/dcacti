# TASK-262 — Shared-Board Battle Presentation and Event Handling

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
-->

---

## Metadata

```text
Task ID:           TASK-262
Type:              FEATURE
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     client
Supporting Agents: testing, review
Workflow:          development/feature.md
Skills:            client/phaser-battle-presentation, client/client-event-projection,
                   client/client-state-authority, testing/test-scenario-generation,
                   quality/implementation-review, quality/scope-validation
Dependencies:      TASK-258 (DONE), TASK-259 (DONE), TASK-260 (DONE), TASK-261 (DONE)
Declared Files:    src/frontend/client/src/game/scenes/BattleScene.ts,
                   src/frontend/client/tests/SceneLifecycle.test.ts
```

---

## Objective

Verify and complete the frontend presentation/event handling for the shared-board
battle lifecycle introduced by TASK-258…TASK-261 (ADR-026): the authoritative backend
now resolves a complete round — Player action and its match/cascade effects, the Boss
response on the **same** board, the step 19a status-effect lifecycle, the terminal
check — and commits it as **one** state transition with a single `Turn`/`Sequence`
increment. The frontend must present that round in the delivered order on one board,
without independently reimplementing any authoritative combat logic and without
replaying a resolution the server has already moved past.

Presentation only: no gameplay rule, no server calculation, no wire member, no
client-authoritative Match-3 computation (`AGENTS.md` §10; ADR-001).

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md` §2 (atomic round, `Turn`/`Sequence` once), §3 (ATK gating), §4 (actor-relative cell effects), §7 (CAS persistence vs. non-transactional SignalR delivery), §8 (Boss board action; deterministic AI)
- `docs/01-game-design/GAME_RULES.md` §17 — the canonical resolution order and its step 18 expansion (18a Boss board action → 18b Passive → 18c Skill → 18d ATK-gated attack), step 19a; §18 server authority
- `docs/01-game-design/MATCH3_RULES.md` §8.1–§8.2 — one committed round advances `Turn`/`Sequence` once
- `docs/02-technical/GAME_EVENTS.md` §1 — the authoritative ordering of one resolution, including the Player phase's damage reports followed by the **Boss Response Phase** (its own swap/board cycle, then boss passive/skill/attack) and the terminal outcome last; §1.1 the board-cycle ordering contract; §1.2 a rejected action emits nothing; §2 each event's payload; §3 item 5–7 (every §2 event is delivered; events are not state)
- `docs/02-technical/GAME_STATE.md` §5, §5.1 — one post-resolution write-back; nothing is written mid-resolution; §2.2 `Combo`/`MatchCount` are the Player action's counters
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3 — `ReceiveEvents(battleId, serverSequence, events[])`; §3.1 items 1–2 (write-back before the batch; **no per-step delivery**, no intermediate board), item 4 (a rejected action delivers nothing), item 6 (CAS atomicity ≠ exactly-once delivery); §3.2.2 the **closed** `type` discriminator set and its `SwapStarted` protocol *proposal*; §3.2.7 `cascadeDepth` is per-event; §3.2.14/§3.2.15 damage parties; §3.2.16 passive `source`; §3.2.24 item 3 `source` is **stage ownership, not a side**; §4 the one state push; §4 item 6 / §7 snapshot recovery (ADR-008)
- `docs/03-decisions/ADR/ADR-008-battle-recovery.md` — recovery is a snapshot resynchronization, never an event replay
- `docs/02-technical/ARCHITECTURE.md` §2.2, §2.2.1 (the runtime never reorders, filters or interprets a batch), §5 (anti-overengineering)
- `tasks/completed/TASK-245-…md` (PD-1…PD-8), `TASK-246-…md` (§4/§5, C-1), `TASK-252-…md` (the one actor/action adapter; D-2 timeline-owned readouts), `TASK-253-…md` (the Move Log's `serverSequence` bookkeeping), `TASK-254-…md` (one lifecycle; the single supported board transition) — **all DONE and immutable**
- `tasks/completed/TASK-261-atomic-round-orchestration-and-redis-state-persistence.md` — the committed backend round (Phase A `7cd0782`) and its Remaining Issues §11 (the Boss phase's `ComboChanged` is the Boss resolution's own value, while `BattleState.Combo`/`MatchCount` stay the Player's)
- `tasks/TASK_LIFECYCLE.md` §6 — the two-stage commit protocol

---

## Preflight Findings

Verified by direct inspection at HEAD `705fc31` (working tree: the `.ai/` documentation
sync and `scripts/collect-project-state.sh` are modified/untracked and are **not** this
task's; they are disclosed as unowned/pre-existing).

1. **Task ID and roadmap reconciliation.** The highest completed task is `TASK-261`;
   `TASK-262`…`TASK-265` exist nowhere in `tasks/`. TASK-258 §9's roadmap proposed
   `TASK-262 = Atomic Round Orchestration` (executed as **TASK-261**), `TASK-263 =
   Realtime `SwapStarted`` and `TASK-264 = Phaser Boss-swap presentation`. This task's
   brief consolidates the presentation/event-handling work those two slots described
   into `TASK-262`; the stale roadmap allocation is recorded here, and no
   `TASK-263`/`TASK-264` manifest is created (explicitly out of scope).
2. **What the backend actually delivers for one committed round** (`BattleStateService.ResolveSwapAsync`,
   `7cd0782`), in one `ReceiveEvents` batch under a single `serverSequence`:
   `[Player board cycle: MatchCreated/GemMatched/ComboChanged/CascadeCreated…]` →
   `PassiveCharged/Triggered(source="pet")` → `RelicTriggered` / `PowerChanged(source="relic")` →
   `PowerChanged(source="match")` → `DamageCalculated/Dealt/Taken(source="player",target="boss")`
   *(only iff the Player cleared an ATK gem — ADR-026 D1)* → `BattleWon` *(terminal)* →
   `[Boss board cycle: MatchCreated/GemMatched/ComboChanged/CascadeCreated…]` →
   `PassiveCharged/Triggered(source="boss")` → `BossSkillCast` (+`PowerChanged(source="boss")` for Drain Power)
   → `DamageCalculated/Dealt/Taken(source="boss",target="player")` *(only iff the Boss cleared an ATK gem)* →
   `BattleLost` *(terminal)*. Events are the executor's own list, concatenated in that order.
3. **`BattleEventType` has exactly the sixteen wire discriminators** (`BattleEvent.cs`).
   `TurnStarted`, `TurnEnded`, `SwapStarted`, `SwapResolved` and `MatchResolved` are
   **not** members of the Domain event model and are **not** emitted — confirmed by
   `BattleStateServiceTests.ExecuteSwap_ShouldNotWriteAnyBattleEventOrAddAMessage` and
   `BossResponseWireTests.ReceiveEvents_ShouldNotCarryAForbiddenBossEventName`.
   `SIGNALR_PROTOCOL.md` §3.2.2 carries the `SwapStarted` admission only as a
   **Protocol Proposal** ("its schema admission will be formalized upon downstream
   implementation"); the discriminator set is closed.
4. **No delivered member names the actor of a board-cycle event.** `MatchCreated`,
   `GemMatched`, `ComboChanged` and `CascadeCreated` carry no party/stage member
   (`SIGNALR_PROTOCOL.md` §3.2.6–§3.2.12), and the Boss's board cycle uses the same
   builder the Player's does (`BattleEventBuilder.Build(bossResolution, bossResolution.TotalMatches)`).
   The delivered members that *do* name an actor are: damage `source`/`target`,
   `PassiveCharged`/`PassiveTriggered.source`, `BossSkillCast`, `CardCast`/`PetSkillCast`,
   `RelicTriggered`, `PowerChanged.source` (a **stage**, never a side).
5. **The frontend consumer is TASK-252/253/254's model.** `BattleEventPresenter.adaptPresentedEvent`
   is the single event→(actor, action, kind) adapter, and `BattleScene.buildPresentationPhases`
   groups its output into one ordered timeline per batch. Board-cycle events adapt to
   `actor: 'swap'` — "the committed Swap — the resolution's board cycle" — which names
   **no side**; the adapter's own doc block states that no party is inferred from
   ordering, timing or a client-held turn. `SceneLifecycle.test.ts:6855–6874` enforces
   "no per-actor branch" and `:6820–6831` enforces "every phase's actor/action comes
   from the same single adapter call".
6. **Two concrete gaps against this task's scope, both in `BattleScene.ts`:**
   - **The single supported board transition is spliced at the wrong point of a two-cycle round.**
     `buildPresentationPhases` splices `settle` at the first `damage`/`retaliation`
     phase. TASK-254 wrote that for a one-board-cycle resolution; under ADR-026 §2 the
     round's board cycle spans the Player's Swap **and** the Boss's step 18a board
     action, whose events are delivered **after** the Player's damage reports. The
     committed board is therefore applied while the round's board work is still being
     presented, and the Boss's own board cycle plays over a board the resolution has
     already left behind.
   - **An already-accepted resolution is presented a second time.** `playEventBatch`
     compares `serverSequence` against `presentationSequence` *only while a timeline is
     playing*, and `releaseTimeline` drops that value. A re-delivered or older batch
     that arrives after its timeline completed is therefore rebuilt and replayed
     (swap, matches, settle, combat), even though a newer resolution has already been
     accepted — the presentation counterpart of the Move Log's own `beginSequence`
     bookkeeping (`BattleScene.ts:5235–5252`), which does not have this hole.
7. **Verified non-defects (do not "fix" these).** Input is locked continuously from
   submission through presentation, and the terminal handoff is the timeline's last
   phase and is also performed by completion/supersession/abort; `Combo`/`MatchCount`
   readouts are rendered from the authoritative `playerState` and never folded from the
   delivered `ComboChanged` values (`renderPlayerHud`); supersession by a newer
   `serverSequence`/state and ADR-008 snapshot recovery both converge the board and
   release the guard; and `Turn`/`Sequence` are never written by the client at all.

---

## Scope

### In Scope

- **Board-update ordering on the shared board.** The one supported board transition
  (TASK-254's `settle`) is applied where **the round's board cycle actually ends** —
  after the last delivered board-cycle phase — while TASK-254's placement (the first
  combat stage, or the resolution's end when it delivers none) is preserved whenever the
  round's board cycle does not continue past a combat stage. Exactly one board
  transition, exactly one committed board, nothing derived.
- **No replay of a resolution the client has already accepted.** The presentation
  remembers the newest `serverSequence` it has accepted, per battle (the Move Log's own
  convention), so a duplicate or older batch is recorded and presented no second time
  even after its timeline has ended and even if a newer resolution has since been
  accepted. Reset per battle, per run.
- **Verification of the delivered contract and the presentation it drives**, with
  focused tests for: the Player phase followed by the Boss phase on one board; the
  actor attribution the delivered members support (and the absence of any it does not);
  the ordering of board updates, combat effects and terminal presentation; duplicate and
  stale batches; snapshot recovery during and after an interrupted presentation; the
  input lock/unlock behavior; terminal rounds; and the existing standalone flow.
- **Reporting the residual contract gap** (below) rather than inventing a protocol.

### Out of Scope

- **Any wire/protocol change**: no new or renamed hub method, event type, state-push
  member, subscription or `type` discriminator — in particular the `SwapStarted`
  admission, an `actor` member on the board-cycle events, and any per-pass board
  (`SIGNALR_PROTOCOL.md` §3.1 item 2, §3.2.2; ADR-026's protocol extension is its own
  decision, reported as G-1).
- Any backend, Domain, Application, Infrastructure or Api change; any combat rule,
  damage calculation, Boss AI, skill magnitude or Redis persistence change.
- Re-baselining TASK-194/TASK-195/TASK-197 balance evidence; touching
  `tasks/artifacts/*`.
- Any client-side simulation of authoritative Match-3 or combat rules: no match
  detection, clearing, gravity/spawn derivation, refill identity, RNG draw, damage,
  HP, Power, Combo, `MatchCount`, Passive progression, or reverse-engineering of the
  Boss's swap from a board diff (`AGENTS.md` §10; ADR-001).
- Any fabricated presentation: no invented Boss-phase banner, phase label, turn
  indicator or animation for an event the server did not deliver — the wire carries no
  turn/phase signal (`GAME_STATE.md` §2.0.3; TASK-246 §4 F-3/C-1).
- TASK-252/253/254's adapter vocabulary, orchestration machine, legal-transition table,
  D-2 timeline-owned readouts, PD-1…PD-8 boundaries, supersession keying and outcome
  handoff semantics; the TASK-209 §5 / TASK-210 §9 diagnostic boundary; the DEV-only
  Move Log.
- Creating `TASK-263` or any later manifest; editing `tasks/completed/*` or
  `tasks/blocked/*` (`AGENTS.md` §16–§18).
- Broad UI redesign or unrelated Phaser refactoring.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-262:

```text
src/frontend/client/src/game/scenes/BattleScene.ts
src/frontend/client/tests/SceneLifecycle.test.ts
```

*(The task manifest itself is excluded from the implementation declared file set, and
the list strictly matches the `Declared Files:` field in `## Metadata`.)*

**Shared-file ownership.** Both files were last owned by TASK-254 (`DONE`, immutable,
filed in `tasks/completed/`, which records its own Phase A SHA). `tasks/active/` contains
this manifest only, and `tasks/backlog/` and `tasks/blocked/` contain no other task, so
no concurrent shared-file owner exists and no joint group commit is required
(`TASK_LIFECYCLE.md` §6.1 item 5).

**Unowned / pre-existing disclosure (P-3).** `git status --porcelain` at pickup reports
**modified**: `.ai/HARNESS_COMPATIBILITY.md`, `.ai/HARNESS_MAPPING.md`,
`.ai/agents/AGENT_SELECTION.md`, `.ai/agents/orchestrator.md`, `.ai/skills/README.md`,
`.ai/skills/SKILL_REGISTRY.md`, `.ai/workflow/README.md`; **untracked**: `.agents/`,
`.ai/PROJECT_STATE.md`, `.ai/skills/discovery/task-readiness-assessment.md`,
`.ai/workflow/core/task-selection.md`, `.claude/`,
`scripts/collect-project-state.sh`. None is this task's; none is staged, and all are
disclosed in the Phase A commit body.

---

## Acceptance Criteria

- [x] The round's Player phase and Boss phase are presented in the delivered order on
      one board, in one timeline, with no re-sorting, filtering or duplication
      (`ARCHITECTURE.md` §2.2.1 rule 4; `SIGNALR_PROTOCOL.md` §3.1 item 5).
- [x] The single supported board transition applies the round's committed board exactly
      once and at the end of the round's board cycle: after the last delivered
      board-cycle phase, and never at a point that leaves a delivered board cycle
      un-presented. TASK-254's placement is preserved for every batch whose board cycle
      does not continue past a combat stage.
- [x] Actor attribution remains exactly what the delivered members support and no
      more: the board cycle's own events keep the adapter's side-free `swap` actor, a
      delivered `source="boss"` damage instance, Boss Passive and `BossSkillCast` are
      attributed to the Boss, and `PowerChanged`'s `source` stays the mutation **stage**
      (`SIGNALR_PROTOCOL.md` §3.2.24 item 3). No phase names a party the delivered
      members did not name; the single-adapter invariant of TASK-252 holds.
- [x] The Boss phase's own `ComboChanged` values never corrupt the authoritative
      `Combo`/`MatchCount` readouts: the displayed `COMBO ×N` / `MATCHES N` remain
      `BattleState.playerState`'s values as delivered for the whole round
      (`GAME_STATE.md` §2.2; TASK-261 Remaining Issue §11).
- [x] `Turn` and `Sequence` are never written, folded or incremented client-side; an
      older or duplicate batch is recorded and presented no second time, before or
      after its timeline ends, including after a newer resolution has been accepted.
- [x] Existing snapshot recovery and sequence protections remain intact: an aborted
      timeline converges the board and the readouts on the delivered state, releases the
      guard, and never replays the abandoned or superseded round; recovery ingests the
      snapshot through the same state path (`ADR-008`).
- [x] Input is locked for the whole committed round (both phases) and released by
      completion, abort, supersession or a rejected action; a terminal round presents
      nothing further, accepts no further action request, and reaches `ResultScene` with
      `VICTORY`/`DEFEAT` stated.
- [x] Animation timing determines no gameplay value or outcome.
- [x] The SignalR wire contract is unchanged: the three-member `ReceiveEvents`
      envelope, the nine-member state push, and the closed sixteen-name discriminator
      set; no new hub method, event, subscription or member.
- [x] Focused tests cover the eight required areas (§3.D); all relevant frontend tests
      and the frontend build pass at MEDIUM validation depth.
- [x] The browser E2E suite is reported accurately — not run, because the documented
      local stack cannot be started in this environment (no Docker, no PostgreSQL).
- [x] Zero backend, documentation, generated-artifact or unrelated-file changes.
- [x] Residual contract gaps are reported, not silently designed around.

---

## Current State

`BattleScene.ts` already plays one ordered timeline per delivered `ReceiveEvents` batch
on TASK-252's actor/action model, holds the round's board and readouts until the
timeline presents them (TASK-254), locks input from submission through convergence, and
supersedes playback on a newer `serverSequence`. `BattleEventPresenter.ts` parses all
sixteen discriminators and owns the one adapter; nothing in the frontend models the
shared board's second board cycle.

What this task changes is exactly preflight finding 6: the splice point of the single
supported board transition, and the absence of an accepted-resolution memory behind the
duplicate/older-batch guard.

---

## Implementation Notes

- `buildPresentationPhases` — the `settle` splice keeps TASK-254's rule (the first
  `damage`/`retaliation` phase, or the end when the resolution delivers none) and moves
  later only when the round's board cycle continues past that point, i.e. when a
  delivered `MatchCreated`/`GemMatched`/`CascadeCreated`/`ComboChanged` phase follows the
  first combat stage — the Boss's step 18a board action. One helper, no new abstraction.
- `playEventBatch` — the accepted-resolution memory is the Move Log's own convention
  applied to the presentation (`serverSequence` strictly increasing,
  `SIGNALR_PROTOCOL.md` §3 item 2), keyed by `battleId` so a new battle always starts at
  its own first sequence. It is reset by `create()` and by teardown, like every other
  per-battle guard.
- No new field is added to `RuntimeBattleState` (`GAME_STATE.md` §2.0.3 forbids a
  lifecycle/phase/status member), no event type is added or accepted, and no delivered
  member is renamed or reinterpreted.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — the board-transition splice point for a two-cycle round and for
                         the preserved one-cycle/no-combat cases; the accepted-resolution
                         memory (duplicate, older, new battle, after completion); the
                         terminal and input-lock predicates — SceneLifecycle.test.ts,
                         the TASK-262 describe (11 tests)
[x] Integration tests  — a delivered shared-board round batch presented in one timeline
                         in the delivered order; the actor attribution of every phase; the
                         authoritative Combo/MatchCount readouts across the Boss board
                         cycle; the wire-contract regression (the closed discriminator
                         set, the three-member envelope, no new state member) is covered
                         by the existing BattleEventPresentation.test.ts / RuntimeBoundaries
                         .test.ts assertions, run in this task's validation
[x] Gameplay scenarios — Given a committed shared-board round whose Player phase dealt
                         damage and whose Boss phase made its own swap, match and cascade,
                         When the server delivers the state push and one batch,
                         Then the Player's board cycle presents on the Player's board,
                         And the Boss's board cycle presents after the Player's damage,
                         And the committed board is applied once at the end of the round's
                             board cycle,
                         And the Boss's Passive/Skill/retaliation present in delivered
                             order,
                         And the Combo/MatchCount readouts show the Player's authoritative
                             values throughout,
                         And input stays locked until the timeline ends
```

### Key Edge Cases

- A round whose Player attack is gated off (ADR-026 D1): no player damage reports are
  delivered, the Boss phase's board cycle is present, and no party is claimed for it.
- A terminal round in either direction: `BattleWon` last in the Player phase's batch,
  `BattleLost` last in the Boss phase's batch; nothing presents after the handoff.
- A batch whose board cycle ends before its first combat stage (TASK-245/254's fixtures):
  the placement must not move.
- A Power/Passive-only tail after the round's board cycle (`SceneLifecycle.test.ts`
  `:4125–4146`): the placement must not move.
- Snapshot recovery mid-presentation: a jumped `sequence` aborts the timeline, converges
  the board/readouts and does not replay the abandoned round.
- A duplicate batch delivered after its timeline completed, and one delivered after a
  newer resolution was accepted.

---

## Stop Conditions

- If the required presentation needs a wire member or discriminator that
  `SIGNALR_PROTOCOL.md` §3.2.2 does not admit: **STOP** and report the smallest concrete
  contract decision needed (`AGENTS.md` §20; this task's §6). It does not authorize
  adding one.
- If the delivered contract and `GAME_EVENTS.md` §1 disagree about the round's ordering:
  report per `AGENTS.md` §4 rather than picking one.
- If a required fix would change a persisted contract, a gameplay rule, or the Boss AI:
  STOP and escalate.

---

## Reported Contract Gaps (not resolved here)

```text
G-1  No delivered member identifies the actor of a board-cycle event, and the Boss's
     swap coordinates are not delivered at all. `SIGNALR_PROTOCOL.md` §3.2.2 admits no
     `SwapStarted`/`SwapResolved`; `GAME_EVENTS.md` §1 places a Boss `SwapStarted`
     (actor="boss") in the Boss Response Phase and ADR-026's Consequences name the
     protocol extension as required. Consequence: the frontend can present the round's
     two board cycles in the delivered order on one board, but can neither attribute a
     given Match/Cascade/Combo to Player or Boss nor animate the Boss's swap as a swap.
     The smallest concrete decision: formalize the §3.2.2 proposal — admit
     `SwapStarted` ({ actor: "player" | "boss", fromCell, toCell }) and its `SwapResolved`
     counterpart as discriminators with their own §3.2.x subsections and wire projection,
     emitted by the Player's SwapExecutor and by the Boss's step 18a. Until then, adding
     the attribution client-side would claim a party the delivered members do not name
     (`AGENTS.md` §10; TASK-252's single-adapter invariant).
G-2  `BattleState.Combo`/`MatchCount` remain the Player's counters while the Boss
     phase's `ComboChanged` reports the Boss resolution's own run (TASK-261 Remaining
     Issue §11). The frontend is correct as implemented (readouts are the authoritative
     Player values and are never folded from delivered combos); whether the wire should
     distinguish the two runs is a separate contract decision, reported, not assumed.
G-3  ADR-026 §8 promises a `BoardReshuffled` event on the Boss AI's deadlock reshuffle;
     no such discriminator exists (§3.2.2 is closed) and none is emitted (TASK-261
     Remaining Issue §4). The client therefore presents the reshuffle only as the
     committed board. Reported, not resolved.
```

---

## Completion Evidence

### Commit

- Implementation slice (Phase A): `ab977554854d667a7b502735dc4f37b030550f92`
- Subject: `feat(client): present the shared-board round on one board and stop replaying accepted resolutions`
- Owns: `TASK-262`
- Files: `src/frontend/client/src/game/scenes/BattleScene.ts`,
  `src/frontend/client/tests/SceneLifecycle.test.ts` (the declared file set, exactly)
- Shared with: `none`
- Unowned / pre-existing (left unstaged, disclosed in the commit body):
  `.ai/HARNESS_COMPATIBILITY.md`, `.ai/HARNESS_MAPPING.md`,
  `.ai/agents/AGENT_SELECTION.md`, `.ai/agents/orchestrator.md`,
  `.ai/skills/README.md`, `.ai/skills/SKILL_REGISTRY.md`,
  `.ai/workflow/README.md`, `.gitignore`, `.agents/`, `.ai/PROJECT_STATE.md`,
  `.ai/skills/discovery/task-readiness-assessment.md`,
  `.ai/workflow/core/task-selection.md`, `.claude/`
- Completion record filing (Phase B): this completion record alone, filed under its own
  bookkeeping commit (`tasks/completed/TASK-262-shared-board-battle-presentation-and-event-handling.md`).

### Changed Files

- `src/frontend/client/src/game/scenes/BattleScene.ts`
  - `buildPresentationPhases` + `lastBoardCyclePhaseIndex` — the single supported board
    transition is spliced where the round's board cycle ends (after the last
    `swap`/`match`/`cascade` phase) and never earlier than TASK-254's placement, which
    still governs every batch whose board cycle ends at or before a combat stage and
    every batch that delivers no combat stage at all.
  - `acceptedResolutionSequence` / `acceptedResolutionBattleId` + the guard in
    `playEventBatch` — a batch at or below the newest accepted `serverSequence` for the
    same battle is recorded and presented no second time; the in-flight supersession
    branch now only ever runs for a strictly newer batch. Reset by `create()` and
    `shutdown()`, and re-keyed when the delivered battle changes.
  - `acceptsPlayerAction` — the three action-request paths (`submitSwap`,
    `submitCardCast`, `submitPetSkillCast`) refuse a request once the battle's outcome
    has been handed off, without changing the documented meaning of `isInputLocked()`.
  - Doc blocks for `playEventBatch`, the `settle` splice and the new members.
- `src/frontend/client/tests/SceneLifecycle.test.ts` — the new `TASK-262 — shared-board
  round presentation and event handling` describe (11 tests) covering the eight required
  areas: the delivered two-phase order and the board-transition splice point; the board
  before and after it; the actor attribution the delivered members support; the
  authoritative Combo/MatchCount readouts across the Boss board cycle; duplicate and
  older batches before and after a timeline ends; snapshot recovery mid-presentation and
  no replay of the interrupted round; the input lock/unlock across the whole round;
  terminal rounds (victory and defeat) stopping presentation and input; the closed
  discriminator set; and the per-battle keying of the accepted-resolution memory.

### Behavior Implemented

1. **The committed round is presented as one ordered timeline on one board**
   (`GAME_EVENTS.md` §1): the Player's board cycle and its ATK-gated damage step, the
   Boss's own board cycle on the same board (step 18a), the Boss's Passive, its Skill and
   its ATK-gated instance, then the terminal outcome — delivered order, nothing
   re-sorted, nothing fabricated.
2. **The committed board is applied once, where the round's board work ends.** TASK-254
   spliced the single board transition at the first combat stage; under ADR-026 §2 the
   round's board cycle also contains the Boss's step 18a board action, whose events are
   delivered *after* the Player's damage reports. The transition therefore moves to the
   end of that cycle, and the displayed board is the delivered committed board.
3. **An accepted resolution is never replayed.** `serverSequence` is strictly increasing
   (`SIGNALR_PROTOCOL.md` §3 item 2), so the presentation now remembers the newest one it
   has accepted — per battle, outliving the timeline — and refuses a duplicate or older
   batch after completion as well as during playback (previously only the in-flight
   comparison existed, so a re-delivery after the timeline ended replayed the round).
4. **A terminal round stops gameplay input.** `terminated` is deliberately not an
   input-locked state, so the outcome latch — not the guard — now refuses a Swap, Card
   cast, or Pet Skill cast after the handoff.
5. **Attribution and authority are unchanged where they were already correct**: the
   board cycle keeps the adapter's side-free actor (no party is claimed for an event
   whose delivered members name none), damage/Passive/`BossSkillCast`/`PowerChanged`
   attribution is untouched, and `Combo`/`MatchCount` readouts remain the authoritative
   `playerState` values.

### Validation Results

- `npm run test:run` (cwd `src/frontend/client`) — **PASS**: 24 files, **1085 passed, 0
  failed, 0 skipped** (baseline 1074; +11 new tests).
- `npx tsc --noEmit` — **PASS** (0 errors).
- `npm run build` (`tsc && vite build`) — **PASS**; only the pre-existing
  `@microsoft/signalr` Rollup comment and chunk-size warnings.
- **Negative control (the new tests detect the defect):** with
  `src/frontend/client/src/game/scenes/BattleScene.ts` temporarily restored to `HEAD`
  (only that file stashed), the same suite ran **7 failed / 3 passed** among the 11 new
  tests — the settle-placement tests, the replay/supersession tests, the recovery-replay
  test and the terminal-input test all failed against the pre-change behaviour. The three
  that still passed are the pure invariant guards (attribution, Combo/MatchCount
  readouts, per-battle keying). The stash was popped and the committed revision re-run
  green.
- Backend focused contract regression (tree unchanged; evidence that the contract this
  presentation consumes is intact):
  - `dotnet test tests/backend/GameServer.Domain.Tests --filter FullyQualifiedName~BattleEventEmissionTests`
    — **PASS** (46 passed, 0 failed)
  - `dotnet test tests/backend/GameServer.Application.Tests --filter "FullyQualifiedName~AtomicRoundOrchestrationTests|FullyQualifiedName~BattleStateServiceTests"`
    — **PASS** (89 passed, 0 failed)
- **Not run (environment):** the browser E2E suites (`verify:e2e:smoke`,
  `verify:e2e:collection`, `verify:e2e:history`) and `verify:runtime`. The documented
  local stack cannot be started here: no Docker daemon (`docker` is not installed), no
  PostgreSQL (nothing listening on 5432), and neither the API (5000) nor a Vite dev
  server (5173) is running — the harness's own prerequisites
  (`.ai/workflow/quality/local-e2e-verification.md` §2). Recorded as **not run**, never as
  passing. The browser binary itself is present (Edge), so the missing stack is the
  blocker, not the driver.

### Server Authority & Scope Verification

- [x] Zero client-authoritative gameplay logic: no match detection, clearing, gravity,
      spawn, RNG draw, damage, HP, Power, Combo, `MatchCount` or Passive derivation was
      added; the Boss's swap is never reverse-engineered from a board diff
      (`AGENTS.md` §10, ADR-001).
- [x] No wire, protocol, state-push or backend change; `BattleEventType`,
      `SIGNALR_PROTOCOL.md` §3.2.2's discriminator set and the state push are untouched.
- [x] `Turn` and `Sequence` are read and compared only, never written.
- [x] Adherence to `MVP_SCOPE.md` §1 (presentation of an existing system; no new system
      or content).
- [x] No unrelated file, generated artifact or balance evidence modified: the Phase A
      staged set equals the declared file set exactly, and every unowned/pre-existing
      path is disclosed and left unstaged.
- [x] No documentation change required: `docs/` owns gameplay rules and wire contracts
      (unchanged), and client-side event handling is explicitly not a documentation
      concern (`GAME_EVENTS.md` §3 item 3).
- [x] P-1…P-6 commit policy: single named task, exact declared set, no task ID in the
      subject, no batching, no amend/rebase, unowned paths disclosed.

### Review Pass (`quality/review.md` §1)

```text
Correctness     matches GAME_EVENTS.md §1/§1.1, ADR-026 §2, GAME_STATE.md §2.2/§5.1,
                SIGNALR_PROTOCOL.md §3.1/§3.2/§3.2.2 and TASK-254's splice rule (whose
                placement is preserved wherever the round's board cycle does not
                continue past a combat stage)
Architecture    presentation-only inside the Phaser scene; no runtime, protocol, backend
                or layer change; no second adapter, queue, registry or event bus
Scope           exactly the two declared files; nothing unrelated refactored
Tests           11 new focused tests + 1085 passing; negative control proves 7 of them
                detect the pre-change behaviour; backend contract suites re-run green
Documentation   none required (see above)
Security        untouched (no auth, session, transport or data-exposure surface)
Performance     one linear scan over the already-built phase list per delivered batch
Maintainability one small static helper and one predicate; no new abstraction
Determinism     no RNG, no timing-derived gameplay value, no client authority; the
                accepted-resolution memory reads the delivered counter alone
```
