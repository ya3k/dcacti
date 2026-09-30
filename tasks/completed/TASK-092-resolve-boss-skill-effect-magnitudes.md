# TASK-092 — Resolve Boss Skill Effect Magnitudes

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and tasks/ by path and section.

  THIS TASK RESOLVED EXACTLY ONE REMAINING GAMEPLAY CONTRACT:
  The exact effect magnitudes and durations for the three MVP Boss Skills
  defined in `docs/01-game-design/BOSS_RULES.md §6`:
    1. Drain Power (Thủy Ma)
    2. Root (Mộc Yêu)
    3. Flame Burst (Hỏa Long)
-->

---

## Metadata

```text
Task ID:           TASK-092
Type:              DOCUMENTATION
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     review
Supporting Agents: gameplay, backend
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   gameplay/gameplay-behavior-derivation,
                   quality/documentation-consistency,
                   quality/scope-validation
Dependencies:      TASK-091
Blocks:            Future Boss Skill / Status Effects execution implementation tasks
```

---

## Objective

Determine and record the exact effect magnitudes and durations for the three MVP Boss Skills (`Drain Power`, `Root`, and `Flame Burst`) in `docs/01-game-design/BOSS_RULES.md` §6, while strictly preserving existing base skill damages, passive values, and the resolved End Turn Status Effect tick timing (TASK-091 / `GAME_RULES.md` §17 step 19a).

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Bosses and Combat mechanics in MVP scope
- `docs/01-game-design/BOSS_RULES.md` §6, §6.1, §6.2, §6.3, §6.3.1 — MVP Boss definitions, base stats, passives, skill base damages, and effect magnitudes
- `docs/01-game-design/COMBAT_RULES.md` §5, §5.1, §5.2 — Status Effects (Burn, Buff/Debuff definitions and rules)
- `docs/01-game-design/GAME_RULES.md` §17 — Turn Resolution Order (step 18b Boss Skill, step 19a Status Effect tick)
- `docs/01-game-design/PASSIVE_RULES.md` §6 — Reference confirming effect magnitudes are defined per content entity in domain reference tables
- `tasks/completed/TASK-091-resolve-status-effect-tick-timing.md` — Authoritative timing resolution for DoT ticks at End Turn step 19a

---

## Scope

### In Scope

- Resolving the missing effect magnitude and duration contracts for the 3 MVP Boss Skills:
  1. **Drain Power** (Thủy Ma):
     - Amount type: flat Power reduction
     - Magnitude: `-20 Power`
     - Duration: none (instant subtraction on `PetState.Power`)
  2. **Root** (Mộc Yêu):
     - Pet ATK reduction magnitude: `-30% Pet ATK`
     - Duration: `2 Turns` using the authoritative Turn model
  3. **Flame Burst** (Hỏa Long):
     - Burn DoT magnitude: fixed `50 damage per tick` (no scaling)
     - Burn duration: `2 Turns` = exactly 2 End Turn ticks (step 19a)
- Recording the resolved numbers into `docs/01-game-design/BOSS_RULES.md` §6 and §6.3 / §6.3.1.
- Preserving the End Turn DoT tick timing established by TASK-091 (`GAME_RULES.md` §17 step 19a).
- Preserving existing Boss Skill base damage values (`BOSS_RULES.md` §6.3: Flame Burst 150, Drain Power 120, Root 100).
- Preserving existing Boss Passive values (`BOSS_RULES.md` §6.2: Rage +20% ATK / 3T, Thủy Ma healing -50%, Regeneration 5% MaxHP).

### Out of Scope

- Implementing source code (Domain, Application, Infrastructure, Client).
- Modifying wire protocols or schemas (`SIGNALR_PROTOCOL.md`, `GAME_EVENTS.md`, `API_CONTRACTS.md`).
- Modifying battle state schemas (`GAME_STATE.md`, `REDIS_STATE.md`, `DATABASE.md`).
- Designing new Status Effect types or changing Status Effect resolution timing.
- Adjusting Boss base stats, passive thresholds, or skill charge/cooldown timings.

---

## Current State

Prior to this task, `docs/01-game-design/BOSS_RULES.md` §6 defined the three MVP Boss Skills qualitatively without explicit effect magnitudes or durations. Product owner decisions were received and recorded into `docs/01-game-design/BOSS_RULES.md` §6 and §6.3.1.

---

## Acceptance Criteria

- [x] Drain Power magnitude is explicitly defined (`-20 Power`).
- [x] Drain Power unit/representation (flat vs. percentage) is explicitly defined (flat Power).
- [x] Root ATK reduction magnitude is explicitly defined (`-30% Pet ATK`).
- [x] Root duration is explicitly defined (`2 Turns`).
- [x] Root duration integrates with the authoritative Turn model (`MATCH3_RULES.md` §8.1 / `GAME_RULES.md` §17).
- [x] Flame Burst Burn magnitude is explicitly defined (`50 damage per tick`).
- [x] Flame Burst Burn duration is explicitly defined (`2 Turns`).
- [x] Burn continues to tick according to TASK-091 / `GAME_RULES.md` §17 step 19a (End Turn, one tick per resolved Turn).
- [x] Existing Boss Skill base damage values are unchanged (`BOSS_RULES.md` §6.3: Flame Burst 150, Drain Power 120, Root 100).
- [x] Boss Passive values and base stats remain unchanged (`BOSS_RULES.md` §6.1, §6.2).
- [x] No source code is implemented.
- [x] No SignalR contract, API contract, or Redis state contract is changed.
- [x] No unrelated gameplay rule is changed.
- [x] Relevant documentation is internally consistent.

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[ ] tests/ (unit / integration / gameplay scenarios)
[x] docs/ (docs/01-game-design/BOSS_RULES.md)
```

---

## Implementation Notes

- Canonical domain owner for Boss Skills is `docs/01-game-design/BOSS_RULES.md` §6, §6.3, §6.3.1.
- Canonical domain owner for Status Effects and DoT rules is `docs/01-game-design/COMBAT_RULES.md` §5.
- Timing invariant: Burn applied during Boss Response (step 18b) on Turn $N$ ticks at step 19a of the same Turn $N$ (End Turn), resolving exactly one tick per Turn (2 End Turn ticks total across 2 Turns).

---

## Testing Requirements

### Required Verification

```text
[x] Documentation consistency audit (docs/01-game-design/BOSS_RULES.md, COMBAT_RULES.md, GAME_RULES.md)
[x] Scope check against MVP_SCOPE.md §1
```

### Key Edge Cases

- See `docs/01-game-design/COMBAT_RULES.md` §5.2 — Stacking / refresh rules for Status Effects.
- See `docs/01-game-design/GAME_RULES.md` §17 — Status Effect tick step 19a must not execute if the battle has already reached a terminal state in prior steps.

---

## Stop Conditions

- None hit during execution. Human decisions for all three Boss Skill effect magnitudes were received and recorded.

---

## Completion Evidence

### Changed Files
- `docs/01-game-design/BOSS_RULES.md` — Recorded resolved Boss Skill effect magnitudes and duration contracts in §6, §6.3, and new subsection §6.3.1.
- `tasks/completed/TASK-092-resolve-boss-skill-effect-magnitudes.md` — Completed task manifest.

### Validation Results
- Scope and documentation consistency audit: PASS
- Verified zero references to configurable intervals.
- Verified base damages and passives are intact and unmodified.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
