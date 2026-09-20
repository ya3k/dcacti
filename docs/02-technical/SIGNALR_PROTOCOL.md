# SignalR Protocol

**Version:** 1.1
**Status:** Draft — depends on TDD.md §0 assumption (ASP.NET Core backend)

> This document answers: **"How does realtime communication work?"** It does
> not redefine game mechanics — every validation rule referenced here is
> owned by `GAME_RULES.md` or a domain rule document.

---

# 0. State Delivery Stages

The server → client states delivered over SignalR come from the staged
contract in `GAME_STATE.md` §0:

```text
Battle State Foundation (GAME_STATE.md §2.0)
        ↓  delivered by the initial-state subscription (§4)
        ↓
Battle Events (GAME_EVENTS.md)  — once resolution exists
        ↓  delivered by ReceiveEvents (§3)
        ↓
Full BattleState (GAME_STATE.md §2)  — full snapshots, on resync (§6)
```

Two delivery shapes exist and are not interchangeable:

```text
Subscription push   §4  — server pushes state on join; initial sync
ReceiveEvents       §3  — server pushes one batch per resolved action
GetBattleState      §6  — client requests a snapshot; reconnect resync
```

§4 is the foundation stage's mechanism. It delivers the fields defined by
`GAME_STATE.md` §2.0 and nothing else, and does not change §3 or §6.

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
   the caller only (see §5) and emits no Battle Events.

All three require a battle that already exists. **Creating a battle is not a
Hub method** — it is `POST /api/battle/start` (`API_CONTRACTS.md` §3), which
returns the `battleId` this hub is then scoped to (`§1.1`). Battle State
Foundation adds no *gameplay* method here and does not change this contract; see
§4 for how the joining client receives state.

The group join required by §1.2 is the one non-gameplay client → server call in
this protocol:

```text
JoinBattle(battleId)
```

1. It adds the caller's connection to the group scoped to `battleId` (§1.2). It
   is not a gameplay action: it submits no game input, is not validated against
   a domain rule, and returns no result beyond the group join itself.
2. Its only observable effect is the server-initiated state push defined in §4.
   It changes no battle state, and it is the trigger for that push — not a
   request for state (§4.1).
3. It is not a battle-creation mechanism: §8.4 remains unchanged, and
   `POST /api/battle/start` remains the only documented way to create a battle.
4. `JoinBattle` is a transport step, not a state contract. It carries no battle
   field and introduces no `Status`/lifecycle value (§8.3).

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

# 4. Initial State Delivery (`BattleStateUpdated`)

Defines how the client first receives the authoritative battle state, so the
runtime foundation (`BattleState → SignalR → GameRuntime → BattleScene`) can
be built before gameplay exists (`GAME_STATE.md` §0, §2.0).

```text
BattleStateUpdated(battleId, turn, sequence)
```

Exactly the `GAME_STATE.md` §2.0 fields. No gameplay field is carried, and
no `Status`/lifecycle value is carried anywhere in the protocol.

1. **Trigger.** The battle group is joined (§1.2). The server delivers the
   current battle state to the joining client as its first battle-scoped
   message, before any `ReceiveEvents` for that battle. There is no separate
   client request: joining the group is what triggers delivery.
2. **Direction.** Server → client, server-initiated. It is a **push**, not a
   request/response call — there is no `GetBattleState`-style invocation on
   this path.
3. **Scope.** Sent to the joining caller only. Unlike `ReceiveEvents` (§3.3),
   it is not a group broadcast; the joining client is the only recipient that
   needs it.
4. **Payload.** `battleId` (`GAME_STATE.md` §2.0), `turn`, `sequence`. Casing
   is an implementation detail (§8.1). No other field may be added to this
   record: additional state is introduced by extending `GAME_STATE.md` §2.0,
   not by the wire shape.
5. **Initial values.** For a battle with no resolved action:
   `turn = 0` and `sequence = 0` (`GAME_STATE.md` §2.0.2). Because §2.0 has no
   resolutions, these are the only values deliverable at the foundation
   stage; the rule that `sequence` otherwise increments by exactly 1 per
   resolved action is unchanged (`GAME_STATE.md` §5).
6. **Relationship to `ReceiveEvents` (§3).** Different purpose and different
   content. `ReceiveEvents` carries the ordered **events** produced by
   resolving one action, keyed by the post-resolution `serverSequence`. It
   carries no battle state. `BattleStateUpdated` carries **state**, and
   carries no events. Neither replaces the other; once resolution exists,
   both are used, and §3 remains the only event-delivery path.
7. **Relationship to `GetBattleState` (§6).** Different mechanism, and §6 is
   unchanged. `GetBattleState` is a request/response snapshot used for
   reconnect/resync, where the client has lost synchronization and must
   re-render from a full `GAME_STATE.md` §2 snapshot (`ADR-008`).
   `BattleStateUpdated` is an unsolicited push on join. §6 is **not**
   redefined as an initial-creation mechanism, and the initial path does not
   require a client invocation.
8. **Not a substitute for §6.** If the joining client is already
   resynchronizing a battle that progressed, the snapshot semantics of §6
   apply; §4 alone is not the documented recovery path.
9. **Ownership.** The payload is authoritative server state
   (`GAME_RULES.md` §18, `ADR-001`). The client stores it as a synchronized
   presentation copy and must never author, adjust, or recompute it.

This method name is `BattleStateUpdated` for the `BattleState` it delivers,
and is the only state-push method in this protocol. No second or parallel
state-sync method exists — `GameStateSync`, `SyncEverything`, or similar are
not part of this contract.

---

# 5. Request/Response Acknowledgement

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

# 6. Sequencing & Ordering Guarantees

1. The server processes one action at a time per `battleId` — no two
   resolutions for the same battle run concurrently
   (`REDIS_STATE.md` §4, optimistic lock).
2. If a client sends a new action before receiving `ReceiveEvents` for a
   prior one, the server still resolves them strictly in arrival order.
3. The client must apply `ReceiveEvents` payloads in `serverSequence` order;
   if it receives a `serverSequence` that skips a number, it must treat this
   as state desync (§7).
4. `BattleStateUpdated` (§4) is delivered before any `ReceiveEvents` for that
   battle. It is not sequenced against resolutions and carries no event
   ordering meaning.

---

# 7. Reconnect & Resync

1. On reconnect, the client calls `GetBattleState(battleId)` (a request/
   response Hub method, not a broadcast) to fetch the current authoritative
   `BattleState` snapshot (`GAME_STATE.md` §2) plus its `Sequence`.
2. The client discards any local prediction and re-renders from this
   snapshot — it does not attempt to replay missed individual events.
3. If `GetBattleState` returns `BATTLE_NOT_FOUND` (state expired/cleared,
   see `REDIS_STATE.md` §3 TTL), the client treats the battle as ended and
   falls back to `GET /api/battle/{battleId}/result` (`API_CONTRACTS.md` §4).

This section is the **reconnect/resync** mechanism only. It is not the
initial-synchronization path — that is §4. The two are not interchangeable:
§4 is an unsolicited push on join carrying the `GAME_STATE.md` §2.0 subset,
while §7 is a client-requested full-snapshot recovery per `ADR-008`.

---

# 8. What Is Not Here

1. Exact JSON field names/casing for each event payload — payload *content*
   is defined in `GAME_EVENTS.md` §2; exact wire schema is an implementation
   detail. The same applies to the `BattleStateUpdated` record (§4.4).
2. Any PvP or multi-client-divergent-state scenario — out of MVP scope
   (`MVP_SCOPE.md` §2).
3. Any battle lifecycle / status message. `BattleStateUpdated` carries no
   `Status` field (`GAME_STATE.md` §2.0.3); battle outcome remains the
   `BattleWon` / `BattleLost` events (`GAME_EVENTS.md` §2).
4. A gameplay-free battle-creation method. `Battle State Foundation` adds no
   way to create a battle over SignalR, and does not weaken
   `POST /api/battle/start` (`API_CONTRACTS.md` §3) — see §2.
