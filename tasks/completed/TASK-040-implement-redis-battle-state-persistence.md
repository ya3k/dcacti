# TASK-040 — Implement Redis Battle State Persistence

---

## Metadata

```text
Task ID:           TASK-040
Type:              FEATURE
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     persistence
Supporting Agents: backend, testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis,
                   backend/persistence-analysis, quality/architecture-conformance,
                   quality/scope-validation, testing/test-scenario-generation
Dependencies:      TASK-029, TASK-030 (both DONE)
```

---

## Objective

Make Redis the single source of truth for an active battle's live state:
`POST /api/battle/start` creates `battle:{battleId}:state` on successful
creation, each accepted action loads and saves the state within one
resolution using a `Sequence` compare-and-set with exactly one write-back,
and the key's TTL slides on successful resolutions only — with no
long-lived in-memory battle-state copy remaining in the server process.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — "Active battle state store (Redis)" is
  MVP-required technical infrastructure
- `docs/00-overview/ROADMAP.md` §1 — Phase 2 deliverable ("Active battle
  state in Redis")
- `docs/02-technical/REDIS_STATE.md` §1–§5 — key structure, JSON
  serialization, TTL/lifecycle, `Sequence` compare-and-set concurrency,
  recovery; §7 item 7 + §7 Status note (TASK-030) — the requirement is now
  **due** rather than deferred
- `docs/02-technical/GAME_STATE.md` §2, §5 — the `BattleState` shape
  serialized to Redis and the `Sequence` write-back rules
- `docs/02-technical/ARCHITECTURE.md` §1, §3 (`BattleResolutionService`,
  `BattleStateRepository (Redis)`), §4 steps 1–3, §4.1 (one action → one
  write-back), §5 (anti-overengineering)
- `docs/02-technical/TDD.md` §3, §4 — runtime model ("Persist new
  BattleState → Redis") and persistence strategy
- `docs/02-technical/API_CONTRACTS.md` §3, §6 — battle-start contract and
  error convention
- `docs/02-technical/DATABASE.md` §5 item 3 — active state lives in Redis
  only, never in PostgreSQL mid-battle
- `docs/03-decisions/ADR/ADR-005-redis-active-battle-state.md` — Accepted
  decision this task implements (no new ADR required, `AGENTS.md` §18)
- `docs/00-overview/MVP_SCOPE.md` §2 — OUT list (nothing in this task is OUT)

---

## Scope

### In Scope

- Application-layer `IBattleStateRepository` (or equivalent existing repo
  convention) + `GameServer.Infrastructure/Redis/` implementation of
  `BattleStateRepository` per `ARCHITECTURE.md` §1, §3.
- Create `battle:{battleId}:state` on successful battle creation in
  `POST /api/battle/start` (`REDIS_STATE.md` §3 "Created", §1–§2).
- Resolution path: load state, run the existing Domain resolution, then
  **one** write-back per accepted action guarded by the documented
  `Sequence` compare-and-set; abort/retry on mismatch (`REDIS_STATE.md`
  §4 items 1, 2, 3, 5, 6).
- TTL refresh (sliding expiry, configured default of 30 minutes) on every
  successful resolution; a rejected action writes nothing and does not
  touch the key, TTL, or `Sequence` (`REDIS_STATE.md` §3, §4 item 7).
- Reads used by the existing hub paths (`JoinBattle` initial state) served
  from the store, so the process-local battle registry is no longer the
  source of truth (`REDIS_STATE.md` §2 item 2, `ARCHITECTURE.md` §4).
- Infrastructure DI registration, following the existing conditional
  `IConnectionMultiplexer` pattern (`GameServer.Infrastructure/DependencyInjection.cs`).
- Test and documentation updates required by lifting the `REDIS_STATE.md`
  §7 deferral (`AGENTS.md` §17, §15) — see Acceptance Criteria and
  Implementation Notes.

### Out of Scope

- **Battle-end explicit delete and durable result write** —
  `REDIS_STATE.md` §3's explicit-delete trigger and `ARCHITECTURE.md` §4
  step 4 both require the `BattleResult` write to PostgreSQL
  (`DATABASE.md` §1), which does not exist yet. TTL governs the key until
  that task lands (`TDD.md` §4.2 "TTL or explicit delete"). Report if this
  boundary cannot be held.
- `GET /api/battle/{battleId}/result` / `BattleResult` persistence and any
  reward content (`ROADMAP.md` §1 Phase 2 separate line item;
  `tasks/backlog/TASK-033-*` is BLOCKED on design inputs).
- `GetBattleState` hub method — reconnect/resync is `ROADMAP.md` §1
  Phase 3 and `SIGNALR_PROTOCOL.md` §7; its documented absence
  (`SIGNALR_PROTOCOL.md` §0/§7) must remain untouched.
- `battle:{battleId}:lock` — documented as optional; the `Sequence`
  compare-and-set is the source of correctness (`REDIS_STATE.md` §4 item
  4, `ARCHITECTURE.md` §5).
- Authentication / session / Player identity
  (`tasks/backlog/TASK-034-*`, ADR-007) — the store is keyed by
  `battleId` only.
- Any new Redis key or field, any gameplay rule, and any Match-3 / combat
  / pet / card / relic / boss behaviour change (`REDIS_STATE.md` §1–§2,
  `AGENTS.md` §7, §16).
- Multi-instance scaling, backplane, replication
  (`REDIS_STATE.md` §1, §5; `TDD.md` §7).
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State

`BattleStateService` keeps battles in a process-local
`ConcurrentDictionary<string, BattleState>` (`src/backend/GameServer.Application/Battle/BattleStateService.cs:203`),
and `BattleStartService` documents that it writes no Redis key
(`BattleStartService.cs:62-65`). The `BattleState` JSON mapping already
exists from TASK-029 (`BattleStateJson`), StackExchange.Redis plus a
conditional `IConnectionMultiplexer` registration and Redis health check
already exist (`GameServer.Infrastructure/DependencyInjection.cs:71-77`), but
there is no `Infrastructure/Redis/` module and no `BattleStateRepository`.
`BattleState` can already be created (`POST /api/battle/start`, TASK-030)
and resolved (accepted `Swap`), so `REDIS_STATE.md` §7 item 7's "created and
resolved" precondition is met. Existing API tests blank
`ConnectionStrings:Redis` and several tests assert that no Redis key is
written — assertions that encoded the now-expired deferral.

---

## Acceptance Criteria

- [ ] A successful `POST /api/battle/start` writes `battle:{battleId}:state`
      containing JSON that round-trips to the created `BattleState`
      (`REDIS_STATE.md` §1, §2 item 1, §3 "Created"; `GAME_STATE.md` §2).
- [ ] Each accepted action performs exactly one write-back, and the write
      succeeds only when the stored `Sequence` still matches the value read
      at the start of that resolution; a mismatch aborts the write and the
      resolution is retried against fresh state
      (`REDIS_STATE.md` §4 items 1–3, 5).
- [ ] The key's TTL is reset to the configured default only on a successful
      resolution; a rejected action performs no write, no TTL reset, and no
      `Sequence` change (`REDIS_STATE.md` §3, §4 item 7;
      `MATCH3_RULES.md` §2.1.5).
- [ ] `Sequence` is the only compare-and-set token; `Turn` is never compared
      (`REDIS_STATE.md` §4 item 6; `GAME_STATE.md` §5 item 3).
- [ ] After this task, the server process holds no long-lived in-memory
      copy of `BattleState` across requests: creation, resolution, and the
      `JoinBattle` initial-state read are all served through the repository
      (`REDIS_STATE.md` §2 item 2; `ARCHITECTURE.md` §4 steps 1–3).
- [ ] No additional Redis key, hash field, or Redis-only JSON field is
      introduced, and no active state is written to PostgreSQL
      (`REDIS_STATE.md` §1–§2; `DATABASE.md` §5 item 3).
- [ ] When the store cannot be reached or is not configured, the action
      fails instead of persisting state in process memory, and the response
      follows the documented error convention (`REDIS_STATE.md` §2 item 2;
      `API_CONTRACTS.md` §6) — any new machine-readable code is documented
      in `docs/` before the code that returns it (`AGENTS.md` §17).
- [ ] No battle-end delete, `BattleResult` write, `GetBattleState`, or
      session/identity behaviour is added (Out of Scope above; the documented
      absence of `GetBattleState` in `BattleHub` and
      `src/frontend/client/tests/RuntimeBoundaries.test.ts:242` plus
      `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs` still holds).
- [ ] Tests that asserted the expired §7 deferral ("writes no Redis key",
      "process-local registry") are updated with their reason for change
      recorded against `REDIS_STATE.md` §7's status note
      (`AGENTS.md` §15) — no assertion is weakened to make an incorrect
      implementation pass.
- [ ] Documentation reflects the new behaviour: the `REDIS_STATE.md` §7
      status note and source comments that state "no Redis key is written"
      are updated (`AGENTS.md` §17, §23).
- [ ] All relevant tests pass at the required validation depth
      (`core/validation.md` §2, HIGH for this risk level).
- [ ] Quality review checklist passes (`quality/review.md` §1).
- [ ] No authoritative rules or contracts violated
      (`AGENTS.md` §10 / ADR-001, ADR-005).

---

## Affected Files & Areas

```text
[ ] src/backend/ (Application: repository contract + BattleStartService /
                  BattleStateService wiring; Infrastructure: new Redis/ module;
                  Api: composition only if required)
[x] src/frontend/client/ — none (out of scope)
[x] tests/ (Application unit tests, Api integration tests, Infrastructure tests)
[x] docs/ (REDIS_STATE.md §7 status note; any other doc statement that the
           record is not yet written)
```

---

## Implementation Notes

- Reuse the TASK-029 serializer; do not write a second serialization path
  or a second randomization/identity mechanism (`AGENTS.md` §9, §11).
- `ARCHITECTURE.md` §3/§4 and `REDIS_STATE.md` §2 item 2, §4 item 1 name
  `BattleResolutionService`; today that role is served by
  `BattleStateService`. Prefer the smallest change over a rename — but
  report the naming mismatch if it blocks a clean repository seam
  (`AGENTS.md` §16).
- Register the repository beside the existing conditional Redis
  `IConnectionMultiplexer` block in
  `GameServer.Infrastructure/DependencyInjection.cs:71-77`. Tests that blank
  `ConnectionStrings:Redis` (`BattleStartEndpointTests.cs:604`,
  `BattleStartSmokeTest.cs:219`, `AuthPlayerOwnershipTests.cs:233`) already
  substitute an in-memory `GameDbContext` — follow that established
  substitution pattern for the repository rather than adding a production
  in-memory fallback.
- Deferral-era comments/tests to revisit: `BattleStartService.cs:62-65`,
  `BattleStateService.cs:496`, `RuntimeStatus.cs:8`,
  `BattleStateSerializationLifecycleTests.cs:33-38, 252-275`,
  `BattleStateServiceTests.cs:540-549`.
- `docker-compose.yml` already provides a local Redis (port 6379) and
  `.env.example` already documents `ConnectionStrings__Redis`.
- Battle-end flow (`ARCHITECTURE.md` §4 step 4) is deliberately not wired
  in this task — see Out of Scope.

---

## Testing Requirements

### Required Verification
```text
[ ] Unit tests         — repository compare-and-set (Sequence match vs
                          mismatch), TTL refresh vs no-refresh on rejection,
                          BattleState JSON round-trip through the store
[ ] Integration tests  — battle start creates the key; accepted Swap
                          produces exactly one write-back with the expected
                          Sequence; rejected Swap writes nothing; initial
                          JoinBattle state served from the store; store
                          unavailable ⇒ action fails without in-process
                          persistence
[ ] Gameplay scenarios — Given a created battle, When a Swap is accepted,
                          Then Redis holds the resolved BattleState described
                          by GAME_STATE.md §2 and the emitted events are
                          unchanged; Given a rejected Swap, Then the stored
                          record, Sequence and TTL are untouched
                          (REDIS_STATE.md §4 item 7, MATCH3_RULES.md §2.1.5)
```

### Key Edge Cases
- Concurrent/reserialized write conflict: a stale `Sequence` must not
  overwrite (`REDIS_STATE.md` §4 items 2–3, 6).
- TTL sliding on the successful resolution only — including the first
  resolution after creation (`REDIS_STATE.md` §3).
- Round-trip losslessness for counters, board content, and
  `LastCommittedSwapPair`/`Combo`/`MatchCount` zero-vs-absent rules
  (`REDIS_STATE.md` §7 items 9–12; `GAME_STATE.md` §2.1.7 item 5).
- `docker-compose` Redis used for the integration layer; unit tests use the
  repo's existing substitution pattern.

---

## Stop Conditions

- Universal stop conditions in `AGENTS.md` §20 always apply.
- If required behavior cannot be fully derived from Authoritative
  References: STOP per `AGENTS.md` §7.
- If implementation appears to require the battle-end delete, a
  `BattleResult`/PostgreSQL write, or reward content: STOP and report —
  that is a separate task, not this one.
- If a new Redis key/field, a new SignalR method, or a new REST contract
  element would be needed: STOP per `AGENTS.md` §7 / §18.
- If handling an unconfigured/unreachable Redis would require inventing a
  documented contract not derivable from `REDIS_STATE.md` /
  `API_CONTRACTS.md` §6: STOP and report (`AGENTS.md` §20).
- If task requires client-authoritative game logic calculation: STOP per
  `AGENTS.md` §10.
- If task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: STOP & decompose.

---

## Completion Evidence

### Outcome — DONE

Redis is the active battle state store per `REDIS_STATE.md` §1–§4. A successful
`POST /api/battle/start` writes `battle:{battleId}:state`; each accepted action
loads that record, resolves it, and performs **one** write-back guarded by the
`Sequence` compare-and-set; the key's TTL is the documented 30-minute sliding
expiry, refreshed on a successful resolution only. The server process holds no
long-lived in-memory copy of `BattleState`. The `REDIS_STATE.md` §7 deferral is
discharged (status note updated); §3's explicit battle-end delete remains
out of scope because the `BattleResult` → PostgreSQL precondition
(`DATABASE.md`) does not exist yet, so the TTL governs expiry.

### Changed Files

**Created — Application**
- `src/backend/GameServer.Application/Battle/IBattleStateRepository.cs` — the
  active-state persistence contract: `CreateAsync`, `GetAsync`,
  `TryUpdateAsync(state, expectedSequence)`. It names no key, TTL, connection,
  or Redis type, so Domain/Application stay storage-independent.

**Created — Infrastructure**
- `src/backend/GameServer.Infrastructure/Redis/BattleStateRepository.cs` — the
  Redis implementation: key `battle:{battleId}:state`, value written/read through
  the TASK-029 `BattleStateSerializer`, 30-minute sliding TTL, and a Lua
  compare-and-set that writes + refreshes the TTL only when the stored `Sequence`
  still matches. Failures propagate; there is no in-process fallback.

**Modified — Application**
- `.../Battle/BattleStateService.cs` — now store-backed and async:
  `CreateBattleAsync` (composes then writes the record), `GetBattleAsync` /
  `GetInitialStateForGroupAsync` (read the record), `ResolveBoardAsync`,
  `ExecuteSwapAsync` (load → resolve → CAS write-back → abort/retry on mismatch).
  The `ConcurrentDictionary<string, BattleState> _battles` authoritative registry
  and `ActiveBattleCount` are **removed**; the parameterless constructor is
  removed so a store cannot be omitted. The Pet/Boss *configuration* registries
  are retained — they are loadout inputs, not battle state.
- `.../Battle/BattleStartService.cs` — awaits `CreateBattleAsync`; a store
  failure fails creation rather than yielding an unpersisted battle.
- `.../DependencyInjection.cs` — registration comment updated.
- `.../Runtime/RuntimeStatus.cs`, `.../Runtime/RuntimeService.cs` — stale
  "not implemented yet" comments corrected.

**Modified — Infrastructure**
- `.../DependencyInjection.cs` — registers
  `IBattleStateRepository → BattleStateRepository` beside the existing
  conditional `IConnectionMultiplexer`, inside the connection-string branch (no
  in-process substitute is registered when Redis is unconfigured).

**Modified — Api**
- `.../Controllers/BattleController.cs`,
  `.../Controllers/BattleStartStateSummary.cs` — `initialState` reads the created
  record (`ForAsync`).
- `.../Hubs/BattleHub.cs` — `JoinBattle` and `Swap` await the store-backed
  Application calls. **No method, payload member, or event was added or changed:
  the `SIGNALR_PROTOCOL.md` wire contract is untouched.**

**Modified — Domain**
- `.../Battle/BattleState.cs` — one doc-comment cref updated to the renamed
  method. No behavioral change; the serializer and the domain model are unchanged.

**Created — Tests**
- `tests/backend/GameServer.Infrastructure.Tests/RedisBattleStateRepositoryTests.cs`
  (11 tests) — the Redis contract against **real Redis**: key spelling and
  single-key isolation, TASK-029 JSON round-trip through the store, presence of
  the 30-minute TTL at creation, TTL refresh on success (twice, to prove it
  slides repeatedly), no refresh on a stale refusal, CAS success, CAS conflict
  leaving the newer state intact, `Sequence`-only comparison (never `Turn`), and
  no record created for an unknown battle.
- `tests/backend/GameServer.Api.Tests/RedisBattleStateSmokeTest.cs` (1 test) —
  the TASK-040 §20 end-to-end smoke test (see Smoke Test below).
- `tests/backend/GameServer.Application.Tests/BattleStatePersistenceContractTests.cs`
  (7 tests) — the Application layer's use of the CAS: creation writes the record,
  an accepted action performs exactly one write-back, a rejected action performs
  none, a conflicting write is aborted and retried against fresh state (exactly
  one write applied), an unresolvable contention never reports acceptance and
  never overwrites, and `Sequence` (not `Turn`) is the token.
- `tests/backend/GameServer.Application.Tests/InMemoryBattleStateRepository.cs`,
  `tests/backend/GameServer.Api.Tests/ApiTestBattleStateRepository.cs` — test
  doubles implementing the documented CAS semantics. **Test-only**; the
  production composition registers the Redis implementation and nothing else.

**Modified — Tests**
- `BattleStateServiceTests.cs`, `BossResponseTests.cs`, `VictoryDefeatTests.cs`,
  `BattleStartServiceTests.cs`, `BattleStateSerializationLifecycleTests.cs` —
  migrated to the async/store-backed API. `Assert.Same` assertions whose intent
  was "a rejection left the state untouched" became `WriteCount` + value
  comparisons (strictly stronger); `ActiveBattleCount` assertions became
  store-record assertions.
- `ApiIntegrationTests.cs`, `BattleStartEndpointTests.cs`,
  `BattleStartSmokeTest.cs`, `BossResponseWireTests.cs`,
  `AuthPlayerOwnershipTests.cs` — same migration, plus each host now registers
  the store double (all of them blank `ConnectionStrings:Redis`, so without it
  `BattleStateService` cannot be resolved).
- `InfrastructureRegistrationTests.cs` — asserts the Redis repository is
  registered, that **no** store is registered when Redis is unconfigured, and
  that `BattleStateService` has no store-less construction.

**Modified — Docs**
- `docs/02-technical/REDIS_STATE.md` — §7 item 3 and the §7 status note now
  record that the deferral is discharged: what is written, that the process holds
  no long-lived copy, and that §3's explicit delete stays out of scope until the
  `BattleResult` write exists.

### Validation Results

```text
Domain:          905 passed, 0 failed   (unchanged from pre-task baseline)
Application:     206 passed, 0 failed   (baseline 199; +7 new CAS contract tests)
Infrastructure:  102 passed, 0 failed   (baseline 88; +11 real-Redis contract tests, +3 registration)
Api:             113 passed, 0 failed   (baseline 112; +1 end-to-end Redis smoke test)
Total (backend): 1326 passed, 0 failed
Frontend tests:  188 passed, 0 failed   (12 files; unchanged from baseline)
Frontend build:  succeeded (tsc + vite build; pre-existing chunk-size warnings only)
```

Every backend suite was re-run after all edits stopped; no failures and no
failures ignored. The redis contract and smoke suites were run against a real
Redis at `127.0.0.1:6379` (`PING` verified), not a mock.

### Smoke Test — `RedisBattleStateSmokeTest`

One real request through the **production composition** (real `BattleStartService`,
real `BattleStateService`, real `GameServer.Infrastructure.Redis.
BattleStateRepository`, real Redis; only PostgreSQL is substituted), with the
documented key then read by a raw Redis client. Observed output:

```text
Redis: 127.0.0.1:6379
PING: 00:00:00.0011279
POST /api/battle/start → 200 battleId=5cd5f378a4d44e6e9b4869cf7d46d14b
EXISTS battle:5cd5f378a4d44e6e9b4869cf7d46d14b:state = true
KEYS battle:5cd5f378a4d44e6e9b4869cf7d46d14b:* = [battle:5cd5f378a4d44e6e9b4869cf7d46d14b:state]
JSON root members: battleId, turn, sequence, rngSeed, rngState, boardState,
                   combo, matchCount, petState, bossState
TTL at creation = 00:29:59.9250000
Swap 1→2
TTL before Swap (deliberately shortened) = 00:01:00
REDIS after Swap: sequence=1 turn=1 combo=1 matchCount=1 committedPair=(1,2)
                  bossHp=4947 petHp=920
TTL after Swap = 00:29:59.9700000 (slid forward)
BattleStateUpdated push: sequence=1 matchCount=1
Stale CAS refused; stored matchCount still 1 (not 4242)
```

What this verifies, against the documented contract:

1. **Key exists and is the only one** — `battle:{battleId}:state` (§1); no
   lock key, no per-resolution key, no board key.
2. **JSON shape** — exactly the `GAME_STATE.md` §2 root member set, with no
   Redis-only and no wire-projection member (§2 item 1), round-tripping through
   the TASK-029 serializer to `turn=0, sequence=0, 64 cells`.
3. **Sequence** — `0` at creation, `1` after exactly one committed Swap
   (§4 item 5: one write-back per resolution).
4. **TTL present** — ≈30 minutes at creation; deliberately shortened to 60 s,
   then observed back at ≈30 minutes after the successful resolution, proving
   the §3 sliding refresh is a real reset.
5. **Mutation updates Redis** — the stored record changed and matches what the
   hub pushed over SignalR (`BattleStateUpdated`), so Redis and the wire agree.
6. **Stale CAS cannot overwrite** — an update built from the pre-Swap state
   (`expectedSequence: 0`) was refused and the Sequence-1 record kept its own
   `matchCount` (1, not the stale attempt's 4242) — §4 items 2–3.

### Scope Verification

Confirmed **NOT** implemented by TASK-040:

```text
[x] GetBattleState                    — no such hub method; SIGNALR_PROTOCOL.md §0/§7 absence intact
[x] Reconnect / Resync                — no snapshot-recovery path added (ROADMAP Phase 3)
[x] BattleResult persistence          — none
[x] Reward persistence                — none
[x] PostgreSQL battle persistence     — no table, no migration, no EF change
[x] Authentication / JWT / session    — TASK-034 untouched, no middleware, no contract change
[x] Gameplay changes                  — no Match-3 / combat / pet / card / relic / boss rule changed
[x] New Redis key or field            — §1's single state key only
[x] New SignalR method / event        — BattleHub's surface and payloads unchanged
[x] Frontend changes                  — none by this task
[x] tasks/completed/*                 — untouched (TASK-029/030/031/031A not reopened)
```

Source-of-truth check: implementation follows `REDIS_STATE.md` (key, JSON, TTL,
CAS, failure semantics) and reuses the TASK-029 serializer unchanged, so no
higher-level contract was modified to ease the implementation. `REDIS_STATE.md`
§7's status note was updated because the task discharges the deferral it records.


