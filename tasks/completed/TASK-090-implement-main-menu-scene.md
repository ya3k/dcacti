# TASK-090 — Implement the MainMenuScene (Main Menu Presentation and Navigation)

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and src/ by path and section; copies only what an
  implementer needs to know where to look.

  This is the "MainMenuScene" task explicitly deferred by TDD.md §2.1's MVP
  staging note (the same note that produced TASK-087's ResultScene; its
  sibling is now DONE) and recorded as "owned by a later task" in
  GameConfig.ts. It is the only documented scene that does not exist, so
  the implemented transition order still bypasses it
  (PreloaderScene → LobbyScene directly).
-->

---

## Metadata

```text
Task ID:           TASK-090
Type:              FEATURE (MainMenuScene and its place in the lifecycle are
                   specified by TDD.md §2.1 + ARCHITECTURE.md §1 + ADR-003;
                   only the client implementation is missing -
                   tasks/TASK_TYPES.md §2)
Status:            DONE
Risk:              MEDIUM (FEATURE baseline for Frontend/Phaser;
                   presentation-only scene, no battle-state/auth/protocol
                   change - tasks/TASK_TYPES.md §4, validation depth per
                   .ai/workflow/core/validation.md §2)
Priority:          MEDIUM (completes the documented six-scene lifecycle and
                   lifts the recorded staging bypass - docs/02-technical/
                   TDD.md §2.1; NOT on the Phase 1 critical path - the
                   staging note explicitly allowed the bypass while
                   deferring this scene, docs/00-overview/ROADMAP.md §1)
Primary Agent:     client
Supporting Agents: testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, phaser/scenes,
                   client/phaser-architecture, client/react-phaser-boundary,
                   testing/test-scenario-generation
Dependencies:      TASK-078 (Product Owner staging decision this task lifts),
                   TASK-087 (sibling deferral completed: ResultScene)
```

---

## Objective

Implement the documented `MainMenuScene` — "Main game menu presentation and
navigation" (`docs/02-technical/TDD.md` §2.1) — and remove the staging
bypass, so the implemented scene order matches the specified lifecycle:
`BootScene → PreloaderScene → MainMenuScene → LobbyScene → BattleScene →
ResultScene` (`TDD.md` §2.1 diagram; `docs/02-technical/ARCHITECTURE.md`
§1:145; `docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md`:26, :69).
Presentation and navigation only: the scene renders the main menu and
performs its documented navigation into `LobbyScene`. It owns no state, no
authority, no protocol surface (`AGENTS.md` §10, `TDD.md` §2.1
Server-Authoritative Boundary).

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — client presentation / playable slice
  is IN (same classification as TASK-069 / TASK-087); §2 for exclusions;
  §4 for classification authority
- `docs/02-technical/TDD.md` §2.1 — scene lifecycle diagram (lines 89-101),
  the MVP staging note deferring `MainMenuScene` "to its own task" (lines
  103-111: staging is an implementation-order decision, "not a change to
  the lifecycle"), the `MainMenuScene` definition (line 115), the
  Phaser/React split (lines 119-120), and React Responsibilities & Boundaries
  (lines 147-154: React owns the application shell including "menus,
  settings" — see Stop Conditions)
- `docs/02-technical/ARCHITECTURE.md` §1 — file tree entry
  `MainMenuScene.ts  # Main menu presentation` (line 76) and lifecycle
  (line 145)
- `docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md` — scene list
  (line 26) and clear Phaser Scene lifecycle (line 69)
- `docs/00-overview/ROADMAP.md` §1 — Phase 1 goal ("one playable battle")
  context: this task completes documented presentation, it does not add a
  gameplay system
- `AGENTS.md` §10 / §13 / §16 / §20 — server authority, Phaser vs React
  boundary, task discipline, stop conditions

---

## Scope

### In Scope

- New `src/frontend/client/src/game/scenes/MainMenuScene.ts`: main menu
  presentation on create + its documented navigation action starting
  `LobbyScene`.
- Register the scene in `GameConfig.ts` and route the boot flow through it
  (`PreloaderScene` starts `MainMenuScene` instead of skipping to
  `LobbyScene`); update the `GameConfig.ts` staging comment (lines 34-43),
  which currently records the deferral as still pending.
- Update tests that encode the staging bypass, and add scene + navigation
  tests (see Testing Requirements).

### Out of Scope

- **Any navigation edge not in the `TDD.md` §2.1 lifecycle** — return from
  `LobbyScene` to the menu, post-`ResultScene` navigation, or any
  loop-back. Only `Preloader → MainMenu` and `MainMenu → Lobby` are
  documented; anything else is invented (STOP per §7).
- **Menu content beyond presentation and navigation** — settings,
  collections, profile/account, history, news. React owns the application
  shell's "menus, settings" (`TDD.md` §2.1 lines 147-154), and no MVP main-menu
  content list exists in `docs/`.
- **React shell changes** (`src/frontend/client/src/app/*`) of any kind.
- **Reward / result presentation or `GET /api/battle/{battleId}/result`**
  — remains deferred with no documented client trigger (TASK-087 Out of
  Scope; TASK-089 report).
- Backend, `docs/`, ADR, API-contract, and SignalR-protocol changes; any new
  hub method, event, or runtime port member; session/auth; reconnect/resync
  (`SIGNALR_PROTOCOL.md` §7, Phase 3).
- Any client-authoritative gameplay logic (`AGENTS.md` §10).
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State

`MainMenuScene` exists nowhere in `src/` — only as documentation and as
deferral records. `GameConfig.ts:67` registers five scenes
(`[BootScene, PreloaderScene, LobbyScene, BattleScene, ResultScene]`) and
its comment (`:34-43`) states `MainMenuScene` "is owned by a later task, so
the registered list is the five scenes below". `PreloaderScene` performs
the staging bypass: both its zero-asset `create()` path and its
`onLoadingComplete()` handler call `this.scene.start('LobbyScene')`, guarded
by `hasTransitioned` so it fires exactly once. Two tests encode the bypass:
`tests/PhaserGame.test.tsx:56-57` asserts the exact registered key list
`['BootScene','PreloaderScene','LobbyScene','BattleScene','ResultScene']`
with the comment "`MainMenuScene` remains deferred to its own task", and
`tests/SceneLifecycle.test.ts` (`:310-315` exposes the documented scene
classes; the Preloader block asserts "transitions to LobbyScene exactly
once"). `LobbyScene` already transitions onward to `BattleScene`
(`LobbyScene.ts:397`) and is unaffected.

---

## Acceptance Criteria

- [ ] `MainMenuScene` exists at
      `src/frontend/client/src/game/scenes/MainMenuScene.ts`, is registered
      in `GameConfig.ts`, and the registered order equals
      `[BootScene, PreloaderScene, MainMenuScene, LobbyScene, BattleScene,
      ResultScene]` — asserted by a test against the `TDD.md` §2.1 diagram.
- [ ] `PreloaderScene` starts `MainMenuScene` (both the zero-asset and
      load-complete paths, still guarded so the transition fires at most
      once); the direct `PreloaderScene → LobbyScene` bypass is gone —
      asserted by test.
- [ ] On create, `MainMenuScene` presents a main menu, and its documented
      navigation action starts `LobbyScene` — asserted by test; navigating
      adds or changes no runtime port member, so
      `tests/RuntimeBoundaries.test.ts`'s port-surface assertion still
      passes.
- [ ] The scene is presentation/interaction only: no gameplay computation,
      no state mutation, no REST call, no hub interaction, no reward or
      progression rendering (`AGENTS.md` §10, `GAME_STATE.md` §4).
- [ ] No invented content or edges: no settings/collections/profile/history
      UI, no React shell edit, no navigation edge beyond
      `Preloader → MainMenu → Lobby`, and zero backend/docs/ADR/API/SignalR
      changes.
- [ ] Tests that encode the staging bypass are updated to the documented
      lifecycle (Preloader target, registered key list, documented scene
      classes) with no other assertion loosened; all previously passing
      tests still pass.
- [ ] All relevant tests pass at MEDIUM validation depth
      (`.ai/workflow/core/validation.md` §2: build + unit + integration +
      architecture conformance + `quality/review.md`).
- [ ] Quality review checklist passes (`quality/review.md` §1); no
      authoritative rule or contract violated (`AGENTS.md` §10 / ADR-001).
- [ ] Zero edits to `tasks/completed/*` and `tasks/blocked/*`
      (`AGENTS.md` §16).

---

## Affected Files & Areas

```text
[ ] src/backend/                                   - NOT touched
[x] src/frontend/client/src/game/scenes/MainMenuScene.ts   - NEW (menu presentation + navigation)
[x] src/frontend/client/src/game/GameConfig.ts             - register scene; update deferral comment (:34-43, :67)
[x] src/frontend/client/src/game/scenes/PreloaderScene.ts  - start MainMenuScene (remove bypass)
[x] src/frontend/client/tests/ (new/extended)              - scene, navigation, lifecycle tests
[x] src/frontend/client/tests/PhaserGame.test.tsx          - expected registered scene list (:56-57)
[ ] src/frontend/client/src/game/runtime/          - reused as-is; no port/contract change
[ ] src/frontend/client/src/app/ (React shell)     - NOT touched
[ ] tests/                                         - covered by the client suite above
[ ] docs/ + tasks/ (other than this file)          - NOT touched
```

---

## Implementation Notes

- Follow existing scene structure: class extends `Phaser.Scene` with a
  string key, `create()` doing presentation only (`PreloaderScene.ts`,
  `ResultScene.ts` are the closest precedents); register in
  `GameConfig.ts:67`'s import list and `scene:` array.
- Preserve `PreloaderScene`'s once-only guard: with an empty load queue both
  `create()` and the `complete` handler run, so route both paths through the
  same guarded method that now starts `MainMenuScene`.
- Test harness precedent: `tests/SceneLifecycle.test.ts`
  (`createSceneHarness` / `runScene`, Preloader block) and
  `tests/PhaserGame.test.tsx:53-57`.
- The Phaser menu is game-canvas presentation of the game's entry flow;
  React keeps the application/platform shell (`TDD.md` §2.1 Phaser/React
  split). Do not move shell concerns into the scene or game flows into React.
- `GameConfig.ts`'s lifecycle comment (`:34-43`) must end up describing the
  implemented six-scene order — it is a source comment, not a `docs/` edit.
- Keep the scene key a literal string consistent with `scene.start(...)`
  usage across the other scenes; no new navigation framework or abstraction
  (`AGENTS.md` §9).

---

## Testing Requirements

### Required Verification

```text
[ ] Unit tests         - MainMenuScene (mocked/no runtime): presents the menu
                         on create; navigation action starts LobbyScene;
                         no transition before the action; shutdown before
                         navigation is safe and starts nothing.
                         PreloaderScene: both zero-asset and load-complete
                         paths start MainMenuScene exactly once; no path
                         starts LobbyScene.
[ ] Integration tests  - registered scene key order equals the TDD.md §2.1
                         diagram (PhaserGame.test.tsx); documented scene
                         classes include MainMenuScene; Boot → Preloader →
                         MainMenu handoff asserted via the scene harness.
[ ] Gameplay scenarios - N/A, justified: presentation and navigation only;
                         no gameplay rule, GameplayState, or RNG touched
                         (scenes are presentation components, TDD.md §2.1);
                         MEDIUM depth is met by build + unit + integration +
                         architecture conformance + review.
[ ] Regression         - full client suite (`npx vitest run`), type check
                         (`npx tsc --noEmit`), production build
                         (`npx vite build`), and RuntimeBoundaries
                         port-surface assertion all pass unchanged.
```

### Key Edge Cases

- Both preloader completion paths (zero-asset `create()` and load
  `complete` handler) — menu must still be entered exactly once.
- Navigation action fired twice / scene already started — no duplicate
  `LobbyScene` start (mirror the existing `hasTransitioned` precedent).
- Scene stopped before any navigation (app teardown) — no leaked
  subscriptions, no invented transition.
- React shell renders concurrently with the canvas menu — no duplicate or
  competing menu UI introduced by this task.

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative
  References: STOP per `AGENTS.md` §7 — report the missing detail
- If any criterion requires menu content other than presentation + the
  documented navigation (settings, collections, profile, account, news):
  STOP per `AGENTS.md` §7/§20 — no MVP main-menu content is documented, and
  `TDD.md` §2.1 assigns shell "menus, settings" to React
- If any criterion requires a navigation edge beyond
  `Preloader → MainMenu → Lobby` (return-to-menu, post-result navigation):
  STOP per `AGENTS.md` §7 — the documented lifecycle defines no such edge
  (`TDD.md` §2.1; TASK-087 Out of Scope)
- If any criterion requires reward/progression rendering or a call to
  `GET /api/battle/{battleId}/result`: STOP per `AGENTS.md` §7/§20 — no
  documented client trigger exists (TASK-087 Out of Scope, TASK-089 report)
- If the task requires a protocol change, a new hub method/event, or a
  runtime port change: STOP per `AGENTS.md` §7/§18
- If the task requires backend, `docs/`, or ADR changes: STOP per
  `AGENTS.md` §17/§18 — report instead
- If the task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: STOP & decompose (`tasks/README.md` §13)
- If satisfying any criterion requires editing `tasks/completed/*` or
  `tasks/blocked/*`: STOP per `AGENTS.md` §16 — report instead

---

## Completion Evidence

### Status

DONE

### Changed Files

- `src/frontend/client/src/game/scenes/MainMenuScene.ts` — NEW: Main menu presentation and navigation to LobbyScene
- `src/frontend/client/src/game/scenes/PreloaderScene.ts` — Modified: transition target changed from LobbyScene to MainMenuScene (both zero-asset create() and load-complete paths converge on guarded onLoadingComplete)
- `src/frontend/client/src/game/GameConfig.ts` — Modified: added MainMenuScene import, registered in scene array, updated lifecycle comment
- `src/frontend/client/tests/SceneLifecycle.test.ts` — Modified: added MainMenuScene import, updated scene classes test, updated Preloader tests to target MainMenuScene, added 6 MainMenuScene unit tests, extended harness rectangle mock with setInteractive/on
- `src/frontend/client/tests/PhaserGame.test.tsx` — Modified: updated expected scene key list to include MainMenuScene
- `src/frontend/client/tests/RuntimeBoundaries.test.ts` — Modified: added MainMenuScene.ts to the boundary-scanned scene file list

### Scene Lifecycle

Confirmed implemented order:

```text
BootScene
  → PreloaderScene
    → MainMenuScene
      → LobbyScene
        → BattleScene
          → ResultScene
```

All six scenes are registered in GameConfig.ts in exactly this order.

### MainMenu Behavior

MainMenuScene renders a minimal main menu with:
- A dark background inside the safe area
- "DCACTI" title text
- A "START BATTLE" button that navigates to LobbyScene

Navigation is guarded by a `hasTransitioned` flag so repeated clicks start LobbyScene at most once. Shutdown before navigation is safe (no subscriptions, no leaked state). The scene performs no API calls, no SignalR interaction, no gameplay computation, and owns no game state.

### Tests

**Unit:**
- MainMenuScene (6 tests): creation, presentation, no pre-navigation, navigation starts LobbyScene, repeated navigation guard, shutdown safety — all PASS
- PreloaderScene (4 tests): zero-asset path targets MainMenuScene, load-complete path targets MainMenuScene, transitions exactly once, never starts LobbyScene directly — all PASS

**Integration:**
- PhaserGame.test.tsx: registered scene key list includes MainMenuScene — PASS
- SceneLifecycle.test.ts: documented scene classes include MainMenuScene — PASS
- RuntimeBoundaries.test.ts: MainMenuScene.ts included in boundary-scanned files — PASS

**TypeScript:** `npx tsc --noEmit` — 0 errors

**Build:** `npx vite build` — success

**Review:** Quality checklist passed — correctness, architecture, scope, tests, documentation, security, performance, maintainability all verified.

### Scope Verification

- [x] No gameplay logic
- [x] No API calls
- [x] No SignalR changes
- [x] No runtime port changes
- [x] No authentication changes
- [x] No React shell changes (`src/frontend/client/src/app/*` untouched)
- [x] No backend changes
- [x] No docs changes
- [x] No database changes
- [x] No Redis changes
- [x] No reward/result implementation
- [x] No undocumented menu features (Settings, Collections, Profile, Account, History, News, Rewards, Progression)
- [x] No navigation edges beyond Preloader → MainMenu → Lobby
- [x] Zero edits to tasks/completed/* and tasks/blocked/*

### Next Step

TASK-091 (LobbyScene full implementation) or the next backlog task as determined by the orchestrator. Do not implement here.
