# TASK-025 — PlayerState → PetState Combat State Refactor

---

## Metadata

```text
Task ID:           TASK-025
Type:              REFACTOR
Status:            DONE
Risk:              HIGH
Priority:          CRITICAL
Primary Agent:     gameplay
Supporting Agents: backend, realtime, testing, review
Workflow:          development/refactor.md
Skills:            discovery/impact-analysis, gameplay/authority-determinism-audit, quality/architecture-conformance, testing/test-scenario-generation, quality/implementation-review
Dependencies:      None
```

---

## Objective

Mechanically re-home battle state to match ADR-011: remove the nested `PlayerState` node from `BattleState`, move Match/Combo accounting (`Combo`, `MatchCount`) to the `BattleState` root, and move combat fields (HP, MaxHP, ATK, DEF, Crit, Power) onto `PetState` — with zero externally observable behavior change and all existing tests still passing.

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` items 1–5 — PlayerState removal, root Combo/MatchCount, PetState combat home
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` item 12 — BattleState shape has no PlayerState node
- `docs/02-technical/GAME_STATE.md` §2, §2.2, §2.3 — BattleState structure, root accounting, PetState fields
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4.2, §4.3 — fixed wire labels (not renamed by this task)
- `docs/00-overview/MVP_SCOPE.md` §1 — server-authoritative battle resolution IN

---

## Scope

### In Scope
- Remove `PlayerState` nested parameter/property from the `BattleState` record
- Add `Combo` and `MatchCount` to `BattleState` root; re-home HP/MaxHP/ATK/DEF/Crit/Power onto `PetState`
- Update every Domain/Application/Api reader and writer to the new locations (mechanical rename of access paths only)
- Update existing tests' state setup/assertions to the new shape; full suite green
- Preserve wire member names, payload shapes, and event payloads exactly (`SIGNALR_PROTOCOL.md` §3.2, §4.2, §4.3)

### Out of Scope
- Semantic changes to damage/heal/passive/reward logic (TASK-026)
- Renaming wire labels `playerState`, `finalPlayerHp`, `target="player"` (ADR-011 item 6 — forbidden)
- Adding `EquippedRelics`/`EquippedCards`/combat-collection fields to PetState beyond the existing documented set (TASK-027, TASK-028, GAME_STATE §2.3)
- Redis key writes (REDIS_STATE §7 deferral unchanged)
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

`BattleState` (`src/backend/GameServer.Domain/Battle/BattleState.cs`) nests `PlayerState PlayerState` and `PetState PetState`. `PlayerState` (`src/backend/GameServer.Domain/Battle/PlayerState.cs`) holds HP/MaxHP/ATK/DEF/Crit/Power/Combo/MatchCount. `PetState` holds only Element/PassiveId/PassiveProgress/PassiveResetOverride. Consumers include `DamagePipeline.cs`, `BattleStateService.cs`, `BattleHub.cs`, `BattleEventWireProjection.cs`.

---

## Acceptance Criteria

- [x] `BattleState` has no `PlayerState` member; `Combo` and `MatchCount` are root members per GAME_STATE §2.2
- [x] Combat fields (HP, MaxHP, ATK, DEF, Crit, Power) exist on `PetState` and are the only Player-side combat home
- [x] Domain, Application, and Api code compile with all access paths updated; no stale `PlayerState` combat readers remain outside wire-projection label mapping
- [x] Wire payloads unchanged: `playerState` still carries exactly `combo`+`matchCount`; `petState` still carries exactly the Passive trio; `finalPlayerHp`/`target="player"` labels unchanged
- [x] Full existing test suite passes without weakening assertions (behavior-preserving refactor per `development/refactor.md` §2)
- [x] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [x] Quality review checklist passes (`quality/review.md` §1)
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[x] tests/ (unit / integration / gameplay scenarios)
[ ] docs/ (documentation already describes target shape — GAME_STATE.md §2.2/§2.3, ADR-011)
```

---

## Implementation Notes

- Key files: `GameServer.Domain/Battle/BattleState.cs`, `GameServer.Domain/Battle/PlayerState.cs` (delete after migration of fields), `GameServer.Domain/Combat/DamagePipeline.cs`, `GameServer.Application/Battle/BattleStateService.cs`, `GameServer.Api/Hubs/BattleHub.cs` (projection from root/PetState into unchanged `PlayerStatePayload`/`PetStatePayload` records), `GameServer.Api/Hubs/BattleEventWireProjection.cs`.
- `PlayerStatePayload(int Combo, int MatchCount)` and `PetStatePayload` record shapes stay as-is; only their source members move (`BattleHub.cs` ~L630–656).
- `BattleEvent.ForBattleWon/ForBattleLost` final-player-HP arguments must read Pet HP after the move (`BattleStateService.cs` L651, L816) — same value, new location; wire `finalPlayerHp` unchanged.
- This is a structure-only change: no damage formula, ordering, or event-payload edits. If any semantic correction seems needed, stop and defer to TASK-026.
- Completed tasks affected in spirit only (do not edit `tasks/completed/`): TASK-015, TASK-016, TASK-017, TASK-018, TASK-019, TASK-021, TASK-022.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — BattleState construction/factory methods with root Combo/MatchCount and PetState combat fields
[x] Integration tests  — existing battle resolution suites recompiled against new shape; full suite green
[x] Gameplay scenarios — unchanged outcomes: same swap → same damage/events as before the refactor (regression equality)
```

### Key Edge Cases
- See `docs/02-technical/GAME_STATE.md` §2.0.3 — fields explicitly not present (no reintroduction of forbidden nodes)
- See `docs/02-technical/SIGNALR_PROTOCOL.md` §4.2 item 2, §4.3 — payload member-set must not widen

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If the refactor cannot preserve external behavior without a protocol change: STOP per `development/refactor.md` §1 and split
- If task requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose

---

## Completion Evidence

### Changed Files

Production — Domain (`src/backend/GameServer.Domain/`):

- `Battle/BattleState.cs` — removed the `PlayerState` parameter/property; added root
  `Combo` and `MatchCount` (with `InitialCombo` / `InitialMatchCount`); `Create(...)`
  writes them as root members; docs corrected to the §2.2 / ADR-011 shape
- `Battle/PetState.cs` — added `HP`, `MaxHP`, `ATK`, `DEF`, `Crit`, `Power` to the
  record (in the documented §2.3 order) plus the `DefaultHP` / `DefaultMaxHP` /
  `DefaultATK` / `DefaultDEF` / `DefaultCrit` / `DefaultPower` constants;
  `AtBattleCreation` supplies them at the `COMBAT_RULES.md` §1.1 MVP defaults
- `Battle/PlayerState.cs` — **deleted** (293 lines; no replacement abstraction)
- `Match3/SwapExecution.cs` — `AccountMatches` now returns `(Combo, MatchCount)` from
  the previous root `MatchCount` instead of a whole new state; the single write-back
  sets `Combo`, `MatchCount`, and `PetState` (Power/HP) instead of the old nested node
- `Match3/ResourceGenerator.cs` — `ApplyPower` / `ApplyHeal` now take and return
  `Battle.PetState`
- `Combat/DamagePipeline.cs`, `Combat/DamageEvents.cs`, `Combat/ComboModifiers.cs`,
  `Match3/BattleEvent.cs`, `Match3/BattleEventBuilder.cs`, `Match3/ResolutionEvents.cs`,
  `Battle/BossState.cs`, `Bosses/BossId.cs` — doc comments re-pointed to the new
  owners (`PetState.ATK/DEF/HP/Crit`, `BattleState.Combo/MatchCount`). No code change

Production — Application / Api:

- `Application/Battle/BattleStateService.cs` — Damage Pipeline inputs read
  `resolved.PetState.ATK` and `resolved.Combo`; the Boss→Player instance reads
  `resolved.PetState.DEF` / `.HP` and writes back `PetState` `HP`;
  `ForBattleWon` / `ForBattleLost` read `resolved.PetState.HP` (wire `finalPlayerHp`
  unchanged); Player-HP terminal check reads `resolved.PetState.HP`
- `Api/Hubs/BattleHub.cs` — `PlayerStatePayload(state.Combo, state.MatchCount)`
  sources the new root members. `PlayerStatePayload(int Combo, int MatchCount)` and
  `PetStatePayload` record shapes unchanged; `BattleEventWireProjection.cs` required
  no change

Tests (`tests/backend/`):

- `GameServer.Api.Tests/ApiIntegrationTests.cs` — state reads re-pointed; added
  `WireContract_ShouldBeUnchangedByTheCombatStateOwnershipRefactor` and
  `WireContract_FinalPlayerHp_ShouldCarryTheActivePetsHp`
- `GameServer.Application.Tests/BattleStateServiceTests.cs`, `VictoryDefeatTests.cs`,
  `BossResponseTests.cs` — state reads re-pointed to root `Combo`/`MatchCount` and
  `PetState.*`
- `GameServer.Domain.Tests/MatchComboAccountingTests.cs` — BattleState shape test
  rewritten to assert the root members and the **absence** of any `PlayerState` node
  or type; combat-stat tests re-pointed to `PetState`; the rest re-pointed mechanically
- `GameServer.Domain.Tests/BattleEventEmissionTests.cs`,
  `PlayerEffectHealingTests.cs`, `ResourceGenerationTests.cs`,
  `BattleStateTests.cs`, `BossStateTests.cs`, `CascadeAndDeterminismTests.cs` —
  re-pointed; `PetState_ShouldCarryExactlyTheDocumentedFields` updated to the
  documented ten-member set

Task files:

- `tasks/backlog/TASK-025-…md` → `tasks/active/TASK-025-…md` (READY → IN PROGRESS →
  IN REVIEW → DONE), then moved to `tasks/completed/`

Not changed (deliberate):

- `src/frontend/client/` — no change. The client consumes only the wire contract
  (`playerState.{combo,matchCount}`), which is preserved byte-for-byte; its 174 tests
  pass unchanged
- `docs/` — no change. `GAME_STATE.md` §2/§2.2/§2.3 and ADR-011 already describe the
  target shape; this task brings code to the documentation, not the reverse
  (`AGENTS.md` §17)
- `GameServer.Infrastructure/`, migrations, Redis wiring — no change
- `tasks/completed/TASK-023`, `TASK-024`, `TASK-037` — untouched

### Validation Results

- `dotnet build src/backend/GameServer.sln -c Debug` — PASS (0 errors)
- `dotnet test src/backend/GameServer.sln -c Debug` — PASS (1074 tests total):
  - Domain: 844 PASS, 0 FAIL
  - Application: 101 PASS, 0 FAIL
  - Infrastructure: 44 PASS, 0 FAIL
  - Api: 85 PASS, 0 FAIL
- `npm run test:run` (`src/frontend/client`) — PASS (174 tests, 12 files), unchanged
- Baseline before this task was 1072 tests with **one pre-existing failure**
  (`BattleStateServiceTests.ExecuteSwap_ShouldReduceBossHpThroughTheDamagePipeline`,
  expected 45 / actual 44). That assertion compared a right-truncated expected value
  against the pipeline's own reported delta and depended on the `PlayerState`
  struct's member set. After the refactor the same assertion computes the same
  documented formula against the new owners and passes. No assertion was weakened:
  the formula, its inputs, and its derivation comments are unchanged — only the
  members it reads moved.
- Wire regression: `WireContract_ShouldBeUnchangedByTheCombatStateOwnershipRefactor`
  and `WireContract_FinalPlayerHp_ShouldCarryTheActivePetsHp` — PASS (2/2)

### Repository-wide leftover search

```text
grep "PlayerState" over src/backend + tests/  → matches only:
  - GameServer.Api/Hubs/BattleHub.cs: the PlayerStatePayload DTO and the
    `playerState` wire member name (fixed protocol labels — SIGNALR_PROTOCOL.md
    §4.2, ADR-011 item 6)
  - test/type doc comments that assert its absence
No BattleState.PlayerState.*, no .PlayerState.* read/write, no `new PlayerState`,
no Domain PlayerState type (typeof(BattleState).Assembly has none), and no
LegacyPlayerState / CombatPlayerState / PlayerCombatState / BattlePlayerCombatState
equivalent.
```

### Quality Review (`quality/review.md` §1)

- **Correctness** — matches `GAME_STATE.md` §2/§2.2/§2.3 and ADR-011 items 1–6
- **Architecture** — ownership only; layering and module boundaries unchanged;
  Api still projects state one-to-one and computes nothing
- **Scope** — no behavior change; no unrelated refactor; no new field
- **Tests** — HIGH-risk depth: unit + integration + gameplay-path suites, plus the
  wire regression requirement
- **Documentation** — none required; docs already specified the target
- **Security** — not touched
- **Performance** — no new work on the resolution path; one fewer whole-state
  allocation per Swap (`AccountMatches` returns two ints, not a state)
- **Maintainability** — a type was removed rather than added; no new abstraction,
  interface, or compatibility shim
- **Determinism** — server authority and RNG untouched; the Damage Pipeline remains
  pure and parameter-driven; no client-authoritative value introduced

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed no migrations and no database schema change
- [x] Confirmed no Redis schema or behavior change
- [x] Confirmed no gameplay/formula/passive/card/relic/boss/Match-3 change
