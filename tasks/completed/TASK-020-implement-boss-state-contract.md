# TASK-020 — Implement BossState Contract

---

## Metadata

```text
Task ID:           TASK-020
Type:              FEATURE
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     backend
Supporting Agents: gameplay, testing
Workflow:          development/feature.md
Skills:            dotnet-backend-patterns
Dependencies:      TASK-016 (combat stats in PlayerState — DONE),
                   TASK-019 (Resolve Player Effects — DONE),
                   TASK-020A (Resolve Boss State & MVP Boss Configuration Contract — DONE)
Blockers:          None
```

---

## Objective

Define and implement the `BossState` record in the Domain layer per `GAME_STATE.md §2.4`, add it to `BattleState`, and provide MVP boss configuration values — establishing the state foundation required by the Damage Pipeline (steps 15–17 of `GAME_RULES.md §17`). Also add the `Element` field to `PetState` per `GAME_STATE.md §2.3`, which is documented but not yet implemented and is required for the Element Modifier calculation (`ELEMENT_RULES.md §2.2`).

---

## Context

Steps 1–14 of the battle resolution pipeline are implemented. The next step is Step 15 (Calculate Damage), which is blocked because:

1. **`BossState` does not exist** — `GAME_STATE.md §2.4` defines the contract (`BossId`, `Element`, `HP/MaxHP/ATK/DEF`, `State`, `PassiveProgress`, `StatusEffects[]`) but no code implements it. `BattleState` (`BattleState.cs`) has no `BossState` field.
2. **`PetState.Element` is missing** — `GAME_STATE.md §2.3` documents `Element` as a `PetState` field, but the current `PetState` record only carries `PassiveId`, `PassiveProgress`, `PassiveResetOverride` (see `PetState.cs` line 27: "not yet implemented, not not required").

The Damage Pipeline (`COMBAT_RULES.md §3`) requires:
- `BossState.DEF` — Defense Mitigation formula (`K / (K + DEF)`)
- `BossState.HP` — Apply Final Damage (HP reduction, win/loss check)
- `BossState.Element` — Element Modifier (`ElementMatchups.Resolve(attacker, defender)`)
- `PetState.Element` — Element Modifier (attacker's element, from the active Pet)

Without these fields, no damage can be calculated, no Boss can take damage, and the battle cannot end.

---

## Authoritative Sources

- `docs/00-overview/MVP_SCOPE.md` §1 — confirm Boss is IN scope
- `docs/01-game-design/GAME_RULES.md` §1.1, §15 — Boss rules, one Boss per battle
- `docs/01-game-design/COMBAT_RULES.md` §1.2 — Boss stats (HP, MaxHP, ATK, DEF, Element)
- `docs/01-game-design/BOSS_RULES.md` §1, §5, §6 — Boss structure, Boss State enum, MVP boss reference
- `docs/01-game-design/ELEMENT_RULES.md` §2.2 — Element Modifier lookup
- `docs/02-technical/GAME_STATE.md` §2.4 — BossState contract (full field list)
- `docs/02-technical/GAME_STATE.md` §2.3 — PetState (Element field, documented but absent)
- `docs/02-technical/GAME_EVENTS.md` — BattleStarted event requires Pet and Boss
- `docs/02-technical/API_CONTRACTS.md` §3 — POST /api/battle/start (already has bossId)
- `docs/02-technical/ARCHITECTURE.md` — Domain layer structure

---

## Scope

### In Scope

- `BossState` readonly record struct in `GameServer.Domain/Battle/`
  - Fields: `BossId` (string), `Element` (Element), `HP` (int), `MaxHP` (int), `ATK` (int), `DEF` (int), `State` (BossStateKind enum)
- `BossStateKind` enum with **four** members: `Idle`, `Charging`, `Enraged`, `Stunned` (per `BOSS_RULES.md §1`)
- `BossState` added as a field on `BattleState` record
- `BattleState.Create` updated to accept `BossState` parameter
- `PetState.Element` field added (documented in `GAME_STATE.md §2.3`, currently absent)
- `BattleState.Create` updated to pass `Element` to `PetState.AtBattleCreation`
- `bossId` validation added to `API_CONTRACTS.md §3`
- Static boss configuration definitions for all three content-defined MVP bosses (Hỏa Long, Thủy Ma, Mộc Yêu) with Element assignments and approved base stats (HP=5000, ATK=100, DEF=50 per `BOSS_RULES.md §6.1`)
- `BossState.Initial()` factory method returning `HP = MaxHP`, `State = Idle`

### Out of Scope

- Boss Passive mechanics (`BOSS_RULES.md §3`) — triggers, charge, threshold, effects
- Boss Skill mechanics (`BOSS_RULES.md §4`) — timing, targeting, damage application
- Boss State machine transitions (multi-phase behavior)
- `StatusEffects[]` on BossState — owned by the Status Effects system, "not yet implemented, not not required"
- `PassiveProgress` on BossState — owned by the Boss Passive system, "not yet implemented, not not required"
- Damage Pipeline implementation (Steps 15–17) — separate task
- Boss Response (Step 18) — separate task
- Wire delivery of BossState (`SIGNALR_PROTOCOL.md`) — separate task
- Redis persistence of BossState (`REDIS_STATE.md`) — separate task
- Any system listed in `docs/00-overview/MVP_SCOPE.md` §2 (OUT)

---

## Current State

- `BattleState.cs`: `sealed record` with `BattleId`, `Turn`, `Sequence`, `RngSeed`, `RngState`, `BoardState`, `PlayerState`, `PetState`, `LastCommittedSwapPair`. No `BossState`.
- `PlayerState.cs`: `readonly record struct` with `HP`, `MaxHP`, `ATK`, `DEF`, `Power`, `Crit`, `Combo`, `MatchCount`. Has `Initial` factory.
- `PetState.cs`: `readonly record struct` with `PassiveId`, `PassiveProgress`, `PassiveResetOverride`. Has `AtBattleCreation` factory. Missing `Element`.
- `Element.cs`: enum with `Moc`, `Tho`, `Thuy`, `Hoa`, `Kim`.
- `ElementMatchups.cs`: `Resolve(Element? attacker, Element? defender)` — accepts `null` on either side (elementless = neutral).
- `ElementModifiers.cs`: configurable factors (1.50 / 1.00 / 0.75).
- No `BossState` type, no boss configuration, no `GameServer.Domain/Bosses/` directory.
- `ResourceGenerator.cs`: `BaseDamagePool` and `DefensePool` are generated but consumed by nothing (damage pipeline not implemented).
- `BattleStateService.CreateBattle` currently accepts `PassiveConfiguration` but no boss configuration.

---

## Resolved Contracts

### BossState Enum (Canonical)

Per `BOSS_RULES.md §1` (authoritative source of truth):

```text
Idle
Charging
Enraged
Stunned
```

`COMBAT_RULES.md §1.2` previously used "ChargingSkill" — corrected to "Charging" per `TASK-020A`.

### Boss Initialization Path

```text
1. Client sends bossId in POST /api/battle/start (already documented in API_CONTRACTS.md §3)
2. Application layer resolves bossId → Boss definition (Element, stats, PassiveId, SkillId)
3. BossState is created from the definition via BossState.Initial(bossId, element, maxHp, atk, def)
4. BattleState.Create accepts BossState (like it accepts PetState)
```

No new endpoint, SignalR method, or Redis behavior is required.

### PetState.Element

Already documented in `GAME_STATE.md §2.3` and `PET_RULES.md §1`. No documentation change needed — consistent and correct. Will be implemented by TASK-020.

### Staged BossState Fields

```text
Implement now:
  BossId, Element, HP, MaxHP, ATK, DEF, State

Deferred (same convention as PlayerState):
  PassiveProgress — owned by Boss Passive system ("not yet implemented, not not required")
  StatusEffects[] — owned by Status Effects system ("not yet implemented, not not required")
```

### Configuration Ownership

Boss configuration (stat definitions per Boss) belongs in the Domain `Bosses/` module, alongside the `BossId` identity type. The smallest existing architectural location is `GameServer.Domain/Bosses/` — a new file `BossDefinition.cs` holding the MVP stat records. No large abstractions (registry, service, database) are needed for MVP.

---

## Approved Boss Base Stats (Project-Owner Decision)

Per `BOSS_RULES.md §6.1`:

```text
Boss        Element   HP / MaxHP   ATK   DEF
---------   -------   ----------   ---   ---
Hỏa Long    Hỏa       5000         100   50
Thủy Ma     Thủy      5000         100   50
Mộc Yêu     Mộc       5000         100   50
```

These are MVP base configuration — not universal balance invariants. The
project owner approved these values. Boss configuration definitions will use
these as static defaults at battle creation.

---

## Acceptance Criteria

- [x] `BossState` readonly record struct exists in `GameServer.Domain/Battle/` with fields: `BossId` (string), `Element` (Element), `HP` (int), `MaxHP` (int), `ATK` (int), `DEF` (int), `State` (BossStateKind)
- [x] `BossStateKind` enum has exactly four members: `Idle`, `Charging`, `Enraged`, `Stunned` (per `BOSS_RULES.md §1`)
- [x] `BossState` has `Initial` static factory method matching `PlayerState.Initial` pattern (returns `HP = MaxHP`, `State = Idle`)
- [x] `BattleState` includes `BossState BossState` as a record parameter
- [x] `BattleState.Create` factory methods accept `BossState` and pass it through
- [x] `PetState` includes `Element Element` as a record parameter
- [x] `PetState.AtBattleCreation` accepts `Element` parameter
- [x] `BossId` identity type follows `PassiveId` pattern (`readonly record struct` wrapping string)
- [x] Boss configuration definitions exist for Hỏa Long (Hỏa), Thủy Ma (Thủy), Mộc Yêu (Mộc) — Element assignments and approved base stats per `BOSS_RULES.md §6.1`
- [x] All existing tests pass (837 tests, 0 warnings)
- [x] New unit tests cover: `BossState.Initial`, `BattleState.Create` with BossState, `PetState.AtBattleCreation` with Element
- [x] `TASK-020A` documentation changes are complete (stale terminology corrected)
- [x] No "ChargingSkill" references remain in `docs/`

---

## Affected Areas

```text
[x] Domain (GameServer.Domain/) — Battle/ module (BossState, BattleState, PetState)
[ ] Application (GameServer.Application/)
[ ] API (GameServer.Api/)
[ ] Client (client/)
[ ] SignalR / Redis (GameServer.Infrastructure/SignalR/, /Redis/)
[ ] PostgreSQL (GameServer.Infrastructure/Postgres/)
[x] Tests (tests/) — unit tests for new types and updated factories
[x] Documentation (docs/) — TASK-020A contract resolution
[ ] ADR (docs/03-decisions/ADR/)
```

---

## Implementation Notes

1. **Follow existing patterns.** `BossState` should mirror `PlayerState` in structure: `readonly record struct` with an `Initial` factory. `BattleState.Create` should accept `BossState` like it accepts `PetState`.

2. **BossState belongs in `Battle/`, not `Bosses/`.** The `Battle/` directory contains state records (`BattleState`, `PlayerState`, `PetState`) that `BattleState` references. `BossState` is a state record. The `Bosses/` directory (per `ARCHITECTURE.md`) is for boss mechanics (Passive, Skill) and boss configuration definitions, not state.

3. **Boss base stats are approved.** `BOSS_RULES.md §6.1` documents the project-owner-approved MVP defaults: HP=5000, ATK=100, DEF=50 for all three content-defined bosses. These are configuration, not invariants.

4. **`PetState.Element` is a one-line addition.** Add `Element Element` parameter to the `PetState` record and `AtBattleCreation` factory. Update all call sites that construct `PetState` (currently: `BattleState.Create` and tests).

5. **Do not implement `PassiveProgress` or `StatusEffects[]` on BossState.** These are documented in `GAME_STATE.md §2.4` but owned by later systems (Boss Passive, Status Effects). They are "not yet implemented, not not required" — same staging convention as `PlayerState` fields. Leave them for their owning tasks.

6. **`BossStateKind` enum should use all four states.** `BOSS_RULES.md §1` defines: `Idle`, `Charging`, `Enraged`, `Stunned`. `COMBAT_RULES.md §1.2` has been corrected to match. Full state machines are explicitly deferred to Future Expansion (`BOSS_RULES.md §5 item 3`).

7. **Element Modifier depends on both `PetState.Element` and `BossState.Element`.** The `ElementMatchups.Resolve(attacker, defender)` function already accepts `null` on either side (neutral matchup). Adding `Element` to both PetState and BossState makes the modifier calculable. This task does NOT implement the modifier calculation — that is owned by the Damage Pipeline task.

8. **`BossId` identity type.** Follow the same pattern as `PassiveId` (`readonly record struct` wrapping a `string`). This is an identity, not a definition — the boss's definition (stats, passive, skill) lives in boss configuration, not in the identity wrapper.

9. **Configuration ownership.** MVP Boss definitions belong in `GameServer.Domain/Bosses/BossDefinition.cs` — a static configuration file. No registry, service, or database abstraction is needed for MVP.

---

## Testing Requirements

### Test Types Required

```text
[x] Unit tests         — BossState.Initial, BattleState.Create with BossState,
                         PetState.AtBattleCreation with Element, BossStateKind enum members
[ ] Integration tests  — N/A (no new boundary crossings)
[ ] Gameplay scenarios — N/A (damage pipeline not yet implemented)
[ ] API tests          — N/A (no new endpoints)
[ ] Realtime tests     — N/A (no wire changes)
[ ] Persistence tests  — N/A (no schema changes)
```

### Key Edge Cases

- `BossState.Initial` produces valid starting values (HP = MaxHP, State = Idle)
- `BattleState.Create` with all parameters including `BossState` and `PetState` with Element
- `ElementMatchups.Resolve(PetState.Element, BossState.Element)` returns correct matchup for each MVP boss pairing
- All 806 existing tests continue to pass after adding `BossState` to `BattleState` and `Element` to `PetState`
- `BossStateKind` enum contains exactly 4 members (Idle, Charging, Enraged, Stunned)

---

## Documentation Impact

This task implements already-documented behavior (`GAME_STATE.md §2.4`, `§2.3`). TASK-020A resolves the terminology conflicts and documentation gaps. No additional doc changes beyond TASK-020A are required for this task.

---

## Stop Conditions

- If `GAME_STATE.md §2.4` BossState contract is ambiguous about a field's type or semantics: STOP per `AGENTS.md §7`
- If adding `BossState` to `BattleState` breaks existing tests in a way that suggests an architectural conflict: STOP per `AGENTS.md §20`

---

## Dependencies

- TASK-016 (combat stats in PlayerState) — DONE
- TASK-019 (Resolve Player Effects) — DONE
- TASK-020A (Resolve Boss State & MVP Boss Configuration Contract) — DONE

---

## Completion Evidence

<!-- TO BE FILLED BY THE AGENT after the task reaches DONE. -->

### Summary

Implemented the `BossState` contract (`GAME_STATE.md` §2.4) as the Domain-layer
state foundation, plus the `PetState.Element` field (§2.3) and the static MVP
Boss configuration (`BOSS_RULES.md` §6.1).

Created `BossStateKind` (the canonical four-member enum `Idle` / `Charging` /
`Enraged` / `Stunned`), `BossState` (`readonly record struct` with an `Initial`
factory), `BossId` (identity value, following the `PassiveId` pattern),
`BossDefinition` (configuration) and `BossDefinitions` (the three
content-defined MVP Bosses). Added `Element` to `PetState` and `BossState` to
`BattleState`, updating every creation factory and call site.

No Boss mechanic, Damage Pipeline, or infrastructure change is included.

### Changes

**Created**

- `src/backend/GameServer.Domain/Battle/BossStateKind.cs` — the canonical
  four-member State enum per `BOSS_RULES.md` §1. No `ChargingSkill`.
- `src/backend/GameServer.Domain/Battle/BossState.cs` — `readonly record struct
  BossState(BossId, Element, HP, MaxHP, ATK, DEF, BossStateKind)` with
  `Initial(...)`, the `InitialState` constant, and a derived `IsIdle`.
- `src/backend/GameServer.Domain/Bosses/BossId.cs` — identity value wrapper,
  same shape as `PassiveId`.
- `src/backend/GameServer.Domain/Bosses/BossDefinition.cs` — the configuration
  record (`BossId`, `Element`, `MaxHP`, `ATK`, `DEF`) plus
  `ToInitialState()`.
- `src/backend/GameServer.Domain/Bosses/BossDefinitions.cs` — the static MVP
  definitions with the approved values.
- `tests/backend/GameServer.Domain.Tests/BossStateTests.cs` — focused Domain
  contract tests.

**Modified**

- `src/backend/GameServer.Domain/Battle/PetState.cs` — added `Element` as the
  first record parameter; `AtBattleCreation` now takes it.
- `src/backend/GameServer.Domain/Battle/BattleState.cs` — added the
  `BossState BossState` record parameter; updated `Create`, `CreateWith`, and
  `DefaultPassive`; updated the type's stage documentation.
- `src/backend/GameServer.Application/Battle/BattleStateService.cs` — renamed
  `PassiveConfiguration` to `PetConfiguration` (it now carries the Pet's
  Element too), added `CreateBattle(..., PetConfiguration, BossDefinition)`,
  renamed the registry and accessor accordingly.
- `tests/backend/GameServer.Domain.Tests/BattleStateTests.cs`,
  `CascadeAndDeterminismTests.cs` — updated the stage-boundary field
  assertions to include the newly documented `BossState`/`Element` members.
- `tests/backend/GameServer.Application.Tests/BattleStateServiceTests.cs`,
  `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs` — updated call
  sites.

**Not created** (deliberately, per `AGENTS.md` §9): no `BossRegistry`,
`BossService`, `BossRepository`, `BossDatabase`, or `BossFactory`.

### Tests

```text
Baseline (before changes)
  Domain 701 / Application 53 / Infrastructure 1 / Api 51 = 806, 0 failed
  Build: 0 warnings, 0 errors

Focused (GameServer.Domain.Tests, after changes)
  Passed: 732, Failed: 0

Full backend (dotnet test src/backend/GameServer.sln)
  Domain         732 passed / 0 failed
  Application     53 passed / 0 failed
  Infrastructure   1 passed / 0 failed
  Api             51 passed / 0 failed
  Total          837 passed / 0 failed

Build (dotnet clean + dotnet build src/backend/GameServer.sln)
  0 Warning(s), 0 Error(s)
```

New coverage: `BossState.Initial` (`HP == MaxHP`, `State == Idle`, approved
stats preserved, no stat derivation, readonly value), the `BossStateKind` set
(exactly four members, no `ChargingSkill`), the deferred-field and
no-mechanics checks, `BossId` as an identity, all three MVP definitions with
their Elements and `5000 / 5000 / 100 / 50` stats, `BattleState.Create`
preserving `BossState` and requiring a Boss, and
`ElementMatchups.Resolve(PetState.Element, BossState.Element)` across every
MVP Pet × Boss pairing.

Five pre-existing tests failed on first run — all were stage-boundary
assertions whose lists named `BossState` / `Element` as "not yet implemented".
They were updated because the field is now implemented exactly as `GAME_STATE.md`
§2.3/§2.4 specifies; no assertion was weakened to make incorrect behavior pass
(`AGENTS.md` §15).

### Documentation Consulted

- `AGENTS.md`, `.ai/README.md`, `.ai/agents/orchestrator.md`,
  `.ai/agents/backend.md`, `.ai/agents/testing.md`, `.ai/agents/review.md`
- `docs/00-overview/MVP_SCOPE.md` (Boss is IN scope)
- `docs/01-game-design/GAME_RULES.md` §1.1, §15, §17
- `docs/01-game-design/BOSS_RULES.md` §1, §3, §4, §5, §6, §6.1
- `docs/01-game-design/COMBAT_RULES.md` §1.1, §1.2, §3, §3.2
- `docs/01-game-design/ELEMENT_RULES.md` §1, §1.1, §1.2, §2, §5, §6
- `docs/01-game-design/PET_RULES.md` §2
- `docs/01-game-design/PASSIVE_RULES.md` §1, §4, §8
- `docs/02-technical/GAME_STATE.md` §0, §2, §2.0, §2.0.3, §2.0.5, §2.1.10,
  §2.2, §2.3, §2.4, §5.1
- `docs/02-technical/API_CONTRACTS.md` §3
- `docs/02-technical/ARCHITECTURE.md` §1, §2.1, §5
- `docs/02-technical/GAME_EVENTS.md` §2
- `docs/02-technical/REDIS_STATE.md` §7

### Documentation Changed

**None.** All four documentation files the task asks to verify
(`BOSS_RULES.md`, `COMBAT_RULES.md`, `GAME_STATE.md`, `API_CONTRACTS.md`) were
already correct after TASK-020A and remain consistent with the implementation:

- Boss states are `Idle / Charging / Enraged / Stunned` in `BOSS_RULES.md` §1,
  `COMBAT_RULES.md` §1.2, and `GAME_STATE.md` §2.4 — matching
  `BossStateKind`. Searched `docs/` for `ChargingSkill`: **0 results**.
- `BOSS_RULES.md` §6.1 lists Hỏa Long (Hỏa), Thủy Ma (Thủy), Mộc Yêu (Mộc) at
  `5000 / 100 / 50`, Initial State `Idle` — matching `BossDefinitions`.
- `API_CONTRACTS.md` §3 already documents `bossId` validation ("must be a valid
  MVP Boss — BOSS_RULES.md §6"), so the in-scope item needed no doc change.

The working tree's `docs/` modifications predate this task (they are TASK-020A
and TASK-017's uncommitted edits); this task did not write to any file under
`docs/`.

### Validation

```text
BossState implemented:                     YES
BossStateKind (4 canonical states):        YES  (Idle, Charging, Enraged, Stunned)
BossState.Initial:                         YES  (HP == MaxHP, State == Idle)
PetState.Element implemented:              YES
BattleState.BossState implemented:         YES
BattleState.Create factories updated:      YES
MVP Boss definitions:
    Hỏa Long:                              YES  (Hỏa, 5000/5000/100/50, Idle)
    Thủy Ma:                               YES  (Thủy, 5000/5000/100/50, Idle)
    Mộc Yêu:                               YES  (Mộc, 5000/5000/100/50, Idle)

Boss Passive:                              NOT IMPLEMENTED
Boss Skill:                                NOT IMPLEMENTED
Boss State Machine / transitions:          NOT IMPLEMENTED
Enrage mechanics:                          NOT IMPLEMENTED
Stun mechanics:                            NOT IMPLEMENTED
Damage Pipeline (GAME_RULES.md §17 15–17): NOT IMPLEMENTED
Boss Response (§17 step 18):               NOT IMPLEMENTED
Victory / Defeat (§17 step 19):            NOT IMPLEMENTED
PassiveProgress on BossState:              NOT IMPLEMENTED (deferred)
StatusEffects[] on BossState:              NOT IMPLEMENTED (deferred)

SignalR changes:                           NONE
Redis changes:                             NONE
PostgreSQL changes:                        NONE
API endpoint changes:                      NONE
New endpoint / hub method:                 NONE
Documentation changes:                     NONE
New ADR:                                   NONE (no architecture change)
```

`BossState` carries exactly the seven documented fields; the two deferred §2.4
members are absent and unstubbed, following the repo's "not yet implemented —
not not required" convention. `BossState` is not a wire member: §2.4 adds
authoritative state, and delivery is a protocol change owned by its own task
(`SIGNALR_PROTOCOL.md` §4 item 4).

### Risks

- `BattleStateService.PassiveConfiguration` was renamed to `PetConfiguration`
  and gained the Pet's `Element`, and `CreateBattle` now requires a
  `BossDefinition`. This is an Application-internal API change with no
  production callers beyond DI registration (battle creation is not yet exposed
  through any endpoint or hub method), and no wire contract is affected.
- `BattleState.Create` signature arity changed, so downstream tasks must supply
  a Boss definition. That is the documented requirement
  (`GAME_EVENTS.md` §2 `BattleStarted` requires a Pet and a Boss), not a
  workaround.

### Remaining Issues

- `BOSS_RULES.md` §6.1 records that two further MVP Bosses are "not yet
  content-defined" against `GAME_RULES.md` §19's five-Boss scope. They are
  deliberately absent from `BossDefinitions` rather than invented. Suggested
  follow-up: a Boss content-authoring task.
- `GAME_STATE.md` §2.2 still states that "`BossState` (§2.4) still does not
  exist, so §2's full shape still cannot be produced and
  `POST /api/battle/start` still cannot create a battle". The first clause is
  now outdated. This is a benign stale cross-reference in a paragraph whose
  operative point (loadout systems do not exist, so the endpoint cannot create
  a battle) remains true, and this task was scoped not to modify documentation.
  Reported rather than silently edited, per `AGENTS.md` §17. Suggested
  follow-up: a documentation-consistency task.

### Agent

backend (implementation), testing (focused Domain tests)

### Workflow Used

`.ai/workflow/development/feature.md` (documentation-first: task → docs →
existing implementation → plan → implement → test → review → report)

### Skills Used

`dotnet-backend-patterns` (task-declared); `documentation-discovery`,
`scope-validation`, `impact-analysis`, `test-scenario-generation` applied per
`.ai/agents/backend.md` and `.ai/agents/testing.md`.

### Status

DONE

---

## Handoff

None — the task completed without a mid-execution handoff.

The Damage Pipeline (Steps 15–17) remains the next unit of work and is now
unblocked: `BossState.DEF` (mitigation, `COMBAT_RULES.md` §3.2),
`BossState.HP` (damage application and win/loss, `GAME_RULES.md` §1.4),
`BossState.Element` (Element Modifier, `ELEMENT_RULES.md` §2.2), and
`PetState.Element` (the attacker's Element) are all present in the Domain
model.
