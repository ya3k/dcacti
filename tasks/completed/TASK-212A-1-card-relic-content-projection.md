# TASK-212A-1 — Expose Card & Relic Effect Content

```text
Task ID:            TASK-212A-1
Type:               Contract amendment + backend projection + client
                    presentation (implementation)
Status:             DONE
Risk:               LOW (additive read-contract members, no schema, no realtime,
                    no gameplay, no membership change)
Priority:           P1
Primary Agent:      implementation
Parent:             TASK-212A (content-contract audit; decision AMEND BOTH, §6/§7)
Evidence base:      current working tree (src/, tests/, docs/, tasks/)
Model:              DeepSeek Harness agent
```

**Scope discipline.** This task implements TASK-212A §6/§7 and nothing else. The
contract widening, the backend projection, the client models, the two content
surfaces, their tests, and the one authoritative document the decision names are
the whole change. TASK-212B (cost/affordability), TASK-213 (acquisition),
TASK-217 (documentation sweep), TASK-218 (in-battle Relic callout), TASK-219
(Signature Skill) are **not** touched. All pre-existing working-tree changes
(the 26 modified tracked files recorded by TASK-212A §0) were left as they were
found.

---

## 1. Approved contract decision

```text
TASK-212A §6: AMEND BOTH
  §5.3  GET /api/cards   + effectDefinition
  §5.4  GET /api/relics  + trigger, + condition (nullable), + effectDefinition
```

The amendment is **additive and projection-only**. It exposes the structured
content the definitions already author, store, and load — the Card's and the
Relic's own effect declarations — and adds **no** new vocabulary, **no**
server-composed prose, **no** cost/affordability/legality member, **no** realtime
member, **no** schema, index, query, repository, or Domain change, and **no**
change to either endpoint's membership semantics.

`TASK-212A-1` is the sanctioned child ID (TASK-212A §7); the audit's optional
split (contract+backend / client) was not enforced, so this one task carries
both halves.

---

## 2. Exact response changes

### 2.1 `GET /api/cards` (`API_CONTRACTS.md` §5.3)

`+ effectDefinition` — the Card's own structured effect rule, one object per
effect, in stored order.

```json
[
  {
    "cardId": "card-heal",
    "name": "Heal",
    "category": "Basic",
    "effectDefinition": [
      { "effectType": "Heal", "valueType": "PercentMaxHp", "value": 20 }
    ]
  }
]
```

Measured against the running stack (raw body, TASK-212A-1's own dev account):

```json
[{"cardId":"card-heal","name":"Heal","category":"Basic","effectDefinition":[{"effectType":"Heal","valueType":"PercentMaxHp","value":20}]},{"cardId":"card-shield","name":"Shield","category":"Basic","effectDefinition":[{"effectType":"Shield","valueType":"PercentMaxHp","value":20}]},{"cardId":"card-power-charge","name":"Power Charge","category":"Basic","effectDefinition":[{"effectType":"Power","valueType":"Flat","value":25}]}]
```

Element members and their present-iff semantics (`DATABASE.md` §1/§3, unchanged
and restated nowhere):

```text
effectType  string  always present
valueType   string  always present
value       int     present iff valueType interprets one; ABSENT (not 0, not
                    null) for "Undetermined"
duration    int     present iff effectType = "Burn"; absent otherwise
scope       string  present iff effectType = "Crit"; absent otherwise
```

Present, never null, never empty: `DATABASE.md` §1 stores the column NOT NULL
and every Card states at least one effect (TASK-111 D-1b's array shape).

### 2.2 `GET /api/relics` (`API_CONTRACTS.md` §5.4)

`+ trigger`, `+ condition` (nullable), `+ effectDefinition`.

```json
[
  {
    "relicId": "relic-instance-1",
    "name": "Berserker Core",
    "trigger": "OnMatchCount",
    "condition": { "conditionType": "MatchCountAtLeast", "threshold": 3 },
    "effectDefinition": [
      { "effectType": "ATK", "valueType": "Percentage", "value": 5,
        "target": "Pet", "lifetime": "Battle" }
    ]
  }
]
```

Measured against the running stack:

```json
{"relicId":"relicinst_318ee1b8b28b43bea2a4836015a0ebb4","name":"Berserker Core","trigger":"OnMatchCount","condition":{"conditionType":"MatchCountAtLeast","threshold":3},"effectDefinition":[{"effectType":"ATK","valueType":"Percentage","value":5,"target":"Pet","lifetime":"Battle"}]}
```

Member semantics:

```text
trigger                     string   always present, never null
condition                   object?  NULLABLE — emitted as an explicit `null`
                                     when the Relic declares no extra condition
                                     (RELIC_RULES.md §8.1 item 4). The member is
                                     always written, so its presence is fixed
                                     and exactly assertable and `null` cannot be
                                     confused with an absent member.
condition.conditionType     string   always present when condition is not null
condition.threshold         int      always present when condition is not null
effectDefinition[].effectType    string  always present
effectDefinition[].valueType     string  always present
effectDefinition[].value            int  present iff valueType interprets one;
                                         absent — never 0, never null — for
                                         "Undetermined" (RELIC_RULES.md §8.2
                                         item 3)
effectDefinition[].target        string  always present
effectDefinition[].lifetime      string  always present
```

The content is delivered **per owned instance**, exactly as `name` already was:
two owned copies of one definition repeat the content and remain two elements
with distinct `relicId`s. `definitionId` is deliberately still not exposed.

### 2.3 Unchanged

Routes, statuses, envelopes, ordering (§5.5), authentication (§2.8), the
empty-collection `200 []` answer, and both endpoints' membership semantics
(`GET /api/cards` is still the unlock set; a `PetSkill` `CardDefinition` is still
never an unlock row). No `unlocked`, `playerId`, `powerCost`, `loadoutCopyLimit`,
`definitionId`, `acquiredAt`, cost, affordability, legality, or §5.6
equip/loadout member was added. No SignalR, `GAME_STATE.md`, `GAME_EVENTS.md`,
`REDIS_STATE.md`, `DATABASE.md`, migration, or ADR change.

---

## 3. Source-of-truth mapping

```text
wire value                            source of truth (unchanged, referenced)
cardId / name / category              CardDefinition.CardDefinitionId / .Name /
                                      .Category
effectDefinition                      CardDefinition.EffectDefinition
  .effectType / .valueType / .value     DATABASE.md §1 item 1 / §3 (member names
                                        and present-iff rules); CARD_RULES.md §2 /
                                        §4.1 (identities and magnitudes)
  .duration / .scope                    DATABASE.md §3; CARD_RULES.md §4.1

relicId                               Relic.RelicInstanceId
name / trigger / condition /
  effectDefinition                    RelicDefinition.Name / .Trigger /
                                      .Condition / .EffectDefinition
  condition.conditionType / .threshold  RELIC_RULES.md §8.1; DATABASE.md §1
  effectDefinition[].effectType /
    .valueType / .value                 RELIC_RULES.md §8.2; DATABASE.md §1
  effectDefinition[].target /
    .lifetime                           RELIC_RULES.md §8.3
```

| Layer | File | What changed |
|---|---|---|
| Persistence | `CardRepository.cs`, `RelicRepository.cs` | **nothing** — both already loaded the definition (`ListUnlockedAsync` joins `PlayerUnlockedCard → CardDefinition`; `ListDefinitionsAsync` returns the whole `RelicDefinition` row) |
| Domain | `CardDefinition.cs`, `CardEffectDefinition.cs`, `RelicDefinition.cs`, `RelicCondition.cs`, `RelicEffectDefinition.cs` | **nothing** |
| Schema / migrations | — | **nothing** — no column, index, join, or migration |
| Application read model | `CardCollectionItem.cs`, `RelicCollectionItem.cs` | the definition value is now carried as a member |
| Application projection | `CollectionQueryService.cs` | a pure field copy; the dictionary keyed by definition id now holds the definition instead of its name |
| Api response | `CollectionResponses.cs` | one new member on `CardResponse`, three on `RelicResponse`, plus three response records for the nested shapes |
| Api controller docs | `CollectionController.cs` | doc comments only |
| Contract document | `docs/02-technical/API_CONTRACTS.md` | §5.3 / §5.4 amended, version preamble 1.18 |

The projection computes nothing: no magnitude is resolved, no proportion is
applied to a Max HP, no cost is derived, no optional member is defaulted, and no
prose is composed from a name, an id, or a category.

---

## 4. Frontend presentation changes

```text
API                        (GET /api/cards · GET /api/relics)
 ↓
ApiService                  (unchanged — pure pass-through of the typed arrays)
 ↓
GameRuntime                 (unchanged — pure pass-through)
 ↓
LobbyScene / CollectionViewerScene
 ↓
presentation/ContentEffectFormat   (the one formatter both surfaces use)
```

No `fetch()` from a scene, no new runtime store, no new SignalR subscription or
event, no caching in a scene, no second Card/Relic model for the same endpoint.

### 4.1 The shared formatter — `presentation/ContentEffectFormat.ts` (new)

A plain module of functions (`RewardSummaryFormat.ts`'s precedent), not a class,
service, registry, or store. It consumes only authoritative fields and is
**presentation only**:

```text
Heal 20% Max HP
Shield 20% Max HP
Power 25
Damage 100, Burn 50 (2 turns)
Crit 10 percentage points (NextAttack)
OnMatchCount MatchCountAtLeast 3 | ATK 5% (Pet, Battle)
OnBattleStart | BurnDamage 30% (Pet, Battle)          (nullable condition: no segment)
```

Rules it implements:

* **Preserves identity and magnitude.** `effectType` is printed as its own token
  and `value` as its own number; a value-interpretation token is translated only
  into a unit (`% Max HP`, `%`, `percentage points`) so the number is never read
  as the wrong quantity. `duration` and `scope` are printed when, and only when,
  the payload carries them.
* **Handles known effect types** through two small token→unit tables — the only
  translation in the module.
* **Safe fallback for unknown future types.** An unrecognized `effectType` is
  printed verbatim; an unrecognized `valueType` is printed verbatim beside its
  magnitude instead of being given an assumed unit; an unrecognized
  `conditionType` is printed verbatim with its threshold. An element with no
  effect identity at all is omitted.
* **Never infers mechanics.** Nothing is resolved, computed, or interpolated: no
  `PercentMaxHp` is applied to a Pet's Max HP, no `Damage` element becomes a dealt
  amount, no lifetime/threshold/duration is derived, and no `0`/`NaN`/`undefined`
  can reach the text (pinned by test).
* **Is not a content catalog.** No per-Card, per-Relic, per-definition, or per-id
  table and no hardcoded magnitude (pinned against the module's own source).

### 4.2 Lobby loadout rows (`LobbyScene.ts`)

Each Card and Relic row now draws a **second line** carrying that option's own
delivered content, at `y + 15`, in 12 px monospace, inside the existing 30 px row
pitch:

```text
○ Heal  [Basic]              ○ Berserker Core  slot 1
  Heal 20% Max HP              OnMatchCount MatchCountAtLeast 3 | ATK 5% (Pet, Battle)
```

* The identity line is unchanged, so the change is additive; the Pet and Boss
  rows draw no second line (their contracts carry no such member).
* Both lines are the same hit area — activating the content line runs the same
  selection toggle, so existing selection semantics are unchanged.
* Row ordering, slot numbering, the equipped marker, the review block, the
  start/retry/back controls, and preserved-loadout restore are untouched.
* Measured geometry (real stack): content lines at `y` 123/153/183 in a column
  whose rows are at 108/138/168; the widest provisioned Relic line ends at
  x = 1243 inside the safe-area edge x = 1256, and the line box clears the next
  row by 15 px. The line word-wraps at its column's own width, so an over-long
  future rule wraps instead of running under the neighbouring column.
* The scene holds no token table and no per-id lookup: it calls the shared
  formatter and draws what comes back.

### 4.3 Collection viewer detail panel (`CollectionViewerScene.ts`)

The Card and Relic panels now include the same content, rendered by the same
formatter, so the two surfaces cannot describe one Card or Relic two ways:

```text
ITEM DETAIL                              ITEM DETAIL

Card: Shield                             Relic: Mana Crystal
Category: Basic                          Changes: OnMatchCount MatchCountAtLeast 4 | Power 10 (Pet, Immediate)
Effect: Shield 20% Max HP                Relic ID: relic-instance-2
Card ID: card-shield
```

The list rows, tabs, navigation, reload, and teardown are unchanged; the panel
omits a content line when the response carried none. No `Effect:`/`Changes:` line
is ever drawn empty or fabricated.

No site in this task was identified as historical/documentation-only, so none was
left untouched on that ground.

---

## 5. Tests

### 5.1 Backend

`tests/backend/GameServer.Api.Tests/CollectionEndpointTests.cs`

```text
updated  Cards_ForTheOwner_ShouldReturnExactlyTheFourDocumentedMembers
           member set { cardId, category, effectDefinition, name } + the stored
           element's own members and values
updated  Cards_ShouldExposeNoExcludedColumn
           effectDefinition removed from the forbidden list and asserted
           positively; playerId / powerCost / loadoutCopyLimit / unlocked /
           §5.6 equip members still forbidden by name
added    Cards_ShouldProjectEveryEffectOfAMultiEffectDefinition
           two elements in stored order; Burn carries duration and no scope
added    Cards_ShouldCarryScopeOnACritElementAndNoDuration
added    Cards_ShouldEmitAnUndeterminedElementWithoutAValueMember
added    Cards_ShouldCarryNoCostOrAffordabilityMember
updated  Relics_ForTheOwner_ShouldReturnExactlyTheFiveDocumentedMembers
           member set { condition, effectDefinition, name, relicId, trigger }
           with the condition object and the effect element asserted value by value
added    Relics_WithNoCondition_ShouldEmitAnExplicitNull
added    Relics_ShouldEmitEveryDocumentedConditionForm (3 forms)
updated  Relics_ShouldExposeNoExcludedMember
           trigger / condition / effectDefinition asserted positively; playerId /
           acquiredAt / definitionId / relicDefinitionId / §5.6 equip members
           still forbidden, and the definition identity and owner identity are
           asserted absent at any depth
```

`tests/backend/GameServer.Application.Tests/CollectionQueryServiceTests.cs`

```text
updated  ListCards_ShouldProjectExactlyTheFourDocumentedMembers
updated  ListCards_ShouldExposeNoExcludedColumn
added    ListCards_ShouldCarryAMultiEffectDefinitionUnchanged
added    ListCards_ShouldNotExposeCostLegalityOrAffordability
updated  ListRelics_ShouldProjectExactlyTheFiveDocumentedMembers
added    ListRelics_ShouldCarryTheStructuredConditionWhenTheDefinitionHasOne
updated  ListRelics_ShouldCarryTheInstanceIdentityAndTheDefinitionName
updated  ListRelics_ShouldExposeNoExcludedMember
```

Membership semantics are re-asserted unchanged (`ListCards_ShouldReturnOnlyThe
CallersUnlockedDefinitions`, `ListRelics_ShouldReturnOnlyTheCallersOwned
Instances`, `…ForAPlayerWithNoUnlocks/OwningNothing_ShouldReturnAnEmptyCollection`,
`ListRelics_ShouldReturnTwoElementsForTwoInstancesOfOneDefinition`,
`ListRelics_ShouldResolveNamesInOneBulkRead`), as are the §5.5/§5.6 cross-route
assertions. No existing assertion was loosened.

### 5.2 Client

New: `tests/ContentEffectFormat.test.ts` — 22 tests:

```text
Card   identity + magnitude + interpretation for every provisioned shape
       percentage-point unit; multi-effect order; Burn duration / Crit scope
       Undetermined without an invented magnitude; unknown identity verbatim
       unknown valueType verbatim beside its magnitude
       element with no identity omitted; absent/non-array/unusable member → ''
       NaN / Infinity / null / undefined / non-numeric never reach the text
Relic  trigger + condition + effect for a provisioned Relic
       nullable condition omits the segment; all three documented forms
       unknown form verbatim, never an invented threshold
       target + lifetime; Undetermined; unknown tokens verbatim
       element with no identity omitted; absent/non-array/unusable member → ''
       NaN / null / undefined never reach the text
Scope  no per-id content table, no hardcoded magnitude, no Math.*,
       no cost/affordability/legality term (asserted against the module source)
```

Updated: `tests/CollectionService.test.ts` (31 tests)

```text
the §5.3/§5.4 fixtures now carry the authoritative members and match the actual
serialized response (a Card = the four members, a Relic = the five)
added   effect-specific extra members and their absence (Burn duration,
        Crit scope, Undetermined with no value)
added   a Card carries no cost/affordability/legality member
added   a null Relic condition is the contract's "no condition", not an
        unsupported or absent member
added   an Undetermined Relic effect has no value member
```

Updated: `tests/LobbyScene.test.ts` (92 tests)

```text
the starter fixtures now carry the responses' own effectDefinition / trigger /
condition; a Card and a Relic factory were added
added   states what each Card changes
added   states what each Relic changes and when (trigger + condition + effect)
added   omits the condition segment for a Relic that declares none
added   renders unknown content tokens verbatim rather than guessing
added   draws no content line when the response carries no usable rule
added   treats a row's content line as part of that option (selection intact)
added   keeps every content line inside its own row (y + 15, ≥13 px clear of
        the next row)
added   keeps the last collection row clear of the Boss band
added   holds no client-side Card or Relic content catalog (architectural block)
```

Updated: `tests/CollectionViewerScene.test.ts` (60 tests)

```text
added   the Card panel renders the effect through the shared formatter
added   a multi-effect Card renders every element without merging them
added   an unknown effect token renders verbatim
added   the effect line is omitted rather than shown empty
added   the Relic panel renders trigger + condition + effect
added   a Relic with no condition renders without inventing one
added   holds no client-side Card or Relic content catalog
```

Updated: `tests/RuntimeBoundaries.test.ts` (54 tests) — the runtime's forbidden
cost terms were **extended** (`PowerCost`, `powerCost`, `CostFrom`, `costFrom`)
rather than weakened, and the existing `EffectiveCardCost` / `CardCostModifier` /
`IsAffordable` / `CanAfford` set is unchanged.

`LobbyScene.test.ts`'s "no client-authoritative gameplay" term scan
(`damage`/`cascade`/`combo`/`power`/…) was **not** weakened: the new vocabulary
lives in the separate presentation module, and the scene only calls it.

### 5.3 Results

```text
npx tsc --noEmit                      PASS (no output)
npx vitest run                        PASS — 21 files / 857 tests
npm run build                         PASS — tsc + vite build
git diff --check                      PASS (exit 0)
dotnet test src/backend/GameServer.sln  PASS — 2 931 tests
   Domain 1 558 · Infrastructure 412 · Api 334 · Application 627
```

---

## 6. Browser E2E (real backend + PostgreSQL + Redis + SignalR stack)

Stack: PostgreSQL 17 on `localhost:5433`, Redis 8 on `localhost:6379`, the real
API on `http://localhost:5000` (Development, `DevelopmentAuthentication__Enabled
=true`, real user-secrets signing key), the Vite dev server on
`http://localhost:5173`, headless Edge over CDP.

### 6.1 `npm run verify:e2e:smoke` — PASS, 138 checks × 2 runs, 0 failures

```text
ALL SMOKE TEST RUNS PASSED CLEANLY (NO FLAKINESS)
```

The journey the task requires is covered end to end: Login → Main Menu → Lobby →
loadout selection → START BATTLE → battle → Result → PLAY AGAIN (loadout
preserved and editable) → battle 2 → MAIN MENU, plus Collection reachability and
the §9 safety gates (`zeroUncaughtExceptions`, `zeroFatalConsoleErrors`,
`zeroUnexpectedApiResponses`, `zeroDiscordDependencies`).

New in phase 4 (TASK-212A-1's own assertions):

```text
PASS  phase4.cardRowsStateWhatTheyChange
        ["Heal 20% Max HP","Shield 20% Max HP","Power 25"]
PASS  phase4.relicRowsStateTriggerConditionAndEffect
        ["OnMatchCount MatchCountAtLeast 3 | ATK 5% (Pet, Battle)",
         "OnCombo ComboAtLeast 3 | Crit 10 percentage points (Pet, NextAttack)",
         "OnMatchCount MatchCountAtLeast 4 | Power 10 (Pet, Immediate)"]
PASS  phase4.contentLinesSitInsideTheirOwnRow
        x=422 y=123/153/183 (Cards) · x=794 y=123/153/183 (Relics):
        each content line is exactly 15 px below a row's own line
PASS  phase4.contentLinesStayInsideTheSafeArea
        widest right edge = 1243 ≤ 1256; widest bottom = 196 ≤ 696
PASS  phase4.contentIsNotAClientSideCatalog
        no un-delivered content identity is on screen
PASS  phase4.renderedContentMatchesTheDeliveredResponse
        every element of every returned Card's effectDefinition and every
        returned Relic's trigger/condition/effect is on screen
```

Existing checks that prove the guards the task requires:

```text
PASS  phase4.loadoutSelected                     (card/relic selection still works)
PASS  phase4.noBossInitiallySelected
PASS  phase4.bossSelected
PASS  phase4b.retryStartedTheBattle / phase4.battleSceneReached
PASS  phase7.victoryOutcomeRendered              (battle unaffected)
PASS  phase7.resultSceneReached                  (result unaffected)
PASS  phase8.preservedLoadoutRestored            (PLAY AGAIN keeps the loadout)
PASS  phase8.preservedLoadoutIsEditable
PASS  phase3b.battleHistoryOpened / phase3c.lobbyOffersAnExitAndTheStart
PASS  phase3c.lobbyControlsFitTheFrameWithoutOverlap
PASS  safety.zeroUncaughtExceptions              []
PASS  safety.zeroFatalConsoleErrors              []
PASS  safety.zeroUnexpectedApiResponses          []
PASS  safety.onlyTheInducedDocumentedRejectionsOccurred
        the three deliberate POST /api/battle/start 400 rejections phase 4b
        induces (PET_NOT_OWNED, with the §6 envelope)
```

No E2E assertion was weakened; five content checks and one geometry check were
added.

### 6.2 `npm run verify:e2e:collection` — PASS, 42 checks × 2 runs, 0 failures

```text
ALL COLLECTION VIEWER RUNS PASSED CLEANLY (NO FLAKINESS)
```

Confirms the modified Collection surface: the Relic detail panel now renders

```text
ITEM DETAIL

Relic: Berserker Core
Changes: OnMatchCount MatchCountAtLeast 3 | ATK 5% (Pet, Battle)
Relic ID: relicinst_2efe77434782469d9d72f03ca29c984d
```

with `phase6.detailInventsNoFields`, `safety.zeroUncaughtExceptions`,
`safety.zeroUnexpectedApiResponses`, and `safety.noBattleApiCalls` all still
passing.

### 6.3 Wire verification against the running stack

A throwaway development account's `GET /api/cards` and `GET /api/relics` bodies
were read directly; both match `API_CONTRACTS.md` §5.3/§5.4's amended examples
member for member (bodies quoted in §2.1/§2.2 above).

### 6.4 Observed limitation (not a defect)

In a **development** build the game mounts two development-only React
diagnostics (`StatusOverlay`, `ViewportDebugOverlay`, gated by
`import.meta.env.DEV` — `App.tsx` states neither is mounted in a normal player
presentation). The Runtime Status card sits over the surface's top-right corner
and therefore covers the tail of the longest Relic content line in development
screenshots. The game's own presentation does not clip it — the measured right
edge is 1243 against a safe-area edge of 1256 — and neither overlay exists in a
production bundle. No game-UI change was made for a diagnostic that is not part
of the player-facing frame.

---

## 7. Files changed

```text
docs/02-technical/API_CONTRACTS.md                                (amended)
   §5.3 GET /api/cards, §5.4 GET /api/relics, version preamble 1.18

src/backend/GameServer.Application/Collection/CardCollectionItem.cs        (read model)
src/backend/GameServer.Application/Collection/RelicCollectionItem.cs       (read model)
src/backend/GameServer.Application/Collection/CollectionQueryService.cs    (projection)
src/backend/GameServer.Api/Controllers/CollectionResponses.cs              (response shapes)
src/backend/GameServer.Api/Controllers/CollectionController.cs             (doc comments)

src/frontend/client/src/services/api/CollectionModels.ts                   (client models)
src/frontend/client/src/game/presentation/ContentEffectFormat.ts           (new: formatter)
src/frontend/client/src/game/scenes/LobbyScene.ts                          (Card/Relic rows)
src/frontend/client/src/game/scenes/CollectionViewerScene.ts               (detail panels)

tests/backend/GameServer.Api.Tests/CollectionEndpointTests.cs
tests/backend/GameServer.Application.Tests/CollectionQueryServiceTests.cs
src/frontend/client/tests/ContentEffectFormat.test.ts                      (new)
src/frontend/client/tests/CollectionService.test.ts
src/frontend/client/tests/LobbyScene.test.ts
src/frontend/client/tests/CollectionViewerScene.test.ts
src/frontend/client/tests/RuntimeBoundaries.test.ts
src/frontend/client/scripts/standalone-web-smoke.mjs                       (E2E checks)

tasks/completed/TASK-212A-1-card-relic-content-projection.md               (this record)
```

**Not touched** (verified by `git status`): `ApiService.ts`, `GameRuntime.ts`,
`GameRuntimeEvents.ts`, `BattleScene.ts`, `ResultScene.ts`, `MainMenuScene.ts`,
`BattleEventPresenter.ts`, `SignalRService.ts`, `BattleHub.cs`, every migration,
`DATABASE.md`, `SIGNALR_PROTOCOL.md`, `GAME_STATE.md`, `GAME_EVENTS.md`,
`REDIS_STATE.md`, every game-rule document, and every file the pre-existing
working tree already had modified.

---

## 8. Remaining boundaries and gaps

```text
CARRIED FORWARD (owned elsewhere, not resolved here)

1. TASK-212B — Card cost and affordability. §5.3 still exposes no `powerCost`:
   the composed value is EffectiveCardCost (CARD_RULES.md §3.6) and the realtime
   member is SIGNALR_PROTOCOL.md §4 item 15's. TASK-212B must still reconcile
   §4 item 15 with SIGNALR_PROTOCOL.md:1099 ("a client that must show the spent
   Cost reads the Card's definition"), as TASK-212A §8 hands forward.

2. TASK-213 — Content reachability. The starter grant is 3 Basic Cards and 3
   Relics against exactly-3 / 3–5 requirements, so today the loadout is a
   permutation: comprehension improved, but no build decision changed. Membership
   semantics were deliberately not stretched to compensate.

3. TASK-219 — Signature Skill. A PetSkill CardDefinition is still not an unlock
   row, so the newly exposed Card `effectDefinition` carries no Signature Skill
   content, and `CardCast` / `PetSkillCast` are untouched. The widened member
   shape would accommodate a PetSkill definition if TASK-219 chooses to expose
   one; this task neither performs nor prejudges that.

4. TASK-218 — in-battle Relic trigger callout. `RelicTriggered` keeps its
   `{ type, relicId }` shape; nothing new reaches the SignalR wire.

5. Diagnostic overlay overlap (§6.4) — development-only, reported not fixed.

6. TASK-217 — documentation/cross-reference sweep. This task added one
   member-list restatement per widened section; the drift TASK-212A §8 recorded
   (MVP_SCOPE.md's `OnMatch` claim vs RelicFiringPoint.cs; the stale "not
   implemented" comments in CardDefinition.cs / RelicDefinition.cs) is reported
   there and remains unaddressed here.

7. Presentation length ceiling. The widest *provisioned* Relic line measures
   1243 px against a 1256 px safe-area edge. A future rule substantially longer
   than today's content will word-wrap inside its column, which is the
   documented fail-safe; it would grow the row's own line box rather than cross
   into the neighbouring column. Not a defect for any provisioned content.

NEWLY OBSERVED (reported, no owner, not fixed)

D1. The Lobby still renders whatever GET /api/cards returns without filtering by
    category (TASK-212A §8 D1) — latent, because no PetSkill row is ever an
    unlock row. Unchanged.
D2. tests/backend/GameServer.Api.Tests/TestProvisionedContent.cs remains a
    fixture rather than a content source, and it still seeds Pet Skill Card
    values that differ from the Domain/provisioning values (Tidal Barrier 30/30,
    Iron Fang 150/+30) and a Relic Trigger of "seeded". Only `card-heal`,
    `card-shield`, `card-power-charge` are ever unlock rows in these tests, and
    those three match the provisioning migration exactly, so no assertion in
    this task rests on the divergent rows. The audit's finding is confirmed, not
    resolved: the file documents itself as fixture content and a future
    implementer must not read it as authored data.
D3. The player-visible content is token-plus-magnitude (`Heal 20% Max HP`,
    `OnMatchCount MatchCountAtLeast 3 | ATK 5% (Pet, Battle)`), not a sentence.
    No document authors Card/Relic prose (TASK-082 R2-7 was superseded for both
    members), so this is the documented maximum; human phrasing would need a
    design decision and a localization owner.
```

---

## 9. Final Report

```text
TASK-212A-1 COMPLETE

Contract:   API_CONTRACTS.md §5.3 + effectDefinition
            API_CONTRACTS.md §5.4 + trigger + condition (nullable) + effectDefinition
            additive; membership, routes, statuses, ordering, auth unchanged

Backend:    Application read models + projection + Api response records only.
            No repository, query, Domain, schema, index, or migration change.

Client:     CollectionModels + one shared formatter + two content surfaces
            (Lobby loadout rows, Collection viewer detail panels).
            ApiService / GameRuntime / SignalR untouched.

Tests:      tsc PASS · vitest 21 files / 857 tests PASS · npm run build PASS ·
            dotnet test 2 931 tests PASS · git diff --check PASS

E2E:        verify:e2e:smoke      138 checks × 2 runs, 0 failures
            verify:e2e:collection  42 checks × 2 runs, 0 failures

Decision:   implemented as approved; TASK-212B / 213 / 217 / 218 / 219 untouched
```
