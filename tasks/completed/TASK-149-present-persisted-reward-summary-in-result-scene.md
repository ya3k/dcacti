# TASK-149 — Present the Persisted `RewardSummary` in `ResultScene`

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and tasks/ by path and section.

  PROVENANCE: Identified during the TASK-148 task-generation pass (the
  repository task-discovery pass following TASK-143/144/145/146/147/148).

  TASK-148 closed the in-battle event-presentation gap. The outcome path,
  however, still stops one link short of its own documented contract:

      GET /api/battle/{battleId}/result  →  ApiService.getBattleResult  →  ???

  `BattleResultResponse.rewards` (`API_CONTRACTS.md` §4 note 1) is the one
  authoritative record of what a battle awarded, its member list is frozen by
  `DATABASE.md` §1 ("Reward semantics for `RewardSummary`"), the server
  persists and returns it, and the client already models it end-to-end
  (`ApiService.getBattleResult`, `RewardSummaryResponse`). Nothing renders it:
  `ResultSceneData` carries only `outcome`/`finalBossHp`/`finalPlayerHp`, so a
  finished battle still tells the player nothing about the XP and Levels it
  granted.

  TASK-087 recorded the deferral with two explicit grounds, and both are now
  discharged:

    1. "the `RewardSummary` member list is deferred (`API_CONTRACTS.md` §4
       note 1, `DATABASE.md` §1)" — no longer true. The member list is frozen
       and landed: `DATABASE.md` §1 items 3–4 fix the 8-member contract, the
       Player track by `COMBAT_RULES.md` §7 and the Pet track by `PET_RULES.md`
       §5.3–§5.5 (TASK-067).
    2. "no documented client trigger at battle end" for calling
       `GET /api/battle/{battleId}/result` — no longer true either. TASK-144
       made the runtime call that route as §7.3's documented fallback
       (`GameRuntime.handleBattleNotRecoverable`), and `ResultScene` is reached
       only from a terminal outcome event, which is the "battle has already
       ended" condition `API_CONTRACTS.md` §4 states for the endpoint.

  TASK-148's Out of Scope recorded the gap again as "a distinct gap, not owned
  here". That task was never created; this task is it.

  THIS IS A CLIENT PRESENTATION TASK ONLY.
  It adds no REST endpoint, no wire member, no SignalR method, no event, no
  gameplay rule, and no client authority. It reads an endpoint that is already
  implemented and already documented, and renders members that are already
  frozen. No file under src/backend/ is modified by this task.
-->

---

## Metadata

```text
Task ID:           TASK-149
Type:              FEATURE (TASK_TYPES.md §2 — "Implement a documented mechanic,
                   capability, or system that already has a home in docs/ but has
                   not yet been built"; workflow development/feature.md)
Status:            DONE (direct execution recorded: Changed Files + Validation
                   Results in Completion Evidence; all acceptance criteria
                   verified. Lifecycle: BACKLOG → IN PROGRESS → DONE per
                   TASK_LIFECYCLE.md §3.)
Risk:              LOW–MEDIUM (TASK_TYPES.md §4 — FEATURE baseline is MEDIUM; this
                   is a read-and-render change in one scene plus one runtime port
                   method. It crosses the GameRuntime port boundary but changes no
                   contract: it consumes an endpoint and a member list that are
                   already implemented and already frozen.)
Priority:          MEDIUM (completes the end-to-end outcome path of the one
                   playable battle the MVP is built around; no other task is
                   blocked by it)
Primary Agent:     client (TASK_TYPES.md §5 — Frontend/Phaser domain → Client)
Supporting Agents: testing, review
Workflow:          development/feature.md
Skills:            client/client-state-authority,
                   client/phaser-battle-presentation,
                   backend/api-contract-validation,
                   testing/test-scenario-generation,
                   quality/implementation-review
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-041 (DONE — BattleResult persistence, so a result row
                   exists to read), TASK-067 (DONE — the Pet-track members are
                   implemented, completing the 8-member RewardSummary),
                   TASK-076 (DONE — client Battle API service, including
                   ApiService.getBattleResult and RewardSummaryResponse),
                   TASK-087 (DONE — ResultScene and the outcome handoff this task
                   extends), TASK-144 (DONE — client reconnect/resync recovery,
                   which already reuses ApiService.getBattleResult as §7.3's
                   fallback)
```

---

## Objective

Read the persisted battle result through the `GameRuntime` port when `ResultScene` is reached, and render the delivered `rewards` members — the Player track and the Pet track of the `RewardSummary` contract — alongside the outcome and terminal HP values the scene shows today, presenting every member verbatim from the server's response and computing no gameplay value.

The scene currently receives only `outcome`, `finalBossHp`, and `finalPlayerHp` (`BattleScene.findOutcomeEvent`, `SIGNALR_PROTOCOL.md` §3.2.19) and renders exactly those. The reward summary is not part of the outcome event signal (`SIGNALR_PROTOCOL.md` §3.2.19 item 3: `reward summary` is omitted from the wire under the §3.2.25 convention) — it is delivered by the REST endpoint `GET /api/battle/{battleId}/result` (`API_CONTRACTS.md` §4), which is already implemented server-side and already wrapped client-side by `ApiService.getBattleResult`.

---

## Context

The two halves of this path already exist and are unconnected at the presentation end:

```text
src/frontend/client/src/services/api/BattleModels.ts
├── BattleResultResponse        (lines 456–478)  { battleId, outcome, rewards, durationTurns }
└── RewardSummaryResponse       (lines 544–564)  the 8-member contract, all required

src/frontend/client/src/services/api/ApiService.ts
└── getBattleResult(battleId)   (lines 232–234)  GET /api/battle/{battleId}/result

src/frontend/client/src/game/runtime/GameRuntime.ts
└── handleBattleNotRecoverable  (line 684)       already calls this.api.getBattleResult
                                                 — proving the port→transport path is
                                                 the established one, but that call
                                                 discards the response

src/frontend/client/src/game/scenes/ResultScene.ts
├── ResultSceneData             (lines 11–15)    { outcome, finalBossHp, finalPlayerHp }
└── renderResult()              (lines 112–140)  renders only those three
```

**Why it is a runtime-port change and not a direct `ApiService` call.** `ARCHITECTURE.md` §2.2.1 rule 1 forbids a Phaser scene from importing `fetch`, `ApiService`, or any other HTTP/REST client: the only sanctioned route from a scene to a transport is the `GameRuntime` port, and the port's pre-battle collection reads (`getPets`/`getPet`/`getCards`/`getRelics`, `GameRuntime.ts` lines 699–749) are the exact precedent — each is a pass-through delegation that adds no orchestration and holds no state. §2.2.3 governs the pre-battle *selection* boundary and is not this path; the applicable rule is §2.2.1 rule 1.

**Why the server half needs no work.** The endpoint is implemented (TASK-041), `rewards` is always present for both outcomes (`API_CONTRACTS.md` §4 note 1, `DATABASE.md` §1 item 5), and the 8-member member list is frozen (`DATABASE.md` §1 items 3–4). The `RewardSummary` Pet-track member list — the one part once deferred to "the implementation task" — landed with TASK-067, so nothing about this contract is still open.

**Why not the wire.** `SIGNALR_PROTOCOL.md` §3.2.19 item 3 fixes the outcome event's member set to exactly four and disposes of `reward summary` by the §3.2.25 omission convention. Rendering the reward summary is therefore *not* a reason to add a wire member, and this task adds none.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Player ("Player XP / Level — persistent account progression") and Pets ("Pet XP / Pet Level") are IN scope
- `docs/02-technical/API_CONTRACTS.md` **§4** — `GET /api/battle/{battleId}/result` response shape
- `docs/02-technical/API_CONTRACTS.md` **§4 note 1** — `rewards` is always present for both outcomes and its value is `BattleResult.RewardSummary`; the Player track is `COMBAT_RULES.md` §7, the Pet track is `PET_RULES.md` §5.3–§5.5
- `docs/02-technical/API_CONTRACTS.md` **§4 notes 6–7** — the endpoint requires an authenticated session (`401 UNAUTHENTICATED`) and returns `404 BATTLE_NOT_FOUND` for a battle that does not exist or is not the caller's
- `docs/02-technical/API_CONTRACTS.md` **§4** — "Only returns data for a battle that has already ended"
- `docs/02-technical/DATABASE.md` **§1, "Reward semantics for `RewardSummary`"** — the canonical 8-member member list (items 1–2), "No placeholder members" (item 3), "The 8-member projection is the contract in force" (item 4), and the both-outcomes semantics (item 5)
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3.2.19 item 3** — `reward summary` is omitted from the `BattleWon`/`BattleLost` wire item under the §3.2.25 convention; the outcome event's member set is exactly four
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§7.3** — the `404 BATTLE_NOT_FOUND` fallback semantics the runtime already implements
- `docs/02-technical/GAME_STATE.md` **§4** — Client Presentation State: not authoritative, owned entirely by the client
- `docs/02-technical/ARCHITECTURE.md` **§2.2.1 rule 1** — a scene depends on the runtime port, never on the transport (this rule covers `fetch`/`ApiService`, not SignalR alone)
- `docs/02-technical/ARCHITECTURE.md` **§2.2.1 rule 3** — "The runtime coordinates; it does not compute"
- `docs/02-technical/ARCHITECTURE.md` **§5** — anti-overengineering
- `docs/01-game-design/COMBAT_RULES.md` **§7** — the Player XP reward the Player-track members report
- `docs/01-game-design/PET_RULES.md` **§5.3–§5.5** — the Pet XP reward the Pet-track members report
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — server authority
- `docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md` — presentation ownership

---

## Current State

Verified against the working tree at the time of creation:

```text
src/frontend/client/src/game/scenes/ResultScene.ts
  - lines 11–15   ResultSceneData: outcome, finalBossHp, finalPlayerHp
  - lines 55–64   shutdown(): clears resultData and the three text objects
  - lines 66–110  drawShell(): outcome / boss HP / player HP text objects
  - lines 112–140 renderResult(): renders exactly the three delivered members
  => no reward member is read, held, or rendered.

src/frontend/client/src/game/scenes/BattleScene.ts
  - lines 844–854 handleBattleEvents(): finds the outcome event and calls
                  this.scene.start('ResultScene', outcome) with only the
                  three outcome members
  - lines 1022–1051 findOutcomeEvent(): reads type/outcome/finalBossHp/
                  finalPlayerHp and nothing else

src/frontend/client/src/game/runtime/GameRuntime.ts
  - lines 699–749 pre-battle collection reads: the established pass-through
                  port pattern this task follows
  - line  684     this.api.getBattleResult(battleId) — already called on the
                  §7.3 expiry fallback; its response is discarded
  => no port method exists that returns a battle result to presentation.

src/frontend/client/src/services/api/ApiService.ts
  - lines 232–234 getBattleResult(battleId) → BattleResultResponse
  => the transport half is complete; only the presentation half is missing.
```

Client test suites that currently assert this area live in
`src/frontend/client/tests/ResultScene.test.ts` and
`src/frontend/client/tests/BattleService.test.ts`.

---

## Dependencies

- **TASK-041** (DONE) — implemented `BattleResult` persistence, so a terminal
  battle has a result row for `GET /api/battle/{battleId}/result` to return.
- **TASK-067** (DONE) — landed the Pet-track members, completing the 8-member
  `RewardSummary` contract. Before it, the Pet member list was the one part
  `DATABASE.md` §1 deferred to "the implementation task".
- **TASK-076** (DONE) — implemented the client Battle API service, including
  `ApiService.getBattleResult` and the `RewardSummaryResponse` type this task
  renders.
- **TASK-087** (DONE) — implemented `ResultScene` and the outcome handoff this
  task extends, and deferred reward-summary presentation to a separate task.
- **TASK-144** (DONE) — client reconnect/resync recovery, which already reuses
  `ApiService.getBattleResult` as §7.3's fallback, establishing the
  runtime-port-to-result-route precedent.

No dependency is unresolved. No other task depends on this one.

---

## Scope

### In Scope

- Add exactly one pass-through read to the `GameRuntime` port that returns the
  persisted battle result for a `battleId` — delegating to the existing
  `ApiService.getBattleResult` and adding no orchestration, no caching, and no
  held state, matching the existing collection-read precedent
  (`ARCHITECTURE.md` §2.2.1 rule 3).
- Have `ResultScene` obtain the result through that port when the scene is
  reached, and render the delivered `rewards` members: the four Player-track
  members and the four Pet-track members of `DATABASE.md` §1's contract.
- Present every member verbatim from the response — including `0` grants on a
  defeat and `null` "no row" values.
- Present the Player and Pet tracks distinguishably so a reader can tell the
  account progression from the Pet progression.
- Handle the endpoint's documented non-success outcomes without fabricating a
  reward: `404 BATTLE_NOT_FOUND` and `401 UNAUTHENTICATED`
  (`API_CONTRACTS.md` §4 notes 6–7) leave the outcome and terminal HP
  presentation intact and add no invented numbers.
- Keep the existing outcome and terminal-HP presentation working unchanged when
  the result read fails, is unavailable, or has not resolved yet.
- Extend `src/frontend/client/tests/ResultScene.test.ts` with rendering
  scenarios for both tracks, both outcomes, the `null` members, and the
  failed-read path.
- Correct any in-file comment in the touched files that the change makes stale.

### Out of Scope

- **Any change under `src/backend/`.** `GET /api/battle/{battleId}/result` and
  its 8-member `rewards` object are implemented and frozen. If a
  documentation-supported server defect is found, STOP per Stop Conditions
  rather than expanding this task.
- **Any new REST endpoint, response member, or `rewards` member.** The member
  list is owned and fixed by `DATABASE.md` §1 items 3–4; this task renders what
  the contract already carries.
- **Any change to `SIGNALR_PROTOCOL.md`'s wire schema, and any new SignalR
  method, event, subscription, or member.** In particular, no reward member is
  added to `BattleWon`/`BattleLost`: §3.2.19 item 3 disposes of `reward summary`
  by the §3.2.25 omission convention.
- **Any reward, XP, Level, or progression computation on the client**
  (`GAME_RULES.md` §18, `AGENTS.md` §10, ADR-001). No XP is summed, no Level is
  derived from XP, no `playerLeveledUp`/`petLeveledUp` flag is recomputed, and
  no `null` is converted to a `0`.
- **Any use of `rngSeed`/`rngState`, or any client-side randomness.** All values
  are delivered (`SIGNALR_PROTOCOL.md` §4.1 item 2, `AGENTS.md` §11).
- **Navigation, retry, or "play again" behavior after `ResultScene`** — the
  scene's lifecycle ends there (`TDD.md` §2.1); adding navigation is a separate
  decision this task does not make.
- **Any change to `BattleScene`'s outcome detection** (`findOutcomeEvent`). The
  reward summary is not part of that signal; the scene still hands off the same
  three outcome members.
- **Any change to the reconnect/resync path** — TASK-144's §7.3 fallback is
  complete and this task does not alter it.
- **Any change to `BattleResultResponse` or `RewardSummaryResponse`.** They
  already match the authoritative shape (TASK-076).
- **Animation, audio, asset, or layout-system work beyond rendering the
  delivered members as text** — scene-local presentation is the smallest
  correct change (`AGENTS.md` §9).
- **Any change to `docs/`.** No authoritative contract is amended by this task.
- **Any modification to a completed task file** (`TASK_LIFECYCLE.md` §3).
- **Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.**

---

## Acceptance Criteria

- [x] The `GameRuntime` port exposes exactly one new read that returns the persisted battle result for a supplied `battleId`, delegating to `ApiService.getBattleResult` and adding no orchestration, caching, or held state (`ARCHITECTURE.md` §2.2.1 rule 3)
- [x] `ResultScene` obtains the result through that port and imports neither `fetch` nor `ApiService` nor any other HTTP/REST client (`ARCHITECTURE.md` §2.2.1 rule 1)
- [x] All four Player-track members (`playerXpGained`, `newPlayerXp`, `playerLeveledUp`, `newPlayerLevel`) are rendered from the delivered values (`DATABASE.md` §1 item 1)
- [x] All four Pet-track members (`petXpGained`, `newPetXp`, `petLeveledUp`, `newPetLevel`) are rendered from the delivered values (`DATABASE.md` §1 item 2)
- [x] The Player track and the Pet track are presented distinguishably, so a reader can tell which progression each value belongs to
- [x] Each member is presented verbatim: `playerXpGained = 0` and `petXpGained = 0` on a defeat render as `0`, never omitted and never substituted (`DATABASE.md` §1 item 5)
- [x] A `null` `newPlayerXp`/`newPlayerLevel`/`newPetXp`/`newPetLevel` is presented as the delivered "no row" state and is never converted to a number (`DATABASE.md` §1 item 4, `BattleModels.ts` `RewardSummaryResponse`)
- [x] `playerLeveledUp`/`petLeveledUp` are rendered from the delivered booleans and are never re-derived by comparing the XP or Level members
- [x] The scene computes no gameplay value: no XP summation, no Level derivation, no reward arithmetic, and no RNG (`GAME_RULES.md` §18, `ADR-001`)
- [x] A `404 BATTLE_NOT_FOUND` or `401 UNAUTHENTICATED` result read (`API_CONTRACTS.md` §4 notes 6–7) leaves the outcome and terminal-HP presentation intact and renders no invented reward value
- [x] A failed or still-pending result read never blocks, breaks, or removes the existing outcome and terminal-HP presentation
- [x] The outcome, terminal Boss HP, and terminal Pet HP presentation is unchanged for a delivered outcome (`SIGNALR_PROTOCOL.md` §3.2.19)
- [x] No wire member, SignalR method, event, subscription, or discriminator value is added, and no reward member is added to `BattleWon`/`BattleLost` (`SIGNALR_PROTOCOL.md` §3.2.19 item 3, §3.2.25)
- [x] No REST endpoint or response member is added or changed (`API_CONTRACTS.md` §4)
- [x] Any in-file comment made stale by the change is corrected, and no comment claims a behavior the module does not implement
- [x] Zero files under `src/backend/` are modified
- [x] Zero files under `docs/` are modified
- [x] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [x] Quality review checklist passes (`quality/review.md` §1)
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api) — NONE (Out of Scope)
[x] src/frontend/client/ (src/game/runtime/GameRuntime.ts — new pass-through read;
                          src/game/runtime/GameRuntimeEvents.ts — the port capability;
                          src/game/scenes/ResultScene.ts — renders the delivered rewards;
                          src/game/scenes/BattleScene.ts — carried battleId into the
                          ResultScene handoff, and only the handoff)
[x] tests/ (src/frontend/client/tests/ResultScene.test.ts — reward + port suites;
            RuntimeBoundaries.test.ts — port capability inventory;
            BattleEventPresentation.test.ts, SceneLifecycle.test.ts — handoff battleId)
[ ] docs/ (documentation updates if applicable) — NONE (Out of Scope)
```

---

## Implementation Notes

- Follow the existing port convention exactly. The pre-battle collection reads in `GameRuntime.ts` (lines 699–749) are the template: a one-line delegation to `ApiService`, no caching, no state, no ordering imposed. Do not add a second transport path and do not have the scene reach `ApiService` (`ARCHITECTURE.md` §2.2.1 rule 1).
- Read the member set from `DATABASE.md` §1 and the response shape from `API_CONTRACTS.md` §4 rather than from this task file — do not add a member the contract does not have, and do not omit one it does. `RewardSummaryResponse` in `BattleModels.ts` (lines 544–564) is the client type that already matches; reuse it, do not re-declare it.
- The endpoint returns a result only for a battle that has already ended (`API_CONTRACTS.md` §4), and `ResultScene` is reached only from a terminal outcome event — so the read is well-formed by construction. The interest is in the failure and latency cases, not in a "not yet ended" case.
- `ResultScene` is a Phaser scene: confirm whether it can await a promise safely in its lifecycle, and if it cannot, hold the read result in scene-local presentation state and render on resolution — the same "presentation-local state only" posture `BattleScene` uses for its selection (`GAME_STATE.md` §4). Do not adopt a new state mechanism.
- A `null` XP/Level member is the documented "this track had no row" value (`DATABASE.md` §1 item 4). Present it as such; do not print `0`, do not print nothing as if the member were absent, and do not hide the row.
- `playerLeveledUp`/`petLeveledUp` are delivered booleans. Rendering "Level Up" by comparing `newPlayerLevel` to anything is a client-side derivation of a server-authored value — do not do it.
- Carry `battleId` into the `ResultScene` handoff if the scene does not already have it. `BattleScene.handleBattleEvents` currently passes only the three outcome members (lines 849–853), and the batch's `battleId` comes from the `BattleEventsEnvelope` the runtime forwards (`GameRuntimeEvents.ts`). Add the id to the handoff data only — do not change `findOutcomeEvent`'s member set.
- Do not add navigation, a retry button, or a "play again" path. `TDD.md` §2.1 ends the client lifecycle at `ResultScene`, and adding navigation is a product decision this task has no authority for (`AGENTS.md` §7).
- The scene's `shutdown()` clears scene-local references (lines 55–64). Any new held value must be cleared there too, matching the existing cleanup convention.
- Keep the presentation honest about absence: if the reward read fails, the honest render is "the outcome is known and the reward summary is unavailable", not a zeroed reward table.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — the port read delegates to ApiService.getBattleResult
                         with the supplied battleId and returns its response
                         unchanged; the scene's formatting of a delivered
                         RewardSummaryResponse
[x] Integration tests  — a terminal outcome event reaching the scene leads to a
                         result read on the port, and the rendered members match
                         the delivered response
[x] Gameplay scenarios — See "Key Edge Cases" below (Given/When/Then)
```

### Key Edge Cases

- **A victory result** (`playerXpGained: 100, petXpGained: 100`, both "leveled up" flags derivable only as delivered) renders all eight members from the response (`DATABASE.md` §1 item 5).
- **A defeat result** renders `playerXpGained: 0` and `petXpGained: 0` as `0` — the members are always present and zero is a real value, never an omission (`DATABASE.md` §1 item 5, `API_CONTRACTS.md` §4 note 1).
- **A `null` `newPlayerXp`** is presented as the delivered "no row" state and is not converted to `0` (`DATABASE.md` §1 item 4).
- **A `null` `newPetXp`** is handled identically, independently of the Player track.
- **`playerLeveledUp: true` with `newPlayerLevel` equal to a value the client could not have predicted** renders the delivered level verbatim — the scene derives no level from XP.
- **`playerLeveledUp: false`** renders a non-level-up presentation, and the scene does not infer it by comparing XP values.
- **The result read returns `404 BATTLE_NOT_FOUND`** — the outcome and terminal HP values still render, and no reward value is invented (`API_CONTRACTS.md` §4 notes 6–7, `SIGNALR_PROTOCOL.md` §7.3).
- **The result read rejects with `401 UNAUTHENTICATED`** — handled the same way, with no fabricated reward.
- **The result read has not resolved when the scene first renders** — the outcome and terminal HP values appear immediately and the reward summary does not block them.
- **No result read is attempted at all** (e.g. the scene is reached without a battleId) — the outcome presentation is unaffected and the scene fabricates nothing.
- **No outcome-member regression**: `outcome`, `finalBossHp`, and `finalPlayerHp` render exactly as before for both `"victory"` and `"defeat"` (`SIGNALR_PROTOCOL.md` §3.2.19).
- **No wire regression**: the delivered `BattleWon`/`BattleLost` item is still exactly `type`, `outcome`, `finalBossHp`, `finalPlayerHp` and carries no reward member (`SIGNALR_PROTOCOL.md` §3.2.19 item 3).

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 apply.

- If `DATABASE.md` §1's `RewardSummary` member list and `API_CONTRACTS.md` §4 note 1's `rewards` shape are found to disagree, or either disagrees with the server's actual response: STOP per `AGENTS.md` §4 — report the conflict, do not pick a side.
- If `RewardSummaryResponse` in `BattleModels.ts` is found to disagree with `DATABASE.md` §1's 8-member contract: STOP and report — reconciling a client type against its owning contract is a distinct decision, not a silent edit inside this task.
- If rendering a reward member appears to require a client-side gameplay computation (XP summation, Level derivation, a `null`-to-`0` substitution, or re-deriving a `leveledUp` flag): STOP per `AGENTS.md` §10.
- If the work appears to require a new REST endpoint, response member, wire member, SignalR method, or event: STOP — `API_CONTRACTS.md` §4, `DATABASE.md` §1, and `SIGNALR_PROTOCOL.md` §3.2.19/§3.2.25 fix those surfaces.
- If `ResultScene` cannot perform the read through the `GameRuntime` port without a scene importing the transport directly: STOP and report — `ARCHITECTURE.md` §2.2.1 rule 1 forbids the shortcut, and widening the port is an architecture question, not an implementation detail.
- If completing this appears to require navigation, retry, or a post-result flow: STOP — `TDD.md` §2.1 ends the client lifecycle at `ResultScene`, and the product decision is not this task's.
- If the change requires a presentation-mechanism redesign rather than an extension of the existing path: STOP and decompose (`tasks/README.md` §13).
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Changed Files
- `src/frontend/client/src/game/runtime/GameRuntimeEvents.ts` — added `getBattleResult(battleId)` to the `GameRuntimePort` interface as a documented capability (delegating read of `API_CONTRACTS.md` §4); extended the existing type-only `BattleModels` import with `BattleResultResponse`. No model type is declared or re-exported.
- `src/frontend/client/src/game/runtime/GameRuntime.ts` — implemented `getBattleResult(battleId)` as a one-line pass-through to the existing `ApiService.getBattleResult`; no orchestration, no caching, no held state, no computation. Extended the existing type-only `ApiService` import with `BattleResultResponse`.
- `src/frontend/client/src/game/scenes/ResultScene.ts` — added an optional `battleId` to `ResultSceneData`; added a `rewardText` area and `loadRewards()`, which reads the persisted result through the `readRuntime` port and renders the delivered `rewards` via `formatRewards`/`formatDelivered`/`formatLeveledUp`; cleared the new reference in `shutdown()`. The outcome and terminal-HP rendering path is byte-identical.
- `src/frontend/client/src/game/scenes/BattleScene.ts` — the outcome handoff now also carries the batch's `battleId` (`{ ...outcome, battleId: envelope.battleId }`). `findOutcomeEvent`'s member set and all outcome detection are unchanged.
- `src/frontend/client/tests/ResultScene.test.ts` — added a reward-presentation suite (12 tests: port read by `battleId`; all four Player-track and all four Pet-track members; both tracks distinguishable; defeat `0` grants rendered as `0`; `null` members rendered as unavailable and never as `0`; delivered `leveledUp` flags not re-derived; rejected read leaves the outcome intact and renders no reward; no request without a `battleId`; no request without a runtime; outcome rendered before the read resolves; shutdown cleanup) and a `GameRuntime` port suite (2 tests: delegation with the supplied id and unchanged response; rejection propagated without fabrication). Advanced the superseded forbidden-token assertion (see below).
- `src/frontend/client/tests/RuntimeBoundaries.test.ts` — added `getBattleResult` to the port capability inventory and updated the pinned type-only import assertion. The boundary assertions themselves (no model declared, no re-export, no transport in a scene) are unchanged and still pass.
- `src/frontend/client/tests/BattleEventPresentation.test.ts` — the two outcome-preservation assertions now expect the handoff `battleId`.
- `src/frontend/client/tests/SceneLifecycle.test.ts` — the two outcome-handoff assertions now expect the handoff `battleId`.
- `tasks/completed/TASK-149-present-persisted-reward-summary-in-result-scene.md` — this task file (moved from `backlog/`).

### Superseded Test Assertions
Four tests encoded the pre-TASK-149 state and were advanced rather than deleted, following the TASK-144 precedent (`completed tasks are immutable, TASK_LIFECYCLE.md §3`; the *assertion* is superseded by the current authoritative contract, not the completed task):

1. `ResultScene.test.ts` — "contains no client-side outcome derivation or reward logic" listed `RewardSummary`, `/api/battle/`, and `getBattleResult` as forbidden in `ResultScene.ts`. TASK-149 implements exactly that rendering, so the list was narrowed to the transport implementations (`ApiService`, `services/api/ApiService`, `services/realtime`, `@microsoft/signalr`, `HubConnection`) plus the HP-derivation tokens, and now additionally asserts the scene reaches the route through `readRuntime` and never via `fetch(`. Every property that still holds is preserved, and `RuntimeBoundaries.test.ts` independently enforces the scene/transport boundary for every registered scene.
2. `RuntimeBoundaries.test.ts` — the port capability inventory gained `getBattleResult`.
3. `BattleEventPresentation.test.ts` (×2) and `SceneLifecycle.test.ts` (×2) — the handoff assertions gained the `battleId` member the outcome handoff now carries.

### Validation Results
- `npx vitest run tests/ResultScene.test.ts` — PASS (30 tests)
- `npm run test:run` — PASS (520 tests, 18 files)
- `npx tsc --noEmit -p tsconfig.json` — PASS (exit 0, zero diagnostics)
- `npm run build` — PASS (exit 0; 138 modules transformed, `dist/assets/index-Ddw5wEvT.js` 2,084.84 kB). The stderr `/*#__PURE__*/` annotation notices and the >500 kB chunk-size warning originate from the third-party `@microsoft/signalr` bundle and pre-date this task; this change adds no dependency.
- Mutation check (verification that the `null` assertion is not vacuous): temporarily changing `formatDelivered` to return `'0'` for `null` made the "null resulting value" test fail (`expected ... not to contain 'XP 0'`); the implementation was then restored and the suite re-run green.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic — the scene performs no XP summation, no Level derivation, no reward arithmetic, no `leveledUp` re-derivation, no `null`-to-`0` substitution, and no RNG. Every rendered member is the delivered value.
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1 — Player XP/Level and Pet XP/Level are IN)
- [x] Confirmed zero files under `src/backend/` modified
- [x] Confirmed zero files under `docs/` modified
- [x] Confirmed zero completed task files modified
- [x] Confirmed no REST endpoint, response member, SignalR method, event, subscription, or discriminator value added — no reward member is added to `BattleWon`/`BattleLost` (`SIGNALR_PROTOCOL.md` §3.2.19 item 3)
- [x] Confirmed no second API request, no second result trigger, and no change to the reconnect fallback — `getBattleResult` is a second caller of the same `GET /api/battle/{battleId}/result` route TASK-144's §7.3 fallback already uses
- [x] Confirmed `ResultScene` imports no transport implementation, and the port declares no model type and re-exports nothing (`ARCHITECTURE.md` §2.2.1 rule 1, `RuntimeBoundaries.test.ts`)
