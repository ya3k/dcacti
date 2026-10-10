# TASK-245 — Phaser Cascade Animation & Combat Presentation Sequencing

## Metadata

```text
Task ID:           TASK-245
Type:              FEATURE
Status:            DONE
Risk:              MEDIUM
Priority:          MEDIUM
Primary Agent:     client
Supporting Agents: testing, review
Workflow:          development/feature.md
Skills:            client/phaser-battle-presentation, client/phaser-match3, client/client-event-projection, client/client-state-authority, phaser/tweens
Dependencies:      TASK-088, TASK-210, TASK-207 (G15)
Declared Files:    src/frontend/client/src/game/scenes/BattleScene.ts, src/frontend/client/src/game/scenes/BattleEventPresenter.ts, src/frontend/client/tests/BattleEventPresentation.test.ts, src/frontend/client/tests/SceneLifecycle.test.ts
```

---

## Objective

Add an ordered, sequential presentation timeline to the in-battle Phaser scene so that a resolved Swap is shown as visible phases — swap, match feedback, approved gravity/refill interpolation, each cascade pass, player damage, boss retaliation, and the terminal outcome — with the player's input locked for the duration of that timeline and released safely on completion, error, supersession, or scene shutdown, using only server-authoritative data already delivered by the existing wire contract.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Match-3 (8×8 board, Cascade, Combo) is IN scope
- `docs/01-game-design/GAME_RULES.md` §17 — fixed event resolution step order
- `docs/01-game-design/GAME_RULES.md` §18 — server-authoritative battle (client may only request actions)
- `docs/01-game-design/MATCH3_RULES.md` §4.2 — pass sequencing and cascade depth (`d ≥ 2` is a Cascade)
- `docs/01-game-design/MATCH3_RULES.md` §4.4 items 3, 7 — gravity never reorders gems and emits no events
- `docs/01-game-design/COMBAT_RULES.md` §1–§6 — combat step order feeding the damage events
- `docs/02-technical/GAME_EVENTS.md` §1 — **the authority for event ordering within one resolution, including damage followed by `BattleWon` / `BattleLost`**
- `docs/02-technical/GAME_EVENTS.md` §1.1 — the board cycle ordering contract (cascade pass placement)
- `docs/02-technical/GAME_STATE.md` §2.0 / §2.1 — board and battle-state fields
- `docs/02-technical/GAME_STATE.md` §5.3 items 3–4 — a snapshot is taken between resolutions and carries no cascade history
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3 / §3.1 — `ReceiveEvents` envelope, write-back-before-batch, atomicity
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.1 items 2 and 5 — no per-step delivery; the client does not compute gravity results or spawned Gems
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.6 `MatchCreated`, §3.2.7 `CascadeCreated`, §3.2.9 `GemMatched`
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.14 `DamageDealt`, §3.2.15 `DamageTaken`, §3.2.19 `BattleWon` / `BattleLost`
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4 item 11 and §8 items 5–9 — `BattleStateUpdated` is the only state push; no new message, method, or subscription
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — server authority
- `docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md` — Phaser owns battle rendering and presentation
- `docs/03-decisions/ADR/ADR-004-signalr-realtime.md` — realtime transport
- `docs/03-decisions/ADR/ADR-008-battle-recovery.md` — snapshot-based recovery, no event replay
- `.ai/skills/client/phaser-battle-presentation/SKILL.md` — animation sequencing pipeline and input locking
- `tasks/TASK_LIFECYCLE.md` §6 — two-stage commit protocol

---

## Product-Owner Decisions Applied (authorized, recorded)

These were approved for this work and are recorded here as task constraints. They are **not** documentation changes; the authoritative technical documents are unchanged by this task.

```text
PD-1  Gravity/refill: visual interpolation between server-authoritative
      results is ALLOWED. Client-side computation of match detection,
      gravity results, spawned Gem identities, RNG, damage, HP, or any
      other authoritative gameplay result remains FORBIDDEN.

PD-2  Presentation sequencing: a client-side presentation queue/timeline
      and an input lock covering that presentation are APPROVED. This
      deliberately supersedes TASK-210 §8's prior "no queue and no input
      lock of its own" decision for this presentation path.

PD-3  SignalR: NO protocol changes are authorized.

PD-4  Intermediate board snapshots: do NOT add per-pass board snapshots
      or any new wire payload.

PD-5  Event ordering: cite GAME_EVENTS.md §1 as the authority, including
      damage followed by BattleWon / BattleLost. Do not attribute that
      ordering guarantee solely to SIGNALR_PROTOCOL.md.

PD-6  Event locations: DamageDealt is SIGNALR_PROTOCOL.md §3.2.14;
      DamageTaken is §3.2.15.

PD-7  Status effects: do NOT add a status-effect-duration event.

PD-8  Interpolation boundary (clarification of PD-1, authorized):
      For presentation only, the client MAY pair existing gem sprites with
      destination cells using within-column relative order to animate the
      transition from the previously rendered board to the authoritative
      final board. This permission does NOT authorize the client to:
        - calculate gravity destinations by applying gameplay rules;
        - infer or generate spawned Gem identities;
        - perform match detection or calculate cascade results;
        - construct intermediate authoritative board states;
        - use presentation pairing to determine gameplay state.
      The authoritative board remains exclusively server-provided. The
      pairing is a visual heuristic, NOT a server-guaranteed Gem identity
      mapping.
```

---

## Conflict Analysis — Resolved by PD-8

This section records the conflict determination required by `AGENTS.md` §4 and its resolution. **No authoritative document has been modified** — the resolution is a Product-Owner authorization recorded here as a task constraint.

### The two constraints

```text
C-1  TASK-088 Stop Conditions (tasks/completed/TASK-088-...md):
     "If presentation requires reconstructing intermediate board states
      (gravity / spawn / per-pass frames): STOP per AGENTS.md §10 and
      SIGNALR_PROTOCOL.md §3.1 items 2/5 — intermediate states are never sent"

C-2  SIGNALR_PROTOCOL.md §3.1 item 5:
     "it does not compute matches, Cascades, Combo, Special Gems, gravity
      results, or spawned Gems, and never draws from rngSeed/rngState"
```

### The determination

PD-1 and these constraints are **reconcilable**, but only under a strict reading that this task fixes as its boundary. The distinction is between *reading a delivered value* and *producing a value from the rules*:

```text
PERMITTED (presentation of delivered data)     FORBIDDEN (reconstruction)
─────────────────────────────────────────────  ──────────────────────────────────────
Tweening a gem's display object between its    Applying MATCH3_RULES.md §4.4's
delivered prior cell and its destination        compaction rule client-side to COMPUTE
READ FROM the authoritative pushed board        which cell a gem falls to
Fading / popping exactly the cells named by    Deriving cleared cells by running match
MatchCreated.cells / GemMatched.cellIndex       detection
Entering new gems from above at cells whose    Deriving spawned Gem identity from
identity is READ from the pushed board          rngSeed / rngState / any client RNG
Playing N cascade phases counted from the      Fabricating a per-pass intermediate
delivered CascadeCreated.cascadeDepth values    board the server never sent
```

### The ambiguity that was recorded

`MATCH3_RULES.md` §4.4 item 7 states gravity **emits no events**, and item 3 states gems never change relative order within a column. The delivered event stream therefore does **not** associate a specific pre-fall gem with its specific post-fall cell. To animate a fall, the client must pair the two by column-relative ordering of the two delivered boards.

That pairing is a partial re-derivation of §4.4 item 3, and is precisely the behaviour C-1/C-2 describe. At intake, PD-1 authorized interpolation "based on server-authoritative results" but did not say in terms whether within-column relative-order pairing was covered, and neither C-1 (an immutable completed-task stop condition) nor C-2 (an authoritative technical document) had been amended.

### Resolution — PD-8

The Product Owner has now resolved this exact ambiguity by authorizing the pairing explicitly and bounding it:

- **Authorized:** pairing *existing* gem sprites with destination cells using **within-column relative order**, to animate the transition from the previously rendered board to the authoritative final board — **for presentation only**.
- **Still forbidden:** calculating gravity destinations by applying gameplay rules; inferring or generating spawned Gem identities; performing match detection or calculating cascade results; constructing intermediate authoritative board states; using presentation pairing to determine gameplay state.
- **Bound:** the authoritative board remains exclusively server-provided, and the pairing is a **visual heuristic, not a server-guaranteed Gem identity mapping**.

This satisfies the clarification the Conflict Analysis previously identified as outstanding. C-1 remains intact as written: the task does not reconstruct intermediate board states, does not compute gravity results, and does not infer spawned identities — it moves an already-rendered sprite to a cell whose occupant is read from the authoritative board. C-2 remains intact for the same reason.

### Ownership

`SIGNALR_PROTOCOL.md` §3.1 owns the client-computation boundary (`docs/02-technical/`, per `AGENTS.md` §2). **No wording change to that document is made or required by this resolution**: PD-8 is an explicit Product-Owner authorization recorded as a task constraint, consistent with how PD-1…PD-7 are already recorded. If a future task wishes to codify the boundary in the protocol document, that remains a separate protocol-document change with its own task and approval.

### Lifecycle consequence

The blocking clarification is resolved, so the task is no longer held in BACKLOG by this conflict. Readiness is re-evaluated in the section below.

---

## Scope

### In Scope

- Introduce persistent, indexed gem display objects in `BattleScene` so a gem's view survives a `BattleStateUpdated` push and can be tweened, replacing the current destroy-and-recreate board redraw.
- Add a scene-local ordered presentation timeline that plays, in `GAME_EVENTS.md` §1 order:
  - swap movement;
  - match feedback on the delivered `MatchCreated.cells` / `GemMatched.cellIndex`;
  - approved gravity/refill interpolation under the PD-1 / PD-8 boundary above, pairing existing gem sprites to destination cells by within-column relative order for presentation only;
  - one phase per delivered cascade pass, keyed on `CascadeCreated.cascadeDepth`;
  - player-damage phase and boss-retaliation phase, separated using the delivered `source` / `target` of `DamageDealt` / `DamageTaken`;
  - terminal outcome handoff on `BattleWon` / `BattleLost`.
- Tween the HP gauges so a decrease is visible rather than instantaneous.
- Replace the scene's single `presentationLocked` boolean with a timeline-aware input guard that reports locked for the whole timeline.
- Release the lock and clean up on: timeline completion, presentation error, a newer authoritative state / higher `serverSequence` arriving (supersession → snap to authoritative board), and scene `shutdown()` / `DESTROY`.
- Add unit and scene-lifecycle tests for ordering, multi-cascade playback, input locking, outcome handoff, and teardown.
- Follow the two-stage commit protocol (`tasks/TASK_LIFECYCLE.md` §6).

### Out of Scope

- Any backend, Domain, Application, or SignalR change (`PD-3`).
- Any new hub method, event type, subscription, or wire member; in particular **no per-pass board snapshots and no new payloads** (`PD-4`).
- Any status-effect-duration event (`PD-7`); status-effect state continues to arrive only as state in the `BattleStateUpdated` push.
- Client-side computation of match detection, gravity results, spawned Gem identities, RNG, damage, HP, combo, or any authoritative gameplay value (`AGENTS.md` §10, `PD-1`).
- Treating the PD-8 presentation pairing as a server-guaranteed Gem identity mapping, or using it to determine any gameplay state (`PD-8`).
- Editing `tasks/completed/*`, `tasks/blocked/*`, `docs/`, or ADRs (`AGENTS.md` §16/§17/§18).
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.
- Unrelated cleanup or refactoring beyond what the timeline requires (`AGENTS.md` §16).

---

## Current State

`BattleScene.renderBoard` destroys and recreates every cell on each state push (`this.boardLayer.removeAll(true)`), so there are no persistent gem objects to animate. `BattleScene.presentEventBatch` is a synchronous pass that spawns highlights and floaters and releases the input guard through a short alpha tween; it holds no queue and no timeline. `BattleScene.updateGauge` sets the gauge fill size directly with no tween, so HP changes are not animated. The input guard is the single boolean reported by `BattleScene.isInputLocked()` (`presentationLocked || swapPending || actionInFlight`). `BattleEventPresenter` already parses all fourteen non-outcome event types plus the outcome events and performs zero gameplay calculation. TASK-207 recorded the resulting gap as **G15** ("no pop/gravity animation; highlights land on the post-resolution board", priority P3).

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-245:
```text
src/frontend/client/src/game/scenes/BattleScene.ts
src/frontend/client/src/game/scenes/BattleEventPresenter.ts
src/frontend/client/tests/BattleEventPresentation.test.ts
src/frontend/client/tests/SceneLifecycle.test.ts
```

*(Note: The task manifest file itself is excluded from the implementation declared file set. The list above must strictly match the `Declared Files:` field in `## Metadata`.)*

**Shared-file ownership:** `BattleScene.ts`, `BattleEventPresenter.ts`, and `BattleEventPresentation.test.ts` were last owned by TASK-232 (`Status: DONE`, immutable). This task re-declares them as its own implementation surface; per `TASK_LIFECYCLE.md` §6.1 item 5, if any of these files is concurrently declared by another `tasks/active/` task, the slices must be combined into a single joint group commit rather than committed separately. No such active task exists at the time of filing (both `tasks/active/` and `tasks/blocked/` contain only `.gitkeep`).

**Unowned / pre-existing disclosure (P-3):** `src/frontend/client/pnpm-workspace.yaml` carries a pre-existing uncommitted modification (`esbuild: true`) that predates this task. It is **not** part of this task's declared file set and must not be staged, reverted, or included in either commit phase.

---

## Acceptance Criteria

- [ ] The board is rendered from persistent, indexed gem display objects; a normal resolution no longer destroys and recreates all 64 cells.
- [ ] A delivered multi-pass resolution plays one ordered phase per delivered `CascadeCreated.cascadeDepth`, in `GAME_EVENTS.md` §1 order.
- [ ] Player damage and boss retaliation are presented as two distinct, sequentially ordered phases, distinguished using only the delivered `source` / `target` of `DamageDealt` / `DamageTaken` (`§3.2.14` / `§3.2.15`).
- [ ] Gravity/refill motion uses only values from the authoritative boards; no cell is cleared, moved, or spawned by client-side rule evaluation (`PD-1`, `PD-8`, `AGENTS.md` §10).
- [ ] Presentation pairing of a rendered gem to a destination cell uses within-column relative order only, and its result is never read back to determine gameplay state (`PD-8`).
- [ ] `BattleWon` / `BattleLost` handoff to `ResultScene` occurs after the preceding damage phases, consistent with `GAME_EVENTS.md` §1, and is never delayed or blocked by the timeline.
- [ ] HP gauges animate to their delivered values rather than jumping.
- [ ] The input guard reports locked for the entire timeline and blocks swap/action submission throughout.
- [ ] The input guard is released and no tween or listener leaks on: completion, presentation error, supersession by a newer authoritative state, and scene `shutdown()` / `DESTROY`.
- [ ] When a newer authoritative state arrives mid-playback, the scene aborts playback and converges on the authoritative board with no orphaned display objects.
- [ ] Zero client-authoritative gameplay computation: no HP accumulation, no damage derivation, no match detection, no gravity/spawn derivation (`AGENTS.md` §10).
- [ ] The SignalR wire contract is unchanged: no new/renamed member on `BattleStateUpdated` or `ReceiveEvents`, and `CascadeCreated` still carries only `{ type, cascadeDepth }` (`PD-3`, `PD-4`).
- [ ] All relevant tests pass at the required validation depth (`.ai/workflow/core/validation.md` §2).
- [ ] Quality review checklist passes (`.ai/workflow/quality/review.md` §1).
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

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

- `BattleScene.ts` — `renderBoard` / `drawCell` own the destructive redraw that must become a persistent view map; `presentEventBatch` owns the current synchronous presentation pass; `updateGauge` owns the un-tweened gauge write; `isInputLocked` owns the guard; `shutdown` owns teardown (`tweens.killAll()` is already called there).
- `BattleEventPresenter.ts` — presentation-only parsing/formatting helpers; extend without adding gameplay logic. It already exposes `parseInBattleEvent`, `selectBatchCallout`, `formatInBattleEvent`, and the Special Gem presentation helpers.
- The timeline is expected to be a single scene-local ordered structure with a completion callback — not an interface, registry, generic queue, or event bus (`AGENTS.md` §9).
- Supersession should key on the delivered `serverSequence` from the `ReceiveEvents` envelope and the sequence on the `BattleStateUpdated` push; state arrives through the existing `GameRuntime.receiveBattleState` ingestion path.
- `BattleScene.findOutcomeEvent` already identifies the terminal outcome members; the timeline must not delay that handoff.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — timeline step ordering for a multi-cascade batch; gravity/refill
                         interpolation draws only delivered values; input-guard lifecycle;
                         supersession/convergence; malformed-event tolerance
[x] Integration tests  — wire-contract regression: BattleStateUpdated and ReceiveEvents
                         member sets unchanged; CascadeCreated still { type, cascadeDepth }
[x] Gameplay scenarios — Given a Swap that produces a Cascade
                         When the server resolves it and delivers one batch
                         Then phases play in GAME_EVENTS.md §1 order
                         And input stays locked until the last phase completes
                         And a Win/Loss outcome still reaches ResultScene
```

### Key Edge Cases
- Multi-pass resolution (≥ 2 `CascadeCreated` events) — one phase per delivered depth (`MATCH3_RULES.md` §4.2).
- `MatchCreated.cascadeDepth` vs `CascadeCreated.cascadeDepth` for the same pass are distinct values and must never be equated (`SIGNALR_PROTOCOL.md` §3.2.7 item 2).
- Boss-phase batch: `PassiveCharged(source="boss")` → `BossSkillCast` → `Damage* (source="boss", target="player")` in one batch (`GAME_RULES.md` §17 steps 18a–18c).
- A rejected Swap emits no events and must produce no timeline (`GAME_EVENTS.md` §1.2).
- Scene shut down mid-timeline (battle abandoned / teardown) — guard released, no leaked tweens or listeners, no transition.
- A newer authoritative state or reconnect snapshot arriving mid-playback — abort and converge (`ADR-008`; `GAME_STATE.md` §5.3 items 3–4).
- An unknown or malformed event type inside the batch — ignored safely with no fabricated presentation.

---

## Stop Conditions

- If a proposed animation step cannot be expressed within the PD-1 / PD-8 boundary — i.e. it would require calculating a gravity destination by gameplay rules, inferring or generating a spawned Gem identity, performing match detection, calculating a cascade result, or constructing an intermediate authoritative board state: **STOP** per `AGENTS.md` §10 — report rather than widening the boundary.
- If a proposed animation step would treat the PD-8 presentation pairing as a server-guaranteed Gem identity mapping, or use it to determine gameplay state: **STOP** per `PD-8`.
- If any required behaviour cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7.
- If satisfying a criterion requires client-authoritative gameplay calculation: STOP per `AGENTS.md` §10.
- If satisfying a criterion requires a new hub method, event, subscription, or wire member, or a per-pass snapshot: STOP per `AGENTS.md` §18 and `SIGNALR_PROTOCOL.md` §8 items 5–9 — that is a protocol change with its own task and ADR.
- If a status-effect-duration presentation is required beyond delivered state: STOP per `PD-7`.
- If satisfying a criterion requires editing `tasks/completed/*`, `tasks/blocked/*`, `docs/`, or an ADR: STOP per `AGENTS.md` §16/§17/§18 — report instead.
- If the task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose.

---

## Readiness Evaluation (BACKLOG → READY)

Re-evaluated against `tasks/TASK_LIFECYCLE.md` §3 ("What must happen before → READY") after the PD-8 clarification.

```text
[x] Task type confirmed (TASK_TYPES.md)
    FEATURE — development/feature.md. The capability has a documented home
    (MVP_SCOPE.md §1 Match-3; the phaser-battle-presentation skill) and the
    task builds it. No gameplay rule is invented or changed, so this is not
    GAMEPLAY-CHANGE; no ADR or architecture change is made, so it is not
    ARCHITECTURE.

[x] Relevant documentation exists in docs/
    All Authoritative References verified present at their cited paths and
    sections (GAME_EVENTS.md §1; SIGNALR_PROTOCOL.md §3.1, §3.2.x, §4, §8;
    GAME_STATE.md §2, §5.3; MATCH3_RULES.md §4.2, §4.4; GAME_RULES.md §17,
    §18; ADR-001/003/004/008).

[x] MVP scope confirmed (MVP_SCOPE.md §1)
    "Match-3" is IN: 8x8 board, Cascade, Combo. Presentation of those systems
    is in scope; nothing OUT in §2 is touched.

[x] Task is not blocked by an unresolved dependency
    TASK-088, TASK-210, TASK-207 (G15) are all Status: DONE and immutable.
    The one blocking item at intake — the §4.4 item-3 pairing ambiguity — is
    resolved by PD-8 (see Conflict Analysis). No other dependency is open.

[x] Primary agent assigned
    client.

[x] Workflow assigned
    development/feature.md.

[x] Acceptance criteria are testable
    All criteria are binary and observable (object reuse, ordered phases,
    distinct damage phases, lock state, leak-free teardown, unchanged wire
    members, zero gameplay computation).

[x] Declared file set established from actual inspection
    All four paths verified to exist. The Declared Files metadata field and
    the Declared File Set block match exactly. Shared-file ownership against
    TASK-232 is disclosed.

[x] Stop conditions are complete and do not contradict the resolution
    The former blocking stop condition is replaced by bounded stop
    conditions restating the PD-8 prohibitions.
```

**Determination: every BACKLOG → READY criterion passes. The task is promoted to READY.**

**Residual risk carried into execution (not a blocker):** PD-8 defines the pairing as a *visual heuristic, not a server-guaranteed Gem identity mapping*. A pairing can therefore differ from the server's true internal identity association while still landing every sprite on a correct authoritative destination cell. Acceptance is therefore stated in terms of **destination cells converging on the authoritative board**, never in terms of sprite identity matching a server-side gem identity — because the wire carries no such identity.

---

## Completion Evidence

### Commit
- Implementation slice (Phase A): `95dad7d454200d725878458b1bb5562d8b3be7ee`
- Subject: `feat: sequence battle resolution presentation with a cascade timeline`
- Owns: `TASK-245`
- Files: `src/frontend/client/src/game/scenes/BattleScene.ts`, `src/frontend/client/src/game/scenes/BattleEventPresenter.ts`, `src/frontend/client/tests/BattleEventPresentation.test.ts`, `src/frontend/client/tests/SceneLifecycle.test.ts`
- Shared with: `none`
- Unowned / pre-existing: `src/frontend/client/pnpm-workspace.yaml` (not owned, not staged; its SHA-256 was unchanged before and after this task)

### Changed Files
- `src/frontend/client/src/game/scenes/BattleScene.ts` — persistent indexed gem views replacing the destroy-and-recreate redraw; the scene-local presentation timeline (swap, match/`cascadeDepth` phases, PD-8 settle, damage, retaliation, outcome) with a timeline-aware input guard; held board/HP presentation released by the timeline, an abort, or the next push; supersession by a newer `serverSequence`; animated HP gauges; teardown of every timeline resource.
- `src/frontend/client/src/game/scenes/BattleEventPresenter.ts` — presentation-only damage party vocabulary and `resolveDamagePresentationPhase`, classifying a delivered `DamageDealt`/`DamageTaken` into its ordered combat phase from `source`/`target` alone.
- `src/frontend/client/tests/BattleEventPresentation.test.ts` — 6 new tests: the combat-phase classifier as a unit, and the unchanged-wire regression pins (three-member `ReceiveEvents` envelope, `CascadeCreated` = `{ type, cascadeDepth }`, extra members unread, no board-resolution message accepted).
- `src/frontend/client/tests/SceneLifecycle.test.ts` — opt-in recorded tween manager plus live text/rectangle view state in the shared harness; 18 new timeline tests (phase ordering, persistence, held convergence, damage/retaliation, lock lifecycle, supersession, error, teardown, gauge travel, outcome handoff, rejected swap, unknown events, authority source pins).

### Validation Results
- `npx vitest run tests/BattleEventPresentation.test.ts tests/SceneLifecycle.test.ts` — PASS (269 tests; baseline 245)
- `npx vitest run` (full client suite) — PASS (23 files, 968 tests; baseline before this task: 944)
- `npx tsc --noEmit` — PASS
- `npx vite build` — PASS
- Not run: the local E2E browser suites (`verify:e2e:smoke`) — they require a running `GameServer.Api` (with its session signing secret), a Vite dev server and Chromium; that stack is not running in this session and the suites are outside this task's declared verification levels.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed P-1…P-6 commit policy compliance

