# ADR-004: SignalR for Realtime Communication

**Status:** Accepted
**Date:** 2026-09-19

## Context

Battle actions (Swap, Card Cast, Pet Skill Cast) and their resulting Battle
Events must be exchanged between client and server with strict ordering
(`GAME_RULES.md` §17) and support for reconnection (`SIGNALR_PROTOCOL.md`
§6). `TDD.md` §1 and §5 establish SignalR as this transport, and
`SIGNALR_PROTOCOL.md` fully specifies its usage.

## Decision

SignalR is used for all in-battle realtime communication: client-to-server
Hub method calls (`Swap`, `CardCast`, `PetSkillCast`) and server-to-client
event delivery (`ReceiveEvents`). REST is used only for non-realtime
concerns (`TDD.md` §5, `API_CONTRACTS.md`).

Note: SignalR is a .NET-native realtime framework. Choosing it is coupled to
the backend runtime being ASP.NET Core, which `TDD.md` §0 records as an
**ASSUMPTION**, not yet independently confirmed by a human decision or its
own ADR. This ADR documents the transport decision as it stands in the
existing docs; it does not resolve that open assumption.

## Alternatives Considered

### Option A — Raw WebSockets
Lower-level, framework-agnostic realtime channel. Not chosen: existing docs
name SignalR specifically and rely on its built-in Hub method
invocation/broadcast model (`SIGNALR_PROTOCOL.md` §2-3), which raw
WebSockets would require reimplementing.

### Option B — Socket.IO
A common alternative in Node.js-centric stacks. Not chosen: existing docs
consistently reference SignalR, and Socket.IO would not fit an ASP.NET Core
backend without a non-native bridge.

### Option C — REST Polling / Long Polling
Client repeatedly polls for state changes. Not chosen: cannot meet the
ordered, low-latency event delivery requirement of `GAME_RULES.md` §17
without significant added complexity, and SignalR already provides
ordered, connection-based delivery.

### Option D — SignalR (Chosen)
.NET-native realtime library with Hub method invocation and group-based
broadcast, used exactly as `SIGNALR_PROTOCOL.md` specifies.

## Why

SignalR's Hub/group model maps directly onto the battle's requirements:
request/response Hub methods for actions (`SIGNALR_PROTOCOL.md` §2, §5) and
ordered group broadcast for Battle Events (`SIGNALR_PROTOCOL.md` §3), with
built-in reconnection support that `SIGNALR_PROTOCOL.md` §7 builds on.

## Consequences

### Positive
- Built-in connection/group management removes the need to hand-roll
  connection tracking for reconnect/resync (ADR-008).
- Ordered event delivery matches the strict resolution ordering required by
  `GAME_RULES.md` §17.

### Negative
- Ties the backend to the .NET ecosystem (see the open ASSUMPTION in
  `TDD.md` §0) — SignalR's server component is not available as a
  first-class implementation outside .NET.

### Trade-offs
- If the backend runtime assumption in `TDD.md` §0 is ever revisited and a
  non-.NET backend is chosen instead, this ADR would need to be superseded.

## Related Documents

- `docs/02-technical/TDD.md` (§0, §1, §5)
- `docs/02-technical/SIGNALR_PROTOCOL.md` (entire document)
- `docs/02-technical/API_CONTRACTS.md` (§1, boundary with REST)
