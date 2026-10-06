# TASK-202 — Post-Result Lifecycle Product Decision & Specification

<!--
  GEN-TASK EXECUTION MANIFEST — PRODUCT-OWNER DECISION RECORD & SPECIFICATION
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and tasks/ by path and section — it does NOT invent game
  rules, UX buttons, navigation routes, or runtime cleanup behavior.

  PROVENANCE (pre-decision): A repository-wide lifecycle audit established that
  ResultScene was the terminal scene in the implemented Phaser lifecycle
  (TDD.md §2.1, GameConfig.ts scene registration, TASK-087, TASK-149). GDD.md §2
  defined a "Core Gameplay Loop" that ends at "Rewards", and ROADMAP.md §1
  Phase 3 targets a "Shippable standalone web application", but NO authoritative
  Product Owner decision defined the post-result player flow. Prior tasks
  explicitly DEFERRED navigation rather than permanently prohibiting it:
    - TASK-087 Out of Scope:  "Post-result navigation — the documented lifecycle
      ends at ResultScene (TDD.md §2.1); no successor scene or button behavior
      is documented."
    - TASK-149 Out of Scope:  "Navigation, retry, or 'play again' behavior after
      ResultScene ... adding navigation is a separate decision this task does
      not make."
    - ResultScene.ts:60-61:   "Navigation after ResultScene is not in scope
      (TDD.md §2.1 lifecycle ends at ResultScene)."
  Those statements deferred a decision; they did not deny one. Per AGENTS.md §7,
  §20, and §23, an AI agent must NOT make that decision on the project's behalf.

  PROVENANCE (decision supplied): The Product Owner has now answered all four
  decisions. D-202-01 = C, D-202-02 = A, D-202-03 = D, D-202-04 = A. The answers
  are recorded verbatim in "PRODUCT OWNER APPROVED" below and were transcribed
  by the Product Owner, not chosen or inferred by an agent.

  THIS TASK'S DELIVERABLE IS THE RECORD + THE AUTHORIZED DOCUMENTATION
  INTEGRATION + THE DOWNSTREAM TASK HANDOFF. It records the approved decisions,
  integrates the approved player-facing flow into the owning authoritative
  documents (GDD.md §2, TDD.md §2.1), records the loadout state-carrier
  architectural REQUIREMENT/BOUNDARY that D-202-03 = D creates
  (ARCHITECTURE.md §2.2.3 — boundary only, no mechanism chosen), and creates the
  downstream implementation task (TASK-203). It selects no technical mechanism
  that the decisions do not already fix.

  BOUNDARY: this task file, the authorized sections of docs/00-overview/GDD.md,
  docs/02-technical/TDD.md, and docs/02-technical/ARCHITECTURE.md, and the new
  tasks/backlog/TASK-203-*.md only. Zero files under src/. Zero files under
  tests/. No implementation.
-->

---

## Metadata

```text
Task ID:            TASK-202
Type:               DOCUMENTATION (TASK_TYPES.md §2 — decision record,
                    specification & authorized documentation integration;
                    documentation/documentation-change.md)
Status:             DONE
Risk:               LOW (decision record and authorized documentation
                    integration only; zero production code, zero tests, zero
                    game-rule/contract behavior change)
Priority:           HIGH (unblocks the post-result player loop and closes the
                    standalone web loop gap noted in ROADMAP.md §1 Phase 3)
Primary Agent:      review (TASK_TYPES.md §2 / AGENT_SELECTION.md §1)
Supporting Agents:  client, gameplay, orchestrator
Workflow:           documentation/documentation-change.md
Skills:             discovery/documentation-discovery,
                    discovery/impact-analysis,
                    quality/documentation-consistency,
                    quality/scope-validation
Dependencies:       TASK-201 (DONE — reconciled backlog lifecycle baseline)
Decision authority: Product Owner — D-202-01 … D-202-04, APPROVED (recorded in
                    "PRODUCT OWNER APPROVED" below)
Blocks:             TASK-203 (the downstream implementation task created here)
```

**Lifecycle note.** This task was executed directly. Transition path per
`tasks/TASK_LIFECYCLE.md` §2: `BACKLOG → READY` (the Product Owner decision
supplied the missing input; every READY criterion in §3 is now met) `→
IN PROGRESS → IN REVIEW → DONE`, with the single permitted file move
`backlog/ → completed/`. `tasks/README.md` §4: the filename is unchanged, only
the folder moved.

---

## Objective

Record the four authoritative Product Owner decisions governing the player's
post-result lifecycle after `ResultScene`, integrate the approved player-facing
flow into the owning authoritative documents (`GDD.md` §2, `TDD.md` §2.1),
record the loadout state-carrier **architectural requirement/boundary** that
D-202-03 = D creates without choosing a mechanism, and hand the approved flow to
a downstream implementation task (`TASK-203`).

**This task records and formalizes the decision. It does not implement it.**

---

## PRODUCT OWNER APPROVED

> **D-202-01 THROUGH D-202-04 ARE ANSWERED AND APPROVED.**
>
> The decisions below were supplied by the Product Owner as this task's input.
> They are authoritative product decisions. They are recorded verbatim and are
> **not** open to reinterpretation, replacement, or extension by an agent
> (`AGENTS.md` §7, §20, §23).
>
> This approval authorizes the documentation integration this task performed and
> the downstream implementation task it created. It does **not** authorize
> implementation inside this task: no `src/` or `tests/` file is in scope.

### Approved Decisions — verbatim Product Owner record

```text
D-202-01 = C
Result destination:
- ResultScene provides:
  - PLAY AGAIN → LobbyScene
  - MAIN MENU → MainMenuScene

D-202-02 = A
Interaction:
- Explicit UI buttons.
- Do not use full-screen tap/any-key/automatic transition as the primary navigation mechanism.

D-202-03 = D
Loadout:
- Preserve the previous loadout when returning through PLAY AGAIN.
- The player must still be able to edit/change the loadout in LobbyScene before starting the next battle.

D-202-04 = A
Runtime cleanup:
- Clear active battle state when leaving the completed battle.
- Do not allow stale battle/session state to affect the next battle.
```

### Decision Manifest

| ID       | Decision                | Answer | Status             |
| -------- | ----------------------- | ------ | ------------------ |
| D-202-01 | Post-result destination | **C**  | **APPROVED**       |
| D-202-02 | Interaction model       | **A**  | **APPROVED**       |
| D-202-03 | Loadout behavior        | **D**  | **APPROVED**       |
| D-202-04 | Runtime cleanup         | **A**  | **APPROVED**       |

### Approval Record

```text
D-202-01: C — APPROVED (Product Owner)
D-202-02: A — APPROVED (Product Owner)
D-202-03: D — APPROVED (Product Owner)
D-202-04: A — APPROVED (Product Owner)

Approval source:  Product Owner decision supplied as TASK-202 task input.
                  No repository document, ADR, or commit is the source; the
                  Product Owner's decision text is the authority.
Approval date:    (not recorded in the decision text; no date was supplied)
```

A decision is **APPROVED** only when it is supplied by the Product Owner. Every
row above is APPROVED on that basis. Nothing in this task, and nothing in
`docs/`, records an approved product choice that the Product Owner did not
supply.

**Interpretation boundary (recorded, not a reinterpretation).** The pre-decision
manifest described the option letters with non-binding wording. The approved
meaning of `D-202-01 = C` is taken **verbatim from the Product Owner's decision
text above** — `ResultScene` provides `PLAY AGAIN → LobbyScene` and
`MAIN MENU → MainMenuScene` — and not from the earlier option description. That
text is the authority; the earlier wording is retained below only as historical
record.

---

## Product Owner Decisions vs. Downstream Technical Work

The boundary between what is **decided** (closed, not an agent's to change) and
what is **still technical implementation work** (open, owned by `TASK-203`) is:

| Item | Status | Owner |
| --- | --- | --- |
| `ResultScene` exposes `PLAY AGAIN` and `MAIN MENU` | **PO DECIDED** (D-202-01 = C) | closed |
| `PLAY AGAIN` destination is `LobbyScene` | **PO DECIDED** (D-202-01 = C) | closed |
| `MAIN MENU` destination is `MainMenuScene` | **PO DECIDED** (D-202-01 = C) | closed |
| Navigation is driven by explicit UI buttons | **PO DECIDED** (D-202-02 = A) | closed |
| No full-screen tap / any-key / automatic transition as the primary navigation mechanism | **PO DECIDED** (D-202-02 = A) | closed |
| The previous loadout is preserved when returning through `PLAY AGAIN` | **PO DECIDED** (D-202-03 = D) | closed |
| The player can still edit/change the loadout in `LobbyScene` before the next battle | **PO DECIDED** (D-202-03 = D) | closed |
| Active battle state is cleared when leaving the completed battle | **PO DECIDED** (D-202-04 = A) | closed |
| Stale battle/session state must not affect the next battle | **PO DECIDED** (D-202-04 = A) | closed |
| Button geometry, labels, layout, ordering, and styling | **DOWNSTREAM TECHNICAL** | `TASK-203` |
| Which concrete navigation call / guard shape is used | **DOWNSTREAM TECHNICAL** | `TASK-203` |
| The concrete **shape and ownership** of the loadout state carrier that D-202-03 = D requires | **DOWNSTREAM ARCHITECTURE DECISION** (requirement/boundary recorded in `ARCHITECTURE.md` §2.2.3; mechanism NOT chosen here, `AGENTS.md` §18) | `TASK-203` (record first) |
| The concrete runtime API/contract for clearing active battle state | **DOWNSTREAM TECHNICAL** | `TASK-203` |
| Test names, placement, and depth | **DOWNSTREAM TECHNICAL** | `TASK-203` |
| Any server-side / REST / SignalR / Redis / database change | **NOT REQUIRED** — no approved decision implies one | n/a |

No row marked **PO DECIDED** may be changed by the downstream task. No row
marked **DOWNSTREAM** may be decided by this task.

---

## Authoritative References

- `docs/00-overview/GDD.md` §2 — Core Gameplay Loop; **updated by this task**
  with the approved post-result player flow
- `docs/00-overview/MVP_SCOPE.md` §1 (IN), §2 (OUT), §3 (FUTURE), §4
  (classification authority)
- `docs/00-overview/ROADMAP.md` §1 — Phase 3 "Shippable standalone web
  application" goal
- `docs/02-technical/TDD.md` §2.1 — Phaser scene lifecycle; **updated by this
  task** with the approved lifecycle and its continuations
- `docs/02-technical/ARCHITECTURE.md` §1 (client scene tree), §2.2.1 (runtime
  coordination rules 1–6), §2.2.3 (pre-battle selection boundary; **updated by
  this task** with the loadout state-carrier requirement/boundary), §5
  (anti-overengineering)
- `docs/02-technical/API_CONTRACTS.md` §3 (`POST /api/battle/start`), §4
  (`GET /api/battle/{battleId}/result` including notes 1–7), §5.1–§5.6
  (collection reads; §5.6 no equip/loadout state)
- `docs/02-technical/REDIS_STATE.md` §3 (active-state lifecycle and battle-end
  delete), §4 (concurrency/`Sequence`), §6 (what is not here)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §1–§2 (connection, `JoinBattle`, client
  actions), §4 (`BattleStateUpdated`), §7 (reconnect/resync snapshot, §7.3
  `BATTLE_NOT_FOUND` fallback), §8.3 (no battle lifecycle/status message)
- `docs/02-technical/GAME_STATE.md` §2.0.5, §2.2, §2.3, §2.4 (delivered state),
  §4 (Client Presentation State — client-owned, non-authoritative)
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md`
- `docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md`
- `docs/03-decisions/ADR/ADR-005-redis-active-battle-state.md`
- `docs/03-decisions/ADR/ADR-008-battle-recovery.md`
- `docs/03-decisions/ADR/ADR-015-application-session-authentication-contract.md`
- `docs/03-decisions/ADR/ADR-020-standalone-web-account-authentication.md`
- `tasks/completed/TASK-087-client-battle-outcome-result-scene.md` — ResultScene
  creation and the outcome handoff
- `tasks/completed/TASK-149-present-persisted-reward-summary-in-result-scene.md`
  — reward presentation; the explicit post-result navigation deferral
- `tasks/completed/TASK-181-local-web-development-authentication-and-playability.md`
  — local playability baseline (SUPERSEDED by ADR-020/TASK-187)
- `tasks/completed/TASK-190-collection-viewer-ui-scene.md` — `MainMenuScene`
  navigation precedent (`COLLECTION` button and `< BACK` return)
- `tasks/completed/TASK-201-reconcile-lifecycle-state-of-task-190-191-196.md` —
  lifecycle reconciliation baseline; recorded TASK-202 as the next task ID
- `tasks/backlog/TASK-203-post-result-lifecycle-implementation.md` — **created by
  this task**; the downstream implementation of the approved decisions

---

## Current State (verified against the working tree at decision time)

Recorded as the pre-implementation baseline the downstream task starts from.
**Nothing in this section was changed by this task.**

```text
src/frontend/client/src/game/scenes/ResultScene.ts
  - lines 14-29   ResultSceneData: outcome, finalBossHp, finalPlayerHp, battleId?
  - lines 63-102  scene fields, init/create/shutdown
  - lines 104-160 drawShell(): shell, outcome text, boss/player HP text,
                  reward area
  - lines 162-190 renderResult(): renders the three delivered outcome members
  - lines 202-230 loadRewards(): reads GET /api/battle/{battleId}/result through
                  the runtime port and renders the delivered rewards
  - lines 60-61   "Navigation after ResultScene is not in scope (TDD.md §2.1
                  lifecycle ends at ResultScene)."
  => no interactive control, no pointer/keyboard listener, no scene.start(),
     no automatic transition.

src/frontend/client/src/game/scenes/BattleScene.ts
  - lines 893-930 handleBattleEvents(): first BattleWon/BattleLost transitions to
                  ResultScene with { ...outcome, battleId: envelope.battleId }
  - line  918     this.scene.start('ResultScene', {...}) — the only path into
                  ResultScene
  - lines 165-194 shutdown(): detaches subscriptions and clears scene state
  => BattleScene stops when ResultScene starts; ResultScene has no successor.

src/frontend/client/src/game/GameConfig.ts
  - lines 67-75   registered scene list: BootScene, PreloaderScene,
                  MainMenuScene, LobbyScene, CollectionViewerScene, BattleScene,
                  ResultScene
  => ResultScene is registered and reachable; MainMenuScene and LobbyScene are
     both registered and reachable, so both approved destinations already exist.

src/frontend/client/src/game/runtime/GameRuntime.ts
  - lines 116-130 fields: state, battleState, listeners, subscriptions
  - lines 256-282 dispose(): detaches subscriptions, clears listeners,
                  disconnects the transport
  - lines 679-697 handleBattleNotRecoverable(): battleState = null;
                  updateState({ sync: 'awaiting_battle' })  ← the ONLY place the
                  runtime currently drops a battle
  - lines 774-782 getBattleResult(): pass-through result read
  => the runtime holds one synchronized battle copy and one transport connection;
     no per-battle "cleanup" API exists beyond dispose() and the §7.3 path.

src/frontend/client/src/game/scenes/MainMenuScene.ts
  - lines 100-118 transitionTo(sceneKey) — the existing navigation precedent,
                  with a hasTransitioned guard
  => a documented scene-to-scene navigation pattern already exists to follow.

src/frontend/client/src/state/GameRuntimeState.ts
  - lines 65-86   GameRuntimeState: connection, session, runtime, sync, engine,
                  connectionId, lastError — technical state only
  => there is no gameplay field to "clear"; `sync: 'awaiting_battle'` is the
     documented connected-but-no-battle value.

src/frontend/client/src/game/scenes/LobbyScene.ts
  - line  186     private selectedPetId: string | null = null
  - lines 191/200 private selectedCardIds: string[] / selectedRelicIds: string[]
  - line  214     private selectedBossId: string | null = null
  - lines 282-292 shutdown(): the selection fields are reset
  => the pre-battle selection is ephemeral, scene-local state that does not
     survive a LobbyScene shutdown. This is the fact that makes D-202-03 = D
     require a state carrier (ARCHITECTURE.md §2.2.3, recorded by this task).

src/frontend/client/tests/
  - ResultScene.test.ts, SceneLifecycle.test.ts, LobbyScene.test.ts,
    GameRuntime.test.ts, RuntimeBoundaries.test.ts
  => the existing client test homes for the downstream task's tests.
```

**Establishing fact at decision time:** returning to any earlier screen required
a hard browser reload, which rebooted Phaser to `BootScene` → `PreloaderScene` →
`MainMenuScene`. D-202-01 = C removes that requirement in the approved design;
implementing it is `TASK-203`.

---

## The Decision Questions (resolved)

Each question below is **resolved**. The question, the options that were offered,
and the evidence-backed consequences are retained as the historical record of
what the Product Owner decided between; the **Approved answer** line is the
authority. Options are deliberately not re-ranked or re-scored.

### D-202-01 — Post-result destination

**Question:** After a battle reaches a terminal outcome and `ResultScene` has
presented it, where does the player go?

**Approved answer: `C`** — `ResultScene` provides `PLAY AGAIN → LobbyScene` and
`MAIN MENU → MainMenuScene` (verbatim Product Owner decision text).

```text
Options as recorded before the decision:
A. MainMenuScene
B. LobbyScene
C. MainMenuScene + PLAY AGAIN action
D. LobbyScene + PLAY AGAIN/RETRY action
E. ResultScene intentionally remains terminal
F. Other explicitly specified flow
```

**Recorded consequences.** `MainMenuScene` is the existing hub (`START BATTLE`,
`COLLECTION`) and matches the `CollectionViewerScene` → `MainMenuScene`
precedent (TASK-190), which is why it is the `MAIN MENU` destination.
`LobbyScene` is where the documented pre-battle selection flow lives (`TDD.md`
§2.1, `ARCHITECTURE.md` §2.2.3), so it is the only destination that can lead
directly to another battle, which is why it is the `PLAY AGAIN` destination.
Option `E` (the status quo) was **not** chosen: the lifecycle is now explicitly
non-terminal, and `TDD.md` §2.1 records it as such.

**Explicitly not added.** No stage progression, campaign map, energy/stamina
gate, or any other system `MVP_SCOPE.md` §2 lists as OUT or §3 lists as FUTURE.
Both approved destinations are scenes that already exist and are already in
scope.

---

### D-202-02 — Interaction model

**Question:** If `ResultScene` is not permanently terminal, how does the player
proceed?

**Approved answer: `A`** — explicit UI buttons. Full-screen tap, any-key, and
automatic transition are **not** the primary navigation mechanism (verbatim
Product Owner decision text).

```text
Options as recorded before the decision:
A. Explicit button(s)
B. Screen tap / click
C. Keyboard confirmation
D. Automatic timed transition
E. Other
```

**Recorded compatibility constraint satisfied.** `D-202-01 = C` with
`D-202-02 = A` is compatible: two distinct actions (`PLAY AGAIN`, `MAIN MENU`)
are expressible as two explicit buttons. The recorded incompatibility — a
full-screen tap area cannot express two distinct destinations — is exactly the
pairing the Product Owner avoided, and the approved interaction model is the one
that can express the approved two-destination flow.

**Recorded precedent.** No existing scene implements a keyboard listener or an
automatic transition; `MainMenuScene.transitionTo` (lines 112–118) and
`CollectionViewerScene`'s `< BACK` are pointer-driven. The approved model matches
the existing precedent rather than adding a second input surface.

---

### D-202-03 — Loadout behavior

**Question:** If the player returns to `LobbyScene` or starts another battle,
what happens to the previous battle's loadout?

**Approved answer: `D`** — the previous loadout is preserved when returning
through `PLAY AGAIN`, and the player must still be able to edit/change the
loadout in `LobbyScene` before starting the next battle (verbatim Product Owner
decision text).

```text
Options as recorded before the decision:
A. Preserve previous loadout
B. Reset to default loadout
C. Require explicit new selection
D. Preserve loadout but allow editing
E. Other
```

**Evidence-backed constraint this answer engages (requirement, not mechanism).**
The in-progress selection is **ephemeral, scene-local state owned by
`LobbyScene`**, created when the scene is and discarded when it shuts down
(`ARCHITECTURE.md` §2.2.3 rules 1–2; fields at `LobbyScene.ts:186`
`selectedPetId`, `:214` `selectedBossId`, selection sets for cards and relics,
reset at `LobbyScene.ts:289–292`). It is **not** modelled in
`state/GameRuntimeState.ts`, **not** held on `GameRuntime`, and has no store or
module of its own (§2.2.3 rule 1, `ARCHITECTURE.md` §5.5).

Consequently, `D-202-03 = D` **requires a state carrier** that survives the
`LobbyScene` → `BattleScene` → `ResultScene` → `LobbyScene` round trip
(`ARCHITECTURE.md` §2.2.3, "Post-Result Loadout Carrier — Architectural
Requirement"). The concrete shape, ownership, and lifetime of that carrier is an
**architecture decision that is still open** and must be recorded before it is
implemented (`AGENTS.md` §18; `TASK-203` first step). This task records the
requirement and the boundary only — it does **not** choose the mechanism.

Two boundary statements follow directly from the approved answer and are
recorded in `ARCHITECTURE.md` §2.2.3 and `TDD.md` §2.1:

```text
1. The preserved loadout is a STARTING POINT, not a lock. LobbyScene remains the
   editing surface: the player may change any part of the preserved loadout
   before starting the next battle (D-202-03 = D, second clause).
2. The preserved loadout is NOT active battle state. It is pre-battle selection
   state, so the D-202-04 = A cleanup of ACTIVE BATTLE state must not clear it —
   otherwise D-202-03 = D could not hold.
```

**Not inferred from the implementation.** That the selection is ephemeral today
is an observation about the code, not a decision about whether it should stay
ephemeral. The Product Owner has now decided that it must not.

---

### D-202-04 — Runtime cleanup

**Question:** What is the expected runtime state after a completed battle when
the player leaves `ResultScene`?

**Approved answer: `A`** — clear active battle state when leaving the completed
battle, and do not allow stale battle/session state to affect the next battle
(verbatim Product Owner decision text).

```text
Options as recorded before the decision:
A. Clear active battle state
B. Retain battle result only
C. Clear SignalR battle connection
D. Clear both active battle and connection state
E. Other explicitly specified behavior
```

**Evidence-backed constraint this answer engages.**

```text
GameRuntime.battleState : RuntimeBattleState | null
        the synchronized presentation copy of the server's last
        BattleStateUpdated push (SIGNALR_PROTOCOL.md §4, GAME_STATE.md §2.0.5).
        Not authoritative and not client-owned (GAME_STATE.md §4).

GameRuntime.state.sync : 'unsynchronized' | 'awaiting_battle' | 'synchronized'
        technical synchronization status (GameRuntimeState.ts:59).
        'awaiting_battle' is the documented value for "connected, no current
        battle" (GameRuntimeState.ts:56-57).
```

```text
Recorded precedent  GameRuntime.handleBattleNotRecoverable (lines 679-697)
                    battleState = null; updateState({ sync: 'awaiting_battle' })
                    → the ONLY place the runtime currently drops a battle.
                    It is contractually tied to SIGNALR_PROTOCOL.md §7.3's
                    BATTLE_NOT_FOUND, so whether it may also serve a normal
                    post-result exit is a contract question for the downstream
                    task to resolve and document — not a mechanism this task
                    chooses.

Recorded precedent  GameRuntime.dispose (lines 256-282)
                    process-lifetime teardown, NOT a per-battle cleanup. The
                    approved answer is not this.

NO CONNECTION CHANGE. The approved answer is A, not C or D: the SignalR
connection is NOT to be cleared by the post-result exit. The one-runtime,
one-connection rule (ARCHITECTURE.md §2.2.1 rule 6) and §7 reconnect (ADR-008)
therefore remain untouched, and no per-battle disconnect is introduced.

NO WIRE CHANGE. There is no battle lifecycle/status field, and none may be
added: SIGNALR_PROTOCOL.md §8.3 states no such message exists. The approved
cleanup is client-local state behavior only.

NO SERVER CHANGE. The authoritative Redis active-state record
(battle:{battleId}:state) is already created on battle start and deleted at
battle end after the result write (REDIS_STATE.md §3); it is not this task's or
the downstream task's to change.

SESSION IS NOT PERSISTED AND IS NOT CLEARED. The application session is held in
memory only (ADR-015 D5; ADR-020). "Do not allow stale ... session state to
affect the next battle" is satisfied by not introducing session persistence and
by not carrying a stale battle's session-scoped state forward — not by
introducing token persistence or a session reset mechanism, which no approved
decision requests.
```

---

## State & Security Analysis

Evidence-backed implementation constraints the approved decisions must account
for. This section informs implementation; it designs nothing beyond what the
approved answers require.

| Area | Classification | Evidence |
| --- | --- | --- |
| **Battle terminal state** | REQUIRED | A battle ends when `BattleWon`/`BattleLost` is resolved (`GAME_EVENTS.md` §2; `GAME_RULES.md` §17 step 18). The event is the only terminal signal; `BattleStateUpdated` always precedes it (`SIGNALR_PROTOCOL.md` §3.1, §6.4). There is no `Status`/lifecycle field to read (`SIGNALR_PROTOCOL.md` §8.3). Any post-result flow starts from an already-terminal battle. |
| **BattleResult persistence** | REQUIRED | `BattleResultId` = `BattleId`; at most one row per battle (`DATABASE.md` §1, `API_CONTRACTS.md` §4 note 2). Written before the Redis delete (`ARCHITECTURE.md` §4 item 4, `REDIS_STATE.md` §3). A second battle for the same player creates a *new* `BattleId` and a new row — `PLAY AGAIN` can never reuse or overwrite the previous result. |
| **`GET /api/battle/{battleId}/result`** | REQUIRED | Returns data only for a battle that has already ended (`API_CONTRACTS.md` §4). Requires an authenticated session (`401 UNAUTHENTICATED`, note 6) and owner-only access (`404 BATTLE_NOT_FOUND` for another player's battle, note 7). `ResultScene.loadRewards` (lines 202–230) is the existing consumer; it treats a failed read as "unavailable" and fabricates nothing. Leaving `ResultScene` must not change this posture. |
| **`POST /api/battle/start`** | REQUIRED | The only documented way to create a battle; requires the full gameplay loadout (`petId`, `bossId`, `cardLoadout` ×3, `relicLoadout` 3–5, `API_CONTRACTS.md` §3) and validates ownership/category/count/distinctness server-side. No gameplay-free variant exists and none may be introduced. `PLAY AGAIN` therefore returns to `LobbyScene` (where the request is built), never straight into a battle. `GameRuntime.startBattle` (lines 537–552) performs REST → connect → join. |
| **Redis active battle state** | NOT AFFECTED | `battle:{battleId}:state` is already created on battle start and deleted at battle end after the result write (`REDIS_STATE.md` §3). By the time `ResultScene` renders, the active-state record is already gone in the normal case (a failed delete falls back to the sliding TTL, §3 ¶2). The approved flow adds no key, changes no TTL, and reintroduces no active state. |
| **SignalR BattleHub** | NOT AFFECTED by the approved answer | `D-202-04 = A` is not `C`/`D`: the connection is retained. The hub connection is process-wide and already connected; `JoinBattle` is called by `startBattle` (§1.2, §2). No new hub method, event, or subscription is introduced by any approved answer. |
| **ApplicationSession** | REQUIRED | Session is held in memory only, never persisted (ADR-015 D5; `ApplicationSession.ts`). Every covered endpoint and the hub require it (`API_CONTRACTS.md` §1, §2.8; ADR-020). A page reload re-authenticates. The approved flow reuses the same session and must not introduce token persistence. |
| **Loadout state (state carrier)** | REQUIRED — the one open architecture question | In-progress selection is ephemeral and scene-local to `LobbyScene` (`ARCHITECTURE.md` §2.2.3 rules 1–2). Collection reads carry no equip state (`API_CONTRACTS.md` §5.6). `D-202-03 = D` therefore requires a carrier that survives the round trip; its concrete form is an architecture decision that must be recorded before implementation (`AGENTS.md` §18). The requirement and boundary are recorded in `ARCHITECTURE.md` §2.2.3; the mechanism is left open. |

**Classification key.** `REQUIRED` — the approved decisions cannot be
implemented without resolving this. `NOT AFFECTED` — the approved answers change
nothing here. `UNKNOWN` — **none**: each area has a documented contract or a
verified implementation in the working tree. The approved flow is bounded by
known contracts, not by missing information. The single open item is explicitly
an architecture **decision** (the loadout carrier's shape), not missing
information.

**Security-relevant invariants the approved flow must not violate.**

```text
1. The client never authors authoritative state (GAME_RULES.md §18, ADR-001).
   No post-result flow may compute an outcome, HP, reward, or progression value.
2. Ownership is never established from client input (API_CONTRACTS.md §4 note 7).
   The PLAY AGAIN flow must not send a playerId or reuse another battle's id.
3. No secret is introduced, logged, or returned; the session stays in memory
   (ADR-015 D5/D10; API_CONTRACTS.md §2.8).
4. 404 BATTLE_NOT_FOUND is used only for an authenticated caller's missing or
   non-owned battle — never for an unauthenticated caller (§4 note 6).
5. A preserved loadout is not authority: it becomes authoritative only when the
   server validates it in POST /api/battle/start (API_CONTRACTS.md §3,
   ARCHITECTURE.md §2.2.3 rule 5). Preserving it must not be turned into
   client-side legality checking.
```

---

## Scope

### In Scope (delivered)

- Record D-202-01 through D-202-04 with their exact Product Owner answers.
- Mark the decision manifest and approval record **PRODUCT OWNER APPROVED**.
- Preserve the decision rationale, the offered options, and the compatibility
  constraints as historical record.
- Distinguish the PO decisions from the technical implementation details that
  remain downstream work.
- Update `docs/00-overview/GDD.md` §2 with the approved player-facing
  post-result flow.
- Update `docs/02-technical/TDD.md` §2.1 with the approved lifecycle, the
  `PLAY AGAIN` loadout-preservation + editability rule, the explicit-button
  interaction decision, and the clear-completed-battle-state rule.
- Record in `docs/02-technical/ARCHITECTURE.md` §2.2.3 the **architectural
  requirement/boundary** that `D-202-03 = D` creates — and nothing more.
- Create the downstream implementation task, assigned the next valid repository
  task ID.
- Lifecycle bookkeeping for TASK-202 per `tasks/TASK_LIFECYCLE.md`.

### Out of Scope (not done, and not authorized here)

- **Implementing any part of the approved flow.** No `ResultScene` button, no
  navigation, no loadout carrier, no runtime cleanup code.
- **Any change under `src/frontend/` or `src/backend/`** — including
  `ResultScene.ts`, `BattleScene.ts`, `LobbyScene.ts`, `MainMenuScene.ts`,
  `GameConfig.ts`, `GameRuntime.ts`, and `GameRuntimeEvents.ts`.
- **Choosing a concrete loadout state-carrier mechanism.** That is an
  architecture decision reserved to the downstream task (`AGENTS.md` §18).
- **Any change to battle state, reward handling, or progression.**
- **Any new REST endpoint, wire member, SignalR method/event, Redis key, or
  database column.**
- **Any change to `tests/`**, to any other `tasks/completed/*`, or to
  `tasks/blocked/*`. (TASK-202's own move into `tasks/completed/` is in scope
  bookkeeping.)
- **Marking the downstream implementation task as DONE** — it is created as
  `BACKLOG`.
- **Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.**

---

## Implementation Notes — Downstream Implementation Boundary

*(This section carries the template's **Implementation Notes** role for this
task: it states where implementation is bounded, rather than how to write it.
This task performs no implementation.)*

The implementation is **not** this task. It is `TASK-203`.

```text
TASK-202  (this task — decision record, specification, authorized doc
           integration, boundary, downstream creation)          ← DONE
    ↓
PO decision on D-202-01 … D-202-04                               ← SUPPLIED
    ↓
authoritative GDD/TDD/ARCHITECTURE update                        ← DONE HERE
    ↓
TASK-203 (downstream implementation task)                        ← CREATED HERE
    ↓
ResultScene / runtime / tests                                    ← NOT HERE
```

**`TASK-203` is responsible for:**

1. Implementing `ResultScene`'s two explicit buttons exactly as approved:
   `PLAY AGAIN → LobbyScene` and `MAIN MENU → MainMenuScene` (D-202-01 = C),
   using the existing navigation mechanism (`this.scene.start(sceneKey)` with a
   `hasTransitioned`-style guard, as in `MainMenuScene.transitionTo`) and no new
   navigation abstraction (`ARCHITECTURE.md` §5, `AGENTS.md` §9).
2. Implementing exactly the approved interaction model (D-202-02 = A): explicit
   buttons as the primary navigation mechanism, with no full-screen tap,
   any-key, or automatic transition.
3. Recording, **before implementing it**, the architecture decision for the
   loadout state carrier that D-202-03 = D requires (`AGENTS.md` §18), and only
   then implementing preservation of the previous loadout on `PLAY AGAIN` while
   `LobbyScene` remains the editing surface for the next battle's loadout.
4. Implementing exactly the approved runtime cleanup (D-202-04 = A): clearing
   active battle state on leaving the completed battle, with no stale battle
   state reaching the next battle, and **without** disconnecting the transport
   (that is option C/D, which was not approved) and **without** clearing the
   preserved pre-battle loadout.
5. Preserving every existing behavior: the outcome and terminal-HP presentation
   (`SIGNALR_PROTOCOL.md` §3.2.19), the delivered-null handling
   (`DATABASE.md` §1 items 4–5), and the failed-read posture
   (`API_CONTRACTS.md` §4 notes 6–7).
6. Reaching any destination and any transport only through the `GameRuntime`
   port — a scene must not import `fetch`, `ApiService`, `@microsoft/signalr`, or
   `HubConnection` (`ARCHITECTURE.md` §2.2.1 rule 1, §2.2.3 rule 3).
7. Adding client tests for the approved flow, including the negative cases the
   decisions imply (no transition when the outcome is absent, no duplicate
   transition, no fabricated value on a failed read, no stale battle state on
   the next battle, loadout still editable after `PLAY AGAIN`).
8. Leaving backend, database, SignalR contracts, and Redis behavior unchanged —
   no approved decision requires a server-side change.

**Not** `TASK-203`'s responsibility: anything the Product Owner has not
approved. An implementation that adds an unapproved destination, control, or
cleanup behavior is out of bounds regardless of how reasonable it looks.

---

## Acceptance Criteria

TASK-202 is complete only when every criterion below holds.

- [x] All four decisions (D-202-01 … D-202-04) are recorded with their exact
      Product Owner answers.
- [x] Each decision retains its question, offered options, and evidence-backed
      consequences as historical record.
- [x] Product Owner approval status is unambiguous — the manifest and approval
      record read **APPROVED**, not PENDING.
- [x] No product choice beyond the four supplied answers has been introduced
      anywhere in this task or in `docs/`.
- [x] `docs/00-overview/GDD.md` §2 records the approved player-facing
      post-result flow.
- [x] `docs/02-technical/TDD.md` §2.1 records the approved lifecycle diagram, the
      `PLAY AGAIN` loadout-preservation + editability rule, the explicit-button
      interaction decision, and the clear-completed-battle-state rule.
- [x] `docs/02-technical/ARCHITECTURE.md` §2.2.3 records the loadout state-carrier
      architectural requirement/boundary only, with no mechanism chosen.
- [x] The lifecycle is no longer described as permanently terminating at
      `ResultScene`.
- [x] GDD, TDD, ARCHITECTURE and this task agree with each other.
- [x] The downstream implementation task exists, references TASK-202 as its
      decision authority, and is not marked DONE.
- [x] No production code changed (`src/`).
- [x] No tests changed (`tests/`).
- [x] No game rules changed.
- [x] No API, database, SignalR, or Redis change.
- [x] No unsupported technical decision introduced.

---

## Affected Files & Areas

```text
[x] tasks/backlog/TASK-202-post-result-lifecycle-product-decision-and-specification.md
        → moved to tasks/completed/ (this file)
[x] docs/00-overview/GDD.md             — §2 post-result player flow; version note
[x] docs/02-technical/TDD.md            — §2.1 approved lifecycle + rules;
                                          version note
[x] docs/02-technical/ARCHITECTURE.md   — §2.2.3 loadout state-carrier
                                          requirement/boundary; §2.2 transition
                                          reference; version note
[x] tasks/backlog/TASK-203-post-result-lifecycle-implementation.md
                                        — downstream implementation task CREATED
[ ] src/frontend/                       — NOT modified
[ ] src/backend/                        — NOT modified
[ ] tests/                              — NOT modified
[ ] docs/01-game-design/                — NOT modified (no game rule changed)
[ ] docs/00-overview/MVP_SCOPE.md       — NOT modified (no scope change)
[ ] docs/00-overview/ROADMAP.md         — NOT modified
[ ] docs/02-technical/API_CONTRACTS.md, SIGNALR_PROTOCOL.md, REDIS_STATE.md,
    GAME_STATE.md, DATABASE.md, GAME_EVENTS.md
                                        — NOT modified (no contract change)
```

---

## Testing Requirements

*(This decision-and-documentation task changes no behavior, so the template's
per-verification-type checklist is replaced by the document checks that actually
apply. `TASK-203` carries the unit, integration, and scenario requirements for
the approved flow.)*

No production test suite is required
(`AGENTS.md` §15 applies to gameplay behavior, which this task does not change).
**No test was created for implementation that has not happened yet.**

```text
[x] Task document conforms to tasks/TASK_TEMPLATE.md — every required section
    present (Metadata, Objective, Authoritative References, Scope, Current
    State, Acceptance Criteria, Affected Files & Areas, Implementation Notes,
    Testing Requirements, Stop Conditions, Completion Evidence); the
    decision-specific sections are additive, not replacements.
[x] Task lifecycle metadata is valid — Type/Status/Risk/Priority/Primary
    Agent/Workflow/Skills/Dependencies per TASK_TEMPLATE.md; Status is DONE
    per TASK_LIFECYCLE.md §1 (executed directly, deliverables verified).
[x] All four PO answers recorded exactly  — verbatim block + manifest + record
[x] GDD / TDD / ARCHITECTURE agree         — cross-read against TASK-202
[x] No production files changed            — verified by working-tree inspection
[x] No test files changed                  — verified by working-tree inspection
[x] No game rule / API / DB / SignalR / Redis contract changed
[x] Downstream task created and not DONE   — tasks/backlog/TASK-203-*.md
[x] Skill count within budget              — 4 skills (Simple/Normal budget,
                                             tasks/README.md §12)
```

No test command was run: this task changes no code and no test, so there is no
behavior to validate. Running the suite would not increase confidence in a
documentation deliverable, and `AGENTS.md` §16 forbids touching source to make
unrelated failures pass.

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 apply. Task-specific:

- If this task were asked to choose, assume, or default any of D-202-01 …
  D-202-04: **STOP** per `AGENTS.md` §7, §20, §23. *(Did not fire — the Product
  Owner supplied all four answers.)*
- If a change is requested to `ResultScene.ts`, any Phaser scene, `GameConfig.ts`,
  `GameRuntime.ts`, or any runtime contract: **STOP** — implementation belongs to
  `TASK-203`, not here. *(No such change was made.)*
- If a change to `docs/` is requested that is not required by an approved
  decision: **STOP** per `AGENTS.md` §17 — do not replace an authoritative rule
  with an assumption. *(Only the sections the approved decisions require were
  changed.)*
- If an approved decision requires a new REST endpoint, wire member, SignalR
  method/event, Redis key, or schema change without an approved ADR: **STOP** per
  `AGENTS.md` §18. *(None does.)*
- If `D-202-03 = A` or `D` and the concrete state carrier would have to be
  chosen to finish this task: **STOP** — record the requirement/boundary and
  leave the mechanism to the downstream architecture decision. *(Fired as a
  boundary rule; handled by recording `ARCHITECTURE.md` §2.2.3 without choosing a
  mechanism.)*
- If an approved flow would require an out-of-scope feature (stage progression,
  energy/stamina, campaign map, or anything in `MVP_SCOPE.md` §2/§3): **STOP**
  per `AGENTS.md` §8. *(None does.)*

---

## Completion Evidence

### Decision Record

```text
D-202-01 = C  APPROVED  PLAY AGAIN → LobbyScene; MAIN MENU → MainMenuScene
D-202-02 = A  APPROVED  Explicit UI buttons only as the primary mechanism
D-202-03 = D  APPROVED  Preserve previous loadout; still editable in LobbyScene
D-202-04 = A  APPROVED  Clear active battle state on leaving the battle

Decision source:  Product Owner (task input)
Recorded verbatim in this task's "PRODUCT OWNER APPROVED" section
Task status:      DONE
```

### Changed Files

- `tasks/completed/TASK-202-post-result-lifecycle-product-decision-and-specification.md`
  — this file. Moved from `tasks/backlog/`; the four `PENDING` states replaced
  with the exact approved answers; decision manifest and approval record marked
  **PRODUCT OWNER APPROVED**; rationale, options, and compatibility constraints
  preserved; PO decisions separated from downstream technical work; scope,
  boundary, acceptance criteria, stop conditions, and completion evidence
  updated.
- `docs/00-overview/GDD.md` — §2 Core Gameplay Loop now records the approved
  post-result player flow (`PLAY AGAIN` returns to battle preparation with the
  previous loadout preserved and still editable; `MAIN MENU` returns to the main
  menu), and states that the result screen is no longer the permanent end of the
  loop. Version note updated.
- `docs/02-technical/TDD.md` — §2.1 Phaser Scene Lifecycle now records the
  approved lifecycle with its `PLAY AGAIN → LobbyScene` and
  `MAIN MENU → MainMenuScene` continuations, the explicit-button interaction
  decision, the loadout preservation + editability rule, and the
  clear-completed-battle-state rule. The staging note distinguishes the approved
  design from what is currently implemented (`TASK-203`). Version note updated.
- `docs/02-technical/ARCHITECTURE.md` — §2.2.3 records the post-result loadout
  carrier as an architectural **requirement/boundary** (why one is needed, what
  it must and must not be, and that its concrete form is an open architecture
  decision), with no mechanism chosen; §2.2's transition reference defers the
  post-result continuations to `TDD.md` §2.1. Version note updated.
- `tasks/backlog/TASK-203-post-result-lifecycle-implementation.md` — the
  downstream implementation task, created with the next valid repository task ID
  and referencing TASK-202 as its decision authority.

### Validation Results

```text
Task document vs tasks/TASK_TEMPLATE.md    PASS — all required sections present
Lifecycle metadata validity                PASS — DONE per TASK_LIFECYCLE.md §1
Decision record exactness                  PASS — 4/4 answers verbatim, 0 PENDING
Manifest / approval-record status          PASS — PRODUCT OWNER APPROVED (4/4)
GDD ↔ TDD ↔ ARCHITECTURE ↔ TASK-202        PASS — consistent, no contradiction
ResultScene terminality removed in docs    PASS — GDD §2, TDD §2.1, ARCHITECTURE
src/ modification check                    PASS — 0 files changed
tests/ modification check                  PASS — 0 files changed
Game-rule modification check               PASS — docs/01-game-design/ untouched
Contract modification check                PASS — API/SignalR/Redis/DB untouched
Out-of-scope doc modification check        PASS — MVP_SCOPE.md, ROADMAP.md untouched
Downstream task creation check             PASS — TASK-203 created, Status BACKLOG
Skill budget check                         PASS — 4 skills (limit 7)
git diff --check                           PASS — no whitespace errors
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic introduced
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1, §2, §3)
- [x] Confirmed zero production code changed (`src/`)
- [x] Confirmed zero tests changed (`tests/`)
- [x] Confirmed no game rule changed (`docs/01-game-design/`)
- [x] Confirmed zero API, database, SignalR, or Redis contract change
- [x] Confirmed no unapproved product choice recorded as a decision
- [x] Confirmed no unsupported technical mechanism chosen for the loadout carrier
