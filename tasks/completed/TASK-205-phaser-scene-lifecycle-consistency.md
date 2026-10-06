# TASK-205 — Phaser Scene Lifecycle Consistency Audit & Fix

---

## Metadata

```text
Task ID:           TASK-205
Type:              BUG (TASK_TYPES.md — a completed task's approved cleanup is
                   never invoked in the real runtime; development/bug-fix.md)
Status:            DONE
Risk:              LOW (client scene teardown wiring and one scene-local async
                   guard; no server contract, no gameplay, no state model change)
Priority:          MEDIUM (repo-wide lifecycle consistency; one recorded hazard
                   — a post-teardown async reward write in ResultScene)
Primary Agent:     client (TASK_TYPES.md §5 — Frontend/Phaser; AGENT_SELECTION.md)
Supporting Agents: testing, review
Workflow:          development/bug-fix.md
Skills:            client/phaser-architecture, phaser/scenes,
                   testing/test-scenario-generation, quality/scope-validation
                   (4 skills — Simple/Normal budget, tasks/README.md §12)
Dependencies:      TASK-204 (DONE — its reported repo-wide finding), TASK-203
                   (DONE — the lifecycle whose scenes this fixes), TASK-202 (DONE
                   — decision authority)
Blocks:            None
```

**Lifecycle note.** `tasks/README.md` §4: the status field reflects the state, and
the only file move was into `completed/`. This task arrived out of band as an
execution manifest rather than as a `backlog/` file; its record is filed here so
the TASK-203/204 chain stays traceable (`tasks/README.md` §5), exactly as
TASK-204's was.

---

## Objective

Attach every scene's existing teardown to Phaser 4.2.1's actual scene lifecycle
events, so cleanup runs in the real browser, is idempotent across scene reuse,
and cannot reach destroyed presentation objects — and reconcile the lifecycle
wording in `.ai/skills/` so a custom method named `shutdown()` is never presented
as a Phaser hook.

---

## Authoritative References

- `tasks/completed/TASK-204-fix-task-203-phaser-scene-lifecycle-regression.md` —
  the confirmed finding this task closes ("Reported Issue", §"Reported Issue")
- `tasks/completed/TASK-203-post-result-lifecycle-implementation.md` — the
  approved lifecycle whose scenes are fixed here
- `docs/02-technical/ARCHITECTURE.md` §2.2.1 (runtime port + client-local
  cleanup), §2.2.3 rules 1–2 ("ephemeral, scene-local, discarded on shutdown")
- `docs/02-technical/TDD.md` §2.1 — the approved scene lifecycle
- `docs/03-decisions/ADR/ADR-022-post-result-preserved-loadout-carrier.md` — D5
  (preserved loadout ≠ active battle state) and the carrier boundary
- `.ai/skills/client/phaser-architecture/SKILL.md` — "Resource Lifecycle &
  Cleanup" (the approved event-based teardown pattern)
- `.ai/skills/phaser/scenes/SKILL.md` — gotchas 10 and 14
- Installed dependency (read, not modified):
  `src/frontend/client/node_modules/phaser@4.2.1` —
  `src/scene/Scene.js` (no `shutdown` method),
  `src/scene/Systems.js` (`#shutdown` emits `SHUTDOWN`; `#destroy` emits
  `DESTROY`), `src/scene/SceneManager.js` (`stop` → `sys.shutdown()`,
  `create` → `scene.create(data)`),
  `src/gameobjects/DisplayList.js` (`#shutdown` destroys every child and handles
  the same `SHUTDOWN` event),
  `src/gameobjects/GameObject.js` (`#destroy` returns early once `scene` is
  released — the idempotency the teardowns rely on),
  `src/events/EventEmitter.js` + `eventemitter3` (`listeners(event)` returns the
  original functions, which is how the browser checks identify a scene's own
  teardown)

---

## Root Cause

TASK-204 proved the pattern and fixed one instance of it: a scene method that
merely *exists* is never invoked by Phaser 4.2.1, which calls only
`init` / `preload` / `create` / `update` by name. The teardown signal is the
`Phaser.Scenes.Events.SHUTDOWN` / `DESTROY` **event** on `scene.events`.

`MainMenuScene`, `LobbyScene`, `CollectionViewerScene`, and `ResultScene` all
declared a custom `shutdown()` method and relied on the engine calling it, so the
teardowns were dead code in the browser:

```text
LobbyScene            ARCHITECTURE.md §2.2.3 rule 1 documents the in-progress
                      selection as "discarded when the scene shuts down"; in the
                      runtime it was not, so a reused instance kept a previous
                      selection (harmless on today's entry paths only because
                      PLAY AGAIN overwrites it from the carrier)
CollectionViewerScene its shell/object lists were never cleared, so every
                      reopening stacked another shell on the ended run's
ResultScene           its references were never dropped, so a `loadRewards()`
                      promise that settled after the player left could write to a
                      destroyed `Text` (the E2E report's concrete risk)
MainMenuScene         its teardown owns only the per-run navigation guard, so the
                      impact was latent rather than user-visible
```

A second, independent gap existed in `ResultScene` even once teardown ran: the
reward read is asynchronous, and nulling the reference only protects against the
*current* run. If the instance was reused before the read settled, the continuation
would write the ended run's rewards into the **next** battle's live reward area.

---

## Scope

### In Scope

- The four scenes' teardown wiring, their ownership audit, and the tests that
  model the engine's real lifecycle.
- `ResultScene`'s asynchronous reward loading: a scene-owned run guard.
- Reconciliation of the lifecycle wording in the two `.ai/skills/` documents that
  mention `shutdown`.
- Browser verification of the full post-result flow and of the scene teardown
  behaviour.

### Out of Scope (unchanged, verified not required)

- `GameRuntime.emit()` / `emit()`-level exception swallowing — the fix is
  lifecycle cleanup at the source (task constraint 1).
- Navigation semantics, `PLAY AGAIN` / `MAIN MENU`, preserved-loadout
  architecture, and active-battle cleanup semantics (TASK-203/TASK-204).
- Backend, REST, SignalR, Redis, PostgreSQL, authentication, battle protocol.
- `node_modules` / the Phaser version.
- `docs/` source-of-truth documents: after this fix, `ARCHITECTURE.md` §2.2.3
  rule 1's "discarded when the scene shuts down" is **true**, so no correction to
  it is required (`AGENTS.md` §17).

---

## Current State

Each of the four scenes declared `shutdown()` with real cleanup, and each was
driven by name in the unit suites (`runScene(scene, ctx, 'shutdown')`) — a
lifecycle the engine does not have. `ResultScene.create()` started `loadRewards()`
with no run identity, and its only protection was the `?.` on a reference the
(never-invoked) teardown would have nulled.

---

## Acceptance Criteria

- [x] `MainMenuScene` cleanup is attached to actual Phaser lifecycle.
- [x] `LobbyScene` cleanup is attached to actual Phaser lifecycle.
- [x] `CollectionViewerScene` cleanup is attached to actual Phaser lifecycle.
- [x] `ResultScene` cleanup is attached to actual Phaser lifecycle.
- [x] ResultScene async reward loading cannot update destroyed UI (and cannot
      write into a later run's presentation).
- [x] Scene reuse does not accumulate listeners/subscriptions.
- [x] No global exception swallowing was introduced.
- [x] No navigation/gameplay behavior changed.
- [x] Existing preserved-loadout behavior remains intact.
- [x] Unit tests cover real lifecycle events (`SHUTDOWN` / `DESTROY`), not
      `scene.shutdown()`.
- [x] `npx tsc --noEmit`, `npx vitest run`, `npm run build`, `git diff --check`
      all pass.
- [x] Real-browser E2E passes, including the repo-wide teardown checks.
- [x] Lifecycle documentation no longer implies that a custom `shutdown()` method
      is automatically called by Phaser.

---

## Affected Files & Areas

```text
[x] src/frontend/client/src/game/scenes/MainMenuScene.ts
        — lifecycle registration; teardown doc (ownership audit)
[x] src/frontend/client/src/game/scenes/LobbyScene.ts
        — lifecycle registration; teardown/class docs
[x] src/frontend/client/src/game/scenes/CollectionViewerScene.ts
        — lifecycle registration; teardown/class docs
[x] src/frontend/client/src/game/scenes/ResultScene.ts
        — lifecycle registration, `rewardLoadRun` guard, teardown docs
[x] src/frontend/client/src/game/scenes/BattleScene.ts
        — registration made idempotent (2 lines + comment); teardown body
          unchanged (see Implementation Notes)
[x] src/frontend/client/tests/SceneLifecycle.test.ts
        — engine-event harness support, TASK-205 lifecycle suite, ResultScene
          async-safety suite, source pins for all five scenes
[x] src/frontend/client/tests/LobbyScene.test.ts
        — harness models `scene.events`; teardown suite drives engine events
[x] src/frontend/client/tests/CollectionViewerScene.test.ts
        — same, plus the "no attached teardown keeps everything" defect shape
[x] src/frontend/client/tests/ResultScene.test.ts
        — teardown tests drive the engine event
[x] src/frontend/client/scripts/standalone-web-smoke.mjs
        — scene-lifecycle snapshot and repo-wide teardown checks
[x] src/frontend/client/scripts/collection-viewer-smoke.mjs
        — viewer teardown/reopen checks
[x] .ai/skills/client/phaser-architecture/SKILL.md
        — methods-vs-events section, idempotent registration, cancel async
          continuations
[x] .ai/skills/phaser/scenes/SKILL.md
        — gotchas 10 and 14 state that `shutdown`/`destroy` are events
[ ] src/backend/, docs/, node_modules/                — NOT changed
```

---

## Implementation Notes

**The lifecycle mechanism.** Every scene registers the same pair in `create()`,
before the resources it releases are created:

```ts
this.events.off(Phaser.Scenes.Events.SHUTDOWN, this.shutdown, this);
this.events.off(Phaser.Scenes.Events.DESTROY, this.shutdown, this);
this.events.once(Phaser.Scenes.Events.SHUTDOWN, this.shutdown, this);
this.events.once(Phaser.Scenes.Events.DESTROY, this.shutdown, this);
```

- The `once` pair is TASK-204's approved pattern; the `off` pair in front of it is
  what makes the *registration* idempotent. `once` alone consumes the handler that
  fired, but the run's **unfired** `DESTROY` handler stays on the emitter, so a
  scene that is stopped and started again (which is what `PLAY AGAIN` does to
  `LobbyScene` and `BattleScene`) would stack one more handler per run. Detaching
  first is the repository's own idempotency technique
  (`BattleScene.registerBoardInput` documents the same move), and `DESTROY` is
  terminal, so nothing else can interfere.
- `BattleScene` (fixed by TASK-204) was brought to the same idempotent form — two
  lines and a comment, no change to its teardown body or to any behaviour it
  established. It is the only file touched outside the four listed scenes, and it
  is reported here rather than silently expanded (`AGENTS.md` §16).
- Teardown bodies are unchanged: they release only what each scene owns —
  subscriptions, timers/tweens, object lists, per-run guards, scene-local caches.
  Nothing clears the preserved loadout carrier (ADR-022), the runtime's
  synchronized battle copy, or any other layer's state.
- Every teardown is idempotent and safe against already-destroyed children:
  Phaser's `DisplayList#shutdown` handles the same event and destroys the scene's
  game objects *before* the scene's own handler runs, and `GameObject#destroy`
  returns early once the object has released its scene.

**ResultScene's asynchronous reward loading.** A per-presentation run counter is
the smallest scene-owned guard that closes both windows:

```text
create()   → const rewardRun = ++this.rewardLoadRun;  void this.loadRewards(rewardRun)
shutdown() → this.rewardLoadRun++;                    (+ drop the run's references)
loadRewards(run) → after the await: if (run !== this.rewardLoadRun) return;
```

- Teardown invalidates an in-flight read, so it cannot write to the destroyed
  `Text` objects of the run that started it.
- `create()` invalidates the previous run's read, so a stale delivery cannot
  overwrite the **live** reward area of a later presentation — which the
  null-guard alone cannot prevent.
- No cancellation registry, controller, or global state was introduced.

---

## Completion Evidence

### Changed Files

- `src/frontend/client/src/game/scenes/{MainMenu,Lobby,CollectionViewer,Result,Battle}Scene.ts`
  — the idempotent lifecycle registration (with the engine behaviour documented at
  each call site), the teardown doc comments, and `ResultScene`'s `rewardLoadRun`
  guard.
- `src/frontend/client/tests/SceneLifecycle.test.ts` — the harness's `Text` mock
  now records (and, by default, still throws on) a write to a destroyed object, so
  an asynchronous stale write is observable; new suites "Phaser scene lifecycle
  consistency" (6 tests) and "ResultScene — asynchronous reward loading" (4 tests);
  the MainMenu / CollectionViewer / Lobby teardown tests now raise the engine's
  events; the source pin covers all five scenes.
- `src/frontend/client/tests/LobbyScene.test.ts`,
  `src/frontend/client/tests/CollectionViewerScene.test.ts` — the harnesses model
  `scene.events` and the engine's teardown (`DisplayList` destruction, then
  `SHUTDOWN`); the teardown suites were rewritten to drive the engine event and
  were extended with DESTROY, reuse/no-accumulation, and defect-shape tests.
- `src/frontend/client/tests/ResultScene.test.ts` — its two teardown tests drive
  the engine event and assert the released references.
- `src/frontend/client/scripts/standalone-web-smoke.mjs` — a scene-lifecycle
  snapshot (each scene's own teardown handlers counted by function identity, plus
  the state only its teardown releases) and five new checks in phases 7–8.
- `src/frontend/client/scripts/collection-viewer-smoke.mjs` — two new checks: the
  viewer's teardown is registered on the engine events, and a reopen after a real
  shutdown rebuilt its shell without stacking or accumulating handlers.
- `.ai/skills/client/phaser-architecture/SKILL.md` — new §5 "Scene Lifecycle:
  Methods vs Events"; the cleanup pattern now shows the idempotent registration and
  four teardown rules (including cancelling asynchronous continuations); the
  Do/Don't table no longer talks about "on scene shutdown".
- `.ai/skills/phaser/scenes/SKILL.md` — gotchas 10 and 14 now state that
  `shutdown`/`destroy` are events on `scene.events`, that Phaser never calls a
  scene method by those names, and that a restartable scene detaches before
  attaching.

### Validation Results

```text
npx tsc --noEmit                               PASS — clean
npx vitest run  (full frontend suite)          PASS — 686 tests, 19 files
npm run build  (tsc && vite build)             PASS
git diff --check                               PASS — no whitespace errors
npm run verify:e2e:smoke  (2 runs)             PASS — 58 checks each, 0 failures
npm run verify:e2e:collection  (2 runs)        PASS — 42 checks each, 0 failures
```

### Regression Proof

Removing the fix makes the new tests fail, in the expected places, and restoring
it makes them pass again:

```text
registration removed (all five scenes)
  → 37 failures across the four scene suites, including
    "ResultScene — asynchronous reward loading > does not write to the
     presentation when the reward read settles after SHUTDOWN"
    (a write was attempted against a Text the engine had destroyed) and the
    Lobby/CollectionViewer reuse and DESTROY tests
run guard removed (ResultScene only)
  → "does not write the ended run's rewards into a later run's presentation"
    fails; the destroyed-UI test still passes, which is exactly why the guard,
    not the null-reference, is what closes the reuse window
```

### Browser E2E Result (the acceptance gate)

```text
MainMenu → Lobby → Battle 1 → Result → PLAY AGAIN → Lobby(restored)
   → edit Boss → Battle 2 (new battleId) → Result → MAIN MENU → MainMenu
```

Verified in a real headless Edge against the live backend and Vite dev server,
twice, with zero uncaught exceptions and zero fatal console errors. The new
lifecycle checks, read from the live scenes in the browser:

```text
phase7.stoppedScenesRanTheirTeardown
  MainMenu  hasTransitioned=false          (its teardown ran)
  Lobby     selectedPetId=null             (ARCHITECTURE.md §2.2.3 rule 1 holds)
  Battle    outcomeHandled=false           (fresh for the next battle)
phase7.everySceneRegisteredItsTeardownOnce
  active ResultScene  shutdown=1 destroy=1;   stopped scenes  shutdown=0 destroy=1
phase8.noDuplicateSceneSubscription
  Lobby run 1 = {shutdown:1, destroy:1};  Lobby run 2 = {shutdown:1, destroy:1}
phase8.resultSceneTeardownReleasedItsPresentation
  ResultScene stopped: resultData=null, rewardText=null
phase8.noStaleSceneSubscriptions
  MainMenu active 1/1; Lobby, Battle, Result stopped 0/1;
  CollectionViewerScene (never entered in this flow) 0/0
collection-viewer run
  phase7.reopenRanTheSceneTeardown: shellObjects 5 → 5, handlers 1/1 → 1/1
```

Also unchanged and re-verified: the preserved loadout survives and is editable,
Battle 2 has its own identity and its own 64-cell board, both post-result exits
work, and the runtime subscriber counts are identical for both battles.

### Server Authority & Scope Verification

- [x] Zero client-authoritative gameplay logic introduced
- [x] Zero backend / REST / SignalR / Redis / database change
- [x] Zero game rule, balance, progression, or content change
- [x] Zero change to TASK-202's decisions, ADR-022's carrier, or the runtime port
- [x] Zero `GameRuntime.emit()` / global exception-swallowing change
- [x] Zero `docs/` change (no source-of-truth correction was needed)
- [x] Zero `node_modules` change
- [x] No scope gate triggered: no lifecycle abstraction, no global scene
      lifecycle manager, no GameRuntime change, no Phaser patch, no loadout
      architecture change, no backend change, no product-behaviour change, no ADR

---

## Residual Findings (reported, not fixed here — `AGENTS.md` §16)

1. **`BattleScene`'s registration was made idempotent** (2 lines + comment) so the
   repository has one lifecycle-registration pattern and criterion 6 holds for
   every reusable scene. Its teardown body and TASK-204's verified behaviour are
   unchanged; this is the only change outside the four scenes the task listed.
2. **A scene whose teardown does not null a reference is still exposed** to a
   stale asynchronous write. `ResultScene` is the only scene that starts an
   asynchronous read and it is now guarded; a future scene that adds one should
   follow the rule recorded in `.ai/skills/client/phaser-architecture` §"Resource
   Lifecycle & Cleanup" rule 4.
3. **`CollectionViewerScene`'s teardown runs after the engine has destroyed its
   children**, which is the engine's order for every scene. It is safe because
   `GameObject#destroy` is a no-op once the object has released its scene; any
   future teardown that writes to (rather than drops) a destroyed object would
   still throw, and must not.
