# TASK-293 — Remove Retired Special Gem Model and Presentation from the Frontend

---

## Metadata

```text
Task ID:           TASK-293 (highest existing ID on disk is TASK-292)
Type:              BUG
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     client
Supporting Agents: testing, review, realtime
Workflow:          development/bug-fix.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis,
                   client/client-event-projection, client/phaser-battle-presentation,
                   realtime/realtime-protocol-validation,
                   testing/test-scenario-generation, quality/implementation-review
Dependencies:      ADR-028 (Accepted); TASK-292 (DONE — backend retirement);
                   TASK-286, TASK-287, TASK-289 (DONE — reconciled contracts)
Declared Files:    see ## Declared File Set (P-2 / P-5)
```

<!--
  TASK TYPE. The requested behavior (ordinary functional Gem types only, no
  Special Gems) already matches docs/01-game-design/ and docs/02-technical/ — it
  was reconciled by TASK-286/TASK-287/TASK-289 and implemented on the backend by
  TASK-292. `development/gameplay-change.md` §2 therefore rules out
  GAMEPLAY-CHANGE ("if the requested behavior already matches docs/…: this is not
  a gameplay change"). The defect is that the frontend implementation and its
  tests still deviate from those approved contracts, which is `development/
  bug-fix.md` §1's "Implementation bug" (plus several "Test bug" items, §4).
  No game rule, resource value, or wire shape is changed by this task.

  RISK DEVIATION (disclosed). tasks/TASK_TYPES.md §4 gives BUG a LOW–MEDIUM
  baseline. MEDIUM is selected because the slice touches the SignalR/REST
  deserialization models and the whole battle presentation pipeline (board
  views, animation/effect queues, per-phase pacing, the Move Log), and because a
  missed reference would break board hydration rather than fail loudly.
  Validation is nevertheless run at the task-requested depth, which exceeds
  MEDIUM: focused tests, the full frontend suite, the production build,
  type checking, gameplay-scenario-level match/mismatch presentation tests, and
  a source-level boundary test that the retired contract cannot reappear.
-->

---

## Objective

Remove the retired Special Gem model and every presentation path that exists only
to support it from `src/frontend/client/`, so the client models and renders board
cells as ordinary functional Gem types (`ATK`, `DEF`, `HP`, `POWER`) only and its
deserializers match the wire contract TASK-292 left in place. Ordinary match
feedback, match resolution presentation, gravity/drop animation, cascades, board
reconciliation, combat/passive effects, input locking and turn completion are
preserved unchanged.

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-028-retire-special-gems-from-battle-state-model.md`
  — the governing decision (Status: Accepted); §5 retires the client's Special
  Gem interfaces/presentation, §6 fixes the deployment policy, and §7 assigns
  this slice
- `docs/00-overview/MVP_SCOPE.md` §1 — Match 3/4/5 and L/T remain IN as
  **matches**; this is a reduction, not new content
- `docs/02-technical/GAME_STATE.md` §2.1.1–§2.1.4 (pure `GemType` cells,
  special-gem retirement), §2.1.5 item 3 (Spawn produces ordinary gems),
  §2.1.7 (serialization / tolerant reader), §2.1.8 (resolution), §2.1.9 item 3
  (transport projections) and §4 (client presentation state)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.6 item 6 (no
  `createdSpecialGems`), §3.2.9 item 3 (no `specialGem`), §3.2.10/§3.2.11
  (retired), §3.2.12 (no member outside the schema), §4.1 items 5–6 (board
  carries pure `GemType`; the client derives nothing), §8
- `docs/02-technical/GAME_EVENTS.md` §1–§2 (events the client presents)
- `docs/01-game-design/MATCH3_RULES.md` §1.A, §4.4, §4.5 item 7, §5 (retired),
  §6 (`docs/01-game-design/GAME_RULES.md` §6, §17, §18;
  `docs/01-game-design/COMBAT_RULES.md` §2–§4)
- `docs/02-technical/TDD.md` §2.1 (client responsibility split), §6 (RNG)
- `docs/02-technical/API_CONTRACTS.md` §3 (battle-start state summary)
- `tasks/completed/TASK-292-…md` — the backend contract this task must match
- `tasks/backlog/TASK-275-…md` and `tasks/backlog/TASK-285-…md` — the
  presentation behaviors those tasks introduced and this task must NOT regress

---

## Scope

### In Scope

- Delete the retired Special Gem types/props from the active client models:
  `SpecialGemPayload`, `BattleStartSpecialGem`, `RuntimeSpecialGem`, the
  `specialGem` member of `CellPayload` / `BattleStartCell` / `RuntimeCell`, and
  the `specialGem` / `createdSpecialGems` members of `PresentedGemMatched` /
  `PresentedMatchCreated`.
- Delete the deserialization/normalization for those members
  (`GameRuntime.readSpecialGem`, and the event parser branches).
- Delete the retired presentation: `SPECIAL_GEM_PRESENTATIONS`,
  `resolveSpecialGemPresentation`, `formatGemCellLabel`, `parseGemBaseLabel`,
  `parseSpecialGemBadge`, the activation-occurrence/tally helpers, the
  `special-activation` and `summary` presentation phase/action vocabulary, the
  `BattleScene` activation overlay and per-turn tally, the `GemView` per-cell
  content decoration, and the `createdSpecialGems` handling in
  `presentMatchDestruction` / `replayBoardDeltas`.
- Remove the presentation constants, helpers and doc comments those paths owned
  (timing-policy `activation`/`summary` entries, `ACTIVATION_*`,
  `TURN_SUMMARY_*`, `passRangeEnd`, the creation-claim replay).
- Delete the obsolete tests that asserted retired behavior; adapt survivors; add
  the regression coverage listed in §5 of the request.

### Out of Scope

- Any backend file (`src/backend/`, `tests/backend/`), the wire protocol,
  `docs/`, any ADR, `docs/03-decisions/README.md`, or `tasks/completed/`.
- Redis data, migrations, or deployment: ADR-028 §3/§6 keeps the approved
  drain-then-joint-deploy procedure, and no compatibility shim is invented.
- Balance evidence, simulations, or expected combat outcomes.
- Any client-side gameplay authority, RNG, or re-derivation of server values.
- In-place migration of a stored board that still carries a retired member: the
  tolerant reader ignores it (`GAME_STATE.md` §2.1.7 item 4) and that behavior is
  asserted, not implemented anew.

---

## Current State

TASK-292 removed the retired model from the Domain/Api and from the Redis JSON
mapping, so the emitted `board` cells are pure `gemType` and `MatchCreated` /
`GemMatched` carry only their ordinary members. The frontend still declared
`SpecialGemPayload` / `BattleStartSpecialGem` / `RuntimeSpecialGem`, still
expected `cell.specialGem` on every board payload it read, still parsed
`createdSpecialGems` / `specialGem` off the event stream, and still rendered
per-cell badges, activation rings and a per-turn activation tally — a
presentation path with no producer.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-293 (reconciled to the final changed
set before Phase A):

```text
src/frontend/client/src/game/runtime/GameRuntime.ts
src/frontend/client/src/game/runtime/GameRuntimeEvents.ts
src/frontend/client/src/game/scenes/BattleEventPresenter.ts
src/frontend/client/src/game/scenes/BattleScene.ts
src/frontend/client/src/services/api/ApiService.ts
src/frontend/client/src/services/api/BattleModels.ts
src/frontend/client/src/services/realtime/SignalRService.ts
src/frontend/client/tests/BattleEventPresentation.test.ts
src/frontend/client/tests/BattleService.test.ts
src/frontend/client/tests/GameRuntime.test.ts
src/frontend/client/tests/RuntimeBoundaries.test.ts
src/frontend/client/tests/SceneLifecycle.test.ts
src/frontend/client/tests/SignalRService.test.ts
```

*(13 paths — 7 under `src/frontend/client/src/`, 6 under
`src/frontend/client/tests/` — and no deletions: the retired modules are removed
member-by-member from files that keep their surviving role. The task record
itself is excluded from the declared set — `tasks/TASK_TEMPLATE.md`.
Shared-file determination: no other task holds an authorized active manifest over
any of these paths; TASK-292 owns the disjoint `src/backend/` and `tests/backend/`
paths.)*

---

## Acceptance Criteria

- [x] AC-1 No active frontend model declares or references `SpecialGem*`,
  `specialGem`, `createdSpecialGems`, `SpecialGemPayload`,
  `BattleStartSpecialGem`, `RuntimeSpecialGem`, `SpecialGemVisualVariant`,
  `SPECIAL_GEM_PRESENTATIONS`, `resolveSpecialGemPresentation`,
  `formatGemCellLabel`, `parseSpecialGemBadge`, `parseGemBaseLabel`,
  `SpecialActivationOccurrence`, `specialActivationOccurrences`,
  `summarizeSpecialActivations`, `formatSpecialActivationSummary`, or the
  `special-activation` / `turn-summary` / `summary` presentation vocabulary.
- [x] AC-2 `CellPayload`, `BattleStartCell` and `RuntimeCell` each declare exactly
  one member — the delivered `gemType` — asserted structurally by a boundary test.
- [x] AC-3 `MatchCreated` carries only `shape`, `cells`, `gemType`,
  `cascadeDepth`; `GemMatched` only `cellIndex` and `gemType`. A payload that
  still carries a retired member parses to that ordinary shape (tolerant reader).
- [x] AC-4 The board renders `ATK`, `DEF`, `HP`, `POWER` with one uniform tile
  treatment and an undecorated type label; no per-cell overlay, badge, ring or
  second styling exists.
- [x] AC-5 Match-4, Match-5 and L/T resolve and animate as ordinary confirmed
  matches — the delivered swap/match/gravity/refill/settle sequence — with no
  creation, activation, detonation, chain or area-clear step.
- [x] AC-6 The presentation timeline contains no activation or tally phase; the
  Move Log reports the delivered event's own members only.
- [x] AC-7 Board hydration, per-pass Gravity/Spawn presentation, cascade ordering,
  supersession/recovery convergence, input locking and turn completion are
  unchanged and still covered (three-pass cascade coherence, Boss/Player shared
  pipeline, duplicate-batch idempotency).
- [x] AC-8 Obsolete tests that only asserted retired behavior are removed;
  relevant synchronization and animation coverage is replaced rather than
  weakened, and the ten coverage areas of §5 of the request are satisfied.
- [x] AC-9 Focused tests, the full frontend suite, `tsc` type checking and the
  production build pass.
- [x] AC-10 `git diff --check` is clean and a final source sweep finds no retired
  symbol in active frontend code.
- [x] AC-11 No backend file, `docs/` file, ADR, decision index, or task history is
  modified by this task slice.
- [x] AC-12 Quality review checklist passes (`.ai/workflow/quality/review.md` §1).

---

## Affected Files & Areas

```text
[ ] src/backend/           NOT TOUCHED (completed by TASK-292)
[x] src/frontend/client/   (models / runtime / scenes / services)
[x] tests/ (frontend unit + presentation scenarios)
[ ] docs/                  NOT TOUCHED (reconciled by TASK-287/TASK-289; ADR-028 §5 owns the decision)
```

---

## Implementation Notes

- `BattleScene.gemContentKeyFor` reduces to the delivered `gemType`; the single
  clear-confirmation gate (`clearConfirmedCell`) compares that one key, so a
  delivered clear still empties a cell only when the rendered content is what the
  event confirms.
- `PRESENTATION_TIMING` loses its `activation` and `summary` entries with the
  phases that used them; every surviving phase hold is still derived by
  `phaseHoldMs`/`phaseEffectMs` and still bounded by `trackPhaseAnimation`.
- `PresentationAction`/`PresentationPhaseKind` keep the surviving vocabulary;
  `ZERO_HOLD_PHASE_KINDS`, `lastBoardCyclePhaseIndex` and the `boardPresented`
  guard lose only the removed kinds.
- `GameRuntime.readCell` is a shape check on the delivered member set and writes
  no placeholder; a tolerated legacy per-cell member is dropped, which is what
  `GAME_STATE.md` §2.1.7 item 4 says a reader does.
- No public production API was added for testing: the new assertions use the
  existing scene harness, the existing model types, and source-level boundary
  checks.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — cell deserialization (REST + SignalR + runtime), event
                         parsing/formatting, callback/vocabulary adapters
[x] Integration tests  — full presentation timelines through the scene harness:
                         hydration, swap, match, gravity, refill, cascade, settle
[x] Gameplay scenarios — Match-4 / Match-5 / L-T resolve as confirmed matches and
                         present no created effect (state-transition level)
```

### Key Edge Cases

- A `MatchCreated` / `GemMatched` / board payload that still carries a retired
  member parses to the ordinary shape without error and without inventing a value
  (ADR-028 §4, `GAME_STATE.md` §2.1.7 item 4).
- Overlapping L/T cells clear once and present once.
- A duplicate batch replays no clear; a superseding snapshot still converges the
  board and releases the lock.
- A cycle whose starting board the client does not hold still withholds its board
  mutation while presenting its report.

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References:
  STOP per `AGENTS.md` §7.
- If a change requires touching `src/backend/` or the wire protocol: STOP — that
  is TASK-292's completed scope, and a wire change needs its own task.
- If a change requires editing `docs/`, an ADR, `docs/03-decisions/README.md`, or
  either of the pre-existing working-tree items: STOP.
- If a reviewer finds an authoritative rule conflict: STOP (`AGENTS.md` §4).
- If an active Special Gem presentation dependency cannot be removed without
  losing ordinary match presentation: STOP and record the residual dependency
  plus a separate follow-up task.

---

## Completion Evidence

**Completed.** `Status: DONE`. Phase A implementation slice committed; Phase B
completion record filed.

### Commit

```text
Phase A implementation slice   5f0e7f1a9f18931f5891e70587596f6cea04a485
Subject                        fix(frontend): retire special gems from the client model and presentation
Owns                           TASK-293
Files                          13 declared paths (7 under src/frontend/client/src, 6 under
                                src/frontend/client/tests; no deletions — see ## Declared File Set)
Diff                           13 files changed, 761 insertions(+), 3007 deletions(-)
Shared with                    none
Unowned / pre-existing         none
Phase B completion record      tasks/completed/TASK-293-remove-retired-special-gem-model-and-presentation-from-frontend.md
```

The pre-existing working-tree items (`docs/03-decisions/README.md` modification
and the untracked `docs/03-decisions/ADR/ADR-028-…md`) were **not** staged or
modified by this task; `git status` shows them in the same state they were found.

### Changed Files

**Models — the retired members removed**

- `services/realtime/SignalRService.ts` — `CellPayload` is `{ gemType }` only;
  `SpecialGemPayload` deleted; the `BoardPayload` contract comment states the
  pure-`GemType` cell delivered by `SIGNALR_PROTOCOL.md` §4.1 item 5.
- `services/api/BattleModels.ts` — `BattleStartCell` is `{ gemType }`;
  `BattleStartSpecialGem` deleted; the `BattleStartInitialState` shape diagram
  reads `cells: [ { gemType } ]`.
- `services/api/ApiService.ts` — `BattleStartSpecialGem` dropped from the battle
  wire model re-export list.
- `game/runtime/GameRuntimeEvents.ts` — `RuntimeCell` is `{ gemType }`;
  `RuntimeSpecialGem` deleted.
- `game/runtime/GameRuntime.ts` — `readCell` reads the one delivered member and
  `readSpecialGem` is deleted.

**Presentation — the retired paths removed**

- `game/scenes/BattleEventPresenter.ts` — `PresentedMatchCreated` loses
  `createdSpecialGems`; `PresentedGemMatched` loses `specialGem`; both parsers
  and both formatters follow. `PresentationAction` drops `special-activation` /
  `turn-summary`; `PresentationPhaseKind` drops `special-activation` / `summary`.
  The whole Special Gem presentation table and the activation-occurrence/tally
  helpers (`SPECIAL_GEM_PRESENTATIONS`, `resolveSpecialGemPresentation`,
  `formatGemCellLabel`, `parseGemBaseLabel`, `parseSpecialGemBadge`,
  `SpecialActivationOccurrence`, `specialActivationOccurrences`,
  `summarizeSpecialActivations`, `formatSpecialActivationSummary`) are deleted.
- `game/scenes/BattleScene.ts` — `drawGemContent` writes the delivered Gem colour
  and type label only (no per-cell stroke/size/badge); the cell content key is the
  delivered `gemType`; `replayBoardDeltas` / `presentMatchDestruction` /
  `clearConfirmedCell` lose the creation-claim half; `presentCreatedSpecialGem`,
  `presentSpecialActivations`, `presentTurnSummary`, `passRangeEnd`, the
  activation overlay constants and the turn-summary strip constants are deleted;
  the timing policy loses its `activation`/`summary` entries; the timeline,
  `phaseEffectMs`, `lastBoardCyclePhaseIndex` and `boardPresented` lose only the
  removed kinds.

**Tests**

- `tests/SceneLifecycle.test.ts` — the Match-tier block is rewritten: the
  retired creation-cell suites (30, 32, 34–38) and the activation/tally suites
  (BOSS-TURN 4–11, 13, 14) are deleted, and the surviving coverage is adapted.
  Added: Match-4 / Match-5 / L/T resolve as one confirmed match and present no
  created effect; the duplicate-batch idempotency test no longer speaks of a
  creation; an ordinary board cycle's timeline contains no activation or tally
  phase; a two-pass cascade round keeps one view per cell; the three-pass cascade
  coherence, the Boss/Player shared pipeline and the recovery/supersession tests
  are preserved (adapted only where their fixtures carried the retired member).
- `tests/BattleEventPresentation.test.ts` — the retired presentation-table tests
  are replaced by two tolerant-reader tests (a `MatchCreated` and a `GemMatched`
  carrying a retired member parse to the ordinary shape), and the fixture table
  drops the retired member.
- `tests/GameRuntime.test.ts` — the retired "absent Special Gem" test is replaced
  by one asserting a cell's synchronized copy carries exactly its `gemType` and
  no modelled retired member.
- `tests/BattleService.test.ts` — the REST board test asserts 64 ordinary cells
  with exactly one member each, plus a tolerant-reader case for a retired
  per-cell member.
- `tests/SignalRService.test.ts` — the fixtures use the ordinary contract, the
  "cell entries carrying a Special Gem" test becomes the four-Gem-type contract
  test, and the negative-contract method test builds the retired identifier from
  fragments.
- `tests/RuntimeBoundaries.test.ts` — two new source-level boundary tests: no
  active `src/` file names a retired member, and each cell model declares exactly
  one member (`gemType`).

### Validation Results

```text
npx tsc --noEmit                         PASS  0 errors
npm run test:run (full frontend suite)   PASS  1243 passed / 0 failed (24 files)
npm run build (tsc && vite build)        PASS  tsc clean; 83 modules transformed; bundle emitted
git diff --check                         PASS  clean
Retired-symbol sweep over src/           PASS  0 matches for
                                               SpecialGem|specialGem|createdSpecialGems|
                                               LineClear|SPECIAL_GEM|special-activation|
                                               turn-summary
src/backend/ + tests/backend/            PASS  0 matches (TASK-292's state, untouched)
docs/ + tasks/completed/                 PASS  unchanged by this slice
```

Baseline comparison: the pristine working tree at `HEAD` (`652a23c`) ran the same
suite as `1257 passed / 0 failed` (24 files) before this slice; the final run
above is `1243 passed / 0 failed`. The 14-case reduction is confined to
`SceneLifecycle.test.ts` (−14: the nine retired creation-cell cases became five
match-tier cases, and the eleven activation/tally cases became one) and
`BattleEventPresentation.test.ts` (−2: three retired presentation-table cases and
one retired fixture-table row became two tolerant-reader cases), partly offset by
`RuntimeBoundaries.test.ts` (+2 boundary cases). Every removal was of a case whose
only subject no longer exists; no surviving assertion was weakened.

### Compatibility With TASK-292's Backend Contract

| Contract | Backend (TASK-292) | Frontend (this slice) |
| --- | --- | --- |
| `BoardPayload.Cells[]` | `CellPayload(string GemType)` → `{ gemType }` | `CellPayload { gemType }`; `RuntimeCell { gemType }` |
| REST battle-start cell | `BattleStartCell(GemType)` → `{ gemType }` | `BattleStartCell { gemType }` |
| `MatchCreated` | `shape`, `cells`, `gemType`, `cascadeDepth` | same four members; no `createdSpecialGems` |
| `GemMatched` | `cellIndex`, `gemType` | same two members; no `specialGem` |
| Retired wire enums/DTOs | none emitted | none modelled |
| Event names / order / state transitions | unchanged | unchanged |

No discrepancy was found, so no separate follow-up task is required and the wire
protocol was not modified.

### Residual-Reference Audit (frontend-wide)

| Class | Occurrence | Disposition |
| --- | --- | --- |
| Active code that had to be removed | All retired model members, presentation tables, activation/tally phases and their constants | Removed (this commit) |
| Negative-contract tests | `RuntimeBoundaries.test.ts` (no active file names a retired member; each cell model has one member), `SignalRService.test.ts` (no retired gameplay method), `BattleEventPresentation.test.ts` / `GameRuntime.test.ts` / `BattleService.test.ts` (a retired member is tolerated and ignored) | Retained — they guard reintroduction, and the retired identifiers are assembled from fragments so none is live in source |
| Historical / auditability | `tasks/completed/TASK-*`, `docs/` retirement statements, `battle-*-shots/*.png` fixture screenshots | Retained |
| Unrelated term sharing the search string | "activation" as in a UI control's activation (`App.tsx`, `LobbyScene.ts`, `ResultScene.ts`, `CollectionViewerScene.test.ts`, `LobbyScene.test.ts`); the Boss skill display name "Flame Burst" | Left untouched |

### Quality Review (`.ai/workflow/quality/review.md` §1)

```text
Correctness   PASS  The client models exactly the members SIGNALR_PROTOCOL.md §3.2.6/§3.2.9
                    and GAME_STATE.md §2.1.3 define, and renders a cell as its Gem type.
                    No gameplay value is computed: the board content key is the delivered
                    `gemType`, clears are confirmed against delivered members, and the
                    surviving phase holds are still derived from the delivered events.
Architecture  PASS  All changes stay in the client's models, runtime port and Phaser
                    presentation layer. No new module, interface, factory, registry or
                    state store; no client/backend responsibility moved (AGENTS.md §9, §13).
Scope         PASS  13 declared paths, all under src/frontend/client/. No backend, docs,
                    ADR, decision-index or task-history change; no gameplay or balance value.
Tests         PASS  Removed only suites whose sole subject was the retired behavior; the
                    synchronization, pacing, ordering, recovery and duplicate-batch
                    invariants keep coverage; the ten §5 areas are each covered.
Documentation PASS  docs/01-game-design/ and docs/02-technical/ already state the pure-GemType
                    contract (TASK-287/TASK-289) and ADR-028 §5 already retires the client
                    presentation, so code now matches them and no doc edit was required.
Security      PASS  No authentication, session or authorization surface touched.
Performance   PASS  Net removal on the render path: a redraw no longer resolves a per-cell
                    presentation table, and the timeline no longer allocates activation or
                    tally overlays. No new query, listener or allocation.
Maintainability PASS  Net -2246 lines (761 insertions, 3007 deletions across the 13 paths); a retired presentation deleted rather than bypassed, so it
                    cannot be reintroduced behind a flag.
Determinism   PASS  No RNG read, no board derivation, no ordering change: the timeline is
                    still built in delivered order and the server remains authoritative.
```

### Remaining Issues / Follow-ups

```text
F-1  Balance re-baseline: TASK-292 F-2's Balance-corpus delta (4 newly failing
     TASK-195/H06 expectations plus 2 newly passing) is unchanged by this
     frontend-only slice. Re-deriving tasks/artifacts/TASK-194-*.json and
     TASK-197-*.json under the match-only resolution is a Product-Owner-approved
     balance decision, not part of this task.
F-2  Infrastructure database-provisioning failures (4 tests): unchanged
     pre-existing backend baseline, not caused by this slice and out of scope.
F-3  Deployment: ADR-028 §3/§6 requires draining active battles before the joint
     server/client deployment. This slice deployed nothing and pushed nothing; it
     does not by itself establish joint deployment readiness.
```

---

## Final Report

See the response accompanying this record: task ID/type/risk/status, removed
types/fields/paths/assets, changed files, tests added/updated/removed, the
residual-reference audit, the actual validation results, the compatibility
confirmation, the branch/HEAD/diff/commit SHAs, remaining follow-ups, and the
confirmation that backend code, ADRs, the decision index and the balance evidence
were not modified.
