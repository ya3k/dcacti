# TASK-275 — Authoritative Turn Lifecycle & Match-3 Animation Pipeline

---

## Metadata

```text
Task ID:           TASK-275
Type:              FEATURE
Status:            IN REVIEW — CONDITIONAL PASS
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     client
Supporting Agents: realtime, testing, review
Workflow:          development/feature.md
Skills:            client/phaser-battle-presentation, client/client-state-authority,
                   realtime/realtime-protocol-validation, testing/test-scenario-generation,
                   quality/implementation-review
Dependencies:      TASK-272 (IN REVIEW), TASK-273, TASK-274 (both preserved)
Declared Files:    src/frontend/client/src/game/scenes/BattleScene.ts,
                   src/frontend/client/src/game/scenes/BattleEventPresenter.ts,
                   src/frontend/client/tests/SceneLifecycle.test.ts,
                   src/frontend/client/tests/BattleEventPresentation.test.ts
```

---

## Objective

Present each accepted authoritative Round as a readable, sequential gameplay
experience instead of showing the resolution's final board immediately: per-pass
match presentation, confirmed-gem destruction, turn banners at delivered
board-action boundaries, and convergence onto the accepted authoritative board —
without ever simulating Match-3 resolution on the client.

---

## First Gate — Authoritative Event Contract Validation

### Delivered contract

```text
ReceiveEvents(battleId, serverSequence, events[])      SIGNALR_PROTOCOL.md §3
```

* one batch per resolved action, atomic at the message level (§3.1); the state
  write-back precedes the batch (§3.1 item 1, `GAME_STATE.md` §5.1 item 3);
* `serverSequence` is `BattleState.Sequence` after the resolution — strictly
  increasing, no gaps (§3 item 2);
* event order inside the batch is `GAME_RULES.md` §17's order as enumerated by
  `GAME_EVENTS.md` §1.1; the transport projection sorts and filters nothing
  (§3.2.1 item 3);
* the delivered board arrives **only** in the `BattleStateUpdated` push: 64 cells,
  never partial (§4.1 item 3).

### Event → presentation phase mapping

| Delivered event (§3.2.x) | Payload members read | Class | Phase | Completion | Fallback |
| --- | --- | --- | --- | --- | --- |
| `SwapStarted` §3.2.26 | `actor`, `fromCell`, `toCell` | accepted-action report | `banner` (new) then `swap` | phase hold (banner `TURN_BANNER_TOTAL_MS`; swap `SWAP_TWEEN_MS`) | `actor` outside the contract's two values → **no banner** and marked cells only; a `boss` boundary is marked, never exchanged (the client cannot know that board) |
| `SwapResolved` §3.2.27 | `outcome` | committed-exchange report | none (recorded) | — | — |
| `MatchCreated` §3.2.6 | `shape`, `cells[]`, `gemType`, `cascadeDepth`, `createdSpecialGems[]` | detection report | `match` | hold = highlight lifetime | a cell whose **rendered content** is not the delivered `gemType` is **not** cleared |
| `GemMatched` §3.2.9 | `cellIndex`, `gemType`, `specialGem?` | cleared-cell report (pre-removal) | extends `match` / `cascade` | host hold | same delivered-content gate |
| `CascadeCreated` §3.2.7 | `cascadeDepth` | pass boundary | one `cascade` phase per depth | 60 ms when it carries no Match/Gem feedback | never opens gameplay data of its own |
| `ComboChanged` §3.2.8 | `combo` | accounting report | joins open phase (`feedback`) | host hold | — |
| `DamageCalculated` §3.2.13 | six pipeline factors | pipeline report | none (developer only) | — | — |
| `DamageDealt` / `DamageTaken` §3.2.14/15 | `source`, `target`, `amount` | damage instance | `damage` / `retaliation` / `feedback` | hold ≥ floater + gauge | unrecognised party → no floater, no actor |
| `PassiveCharged` / `PassiveTriggered` §3.2.16/17 | `passiveId`, `source`, `sourceId`, `progress`, `threshold` | Passive report | `feedback` | host hold | non-Pet `source` → no Pet gauge travel |
| `BossSkillCast` §3.2.18 | `skillId`, `sourceId` | Boss step | `boss-skill` | hold = callout hold + fade | unknown `skillId` → the formatter's existing fallback |
| `CardCast` / `PetSkillCast` §3.2.20/21 | `cardId` | cast report | `cast` | 60 ms | — |
| `RelicTriggered` §3.2.23 | `relicId` | Relic step | `relic` | 60 ms | unloaded id shown verbatim |
| `PowerChanged` §3.2.24 | `delta`, `power`, `source` | Power stage | `feedback` | host hold | `source` is stage ownership, **not** a side (§3.2.24 item 3) |
| `BattleWon` / `BattleLost` §3.2.19 | `outcome`, `finalBossHp`, `finalPlayerHp` | **terminal result** | `outcome` (last) | zero hold | handed off only when delivered |

There is **no visual-only event** in the closed set (`GAME_EVENTS.md` §3 item 5),
and no turn/phase/status member exists on the wire (§3.2.2 item 2).

### Data-contract finding (decisive)

```text
per-pass board             NOT DELIVERED   SIGNALR_PROTOCOL.md §3.1 item 2
gravity moves              NOT DELIVERED   MATCH3_RULES.md §4.4 item 7 — gravity emits no event
refill / spawn contents    NOT DELIVERED   §3.1 items 2 and 5; GAME_EVENTS.md §1.1
intermediate board state   NOT DELIVERED   GAME_STATE.md §3, §5.1 item 2, §5.3 item 4
board per event            NEVER           SIGNALR_PROTOCOL.md §3.2.12 item 1
```

Measured against `TASK-275` §3's required transition representation:

| Required fact | Available? |
| --- | --- |
| which gems are cleared, and their coordinates | **yes** — `MatchCreated.cells`, `GemMatched.cellIndex` |
| the ordering of resolution passes | **yes** — `CascadeCreated.cascadeDepth`, `MatchCreated.cascadeDepth` |
| which existing gems move, source → destination | **no**, per pass |
| which new gems appear, and their destination cells | **no** |
| the resulting authoritative board, per pass | **no** — only the Round's final board |

**The mandatory stop condition of `TASK-275` §3 is therefore triggered for the
per-pass Gravity / Refill transition.** No cascade state was fabricated, no gem
identity was inferred, and no client-side Match-3 resolution was implemented.
The smallest required protocol/data-contract change is recorded below
(`TASK-254` DR-1 option **B**) and was **not** implemented; no backend production
code was modified.

The previously accepted boundary is preserved: `TASK-245` PD-3 (no protocol
change), PD-4 (no per-pass snapshots), PD-8 (within-column pairing only between
the previously rendered board and the authoritative **final** board), and
`TASK-254` DR-1 (option A — one supported board transition, one phase per
delivered cascade depth — accepted as the supported subset).

---

## Scope

### In Scope
* **Presentation-only turn banners** (`banner` phase kind / `turn-banner` action),
  opened once per delivered `SwapStarted` boundary whose `actor` is one of the
  contract's two values, placed before the exchange they introduce.
* **Confirmed-gem destruction** per match/cascade step: `MatchCreated.cells` and
  `GemMatched.cellIndex` only, each gated on the cell's **rendered content**
  agreeing with the delivered `gemType` (and the consumed `specialGem` where the
  event carries one).
* **Refill entrance** on the single authoritative board transition, now triggered
  for every cell a clear emptied.
* **Tests** for the banner lifecycle, destruction ordering, the refused clear, the
  single board transition, recovery during banner / swap / destruction / refill,
  input locking, duplicate suppression and shutdown.

### Out of Scope
* **Per-pass gravity/drop animation and refill contents** — the contract delivers
  neither (`TASK-275` §3 stop condition; `TASK-254` DR-1 option B is the
  decision required). The accepted conservative fallback is retained.
* No wire change: no hub method, argument, subscription, state-push member, or
  `type` discriminator (`SIGNALR_PROTOCOL.md` §3.2.2 stays closed at 18 names).
* No backend, Domain, Application, Redis, or PostgreSQL change.
* No gameplay rule, RNG, balance, damage, HP, Power, Combo, or Passive change.
* No client-side match detection, gravity destination, spawned identity, or
  cascade result (`AGENTS.md` §10).
* No commit or push (operator instruction).

---

## Current State Before This Task

The pipeline built by TASK-245/252/254/262/263/264 already played one phase per
delivered cascade pass and converged the Round's single committed board in one
`settle` phase, and TASK-272/273/274 governed hold, admission, supersession and
recovery. Two `TASK-275` requirements were unmet: there was **no turn banner of
any kind**, and a match step presented only a flashing overlay — **no gem was
ever removed**, so no cell was ever re-filled from the delivered board.

---

## Declared File Set

```text
src/frontend/client/src/game/scenes/BattleScene.ts
src/frontend/client/src/game/scenes/BattleEventPresenter.ts
src/frontend/client/tests/SceneLifecycle.test.ts
src/frontend/client/tests/BattleEventPresentation.test.ts
```

---

## Acceptance Criteria

* [x] **AC-1 (Banner boundary):** exactly one banner per delivered `SwapStarted`
      with a recognised `actor`, in the delivered order, before its exchange.
* [x] **AC-2 (Banner is presentation-only):** no gameplay event, no sequence
      increment, no request, no state member, no new HUD region, no queue.
* [x] **AC-3 (Banner completion):** the next phase starts only after the banner
      phase's pacing tween completes; an unrecognised `actor` and a batch with no
      boundary play no banner.
* [x] **AC-4 (Destruction):** only cells the delivered events confirm are cleared,
      and only when the rendered content agrees; a `MatchCreated` cell is
      confirmed by its `gemType`, a `GemMatched` cell by `gemType` + `specialGem`.
* [x] **AC-5 (No fabrication):** a cascade pass whose delivered cells disagree
      with the rendered board clears nothing — no fabricated cascade state.
* [x] **AC-6 (Convergence):** the Round's single board transition re-labels every
      cleared cell from the accepted authoritative board, with the refill
      entrance; `settledBoardSequence`/`settledBattleId` match that state.
* [x] **AC-7 (No derived movement):** no gem view changes cell except by a
      delivered `SwapStarted`.
* [x] **AC-8 (Recovery):** a snapshot during the banner, the swap, the destruction
      or the refill transition converges the board, restores every cleared cell,
      cancels the phase's resources, and releases the lock exactly once.
* [x] **AC-9 (Input):** input stays locked across banner → swap → destruction →
      transition, and is released at timeline end or by valid recovery.
* [x] **AC-10 (No duplicates):** a repeated batch creates no second banner, no
      second clear and no second effect.
* [x] **AC-11 (Shutdown):** a scene taken down mid-banner owns no tween, effect,
      lock or timeline afterwards.
* [x] **AC-12 (Verification):** `git diff --check`, `npx tsc --noEmit`,
      `npm run build` and the full frontend suite pass; TASK-273/TASK-274
      regression assertions are green and unweakened; pre-existing working-tree
      changes are preserved; no commit or push.

---

## Implementation Notes

### `BattleEventPresenter.ts` (+2 lines)
* `PresentationPhaseKind` gains `'banner'`; `PresentationAction` gains
  `'turn-banner'`. No other vocabulary changes; `adaptPresentedEvent` maps **no**
  delivered event to either (pinned by test), so a banner can only come from the
  scene's delivered-boundary handling.

### `BattleScene.ts` (+325 / −4)
* `TURN_BANNER_*` constants: the three beats, the enter scale, font size, and the
  two label/colour entries — declared together so one expression derives a
  phase's hold (`phaseEffectMs`).
* `buildPresentationPhases`: a `bannerSide` cursor; when an adapted `swap` step's
  `actor` is `player`/`boss` and differs from the last banner, the open phase is
  flushed and a `banner` phase is pushed before the exchange. The submitted-pair
  fallback (no delivered boundary) plays **no** banner — it has no delivered actor.
* `playTurnBanner`: one transient `Text` in the feedback layer at the board's
  centre, registered in `timelineEffects`; a scale-in enter tween and one
  delayed fade-out tween that destroys it. No HUD region, no queue.
* `GemView.cleared`; `gemContentKeyFor` extracted so a delivered cell and a
  delivered clear are compared in one vocabulary.
* `presentMatchDestruction` + `clearConfirmedCell`: hide a view's tile and label,
  drop its content, and mark it cleared — only when the rendered content is the
  content the event names. Idempotent for a repeated event or a cell named twice.
* `drawGemContent` restores visibility and clears the flag; `renderBoard` treats a
  cleared view as a content change, so the `settle` phase re-labels it from the
  delivered cell with the existing `placeGemView` refill entrance.

### Tests
* `SceneLifecycle.test.ts`: 13 new tests in
  `TASK-275 — turn banners, confirmed-gem destruction & the single board transition`.
* `BattleEventPresentation.test.ts`: 2 new tests pinning that no delivered event
  maps to the banner and that the presenter only *names* the vocabulary.
* Four pre-existing exact-phase-list assertions (TASK-263 ×2, TASK-264 ×2, plus
  the `BOTH_ACTORS_PHASES` fixture) were **extended, not weakened**: the same
  delivered order plus the new presentation-only banner entries.

---

## Testing Requirements

```text
[x] git diff --check                          exit 0
[x] npx tsc --noEmit (src/frontend/client)    exit 0
[x] npm run build                             PASS (tsc + vite, 83 modules)
[x] npx vitest run                            24 files, 1188 tests, all pass
[x] Focused presentation suites               431 tests pass
    (SceneLifecycle 329, BattleEventPresentation 102)
[ ] Backend tests                             NOT RUN — no backend change is
                                              authorized or required by the
                                              validated contract (§10)
```

Baseline: this working tree held 1,173 passing tests across 24 files before this
task's changes; the four assertions that failed in the first post-change run were
pre-existing passing tests whose expectation this task deliberately extends (the
banner phase is new). No TASK-273/TASK-274 assertion was relaxed.

**Phaser.** Installed `phaser` is **4.2.1** (`package.json` and
`node_modules/phaser/package.json` agree). Every API this change uses
(`add.text`, `setOrigin`, `setAlpha`, `setScale`, `setVisible`, `tweens.add`,
`killTweensOf`, `destroy`) is Phaser 4 API already used in the file, and
`npx tsc --noEmit` validates each call against the installed typings. No Phaser 3
API and no `this.time.*` timer was introduced.

---

## Stop Conditions

* If truthful per-pass gravity/refill presentation requires intermediate
  server-resolved data the contract does not provide: **STOP that portion**,
  record the exact limitation and the decision required (`TASK-254` DR-1), and do
  not approximate it as authoritative. — **Hit, recorded, not approximated.**
* If a criterion would need a new hub method, event, subscription, wire member or
  per-pass snapshot: STOP per `AGENTS.md` §18 (`SIGNALR_PROTOCOL.md` §8 items
  5–9). — **Not required by anything implemented here.**

---

## Completion Evidence

### Changed Files
| File | Change |
| --- | --- |
| `src/frontend/client/src/game/scenes/BattleEventPresenter.ts` | `banner` phase kind, `turn-banner` action |
| `src/frontend/client/src/game/scenes/BattleScene.ts` | banner constants, banner phase build + render, `GemView.cleared`, `gemContentKeyFor`, `presentMatchDestruction`, `clearConfirmedCell`, visibility restore in `drawGemContent`/`renderBoard` |
| `src/frontend/client/tests/SceneLifecycle.test.ts` | 13 new tests; 5 pre-existing expectations extended |
| `src/frontend/client/tests/BattleEventPresentation.test.ts` | 2 new tests |

### Preservation
* Branch `master`, HEAD `2bffa0b4cb591b32c4abf749013f49ba0039ac1b` — unchanged.
* `git status --short` after this task is the same 19 entries as before it
  (15 modified, 4 untracked); no pre-existing change was reverted, staged, or
  destroyed.
* Every declared source edit was verified scoped by diffing the working file
  against the session's pristine capture of the pre-existing content: 4 removed
  lines in `BattleScene.ts`, 0 in the presenter, 2 in the test file — all
  intended.
* No commit, no push, no staging.

### Required Protocol / Data-Contract Follow-Up
**`TASK-254` DR-1 option B.** To present Gravity/Drop and Refill per authoritative
pass, the contract must deliver one of:

```text
(B1) a per-pass board snapshot inside the round's single batch (a new payload
     member or discriminator), or
(B2) a per-pass transition record: cleared cells (already delivered), the source
     and destination cell of each surviving gem, and the destination cell and
     Gem type of each spawned gem
```

Either is a protocol/state-contract change requiring its own authorized task
(`GAME_RULES.md` §16 → `GAME_EVENTS.md` §2 → `SIGNALR_PROTOCOL.md` §3.2.x), an
ADR-026 amendment, and Product-Owner approval. `SIGNALR_PROTOCOL.md` §3.1 item 2
(no per-step delivery) and `TASK-245` PD-4 must be reconciled by that decision
first.

### Findings Reported, Not Fixed
1. `timelineEffects` retains references to effects that destroyed themselves on a
   **normally completed** timeline (`finishPresentation` → `releaseTimeline` drops
   neither the array nor its entries; only `cancelTimelineEffects` on abort or
   shutdown does). Bounded per resolution (~1–6) but unbounded across a long
   battle. Suggested follow-up: drop the array on timeline release.
2. `GAME_EVENTS.md` §2 `PassiveCharged`/`PassiveTriggered` item 4 labels the Boss
   steps 18a/18b/18c where `GAME_RULES.md` §17 uses 18a–18d (letters off by one;
   the ordering agrees). Docs-vs-docs.
3. `TASK-246` C-1 remains open (`TurnStarted`/`TurnEnded` are defined in
   `GAME_EVENTS.md` §1/§2 but are outside the closed wire set), as does TASK-246
   `PD-B` (a server-asserted turn indicator). This task's banner is derived from
   the delivered `SwapStarted.actor` and adds no wire member, so it neither
   resolves nor depends on C-1.
4. `TASK-218A` forbade "a turn banner" within its own Relic-HUD scope. TASK-275
   explicitly requires `PLAYER TURN` / `BOSS TURN` presentation; this task
   satisfies that while keeping the TASK-218A boundary it was about — no new HUD
   region, no event queue, no second notification system, no wire change. Recorded
   rather than silently resolved (`AGENTS.md` §4).

### Verdict

```text
CONDITIONAL PASS
```

Safe, fully tested and non-fabricating for every phase the contract supports; the
per-pass Gravity/Refill animation required by `TASK-275` §6.3/§6.4 is **not
implementable** under the validated contract and is reported as the DR-1 protocol
decision above. If the operator treats per-pass gravity/refill as a blocking
acceptance requirement, the verdict is FAIL pending that authorized change.
