# TASK-315 — Documentation-Only Reconciliation: Active Boss Ability and Combat Lifecycle Specifications

---

## Metadata

```text
Task ID:           TASK-315
Type:              DOCUMENTATION
Status:            DONE
Risk:              LOW
Priority:          HIGH
Primary Agent:     documentation
Supporting Agents: gameplay, review
Workflow:          documentation/documentation-change.md
Skills:            documentation/spec-reconciliation, discovery/impact-analysis, quality/scope-validation
Dependencies:      TASK-313, TASK-314
Declared Files:    docs/01-game-design/BOSS_RULES.md, docs/01-game-design/COMBAT_RULES.md, docs/02-technical/GAME_STATE.md, .ai/PROJECT_STATE.md
```

---

## Objective

Reconcile stale active specifications identified following the ADR-029 Slice 5 / TASK-313 retirement of the legacy Boss Skill mechanism, ensuring authoritative documentation matches the live server-authoritative code:
1. Reconcile remaining active specifications in `BOSS_RULES.md`, `COMBAT_RULES.md`, and `GAME_STATE.md` that refer to retired Boss Skill execution or authored Boss Skill Base Damage (which was removed in TASK-313; all Boss abilities deal zero direct damage per D3).
2. De-stale cooldown counter references so `SkillCooldown` is explicitly described as the Boss ability / Boss Card cooldown counter.
3. Update Root and status effect worked examples so step numbers and durations match the canonical Boss Card lifecycle (step 18c cast, 3-Turn approved duration per TASK-309 B-3, yielding an effective 2-attack debuff window).
4. Update Stun behavior description in `GAME_STATE.md` §2.4.5 to reflect the current one-action response policy (prevents both Boss Card casting and board match swap actions).
5. Update `.ai/PROJECT_STATE.md` navigation cache with current project baseline, completed tasks (TASK-313, TASK-314), and resolved items.
6. Strictly preserve historical changelogs, retirement markers, storage compatibility descriptions, and the Fifth Gem's FUTURE status. Zero changes to runtime code, tests, balance values, schemas, or wire contracts.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1, §3 — MVP Boss abilities are IN; Fifth Gem is OUT/FUTURE
- `docs/01-game-design/BOSS_RULES.md` §4.1, §4.3, §4.4, §6.2.1, §6.3 — Canonical Boss Cards, zero direct damage (D3), response-phase action policy
- `docs/01-game-design/COMBAT_RULES.md` §3.4, §5.3, §5.4.3, §5.5.2, §5.5.3 — Boss damage composition, zero direct damage, Buff/Debuff lifecycle
- `docs/02-technical/GAME_STATE.md` §2.4.3, §2.4.5, §5.1.1 — `SkillCooldown`, Stunned state reflection, status effects lifecycle
- `docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md` — Slice 5 delivered, operator decisions OD-311-1 = B, OD-311-2 = A

---

## Scope

### In Scope
- Documentation reconciliation in `docs/01-game-design/BOSS_RULES.md` (§6.2.1).
- Documentation reconciliation in `docs/01-game-design/COMBAT_RULES.md` (§3.4, §4 item 1, §5.3.3, §5.4.3, §5.5.2, §5.5.3).
- Documentation reconciliation in `docs/02-technical/GAME_STATE.md` (§2.4.5, §5.1.1).
- Navigation cache refresh in `.ai/PROJECT_STATE.md`.

### Out of Scope
- Any runtime code, tests, schemas, balance values, boss/card definitions, or wire contracts.
- Any change or migration to persisted JSONB `chargeRequirement` or `baseDamage` (OD-311-1 = B).
- Modifying historical changelogs, prior versions, or retirement records.
- Fifth Gem / Power-drain Gem work (`MVP_SCOPE.md` §3).

---

## Current State

ADR-029 Slice 5 / TASK-313 physically retired `BossState.SkillCharge`, legacy Boss Skill execution/fallback, and `BossSkillCast`. While the primary sections were updated in TASK-313, several secondary illustrative examples and active prose passages still mention "Boss Skill", "Skill's authored Base Damage", legacy 2-Turn Root duration examples, or pre-one-action Stun descriptions.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-315:
```text
docs/01-game-design/BOSS_RULES.md
docs/01-game-design/COMBAT_RULES.md
docs/02-technical/GAME_STATE.md
.ai/PROJECT_STATE.md
```

---

## Acceptance Criteria

- [x] 1. `BOSS_RULES.md` §6.2.1 is reconciled to reflect that Boss abilities are canonical Boss Cards that deal zero direct damage, with Rage modifying `EffectiveBossATK`.
- [x] 2. `COMBAT_RULES.md` §3.4, §4 item 1, §5.3.3, §5.4.3, §5.5.2, and §5.5.3 are reconciled: Boss abilities deal zero direct damage; Root lifecycle worked example uses step 18c and the canonical 3-Turn duration; `SkillCooldown` is described as the Boss ability counter.
- [x] 3. `GAME_STATE.md` §2.4.5 Stun description reflects the one-action response policy (prevents Boss Card casting and board match actions); §5.1.1 references Boss response step 18 and `SkillCooldown`.
- [x] 4. `.ai/PROJECT_STATE.md` navigation cache is refreshed with current HEAD, verified tasks, and resolved status.
- [x] 5. Zero changes to runtime code, tests, balance values, database schemas, or wire contracts.
- [x] 6. Historical changelogs and retirement notices are preserved.
- [x] 7. Two-stage commit protocol (Phase A implementation commit, Phase B completion record) is strictly followed without pushing.

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[ ] tests/ (unit / integration / gameplay scenarios / balance telemetry)
[x] docs/ (documentation updates only)
```

---

## Implementation Notes

Narrow, precise text updates only. Verified each diff against authoritative code and design contracts.

---

## Testing Requirements

### Required Verification
```text
[x] Documentation validation — verify cross-references, step numbers, and section pointers
[x] Scope verification        — verify zero source/test/schema files modified in git diff
```

---

## Stop Conditions

- If any edit requires changing code, tests, or balance values: STOP per `AGENTS.md` §16
- If any conflict between documentation and code is discovered: STOP per `AGENTS.md` §4
- If Fifth Gem work is required: STOP (`MVP_SCOPE.md` §3)

---

## Completion Evidence

### Commit
- Implementation slice (Phase A): `590c7a6ec189107cab37b00f014cb0022aed0c9d`
- Subject: `docs: reconcile stale active specifications for boss abilities and combat lifecycle`
- Owns: `TASK-315`
- Files:
  - `docs/01-game-design/BOSS_RULES.md`
  - `docs/01-game-design/COMBAT_RULES.md`
  - `docs/02-technical/GAME_STATE.md`
  - `.ai/PROJECT_STATE.md`
- Shared with: `none`
- Unowned / pre-existing: `none`

### Changed Files
- `docs/01-game-design/BOSS_RULES.md` — §6.2.1 updated to reconcile Rage damage scope (replaces legacy Boss Skill Base Damage references with zero-direct-damage rule; Rage modifies `EffectiveBossATK` derived from confirmed ATK-gem match attacks; version bumped to 2.19).
- `docs/01-game-design/COMBAT_RULES.md` — §3.4 updated to compose Boss attack damage from `EffectiveBossATK` + `BaseDamagePool`; §4 item 1 revive-prohibition updated to "Boss Card"; §5.3.3 and §5.4.3 Root worked example updated to step 18c cast with canonical 3-Turn duration; §5.3.4 de-staled `SkillCooldown` reference; §5.5.2 and §5.5.3 damage scope updated to reflect zero direct ability damage (version bumped to 3.4).
- `docs/02-technical/GAME_STATE.md` — §2.4.5 Stunned state description updated to reflect the one-action response policy (prevents both Boss Card casting and board match swap actions); §5.1.1 status effect lifecycle updated to refer to step 18 Boss response and the Boss ability cooldown counter (version bumped to 2.36).
- `.ai/PROJECT_STATE.md` — refreshed navigation cache to version 1.9 with current HEAD, verified tasks, and resolved status.

### Validation Results
- `git diff --check` — **PASS** (0 errors)
- Staged-set exact equality: 4 files declared, 4 files staged, 0 extraneous paths, task record excluded.
- Zero source, test, schema, balance, or wire changes.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed P-1…P-6 commit policy compliance
