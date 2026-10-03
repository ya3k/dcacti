# TASK-140 — Implement PetState ATKModifiers and CardCostModifiers Runtime State Carriers

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and tasks/ by path and section.

  THIS TASK IS A RUNTIME STATE AND SCHEMA IMPLEMENTATION TASK.
  It implements the two BattleState runtime collections defined in authoritative
  documentation (GAME_STATE.md §2.3.5, §2.3.6, §2.3.7, §2.3.8, §5.1.3, §5.1.4):
    - PetState.ATKModifiers[]
    - PetState.CardCostModifiers[]
  along with their Domain representation, lifecycle helpers, JSON serialization,
  and active BattleState Redis persistence.

  THIS IS NOT A RELIC RESOLUTION TASK.
  It does not implement Relic trigger evaluation, condition evaluation, effect execution,
  RelicTriggered events, or IRelicDefinitionLookup.

  PROVENANCE: Identified as an independent prerequisite blocker during the TASK-133
  execution STOP. While TASK-138 and TASK-139 resolved and codified the combat
  arithmetic composition formula for Pet ATK modifiers, source code PetState
  completely lacks the ATKModifiers[] and CardCostModifiers[] collections required
  to hold applied Battle-lifetime modifiers.
-->

---

## Metadata

```text
Task ID:           TASK-140
Type:              FEATURE (TASK_TYPES.md §2 — "Implement a documented mechanic,
                   capability, or system that already has a home in docs/ but has
                   not yet been built"; workflow development/feature.md)
Status:            DONE (direct execution recorded: Changed Files + Validation
                   Results in Completion Evidence; all twenty-one acceptance
                   criteria independently verified — see lifecycle note below.)
                   Lifecycle reconciliation: transitioned BACKLOG → DONE per
                   TASK_LIFECYCLE.md §3 (direct execution verified: state shape,
                   deterministic serialization, replace/coexist/remove, Redis
                   CAS round-trip, and regression suites all PASS).
Risk:              MEDIUM (touches Domain BattleState, PetState schema, JSON
                   serialization, and active Redis battle state persistence)
Priority:          HIGH (blocks server-authoritative Relic effect resolution in TASK-133)
Primary Agent:     backend (ARCHITECTURE.md Domain and Application layers owner)
Supporting Agents: testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery,
                   gameplay/authority-determinism-audit,
                   backend/persistence-analysis,
                   testing/test-scenario-generation,
                   quality/architecture-conformance
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-134 (DONE — CardCostModifier state carrier contract),
                   TASK-136 (DONE — Relic Battle-lifetime ATK runtime carrier contract),
                   TASK-137 (DONE — Relic × BuffDebuff coexistence contract),
                   TASK-138 (DONE — Pet ATK composition decision set D1–D6),
                   TASK-139 (DONE — Applied unified Pet ATK composition contract)
Blocks:            TASK-133 (unblocks runtime carrier prerequisite only;
                   IRelicDefinitionLookup remains a separate blocker)
Estimate:          Normal (Domain types, PetState properties, lifecycle helpers,
                   serialization DTOs/mappers, and unit/integration tests)
```

---

## Objective

Implement the server-authoritative BattleState runtime collections `PetState.ATKModifiers[]`
and `PetState.CardCostModifiers[]` in `src/backend/GameServer.Domain`, including their
instance schemas, state lifecycle helper methods (apply/refresh, remove), JSON serialization
and deserialization in `BattleStateSerializer`, and round-trip active battle state
persistence in Redis (`battle:{battleId}:state`), adhering strictly to `GAME_STATE.md`
§2.3.5, §2.3.6, §2.3.7, §2.3.8, §5.1.3, and §5.1.4.

This task implements runtime state carriers and serialization only. It does not
implement Relic trigger evaluation, effect execution, or `IRelicDefinitionLookup`.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Active Battle State and Relics in MVP scope
- `docs/02-technical/GAME_STATE.md` §2.3.5 — `CardCostModifiers[]` Instance Schema (`SourceIdentity`, `CostReductionPercentage`)
- `docs/02-technical/GAME_STATE.md` §2.3.6 — `CardCostModifiers[]` JSON Serialization and Round-Trip
- `docs/02-technical/GAME_STATE.md` §2.3.7 — `ATKModifiers[]` Instance Schema (`SourceIdentity`, `ATKModifierPercentage`, deterministic sort)
- `docs/02-technical/GAME_STATE.md` §2.3.8 — `ATKModifiers[]` JSON Serialization and Round-Trip
- `docs/02-technical/GAME_STATE.md` §5.1.3 — `CardCostModifiers[]` Lifecycle (Apply, Refresh, Expire)
- `docs/02-technical/GAME_STATE.md` §5.1.4 — `ATKModifiers[]` Lifecycle (Apply, Refresh, Expire)
- `docs/02-technical/GAME_STATE.md` §0 item 5 — No second representation of state
- `docs/02-technical/REDIS_STATE.md` §4, §7 — Battle state key `battle:{battleId}:state`, CAS versioning, TTL
- `docs/02-technical/TDD.md` §6 — Determinism and integer arithmetic
- `docs/02-technical/ARCHITECTURE.md` §2.1, §5 — Domain purity and anti-overengineering
- `docs/01-game-design/COMBAT_RULES.md` §5.6 / §5.6.6 — Unified Pet ATK composition contract (permanent `PetState.ATK`, derived `EffectivePetATK`)
- `docs/01-game-design/RELIC_RULES.md` §8.3, §8.4, §8.5 — Relic effect types, targets, and Battle lifetimes

---

## Scope

### In Scope

- **`ATKModifier` Domain Model & Lifecycle**:
  - Add `ATKModifier` record in `GameServer.Domain.Battle` with exact documented members:
    - `SourceIdentity` (`string`, required)
    - `ATKModifierPercentage` (`int`, required — signed percentage points)
  - Add `ATKModifiers` static lifecycle helper class in `GameServer.Domain.Battle`:
    - `Apply`: Adds a new modifier or replaces/refreshes an existing modifier with the same `SourceIdentity` in place (never duplicates; two simultaneous elements with the same identity are never observable).
    - `Remove`: Removes the modifier matching a specific `SourceIdentity` (idempotent, leaves other sources untouched).
    - Deterministic ordering: Preserves elements sorted by `SourceIdentity` ordinal (`GAME_STATE.md` §2.3.7 item 7).
- **`CardCostModifier` Domain Model & Lifecycle**:
  - Add `CardCostModifier` record in `GameServer.Domain.Battle` with exact documented members:
    - `SourceIdentity` (`string`, required)
    - `CostReductionPercentage` (`int`, required — percentage points)
  - Add `CardCostModifiers` static lifecycle helper class in `GameServer.Domain.Battle`:
    - `Apply`: Adds a new modifier or replaces/refreshes an existing modifier with the same `SourceIdentity` in place (never duplicates).
    - `Remove`: Removes the modifier matching a specific `SourceIdentity`.
    - Preserves deterministic element order for round-trip fidelity (`GAME_STATE.md` §2.3.6 item 6).
- **`PetState` Integration**:
  - Add `ATKModifiers` property (`ATKModifier[]`, init-only, initialized to `[]`).
  - Add `CardCostModifiers` property (`CardCostModifier[]`, init-only, initialized to `[]`).
  - Update `PetState.AtBattleCreation` to initialize both collections to empty arrays `[]` (always present, never null, never omitted).
  - Add equality / comparison helper methods (`ATKModifiersEqual`, `CardCostModifiersEqual`).
  - Ensure `PetState.ATK` remains permanent base ATK; no `EffectivePetATK` property is stored on `PetState`.
- **JSON Serialization & Deserialization**:
  - Update `BattleStateJsonNames` with member names:
    - `atkModifiers = "atkModifiers"`
    - `cardCostModifiers = "cardCostModifiers"`
  - Add DTO records in `GameServer.Domain.Battle.Serialization`:
    - `ATKModifierJson` (`sourceIdentity`, `atkModifierPercentage`)
    - `CardCostModifierJson` (`sourceIdentity`, `costReductionPercentage`)
  - Update `PetStateJson` to include both collections.
  - Update `BattleStateSerializer`:
    - Maps domain collections to JSON DTOs (empty collection serializes as `[]`, never omitted or null).
    - Maps JSON DTOs back to domain arrays losslessly.
- **Active BattleState Redis Persistence**:
  - Verify existing `battle:{battleId}:state` serialization path preserves both collections across write-back and recovery without schema or TTL modifications.
- **Automated Verification**:
  - Unit tests for `ATKModifier`, `CardCostModifier`, and their lifecycle helpers.
  - Serialization round-trip unit tests (empty and populated states).
  - Redis persistence integration tests verifying CAS, versioning, and state integrity.

### Out of Scope

- Relic trigger evaluation, condition matching, and effect execution (reserved for TASK-133).
- `IRelicDefinitionLookup` interface or implementation (separate independent TASK-133 prerequisite).
- Implementing gameplay damage calculations or full `EffectivePetATK` pipeline evaluation (contract owned by `COMBAT_RULES.md` §5.6.6).
- Implementing `EffectiveCardCost` calculation in `CardCastExecutor` (owned by Card resolution).
- Modifying SignalR protocol, messages, or hubs (these are internal BattleState collections, not wire members).
- Modifying PostgreSQL database schema, migrations, or tables (active battle state is stored in Redis only).
- Introducing any new ATK cap or valid range.
- Modifying `TASK-133`, `TASK-136`, `TASK-137`, `TASK-138`, or `TASK-139`.
- Modifying `docs/` or authoritative documentation.

---

## Current State

- `src/backend/GameServer.Domain/Battle/PetState.cs` currently contains `ActiveStatusEffects` (`StatusEffect[]`) and `NextAttackCritModifiers` (`NextAttackCritModifier[]`), but does **not** contain `ATKModifiers` or `CardCostModifiers`.
- `src/backend/GameServer.Domain/Battle/Serialization/BattleStateJson.cs` and `BattleStateSerializer.cs` serialize `PetStateJson` with `activeStatusEffects` and `nextAttackCritModifiers`, but have no mappings for `atkModifiers` or `cardCostModifiers`.
- `GAME_STATE.md` already defines the full specification for both collections (§2.3.5, §2.3.6, §2.3.7, §2.3.8, §5.1.3, §5.1.4).

---

## Acceptance Criteria

- [ ] `PetState.ATKModifiers[]` exists with exactly the documented fields (`SourceIdentity: string`, `ATKModifierPercentage: int`).
- [ ] `PetState.CardCostModifiers[]` exists with exactly the documented fields (`SourceIdentity: string`, `CostReductionPercentage: int`).
- [ ] Required collection initialization semantics are preserved (both collections initialize to `[]` at battle creation, never null, never omitted).
- [ ] Same-source refresh/replacement is deterministic (applying a modifier with an existing `SourceIdentity` replaces the entry in place and never duplicates).
- [ ] Different sources can coexist simultaneously in both collections.
- [ ] Source-specific removal is deterministic (removing by `SourceIdentity` deletes only that source's modifier).
- [ ] `ATKModifiers` ordering is deterministic (sorted by `SourceIdentity` ordinal per `GAME_STATE.md` §2.3.7 item 7).
- [ ] Both collections serialize according to `GAME_STATE.md` (`atkModifiers` and `cardCostModifiers` JSON arrays).
- [ ] Both collections round-trip losslessly (`Domain -> JSON -> Domain` preserves counts, fields, values, and order).
- [ ] Empty collections round-trip as empty arrays `[]` (never omitted, never null).
- [ ] Existing Redis active-state persistence (`battle:{battleId}:state`) preserves both collections.
- [ ] Existing Redis TTL semantics remain unchanged.
- [ ] Existing CAS/version semantics remain unchanged.
- [ ] No PostgreSQL schema changes, tables, or migrations are introduced.
- [ ] No SignalR contract changes or wire events are introduced.
- [ ] No parallel/duplicate state representation is introduced (`EffectivePetATK` is not stored).
- [ ] `PetState.ATK` remains permanent/base state (never mutated or overwritten by modifiers; no `DefaultATK` reset).
- [ ] No Relic trigger/effect resolution is implemented.
- [ ] No `IRelicDefinitionLookup` is implemented.
- [ ] Existing tests remain green.
- [ ] New tests cover both carriers, lifecycle helpers, serialization round-trip, and Redis persistence.

---

## Affected Files & Areas

```text
[x] src/backend/GameServer.Domain/Battle/
    ├── ATKModifier.cs (new)
    ├── ATKModifiers.cs (new)
    ├── CardCostModifier.cs (new)
    ├── CardCostModifiers.cs (new)
    ├── PetState.cs (updated)
    └── Serialization/
        ├── BattleStateJson.cs (updated)
        └── BattleStateSerializer.cs (updated)
[x] tests/backend/GameServer.Domain.Tests/
    └── Battle/ (unit tests for modifiers, PetState, and serialization)
[x] tests/backend/GameServer.Infrastructure.Tests/
    └── Redis/ (active state round-trip persistence tests)
[ ] docs/ (NONE — documentation is already complete and authoritative)
[x] tasks/backlog/TASK-140-... (this file)
```

---

## Implementation Notes

- **Precedent Pattern**: Follow the pattern established by `NextAttackCritModifier.cs`, `NextAttackCritModifiers.cs`, and their serialization in `BattleStateSerializer.cs`.
- **Field Types**:
  - `ATKModifier.ATKModifierPercentage`: `int` (signed percentage points; positive = increase, negative = decrease per `COMBAT_RULES.md` §5.6.6 and TASK-138 D3).
  - `CardCostModifier.CostReductionPercentage`: `int` (cost reduction percentage points per `GAME_STATE.md` §2.3.5).
  - `SourceIdentity`: `string` (non-null, non-empty stable source token, e.g. Relic instance/definition identity).
- **Collection Immutability**: Like `StatusEffects` and `NextAttackCritModifiers`, properties on `PetState` should be immutable arrays (`T[]`) with `init` accessors, returning modified new copies on lifecycle operations or when rebuilding state.
- **Ordering**:
  - `ATKModifiers`: `GAME_STATE.md` §2.3.7 item 7 specifies elements are kept sorted by `SourceIdentity` ordinal.
  - `CardCostModifiers`: `GAME_STATE.md` §2.3.6 item 6 requires round-trip order preservation.
- **Wire Safety**: Neither collection is exposed in SignalR client projections. They remain server-authoritative internal BattleState.

---

## Testing Requirements

### Required Verification

```text
[ ] Unit tests         — ATKModifier & CardCostModifier records and lifecycle methods
                         (Apply, Refresh, Remove, Sort order)
[ ] Unit tests         — PetState initialization, equality, and state immutability
[ ] Unit tests         — BattleStateSerializer round-trip (empty & populated collections)
[ ] Integration tests  — Active battle Redis state persistence (CAS, serialization, reload)
```

### Key Edge Cases

- **Empty Collection Round-Trip**: Empty arrays must serialize as `[]` and deserialize as non-null empty arrays (`[]`), never `null`.
- **Same-Source Re-Application**: Applying a modifier from an already-present `SourceIdentity` must replace the value without increasing array length.
- **Removal of Non-Existent Source**: Removing a `SourceIdentity` not present in the array must be an idempotent no-op.
- **Negative ATK Modifiers**: `ATKModifierPercentage` must preserve negative integers (e.g., `-30`).
- **Multiple Sources**: Multiple distinct sources must coexist and maintain their respective values independently.

---

## Stop Conditions

The implementation agent must STOP immediately and report per `AGENTS.md` §20 if:

- Authoritative field names or types differ from `GAME_STATE.md` §2.3.5 / §2.3.7.
- Serialization shape or nullability requirements are ambiguous.
- Redis persistence requires a new key pattern, hash, or secondary document.
- Implementation appears to require modifying PostgreSQL schema or adding EF Core migrations.
- Implementation appears to require modifying `SIGNALR_PROTOCOL.md` or client events.
- Implementation appears to require adding a third field to either modifier record (e.g. `Duration`, `RemainingTurns`, `ExpiresAt`).
- Implementation attempts to fold in Relic trigger evaluation, condition matching, or effect execution.
- Implementation attempts to fold in `IRelicDefinitionLookup`.
- Implementation attempts to compute or store `EffectivePetATK` on `PetState`.

---

## Downstream Handoff

```text
TASK-140 (this task)
    ↓ implements PetState.ATKModifiers[] & PetState.CardCostModifiers[]
TASK-141 (separate prerequisite task, if needed)
    ↓ implements IRelicDefinitionLookup
TASK-133
    ↓ can resume server-authoritative Relic trigger and effect resolution
```

Completing this task resolves the runtime state carrier blocker for TASK-133.
TASK-133 remains blocked on its remaining independent prerequisites (`IRelicDefinitionLookup`).

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Changed Files

- `src/backend/GameServer.Domain/Battle/ATKModifier.cs` — new. `SourceIdentity` (string, non-empty) + `ATKModifierPercentage` (int, signed percentage points). Exactly two members; no third (§2.3.7 items 4/5/8).
- `src/backend/GameServer.Domain/Battle/ATKModifiers.cs` — new. `Apply` (append or refresh in place, never duplicate), `Remove` (source-specific, idempotent no-op when absent), `ModifiersEqual`, plus the `SourceIdentity` deterministic sort of §2.3.7 item 7 applied on every write.
- `src/backend/GameServer.Domain/Battle/CardCostModifier.cs` — new. `SourceIdentity` (string, non-empty) + `CostReductionPercentage` (int). Exactly two members; no `StackCount` or other third member (§2.3.5 items 5/7).
- `src/backend/GameServer.Domain/Battle/CardCostModifiers.cs` — new. `Apply`, `Remove`, `ModifiersEqual`; order-preserved, deliberately **not** sorted (§2.3.5 item 8, §2.3.6 item 6).
- `src/backend/GameServer.Domain/Battle/PetState.cs` — updated. Added `ATKModifiers` and `CardCostModifiers` (`init`-only `T[]`, initialized `[]`), `ATKModifiersEqual`/`CardCostModifiersEqual`, `AtBattleCreation` initialization of both, and the `ATK` doc note recording that the permanent stat is never mutated and `EffectivePetATK` is not stored. No existing member renamed or removed.
- `src/backend/GameServer.Domain/Battle/Serialization/BattleStateJson.cs` — updated. Added `atkModifiers`/`cardCostModifiers` member names, their `sourceIdentity`/`atkModifierPercentage`/`costReductionPercentage` element names, the two `PetStateJson` members, and the `ATKModifierJson`/`CardCostModifierJson` DTOs. Both `PetStateJson` members are always written with no ignore condition.
- `src/backend/GameServer.Domain/Battle/Serialization/BattleStateSerializer.cs` — updated. Both directions mapped: `[]` when empty, never omitted or null; `null` rejected with a message naming §2.3.7 item 6 / §2.3.5 item 6; blank `SourceIdentity` rejected naming §2.3.7 item 3 / §2.3.5 items 3+7. No re-sorting on read and no reordering of the order-preserving collection.
- `tests/backend/GameServer.Domain.Tests/Battle/ATKModifierTests.cs` — new. 22 tests.
- `tests/backend/GameServer.Domain.Tests/Battle/CardCostModifierTests.cs` — new. 18 tests.
- `tests/backend/GameServer.Domain.Tests/Battle/BattleStateModifierSerializationTests.cs` — new. 13 tests.
- `tests/backend/GameServer.Domain.Tests/BattleStateTests.cs` — updated. `PetState_ShouldCarryExactlyTheDocumentedFields` extended additively for the two new members (the established per-stage pattern). No existing assertion weakened.
- `tests/backend/GameServer.Application.Tests/BattleStateSerializationLifecycleTests.cs` — updated. 3 new tests over the production `BattleStateService.CreateBattleAsync` path.
- `tests/backend/GameServer.Infrastructure.Tests/RedisBattleStateRepositoryTests.cs` — updated. 4 new tests against live Redis.

### Validation Results

- `dotnet test tests/backend/GameServer.Domain.Tests` — PASS (1405 tests; baseline 1342, +63)
- `dotnet test tests/backend/GameServer.Application.Tests` — PASS (449 tests; baseline 446, +3)
- `dotnet test tests/backend/GameServer.Infrastructure.Tests` — PASS (354 tests; baseline 350, +4; live Redis at `127.0.0.1:6379`, carrier round-trip and CAS tests executed, none skipped)
- `dotnet test tests/backend/GameServer.Api.Tests` — PASS (265 tests; baseline 265, +0)
- Total — PASS (2473 tests; baseline 2403, +70). No frontend change was required.

### Recorded Implementation Decisions (not doc-mandated; TASK-140-local)

1. **`SourceIdentity` comparer for the §2.3.7 item 7 sort is `StringComparer.Ordinal`.** `GAME_STATE.md` §2.3.7 item 7 and `TASK-136` D2/D9 require a "deterministic sort on the identity key" but name no comparison. Ordinal was chosen because it is a pure code-point comparison and therefore cannot vary with host culture/locale/ICU — which is what `TDD.md` §6 and `REDIS_STATE.md` §7 item 16's byte-identical-serialization requirement need. It also matches the convention §5.1.1 item 6 already fixes for `StatusEffects[]` and that `StatusEffectLifecycle` already implements.
2. **The CLR type is `int`.** §2.3.7/§2.3.5/§2.3.8/§2.3.6 fix the *serialized* type as JSON `number` and the unit as percentage points; `int` is TASK-140's conforming Domain choice, matching `PetState.Crit`'s existing percentage-unit convention.
3. **`CardCostModifiers` is not sorted.** §2.3.6 item 6 requires order *preservation* where the sibling §2.3.7 item 7 requires a sort. The two lifecycles therefore differ deliberately.

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed no PostgreSQL schema changes or migrations introduced
- [x] Confirmed no SignalR contract or wire events introduced
- [x] Confirmed no Relic trigger/effect resolution implemented
- [x] Confirmed no IRelicDefinitionLookup implemented
- [x] Confirmed PetState.ATK remains permanent/base and EffectivePetATK is not stored

### No Stop Condition Fired

The audit of `GAME_STATE.md` §2.3.5–§2.3.8 / §5.1.3–§5.1.4 against `COMBAT_RULES.md` §5.6.6, `CARD_RULES.md` §3.6, `RELIC_RULES.md` §8.3–§8.5, `REDIS_STATE.md` §7 items 15–16, `DATABASE.md`, `SIGNALR_PROTOCOL.md` §4.2/§4.3, `TASK-134`, `TASK-136`, `TASK-138` and `TASK-139` found **no conflict** on member names, types, requiredness, nullability, always-present semantics, refresh semantics, removal semantics, ordering, or Redis persistence. No third modifier field, no new SignalR member, no PostgreSQL persistence, no new architectural abstraction, and no new gameplay rule was required.

### Remaining Blocker for TASK-133

`IRelicDefinitionLookup` (Application-layer definition-lookup contract/boundary) remains open and is **not** addressed by this task. TASK-140 resolves only the runtime state carrier prerequisite.

