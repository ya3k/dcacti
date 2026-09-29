# TASK-078 — Implement Client LobbyScene Match-Start Trigger

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
-->

---

## Metadata

```text
Task ID:           TASK-078
Type:              FEATURE
Status:            DONE
Risk:              LOW
Priority:          HIGH
Primary Agent:     client
Supporting Agents: N/A
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, phaser/scenes, client/phaser-architecture, client/client-state-authority, testing/test-scenario-generation, quality/scope-validation
Dependencies:      TASK-075, TASK-076, TASK-077, TASK-080, TASK-081, TASK-082, TASK-083, TASK-084, TASK-085
```

---

## Objective

Implement `LobbyScene` as the client presentation layer for the MVP pre-battle
selection flow — Choose Pet, Equip Cards, Equip Relics, loadout Review, Start
Battle (`GDD.md` §2; loadout review per `TDD.md` §2.1) — which reads the owned collection only through
`GameRuntimePort`, keeps the in-progress selection as ephemeral scene-local
state (`ARCHITECTURE.md` §2.2.3), constructs a `BattleStartRequest`
(`API_CONTRACTS.md` §3) from that selection with the fixed MVP Boss
(`boss-hoa-long`), delegates execution to `GameRuntimePort.startBattle`
(`TASK-077`, `ARCHITECTURE.md` §2.2.3 rule 5), and on successful start
transitions to `BattleScene`.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 (IN — MVP: Pets, Cards, Relics, Bosses, Combat) and §2 (OUT — Explicitly Excluded from MVP) — scope classification for this feature
- `docs/00-overview/GDD.md` §2 — pre-battle selection steps (Enter Battle → Choose Pet → Equip Cards → Equip Relics → Start Battle)
- `docs/02-technical/TDD.md` §2.1 — Phaser scene lifecycle (incl. the MVP staging note), `LobbyScene` ownership, and the no-MVP-Boss-selection rule
- `docs/02-technical/ARCHITECTURE.md` §1, §2.2, §2.2.1, §2.2.3 — frontend layers and the pre-battle selection boundary (rules 1–6: ephemeral scene-local selection, transport only through the port, collection read as selection source, server-side validity, capabilities-not-models)
- `docs/02-technical/API_CONTRACTS.md` §3 — `POST /api/battle/start` contract and server-side validation; §5 (§5.1–§5.6) — collection read models; §2.8 — authentication errors
- `docs/01-game-design/PET_RULES.md` §2, `docs/01-game-design/CARD_RULES.md` §1, `docs/01-game-design/RELIC_RULES.md` §2 (§2.3 equip-slot order) — loadout constraints, enforced by the server
- `docs/01-game-design/BOSS_RULES.md` §6.4 — canonical technical Boss Identity (`boss-hoa-long`)
- `docs/02-technical/DATABASE.md` §2 — MVP starter ownership composition
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — server-authoritative architecture
- `docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md` — Phaser scene lifecycle and runtime boundary
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` — loadout and ownership boundaries

---

## Product Owner Decisions Applied

```text
Boss (D4)      MVP battle start uses the fixed Boss ID "boss-hoa-long". No
               Boss selection UI, no Boss collection APIs, and no Boss
               selection state exist in MVP. LobbyScene supplies this bossId
               in the BattleStartRequest.

Scene order    The MVP implementation stages the documented lifecycle as
               BootScene → PreloaderScene → LobbyScene → BattleScene.
               MainMenuScene is NOT created by this task; the full documented
               lifecycle (TDD.md §2.1) remains the design target. The staged
               decision is recorded explicitly in TDD.md §2.1.

Selection      The full pre-battle selection flow (Choose Pet, Equip 3 Basic
scope          Cards, Equip 3–5 Relics, Review, Start Battle) is part of THIS
               task — there is no separate follow-up task. Selection state is
               ephemeral and scene-local (no React, no client-global
               persistence). Boundary: LobbyScene → GameRuntimePort →
               GameRuntime → ApiService.

Collection     LobbyScene reads the owned collection (Pets, Cards, Relics)
capability     only through runtime-port capabilities. It never imports
               ApiService, fetch, or SignalR.

Authentication Session/authentication is established by the completed
dependency     authentication tasks (ADR-015). TASK-078 establishes nothing;
               401 is handled as a documented error. TASK-036 is an
               independent credential-hygiene task and is not a dependency.
```

---

## Scope

### In Scope

1. **Runtime port capabilities** — add to `GameRuntimePort`
   (`src/frontend/client/src/game/runtime/GameRuntimeEvents.ts:226`):
   - `startBattle(request: BattleStartRequest): Promise<void>`
     (`ARCHITECTURE.md` §2.2.3 rule 5)
   - collection reads whose signatures match the existing `ApiService`
     methods (`src/frontend/client/src/services/api/ApiService.ts:132–207`):
     `getPets(): Promise<PetResponse[]>`, `getPet(petId: string): Promise<PetResponse>`,
     `getCards(): Promise<CardResponse[]>`, `getRelics(): Promise<RelicResponse[]>`

   `GameRuntime` implements these by delegating to the existing `ApiService`
   methods — no new orchestration logic. The port carries capabilities only:
   it defines, re-exports, and owns no collection read model, loadout, or
   selection type (`ARCHITECTURE.md` §2.2.3 rule 6).

2. **`LobbyScene` selection flow** (`src/frontend/client/src/game/scenes/LobbyScene.ts`, new):
   - Presents the pre-battle steps of `GDD.md` §2 (Choose Pet → Equip Cards →
     Equip Relics → Start Battle) together with the loadout review
     (`TDD.md` §2.1 — "Battle preparation, loadout review, and match start
     trigger") and the fixed Boss target, in
     logical game coordinates (1280 × 720).
   - Loads the owned Pet/Card/Relic collection through the runtime port only.
   - Keeps the in-progress selection as ephemeral, scene-local state,
     discarded on scene shutdown — no store, manager, or module of its own
     (`ARCHITECTURE.md` §2.2.3 rules 1–2, §5; `AGENTS.md` §9).
   - Builds the selection from the collection read; the player's choice order
     is the request order — for Relics, position *i* is equip slot *i + 1*
     (`RELIC_RULES.md` §2.3; `ARCHITECTURE.md` §2.2.3 rule 4).
   - Interaction design limits selection to one Pet, three Basic Card slots,
     and three-to-five Relic slots. The scene performs no loadout legality
     validation — ownership, counts, category, copy limit, and distinctness
     are server-validated (`API_CONTRACTS.md` §3; `ARCHITECTURE.md` §2.2.3
     rule 5).

3. **Request construction and start** — construct `BattleStartRequest`
   (`API_CONTRACTS.md` §3) from the selection:
   - `petId` = the selected owned Pet **instance** id taken from the loaded
     collection (not a Pet definition id, not hardcoded).
   - `bossId` = `"boss-hoa-long"` (`BOSS_RULES.md` §6.4, D4 above). No Boss
     picker and no Boss fetch.
   - `cardLoadout` = exactly 3 Basic Card ids from the owned collection
     (starter set `card-heal`, `card-shield`, `card-power-charge` —
     `DATABASE.md` §2, `CARD_RULES.md` §1).
   - `relicLoadout` = 3–5 distinct owned Relic **instance** ids
     (starter set `relic-berserker-core`, `relic-mana-crystal`,
     `relic-assassin-eye` — `DATABASE.md` §2). `relic-emergency-core` is
     provisioned but deliberately not starter-owned: it must never be
     presented or submitted unless the collection read returns it.
   - No Signature/PetSkill card member and no members beyond the documented
     four — the server derives/validates the rest (`API_CONTRACTS.md` §3).
   - Submit via `runtime.startBattle(request)`. The scene does NOT duplicate
     the REST → connect → join orchestration owned by
     `GameRuntime.startBattle` (`TASK-077`).

4. **Scene routing** — register `LobbyScene` in `GameConfig.ts`
   (`[BootScene, PreloaderScene, LobbyScene, BattleScene]`); `PreloaderScene`
   transitions to `LobbyScene`; on successful `startBattle` resolution
   `LobbyScene` transitions to `BattleScene`; on rejection it stays active
   with error feedback.

5. **Tests** — unit and integration coverage per Testing Requirements,
   including updates to the existing scene-list/scene-lifecycle assertions
   (`PhaserGame.test.tsx`, `RuntimeBoundaries.test.ts`,
   `SceneLifecycle.test.ts`) for the staged scene order.

### Out of Scope

- **Boss selection** — no Boss selection UI, no Boss collection APIs, no Boss
  selection state (D4 above). `LobbyScene` uses the fixed MVP `bossId` only.
- **Client authentication establishment** — session/auth is established by
  the completed authentication tasks (`ADR-015`); tests establish
  `ApplicationSession` directly. This task performs no Discord OAuth, token
  exchange, or credential storage. `TASK-036` (Discord credential/secret
  hygiene) is an independent task and is **not** a dependency of this task;
  a `401 UNAUTHENTICATED` response is handled as a documented error
  (`API_CONTRACTS.md` §2.8/§3).
- **`MainMenuScene` and `ResultScene`** — deferred to their own tasks (see
  Product Owner Decisions Applied, scene order).
- **Selection persistence** — no React, client-global, or persisted store for
  the in-progress selection.
- **New services or state containers** — no `LobbyService`, `LoadoutService`,
  `BattleStateManager`, or generic state store; `GameRuntime` does not hold
  the selection (`ARCHITECTURE.md` §2.2.3 rules 1–2, §5).
- **Server-side, contract, or ADR changes** — no backend code, no
  API/SignalR contract changes, no ADR modifications.
- **Client-authoritative logic** — no loadout-validity judgment, gameplay
  effect, damage, or combat calculation (`AGENTS.md` §10, `ADR-001`).
- **Full visual art / sprite asset pipelines** — Phaser text, shapes, and
  placeholder layouts in logical game coordinates.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State

`GameRuntime.startBattle` exists (`TASK-077`, `GameRuntime.ts:466`), but
`GameRuntimePort` (`GameRuntimeEvents.ts:226`) exposes neither `startBattle`
nor any collection read, so no scene can reach either. `ApiService` already
implements the collection reads (`getPets`/`getPet`/`getCards`/`getRelics`,
`TASK-075`, `ApiService.ts:132–179`) and battle start (`TASK-076`,
`ApiService.ts:207`). `GameConfig.ts` registers
`[BootScene, PreloaderScene, BattleScene]` and `PreloaderScene` transitions
directly to `BattleScene`, so the selection flow and `startBattle` have no
scene caller. The selection boundary is documented
(`ARCHITECTURE.md` §2.2.3, `TASK-081`), the selection surface is decided
(`TASK-080`), starter ownership is implemented (`TASK-083`, `TASK-084`), and
MVP content definitions are provisioned (`TASK-085`). Existing tests assert
the three-scene list (`PhaserGame.test.tsx:54`, `RuntimeBoundaries.test.ts:46`).

---

## Acceptance Criteria

- [x] `GameRuntimePort` defines `startBattle(request: BattleStartRequest): Promise<void>` and the collection reads `getPets`, `getPet`, `getCards`, `getRelics` with signatures matching `ApiService.ts:132–207`; `GameRuntime` implements them by delegation to the existing `ApiService` methods only, and the port itself defines no collection/loadout/selection types (`ARCHITECTURE.md` §2.2.3 rule 6).
- [x] `LobbyScene` exists, extends `Phaser.Scene`, and is registered in `GameConfig.ts`; the boot flow runs `BootScene → PreloaderScene → LobbyScene → BattleScene`, and no `MainMenuScene` is created.
- [x] `PreloaderScene` transitions to `LobbyScene` on asset-loading completion.
- [x] `LobbyScene` obtains the port via `readRuntime(this)` and behaves safely (error feedback, no unhandled exception) when no runtime is present.
- [x] `LobbyScene` presents the `GDD.md` §2 selection steps and limits interaction to exactly 1 Pet, 3 Basic Cards, and 3–5 distinct Relics; no Boss selection control, state, or API call exists.
- [x] The in-progress selection is scene-local only, is absent from runtime/global/React state, and is discarded on scene shutdown (`ARCHITECTURE.md` §2.2.3 rules 1–2).
- [x] `LobbyScene` reads Pets, Cards, and Relics only through `GameRuntimePort`; it imports neither `@microsoft/signalr`, nor `fetch`, nor `ApiService`, nor the Discord SDK.
- [x] The submitted `petId` is an owned Pet **instance** id taken from the loaded collection — never a Pet definition id and never hardcoded.
- [x] The submitted `cardLoadout` contains exactly 3 Basic Card ids from the owned collection, with no Signature/PetSkill card member.
- [x] The submitted `relicLoadout` contains 3–5 distinct owned Relic **instance** ids in player-chosen order (position *i* = equip slot *i + 1*); no Relic absent from the collection read (e.g. `relic-emergency-core`) is ever presented or submitted.
- [x] The submitted `bossId` is `"boss-hoa-long"` (`BOSS_RULES.md` §6.4), and the request carries exactly the `API_CONTRACTS.md` §3 members — no status/battleState/damage/combat/match fields.
- [x] Starting the battle calls `runtime.startBattle(request)` exactly once per player intent; the scene neither duplicates the `GameRuntime.startBattle` orchestration nor decides loadout validity.
- [x] Repeated activation of the start trigger while a request is pending results in exactly one `startBattle` call (scene-local in-flight guard; no request queue).
- [x] On successful `startBattle` resolution, `LobbyScene` transitions to `BattleScene`.
- [x] On rejection (`401 UNAUTHENTICATED`, `400 INVALID_LOADOUT` / `PET_NOT_OWNED` / `BOSS_NOT_FOUND`, transport failure), `LobbyScene` remains active, displays error feedback, keeps the selection intact for retry, and does not transition.
- [x] No client-authoritative gameplay logic, loadout-validity computation, or client-side RNG is introduced (`AGENTS.md` §10 / `ADR-001`).
- [x] All relevant tests pass at the required validation depth for LOW risk (`core/validation.md` §2: build + focused unit tests).
- [x] Quality review checklist passes (`quality/review.md` §1).
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / `ADR-001`).

---

## Affected Files & Areas

```text
[x] src/frontend/client/src/game/runtime/GameRuntimeEvents.ts (add capabilities to GameRuntimePort)
[x] src/frontend/client/src/game/runtime/GameRuntime.ts (implement new port capabilities via existing ApiService methods)
[x] src/frontend/client/src/game/scenes/LobbyScene.ts (new scene)
[x] src/frontend/client/src/game/scenes/PreloaderScene.ts (transition target → LobbyScene)
[x] src/frontend/client/src/game/GameConfig.ts (register LobbyScene)
[x] src/frontend/client/tests/ (new LobbyScene tests; GameRuntime.test.ts; scene-list assertions in PhaserGame.test.tsx, RuntimeBoundaries.test.ts, SceneLifecycle.test.ts)
[ ] src/backend/ (none)
[ ] docs/ (none — the staged scene-flow decision is already recorded in TDD.md §2.1)
```

---

## Implementation Notes

- Reference `readRuntime(this)` in `src/frontend/client/src/game/runtime/RuntimeRegistry.ts`
  for obtaining the `GameRuntimePort` inside the scene (returns
  `GameRuntime | null`).
- Follow the scene layout pattern in `BattleScene.ts`, using logical game
  constants from `GameViewport.ts` (`SAFE_AREA`, `GAME_WIDTH`, `GAME_HEIGHT`).
- The collection read is a **selection source, not a selection**
  (`ARCHITECTURE.md` §2.2.3 rule 4): the API defines no collection ordering
  (`API_CONTRACTS.md` §5.5) and returns no equip state (§5.6). Starter
  ownership (`DATABASE.md` §2) is a server fact — the UI presents what the
  collection read returns; it does not hardcode a "starter loadout" or a test
  pet id.
- `bossId` is one fixed MVP value (`"boss-hoa-long"`); there is no Boss
  picker, no Boss list fetch, and no Boss selection state anywhere in the
  scene.
- The in-flight guard is a scene-local flag; listeners, UI containers, and
  the selection state are cleaned up in `shutdown()` to prevent leaks across
  scene restarts. No pending request is queued.
- On a rejected start the scene stays active with its selection intact so
  the player can correct and retry (`ARCHITECTURE.md` §2.2.3 rule 5).

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — LobbyScene creation/lifecycle; collection load through a fake port; selection interactions constrained to 1 Pet / 3 Basic Cards / 3–5 distinct Relics; BattleStartRequest construction matching API_CONTRACTS.md §3 (member set, bossId, instance-id petId, card ids, distinct relic instance ids in chosen order); start issued exactly once through the port; success transition to BattleScene; rejection path (stays + error + retry); shutdown cleanup; no-runtime path.
[x] Integration tests  — port-capability delegation from GameRuntime to the existing ApiService methods (mocked); existing scene-list/scene-lifecycle assertions updated for the staged order (PhaserGame.test.tsx, RuntimeBoundaries.test.ts, SceneLifecycle.test.ts).
[x] Gameplay scenarios — N/A (presentation, selection interaction, and scene lifecycle only; no gameplay rules are exercised).
```

### Key Edge Cases

- `startBattle` rejects with `401 UNAUTHENTICATED` (`API_CONTRACTS.md` §2.8): scene remains on `LobbyScene`, displays an authentication error, re-enables the start trigger.
- `startBattle` rejects with `400 INVALID_LOADOUT` / `PET_NOT_OWNED` / `BOSS_NOT_FOUND` (§3): scene remains, displays the error, selection intact.
- Rapid double activation of the start trigger: exactly one in-flight `startBattle` call.
- No runtime available in the registry: error feedback without unhandled exceptions.
- Collection read returns an empty or partial set: the UI reflects exactly what was read; no hardcoded fallback loadout is submitted.
- Scene shutdown / restart: event listeners, UI containers, and selection state are cleaned up.

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If the task appears to require a Boss selection UI/API, a second Boss id, or client-held Boss data: STOP — D4 fixed the MVP Boss (`AGENTS.md` §7/§20)
- If task requires client-authoritative loadout validation or gameplay logic calculation: STOP per `AGENTS.md` §10
- If implementing requires Discord OAuth flows or secrets handling: STOP per `AGENTS.md` §7/§20 (authentication is out of scope; TASK-036 is independent)
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose

---

## Completion Evidence

### Changed Files

- `src/frontend/client/src/game/runtime/GameRuntimeEvents.ts` — `GameRuntimePort`
  gained the five documented pre-battle capabilities: `startBattle(request:
  BattleStartRequest): Promise<void>` and the collection reads
  `getPets`/`getPet`/`getCards`/`getRelics` (`ARCHITECTURE.md` §2.2.3 rules 3,
  5–6). The read models and the request type are imported **as types** from
  `services/api/BattleModels` / `services/api/CollectionModels`; the port
  declares, re-exports, and owns none of them.
- `src/frontend/client/src/game/runtime/GameRuntime.ts` — implements the four
  new port capabilities as pure delegation to the injected `ApiService`
  (`getPets` → `api.getPets()`, …). No orchestration, caching, ordering, or
  state is added: the runtime holds no collection and no selection.
- `src/frontend/client/src/game/scenes/LobbyScene.ts` — **new scene**. Presents
  `GDD.md` §2's steps (Choose Pet → Equip Cards → Equip Relics → Start Battle)
  plus the loadout Review and the fixed Boss target in logical 1280 × 720
  coordinates; loads the collection through the runtime port; holds the
  in-progress selection as scene-local fields cleared in `shutdown()`; builds
  the four-member `BattleStartRequest`; submits through
  `runtime.startBattle()` behind a scene-local in-flight guard; transitions to
  `BattleScene` only on successful resolution and stays put with error feedback
  (selection intact) on rejection.
- `src/frontend/client/src/game/scenes/PreloaderScene.ts` — transition target
  `BattleScene` → `LobbyScene`.
- `src/frontend/client/src/game/GameConfig.ts` — registers
  `[BootScene, PreloaderScene, LobbyScene, BattleScene]`; the stale
  "only the scenes the runtime foundation requires" comment is replaced by the
  staged-order statement `TDD.md` §2.1 now records.
- `src/frontend/client/tests/LobbyScene.test.ts` — **new**, 44 tests.
- `src/frontend/client/tests/GameRuntime.test.ts` — +8 tests for the new
  port-capability delegation (method-by-method delegation, `getPet` argument
  pass-through, no imposed ordering, rejection propagation, no collection state
  on the runtime).
- `src/frontend/client/tests/RuntimeBoundaries.test.ts` — scene list extended
  with `LobbyScene.ts` and a guard asserting that list equals the scene list
  `GameConfig.ts` actually registers; the scene transport rule extended to the
  REST client and the Discord SDK; a new "the runtime port carries
  capabilities, not models" assertion (exact port member set, no declared or
  re-exported model type); new per-file assertions that the runtime carries no
  Boss source beyond the fixed start value and equips/triggers/evaluates no
  Relic. The generic forbidden-term list drops `boss`/`relic` — the collection
  read is now a documented capability — with those two assertions added so the
  relaxation cannot hide a real violation.
- `src/frontend/client/tests/PhaserGame.test.tsx` — scene-list assertion updated
  to the four-scene staged order.
- `src/frontend/client/tests/SceneLifecycle.test.ts` — `PreloaderScene`
  transition assertions updated to `LobbyScene`.

No other file was changed. `src/backend/`, `docs/`, the API/SignalR contracts,
the ADRs, `TASK-036`, and every `tasks/completed/` file are untouched.

### Validation Results

```text
Unit + integration tests   PASS — `npm run test:run` (vitest) in
                           src/frontend/client: 375 tests / 16 files, 0
                           failures. Baseline before this task was 318 tests
                           / 15 files (TASK-077 recorded 315 at its own
                           completion), so this task adds 57 tests and removes
                           none.
Build/compile              PASS — `npx tsc --noEmit` exit 0; `npm run build`
                           (tsc && vite build) succeeded, emitting
                           dist/index.html + dist/assets (built in ~2.9s).
                           See the environment note below.
Type-check of test sources  PASS — `tsc` covers tests/ (no test file is
                           excluded), so the suite is type-checked too.
Backend suite              NOT RUN — no file under src/backend/ or
                           tests/backend/ was touched by this task.
```

**Environment note (pre-existing, not caused by this task).** On this host,
`vite build` fails with `[vite:esbuild-transpile] ... Access is denied` when
esbuild unlinks its transpile artifact under `%TEMP%`
(`C:\Users\ya3k\AppData\Local\Temp\esbuild-*`). The checkout's own
pre-existing `%TEMP%` ACL is the cause: the failure reproduces on a tree with
this task's frontend files stashed, and it reproduces on an empty
inline Vite config, so it is independent of TASK-078's changes. The build
**passes** when `TEMP`/`TMP` points at a writable directory. `tsc` (the build's
first stage, which is what validates this task's code) passes under the stock
command. Reported, not fixed: it is a host/ACL condition outside this task's
scope (`AGENTS.md` §16).

### Load-Bearing (Mutation) Verification

Each acceptance-critical property was verified by mutating the implementation
and confirming the suite fails, so the assertions are not tautological:

```text
Mutation                                             Result
───────────────────────────────────────────────────  ─────────────────────────
Remove LobbyScene's in-flight guard                  1 test FAILS
Hardcode `petId` instead of using the selection      2 tests FAIL
Sort `relicLoadout`                                  1 test FAILS
Submit a different `bossId`                          2 tests FAIL
Remove the 3-card interaction limit                  1 test FAILS
Transition before the start resolves                 7 tests FAILS
Transition on rejection instead of staying           6 tests FAILS
Keep the selection in `shutdown()`                   2 tests FAILS
Import `ApiService` into the scene                   2 tests FAILS
Sort the relic read in `GameRuntime.getRelics`       1 test FAILS
Replace a delegation with a constant (`getCards`)    1 test FAILS
Hardcode a fallback `petId` when nothing is chosen   3 tests FAILS
Port declares a `LoadoutSelection` type              1 test FAILS
Port re-exports a read model                         1 test FAILS
Port gains an undocumented `getBosses` capability    2 tests FAILS
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic — `LobbyScene` performs
      no damage, HP, Power, Match, Combo, cascade, passive, or RNG computation
      and contains no `Math.random`/`crypto`; it submits a request and renders
      the outcome. Its only local checks are interaction feedback (how many
      slots are filled, which is not a legality judgment) and the documented
      request cardinalities, which `API_CONTRACTS.md` §3 defines and which are
      required even to *form* the request. Ownership, category, copy limit,
      distinctness, and validity remain the server's answer.
- [x] Confirmed the in-progress selection is never client-authoritative state —
      it is scene-local, discarded on `shutdown()`, absent from
      `GameRuntimeState` and from `GameRuntime`, and asserted to be discarded by
      a mutation-verified test.
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — no new system,
      content, endpoint, or identifier; the Pet/Card/Relic identifiers in the
      task text are `DATABASE.md` §2 starter-ownership facts, and the scene
      hardcodes none of them (it submits only what the collection read returned).
- [x] Confirmed no Boss selection: one fixed `bossId` (`"boss-hoa-long"`,
      `BOSS_RULES.md` §6.4), no Boss picker, no Boss read, no Boss state — with a
      dedicated boundary assertion over the runtime files.
- [x] Confirmed no backend, API contract, SignalR, Redis, `BattleState`, ADR, or
      gameplay-rule change.
- [x] Confirmed `TASK-036`, `TASK-079`, and all `tasks/completed/` files are
      unmodified.

### Remaining Issues (report-only, per `AGENTS.md` §16)

1. **The scene cannot reach the battle transport end-to-end in production
   yet.** TASK-077 recorded that no production path wires Discord →
   `ApiService.authenticateDiscord` → `ApplicationSession` →
   `GameRuntime.setSessionStatus`; unchanged by this task (Task-078 declares it
   out of scope). A real start therefore reaches the documented
   `401 UNAUTHENTICATED`, which the scene correctly reports as error feedback
   while keeping the selection.
2. **`App.tsx` connects at mount, before any session exists**, diverging from
   `TDD.md` §2.1 item 4 (session before SignalR connect). Pre-existing
   (recorded by TASK-077); unchanged by this task, which does not touch
   `App.tsx`.
3. **`ARCHITECTURE.md` §1's client file tree is stale** — it lists
   `MainMenuScene.ts`, `LobbyScene.ts`, and `ResultScene.ts` as existing files.
   `LobbyScene.ts` now exists; `MainMenuScene.ts` and `ResultScene.ts` still do
   not. Pre-existing (recorded by TASK-079); documentation, not changed here.
4. **A scene must name the wire models it moves through the port.** The port
   exposes capabilities but its signatures reference `services/api/` model
   types, so `LobbyScene` imports those two modules (`BattleModels`,
   `CollectionModels`) as types. This is consistent with §2.2.3 rule 6 — the
   models stay owned by `services/api/` and are not redefined — but it means the
   scene's transport-independence rule is "no transport *implementation*", not
   "no `services/api/` import at all". The boundary test asserts exactly that
   (allow-list of the two model modules; no `ApiService`, no `fetch`, no
   SignalR, no Discord SDK).
5. **`LobbyScene` layout is a placeholder** — rows are not clipped to their
   column width, so an unusually long Pet identity or Card name could overlap
   the next column. Full visual layout/asset work is explicitly out of scope
   for this task; noted for a future presentation task.
