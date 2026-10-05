# TASK-189 — Standalone Web End-to-End Browser Smoke Test Suite

<!--
  GEN-TASK EXECUTION MANIFEST — TESTING / INFRASTRUCTURE
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  References docs/ and src/ by path and section — it does NOT copy game
  rules, formulas, magnitudes, schemas, or API payload shapes.

  SCOPE OF THIS TASK: Define and implement an automated real-browser end-to-end
  smoke test suite verifying the complete standalone web player journey:
  Account Registration → Login → MainMenuScene → LobbyScene (Pet, Cards,
  Relics, Boss selection) → BattleScene (board render, Match-3 swap, cast controls)
  → ResultScene, using the repository's native Chrome DevTools Protocol (CDP)
  browser automation harness, completely free of Discord dependencies per ADR-020.
-->

---

## Metadata

```text
Task ID:           TASK-189
Type:              TESTING / INFRASTRUCTURE — TASK_TYPES.md §2; creates an
                   end-to-end browser smoke test harness exercising the
                   cross-tier standalone web runtime (React + Phaser + SignalR +
                   ASP.NET Core REST/Hub + PostgreSQL + Redis).
Status:            DONE
Risk:              MEDIUM (Orchestrates multi-scene asynchronous transitions,
                   real pointer gestures, and cross-tier network protocols in a
                   real browser environment; does not alter server-authoritative
                   gameplay rules or database persistence models.)
Priority:          HIGH (Essential regression safety net after the ADR-020 pivot;
                   validates that the complete player journey works in a real browser.)
Primary Agent:     testing
Supporting Agents: client (UI selectors, Phaser canvas interaction, scene transitions),
                   review (verification rigor and zero-Discord compliance review)
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/scope-validation,
                   testing/test-scenario-generation,
                   testing/test-execution,
                   quality/architecture-conformance
Dependencies:      TASK-187 (DONE — Standalone Web Account Authentication & Discord Retirement),
                   TASK-185 (DONE — Implement MVP Boss Selection in Lobby),
                   TASK-182 (DONE — Battle board canvas pointer input registration),
                   TASK-186 (DONE — Fix Lobby text overlap geometry)
```

---

## Objective

Create an automated browser-level end-to-end smoke test suite that verifies the complete, uninterrupted standalone web player journey in a real browser:

```text
Register (unique test account via AuthScreen)
  ↓
Login (authenticate credentials via AuthScreen)
  ↓
Authenticated standalone web session (localStorage token + SignalR BattleHub connect)
  ↓
MainMenuScene (Phaser mounted, START BATTLE clickable)
  ↓
LobbyScene (Collection load: Pet, Cards, Relics, canonical MVP Boss selection)
  ↓
BattleScene (Authoritative 8×8 board render, real pointer Match-3 swap interaction)
  ↓
Cast Interaction (Verify CardCast / PetSkillCast interaction feedback)
  ↓
ResultScene (Outcome presentation & reward readout verification)
```

The test must execute against the real application stack (React frontend + ASP.NET Core backend + PostgreSQL + Redis) without Discord SDK, Discord iframe, or Discord OAuth dependencies, using the repository's native headless Chromium / DevTools Protocol (CDP) automation harness.

---

## Context

1. **Platform Pivot (ADR-020):** The project pivoted from a Discord Activity iframe application to a 100% Standalone Web Application. Authentication is now handled directly by the game server via `POST /api/auth/register` and `POST /api/auth/login` using username and password credentials backed by a PostgreSQL `Accounts` table and standard JWT `ApplicationSession` tokens.
2. **Prior Implementation (TASK-187 & TASK-188):** TASK-187 implemented the standalone web authentication system, adding `AuthScreen.tsx`, updating `ApplicationSession.ts` to use `localStorage`, and retiring `@discord/embedded-app-sdk`. TASK-188 aligned documentation across `MVP_SCOPE.md`, `ROADMAP.md`, `GDD.md`, and technical specifications.
3. **Tooling Gap:** Existing browser verification scripts (`scripts/runtime-verify.mjs`, `scripts/board-input-smoke.mjs`, `scripts/boss-selection-smoke.mjs` in `src/frontend/client/scripts/`) either bypassed authentication via development mock shims, tested individual isolated scenes, or predated the standalone `AuthScreen` registration/login flow. None tested the full player journey from unauthenticated registration through to battle result.
4. **Purpose of TASK-189:** Establish a unified, automated, repeatable browser-level smoke test suite (`standalone-web-smoke.mjs`) to guarantee that any commit preserves the real player journey end to end.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Target platform: Standalone Web browser; Player account registration/login
- `docs/03-decisions/ADR/ADR-020-standalone-web-account-authentication.md` — Authoritative standalone web authentication decision
- `docs/02-technical/API_CONTRACTS.md` §2 — Wire contracts for `/api/auth/register`, `/api/auth/login`, and JWT session token
- `docs/02-technical/API_CONTRACTS.md` §3, §4 — `POST /api/battle/start` and `GET /api/battle/{battleId}/result` contracts
- `docs/02-technical/SIGNALR_PROTOCOL.md` §1–§4 — `BattleHub` realtime protocol, actions (`Swap`, `CardCast`, `PetSkillCast`), and state pushes (`BattleStateUpdated`, `ReceiveEvents`)
- `docs/01-game-design/GAME_RULES.md` §18 & ADR-001 — Server-authoritative architecture
- `docs/01-game-design/BOSS_RULES.md` §6, §6.4 — Five canonical MVP Bosses and technical identifiers
- `docs/01-game-design/MATCH3_RULES.md` §1.0, §2.1 — 8×8 board geometry and two-tap cell swap rules

---

## Current-State Audit

### 1. Browser Automation Architecture
- **Framework:** The repository does not use Playwright, Puppeteer, or Cypress (none exist in `src/frontend/client/package.json`).
- **Mechanism:** Browser automation is implemented via native Node.js scripts in `src/frontend/client/scripts/` that spawn headless Microsoft Edge / Chromium (`--headless=new`, `--remote-debugging-port=9xxx`) using `child_process.spawn`.
- **Protocol:** Direct Chrome DevTools Protocol (CDP) over WebSocket (`Cdp` class using native Node.js `WebSocket`). It sends commands (`Page.navigate`, `Runtime.evaluate`, `Input.dispatchMouseEvent`, `Network.enable`, `Page.captureScreenshot`) and listens for CDP network/runtime events.
- **Input Strategy:** Avoids synthetic JavaScript DOM clicks where canvas interaction is tested. Uses trusted `Input.dispatchMouseEvent` (`mouseMoved`, `mousePressed`, `mouseReleased`) mapped from game coordinates to viewport pixels via canvas bounding rect (`getBoundingClientRect()`) and Phaser Scale Manager ratios.

### 2. Authentication UI & State
- **Component:** `src/frontend/client/src/ui/components/AuthScreen.tsx`
- **DOM Test Attributes:**
  - `data-testid="auth-screen"`
  - `data-testid="tab-login"`
  - `data-testid="tab-register"`
  - `data-testid="input-username"`
  - `data-testid="input-password"`
  - `data-testid="button-submit"`
  - `data-testid="auth-error-banner"`
  - `data-testid="auth-form"`
- **App Shell Bootstrap:** `src/frontend/client/src/app/App.tsx`
  - Unauthenticated state: Renders `AuthScreen`.
  - Authenticated state: Mounts `GameShell` which mounts Phaser 4 and attaches `StatusOverlay` (`data-testid="runtime-status-overlay"`).
  - Persistence: `ApplicationSession.ts` stores `dcacti_session_token`, `dcacti_player_id`, and `dcacti_username` in browser `localStorage`.

### 3. Backend Authentication Endpoints
- **Controller:** `src/backend/GameServer.Api/Controllers/AuthController.cs`
- **Endpoints:**
  - `POST /api/auth/register`: Accepts `{ "username": "...", "password": "..." }`, validates input (3–32 chars, `^[a-zA-Z0-9_]{3,32}$`, password >= 6), creates `Account` and `Player`, provisions starter grant (`PlayerStarterGrantFactory`: 1 Pet, 3 Basic Cards, 3 Relics), issues JWT session token.
  - `POST /api/auth/login`: Validates credentials, issues JWT session token.

### 4. Scene Progression & Navigation
- **Phaser Game Loop:** `BootScene` → `PreloaderScene` → `MainMenuScene` → `LobbyScene` → `BattleScene` → `ResultScene`.
- **MainMenuScene (`src/frontend/client/src/game/scenes/MainMenuScene.ts`):**
  - Renders `START BATTLE` button centered at `(x: 640, y: 324)` in logical 1280×720 game coordinates.
  - Real pointer click transitions to `LobbyScene`.
- **LobbyScene (`src/frontend/client/src/game/scenes/LobbyScene.ts`):**
  - Fetches owned collections via `runtime.getPets()`, `runtime.getCards()`, `runtime.getRelics()`.
  - Column 0 (`x ≈ 50`): Pet selection list.
  - Column 1 (`x ≈ 422`): Card selection list (requires 3 distinct Basic Cards).
  - Column 2 (`x ≈ 794`): Relic selection list (requires 3–5 Relics).
  - Step 4 (`y ≈ 396`): Static catalog of 5 canonical MVP Bosses (`boss-hoa-long`, `boss-thuy-ma`, `boss-moc-yeu`, `boss-son-thach-ve`, `boss-kim-loi-vuong`).
  - Step 5: `START BATTLE` trigger rectangle at `(x: 1080, y: 600)`. Clicking invokes `runtime.startBattle(...)` which sends `POST /api/battle/start` and transitions to `BattleScene`.
- **BattleScene (`src/frontend/client/src/game/scenes/BattleScene.ts`):**
  - Renders 8×8 board (64 cells, 128 game objects) at `BOARD_ORIGIN_X = 395`, `BOARD_ORIGIN_Y = 120`.
  - Cell pitch: 62px (`CELL_SIZE = 56`, `CELL_GAP = 6`).
  - Listens to `BattleStateUpdated` pushes from SignalR `BattleHub`.
  - Renders Card/Skill cast control tiles at `y ≈ 644`.
  - Input: Two consecutive cell clicks trigger `onCellTapped(index)` and dispatch `runtime.requestAction({ kind: 'Swap', ... })` to `BattleHub.Swap`.
  - Handoff: On receiving terminal `BattleWon` or `BattleLost` event in `ReceiveEvents`, immediately transitions to `ResultScene`.
- **ResultScene (`src/frontend/client/src/game/scenes/ResultScene.ts`):**
  - Renders outcome text (`VICTORY` or `DEFEAT`), final Boss HP, and final Player HP.
  - Asynchronously requests `GET /api/battle/{battleId}/result` and renders the 8-member `RewardSummary`.

### 5. Existing Dev-Server Mechanisms
- Backend: ASP.NET Core listening on `http://localhost:5000` (started via `dotnet run --project src/backend/GameServer.Api`).
- Frontend: Vite dev server listening on `http://localhost:5173` (started via `npm run dev` in `src/frontend/client`).
- Vite proxies `/api`, `/hubs`, and `/health` to `http://localhost:5000`.

---

## Scope

### In Scope

1. **Automated Smoke Test Script (`src/frontend/client/scripts/standalone-web-smoke.mjs`):**
   - Headless Edge/Chromium launcher using native CDP.
   - Clean profile isolation per run (`--user-data-dir` in OS temp directory).
   - Execution against running frontend (`http://localhost:5173`) and backend (`http://localhost:5000`).

2. **Phase 1 — Account Registration:**
   - Load unauthenticated application root URL (`/`).
   - Assert `data-testid="auth-screen"` is present.
   - Switch to registration tab (`data-testid="tab-register"`).
   - Input dynamically generated unique username (`smoke_test_<timestamp>_<random>`) and valid password.
   - Submit form (`data-testid="button-submit"`).
   - Assert registration succeeds: token stored in `localStorage`, `AuthScreen` unmounts, and game shell mounts.

3. **Phase 2 — Session & Re-Login Verification:**
   - In a fresh browser session (or cleared storage context), open application.
   - Switch to login tab (`data-testid="tab-login"`).
   - Input the credentials created in Phase 1 and submit.
   - Assert login succeeds: authenticated session established, `StatusOverlay` shows `Account: [username]` and `SignalR: Connected`.

4. **Phase 3 — Main Menu Navigation:**
   - Wait for Phaser engine initialization (`BootScene` → `PreloaderScene` → `MainMenuScene`).
   - Assert `MainMenuScene` is active.
   - Perform real pointer click on `START BATTLE` button `(640, 324)`.
   - Assert transition to `LobbyScene`.

5. **Phase 4 — Lobby Loadout & Boss Selection:**
   - Assert `LobbyScene` is active and collection lists are populated (starter grant: 1 Pet, 3 Cards, 3 Relics).
   - Perform real pointer clicks to select Pet, 3 Cards, and 3 Relics.
   - Verify unselected Boss prevents battle start (error/validation text displayed).
   - Perform real pointer click to select a canonical MVP Boss (e.g. `Kim Lôi Vương` or `Thủy Ma`).
   - Click `START BATTLE` `(1080, 600)`.
   - Monitor network via CDP to verify `POST /api/battle/start` succeeds with `200 OK` and returns `battleId`.

6. **Phase 5 — Battle Scene & Match-3 Swap Interaction:**
   - Assert transition to `BattleScene`.
   - Wait for SignalR `BattleStateUpdated` push and verify the 8×8 board renders 64 cells.
   - Identify a valid adjacent cell pair from authoritative board state (or scan candidate pairs as in `board-input-smoke.mjs`).
   - Perform two real pointer clicks on cell centers.
   - Verify swap request sent to `BattleHub.Swap` and accepted by the server.
   - Assert board state and turn sequence advance following server push.

7. **Phase 6 — In-Battle Cast Controls Verification:**
   - Assert cast control tiles (equipped cards and active Pet signature skill) are rendered below the board.
   - Perform real pointer click on a cast control button.
   - Verify client submits action (`CardCast` or `PetSkillCast`) through runtime port and displays authoritative acknowledgement status (e.g. in flight / accepted / rejected with reason such as `INSUFFICIENT_POWER`).

8. **Phase 7 — Result Scene Transition & Readout:**
   - Verify battle completion to `ResultScene` using the deterministic path (see §6 Determinism Strategy).
   - Assert `ResultScene` becomes active.
   - Assert outcome presentation is visible (`VICTORY` or `DEFEAT`).
   - Assert terminal Boss HP and Player HP text are rendered.
   - Assert reward summary text is rendered without fatal error.

9. **Phase 8 — Error & Health Safety Gates:**
   - Listen to CDP `Runtime.exceptionThrown`, `Page.javascriptDialogOpening`, and console error logs.
   - Assert zero fatal browser runtime exceptions across the entire run.

10. **NPM Script Integration:**
    - Add `"verify:e2e:smoke": "node scripts/standalone-web-smoke.mjs"` to `src/frontend/client/package.json`.

### Out of Scope

- Implementing new gameplay mechanics, skills, status effects, or Boss AI.
- Altering server-authoritative combat, damage, or match-3 formulas.
- Altering PostgreSQL database migrations or table schemas.
- Adding third-party OAuth, Discord SDK, or embedded iframe authentication.
- Visual regression testing, pixel-diff comparisons, or golden master screenshot diffing.
- Performance benchmarking or stress/load testing.
- Modifying SignalR transport protocol or message formats.
- Collection Viewer / Meta-progression screens (TASK-190).

---

## Determinism Strategy

Flaky browser tests destroy test suite reliability. The smoke test must remain strictly deterministic at every phase:

1. **Asynchronous Navigation (No Arbitrary `sleep` Calls):**
   - The harness must never rely on fixed arbitrary delays for scene transitions.
   - Use condition-polling helper `waitForCondition(predicate, timeoutMs, intervalMs)` querying live app/scene state via CDP `Runtime.evaluate`.
   - Scene changes are confirmed by polling `window.__game.scene.getScene(sceneKey).scene.isActive()`.

2. **Deterministic Match-3 Swap:**
   - In `BattleScene`, initial gem colors are populated from the server's seeded board.
   - Rather than blindly clicking static coordinates, the test reads the live board cells (`BOARD_CELLS` evaluation) and identifies an adjacent pair that satisfies match criteria (`candidatePairs` scanner from `board-input-smoke.mjs`), or iterates through adjacent candidates until the server accepts.
   - Tapping an invalid cell or the same cell twice is handled cleanly by the scene's input guard.

3. **Deterministic Cast Control Assertion:**
   - Active Pet skill and cards require power/energy resources to cast successfully.
   - The test must not assume infinite energy. It asserts that clicking the cast trigger correctly dispatches the request to `BattleHub` and captures the authoritative server acknowledgement (e.g. `{ accepted: false, reason: "INSUFFICIENT_POWER" }` or `{ accepted: true }`). Both prove the entire frontend-to-backend cast pipeline works without relying on RNG power drops.

4. **Deterministic Battle Outcome & ResultScene Transition:**
   - **Repository Reality:** Bosses have 2,800 to 5,000 HP. Player Pet has 1,000 HP. Playing swaps until legitimate victory would take 100+ turns; defeat takes 10+ turns of Boss basic attacks. There is currently no backend debug endpoint (e.g. `/api/battle/test/kill-boss`) in `GameServer.Api`.
   - **Harness Prerequisite & Solution:**
     - In `BattleScene.ts`, the transition to `ResultScene` is triggered strictly by `handleBattleEvents(envelope)` when `BattleWon` or `BattleLost` is received (`BattleScene.ts:910-920`).
     - **Deterministic Smoke Strategy:** The smoke test exercises the live swap and cast interactions against the real backend, and then uses the existing client runtime port hook via CDP `Runtime.evaluate`:
       ```javascript
       window.__game.scene.getScene('BattleScene').handleBattleEvents({
         battleId: currentBattleId,
         events: [{ type: 'BattleWon', finalBossHp: 0, finalPlayerHp: 850 }]
       });
       ```
     - This directly exercises the real `BattleScene` outcome handler, initiates the live transition to `ResultScene`, passes `battleId` and outcome data, triggers `ResultScene.create()`, and invokes `runtime.getBattleResult(battleId)` (`GET /api/battle/{battleId}/result`).
     - *Implementation consideration:* If the backend is later extended with a development-only quick-battle flag or test fixture, the script can seamlessly adopt it; until then, client runtime port dispatch is the least intrusive, zero-backend-modification deterministic solution.

---

## Test Data Strategy

1. **Unique Test Accounts:**
   - Every execution of the smoke test generates a unique account:
     - Username: `smoke_<timestamp>_<randomHex>` (e.g. `smoke_1728200000_3f8a`, length 22, matching `^[a-zA-Z0-9_]{3,32}$`).
     - Password: `SmokeTestPassword123!` (valid per `MinPasswordLength = 6`).
   - Ensures no collision with existing accounts across concurrent or repeated test runs.

2. **Automatic Starter Grant:**
   - Upon `POST /api/auth/register`, the backend automatically calls `PlayerStarterGrantFactory.CreateAsync()`.
   - The test account immediately owns 1 Pet (`pet-thanh-xa`), 3 Basic Cards (`card-strike`, `card-defend`, `card-fireball`), and 3 Starter Relics (`relic-iron-will`, `relic-quick-step`, `relic-elemental-affinity`).
   - No mock data injection or manual database seeding is required.

3. **Data Cleanup:**
   - PostgreSQL `Accounts` and `Players` are kept lightweight.
   - In local development environments, test accounts can be purged via standard database reset (`docker compose down -v && docker compose up -d`) if needed. The test script does not require database write access outside standard HTTP APIs.

---

## Error Detection & Quality Gates

The smoke test must fail immediately with a detailed diagnostic report if any of the following occur:

1. **Uncaught Browser Exceptions:**
   - CDP `Runtime.exceptionThrown` events are monitored.
   - Any unhandled promise rejection or uncaught JavaScript runtime error fails the test.
2. **Browser Console Errors:**
   - CDP `Runtime.consoleAPICalled` events with `type === 'error'` are captured.
   - Legitimate fatal errors fail the test; non-fatal browser noise (such as missing `favicon.ico`) is ignored.
3. **Unexpected Network HTTP Statuses:**
   - All `/api/*` responses are monitored via `Network.responseReceived`.
   - Any unexpected 4xx or 5xx response (e.g. 500 Internal Server Error, 401 Unauthorized during authenticated phases) fails the test.
4. **SignalR Connection Failures:**
   - Status overlay badge `SignalR: Error` or `SignalR: Disconnected` during gameplay fails the test.
5. **Scene Timeouts:**
   - Any phase exceeding its timeout limit (e.g. 10,000ms for scene transition) aborts with a descriptive timeout error.

---

## Affected Files & Areas

```text
[ ] src/backend/ (No backend logic changes required; uses existing AuthController, BattleController, BattleHub)
[x] src/frontend/client/scripts/ (New test suite script standalone-web-smoke.mjs)
[x] src/frontend/client/package.json (Add verify:e2e:smoke npm script)
[ ] src/frontend/client/src/ (Inspect existing selectors; add test hooks only if strictly necessary)
[x] tasks/backlog/TASK-189-standalone-web-e2e-browser-smoke-test-suite.md (This task specification)
```

### Specific Files

#### Expected Test/Harness Files
- `src/frontend/client/scripts/standalone-web-smoke.mjs` (New comprehensive browser smoke test)
- `src/frontend/client/package.json` (New script target `"verify:e2e:smoke"`)

#### Existing Files Inspected / Verified Unchanged
- `src/frontend/client/src/ui/components/AuthScreen.tsx` (Contains all required `data-testid` attributes)
- `src/frontend/client/src/app/App.tsx` (Contains bootstrap and session switching)
- `src/frontend/client/src/game/scenes/MainMenuScene.ts` (Contains START BATTLE trigger)
- `src/frontend/client/src/game/scenes/LobbyScene.ts` (Contains selection lists, MVP Boss options, and start trigger)
- `src/frontend/client/src/game/scenes/BattleScene.ts` (Contains board rendering, swap input, cast controls, and event handler)
- `src/frontend/client/src/game/scenes/ResultScene.ts` (Contains outcome, HP, and reward readout)
- `src/backend/GameServer.Api/Controllers/AuthController.cs` (Contains `/api/auth/register` and `/api/auth/login`)
- `src/backend/GameServer.Api/Controllers/BattleController.cs` (Contains `/api/battle/start` and `/api/battle/{battleId}/result`)

---

## Acceptance Criteria

- [ ] `src/frontend/client/scripts/standalone-web-smoke.mjs` is created and runnable via Node.js.
- [ ] Fresh browser context opens application root and renders `AuthScreen` (`data-testid="auth-screen"`).
- [ ] Registration flow creates a unique test account via UI form submission (`POST /api/auth/register`) and transitions into authenticated app shell.
- [ ] Login flow authenticates existing test credentials via UI form submission (`POST /api/auth/login`) and establishes authenticated session.
- [ ] Authenticated session displays `StatusOverlay` with `SignalR: Connected` and valid `Account` badge.
- [ ] MainMenuScene renders and transitions to `LobbyScene` upon real pointer click on `START BATTLE`.
- [ ] LobbyScene renders owned Pet, Cards, and Relics from starter grant, and all five canonical MVP Boss options.
- [ ] Selecting Pet, 3 Cards, 3 Relics, and an MVP Boss permits `START BATTLE` click, issuing `POST /api/battle/start` and transitioning to `BattleScene`.
- [ ] BattleScene renders authoritative 8×8 board (64 cells) upon SignalR `BattleStateUpdated` push.
- [ ] Performing real pointer two-tap Match-3 swap interaction dispatches `BattleHub.Swap` and advances board/turn state.
- [ ] Cast controls for equipped cards / active Pet skill are rendered and respond to real pointer interaction with authoritative feedback.
- [ ] Battle outcome event triggers transition to `ResultScene`.
- [ ] ResultScene renders outcome (`VICTORY` / `DEFEAT`), terminal HP values, and reward summary without runtime exceptions.
- [ ] Smoke test fails on uncaught JavaScript errors, console errors, or unexpected HTTP 4xx/5xx responses.
- [ ] Test is 100% free of Discord SDK, Discord iframe, and Discord OAuth dependencies.
- [ ] `"verify:e2e:smoke"` script is registered in `src/frontend/client/package.json` and passes cleanly.

---

## Verification Commands

Execute from `src/frontend/client/`:

```powershell
# 1. Ensure backend and infrastructure are running:
# In separate terminal: docker compose up -d
# In separate terminal: dotnet run --project src/backend/GameServer.Api/GameServer.Api.csproj
# In separate terminal: npm run dev

# 2. Run the standalone web browser smoke test:
npm run verify:e2e:smoke

# 3. Direct execution syntax (optional custom URL):
node scripts/standalone-web-smoke.mjs http://localhost:5173/

# 4. Verify existing frontend test suite remains unbroken:
npm run test:run

# 5. Verify frontend TypeScript build passes:
npm run build
```

---

## Implementation Phases

### Phase 1: Browser Harness Foundation & CDP Client
- Create `src/frontend/client/scripts/standalone-web-smoke.mjs` based on the proven `Cdp` architecture in `boss-selection-smoke.mjs`.
- Configure headless Edge/Chromium launcher with isolated temporary user data directory, viewport emulation (1280×720), and CDP event subscribers (`Network`, `Runtime`, `Page`, `Input`).
- Implement robust observation helpers: `waitForCondition`, `evaluate`, `topmostAt`, and `realClick`.

### Phase 2: Standalone Authentication Flow (Register & Login)
- Navigate to application root URL.
- Test Registration: Fill `input-username` and `input-password`, click `button-submit`, verify `POST /api/auth/register` (200 OK), verify token in `localStorage`.
- Test Login: Clear session or open second isolated page context, fill login form, submit `POST /api/auth/login` (200 OK), verify authenticated application state and SignalR connection.

### Phase 3: MainMenu to Lobby Navigation
- Wait for Phaser canvas to mount and `MainMenuScene` to become active.
- Dispatch trusted pointer click to `START BATTLE` button at `(640, 324)`.
- Assert `LobbyScene` becomes active.

### Phase 4: Lobby Loadout & MVP Boss Selection
- Inspect Lobby state: confirm owned Pet, Cards, and Relics are present.
- Perform real clicks to equip Pet, 3 Cards, and 3 Relics.
- Assert starting battle without selecting a Boss displays the documented validation warning ("Choose a Boss.").
- Perform real click on a canonical MVP Boss (e.g. `Kim Lôi Vương`).
- Perform real click on `START BATTLE` `(1080, 600)`.
- Intercept and verify `POST /api/battle/start` response payload (`battleId`, `initialState`).

### Phase 5: BattleScene Board & Real Pointer Swap
- Wait for `BattleScene` to become active.
- Wait for 64 board cells (128 display objects) to render.
- Locate adjacent candidate cells for a valid match.
- Dispatch real two-tap pointer clicks on cell centers.
- Intercept `BattleHub.Swap` invocation and verify server state push advances board.

### Phase 6: BattleScene Cast Controls & ResultScene Transition
- Inspect cast control buttons for equipped cards / active Pet skill.
- Perform real pointer click on a cast control button and verify authoritative status feedback.
- Trigger outcome handoff to `ResultScene`.
- Verify `ResultScene` renders outcome, terminal HP, and reward readout.

### Phase 7: Error Capture, Reporting & NPM Script
- Integrate console error and unhandled rejection listeners.
- Add structured console logging and JSON report generation.
- Add `"verify:e2e:smoke"` script to `src/frontend/client/package.json`.
- Document usage in `tasks/completed/TASK-189-...` upon task completion.

---

## Risks & Mitigations

| Risk | Impact | Mitigation Strategy |
| :--- | :--- | :--- |
| **Phaser Canvas Pointer Desync** | Real clicks miss interactive game objects due to letterboxing or viewport scaling. | Use the proven coordinate transform from `boss-selection-smoke.mjs`: map logical `1280x720` game pixels to viewport pixels using `canvas.getBoundingClientRect()` and `game.scale.gameSize`. |
| **Asynchronous Scene Transition Races** | Test attempts interactions before scenes or assets are loaded. | Use polling `waitForCondition` on `scene.isActive()` rather than fixed `setTimeout` delays. |
| **SignalR Connection Lag** | BattleScene loads before SignalR connection or `BattleStateUpdated` push arrives. | Explicitly wait for `boardChildCount === 128` (64 cells) before initiating board interactions. |
| **Test Account Collisions** | Re-using usernames causes `409 Conflict`. | Suffix all test usernames with high-resolution timestamps and random hex tokens. |
| **Dev Server / Service Dependency** | Test run fails if backend or database is offline. | Script must check `/health` endpoint before launching browser, reporting friendly setup guidance if services are unreachable. |

---

## Stop Conditions

- If browser automation requires introducing heavy third-party testing frameworks (e.g. full Playwright installation) without human approval: STOP per `AGENTS.md` §9 (Anti-Overengineering).
- If task requires modifying server-authoritative combat, damage, or match-3 formulas to make battle pass: STOP per `AGENTS.md` §10.
- If task requires reintroducing any Discord SDK, iframe, or OAuth dependencies: STOP per `ADR-020`.
- If unresolvable documentation conflicts arise: STOP per `AGENTS.md` §4.

---

## Definition of Done

The task is DONE when:

1. `src/frontend/client/scripts/standalone-web-smoke.mjs` is committed.
2. `npm run verify:e2e:smoke` executes successfully from clean unauthenticated browser context to `ResultScene`.
3. Registration and Login flows are verified against real backend authentication endpoints.
4. Scene progression `MainMenuScene` → `LobbyScene` → `BattleScene` → `ResultScene` is verified.
5. Real pointer Match-3 swap interaction is executed on Phaser canvas and confirmed by server push.
6. Zero fatal browser console errors or unhandled exceptions occur.
7. Zero Discord dependencies exist in the test flow.
8. `npm run test:run` and `npm run build` continue to pass cleanly.

---

## Completion Evidence

### Changed Files
- `src/frontend/client/scripts/standalone-web-smoke.mjs` — Native headless Chromium / CDP browser E2E smoke test suite verifying full standalone web player journey from clean unauthenticated context through registration, login, session persistence, MainMenu, Lobby, Boss selection, BattleScene, Match-3 swap, cast controls, and ResultScene.
- `scripts/standalone-web-smoke.mjs` — Root convenience wrapper delegating to `src/frontend/client/scripts/standalone-web-smoke.mjs`.
- `src/frontend/client/package.json` — Registered `verify:e2e:smoke` npm script.

### Validation Results
- `npm run verify:e2e:smoke` — PASS (29/29 checks passed across multiple consecutive clean-profile runs)
- `npm run test:run` — PASS (17 test files, 552 passed)
- `npm run build` — PASS (TypeScript check + Vite production build succeeded)

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed 100% Standalone Web journey without Discord dependencies
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
