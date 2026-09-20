# ADR-001: Server-Authoritative Battle Resolution

**Status:** Accepted
**Date:** 2026-09-19

## Context

The game is a Match-3 RPG Boss Battler running as a Discord Activity. Battle
outcomes (Damage, HP, Power, Match/Combo results, Passive progress, Rewards)
determine player progression and must not be forgeable by a modified or
compromised client. `GAME_RULES.md` §18 already fixes which fields the
client may send (`Swap`, `Card Cast`, `Pet Skill Cast`) versus which fields
only the server may produce.

## Decision

The server is the sole authority for resolving every battle action. The
client sends action *requests* only; the server validates, resolves the
full Event Resolution pipeline (`GAME_RULES.md` §17), and pushes the
resulting Battle Events back to the client. The client never computes an
authoritative outcome — only optimistic, discardable presentation.

## Alternatives Considered

### Option A — Client-Authoritative with Server Validation After the Fact
Client computes the outcome and sends the result; server spot-checks or
trusts it. Rejected: allows a modified client to forge damage, HP, Power, or
rewards, which `GAME_RULES.md` §18 explicitly forbids.

### Option B — Shared/Lockstep Authority
Both sides simulate and reconcile, common in competitive multiplayer.
Not applicable: MVP is single-player-vs-Boss, not peer-to-peer
(`TDD.md` §7 Non-Goals explicitly excludes a PvP synchronization model).

### Option C — Full Server Authority (Chosen)
Server owns all game logic and state; client is a renderer of server-pushed
events.

## Why

Full server authority is the only option consistent with the explicit
constraint in `GAME_RULES.md` §18 and is the simplest model for a
single-player-vs-Boss battle — it avoids any reconciliation logic that
peer/lockstep models would require.

## Consequences

### Positive
- Eliminates an entire class of cheating/exploit vectors on gameplay values.
- Single source of truth per battle, simplifying reconnection
  (see ADR-008).

### Negative
- All gameplay logic must run server-side, increasing server compute load
  compared to a client-authoritative model.
- Client-perceived latency depends on round-trip time to the server for
  every Swap/Card action, mitigated only by optimistic local prediction
  for input responsiveness (`TDD.md` §2.1).

### Trade-offs
- Development velocity trades toward the server: any new gameplay mechanic
  requires server-side implementation before it can affect outcomes, even
  during prototyping.

## Related Documents

- `docs/01-game-design/GAME_RULES.md` (§17, §18)
- `docs/02-technical/TDD.md` (§2, §3)
- `docs/02-technical/ARCHITECTURE.md` (§4)
- `docs/02-technical/GAME_STATE.md` (§5)
