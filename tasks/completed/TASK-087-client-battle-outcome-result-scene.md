# TASK-087 — Present the Battle Outcome in a ResultScene

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and src/ by path and section; copies only what an
  implementer needs to know where to look.

  This is the "ResultScene" task explicitly deferred by TDD.md §2.1's MVP
  staging note (recorded per TASK-078, see tasks/completed/TASK-078...md:447-448)
  and the last missing link of the Phase 1 slice (ROADMAP.md §1: "one playable
  battle, start to finish"): the runtime already forwards BattleWon/BattleLost
  (SIGNALR_PROTOCOL.md §3.2.19) but no production code consumes them, so a
  finished battle never reaches a player-visible outcome.
-->

---

## Metadata

```text
Task ID:           TASK-087
Type:              FEATURE (ResultScene and the outcome handoff are specified
                   by TDD.md §2.1 + SIGNALR_PROTOCOL.md §3.2.19 note 2; only
                   the client implementation is missing - tasks/TASK_TYPES.md §2)
Status:            DONE
Risk:              MEDIUM (FEATURE baseline for Frontend/Phaser; presentation
                   only, no combat/battle-state/auth change -
                   tasks/TASK_TYPES.md §4, validation depth per
                   .ai/workflow/core/validation.md §2)
Priority:          HIGH (Phase 1 goal "one playable battle, start to finish" -
                   docs/00-overview/ROADMAP.md §1; battle end currently has no
                   player-visible presentation)
Primary Agent:     client
Supporting Agents: testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, phaser/scenes,
                   client/phaser-architecture, client/client-event-projection,
                   client/client-state-authority
Dependencies:      TASK-006, TASK-007, TASK-007A3, TASK-069, TASK-077,
                   TASK-078, TASK-086
```

---

## Objective

Implement the documented end of the client battle loop: when the runtime
forwards a `BattleWon` / `BattleLost` event (`SIGNALR_PROTOCOL.md` §3.2.19,
whose note 2 states the client uses these members for end-of-battle display),
`BattleScene` hands off to a new `ResultScene` that presents the delivered
`outcome` and terminal HP members verbatim — completing the documented scene
lifecycle `... → LobbyScene → BattleScene → ResultScene` (`TDD.md` §2.1).
Presentation only: the outcome, HP values, and any reward remain
server-authoritative; no new protocol surface, no backend change, no
reward/state derivation on the client (`AGENTS.md` §10, `GAME_STATE.md` §4).

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — server-authoritative battle / client
  presentation is IN (same classification as TASK-069); §2 for exclusions;
  §4 for classification authority
- `docs/00-overview/ROADMAP.md` §1 — Phase 1 goal: one playable battle, start
  to finish (this task is the "finish")
- `docs/02-technical/TDD.md` §2.1 — scene lifecycle diagram
  (`BattleScene → ResultScene`), the MVP staging note deferring ResultScene
  "to its own task", `ResultScene` definition ("Battle outcome presentation
  (Victory/Defeat, summary)"), and Server-Authoritative Boundary (scenes are
  presentation components, never authoritative)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.19 — `BattleWon` / `BattleLost`
  wire members and note 2 (client uses `finalBossHp` / `finalPlayerHp` for
  end-of-battle display); §3.1 / §6.4 — `BattleStateUpdated` is delivered
  before the outcome events; §8.3 — no battle lifecycle/status message exists
- `docs/02-technical/GAME_EVENTS.md` §2 (BattleWon / BattleLost) — the
  `outcome` value set and battle-end trigger semantics; §3.3 — client-side
  handling of each event is a client implementation detail
- `docs/02-technical/GAME_STATE.md` §1 / §4 — Client Presentation State is
  client-owned, non-authoritative, and not reconstructable from the server
- `docs/02-technical/ARCHITECTURE.md` §1 (ResultScene.ts in the client scene
  tree), §2.2.3 (Phaser scene boundary); `docs/03-decisions/ADR/ADR-003*` —
  Phaser scene architecture including ResultScene
- `docs/02-technical/API_CONTRACTS.md` §4 (+ note 1) — the result endpoint and
  the deferred `RewardSummary` member list (boundary for Out of Scope)
- `AGENTS.md` §10 (server-authoritative), §12 (domain boundaries), §16 (task
  discipline), §20 (stop conditions)

---

## Scope

### In Scope

- Create `src/frontend/client/src/game/scenes/ResultScene.ts` and register it
  in the scene list (`src/frontend/client/src/game/GameConfig.ts:66`).
- Outcome handoff in `BattleScene.ts`: subscribe to the existing
  `onBattleEvents` port alongside the current subscriptions
  (`BattleScene.ts:119`, `:122`) and unsubscribe in `shutdown()`
  (`BattleScene.ts:132-136`), following the existing unsubscribe pattern.
- On the first `BattleWon` / `BattleLost` envelope delivered for the current
  battle, transition `BattleScene → ResultScene` carrying only the delivered
  envelope members (`outcome`, `finalBossHp`, `finalPlayerHp` —
  `SIGNALR_PROTOCOL.md` §3.2.19) for display.
- `ResultScene` presentation: the outcome label (Victory / Defeat) derived
  solely from the delivered `outcome` string (`"victory"` | `"defeat"`), and
  the terminal HP values displayed verbatim from `finalBossHp` /
  `finalPlayerHp`; scene-local presentation state initialized on start and
  released on shutdown.
- Test coverage listed under Testing Requirements, including the boundary
  assertions that no outcome/HP value is computed on the client.

### Out of Scope

- **Reward / reward-summary presentation of any kind** — the `RewardSummary`
  member list is deferred (`API_CONTRACTS.md` §4 note 1, `DATABASE.md` §1);
  rendering rewards must STOP (see Stop Conditions), not be guessed.
- **Calling `GET /api/battle/{battleId}/result`** — no documented client
  trigger at battle end; `ApiService.getBattleResult` (`ApiService.ts:232`,
  TASK-076) stays unused by this task (candidate follow-up, report only).
- **Post-result navigation** — the documented lifecycle ends at `ResultScene`
  (`TDD.md` §2.1); no successor scene or button behavior is documented.
- **`MainMenuScene`** — separately deferred by the same TDD §2.1 staging note.
- In-battle event presentation beyond the outcome handoff (match / cascade /
  combo / damage feedback) — a separate client-presentation task; this task
  interprets only `BattleWon` / `BattleLost`.
- `serverSequence` ordering / gap / desync handling (`SIGNALR_PROTOCOL.md`
  §6.3 → §7 reconnect/resync) — Phase 3 (`ROADMAP.md` §1), separate task.
- Backend, SignalR protocol, REST contract, ADR, or `docs/` changes of any
  kind; any new hub method or event; session/auth changes; reconnect behavior.
- Any client-authoritative gameplay logic or recomputation of battle results
  (`AGENTS.md` §10, `TDD.md` §2.1 Server-Authoritative Boundary).
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State

Every link of the pipeline is implemented except the final handoff.
`GameRuntime.forwardBattleEvents` (`GameRuntime.ts:607-620`) validates the
transport envelope and forwards it unchanged to subscribers through
`onBattleEvents` (`GameRuntimeEvents.ts:254`; envelope type `:59`, listener
type `:75`), but it has **no production consumer** — only
`tests/GameRuntime.test.ts`, the mock at `tests/LobbyScene.test.ts:114`, and
the port-surface assertion `tests/RuntimeBoundaries.test.ts:378`.
`BattleScene` subscribes only to runtime events and battle state
(`BattleScene.ts:119`, `:122`) and has no outcome handling;
`GameConfig.ts:66` registers `[BootScene, PreloaderScene, LobbyScene,
BattleScene]`, and `ResultScene.ts` does not exist (recorded by
`tasks/completed/TASK-078...md:447-448`). Battle end reaches the client only
as `BattleWon` / `BattleLost` — there is no `Status` / lifecycle field
(`SIGNALR_PROTOCOL.md` §8.3) — delivered after the final `BattleStateUpdated`
(§3.1, §6.4), so the authoritative terminal state is already stored in the
runtime before any handoff.

---

## Acceptance Criteria

- [x] `ResultScene` exists, is registered in `GameConfig.ts:66`'s scene list,
      and `BattleScene` starts it after receiving an outcome event; the
      transition is asserted by a test (`TDD.md` §2.1 lifecycle).
- [x] The outcome subscription uses the existing `onBattleEvents` port and is
      released in `BattleScene.shutdown()`; no port member is added or changed,
      and `tests/RuntimeBoundaries.test.ts`'s port-surface assertion still
      passes.
- [x] The presented `outcome` equals the delivered member (`"victory"` with
      `BattleWon`, `"defeat"` with `BattleLost`), and the displayed terminal HP
      values equal the delivered `finalBossHp` / `finalPlayerHp` — asserted
      with a test payload whose delivered values differ from any value the
      client could derive locally (e.g. non-zero `finalBossHp` on a win).
- [x] No outcome or HP value is computed on the client: no zero-HP comparison,
      no winner determination, no derived summary — presentation reads only
      delivered members (`GAME_STATE.md` §4, `AGENTS.md` §10).
- [x] An envelope containing no `BattleWon` / `BattleLost` does not trigger the
      transition, and only the first outcome envelope for a battle produces a
      presentation (no repeat/restart on a duplicate).
- [x] No reward/progression rendering and no call to
      `GET /api/battle/{battleId}/result` from this task's code.
- [x] Zero backend, docs, ADR, API-contract, and SignalR-protocol changes;
      zero edits to `tasks/completed/*` and `tasks/blocked/*` (`AGENTS.md` §16).
- [x] All relevant tests pass at MEDIUM validation depth
      (`.ai/workflow/core/validation.md` §2: build + unit + integration +
      architecture conformance + `quality/review.md`).
- [x] Quality review checklist passes (`quality/review.md` §1); no
      authoritative rule or contract violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[ ] src/backend/                                   - NOT touched (delivery already exists)
[x] src/frontend/client/src/game/scenes/ResultScene.ts        - NEW (outcome presentation)
[x] src/frontend/client/src/game/scenes/BattleScene.ts        - outcome subscription + handoff
[x] src/frontend/client/src/game/GameConfig.ts                - scene registration (:66)
[x] src/frontend/client/src/game/runtime/                     - reused as-is; no port/contract change
[x] src/frontend/client/tests/ (new/extended)                 - scene, handoff, boundary tests
[ ] tests/                                                    - covered by the client suite above
[ ] docs/ + tasks/ (other than this file)                     - NOT touched
```

---

## Implementation Notes

- Subscription precedent lives in `BattleScene.ts` (`:86-87` handles, `:119` /
  `:122` subscribe, `:132-136` shutdown); follow the same pattern for
  `onBattleEvents`.
- The runtime passes the envelope through unchanged and never interprets it
  (`GameRuntime.ts:599-606`); keep interpretation in the scene layer — the
  runtime must not grow outcome logic.
- Handoff data must contain only delivered members; the scene layer owns how
  they are laid out (`GAME_STATE.md` §4) — no shared "derived result" object.
- Registration/style precedent: `GameConfig.ts:66`; scene test precedent:
  `tests/LobbyScene.test.ts` (mocked runtime port), envelope-forwarding
  precedent: `tests/GameRuntime.test.ts`, boundary precedent:
  `tests/RuntimeBoundaries.test.ts`.
- `BattleStateUpdated` always precedes the outcome events for the battle
  (`SIGNALR_PROTOCOL.md` §6.4), so the runtime's synchronized state is already
  terminal when the handoff happens — no extra state read is required for the
  presented values.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         - BattleScene with a mocked runtime port: BattleWon ->
                         transition + displayed "victory"/HP values verbatim;
                         BattleLost -> "defeat"; envelope without an outcome
                         event -> no transition; duplicate outcome -> single
                         presentation; shutdown before outcome -> no leak and
                         no transition
[x] Integration tests  - mocked transport: established battle -> receive
                         outcome envelope -> ResultScene started once with the
                         delivered members; port-surface test unchanged
[x] Gameplay scenarios - N/A, justified: presentation of an already
                         server-authoritative outcome; no gameplay rule,
                         GameplayState, or RNG touched (GameRuntimeState.ts
                         scope note); MEDIUM depth is met by build + unit +
                         integration + architecture conformance + review
```

### Key Edge Cases
- Outcome event arrives in the same envelope batch as other events
  (`GAME_EVENTS.md` §1) — handoff must not depend on the event's position in
  the batch.
- Scene stopped early (battle abandoned / app teardown) before any outcome —
  no transition, no invented timeout or locally declared result.
- Disconnect with no outcome event — no presentation is produced; resync /
  result-fetch behavior is out of scope (`SIGNALR_PROTOCOL.md` §7, Phase 3).

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References:
  STOP per `AGENTS.md` §7
- If task requires client-authoritative game logic calculation: STOP per
  `AGENTS.md` §10
- If task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: STOP & decompose
- If any criterion requires rendering reward/progression values from
  `RewardSummary`: STOP per `AGENTS.md` §7 — the member list is deferred
  (`API_CONTRACTS.md` §4 note 1, `DATABASE.md` §1); report instead of guessing
- If any criterion requires calling `GET /api/battle/{battleId}/result` to
  complete the presentation: STOP per `AGENTS.md` §7/§20 — no documented
  client trigger exists at battle end
- If the presentation requires a battle lifecycle/`Status` field, a new hub
  method/event, or any protocol change: STOP per `AGENTS.md` §7/§18 — none
  exists (`SIGNALR_PROTOCOL.md` §8.3)
- If the task requires navigation after `ResultScene` (return-to-lobby etc.):
  STOP per `AGENTS.md` §7 — the documented lifecycle ends at `ResultScene`
  (`TDD.md` §2.1)
- If the task requires `serverSequence` gap detection or resync
  (`SIGNALR_PROTOCOL.md` §6.3/§7): STOP per `AGENTS.md` §8 — Phase 3
  (`ROADMAP.md` §1), separate task
- If satisfying any criterion requires editing `tasks/completed/*`,
  `tasks/blocked/*`, or the backend/docs/ADRs: STOP per `AGENTS.md`
  §16/§17/§18 — report instead

---

## Completion Evidence

### Changed Files
- `src/frontend/client/src/game/scenes/ResultScene.ts` (new)
- `src/frontend/client/src/game/scenes/BattleScene.ts` (outcome subscription and handoff)
- `src/frontend/client/src/game/GameConfig.ts` (registered `ResultScene` in scene list)
- `src/frontend/client/tests/ResultScene.test.ts` (new scene and handoff unit/integration suite)
- `src/frontend/client/tests/SceneLifecycle.test.ts` (added ResultScene and outcome handoff tests)
- `src/frontend/client/tests/RuntimeBoundaries.test.ts` (included ResultScene in boundary tests)
- `src/frontend/client/tests/PhaserGame.test.tsx` (updated expected registered scene list)

### Validation Results
- Targeted: `npx vitest run tests/ResultScene.test.ts tests/SceneLifecycle.test.ts` (70 passed)
- Frontend Suite: `npx vitest run` (17 test files, 407 passed)
- Type Check: `npx tsc --noEmit` (0 errors)
- Production Build: `npx vite build` (clean build)
- Architecture & Boundaries: `tests/RuntimeBoundaries.test.ts` (37 tests passed)

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
