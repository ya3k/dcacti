# TASK-071 — Implement Collection Read Endpoints

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section; copies only what an implementer
  needs to know where to look.

  The four collection response contracts are FINAL (API_CONTRACTS.md §5.1–§5.6,
  resolved by TASK-070; canonical element wire values fixed by TASK-072).
  Nothing here changes a contract, a rule, or a document — this task builds
  the missing implementation behind contracts that already exist.

  Read-only collection infrastructure. NOT gameplay. No battle-start, no
  SignalR, no Redis, no docs changes, no migrations.
-->

---

## Metadata

```text
Task ID:           TASK-071
Type:              FEATURE (the §5 contracts exist; the endpoints do not —
                   tasks/TASK_TYPES.md §2 FEATURE)
Status:            DONE
Risk:              MEDIUM (backend feature baseline; the concrete risks are
                   wire-shape drift from §5 and ownership-disclosure bugs —
                   both are covered by binary acceptance criteria and the
                   required test matrix)
Priority:          HIGH (the client battle-start/loadout flow cannot be built
                   until these reads exist — TASK-069 deferred battle-start
                   orchestration explicitly because §5 was unimplemented;
                   MVP_SCOPE.md §1 lists Player account as "collection owner"
                   with Pets/Cards/Relics IN)
Primary Agent:     backend
Supporting Agents: persistence (implements the Postgres repository addition —
                   backend.md bars the Backend agent from editing
                   GameServer.Infrastructure/), testing (§5 conformance +
                   ownership/authority coverage), review (scope and
                   server-authority verification)
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery,
                   backend/api-contract-validation,
                   backend/persistence-analysis,
                   quality/architecture-conformance,
                   gameplay/authority-determinism-audit,
                   testing/test-scenario-generation
Dependencies:      TASK-069 — DONE, TASK-070 — DONE (§5 contracts resolved),
                   TASK-072 — DONE (canonical element wire values)
```

---

## Objective

Implement exactly the four documented collection read endpoints —
`GET /api/pets`, `GET /api/pets/{petId}`, `GET /api/cards`, `GET /api/relics` —
returning bare, player-scoped arrays/objects that match
`API_CONTRACTS.md` §5.1–§5.6 byte-for-byte in membership, deriving the
authenticated Player exclusively from the server-side session (ADR-015), and
projecting domain element values through the TASK-072 wire mapping
(`Moc→Wood`, `Tho→Earth`, `Thuy→Water`, `Hoa→Fire`, `Kim→Metal`), so the
future client battle-start/loadout flow has authoritative collection data to
read.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Player account (Discord identity,
  collection owner), Pets, Cards, Relics are IN scope; §2/§4 exclusions
- `docs/01-game-design/PET_RULES.md` §1 (Pet data model), §2 (ownership &
  selection), §3 (Tier — five tiers), §4 (Star), §5 (Level), §5.7 (non-XP
  attributes unchanged)
- `docs/01-game-design/CARD_RULES.md` §1 (card categories — `Basic`,
  `PetSkill`), §2 (Basic Cards)
- `docs/01-game-design/RELIC_RULES.md` §1 (Relic structure), §2 (equip rules —
  this task exposes NO equip/loadout state)
- `docs/01-game-design/ELEMENT_RULES.md` §1 (element domain vocabulary)
- `docs/02-technical/API_CONTRACTS.md` §1 (Bearer JWT, ADR-015) and §2.8
  (401 UNAUTHENTICATED), §5.1 (pet list wire schema), §5.2 (pet detail +
  404 PET_NOT_FOUND, non-disclosing), §5.3 (card list), §5.4 (relic list),
  §5.5 (empty → 200 [], no pagination/ordering/filtering), §5.6 (no equip
  fields), §6 (error envelope)
- `docs/02-technical/DATABASE.md` §1 (entity tables), §2 (relations),
  §3 (constraints), §4 (PlayerId ownership indexes — lines 749–751, already
  implemented; no migration needed)
- `docs/02-technical/ARCHITECTURE.md` §2.1 (backend layers, thin-controller
  rule, dependency direction), §5 (anti-overengineering)
- `docs/02-technical/TDD.md` §2.2 (ASP.NET Core backend), §4 (persistence
  strategy)
- `docs/03-decisions/ADR/ADR-001` (server authority), `ADR-011` (ownership
  is server-side), `ADR-012` (card unlock ownership), `ADR-015`
  (authentication/session identity)
- `tasks/completed/TASK-070-resolve-collection-list-endpoint-response-contracts.md`
  — decisions D1–D7 (immutable historical decision record)
- `tasks/completed/TASK-072-define-canonical-element-wire-values.md` —
  canonical element wire values + display-only semantics (immutable)

---

## Scope

### In Scope

- `GET /api/pets` → `API_CONTRACTS.md` §5.1: raw array; each item exactly
  `petId`, `identity`, `element`, `tier`, `star`, `level`. Ownership query by
  authenticated Player.
- `GET /api/pets/{petId}` → §5.2: same bare item shape; owned → 200; missing
  → 404 `{"error":"PET_NOT_FOUND"}`; foreign → the identical 404 (missing and
  foreign indistinguishable; never 403).
- `GET /api/cards` → §5.3: raw array of exactly `cardId`, `name`, `category`;
  membership from the authenticated Player's unlocked-card rows
  (`PlayerUnlockedCard` + `CardDefinition`; no `Card` entity exists or may be
  created).
- `GET /api/relics` → §5.4: raw array of exactly `relicId`, `name`
  (`Relic.RelicInstanceId` + `RelicDefinition.Name`); ownership by
  authenticated Player.
- §5.5/§5.6 semantics for all four: empty → HTTP 200 `[]`, no pagination, no
  ordering guarantee, no search/filter, no ownership query parameter, no
  envelope wrapper, no equip/loadout fields.
- Explicit element API projection (TASK-072 mapping) at the API/application
  projection boundary; `Element.ToString()` must never reach the collection
  API. (Contract implementation, not a new rule.)
- Smallest repository addition: `IPetRepository.ListByPlayerIdAsync`
  (currently absent) implemented over the existing Pet PlayerId index by the
  persistence agent. Existing `ICardRepository.ListUnlockedAsync` and
  `IRelicRepository.ListByPlayerIdAsync`/`GetDefinitionAsync` are reused as-is.
- Explicit response DTO/projection records per endpoint (matches existing
  API response conventions); controllers stay thin.
- Backend tests covering the full matrix in Testing Requirements, plus full
  backend-suite regression.

### Out of Scope

- Everything listed in §16 of the task request, including: `POST /api/battle/start`,
  JoinBattle, GetBattleState, SignalR, Redis, BattleState, BattleScene,
  GameRuntime, loadout UI, battle-start orchestration, Boss collection
  endpoint, all Match-3/board/swap/cascade/combat/damage logic, all
  pet/card/relic GAMEPLAY, passive calculation, rewards, XP, progression,
  authentication redesign, database redesign, new migrations, new tables, new
  ADR, and any `docs/` edit of any kind (the contracts are final; if a doc
  change appears necessary → STOP).
- **Battle-start/Redis element serialization ("Hoa") — MUST NOT be fixed
  here** (see Known Follow-up).
- **TASK-073 must not be created** in this task's session.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State

No collection controller, query service, or DTO exists —
`GET /api/pets`, `/api/pets/{petId}`, `/api/cards`, `/api/relics` all 404
today. Auth infrastructure is complete and reusable
(`AuthenticatedPlayerMiddleware` writes the PlayerId into the request context;
`[Authorize]` + `PlayerIdClaimRequirement` are the established pattern —
precedent: authenticated actions in
`src/backend/GameServer.Api/Controllers/BattleController.cs`). All persistence
entities and PlayerId indexes already exist (no migration), repositories are
DI-registered (`DependencyInjection.cs`), except `IPetRepository` lacks a
by-player list method. Wire contracts are final per TASK-070/TASK-072.

---

## Acceptance Criteria

### Pets list

- [x] `GET /api/pets` requires authentication (unauthenticated → 401 per §2.8).
- [x] Results are scoped to the authenticated Player (server-derived; no
      client-supplied identity).
- [x] Response is HTTP 200 with a raw JSON array (no envelope/wrapper).
- [x] Empty collection returns `[]` (200, not 204/404).
- [x] Each item contains exactly `petId`, `identity`, `element`, `tier`,
      `star`, `level` — no more, no fewer.
- [x] `petId` is `Pet.PetInstanceId`.
- [x] `identity` comes from the PetDefinition.
- [x] `element` is exactly one of `Fire | Water | Earth | Wood | Metal`.
- [x] `element` is produced by the explicit mapping
      (`Moc→Wood, Tho→Earth, Thuy→Water, Hoa→Fire, Kim→Metal`) — never
      `Element.ToString()`.
- [x] `tier` uses the documented string wire values (not a number).
- [x] `xp`, `acquiredAt`, `playerId`, `petDefinitionId` are absent from every
      item.

### Pet detail

- [x] `GET /api/pets/{petId}` requires authentication.
- [x] Owned Pet returns HTTP 200 with the same object shape as one list item.
- [x] Missing Pet returns HTTP 404 with body `{"error":"PET_NOT_FOUND"}`.
- [x] Foreign Pet (owned by another Player) returns the identical 404 status
      and body.
- [x] Foreign ownership is not disclosed (no differing status, body, header,
      or timing-visible branch that distinguishes missing from foreign).

### Cards

- [x] `GET /api/cards` requires authentication.
- [x] Results are scoped to the authenticated Player's unlock membership
      (`PlayerUnlockedCard` × `CardDefinition`).
- [x] Response is a raw array; empty collection returns `[]`.
- [x] Each item contains exactly `cardId`, `name`, `category`.
- [x] `cardId` is `CardDefinition.CardDefinitionId`.
- [x] `category` uses the documented wire values `Basic | PetSkill`.
- [x] No `unlocked` member exists in the response.
- [x] No `playerId`, `powerCost`, `loadoutCopyLimit`, or `effectDefinition`
      is exposed.

### Relics

- [x] `GET /api/relics` requires authentication.
- [x] Results are scoped to the authenticated Player.
- [x] Response is a raw array; empty collection returns `[]`.
- [x] Each item contains exactly `relicId`, `name`.
- [x] `relicId` is `Relic.RelicInstanceId`.
- [x] `name` comes from `RelicDefinition.Name`.
- [x] No `playerId`, `acquiredAt`, `definitionId`, `Trigger`, `Condition`, or
      `Effect`/`EffectDefinition` is exposed.
- [x] No equipped/loadout state is exposed (§5.6).

### General

- [x] No pagination/order/filter/search behavior is invented (no `page`,
      `limit`, `cursor`, `sort`, `search`, `filter`, `total`, `items`,
      `data` parameters or fields).
- [x] No new database table, no migration, no ownership-model change.
- [x] No new authentication mechanism, claim, token behavior, or `playerId`
      parameter.
- [x] No gameplay logic, no SignalR/Redis/battle-start change.
- [x] Server remains authoritative for ownership and collection state.
- [x] Controllers remain thin; no speculative abstractions
      (`CollectionManager`, `UniversalCollectionService`,
      `GenericEntityQueryService`, `InventoryManager`, etc.) are created.
- [x] All relevant tests pass at the required validation depth
      (`core/validation.md` §2).
- [x] Quality review checklist passes (`quality/review.md` §1).
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[x] src/backend/ (Api controllers + Application query/DTO projection; Infrastructure repository addition via persistence agent)
[ ] src/frontend/client/ (no change — no client work in this task)
[x] tests/ (backend unit + API integration + regression)
[ ] docs/ (no change — §5 contracts are final; doc edit required → STOP)
```

---

## Implementation Notes

- **Auth reuse, nothing new**: `[Authorize]` + `PlayerIdClaimRequirement`;
  PlayerId comes from the context item written by
  `AuthenticatedPlayerMiddleware` (`Program.cs` registration,
  `Authentication/AuthenticatedPlayer.cs`). Precedent for reading it in an
  action: `BattleController.cs` (authenticated PlayerId pattern). Zero
  `playerId` query/body/route parameters; zero new claims.
- **Layering**: thin controller → Application query service (precedent:
  `BattleResultQueryService`) → repository interfaces → Infrastructure →
  PostgreSQL (`ARCHITECTURE.md` §2.1). Backend agent must NOT edit
  `GameServer.Infrastructure/` — hand the `IPetRepository.ListByPlayerIdAsync`
  implementation to the persistence supporting agent.
- **Element mapping**: a small explicit projection (switch expression/mapper)
  living at the API/application projection boundary. Test all five pairs,
  especially `Moc→Wood` and `Kim→Metal` (the trap cases for naive
  translation). Never emit `Element.ToString()`.
- **DTOs**: dedicated response records per endpoint so forbidden fields
  (xp/playerId/definitionId/Trigger/…) cannot serialize by accident; never
  expose EF or domain entities directly.
- **Indistinguishable 404**: one not-found path returning the §6
  `PET_NOT_FOUND` envelope for both missing and foreign; ownership filter in
  the query (or a single combined check) — never a distinct 403/alternate
  error for foreign Pets.
- **Tier/category**: `PetTier` and `CardCategory` member names are the wire
  values (`Common|Rare|Epic|Legendary|Mythic`, `Basic|PetSkill`) — verify
  against §5 examples + `PET_RULES.md` §3 / `CARD_RULES.md` §1, serialize as
  strings.
- **Reuse, don't add**: `ICardRepository.ListUnlockedAsync` and
  `IRelicRepository.ListByPlayerIdAsync` already exist; only
  `IPetRepository` needs the new list method (plus, if needed, a single-Pet
  by-id query including ownership).
- Registration of any new query service follows the existing
  `DependencyInjection.cs` conventions.

---

## Known Follow-up

Recorded here, deliberately NOT part of this task:

1. **Battle-start/Redis element wire mismatch ("Hoa")** —
   `BattleStartStateSummary.cs` serializes the element via `Element.ToString()`
   → `"Hoa"`, and frontend fixture `GameRuntime.test.ts` expects `'Hoa'`.
   TASK-072 deliberately did not fix it; §3 does not bind battle-start element
   values, so no doc conflict exists. Converging battle-start/Redis element
   serialization to the TASK-072 wire vocabulary is a **separate future task**
   (BUG/DOCUMENTATION, new task ID — do NOT create it from this task).
2. **Stale comment in `Domain/Elements/Element.cs`** claiming no API Element
   serialization contract exists — outdated since TASK-072, but it lives in
   `GameServer.Domain/`, which the backend agent must not edit (ownership:
   Gameplay agent), and a comment change is not part of implementing §5.
   Excluded from this task; may be folded into follow-up #1.

---

## Testing Requirements

### Required Verification

```text
[ ] Unit tests         — element projection (all five mappings), tier/category
                         string serialization, DTO projection membership,
                         forbidden-field absence, IPetRepository.ListByPlayerIdAsync
[ ] Integration tests  — REST layer against the §5 contracts (in the style of
                         the existing GameServer.Api.Tests REST harness):
                         401 unauthenticated on all four endpoints; 200 raw
                         arrays; empty → []; pet detail 200 / 404 missing /
                         404 foreign with identical bodies; card unlock
                         membership scoping; relic definition-name projection
[ ] Gameplay scenarios — N/A: read-only collection infrastructure; no
                         gameplay rule is exercised (documented in report)
```

### Key Edge Cases

- Empty collection on each endpoint → `200 []`.
- Foreign `petId` → same `404 {"error":"PET_NOT_FOUND"}` as a nonexistent id
  (no ownership disclosure).
- Pet detail shape is member-identical to one list item.
- A Player with an empty unlock set → `GET /api/cards` = `[]`.
- Element mapping correct for all five domain values (Moc/Tho/Thuy/Hoa/Kim).

### Regression

- [x] Full existing backend suite passes unchanged
      (`dotnet test` at solution level) — no existing test may be weakened.
- [x] Frontend suite untouched (not required by this task).

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 always
apply. Task-specific:

- If `API_CONTRACTS.md` §5 conflicts with another authoritative document →
  STOP per `AGENTS.md` §4; do not pick the easier reading.
- If an endpoint requires a field not defined by §5 → STOP; do not add it.
- If ownership cannot be derived from the authenticated Player → STOP.
- If a new persistence model, table, column, or migration appears necessary →
  STOP.
- If the element mapping cannot be implemented deterministically → STOP.
- If tier/category wire values conflict with `PET_RULES.md` §3 /
  `CARD_RULES.md` §1 → STOP.
- If a new ADR appears required → STOP (`AGENTS.md` §18).
- If implementation would change TASK-070 or TASK-072 (both immutable) →
  STOP.
- If implementation would touch battle-start or Redis element serialization →
  STOP (Known Follow-up #1, separate task).
- If gameplay logic becomes necessary → STOP (`AGENTS.md` §7).
- If undocumented pagination/order/filter semantics appear necessary → STOP.
- If required behavior cannot be derived from Authoritative References →
  STOP per `AGENTS.md` §7.
- If task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries → STOP & decompose.
- Do not guess. Do not create TASK-073.

---

## Completion Evidence

### Changed Files

**Created — Api**
- `src/backend/GameServer.Api/Controllers/CollectionController.cs` — the four §5 read endpoints; thin boundary, session-derived identity only, `404 PET_NOT_FOUND` shared by the missing and foreign case.
- `src/backend/GameServer.Api/Controllers/CollectionResponses.cs` — `PetResponse`, `CardResponse`, `RelicResponse`: one explicit wire record per endpoint with the §5 member set fixed by `[JsonPropertyName]`.

**Created — Application**
- `src/backend/GameServer.Application/Collection/CollectionQueryService.cs` — the four reads; Player-scoped, no sorting/paging, no gameplay value computed.
- `src/backend/GameServer.Application/Collection/ElementWireValues.cs` — the canonical API element projection (TASK-072's five pairs); `Element.ToString()` never reaches the wire.
- `src/backend/GameServer.Application/Collection/PetCollectionItem.cs`, `CardCollectionItem.cs`, `RelicCollectionItem.cs` — the §5.1/§5.3/§5.4 read models, each with exactly its documented members.

**Modified — Application**
- `src/backend/GameServer.Application/Pets/IPetRepository.cs` — added `ListByPlayerIdAsync` and the bulk `ListDefinitionsAsync`; doc comment updated (the boundary's "no bulk-listing" note was narrowed to "no bulk-save").
- `src/backend/GameServer.Application/Relics/IRelicRepository.cs` — added the bulk `ListDefinitionsAsync`.
- `src/backend/GameServer.Application/DependencyInjection.cs` — registered `CollectionQueryService` (scoped).

**Modified — Infrastructure** (performed by the persistence role — `backend.md` bars the backend agent from this project)
- `src/backend/GameServer.Infrastructure/Postgres/Repositories/PetRepository.cs` — implemented both new reads over the existing `Pet(PlayerId)` index and `PetDefinition` table.
- `src/backend/GameServer.Infrastructure/Postgres/Repositories/RelicRepository.cs` — implemented the bulk definition read over the existing `RelicDefinition` table.

**Created — tests**
- `tests/backend/GameServer.Api.Tests/CollectionEndpointTests.cs` — 46 REST contract tests (§5.1–§5.6, §2.8, §6).
- `tests/backend/GameServer.Application.Tests/CollectionQueryServiceTests.cs` — the four reads' membership, ownership scoping, and §5.5/§5.6 semantics.
- `tests/backend/GameServer.Application.Tests/ElementWireValuesTests.cs` — all five wire pairs, plus the "never the enum name" and out-of-set guards.
- `tests/backend/GameServer.Infrastructure.Tests/CollectionRepositoryTests.cs` — the new repository operations over the real EF model, and the unchanged index/table inventory.

**Modified — tests** (interface conformance only: 70 insertions, 0 deletions — no assertion weakened)
- `tests/backend/GameServer.Application.Tests/BattleResultTestDoubles.cs` — `InMemoryPetRepository` implements the two new members.
- `tests/backend/GameServer.Application.Tests/BattleStartServiceTests.cs` — the two fakes state the new members as defects on their paths.
- `tests/backend/GameServer.Application.Tests/RelicLoadoutServiceTests.cs` — same.

**Deliberately NOT changed:** every `docs/` file, `src/frontend/`, any migration, `GameServer.Domain/`, `BattleStartStateSummary.cs`, `GameRuntime.test.ts`, TASK-069, TASK-070, TASK-072.

### Validation Results

```text
Baseline (before):  Domain 951 | Application 294 | Infrastructure 202 | Api 187 = 1634 passing, 0 failed
Final (after):      Domain 951 | Application 350 | Infrastructure 217 | Api 233 = 1751 passing, 0 failed
Delta:              +117 new tests; 0 existing tests weakened or removed
```

- `dotnet test src/backend/GameServer.sln` — PASS (1751 tests, 0 failed)
- `dotnet build src/backend/GameServer.sln` — Build succeeded, 0 errors
- `tests/backend/GameServer.Api.Tests` (REST/integration) — PASS (233 tests, 46 of them this task's)
- Frontend suite — not run: this task changed no frontend file (`git status` confirms `src/frontend/` is untouched).

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic — these are read-only queries; no value is computed, no
      outcome is decided, and the client supplies no identity and no state.
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — Pets/Cards/Relics collections are listed IN; the
      Player account is the documented "collection owner".
- [x] No gameplay, no new database tables, no migration, no authentication redesign, no SignalR changes,
      no Redis changes, no battle-start changes, no client-authoritative state.

### Notes on decisions taken inside the contract

1. **§5.2's `404` body includes `message`.** §5.2's own example is
   `{ "error": "PET_NOT_FOUND", "message": "human-readable detail" }`, and §6 defines the same two-member
   envelope. The implementation emits both, per §5.2/§6. (The task prompt's shorter
   `{ "error": "PET_NOT_FOUND" }` is a partial rendering of the same §5.2 example; `docs/` governs per
   `AGENTS.md` §2.) §4's `BATTLE_NOT_FOUND`, by contrast, is shown without `message` in its own section, so
   `BattleController` legitimately differs — each section's own example is followed, and neither is changed here.
2. **Pet definitions are resolved in one bulk read.** `ListDefinitionsAsync` / the equivalent Relic read were
   added so a collection projection issues two queries instead of N+1. They are content reads over the existing
   definition tables — no new index, no cache, no read model (`DATABASE.md` §4 is unchanged and asserted).
3. **A Pet or Relic whose definition row is missing is refused, not defaulted.** §5.1/§5.4 define `identity`,
   `element`, and `name` as the definition's values, so a placeholder would be invented content (`AGENTS.md` §7).
   Under `DATABASE.md` §2's foreign keys the state is unreachable; the guard makes that explicit rather than silent.

### Known Follow-up (recorded, NOT implemented here)

- **Battle-start/Redis element serialization (`"Hoa"`).** `BattleStateSerializer` spells an Element with
  `Element.ToString()` for the runtime/Redis record, so battle-start payloads can carry the Domain enum's
  Vietnamese-derived member name while the §5 collection endpoints now carry `"Fire" | "Water" | "Earth" |
  "Wood" | "Metal"`. TASK-072 deliberately left that path alone and §3 binds no battle-start element value, so
  **no document conflict exists** — the two are different contracts. Converging the battle-start/Redis path on
  the TASK-072 vocabulary is a **separate future task**; it was not created in this session.
- **Stale comment in `Domain/Elements/Element.cs`** claiming no API Element serialization contract exists —
  outdated since TASK-072. It lives in `GameServer.Domain/` (Gameplay ownership) and is excluded here; it may be
  folded into the follow-up above.

### Required Completion Report Fields

```text
Status: DONE
Files Changed: 5 modified + 8 created (production 6, tests 4 files + 3 conformance edits) — listed above
Endpoint Summary: GET /api/pets, GET /api/pets/{petId}, GET /api/cards, GET /api/relics
Authentication / Ownership: [Authorize] + PlayerIdClaimRequirement + AuthenticatedPlayerMiddleware →
  HttpContext.Items["GameServer.PlayerId"]; no playerId parameter anywhere
Element Mapping: Moc→Wood, Tho→Earth, Thuy→Water, Hoa→Fire, Kim→Metal (explicit projection)
Tests: 1751 backend passing, 0 failed (+117 from the 1634 baseline)
Build: succeeded, 0 errors
Migration: none
Regression: none — 0 pre-existing tests changed in substance
Scope Verification: all eight confirmations below hold
Known Follow-up: battle-start/Redis "Hoa" convergence (separate task, not created)
Documentation Changes: none
Next Step: client battle-start/loadout flow (TASK-069's deferred orchestration) may now read these collections
```

```text
No gameplay.
No new database tables.
No migration.
No authentication redesign.
No SignalR changes.
No Redis changes.
No battle-start changes.
No client-authoritative state.
```

