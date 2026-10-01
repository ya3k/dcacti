# TASK-114 — Record the Resolved Combat RNG Draw Contract (Crit Roll Procedure, Draw Point, and Stream)

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  
  PROVENANCE: downstream documentation task from completed TASK-113 (FU-1).
  TASK-113 collected and recorded the explicit Product Owner decisions on the
  combat RNG procedure (D-1 through D-5). This task updates the canonical
  authoritative documentation (docs/) to incorporate those decisions.
  
  BOUNDARY: documentation only. Zero files under src/ or tests/.
-->

---

## Metadata

```text
Task ID:           TASK-114
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md).
Status:            DONE
Risk:              LOW (TASK_TYPES.md §4 — DOCUMENTATION baseline LOW–MEDIUM;
                   LOW because all gameplay and contract decisions were already
                   resolved by the Product Owner in TASK-113; this task records
                   them into canonical documentation without altering balance
                   or introducing new mechanics).
Priority:          HIGH (unblocks the PetSkillCast implementation task on the
                   ROADMAP.md Phase 1 critical path).
Primary Agent:     review
Supporting Agents: gameplay, backend
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   quality/documentation-consistency,
                   quality/scope-validation
Dependencies:      TASK-113 (DONE — resolved Crit roll procedure, draw point,
                   stream, DoT-crit eligibility, and representation contracts)
Blocks:            Crit / Burn / PetSkillCast implementation task (FU-2)
```

---

## Objective

Update authoritative documentation (`COMBAT_RULES.md` §3.3 and §5.2, with minimal consistency cross-references in `TDD.md` §6 and `GAME_STATE.md` §2.6.2) to record the deterministic Combat RNG draw procedure resolved by Product Owner decisions in TASK-113 (D-1 through D-5), without modifying source code, tests, wire protocols, or authored balance values.

---

## Authoritative References

- `tasks/completed/TASK-113-resolve-combat-rng-draw-contract.md` — Authoritative decision input (D-1 through D-5)
- `docs/01-game-design/COMBAT_RULES.md` §1.1, §3.3, §5.2 — Critical Hits and Status Rules canonical owner
- `docs/01-game-design/MATCH3_RULES.md` §1.2.1.4, §7.2 — Bounded RNG selection precedent and board RNG scope
- `docs/02-technical/GAME_STATE.md` §2.6 — Active battle RNG state contract
- `docs/02-technical/TDD.md` §6 — Deterministic PRNG architecture
- `docs/03-decisions/ADR/ADR-009-deterministic-prng.md` — Single PCG32 generator decision
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.13 — `otherModifiers` combined Step-4 modifier carrier

---

## Scope

### In Scope

- Update `COMBAT_RULES.md` §3.3 to document the deterministic Crit roll procedure, bounded RNG selection over bound 100, comparison condition (`V < Crit stat`), draw point (Damage Pipeline step 4), selection count (1 per damage instance), single `RngState` stream, and participation of all pipeline damage instances (including Burn DoT ticks).
- Update `COMBAT_RULES.md` §5.2 item 3 to cross-reference §3.3 for DoT tick Crit participation while preserving step 2 Combo neutralization.
- Update `TDD.md` §6 item 4 with minimal consistency correction noting that combat damage resolution evaluates Crit draws alongside board cascade spawn draws on the single PRNG stream.
- Update `GAME_STATE.md` §2.6.2 item 2 with minimal cross-reference acknowledging combat Crit evaluation as a consumer of the single `RngState` pair.

### Out of Scope

- No source code changes under `src/`.
- No test changes under `tests/`.
- No new Battle Events in `GAME_EVENTS.md` or `GAME_RULES.md` §16.
- No new SignalR wire members, methods, or payloads in `SIGNALR_PROTOCOL.md`.
- No database schema or migration changes in `DATABASE.md`.
- No Redis state model changes in `REDIS_STATE.md`.
- No balance value changes (Crit 5%, Crit multiplier 1.5×, Burn 50/tick, Iron Fang +10 pct points remain unchanged).
- No new ADR creation.

---

## Acceptance Criteria

- [x] `COMBAT_RULES.md` §3.3 defines deterministic Crit roll procedure using bounded RNG selection over bound 100, $V < \text{Crit stat}$, step 4 draw point, and single `RngState` stream.
- [x] `COMBAT_RULES.md` §5.2 item 3 explicitly cross-references §3.3 for DoT tick Crit evaluation while keeping Combo neutralization intact.
- [x] `TDD.md` §6 accurately reflects the PRNG consumers (board cascade spawn + combat Crit evaluation).
- [x] `GAME_STATE.md` §2.6.2 accurately reflects the single `RngState` consumers without adding new fields or generators.
- [x] `MATCH3_RULES.md` §7.2 verified board-scoped and left unchanged.
- [x] `SIGNALR_PROTOCOL.md`, `GAME_EVENTS.md`, `DATABASE.md`, `REDIS_STATE.md`, `ADR-009` remain unchanged.
- [x] All frozen contracts (Shield, duration DR1–DR5, step 19a timing, balance values) remain byte-identical.
- [x] Quality review passes and task transitions to `DONE`.

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api) — UNTOUCHED
[ ] src/frontend/client/ — UNTOUCHED
[ ] tests/ — UNTOUCHED
[x] docs/01-game-design/COMBAT_RULES.md
[x] docs/02-technical/TDD.md
[x] docs/02-technical/GAME_STATE.md
```

---

## Completion Evidence

### Decisions Recorded & Applied

```text
D-1 (Crit Roll): Bounded RNG selection over bound 100, V in [0,100), success iff V < Crit stat. Rejection sampling per MATCH3_RULES.md §1.2.1.4 item 2.
    -> Recorded in docs/01-game-design/COMBAT_RULES.md §3.3 item 2.

D-2 (Crit Draw Point & Stream): Step 4 ("x Other Modifiers"), 1 bounded selection per damage instance, single RngState pair.
    -> Recorded in docs/01-game-design/COMBAT_RULES.md §3.3 items 1-2, with minimal cross-references in docs/02-technical/TDD.md §6 item 4 and docs/02-technical/GAME_STATE.md §2.6.2 item 2.

D-3 (Burn Can Crit): DoT ticks participate in step 4 Crit evaluation.
    -> Recorded in docs/01-game-design/COMBAT_RULES.md §3.3 item 4 and §5.2 item 3.

D-4 (Representation): otherModifiers remains sole combined carrier.
    -> Recorded in docs/01-game-design/COMBAT_RULES.md §3.3 item 6. SIGNALR_PROTOCOL.md unchanged.

D-5 (Burn Events): No battle event emitted on apply/expiry.
    -> Verified existing GAME_STATE.md §5.1.1 item 10 / GAME_EVENTS.md / GAME_RULES.md §16. Left unchanged.
```

### Verification

- [x] Confirmed zero source code modified under `src/`.
- [x] Confirmed zero tests modified under `tests/`.
- [x] Confirmed no balance values changed (Crit 5%, Crit multiplier 1.5×, Burn 50/tick, Iron Fang +10 pct points).
- [x] Confirmed no second RNG stream, state field, or generator added.
- [x] Confirmed no new Battle Event, SignalR method, or payload added.
- [x] Quality review checklist satisfied per `quality/review.md`.

