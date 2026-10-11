# TASK-248 — Restore Hỏa Long Canonical Base Stats

## Metadata

```text
Task ID:           TASK-248
Type:              BUG
Status:            IN REVIEW
Risk:              LOW
Priority:          HIGH
Primary Agent:     backend
Supporting Agents: testing, review
Workflow:          development/bug-fix.md
Skills:            discovery/documentation-discovery, gameplay/gameplay-behavior-derivation, quality/scope-validation, quality/implementation-review
Dependencies:      None
Declared Files:    src/backend/GameServer.Domain/Bosses/BossDefinitions.cs
```

---

## Objective

Restore the canonical base stats of Hỏa Long in `BossDefinitions.cs` to `MaxHP = 5000`, `ATK = 100`, `DEF = 50`, and `EnrageThreshold = 0.30` (30%, resolving to 1500 HP) as defined authoritatively in `docs/01-game-design/BOSS_RULES.md` §6.1. This addresses the unintended regression introduced in commit `3277893` where `MaxHP` was inadvertently set to `1000`. Per explicit operator approval (Option A), canonical game design is maintained without redesign or accommodation.

---

## Problem Statement & Impact

In commit `3277893` (`feat: add BossDefinitions domain model and pnpm workspace configuration`), Hỏa Long's `MaxHP` in `BossDefinitions.cs` was accidentally modified from `5000` to `1000`. This regression broke authoritative battle state initialization:
1. Battles against Hỏa Long start with 1,000 HP instead of the documented 5,000 HP, distorting MVP combat pacing and boss balance.
2. The derived absolute enrage threshold (`MaxHP * EnrageThreshold`) dropped from 1,500 HP to 300 HP.
3. Four automated test assertions broke across domain unit tests and API battle-start integration smoke tests.

Operator has explicitly approved **Option A**: Treat `MaxHP = 1000` as an unintended regression and restore canonical values from `BOSS_RULES.md` §6.1.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Core Match-3 Battle System & MVP Boss Battles
- `docs/01-game-design/BOSS_RULES.md` §5 item 4 — Enrage state transition rule (`BossHP < EnrageThreshold`)
- `docs/01-game-design/BOSS_RULES.md` §6.1 — MVP Boss Base Stats canonical reference: Hỏa Long HP / MaxHP = 5000, ATK = 100, DEF = 50, EnrageThreshold = 1500 (30%)
- `docs/01-game-design/BOSS_RULES.md` §6.2.1 — Hỏa Long Rage passive mechanics
- `docs/01-game-design/BOSS_RULES.md` §6.3.1 item 1 — Hỏa Long Flame Burst skill mechanics
- `docs/02-technical/DATABASE.md` §1 note item 1 — Combat stats (`MaxHP`, `ATK`, `DEF`, `EnrageThreshold`) are Domain-only configuration and not persisted columns
- `tasks/TASK_LIFECYCLE.md` §6 — Two-stage commit protocol (Phase A implementation slice, Phase B completion record filing)

---

## Scope

### In Scope
- `src/backend/GameServer.Domain/Bosses/BossDefinitions.cs` — restore `MaxHP = 5000` for `HoaLong`.
- Verification against existing domain and API smoke tests covering Hỏa Long's canonical values.
- TASK-248 task lifecycle documentation and completion record filing preparation.

### Out of Scope
- Changing `docs/01-game-design/BOSS_RULES.md` or adapting game design to 1000 HP (Option B is rejected).
- Modifying stats, elements, skills, or passives of any other Boss or Pet.
- SignalR protocol, message formats, or DTO contract modifications.
- PostgreSQL schema migrations or Redis state schema alterations.
- Frontend runtime or presentation changes.
- Modifying or regenerating balance simulation artifacts.
- Unrelated refactoring or cleanups.

---

## Current State

Commit `3277893` changed `MaxHP` in `BossDefinitions.HoaLong` from `5000` to `1000`. The code comment immediately above the property declaration still reads:
`// §6.1: MaxHP 5000, ATK 100, DEF 50, Enrage "1500 (30%)" — the shared`
`// MVP base configuration (the Domain defaults, not persisted columns).`
`MaxHP = 1000,`
`ATK = 100,`
`DEF = 50,`
`EnrageThreshold = 0.30,`

Consequently, 3 tests in `GameServer.Domain.Tests` and 1 test in `GameServer.Api.Tests` currently fail.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-248:
```text
src/backend/GameServer.Domain/Bosses/BossDefinitions.cs
```

*(Note: The task manifest file itself is excluded from the implementation declared file set. The list above strictly matches the `Declared Files:` field in `## Metadata`.)*

---

## Implementation Plan

1. Verify `src/backend/GameServer.Domain/Bosses/BossDefinitions.cs` currently sets `MaxHP = 1000`.
2. Change `MaxHP = 1000` to `MaxHP = 5000` for `HoaLong`.
3. Confirm `ATK = 100`, `DEF = 50`, and `EnrageThreshold = 0.30` remain intact and match §6.1.
4. Verify battle initialization via `ToInitialState()` propagates `MaxHP = 5000` and `HP = 5000`.
5. Run targeted domain and API tests to confirm all four failing assertions pass.
6. Run full backend test suite (`dotnet test src/backend/GameServer.sln --nologo`) and frontend test suite (`npm test -- --run`).
7. Verify diff cleanliness with `git diff --check`.

---

## Acceptance Criteria

- [x] `BossDefinitions.HoaLong.MaxHP` is restored to `5000`.
- [x] `BossDefinitions.HoaLong.ATK` is `100` and `DEF` is `50`.
- [x] `BossDefinitions.HoaLong.EnrageThreshold` is `0.30`, yielding an absolute threshold of `1500` HP (`5000 * 0.30`).
- [x] Initial battle state created from `HoaLong.ToInitialState()` has `HP = 5000` and `State = BossStateKind.Idle`.
- [x] `GameServer.Domain.Tests.BossStateTests.Definitions_ShouldCarryTheApprovedHoaLongValues` passes.
- [x] `GameServer.Domain.Tests.BossStateTests.Definitions_ShouldCarryEachBossesOwnDocumentedBaseStats` passes.
- [x] `GameServer.Domain.Tests.BossStateTests.Definitions_ShouldCarryEachBossesOwnDocumentedEnrageThreshold` passes.
- [x] `GameServer.Api.Tests.BattleStartSmokeTest.SmokeTest_PostBattleStart_ShouldComposeAnAuthoritativeBattleWithBothLoadoutSnapshots` passes.
- [x] Full backend suite passes (2,970/2,970 passing).
- [x] Frontend test suite passes (991/991 passing).
- [x] No regression or changes introduced to other boss definitions or game rules.
- [x] `git diff --check` passes with zero issues.

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain / Application / Infrastructure / Api) [src/backend/GameServer.Domain/Bosses/BossDefinitions.cs only]
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[ ] tests/ (unit / integration / gameplay scenarios)
[ ] docs/ (documentation updates if applicable)
```

---

## Implementation Notes

- In `src/backend/GameServer.Domain/Bosses/BossDefinitions.cs`, line 144 was restored from `MaxHP = 1000,` to `MaxHP = 5000,`.
- Domain model `BossDefinition.cs` defaults to `BossRules.BaseMaxHP` (5000) and `BossRules.BaseEnrageThreshold` (0.30), and derives `ToInitialState()` using `MaxHP`.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — GameServer.Domain.Tests (BossStateTests)
[x] Integration tests  — GameServer.Api.Tests (BattleStartSmokeTest)
[x] Solution suite     — dotnet test src/backend/GameServer.sln --nologo
[x] Client suite       — npm test -- --run (from src/frontend/client)
```

### Key Edge Cases
- Boss Enrage transition calculation `bossState.HP < bossState.MaxHP * bossDefinition.EnrageThreshold` evaluates at exactly 1500 HP (`BOSS_RULES.md` §5 item 4, §6.1).

---

## Stop Conditions

- If required canonical values conflict with `docs/01-game-design/BOSS_RULES.md` §6.1: STOP per `AGENTS.md` §4 / §7.
- If fixing the regression requires database schema migration or API wire contract change: STOP per `AGENTS.md` §18.
- If task scope expands to changing other boss definitions: STOP per `AGENTS.md` §16.
- If commit or push authorization is withheld: STOP before creating Git commits per operator instructions.

---

## Completion Evidence

### Commit
- Implementation slice (Phase A): `<pending operator authorization>`
- Subject: `fix(domain): restore Hoa Long canonical base MaxHP to 5000`
- Owns: `TASK-248`
- Files: `src/backend/GameServer.Domain/Bosses/BossDefinitions.cs`
- Shared with: `none`
- Unowned / pre-existing: `none`
- Completion record filing (Phase B): `<pending operator authorization>`

### Changed Files
- `src/backend/GameServer.Domain/Bosses/BossDefinitions.cs` — Restored `MaxHP = 5000` on `BossDefinitions.HoaLong`.

### Validation Results
- `dotnet test tests/backend/GameServer.Domain.Tests/GameServer.Domain.Tests.csproj --filter "FullyQualifiedName~BossStateTests" --nologo` — PASS (56/56 tests passing)
- `dotnet test tests/backend/GameServer.Api.Tests/GameServer.Api.Tests.csproj --filter "FullyQualifiedName~BattleStartSmokeTest.SmokeTest_PostBattleStart_ShouldComposeAnAuthoritativeBattleWithBothLoadoutSnapshots" --nologo` — PASS (1/1 tests passing)
- `dotnet test src/backend/GameServer.sln --nologo` — PASS (2,970/2,970 passed; Domain: 1,558, Infrastructure: 422, Api: 337, Application: 653)
- `npm test -- --run` (src/frontend/client) — PASS (23 test files, 991/991 tests passing)
- `git diff --check` — PASS (zero whitespace / conflict issues)

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed P-1…P-6 commit policy compliance
