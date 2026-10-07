# TASK-213 — Content Reachability & Progression Decision

```text
Task ID:            TASK-213
Type:               DECISION / SPECIFICATION (product decision record; no
                    gameplay, API, database, UI, contract, test, or fixture
                    change)
Status:             DONE (decision recorded; implementation NOT performed)
Risk:               LOW (one product-scope document amended additively; no
                    production, test, contract, schema, realtime, or fixture file
                    touched)
Priority:           HIGH (unblocks / re-orders TASK-212B, TASK-215, TASK-219)
Primary Agent:      review / product-decision
Evidence base:      current working tree (src/, tests/, docs/, tasks/)
                    + two independent read-only source inspections
                    (stale-comment classification; content-reachability baseline)
Model:              DeepSeek Harness agent
```

**Scope discipline.** This task decides and specifies. No production code, test,
migration, API contract, SignalR contract, database schema, fixture, or ADR was
modified. No acquisition API, currency, progression system, Passive effect,
Signature Skill implementation, Card cost, or Relic trigger presentation was
implemented. Nothing was committed, cleaned, staged, or reset. The only files
this task wrote are:

```text
tasks/completed/TASK-213-content-reachability-decision.md     (this record)
docs/00-overview/MVP_SCOPE.md                                 (amended — §1, §3,
                                                               version preamble)
```

`MVP_SCOPE.md` is amended because it is the repository's canonical IN / OUT /
FUTURE authority (`MVP_SCOPE.md` §4; `GAME_RULES.md` §19/§20) and it is the
document that owns the conflicts this task resolves. Without that amendment the
decision would rank below, and be overridden by, the documents it corrects
(`AGENTS.md` §2: GAME DESIGN > TECHNICAL DESIGN > ADR > Task > Code). The
amendment is **additive and minimal** — four edits, no existing
classification removed except the two §1 lines this task's decision falsifies.
Every other document (including the technical contracts that must eventually
change) is **not** touched by this task; the required changes are assigned in
§9/§11.

**Prior records are not edited.** Where this decision supersedes a *fact* stated
in a completed record (TASK-213 audit §7's starter-composition reading,
TASK-212A §6.4's boundary, TASK-200 Q-11's scope), the disagreement is reported
here and the completed record is left intact (`tasks/TASK_LIFECYCLE.md` §3).

**All `TASK-220+` / `TASK-221` / `TASK-223` / `TASK-217C` / `TASK-215A` /
`TASK-215B` ids below are proposals with reserved ids, not authorization.**

---

## 0. Verification Performed (evidence base)

```text
git log --oneline -1        afe5b14 (unchanged by this task; this task did NOT
                            commit)
git status --porcelain      37 modified tracked files + 12 untracked entries
                            BEFORE this task; 13 untracked once this record
                            exists. The 12 pre-existing untracked entries are 2
                            files added by TASK-212A-1 and 10 completed task
                            records.
git diff --check            PASS (exit 0), before and after this task's edits
git status --porcelain -- docs
                            only docs/00-overview/MVP_SCOPE.md is changed by this
                            task (45 insertions / 3 deletions)
MVP_SCOPE.md readability    the amended file was re-read end to end; no test,
                            script, or production file references MVP_SCOPE.md's
                            text (grep over src/, tests/, scripts/ -> only code
                            comments cite it), so the amendment cannot break a
                            test
Backend / client suites     NOT re-run: this task changes one product-scope
                            Markdown document and adds one task record. Nothing
                            under src/ or tests/ changed (verified by
                            `git status --porcelain`). No executable claim below
                            rests on a test run.
Independent inspections     (1) a stale-comment classification pass over
                            BattleStartService.cs / BattleStateService.cs
                            (§11); (2) a content-reachability baseline pass over
                            the starter-grant factory, every provisioning
                            migration, every ownership-writer call site, both
                            loadout services, and the Pet progression writers
                            (§2). Both were read-only and are cited per claim.
UNVERIFIED                  no live PostgreSQL / Redis instance was queried, so
                            "what a running database currently contains" is not
                            asserted; all counts below are migration-level and
                            code-level facts.
```

**Method.** Every headline claim in §2/§4/§5/§7/§8/§11 was re-derived from source
or from an authoritative document, not from a prior task record. Where a claim
rests on a prior record rather than on a fresh artefact, it is marked.

---

## 1. Executive Decision

```text
1. MVP reachability model    FULL MVP CONTENT SET OWNED AT ACCOUNT CREATION,
                             with NO post-creation acquisition. The deterministic
                             Player-creation starter grant IS the MVP content
                             grant (5 Pets / 3 Basic Cards / 10 Relics / 5 freely
                             selectable Bosses). Acquisition/unlock progression
                             is FUTURE and is NOT an MVP system.          §3
2. Card / Relic decision      The 3-Basic-Card loadout is intentionally FIXED
                             and acceptable — but by CONTENT, not by ownership
                             (exactly 3 Basic Cards exist against an exact-3
                             requirement). The Relic loadout becomes a real
                             build decision (any 3–5 of 10 owned).        §4
3. Signature Skill            IN (MVP). It is DERIVED, never owned. The defect
                             is the IDENTIFICATION SOURCE, not the feature; the
                             contract conflict is resolved and the amendment is
                             assigned.                                    §5
4. Pet Passive                DEFERRED — the EFFECT layer only. The charge /
                             threshold / reset mechanism and its presentation
                             stay IN. Tier is a fixed per-Pet value; Star
                             progression is DEFERRED.                     §6
5. Card cost / affordability  NOT REQUIRED for MVP. TASK-212B is explicitly
                             DEFERRED. The reachable slice (rejection-code
                             legibility) belongs to a presentation task.  §7
6. SignalR / API conflict     ONE authoritative rule established: the effective
                             cost is server-composed and is never derived,
                             read, or reconstructed client-side. §4 item 15 /
                             §4.3 item 15 govern; §3.2.20 item 2's contrary
                             sentence is superseded and its correction is
                             assigned (not performed).                    §8
7. Next task                  TASK-223 (commit / worktree ownership) as the
                             process precondition; TASK-221 (content-ownership
                             implementation) and TASK-218 (Relic callout) then
                             run in parallel.                             §9
```

**Why the reachability model is the *smallest* coherent answer, not a new
system.** MVP already documents itself as having exactly one acquisition event
(Player creation) and no acquisition system at all: `DATABASE.md` §2 says the
starter grant exists "so the battle-start flow … can be exercised without a
separate acquisition system" and classifies those rows as "MVP bootstrap, not
acquisition gameplay … They do not define or constrain future gameplay
acquisition systems". `MVP_SCOPE.md` §3 lists no acquisition mechanism, and
`ROADMAP.md` Phase 2/§2 allocates none. So this decision invents **no** mechanic,
**no** currency, **no** drop table, **no** RNG, **no** endpoint, **no** schema
change, and **no** rule: it changes only **which rows the existing bootstrap
creates**. What it fixes is that the bootstrap's *composition* (1 Pet / 3 Cards /
3 Relics, `DATABASE.md` §2.1) delivers a **forced permutation** instead of the
loadout decision that `GDD.md` §2 ("Choose Pet → Equip Cards → Equip Relics"),
`GDD.md` §1.3 pillar 2 ("Build Diversity"), `GDD.md` §6 ("Players can collect
multiple Pets") and `PET_RULES.md` §2.1 ("A player may own an arbitrary number of
Pets (collection), Relics, and Cards") all require.

**Rejected alternative, recorded.** *Keep the 1/3/3 grant and declare MVP's
ownable set to be exactly that.* Rejected because it would leave GDD pillar 2
unmet by construction, permanently remove TASK-212B's motivation (its only
`CardCost` source could never be equipped), reduce TASK-219's payoff to 1 of 5
Signature Skills, and make 11 ownership-capable definitions permanently
unreachable while `MVP_SCOPE.md` §1 declares them IN. If the Product Owner
prefers the rejected alternative, the binding consequences are: (a)
`MVP_SCOPE.md` §1/§3 revert to naming the starter set as the ownable set; (b)
GDD §2's "Choose Pet / Equip Relics" steps must be recorded as non-decisions for
MVP; and (c) **TASK-212B must be cancelled rather than deferred** (§7).

---

## 2. Current Reachability Baseline (verified)

Reachability levels are distinguished deliberately: *a provisioned definition
row* is not *owned content*, and *owned content* is not *selectable loadout
content*.

```text
EX   a definition row exists (provisioned by migration seed data)
RD   a client read can obtain it
SEL  the player can select it in the loadout
USE  the server accepts and resolves it in battle
OWN  an account can actually own it after account creation
```

### 2.1 Provisioned definitions — 28

| Class | Count | Ids | Evidence |
|---|---|---|---|
| Pets | 5 | `pet-xich-lang`, `pet-bach-ho`, `pet-huyen-quy`, `pet-thanh-xa`, `pet-son-hung` | `20260929152651:161,169,177`; `20261004055006:190,200` |
| Basic Cards | 3 | `card-heal`, `card-shield`, `card-power-charge` | `20260929152651:104,111,119` |
| Pet Skill Cards | 5 | `card-inferno`, `card-tidal-barrier`, `card-iron-fang`, `card-venomous-bloom`, `card-earthshaker` | `20260929152651:127,134,142`; `20261004055006:140,160` |
| Relics | 10 | Berserker Core, Mana Crystal, Assassin Eye, Emergency Core, Burning Curse, Combo Fang, Arcane Battery, Execution Mark, Cascade Core, Battle Instinct | `20260929152651:190,197,204,212`; `20261004153916:185,205,233,254,276,308` |
| Bosses | 5 | `boss-def-hoa-long`, `-thuy-ma`, `-moc-yeu`, `-son-thach-ve`, `-kim-loi-vuong` | `20260926151112:71,82,91`; `20261004094100:118,131` |
| **Total** | **28** | | no later migration adds or deletes a content row |

Ownership-capable definitions = 5 Pets + 3 Basic Cards + 10 Relics = **18**.
Bosses and Pet Skill Cards have no ownership relationship by design
(`CARD_RULES.md` §1 item 4; `DATABASE.md` §1 `Player 1─N Relic` / Boss has no
ownership table).

### 2.2 The single acquisition event (verified)

```text
POST /api/auth/register | POST /api/auth/login (new-account branch)
  → AuthController.cs:99-104 / :143-148
  → PlayerRepository.GetOrCreateForAccountAsync
      new-Player branch only — PlayerRepository.cs:33-39 (:38 early return
      for an existing Player); composeStarterGrant at :66
  → PlayerStarterGrantFactory.cs
      Pet     1 instance, pet-xich-lang                     :56, :187-207
      Cards   3 unlock rows, card-heal/-shield/-power-charge :69-74, :217-221
      Relics  3 instances, relic-berserker-core,
              relic-mana-crystal, relic-assassin-eye        :90-95, :232-238
  → one SaveChangesAsync = 7 ownership rows
```

`relic-emergency-core` is **provisioned and deliberately not granted**
(`PlayerStarterGrantFactory.cs:83-84`; `DATABASE.md` §2.1).

### 2.3 Ownership cannot grow — verified by exhaustive call-site search

| Method | Declared | Production call sites |
|---|---|---|
| `ICardRepository.AddUnlockAsync` | `ICardRepository.cs:48` | **0** |
| `ICardRepository.AddDefinitionAsync` | `ICardRepository.cs:38` | **0** (test doubles only) |
| `IPetRepository.AddAsync` | `IPetRepository.cs:42` | **0** |
| `IRelicRepository.AddAsync` | `IRelicRepository.cs:33` | **0** |
| `IRelicRepository.AddDefinitionAsync` | `IRelicRepository.cs:40` | **0** (test doubles only) |
| `IBattleResultRepository.AddAsync` | `IBattleResultRepository.cs:82` | 1 — `BattleResultService.cs:492`, writes a battle result and **no** ownership |

The documented REST surface is 9 routes (`API_CONTRACTS.md:146-162`) matching 9
`[Http*]` attributes; the hub exposes 6 methods (`BattleHub.cs:862,957,1060,1118,
1154,1416`). **No unlock, purchase, drop, claim, reward-grant, or equip endpoint
or hub method exists.**

### 2.4 Reachability matrix (baseline, before this decision)

| Class | EX | RD | SEL | USE | OWN | Owned today |
|---|---|---|---|---|---|---|
| Pets | ✅ 5 | ⚠️ owned only | ✅ 1-option choice | ✅ | ❌ | 1 of 5 |
| Basic Cards | ✅ 3 | ✅ | ⚠️ forced (all 3) | ✅ | ❌ after creation | 3 of 3 |
| Pet Skill Cards | ✅ 5 | ❌ never readable as definitions | ❌ (derived slot only) | ✅ (derived; castable) | N/A by design | 0 of 5 |
| Relics | ✅ 10 | ⚠️ owned only | ⚠️ forced (all 3, any order) | ✅ | ❌ after creation | 3 of 10 |
| Bosses | ✅ 5 | ❌ no endpoint; identity via battle-start only | ✅ 5 free choices | ✅ | N/A | N/A |
| Pet Passive **effects** | ✅ declared as summaries | ✅ id + progress | N/A | ❌ **inert** | N/A | N/A |

**11 of the 18 ownership-capable definitions are ungrantable**
(4 Pets + 7 Relics); 16 of 28 definitions are never owned
(11 + 5 by-design non-ownable Pet Skill Cards). The loadout is a forced
permutation (`API_CONTRACTS.md:356-423`: exactly 3 Basic Cards; 3–5 Relics;
exactly 1 Pet), and the only genuine decision is the Boss.

### 2.5 Loadout validation (verified, unchanged by this decision)

```text
Cards  exactly 3  CardLoadoutService.cs:50,120-124     CountInvalid
       owned     :136-159                              NotUnlocked
       Category == Basic :166-173                      NotBasic
       occurrences ≤ LoadoutCopyLimit (no default) :181-196  CopyLimitExceeded
       Signature Skill DERIVED, appended at index 3 :224,:231-232
                    (source PetDefinition.SignatureSkillCardId;
                     BattleStartService.cs:279,363)
Relics 3–5 inclusive  RelicLoadoutService.cs:48,55,113-118  CountOutOfRange
       pairwise-distinct instances :125-129,173-186     DuplicateInstance
       owned :137-146                                   NotOwned
       order preserved :155-160
Pet    exactly 1, owned  BattleStartService.cs:208-222      PET_NOT_OWNED
```

### 2.6 Pet progression today (verified)

```text
XP / Level   Pet.GrantBattleXp writes XP and Level only
             Pet.cs:340-353 (:348 XP, :352 Level); called
             BattleResultService.cs:527; persisted
             PetRepository.cs:128-129. Formula
             min(floor(XP/100)+1,50) at Pet.cs:289-301.
Tier / Star  CANNOT CHANGE. Both are `init`-only (Pet.cs:172,180); the only
             assignments in the repository are creation-time initializers
             (PlayerStarterGrantFactory.cs:202-203, PlayerRepository.cs:132-133).
             PetDefinition has no Tier/Star column (PetDefinition.cs:54-115);
             PetTier.cs:20-22 states "There is no Tier-up, Evolution, or Tier
             derivation rule in MVP".
Pet count    1 ownable. `new Pet` occurs only at PlayerStarterGrantFactory.cs:187
             and PlayerRepository.cs:127; no reward path inserts a Pet
             (PetRepository.cs:120-126); BattleResultService.cs:509-510 documents
             that the reward does not create one.
```

---

## 3. MVP Content Ownership / Acquisition Decision

**Decision: the whole MVP content set is owned by a newly created Player; MVP has
no post-creation acquisition system.** The `DATABASE.md` §2 starter grant changes
from "minimum bootstrap sufficient for battle start" to "the MVP content grant",
and its composition becomes **18 ownership rows** (5 Pet instances + 3 Card unlock
rows + 10 Relic instances), up from 7.

| Category | Initial ownership (account creation) | Acquisition method after creation | Unlock condition | Duplicates possible? | Loadout membership can change? | Persists across sessions? | Deterministic or random | MVP status | Can the player meaningfully change a build before Battle? |
|---|---|---|---|---|---|---|---|---|---|
| **Pets** | 1 owned `Pet` instance per each of the 5 MVP `PetDefinition`s (5 instances) | **NONE** | none — granted | No (one instance per definition in MVP) | **YES** — choose 1 of 5 as the active Pet; also switches Element, base stats, Passive identity/threshold and Signature Skill | Yes (`Pet` rows persist; XP/Level persist) | Deterministic | **REQUIRED** | **YES** |
| **Basic Cards** | all 3, as `PlayerUnlockedCard` unlock rows | **NONE** | none — granted | No (one unlock row per (`Player`, `CardDefinition`), ADR-012 item 9) | **NO** — exactly 3 required and exactly 3 exist, so only order permutes | Yes | Deterministic | **REQUIRED** | **NO** (a *content* limit, not a reachability limit — §4) |
| **Pet Skill / Signature Cards** | **0 owned.** DERIVED at battle start from the active Pet's `SignatureSkillCardId`; appended as the 4th `equippedCards` entry | N/A — never ownable by design | N/A | N/A (no instances exist at any time) | **YES indirectly** — the derived slot follows the chosen Pet, so 5 distinct Signature Skills are reachable via the Pet choice | N/A (derived per battle; no ownership row) | Deterministic | **REQUIRED** (§5 fixes identification) | **YES** (through the Pet choice) |
| **Relics** | 1 owned `Relic` instance per each of the 10 MVP `RelicDefinition`s (10 instances) | **NONE** | none — granted | The data model permits two instances of one definition, but MVP grants **exactly one instance per definition**, so no duplicates | **YES** — any 3–5 pairwise-distinct owned instances; C(10,3)+C(10,4)+C(10,5) = 372 combinations | Yes | Deterministic | **REQUIRED** | **YES** |
| **Pet Passive effects** | mechanism + presentation only (identity, progress, `PassiveCharged`/`PassiveTriggered`) | **NONE** | N/A — deferred | N/A | N/A | N/A | N/A | **DEFERRED** (§6) | **NO** (only the identity/threshold differs between Pets) |
| **Bosses** | no ownership relationship | **NONE** (no Boss acquisition exists or is needed) | none — all 5 always selectable | N/A | **YES** — choose 1 of 5 per battle | N/A (nothing to persist) | Deterministic | **REQUIRED** | **YES** (currently the only real choice) |
| **Any content beyond the above** (more Pets / Cards / Relics) | none | **NONE in MVP** | — | — | — | — | — | **FUTURE** (`MVP_SCOPE.md` §3) | N/A |

### 3.1 Product contract (binding statements)

```text
R-1  The MVP content grant is the existing deterministic Player-creation
     bootstrap path. No new mechanism, endpoint, currency, drop, RNG, or
     reward→content rule is introduced. (DATABASE.md §2 owns the composition;
     MVP_SCOPE.md §1 owns the scope.)
R-2  The grant is idempotent by construction and runs only on the new-Player
     branch; there are no conditional top-ups (DATABASE.md §2.3 unchanged).
R-3  An account's owned content set does not change after creation in MVP.
R-4  Acquisition/unlock progression after creation is FUTURE and requires a
     Rule Change (GAME_RULES.md §20) before implementation.
R-5  Pet Skill Cards remain non-ownable by design; the Signature Skill is a
     derived composition slot, never an unlock row (CARD_RULES.md §1 item 4,
     ADR-012 item 9). This decision does not make one ownable.
R-6  Which items exist is CONTENT; which are owned is OWNERSHIP. The Card slot's
     fixedness is content-driven and is not fixed by this decision.
```

### 3.2 Direct consequences that MUST be carried into the implementation task

```text
C-1  The loadout becomes a real decision for Pets and Relics, using only
     already-provisioned content. No new content is authored.
C-2  Every collection list surface must be able to present the larger owned set.
     LobbyScene.ts:107 `MAX_LIST_ROWS = 8` silently truncates and is sliced at
     :1274 (pets), :1298 (cards), :1329 (relics). 10 Relics and 5 Pets make that
     cap LIVE; it is a required part of the implementation task, not a
     nice-to-have (pre-existing finding A-18, previously latent).
C-3  `relic-emergency-core` becomes equippable, so the CardCost path becomes
     reachable in production for the first time. This is why §7 must be decided
     in the same record (§7 decides it: cost display is NOT MVP).
C-4  The tests/fixtures that pin the 1/3/3 composition are in scope for the
     implementation task: PlayerStarterGrantFactoryTests.cs,
     PlayerStarterOwnershipTests.cs, PlayerStarterOwnershipPostgresTests.cs,
     PlayerStarterOwnershipGuardTests.cs, PlayerMatchOrCreateTests.cs,
     PlayerPostgresConstraintTests.cs, Application.Tests/TestStarterGrants.cs,
     and the E2E journeys (standalone-web-smoke.mjs's `phase4.starterGrantLoaded`,
     collection-viewer-smoke.mjs, battle-history-smoke.mjs).
C-5  DATABASE.md §2.1/§2.2 must be updated with the implementation (a technical
     document follows the product decision; it does not decide it).
```

---

## 4. Card / Relic Decision

| Question | Decision | Basis |
|---|---|---|
| Is knowing the effect enough? | **YES for MVP.** TASK-212A-1 delivered each Card's and Relic's own structured content at the loadout decision point; `GDD.md:301-309` §17's operative clause ("what each Card/Relic changes") is met by that. | `MVP_SCOPE.md` §1; `GDD.md:303-306`; TASK-212A-1 §4 |
| Must the player be able to change **Cards**? | **NO — not in MVP.** | see below |
| Must the player be able to change **Relics**? | **YES — and this decision makes it possible** (any 3–5 of 10 owned instances). | `GDD.md:229-237` §10; `RELIC_RULES.md` §2.1 item 1 |
| Is the current 3-card loadout intentionally fixed? | **YES — intentionally and structurally fixed, by CONTENT.** `CARD_RULES.md` §1: "A battle loadout always contains exactly 3 Basic Cards + 1 Pet Skill Card (GDD §3)"; every provisioned `LoadoutCopyLimit` is 1 (`CARD_RULES.md` §1 item 5); and MVP defines **exactly 3** Basic Cards (`MVP_SCOPE.md:61`). 3 required ∩ 3 existing = a permutation. | `CARD_RULES.md` §1/§1 item 5; `MVP_SCOPE.md:61` |
| Is that acceptable for MVP? | **YES, recorded as an accepted MVP limitation** — the Card *slot* is not a build decision in MVP. It is a **content** limit, not a reachability limit: ownership of all three is already complete, so no acquisition mechanism could change it. "More Cards" is FUTURE (`MVP_SCOPE.md` §3). | §3 above; `MVP_SCOPE.md` §3 |
| If not fixed, what minimum acquisition/loadout mechanism is required? | **N/A — the loadout mechanism already exists and is sufficient** (`LoadoutCopyLimit` + the exact-3 rule support duplicates and subsets). The missing input is **content**, so no mechanism is required or authorized. | `CARD_RULES.md` §1 items 1-5; `CardLoadoutService.cs:181-196` |
| Is the Relic loadout intentionally fixed? | **NO — only accidentally so.** 10 definitions exist against a 3–5-of-owned requirement. §3 of this record removes the accident. | `RELIC_RULES.md` §2.1, §6 |
| Does the Relic change require content work? | **NO.** All 10 Relics are authored and provisioned; `RELIC_RULES.md` §6/§8.5 own them. Only ownership + presentation capacity change. | §2.1; `RELIC_RULES.md` §6 |

**Consequence for the Card slot.** No Card acquisition, no Card loadout change,
and no Card content is authorized. If the Product Owner later wants the Card slot
to be a decision, the smallest path is **one additional Basic Card definition**
(which is a Rule Change + `MVP_SCOPE.md` §3 movement), not an acquisition system.

**Consequence for the Relic slot — an accepted limitation, recorded.** With
`relic-emergency-core` equippable, its declared content is displayed
("CardCost 50% (Pet, Battle)") but the *resulting* card cost is not (§7). This is
a deliberate, recorded MVP limitation of numeric transparency, not a
comprehension gap and not a defect.

---

## 5. Signature Skill Decision

**Decision: A — Signature Skill is IN (MVP).** It is implemented
server-authoritatively today; the defect is the **identification source**, not the
feature. Deferring it (option B) would contradict `MVP_SCOPE.md:52`,
`GDD.md:121-122` §3 ("1 Pet Skill Card"), `GDD.md:200-212` §8, `CARD_RULES.md`
§4/§4.1 (all five Signature Skills authored with costs and effects), the
implemented derived loadout entry, and both working cast paths.

| Required definition | Decision |
|---|---|
| **How it is identified** | By the active Pet's **`PetDefinition.SignatureSkillCardId`** — an existing required FK to `CardDefinition` (`PET_RULES.md` §8; `DATABASE.md` §1) — resolved server-side and placed as the **4th (last) entry of `petState.equippedCards`** (`CardLoadoutService.cs:224,231-232`; `BattleStartService.cs:279,363`). The client's identification must come from a **delivered server datum**, never from a `Category` it can only learn by holding a Card registry it cannot populate. |
| **How it is obtained** | It is **never owned and never acquired.** It is derived at battle start from the chosen active Pet. It is never a `PlayerUnlockedCard` row and never a §5.3 member (`CARD_RULES.md` §1 item 4; ADR-012 item 9). |
| **Represented as a `CardDefinition`?** | **YES — already is.** All five exist as provisioned `CardDefinition` rows with `Category = PetSkill`, authored costs and effect arrays. What must change is **reachability of the definition to the client**, not the model. |
| **Which cast path is canonical?** | **`PetSkillCast(battleId, clientSequence)` is the canonical client request** for the Signature Skill entry: the server resolves the skill from the loadout and needs no identifier. **`CardCast(<signature cardId>)` remains protocol-conformant and must remain implemented** — `CARD_RULES.md` §6 fires `CardCast` "for every successful Basic Card or Pet Skill Card cast", `CARD_RULES.md` §3 item 2 places no category restriction on the casting path, and both hub methods reach the same `CardCastExecutor` and emit the same event order (`CardCastExecutorTests.cs:309-339`). The contract must state which path the **client** uses, and must stop implying the other is illegal. |
| **What the client may render** | The Skill's delivered **name** and, if the delivered member carries it, its `effectDefinition` through the existing shared formatter. It must **fail closed** to the raw `cardId` when the datum is absent (`BattleScene.ts:1231-1233` already does this) and must never guess a name. It must not hold a Card content registry (this makes `SIGNALR_PROTOCOL.md:1859-1860`'s claim *true* instead of false), must not compute cost/affordability/legality, and must not build a definition→content catalog. |
| **Which contract/document must be amended** | (1) **`SIGNALR_PROTOCOL.md` §4.3 item 13** — replace the `Category == PetSkill` identification rule with the delivered-datum rule, and correct the "no local registry" sentence (N-04). (2) **One read source**, decided here: **`API_CONTRACTS.md` §5.1 `GET /api/pets`** gains the Pet's Signature Skill identity (minimum: `cardId`, `name`, `category`; plus the §5.3 `effectDefinition` shape if the client is to state what it does). The audit's alternative — the **§3 battle-start response** — is an accepted fallback if the Product Owner prefers battle-scoped delivery. (3) **`API_CONTRACTS.md` §5.3's membership must NOT be widened**: making a `PetSkill` card an unlock row would contradict `CARD_RULES.md` §1 item 4 / ADR-012 item 9. |
| **What is NOT part of this decision** | The member's exact name/shape, the client's exact rendering, and the `CardCast`/`PetSkillCast` prose corrections are the **implementation task's** (TASK-219A / TASK-219B, §9). |

**No `MVP_SCOPE.md` amendment is required for this decision** — §1 already lists
"Signature Skill" as IN and this decision confirms it. The conflicts are both in
technical contracts, which this task is forbidden to change (§8, §9 assignment).

---

## 6. Pet Passive Decision

**Decision: B — the Passive EFFECT layer is DEFERRED and moves out of MVP.**
The **mechanism** (charge / threshold / reset / overflow) and the
**presentation** (identity, gauge, counter, reset suffix, both events) stay **IN**
and are already implemented. Tier is a **fixed per-Pet value**; **Star
progression is DEFERRED**.

### 6.1 Why deferral, not adoption

```text
1. An explicit Product Owner decision already defers it.
   TASK-191 Q-11  "Approve pet-passive magnitudes for Xích Lang and Sơn Hùng
                   (not authored anywhere)?"  -> APPROVED — REMAIN
                   UNIMPLEMENTED FOR MVP.
   TASK-200 Q-11  "Pet Passives — REMAIN UNIMPLEMENTED FOR MVP."
   The conflict is therefore not "should Passive be deferred?" but
   "MVP_SCOPE.md still says IN while the Product Owner already deferred it".
2. Adopting it would require inventing five magnitudes, which this task and
   AGENTS.md §7 forbid: PASSIVE_RULES.md:226-229 says the magnitudes "are balance
   values and live in config, not in this document"; no magnitude column exists in
   any migration (PetDefinition carries PassiveId + PassiveThreshold only); no
   config constant exists (grep passiveMagnitude|PassiveDamage|PassiveHeal|
   PassiveShield -> 0 matches); the only number anywhere is COMBAT_RULES.md:526's
   worked example (Bạch Hổ +10pp), which CARD_RULES.md:429 calls a configuration
   illustration, not an authored value.
3. All five effects are absent, while the four Boss passives that share the same
   mechanism ARE implemented — the structural asymmetry the audit recorded.
   BattleStateService.cs:1204-1240 charges progress and emits
   PassiveCharged/PassiveTriggered and applies no effect; :1607-1636 and
   :1641-1775 apply Boss effects.
4. Adopting option A would reverse a recorded Product Owner decision, which a
   decision record may not do on the Product Owner's behalf.
```

### 6.2 What the amendment records (binding)

```text
IN       Passive charge / threshold / reset contract (PASSIVE_RULES.md §2/§4/§5)
         Passive progress + identity delivery, PassiveCharged / PassiveTriggered
         (SIGNALR_PROTOCOL.md §3.2.16/§3.2.17)
         Pet Level progression (PET_RULES.md §5)
         Tier as a fixed per-Pet value (PET_RULES.md §3.4: MVP ships one Tier
         instance per Pet; PetTier.cs:20-22: no Tier-up rule exists)
DEFERRED Per-Pet Passive effect application (all five)
         Pet Star progression (PET_RULES.md §4 item 3: star-up costs, materials
         and curves are undefined Meta Progression concerns)
```

`MVP_SCOPE.md` §1 (Pets block) and §3 (FUTURE) are amended accordingly. **No
effect, magnitude, threshold, reset behavior, or `PassiveId` is authored,
changed, or implied by this decision.**

### 6.3 Future owner and required precondition

```text
TASK-215B (proposed, FUTURE, NOT AUTHORIZED)
  Scope    implement the five Pet Passive effects + Tier/Star progression
  Blocked  a Product Owner magnitude decision, authored in the owning document
           (PASSIVE_RULES.md §8) or in a documented config owner, for all five
           effects — plus a Star cost/curve rule if Star progression is adopted
  Also     MVP_SCOPE.md §1 must move the item back from §3 to §1 first
           (GAME_RULES.md §20 Rule Change Policy)
```

---

## 7. Card Cost / Affordability Decision

**Decision: cost/affordability is NOT REQUIRED for MVP. TASK-212B is explicitly
DEFERRED.**

```text
Is cost/affordability required for MVP?        NO
If yes, what content must become reachable?    N/A (recorded anyway: Emergency
                                               Core — which §3 of this record
                                               now makes equippable)
If no, TASK-212B disposition                    DEFERRED (not cancelled)
```

### 7.1 Reasoning

```text
1. Correctness is already guaranteed and is not at risk. The server composes
   EffectiveCardCost once, before validation, deduction and reporting
   (CARD_RULES.md §3.6 item 7; EffectiveCardCost.cs:59,87,96-109), and the cast
   path already returns four documented rejection codes
   (CardCastRejectionReason.cs:6-43,56-67; CardCastExecutor.cs:50-85;
   BattleHub.cs:1118-1145) that the client already renders
   (BattleScene.ts:1658-1661). Nothing about affordability is wrong today.
2. The Card slot is not a build decision in MVP (§4), so GDD §9's spend-vs-save
   tension has no alternative to weigh in the Card slot.
3. The only reachable player-facing gap is LEGIBILITY of an answer the server
   already gives: the four rejection codes are printed in machine language
   (N-06). That needs no contract change and is explicitly NOT TASK-212B. It is
   assigned to the player-facing-strings task (§9).
4. Exposing the authored PowerCost alone is DOCUMENTED AS MISLEADING: "Card Cost"
   in CARD_RULES.md §3 items 2 and 4 IS EffectiveCardCost (CARD_RULES.md:207-210),
   so a base-cost member compared against petState.power would be wrong whenever
   a modifier is active, and the client is forbidden to correct it
   (SIGNALR_PROTOCOL.md §4 item 15 / §4.3 item 15). This is TASK-212A §6.4's
   recorded exclusion and it stands.
5. Exposing the effective cost requires either delivering the modifier collection
   or a new server-composed member — a genuine wire expansion for one of ten
   Relics, in a game where Card costs are 0 and 20 out of a 100-capped Power pool.
   That is not the smallest coherent contract.
```

### 7.2 Accepted limitation and promotion trigger (recorded)

```text
Accepted MVP limitation
  A player may equip relic-emergency-core (once §3 is implemented) and read its
  declared effect ("CardCost 50% (Pet, Battle)") at the loadout screen, but the
  resulting numeric card cost is not shown anywhere. The player learns the cost
  by the server's own rejection answer. This satisfies GDD §17 ("what each
  Card/Relic changes") and does not satisfy GDD §9's numeric transparency. It is
  recorded here so it is a decision, not an oversight.

Promotion trigger (any one promotes cost display to an MVP requirement)
  (a) a second Card-cost source becomes reachable;
  (b) the Basic Card loadout becomes a real choice (a 4th Basic Card exists);
  (c) the Product Owner asks for an in-battle card price.
  On promotion, the first deliverable is NOT a definition read: per §8 the
  server must deliver the value it charged.
```

---

## 8. SignalR / API Contract Conflict Resolution

### 8.1 The conflict, restated exactly

```text
SIGNALR_PROTOCOL.md §3.2.20 item 2 (:1091-1100)
  "The event carries no Power cost member ... A client that must show the spent
   Cost reads the Card's definition; a client must not recompute it from the
   event."

SIGNALR_PROTOCOL.md §4 item 15 (:1524-1527)
  "... it must never compute, predict, or reconstruct a Card-cost modifier from
   events, from a `PowerChanged` delta, or from a Card's definition."

SIGNALR_PROTOCOL.md §4.3 item 15 (:1987-1992)
  "`power` also does not authorize a client-side cost, affordability, or
   cast-legality computation: the effective cost is composed server-side
   (CARD_RULES.md §3.6), CardCostModifiers[] remains undelivered (§4 item 15),
   and the server remains the only validator of a cast."
```

`§3.2.20 item 2` requires the client to read a datum **from the very source**
`§4 item 15` and `§4.3 item 15` forbid it to read — and the datum it would read is
not even the right value: the spent cost is `EffectiveCardCost`
(`CARD_RULES.md` §3.6 item 2: "a runtime value, not stored state"), which cannot
be recovered from the definition because it depends on an undelivered modifier
collection. This is an **intra-document** conflict, so `AGENTS.md` §2 precedence
cannot decide it; it is an `AGENTS.md` §4/§20 stop condition.

### 8.2 The single authoritative rule (DECIDED)

```text
TASK-213 R-Cost
  A Card cast's cost is composed server-side only. `EffectiveCardCost`
  (CARD_RULES.md §3.6) is a derived runtime value — not stored state, not a
  Persistent/Redis member, and not a Card definition value.
  NO CLIENT may derive, reconstruct, compute, predict, or read it — not from a
  Card's definition, not from a `PowerChanged` delta, not from any event, and not
  from any modifier collection. The server remains the only validator of a cast
  (GAME_RULES.md §18, ADR-001).
  If a spent/effective cost is ever shown to the player it MUST be a value the
  server itself delivers for that cast; displaying it still authorizes no
  client-side affordability or cast-legality judgment.

Governing text:  SIGNALR_PROTOCOL.md §4 item 15 and §4.3 item 15.
Superseded text: SIGNALR_PROTOCOL.md §3.2.20 item 2's sentence "A client that
                 must show the spent Cost reads the Card's definition".
```

### 8.3 Consequences

```text
1. No `powerCost` member is added to API_CONTRACTS.md §5.3 (TASK-212A §6.4's
   exclusion is confirmed and remains binding).
2. No member is added to `CardCast`, to the §4 push, or to any event by this
   decision. GAME_EVENTS.md §2's documented "Power cost paid" element stays as it
   is ("not a wire member" — GAME_EVENTS.md §2 item 3 already says so), so no
   GAME_EVENTS.md change is required for the rule itself.
3. The correction of §3.2.20 item 2 is a DOCUMENTATION-ONLY change to one
   document's wording and is assigned to TASK-217C (§9/§11). It applies this
   record's ruling; it does not re-decide it.
4. If cost display is ever promoted (§7.2), the deliverable is a
   server-composed cast-report value, which is a protocol change owned by its own
   task.
```

---

## 9. Revised Task Dependency / Order

The order below is the recommended order, not a restatement of the old numbering.
**Priority** P0 blocks the core loop · P1 major player-facing, documented-intent,
or contract-integrity · P2 important · P3 polish. **Kind** D = decision, I =
implementation, DOC = documentation-only, P = process.

| # | TASK | Priority | Depends on | Can run in parallel? | Implementation or decision? | Reason |
|---|---|---|---|---|---|---|
| 1 | **TASK-223** (proposed) — Commit / working-tree ownership | **P1 (process)** | none | **NO — must precede every implementation task** | **P + a small decision** (commit granularity / branch model) | 37 modified tracked files + 12 untracked entries (13 once this record exists), HEAD `afe5b14`, no task owns a commit step. A `checkout`/`stash`/`clean` would destroy the §5.3/§5.4 amendment and eleven records of verified work. §10. |
| 2 | **TASK-221** (proposed) — MVP content-ownership implementation (full 18-row grant + collection list capacity) | **P1** | TASK-213 (this record) + TASK-223 | **YES with #3 and #4** | **I** | The only change that turns the documented loop's Pet/Relic steps into real decisions, using only provisioned content. Includes C-2 (`MAX_LIST_ROWS = 8` truncation, now live) and C-4/C-5. §3. |
| 3 | **TASK-218** — Name the Pet's Relic triggers in battle | P2 | none | **YES (with #2)** | **I** | Implementation-only, no contract change, every battle; the client already holds `getRelics()`. Its value **rises** with 10 equippable Relics. Unchanged in substance from TASK-212/213 audits; explicitly independent. |
| 4 | **TASK-219A** — Apply the Signature Skill identification decision (SIGNALR §4.3 item 13 + one Pet read member) | **P1 as a contract conflict / P2 player-visible** | TASK-213 (decision made here) | **YES (with #2/#3)** | **I** — the decision is now made; no further decision round is required | The contradiction is resolved here, so this is no longer a decision task. Fixes a raw developer id on the primary battle surface, and (with #2) 5 Signature Skills become reachable. §5. |
| 5 | **TASK-219B** — Make the client harness model production; tighten the `phase6d` E2E check | P2 | TASK-219A | YES (with #2) | **I** (tests + E2E script) | The suite currently certifies behaviour production cannot reach (a synthetic `PetSkill` row) and the E2E accepts any acknowledgement. Verification integrity on the primary battle surface. |
| 6 | **TASK-217A** — Architecture / component / directory reconciliation **+ stale code comments** (N-10 and the additional sites in §11) | **P1 documentation** | none | YES | **DOC** | `AGENTS.md` §6 routes implementers to components that do not exist; two comments actively deny working code and could cause it to be deleted. Documentation-only in production terms. |
| 7 | **TASK-217C** (proposed) — Apply the Card-cost authority ruling (correct `SIGNALR_PROTOCOL.md` §3.2.20 item 2) | P3 | TASK-213 (ruling made here) | YES | **DOC** — applies a recorded decision, does not re-decide it | Leaving a superseded rule in the protocol makes every future reader follow it. One sentence. §8. |
| 8 | **TASK-217B** — Cross-reference / envelope / stale-claim sweep (+N-01; +A-11e wording) | P3 | none | YES | **DOC** | Must **not** absorb product decisions: the decisions it touches are owned by this record, TASK-215's residual, and TASK-217C. |
| 9 | **TASK-215A** (proposed, residual of TASK-215) — Pin the recorded Passive / Tier / Star deferral with a test (N-07) | P3 | TASK-213 (this record) | YES | **I** (test only, no gameplay) | The deferral must not silently become an implementation default. No effect is implemented. |
| 10 | **TASK-216** (corrected) — Assert the composed battle-end reward path persists Player XP; report the three-write non-atomicity | P2 | none | YES | **I** + one small atomicity decision | **Its old premise is refuted and must not be preserved.** "Verify then fix only if confirmed" has nothing to confirm. The corrected subject is real and protects the only reward the game grants. |
| 11 | **TASK-220** (+ A-12 / A-14 folds) — Player-facing strings, **including the rejection-code legibility slice (N-06)** | P2 | none | YES | **I** | N-06 is the *reachable* slice of the affordability concern and is **explicitly not TASK-212B**. It needs no contract change. §7. |
| — | **TASK-212B** — In-battle Card cost and affordability | **DEFERRED (P3, not on the MVP order)** | — | — | **I**, deferred; its **decision** is made here (§7/§8) | Not required for MVP. Its reachability blocker is cleared by #2 and its protocol blocker is resolved by §8, so it is now blocked by nothing but the Product Owner's willingness to expand the wire contract for one Relic's modifier. Re-enters on a §7.2 promotion trigger. |
| — | **TASK-215B** (proposed) — Pet Passive effects + Tier/Star progression | **FUTURE — NOT AUTHORIZED** | a Product Owner magnitude decision + `MVP_SCOPE.md` §1 movement | — | **I**, future | Blocked on unauthored values. §6.3. |
| — | **TASK-214** — Account progression read surface | P2, **unchanged by this decision** | none | YES | **I** (+ a small decision: a new read, or Battle History as its home) | Level/XP remains invisible outside a finished battle. Not a reachability question. |

### 9.1 Explicit classifications requested by the task statement

```text
TASK-212B   DEFERRED.  Decision made here (cost is not MVP; one authoritative
            cost rule established). Was blocked by two conditions; both are now
            cleared, yet it still does not enter the MVP order.
TASK-215    CLOSED as a decision by this record. Residual = TASK-215A (pin the
            deferral, P3). The effect-layer implementation moves to TASK-215B,
            which is FUTURE and NOT AUTHORIZED.
TASK-218    Implementation, unchanged, independent, "can run independently" —
            confirmed: no dependency on this decision, on TASK-221, or on any
            contract change. Runs in parallel with TASK-221.
TASK-219    Split and re-scoped: 219A is now an IMPLEMENTATION task (the decision
            is made here), 219B is a test/E2E integrity task. The "wrong action"
            half of its original premise stays refuted (TASK-213 audit §6.3).
TASK-216    Kept, CORRECTED. Its named ordering risk is refuted; the corrected
            subject (composed reward-path assertion + three-write non-atomicity)
            is not dropped, and the refuted premise is not preserved.
TASK-217    Split three ways: 217A (architecture + stale code comments, P1
            documentation), 217B (cross-reference/envelope sweep, P3), 217C (the
            Card-cost authority wording correction, P3). 217A/217B must not
            absorb any product decision; 217C applies one already made here.
NEW         TASK-221 (content-ownership implementation, P1) — required by §3.
            TASK-223 (commit/worktree ownership, P1 process) — required by §10.
            TASK-217C (P3 doc) — required by §8.
            TASK-215A (P3 test) / TASK-215B (FUTURE) — the residue of TASK-215.
```

---

## 10. Repository / Worktree Ownership Finding

```text
Finding (N-11, confirmed and re-measured by this task)
  HEAD                       afe5b14 ("feat: implement battle system, game scenes,
                             runtime, and API integration") — a single commit
  Modified tracked files     37
  Untracked entries          12 (13 once this record exists)
                             2 source/test files added by TASK-212A-1
                             10 completed task records
  Task owning a commit step  NONE. tasks/README.md and tasks/TASK_LIFECYCLE.md
                             define statuses and the completed/ lifecycle but
                             name no commit step; no completed record contains
                             one.
  Growth                      TASK-211 §2.5: 20 modified / 4 untracked;
                             TASK-212 §0: 26 / 6; TASK-212A: 26 / 7;
                             TASK-213 audit: 37 / 11; now: 37 / 12.
  Includes                    the whole API_CONTRACTS.md §5.3/§5.4 amendment,
                             2 new source files, 2 changed test files, and every
                             TASK-207 → TASK-213 record.
```

**Decision: a separate task IS needed, and it is the highest-priority *process*
item.** `TASK-223 (proposed) — Commit / working-tree ownership`, P1 (process,
zero player impact), S.

```text
TASK-223 minimal scope (not designed in detail here — that is its own job)
  a. Own the commit/integration step for the pending TASK-208 → TASK-213 chain.
  b. Decide and record the commit granularity (recommended: one commit per
     completed task, committed when the task is closed) and the branch model
     (the tree is on `master` with a single HEAD commit).
  c. Establish that every subsequently completed task record names the commit
     owner/step, so this condition cannot silently recur.
  d. Verify, before committing, that the tree matches what the records claim
     (37 modified + the untracked set), and that no record's claimed file list is
     contradicted by the diff.

NOT in TASK-223, and NOT in this record
  no commit, no stage, no checkout, no stash, no reset, no clean, no branch
  creation, no .gitignore change, no history rewrite.
```

**This task did nothing to the tree**: no `git add`, `git commit`, `git stash`,
`git checkout`, `git reset`, `git clean`, or `git rm`. §0 records the before/after
state and the fact that only the two files listed in the preamble were written.

---

## 11. Documentation Drift Ownership

### 11.1 §8 of the task statement — the two named stale comments

**Both are comment-level documentation drift. Neither is an implementation
defect.** Both are verified against current code and current documents.

| Site | Verbatim (abridged) | Classification | Evidence proving it stale | Owner |
|---|---|---|---|---|
| `src/backend/GameServer.Application/Battle/BattleStartService.cs:248-253` | "BOSS_RULES.md §6 defines exactly three content-defined MVP Bosses and BossDefinitions holds exactly those (BOSS_RULES.md §6.1 records the remaining two as 'not yet content-defined')" | **Misleading comment (moderate risk)** — documentation drift that could cause a wrong *action* (delete or "add" two Bosses). The code it describes is correct. | `BossDefinitions.cs:30-34` ("§6 defines exactly these five … §6.1's earlier 'two further … not yet content-defined' note was retired by TASK-172"); `BossDefinitions.cs:338-345` `All` = five; `BOSS_RULES.md:652` "All five … The set is complete at five"; `BOSS_RULES.md:32-35` retires the quoted note; `MVP_SCOPE.md:74` "5 Bosses"; `ResolveBoss` iterates `All` (`BattleStartService.cs:505-521`) | TASK-217A |
| `src/backend/GameServer.Application/Battle/BattleStateService.cs:1582-1584` | "Passive EFFECT application is out of this task's scope for every Boss (BOSS_RULES.md §3 item 3, §6.2): a trigger emits its event and applies nothing." | **Misleading comment (high risk)** — it sits directly above the code it denies. Documentation drift, **not** an implementation defect: the code matches `BOSS_RULES.md` §6.2. | The Boss branch applies effects: `BattleStateService.cs:1607-1626` (Hỏa Long +20% ATK / 3 turns via `StatusEffectLifecycle.Apply`), `:1627-1636` (Mộc Yêu 5% Max HP regen, clamped), `:1641-1775` (Sơn Thạch Vệ, Kim Lôi Vương), plus once-per-battle eligibility `:2662-2730`; Thủy Ma applied at creation (`BossState.cs:421-434`, consumed `HealResolution.cs:33-39`); `BOSS_RULES.md:652-658` "No Boss-specific value remains to be authored". Git: the sentence entered in `abef308`; the effects landed later (`9567da9`, `c3f1e01`) and the comment was never updated. **The Pet branch is the one that applies no effect** (`:1204-1240`) — which is §6's decision, not this comment's claim. | TASK-217A |

**The correct classification, stated as the decision:**

```text
Both sites are DOCUMENTATION DRIFT expressed as code comments.
Neither is an executable-comment DEFECT in the sense of "the code is wrong":
in both places the executable logic is correct and matches its owning document.
The second is the more dangerous kind — a comment that DENIES the behaviour
directly beneath it — classified as "misleading executable comment", because a
future implementer acting on it would delete working, documented behaviour.
Neither is an implementation defect. Neither is fixed here (AGENTS.md §16).
```

### 11.2 Additional stale sites found while verifying §11.1 (all comment- or doc-level)

| Id | Site | Claim | Verdict | Owner |
|---|---|---|---|---|
| A4 | `BattleStartService.cs:498-501` | "a lookup over the **three** transcribed definitions … §6 defines exactly those three" | Misleading comment (same failure mode as the first site above) | TASK-217A |
| A5 | `BattleStateService.cs:1576-1581` | quotes the retired §6.2 wording "Passive (always active)" and calls `0` "the Always-Active marker" | Documentation drift, harmless — `BOSS_RULES.md:103-104` records the wording was corrected to Battle Start (TASK-124); the numeric part (`?? 0`, `BossDefinition.cs:237`) and the `> 0` guard are accurate | TASK-217A |
| A6 | `BattleStartService.cs:70-72` | "(GAME_RULES.md §17 steps 18–19 remain unimplemented)" | Documentation drift — the parenthetical is false (`BattleStateService.cs:1544-1560,1563-1775`); the normative "this service must never implement gameplay" clause is still true | TASK-217A |
| A7 | `BattleStateService.cs:113`, `:161-167`, `:454`, `:461-463` | Pet / Relic / Boss "selection is not implemented" | Documentation drift — all three are implemented end to end (`BattleStartService.cs:208-243,258,276-338,356-398`) | TASK-217A |
| A8 | `BattleStartService.cs:31,60,157` | `<see cref="BattleStateService.CreateBattle"/>` | Broken `cref` (member is `CreateBattleAsync`, `BattleStateService.cs:481`) — not a factual drift claim | TASK-217A |
| N-10 doc side | `BOSS_RULES.md:722-728` | the two newest rows "are not yet authored or provisioned" | Documentation drift; internally contradicted by `BOSS_RULES.md:652-660` and factually by `BossDefinitions.cs:255-327` + migration `20261004094100:118,131`. **This is the likely origin of the §11.1 first comment**, so fix both in the same sweep. | TASK-217B |
| N-01 | `BattleHub.cs:1107-1110`, `CardCastRejectionReason.cs:26-30` | `CardCast` described as a "Basic Card" cast / "not a Basic Card" | Documentation drift: `CARD_RULES.md:487-491` and `SIGNALR_PROTOCOL.md:1103-1106,1156-1160` admit Basic **or** PetSkill, and the executor matches them | TASK-217B |
| N-04 | `SIGNALR_PROTOCOL.md:1859-1860` | "the client maintains no local registry and performs no card validation" | Documentation drift vs the implemented registry (`BattleScene.ts:382,1156-1173,1230`); the sentence becomes **true** once TASK-219A lands, so it is corrected inside the same item's rewrite | TASK-219A |
| N-05 | `SIGNALR_PROTOCOL.md:1091-1100` | "A client that must show the spent Cost reads the Card's definition" | Documentation drift that contradicts its own document twice; superseded by this record's §8.2 rule | TASK-217C |
| A-11e | `MVP_SCOPE.md:68` | "Trigger system (OnMatch, OnCombo, OnHpBelow, etc.)" | **Documentation/scope drift, decided here as wording, not as a missing trigger.** `OnMatch` **is** a defined trigger (`RELIC_RULES.md:476`, one of the closed 12), so the line is not wrong about the rule — but the runtime's evaluated trigger set is `OnMatchCount` / `OnCombo` / `OnHpBelow` (+ the other firing points), `OnMatch` is not evaluated, and **no provisioned Relic declares it**. The line is therefore accurate as prose and misleading as a description of what is reachable. This record does **not** decide whether `OnMatch` must be implemented: that is a separate question, flagged as **N-13** below. | TASK-217B (wording only) |
| Path drift | the TASK-213 task statement's "Required Reading" names `docs/01-product/GDD.md`, `MVP_SCOPE.md`, `PET_RULES.md`, … | **The directory `docs/01-product/` does not exist.** The real paths are `docs/00-overview/` (GDD, MVP_SCOPE, ROADMAP) and `docs/01-game-design/` (the eight domain rule documents). The statement also names `tasks/completed/TASK-212-audit.md`, whose real file is `TASK-212-post-task-211-product-audit.md`. | Reported only — the task statement is not a repository artefact, so no repository document is wrong. **TASK-217B must not chase this.** | none (report) |
| N-13 (new, carried — **not decided here**) | `RELIC_RULES.md:476` `OnMatch` vs `RelicFiringPoint.cs:11,50` / `RelicResolver.cs:49,57,61` | `OnMatch` is a documented MVP trigger (one of the closed 12) that the runtime does not evaluate. Latent: no provisioned Relic declares it, so no player-visible behaviour is missing today. | **Out of this task's scope** — it is an implementation-coverage question about a trigger, not a content-reachability question. NOT decided and NOT assigned an implementation task here. If the Product Owner confirms `OnMatch` must be IN, it needs its own verification/implementation task; until then the correct action is the TASK-217B wording fix only. | TASK-217B (wording); implementation undecided |

### 11.3 What this task does NOT do to any document

```text
not fixed   every site in §11.1 and §11.2
not changed API_CONTRACTS.md, SIGNALR_PROTOCOL.md, GAME_STATE.md, DATABASE.md,
            CARD_RULES.md, PASSIVE_RULES.md, PET_RULES.md, RELIC_RULES.md,
            BOSS_RULES.md, GDD.md, ROADMAP.md, any ADR, any migration, any
            fixture, any test, any source file
not edited  every prior completed task record
changed     docs/00-overview/MVP_SCOPE.md only (§1, §3, version preamble —
            the decision record itself)
```

---

## 12. Explicit Non-Goals

This task did **not**, and the diff proves it did not:

```text
modify production code                      NOT DONE  (0 files under src/)
modify tests                                NOT DONE  (0 files under tests/)
modify API contracts                        NOT DONE  (API_CONTRACTS.md untouched)
modify SignalR contracts                    NOT DONE  (SIGNALR_PROTOCOL.md
                                                      untouched)
modify database schema / migrations         NOT DONE
add acquisition APIs / endpoints            NOT DONE
add currencies or an economy                NOT DONE
add a progression / unlock system           NOT DONE
implement Passive effects                   NOT DONE  (any of the five)
implement Signature Skill                   NOT DONE
implement Card cost / affordability         NOT DONE
implement Relic trigger presentation        NOT DONE  (TASK-218 remains open)
alter loadout behavior                      NOT DONE
modify fixtures                             NOT DONE
author any magnitude, threshold, cost, or   NOT DONE
  curve
change Tier/Star/Level rules                NOT DONE
commit, stage, stash, checkout, reset,      NOT DONE
  clean, branch, or rm anything
clean or reset the working tree             NOT DONE
fix TASK-217 documentation drift            NOT DONE  (reported and assigned,
                                                      §11)
fix the stale comments                      NOT DONE  (classified and assigned,
                                                      §11.1)
edit a completed task record                NOT DONE  (disagreements reported)
```

Deliberate omissions, recorded so they are not read as oversights:

```text
1. `DATABASE.md` §2.1/§2.2 is NOT amended. It is a TECHNICAL document; the
   product decision is made in MVP_SCOPE.md (a GAME DESIGN document, which
   outranks it per AGENTS.md §2) and the technical amendment is implementation
   work assigned to TASK-221 (C-5).
2. `GDD.md` is NOT amended. Its §17 comprehension requirement remains satisfied
   by the delivered structured content and by the Passive's progress/identity
   presentation; its §15 delegates scope to MVP_SCOPE.md, which is where the
   deferrals are recorded.
3. `PASSIVE_RULES.md`, `PET_RULES.md` and `CARD_RULES.md` are NOT amended: this
   decision authors no rule value, and each already states the fact the decision
   relies on (PASSIVE_RULES §8's magnitudes-live-in-config clause; PET_RULES
   §3.4/§4 item 3; CARD_RULES §1/§4).
4. The `MVP_SCOPE.md` amendment is additive and does not restate any rule owned
   by another document (no threshold, magnitude, cost, formula, or trigger list).
5. TASK-212B is DEFERRED, not cancelled, because the rejected alternative (§1)
   would require its cancellation; a deferred task is reversible if the Product
   Owner overturns §3.
```

---

## 13. Verification

```text
What was verified, and how
  1. Repository state           git log --oneline -1 -> afe5b14;
                                git status --porcelain -> 37 modified tracked +
                                12 untracked (13 with this record); git diff
                                --check -> PASS (exit 0) before and after.
  2. Amendment scope            git status --porcelain -- docs -> only
                                docs/00-overview/MVP_SCOPE.md changed by this
                                task; git diff --stat -> 45 insertions /
                                3 deletions; the full diff was read and contains
                                exactly the four intended edits.
  3. Amendment safety           no test, script, or source file reads
                                MVP_SCOPE.md's text (grep over src/, tests/,
                                scripts/ -> only code comments cite it), so the
                                amendment cannot alter a test outcome. No test
                                suite was therefore re-run.
  4. Baseline: content          every provisioning migration read; 28
                                definitions counted with file:line anchors;
                                18 ownership-capable definitions identified.
  5. Baseline: acquisition      every ownership-writer call site searched across
                                src/ and tests/; 0 production callers for all
                                five add/unlock methods; the 9-route REST table
                                and 6 hub methods checked for any granting path.
  6. Baseline: loadout          CardLoadoutService.cs, RelicLoadoutService.cs and
                                BattleStartService.cs read directly; the exact-3,
                                Basic-only, copy-limit, 3-5-relic, distinct-instance,
                                ownership and derived-Signature-Skill rules
                                confirmed with line numbers.
  7. Baseline: progression      Pet.GrantBattleXp and every Tier/Star writer
                                searched; Tier/Star confirmed init-only and
                                never changed by gameplay; single-ownable-Pet
                                confirmed by `new Pet` call-site search.
  8. Documents                  MVP_SCOPE.md, GDD.md, PET_RULES.md,
                                PASSIVE_RULES.md, CARD_RULES.md, RELIC_RULES.md,
                                ROADMAP.md, GAME_RULES.md §19/§20/§21,
                                DATABASE.md §2, API_CONTRACTS.md §5.1/§5.3,
                                SIGNALR_PROTOCOL.md §3.2.20/§4 item 15/§4.3
                                item 13/§4.3 item 15, GAME_EVENTS.md §2 read
                                directly.
  9. Stale comments             both named sites and seven further sites verified
                                against source, documents and git history (§11).
 10. Dependencies               each ordered task's dependency was traced to a
                                named artefact, not to a prior record.

Evidence limitations
  1. No live PostgreSQL or Redis instance was queried. All content counts are
     migration-level and code-level facts; "what a running database currently
     contains" (including a hand-inserted ownership row) is UNVERIFIED.
  2. No test suite was executed. This task changed one Markdown product document
     and added one task record; no executable behaviour changed,
     `git status --porcelain` proves no source/test file was touched, and no test
     reads MVP_SCOPE.md.
  3. The reachability matrix is derived from code paths, not from a live account
     playthrough. The TASK-213 audit's live E2E observation
     (`phase4.starterGrantLoaded — {"pets":1,"cards":3,"relics":3}`) is cited as
     corroboration, not re-derived.
  4. The §3 decision's *balance* implications (e.g. five Pets and ten Relics from
     the first battle) were not assessed: this task defines the product contract,
     not a balance pass.
```

---

```text
TASK-213 DECISION COMPLETE

Decision: APPROVED

MVP Reachability Model: The full MVP content set (5 Pets, 3 Basic Cards,
10 Relics, 5 always-selectable Bosses) is owned by a newly created Player via
the existing deterministic Player-creation starter grant; MVP has NO
post-creation acquisition/unlock/progression system, and acquisition is FUTURE.
The Card slot stays a forced permutation by CONTENT (exactly 3 Basic Cards
against an exact-3 requirement), not by ownership. Signature Skill stays IN and
derived.

Passive: DEFERRED (the EFFECT layer only — mechanism, progress and presentation
remain IN; Tier is a fixed per-Pet value; Star progression is DEFERRED)

Signature Skill: IN

Card Cost: DEFERRED (not required for MVP; TASK-212B deferred; one authoritative
server-side cost rule established, and the reachable rejection-legibility slice
moves to the player-facing-strings task)

Recommended next task: TASK-223 (commit / working-tree ownership) — the process
precondition; TASK-221 (content-ownership implementation) and TASK-218 (Relic
trigger callout) follow in parallel

Implementation: NOT DONE
Production changes: NONE
Test changes: NONE
```
