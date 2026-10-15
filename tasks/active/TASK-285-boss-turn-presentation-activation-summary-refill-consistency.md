# TASK-285 — Boss-Turn Presentation, Special Activation, Turn Summary & Refill Visual Consistency

---

## Metadata

```text
Task ID:           TASK-285 (next sequential ID: TASK-284A is the highest on disk)
Type:              FIX / PRESENTATION
Status:            IN REVIEW
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     client
Declared Files:    src/frontend/client/src/game/scenes/BattleScene.ts,
                   src/frontend/client/src/game/scenes/BattleEventPresenter.ts,
                   src/frontend/client/tests/SceneLifecycle.test.ts
No commit/push/stage. No existing task status changed.
```

---

## Objective

Make the boss-turn presentation correct, readable and deterministic without
weakening server-authoritative gameplay: a swap must reach its required
presentation state before any step that depends on it, special-cell activations
must be visible and paced, a per-turn activation tally must be shown from
authoritative events, and no cell may keep a stale asset after an
activation/refill.

---

## Authoritative references

- `docs/01-game-design/MATCH3_RULES.md` §1.1 (Gem types), §4.4–§4.5 (Gravity,
  Spawn), §5.5.4 items 1–4 (replacement/reservation), §5.5.5 items 1–8
  (activation trigger, affected set, union report, BFS chains, once per step)
- `docs/01-game-design/GAME_RULES.md` §2 (Turn = atomic Round), §16 (event
  names), §17 (fixed resolution order), §18 (server authority)
- `docs/01-game-design/BOSS_RULES.md` §6.3/§6.4 (boss skills)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.1 (one atomic batch, no per-pass
  board), §3.2.2 (closed 19-name discriminator), §3.2.6 (`MatchCreated`),
  §3.2.9 item 3 (`GemMatched.specialGem` present iff a Special Gem was
  consumed), §3.2.10 (Special Gem representation; no `SpecialGemActivated`
  event), §3.2.26/.27 (`SwapStarted`/`SwapResolved`), §3.2.28
  (`CascadeStepResolved`), §5 (direct acknowledgement, rejection = no events)
- `docs/02-technical/GAME_EVENTS.md` §1, §1.1 item 8, §1.2 (rejected swap emits
  no events), §2, §3 item 3 (client-side event handling is an implementation
  detail)
- `docs/03-decisions/ADR/ADR-026` §2, §4, §8 (shared board, atomic round,
  actor-relative effects, unreported deadlock reshuffle), `ADR-027`
  (authoritative cascade pass reporting), `ADR-008` (snapshot recovery)
- `tasks/completed/TASK-264` (boss swap choreography: a non-Player boundary is
  presented as marked cells, never as a content exchange) — **preserved, not
  reversed**
- `tasks/active/TASK-275` (turn banners, confirmed-gem destruction, single board
  transition) — extended, not weakened

---

## Findings (root causes)

1. **No structural swap→match barrier.** `advancePresentation` completed a phase
   on a *declared* hold only (`phaseHoldMs`), so nothing in the machine enforced
   "a phase cannot complete before the animations it started". The relationship
   held by arithmetic alone (`hold = effect + beat`), which a future timing edit
   could silently break. → **Fixed** by `trackPhaseAnimation` +
   `hold = max(declared, animations actually started)`.
2. **A board cycle the client cannot place was mutated anyway.** The Boss
   Response Phase's board action is delivered as two §1.0 cells
   (`SwapStarted.actor = "boss"`); per TASK-264 it is presented as marked cells,
   because the board the Boss acted on (post-Player-cycle, possibly reshuffled —
   `ADR-026` §8) is not the board on screen. But that cycle's *delivered clears,
   `dropMoves` and `spawnedGems` were still applied to the rendered board*. A
   per-cell content gate (`clearConfirmedCell`) refused the individual clears
   whose rendered content disagreed, which is exactly the reported symptom: the
   cell keeps a stale sprite, the Fall moves views between coordinates the report
   never described, and the correct asset appears only at the next board update.
   → **Fixed** by `PresentationPhase.boardBacked`: a cycle opened by a boundary
   the client cannot place presents its banner, marker, highlights, activation
   overlays, callout and combat stages, but applies **no** clear, Fall or Refill;
   the round's single `settle` converges the committed board.
3. **Special-cell activation had no presentation at all.** A `GemMatched`
   carrying `specialGem` was drawn exactly like an ordinary gem flash, and no
   hold, overlay or phase existed. → **Fixed**: a `special-activation` phase per
   recorded pass, opened before that pass's Match/Cascade phase, carrying one
   ring (Special Gem's own stroke colour) plus a badge label
   (`formatGemCellLabel`) per activated cell, held for the activation's own
   lifetime.
4. **No per-turn activation summary.** → **Fixed**: a terminal `summary` phase
   (`ATK ×2 · HP ×1`), counted from the batch's own delivered activation reports.
5. **Gravity/refill ownership was implicit.** Displaced-view pooling keyed on
   "is this cell another move's origin" and the refill pool was popped blind, so
   an already-incomplete map could orphan or double-map a view. → **Fixed** by
   claimed-view pooling (`poolGemView`) and an ownership-checked pool
   (`takePooledView`, used by both the Refill step and `renderBoard`).

---

## Scope

### In scope
- One centralized, frozen timing policy (`PRESENTATION_TIMING`) with derived
  holds; readability floors for the swap, activation, damage/floater and
  Fall/Refill steps.
- A per-phase animation barrier.
- `boardBacked` on board-cycle phases (no local mutation for cycles the client
  cannot place).
- The `special-activation` phase + overlay + activation hold.
- The `summary` phase + pure counting/formatting helpers.
- Gravity/refill view-ownership hardening.
- 16 new deterministic regression tests + 7 extended expectations.

### Out of scope
- No wire, protocol, state, Redis, database, gameplay-rule, damage/HP/Power/
  Combo/Passive, RNG or backend change.
- No reversal of `TASK-264` (the Boss's pair is still marked, never exchanged).
- No `SpecialGemActivated` event and no protocol change (none is needed — see
  below); no client-side board reconstruction, match detection or gravity
  computation (`AGENTS.md` §10).
- No commit, push, stage, reset, clean or restore; no existing task status
  changed.

---

## Acceptance criteria

- [x] **AC-1** A Match step starts only after the swap step that produced it has
      completed, including its own animations (structural barrier).
- [x] **AC-2** A rejected swap presents no dependent step and rolls the board
      back to the newest authoritative state.
- [x] **AC-3** A duplicate/older batch presents no second phase, clear, Fall,
      Refill, activation or summary.
- [x] **AC-4** A cycle the client cannot place mutates no board content, and the
      round's single `settle` converges the delivered board. **Revised in the
      board-animation iteration (§9):** "cannot place" is now established by
      replaying the batch's own delivered board deltas onto the rendered board and
      asking whether they land on the round's committed board — not by the
      boundary's `actor`. The Player's boundary is still placeable from the
      contract alone, and a Boss boundary is now placeable when it reconciles, so
      the Boss's ordinary cycle applies its clears, Fall and Refill through the same
      shared pipeline the Player's uses.
- [x] **AC-5** Every delivered activation is presented at its own cell with its
      Special Gem type, before that cell is destroyed/replaced, for single,
      multiple, chained and cross-pass activations.
- [x] **AC-6** An activation step is held longer than an ordinary Match step and
      longer than its own overlay; the overlay is abort-safe.
- [x] **AC-7** The tally counts one activation per delivered consumed Special Gem,
      aggregated by cell type, nonzero types only, scoped to one turn, shown
      last; no summary for a turn with none; duplicate events and replayed
      batches never double count.
- [x] **AC-8** Every activated/refilled cell renders the delivered cell's asset
      as part of that replacement; no cleared view is left behind, no cell is
      left without a view and no view object is mapped to two cells.
- [x] **AC-9** `npx tsc --noEmit`, `npm run build`, the full frontend suite,
      `git diff --check` and `git diff --cached --check` pass; pre-existing
      working-tree changes preserved.

---

## Testing record

```text
npx tsc --noEmit (src/frontend/client)                     exit 0
npm run build (tsc + vite, 83 modules)                     PASS
npx vitest run                                             24 files, 1249 tests, all pass
    baseline before this task                              24 files, 1233 tests, all pass
    SceneLifecycle.test.ts                                 368 tests (16 new; 7 extended)
    BattleEventPresentation.test.ts                        106 tests (unchanged, green)
git diff --check                                           exit 0
git diff --cached --check                                  exit 0
Backend suites                                             NOT RUN — no backend change
```

Extended (deliberately, not weakened): the timing literals in the TASK-246
pacing tests (220→300 swap effect, 400→460 damage floater, 660→720 callout,
60→80 beat, 460→540 cast/floater hold, 720→800 boss-skill hold) and one TASK-275
phase-list assertion that now includes the pass's activation step.

---

## Definition and source of the tally

```text
one activation   one delivered `GemMatched` whose `specialGem` is present
                 (SIGNALR_PROTOCOL.md §3.2.9 item 3 + MATCH3_RULES.md §5.5.5
                 item 1: a Special Gem activates when it is consumed)
aggregation      by the activated cell's `gemType` (ATK / DEF / HP / POWER,
                 MATCH3_RULES.md §1.1), in that order, nonzero types only
identity         the delivered pass the report belongs to
                 (`CascadeStepResolved`, §3.2.28 item 5) plus the cell — so a
                 cell that activates in two passes counts twice (§5.8.3 item 6)
                 and a duplicate report inside one pass counts once
                 (§5.8.2 item 3: at most one activation per step)
turn boundary    one accepted batch = one committed Round = one Turn
                 (GAME_RULES.md §2, ADR-026 §2, SIGNALR_PROTOCOL.md §3.1)
not counted      damage targets, animation frames, matched cells, cells an
                 activation cleared, `GemMatched` without a `specialGem`
```

---

## Residual risks, protocol gaps and follow-up tasks

1. **Residual protocol gap — no board identity for a board action.** The
   contract delivers no statement of *which board* an action was validated on,
   and a deadlock reshuffle emits nothing (`ADR-026` §8, `GAME_EVENTS.md` §3
   item 8). The client therefore cannot distinguish "the tracked intermediate
   board is the Boss's starting board" from "the server reshuffled". This task
   keeps TASK-264's conservative presentation (marked cells, no mutation of a
   cycle the client cannot place) and **does not implement a protocol change**.
   Smallest compatible change if the operator wants a placeable Boss cycle
   (follow-up, unapproved): one additive, presentational item marking the
   reshuffle (e.g. `BoardReshuffled { passDepth? }`) or, per `TASK-254` DR-1
   option B, a per-pass transition record; `SIGNALR_PROTOCOL.md` §3.1 item 2 and
   `TASK-245` PD-4 would have to be reconciled first.
2. **Consequence of item 1 — superseded by §9, reported not implemented.** This
   iteration originally left the Boss cycle's `gravity-drop` and `refill-spawn`
   phases playing (delivered order and holds preserved) while they moved nothing,
   with the round converging at the single `settle`. That was the visual cost of an
   over-broad withheld mutation, and it is exactly the reported defect. Since §9
   the Boss's ordinary cycle applies its board work, and a cycle that genuinely
   cannot be placed emits **no** movement step at all — so no phase on screen
   implies a Fall or a Refill that did not play.
3. **Dev-log growth (pre-existing, reported by TASK-275).** `timelineEffects`
   retains references to effects that destroyed themselves on a normally
   completed timeline; this task's overlays are bounded per turn, so the growth
   is unchanged, not worsened. Suggested follow-up: drop the array on
   `releaseTimeline`.
4. **Player-turn banner asymmetry (pre-existing).** The Boss banner is a
   timeline phase; the Player banner is still played outside the timeline by
   `presentPlayerTurnBannerIfNeeded`. Unchanged here (out of scope); a follow-up
   could move both onto the delivered-boundary path.
5. **No `SpecialGemActivated` event is needed.** Activations are uniquely
   identified by `GemMatched.specialGem`, so no client fabrication and no
   protocol change was required for §5/§6.

---

## Verdict

```text
IN REVIEW
```

Implementation and verification are complete for the scoped presentation
changes; the protocol gap above is recorded, not implemented.

---

## 9. Board-animation unification — one pipeline for the Player and Boss turns

### Root cause

`BattleScene.buildPresentationPhases` decided whether a delivered board cycle's
work could be applied to the rendered board from the **boundary's actor alone**
(`cycleBoardBacked = step.actor === 'player'`, `BattleScene.ts`), so every phase of
the Boss Response Phase's cycle carried `boardBacked: false`. That flag is consumed
by exactly four call sites — `playSwapPhase` (which marked the two cells instead of
exchanging them), `playGravityDrop`, `playRefillSpawn` and `presentMatchDestruction`
— so the Boss turn's swap, clears, Fall and Refill were all withheld, the timeline's
`settle` phase re-labelled every differing cell from the committed board in one
step, and the sprites appeared to change only when the snapshot was applied.

The justification was TASK-264's finding that "through the whole round the displayed
board is the committed board the round started from … not the board the Boss's
action is validated against". **That premise no longer held.** TASK-275/TASK-285 made
the Player's own cycle board-backed, so the Player's clears, Fall and Refill *are*
applied locally: at the moment the Boss cycle plays, the rendered board **is** the
post-Player-cycle board, which is exactly the board the Boss's step-18a action was
resolved on (`ADR-026` §2 — the same Match-3 pipeline,
`BattleStateService.cs` step 9). The actor was a proxy for "the client holds this
cycle's starting board", and the proxy had become inaccurate.

### Fix

One board-presentation pipeline for both turns; no duplicated animation code.

1. **Reconciliation pre-pass** (`replayBoardDeltas`, `renderedBoardContents`,
   `deliveredBoardContents`, `sameBoardContents`). Before the timeline is built, the
   batch's own delivered board deltas — the `SwapStarted` pair, each
   `MatchCreated.cells` / `GemMatched` clear, each `createdSpecialGems` claim, each
   `CascadeStepResolved` `dropMoves` and `spawnedGems` — are replayed in delivered
   order onto the board the scene renders, and the result is compared with the
   round's committed board (the state push that `admitsBatch` proved represents this
   resolution). It computes no match, gravity destination, spawned identity, board
   of its own, or RNG draw; it is a verification of the presentation's own replay.
2. **Placeability is per cycle.** `cycleBoardBacked` is `true` for the Player's
   boundary (unchanged — never questioned, so the Player Turn cannot regress) and for
   a Boss boundary only when that reconciliation succeeded. The decision then flows
   through the same `boardBacked` flag the Player's cycle uses, so the Boss's cycle
   runs `playSwapPhase` / `presentMatchDestruction` / `playGravityDrop` /
   `playRefillSpawn` unchanged.
3. **No movement step where no movement plays.** A cycle that cannot be placed folds
   its delivered `CascadeStepResolved` into the phase presenting that pass and opens
   no `gravity-drop` / `refill-spawn` phase, so nothing on screen claims a Fall or a
   Refill that did not happen. `playSwapPhase` reads `phase.boardBacked` instead of
   re-deriving the actor test.

### Why no protocol change is needed

The delivered contract is sufficient for the ordinary Boss cycle: `ADR-027`'s
per-pass report carries the origin and destination of every Gem Gravity moved and
the cell and type of every Gem Spawn placed, `MatchCreated`/`GemMatched` carry the
pass's cleared cells and types, and the round's committed board arrives in the state
push that precedes the batch (`SIGNALR_PROTOCOL.md` §3.1 items 1–3, §4;
`GAME_STATE.md` §5.0). The one operation the client cannot name — the deadlock
reshuffle — is a **deliberately closed** contract decision (`ADR-026` §8,
`GAME_EVENTS.md` §3 item 8: "there is no `BoardReshuffled` event, discriminator, or
payload member, and none is promised by any owning document"), so no event is
proposed. The reconciliation check resolves that case without one: where the deltas
do not replay onto the committed board, the cycle starts from a board the client has
never held and is presented without board work. Adding a per-action board identity
would contradict `SIGNALR_PROTOCOL.md` §3.1 item 2 and `ADR-027` constraints (1)/(6)
and remains an operator decision, not a recommendation.

### Evidence

```text
npx tsc --noEmit (src/frontend/client)             exit 0
npm run build (tsc + vite, 83 modules)             PASS (pre-existing signalr/chunk warnings only)
npx vitest run                                     24 files, 1253 tests, all pass
    baseline before this iteration                 24 files, 1249 tests, all pass
    SceneLifecycle.test.ts                         372 tests (4 new; 1 expectation revised)
git diff --check / --cached --check                exit 0
Backend suites                                     NOT RUN — no backend file touched
Browser E2E (verify:e2e:*)                         NOT RUN — no local stack (docker/Postgres/API/Vite)
```

New tests (in `TASK-285 — one board pipeline for the Player Turn and the Boss Turn`),
driving a complete delivered round — the Player's cycle and the Boss's, each closed by
its own pass report:

- **17.** the Boss's authorised swap exchanges the two views (identity + travel in
  flight, then the cells), its Match clears the confirmed cells, its Fall and Refill
  run, and the board is already the committed board **before** `settle` starts; the
  Boss cycle's phase kinds and holds equal the Player cycle's in the same round. Also
  asserts the refill entrance is in flight, no cleared view survives, 64 distinct
  views, no cell without a view, no view incoherence.
- **18.** a committed board the deltas do **not** produce (the reshuffle case): the
  Boss step marks the delivered cells, moves no view, clears nothing, opens no
  movement step, and the round converges at `settle`.
- **19.** a chained Boss cascade: the second pass opens after the first pass's Refill,
  in order, and converges.
- **20.** a Boss cycle superseded mid-Fall: the travel is cancelled with its timeline,
  the board converges on the newer state, no view is lost or duplicated.

Mutation checks (each reverted immediately; the final tree contains only the intended
change):

```text
cycleBoardBacked = side === 'player'            3 fail (17, 19, 20) — reproduces the defect
exchangedOnHeldBoard = <actor test>             1 fail (17)         — the swap gate is load-bearing
lastCyclePlaceable = true                       6 fail (18 + the pre-existing TASK-264/TASK-275
                                                                    conservative cases)
```

### Residual (reported, not fixed)

- The reconciliation check's failure mode is **safe**: an unrecognised or incomplete
  delivery withholds the Boss cycle's animation and converges at `settle` rather than
  fabricating movement. That also means a genuine report gap would be invisible in the
  UI; making the withholding observable in the developer Move Log is a small
  follow-up, not implemented here.
- The reshuffle case still cannot be *animated* — by construction the client does not
  hold that board — and the client still cannot say why (§Residual risks item 1).

