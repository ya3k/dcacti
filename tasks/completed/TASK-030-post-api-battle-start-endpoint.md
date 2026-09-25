# TASK-030 — POST /api/battle/start Endpoint

---

## Metadata

```text
Task ID:           TASK-030
Type:              FEATURE
Status:            DONE
Risk:              HIGH
Priority:          CRITICAL
Primary Agent:     backend
Supporting Agents: persistence, realtime, testing, review
Workflow:          development/feature.md
Skills:            backend/api-contract-validation, discovery/documentation-discovery, backend/persistence-analysis, testing/test-scenario-generation, quality/scope-validation
Dependencies:      TASK-024, TASK-027, TASK-028
```

---

## Objective

Implement the documented `POST /api/battle/start` endpoint per API_CONTRACTS §3: authenticate the session, validate petId ownership and bossId, run Card/Relic loadout validation and snapshot (from TASK-027/028 services), create the BattleState via the existing battle bootstrap, and return the contract response — with documented error codes and no client-supplied combat values.

---

## Authoritative References

- `docs/02-technical/API_CONTRACTS.md` §3 — request/response contract; §6 — error convention; §1 endpoint summary
- `docs/00-overview/MVP_SCOPE.md` §1 — server-authoritative battle resolution IN
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` item 4 — loadout selected at POST /api/battle/start for the one active Pet
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` items 7–10 — equip counts and snapshot timing at this endpoint
- `docs/02-technical/GAME_STATE.md` §2 — BattleState creation contract; §2.3 PetState from Pet configuration
- `docs/02-technical/SIGNALR_PROTOCOL.md` §1 — client obtains battleId/hub URL from this endpoint
- `docs/02-technical/REDIS_STATE.md` §3, §7 — TTL lifecycle begins when endpoint lands; persistence deferral conditions

---

## Scope

### In Scope
- New controller/endpoint implementing API_CONTRACTS §3 request validation (session, petId owned by Player, bossId valid, loadout via TASK-027/028 services)
- Battle creation through `BattleStateService` with PetState built from persisted Pet (TASK-024) and loadout snapshots applied
- Documented error responses per API_CONTRACTS §6 (including INVALID_LOADOUT / PET_NOT_OWNED as specified)
- Integration tests covering success path and each documented error code

### Out of Scope
- Redis persistence writes — REDIS_STATE §7 deferral gate is separate; endpoint may bootstrap in-memory state per current runtime model (cite §7 in Completion Evidence if still deferred)
- Client-side battle-start logic (client only calls the endpoint)
- Changing the endpoint contract (API_CONTRACTS §3 owns it)
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

`POST /api/battle/start` does not exist. BossDefinition/BossId already reference the contract (`src/backend/GameServer.Domain/Bosses/BossId.cs`, `BossDefinition.cs`). `BattleStateService` (Application) can create BattleState from a `PetConfiguration` but has no HTTP entry point or persistence-backed Pet/loadout validation.

---

## Acceptance Criteria

- [x] `POST /api/battle/start` exists and returns exactly the API_CONTRACTS §3 response shape
- [x] Invalid session, unowned petId, invalid bossId, and invalid Card/Relic loadout each return the documented error code (API_CONTRACTS §6)
- [x] On success, BattleState contains PetState derived from the persisted Pet (TASK-024) plus EquippedRelics (TASK-027) and EquippedCards (TASK-028) snapshots
- [x] No request field accepts Damage/HP/Power/Match/Combo values (API_CONTRACTS §1 server-authoritative note)
- [x] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [x] Quality review checklist passes (`quality/review.md` §1)
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[x] tests/ (unit / integration / gameplay scenarios)
[ ] docs/ (contract already documented — API_CONTRACTS §3)
```

---

## Implementation Notes

- New controller alongside `AuthController` under `src/backend/GameServer.Api/Controllers/`.
- Reuse TASK-027/TASK-028 Application services for loadout validation+snapshot; do not duplicate that logic in the controller.
- PetState construction: persist-derived path should mirror `BattleStateService.PetConfiguration.ToPetState()` but source fields from the persisted Pet row.
- Redis: if the endpoint is implemented while REDIS_STATE §7 still defers storage, document that no key is written yet and §7 remains in force (do not invent a partial store).
- Do not edit `tasks/completed/`.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — request validation branches (session, pet, boss, each loadout failure mode)
[x] Integration tests  — full endpoint success path returns contract response and creates BattleState with snapshots
[x] Gameplay scenarios — Given an authenticated Player with a valid pet/boss/loadout, When POST /api/battle/start succeeds, Then battleId is returned and PetState carries the snapshotted loadout (API_CONTRACTS §3, ADR-012 items 7–10)
```

### Key Edge Cases
- See `docs/02-technical/API_CONTRACTS.md` §6 — every documented error code has a test
- See `docs/02-technical/SIGNALR_PROTOCOL.md` §1 — returned battleId/hub URL feed the connection flow

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If the contract and another doc disagree on request/response fields: STOP per `AGENTS.md` §4 (data contract conflict)
- If task requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose

---

## Completion Evidence

### Outcome — DONE

`POST /api/battle/start` is implemented per `API_CONTRACTS.md` §3. The endpoint
resolves the requesting Player's Pet and ownership, resolves the Boss, delegates
Card and Relic loadout validation/snapshot to the completed TASK-027/028
services, composes `PetState` carrying **both** snapshot arrays, and creates the
authoritative `BattleState` through the existing `BattleStateService`.

### Changed Files

**Created — Application**
- `src/backend/GameServer.Application/Battle/BattleStartService.cs` — the
  orchestration boundary: Pet resolve + ownership, Boss resolve,
  `CardLoadoutService`, `RelicLoadoutService`, then `CreateBattle`. It
  implements no loadout rule; it sequences and maps outcomes.
- `src/backend/GameServer.Application/Battle/BattleStartRequest.cs` — the four
  documented §3 request members and no others.
- `src/backend/GameServer.Application/Battle/BattleStartResult.cs` — the
  documented outcome set (`Started` / `PetNotOwned` / `BossNotFound` /
  `InvalidLoadout`). The Card/Relic internal rejection enums are deliberately
  not surfaced as wire codes.

**Created — Api**
- `src/backend/GameServer.Api/Controllers/BattleController.cs` — the thin §3
  endpoint (`POST /api/battle/start`) and the §6 error mapping.
- `src/backend/GameServer.Api/Controllers/BattleStartResponse.cs` — the §3
  response records (`battleId`, `signalrHub`, `initialState`).
- `src/backend/GameServer.Api/Controllers/BattleStartStateSummary.cs` — the
  one-to-one projection of the created `BattleState` onto `initialState`.

**Modified — Application**
- `.../Battle/BattleStateService.cs` — `PetConfiguration` extended with
  `EquippedCards`, threaded through `ToPetState()` into
  `PetState.AtBattleCreation`. This closes the integration gap named in §4 of
  the task; the smallest change that carries the second snapshot.
- `.../Pets/IPetRepository.cs` — added `GetByIdAsync(petInstanceId)`, the
  ownership-resolution read. Deliberately **not** Player-filtered, so "no such
  Pet" and "another Player's Pet" remain distinguishable to the caller.
- `.../DependencyInjection.cs` — registered `BattleStartService` (scoped).

**Modified — Infrastructure**
- `.../Postgres/Repositories/PetRepository.cs` — EF implementation of
  `GetByIdAsync` (primary-key lookup on `PetInstanceId`).

**Created — Tests**
- `tests/backend/GameServer.Application.Tests/BattleStartServiceTests.cs`
  (38 tests) — orchestration success path, both snapshots, Pet ownership,
  Boss resolution, all four Card rules, all three Relic rules, failure
  atomicity, snapshot immutability, no write-back, server authority.
- `tests/backend/GameServer.Api.Tests/BattleStartEndpointTests.cs` (23 tests) —
  the endpoint end to end: §3 response shape, both snapshots on the wire,
  every documented error code, atomicity, client-supplied authoritative values
  ignored, snapshot isolation against real persistence.
- `tests/backend/GameServer.Api.Tests/BattleStartSmokeTest.cs` (1 test) — the
  documented §22 smoke test, walking one real request through the production
  pipeline and inspecting the resulting authoritative state.
- `tests/backend/GameServer.Infrastructure.Tests/PetOwnershipResolutionTests.cs`
  (6 tests) — the new ownership-resolution read, including that it is not
  Player-filtered and performs no write.

**Modified — Tests**
- `.../Application.Tests/ApplicationRegistrationTests.cs` — asserts
  `BattleStartService` is registered (scoped).

**No change** — `BattleHub`, the Redis boundary, `DATABASE.md` schema (no new
migration, no new `DbSet`), and every TASK-027/028/038/039 contract.

### Validation Results

```text
dotnet build src/backend/GameServer.sln                    — 0 errors
                                                             (2 pre-existing MSB3277
                                                              EF-version warnings only)

dotnet test src/backend/GameServer.sln (full backend suite)
  GameServer.Domain.Tests                                  — PASS (862)
  GameServer.Application.Tests                             — PASS (193)
  GameServer.Infrastructure.Tests                          — PASS (88)
  GameServer.Api.Tests                                     — PASS (109)
  TOTAL                                                    — PASS (1252), 0 failed

npm run test:run (src/frontend/client)                     — PASS (174, 12 files)

Baseline before this task: 1183 passing (Domain 862, Application 154,
Infrastructure 82, Api 85). Added 69; no test regressed.
```

**Mutation check (test teeth).** Two mutants were introduced and both were
caught, then reverted: dropping `EquippedCards` from the `PetConfiguration`
composition failed 4 tests (including the smoke test), and disabling the
`!relics.IsValid` gate failed 5. Note: after reverting a mutant, the stale
compiled artifact initially produced spurious failures until `bin`/`obj` were
purged and rebuilt — the same observation TASK-027 recorded, confirming the
tests observe the built artifact.

### Smoke Test (task §22)

Driven through the unmodified production pipeline (real controller, services,
and persistence wiring) over HTTP:

```text
POST /api/battle/start
  petId        = smoke_pet
  bossId       = Hỏa Long
  cardLoadout  = [heal, shield, power_charge]
  relicLoadout = [relic_a, relic_b, relic_c]
        ↓
    200 battleId=339bfb1836a54944ae4db17eefb95088  signalrHub=/hubs/battle
        ↓
initialState + authoritative store:
  PetState.EquippedCards[] (4) = [heal, shield, power_charge, thanh_xa_skill]
  PetState.EquippedRelics[] (3) = slot 1 relic_a, slot 2 relic_b, slot 3 relic_c
  turn=0 sequence=0 cells=64 boss=Hỏa Long/5000hp petElement=Moc
```

Snapshot independence was verified: after revoking every unlock and deleting
every Relic row, the created battle's two loadout arrays are unchanged. The
created state was retrieved through the existing `GetInitialStateForGroup`
path (the same Application boundary `BattleHub` pushes from on group join) —
no new transport path was introduced.

### Redis (REDIS_STATE.md §7)

**No Redis key is written.** Per this task's Scope, Redis persistence writes
are a separate storage change and remain out of scope; the endpoint bootstraps
its battle in the existing process-local registry, exactly as the Board
Foundation state does. §7's requirement is now **due** rather than discharged:
its deferral reason ("no real battle can exist yet") is spent, so §7 item 3 and
the status note after item 12 were updated to record that the requirement is
now live but not yet implemented by this task. No partial or Card/Relic-specific
Redis structure was introduced.

### Documentation

Minimal staging-status corrections only — no gameplay or wire contract changed
(`AGENTS.md` §17):

- `docs/02-technical/GAME_STATE.md` — §2.3's "implemented so far" list and the
  "remaining collection members" paragraph now record `EquippedRelics[]` /
  `EquippedCards[]` as implemented and populated at battle start, leaving only
  `StatusEffects[]` outstanding; the stale claim that the Domain model "currently
  nests the combat stats under a type named `PlayerState`" was corrected (that
  refactor landed in TASK-025). §2.2 item 2 and §2.3's closing paragraph no
  longer state that `BossState` does not exist or that `POST /api/battle/start`
  cannot create a battle.
- `docs/02-technical/REDIS_STATE.md` — §7 item 3's deferral reason updated to
  record that the endpoint now exists, the deferral is still in force for the
  reason that the write is this task's out-of-scope storage change, and §7
  item 7's requirement is now due; a status note follows items 8/12 so their
  historical staging statements are not read as current.

No other document required a change: `API_CONTRACTS.md` §3/§6 already specified
this endpoint exactly, and `RELIC_RULES.md` / `CARD_RULES.md` / `DATABASE.md` /
`SIGNALR_PROTOCOL.md` were already accurate.

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic — the client submits
      only `petId`/`bossId`/`cardLoadout`/`relicLoadout`; a request supplying
      `battleId`, `turn`, `sequence`, `rngSeed`, `rngState`, `hp`, `atk`, `def`,
      `crit`, `power`, `combo`, `matchCount`, or `board` is verified to change
      nothing
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed `CardLoadoutService` and `RelicLoadoutService` are used, not
      duplicated — no copy-limit, duplicate-instance, category, or Signature
      Skill rule exists in the controller or orchestration layer
- [x] Confirmed no Card, Relic, or Boss gameplay: no cast, effect, trigger,
      stacking, AI, response, attack, victory, or defeat
- [x] Confirmed no new SignalR method or event — `BattleHub`'s surface
      (`JoinBattle`, `Swap`, `Ping`; `BattleStateUpdated`, `ReceiveEvents`,
      `RuntimeStatusChanged`) is unchanged, and no `BattleStarted` /
      `BattleCreated` / `CardsEquipped` / `RelicsEquipped` was invented
- [x] Confirmed no new database schema — no migration, no new `DbSet`, no table
- [x] Confirmed no speculative Redis state — no key written, no partial store
- [x] Confirmed no contract change to TASK-027 (Relic), TASK-028 (Card),
      TASK-038 (Relic loadout), or TASK-039 (Card copy limit)
- [x] Confirmed an invalid loadout creates no partial battle, on every
      documented rejection path
- [x] Confirmed `src/frontend/client/` untouched
- [x] Confirmed `tasks/completed/*` untouched
