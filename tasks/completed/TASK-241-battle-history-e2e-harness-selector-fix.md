# TASK-241 — Battle History E2E Harness Selector Fix

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section; does not copy rules or schemas.
  Created following verified implementation of the battle history smoke harness selector fix.
-->

<!--
  Lifecycle: created BACKLOG 2026-10-10 upon authorization to formalize the verified harness fix.
-->

<!--
  Lifecycle: BACKLOG → READY completed 2026-10-10 after validating tasks/TASK_LIFECYCLE.md §3 READY criteria:
    [x] Task type confirmed (BUG — tasks/TASK_TYPES.md §2, development/bug-fix.md;
        harness selector bug in test script)
    [x] Relevant documentation exists in docs/ (MVP_SCOPE.md §1, API_CONTRACTS.md §4.5, ARCHITECTURE.md §2.2)
    [x] MVP scope confirmed (MVP_SCOPE.md §1 — test harness stability)
    [x] Not blocked: Dependencies — None
    [x] Primary agent assigned (testing), supporting agents assigned (review)
    [x] Workflow assigned (development/bug-fix.md)
    [x] Acceptance criteria are binary and testable
    [x] Uniqueness: TASK-240 is highest completed ID; TASK-241 is next sequential ID
    [x] Ownership: tasks/active/ and tasks/blocked/ hold no open record
-->

<!--
  Lifecycle: READY → IN PROGRESS completed 2026-10-10: implementation verified in
  src/frontend/client/scripts/battle-history-smoke.mjs; working tree and diff inspected;
  all governance and scope boundaries verified.
-->

<!--
  Lifecycle: IN PROGRESS → IN REVIEW completed 2026-10-10: quality review confirms
  minimal bounded fix, no production changes, no gameplay or contract modifications,
  and fail-fast assertion in place.
-->

<!--
  Lifecycle: IN REVIEW → DONE completed 2026-10-10: Phase A implementation slice
  committed as ce15e07f7e1d617d33f94f74c1a8a6825fb00a61, and this completion record
  is filed under its own Phase B commit.
-->

---

## Metadata

```text
Task ID:           TASK-241
Type:              BUG
Status:            DONE
Risk:              LOW
Priority:          LOW
Primary Agent:     testing
Supporting Agents: review
Workflow:          development/bug-fix.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis, testing/test-scenario-generation, quality/scope-validation, quality/implementation-review
Dependencies:      None
Declared Files:    src/frontend/client/scripts/battle-history-smoke.mjs
```

---

## Objective

Formalize and close the verified Battle History E2E smoke harness selector fix according to repository task governance, resolving the pre-existing Lobby start-battle selector defect (recorded upstream as TASK-221 R-1 and MVP acceptance audit F-1) where `battle-history-smoke.mjs` selected the first interactive rectangle (`< BACK`) instead of the visible `START BATTLE` control, causing accidental navigation to `MainMenuScene` and timeout waiting for `BattleScene`.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — End-to-end browser smoke verification in MVP scope.
- `docs/02-technical/API_CONTRACTS.md` §4.5 — Battle History endpoint contract.
- `docs/02-technical/ARCHITECTURE.md` §2.2 — Standalone web client structure.
- `tasks/completed/TASK-221-content-ownership-and-relic-loadout.md` §10 (R-1) — Defect identification and attribution.
- `tasks/artifacts/MVP-ACCEPTANCE-RELEASE-CLOSURE-AUDIT.md` §2.3, §2.4, §6.1 (F-1) — Root-cause analysis, reproduction, and acceptance classification.
- `tasks/TASK_LIFECYCLE.md` §6 — Two-stage commit protocol (Phase A implementation slice, Phase B completion record filing).
- `AGENTS.md` §§9, 10, 13, 14, 16; `.ai/workflow/development/bug-fix.md` §§1–4 — Bug classification, minimal fix discipline, server authority, scope discipline.

---

## Scope

### In Scope

1. **`src/frontend/client/scripts/battle-history-smoke.mjs`**:
   - Extend `LOBBY_SNAPSHOT` evaluation to map interactive rectangle objects with their matching text labels into a `controls` collection.
   - Introduce `lobbyControlsOf(snap)` and `lobbyControlOf(snap, label)` helper functions to look up controls deterministically by caption and throw descriptive errors when absent.
   - In Phase 4 (first battle) and Phase 7 (second battle), resolve `START BATTLE` by caption (`lobbyControlOf(lobby, 'START BATTLE')`) and click its center coordinates.
   - Add fail-fast checks in both battle-start condition waiters to throw immediately if the active scene transitions to `MainMenuScene` instead of `BattleScene`.
2. **Repository Governance**:
   - Execute the two-stage commit protocol (Phase A implementation slice, Phase B completion record filing).
   - Preserve untracked audit artifact `tasks/artifacts/MVP-ACCEPTANCE-RELEASE-CLOSURE-AUDIT.md`.

### Out of Scope

- Modifying `boss-selection-smoke.mjs` or other smoke scripts.
- Retiring or redesigning legacy smoke scripts.
- Harmonizing SignalR console-noise filters or logging.
- Any change to production code (`src/frontend/client/src/`, `src/backend/`).
- Any change to gameplay behavior, rules, database schema, API contracts, or SignalR protocol.
- Reopening Pet Passive mechanical effects.
- Pushing commits, merging, or deploying.
- Creating follow-up tasks for unrelated harness maintenance.
- Modifying existing completed task records.
- Staging or deleting untracked audit artifact `tasks/artifacts/MVP-ACCEPTANCE-RELEASE-CLOSURE-AUDIT.md`.

---

## Current State

In `src/frontend/client/scripts/battle-history-smoke.mjs`, the start button was resolved using `lobby.interactive.find((o) => o.type === 'Rectangle' && o.enabled)`. When TASK-211 added the `< BACK` navigation button before `START BATTLE` in the Lobby scene's display list, that query began selecting `< BACK`, causing the test to navigate back to `MainMenuScene` and time out after 15,000ms. This was recorded as defect R-1 in TASK-221 §10 and finding F-1 in `tasks/artifacts/MVP-ACCEPTANCE-RELEASE-CLOSURE-AUDIT.md`. Resolving the button by its visible caption `'START BATTLE'` mirrors the stable pattern used in `standalone-web-smoke.mjs`.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-241 (1 file):
```text
src/frontend/client/scripts/battle-history-smoke.mjs
```

Evidence per declared path:

| Path | Action | Evidence |
|---|---|---|
| `src/frontend/client/scripts/battle-history-smoke.mjs` | Edit | Map controls with text labels in `LOBBY_SNAPSHOT`, resolve `START BATTLE` by caption in Phase 4 and Phase 7, and add fail-fast condition waiter check for accidental `MainMenuScene` navigation. |

*(Note: The task manifest file itself is excluded from the implementation declared file set. The list above strictly matches the `Declared Files:` field in `## Metadata`.)*

---

## Acceptance Criteria

- [x] Lobby controls are resolved by their visible captions (`'START BATTLE'`).
- [x] Both battle-start interactions (Phase 4 and Phase 7) select `START BATTLE`.
- [x] Fail-fast assertion in both battle-start condition waiters catches accidental navigation to `MainMenuScene`.
- [x] Only test-harness code changed (`src/frontend/client/scripts/battle-history-smoke.mjs`).
- [x] No production source, gameplay rules, database schema, API contract, or SignalR protocol changed.
- [x] Untracked audit artifact `tasks/artifacts/MVP-ACCEPTANCE-RELEASE-CLOSURE-AUDIT.md` is preserved intact.
- [x] Two-stage commit protocol (Phase A implementation slice, Phase B completion record) followed exactly per `tasks/TASK_LIFECYCLE.md` §6.
- [x] No git push occurred.

---

## Affected Files & Areas

```text
[ ] src/backend/ (no backend changes)
[ ] src/frontend/client/src/ (no production client changes)
[x] src/frontend/client/scripts/ (battle-history-smoke.mjs)
[ ] docs/ (no documentation changes)
```

---

## Implementation Notes

- `LOBBY_SNAPSHOT` extracts active text objects and rectangle objects from `scene.children.list`, pairing rectangle buttons with co-located text objects by `(x, y)` coordinate match to populate `controls: [{ label, x, y, width, height }, ...]`.
- Helper `lobbyControlOf(snap, label)` searches `snap.controls` for matching `label` and throws `new Error(\`LobbyScene rendered no "\${label}" control.\`)` if not found.
- Both Phase 4 and Phase 7 condition checks inspect `scene === 'MainMenuScene'` and throw `new Error('Lobby activated < BACK and returned to MainMenuScene instead of starting battle.')` to fail immediately rather than waiting for timeout.

---

## Testing Requirements

### Required Verification
```text
[x] E2E Smoke suite  — npm run verify:e2e:history in src/frontend/client
[x] Zero regression  — No production code touched
```

### Commands
```text
npm run verify:e2e:history   # in src/frontend/client
```

---

## Stop Conditions

- If resolving the button requires changes to `LobbyScene.ts` or any production frontend/backend code: STOP and report.
- If changes to `battle-history-smoke.mjs` alter game rules, API assertions, or verification standards: STOP and report.
- If widening beyond the single declared file is required: STOP per `tasks/TASK_LIFECYCLE.md` §6.1 / P-5.

---

## Completion Evidence

### Commit
- Implementation slice (Phase A): `ce15e07f7e1d617d33f94f74c1a8a6825fb00a61`
- Subject: `fix: resolve lobby start battle control by caption in history smoke`
- Owns: `TASK-241`
- Files: `src/frontend/client/scripts/battle-history-smoke.mjs`
- Shared with: `none`
- Unowned / pre-existing: `none`
- Phase B: this completion record alone, filed for the first time under its own bookkeeping commit (`tasks/completed/TASK-241-battle-history-e2e-harness-selector-fix.md`).

### Changed Files
- `src/frontend/client/scripts/battle-history-smoke.mjs` — In `LOBBY_SNAPSHOT`, added mapping of interactive rectangle controls with co-located text labels into `snap.controls`. Added `lobbyControlsOf(snap)` and `lobbyControlOf(snap, label)`. Updated Phase 4 and Phase 7 battle-start sequences to click the button resolved by caption `'START BATTLE'`. Added fail-fast condition check in both phases to throw an immediate descriptive error if `MainMenuScene` is reached instead of `BattleScene`.

### Validation Results
- Recorded execution evidence: The suite previously executed successfully across 2 runs (`npm run verify:e2e:history`), completing 51/51 checks in each run (total 102/102 checks passing, 0 failures). The 14 generated timestamped PNG screenshots in `src/frontend/client/battle-history-shots/` (`run1-01` through `run1-07`, `run2-01` through `run2-07`) confirm end-to-end progression through empty history, lobby, real battle resolution, result presentation, single-entry history, second battle resolution, and two-entry history.
- Runtime environment status during closure: Verification was re-tested in the current environment via `npm run verify:e2e:history` in `src/frontend/client`. The local backend service (port 5000) and frontend Vite server (port 5173) are currently offline, producing the expected preflight message `[BATTLE HISTORY SMOKE FATAL ERROR] Backend service not reachable at http://localhost:5000/health (fetch failed)`. Per repository policy, this environmental limitation is recorded directly rather than fabricating new run output.
- Working tree integrity: Verified that zero production source files, database models, API contracts, SignalR protocols, or game design documents were touched.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed no production files modified
- [x] Confirmed P-1…P-6 commit policy compliance
