# ADR-002: Modular Monolith Backend

**Status:** Accepted
**Date:** 2026-09-19

## Context

The backend must run all server-authoritative game logic (Match-3, Combat,
Passive, Relic, Boss systems) plus persistence and realtime transport for a
single-player-vs-Boss MVP. `MVP_SCOPE.md` §2 explicitly excludes
Microservices, Kubernetes, and Kafka from MVP scope.

## Decision

The backend is a single deployable service internally split into four
layers — Domain, Application, Infrastructure, Api — with a fixed dependency
direction (`ARCHITECTURE.md` §1-2). There is no service boundary between
Match-3, Combat, and Boss logic; they are Domain modules within one process
(`ARCHITECTURE.md` §5.4).

## Alternatives Considered

### Option A — Microservices
Separate deployable services per domain (e.g. a Match-3 service, a Combat
service, a persistence service). Rejected: explicitly excluded by
`MVP_SCOPE.md` §2; adds network calls and distributed-transaction concerns
to a resolution pipeline that `GAME_RULES.md` §17 requires to execute in a
strict, single logical order.

### Option B — Unstructured Monolith
A single service with no internal layering or dependency direction (all
logic and I/O mixed together). Rejected implicitly by `ARCHITECTURE.md` §2's
explicit layer boundaries and the rule that Domain must never reference
Infrastructure or Api types — an unstructured monolith would make that rule
unenforceable.

### Option C — Modular Monolith (Chosen)
Single service, internally layered with enforced dependency direction.

## Why

A modular monolith satisfies the MVP scope exclusion of microservices while
still keeping Domain logic (the direct expression of `GAME_RULES.md` and the
domain rule documents) testable and decoupled from transport/persistence
concerns, per `ARCHITECTURE.md` §2.

## Consequences

### Positive
- No distributed-systems complexity (no network calls between Match-3,
  Combat, and Boss logic).
- Domain layer is unit-testable without any framework dependency.
- Matches MVP scope constraints directly.

### Negative
- All game logic scales as one deployable unit; there is no way to scale
  Match-3 resolution independently from persistence load.

### Trade-offs
- If a future phase requires independent scaling of subsystems, this
  decision would need to be revisited and superseded by a new ADR —
  it is not designed to be a stepping stone to microservices.

## Related Documents

- `docs/00-overview/MVP_SCOPE.md` (§2)
- `docs/02-technical/ARCHITECTURE.md` (§1, §2, §5)
- `docs/02-technical/TDD.md` (§1, §7)
