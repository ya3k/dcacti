# ADR-005: Redis for Active Battle State

**Status:** Accepted
**Date:** 2026-09-19

## Context

An in-progress battle's state (`BattleState` — `GAME_STATE.md` §2) must be
readable/writable on every resolved Swap or Card Cast with low latency, and
must survive a single server instance restart so a player can reconnect
mid-battle (`SIGNALR_PROTOCOL.md` §7). This state is transient — it exists
only for the lifetime of one battle (`GAME_STATE.md` §1) — unlike durable
player/collection data.

## Decision

Active, in-progress `BattleState` is stored exclusively in Redis
(`REDIS_STATE.md`), keyed per battle (`battle:{battleId}:state`), with
sliding TTL and optimistic-concurrency writes gated on `BattleState.Sequence`
(`REDIS_STATE.md` §3-4). PostgreSQL is never read or written on the hot path
of resolving a single action (`TDD.md` §4.3).

## Alternatives Considered

### Option A — In-Process Memory Only
Keep `BattleState` in server memory for the duration of the connection.
Rejected: cannot survive an instance restart or support the reconnect flow
that `SIGNALR_PROTOCOL.md` §7 requires (`GetBattleState` must return a
recoverable snapshot).

### Option B — PostgreSQL for Active State Too
Use the same durable store for both active and persistent data. Rejected:
`TDD.md` §4.3 explicitly states PostgreSQL is never queried on the hot path
of resolving a single Swap; a relational store adds unnecessary write
overhead for state that is discarded at battle end.

### Option C — Redis (Chosen)
In-memory key-value store scoped specifically to active, transient battle
state, cleared on battle completion (`REDIS_STATE.md` §3).

## Why

Redis gives low-latency read/write for the hot resolution path while
providing enough durability (surviving a single server instance restart, not
a Redis instance loss) to support reconnect. The clean split — Redis for
active state, PostgreSQL for durable state — keeps each store's
responsibility singular (`TDD.md` §4).

## Consequences

### Positive
- Fast per-action read/write without touching the relational store.
- Natural TTL-based cleanup of abandoned battles (`REDIS_STATE.md` §3).

### Negative
- If the Redis instance itself is lost before a battle completes, that
  battle's progress is lost — `REDIS_STATE.md` §5 explicitly accepts this
  risk for MVP (no replication requirement specified).

### Trade-offs
- No write-behind/backup of active state to PostgreSQL mid-battle; battle
  reliability is bounded by single-instance Redis availability for MVP.

## Related Documents

- `docs/00-overview/MVP_SCOPE.md` (§1)
- `docs/02-technical/TDD.md` (§4)
- `docs/02-technical/REDIS_STATE.md` (entire document)
- `docs/02-technical/GAME_STATE.md` (§1, §2, §5)
