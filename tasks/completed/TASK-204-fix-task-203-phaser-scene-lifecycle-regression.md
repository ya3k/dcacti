# TASK-204 — Fix the TASK-203 Phaser Scene Lifecycle Regression (BattleScene Teardown, Result Navigation, Battle Reuse)

---

## Metadata

```text
Task ID:           TASK-204
Type:              BUG (TASK_TYPES.md — a completed task's approved behavior is
                   functionally blocked in the real runtime; development/bug-fix.md)
Status:            DONE
Risk:              MEDIUM (client scene lifecycle and runtime subscriptions; no
                   server contract, no gameplay, no state-model change)
Priority:          HIGH (TASK-203 is functionally blocked in the browser; the
                   player cannot leave ResultScene)
Primary Agent:     client (TASK_TYPES.md §5 — Frontend/Phaser; AGENT_SELECTION.md)
Supporting Agents: testing, review
Workflow:          development/bug-fix.md
Skills:            client/phaser-architecture, discovery/impact-analysis,
                   testing/test-scenario-generation, quality/scope-validation
                   (4 skills — Simple/Normal budget, tasks/README.md §12)
Dependencies:      TASK-203 (DONE — its approved behavior is what this task
                   unblocks), TASK-202 (DONE — decision authority)
Blocks:            None
```

**Lifecycle note.** `tasks/README.md` §4: the status field reflects the state, and
the only file move was into `completed/`. This task arrived out of band as an
execution manifest rather than as a `backlog/` file; its record is filed here so
the TASK-203 chain stays traceable (`tasks/README.md` §5).

---

## Objective

Make the TASK-203 post-result lifecycle work in the real Phaser 4.2.1 browser
runtime by attaching `BattleScene`'s existing teardown to the engine's actual
scene lifecycle event, so the completed battle's cleanup cannot reach a
shut-down scene, and so a reused `BattleScene` instance starts the next battle
from a clean slate.

---

## Authoritative References

- `tasks/completed/TASK-203-post-result-lifecycle-implementation.md` — the
  approved implementation, its decisions, and its affected-file boundary
- `tasks/completed/TASK-202-post-result-lifecycle-product-decision-and-specification.md`
  — `D-202-01 = C`, `D-202-02 = A`, `D-202-03 = D`, `D-202-04 = A`
- `docs/00-overview/GDD.md` §2, §2.1 — the approved player-facing post-result flow
- `docs/02-technical/TDD.md` §2.1 — the approved scene lifecycle, the two
  continuations, the cleanup rule
- `docs/02-technical/ARCHITECTURE.md` §2.2.1 (rules 1–6, the client-local
  `clearActiveBattleState()`), §2.2.3 (carrier boundary)
- `docs/03-decisions/ADR/ADR-022-post-result-preserved-loadout-carrier.md` — D5
  (preserved loadout ≠ active battle state) and D6 (the cleanup capability)
- `docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md` — Phaser is the client
  presentation engine
- Installed dependency (read, not modified):
  `src/frontend/client/node_modules/phaser@4.2.1` —
  `src/scene/Scene.js` (no `shutdown` method exists),
  `src/scene/Systems.js` (`#shutdown` emits `Phaser.Scenes.Events.SHUTDOWN`;
  `#destroy` emits `DESTROY`), `src/scene/SceneManager.js` (`stop` →
  `sys.shutdown()`, `create` → `scene.create(data)`),
  `src/gameobjects/DisplayList.js` (`#shutdown` destroys every child)
- `.ai/skills/client/phaser-architecture/SKILL.md` ("Resource Lifecycle &
  Cleanup": `this.events.once(Phaser.Scenes.Events.SHUTDOWN, …)`) and
  `.ai/skills/phaser/scenes/SKILL.md` (gotcha 14)

---

## Root Cause

`BattleScene` (and the other scenes) declared a method named `shutdown()` and
relied on Phaser invoking it. Phaser 4.2.1 does not: a scene's methods are
invoked by name only for `init` / `preload` / `create` / `update`
(`SceneManager.bootScene`, `SceneManager.create`), and `Phaser.Scene` declares no
`shutdown` method at all. The engine's teardown signal is the
`Phaser.Scenes.Events.SHUTDOWN` event emitted by `Phaser.Scenes.Systems#shutdown`
(`SceneManager.stop`, and restarting a running scene), with `DESTROY` from
`Systems#destroy`.

Two independent consequences followed:

```text
1. ResultScene exit            → clearActiveBattleState() → GameRuntime.updateState
                               → emit → the stale BattleScene listener runs
                               → BattleScene.renderRuntimeState() writes to a Text
                                 that Phaser's DisplayList#shutdown destroyed
                               → TypeError aborts `scene.start(...)`

2. BattleScene reuse           → `outcomeHandled === true` survives the instance
                               → Battle 2's outcome is discarded
                               → Battle 2 never reaches ResultScene
```

`Systems#shutdown`'s own documentation calls the *Systems* method "Scene.shutdown",
which is how the scene-level method was mistaken for an engine hook.

---

## Scope

### In Scope

- `BattleScene`'s teardown, wired to `Phaser.Scenes.Events.SHUTDOWN` / `DESTROY`.
- The TASK-203 navigation regression it caused, and `BattleScene` reuse.
- The tests proving the mechanism (scene lifecycle, listener cleanup, both
  post-result exits, battle reuse, the cleanup boundary) and the browser E2E flow.

### Out of Scope (unchanged, verified not required)

- Any server/API/SignalR/Redis/database/gameplay/balance change; any change to
  TASK-202's product decisions or ADR-022's carrier.
- The `ApplicationSession` `localStorage` mismatch (TASK-203's reported issue).
- The repo-wide lifecycle finding (see "Reported Issue" below).

---

## Current State

`BattleScene.create()` subscribed to three `GameRuntime` streams and released
them only in a custom `shutdown()` that nothing called. The unit harnesses drove
that method by name (`runScene(scene, ctx, 'shutdown')`), so the suite modelled a
lifecycle the engine does not have — which is why 652 green tests coexisted with
a broken browser.

---

## Acceptance Criteria

- [x] `BattleScene`'s cleanup is attached to `Phaser.Scenes.Events.SHUTDOWN`
      (and `DESTROY`), not to the existence of a method.
- [x] After Phaser shuts the scene down, none of its listeners remains on the
      runtime (`runtimeListeners` / `battleListeners` / `battleStateListeners`).
- [x] `ResultScene` reaches `scene.start(...)` on both exits: `PLAY AGAIN` →
      `LobbyScene`, `MAIN MENU` → `MainMenuScene`, with no runtime-listener
      exception.
- [x] `clearActiveBattleState()` still clears ACTIVE battle state and still does
      not clear the preserved loadout (ADR-022 D5, TASK-203's invariant).
- [x] A reused `BattleScene` starts with `outcomeHandled === false`, exactly one
      subscription per stream, and can process Battle 2's outcome.
- [x] Battle 2 has its own battle identity, holds no Battle 1 state, and reaches
      `ResultScene`.
- [x] No backend, REST, SignalR, Redis, database, gameplay, balance, or
      authentication change; no `docs/` or `node_modules` change.
- [x] `npx tsc --noEmit`, `npx vitest run`, `npm run build`,
      `npm run verify:e2e:smoke`, `git diff --check` all pass.

---

## Affected Files & Areas

```text
[x] src/frontend/client/src/game/scenes/BattleScene.ts
        — the two lifecycle registrations and the teardown documentation
[x] src/frontend/client/tests/support/SceneEventEmitter.ts (NEW)
        — the scene's own event emitter, for driving the engine's lifecycle
[x] src/frontend/client/tests/SceneLifecycle.test.ts
[x] src/frontend/client/tests/ResultScene.test.ts
[x] src/frontend/client/tests/BattleEventPresentation.test.ts
[x] src/frontend/client/scripts/standalone-web-smoke.mjs
        — the post-result browser flow (the existing E2E harness)
[ ] src/backend/, docs/, node_modules/                — NOT changed
[ ] src/frontend/client/src/game/scenes/ResultScene.ts, LobbyScene.ts,
    MainMenuScene.ts, CollectionViewerScene.ts        — NOT changed (see below)
```

---

## Implementation Notes

- `Phaser.Scenes.Events.SHUTDOWN` / `DESTROY`, registered with `once` in
  `create()` — the pattern `.ai/skills/client/phaser-architecture` documents. No
  polling, timer, manual call from another scene, monkey patch, Phaser-internal
  change, or navigation-specific hack.
- Registering on `DESTROY` as well covers the game-destroyed path; releasing
  twice is idempotent (every handle is null-safe).
- No new state mechanism, no port capability, no API, no ADR: the change is
  inside an existing scene's existing teardown, and the mechanism is already the
  repository-approved one (`AGENTS.md` §18 checked first — the decision exists).

---

## Completion Evidence

### Changed Files

- `src/frontend/client/src/game/scenes/BattleScene.ts` — `create()` now
  subscribes its existing teardown to `Phaser.Scenes.Events.SHUTDOWN` and
  `DESTROY` (`events.once`), with the engine behavior documented at the call
  site; the teardown doc comment now states that it is invoked *by the engine's
  event*, not because the method exists. The teardown body is unchanged: it
  still releases all three runtime subscriptions and resets the scene-instance
  state (`outcomeHandled`, input guards, board/cast/feedback layers, card
  definitions, `currentBattleState`).
- `src/frontend/client/tests/support/SceneEventEmitter.ts` — **NEW**: a faithful
  `scene.events` (eventemitter3 semantics), so a test raises the engine's events
  instead of calling a method by name.
- `src/frontend/client/tests/SceneLifecycle.test.ts` — the mock exposes
  `Phaser.Scenes.Events`; the harness models `this.events`, the display list
  (destroyed on teardown, with a destroyed Text throwing when written to — the
  browser evidence), and the runtime's notify-on-`clearActiveBattleState`
  transition; the shutdown tests now drive the engine event; new TASK-204 suite
  (7 tests) plus 4 lifecycle tests.
- `src/frontend/client/tests/ResultScene.test.ts`,
  `src/frontend/client/tests/BattleEventPresentation.test.ts` — mock, harness,
  and their BattleScene teardown tests updated to the engine event.
- `src/frontend/client/scripts/standalone-web-smoke.mjs` — phase 7 now reads the
  battle id from the synchronized battle state (the technical runtime state has
  none) and asserts no automatic transition; **new phase 8** exercises
  `PLAY AGAIN` → Lobby with the loadout restored and edited → Battle 2 with a new
  battle identity, a fresh guard and no duplicate listener → Battle 2's outcome →
  ResultScene → `MAIN MENU` → MainMenuScene, capturing the real
  `POST /api/battle/start` bodies.

### Validation Results

```text
npx tsc --noEmit                                PASS — clean
npx vitest run  (full frontend suite)           PASS — 663 tests, 19 files
npm run build  (tsc && vite build)              PASS
npm run verify:e2e:smoke  (2 runs, 53 checks)   PASS — 0 failures, both runs
git diff --check                                PASS — no whitespace errors
git status --short                              reviewed — only TASK-204 files
Regression proof (fix temporarily reverted)     11 unit tests fail, and the
                                                browser E2E fails exactly where
                                                the report says: PLAY AGAIN never
                                                reaches LobbyScene
```

### Browser E2E Result (the acceptance gate)

```text
MainMenu → Lobby → Battle 1 → Result → PLAY AGAIN → Lobby(restored)
   → edit Boss → Battle 2 (new battleId) → Result → MAIN MENU → MainMenu
```

Verified in a real headless Chromium browser against the live backend and Vite
dev server: zero uncaught exceptions, zero fatal console errors, no stuck
ResultScene, both continuations work, the preserved loadout survives and is
editable, the edited loadout is what Battle 2 submitted, Battle 2 has a new
battle id and its own 64-cell board, `outcomeHandled` starts `false`, the
runtime-state subscriber count is identical for both battles (no duplicate), and
no automatic transition occurs.

### Server Authority & Scope Verification

- [x] Zero client-authoritative gameplay logic introduced
- [x] Zero backend / REST / SignalR / Redis / database change
- [x] Zero game rule, balance, progression, or content change
- [x] Zero change to TASK-202's decisions, ADR-022's carrier, or the runtime port
- [x] Zero `docs/` change (no source-of-truth conflict introduced)
- [x] Zero `node_modules` change

---

## Reported Issue (not fixed here — separate concern, `AGENTS.md` §16)

**The E2E report's repo-wide lifecycle finding is confirmed and remains open.**
The same "a method named `shutdown()` is not an engine hook" assumption exists in
`MainMenuScene`, `LobbyScene`, `CollectionViewerScene`, and `ResultScene`. Their
teardowns are now known not to run in Phaser 4.2.1:

- `LobbyScene` — `ARCHITECTURE.md` §2.2.3 rule 1 calls the in-progress selection
  "discarded when it shuts down"; in the real runtime it is not, so the scene
  instance keeps a previous selection across a shutdown/start. Harmless on every
  current entry path (`PLAY AGAIN` overwrites it from the carrier), but the
  documented contract and the implementation disagree.
- `ResultScene` — its reference cleanup never runs, so a `loadRewards()` promise
  that settles *after* the player leaves could write to a destroyed Text from an
  async continuation (the same defect class as this task's, reachable only when
  the result read is slower than the player's click). Not observed in the E2E.
- `MainMenuScene`, `CollectionViewerScene` — their teardowns are empty/no-op
  today, so nothing is currently broken; they are listed so the residual set is
  explicit.

This is a bounded, repo-wide scene-lifecycle audit, not part of TASK-204:
TASK-203's correctness did not require it (§2.2.3's carrier already keeps the
`PLAY AGAIN` loadout), and expanding this task into that refactor is exactly what
its scope boundary forbids. **Suggested follow-up:** one task that attaches each
scene's teardown to `Phaser.Scenes.Events.SHUTDOWN` / `DESTROY` and reconciles
`ARCHITECTURE.md` §2.2.3 rule 1 with the resulting behavior.
