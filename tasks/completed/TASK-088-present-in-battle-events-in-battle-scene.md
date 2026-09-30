# TASK-088 — Present In-Battle Server Events in BattleScene

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and src/ by path and section; copies only what an
  implementer needs to know where to look.

  This is the in-battle event presentation task explicitly deferred by
  TASK-087's Out of Scope ("In-battle event presentation beyond the outcome
  handoff (match / cascade / combo / damage feedback) - a separate
  client-presentation task", tasks/backlog/TASK-087...md:124-126). Every
  other link of the Phase 1 pipeline exists: events are emitted
  (TASK-006), delivered (TASK-007/007A/007A3), swappable by the player
  (TASK-069), and a finished battle reaches ResultScene (TASK-087) - but a
  resolved Swap still produces no player-visible match/cascade/combo/damage
  feedback, because no production code consumes the ten non-outcome event
  types the server already delivers (SIGNALR_PROTOCOL.md §3.2.6-§3.2.18).
-->

---

## Metadata

```text
Task ID:           TASK-088
Type:              FEATURE (client-side handling of each event is explicitly a
                   client implementation detail - GAME_EVENTS.md §3 item 3;
                   only the presentation implementation is missing -
                   tasks/TASK_TYPES.md §2)
Status:            DONE
Risk:              MEDIUM (FEATURE baseline for Frontend/Phaser; presentation
                   only, no combat/battle-state/protocol/auth change -
                   tasks/TASK_TYPES.md §4, validation depth per
                   .ai/workflow/core/validation.md §2)
Priority:          HIGH (Phase 1 goal "one playable battle, start to finish" -
                   docs/00-overview/ROADMAP.md §1; a Swap's match / cascade /
                   combo / damage result currently has no player-visible
                   feedback during the battle)
Primary Agent:     client
Supporting Agents: testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, client/phaser-battle-presentation,
                   client/client-event-projection, client/client-state-authority,
                   phaser/tweens
Dependencies:      TASK-006, TASK-007, TASK-007A3, TASK-069, TASK-077,
                   TASK-078, TASK-086, TASK-087
```

---

## Objective

When the runtime forwards a delivered `ReceiveEvents` batch
(`SIGNALR_PROTOCOL.md` §3), `BattleScene` presents the ten non-outcome event
types of the closed wire discriminator (§3.2.2: `MatchCreated`,
`CascadeCreated`, `ComboChanged`, `GemMatched`, `DamageCalculated`,
`DamageDealt`, `DamageTaken`, `PassiveCharged`, `PassiveTriggered`,
`BossSkillCast` — §3.2.6-§3.2.18) as in-battle feedback, in the delivered
order, using only delivered members. Presentation only: the events are not
state (`GAME_EVENTS.md` §3 item 6), the client computes no gameplay value
(`GAME_RULES.md` §18, `AGENTS.md` §10), and no protocol, backend, or
contract surface changes.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 (Technical: server-authoritative battle
  resolution + realtime communication are IN) + §4 (classification
  authority) — same IN classification the completed client battle tasks took
  (TASK-069, TASK-087); §2 for exclusions
- `docs/00-overview/ROADMAP.md` §1 — Phase 1: one playable battle, start to
  finish, including "swap, match, cascade, combo" and server-authoritative
  resolution over SignalR
- `docs/00-overview/GDD.md` §13 — "the client only sends actions and renders
  resulting events"; §17 Design Philosophy (the player should understand why
  a Match is valuable, what their Pet Passive does and when it triggers, how
  Elements affect damage)
- `docs/01-game-design/GAME_RULES.md` §16 (canonical event list), §17 (fixed
  Swap resolution order — the presentation must not reorder what the server
  ordered), §18 (client may send only actions; Damage/HP/Power/Match/Combo/
  Passive Progress are never client-authored)
- `docs/02-technical/GAME_EVENTS.md` §1 (one atomic batch per resolved
  action), §1.1 (the board-cycle ordering contract), §2 (what each event
  means and its payload), §3 item 3 (client-side handling is a client
  implementation detail), §3 item 5 (an event that exists is an event that
  is delivered), §3 item 6 (events are not state; the client renders them)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3 (delivery), §3.1 (batch atomicity,
  written after state write-back, no intermediate states, "applies the batch
  in order"), §3.2.2 (the closed 12-value discriminator), §3.2.3 (camelCase),
  §3.2.5 (omitted, never explicit `null`), §3.2.6-§3.2.18 (each event's exact
  members), §6 item 3 (apply payloads in order) and §6 item 4
  (`BattleStateUpdated` precedes the batch), §8 items 3/7 (no lifecycle status
  message, no board-resolution-specific message)
- `docs/02-technical/GAME_STATE.md` §2.2/§2.3 (what the state push delivers —
  `combo`/`matchCount`, the Passive trio — and the combat-stats note: HP /
  MaxHP / ATK / DEF / Power are **not** delivered; delivering them is "a
  protocol change owned by its own task"), §3 (Transient Resolution State),
  §4 (Client Presentation State is client-owned and non-authoritative)
- `docs/02-technical/TDD.md` §2.1 — scene lifecycle
  (`... → BattleScene → ResultScene`), `BattleScene` = "in-battle visual
  presentation, board animations, and Phaser runtime", Server-Authoritative
  Boundary (scenes are presentation components)
- `docs/02-technical/ARCHITECTURE.md` §2.2 / §2.2.2 (client presentation
  layering; index → screen mapping is the client's own concern);
  `docs/03-decisions/ADR/ADR-003*` (Phaser scene architecture)
- `.ai/skills/client/phaser-battle-presentation` (binding: composition,
  chronological animation sequencing, floating combat text, input lock) and
  `.ai/skills/client/client-event-projection` (event batch → presentation,
  order preservation)
- `AGENTS.md` §9 (anti-overengineering), §10 (server-authoritative), §12
  (domain boundaries), §16 (task discipline), §20 (stop conditions)

---

## Scope

### In Scope

- Interpret the delivered `events[]` payloads in the scene layer for the ten
  non-outcome types, per the member tables of `SIGNALR_PROTOCOL.md`
  §3.2.6-§3.2.18, and present each as in-battle feedback:
  - `MatchCreated` — feedback on the delivered `cells` (with delivered
    `shape` / `gemType` / `cascadeDepth` used for presentation only);
  - `GemMatched` — cleared-cell feedback for the delivered `cellIndex`
    (0..63) and, when the member is present, the consumed `specialGem`
    (§3.2.9, §3.2.10);
  - `CascadeCreated` — cascade-depth feedback from its delivered
    `cascadeDepth` (§3.2.7 — its value is **not** `MatchCreated.cascadeDepth`,
    §3.2.7 item 2);
  - `ComboChanged` — combo feedback from the delivered `combo` (§3.2.8);
  - `DamageCalculated` / `DamageDealt` / `DamageTaken` — floating damage
    feedback from the delivered `finalDamage` / `amount`, attributed by the
    delivered `source` / `target`, with the delivered pipeline members
    available as verbatim breakdown (§3.2.13-§3.2.15);
  - `PassiveCharged` / `PassiveTriggered` — Passive feedback from the
    delivered `passiveId` / `source` / `sourceId` / `progress` / `threshold`
    (§3.2.16, §3.2.17);
  - `BossSkillCast` — Boss Skill feedback from the delivered `skillId` /
    `sourceId` (§3.2.18).
- Present the batch's events in delivered order (`GAME_EVENTS.md` §1.1,
  `SIGNALR_PROTOCOL.md` §3.1 item 5, §6 item 3) — chronological sequencing per
  `GAME_RULES.md` §17 and `client/phaser-battle-presentation`.
- Presentation-local input guard while a feedback sequence plays
  (`client/phaser-battle-presentation`: lock player input during the
  animation sequence): taps do not submit a Swap meanwhile; the guard is
  released on sequence completion, on scene `shutdown()`, on presentation
  error, and must never delay or block the existing outcome handoff.
- Scene-local presentation lifecycle: feedback objects are created per batch
  and fully released in `shutdown()` (follow the existing unsubscribe/cleanup
  pattern, `BattleScene.ts:138-155`).
- Test coverage listed under Testing Requirements, including the boundary
  assertions that no gameplay value is computed on the client.

### Out of Scope

- **HP bars or any HP / MaxHP / ATK / DEF / Power / StatusEffects
  presentation (Pet or Boss).** None of these reaches the client mid-battle
  (`GAME_STATE.md` §2.3 combat-stats note; `SIGNALR_PROTOCOL.md` §4 stage
  list delivers only `combo`/`matchCount` and the Passive trio). Deriving HP
  by accumulating delivered damage would be client-authoritative computation
  (`AGENTS.md` §10) — STOP, report as a candidate follow-up blocked on the
  documented protocol change.
- **Reconstructing intermediate board states** — swap/gem-drop tweens across
  board positions, gravity, spawn, or per-pass board frames: intermediate
  states are Transient Resolution State and are never sent
  (`SIGNALR_PROTOCOL.md` §3.1 item 2), and the client must not compute
  gravity or spawned Gems (§3.1 item 5). Feedback highlights the delivered
  cells of the current state; it does not simulate the resolution.
- **Any event outside the closed discriminator** (§3.2.2): `TurnStarted`,
  `TurnEnded`, `SwapStarted`, `SwapResolved`, `MatchResolved`,
  `PowerChanged`, `RelicTriggered`, `CardCast`, `PetSkillCast`,
  `BattleStarted` are not delivered and must not be fabricated.
- **The `CardCast` / `PetSkillCast` action path or its UI** — unimplemented
  backend and client side; the boundary question is BLOCKED
  (`tasks/blocked/TASK-079...md` — must not be reopened).
- Outcome presentation, reward presentation, `GET /api/battle/{id}/result`,
  and post-result navigation — TASK-087's scope and stop conditions apply
  unchanged.
- `serverSequence` gap detection / desync / resync
  (`SIGNALR_PROTOCOL.md` §6 item 3, §7) — Phase 3
  (`ROADMAP.md` §1), separate task (same boundary as TASK-087).
- Replacing the board's state-driven rendering with event-driven rendering,
  or any change to `GameRuntime` / `SignalRService` port or transport code:
  the runtime stays opaque to event names (`GameRuntimeEvents.ts:21-23`,
  `:67-68`).
- A `BoardView` / `PlayerView` / `BossView` / `BattleHUDView` component
  hierarchy refactor of `BattleScene` — scene-local presentation is the
  smallest correct change (`AGENTS.md` §9); decomposition is optional, not
  required by this task.
- Backend, SignalR protocol, REST contract, ADR, or `docs/` changes of any
  kind; session/auth changes; reconnect behavior.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State

Delivery is complete and unconsumed: the backend emits all ten types during
a Swap's resolution and boss response (`BattleEventBuilder.cs:111-145`,
`BattleStateService.cs:947-1190`) and projects them to the wire
(`BattleEventWireProjection.cs:656-665`); `SignalRService` receives
`ReceiveEvents` generically (`SignalRService.ts:301`) and
`GameRuntime.forwardBattleEvents` (`GameRuntime.ts:607-646`) validates only
the transport envelope and forwards `events` unchanged as `readonly unknown[]`
(`GameRuntimeEvents.ts:67-68`). `BattleScene` subscribes to that port
(`BattleScene.ts:128-129`) but its handler (`BattleScene.ts:585-596`) looks
only for `BattleWon` / `BattleLost` and returns for every other batch, so
match / cascade / combo / damage / passive / Boss-Skill feedback does not
exist anywhere on the client. The board itself re-renders solely from the
state push (`renderBattleState` → `renderBoard`), which already carries the
post-resolution state before the batch arrives
(`SIGNALR_PROTOCOL.md` §6 item 4).

---

## Acceptance Criteria

- [ ] Each of the ten non-outcome delivered types produces visible
      `BattleScene` feedback when its event arrives in a batch; asserted
      per type with test payloads whose delivered values differ from any
      value the client could derive locally (e.g. non-round `finalDamage`,
      `progress` values that do not match a locally recomputed charge).
- [ ] Every presented value equals the delivered member verbatim — no
      arithmetic, clamping, rounding, or re-labeling of `combo`, `amount`,
      `finalDamage`, `progress`, `threshold`, `cellIndex`, `cascadeDepth`,
      or identity strings (`GAME_RULES.md` §18, `AGENTS.md` §10).
- [ ] Feedback plays in the delivered array order of each batch, and the
      test for ordering fails if the presentation order is changed
      (`GAME_EVENTS.md` §1.1, `SIGNALR_PROTOCOL.md` §3.1 item 5).
- [ ] No client-authoritative computation: no HP accumulation, no damage or
      combo recomputation, no match detection, no Passive threshold
      evaluation, no board/RNG interaction; rendered board cell values still
      come only from the state push (asserted by a test).
- [ ] An event whose `type` is not in the closed discriminator
      (§3.2.2) produces no presentation and no error (fabrication guard), and
      a batch containing none of the ten types changes nothing visible.
- [ ] The existing outcome behavior is unchanged: the first
      `BattleWon` / `BattleLost` envelope still transitions once to
      `ResultScene` with the delivered members, and presentation work for
      sibling events neither delays nor blocks that transition
      (TASK-087 tests still pass unchanged).
- [ ] The presentation input guard is always released — sequence completed,
      scene shut down mid-sequence, and presentation error paths each leave
      the scene able to accept the next Swap (three asserted cases).
- [ ] Scene `shutdown()` releases the event subscription and all
      presentation objects/tweens; repeated scene starts leak nothing.
- [ ] Zero backend, docs, ADR, API-contract, and SignalR-protocol changes;
      no new hub method, event, or REST call; the runtime port surface
      (`tests/RuntimeBoundaries.test.ts`) passes unchanged; zero edits to
      `tasks/completed/*` and `tasks/blocked/*` (`AGENTS.md` §16).
- [ ] All relevant tests pass at MEDIUM validation depth
      (`.ai/workflow/core/validation.md` §2: build + unit + integration +
      architecture conformance + `quality/review.md`).
- [ ] Quality review checklist passes (`quality/review.md` §1); no
      authoritative rule or contract violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[ ] src/backend/                                   - NOT touched (delivery already exists)
[x] src/frontend/client/src/game/scenes/BattleScene.ts        - event presentation + input guard
[~] src/frontend/client/src/game/scenes/ (new presenter file) - only if scene-local helpers
                                                          keep BattleScene readable (optional)
[ ] src/frontend/client/src/game/runtime/          - reused as-is; no port/contract change
[ ] src/frontend/client/src/services/              - NOT touched (transport unchanged)
[x] src/frontend/client/tests/ (new/extended)      - per-type presentation, order, boundary tests
[ ] tests/                                         - covered by the client suite above
[ ] docs/ + tasks/ (other than this file)          - NOT touched
```

---

## Implementation Notes

- Entry point: extend `handleBattleEvents` (`BattleScene.ts:585`) — keep its
  outcome branch first (`findOutcomeEvent`, `:606`) so TASK-087 behavior is
  untouched, and route non-outcome events to the presentation path. The
  subscription and cleanup already exist (`:128-129`, `:138-155`).
- The runtime deliberately knows no event names (`GameRuntimeEvents.ts:21-23`)
  — all interpretation belongs in the scene layer, using the member tables of
  `SIGNALR_PROTOCOL.md` §3.2.6-§3.2.18 with `camelCase` names (§3.2.3) and
  omitted-never-`null` optionality (§3.2.5; e.g. `GemMatched.specialGem` is
  present exactly when a Special Gem was consumed).
- The state push precedes the batch (§6 item 4), so the board already shows
  the resolution's final state when feedback plays: feedback is the
  presentation of the events that produced it, not a trigger to re-render
  and never a source for cell content.
- Cell geometry: reuse the existing index → screen mapping and `BOARD_*`
  constants in `BattleScene` for cell-targeted feedback — one coordinate
  convention in both directions (`MATCH3_RULES.md` §1.0,
  `ARCHITECTURE.md` §2.2.2).
- Sequencing: queue feedback per received batch in array order; tween/text
  mechanics per `client/phaser-battle-presentation` and `phaser/tweens`.
  That skill's HP-bar/tween guidance cannot be followed (HP is not delivered)
  — record it as the reported protocol gap in Out of Scope rather than
  inventing HP.
- Test precedent: `tests/ResultScene.test.ts` and `tests/SceneLifecycle.test.ts`
  (mocked runtime port, scene transitions), `tests/GameRuntime.test.ts`
  (envelope forwarding), `tests/RuntimeBoundaries.test.ts` (port-surface
  assertion). Suite commands from TASK-087: `npx vitest run`,
  `npx tsc --noEmit`, `npx vite build`.
- `playerState.combo` / `matchCount` already exist on the synchronized state
  (`GameRuntimeEvents.ts`, `RuntimePlayerState`) but are not rendered today;
  this task's combo feedback is event-driven (`ComboChanged`) — do not add
  new state-driven HUD members unless needed for the feedback itself.

---

## Testing Requirements

### Required Verification
```text
[ ] Unit tests         - BattleScene with a mocked runtime port: one test per
                         delivered type asserting its feedback and verbatim
                         values; batch order assertion (deliver A, B, C ->
                         presented A, B, C); unknown `type` -> no feedback;
                         input guard released on completion / shutdown /
                         error; outcome batch still transitions once
[ ] Integration tests  - mocked transport: established battle -> ReceiveEvents
                         batch of the ten types -> presentation produced in
                         order; port-surface test unchanged
[ ] Gameplay scenarios - N/A, justified: presentation of already
                         server-authoritative events; no gameplay rule,
                         GameplayState, damage math, or RNG touched
                         (GameRuntimeState.ts scope note); MEDIUM depth is met
                         by build + unit + integration + architecture
                         conformance + review
```

### Key Edge Cases
- Final batch: damage / passive events alongside `BattleWon`/`BattleLost` in
  the same `events[]` (`GAME_EVENTS.md` §1) — outcome transition wins and is
  never delayed by feedback.
- `GemMatched` with and without `specialGem` (§3.2.5 omitted member);
  `Burst`/`Area` entries without `orientation` (§3.2.10 item 6).
- `MatchCreated.cascadeDepth` vs `CascadeCreated.cascadeDepth` carrying
  different values for the same pass (§3.2.7 item 2) — never equated.
- Boss-phase batch: `PassiveCharged(source="boss")` → `BossSkillCast` →
  `Damage* (source="boss", target="player")` in one batch
  (`GAME_RULES.md` §17 step 18a-18c).
- Scene shut down mid-sequence (battle abandoned / teardown) — guard
  released, no leaked tweens or listeners, no transition.
- Batch with only `ComboChanged` after a rejected swap produces nothing —
  rejected swaps emit no events at all (`GAME_EVENTS.md` §1.2).

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References:
  STOP per `AGENTS.md` §7
- If task requires client-authoritative game logic calculation (HP totals,
  damage, combo, Passive progress, match detection): STOP per `AGENTS.md` §10
- If the presentation requires HP / Power / ATK / DEF / StatusEffects values
  that the wire does not deliver: STOP per `AGENTS.md` §7/§18 — they are not
  delivered (`GAME_STATE.md` §2.3 combat-stats note assigns delivery to its
  own protocol-change task); report instead of accumulating damage locally
- If the presentation requires an event outside the closed 12-value
  discriminator (`TurnStarted`, `MatchResolved`, `PowerChanged`,
  `RelicTriggered`, `CardCast`, `PetSkillCast`, ...): STOP per
  `AGENTS.md` §7/§18 — `SIGNALR_PROTOCOL.md` §3.2.2 defines the set as
  closed; adding an event is a protocol change with its own task
- If the task pulls in the `CardCast` / `PetSkillCast` action path or its UI:
  STOP — that boundary is BLOCKED (`tasks/blocked/TASK-079...md`, must not be
  reopened); report instead
- If presentation requires reconstructing intermediate board states
  (gravity / spawn / per-pass frames): STOP per `AGENTS.md` §10 and
  `SIGNALR_PROTOCOL.md` §3.1 items 2/5 — intermediate states are never sent
- If presentation requires rewards, `GET /api/battle/{battleId}/result`, or
  navigation after `ResultScene`: STOP per TASK-087's stop conditions
  (RewardSummary member list / no documented client trigger / lifecycle ends
  at `ResultScene`)
- If `serverSequence` gap detection or resync is required: STOP per
  `AGENTS.md` §8 — Phase 3 (`ROADMAP.md` §1)
- If task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: STOP & decompose
- If satisfying any criterion requires editing `tasks/completed/*`,
  `tasks/blocked/*`, or the backend/docs/ADRs: STOP per `AGENTS.md`
  §16/§17/§18 — report instead

---

## Completion Evidence

### Changed Files
- `src/frontend/client/src/game/scenes/BattleScene.ts` — in-battle event presentation, input guard locking during presentation, verbatim feedback display, teardown on shutdown
- `src/frontend/client/src/game/scenes/BattleEventPresenter.ts` — scene-local typed parser and verbatim formatter for 10 non-outcome server events
- `src/frontend/client/tests/BattleEventPresentation.test.ts` — per-event presentation tests for all 10 types, exact ordering assertion, unknown event handling, input guard lifecycle, outcome regression, and authority boundary checks

### Validation Results
- `vitest run`: 18 test files passed (439 passed, 0 failed)
- `tsc --noEmit && vite build`: passed clean (137 modules transformed)
- `RuntimeBoundaries.test.ts`: passed (37 tests)
- `dotnet test src/backend/GameServer.sln`: 253 unit tests passed (zero backend changes made)

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Ten documented event types presented verbatim (`MatchCreated`, `CascadeCreated`, `ComboChanged`, `GemMatched`, `DamageCalculated`, `DamageDealt`, `DamageTaken`, `PassiveCharged`, `PassiveTriggered`, `BossSkillCast`)
- [x] Event ordering strictly preserved
- [x] Unknown events safely ignored with no fabricated presentation
- [x] Input guard locked during presentation and released on complete / shutdown / error
- [x] Outcome handoff to ResultScene preserved without blocking or delay
- [x] Zero backend, docs, ADR, or protocol changes
