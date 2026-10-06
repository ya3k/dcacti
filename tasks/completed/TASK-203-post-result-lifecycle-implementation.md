# TASK-203 — Implement the Approved Post-Result Lifecycle (ResultScene Buttons, Result → Lobby / MainMenu, Preserved Editable Loadout, Battle-State Cleanup)

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section. Does NOT copy game rules, payload
  shapes, or state schemas.

  DECISION AUTHORITY: TASK-202 (DONE) — the Product Owner approved the
  post-result lifecycle and TASK-202 recorded it. This task IMPLEMENTS exactly
  that, and adds nothing:
      D-202-01 = C  ResultScene provides PLAY AGAIN → LobbyScene and
                    MAIN MENU → MainMenuScene
      D-202-02 = A  Explicit UI buttons only as the primary navigation
                    mechanism (no full-screen tap, any-key, or automatic
                    transition)
      D-202-03 = D  Preserve the previous loadout on PLAY AGAIN; it must still
                    be editable in LobbyScene before the next battle
      D-202-04 = A  Clear active battle state when leaving the completed
                    battle; no stale battle/session state may affect the next
                    battle
  The decisions are transcribed into authoritative documentation by TASK-202:
  docs/00-overview/GDD.md §2/§2.1, docs/02-technical/TDD.md §2.1, and
  docs/02-technical/ARCHITECTURE.md §2.2.3. Read those, not this file, for the
  rule text.

  THIS TASK MAY NOT ADD A DESTINATION, CONTROL, OR CLEANUP BEHAVIOR the
  Product Owner did not approve. If implementation appears to need one, STOP
  (AGENTS.md §7, §20).

  OPEN ARCHITECTURE ITEM (must be closed FIRST, in this task): D-202-03 = D
  requires the previous loadout to survive the Lobby → Battle → Result → Lobby
  round trip, which the current ephemeral scene-local selection cannot do.
  ARCHITECTURE.md §2.2.3 records the requirement and boundary but deliberately
  chooses NO mechanism/owner/lifetime. Recording that decision (and any ADR it
  needs) is this task's FIRST step, before any carrier is implemented
  (AGENTS.md §18). Do NOT pick a mechanism silently inside the code change.

  BOUNDARY: src/frontend/client/ (ResultScene, LobbyScene, runtime port/runtime,
  the carrier module chosen by the recorded architecture decision) and the
  client tests for them, plus the FIRST-step documentation of the carrier
  decision. No backend change. No REST/SignalR/Redis/database change. No game
  rule or balance change. No BattleScene change is expected (ResultScene already
  receives the outcome handoff and battleId from BattleScene; navigation and the
  cleanup do not require touching it).
-->

---

## Metadata

```text
Task ID:           TASK-203
Type:              FEATURE (TASK_TYPES.md §2 — documented behavior that exists
                   in docs/ but is not built; development/feature.md, with a
                   gated architecture-decision first step per AGENTS.md §18)
Status:            DONE
Risk:              MEDIUM (client scene lifecycle, runtime state clearing, and
                   a new client-side state carrier; no server contract changes)
Priority:          HIGH (closes the standalone web loop gap, ROADMAP.md §1
                   Phase 3; the player is currently stuck at ResultScene)
Primary Agent:     client (TASK_TYPES.md §5 — Frontend/Phaser; AGENT_SELECTION.md)
Supporting Agents: testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery,
                   client/phaser-architecture,
                   client/client-state-authority,
                   testing/test-scenario-generation,
                   quality/scope-validation
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-202 (DONE — decision authority for D-202-01 … D-202-04)
Blocks:            None
```

**Lifecycle note.** Executed directly by its agent. Transition path per
`tasks/TASK_LIFECYCLE.md` §2: `BACKLOG → READY` (TASK-202 had already created
the authoritative documentation, confirmed MVP scope, and made the acceptance
criteria binary, so this was the orchestrator's call and was satisfied when the
task was picked up) `→ IN PROGRESS → IN REVIEW → DONE`, with the single
permitted file move `backlog/ → completed/`. `tasks/README.md` §4: the filename
is unchanged, only the folder moved.

**Architecture-decision gate closed first (AGENTS.md §18).** The gated first
step was executed before any carrier code was written: `D-202-03 = D`'s
loadout state carrier is recorded as **ADR-022**
(`docs/03-decisions/ADR/ADR-022-post-result-preserved-loadout-carrier.md`),
indexed in `docs/03-decisions/README.md` §7, and transcribed into the owning
document `docs/02-technical/ARCHITECTURE.md` §2.2.3 (with §2.2.1 recording the
post-result cleanup capability and §1 listing the accessor module). The carrier
decision, and the no-new-mechanism conclusion behind it, are in that ADR.

**Status note (filing record).** Filed as `BACKLOG` per `tasks/README.md` §6
step 6 (a new task is filed as `BACKLOG`). Every BACKLOG → READY criterion in
`tasks/TASK_LIFECYCLE.md` §3 was
already satisfied — the authoritative documentation exists (`GDD.md` §2/§2.1,
`TDD.md` §2.1, `ARCHITECTURE.md` §2.2.3), MVP scope is confirmed, no dependency
is unresolved, and the acceptance criteria below are binary and testable — so the
`BACKLOG → READY` transition is the orchestrator's to make, not this task's.
This task is now **DONE**; its execution evidence is recorded in
"Completion Evidence" below.

---

## Objective

Implement the Product-Owner-approved post-result lifecycle exactly as recorded in
`TASK-202` and transcribed into `GDD.md` §2/§2.1, `TDD.md` §2.1, and
`ARCHITECTURE.md` §2.2.3: give `ResultScene` two explicit UI buttons
(`PLAY AGAIN` → `LobbyScene`, `MAIN MENU` → `MainMenuScene`), preserve the
previous loadout across `PLAY AGAIN` while keeping it editable in `LobbyScene`,
and clear active battle state when the player leaves the completed battle so no
stale battle state reaches the next battle.

---

## Authoritative References

- `tasks/completed/TASK-202-post-result-lifecycle-product-decision-and-specification.md`
  — **DECISION AUTHORITY.** D-202-01 = C, D-202-02 = A, D-202-03 = D,
  D-202-04 = A, with their rationale and compatibility constraints. Read this
  first.
- `docs/00-overview/GDD.md` §2, §2.1 — approved player-facing post-result flow
- `docs/02-technical/TDD.md` §2.1 — approved Phaser scene lifecycle, the two
  continuations, the loadout-preservation + editability rule, the explicit-button
  interaction decision, and the battle-state cleanup rule
- `docs/02-technical/ARCHITECTURE.md` §2.2.1 (runtime coordination rules 1–6),
  §2.2.3 (pre-battle selection boundary **and the post-result loadout carrier
  requirement/boundary**), §5 (anti-overengineering)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4 (`BattleStateUpdated` — the client's
  synchronized battle copy), §7.3 (`BATTLE_NOT_FOUND` fallback, the existing
  drop-a-battle path), §8.3 (no battle lifecycle/status message exists)
- `docs/02-technical/API_CONTRACTS.md` §3 (`POST /api/battle/start` — the only
  way to create a battle; validates the submitted loadout server-side), §4
  (`GET /api/battle/{battleId}/result`, notes 1–7), §5.5–§5.6 (collection reads
  carry no equip/loadout state)
- `docs/02-technical/GAME_STATE.md` §2.0.5, §2.3, §4 (client presentation state
  is client-owned, non-authoritative)
- `docs/02-technical/REDIS_STATE.md` §3 (active-state lifecycle; already deleted
  server-side at battle end — not this task's to change)
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md`,
  `ADR-003-phaser-game-runtime.md`, `ADR-005-redis-active-battle-state.md`,
  `ADR-008-battle-recovery.md`, `ADR-015-application-session-authentication-contract.md`,
  `ADR-020-standalone-web-account-authentication.md`
- `docs/00-overview/MVP_SCOPE.md` §1 (IN), §2 (OUT), §3 (FUTURE), §4
- `.ai/skills/client/phaser-architecture/SKILL.md`,
  `.ai/skills/client/client-state-authority/SKILL.md`

---

## Scope

### In Scope

1. **`ResultScene` explicit buttons** (`D-202-01 = C`, `D-202-02 = A`):
   - `PLAY AGAIN` → `LobbyScene`
   - `MAIN MENU` → `MainMenuScene`
   - Buttons are the primary and only navigation mechanism.
   - Use the existing navigation mechanism (`this.scene.start(sceneKey)` with a
     `hasTransitioned`-style guard, as in `MainMenuScene.transitionTo`) — **no**
     new navigation abstraction, router, or scene manager (`ARCHITECTURE.md` §5,
     `AGENTS.md` §9).
   - Existing presentation is preserved unchanged: outcome, terminal HP values,
     and the persisted reward summary render exactly as delivered, and a failed
     result read still leaves the reward area empty
     (`SIGNALR_PROTOCOL.md` §3.2.19; `API_CONTRACTS.md` §4 notes 6–7;
     `DATABASE.md` §1 items 4–5).
2. **`Result` → `Lobby` / `MainMenu` navigation**: the transitions above, each
   fireable at most once per `ResultScene` instance.
3. **Preserved editable loadout** (`D-202-03 = D`):
   - **Step 3a (first, gated).** Record the architecture decision for the loadout
     state carrier required by `ARCHITECTURE.md` §2.2.3 — its shape, owner,
     lifetime, and clearing rules — in the owning document, and in an ADR if the
     change warrants one (`AGENTS.md` §18,
     `.ai/workflow/architecture/architecture-change.md`,
     `.ai/workflow/architecture/adr-change.md`). **Do not implement the carrier
     before this is recorded.** The requirement/boundary is already documented;
     the mechanism is not.
   - **Step 3b.** Implement the carrier so the loadout used in the battle that
     just ended is still selected when `LobbyScene` is entered through
     `PLAY AGAIN`.
   - **Step 3c.** `LobbyScene` remains the editing surface: every part of the
     preserved selection (Pet, Cards, Relics, Boss) can still be changed before
     `Start Battle`, and the submitted `POST /api/battle/start` request carries
     the edited values. Preservation is a convenience, never a lock and never a
     client-side legality decision (`ARCHITECTURE.md` §2.2.3 rule 5;
     `API_CONTRACTS.md` §3).
   - The carrier must not be gameplay state, must not be added to
     `state/GameRuntimeState.ts`, must not mirror the owned collection, and must
     not become a heavy state store (`ARCHITECTURE.md` §2.2.1 rule 5, §2.2.3,
     §5.5).
4. **Active battle-state cleanup** (`D-202-04 = A`):
   - When the player leaves the completed battle, the runtime's synchronized
     battle copy is cleared, so no stale battle state can be present when the
     next battle starts.
   - The `sync` status returns to the documented connected-but-no-battle value
     (`GameRuntimeState.ts:56-57`).
   - The SignalR connection is **not** disconnected by this action.
   - The preserved pre-battle loadout is **not** battle state and is **not**
     cleared by this cleanup.
   - Whether the existing `handleBattleNotRecoverable` shape
     (`battleState = null` + `sync: 'awaiting_battle'`, `GameRuntime.ts:679-697`)
     may be reused for a normal post-result exit, or whether a distinct
     documented capability is required, is resolved and documented here — that
     path is currently tied to `SIGNALR_PROTOCOL.md` §7.3's `BATTLE_NOT_FOUND`.
     No new wire message is introduced: §8.3 records that no battle
     lifecycle/status message exists.
5. **Boundary preservation:** any destination and any transport is reached only
   through the `GameRuntime` port. No scene imports `fetch`, `ApiService`,
   `@microsoft/signalr`, or `HubConnection` (`ARCHITECTURE.md` §2.2.1 rule 1,
   §2.2.3 rule 3). No gameplay value is computed or derived on the client
   (`GAME_RULES.md` §18, ADR-001).
6. **Tests** for the approved flow, including the negative cases below.

### Out of Scope

- **Any server-side change.** No REST endpoint, wire member, SignalR
  method/event, Redis key, or database column. No approved decision requires one.
- **Disconnecting the transport on post-result exit.** That is `D-202-04 = C/D`,
  which was **not** approved.
- **Session persistence or a session reset mechanism.** The session stays in
  memory (ADR-015 D5, ADR-020); no approved decision requests otherwise.
- **Any destination or control other than the two approved buttons.**
  No `RETRY`, `REMATCH`, `CONTINUE`, `NEXT`, collection shortcut, keyboard
  confirmation, screen tap, or automatic timed transition.
- **Client-side loadout validation or legality checking.**
- **Changing game rules, balance, rewards, progression, or content.**
- **Stage progression, campaign map, energy/stamina, or any item in
  `MVP_SCOPE.md` §2 (OUT) or §3 (FUTURE).**
- **Modifying `BattleScene`** — not expected to be necessary; if it appears to be,
  STOP and report before touching it (`AGENTS.md` §16).
- **Modifying the backend, `docs/01-game-design/`, `MVP_SCOPE.md`, or `ROADMAP.md`.**
- **Unrelated refactors**, including tidying unrelated scenes, services, or tests.
- **Marking this task DONE.** It is created as `BACKLOG`; completion is its
  executor's and the review workflow's call.

---

## Current State

- `ResultScene.ts` presents outcome, terminal HP, and rewards, and has **no**
  interactive control, no pointer/keyboard listener, no `scene.start()`, and no
  automatic transition (lines 60–61 state that navigation was out of scope).
- `BattleScene.handleBattleEvents` (lines 893–930) is the only path into
  `ResultScene`, handing off `{ ...outcome, battleId }`; `BattleScene.shutdown`
  (lines 165–194) already clears its own scene state.
- `MainMenuScene.transitionTo` (lines 111–118) is the existing, guarded
  navigation precedent to follow.
- `LobbyScene` holds its pre-battle selection in ephemeral scene-local fields
  (`selectedPetId`, `selectedCardIds`, `selectedRelicIds`, `selectedBossId`) and
  resets them in `shutdown()` (lines 282–292) — so nothing survives the round
  trip today, which is exactly what `D-202-03 = D` requires changing.
- `GameRuntime` holds one synchronized battle copy (`battleState`) and one
  process-wide connection; the only current drop-a-battle path is
  `handleBattleNotRecoverable` (lines 679–697), tied to `BATTLE_NOT_FOUND`.
- Both approved destinations (`LobbyScene`, `MainMenuScene`) are already
  registered in `GameConfig.ts`; no new scene is needed.

---

## Acceptance Criteria

- [x] `ResultScene` renders exactly two explicit interactive navigation controls,
      behaving as `PLAY AGAIN` and `MAIN MENU`.
- [x] Activating `PLAY AGAIN` starts `LobbyScene`; activating `MAIN MENU` starts
      `MainMenuScene`.
- [x] Each transition fires at most once per `ResultScene` instance (a second
      activation or a double press does not start a second scene).
- [x] No full-screen tap, any-key, or automatic timed transition is the primary
      (or any) navigation mechanism on `ResultScene`.
- [x] Existing `ResultScene` presentation is unchanged: outcome, terminal HP
      values, and delivered reward members render exactly as before; a failed
      result read still leaves the reward area empty and fabricates no value.
- [x] After `PLAY AGAIN`, `LobbyScene` shows the previous battle's Pet, Boss,
      Card, and Relic selections still selected.
- [x] Every part of the preserved selection can still be changed in `LobbyScene`,
      and the `POST /api/battle/start` request carries the edited values.
- [x] Leaving the completed battle clears the runtime's synchronized battle
      state; a subsequent battle begins with no stale battle state from the
      previous battle.
- [x] The post-result exit does **not** disconnect the SignalR connection.
- [x] The battle-state cleanup does **not** clear the preserved pre-battle
      loadout.
- [x] The loadout carrier's architecture decision is recorded in the owning
      document (and ADR if required) **before** the carrier is implemented.
- [x] The loadout carrier is not gameplay state, is not added to
      `state/GameRuntimeState.ts`, and mirrors no owned-collection data.
- [x] No Phaser scene imports `fetch`, `ApiService`, `@microsoft/signalr`, or
      `HubConnection`.
- [x] No authoritative gameplay value is computed, derived, or cached on the
      client (`GAME_RULES.md` §18, ADR-001).
- [x] Zero backend, REST, SignalR, Redis, or database changes.
- [x] All relevant tests pass at the required validation depth
      (`core/validation.md` §2), including the edge cases below.
- [x] Quality review checklist passes (`quality/review.md` §1).
- [x] No authoritative rule or contract violated (`AGENTS.md` §10, ADR-001).

---

## Affected Files & Areas

```text
[x] src/frontend/client/src/game/scenes/ResultScene.ts
        — the two explicit buttons and the guarded transitions
[x] src/frontend/client/src/game/scenes/LobbyScene.ts
        — restoring the preserved selection and keeping it editable
[x] src/frontend/client/src/game/runtime/GameRuntime.ts
    and src/frontend/client/src/game/runtime/GameRuntimeEvents.ts
        — the post-result battle-state cleanup capability exposed through the port
[x] the loadout carrier module/owner chosen by the recorded architecture
    decision — src/frontend/client/src/game/state/PreservedLoadout.ts
    (NEW; Phaser game-registry carrier, ADR-022)
[x] docs/02-technical/ARCHITECTURE.md §1, §2.2.1, §2.2.3 and
    docs/03-decisions/ADR/ADR-022-post-result-preserved-loadout-carrier.md,
    docs/03-decisions/README.md §7, docs/02-technical/TDD.md §2.1
        — the carrier architecture decision, recorded BEFORE implementation
[x] tests/ (src/frontend/client/tests/)
        — ResultScene.test.ts, LobbyScene.test.ts, SceneLifecycle.test.ts,
          GameRuntime.test.ts, RuntimeBoundaries.test.ts (extended),
          PreservedLoadout.test.ts (NEW — the carrier's own contract)
[ ] src/frontend/client/src/game/scenes/BattleScene.ts — NOT changed (see Scope)
[ ] src/backend/                                       — NOT changed
[ ] docs/01-game-design/, MVP_SCOPE.md, ROADMAP.md     — NOT changed
```

---

## Implementation Notes

- **Existing navigation precedent:** `MainMenuScene.transitionTo(sceneKey)`
  (`MainMenuScene.ts:111-118`) — a `hasTransitioned` boolean guard plus
  `this.scene.start(sceneKey)`. Follow it; do not generalize it.
- **Runtime access from a scene:** `readRuntime(this)` from
  `runtime/RuntimeRegistry.ts` is the documented scene → runtime accessor
  (`ARCHITECTURE.md` §2.2.1 rule 2); it returns `null` in an isolated scene test,
  so scene presentation must never depend on it.
- **Cleanup precedent to evaluate, not assume:** `GameRuntime.handleBattleNotRecoverable`
  (`GameRuntime.ts:679-697`) sets `battleState = null` and
  `updateState({ sync: 'awaiting_battle', lastError: null })`. Reuse is
  acceptable **only** if reusing a path contractually tied to
  `SIGNALR_PROTOCOL.md` §7.3 does not contradict that contract's ownership; the
  resolution is documented, not implied.
- **What is cleared is the runtime's synchronized battle copy**, not the
  authoritative Redis record (already deleted server-side at battle end,
  `REDIS_STATE.md` §3) and not the preserved pre-battle loadout.
- **The loadout carrier is a new state carrier, so `AGENTS.md` §18 applies**: it
  is the one part of this task that must be decided and documented before it is
  coded. Keep it minimal — `ARCHITECTURE.md` §5.5 and §2.2.3 forbid a heavy state
  store, a store framework, or a new manager layer (`AGENTS.md` §9).
- **A stale code comment must be corrected as part of touching `ResultScene`:**
  `ResultScene.ts:60-61` still reads "Navigation after ResultScene is not in scope
  (TDD.md §2.1 lifecycle ends at ResultScene)." That was true when it was written
  and is now contradicted by `TDD.md` §2.1. Update the comment to describe the
  implemented, approved behavior. It was left untouched by TASK-202 because
  TASK-202 must not modify production code (`AGENTS.md` §16).

---

## Testing Requirements

### Required Verification

```text
[ ] Unit tests         — ResultScene button activation and the at-most-once
                         guard; the post-result cleanup capability on the
                         runtime; the carrier's round trip; LobbyScene restoring
                         and re-submitting an edited selection; a failed result
                         read still rendering no reward value
[ ] Integration tests  — ResultScene → LobbyScene → POST /api/battle/start
                         carries the preserved/edited loadout (through the
                         runtime port, with the transport stubbed);
                         ResultScene → MainMenuScene;
                         subsequent battle starts with no stale battle state
[ ] Boundary tests     — no scene imports fetch / ApiService / @microsoft/signalr
                         / HubConnection (extend RuntimeBoundaries.test.ts)
[ ] Gameplay scenarios — see below
```

### Gameplay Scenarios (derived from `GDD.md` §2.1 and `TDD.md` §2.1)

```text
Given a battle has reached a terminal outcome
And ResultScene has been presented
When the player activates PLAY AGAIN
Then LobbyScene is started exactly once
And the loadout from the battle that just ended is still selected
And the completed battle's active battle state has been cleared

Given ResultScene is presented with a preserved loadout
And the player changes the Pet, a Card, a Relic, or the Boss in LobbyScene
When the player starts the next battle
Then the submitted POST /api/battle/start request carries the changed values
And the server validates them exactly as before

Given a battle has reached a terminal outcome
And ResultScene has been presented
When the player activates MAIN MENU
Then MainMenuScene is started exactly once
And no navigation to LobbyScene or BattleScene occurs

Given a battle has reached a terminal outcome
And the player has left the completed battle
When the next battle starts
Then the runtime holds no battle state from the completed battle
And the SignalR connection was never disconnected by the exit
```

### Key Edge Cases

```text
- A second activation (or double press) of either button must not start a second
  scene (the at-most-once guard).
- ResultScene presented without outcome data: presentation degrades as today and
  no value is fabricated; button behavior is unaffected.
- ResultScene presented without a battleId: no result read is issued, and no
  transition is blocked by the missing read.
- The result read fails (404 BATTLE_NOT_FOUND / 401 UNAUTHENTICATED,
  API_CONTRACTS.md §4 notes 6–7): the reward area stays empty and the outcome
  presentation stands; navigation is still available.
- Cleanup when no battle state is present (already dropped): must not throw and
  must not disconnect.
- Cleanup must not clear the preserved pre-battle loadout.
- Returning through MAIN MENU and later entering LobbyScene is NOT covered by the
  approved decisions — do not assert or invent behavior there (see Stop
  Conditions).
```

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 apply. Task-specific:

- If the loadout carrier's architecture decision cannot be recorded before the
  carrier is implemented: **STOP** per `AGENTS.md` §18 — do not pick a mechanism
  inside the code change.
- If any required behavior cannot be fully derived from `TASK-202` plus the
  Authoritative References above: **STOP** per `AGENTS.md` §7.
- If implementing the preserved loadout would change player-visible loadout
  behavior on any entry path **other than `PLAY AGAIN`** (for example
  `MainMenuScene` → `LobbyScene`, or a first-ever Lobby entry): **STOP** and
  report — `D-202-03 = D` approves preservation for the `PLAY AGAIN` return only;
  the other paths are undecided and must not be decided here.
- If a server-side change (endpoint, wire member, SignalR method/event, Redis key,
  schema) appears necessary: **STOP** per `AGENTS.md` §18.
- If a keyboard confirmation, screen-tap, or automatic timed transition is
  requested as the way forward: **STOP** — it contradicts `D-202-02 = A`.
- If disconnecting the transport on the post-result exit is requested: **STOP** —
  that is `D-202-04 = C/D`, not approved.
- If a change to `BattleScene`, `docs/01-game-design/`, `MVP_SCOPE.md`, or
  `ROADMAP.md` appears necessary: **STOP** and report (`AGENTS.md` §16).
- If the approved flow would require an out-of-scope feature: **STOP** per
  `AGENTS.md` §8.
- If the work exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: **STOP** and decompose (`tasks/README.md` §13).

---

## Completion Evidence

<!--
  COMPLETED BY THE EXECUTING AGENT (TASK-203 executed directly).
-->

### Changed Files

- `src/frontend/client/src/game/state/PreservedLoadout.ts` — **NEW**: the
  post-result preserved-loadout carrier (`preserveLoadout` /
  `readPreservedLoadout` / `clearPreservedLoadout` over a second documented
  Phaser game-registry key). It declares no loadout type of its own — it holds
  the four documented `BattleStartRequest` members — and it is not gameplay
  state, not technical runtime state, and not persisted anywhere.
- `src/frontend/client/src/game/scenes/ResultScene.ts` — the two approved
  explicit controls (`PLAY AGAIN`, `MAIN MENU`) following
  `MainMenuScene.drawButton`'s existing convention, a single shared
  `hasTransitioned`-style guard, and the `clearActiveBattleState()` call on both
  exits before starting the next scene. The stale "Navigation after ResultScene
  is not in scope" comment is replaced by the implemented behavior.
- `src/frontend/client/src/game/scenes/LobbyScene.ts` — `LobbySceneData` start
  data (`{ restorePreservedLoadout: true }`), `init()` reading it,
  `applyPreservedLoadout()` restoring the preserved selection verbatim before
  the first render, and `preserveLoadout(this, request)` on a **successful**
  `startBattle` only. `shutdown()` still discards the scene's own selection and
  deliberately leaves the carrier alone.
- `src/frontend/client/src/game/runtime/GameRuntimeEvents.ts` — the documented
  `GameRuntimePort.clearActiveBattleState(): void` capability.
- `src/frontend/client/src/game/runtime/GameRuntime.ts` — the implementation:
  drops the synchronized copy, returns `sync` to `awaiting_battle` when
  connected (`unsynchronized` otherwise), performs no transport operation,
  reads no result, and touches no preserved loadout.
- `docs/03-decisions/ADR/ADR-022-post-result-preserved-loadout-carrier.md` —
  **NEW**: the §18 architecture decision (carrier, owner, lifetime, access
  boundary, cleanup semantics, rejected alternatives, and the explicit
  distinction between the preserved loadout, active battle state, and battle
  result data).
- `docs/03-decisions/README.md` — version note + §7 index row for ADR-022.
- `docs/02-technical/ARCHITECTURE.md` — §1 (accessor module in the client tree),
  §2.2.1 (the client-local `clearActiveBattleState()` capability),
  §2.2.3 (the carrier requirement is now decided and recorded; rule 1 gains the
  one documented lifetime exception; the concept table separates preserved
  loadout / active battle state / battle result data); version note.
- `docs/02-technical/TDD.md` — §2.1 implementation status: the two
  continuations are implemented, the carrier is decided, and the staging note
  now records the implemented graph as matching the design graph; version note.
- `src/frontend/client/tests/PreservedLoadout.test.ts` — **NEW**: the carrier's
  own contract (round trip, copy isolation, explicit clearing, key separation
  from the runtime, no transport/persistence).
- `src/frontend/client/tests/ResultScene.test.ts` — post-result navigation
  (both controls, at-most-once guard, no automatic transition, cleanup through
  the port, navigation without a runtime, no keyboard/timer path).
- `src/frontend/client/tests/LobbyScene.test.ts` — preserved-loadout behavior
  (restore on the `PLAY AGAIN` entry only, submission of the restored loadout,
  editability, no restore on a normal entry, empty carrier, preserve-on-success,
  preserve-nothing-on-rejection, carrier survives `shutdown()`).
- `src/frontend/client/tests/SceneLifecycle.test.ts` — the cross-scene round
  trip (Lobby → battle → Result → `PLAY AGAIN` → Lobby with the loadout restored
  and re-submitted), the `MAIN MENU` exit, and "no stale battle state for the
  next battle".
- `src/frontend/client/tests/GameRuntime.test.ts` — `clearActiveBattleState`
  semantics (copy dropped, documented `sync` value per connection, transport
  untouched, no §7.3 result fallback, idempotent, completed battle
  unaddressable, next battle arrives as its own fresh push, no result data held).
- `src/frontend/client/tests/RuntimeBoundaries.test.ts` — the port member list
  updated for the new capability, plus carrier boundary assertions (no
  transport, no persistence, not technical runtime state, not cleared by the
  cleanup, read by exactly one scene entry).

### Validation Results

```text
npx tsc --noEmit                               PASS — clean (noUnusedLocals included)
npx vitest run tests/ResultScene.test.ts       PASS (38 tests)
npx vitest run tests/LobbyScene.test.ts        PASS (64 tests)
npx vitest run tests/PreservedLoadout.test.ts  PASS (6 tests)
npx vitest run tests/SceneLifecycle.test.ts    PASS (95 tests)
npx vitest run tests/GameRuntime.test.ts       PASS (121 tests)
npx vitest run tests/RuntimeBoundaries.test.ts PASS (50 tests)
npx vitest run  (full frontend suite)          PASS (652 tests, 19 files)
npm run build  (tsc && vite build)             PASS
git diff --check                               PASS — no whitespace errors
git status --short                             reviewed — only TASK-203 files
Backend validation                             NOT REQUIRED — zero backend/API/
                                               SignalR/Redis/database files
                                               changed; no shared contract is
                                               affected
E2E browser smoke (scripts/*.mjs)              NOT RUN — requires a live backend
                                               + browser; no script asserts
                                               ResultScene terminality, and the
                                               outcome/reward presentation they
                                               read is unchanged
```

### Decision Conformance

```text
D-202-01 = C — ResultScene renders PLAY AGAIN → LobbyScene and MAIN MENU →
               MainMenuScene as explicit controls; both verified by unit tests
               and by the cross-scene round trip.
D-202-02 = A — explicit buttons only: two interactive hit areas, no keyboard
               path, no full-screen tap, no automatic timed transition.
D-202-03 = D — the loadout used in the completed battle is preserved on a
               successful start and restored on the PLAY AGAIN entry only,
               where LobbyScene remains the editing surface and the next
               request carries the edited values.
D-202-04 = A — both exits call clearActiveBattleState(): the synchronized copy
               is dropped, sync returns to the documented value, the SignalR
               connection is never disconnected, no result route is read, and
               the preserved loadout is not cleared.
Carrier architecture decision recorded before implementation — ADR-022, with
               ARCHITECTURE.md §2.2.3 updated in the same change set.
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic (the carrier holds a
      submission input, is never authoritative, and is validated server-side)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1, §2)
- [x] Confirmed zero backend / REST / SignalR / Redis / database change
- [x] Confirmed no game rule or balance change (`docs/01-game-design/` untouched)
- [x] Confirmed no unapproved destination, control, or cleanup behavior added
      (no `RETRY`/`CONTINUE`/keyboard/tap/auto transition; the transport is not
      disconnected; `MainMenuScene → LobbyScene` behavior is unchanged and
      undecided)
- [x] Confirmed no Phaser scene imports `fetch`, `ApiService`,
      `@microsoft/signalr`, or `HubConnection`

### Reported Issue (not fixed here — outside TASK-203, `AGENTS.md` §16)

- **`ApplicationSession` persists the session token in `localStorage`** while
  `TASK-202` and `ADR-015 D5`/`ADR-020` describe the session as memory-only
  (`src/frontend/client/src/services/api/ApplicationSession.ts:69-121`).
  Impact: a documentation/implementation mismatch about session persistence —
  unrelated to the post-result lifecycle and not touched by this task.
  Suggested follow-up: a documentation-or-code reconciliation task for the
  session persistence contract.
