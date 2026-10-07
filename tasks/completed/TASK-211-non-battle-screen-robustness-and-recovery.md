# TASK-211 — Non-Battle Screen Robustness & Recovery

---

## Metadata

```text
Task ID:           TASK-211
Type:              BUG (TASK_TYPES.md §2 — the P1 defects the TASK-211 post-TASK-210
                   audit verified: a navigation dead end, an unrecoverable read, a
                   discarded error envelope, and a silently failing reward read).
                   Client presentation + the shared client transport only. One
                   implementation-legibility correction rides along inside the same
                   presentation surface: the Result screen's "Final Player HP:" label
                   names a value the contract defines as the Pet's HP, and the
                   delivered `durationTurns` was never rendered.
Status:            DONE
Risk:              LOW (client presentation and the client's own error handling; no
                   gameplay rule, no wire contract, no server file, no persistence,
                   no state model, no new store).
Priority:          P1 (the audit's only substantial unblocked P1 cluster)
Primary Agent:     client (TASK_TYPES.md §5 — Frontend/Phaser)
Supporting Agents: testing, review
Workflow:          development/bug-fix.md
Dependencies:      TASK-211 post-TASK-210 audit (DONE — the binding scope, §4 items 1–10),
                   TASK-207 (DONE — the audit whose U1/U2/U3 findings this closes),
                   TASK-203/204/205 (DONE — the post-result lifecycle and the
                     `ResultScene` run-guard pattern this task adopts for the Lobby),
                   TASK-149 (DONE — the result read and reward presentation),
                   TASK-208/208A/209/210 (DONE — the battle surface, untouched here)
Blocks:            None. TASK-218 / TASK-212 / TASK-219 / TASK-216 keep their own
                   positions in the audit's §6 sequence.
```

**Lifecycle note.** Like TASK-204/205/209/210, this task arrived out of band as an
execution manifest derived from the audit's §4 scope rather than as a `backlog/`
file; its record is filed here so the TASK-207 → TASK-208 → TASK-208A → TASK-209 →
TASK-210 → TASK-211 chain stays traceable (`tasks/README.md` §5).

**Scope discipline.** No backend file, no `docs/` file, no ADR, no migration, no
SignalR contract, no REST endpoint, no database column, no persistence, no runtime
store, no gameplay rule, no reward or progression rule, and no scene other than
`LobbyScene` and `ResultScene` was touched. `GameRuntime`, `GameRuntimeEvents`,
`SignalRService`, `BattleScene`, `BattleEventPresenter`, `App.tsx` and every
`docs/` file are **unchanged by this task**.

**Scope correction recorded (AGENTS.md §16), not implemented.** The audit's §4 item 3
recommends falling back to the §6 machine code (`error`) when the server sends no
`message`. This task deliberately does **not** put the machine code in the
player-facing text: the task's own §4 forbids exposing "implementation-specific
diagnostics", and a code such as `PET_NOT_OWNED` is exactly that. The code stays on
the thrown error (`ApiRequestError.code`) for diagnostics and tests, and the player
is shown a neutral statement instead. See "Changes — 1" below.

---

## Objective

Make every non-battle screen the player can reach survivable and legible: the Lobby
must be escapable and recoverable when an operation fails, a rejected battle start
must tell the player what the server actually said, and the Result screen must never
present an unexplained blank where the game's only reward belongs — while reading the
authoritative values it already receives.

The principle applied throughout is the one TASK-210 used on the battle surface:

```text
existing authoritative data and existing documented behavior
        ↓
better presentation, real recovery, and a lifecycle guard
```

— never *new data → new contract → new mechanics*.

---

## Authoritative References

- `tasks/completed/TASK-211-post-task-210-product-audit.md` §2.3 (the verified state
  of the non-battle screens), §3 Part A/B (NG-01…NG-10), §4 "Proposed TASK-211
  implementation scope" items 1–10 (the binding scope), §5 (the required coverage and
  E2E), §7.1 (the implementation-only classification)
- `tasks/completed/TASK-207-product-roadmap-and-gameplay-gap-audit.md` — U1 (silent
  reward failure), U2 (Lobby dead end / unrecoverable read), U3 (discarded error
  envelope), U7 (`durationTurns` discarded), U8 ("Final Player HP" mislabel)
- `tasks/completed/TASK-205-phaser-scene-lifecycle-consistency.md` and
  `TASK-203-post-result-lifecycle-implementation.md` — the scene-lifecycle contract
  and the `ResultScene` `rewardLoadRun` guard this task adopts for the Lobby
- `tasks/completed/TASK-202-*` — `D-202-01 = C` (the two explicit exits),
  `D-202-02 = A` (no automatic transition), `D-202-03 = D` (the preserved loadout),
  `D-202-04 = A` (the active-battle cleanup)
- `docs/02-technical/API_CONTRACTS.md` §3 (`POST /api/battle/start` and its three
  documented rejections), §4 (the result response, `durationTurns`, notes 5–7),
  §5.1–§5.6 (the collection reads), §6 (the `{ error, message }` error convention),
  §2.8 (the session and its `401`)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.19 (`BattleWon`/`BattleLost`, and the
  fixed `finalPlayerHp` protocol label), §4.3 item 15 / §4.4 item 10 (the delivered
  Pet/Boss members)
- `docs/02-technical/GAME_STATE.md` §2.3 (`PetState.HP` — the value `finalPlayerHp`
  carries), §2.4, §4 (client presentation state)
- `docs/02-technical/ARCHITECTURE.md` §2.2.1 rules 1–2 (`readRuntime`), §2.2.3 rules
  1–6 (scene-local state, the runtime boundary, the server's validation authority)
- `docs/03-decisions/ADR/ADR-001` (server authority), `ADR-011` (no Player HP pool),
  `ADR-022` (the preserved pre-battle loadout)
- `docs/00-overview/GDD.md` §2.1 / `docs/02-technical/TDD.md` §2.1 (the documented
  scene flow), `docs/00-overview/MVP_SCOPE.md` §1 (no new system, no new content)

---

## What Changed

### 1. The shared authenticated transport reads the §6 error envelope

`ApiService.get`/`post` discarded the response body entirely and threw
`Request to <path> failed with status <status>` (`API_CONTRACTS.md` §6 was already
specified and already returned by the server). It now reads the documented envelope
and rejects with an exported `ApiRequestError` carrying:

```text
message   the player-facing statement
status    the HTTP status                 → diagnostics and tests only
code      the §6 machine-readable code    → diagnostics and tests only
```

`message` is the §6 `message` — the endpoint's own human-readable detail, which is
what the backend already sends for all three documented start rejections
(`"A Pet must be selected and it must be owned by the player."`,
`"The selected Boss is not a valid MVP Boss."`, `"The Card loadout is invalid."`,
`"An authenticated session is required to start a battle."`). When the server sent no
envelope at all, the client states that the request failed — it never falls back to
its own path, status, or body.

**Why not the machine code as the fallback.** The audit's parenthetical suggestion to
fall back to `error` would put `PET_NOT_OWNED` on a player's screen. This task's §4
forbids implementation-specific diagnostics in the player-facing message, so the code
stays on the error object and a neutral statement is shown. This is the one point
where the task record knowingly narrows the audit's wording; it is recorded here
rather than silently applied (`AGENTS.md` §16).

`register`/`login` are untouched: their existing `err?.error` handling is the NG-11
one-line BUG the audit left for its own task.

### 2. Lobby — a real exit from every state

`< BACK` → `MainMenuScene` is drawn on **every** render pass, so it is usable while
the collection read is in flight, after a failure, and in the ready state. It reaches
`MainMenuScene` and nothing else: Collection and Battle History are deliberately not
reachable from the Lobby (`D-202-01 = C`).

It is drawn in the top-right corner — the one band of this layout that is
structurally free (the header is a fixed, left-aligned literal; the collection
columns' title band starts 27 px lower) — as an exported layout constant
(`LOBBY_BACK_BUTTON`), so the geometry is asserted against the same numbers the scene
draws with.

### 3. Lobby — an explicit asynchronous run guard

`LobbyScene` had no run, generation, or cancellation guard
(`grep 'isActive|generation|runId|token'` returned nothing), which was harmless only
because the Lobby could not be left. It now has the `ResultScene` pattern:

```text
create()        → asyncRun += 1, and the value is handed to every operation it starts
shutdown()      → asyncRun += 1
< BACK          → asyncRun += 1 before the transition is requested
loadCollection(run) / startBattle(run) → act only while run === asyncRun
```

So a collection read that settles after `< BACK` renders nothing, and a `startBattle`
that settles after `< BACK` **preserves no loadout and opens no battle** — the two
requirements `< BACK` makes reachable. `BACK` invalidates the attempt explicitly
rather than relying on the teardown happening to follow. It is plain scene-local
state: no cancellation registry, controller, or global store (`AGENTS.md` §9).

A single `hasTransitioned` claim guards this run's one transition (to `MainMenuScene`
or to `BattleScene`), claimed before `preserveLoadout` + `scene.start`, so a repeated
`START`/`RETRY` cannot produce a duplicate transition either.

### 4. Lobby — start failure recovery, and collection-read recovery

A failure now names the operation that failed (`failedOperation`), which is what puts
a working `RETRY` on screen:

```text
MainMenu
    ↓
Lobby
    ├── BACK → MainMenu
    ├── START BATTLE
    │     ├── success → Battle
    │     └── failure → error + RETRY + BACK
    └── collection read failure → error + RETRY + BACK
```

`RETRY` repeats **only** the operation that actually failed — the battle start, or the
collection read — and is inert while anything is in flight, so it can never create a
second concurrent operation. A failed attempt releases the in-flight guard, which is
what makes the retry submit again. The rejected selection survives untouched, and a
failed start preserves nothing (`ARCHITECTURE.md` §2.2.3 rule 5).

`requestStart` is now gated on `loading` as well as `startPending`, so the "disabled"
START BATTLE state is real: on the `PLAY AGAIN` entry the preserved loadout is applied
before the read finishes, and a click during the read can no longer submit a request
built from a partially populated view (NG-05). The hint line also stays expressive on
the error path instead of going blank (NG-06).

### 5. Lobby — the player-facing error line

The error line shows the transport's own player-safe statement:

```text
Battle start failed: A Pet must be selected and it must be owned by the player.
Collection load failed: An authenticated session is required.
```

No stack trace, no raw JSON, no HTTP status, no request path, no exception class, and
no machine code. `describeError` no longer renders a rejecting non-`Error`'s string
form, so nothing that is not a message can reach the screen through this line. The
technical facts stay reachable on `ApiRequestError` for diagnostics and tests.

### 6. Result — explicit reward loading / success / failure states

`loadRewards` kept only `rewards` and swallowed every failure with a bare
`catch { return; }`; the reward area was created empty and never written before the
await, so a failure and a still-loading read were the same blank box.

The block now has an explicit state:

```text
loading   → "Loading rewards…"
success   → the eight delivered members (+ the delivered duration)
failure   → "Rewards could not be loaded.\nPlease try again."  + RETRY
```

A failure is never rendered as a zero reward (`REWARDS` + `+0 XP` is a delivered
fact, `DATABASE.md` §1 item 5, and stays distinguishable). A failure with no
addressable read at all (no `battleId`, or no runtime) is reported as
`Rewards are unavailable for this battle.` with **no** RETRY, because there is nothing
to repeat — an honest statement and no inert control.

**It fails closed on a malformed payload.** An absent or non-object `rewards`, a
non-numeric member, or a non-numeric `durationTurns` is treated as the failure state,
never as data, so a `{}`-shaped response cannot render literal `undefined` under a
`REWARDS` heading (NG-10). The four "resulting value" members stay nullable **by
contract** and render as `—`, which is not malformed.

### 7. Result — reward RETRY

`RETRY` repeats **only** the reward read: it never re-enters the battle, never
re-reads the outcome, and never navigates. The run identity advances first, so the
read that failed — or any read still in flight — can no longer write, and an
activation while a read is in flight is ignored. The control is destroyed and
re-created by every reward render pass, so repeated retries cannot accumulate a
second block, a second control, or a second listener, and the existing
`rewardLoadRun` lifecycle protection (TASK-205) is intact and extended to the retry
path.

`PLAY AGAIN → Lobby` and `MAIN MENU → MainMenu` remain the two approved exits, both
usable in the failure state, and `clearActiveBattleState()` still runs before leaving.

### 8. Result — the delivered duration is rendered

`BattleResult.DurationTurns` was delivered and discarded. It is now rendered from the
same documented result read that delivers the rewards, in the vocabulary
`BattleHistoryScene` already uses:

```text
Duration: <durationTurns> turns
```

Nothing is counted, no timestamp is read, and no length is derived from combat events.
`0` is rendered as `0` — a battle that reached a terminal state before any committed
Swap records a value, not an absence (`API_CONTRACTS.md` §4 note 5). When the read
fails, no length is invented and the line is empty.

### 9. Result — the terminal Pet HP is labelled as the Pet

```text
Final Player HP: 850   ✗  (was)
Final Pet HP: 850      ✓
```

`finalPlayerHp` is a **fixed protocol label** for the active `PetState.HP` at battle
end (`SIGNALR_PROTOCOL.md` §3.2.19, `GAME_STATE.md` §2.3, ADR-011 — there is no Player
HP pool). The wire member is untouched; only the string a player reads was wrong, and
the fallback line (`NO RESULT`) is corrected with it. The scene's private
`playerHpText` field keeps its historical name because several browser harnesses read
it by that name; the field's own documentation records what it actually holds.

### 10. Explicitly not done (the task's non-goals, and the audit's)

The Signature Skill read source (NG-14 / TASK-219), crit events and any SignalR member
(NG-17 / TASK-217), heal events (NG-16), a new Relic contract (NG-15 / TASK-218), Card
cost/effect exposure (NG-18 / TASK-212), Tier/Star/progression scope (NG-21), a
progression read (NG-20), logout / 401 session reset (NG-12), the post-login blank
bounce (NG-13), the `USERNAME_ALREADY_TAKEN` typo (NG-11), silent list truncation
(U13), the dead endpoints (U14/U15), and every `docs/` change (TASK-217). None of them
was touched, and none of them was needed to complete this task.

---

## Tests

Extended in place — no parallel test infrastructure was created. (The working tree
also carries TASK-209/TASK-210's uncommitted test work in the same files, so each
section below describes what TASK-211 added.)

`tests/LobbyScene.test.ts` (+16 tests, and a harness correction)

- **Harness.** A rectangle control is now identified by the caption a player reads at
  its centre (the scene's own drawing convention) instead of a fixed
  `text: 'START BATTLE'` on every rectangle — the assumption that made "the first
  enabled rectangle is the start trigger" look true, and that a new exit beside the
  trigger would have silently broken. `shutdownScene`/`destroyScene` now model the
  engine's own display-list half of a scene teardown, so "a reused scene accumulates
  nothing" is observable. New helpers: `controlLabelled`, `rendersControl`,
  `liveControlLabels`, `setCollectionFailure`, and `collectionGate` /
  mutable-failure harness options.
- **New coverage.** `< BACK` in the loading, error and ready states; BACK →
  MainMenuScene and nowhere else; BACK while a read is in flight; BACK after a
  rejected start; BACK invalidating an in-flight attempt (a stale completion cannot
  navigate and preserves nothing); a collection read that settles after BACK being
  discarded; `RETRY` repeating the start operation and succeeding; `RETRY` behind a
  failed read re-invoking that read exactly once and clearing the error; repeated
  activation while an attempt is in flight being inert; START not submitting while the
  read is in flight; one transition however often START is pressed; the hint line
  staying informative on the error path; no control accumulation across a
  shutdown/start cycle; the preserved loadout untouched by Lobby → BACK → MainMenu;
  and the two new controls' exported geometry (inside the safe area, clear of each
  other, the columns' title band, and the START BATTLE row).
- **Updated to the documented intent.** The rejection cases now carry the §6 envelope
  (`ApiRequestError`) and assert the server's message is shown while **no** HTTP
  status, request path, or machine code is; the previous cases pinned
  `Request to /api/battle/start failed with status 400`, which was the defect.

`tests/ResultScene.test.ts` (+12 tests, and a harness correction)

- **Harness.** Text and rectangle doubles now model `destroyed`, and `clickControl`
  and `liveCaptions()` see only live objects — without it, "one RETRY control" could
  not be distinguished from "one RETRY control per render pass".
- **New coverage.** The loading state is visible before the read settles; the success
  state renders the eight delivered members; `Duration: N turns` renders from the
  delivered `durationTurns` (including `0`); `RETRY` exists only on failure; `RETRY`
  re-reads and renders the members; repeated `RETRY` issues sequential reads and keeps
  exactly one failure statement and one control; a stale read cannot overwrite a newer
  retry; PLAY AGAIN and MAIN MENU remain usable after a failure and restart no battle;
  an absent or malformed payload degrades to the failure state without rendering
  `undefined`; an unaddressable read is reported as unavailable with no inert control;
  teardown releases the RETRY objects; and the terminal line is `Final Pet HP:` with
  no Player HP value anywhere.
- **Updated to the documented intent.** Every `Player HP` assertion became
  `Pet HP`, and the silent-failure case became the readable, recoverable failure the
  audit asked for.

`tests/SceneLifecycle.test.ts` (+5 tests, plus stronger lifecycle entries)

- The Lobby's run guard, in the shape of the ResultScene run-guard suite: a start
  attempt that settles after SHUTDOWN navigates nowhere, preserves nothing, and
  attempts no write against a destroyed object (`destroyedWriteAttempts` empty); the
  same for a reused instance; the positive control (a start that settles inside its
  own run still enters the battle and preserves the loadout it submitted); BACK
  invalidating the attempt; and exactly one start request however often the control is
  activated. The `SCENES` lifecycle sweep now also asserts that a stopped Lobby
  releases `hasTransitioned` and a stopped Result releases its reward state.

`tests/BattleService.test.ts` and `tests/CollectionService.test.ts` (updated)

- The transport's failure cases now assert the documented §6 behavior — the server's
  `message` as the statement, plus `code` and `status` on the error — instead of
  `rejects.toThrow('400')`, which the discarded-status string satisfied. One case was
  added: a response with no envelope must not surface the client's own status or path.

**No existing lifecycle test was weakened.** The failure-state assertions that pinned
the previous defective behavior were replaced with assertions of the documented
intent, with the reasoning recorded in each test (`AGENTS.md` §15).

---

## Verification

```text
1. npx tsc --noEmit                     PASS (no output)
2. npx vitest run                       PASS (20 files, 817 tests; 783 before, +34)
3. npm run build                        PASS (tsc + vite build; dist written)
4. git diff --check                     PASS (exit 0)
5. Backend tests                        NOT RUN — no backend file changed
6. Browser E2E                          PASS (see below)
7. Diff audit                           PASS (see below)
```

### Browser E2E (`scripts/standalone-web-smoke.mjs`)

Two runs, each registering a fresh account, playing a real battle through the real
backend/PostgreSQL/Redis/SignalR, and additionally fighting a **third** battle to its
own terminal resolution so the reward-success path is exercised against a persisted
`BattleResult` row:

```text
RUN 1: 132 checks, 0 failures   (the real battle: 10 swaps in 7s, "Duration: 9 turns")
RUN 2: 132 checks, 0 failures   (the real battle: 11 swaps in 8s, "Duration: 11 turns")
safety.zeroUncaughtExceptions / zeroFatalConsoleErrors / zeroUnexpectedApiResponses:
       PASS in both runs
phase0.inspectedPageIsForeground: PASS in both runs
```

TASK-210 recorded 100 checks per run; this run carries 132, of which the TASK-211
checks are listed below.

The checks TASK-211 added or corrected:

```text
phase3c.lobbyOffersAnExitAndTheStart                    the Lobby is not a dead end
phase3c.lobbyControlsFitTheFrameWithoutOverlap          < BACK and START BATTLE, inside
                                                        the safe area and disjoint
phase3c.backReturnedToMainMenu                          BACK → Main Menu
phase3c.stoppedLobbyRanItsTeardown                      the stopped Lobby released its
                                                        selection and its transition claim
phase4b.rejectedStartIsReadable                         the server's own detail, and no
                                                        status / path / machine code
phase4b.serverAnsweredWithTheDocumentedEnvelope         the §6 envelope read off the wire
                                                        (Network.getResponseBody)
phase4b.rejectedStartOffersRetryAndBack                 RETRY + BACK beside START BATTLE
phase4b.theRejectedSelectionSurvived                    the rejected selection is intact
phase4b.retryRepeatsTheStartOperation                   RETRY repeats the start operation
phase4b.backFromFailureReturnedToMainMenu               failure → BACK
phase4b.retryStartedTheBattle                           failure → RETRY → Battle
phase7.terminalPetHpIsLabelledAsThePet                  "Final Pet HP: 850"
phase7b.rewardFailureIsReadable                         readable failure, no HTTP internals
phase7b.rewardFailureIsNotAZeroReward                   not rendered as a delivered zero
phase7b.rewardFailureOffersRetryBesideBothExits         RETRY + PLAY AGAIN + MAIN MENU
phase7b.noDurationIsInventedWithoutAResult              no invented length
phase7b.rewardRetryRereadsWithoutDuplicatingAnything    one read run, one RETRY, one block
phase7b.rewardRetryDidNotRestartTheBattle               the retry touched only the read
phase8.resultControlsAreTheTwoExitsPlusRetryOnFailure   RETRY only in the failure state
phase8.battle2RewardFailureIsReadableOnAReusedScene     a second, independent failure state
phase8.mainMenuIsUsableFromTheRewardFailure             failure → MAIN MENU
phase10.reusedLobbyStillHoldsOneControlOfEachKind       reuse accumulates nothing
phase10.realBattleLoadoutSelected                       a real battle is fought
phase10.realBattleReachedItsOwnResult                   the server resolved it (10 swaps)
phase10.serverOutcomeRendered                           the server's own outcome
phase10.deliveredRewardsAreRendered                     the reward SUCCESS state
phase10.durationIsRenderedFromTheDeliveredResult        "Duration: 10 turns" == delivered 10
phase10.rewardLinesCarryTheDeliveredMembers             rendered members == delivered members
phase10.terminalPetHpIsLabelledAndVerbatim              label and value, both checked
phase10.successOffersNoRetryAndKeepsBothExits           success carries no RETRY
phase10.rewardPresentationDoesNotOverlapOrClip          no clipping, no overlap
phase10.returnedToMainMenuAfterTheRealBattle            the run ends where it started
safety.onlyTheInducedDocumentedRejectionsOccurred       exactly the 3 induced 400s
```

**Nothing was weakened to make this pass.** The three induced `400`s are the
deliberate `PET_NOT_OWNED` rejections that reach the Lobby's failure state; each is
asserted from its own §6 envelope on the wire, and the safety filter names them
explicitly rather than ignoring all non-2xx responses. The real battle in phase 10 is
fought with real pointer input (`driveBattleToTerminal`), not injected, because a
`BattleResult` row exists only after the server resolves a terminal outcome.

**Three harness defects were found and fixed while verifying** (reported here, not
hidden):

1. Phase 8's Battle 2 start clicked "the first enabled rectangle in the Lobby", which
   is now `< BACK` — the Lobby draws an exit beside the trigger (TASK-211 §1). The
   lookup was corrected to resolve the control by the caption a player reads, and the
   control's point is its centre (a `Rectangle`'s position is its centre, unlike the
   collection rows whose text origin is `(0, 0)`). Had this not been caught it would
   have silently clicked the wrong control, and RUN 1 failed once on exactly that
   before the fix.
2. **The TASK-210 foreground guard did not cover the evaluate-driven outcome
   handoffs.** TASK-210 fixed "a hidden document suspends `requestAnimationFrame`, so a
   queued scene start is never processed" by re-asserting the foreground before every
   *real click*. The two outcome injections are delivered through
   `Runtime.evaluate`, not a click, so nothing re-asserted it for them: RUN 2 failed
   once with `activeScene: "BattleScene"`, `outcomeHandled: false` — a scene start the
   scene had claimed but the engine's loop never processed. `ensurePageVisible` is now
   called before each injection, exactly as `realClick` does, and the phase-7 failure
   diagnostic now reports `document.visibilityState` and the loop's own state so a
   future occurrence explains itself. This is a harness correction: it only makes the
   engine's loop step, and cannot turn a failing check into a passing one.
3. `phase7.resultSceneReached`'s failure detail previously reported only the active
   scene; it now also reports the loop state (see 2).

After the two corrections, the suite was run end to end again: **both runs, 132
checks each, 0 failures**. The first correction was a real harness defect; the second
is the same environment fragility TASK-210 documented, in a path its fix did not
cover.

### Diff audit

Changed by this task:

```text
src/frontend/client/src/services/api/ApiService.ts            §6 envelope on get/post
src/frontend/client/src/game/scenes/LobbyScene.ts             BACK, RETRY, run guard,
                                                              loading gate, hint line,
                                                              safe error text
src/frontend/client/src/game/scenes/ResultScene.ts            reward states + RETRY,
                                                              payload guard, duration,
                                                              Pet HP label
src/frontend/client/tests/LobbyScene.test.ts                  tests + harness
src/frontend/client/tests/ResultScene.test.ts                 tests + harness
src/frontend/client/tests/SceneLifecycle.test.ts              Lobby run-guard tests
src/frontend/client/tests/BattleService.test.ts               §6 envelope assertions
src/frontend/client/tests/CollectionService.test.ts           §6 envelope assertions
src/frontend/client/scripts/standalone-web-smoke.mjs          existing E2E harness
```

Verified unchanged by this task: every `docs/` file, every backend file
(`src/backend/**`), every backend test, `GameRuntime.ts`, `GameRuntimeEvents.ts`,
`SignalRService.ts`, `App.tsx`, `BattleScene.ts`, `BattleEventPresenter.ts`,
`MainMenuScene.ts`, `CollectionViewerScene.ts`, `BattleHistoryScene.ts`,
`PreservedLoadout.ts`, `RewardSummaryFormat.ts`, `GameRuntime.test.ts`,
`RuntimeBoundaries.test.ts`, `BattleEventPresentation.test.ts` and
`SignalRService.test.ts`. No protocol change, no new event, no new store, no new scene,
no new endpoint, no gameplay file.

---

## Remaining Issues

Reported, not fixed (`AGENTS.md` §16). Every one of these is outside this task's
scope, and none blocks its completion.

1. **The Lobby's error line still echoes the realtime layer's own wording.** A
   `startBattle` that fails while connecting surfaces
   `SignalR connection is not established.` — a transport name in player-facing text.
   It is `SignalRService`'s own message, shared with every scene that reports a
   failure, and renaming it is a realtime-presentation change this task did not
   undertake. The defect the task named — the discarded §6 envelope — is fixed at its
   source, so no HTTP status or path can appear any more.
2. **ResultScene reads the result only after an outcome handoff.** The duration and the
   rewards both come from that one read, so a battle whose result row is missing shows
   the failure state and no length. That is the documented behavior; a "length is known
   before the result exists" state would need a new contract.
3. **The card/relic/collection error lines elsewhere are unchanged.** The shared
   transport now returns a player-safe message everywhere, which improves them for
   free, but `CollectionViewerScene` and `BattleHistoryScene` were not otherwise
   touched (their `describeError` still renders a rejecting non-`Error`'s string form).
4. **NG-11 (`USERNAME_ALREADY_TAKEN` vs `USERNAME_ALREADY_EXISTS`)** is still a
   one-line BUG with no owning task.
5. **The TASK-209 record still does not exist under `tasks/`** (audit NG-23). This task
   did not reconstruct it, and cited the shipped code and tests instead.
6. **The audit's `error` fallback for an envelope-less response is intentionally not
   implemented** (see the Metadata note). If a reviewer wants the machine code shown to
   players, that is a product decision that contradicts this task's §4 and should be
   taken explicitly.

---

## Final Report

```text
TASK-211 COMPLETE

Lobby navigation:
    < BACK → MainMenuScene, drawn in every state (loading, error, ready) and reaching
    nothing else. The Lobby is no longer a dead end. Layout exported and asserted.

Lobby recovery:
    A failed battle start shows the server's own §6 detail with working RETRY + BACK;
    a failed collection read shows a working RETRY. RETRY repeats only the operation
    that failed, is inert while anything is in flight, and a failed attempt releases
    the guard so the retry can submit. The rejected selection survives; a failed start
    preserves nothing.

Lobby async lifecycle:
    A scene-local run identity (the ResultScene `rewardLoadRun` pattern) plus a
    single transition claim. A start that settles after BACK or SHUTDOWN preserves no
    loadout and opens no battle; a stale collection read renders nothing; a reused
    instance accumulates no control, listener, or async work.

Lobby error presentation:
    The shared authenticated transport now reads the documented §6 envelope and rejects
    with ApiRequestError (player-safe message; code + status for diagnostics). The
    player-facing line exposes no stack, raw JSON, HTTP status, path, exception name,
    or machine code.

Result reward states:
    loading / success / failure are explicit. A failure says "Rewards could not be
    loaded. Please try again." and is never rendered as a zero reward. An absent or
    malformed payload fails closed. A failure with nothing to repeat says so and offers
    no inert control.

Result reward retry:
    RETRY repeats only the read, under a fresh run identity; no duplicate block, no
    duplicate control, no duplicate listener, no stale write, and no battle restart.
    PLAY AGAIN → Lobby and MAIN MENU → MainMenu stay usable after a failure, with
    clearActiveBattleState() still running before each exit.

Result presentation corrections:
    The delivered durationTurns renders as "Duration: N turns" from the same
    authoritative result read that delivers the rewards — never counted or derived.
    "Final Player HP:" became "Final Pet HP:", matching SIGNALR_PROTOCOL.md §3.2.19,
    GAME_STATE.md §2.3 and ADR-011. The wire member is untouched.

Tests:
    npx tsc --noEmit PASS; npx vitest run PASS (20 files / 817 tests, +34);
    npm run build PASS; git diff --check PASS. Coverage added for the Lobby exit,
    failure/retry/back recovery, the run guard across SHUTDOWN and reuse, the loading
    gate, the hint line, control layout, the reward block's three states, reward retry
    and its guards, the malformed-payload guard, the delivered duration, and the Pet
    HP label. The tests that pinned the previous defective behavior were updated to
    the documented intent with their reasoning recorded.

Browser E2E:
    scripts/standalone-web-smoke.mjs, two runs, each with two injected outcomes and
    one REAL battle fought to its own terminal resolution against the real backend,
    PostgreSQL, Redis and SignalR: 132 checks per run, 0 failures (the real battles
    took 10 swaps / 7s and 11 swaps / 8s). The Lobby exit and its layout, the readable
    §6 rejection with its envelope read off the wire, failure → RETRY → Battle, failure
    → BACK, the Result reward failure with RETRY and both exits, the reward SUCCESS
    state with "Duration: 9 turns" / "Duration: 11 turns" matching the delivered
    `durationTurns`, the corrected Pet HP label, and repeated scene reuse all pass,
    with zero uncaught exceptions, zero fatal console errors and zero unexpected API
    responses. Three harness defects were found while verifying and are reported in
    the E2E section: the Battle 2 start clicked the Lobby's new `< BACK` because it
    looked up "the first enabled rectangle", and the evaluate-driven outcome handoffs
    were not covered by TASK-210's foreground guard (a hidden document suspends the
    loop that processes a queued scene start).

Files Changed:
    src/frontend/client/src/services/api/ApiService.ts
    src/frontend/client/src/game/scenes/LobbyScene.ts
    src/frontend/client/src/game/scenes/ResultScene.ts
    src/frontend/client/tests/LobbyScene.test.ts
    src/frontend/client/tests/ResultScene.test.ts
    src/frontend/client/tests/SceneLifecycle.test.ts
    src/frontend/client/tests/BattleService.test.ts
    src/frontend/client/tests/CollectionService.test.ts
    src/frontend/client/scripts/standalone-web-smoke.mjs

Contract impact:
    NONE. No wire member, endpoint, event, database column, persistence, store, ADR or
    docs/ change. Every value and error this task presents was already delivered
    (API_CONTRACTS.md §3, §4, §6; SIGNALR_PROTOCOL.md §3.2.19). The `finalPlayerHp`
    wire name is a fixed protocol label and was not moved.

Existing behavior preserved:
    TASK-203/204/205/206/208A/209/210 lifecycle, loadout, battle and runtime-boundary
    behavior is intact: PLAY AGAIN → Lobby with the preserved, editable loadout;
    MAIN MENU → MainMenu; clearActiveBattleState() on both exits; the preserved
    loadout untouched by the Lobby's own teardown and by its exit; one teardown handler
    per scene event across reuse; and no scene reaching the transport directly.
```
