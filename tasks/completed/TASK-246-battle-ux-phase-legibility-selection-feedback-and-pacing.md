# TASK-246 — Battle UX: Phase Legibility, Selection Feedback, and Presentation Pacing

## Metadata

```text
Task ID:           TASK-246
Type:              FEATURE
Status:            DONE
Risk:              MEDIUM
Priority:          MEDIUM
Primary Agent:     client
Supporting Agents: testing, review
Workflow:          development/feature.md
Skills:            client/phaser-battle-presentation, client/phaser-match3,
                   client/client-event-projection, client/client-state-authority,
                   phaser/tweens
Dependencies:      TASK-245, TASK-232, TASK-210, TASK-088
Declared Files:    src/frontend/client/src/game/scenes/BattleScene.ts,
                   src/frontend/client/src/game/scenes/BattleEventPresenter.ts,
                   src/frontend/client/tests/BattleEventPresentation.test.ts,
                   src/frontend/client/tests/SceneLifecycle.test.ts
```

---

## Objective

Make a resolved Swap **legible** to the player: the cell they have selected must look
selected, the resolution's phase and whose action it is must be continuously readable
from authoritative data only, selection feedback must be cleaned up on every path that
ends a selection, and TASK-245's presentation timeline must be paced so those phases can
actually be read.

This task **derives** phase presentation exclusively from state and events the server
already delivers. It does **not** add a phase, status, turn-ownership, or timer concept to
the wire. Where the existing contract cannot express a phase, this task records that limit
and presents the strongest honest statement available rather than inventing one.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Match-3 (8×8 board, Cascade, Combo) is IN scope
- `docs/01-game-design/GAME_RULES.md` §2 item 1 — **"A Turn represents one player
  Swap/Action."** The turn model contains no time element
- `docs/01-game-design/GAME_RULES.md` §17 — fixed resolution step order (1 Validate Swap …
  19 End Turn; 19a status-effect tick)
- `docs/01-game-design/GAME_RULES.md` §18 — server-authoritative battle
- `docs/01-game-design/MATCH3_RULES.md` §2 item 1, §2.1.1–§2.1.5 — the tap/tap Swap
  interaction, the two-cell request, and rejection semantics
- `docs/01-game-design/MATCH3_RULES.md` §4.2 — cascade pass sequencing
- `docs/02-technical/GAME_EVENTS.md` §1 and §1.1 — event ordering authority
- `docs/02-technical/GAME_STATE.md` §2, §2.0.1–§2.0.3, §2.0.5, §5 — the `BattleState`
  field list, the meaning of `Turn`, and the **explicit absence of any `Status` /
  lifecycle field**
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3, §3.1 items 1–2 and item 5 — batch atomicity,
  the write-back-before-batch rule, **no per-step delivery**, and the client computation
  boundary
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.2 — the **closed 16-name `type`
  discriminator set**
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.14 / §3.2.15 — `DamageDealt` / `DamageTaken`
  and their `source`/`target`
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.19 — `BattleWon` / `BattleLost`
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4 item 4, item 11, §8 items 3, 5–7, 9 —
  `BattleStateUpdated` is the only state push; no lifecycle message may be introduced
- `docs/02-technical/REDIS_STATE.md` §3 — the 30-minute **sliding inactivity** TTL
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md`
- `docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md`
- `docs/03-decisions/ADR/ADR-008-battle-recovery.md` — recovery, not disconnect-loss
- `tasks/completed/TASK-245-phaser-cascade-animation-and-combat-presentation-sequencing.md`
  — the timeline this task tunes, and its PD-1…PD-8 constraints (unchanged here)
- `tasks/completed/TASK-210-battle-feedback-and-presentation-pass.md` §9 — the TASK-209
  diagnostic boundary this task must keep
- `tasks/TASK_LIFECYCLE.md` §6 — two-stage commit protocol
- `.ai/workflow/quality/local-e2e-verification.md` — the required browser verification
- `.ai/skills/client/phaser-battle-presentation/SKILL.md` — animation sequencing and the
  input-lock policy

---

## Investigation Findings (recorded from actual inspection of local HEAD `3277893`)

### F-1 — A selected cell has no selected-cell treatment (defect)

`onCellTapped` stores `this.selectedCell` and calls `renderSwapStatus()`
(`BattleScene.ts:2143–2164`). `renderSwapStatus` writes **only the `swapText` string**
(`BattleScene.ts:2232–2281`): `"Cell 12 selected — select a neighbour."`

No code path draws, tints, strokes, scales, or otherwise marks the selected cell.
`drawGemContent` (`BattleScene.ts:1989–2002`) sets stroke from **Special Gem presentation
only** (`specialPresentation.strokeColor`, else `0x0b0f19`), and it is called only from
`createGemView` and `renderBoard`. The only per-cell overlay is `spawnCellHighlight`
(`BattleScene.ts:3074–3105`), which always **fades to alpha 0 and destroys itself**, and is
keyed exclusively on delivered `MatchCreated.cells` / `GemMatched.cellIndex`.

Consequence: the player's only confirmation that a cell is selected is a text line below
the board. Selection is effectively invisible on the board itself, and is not
distinguishable from match/cascade highlighting because there is no selection treatment at
all.

### F-2 — Selection is not cleared on the paths that end a selection

`this.selectedCell` is assigned in exactly four places (`BattleScene.ts:506`, `827`,
`2148–2161`): set on first tap, cleared on same-cell re-tap, cleared on pair submission,
and cleared in `shutdown()`. It is **not** cleared when:

- the input guard engages during the resolution timeline (`isInputLocked()` prevents new
  taps but leaves a stale `selectedCell`);
- a swap request is **rejected** (`renderSwapStatus` reports the reason; `selectedCell` was
  already cleared at `:2161`, so this path is correct by accident rather than by design);
- the timeline is superseded by a newer authoritative state (`abortPresentation`,
  `BattleScene.ts:2925–2934`, does not touch selection);
- a new push arrives for a different battle.

Consequence: a stale selection can survive a resolution and silently pair with the
player's next tap, submitting a Swap the player did not intend.

### F-3 — No phase indicator exists, and the contract cannot fully express one

There is no `YOUR TURN` / `BOSS TURN` / `RESOLVING` indicator. The scene renders a
`connectionText`, the HUD values, a transient `calloutText`, and the interaction lines. The
delivered `turn` counter (`GameRuntimeEvents.ts:130`, validated at `GameRuntime.ts:1263`) is
**deliberately never rendered** — it is listed among the TASK-209/TASK-210 diagnostics that
must stay unrendered (`BattleScene.ts:869`, TASK-210 §9).

What the contract actually exposes (verified against the closed wire set and the state
shape):

```text
YOUR TURN        NOT derivable. No field states whose turn it is. BattleState.Turn is a
                 committed-Swap counter (GAME_STATE.md §2.0.1), not an ownership flag.
                 CardCastsUsedThisTurn exists in domain state but is NOT delivered
                 (SIGNALR_PROTOCOL.md §4.3 item 2 fixes petState to eight members).
                 GAME_STATE.md §2.0.3: no Status/lifecycle field exists, and introducing
                 one "must be introduced by its own design/ADR task, not added here."

BOSS TURN/ATTACK  Derivable ONLY from within a delivered batch: DamageDealt/DamageTaken
                 with source="boss"/target="player" (§3.2.14 item 3), plus BossSkillCast
                 (§3.2.18) when a Skill fires. GAME_RULES.md §17 step 18 ("Resolve Boss
                 Response") is a stage of the SAME committed Swap's single resolution.
                 No intent data exists, so it cannot be shown before the batch arrives.

RESOLVING        Not a server phase and unobservable by construction: the resolution
                 completes and the state is written back BEFORE the batch is sent
                 (§3.1 item 1), and §3.1 item 2 forbids per-step delivery. The only
                 honest meaning is the local one already modelled by isInputLocked().

TERMINAL OUTCOME Fully derivable and already wired: BattleWon/BattleLost (§3.2.19) via
                 findOutcomeEvent -> ResultScene.
```

### F-4 — Timing audit: phase holds are shorter than the animations inside them

Current constants (`BattleScene.ts:141–142, 212, 241–246, 257, 260`):

```text
SWAP_PHASE_MS       220     GEM_REFILL_MS      220
MATCH_PHASE_MS      320     GAUGE_TWEEN_MS     250
CASCADE_PHASE_MS    320     CALLOUT_HOLD_MS    700
SETTLE_PHASE_MS     340     CALLOUT_FADE_MS    350
DAMAGE_PHASE_MS     420     FLOATER_LIFE_MS    600
FEEDBACK_PHASE_MS   200     cell highlight     350–400 (per Match/Gem)
```

Measured consequences:

- **Swap phase is exactly as long as its own tween** (`SWAP_PHASE_MS` = the swap tween
  `duration: SWAP_PHASE_MS`, `:2867`/`:2874`). The exchange is never seen to settle before
  the next phase begins.
- **`FLOATER_LIFE_MS` (600) exceeds every phase hold that spawns a floater** (damage 420,
  feedback 200). Damage floaters and the callout therefore outlive their own phase and
  overlap the following ones — feedback reads as a jumble, which is the opposite of
  TASK-245's intent.
- **`CALLOUT_FADE_MS` (350) plus `CALLOUT_HOLD_MS` (700)** means a callout lives ~1050 ms
  regardless of timeline length, so it bleeds across phases.
- A typical resolution costs **~1.3 s** (swap+match+settle+damage) and a two-cascade
  resolution with retaliation **~2.4 s** — already long, yet still *rushed*, because the
  holds are shorter than the animations they contain rather than because they are too
  short in absolute terms.

**Root cause: phase holds do not cover the animations they trigger.** The fix is a
**measured** gain/offset adjustment, not a blanket slowdown.

### F-5 — Time-limit audit: no gameplay deadline exists (negative result, recorded)

A repository-wide search found **no per-turn deadline and no per-battle time limit** in any
authoritative document or in any implementation layer.

```text
Per-turn deadline    NONE. GAME_RULES.md §2 item 1 defines a Turn as "one player
                     Swap/Action" with no time element. GAME_RULES.md §17's steps 1–19a
                     contain no time or timeout step.
                     Repo-wide search of docs/ for "deadline", "time limit",
                     "turn timer": ZERO matches.

Per-battle limit     NONE. GAME_RULES.md §1 item 4: "A battle ends when either the Boss or
                     the active Pet reaches 0 HP." GAME_EVENTS.md §1 omits any time-based
                     termination.

The only time-based behaviour that EXISTS, and why it is NOT a gameplay limit:
  - REDIS_STATE.md §3: a 30-minute SLIDING inactivity TTL (implementation
    BattleStateRepository.StateTtl). It resets on every successful resolution, so a battle
    being actively played never expires regardless of its length. It bounds infrastructure
    lifetime, not gameplay.
  - "Turn countdown" throughout the docs means StatusEffect.RemainingTurns, a duration
    counted in discrete Turns and decremented once per Turn at step 19a
    (COMBAT_RULES.md §5.3, GAME_STATE.md §5.1.1). It is not wall-clock and not a deadline.
  - Abandonment consequence (REDIS_STATE.md §3): the record expires, GetBattleState returns
    BATTLE_NOT_FOUND, and NO outcome is recorded — not a loss and not a draw.
```

**Conclusion: any "15 seconds per turn" or "10 minutes per battle" value is an
unapproved proposal with no basis in the current documentation.** See the Product Owner
decision request at the end of this record. This task must not implement one.

---

## Scope

### In Scope

- **Selection treatment.** A clear, persistent selected-cell visual on the board, visually
  distinct from match/cascade highlighting, drawn from the scene-local `selectedCell` index
  alone. It must not read, infer, or display gameplay meaning.
- **Selection lifecycle.** Clear the selection on every path that ends it: same-cell
  re-tap, pair submission, swap rejection, input-guard engagement / timeline start,
  timeline supersession, battle change, and scene teardown. Selection feedback must never
  survive the gesture or the resolution it belonged to.
- **Phase legibility from authoritative data only**, presenting the strongest honest
  statement the contract supports (F-3):
  - a persistent **`YOUR TURN`** state when no resolution is in flight and input is
    unlocked — presented explicitly as the client's own interaction state, not as a server
    turn claim;
  - **`RESOLVING`** while the TASK-245 timeline is playing (already exactly the
    `isInputLocked()` condition);
  - **`BOSS ATTACK`** during the `retaliation` phase, derived from the delivered
    `source="boss"` / `target="player"` classification that
    `resolveDamagePresentationPhase` already performs;
  - the terminal **`VICTORY` / `DEFEAT`** statement from `BattleWon` / `BattleLost` before
    the `ResultScene` handoff.
- **Honest documentation of the gap** in code comments and in the task record: the wire
  exposes no turn-ownership, no phase, and no boss-intent field, so a *server-asserted*
  `YOUR TURN` / `BOSS TURN` indicator is not achievable within this task.
- **Measured pacing adjustment** to the F-4 constants so each phase hold covers the
  animation it triggers, verified in a real browser (not only through mocked tween tests).
- **Tests** for the selection treatment, the selection lifecycle, the phase indicator, and
  the pacing constants.
- Follow the two-stage commit protocol (`tasks/TASK_LIFECYCLE.md` §6).

### Out of Scope

- **Any change to the SignalR contract**: no new hub method, event type, state-push member,
  subscription, or wire member. In particular, no `phase` / `status` / `activeTurn` /
  `whoseTurn` field and no `TurnStarted` / `TurnEnded` admission (`PD-3`).
- **Any gameplay time limit, turn deadline, battle timer, auto-move, auto-pass, skip-turn,
  timeout loss, draw-on-timeout, or forfeit control** — see the decision request below.
- **Any client-authoritative turn or phase state**, and any inference of whose turn it is
  from animation timing alone. Phase presentation is derived from delivered state/events
  only.
- Rendering the raw `turn` counter, or any transport/battle-id/RNG diagnostic
  (TASK-210 §9 boundary preserved).
- Any change to TASK-245's PD-1…PD-8 boundaries, supersession logic, or outcome handoff
  semantics.
- Backend, Domain, Application, Infrastructure, or `docs/` changes.
- Editing `tasks/completed/*`, `tasks/blocked/*`, `docs/`, or any ADR.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.
- Unrelated refactoring or cleanup (`AGENTS.md` §16).

---

## Conflict & Limit Analysis (AGENTS.md §4)

This task surfaces one documentation conflict and one documentation gap. **Neither is
resolved by this task**; both are reported as required.

### C-1 — Documented-but-undelivered events (pre-existing, reported not resolved)

`GAME_EVENTS.md` §1 places `BattleStarted`, `TurnStarted`, `SwapStarted`, `SwapResolved`,
`MatchResolved`, and `TurnEnded` in the ordered event list, and §2 defines
`TurnStarted` / `TurnEnded` with a "Turn number" payload. §3 item 5 states *"every event
defined in §2 belongs to the ordered list of §1 — none is defined as presentation-only. An
event that exists is an event that is delivered in the resolution's batch."*

`SIGNALR_PROTOCOL.md` §3.2.2's `type` table is a **closed set of exactly 16 names** and
contains none of them. Implementation (`BattleEventType`, `BattleEvent.cs`) has exactly
those 16 members; a search of `src/backend` for the missing names finds comments only —
zero members and zero emissions.

```text
Determination: this is a documentation-vs-documentation conflict between
GAME_EVENTS.md §1/§2/§3-item-5 and SIGNALR_PROTOCOL.md §3.2.2, with the implementation
siding with SIGNALR_PROTOCOL.md §3.2.2. Per AGENTS.md §4 it is REPORTED, not silently
resolved.

Consequence for THIS task: it confirms F-3. There is no turn-boundary event to build a
turn indicator on, so this task must not assume one exists. Resolving C-1 is a separate
protocol/documentation task and is NOT in this task's scope.
```

### C-2 — Missing gameplay rule: no turn or battle time limit (AGENTS.md §7 / §20)

F-5 establishes that no per-turn deadline and no per-battle time limit is documented. Per
`AGENTS.md` §7 and §20 this is a **missing rule**, not licence to design one.

```text
Determination: the timer values named in the Product Owner Decision Request below
(15 seconds per turn, 10 minutes per battle) are UNAPPROVED PROPOSALS with no
documentation basis. They are recorded only to make the decision concrete.

This task therefore implements NO time limit. If a time limit is ever required, it is a
GAMEPLAY-CHANGE requiring, in order (AGENTS.md §17/§18):
  1. an explicit Product Owner decision on the rule itself;
  2. the owning design document updated (GAME_RULES.md §2 Turn Rules, plus §1's
     battle-end condition and GAME_EVENTS.md §1's termination list);
  3. an ADR if the authoritative model changes;
  4. a protocol task if any wire signal is needed (SIGNALR_PROTOCOL.md §4 item 4 / §8);
  5. implementation and tests.
None of steps 2–5 may begin before step 1.
```

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-246:

```text
src/frontend/client/src/game/scenes/BattleScene.ts
src/frontend/client/src/game/scenes/BattleEventPresenter.ts
src/frontend/client/tests/BattleEventPresentation.test.ts
src/frontend/client/tests/SceneLifecycle.test.ts
```

*(The task manifest file itself is excluded from the implementation declared file set. The
list above must strictly match the `Declared Files:` field in `## Metadata`.)*

**Shared-file ownership:** all four files were last owned by **TASK-245** (`Status: DONE`,
immutable, committed in `95dad7d`). This task re-declares them as its own implementation
surface; per `TASK_LIFECYCLE.md` §6.1 item 5, if any of these files is concurrently
declared by another `tasks/active/` task, the slices must be combined into a single joint
group commit rather than committed separately. `tasks/active/` and `tasks/blocked/`
contain only `.gitkeep` at the time of filing, so no group commit is currently required.

**Unowned / pre-existing disclosure (P-3):** TASK-245 §Declared File Set recorded
`src/frontend/client/pnpm-workspace.yaml` as carrying a pre-existing **uncommitted**
modification (`esbuild: true`) and required it be left unstaged.

**That disclosure is now stale and is corrected here from direct inspection of local HEAD
`3277893`.** The path is currently **clean** (`git status --porcelain` reports nothing for
it) and its tracked content already contains `allowBuilds:\n  esbuild: true`. It is no
longer an uncommitted working-tree change, so there is nothing for this task to preserve or
avoid staging.

```text
Path:    src/frontend/client/pnpm-workspace.yaml
Status:  clean / tracked (not modified in the working tree)
Content: allowBuilds:\n  esbuild: true
SHA-256: AC02D96368617C760F093CFE61FDEC64B6244007AB3553E0D6621F706F54A353
```

It remains **not** part of this task's declared file set under any circumstance. The SHA-256
above must be identical before and after this task; any change to it is a STOP condition.

---

## Acceptance Criteria

- [ ] A selected cell is rendered with a distinct, persistent visual treatment on the board
      itself, not only in the status text line.
- [ ] The selection treatment is visually distinguishable from match/cascade highlighting:
      it persists while the cell stays selected and is never faded out or self-destroyed the
      way `spawnCellHighlight` overlays are.
- [ ] The selection treatment is derived from the scene-local `selectedCell` index only, and
      renders no gameplay value.
- [ ] The selection and its treatment are cleared on: same-cell re-tap, pair submission,
      swap rejection, input-guard engagement / timeline start, timeline supersession, battle
      change, and scene `shutdown()` / `DESTROY`.
- [ ] No stale selection can pair with a later tap to submit an unintended Swap.
- [ ] A persistent phase indicator shows `YOUR TURN` when no resolution is in flight and
      input is unlocked, and `RESOLVING` while the TASK-245 timeline is playing.
- [ ] The indicator shows `BOSS ATTACK` during the `retaliation` phase, derived only from
      the delivered `source="boss"` / `target="player"` classification.
- [ ] The indicator shows the terminal `VICTORY` / `DEFEAT` statement from
      `BattleWon` / `BattleLost` before the `ResultScene` handoff.
- [ ] The `YOUR TURN` state is presented as the client's own interaction state and is never
      described in code or UI as a server-asserted turn claim.
- [ ] The phase indicator is derived exclusively from delivered state/events and the local
      timeline; **no** phase is inferred from animation timing alone, and no
      client-authoritative turn state is introduced (`AGENTS.md` §10).
- [ ] The raw `turn` counter and every transport/battle-id/RNG diagnostic remain unrendered
      (TASK-210 §9 boundary preserved).
- [ ] Phase holds are adjusted so each phase's hold covers the animations that phase
      triggers; in particular `SWAP_PHASE_MS` exceeds the swap tween duration and
      floater/callout lifetimes no longer overrun their own phase by a large margin.
- [ ] The pacing change is verified in a **real browser** with captured output
      (`.ai/workflow/quality/local-e2e-verification.md` §5, §8.2, §9), not only through
      mocked tween tests.
- [ ] The measured round-trip is recorded in the completion evidence: pre-change and
      post-change resolution durations, and the evidence source.
- [ ] Zero client-authoritative gameplay computation: no HP, damage, match, gravity, spawn,
      combo, phase, or turn derivation (`AGENTS.md` §10, `PD-1`, `PD-8`).
- [ ] The SignalR wire contract is unchanged: no new/renamed member on `BattleStateUpdated`
      or `ReceiveEvents`; `CascadeEvent`'s discriminator set remains the closed 16 names.
- [ ] No gameplay time limit, deadline, auto-move, or forfeit is introduced anywhere.
- [ ] All relevant tests pass at the required validation depth
      (`.ai/workflow/core/validation.md` §2).
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

- `BattleScene.ts` — `selectedCell` (`:506`) and `onCellTapped` (`:2143–2164`) own the
  selection; `renderSwapStatus` (`:2232–2281`) owns the selection *text*; `drawGemContent`
  (`:1989–2002`) owns the per-cell stroke and is where a selected-cell treatment must be
  applied **without** clobbering Special Gem styling — the two must compose, and
  `renderBoard`'s content-change check (`view.content !== gemContentKey(cell)`) currently
  gates `drawGemContent`, so a selection change on an unchanged cell needs its own redraw
  path.
- `spawnCellHighlight` (`:3074–3105`) is the **incorrect** model to copy for selection: it
  is a transient overlay that always fades to alpha 0 and destroys itself.
- The phase indicator belongs with the existing static shell in `drawRuntimeShell`
  (`:880`) / the `calloutText` slot (`:1078`), and must not collide with the callout, which
  shares a slot with the board message (TASK-210 §8's layout constraints). Layout must keep
  every growing line clear of the board and inside the 24 px safe area.
- Phase derivation should reuse `resolveDamagePresentationPhase` (`BattleEventPresenter.ts`)
  rather than introduce a second classifier — the party vocabulary must keep one spelling.
- Pacing constants are the block at `BattleScene.ts:232–260`; `advancePresentation`
  (`:2757–2787`) applies `phase.holdMs`, and every phase plays immediately and in order when
  no tween manager is present (a unit harness), which is what keeps ordering observable.
- The indicator's phase source must be the same condition the guard uses
  (`isInputLocked()`, `:3320–3322`) for the `RESOLVING` state, so the two can never disagree.
- Do not introduce an interface, registry, generic queue, or event bus for this
  (`AGENTS.md` §9). A single scene-local field plus the existing phase switch is sufficient.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — selected-cell treatment is applied and cleared; the phase
                         indicator's value per interaction state; the retaliation phase
                         classification; pacing constants cover their animations
[x] Integration tests  — wire-contract regression: BattleStateUpdated and ReceiveEvents
                         member sets unchanged; the discriminator set still the closed 16
                         names; no phase/status/turn field introduced anywhere
[x] Gameplay scenarios — Given a cell is tapped
                         When it becomes the first selected cell
                         Then that cell is rendered as selected
                         And the selection persists until the gesture ends

                         Given a resolution timeline is playing
                         When the player watches it
                         Then the indicator reads RESOLVING for the whole timeline
                         And it reads BOSS ATTACK during the retaliation phase
                         And it returns to YOUR TURN when the timeline ends

                         Given a swap is rejected
                         When the server returns the reason
                         Then no selection survives
```

### Key Edge Cases
- Same-cell re-tap clears selection **and** its treatment (no orphaned highlight).
- A tap while the guard is engaged selects nothing and leaves nothing selected.
- Timeline supersession (`abortPresentation`) — indicator returns to `YOUR TURN`, selection
  is clear, and the phase indicator never sticks on `RESOLVING`.
- Scene `shutdown()` mid-timeline — indicator and selection are dropped; no leaked tween or
  listener.
- A battle with no delivered `source="boss"` damage — `BOSS ATTACK` never shows, and no boss
  phase is fabricated.
- Terminal batch: `VICTORY` / `DEFEAT` is presented before the `ResultScene` handoff and is
  never delayed or blocked by the timeline.
- A Special Gem cell that is also selected — both the Special Gem styling and the selection
  treatment remain visible (composition, not clobbering).
- A selected cell on the post-resolution board — the treatment follows the authoritative
  board and never marks a cell the delivered board no longer contains.

---

## Validation Strategy

```text
Level                          How
─────────────────────────────  ────────────────────────────────────────────────
Unit / component               npx vitest run (client suite) — the existing
                               tests/SceneLifecycle.test.ts harness already exposes
                               strokeColor / fillColor on the rectangle mock, so the
                               selection treatment is directly assertable.
Type / build                   npx tsc --noEmit ; npx vite build
Browser (REQUIRED, not optional)  npm run verify:e2e:smoke from
                               src/frontend/client with the documented stack up
                               (.ai/workflow/quality/local-e2e-verification.md §3),
                               recording the captured console output (§8.2) and
                               reporting pre/post pacing measurements.
Wire-contract regression       The existing unchanged-wire pins in
                               tests/BattleEventPresentation.test.ts must still pass.
```

A preflight failure is **not** a passing test and must be reported as such
(`.ai/workflow/quality/local-e2e-verification.md` §9). If the E2E stack cannot be brought
up, the pacing verification is **incomplete** and must be recorded as not run — never
reported as passing, and never replaced by a mocked-tween claim.

---

## Risks

```text
R-1  Selection treatment clobbers Special Gem styling (or vice versa), making a
     Special Gem look unselected or a selected Special Gem unrecognisable.
     Mitigation: compose both in one draw path; test the combined case explicitly.

R-2  The phase indicator overstates authority — a player reads "YOUR TURN" as a
     server assertion. The wire cannot support that claim (F-3).
     Mitigation: derive it from local interaction state only, label it honestly in
     code comments, and record the limitation in the task record.

R-3  Pacing is tuned to feel right on one machine and becomes sluggish or too fast
     elsewhere. Mitigation: measure in the real browser, record numbers, and prefer
     covering the internal animations over a blanket multiplier.

R-4  Extending phase holds lengthens the input lock, making the game feel less
     responsive even as it becomes more readable. Mitigation: measure total
     resolution duration before and after and keep the increase bounded and justified.

R-5  An implementer reads .ai/skills/client/phaser-battle-presentation/SKILL.md's
     illustrative HUD ("Turn: 5 | Boss Intent: [Flamestrike] | Status: In Battle")
     or client-event-projection's event table as a contract and invents a boss-intent
     or status field. Those names do not exist in the contract.
     Mitigation: do not implement any indicator the contract cannot source; report the
     skill/contract drift (see Remaining Issues) rather than following the skill.

R-6  Scope creep into the time-limit question. Mitigation: F-5 and C-2 fix the answer
     as "no such rule exists"; the decision request below is the only sanctioned path.
```

---

## Stop Conditions

- If a proposed indicator requires a turn-ownership, phase, or status value that the
  contract does not deliver — STOP per `AGENTS.md` §7/§20 and report; do not infer it, and
  do not add the field.
- If satisfying a criterion requires a new hub method, event, subscription, state-push
  member, or any wire change — STOP per `AGENTS.md` §18 and `SIGNALR_PROTOCOL.md` §4 item 4
  / §8 items 3, 5–7 — that is a protocol change with its own task and ADR.
- If satisfying a criterion requires client-authoritative turn/phase state, or inferring
  whose turn it is from animation timing alone — STOP per `AGENTS.md` §10/§20.
- If a time limit, deadline, auto-move, or forfeit appears necessary — STOP. It is a missing
  gameplay rule (`AGENTS.md` §7); report and await the Product Owner decision below.
- If tuning pacing would alter authoritative gameplay, event order, or an acceptance
  criterion of TASK-245 — STOP; this task may change display timings only.
- If the selected-cell treatment cannot be made distinct from match/cascade highlighting
  within the existing presentation layers — STOP and report the layout constraint rather
  than adding a new layer or abstraction.
- If the browser E2E stack cannot be brought up — do **not** substitute mocked-tween
  evidence. Record the pacing verification as not run (`local-e2e-verification.md` §9).
- If satisfying a criterion requires editing `tasks/completed/*`, `tasks/blocked/*`,
  `docs/`, or an ADR — STOP per `AGENTS.md` §16/§17/§18; report instead.
- If the task exceeds 7 skills or crosses multiple uncoupled architectural boundaries —
  STOP & decompose.

---

## Readiness Evaluation (BACKLOG → READY)

Per `tasks/TASK_LIFECYCLE.md` §3.

```text
[x] Task type confirmed (TASK_TYPES.md)
    FEATURE — development/feature.md. The capability has a documented home
    (MVP_SCOPE.md §1 Match-3; the phaser-battle-presentation skill) and the task builds
    it. No gameplay rule is invented or changed (C-2), and no ADR or architecture change
    is made, so it is neither GAMEPLAY-CHANGE nor ARCHITECTURE.

[ ] Relevant documentation exists in docs/
    PARTIAL — the presentation path is fully documented (GAME_RULES.md §2/§17/§18,
    MATCH3_RULES.md §2, GAME_STATE.md §2/§2.0/§5, SIGNALR_PROTOCOL.md §3/§3.1/§3.2/
    §4/§8, ADR-001/003/008). The turn-ownership/phase signal this task would ideally
    present is NOT documented and is NOT delivered (F-3), and the events
    GAME_EVENTS.md §1/§2 define for it are not on the wire (C-1). The task is
    deliberately scoped to what the delivered contract can honestly source, and records
    the limit rather than resolving it. Ready only on that basis.

[ ] MVP scope confirmed (MVP_SCOPE.md §1)
    "Match-3" is IN: 8x8 board, Cascade, Combo. Presentation of those systems is in
    scope; nothing OUT in §2 is touched.

[ ] Task is not blocked by an unresolved dependency
    TASK-245, TASK-232, TASK-210, TASK-088 are all Status: DONE and immutable.
    C-1 is a documentation conflict that constrains the design but does not block this
    task's scoped work; C-2 is a missing gameplay rule that this task deliberately does
    not need.

[ ] Primary agent assigned
    client.

[ ] Workflow assigned
    development/feature.md.

[ ] Acceptance criteria are testable
    All criteria are binary and observable (treatment present/absent and persistent,
    selection cleared per path, indicator value per interaction state, hold durations
    versus animation durations, unchanged wire members, zero gameplay computation).

[ ] Declared file set established from actual inspection
    All four paths verified to exist. The Declared Files metadata field and the Declared
    File Set block match exactly. Shared-file ownership against TASK-245 is disclosed.

[ ] Stop conditions are complete
    They restate the F-3/C-1/C-2 limits and forbid resolving them by inference or by
    protocol change.
```

**Determination: the task is READY for pickup on the stated basis**, with one caveat carried
into execution: **the `YOUR TURN` / `BOSS TURN` half of the original UX scope is only
partially satisfiable.** A server-asserted turn indicator is not achievable without a
protocol change (F-3, C-1). The task presents the honest client-side equivalent and records
the gap; a Product Owner decision may later authorise the protocol work.

---

## Product Owner Decision Request

Two decisions are requested. **Neither blocks this task.** Both are recorded here so they
are not silently assumed.

### PD-A — Turn / battle time limits (BLOCKING for any future time-limit work)

```text
Question:  Should the game have a per-turn deadline and/or a per-battle time limit?

Finding:   NEITHER EXISTS today. No authoritative document defines one (F-5). The only
           time-based behaviour is a 30-minute SLIDING inactivity TTL in Redis
           (REDIS_STATE.md §3), which bounds infrastructure lifetime, not gameplay, and
           whose consequence is abandonment with NO recorded outcome — not a loss, not a
           draw. GAME_RULES.md §2 item 1 defines a Turn as "one player Swap/Action" with
           no time element.

Status of the timer values named in the originating request
           ("15 seconds per turn", "10 minutes per battle"):
           UNAPPROVED PROPOSALS. They have no documentation basis and are NOT
           requirements of this task. This task implements no time limit.

If YES:    which of (a) per-turn deadline, (b) per-battle limit, or both?
           what happens on expiry — auto-move, skipped turn, loss, draw, or abandonment
             with no outcome?
           is the limit enforceable server-side, and is it visible to the player as a
             countdown?
           Which document owns the rule (GAME_RULES.md §2 / §1; GAME_EVENTS.md §1), and is
             an ADR required?
           Required follow-up: a design change + owning-doc update, then possibly an ADR,
           then a protocol task if the client must be told, then implementation
           (AGENTS.md §17/§18). None of that may begin before this decision.

If NO:     record the decision so the absence is deliberate and future tasks stop
           re-raising it.
```

### PD-B — Turn/phase signal on the wire (NOT blocking; limits the indicator)

```text
Question:  Should the client be able to present a server-asserted turn/phase indicator
           ("YOUR TURN" / "BOSS TURN") rather than the client-derived equivalent?

Finding:   The contract delivers no turn-ownership and no phase field
           (GAME_STATE.md §2.0.3: no Status/lifecycle field, and one "must be introduced
           by its own design/ADR task, not added here"; SIGNALR_PROTOCOL.md §8 item 3
           forbids a lifecycle message). The events GAME_EVENTS.md §1/§2 define for turn
           boundaries are not on the wire (C-1).

Status:    TASK-246 presents the client-derived equivalent (YOUR TURN from local
           interaction state, BOSS ATTACK from delivered boss damage) and records the
           limitation. It does NOT add a field or event.

If YES:    a separate protocol task is required, plus resolution of C-1 first
           (AGENTS.md §4): extend GAME_STATE.md §2.0 and widen SIGNALR_PROTOCOL.md §4, or
           admit the events into §3.2.2, with an ADR per §2.0.3 item 3.
If NO:     TASK-246's client-derived indicator is the accepted end state.
```

---

## Remaining Issues (reported, not fixed — `AGENTS.md` §16)

```text
I-1  Docs-vs-docs conflict C-1 (GAME_EVENTS.md §1/§2/§3-item-5 vs
     SIGNALR_PROTOCOL.md §3.2.2; implementation sides with §3.2.2). Needs its own
     resolution task. Not in this task's scope.

I-2  Client skill/contract drift, which risks an implementer inventing non-existent
     signals (.ai/skills/ is NOT a source of truth — AGENTS.md §19):
       - client-event-projection/SKILL.md's event table names GemSwapped,
         SpecialGemCreated, GemsDropped, PlayerHealed, PowerGained, PetSkillTriggered,
         BossPhaseChanged, BattleConcluded, BattleStateRestored — NONE are valid `type`
         values under the closed 16-name set; the real names are MatchCreated /
         GemMatched / DamageDealt / PowerChanged / BattleWon / BattleLost and the state
         push is BattleStateUpdated.
       - client-state-authority/SKILL.md line 58 relies on a BattleConcluded event.
       - phaser-battle-presentation/SKILL.md's illustrative HUD shows "Turn: 5 | Boss
         Intent: [Flamestrike] | Status: In Battle"; Boss Intent and Status exist nowhere
         in the contract, and line 31 promises "boss turn intents, phase changes".
     Suggested follow-up: a documentation task correcting the three skill files.

I-3  Battle abandonment (TASK-212 §A-16 / TASK-213): no BACK/FORFEIT/QUIT control exists
     and no rule defines what abandonment does. Recorded there as DECISION-REQUIRED and
     deliberately not designed. Still unowned; overlaps PD-A.

I-4  Floater and callout lifetimes (FLOATER_LIFE_MS 600, CALLOUT_HOLD_MS 700 +
     CALLOUT_FADE_MS 350) exceed the phase holds that spawn them, so feedback overlaps
     across phases. This task addresses it as part of the pacing adjustment (F-4); if it
     proves to need a redesign of the feedback layers rather than a retune, STOP and
     report.
```

---

## Execution Preflight Verification (at pickup)

Re-verified by direct inspection at pickup, against local HEAD
`327789314a1861d39a4d736db330de24547bf208` (branch `master`, upstream
`origin/master`):

```text
Worktree               clean except this untracked task record
                       (`git status --porcelain` reported only this file). No
                       pre-existing modification to preserve, and the
                       `pnpm-workspace.yaml` disclosure above was re-confirmed
                       as clean/tracked.
tasks/active/          only `.gitkeep`; tasks/blocked/ only `.gitkeep` — no
                       concurrent declared-file owner, so no joint group commit
                       is required (`TASK_LIFECYCLE.md` §6.1 item 5).
Dependencies           TASK-245, TASK-232, TASK-210, TASK-088 all present in
                       tasks/completed/ (Status: DONE, immutable).
Declared files         all four paths exist at the declared locations.
MVP scope              MVP_SCOPE.md §1 "Match-3" — 8×8 board listed IN.
Turn model             GAME_RULES.md §2 item 1: "A Turn represents one player
                       Swap/Action." — no time element (C-2 / F-5 confirmed).
No lifecycle field     GAME_STATE.md §2.0.3 "Fields Explicitly Not Present":
                       "§2.0 contains **no `Status` field**, and this document
                       introduces none." (F-3 confirmed).
Closed discriminator   SIGNALR_PROTOCOL.md §3.2.2 fixes the closed set as the
                       twelve of §3.2.6–§3.2.19 plus the four of §3.2.20,
                       §3.2.21, §3.2.23, §3.2.24 — sixteen names, none of them
                       a turn or phase event (C-1 confirmed).
Single state push      SIGNALR_PROTOCOL.md §3.1 items 1–3, 5 — write-back
                       before batch, no per-step delivery, no new message type
                       (F-3 confirmed).
Inactivity TTL         REDIS_STATE.md §3 — "default 30 minutes of inactivity",
                       sliding, refreshed on every successful resolution; a
                       time-based infrastructure bound, not a gameplay limit
                       (F-5 confirmed).
```

**Determination: every BACKLOG → READY criterion passes on independent
re-verification, and no blocker was found. TASK-246 is promoted READY and picked
up as IN PROGRESS.** The `YOUR TURN` label was confirmed with the requester at
pickup (the task's originating request offered `YOUR MOVE` / `CHỌN NƯỚC ĐI` as a
wording variant for the same client interaction state; this task implements the
record's own acceptance-criteria string).

---

## Completion Evidence

**Status: DONE.** Implementation, tests, L-1 correction, and real-browser verification are
complete; Phase A implementation slice is committed under `70ab345e0af94e644426972340b5c2fe31bff6ee`,
and Phase B completion record filing is prepared per `tasks/TASK_LIFECYCLE.md` §6.

### Commit

- Phase A (implementation slice): `70ab345e0af94e644426972340b5c2fe31bff6ee`
- Subject: `feat: battle ux phase legibility selection feedback and pacing`
- Owns: `TASK-246`
- Files: `src/frontend/client/src/game/scenes/BattleScene.ts`, `src/frontend/client/src/game/scenes/BattleEventPresenter.ts`, `src/frontend/client/tests/BattleEventPresentation.test.ts`, `src/frontend/client/tests/SceneLifecycle.test.ts`
- Shared with: `none`
- Unowned / pre-existing: `tasks/artifacts/TASK-194-baseline-simulation-results.json, tasks/artifacts/TASK-197-controlled-balance-experiments.json` (present in the working tree; not staged, not owned)

### Changed Files (exactly the declared file set)

```text
M src/frontend/client/src/game/scenes/BattleScene.ts
M src/frontend/client/src/game/scenes/BattleEventPresenter.ts
M src/frontend/client/tests/BattleEventPresentation.test.ts
M src/frontend/client/tests/SceneLifecycle.test.ts
```

Diffstat: `BattleScene.ts` +572/−47 · `BattleEventPresenter.ts` +85 ·
`BattleEventPresentation.test.ts` +120 · `SceneLifecycle.test.ts` +769.
No unrelated file was staged or committed; `src/frontend/client/pnpm-workspace.yaml` is
untouched and its SHA-256 is still
`AC02D96368617C760F093CFE61FDEC64B6244007AB3553E0D6621F706F54A353`.

### What Changed

- **Selection treatment** (`BattleScene.ts`): one persistent, opaque, 4 px white
  ring (`SELECTION_RING_*`), created once per scene run outside every container
  and driven by `renderSelection()` from `selectedCell` alone. It is moved onto
  the selected cell and hidden when nothing is selected, when the rendered board
  no longer contains that cell, and on teardown. It is never tweened, never
  faded, and never destroyed while the scene lives, and it is drawn *beside* the
  cell's own tile, so a Special Gem's stroke and badge are never clobbered.
- **Selection lifecycle**: `clearSelection()` is now the single way a selection
  ends, and every boundary calls it — same-cell re-tap, pair submission, swap
  rejection/transport failure, guard engagement (`submitSwap`,
  `submitCardCast`, `submitPetSkillCast`), timeline start (`playEventBatch`),
  timeline end/supersession/error (`releaseTimeline`), a delivered board or a
  null state (`renderBattleState`, `renderBoard`), `create()` reuse, and
  `shutdown()`/`DESTROY`.
- **Interaction indicator** (`BattleScene.ts` + `BattleEventPresenter.ts`): one
  persistent line in the boss band's clear right half (`PHASE_TEXT_*`), written
  by `renderPhaseIndicator()` from `phaseIndicator()` alone. It states
  `YOUR TURN` when the client permits input, `RESOLVING` while the local
  timeline plays (the same `isInputLocked()` condition the guard uses, so the two
  cannot disagree), `BOSS ATTACK` only while a phase a delivered
  `source="boss"`/`target="player"` instance opened is playing, and the delivered
  terminal `VICTORY`/`DEFEAT` immediately before the `ResultScene` handoff. It is
  empty before any authoritative board exists. The vocabulary lives in
  `BattleEventPresenter` (`INTERACTION_YOUR_TURN`, `INTERACTION_RESOLVING`,
  `INTERACTION_BOSS_ATTACK`, `resolveOutcomeStatement`).
- **Pacing** (`BattleScene.ts`): the per-phase hold constants are gone; a hold is
  now **derived** from the phase's own delivered events
  (`phaseHoldMs`/`phaseEffectMs`/`phasePresentationEffectMs` = the longest
  animation that phase starts + one `PHASE_SETTLE_MS` beat), so a hold can never
  be shorter than its own feedback for any batch shape. Effect lifetimes were
  corrected rather than blanket-slowed (values below).
- **Finding L-1 Correction (outcome phase hold)** (`BattleScene.ts` + `SceneLifecycle.test.ts`):
  `phaseHoldMs` was updated with an early return `if (kind === 'outcome') return 0;`,
  eliminating the spurious 60 ms `PHASE_SETTLE_MS` hold on terminal outcome handoff so the
  outcome phase has a true zero-duration hold (0 ms). Tested and verified via focused
  regression test confirming failure before the fix and pass after.

### Timing: before → after

```text
constant                  HEAD      TASK-246   why
SWAP_TWEEN_MS             220       220        (unchanged; the swap tween)
MATCH_HIGHLIGHT_MS        400*      300        flash was gratuitously long
GEM_HIGHLIGHT_MS          350*      260        as above
GEM_REFILL_MS             220       220        (unchanged)
GAUGE_TWEEN_MS            250       250        (unchanged)
FLOATER_LIFE_MS           600       400        no longer outlives its phase
CALLOUT_HOLD_MS           700       420        ┐ total 660, inside its own
CALLOUT_FADE_MS           350       240        ┘ resolution (was 1050)
PHASE_SETTLE_MS           —         60         the beat after a phase's own animation
```

```text
phase hold                HEAD      TASK-246   derived as
swap                      220       280        SWAP_TWEEN_MS + settle  (> its tween)
match                     320       360        MATCH_HIGHLIGHT_MS + settle
cascade                   320       360–460    the phase's own events + settle
settle                    340       280        GEM_REFILL_MS + settle
damage / retaliation      420       460        FLOATER_LIFE_MS + settle
feedback                  200       460        FLOATER_LIFE_MS + settle
outcome                   0         0          no animation, no hold
```

Resolution totals (holds only): a typical Swap+Match+Settle+Damage goes
**1300 ms → 1380 ms (+6.2 %)**; a Swap+Match+2 Cascades+Settle+Damage+Retaliation
goes **2360 ms → 2560 ms (+8.5 %)**. A cascade phase that also carries a
delivered `PowerChanged` now takes 460 ms — exactly the floater it starts —
instead of a fixed 320 ms.

**Evidence source for those two sides.** The *pre-change* figures are HEAD's own
constants, read from `BattleScene.ts` at `3277893` before the first edit and
independently recorded by this task's F-4 audit (no browser instrument read them
before the change; the baseline browser run above predates it and did not publish
per-phase timings). The *post-change* figures are both derived in code and
**measured in the real browser** below.

**Measured in the real browser** (probe, injected multi-cascade batch; `t` is ms
after delivery, sampled from the scene's own phase list):

```text
phase                        appears at t    measured hold   derived hold
match depth=1                       3 ms             —              360
cascade depth=1                   380 ms           377              360
cascade depth=2                   748 ms           368              360
settle                           1217 ms           469              460   (this cascade carried the Power floater)
damage player->boss              1499 ms           282              280
retaliation boss->player         1984 ms           485              460
timeline settled                 2430 ms          2380 of holds + sampling overhead
```

### Validation Results

```text
npx tsc --noEmit                                   PASS
npx vitest run (full client suite)                 PASS — 23 files, 991 tests
                                                   (TASK-245's recorded baseline: 968; +23 new)
npx vitest run tests/SceneLifecycle.test.ts \
             tests/BattleEventPresentation.test.ts  PASS — 292 tests
npx vite build                                     PASS (built in 2.61 s)
dotnet test src/backend/GameServer.sln             PASS — 2970 tests passed, 4 pre-existing failures (isolated in BossDefinitions / BattleStart smoke test due to HoaLong base stats 1000 vs 5000; untouched)
```

New tests (23): selection treatment presence/position/style and its
non-animation; same-cell deselection; clear-on-submit; composition with a Special
Gem; no stale selection can pair with a later tap (held swap + rapid taps during
the lock + the next tap selecting rather than submitting); clear-on-cast-guard;
clear on a delivered board / null state / SHUTDOWN / DESTROY; indicator claims no
server turn; indicator empty before a board; `RESOLVING`/`BOSS ATTACK`/`YOUR
TURN` per phase; no fabricated `BOSS ATTACK`; terminal `VICTORY`/`DEFEAT` before
the handoff; indicator dropped on teardown; every phase's hold exceeds the
longest animation it starts (asserted on the recorded tween queue, including the
callout's bounded lifetime); ordinary-resolution total stays under 1.5× the holds
it replaced; zero-duration hold for terminal outcome phase (finding L-1).
Plus presenter-level pins: the three interaction statements and the
outcome statement, no turn/phase/status member read anywhere in the presenter,
`RuntimeBattleState` still exactly its nine documented members, and the closed
discriminator set still rejecting `BattleStarted`/`TurnStarted`/`TurnEnded`/
`SwapStarted`/`SwapResolved`/`MatchResolved`/`PhaseChanged`/`BossPhaseChanged`/
`BossTurnStarted`.

### Real-browser verification (headless Edge over CDP, real Phaser runtime)

Run with a standalone probe (`%TEMP%\dcacti-task246-probe\probe.mjs`, a temporary
evidence tool outside the repository) plus the documented suite
(`npm run verify:e2e:smoke`, `.ai/workflow/quality/local-e2e-verification.md`).

**Probe results (all PASS):**

```text
selected cell      tap 12 → ring visible, x=671 y=210 (cell 12's centre),
                   strokeColor 0xffffff, lineWidth 4, width 60, alpha 1,
                   selectedCell 12, swapText "Cell 12 selected — select a
                   neighbour."; ring inBoardLayer=false, inFeedbackLayer=false
second tap 12    → ring hidden, selectedCell null, "Select a cell to swap."
rapid taps        3 real clicks (cells 30/40/50) during the timeline:
                   selectedCell null, ring hidden, inputLocked true,
                   battle sequence 0 → 0 (nothing submitted)
next tap 30      → selectedCell 30, ring visible, sequence still 0 (the refused
                   taps were not remembered and no stale pair was completed)
indicator          RESOLVING at every phase; BOSS ATTACK only while
                   "retaliation boss->player" played; YOUR TURN at t=2429 ms with
                   inputLocked false
terminal           observed labels {RESOLVING, VICTORY}; ResultScene active
                   517 ms after the terminal batch
frame loop         60.2 fps, page visible, loop not sleeping (no stall)
page errors        none (Runtime.exceptionThrown / console.error captured)
screenshots        01-selected-cell.png (white ring on cell 12 + YOUR TURN),
                   02-boss-attack-indicator.png (BOSS ATTACK + the −80 retaliation
                   floater over the Pet panel), 03-result-handoff.png
```

**Cast-guard distinction (diagnostic):** with a resolution timeline playing
(`inputLocked true`, `RESOLVING`), a real click on the first cast control left
`castText` empty (`refused: true`); the *same* click after the timeline settled
was acknowledged in **2 ms** (`CardCast card-heal: rejected
(INSUFFICIENT_POWER).`). The cast path is healthy; a click landing mid-timeline is
refused by the documented input guard.

**Documented suite (`verify:e2e:smoke`) — baseline vs this task:**

```text
baseline, unmodified HEAD (3277893)   RUN 1: 159 checks, 4 failures (aborts):
  phase5c.oneFloaterPerDamageInstancePlusPower   [["-88"],["-40","-88"],["-40"]]
  phase5c.damageFloatersAreAnchoredOnThePartyThatTookTheHit  (+12 floater lost)
  phase5cRelic.everyRelicCalloutNamesTheDeliveredRelic       callouts ["COMBO ×4"]
  phase5cRelic.anUnresolvableRelicShowsNothing               floaters []

after TASK-246 run A                  RUN 1: 2 of those 4 now pass
  (everyRelicCalloutNamesTheDeliveredRelic, anUnresolvableRelicShowsNothing),
  the two floater-coexistence checks still fail, and phase 6 aborts:
  "Timeout waiting for condition: Authoritative cast acknowledgement feedback
   (8000ms)"
after TASK-246 run B                  INVALID — aborted at phase 4b because this
  session edited a served source file mid-run (Vite HMR reloaded the page). Not
  evidence about the code; recorded so it is not mistaken for a result.
after TASK-246 run C                  same phase-6 abort, plus
  phase5c.feedbackMutatesNoAuthoritativeState and 3 phase-5c/5c-relic checks.
```

Root cause of the suite's phase-5c/phase-6 results (pre-existing, and outside this
task's declared files — `scripts/standalone-web-smoke.mjs`):

```text
1. Its probe batches use descending `serverSequence` values (999999, 999998,
   999997) and are delivered while an earlier probe's timeline is still playing,
   so TASK-245's sequence-based supersession discards them. Baseline evidence:
   the Relic callout stayed "COMBO ×4" and the +3 floater never appeared.
2. `phase5c.oneFloaterPerDamageInstancePlusPower` requires all three floaters on
   screen in one instant — what the pre-TASK-245 synchronous presentation
   produced and a per-phase sequential timeline cannot. Baseline measurement:
   [["-88"],["-40","-88"],["-40"]].
3. Its phase-6 cast click is dispatched once, with wall-clock waits, so a click
   that lands while a timeline is playing is refused by the input guard and the
   8 s wait then times out. The measured frame loop runs at 60 fps, so this is the
   harness's wall-clock choreography against the timeline's game time — not a
   stalled browser and not a cast-path defect (proved by the cast-guard
   diagnostic above).
```

`verify:e2e:collection` and `verify:e2e:history` were **not run** (they cover the
collection viewer and battle history, which this task does not touch).

### Git status (exact, at completion-evidence time)

```text
Phase A implementation commit: 70ab345e0af94e644426972340b5c2fe31bff6ee
Phase B completion record: tasks/completed/TASK-246-battle-ux-phase-legibility-selection-feedback-and-pacing.md
Unstaged pre-existing files preserved:
  tasks/artifacts/TASK-194-baseline-simulation-results.json
  tasks/artifacts/TASK-197-controlled-balance-experiments.json
Backlog follow-up task created:
  tasks/backlog/TASK-247-align-web-smoke-e2e-harness-with-sequential-battle-presentation.md

staged: Phase B completion record only
committed: Phase A committed, Phase B prepared
pushed: none
```

### Remaining Issues (reported, not fixed)

```text
I-5  The documented browser smoke suite is incompatible with a sequential,
     correctly-paced presentation timeline: its phase-5c probes are discarded by
     sequence-based supersession, its floater-coexistence expectation predates
     TASK-245, and its single phase-6 cast click can land mid-timeline and be
     refused by the input guard. Evidence for every point is recorded above.
     Formally tracked in backlog task TASK-247
     (tasks/backlog/TASK-247-align-web-smoke-e2e-harness-with-sequential-battle-presentation.md).

I-6  The suite's `inputLockReasons.presentationLocked` still reads the field
     TASK-245 removed (`presentationLocked`), so it always reports false. Tracked in
     TASK-247.

I-7  The suite's `hudBounds` list does not include the new interaction indicator,
     so its browser checks do not measure that line. This task verified the line
     directly instead (the probe read its rendered bounds, and it sits at x ≤ 1244
     inside the 1256 safe-area edge). Tracked in TASK-247.

I-8  In the dev shell the indicator shares the top-right corner with the
     dev-only logout control (`StatusOverlay`); the player-facing frame hides
     those overlays (`hiddenDevOverlayCount: 2`) and a production build has none,
     so there is no player-facing overlap. Cosmetic, dev-only.

I-1…I-4 from the investigation stand unchanged (C-1 documentation conflict, skill
drift, abandonment, and the floater/callout lifetimes now addressed by §5).
```

