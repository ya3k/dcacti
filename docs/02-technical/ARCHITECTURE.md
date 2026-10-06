# Architecture

**Version:** 1.7 (§2.2.3's post-result loadout carrier requirement is now
**decided and implemented** per TASK-203 / ADR-022 — the carrier is a second
documented key in Phaser's game-wide registry, behind the
`game/state/PreservedLoadout.ts` accessor; it is owned by the client
game-presentation layer, lives for the running game instance, is written on a
successful battle start, and is read only on the approved `ResultScene` →
`PLAY AGAIN` entry, with `LobbyScene` remaining the editing surface. §2.2.3
rule 1 gains that one documented lifetime exception; §2.2.3's
"deliberately not decided here" note is discharged; §2.2.1 records the
client-local `clearActiveBattleState()` capability the post-result exit uses —
no disconnect, no wire message, and it never clears the preserved loadout — and
§1's client tree lists the accessor module. No game rule, API contract, wire
member, Redis key, or database column changes. Prior 1.6 (§2.2.3 records the **post-result loadout carrier** as an
architectural requirement/boundary per TASK-202 — the approved Product Owner
decision `D-202-03 = D` requires the previous loadout to survive the
`LobbyScene` → `BattleScene` → `ResultScene` → `LobbyScene` round trip, which the
ephemeral scene-local selection of §2.2.3 rule 1 cannot do. This version records
**why a carrier is required and what it must and must not be**; it deliberately
chooses no mechanism, no owner, and no lifetime — that remains an open
architecture decision to be recorded before implementation (`AGENTS.md` §18).
§2.2 item 2's transition reference now defers the approved post-result
continuations to `TDD.md` §2.1 instead of ending the list at `ResultScene`. No
boundary, port capability, endpoint, wire member, state model, or contract is
changed by this version. Version 1.5 (§2.2.3 pre-battle selection boundary synchronized per
TASK-185 — the flow's step list, its diagram, and rule 1 now include the Boss
choice the Lobby makes. The five canonical Boss identities are static selection
content the `LobbyScene` holds, not a collection read and not a new port
capability, so rule 6 is unchanged. No boundary, port capability, endpoint,
wire member, or contract changed. Version 1.4 (§2.2.1 implementation-status wording synchronized — the
`CardCast`/`PetSkillCast` clause and the reconnect/resync recovery clause are
corrected from "not implemented yet" to implemented per TASK-107, TASK-115,
TASK-120, TASK-143, and TASK-144, and the sentence's contract-ownership
citation now names the documents that own those contracts. Status
synchronization only: no wire member, contract, boundary, or rule is changed.)
Version 1.3 (§2.2.1 implementation-status sentence corrected again — the
full `GAME_RULES.md` §17 pipeline is implemented in the Swap path (board
resolution through Boss Response, terminal Victory/Defeat, and the step 19a
Status Effect tick), so the earlier "battle resolution … not implemented"
wording was wrong; `CardCast`, `PetSkillCast`, and reconnect/resync recovery
remain not implemented. Version 1.2 (§2.2.1 implementation-status sentence
synchronized per TASK-100 — `Swap` is implemented end-to-end and is no longer
described as unimplemented. Version 1.1 (§2.2.3 added
per TASK-081 — the pre-battle selection boundary is now stated: the in-progress
Pet/Card/Relic selection is ephemeral `LobbyScene`-local state, the collection
read it is built from reaches the scene through the runtime port rather than
`services/api/` directly, and the port's pre-battle capability is
`startBattle(request: BattleStartRequest): Promise<void>`. §2.2.1 rule 1 is
restated as transport-general — it now covers `fetch`/`ApiService` as well as
`@microsoft/signalr` — which resolves the scope `TASK-079` recorded as
"currently unstated in docs/". No API contract, gameplay rule, endpoint, or
client source file changed.)))
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
│   │   ├── state/                # Client game-presentation state carriers
│   │   │   └── PreservedLoadout.ts     # Post-result preserved loadout (ADR-022)
│   │   └── scenes/               # Phaser Scenes
│   │       ├── BootScene.ts      # Technical init (scales, asset pre-setup)
│   │       ├── PreloaderScene.ts # Asset loading & load progress presentation
│   │       ├── MainMenuScene.ts  # Main menu presentation
│   │       ├── LobbyScene.ts     # Battle preparation presentation
│   │       ├── BattleScene.ts    # Main battle presentation & animation runtime
│   │       └── ResultScene.ts    # Battle outcome presentation
│   │
│   ├── services/                 # Isolated external communication layers
│   │   ├── realtime/             # SignalR Hub client wrapper & event dispatcher
│   │   │   └── SignalRService.ts # SignalR connection lifecycle — the only
│   │   │                         #   SignalR implementation in the client
│   │   └── api/                  # REST API client (auth, collections, battle)
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
   `BattleScene` → `ResultScene`, and on from `ResultScene` to the approved
   post-result continuations — `TDD.md` §2.1 owns that lifecycle and is not
   restated here), sprites, tweens, animations, and user pointer
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
   hub. The rule covers every transport the client uses, not SignalR alone: a
   scene must not import `fetch`, `ApiService`, or any other HTTP/REST client
   either. `services/api/` is an isolated transport layer like
   `services/realtime/` (§2.2 item 3), and a scene reaching it directly would
   bypass this boundary in exactly the way this rule forbids. The pre-battle
   selection flow that needs a collection read is §2.2.3's subject.
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
   (`GAME_STATE.md` §2.0.5, §4) and never authors, adjusts, or recomputes it.
6. **One runtime, one connection.** `SignalRService` is a process-wide
   singleton and `GameRuntime.initialize()` is idempotent, so React
   StrictMode's development double-invocation cannot create a second SignalR
   connection or a second Phaser instance.

The runtime also owns the client-local cleanup the approved post-result
lifecycle requires. When the player leaves a completed battle, `ResultScene`
asks the runtime to drop its synchronized battle copy through

```text
clearActiveBattleState(): void
```

— a coordination-only capability with no transport participation. It clears
`battleState`, returns `sync` to the documented no-current-battle value for the
connection it actually has (`awaiting_battle` when connected, `unsynchronized`
otherwise — `state/GameRuntimeState.ts`'s `SyncStatus`), and performs no hub
call, no disconnect, and no result read. It is **not**
`SIGNALR_PROTOCOL.md` §7.3's `BATTLE_NOT_FOUND` path, which is a failed
recovery that additionally takes the result fallback, and it introduces no wire
message (§8.3). It never touches the preserved pre-battle selection: active
battle state and the preserved loadout are different concepts with independent
lifetimes (§2.2.3, ADR-022).

`GameRuntime` coordinates the initial state subscription
(`SIGNALR_PROTOCOL.md` §4): it receives the server-pushed
`BattleStateUpdated` payload and exposes it to the scenes through its port,
exactly as it forwards `ReceiveEvents` (rule 4). The runtime does not derive,
extend, or validate gameplay meaning from that payload.

The runtime foundation establishes these boundaries. The initial
state-delivery contract (`SIGNALR_PROTOCOL.md` §4) and the staged state it
carries — Battle State Foundation (`GAME_STATE.md` §2.0) and the Board
Foundation State that extends it (`GAME_STATE.md` §2.0.5) — are implemented:
joining a battle's group (`JoinBattle`, `SIGNALR_PROTOCOL.md` §1.2) triggers
the server's `BattleStateUpdated` push, and `GameRuntime` stores that payload
as the client's synchronized copy and exposes it through its port. The board
is delivered as a field of that same push and is rendered by `BattleScene`;
the client never generates or validates it (`GAME_STATE.md` §2.0.5.4).

The client → server gameplay method `Swap` (`SIGNALR_PROTOCOL.md` §2) is
implemented end-to-end: `BattleScene` requests it through `GameRuntime`, and the
server resolves it through the full `GAME_RULES.md` §17 pipeline — board
resolution, Passive charge, the Combat Damage Pipeline, Boss Response (Enrage,
Boss Passive, Boss Skill or Basic Attack), the terminal Victory/Defeat check, and
the End Turn step 19a Status Effect tick — committing one write-back
(`GAME_STATE.md` §5.1) and pushing the resulting ordered Battle Events. The
other client → server gameplay methods (`CardCast`, `PetSkillCast` —
`SIGNALR_PROTOCOL.md` §2) and reconnect/resync snapshot recovery
(`SIGNALR_PROTOCOL.md` §7, ADR-008) are implemented. Their contracts are
owned by `SIGNALR_PROTOCOL.md` §2 and §7, `CARD_RULES.md` §2–§4 (casting),
and `GAME_STATE.md` §5.3 (snapshot compatibility); none of those contracts is
restated in the client runtime.

### 2.2.3 Pre-Battle Selection Boundary

The MVP pre-battle selection flow (Choose Pet, Equip Cards, Equip Relics,
Choose Boss, Start Battle) is `LobbyScene`'s responsibility (`TDD.md` §2.1,
TASK-080, TASK-185). This
section owns the boundary that flow runs on: where its in-progress selection
lives, how it reads the owned collection, and what the runtime port exposes to
it.

```text
        LobbyScene                        ← interaction surface (TDD.md §2.1)
   ┌────────┴─────────┐
   │ in-progress      │  ephemeral, scene-local, discarded on shutdown
   │ selection        │  petId · bossId · cardLoadout[] · relicLoadout[]
   └────────┬─────────┘
            │  (GameRuntimePort)
            ▼
      GameRuntime                          ← coordination boundary
        ├── collection read request ──▶ ApiService (services/api/, §2.2 rule 3)
        └── startBattle(request) ─────▶ POST /api/battle/start
                                            │
                                            ▼
                                    Server validates and snapshots into
                                    PetState.EquippedCards[] /
                                    EquippedRelics[] (GAME_STATE.md §2.3)
```

**Rules:**

1. **The in-progress selection is ephemeral scene state, and `LobbyScene` owns
   it.** It is the player's not-yet-submitted choice of one Pet, one Boss, and
   the Card and Relic sets for the upcoming battle. It is created when the
   scene is,
   lives only in the scene's own fields, and is discarded when the scene shuts
   down. It is the same category as `BattleScene`'s selected board cell: client
   presentation state that no other layer reads and that the server never
   receives except as the request it is submitted in. It is **not** modelled in
   `state/GameRuntimeState.ts` (rule 5), is **not** put on `GameRuntime`, and
   is **not** given a store, manager, or module of its own
   (`ARCHITECTURE.md` §5.5, `AGENTS.md` §9). The one documented exception is the
   post-result preserved-loadout carrier below, which extends this state's
   **lifetime** — never its nature, its owner, or its authority.
2. **The in-progress selection is not owned by the runtime, and the runtime
   does not hold it.** `GameRuntime` coordinates the request the scene submits;
   coordinating a request is not owning the state the request was built from.
   A scene that has been shut down has no selection, and the runtime keeps
   none on its behalf.
3. **Scenes reach the transport only through the runtime port.** The collection
   read data the flow needs (`GET /api/pets`, `/api/cards`, `/api/relics` —
   `API_CONTRACTS.md` §5.1, §5.3, §5.4) is HTTP REST communication, which
   `services/api/` isolates (§2.2 rule 3). Rule 1's boundary is therefore not
   limited to SignalR: a Phaser scene must not import `fetch`, `ApiService`, or
   any other transport client, exactly as it must not import
   `@microsoft/signalr`. `LobbyScene` requests the collection through the
   runtime port and receives the resulting read models; `GameRuntime` is what
   calls `ApiService`, as it already does for battle start.
4. **The read data is a selection source, never a selection.** A collection
   response carries no equip state and no defined ordering
   (`API_CONTRACTS.md` §5.5, §5.6). The scene builds the in-progress selection
   from it, and the order the player chooses in is what the request carries —
   for Relics, position *i* is equip slot *i + 1* (`RELIC_RULES.md` §2.3).
   Collection ordering never becomes loadout ordering.
5. **The runtime port exposes `startBattle`, and the flow's validity is never
   the scene's to decide.** The port's pre-battle capability is

   ```text
   startBattle(request: BattleStartRequest): Promise<void>
   ```

   — the scene submits the documented `API_CONTRACTS.md` §3 request and the
   runtime performs the documented start sequence (REST → connect → join). The
   scene does not validate the loadout, does not decide whether the selection
   is legal, and does not handle the battle: the server validates the submitted
   request (count, ownership, category, copy limit, distinctness), resolves the
   submitted Boss identity, and is authoritative for the resulting
   `BattleState` (`GAME_RULES.md` §18,
   ADR-001). A rejected request leaves the scene active with its selection
   intact and no battle created.
6. **The port carries capabilities, not models.** It exposes the start
   capability above and the collection read the flow needs; it does not define,
   re-export, or own the collection read models or any loadout/selection type.
   Those wire shapes stay where they already live (`services/api/`), as they do
   for `RuntimeBattleState`'s relation to the server's state contract.

#### Post-Result Loadout Carrier — Architectural Requirement

The approved post-result lifecycle (`TDD.md` §2.1, `GDD.md` §2.1) has
`ResultScene` continue to `LobbyScene` on `PLAY AGAIN`, and requires the loadout
used in the battle that just ended to still be selected when the player arrives
there — while remaining fully editable before the next battle is started
(Product Owner decision `D-202-03 = D`, recorded in TASK-202).

Rules 1 and 2 above make the current model unable to satisfy that requirement.
The in-progress selection is created with the `LobbyScene` instance and discarded
when it shuts down; the `LobbyScene` that started the battle is already gone by
the time `ResultScene` is presented, and the runtime keeps no selection on its
behalf. **The approved decision therefore requires a state carrier that outlives
a `LobbyScene` instance.** This subsection recorded that requirement and its
boundary; the mechanism it left open is now decided and recorded below
(ADR-022) and implemented by TASK-203.

**What the carrier must be.**

```text
1. It carries the same category of state rule 1 describes: the player's
   not-yet-submitted choice of one Pet, one Boss, and the Card and Relic sets
   for the UPCOMING battle. Preserving it changes its LIFETIME, not its nature.
2. It is client presentation/interaction state, never authoritative. It becomes
   authoritative only when the server validates it inside
   POST /api/battle/start (rule 5, API_CONTRACTS.md §3, GAME_RULES.md §18,
   ADR-001). Restoring it must not become client-side legality checking.
3. LobbyScene remains the editing surface. A restored selection is a starting
   point the player may change in any part — Pet, Cards, Relics, Boss — before
   starting the next battle (D-202-03 = D, second clause).
4. It must be reachable without breaking rule 3: the scene still obtains
   collection read data and submits the start request only through the runtime
   port, and imports no transport client.
```

**What the carrier must not be.**

```text
1. NOT gameplay state. It is not BattleState, PetState, or any server-owned
   value, and it is not corroborated by the collection reads
   (API_CONTRACTS.md §5.6 — no equip state).
2. NOT technical runtime state. It must not be added to
   state/GameRuntimeState.ts, whose contract is connection/session/runtime/
   synchronization status only (§2.2.1 rule 5).
3. NOT a second source of truth. It does not own, mirror, or modify the owned
   collection (DATABASE.md §2) and does not replace the server's loadout
   snapshot (GAME_STATE.md §2.3).
4. NOT a heavy state store or a new state-management framework
   (`ARCHITECTURE.md` §5.5, `AGENTS.md` §9). A carrier is required; an
   infrastructure layer is not.
5. NOT active battle state. D-202-04 = A clears ACTIVE BATTLE state when the
   player leaves the completed battle; the preserved pre-battle selection is a
   different concept and must not be cleared by that cleanup, or D-202-03 = D
   could not hold. The converse also holds: preserving the selection does not
   license retaining stale battle state.
```

**The carrier decision (recorded by TASK-203 / ADR-022).** The requirement above
was recorded without a mechanism by TASK-202. TASK-203, which implements the
approved flow, recorded the mechanism before implementing it (`AGENTS.md` §18,
`.ai/workflow/architecture/architecture-change.md`). It is:

```text
Carrier    A second documented key in Phaser's game-wide registry
           (game.registry — the mechanism §2.2.1 rule 2 already uses to publish
           the runtime), published and read through its own accessor module
           game/state/PreservedLoadout.ts (preserveLoadout / readPreservedLoadout
           / clearPreservedLoadout).

Value      Exactly the four documented BattleStartRequest members of the
           loadout most recently submitted (API_CONTRACTS.md §3) — no new
           loadout/selection type is declared (rule 6).

Owner      The client game-presentation layer (the Phaser game instance that
           owns the registry and the scene manager). NOT GameRuntime: rule 2 is
           unchanged, and the runtime port gains no carrier capability.

Lifetime   The running game instance. It survives every scene shutdown/start;
           a new game (page reload) starts empty. Nothing is persisted to
           localStorage, sessionStorage, the URL, the backend, or a database.

Access     Written by LobbyScene when a battle start SUCCEEDS — that accepted
           request is the loadout the battle is fought with. Read by LobbyScene
           only when it was entered through the approved ResultScene PLAY AGAIN
           continuation, which passes { restorePreservedLoadout: true } as scene
           start data (the documented scene.start(key, data) mechanism
           BattleScene → ResultScene already uses). No other Lobby entry reads
           it, so MainMenuScene → LobbyScene and a first-ever Lobby entry keep
           exactly today's behavior.

Editing    A restored selection is a starting point, never a lock: every part of
           it (Pet, Cards, Relics, Boss) can still be changed before Start
           Battle, and the submitted request carries the edited values. The
           scene restores the ids verbatim and validates nothing — the server
           validates the submitted request (rule 5).

Cleanup    Never cleared by the D-202-04 = A active-battle cleanup (the two are
           different concepts — see below). Overwritten by the next successful
           battle start. Explicitly clearable through the accessor.
```

The concepts this boundary separates are distinct and must not be collapsed
into one state model (`AGENTS.md` §12, §13; ADR-011 item 4):

```text
Owned collection      Player owns Pet/Card/Relic — persistent, server-side
                          (DATABASE.md §2)

      ≠

In-progress selection Player selected items for the UPCOMING battle — created
                      with the LobbyScene, ephemeral and scene-local (rule 1).
                      Once the battle starts it is what the carrier below holds.

      ≠

Preserved loadout     That same selection, kept past the LobbyScene's shutdown
                      so PLAY AGAIN does not rebuild it (ADR-022). Client-owned,
                      presentation-only, alive for the running game instance,
                      cleared by nothing but an explicit call.

      ≠

Active battle state   The runtime's synchronized presentation copy of the
                      server's last BattleStateUpdated push for the battle in
                      progress. Dropped by clearActiveBattleState() when the
                      player leaves the completed battle (§2.2.1, D-202-04 = A).

      ≠

Battle result data    The persisted BattleResult the result route returns
                      (API_CONTRACTS.md §4, DATABASE.md §1). Read on demand by
                      ResultScene, stored as no state, never promoted into
                      active battle state.

      ≠

BattleState snapshot  Server validated the request and snapshotted the loadout
                      into PetState.EquippedCards[] / EquippedRelics[] —
                      server-authoritative (GAME_STATE.md §2.3)
```

Only the server-authored entries are authoritative, and the client authors none
of them. `D-202-04 = A` clears the **active battle state**; the preserved
loadout is not battle state, so clearing it would make `D-202-03 = D`
impossible — and the converse holds too, because preserving the loadout
licenses retaining no stale battle state.

This section introduces no new endpoint and no change to any API contract: it
records where the existing documented flow's state and reads belong
(`API_CONTRACTS.md` §3, §5.5, §5.6 unchanged). The carrier it now names
(ADR-022) is a client presentation state carrier — it adds no state model to
the server and declares no type of its own, holding the wire request the client
already builds.

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
React + Vite (Frontend)
      │
      │ User credentials (username, password)
      ▼
ASP.NET Core Backend (POST /api/auth/register or POST /api/auth/login; ADR-020)
      │
      │ server-side password verification / hashing (IPasswordHasher<Account>)
      │ Account + Player match/create (DATABASE.md §1)
      ▼
ASP.NET Core Backend
      │
      │ authenticated application session (JWT Bearer Token, ADR-015)
      ▼
REST API / SignalR Hub (BattleHub)
```

1. **Authentication (ADR-020):** Standalone Web authentication via standard username/password replaces the former Discord Activity OAuth boundary. Discord SDK and Discord Activity dependencies are retired.
2. **Account & Password Security:**
   - Password hashes are stored securely in PostgreSQL using PBKDF2 with unique cryptographic salt.
   - Passwords are never logged, echoed, or stored in plaintext.
3. **Authentication Boundary:**
   - Frontend passes credentials to `/api/auth/register` or `/api/auth/login`.
   - Backend creates or authenticates the Account, links/retrieves the Player, and returns the application session token (`ADR-015`).
4. **SignalR Authentication:**
   - SignalR Hub (`BattleHub`) connects using the authenticated application session token.
5. **Architectural Isolation:**
   - No direct coupling exists between authentication presentation and `BattleScene`, `Domain`, `Combat`, `Match3`, `Redis`, or `PostgreSQL`.

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

## 4.1 One Action Is One Resolution

The shape of step 2 above, for a Swap:

```text
BattleHub.Swap(...)                    thin transport (no game logic)
    ↓
BattleResolutionService                Application — sequencing only
    ├── Domain: validate the Swap                  MATCH3_RULES.md §2.1
    ├── Domain: commit, detect, create Special Gems, activate, gravity,
    │           spawn, repeat until stable         MATCH3_RULES.md §3–§5
    ├── Domain: Combo, Match count, resources      MATCH3_RULES.md §6
    ├── state: Turn / Sequence / RngState          GAME_STATE.md §5.1
    ├── BattleStateRepository (one write-back)     REDIS_STATE.md §4
    └── → ordered Battle Events                    GAME_EVENTS.md §1
    ↓
BattleHub → ReceiveEvents / state push             SIGNALR_PROTOCOL.md §3–§4
```

1. **The loop belongs to Domain, not to Application.** The cascade loop
   (`MATCH3_RULES.md` §4) is board behaviour, so it lives in the Match-3 domain
   module (`Match3Engine` §3); `BattleResolutionService` sequences the pipeline
   steps of `GAME_RULES.md` §17 and does not implement or re-implement the
   loop. This is the layer boundary of §2.1: Application orchestrates, Domain
   decides.
2. **One action produces one state write-back and one event batch.** The
   intermediate states of `MATCH3_RULES.md` §4.1 remain inside the Domain call
   and the `ResolutionContext` (`GAME_STATE.md` §3); they never reach the
   repository, the Hub, or the wire.
3. **A rejected action never reaches step 2's mutation stage.** Validation
   returns, `BattleResolutionService` reports the rejection to the Hub, and no
   repository write, no state change, and no event occurs
   (`MATCH3_RULES.md` §2.1.5, `SIGNALR_PROTOCOL.md` §5).
4. **`Match3Engine` owns no combat, Power, Passive, or Relic decision.** The
   board stages produce the matches, cleared Gems, Combo, and Match count that
   the later pipeline steps consume; those consumers are owned by their own
   domains (`ARCHITECTURE.md` §3, `GAME_RULES.md` §17).

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
