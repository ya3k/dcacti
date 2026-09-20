# ADR-008: Snapshot-Based Battle Reconnection

**Status:** Accepted
**Date:** 2026-09-19

## Context

A player may lose connection mid-battle (browser refresh, Discord
reconnect, temporary network loss). The battle's authoritative state must
be recoverable without corrupting the strict Event Resolution order defined
in `GAME_RULES.md` §17. `SIGNALR_PROTOCOL.md` §6 and `REDIS_STATE.md` §3/§5
already establish a concrete strategy for this.

## Decision

On reconnect, the client calls a request/response `GetBattleState(battleId)`
Hub method and receives the current authoritative `BattleState` snapshot
plus its `Sequence` (`SIGNALR_PROTOCOL.md` §6.1). The client discards any
local prediction and re-renders entirely from this snapshot — it does **not**
replay individual missed events (`SIGNALR_PROTOCOL.md` §6.2). No event log
is persisted for replay purposes (`ARCHITECTURE.md` §5.2: "no message queue
/ event sourcing infrastructure").

If the battle's Redis key has expired or the Redis instance itself was lost
before the battle completed, `GetBattleState` returns `BATTLE_NOT_FOUND`;
the client treats the battle as ended and falls back to
`GET /api/battle/{battleId}/result` (`SIGNALR_PROTOCOL.md` §6.3,
`REDIS_STATE.md` §5). This is an **accepted risk for MVP** — no Redis
replication or write-behind persistence exists to prevent this loss
(`REDIS_STATE.md` §5.2, explicitly flagged there as not implemented for
MVP).

## Alternatives Considered

### Option A — Event-Sourcing / Replay Log
Persist every Battle Event and replay them on reconnect to rebuild state.
Rejected: `ARCHITECTURE.md` §5.2 explicitly excludes event-sourcing
infrastructure for MVP as unnecessary complexity relative to snapshot
resync.

### Option B — No Recovery (Battle Ends on Disconnect)
Treat any disconnect as an automatic loss/abandonment. Not the documented
behavior: `SIGNALR_PROTOCOL.md` §6 explicitly defines a recovery path
(`GetBattleState`) rather than immediate termination.

### Option C — Snapshot-Based Resync (Chosen)
Recover by fetching the current `BattleState` snapshot; no replay, no
persisted event log.

## Why

Snapshot resync is the natural consequence of ADR-001 (server-authoritative,
single current `BattleState`) and ADR-005 (Redis holds that state): the
server already has one authoritative current state at all times, so
reconnecting a client only needs the current snapshot, not a history of how
it got there.

## Consequences

### Positive
- Simple recovery model — no event log storage, no replay logic to keep in
  sync with the resolution pipeline.
- Directly reuses the existing `BattleState` snapshot and `Sequence`
  mechanism (ADR-005, `GAME_STATE.md` §5).

### Negative
- If Redis itself fails before battle completion, the battle cannot be
  recovered at all — the player loses progress with no persisted fallback
  (`REDIS_STATE.md` §5.2).

### Trade-offs
- Choosing snapshot-over-replay means the client cannot show a
  "replay-the-missed-actions" animation on reconnect — it jumps straight to
  the current state. Acceptable given `GDD.md`'s Design Philosophy pillar of
  player clarity is not compromised (the player still sees an accurate
  current state, just without a caught-up animation).

## Related Documents

- `docs/01-game-design/GAME_RULES.md` (§17)
- `docs/02-technical/SIGNALR_PROTOCOL.md` (§6)
- `docs/02-technical/REDIS_STATE.md` (§3, §5)
- `docs/02-technical/GAME_STATE.md` (§5)
- `docs/02-technical/ARCHITECTURE.md` (§5.2)
