# skills/backend/persistence-analysis.md — Skill: Persistence Analysis

**Version:** 1.0

> Analyze how a change uses Redis (active battle state) and PostgreSQL
> (durable data) against the storage documents: what lives where, how state
> is written, how concurrency and lifecycle are handled, and whether any
> operation is destructive.

## Purpose

Check that persistence behavior — schema changes, queries, repositories,
active-state read/write, TTL/lifecycle, concurrency, recovery — conforms to
`DATABASE.md`, `REDIS_STATE.md`, `GAME_STATE.md`, and the storage boundary
in ADR-005/ADR-006, and surface data-loss and hot-path risks before
implementation or merge.

## When to Use

- A task changes entities, queries, repositories, or migrations.
- A task reads/writes `BattleState` or changes what is stored at battle
  start/end.
- `development/refactor.md` §2 (persistence behavior must stay the same).
- `quality/testing.md` §1 "Persistence tests".
- Anything classified DATABASE, or MEDIUM/HIGH risk items such as a database
  migration or persistence-strategy change (`core/task-intake.md` §3).

## When Not to Use

- REST payload questions (`api-contract-validation`).
- Wire/event delivery (`realtime-protocol-validation`).
- Deciding to change the persistence strategy itself — that is an
  architecture change (`architecture/architecture-change.md` + ADR).

## Inputs

```text
The change (migration, entity/query change, repository or state-store code)
Documentation Context (documentation-discovery output)
The domain rule documents for the data being persisted (PET/CARD/RELIC/… rules)
```

## Prerequisites & Required Context

- Which store owns the data: durable → PostgreSQL; active battle state →
  Redis only (`TDD.md` §4, ADR-005, ADR-006).
- Layering: repositories live in Infrastructure; Domain is persistence-free
  (`ARCHITECTURE.md` §2).

## Authoritative Sources

```text
DATABASE.md                        entities, relationships, constraints, indexes, exclusions
REDIS_STATE.md                     keys, serialization, TTL/lifecycle, concurrency, recovery
GAME_STATE.md                      BattleState shape; Sequence (versioning/concurrency)
TDD.md §4                          persistence strategy, hot-path rule
ARCHITECTURE.md §1–§4              repositories, who may write during a battle
Domain rules for persisted concepts (PET_RULES.md §3–§5, CARD_RULES.md §1,
   RELIC_RULES.md §2, …)          the rules that DATABASE.md constraints cite
ADR-005, ADR-006, ADR-008          why Redis/PostgreSQL; recovery stance
docs/03-decisions/README.md §8     open items (e.g. card ownership model assumption)
```

## Procedure

1. **Identify data and store.** For each piece of data touched, decide
   from the documents which store owns it. Active battle state in PostgreSQL,
   or durable results only in Redis, is a boundary violation
   (`AGENTS.md` §13).
2. **Compare to `DATABASE.md`:** entities, relationships, constraints,
   indexes. Constraint *values* come from the cited domain rule; verify the
   document→rule link is intact rather than copying values.
3. **Compare to `REDIS_STATE.md`:** key structure, serialization contract
   (must correspond to `GAME_STATE.md`), lifecycle (creation, TTL refresh,
   explicit clear), and recovery stance.
4. **Concurrency and sequence.** Verify the documented single-writer / optimistic
   concurrency model is preserved (based on `BattleState.Sequence`), and that
   only the documented writer (`BattleResolutionService`,
   `ARCHITECTURE.md` §4) mutates state during a battle.
5. **Hot path.** Verify nothing new queries PostgreSQL while resolving an
   action (`TDD.md` §4).
6. **Battle end.** Verify the documented end-of-battle sequence: durable
   result written before/with clearing active state (per `TDD.md` §4 and
   `REDIS_STATE.md` §3); no partial result written for abandoned battles.
7. **Destructive-change check.** Any migration or change that drops, renames,
   narrows, or rewrites existing data, or changes the meaning of stored
   values, is *destructive until proven otherwise*. Record what data is
   affected and whether the task explicitly confirms it
   (`.ai/README.md` §13, "destructive database change … not explicitly
   confirmed").
8. **Assumption check.** Flag reliance on assumption-level items
   (e.g. card ownership persistence model, `DATABASE.md` §2 / README §8) —
   report as unconfirmed; do not pick one.
9. **Scope check.** Reject tables/keys for out-of-scope systems
   (`DATABASE.md` "What Is Not Here", `MVP_SCOPE.md` §2).
10. **Layer check.** Persistence types must not leak into Domain
    (`ARCHITECTURE.md` §2); hand structural findings to `architecture-conformance`.

## Outputs

```text
Persistence Analysis Report
| Data item | Owning store (per docs) | Actual/proposed store | Match |
- Schema/constraint mismatches (vs DATABASE.md § / rule doc §)
- Redis key/serialization/lifecycle mismatches (vs REDIS_STATE.md §)
- Concurrency & sequence findings
- Hot-path violations
- Destructive operations: what data, reversible?, explicitly confirmed by the task?
- Assumption-level dependencies
- Out-of-scope persistence
- Recommended smallest correction / required decision
```

## Validation

```text
[ ] Every data item has an owning store citing a document section
[ ] Redis representation checked against GAME_STATE.md, not a private definition
[ ] Every migration/rewrite classified destructive or not-destructive with reason
[ ] Concurrency and single-writer property explicitly checked
[ ] No constraint value or schema field was invented or copied without its source
```

## Stop Conditions

- **Destructive or data-losing operation not explicitly confirmed** by the
  task → stop before proposing or running it.
- Database, Redis, state, or API documents disagree about the same data →
  data-contract conflict.
- Persisting the data requires a schema/key not in the documents →
  missing documentation; do not invent it.
- The change moves responsibility across the Redis/PostgreSQL boundary or
  changes recovery/persistence strategy → architecture change (needs ADR).
- The task depends on an open item (e.g. card ownership model) without a
  recorded decision.

Baseline stop conditions in `.ai/skills/README.md` §6 also apply.

## Common Failure Modes

- Writing "a little" battle state to PostgreSQL for convenience.
- Adding Redis keys or fields beyond the documented structure.
- Adding speculative indexes (the document says add only for a real query
  pattern).
- Treating a rename/column narrowing as harmless.
- Holding a long-lived in-memory copy of battle state across requests.
- Assuming a card ownership model the docs mark as an assumption.

## Traceability

```text
Used by:    development/feature.md (backend/database), refactor.md (§2 persistence),
            bug-fix.md (data bugs); architecture/architecture-change.md;
            quality/testing.md (persistence tests); quality/review.md
Reads:      DATABASE.md; REDIS_STATE.md; GAME_STATE.md; TDD.md §4;
            ARCHITECTURE.md; domain rules; ADR-005/006/008
Produces:   Persistence Analysis Report
Depends on: documentation-discovery (only if context not already supplied)
```
