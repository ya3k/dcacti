# TASK-186 — Fix Lobby Text Overlap / Presentation Spacing

<!--
  GEN-TASK EXECUTION MANIFEST — BUG FIX TASK (presentation)
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  References docs/ and src/ by path and line — it does NOT copy game rules,
  formulas, magnitudes, schemas, or payload shapes as new authority.

  SCOPE OF THIS TASK: stop the Lobby's three bottom text blocks
  (`selectionText`, `reviewText`, `messageText`) from being drawn on top of
  each other, by re-spacing the Lobby's bottom text band inside the EXISTING
  logical viewport.

  IT ADDS NO GAMEPLAY. IT CHANGES NO RULE, NO WIRE CONTRACT, NO PROTOCOL.
  IT TOUCHES NO BACKEND SOURCE. IT TOUCHES NO AUTHENTICATION. IT MOVES NO
  BUTTON, NO COLLECTION LIST, NO BOSS ROW, AND NO CSS.

  READ "Root Cause", "Current Geometry" AND "Technical Design Constraints"
  BEFORE PLANNING: the work request's coordinates reproduce exactly for two
  of the three bounds and are OFF BY 15 px FOR THE THIRD (the
  `reviewText`/`messageText` overlap is 22 px, not "~7 px"). The conclusion is
  unchanged, but the numbers you plan against must be the measured ones.
-->

---

## Metadata

```text
Task ID:           TASK-186
Title:             Fix Lobby Text Overlap / Presentation Spacing
Type:              BUG (development/bug-fix.md — implementation/presentation
                   defect: the rendered result does not match the
                   presentation the code itself declares, `TDD.md` §2.1's
                   LobbyScene flow and `ARCHITECTURE.md` §2.2.2 rule 5's
                   safe-area rule)
Status:            BACKLOG
Risk:              LOW (one presentation file plus focused tests/tooling; no
                   game rule, no state, no transport, no backend, no schema,
                   no CSS, no auth)
Priority:          MEDIUM (cosmetic in kind, but it makes the step-5 REVIEW
                   summary and the Start Battle status line partly unreadable
                   in the MVP's only supported viewport)
Source:            TASK-185 browser verification (observed, deliberately not
                   changed by TASK-185)
Primary Agent:     client (LobbyScene presentation layout)
Supporting Agents: review (confirms no gameplay/authority/scope boundary is
                   crossed), testing (focused layout regression coverage +
                   the real-browser geometry run)
Workflow:          development/bug-fix.md
Skills:            client/phaser-architecture,
                   discovery/impact-analysis,
                   testing/test-scenario-generation,
                   quality/scope-validation
Dependencies:      TASK-183 (DONE — Runtime Status overlay clickability; its
                   `pointer-events: none` contract must survive),
                   TASK-185 (DONE — MVP Boss Selection; the five-step Lobby
                   flow and the Boss behaviour must survive).
                   Neither task is reopened by this one; both are regression
                   context only.
```

Skill count: 4 (simple-task budget 2–4, `tasks/README.md` §12).

---

## Objective

Re-space the Lobby's bottom text band so that the three live blocks

```text
selectionText   ("Pet: … / Cards: n/3 / Relics: n/5 (min 3)")
reviewText      ("5. REVIEW — Boss: … " + Pet/Cards/Relics summary)
messageText     ("START BATTLE — …")
```

never overlap each other when rendered, while leaving the Lobby's five-step
flow, its visual style, the Start Battle trigger, the Boss rows, the
collection lists, the Runtime Status overlay, and every gameplay/transport
boundary exactly as they are.

This is a **presentation-only** change inside
`src/frontend/client/src/game/scenes/LobbyScene.ts`.

---

## Background

`LobbyScene` draws one fixed logical layout (`LobbyScene.ts` `drawShell()`):
a shell rectangle inside `SAFE_AREA`, three collection columns (steps 1–3),
the Boss band (step 4, added by TASK-185), the bottom-left text band, and the
bottom-right Start Battle trigger.

TASK-185's real-browser verification (see the completed task's Browser Smoke
Test evidence) reached the Lobby and observed that the bottom-left band's
blocks are drawn on top of one another. TASK-185 recorded the finding and
deliberately did not change it, because it is independent of Boss selection:
the four bottom offsets and the 4-line review block are **byte-identical at
`HEAD`** (`git show HEAD:src/frontend/client/src/game/scenes/LobbyScene.ts`
carries the same `- 132 / - 96 / - 58 / - 26` offsets and the same four-line
`reviewText` body). TASK-185 only re-labelled the review's first line
(`4. REVIEW — Boss: <fixed>` → `5. REVIEW — Boss: <selected>`), which does not
change its line count. The defect therefore predates TASK-185 and reproduces
in the current worktree.

---

## Root Cause

### The defect

The four bottom-left blocks are positioned by **fixed pixel offsets from the
safe area's bottom edge**:

```text
src/frontend/client/src/game/scenes/LobbyScene.ts
  L623  selectionText = small(x, SAFE_AREA.y + SAFE_AREA.height - 132, …)
  L627  reviewText    = small(x, SAFE_AREA.y + SAFE_AREA.height -  96, …)
  L629  messageText   = small(x, SAFE_AREA.y + SAFE_AREA.height -  58, …)
  L632  errorText     = small(x, SAFE_AREA.y + SAFE_AREA.height -  26, …)
```

Those offsets allocate **36 px** between `selectionText` and `reviewText` and
**38 px** between `reviewText` and `messageText`. The blocks themselves render
**45 px** (3 lines) and **60 px** (4 lines) tall. Whenever the allocated gap is
smaller than the height of the block above it, the next block starts *inside*
its predecessor and is painted over it (draw order is creation order, so the
later block wins).

```text
allocated gap  36 px  <  selectionText height 45 px  →  9 px overlap
allocated gap  38 px  <  reviewText    height 60 px  → 22 px overlap
allocated gap  32 px  >  messageText   height 15 px  →  0 px overlap (17 px clear)
```

The root cause is therefore **not** "hardcoded absolute coordinates" (they are
derived from `SAFE_AREA` and scale with the viewport) and **not** a font/CSS
problem: the gaps were simply never derived from the rendered height of the
block they must clear (nothing in the file records the blocks' line counts).
The offsets are consistent with an assumed ~10–12 px line (36 px / 3 lines,
38 px / 4 lines); the real line height of a 14 px monospace Phaser `Text` is
**15 px** (measured — see "Current Geometry"). Whether that assumption was ever
written down is not recorded anywhere in the file or in `docs/`.

### Pre-existing, and reproduced independently of TASK-185

- `git show HEAD:…/LobbyScene.ts` has the identical offsets and the identical
  4-line review body → the geometry is unchanged by TASK-185.
- No test asserts these coordinates: a repository search for `- 132`, `- 96`,
  `- 58`, `- 26`, `564`, `638` across `src/frontend/client/tests/` returns no
  match. The defect is invisible to the current suite, which is why it
  survived to a browser run.
- The scene's own test harness cannot see it either: the harness's
  `add.text` double discards both coordinates
  (`tests/LobbyScene.test.ts:239` — `text: (_x: number, _y: number, value)`),
  so no existing unit test *can* observe a position.

---

## Current Geometry

All values are **logical game pixels** in the fixed 1280 × 720 space
(`GameViewport.ts`), whose safe area is
`SAFE_AREA = { x: 24, y: 24, width: 1232, height: 672 }` → the safe area's
bottom edge is **y = 696** and its top edge is **y = 24**.

### Measured line height (the number the offsets got wrong)

Phaser 4 renders multi-line `Text` with
`lineHeight = measuredFontSize + strokeThickness`, where `measuredFontSize` is
`actualBoundingBoxAscent + actualBoundingBoxDescent` of the style's own
`testString` (`'|MÉqgy'`), and `lineSpacing` is 0 here:

```text
node_modules/phaser/src/gameobjects/text/GetTextSize.js:74   lineHeight = size.fontSize + strokeThickness
node_modules/phaser/src/gameobjects/text/MeasureText.js:26-39 fontSize = ascent + descent of style.testString
node_modules/phaser/src/gameobjects/text/Text.js:128          Text defaults to origin (0, 0)
```

Measured in the repository's own real browser (headless Edge/Chromium, the
engine the CDP harnesses drive) with the exact font string the scene uses
(`'14px ui-monospace, monospace'`, `LobbyScene.ts` `small()`):

```text
ascent = 12, descent = 3        → lineHeight (fontSize) = 15 px per line
advance width per character     = 7.697265625 px  (monospace; the resolved
                                   family on the verification host is a
                                   Consolas-class fixed font)
```

Reproduce it with a throwaway HTML page (no repository file needed):

```html
<canvas id="c"></canvas>
<script>
  const ctx = document.getElementById('c').getContext('2d');
  ctx.font = '14px ui-monospace, monospace';        // = Phaser style._font
  const m = ctx.measureText('|M\u00C9qgy');          // = Phaser style.testString
  document.title = (m.actualBoundingBoxAscent + m.actualBoundingBoxDescent); // 15
</script>
```

### Current rendered bounds (game pixels, origin 0,0 → these are the blocks' own rects)

```text
block            top    height  bottom  lines  x-range (left column)   source
---------------  -----  ------  ------  -----  ----------------------  ------------------
selectionText      564      45     609      3  x = 50 …(content)       LobbyScene.ts:623
reviewText         600      60     660      4  x = 50 …(content)       LobbyScene.ts:627
messageText        638      15     653      1  x = 50 …(content)       LobbyScene.ts:629
errorText          670      15     685      1  x = 50 …(content)       LobbyScene.ts:632
---------------  -----  ------  ------  -----  ----------------------  ------------------
```

- `selectionText` is **always** 3 lines (Pet / Cards / Relics counters).
- `reviewText` is **always** exactly 4 lines today, regardless of content,
  because it is *not* word-wrapped (only `messageText` and `errorText` call
  `setWordWrapWidth`, `LobbyScene.ts:630/633`). Its height is therefore a
  deterministic 60 px — but see "Technical Design Constraints" item 3.
- `messageText` is 1 line in every reachable state: the longest message is
  `'Exactly 3 Basic Cards: deselect one first.'` (42 chars ≈ 323 px) against a
  wrap width of `SAFE_AREA.width - 100 = 1132 px`.
- `errorText` is 1 line for realistic transport/server messages; it can grow
  to 2 lines for very long messages, and it is the bottom-most block.

### Overlap arithmetic (measured, not estimated)

```text
selectionText.bottom 609  >  reviewText.top   600   →  overlap  9 px   (request: "~9 px"  ✔)
reviewText.bottom    660  >  messageText.top  638   →  overlap 22 px   (request: "~7 px"  ✘)
messageText.bottom   653  <  errorText.top    670   →  clear   17 px
```

**The two `selectionText`/`reviewText` figures in the work request reproduce
exactly (564, 609, 600).** The third figure does not: `reviewText` ends at 660,
so the `reviewText`/`messageText` overlap is **22 px**, not ~7 px. (660 is
itself correct in the request, as is 638; the stated overlap is an arithmetic
slip.) The defect — and the fix — are unaffected.

### Non-text neighbours that constrain the fix (do not move them)

```text
element                   rect (game px)                 source
------------------------  -----------------------------  --------------------------------
last Boss row (5th)       x 50 …, y 516 … 531           BOSS_LIST_TOP 396 + 4×30, L97-98
START BATTLE trigger      x 930 … 1230, y 578 … 622      L709-713 (w300 h44, centre 1080,600)
SAFE_AREA bottom edge     y = 696                        GameViewport.ts (SAFE_AREA_MARGIN 24)
```

The Start Battle trigger is created **after** the text blocks
(`drawShell()` runs before `drawStartTrigger()`), so anything drawn inside its
rect is painted over by the button's fill. Today nothing intersects it: the
review's widest line (line 4) sits at y 645–660, below the button's 622.

### Available vertical band — the reason this needs a whole-band re-space

```text
free band: last Boss row bottom (531) → safe-area bottom edge (696)  = 165 px
the four blocks at 15 px/line                                        = 135 px
total slack to share between 4 gaps AND the bottom inset             =  30 px
     gaps = boss-row→selection, selection→review, review→message, message→error
     inset = errorText.bottom (685 today) → edge (696) = 11 px today
```

Today's slack is spent as: 33 px above `selectionText`, then 36 / 38 / 32 px
between the blocks, and an 11 px bottom inset — which is more than 30 px of
slack, so the band is *over-subscribed* by construction. The budget therefore
forces a whole-band re-space, and it is tight:

```text
ILLUSTRATIVE balance (arithmetic only — not a prescribed implementation):
  inset 11 px (unchanged)  +  gaps 4 / 5 / 5 / 5 px  = 30 px
  → selection 535…580, review 585…645, message 650…665, error 670…685
  → boss row bottom 531 < 535 ✔   review's 4th line 630…645 is below the
    trigger's bottom 622 ✔   every block ≤ 696 ✔   every gap ≥ 4 px ✔
```

Two consequences the implementer must plan against:

- **The ~36–38 px convention between the bottom blocks cannot be preserved**
  (4 × 36 px would need 279 px). A gap convention of roughly 4–7 px is what the
  measured budget allows; the completion evidence must state the chosen gaps
  and the resulting bottom inset. That is a consequence of the budget, not a
  style change to be "fixed".
- **A fixed-offset layout at the 15 px measured line height has almost no
  headroom.** One extra pixel of line height per line (a host whose
  `ui-monospace`/`monospace` substitutes a taller fixed font — 16 px lines)
  adds 7 px of block height and would consume the whole remaining slack. A
  layout that derives each block's top from the previous block's own measured
  height is therefore the more robust mechanism; if fixed offsets are chosen
  instead, the completion evidence must state which end of the band absorbs
  font drift and why the AC-01/AC-02 floor still holds there.

---

## Authoritative References

- `docs/02-technical/ARCHITECTURE.md` §2.2.2 — the fixed logical resolution
  (1280 × 720), the FIT/letterbox scaling model, and rule 5's safe area
  ("important presentation is not placed against the viewport edge"). This is
  the viewport contract this fix must live inside; it is **not** changed
- `docs/02-technical/TDD.md` §2.1 — the `LobbyScene` responsibility bullet:
  the scene owns the pre-battle flow (Choose Pet → Equip Cards → Equip Relics
  → Choose Boss → Start Battle) as presentation and interaction only, and the
  boss-selection statement added by TASK-185
- `docs/00-overview/GDD.md` §2 — the pre-battle step list the Lobby presents
- `docs/00-overview/MVP_SCOPE.md` §1/§2 — 5 Bosses, Pets, Cards, Relics are IN;
  confirm no OUT item is touched (this task adds no system, no content, no UI
  framework)
- `docs/02-technical/API_CONTRACTS.md` §3 — the `POST /api/battle/start`
  request's four members, which must stay untouched
- `docs/01-game-design/RELIC_RULES.md` §2.1 — 3–5 owned Relics, the source of
  the review block's widest possible line (see Technical Design Constraints)
- `src/frontend/client/src/game/GameViewport.ts` — `GAME_WIDTH`/`GAME_HEIGHT`,
  `SAFE_AREA_MARGIN`, `SAFE_AREA` (the only layout constants this task may
  depend on)
- No ADR governs Lobby pixel layout; no ADR change is expected.

---

## Scope

### In Scope

- The bottom-left text band of `LobbyScene` — the positions (and only the
  positions) of `selectionText`, `reviewText`, `messageText`; `errorText` may
  be repositioned too, solely to keep the stack ordered and inside the safe
  area, without changing its content or its 1-line behaviour
- Whatever minimal, Lobby-local mechanism the implementer proves necessary:
  adjusted named layout constants, offsets, or deriving each block's top from
  the previous block's own measured height at render time
  (`Text.height` / `getBounds()` after `setText`)
- Focused Lobby presentation tests that prove the layout relationship
- A focused real-browser geometry check, added to
  `src/frontend/client/scripts/` following the existing `*-smoke.mjs`
  convention, or equivalent assertions added to an existing script
- Reporting (not fixing) any adjacent presentation defect found while
  measuring

### Out of Scope (NON-GOALS — do not implement any of these)

```text
Boss selection changes        Boss catalog changes
Battle Start changes          the Start Battle trigger's position, size,
                              label, styling, or behaviour
Match-3 changes               Phaser board changes
Runtime overlay changes       any App.css change
Auth changes                  SignalR changes
Backend changes               API changes
Database changes / migrations Relic changes
Pet changes                   Card changes
ResultScene changes           responsive redesign
new UI framework              typography redesign
broad Lobby redesign          a website-style container or scrolling
```

Explicitly:

- **Do not move the Start Battle trigger.** Its rect (x 930–1230,
  y 578–622, centre 1080,600) is TASK-183-verified geometry and the smoke
  harnesses aim real pointer events at that centre. "Battle Start changes" is
  a listed non-goal.
- **Do not move the Boss band, the three collection columns, the shell, or the
  step headers.** They are TASK-185-verified presentation.
- **Do not change what any block displays.** `selectionText` keeps its three
  counters; `reviewText` keeps `5. REVIEW — Boss: <identity>` plus the Pet /
  Cards / Relics summary of the values that would be submitted; `messageText`
  keeps `describeStartTriggerMessage()`'s strings. This task changes *where*
  the text is drawn, never *what* it says.
- **Do not change `App.css`.** The overlap is inside the Phaser canvas; no CSS
  participates in it. `App.css` was inspected for this task and requires no
  change (`.game-shell`, `.game-shell__canvas`, `.game-shell__overlay`,
  `.status-overlay-card` are all viewport/overlay concerns, not Lobby layout).
- **Do not change the viewport contract.** 1280 × 720 logical, safe margin 24,
  FIT + CENTER_BOTH. Do not introduce responsive behaviour, a second layout,
  or a media-query/CSS-driven Lobby layout — none exists in this architecture
  and `ARCHITECTURE.md` §2.2.2 rule 6 forbids presentation from changing the
  logical space.
- **Do not add a module, class, manager, or store for the layout**
  (`AGENTS.md` §9, `ARCHITECTURE.md` §5). Two or three named module-level
  constants inside `LobbyScene.ts` (next to `ROW_HEIGHT` / `BOSS_HEADER_TOP`),
  or a couple of local expressions in `drawShell()`/`render()`, are the
  expected size of the change.
- **Do not "fix" the adjacent findings** listed under "Unrelated Findings" —
  record them.

### Authoritative viewports for this fix

```text
PRIMARY (the contract):  the logical 1280 x 720 game space, SAFE_AREA
                         (ARCHITECTURE.md §2.2.2, GameViewport.ts).
                         Every layout value is logical; the Scale Manager
                         scales the whole canvas, so ONE logical fix covers
                         every viewport size.

VERIFICATION viewport:   1280 x 720 CSS px, deviceScaleFactor 1, via CDP
                         Emulation.setDeviceMetricsOverride — the exact
                         configuration TASK-181/182/183/185 harnesses use and
                         the only viewport at which rendered geometry is
                         authoritative for this task.

NOT required (and not authorized): per-size layouts for 1920x1080, 1366x768,
                         1024x768, 800x600, or the 320x240 minimum supported
                         CSS viewport (GameViewport.ts). They all render the
                         SAME logical layout; a logical-space fix is
                         viewport-independent by construction.
```

---

## Acceptance Criteria

### AC-01 — No Selection/Review overlap

`selectionText` and `reviewText` have visible separation:
`selectionRect.bottom < reviewRect.top`, with a positive gap of **at least
4 game px** (4 px is the platform font-substitution tolerance derived in
Technical Design Constraints item 1, not an arbitrary amount).

### AC-02 — No Review/Message overlap

`reviewText` and `messageText` have visible separation:
`reviewRect.bottom < messageRect.top`, same `gap >= 4` game px floor.

### AC-03 — Existing Lobby flow preserved

All five steps remain visible with the same labels and the same section bands:
the three collection columns (`1. CHOOSE PET`, `2. EQUIP CARDS`,
`3. EQUIP RELICS` at y 58–348), the Boss band (`4. CHOOSE BOSS` header at 370,
five rows at 396–531), and the review block's first line still reading
`5. REVIEW — Boss: …`. The shell, the column headers, the Boss rows, and the
Start Battle label are untouched; only the bottom-left text band's vertical
positions may differ (the review block's own y is one of them).

### AC-04 — Boss Selection preserved

TASK-185 behaviour is unchanged: `selectedBossId` starts `null`; all five
`MVP_BOSSES` render with `○` markers; selecting one marks exactly it and
updates the review's first line to `5. REVIEW — Boss: <canonical identity>`;
re-selecting the current one clears it; a complete loadout with no Boss still
submits nothing and reports `Choose a Boss.`

### AC-05 — Start Battle preserved

Unchanged: `BattleStartRequest` still has exactly its four documented members
(`petId`, `bossId`, `cardLoadout`, `relicLoadout`) with the same values;
one activation issues exactly one `startBattle` call; the in-flight guard,
completeness check, error path, and `BattleScene` transition are untouched; and
the trigger's own rect is unchanged at x 930–1230, y 578–622 (centre
1080,600).

### AC-06 — No gameplay regression

No change to `BattleScene`, `GameRuntime`, the runtime port, `SignalRService`,
the Match-3 board path, any combat/domain value, any backend source, any
migration, or any wire contract.

### AC-07 — Automated regression

Add/update **only** the focused coverage that proves the layout contract:

- The `LobbyScene` test harness currently throws both coordinates away
  (`tests/LobbyScene.test.ts:239`). It must record them (or the scene must
  expose the layout through named constants/testable values) so a position
  change is observable in jsdom, where canvas metrics do not exist.
- The added assertion must encode **heights**, not two constants compared to
  each other: for every adjacent pair it must assert
  `nextTop - currentTop >= currentLines × lineHeight + minGap`, so that
  reverting any offset to today's value fails the test. A test that merely
  re-states today's numbers is not evidence.
- No screenshot assertions, and no new dependency (no image-diff library).

### AC-08 — Real browser verification

A real-browser (CDP) run at 1280 × 720 measures the **live** `Text` objects'
geometry with `getBounds()` (or `x`/`y`/`width`/`height`) and proves
`selection.bottom < review.top` and `review.bottom < message.top` with the
AC-01/AC-02 gap. Calling scene methods from JavaScript to "make" the UI is not
acceptable as evidence; the state must be reached with real pointer input (as
TASK-185's harness already does for Pet/Cards/Relics/Boss), and only then
measured.

### AC-09 — No new overlap with pre-existing Lobby elements

After the fix, none of the four bottom blocks intersects the Start Battle
trigger's rect, and none leaves the safe area:

- `rect.bottom <= 696` for all four blocks;
- `errorText` is the bottom-most block and grows downward when a long
  transport/server message wraps: **prefer to keep `errorText.top <= 670`**
  (today's value, which leaves an 11 px inset and bounds the 2-line worst case
  at 700). If the chosen mechanism needs those few pixels, that trade is
  allowed **only if it is stated explicitly** in the completion evidence with
  the measured `errorText` top and the resulting 2-line worst-case bottom;
- the four blocks stay in the left column (no horizontal relocation into the
  bottom-right corner).

### AC-10 — Block heights stay bounded and documented

The fix must not introduce a *variable* block height that can silently
re-create an overlap:

- If `reviewText` is left unwrapped (today's behaviour) its height stays
  exactly 4 lines, and the fix must state that the review's widest reachable
  line (5 Relics, ~1740 px) already overflows horizontally **before** this
  task — recorded as an unrelated finding, not fixed here.
- If the implementer adds word wrapping to `reviewText`, the layout must
  remain overlap-free at the documented maximum loadout (1 Pet + 3 Basic Cards
  + **5** Relics, `RELIC_RULES.md` §2.1), i.e. it must budget the resulting
  5-line (75 px) block, and the browser run must verify that worst case.
  Adding wrapping without re-budgeting is a defect.

### AC-11 — Nothing outside the presentation band moved

`git diff --stat` shows changes only in: `LobbyScene.ts`, the focused Lobby
test file, and (optionally) one `scripts/*-smoke.mjs` file. No
`App.css`, no `GameViewport.ts`, no `GameConfig.ts`, no other scene, no test
outside the Lobby/geometry scope.

### Baseline criteria

- [ ] `npx tsc --noEmit` and `npm run build` (in `src/frontend/client`) pass
- [ ] `npm run test:run` (in `src/frontend/client`) passes with the new coverage
- [ ] All relevant tests pass at the required validation depth
      (`core/validation.md` §2)
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rule or contract violated (`AGENTS.md` §10 / ADR-001)
- [ ] The real-browser geometry run is actually performed and its observed
      numbers reported (an unverified claim is a failed criterion)

---

## Technical Design Constraints

Recorded because they change what a correct fix looks like; the implementing
agent must verify them before choosing a mechanism.

1. **The rendered line height is font-dependent; do not hard-code 15 px as
   universal truth.** 15 px is measured on the verification host
   (`ui-monospace, monospace` resolving to a Consolas-class family). On a host
   where that family is substituted, the same 14 px style renders 13–16 px per
   line. A fix whose correctness depends on the exact 15 px must therefore
   leave a **≥ 4 px** gap (2 px per line of drift across the two adjacent
   blocks), or must derive positions from each block's own measured height
   (`Text.height` / `getBounds()` after `setText`), which is self-correcting
   and is an explicitly acceptable approach for this task.
2. **The Start Battle trigger is drawn on top of the text.** Because
   `drawStartTrigger()` runs after `drawShell()`, any wide review line that
   lands inside y 578–622 and reaches x ≥ 930 is painted over by the button.
   With real server-minted identities the review's `Relics:` line is
   `'Relics: ' + relicinst_<32 hex>` ×3 ≈ 138 characters ≈ 1062 px wide, so it
   *is* wide enough to reach the button's x-range — it is safe today only
   because it sits at y 645–660. Moving the review block up by more than
   ~23 px would slide that line into the button's band. Keep the review
   block's last line at `y >= 622` (i.e. `reviewTop >= 577`), or keep every
   line that reaches x ≥ 930 out of that band.
3. **`reviewText` is not word-wrapped today** (only `messageText`/`errorText`
   are — `LobbyScene.ts:630/633`), which is exactly why its height is a
   deterministic 4 lines. Adding wrap changes the block's height in the
   5-Relic case (see AC-10). Adding wrap is permitted but not required; the
   smallest change does not add it.
4. **`errorText` is the bottom-most block and grows downward.** Do not push it
   closer to the safe-area edge (AC-09) to buy space for the blocks above it.
5. **The Runtime Status panel is not a layout constraint.**
   `.status-overlay-card` (`App.css:120-147`) is an opaque, bottom-right,
   `z-index: 5`, `pointer-events: none` DOM panel anchored `right: 12px;
   bottom: 12px` with `max-width: min(420px, …)`; its height depends on
   runtime content. Consequences: (a) `document.elementFromPoint` returns the
   **canvas** even where the panel visually covers it, because the panel opts
   out of hit-testing — so `elementFromPoint` proves nothing about visual
   occlusion and must not be used as the overlap test; (b) the panel already
   occupies the bottom-right corner, so do **not** "solve" the overlap by
   moving text rightwards into it, and do not change the panel (TASK-183
   scope).

---

## Testing Requirements

### Required Verification

```text
[ ] Unit tests         — the `LobbyScene` harness records text coordinates and
                         asserts, for the bottom band:
                           · selectionText.top  >= lastBossRow.bottom + minGap
                           · reviewText.top     >= selectionText.bottom + minGap
                           · messageText.top    >= reviewText.bottom    + minGap
                           · errorText.top      >= messageText.bottom   + minGap
                           · every block's bottom <= 696
                           · errorText.top <= its pre-fix 670, or the stated
                             trade recorded per AC-09
                         with the line counts/heights modelled explicitly, so
                         restoring any pre-fix offset fails the suite
[ ] Integration tests  — N/A: no state, transport, or contract boundary is
                         touched. The existing LobbyScene suite (Boss
                         selection, request shape, shutdown reset) must pass
                         UNMODIFIED in substance
[ ] Gameplay scenarios — N/A: this task changes no gameplay behavior
```

Commands (run from `src/frontend/client`):

```text
npx tsc --noEmit
npm run test:run
npm run build
```

### Key Edge Cases

- The message block changes text on selection messages
  (`'Exactly 3 Basic Cards: deselect one first.'`) and on the start-pending
  message — all still 1 line; assert the ordering in more than one state
- An error is present (`startError` non-empty → `errorText` populated) in
  addition to a message — both must remain separated (today 17 px)
- The no-runtime path (`startError` set, no collection) renders the same
  layout — the offsets are fixed at shell creation, so confirm nothing
  depends on render order
- A collection re-render (`render()` runs again after load and after every
  selection) must not shift or duplicate the blocks: the bottom band is
  created once in `drawShell()`; verify the fix does not relocate it into the
  per-render path in a way that re-runs on every click
- `shutdown()` still nulls every text field (`LobbyScene.ts:282-290`) — a new
  field must be cleared there too
- The five Boss rows are still fully drawn and clickable (the 5th row's bottom
  at 531 is the upper bound of the band)

---

## Browser Smoke Requirements

A real-browser run is **required**, using the repository's existing CDP
conventions — the harnesses in `src/frontend/client/scripts/`
(`board-input-smoke.mjs` L379/408, `lobby-start-overlay-smoke.mjs`
L317/334, `boss-selection-smoke.mjs` L336/398: `--window-size=1280,720` plus
`Emulation.setDeviceMetricsOverride { width: 1280, height: 720,
deviceScaleFactor: 1 }`).

```text
Open application (dev auth, TASK-181)           → Lobby
  → select Pet + 3 Basic Cards + 3 Relics + a NON-default Boss with REAL
    pointer events, so reviewText/messageText carry realistic content
  → measure, without invoking any scene method:
        selectionText.getBounds()   (and .text)
        reviewText.getBounds()      (and .text)
        messageText.getBounds()     (and .text)
        errorText.getBounds()       (and .text)
        startTrigger.getBounds()    (the interactive rectangle, as
                                     lobby-start-overlay-smoke.mjs:185 already
                                     does for the button)
  → assert  selection.bottom + 4 <= review.top
            review.bottom    + 4 <= message.top
            message.bottom        <= error.top
            every block.bottom    <= 696
            errorText.top         <= 670 (or the AC-09 trade, stated)
            no block rect intersects the trigger rect
  → report the ACTUAL numbers (rect table + gaps), not "no overlap observed"
```

Requirements on the run:

- Geometry comes from the **rendered** objects (`getBounds()` /
  `x`/`y`/`width`/`height` on the live scene fields). Reading scene state is
  observation; **calling** `scene.requestStart()`, `selectBoss()`,
  `selectPet()`, `toggleCard()`, `toggleRelic()`, or any other callback is
  **not acceptable** as the way the UI is driven or as proof the UI works.
- `elementFromPoint` must **not** be used as the overlap/occlusion test
  (Technical Design Constraints item 5).
- The run must be actually performed; if the local environment cannot start
  the backend/frontend, report the criteria as **unverified** — never claim an
  unobserved result and never substitute a programmatic call.
- Reuse the existing scripts where they already cover this: they are the
  cleanest regression evidence for the TASK-182/183/185 items below.

### Regression Requirements

```text
TASK-182 — board interaction remains functional
           board-input-smoke.mjs must still pass, or boss-selection-smoke.mjs's
           board section (real clicks on adjacent cells → exactly one Swap per
           gesture → server-accepted)

TASK-183 — Runtime Status overlay remains `pointer-events: none` and does not
           block START BATTLE
           lobby-start-overlay-smoke.mjs must still pass, or the equivalent
           check in boss-selection-smoke.mjs (OVERLAY_STATE pointerEvents) —
           and the trigger's rect must be unchanged (AC-05)

TASK-185 — Boss selection remains functional
           boss-selection-smoke.mjs must still pass: five Bosses, none
           initially selected, review line 1 follows the selection, a
           complete loadout with no Boss submits nothing
```

Running the existing `boss-selection-smoke.mjs` covers all three regressions
plus reaches the Lobby with a realistic loadout; the new geometry assertions
may live in that script's snapshot or in a new focused script. Keep the new
tooling small — a new 700-line harness duplicating the existing one is not
wanted (`AGENTS.md` §9).

---

## Expected Files & Components

```text
[x] src/frontend/client/src/game/scenes/LobbyScene.ts
      EXPECTED — the only production file this task may change: the bottom
      band's positions/constants in `drawShell()` (and, only if the chosen
      mechanism requires it, position assignments in `render()`), plus
      `shutdown()` if a new field is introduced

[x] src/frontend/client/tests/LobbyScene.test.ts
      EXPECTED — focused layout coverage (the harness must stop discarding
      x/y, L239)

[x] src/frontend/client/scripts/<name>-smoke.mjs
      OPTIONAL — a focused geometry check, or assertions added to
      boss-selection-smoke.mjs / lobby-start-overlay-smoke.mjs

[ ] src/frontend/client/src/app/App.css
      NOT EXPECTED. Inspected for this task; the overlap is canvas-internal
      and no CSS participates. A change here means the fix went out of scope

[ ] src/frontend/client/src/game/GameViewport.ts / GameConfig.ts
      NOT EXPECTED — the viewport contract is authoritative and unchanged

[ ] Any other scene, runtime, service, state, or UI file — NOT EXPECTED
[ ] src/backend/**, migrations, tests/backend/** — FORBIDDEN by scope
[ ] docs/** — NOT EXPECTED (see Documentation Impact)
[ ] tasks/completed/** — IMMUTABLE, must not be edited
```

NOTE — path correction to the work request: it lists
`src/frontend/client/src/game/scenes/LobbyScene.test.ts`. **No such file
exists.** The Lobby suite is `src/frontend/client/tests/LobbyScene.test.ts`
(1091 lines, the harness quoted above). All other paths in the request were
verified as written.

### Documentation Impact

**None expected.** No `docs/` file states Lobby pixel coordinates:
`TDD.md` §2.1 and `GDD.md` §2 describe the *flow*, and `ARCHITECTURE.md`
§2.2.2 states the logical space and the safe-area rule — all of which remain
true and unchanged after the fix. Per `AGENTS.md` §17, no documentation change
is required because no documented behaviour changes. Do not edit `TDD.md`,
`GDD.md`, `ARCHITECTURE.md`, or any other document to "record" a new pair of
offsets; if the implementer believes a layout convention is worth documenting,
that is a reviewer/PO decision — stop and ask instead of deciding it.

---

## Git / Worktree Safety

`git status` at task creation (`master`) shows an intentionally dirty tree —
NOT a clean baseline, and none of it belongs to this task:

```text
 M docs/00-overview/GDD.md
 M docs/01-game-design/RELIC_RULES.md
 M docs/02-technical/ARCHITECTURE.md
 M docs/02-technical/DATABASE.md
 M docs/02-technical/TDD.md
 M src/frontend/client/src/app/App.css
 M src/frontend/client/src/game/scenes/LobbyScene.ts          ← this task's file
 M src/frontend/client/tests/LobbyScene.test.ts               ← this task's file
 M src/frontend/client/tests/RuntimeBoundaries.test.ts
 M src/frontend/client/tests/ViewportCss.test.ts
 M tests/backend/GameServer.Api.Tests/BattleStartEndpointTests.cs
 M tests/backend/GameServer.Infrastructure.Tests/PetCardRelicDefinitionPostgresProvisioningTests.cs
 M tests/backend/GameServer.Infrastructure.Tests/RelicDefinitionLookupTests.cs
 M tests/backend/GameServer.Infrastructure.Tests/RelicProvisionedContent.cs
 M tests/backend/GameServer.Infrastructure.Tests/RelicStructuredStorageTests.cs
 ?? src/backend/GameServer.Infrastructure/Postgres/Migrations/20261004153916_ProvisionRemainingMvpRelicDefinitions.cs
 ?? src/backend/GameServer.Infrastructure/Postgres/Migrations/20261004153916_ProvisionRemainingMvpRelicDefinitions.Designer.cs
 ?? src/frontend/client/scripts/boss-selection-smoke.mjs
 ?? src/frontend/client/scripts/lobby-start-overlay-smoke.mjs
 ?? tasks/completed/TASK-185-implement-mvp-boss-selection-in-lobby.md
 ?? tests/backend/GameServer.Infrastructure.Tests/RemainingMvpRelicDefinitionProvisioningTests.cs
```

The uncommitted work above is TASK-184/TASK-185-era work (Boss selection, the
Relic migration and its tests, the TASK-185 documentation reconciliation,
TASK-183's overlay CSS/comments, and the two smoke scripts). It is **not
committed**, so `git diff` cannot distinguish "TASK-185's change" from
"HEAD": read the current files, not a reconstructed baseline.

The implementing agent MUST:

- **not** revert, stash, reset, check out, clean, or "tidy" any of it —
  no `git stash`, `git checkout --`, `git reset`, `git restore`, `git clean`,
  or `git commit -a`;
- **not** modify the Relic migration work in any way
  (`20261004153916_ProvisionRemainingMvpRelicDefinitions.*`,
  `RemainingMvpRelicDefinitionProvisioningTests.cs`, the Relic test edits);
- **not** modify TASK-185 behaviour (five-step flow, Boss catalog, review
  line 1, `selectedBossId` semantics, `describeIncompleteSelection()`) or
  TASK-185's completed task file;
- **not** modify `tasks/completed/**` at all (immutable historical record);
- keep the change confined to the files listed in "Expected Files &
  Components", and stage only those files if staging is requested;
- if a pre-existing uncommitted change already fixes part of this defect
  (i.e. the measurements in "Current Geometry" no longer reproduce), **STOP**
  and report that per Stop Condition 6 instead of layering a second fix.

---

## Stop Conditions

Universal stop conditions (`AGENTS.md` §20, `.ai/README.md` §13) always apply.
Stop, set the task BLOCKED, and report — do not guess — if any of these fires:

1. **The reported overlap cannot be reproduced or located.** Re-measure first:
   `selectionText`/`reviewText` at 564/600 with 3/4 lines and 15 px line height;
   if the live geometry disagrees, record what was actually measured and stop.
2. **The three blocks are generated by a different layout system than
   expected.** If they are not the Phaser `Text` objects created in
   `LobbyScene.drawShell()` (or the canvas is no longer the renderer for them),
   stop and report the actual mechanism.
3. **Fixing the overlap would require changing the canonical viewport** — the
   1280 × 720 logical space, the 24 px safe-area margin, or the FIT scaling
   model (`ARCHITECTURE.md` §2.2.2). That is an architecture change, not this
   task.
4. **Fixing the overlap would require changing gameplay behaviour** — any
   rule, value, selection semantic, request member, or scene transition.
5. **Fixing the overlap would require changing backend, API, or protocol
   behaviour** — any endpoint, payload member, SignalR message, or migration.
6. **The issue is already fixed by another uncommitted change** in the tree.
   Do not stack a second fix on top of it; report and let a human reconcile.
7. **A conflicting authoritative UI specification cannot be reconciled
   without a Product Owner decision** — including: no overlap-free
   arrangement exists inside the safe area for the four blocks at their real
   heights without moving the Start Battle trigger, the Boss band, the
   collection columns, the shell, or the Runtime Status overlay; or a correct
   fix would require changing *what* `reviewText` displays (e.g. dropping the
   submitted-instance identities for names, or hiding lines).
8. **The fix appears to require touching anything in the NON-GOALS** — the
   trigger, the Boss band, the columns, `App.css`, `GameViewport.ts`, the
   overlay, `TASK-182`/`TASK-183`/`TASK-185` behaviour, backend, auth, or
   protocol. Those are other tasks' scope. Report the exact requirement.
9. **The fix would clip, truncate, ellipsize, shrink, or wrap-hide any block's
   content**, or would make a block's height variable without budgeting the
   worst documented loadout (AC-10).
10. **The real-browser run cannot be executed** in this environment (backend,
   dev auth, or the browser harness unavailable). Report the affected
   criteria as **unverified**. Do not substitute a programmatic scene call,
   a screenshot eyeball, or `elementFromPoint` for the geometry check, and do
   not claim an unobserved result.
11. If this task would exceed the simple-task skill budget (4) or cross an
   uncoupled architectural boundary: **STOP & decompose**.

---

## Final Report Requirements

The implementing agent reports, concisely and factually (`AGENTS.md` §21),
with actual observed values:

```text
## Status
IN REVIEW / BLOCKED (with the stop condition that fired, quoted)

## Changes
- exact files and what moved (old offset → new offset/named constant)

## Root Cause Restated Or Corrected
- whether the measured geometry matched this task's "Current Geometry";
  state any corrected number explicitly

## Layout Contract Implemented
- the chosen mechanism and why (fixed offsets vs measured-height derivation)
- the gap convention chosen and its justification against the 165 px budget
- the worst-case line counts assumed for each block

## Tests
- new/changed test names + what relationship they assert
- npx tsc --noEmit / npm run test:run / npm run build results (counts)

## Browser Geometry Evidence (AC-08/AC-09)
- the ACTUAL rect table: block, text, x, y, width, height (game px)
- selection.bottom→review.top and review.bottom→message.top gaps
- message.bottom→error.top gap, error.top, and the trigger rect intersection
  result (must be none)
- which states were measured (default; selection message; error present;
  5-Relic loadout if wrapping was added)

## Regression Evidence
- TASK-182 board input: command + observed result
- TASK-183 overlay `pointer-events: none` + trigger clickability: command +
  observed result
- TASK-185 Boss selection + review line 1: command + observed result

## Documentation
- "No documentation change required (AGENTS.md §17): no documented behaviour
  changed" — or the conflict that was found and reported instead

## Remaining Issues (report, do not fix)
- the review block's horizontal overflow at 4–5 long Relic instance
  identities (pre-existing, AC-10)
- the Runtime Status panel's visual overlap with the bottom-right corner
  (TASK-183 territory)
- any other measurement surprise, with file:line

## Confirmation
- no gameplay, state, transport, backend, schema, auth, CSS, or viewport
  change; the worktree's unrelated uncommitted work untouched
```

---

## Unrelated Findings (record, do not fix)

Per `AGENTS.md` §16 — found while measuring for this task, deliberately out of
scope:

1. **`reviewText` has no word wrap and can overflow horizontally.** With 4–5
   selected Relics, `'Relics: ' + relicinst_<32 hex>` lines reach ~1062 px
   (3 Relics) and ~1740 px (5 Relics, the documented maximum) from x = 50,
   past the safe-area right edge (1256) and the canvas edge (1280).
   `messageText`/`errorText` are wrapped (`LobbyScene.ts:630/633`);
   `reviewText` is not. Impact: the last identity/identities are unreadable in
   the 4–5 Relic case. Suggested follow-up: a separate presentation task —
   **not** this one, because adding wrap changes the block's height and thus
   this task's budget (AC-10, constraint 3).
2. **The Runtime Status panel may visually cover the Start Battle trigger.**
   `.status-overlay-card` is opaque and anchored bottom-right
   (`App.css:120-147`) while the trigger occupies x 930–1230, y 578–622. The
   TASK-183 fix is `pointer-events: none`, so the trigger is *clickable* but
   its visibility depends on the panel's runtime height; and because the panel
   opts out of hit-testing, `elementFromPoint` cannot detect the occlusion.
   Impact: a possible invisible-button condition. Suggested follow-up: a
   TASK-183-scoped presentation task, measured as geometry (panel rect vs
   trigger rect) — not this one.
3. **The work request's `reviewText`/`messageText` overlap figure is wrong
   (22 px, not ~7 px)** because `reviewText` is 4 lines, i.e. 60 px tall. The
   other two figures reproduce exactly. Recorded so the number is not carried
   forward as evidence.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual. Report the ACTUAL observed result.
-->

### Changed Files
- `<file path>` — <summary of change>

### Validation Results
- `npx tsc --noEmit` — PASS / FAIL
- `npm run test:run` — PASS (<N> tests, <N> files)
- `npm run build` — PASS / FAIL
- `node scripts/<harness>.mjs http://localhost:5173/` — PASS (<N> checks, <N> failures)

### Browser Text Geometry Verification (AC-08)

Report the actual measured numbers:

```text
viewport / device metrics       : 1280 x 720, deviceScaleFactor 1
block            text (first line)      x     y     width  height  bottom
---------------  ---------------------  ----  ----  -----  ------  ------
selectionText    Pet:   petinst_…       50     ?      ?      ?       ?
reviewText       5. REVIEW — Boss: …    50     ?      ?      ?       ?
messageText      START BATTLE — …       50     ?      ?      ?       ?
errorText        (none)                 50     ?      ?      ?       ?
startTrigger     START BATTLE           930   578   300    44      622
---------------  ---------------------  ----  ----  -----  ------  ------
selection.bottom → review.top gap   : ? px   (AC-01, must be >= 4)
review.bottom    → message.top gap  : ? px   (AC-02, must be >= 4)
message.bottom   → error.top gap    : ? px
trigger rect ∩ any block rect       : NONE / <the intersection found>
```

### Server Authority & Scope Verification
- [ ] Confirmed zero client-authoritative gameplay logic added or changed
- [ ] Confirmed `BattleStartRequest`'s members and values are unchanged
- [ ] Confirmed the Start Battle trigger's rect, behaviour, and styling are unchanged
- [ ] Confirmed the five-step flow, Boss catalog, and Boss selection are unchanged
- [ ] Confirmed `App.css`, `GameViewport.ts`, `GameConfig.ts`, `BattleScene`,
      `GameRuntime`, services, state, and `src/backend/` are untouched
- [ ] Confirmed no gameplay rule, schema, migration, protocol, or auth change
- [ ] Confirmed the worktree's unrelated uncommitted work was not touched
- [ ] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)

---

## Revision History

**Revision 1 — task created (BACKLOG).** Generated from TASK-185's browser
verification record.

Three findings shaped this specification rather than being copied from the
work request:

1. **The offsets are not "hardcoded" in the sense the request warns about** —
   they are derived from `SAFE_AREA`, so they scale with the viewport and a
   viewport change is not the issue. The defect is that the gaps were never
   derived from the blocks' *rendered heights*: 36 px and 38 px were allocated
   where 45 px and 60 px are drawn. The task states the root cause in those
   terms so the fix is measured against heights, not against taste.
2. **One of the request's three overlap figures is wrong.** `reviewText` is
   4 lines = 60 px, so the `reviewText`/`messageText` overlap is 22 px, not
   "~7 px". The 9 px `selectionText`/`reviewText` figure and both block tops
   reproduce exactly with a measured 15 px line height.
3. **The bottom band is nearly full (165 px for 135 px of text plus 4 gaps),
   and two immovable neighbours bound it** — the 5th Boss row's bottom edge
   (531) and the Start Battle trigger (whose draw order means wide lines
   inside y 578–622 are painted over). The task therefore specifies the
   *contract* and the measured constraints, not one particular offset
   arrangement, and lists "no overlap-free arrangement exists" as an explicit
   stop condition rather than pre-authorizing a move of the trigger, the Boss
   band, or the overlay.

Also corrected: the request's test path
(`src/frontend/client/src/game/scenes/LobbyScene.test.ts`) does not exist; the
suite is `src/frontend/client/tests/LobbyScene.test.ts`.
