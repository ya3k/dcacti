# TASK-021 — Implement Damage Pipeline (Steps 15–17)

---

## Metadata

```text
Task ID:           TASK-021
Type:              FEATURE
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: backend, testing
Workflow:          development/feature.md
Skills:            dotnet-backend-patterns
Dependencies:      TASK-016 (combat stats in PlayerState),
                   TASK-018 (resource generation — BaseDamagePool),
                   TASK-019 (player effects healing),
                   TASK-020 (BossState contract)
Historical:        TASK-020A (boss state & MVP boss configuration contract — DONE),
                   TASK-021A (damage events SignalR contract — DONE)
```

---

## Objective

Implement the Damage Pipeline — GAME_RULES.md §17 steps 15–17: Calculate Damage, Apply Element Modifier, and Apply Final Damage. This is the first time player damage reaches the Boss's HP, completing the offensive half of the battle loop.

---

## Context

Steps 1–14 of the resolution are implemented: board resolution, match/combo accounting, resource generation, power update, and player-effect healing. The transient pools produced by resource generation — `BaseDamagePool` and `DefensePool` — are carried on `SwapExecutionResult.Resources` but are consumed by no pipeline step yet. The Boss's HP is never reduced. This task closes that gap.

---

## Authoritative Sources

- `docs/00-overview/MVP_SCOPE.md` §1 — confirm Damage Pipeline is IN scope
- `docs/01-game-design/GAME_RULES.md` §5 — Combo damage multipliers (table)
- `docs/01-game-design/GAME_RULES.md` §14 — Combat Rules (damage conceptual flow)
- `docs/01-game-design/GAME_RULES.md` §17 steps 15–17 — resolution order
- `docs/01-game-design/COMBAT_RULES.md` §3 — Damage Pipeline (6-step formula)
- `docs/01-game-design/COMBAT_RULES.md` §3.2 — Defense Mitigation formula (K constant)
- `docs/01-game-design/COMBAT_RULES.md` §3.3 — Critical Hits (deferred; MVP has no Crit roll)
- `docs/01-game-design/ELEMENT_RULES.md` §2.2 — Element Modifier values
- `docs/02-technical/GAME_STATE.md` §2.2 — PlayerState (ATK, DEF, Crit)
- `docs/02-technical/GAME_STATE.md` §2.4 — BossState (HP, DEF, Element)
- `docs/02-technical/GAME_EVENTS.md` §2 — DamageCalculated / DamageDealt / DamageTaken events

---

## Scope

### In Scope

- Calculate Base Damage: `PlayerState.ATK + BaseDamagePool`
- Apply Combo Modifier: `GAME_RULES.md` §5 table lookup by `PlayerState.Combo`
- Apply Element Modifier: `ELEMENT_RULES.md` §2.2 via `ElementMatchups.Resolve` and `ElementModifiers`
- Apply Defense Mitigation: `COMBAT_RULES.md` §3.2 formula `Pre-Defense × (K / (K + DEF))`
- Apply Final Damage to Boss HP: `BossState.HP - FinalDamage`, clamped to ≥ 0
- Emit `DamageCalculated`, `DamageDealt`, `DamageTaken` events (GAME_EVENTS.md §2)
- Unit tests for every pipeline step and the full pipeline

### Out of Scope

- Relic / Passive / Buff / Debuff modifiers (COMBAT_RULES.md §3 step 4 "Other Modifiers") — not yet implemented
- Critical Hit roll (COMBAT_RULES.md §3.3) — MVP Crit is a stat on PlayerState but no roll is performed
- Shield absorption (COMBAT_RULES.md §4 item 2) — not yet implemented
- Boss damage to Player (Boss Skill / Boss Attack) — steps 18+, own task
- Victory / Defeat check — step 19, own task
- Any system listed in `docs/00-overview/MVP_SCOPE.md` §2 (OUT)

---

## Current State

- `PlayerState` has `ATK`, `DEF`, `Crit` (TASK-016)
- `BossState` has `HP`, `MaxHP`, `DEF`, `Element` (TASK-020)
- `ResourceGeneration` carries transient `BaseDamagePool` and `DefensePool` (TASK-018)
- `SwapExecutionResult` carries the `ResourceGeneration` (committed result)
- `ElementModifiers` record exists with default factors (TASK-008)
- `ElementMatchups.Resolve(attackElement, defendElement)` exists (TASK-008)
- `BattleEventType` enum and `BattleEvent` record exist but have no Damage members (TASK-006)
- No damage calculation, no HP reduction, no Damage events exist

---

## Acceptance Criteria

- [x] Given PlayerState.ATK = 50 and BaseDamagePool = 30, when Calculate Base Damage runs, then Base Damage = 80
- [x] Given Base Damage = 80 and Combo = 3 (1.20×), when Combo Modifier is applied, then result = 96
- [x] Given Pre-Combo damage and attacking Element Mộc against defending Element Thổ (Advantage), when Element Modifier is applied, then result = Pre-Combo × 1.50
- [x] Given Pre-Element damage = 96 and Boss.DEF = 40 with K = 100, when Defense Mitigation is applied, then Mitigated Damage = 96 × (100 / (140)) ≈ 68.57 → truncated toward zero to 68 (Math.Truncate or equivalent; no rounding-to-nearest)
- [x] Given Final Damage = 68 and Boss.HP = 500, when Final Damage is applied, then Boss.HP = 432
- [x] Given Final Damage = 600 and Boss.HP = 500, when Final Damage is applied, then Boss.HP = 0 (clamped, not negative)
- [x] `DamageCalculated` event is emitted with full breakdown per `GAME_EVENTS.md` §2: Base, Combo Modifier, Element Modifier, Other Modifiers (pass-through 1.00× for MVP), Defense, Final Damage
- [x] `DamageDealt` and `DamageTaken` events are emitted with source, target, and Final Damage amount
- [x] All relevant tests pass at the depth required by `core/validation.md §2` for MEDIUM risk
- [x] `quality/review.md §1` checklist passes
- [x] Documentation impact addressed (§ Documentation Impact below)

---

## Affected Areas

```text
[x] Domain (GameServer.Domain/) — Combat module (new DamagePipeline.cs),
    Battle module (BossState HP update), Elements module (existing, read-only),
    Match3 module (SwapExecutionResult carry-through)
[x] Application (GameServer.Application/) — BattleStateService.ExecuteSwap()
    integration: insert Damage Pipeline after PassiveTracker.Charge and before
    final state write-back
[x] API (GameServer.Api/) — BattleEventWireProjection (TASK-021A:
    the three Damage events added to the §3.2 wire schema)
[ ] Client (client/)
[ ] SignalR / Redis (GameServer.Infrastructure/SignalR/, /Redis/)
[ ] PostgreSQL (GameServer.Infrastructure/Postgres/)
[x] Tests (tests/)
[ ] Documentation (docs/) — see Completion Evidence § Documentation Changed
[ ] ADR (docs/03-decisions/ADR/)
```

---

## Implementation Notes

1. **Combo Modifier table** is in `GAME_RULES.md` §5 (canonical). The implementation reads from it; it must not be copied into the task or code comments beyond a reference.

2. **"Other Modifiers" step is a pass-through for MVP.** COMBAT_RULES.md §3 step 4 lists Relic bonuses, Passive bonuses, Buffs/Debuffs, and Crit multiplier. None is implemented in MVP. The pipeline must have a clear extension point for step 4 (a method or a modifier chain) but for now it returns the input unchanged. Do NOT implement Crit roll logic — `PlayerState.Crit` exists as a stat but no roll is performed (§3.3 item 1 says "Crit is evaluated once per damage instance" but the roll infrastructure does not exist yet).

3. **K constant** for Defense Mitigation is `100` (COMBAT_RULES.md §3.2, configuration). Store it as a named constant in the pipeline, not as a magic number.

4. **Element Modifier lookup** uses the existing `ElementMatchups.Resolve(attackElement, defendElement)` which returns `ElementMatchup` (Advantage/Neutral/Disadvantage), then `ElementModifiers.Default.For(matchup)` returns the factor. The attack Element comes from the active Pet's `PetState.Element`; the defend Element comes from `BossState.Element`.

5. **Damage events** (GAME_EVENTS.md §2): `DamageCalculated` carries the full breakdown per the doc spec: Base, Combo Modifier (factor), Element Modifier (factor), Other Modifiers (pass-through 1.00× for MVP — the field is required by the event contract even when no Relic/Passive/Buff modifiers exist), Defense, Final Damage. `DamageDealt`/`DamageTaken` carry source, target, and Final Damage amount. These are Domain values, not wire types — wire serialization is a protocol task.

6. **Boss HP update** is a state write: `BossState with { HP = max(BossState.HP - finalDamage, 0) }`. It is part of the single post-resolution write-back (`GAME_STATE.md` §5.1). Boss HP is never negative.

7. **The pipeline operates on one damage instance per committed Swap.** MVP has no multi-hit, no Damage-over-Time, and no Boss-to-Player damage in this task. A future task extends the pipeline to multiple instances.

8. **Application integration point — `BattleStateService.ExecuteSwap()`** (`GameServer.Application/Battle/BattleStateService.cs:405`). The existing pipeline is:

   ```text
   SwapExecutor.Execute(state, request)        // steps 1–9 (board resolution)
           ↓
   PassiveTracker.Charge(...)                  // step 10 (charge passive)
           ↓
   write-back + return                         // final state write
   ```

   TASK-021 inserts the Damage Pipeline after PassiveTracker.Charge and before the final write-back:

   ```text
   SwapExecutor.Execute(state, request)        // steps 1–9
           ↓
   PassiveTracker.Charge(...)                  // step 10
           ↓
   [new] DamagePipeline.Calculate(...)         // steps 15–17
           ↓
   [new] BossState HP update                   // part of step 17
           ↓
   [new] emit DamageCalculated/DamageDealt/DamageTaken events
           ↓
   write-back + return                         // final state write
   ```

   The Domain `DamagePipeline` remains responsible for the pure calculation and state transformation. Application orchestrates when it executes as part of the existing swap resolution flow. No new service classes (`BattleResolutionService`, `BattleStateManager`, `GameStateManager`) are created — the existing `BattleStateService.ExecuteSwap()` is the integration point.

---

## Testing Requirements

### Test Types Required

```text
[x] Unit tests         — DamagePipeline: each step in isolation, full pipeline,
                         edge cases (zero damage, overkill, elementless attacker)
[x] Integration tests  — SwapExecution → DamagePipeline → BossState HP update
[ ] Gameplay scenarios — full state-transition chain (swap → match → resources → damage → boss HP)
[ ] API tests          — N/A (no REST endpoint for damage)
[ ] Realtime tests     — N/A (events are Domain values; wire delivery is a protocol task)
[ ] Persistence tests  — N/A (no DB change)
```

### Key Edge Cases

- `BaseDamagePool = 0` and `PlayerState.ATK = 0`: Base Damage = 0, Final Damage = 0, Boss HP unchanged
- Elementless attacker (null) against any defending Element: Neutral (1.00×) per ELEMENT_RULES.md §3
- Final Damage exceeds Boss HP: Boss HP clamped to 0, not negative
- Combo = 1 (first Match of a Swap): 1.00× multiplier (no bonus)
- Boss DEF = 0: Mitigated Damage = Pre-Defense × (K / K) = Pre-Defense (no mitigation)
- Boss DEF extremely high: Mitigated Damage approaches 0 but never reaches it (formula asymptote)

---

## Documentation Impact

**Option A — None:**
> This task implements already-documented behavior. The Damage Pipeline formula is fully specified in COMBAT_RULES.md §3, Element Modifiers in ELEMENT_RULES.md §2.2, Combo multipliers in GAME_RULES.md §5, and Damage Events in GAME_EVENTS.md §2. No doc changes required.

---

## Stop Conditions

- If the required behavior cannot be fully derived from the Authoritative Sources listed above: STOP per `AGENTS.md §7`
- If `ElementMatchups.Resolve` or `ElementModifiers` has a bug or ambiguity: STOP and report per `AGENTS.md §4`
- If the BossState contract does not support HP mutation (e.g. immutable with no `with` expression): STOP and report per `AGENTS.md §20`

---

## Dependencies

- TASK-016 (DONE) — PlayerState has ATK, DEF, Crit
- TASK-018 (DONE) — ResourceGeneration carries BaseDamagePool, DefensePool
- TASK-019 (DONE) — ResourceGenerator.ApplyHeal resolves player effects
- TASK-020 (DONE) — BossState record with HP, DEF, Element
- TASK-020A (DONE) — Boss definition contract (Element, stats per Boss)
- TASK-021A (DONE) — Damage events SignalR contract resolution

---

## Completion Evidence

### Summary

Implemented the Damage Pipeline (`GAME_RULES.md` §17 steps 15–17,
`COMBAT_RULES.md` §3) as a new Domain `Combat` module, and integrated it into
the existing `BattleStateService.ExecuteSwap()` resolution flow so that a
committed Swap reduces `BossState.HP`.

The pipeline runs the six documented steps in the documented order — Base
Damage, Combo Modifier, Other Modifiers (pass-through `1.00×`), Element
Modifier, Defense Mitigation, Final Damage — applies the truncated result to
Boss HP clamped at `0`, and produces the three `GAME_EVENTS.md` §2 reports
(`DamageCalculated`, `DamageDealt`, `DamageTaken`).

A documentation conflict blocking the SignalR delivery of the three Damage
events was detected during implementation and reported rather than resolved
unilaterally (`AGENTS.md` §4). It was closed by **TASK-021A**, which extended
`SIGNALR_PROTOCOL.md` §3.2.2 and added §3.2.13–§3.2.15. TASK-021 then completed
against that resolved contract.

### Changes

**Created**

- `src/backend/GameServer.Domain/Combat/ComboModifiers.cs` — the Combo
  Modifier table of `GAME_RULES.md` §5. The five factors are stored as integer
  numerators over `Denominator = 100`, following the `ElementModifiers`
  precedent for configuration values, so the step-2 multiplication is exact
  integer arithmetic and the only place a fraction is discarded is the
  documented truncation of §3 step 6.
- `src/backend/GameServer.Domain/Combat/DamageEvents.cs` — the Domain event
  values: `DamageParty`, `DamageCalculation` (the six-member breakdown),
  `DamageDealtEvent`, `DamageTakenEvent`, `DamageEvents`, and `DamageResult`.
- `src/backend/GameServer.Domain/Combat/DamagePipeline.cs` — the six-step
  pipeline, the named `DefenseMitigationConstant = 100`
  (`COMBAT_RULES.md` §3.2), the `NoOtherModifiers = 1.00` pass-through
  (§3 step 4), `DamageInputs`, and the Boss HP write.
- `tests/backend/GameServer.Domain.Tests/DamagePipelineTests.cs` — 60 focused
  Domain tests.

**Modified**

- `src/backend/GameServer.Domain/Match3/BattleEvent.cs` — added the three
  `BattleEventType` members, their payload slots, the public
  `ForDamageCalculated`/`ForDamageDealt`/`ForDamageTaken` factories, and their
  throwing accessors.
- `src/backend/GameServer.Application/Battle/BattleStateService.cs` —
  `ExecuteSwap()` now calls `DamagePipeline.Calculate` after
  `PassiveTracker.Charge`, merges the returned `BossState` into the same single
  post-resolution write-back, and appends the three Damage events in the
  documented order.
- `tests/backend/GameServer.Application.Tests/BattleStateServiceTests.cs` —
  updated the event-order assertion for the two now-documented trailing stages,
  and added 8 integration tests covering `ExecuteSwap → pipeline → Boss HP`.
- `tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs` — updated
  the two stage-boundary assertions that named the Damage events as "not yet
  implemented".

**Not created** (deliberately, per `AGENTS.md` §9): no `BattleResolutionService`,
`BattleStateManager`, `GameStateManager`, `UniversalStateStore`, or
`CombatEngine`. No modifier chain, registry, or interface was introduced for
step 4 — the extension point is the stage itself.

### Damage Pipeline

```text
Step 1  Base        = PlayerState.ATK + ResourceGeneration.BaseDamagePool   §3 step 1
Step 2  → Combo     × GAME_RULES.md §5 row for PlayerState.Combo            §3 step 2
Step 3  → Element   × ElementModifiers.Default.For(matchup)                 §3 step 3, ELEMENT §2.2
Step 4  → Other     × 1.00 (pass-through, reported in the breakdown)        §3 step 4
Step 5  → Defense   × (100 / (100 + BossState.DEF))                         §3.2
Step 6  → Final     Math.Truncate, minimum 0                                §3 step 6
        → Boss HP   max(HP − FinalDamage, 0)                                §3 step 6, §1.4
```

The matchup is resolved by the existing `ElementMatchups.Resolve` (attacker =
`PetState.Element`, defender = `BossState.Element`); an elementless attacker
resolves Neutral (`ELEMENT_RULES.md` §3). `PlayerState.Crit` is deliberately
never read — no Crit roll exists (§3.3).

### Application Integration

`BattleStateService.ExecuteSwap()` resolves in this order, unchanged up to
step 10:

```text
SwapExecutor.Execute(state, request)        steps 1–9, one write-back
        ↓
PassiveTracker.Charge(...)                  step 10
        ↓
DamagePipeline.Calculate(...)               steps 15–17  ← TASK-021
        ↓
resolved with { BossState = ... }           step 17 HP write, same write-back
        ↓
DamageCalculated / DamageDealt / DamageTaken appended   ← TASK-021
        ↓
WithEvents(...).WithState(resolved)         the single stored value
```

The boundary supplies the values the earlier stages already produced
(`PlayerState.ATK`, `Resources.BaseDamagePool`, `PlayerState.Combo`,
`PetState.Element`, `BossState.Element`, `BossState.DEF`) and decides no part of
the formula. `DamagePipeline.Calculate` returns the whole `BossState` as a new
value, so the HP write cannot split the `GAME_STATE.md` §5.1 write-back in two.
A rejected Swap returns before the pipeline is reached, so it deals no damage
and emits no event.

### Boss HP

```text
newHp = max(currentHp − finalDamage, 0)
```

Applied by the Domain pipeline and carried in the same `BattleState` value the
executor already produced. Boss HP is never negative: overkill clamps at `0`
(`500 − 600 = 0`). Every other Boss field — `BossId`, `Element`, `MaxHP`,
`ATK`, `DEF`, `State` — is carried across unchanged. No Victory/Defeat is
derived from `HP == 0` and no Boss State transitions.

### Events

```text
DamageCalculated   Base, ComboModifier, ElementModifier, OtherModifiers,
                   Defense, FinalDamage        (GAME_EVENTS.md §2)
DamageDealt        Source, Target, Amount      (GAME_EVENTS.md §2)
DamageTaken        Source, Target, Amount      (GAME_EVENTS.md §2)
```

Emitted in the `GAME_EVENTS.md` §1 order — `DamageCalculated`, `DamageDealt`,
`DamageTaken` — after the Passive stage's reports and before the not-yet-
implemented `BossSkillCast`/`TurnEnded`. `DamageDealt` and `DamageTaken` carry
the same amount, which is exactly the change Boss HP shows: one application of
damage, two reports.

### SignalR Contract

TASK-021A extended `SIGNALR_PROTOCOL.md` §3.2.2 to a seven-value closed
discriminator set and added §3.2.13–§3.2.15. `BattleEventWireProjection`
projects the three events onto that schema and `ReceiveEvents` delivers them
through the existing batch — no new method, envelope, or transport:

```json
{ "type": "DamageCalculated", "base": 100, "comboModifier": 1.5,
  "elementModifier": 1.5, "otherModifiers": 1.0,
  "defense": 68.57142857142857, "finalDamage": 68 }

{ "type": "DamageDealt", "source": "player", "target": "boss", "amount": 68 }

{ "type": "DamageTaken", "source": "player", "target": "boss", "amount": 68 }
```

`DamageParty` is projected to its lowercase documented name; `base` is carried
by a factory parameter named `baseDamage` with `[JsonPropertyName("base")]`
because `base` is a C# keyword. No member outside the §3.2 tables is emitted.

### Tests

```text
Baseline (before TASK-021)
  Domain 732 / Application 53 / Infrastructure 1 / Api 51 = 837, 0 failed
  Build: 0 warnings, 0 errors

Focused
  DamagePipelineTests                       60 passed / 0 failed
  BattleStateServiceTests (Damage group)     6 passed / 0 failed
  BattleEventEmissionTests                  46 passed / 0 failed
  ApiIntegrationTests (SignalR boundary)     3 passed / 0 failed

Full backend (dotnet test src/backend/GameServer.sln)
  Domain            792 passed / 0 failed
  Application        61 passed / 0 failed
  Infrastructure      1 passed / 0 failed
  Api                51 passed / 0 failed
  Total             905 passed / 0 failed

Frontend (src/frontend/client, vitest run)
  12 files, 174 passed / 0 failed

Build (dotnet clean + dotnet build src/backend/GameServer.sln)
  0 Warning(s), 0 Error(s)
```

New coverage: Base Damage (ATK + pool, zero/zero, the DEF pool proven not to be
an input), every `GAME_RULES.md` §5 Combo row including `5+` and the
no-row-below-1 rejection, all five Advantage and five Disadvantage pairings plus
equal-Element Neutral and elementless on either side, the `1.00×` step-4
pass-through, `K = 100`, `DEF = 0`, the high-DEF asymptote, truncation
(`96 → 68.57 → 68`, explicitly not `69`), the `HP 500 − 68 = 432` and overkill
`→ 0` cases, every other Boss field carried unchanged, determinism, the full
breakdown, event source/target/amount agreement with the applied HP delta, and
caller-supplied Combo/Element configuration.

### Regression

All previously passing tests remain green. Two Domain assertions and one
Application assertion were updated rather than deleted: each was a
stage-boundary list that explicitly named the Damage events as "not yet
implemented", and each now names them as implemented. No assertion was
weakened, no failure was suppressed, and `BattleEventWireProjection` was not
bypassed.

### Documentation Consulted

- `AGENTS.md`, `docs/AGENTS.md`
- `docs/00-overview/MVP_SCOPE.md` §1, §2
- `docs/01-game-design/GAME_RULES.md` §1.4, §5, §8, §14, §16, §17, §18
- `docs/01-game-design/COMBAT_RULES.md` §1.1, §1.2, §2, §3, §3.1, §3.2, §3.3, §4, §5, §7
- `docs/01-game-design/ELEMENT_RULES.md` §1, §1.1, §1.2, §2, §2.1, §2.2, §3, §5, §6
- `docs/01-game-design/BOSS_RULES.md` §1, §3–§5, §6.1
- `docs/02-technical/GAME_STATE.md` §0, §2, §2.2, §2.3, §2.4, §3, §5.1
- `docs/02-technical/GAME_EVENTS.md` §1, §1.1, §1.2, §2, §3
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3, §3.2, §3.2.1–§3.2.15, §4
- `docs/02-technical/ARCHITECTURE.md` §1, §2.1, §3, §4.1, §5
- `docs/02-technical/TDD.md`
- `tasks/completed/TASK-021A-resolve-damage-events-signalr-contract.md`

### Documentation Changed

**None by TASK-021.** The owning documents already specified the formula, the
modifiers, the ordering, and the event semantics, so this task changed no
documentation. `SIGNALR_PROTOCOL.md` was extended by **TASK-021A**, not by this
task, and this task's implementation matches the result. The `docs/` files in
the working tree predate this session and belong to TASK-017/020A.

Verified consistent after implementation:

```text
GAME_RULES.md §5        Combo 1.00 / 1.10 / 1.20 / 1.35 / 1.50   == ComboModifiers.Default
COMBAT_RULES.md §3.2    K = 100                                  == DefenseMitigationConstant
ELEMENT_RULES.md §2.2   1.50 / 1.00 / 0.75                       == ElementModifiers.Default
GAME_EVENTS.md §1       DamageCalculated, DamageDealt, DamageTaken == emitted order
GAME_STATE.md §2.4      BossState.HP is the value written        == applied write
SIGNALR_PROTOCOL.md     §3.2.2 seven-value set, §3.2.13–§3.2.15  == projection output
```

### Validation

```text
Base Damage = ATK + BaseDamagePool:            YES  (80 from 50 + 30)
Combo Modifier (GAME_RULES.md §5):             YES  (all rows, 5+ included)
Other Modifiers (pass-through 1.00×):          YES  (present and reported)
Element Modifier (ELEMENT_RULES.md §2.2):      YES  (1.50 / 1.00 / 0.75)
Elementless attacker → Neutral:                YES
Defense Mitigation (K = 100, COMBAT_RULES §3.2): YES
Truncation toward zero (96 → 68.57 → 68):      YES  (not 69)
Boss HP applied and clamped ≥ 0:               YES  (432; 500 − 600 → 0)
DamageCalculated full breakdown:               YES  (six documented members)
DamageDealt / DamageTaken:                     YES  (source, target, amount)
SignalR delivery via existing ReceiveEvents:   YES  (3 boundary tests pass)

Crit roll:                                     NOT IMPLEMENTED
Relic damage modifiers:                        NOT IMPLEMENTED
Passive damage modifiers:                      NOT IMPLEMENTED
Buff / Debuff:                                 NOT IMPLEMENTED
Shield:                                        NOT IMPLEMENTED
Boss Attack / Skill / Response:                NOT IMPLEMENTED
Victory / Defeat:                              NOT IMPLEMENTED

New SignalR method / envelope:                 NONE
Redis changes:                                 NONE
PostgreSQL changes:                            NONE
REST endpoint changes:                         NONE
Frontend changes:                              NONE
New ADR:                                       NONE
```

### Remaining Issues

- The Defense Pool that `ResourceGeneration` generates is still consumed by no
  documented step: `COMBAT_RULES.md` §3.2's mitigation reads the *defender's*
  DEF, and no rule places the attacker's DEF pool in the Damage Pipeline. Left
  unimplemented rather than guessed (`AGENTS.md` §7). Suggested follow-up: a
  design task deciding whether the DEF pool has a consumer.
- `docs/02-technical/GAME_STATE.md` §2.2 still contains a stale cross-reference
  claiming `BossState` "still does not exist" (it does, since TASK-020). Benign
  and pre-existing; reported rather than silently edited (`AGENTS.md` §17).
  Suggested follow-up: a documentation-consistency task.
- `BOSS_RULES.md` §6.1 records two further MVP Bosses as not content-defined
  against `GAME_RULES.md` §19's five-Boss scope. Pre-existing; suggested
  follow-up: a Boss content-authoring task.

### Agent

gameplay (Domain implementation), backend (Application integration),
testing (Domain, Application, and API boundary tests)

### Workflow Used

`.ai/workflow/development/feature.md` (documentation-first: task → docs →
existing implementation → plan → implement → test → review → report)

### Status

DONE

---

## Handoff

None — the task completed without a mid-execution handoff.

The next unit of work is `GAME_RULES.md` §17 step 18 (Boss Response) and its
prerequisites — the Boss Passive and Boss Skill systems (`BOSS_RULES.md` §3–§4),
which are unimplemented. Step 19 (Victory/Defeat) follows.
