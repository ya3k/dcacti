# TASK-182 — Fix Battle Board Canvas Input Registration

<!--
  GEN-TASK EXECUTION MANIFEST — BUG FIX TASK
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  References docs/ and src/ by path and section — it does NOT copy game
  rules, formulas, magnitudes, schemas, or payload shapes as new authority.

  SCOPE OF THIS TASK: make the already-implemented Match-3 board actually
  receive pointer input in a real browser, by correcting the Phaser input
  registration of the existing board layer.

  IT ADDS NO GAMEPLAY. IT CHANGES NO RULE. IT CHANGES NO PROTOCOL.
  IT TOUCHES NO BACKEND SOURCE. IT TOUCHES NO AUTHENTICATION.

  READ "Root Cause" AND "Disproved Hypotheses" BEFORE PLANNING: the
  diagnosis recorded by TASK-181's smoke test is INCOMPLETE and, taken
  literally, would produce a no-op fix. The `boardLayer === null` early
  return is real code, but it is NOT the live defect.
-->

---

## Metadata

```text
Task ID:           TASK-182
Title:             Fix Battle Board Canvas Input Registration
Status:            DONE
Type:              Bug Fix (development/bug-fix.md §1 — Implementation bug)
Priority:          High
Risk:              LOW (one presentation-layer file plus tests; no protocol,
                   no backend, no gameplay rule, no schema, no auth touched)
Source:            TASK-181 browser smoke test
Primary Agent:     client (BattleScene presentation lifecycle)
Supporting Agents: review (confirms no gameplay/authority boundary is crossed),
                   testing (input-registration regression coverage)
Workflow:          development/bug-fix.md
Skills:            phaser/input-keyboard-mouse-touch,
                   phaser/groups-and-containers,
                   client/phaser-match3,
                   client/phaser-battle-presentation,
                   quality/scope-validation,
                   testing/test-scenario-generation
Dependencies:      TASK-181 (source of this work; its development
                   authentication is consumed unchanged and is NOT modified)
```

---

## Objective

Make the existing 8 x 8 Match-3 board respond to a normal mouse (and touch)
interaction on the browser canvas, so that a real pointer gesture reaches the
**existing**, unchanged authoritative swap path:

```text
real pointer input
    ↓
BattleScene board handler
    ↓
GameRuntime.requestAction(Swap)
    ↓
SignalRService.swap
    ↓
BattleHub.Swap
    ↓
authoritative server
```

The server-side swap implementation already works and is **not** the subject
of this task. TASK-181's smoke test verified 8/8 valid swaps accepted
(sequence 0 → 8, board changed, `matchCount` 0 → 13, combo = 1,
`bossHp` 5000 → 4657) through `SignalRService.swap()`. What has never worked
is the **canvas input** that is supposed to call it.

**Everything downstream of `onCellTapped` is already implemented and correct.**
This is an input-registration defect, not a gameplay, protocol, or server
defect.

---

## Root Cause

### The defect

`BattleScene.registerBoardInput()` attaches the board's pointer handler to the
`boardLayer` container:

```ts
this.boardLayer.on(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN, (pointer) => {
  this.onCellTapped(BattleScene.cellIndexAt(pointer.x, pointer.y));
});
```

A container is only a valid input target when it has a hit area. Phaser
requires `setSize(width, height)` (or an explicit hit area) **before** the
object can be picked by the input system. `boardLayer` is created as a bare
`this.add.container(0, 0)` and is **never** given a size and **never**
`setInteractive()`-enabled:

```text
src/frontend/client/src/game/scenes/BattleScene.ts
  line 281   this.boardLayer = this.add.container(0, 0);   ← no setSize
  line 546   registerBoardInput()                          ← .on(...) only
```

Consequently `boardLayer` is not an input-enabled Game Object, Phaser's input
system never picks it, and `GAMEOBJECT_POINTER_DOWN` is never emitted. The
handler is registered on an emitter that can never fire — so **no pointer
event ever reaches `onCellTapped`**, and no swap request is ever produced by a
real gesture.

This is corroborated by the repository's own Phaser 4 guidance, which states
the requirement in three independent places:

```text
.ai/skills/phaser/input-keyboard-mouse-touch/SKILL.md:145
    "Containers must specify a shape or call setSize first"
    container.setSize(200, 200);
    container.setInteractive();

.ai/skills/phaser/groups-and-containers/SKILL.md:220
    setSize(width, height) | Set hit area size (required for input)

.ai/skills/phaser/groups-and-containers/SKILL.md:402
    13. Container needs setSize() for input. Containers have no implicit
    size. You must call container.setSize(width, height) before
    setInteractive() will work with a hit area.
```

`boardLayer` is the **only** container in the scene used as an input target;
`castControlsLayer` places its input on the child `tile` / `label` rectangles
that already call `setInteractive(...)` (line ~441), which is why card/skill
buttons work while the board does not.

### Disproved hypotheses — do not "fix" these

**H1 — "`registerBoardInput()` runs before `boardLayer` exists."**

The TASK-181 smoke test reported this sequence:

```text
BattleScene.create() → registerBoardInput() → boardLayer is null → early return
```

The null-guard at line 547 is real, and `registerBoardInput()` is indeed
called at line 140. **But the ordering is already correct.** `drawRuntimeShell()`
is called on line 139 and creates `boardLayer` on its last line (281); only
then does line 140 call `registerBoardInput()`. At that moment `boardLayer` is
a non-null container, the guard passes, and `.on(...)` **does** execute.

Therefore moving the call, deferring it to `renderBoard`, or removing the
guard changes nothing observable. What the smoke test actually observed was
the *consequence* of the missing hit area (no pointer event reaching a
registered-looking handler), not an ordering fault. **Verify this in the
current code before planning** — do not implement a reordering fix on the
strength of the TASK-181 summary.

The guard itself is defensible defensive code and may remain; it is simply not
the cause.

**H2 — "The bug is in the swap request path."**

Disproved by TASK-181: 8/8 swaps accepted through the existing path. The
runtime port, `SignalRService.swap`, the hub method, validation, and
authority all behave correctly. Do not modify them.

**H3 — "The board redraw (`removeAll(true)`) destroys the input registration."**

`removeAll(true)` destroys the layer's *children*; the container and its own
listener survive, and the listener is on the container, not the cells.
Nevertheless, any redraw/reconnect re-entry must not add a second listener —
see the duplicate-handler requirement below.

---

## Evidence That The Existing Tests Cannot Catch This

`tests/SceneLifecycle.test.ts` already contains a full `BattleScene — Swap
input` suite, and it **passes today** — including
`it('registers board pointer input')`, which asserts
`boardInputHandlers.has('gameobjectdown')`.

It passes because the jsdom harness mocks the container's `on` as a plain map
write and then invokes the captured handler directly
(`tapCell` → `boardInputHandlers.get('gameobjectdown')`). The mock
`makeContainer()` **has no `setSize` and no `setInteractive`**, so the harness
cannot represent "this container is not an input target" and therefore cannot
fail on the real defect.

```text
tests/SceneLifecycle.test.ts
  line 175   const makeContainer = () => { ... }   ← no setSize/setInteractive
  line 197   on: (event, handler) => boardInputHandlers.set(event, handler)
  line 361   tapCell() invokes the handler directly, bypassing Phaser picking
```

This is the single most important fact for planning the tests: **the existing
suite is not evidence that canvas input works.** A passing `Swap input` suite
coexists with a board that is completely uninteractive in the browser. The new
tests must therefore assert on the *input-enabling* contract (the layer is an
interactive container with a hit area covering the board), not merely that a
handler was handed to `on`.

This is a Test bug in the sense of `development/bug-fix.md` §1 — the harness
asserts the wrong thing — but the fix is to **strengthen** the harness, never
to weaken an assertion (`AGENTS.md` §15).

---

## Authoritative References

- `docs/01-game-design/MATCH3_RULES.md` §1.0 (row-major `index = row * 8 + column`), §2 item 1 (tap-to-select interaction), §2.1.1 (the Swap request's two cells), §2.1.2 (server-side validation — unchanged), §2.1.3 item 2 (nothing is clamped), §2.1.5 (rejection is a no-op), §5 item 2
- `docs/02-technical/SIGNALR_PROTOCOL.md` §2.1 (the Swap request), §3.1 / §4 (the authoritative `BattleStateUpdated` push that re-renders the board), §4 item 10 (no client-generated board), §5 (acknowledgement/rejection feedback), §8.3 (transport failure is presentation state)
- `docs/02-technical/ARCHITECTURE.md` §2.2 rule 3 (the scene talks to `GameRuntime`, never to SignalR), §2.2.2 (presentation-layer mapping is the client's concern), §5 (anti-overengineering)
- `AGENTS.md` §10 / `GAME_RULES.md` §18 / `ADR-001` (server authority — the client requests, never decides)
- `ADR-003` (Phaser renders and owns client-side battle presentation)
- `docs/00-overview/MVP_SCOPE.md` §1 — the loop this task restores; §2 / §4 — nothing may be added
- `.ai/workflow/development/bug-fix.md` §2–§4 — the workflow and the "never change behavior to make a test pass" rule

---

## Scope

### In Scope

- Making the existing `boardLayer` a valid Phaser input target so the board's
  already-written pointer handler can actually fire (AC-01, AC-02).
- Preserving the existing pointer semantics: the same
  `GAMEOBJECT_POINTER_DOWN` handler, the same `cellIndexAt` coordinate
  inverse, the same two-tap select-then-swap contract, the same
  out-of-cell/gap ignore behavior (AC-02, AC-03, AC-07).
- Ensuring repeated `drawRuntimeShell()` / redraw / scene re-entry cannot
  register the handler more than once (AC-04).
- Regression tests at the lifecycle/input-registration boundary, including
  strengthening the jsdom harness so the defect is representable (AC-10).
- Confirming the frontend build passes and the existing suites still pass
  (AC-10, AC-11).
- An **actual browser** smoke test driven by a **real canvas gesture**,
  using TASK-181's existing development authentication (AC-12, AC-13).

### Out of Scope

- Any change to `GameRuntime.requestAction`, `SignalRService.swap`, the hub
  contract, or the Swap payload shape
- Any change to server-side swap validation, adjacency, or state mutation
- Any gameplay change: board dimensions, Gem types, matching, cascade, combo,
  special gems, turn order, damage, Boss, Card, Relic, or Pet behavior
- Any authentication change — TASK-181's development path and the Discord path
  both stay exactly as they are
- New gesture semantics (drag-to-swap, swipe thresholds, hold, multi-select)
  that the current implementation does not already define
- ResultScene navigation, campaign/stage, energy/tickets, RPS, Speed, Boss
  selection, six-Relic provisioning
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Acceptance Criteria

### AC-01 — Board input target is valid
The board layer is a Phaser input-enabled Game Object with an explicit hit
area covering the full drawn board extent (8 columns x 8 rows at the existing
cell pitch), established before any pointer handler is attached to it. The
existing null-guard behavior is not the fix and is not treated as one.

### AC-02 — Normal mouse interaction reaches the board swap path
A normal left-button mouse click on a drawn cell, delivered through Phaser's
real input system on the canvas, resolves to that cell's `MATCH3_RULES.md`
§1.0 index and is handled by the existing `onCellTapped` contract: first tap
selects, second tap on a different cell submits the pair.

### AC-03 — Touch / pointer compatibility
The existing implementation uses Phaser pointer events, which cover mouse and
touch uniformly. The fix introduces no mouse-only or touch-only branch and
does not change the event type, so touch behavior remains whatever Phaser
delivers for the same handler. No new gesture semantics are added.

### AC-04 — No duplicate pointer handlers
The board's pointer handler is registered at most once, regardless of how many
times `drawRuntimeShell()`, `renderBoard()`, a redraw, or a scene re-entry
occurs. A repeated shell draw must not produce `handler × 2`, `handler × 3`,
or `handler × N`, and must never produce duplicate swap requests for one
gesture. The existing lifecycle/disposal pattern (`shutdown()`) is used or
extended; no new abstraction is introduced.

### AC-05 — Authenticated swap request
A valid two-cell interaction produces exactly one Swap request through the
existing `GameRuntime.requestAction({ kind: 'Swap', fromCell, toCell })` path
and therefore exactly one authenticated `SignalRService.swap` call.

### AC-06 — Server remains authoritative
The scene changes no board, turn, sequence, HP, or combo value on request or
on acknowledgement. The board is re-rendered only from the authoritative
`BattleStateUpdated` push. No client-side match, cascade, or damage logic is
introduced.

### AC-07 — Existing invalid-input behavior unchanged
Taps outside the board and taps in the gap between cells still resolve to no
cell and are ignored (nothing selected, nothing sent, nothing clamped). A tap
on the already-selected cell still clears the selection instead of submitting.
A non-adjacent pair is still sent as selected and left entirely to the
server's validation. Server rejections are still a local no-op with the
machine-readable reason shown as feedback.

### AC-08 — No gameplay rules modified
No board dimension, Gem type, matching rule, cascade rule, combo rule, special
gem, turn order, damage formula, Boss behavior, Card, Relic, or Pet behavior is
changed. The four documented Gem names and the 8 x 8 / 64-cell contract are
untouched.

### AC-09 — No backend source changes
The task requires zero changes under `src/backend/`. The API, hub, domain,
persistence, and authentication code are all unmodified.

### AC-10 — Frontend tests pass, including new regression coverage
The frontend suite passes (`npm run test:run`). New or updated tests cover, at
minimum: (1) the board layer is enabled as an input target with a hit area and
input registration happens after the layer exists; (2) input is not silently
skipped because `boardLayer` is null; (3) a board interaction at a real cell
coordinate reaches the existing `requestAction` swap path; (4) duplicate
registration cannot produce duplicate swap requests; (5) existing BattleScene
lifecycle tests continue to pass. Tests must not depend on arbitrary timing.
No existing assertion may be weakened or deleted to make the fix fit
(`AGENTS.md` §15, `development/bug-fix.md` §4).

### AC-11 — Frontend build passes
`npm run build` (`tsc && vite build`) passes with no type errors.

### AC-12 — Real browser canvas interaction verified
Using TASK-181's existing development authentication in a normal browser tab,
the documented flow is executed and the result is **actually observed and
reported** — not asserted from intent:

```text
1.  Start backend
2.  Start frontend
3.  Open a normal browser tab
4.  Development authentication succeeds
5.  Enter Lobby
6.  Start Battle
7.  SignalR connects
8.  Board renders
9.  Perform an ACTUAL mouse gesture on the canvas   ← the TASK-181 difference
10. A swap request is generated
11. The server accepts the swap
12. Board state changes
13. No duplicate swap request is generated
```

Step 9 must be a genuine pointer interaction with the canvas. A manually
invoked `SignalRService.swap()` or any direct JavaScript call does **not**
satisfy this criterion — that is precisely what TASK-181 already proved and
what this task exists to move past. If the environment permits mouse input but
not realistic touch input, report the touch result separately as unverified
rather than claiming it.

### AC-13 — TASK-181 authentication unchanged
The development authentication mechanism, its configuration, its
environment-scoping, and the Discord path are all unmodified. No secret
handling changes.

### Baseline criteria

- [ ] All relevant tests pass at the required validation depth
- [ ] Quality review checklist passes
- [ ] No authoritative rule or contract violated (`AGENTS.md` §10 / ADR-001)
- [ ] No unrelated behavior changed (`AGENTS.md` §16)

---

## Current State

```text
src/frontend/client/src/game/scenes/BattleScene.ts
  line 108   private boardLayer: Phaser.GameObjects.Container | null = null;
  line 137   create()            ← drawRuntimeShell(); then registerBoardInput();
  line 139     drawRuntimeShell()
  line 140     registerBoardInput()
  line 165   shutdown()          ← destroys boardLayer, clears input state
  line 196   drawRuntimeShell()  ← creates the board layer last
  line 281     this.boardLayer = this.add.container(0, 0);   ← NO setSize, NO setInteractive
  line 282     this.castControlsLayer = this.add.container(0, 0);
  line 283     this.feedbackLayer = this.add.container(0, 0);
  line 441     .setInteractive({ useHandCursor: true });     ← cast tiles DO setInteractive
  line 442     tile.on(GAMEOBJECT_POINTER_DOWN, onTrigger)   ← so buttons work
  line 467   renderBoard()   ← boardLayer.removeAll(true); redraws 64 cells
  line 504   drawCell()      ← x/y from BOARD_ORIGIN + pitch (the §1.0 inverse)
  line 546   registerBoardInput()  ← guard passes; .on() attaches to a
                                     non-interactive container ⇒ never fires
  line 575   cellIndexAt()   ← the index inverse; correct, reuse unchanged
  line 614   onCellTapped()  ← select / clear / submit; correct
  line 649   submitSwap()    ← runtime.requestAction({ kind: 'Swap', ... });
                               correct, reuse unchanged
```

Unchanged collaborators that must be reused as-is:

```text
src/frontend/client/src/game/runtime/GameRuntime.ts
  line 398   requestAction()        ← validates kind, sources battleId
  line 428   RUNTIME_ACTION_SWAP    → this.signalR.swap(battleId, from, to, seq)
src/frontend/client/src/game/runtime/GameRuntimeEvents.ts
  line 458   RUNTIME_ACTION_SWAP = 'Swap'
src/frontend/client/src/services/realtime/SignalRService.ts
  line 707   swap(battleId, fromCell, toCell, clientSequence)
```

Test seams that must be strengthened rather than bypassed:

```text
src/frontend/client/tests/SceneLifecycle.test.ts
  line 15    vi.mock('phaser', ...)          ← mocked Phaser surface
  line 175   makeContainer()                 ← no setSize / setInteractive
  line 197   on: (event, handler) => ...     ← records handlers in a Map
  line 361   tapCell()                       ← invokes the handler directly,
                                               bypassing Phaser input picking
  line 1051  'registers board pointer input' ← passes today despite the defect
```

---

## Technical Design Constraints

Prefer the **smallest correct change**, consistent with `ARCHITECTURE.md` §5
and `AGENTS.md` §9.

The fix must make the existing container a legitimate input target. Inspect
the installed Phaser 4.2.1 API (`node_modules/phaser/types/phaser.d.ts`,
`Phaser.GameObjects.Container`) and the repository's own skills before
choosing the mechanism. Two are documented and both are acceptable if they
satisfy AC-01 and leave `cellIndexAt` correct:

```text
(a) size the container to the board extent and enable input on it, keeping the
    existing container-level GAMEOBJECT_POINTER_DOWN handler; or
(b) give the container an explicit hit area (e.g. a rectangle covering the
    board extent) via setInteractive(hitArea, callback), keeping the same
    handler and the same pointer-local coordinate space.
```

Whichever is chosen, the pointer coordinates the handler receives must remain
the board-layer-local space `cellIndexAt` already inverts — if the hit area or
container origin changes that space, `cellIndexAt` must be kept consistent
with `drawCell`, because both derive from the single `MATCH3_RULES.md` §1.0
convention. Verify this explicitly rather than assuming it.

Constraints that bind the implementation:

- Do **not** add a scene-level `this.input.on('pointerdown', ...)` listener as
  the board's input path unless it is proven correct; it changes the input
  model, and the existing contract is a game-object pointer event on the board
  layer.
- Do **not** make each of the 64 cells individually interactive as a
  workaround unless the container approach is proven unworkable — that is 64
  input targets to maintain and re-create on every redraw, and it is a larger
  change than the defect requires. If it becomes necessary, STOP and report
  why before implementing it.
- Do **not** introduce drag/swipe gesture semantics. The implemented contract
  is tap-to-select then tap-to-swap (`MATCH3_RULES.md` §2 item 1). Adding
  drag would be inventing interaction behavior, not fixing registration.
- Do **not** add a second swap path, an optimistic board mutation, or any
  client-side match/resolution logic.
- Keep the change inside the presentation layer. `GameRuntime`,
  `SignalRService`, and everything under `src/backend/` stay untouched.

---

## Testing Requirements

### Required Verification

```text
[x] Unit / lifecycle tests — the board layer is input-enabled with a hit area
                             covering the board extent; registration occurs
                             after the layer exists; registration is not
                             skipped; duplicate draw cannot double-register;
                             repeated gestures never double-submit
[x] Boundary tests         — a pointer event at a real cell coordinate reaches
                             GameRuntime.requestAction with the correct two
                             §1.0 indices as the only payload
[ ] Integration tests      — N/A here beyond the browser smoke test: the
                             authenticated transport path is already covered
                             by the existing suites and is unchanged
[ ] Gameplay scenarios     — N/A: this task changes no gameplay behavior
```

The jsdom harness in `tests/SceneLifecycle.test.ts` must be **extended** so the
defect is representable — at minimum, the mocked container must model
`setSize` / `setInteractive` state, and the board input test must assert the
layer was actually enabled as an input target (AC-01), not merely that `on`
was called. Adding a new focused test file is acceptable if it is the cleaner
seam; duplicating the whole harness is not.

Do not write tests that depend on arbitrary timing, real timers, or
frame-dependent behaviour. Drive the lifecycle synchronously the way the
existing harness does, and keep the existing `flush()` pattern for promise
settlement.

### Key Edge Cases

- A gesture in the gap between two cells still selects and sends nothing
- A gesture outside the board extent (including just beyond the last row or
  column) still resolves to no cell and is ignored, never clamped
- A second tap on the selected cell still clears the selection and sends nothing
- A non-adjacent pair is still submitted and left to the server's validation
- A rejected swap still mutates nothing locally and shows the server's reason
- A transport failure still reports feedback and sends no second request
- `drawRuntimeShell()` called twice still yields exactly one board handler
- A gesture during an in-flight action is still ignored (`isInputLocked()`)
- Scene `shutdown()` still detaches cleanly and leaves no live handler

---

## Stop Conditions

- If the fix appears to require changing the Swap payload shape, the hub
  method, or server-side validation: **STOP** — the server correctly accepts
  swaps today (TASK-181) and that path is out of scope
- If the fix appears to require a backend change, a schema change, or a
  migration: **STOP** and report it as an unplanned requirement
- If the fix appears to require a gameplay rule that is not documented:
  **STOP** per `AGENTS.md` §7 — record the missing rule, do not invent it
- If satisfying AC-04 appears to require a new lifecycle abstraction beyond
  the existing `shutdown()` pattern: **STOP** and report, rather than
  introducing one
- If the defect is found to be a Phaser 4.2.1 lifecycle/input architecture
  problem rather than a local registration defect: **STOP** and report the
  finding before choosing an implementation — this is the escalation
  condition the task's recommended model/effort assumes will not occur
- If AC-12 cannot be executed because the local environment is unavailable:
  report it as unverified. Do **not** claim a browser result that was not
  observed, and do not substitute a programmatic `swap()` call for it
- If any authentication mechanism would have to change to run the smoke test:
  **STOP** — that is TASK-181 scope and is explicitly excluded

---

## Completion Evidence

### Changed Files
- `<file path>` — <summary of change>

### Validation Results
- `<test command or suite>` — PASS (<N> tests)
- `npm run build` — PASS

### Browser Canvas Input Verification

Report the **actual** observed result, not the intended one. An unverified
claim of success is a failed criterion.

```text
[ ] Backend started (state how the TASK-181 signing secret was supplied — never the value)
[ ] Frontend started and opened in a normal browser tab
[ ] Development authentication reached an authenticated session
[ ] Lobby reached
[ ] Battle started
[ ] SignalR connected
[ ] Board rendered (64 cells)
[ ] ACTUAL mouse gesture performed on the canvas (state the cells and how the
    gesture was delivered — real pointer input, not a JS call)
[ ] Swap request generated by the gesture (observed, with evidence)
[ ] Server accepted the swap
[ ] Board state changed from the authoritative push
[ ] No duplicate swap request observed for the single gesture
[ ] Touch input: verified / NOT verifiable in this environment (state which)
```

### Server Authority & Scope Verification
- [ ] Confirmed zero client-authoritative gameplay logic added
- [ ] Confirmed the Swap request shape and the hub contract are unchanged
- [ ] Confirmed the board is re-rendered only from the authoritative push
- [ ] Confirmed `GameRuntime`, `SignalRService`, and `src/backend/` are untouched
- [ ] Confirmed no gameplay rule, schema, migration, or doc change
- [ ] Confirmed TASK-181 authentication is unchanged
- [ ] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)

---

## Revision History

**Revision 1 — task created.** Generated from the TASK-181 browser smoke test's
finding that the Match-3 board does not respond to canvas interaction.

Two findings shaped the specification rather than being copied from the work
request that generated it:

1. **The recorded root cause is incomplete.** The request describes
   `registerBoardInput() → boardLayer === null → early return` as the defect.
   Inspection of the current code shows `drawRuntimeShell()` (line 139) already
   runs before `registerBoardInput()` (line 140) and creates `boardLayer` at
   line 281, so the guard passes and `.on(...)` does execute today. A
   reordering fix would be a no-op. The live defect is that `boardLayer` is a
   bare `this.add.container(0, 0)` that is never given a hit area and never
   `setInteractive()`-enabled, so Phaser never picks it and the registered
   handler can never fire. The task records the disproved hypotheses
   explicitly so the implementing agent does not ship a reordering change.
2. **The existing `Swap input` test suite cannot detect this defect.** It
   passes today because the jsdom harness invokes the captured handler
   directly and its mocked container has no `setSize` / `setInteractive`. The
   task therefore requires the harness to be strengthened to assert the
   input-enabling contract, and states plainly that a green suite is not
   evidence that canvas input works.
