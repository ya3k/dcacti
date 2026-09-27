# TASK-032 — Player/Pet Authority Regression Suite

---

## Metadata

```text
Task ID:           TASK-032
Type:              BUG
Status:            BACKLOG
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

- [ ] Test: `BattleState` type has no PlayerState member and no Player combat-stat fields exist anywhere in Domain (ADR-011 items 1, 5)
- [ ] Test: Player entity/schema has Level but zero combat-stat columns (DATABASE.md §1, ADR-012 item 1)
- [ ] Test: Combo/MatchCount live at BattleState root only (GAME_STATE §2.2)
- [ ] Test: damage/heal/victory/defeat paths read/write PetState HP/ATK/DEF/Power/Crit only (COMBAT_RULES §1.1, ADR-011 item 3)
- [ ] Test: Player is never a DamageDealt/DamageTaken HP subject; wire `target="player"` still means player's side (SIGNALR_PROTOCOL §3.2.15)
- [ ] Test: no Pet XP and no Evolution fields/tables exist (ADR-012 item 6)
- [ ] Test: no persistent equip table and no Pet.CardInventory exist (ADR-012 items 7, 9)
- [ ] Test: Pet Level derives from Player Level via PET_RULES §5 formula with documented clamp (ADR-012 items 3–4)
- [ ] Test: wire member sets match SIGNALR_PROTOCOL §4.2/§4.3 and fixed labels are unchanged (ADR-011 item 6)
- [ ] Full repository test suite passes at required validation depth (`core/validation.md` §2)
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

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
- `<file path>` — <summary of change>

### Validation Results
- `<test command or suite>` — PASS (<N> tests)

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
