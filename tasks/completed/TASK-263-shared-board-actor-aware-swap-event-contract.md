# TASK-263 — Shared-Board Actor-Aware Swap Event Contract

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
-->

---

## Metadata

```text
Task ID:           TASK-263
Type:              FEATURE
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     realtime
Supporting Agents: backend, client, testing, review
Workflow:          development/feature.md
Skills:            realtime/realtime-protocol-validation,
                   backend/api-contract-validation,
                   client/client-event-projection,
                   quality/documentation-consistency,
                   testing/test-scenario-generation,
                   quality/implementation-review
Dependencies:      TASK-258 (DONE), TASK-259 (DONE), TASK-260 (DONE), TASK-261 (DONE),
                   TASK-262 (DONE)
Declared Files:    docs/02-technical/GAME_EVENTS.md,
                   docs/02-technical/SIGNALR_PROTOCOL.md,
                   src/backend/GameServer.Domain/Match3/BattleEvent.cs,
                   src/backend/GameServer.Domain/Match3/SwapExecution.cs,
                   src/backend/GameServer.Application/Battle/BattleStateService.cs,
                   src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs,
                   src/frontend/client/src/game/scenes/BattleEventPresenter.ts,
                   src/frontend/client/src/game/scenes/BattleScene.ts,
                   tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs,
                   tests/backend/GameServer.Domain.Tests/PlayerEffectHealingTests.cs,
                   tests/backend/GameServer.Domain.Tests/ResourceGenerationTests.cs,
                   tests/backend/GameServer.Application.Tests/RoundTestSupport.cs,
                   tests/backend/GameServer.Application.Tests/BattleStateServiceTests.cs,
                   tests/backend/GameServer.Application.Tests/BossResponseTests.cs,
                   tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs,
                   tests/backend/GameServer.Api.Tests/BossWireProjectionTests.cs,
                   src/frontend/client/tests/BattleEventPresentation.test.ts,
                   src/frontend/client/tests/SceneLifecycle.test.ts
```

---

## Objective

Resolve the **G-1** contract gap reported by TASK-262 §Reported Contract Gaps: no delivered
member identifies the actor of a board-cycle event, and the Boss's swap coordinates are not
delivered at all. Define the actor-aware representation of a committed Player and Boss swap,
admit it to the `ReceiveEvents.events[]` schema, emit it from the two action sites, project it
on the wire, and consume it in the client so that each board cycle is attributed to the actor
the **delivered** members name — never to a side inferred from event order, timing, or a
client-held turn.

The protocol extension itself is already decided: `ADR-026` §Consequences requires "extending
SignalR protocol to transmit Boss swap coordinates (`SwapStarted`) for client presentation",
and `SIGNALR_PROTOCOL.md` §3.2.2 carries the exact proposed shape as a **Protocol Proposal**
whose "schema admission will be formalized upon downstream implementation". This task is that
formalization (`AGENTS.md` §18 item 1: the ADR already covers the change).

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md` §2 (atomic round; one `Turn`/`Sequence` per committed round), §4 (actor-relative cell effects; the acting side owns the target), §7 (CAS persistence vs. non-transactional SignalR delivery; idempotent retry), §8 (Boss board action; deterministic AI and deadlock reshuffle), **Consequences** (the required protocol extension)
- `docs/01-game-design/MATCH3_RULES.md` §2.1.1 (the two §1.0 cells, no direction field), §2.1.2 (validation order), §2.1.5 item 6 (a rejected Swap emits **no** Battle Event, including `SwapStarted`/`SwapResolved`), §2.1.6 (accepted Swap lifecycle: `SwapStarted` at step 3, `SwapResolved` at step 7, match detection at step 8), §8.1–§8.2
- `docs/01-game-design/GAME_RULES.md` §16 (canonical event list — `SwapStarted`, `SwapResolved` are already members), §17 (fixed resolution order; step 18a is the Boss's board action), §18 (server authority)
- `docs/02-technical/GAME_EVENTS.md` §1 (event ordering within one resolution, including the Boss Response Phase's `SwapStarted` with `actor="boss"`), §1.1, §1.2 (rejected action emits nothing), §2 `SwapStarted`/`SwapResolved` (trigger and payload), §3 items 1, 5, 6, 7
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3 (the three-member `ReceiveEvents` envelope), §3.1 items 1–6, §3.2.1–§3.2.5 (serialization boundary, discriminator, casing, enum representation, optionality), §3.2.12 (no member outside the schema), §3.2.14/§3.2.24 (`source` vocabulary this event's `actor` reuses), §3.2.25 (the omission convention), §7 (snapshot recovery, ADR-008), §8 items 1, 7
- `docs/03-decisions/ADR/ADR-008-battle-recovery.md` — recovery is a snapshot resynchronization, never an event replay
- `docs/03-decisions/ADR/ADR-025-swap-action-preconditions-and-idempotency.md` — `actionId` replay recognition; a retry must not re-execute or re-broadcast
- `docs/02-technical/ARCHITECTURE.md` §2.1 item 4 (Domain → Application/API transport projection → SignalR → Frontend), §2.2.1 rules 3–4 (the client coordinates and renders; it never computes, reorders, filters, or interprets a batch)
- `docs/03-decisions/README.md` §3 — an ADR owns *why*, `docs/02-technical/` owns *what*
- `tasks/completed/TASK-262-shared-board-battle-presentation-and-event-handling.md` — the G-1/G-2/G-3 report this task resolves (G-1 only)
- `tasks/completed/TASK-258-shared-board-gameplay-contract-audit-and-design.md` §8 C-WIRE-01, §9 item 5 (this task's pre-allocated scope)
- `tasks/TASK_LIFECYCLE.md` §6 — the two-stage commit protocol

---

## Preflight Findings

Verified by direct inspection at HEAD `1d62313` (working tree: the `.ai/` harness-compatibility
documentation sync, `.agents/`, `.claude/`, `.ai/PROJECT_STATE.md`,
`.ai/skills/discovery/task-readiness-assessment.md`, `.ai/workflow/core/task-selection.md` and
`.gitignore` are modified/untracked and are **not** this task's; they are disclosed as
unowned/pre-existing).

1. **Task ID and lifecycle authorization.** The highest task ID on disk is `TASK-262`
   (`tasks/completed/`), so the next sequential ID is `TASK-263` (`tasks/README.md` §3:
   "the next ID is the highest existing ID + 1 across all folders"). `TASK-263` is
   simultaneously the ID `TASK-258` §9 **pre-allocated** to exactly this scope —
   "TASK-263 — Realtime Protocol Extension and Client Event Projection … Admit `SwapStarted`
   to SignalR protocol. Update `BattleHub.cs` and `BattleEventPresenter.ts` to attribute events
   to `'player'` or `'boss'`" — while `TASK-258` §10 records that it created no downstream
   task file, and `TASK-262` §Out of Scope excluded creating it. The ID is therefore
   **verified, not assumed**. Authorization: the caller's task brief carries the objective,
   scope and acceptance criteria (the intake), and `ADR-026` (Accepted) already decided the
   protocol extension, so no new ADR is required (`AGENTS.md` §18 item 1). This manifest is
   required by `TASK_LIFECYCLE.md` §6.2 P-3 (Phase A ownership precondition).
   `tasks/active/`, `tasks/backlog/` and `tasks/blocked/` each contain only `.gitkeep`, so the
   WIP limit is 1, no other task is in flight, and no shared-file owner exists
   (`TASK_LIFECYCLE.md` §6.1 item 5 — no group commit is required).
2. **Decomposition (tasks/README §13) not applied, explicitly.** The change crosses Domain,
   Application, Api and client boundaries. It was kept as **one** task because it is one wire
   contract: the discriminator, its emitters, its projection and its consumer cannot be
   independently verified (a producer-only slice would be an unobservable contract, and a
   consumer-only slice would consume an event that does not exist), the skill count is within
   the §12 budget, and this brief requires one task and to stop after it. The repository has
   the same precedent (`TASK-261` crossed Domain + Application + Api + tests in one slice).
3. **The gap is real and reproducible in the code.** `BattleEventType` has exactly sixteen
   members and no `SwapStarted`/`SwapResolved` (`BattleEvent.cs:53-251`);
   `BattleEventWireProjection.Project(BattleEvent)` (`:732-755`) projects exactly those
   sixteen and throws on any other kind; `SwapExecutor.Execute` (`SwapExecution.cs:488`) and
   the Boss phase (`BattleStateService.cs:1641`) both emit `BattleEventBuilder.Build(...)`
   alone, which produces only `MatchCreated`/`CascadeCreated`/`ComboChanged`/`GemMatched`. The
   delivered batch therefore contains no member naming either actor's board action.
4. **The frontend cannot attribute a board cycle, and does not claim to.** The one adapter
   `BattleEventPresenter.adaptPresentedEvent` (`:868-961`) answers `actor: 'swap'` — a
   deliberately **side-free** value — for `MatchCreated`, `GemMatched`, `CascadeCreated` and
   `ComboChanged`, and its doc block states that none of those four carries a party member.
   `SceneLifecycle.test.ts:7495-7541` pins that answer for a two-cycle round, so the claim is
   tested, not incidental.
5. **The frontend does not predict the Boss's move; it does not present it at all.** No
   client-side boss-move prediction, board diffing, or optimistic board mutation exists
   (`RuntimeBoundaries.test.ts:565-606`; `BattleEventPresenter` contains no such logic). The
   swap phase is opened from `committedSwapCells` (`BattleScene.ts:3605-3615`), which is set
   only by *this client's own* `Swap` submission (`:3127`); the Boss's board cycle reaches the
   screen only as the final committed board through the `settle` phase. Requirement "uses
   committed, validated event data rather than predicting Boss moves locally" is therefore
   satisfied by construction today and must remain so.
6. **Two documented inconsistencies had to be decided, and are recorded here rather than
   silently resolved** (`AGENTS.md` §4):
   - `GAME_EVENTS.md` §2's `SwapStarted` payload line reads "from-cell, to-cell" and omits the
     `actor` member that §1's own ordering list uses in the same document
     (`SwapStarted (Boss move on shared board, actor="boss")`) and that §3.2.2's proposal fixes.
     The payload line is incomplete, not contradictory; the correction is the smallest one —
     add `actor` to the §2 payload line — and it is owned by `GAME_EVENTS.md` §2, which §3.2.1
     names as the owner of "what each event means and what its payload contains".
   - `GAME_EVENTS.md` §2's trigger line for the pair reads "Client Swap request received",
     while the more specific owner (`MATCH3_RULES.md` §2.1.6 steps 2–3, §2.1.5 item 6) places
     `SwapStarted` **after** validation passes and emits nothing for a rejection. The trigger
     wording is corrected to the lifecycle the domain document owns.
   - Both corrections are documentation corrections in the document that owns the statement;
     no gameplay rule, no event name, and no ordering changed.
7. **`SwapResolved`'s documented payload is constant on the delivered path.** §2 defines it as
   "whether the swap was committed or reverted", but a rejected action emits nothing at all
   (`GAME_EVENTS.md` §1.2, `MATCH3_RULES.md` §2.1.5 item 6), so the `reverted` case is
   unreachable in a delivered batch and every delivered instance reports `committed`.
   Recorded in §3.2.27 as a stated property of the event, not as a branch a client may use.
8. **Compatibility surface (measured, not guessed).** Prepending two non-board events to each
   board cycle moves every offset derived from `RoundTestSupport.PlayerBoardEvents`
   (`:208-209`), because that helper **re-derives** the player's board events from
   `BattleEventBuilder.Build` instead of slicing the delivered list — which mis-slices
   `BossBoardEvents` (`:225-250`), and with it `BossClearedAtkGem`, `BossPowerMatchGroups`, and
   the two duplicated scans `BattleStateServiceTests.FirstBossBoardEventIndex` (`:2413-2424`)
   and `BossResponseTests.BossBoardStartIndex` (`:1645-1658`). The repair is to read the
   delivered list's contiguous board-event runs instead of re-deriving them, which restores
   every consumer of those helpers unchanged. The remaining updates are the closed-set gates
   (`BattleEventEmissionTests:1321-1384`, the `BattleEvent` member-reflection set `:960-1093`,
   the `Describe` switch `:1519-1530`) and the Api-side projection mirrors
   (`ApiIntegrationTests.ProjectOne:4201-4303`, the member allow-table `:4065-4097`, the
   closed discriminator list `:4003-4036`).
9. **Idempotency and recovery are untouched by construction.** `SwapExecutionResult.IdempotentReplay`
   hard-codes an empty event list (`SwapExecution.cs:277-278`) and `BattleHub` returns before
   broadcasting (`BattleHub.cs:1115-1118`), so a retry cannot re-broadcast the new events.
   The new events carry no state member, add no `BattleState` field, add no hub method, and add
   no member to the nine-member state push, so ADR-008 snapshot recovery is unchanged
   (`SIGNALR_PROTOCOL.md` §7; three tests already pin the idempotent no-rebroadcast property).

---

## Scope

### In Scope

- **Formalize the G-1 contract** as the two wire discriminators `SwapStarted`
  (`{ type, actor: "player" | "boss", fromCell, toCell }`) and `SwapResolved`
  (`{ type, committed: boolean }`), with their payload, validation rules, emission points,
  batch ordering and wire projection, recorded in the two documents that own them
  (`GAME_EVENTS.md` §2 semantics/payload; `SIGNALR_PROTOCOL.md` §3.2.2 + §3.2.26/§3.2.27 wire
  schema).
- **Domain payload and factories**: `BattleEventType.SwapStarted`/`SwapResolved`, the two
  payload values, the two `BattleEvent` accessors, and the two factories; `SwapExecutor` emits
  the Player's pair at the head of its board cycle (after validation, before the exchange is
  reported).
- **Application emission**: the Boss phase emits its own pair, immediately before the Boss's
  board cycle, carrying the deterministic AI's own selected cells.
- **Wire projection**: two DTO factories and two `Project` arms in
  `BattleEventWireProjection`, with `actor` projected from the Domain two-sided identity as a
  lowercase contract name (the same projection rule `PowerChanged.source` uses).
- **Client consumption**: the parser admits both discriminators; the single adapter attributes
  `MatchCreated`/`GemMatched`/`CascadeCreated`/`ComboChanged` to the actor of the **delivered**
  `SwapStarted` that opens their cycle, and falls back to the existing side-free `swap` actor
  when a batch delivers no swap boundary; each delivered `SwapStarted` opens the swap step for
  the actor and the coordinates it names, so the swap gesture is presented from committed,
  validated event data for whichever actor acted. The pre-existing local pair remains only as
  the documented fallback for a batch that delivers no swap boundary.
- **Contract and regression tests** for the payload, validation, emission points, batch
  ordering, the wire projection, the client attribution, and the untouched idempotency and
  snapshot-recovery behaviour.
- **Reporting G-2 and G-3** with evidence, without changing the contract for them.

### Out of Scope

- Any Boss-swap **choreography** beyond the delivered step: no Boss-specific swap animation,
  camera work, deadlock-reshuffle presentation, or per-actor pacing branch. The generic swap
  step that a delivered `SwapStarted` opens is what this task makes actor-aware; the Boss's own
  presentation design remains `TASK-264`.
- Adding `BoardReshuffled` or any other discriminator, and any change to the deadlock-reshuffle
  implementation (`BossAiEngine` / `BattleStateService`) — G-3 is investigated and reported.
- Distinguishing the Boss's Combo run on the wire (`Game-2`) — investigated and reported; the
  authoritative `playerState.combo`/`matchCount` readouts are not touched.
- Any hub method, subscription, state-push member, `BattleState` field, Redis key, database
  column, gameplay rule, damage/HP/Power value, Boss AI scoring, balance number, or
  animation-timing-derived gameplay value.
- Any client-side Match-3 or combat computation (match detection, clearing, gravity, spawn,
  RNG, damage, Power, Combo folding, or reverse-engineering the Boss's swap from a board diff)
  — `AGENTS.md` §10, ADR-001.
- Re-baselining `tasks/artifacts/*` balance evidence; touching `tasks/completed/*`;
  creating `TASK-264` or any later manifest.
- Broad refactoring of the three duplicated test scan helpers beyond what the new events
  require, and any redesign of the Move Log, the orchestration machine, or the phase model.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-263:

```text
docs/02-technical/GAME_EVENTS.md
docs/02-technical/SIGNALR_PROTOCOL.md
src/backend/GameServer.Domain/Match3/BattleEvent.cs
src/backend/GameServer.Domain/Match3/SwapExecution.cs
src/backend/GameServer.Application/Battle/BattleStateService.cs
src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs
src/frontend/client/src/game/scenes/BattleEventPresenter.ts
src/frontend/client/src/game/scenes/BattleScene.ts
tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs
tests/backend/GameServer.Domain.Tests/PlayerEffectHealingTests.cs
tests/backend/GameServer.Domain.Tests/ResourceGenerationTests.cs
tests/backend/GameServer.Application.Tests/RoundTestSupport.cs
tests/backend/GameServer.Application.Tests/BattleStateServiceTests.cs
tests/backend/GameServer.Application.Tests/BossResponseTests.cs
tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs
tests/backend/GameServer.Api.Tests/BossWireProjectionTests.cs
src/frontend/client/tests/BattleEventPresentation.test.ts
src/frontend/client/tests/SceneLifecycle.test.ts
```

*(The task record itself is excluded from the implementation declared file set, and the list
strictly matches the `Declared Files:` field in `## Metadata`. `BattleHub.cs` is deliberately
**not** declared: it already projects through `BattleEventWireProjection.Project` and enumerates
no event name, so `TASK-258` §9's "Update `BattleHub.cs`" resolves to the projection type that
sits at that boundary — verified by inspection, not assumed.)*

**Shared-file ownership.** `BattleEvent.cs`, `SwapExecution.cs`, `BattleStateService.cs` and
`BattleEventWireProjection.cs` were last owned by `TASK-260`/`TASK-261`; `BattleScene.ts` and
`SceneLifecycle.test.ts` by `TASK-262`. All are `DONE`, immutable, and no task holds an active
manifest, so no concurrent shared-file owner exists and no joint group commit is required
(`TASK_LIFECYCLE.md` §6.1 item 5).

**Unowned / pre-existing disclosure (P-3).** `git status --porcelain` at pickup reports
**modified**: `.ai/HARNESS_COMPATIBILITY.md`, `.ai/HARNESS_MAPPING.md`,
`.ai/agents/AGENT_SELECTION.md`, `.ai/agents/orchestrator.md`, `.ai/skills/README.md`,
`.ai/skills/SKILL_REGISTRY.md`, `.ai/workflow/README.md`, `.gitignore`; **untracked**:
`.agents/`, `.ai/PROJECT_STATE.md`, `.ai/skills/discovery/task-readiness-assessment.md`,
`.ai/workflow/core/task-selection.md`, `.claude/`. None is this task's; none is staged, and all
are disclosed in the Phase A commit body.

---

## Acceptance Criteria

- [x] **The actor of each committed board action is unambiguous.** Every board cycle in a
      delivered batch is preceded by exactly one `SwapStarted` naming its actor, and the
      emitted pair is present for the Player's committed Swap and for the Boss's step 18a
      board action, in that order.
- [x] **The payload is exactly the documented one.** `SwapStarted` carries `type`, `actor`
      (`"player"`/`"boss"`) and the two §1.0 cells `fromCell`/`toCell`, always present;
      `SwapResolved` carries `type` and `outcome`, always present; neither carries a member
      outside its §3.2.x table (`SIGNALR_PROTOCOL.md` §3.2.12), and the enum-valued `actor` is
      projected to its lowercase contract name, never the ordinal (§3.2.4).
- [x] **Validation rules hold.** No pair is emitted for a rejected action — not for an invalid
      cell index, a non-adjacent or same-cell pair, an already-applied/stale action, or a
      swap producing no Match (`GAME_EVENTS.md` §1.2, `MATCH3_RULES.md` §2.1.5 item 6); the
      delivered cells are the validated action's own cells in the order the action named them
      (`MATCH3_RULES.md` §2.1.1 item 2: no direction field, the pair is the identity).
- [x] **Ordering is the documented one.** Within one batch: Player `SwapStarted`,
      Player `SwapResolved`, the Player's board cycle, the Player's combat stages, then the
      Boss's `SwapStarted`, the Boss's `SwapResolved`, the Boss's board cycle, the Boss's
      stages, and the terminal outcome last (`GAME_EVENTS.md` §1, `MATCH3_RULES.md` §2.1.6
      steps 3/7/8, `GAME_RULES.md` §17). Nothing is re-sorted or filtered.
- [x] **Player and Boss swap coordinates are available for presentation**, delivered rather
      than derived: the Player's pair is the committed action's own cells, and the Boss's pair
      is the deterministic AI's selected pair — recorded before the exchange so the delivered
      indices are the cells the action named.
- [x] **The client attributes a board cycle from the delivered actor only.** The one adapter
      answers the actor of the `SwapStarted` that opens the cycle, and the side-free `swap`
      actor only when the batch delivers no swap boundary; no phase claims a party the
      delivered members do not name, the single-adapter invariant of TASK-252 holds, and no
      per-actor branch, registry, pipeline or second vocabulary is introduced
      (`AGENTS.md` §9, `ARCHITECTURE.md` §2.2.1 rule 4).
- [x] **Existing clients, sequence handling and snapshot recovery remain correct.** The
      `ReceiveEvents` envelope stays three members; `serverSequence` semantics are unchanged;
      `Turn`/`Sequence` are never written client-side; the state push stays nine members; the
      closed discriminator set grows by exactly the two admitted names; an idempotent retry
      still broadcasts nothing; and ADR-008 snapshot recovery is unchanged.
- [x] **Backend contract tests and frontend tests pass** at MEDIUM validation depth, with the
      closed-set gates updated only after the two names were justified in
      `GAME_EVENTS.md`/`SIGNALR_PROTOCOL.md` (documentation first, `AGENTS.md` §17).
- [x] **G-2 and G-3 are reported, not redesigned**: the Boss's Combo run is not separated on
      the wire and the authoritative `Combo`/`MatchCount` readouts are untouched; no
      `BoardReshuffled` discriminator is added and the deadlock-reshuffle interaction is
      documented as an open decision.
- [x] Zero unrelated working-tree change: the Phase A staged set equals the declared file set
      exactly, and every unowned/pre-existing path is left unstaged and disclosed.

---

## Current State

The shared-board round already resolves as one atomic transaction (`TASK-261`) and is already
presented as one ordered timeline per delivered batch (`TASK-262`). The wire carries
sixteen discriminators and no swap boundary of either actor; `BattleEventWireProjection` is the
single owner of the schema; the client's one adapter is the single interpreter of a delivered
event into `(actor, action, kind)` and answers a deliberately side-free `swap` actor for the
board cycle.

What this task changes is exactly that: the two missing discriminators, their two emitters,
their projection, and the one adapter's answer for a board cycle — from "a swap whose side is
unknown" to "the swap the delivered `SwapStarted` names".

The pre-existing client-side swap phase source (`committedSwapCells`, set by the client's own
request) is **not** a Boss-move prediction and is not removed: it remains the documented
fallback for a batch that delivers no swap boundary, and it is displaced by the delivered
event whenever one is present.

---

## Implementation Notes

- **Order of work (documentation first).** `GAME_EVENTS.md` §2 (payload/trigger corrections,
  §Preflight Findings 6) → `SIGNALR_PROTOCOL.md` §3.2.2 (admit the two names into the closed
  table, retire the Protocol Proposal) + a new §3.2.26/§3.2.27 with the member tables and
  validation rules → Domain → Application → Api → tests → client → client tests.
- **One payload type, no second vocabulary.** `SwapStarted`'s actor is the existing two-sided
  Domain identity `DamageParty` (`GameServer.Domain.Combat`), which is already the acting side
  for the Boss's board action (`ResourceGenerator.ApplyActorMatchEffects(DamageParty.Boss, …)`),
  and which is projected to its lowercase contract name by the same rule
  `PowerChanged.source` uses. No new enum and no new wire vocabulary is introduced.
- **Emission is a prepend, not a second pipeline.** `SwapExecutor` builds the same
  `BattleEventBuilder.Build(...)` board cycle and places the pair ahead of it;
  `BattleStateService`'s Boss phase does the same for the Boss's selected pair. No detection,
  no RNG draw, no state field, no counter changes; `BattleEventBuilder` gains no parameter and
  no overload (`BattleEventEmissionTests:1412-1426` pins its single public entry point, and
  `:1428-1441` pins `SwapExecutor`'s).
- **The client tracks the delivered actor in the same forward pass it already performs.**
  `buildPresentationPhases` is a single pass over the delivered batch; it carries the newest
  delivered swap actor forward and passes it to the one adapter as an explicit argument
  (default: none). The DEV Move Log's row factory receives the same value so its `actor/action`
  column cannot contradict the timeline.
- **The `PresentationActor` vocabulary is not widened.** `player` and `boss` already exist;
  `swap` remains the honest side-free answer when no delivered member names a side (including
  the `settle` step, which converges the round's board on behalf of both cycles).
- **No timing, no prediction, no derivation**: the swap step's cells come from the delivered
  event; the board transition still happens once, from the delivered state, where
  `TASK-262` placed it.

---

## Testing Requirements

### Required Verification

```text
[ ] Unit tests         — Domain: the emitted pair's presence, payload, and position; the
                         payload accessors and factories; an unknown-kind projection still
                         throws; the `Describe`/closed-set gates updated to the two names
[ ] Integration tests  — Application: the full round's event list, in order, for both board
                         cycles (Player pair before the Player cycle, Boss pair before the
                         Boss cycle, outcome last); a rejected action emits no pair; the Boss's
                         delivered pair equals the AI's selected pair for that board
[ ] Contract tests      — Api: the wire DTO of both events, member-for-member, camelCase, no
                         member outside the table; the projection stays one-to-one and
                         order-preserving; SignalR JSON round trip; the closed discriminator
                         list grows by exactly two
[ ] Frontend tests      — the parser admits both names and still rejects a malformed payload;
                         the adapter's actor for a board cycle is the delivered
                         `SwapStarted.actor`; the side-free fallback is preserved for a batch
                         that delivers no swap boundary; a delivered Player and Boss swap each
                         open their own step with their own coordinates; the single-adapter and
                         no-per-actor-branch invariants; idempotency/recovery tests unchanged
[ ] Gameplay scenario   — Given a committed shared-board round in which the Player's Swap and
                         the Boss's step 18a board action both resolve, When the server
                         delivers the state push and one `ReceiveEvents` batch, Then the batch
                         opens the Player's board action with `SwapStarted(actor="player")` and
                         its coordinates, And the Player's Match/Cascade/Combo events follow
                         it, And the Boss's board action is opened by
                         `SwapStarted(actor="boss")` with the Boss's own coordinates, And the
                         Boss's board cycle follows it, And the terminal outcome is last, And
                         the client attributes each cycle to the actor the event named
```

### Key Edge Cases

- A rejected action in all four rejection reasons: no `SwapStarted`, no `SwapResolved`, no other
  event, no `Sequence` change (`GAME_EVENTS.md` §1.2).
- A round in which the Player's attack is ATK-gated off (ADR-026 D1): both board cycles and both
  swap pairs are still delivered, in order.
- A terminal round in either direction: the outcome event is last, after the Boss's pair and
  cycle (or after the Player's pair when the Boss phase is skipped).
- The deadlock case: the Boss's pair is the AI's pair **on the reshuffled board**, i.e. indices
  into a board the client has not seen before the round's single board transition — reported
  under G-3, not silently presented as a board the client already holds.
- An idempotent retry (`actionId` replay): accepted, no batch, no duplicate pair.
- Snapshot recovery mid-presentation: unchanged — the snapshot carries no event, the timeline
  aborts, and no pair is replayed.
- A batch that delivers no swap boundary (Card cast, Boss-only response, or a synthetic
  fixture): the client keeps the existing side-free board attribution and the existing
  local-pair fallback, and presents exactly what it presented before.

---

## Stop Conditions

- If the wire schema the two events need is not the one `SIGNALR_PROTOCOL.md` §3.2.2's proposal
  and `GAME_EVENTS.md` §1/§2 already fix: **STOP** and report the smallest concrete contract
  decision needed (`AGENTS.md` §20). This task does not authorize a member the documents do not
  define; every member it adds is traceable to §2's payload, §1's `actor` usage, or §3.2.2's
  proposal.
- If `GAME_EVENTS.md` §1 and §2 disagree with `MATCH3_RULES.md` §2.1.6 about the emission
  points or the ordering: report per `AGENTS.md` §4 rather than picking one. (Preflight
  finding 6 records the two corrections this task makes, both in the document that owns the
  statement.)
- If a required change would alter a persisted contract, a gameplay rule, the Boss AI, or any
  `BattleState` field: STOP and escalate.
- If implementing the client attribution requires a second actor vocabulary, a stateful
  adapter, a per-actor branch, or a client-derived actor: STOP — that would contradict
  `AGENTS.md` §9/§10 and TASK-252's invariant.

---

## Reported G-2 / G-3 (investigated here, not resolved here)

```text
G-2  Player and Boss combo runs.  Investigated.  `BattleState.Combo`/`MatchCount` stay the
     Player action's counters (GAME_STATE.md §2.2, TASK-261 Remaining Issue §11) while the
     Boss phase's `ComboChanged` reports the Boss resolution's own run.  The frontend is
     correct as implemented and is not changed: the `COMBO ×N` / `MATCHES N` readouts render
     `BattleState.playerState` and never fold a delivered Combo value, and the Boss cycle's
     `ComboChanged` only *triggers* that render.  A wire representation that separates the two
     runs is a contract decision of its own and is NOT made here.

G-3  `BoardReshuffled`.  Investigated.  ADR-026 §8 says the Boss AI's deadlock reshuffle emits
     `BoardReshuffled`; `GAME_EVENTS.md` §1/§2 (the event list's owner, higher precedence per
     AGENTS.md §2) defines no such event, `GAME_RULES.md` §16 does not list it, and
     `BattleEventType` has no member for it.  No discriminator and no DTO member is added here.
     The interaction this task makes visible: the Boss's delivered `SwapStarted` coordinates
     are indices into the board the AI selected the move on, i.e. the **reshuffled** board in
     the deadlock case, which the client has not seen until the round's single board
     transition.  Resolving it needs a GAME_RULES.md §16 / GAME_EVENTS.md §2 definition first
     (or an ADR-026 §8 correction), which is a Product Owner documentation decision.
```

---

## Completion Evidence

### Commit

- Implementation slice (Phase A): `1a858283140a554c801b3bdfe74babfcb8c86eb2`
- Subject: `feat(realtime): admit the shared board's actor-aware swap boundary` (no task ID in
  the subject, `TASK_LIFECYCLE.md` §6.2 P-4; the task ID is in the body under `owns:`)
- Owns: `TASK-263`
- Files: the declared file set, exactly (18 paths — 8 implementation/documentation, 10 test)
- Shared with: `none`
- Unowned / pre-existing (left unstaged, disclosed in the commit body):
  `.ai/HARNESS_COMPATIBILITY.md`, `.ai/HARNESS_MAPPING.md`,
  `.ai/agents/AGENT_SELECTION.md`, `.ai/agents/orchestrator.md`,
  `.ai/skills/README.md`, `.ai/skills/SKILL_REGISTRY.md`,
  `.ai/workflow/README.md`, `.gitignore`, `.agents/`, `.ai/PROJECT_STATE.md`,
  `.ai/skills/discovery/task-readiness-assessment.md`,
  `.ai/workflow/core/task-selection.md`, `.claude/`
- **Disclosed correction (Phase A message only).** The slice was first committed with a
  message whose long first line became the subject and carried the task ID, which violates
  P-4. It was corrected **immediately, before Phase B, with a message-only
  `git commit --amend`** on the just-created, unpushed commit — the tree is byte-identical
  (`eaaba07992d7a5f74b87d5892ff45ecb31388329` before and after), the commit count is
  unchanged (no duplicate implementation commit, no other commit on top, nothing rewritten in
  shared history), and the SHA recorded above is the corrected one. `TASK_LIFECYCLE.md` §6.4
  item 6 prohibits rewriting history to repair an ambiguous state; this was not an ambiguous
  state but a template error in my own message, and it is recorded here rather than left
  silently embedded in history.
- Completion record filing (Phase B): this completion record alone, filed under its own
  bookkeeping commit (a direct addition: the manifest was never staged in Phase A,
  `TASK_LIFECYCLE.md` §6.1 Phase A step 1).

### Changed Files

**Documentation (the contract, first — `AGENTS.md` §17).**

- `docs/02-technical/GAME_EVENTS.md` §2 `SwapStarted`/`SwapResolved` — the `actor` member added
  to the `SwapStarted` payload line (§1 of the same document already used
  `actor="boss"`), the trigger corrected to the lifecycle `MATCH3_RULES.md` §2.1.6 owns
  (steps 3 and 7, after validation), and the rejected-action and `reverted`-is-unreachable
  points stated.
- `docs/02-technical/SIGNALR_PROTOCOL.md` — version 2.22; §3.2.2's discriminator table admits
  `SwapStarted`/`SwapResolved`, the ADR-026 / TASK-259 Protocol Proposal is discharged and
  removed, and item 2 states why the other four `GAME_EVENTS.md` §2 names stay outside the set;
  §3.2.12 item 1's enumeration accounts for the two new subsections; **§3.2.26 `SwapStarted`**
  and **§3.2.27 `SwapResolved`** own the member tables, the value vocabularies, the emission
  points, the ordering, and the "no other member" rules.

**Backend implementation.**

- `src/backend/GameServer.Domain/Match3/BattleEvent.cs` — `BattleEventType.SwapStarted` /
  `SwapResolved` with their documentation; the two payload values `SwapStartedEvent`
  (`DamageParty`, both cells) and `SwapResolvedEvent` (the committed fact); the two
  `BattleEvent` fields, accessors and factories `ForSwapStarted` / `ForSwapResolved`; the
  `ToString` arms; the enum's stale member-count sentence corrected.
- `src/backend/GameServer.Domain/Match3/SwapExecution.cs` — `SwapExecutor.Execute` opens its
  board cycle with the Player's pair, built from the same committed resolution.
- `src/backend/GameServer.Application/Battle/BattleStateService.cs` — the step 18a Boss phase
  emits its own pair from the deterministic AI's selected cells immediately before the Boss's
  board cycle.
- `src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs` — the DTO's `actor`,
  `fromCell`, `toCell` members; the `SwapStarted` / `SwapResolved` factories; the two `Project`
  arms and `ProjectSwapResolved`; the class and member documentation and the closed-set
  fallback message updated. `BattleHub.cs` needs no change: it already projects through this
  type and enumerates no event name.

**Frontend implementation.**

- `src/frontend/client/src/game/scenes/BattleEventPresenter.ts` — `PresentedSwapStarted` /
  `PresentedSwapResolved`, their parsers, their `formatInBattleEvent` lines, the union members;
  `deliveredSwapOf` as the single projection of the delivered boundary; `adaptPresentedEvent`
  gains the optional delivered board-action actor and attributes `MatchCreated`, `GemMatched`,
  `CascadeCreated` and `ComboChanged` with it (side-free `swap` when the batch delivered no
  boundary); `SwapResolved` presents no step of its own; the actor-map documentation updated.
- `src/frontend/client/src/game/scenes/BattleScene.ts` — `buildPresentationPhases` carries the
  delivered actor forward through its existing single pass and hands it to the one adapter;
  the swap step is opened by the delivered `SwapStarted`; the submitted pair survives only as
  the documented fallback for a batch that delivers no boundary; `playSwapPhase` takes its
  cells from the delivered event; the DEV Move Log's row factory receives the same actor.

**Tests.**

- `tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs` — the closed-name gate
  (18) and the `BattleEvent` member-reflection set updated; the `Describe` helper's two new
  arms; **8 new tests** for the boundary pair's presence, position, payload, order-echoing,
  the rejected-action case, and the throwing accessors.
- `tests/backend/GameServer.Domain.Tests/ResourceGenerationTests.cs`,
  `PlayerEffectHealingTests.cs` — the two "this stage adds no event" gates admit the action's
  own boundary pair while still asserting the stage adds none.
- `tests/backend/GameServer.Application.Tests/RoundTestSupport.cs` — `PlayerBoardEvents` /
  `BossBoardEvents` read the delivered list's two contiguous board-event runs instead of
  re-deriving the first (which the boundary pair had shifted) — the repair that restores every
  consumer of `BossClearedAtkGem` / `BossPowerMatchGroups`.
- `tests/backend/GameServer.Application.Tests/BattleStateServiceTests.cs`,
  `BossResponseTests.cs` — the two duplicated position scans find the second board-event run.
- `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs` — the closed discriminator list,
  the member allow-table (two arms), and the mirror `ProjectOne` (two arms).
- `tests/backend/GameServer.Api.Tests/BossWireProjectionTests.cs` — **6 new tests** for the two
  events' member sets, camelCase names, cell `0`, the actor projection, the absent actor/cells
  on `SwapResolved`, and the pair's order preservation.
- `src/frontend/client/tests/BattleEventPresentation.test.ts` — the closed set (18) and the
  parser's admitted names (16); the adapter map with the boundary; **4 new tests** (parser
  round trips for both, malformed payloads, batch attribution, the one boundary projection);
  the TASK-246 ignored-name list narrowed to the names still outside the set; the Move Log's
  in-battle list, fixture and rows.
- `src/frontend/client/tests/SceneLifecycle.test.ts` — the TASK-262 "discriminator set does not
  admit" test narrowed to the names still unadmitted, plus a **new TASK-263 block (5 tests)**
  for per-cycle attribution, the delivered pair displacing the submitted one, the fallback, an
  unrecognised actor word, and a malformed boundary; the TASK-253 closed-type list and
  every-type fixture extended, with the board-cycle rows asserted as the delivered actor's.

### Validation Results

- `dotnet test tests/backend/GameServer.Domain.Tests` — **PASS**: 1613 passed, 0 failed
  (baseline 1605; +8 new). The pre-change baseline of the focused file was 46/46.
- `dotnet test tests/backend/GameServer.Application.Tests --filter "FullyQualifiedName!~Balance"`
  — **PASS**: 632 passed, 0 failed. This covers the atomic-round ordering, the Boss Response
  ordering, the ATK-gated instances, the terminal paths, the idempotent-replay no-rebroadcast
  guards and the reconnect/recovery tests.
- `dotnet test tests/backend/GameServer.Api.Tests` — **PASS**: 365 passed, 0 failed. This
  includes the end-to-end `ReceiveEvents` order/JSON comparison, the member allow-table, the
  SignalR JSON boundary, the forbidden-name gate and the new projection tests.
- **Pre-existing failures, verified not caused by this task.** The full Application project run
  reports 10 failures, all in `Balance.Tests`
  (`BalanceEvidenceMeasurementCorrectionTests` ×8, `BalanceSimulatorTests.H06`,
  `BalanceBaselineMatrixTests.TASK194_AllSeventyFiveRuns`). The same 10 tests fail, with the
  same names, in a **pristine `git worktree` at HEAD `1d62313`** —
  `git worktree add --detach <tmp> HEAD` → `dotnet test … --filter "FullyQualifiedName~Balance"`
  → 52 passed / 10 failed. They assert archived TASK-194/TASK-197 balance evidence that the
  ADR-026 mechanics change invalidated (`TASK-261` Remaining Issue §4), and
  `MetricAccumulator`'s type switch has no `default` arm, so the two new events are ignored by
  the harness rather than consumed. Out of this task's scope; not touched. The temp worktree
  was removed and pruned.
- `npm run build` (cwd `src/frontend/client`: `tsc && vite build`) — **PASS**; only the
  pre-existing `@microsoft/signalr` Rollup comment and chunk-size warnings.
- `npx vitest run` — **PASS**: 24 files, **1094 passed, 0 failed** (baseline 1085; +9 new).
- **Not run (environment):** the browser E2E suites (`verify:e2e:smoke`,
  `verify:e2e:collection`, `verify:e2e:history`) and `verify:runtime`. The documented local
  stack cannot be started here (no Docker daemon, no PostgreSQL, no API or Vite dev server) —
  the harness's own prerequisites. Recorded as **not run**, never as passing.
- **Working-tree hygiene.** Running the balance suites regenerates
  `tasks/artifacts/TASK-194-baseline-simulation-results.json` and
  `tasks/artifacts/TASK-197-controlled-balance-experiments.json` (a 7 M-line diff of archived
  evidence, unrelated to this task). Both were restored to `HEAD` with
  `git checkout -- tasks/artifacts/` after verification, so no unrelated change is staged or
  committed.

### Server Authority & Scope Verification

- [x] No client-authoritative gameplay logic was added: no match detection, clearing, gravity,
      spawn, RNG draw, damage, HP, Power, Combo, `MatchCount`, or Passive derivation, and no
      swap is reverse-engineered from a board difference (`AGENTS.md` §10, ADR-001).
- [x] No gameplay rule, damage value, Boss AI weight, balance number, or persisted contract
      changed; no `BattleState` field, Redis key, database column, hub method, subscription or
      state-push member was added.
- [x] `Turn` and `Sequence` are read and compared only, never written client-side.
- [x] Adherence to `MVP_SCOPE.md` §1 (a protocol extension to an existing system; no new
      system or content).
- [x] Documentation first (`AGENTS.md` §17): the two names were justified in `GAME_EVENTS.md`
      §2 and `SIGNALR_PROTOCOL.md` §3.2.26–§3.2.27 before the closed-set gates were edited.
- [x] The two documentation corrections this task makes (the missing `actor` member and the
      trigger wording) are recorded in §Preflight Findings 6 rather than resolved silently
      (`AGENTS.md` §4), and both are in the document that owns the statement.
- [x] P-1…P-6 commit policy: single named task, exact declared set, no task ID in the subject,
      no batching, no amend/rebase, unowned paths disclosed.

### Review Pass (`quality/review.md` §1)

```text
Correctness     matches GAME_EVENTS.md §1/§2/§1.2, MATCH3_RULES.md §2.1.1/§2.1.5/§2.1.6,
                SIGNALR_PROTOCOL.md §3.1/§3.2.2/§3.2.4/§3.2.12/§3.2.26/§3.2.27 and ADR-026 §2/§8
Architecture    Domain payload + two emission sites + the one transport projection + the one
                client adapter; no new module, bus, registry or second pipeline
Scope           exactly the declared 18 files; nothing unrelated refactored
Tests           8 new Domain, 6 new Api projection, 9 new frontend; all backend and frontend
                suites green except the 10 verified pre-existing Balance failures
Documentation   GAME_EVENTS.md and SIGNALR_PROTOCOL.md updated as the owning documents
Security        untouched (no auth, session, transport or data-exposure surface)
Performance     two extra events per committed board action; the client's existing single
                forward pass gains one lookup per event
Maintainability one payload pair, two factories, one projection helper; no new abstraction
Determinism     no RNG, no timestamp, no identifier, no timing-derived value; the delivered
                cells are the action's own and the actor is a delivered member
```
