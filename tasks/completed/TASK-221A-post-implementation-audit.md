# TASK-221A — Post-TASK-221 Content Ownership & Loadout Audit

```text
Task ID:            TASK-221A
Type:               AUDIT / VERIFICATION (no implementation, no production
                    change, no test change, no documentation change)
Status:             DONE (audit performed; nothing implemented)
Risk:               LOW (one new audit record authored; the working tree's
                    executable content is unchanged)
Priority:           HIGH (re-baselines the downstream implementation order now
                    that the reachable-content baseline has moved)
Depends on:         TASK-221 (the implementation audited), TASK-213 (the product
                    decision it implements), TASK-223 (worktree ownership)
Primary Agent:      review / verification
Evidence base:      current working tree (src/, tests/, docs/, scripts/, tasks/),
                    git state (read-only), targeted backend + frontend test runs
Model:              DeepSeek Harness agent
```

**Scope discipline.** This task inspected and recorded. It modified **no**
production file, **no** test file, **no** script, and **no** existing
documentation. It ran **no** `git add`, `git commit`, `git stage`, `git stash`,
`git checkout`, `git restore`, `git reset`, `git clean`, `git rm`, `git branch`,
`git tag`, `git merge`, `git rebase`, `git push`, `git cherry-pick`, or
`git am`. It created exactly one file — this record. `HEAD` is unchanged at
`afe5b14`. The Relic loadout rule is **3–5** before and after this record; this
record does **not** reinterpret the old TASK-221 task-statement wording, and it
changes no rule, contract, or behaviour.

---

## 1. Executive Decision

```text
Decision:  PASS

TASK-221 post-condition:  HOLDS

Relic rule:               3–5 (unchanged, consistently implemented)
All 10 Relics reachable:  YES

Ownership baseline:       5 Pets / 3 Basic Cards / 10 Relics / 5 Bosses
                          18 ownership rows (was 7), one atomic commit

No HIGH-severity finding. No implementation defect in the TASK-221 change.
The seven MEDIUM findings are all DOCUMENTATION / TEST-COVERAGE drift; none
of them makes the shipped behaviour wrong.
```

**Why PASS.** Every audit point in the task statement resolves in TASK-221's
favour, and each was re-derived from source or from an authoritative document
rather than from TASK-221's own record:

```text
1. Ownership consistency   The complete path Player creation → starter grant →
                           persisted ownership → collection/API → Lobby →
                           BattleStartRequest → Battle carries exactly
                           5 / 3 / 10 / 5, with no post-creation acquisition,
                           no duplicated ownership semantics, and no remaining
                           1-Pet / 3-Relic assumption in executable code.  §2
2. Relic 3–5 rule          Every layer agrees on 3–5: the domain rule, the API
                           contract, the server validator, the loadout request,
                           the Lobby, battle start, the tests, and the E2E.
                           No layer implements, asserts, or displays "exactly
                           3".                                                                               §3
3. Relic reachability      All ten owned Relics are genuinely reachable — both
                           pages, selection from either page, 3/4/5 counts,
                           deselection (including off-page), paging with live
                           selections, request ordering, and PLAY AGAIN
                           preservation. The pager introduces no hidden state
                           and no selection bug.                                                §4
4. Existing accounts       Starter grants are creation-only by construction;
                           pre-TASK-221 accounts remain at 1 / 3 / 3 and cannot
                           exercise the MVP content set. Classified as a
                           PRODUCT/TESTING consideration, not silently fixed. §5
5. API / contracts         Nothing widened; nothing contradicted. §6
6. Documentation           DATABASE.md §2 accurately describes 5 / 3 / 10 and 18
                           rows; MVP_SCOPE.md remains canonical; the TASK-221
                           completion record accurately reflects the
                           implementation (including its file/count
                           arithmetic). Six documentation drift items exist —
                           five of them pre-existing or in immutable task
                           records — and are reported, not rewritten. §7
```

**The one deliberate interpretive question, answered the same way TASK-221
answered it.** The audit task statement's **Risk Scan** item 2 ("hard-coded
exactly-3 assumptions") and the earlier TASK-221 task statement's "exactly 3"
wording are **not** the rule. `RELIC_RULES.md` §2.1 item 1 (`RELIC_RULES.md:195`,
"`relicLoadout` selects **3–5 Relics owned by the Player**") is the specific domain
rule and outranks a task (`AGENTS.md` §2). TASK-213 §3 recorded "any 3–5 of 10
owned Relics". The implementation, the validator (`MinLoadoutSize = 3`,
`MaxLoadoutSize = 5`), and `API_CONTRACTS.md` §3 all implement 3–5. This audit
therefore audits against **3–5** and treats every "exactly 3" occurrence as a
wording/coverage matter to classify — never as a rule to apply.

**Why not MODIFY/SPLIT/DEFER.** No finding requires a code change to make
TASK-221's own post-condition true; every MEDIUM finding is either (a) drift in a
document the next documentation task already owns, or (b) a test-coverage gap
that adds evidence rather than repairing behaviour. The overall baseline
(`5 / 3 / 10 / 5`, all ten Relics reachable, 3–5 preserved) **HOLDS**.

---

## 2. Ownership Consistency

### 2.1 The complete path, re-derived

```text
Player creation
  AuthController.cs:101 / :145
      composeStarterGrant: cancellation => _starterGrants.CreateAsync(
  ↓
Starter grant
  PlayerStarterGrantFactory.cs:184-281   CreateAsync(acquiredAt, ct)
      :190-194   ResolveAsync(StarterPetDefinitionIds)     → 5 PetDefinition rows
      :202-206   ResolveAsync(StarterCardDefinitionIds)    → 3 CardDefinition rows
      :208-212   ResolveAsync(StarterRelicDefinitionIds)   → 10 RelicDefinition rows
      :221-246   5 Pet instances (Tier Common, Star 1, XP 0, Level 1, server AcquiredAt)
      :248-261   3 PlayerUnlockedCard rows
      :263-278   10 Relic instances (own relicinst_* id, server AcquiredAt)
      :280       return new PlayerStarterGrant(starterPets, starterCards, starterRelics)
  ↓
Persisted ownership
  PlayerRepository.cs:57-64    new Player
  PlayerRepository.cs:66-67    compose + StageStarterOwnership(playerId, grant)
  PlayerRepository.cs:130-176  pet loop (:134-150) | card loop (:152-162) | relic loop (:164-176)
  PlayerRepository.cs:69-95    ONE SaveChangesAsync; catch discards the WHOLE
                               staged batch (:78, :89) — 18 rows or none
  ↓
Collection / API
  CollectionQueryService.cs:89-120   ListPetsAsync   (no cap, no page, no limit)
  CollectionQueryService.cs:196-236  ListCardsAsync  (unlocked set)
  CollectionQueryService.cs:262-334  ListRelicsAsync (owned set, two queries, no N+1)
  ↓
Lobby
  LobbyScene.ts:1416   renderPets    (5 rows, slice(0, 8))
  LobbyScene.ts:1440   renderCards   (3 rows)
  LobbyScene.ts:1460-1500 renderRelics (paged, slice(page*8, page*8+8))
  ↓
BattleStartRequest
  LobbyScene.ts:1113-1122 buildStartRequest → exactly 4 members
      petId, bossId, cardLoadout (3), relicLoadout (3–5)
  ↓
Battle
  BattleStartService.cs:505-521 ResolveBoss over BossDefinitions.All (5)
  RelicLoadoutService.cs:113-118 count bound 3–5; :125-186 distinctness/ownership/order
```

### 2.2 The six required confirmations

| # | Requirement | Verdict | Evidence |
|---|---|---|---|
| O-1 | 5 Pets actually owned by a fresh account | ✅ **PASS** | `PlayerStarterGrantFactory.cs:82-89` names all five `pet-xich-lang, pet-bach-ho, pet-huyen-quy, pet-thanh-xa, pet-son-hung`; `AuthStarterOwnershipTests.cs:42` `Assert.Equal(5, ...Pets.CountAsync(...))`; `:92` `Assert.Equal(5, pets.Count)`; `PlayerStarterOwnershipTests.cs:70`; `PlayerStarterOwnershipPostgresTests.cs:125` (real PostgreSQL) |
| O-2 | 3 Basic Cards owned | ✅ **PASS** | `PlayerStarterGrantFactory.cs:99-104` `card-heal, card-shield, card-power-charge`; `AuthStarterOwnershipTests.cs:43, :120-121, :251`; no `PetSkill` card becomes an unlock row (`:103-122`) |
| O-3 | 10 Relics owned | ✅ **PASS** | `PlayerStarterGrantFactory.cs:126-138` names all ten; `AuthStarterOwnershipTests.cs:44, :160` (10 distinct instance ids), `:254`; `PlayerStarterOwnershipPostgresTests.cs:125, :560` |
| O-4 | 5 Bosses remain selectable | ✅ **PASS** | Server `BossDefinitions.cs:338` `All` over `:115, :159, :204, :256, :299` (5 ids); client `LobbyScene.ts:80-86` `MVP_BOSSES` (5 entries); `AuthStarterOwnershipTests.cs:268` `RegisteredAccount_CanStartABattleAgainstEveryCanonicalBoss` starts a battle against each of the five |
| O-5 | No post-creation acquisition introduced | ✅ **PASS** | Exhaustive call-site search over `src/`: the only production ownership writers are `PlayerRepository.cs:148/160/174` (the creation bootstrap). `IPetRepository.AddAsync`, `IRelicRepository.AddAsync`, `ICardRepository.AddUnlockAsync`, and both `AddDefinitionAsync` have **0 production call sites**; `IBattleResultRepository.AddAsync` has one (`BattleResultService.cs:492`) and writes a battle result and **no** ownership. No unlock/purchase/drop/claim/reward-grant/equip endpoint or hub method exists |
| O-6 | No duplicated or bypassed ownership semantics | ✅ **PASS** | `PlayerStarterGrant` (`:65-91`) is the single carrier; the factory addresses **no** `PlayerId` (`:166-171` — creation-time-only is structural, there is no overload that could run for an existing Player); `PlayerRepository.StageStarterOwnership` re-reads nothing and invents no count (`:121-129` — "the counts are the composition's, never a literal here"); collection reads are Player-filtered at the query (`CollectionQueryService.cs:98-100, :271-273`) and read no request value |

### 2.3 The three risk items that named this section

| Risk (task statement) | Verdict | Evidence |
|---|---|---|
| "no stale 1-Pet/3-Relic assumptions remain in executable code" | ✅ **PASS** | Grep over `src/` for `1 Pet`, `one starter Pet`, `StarterPet` (singular), `single Pet`, `7 ownership rows`, `three starter`, `3 starter`, `minimum bootstrap`, `only three Relics`, `MAX = 3` returns **only documentation comments that describe the new 5/3/10 state** (`PlayerStarterGrantFactory.cs:27, :92`) plus one correct history note in a test (`PlayerStarterOwnershipTests.cs:319`). No executable assumption survived |
| "no hard-coded maximum visible Relics" (Lobby) | ✅ **PASS** | `LobbyScene.ts:119 MAX_LIST_ROWS = 8` is a *column grid capacity*, now consumed as a **page size** (`:905, :1479, :1526-1527`), not a truncation. `drawRelicPager` returns early when `pageCount <= 1` (`:1521-1523`), so it is drawn exactly when the column needs it |
| "no hard-coded exactly-3 assumptions" | ✅ **PASS** | See §3 |

### 2.4 Findings

**O-F1 — LOW — `DATABASE.md` "Selection basis" sentence vs the factory's own array-order comments.**
`DATABASE.md:1336-1339` says the grant "references the provisioned content set —
it is NOT derived from document ordering, alphabetical ordering, migration
ordering, or database ordering"; `PlayerStarterGrantFactory.cs:77-80` says "The
order is `PET_RULES.md` §8's own document order" and `:121-124` "The order is
`RELIC_RULES.md` §6's own table order".

- **Evidence:** the two statements are both true of different subjects — the
  *set* (which identities are granted) is not a slice of any ordering, while the
  *array literal's sequence* happens to follow document order. The reads are
  explicitly not ordered contracts (`API_CONTRACTS.md` §5.5;
  `PlayerStarterGrantFactory.cs:79-80, :122-124`).
- **Authoritative rule:** `API_CONTRACTS.md` §5.5 defines no order for the
  collection reads; `RELIC_RULES.md` §2.3 item 2 fixes equip-slot order as the
  *request* order, not any storage order.
- **Impact:** a reader could report a false contradiction, or "fix" the array
  literals to match `DATABASE.md`'s wording and thereby change nothing
  observable. No behaviour is wrong.
- **Priority / effort / owner:** P3 · XS (one clarifying clause) · TASK-217B
  (cross-reference/stale-claim sweep).

**O-F2 — INFORMATIONAL (no defect) — `relicPage` is clamped at read time, never normalized.**
`LobbyScene.ts:1476` and `:1525` clamp the page with `Math.min(this.relicPage, …)`
for rendering, but do not write the clamped value back to `this.relicPage`.

- **Evidence:** `relicPage` is initialized in `create()` (`:584`) and cleared in
  `shutdown()` (`:650`); `changeRelicPage` (`:920-933`) re-clamps its target
  against the *current* page count, so a stale-high page converges to a legal
  page on the next pager activation or re-render. No empty page can be shown.
- **Classification:** correct/intentional. Recorded so a future reader does not
  mistake the read-time clamp for a missing normalization.

---

## 3. Relic 3–5 Rule Audit

**The authoritative rule, quoted verbatim.**
`RELIC_RULES.md:195-198` (specific domain rule, `AGENTS.md` §2):

```text
1. `relicLoadout` selects **3–5 Relics owned by the Player**
   (`API_CONTRACTS.md` §3; `GAME_RULES.md` §13.3). The count bound is stated
   here; the duplicate-instance rule is §2.4, the slot-index source is §2.3,
   and the resulting behavior is §2.5.
```

### 3.1 Layer-by-layer agreement

| Layer | Site | Bound | Verdict |
|---|---|---|---|
| Domain rule | `RELIC_RULES.md:195` (§2.1 item 1); `:176-177` (§2 item 1); `:217`; `:411` (§8 validation order 1) | 3–5 | ✅ |
| Domain rule (cross-doc) | `GAME_RULES.md:271`; `PET_RULES.md:90`; `GDD.md:122, :231`; `MVP_SCOPE.md:81` | 3–5 | ✅ |
| Technical contract | `API_CONTRACTS.md:363` ("relicLoadout must be 3–5 Relics owned by the player"), `:384` ("1. count  3–5 elements"), `:376-396` (3-step validation, order preserved) | 3–5 | ✅ |
| Technical contract (state) | `GAME_STATE.md:1213` (`EquippedRelics[]` 3–5), `:1299`, `:1321` | 3–5 | ✅ |
| ADR | `ADR-012:79` | 3–5 | ✅ |
| **Server validator** | `RelicLoadoutService.cs:48` `MinLoadoutSize = 3`, `:55` `MaxLoadoutSize = 5`, `:113-118` `CountOutOfRange` | 3–5 | ✅ |
| Battle start | `BattleStartService.cs` consumes the validated ordered snapshot; `AuthStarterOwnershipTests.cs:340` asserts the submitted order is the equipped order | 3–5 | ✅ |
| **Lobby selection** | `LobbyScene.ts:99-100` `MIN_RELIC_LOADOUT_SIZE = 3` / `MAX_RELIC_LOADOUT_SIZE = 5`; `:885-889` add-cap; `:1132-1134` completeness floor; `:1259-1260` display `Relics: n/5 (min 3)` | 3–5 | ✅ |
| Player-facing text | `LobbyScene.ts:1259-1260`; no `"3 Relics"` / `"3 relics"` / `"exactly 3 Relics"` string exists anywhere in `src/frontend/client/src` (grep) | 3–5 | ✅ |
| Server tests | `RelicLoadoutServiceTests.cs:123-143` (4 and 5 **accepted**), `:145-163` (0, 1, 2, 6, 7 rejected as `CountOutOfRange`), `:407-408` (`MinLoadoutSize == 3`, `MaxLoadoutSize == 5`); `BattleStartServiceTests.cs:135-155` `[InlineData(3)][InlineData(4)][InlineData(5)]` | 3–5 | ✅ |
| E2E | `standalone-web-smoke.mjs:3307-3321` asserts a 3-Relic edited loadout | 3 (minimum) | ✅ but see R-F3 |

### 3.2 Explicit search for the incorrect assumptions the task statement named

```text
pattern searched across the repo   occurrences   classification
exactly 3 (Relic context)          0 in docs/, 0 in src/   —
3 relics required                  0             —
3/3 relics                         0             —
MAX = 3 (Relics)                   0             —
MAX_RELIC_LOADOUT_SIZE = 3         0 (it is = 5) —
```

**Every `exactly 3` in the repository is about Basic Cards, Match-3 line length,
the Boss row set, a worked damage example, or version-preamble history — never
about Relics.** Classification table for the material occurrences:

| Site | Verbatim | Classification |
|---|---|---|
| `CARD_RULES.md:76, :100`; `API_CONTRACTS.md:362, :399, :405`; `ADR-012:94`; `DATABASE.md:1323`; `MVP_SCOPE.md:122-125` | "exactly 3 Basic Cards" | **correct/intentional** — the Card loadout, a different slot |
| `GAME_STATE.md:1299` | "fixed at battle start (3–5 Relics, exactly 3 Basic + 1 Pet Skill Card)" | **correct/intentional** — states 3–5 on the same line |
| `DATABASE.md:183` ("Prior 1.19 … 3 Relics: Berserker Core"), `:194` ("Prior 1.18 … 1 Pet / 3 Cards / 3–5 Relics") | old-composition counts | **correct/intentional** — version-preamble history, not current state; the current-state counterparts were synchronized by TASK-221 (§7, D-1) |
| `TASK-221:136` | "Selected Relics: exactly 3 of the 10 — and 4 or 5, per RELIC_RULES.md §2.1" | **stale documentation** (self-qualified) — R-F1 |
| `TASK-221:575` | "Selectable: exactly 3 (within the documented 3–5 rule, which is unchanged)" | **stale documentation** — R-F1 |
| TASK-221 task statement's "exactly 3" (quoted at `TASK-221:56-58`) | — | **correct/intentional handling** — TASK-221 §1 (`:55-66`) and R-4 (`:545-553`) raised it, kept 3–5, and assigned a rule change as its own task if the Product Owner ever wants exactly three |
| `LobbyScene.test.ts:922` | `it('keeps the Relic selection within three to five distinct instances')` | **test fixture / coverage gap** — R-F2 |

### 3.3 Findings

**R-F1 — MEDIUM — the TASK-221 completion record still says "exactly 3" in its own summaries.**

- **Evidence:** `tasks/completed/TASK-221-content-ownership-and-relic-loadout.md:137`
  ("Selected Relics: exactly 3 of the 10 — and 4 or 5, per `RELIC_RULES.md` §2.1")
  and `:575` ("- Selectable: exactly 3 (within the documented 3–5 rule, which is
  unchanged)"). The record itself records the resolution at `:56-66` and `:545-553`.
- **Authoritative rule:** `RELIC_RULES.md:195` — 3–5.
- **Impact:** the closing status block (`:564-596`) is what a downstream reader or
  an automated summary is most likely to consume. Read alone, `:575` states that
  only three Relics are selectable, which would license "correcting"
  `RELIC_RULES.md` §2.1, `API_CONTRACTS.md` §3, `GAME_STATE.md` §2.3, and
  `RelicLoadoutService` **away from** 3–5 — the exact move the record's own R-4
  says requires a `GAME_RULES.md` §20 Rule Change.
- **Constraint on the fix:** `tasks/TASK_LIFECYCLE.md:218` — "**Completed tasks are
  immutable.** Do not edit a completed task's …". The correction channel is a new
  record, not an edit of TASK-221.
- **Priority / effort / owner:** P2 · S (one corrective record, or one line in the
  next documentation task's record) · documentation agent under a new small task
  (proposed **TASK-225**), or TASK-217B if the Product Owner accepts a
  superseding note rather than a record correction.

**R-F2 — MEDIUM — the Lobby's 4- and 5-Relic selections are never exercised by any test.**

- **Evidence:** the only test whose name claims the upper bound is
  `LobbyScene.test.ts:922-930`, "keeps the Relic selection within three to five
  distinct instances" — it clicks three Relics and asserts
  `'Relics: 3/5'` (`:929`) against the **default 3-Relic fixture**
  (`LobbyScene.test.ts:152-156 STARTER_RELICS`). Grep for `At most 5 Relics`,
  `Relics: 4/5`, `Relics: 5/5`, `Relics: 6/5` over `src/frontend/client` →
  **zero matches**, so `toggleRelic`'s cap branch (`LobbyScene.ts:885-889`) and
  the `MAX_RELIC_LOADOUT_SIZE` interaction (`:885`) are **uncovered at the UI
  layer**. The TASK-221 describe block that owns the pager fixture (`:2359-2404`,
  `GRANTED_RELICS` = 10) also only ever selects three (see `:2504-2537`,
  `:2539-2591`).
- **Authoritative rule:** `RELIC_RULES.md:195` — 3–5 is an inclusive range, so 4
  and 5 are legal loadouts, not edge cases.
- **Impact:** a regression that broke adding a 4th or 5th Relic (e.g. an
  off-by-one in the cap, or a page clamp that hid the 4th selection) would ship
  green. The property "3–5 is selectable" is asserted on the server
  (`RelicLoadoutServiceTests.cs:123-143`) but not on the surface the player uses.
- **Recommendation:** add 4- and 5-Relic cases to the existing TASK-221 describe
  block, including the cap refusal message and a 5-Relic request across both
  pages. Do **not** weaken the existing cases.
- **Priority / effort / owner:** P2 · S–M · testing agent, on the existing
  `LobbyScene.test.ts` (fixture `GRANTED_RELICS` already supplies ten).

**R-F3 — LOW — the E2E only ever submits a 3-Relic loadout.**

- **Evidence:** `standalone-web-smoke.mjs:1728-1751`, `selectLoadout` fills
  `relicRows.slice(0, 3 - lobby.selectedRelicIds.length)` and returns only when
  `selectedRelicIds.length === 3` (`:1746`); `:3307-3321` edits a 3-Relic set to
  another 3-Relic set. The 4- and 5-element sizes are never carried over HTTP.
- **Authoritative rule:** `API_CONTRACTS.md:363, :384`; `RELIC_RULES.md:195`.
- **Impact:** the wire path for 4- and 5-Relic loadouts is proven only by
  in-process server tests (`BattleStartServiceTests.cs:135-155`), not by a live
  browser→API→battle journey. Lower than R-F2 because the server is the
  authoritative validator and it is covered.
- **Priority / effort / owner:** P3 · S · testing agent / E2E harness owner.

**R-F4 — LOW — the Lobby's completeness check enforces only the lower bound.**

- **Evidence:** `LobbyScene.ts:1132-1134` returns `Choose at least 3 Relics.` when
  `selectedRelicIds.length < MIN_RELIC_LOADOUT_SIZE`; there is no corresponding
  `> MAX_RELIC_LOADOUT_SIZE` branch. `toggleRelic` (`:885-889`) is the only upper
  bound.
- **Impact:** **not reachable today** — the only path that can raise the selection
  is `toggleRelic`, which refuses the 6th; the restored PLAY AGAIN set
  (`applyPreservedLoadout`, `:808-823`) can only carry a previously valid ≤5
  request, and the carrier is a Phaser in-memory registry
  (`PreservedLoadout.ts:14, :48-52`) that cannot cross a session or be written
  from storage. Classification: **latent interaction-boundary observation, not a
  defect**; the server would reject `>5` with `CountOutOfRange` regardless
  (`RelicLoadoutService.cs:113-118`).
- **Priority / effort / owner:** P3 · XS (optional belt-and-braces guard) ·
  implementation agent, only if the Product Owner wants defence in depth.

**R-F5 — INFORMATIONAL — the client duplicates the 3/5 bounds.**

- **Evidence:** `LobbyScene.ts:99-100` repeats `3` and `5`, which
  `RelicLoadoutService.cs:48, :55` own server-side.
- **Classification:** **correct/intentional.** `LobbyScene.ts:88-97` documents
  these as *interaction* cardinalities that "shape the *interaction* — how many
  slots the player may fill. They are not a validity judgment". The client makes
  no legality decision (`ARCHITECTURE.md` §2.2.3 rule 5), and the server remains
  the only validator (`GAME_RULES.md` §18, ADR-001). No shared constant is
  warranted across the wire boundary (`AGENTS.md` §9).

---

## 4. Full Relic Reachability Audit

### 4.1 Mechanism, re-derived from source

```text
LobbyScene.ts
  :1476   const page = Math.min(this.relicPage, this.relicPageCount() - 1)
  :1478-1479  ownedRelics.slice(page * 8, page * 8 + 8)
  :905    relicPageCount() = max(1, ceil(ownedRelics.length / 8))
  :920-933 changeRelicPage(delta) — clamped, no wrap, clears only selectionMessage
  :1518-1546 drawRelicPager() — early return when pageCount <= 1 (:1521-1523);
             PREV/NEXT via drawControl; indicator `${first}-${last} of ${total}`
  :584    relicPage = 0  in create()
  :650    relicPage = 0  in shutdown()
```

### 4.2 The eleven required checks

| Check | Verdict | Evidence |
|---|---|---|
| First page | ✅ | `LobbyScene.test.ts:2415-2429` — 8 rows = `GRANTED_RELIC_NAMES.slice(0, 8)`, `NEXT`/`PREV` drawn, indicator `1-8 of 10` |
| Second page | ✅ | `:2431-2453` — `NEXT` → `9-10 of 10`, rows exactly `['Cascade Core', 'Battle Instinct']` |
| Selection from both pages | ✅ | `:2504-2537` — one from page 0 (`Berserker Core`), two from page 1 (`Cascade Core`, `Battle Instinct`) |
| Selection count 3 | ✅ | `:2504-2537`; E2E `standalone-web-smoke.mjs:3307-3321`; server `BattleStartServiceTests.cs:135-137` |
| Selection count 4 | ⚠️ **server only** | `RelicLoadoutServiceTests.cs:123-131`, `BattleStartServiceTests.cs:136` — **no UI/E2E coverage** → **R-F2 / R-F3** |
| Selection count 5 | ⚠️ **server only** | `RelicLoadoutServiceTests.cs:133-143`, `BattleStartServiceTests.cs:138` — **no UI/E2E coverage** → **R-F2 / R-F3** |
| Deselection | ✅ | `LobbyScene.test.ts:932-943` (toggle out → `Relics: 0/5`); off-page deselection `:2576` (`Cascade Core` deselected while on page 1) |
| Page navigation while selections exist | ✅ | `:2480-2502` — select on page 0, page to 1, select twice, page back; review still reads `relic-instance-1, relic-instance-10, relic-instance-9`; on-page row shows `slot 1` and off-page rows keep `slot 2` / `slot 3` |
| BattleStartRequest ordering/content | ✅ | `:2504-2537` asserts `relicLoadout` equals `['relic-instance-1','relic-instance-9','relic-instance-10']` **in selection order** and that the request has exactly the four documented members; `BattleStartServiceTests.cs` and `AuthStarterOwnershipTests.cs:340` assert the order survives to `EquippedRelics` |
| PLAY AGAIN preservation | ✅ | `:2539-2591` — a preserved loadout with two page-1 instances is restored, restated in the review, marked `●` after paging, edited (deselect off-page, select on-page), and the edited order is what the next request carries; E2E `standalone-web-smoke.mjs:3294-3321`, `:3377-3394` |
| Page discarded on teardown | ✅ | `:2593-2604` — `shutdownScene` → `relicPage === 0` |
| No wrap-around | ✅ | `:2455-2467` — `PREV` on page 0 is a no-op, `NEXT` twice on page 1 stays on page 1 |

### 4.3 Does the pager introduce hidden state or selection bugs?

**No.** Stated as the four properties that would have to fail, each checked:

```text
1. The page is not part of the selection.
   relicPage  (LobbyScene.ts:436) and selectedRelicIds (:421) are separate
   fields; changeRelicPage (:920-933) never touches the selection, and
   buildStartRequest (:1113-1122) reads only selectedRelicIds. The page is not a
   request member — asserted at LobbyScene.test.ts:2530-2536 (exactly four keys).

2. The page survives correctly across renders and re-reads.
   render() recreates dynamic objects each pass (:1240-1252) and drawRelicPager
   is called from the same pass (:1252). Two pages can legitimately reuse one row
   position; the E2E therefore asserts content-line uniqueness *per page*
   (standalone-web-smoke.mjs:1627-1637), which is the correct invariant.

3. The page cannot present an out-of-range or empty slice.
   :1476 and :1525 both clamp with Math.min against the current read; empty
   collections short-circuit at :1466-1469 before any slice. No ordering is
   assumed: the page is a slice of whatever order the read returned, and
   API_CONTRACTS.md §5.5 fixes no order.

4. Paging cannot change what is submitted.
   A selected Relic that scrolls off-page keeps its index in selectedRelicIds and
   therefore its equip slot — asserted at LobbyScene.test.ts:2489-2501. Nothing
   in toggleRelic, requestStart, or buildStartRequest is keyed on the page.
```

### 4.4 Findings

**P-F1 — LOW (latent UI limitation, pre-existing, not caused by TASK-221) — the Collection viewer silently truncates at 14 rows with no notice.**

- **Evidence:** `CollectionViewerScene.ts:62 const MAX_LIST_ROWS = Math.floor(CONTENT_HEIGHT / ROW_HEIGHT)`
  — with `SAFE_AREA.height = 672`, `CONTENT_TOP = SAFE_AREA.y + 130` (`:54`) and
  `CONTENT_BOTTOM = SAFE_AREA.y + SAFE_AREA.height - 40` (`:55`), so
  `CONTENT_HEIGHT = 502` and `ROW_HEIGHT = 34` (`:60`) → `MAX_LIST_ROWS = 14`.
  `:660, :674, :687` each `slice(0, MAX_LIST_ROWS)` with **no** "showing N of M"
  line — unlike `BattleHistoryScene.ts:578-584`, which does print one.
- **Impact today: none.** 14 ≥ 10, so all ten owned Relics render and the
  TASK-221 record's claim ("the viewer renders all ten owned Relics (its own row
  capacity is 14)", `TASK-221:400-402`) is accurate. It becomes a silent
  truncation the moment ownership can exceed 14 — which `MVP_SCOPE.md` §1/§3
  explicitly forbid without a Rule Change, so it cannot happen in MVP.
- **Classification:** **UI limitation**, pre-existing (the file's only change in
  the pending tree belongs to TASK-190/TASK-212A-1, not TASK-221).
- **Priority / effort / owner:** P3 · S (reuse the `BattleHistoryScene` notice
  pattern) · only if the Product Owner wants the guard now; otherwise no action.

**P-F2 — MEDIUM (test-coverage gap) — "all ten rendered in the Collection viewer" has no deterministic assertion.**

- **Evidence:** `CollectionViewerScene.test.ts:127-138` — the fixture commented
  "The starter ownership profile `DATABASE.md` §2 records the server grants" is
  `STARTER_PETS = [pet('pet-instance-1', 'Xích Lang')]` (1 pet) and
  `STARTER_RELICS` with **3** entries. Grep for `RELICS (10)`, `RELICS (9)`, or
  any 10-relic case in `src/frontend/client/tests` → **zero matches**. The only
  live check is `collection-viewer-smoke.mjs` (see P-F3), and the tab-count badge
  assertion compares the badge to the *delivered* array
  (`standalone-web-smoke.mjs`-style `tabs.includes('RELICS (' + ownedRelics.length + ')')`
  at `collection-viewer-smoke.mjs:663`), which is self-consistent rather than
  pinned to ten.
- **Impact:** the viewer's capacity claim and the "no viewer truncation" property
  are unverified by any deterministic test; a regression lowering
  `MAX_LIST_ROWS` below 10 (or a viewer that rendered 5 rows) would pass the unit
  suite. The Lobby equivalent *is* covered (`LobbyScene.test.ts:2415-2453`).
- **Priority / effort / owner:** P2 · S · testing agent — one case with a
  10-relic fixture mirroring the Lobby's `GRANTED_RELICS`.

**P-F3 — MEDIUM (stale harness expectation) — the Collection-viewer E2E still pins the old three-Relic starter set.**

- **Evidence:** `collection-viewer-smoke.mjs:45-52`:

  ```text
  /**
   * The starter grant DATABASE.md §2 records the server makes (TASK-083, TASK-187):
   * 1 Pet, 3 Basic Cards, 3 Relics. These are the deterministic expectations the
   * assertions below are written against.
   */
  const STARTER_PET_IDENTITY = 'Xích Lang';
  const STARTER_CARD_NAMES = ['Heal', 'Shield', 'Power Charge'];
  const STARTER_RELIC_NAMES = ['Berserker Core', 'Mana Crystal', 'Assassin Eye'];
  ```

  plus the header comment `:16-18` ("CARDS tab … → 3 starter Cards rendered" /
  "RELICS tab … → 3 starter Relics rendered") and the assertion at `:792-799`,
  which only requires those **three** names to be present among the rows.
- **Impact:** the check still passes (3 ⊂ 10) but it now certifies *less* than the
  baseline it names: a viewer that rendered only 3 of the 10 owned Relics would
  pass `phase5.starterRelicsRendered`. The comment also asserts a superseded fact
  ("1 Pet, 3 Basic Cards, 3 Relics") that contradicts `DATABASE.md` §2 item 1.
  `STARTER_PET_IDENTITY` is used only in a `.some(...)` membership check
  (`:676-680`), so it carries **no** ordering assumption.
- **Classification:** **stale harness expectation + weakened assertion** (E2E
  script, not production code).
- **Priority / effort / owner:** P2 · S · E2E harness owner — update the three
  constants/comments to the 5/3/10 baseline and assert the viewer renders the
  **full** delivered Relic set.

---

## 5. Existing-Account Behavior

### 5.1 What the implementation actually does

```text
PlayerRepository.cs:33-39   existing Player → `return existing;` BEFORE
                            composeStarterGrant is ever invoked
PlayerRepository.cs:66      composeStarterGrant(...) runs only on the new-Player
                            branch, after the early return
PlayerRepository.cs:1357-1362 (DATABASE.md §2 item 3)  "Not a repair / top-up
                            mechanism … The system must NOT evaluate conditional
                            top-ups"
```

Grep for `top-up`, `backfill`, `migration` of ownership, and any conditional
re-grant in `src/` → **no such code exists**. There is also **no** database-reset
script in the repository: `docker-compose.yml` mounts named volumes
(`postgres_data`, `redis_data`), and `scripts/` contains only the browser
harnesses plus a duplicate of `standalone-web-smoke.mjs`.

### 5.2 The four required determinations

| Question | Answer | Evidence |
|---|---|---|
| Are starter grants applied only at account creation? | **YES** | `PlayerRepository.cs:36-39` early return; composition is invoked only at `:66`, which is only reachable on the new-Player branch. The factory deliberately addresses no `PlayerId` so "creation-time only" is structural (`PlayerStarterGrantFactory.cs:166-171`) |
| Are existing accounts intentionally unchanged? | **YES, intentionally** | `DATABASE.md:1357-1359` — "Authenticating an existing Player performs no starter grant; the starter initialization path is reachable only when a new `Player` row is inserted." TASK-221's §9 non-goals include "no migration"; the task statement for this audit forbids adding migration/backfill |
| Do development/test reset flows create fresh accounts? | **YES, for every harness** | Each E2E harness registers a **unique new account** per run: `standalone-web-smoke.mjs` (Phase 1 register, then Phase 3b calls the account "fresh", `:1230-1238`), `collection-viewer-smoke.mjs:546-548` ("Register unique account"), `battle-history-smoke.mjs:969` ("Empty Battle History on a fresh account"). Backend PostgreSQL tests create their own Players and clean up (`TestStarterGrants.cs:135-166`) |
| Is the MVP assumption only about new accounts? | **YES** | `MVP_SCOPE.md:103-120` phrases the grant as "owned by a **newly created** Player"; `DATABASE.md:1352-1356` scopes it to "**New Player creation**" |

### 5.3 Finding

**E-F1 — MEDIUM (product/testing consideration, explicitly NOT fixed here) — pre-TASK-221 accounts cannot exercise the approved MVP content set.**

- **Evidence:** an account whose `Player` row was inserted before TASK-221 owns
  1 Pet / 3 Basic Cards / 3 Relics and is never topped up
  (`PlayerRepository.cs:36-39`). The local development database lives in the
  `postgres_data` named volume (`docker-compose.yml`), so a developer's existing
  accounts keep the old composition indefinitely; only a newly registered account
  gets 5 / 3 / 10. No test or harness reuses such an account, so **no test is
  affected** — but a human playing with an old account would see only three
  Relics in the Lobby (one page, no pager) and only one selectable Pet.
- **Authoritative rule / contract:** `MVP_SCOPE.md:105-107` (the grant "IS the MVP
  content grant") plus `:117-120` and `R-3` of `TASK-213:312` ("An account's
  owned content set does not change after creation in MVP"). These two are
  consistent **only** if the MVP claim is read as being about accounts created
  under the new baseline — which is exactly what the implementation does.
- **Impact:** a reviewer could otherwise read the Lobby's "No owned Relics
  returned."-style behaviour, or a single-page Relic column on an old account,
  as a TASK-221 regression. It is not: it is the documented no-backfill rule.
  The only real cost is developer-/tester-experience and manual-QA variance.
- **Classification per the task statement:** **product/testing consideration**,
  to be recorded — *not* silently fixed. This audit added no migration, no
  backfill, and no conditional top-up.
- **Recommendation (a decision, not an implementation):** the Product Owner
  should record, in one line, whether the MVP content baseline is defined over
  (a) **newly created accounts only** (current behaviour — recommended, since it
  is what `DATABASE.md` §2 item 3 and `MVP_SCOPE.md` §1 already say), or (b) *all*
  accounts, which would require a `GAME_RULES.md` §20 Rule Change plus a one-off
  backfill task. Recommend (a). For manual QA, the operational workaround is to
  register a new account (or drop the local `postgres_data` volume) — no product
  change.
- **Priority / effort / owner:** P2 · XS (a one-line product record) · Product
  Owner with a documentation agent.

---

## 6. API / Contract Consistency

| Contract surface | Requirement | Verdict | Evidence |
|---|---|---|---|
| `GET /api/pets`, `/api/cards`, `/api/relics` response semantics | Full owned collection; empty → `200 []`; no wrapper | ✅ | `CollectionQueryService.cs:98-119` (pets), `:204-235` (cards), `:271-333` (relics); empty short-circuits at `:102-107`, `:275-278`; `API_CONTRACTS.md` §5.5 |
| Membership semantics | `/api/cards` membership **is** the unlocked set; Pet Skill Cards are never members | ✅ unchanged | `CollectionQueryService.cs:204-206` reads `ListUnlockedAsync` (the `PlayerUnlockedCard` rows); the grant creates exactly 3 Basic unlock rows (`PlayerStarterGrantFactory.cs:248-261`); `AuthStarterOwnershipTests.cs:121` asserts exactly `card-heal, card-power-charge, card-shield` |
| Relic response shape | `relicId`, `name`, `trigger`, `condition`, `effectDefinition` — no definition id | ✅ unchanged | `CollectionResponses.cs:237-243`; `CollectionQueryService.cs:317-330`; `API_CONTRACTS.md` §5.4 |
| No pagination / limit / cursor / sort on collection reads | none in MVP; "the array is the full collection" | ✅ | `CollectionQueryService.cs:46-48` ("It sorts nothing … no sort, filter, page, limit, cursor, or search is applied"); `API_CONTRACTS.md` §5.5 |
| `BattleStartRequest` validation | exactly 4 members; `cardLoadout` exactly 3 Basic; `relicLoadout` 3–5 owned, pairwise distinct, order = slot order | ✅ unchanged | `API_CONTRACTS.md:344-396`; `RelicLoadoutService.cs:113-118, :125-186`; `LobbyScene.ts:1113-1122`; asserted at `LobbyScene.test.ts:966-980, :2530-2536` and `standalone-web-smoke.mjs:3387` |
| Preserved-loadout semantics | Client presentation state only; not persisted to storage or the backend; editable on restore; never authoritative | ✅ unchanged | `PreservedLoadout.ts:34-58, :79-114`; `LobbyScene.ts:790-823`; ADR-022; asserted at `LobbyScene.test.ts:2539-2591` and `standalone-web-smoke.mjs:3224-3231, :3377-3394` |
| No equip/loadout member leaked into a collection response | §5.6 excludes equip members | ✅ | `CollectionQueryService.cs:50-52`; `CollectionResponses.cs` has no equip member |

**Contract-widening check (the task statement's explicit constraint).** TASK-221
added **no** table, column, migration, endpoint, request member, response member,
hub method, event, Redis shape, or SignalR state. Verified independently:

```text
API_CONTRACTS.md diff     §3's member list is unchanged (the same 4 members);
                          the Relic bound is "3–5" before and after
CollectionResponses.cs    the three response records' member sets are the
                          §5.1/§5.3/§5.4 sets; no new member
RelicLoadoutService.cs    untouched — Min 3 / Max 5 before and after
BattleHub.cs / SignalR    the pending diff belongs to TASK-208/TASK-210/212A-1
                          (reconnect recovery, event presentation, state
                          projection); grep finds no ownership or loadout-count
                          change
```

**C-F1 — INFORMATIONAL (no violation).** `API_CONTRACTS.md:352`'s request
example shows three `relic_id_*` entries and `:873-884`'s relic example shows one
element. Both are minimal **valid** illustrations inside the documented 3–5 bound,
not bounds themselves. Classification: correct/intentional.

**Conclusion.** The new ownership set contradicts no API contract, membership
semantic, collection contract, `BattleStartRequest` validation rule, or
preserved-loadout semantic. **No contract needed widening, and none was
widened.**

---

## 7. Documentation Consistency

### 7.1 The four required verifications

| # | Requirement | Verdict | Evidence |
|---|---|---|---|
| D-1 | `DATABASE.md` accurately describes the starter composition | ✅ **PASS** | §2 item 1: "**Starter Pets (5 owned `Pet` rows)**" (`:1304`) with all five canonical ids in `PET_RULES.md` §8 order (`:1305-1307`); "**Starter Basic Cards (3 `PlayerUnlockedCard` rows)**" (`:1314-1317`) with the Pet Skill exclusion (`:1318-1322`); "**Starter Relics (10 owned `Relic` rows)**" (`:1324-1331`) with all ten ids; "Owning all ten is what makes the 3–5 Relic loadout a real build decision … nothing is equipped by the grant" (`:1334-1335`). Item 3: "5 `Pet`, 3 `PlayerUnlockedCard`, 10 `Relic`" (`:1353-1354`). Item 4: "all **18** starter ownership rows" (`:1368`) and "(Player + 18 ownership entities)" (`:1377`). **5 + 3 + 10 = 18 — arithmetic consistent.** The version preamble (`:3-11`) records the 1.36 change |
| D-2 | `MVP_SCOPE.md` remains the canonical product decision | ✅ **PASS** | §1 "Content ownership & reachability (TASK-213 decision)" (`:103-126`) states 5 Pets / 3 Basic Cards / 10 Relics / 5 Bosses, the derived-Pet-Skill rule, and "MVP has NO acquisition system"; §1 `:81` "3–5 equipped Relics per battle"; §3 FUTURE lists post-creation acquisition, Passive effects, and Pet Star progression (`:163-177`); §2/§4 unchanged. No §1 item claims a Relic count other than 10 or is inconsistent with §3 |
| D-3 | The TASK-221 completion record accurately reflects the implementation | ✅ **PASS (with the R-F1 wording exception)** | Verified against the tree: §2's BEFORE/AFTER table matches the diff exactly (5 ids / 3 ids / 10 ids; 18 rows); §8's file list is complete and no file outside it carries a TASK-221 signature; the provenance arithmetic is **exact** — `git status --porcelain` reports **48 modified tracked files + 16 untracked entries, 0 deleted/renamed**, matching the record's "now 48 modified tracked files + 16 untracked entries" (`TASK-221:477-479`), and TASK-223's measured base (38 modified + 13 untracked) plus the record's own 10 newly-modified files reconciles to 48. Its §7 suite claims were re-run and reproduce (§12). Only the "exactly 3" summaries at `:137` and `:575` are stale → **R-F1** |
| D-4 | No new contradictory technical documentation was introduced | ✅ **PASS** | The only document TASK-221 wrote is `DATABASE.md` §2 + its preamble, and both are synchronized with `MVP_SCOPE.md` §1 and with the code. Every conflicting item found by this audit is either **pre-existing** or inside a **task record** (below) — none was introduced by TASK-221 |

### 7.2 Documentation findings

**D-F1 — MEDIUM — `DATABASE.md` §2 item 2 still frames the grant as a "minimum bootstrap".**

- **Evidence:** `DATABASE.md:1341-1345`:

  ```text
  **2. Semantic classification — MVP bootstrap, not acquisition gameplay:**
  These rows represent **MVP bootstrap / test content** for newly created Players.
  They exist solely so a newly created Player has the owned content the
  `POST /api/battle/start` loadout validation rules require (`API_CONTRACTS.md`
  §3: 1 Pet, 3 Basic Cards, 3–5 Relics). They do **not** define or constrain future
  ```

  Contradicted by the higher-authority `MVP_SCOPE.md:105-107` (GAME DESIGN >
  TECHNICAL, `AGENTS.md` §2): "The deterministic Player-creation starter grant
  (`DATABASE.md` §2) IS the MVP content grant — **it is not a minimum
  bootstrap**".
- **Impact:** the paragraph reads as the **old 1 / 3 / 3 composition**
  ("1 Pet, 3 Basic Cards, 3–5 Relics"), labels the rows disposable "test
  content", and invites a future task to re-compose them freely — whereas
  `MVP_SCOPE.md` §1/§3 require a `GAME_RULES.md` §20 Rule Change before the owned
  set changes. Mitigated by `:1297-1298` ("**is the MVP content set**") and
  `:1349-1350` (the Rule-Change pointer), hence MEDIUM rather than HIGH.
- **Authoritative rule:** `MVP_SCOPE.md:103-126`.
- **Priority / effort / owner:** P2 · S (retitle item 2 and replace the
  "exist solely so … require" rationale with the §1 rationale) · **TASK-217B**
  (cross-reference / stale-claim sweep). This is an `AGENTS.md` §4 documentation
  conflict and must be raised, not silently edited.

**D-F2 — MEDIUM (pre-existing, not caused by TASK-221) — `DATABASE.md` §1 says the Relic ownership storage shape "remains OPEN", contradicting the same document.**

- **Evidence:** `DATABASE.md:472-481` presents the `Relic` entity as "a player's
  OWNED instance, **if** Relics have per-instance state; **otherwise** ownership is
  a join table … this storage-shape question remains OPEN and is NOT decided by
  `RELIC_RULES.md` §2.2–§2.5" — immediately above `:482-485`, which lists
  `RelicInstanceId (PK)`, `PlayerId (FK)`, `RelicDefinitionId (FK)`, `AcquiredAt`.
  Contradicted again by `:1271` ("Player 1 ── N Relic (owned instances)"),
  `:1291-1293`, `:1324`, `:1353-1354`.
- **Impact:** an implementer could conclude the shape is undecided and model Relic
  ownership as a join table, which cannot carry the ten distinct server-minted
  `RelicInstanceId`s the contract requires (`RELIC_RULES.md` §2.2;
  `DATABASE.md:1331-1334`). `RELIC_RULES.md` §2.2 delegates the shape to
  `DATABASE.md`, so this document is the owner and contradicts itself.
- **Classification:** **pre-existing stale documentation.** Untouched by TASK-221
  (the pending `DATABASE.md` diff contains no hunk in this range), and outside the
  1-Pet/3-Relic question — reported because it is in the document this task owns.
- **Priority / effort / owner:** P3 · S (state the decided instance-row shape,
  drop the "remains OPEN" parenthetical) · **TASK-217B**.

**D-F3 — MEDIUM (newly relevant because of TASK-221) — `ROADMAP.md:42-44` still says the two remaining Pet rows are unprovisioned.**

- **Evidence:** `ROADMAP.md:42-44`:

  ```text
  - All 5 Pets (all 5 Signature Skills are now content-defined — `CARD_RULES.md`
    §4.1; the Thanh Xà and Sơn Hùng rows remain to be provisioned, see
    PET_RULES.md §8 note)
  ```

  Contradicted by `DATABASE.md` §5 item 4 (both rows provisioned by
  `20261004055006_ProvisionThanhXaAndSonHungSignatureSkills`, so
  `PetDefinition` = 5) and by `PlayerStarterGrantFactory.cs:314-321` — a missing
  definition **aborts the whole grant**.
- **Why newly relevant:** before TASK-221 this stale line was harmless (only one
  Pet was granted). After TASK-221 the shipped bootstrap resolves **all five**
  `PetDefinition` rows, so the stale claim now directly contradicts the bootstrap
  that just landed. Already reported as stale by
  `TASK-212-post-task-211-product-audit.md` and assigned to TASK-217B; still
  unfixed.
- **Priority / effort / owner:** P2 · XS (delete the trailing clause) ·
  **TASK-217B**.

**D-F4 — MEDIUM (newly false because of TASK-221) — two completed records state that `relic-emergency-core` is deliberately not granted.**

- **Evidence:**
  - `TASK-213-content-reachability-decision.md:210-211`: "`relic-emergency-core`
    is **provisioned and deliberately not granted** (`PlayerStarterGrantFactory.cs:83-84`;
    `DATABASE.md` §2.1)."
  - `TASK-213-post-task-212a-1-product-gameplay-audit.md:344-346`: "…
    **deliberately not selected** for the starter set
    (`PlayerStarterGrantFactory.cs:81-84`; `DATABASE.md:1313-1315`)."
  - `TASK-213-post-…audit.md:114-116`: "The single provisioned `CardCost` source
    is Emergency Core, which is **not in the starter grant** … and cannot be
    acquired by any … path."
- **Why newly false:** `PlayerStarterGrantFactory.cs:131` now grants
  `relic-emergency-core`; `DATABASE.md:1327` lists it; `MVP_SCOPE.md:110` owns all
  ten. The cited line ranges (`PlayerStarterGrantFactory.cs:81-84`, `DATABASE.md:1313-1315`)
  no longer point at an exclusion — they now point at the Pet-array and
  "No Pet is privileged" text.
- **Impact:** a reader could re-introduce an "Emergency Core is excluded by
  design" assumption, or declare the CardCost path unreachable — the opposite of
  `TASK-213:332-334` (§3.2 C-3, "`relic-emergency-core` becomes equippable") and
  TASK-221 R-5 (`:555-559`).
- **Classification:** **stale documentation inside completed (immutable) task
  records** (`TASK_LIFECYCLE.md:218`) — reported, not edited.
- **Priority / effort / owner:** P2 · S (one superseding note, or fold into the
  same corrective record as R-F1) · documentation agent under proposed
  **TASK-225**.

**D-F5 — MEDIUM (newly false because of TASK-221) — `TASK-213` §2.6 states "Pet count 1 ownable".**

- **Evidence:** `TASK-213-content-reachability-decision.md:277-280`:

  ```text
  Pet count    1 ownable. `new Pet` occurs only at PlayerStarterGrantFactory.cs:187
               and PlayerRepository.cs:127; no reward path inserts a Pet
               (PetRepository.cs:120-126); BattleResultService.cs:509-510 documents
               that the reward does not create one.
  ```

  Superseded by `DATABASE.md:1304` ("**Starter Pets (5 owned `Pet` rows)**") and
  `MVP_SCOPE.md:108`. The section is headed "§2.6 Pet progression today
  (verified)" and speaks in the present tense.
- **Impact:** a reader could treat "only one Pet can be owned" as a current
  product rule and flag or "repair" the five-Pet grant. No `docs/` file makes this
  claim — `GAME_RULES.md:63` ("A player may own multiple Pets; only one Pet is
  active in a battle"), `GDD.md`, and `PET_RULES.md:79-81` are all correct. The
  statement the record *intends* (no reward path inserts a Pet; the reward does
  not create one) is still true; only the "1 ownable" cap is falsified.
- **Classification / owner:** stale documentation in an immutable completed
  record · same owner as D-F4.

**D-F6 — LOW — `MVP_SCOPE.md:79` says "~10 Relics" where the provisioned set is exactly ten.**

- **Evidence:** §1 Relics block `:77-82` reads "~10 Relics / Trigger system … /
  3–5 equipped Relics per battle", while the same document's §1 ownership block
  `:110` says "10 Relics one owned Relic instance per MVP Relic definition", and
  `DATABASE.md:19, :1324` and `RELIC_RULES.md:763-777` record exactly ten
  provisioned. (`ROADMAP.md:50` carries the same soft wording in a `Status:
  Planning` document.)
- **Impact:** a reader could treat the Relic count as a target rather than a fixed
  set. It reaches no loadout bound and no ownership rule.
- **Priority / effort / owner:** P3 · XS · TASK-217B.

**D-F7 — LOW — TASK-221:261 counts "19 rows" where the document it cites counts 18.**

- **Evidence:** `TASK-221:261` — "a new Player receives 5 Pet / 3 Card / 10 Relic
  rows (19 rows, one commit)"; `DATABASE.md:1368` and `:1377` — "the Player and all
  18 starter ownership rows". Both are correct: 19 = 18 ownership rows + the
  `Player` row.
- **Impact:** a diff-based reader could report a false contradiction.
- **Priority / effort / owner:** P3 · XS · same corrective record as R-F1.

**D-F8 — INFORMATIONAL — no `docs/` document states the presentation requirement that every owned Relic must be inspectable and selectable.**

- **Evidence:** the requirement lives only in `TASK-213-content-reachability-decision.md:327-331`
  (C-2) and `TASK-221:165-210` (§4). No `docs/` file mentions a loadout-screen row
  capacity, a pager, or a visibility requirement — grep over `docs/` for
  `MAX_LIST_ROWS`, `slice(0`, `row capacity`, `visible rows`, `pager` returns
  nothing.
- **Classification:** **missing information (`AGENTS.md` §21), not a conflict.**
  Recorded so the next reader knows why the pager exists; no action required for
  TASK-221A.

**D-F9 — INFORMATIONAL (false-positive guard) — every other `exactly`/count hit is correct.**
`ROADMAP.md:28, :36` are Phase-1 slice deliverables superseded by Phase 2 `:50`;
`DATABASE.md:43` is about the Boss row set; `COMBAT_RULES.md` and
`MATCH3_RULES.md` hits are worked examples and match length; `DATABASE.md`'s and
`RELIC_RULES.md`'s "Prior 1.x" entries are version-preamble history whose
current-state counterparts are synchronized (§7.1 D-1). None is drift.

---

## 8. New Risks

Twelve risk classes were searched for explicitly. Every finding below carries
evidence, the authoritative rule or contract, impact, priority, effort, and an
owner.

| # | Risk (task statement) | Verdict | Finding / evidence | Severity |
|---|---|---|---|---|
| 1 | hard-coded maximum visible Relics | ⚠️ latent only | Lobby cap is now a **page size**, not a truncation (`LobbyScene.ts:905, :1479`); the pager is drawn only when needed (`:1521-1523`). **`CollectionViewerScene.ts:62, :660, :674, :687`** keeps a hard 14-row `slice(0, …)` with **no truncation notice** — latent because 14 ≥ 10 (**P-F1**) | LOW |
| 2 | hard-coded exactly-3 assumptions | ✅ none in code | See §3.2. `RelicLoadoutService.cs:48, :55` = 3/5; `LobbyScene.ts:99-100` = 3/5; zero "exactly 3 Relics" strings. Two stale occurrences live in the TASK-221 **record** (**R-F1**) | MEDIUM (doc) |
| 3 | ordering assumptions in starter grants | ✅ none material | The grant's array order is presentation-neutral and no read is an ordered contract (`PlayerStarterGrantFactory.cs:77-80, :121-124`; `API_CONTRACTS.md` §5.5). The E2E selects from whatever the read returned (`standalone-web-smoke.mjs:1728-1751`) and asserts only the order the player chose (`:3385`). `collection-viewer-smoke.mjs:676-680` uses a `.some(...)` membership check, not `[0]` | — |
| 4 | ownership-count assertions that should be 5/3/10 | ⚠️ gap | Backend tests **do** pin 5/3/10 exactly (`AuthStarterOwnershipTests.cs:42-44, :68-70, :92, :120-121, :160, :248-254`; `PlayerStarterOwnershipTests.cs:70, :239`; `PlayerStarterOwnershipPostgresTests.cs:125, :560`; `PlayerStarterGrantFactoryTests.cs:140, :143`). **The E2E does not**: every wait condition is loose — `ownedPetsCount > 0 && ownedCardsCount >= 3 && ownedRelicsCount >= 3` (`standalone-web-smoke.mjs:1458, :1939, :3213, :3618`; `battle-history-smoke.mjs:1058, :1347`) and the strongest relic bound is `ownedRelicsCount > 8` (`standalone-web-smoke.mjs:1576`) | MEDIUM (harness) |
| 5 | tests using incomplete provisioned content | ✅ none | `TestProvisionedContent.cs:39-65` seeds **all** 5 Pets / 8 Cards / 10 Relics; `SeedPets` (`:88-95`) wires each Pet's `SignatureSkillCardId` to that Pet's Pet Skill Card; the API host's grant cannot abort for a missing definition | — |
| 6 | fixtures diverging from production starter content | ⚠️ by design, 3 occurrences | `LobbyScene.test.ts:152-156` and `CollectionViewerScene.test.ts:134-138` keep a **3-Relic** `STARTER_RELICS`, and `CollectionViewerScene.test.ts:127-128` describes it as "The starter ownership profile `DATABASE.md` §2 records the server grants" — which is now **factually wrong**. `TestStarterGrants.cs:41-90` correctly mirrors 5/3/10. The divergence is what makes **R-F2** unexercisable; the comment is a documentation defect | MEDIUM (fixture comment) / LOW (fixtures) |
| 7 | `CollectionViewerScene` assumptions about Relic count | ⚠️ | `MAX_LIST_ROWS = 14 ≥ 10` so all ten render; but no deterministic test covers ten (**P-F2**) and the E2E relic expectation is still the 3-name legacy list (**P-F3**) | MEDIUM |
| 8 | `BattleStartRequest` assumptions about Relic count | ✅ none | `LobbyScene.ts:1113-1122` copies the selection; the only bounds are the documented 3–5 interaction limits (`:99-100, :885-889, :1132-1134`) and no default or truncation is applied. Server remains authoritative (`RelicLoadoutService.cs`) | — (LOW observation: **R-F4**) |
| 9 | preserved-loadout assumptions | ✅ none | `PreservedLoadout.ts` carries the four request members only, copies in and out (`:79-86, :99-114`), lives in the Phaser registry (`:48-52`) so it cannot cross a session or be tampered with via storage; restore is verbatim and editable (`LobbyScene.ts:808-823`); paging does not touch it (`:920-933`) | — |
| 10 | fresh-account E2E assumptions | ✅ sound | All three harnesses register a **unique new** account per run (`standalone-web-smoke.mjs` Phase 1; `collection-viewer-smoke.mjs:546-548`; `battle-history-smoke.mjs:969`), which is exactly what the creation-only grant requires. No harness reuses a pre-TASK-221 account | — (see **E-F1**) |
| 11 | stale comments describing the old bootstrap | ⚠️ 3 sites | `collection-viewer-smoke.mjs:16-18, :45-49` ("1 Pet, 3 Basic Cards, 3 Relics", "3 starter Relics rendered") — **P-F3**; `CollectionViewerScene.test.ts:127` ("The starter ownership profile `DATABASE.md` §2 records the server grants" above a 1/3/3 fixture) — item 6; `LobbyScene.test.ts:96` ("the three starter-owned Relics' …") — this one describes the *local default fixture*, so it is correct in context. **No stale old-bootstrap comment remains in `src/` production code** | MEDIUM / LOW |
| 12 | player-facing text saying exactly 3 Relics | ✅ none | `LobbyScene.ts:1259-1260` renders `Relics: n/5 (min 3)`; the error/status vocabulary is generic (`:1125-1142`, `:1302-1319`); grep for `3 Relics`, `three Relics`, `exactly 3 Relic` over `src/frontend/client/src` → **zero matches** | — |

### 8.1 New-risk summary

```text
HIGH     0
MEDIUM   10   R-F1, R-F2, P-F2, P-F3, E-F1, D-F1, D-F2, D-F3, D-F4, D-F5
LOW       6   O-F1, R-F3, R-F4, P-F1, D-F6, D-F7
INFO      4   O-F2, C-F1/D-F9, R-F5, D-F8      (verified-correct /
                                                false-positive guards /
                                                missing information)
CLEAN        the remaining risk classes resolve with no finding at all
```

*Count reconciliation:* the twelve-row risk-scan table above crosses finding
boundaries — one risk class (ownership-count assertions, row 4) and one risk
class (fixtures diverging, row 6) each contain more than one finding, and row 1
and row 7 share P-F1/P-F2/P-F3. The authoritative per-finding index is §8.2.

### 8.2 Severity / ownership index (all findings)

| ID | Severity | Class | Location | Owner |
|---|---|---|---|---|
| R-F1 | MEDIUM | stale documentation (task record, immutable) | `TASK-221:137, :575` | proposed TASK-225 / TASK-217B |
| R-F2 | MEDIUM | test-coverage gap | `LobbyScene.test.ts:922-930` + fixture `:152-156` | testing agent |
| P-F2 | MEDIUM | test-coverage gap | `CollectionViewerScene.test.ts:127-138` | testing agent |
| P-F3 | MEDIUM | stale harness expectation + weakened assertion | `collection-viewer-smoke.mjs:16-18, :45-52, :792-799` | E2E harness owner |
| E-F1 | MEDIUM | product/testing consideration | `PlayerRepository.cs:36-39`; `DATABASE.md:1357-1359` | Product Owner |
| D-F1 | MEDIUM | stale documentation (`AGENTS.md` §4 conflict) | `DATABASE.md:1341-1345` | TASK-217B |
| D-F2 | MEDIUM | pre-existing stale documentation | `DATABASE.md:472-481` | TASK-217B |
| D-F3 | MEDIUM | stale documentation, newly relevant | `ROADMAP.md:42-44` | TASK-217B |
| D-F4 | MEDIUM | stale documentation, newly false (immutable records) | `TASK-213:210-211`; `TASK-213-post-…audit.md:344-346, :114-116` | proposed TASK-225 |
| D-F5 | MEDIUM | stale documentation, newly false (immutable record) | `TASK-213:277-280` | proposed TASK-225 |
| O-F1 | LOW | documentation wording | `DATABASE.md:1336-1339` vs `PlayerStarterGrantFactory.cs:77-80, :121-124` | TASK-217B |
| R-F3 | LOW | test-coverage gap (E2E) | `standalone-web-smoke.mjs:1728-1751, :3307-3321` | E2E harness owner |
| R-F4 | LOW | latent client guard omission (not reachable) | `LobbyScene.ts:1132-1134` | implementation agent (optional) |
| P-F1 | LOW | UI limitation, pre-existing | `CollectionViewerScene.ts:62, :660, :674, :687` | implementation agent (optional) |
| D-F6 | LOW | soft wording | `MVP_SCOPE.md:79` | TASK-217B |
| D-F7 | LOW | clarity | `TASK-221:261` | proposed TASK-225 |
| D-F8 | INFO | missing information (`AGENTS.md` §21) | task records only | — |
| C-F1, D-F9, O-F2, R-F5 | INFO | verified-correct / false-positive guards | see §6, §7.2, §2.4, §3.3 | — |

**No finding is an implementation defect in the TASK-221 change.** Of the sixteen
material findings: nine are documentation drift (three of them — R-F1, D-F4, D-F5
— inside immutable completed task records, and three — D-F1, D-F2, O-F1 — in
`DATABASE.md`, which TASK-217B already owns); four are test-coverage gaps
(R-F2, P-F2, P-F3, R-F3); one is a product/testing consideration (E-F1); and two
are latent UI/guard observations (P-F1, R-F4). None of them makes the shipped
behaviour wrong, and none contradicts the 3–5 rule or the 5 / 3 / 10 / 5
baseline.

---

## 9. Downstream Task Reassessment

### 9.1 TASK-212B — In-battle Card cost and affordability

```text
Disposition:  UNCHANGED — remains DEFERRED (not cancelled)
Readiness:    UNCHANGED
```

- **What TASK-221 changed:** `relic-emergency-core` became equippable, so its
  declared content ("CardCost 50% (Pet, Battle)") is now presented in the Lobby and
  printed on the Card/Relic row (E2E `phase4.relicRowsStateTriggerConditionAndEffect`,
  `standalone-web-smoke.mjs:1603-1609`). This is exactly `TASK-213:332-334`'s
  recorded consequence C-3.
- **What it did not change:** the number of Card-cost **sources**. Exhaustive
  search over the migrations finds `CardCost` declared by **only** one relic —
  `20261003074309_StructureRelicDefinitionStructuredColumns.cs:218, :402`
  (`relic-emergency-core`); the remaining-relics migration mentions `CardCost` only
  in a comment listing the allowed `effectType` set
  (`20261004153916_ProvisionRemainingMvpRelicDefinitions.cs:85`). So
  `TASK-213:518-524`'s promotion trigger **(a) "a second Card-cost source becomes
  reachable"** is **not met**. Trigger **(b)** ("the Basic Card loadout becomes a
  real choice — a 4th Basic Card exists") is **not met** either: `DATABASE.md:1322-1323`
  — "the loadout is exactly 3 Basic Cards and exactly 3 exist", and TASK-221 grants
  3 of 3. Trigger **(c)** (the Product Owner asks for an in-battle card price) is
  not evidenced.
- **Conclusion / instruction to the next implementer:** **do not** reintroduce
  `CardCostModifiers`, an effective-cost member, or a cost display on the strength
  of TASK-221. `TASK-213` §7.1 point 4 remains binding: exposing the authored
  `PowerCost` alone is documented as misleading, and the effective cost must be
  server-composed if it is ever shown. TASK-212B stays off the MVP order; its only
  remaining blocker is the Product Owner's willingness to expand the wire contract
  for one Relic's modifier.
- **Effort to keep deferred:** zero. **Owner:** Product Owner (decision).

### 9.2 TASK-218 — Relic trigger presentation

```text
Disposition:  UNCHANGED — still P2, still implementation-only, still independent
Readiness:    UNCHANGED (the decision and mechanism are unchanged)
Value:        RISES — coverage goes from 3 of 10 relic definitions to 10 of 10
```

- **The blocker, verified:** `BattleEventPresenter.ts:445-463` parses
  `RelicTriggered` as `{ type, relicId }` only, and `:624-628` records why no
  callout is drawn — "the contract delivers only the Relic's owned instance
  identity and **this scene has no Relic definition source**, so there is no
  player-facing name to show".
- **What TASK-221 changes:** the definition source now exists on the client and is
  reachable — `GameRuntime.getRelics()` (`GameRuntime.ts:812-813`) →
  `ApiService.getRelics()` (`ApiService.ts:310`) → `GET /api/relics`, which
  returns **all ten** owned instances with their `name`, `trigger`, `condition`,
  and `effectDefinition` (`CollectionQueryService.cs:262-334`);
  `LobbyScene.ts:305, :710` already consumes it. Before TASK-221 only **three**
  relics could ever be owned, so at most three of the ten relic trigger
  definitions could ever fire and be named in battle. Now all ten can.
- **Consequences for the task's scope:**
  1. It remains **implementation-only with no contract change** — the name comes
     from the delivered collection read, not from a new event member.
  2. It must **choose and record its name source**. Recommended: a battle-scoped
     map built from `getRelics()` — the same pattern `BattleScene.loadCardDefinitions()`
     (`BattleScene.ts:1156-1173`) already uses for Cards — keyed by owned instance
     id, with a fail-closed fallback to the raw `relicId`.
  3. The task should now assert coverage across **all ten** relic definitions, not
     three. This is the substantive scope increase.
  4. It must not become a second content catalog: the map holds only what the read
     delivered (`AGENTS.md` §7, `GAME_STATE.md` §0 item 5).
- **Priority:** P2, unchanged — but it is now the largest player-visible coverage
  gain per unit of effort. **Effort:** S–M. **Owner:** implementation agent
  (client presentation) + testing agent.

### 9.3 TASK-219A — Signature Skill identification / protocol correction

```text
Disposition:  UNCHANGED — still an implementation task; its decision was made by
              TASK-213 §5 and needs no further decision round
Readiness:    UNCHANGED (TASK-221 did not alter the Pet Skill Card path)
Impact:       RISES — from 1 of 5 reachable Signature Skills to 5 of 5
```

- **The defect, re-verified live in source:**
  `BattleScene.ts:1156-1173 loadCardDefinitions()` populates `cardDefinitions`
  from `this.runtime.getCards()`; `:1228-1234` then identifies the Signature Skill
  as `this.cardDefinitions.get(cardId)?.category === 'PetSkill'` and falls back to
  `displayName = cardId`.
- **Why it fails:** `GameRuntime.getCards()` → `ApiService.getCards()` → `GET /api/cards`,
  whose membership is the **unlocked** set (`CollectionQueryService.cs:204-206`;
  `API_CONTRACTS.md` §5.3 — "presence in this array is the unlocked state"). The
  grant creates three **Basic** unlock rows and no Pet Skill row
  (`PlayerStarterGrantFactory.cs:248-261`; `CARD_RULES.md` §1 item 4). The derived
  Signature Skill's `cardId` is therefore never a key in `cardDefinitions`, so
  `isPetSkill` is always `false`: the primary battle surface renders
  `Card: card-inferno` (a raw developer id) instead of `Skill: Inferno`, and the
  trigger submits `CardCast` (`:1256-1257`) rather than `PetSkillCast`
  (`:1254-1255`).
- **What TASK-221 changes:** nothing on this path. Pet Skill Cards remain
  non-ownable by design, so `getCards()` still returns three rows. The
  identification defect is unaffected.
- **What TASK-221 changes about its impact:** the number of reachable Signature
  Skills. Before, one Pet was owned, so exactly **one** of the five Signature
  Skills could reach a battle. Now all **five** Pets are owned, so all five
  Signature Skills are reachable (`PET_RULES.md` §2.1 item 1 — choosing the active
  Pet is a loadout decision) and every one of them is mis-identified. The defect
  moves from 1-in-5 to 5-in-5.
- **The already-recorded remedy** (`TASK-213:383-391`): the identification must
  come from a **delivered server datum**; the decided read source is
  `API_CONTRACTS.md` §5.1 `GET /api/pets` gaining the active Pet's Signature Skill
  identity (minimum `cardId`, `name`, `category`); `SIGNALR_PROTOCOL.md` §4.3
  item 13's `Category == PetSkill` rule is to be replaced; and `API_CONTRACTS.md`
  §5.3's membership must **not** be widened.
- **Note this is the only remaining case where a contract must be widened** — and
  it is authorised by an existing decision, not by this audit. This audit changes
  no contract.
- **Priority:** P1 as a contract/consistency defect, P2 as player-visible —
  **unchanged**, but its impact argument is now five times stronger. **Effort:**
  M (one read member + its projection + client rendering + tests). **Owner:**
  implementation agent (backend read member) + implementation agent (client) +
  testing agent.

### 9.4 TASK-215A — Pin the Passive / Tier / Star deferral

```text
Disposition:  UNCHANGED — still P3, still a test-only task
Readiness:    UNCHANGED
```

- **Does the five-Pet ownership change any assumption about Passive / Tier / Star?**
  **No assumption is broken, and none is newly required.**
  - `Pet.Tier` and `Pet.Star` remain `init`-only; the grant sets `Tier = PetTier.Common`
    and `Star = Pet.MinStar` for every one of the five instances
    (`PlayerStarterGrantFactory.cs:240-241`), which is exactly the single-Tier,
    fixed-Star contract (`PET_RULES.md` §3.4; `MVP_SCOPE.md:64-65`). Granting five
    instances does not create a Tier-up or Star-up rule.
  - Passive **identity** and threshold are still per-`PetDefinition`
    (`PetDefinition.PassiveId` / `PassiveThreshold`) and the **effect** layer is
    still absent — `BattleStateService` charges progress and emits
    `PassiveCharged` / `PassiveTriggered` without applying an effect, matching
    `TASK-213` §6 (`MVP_SCOPE.md:66-68, :173-174`).
- **What TASK-221 does change:** the fixture surface. Where a "Tier and Star cannot
  change" assertion previously had one Pet instance to exercise, it now has five —
  a strictly better test, and a cheap one to write, because the five instances are
  reachable directly from the grant (`TestStarterGrants.cs:41-90`,
  `PlayerStarterGrantFactoryTests.cs:140`).
- **One genuine interaction worth naming (not a TASK-215A blocker):** with five
  owned Pets, "which Pet does XP go to?" becomes an observable question for the
  first time. The reward path already scopes Pet XP to the battle's own active Pet
  (`BattleResultService.cs:525-530`, operating on the `pet` resolved from the
  battle/result identity, not on "the Player's first Pet"), so the answer is
  correct today — but it is now a property worth pinning. Recommend TASK-215A (or
  TASK-216, below) add "the reward progresses exactly the battle's active Pet, and
  no other owned Pet" as a case.
- **Priority:** P3, unchanged. **Effort:** S. **Owner:** testing agent.

### 9.5 TASK-217A / TASK-217B / TASK-217C — Documentation reconciliation

Only drift that TASK-221 makes **newly relevant** is listed. No broad cleanup is
performed or proposed.

```text
TASK-217A  Architecture / component / directory reconciliation + stale code
           comments
           TASK-221-RELEVANT DRIFT: NONE FOUND.
           The two named stale comments TASK-213 §11.1 assigned to TASK-217A
           (BattleStartService.cs:248-253 "exactly three ... Bosses";
           BattleStateService.cs:1582-1584 "Passive EFFECT application is out of
           this task's scope ... applies nothing") are BOTH UNRELATED to TASK-221
           and remain open. TASK-221 authored no stale code comment: grep over
           src/ for old-bootstrap wording finds only comments describing the NEW
           5/3/10 state. ADD NOTHING.

TASK-217B  Cross-reference / envelope / stale-claim sweep          ◄ PRIMARY OWNER
           NEWLY RELEVANT DRIFT (4 items, all caused or exposed by TASK-221):
             D-F1  DATABASE.md:1341-1345  §2 item 2 still calls the grant a
                                           "minimum bootstrap" and reads in the
                                           old 1 / 3 / 3 shape, contradicting
                                           MVP_SCOPE.md:105-107.        MEDIUM
             D-F3  ROADMAP.md:42-44       "the Thanh Xà and Sơn Hùng rows remain
                                           to be provisioned" — now directly
                                           contradicts the shipped 5-Pet grant
                                           (a missing definition aborts it). MEDIUM
             D-F6  MVP_SCOPE.md:79        "~10 Relics" vs the exact ten.  LOW
             O-F1  DATABASE.md:1336-1339  the "Selection basis" sentence vs the
                                           factory's array-order comments. LOW
           PRE-EXISTING, IN THE SAME DOCUMENT, UNTOUCHED BY TASK-221 (report only):
             D-F2  DATABASE.md:472-481    the Relic ownership storage shape is
                                           called "OPEN" while §1/§2 decide it. MEDIUM
           TASK-217B must still NOT absorb any product decision: D-F1 and D-F2
           are documentation-consistency corrections to text MVP_SCOPE.md and
           DATABASE.md already own.

TASK-217C  Apply the Card-cost authority ruling (SIGNALR_PROTOCOL.md §3.2.20 item 2)
           TASK-221-RELEVANT DRIFT: NONE.
           TASK-221 did not touch SIGNALR_PROTOCOL.md's Card-cost sentences. The
           ruling and its one-sentence correction are unchanged, and TASK-212B's
           continued deferral (§9.1) means the correction stays P3. ADD NOTHING.

NOT OWNED BY 217A/B/C — because completed task records are immutable
(TASK_LIFECYCLE.md:218), the following cannot be repaired by a documentation
sweep and need a small superseding record:
  R-F1  TASK-221:137, :575              "exactly 3" summaries.       MEDIUM
  D-F4  TASK-213:210-211 + TASK-213-post-…audit.md:344-346, :114-116
                                        "relic-emergency-core … deliberately
                                        not granted/selected".       MEDIUM
  D-F5  TASK-213:277-280                "Pet count 1 ownable".       MEDIUM
  D-F7  TASK-221:261                    "19 rows" vs 18.             LOW
  (Plus the LOW labelled pre-decision baseline counts TASK-213:203-207, :236,
   :240-244 and the TASK-213-post-…audit baseline rows, which are dated audit
   evidence and need no correction.)
```

### 9.6 TASK-216 — XP persistence / atomicity

```text
Disposition:  UNCHANGED (corrected subject stands — do not revive the old premise)
Readiness:    UNCHANGED
```

- **Does TASK-221 create any new interaction with reward/progression persistence?**
  **No new write path, no new track, no new ordering.** The reward path is
  untouched by TASK-221: `BattleResultService.cs:492` writes the battle result
  first (`firstDurableWrite`, `:511`) and only then applies the two independent
  progression grants — Player XP at `:518-523` and Pet XP at `:525-530`. Neither
  reads the other's values (`:505-510`), and neither is keyed on the collection.
- **The one materially new fact:** "the Pet" that receives XP is now one of five
  owned instances rather than the only one. The code already resolves it from the
  battle's own identity and not from "the Player's first Pet", so the behaviour is
  correct — but the blast radius of a composed-reward-path defect is larger (the
  wrong Pet could be progressed), and the correct-Pet property is now worth
  asserting. This **strengthens** TASK-216's corrected subject; it does not change
  its scope.
- **The old premise stays refuted.** TASK-213 §9's classification governs:
  "**Its old premise is refuted and must not be preserved.** 'Verify then fix only
  if confirmed' has nothing to confirm. The corrected subject is real." Nothing in
  TASK-221 revives it.
- **Recommended addition to TASK-216's scope (one case, not a new task):** assert
  that the composed battle-end reward path progresses **exactly the battle's
  active Pet** and leaves the other four owned Pets' `XP`/`Level` untouched. This
  is the TASK-221-induced coverage improvement, and it pairs naturally with the
  three-write non-atomicity report TASK-216 already owns.
- **Priority:** P2, unchanged. **Effort:** S–M. **Owner:** implementation agent +
  testing agent.

### 9.7 TASK-224 / worktree provenance (context, not a reassignment)

TASK-221 added 10 newly modified tracked files and 1 new untracked entry (this
audit record makes 17). The tree is now **48 modified tracked files + 16 untracked
entries, 0 deleted/renamed** (measured; `HEAD` = `afe5b14`), against TASK-223's
recorded base of 38 / 13. `tasks/backlog/TASK-224-commit-step-and-task-209-record.md`
remains **BACKLOG, blocked on the Product Owner decision** it names (TASK-223 §10
F-2). Its urgency rises with the tree; its content does not change because of
TASK-221. **This audit does not fix TASK-224 and does not commit anything.**

---

## 10. Priority Changes

| Task | Before TASK-221A | After TASK-221A | Change / reason |
|---|---|---|---|
| **TASK-221** | P1, in progress | **CLOSED — audited PASS** | Post-condition HOLDS (§1–§7). Recorded here rather than by editing the immutable record |
| **TASK-219A** | P1 (contract conflict) / P2 (player-visible) | **P1 — recommended next implementation task** | Priority unchanged but **ranked first**: the reachable Signature Skills went from 1 of 5 to **5 of 5**, so the mis-identification now affects every one the product can present. Only remaining wrong player-visible value on the primary battle surface |
| **TASK-218** | P2, parallel | **P2, parallel — value raised** | Coverage rises from 3 of 10 relic definitions to **10 of 10**. Same effort, same independence, no contract change. Genuinely parallel with TASK-219A |
| **TASK-212B** | DEFERRED (P3, off the MVP order) | **DEFERRED — unchanged** | The only Card-cost source count is still 1 (`relic-emergency-core`); promotion triggers (a)/(b)/(c) of `TASK-213:518-524` are all unmet. Do not re-enter the order; do not add `CardCostModifiers` |
| **TASK-215A** | P3 | **P3 — unchanged, scope note added** | No Passive/Tier/Star assumption is broken by five-Pet ownership; the fixture surface grows from 1 to 5 Pet instances, which makes the test easier and stronger. Add "the reward progresses exactly the battle's active Pet" |
| **TASK-216** | P2 (corrected) | **P2 — unchanged, scope note added** | No new persistence interaction; the active-Pet scoping is already correct and is now worth pinning. The refuted old premise stays refuted |
| **TASK-217B** | P3 (cross-reference / stale-claim sweep) | **P2 — scope grew by 4 newly relevant items (D-F1, D-F3, D-F6, O-F1) plus 1 pre-existing (D-F2)** | It is the natural owner of every drift item that lives in an editable `docs/` file |
| **TASK-217A** | P1 documentation | **P1 — unchanged, nothing added** | TASK-221 authored no stale code comment; the two comments TASK-213 §11.1 assigned remain open and unrelated |
| **TASK-217C** | P3 documentation | **P3 — unchanged, nothing added** | TASK-221 did not touch the Card-cost sentences; TASK-212B stays deferred |
| **NEW (proposed) TASK-225** | — | **P2 — new task** | A small corrective record for the immutable-record drift this audit proved false: R-F1, D-F4, D-F5, D-F7. Cannot be done by TASK-217A/B/C under `TASK_LIFECYCLE.md:218` |
| **NEW (recommended) coverage work** | — | **P2 — folds into existing tasks** | R-F2 and P-F2 (add 4-/5-Relic Lobby cases and a ten-Relic viewer case) and P-F3 (refresh the collection-viewer E2E expectations to 5/3/10) should be added to the testing/implementation tasks that already own those files, not created as a separate task |
| **E-F1** | — | **P2 decision (Product Owner, one line)** | Confirm in writing that the MVP content baseline is defined over **newly created accounts only**. No migration, no backfill, no top-up |
| **TASK-224** | BACKLOG, P1 process | **BACKLOG — unchanged, urgency raised** | The tree grew to 48 modified / 16 untracked; still blocked on its named Product Owner decision. Not touched here |

---

## 11. Recommended Next Task

```text
Recommended next implementation task:  TASK-219A
                                       (Signature Skill identification / protocol
                                        correction)
```

**Why TASK-219A first, and not TASK-218.**

```text
1. It is the highest-severity remaining player-visible defect. The primary battle
   surface renders a raw developer card id ("Card: card-inferno") for the Pet's
   Signature Skill and routes the cast through the wrong hub method
   (CardCast instead of PetSkillCast). That is a correctness defect, not a
   missing enhancement (BattleScene.ts:1156-1173, :1228-1234, :1250-1258;
   BattleEventPresenter.ts:445-463, :624-628).
2. TASK-221 multiplies its reach. The number of reachable Signature Skills went
   from 1 of 5 to 5 of 5 because all five Pets are now owned and the active Pet is
   a loadout choice; every one of them is mis-identified today.
3. The decision it needs is already recorded. TASK-213 §5 decided the
   identification source, the read member, the canonical cast path, and the
   required SIGNALR_PROTOCOL.md §4.3 item 13 correction. It is no longer a
   decision task and requires no further round.
4. Its contract change is authorised, not invented. TASK-213 §5 fixed
   GET /api/pets (§5.1) as the delivered source and explicitly forbade widening
   §5.3's membership. This audit widens nothing; it reports that the existing
   implementation violates the recorded rule.
5. It is independent of the documentation work, so TASK-217B / TASK-225 can run
   in parallel.
```

**Run in parallel (all independent):**

```text
TASK-218   Relic trigger presentation — P2, implementation-only, no contract
           change; value raised from 3 of 10 to 10 of 10 relic definitions.
           TASK-221's completion is the precondition that made its coverage
           complete, and it touches a different surface (RelicTriggered callouts)
           than TASK-219A.

TASK-217B  Documentation reconciliation sweep — P2 now, scope +4 items
           (D-F1, D-F3, D-F6, O-F1) +1 pre-existing (D-F2).

TASK-225   NEW, P2 — corrective record for the immutable-record drift
           (R-F1, D-F4, D-F5, D-F7). Cannot be folded into 217A/B/C.

(one line) Product Owner — confirm the MVP content baseline is defined over
           newly created accounts only (E-F1).

TASK-224   process precondition, unchanged content, urgency raised — the tree is
           at 48 modified + 17 untracked once this record exists.
```

**Recommended order of the tests/coverage additions** (fold into the tasks that
already own each file, per `AGENTS.md` §16 — do not silently expand a task's
scope): R-F2 (Lobby 4-/5-Relic cases) → P-F2 (viewer ten-Relic case) → P-F3
(refresh `collection-viewer-smoke.mjs` to 5/3/10) → R-F3 (E2E 4-/5-Relic submit) →
TASK-215A / TASK-216 active-Pet assertions.

---

## 12. Verification

### 12.1 Lightweight Git verification (read-only; nothing altered)

```text
git log --oneline -1              afe5b14  (unchanged — this audit committed nothing)
git branch --show-current         master
git diff --check                  PASS (exit 0)
git status --porcelain            48 modified tracked files + 16 untracked entries
                                  (0 deleted, 0 renamed)
                                  TASK-221's §8 provenance arithmetic re-verified
                                  exactly; TASK-223's base (38 / 13) plus this
                                  task's 10 newly modified files reconciles to 48
git diff --stat                   48 files changed, 11791 insertions(+), 1502 deletions(-)
```

Only `warning: LF will be replaced by CRLF` notices are emitted. **No file was
staged, committed, stashed, checked out, reset, cleaned, or moved by this audit.**

### 12.2 Targeted tests run by this audit (to prove findings, not to re-certify TASK-221)

```text
Backend — Application.Tests, filter PlayerStarterGrantFactoryTests | RelicLoadoutServiceTests
    dotnet test tests/backend/GameServer.Application.Tests/... --filter "..."
    RESULT: Passed! Failed 0, Passed 59, Skipped 0, Total 59 (52 ms)
    PROVES: 5 / 3 / 10 / 18-row grant composition and definition-resolution
            abort; the 3–5 validator bound with 4 and 5 accepted and
            0/1/2/6/7 rejected as CountOutOfRange (R-F2/R-F3's server half).

Backend — Api.Tests, filter AuthStarterOwnershipTests
    dotnet test tests/backend/GameServer.Api.Tests/... --filter "..."
    RESULT: Passed! Failed 0, Passed 10, Skipped 0, Total 10 (2 s)
    PROVES: O-1..O-4 exactly (5 Pets / 3 Basic Cards / 10 Relics / registration
            response shape); no second starter set on re-login (:51); every
            canonical Boss startable with a granted loadout (:268); the submitted
            Relic order is what the battle carries (:340); failed registration
            creates no ownership (:214).
    NOTE:   MSB3277 EntityFrameworkCore.Relational 10.0.4 vs 10.0.12 version
            warnings are emitted by the existing Api.Tests project reference graph.
            Pre-existing, unrelated to TASK-221, not a failure.

Frontend — vitest, full suite
    npx vitest run   (workdir src/frontend/client)
    RESULT: Test Files 21 passed (21); Tests 868 passed (868) — 5.11 s
    Reproduces TASK-221 §7's "vitest 868/868 (21 files)" claim exactly.
    LobbyScene.test.ts alone: 103 passed.

NOT run by this audit (out of scope, and no claim below rests on them):
    - the live-stack E2E harnesses (standalone-web-smoke.mjs,
      collection-viewer-smoke.mjs, battle-history-smoke.mjs): they need
      PostgreSQL + Redis + GameServer.Api + Vite + a headless browser. The E2E
      claims in §3/§4 are read from the harness *source*, and TASK-221 §7's live
      results are cited as TASK-221's evidence, not re-derived here.
    - the real-PostgreSQL suite (PlayerStarterOwnershipPostgresTests): needs the
      docker-compose PostgreSQL on :5433. TASK-221 §7 reports 9/9 passing against
      it; this audit did not re-run it and does not assert it.
```

### 12.3 Source-inspection method (per claim)

```text
Every headline claim in §2–§7 is derived from one of:
  (a) a direct read of the file and line cited, or
  (b) an exhaustive call-site search (ownership writers, CardCost declarations,
      "exactly 3" / "3 relics" / old-bootstrap wording, MAX_/slice(0, caps), and
      ownership-count assertions), or
  (c) an authoritative document quoted verbatim at its line.
TASK-221's own record was used only as evidence *about what TASK-221 did* (§7.1
D-3) and was independently checked against the diff, git status, and the test
runs above.
```

---

## 13. Explicit Out-of-Scope Items

```text
[✓] No production code was modified.
[✓] No test was modified.
[✓] No script was modified.
[✓] No documentation was modified or rewritten — including every drift item
    reported in §7/§8, which is reported, not fixed.
[✓] The 3–5 domain rule was NOT changed, reinterpreted, narrowed, or widened.
[✓] No API contract, request member, response member, event, hub method, Redis
    shape, or SignalR member was changed or widened.
[✓] No acquisition, unlock, drop, purchase, claim, reward-grant, or equip path
    was added.
[✓] No progression system, economy, currency, or random reward was added.
[✓] No migration, backfill, or conditional top-up was added — including for the
    existing-account case (§5), which is classified as a product/testing
    consideration instead.
[✓] TASK-212B was NOT implemented, and no CardCostModifiers or cost display was
    reintroduced (§9.1).
[✓] TASK-218 was NOT implemented (§9.2).
[✓] TASK-219A was NOT implemented (§9.3).
[✓] No Passive effect, Tier progression, or Star progression was implemented
    (§9.4).
[✓] TASK-216 was NOT implemented, and its refuted old premise was NOT revived
    (§9.6).
[✓] TASK-224 was NOT fixed; no commit step was created and nothing was committed
    (§9.7).
[✓] TASK-217A / TASK-217B / TASK-217C performed no work here; only newly relevant
    drift was identified and assigned (§9.5).
[✓] No commit, stage, stash, checkout, reset, clean, branch, tag, or history
    rewrite was performed.
[✓] No broad documentation cleanup was attempted.
[✓] No test was weakened, skipped, or modified to make a finding pass; the two
    suites run in §12.2 were executed unmodified.
```

---

```text
TASK-221A AUDIT COMPLETE

Decision: PASS

TASK-221 post-condition: HOLDS

Relic rule: 3–5

All 10 Relics reachable: YES

Ownership baseline: 5 Pets / 3 Basic Cards / 10 Relics / 5 Bosses

Recommended next task: TASK-219A

Implementation: NOT DONE
Production changes: NONE
Test changes: NONE
```
