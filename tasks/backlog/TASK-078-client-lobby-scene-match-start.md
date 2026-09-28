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
Status:            BACKLOG
Risk:              LOW
Priority:          HIGH
Primary Agent:     client
Supporting Agents: N/A
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, phaser/scenes, client/phaser-architecture, client/client-state-authority, testing/test-scenario-generation, quality/scope-validation
Dependencies:      TASK-075, TASK-076, TASK-077
```

---

## Objective

Implement `LobbyScene` as the client presentation layer responsible for battle preparation, loadout review, and match-start triggering (`TDD.md` §2.1, `ADR-003`). `LobbyScene` exposes the match-start trigger to player interaction, constructs a valid `BattleStartRequest` (`API_CONTRACTS.md` §3), delegates execution to `GameRuntimePort.startBattle` (`TASK-077`), and upon successful start orchestrates the transition to `BattleScene`.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Battle Loop (Single-Player vs. Boss) is IN scope
- `docs/02-technical/TDD.md` §2.1 — Phaser Scene Lifecycle (`BootScene` → `PreloaderScene` → `MainMenuScene` → `LobbyScene` → `BattleScene` → `ResultScene`) and `LobbyScene` ownership (battle preparation, loadout review, match start trigger)
- `docs/02-technical/ARCHITECTURE.md` §1, §2.2, §2.2.1 — Frontend layers, scene coordination through `GameRuntimePort`, transport independence (scenes never import SignalR or HTTP clients directly)
- `docs/02-technical/API_CONTRACTS.md` §3 — `POST /api/battle/start` request schema (`petId`, `bossId`, `cardLoadout`, `relicLoadout`), wire constraints, and error envelope
- `docs/02-technical/API_CONTRACTS.md` §5 — Collection read models (`PetResponse`, `CardResponse`, `RelicResponse`)
- `docs/01-game-design/BOSS_RULES.md` §6.4 — Canonical technical Boss Identity (e.g. `boss-hoa-long`)
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — Server-authoritative architecture
- `docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md` — Phaser scene lifecycle and runtime boundary
- `docs/03-decisions/ADR/ADR-011-loadout-and-ownership-boundaries.md` — Loadout and combat boundaries

---

## Scope

### In Scope

- Expose `startBattle(request: BattleStartRequest): Promise<void>` on the `GameRuntimePort` interface (`src/frontend/client/src/game/runtime/GameRuntimeEvents.ts`) so scenes coordinate battle start exclusively through the port.
- Implement `LobbyScene` (`src/frontend/client/src/game/scenes/LobbyScene.ts`):
  1. Presents battle preparation info (selected Pet, Boss target, equipped Card loadout, equipped Relic loadout) in logical game resolution (1280 × 720).
  2. Provides an interactive "Start Battle" trigger (button / pointer event) that constructs a valid `BattleStartRequest` (`API_CONTRACTS.md` §3).
  3. Dispatches the request through `runtime.startBattle(request)` and prevents duplicate in-flight triggers while the request is pending.
  4. On successful resolution, initiates scene transition to `BattleScene` (`this.scene.start('BattleScene')`).
  5. On rejection / failure, presents the error message / status feedback without crashing or changing scene.
- Update `GameConfig.ts` to register `LobbyScene` in the Phaser game configuration scene list (`[BootScene, PreloaderScene, LobbyScene, BattleScene]`).
- Update `PreloaderScene.ts` transition target from `BattleScene` to `LobbyScene`.
- Unit and integration tests covering `LobbyScene` lifecycle, button interaction, request construction, `GameRuntimePort.startBattle` delegation, success scene transition, error presentation, and double-click guard.

### Out of Scope

- **Client application-session / Discord OAuth establishment** — `ApplicationSession` continues to be tested with direct tokens or existing mocks (blocked by TASK-036).
- **Full visual art / sprite asset pipelines** — uses Phaser text, shapes, and placeholder layouts in logical game coordinates.
- **MainMenuScene and ResultScene** — deferred to their respective scene tasks.
- **Client-authoritative state or gameplay logic** — no validation of battle outcome, damage, or match mechanics.
- **Backend changes, documentation changes, or ADR modifications.**
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State

`GameRuntime.startBattle` was implemented in TASK-077, but `GameRuntimePort` (`GameRuntimeEvents.ts`) does not yet expose `startBattle`. `GameConfig.ts` currently registers `[BootScene, PreloaderScene, BattleScene]`, and `PreloaderScene` transitions directly to `BattleScene`, leaving `startBattle` with no scene caller. `ApiService` provides collection reads (`getPets`, `getCards`, `getRelics`, TASK-075) and battle start/result methods (`startBattle`, `getBattleResult`, TASK-076).

---

## Acceptance Criteria

- [ ] `GameRuntimePort` defines `startBattle(request: BattleStartRequest): Promise<void>`.
- [ ] `LobbyScene` exists, extends `Phaser.Scene`, and is registered in `GameConfig.ts` scene list.
- [ ] `PreloaderScene` transitions to `LobbyScene` upon asset loading completion.
- [ ] `LobbyScene` reads `GameRuntimePort` from the registry via `readRuntime(this)` and operates safely when no runtime is present.
- [ ] Triggering "Start Battle" in `LobbyScene` invokes `runtime.startBattle(request)` with a valid `BattleStartRequest` schema matching `API_CONTRACTS.md` §3.
- [ ] Multiple rapid clicks on the start trigger while a request is pending issue exactly one `startBattle` call.
- [ ] On successful `startBattle` resolution, `LobbyScene` transitions to `BattleScene`.
- [ ] On `startBattle` rejection/failure (e.g. `401 UNAUTHENTICATED`, `400 INVALID_LOADOUT`, transport failure), `LobbyScene` remains active, displays error feedback, and does not transition to `BattleScene`.
- [ ] `LobbyScene` imports no transport libraries (`@microsoft/signalr`, `fetch`, `HubConnection`) directly; all actions pass through `GameRuntimePort`.
- [ ] No client-authoritative gameplay logic or state calculation is introduced.
- [ ] All relevant tests pass at the required validation depth (`core/validation.md` §2).
- [ ] Quality review checklist passes (`quality/review.md` §1).
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[x] src/frontend/client/src/game/runtime/GameRuntimeEvents.ts (add startBattle to GameRuntimePort)
[x] src/frontend/client/src/game/scenes/LobbyScene.ts (new scene)
[x] src/frontend/client/src/game/scenes/PreloaderScene.ts (update transition target to LobbyScene)
[x] src/frontend/client/src/game/GameConfig.ts (register LobbyScene)
[x] src/frontend/client/tests/ (LobbyScene.test.ts, GameRuntime.test.ts)
[ ] src/backend/ (none)
[ ] docs/ (none)
```

---

## Implementation Notes

- Reference `readRuntime(this)` in `src/frontend/client/src/game/runtime/RuntimeRegistry.ts` for obtaining `GameRuntimePort` inside scenes.
- Follow the scene layout pattern in `BattleScene.ts` (using logical game constants from `GameViewport.ts` — `SAFE_AREA`, `GAME_WIDTH`, `GAME_HEIGHT`).
- In `LobbyScene`, default request payload can configure the MVP starter loadout:
  - `petId`: active/first owned Pet ID or configured test pet ID
  - `bossId`: `"boss-hoa-long"` (`BOSS_RULES.md` §6.4)
  - `cardLoadout`: 3 Basic Card IDs
  - `relicLoadout`: 3–5 Relic IDs
- Ensure button/trigger states are properly cleaned up in `shutdown()` to prevent listener leaks across scene restarts.

---

## Testing Requirements

### Required Verification

```text
[ ] Unit tests         — LobbyScene creation, rendering of loadout/boss info, start button interaction, request construction shape matching API_CONTRACTS.md §3, single-invocation guard during in-flight start, error feedback display on rejection, and transition to BattleScene on success.
[ ] Integration tests  — GameRuntimePort.startBattle invocation verified via mock/fake runtime port.
[ ] Gameplay scenarios — N/A (presentation & scene lifecycle only).
```

### Key Edge Cases

- `startBattle` rejects with `401 UNAUTHENTICATED`: scene remains on `LobbyScene`, displays authentication error, re-enables start button.
- `startBattle` rejects with `400 INVALID_LOADOUT`: scene remains on `LobbyScene`, displays loadout error.
- Rapid double-click on start trigger: second click is ignored while first request is unresolved.
- No runtime available: start trigger displays appropriate error feedback without throwing unhandled exceptions.
- Scene shutdown / restart: event listeners and UI containers are cleaned up.

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If task requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose
- If implementing requires inventing Discord OAuth flows or secrets handling: STOP per `AGENTS.md` §7/§20

---

## Completion Evidence

### Changed Files
- TBD

### Validation Results
- TBD

### Server Authority & Scope Verification
- [ ] Confirmed zero client-authoritative gameplay logic
- [ ] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
