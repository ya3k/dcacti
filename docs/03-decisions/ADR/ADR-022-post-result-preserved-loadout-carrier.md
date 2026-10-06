# ADR-022: Post-Result Preserved-Loadout Carrier

**Status:** Accepted
**Date:** 2026-10-06

## Context

The Product Owner's approved post-result lifecycle (`GDD.md` §2/§2.1,
`TDD.md` §2.1) has `ResultScene` continue to `LobbyScene` on `PLAY AGAIN` and
requires the loadout used in the battle that just ended to still be selected
when the player arrives there, while remaining fully editable before the next
battle is started (`D-202-03 = D`, recorded in `TASK-202`). The same decision
set requires the completed battle's active battle state to be cleared when the
player leaves it, without disconnecting the transport and without clearing that
loadout (`D-202-04 = A`).

`ARCHITECTURE.md` §2.2.3 rules 1–2 make the current model unable to satisfy the
first requirement: the in-progress selection is **ephemeral `LobbyScene`-local
state**, created with the scene and discarded on `shutdown()`, and `GameRuntime`
deliberately holds no selection on the scene's behalf. The `LobbyScene` that
started the battle is already gone when `ResultScene` is presented, so the
selection has nowhere to survive the
`LobbyScene → BattleScene → ResultScene → LobbyScene` round trip.

`ARCHITECTURE.md` §2.2.3 ("Post-Result Loadout Carrier — Architectural
Requirement") and `TASK-202` therefore recorded the requirement — a carrier
that outlives a `LobbyScene` instance, client-owned, non-authoritative — and
deliberately chose **no mechanism, owner, or lifetime**. Choosing it was left
as the first, gated step of the downstream implementation task (`TASK-203`,
`AGENTS.md` §18). This ADR records that choice.

Nothing in this ADR changes a game rule, an API contract, a wire member, a
Redis key, a database column, or the server's authority. The carrier carries
the player's **not-yet-submitted** pre-battle selection, so preserving it
changes its **lifetime**, not its nature (`ARCHITECTURE.md` §2.2.3).

## Decision

### D1 — The carrier is the Phaser game-wide registry, behind a dedicated accessor

The preserved loadout is held as a second documented key in **Phaser's
game-wide registry** (`game.registry`, a `Phaser.Data.DataManager`), published
and read through its own accessor module:

```text
src/frontend/client/src/game/state/PreservedLoadout.ts
    preserveLoadout(scene, loadout)   → publish the submitted loadout
    readPreservedLoadout(scene)       → the preserved loadout, or null
    clearPreservedLoadout(scene)      → drop it explicitly
```

The value is exactly the four documented `BattleStartRequest` members
(`API_CONTRACTS.md` §3 — `petId`, `bossId`, `cardLoadout`, `relicLoadout`), as
the loadout the player most recently submitted. No new loadout, selection, or
collection type is introduced: the carrier stores the wire request the client
already builds, so there is no second spelling of a loadout shape
(`ARCHITECTURE.md` §2.2.3 rule 6).

The registry is the mechanism the client **already** uses for exactly this kind
of scene-lifecycle handoff: `GameConfig.ts`'s `postBoot` publishes `GameRuntime`
under `RUNTIME_REGISTRY_KEY` and `RuntimeRegistry.ts` is its documented
accessor (`ARCHITECTURE.md` §2.2.1 rule 2). The carrier adds a second key and a
second accessor module; it adds no new state infrastructure, no store, no
manager, and no framework.

### D2 — Owner: the client game-presentation layer, not the runtime

The carrier is owned by the Phaser game instance that owns the registry and the
scene manager — i.e. the client game-presentation layer. It is **not** owned by
`GameRuntime`:

- Rule 2 of `ARCHITECTURE.md` §2.2.3 ("the in-progress selection is not owned by
  the runtime, and the runtime does not hold it") is unchanged. Coordinating the
  request a scene submits is not owning the state the request was built from.
- It is not technical runtime state and is not added to
  `state/GameRuntimeState.ts`, whose contract is connection/session/runtime/
  synchronization status only (§2.2.1 rule 5).
- It is not gameplay state: it is never authoritative, is corroborated by
  nothing, and becomes authoritative only when the server validates it inside
  `POST /api/battle/start` (`GAME_RULES.md` §18, ADR-001).

### D3 — Lifetime: the running game instance (in memory)

```text
LobbyScene shutdown          → carrier survives  (this is the point of the carrier)
BattleScene / ResultScene    → carrier untouched (neither owns or reads it)
Page reload / new game       → carrier starts empty (nothing is persisted)
```

It survives every scene `shutdown()`/`start()` within one running game and ends
with the game instance. Nothing about it is written to `localStorage`,
`sessionStorage`, the URL, the backend, or any database: the client persists no
loadout (ADR-015 D5's in-memory session posture; `AGENTS.md` §13).

### D4 — Access boundary: `LobbyScene` only, on the approved entry only

```text
WRITE   LobbyScene, when a battle start SUCCEEDS — the request the server
        accepted is the loadout that battle is fought with.

READ    LobbyScene, and only when it was entered through the approved
        ResultScene PLAY AGAIN continuation, which passes
        `{ restorePreservedLoadout: true }` as scene start data (the
        documented `scene.start(key, data)` mechanism BattleScene →
        ResultScene already uses). No other entry reads it.

CLEAR   Explicit, by the documented accessor. Nothing else clears it.
```

Two consequences are load-bearing and are the reason the read is gated on the
entry:

1. **Preservation is approved for `PLAY AGAIN` only.** `MainMenuScene` →
   `LobbyScene` and a first-ever Lobby entry are **not** decided by `TASK-202`
   or this ADR, so they must not acquire preserved-loadout behavior by
   accident. They read nothing: the flag is absent, so the Lobby starts
   unselected exactly as it does today.
2. **A restored selection is a starting point, never a lock.** `LobbyScene`
   remains the editing surface; every part of the preserved selection can still
   be changed before `Start Battle`, and the submitted request carries the
   edited values. Restoring is not client-side legality checking: the scene
   restores the ids verbatim and lets the server validate the submitted request
   (`ARCHITECTURE.md` §2.2.3 rule 5, `API_CONTRACTS.md` §3).

Scenes reach the carrier directly, as they reach `RuntimeRegistry`; it is
client presentation state, not transport, so `ARCHITECTURE.md` §2.2.1 rule 1 /
§2.2.3 rule 3 are untouched. The runtime port declares no loadout or selection
type (rule 6 unchanged), and `GameRuntimePort` gains no carrier capability.

### D5 — Cleanup semantics: the preserved loadout is never battle cleanup's

Three concepts this ADR keeps explicitly distinct:

```text
Preserved loadout        The player's not-yet-submitted choice for the NEXT
                         battle, kept so PLAY AGAIN does not rebuild it.
                         Client-owned, presentation-only. Survives the scene
                         round trip. NOT cleared by the post-result exit.

      ≠

Active battle state      The runtime's synchronized presentation copy of the
                         server's last BattleStateUpdated push for the battle
                         in progress (SIGNALR_PROTOCOL.md §4, GAME_STATE.md
                         §2.0.5). Dropped when the player leaves the completed
                         battle (D-202-04 = A).

      ≠

Battle result data       The persisted BattleResult the result route returns
                         (API_CONTRACTS.md §4, DATABASE.md §1). Read on demand
                         by ResultScene, never stored as state, and never
                         promoted into active battle state.
```

Therefore:

- `clearActiveBattleState()` **must not** clear the preserved loadout. The
  preserved loadout is not battle state; clearing it would make `D-202-03 = D`
  impossible.
- The preserved loadout is **overwritten** by the next successful battle start,
  which is how a player who edits it and plays again moves forward without a
  second clearing rule.
- Preserving the loadout does not license retaining stale battle state: the two
  have independent lifetimes and neither implies the other.

### D6 — The post-result exit clears active battle state through a dedicated port capability

`D-202-04 = A` is implemented as a new, explicitly documented client-local
capability on the runtime port:

```text
GameRuntimePort.clearActiveBattleState(): void
```

- It drops the runtime's synchronized battle copy (`getBattleState()` → `null`),
  so no stale battle id, turn, sequence, board, or terminal outcome can be
  addressed or rendered by the next battle.
- It returns `sync` to the documented value for the connection it actually has:
  `awaiting_battle` — "connected, no current battle"
  (`GameRuntimeState.ts` §`SyncStatus`) — when the connection is `connected`,
  and `unsynchronized` otherwise, so the status never claims a connection that
  is not there.
- It performs **no transport operation**: it does not disconnect, reconnect, or
  invoke a hub method. `D-202-04 = C/D` (clearing the SignalR battle
  connection) was not approved.
- It reads no result route and stores no result data.
- It is client-local state behavior only and introduces no wire message:
  `SIGNALR_PROTOCOL.md` §8.3 records that no battle lifecycle/status message
  exists.
- It never touches the preserved loadout (D5).

## Rejected Alternatives

### Reuse `handleBattleNotRecoverable` (the §7.3 `BATTLE_NOT_FOUND` path)

Rejected. That path is contractually tied to `SIGNALR_PROTOCOL.md` §7.3 —
"the snapshot cannot be recovered" — and it additionally takes §7.3's
battle-result fallback read. A normal post-result exit is not a failed
recovery, and taking a recovery fallback for it would misreport a completed
battle as a lost session. The shape it uses (drop the copy, return `sync` to
the no-battle value) is followed; the §7.3 semantics are not borrowed.

### The runtime owns the carrier (a port capability that holds the selection)

Rejected. It contradicts `ARCHITECTURE.md` §2.2.3 rule 2, which deliberately
kept the selection out of the runtime, and it would put pre-battle selection
state next to the synchronized battle copy whose lifetime `D-202-04 = A`
controls — exactly the coupling D5 separates. `TASK-202` also records the
carrier as client presentation state, not runtime state.

### Thread the loadout through scene start data (`BattleScene` → `ResultScene` → `LobbyScene`)

Rejected. It requires modifying `BattleScene` (outside `TASK-203`'s boundary and
not expected to be necessary) and it would put pre-battle selection state on the
battle and result scenes, which own neither it nor its lifetime. It also only
works while every intermediate scene cooperates, so a single future scene in the
chain would silently break preservation.

### A module-scoped global slot for the loadout

Rejected. It is an application-global mutable value owned by no documented
component (`AGENTS.md` §9's caution; `TASK-203`'s "global mutable variables"),
and its lifetime is the JS module rather than the game instance, so a fresh game
in the same page could inherit a previous game's loadout. The registry carrier
has the game instance's lifetime and needs no global of its own.

### `localStorage` / `sessionStorage`

Rejected. It invents persistence beyond the running client for a convenience
selection, outliving the session that owns it. No approved decision asks for a
loadout that survives a reload.

### A store or state-management framework (Redux / Zustand / MobX / XState), URL state, or a new manager layer

Rejected. `ARCHITECTURE.md` §5.5 and `AGENTS.md` §9 forbid a heavy client state
store, and the repository's boundary tests already forbid those dependencies. A
single small value needs none of it.

### Backend / session persistence of the "previous loadout"

Rejected. No approved decision requests it, it would add an endpoint or a wire
member, and equip/loadout state is deliberately absent from the collection reads
(`API_CONTRACTS.md` §5.6).

## Consequences

- `D-202-03 = D` becomes implementable: the loadout survives the round trip, and
  `LobbyScene` restores it only on the approved entry while remaining the
  editing surface.
- `D-202-04 = A` becomes implementable without touching the transport, the
  server, or the preserved selection, and the two requirements cannot silently
  clear each other.
- Preserved-loadout state is now a named, documented client presentation state
  with an explicit owner, lifetime, access boundary, and cleanup rule, instead
  of an open question.
- `ARCHITECTURE.md` §2.2.3's "deliberately not decided here" paragraph is
  discharged by this ADR; rule 1 gains the one documented lifetime exception,
  and §2.2.1 records the new client-local cleanup capability.
- No game rule, API contract, wire member, Redis key, database column, or
  server behavior changes. Nothing about the carrier reaches the server except
  as the same `POST /api/battle/start` request that already carries the loadout.
- `MainMenuScene` → `LobbyScene` and first-entry Lobby behavior are unchanged
  and remain undecided: the carrier is read only on the `PLAY AGAIN` entry.

## Related Documents

- `docs/02-technical/ARCHITECTURE.md` §1 (client file tree), §2.2.1 (runtime
  coordination, rules 1–6), §2.2.3 (pre-battle selection boundary; the
  post-result carrier requirement this ADR discharges), §5.5
  (anti-overengineering)
- `docs/02-technical/TDD.md` §2.1 — the approved Phaser scene lifecycle, its two
  post-result continuations, and the loadout-preservation + editability rule
- `docs/00-overview/GDD.md` §2, §2.1 — the approved player-facing post-result
  flow
- `docs/02-technical/GAME_STATE.md` §2.0.5, §4 — the synchronized battle-state
  copy and client presentation state
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4, §7.3, §8.3 — the state push, the
  `BATTLE_NOT_FOUND` fallback, and the absence of any battle lifecycle message
- `docs/02-technical/API_CONTRACTS.md` §3, §4, §5.6 — the only battle-creation
  route, the result route, and the absence of equip state in collection reads
- `docs/02-technical/REDIS_STATE.md` §3 — active state is already deleted
  server-side at battle end
- ADR-001 — server-authoritative battle resolution
- ADR-003 — Phaser game runtime on the client
- ADR-008 — snapshot-based battle reconnection
- `tasks/completed/TASK-202-post-result-lifecycle-product-decision-and-specification.md`
  — the decision authority (`D-202-01` … `D-202-04`) that created this
  requirement
- `tasks/completed/TASK-203-post-result-lifecycle-implementation.md` — the
  implementation task that recorded this ADR before implementing the carrier
