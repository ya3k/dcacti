# TASK-141 — Implement Application RelicDefinition Lookup Boundary

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and tasks/ by path and section.

  THIS TASK IS AN APPLICATION / INFRASTRUCTURE ARCHITECTURAL BOUNDARY TASK.
  It establishes the Application-level IRelicDefinitionLookup boundary and its
  backing implementation for retrieving shared/static RelicDefinition content by
  canonical definition identity.

  THIS IS NOT A RELIC RESOLUTION TASK.
  It does NOT implement Relic trigger evaluation, condition matching, effect execution,
  RelicTriggered event emission, or combat modifier application.

  PROVENANCE: Identified by TASK-133, TASK-138, TASK-139, and TASK-140 as the
  remaining independent prerequisite blocker for server-authoritative Relic resolution.
  TASK-140 implemented the PetState.ATKModifiers[] and PetState.CardCostModifiers[]
  runtime carriers. TASK-133 is now blocked solely by the absence of a clean
  Application-level definition lookup boundary for static Relic content.
-->

---

## Metadata

```text
Task ID:           TASK-141
Type:              FEATURE (TASK_TYPES.md §2 — "Implement a documented mechanic,
                   capability, or system that already has a home in docs/ but has
                   not yet been built"; workflow development/feature.md)
Status:            DONE (direct execution recorded: Changed Files + Validation
                   Results in Completion Evidence; all twenty-one acceptance
                   criteria independently verified — see lifecycle note below.)
                   Lifecycle reconciliation: transitioned BACKLOG → DONE per
                   TASK_LIFECYCLE.md §3 (direct execution verified:
                   IRelicDefinitionLookup + ScopedRelicDefinitionLookup + DI
                   registration present, Application/Infrastructure suites PASS).
Risk:              MEDIUM (TASK_TYPES.md §4 — touches Application/Infrastructure boundary
                   and dependency registration; no database schema or gameplay rule changes)
Priority:          HIGH (Direct critical-path blocker for TASK-133 and GAME_RULES.md §17 step 11)
Primary Agent:     backend (TASK_TYPES.md §5 — Application/API domain → Backend)
Supporting Agents: persistence, testing, review
Workflow:          development/feature.md
Skills:            discovery/impact-analysis,
                   backend/persistence-analysis,
                   quality/architecture-conformance,
                   testing/test-scenario-generation,
                   quality/implementation-review
Dependencies:      TASK-131, TASK-132, TASK-134, TASK-136, TASK-140
```

---

## Objective

Establish the Application-level `IRelicDefinitionLookup` boundary and its Infrastructure implementation for retrieving shared/static `RelicDefinition` content by canonical Relic definition identity. This resolves the remaining independent architectural blocker for TASK-133 without implementing Relic trigger evaluation, condition evaluation, or effect execution.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Confirm Relics ("~10 Relics", trigger system) are in MVP scope
- `docs/01-game-design/RELIC_RULES.md` §1 (Relic structure), §2.2 (distinction between owned instance and definition identity), §6 (MVP Relic reference list), §8 (Trigger/Condition/Effect contract: §8.1 structured Condition, §8.2 structured EffectDefinition[], §8.3 allowed combinations, §8.5 provisioned row values, §8.6 storage migration landed, §8.7 implementation status)
- `docs/02-technical/DATABASE.md` §1 (`RelicDefinition` table, `RelicDefinitionId` PK, structured `jsonb` columns), §2 (Relic N ── 1 RelicDefinition: Player ownership instances vs shared static content), §4 (indexing)
- `docs/02-technical/ARCHITECTURE.md` §2.1 (Domain / Application / Infrastructure layering and dependency inversion), §3 ("PersistenceRepository (Postgres) — Infrastructure"), §5 (anti-overengineering — no speculative managers or caches)
- `docs/02-technical/TDD.md` §4 (PostgreSQL kept off the hot path; active battle state separated from persistent definitions), §6 (deterministic retrieval)
- `docs/02-technical/GAME_STATE.md` §0 (separation of static definition content from mutable battle state), §2.3 (`PetState.EquippedRelics[]` carries instance identities, not definitions)
- `docs/02-technical/REDIS_STATE.md` §2 (active battle state boundary — no static content stored in Redis)
- `docs/02-technical/GAME_EVENTS.md` §1.1, §2 (`RelicTriggered` event definition)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §2 (client-server actions), §3.2.23 (`RelicTriggered` wire contract)
- `docs/03-decisions/ADR/ADR-001` — Server authority
- `docs/03-decisions/ADR/ADR-018` — Relic Trigger/Condition/Effect contract
- Existing codebase precedents:
  - `src/backend/GameServer.Application/Cards/ICardDefinitionLookup.cs` and `ScopedCardDefinitionLookup.cs` (Application definition lookup pattern for singleton consumers)
  - `src/backend/GameServer.Application/Relics/IRelicRepository.cs` and `src/backend/GameServer.Infrastructure/Postgres/Repositories/RelicRepository.cs` (existing PostgreSQL storage and definition query)
  - `src/backend/GameServer.Domain/Relics/RelicDefinition.cs` (canonical Domain model)

---

## Scope

### In Scope

1. **Application Lookup Interface:** Define `IRelicDefinitionLookup` in `GameServer.Application.Relics` following existing repository conventions (`ICardDefinitionLookup` precedent):
   ```csharp
   Task<RelicDefinition?> GetDefinitionAsync(
       string relicDefinitionId,
       CancellationToken cancellationToken = default);
   ```
2. **Infrastructure Implementation:** Implement the lookup boundary to query the existing PostgreSQL `RelicDefinition` table using EF Core and Domain model mapping, reusing the structured `Condition` and `EffectDefinition[]` columns established by TASK-132.
3. **Singleton/Scoped Adaptation:** Provide the DI registration/wiring (e.g. `ScopedRelicDefinitionLookup` via `IServiceScopeFactory`, matching `ScopedCardDefinitionLookup`) allowing singleton consumers such as `BattleStateService` to consume `IRelicDefinitionLookup` without capturing a scoped `DbContext`.
4. **Not-Found Convention:** Follow the repository's established convention: return `null` when a definition identity is not found. Do not invent fallback definitions, default Relics, or silent synthetic objects.
5. **Domain Representation:** Ensure the lookup returns the authoritative Domain `RelicDefinition` model, complete with structured `RelicCondition?` and `RelicEffectDefinitions`.
6. **Tests:** Unit tests verifying Application lookup contract and missing-definition behavior; integration tests verifying round-trip retrieval against PostgreSQL and provisioned Relic content.

### Out of Scope

- Relic trigger evaluation, condition matching, or effect execution (`GAME_RULES.md` §17 step 11 / TASK-133)
- `RelicTriggered` event emission or `SIGNALR_PROTOCOL.md` changes
- Berserker Core, Mana Crystal, Assassin Eye, Emergency Core, or Burning Curse runtime execution
- Applying ATK, CardCost, Crit, or Power modifications to `PetState`
- Adding Relic resolution hooks into `BattleStateService.ResolveSwapAsync`
- Any change to `PetState`, `BattleState`, or Redis serialization
- Any change to PostgreSQL schema or EF Core migrations (existing TASK-132 storage is consumed as-is)
- Any new caching abstraction (no Redis caching, no memory cache)
- Any modification to Player ownership methods or tables (`Relic` instance repository)
- Any change to authoritative documentation in `docs/`
- Modifying completed tasks or TASK-133/134/136/137/138/139/140

---

## Current State

```text
Implemented (do not duplicate or refactor):
  RelicDefinition Domain model                   GameServer.Domain.Relics.RelicDefinition
                                                 (carries structured Condition and EffectDefinition)
  Structured RelicDefinition storage             PostgreSQL RelicDefinition table with jsonb columns
                                                 (TASK-132 / migration 20261003074309)
  IRelicRepository / RelicRepository             Scoped PostgreSQL persistence boundary for Relic instances
                                                 and static content (includes GetDefinitionAsync)
  ICardDefinitionLookup / ScopedCardDefinitionLookup
                                                 The canonical precedent for Application definition lookup
                                                 consumed by singleton services

NOT implemented (this task's work):
  IRelicDefinitionLookup                         No standalone Application definition lookup abstraction
                                                 exists for Relics
  ScopedRelicDefinitionLookup                    No scope-factory adapter exists for singleton consumers
  DI Registration for IRelicDefinitionLookup     Not registered in DependencyInjection.cs
```

---

## Acceptance Criteria

- [ ] An Application-level RelicDefinition lookup abstraction exists (`IRelicDefinitionLookup` in `GameServer.Application.Relics`).
- [ ] The abstraction follows existing repository/Application naming conventions (`ICardDefinitionLookup` precedent).
- [ ] The lookup accepts the canonical Relic identity (`relicDefinitionId`).
- [ ] Existing structured RelicDefinition content can be retrieved through the abstraction.
- [ ] The returned value uses the existing Domain representation (`RelicDefinition`).
- [ ] Missing definitions follow the repository's established not-found/error convention (returns `null`).
- [ ] Infrastructure implements the Application lookup boundary (or adapts the existing repository implementation).
- [ ] Existing structured JSONB RelicDefinition storage is reused without modification.
- [ ] No new database schema or migration is introduced.
- [ ] No Redis cache or new Redis contract is introduced.
- [ ] No Player-scoped ownership lookup is incorrectly used as shared definition lookup.
- [ ] Dependency direction remains valid (Domain has no dependencies on Application or Infrastructure; Application does not depend on Infrastructure).
- [ ] Relic trigger/condition/effect resolution is not implemented.
- [ ] No gameplay rules are changed.
- [ ] No SignalR contract is changed.
- [ ] Tests cover existing and missing RelicDefinition lookup.
- [ ] Existing backend tests remain green.
- [ ] TASK-133's lookup-boundary blocker is resolved by this task.
- [ ] All relevant tests pass at the required validation depth (`core/validation.md` §2).
- [ ] Quality review checklist passes (`quality/review.md` §1).
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[x] src/backend/GameServer.Application/Relics/IRelicDefinitionLookup.cs (new Application interface)
[x] src/backend/GameServer.Application/Relics/ScopedRelicDefinitionLookup.cs (new singleton adapter)
[x] src/backend/GameServer.Application/DependencyInjection.cs (DI registration)
[x] src/backend/GameServer.Infrastructure/DependencyInjection.cs (if Infrastructure registration is needed)
[ ] src/backend/GameServer.Domain/ (no domain modifications required)
[ ] src/frontend/client/ (none)
[x] tests/backend/GameServer.Application.Tests/ (Application unit tests for lookup contract and adapter)
[x] tests/backend/GameServer.Infrastructure.Tests/ (Integration tests against PostgreSQL RelicDefinition storage)
[ ] docs/ (authoritative documentation remains unmodified)
[ ] tasks/completed/ (MUST remain unmodified)
```

---

## Implementation Notes

- **Mirror `ICardDefinitionLookup`.** The repository already established this exact pattern for `CardDefinition`:
  `ICardDefinitionLookup` lives in `GameServer.Application.Cards` and defines `Task<CardDefinition?> GetDefinitionAsync(string cardDefinitionId, CancellationToken cancellationToken = default);`.
  `ScopedCardDefinitionLookup` lives in `GameServer.Application.Cards`, implements `ICardDefinitionLookup`, takes `IServiceScopeFactory`, creates a scope per call, and delegates to the scoped repository.
  `DependencyInjection.cs` registers `services.AddSingleton<ICardDefinitionLookup>(provider => new ScopedCardDefinitionLookup(provider.GetRequiredService<IServiceScopeFactory>()));`.
  Follow this proven pattern directly for `IRelicDefinitionLookup`.
- **Do not conflate Relic instances with Relic definitions.** `PetState.EquippedRelics[]` contains `EquippedRelicIdentity` representing owned `RelicInstanceId` values. `RelicDefinitionId` represents the static content row (e.g. `"relic-berserker-core"`). This lookup is for `RelicDefinitionId` → `RelicDefinition`.
- **Preserve Clean Architecture.** `IRelicDefinitionLookup` belongs in `GameServer.Application.Relics`. Domain types (`RelicDefinition`) cross this boundary; EF Core entities or database DTOs do not.
- **Fail cleanly on missing definitions.** Return `null` when a definition row is absent, matching `ICardDefinitionLookup`, `IRelicRepository.GetDefinitionAsync`, and `IBossDefinitionLookup`. Do not throw custom exceptions or fabricate placeholder rows (`AGENTS.md` §7).
- **Anti-Overengineering.** Do not create `RelicDefinitionCache`, `RelicDefinitionManager`, or generic lookup abstractions (`ARCHITECTURE.md` §5, `AGENTS.md` §9).

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — IRelicDefinitionLookup contract with test double; ScopedRelicDefinitionLookup
                          scope handling and argument validation; null returned on missing definition
[x] Integration tests  — PostgreSQL retrieval of provisioned RelicDefinition rows
                          ("relic-berserker-core", "relic-mana-crystal", "relic-assassin-eye",
                          "relic-emergency-core"); assertion of structured Condition and
                          EffectDefinition[] deserialization; missing definition returns null
[ ] Gameplay scenarios — N/A (no gameplay resolution logic in this task)
```

### Key Scenarios

```text
Given a valid provisioned RelicDefinitionId ("relic-berserker-core")
When GetDefinitionAsync is called on IRelicDefinitionLookup
Then the returned RelicDefinition is not null
And RelicDefinitionId equals "relic-berserker-core"
And Condition is MatchCountAtLeast with threshold 3
And EffectDefinition contains one ATK Percentage effect of value 5 with target Pet and lifetime Battle

Given an unknown or non-existent RelicDefinitionId ("relic-non-existent")
When GetDefinitionAsync is called on IRelicDefinitionLookup
Then the returned result is null

Given an empty or whitespace RelicDefinitionId
When GetDefinitionAsync is called on IRelicDefinitionLookup
Then an ArgumentException is thrown
```

---

## Stop Conditions

- If the correct Application ownership boundary is ambiguous: STOP per `AGENTS.md` §20.
- If `RelicDefinition` Domain shape conflicts with TASK-131/TASK-132: STOP per `AGENTS.md` §4.
- If missing-definition behavior is undefined and existing project conventions do not resolve it: STOP per `AGENTS.md` §20.
- If the lookup requires a new gameplay rule: STOP per `AGENTS.md` §7.
- If the lookup requires a new database schema: STOP per `AGENTS.md` §18.
- If the lookup requires Redis caching not already documented: STOP per `AGENTS.md` §18.
- If the lookup requires changing the Relic state model or `PetState`: STOP per `AGENTS.md` §20.
- If the lookup requires changing SignalR: STOP per `AGENTS.md` §20.
- If the lookup requires a new ADR: STOP per `AGENTS.md` §18.
- If the lookup cannot be implemented without modifying TASK-133's gameplay scope: STOP per `AGENTS.md` §16.

---

## Downstream Handoff

```text
TASK-140 (DONE)
    ↓ implements PetState.ATKModifiers[] & PetState.CardCostModifiers[]
TASK-141 (this task)
    ↓ implements IRelicDefinitionLookup boundary
TASK-133
    ↓ can resume server-authoritative Relic trigger and effect resolution
```

Completing this task resolves the `IRelicDefinitionLookup` prerequisite blocker for TASK-133.

---

## Completion Evidence

### Changed Files

- `src/backend/GameServer.Application/Relics/IRelicDefinitionLookup.cs` — new. `Task<RelicDefinition?> GetDefinitionAsync(string relicDefinitionId, CancellationToken cancellationToken = default)`, mirroring `ICardDefinitionLookup`. It takes the canonical static content identity (`DATABASE.md` §1 `RelicDefinitionId`) and no `PlayerId`, Discord identity, equipped, or inventory context, and returns the Domain `RelicDefinition` — no EF entity, `DbContext`, database DTO, or JSON payload crosses the boundary (`ARCHITECTURE.md` §2 item 3).
- `src/backend/GameServer.Application/Relics/ScopedRelicDefinitionLookup.cs` — new. The singleton adapter: it holds only `IServiceScopeFactory`, creates one scope per lookup, resolves the scoped `IRelicRepository`, and delegates to its existing `GetDefinitionAsync`. No scoped repository or `GameDbContext` is captured, and the definition is retained nowhere (no cache).
- `src/backend/GameServer.Application/DependencyInjection.cs` — updated. `services.AddSingleton<IRelicDefinitionLookup>(provider => new ScopedRelicDefinitionLookup(provider.GetRequiredService<IServiceScopeFactory>()))`, the same registration shape as `ICardDefinitionLookup`. No consumer is wired: `BattleStateService` is unchanged and no Relic resolution is added.
- `tests/backend/GameServer.Application.Tests/RelicDefinitionLookupTests.cs` — new. 15 tests: the contract shape (one method, no Player context, `Task<RelicDefinition>`); adapter constructor takes only `IServiceScopeFactory`; known identity returns the same Domain instance; structured `Condition` preserved; complete ordered `EffectDefinition[]` preserved member-by-member; null `Condition` preserved; unknown identity and empty store return `null`; one scope per lookup; blank/whitespace/null identity rejected before any scope or query; singleton registration usable from the root scope under `ValidateScopes = true`.
- `tests/backend/GameServer.Application.Tests/ApplicationRegistrationTests.cs` — updated. 1 new test asserting the `IRelicDefinitionLookup` descriptor is a singleton built by a factory. No existing assertion changed.
- `tests/backend/GameServer.Infrastructure.Tests/RelicDefinitionLookupTests.cs` — new. 8 tests: the existing repository read returns the structured Domain definition by identity; unknown identity (including the deferred `relic-burning-curse`) returns `null`; the structured `Condition` of all four provisioned rows materializes form-and-threshold; the complete ordered `EffectDefinition[]` materializes with all five members per element; a null `Condition` round-trips as null; and three applied-PostgreSQL tests reading the TASK-132 rows through `RelicRepository`, plus the end-to-end composition `AddApplicationServices` + `AddDbContext` + `IRelicRepository` → `IRelicDefinitionLookup` → applied row → Domain (under `ValidateScopes = true`).

### Validation Results

- `dotnet test tests/backend/GameServer.Domain.Tests` — PASS (1405 tests; unchanged)
- `dotnet test tests/backend/GameServer.Application.Tests` — PASS (465 tests; baseline 449, +16)
- `dotnet test tests/backend/GameServer.Infrastructure.Tests` — PASS (362 tests; baseline 354, +8; live PostgreSQL at `localhost:5433`, applied `RelicDefinition` jsonb rows read, none skipped)
- `dotnet test tests/backend/GameServer.Api.Tests` — PASS (265 tests; unchanged)
- Total — PASS (2497 tests; baseline 2473, +24). No frontend change was required.
- The live-PostgreSQL precondition was verified with a temporary probe test that asserts (rather than skips) that the `RelicDefinition` `Condition`/`EffectDefinition` columns are `jsonb` and that `relic-berserker-core` reads as `MatchCountAtLeast | 3 | array | ATK | 5`. The probe passed and was deleted; it is not part of the suite.

### Reused Storage, No New Schema

No table, column, index, migration, or `docs/` contract was added or changed. The read goes through the TASK-132 structured `jsonb` storage and the existing `RelicDefinition`/`GameDbContext` mapping via `RelicRepository.GetDefinitionAsync`; a value that is not well formed is rejected by the Domain reader rather than defaulted (`DATABASE.md` §1 item 6), and no synthetic or fallback definition exists. `RelicRepository` was not modified.

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed no Relic trigger evaluation, condition evaluation, or effect execution
- [x] Confirmed no TASK-133 wiring (`BattleStateService.ResolveSwapAsync` unchanged)
- [x] Confirmed no `PetState`/`BattleState` change and no runtime modifier carrier consumed
- [x] Confirmed no PostgreSQL schema, migration, or table change
- [x] Confirmed no Redis key, cache, or definition snapshot introduced
- [x] Confirmed no SignalR protocol, event, or wire member change
- [x] Confirmed no `PlayerId`/ownership dependency in the definition lookup
- [x] Confirmed `tasks/completed/` and TASK-133/134/136/137/138/139/140 unmodified

### No Stop Condition Fired

The audit of `DATABASE.md` §1 (the `RelicDefinition` block and its Relic `EffectDefinition`/`Condition` contract note), `RELIC_RULES.md` §1/§2.2/§8.1–§8.7, `ARCHITECTURE.md` §2.1/§5, `TDD.md` §4, `GAME_STATE.md` §0/§2.3, and `REDIS_STATE.md` §2 against the existing `ICardDefinitionLookup`/`ScopedCardDefinitionLookup` and `IRelicRepository`/`RelicRepository` code found **no conflict**: the Application ownership boundary is unambiguous (the Card precedent), the Domain `RelicDefinition` shape matches TASK-131/TASK-132, the structured `jsonb` storage is readable through the existing contract, and the missing-definition convention is `null` across `ICardDefinitionLookup`, `IRelicRepository`, and `IBossDefinitionLookup`. The singleton/scoped lifetimes required only the repository's existing scope-factory adaptation, so no lifetime, architecture, schema, Redis, gameplay, or ADR decision was changed.

