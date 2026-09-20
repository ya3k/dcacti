# ADR-003: Use React + Vite with Phaser 4 for the Discord Activity Client

**Status:** Accepted
**Date:** 2026-09-19

## Context

The game runs as an embedded Discord Activity (client-side iframe web surface)
requiring an 8×8 Match-3 board with interactive swap/match/cascade animations,
Pet/Boss sprites, particle VFX, and combat presentation, alongside non-gameplay
screens (menus, loadout, settings, collection viewers, Discord auth lifecycle).

An evaluation of frontend architectures was conducted:
- A full-stack framework like Next.js introduces Server-Side Rendering (SSR),
  Static Site Generation (SSG), Server Components, and App Router complexity.
  However, a Discord Activity client has no SSR, SSG, or SEO requirements, and
  authoritative game logic is fully owned by the ASP.NET Core backend.
- A pure canvas/PixiJS setup lacks built-in Scene management and declarative UI
  tooling needed for surrounding menus and platform integration.

## Decision

Adopt **React + TypeScript + Vite + Phaser 4** with the **Discord Embedded App SDK**,
**SignalR client**, and **Vitest**:
- **Phaser 4** is the primary game runtime and owns the canvas, game loop,
  Scenes (`BootScene`, `PreloaderScene`, `MainMenuScene`, `LobbyScene`,
  `BattleScene`, `ResultScene`), sprites, tweens, VFX, and board pointer input.
- **React + Vite** provides the client-side SPA application shell, handling HTML
  UI overlays, menus, settings, connection status, and the Discord SDK boundary.
- **Vite** serves as the frontend development server, bundler, and build system.

## Alternatives Considered

### Option A — Next.js + React + Phaser
Use Next.js for frontend structure. Rejected: Next.js server runtime, SSR, SSG,
and App Router features add unnecessary bundle/runtime overhead and complexity
inside a client-side Discord Activity iframe, where SEO and server-side rendering
are not required.

### Option B — Custom Canvas or Low-Level 2D Library (e.g. PixiJS)
Build a custom renderer or use a library without scene/animation management.
Rejected: Lacks built-in scene lifecycle, tween systems, and sprite pipelines
specifically required for Match-3 board interaction and combat presentation.

### Option C — React + Vite + Phaser 4 (Chosen)
A lean, client-side SPA architecture where Phaser 4 drives the game runtime and
canvas, while React provides a lightweight declarative UI and platform shell.

## Why

1. **Phaser is the primary game runtime:** Scene lifecycle, sprite rendering,
   tweens, and input handling are purpose-built for 2D game loops.
2. **Discord Activity is a pure client-side SPA:** The game runs entirely within
   an iframe talking to an authoritative ASP.NET Core backend over SignalR/REST;
   SSR/SEO are irrelevant.
3. **Vite is fast and lightweight:** Provides near-instant HMR, fast production
   bundling, simple configuration, and native Vitest integration.
4. **Natural Scene Architecture:** Phaser Scenes cleanly isolate initialization,
   preloading, menus, lobby, battle, and results.
5. **Clean React Boundary:** React remains dedicated to HTML UI, menus, modal
   overlays, and platform SDK lifecycle without interfering with the game canvas.
6. **Alignment with Best Practices:** Follows recommended Discord Activity and
   Phaser template architectures.

## Consequences

### Positive
- Simpler frontend runtime without SSR or Node server complexity.
- Clear Phaser Scene lifecycle (`BootScene` → `PreloaderScene` → `MainMenuScene`
  → `LobbyScene` → `BattleScene` → `ResultScene`).
- Minimal framework overhead and smaller client bundle footprint for fast
  Discord Activity load times.
- Clean separation of concerns between Phaser canvas, React DOM UI, and isolated
  transport services (`services/discord`, `services/realtime`, `services/api`).
- Seamless testing workflow via Vitest.

### Trade-offs
- No built-in SSR (irrelevant for embedded Discord Activities; any future
  public landing/marketing pages would be hosted separately).
- Bridging two UI layers (Phaser canvas and React DOM overlays) requires
  disciplined event/service boundaries (`ARCHITECTURE.md` §2.2).

## Related Documents

- `docs/00-overview/GDD.md` (header, "Core Technology")
- `docs/02-technical/TDD.md` (§1, §2.1)
- `docs/02-technical/ARCHITECTURE.md` (§1, §2.2, §3)
