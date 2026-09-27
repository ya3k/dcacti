# TASK-032 — Player/Pet Authority Regression Suite

---

## Metadata

```text
Task ID:           TASK-032
Type:              BUG
Status:            DONE
Risk:              LOW
Priority:          HIGH
Primary Agent:     testing
Supporting Agents: gameplay, backend, realtime, review
Workflow:          development/bug-fix.md
Skills:            testing/test-scenario-generation, gameplay/authority-determinism-audit, quality/architecture-conformance, quality/implementation-review, discovery/impact-analysis
Dependencies:      TASK-023, TASK-024, TASK-025, TASK-026, TASK-027, TASK-028, TASK-029, TASK-030, TASK-031
```

---

## Objective

Deliver a cross-layer Player/Pet authority regression suite that encodes the ADR-011/ADR-012 invariants as automated tests spanning Domain, Application, Persistence, and wire projections — locking Player as account-only, Pet/PetState as sole combat authority, battle-scoped loadout snapshots, and fixed wire labels — with the full repository test suite green.

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` items 1–6 — no PlayerState node; root accounting; PetState combat home; no Player combat pool; fixed wire names
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` items 1–12 — Player Level account attribute; no Pet XP/Evolution; ownership/equip split; loadout snapshots; fixed labels
- `docs/02-technical/GAME_STATE.md` §2 — BattleState shape invariants
- `docs/02-technical/DATABASE.md` §1–§3 — Player/Pet/Card/Relic schema invariants (no Player combat columns, no equip table, no Pet.CardInventory)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4.2, §4.3 — fixed payload member sets
- `docs/01-game-design/COMBAT_RULES.md` §1.1 — active Pet as damage subject
- `docs/00-overview/MVP_SCOPE.md` §1 — Player Level, Pets, Cards, Relics, server-authoritative battle IN

---

## Scope

### In Scope
- Regression tests for authority invariants across layers (see Acceptance Criteria)
- Full-suite run and triage: any failure is fixed in the owning layer or reported — tests are not weakened to pass (AGENTS.md §15)
- Invariant catalog documented in the suite's test naming so future tasks extend rather than bypass it

### Out of Scope
- New gameplay behavior or rule changes (no GAMEPLAY-CHANGE here)
- Performance/load testing
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

Tests exist per completed TASK-001..022 for board/combat/passive/boss behavior, but no consolidated suite asserts the ADR-011/012 Player/Pet authority invariants end-to-end after the TASK-025..031 refactor chain. Persistence tests for TASK-023/024/027/028 and wire tests from TASK-031 exist as separate suites.

---

## Acceptance Criteria

- [x] Test: `BattleState` type has no PlayerState member and no Player combat-stat fields exist anywhere in Domain (ADR-011 items 1, 5)
- [x] Test: Player entity/schema has Level but zero combat-stat columns (DATABASE.md §1, ADR-012 item 1)
- [x] Test: Combo/MatchCount live at BattleState root only (GAME_STATE §2.2)
- [x] Test: damage/heal/victory/defeat paths read/write PetState HP/ATK/DEF/Power/Crit only (COMBAT_RULES §1.1, ADR-011 item 3)
- [x] Test: Player is never a DamageDealt/DamageTaken HP subject; wire `target="player"` still means player's side (SIGNALR_PROTOCOL §3.2.15)
- [x] Test: no Pet XP and no Evolution fields/tables exist (ADR-012 item 6)
- [x] Test: no persistent equip table and no Pet.CardInventory exist (ADR-012 items 7, 9)
- [x] Test: Pet Level derives from Player Level via PET_RULES §5 formula with documented clamp (ADR-012 items 3–4)
- [x] Test: wire member sets match SIGNALR_PROTOCOL §4.2/§4.3 and fixed labels are unchanged (ADR-011 item 6)
- [x] Full repository test suite passes at required validation depth (`core/validation.md` §2)
- [x] Quality review checklist passes (`quality/review.md` §1)
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

**Coverage ownership.** The first nine criteria were verified to be already protected by existing suites, which are cited from the new suites' comments rather than duplicated: `BattleStateTests` and `MatchComboAccountingTests` (criteria 1, 3), `PlayerPersistenceTests` / `PetPersistenceTests` (criteria 2, 6, 7), `VictoryDefeatTests` / `PlayerEffectHealingTests` (criteria 4), `BossWireProjectionTests` (criterion 5), `PetLevelDerivationTests` / `PetLevelRecomputeTests` (criterion 8), and `ApiIntegrationTests` / `BossWireProjectionTests` (criterion 9). TASK-032 added the assertions those suites did not carry — see "Coverage Disposition" below.

---

## Affected Files & Areas

```text
[x] src/backend/ (only if a failing invariant reveals a defect to fix in the owning layer)
[ ] src/frontend/client/ (only if a failing invariant reveals a projection defect)
[x] tests/ (primary deliverable — cross-layer regression suite)
[ ] docs/ (only if a genuine doc/code conflict is found — then STOP per AGENTS.md §4 before changing docs)
```

---

## Implementation Notes

- Organize tests by invariant (name tests after the ADR item they enforce) so later tasks can extend the catalog. (Note: TASK-033 is now a concrete task — Player Reward XP / Level Progression — not a placeholder for "future tasks".)
- Reuse existing suites where they already cover an invariant (TASK-031 wire tests, TASK-026 combat invariants) — do not duplicate; fill gaps only.
- If an invariant fails because docs and code disagree: STOP per AGENTS.md §4 and report; do not edit docs or weaken the test to force green.
- Do not edit `tasks/completed/`.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — each Acceptance Criteria invariant has at least one automated test
[x] Integration tests  — cross-layer scenario: auth → pet persist → battle start loadout → resolution → wire projection
[x] Gameplay scenarios — Given the full MVP battle flow, When executed end-to-end, Then all ADR-011/012 authority invariants hold and full suite is green
```

### Key Edge Cases
- See `docs/02-technical/GAME_STATE.md` §2.0.3 — fields explicitly not present (negative assertions)
- See `docs/01-game-design/RELIC_RULES.md` §5 — anti-infinite-chain still holds under loadout snapshot (no behavior change intended)

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If a failing invariant indicates a documentation conflict: STOP per `AGENTS.md` §4 — do not change docs or tests unilaterally
- If task requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose

---

## Completion Evidence

### Changed Files
- `tests/backend/GameServer.Application.Tests/AuthorityRegressionSuiteTests.cs` — new. The Application/Domain share of the authority regression suite: the Domain+Application combat-readiness absence scans, the `BattleState` positional-constructor `PlayerState` exclusion, the four Player/Pet/definition-row snapshot-independence guarantees, the committed-swap loadout write-back + round-trip guarantee, and the guard self-check.
- `tests/backend/GameServer.Api.Tests/ApiAuthorityRegressionTests.cs` — new. The Api layer's share: the combat-readiness absence scan, the `PlayerState` node absence, and the `PlayerStatePayload` member-set guard.
- `tests/backend/GameServer.Infrastructure.Tests/InfrastructureAuthorityRegressionTests.cs` — new. The Infrastructure layer's share: the combat-readiness absence scan and the mapped-Player-model negative shape.
- `tasks/backlog/TASK-032-…md` → `tasks/active/TASK-032-…md` — lifecycle move (`READY` → `IN PROGRESS` → `IN REVIEW`).

No production file was changed. `git status src/backend` shows no modification attributable to this task.

### Coverage Disposition (AGENTS.md §15 — no duplicated coverage)
The existing suites already protect A, B, C, D, E, F, G, I, J and the cores of H and L; those are referenced from the new suites' comments rather than re-asserted. The four gaps actually closed are:
1. **K (was MISSING)** — no test asserted the removed combat-readiness types were absent; the only mention anywhere was a test *comment*. Now scanned in Domain, Application, Api, and Infrastructure.
2. **H (was PARTIAL)** — snapshot-independence tests mutated only Card/Relic *ownership* rows; nothing excluded a re-read of the **Player row**, **Pet row**, or any **definition row**. Now closed for all four.
3. **Loadout round-trip on the resolution path** — loadouts were round-tripped at creation and never after a committed swap, and `BattleStateServiceTests` creates loadouts-free battles. Now asserted across the write-back and the serialize/deserialize cycle.
4. **L (was PARTIAL)** — the `BattleState` field-set assertions bound the properties, not the positional record's own parameter list. Now asserted on the constructor.

### Validation Results
- `dotnet build src\backend\GameServer.sln` — PASS (0 errors)
- `GameServer.Application.Tests` — PASS (252 tests; +10)
- `GameServer.Domain.Tests` — PASS (911 tests)
- `GameServer.Infrastructure.Tests` — PASS (177 tests; +2)
- `GameServer.Api.Tests` — PASS (187 tests; +3)
- Full backend suite (`dotnet test src\backend\GameServer.sln`) — **1527 passed, 0 failed** (baseline 1512)

### Regression-Guard Falsifiability (AGENTS.md §15)
Every new guard was proven able to fail, so none is vacuous:
- The four combat-readiness absence scans were verified by temporarily injecting `ICombatStatsSource` / `PlayerCombatProfile` / `PlayerCombatProfileSource` / `IsCombatReady` into Domain, Api, and Infrastructure — each layer's scan failed as intended. The injections were then removed (`git status` confirms no residue).
- `IndependenceGuard_ShouldDetectEveryMemberItCompares` mutates each of the 24 authority-bearing members in turn and asserts the shared guard rejects it, while asserting the guard accepts the unchanged baseline. This self-check found and fixed a real brittleness during implementation: the guard originally dereferenced an absent loadout with `!`, so the documented staging position (a battle created without a loadout) threw instead of comparing. The guard now compares absence-as-absence and empty-as-empty, which also closes the absent-vs-empty distinction (`GAME_STATE.md` §0 item 4).

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] No gameplay implemented; no board, gem, swap, match, cascade, combat, Pet, Boss, Card, Relic, Passive, XP, or reward behavior added or changed
- [x] No production behavior, API, SignalR, Redis, or PostgreSQL contract changed
- [x] No new architecture introduced (no new service, manager, interface, or abstraction)
- [x] TASK-041, TASK-056, TASK-057 untouched
- [x] No authoritative documentation changed
