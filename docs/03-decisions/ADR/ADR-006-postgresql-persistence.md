# ADR-006: PostgreSQL for Persistent Data

**Status:** Accepted
**Date:** 2026-09-19

## Context

Durable data — Player accounts, owned Pets, owned Relics, Battle Results,
Rewards — must persist across sessions and has clear relational structure:
a Player owns many Pets and Relics, and a Battle Result references both a
Pet instance and a Boss definition (`DATABASE.md` §1-2). `MVP_SCOPE.md` §1
names PostgreSQL as the required persistence technology.

## Decision

All durable, cross-session data is stored in PostgreSQL, modeled as the
entities and relationships defined in `DATABASE.md`. PostgreSQL is queried
only outside the hot resolution path — at battle start (loadout validation,
`API_CONTRACTS.md` §2) and battle end (writing `BattleResult`, `TDD.md` §4.2)
— never per-action during an active battle.

## Alternatives Considered

### Option A — A Different RDBMS (e.g. MySQL, SQL Server)
Rejected implicitly: `MVP_SCOPE.md` §1 and `DATABASE.md` both name
PostgreSQL specifically; no alternative RDBMS is referenced anywhere in the
existing documentation.

### Option B — A Document/NoSQL Store (e.g. MongoDB)
Rejected: the data model in `DATABASE.md` §1-2 is explicitly relational
(Player 1—N Pet, Player 1—N Relic, foreign keys to static Definition
tables), which fits a relational schema more directly than a document
model.

### Option C — PostgreSQL (Chosen)
Relational database for all durable entities, with indexes scoped to the
actual query patterns already identified (`DATABASE.md` §4).

## Why

The entity relationships in `DATABASE.md` are inherently relational (owned
instances referencing shared static definitions, foreign-keyed battle
history), and PostgreSQL is the technology already named for this role in
`MVP_SCOPE.md` §1.

## Consequences

### Positive
- Relational constraints (`DATABASE.md` §3) enforce data integrity (valid
  Tier/Star/Level ranges, valid Card categories) at the storage layer.
- Clear separation from Redis: PostgreSQL never holds transient battle
  state, avoiding any ambiguity about which store is authoritative for what
  (ADR-005).

### Negative
- None identified beyond standard RDBMS operational overhead (backups,
  migrations) — not elaborated further here to avoid speculative content
  not present in `DATABASE.md`.

### Trade-offs
- Schema changes (e.g. adding Card progression, per `DATABASE.md` §2's
  noted ASSUMPTION) require migrations, unlike a schemaless store — accepted
  given the relational shape of the existing data model.

## Related Documents

- `docs/00-overview/MVP_SCOPE.md` (§1)
- `docs/02-technical/TDD.md` (§4)
- `docs/02-technical/DATABASE.md` (entire document)
