# Redis State

**Version:** 1.0
**Status:** Draft

> This document answers: **"How is active battle state represented in
> Redis?"** It does not redefine what any field means — see `GAME_STATE.md`
> for the `BattleState` shape and `GAME_RULES.md`/domain rules for meaning.

---

# 1. Key Structure

```text
battle:{battleId}:state       →  serialized BattleState (GAME_STATE.md §2)
battle:{battleId}:lock         →  short-lived lock key, used during
                                   resolution (see §4)
```

No other Redis keys are needed for MVP (no leaderboard, no pub/sub channel
beyond what SignalR's own backplane may require if the server is scaled to
multiple instances — out of scope for MVP, single-instance assumed per
`TDD.md` §7).

---

# 2. Serialization

1. `BattleState` is serialized as JSON (matches the shape in
   `GAME_STATE.md` §2 exactly — no additional Redis-only fields beyond
   `Sequence`, which is already part of `BattleState`).
2. The serialized value is the single source of truth for a battle's live
   state; the server process holds no long-lived in-memory copy across
   requests (`ARCHITECTURE.md` §4 — `BattleResolutionService` loads, uses,
   and saves it within one resolution).

---

# 3. TTL / Lifecycle

```text
Created:   on POST /api/battle/start (API_CONTRACTS.md §2)
Refreshed: TTL reset on every successful resolution (sliding expiry),
           default 30 minutes of inactivity — Configurable
Deleted:   explicitly, when BattleWon/BattleLost is resolved and the result
           has been written to PostgreSQL (DATABASE.md)
```

If a battle's key expires from inactivity before completion, the battle is
considered abandoned; the client's `GetBattleState` call
(`SIGNALR_PROTOCOL.md` §6) will receive `BATTLE_NOT_FOUND`, and no partial
result is written to PostgreSQL.

---

# 4. Concurrency

1. Before resolving an action, `BattleResolutionService` reads
   `battle:{battleId}:state` along with its `Sequence`.
2. On write, it uses an optimistic check: write succeeds only if
   `Sequence` in Redis still matches what was read (compare-and-set,
   e.g. via a Lua script or `WATCH`/`MULTI`/`EXEC`). If it does not match,
   the resolution is aborted and retried against the fresh state.
3. This guarantees the single-writer-at-a-time property required by
   `SIGNALR_PROTOCOL.md` §5.1 even if two requests for the same battle
   somehow arrive concurrently.
4. `battle:{battleId}:lock` is an optional short-TTL (a few seconds) mutex
   used only to avoid wasted retries under contention; it is not itself the
   source of correctness — the `Sequence` compare-and-set is.

---

# 5. Recovery

1. Redis is the only store for active state — there is no write-behind to
   PostgreSQL during an active battle (`TDD.md` §4.3).
2. If the Redis instance itself is lost before a battle completes, that
   battle's progress is lost; this is accepted for MVP (single Redis
   instance, no replication requirement specified). Flag as a
   **Potential Future Idea** only if reliability requirements change —
   not implemented now.

---

# 6. What Is Not Here

1. Field-by-field JSON schema — derived directly from `GAME_STATE.md` §2;
   not re-specified to avoid duplication.
2. Redis Cluster / sharding — out of scope, no such requirement exists in
   `MVP_SCOPE.md`.
