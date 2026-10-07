# TASK-210 — Battle Feedback & Presentation Pass

---

## Metadata

```text
Task ID:           TASK-210
Type:              FEATURE (TASK_TYPES.md §2 — the player-facing battle
                   presentation the GDD requires but which did not exist as a
                   HUD; development/feature.md). One defect rides along and is
                   fixed inside the same presentation surface: the damage floater
                   was keyed on the event's discriminator rather than on the
                   delivered party that took the hit.
Status:            DONE
Risk:              LOW–MEDIUM (client presentation only: BattleScene drawing,
                   BattleEventPresenter callout policy, and their tests. No
                   server contract, no gameplay rule, no state model, no
                   transport, no new wire member.)
Priority:          P0 (GDD §17 comprehension; the audit's G1/G2/G3/G7 all reduce
                   to this surface)
Primary Agent:     client (TASK_TYPES.md §5 — Frontend/Phaser)
Supporting Agents: testing, review
Workflow:          development/feature.md
Skills:            client/phaser-battle-presentation,
                   client/client-event-projection,
                   client/phaser-architecture,
                   phaser/tweens
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-209 (DONE — the authoritative state contract this pass
                     presents; its delivered members are the only values used),
                   TASK-208 / TASK-208A (DONE — the projection decision and the
                     `docs/` amendment that authorizes it),
                   TASK-207 (DONE — the audit that ranked G1/G2/G3/G7 and
                     reported G11's floater misattribution),
                   TASK-205 (DONE — the scene-lifecycle pattern this pass keeps)
Blocks:            None
```

**Lifecycle note.** Like TASK-204/205 and TASK-209, this task arrived out of
band as an execution manifest rather than as a `backlog/` file; its record is
filed here so the TASK-207 → TASK-208 → TASK-208A → TASK-209 → TASK-210 chain
stays traceable (`tasks/README.md` §5).

**Scope discipline.** No production file outside the battle presentation surface
was touched. In particular this task changed **no** protocol, **no** backend
file, **no** `docs/` file, **no** gameplay rule, **no** wire member, **no**
SignalR method, **no** store, and **no** scene other than `BattleScene`.
TASK-208 / TASK-208A were not reopened.

---

## Objective

Turn the BattleScene from a functional readout into a readable gameplay
experience: the player must be able to answer *how much HP the Boss has*, *how
much HP the Pet has*, *how much Power is held*, *whether the last move produced
a combo*, *whether damage was dealt or taken*, and *what effect is active* —
without reading a developer diagnostic.

The principle applied throughout:

```text
existing authoritative data
        ↓
better presentation
```

— never *new data → new mechanics → new contract*.

---

## Authoritative References

- `docs/00-overview/GDD.md` §9 (Power as the spend-or-save resource), §11 (the
  combat stats), §17 (the player must always understand what happened), and
  line 86 (HP ends the battle)
- `docs/00-overview/MVP_SCOPE.md` §1 — HP, ATK, DEF, Power, Crit, Status Effects,
  Damage and Element interaction are MVP **IN**; nothing new was added
- `docs/01-game-design/GAME_RULES.md` §12 — Power's documented `0–100` range;
  §18 — server authority
- `docs/01-game-design/MATCH3_RULES.md` §6 (Combo: one committed Swap's Match
  total, `1` is the ordinary case, a rejected Swap keeps the previous value),
  §1.0 (the board's only coordinate convention)
- `docs/01-game-design/PASSIVE_RULES.md` §2–§5 (charge/trigger/reset are the
  server's), §6 item 1 (the progress pair is the UI-facing value)
- `docs/01-game-design/CARD_RULES.md` §3, §3.6 (no client-side cost, modifier,
  affordability or legality)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.14/§3.2.15 (the damage reports and
  their party identifiers), §3.2.16–§3.2.18 (Passive charge/trigger and the Boss
  skill), §3.2.24 (`PowerChanged`), §4.3 item 14/15 (the delivered `petState`
  members), §4.4 items 4–10 (the delivered Boss projection)
- `docs/02-technical/GAME_STATE.md` §2.3.1 (the Status Effect instance schema,
  including item 3's mutually exclusive duration models and item 6's
  one-instance-per-identity rule), §2.3.2
- `docs/02-technical/GAME_EVENTS.md` §1 (delivery order), §2 (the closed event
  set — which contains **no** heal event)
- `docs/02-technical/ARCHITECTURE.md` §2.2.1 rules 1/3/5/6, §2.2.2 (presentation
  may scale/convert, gameplay may not), §2.2.3 rule 3
- `docs/03-decisions/ADR/ADR-001` (server authority), `ADR-003` (Phaser owns
  battle presentation), `ADR-014` (`PetState` carries no client-visible
  identity), `ADR-022` (the preserved pre-battle loadout)
- `.ai/skills/client/phaser-battle-presentation` (floating combat text, HP bar
  tweening, "read damage numbers from the payload, never calculate them")
- `tasks/completed/TASK-208-*.md` §L (the TASK-209 envelope this pass stays
  inside), §M item 4
- `tasks/completed/TASK-208A-*.md` — the landed `docs/` amendment
- `tasks/completed/TASK-207-*.md` — G1/G2/G3/G7 and G11 (floater misattribution)

---

## What Changed

### 1. HP — a gauge of the authoritative pair, beside the numbers

`BattleScene` now draws a bordered track and a proportional fill for the Boss and
for the active Pet, with the delivered `hp / maxHp` still printed next to it.

- Source: `bossState.hp`/`maxHp` (§4.4) and `petState.hp`/`maxHp` (§4.3 item 15)
  — the same two numbers the text shows.
- The only derivation is `fillWidth = round(trackWidth * current / max)`, the
  `barWidth = current / max` shape `ARCHITECTURE.md` §2.2.2 permits.
- No second HP value is stored (asserted by test), no HP is accumulated from a
  damage event, and no event is consulted.
- Zero HP draws an empty track and still prints the delivered `0`. Full HP fills
  the track. `max <= 0` draws no proportion instead of dividing by it, `hp > max`
  caps the *ratio* only, and a non-finite ratio draws nothing — in every case the
  payload and the displayed numbers are untouched.

### 2. Power — the delivered value against the documented range

A `POWER` caption, a 140 px track and the delivered value over
`POWER_RANGE_MAX = 100`. No `maxPower` member is read or invented (asserted by a
source test), and no cost, affordability or cast legality is derived.

### 3. Damage / resource feedback

- `DamageDealt`/`DamageTaken` are now routed by the **delivered party that took
  the damage** (`target`), not by the event's own discriminator — which is the
  TASK-207 G11 defect: the two reports of one instance carry *identical* payloads
  (§3.2.15 item 1), so the previous discriminator-keyed placement drew every hit
  twice and on the wrong side.
- The pair is collapsed into one floater per instance; a `DamageTaken` with no
  matching preceding `DamageDealt` is still drawn.
- A Boss-targeted hit floats over the Boss HP gauge (above the board); a
  player-targeted hit over the Pet HP gauge (beside the board); an unrecognized
  party draws nothing rather than guessing a panel.
- `PowerChanged` draws a signed floater over the Power gauge (`delta` as
  delivered; `power` never recomputed from it).
- **Heal has no feedback because the contract has none**: `GAME_EVENTS.md` §2
  defines no heal event and `ResourceGenerator.ApplyHeal` emits nothing, so no
  amount exists to show. Nothing is derived in its place, and a test pins that a
  `PassiveTriggered` is not turned into an invented `+N`. Reported below.

### 4. Combo / Match feedback

- The persistent Combo/Match lines are unchanged in value and vocabulary
  (`COMBO ×N` from 2 upward, `MATCHES n` always) and still follow the delivered
  state, so the push that publishes an ordinary single-Match Swap clears a stale
  line. It is deliberately *not* aged out on a timer: §6.5 items 1–2 make a
  rejected Swap keep the previous value, so a client-side expiry would disagree
  with the state.
- The *moment* a Combo is earned is marked by the transient callout, set from the
  delivered `ComboChanged`.

### 5. Passive presentation

The Passive line is split so its progress is legible:

```text
Passive: <delivered passiveId>
[██████░░░░░] 2 / 5 Matches (reset: Default)
```

The progress bar is the delivered `current / threshold` drawn as a proportion —
"does progress exist" at a glance — and nothing else: no charge, no Threshold
comparison, no trigger evaluation, no reset (§4.3 item 9). The client holds no
Passive definition, so the delivered identity is printed as itself rather than as
a guessed name.

### 6. Status Effect presentation

```text
Effects: Burn (25, 2 turns); Shield (100, ShieldDepleted)
Effects: none
```

Each instance is rendered from the members that identify it to a player — the
delivered `Id` (§2.3.1 item 1), its applied `Magnitude`, and the one duration
model its element carries (§2.3.1 item 3). The internal classification (`Type`,
`Source`) is no longer printed, and nothing is counted or stacked because §2.3.1
item 6 allows at most one instance per identity. An identity with no client
definition still renders as itself, and an element carrying neither duration
member renders `no duration` rather than an invented one.

### 7. Battle event presentation — one selective callout

A single transient line on the row directly above the board reports the most
important thing a resolution produced:

```text
COMBO ×N · MATCH · CARD: <name> · PET SKILL: <name> · BOSS SKILL ·
PASSIVE n/m · BOSS PASSIVE n/m · PASSIVE TRIGGERED · BOSS PASSIVE TRIGGERED
```

- `BattleEventPresenter.describeEventCallout` maps one delivered event to at most
  one statement; `selectBatchCallout` picks the batch's most important (Combo >
  cast > Boss action > Passive trigger > Passive charge > Match), so a resolution
  is told once rather than replayed as a second event feed.
- `GemMatched`/`CascadeCreated` are already shown as the per-cell highlights;
  `DamageCalculated`'s six factors stay developer detail; `DamageDealt`/
  `DamageTaken`/`PowerChanged` are the floaters.
- `BossSkillCast` shows no `skillId` (a technical identity with no client-visible
  definition) and a Card show shows its loaded display name or, failing that, the
  delivered identity — never a guessed name.
- **`RelicTriggered` produces no callout**: the contract delivers only the
  Relic's owned instance identity and this scene has no Relic definition source,
  so there is no player-facing name to show. Reported below rather than rendered
  as a raw id.

### 8. Timing, hierarchy and layout

- The callout holds ~700 ms and fades over 350 ms with no queue and no input
  lock of its own; floaters rise 30 px over 600 ms and destroy themselves. The
  existing (unchanged) presentation guard is released on the same pass it always
  was, so a player is never waiting on feedback.
- Hierarchy: PRIMARY = the Boss band and the Pet's name/HP; SECONDARY = Power,
  Combo/Match, the cast controls; TERTIARY = Passive, Status Effects, the
  interaction lines and the connection line.
- The board did not move (`BOARD_ORIGIN_X/Y` unchanged) and the cast controls did
  not move. Banded HUD lines now use origin `(0, 0)` so a longer value grows
  *downwards* into free space; every line that can grow declares an advanced
  `wordWrap` width and a `maxLines` cap, so a long name or a long Status Effect
  list cannot reach the board. The callout and the board message share one slot
  (they are mutually exclusive) and sit above the board; the cast acknowledgement
  sits in the strip between the board's bottom edge and the controls.
- `1280 × 720`, 16:9 and the 24 px safe area are unchanged.

### 9. Developer diagnostics

The TASK-209 boundary is kept: the transport state, battle id, turn/sequence,
RNG seed/state and the raw event log are still not rendered anywhere. The
scene's `getPresentedEvents()` diagnostic log is still accumulated and is still
never drawn. No debug infrastructure was redesigned.

---

## Tests

Extended in place — no parallel test infrastructure was created. (The working
tree carries TASK-209's uncommitted test work in the same files, so this section
describes what TASK-210 added, not a diff against `HEAD`.)

`tests/BattleEventPresentation.test.ts`

- The callout policy as a pure unit: which events produce no callout and why,
  Combo's `≥2` threshold, cast naming with and without a loaded definition, Boss
  skill identity never leaking, Passive attribution by delivered `source`, and
  batch selection by importance then delivery order.
- Harness: the text mock gained `setAlpha`, and the rectangle mock gained
  `setSize`/`setPosition`/`setVisible` (the three calls the gauges make).

`tests/SceneLifecycle.test.ts`

- HP: both gauges full; the gauge sized to a partial pair (`1250/5000 → 63 px`,
  `640/1000 → 128 px`); zero HP (empty track, `0` still on screen); a
  non-positive `max` and an `hp > max` payload; and "no second HP value exists"
  (no `bossHp`/`petHp`/`currentHp`/`playerHp`/`remainingHp` field).
- Power: `0`, `45`, `100`; and a source test that no `maxPower` member is
  introduced.
- Combo: no line at `1`, `COMBO ×N` from 2, and a later push clearing it.
- Passive: idle vs in-progress, with the progress gauge's fill.
- Status: the exact compact two-effect line, an unrecognized identity fallback,
  and the empty collection.
- Feedback: one floater per damage instance with the pair collapsed; the side
  decided by the delivered party (Boss anchor `(161, 108)`, Pet `(136, 208)`);
  a lone `DamageTaken`; an unknown party; the signed Power floater `(166, 213)`;
  one selective callout per batch; and that a Passive trigger produces no
  invented heal.
- Layout: every growing line declares a wrap width and a line cap, its left edge
  plus that width stays clear of the board, and the callout/message rows are
  above the board while the cast line is below it.
- The source scan that forbade the bare word `damage` was narrowed to the
  gameplay identifiers (`DamagePipeline`, `ApplyDamage`, `CalculateDamage`, …)
  and strengthened with a direct property: no arithmetic may touch a delivered
  `hp`/`maxHp`/`power`/`amount`.

`tests/ResultScene.test.ts`

- Harness only: the same rectangle/text mock capabilities the scene now uses.

---

## Verification

```text
1. npx tsc --noEmit                     PASS (no output)
2. npx vitest run                       PASS (20 files, 783 tests)
3. npm run build                        PASS (tsc + vite build; dist written)
4. Backend tests                        NOT RUN — no backend file changed
5. Browser E2E                          PASS (see below)
6. Diff audit                           PASS (see below)
```

### Browser E2E (`scripts/standalone-web-smoke.mjs`)

Two runs, each registering a fresh account and playing **two complete real
battles** (4 battles total) through the real backend, database, Redis and
SignalR. `100 checks per run, 0 failures` in both runs, including
`zeroUncaughtExceptions`, `zeroFatalConsoleErrors` and
`zeroUnexpectedApiResponses`.

The presentation-specific checks added or updated:

```text
phase0.inspectedPageIsForeground
phase5b.hudBossHpIsVisible / hudPetHpIsVisible / hudPetPowerIsVisible
phase5b.bossHpGaugeRenders / petHpGaugeRenders / powerGaugeRenders
         (fill = round(trackWidth * current / max), asserted against the
          authoritative push rather than a pixel constant)
phase5b.hudMatchFeedbackIsVisible
phase5b.passivePresentationIsReadable     (identity, pair, reset, gauge)
phase5b.statusEffectPresentationIsReadable
phase5b.noDeveloperDiagnosticsInTheHud
phase5b.rawEventLogIsNotRendered
phase5b.hudDoesNotOverlapTheBoard / hudStaysInsideTheSafeArea
phase5b.hudDoesNotBlockTheCastControls
phase5b.hudScreenshotCapturedWithPlayerFacingFrame
phase5c.aRealResolutionReachesThePlayer        (a real Swap's own feedback)
phase5c.comboCalloutUsesTheDeliveredValue
phase5c.oneSelectiveCalloutPerBatch
phase5c.oneFloaterPerDamageInstancePlusPower
phase5c.damageFloatersAreAnchoredOnThePartyThatTookTheHit
phase5c.feedbackNeverOverlapsTheBoardOrLeavesTheSafeArea
phase5c.feedbackMutatesNoAuthoritativeState
phase6c.gaugesConvergedWithTheNumbers          (after a real reconnect)
phase6d.signatureSkillControlRemainsUsable
```

The real resolutions in the runs produced real `COMBO ×2` / `COMBO ×3` callouts
and real damage floaters on both sides; the deterministic batch pins the exact
per-instance semantics. Evidence frames:
`smoke-shots/run{1,2}-06b-battle-hud.png` (the HUD with the two
DEVELOPMENT-ONLY React overlays hidden) and
`smoke-shots/run{1,2}-07b-combat-feedback.png` (the `COMBO ×4` callout with
`-88` over the Boss gauge, `-40` over the Pet gauge and `+12` over the Power
gauge).

**Harness defect found and fixed while verifying (reported here, not hidden).**
An intermittent phase-8 failure — a ResultScene transition that the scene had
*claimed* (`hasTransitioned: true`) but which never completed, so `MAIN MENU`
appeared to do nothing — was traced to the harness's own environment, not to the
application: the CDP target the suite opens with `PUT /json/new` is not
necessarily the active tab, and while the document is `hidden` Chromium suspends
`requestAnimationFrame`, so Phaser's game loop delivers no steps and a queued
scene start is never processed. The failure diagnostic now proves it
(`documentVisibility: "hidden"`, `loopRunning: true`), `ensurePageVisible`
re-asserts the foreground tab at setup and before every real click, and a second
probe fragility was removed at the same time: `ACTIVE_SCENE` asked
`scene.isActive()` on a scene whose `ScenePlugin` had released its manager, which
throws inside Phaser instead of answering, and now reports such a scene as not
running. Both are harness-only changes; neither can turn a failing check into a
passing one.

### Diff audit

Changed by this task (the working tree also carries TASK-209's uncommitted work
in the same two source files and the same test/E2E files):

```text
src/frontend/client/src/game/scenes/BattleScene.ts             presentation only
src/frontend/client/src/game/scenes/BattleEventPresenter.ts    callout policy
src/frontend/client/tests/SceneLifecycle.test.ts               tests
src/frontend/client/tests/BattleEventPresentation.test.ts      tests
src/frontend/client/tests/ResultScene.test.ts                  harness only
src/frontend/client/scripts/standalone-web-smoke.mjs           existing E2E harness
                                                               (presentation checks,
                                                                player-facing screenshots,
                                                                foreground-tab guard)
```

Verified unchanged by this task: every `docs/` file, every backend file
(`src/backend/**`), `GameRuntime.ts`, `GameRuntimeEvents.ts`, `SignalRService.ts`,
`App.tsx`, the other frontend tests, and every backend test. No protocol change,
no new event, no new store, no new scene, no gameplay file.

---

## Remaining Issues

Reported, not fixed (`AGENTS.md` §16). None blocks TASK-211.

1. **No heal feedback is possible — the protocol has no heal event.**
   `GAME_EVENTS.md` §2 defines no heal event and `ResourceGenerator.ApplyHeal`
   emits nothing, so no healing amount ever reaches the client (TASK-208 §B had
   already recorded this). The state push carries the resulting HP, so the HUD's
   HP gauge and numbers do show that a heal happened; what cannot be shown is
   *how much*, and deriving it from two states would be client-side gameplay
   reconstruction (`AGENTS.md` §10). A `HealApplied`-style event would be its own
   gameplay/event-contract task.
2. **`RelicTriggered` has no player-facing presentation.** The event carries only
   the Relic's owned instance identity (`SIGNALR_PROTOCOL.md` §3.2.23), and the
   battle scene has no Relic definition source, so there is no name to show. The
   existing `GameRuntimePort.getRelics()` read returns owned **instances**; if it
   (or a definition read) exposes a display name, a follow-up could name the
   trigger. Reported rather than rendering a raw id (audit G12).
3. **The Pet Skill Card is not resolvable from the client's Card read.** In a real
   battle the fourth cast control renders `Card: card-inferno` instead of
   `Skill: Inferno`, because `GET /api/cards` (ADR-012: membership *is* the
   unlocked set) does not include the Signature Skill Card that the delivered
   `petState.equippedCards` names. The same gap makes a `PET SKILL` callout fall
   back to the delivered identity. It is a read-contract gap, not a presentation
   defect this task may paper over, and it predates TASK-210 (audit G12 /
   TASK-209's cast controls). Changing it would touch the Card read contract.
4. **`ARCHITECTURE.md` §2.2.2 item 5 still says "No gameplay HUD exists yet."**
   Stale before TASK-210 (it was already false after TASK-209) and explicitly
   assigned to TASK-217 by TASK-207 §J-4 and TASK-208 §K item 5. Reported, not
   edited.
5. **Card cost / affordability** remains undelivered and deliberately untouched
   here: `API_CONTRACTS.md` §5.3 excludes `powerCost` and §4 item 15 forbids the
   client to reconstruct a modifier (audit G2; recommend TASK-212). Presenting
   Power does **not** authorize it, and nothing in this pass derives a cost.
6. **Critical-hit feedback is not implemented**: no delivered event carries a
   crit flag, and `DamageCalculated`'s factors are not a crit marker. Inventing
   one would be client-side damage interpretation.

---

## Final Report

```text
TASK-210 COMPLETE

HP Presentation:
    A bordered gauge plus the delivered `hp / maxHp` text for both sides, drawn
    from `petState`/`bossState` only. The single derivation is the visual ratio
    (`round(trackWidth * current / max)`). Zero HP, full HP, `max <= 0`,
    `hp > max` and a non-finite ratio all render without mutating the payload or
    the numbers, and no second HP value exists in the scene (asserted).

Power Presentation:
    A `POWER` caption, a 140 px gauge and the delivered value over the documented
    `0–100` invariant (`GAME_RULES.md` §12). No `maxPower` member is read or
    invented (asserted by source test); no cost, affordability or legality is
    derived.

Damage/Heal Feedback:
    Damage: one floater per instance, keyed on the delivered party that took it
    (fixing G11's twin/wrong-side floaters), over the Boss or Pet gauge.
    Resource: a signed `PowerChanged` floater over the Power gauge.
    Heal: none — the contract emits no heal event and no amount; the resulting HP
    is visible through the gauge. Documented as a contract gap, not derived.

Combo/Match Feedback:
    The persistent `COMBO ×N` / `MATCHES n` lines keep following the delivered
    state (stale values clear on the push that replaces them) and the moment a
    Combo is earned is marked by the transient callout from `ComboChanged`. No
    new combo calculation, no timer-based expiry.

Passive/Status:
    Passive: the delivered identity plus a `current / threshold` progress gauge
    and the delivered reset behavior. Status: `Id (magnitude, duration)` per
    instance from the members that identify it, with the internal classification
    dropped, a neutral "no duration" fallback, and unknown identities rendered as
    themselves.

Battle Events:
    One selective callout per resolution (Combo > cast > Boss action > Passive
    trigger > charge > Match), colour-coded, holding ~700 ms. Per-cell highlights
    for matches, floaters for damage and Power. RelicTriggered and Critical Hit
    are documented as gaps rather than faked.

Visual/Layout:
    PRIMARY Boss band and Pet name/HP; SECONDARY Power, Combo/Match, cast
    controls; TERTIARY Passive, Status, interaction and connection lines. Board
    and cast controls unmoved; 1280×720, 16:9 and the 24 px safe area preserved;
    banded lines grow downward with advanced word-wrap and line caps, so nothing
    reaches the board.

Lifecycle Safety:
    All new presentation lives on the same scene-owned objects and tweens the
    TASK-205 pattern already releases: gauges are created once and resized, the
    callout and floaters are tween-driven and consumed, `shutdown()` drops every
    new reference and `tweens.killAll()` releases the animations. Verified across
    scene reuse, PLAY AGAIN, battle → result and a second battle in the E2E.

Tests:
    npx tsc --noEmit PASS; npx vitest run PASS (20 files / 783 tests, +24 for
    this task); npm run build PASS. Coverage added for HP (full/partial/zero/
    defensive/no-derived-state), Power (0/mid/100/no-maxPower), Combo (1/>1/
    stale-cleared), Status (empty/one/multiple/unknown), Feedback (damage pair,
    unknown party, signed Power, selective callout, no invented heal) and Layout
    (wrap + cap + board clearance).

Browser E2E:
    scripts/standalone-web-smoke.mjs, two runs, each with two complete real
    battles against the real backend/Postgres/Redis/SignalR:
    100 checks per run, 0 failures. Gauges, numbers, callout, floaters, Passive
    and Status lines, no-diagnostics, no-overlap, safe-area, cast-control
    clearance, Signature Skill usability, resync convergence, Result and
    PLAY AGAIN all pass, with zero uncaught exceptions, zero fatal console
    errors and zero unexpected API responses. Evidence:
    smoke-shots/run{1,2}-06b-battle-hud.png and
    run{1,2}-07b-combat-feedback.png.
    One environment defect in the harness itself was found and fixed while
    verifying (a CDP target that is not the foreground tab freezes Phaser's loop
    because a hidden document suspends rAF; plus a `ScenePlugin`-manager-null
    probe crash). Both fixes are harness-only and cannot mask a failing check.

Files Changed:
    src/frontend/client/src/game/scenes/BattleScene.ts
    src/frontend/client/src/game/scenes/BattleEventPresenter.ts
    src/frontend/client/tests/SceneLifecycle.test.ts
    src/frontend/client/tests/BattleEventPresentation.test.ts
    src/frontend/client/tests/ResultScene.test.ts        (harness only)
    src/frontend/client/scripts/standalone-web-smoke.mjs (existing harness)

TASK-211 READY:
    YES — the battle presentation surface is readable, the contract was not
    touched, and no open item above blocks TASK-211 (card cost/affordability).

Remaining Contract Gaps:
    1. No heal event exists (`GAME_EVENTS.md` §2) — heal *amounts* cannot be
       shown without a new event contract.
    2. `RelicTriggered` carries only an owned instance identity and the battle
       scene has no Relic definition source — no player-facing Relic name.
    3. `GET /api/cards` does not expose the Signature Skill Card that
       `petState.equippedCards` names, so the Pet Skill control and a
       `PET SKILL` callout fall back to the raw identity.
    4. No delivered event carries a crit flag — critical-hit feedback is not
       implementable from the current contract.
```
