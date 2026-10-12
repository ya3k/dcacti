# TASK-259 — Shared-Board Gameplay Rules and Documentation Synchronization

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
-->

---

## Metadata

```text
Task ID:           TASK-259
Type:              DOCUMENTATION
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     review
Supporting Agents: gameplay, backend
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery, quality/documentation-consistency
Dependencies:      TASK-258
Declared Files:    docs/00-overview/MVP_SCOPE.md,
                   docs/01-game-design/GAME_RULES.md,
                   docs/01-game-design/MATCH3_RULES.md,
                   docs/01-game-design/COMBAT_RULES.md,
                   docs/01-game-design/BOSS_RULES.md,
                   docs/02-technical/GAME_STATE.md,
                   docs/02-technical/GAME_EVENTS.md,
                   docs/02-technical/SIGNALR_PROTOCOL.md,
                   docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md,
                   docs/03-decisions/README.md
```

---

## Objective

Synchronize authoritative game rules in `docs/01-game-design/`, technical specifications in `docs/02-technical/`, `MVP_SCOPE.md`, and the ADR index with Product Owner-approved decisions D1–D7 and `ADR-026`:
- Establish the single shared-board combat model for Player and Boss.
- Enforce ATK-match-gated damage calculation and retire fallback basic attacks.
- Define actor-relative cell effects for all four gem types (`ATK`, `DEF`, `HP`, `POWER`).
- Update Boss skill definitions to deal zero direct base damage and remove Burn DoT.
- Document atomic round state persistence versus non-transactional SignalR event delivery.
- Preserve unresolved balance parameters (SkillCharge unit, clamp policy, modifier compounding) explicitly without inventing defaults.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Match-3, Bosses, and Combat scope
- `docs/01-game-design/GAME_RULES.md` §1.3, §2, §14, §15, §17 — Core rules and resolution sequence
- `docs/01-game-design/MATCH3_RULES.md` §2, §8 — Swap rules and counter progression
- `docs/01-game-design/COMBAT_RULES.md` §2, §3, §3.4, §4 — Resource generation, damage pipeline, and healing/shield rules
- `docs/01-game-design/BOSS_RULES.md` §3, §6.3, §6.3.1 — Boss mechanics and skill definitions
- `docs/02-technical/GAME_STATE.md` §2.1, §2.4, §5 — State definitions and atomic round concurrency
- `docs/02-technical/GAME_EVENTS.md` §1 — Event emission sequence
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3, §3.1, §3.2 — Realtime event delivery boundaries
- `docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md` — Authoritative ADR
- `docs/03-decisions/README.md` — ADR index
- `tasks/active/TASK-258-shared-board-gameplay-contract-audit-and-design.md` — Verified audit artifact

---

## Scope

### In Scope
- Synchronize all relevant authoritative documents to reflect D1–D7 exactly as approved.
- Create and accept `ADR-026: Shared-Board Gameplay and Actor-Relative Match Resolution`.
- Update `docs/03-decisions/README.md` version and index table.
- Eliminate legacy contradictory rules (e.g., fixed basic attack fallback, "Bosses do not match gems").
- Explicitly label unresolved balance choices (SkillCharge unit, clamp policy, Flame Burst / Rage compounding).

### Out of Scope
- Code modifications in `src/backend/` or `src/frontend/`.
- Test modifications in `tests/`.
- Pre-authorizing or creating downstream implementation tasks (`TASK-260`..`TASK-265`).
- Inventing balance values or compounding formulas.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-259:
```text
docs/00-overview/MVP_SCOPE.md
docs/01-game-design/GAME_RULES.md
docs/01-game-design/MATCH3_RULES.md
docs/01-game-design/COMBAT_RULES.md
docs/01-game-design/BOSS_RULES.md
docs/02-technical/GAME_STATE.md
docs/02-technical/GAME_EVENTS.md
docs/02-technical/SIGNALR_PROTOCOL.md
docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md
docs/03-decisions/README.md
```

---

## Acceptance Criteria

- [x] Authoritative game rules and technical specifications reflect D1–D7 without contradiction.
- [x] `ADR-026` is authored and indexed as `Accepted` in `docs/03-decisions/README.md`.
- [x] Legacy fallback basic attack (`Base Damage = Boss.ATK`) is formally retired.
- [x] Damage is strictly gated on confirmed ATK-gem matches (`ClearedGemCounts.Atk > 0`).
- [x] Boss skills deal zero direct base damage (`BaseDamage = 0`) and Burn DoT is removed.
- [x] Unresolved balance choices are explicitly documented and not silently decided.
- [x] Zero gameplay code or test files modified.
- [x] Documentation validation (`git diff --check`, whitespace checks) passes cleanly.

---

## Completion Evidence

### Commit
- Implementation slice (Phase A): `083372b84cf22da815049b49f9976378ba2f9909`
- Subject: `docs: synchronize game rules and technical specifications for shared-board battle`
- Owns: `TASK-259`
- Files: the declared file set above
- Shared with: `none`
- Unowned / pre-existing: `none`
- Completion record filing (Phase B): this completion record alone, filed under its own bookkeeping commit (`tasks/completed/TASK-259-shared-board-gameplay-rules-and-documentation-synchronization.md`).

### Changed Files
- `docs/00-overview/MVP_SCOPE.md` — Match-3, Boss, and Combat scope updated for shared board.
- `docs/01-game-design/GAME_RULES.md` — Core rules updated for shared-board model, ATK-gated damage, and atomic turn sequence.
- `docs/01-game-design/MATCH3_RULES.md` — Swap rules and board turn progression updated.
- `docs/01-game-design/COMBAT_RULES.md` — Resource generation and damage pipeline updated for actor-relative matches and zero base damage Boss skills.
- `docs/01-game-design/BOSS_RULES.md` — Boss skill definitions updated to zero direct damage and no Burn DoT; Boss board moves documented.
- `docs/02-technical/GAME_STATE.md` — Battle state updated for atomic round and actor-relative effects.
- `docs/02-technical/GAME_EVENTS.md` — Event emission sequence aligned with atomic round.
- `docs/02-technical/SIGNALR_PROTOCOL.md` — Realtime delivery boundaries documented.
- `docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md` — ADR-026 authored and accepted.
- `docs/03-decisions/README.md` — ADR index updated with ADR-026.

