# TASK-231 — Boss Interaction & Combat Readability Presentation Pass

## Metadata

```text
Task ID:           TASK-231
Type:              FEATURE
Status:            DONE
Risk:              LOW
Priority:          HIGH
Primary Agent:     client
Supporting Agents: review
Workflow:          development/feature.md
Skills:            client/phaser-battle-presentation, client/client-event-projection, client/client-state-authority, phaser/scenes
Dependencies:      TASK-230
Declared Files:    src/frontend/client/src/game/scenes/BattleEventPresenter.ts, src/frontend/client/src/game/scenes/BattleScene.ts, src/frontend/client/scripts/standalone-web-smoke.mjs, src/frontend/client/tests/BattleEventPresentation.test.ts, src/frontend/client/tests/SceneLifecycle.test.ts
```

---

## Objective

Enhance player-facing combat feedback readability during battle by mapping all five canonical Boss Skill identities to their display names from `BOSS_RULES.md`, presenting Boss Enrage transitions clearly in the HUD and transient callout feed while suppressing redundant callouts when state has not changed, and mapping CardCast and Swap rejection codes to player-friendly messages with safe fallbacks for unrecognized codes.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — MVP Phase 2 Boss content and combat presentation
- `docs/01-game-design/BOSS_RULES.md` §5 item 4 — Enrage permanent state transition and strict `<` boundary
- `docs/01-game-design/BOSS_RULES.md` §6.1 — MVP Boss base stats and Enrage thresholds (Hỏa Long 1500, Thủy Ma 1500, Mộc Yêu 1500, Sơn Thạch Vệ 1500, Kim Lôi Vương 2100)
- `docs/01-game-design/BOSS_RULES.md` §6.3, §6.4 — MVP Boss Skills: Flame Burst (`flame-burst`), Drain Power (`drain-power`), Root (`root`), Earthquake (`earthquake`), Thunder Strike (`thunder-strike`)
- `docs/01-game-design/MATCH3_RULES.md` §2.1.2, §2.1.4 — Swap validation checks and rejection reasons (`NO_MATCH_FROM_SWAP`, `INVALID_SWAP`, `INVALID_CELL_INDEX`, `STALE_ACTION`)
- `docs/01-game-design/CARD_RULES.md` §3 — Card cast validation and rejection reasons (`INSUFFICIENT_POWER`, `CARD_CAST_ALREADY_USED_THIS_TURN`, `CARD_NOT_IN_LOADOUT`, `INVALID_CARD`)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.18 — `BossSkillCast` wire event schema
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4.4 — `bossState` projection (`bossId`, `hp`, `maxHp`)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §5 item 3 — Swap, CardCast, and PetSkillCast acknowledgement rejection contract
- `tasks/TASK_LIFECYCLE.md` §6 — Two-stage commit protocol (Phase A implementation slice commit, Phase B completion record commit)

---

## Scope

### In Scope
- Map all five canonical `BossSkillCast.skillId` values to their display names from `BOSS_RULES.md` §6.3 / §6.4 (`Flame Burst`, `Drain Power`, `Root`, `Earthquake`, `Thunder Strike`), falling back cleanly to `'BOSS SKILL'` when unrecognized.
- Detect Boss Enrage state transitions from authoritative `bossState` updates (`hp < EnrageThreshold`), present the `[ENRAGED]` indicator in the Boss HUD, and emit a `'BOSS ENRAGED'` callout on initial transition while preventing duplicate callouts when Enrage state remains unchanged.
- Map Swap and CardCast/PetSkillCast rejection codes to player-friendly messages (`NO_MATCH_FROM_SWAP`, `INVALID_SWAP`, `INVALID_CELL_INDEX`, `STALE_ACTION`, `INSUFFICIENT_POWER`, `CARD_CAST_ALREADY_USED_THIS_TURN`, `CARD_NOT_IN_LOADOUT`, `INVALID_CARD`, `BATTLE_NOT_FOUND`) with safe fallbacks for unrecognized codes.
- Display player-friendly rejection feedback via the prominent combat callout feed in `BattleScene`.
- Add unit and scene lifecycle tests covering all five boss skills, Enrage transitions, and rejection mappings.
- Follow the two-stage commit protocol established under TASK-230.

### Out of Scope
- Backend or SignalR protocol schema modifications.
- Introducing new client-side game logic or authoritative state calculation.
- Expanding MVP boss content beyond the five canonical bosses.

---

## Current State

`BattleEventPresenter` formats `BossSkillCast` as generic `'BOSS SKILL'` with no skill name resolution. Boss HUD displays only the boss display name with no indication of Enraged state, and no callout is issued upon crossing the Enrage threshold. Action rejections display raw machine-readable codes in small transport feedback labels with no friendly callouts.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-231:
```text
src/frontend/client/src/game/scenes/BattleEventPresenter.ts
src/frontend/client/src/game/scenes/BattleScene.ts
src/frontend/client/scripts/standalone-web-smoke.mjs
src/frontend/client/tests/BattleEventPresentation.test.ts
src/frontend/client/tests/SceneLifecycle.test.ts
```

---

## Acceptance Criteria

- [x] All five canonical `BossSkillCast.skillId` values resolve to display names: `Flame Burst`, `Drain Power`, `Root`, `Earthquake`, `Thunder Strike`.
- [x] Unknown `BossSkillCast.skillId` falls back safely to `'BOSS SKILL'`.
- [x] Boss Enrage transition (`hp < EnrageThreshold`) displays `[ENRAGED]` in the Boss HUD and emits `'BOSS ENRAGED'` callout.
- [x] Repeated state updates while already enraged do NOT produce redundant Enrage callouts.
- [x] Swap and CardCast rejection codes map to clear player-friendly feedback with safe fallbacks for unknown codes.
- [x] Unit tests in `BattleEventPresentation.test.ts` verify all five boss skills, Enrage evaluation, and rejection code formatting.
- [x] Lifecycle tests in `SceneLifecycle.test.ts` verify boss skill presentation, Enrage transitions, and rejection callouts in `BattleScene`.
- [x] All client test suites pass with zero failures and TypeScript type-check succeeds.
- [x] Exact declared-file Phase A implementation commit followed by dedicated Phase B completion-record commit per TASK-230.

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)
[x] src/frontend/client/ (scenes / runtime / services / state / ui)
[x] tests/ (unit / integration / gameplay scenarios)
[ ] docs/ (documentation updates if applicable)
```

---

## Implementation Notes

- `BOSS_RULES.md` §6.1 fixes Enrage thresholds: 1500 for Hỏa Long (5000 MaxHP), 1500 for Thủy Ma (5000 MaxHP), 1500 for Mộc Yêu (5000 MaxHP), 1500 for Sơn Thạch Vệ (3000 MaxHP), and 2100 for Kim Lôi Vương (2800 MaxHP). Strict `<` boundary is authoritative.
- `BattleScene.renderBossHud` evaluates Enrage state from authoritative `bossState` and handles state tracking to ensure transition callout fires at most once per battle.
- Existing source guard in `CastFeedbackWaitPredicate.test.ts` asserts literal `: rejected (${acknowledgement.reason ?? 'unknown'}).` in `BattleScene.ts`, which is preserved in transport status.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — BattleEventPresenter boss skill lookup, isBossEnraged, formatSwapRejection, formatCardCastRejection
[x] Integration tests  — BattleScene presentation of boss skills, Enrage transitions, and rejection callouts
[x] Full test pass     — Client test suite passes cleanly
```

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If task requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose

---

## Completion Evidence

### Commit
- Implementation slice (Phase A): `53bf1970f4bd5e5ea23d52b6ce4af6c2e310500b`
- Subject: `feat: present canonical boss skills, enrage state, and action rejection feedback`
- Owns: `TASK-231`
- Files: `src/frontend/client/src/game/scenes/BattleEventPresenter.ts, src/frontend/client/src/game/scenes/BattleScene.ts, src/frontend/client/scripts/standalone-web-smoke.mjs, src/frontend/client/tests/BattleEventPresentation.test.ts, src/frontend/client/tests/SceneLifecycle.test.ts`
- Shared with: `none`
- Unowned / pre-existing: `none`

### Changed Files
- `src/frontend/client/src/game/scenes/BattleEventPresenter.ts` — Added `BOSS_SKILL_NAMES` canonical mapping for all 5 MVP skills, `resolveBossSkillDisplayName`, `BOSS_ENRAGE_THRESHOLDS`, `isBossEnraged` with strict `<` boundary, `SWAP_REJECTION_MESSAGES`, `formatSwapRejection`, `CARD_CAST_REJECTION_MESSAGES`, and `formatCardCastRejection`. Exported `CALLOUT_BOSS_COLOR`.
- `src/frontend/client/src/game/scenes/BattleScene.ts` — Updated `renderBossHud` to present `[ENRAGED]` tag on boss name and emit `'BOSS ENRAGED'` callout on state transition while tracking `bossEnraged` to avoid duplicate callouts. Updated `renderSwapStatus` and `renderCastStatus` to show player-friendly rejection callouts via `showCallout` while preserving exact `: rejected (${acknowledgement.reason ?? 'unknown'}).` transport status strings.
- `src/frontend/client/scripts/standalone-web-smoke.mjs` — Updated `isPlayerFacingCallout` allow-list to include canonical `BOSS SKILL: <name>`, `BOSS ENRAGED`, and rejection callouts.
- `src/frontend/client/tests/BattleEventPresentation.test.ts` — Added 5 unit tests covering all 5 canonical boss skills, unknown skill fallback, `isBossEnraged` boundaries and defeat conditions, `formatSwapRejection` canonical codes & safe fallbacks, and `formatCardCastRejection` canonical codes & safe fallbacks.
- `src/frontend/client/tests/SceneLifecycle.test.ts` — Updated TASK-210 test assertion for `flame-burst` to `'BOSS SKILL: Flame Burst'`. Added 3 comprehensive scene lifecycle tests validating all 5 boss skills callout presentation, Enrage HUD indicator & transition callout with duplicate suppression on subsequent state pushes, and Swap & CardCast rejection player-friendly callouts and transport status text.

### Validation Results
- Client vitest suite: PASS (22 files, 906 tests)
- Client TypeScript type-check (`tsc --noEmit`): PASS (0 errors)
- Backend .NET test suite (`GameServer.sln`): PASS (2,970 tests)

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed P-1…P-6 commit policy compliance
