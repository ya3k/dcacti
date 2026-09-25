# TASK-028 — Card Ownership & Loadout Snapshot

---

## Metadata

```text
Task ID:           TASK-028
Type:              FEATURE
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     persistence
Supporting Agents: gameplay, backend, testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, backend/persistence-analysis, gameplay/gameplay-behavior-derivation, testing/test-scenario-generation, quality/documentation-consistency
Dependencies:      TASK-023 (DONE), TASK-024 (DONE), TASK-025 (DONE),
                   TASK-039 (DONE — prerequisite contract; resolved the
                   Card loadout duplicate/copy-limit and validation
                   contract)
```

---

## Prerequisite Contract — Resolved by TASK-039 (DONE)

This task's former blocker — the Card loadout duplicate/copy semantics —
was resolved by **TASK-039 — Resolve Card Loadout Duplicate and Validation
Contract** (now DONE), through an explicit human gameplay decision.
Nothing below is open:

```text
A. Duplicate Basic Cards
   → RESOLVED: duplicates are permitted, bounded per CardDefinition by
     that definition's explicit `LoadoutCopyLimit`. `[A, A, B]` is valid
     iff `LoadoutCopyLimit(A) ≥ 2`. A limit of 1 makes a Basic Card
     loadout-unique.                       CARD_RULES.md §1

B. Missing LoadoutCopyLimit
   → RESOLVED: every CardDefinition must define its limit explicitly; a
     missing value is invalid definition data. There is NO default.
                                          CARD_RULES.md §1, DATABASE.md §1

C. Validation sequence and error mapping
   → RESOLVED: deterministic 4-step order — count → ownership →
     category → copy limit — all failures rejected with
     `INVALID_LOADOUT`.                    API_CONTRACTS.md §3

D. Snapshot representation
   → RESOLVED: `PetState.EquippedCards[]` holds exactly 4
     `CardDefinitionId` entries (3 submitted Basics + 1 derived Signature
     Skill); repeated ids are the same definition repeated, not
     instances; element order carries no gameplay significance.
                                          GAME_STATE.md §2.3
```

Concrete `LoadoutCopyLimit` values are content/balance configuration and
are **not** part of this task (CARD_RULES.md §1 item 5). This task
implements the mechanism, not the values.

---

## Objective

Persist CardDefinition and the `PlayerUnlockedCard` unlock-flag join per DATABASE.md §1–§2 (ADR-012 item 9), validate the submitted Basic loadout per CARD_RULES §1 and API_CONTRACTS §3 (exactly 3 entries: count, ownership, category, per-CardDefinition `LoadoutCopyLimit`; the Pet Skill Card is derived, not submitted), and snapshot the 4-card result into `PetState.EquippedCards[]` at battle start — with no `Pet.CardInventory` and no instance table.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — 3 Basic Cards + 5 Pet Skill Cards IN
- `docs/01-game-design/CARD_RULES.md` §1 — loadout composition; §3 — equip for active Pet
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` item 4 — battle-scoped Card loadout at POST /api/battle/start
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` items 9–10 — PlayerUnlockedCard join; PetState.EquippedCards snapshot; no Pet.CardInventory
- `docs/02-technical/DATABASE.md` §1 — CardDefinition; §2 — ownership join (ADR-012 resolves ASSUMPTION); §3 constraints
- `docs/02-technical/GAME_STATE.md` §2.3 — PetState.EquippedCards
- `docs/02-technical/API_CONTRACTS.md` §3 — POST /api/battle/start; the 4-step `cardLoadout` validation sequence and its `INVALID_LOADOUT` mapping; §6 error envelope convention

---

## Scope

### In Scope
- CardDefinition and `PlayerUnlockedCard(PlayerId, CardDefinitionId)` entities on `GameDbContext` with EF migration
- Completes TASK-024's deferred `SignatureSkillCardId` FK on PetDefinition now that CardDefinition exists
- Battle-start loadout validation per API_CONTRACTS §3 (exactly 3 submitted Basic Cards: count, ownership via `PlayerUnlockedCard`, category = Basic, per-CardDefinition `LoadoutCopyLimit` satisfied) plus the active Pet's derived Signature Skill Card per CARD_RULES §1
- Snapshot into `PetState.EquippedCards[]` at battle start
- Tests for unlock validation, loadout cardinality, snapshot immutability

### Out of Scope
- Card Tier/Star/Level instance tables (ADR-012 item 9 — MVP Cards have none)
- `Pet.CardInventory` (ADR-012 item 9 forbids it)
- Card cast/Power-spend resolution logic changes (existing CARD_RULES behavior; only ownership/snapshot plumbing)
- Persistent equip table (ADR-012 items 7–8 pattern)
- Relic loadout (TASK-027)
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

Implemented by TASK-028. `CardDefinition` and `PlayerUnlockedCard` exist in Domain with EF configurations, a `CardRepository` over `GameDbContext`, and the `AddCardPersistence` migration applied. `PetState.EquippedCards[]` exists as `EquippedCardIdentity[]` and `PetDefinition.SignatureSkillCardId` is mapped (completing TASK-024's deferral). `CardLoadoutService` implements the documented 4-step validation and Signature Skill derivation.

Not yet wired: `BattleStateService.PetConfiguration` does not yet carry a Card loadout, and the `POST /api/battle/start` endpoint itself does not exist — that composition belongs to TASK-030 (see §20 of the task brief: no second battle-start architecture is invented here).

---

## Acceptance Criteria

- [x] CardDefinition and PlayerUnlockedCard entities match DATABASE.md §1–§2, registered with an applying EF migration
- [x] `PetDefinition.SignatureSkillCardId` FK added and references CardDefinition (completes TASK-024 deferral)
- [x] Battle-start loadout validation implements the documented card loadout sequence (count, ownership, category, copy limit — API_CONTRACTS §3) plus the derived Signature Skill composition (the endpoint wiring itself is TASK-030)
- [x] `PetState.EquippedCards[]` exists per GAME_STATE §2.3 and carries the battle-scoped 4-entry snapshot; it is a value with no live DB read path
- [x] No `Pet.CardInventory` field and no Card instance/Tier/Star/Level table exists
- [x] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [x] Quality review checklist passes (`quality/review.md` §1)
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[x] tests/ (unit / integration / gameplay scenarios)
[ ] docs/ (documentation already specifies ownership/equip — ADR-011/012, CARD_RULES)
```

---

## Implementation Notes

- Same Application-service pattern as TASK-027: expose validation+snapshot for tests and wire into TASK-030's `POST /api/battle/start`.
- Extend `PetState` in `GameServer.Domain/Battle/BattleState.cs` with `EquippedCards` only as specified by GAME_STATE §2.3 — do not fold Relic members into this task.
- Invalid-loadout errors use the API_CONTRACTS §6 error envelope; the card validation sequence and its `INVALID_LOADOUT` mapping are defined in API_CONTRACTS §3. A `CardDefinition` with no explicit `LoadoutCopyLimit` is invalid definition data — do not substitute a default (CARD_RULES §1 item 2). Reuse TASK-027's `RelicLoadoutService` shape (Application service + `*LoadoutValidation` value + repository-owned ownership read) without altering the Relic contract.
- Do not edit `tasks/completed/`.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — unlock-flag check; Basic cardinality; category check; per-CardDefinition copy limit; Pet↔Signature card binding; snapshot immutability
[x] Integration tests  — unlocked Cards load from DB at battle start; SignatureSkillCardId FK migration applies
[x] Gameplay scenarios — Given a Player with the required unlocks, When battle starts, Then PetState.EquippedCards contains exactly the documented loadout for the active Pet (CARD_RULES §1)
```

### Key Edge Cases
- See `docs/01-game-design/CARD_RULES.md` §1 — loadout composition, ownership, copy limit; §3 — active-Pet binding
- See `docs/02-technical/API_CONTRACTS.md` §3 — cardLoadout validation sequence and `INVALID_LOADOUT` mapping; §6 — error envelope convention

---

## Stop Conditions

- If required behavior cannot be derived from Authoritative References: STOP per `AGENTS.md` §7
- If a Card instance/Tier/Star/Level table or `Pet.CardInventory` seems required: STOP — conflicts with ADR-012 item 9; report per `AGENTS.md` §4/§18
- If task requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose

---

## Completion Evidence

### Changed Files
- `src/backend/GameServer.Domain/Cards/CardCategory.cs` — new; `Basic | PetSkill` closed set (CARD_RULES §1, DATABASE §3)
- `src/backend/GameServer.Domain/Cards/CardDefinition.cs` — new; the six documented fields incl. required `LoadoutCopyLimit`
- `src/backend/GameServer.Domain/Cards/PlayerUnlockedCard.cs` — new; `(PlayerId, CardDefinitionId)` unlock flag
- `src/backend/GameServer.Domain/Cards/EquippedCardIdentity.cs` — new; the `CardDefinitionId` element `EquippedCards[]` carries
- `src/backend/GameServer.Domain/Pets/PetDefinition.cs` — added `SignatureSkillCardId` (completes TASK-024 deferral)
- `src/backend/GameServer.Domain/Battle/PetState.cs` — added `EquippedCards` member + `AtBattleCreation` parameter
- `src/backend/GameServer.Application/Cards/ICardRepository.cs` — new; content + unlock boundary, no equip write, no quantity
- `src/backend/GameServer.Application/Cards/CardLoadoutRejectionReason.cs` — new; 5 reasons, all mapping to `INVALID_LOADOUT`
- `src/backend/GameServer.Application/Cards/CardLoadoutValidation.cs` — new; snapshot-or-nothing result value
- `src/backend/GameServer.Application/Cards/CardLoadoutService.cs` — new; the 4-step validation + Signature Skill derivation
- `src/backend/GameServer.Application/DependencyInjection.cs` — registered `CardLoadoutService`
- `src/backend/GameServer.Infrastructure/Postgres/Configurations/CardDefinitionConfiguration.cs` — new
- `src/backend/GameServer.Infrastructure/Postgres/Configurations/PlayerUnlockedCardConfiguration.cs` — new; composite PK
- `src/backend/GameServer.Infrastructure/Postgres/Configurations/PetDefinitionConfiguration.cs` — mapped `SignatureSkillCardId` FK
- `src/backend/GameServer.Infrastructure/Postgres/GameDbContext.cs` — added `CardDefinitions`, `PlayerUnlockedCards`
- `src/backend/GameServer.Infrastructure/Postgres/Repositories/CardRepository.cs` — new
- `src/backend/GameServer.Infrastructure/DependencyInjection.cs` — registered `ICardRepository`
- `src/backend/GameServer.Infrastructure/Postgres/Migrations/20260925150903_AddCardPersistence.cs` (+ `.Designer.cs`, snapshot) — new migration
- `tests/backend/GameServer.Application.Tests/CardLoadoutServiceTests.cs` — new; 26 tests
- `tests/backend/GameServer.Infrastructure.Tests/CardPersistenceTests.cs` — new; 14 tests
- `tests/backend/GameServer.Domain.Tests/PetStateEquippedCardsTests.cs` — new; 8 tests
- `tests/backend/GameServer.Application.Tests/ApplicationRegistrationTests.cs` — +1 registration test
- `tests/backend/GameServer.Infrastructure.Tests/InfrastructureRegistrationTests.cs` — +1 registration test
- `tests/backend/GameServer.Infrastructure.Tests/PetPersistenceTests.cs` — `PetDefinition` field set now includes `SignatureSkillCardId` (deferral completed)
- `tests/backend/GameServer.Infrastructure.Tests/PetLevelRecomputeTests.cs` — fixture supplies the new required FK
- `tests/backend/GameServer.Domain.Tests/BattleStateTests.cs` — `EquippedCards` removed from the later-stage guard and added to the documented field set
- `tests/backend/GameServer.Infrastructure.Tests/RelicPersistenceTests.cs` — table list includes the two new Card tables

### Validation Results
- `dotnet test src/backend/GameServer.sln` — PASS (1,183 tests: Domain 862, Application 154, Api 85, Infrastructure 82)
- `npm run test:run` (src/frontend/client) — PASS (174 tests, 12 files)
- `dotnet ef database update` — PASS; `20260925150903_AddCardPersistence` applied
- Applied schema verified against DATABASE.md §1–§4 (7 tables; `LoadoutCopyLimit` and `SignatureSkillCardId` both `NOT NULL` with no default; composite PK `(PlayerId, CardDefinitionId)`; only `IX_PlayerUnlockedCard_PlayerId`; no implicit FK indexes)

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] No CardInstance / CardQuantity / CardInventory / PlayerCardCopies / persistent equip table
- [x] No Card gameplay, casting, effects, or Power spend
- [x] No concrete `LoadoutCopyLimit` or other balance values invented
- [x] No new SignalR event, no Redis change, no frontend change
- [x] TASK-027 Relic contract unchanged
