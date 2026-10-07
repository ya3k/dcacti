# TASK-221 — MVP Content Ownership & Relic Loadout

```text
Task ID:            TASK-221
Type:               IMPLEMENTATION (backend ownership bootstrap, client Lobby
                    presentation, tests, one technical document)
Status:             DONE
Risk:               LOW-MEDIUM (one composition constant set widened; one Lobby
                    column gains a bounded pager; no schema, migration, endpoint,
                    protocol, SignalR, Redis, or game-rule change)
Priority:           P1 (makes the documented Pet / Relic loadout steps real)
Depends on:         TASK-213 (the approved ownership decision, implemented here);
                    TASK-223 (worktree ownership — its F-4 constraints honoured)
Primary Agent:      implementation
Evidence base:      current working tree (src/, tests/, docs/, scripts/) + a live
                    real stack (PostgreSQL, Redis, GameServer.Api, Vite, headless
                    Edge) driven end to end
Model:              DeepSeek Harness agent
```

**Scope discipline.** The implementation is exactly TASK-213's decision: the
existing Player-creation bootstrap now grants the whole provisioned MVP content
set, and the existing Lobby Relic column can present it. No table, column,
migration, endpoint, request/response member, SignalR method, event, Redis shape,
game rule, currency, drop, RNG, acquisition service, or progression service was
added, changed, or designed. Nothing was committed, staged, stashed, reset,
cleaned, or checked out (`TASK-223` §10 F-4); `HEAD` is unchanged at `afe5b14`.

---

## 1. Scope

```text
In scope (implemented)
  A. The Player-creation starter grant composition: 5 Pets / 3 Basic Cards /
     10 Relics, replacing 1 / 3 / 3 — using only the existing ownership model
     (Pet / PlayerUnlockedCard / Relic rows) and existing provisioned
     definitions.
  B. The Relic column's presentation capacity in the Lobby, so all ten owned
     Relics can be inspected and selected (TASK-213 §3.2 C-2).
  C. The tests and fixtures that pinned the old composition, plus new tests for
     the ownership set, the relic loadout decision, and the column's capacity.
  D. DATABASE.md §2 (the technical document that owns the composition), because
     the implementation would otherwise contradict it (AGENTS.md §17).

Out of scope (not implemented, not designed, not scaffolded)
  post-creation acquisition; progression / economy; currencies; random rewards;
  Card cost / affordability / CardCostModifiers; Signature Skill mechanics or
  ownership; Pet Passive effects; Tier / Star progression; Boss status effects;
  RelicTriggered presentation; TASK-217 documentation reconciliation;
  TASK-219A protocol correction; TASK-224 commit/provenance work; repository
  cleanup; Git commit; branch creation; any migration.
```

**One deliberate decision, recorded rather than silently taken.** The task
statement describes the Relic loadout as "exactly 3", while `RELIC_RULES.md`
§2.1 item 1 — a specific domain rule, which outranks a task (`AGENTS.md` §2) —
fixes `relicLoadout` at **3–5**, and both the server validator
(`RelicLoadoutService.Min/MaxLoadoutSize`) and `API_CONTRACTS.md` §3 implement
3–5. TASK-213 §3 recorded "any 3–5 of 10 owned" for the same reason. This
conflict was raised with the Product Owner before implementation; the recorded
answer is to **keep 3–5 everywhere** (server validator and Lobby bound
unchanged) and make all ten owned Relics reachable, so selecting exactly three —
the smallest documented legal loadout, C(10,3) = 120 combinations — is fully
supported and is what every new test exercises. No game rule was changed, and
`RelicLoadoutService` is untouched.

---

## 2. Ownership Bootstrap Changes

```text
existing account creation
        ↓
existing ownership bootstrap   (unchanged path, unchanged tables)
        ↓
full provisioned MVP ownership set   (changed composition)
```

**`PlayerStarterGrantFactory`** (`src/backend/GameServer.Application/Players/`)

```text
BEFORE                                  AFTER
StarterPetDefinitionId  "pet-xich-lang" StarterPetDefinitionIds   5 ids
StarterCardDefinitionIds  3 ids         StarterCardDefinitionIds  3 ids (unchanged)
StarterRelicDefinitionIds 3 ids         StarterRelicDefinitionIds 10 ids
CreateAsync → 1 Pet / 3 cards / 3 relics → 5 / 3 / 10 instances
7 ownership rows                       18 ownership rows
```

The identity lists are the provisioned content set, transcribed in
`PET_RULES.md` §8 / `RELIC_RULES.md` §6 document order:

```text
pets   pet-xich-lang, pet-bach-ho, pet-huyen-quy, pet-thanh-xa, pet-son-hung
relics relic-berserker-core, relic-mana-crystal, relic-assassin-eye,
       relic-emergency-core, relic-burning-curse, relic-combo-fang,
       relic-arcane-battery, relic-execution-mark, relic-cascade-core,
       relic-battle-instinct
```

- Every definition is still resolved **before** any ownership row is
  constructed, and a missing definition row still aborts the whole grant with
  the same `InvalidOperationException` (`AGENTS.md` §7). The three per-content
  resolution loops became one small private `ResolveAsync` helper, used three
  times — not a new abstraction, and it authors nothing.
- Every Pet instance carries the documented creation values
  (`Tier = Common`, `Star = 1`, `XP = 0`, `Level = 1`, server `AcquiredAt`) and
  its own minted `PetInstanceId`; every Relic instance carries its own minted
  `RelicInstanceId` and a server `AcquiredAt`. No instance is privileged.
- Nothing about *which* instance is the active Pet or an equipped Relic is
  decided here: ownership is not a loadout (`RELIC_RULES.md` §2.5).

**`PlayerStarterGrant`** — `StarterPet` (one `Pet`) became `StarterPets`
(`IReadOnlyList<Pet>`). That is the only shape change, and it is required by the
composition: five owned instances cannot be represented by one member. No
parallel or duplicate member was added.

**`PlayerRepository.StageStarterOwnership`** — the single-Pet stage became a loop
over the grant's Pets, beside the existing Card and Relic loops. The row counts
remain the composition's, never a literal in the repository, and the whole batch
still commits through the one `SaveChangesAsync` — or none of it does
(`DATABASE.md` §2 item 4). The lost-race discard, the existing-Player early
return, and the "no top-up" rule are untouched.

**Bosses** required no change: `MVP_SCOPE.md` §1 records them as having no
ownership relationship, and `BattleStartService` already resolves all five
through `BossDefinitions.All`. §6 below adds the test that proves all five are
reachable with a granted loadout.

---

## 3. Relic Loadout Changes

```text
Owned Relics:    10   (was 3)
Selected Relics: exactly 3 of the 10 — and 4 or 5, per RELIC_RULES.md §2.1
                       (3–5, unchanged server rule)
```

**Server side: no change at all.** `RelicLoadoutService` keeps
`MinLoadoutSize = 3` / `MaxLoadoutSize = 5`, the pairwise-distinctness rule, the
Player-scoped ownership read, and the request-order-as-slot-order rule. The
selection reaches the battle through the existing `BattleStartRequest.relicLoadout`
(`API_CONTRACTS.md` §3) and the existing `POST /api/battle/start` validation. No
new endpoint, member, hub method, event, or SignalR state was added. Ownership
comes from persistence (`IRelicRepository.ListOwnedInstancesAsync`, filtered by
`PlayerId`), never from a client claim — the server stays authoritative
(`GAME_RULES.md` §18, ADR-001).

**Client side: the selection model is unchanged.** `selectedRelicIds` still
holds the chosen owned instance ids in click order, `toggleRelic` still caps the
selection at the documented maximum and still orders it by selection, and
`buildStartRequest` still sends it unsorted and un-deduplicated. What changed is
only *which rows can be reached* (§4). Selecting exactly three of the ten is what
every new client test does.

**Because the whole set is owned, every legal combination is reachable** — no
currency, RNG, drop, unlock, or acquisition was introduced to obtain any Relic.

---

## 4. Lobby Presentation Changes

**The defect (TASK-213 §3.2 C-2, now fixed).** `LobbyScene` rendered each
collection column with `slice(0, MAX_LIST_ROWS)`, `MAX_LIST_ROWS = 8`. With ten
owned Relics the ninth and tenth were silently unreachable — they could not be
inspected or selected at all.

**Why the cap could not simply be raised.** The three columns, the Boss band, and
the bottom text band already occupy the 672 px safe area: eight rows at the 30 px
pitch end at `y = 348`, the Boss band begins at `y = 370`, and the bottom text
band starts at `y = 535` with the Boss band's fifth row ending at `y = 531`. Ten
rows (300 px) would run into the Boss band. There is no room for ten rows.

**The change (bounded pagination, the smallest mechanism the scene supports).**

```text
Relic column, 10 owned instances
  page 0  rows 1-8      PREV  NEXT   "1-8 of 10"
  page 1  rows 9-10     PREV  NEXT   "9-10 of 10"
```

- One new scene field, `relicPage` (0-based), plus `relicPageCount()` and
  `changeRelicPage(delta)` — no scroll container, no wheel handler, no viewport
  object, no second interaction system (`AGENTS.md` §9).
- `renderRelics()` slices the owned array by page instead of by the cap, and
  clamps the page against the current read so a shorter collection can never show
  an empty page.
- `drawRelicPager()` draws `PREV` / `NEXT` with the scene's existing
  `drawControl` convention (interactive rectangle + centred caption, re-created
  each render pass) **only when the column needs more than one page**, so a
  collection that fits renders exactly as it did before. A page indicator states
  `first-last of total` so the player can tell that more instances exist.
- The pager sits on the Relic column's own **header line**, in the free room to
  the right of the left-aligned header literal and above the first row. Measured
  in the real stack: `PREV` = 1072..1146 × 82..104, `NEXT` = 1156..1230 ×
  82..104 — inside the 24 px safe area, clear of the top-right `< BACK` / `RETRY`
  band (which ends at y = 73), clear of the first collection row (y = 108), and
  clear of the Boss band, the bottom text band, and START BATTLE.
- `relicPage` is reset in `create()` and in the teardown, like the rest of the
  scene's in-progress presentation state.
- Paging never touches the selection: a selected Relic stays selected when its
  page scrolls out of view and keeps its equip slot, and the review line and the
  submitted `relicLoadout` list it exactly as chosen
  (`RELIC_RULES.md` §2.3 — slot order is selection order, not display order).

**No other Lobby layout changed.** Pet rows, Card rows, Boss rows, the review
band, `< BACK`, `RETRY`, and START BATTLE keep their geometry; the pager is drawn
only for a column that needs it.

---

## 5. Boundary / Architecture Confirmation

```text
API  →  ApiService  →  GameRuntime  →  Scene      (unchanged)
```

```text
[✓] The Lobby still reaches the collection and the battle start only through the
    runtime port: it imports no `fetch`, no `@microsoft/signalr`, no `ApiService`,
    and no Discord SDK. The pager reads the collection the port already returned.
[✓] No HTTP call from `LobbyScene`; no direct database access; no direct SignalR
    access; no new SignalR state, event, or hub method.
[✓] No client-side ownership store and no duplicated ownership state: the scene
    holds the three response arrays it reads and no other copy of ownership, and
    the page is presentation state, not ownership state.
[✓] The client decides no ownership, count, category, copy-limit, distinctness,
    or battle legality. The pager decides none of them either.
[✓] The API contract did not need to change: the existing `GET /api/pets` /
    `/api/cards` / `/api/relics` reads already return the full owned set, so the
    approved model is representable without extending a single member.
[✓] No migration: the schema is untouched. The new ownership rows use the
    existing `Pet` / `PlayerUnlockedCard` / `Relic` tables and the existing FK
    and constraint set, and the existing provisioned definitions already cover
    every granted identity.
[✓] No new persistence mechanism, service, factory, manager, or abstraction on
    either side of the wire.
```

---

## 6. Tests

**Backend — added / updated**

```text
Application.Tests/PlayerStarterGrantFactoryTests.cs   (rewritten: 34 cases)
  composition is 5 Pets / 3 Basic Cards / 10 Relics (18 ownership rows)
  every provisioned MVP Pet and every provisioned MVP Relic is owned exactly once
  the three Basic Cards, and no PetSkill Card, are unlock rows
  nothing outside the provisioned content set is granted
  every Pet instance carries the documented creation values + server AcquiredAt
  10 distinct Relic instance ids; 5 distinct Pet instance ids in one grant
  instance ids are distinct across Players; definition ids are never used as
  instance ids; the composition is deterministic and addresses no Player
  a missing definition aborts the whole grant (all 5 + 3 + 10 identities)

Infrastructure.Tests/PlayerStarterOwnershipTests.cs   (updated, 1 added)
  a new Player receives 5 Pet / 3 Card / 10 Relic rows (19 rows, one commit)
  one owned instance per definition, and no duplicate definition
  an existing Player is not re-granted; an empty grant creates no ownership
  all ten Relic instances are distinct and server-timestamped

Infrastructure.Tests/PlayerStarterOwnershipPostgresTests.cs   (updated)
  against real PostgreSQL: 5 / 3 / 10 exact definition sets, 10 distinct Relic
  instance ids, every Pet's creation values, every starter FK resolves, the
  failed commit rolls back all 18 rows, concurrent first login produces exactly
  one set (18 rows), repeat authentication grants nothing

Infrastructure.Tests/TestStarterGrants.cs   (updated)
  the boundary fixture now stages the documented cardinality (5 / 3 / 10)

Api.Tests/AuthStarterOwnershipTests.cs   (updated, 3 added)
  registration creates 5 Pets / 3 Basic Cards / 10 Relics and every documented
  Pet and Relic identity; re-login grants nothing; the three collection reads
  return 5 / 3 / 10; register response shape unchanged
  NEW RegisteredAccount_CanStartABattleAgainstEveryCanonicalBoss — a brand-new
  account starts a battle against each of the five canonical Bosses using its
  granted Pet, its three Basic Cards, and exactly three of its ten Relics, and
  the submitted Relic order is what the battle carries

Api.Tests/TestProvisionedContent.cs   (updated)
  the in-memory host now seeds the provisioned content set (5 Pets, 8 Cards,
  10 Relics) the grant references
```

**Backend — pre-existing coverage that still passes unchanged**

```text
RelicLoadoutServiceTests   3 accepted / 4 accepted / 5 accepted;
                           0, 1, 2 and 6+ rejected as CountOutOfRange;
                           duplicate instance rejected; unowned rejected;
                           request order preserved as slot order;
                           MinLoadoutSize = 3, MaxLoadoutSize = 5
BattleStartEndpointTests   out-of-range Relic counts rejected; duplicate and
                           unowned Relic instances rejected; order preserved
```

No assertion was weakened. Every changed assertion was changed because the
approved ownership set it pinned is different, not because it failed.

**Client — added (11 new cases in `tests/LobbyScene.test.ts`)**

```text
the column shows one page of eight and offers the rest through its own controls
every one of the ten owned Relics is reachable by paging (union of pages = owned set)
paging stops at both ends (no wrap-around)
no paging control is drawn when the whole column fits one page
the selection survives a page change, in selection order, with correct slot numbers
exactly the three selected Relics of the ten are submitted (one from page 0,
  two from page 1), and the request still carries exactly four members
a ten-Relic loadout restored through PLAY AGAIN is presented, stays editable, and
  the edited selection is what the next battle submits
the page is discarded with the rest of the in-progress state on teardown
layout: both controls inside the safe area, clear of every row band, of each
  other, of the < BACK / RETRY band, and of the column's header literal; the
  controls are drawn where the exported layout says; every row and content line
  stays inside the list block on both pages
```

---

## 7. Real-Stack Verification

Environment: PostgreSQL (`localhost:5433`, the provisioned content set incl. all
ten Relics), Redis (`localhost:6379`), `GameServer.Api`
(`http://localhost:5000`, `--launch-profile http`), Vite dev server
(`http://localhost:5173`), headless Edge driven over CDP by the repository's own
harnesses.

**Live ownership probe (fresh account, real HTTP).**

```text
POST /api/auth/register  → 200, player_5bef9dc400d54a3cb6b4456fe306a8c8
GET  /api/pets     → 5   (five distinct petinst_* instances)
GET  /api/cards    → 3   (card-heal, card-power-charge, card-shield — Basic only)
GET  /api/relics   → 10  (Assassin Eye, Battle Instinct, Emergency Core,
                          Cascade Core, Mana Crystal, Berserker Core,
                          Burning Curse, Combo Fang, Execution Mark,
                          Arcane Battery — ten relicinst_* instances)
```

**The required flow (headless browser, `scripts/standalone-web-smoke.mjs`,
2 runs × 140 checks, both runs clean).**

```text
[✓]  1. create a fresh account (AuthScreen register + clear storage + re-login)
[✓]  2. confirm full ownership — phase4.starterGrantLoaded {pets:5, cards:3, relics:10}
[✓]  3. enter the Lobby (START BATTLE from the main menu)
[✓]  4. inspect all 10 Relics — phase4.everyOwnedRelicIsReachable walks the
        column's own PREV/NEXT controls: 2 pages (8 + 2), 10 distinct rows, each
        with its own delivered content line, and all 10 owned names covered
[✓]  5. select different 3-of-10 combinations — phase4.loadoutSelected selects
        three of the ten (each run draws a different three: the read order is not
        an ordered contract), and the pager survey covers both pages
[✓]  6. START BATTLE
[✓]  7. the battle starts with the selected Relics — the intercepted
        POST /api/battle/start body carries exactly the three chosen instances
[✓]  8. complete/reach Result — phase7 / phase8 reach ResultScene with the
        server's outcome and terminal HP
[✓]  9. PLAY AGAIN — phase8.playAgainReachedLobby
[✓] 10. preserved loadout — phase8.preservedLoadoutRestored restores the same
        petId / bossId / 3 Cards / 3 Relic instances
[✓] 11. change the Relic selection — phase8.preservedRelicSelectionIsEditable:
        one restored Relic is deselected and an owned Relic that was NOT in the
        restored set takes its place (paging to the row when it is on the other
        page), giving a 3-of-10 loadout that differs from Battle 1's, in the
        order chosen
[✓] 12. start another battle — phase8.battle2SceneReached / new battle identity
[✓] 13. the new selection is used — phase8.battle2RequestCarriesEditedLoadout:
        the intercepted Battle 2 request's `relicLoadout` is the edited three, in
        the chosen order, and is NOT the restored set; `bossId` carries the edited
        Boss and the request still has exactly four members
```

**Additional real-stack assertions.**

```text
[✓] zero uncaught exceptions            safety.zeroUncaughtExceptions = []
[✓] zero fatal console errors            safety.zeroFatalConsoleErrors = []
[✓] no unexpected API responses          safety.zeroUnexpectedApiResponses = [];
                                          only the three induced documented
                                          400 rejections occurred
[✓] no overlap / no safe-area overflow   phase3c.lobbyControlsFitTheFrameWithout
                                          Overlap (PREV/NEXT/BACK/START inside
                                          24..1256 × 24..696, no two overlapping);
                                          phase4.contentLinesStayInsideTheSafeArea
                                          (every content line inside the safe
                                          area, measured from Phaser's own bounds)
[✓] no regression to Card/Pet/Boss       phase4.fiveBossesRendered = 5;
    selection                             phase4.cardRowsStateWhatTheyChange = 3
                                          Basic Cards; the Pet read is 5 and one is
                                          selected; the four-member start request
                                          is unchanged
[✓] no new player-facing diagnostics     the pager renders "1-8 of 10"; the error
                                          line still renders only the server's own
                                          message (`phase4b.rejectedStartIsReadable`)
[✓] collection viewer (2 runs, 42 checks each, clean) — the viewer renders all ten
    owned Relics (its own row capacity is 14) and its Relics-tab count badge reads
    the delivered set
```

**Suite results.**

```text
Backend   Domain 1558/1558 · Application 640/640 · Infrastructure 414/414 ·
          Api 336/336   (0 failed; the 9 PlayerStarterOwnershipPostgresTests ran
          against real PostgreSQL — 2 s, not a skip-only path)
Frontend  vitest 868/868 (21 files) · npx tsc --noEmit 0 · npm run build 0
E2E       standalone-web 140/140 × 2 runs clean · collection viewer 42/42 × 2 runs
          clean
Git       git diff --check 0
```

**One harness race found, attributed, and fixed (reported, not hidden).**
`phase5c.oneFloaterPerDamageInstancePlusPower` failed in 2 of 3 smoke runs with
the diagnostic `[["+12","+12","-40","-40","-88","-88"]]` — exactly two injections'
worth of floaters alive at one sampled instant. Root cause is the harness's own
assumption, not the application: a floater lives 600 ms of **game** time
(`FLOATER_LIFE_MS`), so the fixed `delay(900)` between the screenshot pass and
the measurement pass cannot make the first batch's floaters expire when the frame
loop stalls (the fragility TASK-210 already recorded for a hidden document).
TASK-221 makes a battle's delivered feedback longer (ten ownable Relics, including
the `OnCascade` / `OnPowerGain` ones), which is what made it bite. The fix waits
on the scene's own feedback layer (`waitForFeedbackToFade`) instead of on the
clock, before and between the two injections; **the assertion, the injected
batch, and the scene are unchanged**, and both runs then reported
`[["+12","-40","-88"]]`. No test was weakened to pass.

---

## 8. Files Changed

```text
Production (4)
  src/backend/GameServer.Application/Players/PlayerStarterGrantFactory.cs
      5 starter Pets, 10 starter Relics; whole-grant definition verification
  src/backend/GameServer.Application/Players/PlayerStarterGrant.cs
      StarterPet → StarterPets (the composition's cardinality)
  src/backend/GameServer.Infrastructure/Postgres/Repositories/PlayerRepository.cs
      stage one Pet row per granted Pet instance
  src/frontend/client/src/game/scenes/LobbyScene.ts
      the Relic column's page: relicPage, relicPageCount, changeRelicPage,
      drawRelicPager, paged renderRelics, PREV/NEXT + indicator layout

Tests (8)
  tests/backend/GameServer.Application.Tests/PlayerStarterGrantFactoryTests.cs
  tests/backend/GameServer.Infrastructure.Tests/PlayerStarterOwnershipTests.cs
  tests/backend/GameServer.Infrastructure.Tests/PlayerStarterOwnershipPostgresTests.cs
  tests/backend/GameServer.Infrastructure.Tests/TestStarterGrants.cs
  tests/backend/GameServer.Api.Tests/AuthStarterOwnershipTests.cs
  tests/backend/GameServer.Api.Tests/TestProvisionedContent.cs
  src/frontend/client/tests/LobbyScene.test.ts
  src/frontend/client/scripts/standalone-web-smoke.mjs
      (starter-grant expectations, the 10-Relic pager survey, the per-page Relic
       content assertions, a real Relic-selection change across PLAY AGAIN, and
       the feedback-fade harness fix above)

Documentation (1)
  docs/02-technical/DATABASE.md   §2 items 1–4 + version preamble 1.36

Task record (1)
  tasks/completed/TASK-221-content-ownership-and-relic-loadout.md   (this file)
```

No other file was created, modified, or deleted. `git status` shows only these
plus the pre-existing uncommitted `TASK-207 → TASK-213` chain that was already in
the tree before this task (`TASK-223` §2 measured it as 38 modified tracked files
+ 15 untracked entries). Of this task's 13 modified files, ten were not in that
chain's modified set (the two `Players/` types, `PlayerRepository`,
`DATABASE.md`, and six test files) and three were already modified by
TASK-211/212A-1 (`LobbyScene.ts`, `standalone-web-smoke.mjs`,
`LobbyScene.test.ts`), so this task added to files the chain already owned. This
task added exactly one new file (this record) and touched nothing outside the
list above. The tree is therefore now 48 modified tracked files + 16 untracked
entries, all attributable: TASK-223 §2's inventory plus this task's §8 list plus
this record.

---

## 9. Explicit Non-Goals

```text
[✓] no post-creation acquisition, unlock, drop, purchase, claim, or reward path
[✓] no progression system, no economy, no currency, no random reward
[✓] no Card cost, no affordability display, no CardCostModifiers
[✓] no Signature Skill mechanics or ownership; no PetSkill Card became an unlock
    row (`CARD_RULES.md` §1 item 4 — asserted in both the factory tests and the
    E2E, where no Pet Skill Card content appears in the Card column)
[✓] no Pet Passive effect; no Tier or Star progression
[✓] no Boss status effect; no RelicTriggered presentation or callouts
[✓] no change to `/api/cards` membership, no Card selection beyond the three
    Basic Cards, no Card cost
[✓] no new table, column, index, constraint, migration, endpoint, request or
    response member, hub method, event, Redis shape, or SignalR state
[✓] no change to `RelicLoadoutService`, to the 3–5 rule, or to any game rule
[✓] no TASK-217 documentation reconciliation, no TASK-219A protocol correction,
    no TASK-224 commit/provenance work, no repository cleanup
[✓] no Git commit, no branch, no stage, no stash, no checkout, no reset, no clean
[✓] no fixture modified merely to make a test pass; no assertion weakened
```

---

## 10. Remaining Issues

```text
R-1  [PRE-EXISTING, reported not fixed] `src/frontend/client/scripts/
     battle-history-smoke.mjs` cannot leave the Lobby. Its START BATTLE lookup is
     `lobby.interactive.find(o => o.type === 'Rectangle' && o.enabled)`, which
     selects the FIRST interactive rectangle in the scene's own
     `interactiveObjects` — that is `< BACK` (TASK-211 added it before the START
     BATTLE trigger), so the click returns to the main menu and the run dies at
     "Timeout waiting for condition: BattleScene active after START BATTLE".
     ATTRIBUTED, not assumed: the same journey was run with TASK-221's pager
     temporarily disabled and it failed identically at the same step, so the
     defect predates this task and is independent of it. Its phase-4 ownership
     expectations (`ownedPetsCount > 0`, `ownedCardsCount >= 3`,
     `ownedRelicsCount >= 3`) are satisfied by 5 / 3 / 10 and needed no update.
     Suggested follow-up: a small E2E-harness task that addresses the START
     BATTLE control by its caption (the pattern `standalone-web-smoke.mjs`
     already uses) in `battle-history-smoke.mjs` and, while it is there,
     `boss-selection-smoke.mjs` (same lookup) and
     `lobby-start-overlay-smoke.mjs` (which scans the scene's own fields for an
     object list the Lobby does not expose, and is not wired into package.json).

R-2  [HARNESS, fixed here, residual] The feedback-fade fix in
     `standalone-web-smoke.mjs` removes the fixed-delay assumption from the one
     check that depended on it. The deeper fragility TASK-210 recorded — a
     hidden/stalled document suspending `requestAnimationFrame`, so Phaser
     advances no tweens while wall-clock time passes — is a property of the
     whole suite. A follow-up could gate every "wait for a transient to fade"
     step on the scene rather than on `delay()`.

R-3  [NOT A DEFECT, recorded] `GET /api/relics` (and `/api/pets`) define no order
     (`API_CONTRACTS.md` §5.5; `IRelicRepository` is explicitly not an ordered
     contract), so which three Relics a player picks first — and which Page 0 of
     the column shows — varies between accounts and runs. The E2E now selects
     from whatever the read returned (all ten are reachable) and asserts the
     order the player chose reaches the request, which is the only order the
     contract fixes (`RELIC_RULES.md` §2.3).

R-4  [SCOPE, reported] The task statement's "exactly 3" wording for the Relic
     loadout remains inconsistent with `RELIC_RULES.md` §2.1 item 1 (3–5) and
     with `MVP_SCOPE.md` §1 ("3–5 equipped Relics per battle"). This task keeps
     the documented 3–5 and supports exactly 3 within it (see §1). If the Product
     Owner intends the loadout to be exactly three, that is a Rule Change
     (`GAME_RULES.md` §20) with its own task: `RELIC_RULES.md` §2.1 item 1,
     `GAME_RULES.md` §13.3, `API_CONTRACTS.md` §3, `GAME_STATE.md` §2.3,
     `RelicLoadoutServiceTests`, and `BattleStartEndpointTests` would all move
     together.

R-5  [UNCHANGED BY THIS TASK] `relic-emergency-core` is equippable and its
     declared content ("CardCost 50% (Pet, Battle)") is now presented in the
     Lobby and confirmed on screen by the E2E, while the resulting card cost is
     still not shown anywhere. That is TASK-213 §7's recorded accepted MVP
     limitation, not an oversight of this task.
```

---

```text
TASK-221 COMPLETE

MVP ownership:
- Pets: 5
- Basic Cards: 3
- Relics: 10
- Bosses: 5

Relic loadout:
- Owned: 10
- Selectable: exactly 3 (within the documented 3–5 rule, which is unchanged)
- All 3-of-10 combinations reachable: YES

Card loadout unchanged: YES

Post-creation acquisition introduced: NO

Production changes: YES
Test changes: YES
Documentation changes: docs/02-technical/DATABASE.md (§2 items 1–4 + version
  preamble 1.36) — the document that owns the bootstrap composition.
  MVP_SCOPE.md is unchanged: TASK-213 already recorded the canonical decision.

Verification:
- Backend: PASS (Domain 1558, Application 640, Infrastructure 414, Api 336 — 0 failed)
- Frontend tests: PASS (868/868, 21 files)
- TypeScript: PASS (npx tsc --noEmit, exit 0)
- Build: PASS (npm run build, exit 0)
- Real-stack E2E: PASS (standalone-web 140/140 × 2 runs clean;
  collection viewer 42/42 × 2 runs clean; battle-history journey blocked by the
  pre-existing control-lookup defect R-1, proven independent of this task)
```
