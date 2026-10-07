# TASK-207 — Product Roadmap & Gameplay Gap Audit

```text
Task ID:            TASK-207
Type:               AUDIT (evidence-based product/gameplay gap classification;
                    no gameplay, UI, backend, or contract change)
Status:             DONE
Risk:               NONE (read-only audit; zero production file modified)
Priority:           HIGH (determines what should be built next)
Primary Agent:      review / product-audit
Evidence base:      current repository state (src/, tests/, docs/, tasks/)
Model:              Gemini 3.8
Reasoning:          High
```

**Scope discipline.** This task audits only. No production code, test, migration,
contract, ADR, or `docs/` file was modified. No temporary instrumentation was
added. The single file created is this record. Nothing in TASK-203–TASK-206 is
reported as missing; their outcomes are treated as shipped baseline.

**No implementation is performed here.** Recommended tasks below are proposals
with reserved IDs, not authorization.

---

## 0. Verification Performed (audit evidence base)

```text
npx tsc --noEmit                      PASS (clean)
npx vitest run  (client)              PASS — 751 tests / 20 files
git log / repository inspection       HEAD afe5b14
Screenshot artifacts re-inspected     src/frontend/client/smoke-shots/*,
                                      collection-viewer-shots/*, battle-history-shots/*
Source re-read for every headline gap BattleScene.ts, BattleEventPresenter.ts,
                                      GameRuntimeEvents.ts, GameRuntime.ts, LobbyScene.ts,
                                      ResultScene.ts, CollectionViewerScene.ts,
                                      BattleHistoryScene.ts, MainMenuScene.ts, App.tsx,
                                      StatusOverlay.tsx, ApiService.ts, CollectionModels.ts,
                                      BattleModels.ts, PlayerStarterGrantFactory.cs,
                                      BattleResultService.cs, BattleStateService.cs,
                                      PlayerRepository.cs, PetRepository.cs, Pet.cs, PetTier.cs,
                                      BattleEventBuilder.cs, BattleHub.cs
Docs re-read for every claim          GDD.md, MVP_SCOPE.md, ROADMAP.md, SIGNALR_PROTOCOL.md
                                      §4.2–§4.4, API_CONTRACTS.md §5.1–§5.4/§2.3,
                                      PET_RULES.md §5/§6, PASSIVE_RULES.md §8,
                                      DATABASE.md §1/§2, COMBAT_RULES.md §7
```

Every claimed "missing" item was checked against source before being reported.
"Server sends it but the client discards it", "nobody sends it", and "no rule
owns it" are three different findings below and are labelled differently.

---

## A. Executive Summary

1. **The game is functionally complete end-to-end but not yet player-presentable.**
   Every stage of the loop runs against the real server (auth → menu → lobby →
   battle → result → history/collection → replay), all 751 client tests and the
   browser E2E harnesses pass, and the flow survives real server-resolved
   battles. Maturity is **vertical-slice-complete, presentation-immature**.
2. **The single largest gap is that the battle screen is a developer diagnostic
   readout, not a game.** It is titled `BATTLE SCENE` and renders `SignalR:`,
   `Sync:`, `Conn:`, `BattleId:`, `RngSeed:`/`RngState:`, raw event lines
   (`Match: Straight ATK [10, 11, 12] depth: 0`,
   `DamageCalculated: base 137, combo 2.5x, elem 1.25x, other 1.1x, def 43.21, final 99`),
   and raw identifiers (`RelicTriggered: relicinst_…`, `BossSkillCast: flame-burst-mega by boss-hoa-long`)
   (`BattleScene.ts:266-386`, `:441-467`, `:1021`, `BattleEventPresenter.ts:496-540`).
   Visual evidence: `src/frontend/client/smoke-shots/run1-06-battle-initial.png`,
   `run1-07-battle-post-swap.png`.
3. **The player can never see their own Pet's HP or Power during a battle.** The
   realtime projection carries exactly five `petState` members
   (`passiveId`, `passiveProgress`, `passiveResetOverride?`, `equippedCards`,
   `statusEffects[]`) and two `bossState` members (`hp`, `maxHp`); `HP`, `MaxHP`,
   `ATK`, `DEF`, `Crit`, `Power` are explicitly **not delivered**
   (`SIGNALR_PROTOCOL.md:1679-1693`, `:1617-1622`; `GameRuntimeEvents.ts:155-193`;
   `BattleHub.cs:195-202`). The battle-start response *does* contain them
   (`BattleModels.ts:321-380`) and the runtime deliberately discards it
   (`GameRuntime.ts:562-566`). Healing emits **no event at all**
   (`ResourceGenerator.cs:400-404`), so current HP is not even derivable.
4. **This is a contract-blocked gap, not a UI bug.** Closing it requires a
   documented projection change (the `TASK-160` D-1A precedent widened `petState`
   for `statusEffects[]`; the same mechanism is required here), so it is an
   `AGENTS.md` §4/§18 stop-and-decide item for the Product Owner — **not**
   something an implementation task may invent.
5. **GDD §17's comprehension promise ("what each Card/Relic changes… why the
   Boss is dangerous") is currently unachievable from the shipped contracts.**
   `GET /api/cards` exposes only `cardId`/`name`/`category` and deliberately
   excludes `powerCost` and `effectDefinition`; `GET /api/relics` exposes only
   `relicId`/`name` (`API_CONTRACTS.md:783-786`, `:800-811`;
   `CollectionModels.ts:110-154`). The Lobby therefore asks the player to equip
   3 Relics it cannot describe (`LobbyScene.ts:999-1002`), and the in-battle cast
   tiles show no cost or affordability (`BattleScene.ts:503-558`).
6. **The loadout screen presents four decisions, of which only one is real.** A
   player permanently owns exactly 1 Pet, all 3 Basic Cards, and exactly 3 Relics
   (`PlayerStarterGrantFactory.cs:56,69-74,90-95`), and there is **no acquisition
   path anywhere** (`DATABASE.md:1287-1289`, `:1320-1327`: starter rows are
   "MVP bootstrap / test content"). So Pet, Cards, and Relics are all forced
   (3 owned relics vs. a minimum of 3, `LobbyScene.ts:98,727`); only **Boss
   choice** (5 options) is a genuine decision. GDD §1.3 pillar 2 "Build
   Diversity" is not exercisable.
7. **Progression is real, persisted, visible — and currently consequence-free.**
   Player XP (`+100` win / `+0` loss, Level 1–50, XP uncapped; `Player.cs:83,95,104,231`)
   and Pet XP (same amounts, hard cap 4900; `Pet.cs:113,124,141,300,348-352`) are
   granted exactly once and persisted to the owning row on the battle-end path
   (`BattleResultService.cs:511-531`, `PlayerRepository.cs:191-222`,
   `PetRepository.cs:105-134`). But TASK-200 decisions **Q-6** and **Q-7** fix
   Pet Tier/Star/Level as "COSMETIC FOR MVP" and Player XP pacing as
   "without combat stat scaling", and `PetState` stats are fixed constants
   (`DefaultMaxHP = 1000`, `DefaultATK = 50`). The loop
   `Battle → Reward → Progression → Stronger build → Battle again` therefore
   **breaks at "Progression → Stronger build"** — deliberately, for MVP.
8. **The largest product-loop gap is therefore the reward/replay incentive, not
   missing content.** Defeat grants exactly `+0 XP` on both tracks (observed in
   `battle-history-shots/run1-04-result1.png`: `Player: +0 XP · XP 0 · Level 1`),
   the only reward channel in the game is XP, and the only replay driver left is
   boss variety. After TASK-206 the account is now inspectable (Battle History),
   but it is a read-only ledger with no per-entry action
   (`BattleHistoryScene.ts:635-636`).
9. **Several fully-delivered values are simply never drawn** — the cheapest,
   highest-certainty wins in this audit: `playerState.combo`/`matchCount`
   (`GameRuntimeEvents.ts:282-287` → never rendered in `BattleScene.ts:441-467`;
   pinned absent by `SceneLifecycle.test.ts:1665-1678`), per-cell special-gem
   state (`RuntimeCell.specialGem` dropped at `BattleScene.ts:601-605`),
   `durationTurns` on the result screen (fetched then discarded,
   `ResultScene.ts:465-466`), and `RuntimeStatusEffect.targetStat`.
10. **Recommendation: finish the core battle loop before expanding features.**
    The project does **not** need new systems; it needs the existing loop made
    presentable and legible. Two true stop conditions were found and are reported
    in §J rather than resolved: (a) GDD §9/§11 pet-HP/Power visibility vs the
    frozen `SIGNALR_PROTOCOL.md` §4.3 projection, and (b) `MVP_SCOPE.md` §1's
    "Tier, Star, Level progression" and "5 Pets / ~10 Relics" vs. the
    implementation (no Tier/Star progression rule exists; 4 Pets and 7 Relics are
    unobtainable).

---

## B. Current Player Loop (real, traced in code)

```text
Auth (AuthScreen)                                  DONE
  └─ register / login → JWT application session, GameShell mounts
     App.tsx:119-132; AuthScreen.tsx; ApiService.ts:73-114
→ Main Menu (MainMenuScene)                        DONE
  └─ START BATTLE (640,324) · COLLECTION (640,394) · BATTLE HISTORY (640,464)
     MainMenuScene.ts:83-111
→ Lobby / Loadout (LobbyScene)                     PARTIAL
  ├─ 1 CHOOSE PET / 2 EQUIP CARDS / 3 EQUIP RELICS / 4 CHOOSE BOSS / 5 REVIEW
  │  LobbyScene.ts:838-869
  ├─ GAP: no < BACK and no RETRY — the only scene.start is BattleScene:678;
  │        a failed/empty collection read is a dead end (LobbyScene.ts:456,872)
  ├─ GAP: cards/relics/pets carry no cost, effect, stat, passive or skill text
  └─ GAP: 3 of 4 decisions are forced by starter ownership (see A6)
→ Battle start (POST /api/battle/start → connect → JoinBattle)   DONE
     GameRuntime.ts:598-613; BattleHub.cs:799
→ Battle / Match-3 / Combat (BattleScene)          GAP (presentation)
  ├─ board: 8×8, four gem types, tap-tap swap                       PARTIAL
  │    board drawn from authoritative cells; match highlight + -N floaters only
  │    BattleScene.ts:569-637, :1041-1122
  ├─ GAP: no HUD — no Pet HP, no Power, no combo counter, no turn indicator
  ├─ GAP: special gems not drawn on the board (BattleScene.ts:601-605)
  ├─ GAP: all 14 in-battle events render as a raw 13px monospace log
  └─ GAP: Pet HP absent from the wire entirely (A3)
→ Boss interaction                                 PARTIAL
  ├─ Boss HP as text `Boss HP: 2668 / 2800` (BattleScene.ts:459)
  └─ GAP: no boss identity/element/telegraph; BossSkillCast arrives post-hoc by id
→ Victory / Defeat                                 PARTIAL
  ├─ no in-battle outcome moment: the terminal batch is discarded
  │    (BattleScene.ts:985-990 returns before presentEventBatch)
  └─ ResultScene renders VICTORY/DEFEAT + Final HP (ResultScene.ts:401-429)
→ Result / Rewards (ResultScene)                   PARTIAL
  ├─ all 8 RewardSummary members rendered (RewardSummaryFormat.ts:36-40)
  ├─ GAP: silent empty reward area on read failure — no loading/error/retry
  │    ResultScene.ts:464-471; pinned by ResultScene.test.ts:815-827
  ├─ GAP: `durationTurns` delivered and discarded (ResultScene.ts:465-466)
  └─ GAP: `Final Player HP:` labels the *Pet's* HP (SIGNALR_PROTOCOL.md:1018)
→ Progression                                      PARTIAL
  ├─ Player XP/Level and Pet XP/Level persisted and returned — DONE
  └─ GAP: no combat/stat consequence (Q-6/Q-7); no level-up moment beyond a suffix
→ Collection (CollectionViewerScene)               DONE (as a viewer)
  └─ GAP: cannot grow (1 Pet / 3 Cards / 3 Relics forever); no cost/effect/stats
→ Battle History (BattleHistoryScene)              DONE (as a ledger)
  └─ GAP: purely informational; the only post-battle home of Player Level/XP
→ Replay                                           DONE
  ├─ PLAY AGAIN → Lobby with the preserved loadout, still editable
  │    ResultScene.ts:342-351; PreservedLoadout.ts; ADR-022 (TASK-203/205)
  ├─ MAIN MENU → MainMenuScene
  └─ GAP: no progression-driven reason to replay (A7/A8)
```

---

## C. Gameplay Gap Matrix

Scoring: Player impact / Gameplay importance / MVP relevance / Implementation
risk / Dependency-blocking, each 1–5. Priority: **P0** blocks the core playable
loop · **P1** major gameplay gap · **P2** important polish/progression gap ·
**P3** nice-to-have.

| ID | Area | Current State | Gap | Player | Game | MVP | Risk | Dep | Priority | Evidence |
|----|------|---------------|-----|-------:|-----:|----:|-----:|----:|----------|----------|
| G1 | Pet HP during battle | Nothing delivered; only a transient `-N` floater and `Final Player HP` at the end | **SPECIFIED BUT MISSING** — GDD §86/§243 require the Pet's HP to matter; the projection omits `HP`/`MaxHP` | 5 | 5 | 5 | 4 | 5 | **P0** | `SIGNALR_PROTOCOL.md:1679-1693`; `GameRuntimeEvents.ts:155-193`; `BattleHub.cs:195-202`; `GameRuntime.ts:562-566` |
| G2 | Power / resource | Transient `PowerChanged` log line only; no gauge, no cost, no affordability | **SPECIFIED BUT MISSING** — GDD §218-222's spend/save decision cannot be presented | 4 | 5 | 5 | 3 | 4 | **P1** | `BattleEventPresenter.ts:539`; `BattleScene.ts:441-467,503-558`; `API_CONTRACTS.md:783-786` |
| G3 | Battle presentation | Diagnostic console: `BATTLE SCENE`, `SignalR:`, `Sync:`, `Conn:`, `RngSeed:`, raw event log | **IMPLEMENTED BUT NOT PLAYER-FACING** — no HUD layer at all | 5 | 5 | 5 | 4 | 4 | **P0** | `BattleScene.ts:266-386,441-467,1021`; `smoke-shots/run1-07-battle-post-swap.png` |
| G4 | Special gems | Delivered per cell, deliberately not drawn | **IMPLEMENTED BUT NOT PLAYER-VISIBLE** — Match-4/5 and L/T rewards are invisible | 4 | 4 | 4 | 2 | 2 | **P1** | `BattleScene.ts:601-605`; `GameRuntimeEvents.ts:334-337` |
| G5 | Terminal resolution | Terminal batch dropped; no in-battle outcome moment | **PARTIALLY IMPLEMENTED** — killing blow + sibling events never presented | 4 | 3 | 4 | 2 | 2 | **P1** | `BattleScene.ts:980-990` |
| G6 | Combo / match count | Delivered in `playerState`, never rendered | **IMPLEMENTED BUT NOT PLAYER-VISIBLE** | 4 | 4 | 4 | 1 | 1 | **P1** | `GameRuntimeEvents.ts:282-287`; `BattleScene.ts:441-467`; `SceneLifecycle.test.ts:1665-1678` |
| G7 | Boss readability | `Boss HP: hp / maxHp` text; no name/element/state/telegraph | **PARTIALLY IMPLEMENTED** — `BossSkillCast` arrives after the hit, by raw id | 4 | 4 | 4 | 4 | 4 | **P1** | `BattleScene.ts:459`; `GameRuntimeEvents.ts:236-249`; `SIGNALR_PROTOCOL.md:1893` |
| G8 | Passive effect | Progress `(2 / 5 Matches, reset: Default)` is rendered | **IMPLEMENTED BUT NOT PLAYER-VISIBLE / SPECIFIED-BUT-MISSING EFFECT** — the trigger's effect is not reported by contract, and the **starter Pet's** passive (Xích Lang) is unimplemented for MVP | 4 | 4 | 4 | 3 | 3 | **P1** | `BattleScene.ts:1199-1205`; `SIGNALR_PROTOCOL.md:1694-1703`; `tasks/completed/TASK-200` Q-11 |
| G9 | Card legality feedback | Rejections surface as raw codes; the "accepted. Awaiting…" line never clears | **PARTIALLY IMPLEMENTED** — the only teacher of ADR-021 / cost rules is a rejection string | 3 | 3 | 4 | 2 | 2 | **P2** | `BattleScene.ts:814-861,930-961`; `CardCastRejectionReason.cs:58-61` |
| G10 | Damage explanation | One debug line with six factors | **PARTIALLY IMPLEMENTED** — no Advantage/Neutral/Disadvantage label, no crit marker, no relic/buff attribution | 4 | 3 | 4 | 2 | 2 | **P2** | `BattleEventPresenter.ts:521`; `SIGNALR_PROTOCOL.md:1072` |
| G11 | Damage floaters | `-N` drawn by event type, not by target | **PARTIALLY IMPLEMENTED** — every instance emits both `DamageDealt` and `DamageTaken`; boss→pet hits draw on the boss side | 3 | 3 | 3 | 1 | 1 | **P2** | `BattleScene.ts:1092-1122`; `BattleStateService.cs:1864-1865,2235-2236` |
| G12 | Relic feedback | `RelicTriggered: <instance id>`; no name lookup, no effect; condition-failed triggers emit nothing | **PARTIALLY IMPLEMENTED** | 3 | 4 | 4 | 3 | 3 | **P2** | `BattleEventPresenter.ts:445-463`; `BattleScene.ts:480-497` (only `getCards()` is loaded in battle) |
| G13 | Boss status effects | Burn/shield/rage exist server-side; `BossState.StatusEffects[]` not delivered | **IMPLEMENTED BUT NOT PLAYER-VISIBLE** | 3 | 3 | 3 | 3 | 3 | **P2** | `SIGNALR_PROTOCOL.md:1893`; `GameRuntimeEvents.ts:236-249`; `BattleStateService.cs:1429,1624,1745` |
| G14 | Network / desync | Stage labels + React overlay exist | **PARTIALLY IMPLEMENTED** — `lastError` and resync are invisible in battle; the mandated `serverSequence` gap check (§6 item 3) is not implemented anywhere | 3 | 3 | 4 | 3 | 3 | **P2** | `BattleScene.ts:392-416`; `GameRuntime.ts:976-986`; `GameRuntimeEvents.ts:67` (stale citation) |
| G15 | Match/cascade moment | Board redraws first, then a 400 ms white highlight per matched cell | **PARTIALLY IMPLEMENTED** — no pop/gravity animation; highlights land on the post-resolution board | 3 | 3 | 3 | 2 | 2 | **P3** | `BattleScene.ts:1047-1069`; `BattleHub.cs:1029,1039` |

---

## D. Product / UX Gap Matrix

| ID | Area | Current State | Gap | Player | Game | MVP | Risk | Dep | Priority | Evidence |
|----|------|---------------|-----|-------:|-----:|----:|-----:|----:|----------|----------|
| U1 | Result rewards | Reward block normally renders all 8 delivered members | **GAP**: on a failed result read the area is silently empty — no loading, no error, no retry; indistinguishable from a zero reward | 5 | 3 | 5 | 1 | 1 | **P1** | `ResultScene.ts:464-471`; `ResultScene.test.ts:815-827` |
| U2 | Lobby escape | Rows + START BATTLE only | **GAP**: no `< BACK`, no `RETRY`; a failed or empty collection read forces a page reload | 4 | 2 | 4 | 1 | 1 | **P1** | `LobbyScene.ts:456,678,872,939,963,989` (sole `scene.start` is :678) |
| U3 | Start-failure clarity | `Battle start failed: ${message}` where message is the raw code | **GAP**: the server's human `message` is discarded; all loadout reasons collapse to `INVALID_LOADOUT`, naming no slot | 4 | 3 | 4 | 2 | 2 | **P1** | `LobbyScene.ts:670,1086-1088`; `ApiService.ts:82-86,105-109`; `BattleController.cs:329-340` |
| U4 | Decision-point information | Pet: identity + element + `Lv N`. Cards: `name [category] slot n`. Relics: `name slot n`. Boss: name + element | **SPECIFIED BUT MISSING** — GDD §303-306 requires comprehension of "what each Card/Relic changes"; no cost/effect/stat/passive/skill data exists in the read contracts | 4 | 4 | 5 | 3 | 3 | **P1** | `LobbyScene.ts:949,973,999,1026`; `API_CONTRACTS.md:783-786,800-811,703-713` |
| U5 | Build diversity | 4 presented decisions, 1 real | **GAP (product loop)**: Pet/Cards/Relics are forced by starter ownership; no acquisition path exists | 5 | 4 | 5 | 4 | 5 | **P1** | `PlayerStarterGrantFactory.cs:56,69-74,90-95`; `LobbyScene.ts:98,97,727`; `DATABASE.md:1287-1289,1320-1327` |
| U6 | Account progression home | Player Level/XP appears only inside a battle's reward block or Battle History's newest entry | **GAP**: no account surface; no progression read endpoint exists (`/api/auth`, `/api/battle`, `api/pets|cards|relics` only); Battle History shows `—` for a fresh account | 4 | 3 | 4 | 3 | 3 | **P2** | `BattleHistoryScene.ts:418,519,530-544`; `BattleController.cs:117,210,292`; `CollectionController.cs:89,135,187,220` |
| U7 | Result completeness | Outcome + 2 HP values + rewards | **GAP**: `durationTurns` delivered and discarded; no Pet/Boss identity; the history screen shows Duration for the same battle | 3 | 2 | 3 | 1 | 1 | **P2** | `ResultScene.ts:465-466`; `BattleHistoryScene.ts:704` |
| U8 | Misleading strings | `Final Player HP:` prints the **Pet's** HP; elements shown as `Fire` in Collection but `Hỏa` in the Lobby; the review step prints raw instance/definition ids | **GAP** — contradicts ADR-011's "no Player HP pool" and GDD §3's Player/Pet split | 3 | 2 | 3 | 1 | 1 | **P2** | `ResultScene.ts:428` vs `SIGNALR_PROTOCOL.md:1018`; `CollectionViewerScene.ts:654` vs `LobbyScene.ts:1026`; `LobbyScene.ts:862-869` |
| U9 | Duplicate-username error | `AuthScreen` maps `USERNAME_ALREADY_TAKEN` | **GAP**: the backend returns `USERNAME_ALREADY_EXISTS`, so the intended Vietnamese message never shows | 3 | 1 | 3 | 1 | 1 | **P2** | `AuthScreen.tsx:43` vs `AuthController.cs:84` |
| U10 | Debug UI in the player surface | `StatusOverlay` ("Runtime Status / Web Standalone / Account / Backend / SignalR / Phaser / Runtime / Server Sync / Connection Id / Last Error") is rendered unconditionally in production | **GAP**: dev panel permanently over the game surface; its own CSS notes it sits above the Lobby's START BATTLE trigger, and the screenshot shows that button visually truncated | 4 | 2 | 4 | 2 | 2 | **P1** | `App.tsx:129-130` (only the viewport overlay is DEV-gated); `StatusOverlay.tsx:20-76`; `App.css:120-147`; `smoke-shots/run1-05-lobby-selected.png` |
| U11 | Session lifecycle | Login only; `session.clear()` only on a failed bootstrap connect | **GAP**: no logout; a mid-session `401` never resets the session, so every screen offers a RETRY that cannot succeed while the JWT stays in storage | 4 | 2 | 4 | 3 | 3 | **P2** | `App.tsx:37-53,119-125`; `API_CONTRACTS.md:304-311`; no `UNAUTHENTICATED` escalation outside `ApiService` |
| U12 | Empty/loading/error parity | Collection viewer and Battle History have loading + empty + error + RETRY; Lobby has loading + empty + error text only; Result and Preloader have none | **GAP**: state coverage is inconsistent across screens | 3 | 2 | 3 | 2 | 2 | **P2** | `CollectionViewerScene.ts:583,598,607-636,806-810`; `BattleHistoryScene.ts:54,558,566-568,573`; `ResultScene.ts:277-285,464-471`; `PreloaderScene.ts:77-84` |
| U13 | Silent list truncation | Lobby caps rows at 8, Collection at 14 | **GAP**: no "showing N of M" notice (Battle History does this correctly); latent at starter content | 2 | 2 | 3 | 1 | 1 | **P3** | `LobbyScene.ts:106,943,967,993`; `CollectionViewerScene.ts:61,650,664,677`; `BattleHistoryScene.ts:583-586` |
| U14 | Dead capability | `GET /api/pets/{petId}` implemented, documented (§5.2), exposed through the port — no production client caller | **GAP (dead surface)**: a client test even pins that it is not called | 1 | 1 | 2 | 1 | 1 | **P3** | `CollectionController.cs:135`; `ApiService.ts:173`; `GameRuntime.ts:787`; `GameRuntimeEvents.ts:480`; `CollectionViewerScene.test.ts:1135`, `LobbyScene.test.ts:435` |
| U15 | Undocumented server surface | `RuntimeStatusChanged` emitted by the hub; no client subscriber; absent from `SIGNALR_PROTOCOL.md`/`GAME_EVENTS.md`; `/health`, `/health/detail` and `ApiService.checkHealth()` unreachable | **GAP (contract hygiene)** | 1 | 1 | 2 | 2 | 2 | **P3** | `BattleHub.cs:758,772`; `Program.cs:133,136`; `ApiService.ts:59-68` |

---

## E. Progression Gap Analysis

**1. Is XP/level visible enough?**
Partially. Both tracks are rendered wherever a reward block exists
(`RewardSummaryFormat.ts:36-40`), in the shape
`Player: +<gained> XP · XP <newXp> · Level <newLevel>[ · LEVEL UP]`. Observed
real output on the Result screen and Battle History is the defeat case
`Player: +0 XP · XP 0 · Level 1` / `Pet: +0 XP · XP 0 · Level 1`
(`battle-history-shots/run1-04-result1.png`, `run1-07-history-two.png`). Gaps:
(a) there is **no account surface at all** — Player Level/XP is reachable only by
finishing a battle or by finding Battle History, which renders `—` for a fresh
account because no progression read endpoint exists (`BattleHistoryScene.ts:519`;
`BattleController.cs:117,210,292`); (b) there is **no XP-to-next-level / progress
bar** anywhere, and no "level" context (what Level 4 means); (c) Pet XP is
persisted (`Pet.cs:208`) but deliberately excluded from every response
(`API_CONTRACTS.md:734`), so between battles the player can see only Pet *Level*;
(d) the level-up event is a trailing `· LEVEL UP` suffix rather than a moment.
Verdict: **visible, but thin and unhomed.**

**2. Are rewards meaningful enough?**
Structurally thin. The only reward channel in the game is XP on two tracks; on
defeat both amounts are exactly `0` (`Player.cs:104`, `Pet.cs:124`), and the
observed real battles were defeats with `+0 XP` (`battle-history-shots/run1-04-result1.png`).
There is no currency, item, unlock, or content reward anywhere. Combined with
Q-6/Q-7 (no stat consequence), a reward is a number that changes a displayed
number. The reward *moment* is also fragile: a failed result read renders nothing
at all (`ResultScene.ts:464-471`). Verdict: **correct per rules, but not yet
motivating.**

**3. Does the player have a reason to replay?**
Weak but non-zero. `PLAY AGAIN` returns to the Lobby with the previous loadout
preserved and editable (`ResultScene.ts:342-351`, `PreservedLoadout.ts`, ADR-022
/ TASK-203/205 — working, verified). Remaining replay drivers: five bosses with
distinct passives/skills (real variety, `BossDefinitions.cs:10-16`), and the win/
loss outcome itself. Missing drivers: no progression effect, no unlock chase, no
higher difficulty, no score/rank, no per-battle statistics aggregate. Observed
evidence: the two real battles were both defeats on the same boss with the same
forced loadout. Verdict: **boss variety is the only replay reason today.**

**4. Does loadout meaningfully affect replay?**
**No — the loadout is effectively fixed.** 1 owned Pet (one battle = one Pet) so
there is no Pet choice; exactly 3 owned Basic Cards against an exact-3
requirement (`LobbyScene.ts:97,724`) so there is no Card choice; exactly 3 owned
Relics against a minimum of 3 (`LobbyScene.ts:98,727`;
`PlayerStarterGrantFactory.cs:90-95`) so all three must be equipped and the
"3–5 Relics" band collapses. Only the Boss is a decision
(`LobbyScene.ts:1026`). Because Pet stats are fixed constants
(`PetState.cs:740,757`) and Level is cosmetic (Q-6), nothing the player changes
alters their combat numbers. Verdict: **loadout does not meaningfully affect
replay; boss selection does.**

**5. Is Collection useful beyond viewing?**
No. It is a read-only three-tab viewer with a detail panel that prints wire
member names and raw ids (`Instance ID:`, `Card ID:`, `Relic ID:` —
`CollectionViewerScene.ts:750-782`), carries no equip action (equip state is
battle-scoped, `API_CONTRACTS.md:822-829`), and shows no stats, cost, effect,
passive, skill or XP. Because ownership can never grow
(`PlayerStarterGrantFactory.cs:56,90-95`; no acquisition path in the backend),
the collection is a static 1/3/3 display. Verdict: **informational only, and it
cannot grow.**

**6. Does Battle History contribute to retention, or is it merely informational?**
Merely informational in its current form, with one accidental structural
importance. Entries are plain `Text` with no per-entry action
(`BattleHistoryScene.ts:635-636`), no aggregate (no win rate, no total battles,
no best run), no filtering, no replay, and no Pet/Boss identity (the §4.5
contract exposes neither; `API_CONTRACTS.md:660-664`). Its one real function —
being the **only** place account Level/XP survives leaving the Result screen —
is a side effect of rendering `history[0].rewards`, not a designed account
surface. Verdict: **a ledger, not a retention mechanic.**

**Loop verdict.** `Battle → Reward → Progression` is implemented and persisted;
`Progression → Collection/Loadout → Stronger/Different build → Battle again` is
where the loop breaks, twice over: progression has no mechanical consequence by
Product Owner decision (Q-6/Q-7), and the loadout cannot vary because ownership
cannot grow. The break is documented intent, not a defect — but it means the MVP
currently offers a single-repeatable battle experience, not a progression loop.

---

## F. Battle Feedback Audit

> **Can a new player understand what is happening during a battle without
> reading developer documentation?**
>
> **No.** The battle scene is a diagnostic console. A new player sees a title
> reading `BATTLE SCENE` (`BattleScene.ts:278`), a connection readout
> (`SignalR: connected`, `Sync: …`, `Conn: …` — `:408-416`), and a monospace
> block containing `BattleId:`, `Turn: 1  Sequence: 1`,
> `RngSeed: 10310957908601430000  RngState: 1696911042865324052_af03`,
> `Passive: passive-xich-lang (2 / 5 Matches, reset: Default)`,
> `Boss HP: 2668 / 2800`, `Status Effects: none` (`:441-467`), plus a rolling
> 14-line log of raw event payloads (`:1021`). Identifiers are internal
> (`passive-xich-lang`, `relicinst_13dd…`, `card-inferno`, `flame-burst-mega`,
> `boss-hoa-long`). There is no HP bar, no Power gauge, no combo counter, no turn
> indicator, no boss identity, no card cooldown/affordability state, and no
> in-battle outcome moment. The player's own Pet HP is not on the wire at all.
> `GDD.md:303-306` ("The player should always understand …") is not met.

| Feedback category | Verdict | Evidence |
|---|---|---|
| Action feedback (swap/cast accepted) | Present, transport-phrased and never cleared (`Swap 4 -> 12: accepted. Awaiting the server's state push.`) | `BattleScene.ts:814-861,930-961` |
| Invalid action | Raw machine code only (`rejected (NO_MATCH_FROM_SWAP)`); no mapping, no slot naming | `:842,953`; `ApiService.ts:82-86` |
| Match feedback | 400 ms white highlight per matched cell, drawn **after** the board was already redrawn | `:1047-1069`; `BattleHub.cs:1029,1039` |
| Cascade feedback | One log line `Cascade: depth N` | `BattleEventPresenter.ts:517` |
| Combo feedback | One transient log line `Combo: N`; the authoritative `playerState.combo` is **never rendered** | `:519`; `GameRuntimeEvents.ts:282-287`; `SceneLifecycle.test.ts:1665-1678` |
| Damage numbers | Present (`-132`), but emitted twice per instance and positioned by event type rather than by target, so a boss hit paints the boss side | `BattleScene.ts:1092-1122`; `BattleStateService.cs:1864-1865,2235-2236` |
| Why damage happened | All six factors in one 13px debug line; no Advantage/Disadvantage label, no crit marker, no relic/buff attribution | `BattleEventPresenter.ts:521` |
| Healing | **Completely invisible** — healing emits no event by design and there is no HP state to move | `ResourceGenerator.cs:400-404`; `SIGNALR_PROTOCOL.md:1679-1693` |
| Shield / buff / debuff (Pet) | Text list including internal ids and raw magnitudes; `targetStat` unrendered | `BattleScene.ts:465,1223-1240` |
| Status effects (Boss) | Not delivered at all | `SIGNALR_PROTOCOL.md:1893` |
| Boss HP | Text pair only; no bar, no percentage | `BattleScene.ts:459` |
| Pet HP | **Not delivered and not rendered** | `GameRuntimeEvents.ts:155-193` |
| Power / resource | Transient `PowerChanged` line; no persistent value, no cost on the tile | `BattleEventPresenter.ts:539`; `BattleScene.ts:503-558` |
| Passive charge | Delivered and rendered as `(current / threshold Matches, reset: X)` | `BattleScene.ts:1199-1205` |
| Passive trigger effect | Not reported by contract; the starter Pet's passive effect is unimplemented | `SIGNALR_PROTOCOL.md:1694-1703`; TASK-200 Q-11 |
| Relic trigger | Opaque instance id; no name lookup in battle; condition-failed triggers emit nothing | `BattleEventPresenter.ts:445-463`; `BattleScene.ts:480-497` |
| Card cast / Pet Skill | Button + ack; no cost, no disabled state, no effect feedback (crit/burn/next-attack all underivable) | `:503-558`; `SIGNALR_PROTOCOL.md:1072,1457` |
| Boss skill | Post-hoc line by raw id; no telegraph (charge/cooldown/state not delivered) | `BattleEventPresenter.ts:531`; `SIGNALR_PROTOCOL.md:1893` |
| Turn / action budget | Bare `Turn: N Sequence: N`; no "your turn", no budget display; the one-cast-per-turn rule is learned only from a rejection | `BattleScene.ts:444`; `CardCastExecutor.cs:20,84` |
| Waiting / network / reconnecting | Stage labels present; `lastError`, resync outcomes and the mandated sequence-gap check are not surfaced in battle | `BattleScene.ts:392-416`; `GameRuntime.ts:976-986` |
| Victory / defeat in battle | **Absent**, and the terminal event batch (killing blow, final DamageCalculated, sibling events) is discarded | `BattleScene.ts:980-990` |
| Battle progression clarity | `BattleId`, `Turn`, `Boss HP: h / m` only; no boss name, no remaining-HP percentage, no match count | `:443-459` |

**Highest-value missing feedback (ranked).**
1. Pet HP (and therefore all incoming damage, healing and shield absorb) — G1.
2. Power as a persistent value plus card cost/affordability — G2.
3. A player-facing HUD replacing the diagnostic readout — G3.
4. An in-battle outcome moment, and not discarding the terminal batch — G5.
5. Special-gem state on the board — G4.
6. Combo/match-count rendering (data already delivered) — G6.
7. Boss identity + telegraph — G7.
8. Element matchup / crit attribution in the damage explanation — G10.

---

## G. Recommended Roadmap (next 10 tasks)

Reserved IDs start at TASK-208 (TASK-207 is this audit). Each entry is a
proposal, not an authorization. Effort: S ≤ 1 session · M = 2–4 · L = 5+.

```text
TASK-208 — Resolve the in-battle Pet-state presentation contract
Priority:            P0 (blocks G1/G2/G3/G7)
Player value:        None directly; it is the prerequisite for the player being
                     able to see their own Pet's HP and Power at all.
Dependencies:        None. Requires a Product Owner decision ledger
                     (TASK-160 D-1A / D-2A is the precedent).
Estimated complexity: S
Why now:             G1/G2/G3 are the top three gaps and all three are blocked by
                     the frozen SIGNALR_PROTOCOL.md §4.3/§4.4 projection. No
                     implementation task may widen a wire contract on its own
                     (AGENTS.md §4/§18), and the client may not compute HP
                     (AGENTS.md §10) — so this decision must come first.

TASK-209 — Implement the delivered Pet HP/Power/boss-identity projection + battle HUD
Priority:            P0
Player value:        The player can finally see their own health, their resource,
                     and how close the boss is to dying.
Dependencies:        TASK-208.
Estimated complexity: M–L
Why now:             It converts the single biggest player-facing gap; everything
                     in the battle screen's presentation is subordinate to it.

TASK-210 — Battle feedback pass over already-delivered data
Priority:            P1
Player value:        Match depth becomes visible: special gems drawn on the board,
                     a combo counter, an in-battle VICTORY/DEFEAT moment with the
                     killing blow, readable event messages instead of raw ids.
Dependencies:        None (no contract change). Can run in parallel with TASK-208.
Estimated complexity: M
Why now:             Highest value per unit of risk: every input already reaches
                     the client (G4/G5/G6/G12), so this is pure presentation with
                     no contract or authority question.

TASK-211 — Non-battle screen robustness & readability
Priority:            P1
Player value:        Removes the two dead ends and the misleading strings: a
                     Lobby < BACK and RETRY, human-readable start-failure
                     messages, a Result reward loading/error/retry state, the
                     delivered Duration, and the "Pet HP" label correction.
Dependencies:        None (no contract change).
Estimated complexity: S–M
Why now:             U1/U2/U3/U7/U8 are cheap, certain, and hit failure paths a
                     real player will meet on their first bad network moment.

TASK-212 — Expose Card cost/effect and Relic effect at the decision point
Priority:            P1
Player value:        The player can finally understand what they are equipping,
                     which GDD §303-306 requires and which no current contract
                     permits.
Dependencies:        Product Owner scope approval (it widens API_CONTRACTS §5.3/§5.4),
                     then client rendering in Lobby + Collection.
Estimated complexity: M
Why now:             It is the cheapest way to restore the meaning of the loadout
                     screen while the acquisition question (TASK-213) is decided.

TASK-213 — Content reachability decision (5 Pets / ~10 Relics vs starter-only ownership)
Priority:            P1 (product-loop blocking)
Player value:        Determines whether build diversity, the collection, and the
                     loadout ever become real.
Dependencies:        Product Owner decision. MVP_SCOPE.md §1 lists the content as
                     IN; DATABASE.md §2 item 2 declares the starter grant
                     bootstrap-only and defines no acquisition path.
Estimated complexity: S (decision + documentation; implementation would be separate)
Why now:             Until this is answered, the Lobby, Collection and Pet-skill
                     surfaces cannot do more than display a fixed starter set.

TASK-214 — Player-facing account progression surface
Priority:            P2
Player value:        Level/XP stops being a by-product of a battle reward; the
                     account has a home, and a fresh account no longer shows `—`.
Dependencies:        A progression read endpoint (contract addition) or a
                     documented decision that Battle History remains the home.
Estimated complexity: M
Why now:             E1/E6 show progression is visible only where a battle
                     happened; retention needs it to be visible everywhere.

TASK-215 — MVP scope reconciliation for Tier/Star progression
Priority:            P2
Player value:        Removes an MVP promise the game cannot currently keep.
Dependencies:        Product Owner decision (MVP_SCOPE §1 + ROADMAP claim
                     "Tier, Star, Level progression"; ADR-012/code have no
                     Tier/Star progression rule, and GDD §14 defines no owner).
Estimated complexity: S
Why now:             It is a genuine §4 conflict (see §J-2) that will otherwise
                     be re-discovered by the next content task.

TASK-216 — Verify and, if confirmed, fix the Player-XP persistence ordering risk
Priority:            P2
Player value:        Protects the only reward the game grants.
Dependencies:        None (a composed persistence test is missing today).
Estimated complexity: S
Why now:             Code reading shows Player progression can be skipped while
                     the persisted RewardSummary claims it landed (§J-3). It is
                     unproven and must be verified before it is called a defect.

TASK-217 — Documentation and cross-reference reconciliation (documentation only)
Priority:            P3
Player value:        Indirect: removes ambiguity that would otherwise be resolved
                     by guesswork in later tasks.
Dependencies:        None. Must stay separate from gameplay work (task constraint).
Estimated complexity: M
Why now:             It is real, but it must never outrank a gameplay deficiency —
                     hence last.
```

Scoring summary (Player / Gameplay / MVP / Risk / Dependency):

| Task | Player | Game | MVP | Risk | Dep | Priority |
|------|-------:|-----:|----:|-----:|----:|----------|
| TASK-208 | 3 | 5 | 5 | 2 | 5 | P0 |
| TASK-209 | 5 | 5 | 5 | 4 | 3 | P0 |
| TASK-210 | 4 | 4 | 4 | 2 | 1 | P1 |
| TASK-211 | 4 | 2 | 4 | 1 | 1 | P1 |
| TASK-212 | 4 | 4 | 5 | 3 | 2 | P1 |
| TASK-213 | 5 | 4 | 5 | 4 | 5 | P1 |
| TASK-214 | 4 | 3 | 4 | 3 | 3 | P2 |
| TASK-215 | 2 | 2 | 4 | 1 | 2 | P2 |
| TASK-216 | 3 | 2 | 3 | 2 | 2 | P2 |
| TASK-217 | 1 | 1 | 3 | 1 | 1 | P3 |

**Deliberately not recommended (out of MVP or not proven).** New systems of any
kind (map, environment, PvP, gacha, currency shops, battle pass); a battle-log
replay feature; animations beyond the missing minimum feedback; anything
requiring a new architecture boundary. `MVP_SCOPE.md` §2/§3 and the audit
constraints both forbid them.

---

## H. Recommended Next Task

```text
RECOMMENDED NEXT TASK:
TASK-208 — Resolve the in-battle Pet-state presentation contract
           (active Pet HP/MaxHP and Power; Boss identity for presentation)
```

**Why this one, after TASK-206.**

1. It gates the audit's three highest-ranked gaps. G1 (Pet HP invisible), G2
   (Power invisible) and G3 (diagnostic console instead of a HUD) all reduce to
   the same root cause: the frozen realtime projection carries five `petState`
   members and two `bossState` members and excludes `HP`, `MaxHP`, `ATK`, `DEF`,
   `Crit`, `Power` (`SIGNALR_PROTOCOL.md:1679-1693`, `:1617-1622`;
   `BattleHub.cs:195-202`). Until that projection is widened by a recorded
   Product Owner decision, no player-facing battle HUD can be built.
2. Every alternative "next task" is either blocked by it or strictly smaller.
   TASK-210 (delivered-data feedback) is genuinely independent and could run in
   parallel, but on its own it still leaves the player unable to see their own
   health — the one thing a boss battler cannot omit. TASK-211/212/213 are real
   but lower-impact or decision-gated.
3. It cannot be skipped or worked around without violating a rule. The client may
   not derive HP (`AGENTS.md` §10, ADR-001), healing emits no event
   (`ResourceGenerator.cs:400-404`), and the battle-start snapshot is deliberately
   discarded (`GameRuntime.ts:562-566`). So the only legitimate path is a
   documented projection change — exactly the shape of the `TASK-160` D-1A
   precedent that added `petState.statusEffects[]`.
4. It is the correct `AGENTS.md` behaviour at a conflict. `GDD.md:86` (the battle
   ends when the Pet or Boss reaches 0 HP), `GDD.md:218-222` (Power is the primary
   resource the player continuously spends or saves) and `GDD.md:303-306`
   (the player should always understand what is happening) describe information
   the frozen contract withholds. §4 says detect, explain, propose the smallest
   correction, then **stop** — which is precisely this task.
5. It is cheap, risk-free, and the audit has already assembled the evidence:
   complexity S, zero production code, and the affected documents are known
   (`SIGNALR_PROTOCOL.md` §4.3/§4.4, `GAME_STATE.md` §2.3/§2.4, `GAME_EVENTS.md`
   if events are touched, plus the client models `GameRuntimeEvents.ts` /
   `SignalRService.ts` and their tests in the follow-on task).

TASK-209 (HUD implementation) is the intended immediate successor, and TASK-210
may proceed in parallel because it needs no contract change.

---

## I. Classification Summary (docs vs implementation)

| Item | Classification | Evidence |
|---|---|---|
| 8×8 board, 4 gem types, Match 3/4/5, L/T, cascade, combo | IMPLEMENTED | `MATCH3_RULES.md`; `BattleEventBuilder.cs:98-147`; `BattleScene.ts:569-637` |
| 5 Elements, Tương Khắc, Advantage/Neutral/Disadvantage | IMPLEMENTED | `ELEMENT_RULES.md`; `ElementMatchups` usage in `BattleStateService.cs` |
| 5 Pets, 5 Bosses, 3 Basic Cards, 5 Pet Skill Cards, ~10 Relics (content) | IMPLEMENTED (content provisioned) | `BossDefinitions.cs:10-16`; migrations `20260929152651`, `20261004055006`, `20261004153916`, `20260926151112`, `20261004094100` |
| Same content as **ownable/playable** content | IMPLEMENTED BUT NOT PLAYER-VISIBLE (4 Pets, 7 Relics, 4 Skill Cards unreachable) | `PlayerStarterGrantFactory.cs:56,90-95`; `DATABASE.md:1287-1289,1320-1327` |
| Player XP / Level (1–50, +100 win, +0 loss, uncapped) | IMPLEMENTED (persisted, exactly-once) | `Player.cs:52,83,95,104,231`; `BattleResultService.cs:511-531`; `PlayerRepository.cs:191-222` |
| Pet XP / Level (4900 cap, 1–50, active Pet only) | IMPLEMENTED (persisted, capped) | `Pet.cs:113,124,141,300,348-352`; `PetRepository.cs:105-134` |
| Player XP reset bug on retry | NOT PRESENT — exactly-once guard verified | `BattleResultService.cs:314-333,491-531` |
| RewardSummary 8 members, null semantics | IMPLEMENTED and matches docs | `BattleResultService.cs:649-667`; `DATABASE.md:1169-1229`; `API_CONTRACTS.md:447-456`; `RewardSummaryFormat.ts:50-65` |
| Battle history endpoint + surface | IMPLEMENTED (TASK-163/164/206) | `BattleController.cs:292`; `BattleHistoryScene.ts`; `API_CONTRACTS.md §4.5` |
| Post-result replay loop (PLAY AGAIN/MAIN MENU, preserved loadout) | IMPLEMENTED (TASK-202/203/205) | `ResultScene.ts:342-368`; `PreservedLoadout.ts`; ADR-022 |
| Scene lifecycle / async safety | IMPLEMENTED (TASK-204/205) | `SceneLifecycle.test.ts` (132 tests) |
| Passive progress visibility ("7 / 10 Matches") | IMPLEMENTED | `BattleScene.ts:1199-1205`; `PASSIVE_RULES.md:193-195` |
| Power visibility / pet-HP visibility in battle | SPECIFIED BUT MISSING (contract-blocked) | `GDD.md:86,218-222` vs `SIGNALR_PROTOCOL.md:1679-1693` |
| Card/Relic effect comprehension | SPECIFIED BUT MISSING (no contract member exists) | `GDD.md:303-306`; `API_CONTRACTS.md:783-786,800-811` |
| Tier / Star progression | SPECIFIED BUT MISSING (and unowned by any rule) | `MVP_SCOPE.md:53`; `ROADMAP.md:51`; `PetTier.cs:20-22`; `Pet.cs:36-39`; `PET_RULES.md:129-134`; `GDD.md:276-285` |
| Pet Level → combat stat scaling | DECIDED DIFFERENTLY BY A TASK THAN BY THE DOMAIN RULE | `PET_RULES.md:329-330,344-356` vs `TASK-200` Q-6 vs `PetState.cs:740,757` (§J-2) |
| Match/Cascade/Combo/GemMatched events | IMPLEMENTED and emitted (contra an over-broad reading of §4.2 item 6) | `BattleEventBuilder.cs:111-145`; `SwapExecution.cs:475`; `BattleEventWireProjection.cs:735-738` |
| Special-gem cells on the board | IMPLEMENTED BUT NOT PLAYER-VISIBLE | `GameRuntimeEvents.ts:334-337`; `BattleScene.ts:601-605` |
| Combo / matchCount | IMPLEMENTED BUT NOT PLAYER-VISIBLE | `GameRuntimeEvents.ts:282-287`; `BattleScene.ts:441-467` |
| `durationTurns` on the result screen | IMPLEMENTED BUT NOT PLAYER-VISIBLE | `BattleModels.ts:484`; `ResultScene.ts:465-466` |
| Boss status effects | IMPLEMENTED SERVER-SIDE, NOT PLAYER-VISIBLE | `BattleStateService.cs:1429,1624,1745`; `SIGNALR_PROTOCOL.md:1893` |
| MVP_SCOPE §1 naming the shipped client surfaces | STALE DOCUMENTATION (5 surfaces are FUTURE by §4) | `MVP_SCOPE.md:24-89` names no surface; `MainMenuScene`, `LobbyScene`, `CollectionViewerScene`, `BattleHistoryScene`, `ResultScene` |
| GDD §2.1 "battle and collection entry points" | STALE DOCUMENTATION (there are three) | `GDD.md:103-104`; `MainMenuScene.ts:83-111` |
| GDD §2.1 citation of `MVP_SCOPE.md` §1 for the replay loop | STALE / BROKEN CITATION | `GDD.md:112-114` vs `MVP_SCOPE.md:24-89` |
| ROADMAP Phase 0 "current / IN PROGRESS" | STALE DOCUMENTATION | `ROADMAP.md:14-16` vs shipped Phase 2 + Phase 3 items |
| ROADMAP "Thanh Xà and Sơn Hùng rows remain to be provisioned" | STALE DOCUMENTATION | `ROADMAP.md:42-44` vs `PET_RULES.md:392-405`; migration `20261004055006` |
| ARCHITECTURE "No gameplay HUD exists yet" | STALE DOCUMENTATION | `ARCHITECTURE.md:593-594` vs `BattleScene.ts:441-467,503-558` |
| ARCHITECTURE/TDD Discord Activity, `services/discord/`, `DiscordService` | STALE DOCUMENTATION | `ARCHITECTURE.md:169,178,603,661`; `TDD.md:75,93,100,241`; no such directory; ADR-020 |
| ARCHITECTURE directory trees / "Major Components" names | STALE DOCUMENTATION | `ARCHITECTURE.md:64-89,117-138,641-665` |
| `DATABASE.md:1332` `POST /api/auth/discord` for player creation | STALE DOCUMENTATION | `AuthController.cs:20,50,119` (register/login only) |
| `API_CONTRACTS.md` "§2.8" session pointer (7 sites + ADR-015) | STALE CROSS-REFERENCE | §2.3 is the session section (`API_CONTRACTS.md:241`) |
| Dangling TASK-175 / TASK-184 | STALE / MISSING RECORDS | cited by `DATABASE.md:16,23,806`; `RELIC_RULES.md:4,764,1294`; no file under `tasks/` |
| `GET /api/pets/{petId}`, `/health`, `/health/detail`, `RuntimeStatusChanged` | IMPLEMENTED BUT UNUSED / UNDOCUMENTED (no rule requires them) | §D U14/U15 |
| Map/Terrain/Weather/PvP/Gacha/Guild/Trading | OUT OF MVP SCOPE (correctly absent) | `MVP_SCOPE.md:93-109` |

---

## J. Reported Conflicts and Stop Conditions (not resolved here)

Per `AGENTS.md` §4/§20 these are reported, not fixed. Each needs a human
decision before implementation.

**J-1 — Battle information visibility: GDD vs the frozen realtime contract.**
`GDD.md:86` (the battle ends when the Pet reaches 0 HP), `GDD.md:218-222` (Power
is the primary resource the player spends or saves) and `GDD.md:303-306` (the
player should always understand what is happening) vs `SIGNALR_PROTOCOL.md:1679-1693`
(`petState` carries five members; `HP`/`MaxHP`/`Power` are "not delivered") and
`:1893` (Boss state beyond `hp`/`maxHp` is not delivered). Owner that *should*
decide: Product Owner, on the protocol document, following the TASK-160 D-1A
precedent. Blocks: G1, G2, G3, G7. Proposed smallest correction: widen the
`petState` projection by `hp`/`maxHp`/`power` and the `bossState` projection by
the presentation-only members needed for identity — implementation in TASK-209
after approval.

**J-2 — Pet progression and stat composition: domain rule vs Product Owner decision.**
`PET_RULES.md:329-330` ("Level scales base stats (HP/ATK/DEF) via a stat curve")
and `PET_RULES.md:344-356` (`Final Stat = f(Base Stat[Tier], Level Curve[Level],
Star Bonus[Star])`, "all three axes contribute") vs TASK-200 **Q-6** ("Pet
Tier/Star/Level remain progression signals **without** combat stat scaling for
MVP") and the code (`PetState.DefaultMaxHP = 1000`, `DefaultATK = 50` — no curve
at all). Under the §2 precedence order a specific domain rule outranks a task
record, so the implemented behaviour and the decision ledger are in tension with
`PET_RULES.md`. Compounding it: `MVP_SCOPE.md:53` and `ROADMAP.md:51` promise
"Tier, Star, Level progression", while `PetTier.cs:20-22` and `Pet.cs:36-39`
state that no Tier/Star progression rule exists in MVP and `GDD.md:276-285` §14
defines no owner for one. Owner that *should* decide: Product Owner (either
narrow `MVP_SCOPE.md` §1/`ROADMAP.md` to "Level progression", or authorize the
curve and the Tier/Star axes). Affects: TASK-213/TASK-215.

**J-3 — Player-XP persistence is ordering-dependent (unverified by execution).**
On the battle-end path the Player row is fetched and mutated, then
`PlayerRepository.SaveProgressionAsync` evaluates
`entry.State == EntityState.Unchanged && _dbContext.Players.Local.Contains(player)`
(`PlayerRepository.cs:197,215-218`) and returns **without** calling
`SaveChangesAsync`. Today the grant is still flushed incidentally by the very
next call, `PetRepository.SaveProgressionAsync` → `SaveChangesAsync`
(`PetRepository.cs:131`, invoked from `BattleResultService.cs:529`) on the same
scoped context. If the Pet row is absent that call returns before its
`SaveChangesAsync` (`PetRepository.cs:123-126`), so the Player grant would be
dropped while the persisted `RewardSummary` reports it as applied
(`BattleResultService.cs:436-477`). Execution was **not** performed during this
audit (read-only mandate), and no test composes `BattleResultService` with real
EF repositories for this case, so this is reported as a **risk to verify**, not
as a confirmed defect. Affects: TASK-216.

**J-4 — Documentation-only drift that could mislead a future task.**
`ARCHITECTURE.md:593-594` asserts "No gameplay HUD exists yet";
`ARCHITECTURE.md:641-665` names types that do not exist;
`DATABASE.md:1332` names a retired endpoint for player creation;
`API_CONTRACTS.md` cites its own session section as "§2.8" at seven sites and
`ADR-015` does likewise; `TASK-175`/`TASK-184` are cited as completed work but
have no record under `tasks/`. None of these is a product defect; they are
reported because each could cause a future task to implement against a wrong
description. Affects: TASK-217.

---

## K. Explicitly Out of Scope for This Audit

* No gameplay, balance, UI, backend, contract, migration, or ADR change.
* No session-persistence reconciliation (kept separate as instructed); only the
  player-visible consequence (U11) is reported.
* No documentation cleanup was performed (reported only; TASK-217 proposes it).
* Nothing from TASK-203–TASK-206 was re-opened; no regression in that work was
  found (scene lifecycle, preserved loadout, and the history surface were all
  re-verified as working).
* No new product requirement was invented: every "missing" item above is traced
  either to a `docs/` statement or to a delivered-but-unrendered value. Where a
  gap needs a decision, it is reported as a decision, not designed here.
