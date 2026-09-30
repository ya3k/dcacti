# TASK-089 — Synchronize RewardSummary Documentation Contracts

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and src/ by path and section; copies only what an
  implementer needs to know where to look.
-->

---

## Metadata

```text
Task ID:           TASK-089
Type:              DOCUMENTATION (synchronize stale documentation-only wording with completed implementation)
Status:            DONE
Risk:              LOW (documentation change only, zero code/schema/behavior change)
Priority:          MEDIUM (removes stale staging/deferred wording that contradicts landed contracts)
Primary Agent:     persistence
Supporting Agents: backend, review
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery, quality/documentation-consistency, backend/persistence-analysis, backend/api-contract-validation
Dependencies:      TASK-065, TASK-067, TASK-068
```

---

## Objective

Synchronize stale `RewardSummary` staging and deferral wording in `docs/02-technical/API_CONTRACTS.md` §4
and `docs/02-technical/DATABASE.md` §1 with the landed Player XP and Pet XP progression implementations
(TASK-065, TASK-067, TASK-068) and established backend `BattleResult` persistence contracts.
This is a documentation-only synchronization: no source-code change, no API behavior change, no database
schema change, no RewardSummary redesign, and no gameplay change.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 (MVP progression contracts)
- `docs/01-game-design/COMBAT_RULES.md` §7 — Player XP reward amounts (`+100` on win, `+0` on loss) and Level formula
- `docs/01-game-design/PET_RULES.md` §5.3–§5.5 — Pet XP reward amounts (`+100` to active Pet on win, `+0` on loss), hard cap (4900), and Level formula
- `docs/02-technical/DATABASE.md` §1 — `RewardSummary` member list ownership and `BattleResult` entity definition
- `docs/02-technical/API_CONTRACTS.md` §4 — `GET /api/battle/{battleId}/result` response schema and notes
- `tasks/completed/TASK-065-implement-player-xp-progression.md` — completed Player XP progression
- `tasks/completed/TASK-067-implement-pet-xp-progression.md` — completed Pet XP progression and 8-member `RewardSummary` projection
- `tasks/completed/TASK-068-resolve-rewardsummary-defeat-shape-contradiction.md` — resolution of symmetrical defeat shape (Option A)

---

## Scope

### In Scope

- Synchronize `docs/02-technical/API_CONTRACTS.md` §4:
  - Update the `GET /api/battle/{battleId}/result` example response from `"rewards": {}` to reflect the landed 8-member `RewardSummary` schema.
  - Update Note 1 to remove stale phrases stating `rewards` is "currently the staging value `{}` until the implementation task lands" and "The Pet member list is deferred to the implementation task".
  - Update Note 3 to remove the obsolete staging reference (`{}` staging).
- Synchronize `docs/02-technical/DATABASE.md` §1:
  - Update the `BattleResult` entity block description of `RewardSummary` to remove "while its member list is deferred to the implementation task".
  - Update "Identity and reward sourcing for `BattleResult`" item 3 to remove stale `{}` staging wording ("staging value is the empty JSON object `{}`").
  - Update "Reward semantics for `RewardSummary`" item 2 and item 4 to reflect that the 8-member projection has landed (TASK-067) and retire `{}` staging in force.

### Out of Scope

- **Zero source code changes** (`src/backend/`, `src/frontend/client/`).
- **Zero API behavior changes** or response transformations.
- **Zero database schema changes** or migration files.
- **Zero RewardSummary shape redesign** or new field additions.
- **Zero gameplay rule changes**.

---

## Current State

With the completion of TASK-065, TASK-067, and TASK-068, the backend `BattleResultService` persists and returns the authoritative 8-member `RewardSummary` JSON:
- `playerXpGained` (int)
- `newPlayerXp` (int | null)
- `playerLeveledUp` (bool | null)
- `newPlayerLevel` (int | null)
- `petXpGained` (int)
- `newPetXp` (int | null)
- `petLeveledUp` (bool | null)
- `newPetLevel` (int | null)

However, `API_CONTRACTS.md` §4 and `DATABASE.md` §1 still contain transitional phrases claiming `RewardSummary` is pending implementation or carrying an empty `{}` staging shape.

---

## Acceptance Criteria

- [x] `API_CONTRACTS.md` §4 response schema and contract notes reflect the landed 8-member `RewardSummary` structure without referencing pending implementation or `{}` staging.
- [x] `DATABASE.md` §1 entity definition and "Reward semantics for `RewardSummary`" text describe the landed 8-member `RewardSummary` contract and remove transitional deferral text.
- [x] No source code files in `src/` are modified.
- [x] No database schema or migration files are created or altered.
- [x] No gameplay rules in `docs/01-game-design/` are modified.
- [x] Quality review checklist passes (`quality/review.md` §1).

---

## Affected Files & Areas

```text
[ ] src/backend/                                   - NOT touched (prohibited)
[ ] src/frontend/client/                           - NOT touched (prohibited)
[ ] tests/                                         - NOT touched (prohibited)
[x] docs/02-technical/API_CONTRACTS.md             - §4 response example & notes synchronization
[x] docs/02-technical/DATABASE.md                  - §1 RewardSummary contract synchronization
```

---

## Implementation Notes

- Edit strictly the canonical owning sections per `.ai/workflow/documentation/documentation-change.md`.
- Ensure version headers and changelogs in `API_CONTRACTS.md` and `DATABASE.md` are incremented cleanly per repository standards.

---

## Testing Requirements

### Required Verification
```text
[x] Documentation consistency check — verify API_CONTRACTS.md §4, DATABASE.md §1, and COMBAT_RULES.md/PET_RULES.md are perfectly aligned.
[x] Scope verification — ensure zero code files and zero migrations are touched.
```

### Key Edge Cases
- Ensure `RewardSummary` continues to correctly document both `"victory"` and `"defeat"` shapes symmetrically per TASK-068 Option A.

---

## Stop Conditions

- If implementation requires changing backend code: STOP per `AGENTS.md` §16.
- If implementation requires altering the database schema: STOP per `AGENTS.md` §18.
- If documentation changes contradict `COMBAT_RULES.md` or `PET_RULES.md`: STOP per `AGENTS.md` §4.

---

## Completion Evidence

### Changed Files
- `docs/02-technical/API_CONTRACTS.md` — synchronized §4 response schema and contract notes with the landed 8-member `RewardSummary` contract.
- `docs/02-technical/DATABASE.md` — synchronized §1 entity definition and `RewardSummary` semantics notes with the landed 8-member contract.

### Validation Results
- Documentation consistency audit — PASS (API_CONTRACTS.md §4, DATABASE.md §1, and COMBAT_RULES.md/PET_RULES.md aligned)
- Scope check — PASS (zero source code, schema, migration, or test changes)

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
