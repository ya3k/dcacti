---
name: react-phaser-boundary
description: "Use this skill when implementing or modifying the boundary between React UI components and the Phaser 4 Game Runtime in DCacti. Covers GameShell, PhaserGame mounting, GameRuntimeContext, RuntimeRegistry, and preventing cross-layer mutation."
---

# skills/client/react-phaser-boundary.md — Skill: React ↔ Phaser Boundary

**Version:** 1.0  
**Status:** Binding  
**Scope:** Frontend boundary coordination between React application UI and Phaser 4 canvas presentation.

> Provides the procedure and architectural rules for coordinating React DOM UI (shell, lobby, menus, overlays, Discord Activity SDK) and the Phaser 4 Game Runtime without tight coupling or direct internal cross-mutation.

---

## Purpose

Define the communication and lifecycle contracts across the React ↔ Phaser boundary in DCacti, ensuring:
1. React owns the DOM UI, Discord Activity SDK, and application routing.
2. Phaser owns the 2D canvas, scene lifecycle, and gameplay visual presentation.
3. React and Phaser never reach directly into each other's internal state.

---

## When to Use

- Implementing or updating React UI components that overlay or interact with the game canvas.
- Mounting or unmounting the Phaser game instance via `PhaserGame.tsx`.
- Passing user actions from React menus (e.g. Lobby readiness, loadout selections) into the game runtime.
- Emitting presentation events from Phaser scenes for React overlay display (e.g. status banners, connection loss).

---

## When Not to Use

- Writing standalone React components that do not interface with the game runtime (standard React).
- Implementing purely internal Phaser scene rendering, tweens, or gem animations (use `client/phaser-architecture` or `client/phaser-match3`).
- Processing backend SignalR wire protocols (handled in `services/realtime/SignalRService.ts`).

---

## Authoritative Sources

- `docs/02-technical/ARCHITECTURE.md` §1, §2.2, §2.2.1 — Frontend architecture and Game Runtime coordination
- `docs/02-technical/TDD.md` §2.1 — Client responsibility boundaries
- `docs/03-decisions/ADR/ADR-003*` — Phaser 4 as presentation engine

---

## Architectural Rules

```text
┌────────────────────────────────────────────────────────┐
│                   React UI Shell                       │
│  (App.tsx, GameShell.tsx, Lobby, Menus, HTML Overlays) │
└──────────────────────────┬─────────────────────────────┘
                           │ reads via GameRuntimeContext
                           ▼
┌────────────────────────────────────────────────────────┐
│           GameRuntime (game/runtime/GameRuntime.ts)     │
│   - Runtime Lifecycle & Port                           │
│   - SignalR Service Coordination                       │
└──────────────────────────▲─────────────────────────────┘
                           │ accessed via RuntimeRegistry
┌──────────────────────────┴─────────────────────────────┐
│                 Phaser Game Runtime                    │
│   (PhaserGame.ts, Scenes: Boot, Preloader, BattleScene)│
└────────────────────────────────────────────────────────┘
```

1. **Strict Separation of Concerns:**
   - **React owns:** Discord Activity lifecycle, Lobby UI, Loadout selection, Modal dialogs, Settings, HTML HUD overlays, Viewport wrapper (`GameShell.tsx`).
   - **Phaser owns:** BattleScene, 8x8 Board canvas, Gem rendering, Battle animations, Particle VFX, Gameplay pointer input.
2. **No Direct DOM ↔ Scene Coupling:**
   - React components must **never** call Phaser scene methods directly (`scene.add...`, `scene.boardView...`).
   - Phaser scenes must **never** call React state setters (`setState`, React hooks).
3. **Bridge via `GameRuntime` and `GameRuntimeContext`:**
   - React components consume runtime status via `useGameRuntime()` from `GameRuntimeContext.tsx`.
   - Phaser scenes obtain the shared runtime port via `RuntimeRegistry.getInstance().getRuntime()` or scene injection in `init()`.
4. **Idempotent Single Instance:**
   - The Phaser canvas is created once inside `PhaserGame.tsx`. React `StrictMode` mount/unmount cycles must not spawn multiple Phaser instances or multiple SignalR connections (`ARCHITECTURE.md` §2.2.1).

---

## Procedure

1. **Mounting the Canvas:**
   - Mount the canvas inside `src/frontend/client/src/game/PhaserGame.tsx`.
   - Position absolute with `inset: 0` inside `GameShell.tsx` to maintain the 16:9 aspect container (`GameViewport.ts`).
2. **Triggering Runtime Actions from React:**
   - Call methods exposed on `GameRuntime` (e.g. `runtime.connect()`, `runtime.sendAction()`).
   - Do not reach into Phaser game object instances.
3. **Exposing Phaser Presentation State to React:**
   - Phaser scenes publish technical/lifecycle notifications to `GameRuntime` event emitters (`GameRuntimeEvents.ts`).
   - React hooks subscribe to these runtime events to toggle HTML dialogs or banners.
4. **Handling Teardown / Cleanup:**
   - On unmount, invoke `runtime.dispose()` or scene shutdown cleanly.
   - Clean up event listeners in React `useEffect` returns and Phaser scene `shutdown` / `destroy` events.

---

## Do / Don't

| Do | Don't |
|---|---|
| Use `GameRuntimeContext` to read technical connection/session state in React. | Reach into Phaser scenes from React to read game variables. |
| Use `RuntimeRegistry` to access `GameRuntimePort` within Phaser scenes. | Import React components or React contexts inside Phaser scenes. |
| Render responsive HTML menus in React layered above the Phaser canvas. | Implement HTML forms or complex menus manually inside Phaser graphics. |
| Keep the 8x8 Match-3 board strictly inside Phaser. | Attempt to render board cells as React DOM elements. |

---

## Stop Conditions

- Implementation requires React components to authoritatively compute game rules or state.
- React components attempt to directly mutate Phaser game objects or vice versa.
- Ambiguity in whether a UI element belongs in React (DOM overlay) vs Phaser (canvas presentation).

---

## Traceability

- **Reads:** `docs/02-technical/ARCHITECTURE.md`, `docs/02-technical/TDD.md`, `docs/03-decisions/ADR/ADR-003*`
- **Used by:** `client` agent, `orchestrator` agent
- **Related Skills:** `client/phaser-architecture`, `client/client-event-projection`, `phaser/scenes`
