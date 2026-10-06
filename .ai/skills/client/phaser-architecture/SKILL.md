---
name: phaser-architecture
description: "Use this skill when developing or structuring Phaser 4 scenes, view components, viewports, scaling, and asset pipelines in DCacti. Covers BootScene, PreloaderScene, BattleScene, GameViewport, GameRuntimePort integration, and resource cleanup."
---

# skills/client/phaser-architecture.md — Skill: DCacti Phaser Architecture

**Version:** 1.0  
**Status:** Binding  
**Scope:** Architecture and structural conventions for Phaser 4 scenes and view components in DCacti.

> Defines architectural standards for DCacti Phaser scenes, View hierarchies, resolution scaling (1280x720 FIT), asset loading pipelines, and runtime port integration.

---

## Purpose

Standardize Phaser 4 scene implementation in DCacti to ensure:
1. Scenes strictly follow the DCacti scene flow (`BootScene` → `PreloaderScene` → `BattleScene`).
2. View components are modular and decoupled from network transports.
3. Canvas scaling adheres to the logical 1280x720 16:9 safe viewport.
4. Scene resources and event listeners are properly disposed of to prevent memory leaks.

---

## When to Use

- Creating or modifying Phaser scenes in `src/frontend/client/src/game/scenes/`.
- Adding visual sub-components / View classes (e.g. `BoardView`, `PetView`, `BossView`).
- Configuring the scene lifecycle: the methods Phaser invokes by name (`init`, `preload`, `create`, `update`) and the teardown **events** the engine emits (`Phaser.Scenes.Events.SHUTDOWN` / `DESTROY`).
- Integrating Phaser scenes with `GameRuntimePort`.
- Implementing canvas resizing, viewport calculations, or safe area letterboxing.

---

## When Not to Use

- Generic Phaser API syntax lookup (use upstream skills: `phaser/scenes`, `phaser/tweens`, `phaser/sprites-and-images`).
- Implementing React DOM overlays (use `client/react-phaser-boundary`).
- Defining authoritative game logic or damage formulas (belongs in backend domain docs).

---

## Authoritative Sources

- `docs/02-technical/ARCHITECTURE.md` §1, §2.2 — Client structure and scene hierarchy
- `docs/02-technical/TDD.md` §2.1 — Phaser scene lifecycle and viewport standards
- `docs/03-decisions/ADR/ADR-003*` — Phaser 4 presentation engine

---

## Architecture Standards

### 1. Scene Flow and Responsibilities

```text
BootScene.ts        → Technical initialization, Scale Manager config, boot assets
     ↓
PreloaderScene.ts   → Load game texture atlases, sprites, audio; display loading progress
     ↓
BattleScene.ts      → Main battle presentation: board, entities, animations, VFX
```

- **BootScene:** Configures viewport, initializes `GameViewport.ts`, preloads minimal preloader assets, transitions immediately to `PreloaderScene`.
- **PreloaderScene:** Loads textures, animations, fonts, and sound effects using Phaser's loader plugin. Displays visual progress bar. Transitions to `BattleScene` once assets are cached.
- **BattleScene:** Orchestrates presentation components. Consumes events from `GameRuntimePort`.

### 2. View Component Pattern

Rather than putting all rendering code into monolithic scene files, structure scenes using View classes:

```text
BattleScene
├── BoardView             (Match-3 8x8 grid & gem sprites)
├── PlayerView            (Player avatar, HP, Power gauge)
├── PetView               (Pet sprite, idle animation, skill cast effects)
├── BossView              (Boss sprite, HP bar, intent indicators)
├── CombatEffectsView     (Damage numbers, floating text, particles)
└── BattleHUDView         (In-game status, turn indicator, phase notifications)
```

- Each View receives a reference to the `Phaser.Scene` and container/depth layer.
- Views expose declarative update methods (e.g., `updateHealth(current, max)`, `animateGemSwap(from, to)`).

### 3. Viewport and Scaling Conventions

- **Logical Resolution:** `1280 x 720` (16:9 standard landscape).
- **Scale Mode:** `Phaser.Scale.FIT` with `Phaser.Scale.CENTER_BOTH`.
- **Coordinate Reference:** Use `GameViewport.ts` constants and offsets. Do not hardcode arbitrary window coordinates (`window.innerWidth`).

### 4. Runtime Port Integration & Transport Independence

- Scenes must **never** import `SignalRService` or `@microsoft/signalr`.
- Retrieve `GameRuntimePort` from `RuntimeRegistry.getInstance().getRuntime()` or scene `init(data)`.
- Subscribe to runtime events in `create()` and release the subscriptions in the teardown registered on the `Phaser.Scenes.Events.SHUTDOWN` / `DESTROY` events.

### 5. Scene Lifecycle: Methods vs Events

Phaser invokes exactly four scene methods **by name** — `init`, `preload`, `create`,
`update` (`SceneManager.bootScene` / `SceneManager.create`) — and no others.
`Phaser.Scene` declares **no** `shutdown` method, and neither `shutdown()` nor
`destroy()` is an engine hook.

Teardown reaches a scene only as an **event on its own emitter**:

```text
Phaser.Scenes.Systems#shutdown   → scene.events.emit(Phaser.Scenes.Events.SHUTDOWN)
Phaser.Scenes.Systems#destroy    → scene.events.emit(Phaser.Scenes.Events.DESTROY)
```

`SHUTDOWN` is emitted for a queued `stop` and when a running scene is
restarted/started again; `DESTROY` when the scene (or the game) is destroyed.
Declaring a method named `shutdown()` without registering it on those events
means the cleanup never runs — and a scene that is stopped keeps its
subscriptions pointed at game objects `DisplayList#shutdown` has already
destroyed.

---

## Resource Lifecycle & Cleanup

```js
// Standard Scene Cleanup Pattern
create() {
    this.runtime = RuntimeRegistry.getInstance().getRuntime();
    this.unsubscribe = this.runtime.onBattleEvents((batch) => this.handleEvents(batch));

    // Detach before attaching: `once` alone would leave this run's *unfired*
    // DESTROY handler behind, so a scene that is stopped and started again
    // would stack another handler on every run.
    this.events.off(Phaser.Scenes.Events.SHUTDOWN, this.cleanup, this);
    this.events.off(Phaser.Scenes.Events.DESTROY, this.cleanup, this);
    this.events.once(Phaser.Scenes.Events.SHUTDOWN, this.cleanup, this);
    this.events.once(Phaser.Scenes.Events.DESTROY, this.cleanup, this);
}

cleanup() {
    if (this.unsubscribe) {
        this.unsubscribe();
        this.unsubscribe = null;
    }
    this.tweens.killAll();
    this.time.removeAllEvents();
}
```

Rules for the teardown:

```text
1. Register it on both events in create(), before the resources it releases are
   created, and `off` the pair first so the registration is idempotent.
2. Release only what the scene owns: its own subscriptions, timers, tweens,
   caches and per-run guards. Never the runtime's synchronized state, the
   preserved loadout carrier (ADR-022), or any other scene's/global state.
3. Make it idempotent and reference-safe: Phaser destroys the scene's game
   objects itself (`DisplayList#shutdown` handles the same event), so the
   teardown runs against already-destroyed children. Every handle must be
   null-safe, and re-running it must be harmless.
4. Cancel asynchronous continuations: a promise the scene started (an API read,
   say) can settle after teardown or after the instance has been reused. Capture
   a per-run token in create(), bump it in the teardown, and check it before the
   continuation writes to any game object.
```

---

## Do / Don't

| Do | Don't |
|---|---|
| Break scene presentation into modular View classes. | Write 2,000-line monolithic scene classes containing all game rendering. |
| Use 1280x720 logical coordinates mapped via `GameViewport.ts`. | Use raw DOM pixel coordinates that break across resolutions. |
| Consume `GameRuntimePort` for server events. | Import SignalR Hub connections directly into scenes. |
| Clean up tweens, timers, caches, and runtime subscriptions in the teardown registered on `Phaser.Scenes.Events.SHUTDOWN` / `DESTROY`. | Leave dangling event listeners that cause memory leaks across scene reloads. |
| Treat `shutdown()` / `destroy()` as event names, and register your own cleanup method on them. | Assume a scene method named `shutdown()` is a Phaser lifecycle hook — the engine never calls it. |

---

## Stop Conditions

- Implementation requires Phaser scenes to perform network I/O or manage WebSocket connections directly.
- Required visual assets or animation frames are not defined or missing from preloader.
- Scene architecture conflicts with `ARCHITECTURE.md` §2.2.

---

## Traceability

- **Reads:** `docs/02-technical/ARCHITECTURE.md`, `docs/02-technical/TDD.md`, `docs/03-decisions/ADR/ADR-003*`
- **Used by:** `client` agent, `orchestrator` agent
- **Related Skills:** `client/react-phaser-boundary`, `client/phaser-match3`, `client/phaser-battle-presentation`, `phaser/scenes`, `phaser/game-setup-and-config`
