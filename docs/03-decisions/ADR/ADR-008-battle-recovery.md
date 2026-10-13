# ADR-008: Snapshot-Based Battle Reconnection

**Status:** Accepted
**Date:** 2026-09-19
**Amended:** 2026-10-13 (per TASK-273: the same snapshot is recorded as the
answer to a **detected** `serverSequence` desync and not only to a reconnect;
**snapshot precedence over pending and in-flight batches** and the rule that a
superseded resolution is never replayed are stated explicitly; the
`GameRuntime` (delivery integrity, recovery orchestration) / `BattleScene`
(presentation admission, convergence) ownership split is recorded; and the two
invalid `ARCHITECTURE.md` §5.2 references are corrected to §5 item 2. The
original Context, Decision items 1–4, and the Alternatives records are preserved
unmodified below.)

## Context

A player may lose connection mid-battle (browser refresh, Discord
reconnect, temporary network loss). The battle's authoritative state must
be recoverable without corrupting the strict Event Resolution order defined
in `GAME_RULES.md` §17. `SIGNALR_PROTOCOL.md` §7 and `REDIS_STATE.md` §3/§5
already establish a concrete strategy for this.

## Decision

The client calls a request/response `GetBattleState(battleId)` Hub method and
receives the current authoritative `BattleState` snapshot plus its `Sequence`
(`SIGNALR_PROTOCOL.md` §7.1). The client discards any local prediction and
re-renders entirely from this snapshot — it does **not** replay individual
missed events (`SIGNALR_PROTOCOL.md` §7.2). No event log is persisted for
replay purposes (`ARCHITECTURE.md` §5 item 2: "no message queue / event
sourcing infrastructure").

**The same snapshot is the answer to a *detected* desynchronization, not only to
a reconnect.** `SIGNALR_PROTOCOL.md` §6 item 3 makes a `ReceiveEvents` batch whose
`serverSequence` skips past the `sequence` the client's synchronized state holds a
state desync. That gap means the state write-back of a committed resolution never
arrived, so the client withholds the batch rather than presenting its events
against a board it does not hold, and requests this same §7.1 snapshot. No second
recovery mechanism, request, event, or state model is introduced: reconnect and
detected desync differ only in what opened the gap, never in how it is closed.

### Snapshot precedence over pending and in-flight batches

Because the snapshot is taken between resolutions and carries the complete result
of the resolutions up to its `sequence` (`GAME_STATE.md` §5.1 item 3, §5.3
item 3), it **supersedes every batch it covers**:

- A batch whose `serverSequence` is **at or below** the snapshot's `sequence`
  describes a resolution the snapshot already contains, so it is **never
  replayed**. Animating it would replay a resolution the client has jumped past,
  which is exactly the event replay this ADR rejects. The resolution is recorded,
  not re-presented.
- A batch that was **withheld** because it opened the gap is never applied after
  the snapshot lands either: the snapshot is the answer to that gap, and the
  withheld batch's events are not re-applied on top of it.
- A batch strictly **above** the snapshot's `sequence` is a genuinely newer
  resolution and is delivered through the ordinary `ReceiveEvents` path.

An accepted recovery response whose envelope `serverSequence` and snapshot
`sequence` disagree is not a snapshot at all: the client refuses it rather than
choosing one of the two values (`SIGNALR_PROTOCOL.md` §7 item 5).

### Runtime / scene ownership

The two halves of this recovery are separated by the boundary
`ARCHITECTURE.md` §2.2.1 draws, and neither half interprets gameplay:

- **`GameRuntime` owns delivery integrity and recovery orchestration.** It
  compares a batch's battle identity and `serverSequence` with the synchronized
  copy, withholds a batch that cannot be placed, publishes the synchronization
  status, requests the §7.1 snapshot, deduplicates that request to one in flight
  per battle, and bounds its retry. It performs no gameplay calculation and never
  authors, merges, or advances battle state (`ARCHITECTURE.md` §2.2.1 rule 3).
- **`BattleScene` owns presentation admission and convergence.** It refuses to
  build or animate a timeline from a batch the authoritative state it renders
  does not represent, and, when a snapshot arrives, cancels the obsolete
  presentation, converges the board and every timeline-owned readout on the
  delivered state, and releases the presentation locks. It never imports or calls
  SignalR: recovery is requested by the runtime, not by a scene
  (`ARCHITECTURE.md` §2.2.1 rule 1).

If the battle cannot be recovered at all, that is `SIGNALR_PROTOCOL.md` §7.3's
`BATTLE_NOT_FOUND` below — and the runtime publishes the loss to the scene
(`null` with the `'ended'` source) so the scene can drop what it was presenting
instead of leaving a stale board behind.

If the battle's Redis key has expired or the Redis instance itself was lost
before the battle completed, `GetBattleState` returns `BATTLE_NOT_FOUND`;
the client treats the battle as ended and falls back to
`GET /api/battle/{battleId}/result` (`SIGNALR_PROTOCOL.md` §7.3,
`REDIS_STATE.md` §5). This is an **accepted risk for MVP** — no Redis
replication or write-behind persistence exists to prevent this loss
(`REDIS_STATE.md` §5.2, explicitly flagged there as not implemented for
MVP).

## Alternatives Considered

### Option A — Event-Sourcing / Replay Log
Persist every Battle Event and replay them on reconnect to rebuild state.
Rejected: `ARCHITECTURE.md` §5 item 2 explicitly excludes event-sourcing
infrastructure for MVP as unnecessary complexity relative to snapshot
resync.

### Option B — No Recovery (Battle Ends on Disconnect)
Treat any disconnect as an automatic loss/abandonment. Not the documented
behavior: `SIGNALR_PROTOCOL.md` §7 explicitly defines a recovery path
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
- `docs/02-technical/SIGNALR_PROTOCOL.md` (§3.1, §6 items 3–4, §7)
- `docs/02-technical/REDIS_STATE.md` (§3, §5)
- `docs/02-technical/GAME_STATE.md` (§5, §5.0, §5.1 item 3, §5.3)
- `docs/02-technical/ARCHITECTURE.md` (§2.2.1 rules 1, 3 and 4, §5 item 2)
