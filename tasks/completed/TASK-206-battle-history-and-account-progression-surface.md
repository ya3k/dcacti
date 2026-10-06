# TASK-206 — Battle History & Account Progression Surface

---

## Metadata

```text
Task ID:           TASK-206
Type:              FEATURE (TASK_TYPES.md — a new read-only client presentation
                   surface over an already-frozen endpoint;
                   development/feature.md)
Status:            DONE
Risk:              LOW (client presentation, one transport read, one runtime
                   port capability, one scene; no server contract, no gameplay,
                   no progression rule, no state-model change)
Priority:          MEDIUM (Player Level / XP was only inspectable on ResultScene;
                   completed battles were not inspectable at all)
Primary Agent:     client (TASK_TYPES.md §5 — Frontend/Phaser; AGENT_SELECTION.md)
Supporting Agents: testing, review
Workflow:          development/feature.md
Skills:            client/phaser-architecture, phaser/scenes,
                   client/client-state-authority,
                   testing/test-scenario-generation, quality/scope-validation
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-164 (frozen `API_CONTRACTS.md` §4.5 contract), TASK-163
                   (the endpoint's response contract), TASK-149 (the §4 result
                   read and the persisted `RewardSummary` presentation), TASK-190
                   (the read-only viewer precedent and its approved layout),
                   TASK-205 (the scene-lifecycle and async-guard pattern)
Blocks:            None
```

**Lifecycle note.** `tasks/README.md` §4: the status field reflects the state, and
the only file move was into `completed/`. This task arrived out of band as an
execution manifest rather than as a `backlog/` file; its record is filed here so
the read-only-surface chain (`TASK-149` → `TASK-190` → `TASK-206`) stays
traceable (`tasks/README.md` §5), exactly as `TASK-204`'s and `TASK-205`'s were.

---

## Objective

Give the player a persistent, read-only place to inspect completed battles and the
latest delivered Player XP / Level after leaving `ResultScene`, reading the frozen
`GET /api/battle/history` endpoint (`API_CONTRACTS.md` §4.5) through the existing
runtime-port boundary.

---

## Authoritative References

- `docs/02-technical/API_CONTRACTS.md` §4.5 — the frozen history contract (bare
  array, the §4 element shape plus `completedAt`, the `CompletedAt` DESC /
  `BattleResultId` DESC ordering clients MAY rely on, no pagination/filter/sort,
  `200 []` for empty history, session-scoped `PlayerId`, REST-only) and §4 / §6
  for the shared element members and the error envelope. §2.3 is the application
  session mechanism the endpoint's `401 UNAUTHENTICATED` comes from.
- `docs/02-technical/ARCHITECTURE.md` §2.2.1 (runtime port boundary; scenes never
  reach the transport), §2.2.3 rules 1–3 and 6 (ephemeral scene-local state,
  port-carried capabilities, no store/manager), §5 item 5 (no heavy client state
  management).
- `docs/02-technical/DATABASE.md` §1 — the persisted `BattleResult` row the
  endpoint projects, the `RewardSummary` member list and its `null` semantics,
  and `CompletedAt` / `DurationTurns` sourcing.
- `docs/00-overview/GDD.md` §2.1 (result-screen exits; no automatic advance) and
  §14 (Player XP / Level is persistent account progression).
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` (the client
  renders, never computes), ADR-015 / ADR-020 (session-derived identity),
  ADR-022 (battle result data is read on demand, stored as no state, and is not
  the preserved-loadout carrier).
- `tasks/completed/TASK-190-collection-viewer-ui-scene.md` — the approved
  read-only surface conventions this scene follows.
- `tasks/completed/TASK-205-phaser-scene-lifecycle-consistency.md` — the scene
  lifecycle events and the scene-owned async run guard.

---

## Scope

### In Scope

- One `ApiService` read for the frozen route, reusing the authenticated `get<T>()`
  path.
- One runtime-port capability (`getBattleHistory()`), implemented by delegating to
  `ApiService`.
- One client wire model for the §4.5 element, reusing the §4 result model, the
  outcome value set, and the `RewardSummary` model.
- One Phaser presentation scene (read-only): Player Level / XP from the newest
  delivered element, one block per delivered battle in the delivered order, an
  explicit empty state, an error state with `RETRY`, and `< BACK`.
- A third `MainMenuScene` navigation action that leaves the two existing entries
  untouched.
- Unit tests for every one of those behaviours, and browser E2E coverage of the
  full player journey over real, server-resolved battles.

### Out of Scope (unchanged, verified not required)

- `src/backend/`, the REST contract, SignalR, Redis, PostgreSQL, migrations,
  authentication/session semantics, gameplay, progression formulas, rewards.
- Pagination, filtering, search, replay, battle mutation, equip/loadout actions,
  upgrades.
- Any new global state system, store, singleton, cache, or state-management
  library; `GameRuntimeState` gained no member.
- Any new architecture boundary or ADR; `docs/` source-of-truth documents were
  not modified (see "Residual Findings").
- The unrelated stale `ROADMAP.md` wording (explicitly out of scope).

---

## Implementation Notes

**One element model, no duplicates.** `BattleHistoryItemResponse` extends
`BattleResultResponse` and adds exactly `completedAt` — §4.5 note 2 makes the
element a §4 result object plus one further member, so `BattleOutcome` and
`RewardSummaryResponse` are reused rather than restated, and no
history-specific reward or outcome shape exists (§4.5 note 12).

**One authenticated transport path.** `ApiService.getBattleHistory()` builds the
single documented path with no query parameter (§4.5 notes 5–6) and carries no
identity (§4.5 note 8), returning the array exactly as delivered — no sorting,
reversing, filtering, or `completedAt` interpretation.

**One port capability.** `GameRuntimePort.getBattleHistory()` is documented as a
capability, not a model; `GameRuntime` delegates to `ApiService` and adds no
orchestration, cache, or state. Scenes never import `ApiService`, `fetch`, or a
SignalR client.

**One scene.** `BattleHistoryScene` renders the newest delivered element's Player
track as the account display and one block per delivered element: `Battle #n`,
`Outcome`, `Duration`, `Completed At`, and the `REWARDS` block. The reward
rendering moved to `game/presentation/RewardSummaryFormat.ts` so `ResultScene`
and this scene present the same eight delivered members the same way instead of
each carrying its own copy; `ResultScene`'s rendering is otherwise unchanged.

**Lifecycle and async safety follow TASK-205 exactly.** `create()` registers the
idempotent `off` + `once` pair for `Phaser.Scenes.Events.SHUTDOWN` / `DESTROY`
before building anything; the teardown destroys the scene's own objects, detaches
its handlers, drops the loaded history, and advances a scene-owned
`historyLoadRun` counter. `loadHistory(run)` re-checks that counter after the
await, so a read that settles after teardown — or after the instance was reused
— mutates nothing, including the error path. No global, cancellation, or
registry mechanism was introduced.

**The history is ephemeral presentation state.** It lives in a plain scene field
and is released by that scene; nothing was added to `GameRuntimeState`, the
synchronized battle copy, browser storage, or any module-level slot
(`ARCHITECTURE.md` §2.2.3 rules 1–2, ADR-022 D3).

**Main menu.** `BATTLE HISTORY` is drawn with the existing button pattern at
`(640, 464)` — one button pitch below `COLLECTION` — so the two existing entries
keep their own centres and the E2E click coordinates for `START BATTLE`
(`640, 324`) are unchanged.

**No client-side authority.** No XP is summed, no Level is derived, no
`leveledUp` flag is recomputed, no `durationTurns` is re-counted, no
`completedAt` is parsed or localised, no order is inferred, and no victory/defeat
semantics is decided; a `null` member renders as unavailable rather than `0`.

---

## Acceptance Criteria

- [x] Main Menu exposes Battle History.
- [x] Battle History loads through `GameRuntimePort`.
- [x] Scene never directly accesses transport.
- [x] API contract remains unchanged.
- [x] History renders server-delivered values verbatim.
- [x] Newest-first ordering is preserved exactly as returned.
- [x] Empty history has an explicit empty state.
- [x] No fabricated Player Level / XP.
- [x] Error state works.
- [x] `RETRY` works (a fresh request).
- [x] `BACK` works.
- [x] Player Level / XP remains inspectable after leaving `ResultScene`.
- [x] Second completed battle appears.
- [x] Both battles are displayed correctly.
- [x] Lifecycle cleanup uses Phaser SHUTDOWN / DESTROY.
- [x] Lifecycle registration is idempotent.
- [x] Async stale-read guard exists.
- [x] No stale async callback mutates destroyed/reused UI.
- [x] No new global state system.
- [x] Existing gameplay flow remains unchanged.
- [x] Existing 58 main E2E checks remain passing (now 66 per run: 58 + 8 new).
- [x] Existing 42 collection E2E checks remain passing.
- [x] New Battle History E2E checks pass (51 per run).
- [x] Zero uncaught exceptions. Zero fatal console errors.
- [x] TypeScript, Vitest, production build, and `git diff --check` pass.

---

## Affected Files & Areas

```text
[x] src/frontend/client/src/services/api/BattleModels.ts
        — `BattleHistoryItemResponse` (§4.5 element = §4 result + completedAt)
[x] src/frontend/client/src/services/api/ApiService.ts
        — `getBattleHistory()` over the shared authenticated `get<T>()`; type re-export
[x] src/frontend/client/src/game/runtime/GameRuntimeEvents.ts
        — `GameRuntimePort.getBattleHistory()` capability
[x] src/frontend/client/src/game/runtime/GameRuntime.ts
        — delegation to `ApiService`, no cache, no state
[x] src/frontend/client/src/game/presentation/RewardSummaryFormat.ts
        — NEW: the presentation layer's one `RewardSummary` rendering
[x] src/frontend/client/src/game/scenes/BattleHistoryScene.ts
        — NEW: the read-only Battle History & account-progression scene
[x] src/frontend/client/src/game/scenes/ResultScene.ts
        — uses the shared reward formatter (same rendering; no behaviour change)
[x] src/frontend/client/src/game/scenes/MainMenuScene.ts
        — third navigation action + `BATTLE HISTORY` button at (640, 464)
[x] src/frontend/client/src/game/GameConfig.ts
        — `BattleHistoryScene` registered as a read-only side branch
[x] src/frontend/client/tests/BattleHistoryScene.test.ts
        — NEW: normal / empty / error / retry / navigation / lifecycle / async race /
          boundary & authority (34 tests)
[x] src/frontend/client/tests/BattleService.test.ts
        — `ApiService.getBattleHistory` suite: URL, no query parameter, session
          header, delivered order, element shape, `200 []`, null members, 401 /
          500 / network propagation (11 tests)
[x] src/frontend/client/tests/GameRuntime.test.ts
        — history-port delegation, order preservation, no computation, `[]`,
          rejection, no state held, no transport operation (8 tests)
[x] src/frontend/client/tests/SceneLifecycle.test.ts
        — registration list, main-menu coordinates/overlap, BATTLE HISTORY
          navigation, the scene's own suite, and its entry in the TASK-205
          lifecycle-consistency matrix (source pin included)
[x] src/frontend/client/tests/RuntimeBoundaries.test.ts
        — `BattleHistoryScene.ts` in the scene boundary list; the port's
          capability list and its type-only battle-model import updated
[x] src/frontend/client/tests/PhaserGame.test.tsx
        — registered scene list
[x] src/frontend/client/scripts/standalone-web-smoke.mjs
        — fresh-account Battle History phase (empty state, no fabricated
          Level/XP, not an error state, teardown ran, authenticated `200`), plus
          the scene in the lifecycle snapshots and `ACTIVE_SCENE`
[x] src/frontend/client/scripts/battle-history-smoke.mjs
        — NEW harness: the required flow over real, server-resolved battles
[x] src/frontend/client/package.json
        — `verify:e2e:history`
[ ] src/backend/, docs/, node_modules/                — NOT changed
```

---

## Completion Evidence

### Validation Results

```text
npx tsc --noEmit                               PASS — clean
npx vitest run  (full frontend suite)          PASS — 751 tests, 20 files
npm run build  (tsc && vite build)             PASS
git diff --check                               PASS — no whitespace errors
npm run verify:e2e:smoke  (2 runs)             PASS — 66 checks each, 0 failures
npm run verify:e2e:collection  (2 runs)        PASS — 42 checks each, 0 failures
npm run verify:e2e:history  (2 runs)           PASS — 51 checks each, 0 failures
```

The 66 main checks are the 58 pre-existing checks (all still present, by name)
plus 8 new `phase3b.*` checks; the 42 collection checks are unchanged.

### Browser E2E Result (the acceptance gate)

Verified in a real headless Edge against the live backend and Vite dev server,
twice, with zero uncaught exceptions and zero fatal console errors.

```text
verify:e2e:smoke (main journey, fresh account per run)
  phase3b.battleHistoryOpened
  phase3b.emptyStateShown            status="No battles yet", entries=[]
  phase3b.noFabricatedLevelOrXp      progression="—" (no Level/XP anywhere)
  phase3b.emptyIsNotAnErrorState     controls=["< BACK"] (no RETRY)
  phase3b.teardownRegisteredOnEngineLifecycleEvents  1/1
  phase3b.historyReadWasAuthenticated200             GET /api/battle/history → 200
  phase3b.backReturnedToMainMenu
  phase3b.stoppedHistorySceneRanItsTeardown          historyLength=0, shellObjects=0,
                                                     progressionText=null,
                                                     shutdown=0, destroy=1
  (the pre-existing 58 checks all still pass, including both existing menu entries
   at (640, 324) and (640, 394) and the unchanged battle/post-result lifecycle)

verify:e2e:history (the required flow, real battles, fresh account per run)
  phase2.*    three entries; BATTLE HISTORY at (640,464); START BATTLE (640,324) and
              COLLECTION (640,394) unmoved; no overlap
  phase3.*    empty history on the fresh account; explicit empty state; no
              fabricated Level/XP; not an error state; teardown wired; BACK works
  phase5.*    battle 1 played to its own terminal resolution by real cell taps
              (12–17 swaps) → the server's own DEFEAT → ResultScene read the
              persisted result (§4) → `/api/battle/history` returned exactly one
              element whose members are the §4 shape plus `completedAt`, with
              `rewards` / `durationTurns` identical to the result route's
  phase6.*    the completed battle is visible with Outcome / Duration /
              Completed At / Player / Pet rendered verbatim, the account line is
              the newest delivered element's own Player track, and the rendered
              order equals the delivered order
  phase7.*    battle 2 is a new battle (new battleId) and reaches its own terminal
              resolution; `/api/battle/history` returns both, newest first
  phase8.*    both battles are visible newest-first, every value rendered verbatim,
              and the account line is the newest element's
  phase9.*    zero uncaught exceptions, zero fatal console errors, zero unexpected
              API responses, and every history request was a bare authenticated GET
```

### Regression Proof

- The pre-existing 58 main checks and 42 collection checks were re-run unchanged
  and all pass; the new checks are additive.
- The new unit suites cover the defect shapes directly: a write to an
  engine-destroyed `Text` after teardown, a stale delivery into a *reused* run's
  presentation, a stale failure painting a later run's error state, handler
  accumulation across scene reuse, and a duplicate `RETRY`.
- The new scene is included in the repo-wide TASK-205 lifecycle matrix
  (registration on the engine events, teardown on both, idempotence, no
  accumulation across reuse, source pin), so a future scene cannot regress it.

### Scope & Authority Verification

- [x] Zero client-authoritative gameplay logic: no XP/Level/`leveledUp`/
      `durationTurns`/`completedAt`/ordering/semantics computation
- [x] Zero backend / REST / SignalR / Redis / database / migration change
- [x] Zero API contract change; §4.5 consumed exactly as frozen
- [x] Zero change to `GameRuntimeState`, the synchronized battle copy, the
      preserved-loadout carrier (ADR-022), or the transport services
- [x] Zero `docs/` change
- [x] Zero new state-management library, store, singleton, or global cache
- [x] No scope gate triggered: no new endpoint, no new field, no new boundary,
      no new ADR

---

## Residual Findings (reported, not fixed here — `AGENTS.md` §16)

1. **`MVP_SCOPE.md` §1 does not name a battle-history surface**, and `ROADMAP.md`
   does not mention one either; §4 of that document treats an unlisted feature as
   FUTURE by default. The endpoint itself was already approved and frozen by
   `TASK-163` / `TASK-164` (`API_CONTRACTS.md` §4.5) and this task is explicitly
   the client surface for it, so no scope gate fired — but the MVP scope
   document's §1 list is now behind the implemented client surface and should be
   reconciled by a documentation task.
2. **`GDD.md` §2.1** still says the main menu is "where the battle and collection
   entry points live", which predates the third entry point. `ARCHITECTURE.md`
   §2.2.3 describes the read-only side-branch pattern generically and remains
   accurate.
3. **`API_CONTRACTS.md` cross-references to `§2.8` are stale** — the application
   session mechanism is §2.3 in the current document — including §4.5 note 7,
   which this task's contract reading relies on. The task itself listed "§2.8" as
   required reading, which is the same staleness. `docs/` was left untouched per
   this task's scope; a small reference correction is the follow-up.
4. **Harness requirement discovered while verifying:** a page driven purely over
   CDP is not focused by default, and Phaser's `TimeStep` skips its updates while
   the window is blurred, which silently freezes battle input mid-fight. The new
   harness enables `Emulation.setFocusEmulationEnabled` for that reason. This is a
   headless-harness concern, not a product defect; the older harnesses only need
   one or two Swaps per battle and are unaffected.
5. **`STALE_ACTION` is a normal answer, not a hang.** `MATCH3_RULES.md` §2.1.4
   rejects a repeat of the most recently committed pair, so a driver that
   resubmits the same pair on an unchanged board would loop forever. The new
   harness remembers the rejected pair per board and moves on; a future automated
   battle driver must do the same.
6. **A history longer than the safe area** is presented with a "showing the N
   newest of M battles" notice rather than silently clipped, because §4.5 note 5
   makes the delivered array the complete history and MVP defines no pagination.
