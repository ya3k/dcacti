# TASK-254 — Unified Battle Action Presentation Pipeline

<!--
  GEN-TASK EXECUTION MANIFEST TEMPLATE
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
-->

---

## Metadata

```text
Task ID:           TASK-254
Type:              FEATURE
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     client
Supporting Agents: testing, review
Workflow:          development/feature.md
Skills:            client/phaser-battle-presentation, client/client-event-projection,
                   client/client-state-authority, client/phaser-match3,
                   testing/test-scenario-generation, quality/implementation-review
Dependencies:      TASK-252 (DONE), TASK-253 (DONE)
Declared Files:    src/frontend/client/src/game/scenes/BattleScene.ts,
                   src/frontend/client/src/game/scenes/BattleEventPresenter.ts,
                   src/frontend/client/tests/BattleEventPresentation.test.ts,
                   src/frontend/client/tests/SceneLifecycle.test.ts
```

---

## Objective

Give every Player and Boss action **one** presentation lifecycle — **Action → Execution → Board Resolution → Cascades → Combat Effects → Recovery → Next Actor** — so that the delivered step a player is watching is always presented before the value or callout it caused, every effect a step starts completes before a conflicting later step begins, input stays locked from submission through presentation and authoritative reconciliation, and the resolution's final authoritative board is never revealed before the supported intermediate transitions have finished presenting.

The task closes the six audit findings below on the existing single actor/action adapter and orchestration model. It changes **presentation only**: no gameplay rule, no server calculation, no wire member, and no client-authoritative Match-3 computation (`AGENTS.md` §10; ADR-001).

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Match-3 (8×8 board, Cascade, Combo), Pet Passive and its in-battle presentation, Cards, Relics and Bosses are IN scope; §4 items 2–3 — anything absent from §1/§2 is FUTURE by default and the ambiguity must be **reported**, not assumed
- `docs/01-game-design/GAME_RULES.md` §2 item 1 — "A Turn represents one player Swap/Action" (the action model's unit)
- `docs/01-game-design/GAME_RULES.md` §11 item 4 and §17 (lines 453–463) — a Card cast is an auxiliary action within the current Turn, does not resolve the board, and does not reach step 18
- `docs/01-game-design/GAME_RULES.md` §17 (lines 407–445) — the canonical resolution order, including step 18's expansion (18a Boss Passive, 18b Boss Skill, 18c Boss Attack) and step 19a
- `docs/01-game-design/GAME_RULES.md` §18 — server authority; the client may send only `Swap`, `Card Cast`, `Pet Skill Cast`
- `docs/01-game-design/MATCH3_RULES.md` §4.1 and §4.2 — the resolution's intermediate passes are Transient Resolution State, and cascade pass sequencing/depth
- `docs/02-technical/GAME_EVENTS.md` §1 and §1.1 — the ordering authority for one resolution, including cascade-pass placement; §1.2 — a rejected action emits nothing
- `docs/02-technical/GAME_STATE.md` §5 and §5.1 — `Sequence` semantics, the single post-resolution write-back, and "nothing is written mid-resolution"
- `docs/02-technical/GAME_STATE.md` §3 and §5.3 item 4 — Transient Resolution State, and that a snapshot carries the board **and not the cascade history**
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3 — the `ReceiveEvents(battleId, serverSequence, events[])` envelope; §3.1 items 1–2 — write-back before batch, **no per-step delivery**, the intermediate states of a resolution are never sent; item 4 — a rejected action delivers nothing
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.2 — the closed 16-name `type` discriminator set; §3.2.6 `MatchCreated`, §3.2.7 `CascadeCreated`, §3.2.9 `GemMatched`, §3.2.14/§3.2.15 `DamageDealt`/`DamageTaken`, §3.2.16/§3.2.17 Passive, §3.2.18 `BossSkillCast`, §3.2.19 outcome, §3.2.20/§3.2.21 casts, §3.2.23 `RelicTriggered`, §3.2.24 `PowerChanged` (item 3 — `source` is **stage ownership, not a side**)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §2 (the three client requests and the opaque `clientSequence`), §4 — `BattleStateUpdated` is the only state push, §4 items 4/11 and §8 items 3, 5–9 — no member, method, event, subscription or lifecycle message may be added
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — server authority
- `docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md` — Phaser owns battle rendering and presentation
- `docs/03-decisions/ADR/ADR-008-battle-recovery.md` — recovery is a snapshot, never an event replay
- `docs/02-technical/ARCHITECTURE.md` §2.2, §2.2.1, §2.2.2, §5 — frontend layering, the scene↔runtime port, viewport/scaling ownership, anti-overengineering
- `tasks/completed/TASK-252-generalize-battle-presentation-actor-action-orchestration.md` — the one actor/action adapter, the orchestration machine and the D-2 timeline-owned readouts this task extends (**DONE**, immutable)
- `tasks/completed/TASK-253-developer-move-log-for-battle-presentation.md` — the DEV-only Move Log surface, which reads the machine this task refines (**DONE**, immutable). Its `D-1` surface is **not** widened here
- `tasks/completed/TASK-245-phaser-cascade-animation-and-combat-presentation-sequencing.md` — the timeline and its PD-1…PD-8 boundaries (**unchanged here**)
- `tasks/completed/TASK-246-battle-ux-phase-legibility-selection-feedback-and-pacing.md` — pacing, the derived-hold invariant (F-4) and §4 F-3 (the wire carries no turn/phase signal); C-1/I-1 (the unresolved `GAME_EVENTS.md` §1/§2 vs `SIGNALR_PROTOCOL.md` §3.2.2 conflict) remains reported, not resolved
- `tasks/completed/TASK-209-in-battle-state-projection-and-hud.md` §5 and `tasks/completed/TASK-210-battle-feedback-and-presentation-pass.md` §9 — the diagnostic boundary this task does **not** relax
- `tasks/completed/TASK-088-*.md` — the presentation-feedback baseline the phase model grew from
- `.ai/skills/client/phaser-battle-presentation/SKILL.md` — sequencing and input-lock policy **only**; its illustrative component names and its `Turn: … | Boss Intent: … | Status: …` HUD are not in the contract and are **not** followed (`AGENTS.md` §19; TASK-246 I-2)
- `.ai/workflow/core/validation.md` §2 — MEDIUM validation depth; `.ai/workflow/quality/local-e2e-verification.md` §9, §13 — the "not run vs passing" rule and the prohibition on editing a harness to go green
- `tasks/TASK_LIFECYCLE.md` §6 — the two-stage commit protocol

---

## Audit Findings This Task Resolves

Verified by direct inspection at HEAD `4645e64`. Symbol references are canonical; line references are to that revision.

```text
AF-1  A Boss Skill step is presented by nothing and is held for no time.
      ZERO_HOLD_PHASE_KINDS (BattleScene.ts:614–619) contains 'boss-skill', so
      phaseHoldMs (:3541–3550) returns 0 for it unconditionally, whatever events
      it carries. playPresentationPhase (:3696–3733) has no 'boss-skill' case: the
      step falls to the default arm, and presentCombatFeedback (:4091–4140) has no
      BossSkillCast case, so a boss-skill phase holding only its own delivered
      BossSkillCast renders no animation at all. The only Boss Skill feedback on
      screen is the batch callout — which AF-2 shows is raised before the step
      plays. resolveBossSkillDisplayName (BattleEventPresenter.ts:594) already
      resolves the delivered skillId and is used only by that callout.
      Consequence: the audit's "boss-skill is configured as a zero-hold phase and
      lacks a corresponding visual implementation".

AF-2  A batch callout is raised before its causal event is presented.
      handleBattleEvents selects exactly one callout for the batch and shows it at
      batch arrival (BattleScene.ts:3309–3319), before buildPresentationPhases
      (:3391–3527) runs and before advancePresentation (:3640–3683) plays anything.
      selectBatchCallout (BattleEventPresenter.ts:1379–1401) picks by priority, so
      a Boss Skill / cast / Relic callout is on screen while the phases that report
      that same event are still pending.
      Consequence: the audit's "batch callouts can appear before their causal event
      is presented".

AF-3  Every cascade transition collapses into one board transition.
      buildPresentationPhases emits one phase per delivered
      CascadeCreated.cascadeDepth (:3448–3476), but the match/cascade phases are
      presentation-only (highlights); the board moves to the delivered
      authoritative board in exactly one 'settle' phase, spliced at the first
      damage/retaliation phase (:3496–3513) and played by
      playPresentationPhase's 'settle' arm (:3701–3705).
      Grounded constraint, not a coding oversight: SIGNALR_PROTOCOL.md §3.1
      item 2 (no per-step delivery; the intermediate states of a resolution are
      never sent) and GAME_STATE.md §5.3 item 4 (a snapshot carries the board and
      not the cascade history). No per-pass board exists to present.
      Consequence: the audit's "cascade presentation collapses into a single
      transition to the final board" — resolvable only as far as the delivered
      data supports (see Decisions Required DR-1).

AF-4  A zero-hold step's own effect can overrun into the next actor step.
      phaseHoldMs returns 0 for every ZERO_HOLD_PHASE_KINDS kind before it consults
      phaseEffectMs (:3541–3550), and a 'feedback'-kind step joins the phase
      already open (:3454–3459). So a boss-skill / cast / relic phase that receives
      a delivered PowerChanged or damage-direction event starts a floater and a
      gauge travel (phasePresentationEffectMs :3583–3622) and is still held for 0.
      The derived-hold invariant the existing test asserts for other kinds
      (SceneLifecycle.test.ts:6336–6384) is exercised for these kinds only with an
      empty event list (:6381–6383), so the overrun is unpinned rather than
      disproved.
      Consequence: the audit's "combat effects may overlap later actor actions".

AF-5  Input unlocks when the request settles, before presentation starts.
      onActionSettled (BattleScene.ts:4530–4536) transitions 'submitted' → 'idle'
      whenever no timeline is playing, and isInputLocked (:4430–4432) is false in
      'idle'. The acknowledgement and its resolution's state push / event batch
      travel on separate paths (SIGNALR_PROTOCOL.md §3.1; the scene's own machine
      doc at :536 and :4497–4504 treats both orders as legal), so an acknowledgement
      that settles before the resolution is presented releases the guard.
      Consequence: the audit's "input can unlock after the request resolves but
      before presentation starts, allowing duplicate swaps and a misleading
      'Board has changed' rejection" — the duplicate reaches the server, whose
      LastCommittedSwapPair check rejects it as STALE_ACTION, which the client
      presents as 'Board has changed. Please try again.'
      (BattleEventPresenter.ts:643, formatSwapRejection :1179).

AF-6  The resolution's final authoritative board can be revealed immediately.
      shouldHoldResolution (BattleScene.ts:1972–1982) requires isActionInFlight(),
      and renderBattleState's hold is `shouldHoldResolution(state) ||
      isPresenting()` (:1864). Under AF-5's ordering the guard is already 'idle',
      so the arriving authoritative state is not held, and renderBattleState
      renders the resolution's final delivered board before any of its supported
      transitions have presented (:1874 ff.).
      Consequence: the audit's "the final authoritative board must not be revealed
      before supported intermediate transitions finish presenting".
```

**Verified non-defects — do not "fix" these.** Transient effects are already tracked and cancelled with their timeline (`timelineEffects`, `cancelTimelineEffects` :3925–3933, called by `abortPresentation` :3887–3914). A callout a timeline raised is cancelled with it (`cancelTimelineCallout` :3971–3987). Duplicate and older batches are dropped on `serverSequence` (:3280–3288) and a newer state supersedes playback (`isSupersededBy` :1935–1945). Teardown releases its subscriptions, destroys its layers, kills its tweens and drops the guard and the timeline (`shutdown` :1227–1293: unsubscribes :1228–1233, layers :1258–1263, `tweens.killAll()` :1268, machine back to `idle` + `releaseTimeline()` :1275–1276, `timelineEffects = []` :1293), and `create()` re-initialises the same set (:1078–1101).

---

## Scope

### In Scope

- **One lifecycle for both actors.** The seven stages — Action, Execution, Board Resolution, Cascades, Combat Effects, Recovery, Next Actor — expressed on the existing single adapter (`adaptPresentedEvent`) and the existing orchestration machine. A Player Swap, a Card / Pet-Skill cast, a Relic trigger and every Boss step (passive, skill, retaliation) reach the same pipeline; no per-actor branch, pipeline, registry, interface or event bus (`AGENTS.md` §9; `ARCHITECTURE.md` §5).
- **Skip inapplicable stages without inventing events.** A resolution with no cascade presents no cascade stage; a cast that moves no board presents no board-resolution stage. Stage membership is derived only from delivered events.
- **A real Boss Skill step.** `'boss-skill'` stops being a zero-hold, presentation-less kind: its phase presents its own delivered step (its `skillId` resolved through the existing `resolveBossSkillDisplayName`, and the Boss band it belongs to) and is held for at least the longest animation that step starts. No new catalog, asset or per-Boss animation is introduced.
- **Causal callout timing.** A callout for a delivered event is presented with the phase that owns that event, not at batch arrival; a callout raised outside a timeline (a transport rejection's own feedback) keeps its current behaviour.
- **Effect completion before a conflicting subsequent stage.** The derived-hold invariant (`holdMs` ≥ the longest animation the phase starts) holds for **every** phase kind, including the delivered-action kinds, or those kinds are given the presentation that makes their hold meaningful.
- **The input-lock invariant.** Input stays disabled continuously from swap submission through presentation, recovery and authoritative state reconciliation. A settled request does not unlock input; presentation completion (or supersession/abort, which converge immediately) does.
- **No premature final-board reveal.** While a resolution is pending presentation, the authoritative board it delivers is not rendered as the displayed board; the resolution's own supported transitions present it, and recovery converges it.
- **Recovery.** On completion, abort, supersession and teardown: active effects cancelled or completed, HUD/readouts converged on the delivered state, and the machine advanced to the next actor only after that convergence.
- **Preserve every existing contract**: the `BattleOrchestrationState` machine and its legal transition table, TASK-245's PD-1…PD-8 boundaries and supersession keying, TASK-252's D-2 timeline-owned readouts, and the TASK-253 Move Log's read-only consumption of the machine.
- Unit, integration and gameplay-scenario tests for the shared lifecycle, actor ordering, causal callout timing, effect completion, the lock invariant, absence of a premature board reveal, supported cascade ordering, and cancellation/cleanup.
- Follow the two-stage commit protocol (`tasks/TASK_LIFECYCLE.md` §6).

### Out of Scope

- **Any wire change**: no new/renamed hub method, event type, state-push member, subscription or `type` discriminator — in particular no per-cascade board, snapshot, `phase`, `status`, `activeTurn` or `whoseTurn` member (`SIGNALR_PROTOCOL.md` §3.1 item 2, §4 items 4/11, §8 items 3, 5–9).
- **Any client-side simulation of authoritative Match-3 rules**: no match detection, clearing, gravity destination, spawned-Gem identity, refill result, RNG draw, damage, HP, Power, Combo or Passive derivation (`AGENTS.md` §10; TASK-245 PD-1/PD-8 unchanged).
- **Fabricated presentation**: no per-cascade board, no invented Gem identity, no invented refill result, and no phase, callout or animation for an event the server did not deliver.
- **A per-cascade board transition beyond what the delivered data supports.** The current contract delivers no intermediate board (AF-3); the finer-grained portion is documented and reported, and any protocol work it would need is a separate decision (DR-1).
- **A Boss turn.** `GAME_RULES.md` §2 item 1 and §17 place the Boss response inside the same resolution; a separate Boss turn would be a GAMEPLAY-CHANGE plus a protocol task.
- Any change to TASK-245's PD-1…PD-8 boundaries, supersession keying or outcome-handoff semantics.
- TASK-253's DEV-only Move Log behaviour, and any widening of it; the TASK-209 §5 / TASK-210 §9 diagnostic boundary.
- Any rendered turn/sequence/transport/RNG diagnostic, and any gameplay time limit, deadline, auto-move or forfeit (TASK-246 C-1/C-2 remain undecided).
- Backend, Domain, Application, Infrastructure, Api, `docs/` or ADR changes.
- Editing `tasks/completed/*` or `tasks/blocked/*` (`AGENTS.md` §16/§17/§18).
- Items listed OUT in `docs/00-overview/MVP_SCOPE.md` §2, and unrelated refactoring.

---

## Current State

`BattleScene.ts` already plays one ordered timeline per delivered `ReceiveEvents` batch and already carries the TASK-252 actor/action model: `buildPresentationPhases` (:3391–3527) is a single forward pass over `adaptPresentedEvent`, each phase carries `actor`/`action`/`kind`/`detail`/`holdMs`/`events`, `BattleOrchestrationState` with `BATTLE_ORCHESTRATION_LEGAL_TRANSITIONS` (:541–590) is the guard's one source of truth, `advancePresentation` (:3640–3683) plays the phases with a derived hold, and `abortPresentation` / `releaseTimeline` / `settleHeldPresentation` (:3887–4032) end and converge it. `BattleEventPresenter.ts` parses all sixteen discriminators and owns the one event→(actor, action, kind) adapter.

What is missing is exactly AF-1…AF-6: a Boss Skill step with a presentation and a hold, a callout bound to the phase that caused it, a hold that covers every kind's own effects, and a guard and board-hold that key on "the resolution is still being presented" rather than on "a request is still in flight".

`SceneLifecycle.test.ts` currently pins the behaviour this task must change: `:6336–6384` asserts the hold invariant and asserts that `'outcome'`, `'cast'`, `'relic'` and `'boss-skill'` are zero-hold steps (with an empty event list), `:6386–6419` pins a batch's total timeline hold at exactly 1660 ms, and `:6425–6444` pins the delivered phase order including a `boss-skill` step. Those expectations are part of this task's declared file set and must be updated deliberately, not deleted.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-254:

```text
src/frontend/client/src/game/scenes/BattleScene.ts
src/frontend/client/src/game/scenes/BattleEventPresenter.ts
src/frontend/client/tests/BattleEventPresentation.test.ts
src/frontend/client/tests/SceneLifecycle.test.ts
```

*(The task manifest file itself is excluded from the implementation declared file set. The list above strictly matches the `Declared Files:` field in `## Metadata`.)*

**Shared-file ownership.** All four files were last owned by TASK-253 (`Status: DONE`, immutable, filed in `tasks/completed/`; Phase A commit `39c5247`, Phase B `4645e64`), and before that by TASK-252 and TASK-246. Per `TASK_LIFECYCLE.md` §6.1 item 5, a joint group commit is required only when another `tasks/active/` manifest declares one of these paths. At filing, `tasks/active/` contains only `TASK-248-restore-hoa-long-canonical-base-stats.md` (`Status: IN REVIEW`), which declares `src/backend/GameServer.Domain/Bosses/BossDefinitions.cs` — no overlap. `tasks/backlog/` contains no other task, so no concurrent shared-file owner exists and this is a no-group-commit case.

**Not declared.** `src/frontend/client/scripts/standalone-web-smoke.mjs` (the browser harness TASK-253 last owned) is **not** in this task's declared file set. If a change to it becomes necessary, see Stop Conditions.

**Unowned / pre-existing disclosure (P-3).** `git status --porcelain` at HEAD `4645e64` reports a clean working tree, and `git rev-parse HEAD` is `4645e64428b7b0c6b247a93f7821c16a1ea0140d` under branch `master`. No unowned or pre-existing modification is present; if one appears at pickup it must be disclosed in the Phase A commit body and left unstaged.

---

## Acceptance Criteria

- [ ] Every Player and Boss action is presented through one lifecycle with the seven stages — Action, Execution, Board Resolution, Cascades, Combat Effects, Recovery, Next Actor — and both actors reach it through the same adapter and orchestration model: no per-actor branch, pipeline, registry, interface or event bus.
- [ ] A stage that has no delivered event is skipped, and no phase, callout, animation or gameplay event is fabricated for it (a resolution with no `CascadeCreated` presents no cascade stage; a cast that moves no board presents no board-resolution stage).
- [ ] Actor ordering follows the delivered event order (`GAME_EVENTS.md` §1/§1.1): a Boss step's phases appear in the order the server delivered them (step 18b before step 18c), asserted for a batch carrying both.
- [ ] A `boss-skill` phase presents its own delivered step using only delivered members (`BossSkillCast.skillId`/`sourceId`, resolved through the existing `resolveBossSkillDisplayName`, and the Boss band it belongs to) — no new catalog, no per-Boss animation asset, no inference.
- [ ] A `boss-skill` phase is held for a non-zero time that covers the longest animation that step starts; assert `holdMs > 0` for a boss-skill phase with its own `BossSkillCast` and `holdMs ≥` the longest effect it starts when it carries effect-starting events.
- [ ] A callout for a delivered event is not presented before the phase that owns that event has started; asserted for a batch whose callout-bearing event precedes a later phase (e.g. a Boss Skill step delivered before the damage phase).
- [ ] For **every** phase kind, the hold is never shorter than the longest animation that phase starts — including the delivered-action kinds; the existing assertion is strengthened from an empty-event probe to a representative-event probe per kind.
- [ ] No combat effect the timeline starts is still animating when a conflicting subsequent phase begins: `timelineEffects` from a completed phase have released, and a phase's own hold covers them (AF-4 closed).
- [ ] Cascades are presented in supported order: one phase per delivered `CascadeCreated.cascadeDepth`, in the delivered order, with that pass's own delivered Match/Gem feedback; the single supported board transition occurs after the last cascade phase and before the combat stages, and no per-cascade board, Gem identity or refill result is claimed, invented or derived.
- [ ] The cascade limitation is documented in the completion evidence: per-cascade board transitions are not groundable from the current contract (`SIGNALR_PROTOCOL.md` §3.1 item 2, `GAME_STATE.md` §5.3 item 4), and the protocol-design decision that finer granularity would require is named (DR-1) rather than approximated.
- [ ] While a resolution is pending presentation, the displayed board is not that resolution's final delivered board: no premature reveal for the acknowledgement-settled-first ordering, and none for the batch-arrived-first ordering; asserted for both.
- [ ] Input is locked continuously from Swap submission through presentation, recovery and authoritative state reconciliation: `isInputLocked()` is true in every state except the state that accepts player input and `terminated`, and a settled action request does not by itself unlock it while its resolution is still pending presentation.
- [ ] A duplicate submission is prevented by that lock: a second tap or cast during the pending-presentation window submits nothing and the runtime port is not asked a second time, so no duplicate request (and therefore no `STALE_ACTION` "Board has changed" rejection) can be produced by a double submission.
- [ ] Recovery converges on completion, abort, supersession and teardown: every timeline-owned effect is cancelled or completed, every D-2 readout and the board match the delivered authoritative state, and the machine advances to the next actor only after that convergence.
- [ ] Cancellation and cleanup remain correct: after `abortPresentation`, no tween started by the aborted timeline is still running, no orphan object remains, and `shutdown()`/`DESTROY` mid-timeline leaves no leaked tween or listener.
- [ ] Animation timing never determines an authoritative gameplay outcome: no gameplay value is read from, or gated on, a tween, clock, elapsed time or phase order.
- [ ] Zero client-authoritative gameplay computation: no match detection, clearing, gravity/spawn derivation, refill result, HP accumulation, damage derivation, combo counting or Passive progression (`AGENTS.md` §10; ADR-001).
- [ ] The SignalR wire contract is unchanged: the three-member `ReceiveEvents` envelope, `CascadeCreated = { type, cascadeDepth }`, the nine-member state push, and the closed 16-name discriminator set; no new hub method, event, subscription or state-push member (TASK-245 PD-3/PD-4 preserved).
- [ ] No rendered turn/sequence/transport/RNG diagnostic is introduced, and TASK-253's DEV-only Move Log surface is unchanged in scope (TASK-209 §5 / TASK-210 §9 hold).
- [ ] All relevant tests pass at the required validation depth (`.ai/workflow/core/validation.md` §2, MEDIUM); the updated `SceneLifecycle.test.ts` hold/timeline-length expectations are updated deliberately with the reason recorded, not deleted.
- [ ] The browser suite is reported accurately: run when the documented local stack is available, otherwise recorded as **not run** (`.ai/workflow/quality/local-e2e-verification.md` §9), never as passing and never replaced by mocked-tween evidence.
- [ ] Quality review checklist passes (`.ai/workflow/quality/review.md` §1).
- [ ] No authoritative rule or contract is violated (`AGENTS.md` §10 / ADR-001).

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

- `BattleScene.ts` — `ZERO_HOLD_PHASE_KINDS` (:614–619) and `phaseHoldMs` (:3541–3550) own the hold rule that AF-1/AF-4 close; `playPresentationPhase` (:3696–3733) is where a boss-skill arm belongs beside the existing `swap` / `settle` / `damage` / `retaliation` / `outcome` arms; `presentCombatFeedback` (:4091–4140) is the existing transient-effect path a boss-skill step may use rather than a second one; `buildPresentationPhases` (:3391–3527) owns the stage grouping and the `settle` splice; `handleBattleEvents`'s callout block (:3309–3319) is where AF-2's timing lives; `renderBattleState` (:1827 ff.), `shouldHoldResolution` (:1972–1982) and `onActionSettled` (:4530–4536) are where AF-5/AF-6 live; `settleHeldPresentation` (:4006–4032) is the convergence point Recovery extends.
- `BattleEventPresenter.ts` — extend the presentation-only stage vocabulary and the existing single adapter (`adaptPresentedEvent`); reuse `resolveBossSkillDisplayName` (:594) and `selectBatchCallout` (:1379) rather than adding a second resolver or formatter; keep one spelling of the party vocabulary and one damage classifier (`resolveDamagePresentationPhase`).
- Introduce no new abstraction: the lifecycle is expected to be the existing discriminated phase value plus the existing state machine (`AGENTS.md` §9; `ARCHITECTURE.md` §5).
- Presentation must remain fully synchronous with no tween manager present (the unit-harness mode), so ordering stays observable without a running clock.
- Durations must be **selected to fit the presentation implemented here**, derived from the effect constants the phase actually starts (`SWAP_TWEEN_MS`, `MATCH_HIGHLIGHT_MS`, `GEM_HIGHLIGHT_MS`, `FLOATER_LIFE_MS`, `GAUGE_TWEEN_MS`, `GEM_REFILL_MS`, `PHASE_SETTLE_MS`). Do not adopt any proposed duration from the audit without checking it against those existing effects and the tested lifecycle; a duration is not a substitute for sequencing.
- Do not change the diagnostic strings `presentedPhases` / `presentedEventsLog` / the Move Log record unless a test in this task's declared file set is updated with it; they are the developer record the TASK-247 harness and TASK-253 panel read.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — the seven-stage lifecycle and its skip rules on the one adapter;
                         the boss-skill step's presentation and hold; the callout-vs-phase
                         ordering predicate; the derived hold per phase kind with a
                         representative event each; the lock predicate per machine state,
                         including the request-settled-first ordering; the board-hold
                         predicate for both delivery orders
[x] Integration tests  — wire-contract regression: three-member ReceiveEvents envelope,
                         CascadeCreated = { type, cascadeDepth }, nine-member state push,
                         closed 16-name discriminator set, and the absence of any
                         phase/status/turn/board-snapshot member; a rejected Swap produces
                         no timeline and no callout
[x] Gameplay scenarios — Given a committed Swap that produces a cascade and a Boss
                         response, When the server resolves it and delivers one batch,
                         Then the stages present in GAME_EVENTS.md §1 order,
                         And the Boss Skill step is presented before the retaliation it
                         precedes, And the callout for a step appears with that step,
                         And the final board is not displayed before the supported
                         transitions have played, And input stays locked until recovery
                         converges, And a second tap during that window submits nothing,
                         And an abort mid-timeline cancels only that timeline's effects,
                         And a terminal outcome still reaches ResultScene
```

### Key Edge Cases

- Multi-pass resolution (≥ 2 `CascadeCreated`) — one stage per delivered depth, in delivered order (`MATCH3_RULES.md` §4.2 item 2).
- `MatchCreated.cascadeDepth` and `CascadeCreated.cascadeDepth` are distinct values for one pass and must never be equated (`SIGNALR_PROTOCOL.md` §3.2.7 item 2).
- Boss-phase batch: `PassiveCharged(source="boss")` → `BossSkillCast` → boss `Damage*` in one batch (`GAME_RULES.md` §17 steps 18a–18c), all attributed to the boss actor through the shared adapter, with the Boss Skill step's own presentation non-zero.
- A cast batch with no board movement (a Card cast does not resolve the board — `GAME_RULES.md` §17 lines 453–463): no board-resolution stage, and the cast's own step is attributed to the player.
- `PowerChanged.source` values (`match` / `card` / `relic` / `boss`) are never read as sides (`SIGNALR_PROTOCOL.md` §3.2.24 item 3).
- A rejected Swap emits nothing, produces no timeline and no callout, and leaves no selection (`GAME_EVENTS.md` §1.2).
- Acknowledgement settles before the state push; acknowledgement settles before the batch; batch arrives before the acknowledgement; the state push arrives after the batch — the guard and the board hold must be correct in every one of these orders, and a duplicate submission must be impossible in all of them.
- A newer state or a newer batch arriving mid-playback — abort, converge, no orphaned effect (`ADR-008`).
- Scene shut down mid-timeline — guard released, every timeline-owned effect cancelled, no leaked tween or listener.
- An unknown or malformed event inside the batch — ignored safely, recorded in the developer log only, no fabricated presentation and no fabricated stage.
- A battle with no combat stages at all (a board-only resolution) and a battle whose resolution ends the battle — Recovery and Next Actor behave correctly in both.

---

## Decisions Required Before Implementation

```text
DR-1  Per-cascade board transitions (AF-3). DECISION REQUIRED — NOT AUTHORIZED HERE.
      The current contract delivers no intermediate board: SIGNALR_PROTOCOL.md §3.1
      item 2 states the resolution's intermediate states are never sent, and
      GAME_STATE.md §5.3 item 4 states a snapshot carries the board and not the
      cascade history. Presenting a truthful board transition per cascade pass
      therefore requires new server-resolved intermediate data (a per-pass board or
      snapshot), which is a protocol/state-contract change.
      Required: an operator decision on whether to (A) accept the single supported
      board transition with one phase per delivered cascade depth — the supported
      subset this task implements — or (B) authorize a separate protocol-design task
      for intermediate snapshots. This task does NOT add, simulate or approximate the
      missing data, and it reports the limitation instead.

DR-2  Boss Skill presentation source. DECISION REQUIRED — NARROW.
      The only delivered Boss Skill identity is BossSkillCast.skillId/sourceId, and
      the client already transcribes the canonical display names
      (BOSS_SKILL_NAMES / resolveBossSkillDisplayName, BOSS_RULES.md §6.3/§6.4).
      Required: confirm that the Boss Skill step's presentation is built from that
      existing transcription plus the Boss band — i.e. that no new catalog, asset or
      per-Boss animation is introduced and no presentation is invented for an
      unrecognised skillId (which must render as an inert/identity step).
```

---

## Stop Conditions

- If a proposed stage, actor or action identity cannot be sourced from delivered event members — STOP per `AGENTS.md` §7/§10; do not invent it and do not add a wire member.
- If satisfying a criterion requires a new hub method, event type, subscription, state-push member or `type` discriminator — STOP per `AGENTS.md` §18 and `SIGNALR_PROTOCOL.md` §4 items 4/11, §8 items 3, 5–9.
- If truthful per-cascade board presentation can only be achieved with intermediate server snapshots or other protocol data the current contract does not provide — STOP that portion, document the exact limitation and the decision required (DR-1), and do not silently approximate it as authoritative.
- If satisfying a criterion requires client-authoritative Match-3 computation (match detection, clearing, gravity, refill, spawned-Gem identity, RNG) — STOP per `AGENTS.md` §10.
- If an authoritative gameplay outcome would depend on animation timing, a tween, or a clock — STOP per `AGENTS.md` §10/§11.
- If the lifecycle cannot be expressed without an interface, registry, generic queue, event bus or second animation pipeline — STOP per `AGENTS.md` §9 / `ARCHITECTURE.md` §5 and report the constraint.
- If the work would change TASK-245's PD-1…PD-8 boundary, its supersession keying, or its outcome handoff — STOP and report.
- If the work would need to widen or change the TASK-253 Move Log surface, or relax the TASK-209 §5 / TASK-210 §9 diagnostic boundary — STOP; that is TASK-253 rework or a new task.
- If a Boss turn, a phase/status wire field, or a gameplay time limit appears necessary — STOP; each is a GAMEPLAY-CHANGE and/or protocol task (`AGENTS.md` §7/§17/§18; TASK-246 C-1/C-2).
- If a change to `src/frontend/client/scripts/standalone-web-smoke.mjs` becomes necessary (for example because a resolution's hold grows and a harness wait or budget is no longer correct) — STOP and report: that path is not in this task's declared file set, so per P-5/P-2 the declared set must be amended by the requester, or the harness change carried by a separate task. Never edit the harness to make a run pass (`.ai/workflow/quality/local-e2e-verification.md` §13).
- If satisfying a criterion requires editing `tasks/completed/*`, `tasks/blocked/*`, `docs/` or an ADR — STOP per `AGENTS.md` §16/§17/§18; report instead.
- If the browser E2E stack cannot be brought up — record the browser verification as **not run** (`.ai/workflow/quality/local-e2e-verification.md` §9); never substitute mocked-tween evidence.
- If the task exceeds 7 skills or crosses multiple uncoupled architectural boundaries — STOP & decompose.

---

## Readiness Evaluation (BACKLOG → READY)

Per `tasks/TASK_LIFECYCLE.md` §3, evaluated at filing against HEAD `4645e64`.

```text
[x] Task type confirmed (TASK_TYPES.md)
    FEATURE — development/feature.md. The capability has a documented home
    (MVP_SCOPE.md §1 Match-3 / Cascade / Combo, Pet Passive in-battle presentation,
    Cards, Relics, Bosses; GAME_EVENTS.md §1/§1.1 ordering; the phaser-battle-
    presentation skill's sequencing and input-lock policy), and the task builds it.
    No gameplay rule is invented or changed (not GAMEPLAY-CHANGE), and no ADR,
    layering, protocol or realtime-strategy change is made (not ARCHITECTURE).

[x] Relevant documentation exists in docs/
    Verified present at the cited sections: GAME_RULES.md §2/§11/§17/§18;
    MATCH3_RULES.md §4.1/§4.2; GAME_EVENTS.md §1/§1.1/§1.2; GAME_STATE.md
    §3/§5/§5.1/§5.3; SIGNALR_PROTOCOL.md §2/§3/§3.1/§3.2.x/§4/§8;
    ARCHITECTURE.md §2.2/§2.2.1/§2.2.2/§5; ADR-001/003/008.

[x] MVP scope confirmed (MVP_SCOPE.md §1)
    Everything this task presents is IN: the 8×8 board, Cascade and Combo; the Pet
    Passive mechanism "and its in-battle presentation"; Cards; Relics; Bosses
    (Element, Passive, Skill per Boss). Nothing in §2 is touched and no new system,
    content, currency or progression is added. No IN/OUT/FUTURE question arises
    (MVP_SCOPE.md §4 item 3): the task changes how already-IN features are
    presented.

[x] Task is not blocked by an unresolved dependency
    TASK-252 (Phase A 1e5575e, filed d99e4b6) and TASK-253 (Phase A 39c5247, filed
    4645e64) are both Status: DONE and immutable in tasks/completed/. They last
    owned all four declared files, so no concurrent owner exists — the only active
    task (TASK-248, IN REVIEW) declares a backend file. DR-1 is a documented
    limitation with a supported subset to implement, not a blocker for the task as
    scoped; DR-2 is narrow.

[x] Primary agent assigned
    client (TASK_TYPES.md §5 domain × type matrix: Frontend/Phaser × FEATURE;
    AGENT_SELECTION.md §1 "Frontend feature").

[x] Workflow assigned
    development/feature.md.

[x] Acceptance criteria are testable
    Every criterion is binary and observable: presence/absence of a stage and of a
    per-actor branch; the actor attribution of a delivered event; a non-zero
    boss-skill hold and its relation to the effects it starts; callout-vs-phase
    ordering; the lock predicate per machine state and per delivery order; the
    absence of a premature final-board write; unchanged wire member sets; unchanged
    discriminator set; zero gameplay computation.

[x] Declared file set established from actual inspection
    All four paths exist and were inspected at HEAD 4645e64:
    src/frontend/client/src/game/scenes/BattleScene.ts,
    src/frontend/client/src/game/scenes/BattleEventPresenter.ts,
    src/frontend/client/tests/BattleEventPresentation.test.ts,
    src/frontend/client/tests/SceneLifecycle.test.ts.
    The Declared Files metadata field and the Declared File Set block match exactly.

[x] Stop conditions are complete
    They bound the work to delivered data, forbid wire and gameplay changes, forbid
    fabricating per-cascade boards, forbid touching TASK-245's PD boundary, forbid
    widening the DEV-only Move Log, forbid editing the smoke harness outside the
    declared set, and require an honest "not run" report for the browser suite.
```

**Determination: every BACKLOG → READY criterion passes; the task is eligible for promotion to READY.** Promotion is the requester's / Orchestrator Agent's transition (`TASK_LIFECYCLE.md` §3, §4: `BACKLOG → READY` is a status-field change with no file move, and the file already lives in `tasks/backlog/`). This filing leaves `Status: BACKLOG` per `tasks/README.md` §6 ("Set Status: BACKLOG", "Save to tasks/backlog/").

**Recorded discrepancy (repository evidence preferred).** `tasks/README.md` §6 fixes `Status: BACKLOG` for a newly created task, while the two most recent neighbouring manifests (TASK-252, TASK-253) were promoted to READY in the same filing. This manifest follows the explicit lifecycle rule and records the readiness evidence so either transition is auditable.

**Residual risk carried into execution (not a blocker).** `PresentationPhaseKind`, the phase-detail strings and the hold values are pinned by roughly 300 existing tests across `SceneLifecycle.test.ts` and `BattleEventPresentation.test.ts`, including an exact per-batch timeline length (1660 ms) and the zero-hold assertions at `:6381–6383`. The browser smoke harness also carries timing waits and budgets built on the current resolution lengths. This task must update those expectations deliberately — with the reason recorded — and must not delete an assertion to go green.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Commit

- Implementation slice (Phase A): `945a3e7c6f1bf6376a53445d5ce41a9f69561fc7`
- Subject: `feat: give every battle action one causally ordered presentation lifecycle`
- Owns: `TASK-254`
- Files: `src/frontend/client/src/game/scenes/BattleScene.ts`,
  `src/frontend/client/src/game/scenes/BattleEventPresenter.ts`,
  `src/frontend/client/tests/BattleEventPresentation.test.ts`,
  `src/frontend/client/tests/SceneLifecycle.test.ts`
- Shared with: `none`
- Unowned / pre-existing: `tasks/backlog/TASK-255-validate-and-correct-committed-swap-pair-turn-semantics.md`
  (present and untracked at pickup, unrelated to this task, left unstaged; the task record itself is
  excluded from Phase A per P-5)

### Changed Files

- `src/frontend/client/src/game/scenes/BattleScene.ts` — the one lifecycle: `ZERO_HOLD_PHASE_KINDS`
  is `outcome` alone, so every other kind (including the delivered-action kinds) is held for the
  longest animation its own events start (`phaseEffectMs`/`phasePresentationEffectMs`, with a
  delivered `BossSkillCast` contributing its callout's `CALLOUT_HOLD_MS + CALLOUT_FADE_MS`); a
  `boss-skill` arm presents its own delivered cast (`presentBossSkillStep`); the batch's single
  callout is bound to the phase that owns its event and raised there, never at batch arrival
  (`PresentationPhase.callout`, `buildPresentationPhases`, `playPresentationPhase`); the
  board-resolution step exists only when the resolution delivered a board cycle (`boardPresented`);
  the guard's release and the board hold key on `resolutionPending`/`submissionOutstanding`
  (`renderBattleState`, `shouldHoldResolution`, `isExpectedResolutionState`,
  `releasePendingResolution`, `onTimelineStarted`, `onTimelineEnded`, `onActionSettled`, the three
  submit methods); and Recovery converges the board and every readout **before** the machine advances
  (`finishPresentation`, `abortPresentation`).
- `src/frontend/client/src/game/scenes/BattleEventPresenter.ts` — `selectBatchCalloutEvent` exposes
  the one selection rule together with the delivered event it selected; `selectBatchCallout` is now
  its callout-only projection, so the priority ladder and the delivery-order tie-break still have one
  spelling.
- `src/frontend/client/tests/SceneLifecycle.test.ts` — 18 new regression tests in two nested
  `TASK-254` blocks (the seven-stage lifecycle and single-adapter attribution, the delivered Boss
  phase order, the Boss Skill step's presentation and hold, causal callout timing, effect completion
  inside a hold, the lock window in both delivery orders, duplicate-submission prevention, the
  delivered-facts-only release, convergence-before-release on completion and abort, Boss Skill
  presentation cancellation, the rejected-action case, the diagnostic boundary, and the board hold /
  skipped stages / no-fabricated-board scenarios), plus three deliberately updated pinned
  expectations (the batch total hold 1660 → 1720 ms, the per-kind hold table, and the callout's
  causal raise) with the reasons recorded in place.
- `src/frontend/client/tests/BattleEventPresentation.test.ts` — one new unit test for
  `selectBatchCalloutEvent` (the owning event, the tie-break, and the no-callout case).

### Validation Results

- `npx tsc --noEmit` (client build's type gate; `npm run build` is `tsc && vite build`) — PASS
- `npx vitest run tests/SceneLifecycle.test.ts tests/BattleEventPresentation.test.ts` — PASS (361 tests)
- `npx vitest run` (whole client suite, 23 files) — PASS (1060 tests; 1041 before this task)
- `npm run verify:e2e:smoke` (documented standalone-web journey, backend `/health` = Healthy on
  :5000, Vite on :5173, Edge at the documented candidate path; two clean-context runs per
  invocation) — **NOT PASSING in this environment**, reported not as passing:
  - Run A (no change applied to the harness): 162 checks, 1 failure —
    `phase5c.aRealResolutionReachesThePlayer` (`callouts: []`, 27 events presented), then the run
    completed and the suite exited 1.
  - Run B: fatal timeout at `First feedback pass faded and timeline settled` (8000 ms), before the
    injected-feedback measurement.
  - Run C: reached Phase 10 before failing (`phase10b.committedActionAfterReconnectDeliveredOnTheNewConnection`
    — no candidate Swap committed after the transport reconnect, no `BattleStateUpdated` delivered —
    and `phase10.realBattleReachedItsOwnResult` — 120 swap attempts in 182 s).
  - Baseline comparison: the same suite on unmodified `HEAD` (implementation stashed) fails at
    `First feedback pass faded and timeline settled` (8000 ms) in exactly the same way as Run B, so
    that environment is not green at baseline either.
  - Run A's failure is attributable to this task's required behaviour and needs a **harness** budget
    change, which is outside the declared file set (Stop Conditions; see Remaining Issues).
- `scripts/standalone-web-smoke.mjs` — **not modified** (outside the declared file set; the harness was
  never edited to make a run pass, `.ai/workflow/quality/local-e2e-verification.md` §13).

### Stage Coverage (the seven stages)

| Stage | Presented by | Test |
| --- | --- | --- |
| Action | the delivered action's own step: `playSwapPhase` (the committed Swap), the `cast` / `relic` steps, and `presentBossSkillStep` (a Boss Skill cast) | `presents Player and Boss actions through one lifecycle, in the delivered order`; `presents a Boss Skill step from its own delivered cast, before the retaliation` |
| Execution | the `match` phase (`presentCombatFeedback`'s Match / Gem highlights) | `presents Player and Boss actions through one lifecycle…`; `plays one ordered phase per delivered pass, in GAME_EVENTS.md §1 order` |
| Board Resolution | the `settle` phase (`renderBoard(…, true)`), present only when the resolution delivered a board cycle | `presents no board state the server did not deliver`; `skips the Board Resolution stage for a cast that moves no board`; `skips the Board Resolution stage for a Boss-only resolution that moved no board` |
| Cascades | one phase per delivered `CascadeCreated.cascadeDepth`, carrying that pass's own feedback | `plays one ordered phase per delivered pass…`; `presents no board state the server did not deliver` |
| Combat Effects | the `damage` / `retaliation` / `feedback` phases, the Boss-owned steps, and the floaters / gauges their events start | `presents player damage and boss retaliation as two distinct ordered phases`; `completes a delivered-action step's own effects inside its hold`; `presents a Boss phase batch in delivered order, all attributed to the Boss` |
| Recovery | `settleHeldPresentation` from `finishPresentation` / `abortPresentation`, **before** the guard is released | `converges the delivered state before the guard advances to the next actor`; `converges every readout on the authoritative state when the timeline completes` (TASK-252) |
| Next Actor | `onTimelineEnded` / `onActionSettled` advancing the machine to `idle` | `keeps input locked from submission through presentation and reconciliation`; `locks input in every state except the accepting and the terminated state` (TASK-252) |

### Cascade Limitation (DR-1)

- **Hit, and reported rather than approximated.** The contract delivers no intermediate board:
  `SIGNALR_PROTOCOL.md` §3.1 item 2 (no per-step delivery; a resolution's intermediate states are
  never sent) and `GAME_STATE.md` §5.3 item 4 (a snapshot carries the board and not the cascade
  history). TASK-254 therefore presents one phase per delivered `CascadeCreated.cascadeDepth`, in the
  delivered order, each with that pass's own delivered Match / Gem feedback, and exactly **one**
  board transition — the `settle` phase, placed after the last cascade phase and before the combat
  stages. No per-cascade board, Gem identity, refill result or gameplay event is claimed, invented or
  derived (`presents no board state the server did not deliver` asserts that every board the
  presentation shows is the gesture's own board, that board with the player's own two committed cells
  exchanged, or the resolution's single delivered final board). Truthful per-cascade board
  transitions require new server-resolved intermediate data, i.e. the DR-1 protocol/state-contract
  decision (option B: a separate protocol-design task). TASK-254 changes no protocol.

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic (no match detection, clearing, gravity,
  refill, Gem identity, RNG, damage, HP, Power, Combo or Passive derivation; every presented value is
  a delivered member — `presents no board state the server did not deliver`, the TASK-245 PD-1…PD-8
  source guards, and the existing authority/randomness tests all pass)
- [x] Confirmed no fabricated per-cascade board, Gem identity, refill result or gameplay event (a
  stage with no delivered event is skipped: no cascade stage without `CascadeCreated`, no
  board-resolution stage for a resolution that moved no board, no callout or phase for a rejected
  action)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1: board, Cascade/Combo, Pet Passive
  presentation, Cards, Relics, Bosses — presentation of already-IN features only; nothing from §2 is
  touched and no new system, content or wire member is added)
- [x] Confirmed P-1…P-6 commit policy compliance (Phase A stages exactly the declared file set and
  excludes the task record; the unowned/pre-existing path is disclosed above; Phase B stages only
  this record; no amend, rebase, force push, `git commit -a`, batching or push)

### Remaining Issues

- **Browser-suite follow-up (reported, not fixed here).** `scripts/standalone-web-smoke.mjs` is
  outside this task's declared file set, so its waits/budgets were not adjusted. Two of its
  assumptions no longer hold under this task's required behaviour:
  1. `phase5c.aRealResolutionReachesThePlayer` samples the callout for a fixed 2500 ms window from
     just after the Swap's acceptance. A callout is now raised with the phase that owns its delivered
     event (`SIGNALR_PROTOCOL.md` §3.1 delivers no per-step message, so the phase is the only causal
     place), so a resolution whose selected callout-bearing event is delivered late in the batch (a
     Relic trigger, a Passive change, a Boss Skill cast after the damage) raises its line later than
     the window's start; the failing run presented 27 events inside the window and observed no
     callout, while the runs that passed observed a `COMBO ×2` / `MATCH` line whose event is delivered
     early. Fixing it means widening that wait (or sampling until the timeline settles) — a harness
     change, to be carried by an amended declared file set or a separate task.
  2. `phase10.realBattleReachedItsOwnResult` caps a real battle at 120 swap **attempts** while tapping
     without regard to the input guard, and `driveBattleToTerminal`'s wait accepts the tail of a
     previous resolution's board change. Longer resolutions therefore consume more of that cap. The
     observed run exhausted it after a post-reconnect transport failure had already stopped the battle
     progressing (no `BattleStateUpdated` was delivered after the reconnect in that run), which is a
     transport/reconnect condition outside this task's files.
  The suite is also not green on unmodified `HEAD` in this environment (same fatal wait as Run B
  above), so no browser-suite verdict is claimed for this task.
- DR-2 (Boss Skill presentation source) confirmed and implemented as the narrow reading: the step is
  presented from the delivered `BossSkillCast.skillId` resolved through the existing
  `resolveBossSkillDisplayName` transcription (via the one existing `describeEventCallout` formatter,
  on the Boss band's own callout row in `CALLOUT_BOSS_COLOR`), with `sourceId` read as the delivered
  caster identity the event carries and the battle's one Boss band as the band it belongs to. No
  catalog, asset or per-Boss animation is introduced, and an unrecognised `skillId` keeps that
  formatter's existing `BOSS SKILL` fallback rather than an invented one.

