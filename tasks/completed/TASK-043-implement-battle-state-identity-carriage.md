# TASK-043 — Implement Battle State Identity Carriage

---

## Metadata

```text
Task ID:           TASK-043
Type:              FEATURE
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: backend, realtime, testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis,
                   gameplay/authority-determinism-audit, backend/persistence-analysis,
                   realtime/realtime-protocol-validation, testing/test-scenario-generation
Dependencies:      TASK-029, TASK-030, TASK-040, TASK-042 (all DONE)
```

---

## Objective

Add the two documented identity members to the runtime battle state —
`BattleState.PlayerId` (`GAME_STATE.md` §2.8) and `BattleState.PetState.PetId`
(`GAME_STATE.md` §2.3, the owned Pet instance) — populate them exactly once at
battle creation from the authenticated battle-start request, and carry them
through JSON serialization and the Redis round-trip unchanged, so the
battle-end persistence path can later copy them into `BattleResult`
(`DATABASE.md` §1 note 2) without re-deriving identity from a session or from
client input (`GAME_RULES.md` §18, ADR-001, `AGENTS.md` §10).

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 (Technical — "Active battle state store
  (Redis)", "Persistent storage (PostgreSQL)"), §4 — scope rule
- `docs/02-technical/GAME_STATE.md` §2.8 — Battle Identity: denotation
  (identity only), staging, not-a-wire-member, battle-end sourcing
- `docs/02-technical/GAME_STATE.md` §2.3 — `PetId` denotes the owned Pet
  instance (the `petId` submitted to `POST /api/battle/start`)
- `docs/02-technical/GAME_STATE.md` §2.0.3 — no lifecycle `Status` member
- `docs/02-technical/DATABASE.md` §1 note 2 — both identities are members of
  the state record and round-trip through serialization with it
- `docs/02-technical/REDIS_STATE.md` §2 (serialization contract), §3 (record
  written at creation)
- `docs/02-technical/API_CONTRACTS.md` §1, §3 — authenticated requesting
  Player at battle start
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4, §7.1 — stage projections and
  snapshot exclude the member; §3.2.19 note 3 still holds
- `docs/03-decisions/ADR/ADR-014-battle-state-player-identity.md` — decision;
  records the Domain `BattleState` + `BattleStateJson` addition as an
  implementation prerequisite for TASK-041's re-audit

---

## Scope

### In Scope

- `BattleState.PlayerId` member (identity only — no stats, no resource pool,
  no gameplay value) populated from the authenticated battle-start request at
  creation (`GAME_STATE.md` §2.8 items 1–2)
- `BattleState.PetState.PetId` member denoting the owned Pet instance
  (`GAME_STATE.md` §2.3)
- Threading both identities from `BattleStartService` through
  `BattleStateService.CreateBattleAsync` into `BattleState.Create` / `PetState`
  construction — set once, never re-derived afterward (`DATABASE.md` §1 note 2)
- Explicit serializer mapping for both members in `BattleStateJson` /
  `BattleStateSerializer` so the serialized Redis record round-trips them
  (`REDIS_STATE.md` §2)
- Tests covering creation, serialization, and the Redis round-trip

### Out of Scope

- **`BattleResult` persistence, DB writes, or battle-end copy logic** —
  TASK-041 owns the battle-end sourcing step (`GAME_STATE.md` §2.8 item 4)
- **Any SignalR wire field, Battle Event, `BattleStateUpdated` stage, or
  `GetBattleState` snapshot member** — `GAME_STATE.md` §2.8 item 3; no
  protocol change (`ADR-014`)
- **`PetDefinitionId`, lifecycle `Status`, auth/session mechanism changes**
  — `GAME_STATE.md` §2.0.3; sessions are TASK-034
- **New owner store, new DB table/column, new Redis key or TTL behavior**
  (`REDIS_STATE.md` §1, §3) — payload content only
- **`RewardSummary` / rewards / XP** — TASK-033 owns
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

`BattleState` (`src/backend/GameServer.Domain/Battle/BattleState.cs`) and
`PetState` (`src/backend/GameServer.Domain/Battle/PetState.cs`) carry neither
member; serialization is explicit per-field in
`src/backend/GameServer.Domain/Battle/Serialization/BattleStateJson.cs` and
`BattleStateSerializer.cs` and maps neither.
`BattleStartService` (`src/backend/GameServer.Application/Battle/`) resolves
the authenticated `playerId` for loadout validation only (ownership check),
and `BattleStateService.CreateBattleAsync(battleId, petConfiguration,
bossDefinition, ct)` receives no identity;
`BattleStateService.PetConfiguration` (built at `BattleStartService` L294–300)
carries no Pet instance id.

---

## Acceptance Criteria

- [x] `BattleState.PlayerId` exists, holds the account/owner identity of the
      Player who created the battle, and carries no stats, resource pool, or
      gameplay value (`GAME_STATE.md` §2.8 item 1)
- [x] `BattleState.PetState.PetId` holds the owned Pet instance id submitted
      to `POST /api/battle/start` (`GAME_STATE.md` §2.3)
- [x] Both members are set exactly once at battle creation from the
      authenticated request and are never re-derived from client input or a
      session afterward (`DATABASE.md` §1 note 2; `GAME_STATE.md` §2.8 item 4)
- [x] Serializing a created `BattleState` and deserializing it returns both
      members unchanged; a record that drops or defaults either does not
      round-trip (`REDIS_STATE.md` §2, `DATABASE.md` §1 note 2)
- [x] The record written to Redis at creation reads back from the repository
      with both members intact (`REDIS_STATE.md` §3)
- [x] No Battle Event, `BattleStateUpdated` stage projection, or
      `GetBattleState` snapshot delivers either member; existing projection
      and wire tests remain green (`GAME_STATE.md` §2.8 item 3;
      `SIGNALR_PROTOCOL.md` §4, §7.1)
- [x] No `Status` or `PetDefinitionId` member added; no API, SignalR, or DB
      contract changed (`GAME_STATE.md` §2.0.3)
- [x] All relevant tests pass at the required validation depth
      (`core/validation.md` §2)
- [x] Quality review checklist passes (`quality/review.md` §1)
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Completion Criteria

```text
[x] Requirement understood; MVP scope checked (MVP_SCOPE.md §1, §4)
[x] Authoritative References read; no documentation conflict introduced (AGENTS.md §4)
[x] Existing implementation checked (see Current State)
[x] Plan created under development/feature.md
[x] Code implemented in Domain (state members + serialization) and Application
    (creation-path threading) only — no Infrastructure/Api production change made
[x] Relevant tests added/updated and passing
[x] No unrelated behavior changed (AGENTS.md §16); tasks/completed/ untouched
[x] Completion Evidence filled below
```

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain / Application)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[x] tests/ (unit / integration)
[ ] docs/ (documentation updates if applicable)
```

---

## Implementation Notes

- Expected touch points: `GameServer.Domain/Battle/BattleState.cs`
  (`BattleState.Create` overloads at L389 / L518),
  `GameServer.Domain/Battle/PetState.cs` (L313 record struct),
  `GameServer.Domain/Battle/Serialization/BattleStateJson.cs` +
  `BattleStateSerializer.cs` (explicit per-field mapping — both members must
  be added by name; there is no attribute-based discovery),
  `GameServer.Application/Battle/BattleStartService.cs` (authenticated
  `playerId` at L154, ownership check at L183, owned Pet already in hand at
  L294–300, `CreateBattleAsync` call at L320),
  `GameServer.Application/Battle/BattleStateService.cs` (`CreateBattleAsync`
  at L348, `BattleState.Create` at L366, `PetConfiguration` type).
  Infrastructure and Api changes are not expected — flag any that seem
  required as a scope question before making it.
- `ADR-014` L112–115 states the serialized record "gains one member: the
  Domain `BattleState` and `BattleStateJson` must add `playerId`"; match the
  serializer's existing JSON naming for sibling members.
- `GAME_STATE.md` §2.8 item 2: the member is added with its owning system —
  this task is that stage; do not alter §2.0 staging rules.
- Validation order in `BattleStartService` is unchanged: a rejected request
  never reaches creation, so no state (and no identity) exists for it.
- Do not modify `tasks/completed/` or `tasks/backlog/TASK-041-*`.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — creation sets both identities; serialize → deserialize
                         round-trip preserves both (drop/default of either
                         member fails the round-trip assertion)
[x] Integration tests  — repository round-trip: state written at creation
                         reads back from Redis with both members intact
                         (RedisBattleStateRepositoryTests pattern)
[x] Gameplay scenarios — N/A: no gameplay behavior changes; identity is
                         denotational only (GAME_STATE.md §2.8 item 1)
[x] Regression         — existing BattleStateUpdated / GetBattleState
                         projection and wire tests still pass with no new
                         member present
```

### Tests Added

```text
Domain      BattleStateSerializationTests — owning-Player round trip
              (RoundTrip_ShouldPreserveTheOwningPlayerIdentity), owned-Pet
              instance round trip
              (RoundTrip_ShouldPreserveTheOwnedPetInstanceIdentity), repeated
              cycle stability, and reject-on-missing for each member
            BattleStateTests — Create_ShouldRecordTheOwningPlayerAndTheSelectedOwnedPet
Application BattleStartServiceTests — owner recorded from the authenticated
              request; PetState.PetId is the owned instance (not the
              definition); owner sourced from the caller, not the Pet row;
              both identities survive runtime serialization
            BattleStateSerializationLifecycleTests — created-battle record
              member set + both identities through the store round trip
Infrastructure RedisBattleStateRepositoryTests — raw stored document and
              repository read both carry both identities; the Sequence-guarded
              write-back preserves them
Api         ApiIntegrationTests —
              BattleStateUpdated_ShouldExcludeBothBattleIdentitiesFromTheWire
              ReceiveEvents_ShouldExcludeBothBattleIdentitiesFromTheWire
            RedisBattleStateSmokeTest — end-to-end
              POST /api/battle/start → Redis → read back carries
              PlayerId = requesting Player and PetState.PetId = submitted petId
```

### Key Edge Cases

- Default/missing `PlayerId` or `PetId` after deserialization must fail the
  round-trip test, not silently pass
- Battle start validation failure leaves no partial state behind
- No member added to any stage projection; snapshot payload shape unchanged
  (protocol tests must assert exclusion, not merely absence of failure)

---

## Stop Conditions

- Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 apply.
- **If the exact identity value (account vs Player row vs session id) for
  `BattleState.PlayerId`, or the exact identity of the owned Pet instance for
  `BattleState.PetState.PetId`, cannot be derived from the Authoritative
  References: STOP per `AGENTS.md` §7 / §20 — report precisely which value is
  ambiguous; do not invent one.**
- If implementing requires adding a field to a `BattleStateUpdated` stage, a
  Battle Event, or a `GetBattleState` snapshot: STOP — that is a protocol
  change beyond this task (`GAME_STATE.md` §2.8 item 3) — report per
  `AGENTS.md` §18.
- If a DB column, new owner store, `PetDefinitionId`, or lifecycle `Status`
  appears required: STOP — out of scope — report per `AGENTS.md` §16.
- If MVP_SCOPE §1 read as not covering the change: STOP & report per
  `MVP_SCOPE.md` §4.
- If the skill budget (7) is exceeded: STOP and decompose per `TASK_TEMPLATE.md`.

---

## Completion Evidence

```text
Commits:            (not committed — workspace changes only, per session)
Tests run:          dotnet test src/backend/GameServer.sln --nologo -v q
Test count:         1340 passed / 0 failed / 0 skipped
                      Domain         911
                      Application    210
                      Infrastructure 104  (real Redis @ 127.0.0.1:6379)
                      Api            115
Performance:        N/A — no hot-path query added; the members ride the
                    existing single write-back (REDIS_STATE.md §4 item 5)
Docs updated:       no — GAME_STATE.md §2.8/§2.3, DATABASE.md §1, REDIS_STATE.md
                    §2 and ADR-014 already specify this contract; the code was
                    written to match them (AGENTS.md §17: docs were correct)
Evidence files:     none created (evidence is the passing suites above)
Quality review:     completed — see the Final Report "Scope Verification"
Risks found:        none blocking. See "Remaining Issues" in the final report.
Change Impact Read: yes
```

---

## Revision History

| Revision | Date       | Change                                                        |
|----------|------------|---------------------------------------------------------------|
| 1        | (created)  | Created from TASK-041 readiness re-audit: two documented      |
|          |            | identity members absent from Domain battle state; ADR-014     |
|          |            | records the Domain addition as TASK-041's implementation      |
|          |            | prerequisite. Assigned sequential ID TASK-043 (next free).    |
| 2        | (executed) | Implemented. `PlayerId`/`PetId` value objects added; both     |
|          |            | members carried by `BattleState`/`PetState`, populated once   |
|          |            | at creation from the authenticated battle-start context,      |
|          |            | serialized in `BattleStateJson`/`BattleStateSerializer`, and  |
|          |            | verified through Domain, Application, real-Redis, and wire-   |
|          |            | boundary tests. No SignalR, DB, TTL, key, or CAS change.      |
