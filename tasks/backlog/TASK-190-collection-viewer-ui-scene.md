# TASK-190 — Collection Viewer UI Scene (Meta Progression)

<!--
  GEN-TASK EXECUTION MANIFEST — FEATURE (Frontend / Presentation)
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  References docs/ and src/ by path and section — it does NOT copy game
  rules, formulas, magnitudes, schemas, or API payload shapes.

  SCOPE OF THIS TASK: Implement a dedicated read-only Collection Viewer scene
  accessible from MainMenuScene, allowing the authenticated player to inspect
  their owned Pets, unlocked Cards, and owned Relics using existing backend
  endpoints and frontend services, without modifying ownership models or introducing
  progression mutation.
-->

---

## Metadata

```text
Task ID:           TASK-190
Type:              FEATURE — TASK_TYPES.md §2; implements documented meta-progression UI
                   from ROADMAP.md Phase 3 using existing API_CONTRACTS.md §5 endpoints.
Status:            BACKLOG
Risk:              MEDIUM (Phaser scene lifecycle, UI coordinate layout, memory cleanup,
                   E2E smoke test integration; zero gameplay logic or schema change)
Priority:          MEDIUM
Primary Agent:     client (owns Phaser scenes, UI layout, GameRuntime port consumption,
                   and client scene navigation)
Supporting Agents: testing (unit scene tests and browser E2E smoke coverage),
                   review (conformance to zero-mutation and pure-viewer boundary)
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/architecture-conformance,
                   quality/scope-validation,
                   testing/test-scenario-generation,
                   client/phaser-architecture
Dependencies:      TASK-187 (DONE — Standalone Web Account Authentication & Discord Retirement),
                   TASK-075 (DONE — Implement Client Collection Read Service),
                   TASK-090 (DONE — Implement Main Menu Scene),
                   TASK-189 (DONE — Standalone Web E2E Browser Smoke Test Suite)
```

---

## Objective

Create a dedicated, read-only **Collection Viewer UI Scene** (`CollectionViewerScene`) in Phaser, accessible from the authenticated `MainMenuScene`, allowing the player to inspect their currently owned **Pets**, unlocked **Cards**, and owned **Relics**. The scene must consume the repository's existing collection read endpoints (`API_CONTRACTS.md` §5) through the existing `GameRuntime` port (`GameRuntimePort`), adhere strictly to existing Phaser viewport conventions (`1280×720`, `SAFE_AREA`), support category switching (Pets / Cards / Relics) and item detail inspection, provide safe back-navigation to `MainMenuScene`, and establish automated browser smoke verification without introducing collection mutation, new progression rules, or SignalR battle connections.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Standalone web client; Player account collection owner; Pet, Card, Relic inventories.
- `docs/00-overview/MVP_SCOPE.md` §2 — OUT: Inventory mutation, crafting, trading, gacha, sell/buy mechanics.
- `docs/00-overview/ROADMAP.md` §1 (Phase 3) — Meta progression UI / Collection Viewer (Pet Collection, Card Collection, Relic Collection).
- `docs/00-overview/GDD.md` §2 — Player owns collections; inspectable meta-progression inventory.
- `docs/02-technical/API_CONTRACTS.md` §5 — Collection read endpoints:
  - `GET /api/pets` (§5.1) — List owned Pets: `[{ petId, identity, element, tier, star, level }]`.
  - `GET /api/pets/{petId}` (§5.2) — Read single owned Pet detail.
  - `GET /api/cards` (§5.3) — List unlocked Cards: `[{ cardId, name, category }]`.
  - `GET /api/relics` (§5.4) — List owned Relics: `[{ relicId, name }]`.
  - List semantics (§5.5) — Empty collection is `200 []`; ordering is undefined; no pagination.
  - No equip/loadout state (§5.6) — Endpoints carry no equip/slot data; collection read is decoupled from loadout.
- `docs/02-technical/ARCHITECTURE.md` §2.2 — Frontend scene architecture: Scenes depend on `GameRuntime` port via `readRuntime(this)`.
- `docs/02-technical/TDD.md` §2.1 — Phaser scene lifecycle and client presentation boundaries.
- `docs/03-decisions/ADR/ADR-020-standalone-web-account-authentication.md` — Standalone web JWT authentication.
- `docs/01-game-design/GAME_RULES.md` §18 & ADR-001 — Server-authoritative architecture; client performs zero authoritative calculation.

---

## Current-State Audit

### Backend
1. **Existing Endpoints:**
   - `GET /api/pets`: Returns `PetResponse[]` for the authenticated caller.
   - `GET /api/pets/{petId}`: Returns `PetResponse` for a specific owned instance; returns `404 PET_NOT_FOUND` if absent or not owned by caller.
   - `GET /api/cards`: Returns `CardResponse[]` for unlocked cards.
   - `GET /api/relics`: Returns `RelicResponse[]` for owned relic instances.
2. **DTO Contracts (`API_CONTRACTS.md` §5):**
   - `PetResponse`: `petId` (string instance id), `identity` (string, e.g. "Xích Lang"), `element` (string: "Fire" | "Water" | "Earth" | "Wood" | "Metal"), `tier` (string, e.g. "Common"), `star` (int), `level` (int). Persisted fields `xp`, `acquiredAt`, `playerId`, `petDefinitionId` are explicitly omitted by the server contract.
   - `CardResponse`: `cardId` (string definition id), `name` (string), `category` ("Basic" | "PetSkill"). Presence in the array represents the unlocked state; no level or quantity exists in MVP.
   - `RelicResponse`: `relicId` (string instance id), `name` (string).
3. **Data Model & Semantics:**
   - Read endpoints are separate per domain (Pets, Cards, Relics); there is no combined aggregate endpoint.
   - Authenticated callers only read their own collection via JWT `player_id` claim; no `playerId` query parameter is accepted.
   - MVP lists have no server pagination, sorting guarantees, or filter parameters.
   - Only owned/unlocked items are returned. The backend does not expose unowned or locked items with flags.
   - Default starter grant (TASK-083, TASK-187): 1 Pet (Xích Lang, Fire, Lv 1), 3 Basic Cards (Heal, Shield, Power Charge), 3 Relics.

### Frontend
1. **Services & Runtime Port:**
   - `ApiService` (`src/frontend/client/src/services/api/ApiService.ts`): Implements `getPets()`, `getPet(petId)`, `getCards()`, and `getRelics()` using `ApplicationSession` bearer tokens.
   - `GameRuntimePort` (`src/frontend/client/src/game/runtime/GameRuntimeEvents.ts`) & `GameRuntime` (`src/frontend/client/src/game/runtime/GameRuntime.ts`): Expose `getPets()`, `getPet(petId)`, `getCards()`, `getRelics()`.
   - `readRuntime(scene)` (`src/frontend/client/src/game/runtime/RuntimeRegistry.ts`): Standard accessor for Phaser scenes to access `GameRuntime` without direct coupling.
2. **Existing Scenes & Presentation Patterns:**
   - Target viewport: `1280×720` (16:9), `SAFE_AREA` margin 24px (`1232×672` playable area), configured in `GameViewport.ts`.
   - `MainMenuScene` (`src/frontend/client/src/game/scenes/MainMenuScene.ts`): Renders dark background (`0x0f172a`), safe area frame, "DCACTI" title at `y = 204` (`SAFE_AREA.y + 180`), and "START BATTLE" button at `x = 640`, `y = 324` (`SAFE_AREA.y + 300`).
   - `LobbyScene` (`src/frontend/client/src/game/scenes/LobbyScene.ts`): Demonstrates collection rendering with pure Phaser text and shape primitives, tracking interactive hit objects in `interactiveObjects` and `renderedTexts`, clearing them during render and `shutdown()`.
   - Navigation: Scene transition via `this.scene.start('SceneKey')`.
3. **Smoke Test Baseline (TASK-189):**
   - Headless Chrome DevTools Protocol (CDP) test in `src/frontend/client/scripts/standalone-web-smoke.mjs`.
   - Clicks "START BATTLE" at logical game coordinates `(640, 324)`. Any new MainMenu button must not overlap this coordinate.

---

## Product Behavior & User Experience

```text
MainMenuScene
    │
    ├─► [START BATTLE] ──► LobbyScene ──► BattleScene ──► ResultScene
    │
    └─► [COLLECTION] ────► CollectionViewerScene
                               ├── Category Tabs: [ PETS ] [ CARDS ] [ RELICS ]
                               ├── Item Grid / List (Owned items)
                               ├── Item Detail Panel (Inspect metadata)
                               └── [ < BACK ] ──► MainMenuScene
```

1. **Main Menu Entry:**
   - A dedicated `COLLECTION` button is added to `MainMenuScene` below `START BATTLE`.
   - Clicking `COLLECTION` transitions to `CollectionViewerScene`.
2. **Category Navigation:**
   - Tabs at top: `PETS`, `CARDS`, `RELICS`.
   - Default active tab on entry: `PETS`.
   - Switching tabs updates the rendered list/grid and clears any selected item detail.
3. **Item List & Inspection:**
   - Each tab displays all owned items returned by the server.
   - Clicking an item selects it and populates the **Item Detail Panel**.
   - Clicking the selected item again or selecting another updates/toggles the detail view.
4. **Item Detail Panel:**
   - A dedicated right-hand side panel within `SAFE_AREA` displaying full available item metadata:
     - **Pet:** Identity, Element, Tier, Star, Level, Instance ID.
     - **Card:** Name, Category ("Basic" | "PetSkill"), Card ID.
     - **Relic:** Name, Relic Instance ID.
5. **Back Navigation:**
   - A persistent `< BACK` button in the header returns to `MainMenuScene`.
   - Scene shuts down cleanly, releasing all listeners and interactive objects.
6. **Viewer Boundary:**
   - Strictly read-only: No equip, unequip, upgrade, level-up, fusion, dismantle, sell, or deck-building interactions.

---

## Scope

### In Scope
- Add `COLLECTION` navigation button in `MainMenuScene`.
- Implement `CollectionViewerScene` registering in `GameConfig.ts`.
- Implement category navigation tab bar: `PETS`, `CARDS`, `RELICS`.
- Implement data loading using `readRuntime(this)` (`getPets()`, `getCards()`, `getRelics()`).
- Implement item list/grid rendering with clear type and attribute indicators.
- Implement item detail panel for inspecting selected item metadata.
- Implement loading, loaded, empty category, and API error states (with retry button).
- Implement back-navigation button returning safely to `MainMenuScene`.
- Implement proper Phaser scene lifecycle and resource cleanup in `shutdown()`.
- Add unit tests for `CollectionViewerScene` and update `SceneLifecycle.test.ts`.
- Extend or create browser E2E smoke test covering the collection navigation journey.

### Out of Scope
- Card/Relic/Pet equip or loadout changes (belongs strictly to `LobbyScene` battle preparation).
- Progression actions: spending XP, leveling pets, star upgrades, card unlocks.
- Inventory mutations: selling, buying, gacha, crafting, trading.
- Locked/unowned item codex/catalog (backend endpoints only return owned items; no locked item catalog exists in API).
- SignalR connection creation or battle state subscription in Collection Viewer.
- Redesigning `MainMenuScene` layout beyond adding the collection button.
- Discord SDK or third-party authentication integrations.

---

## UI / Layout Specification

Authoring resolution: `1280×720` (16:9). Safe area margins: `x: 24, y: 24, width: 1232, height: 672`.

```text
+-----------------------------------------------------------------------------------+
| SAFE_AREA (1232 x 672)                                                            |
|  [ < BACK ]       COLLECTION VIEWER                          Player: smoke_user   |
|  -------------------------------------------------------------------------------  |
|  [  PETS (1)  ]   [  CARDS (3)  ]   [  RELICS (3)  ]                              |
|  -------------------------------------------------------------------------------  |
|  +---------------------------------------+  +----------------------------------+  |
|  | ITEM LIST / GRID                      |  | ITEM DETAIL PANEL                |  |
|  |                                       |  |                                  |  |
|  | > [●] Xích Lang   Fire   Lv 1  ★1     |  | Xích Lang                        |  |
|  |                                       |  | Element: Fire                    |  |
|  |   [○] Thanh Xà    Wood   Lv 1  ★1     |  | Tier: Common                     |  |
|  |                                       |  | Star: 1                          |  |
|  |                                       |  | Level: 1                         |  |
|  |                                       |  | Instance ID: pet-xxx             |  |
|  |                                       |  |                                  |  |
|  |                                       |  |                                  |  |
|  +---------------------------------------+  +----------------------------------+  |
|  Status / Feedback note                                                           |
+-----------------------------------------------------------------------------------+
```

### Visual Specifications
- **Colors:**
  - Background fill: `0x0f172a` (Slate 900)
  - Safe area border: `0x334155` (Slate 700, 2px)
  - Active Tab / Primary Button: `0x1d4ed8` (Blue 700), border `0x60a5fa` (Blue 400)
  - Inactive Tab / Secondary Button: `0x1e293b` (Slate 800), border `0x475569` (Slate 600)
  - Detail Panel Background: `0x1e293b`, border `0x334155`
  - Text colors: Title `#e2e8f0`, Subtitle/Labels `#94a3b8`, Value `#f8fafc`, Error `#f87171`
  - Element colors (consistent with Lobby/Battle): Fire `#ef4444`, Water `#3b82f6`, Earth `#d97706`, Wood `#22c55e`, Metal `#e2e8f0`
  - Card category colors: Basic `#93c5fd`, PetSkill `#c084fc`
- **Coordinates & Layout:**
  - Header:
    - Back button: `x = SAFE_AREA.x + 20, y = SAFE_AREA.y + 20`, size `140×36`.
    - Title: `x = GAME_WIDTH / 2, y = SAFE_AREA.y + 28`, centered.
  - Tab Bar:
    - `y = SAFE_AREA.y + 70`, button height `40`, width `160`, gap `16`.
    - Centered horizontally or left-aligned starting at `SAFE_AREA.x + 20`.
  - Content Area (`y = SAFE_AREA.y + 130` to `SAFE_AREA.y + SAFE_AREA.height - 40`):
    - Left Column (List): width `680px`.
    - Right Column (Detail Panel): width `480px`.
- **MainMenuScene Coordinate Alignment:**
  - Existing "START BATTLE" button: center `x = 640, y = 324` (`SAFE_AREA.y + 300`), size `280×50`.
  - New "COLLECTION" button: center `x = 640, y = 394` (`SAFE_AREA.y + 370`), size `280×50`.
  - Spacing ensures no click target collision with automated tests targeting `(640, 324)`.

---

## State & Data Flow

### 1. Data Fetching
- On `create()`:
  - If `runtime === null`: render error "No runtime available: cannot load collection".
  - Otherwise, set `loading = true` and invoke `loadCollections()`:
    ```ts
    const [pets, cards, relics] = await Promise.all([
      runtime.getPets(),
      runtime.getCards(),
      runtime.getRelics(),
    ]);
    ```
  - On success: store `ownedPets`, `ownedCards`, `ownedRelics`, set `loading = false`, trigger `render()`.
  - On error: store `loadError = describeError(error)`, set `loading = false`, trigger `render()`.

### 2. State Machine
- `tab`: `'pets' | 'cards' | 'relics'` (defaults to `'pets'`).
- `selectedItemId`: `string | null` (resets to `null` on tab switch).
- `loading`: `boolean`.
- `loadError`: `string | null`.

### 3. Error, Empty, and Loading States
- **Loading:** Render loading indicator text ("Loading collection items…").
- **Empty Category:** If `items.length === 0`, render category-specific empty notice:
  - Pets: "No owned Pets returned."
  - Cards: "No unlocked Cards returned."
  - Relics: "No owned Relics returned."
- **API Error:** Render error banner ("Collection load failed: <message>") and a clickable `RETRY` button that re-invokes `loadCollections()`.

---

## Affected Files & Areas

```text
[ ] src/frontend/client/src/game/scenes/CollectionViewerScene.ts    (NEW: Dedicated collection viewer scene)
[ ] src/frontend/client/src/game/scenes/MainMenuScene.ts            (UPDATE: Add COLLECTION navigation button)
[ ] src/frontend/client/src/game/GameConfig.ts                     (UPDATE: Register CollectionViewerScene)
[ ] src/frontend/client/tests/CollectionViewerScene.test.ts        (NEW: Unit tests for CollectionViewerScene)
[ ] src/frontend/client/tests/SceneLifecycle.test.ts               (UPDATE: Verify scene registration & transitions)
[ ] src/frontend/client/scripts/standalone-web-smoke.mjs           (UPDATE: Optional extension for E2E smoke verification)
[ ] src/frontend/client/scripts/collection-viewer-smoke.mjs        (NEW: Dedicated E2E collection viewer smoke script)
```

---

## Acceptance Criteria

<!--
  Binary, testable verification conditions derived from authoritative docs.
-->

- [ ] `MainMenuScene` renders an interactive `COLLECTION` button positioned at `(640, 394)` without altering the `(640, 324)` coordinates of `START BATTLE`.
- [ ] Clicking `COLLECTION` in `MainMenuScene` transitions to `CollectionViewerScene`.
- [ ] `CollectionViewerScene` is registered in `GameConfig.ts` with correct Phaser scene key `'CollectionViewerScene'`.
- [ ] `CollectionViewerScene` requests collection data through `GameRuntimePort` (`getPets()`, `getCards()`, `getRelics()`) without direct SignalR or database dependencies.
- [ ] A loading indicator is displayed while collection requests are in flight.
- [ ] API load failures display the error message and an interactive `RETRY` button.
- [ ] Three category tabs (`PETS`, `CARDS`, `RELICS`) allow switching between item categories.
- [ ] Switching tabs updates the item list, displays count badges (e.g. `PETS (1)`), and clears any active item selection.
- [ ] Pets category renders owned Pet instances showing identity, element, tier, star, and level.
- [ ] Cards category renders unlocked Card definitions showing card name and category (`Basic` / `PetSkill`).
- [ ] Relics category renders owned Relic instances showing relic name.
- [ ] Empty collection responses display category-specific empty messages without errors.
- [ ] Clicking an item opens the item detail panel displaying existing item fields verbatim.
- [ ] Clicking `< BACK` returns to `MainMenuScene`.
- [ ] `CollectionViewerScene.shutdown()` cleanly removes all interactive listeners and destroys dynamic text/shape objects.
- [ ] No SignalR BattleHub connection or battle state is established or mutated by `CollectionViewerScene`.
- [ ] No client-side progression calculation, level derivation, or inventory mutation is performed.
- [ ] Unit tests for `CollectionViewerScene` cover loading, rendering, tab navigation, detail inspection, empty states, error retry, and shutdown cleanup.
- [ ] Existing tests in `SceneLifecycle.test.ts` pass with `CollectionViewerScene` included in the scene list.
- [ ] Browser E2E smoke verification confirms the complete navigation loop: Login → MainMenu → Collection → Tabs → Detail → Back → MainMenu.
- [ ] Existing `standalone-web-smoke.mjs` test passes without regression.

---

## Implementation Notes

1. **Phaser Hit Object Cleanup:** Follow the exact pattern from `LobbyScene.ts`: maintain arrays `private interactiveObjects: Phaser.GameObjects.GameObject[] = []` and `private renderedTexts: Phaser.GameObjects.Text[] = []`. On every re-render and in `shutdown()`, detach listeners (`object.off(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN)`) and destroy objects before rebuilding.
2. **MainMenu Layout Stability:** In `MainMenuScene.ts`, keep `startBattle` button bounds unchanged (`buttonX = GAME_WIDTH / 2 = 640`, `buttonY = SAFE_AREA.y + 300 = 324`, `buttonWidth = 280`, `buttonHeight = 50`). Add the collection button at `buttonY = SAFE_AREA.y + 370 = 394` with matching width and height.
3. **Double-Transition Guard:** Mirror `hasTransitioned` guard in `MainMenuScene` and `CollectionViewerScene` to prevent duplicate transition events on rapid pointer clicks.
4. **Data Isolation:** Store received collections as plain arrays (`PetResponse[]`, `CardResponse[]`, `RelicResponse[]`). Do not create custom client-side wrappers that alter or infer missing backend fields.
5. **Detail Panel Presentation:** Use a persistent container or bounding box on the right half of the safe area. When no item is selected, display "Select an item to view details". When an item is selected, render rows of key-value pairs formatted cleanly.
6. **No Realtime SignalR Overhead:** Unlike `BattleScene`, `CollectionViewerScene` does not listen to `ReceiveEvents` or `BattleStateUpdated`. It operates entirely over the existing REST methods on `GameRuntimePort`.

---

## Implementation Phases

1. **Phase 1: Scene Scaffolding & Configuration**
   - Create `src/frontend/client/src/game/scenes/CollectionViewerScene.ts` skeleton.
   - Register `CollectionViewerScene` in `src/frontend/client/src/game/GameConfig.ts`.
   - Update `SceneLifecycle.test.ts` to include `CollectionViewerScene` in scene order assertions.
2. **Phase 2: Main Menu Navigation**
   - Add `COLLECTION` button to `MainMenuScene.ts` at `y = 394`.
   - Add pointer event handler transitioning to `CollectionViewerScene`.
   - Verify `MainMenuScene` unit tests.
3. **Phase 3: Data Loading & Runtime Port Integration**
   - Implement `loadCollections()` in `CollectionViewerScene` calling `runtime.getPets()`, `runtime.getCards()`, `runtime.getRelics()`.
   - Handle loading state, loaded state, and error handling with retry trigger.
4. **Phase 4: Shell, Header & Tab Navigation**
   - Implement `drawShell()` with safe area background, header title, `< BACK` button, and tab bar (`PETS`, `CARDS`, `RELICS`).
   - Implement tab switching logic and active tab styling.
5. **Phase 5: Item List Rendering**
   - Implement category-specific list rendering (`renderPets()`, `renderCards()`, `renderRelics()`).
   - Implement empty state messaging when arrays are empty.
   - Attach interactive click handlers to item rows.
6. **Phase 6: Detail Panel Rendering**
   - Implement `renderDetailPanel()` on right column displaying selected item attributes.
   - Format element badges, tiers, stars, and levels using established styling.
7. **Phase 7: Teardown & Lifecycle Cleanup**
   - Implement comprehensive `shutdown()` method clearing all interactive handlers, texts, and cached selection state.
8. **Phase 8: Unit Testing**
   - Create `src/frontend/client/tests/CollectionViewerScene.test.ts` mirroring `LobbyScene.test.ts` mocking patterns.
   - Test tab switching, selection, detail rendering, error state retry, empty collections, and cleanup.
9. **Phase 9: Browser E2E Smoke Verification**
   - Create `scripts/collection-viewer-smoke.mjs` (or extend `standalone-web-smoke.mjs`) testing:
     - Account login → MainMenu → Click Collection → Verify CollectionViewerScene active.
     - Verify default Pets tab renders starter pet ("Xích Lang").
     - Click Cards tab → Verify 3 starter cards rendered.
     - Click Relics tab → Verify 3 starter relics rendered.
     - Click item → Verify detail panel displays name/ID.
     - Click Back → Verify MainMenuScene active again.
10. **Phase 10: Regression Verification & Code Review**
    - Run full frontend test suite (`pnpm test:run`).
    - Run frontend typecheck and build (`pnpm build`).
    - Run existing `standalone-web-smoke.mjs` to ensure zero regression in battle flow.

---

## Testing Requirements

### Required Verification
```text
[ ] Unit tests         — CollectionViewerScene.test.ts:
                         - Scene instantiation and runtime port resolution
                         - Collection data fetch on create
                         - Tab switching (Pets, Cards, Relics) and count badges
                         - Item selection and detail panel population
                         - Empty state rendering for empty collections
                         - Error display and retry button functionality
                         - Back navigation invoking this.scene.start('MainMenuScene')
                         - Shutdown listener detachment and object disposal
[ ] Integration tests  — SceneLifecycle.test.ts:
                         - Registration in GameConfig scene list
                         - Bidirectional transition MainMenuScene <-> CollectionViewerScene
[ ] Browser E2E Smoke  — scripts/collection-viewer-smoke.mjs (or standalone-web-smoke.mjs):
                         - Real browser CDP click navigation
                         - Verification of starter Pet, Cards, Relics
                         - Verification of Back button returning to MainMenuScene
```

### Key Edge Cases
- **No Runtime Available:** If `readRuntime(this)` returns `null` (e.g. headless or uninitialized harness), scene must display graceful error message rather than crash.
- **Empty Collection Category:** If player owns 0 items in a category (e.g. 0 relics), scene must display clean empty state ("No owned Relics returned.") without throwing or rendering blank rectangles.
- **Rapid Navigation / Multi-Click:** Double-clicking `< BACK` or tab buttons must not cause duplicate scene transitions or unhandled exceptions.
- **API Network Failure:** Network error or 500 response from collection API shows user-friendly error note and functional RETRY button.
- **Session Expiration (401):** If token expires during fetch, error state handles rejection gracefully; `ApplicationSession` clear triggers auth redirect via global shell.

---

## Verification Commands

```bash
# Frontend Unit Tests
pnpm --dir src/frontend/client test:run

# Frontend Build & Typecheck
pnpm --dir src/frontend/client build

# Existing Standalone Web E2E Browser Smoke Test (Regression Safety Net)
pnpm --dir src/frontend/client verify:e2e:smoke

# Dedicated Collection Viewer Browser Smoke Verification
node src/frontend/client/scripts/collection-viewer-smoke.mjs
```

---

## Stop Conditions

<!-- Universal stop conditions in AGENTS.md §20 always apply. -->

- If implementation requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10.
- If task requires inventing new collection APIs, locked item schemas, or backend progression models: STOP per `AGENTS.md` §7.
- If task modifies existing `START BATTLE` coordinates `(640, 324)` in `MainMenuScene` breaking `standalone-web-smoke.mjs`: STOP & adjust layout.
- If task introduces Discord SDK or embedded iframe dependencies: STOP per `ADR-020`.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Changed Files
- `src/frontend/client/src/game/scenes/CollectionViewerScene.ts` — NEW: read-only collection viewer scene (tabs, item list, detail panel, loading/error/retry, back navigation, shutdown cleanup).
- `src/frontend/client/src/game/scenes/MainMenuScene.ts` — UPDATE: added `COLLECTION` button at `(640, 394)` via a shared `drawButton` helper; `START BATTLE` unchanged at `(640, 324)`.
- `src/frontend/client/src/game/GameConfig.ts` — UPDATE: registered `CollectionViewerScene`.
- `src/frontend/client/tests/CollectionViewerScene.test.ts` — NEW: 51 unit tests.
- `src/frontend/client/tests/SceneLifecycle.test.ts` — UPDATE: scene registration + MainMenu coordinates/navigation + viewer lifecycle.
- `src/frontend/client/tests/PhaserGame.test.tsx` — UPDATE: registered-scene-list assertion.
- `src/frontend/client/tests/RuntimeBoundaries.test.ts` — UPDATE: scene boundary list covers the new scene.
- `src/frontend/client/scripts/collection-viewer-smoke.mjs` — NEW: CDP browser smoke test.
- `src/frontend/client/package.json` — UPDATE: added `verify:e2e:collection` script.

### Validation Results
- `npx vitest run tests/CollectionViewerScene.test.ts` — PASS (51 tests)
- `npx vitest run tests/SceneLifecycle.test.ts` — PASS (92 tests)
- `npx vitest run` (full frontend suite) — PASS (615 tests, 18 files)
- `npm run build` — PASS (`tsc` clean + `vite build`)
- `node scripts/collection-viewer-smoke.mjs` — PASS (40 checks × 2 clean-context runs)
- `node scripts/standalone-web-smoke.mjs` (TASK-189 regression) — PASS (29 checks × 2 runs)

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1, §2)
- [x] Confirmed read-only: no equip, upgrade, mutation, gacha, trade, or loadout change
- [x] Confirmed no `BattleHub` connection, no battle state, no direct HTTP/SignalR in the scene
- [x] Confirmed backend, database, authentication, and SignalR protocol unchanged

### Implementation Note
`< BACK`, tab, and item clicks in the browser smoke test use a retry-until-observed
helper (`clickUntil`), matching the retry convention `standalone-web-smoke.mjs`
already uses. Phaser maps a pointer into game space from canvas bounds the Scale
Manager measures a frame or two after layout settles, so a click dispatched in
that window is mapped against stale bounds. This was diagnosed by instrumenting
Phaser's own pointer (a mis-mapped click reported `pointer.y = -330` for a screen
`y = 394`); it is a harness timing characteristic, not a scene defect.

