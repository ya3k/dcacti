# TASK-311 — ADR-029 Slice 5 Removal-Readiness Reconciliation

<!--
  GEN-TASK EXECUTION MANIFEST — STATUS: DONE
-->

---

## Metadata

```text
Task ID:           TASK-311
Type:              DOCUMENTATION
Status:            DONE
Risk:              LOW
Priority:          HIGH
Primary Agent:     orchestrator
Supporting Agents: gameplay, review, backend
Workflow:          documentation/documentation-change.md
Skills:            quality/documentation-consistency,
                   quality/implementation-review,
                   gameplay/gameplay-behavior-derivation
Dependencies:      TASK-309 (DONE), TASK-310 (DONE), ADR-029 (Accepted, amended)
Declared Files:    tasks/artifacts/TASK-311-adr-029-slice-5-removal-readiness-reconciliation.md,
                   docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md,
                   docs/03-decisions/README.md,
                   docs/01-game-design/BOSS_RULES.md,
                   docs/01-game-design/COMBAT_RULES.md,
                   docs/02-technical/GAME_EVENTS.md,
                   docs/02-technical/SIGNALR_PROTOCOL.md,
                   docs/02-technical/GAME_STATE.md,
                   docs/02-technical/DATABASE.md,
                   docs/02-technical/REDIS_STATE.md,
                   .ai/PROJECT_STATE.md
```

---

## Objective

Resolve the documented Slice 5 readiness gaps identified by the TASK-310 audit, without implementing Slice 5 or removing any legacy runtime behavior:
1. Record explicit old-vs-new Boss ability behavior comparisons for ADR-029 §2.9(b).
2. Reconcile contradictory Boss design documentation and removal-gate terminology for §2.9(c).
3. Prepare the required ADR-029 §7 and ADR index amendments for §2.9(d), following documentation-first governance.
4. Update project navigation/state only as required by the repository lifecycle.

The purpose is to make the eventual `SkillCharge`/legacy `BossSkill` removal task reviewable and registrable. This task must not itself authorize or implement that removal.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1, §3, §4
- `docs/01-game-design/BOSS_RULES.md` §4.1–§4.4, §6, §6.1–§6.4
- `docs/01-game-design/COMBAT_RULES.md` §1.2, §2, §3.3, §3.4, §4, §5.1–§5.5
- `docs/01-game-design/CARD_RULES.md` §1.1, §3 item 9, §3.6, §6
- `docs/01-game-design/GAME_RULES.md` §12, §15, §16, §17 step 18
- `docs/02-technical/GAME_EVENTS.md` §1, §2 (`BossSkillCast`, `BossCardCast`, `PowerChanged`)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.18, §3.2.30, §4.4
- `docs/02-technical/GAME_STATE.md` §2.4, §2.4.3, §5.1.1
- `docs/02-technical/DATABASE.md` §1, §3, §5
- `docs/02-technical/REDIS_STATE.md` §2.1
- `docs/03-decisions/ADR/ADR-024-database-driven-boss-balance-configuration.md`
- `docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md`
- `docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md`
- `docs/03-decisions/README.md`
- `tasks/completed/TASK-309-adr-029-slice-4-remaining-boss-card-abilities.md`
- `tasks/completed/TASK-310-reconcile-task-309-review-findings.md`
- `tasks/TASK_LIFECYCLE.md`

---

## Scope

### In Scope

- Scope A: Record a reviewable, per-ability comparison for all five Bosses (`earthquake`, `flame-burst`, `drain-power`, `root`, `thunder-strike`), covering trigger/selection point, action replacement, attack eligibility, cost, cooldown, duration, effect magnitude, application/expiration timing, `NextAtkBuff` qualification and consumption timing, power changes, event ordering, and intentional divergences.
- Scope B: Reconcile known contradictions across approved documentation: the retired direct-damage/Burn reference table in `BOSS_RULES.md` §6, the authoritative zero-direct-damage/Burn behavior, the inconsistent gate labels referring to the removal gate as "Slice 4 parity gate", and distinguishing Slice 4 from Slice 5.
- Scope C: Prepare documentation-first amendments for ADR-029 §7, the ADR index, `BOSS_RULES.md`, `GAME_EVENTS.md`, `SIGNALR_PROTOCOL.md`, `GAME_STATE.md`, `DATABASE.md`, and `REDIS_STATE.md`.
- Scope D: Explicitly track and defer unrelated issues with blocker classification.

### Out of Scope

- Do not remove `SkillCharge` or the legacy `BossSkill` path.
- Do not remove or rename `BossSkillCast`.
- Do not change Boss ability values, costs, cooldowns, durations, or cast-selection policy.
- Do not change `CastPriorityScore = 85` or regenerate the approved simulation baseline.
- Do not implement the Fifth Gem.
- Do not register the eventual removal task as part of this task.
- Do not push changes.
- Do not amend, rebase, or rewrite existing commits.

---

## Declared File Set (P-2 / P-5)

```text
tasks/artifacts/TASK-311-adr-029-slice-5-removal-readiness-reconciliation.md
docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md
docs/03-decisions/README.md
docs/01-game-design/BOSS_RULES.md
docs/01-game-design/COMBAT_RULES.md
docs/02-technical/GAME_EVENTS.md
docs/02-technical/SIGNALR_PROTOCOL.md
docs/02-technical/GAME_STATE.md
docs/02-technical/DATABASE.md
docs/02-technical/REDIS_STATE.md
.ai/PROJECT_STATE.md
```

*(The TASK-311 record itself is excluded from the Phase A set.)*

---

## Acceptance Criteria

- [x] 1. The task was registered and executed in accordance with the actual repository lifecycle, with an exact declared file set.
- [x] 2. The five-Boss comparison explicitly covers the known `thunder-strike` same-round-consumption divergence and the `flame-burst`/`root` duration divergence.
- [x] 3. Every substantive comparison is traceable to source code, a named test, or an authoritative rule.
- [x] 4. The retired direct-damage/Burn table no longer contradicts the authoritative Boss Card rules, while historical legacy behavior remains accurately described where needed.
- [x] 5. Removal-gate terminology is consistent across the affected documents.
- [x] 6. ADR-029's removal requirements and ADR index amendments are prepared and internally consistent.
- [x] 7. Event, protocol, state, persistence, and frontend consumers that the future removal task must reconcile are explicitly enumerated.
- [x] 8. No runtime code, effect values, database schema, or serialized contracts are changed.
- [x] 9. `git diff --check` passes.
- [x] 10. Relevant documentation validation or repository-required checks pass.
- [x] 11. The final report lists changed files, verification evidence, deferred issues, and any unresolved operator decisions.
- [x] 12. The final report explicitly states that Slice 5 remains unauthorized and unimplemented, and that the Fifth Gem remains FUTURE.

---

## Stop Conditions

- If an approved documentation decision requires a gameplay or compatibility choice not already settled by ADR-029, STOP and request operator direction per `AGENTS.md` §20.
- If the actual repository state conflicts with TASK-310's findings, report the conflict and reassess before editing.
- Do not register the Slice 5 removal task or begin its implementation.

---

## Completion Evidence

### Commit

- Implementation slice (Phase A): `0207c4220984e7b3fe06256d3e661bd5262786a0`
- Subject: `docs: reconcile adr-029 slice 5 removal-readiness prerequisites`
- Owns: `TASK-311`
- Files: the exact 11-file declared set in §5 (staged-set equality verified: `staged=11 expected=11`, task record excluded)
- Shared with: `none`
- Unowned / pre-existing: `none`

### Changed Files

- `tasks/artifacts/TASK-311-adr-029-slice-5-removal-readiness-reconciliation.md` — comprehensive engineering artifact providing per-ability old-vs-new comparisons for all five Bosses, consumer inventory, deferred issues classification, and readiness assessment.
- `docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md` — header, §2.9 item 25, §4 (Slice 5), and §7 amended in place under documentation-first governance.
- `docs/03-decisions/README.md` — v1.25, table entry and in-place amendment summary updated for ADR-029 amendment.
- `docs/01-game-design/BOSS_RULES.md` — v2.17, §6 overview reference table reconciled to zero-direct-damage and Boss Card rules with historical context preserved; §4.4 gate terminology reconciled to Slice 5 removal gate.
- `docs/01-game-design/COMBAT_RULES.md` — v3.2, §2 POWER bullet and preamble gate terminology reconciled to Slice 5 removal gate.
- `docs/02-technical/GAME_EVENTS.md` — v2.20, `BossSkillCast` annotated with Slice 5 removal expectations; gate terminology reconciled.
- `docs/02-technical/SIGNALR_PROTOCOL.md` — v2.29, `BossSkillCast` §3.2.18 annotated with transitional status and Slice 5 expectations; §3.2.30 item 8 gate terminology reconciled.
- `docs/02-technical/GAME_STATE.md` — v2.34, four instances of "Slice 4 parity gate" reconciled to "Slice 5 removal gate".
- `docs/02-technical/DATABASE.md` — v1.42, `BossDefinition.SkillDefinition` annotated with Slice 5 removal scope and persistence disposition note.
- `docs/02-technical/REDIS_STATE.md` — v1.17, `BossState.SkillCharge` future retirement, tolerant-reader deserialization, and battle drain expectations annotated.
- `.ai/PROJECT_STATE.md` — v1.6, AI navigation cache refreshed for HEAD `36137e3`, TASK-310 completed, and TASK-311 in-flight/completed.

### Validation Results

```text
[x] dotnet build src/backend/GameServer.sln --nologo --verbosity q
      0 Error(s); pre-existing EF Core version warnings only
[x] Focused Application tests
      dotnet test src/backend/GameServer.sln --filter TASK309 --nologo
      38/38 passed (9 Infrastructure, 3 Domain, 18 Application, 8 Api)
[x] Client tests
      npm run test:run — 24 test files, 1271/1271 passed
[x] Client build
      npm run build — tsc && vite build exit 0
[x] git diff --check
      exit 0 (no whitespace or conflict errors)
```

### Server Authority & Scope Verification

- [x] Confirmed zero runtime code, gameplay values, or database schemas were modified.
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1, §3, §4); the Fifth Gem remains FUTURE.
- [x] Confirmed P-1…P-6 commit policy compliance; two-stage commit with exact declared file set.
- [x] Confirmed that Slice 5 remains unauthorized and unimplemented.
