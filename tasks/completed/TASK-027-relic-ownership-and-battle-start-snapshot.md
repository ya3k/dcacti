# TASK-027 — Relic Ownership & Battle-Start Snapshot

---

## Metadata

```text
Task ID:           TASK-027
Type:              FEATURE
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     persistence
Supporting Agents: gameplay, backend, testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, backend/persistence-analysis, gameplay/gameplay-behavior-derivation, backend/api-contract-validation, testing/test-scenario-generation
Dependencies:      TASK-023 (DONE), TASK-025 (DONE), TASK-038 (DONE — prerequisite
                   contract; resolved EquippedRelics element type, equip slot
                   index source, and duplicate selection policy)
```

---

## Prerequisite Contract — Resolved by TASK-038 (DONE)

This task's three former blockers were resolved in the authoritative
documentation by **TASK-038 — Resolve Relic Loadout Snapshot Contract**
(now DONE). Nothing below is open:

```text
A. PetState.EquippedRelics[] element type
   → RESOLVED: each element is one owned Relic instance identity
     (RelicInstanceId), not RelicDefinitionId and not resolved definition
     data.                                        RELIC_RULES.md §2.2

B. Equip slot index source
   → RESOLVED: slot index = submitted `relicLoadout` position + 1.
     The request array order is authoritative; no sort by RelicInstanceId,
     RelicDefinitionId, AcquiredAt, or database order is permitted.
     Fixed at battle start.                       RELIC_RULES.md §2.3

C. Duplicate Relic selection policy
   → RESOLVED: the same RelicInstanceId may occupy AT MOST ONE slot
     (duplicates rejected as INVALID_LOADOUT); two DISTINCT instances that
     reference the same RelicDefinition MAY be equipped together.
                                                  RELIC_RULES.md §2.4
```

The resulting validation, slot-assignment, and snapshot behavior is stated
deterministically in `RELIC_RULES.md` §2.5 and `API_CONTRACTS.md` §3. This
task may therefore be implemented without guessing any gameplay rule.

---

## Objective

Persist RelicDefinition and Player-owned Relic instances per DATABASE.md §1–§2, validate the documented battle-start equip rules (3–5 Relics, each owned by the Player, no repeated Relic instance) for the active Pet, and snapshot the selected loadout into `PetState.EquippedRelics[]` as owned Relic instance identities in the submitted `relicLoadout` order at battle start — no persistent equip table and no live inventory queries during battle.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — ~10 Relics; 3–5 equipped per battle IN
- `docs/01-game-design/RELIC_RULES.md` §2 — ownership vs equip; §2.1 — selection/validation; §2.2 — `EquippedRelics[]` element is an owned Relic instance identity; §2.3 — slot index = submitted array position + 1; §2.4 — duplicate instance rejected (`INVALID_LOADOUT`), distinct instances of one definition permitted; §2.5 — deterministic valid/invalid behavior; §4 — trigger order (consumes slot order; not implemented here)
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` item 4 — battle-scoped loadout at POST /api/battle/start
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` items 7–8 — persistent ownership, battle-scoped equipment, PetState.EquippedRelics snapshot, no persistent equip table
- `docs/02-technical/DATABASE.md` §1–§2 — Relic entities/relationships; §3 constraints
- `docs/02-technical/GAME_STATE.md` §2.3 — PetState.EquippedRelics (element identity + preserved order)
- `docs/02-technical/API_CONTRACTS.md` §3 — POST /api/battle/start (ordered `relicLoadout`, validation order); §6 error convention

---

## Scope

### In Scope
- RelicDefinition and Relic (instance owned by Player) entities on `GameDbContext` with EF migration
- Owned-Relic validation for battle start: 3–5 count, ownership, and rejection of a repeated `RelicInstanceId` (RELIC_RULES §2.1, §2.4)
- Snapshot of the selected Relics into `PetState.EquippedRelics[]` as instance identities in submitted order, so element *i* is slot *i + 1* (RELIC_RULES §2.2, §2.3, §2.5)
- Tests for ownership validation, count bounds, duplicate rejection, same-definition distinct instances, order preservation, and snapshot immutability during battle

### Out of Scope
- Persistent equip/loadout table (ADR-012 item 7 forbids it)
- Live PostgreSQL inventory reads during an active battle (snapshot only — ADR-012 item 8)
- Relic trigger/effect/stacking resolution logic of any kind (RELIC_RULES §4–§5 stay as documented; only ownership/snapshot plumbing)
- Any Relic stacking semantics: §2.4 permits two instances of one definition to be equipped but defines no combined effect
- Card loadout (TASK-028); Player Level (TASK-023)
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

No Relic entities exist; `GameDbContext` has no sets. `PetState` has no `EquippedRelics` member. `POST /api/battle/start` is not implemented (API_CONTRACTS §3 documented only). Battle bootstrap path is `BattleStateService` with caller-supplied `PetConfiguration`.

---

## Acceptance Criteria

- [ ] RelicDefinition and owned-Relic entities match DATABASE.md §1–§2, registered with an applying EF migration
- [ ] Battle-start path validates the documented equip count (3–5) and ownership of each selected Relic for the requesting Player (RELIC_RULES §2.1, API_CONTRACTS §3)
- [ ] Battle-start path rejects a selection containing a repeated `RelicInstanceId` with `INVALID_LOADOUT`; a selection with two distinct instances of one `RelicDefinition` is accepted (RELIC_RULES §2.4)
- [ ] `PetState.EquippedRelics[]` exists per GAME_STATE §2.3, holds one owned Relic instance identity per element, and preserves the submitted `relicLoadout` order so `EquippedRelics[i]` is slot `i + 1` (RELIC_RULES §2.2, §2.3, §2.5)
- [ ] The snapshot is written once at battle start and does not change from live DB reads mid-battle (RELIC_RULES §2.5, ADR-012 item 8)
- [ ] No persistent equip table is created; no Relic ownership rows are written during battle
- [ ] No Relic trigger/effect/stacking logic is implemented or changed (RELIC_RULES §4–§5 are out of this task's scope)
- [ ] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[x] tests/ (unit / integration / gameplay scenarios)
[ ] docs/ (documentation already specifies ownership/equip split — ADR-011/012, RELIC_RULES)
```

---

## Implementation Notes

- Snapshot population should hook the same battle-bootstrap entry used by TASK-030 (`POST /api/battle/start`); until TASK-030 lands, expose the validation+snapshot as an Application service callable from tests and later wired by TASK-030.
- Extend `PetState` in `GameServer.Domain/Battle/BattleState.cs` with `EquippedRelics` only as specified by GAME_STATE §2.3 — do not add unrelated members here (TASK-028 adds EquippedCards separately).
- Error codes for invalid counts/ownership follow API_CONTRACTS §6 (`INVALID_LOADOUT` semantics as documented).
- Do not edit `tasks/completed/`; related history: TASK-018 (resource generation may interact with Relic triggers — leave trigger logic untouched).

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — equip-count bounds, ownership check, duplicate-instance rejection, distinct-instances-of-one-definition acceptance, snapshot order/immutability
[x] Integration tests  — owned Relics load from DB at battle start; snapshot survives subsequent ownership-row changes
[x] Gameplay scenarios — Given a Player owning N Relics, When battle starts with a valid 3–5 selection, Then PetState.EquippedRelics holds one instance identity per element in submitted order, so EquippedRelics[i] is slot i + 1 (RELIC_RULES §2.2, §2.3, §2.5)
```

### Key Edge Cases
- See `docs/01-game-design/RELIC_RULES.md` §2.1–§2.5 — count bounds; ownership; duplicate-instance rejection with `INVALID_LOADOUT`; two distinct instances of one `RelicDefinition` are permitted; submitted-order slot assignment; snapshot timing and immutability
- Order-preservation is an explicit obligation: an implementation that re-sorts the selection (by id, acquisition date, or DB order) violates §2.3
- Anti-infinite-chain §5 remains trigger-domain (untouched by this task)
- See `docs/02-technical/API_CONTRACTS.md` §3, §6 — validation order and error convention for invalid loadout

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If an implementation seems to require re-sorting the submitted `relicLoadout` or ordering slots by any property other than array position: STOP — contradicts RELIC_RULES §2.3
- If a persistent equip table seems required: STOP — conflicts with ADR-012 item 7; report per `AGENTS.md` §4/§18
- If task requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10
- If the work expands into Relic trigger/effect/stacking resolution (RELIC_RULES §4–§5): STOP & decompose — that is a separate task, not this one
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose

---

## Completion Evidence

### Outcome — DONE

Relic ownership persistence, loadout validation, and the battle-start snapshot
are implemented. The contract TASK-038 resolved (`RELIC_RULES.md` §2.1–§2.5)
is now executable code, and every one of the task's 23 completion criteria is
satisfied.

### Changed Files

**Created — Domain**
- `src/backend/GameServer.Domain/Relics/RelicDefinition.cs` — static content
  (`RelicDefinitionId`, `Name`, `Trigger`, `Condition?`, `EffectDefinition`)
  per `DATABASE.md` §1. Definition only; no trigger/effect logic.
- `src/backend/GameServer.Domain/Relics/Relic.cs` — an owned instance
  (`RelicInstanceId`, `PlayerId`, `RelicDefinitionId`, `AcquiredAt`) per
  `DATABASE.md` §1. Ownership only: no equip column, no per-instance state.
- `src/backend/GameServer.Domain/Relics/EquippedRelicIdentity.cs` — the
  battle-snapshot identity value type, matching the `PassiveId`/`BossId`
  pattern (`GAME_STATE.md` §2.3).

**Modified — Domain**
- `src/backend/GameServer.Domain/Battle/PetState.cs` — added the
  `EquippedRelics` member (`EquippedRelicIdentity[]?`) and threaded it through
  `AtBattleCreation`. Identity-only, order-preserving, never sorted.

**Created — Application**
- `src/backend/GameServer.Application/Relics/IRelicRepository.cs` — the
  persistence boundary. Exposes **no** equip write.
- `src/backend/GameServer.Application/Relics/RelicLoadoutService.cs` — the
  validator: count (3–5) → distinctness → ownership → order-preserving
  snapshot.
- `src/backend/GameServer.Application/Relics/RelicLoadoutValidation.cs` — the
  result value carrying the ordered snapshot or a rejection reason.
- `src/backend/GameServer.Application/Relics/RelicLoadoutRejectionReason.cs` —
  the internal rejection reasons, all mapping to the one documented
  `INVALID_LOADOUT` outcome.

**Modified — Application**
- `src/backend/GameServer.Application/Battle/BattleStateService.cs` — extended
  `PetConfiguration` with `EquippedRelics` and passed it into `ToPetState()`.
  No redesign; the smallest change that carries the snapshot.
- `src/backend/GameServer.Application/DependencyInjection.cs` — registered
  `RelicLoadoutService` (scoped).

**Created — Infrastructure**
- `src/backend/GameServer.Infrastructure/Postgres/Configurations/RelicConfiguration.cs`
  and `RelicDefinitionConfiguration.cs` — `DATABASE.md` §1/§4 mappings. One
  declared index: `Relic(PlayerId)`. No FK index (the TASK-024 convention
  removal still applies). No definition-level uniqueness constraint.
- `src/backend/GameServer.Infrastructure/Postgres/Repositories/RelicRepository.cs`
  — EF implementation; ownership filtered by `PlayerId` in the query itself.
- `src/backend/GameServer.Infrastructure/Postgres/Migrations/20260925134303_AddRelicPersistence.cs`
  (+ `.Designer.cs`) — the scaffolded migration.

**Modified — Infrastructure**
- `src/backend/GameServer.Infrastructure/Postgres/GameDbContext.cs` — added
  `Relics` and `RelicDefinitions` sets.
- `src/backend/GameServer.Infrastructure/DependencyInjection.cs` — registered
  `IRelicRepository`.
- `.../Migrations/GameDbContextModelSnapshot.cs` — EF-maintained; updated by
  the scaffold.

**Created — Tests**
- `tests/backend/GameServer.Application.Tests/RelicLoadoutServiceTests.cs`
  (25 tests) — count, ownership, duplicates, same-definition, order,
  server authority.
- `tests/backend/GameServer.Infrastructure.Tests/RelicPersistenceTests.cs`
  (14 tests) — field sets, mapping, FKs, indexes, no-equip-table, round-trip.
- `tests/backend/GameServer.Infrastructure.Tests/RelicLoadoutSnapshotTests.cs`
  (6 tests) — real-persistence ownership, ordering, snapshot isolation,
  no write-back, PetState carry-through.
- `tests/backend/GameServer.Domain.Tests/PetStateEquippedRelicsTests.cs`
  (10 tests) — member shape, order preservation, identity-only, no trigger
  state.

**Modified — Tests (staging assertions advanced)**
- `tests/backend/GameServer.Domain.Tests/BattleStateTests.cs` — two
  assertions listed `EquippedRelics` as a not-yet-implemented later-stage
  field. This stage implements it, so it moved from the "deferred" list to the
  "declared" list, exactly as every earlier stage advanced its own. No
  assertion was weakened: both tests still assert the field set exactly, and
  the other two collections (`StatusEffects`, `EquippedCards`) plus
  `PetId`/`Tier`/`Star`/`Level` remain asserted absent.

### Validation Results

```text
dotnet build src/backend/GameServer.sln                 — Build succeeded, 0 errors
                                                          (2 pre-existing MSB3277
                                                           EF-version warnings only)

dotnet test  (focused, Relic/EquippedRelics filters)
  GameServer.Application.Tests    RelicLoadout           — PASS (25)
  GameServer.Infrastructure.Tests Relic                  — PASS (20)
  GameServer.Domain.Tests         EquippedRelics         — PASS (10)

dotnet test src/backend/GameServer.sln   (full backend suite)
  GameServer.Application.Tests                           — PASS (126)
  GameServer.Domain.Tests                                — PASS (854)
  GameServer.Infrastructure.Tests                        — PASS (64)
  GameServer.Api.Tests                                   — PASS (85)
  TOTAL                                                  — PASS (1129), 0 failed

Baseline before this task: 1074 passing. Added 55; no test regressed.
```

**Real PostgreSQL verification** (stronger than the InMemory suite): the API
host was switched to the configured `localhost:5433/dcacti_db`, and
`dotnet ef database update` applied `AddRelicPersistence` together with the
previously-pending `AddPetPersistence`. The live schema was then queried
directly:

```text
tables                 Relic, RelicDefinition (plus the pre-existing
                       Player, Pet, PetDefinition)
Relic columns          RelicInstanceId varchar(64) NOT NULL
                       PlayerId varchar(64) NOT NULL
                       RelicDefinitionId varchar(64) NOT NULL
                       AcquiredAt timestamptz NOT NULL
RelicDefinition cols   RelicDefinitionId varchar(64) NOT NULL
                       Name varchar(64) NOT NULL
                       Trigger varchar(64) NOT NULL
                       Condition varchar(128) NULL
                       EffectDefinition varchar(128) NOT NULL
indexes on Relic       PK_Relic, IX_Relic_PlayerId   (exactly 2 — no implicit
                       FK index, confirming the TASK-024 convention removal)
FKs on Relic           FK_Relic_Player_PlayerId, FK_Relic_RelicDefinition_RelicDefinitionId
equip/loadout tables   NONE
```

**Mutation check (test teeth).** The ordering assertions were verified to be
non-vacuous: introducing an `OrderBy` into the snapshot rebuild made **4**
tests fail, including order preservation. The mutant was reverted and the
build output purged (`bin`/`obj` of the Application project were deleted and
rebuilt), because a stale mutant binary initially produced 3 spurious failures
against the real repository — confirming the tests observe the built artifact,
not just the source.

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic — ownership is read
      from persistence and filtered by `PlayerId` in the query itself; the
      client supplies only the requested instance ids (`GAME_RULES.md` §18,
      ADR-001)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1 — Relics, trigger
      system, and 3–5 equipped Relics per battle are IN)
- [x] Confirmed no Relic trigger engine, no effect resolution, no stacking
      (`RELIC_RULES.md` §4–§5 untouched and unimplemented)
- [x] Confirmed no persistent equip table and no equip column
      (`DATABASE.md` §2, ADR-012 item 7)
- [x] Confirmed no write-back to ownership rows and no live inventory read
      after the snapshot (ADR-012 item 8)
- [x] Confirmed `POST /api/battle/start` remains unimplemented (TASK-030 owns it)
- [x] Confirmed no Redis key or schema added (TASK-029 owns serialization)
- [x] Confirmed `src/frontend/client/` untouched
- [x] Confirmed no ADR, MVP-scope, or `docs/` change was required
- [x] Confirmed `tasks/completed/*` untouched

### Acceptance Criteria

- [x] RelicDefinition and owned-Relic entities match `DATABASE.md` §1–§2,
      registered with an applying EF migration
- [x] Battle-start path validates the documented equip count (3–5) and
      ownership of each selected Relic for the requesting Player
- [x] Battle-start path rejects a repeated `RelicInstanceId` with
      `INVALID_LOADOUT`; two distinct instances of one `RelicDefinition` are
      accepted
- [x] `PetState.EquippedRelics[]` exists per `GAME_STATE.md` §2.3, holds one
      instance identity per element, and preserves submitted order so
      `EquippedRelics[i]` is slot `i + 1`
- [x] The snapshot is written once at battle start and does not change from
      live DB reads mid-battle
- [x] No persistent equip table is created; no ownership rows are written
      during battle
- [x] No Relic trigger/effect/stacking logic is implemented or changed
- [x] All relevant tests pass at the required validation depth
- [x] Quality review checklist passes (docs-only items N/A; no `docs/` change)
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)
