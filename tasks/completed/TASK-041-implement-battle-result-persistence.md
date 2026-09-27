# TASK-041 — Implement Battle Result Persistence at Battle End

---

## Metadata

```text
Task ID:           TASK-041
Type:              FEATURE
Status:            DONE
Risk:              HIGH
Priority:          CRITICAL
Primary Agent:     backend
Supporting Agents: persistence, realtime, testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, backend/api-contract-validation,
                   backend/persistence-analysis, testing/test-scenario-generation,
                   quality/architecture-conformance, quality/implementation-review
Dependencies:      TASK-023, TASK-024, TASK-029, TASK-030, TASK-040 (all DONE);
                   TASK-042/043/044/045/046/047/048/049/050/051 (DONE — resolved
                   BattleResult contracts); TASK-052/053 (DONE — BossDefinition
                   provisioning contract + the three rows); TASK-034/054/055 (DONE —
                   application session authentication, ADR-015 D1–D11)
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

*Recorded at task creation; retained as history. See **Completion Evidence**
for what the implementation actually changed.*

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

- [x] A resolution that emits `BattleWon` or `BattleLost` produces exactly one
      `BattleResult` row whose fields come from authoritative server state,
      with no client-supplied value
- [x] A non-terminal resolution writes no row and deletes no key
- [x] Ordering holds: the row is written before the key is deleted; if the
      PostgreSQL write does not succeed, `battle:{battleId}:state` is not
      deleted
- [x] After a successful battle end the `battle:{battleId}:state` record no
      longer exists; `REDIS_STATE.md` §3's status note no longer says the
      delete is unimplemented
- [x] `GET /api/battle/{battleId}/result` returns the §4 response shape for a
      completed battle and `404` with `{ "error": "BATTLE_NOT_FOUND" }` (§6)
      otherwise
- [x] A migration creates the entity and the `DATABASE.md` §4 index
- [x] `RewardSummary` is persisted as JSON containing no invented reward line
      items or XP values
- [x] No `Status` / lifecycle field is added to `BattleState`, and no
      `BattleWon` / `BattleLost` payload member changes
- [x] All relevant tests pass at the required validation depth
      (`core/validation.md` §2)
- [x] Quality review checklist passes (`quality/review.md` §1)
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Completion Criteria

```text
[x] Requirement understood; MVP scope checked (MVP_SCOPE.md §1, §4)
[x] Authoritative References read; no documentation conflict introduced (§4)
[x] Existing implementation checked (see Current Repository State)
[x] Plan created under development/feature.md
[x] Code implemented in Application / Infrastructure / Api (plus the Domain
    result record and the mapping the battle-end lookup needs)
[x] Relevant tests added/updated and passing
[x] REDIS_STATE.md §3 status note updated for the now-implemented delete
[x] No unrelated behavior changed (§16); tasks/completed/ untouched
[x] Completion Evidence filled below
```

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain — the BattleResult record + BattleOutcome value set +
    BattleSeed; Application — battle-end sequencing, the result/query services,
    the repository/lookup contracts, the active-state delete operation;
    Infrastructure/Postgres — entity configuration, repository, the two
    battle-end lookups, migration; Infrastructure/Redis — the delete;
    Api — result endpoint + response shape)
[ ] src/frontend/client/  — no change (out of scope, and none needed)
[x] tests/ (Domain — none new; Application / Infrastructure / Api — new suites)
[x] docs/ (REDIS_STATE.md §3 status note + version history only)
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

### Changed Files

**Created — Domain**
- `src/backend/GameServer.Domain/Battle/BattleResult.cs` — the documented
  eight-value battle-end record (`DATABASE.md` §1). No `Status`, snapshot,
  winner/loser, `Sequence`, RNG, or board member.
- `src/backend/GameServer.Domain/Battle/BattleOutcome.cs` —
  `victory` | `defeat` as the closed set (`GAME_EVENTS.md` §2, TASK-050
  Decision C), plus `BattleOutcomes` as the one place the contract's spellings
  are stated so persistence and REST cannot drift.
- `src/backend/GameServer.Domain/Battle/BattleSeed.cs` — the wrapper that makes
  "no seed supplied" distinct from a legal `0` seed, so
  `CreateBattleAsync` accepts exactly the documented battle-start inputs.
- `src/backend/GameServer.Domain/Players/ICombatStatsSource.cs` — the
  battle-readiness boundary for `DATABASE.md` §1 sourcing item 3's documented
  condition.
- `src/backend/GameServer.Domain/Players/Player.cs` — added
  `IsCombatReady`, the named concept that condition reads, explicitly recorded
  as the only value the current contract supports (no readiness column exists).

**Created — Application**
- `src/backend/GameServer.Application/Battle/BattleResultService.cs` — the
  battle-end step: documented Identity lookup, fail-closed on an unresolved
  definition, the eight-value row built from authoritative state and the server
  clock, the PostgreSQL write, and the active-state delete **only after** it.
- `src/backend/GameServer.Application/Battle/BattleResultQueryService.cs` —
  `API_CONTRACTS.md` §4's owner-only read; missing and foreign are one answer.
- `src/backend/GameServer.Application/Battle/IBattleResultRepository.cs`, and
  `IBossDefinitionLookup.cs`, `IBattleResultPersistence.cs` — the three
  Application contracts the boundaries above are expressed through.

**Created — Infrastructure**
- `Postgres/Configurations/BattleResultConfiguration.cs` — the eight columns,
  the `BattleId`-keyed primary key, the three `Restrict` foreign keys, the
  outcome conversion, and the one `(PlayerId, CompletedAt DESC)` index (§4).
- `Postgres/Repositories/BattleResultRepository.cs`,
  `BossDefinitionLookup.cs`, `PlayerCombatProfileSource.cs` — the durable write
  and the two battle-end reads, on the existing Postgres persistence boundary.
- `Postgres/Migrations/20260927075413_AddBattleResultPersistence.cs`
  (+ `.Designer.cs`) — schema only (no row is provisioned; a `BattleResult` is
  a battle's own terminal record).

**Modified — src/backend**
- `Application/Battle/BattleStateService.cs` — the terminal paths now invoke
  the documented battle-end step after the `Sequence` compare-and-set commits;
  the outcome is read from the resolution's own `BattleWon`/`BattleLost` events
  (the only documented end signal). Added the diagnostic record of terminal
  resolutions that reached no durable result.
- `Application/Battle/BattleStartService.cs` — the creation call site follows
  the seed-carrying creation signature.
- `Application/Battle/IBattleStateRepository.cs` — gained the §3-documented
  `DeleteAsync`; the "no deletion" header text corrected.
- `Application/DependencyInjection.cs` — registers the result, query, and
  battle-end boundaries and the server clock (`TimeProvider.System`).
- `Infrastructure/Redis/BattleStateRepository.cs` — implements the documented
  battle-end delete of `battle:{battleId}:state`; class docs updated.
- `Infrastructure/Postgres/GameDbContext.cs` — the `BattleResults` set.
- `Infrastructure/Postgres/Configurations/BossDefinitionConfiguration.cs` —
  the `Identity` conversion is now declared on the property with an explicit
  `ValueComparer`, so the battle-end lookup filters in SQL instead of failing to
  translate.
- `Infrastructure/DependencyInjection.cs` — registers the three new
  persistence boundaries.
- `Api/Controllers/BattleController.cs` — `GET /api/battle/{battleId}/result`,
  thin; the caller identity comes from the session, never the request.
- `Api/Controllers/BattleResultResponse.cs` (new) — the §4 body: exactly
  `battleId`, `outcome`, `rewards`, `durationTurns`, with `rewards` emitted as
  the stored JSON object.

**Modified / created — tests**
- `Application.Tests/BattleResultServiceTests.cs` (new, 24 tests) — the eight
  fields, `DurationTurns` at `Turn` 0/1/N and never from `Sequence`, the two
  outcomes, `RewardSummary = {}`, the Identity→`BossDefinitionId` lookup, the
  fail-closed cases, the write-then-delete order, both failure paths, and
  at-most-one-row.
- `Application.Tests/BattleResultTerminalFlowTests.cs` (new, 10 tests) — the
  same contract through the real resolution pipeline.
- `Application.Tests/BattleResultTestDoubles.cs` (new) — the in-memory doubles.
- `Infrastructure.Tests/BattleResultPersistenceTests.cs` (new, 26 tests) and
  `BattleResultPostgresTests.cs` (new, 11 tests, live PostgreSQL) — the
  declared model and the applied schema: columns, key, foreign keys, index
  descending, `jsonb`, round-trip, duplicate protection, and the lookup against
  the provisioned rows.
- `Api.Tests/BattleResultEndpointTests.cs` (new, 15 tests) and
  `BattleResultSmokeTest.cs` (new, 1 test, real PostgreSQL + real Redis).
- Existing suites updated only where the change legitimately moved a documented
  boundary: `BattleStateServiceTests` (the service's field set),
  `InMemoryBattleStateRepository` / `ApiTestBattleStateRepository` (the new
  interface member), `PlayerPersistenceTests` (properties vs stored columns),
  `CardPersistenceTests` / `RelicPersistenceTests` (the model's table list).
  **No assertion was weakened to make an implementation pass.**

### Validation Results

Baseline recorded before implementation: Domain 911 · Application 210 ·
Infrastructure 139 · API 168 — **1428 tests, 0 failures**.

- `dotnet build src/backend/GameServer.sln` — **PASS**, 0 errors. Warnings are
  all pre-existing (`NU1900` vulnerability-feed timeouts; `MSB3277` EF Core
  version unification; `xUnit2013`/`CS0618`/`CS8631` in pre-existing tests).
- `GameServer.Domain.Tests` — **PASS (911)** — unchanged.
- `GameServer.Application.Tests` — **PASS (244)**, was 210 (**+34**).
- `GameServer.Infrastructure.Tests` — **PASS (176)**, was 139 (**+37**;
  includes 11 live-PostgreSQL assertions).
- `GameServer.Api.Tests` — **PASS (184)**, was 168 (**+16**).
- Full backend suite — **PASS (1515 tests, 0 failures)**, was 1428 (**+87**).
- Frontend `npx tsc --noEmit` — **PASS**, no errors.
- Frontend `npx vitest run` — **PASS (202 tests)**, unchanged.
- `dotnet ef migrations has-pending-model-changes` — "No changes have been made
  to the model since the last migration."
- `dotnet ef database update` — applied
  `20260927075413_AddBattleResultPersistence`; the only environment applied is
  local development PostgreSQL (`localhost:5433`), the only battle-capable
  environment present.

### Smoke Test (real PostgreSQL + real Redis)

`BattleResultSmokeTest` walks the whole documented path with neither store
substituted. Observed output:

```text
PostgreSQL: localhost:5433/dcacti_db      Redis: 127.0.0.1:6379 (PING ok)
POST /api/battle/start                 → 200
EXISTS battle:{battleId}:state         = true
9 committed Swaps played through the hub against the real content Boss
BattleResult row (read by SQL):
  BattleResultId   = <battleId>              ← = the battle's own id
  PlayerId         = <authenticated player>
  PetInstanceId    = <the submitted petId>
  BossDefinitionId = boss-def-hoa-long       ← ≠ "boss-hoa-long"
  Outcome          = defeat
  DurationTurns    = 9                       ← the terminal Turn
  CompletedAt      = 2026-09-27T…+00:00      ← server clock
  RewardSummary    = {}
EXISTS battle:{battleId}:state         = false   ← deleted after the write
GET /api/battle/{battleId}/result      → 200
  {"battleId":"…","outcome":"defeat","rewards":{},"durationTurns":9}
GET without a session                  → 401 UNAUTHENTICATED
GET as a foreign player                → 404 BATTLE_NOT_FOUND
```

The run resolves to `defeat` because the canonical Hỏa Long out-damages the
canonical Pet over the battle's real length; that is the resolution's own
outcome and both documented values are covered — `victory` by the terminal-flow
and endpoint suites, `defeat` here.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic — every persisted
      value comes from the authoritative `BattleState`, the resolution's own
      events, the documented Identity lookup, or the server clock; the endpoint
      reads no request-supplied identity
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1 — "Persistent storage
      (PostgreSQL)"; no system or content added)
- [x] Confirmed TASK-033 untouched (no reward field, value, amount, or XP
      curve; `RewardSummary` is the documented `{}`)
- [x] Confirmed TASK-034 / TASK-054 / TASK-055 untouched (ADR-015 D1–D11
      consumed as written; `git diff` on ADR-015 is empty)
- [x] Confirmed the Redis `Sequence` CAS authority is unchanged, and no second
      concurrency mechanism was introduced

### Report-Only Items (`AGENTS.md` §16)
1. **`GET /api/battle/history`** is listed in `API_CONTRACTS.md` §1 with no
   defining section. Not implemented; no query was invented for it.
2. **`ARCHITECTURE.md` §4 names `BattleResolutionService`; the Application
   orchestrator is `BattleStateService`.** The naming divergence the task file
   records. Not renamed here.
3. **`DATABASE.md` §1 "Identity and reward sourcing for `BattleResult`" has two
   items numbered 3** (the fail-closed rule and the `RewardSummary` owner) — a
   pre-existing numbering defect, left untouched to avoid citation churn.
   `DATABASE.md` was not edited by this task.
4. **`Player.IsCombatReady` is a defined-but-unimplemented contract member.**
   The documented battle-end condition names battle-readiness, but no document
   defines what makes an account *not* ready; the member reports the only value
   the current contract supports (`true`) and must not synthesize a rule. A
   future design task owns it.

---

## Revision History

**Revision 2 — implemented; Status BACKLOG → IN PROGRESS → DONE.** Every
prerequisite the TASK-051 §17 audit listed is now satisfied: TASK-034 is DONE
(ADR-015 D1–D11), TASK-052/053 are DONE (the three canonical `BossDefinition`
rows are provisioned), and TASK-042/043/049/050 had already fixed the identity,
duration, completion, and outcome contracts. The implementation follows those
contracts as written; no new contract was invented and `DATABASE.md` /
`API_CONTRACTS.md` / `GAME_STATE.md` / `GAME_EVENTS.md` / `SIGNALR_PROTOCOL.md`
required no edit. The only documentation change is `REDIS_STATE.md` §3's status
note, which explicitly recorded the delete as unimplemented pending this task.
Four implementation findings were resolved without changing any documented
contract:

1. **The battle-end step could not run inside the resolution.** `BattleStateService`
   is a singleton (it carries each battle's loadout and Boss configuration
   between requests) while the durable result boundary is scoped (it resolves
   the request-scoped `GameDbContext`). The step is reached through a one-method
   Application contract that the composition root resolves in a scope of its
   own, so neither the documented CAS ordering nor the lifetimes changed.
2. **The battle-end step must run after the CAS commit, not inside the
   resolution.** The first implementation persisted during the terminal
   resolution, before the write-back was attempted; a store refusal then left a
   durable result for a transition that was never authoritative. It now runs
   from the path that performed the accepted write-back.
3. **`Identity` needed a property-declared conversion.** The documented lookup
   filters on `Identity`, and comparing through the `BossId` wrapper's `Value`
   member did not translate to SQL. The conversion is now declared on the
   property with a `ValueComparer`, so the lookup is a single indexed column
   comparison.
4. **`rewards` must be emitted as an object, not a string.** `API_CONTRACTS.md`
   §4 shows `"rewards": {}`; returning the stored JSON text directly put a
   quoted string on the wire. The response parses the stored document so the
   payload is the shape the contract defines.

A fifth finding is report-only, not a defect: `Player.IsCombatReady` is defined
by `DATABASE.md` §1 sourcing item 3 and unnamed nowhere else, so it is
implemented as the single value the current contract supports rather than as an
invented readiness rule.

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
