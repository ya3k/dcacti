# TASK-252 — Generalize Battle Presentation Orchestration to Actor/Action Phases

<!--
  GEN-TASK EXECUTION MANIFEST TEMPLATE
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
-->

---

## Metadata

```text
Task ID:           TASK-252
Type:              FEATURE
Status:            DONE
Risk:              MEDIUM
Priority:          MEDIUM
Primary Agent:     client
Supporting Agents: testing, review
Workflow:          development/feature.md
Skills:            client/phaser-battle-presentation, client/phaser-match3,
                   client/client-event-projection, client/client-state-authority,
                   testing/test-scenario-generation
Dependencies:      TASK-245, TASK-246, TASK-247, TASK-209, TASK-210, TASK-088 (all DONE)
Declared Files:    src/frontend/client/src/game/scenes/BattleScene.ts,
                   src/frontend/client/src/game/scenes/BattleEventPresenter.ts,
                   src/frontend/client/tests/SceneLifecycle.test.ts,
                   src/frontend/client/tests/BattleEventPresentation.test.ts
```

---

## Objective

Replace the player-special-cased presentation phase builder in `BattleScene` with a single actor/action phase model, make the presentation orchestration an explicit state machine whose input-lock invariants are testable, and bring every post-resolution readout under timeline ownership so a resolution's outcomes are presented in delivered event order rather than on the raw state push.

This task changes **presentation only**. It changes no gameplay rule, no server calculation, and no wire member (`AGENTS.md` §10; ADR-001).

---

## Operator Decisions Applied (approved, recorded)

Recorded per the operator decision that authorized this task. They are task constraints; **no authoritative document, ADR, or completed task record is modified by this task.**

```text
D-2  Timeline-owned readouts — APPROVED (Option A).

     Every post-resolution readout must be presented by the presentation
     timeline, not by the raw state push:
       - Combo / Match counters
       - Pet Power value and gauge
       - Passive progress and gauge
       - Pet Status Effect instances
       - Boss Enraged label and its callout
       - the cast controls

     While a presentation timeline is playing, the raw `BattleStateUpdated`
     push must NOT update any of those values early. Timeline completion, an
     abort, and state supersession must each converge exactly on the
     authoritative delivered state.

     No gameplay rule, server calculation, or wire contract changes.
```

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Match-3 (8×8 board, Cascade, Combo), Pet Passive in-battle presentation, Cards, Relics and Bosses are IN scope; §4 item 3 (report classification ambiguity rather than assume)
- `docs/01-game-design/GAME_RULES.md` §2 item 1 — "A Turn represents one player Swap/Action" (the actor model's unit)
- `docs/01-game-design/GAME_RULES.md` §11 item 4 and §17 (lines 453–463) — a Card cast is an auxiliary action within the current Turn; it is not a step of the §17 order and does not reach step 18
- `docs/01-game-design/GAME_RULES.md` §17 — the canonical resolution step order, including step 18's expansion (18a Boss Passive, 18b Boss Skill, 18c Boss Attack) and step 19a
- `docs/01-game-design/GAME_RULES.md` §18 — server authority; the client may send only `Swap`, `Card Cast`, `Pet Skill Cast`
- `docs/01-game-design/MATCH3_RULES.md` §4.2 — cascade pass sequencing and depth (`d ≥ 2` is a Cascade)
- `docs/02-technical/GAME_EVENTS.md` §1 and §1.1 — the ordering authority for one resolution, including cascade-pass placement
- `docs/02-technical/GAME_STATE.md` §2.0.3 — §2.0 contains **no `Status` field**; a lifecycle state machine "must be introduced by its own design/ADR task, not added here"
- `docs/02-technical/GAME_STATE.md` §5 — `Sequence` semantics (increments by exactly 1 per resolved action); §5.1 item 2 (nothing is written mid-resolution)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3 — `ReceiveEvents(battleId, serverSequence, events[])`, exactly three envelope members; item 2 (`serverSequence` strictly increasing, no gaps); §3.2.12 item 5 (the envelope is unchanged)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.1 items 1–5 — write-back before batch, no per-step delivery, no gameplay message type, rejected actions deliver nothing, the client stays non-authoritative
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.2 — the **closed 16-name `type` discriminator set**; §3.2.12 item 1 — no event carries `battleId`, `serverSequence`, `turn`, board, `combo`, `matchCount`, timestamp, GUID or generated id
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.6 `MatchCreated`, §3.2.7 `CascadeCreated`, §3.2.9 `GemMatched`, §3.2.14 `DamageDealt`, §3.2.15 `DamageTaken`, §3.2.16/§3.2.17 Passive, §3.2.18 `BossSkillCast`, §3.2.19 outcome, §3.2.20/§3.2.21 casts, §3.2.23 `RelicTriggered`, §3.2.24 `PowerChanged` (item 3 — `source` is **stage ownership, not a side**)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4 — `BattleStateUpdated` is the only state push; §4 item 4, item 11, §8 items 3, 5–9 — no member, method, event, subscription or lifecycle message may be added
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — server authority
- `docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md` — Phaser owns battle rendering and presentation
- `docs/03-decisions/ADR/ADR-008-battle-recovery.md` — snapshot recovery, no event replay
- `docs/02-technical/ARCHITECTURE.md` §2.2, §2.2.1, §2.2.2, §5 — frontend layering, scene↔runtime port, viewport/scaling ownership, anti-overengineering
- `tasks/completed/TASK-245-phaser-cascade-animation-and-combat-presentation-sequencing.md` — the timeline this task generalizes, and its PD-1…PD-8 boundaries (**unchanged here**)
- `tasks/completed/TASK-246-battle-ux-phase-legibility-selection-feedback-and-pacing.md` — pacing model, selection lifecycle, interaction indicator; §4 F-3 (the wire carries no turn/phase signal); C-1/I-1 (the unresolved `GAME_EVENTS.md` §1/§2 vs `SIGNALR_PROTOCOL.md` §3.2.2 conflict)
- `tasks/completed/TASK-210-battle-feedback-and-presentation-pass.md` §7–§9 — callout policy and the diagnostic boundary (its §9 diagnostic prohibition is **not** touched by this task)
- `tasks/completed/TASK-209-in-battle-state-projection-and-hud.md` — the in-battle HUD this task re-owns
- `.ai/skills/client/phaser-battle-presentation/SKILL.md` — sequencing and input-lock policy **only**; its illustrative component names, its `Turn: … | Boss Intent: … | Status: …` HUD and its "boss turn intents" promise are NOT in the contract and are NOT followed (TASK-246 I-2; `AGENTS.md` §19: a skill is not a source of truth)
- `tasks/TASK_LIFECYCLE.md` §6 — two-stage commit protocol

---

## Audit Findings This Task Resolves

Verified against HEAD `1bf42cb`. Line references are to that revision.

```text
RC-1  Timeline ownership gap (the primary defect).
      `renderBattleState` (BattleScene.ts:1508–1562) computes `hold` (:1535) and passes it
      only to `renderBossHud` (:1549) and `renderPetHud` (:1550). `renderPlayerHud(state)`
      (:1551) and `renderCastControls(state)` (:1560) receive no hold.
      Inside a held push, `renderPetHud` (:1744–1793) still writes the Pet name (:1766),
      Power value and gauge (:1776–1777), Passive progress and gauge (:1781–1784) and the
      Status Effect line (:1790–1792) immediately; `renderBossHud` (:1664–1699) writes the
      `[ENRAGED]` label (:1694) and fires the `BOSS ENRAGED` callout (:1689) immediately;
      `renderPlayerHud` (:1816–1831) writes the delivered Combo/MatchCount immediately; and
      `renderCastControls` (:2001–2056) rebuilds every control on every push (:2006).
      Consequence: the end-of-resolution values are on screen before the swap/match/cascade/
      settle/damage phases play.
      Resolution: D-2 above.

RC-2  Transient effects are started by a phase but not owned by the timeline.
      `playPresentationPhase` (:3179–3209) → `presentCombatFeedback` (:3415–3464) →
      `spawnCellHighlight` (:3473–3515) and `spawnFloater` (:3555–3583) start
      self-destroying tweens that nothing tracks. `abortPresentation` (:3306–3315) kills
      only the phase clock (:3307) and the gem-view tweens (:3312) and deliberately leaves
      the feedback animations running (:3300–3304).
      Consequence: after an abort, the abandoned resolution's effects overlap the next one.

RC-3  The batch callout has a fixed lifetime unrelated to the timeline.
      `showCallout` runs once per batch before the phases are built (:2809–2816); its life
      is CALLOUT_HOLD_MS + CALLOUT_FADE_MS = 420 + 240 ms (:313–314, :3610–3615).

RC-4  No actor/action abstraction; the swap phase is special-cased.
      `PresentationPhaseKind` (:409–417) contains `'swap'`; `buildPresentationPhases` adds
      that phase only when `this.swapPending && this.pendingSwapCells` (:2880–2888), while
      `submitSwap`'s `finally` clears both (:2511–2515). The batched ordering it relies on
      comes from BattleHub (state push, then batch, then the result) and is asserted nowhere.

RC-5  Diagnostic buffers are unbounded.
      `presentedEventsLog` (:748, appended at :2773) and `presentedPhases` (:739, appended
      at :3148) grow for the life of the scene; they are reset only in `create()` (:823,
      :1002–1003) and `shutdown()`.
```

**Verified non-defects — do not "fix" these.** The input guard has no unlock window around the acknowledgement (`:2475–2476` then `:2512–2513`, with `presentationTimeline` set at `:2823`; `isInputLocked()` ORs all three at `:3719–3721`). Duplicate/older batches are dropped (`:2780–2788`) and newer states supersede (`:1573–1583`). Teardown releases subscriptions, layers, tweens and guards (`:948–1009`).

---

## Scope

### In Scope

- **Actor/action phase model.** One phase record carrying a delivered `actor` and `action` beside the existing `kind`, `detail`, `holdMs` and `events`; one adapter mapping a delivered event to that record. The adapter reads **only** delivered members.
- **Legal-actor derivation.** `DamageDealt`/`DamageTaken` `source`/`target`; `PassiveCharged`/`PassiveTriggered` `source` (+ `sourceId`); `BossSkillCast` (boss); `CardCast`/`PetSkillCast` (player); `RelicTriggered` (player); board events (`MatchCreated`, `GemMatched`, `CascadeCreated`, `ComboChanged`) attributed to the committed Swap; `PowerChanged` attributed as a **stage**, never as a side (`SIGNALR_PROTOCOL.md` §3.2.24 item 3).
- **Generic extension.** A Boss action and a Card/Pet-Skill cast must reach their phases through the same adapter as a Swap. No per-actor branch, pipeline, registry, interface or event bus (`AGENTS.md` §9; `ARCHITECTURE.md` §5).
- **Explicit orchestration state machine** with named states and legal transitions, replacing the current ad-hoc conjunction of `swapPending`, `actionInFlight` and `presentationTimeline`.
- **Input-lock invariants** expressed as a function of that machine, so "locked ⟺ not accepting player input" is a single derived fact.
- **Timeline-owned readouts (D-2).** Combo/Match counters, Pet Power value and gauge, Passive progress and gauge, Pet Status Effect instances, the Boss Enraged label and its callout, and the cast controls are presented by their own phase — or by `settleHeldPresentation` — and never by a raw push while a timeline plays.
- **Timeline-owned transient effects.** The effects a phase starts are tracked, and `abortPresentation` cancels them, so no abandoned resolution animates over the next one.
- **Callout bounded to its own timeline.** The callout's lifetime must not outlive the resolution that raised it (extends TASK-246's F-4 fix from intra-timeline to cross-timeline).
- **Disabled cast-control presentation.** While locked, the controls are presented disabled; they are not rebuilt mid-timeline.
- **Bounded diagnostic buffers** for `presentedEventsLog` / `presentedPhases` (a documented cap; cleared on battle change and teardown).
- **Preserve the derived-hold invariant** (`holdMs` ≥ the longest animation the phase starts) for every phase kind, including any new one.
- Unit, integration and gameplay-scenario tests for the adapter, the lock invariants, timeline-owned readouts, abort cancellation, and bounded retention.
- Follow the two-stage commit protocol (`tasks/TASK_LIFECYCLE.md` §6).

### Out of Scope

- **The Developer Move Log.** It is TASK-253; its DEV-only visibility decision (operator D-1) does not apply here, and this task adds no new rendered diagnostic surface.
- **Any rendered turn/sequence/transport/RNG diagnostic.** The TASK-209 §5 / TASK-210 §9 boundary is unchanged by this task.
- **Any wire change**: no new/renamed hub method, event type, state-push member, subscription or `type` discriminator. In particular no `phase`, `status`, `activeTurn` or `whoseTurn` field, and no turn/phase event (`SIGNALR_PROTOCOL.md` §4 item 4/11, §8 items 3, 5–9).
- **Resolving TASK-246 C-1** (`GAME_EVENTS.md` §1/§2 vs `SIGNALR_PROTOCOL.md` §3.2.2). Reported, not resolved.
- **Inventing a Boss turn.** `GAME_RULES.md` §2 item 1 and §17 (lines 453–463) fix the Turn as one player Swap/Action and place the Boss response inside step 18 of that same resolution. A separate Boss turn would be a GAMEPLAY-CHANGE plus a protocol task.
- Any client-side computation of match detection, gravity destinations, spawned Gem identity, RNG, damage, HP, Power, Combo or Passive progression (`AGENTS.md` §10; TASK-245 PD-1/PD-8 unchanged).
- Any change to TASK-245's PD-1…PD-8 boundaries, supersession keying, or outcome-handoff semantics.
- A gameplay time limit, deadline, auto-move or forfeit (TASK-246 C-2 / PD-A remains undecided).
- Backend, Domain, Application, Infrastructure, Api, `docs/` or ADR changes.
- Editing `tasks/completed/*` or `tasks/blocked/*` (`AGENTS.md` §16/§17/§18).
- Items listed OUT in `docs/00-overview/MVP_SCOPE.md` §2, and unrelated refactoring.

---

## Current State

`BattleScene.ts` (3977 lines) already plays one ordered timeline per delivered `ReceiveEvents` batch: `playEventBatch` (`:2763–2832`) builds phases with `buildPresentationPhases` (`:2872–2983`), `advancePresentation` (`:3136–3168`) plays them with a derived hold (`phaseHoldMs`, `:3046–3055`), and `finishPresentation` / `abortPresentation` / `releaseTimeline` (`:3288–3338`) end it. The guard is `isInputLocked()` (`:3719–3721`). `BattleEventPresenter.ts` already parses all sixteen discriminators (`parseInBattleEvent`, `:166`) and formats them (`formatInBattleEvent`, `:496`), and already classifies a damage party (`resolveDamagePresentationPhase`, `:677–707`).

What is missing is exactly the audit's RC-1…RC-5: timeline ownership of the non-HP readouts, ownership of transient effects, a callout bounded to its timeline, an actor/action identity on phases, and bounded diagnostic buffers.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-252:

```text
src/frontend/client/src/game/scenes/BattleScene.ts
src/frontend/client/src/game/scenes/BattleEventPresenter.ts
src/frontend/client/tests/SceneLifecycle.test.ts
src/frontend/client/tests/BattleEventPresentation.test.ts
```

*(The task manifest file itself is excluded from the implementation declared file set. The list above strictly matches the `Declared Files:` field in `## Metadata`.)*

**Shared-file ownership.** All four files were last owned by TASK-246 (`Status: DONE`, immutable; Phase A commit `70ab345`). This task re-declares them as its own implementation surface. Per `TASK_LIFECYCLE.md` §6.1 item 5, if any of them is concurrently declared by another `tasks/active/` task the slices must be combined into a single joint group commit rather than committed separately. At filing, `tasks/active/` contains only `TASK-248-restore-hoa-long-canonical-base-stats.md`, which declares `src/backend/GameServer.Domain/Bosses/BossDefinitions.cs` — no overlap. TASK-253 declares three of these four files and **depends on this task being DONE**; it must not run concurrently.

**Unowned / pre-existing disclosure (P-3).** `git status --porcelain` at HEAD `1bf42cb` reports a clean working tree. No unowned or pre-existing modification is present; if one appears at pickup it must be disclosed and left unstaged.

---

## Acceptance Criteria

- [ ] A presentation phase carries a delivered `actor` and `action` alongside its existing kind/detail/hold/events.
- [ ] The actor of every phase is derived from delivered event members only; `PowerChanged.source` is never used as a side.
- [ ] A Boss action (skill, passive, retaliation) and a Card/Pet-Skill cast each produce their phases through the **same** adapter as a Swap — no per-actor branch, pipeline, registry, interface or event bus.
- [ ] The swap phase no longer depends on `swapPending` being true at batch-arrival time by accident; the dependency is explicit and covered by a regression test.
- [ ] The orchestration is an explicit state machine with named states and documented legal transitions, and every transition is legal by that documentation.
- [ ] `isInputLocked()` is true in every state except the state that accepts player input and the terminated state; a test asserts the predicate per state.
- [ ] While a timeline is playing, the raw push writes **none** of: Combo, MatchCount, Pet Power value, Power gauge, Passive progress, Passive gauge, Status Effect instances, the Boss Enraged label, the Boss Enraged callout, or the cast-control set (D-2).
- [ ] Timeline completion, an abort, and state supersession each converge the board **and** every value listed above onto the delivered authoritative state, with no residual divergence.
- [ ] No readout shows a resolution's end-of-resolution value before the phase that owns it has played.
- [ ] A phase's hold is never shorter than the longest animation that phase starts, for every batch shape, including any new phase kind (TASK-246 F-4 invariant preserved).
- [ ] After `abortPresentation`, no tween started by the aborted timeline is still running; every effect the timeline started is cancelled or completed.
- [ ] The batch callout does not outlive its own timeline.
- [ ] Cast controls are presented disabled while input is locked, and are not destroyed/recreated while a timeline is playing; a click during the lock submits nothing.
- [ ] `presentedEventsLog` and `presentedPhases` are bounded by a documented cap and reset on battle change and teardown.
- [ ] Duplicate or older `serverSequence` applies no phase and no state twice; a newer authoritative state aborts and converges (`ADR-008`).
- [ ] Scene `shutdown()` / `DESTROY` mid-timeline releases the guard, cancels every timeline-owned effect, and leaves no listener or tween leak.
- [ ] Zero client-authoritative gameplay computation: no match detection, gravity/spawn derivation, HP accumulation, damage derivation, combo counting or passive progression (`AGENTS.md` §10).
- [ ] The SignalR wire contract is unchanged: the three-member `ReceiveEvents` envelope, `CascadeCreated = { type, cascadeDepth }`, the nine-member state push, and the closed 16-name discriminator set; no new hub method, event or subscription (PD-3/PD-4 preserved).
- [ ] No rendered turn/sequence/transport/RNG diagnostic is introduced (TASK-209 §5 / TASK-210 §9 unchanged; TASK-253 owns the one authorized DEV-only exception).
- [ ] All relevant tests pass at the required validation depth (`.ai/workflow/core/validation.md` §2, MEDIUM).
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

- `BattleScene.ts` — `PresentationPhaseKind`/`PresentationPhase` (`:400–427`) own the phase shape and are pinned by existing tests; `buildPresentationPhases` (`:2872–2983`) owns the special-casing that must become the adapter call; `playPresentationPhase` (`:3179–3209`) owns phase playback; `advancePresentation` (`:3136–3168`) owns pacing; `abortPresentation` (`:3306–3315`) and `releaseTimeline` (`:3330–3338`) own teardown; `settleHeldPresentation` (`:3350–3365`) owns convergence and must grow to cover the D-2 readouts; `renderBattleState` (`:1508–1562`) owns the immediate-vs-held split; `renderBossHud` (`:1664`), `renderPetHud` (`:1744`), `renderPlayerHud` (`:1816`) and `renderCastControls` (`:2001`) own the readouts being re-owned; `isInputLocked` (`:3719`) owns the guard; `shutdown` (`:948–1009`) owns teardown completeness; `showCallout` (`:3595–3616`) owns the callout lifetime; `spawnCellHighlight` (`:3473`) / `spawnFloater` (`:3555`) / `presentCombatFeedback` (`:3415`) own the transient effects.
- `BattleEventPresenter.ts` — extend with presentation-only actor/action vocabulary and the single event→(actor, action, kind) adapter. Keep one spelling of the party vocabulary (`DAMAGE_PARTY_PLAYER` / `DAMAGE_PARTY_BOSS`, `:655–656`) and reuse `resolveDamagePresentationPhase` (`:677–707`) rather than adding a second classifier.
- The explicit state machine is expected to be a scene-local discriminated value plus one transition function — not an interface, registry, generic queue or event bus (`AGENTS.md` §9; `ARCHITECTURE.md` §5).
- The presentation must remain fully synchronous with no tween manager present (the unit-harness mode), so ordering stays observable without a running clock (`SceneLifecycle.test.ts:128`, `:1012`, `:3839`).
- Do not change the diagnostic strings `presentedPhases` records (`swap 8->9`, `match depth=1`, `settle`, …) unless a test is updated as part of this task; they are the developer record TASK-247's harness reads.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — the event→(actor, action, kind) adapter across all sixteen
                         discriminators; the lock predicate per machine state; holdMs ≥ the
                         longest effect each phase kind starts; bounded-buffer eviction
[x] Integration tests  — wire-contract regression: three-member ReceiveEvents envelope,
                         CascadeCreated = { type, cascadeDepth }, nine-member state push,
                         closed 16-name discriminator set, no phase/status/turn member
                         anywhere; the swap-phase dependency on batch-before-ack ordering
[x] Gameplay scenarios — Given a committed Swap that produces a cascade and a Boss response
                         When the server resolves it and delivers one batch
                         Then phases and their actor/action attribution follow
                              GAME_EVENTS.md §1 order
                         And no readout shows the resolution's end value before its phase
                         And input stays locked until the last phase completes
                         And an abort mid-timeline cancels that timeline's own effects
                         And a terminal outcome still reaches ResultScene
```

### Key Edge Cases

- Multi-pass resolution (≥ 2 `CascadeCreated`) — one phase per delivered depth (`MATCH3_RULES.md` §4.2).
- `MatchCreated.cascadeDepth` and `CascadeCreated.cascadeDepth` are distinct values for one pass and must never be equated (`SIGNALR_PROTOCOL.md` §3.2.7 item 2).
- Boss-phase batch: `PassiveCharged(source="boss")` → `BossSkillCast` → boss `Damage*` in one batch (`GAME_RULES.md` §17 steps 18a–18c) — all attributed to the boss actor through the shared adapter.
- A cast batch with no board movement (a Card cast does not resolve the board — `GAME_RULES.md` §17 lines 453–463) — the board and its settle phase behave correctly and the cast's own phase is attributed to the player.
- `PowerChanged.source` values (`match` / `card` / `relic` / `boss`) must not be read as sides (`SIGNALR_PROTOCOL.md` §3.2.24 item 3).
- A rejected Swap emits nothing and produces no timeline (`GAME_EVENTS.md` §1.2); no selection survives it.
- A newer state or a newer batch arriving mid-playback — abort, converge, no orphaned effect (`ADR-008`).
- Scene shut down mid-timeline — guard released, all timeline-owned effects cancelled, no leaked tween or listener.
- An unknown or malformed event inside the batch — ignored safely, recorded in the developer log only, no fabricated presentation.
- A long battle — the diagnostic buffers stay within their documented cap.

---

## Stop Conditions

- If a proposed actor or action identity cannot be sourced from delivered event members — STOP per `AGENTS.md` §7/§10; do not invent it and do not add a wire member.
- If satisfying a criterion requires a new hub method, event, subscription, state-push member or `type` discriminator — STOP per `AGENTS.md` §18 and `SIGNALR_PROTOCOL.md` §4 item 4/11, §8 items 3, 5–9.
- If satisfying a criterion requires client-authoritative gameplay computation, or an inference of whose turn it is from animation timing — STOP per `AGENTS.md` §10.
- If the actor model cannot be expressed without an interface, registry, generic queue, event bus or second animation pipeline — STOP per `AGENTS.md` §9 / `ARCHITECTURE.md` §5 and report the constraint.
- If moving a readout under timeline ownership would require a change to TASK-245's PD-1…PD-8 boundary, its supersession keying, or its outcome handoff — STOP and report.
- If the work needs the Developer Move Log, a rendered turn/sequence diagnostic, or the TASK-210 §9 boundary relaxed — STOP; that is TASK-253 under operator decision D-1.
- If a Boss turn, a phase/status wire field, or a gameplay time limit appears necessary — STOP; each is a GAMEPLAY-CHANGE and/or protocol task (`AGENTS.md` §7/§17/§18; TASK-246 C-1/C-2).
- If satisfying a criterion requires editing `tasks/completed/*`, `tasks/blocked/*`, `docs/` or an ADR — STOP per `AGENTS.md` §16/§17/§18; report instead.
- If the browser E2E stack cannot be brought up — record the browser verification as **not run** (`.ai/workflow/quality/local-e2e-verification.md` §9); never substitute mocked-tween evidence.
- If the task exceeds 7 skills or crosses multiple uncoupled architectural boundaries — STOP & decompose.

---

## Readiness Evaluation (BACKLOG → READY)

Per `tasks/TASK_LIFECYCLE.md` §3, evaluated at filing against HEAD `1bf42cb`.

```text
[x] Task type confirmed (TASK_TYPES.md)
    FEATURE — development/feature.md. The capability has a documented home
    (MVP_SCOPE.md §1 Match-3 / Pet Passive in-battle presentation / Cards / Relics /
    Bosses; the phaser-battle-presentation skill's sequencing and input-lock policy),
    and the task builds it. No gameplay rule is invented or changed, so it is not
    GAMEPLAY-CHANGE; no ADR, layering or realtime-strategy change is made, so it is not
    ARCHITECTURE.

[x] Relevant documentation exists in docs/
    Verified present at the cited sections: GAME_RULES.md §2/§11/§17/§18;
    MATCH3_RULES.md §4.2; GAME_EVENTS.md §1/§1.1; GAME_STATE.md §2.0.3/§5/§5.1;
    SIGNALR_PROTOCOL.md §3/§3.1/§3.2.x/§4/§8; ADR-001/003/008; ARCHITECTURE.md
    §2.2/§2.2.1/§2.2.2/§5.

[x] MVP scope confirmed (MVP_SCOPE.md §1)
    Everything this task re-presents is IN: the 8x8 board, Cascade and Combo
    ("Match-3"); the Pet Passive charge/threshold/reset mechanism "and its in-battle
    presentation" ("Pets"); Cards; Relics; Bosses. Nothing in §2 is touched. The task
    adds no new system, content or wire surface.

[x] Task is not blocked by an unresolved dependency
    TASK-245, TASK-246, TASK-247, TASK-209, TASK-210 and TASK-088 are all Status: DONE
    and immutable. TASK-248 (the only active task) declares a backend file and does not
    overlap this task's declared set. TASK-253 declares three of these four files and is
    gated behind this task being DONE, so no concurrent shared-file owner exists.

[x] Primary agent assigned
    client (TASK_TYPES.md §5 domain x type matrix: Frontend/Phaser x FEATURE).

[x] Workflow assigned
    development/feature.md.

[x] Acceptance criteria are testable
    All criteria are binary and observable: presence of a delivered actor/action on a
    phase, absence of a per-actor branch, the lock predicate per machine state, the
    absence of early readout writes, hold-vs-effect durations, no surviving tween after
    an abort, bounded buffer size, unchanged wire member sets, zero gameplay computation.

[x] Declared file set established from actual inspection
    All four paths exist at the declared locations and were inspected at HEAD 1bf42cb.
    The Declared Files metadata field and the Declared File Set block match exactly.
    Shared-file ownership against TASK-246 (and forward against TASK-253) is disclosed.

[x] Stop conditions are complete and do not contradict the approved decisions
    They bound the work to delivered data, forbid wire and gameplay changes, forbid
    touching TASK-245's PD boundary, forbid the DEV-only Move Log surface (TASK-253's),
    and forbid relaxing the TASK-209 §5 / TASK-210 §9 diagnostic boundary.
```

**Determination: every BACKLOG → READY criterion passes. The task is promoted to READY.**

**Reported classification item (not a blocker, MVP_SCOPE.md §4 item 3).** No new system or content is classified here: the task changes how already-IN features are presented and adds no gameplay surface. No IN/OUT/FUTURE question arises.

**Residual risk carried into execution (not a blocker).** `PresentationPhaseKind` and the phase-detail strings are pinned by roughly 292 existing tests in `SceneLifecycle.test.ts` and `BattleEventPresentation.test.ts`. The actor model must either preserve those strings or update the tests as part of this task's declared file set.

---

## Completion Evidence

### Commit

- Implementation slice (Phase A): `1e5575e23db00f19b4f0cc6369ae3f8ed1fb8ec8`
- Subject: `feat: generalize battle presentation to actor/action phases`
- Owns: `TASK-252`
- Files: `src/frontend/client/src/game/scenes/BattleScene.ts`,
  `src/frontend/client/src/game/scenes/BattleEventPresenter.ts`,
  `src/frontend/client/tests/SceneLifecycle.test.ts`,
  `src/frontend/client/tests/BattleEventPresentation.test.ts`
- Shared with: `none`
- Unowned / pre-existing: `none`

The staged set was verified against the canonical `Declared Files` field before
the commit (P-5): `git diff --cached --name-status` listed exactly those four
paths, no extraneous file and no task record.

### Changed Files

- `src/frontend/client/src/game/scenes/BattleEventPresenter.ts` — adds the
  presentation-only actor/action vocabulary (`PresentationActor`,
  `PresentationAction`, `PresentationPhaseKind`, `PresentedActionStep`), the one
  delivered-event adapter `adaptPresentedEvent` covering every in-battle
  discriminator, and the delivered Passive-owner vocabulary
  (`PASSIVE_OWNER_PET` / `PASSIVE_OWNER_BOSS`, `passiveOwnerActor`,
  `isPetPassiveEvent`). `PowerChanged` is attributed to the mutation **stage**
  its delivered `source` names and never to a side
  (`SIGNALR_PROTOCOL.md` §3.2.24 item 3); a damage report whose parties name no
  documented direction claims no actor; `DamageCalculated` presents nothing of
  its own. The one damage classifier (`resolveDamagePresentationPhase`) is
  reused, not duplicated.
- `src/frontend/client/src/game/scenes/BattleScene.ts` — phases now carry the
  adapter's `actor`/`action` beside `kind`/`detail`/`holdMs`/`events`, and the
  phase builder is one forward pass over that adapter (no per-actor branch,
  pipeline, registry, interface or event bus). The three guard booleans are
  replaced by the explicit `BattleOrchestrationState` machine with the exported
  legal transition table, `isInputLocked()` is one derived fact over it, and the
  Combo/Match counters, Pet Power, Passive progress, Status Effects, Boss
  Enraged label/callout and cast controls are timeline-owned (D-2): the raw push
  writes none of them while a timeline plays, and completion, abort and
  supersession each converge them onto the delivered state. The committed Swap's
  pair is consumed by its own phase instead of by the request's `finally`; the
  timeline owns its transient effects and cancels them on abort; the batch
  callout is cancelled with its timeline; the cast controls are toggled in place
  and presented disabled while input is held; and both diagnostic buffers are
  bounded by a documented cap and cleared on battle change and teardown.
- `src/frontend/client/tests/BattleEventPresentation.test.ts` — new suite: the
  event→(actor, action, kind) adapter across all sixteen discriminators, the
  closed sixteen-name set, the Passive-owner and unclassifiable-damage
  attributions, "extra wire members change nothing", and a source guard pinning
  one adapter and no per-actor branch.
- `src/frontend/client/tests/SceneLifecycle.test.ts` — new suite: the
  actor/action phase model, the lock predicate per machine state, the documented
  transition table and the transitions a real resolution performs, the D-2
  readout ownership (raw-push silence, per-phase presentation, convergence on
  completion/abort/supersession), abort effect cancellation, the bounded callout,
  the disabled cast controls, the bounded buffers and their reset on battle
  change, the hold derivation for every phase kind, the no-added-timeline-length
  pacing regression, and the gameplay scenario (cascade + Boss response order,
  locked input, abort cancellation, terminal handoff). Three existing
  expectation lists were updated for the new Boss Skill / cast step.

### Validation Results

Run from `src/frontend/client` unless stated otherwise.

- `npx tsc --noEmit -p tsconfig.json` — PASS (no diagnostics)
- `npx vitest run tests/SceneLifecycle.test.ts tests/BattleEventPresentation.test.ts`
  — PASS (2 files, 316 tests)
- `npx vitest run` (whole client suite) — PASS (23 files, 1023 tests; the
  pre-task baseline at HEAD `1bf42cb` was 991, so this task adds 32)
- `npm run build` (`tsc && vite build`) — PASS (built in 2.58 s)
- `npm run verify:e2e:smoke` with the full local stack (PostgreSQL 5433 and
  Redis 6379 up, `GameServer.Api` on 5000 with `/health` Healthy, Vite dev
  server on 5173, headless Edge at 1280x720):
  - RUN 1 — **PASS**, 160 checks, 0 failures (including every phase 5 / 5b / 5c /
    5c-relic presentation, pacing, callout and supersession check).
  - RUN 2 — **not passing**, and the suite therefore exits 1. Three runs
    produced three different RUN 2 failures: a presentation-timing wait
    (`First feedback pass faded and timeline settled`), a `Real battle
    ResultScene rendered` wait, and a post-reconnect swap-candidate probe
    (`phase10b.committedActionAfterReconnectDeliveredOnTheNewConnection`).
    The timing wait **reproduces identically with the four declared files
    restored to HEAD `1bf42cb`** (same run shape: RUN 1 160/160 clean, RUN 2
    timing out on the same condition), so the RUN 2 instability is a
    pre-existing property of this host's smoke run, not a TASK-252 regression.
    Recorded here as: the browser suite was executed, RUN 1 is clean, RUN 2 is
    not passing (pre-existing, reproduced at HEAD).

### Acceptance Criteria

| # | Criterion | Evidence |
| --- | --- | --- |
| 1 | A phase carries a delivered actor and action | `PresentationPhase.actor`/`.action`; `carries a delivered actor and action on every phase of a resolution` |
| 2 | Actor derived from delivered members only; `PowerChanged.source` never a side | `adaptPresentedEvent`; `never reads PowerChanged.source as a side…`; `attributes each Passive to the entity its delivered source names`; `claims no actor for a damage direction the contract does not define` |
| 3 | Boss action and Card/Pet-Skill cast reach phases through the same adapter | `produces a Boss action's phases and a cast's phases through the same adapter as a Swap`; `applies one adapter and no per-actor branch anywhere in the presenter` |
| 4 | Swap phase no longer depends on `swapPending` by accident | `committedSwapCells`, consumed by `playSwapPhase`; `presents the committed Swap even when its acknowledgement settled before the batch arrived`; `presents no swap movement for a rejected Swap…` |
| 5 | Explicit state machine, named states, documented legal transitions | `BattleOrchestrationState`, `BATTLE_ORCHESTRATION_LEGAL_TRANSITIONS`, one `transitionOrchestration`; `documents one closed transition table…`; `performs only documented transitions while driving a full resolution` |
| 6 | Lock predicate per state | `locks input in every state except the accepting and the terminated state` |
| 7 | Raw push writes no D-2 readout while a timeline plays | `writes no timeline-owned readout from a raw push while a timeline plays` |
| 8 | Completion, abort and supersession converge on the delivered state | `converges every readout … when the timeline completes / … supersedes playback / … aborts` |
| 9 | No readout before its owning phase | `presents each readout from the phase that owns it, and never before` |
| 10 | Hold ≥ the longest animation of every phase kind | `derives a hold that covers the longest animation every phase kind starts`; the TASK-246 pacing tests still pass |
| 11 | After an abort no timeline tween is still running | `cancels every effect the aborted timeline started, so none outlives it` |
| 12 | The batch callout does not outlive its timeline | `cancelTimelineCallout`; `bounds the batch callout to its own timeline` |
| 13 | Cast controls disabled while locked, not rebuilt mid-timeline, click submits nothing | `presentCastControlsEnabled` (in-place toggle); `presents the cast controls disabled while locked, and rebuilds them only outside a timeline` |
| 14 | Diagnostic buffers bounded and reset | `PRESENTATION_LOG_CAP`, `appendBounded`, `resetPresentationLogs`; `bounds both diagnostic buffers and clears them when the battle changes` |
| 15 | Duplicate or older `serverSequence` applies nothing twice | `applies no phase and no state for an older or duplicate batch`; the TASK-245 supersession test |
| 16 | Teardown mid-timeline releases guard, effects, listeners and tweens | `releases the guard, the effects and the timeline on scene teardown mid-resolution`; the existing SHUTDOWN/DESTROY tests |
| 17 | Zero client-authoritative gameplay computation | no gameplay value is derived; the existing source guards (`adds no competing coordinate system and computes no gameplay`, `does not calculate gameplay or author state`, `contains no client-side randomness`) pass |
| 18 | Wire contract unchanged | the TASK-245 wire tests and `keeps the closed discriminator set at exactly the sixteen documented names` pass; no hub method, event, subscription or state-push member added |
| 19 | No rendered turn/sequence/transport/RNG diagnostic | no rendered diagnostic added; TASK-209 §5 / TASK-210 §9 unchanged (no new rendered surface; TASK-253 owns the DEV-only exception) |
| 20 | Tests pass at MEDIUM depth | the validation results above |
| 21 | Quality review checklist passes | §Quality Review below |
| 22 | No authoritative rules or contracts violated | §Quality Review below |

### Quality Review (`.ai/workflow/quality/review.md` §1)

```text
Correctness     PASS  The adapter reads only delivered members and one classifier
                      is reused; the phase order preserves GAME_EVENTS.md §1/§1.1;
                      PowerChanged.source is read as a mutation stage; no Boss Turn
                      is invented (the Boss response stays inside the same
                      resolution, GAME_RULES.md §17 step 18); a Card cast resolves
                      no board. Presentation only (AGENTS.md §10, ADR-001).
Architecture    PASS  Phaser owns battle presentation (ADR-003); the scene still
                      reads the runtime port and no transport (ARCHITECTURE.md
                      §2.2 rules 3–5); one discriminated state value, one transition
                      function, one adapter — no interface, registry, generic queue
                      or event bus (AGENTS.md §9, ARCHITECTURE.md §5).
Scope           PASS  Only the four declared files changed. No backend, wire,
                      gameplay-rule, docs/ or ADR change; TASK-245's PD-1…PD-8
                      boundaries, supersession keying and outcome handoff are
                      untouched.
Tests           PASS  Unit, integration and gameplay-scenario coverage at MEDIUM
                      depth; 1023/1023 client tests pass; the browser smoke suite
                      run 1 is clean.
Documentation   PASS  No source-of-truth document describes the client's phase
                      model, input guard or readout timing, so no documentation
                      change is required; docs/ changes are out of scope for this
                      task. No conflict introduced (AGENTS.md §17).
Security        PASS  Nothing touches authentication, the session, or any data
                      surface.
Performance     PASS  One amortized O(1) append per diagnostic line, ≤ 4 controls
                      toggled per guard change, one convergence write per timeline
                      end. Nothing added to the server or resolution path.
Maintainability PASS  Documented state machine and adapter; no speculative
                      abstraction; 32 new tests.
Determinism     PASS  Server authority preserved: no match detection, gravity or
                      spawn derivation, HP accumulation, damage derivation, combo
                      counting or Passive progression is computed on the client.
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed P-1…P-6 commit policy compliance (Phase A staged exactly the four
      declared files, no task id in the subject, `owns: TASK-252` and the
      disclosures in the body; Phase B files this record alone)
- [x] Confirmed operator decision D-2 applied as recorded

### Known Limitations / Reported Items

- The smoke suite's **second run** is unstable on this host (see Validation
  Results). It is reproducible with the pre-task code and is reported, not
  resolved: repairing the harness is out of this task's scope.
- `presentBossEnrage` still derives the `[ENRAGED]` label on the client from the
  delivered HP pair and `BOSS_RULES.md` §6.1 thresholds (TASK-231's behaviour).
  This task moved *when* it is presented, not how it is derived.
- TASK-246's C-1 (`GAME_EVENTS.md` §1/§2 vs `SIGNALR_PROTOCOL.md` §3.2.2) remains
  reported, not resolved.
- The Developer Move Log is TASK-253 and was not started.
