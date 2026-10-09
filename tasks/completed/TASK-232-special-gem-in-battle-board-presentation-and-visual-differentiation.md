# TASK-232 — Special Gem In-Battle Board Presentation & Visual Differentiation

## Metadata

```text
Task ID:           TASK-232
Type:              FEATURE
Status:            DONE
Risk:              LOW
Priority:          HIGH
Primary Agent:     client
Supporting Agents: review
Workflow:          development/feature.md
Skills:            client/phaser-battle-presentation, client/client-event-projection, client/client-state-authority, phaser/scenes
Dependencies:      TASK-231
Declared Files:    src/frontend/client/src/game/scenes/BattleEventPresenter.ts, src/frontend/client/src/game/scenes/BattleScene.ts, src/frontend/client/scripts/standalone-web-smoke.mjs, src/frontend/client/tests/BattleEventPresentation.test.ts, src/frontend/client/tests/SceneLifecycle.test.ts
```

---

## Objective

Render authoritative `specialGem` metadata (`LineClear` Horizontal, `LineClear` Vertical, `Burst`, `Area`) on the in-battle Match-3 board with clear, accessible visual indicators while preserving underlying gem labels, normal-gem appearance, and the 128-child board container invariant. Correct the TASK-231 Enrage latch so once a boss becomes Enraged, its state and HUD indicator remain latched for the rest of that battle across healing and defeat, resetting only when a genuinely new battle starts or battle state is cleared.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Match-3 Board, Special Gems (LineClear, Burst, Area), Boss combat presentation
- `docs/01-game-design/MATCH3_RULES.md` §5 — Special Gems (Line Clear Gem horizontal/vertical, Burst Gem, Area Gem)
- `docs/01-game-design/BOSS_RULES.md` §5 item 4 — Enrage permanent state transition, strict `<` boundary, persistence for the rest of the battle
- `docs/02-technical/GAME_STATE.md` §2.1.4 — Special Gem Metadata and Orientation (`LineClear`, `Burst`, `Area`)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.10 — Special Gem transport representation
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4.1 item 5 — Delivering board Special Gem state without client inference
- `tasks/TASK_LIFECYCLE.md` §6 — Two-stage commit protocol (Phase A implementation slice commit, Phase B completion record commit)

---

## Scope

### In Scope
- Define visual presentation configurations and formatters for all four authoritative special gem variants (`LineClear` Horizontal, `LineClear` Vertical, `Burst`, `Area`) in `BattleEventPresenter.ts`.
- Render special gem visual differentiators in `BattleScene.ts` (distinct 3px stroke border styling in accent colors and accessible badge tags on labels), while preserving normal-gem appearance (1px stroke `0x0b0f19`), underlying gem labels, and child count (exactly 128 children for 64 cells).
- Fix the Enrage state latch in `BattleScene.ts` and `BattleEventPresenter.ts` so once Enraged, the state and HUD `[ENRAGED]` tag stay latched through healing and defeat until battle reset or transition.
- Ensure `standalone-web-smoke.mjs` matching-swap search preserves matching-swap invariants when special gems are present by normalizing to underlying base gem labels.
- Add unit and scene lifecycle tests for all special gem variants, normal gems, base label parsing, and Enrage latch persistence.
- Follow the two-stage commit protocol established under TASK-230.

### Out of Scope
- Modifying backend game logic or SignalR wire schemas.
- Adding client-side inference of special gem types from match patterns.
- Modifying non-declared smoke scripts or documentation contracts.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-232:
```text
src/frontend/client/src/game/scenes/BattleEventPresenter.ts
src/frontend/client/src/game/scenes/BattleScene.ts
src/frontend/client/scripts/standalone-web-smoke.mjs
src/frontend/client/tests/BattleEventPresentation.test.ts
src/frontend/client/tests/SceneLifecycle.test.ts
```

---

## Changes Implemented

1. **Special Gem Visual Presentation (`BattleEventPresenter.ts`)**:
   - Added `SpecialGemVisualVariant`, `SpecialGemPresentationConfig`, and `SPECIAL_GEM_PRESENTATIONS` mapping each authoritative special gem variant to visual stroke styling and badge indicators:
     - `LineClearHorizontal`: Sky blue border (`0x38bdf8`), 3px stroke, `[H]` badge.
     - `LineClearVertical`: Sky blue border (`0x38bdf8`), 3px stroke, `[V]` badge.
     - `Burst`: Golden yellow border (`0xfacc15`), 3px stroke, `[BURST]` badge.
     - `Area`: Vibrant pink border (`0xf472b6`), 3px stroke, `[AREA]` badge.
   - Implemented `resolveSpecialGemPresentation(specialGem)` with safe handling of omitted orientation and unrecognized types.
   - Implemented `formatGemCellLabel(baseLabel, specialGem)` which appends the badge while keeping the base label intact for normal gems.
   - Implemented `parseGemBaseLabel` and `parseSpecialGemBadge` helpers for lossless parsing.

2. **In-Battle Board Rendering & Enrage Latch (`BattleScene.ts`)**:
   - Updated `drawCell` to accept optional `specialGem` metadata and render distinct 3px stroke borders and formatted labels for special gems, with font sizing set to 11px to fit comfortably within the 56px tile.
   - Preserved normal gem appearance: 1px stroke `0x0b0f19` and 14px font size.
   - Preserved board container child-count invariant: exactly 1 rectangle tile and 1 text label per cell (128 children for 64 cells).
   - Fixed the Enrage latch: tracked `currentBattleId`, latched `bossEnraged = true` upon initial threshold cross (`hp < EnrageThreshold`), kept it latched through healing and defeat (hp <= 0), and reset only when `state === null` or when `state.battleId` changes to a new battle.

3. **E2E Matching-Swap Invariant Preservation (`standalone-web-smoke.mjs`)**:
   - Updated `findMatchingSwaps` to parse base gem types (`l.split(' ')[0]`) so swaps involving special gems continue to evaluate valid Match-3 patterns accurately.

4. **Unit & Lifecycle Testing**:
   - `BattleEventPresentation.test.ts`: Added unit tests for `resolveSpecialGemPresentation`, `formatGemCellLabel`, `parseGemBaseLabel`, `parseSpecialGemBadge`, and `isBossEnraged` latch persistence across healing and defeat.
   - `SceneLifecycle.test.ts`: Updated `createSceneHarness` tile mock to record stroke width and color, and added lifecycle tests verifying board rendering across all four special gem variants, child count (128), and Enrage persistence across healing, defeat, and battle reset.

---

## Verification Evidence

- `npm run test:run` in `src/frontend/client`: 22 test files, 911 tests passing.
- `npx tsc --noEmit` in `src/frontend/client`: 0 errors.
- `dotnet test src/backend/GameServer.sln`: 4 test projects, 2,970 tests passing.
- `npm run verify:viewport` in `src/frontend/client`: All responsive viewports verified without errors.

---

## Commit Protocol

- Phase A Implementation Commit: `d7c5ae72f36e115386b6394bb414a8607701e8d5`
- Phase B Completion Record Commit: (this commit)
