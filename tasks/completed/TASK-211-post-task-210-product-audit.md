# TASK-211 — Post-TASK-210 Product & UX Gap Audit

```text
Task ID:            TASK-211
Type:               AUDIT (evidence-based product/UX gap classification; no
                    gameplay, UI, backend, contract, or documentation change)
Status:             DONE
Risk:               NONE (read-only audit; zero production/test/docs file modified)
Priority:           HIGH (determines the next implementation task)
Primary Agent:      review / product-audit
Evidence base:      current working tree (src/, tests/, docs/, tasks/, scripts/)
Model:              DeepSeek Harness agent
```

**Scope discipline.** This task audits only. No production code, test, migration,
contract, ADR, or `docs/` file was modified. No temporary instrumentation was
added. The single file created is this record. The harness/gameplay work of
TASK-208A / TASK-209 / TASK-210 is treated as shipped baseline and is not
re-opened; where this audit disagrees with a recorded claim, the disagreement is
**reported** (`AGENTS.md` §16) and the completed record is **not** edited
(`tasks/TASK_LIFECYCLE.md` §3 — completed tasks are immutable).

**No implementation is performed here.** The task proposed in §5 is a proposal
with a reserved ID, not authorization.

---

## 0. Verification Performed (audit evidence base)

```text
git diff --check                       PASS (exit 0; no whitespace errors)
git diff --check --cached              PASS (exit 0)
npx tsc --noEmit (client)              PASS (clean, exit 0)
npx vitest run (client)                PASS — 20 files / 783 tests
dotnet test GameServer.Api.Tests       PASS — 326 passed / 0 failed
dotnet test GameServer.Domain.Tests    PASS — 1558 passed / 0 failed
dotnet test GameServer.Application.Tests PASS — 623 passed / 0 failed
dotnet test GameServer.Infrastructure.Tests PASS — 412 passed / 0 failed
Browser E2E (standalone-web-smoke)     NOT RE-RUN by this audit — it requires a
                                       live backend + PostgreSQL + Redis +
                                       Chromium; TASK-210 records
                                       100 checks × 2 runs at HEAD-equivalent
                                       tree. Inspected statically instead: the
                                       full check inventory and the phases.
```

The client figure (783 tests / 20 files) reproduces TASK-210's recorded result
exactly, which independently corroborates that recorded verification.

**Source re-read for every headline gap** (direct reads, not summaries):
`LobbyScene.ts`, `ResultScene.ts`, `RewardSummaryFormat.ts`, `BattleScene.ts`,
`BattleEventPresenter.ts`, `MainMenuScene.ts`, `CollectionViewerScene.ts`,
`BattleHistoryScene.ts`, `PreloaderScene.ts`, `App.tsx`,
`ui/components/StatusOverlay.tsx`, `ui/components/AuthScreen.tsx`,
`ApiService.ts`, `BattleModels.ts`, `GameRuntime.ts`, `GameRuntimeEvents.ts`,
`PreservedLoadout.ts`, `PlayerStarterGrantFactory.cs`, `AuthController.cs`,
`CollectionController.cs`, `CollectionQueryService.cs`, `CardRepository.cs`,
`BattleEventWireProjection.cs`, `DamagePipeline.cs`,
`standalone-web-smoke.mjs`.

**Docs re-read for every claim:** `AGENTS.md`, `tasks/{README,TASK_LIFECYCLE,
TASK_TYPES}.md`, `GDD.md`, `MVP_SCOPE.md`, `ROADMAP.md`, `API_CONTRACTS.md`
§3/§4/§5.1–§5.6/§6, `SIGNALR_PROTOCOL.md` §3.2.13/§3.2.19/§3.2.23/§4,
`GAME_STATE.md` §2.3/§2.4, `GAME_EVENTS.md` §2/§3,
`ARCHITECTURE.md` §2.2.2/§2.2.3, `CARD_RULES.md` §1/§4.1, `COMBAT_RULES.md`
§3.3, `DATABASE.md` §1/§2, `ADR-011`, `ADR-012`, `ADR-020`, plus the completed
records for TASK-207, TASK-208, TASK-208A and TASK-210.

---

## 1. Current State

**The core loop is now vertically complete and the battle screen is finally
player-facing; the non-battle screens are the least presentable part of the
product and have not been touched since TASK-206.**

1. **TASK-207 → TASK-208 → TASK-208A → TASK-209 → TASK-210 is a shipped
   chain.** The realtime projection was widened by a recorded Product Owner
   decision (`D-208-01…D-208-04`), authored into `SIGNALR_PROTOCOL.md` §4.3 item
   15 / §4.4 item 10 and `GAME_STATE.md` §2.3/§2.4 by TASK-208A, implemented by
   TASK-209, and presented by TASK-210. The four authorized members are
   `petState.hp` / `maxHp` / `power` and `bossState.bossId`
   (`SIGNALR_PROTOCOL.md` §4.3 item 15, §4.4 item 10).
2. **The battle screen is no longer a diagnostic console.** `BattleScene` renders
   HP gauges for both parties with the delivered numbers, a Power gauge against
   the documented `0–100` invariant, `COMBO ×N` / `MATCHES n` from delivered
   state, a `current / threshold` Passive gauge, a compact Status Effect line,
   damage floaters anchored on the delivered party that took the hit, and one
   selective per-batch callout (`BattleScene.ts:1231`,
   `BattleEventPresenter.ts`). Transport state, battle id, turn/sequence, RNG
   seed/state and the raw event log are no longer drawn.
3. **The Lobby, Result, Collection and History screens are byte-identical to the
   state TASK-207 audited.** `git diff --name-only` lists only
   `BattleScene.ts`, `BattleEventPresenter.ts`, `GameRuntime.ts`,
   `GameRuntimeEvents.ts`, `SignalRService.ts`, `App.tsx`, the three docs, the
   tests and the smoke harness. `LobbyScene.ts`, `ResultScene.ts`,
   `MainMenuScene.ts`, `CollectionViewerScene.ts`, `BattleHistoryScene.ts`,
   `PreloaderScene.ts`, `ApiService.ts` and `RewardSummaryFormat.ts` are
   **unmodified**, so TASK-207's line citations on those files still land on the
   same code and its U2/U3/U1/U7/U9/U11–U15 findings survive except where noted.
4. **Two TASK-207 findings are now false and one is now imprecise** (verified, see
   §2): U10 (dev overlay unconditional in production) is fixed by TASK-209 §6;
   U3's *mechanism* was mis-cited; U1's "indistinguishable from a zero reward" is
   not accurate.
5. **The product's largest remaining player-facing defect is a navigation dead
   end, not a missing feature.** The Lobby has exactly one transition —
   `LobbyScene.ts:678 this.scene.start('BattleScene')` — and no `< BACK`, no
   RETRY, no cancel. A player who opens the Lobby, or whose collection read
   fails there, cannot reach the Main Menu, the Collection, or the Battle History
   without a full page reload. `CollectionViewerScene` and `BattleHistoryScene`
   both have `< BACK` and a real RETRY control; the Lobby has neither.
6. **The reward moment — the game's only reward channel — can silently render
   nothing.** `ResultScene.loadRewards` swallows every failure with
   `catch { return; }` (`ResultScene.ts:467-471`), the reward area is created
   empty (`:277-285`), and there is no retry. The failure state and the
   still-loading state are the same empty box.
7. **All four "Remaining Contract Gaps" reported by TASK-210 were
   independently re-verified. One is misclassified.** The Relic-trigger gap is
   not a contract gap: the wire carries the owned Relic *instance* identity and
   `GET /api/relics` already returns instance identity → `RelicDefinition.Name`
   (`API_CONTRACTS.md:802-806`), so `BattleScene` could name a trigger today by
   loading a read it is already allowed to call. The crit "gap" is a documented
   refusal, not an open gap, and the real finding there is stale prose.
8. **Everything green, both sides of the stack.** 783 client tests, 2919 backend
   tests, `tsc --noEmit` clean, `git diff --check` clean. The suites are green
   *and* do not contradict the gaps below, because the tests that touch these
   surfaces pin the current (defective) behaviour rather than the documented
   intent — e.g. `ResultScene.test.ts:833-845` asserts the reward area is empty on
   failure, and `LobbyScene.test.ts:940-944` asserts the transport-status string.
9. **The working tree carries uncommitted deliverables for three tasks and one
   missing record.** See §2 "Repository hygiene" and §9.

---

## 2. Verified Completed Areas

Each entry was verified against code/tests, not accepted from a roadmap claim.
Where a prior claim is now untrue, that is stated.

### 2.1 Battle presentation (TASK-209 / TASK-210) — VERIFIED SHIPPED

| Claim | Verdict | Evidence |
|---|---|---|
| Pet `hp`/`maxHp`/`power` and Boss `bossId` are delivered | IMPLEMENTED | `SIGNALR_PROTOCOL.md` §4.3 item 15, §4.4 item 10; `GameRuntimeEvents.ts` runtime readers; E2E `phase5b.hudPetHpIsVisible`, `phase5b.hudPetPowerIsVisible`, `phase5b.hudBossIdentityIsCorrect` |
| HP gauges + numbers for both parties, no derived HP | IMPLEMENTED | `BattleScene.ts` gauge drawing; `SceneLifecycle.test.ts` full/partial/zero/`hp>max`/`max<=0` cases; source test asserting no second HP field |
| Power gauge over the documented `0–100` invariant, no `maxPower` invented | IMPLEMENTED | `BattleScene.ts`; `SpaceLifecycle`-family source test pinning no `maxPower` |
| Damage floaters keyed on the delivered party (TASK-207 G11 fix) | IMPLEMENTED | `BattleScene.ts`; `phase5c.damageFloatersAreAnchoredOnThePartyThatTookTheHit` |
| Combo/Match from delivered state, no timer expiry | IMPLEMENTED | `BattleScene.ts`; `phase5c.comboCalloutUsesTheDeliveredValue` |
| Passive progress gauge, Status Effect compact line | IMPLEMENTED | `BattleScene.ts`; `phase5b.passivePresentationIsReadable`, `phase5b.statusEffectPresentationIsReadable` |
| No developer diagnostics in the HUD; raw event log not rendered | IMPLEMENTED | `phase5b.noDeveloperDiagnosticsInTheHud`, `phase5b.rawEventLogIsNotRendered` |
| Scene lifecycle/async safety for the battle surface | IMPLEMENTED | `SceneLifecycle.test.ts`; `phase8.resultSceneTeardownReleasedItsPresentation`, `phase8.noStaleSceneSubscriptions` |

### 2.2 Post-result lifecycle (TASK-202/203/204/205) — VERIFIED SHIPPED

| Item | Verdict | Evidence |
|---|---|---|
| `PLAY AGAIN` → Lobby with the preserved loadout, still editable | IMPLEMENTED | `ResultScene.ts:342-351`; `LobbyScene.ts:310,485-500`; `PreservedLoadout.ts:79-114`; ADR-022; E2E `phase8.preservedLoadoutRestored`, `phase8.preservedLoadoutIsEditable`, `phase8.battle2RequestCarriesEditedLoadout` |
| `MAIN MENU` → Main Menu, active battle state cleared on both exits | IMPLEMENTED | `ResultScene.ts:347,365,397-399`; `GameRuntime.ts:379-390`; E2E `phase8.activeBattleStateCleared`, `phase8.mainMenuClearedActiveBattleState` |
| Result scene teardown / reuse / stale-run guard | IMPLEMENTED | `ResultScene.ts:132,182,220,477-479`; `SceneLifecycle.test.ts:4546-4604` |
| Result single-fire transition guard | IMPLEMENTED | `ResultScene.ts:376-383` (`claimTransition`), `phase8.noDuplicateSceneSubscription` |

### 2.3 Non-battle screens — VERIFIED, WITH THE CORRECTIONS BELOW

| TASK-207 claim | Verdict | Evidence |
|---|---|---|
| U10 `StatusOverlay` unconditional in production (P1) | **NO LONGER TRUE — FIXED** | `App.tsx:139-140` gates both overlays on `import.meta.env.DEV` (the working-tree change, verified by diff); `App.css:146` `pointer-events: none` meant it never blocked the click even at HEAD |
| U2 no `< BACK` / no RETRY in the Lobby | **STILL TRUE (and broader than stated)** | sole `scene.start` `LobbyScene.ts:678`; no RETRY identifier in the file; `< BACK` exists only at `CollectionViewerScene.ts:496` → `scene.start('MainMenuScene')` `:396`, and `BattleHistoryScene.ts:477` → `:370`; RETRY only at `CollectionViewerScene.ts:606-632`, `BattleHistoryScene.ts:593-619` |
| U2 failed/empty collection read is a dead end | **STILL TRUE** | `LobbyScene.ts:453-456` sets an error string; `loadCollection()` (`:434`) has **no other caller** (`:360` only), so it cannot be retried |
| U3 server's human-readable `message` discarded | **STILL TRUE** | server returns `{error, message}` (`BattleController.cs:155-159`); `ApiService.post` never reads the body (`ApiService.ts:316-318`) |
| U3 "raw server code" shown | **IMPRECISE — worse than claimed** | the shown text is `Request to /api/battle/start failed with status 400` (`ApiService.ts:317`); the `error` code is discarded too, so `INVALID_LOADOUT`, `PET_NOT_OWNED` and `BOSS_NOT_FOUND` are indistinguishable |
| U3 citation `ApiService.ts:82-86,105-109` is the start path | **MIS-CITED** | those lines are `register`/`login`, which *do* read `err?.error`; the start path is `:316-318` |
| Duplicate START BATTLE request protection | **IMPLEMENTED** | `LobbyScene.ts:611-613` (`startPending` in-flight guard), set `:654`, cleared `:671`/`:676`; pinned by `LobbyScene.test.ts:873-913` |
| Lobby loading state | **IMPLEMENTED** | `:440-441,883-885`, per-column `Loading …` notes `:934,959,985`, in-flight line `:892-893` |
| U1 Result reward area silently empty on failure, no loading/error/retry | **PARTIALLY TRUE** | silent failure + no loading/error/retry TRUE (`ResultScene.ts:277-285,464-471`; `ResultScene.test.ts:833-845`). "Indistinguishable from a zero reward" **NOT ACCURATE** — a zero reward renders `REWARDS` + `+0 XP` (`RewardSummaryFormat.ts:36-40`); the ambiguous pair is *failure* vs *still loading* |
| U7 `durationTurns` delivered and discarded | **STILL TRUE** | delivered (`BattleModels.ts:484`; `API_CONTRACTS.md` §4); only `.rewards` is read (`ResultScene.ts:465-466`); History renders it (`BattleHistoryScene.ts:704`) |
| U8 `Final Player HP:` prints the Pet's HP | **STILL TRUE** | `ResultScene.ts:428` vs `SIGNALR_PROTOCOL.md:1043,1057-1059` (`finalPlayerHp` = `PetState.HP` under a fixed protocol label; no Player HP pool — ADR-011) |
| U9 `USERNAME_ALREADY_TAKEN` vs backend `USERNAME_ALREADY_EXISTS` | **STILL TRUE (BUG)** | `AuthScreen.tsx:43` vs `AuthController.cs:84`; falls through to `:50`, so the raw code is shown on duplicate registration. Untested (no `AuthScreen.test.tsx`) |
| U11 no logout / no 401 session reset | **STILL TRUE (docs vs code)** | no `logout`/`signOut` identifier in `src/`; `ApplicationSession.clear()`'s only caller is `App.tsx:47`; `ADR-020` documents a sign-out that clears the token |
| U12 inconsistent loading/error/retry coverage | **STILL TRUE** | Collection `:583,598,588-592,607-636` ✔; History `:558,572-575,563-568,594-620` ✔; Lobby error text only, no RETRY; Result none; Preloader `PreloaderScene.ts:28` handles `COMPLETE` only |
| U13 silent list truncation | **STILL TRUE** | Lobby `MAX_LIST_ROWS = 8` (`LobbyScene.ts:106,943,967,993`); Collection computes 14 (`CollectionViewerScene.ts:61,650,664,677`) while its tab label prints the true count (`:559`); History does it correctly (`BattleHistoryScene.ts:584`) |
| U14 `GET /api/pets/{petId}` never called in production | **STILL TRUE** | defined `ApiService.ts:173`, ported `GameRuntimeEvents.ts:526`, delegated `GameRuntime.ts:787`; pinned unused by `CollectionViewerScene.test.ts:1135` |
| U15 `RuntimeStatusChanged` / health endpoints undocumented + unreachable | **STILL TRUE** | emitted `BattleHub.cs:821,835`; no client subscriber (`SignalRService.ts:697` is a comment); 0 `docs/` hits; `ApiService.checkHealth()` (`:59-68`) uncalled |
| Raw wire ids / developer text in players' screens | **STILL TRUE** | `CollectionViewerScene.ts:756,761,772,778`; `BattleHistoryScene.ts:702-705` (`Battle #n  <raw id>`, `Outcome: victory`, ISO `Completed At`); `PreloaderScene.ts:46,55` |
| Element shown as `Fire` in Collection but `Hỏa` in the Lobby | **STILL TRUE (and inconsistent within the Lobby)** | `CollectionViewerScene.ts:654` wire value; `LobbyScene.ts:949` wire value for pets but `:1026` Vietnamese for bosses |
| Battle History has no per-entry action | **STILL TRUE (by design)** | `BattleHistoryScene.ts:635-636,648-658` |
| Main Menu has exactly three entries | **STILL TRUE** | `MainMenuScene.ts:83,90,104`; title literal `'DCACTI'` (`:68`) |

### 2.4 Backend / contract baseline — VERIFIED

| Item | Verdict | Evidence |
|---|---|---|
| Battle start validation and rejection codes | IMPLEMENTED | `BattleController.cs:155-159,329-340` (`INVALID_LOADOUT` / `PET_NOT_OWNED` / `BOSS_NOT_FOUND`) |
| Card read excludes `powerCost`/`effectDefinition` by contract | IMPLEMENTED AS DOCUMENTED | `API_CONTRACTS.md:783-786`; `CollectionModels.ts` |
| Relic read returns instance id → definition name | IMPLEMENTED | `API_CONTRACTS.md:802-806`; `CollectionController.cs:220-233`; `CollectionQueryService.cs:240-298` |
| `PetSkill` cards are never unlock rows | IMPLEMENTED AS DOCUMENTED | `PlayerStarterGrantFactory.cs:62-67`; `CARD_RULES.md` §1 item 4 (`:109-113`); ADR-012 item 9 |
| Crit is folded into `otherModifiers` with no separate wire member | IMPLEMENTED AS DOCUMENTED | `DamagePipeline.cs:424-437,536-542`; `COMBAT_RULES.md:396-398` §3.3 item 6; `SIGNALR_PROTOCOL.md:1491-1499` |
| Healing emits no event | IMPLEMENTED AS DOCUMENTED | `ResourceGenerator.cs:400-404`; `GAME_EVENTS.md` §2 has no heal event; pinned by `PlayerEffectHealingTests.cs:467-482` |

### 2.5 Repository hygiene (reported, not fixed)

**Pre-existing uncommitted modifications — NOT unrelated to the last tasks.**
The working tree carries **20 modified tracked files and 4 untracked task
records**, and every one of them is attributable to TASK-208A, TASK-209 or
TASK-210. Nothing unrelated was found.

```text
docs/01-game-design/BOSS_RULES.md                 TASK-208A §6.2.6
docs/02-technical/GAME_STATE.md                   TASK-208A §2.3/§2.4
docs/02-technical/SIGNALR_PROTOCOL.md             TASK-208A §4, §4.3, §4.4, §7, §8
src/backend/GameServer.Api/Hubs/BattleHub.cs      TASK-209 projection
src/frontend/client/src/app/App.tsx               TASK-209 §6 (DEV-gate both overlays)
src/frontend/client/src/game/runtime/GameRuntime.ts        TASK-209
src/frontend/client/src/game/runtime/GameRuntimeEvents.ts  TASK-209
src/frontend/client/src/services/realtime/SignalRService.ts TASK-209
src/frontend/client/src/game/scenes/BattleScene.ts         TASK-209 + TASK-210
src/frontend/client/src/game/scenes/BattleEventPresenter.ts TASK-210
src/frontend/client/scripts/standalone-web-smoke.mjs       TASK-209 + TASK-210
src/frontend/client/tests/{BattleEventPresentation,GameRuntime,ResultScene,
  RuntimeBoundaries,SceneLifecycle,SignalRService}.test.ts  TASK-209 + TASK-210
tests/backend/GameServer.Api.Tests/{ApiIntegrationTests.cs,
  Hubs/BattleHubReconnectRecoveryTests.cs,
  Hubs/PetStateWireProjectionTests.cs}                      TASK-209
tasks/completed/TASK-207-*.md   (untracked)  pre-existing record
tasks/completed/TASK-208-*.md   (untracked)  pre-existing record
tasks/completed/TASK-208A-*.md  (untracked)  pre-existing record
tasks/completed/TASK-210-*.md   (untracked)  pre-existing record
```

**The TASK-209 record does not exist anywhere under `tasks/`.** A repository-wide
glob for `*209*` under `tasks/` and a grep across the tree find only *references*
to TASK-209 (in `TASK-210`, in `SceneLifecycle.test.ts`, `RuntimeBoundaries.test.ts`,
`BattleEventPresentation.test.ts`, `BattleScene.ts`, `App.tsx`,
`standalone-web-smoke.mjs`) — never a record. TASK-210 lists TASK-209 as
`DONE` and as the contract its presentation stands on
(`TASK-210-battle-feedback-and-presentation-pass.md:30`), and the whole
`TASK-209 §4/§5/§6/§12` section numbering used by the shipped code and tests
belongs to a manifest that was never filed. Per `tasks/README.md` §5 and
`TASK_LIFECYCLE.md` §3 this is a traceability gap, reported in §3 (NG-29) and
**not** repaired here (an audit does not create another task's record).

---

## 3. Remaining Gaps

Severity: **P0** blocks the core playable loop · **P1** major player-facing
defect · **P2** important polish · **P3** nice-to-have.
Size: **S** ≤ 1 session · **M** 2–4 · **L** 5+.
Contract impact: `NONE` · `IMPLEMENTATION-ONLY` (fixable inside the existing
contract) · `DOCUMENTATION-ONLY` · `DECISION-REQUIRED` (a Product Owner /
contract decision must land first).

### Part A — Lobby (TASK-207 U2/U3, plus new findings)

```text
Gap                     NG-01 — the Lobby cannot be left except by winning through
Current behavior        The only transition in the file is
                        `LobbyScene.ts:678 this.scene.start('BattleScene')`, reached
                        only after a successful `runtime.startBattle()`. There is no
                        `< BACK`, no cancel, and no keyboard input; `MainMenuScene.ts:144`
                        enters the Lobby one-way. Collection and Battle History both
                        have `< BACK` (`CollectionViewerScene.ts:496` → `:396`,
                        `BattleHistoryScene.ts:477` → `:370`).
Evidence                LobbyScene.ts:14 (doc comment), :678, :901-929 (the only
                        interactive objects besides option rows at :1050-1051);
                        MainMenuScene.ts:144; grep for `scene.start` in LobbyScene.ts
                        returns exactly one hit.
Player impact           Once the player taps START BATTLE on the Main Menu, the
                        Collection and the Battle History become unreachable and
                        there is no way back at all. Requires a full page reload.
Severity                P1
Implementation size     S
Contract impact         NONE
Recommended action      Add a `< BACK` control to `MainMenuScene` as part of
                        TASK-211 (it is the same missing control that makes the
                        failure path unrecoverable — see NG-02).
```

```text
Gap                     NG-02 — a failed or empty collection read is unrecoverable
Current behavior        `loadCollection()` (`LobbyScene.ts:434-461`) is called exactly
                        once, from `create()` (`:360`). On failure it stores
                        `Collection load failed: ${error}` (`:456`) and renders it
                        (`:872`); the three lists stay empty, so
                        `describeIncompleteSelection()` returns "Choose a Pet first."
                        (`:721-722`) and START BATTLE can never succeed. Nothing
                        re-invokes the read.
Evidence                LobbyScene.ts:360,434,453-460,721-722,939,963,989;
                        `loadCollection` has no other caller (grep).
Player impact           A transient network failure at the worst moment permanently
                        bricks the screen; the player must reload the page and
                        re-enter the whole flow.
Severity                P1
Implementation size     S
Contract impact         NONE
Recommended action      Add an explicit RETRY that re-invokes `loadCollection()`
                        (keeping the existing in-flight guard), matching the
                        documented pattern already implemented in
                        `CollectionViewerScene.ts:607-632` and
                        `BattleHistoryScene.ts:594-619`.
```

```text
Gap                     NG-03 — start-failure feedback discards the documented error envelope
Current behavior        `ApiService.post` ignores the response body entirely and throws
                        `Request to ${path} failed with status ${response.status}`
                        (`ApiService.ts:316-318`). The Lobby prints that string
                        verbatim (`LobbyScene.ts:670` via `describeError` `:1086-1088`).
                        The server sends `{ "error": "…", "message": "…" }`
                        (`BattleController.cs:155-159`), and `API_CONTRACTS.md` §6
                        defines exactly that envelope. Consequently
                        `INVALID_LOADOUT`, `PET_NOT_OWNED` and `BOSS_NOT_FOUND`
                        are indistinguishable, and no reason or slot is named.
                        The same discard affects the Collection and History error
                        banners (`CollectionViewerScene.ts:341`,
                        `BattleHistoryScene.ts:330`).
Evidence                ApiService.ts:316-318 vs 82-86/105-109 (register/login DO
                        read `err?.error`); BattleController.cs:155-159,329-340;
                        API_CONTRACTS.md:833-839; LobbyScene.ts:670;
                        LobbyScene.test.ts:940-944,961 (pins the status string).
Player impact           When a start is rejected the player is told only
                        "status 400" and cannot tell whether they need a different
                        Pet, a different Boss, or a different Card set.
Severity                P1
Implementation size     S
Contract impact         IMPLEMENTATION-ONLY — the envelope is already specified
                        (§6) and already returned; the client throws it away.
Recommended action      Parse the §6 envelope in the shared authenticated transport
                        and surface `message` (falling back to `error`) in the
                        Lobby; do not invent reasons and do not weaken the
                        server's authority over validity.
```

```text
Gap                     NG-04 — Lobby async continuations have no run/scene guard
Current behavior        `loadCollection`'s `finally` calls `this.render()` and
                        `startBattle`'s continuations call `this.render()`,
                        `preserveLoadout(...)` and `scene.start('BattleScene')`
                        (`LobbyScene.ts:457-459,671-678`) with no scene-active
                        check, no generation counter and no cancellation.
                        `ResultScene` implements exactly this guard
                        (`rewardLoadRun`, `ResultScene.ts:132,182,220,477-479`).
Evidence                grep for `isActive|generation|runId|token` in LobbyScene.ts
                        → no matches; ResultScene.ts:132-220,477-479;
                        SceneLifecycle.test.ts:4546-4604 (the Result equivalent).
Player impact           Today the exposure is limited because the Lobby cannot be
                        left (NG-01). Adding NG-01's `< BACK` makes it reachable:
                        leaving mid-load would re-render into a torn-down scene and
                        a late `startBattle` could still navigate to a battle the
                        player abandoned.
Severity                P2 (rises to P1 the moment `< BACK` exists)
Implementation size     S
Contract impact         NONE
Recommended action      Adopt the TASK-205 `ResultScene` run-guard pattern in the
                        same change as NG-01 — the two are one fix, not two.
```

```text
Gap                     NG-05 — the "disabled" START BATTLE state is cosmetic only
Current behavior        `drawStartTrigger` computes `enabled = !startPending && !loading`
                        (`LobbyScene.ts:907`) but uses it only for fill/stroke/label
                        colour and the `useHandCursor` option; `setInteractive` and the
                        pointer handler are registered unconditionally (`:922-923`), and
                        `requestStart` checks `startPending` only (`:611`), never
                        `loading`.
Evidence                LobbyScene.ts:611-613,907,910-923.
Player impact           Low but real: on the PLAY AGAIN entry the preserved loadout is
                        applied before the read finishes (`:347`), so a click during
                        loading can submit a request built from a partially
                        populated view.
Severity                P3
Implementation size     S
Contract impact         NONE
Recommended action      Fold into TASK-211: gate `requestStart` on `loading` (or
                        register the handler only when enabled).
```

```text
Gap                     NG-06 — the Lobby hint line blanks out exactly when it is needed
Current behavior        `describeStartTriggerMessage()` returns `this.selectionMessage`
                        (often `''`) whenever `startError !== null`
                        (`LobbyScene.ts:886-888`), so the guidance line goes empty on
                        an error.
Evidence                LobbyScene.ts:882-898.
Player impact           Minor legibility regression on the error path.
Severity                P3
Implementation size     S
Contract impact         NONE
Recommended action      Fold into TASK-211 while the error path is being reworked.
```

### Part B — Result

```text
Gap                     NG-07 — the reward read has no loading, error or retry state
Current behavior        `create()` fires `void this.loadRewards(rewardRun)`
                        (`ResultScene.ts:191`). The reward `Text` is created empty
                        (`:277-285`) and nothing is written before the `await`.
                        `loadRewards` keeps only `rewards` and swallows every failure
                        with a bare `catch { return; }` (`:464-471`) — no message, no
                        notice, no retry. The file registers exactly two controls
                        (`:297-305`) and states it has no keyboard handler, tap target
                        or timer (`:288-296`), so a retry is not even expressible.
Evidence                ResultScene.ts:191,277-285,288-305,446-482;
                        RewardSummaryFormat.ts:36-40; ResultScene.test.ts:833-845
                        (asserts the area stays empty on failure).
Player impact           The game's only reward channel — the entire
                        Battle → Reward → Progression leg — can silently display
                        nothing, and the player cannot distinguish "the server is
                        still answering" from "your rewards are gone". Note the
                        accurate part of TASK-207's U1: the *failure* state is silent
                        and unrecoverable; the *zero-reward* state is distinguishable
                        (it renders `REWARDS` + `+0 XP`).
Severity                P1
Implementation size     S
Contract impact         NONE
Recommended action      Add loading / failure / RETRY states to the reward block as
                        part of TASK-211, following the RETRY pattern already used by
                        Collection and History, and keep the run guard.
```

```text
Gap                     NG-08 — `durationTurns` is delivered and discarded
Current behavior        The battle-result response carries `durationTurns`
                        (`API_CONTRACTS.md` §4; `BattleModels.ts:484`), but
                        `loadRewards` reads only `result.rewards`
                        (`ResultScene.ts:465-466`) and `renderResult` destructures only
                        `outcome`, `finalBossHp`, `finalPlayerHp` (`:410`).
Evidence                ResultScene.ts:410,465-466; BattleModels.ts:484;
                        API_CONTRACTS.md:457,502-504; BattleHistoryScene.ts:704 renders
                        the same value ("Duration: N turns").
Player impact           The player sees the battle's length in the History ledger but
                        not on the screen that summarises the battle they just played.
Severity                P2
Implementation size     S
Contract impact         NONE
Recommended action      Render the delivered `durationTurns` on the Result screen.
```

```text
Gap                     NG-09 — "Final Player HP:" labels the Pet's HP
Current behavior        `ResultScene.ts:428` prints `Final Player HP: ${finalPlayerHp}`
                        (and `:406` the `—` fallback). The value is the battle's
                        `finalPlayerHp`, which `SIGNALR_PROTOCOL.md:1043` defines as
                        "Active Pet HP at battle end (`GAME_STATE.md` §2.3
                        `PetState.HP`) — the wire name `finalPlayerHp` is a fixed
                        protocol label; there is no Player HP pool (ADR-011)".
Evidence                ResultScene.ts:406,428; SIGNALR_PROTOCOL.md:1043,1057-1059;
                        BattleScene.ts:2078-2079; ADR-011:27;
                        COMBAT_RULES.md:656; GAME_EVENTS.md:613-614.
Player impact           The screen tells the player their own health was a number that
                        is actually their Pet's, contradicting the Player/Pet split the
                        rest of the game is built on. The wire name is correct; only
                        the player-facing label is wrong.
Severity                P2
Implementation size     S
Contract impact         NONE (the wire name `finalPlayerHp` is a fixed protocol label
                        and must not change — the UI label is the client's)
Recommended action      Relabel to the Pet's HP as part of TASK-211. Do NOT rename the
                        wire member, and do not change SIGNALR_PROTOCOL.md.
```

```text
Gap                     NG-10 — a `null` reward payload would reject out of a voided promise
Current behavior        The delivered `rewards` is never shape-validated and the render
                        call sits outside the try/catch:
                        `this.rewardText?.setText(formatRewards(rewards))`
                        (`ResultScene.ts:481`, catch ends at `:471`).
                        `formatDeliveredValue` handles `null` but not `undefined`
                        (`RewardSummaryFormat.ts:50-52`), so a `{}`-shaped payload
                        would render literal `undefined`.
Evidence                ResultScene.ts:462-482; RewardSummaryFormat.ts:50-66;
                        contrast GameRuntime.ts:1049-1105 (strict battle-state shape
                        checks). NOT confirmed against a live server — the backend
                        always emits an object (`BattleResultResponse.cs:66,107-114`).
Player impact           Latent robustness: a contract drift would surface as an
                        unhandled rejection plus a half-empty block rather than a
                        degraded empty state.
Severity                P2
Implementation size     S
Contract impact         NONE
Recommended action      Fold into TASK-211's reward-state work: treat an absent or
                        malformed `rewards` as the failure state, not as data.
```

### Part C — Authentication / shell

```text
Gap                     NG-11 — duplicate-username error never matches
Current behavior        `AuthScreen.tsx:43` matches `USERNAME_ALREADY_TAKEN`; the
                        backend returns `USERNAME_ALREADY_EXISTS`
                        (`AuthController.cs:84`). The match fails and the fallback
                        (`:50`) shows the raw token:
                        `Đăng nhập thất bại: USERNAME_ALREADY_EXISTS`.
Evidence                AuthScreen.tsx:41-51; AuthController.cs:84;
                        ApiService.ts:82-86 (the code does reach the client);
                        no `AuthScreen.test.tsx` exists.
Player impact           A new player whose chosen name is taken — one of the most
                        likely first-run errors — sees an untranslated machine token
                        instead of the message the product intends.
Severity                P2
Implementation size     S
Contract impact         NONE
Recommended action      Correct the matched code. Small enough to ride along with
                        TASK-211 or to be its own one-line BUG task; it is NOT in the
                        proposed TASK-211 scope as currently worded.
```

```text
Gap                     NG-12 — no logout; a mid-session 401 never resets the session
Current behavior        No `logout`/`signOut` identifier exists anywhere in `src/`;
                        `ApplicationSession.clear()` has exactly one application caller,
                        the failed bootstrap connect (`App.tsx:47`). The shared
                        transport throws a generic `… failed with status 401`
                        (`ApiService.ts:133-135,316-318`) and nothing clears the token
                        or transitions the session.
Evidence                repo-wide grep; App.tsx:37-53,119-125; ApiService.ts:133-135;
                        ADR-020 (documents that a sign-out clears the token).
Player impact           An expired session leaves the player on screens whose only
                        offered action is a RETRY that cannot succeed, with no way to
                        sign out and start over.
Severity                P2
Implementation size     M
Contract impact         NONE on the API (the 401 contract exists); ADR-020 already
                        documents the intended logout
Recommended action      Separate task (documented-but-unimplemented behaviour → BUG).
                        Not part of TASK-211.
```

```text
Gap                     NG-13 — a post-login connect failure silently returns to a blank login form
Current behavior        `handleAuthenticated` sets session status `'error'` when
                        `initialize()` rejects (`App.tsx:104-108`), and `:119` then
                        renders `AuthScreen` — but the session token was already
                        established and stored (`ApiService.ts:112` →
                        `ApplicationSession.ts:89`) and `AuthScreen`'s message state is
                        freshly null, so no banner appears.
Evidence                App.tsx:101-125; ApiService.ts:88-90,111-113;
                        ApplicationSession.ts:89,108.
Player impact           The player "logs in", is bounced back to the login screen with
                        zero explanation, and a reload silently re-authenticates.
Severity                P2
Implementation size     S
Contract impact         NONE
Recommended action      Separate small task (or fold into NG-12's session lifecycle
                        work).
```

### Part D — Battle-screen residual (TASK-210's reported gaps, re-verified)

```text
Gap                     NG-14 — the Pet's Signature Skill has no name source, so the
                        4th cast control is mislabelled on every battle
Current behavior        `renderCastControls` looks the delivered `petState.equippedCards`
                        entry up in `cardDefinitions`, which is populated only from
                        `runtime.getCards()` (`BattleScene.ts:1156-1173`); with no
                        definition, `isPetSkill` is false and the label falls back to
                        `Card: <raw id>` (`:1230-1233`). `GET /api/cards` serves rows in
                        `PlayerUnlockedCard`, and a `Category = PetSkill` card is
                        "never an unlock row" by contract, so the definition can never
                        be returned there. The cards exist in DB content
                        (`card-inferno`, `card-venomous-bloom`, `card-earthshaker`, …)
                        and are named in `CARD_RULES.md` §4.1.
Evidence                BattleScene.ts:1156-1173,1228-1233;
                        PlayerStarterGrantFactory.cs:62-67; CARD_RULES.md:109-113;
                        DATABASE.md:1302-1305; API_CONTRACTS.md:783-786;
                        CollectionQueryService.cs:192-222; CardRepository.cs:47-63;
                        SceneLifecycle.test.ts:308-313 and
                        BattleEventPresentation.test.ts:650-655 (both INJECT the
                        definition, masking the gap); E2E
                        standalone-web-smoke.mjs:1994-2046 asserts only
                        accepted/rejected, never the label.
Player impact           On the primary interaction surface, in every battle, the Pet's
                        Signature Skill is presented as a *Card* with a raw developer
                        id instead of `Skill: Inferno`. The control still works, so it
                        is legibility, not breakage. This is the most visible of
                        TASK-210's four reported gaps.
Severity                P1 (visibility) — does not block play
Implementation size     M
Contract impact         DECISION-REQUIRED — the client has no name source at all, and
                        inventing a client-side catalog would be a second source of
                        truth (AGENTS.md §7/§9). The owner is API_CONTRACTS.md: either
                        §5.1/§3 carrying the derived Signature Skill, or a §5.x
                        definition read. Widening §5.3's membership semantics would
                        contradict CARD_RULES.md §1 item 4 and ADR-012.
Recommended action      New decision-then-implementation task (TASK-219 in §6). Do not
                        fold into TASK-211 (different surface, different type) and do
                        not fold blindly into TASK-212 (different owner section).
```

```text
Gap                     NG-15 — Relic triggers are invisible; the fix needs no contract change
Current behavior        `RelicTriggered` delivers `{ type, relicId }` only
                        (`BattleEventWireProjection.cs:553-556,767-768`), where
                        `relicId` is the owned Relic *instance* identity. `BattleScene`
                        loads only card definitions, so `BattleEventPresenter` produces
                        no callout for a relic trigger. However `GET /api/relics`
                        already returns `relicId` (= `Relic.RelicInstanceId`) with
                        `name` (= `RelicDefinition.Name`), and the runtime port already
                        exposes `getRelics()`.
Evidence                BattleEventWireProjection.cs:553-556;
                        RelicWireProjectionTests.cs:46-55; RelicResolver.cs:314,381;
                        API_CONTRACTS.md:802-806; CollectionController.cs:220-233;
                        GameRuntimeEvents.ts:538; GameRuntime.ts:812;
                        BattleEventPresenter.ts:624-628 ("this scene has no Relic
                        definition source" — true only because BattleScene never loads
                        the read it is allowed to call); LobbyScene.ts:446-447 and
                        CollectionViewerScene.ts:333-334 do call it.
Player impact           A core build mechanic is silent: the player never learns which
                        Relic fired, although the Relic effects themselves show up as
                        HP/Power movement.
Severity                P2
Implementation size     S
Contract impact         IMPLEMENTATION-ONLY — **TASK-210 misclassified this as a
                        contract gap.** No wire member, endpoint, or document change is
                        needed.
Recommended action      New small client-presentation task (TASK-218 in §6): build an
                        instanceId → name map from the existing `getRelics()` exactly
                        as `loadCardDefinitions()` already does, and fall back to the
                        raw id when unresolved (TASK-208 §D's "never a guessed name").
```

```text
Gap                     NG-16 — healing has no amount feedback
Current behavior        `ResourceGenerator.ApplyHeal` emits no event and adds no
                        `BattleEventType` member (`:400-404`); `GAME_RULES.md` §16's
                        canonical event list has no heal event. The *result* of healing
                        is visible through the delivered `petState.hp`/`maxHp` gauge and
                        numbers; the *amount* is not, and deriving it from two states is
                        forbidden (AGENTS.md §10, ADR-001).
Evidence                ResourceGenerator.cs:400-404,437-447;
                        PlayerEffectHealingTests.cs:467-482; GAME_RULES.md:323-334;
                        GAME_EVENTS.md:664-670 ("a value the client needs but no event
                        reports is a gap to be resolved by adding an event … not by
                        delivering state on a side channel").
Player impact           Cosmetic — the HP gauge moves, only the "+N" attribution is
                        missing. Does not block.
Severity                P3
Implementation size     S–M
Contract impact         DECISION-REQUIRED — requires GAME_RULES.md §16 +
                        GAME_EVENTS.md §2 + SIGNALR_PROTOCOL.md §3.2 (closed event set).
Recommended action      Separate event-contract task, only after the higher-ranked
                        items. Not TASK-212's subject and not TASK-211's.
```

```text
Gap                     NG-17 — the "no crit flag" gap is a documented refusal; the real
                        finding is stale prose
Current behavior        Crit IS implemented and folded into step 4's combined
                        `otherModifiers` (`DamagePipeline.cs:424-437,536-542`), and
                        COMBAT_RULES.md §3.3 item 6 explicitly decides that "No
                        separate Crit event, state property, or wire member is emitted",
                        with SIGNALR_PROTOCOL.md §4 item 14 forbidding the client to
                        reconstruct one. So there is no open gap. But
                        SIGNALR_PROTOCOL.md:826 and :848-852 still describe
                        `otherModifiers` as a "pass-through 1.00 in MVP", and
                        DamageEvents.cs:104-113 still asserts that no Crit roll exists —
                        both false, and contradicted by backend tests that assert 1.5
                        and 1.30.
Evidence                DamagePipeline.cs:424-437,540;
                        COMBAT_RULES.md:396-398; SIGNALR_PROTOCOL.md:826,848-852,
                        1491-1499; DamageEvents.cs:104-113;
                        EffectiveCritCompositionTests.cs:118-119,239;
                        Task177RelicFiringPointRuntimeTests.cs:152,479.
Player impact           A ~5%-chance damage spike arrives with no explanation. Cosmetic;
                        the prose does not mislead a player, only a future implementer.
Severity                P3
Implementation size     S
Contract impact         DOCUMENTATION-ONLY
Recommended action      Report the doc-vs-doc conflict per AGENTS.md §4 (§7 below) and
                        fold the correction into TASK-217. Do NOT create a crit task, and
                        do NOT reopen COMBAT_RULES.md §3.3 item 6 on this evidence.
```

### Part E — Product-loop gaps carried over from TASK-207

```text
Gap                     NG-18 — the Lobby asks for three Cards and three Relics it cannot describe
Current behavior        `GET /api/cards` exposes only `cardId`/`name`/`category` and
                        deliberately excludes `powerCost` and `effectDefinition`
                        (API_CONTRACTS.md:783-786); `GET /api/relics` exposes only
                        `relicId`/`name` (:800-811). The Lobby therefore lists effects
                        it cannot state, and the battle cast tiles show no cost or
                        affordability.
Evidence                API_CONTRACTS.md:783-786,800-811; LobbyScene.ts:949,973,999,1026;
                        SIGNALR_PROTOCOL.md §4 item 15 forbids reconstructing a cost
                        or modifier client-side; CARD_RULES.md §3.6.
Player impact           GDD §17's comprehension promise ("what each Card/Relic changes")
                        is unachievable: the player equips three of each with no basis
                        for the decision. This is a happy-path, every-battle gap.
Severity                P1
Implementation size     M
Contract impact         DECISION-REQUIRED (widens API_CONTRACTS §5.3/§5.4)
Recommended action      Keep as TASK-212, unchanged in substance, decision first.
```

```text
Gap                     NG-19 — 3 of the Lobby's 4 decisions are forced
Current behavior        A player permanently owns 1 Pet, all 3 Basic Cards and exactly
                        3 Relics, with no acquisition path; only the Boss is a real
                        choice.
Evidence                PlayerStarterGrantFactory.cs:56,69-74,90-95;
                        LobbyScene.ts:98,724,727; DATABASE.md:1287-1289,1320-1327.
Player impact           GDD §1.3's "Build Diversity" pillar is not exercisable; the
                        Collection cannot grow.
Severity                P1 (product loop)
Implementation size     S (decision) — implementation would be separate
Contract impact         DECISION-REQUIRED (MVP_SCOPE §1 lists the content as IN)
Recommended action      Keep as TASK-213, unchanged.
```

```text
Gap                     NG-20 — Player Level/XP has no home outside a finished battle
Current behavior        Progression is persisted and rendered only inside a reward
                        block or in the newest Battle History entry; a fresh account
                        shows `—`; no progression read endpoint exists.
Evidence                BattleHistoryScene.ts:418,519,530-544;
                        BattleController.cs:117,210,292; CollectionController.cs.
Player impact           The retention signal (level/XP) is invisible wherever the
                        player is not literally reading a past battle.
Severity                P2
Implementation size     M
Contract impact         DECISION-REQUIRED (new read endpoint)
Recommended action      Keep as TASK-214.
```

```text
Gap                     NG-21 — "Tier, Star, Level progression" is an MVP promise the game cannot keep
Current behavior        MVP_SCOPE.md:53 and ROADMAP.md:51 list Tier/Star/Level
                        progression as IN; PET_RULES.md §5.7/§6 describe stat scaling;
                        TASK-200 Q-6 decided the axes are cosmetic for MVP and
                        PetState uses fixed constants.
Evidence                MVP_SCOPE.md:53; ROADMAP.md:51; PET_RULES.md:329-330,344-356;
                        TASK-200 Q-6; PetState.cs:740,757; PetTier.cs:20-22;
                        TASK-208 §J-2 / D-208-05.
Player impact           Indirect, but it is a genuine AGENTS.md §4 conflict that will
                        be re-discovered by the next content task.
Severity                P2
Implementation size     S
Contract impact         DECISION-REQUIRED (Product Owner narrows the scope text or
                        authorizes the curve)
Recommended action      Keep as TASK-215.
```

```text
Gap                     NG-22 — Player-XP persistence is ordering-dependent (UNVERIFIED)
Current behavior        `PlayerRepository.SaveProgressionAsync` can return without
                        `SaveChangesAsync` when the entry is `Unchanged` and present in
                        `Local`; today the grant is flushed incidentally by the next
                        call (`PetRepository.SaveProgressionAsync` → `SaveChangesAsync`),
                        which itself returns early when the Pet row is absent. No test
                        composes `BattleResultService` with real EF repositories for
                        that case.
Evidence                PlayerRepository.cs:191-222; PetRepository.cs:105-134;
                        BattleResultService.cs:511-531; TASK-207 §J-3;
                        TASK-208 §J-3 (both recorded it as a RISK, not a defect).
Player impact           If confirmed: the only reward the game grants is silently
                        dropped while the persisted RewardSummary reports it as applied.
                        Unproven — must be verified before it is called a defect.
Severity                P2 (P1 if confirmed)
Implementation size     S
Contract impact         NONE expected
Recommended action      Keep as TASK-216, unchanged: verify first, then fix only if
                        confirmed.
```

### Part F — Traceability

```text
Gap                     NG-23 — the TASK-209 record does not exist
Current behavior        TASK-210 declares TASK-209 DONE and builds on its section
                        numbering (`TASK-210:30,42,105,241`), the shipped code and
                        tests cite `TASK-209 §4/§5/§6/§12`, and the whole projection +
                        HUD implementation is in the working tree — but no
                        `TASK-209-*.md` exists under `tasks/`.
Evidence                glob `tasks/**/*209*` → no files; grep for `TASK-209` finds
                        only references (TASK-210, SceneLifecycle.test.ts:1184,1265,
                        1367,1447,1868,1961,1985; RuntimeBoundaries.test.ts:196,274;
                        BattleEventPresentation.test.ts:930,1059; BattleScene.ts:562,
                        921,1024,1717,2036,2135; App.tsx:136;
                        standalone-web-smoke.mjs:551,1422,1615,2049).
Player impact           None directly; it is a traceability/process gap
                        (tasks/README.md §5, TASK_LIFECYCLE.md §3).
Severity                P2 (process)
Implementation size     S
Contract impact         NONE
Recommended action      Separate bookkeeping task (reconstruct the record, or record
                        it as SUPERSEDED) owned by an orchestrator/review role. Not
                        TASK-211, and not repaired by this audit.
```

### Severity roll-up

| Severity | Gaps |
|---|---|
| **P1** | NG-01 (Lobby has no exit), NG-02 (unrecoverable collection read), NG-03 (error envelope discarded), NG-07 (silent reward failure), NG-14 (Signature Skill mislabelled — decision-gated), NG-18 (Card/Relic effects undescribed — decision-gated), NG-19 (forced loadout — decision-gated) |
| **P2** | NG-04, NG-08, NG-09, NG-10, NG-11, NG-12, NG-13, NG-15, NG-17, NG-20, NG-21, NG-22, NG-23 |
| **P3** | NG-05, NG-06, NG-16, NG-17-severity, NG-13-severity, plus the U13/U14/U15/terminology items in §2.3 |
| **P0** | NONE — no gap found blocks the core playable loop |

---

## 4. TASK-211 Decision

```text
MODIFY
```

**Why not DROP / REPLACE.** The proposed scope targets the right two screens and
both halves are still real, verified against the current code and untouched by
TASK-209/TASK-210: the Lobby has no way out and no retry (`LobbyScene.ts:678`,
`:434`, `:456`), and the Result screen's reward read fails silently with no
loading/error/retry (`ResultScene.ts:277-285`, `:464-471`). Nothing in the
TASK-209/TASK-210 chain superseded either. The battle-surface work that the
roadmap expected to run *before* TASK-211 did land, and it did **not** remove or
absorb TASK-211's subject — it is a different surface.

**Why not KEEP.** Three corrections are needed.

1. **The Lobby's real defect is broader than "failure recovery".** TASK-207
   framed it as "a failed or empty collection read forces a page reload", but the
   verified code has **no exit at all**, on the happy path too: the only
   `scene.start` in the file is the successful battle start. A task scoped to
   "failure recovery" would fix the retry and still leave the player trapped.
   The missing control is one and the same, so this is a correction of the
   scope, not an expansion.
2. **TASK-207's U3 is mis-stated and the real defect is worse.** The proposal
   says "human-readable start-failure messages", implying the fix is local to
   the Lobby. In fact `ApiService.post` discards the entire §6 error envelope
   (`ApiService.ts:316-318`), so even the machine code is lost and all three
   documented rejections collapse to one HTTP-status string. The fix belongs in
   the shared transport and benefits Collection and History as well — and it is
   the *documented* behaviour the client is ignoring, so it is a defect, not a
   new feature.
3. **Adding the Lobby exit makes an existing latent async defect reachable.**
   The Lobby has no run guard (`LobbyScene.ts:457-459,671-678`) while
   `ResultScene` has had one since TASK-205. Leaving mid-flight is currently
   impossible only because leaving is impossible. The two must land together.

**Why not SPLIT.** The two halves share one task type, one screen family, one
verification surface (the scene-lifecycle suite plus the E2E) and one session of
work. Splitting would create two records and two review passes for one coherent
change. *If a reviewer enforces `TASK_TYPES.md` §3's one-type-per-task rule
strictly*, the correct split is **TASK-211 (Lobby + transport error legibility)**
and **TASK-211A (Result reward states + duration + HP label)** — the Lobby half
is now M-sized on its own and the Result half is S. This audit does not
recommend that split, but records it as the fallback.

### Proposed TASK-211 implementation scope (MODIFY)

**In scope — Lobby / shell**

1. `< BACK` control on the Lobby → `MainMenuScene`, reachable in every Lobby
   state (loading, error, ready).
2. RETRY for the collection read that re-invokes `loadCollection()`, preserving
   the existing `startPending` in-flight guard and the already-readable
   empty/loading notes.
3. Parse the `API_CONTRACTS.md` §6 error envelope in the shared authenticated
   transport (`get`/`post`) and surface the server's `message` (falling back to
   `error`) in the Lobby's error line — for the collection read and the battle
   start alike. Keep the raw status available for diagnostics, but do not lead
   with it.
4. Adopt the TASK-205 run/scene guard (`ResultScene`'s `rewardLoadRun` pattern)
   for both Lobby async continuations, so a stale `loadCollection` cannot render
   into a torn-down scene and a stale `startBattle` cannot navigate after BACK.
5. Gate `requestStart` on `loading` so the disabled START BATTLE state is real.
6. Keep the hint line expressive on the error path (`describeStartTriggerMessage`).

**In scope — Result**

7. Loading state for the reward block (a visible "Loading rewards…"), a failure
   state that says the rewards are unavailable (explicitly *not* a zero reward),
   and a RETRY control that re-invokes the read under a fresh run id.
8. Fail closed on an absent/malformed `rewards` payload → the same failure state,
   with the render call inside the guard.
9. Render the delivered `durationTurns`.
10. Correct the `Final Player HP:` label so it names the Pet's HP, matching
    `SIGNALR_PROTOCOL.md:1043` and ADR-011. The wire member is untouched.

**Out of scope (explicit non-goals)**

- The Signature Skill read source (NG-14) — contract-decision-required.
- Naming `RelicTriggered` (NG-15) — battle surface, separate task.
- Any heal event or crit indicator (NG-16, NG-17) — contract decisions.
- Card cost/effect exposure (NG-18) — decision-gated, TASK-212.
- Logout / 401 session reset (NG-12) and the post-login blank-form bounce
  (NG-13) — separate session-lifecycle work.
- The `USERNAME_ALREADY_TAKEN` typo (NG-11) — one-line BUG; may ride along only
  if the implementer's task type permits a riding defect (TASK-210 precedent).
- Silent list truncation (U13), dead endpoints (U14/U15), localization, and every
  `docs/` change (TASK-217).
- Any change to gameplay, the wire contract, the database, or an ADR.

---

## 5. Recommended Next Task

```text
TASK-211 — Non-Battle Screen Recovery & Readability
            (Lobby escape/retry/error legibility + Result reward states)
Decision:   MODIFY (the kebab title stays; the scope in §4 is the binding one)
```

**Objective.** Make every non-battle screen the player can reach survivable and
legible: the Lobby must be escapable and recoverable when a read fails, a
rejected battle start must tell the player what the server actually said, and the
Result screen must never present an unexplained blank where the game's only
reward belongs — while reading the authoritative values it already receives.

**Scope.** Items 1–10 of §4 "Proposed TASK-211 implementation scope".

**Non-goals.** Every bullet in §4 "Out of scope". In particular: no wire member,
no endpoint, no event, no doc, no ADR, no gameplay rule, no migration.

**Expected files / areas.**

```text
src/frontend/client/src/game/scenes/LobbyScene.ts        BACK, RETRY, run guard,
                                                         loading gate, hint line
src/frontend/client/src/game/scenes/ResultScene.ts       reward loading/failure/
                                                         RETRY, duration, HP label,
                                                         payload guard
src/frontend/client/src/services/api/ApiService.ts       §6 error-envelope parsing
                                                         (get/post)
src/frontend/client/tests/LobbyScene.test.ts             new state coverage
src/frontend/client/tests/ResultScene.test.ts            new state coverage
src/frontend/client/tests/SceneLifecycle.test.ts         Lobby run-guard coverage
                                                         (Result equivalent exists)
src/frontend/client/scripts/standalone-web-smoke.mjs     failure-path checks +
                                                         a Lobby BACK check
docs/                                                     NONE
src/backend/ · tests/backend/ · migrations                 NONE
```

**Contract impact.** NONE. Every change reads values or errors the existing
contracts already deliver (`API_CONTRACTS.md` §4, §6; `SIGNALR_PROTOCOL.md`
§3.2.19). The `finalPlayerHp` *wire name* is a fixed protocol label and must not
change; only the UI string changes.

**Estimated effort.** S–M. The Result half is S (three states, one value, one
string). The Lobby half is S–M because it adds a control, a retry path, the
run guard and the shared transport parse. If the reviewer splits per
`TASK_TYPES.md` §3, the two halves are S and M respectively.

**Verification requirements.**

```text
Required commands
  npx tsc --noEmit                       PASS
  npx vitest run                         PASS (no existing test weakened; the
                                         failure-state assertions that pin the
                                         current defective behaviour — e.g.
                                         ResultScene.test.ts:833-845 — are
                                         updated to the new documented intent,
                                         with the reasoning recorded per
                                         AGENTS.md §15)
  dotnet test (unchanged backend)        not required (no backend file)
  git diff --check                       PASS

Required new coverage
  Lobby   BACK navigates to MainMenuScene from loading, error and ready states
  Lobby   RETRY re-invokes the collection read exactly once and clears the error
  Lobby   a failed start shows the server's §6 message, not `status NNN`
  Lobby   a promise settling after SHUTDOWN renders nothing and starts nothing
          (the ResultScene run-guard test's shape, SceneLifecycle.test.ts:4546-4604)
  Lobby   START BATTLE cannot submit while the collection read is in flight
  Result  loading state is visible before the read settles
  Result  failure state is visibly different from a zero reward
          (`REWARDS` + `+0 XP` must still render for a real zero reward)
  Result  RETRY re-reads and renders the rewards
  Result  durationTurns is rendered
  Result  the terminal HP line names the Pet's HP
  Result  an absent/malformed rewards payload degrades to the failure state
          without an unhandled rejection

Required E2E
  standalone-web-smoke.mjs   a Lobby `< BACK` check and at least one
                             failure-path check; the existing
                             phase2.statusOverlayMounted check must be
                             re-examined because TASK-209 §6 made that
                             element DEV-only (NG-18 in §2.3/§3).
```

---

## 6. Recommended Task Sequence

Priority order. Effort: **S** ≤ 1 session · **M** = 2–4 · **L** = 5+.

| # | Task | Title | Priority | Effort | Contract impact | Why here |
|---|---|---|---|---|---|---|
| 1 | **TASK-211** (MODIFY) | Non-battle screen recovery & readability | **P1** | S–M | NONE | The only substantial P1 cluster that is unblocked. Removes a hard navigation dead end, the unrecoverable collection read, the discarded error envelope, and the silent reward failure. Presentation-only, low risk, high certainty. |
| 2 | **TASK-218** (NEW) | Name the Pet's Relic triggers in battle from the read the client already has | P2 | S | NONE (implementation-only) | The cheapest remaining battle-screen legibility win, on the surface TASK-209/210 just polished. Verified solvable with the existing `GET /api/relics` (`API_CONTRACTS.md:802-806`); TASK-210 wrongly classified it as contract-blocked, so it was never scheduled. |
| 3 | **TASK-212** | Expose Card cost/effect and Relic effect at the decision point | P1 | M | DECISION-REQUIRED first | GDD §17 comprehension is unmet on the happy path — the player equips three Cards and three Relics it cannot describe. Kept at its roadmap position but now *behind* TASK-211 only because its first half is a decision. |
| 4 | **TASK-219** (NEW) | Pet Signature Skill read source (decision), then name the 4th cast control | P1 (visibility) | M | DECISION-REQUIRED first | Every battle currently labels the Pet's Signature Skill `Card: card-inferno`. It needs a Product Owner/read-contract decision; the implementation cannot precede it (`AGENTS.md` §7/§17). |
| 5 | **TASK-216** | Verify (and only then fix) the Player-XP persistence ordering risk | P2 (P1 if confirmed) | S | NONE | Protects the only reward the game grants. Unproven after two audits; it must be verified before it is called a defect. Cheap and independent. |

**Remaining roadmap tasks, re-ranked but unchanged in substance.** TASK-213
(content reachability — decision, P1 for the product loop), TASK-214 (account
progression surface — decision + contract, P2), TASK-215 (Tier/Star MVP scope
reconciliation — decision, P2), TASK-217 (documentation reconciliation — P3,
now also owning the NG-17 crit/`otherModifiers` staleness and
`ARCHITECTURE.md`'s "No gameplay HUD exists yet"), plus the two new
session-lifecycle items (logout/401 reset, post-login bounce) and the NG-11
one-line BUG, which are not yet assigned IDs.

**Changes to TASK-207's ordering, and why.**

- TASK-211 stays next: verified still real, still unblocked, still the largest
  unblocked P1 cluster. Its scope is corrected (§4) rather than replaced.
- **TASK-218 is new and inserted at #2.** TASK-210's record blocked it by
  classifying it as a contract gap; verification shows it is not one. It is the
  best value-per-risk item on the board and it was invisible in the old roadmap.
- **TASK-219 is new and sits at #4.** TASK-207 folded the Signature Skill
  problem into the generic "card cost/effect" concern (U4/G12). Verification
  shows it is a distinct read-source decision with a distinct owner section, and
  it is the most *visible* defect of the four TASK-210 reported — so it deserves
  its own decision + implementation rather than being an unstated side effect of
  TASK-212.
- **TASK-216 keeps its P2 rating** but rises in the practical order because it
  is S-sized and independent; it is a verification task, so it can also run in
  parallel with #3/#4.
- Nothing in the old roadmap is dropped. TASK-213/214/215 are unchanged in
  substance but remain decision-gated, so they cannot occupy the next slot.

---

## 7. Contract Gaps

The three categories are kept strictly separate, as required.

### 7.1 IMPLEMENTATION-ONLY gaps (no contract change needed)

A gap is implementation-only when the authoritative document *already*
prescribes the behaviour and the code does not do it, **or** when the data is
already delivered and only the client's use of it is missing.

| ID | Gap | Why it is implementation-only | Size |
|---|---|---|---|
| NG-15 | `RelicTriggered` has no player-facing name | The wire carries the owned Relic instance identity and `GET /api/relics` (§5.4) already maps instance identity → `RelicDefinition.Name`; the runtime port already exposes `getRelics()`. `BattleScene` simply never calls it. **TASK-210 classified this as a contract gap — that classification is wrong.** | S |
| NG-03 | Start/read failures discard the documented error envelope | `API_CONTRACTS.md` §6 defines `{ error, message }` and the server returns it; `ApiService.get/post` never parse the body. The client is ignoring an existing contract. | S |
| NG-04 | Lobby async continuations unguarded | `ARCHITECTURE.md` §2.2.3 + the TASK-205 pattern already govern this; `ResultScene` implements it. | S |
| NG-05 / NG-06 | Cosmetic Lobby state defects | No contract involved. | S |
| NG-07 / NG-08 / NG-09 / NG-10 | Result reward states, `durationTurns`, HP label, payload guard | All four values/behaviours are already delivered or already documented (`API_CONTRACTS.md` §4; `SIGNALR_PROTOCOL.md` §3.2.19). The `finalPlayerHp` wire name is fixed and must not move; only the UI string is wrong. | S |

### 7.2 DOCUMENTATION-ONLY gaps

| ID | Gap | Owner | Note |
|---|---|---|---|
| NG-17 | `SIGNALR_PROTOCOL.md:826` and `:848-852` describe `otherModifiers` as a "pass-through `1.00` in MVP"; `DamageEvents.cs:104-113` asserts no Crit roll exists. Both are false (`DamagePipeline.cs:437` applies 1.5; a Burn relic composes 1.30; backend tests assert both). | `SIGNALR_PROTOCOL.md` §3.2.13 (report per `AGENTS.md` §4 — see below); `DamageEvents.cs` comment is implementation-side | Fold into TASK-217 |
| — | `ARCHITECTURE.md` §2.2.2 item 5 still says "No gameplay HUD exists yet" — false since TASK-209 and doubly false after TASK-210. Already assigned to TASK-217 by TASK-207 §J-4, TASK-208 §K item 5 and TASK-210 item 4. | TASK-217 | Not re-litigated here |
| — | TASK-210's record over-classifies the Relic gap as a contract gap and labels the crit item a "contract gap". | TASK-210 record is **immutable** (`TASK_LIFECYCLE.md` §3) | Corrected by this audit; no edit made |
| NG-23 | The TASK-209 record is missing from `tasks/`. | Orchestrator/review bookkeeping | Traceability, not a contract |

**Reported conflict, not resolved (`AGENTS.md` §4).** `COMBAT_RULES.md:396-398`
§3.3 item 6 (a domain rule) states that the Crit outcome *is* carried within step
4's combined `otherModifiers`, while `SIGNALR_PROTOCOL.md:826,848-852` (a
technical document) states that `otherModifiers` is a pass-through `1.00` in MVP.
Under the §2 precedence order the specific domain rule governs, so the technical
prose is the stale side — and the code agrees with the domain rule. This audit
detects, explains, names the owning document, and **stops**; TASK-217 owns the
correction.

### 7.3 CONTRACT-DECISION-REQUIRED gaps

These cannot be implemented by any task until a Product Owner decision is
recorded and the owning document is amended (`AGENTS.md` §7, §17, §18).

| ID | Gap | Decision owner / document | What must be decided | Size |
|---|---|---|---|---|
| NG-14 | The Pet's Signature Skill Card has no read source; the 4th cast control renders `Card: card-inferno` | `API_CONTRACTS.md` — §5.1 (derive the Signature Skill into the Pet read) or §3 (carry it in the battle-start response), **not** §5.3's membership semantics | How the derived Signature Skill's identity and/or display name reaches the client without making a `PetSkill` card an unlock row (`CARD_RULES.md` §1 item 4, ADR-012 item 9) and without a second client catalog | M |
| NG-18 | Card cost/effect and Relic effect are not exposed at the decision point | `API_CONTRACTS.md` §5.3/§5.4 (+ whether `powerCost`/`effectDefinition`/trigger text become readable, and in what presentation-safe form) | Whether to widen the two read contracts, and how much of `effectDefinition`/`Trigger`/`Condition` may be exposed | M |
| NG-16 | Healing emits no event, so no heal *amount* can be shown | `GAME_RULES.md` §16 + `GAME_EVENTS.md` §2 + `SIGNALR_PROTOCOL.md` §3.2 | Whether to add a heal event to the closed event set, its name, payload and resolution-order slot (`GAME_EVENTS.md:664-670` already names this route) | S–M |
| NG-17 (crit) | A crit indicator is not implementable | `COMBAT_RULES.md` §3.3 item 6 + `SIGNALR_PROTOCOL.md` §4 item 14 | **Do not reopen on this evidence.** The current decision (crit folded into `otherModifiers`, no separate member, client forbidden to reconstruct) is coherent; the defect is the stale prose, not the contract | — |
| NG-19 | 3 of the Lobby's 4 decisions are forced; ownership can never grow | `MVP_SCOPE.md` §1 vs `DATABASE.md` §2 (starter grant is bootstrap-only; no acquisition path) | Whether the content listed as IN becomes reachable, and how | S (decision) |
| NG-20 | Player Level/XP has no read surface | `API_CONTRACTS.md` (a new read endpoint) or a documented decision that Battle History remains its home | Whether to add a progression read | M |
| NG-21 | "Tier, Star, Level progression" is promised by `MVP_SCOPE.md`/`ROADMAP.md` but decided cosmetic and unscaled by TASK-200 Q-6 | Product Owner, on `MVP_SCOPE.md` §1 / `ROADMAP.md` / `PET_RULES.md` §5.7/§6 | Narrow the scope text, or authorize the curve and the Tier/Star axes | S (decision) |
| NG-12 | Logout is documented (ADR-020) but unimplemented; 401 never resets the session | Not an API contract gap — the 401 contract exists. The decision is a client session-lifecycle one | Whether logout is in MVP UI scope and how the session surfaces it | M |

---

## 8. Evidence

### 8.1 Commands executed by this audit

```text
git status --short / git log --oneline / git diff --stat / git diff --check
npx tsc --noEmit                     (src/frontend/client)
npx vitest run                       (src/frontend/client)
dotnet test tests/backend/GameServer.Api.Tests/GameServer.Api.Tests.csproj
dotnet test tests/backend/GameServer.Domain.Tests/GameServer.Domain.Tests.csproj
dotnet test tests/backend/GameServer.Application.Tests/…csproj
dotnet test tests/backend/GameServer.Infrastructure.Tests/…csproj
glob tasks/**/*209*   ·   grep TASK-209|211|212|213 across the repository
grep over the client smoke check inventory (record(…) call sites)
```

Browser E2E was **not** executed (it needs a live backend + PostgreSQL + Redis +
Chromium). Its 100-check inventory was read statically and is cited where
relevant.

### 8.2 Source files inspected

```text
Frontend (client)
  src/app/App.tsx, App.css
  src/ui/components/{StatusOverlay.tsx, AuthScreen.tsx, ViewportDebugOverlay.tsx}
  src/services/api/{ApiService.ts, ApplicationSession.ts, BattleModels.ts,
                    CollectionModels.ts}
  src/services/realtime/SignalRService.ts
  src/game/runtime/{GameRuntime.ts, GameRuntimeEvents.ts}
  src/game/scenes/{LobbyScene.ts, ResultScene.ts, BattleScene.ts,
                   BattleEventPresenter.ts, MainMenuScene.ts, PreloaderScene.ts,
                   CollectionViewerScene.ts, BattleHistoryScene.ts}
  src/game/presentation/RewardSummaryFormat.ts
  src/game/state/PreservedLoadout.ts
  scripts/standalone-web-smoke.mjs

Backend
  src/backend/GameServer.Api/Hubs/{BattleHub.cs, BattleEventWireProjection.cs}
  src/backend/GameServer.Api/Controllers/{AuthController.cs, CollectionController.cs,
                                          CollectionResponses.cs, BattleController.cs}
  src/backend/GameServer.Application/Players/PlayerStarterGrantFactory.cs
  src/backend/GameServer.Application/Collection/{CollectionQueryService.cs,
                                                CardCollectionItem.cs}
  src/backend/GameServer.Application/Battle/BattleResultService.cs
  src/backend/GameServer.Domain/Match3/{ResourceGenerator.cs, BattleEvent.cs}
  src/backend/GameServer.Domain/Combat/{DamagePipeline.cs, DamageEvents.cs}
  src/backend/GameServer.Domain/Cards/{CardCategory.cs, CardCastExecutor.cs}
  src/backend/GameServer.Domain/Relics/RelicResolver.cs
  src/backend/GameServer.Infrastructure/Postgres/Repositories/{CardRepository.cs,
                                                              PlayerRepository.cs,
                                                              PetRepository.cs}
  src/backend/GameServer.Infrastructure/Postgres/Migrations/
      20260929152651_ProvisionPetCardRelicContentDefinitions.cs
      20261004055006_ProvisionThanhXaAndSonHungSignatureSkills.cs
```

### 8.3 Tests inspected

```text
Client (20 files / 783 tests — all passing)
  LobbyScene.test.ts (67)          incl. :873-913 (single startBattle),
                                        :940-944,961 (status string),
                                        :966-984 (retry after rejection),
                                        :1079-1123 (re-entry), :1297 (carrier)
  ResultScene.test.ts (39)         incl. :469-483 (teardown), :772-790 (zero
                                        reward), :833-845 (silent failure)
  SceneLifecycle.test.ts (155)     incl. :308-313 (injected card definition),
                                        :4546-4604 (reward run guard, SHUTDOWN
                                        and stale-run), :1665-1678 (combo)
  BattleEventPresentation.test.ts (54) incl. :650-655 (PET SKILL id fallback)
  RuntimeBoundaries.test.ts (54)   :196, :274 (projection boundaries)
  GameRuntime.test.ts (129), SignalRService.test.ts (42),
  CollectionViewerScene.test.ts (55) incl. :1135 (getPet unused),
  BattleHistoryScene.test.ts (34), BattleService.test.ts (48),
  AppLifecycle.test.tsx (7), StatusOverlay.test.tsx (9), GameShell.test.tsx (6),
  CollectionService.test.ts (27), ApplicationSession.test.ts (12),
  PreservedLoadout.test.ts (6), ViewportCss.test.ts (9), GameViewport.test.ts (18),
  ViewportDebugOverlay.test.tsx (3), PhaserGame.test.tsx (9)

Backend (2919 tests — all passing)
  GameServer.Api.Tests (326)       incl. ApiIntegrationTests.cs (petState/bossState
                                        member sets), Hubs/PetStateWireProjectionTests.cs,
                                        Hubs/BattleHubReconnectRecoveryTests.cs,
                                        RelicWireProjectionTests.cs:46-55
  GameServer.Domain.Tests (1558)   incl. PlayerEffectHealingTests.cs:467-482
  GameServer.Application.Tests (623) incl. EffectiveCritCompositionTests.cs:118-119,239,
                                        Task177RelicFiringPointRuntimeTests.cs:152,479
  GameServer.Infrastructure.Tests (412)
```

### 8.4 Browser smoke coverage inspected (static)

`scripts/standalone-web-smoke.mjs`, 100 `record(...)` checks. Phases: 0 (page
foreground), 1 (registration), 2 (re-login + StatusOverlay + SignalR), 3 / 3b
(Main Menu → Lobby; Battle History on a fresh account + `< BACK`), 4 (Lobby
loadout + boss selection + start), 5 (board + swap), 5b (player-facing HUD),
5c (combat feedback), 6 / 6b / 6c / 6d (cast controls, one-cast-per-turn,
reconnect/resync convergence, Signature Skill usability), 7 (battle completion +
Result), 8 (post-result lifecycle: PLAY AGAIN, loadout restoration and
editability, MAIN MENU, second battle), 9 (health & safety gates).

Coverage relevant to this audit:

```text
PRESENT   phase3b.backReturnedToMainMenu          (History has BACK — the Lobby does not)
PRESENT   phase4.noBossValidationPreventedStart
PRESENT   phase5b/5c/6c/6d HUD, feedback, resync, Signature-Skill usability
PRESENT   phase7.resultSceneReached, terminalHpValuesRendered, noAutomaticTransition
PRESENT   phase8.* post-result lifecycle, preserved loadout, active-state clearing
PRESENT   safety.zeroUncaughtExceptions / zeroFatalConsoleErrors /
          zeroUnexpectedApiResponses / zeroDiscordDependencies
ABSENT    any Lobby `< BACK` check
ABSENT    any Lobby collection-read failure / RETRY check
ABSENT    any battle-start rejection message check
ABSENT    any Result reward loading / failure / RETRY check
ABSENT    any assertion on the reward text at all (rewardText is snapshotted at
          :683 and never asserted); Phase 7/8 assert only outcome and the two HP
          strings (:2286-2291, :2596)
ABSENT    any assertion on the Pet Skill cast-control LABEL
          (:1994-2046 asserts only accepted/rejected) — this is why NG-14 stayed green
PRESENT   phase5b.hudPetNameResolvesFromTheClientCatalog  (proves a pet-name
          catalog exists; NG-14 needs a *card-name* source, which does not)
```

This is the coverage asymmetry that explains why all four continuous-integration
surfaces are green while the P1 gaps in §3 are real: every P1 gap above lies on a
failure path (or on a label) that no unit test, E2E check, or type check asserts
against documented intent.

### 8.5 Completed task records inspected

```text
tasks/completed/TASK-207-product-roadmap-and-gameplay-gap-audit.md   (669 lines)
tasks/completed/TASK-208-resolve-in-battle-pet-state-presentation-contract.md
                                    (1395 lines; §A–§O, §J-1…§J-4, §K, §L, §M)
tasks/completed/TASK-208A-apply-task-208-battle-state-projection-decisions-to-
              authoritative-documentation.md                        (367 lines)
tasks/completed/TASK-210-battle-feedback-and-presentation-pass.md    (532 lines)
tasks/completed/TASK-209-*                                           NOT FOUND
```

### 8.6 Authoritative documents inspected

```text
AGENTS.md (binding contract) · docs/AGENTS.md
tasks/{README.md, TASK_LIFECYCLE.md, TASK_TYPES.md}
docs/00-overview/{GDD.md, MVP_SCOPE.md, ROADMAP.md}
docs/01-game-design/{GAME_RULES.md, CARD_RULES.md, COMBAT_RULES.md,
                     BOSS_RULES.md, RELIC_RULES.md, PET_RULES.md}
docs/02-technical/{ARCHITECTURE.md, API_CONTRACTS.md, SIGNALR_PROTOCOL.md,
                   GAME_STATE.md, GAME_EVENTS.md, DATABASE.md}
docs/03-decisions/ADR/{ADR-001, ADR-011, ADR-012, ADR-020, ADR-022}
```

---

## 9. Explicitly Out of Scope for This Audit

- No production, test, migration, contract, ADR or `docs/` file was modified.
- No discovered issue was fixed. No temporary instrumentation was added.
- TASK-207, TASK-208, TASK-208A and TASK-210 were **not** edited; where this
  audit disagrees with a recorded claim (TASK-210's classification of the Relic
  gap; TASK-207's U1/U3/U10 wording), the disagreement is reported in §2 and §3.
- The missing TASK-209 record was **not** reconstructed (§3 NG-23).
- The pre-existing uncommitted working-tree changes were **left untouched** and
  are itemised in §2.5. Nothing unrelated to TASK-208A/209/210 was found.
- No roadmap file was silently updated. Everything in §6 is a proposal in this
  record, not an edit to `ROADMAP.md` or to any task in `tasks/`.
- No new product requirement was invented: every gap above traces either to a
  `docs/` statement, to a delivered-but-unused value, or to a documented-but-
  unimplemented behaviour. Every decision that does not exist is reported as a
  decision (§7.3), not designed here.

---

## 10. Final Report

```text
TASK-211 AUDIT COMPLETE

Decision: MODIFY

  The proposed scope's two screens are right and both halves are still real and
  untouched by TASK-209/TASK-210. Three corrections are required: the Lobby's
  defect is a total navigation dead end (not only a failure-path dead end); the
  start-failure defect is a discarded §6 error envelope in the shared transport
  (not a Lobby-local string); and the Lobby's missing async run guard must land
  with the new exit, because the exit is what makes it reachable. The Result half
  additionally carries the delivered-but-discarded `durationTurns` and the
  ADR-011-contradicting "Final Player HP" label.

Recommended next task: TASK-211
  Non-Battle Screen Recovery & Readability
  (Lobby escape/retry/error legibility + Result reward states)
  P1 · S–M · no contract impact · presentation only

Roadmap changes
  KEEP NEXT     TASK-211 (scope MODIFY)
  NEW #2        TASK-218 — name the Pet's Relic triggers in battle from the
                existing `GET /api/relics` read (S, no contract change).
                TASK-210 misclassified this as a contract gap.
  KEEP #3       TASK-212 — Card cost/effect + Relic effect at the decision point
  NEW #4        TASK-219 — Pet Signature Skill read source (decision, then the
                4th cast control's label). Most visible of TASK-210's four gaps.
  KEEP #5       TASK-216 — verify the Player-XP persistence ordering risk
  RE-RANKED     TASK-213 / TASK-214 / TASK-215 (decision-gated),
                TASK-217 (documentation, now also owns the crit/`otherModifiers`
                staleness), plus two unassigned session-lifecycle items and the
                NG-11 one-line BUG.
  DROPPED       nothing.

Contract gaps (separated, not mixed)
  IMPLEMENTATION-ONLY   RelicTriggered naming (NG-15); the discarded §6 error
                        envelope (NG-03); the Lobby run guard (NG-04); the Result
                        states/duration/label/payload guard (NG-07…NG-10)
  DOCUMENTATION-ONLY    crit / `otherModifiers` "pass-through 1.00" staleness
                        (NG-17, with a COMBAT_RULES vs SIGNALR_PROTOCOL conflict
                        reported per AGENTS.md §4); ARCHITECTURE "no gameplay HUD";
                        TASK-210's gap misclassifications (record immutable)
  DECISION-REQUIRED     Signature Skill read source (NG-14); card cost/effect
                        read (NG-18); heal event (NG-16); content reachability
                        (NG-19); progression read (NG-20); Tier/Star scope
                        (NG-21); logout/session lifecycle (NG-12). The crit
                        indicator is a documented refusal and should NOT be
                        reopened on this evidence.

Verification
  git diff --check                    PASS
  npx tsc --noEmit                    PASS
  npx vitest run                      783 tests / 20 files PASS
  dotnet test (all four projects)     2919 tests PASS (326 / 1558 / 623 / 412)
  Browser E2E                         NOT RE-RUN by this audit (needs live
                                      backend + PostgreSQL + Redis + Chromium);
                                      inventory inspected statically, TASK-210's
                                      recorded result noted

Repository hygiene
  20 modified tracked files + 4 untracked task records are pre-existing and all
  attributable to TASK-208A / TASK-209 / TASK-210. Nothing unrelated found.
  Left untouched. NG-23: the TASK-209 record is missing from `tasks/`.

Implementation: NOT DONE
Production changes: NONE
Test changes: NONE
```
