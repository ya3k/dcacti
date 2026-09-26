# TASK-041 — Implement Battle Result Persistence at Battle End

---

## Metadata

```text
Task ID:           TASK-041
Type:              FEATURE
Status:            BACKLOG
Risk:              HIGH
Priority:          CRITICAL
Primary Agent:     backend
Supporting Agents: persistence, realtime, testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, backend/api-contract-validation,
                   backend/persistence-analysis, testing/test-scenario-generation,
                   quality/architecture-conformance, quality/implementation-review
Dependencies:      TASK-023, TASK-024, TASK-029, TASK-030, TASK-040
```

---

## Objective

Close the battle-end persistence step of `ARCHITECTURE.md` §4 item 4: when a
resolution emits `BattleWon` / `BattleLost`, write the durable battle result to
PostgreSQL (`DATABASE.md` §1) **first**, then perform the explicitly documented
delete of `battle:{battleId}:state` (`REDIS_STATE.md` §3), and serve the
completed result through `GET /api/battle/{battleId}/result`
(`API_CONTRACTS.md` §4). The result is derived exclusively from authoritative
server state — the client supplies nothing (`AGENTS.md` §10, ADR-001).

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 (Technical — "Persistent storage
  (PostgreSQL)"), §4 — scope classification rule for this work
- `docs/00-overview/ROADMAP.md` §1 Phase 2 — "Persistent storage: Pet
  Collection, Battle Results, Rewards" and "Active battle state in Redis"
- `docs/02-technical/TDD.md` §4 items 2–3 — result written at battle end, Redis
  state cleared, PostgreSQL off the hot resolution path
- `docs/02-technical/ARCHITECTURE.md` §4 item 4 — write the durable result,
  then instruct the active-state store to clear; §2.1 — layer direction;
  §3 — `PersistenceRepository (Postgres)` component
- `docs/02-technical/DATABASE.md` §1 — `BattleResult` entity; §2 relationships;
  §3 constraints; §4 `BattleResult(PlayerId, CompletedAt DESC)` index
- `docs/02-technical/API_CONTRACTS.md` §1 (endpoint list), §4 (result
  endpoint), §6 (error envelope)
- `docs/02-technical/REDIS_STATE.md` §3 (Lifecycle — explicit delete
  conditioned on the PostgreSQL write), §5
- `docs/02-technical/GAME_EVENTS.md` §2 (`BattleWon` / `BattleLost`), §3 item 2
- `docs/02-technical/GAME_STATE.md` §2.0.3 (no `Status` / lifecycle field —
  outcome is events, not state), §2.0.1 and §5.1 (`Turn`)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.19 item 3 — `reward summary`
  is deferred on the wire; data shape owned by `DATABASE.md`
- `docs/03-decisions/ADR/ADR-005-redis-active-battle-state.md`,
  `ADR-006-postgresql-persistence.md`, `ADR-001-server-authoritative-battle.md`
- `docs/00-overview/GDD.md` §14, `docs/01-game-design/PET_RULES.md` §5 — reward
  and XP magnitudes are balance/config, owned by TASK-033 (BLOCKED)

---

## Scope

### In Scope

- Persisting the battle result to PostgreSQL at battle end, with the entity
  exactly as `DATABASE.md` §1 defines it, sourced from the authoritative
  `BattleState` / battle-creation data (server-side only)
- Detecting battle end from the resolution's `BattleWon` / `BattleLost` events
  — not from a state field (`GAME_STATE.md` §2.0.3 item 2)
- Performing the documented ordering: result write **then** active-state delete
  (`ARCHITECTURE.md` §4 item 4, `REDIS_STATE.md` §3), including adding the
  delete operation that `IBattleStateRepository` currently documents as
  deliberately absent
- `GET /api/battle/{battleId}/result` per `API_CONTRACTS.md` §4, using the §6
  error envelope, resolving the caller through the endpoint's existing
  requesting-player mechanism
- The EF/migration work for the entity plus the §4 index
- `REDIS_STATE.md` §3's TASK-040 status note, which records the delete as a
  sequencing boundary of "its own task" — this task — updated in the same change
- Tests covering the above

### Out of Scope

- **Reward line items, XP amounts, XP curve, level-up** — TASK-033 (BLOCKED).
  `RewardSummary` is stored as JSON with no line items in it until that task
  exists; no reward field, value, or shape is invented here
- **`GET /api/battle/history`** — listed in `API_CONTRACTS.md` §1 but no
  section defines it; report the gap (§21), do not implement it here
- **Session / authentication mechanism** — TASK-034 (BLOCKED); reuse the
  endpoint's current requesting-player resolution, design nothing
- Discord identity exchange implementation (TASK-035 downstream) and
  credential hygiene (TASK-036)
- Any change to `BattleWon` / `BattleLost` emission or wire schema
- Any `Status` / lifecycle field added to `BattleState`
  (`GAME_STATE.md` §2.0.3 item 3 requires its own design task)
- Frontend / client changes; reconnect or `GetBattleState` behavior
  (`SIGNALR_PROTOCOL.md` §7, ROADMAP Phase 3)
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current Repository State

`BattleStateService` already resolves the terminal HP checks and emits
`BattleWon` / `BattleLost` (`src/backend/GameServer.Application/Battle/BattleStateService.cs`),
then performs the Sequence-guarded write-back through
`IBattleStateRepository`. Nothing else in the battle-end path exists: the
Postgres module carries Player / Pet / Relic / Card only — there is no
`BattleResult` entity, configuration, migration, repository, or write — and no
code anywhere deletes `battle:{battleId}:state`. `BattleController` exposes
only `POST /api/battle/start` and its `ResolveRequestingPlayerId()` /
`AuthenticatedPlayerItemKey` mechanism. `IBattleStateRepository`'s header
explicitly records "no deletion" as part of its current surface.
`REDIS_STATE.md` §3's status note states the delete is unimplemented and
conditioned on this missing write.

---

## Acceptance Criteria

- [ ] A resolution that emits `BattleWon` or `BattleLost` produces exactly one
      `BattleResult` row whose fields come from authoritative server state,
      with no client-supplied value
- [ ] A non-terminal resolution writes no row and deletes no key
- [ ] Ordering holds: the row is written before the key is deleted; if the
      PostgreSQL write does not succeed, `battle:{battleId}:state` is not
      deleted
- [ ] After a successful battle end the `battle:{battleId}:state` record no
      longer exists; `REDIS_STATE.md` §3's status note no longer says the
      delete is unimplemented
- [ ] `GET /api/battle/{battleId}/result` returns the §4 response shape for a
      completed battle and `404` with `{ "error": "BATTLE_NOT_FOUND" }` (§6)
      otherwise
- [ ] A migration creates the entity and the `DATABASE.md` §4 index
- [ ] `RewardSummary` is persisted as JSON containing no invented reward line
      items or XP values
- [ ] No `Status` / lifecycle field is added to `BattleState`, and no
      `BattleWon` / `BattleLost` payload member changes
- [ ] All relevant tests pass at the required validation depth
      (`core/validation.md` §2)
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Completion Criteria

```text
[ ] Requirement understood; MVP scope checked (MVP_SCOPE.md §1, §4)
[ ] Authoritative References read; no documentation conflict introduced (§4)
[ ] Existing implementation checked (see Current Repository State)
[ ] Plan created under development/feature.md
[ ] Code implemented in Application / Infrastructure / Api only
[ ] Relevant tests added/updated and passing
[ ] REDIS_STATE.md §3 status note updated for the now-implemented delete
[ ] No unrelated behavior changed (§16); tasks/completed/ untouched
[ ] Completion Evidence filled below
```

---

## Affected Files & Areas

```text
[ ] src/backend/ (Application — battle-end sequencing; Infrastructure/Postgres —
    entity, configuration, migration, repository; Api — result endpoint;
    Application — active-state delete operation)
[ ] src/frontend/client/  — no change expected (out of scope)
[ ] tests/ (unit / integration / scenario)
[ ] docs/ (REDIS_STATE.md §3 status note only)
```

---

## Implementation Notes

- **Hook point:** the terminal paths in `BattleStateService` that already emit
  the outcome events and call `TryStoreResolvedAsync`. `ARCHITECTURE.md` §4
  names `BattleResolutionService`; no class by that name exists — the
  Application orchestrator is `BattleStateService`. Do not rename or split it
  here (§16); report the naming divergence instead.
- **`IBattleStateRepository` must gain a delete.** Its header states the
  surface is deliberately "no enumeration, no deletion, no query"; adding the
  §3-documented delete is a required part of this task, and the header comment
  must be brought in line in the same change. Layer direction (`ARCHITECTURE.md`
  §2.1): Application expresses it in terms of the battle id, Infrastructure
  implements it — sibling to the existing `Redis/` module.
- **Outcome comes from the event batch, not from state.** `GAME_STATE.md`
  §2.0.3 forbids a lifecycle field; presence of `BattleWon` / `BattleLost` in
  the resolution's events is the only documented end signal.
- **Value sources:** identity FKs from the battle's own creation data;
  `DurationTurns` from the authoritative `BattleState.Turn`
  (`GAME_STATE.md` §2.0.1, §5.1); `CompletedAt` from the server clock.
- **Endpoint:** follow the existing `ResolveRequestingPlayerId()` pattern used
  by `POST /api/battle/start`. The documented failure for a battle that is not
  available to the caller is §4's `404` — §4 defines no other status.
- **New Postgres code belongs in** `GameServer.Infrastructure/Postgres/`, the
  same shape as the existing Player / Pet / Relic / Card configurations,
  repositories, and migrations (`ADR-006`).
- **Hot path:** `TDD.md` §4 item 3 — no PostgreSQL read or write during
  ordinary action resolution; only the terminal path touches Postgres.
- Do not modify `tasks/completed/`.

---

## Testing Requirements

### Required Verification
```text
[ ] Unit tests         — terminal resolution maps to exactly one result row;
                         non-terminal resolution maps to none; ordering
                         (write before delete) against a fake repository;
                         failed write does not delete
[ ] Integration tests  — battle played to a win/defeat writes the row, removes
                         the Redis record, and serves it from the result
                         endpoint; 404 path; caller-scoped read
[ ] Gameplay scenarios — Given a battle whose Boss HP reaches 0 / whose active
                         Pet HP reaches 0, When the action resolves, Then one
                         BattleResult row exists, the active-state key is
                         gone, and GET /api/battle/{battleId}/result returns
                         that battle's outcome
```

### Key Edge Cases

- A second terminal action arriving after the key is removed — the state is
  gone (`REDIS_STATE.md` §3); no second row, no second delete
- An abandoned battle whose key expired before completion — no partial result
  is written (`REDIS_STATE.md` §3)
- Two concurrent terminal resolutions — only the Sequence-guarded write-back
  commits (`REDIS_STATE.md` §4), so only one row results
- A result read scoped to a battle the caller does not own
- PostgreSQL unavailable on the terminal path — the key is not deleted

---

## Stop Conditions

- Universal stop conditions from `AGENTS.md` §20 and `.ai/README.md` §13 always
  apply
- If satisfying `API_CONTRACTS.md` §4's `rewards` member requires reward line
  items, XP amounts, or an XP curve: **STOP per `AGENTS.md` §7** — those are
  balance/config values owned by TASK-033 (BLOCKED) and must not be invented
- If `GET /api/battle/history` is required: **STOP** — `API_CONTRACTS.md` §1
  lists it but no section defines its contract; report the documentation gap
  (§21) instead of designing a response shape
- If battle-end handling requires a `Status` / lifecycle field on
  `BattleState`: **STOP** — `GAME_STATE.md` §2.0.3 item 3 requires a separate
  design/ADR task
- If the failed-PostgreSQL-write path requires inventing a retry, rollback, or
  cross-store consistency policy: **STOP & report** — only the documented
  write-then-delete ordering is specified
- If an authorization failure distinct from §4's `404` is required: **STOP per
  `AGENTS.md` §7** — §4 defines only `404 BATTLE_NOT_FOUND`
- If `MVP_SCOPE.md` §1 is read as not covering this work: **STOP & report per
  `MVP_SCOPE.md` §4** — this task is authorized under §1's "Persistent storage
  (PostgreSQL)" bullet and `TDD.md` §4; do not resolve a scope doubt by
  assuming IN status
- If required behavior cannot be fully derived from the Authoritative
  References: STOP per `AGENTS.md` §7
- If the task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: STOP & decompose

---

## Completion Evidence

*To be completed by the executing agent at DONE.*

### Changed Files
- `<file path>` — <summary of change>

### Validation Results
- `<test command or suite>` — PASS (<N> tests)

### Server Authority & Scope Verification
- [ ] Confirmed zero client-authoritative gameplay logic (result derived from
      server state only)
- [ ] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)

---

## Revision History

**Revision 1 — task created (BACKLOG).** Determined as the next critical-path
step after TASK-040. TASK-040 discharged the Redis active-state write but its
`REDIS_STATE.md` §3 status note records the battle-end delete as
"not implemented ... conditioned on the `BattleResult` write to PostgreSQL
(`DATABASE.md`), which does not exist yet. ... This is a sequencing boundary of
its own task." `ARCHITECTURE.md` §4 item 4 and `TDD.md` §4 item 2 state the
same step, and `API_CONTRACTS.md` §4 already defines the read side. Phase 2 of
`ROADMAP.md` §1 lists "Battle Results" as remaining persistent storage (Pet
Collection is done by TASK-024, active battle state by TASK-040), while the
other remaining Phase 2 item — Rewards — is blocked on TASK-033's missing
balance inputs. Reward content, the history-endpoint contract, and the session
mechanism are explicitly excluded rather than decided here.
