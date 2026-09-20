# SignalR Protocol

**Version:** 1.0
**Status:** Draft — depends on TDD.md §0 assumption (ASP.NET Core backend)

> This document answers: **"How does realtime communication work?"** It does
> not redefine game mechanics — every validation rule referenced here is
> owned by `GAME_RULES.md` or a domain rule document.

---

# 1. Connection

1. Client obtains `battleId` and hub URL from `POST /api/battle/start`
   (`API_CONTRACTS.md` §3).
2. Client connects to the `BattleHub` and joins a group scoped to
   `battleId`.
3. Connection is authenticated using the application session established
   during initial authentication (`POST /api/auth/discord`, `API_CONTRACTS.md` §2,
   ADR-007).
4. `BattleHub` and realtime game handlers deal exclusively with the
   authenticated application player session, with zero direct dependency on
   Discord Embedded App SDK internals.

---

# 2. Client → Server (Hub Methods)

```text
Swap(battleId, fromCell, toCell, clientSequence)
CardCast(battleId, cardId, clientSequence)
PetSkillCast(battleId, clientSequence)     // shorthand; the active Pet's
                                             // Signature Skill is implied
```

1. `clientSequence` is an opaque client-generated request id, echoed back so
   the client can correlate a request with its resulting events — it is
   NOT the authoritative `BattleState.Sequence` (`GAME_STATE.md` §5).
2. Every method is validated server-side per its owning domain document
   before any state changes (`MATCH3_RULES.md` §2 for Swap,
   `CARD_RULES.md` §3 for CardCast/PetSkillCast).
3. An invalid request (e.g. non-adjacent Swap, insufficient Power) does not
   throw a hard error to the connection — it returns a rejection result to
   the caller only (see §4) and emits no Battle Events.

---

# 3. Server → Client (Event Delivery)

```text
ReceiveEvents(battleId, serverSequence, events[])
```

1. All events produced by resolving one action (`GAME_EVENTS.md` §1) are
   delivered together, in one `ReceiveEvents` call, in the fixed order
   defined by `GAME_RULES.md` §17.
2. `serverSequence` is `BattleState.Sequence` after this resolution
   (`GAME_STATE.md` §5) — strictly increasing, no gaps, per battle.
3. Every client in the `battleId` group receives the same
   `ReceiveEvents` call (MVP is single-player-vs-Boss, so in practice this
   is the one connected client, but the group mechanism is what allows a
   reconnect to simply rejoin).

---

# 4. Request/Response Acknowledgement

Hub methods return a direct invocation result to the caller (independent of
the broadcast `ReceiveEvents`):

```json
{ "accepted": true }
```
```json
{ "accepted": false, "reason": "INVALID_SWAP" | "INSUFFICIENT_POWER" | "..." }
```

This lets the client immediately know whether its input was accepted,
without waiting for/parsing the event broadcast.

---

# 5. Sequencing & Ordering Guarantees

1. The server processes one action at a time per `battleId` — no two
   resolutions for the same battle run concurrently
   (`REDIS_STATE.md` §4, optimistic lock).
2. If a client sends a new action before receiving `ReceiveEvents` for a
   prior one, the server still resolves them strictly in arrival order.
3. The client must apply `ReceiveEvents` payloads in `serverSequence` order;
   if it receives a `serverSequence` that skips a number, it must treat this
   as state desync (§6).

---

# 6. Reconnect & Resync

1. On reconnect, the client calls `GetBattleState(battleId)` (a request/
   response Hub method, not a broadcast) to fetch the current authoritative
   `BattleState` snapshot (`GAME_STATE.md` §2) plus its `Sequence`.
2. The client discards any local prediction and re-renders from this
   snapshot — it does not attempt to replay missed individual events.
3. If `GetBattleState` returns `BATTLE_NOT_FOUND` (state expired/cleared,
   see `REDIS_STATE.md` §3 TTL), the client treats the battle as ended and
   falls back to `GET /api/battle/{battleId}/result` (`API_CONTRACTS.md` §3).

---

# 7. What Is Not Here

1. Exact JSON field names/casing for each event payload — payload *content*
   is defined in `GAME_EVENTS.md` §2; exact wire schema is an implementation
   detail.
2. Any PvP or multi-client-divergent-state scenario — out of MVP scope
   (`MVP_SCOPE.md` §2).
