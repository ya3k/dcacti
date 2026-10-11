# TASK-253 — Developer Move Log for Battle Presentation

<!--
  GEN-TASK EXECUTION MANIFEST TEMPLATE
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
-->

---

## Metadata

```text
Task ID:           TASK-253
Type:              FEATURE
Status:            DONE
Risk:              MEDIUM
Priority:          MEDIUM
Primary Agent:     client
Supporting Agents: testing, review
Workflow:          development/feature.md
Skills:            client/phaser-battle-presentation, client/client-event-projection,
                   client/react-phaser-boundary, testing/test-scenario-generation,
                   quality/implementation-review
Dependencies:      TASK-252 (must be DONE), TASK-247, TASK-246, TASK-245, TASK-209, TASK-210
Declared Files:    src/frontend/client/src/game/scenes/BattleScene.ts,
                   src/frontend/client/src/game/scenes/BattleEventPresenter.ts,
                   src/frontend/client/scripts/standalone-web-smoke.mjs,
                   src/frontend/client/tests/SceneLifecycle.test.ts,
                   src/frontend/client/tests/BattleEventPresentation.test.ts
```

---

## Objective

Add one development-only Move Log panel to the battle view, drawn as a Phaser panel in the free band to the **right of the 8×8 board**, showing the actual runtime sequence of a battle — turn/sequence, actor, the player's committed action, match and cascade detail, resource and damage outcomes, Boss skill/passive events, animation lifecycle, and the final result and phase handoff — with bounded retention, auto-scroll, and reset on battle lifecycle. It renders only when `import.meta.env.DEV` is true, and every line it shows is derived from a delivered wire value or from the client's own presentation lifecycle.

---

## Operator Decisions Applied (approved, recorded)

Recorded per the operator decision that authorized this task. They are task constraints. **No authoritative document, ADR, or completed task record is modified by this task**; the supersession below is bounded to the single surface this task declares.

```text
D-1  DEV-only Move Log — APPROVED, with a bounded supersede.

     One developer diagnostic surface is authorized inside the battle view,
     positioned to the right of the board, rendered ONLY when
     `import.meta.env.DEV` is true.

     Bounded supersede — this decision supersedes exactly, and only for the
     Move Log declared in THIS task:
       - `tasks/completed/TASK-210-battle-feedback-and-presentation-pass.md` §9
       - TASK-246's scope note forbidding the rendering of the turn/sequence
         counter and the raw event log

     It does NOT authorize rendering transport state, SignalR status,
     BattleId, RNG seed or state, or any other diagnostic into the battle HUD.

     Harness exception — the operator authorizes editing exactly these two
     assertions in `src/frontend/client/scripts/standalone-web-smoke.mjs`:
       - `phase5b.noDeveloperDiagnosticsInTheHud`     (current: :2391–2395)
       - `phase5b.rawEventLogIsNotRendered`          (current: :2396–2400)
     They are narrowed to exclude exactly the Move Log's own declared text
     objects, and two STRONGER replacement assertions are added:
       1. with DEV=false the Move Log surface does not exist;
       2. non-log HUD text carries no forbidden transport/RNG diagnostic token.
     No assertion may be deleted merely to make the suite pass
     (`.ai/workflow/quality/local-e2e-verification.md` §13), and the
     relationship between this decision, this manifest and the harness change
     must be recorded in the completion evidence.

D-3  Surface — APPROVED: a Phaser in-scene panel in the free band to the
     right of the board. A React overlay is rejected because the position must
     track the canvas layout and Phaser's FIT scaling
     (`GameConfig.ts:65–70`; `ARCHITECTURE.md` §2.2.2).
```

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Match-3 (8×8 board, Cascade, Combo) is IN; §4 items 2–3 — anything absent from §1/§2 is FUTURE by default, and the ambiguity must be **reported**, not assumed IN
- `docs/01-game-design/GAME_RULES.md` §2 item 1 — "A Turn represents one player Swap/Action"
- `docs/01-game-design/GAME_RULES.md` §17 and its step-18/step-19a expansions — the canonical resolution order the log reports
- `docs/01-game-design/MATCH3_RULES.md` §2.1.1 — the two-cell Swap request (the log's swap coordinates); §4.2 — cascade depth
- `docs/02-technical/GAME_EVENTS.md` §1 and §1.1 — the ordering authority for one resolution
- `docs/02-technical/GAME_STATE.md` §2.0.1 — the meaning of `Turn`; §5 — `Sequence`; §5.3 item 4 — a snapshot carries no cascade history
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3 — the `ReceiveEvents(battleId, serverSequence, events[])` envelope (the log's turn/sequence source); §3.1 items 1–2 — write-back before batch, no per-step delivery; §3.2.2 — the closed 16-name `type` set (the only event names the log may name); §3.2.6–§3.2.24 — the per-event members the log may quote
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4 — `BattleStateUpdated` (`turn`, `sequence`); §4 item 4/11 and §8 items 3, 5–9 — the prohibitions this task does not widen
- `docs/02-technical/ARCHITECTURE.md` §2.2.2 — the viewport/scaling contract (the panel is logical-space geometry); §5 — anti-overengineering
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — every logged value is server-provided
- `docs/03-decisions/ADR/ADR-008-battle-recovery.md` — recovery is a snapshot, never an event replay
- `tasks/completed/TASK-252-*.md` — the actor/action phase model and orchestration state machine this log reads (**must be DONE first**)
- `tasks/completed/TASK-245-*.md` (PD-1…PD-8) and `tasks/completed/TASK-246-*.md` — the timeline and pacing the log reports; TASK-246 I-2 records the `client/*` skill/contract drift
- `tasks/completed/TASK-210-*.md` §9 and `tasks/completed/TASK-209-*.md` — the diagnostic boundary this task supersedes **only** as bounded by D-1
- `tasks/completed/TASK-247-align-web-smoke-e2e-harness-with-sequential-battle-presentation.md` — the harness conventions the assertion change must follow
- `.ai/workflow/quality/local-e2e-verification.md` §3, §5, §6, §8.2, §9, §12.1–§12.3, §13 — the required stack, commands, capture, the "not run vs passing" rule, and the prohibition on editing a harness to go green
- `.ai/skills/client/phaser-battle-presentation/SKILL.md` — sequencing and input-lock policy **only**; its illustrative HUD (`Turn: … | Boss Intent: … | Status: …`) and its component names do not exist in the contract and are NOT followed (`AGENTS.md` §19)
- `tasks/TASK_LIFECYCLE.md` §6 — two-stage commit protocol

---

## Scope

### In Scope

- **One Phaser panel** in the free band to the right of the board, inside the 24 px safe area, overlapping neither the board nor any existing HUD line. Geometry is logical-space (`GameViewport.ts`): board spans x `395…885`, y `120…610` (`BattleScene.ts:62–66`), so the band is approximately x `897…1256`, y `120…610` — currently unused by every HUD line and clear of the cast controls, which start at `BOARD_ORIGIN_X` (`:2025`) below the board.
- **Dev-only rendering** following the existing project convention (`App.tsx:203–204`, the `StatusOverlay` / `ViewportDebugOverlay` precedent cited to TASK-209 §6): the surface is created only under `import.meta.env.DEV`, and with the flag false it does not exist at all.
- **Rows sourced only from delivered values or the client's own lifecycle**, covering where available:
  - sequence (`ReceiveEvents.serverSequence`) and `turn` (`BattleStateUpdated`);
  - actor and action identity (from the TASK-252 actor/action phase model);
  - the player's committed Swap coordinates or the cast/skill/relic action identifier;
  - match type and match results (`MatchCreated.shape`, `gemType`, `cells`, `createdSpecialGems`; `GemMatched.cellIndex`/`gemType`);
  - cascade index and resolution events (`CascadeCreated.cascadeDepth`; `ComboChanged.combo`);
  - damage/healing and resource changes (`DamageDealt`/`DamageTaken.amount` with `source`/`target`; `PowerChanged.delta`/`power`/`source`);
  - Boss skill and passive events and their outcomes (`BossSkillCast.skillId`/`sourceId`; `PassiveCharged`/`PassiveTriggered` with `progress`/`threshold`);
  - animation/event lifecycle: queued, animating, completed, failed — from the TASK-252 orchestration state machine;
  - the final result and phase handoff (`BattleWon`/`BattleLost` outcome before the `ResultScene` handoff).
- **Bounded retention** with a documented cap (a ring buffer), **auto-scroll** to the newest row, and **reset** on `create()`, on `shutdown()`, and on a `battleId` change.
- **Harness change per D-1**: narrow `phase5b.noDeveloperDiagnosticsInTheHud` and `phase5b.rawEventLogIsNotRendered` to exclude exactly the Move Log's own declared text objects, and add the two stronger replacement assertions (DEV-off absence; no forbidden token in non-log HUD text).
- Unit tests for row construction, cap/eviction, reset and DEV gating; scene tests for placement, ordering and lifecycle rows; browser E2E for the narrowed and replacement assertions.
- Follow the two-stage commit protocol (`tasks/TASK_LIFECYCLE.md` §6).

### Out of Scope

- **Any change to TASK-252's orchestration model.** This task consumes it; if it needs a change, that is TASK-252 rework.
- **Widening D-1 beyond the one surface.** No transport state, SignalR status, BattleId, RNG seed/state or other diagnostic may be rendered into the battle HUD, and no second DEV surface is added.
- **Deleting or weakening any assertion.** Narrowing is authorized only as D-1 records, and the replacements must be strictly stronger.
- **Hardcoded sample battle events, placeholder rows, or any inferred outcome.** No row may exist for an event the server did not send, and no gameplay meaning may be inferred from a rendered value.
- Any wire change: no new/renamed hub method, event type, state-push member, subscription or `type` discriminator (`SIGNALR_PROTOCOL.md` §4 item 4/11, §8 items 3, 5–9).
- Any client-side computation of match detection, gravity destinations, spawned Gem identity, RNG, damage, HP, Power, Combo or Passive progression (`AGENTS.md` §10).
- A React overlay implementation (rejected by D-3), and any change to `App.tsx` / `App.css`.
- Backend, Domain, Application, Infrastructure, Api, `docs/` or ADR changes.
- Editing `tasks/completed/*` or `tasks/blocked/*` (`AGENTS.md` §16/§17/§18).
- Items listed OUT in `docs/00-overview/MVP_SCOPE.md` §2, and unrelated refactoring.

---

## Current State

The battle view has a **diagnostics-only, unrendered** developer record and no rendered diagnostic surface. `BattleScene.presentedEventsLog` (`:748`, appended at `:2773`) and `presentedPhases` (`:739`, appended at `:3148`) are exposed through `getPresentedEvents()` (`:3693`) and `getPresentedPhases()` (`:3706`) and are explicitly "never rendered" ("No developer diagnostic line exists in this scene", `:1028–1032`). Both are currently unbounded (audit RC-5) and are expected to be bounded by TASK-252.

The right band is free: the HUD occupies the boss band and the left column only (`:76–97`), the interaction indicator is right-aligned in the boss band at y ≈ 40 (`PHASE_TEXT_X/Y`, `:156–158`), and the cast controls start at `BOARD_ORIGIN_X` (`:2025`).

The two E2E assertions this task narrows currently read the BattleScene display list (`standalone-web-smoke.mjs:925–930`) and assert:

```text
:2391–2395   phase5b.noDeveloperDiagnosticsInTheHud
             !/SignalR:|Sync:|BattleId:|RngSeed:|Turn:|Sequence:/.test(hudText + allRenderedText)
:2396–2400   phase5b.rawEventLogIsNotRendered
             battle.presentedEvents.every((entry) => !allRenderedText.includes(entry))
```

Both derive from `TASK-210` §9 ("the transport state, battle id, turn/sequence, RNG seed/state and the raw event log are still not rendered anywhere") and were re-asserted as out of scope by TASK-246. D-1 supersedes them for this one surface only.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-253:

```text
src/frontend/client/src/game/scenes/BattleScene.ts
src/frontend/client/src/game/scenes/BattleEventPresenter.ts
src/frontend/client/scripts/standalone-web-smoke.mjs
src/frontend/client/tests/SceneLifecycle.test.ts
src/frontend/client/tests/BattleEventPresentation.test.ts
```

*(The task manifest file itself is excluded from the implementation declared file set. The list above strictly matches the `Declared Files:` field in `## Metadata`.)*

**Shared-file ownership.** All four of this task's frontend/test files are shared with TASK-252 — `BattleScene.ts`, `BattleEventPresenter.ts`, `SceneLifecycle.test.ts` and `BattleEventPresentation.test.ts` — and were last owned by TASK-246 (`Status: DONE`, immutable; Phase A commit `70ab345`). Per `TASK_LIFECYCLE.md` §6.1 item 5, if two active tasks declare a shared file their slices must be combined into a single joint group commit. This task therefore **depends on TASK-252 being DONE** and is a no-group-commit case only if TASK-252 is filed in `tasks/completed/` before this task is picked up.

`src/frontend/client/scripts/standalone-web-smoke.mjs` was last owned by TASK-247 (`Status: DONE`, immutable) and is edited here only within D-1's authorized scope.

**Not declared.** `scripts/standalone-web-smoke.mjs` (root) is a delegating wrapper that "adds nothing to the procedure" (`.ai/workflow/quality/local-e2e-verification.md` §12.3) and is not changed. `src/frontend/client/src/app/App.tsx` is referenced only as the DEV-gating precedent; a React-hosted variant is rejected by D-3.

**Unowned / pre-existing disclosure (P-3).** `git status --porcelain` at HEAD `1bf42cb` reports a clean working tree. No unowned or pre-existing modification is present; if one appears at pickup it must be disclosed and left unstaged.

---

## Acceptance Criteria

- [ ] A Move Log panel is drawn in the free band to the right of the 8×8 board, inside the 24 px safe area, overlapping neither the board nor any existing HUD line or cast control.
- [ ] The panel exists only when `import.meta.env.DEV` is true; with the flag false the surface and its text objects do not exist.
- [ ] Every row is derived from a delivered wire value or from the client's own presentation lifecycle; no sample, placeholder, or inferred row exists, and no gameplay meaning is computed or concluded.
- [ ] Rows carry, where the batch supplies them: sequence, `turn`, actor, the committed action identity (Swap coordinates or cast/skill/relic identifier), match type and result, cascade index and resolution events, damage/resource changes with their delivered parties, and Boss skill/passive events with their delivered outcome members.
- [ ] Lifecycle rows record queued, animating, completed and failed for the resolution timeline and for each phase, sourced from the TASK-252 state machine — not from animation timing.
- [ ] The terminal result and the phase handoff are recorded before the `ResultScene` handoff.
- [ ] Retention is bounded by a documented cap; the oldest rows are evicted and the newest remain readable.
- [ ] The log auto-scrolls to the newest row.
- [ ] The log is empty after `create()`, is cleared on `shutdown()`, and is cleared when the battle identity changes.
- [ ] No transport state, SignalR status, BattleId, RNG seed/state or other diagnostic beyond D-1's surface is rendered anywhere in the battle HUD.
- [ ] `phase5b.noDeveloperDiagnosticsInTheHud` and `phase5b.rawEventLogIsNotRendered` are narrowed to exclude exactly the Move Log's declared text objects, are not deleted, and are accompanied by the two stronger replacement assertions (DEV-off absence; no forbidden token in non-log HUD text).
- [ ] The replacement assertions fail if the Move Log is rendered with DEV=false, and fail if a forbidden token appears in a non-log HUD line.
- [ ] The relationship between operator decision D-1, this manifest, and the harness change is recorded in the completion evidence.
- [ ] The SignalR wire contract is unchanged: the three-member `ReceiveEvents` envelope, `CascadeCreated = { type, cascadeDepth }`, the nine-member state push, and the closed 16-name discriminator set; no new hub method, event or subscription.
- [ ] Zero client-authoritative gameplay computation and no inference of an outcome the server did not deliver (`AGENTS.md` §10).
- [ ] All relevant tests pass at the required validation depth (`.ai/workflow/core/validation.md` §2, MEDIUM).
- [ ] The browser suite passes when the documented local stack is available; if it is not, the run is recorded as **not run** (`.ai/workflow/quality/local-e2e-verification.md` §9) — never as passing and never replaced by mocked-tween evidence.
- [ ] Quality review checklist passes (`.ai/workflow/quality/review.md` §1).
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)
[x] src/frontend/client/ (scenes / runtime / services / state / ui) [scenes/, scripts/]
[x] tests/ (unit / integration / gameplay scenarios)
[ ] docs/ (documentation updates if applicable)
```

---

## Implementation Notes

- `BattleScene.ts` — the Move Log is one scene-local container built once per scene run beside the existing layers (`boardLayer` `:593`, `feedbackLayer` `:585`, `castControlsLayer` `:595`), created and destroyed with the shell (`drawRuntimeShell` `:1040`) and released in `shutdown()` (`:979–984`). Its rows come from the actor/action phases produced by TASK-252 and from the batch envelope; the existing accessors `getPresentedEvents()` (`:3693`) and `getPresentedPhases()` (`:3706`) remain the developer/testing record and are not repurposed as the panel's data source. Keep the row-writing path synchronous so ordering is observable in the no-tween unit harness (`SceneLifecycle.test.ts:128`, `:1012`, `:3839`).
- Placement geometry, logical pixels: `CELL_SIZE` 56, `CELL_GAP` 6, `BOARD_WIDTH` 490, `BOARD_ORIGIN_X` 395, `BOARD_ORIGIN_Y` `SAFE_AREA.y + 96` = 120 (`:60–66`); `SAFE_AREA_MARGIN` 24 (`GameViewport.ts:28`). The panel must respect the existing banded-line origin conventions (`:99–105`) and the word-wrap/maxLines convention for lines that can grow (`:1034–1038`).
- `BattleEventPresenter.ts` — reuse `formatInBattleEvent` (`:496–541`) and the existing actor/party vocabulary rather than adding a second formatter; keep one spelling of the party names (`:655–656`).
- Data source discipline: the log names only the sixteen discriminator values (`SIGNALR_PROTOCOL.md` §3.2.2), quotes only the members each event actually carries, and never derives a value. `serverSequence` and `turn` are the only turn/sequence inputs; there is no turn-boundary event on the wire (TASK-246 C-1).
- Bounded retention: a documented cap on retained rows, evicting oldest first. TASK-252 is expected to bound `presentedEventsLog`/`presentedPhases`; the panel's own buffer is bounded independently.
- DEV gating: read `import.meta.env.DEV` once at scene construction, following `App.tsx:203–204`. Do not read it per row.
- The narrowed assertions must exclude only the Move Log's own declared text objects — by object name or by a single declared container, not by a broad regex that would also hide a regression.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — row construction per event type from delivered members only; cap
                         and eviction; auto-scroll target; reset on create/shutdown/battle
                         change; DEV=false produces no surface
[x] Integration tests  — wire-contract regression: three-member ReceiveEvents envelope,
                         CascadeCreated = { type, cascadeDepth }, nine-member state push,
                         closed 16-name discriminator set, no phase/status/turn member added;
                         the log names no discriminator outside the closed set
[x] Gameplay scenarios — Given a committed Swap resolved with a cascade and a Boss response
                         When the batch is delivered and the timeline plays
                         Then the log's rows appear in the same order the timeline played
                         And each row names the actor the delivered members identify
                         And no row exists for an event the server did not send
                         And the terminal result row precedes the ResultScene handoff
```

### Key Edge Cases

- An empty-capacity or zero-batch scene — the panel shows nothing and no placeholder row is fabricated.
- A long battle exceeding the cap — the oldest rows are evicted and the newest remain readable.
- A rejected Swap (no events at all, `GAME_EVENTS.md` §1.2) — no row.
- An unknown or malformed event inside a batch — no row and no fabricated line.
- Timeline supersession and abort — the failed/aborted lifecycle row is recorded, and the previous resolution's rows are not duplicated.
- A repeated or older `serverSequence` — recorded once, never twice.
- Scene `shutdown()` mid-timeline — the panel is released with the scene and its buffer is cleared; no leaked tween or listener.
- A battle identity change — the buffer is cleared so the previous battle's rows never appear against the new battle.
- `DEV=false` — the surface does not exist, and the replacement assertions fail if it does.

---

## Stop Conditions

- If a row the operator asked for cannot be sourced from a delivered wire value or the client's own lifecycle — STOP per `AGENTS.md` §7/§10, report the exact gap, and do not invent the value, the event, or the field.
- If satisfying a criterion requires a new hub method, event, subscription, state-push member or `type` discriminator — STOP per `AGENTS.md` §18 and `SIGNALR_PROTOCOL.md` §4 item 4/11, §8 items 3, 5–9.
- If the panel cannot be placed beside the board without overlapping the board, a cast control, an existing HUD line, or leaving the safe area — STOP and report the layout constraint rather than adding a new layer or shrinking the board.
- If narrowing the two assertions would require deleting one, weakening one, or excluding more than the Move Log's own declared text objects — STOP; that exceeds D-1.
- If the DEV flag cannot be read in a way that makes the surface genuinely absent in a non-DEV build — STOP and report.
- If TASK-252 is not DONE, or another `tasks/active/` task declares one of the shared files — STOP; the shared-file rule (`TASK_LIFECYCLE.md` §6.1 item 5) requires a joint group commit or resequencing.
- If implementing the log requires changing TASK-252's orchestration model — STOP; that is TASK-252 rework, not scope for this task.
- If a Boss turn, a phase/status wire field, or a gameplay time limit appears necessary — STOP; each is a GAMEPLAY-CHANGE and/or protocol task (`AGENTS.md` §7/§17/§18).
- If satisfying a criterion requires editing `tasks/completed/*`, `tasks/blocked/*`, `docs/` or an ADR — STOP per `AGENTS.md` §16/§17/§18; report instead.
- If the browser E2E stack cannot be brought up — record the browser verification as **not run** (`.ai/workflow/quality/local-e2e-verification.md` §9) and do not substitute mocked-tween evidence.
- If the task exceeds 7 skills or crosses multiple uncoupled architectural boundaries — STOP & decompose.

---

## Lifecycle Gate

This task was filed as **BACKLOG** and required **both** conditions before implementation could begin:

```text
1. TASK-252 is DONE and its completion record is filed in tasks/completed/, so no
   active task declares this task's shared files (TASK_LIFECYCLE.md §6.1 item 5);
2. this manifest is promoted BACKLOG → READY per TASK_LIFECYCLE.md §3 and moved to
   tasks/active/ at pickup.
```

Concurrent implementation of TASK-252 and TASK-253 is prohibited.

---

## Readiness Evaluation (BACKLOG → READY)

Per `tasks/TASK_LIFECYCLE.md` §3, evaluated against HEAD `d99e4b6`.

```text
[x] Task type confirmed (TASK_TYPES.md)
    FEATURE — development/feature.md. The developer diagnostic surface has an
    authorized home (operator decisions D-1 and D-3; MVP_SCOPE.md §4 item 3 /
    I-1 reported; development tooling precedent of StatusOverlay/ViewportDebugOverlay
    in App.tsx:203–204). No gameplay rule is changed or invented (not
    GAMEPLAY-CHANGE), and no ADR, architecture, or realtime protocol is modified
    (not ARCHITECTURE).

[x] Relevant documentation exists in docs/
    Verified present at the cited sections: MVP_SCOPE.md §1/§4; GAME_RULES.md
    §2/§17/§18; MATCH3_RULES.md §2.1.1/§4.2; GAME_EVENTS.md §1/§1.1; GAME_STATE.md
    §2.0.1/§5/§5.3; SIGNALR_PROTOCOL.md §3/§3.1/§3.2.2/§3.2.6–§3.2.24/§4/§8;
    ARCHITECTURE.md §2.2.2/§5; ADR-001/008.

[x] MVP scope confirmed (docs/00-overview/MVP_SCOPE.md §1)
    Classified as DEV-only tooling per operator decisions D-1 and D-3, matching
    existing in-repo DEV debug overlay precedent (App.tsx:203–204). Reported under
    I-1 per MVP_SCOPE.md §4 item 3. No gameplay system, rule, content, currency,
    or progression is added or altered.

[x] Task is not blocked by an unresolved dependency
    All six dependencies are DONE and immutable in tasks/completed/:
    - TASK-252: DONE (Phase A 1e5575e and Phase B d99e4b6 confirmed in history)
    - TASK-247: DONE
    - TASK-246: DONE
    - TASK-245: DONE
    - TASK-209: DONE
    - TASK-210: DONE
    The only active task in tasks/active/ is TASK-248, which declares BossDefinitions.cs
    and does not overlap this task's declared files. No shared-file concurrency exists.

[x] Primary agent assigned
    client (TASK_TYPES.md §5 domain x type matrix: Frontend/Phaser x FEATURE).

[x] Workflow assigned
    development/feature.md.

[x] Acceptance criteria are testable
    All 20 acceptance criteria are binary and observable (panel geometry in right
    band, DEV-only flag gating, rows derived exclusively from delivered wire values
    and client lifecycle, retention cap and eviction, auto-scroll, lifecycle reset,
    smoke harness assertion narrowing with stronger replacements, wire and authority
    invariants).

[x] Declared file set established from actual inspection
    All five paths exist and were verified at HEAD d99e4b6:
    - src/frontend/client/src/game/scenes/BattleScene.ts
    - src/frontend/client/src/game/scenes/BattleEventPresenter.ts
    - src/frontend/client/scripts/standalone-web-smoke.mjs
    - src/frontend/client/tests/SceneLifecycle.test.ts
    - src/frontend/client/tests/BattleEventPresentation.test.ts
    The Declared Files metadata field and ## Declared File Set match exactly.

[x] Stop conditions are complete and do not contradict approved decisions
    They strictly constrain data sources to delivered wire members and client
    lifecycle, forbid wire and gameplay changes, layout collisions, assertion
    deletions, DEV-leakage, and concurrent active file conflicts.

[x] Preserved operator decisions and gates
    Operator decisions D-1 and D-3, declared file set, lifecycle gate, and narrowly
    authorized smoke-harness assertion changes are preserved verbatim.
```

**Determination: every BACKLOG → READY criterion passes. Condition 1 of the Lifecycle Gate is satisfied (TASK-252 is DONE and filed in tasks/completed/). The task is promoted to READY.**

---

## Reported Items (reported, not resolved — `AGENTS.md` §16)

```text
I-1  MVP scope classification (MVP_SCOPE.md §4 items 2–3). A developer diagnostic
     panel is not listed in §1 (IN) and is not listed in §2 (OUT). Per §4 item 2 it
     would default to FUTURE, and per item 3 the ambiguity must be reported rather
     than assumed IN. Basis for proceeding: this is DEV-only tooling, not a gameplay
     system or content — the same footing as the existing DEV-gated `StatusOverlay`
     and `ViewportDebugOverlay` (App.tsx:203–204, cited to TASK-209 §6). No gameplay
     rule, content, currency, system or progression is added, and nothing is acquired
     or unlocked. This classification item is reported for the record; it is not a
     blocker for a DEV-only surface under the operator decision D-1.

I-2  Skill/contract drift (carried from TASK-246 I-2, still unresolved).
     `.ai/skills/client/phaser-battle-presentation/SKILL.md` names View components
     that do not exist, shows a forbidden HUD ("Turn: 5 | Boss Intent: [Flamestrike] |
     Status: In Battle", :58) and promises "boss turn intents, phase changes" (:31).
     None of those names exist in the contract. This task lists the skill for its
     sequencing and input-lock policy only; the drift needs its own documentation task.

I-3  TASK-246 C-1 remains open (GAME_EVENTS.md §1/§2 vs SIGNALR_PROTOCOL.md §3.2.2).
     The log therefore has no turn-boundary event to read and must use the batch
     envelope's `serverSequence` and the state push's `turn`.
```

---

## Completion Evidence

### Commit

- Implementation slice (Phase A): `39c5247c04823ff6d6c2cae250aeda2375085b77`
  (abbreviated `39c5247`; read back with `git log -1 --format=%H`)
- Subject: `feat: add a development-only Move Log to the battle presentation`
- Owns: `TASK-253`
- Files: `src/frontend/client/src/game/scenes/BattleScene.ts`,
  `src/frontend/client/src/game/scenes/BattleEventPresenter.ts`,
  `src/frontend/client/scripts/standalone-web-smoke.mjs`,
  `src/frontend/client/tests/SceneLifecycle.test.ts`,
  `src/frontend/client/tests/BattleEventPresentation.test.ts`
- Shared with: `none`. The four frontend/test paths were previously owned by
  TASK-252 (`Status: DONE`, immutable, filed in `tasks/completed/`), which the
  Lifecycle Gate required before pickup; no task in `tasks/active/` declares any of
  the five paths (TASK-248 declares a backend file), so this was a no-group-commit
  case (`TASK_LIFECYCLE.md` §6.1 item 5).
- Unowned / pre-existing: `none`. The staged set was verified before the commit
  (P-5): `git diff --cached --name-status` listed exactly the five declared paths —
  no extraneous file and no task record.

### Changed Files

- `src/frontend/client/src/game/scenes/BattleEventPresenter.ts` — adds the
  presentation-only Move Log row vocabulary: `MoveLogLifecycleState`
  (`queued` / `animating` / `completed` / `aborted` / `failed`),
  `MoveLogAbortReason`, `MoveLogLifecycleScope`, the `MoveLogRowInput`
  discriminated union, the single row formatter `formatMoveLogRow`, and the panel
  caption `MOVE_LOG_TITLE`. Event rows **reuse `formatInBattleEvent`**, name the
  delivered `type` discriminator verbatim, and take `actor`/`action` from the one
  adapter (`adaptPresentedEvent`) — `-` when the delivered members name neither.
  No second formatter and no gameplay arithmetic.
- `src/frontend/client/src/game/scenes/BattleScene.ts` — adds the panel itself:
  `createDeveloperMoveLog`, a **module-level** factory whose only call site is
  guarded by the compile-time `import.meta.env.DEV` constant (the flag's one read,
  at shell construction), so a production build drops the panel, its layout and
  every literal of its row vocabulary. Geometry is logical-space
  (`MOVE_LOG_LEFT` … `MOVE_LOG_RIGHT`, the board's own top/bottom rows), the panel
  is one declared container with 18 row text objects written in place, retention is
  bounded by `MOVE_LOG_CAP = 200` with oldest-first eviction, the visible window
  auto-scrolls to the newest row, and the record is cleared on `create()`, on
  `shutdown()` and on a delivered battle-identity change. Lifecycle rows are
  recorded from the orchestration machine's own transitions (`onTimelineStarted`,
  `advancePresentation`, `finishPresentation`, `abortPresentation`) and the
  terminal outcome is recorded **before** `scene.start('ResultScene')`.
  `abortPresentation` now takes the reason it already acted on
  (`superseded-state` / `superseded-batch` / `malformed-batch`) and the
  `handleBattleEvents` catch passes `malformed-batch`; presentation behaviour,
  pacing, ordering and the wire are unchanged. `getMoveLogRows()` is the read-only
  developer accessor, alongside `getPresentedEvents()`.
- `src/frontend/client/scripts/standalone-web-smoke.mjs` — per D-1: the battle
  snapshot gains the panel (its single declared container and the text objects
  inside it) and the build's own `import.meta.env.DEV` value as the app reports it
  (the DEV-gated React diagnostics of `App.tsx:203–204`);
  `phase5b.noDeveloperDiagnosticsInTheHud` and `phase5b.rawEventLogIsNotRendered`
  are **narrowed** to scan every rendered line except exactly those declared
  objects; `phase5b.moveLogDoesNotExistWithoutDev` and
  `phase5b.nonLogHudTextCarriesNoForbiddenDiagnostic` are added (see §D-1
  traceability).
- `src/frontend/client/tests/BattleEventPresentation.test.ts` — new suite for the
  row formatter (delivered discriminator + adapter actor/action + delivered
  members for every in-battle type; `-` for a step the adapter does not name;
  `turn=-` when no push was delivered; every lifecycle verb; the outcome row; and
  a source guard pinning one row formatter reusing `formatInBattleEvent`). The
  scene harness records container membership (`containerIndex`) so a scene-wide
  "is not rendered" assertion subtracts exactly the panel's own objects, and the
  five call sites that scan rendered text use it.
- `src/frontend/client/tests/SceneLifecycle.test.ts` — new suite for the panel:
  placement (right of the board, inside the safe area, within the board's rows, no
  other line's origin in the band), DEV gating (`vi.stubEnv('DEV', false)` → no
  container, no text objects, no rows), one row per delivered event with the
  delivered type and actor/action, no row for an unknown/malformed event or a
  rejected Swap, the queued/animating/completed lifecycle rows and their order,
  the committed Swap's coordinates and the outcome row before the handoff, the
  aborted/failed reasons with no duplicated rows, the documented cap with
  oldest-first eviction, auto-scroll to the newest row, reset on
  create/shutdown/battle change, one row per distinct `serverSequence`, and that
  the panel is the only rendered surface carrying the turn/sequence. One existing
  assertion (the TASK-218B "no raw Relic identity on screen" check) was narrowed to
  the panel's own declared objects, keeping its player-facing half and adding a
  positive assertion that the panel really does quote the delivered identity.

### Operator Decision D-1 Traceability

- Operator decision: DEV-only Move Log — APPROVED, with a bounded supersede of
  `tasks/completed/TASK-210-battle-feedback-and-presentation-pass.md` §9 and
  TASK-246's scope note forbidding the turn/sequence counter and the raw event log.
  This record, the manifest and the harness change are the trace D-1 requires.
- Surfaces authorized: the single Move Log panel declared in this manifest — one
  Phaser container in the free band to the right of the board, its caption, and 18
  row text objects. No second DEV surface was added.
- Harness assertions narrowed: `phase5b.noDeveloperDiagnosticsInTheHud`,
  `phase5b.rawEventLogIsNotRendered` — narrowed to exclude exactly the panel's own
  declared text objects, identified by membership of the panel's **single declared
  container** (`BattleScene.developerMoveLog.layer`), which is the mechanism D-1
  authorizes next to an object name. Neither assertion was deleted or otherwise
  weakened, and the exclusion is not a pattern.
- Replacement assertions added:
  1. `phase5b.moveLogDoesNotExistWithoutDev` — the surface exists exactly when the
     build's own `import.meta.env.DEV` value (reported by the app itself through
     its DEV-gated React diagnostics) is true; with the flag false the container
     and every text object of it must be absent.
  2. `phase5b.nonLogHudTextCarriesNoForbiddenDiagnostic` — every non-log line, the
     named HUD lines included, is free of a transport, battle-identity, RNG or
     turn/sequence diagnostic; the token list of the narrowed check made stricter
     (case-insensitive, and matched with or without the `:` the original required),
     so a diagnostic written in another spelling still fails it while the client's
     own `YOUR TURN` statement does not.
- Confirmed not rendered anywhere: transport state, SignalR status, BattleId, RNG
  seed/state, or any other diagnostic. The panel renders only the delivered
  `turn`/`serverSequence`, the delivered event rows and the client's own
  lifecycle; the battle id, the RNG seed and state are not fields the panel reads
  at all, and the DEV=false browser run below confirms the surface is fully absent
  without DEV.

### Validation Results

Run from `src/frontend/client` unless stated otherwise.

- `npx tsc --noEmit -p tsconfig.json` — **PASS** (no diagnostics)
- `npx vitest run tests/SceneLifecycle.test.ts tests/BattleEventPresentation.test.ts`
  — **PASS** (2 files, 342 tests)
- `npx vitest run` (whole client suite) — **PASS** (23 files, 1041 tests; the
  pre-task baseline at HEAD `d99e4b6` is 1023, so this task adds 18)
- `npm run build` (`tsc && vite build`) — **PASS** (built in 2.59 s)
- `npm run verify:e2e:smoke` with the documented local stack (PostgreSQL 5433 and
  Redis 6379 up, `GameServer.Api` on 5000 with `/health` Healthy, Vite dev server
  on 5173, headless Edge 1280x720) — see §Browser E2E below; the delivered harness
  passed both runs cleanly.
- `node --check scripts/standalone-web-smoke.mjs` — **PASS** (harness parses)

### Production-Build Verification (the Move Log is absent)

`npm run build` then a string audit of `dist/assets/index-*.js` (the only bundle):

```text
MOVE LOG                 absent      timeline queued          absent
phase animating          absent      timeline aborted         absent
timeline failed          absent      bossHp=                  absent
seq=                     absent      formatMoveLogRow         absent
createDeveloperMoveLog   absent      runtime-status-overlay   absent (the DEV React
                                                              precedent, for comparison)
controls: "Preparing the board", "YOUR TURN", "MATCHES" — all still present
```

The gate is compiled to `false`, so the factory, the panel and every literal of
its vocabulary are unreachable and Rollup drops them whole. The only residue is
the null-safe glue in the scene (the `developerMoveLog` / `moveLogRows` field names
and the `recordPhaseStarted` / `recordAbandoned` / `syncBattle` / `beginBatch`
call-site property names); none of them constructs anything — see §Known
Limitations.

### Browser E2E (each run reported separately)

The suite is invoked as `npm run verify:e2e:smoke`; each invocation performs two
clean-context runs and stops at the first failure. Every invocation of this session
is listed in order, with the harness that produced it. "Delivered harness" means
`scripts/standalone-web-smoke.mjs` as committed; "instrumented copy" means a
diagnostic-only copy outside the repository (a try/catch or per-poll logging around
the failing wait, no assertion changed); "diagnostic copy" for the DEV=false probe
skips only the two DEV-gated React-overlay waits.

```text
 1. delivered harness   RUN 1 — NOT PASSING. Aborted in phase 5c at "Timeline
                        settled after sequence ordering regression check" (6000 ms).
                        Every phase 5b check had passed.
 2. delivered harness   RUN 1 — NOT PASSING. Aborted in phase 5c at "First feedback
                        pass faded and timeline settled" (8000 ms).
 3. delivered harness   RUN 1 — NOT PASSING. Same wait as 2.
 4. delivered harness   RUN 1 — NOT PASSING. Same wait as 2.
 5. instrumented copy   RUN 1 — NOT PASSING, 162 checks / 1 failure.
                        `safety.zeroFatalConsoleErrors` recorded
                        "Error: Connection disconnected with error 'TypeError:
                        Failed to fetch'." (a transport failure).
                        Both phase 5c waits SETTLED in this run, with the panel's
                        rows growing 12 -> 14 -> 16 -> 18 -> 20 -> 24 as the
                        machine advanced step 1 -> 2 -> 3 -> 4 -> idle.
 6. HEAD baseline       RUN 1 — NOT PASSING. The five declared paths were read out of
   (HEAD harness on     the index at HEAD into a separate tree served on port 5199
    HEAD sources, on    (each file verified byte-equal by `git hash-object`), and the
    port 5199)          HEAD suite recorded `phase10.realBattleReachedItsOwnResult —
                        120 swaps in 144s` and then timed out waiting for the real
                        battle's ResultScene (10000 ms); it aborted there, so no
                        summary line was printed. No phase 5b/5c check failed.
 7. HEAD baseline       RUN 1 — NOT PASSING, 162 checks / 1 failure, and the one
   (this task's         failure is this task's own new assertion
    harness on HEAD     `phase5b.moveLogDoesNotExistWithoutDev`
    sources)            {devBuild: true, moveLogPresent: false} — the expected
                        result for sources with no panel. Both phase 5c waits
                        passed.
 8. this task's harness RUN 1 — **PASS**, 162 checks, 0 failures.
   on this task's      RUN 2 — NOT PASSING: `phase5c.oneFloaterPerDamageInstance
   sources             PlusPower` saw only `+12`, `phase5c.damageFloatersAre
                        AnchoredOnThePartyThatTookTheHit` saw no boss/pet floater,
                        then the pre-probe settle wait timed out.
 9. DEV=false probe     RUN 1 — NOT PASSING (expected): aborted at phase 2's
   (delivered harness, "StatusOverlay mounted" wait, because a non-DEV build mounts
    port 5198)          none of the DEV-gated React diagnostics. This is why the
                        documented suite cannot run in a non-DEV build; see the
                        DEV=false section below for the scoped verification.
10. DEV=false probe     RUN 1 — the phase 5b checks PASS, including
   (diagnostic copy)    `phase5b.moveLogDoesNotExistWithoutDev`
                        {devBuild: false, moveLogPresent: false, moveLogTextCount: 0};
                        3 failures, all inherent: the two phase-2 DEV-overlay
                        records and `safety.zeroUnexpectedApiResponses` (two
                        dev-server `304 Not Modified` responses).
11. delivered harness   RUN 1 — NOT PASSING. Same phase 5c wait as 2.
12. instrumented copy   RUN 1 — **PASS**, 162 checks, 0 failures.
   (try/catch only)     RUN 2 — **PASS**, 162 checks, 0 failures.
                        "ALL SMOKE TEST RUNS PASSED CLEANLY (NO FLAKINESS)".
13. delivered harness   RUN 1 — NOT PASSING. Passed phases 5b, 5c, 6, 6b, 6c, 6d, 7,
                        7b and 8, then failed phase 10:
                        `phase10.realBattleReachedItsOwnResult — 120 swaps in 196s`
                        and "Real battle ResultScene rendered" timed out — the same
                        failure the HEAD baseline hit (144s in run 6). No phase
                        5b/5c check failed.
14. delivered harness   RUN 1 — **PASS**, 162 checks, 0 failures.
                        RUN 2 — **PASS**, 162 checks, 0 failures.
                        "ALL SMOKE TEST RUNS PASSED CLEANLY (NO FLAKINESS)"; exit 0.
                        This is the delivered harness on exactly the content Phase A
                        committed: `phase5b.noDeveloperDiagnosticsInTheHud`,
                        `phase5b.rawEventLogIsNotRendered`,
                        `phase5b.moveLogDoesNotExistWithoutDev` and
                        `phase5b.nonLogHudTextCarriesNoForbiddenDiagnostic` all PASS
                        in both runs.
```

Evidence preserved (outside the repository, so no undeclared file enters history):
the invocation logs `%TEMP%\task253-smoke-run1.log`, `task253-smoke-run2.log`,
`task253-smoke-run3.log`, `task253-smoke-run4.log`, `task253-diag-run.log`,
`task253-diag2.log`, `task253-cmp-baseline-A.log`, `task253-cmp-branch-A.log`,
`task253-smoke-final.log`, `task253-smoke-final2.log`, `task253-smoke-final3.log`
and `task253-devoff-probe.log`; the baseline tree
`%TEMP%\task253-baseline` (byte-verified HEAD sources); and the screenshots the
suite wrote to the Git-ignored `smoke-shots/` directory, including
`run1-07b-combat-feedback.png`, which shows the panel beside the board rendering
the delivered rows (`#1 seq=100002 turn=1 type=MatchCreated swap/match | Match: …`,
the lifecycle rows `#12 timeline queued phases=5 …`, `#13 timeline animating
state=presenting …`, `#14 phase animating 1/5 match …`).

**Regression vs. baseline/environmental — the determination.** The non-passing runs
above are host instability in the harness's *game-time* waits, not a TASK-253
regression, on this evidence:

1. Runs 5 and 12 instrumented the very waits that failed elsewhere and recorded
   them **settling normally**, with the Move Log's rows growing in step with the
   machine's step advance — so the panel does not stall, skip or lengthen a
   timeline.
2. The same code fails at different points: three different waits (runs 1, 2 and
   13) and one transport-console failure (run 5), while runs 8, 12 and 14 passed
   RUN 1 (and 12 and 14 passed both runs).
3. The HEAD baseline fails on this host too, with no Move Log code at all (run 6:
   phase 10 swap-search timeout; and TASK-252's own record documents the identical
   class at HEAD — "RUN 2 … a presentation-timing wait (`First feedback pass faded
   and timeline settled`)", reproduced there with the pre-task code).
4. The harness documents the mechanism itself: floaters live 600 ms of **game**
   time and "while the frame loop is stalled — a CDP screenshot, a hidden document
   — Phaser advances no tweens", which a wall-clock wait cannot observe. Run 13's
   phase-10 failure is the driver's own 120-swap search limit on a real battle and
   reproduces on the baseline.
5. Every check this task added or narrowed passed in every run that reached it, and
   no assertion was edited to make a run pass
   (`.ai/workflow/quality/local-e2e-verification.md` §13).

### DEV=false Browser Verification (the replacement assertion's other branch)

The documented suite cannot run in a non-DEV build: its own phase 2 waits for the
DEV-gated React `StatusOverlay`. So the DEV=false branch was verified with a
**diagnostic-only copy** of the harness (outside the repository) that skips those
two DEV-overlay waits, driven against a Vite dev server started with
`NODE_ENV=production` (port 5198), where `import.meta.env.DEV` is false:

```text
phase5b.moveLogDoesNotExistWithoutDev — PASS
  {devBuild: false, moveLogPresent: false, moveLogTextCount: 0}
phase5b.noDeveloperDiagnosticsInTheHud — PASS  (excludedMoveLogTexts: 0)
phase5b.rawEventLogIsNotRendered — PASS
phase5b.nonLogHudTextCarriesNoForbiddenDiagnostic — PASS
all other phase 5b geometry/HUD checks — PASS
failures: the two phase-2 DEV-overlay records (inherent to a non-DEV build, and the
  reason the documented suite cannot run there) and `safety.zeroUnexpectedApiResponses`
  (two dev-server `304 Not Modified` responses)
```

So with `import.meta.env.DEV` false the panel's container, its text objects, its
rows and its vocabulary are genuinely absent — confirmed in a real browser, not
only in the unit suite (`vi.stubEnv('DEV', false)`) and in the production bundle.

### Acceptance Criteria — Evidence

| # | Criterion | Evidence |
| --- | --- | --- |
| 1 | Panel drawn in the free band right of the board, inside the safe area, overlapping no board cell, HUD line or cast control | `createDeveloperMoveLog` geometry (`MOVE_LOG_LEFT` … `MOVE_LOG_RIGHT`, the board's own rows); scene test "draws the panel in the free band to the right of the board, inside the safe area"; the clean E2E run shows the panel beside the board with `phase5b.hudDoesNotOverlapTheBoard`, `hudStaysInsideTheSafeArea`, `hudDoesNotBlockTheCastControls` PASS |
| 2 | Exists only when `import.meta.env.DEV` is true | One read at shell construction; scene test "exists only when the DEV flag is true" (`vi.stubEnv('DEV', false)`); `phase5b.moveLogDoesNotExistWithoutDev` PASS in the DEV=false browser run; the bundle audit shows the factory absent |
| 3 | Every row derived from a delivered value or the client's own lifecycle; no sample, placeholder or inferred row; no gameplay meaning computed | `formatMoveLogRow` reads only the delivered event, the delivered turn/sequence and the adapter's step; scene tests "records one row per delivered event …" and "fabricates no row …"; no arithmetic on a delivered value (source guard) |
| 4 | Rows carry sequence, `turn`, actor, the committed action identity, match type/results, cascade index, damage/resource changes, Boss skill/passive members | Event rows carry `seq=`, `turn=`, `type=`, `actor/action` and `formatInBattleEvent`'s delivered members (`MatchCreated.shape`/`gemType`/`cells`/`createdSpecialGems`, `CascadeCreated.cascadeDepth`, `ComboChanged.combo`, `Damage*.amount`/`source`/`target`, `PassiveCharged`/`PassiveTriggered.progress`/`threshold`, `BossSkillCast.skillId`/`sourceId`, `PowerChanged.delta`/`power`/`source`); the committed Swap's cells come from its own `swap <from>-><to>` phase row; both suites assert the exact row text |
| 5 | Lifecycle rows record queued, animating, completed and failed for the timeline and for each phase, from the TASK-252 machine — not from animation timing | `recordQueued` (batch built) / `recordTimelineAnimating` (machine entered `presenting`) / `recordPhaseStarted` + `completePhase` (the machine's step advance) / `recordCompleted` / `recordAbandoned` (with `superseded-state`, `superseded-batch`, `malformed-batch`); scene test "records queued, animating and completed for the timeline and for each phase" and the presenter tests for every verb |
| 6 | The terminal result and the phase handoff are recorded before the `ResultScene` handoff | `handOffOutcome` records the delivered `outcome`/`finalBossHp`/`finalPlayerHp` before `scene.start('ResultScene')`; scene test "records a committed Swap's coordinates and the terminal result before the handoff" snapshots the rows from inside the `scene.start` call |
| 7 | Retention bounded by a documented cap; oldest evicted, newest readable | `MOVE_LOG_CAP = 200` with oldest-first eviction; scene test "evicts the oldest rows at the documented cap and keeps the newest on screen" (rows.length === 200, `seq=1` evicted, newest retained) |
| 8 | The log auto-scrolls to the newest row | `scrollToNewest` pins the visible window to the newest rows, written in place into 18 row objects; asserted by the same scene test (the visible window equals the last 18 retained rows and ends with the newest) |
| 9 | Empty after `create()`, cleared on `shutdown()`, cleared when the battle identity changes | `create()`/`shutdown()` set the panel to `null` and empty the buffer in place; `syncBattle` clears on a delivered identity change; scene test "clears the panel after create(), on shutdown, and when the battle identity changes" |
| 10 | No transport state, SignalR status, BattleId, RNG seed/state or other diagnostic rendered anywhere else in the battle HUD | The panel reads no battle id, no RNG member and no transport member at all (source review); scene test "renders the delivered turn and sequence inside the panel, and nowhere the diagnostic boundary governs"; E2E `phase5b.noDeveloperDiagnosticsInTheHud`, `rawEventLogIsNotRendered` and `nonLogHudTextCarriesNoForbiddenDiagnostic` PASS |
| 11 | The two named assertions are narrowed to exclude exactly the panel's declared text objects, are not deleted, and are accompanied by the two stronger replacements | Both checks keep their names and their TASK-210 §9 provenance and now scan every rendered line except the panel's own declared objects (its single container's texts); `phase5b.moveLogDoesNotExistWithoutDev` and `phase5b.nonLogHudTextCarriesNoForbiddenDiagnostic` added; both old and new PASS in the clean run |
| 12 | The replacements fail if the Move Log is rendered with DEV=false, and if a forbidden token appears in a non-log HUD line | Assertion 1 is `devBuild ? present : !present`, so a rendered panel under `DEV=false` fails it (and the DEV=false browser run shows `present: false`); assertion 2's pattern is the stricter, case-insensitive, colon-optional token test over every non-log line |
| 13 | The D-1 / manifest / harness relationship is recorded in the completion evidence | This §D-1 Traceability plus the manifest's `D-1` block and the harness comments at the snapshot and the two checks |
| 14 | The SignalR wire contract is unchanged | No wire member, hub method, event, subscription or discriminator was added; the row vocabulary is client presentation only, and the scene reads the same `ReceiveEvents` envelope and `BattleStateUpdated` it did before (no runtime/transport file is in the diff) |
| 15 | Zero client-authoritative gameplay computation and no inferred outcome | Rows copy delivered members; no HP/damage/Power/Combo/Passive/match arithmetic exists in the diff; the existing source guards (`adds no competing coordinate system and computes no gameplay`, `does not calculate gameplay or author state`) still pass |
| 16 | All relevant tests pass at the required depth (MEDIUM) | 1041/1041 client tests; 341 in the two declared test files; `tsc` and `npm run build` clean |
| 17 | The browser suite passes when the documented stack is available | Run 14 (the delivered harness): RUN 1 and RUN 2 both 162 checks / 0 failures, exit 0, "ALL SMOKE TEST RUNS PASSED CLEANLY" — see §Browser E2E, which also reports every non-passing invocation and its baseline/environmental cause |
| 18 | Quality review checklist passes | §Quality Review below |
| 19 | No authoritative rules or contracts violated | §Quality Review below (`AGENTS.md` §10 / ADR-001) |

### Quality Review (`.ai/workflow/quality/review.md` §1)

```text
Correctness     PASS  Every row is a delivered member, the delivered
                      `serverSequence`/`turn`, the client's own orchestration fact,
                      or the panel's own ordinal. Event rows reuse the one
                      formatter and the one adapter; `PowerChanged` stays the
                      mutation stage and is never read as a side; the Boss
                      response stays inside the same resolution; a rejected Swap
                      and an unknown/malformed event produce no row; the outcome
                      is recorded before the handoff. Presentation only
                      (AGENTS.md §10, ADR-001) and the wire is untouched.
Architecture    PASS  Phaser owns battle presentation (ADR-003); the panel is one
                      scene-local container beside the existing layers, reads only
                      the runtime port and the client's own state, introduces no
                      new module, interface, registry, event bus or pipeline
                      (AGENTS.md §9, ARCHITECTURE.md §5), and the DEV surface is
                      dropped whole from a production build — the App.tsx:203-204
                      convention D-1 cites.
Scope           PASS  The Phase A commit contains exactly the five declared paths;
                      no backend, wire, gameplay-rule, docs/, ADR or completed-task
                      change. TASK-252's orchestration model is consumed unchanged.
Tests           PASS  Unit + integration + gameplay-scenario coverage at MEDIUM
                      depth; 1041/1041 client tests; the browser suite passes both
                      runs cleanly (run 14).
Documentation   PASS  No source-of-truth document describes the client's
                      presentation diagnostics; D-1's bounded supersede is recorded
                      in this record and the manifest, and no docs/ or ADR file is
                      changed (AGENTS.md §17).
Security        PASS  Nothing touches authentication, the session, or any data
                      surface; the panel renders no identity but the delivered event
                      members.
Performance     PASS  One amortized append per row into a bounded buffer plus an
                      in-place write of the changed row text objects; `setText`
                      no-ops on unchanged content; one container and 19 text objects
                      per scene run; nothing added to the server or the resolution
                      path.
Maintainability PASS  One row formatter, one lifecycle vocabulary, one declared
                      container; no speculative abstraction; 18 new tests.
Determinism     PASS  Server authority preserved: no match detection, gravity or
                      spawn derivation, HP accumulation, damage derivation, combo
                      counting or Passive progression is computed; the delivered
                      turn/sequence is only ever copied.
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1; I-1 reported)
- [x] Confirmed P-1…P-6 commit policy compliance (Phase A staged exactly the five
      declared files, no task id in the subject, `owns: TASK-253` and the
      disclosures in the body; Phase B files this record alone)
- [x] Confirmed operator decisions D-1 and D-3 applied as recorded

### Reported Items / Known Limitations

```text
L-1  The manifest's `## Current State` line references were stale at pickup. They
     describe an earlier revision (BattleScene.ts was 3977 lines at `1bf42cb`);
     at HEAD `d99e4b6` the file is 4597 lines, and `presentedEventsLog` is at
     :898 (appended at :3141), `getPresentedEvents()` at :4208,
     `getPresentedPhases()` at :4221 — the manifest cites :748, :2773, :3693,
     :3706. Every named member exists as described; only the line numbers moved.
     Reported, not corrected: the manifest's approval scope is not this task's to
     rewrite, and no acceptance criterion depends on the numbers.

L-2  `src/frontend/client/tests/ResultScene.test.ts` mocks Phaser's `Text` without
     `setName`, unlike the two declared test files. An earlier iteration of this
     work called `setName` on the panel's text objects and needed a six-line mock
     parity fix there; that would have expanded the declared file set, so the
     implementation was changed to identify the panel's objects by its single
     declared container instead (the other mechanism D-1 authorizes). This file is
     therefore untouched, and the mock gap is reported only so a future task that
     wants `setName` knows it exists.

L-3  `phase5b.rawEventLogIsNotRendered` is evaluated in phase 5b, where the
     harness has presented no events yet (`presentedEventCount: 0`), so on the
     browser side its narrowing is observed as "the panel's own objects are
     excluded" rather than against populated rows. Moving the check is not
     authorized by D-1. The same property is exercised with a populated panel at
     the unit level (the scene test asserting the panel quotes the delivered raw
     event line while every non-log line does not, and the narrowed TASK-218B relic
     assertion).

L-4  In the DEV build the DEV-only React `Runtime Status` card overlaps the
     panel's right-hand columns. It is a DOM diagnostic, not a Phaser HUD line or a
     cast control — the panel is clear of every Phaser line and control, inside the
     24 px safe area, and the harness's own player-facing frame hides both React
     cards for the captured screenshot. Reported as a visual caveat of two DEV
     surfaces, not as a layout violation.

L-5  The production bundle retains the null-safe glue only (the `developerMoveLog`
     and `moveLogRows` field names and the `recordPhaseStarted` / `recordAbandoned`
     / `syncBattle` / `beginBatch` property names); the panel, its factory and
     every literal of its vocabulary are absent, and with `import.meta.env.DEV`
     false no row is ever recorded.

L-6  This host's smoke suite is unstable in its game-time waits (see §Browser E2E):
     the failures move between invocations, reproduce on the HEAD baseline without
     any Move Log code, and match the class of failure TASK-252's record documents
     at HEAD. The suite did pass both runs cleanly once the machine was otherwise
     idle (runs 5 and 12).
```

### Lifecycle Status

- Lifecycle: `READY` → `IN PROGRESS` (manifest moved `tasks/backlog/` →
  `tasks/active/` at pickup) → `IN REVIEW` (implementation and tests complete) →
  `DONE` (this record, after the Phase A and Phase B commits).
- Terminal state per the manifest's "After TASK-253 reaches its prescribed terminal
  state, stop": this task does not touch TASK-252, TASK-248 or any other task, and
  no follow-up task is started here.

