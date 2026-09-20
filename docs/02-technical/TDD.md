# Technical Design Document (TDD)

**Version:** 1.0
**Status:** Draft — contains ASSUMPTIONs pending confirmation (see §0)

> This document answers: **"What technical approach are we using to
> implement the game?"** It does not define exact game rules (see
> `GAME_RULES.md` and domain rules), exact API contracts (see
> `API_CONTRACTS.md`/`SIGNALR_PROTOCOL.md`), exact database columns (see
> `DATABASE.md`), or class-level implementation.

---

# 0. Assumptions Requiring Confirmation

No prior document explicitly confirmed the backend language/framework. The
following is assumed from the file names already agreed for this doc set
(`SIGNALR_PROTOCOL.md`, `REDIS_STATE.md`, `DATABASE.md` = PostgreSQL) and is
marked **ASSUMPTION** per `AGENTS.md` §6 until confirmed or recorded in an
ADR:

```text
ASSUMPTION: Backend = C# / ASP.NET Core (SignalR is a .NET-native
technology; assuming .NET avoids adding a cross-stack SignalR client
implementation on the server side).
```

If this is wrong, only this document and the technical docs that depend on
language-specific detail need to change — game design docs are unaffected.

---

# 1. Technology Stack

```text
Client        React + TypeScript + Vite + Phaser 4 + Discord Embedded App SDK,
               running inside a Discord Activity (iframe), with Vitest for testing
Realtime       SignalR (ASP.NET Core Hub)      [ASSUMPTION, see §0]
Backend        ASP.NET Core (C#)               [ASSUMPTION, see §0]
Active State   Redis                            (per MVP_SCOPE.md §1)
Persistence    PostgreSQL                       (per MVP_SCOPE.md §1)
```

Frontend architecture is a static SPA / Discord Activity client built with
Vite. There is no requirement for Server-Side Rendering (SSR), Static Site
Generation (SSG), SEO, Next.js App Router, Next.js Server Components, or
Next.js API Routes.

No microservices, Kubernetes, or Kafka — explicitly excluded
(`MVP_SCOPE.md` §2).

---

# 2. Responsibility Split

## 2.1 Client (React + Vite + Phaser 4)

The frontend uses a **Phaser-first** architecture with a **React + Vite**
application shell:

```text
Discord Activity
        │
        ▼
React + Vite (Application & UI Shell)
        ├── HTML UI & Overlays
        ├── Menus & Settings
        ├── Connection status & Loading UI
        └── Platform / Discord SDK integration boundary
        │
        ▼
Phaser 4 (Game Runtime & Presentation)
        ├── Scenes & Game Objects
        ├── Board & Gem presentation
        ├── Input & Pointer handling
        ├── Animation, Tweens & Particle effects
        └── Battle visual presentation
        │
        ▼
SignalR / REST API Client (Isolated Services)
```

### Phaser Scene Lifecycle

Phaser Scenes are the primary game presentation architecture:

```text
BootScene
    ↓
PreloaderScene
    ↓
MainMenuScene
    ↓
LobbyScene
    ↓
BattleScene
    ↓
ResultScene
```

- **BootScene:** Technical initialization only (scales, engine config).
- **PreloaderScene:** Asset loading and loading progress presentation.
- **MainMenuScene:** Main game menu presentation and navigation.
- **LobbyScene:** Battle preparation, loadout review, and match start trigger.
- **BattleScene:** In-battle visual presentation, board animations, VFX, and
  Phaser runtime.
- **ResultScene:** Battle outcome presentation (Victory/Defeat, summary).

### Server-Authoritative Boundary

Phaser Scenes and React UI are strictly **presentation and runtime
components**, not authoritative game logic:
- The server authoritatively determines: board state, valid swaps, match
  detection, cascade results, combo, Match count, passive progression,
  resource generation, Power, damage, HP, Five Elements modifiers, boss
  actions, rewards, and battle outcome (`GAME_RULES.md` §18, ADR-001).
- The client sends **action requests** only: Swap, Card Cast, Pet Skill Cast.
- The client never independently calculates authoritative gameplay results.
  It may optimistically check swap adjacency for local pointer feedback, but
  all visual mutations reflect server-pushed Battle Events (`GAME_EVENTS.md`).

### React Responsibilities & Boundaries

- **React owns:** Application shell, Discord Activity integration boundary,
  HTML overlays, menus, settings, connection status UI, loading fallback, and
  non-canvas UI.
- **React does NOT own:** Phaser game loop, 8x8 Match-3 board state,
  authoritative battle state, combat calculations, game simulation, or canvas
  animations. The 8x8 board is not represented as React DOM components.

### Presentation Resolution & Scaling

The client targets a fixed **logical game resolution of 1280 × 720 (16:9)**;
Phaser's Scale Manager (`FIT` + `CENTER_BOTH`) maps that logical space onto the
actual Discord Activity / browser viewport. The Activity behaves as a game
surface, not a scrollable web page: the document never scrolls, aspect ratio is
preserved (letterboxed, never stretched), and the React overlay never affects
document size. Game coordinates are logical and must not be derived from
`window.innerWidth` / `window.innerHeight`.

This is a **presentation** concern only. It must never alter the board's logical
cell count or any game rule — responsive behavior scales the presentation, not
the gameplay (`ARCHITECTURE.md` §2.2.1).

### Service Boundaries

- `services/discord/`: Encapsulates Discord Embedded App SDK lifecycle, client
  context acquisition, and authentication initiation (`DiscordService.ts`).
- `services/realtime/`: Encapsulates SignalR Hub connection and event dispatch.
- `services/api/`: Encapsulates REST API calls.
- Discord SDK and SignalR transport details must remain isolated and never leak
  directly into Phaser gameplay objects.

### Discord Embedded App SDK & Authentication Architecture

1. **Frontend SDK Ownership:**
   - Discord Embedded App SDK runs exclusively on the frontend
     (`client/src/services/discord/DiscordService.ts`).
   - Responsible for SDK initialization, fetching Discord Activity context,
     and initiating authentication flows.
   - The SDK does NOT run on the backend and is NOT imported into Phaser
     Scenes or gameplay logic.

2. **Backend Authentication & Token Exchange:**
   - The ASP.NET Core backend owns server-side Discord OAuth token exchange and
     verification (`POST /api/auth/discord`, `API_CONTRACTS.md` §2).
   - Exchange flow:
     ```text
     Discord Client
          │
          ▼
     Frontend (React + Vite)
          │  (Discord Embedded App SDK acquires authorization code)
          ▼
     ASP.NET Core Backend (POST /api/auth/discord)
          │  (Server-side token exchange using Client Secret)
          ▼
     Discord OAuth API
          │  (Returns Discord user identity)
          ▼
     ASP.NET Core Backend
          │  (Issues authenticated application session)
          ▼
     REST API & SignalR Hub
     ```

3. **Client Secret Security Boundary:**
   - **Frontend:** May contain `Discord Client ID`. MUST NEVER contain
     `Discord Client Secret`.
   - **Backend:** Contains `Discord Client ID` and `Discord Client Secret`
     stored securely in server configuration / environment secrets.
   - Client Secret is never exposed via browser bundles, Vite env vars,
     React config, Phaser code, public assets, or Git.

4. **SignalR Authentication Sequence:**
   - SignalR Hub connections are established only *after* the application
     session is authenticated.
   - `BattleHub` and backend game domain logic interact with authenticated
     application user identities, never with Discord SDK internals.

5. **Separation of Concerns:**
   - Strict boundary: Discord SDK → `DiscordService` → Application Auth
     Session → REST / SignalR → Game Backend.
   - No direct dependency between Discord SDK and BattleScene, Battle Domain,
     Combat, Match-3, Redis, or PostgreSQL.

## 2.2 Backend (ASP.NET Core)

- Owns all game logic: Match-3 resolution, Combo, Element Modifier,
  Passive/Relic triggers, Damage calculation, Boss mechanics
  (`GAME_RULES.md` §17, Event Resolution Rules).
- Validates every client action before applying it.
- Is the sole writer of Redis active battle state and PostgreSQL persistent
  data.
- Emits Battle Events over SignalR to the connected client(s) for that
  battle.

---

# 3. High-Level Runtime Model

```text
Client                     Backend (ASP.NET Core)
------                     -----------------------
Swap request  ──SignalR──▶ Validate (MATCH3_RULES.md)
                            Resolve board / cascade / combo
                            Charge Passive, trigger Relics
                            Calculate damage (COMBAT_RULES.md)
                            Apply Boss response
                            Persist new BattleState → Redis
              ◀──SignalR── Emit ordered Battle Events
Render events
```

This mirrors the Event Resolution Rules in `GAME_RULES.md` §17 exactly — the
backend is not free to reorder those steps.

---

# 4. Persistence Strategy

```text
PostgreSQL   Durable data: Player account, Pet Collection, Card/Relic
              Collection, Battle Results, Rewards (DATABASE.md)
Redis        Active, transient battle state only — exists for the lifetime
              of a battle (REDIS_STATE.md)
```

1. A battle's authoritative live state lives in Redis, not in memory on a
   single server instance, so a reconnect or instance restart can recover
   state (see `REDIS_STATE.md` for recovery detail).
2. On battle end (`BattleWon` / `BattleLost`), the result and any reward
   changes are written to PostgreSQL and the Redis battle state is cleared
   (TTL or explicit delete — see `REDIS_STATE.md`).
3. PostgreSQL is never queried on the hot path of resolving a single Swap —
   only Redis is read/written during active battle resolution.

---

# 5. Realtime Model

SignalR is used for all in-battle communication (Swap, Card Cast, Pet Skill
Cast requests; all Battle Events). REST (`API_CONTRACTS.md`) is used only
for non-realtime concerns: starting/ending a battle session, fetching
collections, fetching battle history. Exact Hub methods and message shapes:
see `SIGNALR_PROTOCOL.md`.

---

# 6. Determinism & RNG

1. Gem spawn randomness (`MATCH3_RULES.md` §7) uses a server-seeded RNG.
2. The RNG seed is part of Active Battle State (`GAME_STATE.md`) so a
   recovered/reconnected session produces identical results if resolution
   is replayed.
3. No gameplay-relevant randomness ever originates on the client.

---

# 7. Non-Goals

Per `MVP_SCOPE.md` §2: no microservices, no horizontal sharding design, no
multi-region architecture, no PvP synchronization model. This document
covers a single-battle, single-player-vs-Boss runtime model only.
