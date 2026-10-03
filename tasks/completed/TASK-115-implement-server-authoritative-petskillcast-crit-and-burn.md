# TASK-115 — Implement Server-Authoritative PetSkillCast, Combat Crit Evaluation, and Burn Lifecycle

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, schemas, or contracts.

  PROVENANCE: Sequenced implementation task following completed TASK-113
  (RNG/Crit/Burn decision contract) and TASK-114 (authoritative documentation
  of that contract).
  All required gameplay, RNG, data, wire, and lifecycle contracts are now
  authoritative in COMBAT_RULES.md v1.7, CARD_RULES.md v1.5, GAME_RULES.md v1.6,
  GAME_STATE.md v1.11, TDD.md v1.5, DATABASE.md v1.24, and SIGNALR_PROTOCOL.md v2.8.
-->

---

## Metadata

```text
Task ID:           TASK-115
Type:              FEATURE
Status:            DONE (Server-authoritative PetSkillCast, Damage Pipeline step 4
                   Crit evaluation, NextAttackCritModifiers[] consumption, and
                   step-19a Burn DoT ticking implemented and verified. All 2344
                   backend tests and 477 frontend tests passing. Formal review
                   pass completed against quality/review.md §1 and core/completion.md
                   §1: PASS. File moved from tasks/backlog/ to tasks/completed/
                   per TASK_LIFECYCLE.md §3.)
Risk:              HIGH
Priority:          HIGH
Primary Agent:     backend
Supporting Agents: gameplay, realtime, testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   gameplay/gameplay-behavior-derivation,
                   gameplay/authority-determinism-audit,
                   realtime/realtime-protocol-validation,
                   testing/test-scenario-generation,
                   quality/scope-validation
                   (7 skills — Complex budget, tasks/README.md §12 hard limit)
Dependencies:      TASK-114 (DONE — recorded Combat RNG draw contract into COMBAT_RULES.md, TDD.md, GAME_STATE.md),
                   TASK-113 (DONE — Product Owner decisions D-1 through D-5 on Crit, Burn, and representation),
                   TASK-112 (DONE — encoded CardDefinition.EffectDefinition array types and 6 content rows),
                   TASK-107 (DONE — implemented Basic CardCast server path),
                   TASK-105 (DONE — Shield refresh-not-stack semantics and depletion contract in COMBAT_RULES.md §4),
                   TASK-095/TASK-096 (DONE — StatusEffect domain model and step-19a lifecycle and serialization)
Blocks:            Client-side Card/Skill cast UI (Phase 1 follow-up)
```

---

## Objective

Implement the server-authoritative combat execution path for `PetSkillCast`, deterministic Combat Crit evaluation in Damage Pipeline step 4, and the End-Turn (step 19a) Burn damage ticking and lifecycle on the active battle server, fulfilling the ROADMAP.md Phase 1 Pet Signature Skill milestone without introducing new RNG streams, new Battle Events, or client-side calculation.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Cards (3 Basic + 5 Pet Skill), Pets (Signature Skill), and Combat (Damage Pipeline, Crit, Status Effects) are IN.
- `docs/01-game-design/COMBAT_RULES.md` §1.1, **§3.1–§3.3** (Damage Pipeline, Step 4 Other Modifiers, deterministic Crit roll procedure, bounded RNG selection over bound 100, $V < \text{Crit stat}$, 1.5× multiplier), **§4** (Shield absorption, refresh-not-stack, depletion at 0, overflow to HP), **§5.1–§5.3** (Status Effects, Burn DoT tick traversing Damage Pipeline with Combo neutralized and Crit active, duration consumption timing).
- `docs/01-game-design/CARD_RULES.md` §1 (Card categories), §3 (Card casting and validation rules), **§4 / §4.1** (Pet Skill Cards: Inferno, Tidal Barrier, Iron Fang; NextAttack Crit scope; independence from Bạch Hổ passive), §6 (`CardCast` and `PetSkillCast` events).
- `docs/01-game-design/GAME_RULES.md` §11 (Card rules), §12 (Power cap 0–100), §16 (Canonical event list), **§17 step 14 / step 19a** (Resolution order for Player Effects and End-Turn DoT tick), §18 (Server authority).
- `docs/01-game-design/PET_RULES.md` §2 (Signature Skill as Pet Skill Card), §8 (Provisioned MVP Pets: Xích Lang, Bạch Hổ, Huyền Quy; Thanh Xà and Sơn Hùng remain deferred).
- `docs/02-technical/GAME_STATE.md` §2.3 (`PetState`), §2.3.1 (`StatusEffect` schema), **§2.6.2** (`RngState` single stream and consumption advancement), **§5.1.1** (Step-19a StatusEffect lifecycle).
- `docs/02-technical/GAME_EVENTS.md` §1.1 (Ordered event list), §2 (`CardCast`, `PetSkillCast`, `DamageCalculated`, `DamageDealt`, `DamageTaken`), §3 (Ordering).
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§2** (`PetSkillCast(battleId, clientSequence)` method), **§3.2.20 / §3.2.21** (`CardCast` and `PetSkillCast` wire DTOs), **§3.2.22** (Emission order: `CardCast` precedes `PetSkillCast`), §3.2.13 (`otherModifiers` combined step-4 multiplier carrier), §5 (Acknowledgement shape).
- `docs/02-technical/DATABASE.md` §1 (Structured `CardEffectDefinitions` array contract).
- `docs/02-technical/TDD.md` **§6** (Deterministic PRNG architecture: single server-seeded stream consumed by cascade spawn and combat Crit).
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` (Server-authoritative battle).
- `docs/03-decisions/ADR/ADR-009-deterministic-prng.md` (Single PCG32 generator).

---

## Current State

- `DamagePipeline.cs` has step 4 hardcoded to `NoOtherModifiers = 1.00` and contains no Crit evaluation or RNG draw.
- `CardCastExecutor.cs` only handles `CardCategory.Basic` and throws `InvalidOperationException` for `Damage`, `Burn`, or `Crit` effect types.
- `StatusEffectLifecycle.cs` handles Shield application and step-19a expiry, but does not tick Burn damage through the `DamagePipeline`.
- `BattleEventType` and `BattleEventWireProjection.cs` contain `CardCast` but lack `PetSkillCast`.
- `BattleHub.cs` exposes `CardCast` for Basic Cards, while `PetSkillCast` is commented as not implemented.

---

## Scope

### In Scope

1. **Deterministic Combat Crit Evaluation in DamagePipeline**:
   - Execute Crit evaluation at Damage Pipeline Step 4 (`COMBAT_RULES.md` §3.3).
   - Consume exactly one bounded RNG selection over bound 100 ($V \in [0, 100)$) from the battle's single `RngState` pair using existing rejection-sampling semantics (`MATCH3_RULES.md` §1.2.1.4 item 2, `TDD.md` §6).
   - Evaluate success: Crit succeeds iff $V < \text{current Crit stat}$ (integer percentage).
   - When successful, multiply Pre-Defense damage by 1.5× (`COMBAT_RULES.md` §3.3 item 2).
   - Advance and return the updated `RngState` alongside the `DamageResult`.
   - Ensure every damage instance traversing the pipeline (Player direct attack, Pet Skill damage, Boss attack, Burn DoT tick) evaluates Crit identically.
   - Report the combined factor in `DamageCalculation.OtherModifiers` / wire `otherModifiers` (no dedicated Crit event/field).

2. **PetSkillCast and Multi-Effect Domain Execution (`CardCastExecutor`)**:
   - Extend `CardCastExecutor` to execute Pet Skill Cards (`CardCategory.PetSkill`) in addition to Basic Cards.
   - Dispatch on the structured `EffectDefinition[]` array without hardcoding card IDs or parsing prose:
     - `Damage`: Process damage through `DamagePipeline.Calculate` using Card base damage, attacker/defender stats, elements, and Crit evaluation.
     - `Burn`: Apply Burn `StatusEffect` (`Type = StatusEffectType.Burn`, `Magnitude = value`, `RemainingTurns = duration`) via `StatusEffectLifecycle.ApplyBurn` / update active effects.
     - `Crit`: Apply temporary Crit modifier scoped to `NextAttack` (`CARD_RULES.md` §4.1).
     - `Heal` / `Shield` / `Power`: Re-use existing handlers for Pet Skill Cards (e.g. Tidal Barrier's 20% MaxHP Heal and 20% MaxHP Shield refresh).
   - Deduct `PowerCost` from `PetState.Power`.
   - Emit `CardCast` event for every successful cast, and additionally emit `PetSkillCast` when casting the active Pet's Signature Skill Card (`CARD_RULES.md` §6, `SIGNALR_PROTOCOL.md` §3.2.22).

3. **Burn DoT Step-19a Ticking & Lifecycle**:
   - During End-Turn resolution (`GAME_RULES.md` §17 step 19a, `COMBAT_RULES.md` §5.1–§5.3), tick each active Burn `StatusEffect` through `DamagePipeline`.
   - For Burn ticks: Combo Modifier is neutralized (1.00×), Element is Hỏa (Fire), and Step 4 Crit evaluation applies (`COMBAT_RULES.md` §5.2 item 3).
   - Emit `DamageCalculated`, `DamageDealt`, `DamageTaken` events for the tick.
   - Decrement `RemainingTurns`; expire and remove Burn instance at 0 turns in the same step-19a pass.
   - Emit NO `BurnApplied`, `BurnExpired`, or `StatusTicked` Battle Events (`GAME_STATE.md` §5.1.1 item 10, TASK-113 D-5).

4. **Realtime & Wire Integration**:
   - Add `BattleEventType.PetSkillCast` to `BattleEvent.cs` and wire projection in `BattleEventWireProjection.cs` producing `{ "type": "PetSkillCast", "cardId": "..." }` (`SIGNALR_PROTOCOL.md` §3.2.21).
   - Implement `PetSkillCast(battleId, clientSequence)` in `BattleHub.cs` and `BattleStateService.cs`.
   - Validate that the cast Card matches the active Pet's equipped Signature Skill Card and that the Pet has sufficient Power.
   - Commit state under the existing `Sequence` compare-and-set (`IBattleStateRepository.TryUpdateAsync`).
   - Broadcast `BattleStateUpdated` then `ReceiveEvents` containing the ordered batch (`CardCast` then `PetSkillCast`, followed by any resulting damage events).

### Out of Scope

- Match-3 board mechanics, Gem swapping, Cascade, Match detection, Gravity.
- Relic trigger evaluation (`OnCardCast` / `RelicTriggered` is Phase 2 / `ROADMAP.md`).
- Passives logic / charging changes.
- Boss AI / Boss Skill changes.
- Content creation for deferred Pets (Thanh Xà, Sơn Hùng Signature Skills remain TBD).
- New Battle Events (`BurnApplied`, `BurnExpired`, `CriticalHit` are strictly forbidden).
- New SignalR methods, API endpoints, or database migrations.
- Frontend rendering / UI modifications.
- Client-side damage, RNG, or Crit calculation.

---

## State Ownership & Concurrency Requirements

- **Server Authority**: All damage, Crit rolls, Burn applications, Power deductions, and HP mutations are strictly server-authoritative (`AGENTS.md` §10, `ADR-001`).
- **Single RNG Stream**: All Crit draws advance the single `RngState` pair carried in `BattleState.RngState`. No separate seed, generator, or stream is introduced (`ADR-009`, `TDD.md` §6, `GAME_STATE.md` §2.6.2).
- **Concurrency & CAS**: Every `PetSkillCast` commits via `IBattleStateRepository.TryUpdateAsync` using `expectedSequence`. A rejected cast mutates no state and emits no events.
- **Turn & Combo Immunity**: A `PetSkillCast` consumes no Turn and does not alter `Combo` or `MatchCount` (`CARD_RULES.md` §3 item 5).

---

## Acceptance Criteria

- [x] `DamagePipeline.Calculate` consumes exactly one bounded RNG selection over bound 100 ($V \in [0, 100)$) from `RngState` for step 4 Crit evaluation.
- [x] Crit succeeds iff $V < \text{current Crit stat}$ and multiplies Pre-Defense damage by 1.5×; otherwise step 4 multiplier is 1.00×.
- [x] `DamagePipeline.Calculate` advances and returns the updated `RngState` deterministically.
- [x] `BattleHub` exposes `PetSkillCast(battleId, clientSequence)` returning the documented `{ accepted, reason }` acknowledgement shape.
- [x] `PetSkillCast` validates that the cast Card is the active Pet's equipped Signature Skill Card and that `Power >= CardDefinition.PowerCost`.
- [x] A rejected `PetSkillCast` changes nothing in state and emits no events.
- [x] A successful `PetSkillCast` deducts `PowerCost` from `PetState.Power`.
- [x] **Inferno** resolves: deals 100 Flat Fire damage through `DamagePipeline` (with Crit evaluation) and applies Burn (50 Flat damage/tick, 2 Turns duration).
- [x] **Tidal Barrier** resolves: restores 20% Max HP (clamped, overheal discarded) and applies 20% Max HP Shield (refresh/replace semantics, no stacking).
- [x] **Iron Fang** resolves: deals 120 Flat damage through `DamagePipeline` (with Crit evaluation) and grants +10 percentage points Crit chance for `NextAttack`.
- [x] Casting a Signature Skill emits both `CardCast` and `PetSkillCast` in exact documented order (`CardCast` precedes `PetSkillCast`).
- [x] Casting a Basic Card continues to emit only `CardCast`.
- [x] Active Burn `StatusEffect` instances tick at End Turn (step 19a) through the `DamagePipeline` with Combo neutralized (1.00×), Element = Hỏa, and Crit evaluation active.
- [x] Burn tick emits `DamageCalculated`, `DamageDealt`, and `DamageTaken` events.
- [x] Burn application and Burn expiry emit NO Battle Events (`GAME_STATE.md` §5.1.1 item 10, TASK-113 D-5).
- [x] `otherModifiers` in `DamageCalculated` wire DTO carries the combined Step-4 factor (1.50 or 1.00); no dedicated Crit event/wire field exists.
- [x] A cast does not increment `Turn`, does not change `Combo`, and does not trigger Boss Response.
- [x] All existing test suites pass; new unit, application, and integration tests cover all Crit, Burn, and PetSkillCast scenarios.
- [x] Quality review checklist passes (`quality/review.md` §1).
- [x] Zero files under `docs/` modified; no new ADR required.

---

## Affected Files & Areas

```text
[x] src/backend/GameServer.Domain/Combat/DamagePipeline.cs (Step 4 Crit evaluation + RngState advancement)
[x] src/backend/GameServer.Domain/Cards/CardCastExecutor.cs (Pet Skill Card resolution: Damage, Burn, Crit, Heal, Shield)
[x] src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs (Step-19a Burn damage ticking)
[x] src/backend/GameServer.Domain/Match3/BattleEvent.cs (BattleEventType.PetSkillCast + factory)
[x] src/backend/GameServer.Application/Battle/BattleStateService.cs (PetSkillCast orchestration + CAS write-back)
[x] src/backend/GameServer.Api/Hubs/BattleHub.cs (PetSkillCast method + wire projection)
[x] src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs (PetSkillCast wire DTO arm)
[x] tests/backend/ (Unit, Application, Integration tests for Crit, Burn, PetSkillCast)
[ ] docs/ (NONE — authoritative docs already complete)
```

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — DamagePipeline Crit evaluation with seeded PRNG (V < Crit stat succeeds, V >= Crit stat fails, 1.5x multiplier, RngState advances by 1 selection).
[x] Unit tests         — CardCastExecutor executing Inferno, Tidal Barrier, and Iron Fang from structured EffectDefinition[] array.
[x] Unit tests         — StatusEffectLifecycle step-19a Burn tick execution through DamagePipeline with Combo neutralized and Crit active.
[x] Unit tests         — Iron Fang NextAttack Crit buff consumption on the subsequent attack.
[x] Application tests  — BattleStateService.ExecutePetSkillCastAsync validation, Power deduction, CAS retry/update, event emission.
[x] Api/Hub tests      — BattleHub.PetSkillCast acknowledgement shape, caller-only response, BattleStateUpdated before ReceiveEvents, wire projection of PetSkillCast.
[x] Regression tests   — Full test suite (Domain, Application, Infrastructure, Api) passing.
```

### Key Edge Cases

- **Crit threshold boundaries**: Crit stat 0% (never crits), Crit stat 5% ($V \in [0, 4]$ crits, $V \ge 5$ does not), Crit stat 100% (always crits).
- **RNG Determinism**: Exact reproducibility of identical battle state + seed producing identical Crit outcomes.
- **Burn tick Crit**: Burn tick critting (50 × 1.5 = 75 damage before DEF) vs non-critting.
- **Burn duration & expiry**: Burn lasting exactly 2 turns, ticking at step 19a of Turn N and Turn N+1, removed at step 19a of Turn N+1.
- **Tidal Barrier Shield refresh**: Applying Tidal Barrier while a Shield exists replaces the magnitude with 20% Max HP; does not add.
- **Signature Skill derivation**: `PetSkillCast` rejected if the active Pet does not have the corresponding Signature Skill Card.

---

## Stop Conditions

- If `DamagePipeline` Crit evaluation appears to require a new RNG seed, stream, or generator: **STOP per `AGENTS.md` §11 / `ADR-009`**.
- If implementation appears to require a new Battle Event (e.g. `CritOccurred`, `BurnApplied`, `BurnExpired`): **STOP per `AGENTS.md` §7 / TASK-113 D-5**.
- If implementation appears to require changing authoritative documentation: **STOP per `AGENTS.md` §17**.
- If PetSkillCast requires running the Swap pipeline, Combo increment, or Boss Response: **STOP per `CARD_RULES.md` §3 item 5**.
- If Thanh Xà or Sơn Hùng Signature Skill content is required: **STOP per `PET_RULES.md` §8** (remains deferred).

---

## Completion Evidence

### Changed Files
- `src/backend/GameServer.Domain/Combat/DamagePipeline.cs` — Step 4 Crit evaluation consuming bounded PRNG from `RngState`, 1.5x damage multiplier, and state advancement.
- `src/backend/GameServer.Domain/Cards/CardCastExecutor.cs` — Structured `EffectDefinition[]` resolution for Pet Skill Cards (Damage, Burn, Crit, Heal, Shield).
- `src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs` — Step-19a Burn damage ticking through `DamagePipeline` with neutralized combo and active Crit.
- `src/backend/GameServer.Domain/Match3/BattleEvent.cs` — `BattleEventType.PetSkillCast` and event factory.
- `src/backend/GameServer.Application/Battle/BattleStateService.cs` — `PetSkillCast` orchestration, Power validation/deduction, ordered event emission, and CAS write-back.
- `src/backend/GameServer.Api/Hubs/BattleHub.cs` — `PetSkillCast(battleId, clientSequence)` endpoint and acknowledgement.
- `src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs` — `PetSkillCast` wire projection arm.
- `tests/backend/` — Comprehensive unit, application, and API test coverage for Crit, Burn, and PetSkillCast.

### Validation Results
- `dotnet test src/backend/GameServer.sln` — PASS (2344 tests passing across Domain, Application, Infrastructure, Api)
- `npm test` (client) — PASS (477 tests passing across 18 test files)

### Formal Review Pass (quality/review.md & core/completion.md)

- **Correctness:** PASS — Server-authoritative `PetSkillCast`, Combat Crit evaluation (1.5x multiplier, $V < \text{Crit stat}$), `NextAttackCritModifiers[]` consumption, and step-19a Burn DoT ticking match `COMBAT_RULES.md` §3.3/§5.1–§5.3, `CARD_RULES.md` §4.1, `GAME_RULES.md` §17, and `GAME_STATE.md` §2.6.2.
- **Architecture:** PASS — Follows `docs/02-technical/ARCHITECTURE.md`, `ADR-001`, `ADR-009`, `ADR-017`.
- **Scope:** PASS — Strictly confined to PetSkillCast, Crit, and Burn execution. 0 files modified under `docs/` or `docs/03-decisions/ADR/`.
- **Tests:** PASS — Full backend and frontend test suites passing (2344 backend + 477 frontend).
- **Documentation:** PASS — Authoritative docs are complete and byte-identical.
- **Security:** PASS — No security or authentication regressions.
- **Performance:** PASS — Single write-back per cast, atomic CAS concurrency.
- **Maintainability:** PASS — Data-driven execution via structured `EffectDefinition[]`.
- **Determinism (Gameplay/Battle Logic):** PASS — Single server-seeded PRNG (`RngState`), server authority strictly preserved.
- **Definition of Done (core/completion.md §1):** PASS — All completion criteria satisfied.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed single RNG stream maintained with no secondary generator
