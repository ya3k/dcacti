# TASK-264 — Boss Swap Choreography After Board Reshuffle

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
-->

---

## Metadata

```text
Task ID:           TASK-264
Type:              FEATURE
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     client
Supporting Agents: testing, review
Workflow:          development/feature.md
Skills:            client/client-event-projection,
                   testing/test-scenario-generation,
                   quality/implementation-review
Dependencies:      TASK-258 (DONE), TASK-259 (DONE), TASK-260 (DONE), TASK-261 (DONE),
                   TASK-262 (DONE), TASK-263 (DONE)
Declared Files:    src/frontend/client/src/game/scenes/BattleScene.ts,
                   src/frontend/client/tests/SceneLifecycle.test.ts
```

---

## Objective

Resolve the shared-board presentation issue TASK-263 reported under G-3: the Boss's delivered
`SwapStarted` coordinates are indices into the board the Boss AI acted on — the post-Player-cycle
board, and in the deadlock case the **reshuffled** board — while the client's displayed board is
the committed round board it holds, and the round's committed board arrives (in the state push)
only at the round's single board transition. The swap step must therefore not present the Boss's
action as an exchange of the two displayed cells' contents: those contents are not the Gems the
Boss's action exchanged. Make the Boss's board action present its delivered coordinates against
the board state the client actually has, keep the round's delivered order intact, and leave the
Player's committed Swap — whose two cells *are* cells of the board the client holds — exactly as
it is.

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md` §2 (atomic round: Player Action Phase → Boss Response Phase → step 19a), §4, §8 (Boss board action, deterministic AI, deadlock reshuffle), **Consequences** (the `SwapStarted` coordinates exist for client presentation)
- `docs/01-game-design/GAME_RULES.md` §17 (fixed resolution order; step 18a is the Boss's board action), §18 (server authority)
- `docs/01-game-design/MATCH3_RULES.md` §1.0 (cell indexing — the coordinates are §1.0 indices), §2.1.1 (the pair is the action's identity, no direction), §2.1.5 item 6 / §2.1.6 (an accepted Swap's lifecycle; a rejection emits nothing), §4.4 (gravity), §7 (RNG — server-seeded; the client draws none)
- `docs/02-technical/GAME_EVENTS.md` §1 (event order within one resolution, including the Boss Response Phase's `SwapStarted` with `actor="boss"`), §2 (`SwapStarted`/`SwapResolved`), §3 item 3 (**client-side handling of each event is a client implementation detail, not a documentation concern**), §3 item 6 (events are not state)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.1 items 1–2 (**the resolution's intermediate states are never sent; there is no per-pass board — exactly one committed board arrives, in the state push**), item 3 (the board travels in the state push), item 5 (the client renders the board from the state it holds and computes nothing), §3.2.26 (the `SwapStarted` member table; item 3 — the resolved board is state, not this event; item 7 — it carries no board), §3.2.27, §4, §8
- `docs/02-technical/ARCHITECTURE.md` §2.2.1 rules 3–5 (the client coordinates and renders; it computes nothing and holds only a synchronized presentation copy)
- `docs/00-overview/MVP_SCOPE.md` §1 (no new system; a presentation correction)
- `tasks/completed/TASK-262-shared-board-battle-presentation-and-event-handling.md` (the one-board round presentation, and its G-3 report), `tasks/completed/TASK-263-shared-board-actor-aware-swap-event-contract.md` (the delivered boundary, and its G-3 report this task resolves), `tasks/completed/TASK-245-phaser-cascade-animation-and-combat-presentation-sequencing.md`, `tasks/completed/TASK-254-unified-battle-action-presentation-pipeline.md` (the phase model and the "skip a stage, never fabricate it" rule)
- `tasks/TASK_LIFECYCLE.md` §6 — the two-stage commit protocol

---

## Preflight Findings

Verified by direct inspection at HEAD `cdddc7b` (working tree: the `.ai/` harness-compatibility
documentation sync, `.agents/`, `.claude/`, `.ai/PROJECT_STATE.md`,
`.ai/skills/discovery/task-readiness-assessment.md`, `.ai/workflow/core/task-selection.md` and
`.gitignore` are modified/untracked and are **not** this task's; they are disclosed as
unowned/pre-existing).

1. **Task ID, authorization and lifecycle.** The highest task ID on disk is `TASK-263`
   (`tasks/completed/`), so the next sequential ID is `TASK-264` (`tasks/README.md` §3).
   `TASK-264` is simultaneously the ID `TASK-258` §9 **pre-allocated** to exactly this scope —
   "TASK-264 — Phaser Battle Presentation for Shared Board and Boss Swaps … Animate Boss swap
   movement and cascades in `BattleScene.ts`, locking input across round playback" — and the ID
   `TASK-262` §Out of Scope and `TASK-263` §Out of Scope both defer to ("the Boss's own
   presentation design remains `TASK-264`"). The ID is **verified, not assumed**, and no new ADR
   is required (`AGENTS.md` §18): no contract is changed. `tasks/backlog/` and `tasks/blocked/`
   contain only `.gitkeep`; no other task holds an active manifest, so no shared-file owner
   exists and no joint group commit is required (`TASK_LIFECYCLE.md` §6.1 item 5).
2. **The defect is real and is in the client, not in the event production.** The Boss's pair is
   emitted from the AI's own selected cells (`BattleStateService.cs:1650-1654`:
   `bossMove.SelectedMove.From/To`), applied to the very board the AI selected it on
   (`:1632-1635`), and the wire projection carries exactly those two cells
   (`BattleEventWireProjection`, `SIGNALR_PROTOCOL.md` §3.2.26). The server is correct and is not
   changed. `BossAiEngine.SelectMove` (`BossAiEngine.cs:187-250`) replaces the board with
   `BoardGenerator.Generate(rng)`'s board when the post-Player-cycle board has zero legal swaps
   and the AI then selects on **that** board (`:203-217`), so in the deadlock case the delivered
   pair is an exchange on a board the client has never held.
3. **Which board the client holds.** `BattleScene.renderBoard` writes gem views only for a
   delivered board (`:2742-2813`); the only delivered board of a round is the state push that
   `BattleHub` sends **before** the batch (`BattleHub.cs:1120-1121`), and it is held by
   `renderBattleState` (`heldSequence`, `:1994-1995`) until the timeline's single `settle` phase
   converges it (`:4033-4037`, placed after the round's last board-cycle phase, `:3775-3788`).
   A board cycle's Match/Gem/Cascade phases flash delivered cell indices and move no content
   (`:4499-4548`). So through the whole round the displayed board is the committed board the
   round started from — which is exactly the board the **Player's** action is validated against
   (`ADR-026` §2: the Player phase runs first) and is **not** the board the Boss's action is
   validated against (post-Player-cycle, possibly reshuffled). `SIGNALR_PROTOCOL.md` §3.1 item 2
   forbids the per-pass board that would carry it.
4. **What the code does today.** `playSwapPhase` (`:4174-4228`) exchanges `gemViews[from]` and
   `gemViews[to]` for **every** swap step, whichever actor opened it. For the Player's step that
   is the exchange the server committed on the board on screen; for the Boss's step it presents
   the displayed cells' contents changing places — an exchange no authoritative state contains,
   and in the reshuffle case a movement of Gems that had nothing to do with the Boss's action.
5. **The contract cannot distinguish the two cases, and does not need to.** No delivered member
   names the board an action was validated on, and none can: the board is state
   (`SIGNALR_PROTOCOL.md` §3.1 item 3, §3.2.26 item 7), recovery is a snapshot and never an event
   replay (ADR-008), and a client-side reconstruction would be a second board simulation
   (`AGENTS.md` §10, §11). The honest, contract-sufficient rule is therefore uniform: present a
   **content exchange** only for an action the client knows was validated against the board it
   displays (its own committed/reported Player action), and present any other delivered pair as
   the two §1.0 **cells** it names — marked, with no content movement — because cell indices are
   board-independent (`MATCH3_RULES.md` §1.0) while the contents at those cells are not.
6. **No `BoardReshuffled` discriminator is added and no protocol member changes.**
   `ADR-026` §8's "emitting `BoardReshuffled`" is unimplemented and undefined in the event
   list's owner (`GAME_EVENTS.md` §2, `GAME_RULES.md` §16, TASK-262/TASK-263 G-3); this task
   neither adds it nor depends on it. The missing contract information is reported in §Remaining
   Contract Limitation rather than invented.
7. **Compatibility surface (measured).** `SceneLifecycle.test.ts`'s existing TASK-262/TASK-263
   blocks assert the *phase list*, the *step attribution* and the *one board transition* — none
   of them asserts a Boss content exchange, and the pacing invariant
   (`:5306-5416`, `phaseEffectMs('swap') = SWAP_TWEEN_MS`, `:6553-6633`) constrains the new
   marker's duration to below the swap phase's hold. `BattleEventPresenter.ts` needs no change:
   the delivered actor/action/kind vocabulary is unchanged, and "which board the client holds" is
   scene state (the scene already owns `committedSwapCells`), not event interpretation.

---

## Scope

### In Scope

- `BattleScene.playSwapPhase`: the swap step presents a **content exchange** only when the step's
  coordinates name an exchange on the board the client holds — the Player's delivered boundary
  (`actor="player"`) and the pre-existing submitted-pair fallback; every other delivered boundary
  (the Boss's, or an actor word outside the contract's vocabulary) is presented as a
  **coordinate marker** on the two delivered §1.0 cells, with no cell's contents moved.
- The marker: two cell marks drawn with the scene's existing transient-highlight primitive,
  registered as timeline-owned effects so an abort, a supersession and a teardown cancel them,
  and with a lifetime shorter than the swap phase's existing hold (TASK-246 §5's pacing
  relationship).
- Regression tests in `SceneLifecycle.test.ts` for: the normal Boss swap, the reshuffle case
  where the reshuffled board's contents differ from the displayed ones, the Player's unchanged
  exchange, the delivered order (Boss step → Boss board cycle → the one board transition), a
  superseded/interrupted presentation, a duplicate or older batch, and a terminal round.

### Out of Scope

- Any wire, protocol, state, persistence, gameplay-rule, Boss AI, damage/HP/Power, or balance
  change; adding a `BoardReshuffled` discriminator or any other member
  (`SIGNALR_PROTOCOL.md` §3.2.2 is closed and unaffected).
- Any client-side board simulation, prediction, or reverse-engineering of the Boss's move or of
  the reshuffled board (`AGENTS.md` §10/§11, ADR-001/ADR-009), and any modification of the
  delivered `SwapStarted` coordinates.
- The Player's swap flow, the actor-aware attribution TASK-263 introduced, the one-board round
  presentation and the single board transition TASK-262 introduced, event deduplication,
  sequence handling, terminal latching, and ADR-008 snapshot recovery — all preserved unchanged.
- G-2 (Player/Boss Combo representation) and G-3's contract question, which is reported here, not
  redesigned.
- Broad animation/UI refactoring, and any change outside the declared file set.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-264:

```text
src/frontend/client/src/game/scenes/BattleScene.ts
src/frontend/client/tests/SceneLifecycle.test.ts
```

*(The task record itself is excluded from the implementation declared file set, and the list
strictly matches the `Declared Files:` field in `## Metadata`. No documentation file is declared:
`GAME_EVENTS.md` §3 item 3 makes client-side event handling a client implementation detail rather
than a documentation concern, and the change is consistent with every contract statement it
touches — `SIGNALR_PROTOCOL.md` §3.1 items 1–2/3/5 and §3.2.26 item 3/7, `ADR-026` §2/§4/§8 —
none of which it alters.)*

**Shared-file ownership.** `BattleScene.ts` and `SceneLifecycle.test.ts` were last owned by
`TASK-263` (DONE, immutable). No task holds an active manifest, so no concurrent shared-file
owner exists and no joint group commit is required (`TASK_LIFECYCLE.md` §6.1 item 5).

**Unowned / pre-existing disclosure (P-3).** `git status --porcelain` at pickup reports
**modified**: `.ai/HARNESS_COMPATIBILITY.md`, `.ai/HARNESS_MAPPING.md`,
`.ai/agents/AGENT_SELECTION.md`, `.ai/agents/orchestrator.md`, `.ai/skills/README.md`,
`.ai/skills/SKILL_REGISTRY.md`, `.ai/workflow/README.md`, `.gitignore`; **untracked**:
`.agents/`, `.ai/PROJECT_STATE.md`, `.ai/skills/discovery/task-readiness-assessment.md`,
`.ai/workflow/core/task-selection.md`, `.claude/`. None is this task's; none is staged, and all
are disclosed in the Phase A commit body.

---

## Acceptance Criteria

- [x] **The Boss's board action is never presented as an exchange of stale contents.** For a
      delivered boundary that is not the Player's committed action, the swap step moves no cell's
      contents: the displayed board before and after that step is identical, and no tween of that
      step targets a Gem view.
- [x] **The delivered coordinates are presented against the board state the client has.** The
      step names the two delivered §1.0 cells, in the delivered order, and marks exactly those two
      cells — a coordinate gesture that is true whether or not the Boss AI reshuffled before
      selecting; no board content is derived, reconstructed, or simulated.
- [x] **The Player's committed Swap is unchanged.** A step opened by `SwapStarted(actor="player")`
      — and the pre-existing fallback step built from the submitted pair — still exchanges the
      two views it names, because those are cells of the board the Player's action was validated
      against and the client holds.
- [x] **The delivered order is unchanged.** `SwapStarted`/`SwapResolved`, the Boss's board cycle,
      the Player's combat stages and the round's single committed board transition keep the order
      `GAME_EVENTS.md` §1 and `SIGNALR_PROTOCOL.md` §3.1 fix: no event is reordered, filtered,
      duplicated, or fabricated.
- [x] **Interruption, deduplication and terminal handling are preserved.** The marker is a
      timeline-owned effect cancelled by a supersession, an abort and a teardown; a duplicate or
      older batch presents nothing; a terminal round still hands its outcome off last; the input
      guard is released on every path (no lock, no stale visual).
- [x] **The pacing relationship holds.** The marker's lifetime (260 ms) is shorter than the swap
      phase's own hold (`SWAP_TWEEN_MS` 220 + `PHASE_SETTLE_MS` 60 = 280 ms), so the phase that
      starts it still outlasts it (TASK-246 §5); the new test asserts it against the scene's real
      tween configuration rather than against a copied constant.
- [x] **No contract, gameplay, or wire change.** No new event, member, hub method, state field,
      Redis key, or database column; no RNG draw and no gameplay value computed on the client
      (`AGENTS.md` §10/§11, ADR-001). Only the two declared frontend files changed.
- [x] **Frontend tests and the type/build checks pass**, and the backend is unaffected (no
      backend file is touched; the solution still builds with 0 errors).
- [x] Zero unrelated working-tree change: the Phase A staged set equals the declared file set
      exactly, and every unowned/pre-existing path is left unstaged and disclosed.

---

## Current State

The shared-board round is already presented as one ordered timeline per delivered batch on one
board (`TASK-262`), and each committed board action already opens its own step from its delivered
`SwapStarted`, attributed to the actor the wire names (`TASK-263`). Both board cycles of the round
are presented on the displayed board, whose contents are written exactly once per round — at the
timeline's single `settle` phase, from the delivered committed board.

What this task changes is one presentation rule inside that existing step, and nothing else: the
step's coordinates decide whether its cells' contents may be exchanged. They may for the Player's
own committed action; they may not for the Boss's, whose coordinates index a board the client
does not hold and, after a deadlock reshuffle, a board whose contents it has never seen.

---

## Implementation Notes

- **One rule, one place.** The rule lives in `BattleScene.playSwapPhase` (the step's own
  presentation) and reads only delivered members: the boundary's `actor` (through the existing
  `deliveredSwapOf`) and the pre-existing submitted-pair fallback. `BattleEventPresenter.ts` is
  untouched — the actor/action/kind vocabulary does not change.
- **The marker reuses the scene's existing transient-highlight primitive**
  (`spawnCellHighlight`, the same one the Match/Gem feedback uses), which already registers the
  effect in `timelineEffects` so a supersession, an abort and a teardown cancel it. Its lifetime
  is a new constant, deliberately below the swap phase's hold.
- **Conservative by construction.** An actor word outside the contract's two values names no side
  (`SIGNALR_PROTOCOL.md` §3.2.26 item 2), so it takes the marker path: the client never claims an
  exchange it cannot know was committed on the board it displays.
- **No new abstraction.** No per-actor registry, branch table, second pipeline, second board
  model, or client-side match/adjacency check is introduced (`AGENTS.md` §9).

---

## Testing Requirements

### Required Verification

```text
[ ] Frontend tests     — the Boss's step marks the delivered pair and moves no content; the
                         Player's step still exchanges its own pair; the reshuffle case (a
                         delivered committed board whose contents at the pair differ from the
                         displayed board's) moves nothing; the delivered phase order and the one
                         board transition are unchanged; a superseded timeline cancels the marker
                         and releases the guard; a duplicate/older batch presents and marks
                         nothing; a terminal round hands its outcome off last; the marker's
                         lifetime is below the swap phase's hold
[ ] Type / build       — `npm run build` (tsc && vite build) in src/frontend/client
[ ] Regression         — `npx vitest run` (the whole frontend suite)
[ ] Gameplay scenario  — Given a committed shared-board round in which the Player's Swap and the
                         Boss's step 18a board action both resolve, And the Boss's action was
                         selected on a board the client does not hold (deadlock reshuffle),
                         When the client presents the round, Then the Player's exchange is shown
                         on the board the Player acted on, And the Boss's step presents only the
                         two cells it named, And the Boss's board cycle follows it, And the
                         round's committed board is applied once at the end of the board cycle
```

### Key Edge Cases

- Both actors' steps in one round: the Player's exchanges, the Boss's marks (the normal case).
- The deadlock case: the pair's cells on the displayed board hold contents the action never
  exchanged — nothing on screen moves for that step.
- A boundary whose `actor` word is outside the contract's two values: no side claimed, no
  exchange presented, the delivered cells still marked.
- A batch that delivers no boundary at all (a pre-extension fixture): the submitted-pair fallback
  still presents the Player's own exchange, unchanged.
- A malformed boundary event: still parsed out, still no step from it (TASK-263).
- Supersession / abort / teardown while the Boss's step plays: the marker is cancelled with the
  timeline and the guard is released.
- A duplicate or older `serverSequence`: nothing presented, nothing marked.
- A terminal round: the Boss's step and board cycle precede the outcome, which is last.

---

## Stop Conditions

- If the fix requires a new wire member, a discriminator, a hub method, a state field, or a
  protocol change: **STOP** and report the smallest concrete contract decision needed
  (`SIGNALR_PROTOCOL.md` §3.2.2 is closed; this task adds none).
- If the fix would require the client to compute a Match, a gravity/spawn result, a board
  difference, or a board reconstruction: **STOP** — that is a second board simulation
  (`AGENTS.md` §10/§11, ADR-001/ADR-009).
- If the delivered order, the actor attribution, the single board transition, event
  deduplication, or ADR-008 recovery would have to change: report per `AGENTS.md` §4 rather than
  picking one.
- If a required change touches a backend file, a gameplay rule, the Boss AI, or a persisted
  contract: STOP and escalate.

---

## Remaining Contract Limitation (reported, not resolved)

```text
The delivered contract cannot express the board a board action was validated on, and therefore
cannot tell the client whether the Boss AI reshuffled before selecting. `SIGNALR_PROTOCOL.md`
§3.1 item 2 forbids per-step delivery and §3.2.26 item 7 gives `SwapStarted` no board;
`ADR-026` §8 names a `BoardReshuffled` event that `GAME_RULES.md` §16 and `GAME_EVENTS.md` §2
(the event list's owner) do not define and that the implementation does not emit (TASK-262 G-3,
TASK-263 G-3).

Consequence, after this task: the Boss's step is presented honestly (its two cells are marked,
and nothing on screen claims an exchange that no authoritative state contains), but the player
cannot see *which* board the Boss acted on, and the Boss's content movement is not animated.

Smallest proposed contract change (NOT made here — it needs an explicit decision, and this task
does not authorize one): either
  (a) define the deadlock reshuffle as an event in the owning documents (`GAME_RULES.md` §16 →
      `GAME_EVENTS.md` §2 → `SIGNALR_PROTOCOL.md` §3.2.x) so a client may state that the board it
      holds is no longer the board in play, or
  (b) make `ADR-026` §8's "emitting `BoardReshuffled`" accurate by dropping it, recording that the
      reshuffle is server-internal.
(a) alone still would not deliver a board, so the swap step's honest presentation would not
change; it would only let the client label the transition. Delivering a per-action board would be
a much larger change and contradicts `SIGNALR_PROTOCOL.md` §3.1 item 2.
```

---

## Completion Evidence

### Commit

- Implementation slice (Phase A): `791d7bca3a219d1e2324dfb35c07f40da52a238a`
- Subject: `feat(client): present the Boss board action on the board the client holds` (no task
  ID in the subject, `TASK_LIFECYCLE.md` §6.2 P-4; the task ID is in the body under `owns:`)
- Owns: `TASK-264`
- Files: the declared file set, exactly (2 paths)
- Shared with: `none`
- Unowned / pre-existing (left unstaged, disclosed in the commit body):
  `.ai/HARNESS_COMPATIBILITY.md`, `.ai/HARNESS_MAPPING.md`,
  `.ai/agents/AGENT_SELECTION.md`, `.ai/agents/orchestrator.md`,
  `.ai/skills/README.md`, `.ai/skills/SKILL_REGISTRY.md`, `.ai/workflow/README.md`,
  `.gitignore`, `.agents/`, `.ai/PROJECT_STATE.md`,
  `.ai/skills/discovery/task-readiness-assessment.md`,
  `.ai/workflow/core/task-selection.md`, `.claude/`
- Completion record filing (Phase B): this completion record alone, filed under its own
  bookkeeping commit (a direct addition: the manifest was never staged in Phase A,
  `TASK_LIFECYCLE.md` §6.1 Phase A step 1).

### Changed Files

- `src/frontend/client/src/game/scenes/BattleScene.ts`
  - `playSwapPhase` now decides, from the delivered boundary alone, whether the step's two cells
    are cells of the board this scene is showing: a boundary that names the Player (`actor ===
    "player"`) and the submitted-pair fallback (no delivered boundary) exchange the two views,
    exactly as before; every other delivered boundary marks the two delivered cells instead and
    moves no content. The doc block records why (ADR-026 §2's phase order, `SIGNALR_PROTOCOL.md`
    §3.1 item 2's "no per-pass board", §3.2.26 items 3/7/2).
  - `markSwapCells(from, to)` — the new private step presentation: two `spawnCellHighlight`
    marks on the delivered §1.0 cells, registered in `timelineEffects` so a supersession, an
    abort and a teardown cancel them with the timeline; no content write and no board update.
  - `SWAP_MARK_MS` / `SWAP_MARK_COLOR` / `SWAP_MARK_FILL_ALPHA` — the marker's lifetime
    (260 ms) and colour, with the pacing relationship to the swap phase's hold documented.
- `src/frontend/client/tests/SceneLifecycle.test.ts` — a new `TASK-264 — Boss swap choreography
  on the board the client holds` block (8 tests) inside the existing TASK-252 describe, using its
  existing harness, fixtures and helpers: the delivered order of both board actions with the one
  board transition; the Player's exchange followed by the Boss's mark-only step (with the mark
  read from the scene's real tween configuration and its lifetime asserted below the phase hold);
  the reshuffle case, where the contents at the pair's cells on the delivered board differ from
  the displayed ones; the Player's exchange from both a delivered boundary and the
  submitted-pair fallback; an actor word outside the contract's two values; supersession
  mid-mark (marks killed and released, board converged, guard released); a duplicate and an
  older batch (nothing presented, nothing marked); and a terminal round (mark-only Boss step,
  outcome last, `ResultScene` handoff).

### Validation Results

- `npx vitest run tests/SceneLifecycle.test.ts` (narrowest relevant suite) — **PASS**: 289
  passed, 0 failed (baseline 281; +8 new).
- `npx tsc --noEmit` — **PASS** (no diagnostics, before and after the new tests).
- `npx vitest run` (whole frontend suite) — **PASS**: 24 files, **1102 passed, 0 failed**
  (baseline 1094; +8 new).
- `npm run build` (cwd `src/frontend/client`: `tsc && vite build`) — **PASS**; only the
  pre-existing `@microsoft/signalr` Rollup `/*#__PURE__*/` comment warnings and the
  chunk-size warning.
- **Mutation check (the new tests are not vacuous).** With the rule temporarily inverted (every
  delivered boundary taking the exchange path), **5 of the 8** new tests fail — the two
  mark-only round cases, the reshuffle case, the unrecognised-actor case, the supersession case
  and the terminal case — and the 3 order/fallback/dedup tests (which assert unchanged
  behaviour) pass. The mutation was reverted immediately; the final tree contains only the
  intended change.
- `dotnet build src/backend/GameServer.sln` — **PASS**: 0 errors, 40 pre-existing warnings. No
  backend file was touched, so no backend suite was run (see "Not run" below).
- **Not run (environment).** The browser E2E suites (`verify:e2e:smoke`,
  `verify:e2e:collection`, `verify:e2e:history`), `verify:runtime` and the viewport checks: the
  documented local stack cannot run here — `docker` is not installed, nothing listens on 5432
  (PostgreSQL), 5000/5001 (API) or 5173 (Vite dev server), and the harnesses drive a real
  AuthScreen → API → SignalR journey (`scripts/standalone-web-smoke.mjs` usage). Recorded as
  **NOT RUN**, never as passing.
- **Not run (out of this change's scope).** The backend test suites: the change is frontend-only
  and no backend file is in the declared set. `TASK-263` verified the Application project's 10
  pre-existing `Balance.Tests` failures (archived TASK-194/TASK-197 evidence invalidated by the
  ADR-026 mechanics change) at a pristine HEAD; they are not re-run or touched here
  (`TASK-264`'s brief: do not repair unrelated balance-evidence tests).
- **Working-tree hygiene.** `git status --porcelain` after the run reports only the two declared
  files modified plus the unowned/pre-existing paths; the build's `dist/` output is gitignored,
  and `tasks/artifacts/*` was never regenerated (no balance suite was run).

### Server Authority & Scope Verification

- [x] No client-authoritative gameplay logic was added: no match detection, adjacency check,
      clearing, gravity, spawn, RNG draw, damage, HP, Power, Combo, `MatchCount`, or Passive
      derivation, and no swap or board state is reverse-engineered from a board difference
      (`AGENTS.md` §10, ADR-001).
- [x] No gameplay rule, damage value, Boss AI weight, balance number, or persisted contract
      changed; no `BattleState` field, Redis key, database column, hub method, subscription or
      state-push member was added.
- [x] The delivered wire contract is untouched: `SwapStarted`'s members, its emission points and
      its projection are unchanged, and the client reads the delivered `actor` exactly as
      `SIGNALR_PROTOCOL.md` §3.2.26 defines it.
- [x] `Turn` and `Sequence` are read and compared only, never written client-side; the round's
      displayed board is written only from the delivered state, at its one transition.
- [x] Adherence to `MVP_SCOPE.md` §1 (a presentation correction inside an existing system; no
      new system or content).
- [x] Documentation impact checked: `GAME_EVENTS.md` §3 item 3 makes client-side event handling
      a client implementation detail rather than a documentation concern, and the change is
      consistent with every contract statement it touches (`SIGNALR_PROTOCOL.md` §3.1 items
      1–3/5, §3.2.26 item 2/3/7, `ADR-026` §2/§4/§8) — none of which it alters. No document was
      changed and no ADR was created (`AGENTS.md` §17/§18).
- [x] P-1…P-6 commit policy: single named task, exact declared set, no task ID in the subject,
      no batching, no amend/rebase, unowned paths disclosed.

### Review Pass (`quality/review.md` §1)

```text
Correctness     matches SIGNALR_PROTOCOL.md §3.1 items 1–3/5 and §3.2.26 items 2/3/7, ADR-026
                §2/§4/§8, MATCH3_RULES.md §1.0, GAME_EVENTS.md §1/§1.2/§3 item 3; the delivered
                event order and attribution are preserved exactly
Architecture    one presentation rule inside the existing swap step; the actor/action vocabulary,
                the one adapter, the phase model, the one board transition, the runtime port and
                the transport boundary are all unchanged
Scope           exactly the declared 2 files; nothing unrelated refactored or "improved"; no
                protocol, gameplay, AI, persistence or balance change
Tests           8 new frontend tests at MEDIUM depth (the task's risk); the full frontend suite
                and the build are green; 5 of the 8 fail if the rule is inverted
Documentation   no update required (GAME_EVENTS.md §3 item 3); no conflict introduced
Security        untouched (no auth, session, transport or data-exposure surface)
Performance     one extra 260 ms highlight tween per non-Player board action; no extra event
                handling, lookup or allocation on the existing single pass
Maintainability one boolean and one 15-line mark helper; no new abstraction, interface, factory,
                registry or second pipeline (AGENTS.md §9)
Determinism     no RNG, no timestamp, no client-held turn, no order-based or timing-derived
                decision; the branch reads one delivered member and the client's own submitted
                pair, and nothing is computed for either side
```

### Remaining Issues

1. **The contract still cannot express the Boss's per-action board** (the limitation §Remaining
   Contract Limitation records, with the smallest proposed change). After this task the Boss's
   step is honest but silent about *which* board it happened on; a decision on
   `BoardReshuffled` (define it, or correct `ADR-026` §8) remains a Product Owner documentation
   decision and is not made here (`AGENTS.md` §7/§20).
2. **No browser E2E verification was possible** (see "Not run"): the fix is verified by unit
   tests and the runtime-free build only. A human run of `verify:e2e:smoke` against a live stack
   would be the end-to-end confirmation.
3. **G-2 is untouched and unchanged** (Player/Boss `Combo`/`MatchCount` representation), as
   TASK-263 reported it.
