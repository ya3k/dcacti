# Architecture

**Version:** 1.0
**Status:** Draft — depends on TDD.md §0 assumption (ASP.NET Core backend)

> This document answers: **"How is the software structured?"** It does not
> define game rules, API payload shapes, or database columns — see the
> respective owning documents.

---

# 1. Project Structure (Backend)

```text
src/
├── GameServer.Domain/          # Pure game logic, no framework dependencies
│   ├── Match3/                  # Board, Swap, Match, Cascade, Special Gems
│   ├── Elements/                 # Tương Khắc resolution
│   ├── Combat/                   # Damage pipeline, stats, status effects
│   ├── Pets/                     # Pet, Tier/Star/Level, Passive
│   ├── Cards/                    # Basic Card, Pet Skill Card
│   ├── Relics/                   # Relic, trigger evaluation
│   ├── Bosses/                   # Boss, Passive, Skill
│   └── Events/                   # Battle Event definitions (GAME_EVENTS.md)
│
├── GameServer.Application/      # Orchestrates Domain to resolve one action
│   ├── BattleResolution/         # Implements the Event Resolution pipeline
│   │                              #   (GAME_RULES.md §17) by calling Domain
│   └── UseCases/                 # StartBattle, ResolveSwap, CastCard, ...
│
├── GameServer.Infrastructure/   # Framework-specific implementations
│   ├── Redis/                    # Active BattleState read/write (REDIS_STATE.md)
│   ├── Postgres/                 # EF Core / repositories (DATABASE.md)
│   └── SignalR/                  # Hub implementation (SIGNALR_PROTOCOL.md)
│
└── GameServer.Api/              # Composition root
    ├── Hubs/                     # SignalR Hub (thin — delegates to Application)
    └── Controllers/              # REST endpoints (API_CONTRACTS.md)
```

```text
client/
├── public/
│   └── assets/
│
├── src/
│   ├── main.tsx                  # Application entry point
│   │
│   ├── app/                      # React root application shell & global styles
│   │   ├── App.tsx               # Application entry; mounts GameShell
│   │   ├── GameShell.tsx         # Owns the available viewport (canvas + overlay)
│   │   └── App.css               # Global game-viewport CSS foundation
│   │
│   ├── game/                     # Phaser 4 game runtime
│   │   ├── PhaserGame.ts         # Phaser game instance wrapper & React bridge
│   │   ├── PhaserGame.tsx        # Canvas mount component (created once)
│   │   ├── GameConfig.ts         # Phaser configuration (renderer, scale, scenes)
│   │   ├── GameViewport.ts       # Logical resolution, safe area, fit calculation
│   │   ├── runtime/              # Client runtime coordination boundary
│   │   │   ├── GameRuntime.ts          # Coordinates Phaser, SignalR, state, events
│   │   │   ├── GameRuntimeContext.tsx  # React bridge to the runtime (React never
│   │   │   │                           #   touches Phaser internals)
│   │   │   ├── GameRuntimeEvents.ts    # Runtime event contract & GameRuntimePort
│   │   │   └── RuntimeRegistry.ts      # Publishes the runtime to Phaser scenes
│   │   └── scenes/               # Phaser Scenes
│   │       ├── BootScene.ts      # Technical init (scales, asset pre-setup)
│   │       ├── PreloaderScene.ts # Asset loading & load progress presentation
│   │       ├── MainMenuScene.ts  # Main menu presentation
│   │       ├── LobbyScene.ts     # Battle preparation presentation
│   │       ├── BattleScene.ts    # Main battle presentation & animation runtime
│   │       └── ResultScene.ts    # Battle outcome presentation
│   │
│   ├── services/                 # Isolated external communication layers
│   │   ├── discord/              # Discord Embedded App SDK integration
│   │   │   └── DiscordService.ts # Discord Activity SDK lifecycle, auth initiation
│   │   ├── realtime/             # SignalR Hub client wrapper & event dispatcher
│   │   │   └── SignalRService.ts # SignalR connection lifecycle — the only
│   │   │                         #   SignalR implementation in the client
│   │   └── api/                  # REST API client
│   │
│   ├── state/                    # Client runtime state contract
│   │   └── GameRuntimeState.ts   # Technical/session state only — no gameplay state
│   │
│   └── ui/                       # React DOM UI components
│       └── components/           # Menus, overlays, HUD dialogs, settings,
│                                 #   StatusOverlay, ViewportDebugOverlay (dev only)
│
└── tests/                        # Vitest unit tests
```

---

# 2. Layers & Dependency Direction

## 2.1 Backend Layers

```text
Domain  ◀──  Application  ◀──  Infrastructure  ◀──  Api
```

1. **Domain** has zero dependencies on ASP.NET Core, Redis, PostgreSQL, or
   SignalR. It is the direct code expression of `GAME_RULES.md` and the
   domain rule documents — testable with plain unit tests, no I/O.
2. **Application** orchestrates Domain calls in the exact order defined by
   `GAME_RULES.md` §17 (Event Resolution Rules). It does not contain game
   rule logic itself — only sequencing and coordination.
3. **Infrastructure** implements persistence and transport. It depends on
   Domain/Application interfaces, never the other way around.
4. **Api** is the thin composition root: SignalR Hub methods and REST
   controllers translate wire messages into Application use-case calls and
   back into wire messages. No game logic lives here.

This direction must not be violated: Domain must never reference
Infrastructure or Api types.

## 2.2 Frontend Layers (Phaser-First with React Shell)

```text
Discord Activity
        │
        ▼
React + Vite (UI Shell & Platform Boundary)
        │
        ▼
Phaser 4 (Game Runtime & Scene Lifecycle)
        │
        ▼
Services (discord/ · realtime/ · api/)
        │
        ▼
Backend (SignalR Hub / REST API)
```

1. **React UI Shell:** Wraps the application, mounts the Phaser canvas, and
   manages HTML overlays, menus, settings, and Discord Activity SDK lifecycle.
2. **Phaser 4 Game Runtime:** Drives the game canvas, scene transitions
   (`BootScene` → `PreloaderScene` → `MainMenuScene` → `LobbyScene` →
   `BattleScene` → `ResultScene`), sprites, tweens, animations, and user pointer
   input on the board.
3. **Services Isolation:**
   - `services/discord/` isolates Discord Embedded App SDK interactions.
   - `services/realtime/` isolates SignalR connection handling and Battle Event
     subscription (`SIGNALR_PROTOCOL.md`).
   - `services/api/` isolates HTTP REST communication (`API_CONTRACTS.md`).
   - *Rule:* Discord SDK and SignalR transport details must never leak into
     individual Phaser game objects or scenes.
4. **Server-Authoritative Principle:** Client layers (React + Phaser) are
   strictly presentation and runtime components; authoritative state, math,
   and rules remain exclusively on the server (`GAME_RULES.md` §18, ADR-001).

### 2.2.1 Game Runtime Coordination

`game/runtime/GameRuntime.ts` is the client's runtime coordination boundary. It
is the single place that couples the game presentation to the realtime
transport, so neither React nor the Phaser scenes has to:

```text
BattleScene / React UI
        │  (GameRuntimePort)
        ▼
    GameRuntime            connection state · runtime lifecycle ·
        │                  server event subscription · scene coordination
        ▼
   SignalRService          the only SignalR implementation in the client
        │
        ▼
     BattleHub             thin transport boundary (ARCHITECTURE.md §1)
        │
        ▼
Application Runtime        connection lifecycle tracking only
```

**Rules:**

1. **Scenes depend on the runtime port, never on the transport.** A Phaser scene
   must not import the SignalR client or `HubConnection`; transport independence
   is what allows the game presentation to be tested and changed without a live
   hub.
2. **React and Phaser do not reach into each other.** React reads runtime status
   through `GameRuntimeContext`; Phaser reads the shared runtime from its
   registry (`RuntimeRegistry.ts`). React never manipulates Phaser internals and
   Phaser never manipulates React components.
3. **The runtime coordinates; it does not compute.** It owns connection state,
   runtime lifecycle, server event subscription and scene lifecycle
   coordination only. It performs no gameplay calculation — no damage, match,
   combo, cascade, passive, or power (`GAME_RULES.md` §18, ADR-001).
4. **Battle events pass through unchanged.** `GameRuntime` forwards
   `ReceiveEvents` batches exactly as the server sent them. It does not
   reorder (which would break the resolution order guaranteed by
   `SIGNALR_PROTOCOL.md` §3), filter, or interpret them.
5. **Client runtime state is technical only.** `state/GameRuntimeState.ts`
   carries connection, session, runtime and synchronization status. It carries
   no gameplay state: authoritative `BattleState` is server-owned
   (`GAME_STATE.md` §2, ADR-001, ADR-005) and is not modelled client-side.
   The client holds a synchronized presentation copy of the server's state
   (`GAME_STATE.md` §2.0, §4) and never authors, adjusts, or recomputes it.
6. **One runtime, one connection.** `SignalRService` is a process-wide
   singleton and `GameRuntime.initialize()` is idempotent, so React
   StrictMode's development double-invocation cannot create a second SignalR
   connection or a second Phaser instance.

`GameRuntime` coordinates the initial state subscription
(`SIGNALR_PROTOCOL.md` §4): it receives the server-pushed
`BattleStateUpdated` payload and exposes it to the scenes through its port,
exactly as it forwards `ReceiveEvents` (rule 4). The runtime does not derive,
extend, or validate gameplay meaning from that payload.

The runtime foundation establishes these boundaries. The initial
state-delivery contract (`SIGNALR_PROTOCOL.md` §4) and the state it carries
(Battle State Foundation — `GAME_STATE.md` §2.0) are implemented: joining a
battle's group (`JoinBattle`, `SIGNALR_PROTOCOL.md` §1.2) triggers the server's
`BattleStateUpdated` push, and `GameRuntime` stores that payload as the client's
synchronized copy and exposes it through its port.

Battle resolution, the client → server gameplay methods (`Swap`, `CardCast`,
`PetSkillCast` — `SIGNALR_PROTOCOL.md` §2), and reconnect/resync snapshot
recovery (`SIGNALR_PROTOCOL.md` §7, ADR-008) are not implemented yet.

### 2.2.2 Game Viewport & Scaling

The client behaves as a game, not as a scrollable web page. The document never
scrolls; the available viewport *is* the game surface.

```text
Discord Activity / Browser viewport
        │
        ▼
Application Root (html / body / #root: 100% × 100%, overflow: hidden)
        │
        ▼
GameShell (owns 100% × 100%, never larger; positioning context)
        ├── Phaser canvas   (position: absolute; inset: 0)
        └── React overlay   (position: absolute; inset: 0; no document size impact)
```

**Logical vs. physical space.** The game is authored against a fixed **logical
game resolution of 1280 × 720 (16:9)** defined in `game/GameViewport.ts`. Game
coordinates are always logical; client code must not derive game logic or layout
from `window.innerWidth` / `window.innerHeight`.

```text
Logical game space 1280 × 720
        │  Phaser Scale Manager (FIT + CENTER_BOTH)
        ▼
Actual viewport (1920×1080, 1366×768, 1024×768, 800×600, …)
```

**Rules:**

1. **Scaling is owned by Phaser's Scale Manager** (`Phaser.Scale.FIT` with
   `autoCenter: CENTER_BOTH`), configured in `game/GameConfig.ts`. The canvas
   must never be resized manually, and the game instance must never be recreated
   on resize.
2. **Aspect ratio is preserved.** The logical space is letterboxed inside the
   viewport rather than stretched; unsupported or portrait aspect ratios stay
   centered and undistorted. A dedicated portrait layout is a future gameplay/UI
   concern, not part of the viewport foundation.
3. **No page scrolling.** `overflow: hidden` on the document and shell, and no
   `overflow: auto` on the game container. Small viewports scale the game down;
   they never introduce scrollbars.
4. **The React overlay never affects document size.** Overlay layers are
   absolutely positioned and pass pointer events through except where a child
   explicitly opts in.
5. **Safe area.** `GameViewport.ts` defines a configurable safe margin inside the
   logical space for future HUD content, so important presentation is not placed
   against the viewport edge. No gameplay HUD exists yet.
6. **Presentation is independent of gameplay rules.** Responsive behavior scales
   the presentation only; it must never change the logical board, its cell count,
   or any game rule.

`GameViewport.ts` also exports `fitWithinViewport()` — a pure helper used for
reporting and the development-only viewport overlay. It is not part of the
runtime resize path, which remains the Scale Manager.

## 2.3 Discord SDK & Authentication Boundaries

```text
Discord Client
      │
      ▼
React + Vite (Frontend)
      │
      │ Discord Embedded App SDK (services/discord/DiscordService.ts)
      │
      │ authorization code / auth data
      ▼
ASP.NET Core Backend (POST /api/auth/discord)
      │
      │ server-side Discord OAuth / token exchange (using Client Secret)
      ▼
Discord OAuth API
      │
      ▼
ASP.NET Core Backend
      │
      │ authenticated application session
      ▼
REST API / SignalR Hub (BattleHub)
```

1. **SDK Location:** The Discord Embedded App SDK runs strictly on the frontend
   within `client/src/services/discord/DiscordService.ts`. It never runs on the
   backend.
2. **Client Secret Security:**
   - **Frontend:** May contain the `Discord Client ID`. Must NEVER contain or
     expose the `Discord Client Secret`.
   - **Backend:** Manages `Discord Client ID` and `Discord Client Secret`
     through secure server-side configuration/environment secrets.
3. **Authentication Boundary:**
   - Frontend initiates Discord SDK authentication and passes the authorization
     code to the backend (`POST /api/auth/discord`).
   - Backend validates credentials, exchanges the code with the Discord OAuth
     API server-side, identifies/creates the player, and returns an application
     session token.
4. **SignalR Authentication:**
   - SignalR Hub (`BattleHub`) connects using the authenticated application
     session, never by directly invoking the Discord SDK.
5. **Architectural Isolation:**
   - Discord platform details remain strictly behind `DiscordService.ts`.
   - No direct coupling exists between Discord SDK and `BattleScene`, `Domain`,
     `Combat`, `Match3`, `Redis`, or `PostgreSQL`.

---

# 3. Major Components

```text
Component                    Owning Layer      Responsibility
---------------------------  ----------------  -----------------------------
Match3Engine                  Domain            MATCH3_RULES.md
ElementResolver                Domain            ELEMENT_RULES.md
DamagePipeline                 Domain            COMBAT_RULES.md
PassiveTracker                 Domain            PASSIVE_RULES.md
RelicTriggerEngine              Domain            RELIC_RULES.md
BossController                  Domain            BOSS_RULES.md
BattleEventBus                  Domain            GAME_EVENTS.md (definitions)
BattleResolutionService          Application       GAME_RULES.md §17 sequencing
RuntimeService                    Application       Hub connection lifecycle only —
                                                    no gameplay (§2.2.1)
BattleStateRepository (Redis)     Infrastructure    REDIS_STATE.md
PersistenceRepository (Postgres)  Infrastructure    DATABASE.md
BattleHub                         Api               SIGNALR_PROTOCOL.md (thin —
                                                    no gameplay rules, §2.1)
Collection/Result Controllers      Api               API_CONTRACTS.md
PhaserGame / Scenes          Client (Game)     Canvas rendering & scene lifecycle
GameConfig / GameViewport     Client (Game)     Scale Manager config; logical resolution
                                                (1280×720), safe area (§2.2.2)
GameRuntime                   Client (Game)     Coordinates Phaser, transport & runtime
                                                state; no gameplay (§2.2.1)
DiscordService                Client (Services) Discord SDK lifecycle & auth boundary
RealtimeService               Client (Services) SignalR connection & event dispatch
ApiService                    Client (Services) REST API communication
GameShell / React Overlays    Client (UI)       Owns the viewport; HTML overlays & menus
```

---

# 4. Communication Between Components

1. `BattleHub` receives a client action (e.g. Swap) → calls
   `BattleResolutionService`.
2. `BattleResolutionService` loads current `BattleState` from
   `BattleStateRepository`, runs the Domain engines in the fixed order from
   `GAME_RULES.md` §17, collects the resulting Battle Events, and writes the
   updated `BattleState` back to `BattleStateRepository`.
3. `BattleResolutionService` returns the ordered event list to `BattleHub`,
   which pushes them to the client(s) per `SIGNALR_PROTOCOL.md`.
4. On battle end, `BattleResolutionService` calls `PersistenceRepository` to
   write the durable result (`DATABASE.md`) and instructs
   `BattleStateRepository` to clear the active state (`REDIS_STATE.md`).

No component other than `BattleResolutionService` writes to either
repository during an active battle — this keeps the resolution order
enforceable in one place.

---

# 5. Anti-Overengineering Notes

1. No generic "plugin system" for Pets/Cards/Relics/Bosses is introduced —
   each is a concrete Domain type with data-driven configuration (numbers
   only), per `AGENTS.md` §2.4. A plugin/scripting layer is not required by
   MVP scope.
2. No message queue / event sourcing infrastructure — Battle Events are an
   in-process list returned by `BattleResolutionService` for one resolved
   action; they are not persisted as an event log in MVP.
3. No CQRS split, no separate read-model store — PostgreSQL serves both
   reads and writes for persistent data; Redis serves both reads and writes
   for active state.
4. Single ASP.NET Core service (per `TDD.md` §7 Non-Goals) — no service
   boundary between Match-3, Combat, and Boss logic; they are Domain
   modules within one process.
5. Minimal client state management — no heavy state stores (Redux, Zustand,
   MobX, TanStack Query, XState) for bootstrap. React UI state is kept minimal;
   gameplay state is owned authoritatively by the server and rendered via
   Phaser Scenes.
