---
name: client-event-projection
description: "Use this skill when implementing the event processing pipeline that ingests server ReceiveEvents batches, updates client presentation state, and triggers Phaser visual animations in DCacti."
---

# skills/client/client-event-projection.md — Skill: Client Event Projection

**Version:** 1.0  
**Status:** Binding  
**Scope:** Pipeline for consuming server events, projecting them into client presentation state, and dispatching visual actions to Phaser.

> Establishes the disciplined unidirectional data flow from SignalR wire events (`ReceiveEvents`) through `GameRuntime` into client presentation state and Phaser scene rendering.

---

## Purpose

Define the event consumption and state projection architecture on the DCacti client:
1. Receive atomic event batches from `SignalRService` via `ReceiveEvents`.
2. Update technical and synchronized presentation state (`GameRuntimeState.ts`).
3. Project events cleanly into Phaser presentation events without ad-hoc object mutation.
4. Guarantee event ordering preservation as received from the server.

---

## When to Use

- Implementing or updating event listeners in `GameRuntime.ts` or `BattleScene.ts`.
- Handling `ReceiveEvents` SignalR messages on the client.
- Synchronizing client presentation mirrors with authoritative server state payloads.
- Dispatching structured presentation events to Phaser View components.

---

## When Not to Use

- Authoritatively creating or dispatching backend battle events (server Application layer).
- Direct low-level SignalR connection management (handled in `SignalRService.ts`).
- Implementing raw Phaser sprite tweens (use `client/phaser-battle-presentation` or `phaser/tweens`).

---

## Authoritative Sources

- `docs/02-technical/GAME_EVENTS.md` §1–§5 — Authoritative event definitions and batching
- `docs/02-technical/SIGNALR_PROTOCOL.md` §2, §3 — `ReceiveEvents` contract and ordering guarantees
- `docs/02-technical/GAME_STATE.md` §2, §4 — Client state mirror structure
- `docs/02-technical/ARCHITECTURE.md` §2.2.1 — Game Runtime coordination

---

## Unidirectional Event Projection Flow

```text
Server (SignalR Hub)
        │
        ▼ ReceiveEvents([Event1, Event2, ...])
SignalRService (services/realtime/SignalRService.ts)
        │
        ▼ (forwards batch unchanged)
GameRuntime (game/runtime/GameRuntime.ts)
        │
        ├──▶ Updates Technical Presentation State (state/GameRuntimeState.ts)
        │
        ▼ Emits to GameRuntimePort
BattleScene / Presentation Layer
        │
        ▼ (Queue & sequential playback)
View Components (BoardView, BossView, PlayerView, PetView)
```

---

## Projection Rules

1. **No Ad-Hoc Mutation:**
   - SignalR network callbacks must **never** reach into Phaser game objects (e.g. `window.battleScene.boss.hp = 10` is forbidden).
   - Events must flow through `GameRuntime` event subscriptions.
2. **Order Preservation:**
   - The server emits events in strict chronological resolution order (`SIGNALR_PROTOCOL.md` §3).
   - `GameRuntime` must forward the event batch without re-sorting, filtering, or dropping events.
3. **Atomic Batch Ingestion:**
   - When a batch arrives, the presentation layer enqueues the entire batch and begins sequential animation playback.
   - Incoming events for the next turn are queued until previous turn visual playback concludes.
4. **Resynchronization Support:**
   - When a full state snapshot arrives (e.g. on reconnect / `BattleStateRestored`), client presentation state resets all entity visuals immediately to match the snapshot.

---

## Event Mapping Table

| Server Event | Presentation State Projection | Visual Action Triggered |
|---|---|---|
| `GemSwapped` | Update presentation board positions | Animate gem swap tween (`BoardView`) |
| `GemMatched` | Clear cells in presentation board | Play gem dissolve VFX + floating combo text |
| `SpecialGemCreated` | Set special gem type at cell | Spawn special gem icon / glow |
| `GemsDropped` | Update cell positions with new gems | Animate gem fall & bounce ease |
| `DamageDealt` | Update target presentation HP | Spawn floating damage text + HP bar tween |
| `PlayerHealed` | Update player presentation HP | Spawn green healing text + HP bar tween |
| `PowerGained` | Update player presentation Power | Animate Power meter fill |
| `PetSkillTriggered` | Mark passive consumed | Play Pet attack animation & special effect |
| `BossPhaseChanged` | Update boss presentation phase | Play phase change animation & roar |
| `BattleConcluded` | Set battle status to Ended | Transition to `ResultScene` after brief delay |

---

## Do / Don't

| Do | Don't |
|---|---|
| Ingest `ReceiveEvents` batches atomically via `GameRuntimePort`. | Attach ad-hoc SignalR event listeners inside individual sprite classes. |
| Animate events sequentially in the exact order received from server. | Execute all batch events concurrently, causing visual overlaps and race conditions. |
| Update presentation mirrors strictly from event payloads. | Recompute what the new HP should be using client math. |

---

## Stop Conditions

- SignalR event shape received on wire does not match `GAME_EVENTS.md` or `SIGNALR_PROTOCOL.md`.
- Network event requires client to guess or extrapolate missing payload data.

---

## Traceability

- **Reads:** `docs/02-technical/GAME_EVENTS.md`, `docs/02-technical/SIGNALR_PROTOCOL.md`, `docs/02-technical/GAME_STATE.md`, `docs/02-technical/ARCHITECTURE.md`
- **Used by:** `client` agent, `realtime` agent, `orchestrator` agent
- **Related Skills:** `client/client-state-authority`, `client/phaser-battle-presentation`, `realtime/realtime-protocol-validation`
